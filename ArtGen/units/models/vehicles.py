"""Vehicles: tracked armor and the shared base of every vehicle archetype
(siege engines, guns, aircraft and missiles build on VehicleBase).

Vehicles are rigid parts moved per frame: hulls rock and recoil, treads and
wheels turn (track links run around the belt, wheels carry hub bolts that
show the rotation), turrets scan, barrels recoil with a muzzle flash and
smoke. Deaths blow the vehicle up (fireball, turret thrown, the hull charred
and smoking). Effects come from models/vfx.py; parts from models/vparts.py.

Catalog parameters (see catalog/vehicles.py):
    archetype   'armor' (this module), 'siege'/'gun' (models/siege.py),
                'aircraft'/'helicopter' (models/aircraft.py), 'missile'
    model       which builder (e.g. 'sherman', 'panzer', 'abrams', 'bradley',
                'mlrs', 'sam')
    colors      {material: '#rrggbb'} overrides of the builder's paints
    tint        materials drawn in the civ color
    fire        normalized time of the shot in ATTACK1 (default: measured
                from the original's muzzle flash)
"""
import math

import numpy as np

import mesh as M
import rig
import render as R
from models import archetype
from models import vparts as VP
from models import vfx as FX

SIN_E, COS_E = R.SIN_E, R.COS_E


# ------------------------------------------------------------ measuring the original

_PROFILE_CACHE = {}


def original_profile(fl):
    """Per-frame measurements of an original flic, averaged over its
    directions: bright (count of near-white/yellow flash pixels), opaque
    (count), and the per direction bounding boxes and bright centroids."""
    key = fl.path
    if key in _PROFILE_CACHE:
        return _PROFILE_CACHE[key]
    import flic as F
    nd, nf = fl.n_anims, fl.n_frames
    bright = np.zeros((nd, nf)); opaque = np.zeros((nd, nf))
    bcent = np.full((nd, nf, 2), np.nan)
    bbox = np.zeros((nd, nf, 4))
    for d in range(nd):
        for f in range(nf):
            idx = fl.frames[d, f]
            op = (idx != 255) & ~F.shadow_mask(idx)
            col = fl.palette[idx].astype(np.float32)
            lum = col.mean(-1)
            br = op & (lum > 200) & (col[..., 2] < col[..., 0] + 10) & ~F.tint_mask(idx)
            bright[d, f] = br.sum()
            opaque[d, f] = op.sum()
            if br.sum() > 3:
                ys, xs = np.nonzero(br)
                bcent[d, f] = (xs.mean() + fl.offset_left, ys.mean() + fl.offset_top)
            ys, xs = np.nonzero(op)
            if len(ys):
                bbox[d, f] = (xs.min() + fl.offset_left, ys.min() + fl.offset_top,
                              xs.max() + fl.offset_left, ys.max() + fl.offset_top)
    out = {'bright': bright, 'opaque': opaque, 'bcent': bcent, 'bbox': bbox}
    _PROFILE_CACHE[key] = out
    return out


def flash_time(ctx, default=0.2):
    """Normalized time of the first big muzzle flash in the original."""
    if ctx is None:
        return default
    p = original_profile(ctx.flic)
    b = p['bright'].mean(0)
    if b.max() < 4:
        return default
    f = int(np.nonzero(b >= 0.5 * b.max())[0][0])
    return f / max(ctx.n - 1, 1)


# ------------------------------------------------------------ base

class VehicleBase:
    """Calibration (the same fit the framework does, so effects can be
    placed in the original's pixel space), effects materials, and timing."""

    loop_actions = ('DEFAULT', 'RUN', 'WALK', 'FIDGET', 'FORTIFY')

    def __init__(self, unit, params):
        self.unit = unit
        self.p = params
        self._cal = None
        self._calibrating = False
        self.mats = M.Materials()

    # -- materials
    def paint(self, table, metal=()):
        colors = dict(table)
        colors.update(self.p.get('colors', {}))
        VP.add_mats(self.mats, colors, tint=set(self.p.get('tint', [])), metal=metal)
        FX.add_fx_mats(self.mats)

    def charred(self):
        """Material remap {id: charred id} for wrecks (darkened copies)."""
        if hasattr(self, '_char'):
            return self._char
        out = {}
        for i, name in enumerate(list(self.mats.names)):
            if name.startswith('fx_') or name.startswith('smoke') or name.endswith('_ch') or name in FX.FX_COLORS:
                continue
            col = np.array(self.mats.color[i], np.float32)
            if self.mats.tint[i]:
                col = np.array((120, 120, 120), np.float32)
            dark = col * 0.58 + np.array([12, 10, 8])
            out[i] = self.mats.add(name + '_ch', tuple(int(v) for v in dark), spec=0.02)
        self._char = out
        return out

    def lit(self):
        """Material remap {id: id} for parts lit up by a muzzle flash or a
        blast (lighter, warmer copies)."""
        if hasattr(self, '_lit'):
            return self._lit
        out = {}
        for i, name in enumerate(list(self.mats.names)):
            if name.startswith('fx_') or name.startswith('smoke') or name.endswith('_ch') or name in FX.FX_COLORS:
                continue
            col = np.array(self.mats.color[i], np.float32)
            warm = col * 0.6 + np.array([255, 236, 180]) * 0.4
            out[i] = self.mats.add(name + '_lit', tuple(int(v) for v in warm), tint=self.mats.tint[i], spec=0.05)
        self._lit = out
        return out

    # -- fitting to the original
    def autofit(self, action='DEFAULT', frame=0, keep_anchor=False):
        """Sets the catalog entry's 'scale' and 'anchor' (which the framework
        reads after building the model) so the model's projected bounding box
        matches the original's in every direction (on average): vehicles are
        long, so their lowest pixel is not under their center, and their
        height alone is a poor measure of size. 'size' (catalog) scales the
        result. Skipped when the catalog sets both scale and anchor."""
        import framework as FW
        import flic as F
        p = self.p
        if 'anchor' in p and 'scale' in p and not p.get('_auto'):
            return
        fl = F.load_unit_flic(self.unit, F.unit_flics(self.unit)[action])
        fits, cents = [], []
        for d in range(min(8, fl.n_anims)):
            idx = fl.frames[d, frame]
            op = (idx != 255) & ~F.shadow_mask(idx)
            ys, xs = np.nonzero(op)
            if len(ys) == 0:
                continue
            ob = (xs.min() + fl.offset_left, ys.min() + fl.offset_top, xs.max() + fl.offset_left + 1,
                  ys.max() + fl.offset_top + 1)
            mesh = self.frame(action, frame, fl.n_frames, None).transformed(R.facing_matrix(d))
            V = mesh.V.reshape(-1, 3)
            sx = V[:, 0]
            sy = -(V[:, 1] * SIN_E + V[:, 2] * COS_E)
            mw, mh = sx.max() - sx.min(), sy.max() - sy.min()
            ow, oh = ob[2] - ob[0] - 0.8, ob[3] - ob[1] - 0.8
            fits.append(0.5 * (ow / max(mw, 1e-3) + oh / max(mh, 1e-3)))
            cents.append(((ob[0] + ob[2]) / 2, (ob[1] + ob[3]) / 2, (sx.max() + sx.min()) / 2, (sy.max() + sy.min()) / 2))
        ppm1 = float(np.median(fits)) * p.get('size', 1.0)
        ax = float(np.median([c[0] - c[2] * ppm1 for c in cents]))
        ay = float(np.median([c[1] - c[3] * ppm1 for c in cents]))
        oax, oay, oh_f = FW.original_stance(self.unit)
        mh_f = FW.model_extent(self, FW.BASE_PPM * FW.SCALE)
        base = FW.BASE_PPM * FW.SCALE * (oh_f - 0.5) / max(mh_f, 1e-3)
        p['scale'] = ppm1 * FW.SCALE / base
        if not keep_anchor:
            p['anchor'] = (ax, ay)
        p['_auto'] = True
        self._cal = None

    # -- calibration
    def cal(self):
        if self._cal is None:
            import framework as FW
            ax, ay, oh = FW.original_stance(self.unit)
            if 'anchor' in self.p:
                ax, ay = self.p['anchor']
            self._calibrating = True
            mh = FW.model_extent(self, FW.BASE_PPM * FW.SCALE)
            self._calibrating = False
            ppm = FW.BASE_PPM * FW.SCALE * (oh - 0.5) / max(mh, 1e-3) * self.p.get('scale', 1.0)
            self._cal = {'ppm': ppm, 'ax': ax, 'ay': ay, 'S': FW.SCALE}
        return self._cal

    def px(self, meters):
        """Meters -> original (1x) pixels."""
        c = self.cal()
        return meters * c['ppm'] / c['S']

    def meters(self, pixels):
        c = self.cal()
        return pixels * c['S'] / c['ppm']

    def to_world(self, X, Y, z=0.0):
        """World point at height z seen at pixel (X, Y) of the original's
        240x240 unit space."""
        c = self.cal()
        k = c['S'] / c['ppm']
        x = (X - c['ax']) * k
        y = ((c['ay'] - Y) * k - z * COS_E) / SIN_E
        return np.array([x, y, z], np.float32)

    def loops(self, action):
        return action in self.loop_actions

    def times(self, action, f, n):
        return rig.frame_times(n, self.loops(action))[f]


def model_to_world(p, d):
    """A model-space point turned to flic direction d (world space)."""
    F = R.facing_matrix(d)
    return (F[:3, :3] @ np.asarray(p, np.float32)).astype(np.float32)


# ------------------------------------------------------------ tracks

def _hull2d(points):
    pts = sorted(set(map(tuple, np.round(points, 5))))
    if len(pts) < 3:
        return np.array(pts)
    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lower, upper = [], []
    for p in pts:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], p) <= 0:
            lower.pop()
        lower.append(p)
    for p in reversed(pts):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], p) <= 0:
            upper.pop()
        upper.append(p)
    return np.array(lower[:-1] + upper[:-1], np.float32)


class Track:
    """One side's track: the belt is the convex hull of its wheels (y, z, r)
    seen from the side; links run around it with `phase` (meters)."""

    def __init__(self, wheels, width, x, link=0.2, visible_wheels=True, top=None):
        self.wheels = wheels
        self.width = width
        self.x = x
        self.link = link
        self.visible = visible_wheels
        pts = []
        for y, z, r in wheels + (top or []):
            for a in np.linspace(0, 2 * math.pi, 24, endpoint=False):
                pts.append((y + r * math.cos(a), z + r * math.sin(a)))
        self.poly = _hull2d(np.array(pts))      # counter-clockwise in (y, z)
        seg = np.roll(self.poly, -1, 0) - self.poly
        self.seglen = np.linalg.norm(seg, axis=1)
        self.L = float(self.seglen.sum())

    def point(self, s):
        """Position (y, z) and tangent at arc length s (counter-clockwise in
        (y, z): along the bottom it runs toward +y)."""
        s = s % self.L
        acc = np.concatenate([[0], np.cumsum(self.seglen)])
        i = int(np.searchsorted(acc, s, side='right') - 1)
        i = min(i, len(self.poly) - 1)
        u = (s - acc[i]) / max(self.seglen[i], 1e-9)
        a, b = self.poly[i], self.poly[(i + 1) % len(self.poly)]
        return a + (b - a) * u, (b - a) / max(self.seglen[i], 1e-9)

    def mesh(self, mats, phase, belt='track', link='track_link', wheel='wheel', hub='wheel_hub', side=1):
        """phase: meters the vehicle has moved forward (the bottom of the
        belt runs backward relative to the hull)."""
        parts = [VP.side_prism([tuple(p) for p in self.poly], self.width, mats[belt], x0=self.x)]
        n = max(8, int(self.L / self.link))
        step = self.L / n
        for k in range(n):
            # the bottom moves toward -y as the vehicle advances: s decreases
            (py, pz), tg = self.point(k * step - phase)
            ang = math.degrees(math.atan2(tg[1], tg[0]))
            # the outward normal of a counter-clockwise loop is (tg.z, -tg.y)
            nrm = np.array([tg[1], -tg[0]])
            c = np.array([py, pz]) + nrm * 0.025
            parts.append(M.box(self.width * 1.06, step * 0.5, 0.07, mats[link]).transformed(
                M.translate(self.x, c[0], c[1]) @ M.rot_x(ang)))
        if self.visible:
            xo = self.x + side * self.width * 0.5
            for y, z, r in self.wheels:
                ang = -math.degrees(phase / r)
                parts.append(VP.disc_wheel(r * 0.92, self.width * 0.35, mats[wheel], mats[hub], angle=ang,
                                           center=(xo, y, z), bolts=5))
        return M.Mesh.concat(parts)


def star(r, mat, thick=0.012, points=5, inner=0.42):
    pts = []
    for i in range(points * 2):
        a = math.pi / 2 + i * math.pi / points
        rr = r if i % 2 == 0 else r * inner
        pts.append((rr * math.cos(a), rr * math.sin(a)))
    return VP.prism(pts, thick, mat)


def decal_on_side(mesh2d_xy, side, y, z, x_surface, tilt=0.0):
    """Places a flat decal (made in the x-y plane, facing +z) on the hull
    side at x = x_surface (side = +1 right, -1 left), facing outward."""
    T = M.translate(side * x_surface, y, z) @ M.rot_y(side * 90 + tilt * side) @ M.rot_z(90 if side > 0 else -90)
    return mesh2d_xy.transformed(T)


# ------------------------------------------------------------ armor builders
#
# Each returns a dict: hull (mesh), tracks [Track, Track], turret (mesh, local to
# the turret ring at its origin), turret_pos (model space), gun (mesh along +y
# from its trunnion at the origin), gun_pos (in turret space), muzzle (gun
# space), plus extras: 'scan' (turret scan amplitude, degrees), 'recoil' (m).

def _box_section(w_bot, w_top, z0, z1, chamfer=0.0):
    """A hull section polygon (x, z), counter-clockwise from the front."""
    c = chamfer
    return [(-w_bot, z0), (w_bot, z0), (w_top + c * 0.0, z1 - c), (w_top - c, z1), (-w_top + c, z1), (-w_top, z1 - c)]


def build_sherman(V):
    """The WWII medium tank (a Sherman): short high hull with sponsons over
    the tracks, sloped glacis and rounded nose, a big rounded cast turret
    with a civ-colored band, civ-colored stars on the sides and front."""
    m = V.mats
    ys = 0.8   # Civ3's vehicles are stubby
    hull_sec = lambda zt, wt: [(-0.80, 0.42), (0.80, 0.42), (0.80, 1.12), (1.30, 1.12), (wt, zt), (-wt, zt), (-1.30, 1.12), (-0.80, 1.12)]
    hull = VP.sweep([
        (-2.75 * ys, hull_sec(1.55, 1.05)),
        (-2.55 * ys, hull_sec(1.72, 1.12)),
        (1.15 * ys, hull_sec(1.75, 1.12)),
        (2.55 * ys, hull_sec(1.18, 1.10)),
        (2.95 * ys, [(-0.80, 0.55), (0.80, 0.55), (0.80, 0.95), (1.15, 0.95), (1.0, 1.1), (-1.0, 1.1), (-1.15, 0.95), (-0.80, 0.95)]),
    ], m['paint'])
    nose = VP.axle_x(0.36, 1.6, m['paint'], seg=14, center=(0, 2.58 * ys, 0.74))
    deck = M.box(1.5, 1.0, 0.06, m['paint_dark'], center=(0, -1.6, 1.75))
    hatches = [M.box(0.42, 0.45, 0.08, m['paint_dark'], center=(sx * 0.45, 1.45, 1.62)) for sx in (-1, 1)]
    mg = VP.tube((0.45, 1.75, 1.45), (0.45, 2.3, 1.32), 0.035, 0.03, m['steel'], seg=6)
    lights = [VP.axle_x(0.07, 0.06, m['steel'], center=(sx * 0.9, 2.3, 1.05)) for sx in (-1, 1)]
    tools = [M.box(0.05, 1.4, 0.06, m['wood'], center=(sx * 1.31, -0.9, 1.3)) for sx in (-1, 1)]
    stars = []
    for side in (-1, 1):
        stars.append(decal_on_side(star(0.34, m['decal']), side, 0.9, 1.45, 1.27))
        stars.append(decal_on_side(star(0.30, m['decal']), side, -1.45, 1.45, 1.27))
    stars.append(star(0.28, m['decal']).transformed(M.translate(0.62, 2.05, 1.32) @ M.rot_x(-60)))
    stars.append(star(0.28, m['decal']).transformed(M.translate(-0.62, 2.05, 1.32) @ M.rot_x(-60)))
    hull = M.Mesh.concat([hull, nose, deck, mg] + hatches + lights + tools + stars)
    wheels = [(-2.55 * ys, 0.42, 0.34)] + [(y * ys, 0.30, 0.29) for y in (-1.95, -1.3, -0.45, 0.2, 1.05, 1.7)] + [(2.62 * ys, 0.78, 0.30)]
    tracks = [Track(wheels, 0.52, sx * 1.04, link=0.19, top=[(0.0, 0.92, 0.12)]) for sx in (-1, 1)]
    # vertical-volute bogies: a bracket and spring housing over each pair of road wheels
    bog = []
    for sx in (-1, 1):
        for y0, y1 in ((-1.95, -1.3), (-0.45, 0.2), (1.05, 1.7)):
            yc = (y0 + y1) / 2 * ys
            bog.append(M.box(0.1, 0.62, 0.22, m['paint_dark'], center=(sx * 1.36, yc, 0.4)))
            bog.append(M.box(0.14, 0.2, 0.42, m['paint'], center=(sx * 1.36, yc, 0.68)))
            bog.append(VP.axle_x(0.07, 0.2, m['wheel_hub'], center=(sx * 1.34, yc, 0.92)))
    hull = hull + M.Mesh.concat(bog)
    # big cast turret: rounded, slightly longer than wide, with a civ-colored band
    prof = [(0.0, 1.0), (0.12, 1.05), (0.34, 1.04), (0.55, 0.96), (0.72, 0.78), (0.84, 0.48), (0.88, 0.0)]
    S = M.scale(1.0, 1.1, 1.0)
    tur = M.lathe(prof, m['paint'], seg=22).transformed(S)
    band = M.lathe([(0.16, 1.065), (0.42, 1.035)], m['band'], seg=22).transformed(S)
    cup = M.cylinder(0.27, 0.25, 0.16, m['paint'], seg=12, z0=0.82).transformed(M.translate(-0.32, -0.25, 0))
    cup_top = M.cylinder(0.26, 0.18, 0.06, m['paint_dark'], seg=12, z0=0.98).transformed(M.translate(-0.32, -0.25, 0))
    hatch = M.box(0.38, 0.32, 0.06, m['paint'], center=(0.32, -0.28, 0.85))
    mantlet = M.box(0.66, 0.24, 0.46, m['paint'], center=(0, 1.1, 0.42), bevel=0.05)
    bustle = M.box(1.1, 0.4, 0.42, m['paint'], center=(0, -1.1, 0.36), bevel=0.08)
    aa = VP.tube((-0.32, -0.05, 0.98), (-0.32, 0.45, 1.15), 0.03, 0.025, m['steel'], seg=6)
    turret = M.Mesh.concat([tur, band, cup, cup_top, hatch, mantlet, bustle, aa])
    gun = M.Mesh.concat([VP.tube((0, -0.1, 0), (0, 2.0, 0), 0.09, 0.075, m['paint'], seg=10),
                         VP.tube((0, 1.86, 0), (0, 2.06, 0), 0.1, 0.1, m['paint_dark'], seg=10)])
    return dict(hull=hull, tracks=tracks, turret=turret, turret_pos=(0, 0.1, 1.73), gun=gun,
                gun_pos=(0, 1.18, 0.42), muzzle=(0, 2.1, 0), recoil=0.35, scan=35)


def build_panzer(V):
    """A late-war medium tank in olive camouflage (the Civ3 Panzer looks
    like a Patton): long hull with side skirts, a rounded cast turret with a
    long gun, civ-colored stripes on the skirts."""
    m = V.mats
    sec = lambda zt, wt: [(-0.95, 0.45), (0.95, 0.45), (0.95, 0.92), (1.42, 0.92), (wt, zt), (-wt, zt), (-1.42, 0.92), (-0.95, 0.92)]
    hull = VP.sweep([
        (-3.0, sec(1.45, 1.25)),
        (-2.8, sec(1.62, 1.3)),
        (1.6, sec(1.66, 1.3)),
        (2.8, sec(1.12, 1.26)),
        (3.15, [(-0.95, 0.6), (0.95, 0.6), (0.95, 0.85), (1.35, 0.85), (1.2, 1.0), (-1.2, 1.0), (-1.35, 0.85), (-0.95, 0.85)]),
    ], m['paint'])
    fenders = [M.box(0.08, 6.0, 0.08, m['paint'], center=(sx * 1.44, 0.0, 0.92)) for sx in (-1, 1)]
    hull = VP.camo(M.Mesh.concat([hull] + fenders), m['paint'],
                   [(0.25, m['paint_dark']), (0.72, m['paint_brown'])], max_edge=0.15, seed=11, scale=0.8)
    stripes = []
    for side in (-1, 1):
        for y in (-2.2, 1.9):
            stripes.append(M.box(0.05, 0.42, 0.5, m['decal'], center=(side * 1.45, y, 0.8)))
    lights = [VP.axle_x(0.08, 0.08, m['steel'], center=(sx * 1.0, 3.0, 1.05)) for sx in (-1, 1)]
    hull = M.Mesh.concat([hull] + stripes + lights)
    wheels = [(-2.75, 0.45, 0.32)] + [(y, 0.32, 0.31) for y in (-2.1, -1.4, -0.7, 0.0, 0.7, 1.4, 2.0)] + [(2.8, 0.62, 0.3)]
    tracks = [Track(wheels, 0.55, sx * 1.15, link=0.2, top=[(0.0, 0.8, 0.12)]) for sx in (-1, 1)]
    prof = [(0.0, 1.1), (0.12, 1.14), (0.38, 1.08), (0.62, 0.94), (0.82, 0.68), (0.94, 0.32), (0.97, 0.0)]
    tur = M.lathe(prof, m['paint'], seg=22).transformed(M.scale(1.0, 1.25, 1.0))
    tur = VP.camo(tur, m['paint'], [(0.25, m['paint_dark']), (0.72, m['paint_brown'])], max_edge=0.15, seed=12, scale=0.8)
    cup = M.cylinder(0.27, 0.25, 0.2, m['paint'], seg=12, z0=0.86).transformed(M.translate(0.38, -0.3, 0))
    cup_top = M.cylinder(0.26, 0.2, 0.06, m['paint_dark'], seg=12, z0=1.06).transformed(M.translate(0.38, -0.3, 0))
    mantlet = M.box(0.72, 0.3, 0.5, m['paint_dark'], center=(0, 1.36, 0.45), bevel=0.08)
    bustle = M.box(1.4, 0.55, 0.42, m['paint'], center=(0, -1.35, 0.32), bevel=0.1)
    basket = M.box(1.5, 0.25, 0.2, m['paint_dark'], center=(0, -1.72, 0.4))
    turret = M.Mesh.concat([tur, cup, cup_top, mantlet, bustle, basket])
    gun = M.Mesh.concat([VP.tube((0, -0.1, 0), (0, 3.0, 0), 0.09, 0.075, m['paint'], seg=10),
                         VP.tube((0, 1.6, 0), (0, 1.95, 0), 0.12, 0.12, m['paint_dark'], seg=10),
                         VP.tube((0, 2.92, 0), (0, 3.15, 0), 0.11, 0.11, m['paint_dark'], seg=10)])
    return dict(hull=hull, tracks=tracks, turret=turret, turret_pos=(0, 0.1, 1.6), gun=gun,
                gun_pos=(0, 1.45, 0.45), muzzle=(0, 3.2, 0), recoil=0.4, scan=40)


def build_abrams(V):
    """Modern main battle tank: low angular hull with side skirts, a wide
    flat faceted turret, long smoothbore, civ-colored turret sides and
    skirt flashes."""
    m = V.mats
    sec = lambda zt, wt, zb=0.45: [(-1.0, zb), (1.0, zb), (1.0, 0.55), (1.55, 0.55), (1.55, 0.98), (wt, zt), (-wt, zt), (-1.55, 0.98), (-1.55, 0.55), (-1.0, 0.55)]
    hull = VP.sweep([
        (-3.3, sec(1.38, 1.5)),
        (1.9, sec(1.42, 1.5)),
        (3.1, sec(1.25, 1.5)),
        (3.45, [(-1.0, 0.6), (1.0, 0.6), (1.0, 0.62), (1.4, 0.62), (1.4, 0.95), (1.35, 1.05), (-1.35, 1.05), (-1.4, 0.95), (-1.4, 0.62), (-1.0, 0.62)]),
    ], m['paint'])
    flashes = [M.box(0.05, 0.9, 0.3, m['decal'], center=(sx * 1.56, 2.4, 0.8)) for sx in (-1, 1)]
    seams = [M.box(0.05, 0.06, 0.42, m['paint_dark'], center=(sx * 1.565, y, 0.77))
             for sx in (-1, 1) for y in (-2.2, -1.1, 0.0, 1.1)]
    grille = M.box(2.0, 1.1, 0.06, m['paint_dark'], center=(0, -2.55, 1.4))
    hull = VP.camo(hull, m['paint'], [(0.4, m['paint_dark'])], max_edge=0.3, seed=41, scale=0.45)
    hull = M.Mesh.concat([hull, grille] + flashes + seams)
    wheels = [(-3.05, 0.5, 0.3)] + [(y, 0.32, 0.3) for y in (-2.4, -1.65, -0.9, -0.15, 0.6, 1.35, 2.1)] + [(3.05, 0.62, 0.3)]
    tracks = [Track(wheels, 0.6, sx * 1.22, link=0.2, visible_wheels=True) for sx in (-1, 1)]
    # wide faceted turret: wedge front, flat sides
    tsec = lambda w, z0, z1, wt: [(-w, z0), (w, z0), (wt, z1), (-wt, z1)]
    tur = VP.sweep([
        (-1.9, tsec(1.25, 0.0, 0.62, 1.15)),
        (0.9, tsec(1.55, 0.0, 0.68, 1.4)),
        (1.75, tsec(0.75, 0.05, 0.6, 0.65)),
    ], m['paint'])
    tur = VP.camo(tur, m['paint'], [(0.4, m['paint_dark'])], max_edge=0.3, seed=42, scale=0.45)
    tside = [M.box(0.04, 1.6, 0.4, m['decal'], center=(sx * 1.47, -0.3, 0.34)) for sx in (-1, 1)]
    bustle = M.box(2.0, 0.6, 0.45, m['paint_dark'], center=(0, -2.05, 0.35))
    cup = M.cylinder(0.24, 0.22, 0.2, m['paint'], seg=10, z0=0.66).transformed(M.translate(0.55, -0.4, 0))
    mg = VP.tube((0.55, -0.3, 0.92), (0.55, 0.35, 0.92), 0.03, 0.03, m['steel'], seg=6)
    sight = M.box(0.35, 0.35, 0.28, m['paint_dark'], center=(-0.7, 0.2, 0.8))
    hatch = M.cylinder(0.25, 0.25, 0.06, m['paint_dark'], seg=10, z0=0.66).transformed(M.translate(-0.5, -0.6, 0))
    mg2 = VP.tube((-0.5, -0.4, 0.82), (-0.5, 0.3, 0.82), 0.025, 0.025, m['steel'], seg=6)
    sight = sight + hatch + mg2
    ant = [VP.tube((sx * 0.9, -1.8, 0.66), (sx * 0.9, -1.85, 1.75), 0.012, 0.008, m['steel'], seg=4) for sx in (-1, 1)]
    turret = M.Mesh.concat([tur, bustle, cup, mg, sight] + tside + ant)
    gun = M.Mesh.concat([VP.tube((0, -0.2, 0), (0, 3.6, 0), 0.1, 0.085, m['paint'], seg=10),
                         VP.tube((0, 1.6, 0), (0, 2.1, 0), 0.13, 0.13, m['paint'], seg=10),
                         M.box(0.45, 0.4, 0.36, m['paint'], center=(0, 0.0, 0), bevel=0.04)])
    return dict(hull=hull, tracks=tracks, turret=turret, turret_pos=(0, -0.1, 1.38), gun=gun,
                gun_pos=(0, 1.65, 0.36), muzzle=(0, 3.65, 0), recoil=0.4, scan=30)


def build_bradley(V):
    """Infantry fighting vehicle: boxy high hull with a sloped nose, small
    offset turret with a slim autocannon and a missile box, desert camo,
    civ-colored panels on the turret and hull."""
    m = V.mats
    sec = lambda zt, wt: [(-1.05, 0.42), (1.05, 0.42), (1.05, 0.55), (1.5, 0.55), (1.5, zt - 0.25), (wt, zt), (-wt, zt), (-1.5, zt - 0.25), (-1.5, 0.55), (-1.05, 0.55)]
    hull = VP.sweep([
        (-2.95, sec(1.85, 1.35)),
        (1.4, sec(1.85, 1.35)),
        (2.75, sec(1.25, 1.35)),
        (3.1, [(-1.05, 0.55), (1.05, 0.55), (1.05, 0.6), (1.45, 0.6), (1.45, 0.9), (1.3, 1.05), (-1.3, 1.05), (-1.45, 0.9), (-1.45, 0.6), (-1.05, 0.6)]),
    ], m['paint'])
    hull = VP.camo(hull, m['paint'], [(0.0, m['paint_dark']), (0.6, m['paint_brown'])], max_edge=0.22, seed=21, scale=0.5)
    panels = [M.box(0.04, 0.9, 0.35, m['decal'], center=(sx * 1.51, -2.0, 1.25)) for sx in (-1, 1)]
    ramp = M.box(1.6, 0.1, 1.1, m['paint_dark'], center=(0, -2.98, 1.05))
    hull = M.Mesh.concat([hull, ramp] + panels)
    wheels = [(-2.7, 0.48, 0.3)] + [(y, 0.3, 0.29) for y in (-2.05, -1.35, -0.65, 0.05, 0.75, 1.45)] + [(2.6, 0.62, 0.3)]
    tracks = [Track(wheels, 0.55, sx * 1.22, link=0.2, visible_wheels=True, top=[(0.0, 0.7, 0.1)]) for sx in (-1, 1)]
    tsec = lambda w, z1, wt: [(-w, 0.0), (w, 0.0), (wt, z1), (-wt, z1)]
    tur = VP.sweep([(-0.9, tsec(0.85, 0.55, 0.7)), (0.6, tsec(0.85, 0.55, 0.7)), (1.0, tsec(0.6, 0.45, 0.5))], m['paint'])
    tur = VP.camo(tur, m['paint'], [(0.0, m['paint_dark']), (0.6, m['paint_brown'])], max_edge=0.2, seed=22, scale=0.5)
    tow = M.box(0.42, 1.2, 0.36, m['decal'], center=(-1.05, -0.2, 0.42), bevel=0.04)
    sight = M.box(0.3, 0.3, 0.25, m['paint_dark'], center=(0.45, 0.2, 0.68))
    ant = VP.tube((0.6, -0.8, 0.55), (0.62, -0.85, 1.4), 0.012, 0.008, m['steel'], seg=4)
    turret = M.Mesh.concat([tur, tow, sight, ant])
    gun = M.Mesh.concat([VP.tube((0, -0.1, 0), (0, 2.0, 0), 0.05, 0.045, m['steel'], seg=8),
                         M.box(0.3, 0.3, 0.28, m['paint'], center=(0, 0.0, 0), bevel=0.03)])
    return dict(hull=hull, tracks=tracks, turret=turret, turret_pos=(0.25, 0.45, 1.85), gun=gun,
                gun_pos=(0, 1.0, 0.3), muzzle=(0, 2.05, 0), recoil=0.12, scan=40, burst=True)


def build_mlrs(V):
    """Radar Artillery: a tracked rocket launcher (like the MLRS): a cab up
    front and a large launcher box on the back that lifts to fire a ripple
    of rockets; desert sand paint, a civ-colored chevron on the box."""
    m = V.mats
    sec = lambda zt: [(-1.05, 0.45), (1.05, 0.45), (1.05, 0.55), (1.45, 0.55), (1.45, zt), (-1.45, zt), (-1.45, 0.55), (-1.05, 0.55)]
    hull = VP.sweep([(-3.1, sec(1.15)), (2.6, sec(1.15)), (3.05, sec(0.8))], m['paint'])
    cab = VP.sweep([(1.2, [(-1.45, 1.1), (1.45, 1.1), (1.4, 2.25), (-1.4, 2.25)]),
                    (2.6, [(-1.45, 1.1), (1.45, 1.1), (1.35, 2.15), (-1.35, 2.15)]),
                    (3.0, [(-1.45, 1.1), (1.45, 1.1), (1.25, 1.6), (-1.25, 1.6)])], m['paint'])
    wins = [M.box(0.9, 0.04, 0.32, m['glass'], center=(sx * 0.65, 2.71, 1.9)) for sx in (-1, 1)]
    wins += [M.box(0.04, 0.8, 0.3, m['glass'], center=(sx * 1.43, 1.95, 1.85)) for sx in (-1, 1)]
    hull = VP.camo(M.Mesh.concat([hull, cab]), m['paint'], [(0.45, m['paint_dark'])], max_edge=0.25, seed=31, scale=0.5)
    hull = M.Mesh.concat([hull] + wins)
    wheels = [(-2.85, 0.5, 0.3)] + [(y, 0.3, 0.28) for y in (-2.2, -1.5, -0.8, -0.1, 0.6, 1.3, 2.0)] + [(2.75, 0.62, 0.28)]
    tracks = [Track(wheels, 0.55, sx * 1.22, link=0.2, visible_wheels=True, top=[(0.0, 0.72, 0.1)]) for sx in (-1, 1)]
    # launcher: the box pivots at its rear lower edge (turret space origin)
    box = M.box(2.6, 3.9, 1.05, m['paint'], center=(0, 0.0, 0.55), bevel=0.04)
    box = VP.camo(box, m['paint'], [(0.45, m['paint_dark'])], max_edge=0.25, seed=32, scale=0.5)
    # the launch end: a dark face with the light rims of 2x6 tubes (reads as a rocket pod)
    tubes = [M.box(2.4, 0.04, 0.92, m['steel_dark'], center=(0, 1.96, 0.53))]
    for i in range(6):
        for j in range(2):
            tubes.append(VP.axle_x(0.16, 0.05, m['track_link'], seg=10).transformed(
                M.translate(-0.95 + i * 0.38, 1.98, 0.3 + j * 0.45) @ M.rot_z(90)))
            tubes.append(VP.axle_x(0.1, 0.06, m['steel_dark'], seg=8).transformed(
                M.translate(-0.95 + i * 0.38, 1.99, 0.3 + j * 0.45) @ M.rot_z(90)))
    chevron = VP.prism([(-0.55, -0.3), (0.0, 0.35), (0.55, -0.3), (0.35, -0.3), (0.0, 0.1), (-0.35, -0.3)], 0.02,
                       m['decal']).transformed(M.translate(0, -0.35, 1.08))
    chev_side = [decal_on_side(VP.prism([(-0.3, -0.25), (0.0, 0.25), (0.3, -0.25), (0.15, -0.25), (0.0, 0.05), (-0.15, -0.25)], 0.02, m['decal']),
                               sx, -0.05, 0.55, 1.31) for sx in (-1, 1)]
    turret = M.Mesh.concat([box, chevron] + tubes + chev_side)
    return dict(hull=hull, tracks=tracks, turret=turret, turret_pos=(0, -1.05, 1.15), gun=None,
                gun_pos=(0, 0, 0), muzzle=(0, 2.0, 0.5), recoil=0.0, scan=0, launcher=True, rest_pitch=14, blast=(0, -1.0, 2.0),
                tubes=[(-0.95 + i * 0.38, 2.0, 0.3 + j * 0.45) for i in range(6) for j in range(2)])


def build_sam(V):
    """Mobile SAM: a tracked carrier with a launcher rail on a turntable
    carrying four white red-tipped missiles; olive drab with civ-colored
    side panels."""
    m = V.mats
    sec = lambda zt, wt: [(-1.05, 0.42), (1.05, 0.42), (1.05, 0.55), (1.45, 0.55), (1.45, zt - 0.2), (wt, zt), (-wt, zt), (-1.45, zt - 0.2), (-1.45, 0.55), (-1.05, 0.55)]
    hull = VP.sweep([(-2.6, sec(1.5, 1.3)), (1.5, sec(1.5, 1.3)), (2.6, sec(1.05, 1.3)),
                     (2.85, [(-1.05, 0.55), (1.05, 0.55), (1.05, 0.6), (1.4, 0.6), (1.4, 0.8), (1.25, 0.9), (-1.25, 0.9), (-1.4, 0.8), (-1.4, 0.6), (-1.05, 0.6)])],
                    m['paint'])
    panels = [M.box(0.04, 0.8, 0.4, m['decal'], center=(sx * 1.46, 1.3, 1.0)) for sx in (-1, 1)]
    panels += [M.box(0.04, 0.6, 0.4, m['decal'], center=(sx * 1.46, -1.9, 1.0)) for sx in (-1, 1)]
    hull = M.Mesh.concat([hull] + panels)
    wheels = [(-2.4, 0.46, 0.3)] + [(y, 0.3, 0.29) for y in (-1.75, -1.05, -0.35, 0.35, 1.05)] + [(2.3, 0.6, 0.28)]
    tracks = [Track(wheels, 0.55, sx * 1.22, link=0.2, visible_wheels=True, top=[(0.0, 0.72, 0.1)]) for sx in (-1, 1)]
    base = M.cylinder(0.7, 0.62, 0.3, m['paint'], seg=14)
    post = M.box(0.5, 0.5, 0.75, m['paint_dark'], center=(0, 0, 0.6))
    turret = M.Mesh.concat([base, post])
    # the launcher (elevates about x at the post top): 4 missiles in 2x2
    rails = [M.box(0.9, 0.3, 0.12, m['paint_dark'], center=(0, 0.2, 0.0))]
    missiles = []
    for i, (x, z) in enumerate([(-0.55, 0.22), (0.55, 0.22), (-0.55, -0.22), (0.55, -0.22)]):
        missiles.append(sam_missile(m).transformed(M.translate(x, -1.0, z)))
    gun = M.Mesh.concat(rails + missiles)
    return dict(hull=hull, tracks=tracks, turret=turret, turret_pos=(0, -0.55, 1.5), gun=gun,
                gun_pos=(0, 0.0, 1.0), muzzle=(0, 2.6, 0), recoil=0.0, scan=40, sam=True, rest_yaw=180, rest_pitch=8, blast=(0, 0.0, 1.9),
                missiles=[(-0.55, -1.0, 0.22), (0.55, -1.0, 0.22), (-0.55, -1.0, -0.22), (0.55, -1.0, -0.22)])


def sam_missile(m, length=3.1, r=0.11):
    """A white SAM with a red nose cone and dark fins, along +y from y=0."""
    body = VP.tube((0, 0, 0), (0, length * 0.82, 0), r, r, m['missile'], seg=10)
    nose = VP.tube((0, length * 0.82, 0), (0, length, 0), r, 0.01, m['missile_tip'], seg=10)
    band = VP.tube((0, length * 0.45, 0), (0, length * 0.62, 0), r * 1.05, r * 1.05, m['decal'], seg=10)
    fins = []
    for a in (45, 135, 225, 315):
        fins.append(M.box(0.02, 0.35, r * 2.6, m['steel_dark']).transformed(M.rot_y(a) @ M.translate(0, 0.25, r * 1.2)))
        fins.append(M.box(0.02, 0.22, r * 2.0, m['steel_dark']).transformed(M.rot_y(a) @ M.translate(0, length * 0.62, r * 0.9)))
    return M.Mesh.concat([body, nose, band] + fins)


ARMOR_BUILDERS = {'sherman': build_sherman, 'panzer': build_panzer, 'abrams': build_abrams,
                  'bradley': build_bradley, 'mlrs': build_mlrs, 'sam': build_sam}

ARMOR_PAINTS = {
    'sherman': {'paint': '#84764c', 'paint_dark': '#62563a', 'wood': '#6a5034', 'track': '#3a3630', 'track_link': '#56524a',
                'wheel': '#4a463c', 'wheel_hub': '#2e2c28', 'steel': ('#6a6a66', 0.5, 24), 'band': '#e8e8e8',
                'decal': '#f0f0f0'},
    'panzer': {'paint': '#a8b086', 'paint_dark': '#6e7a5a', 'paint_brown': '#5e5040', 'track': '#34332e',
               'track_link': '#56534c', 'wheel': '#5a6048', 'wheel_hub': '#2e302a', 'steel': ('#6a6a66', 0.5, 24),
               'decal': '#f0f0f0'},
    'abrams': {'paint': '#a8966a', 'paint_dark': '#7a6c4c', 'track': '#36332c', 'track_link': '#5a564c',
               'wheel': '#5a5440', 'wheel_hub': '#2e2c28', 'steel': ('#5a5a58', 0.5, 24), 'decal': '#f0f0f0'},
    'bradley': {'paint': '#b2a588', 'paint_dark': '#7c725c', 'paint_brown': '#5a5446', 'track': '#2e2d2c',
                'track_link': '#4e4c48', 'wheel': '#4a473e', 'wheel_hub': '#262626', 'steel': ('#5a5a58', 0.5, 24),
                'decal': '#f0f0f0'},
    'mlrs': {'paint': '#b09264', 'paint_dark': '#8a7250', 'track': '#2e2a24', 'track_link': '#4e483e',
             'wheel': '#4e4434', 'wheel_hub': '#2a241e', 'steel_dark': '#2a2826', 'glass': ('#8fa8b8', 0.6, 30),
             'decal': '#f0f0f0'},
    'sam': {'paint': '#6e6a54', 'paint_dark': '#4e4a3c', 'track': '#24221e', 'track_link': '#44403a',
            'wheel': '#3e3a30', 'wheel_hub': '#1e1c1a', 'steel_dark': '#2e2c2a', 'missile': '#e6e4de',
            'missile_tip': '#b02a22', 'decal': '#f0f0f0'},
}


@archetype('armor')
class Armor(VehicleBase):
    """Tracked vehicles. Actions: DEFAULT (idling engine), RUN (treads
    turning, bobbing), ATTACK1 (turret shot with recoil, flash and smoke;
    launchers raise and ripple-fire; the SAM launches a missile), DEATH (the
    vehicle blows up: fireball, turret thrown, charred smoking wreck),
    FORTIFY/FIDGET (turret scanning)."""

    loop_actions = ('DEFAULT', 'RUN', 'WALK', 'FORTIFY', 'FIDGET')

    def __init__(self, unit, params):
        super().__init__(unit, params)
        kind = params['model']
        self.kind = kind
        self.paint(ARMOR_PAINTS[kind], metal=('steel', 'steel_dark'))
        self.b = ARMOR_BUILDERS[kind](self)
        self.track_L = self.b['tracks'][0].L
        self.autofit()

    # -- assembling a pose
    def assemble(self, phase=0.0, yaw=0.0, pitch=0.0, recoil=0.0, body=(0, 0, 0, 0, 0), lift=0.0,
                 turret_off=None, char=False, hide_missiles=(), tracks_off=None):
        """body = (dz, pitch, roll, dy, dx) of the hull; turret_off: an extra
        transform of the turret (blown off)."""
        b = self.b
        dz, bp, br, dy, dx = body
        H = M.translate(dx, dy, dz) @ M.rot_x(bp) @ M.rot_y(br)
        parts = [b['hull'].transformed(H)]
        for i, tr in enumerate(b['tracks']):
            tm = tr.mesh(self.mats, phase, side=1 if tr.x > 0 else -1)
            if tracks_off is not None:
                tm = tm.transformed(tracks_off[i])
            parts.append(tm.transformed(H) if tracks_off is None else tm)
        T = H @ M.translate(*b['turret_pos']) @ M.rot_z(yaw)
        if turret_off is not None:
            T = turret_off @ T
        parts.append(b['turret'].transformed(T))
        G = T @ M.translate(*b['gun_pos']) @ M.rot_x(pitch) @ M.translate(0, -recoil, 0)
        if b.get('gun') is not None:
            g = b['gun']
            if b.get('sam') and hide_missiles:
                g = self._sam_gun(hide_missiles)
            parts.append(g.transformed(G))
        if b.get('launcher'):
            # the launcher box tilts up about its rear lower edge
            P = M.translate(0, -1.95, 0) @ M.rot_x(pitch) @ M.translate(0, 1.95, 0)
            parts[-1] = b['turret'].transformed(T @ P)
            G = T @ P
        out = M.Mesh.concat(parts)
        if char:
            out = VP.recolor(out, self.charred())
        return out, G, H, T

    def _sam_gun(self, hide):
        m = self.mats
        rails = [M.box(0.9, 0.3, 0.12, m['paint_dark'], center=(0, 0.2, 0.0))]
        ms = [sam_missile(m).transformed(M.translate(*p)) for i, p in enumerate(self.b['missiles']) if i not in hide]
        return M.Mesh.concat(rails + ms)

    # -- actions
    def frame(self, action, f, n, ctx):
        t = self.times(action, f, n)
        if action in ('RUN', 'WALK'):
            return self.run(t, n)
        if action == 'ATTACK1' or action.startswith('ATTACK'):
            return self.attack(t, ctx)
        if action == 'DEATH':
            return self.death(t, ctx)
        if action == 'FORTIFY' and self.b.get('launcher'):
            # the launcher box swings round to the side and stays there
            k = rig.smoothstep(t)
            mesh, *_ = self.assemble(yaw=-90 * k, pitch=6 * math.sin(math.pi * t))
            return FX.FxMesh(mesh, self.mats)
        if action in ('FORTIFY', 'FIDGET'):
            return self.fidget(t)
        return self.idle(t)

    def loops(self, action):
        if action == 'FORTIFY' and self.b.get('launcher'):
            return False
        return action in self.loop_actions

    def idle(self, t):
        w = math.sin(2 * math.pi * t * 2)
        mesh, *_ = self.assemble(body=(0.006 * w, 0.15 * w, 0, 0, 0), yaw=self.b.get('rest_yaw', 0),
                                 pitch=self.b.get('rest_pitch', 0))
        return FX.FxMesh(mesh, self.mats)

    def run(self, t, n):
        # the treads go round a whole number of link spacings per loop
        link = self.b['tracks'][0].L / max(8, int(self.b['tracks'][0].L / 0.2))
        phase = t * link * 3
        s = math.sin(2 * math.pi * t * 2)
        c = math.cos(2 * math.pi * t * 3)
        mesh, *_ = self.assemble(phase=phase, body=(0.02 + 0.015 * s, 0.6 * c, 0.3 * s, 0, 0),
                                 yaw=self.b.get('rest_yaw', 0), pitch=self.b.get('rest_pitch', 0))
        return FX.FxMesh(mesh, self.mats)

    def fidget(self, t):
        sc = self.b.get('scan', 30)
        yaw = sc * math.sin(2 * math.pi * t) + self.b.get('rest_yaw', 0)
        pitch = 3 * math.sin(4 * math.pi * t) + self.b.get('rest_pitch', 0) if not self.b.get('launcher') else 0
        if self.b.get('launcher'):
            # the launcher box tilts up a little and back
            k = (1 - math.cos(2 * math.pi * t)) / 2
            pitch = 10 * k
            yaw = 0
        mesh, *_ = self.assemble(yaw=yaw, pitch=pitch)
        return FX.FxMesh(mesh, self.mats)

    def attack(self, t, ctx):
        b = self.b
        mats = self.mats
        tf = self.p.get('fire', None)
        if tf is None:
            tf = flash_time(ctx, 0.2)
        if b.get('launcher'):
            return self.attack_launcher(t, ctx, tf)
        if b.get('sam'):
            return self.attack_sam(t, ctx, tf)
        dt = t - tf
        if b.get('burst'):
            # an autocannon burst: several small kicks
            k = 0.0
            fx = []
            for i in range(4):
                u = dt - i * 0.07
                if 0 <= u < 0.05:
                    k = max(k, 1 - u / 0.05)
            recoil = b['recoil'] * k
            rock = 0.0
        else:
            recoil = b['recoil'] * (math.exp(-max(dt, 0) * 9) if dt >= 0 else 0) * min(1, max(dt, 0) * 40 + (dt >= 0))
            rock = -2.2 * math.exp(-max(dt, 0) * 6) * math.sin(max(dt, 0) * 22) if dt >= 0 else 0
        mesh, G, H, T = self.assemble(recoil=recoil, body=(0, rock, 0, -0.06 * recoil, 0))
        fx = []
        muzzle = (G @ np.array([*b['muzzle'], 1.0], np.float32))[:3]
        fwd = (G[:3, :3] @ np.array([0, 1, 0], np.float32))
        flash_len = 0.12 if b.get('burst') else 0.1
        if not b.get('burst') and 0 <= dt < 0.06:
            mesh = VP.recolor(mesh, self.lit())
        if b.get('burst'):
            for i in range(4):
                u = dt - i * 0.07
                if 0 <= u < 0.045:
                    fx.append(FX.muzzle_flash(mats, muzzle + fwd * 0.05, fwd, 0.55, seed=i))
            if dt > 0:
                fx.append(FX.smoke_puffs(mats, muzzle, fwd, 0.6, min(dt / 0.6, 1.0), seed=3, n=3, color=0.2))
        else:
            if 0 <= dt < flash_len:
                s = 1.0 - dt / flash_len * 0.4
                fx.append(FX.muzzle_flash(mats, muzzle + fwd * 0.05, fwd, 1.5 * s, seed=1))
            if dt >= 0:
                fx.append(FX.smoke_puffs(mats, muzzle + fwd * 0.3, fwd, 1.1, min(dt / 0.55, 1.0), seed=2, n=5,
                                         color=0.45))
        return FX.FxMesh(M.Mesh.concat([mesh] + fx), mats)

    def attack_launcher(self, t, ctx, tf):
        """The launcher box rises, ripple-fires its rockets, settles."""
        b, mats = self.b, self.mats
        up = 52.0
        t_up = max(0.02, tf - 0.15)
        if t < t_up:
            pitch = up * rig.smoothstep(t / t_up)
        elif t < 0.8:
            pitch = up
        else:
            pitch = up * (1 - rig.smoothstep((t - 0.8) / 0.2))
        mesh, G, H, T = self.assemble(pitch=pitch, yaw=-90)
        fx = []
        fwd = G[:3, :3] @ np.array([0, 1, 0], np.float32)
        for k, tube in enumerate(b['tubes'][:6]):
            tk = tf + k * 0.05
            u = t - tk
            p0 = (G @ np.array([*tube, 1.0], np.float32))[:3]
            if 0 <= u < 0.35:
                pos = p0 + fwd * (u * 30)
                rocket = VP.tube(pos - fwd * 0.9, pos, 0.12, 0.12, mats['steel_dark'], seg=6)
                fx.append(rocket)
                fx.append(FX.plume(mats, pos - fwd * 0.9, -fwd, 1.2, 0.16, t=u, seed=k, smoke=False))
            if 0 <= u < 0.6:
                fx.append(FX.smoke_puffs(mats, p0 - fwd * 4.0, -fwd, 0.9, u / 0.6, seed=k + 5, n=3, color=0.0))
        return FX.FxMesh(M.Mesh.concat([mesh] + fx), mats)

    def attack_sam(self, t, ctx, tf):
        """The launcher turns up, a missile leaves with a smoke trail."""
        b, mats = self.b, self.mats
        up = 38.0
        t_up = max(0.02, tf - 0.08)
        if t < t_up:
            pitch = up * rig.smoothstep(t / t_up)
        elif t < 0.75:
            pitch = up
        else:
            pitch = up * (1 - rig.smoothstep((t - 0.75) / 0.25))
        dt = t - tf
        hide = (0,) if dt >= 0 else ()
        # swing round from the travel position (pointing back) to the front, and back at the end
        turn = rig.smoothstep(min(1.0, t / max(t_up * 0.6, 0.02))) * (1 - rig.smoothstep(max(0.0, (t - 0.8) / 0.2)))
        yaw = self.b.get('rest_yaw', 0) * (1 - turn)
        mesh, G, H, T = self.assemble(pitch=pitch, hide_missiles=hide, yaw=yaw)
        fx = []
        fwd = G[:3, :3] @ np.array([0, 1, 0], np.float32)
        if dt >= 0:
            p0 = (G @ np.array([*b['missiles'][0], 1.0], np.float32))[:3]
            dist = dt * 45.0
            pos = p0 + fwd * dist
            if dist < 30:
                fx.append(sam_missile(mats).transformed(_orient_y(pos, fwd)))
                fx.append(FX.plume(mats, pos, -fwd, 1.0, 0.18, t=dt, seed=1, smoke=False))
            # the trail it leaves behind, drifting and fading
            for i in range(8):
                s = i / 8 * min(dist, 22)
                age = (dist - s) / 25.0
                if age < 1:
                    fx.append(FX.puff(mats, p0 + fwd * (s + 0.5), 0.35 * (1 + age * 1.5) * (1 - age ** 2),
                                      'smoke_white' if age < 0.4 else 'smoke_light', seed=40 + i))
            if dt < 0.12:
                fx.append(FX.muzzle_flash(mats, p0 - fwd * 0.2, -fwd, 0.8, seed=4))
        return FX.FxMesh(M.Mesh.concat([mesh] + fx), mats)

    def death(self, t, ctx):
        b, mats = self.b, self.mats
        tb = 0.12                                   # the hit
        u = max(0.0, t - tb)
        char = t > tb + 0.12
        turret_off = None
        tp = np.asarray(b['turret_pos'], np.float32)
        ring = None
        if t >= tb and self.p.get('turret_off'):
            # blown off: an arc up and over to the ground beside the hull
            land = min(1.0, u / 0.45)
            arc = 4 * land * (1 - land) * 2.2
            end = np.array([1.9, -1.4, -tp[2] + 0.42], np.float32)
            turret_off = (M.translate(*(end * land)) @ M.translate(0, 0, arc) @ M.translate(*tp) @
                          M.rot_z(70 * land) @ M.rot_y(-22 * land) @ M.translate(*(-tp)))
            ring = M.cylinder(0.75, 0.75, 0.04, mats['char'], seg=16, z0=tp[2] - 0.01).transformed(M.translate(tp[0], tp[1], 0))
        elif t >= tb:
            # jolted: lifted by the blast, dropping back askew
            hgt = max(0.0, 3.0 * u - 10.0 * u * u)
            land = min(1.0, u / 0.3)
            turret_off = (M.translate(0.1 * land, -0.15 * land, hgt) @ M.translate(*tp) @ M.rot_z(14 * land) @
                          M.rot_y(-5 * land) @ M.translate(*(-tp)))
        jolt = 0.0
        if t >= tb:
            jolt = 2.5 * math.exp(-u * 10) * math.sin(u * 40)
        mesh, G, H, T = self.assemble(body=(0, jolt, jolt * 0.5, 0, 0), char=char, turret_off=turret_off,
                                      pitch=-4 if char else 0)
        fx = [ring] if ring is not None else []
        center = np.asarray(b.get('blast', tp + np.array([0, 0, 0.6])), np.float32)
        if t >= tb:
            fx.append(FX.explosion(mats, center, 1.6, u / 0.55, seed=5, n=10))
            fx.append(FX.debris(mats, center, 0.8, min(u / 0.6, 1.0), seed=6, n=8))
        if t > tb + 0.25:
            fx.append(FX.smoke_column(mats, center + np.array([0, 0, 0.3]), 0.9, (t - tb) * 1.5, seed=7, n=6,
                                      color=0.9, height=2.6))
            fx.append(FX.flames(mats, center, 0.6, t, seed=8, n=4))
        return FX.FxMesh(M.Mesh.concat([mesh] + fx), mats)


def _orient_y(pos, fwd):
    """A transform taking +y to fwd at pos (keeping +z roughly up)."""
    f = np.asarray(fwd, np.float32)
    f = f / (np.linalg.norm(f) + 1e-9)
    up = np.array([0, 0, 1], np.float32)
    if abs(f[2]) > 0.95:
        up = np.array([0, 1, 0], np.float32)
    x = np.cross(f, up); x /= np.linalg.norm(x)
    z = np.cross(x, f)
    T = np.eye(4, dtype=np.float32)
    T[:3, 0], T[:3, 1], T[:3, 2], T[:3, 3] = x, f, z, pos
    return T
