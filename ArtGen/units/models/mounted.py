"""Mounted units: a rigged horse carrying a humanoid rider ('horse_rider').

The horse is built from smooth parts on a small rig: a body (barrel,
shoulders, haunches) whose transform carries everything, a neck and head,
a tail, and four legs posed by forward kinematics (three or four segments
each plus a hoof, every joint a pitch). The rider is a humanoid
(models/humanoid.py) posed in a seated stance by its own IK (feet in the
stirrups, hands at the reins or the weapon) and placed on the saddle.

Pose channels (all in one dict so rig.Keys animates them together):
    h.pos, h.rot, h.pivot   the horse body: translation, (pitch, roll, yaw)
                            about the pivot point (+pitch rears up)
    h.neck, h.head, h.tail  (pitch, roll, yaw) on top of their rest angles
    h.fl, h.fr, h.hl, h.hr  4 joint pitches per leg, top to bottom (+ = the
                            part below swings forward)
    the humanoid channels   the rider (in rider space, before RIDER_SCALE)
    r.free, r.pos, r.rot    the rider leaves the saddle (death): blend weight
                            to a free placement of the pelvis in model space

Catalog parameters (see catalog/mounted.py):
    horse   {'coat', 'mane', 'points' (lower legs), 'hoof', 'muzzle',
             'blanket': None | 'saddle_cloth' | 'caparison' | 'checker' |
             'barding', 'chanfron': bool, 'plume': bool, 'dapple', ...}
    rider   a humanoid parameter dict (+ 'headgear' from props_mounted,
            'pants', 'coat_skirt'), its 'weapon' / 'offhand'
    attack  'thrust' | 'slash' | 'shoot_bow' | 'shoot_gun' | 'javelin'
    victory_rear   bool: the horse rears in VICTORY
    escort  None | 'dog' (the Conquistador's war dog)
"""
import math

import numpy as np

import mesh as M
import rig
from models import archetype
from models import humanoid as H
from models import props as P
from models import props_mounted as PM
from models.props_mounted import tube_y

RIDER_SCALE = 0.82
SEAT = np.array([0.0, -0.04, 1.54], np.float32)   # top of the saddle, body space
PELVIS_SEAT = 0.10                                  # rider pelvis above the seat (rider space)

V3 = lambda *a: np.asarray(a, np.float32)  # noqa: E731


# ------------------------------------------------------------ helpers

def T(*a):
    return M.translate(*a)


def euler(r):
    return M.euler(float(r[0]), float(r[1]), float(r[2]))


def smooth01(u):
    u = min(max(u, 0.0), 1.0)
    return u * u * (3 - 2 * u)


def warp(t, pairs):
    return H._warp(t, pairs)


class Crew:
    """A humanoid whose materials are merged into a shared table: its mesh
    material indexes are remapped (names prefixed) after every pose."""

    def __init__(self, unit, params, mats, prefix):
        self.h = H.Humanoid(unit, params)
        self.mats = mats
        self.prefix = prefix
        self.p = params
        # extra worn gear (head gear, trousers, coats)
        self.h.parts += rider_extras(self.h.body, params)
        # held props: the mounted ones (props_mounted) first, then props.py
        cache = {}
        hm = self.h.mats

        def prop(name, **kw):
            key = (name, tuple(sorted(kw.items())))
            if key not in cache:
                fn = getattr(PM, name, None) or getattr(P, name)
                cache[key] = fn(hm, **kw).transformed(M.scale(params.get('prop_scale', 1.0)))
            return cache[key]
        self.h.prop = prop

    def _sync(self):
        src = self.h.mats
        idx = []
        for i, name in enumerate(src.names):
            idx.append(self.mats.add(self.prefix + name, src.color[i], tint=src.tint[i], spec=src.spec[i],
                                     gloss=src.gloss[i], metal=src.metal[i]))
        return np.asarray(idx, np.int32)

    def prop_mesh(self, name):
        m = self.h.prop(name).copy()
        m.M = self._sync()[m.M]
        return m

    def mesh(self, pose, act, weapon):
        m = self.h.pose_mesh(pose, act, weapon)
        if len(m):
            m.M = self._sync()[m.M]
        return m


def rider_extras(body, p):
    """Gear the humanoid Body doesn't make: head gear from props_mounted,
    trousers, a coat skirt split for the saddle."""
    out = []
    mats = body.mats
    bulk = H.build_params(p).get('bulk', 1.0) if hasattr(H, 'build_params') else p.get('build', {}).get('bulk', 1.0)
    lb = H.LIMB * bulk
    hg = p.get('headgear')
    if hg:
        g = PM.HEADGEAR[hg](mats)
        hs = p.get('headgear_scale', 1.0)
        hs = (hs, hs, hs) if np.isscalar(hs) else tuple(hs)
        # scaled about the top of the head band (z = 0.12), so the hat stays seated
        g = g.transformed(T(0, 0, 0.12) @ M.scale(*hs) @ T(0, 0, -0.12))
        out.append(('head', g.transformed(M.scale(H.HEAD)), 'headgear'))
    pants = p.get('pants')
    if pants:
        pm = P.mat(mats, pants, '#ffffff') if pants in mats else P.mat(mats, 'pants', '#6a5a48')
        for side in 'rl':
            out.append((f'thigh_{side}', M.capsule((0, 0, 0.02), (0, 0, -H.THIGH), 0.10 * lb, 0.075 * lb, pm, seg=10, rings=2), None))
            out.append((f'shin_{side}', M.capsule((0, 0, 0.0), (0, 0, -H.SHIN + 0.04), 0.074 * lb, 0.058 * lb, pm, seg=10, rings=2), None))
        out.append(('pelvis', M.ellipsoid(0.18 * H.TORSO_W * bulk, 0.135 * H.TORSO_W * bulk, 0.13, pm, (0, 0, -0.02), seg=14), None))
    sleeves = p.get('sleeves')
    if sleeves:
        sm = mats[sleeves]
        for side in 'rl':
            out.append((f'arm_{side}', M.capsule((0, 0, 0.0), (0, 0, -H.UPPER_ARM), 0.074 * lb, 0.062 * lb, sm, seg=10, rings=2), None))
            out.append((f'forearm_{side}', M.capsule((0, 0, 0.0), (0, 0, -H.FOREARM + 0.04), 0.06 * lb, 0.05 * lb, sm, seg=10, rings=2), None))
    if p.get('back_gear') == 'spears':
        for a in (-14, 12):
            sp = PM.cav_spear(mats, length=2.3, grip=1.0)
            out.append(('chest', sp.transformed(T(0.0, -0.2, 0.05) @ M.rot_y(a) @ M.rot_x(8)), None))
    coat = p.get('coat_skirt')
    if coat:
        cm = mats[coat]
        tw = H.TORSO_W * bulk
        # front and back flaps of a long coat, hanging past the saddle
        for sy, rx in ((1, 0.0), (-1, 0.0)):
            out.append(('pelvis', M.rounded_box(0.30 * tw, 0.04, 0.42, cm, (0, sy * 0.13 * tw, -0.17), r=0.015, seg=8, p=4)
                        .transformed(M.rot_x(-sy * 12)), None))
        for sx in (1, -1):
            out.append(('pelvis', M.rounded_box(0.05, 0.28 * tw, 0.40, cm, (sx * 0.19 * tw, 0, -0.16), r=0.015, seg=8, p=4)
                        .transformed(M.rot_y(sx * 14)), None))
    return out


# ------------------------------------------------------------ the horse

# Barrel: rings (y, half-width, half-height, center z), rump to chest.
BARREL = [(-0.90, 0.05, 0.06, 1.30), (-0.86, 0.15, 0.18, 1.30), (-0.75, 0.22, 0.26, 1.28), (-0.55, 0.25, 0.29, 1.25),
          (-0.25, 0.245, 0.285, 1.23), (0.10, 0.25, 0.29, 1.23), (0.38, 0.235, 0.29, 1.26), (0.58, 0.19, 0.26, 1.29),
          (0.69, 0.11, 0.17, 1.31), (0.73, 0.03, 0.05, 1.32)]


def barrel_at(y, table=None):
    table = BARREL if table is None else table
    ys = [r[0] for r in table]
    y = min(max(y, ys[0]), ys[-1])
    return tuple(float(np.interp(y, ys, [r[k] for r in table])) for k in (1, 2, 3))


def drape(y0, y1, mats_fn, g=0.03, drop=0.0, arc=1.0, ny=10, na=16, flare=0.12, inset=None, table=None):
    """A cloth/plate lying over the barrel between y0 and y1: over the top
    across `arc` (1 = from side to side at the widest), then hanging `drop`
    meters straight down the flanks. `mats_fn(iy, ia, edge)` gives each quad's
    material (for patterns). `inset(u)` (u = 0..1 along y) can shape the
    hem: the hanging length times inset(u)."""
    ys = np.linspace(y0, y1, ny + 1)
    a0 = math.pi / 2 * (1 - arc)
    angs = np.linspace(a0, math.pi - a0, na + 1)
    nd = 4 if drop > 0 else 0
    rows = []
    for j, y in enumerate(ys):
        rx, rz, zc = barrel_at(y, table)
        rx, rz = rx + g, rz + g
        u = j / max(ny, 1)
        dl = drop * (inset(u) if inset else 1.0)
        pts = []
        side = []
        for k in range(nd, 0, -1):   # right flank hanging (x > 0), bottom first
            f = k / nd
            side.append((rx * (1 + flare * f), y, zc - dl * f + (rz * math.sin(a0) if arc < 1 else 0.0)))
        pts += side
        for a in angs:
            pts.append((rx * math.cos(a), y, zc + rz * math.sin(a)))
        for k in range(1, nd + 1):
            f = k / nd
            pts.append((-rx * (1 + flare * f), y, zc - dl * f + (rz * math.sin(a0) if arc < 1 else 0.0)))
        rows.append(pts)
    V = np.asarray(rows, np.float32)            # (ny+1, npts, 3)
    nr, npt = V.shape[:2]
    verts = V.reshape(-1, 3)
    faces, fm = [], []
    for j in range(nr - 1):
        for i in range(npt - 1):
            a, b, c, d = j * npt + i, j * npt + i + 1, (j + 1) * npt + i, (j + 1) * npt + i + 1
            edge = j == 0 or j == nr - 2 or i == 0 or i == npt - 2
            m = mats_fn(j, i - nd, edge)
            faces += [(a, c, d), (a, d, b)]
            fm += [m, m]
    faces = np.asarray(faces, np.int64)
    mesh = M.from_indexed(verts, faces, 0)
    mesh.M = np.asarray(fm, np.int32)
    return mesh


def plate_patch(y0, y1, a0, a1, g, m, ny=4, na=4, table=None):
    """A patch on the barrel surface between angles a0..a1 (radians, 0 = right side)."""
    ys = np.linspace(y0, y1, ny + 1)
    angs = np.linspace(a0, a1, na + 1)
    rows = []
    for y in ys:
        rx, rz, zc = barrel_at(y, table)
        rows.append([(rx * (1 + g) * math.cos(a), y, zc + rz * (1 + g) * math.sin(a)) for a in angs])
    V = np.asarray(rows, np.float32).reshape(-1, 3)
    npt = na + 1
    faces = []
    for j in range(ny):
        for i in range(na):
            a, b, c, d = j * npt + i, j * npt + i + 1, (j + 1) * npt + i, (j + 1) * npt + i + 1
            faces += [(a, c, d), (a, d, b)]
    return M.from_indexed(V, np.asarray(faces), m)


def hem_fn(ny, na, drop, hem=1):
    """edge(j, i, e) for drape's mats_fn: True within `hem` quads of the
    cloth's border (hem=1: the drape's own edge flag)."""
    nd = 4 if drop > 0 else 0

    def f(j, i, e):
        if hem <= 1:
            return e
        return e or j < hem or j >= ny - hem or i < -nd + hem or i >= na + nd - hem
    return f


# Legs: root (x, y) in body space, segments (length, rest pitch relative to
# the previous segment, radius top, radius bottom, material key), then the
# hoof's rest pitch (so it stands flat).
LEG_SEGS = {
    'f': [(0.64, -4, 0.095, 0.05, 'coat'), (0.36, 4, 0.04, 0.036, 'points'), (0.13, 30, 0.036, 0.038, 'points')],
    'h': [(0.38, 28, 0.12, 0.09, 'coat'), (0.46, -50, 0.075, 0.045, 'coat'), (0.34, 22, 0.042, 0.036, 'points'),
          (0.13, 30, 0.036, 0.038, 'points')],
}
LEG_ROOTS = {'fl': (-0.14, 0.46), 'fr': (0.14, 0.46), 'hl': (-0.15, -0.56), 'hr': (0.15, -0.56)}
HOOF_H = 0.075


class Horse:
    def __init__(self, mats, p):
        self.p = p
        self.mats = mats
        coat = p.get('coat', '#8a6440')
        mm = lambda name, col, **kw: P.mat(mats, name, col, **kw)  # noqa: E731
        self.m_coat = mm('coat', coat, spec=0.10, gloss=14)
        self.m_mane = mm('mane', p.get('mane', '#2a2018'), spec=0.04)
        self.m_points = mm('points', p.get('points', p.get('mane', '#2a2018')), spec=0.08)
        self.m_hoof = mm('hoof', p.get('hoof', '#2c2622'), spec=0.15)
        self.m_muzzle = mm('muzzle', p.get('muzzle', '#3a2a22'), spec=0.12)
        self.m_eye = mm('h_eye', '#141010', spec=0.5, gloss=30)
        self.m_bridle = mm('bridle', p.get('bridle', '#ffffff'), tint=p.get('bridle_tint', True))
        self.m_tack = mm('tack', p.get('tack', '#4a3020'), spec=0.1)
        self.m_saddle = mm('saddle', p.get('saddle', '#5a3a22'), spec=0.12)
        self.m_patch = mm('coat2', p.get('coat2', p.get('mane', '#e8e4dc')), spec=0.08) if p.get('patches') else None
        mats_d = {'coat': self.m_coat, 'points': self.m_points}
        # rest pose of the legs: root heights so the hooves stand on z = 0
        self.leg_root_z = {}
        for leg, (x, y) in LEG_ROOTS.items():
            segs = LEG_SEGS[leg[0]]
            ang, z = 0.0, 0.0
            for L, a, *_ in segs:
                ang += a
                z -= L * math.cos(math.radians(ang))
            self.leg_root_z[leg] = -z + HOOF_H
        self.leg_meshes = {}
        for kind, segs in LEG_SEGS.items():
            ms = []
            for i, (L, a, r0, r1, mk) in enumerate(segs):
                m = mats_d[mk]
                parts = [M.capsule((0, 0, 0), (0, 0, -L), r0, r1, m, seg=10, rings=2)]
                if kind == 'f' and i == 0:   # the forearm muscle
                    parts.append(M.ellipsoid(0.075, 0.09, 0.20, m, (0, 0.02, -0.15), seg=10))
                if kind == 'h' and i == 1:   # the gaskin
                    parts.append(M.ellipsoid(0.065, 0.085, 0.17, m, (0, -0.01, -0.12), seg=10))
                if i == len(segs) - 2:       # the fetlock joint
                    parts.append(M.sphere(r1 * 1.25, m, (0, -0.01, -L), seg=8))
                ms.append(M.Mesh.concat(parts))
            hoof = M.lathe([(0.0, 0.0005), (0.0, 0.058), (HOOF_H * 0.5, 0.054), (HOOF_H, 0.046), (HOOF_H + 0.005, 0.0005)],
                           self.m_hoof, seg=10).transformed(T(0, 0.012, -HOOF_H))
            ms.append(hoof)
            self.leg_meshes[kind] = ms
        self._build_body()

    # -- static parts
    def _build_body(self):
        p = self.p
        c = self.m_coat
        body = [tube_y(BARREL, c, seg=18)]
        for sx in (1, -1):
            body.append(M.ellipsoid(0.14, 0.28, 0.26, c, (sx * 0.125, -0.56, 1.22), seg=14))   # haunches
            body.append(M.ellipsoid(0.12, 0.19, 0.25, c, (sx * 0.115, 0.40, 1.19), seg=12))    # shoulders
        body.append(M.ellipsoid(0.17, 0.13, 0.20, c, (0, 0.60, 1.20), seg=12))                 # chest
        if self.m_patch is not None:
            # pinto patches on the flanks and hindquarters
            for (y0, y1, a0, a1) in p.get('patches'):
                body.append(plate_patch(y0, y1, math.radians(a0), math.radians(a1), 0.012, self.m_patch, 4, 5))
                body.append(plate_patch(y0, y1, math.radians(180 - a1), math.radians(180 - a0), 0.012, self.m_patch, 4, 5))
        if p.get('marks'):
            mk = P.mat(self.mats, 'marks', '#ffffff', tint=True)
            for (y0, y1, a0, a1) in p['marks']:
                body.append(plate_patch(y0, y1, math.radians(a0), math.radians(a1), 0.016, mk, 2, 3))
                body.append(plate_patch(y0, y1, math.radians(180 - a1), math.radians(180 - a0), 0.016, mk, 2, 3))
        if p.get('dapple'):
            dm = P.mat(self.mats, 'dapple', p['dapple'], spec=0.08)
            rng = np.random.default_rng(7)
            for _ in range(p.get('dapple_n', 70)):
                y = rng.uniform(-0.8, 0.6)
                a = rng.uniform(-0.6, math.pi + 0.6)
                da, dy = rng.uniform(0.07, 0.14), rng.uniform(0.04, 0.08)
                body.append(plate_patch(y, y + dy, a, a + da, 0.012, dm, 1, 2))
        self.body_parts = body
        # neck (neck space: along +y, the crest toward +z)
        self.neck_root = V3(0, 0.50, 1.38)
        self.neck_rest = 66.0
        neck = [tube_y([(-0.20, 0.15, 0.30, -0.04), (0.0, 0.15, 0.27, 0.0), (0.22, 0.12, 0.21, 0.01), (0.45, 0.095, 0.155, 0.02),
                        (0.62, 0.085, 0.125, 0.02), (0.68, 0.06, 0.08, 0.02)], c, seg=14)]
        mane_h = p.get('mane_h', 0.07)
        neck.append(tube_y([(-0.14, 0.02, 0.02, 0.27), (-0.05, 0.035, mane_h, 0.27), (0.20, 0.035, mane_h, 0.22),
                            (0.45, 0.032, mane_h * 0.9, 0.17), (0.66, 0.03, mane_h * 0.8, 0.14), (0.72, 0.02, 0.02, 0.13)],
                           self.m_mane, seg=8))
        if p.get('neck_cover'):
            nc = p['neck_cover']
            if nc == 'steel':
                cm = P.mat(self.mats, 'barding_steel', p.get('barding_steel', '#a8acb4'), spec=0.85, gloss=28, metal=True)
            else:   # its own material: the body blanket may be another color
                cm = P.mat(self.mats, 'neck_cloth', p.get('neck_color', '#ffffff'), tint=p.get('neck_tint', True))
            neck.append(tube_y([(-0.16, 0.175, 0.32, -0.02), (0.0, 0.175, 0.29, 0.0), (0.22, 0.145, 0.23, 0.01),
                                (0.45, 0.12, 0.175, 0.02), (0.58, 0.11, 0.15, 0.02)], cm, seg=14, cap0=False, cap1=False))
        self.neck_parts = neck
        self.neck_len = 0.64
        # head (head space: muzzle along +y)
        self.head_rest = -116.0
        hd = [M.ellipsoid(0.098, 0.15, 0.115, c, (0, 0.08, 0.0), seg=14),
              M.ellipsoid(0.092, 0.13, 0.10, c, (0, 0.10, -0.07), seg=12),
              tube_y([(0.12, 0.085, 0.10, -0.01), (0.30, 0.07, 0.08, -0.02), (0.44, 0.064, 0.072, -0.03),
                      (0.50, 0.05, 0.06, -0.03)], c, seg=12),
              M.ellipsoid(0.066, 0.075, 0.072, self.m_muzzle, (0, 0.46, -0.035), seg=12)]
        for sx in (1, -1):
            hd.append(M.ellipsoid(0.018, 0.026, 0.02, self.m_eye, (sx * 0.088, 0.10, 0.035), seg=6))
            hd.append(M.capsule((sx * 0.05, 0.0, 0.08), (sx * 0.07, -0.05, 0.21), 0.032, 0.008, c, seg=7, rings=1))
            hd.append(M.ellipsoid(0.012, 0.016, 0.012, self.m_eye, (sx * 0.035, 0.51, -0.01), seg=5))
        hd.append(M.ellipsoid(0.05, 0.06, 0.035, self.m_mane, (0, 0.03, 0.10), seg=8))   # forelock
        b = self.m_bridle
        hd.append(tube_y([(0.25, 0.08, 0.093, -0.018), (0.36, 0.074, 0.084, -0.026)], b, seg=12, cap0=False, cap1=False))
        hd.append(tube_y([(0.0, 0.103, 0.12, 0.0), (0.035, 0.103, 0.12, 0.0)], self.m_tack, seg=12, cap0=False, cap1=False)
                  .transformed(M.rot_x(-25)))
        for sx in (1, -1):
            hd.append(M.capsule((sx * 0.078, 0.29, -0.02), (sx * 0.1, 0.02, 0.04), 0.012, 0.012, self.m_tack, seg=5, rings=1))
        if p.get('chanfron'):
            st = P.mat(self.mats, 'barding_steel', p.get('barding_steel', '#a8acb4'), spec=0.85, gloss=28, metal=True)
            hd.append(tube_y([(-0.02, 0.105, 0.125, 0.012), (0.12, 0.1, 0.125, 0.012), (0.30, 0.08, 0.095, -0.0),
                              (0.42, 0.07, 0.08, -0.01)], st, seg=12, cap0=True, cap1=True).transformed(T(0, 0, 0.006)))

        if p.get('plume'):
            pm = P.mat(self.mats, 'h_plume', p.get('plume_color', '#ffffff'), tint=p.get('plume_tint', True))
            if p.get('plume') == 'fan':   # a fan of feathers (Egyptian chariot horses)
                for a in (-28, -10, 8, 26):
                    hd.append(M.ellipsoid(0.03, 0.02, 0.13, pm, (0, 0, 0.12), seg=7)
                              .transformed(T(0, 0.0, 0.10) @ M.rot_y(a) @ M.rot_x(-15)))
            else:
                hd.append(M.capsule((0, -0.02, 0.14), (0, -0.10, 0.34), 0.035, 0.05, pm, seg=8, rings=2))
        self.head_parts = hd
        self.head_len = 0.55
        # tail (tail space: hangs from the root)
        tl = []
        pts = [(0, 0, 0), (0, -0.10, -0.10), (0, -0.17, -0.30), (0, -0.19, -0.55), (0, -0.17, -0.78)]
        rr = [0.055, 0.075, 0.09, 0.08, 0.025]
        for a, b2, r0, r1 in zip(pts[:-1], pts[1:], rr[:-1], rr[1:]):
            tl.append(M.capsule(a, b2, r0, r1, self.m_mane, seg=9, rings=2))
        self.tail_parts = tl
        self.tail_root = V3(0, -0.86, 1.40)
        # tack and cloth over the back
        self.back_parts = self._blanket()

    def _blanket(self):
        p = self.p
        kind = p.get('blanket', 'saddle_cloth')
        out = []
        mats = self.mats
        trim_c = p.get('trim')
        trim = P.mat(mats, 'blanket_trim', trim_c, spec=0.5, gloss=20, tint=p.get('trim_tint', False)) if trim_c else None
        covered = kind in ('caparison', 'checker', 'dots', 'barding')
        if kind in ('saddle_cloth', 'pelt'):
            bm = P.mat(mats, 'blanket', p.get('blanket_color', '#ffffff'), tint=p.get('blanket_tint', True))
            y0, y1 = p.get('cloth_span', (-0.40, 0.24))
            hem = hem_fn(10, 14, p.get('cloth_drop', 0.42), p.get('hem', 1))
            out.append(drape(y0, y1, lambda j, i, e: trim if (hem(j, i, e) and trim is not None) else bm, g=0.02,
                             drop=p.get('cloth_drop', 0.42), ny=10, na=14, flare=0.05))
        elif kind in ('caparison', 'checker', 'dots'):
            bm = P.mat(mats, 'blanket', p.get('blanket_color', '#ffffff'), tint=p.get('blanket_tint', True))
            alt = P.mat(mats, 'blanket2', p.get('blanket2', '#f0f0f0'), tint=False)
            ny, na = (28, 30) if kind == 'dots' else (18, 20)
            hem = hem_fn(ny, na, p.get('cloth_drop', 0.62), p.get('hem', 1))

            def pat(j, i, e):
                if hem(j, i, e) and trim is not None:
                    return trim
                if kind == 'checker':
                    return bm if (j + i) % 2 == 0 else alt
                if kind == 'dots':
                    return alt if (j % 2 == 1 and (i + (j // 2) % 2) % 2 == 0) else bm
                return bm
            out.append(drape(-0.98, 0.80, pat, g=0.035, drop=p.get('cloth_drop', 0.62), ny=ny, na=na, flare=0.10,
                             inset=lambda u: 0.78 + 0.22 * math.sin(math.pi * u)))
        elif kind == 'barding':
            st = P.mat(mats, 'barding_steel', p.get('barding_steel', '#a8acb4'), spec=0.85, gloss=28, metal=True)
            out.append(drape(-0.95, 0.76, lambda j, i, e: (trim if (e and trim is not None and not p.get('caparison_over'))
                                                           else st), g=0.04, drop=p.get('cloth_drop', 0.35), ny=12, na=14,
                             flare=0.06, inset=lambda u: 0.6 + 0.4 * math.sin(math.pi * u)))
            if p.get('caparison_over'):
                bm = P.mat(mats, 'blanket', '#ffffff', tint=True)
                oy0, oy1 = p.get('over_span', (-0.70, 0.40))
                hem = hem_fn(10, 14, p.get('over_drop', 0.55), p.get('hem', 1))
                out.append(drape(oy0, oy1, lambda j, i, e: trim if (hem(j, i, e) and trim is not None) else bm, g=p.get('over_gap', 0.075),
                                 drop=p.get('over_drop', 0.55), ny=10, na=14, flare=0.08))
        # saddle (leather seat with pommel and cantle)
        sm = self.m_saddle
        if p.get('saddle_visible', True):
            out.append(drape(-0.28, 0.16, lambda j, i, e: sm, g=0.075 if covered else 0.045,
                             drop=0.10, arc=0.8, ny=5, na=8, flare=0.0))
            rx, rz, zc = barrel_at(0.16)
            out.append(M.ellipsoid(0.10, 0.05, 0.07, sm, (0, 0.17, zc + rz + 0.07), seg=10))
            rx, rz, zc = barrel_at(-0.30)
            out.append(M.ellipsoid(0.13, 0.05, 0.08, sm, (0, -0.30, zc + rz + 0.07), seg=10))
        if not covered and p.get('girth', True):
            rx, rz, zc = barrel_at(0.12)
            out.append(tube_y([(0.09, rx + 0.012, rz + 0.012, zc), (0.15, rx + 0.012, rz + 0.012, zc)], self.m_tack, seg=18,
                              cap0=False, cap1=False))
        if p.get('breastplate', True) and not covered:
            out.append(M.capsule((0.20, 0.42, 1.38), (0, 0.67, 1.16), 0.016, 0.016, self.m_tack, seg=5, rings=1))
            out.append(M.capsule((-0.20, 0.42, 1.38), (0, 0.67, 1.16), 0.016, 0.016, self.m_tack, seg=5, rings=1))
        return out

    # -- posing
    def hoof_points(self, B, hp):
        """Model-space points under each hoof for body transform B."""
        out = []
        for leg, (x, y) in LEG_ROOTS.items():
            segs = LEG_SEGS[leg[0]]
            d = hp['h.' + leg]
            J = B @ T(x, y, self.leg_root_z[leg])
            for i, (L, a, *_) in enumerate(segs):
                J = J @ M.rot_x(a + float(d[i])) @ T(0, 0, -L)
            J = J @ M.rot_x(-sum(s[1] for s in segs) - sum(float(d[i]) for i in range(len(segs))) * 0.0)
            for py in (-0.05, 0.06):
                out.append((J @ np.array([0, py, -HOOF_H, 1], np.float32))[:3])
        return np.asarray(out)

    def pose(self, hp):
        """The horse mesh for pose channels hp, and the transforms riders
        and tack hang on (body, head, seat)."""
        pos, rot, piv = hp['h.pos'], hp['h.rot'], hp['h.pivot']
        B = T(*pos) @ T(*piv) @ euler(rot) @ T(*(-piv))
        g = float(hp.get('h.ground', 0.0))
        if g > 0:
            # keep the lowest hoof on the ground (gallop, rearing)
            zmin = min(self.hoof_points(B, hp)[:, 2])
            B = T(0, 0, -g * zmin) @ B
        meshes = [m.transformed(B) for m in self.body_parts + self.back_parts]
        nr = hp['h.neck']
        Nk = B @ T(*self.neck_root) @ M.euler(self.neck_rest + float(nr[0]), float(nr[1]), float(nr[2]))
        meshes += [m.transformed(Nk) for m in self.neck_parts]
        hr = hp['h.head']
        Hd = Nk @ T(0, self.neck_len, 0.02) @ M.euler(self.head_rest + float(hr[0]), float(hr[1]), float(hr[2]))
        meshes += [m.transformed(Hd) for m in self.head_parts]
        tr = hp['h.tail']
        Tl = B @ T(*self.tail_root) @ euler(tr)
        meshes += [m.transformed(Tl) for m in self.tail_parts]
        for leg, (x, y) in LEG_ROOTS.items():
            kind = leg[0]
            segs = LEG_SEGS[kind]
            d = hp['h.' + leg]
            J = B @ T(x, y, self.leg_root_z[leg])
            ms = self.leg_meshes[kind]
            for i, (L, a, *_) in enumerate(segs):
                J = J @ M.rot_x(a + float(d[i]) if i < len(d) else a)
                meshes.append(ms[i].transformed(J))
                J = J @ T(0, 0, -L)
            hoof_pitch = -sum(s[1] for s in segs)
            J = J @ M.rot_x(hoof_pitch + (float(d[len(segs)]) if len(d) > len(segs) else 0.0))
            meshes.append(ms[-1].transformed(J))
        return meshes, {'body': B, 'neck': Nk, 'head': Hd}


# ------------------------------------------------------------ poses and animations

def horse_rest():
    z4 = V3(0, 0, 0, 0, 0)
    return {'h.pos': V3(0, 0, 0), 'h.rot': V3(0, 0, 0), 'h.pivot': V3(0, -0.6, 0), 'h.neck': V3(0, 0, 0),
            'h.head': V3(0, 0, 0), 'h.tail': V3(0, 0, 0), 'h.fl': z4.copy(), 'h.fr': z4.copy(), 'h.hl': z4.copy(),
            'h.hr': z4.copy(), 'h.ground': np.float32(1), 'r.free': np.float32(0), 'r.pos': V3(0, 0, 0), 'r.rot': V3(0, 0, 0),
            'w.drop': np.float32(0), 'w.pos': V3(0, 0, 0), 'w.rot': V3(0, 0, 0)}


# Rider rest poses for each weapon (rider space).
def rider_stance(weapon, offhand, stance=None):
    p = H.stance(None, None)
    p.update({
        'foot_r': V3(0.34, 0.06, 0.22), 'foot_l': V3(-0.34, 0.06, 0.22), 'toe_r': np.float32(8), 'toe_l': np.float32(8),
        'fyaw_r': np.float32(-14), 'fyaw_l': np.float32(14),
        'hand_l': V3(-0.10, 0.42, 1.02), 'elbow_l': V3(-0.5, -0.8, -0.4),
        'hand_r': V3(0.12, 0.42, 1.02), 'elbow_r': V3(0.5, -0.8, -0.4),
        'spine': V3(-4, 0, 0), 'chest': V3(-2, 0, 0), 'head': V3(6, 0, 0),
    })
    if weapon in ('spear', 'cav_spear', 'javelin'):
        p.update({'hand_r': V3(0.30, 0.22, 1.00), 'w_main': V3(-50, 0, 0), 'elbow_r': V3(0.8, -0.6, -0.3)})
    elif weapon == 'lance':
        p.update({'hand_r': V3(0.28, 0.12, 0.98), 'w_main': V3(-52, 0, 0), 'elbow_r': V3(0.8, -0.6, -0.3)})
    elif weapon in ('sword', 'long_sword', 'sabre'):
        p.update({'hand_r': V3(0.30, 0.26, 1.08), 'w_main': V3(-25, 0, -5), 'elbow_r': V3(0.8, -0.6, -0.3)})
    elif weapon == 'rifle':
        p.update({'hand_r': V3(0.28, 0.22, 0.98), 'w_main': V3(-12, 0, 0), 'elbow_r': V3(0.8, -0.6, -0.3)})
    elif weapon == 'flag_pole':
        p.update({'hand_r': V3(0.30, 0.20, 1.04), 'w_main': V3(-8, 0, 0)})
    if offhand == 'bow':
        p.update({'hand_l': V3(-0.30, 0.30, 1.02), 'w_off': V3(-30, 0, -20), 'elbow_l': V3(-0.8, -0.5, -0.3)})
    elif offhand in ('shield_round', 'shield_oval', 'shield_kite'):
        p.update({'hand_l': V3(-0.22, 0.34, 1.02), 'elbow_l': V3(-0.8, -0.4, -0.4)})
    if stance:
        p.update({k: np.asarray(v, np.float32) for k, v in stance.items()})
    return p


def gallop_legs(t, amp=1.0):
    """Leg channels of a canter/gallop at cycle time t (0..1)."""
    out = {}
    phases = {'hl': 0.0, 'hr': 0.10, 'fl': 0.42, 'fr': 0.52}
    stance_len = 0.42
    for leg, ph in phases.items():
        u = (t - ph) % 1.0
        if u < stance_len:      # on the ground, sweeping back
            s = u / stance_len
            sw = (1 - 2 * s)        # +1 forward .. -1 back
            fold = 0.0
            push = smooth01((s - 0.6) / 0.4)
        else:                   # in the air, folded, coming forward
            s = (u - stance_len) / (1 - stance_len)
            sw = -math.cos(math.pi * s)
            fold = math.sin(math.pi * s)
            push = 0.0
        if leg[0] == 'f':
            out['h.' + leg] = V3(30 * sw * amp + 8 * fold, -95 * fold * amp, -40 * fold * amp + 25 * push, 0, 0)
        else:
            out['h.' + leg] = V3(28 * sw * amp - 8 * fold, -20 * fold * amp, 55 * fold * amp - 15 * push, -30 * fold * amp, 0)
    return out


def walk_legs(t, amp=1.0):
    """A walk: each leg in turn (lateral sequence)."""
    out = {}
    phases = {'hl': 0.0, 'fl': 0.25, 'hr': 0.5, 'fr': 0.75}
    for leg, ph in phases.items():
        u = (t - ph) % 1.0
        if u < 0.7:
            s = u / 0.7
            sw, fold = 1 - 2 * s, 0.0
        else:
            s = (u - 0.7) / 0.3
            sw, fold = -math.cos(math.pi * s), math.sin(math.pi * s)
        if leg[0] == 'f':
            out['h.' + leg] = V3(14 * sw * amp, -60 * fold * amp, -25 * fold * amp, 0, 0)
        else:
            out['h.' + leg] = V3(12 * sw * amp, -10 * fold * amp, 35 * fold * amp, -20 * fold * amp, 0)
    return out


REAR_LEGS = {'h.fl': V3(80, -115, -30, 0, 0), 'h.fr': V3(60, -125, -20, 0, 0),
             'h.hl': V3(-22, 10, -8, 0, 0), 'h.hr': V3(-30, 14, -10, 0, 0)}


class Act:
    def __init__(self, fn, loop=False, legs='ik', arms='ik', hide=()):
        self.fn, self.loop, self.legs, self.arms, self.hide = fn, loop, legs, arms, hide


def _k(base, ks, loop=False):
    return rig.Keys(ks, loop=loop, base=base)


def _ev(ctx, name, default, lo=0.2, hi=0.85):
    v = ctx.event(name, default) if ctx is not None else default
    if v is None:
        v = default
    return min(max(v, lo), hi)


# ------------------------------------------------------------ the archetype

@archetype('horse_rider')
class HorseRider:
    def __init__(self, unit, params):
        self.unit = unit
        self.p = params
        self.mats = M.Materials()
        rp = dict(params.get('rider', {}))
        self.weapon = rp.get('weapon')
        self.offhand = rp.get('offhand')
        self.rider = Crew(unit, rp, self.mats, 'r:')
        self.rs = params.get('rider_scale', RIDER_SCALE)
        self.horse = Horse(self.mats, params.get('horse', {}))
        self.attack = params.get('attack', 'thrust')
        self.base = dict(horse_rest())
        self.base.update(rider_stance(self.weapon, self.offhand, rp.get('stance')))
        self.escort = params.get('escort')
        if self.escort == 'dog':
            self.dog = Dog(self.mats, params.get('dog', {}))
        self._acts = {}

    def loops(self, action):
        return action in ('DEFAULT', 'RUN', 'WALK')

    # -- animations
    def action(self, action, ctx):
        n = ctx.n if ctx is not None else 15
        key = (action, n)
        if key in self._acts and ctx is None:
            return self._acts[key]
        B = self.base
        if action == 'DEFAULT':
            a = self.anim_idle()
        elif action in ('RUN', 'WALK'):
            a = self.anim_run()
        elif action.startswith('ATTACK'):
            style = self.p.get('attacks', {}).get(action, self.attack)
            a = self.anim_attack(style, ctx)
        elif action == 'DEATH':
            a = self.anim_death(ctx)
        elif action == 'FORTIFY':
            a = self.anim_fortify(ctx)
        elif action == 'FIDGET':
            a = self.anim_fidget(ctx)
        elif action == 'VICTORY':
            a = self.anim_victory(ctx)
        else:
            a = self.anim_idle()
        self._acts[key] = a
        return a

    def anim_idle(self):
        B = self.base

        def fn(t):
            p = dict(B)
            s, c = math.sin(2 * math.pi * t), math.cos(2 * math.pi * t)
            p['h.pos'] = V3(0, 0, 0.004 * s)
            p['h.neck'] = V3(2.5 * s, 0, 2 * c)
            p['h.head'] = V3(-3 * s, 0, 0)
            p['h.tail'] = V3(3 * c, 8 * s, 0)
            p['chest'] = B['chest'] + V3(1.2 * s, 0, 0)
            p['head'] = B['head'] + V3(0, 0, 4 * c)
            return p
        return Act(fn, loop=True)

    def anim_run(self):
        B = self.base

        def fn(t):
            p = dict(B)
            p.update(gallop_legs(t))
            ph = 2 * math.pi * t
            p['h.ground'] = 1.0
            p['h.pos'] = V3(0, 0, 0.03 * math.cos(ph - 0.6))
            p['h.rot'] = V3(4 * math.sin(ph + 0.8), 0, 0)
            p['h.neck'] = V3(-2 - 7 * math.sin(ph + 0.8), 0, 0)
            p['h.head'] = V3(4 + 6 * math.sin(ph), 0, 0)
            p['h.tail'] = V3(-18 + 8 * math.sin(ph), 0, 0)
            p['spine'] = V3(-14, 0, 0)
            p['chest'] = V3(-6 + 3 * math.sin(ph), 0, 0)
            p['head'] = V3(16, 0, 0)
            p['root_pos'] = V3(0, 0.04, 0.03 * math.cos(ph - 1.2))
            return p
        return Act(fn, loop=True)

    def anim_attack(self, style, ctx):
        B = self.base
        st = _ev(ctx, 'reach_max', 0.5, 0.3, 0.8)
        lunge = {'h.pos': V3(0, 0.12, 0), 'h.rot': V3(-3, 0, 0), 'h.neck': V3(-10, 0, 0), 'h.head': V3(8, 0, 0),
                 'h.fl': V3(18, -30, -10, 0, 0), 'h.hr': V3(-12, 0, 0, 0, 0)}
        if style == 'thrust':
            wind = {'hand_r': V3(0.34, -0.12, 1.30), 'w_main': V3(-70, 0, -8), 'chest': V3(4, 0, 18), 'spine': V3(2, 0, 8),
                    'elbow_r': V3(0.9, -0.3, 0.2), 'h.neck': V3(6, 0, 0), 'h.pos': V3(0, -0.04, 0)}
            hit = dict(lunge, **{'hand_r': V3(0.22, 0.78, 1.16), 'w_main': V3(-87, 0, 2), 'chest': V3(-14, 0, -6),
                                 'spine': V3(-10, 0, -4), 'root_pos': V3(0, 0.06, 0), 'elbow_r': V3(0.8, -0.6, -0.2)})
            k = [(0.0, {}), (0.3, wind), (0.5, hit), (0.62, hit), (0.85, {'hand_r': B['hand_r'], 'w_main': B['w_main'] + V3(-15, 0, 0)}),
                 (1.0, {c: B[c] for c in set(hit) | set(wind)})]
            w = [(0.5, st)]
        elif style == 'slash':
            up = {'hand_r': V3(0.34, 0.0, 1.72), 'w_main': V3(60, 0, 20), 'chest': V3(6, 0, 16), 'spine': V3(2, 0, 6),
                  'elbow_r': V3(0.9, -0.2, 0.3), 'head': V3(-4, 0, 0), 'h.neck': V3(4, 0, 0)}
            hit = dict(lunge, **{'hand_r': V3(0.50, 0.52, 0.86), 'w_main': V3(-120, 0, -10), 'chest': V3(-16, 0, -14),
                                 'spine': V3(-10, -8, -6), 'root_pos': V3(0.03, 0.05, -0.02), 'head': V3(18, 0, 0),
                                 'elbow_r': V3(0.8, -0.6, -0.2)})
            k = [(0.0, {}), (0.32, up), (0.52, hit), (0.64, dict(hit, hand_r=V3(0.50, 0.36, 0.80), w_main=V3(-150, 0, -10))),
                 (0.86, {'hand_r': B['hand_r'] + V3(0, 0, 0.1), 'w_main': B['w_main'], 'chest': V3(-2, 0, 0)}),
                 (1.0, {c: B[c] for c in set(hit) | set(up)})]
            w = [(0.52, st)]
        elif style == 'javelin':
            up = {'hand_r': V3(0.34, -0.20, 1.62), 'w_main': V3(-100, 0, 0), 'chest': V3(6, 0, 22), 'spine': V3(2, 0, 10),
                  'elbow_r': V3(0.9, -0.2, 0.3)}
            hit = dict(lunge, **{'hand_r': V3(0.26, 0.60, 1.10), 'w_main': V3(-118, 0, 0), 'chest': V3(-16, 0, -8),
                                 'spine': V3(-10, 0, -4), 'root_pos': V3(0, 0.05, -0.02)})
            k = [(0.0, {}), (0.3, up), (0.52, hit), (0.64, hit), (1.0, {c: B[c] for c in set(hit) | set(up)})]
            w = [(0.52, st)]
        elif style == 'shoot_gun':
            aim = {'hand_r': V3(0.12, 0.16, 1.40), 'w_main': V3(-92, 0, 4), 'hand_l': V3(0.02, 0.56, 1.46),
                   'chest': V3(-2, 0, -14), 'head': V3(10, 0, -14), 'elbow_r': V3(1, -0.2, -0.3), 'elbow_l': V3(-0.6, -0.6, -0.4)}
            kick = dict(aim, hand_r=V3(0.13, 0.10, 1.44), w_main=V3(-80, 0, 4), hand_l=V3(0.03, 0.48, 1.52),
                        chest=V3(4, 0, -14), **{'h.neck': V3(10, 0, 8), 'h.head': V3(-12, 0, 0), 'h.fr': V3(10, -20, 0, 0, 0)})
            k = [(0.0, {}), (0.28, aim), (0.46, aim), (0.52, kick), (0.66, aim), (0.84, aim),
                 (1.0, {c: B[c] for c in set(kick)})]
            w = [(0.5, st)]
        elif style == 'shoot_bow':
            aim = {'hand_l': V3(-0.22, 0.62, 1.42), 'w_off': V3(-4, 10, 0), 'chest': V3(0, 0, -28), 'spine': V3(0, 0, -10),
                   'head': V3(4, 0, 30), 'elbow_l': V3(-1, -0.2, -0.5)}
            k = [(0.0, {}), (0.22, dict(aim, hand_r=V3(-0.10, 0.52, 1.42), draw=0.0, elbow_r=V3(0.8, -0.3, 0.1))),
                 (0.5, dict(aim, hand_r=V3(0.12, 0.06, 1.48), draw=1.0, elbow_r=V3(1.0, -0.4, 0.3))),
                 (0.6, dict(aim, hand_r=V3(0.14, 0.04, 1.48), draw=1.0)),
                 (0.66, dict(aim, hand_r=V3(0.30, -0.10, 1.40), draw=0.0)),
                 (0.85, {'draw': 0.0, 'chest': V3(0, 0, -10), 'head': V3(4, 0, 10)}),
                 (1.0, {c: B[c] for c in ('hand_l', 'w_off', 'chest', 'spine', 'head', 'elbow_l', 'hand_r', 'elbow_r', 'draw')})]
            w = [(0.63, st)]
        else:
            raise ValueError(style)
        if self.p.get('attack_rear'):
            # the horse rears before the blow and comes down into it
            rear = dict(REAR_LEGS, **{'h.rot': V3(24, 0, 0), 'h.neck': V3(10, 0, 0), 'h.head': V3(-12, 0, 0)})
            hk = _k({c: B[c] for c in rear}, [(0.0, {}), (0.3, rear), (0.5, {c: B[c] for c in rear}), (1.0, {})])
            anim0 = _k(dict(B, draw=np.float32(0)), k)

            def anim(u):
                q = anim0(u)
                q.update(hk(u))
                return q
        else:
            anim = _k(dict(B, draw=np.float32(0)), k)
        return Act(lambda t: anim(warp(t, w)))

    def anim_death(self, ctx):
        """The horse rears, twists and falls on its left side; the rider is
        thrown off backward and lands on his back."""
        B = dict(self.base)
        down = _ev(ctx, 'down', 0.5, 0.3, 0.8)
        fk_seat = {'thigh_r': V3(78, -22, 0), 'shin_r': V3(-82, 0, 0), 'thigh_l': V3(78, 22, 0), 'shin_l': V3(-82, 0, 0),
                   'arm_r': V3(30, -20, 0), 'forearm_r': V3(50, 0, 0), 'arm_l': V3(30, 20, 0), 'forearm_l': V3(50, 0, 0)}
        B.update(fk_seat)
        B['h.pivot'] = V3(-0.25, -0.6, 0)
        rear = dict(REAR_LEGS, **{'h.rot': V3(22, 0, 0), 'h.neck': V3(12, 0, -6), 'h.head': V3(-15, 0, 0),
                                  'h.tail': V3(20, 0, 0), 'h.ground': 1.0})
        k = [
            (0.0, {}),
            (0.22, dict(rear, **{'r.free': 0.0, 'chest': V3(14, 0, 10), 'head': V3(24, 0, 0),
                                 'arm_r': V3(60, -70, 0), 'arm_l': V3(60, 70, 0), 'forearm_r': V3(40, 0, 0)})),
            (0.40, dict(rear, **{'h.rot': V3(20, -25, 10), 'r.free': 0.65, 'r.pos': V3(-0.35, -0.75, 1.75),
                                 'r.rot': V3(60, -10, 0), 'arm_r': V3(140, -60, 0), 'arm_l': V3(140, 60, 0),
                                 'thigh_r': V3(50, -30, 0), 'shin_r': V3(-40, 0, 0), 'thigh_l': V3(40, 30, 0), 'shin_l': V3(-30, 0, 0)})),
            (0.62, {'h.rot': V3(4, -70, -40), 'h.pos': V3(0.5, -0.55, 0.0), 'h.ground': 0.0, 'h.fl': V3(30, -60, -20, 0, 0), 'h.fr': V3(10, -70, -20, 0, 0),
                    'h.hl': V3(20, 10, 10, 0, 0), 'h.hr': V3(-10, 20, 20, 0, 0), 'h.neck': V3(-5, 0, 10), 'h.head': V3(0, 0, 0),
                    'r.free': 1.0, 'r.pos': V3(-0.75, -1.10, 0.35), 'r.rot': V3(88, -10, 10),
                    'arm_r': V3(150, -70, 0), 'arm_l': V3(150, 70, 0), 'thigh_r': V3(20, -20, 0), 'shin_r': V3(-20, 0, 0),
                    'thigh_l': V3(30, 20, 0), 'shin_l': V3(-30, 0, 0), 'chest': V3(0, 0, 0), 'head': V3(-10, 0, 0)}),
            (0.78, {'h.rot': V3(0, -90, -55), 'h.pos': V3(0.55, -0.85, 0.0), 'h.fl': V3(40, -40, -10, 0, 0), 'h.fr': V3(25, -50, -10, 0, 0),
                    'h.hl': V3(10, 20, 10, 0, 0), 'h.hr': V3(-15, 25, 15, 0, 0), 'h.neck': V3(-22, 0, 14), 'h.head': V3(10, 0, 0),
                    'h.tail': V3(-20, 0, 0), 'r.pos': V3(-0.80, -1.15, 0.14), 'r.rot': V3(92, -6, 10),
                    'arm_r': V3(165, -80, 0), 'arm_l': V3(160, 80, 0), 'forearm_r': V3(10, 0, 0), 'forearm_l': V3(15, 0, 0),
                    'thigh_r': V3(12, -14, 0), 'shin_r': V3(-12, 0, 0), 'thigh_l': V3(8, 16, 0), 'shin_l': V3(-16, 0, 0)}),
            (1.0, {'h.rot': V3(0, -90, -55), 'h.pos': V3(0.55, -0.85, 0.0), 'h.fl': V3(35, -35, -8, 0, 0), 'h.fr': V3(22, -45, -8, 0, 0),
                   'h.hl': V3(8, 18, 8, 0, 0), 'h.hr': V3(-14, 22, 12, 0, 0), 'h.neck': V3(-26, 0, 16), 'h.head': V3(14, 0, 0),
                   'r.pos': V3(-0.80, -1.15, 0.13), 'r.rot': V3(91, -6, 10)}),
        ]
        anim = _k(B, k)
        wk = _k({c: B[c] for c in ('w.drop', 'w.pos', 'w.rot')},
                [(0.0, {}), (0.30, {'w.drop': 0.0, 'w.pos': V3(0.3, -0.2, 2.3), 'w.rot': V3(-30, 0, 0)}),
                 (0.31, {'w.drop': 1.0, 'w.pos': V3(0.28, -0.3, 2.3), 'w.rot': V3(-20, 0, 5)}),
                 (0.46, {'w.pos': V3(0.1, -1.0, 1.6), 'w.rot': V3(40, 0, 20)}),
                 (0.60, {'w.pos': V3(-0.2, -1.5, 0.04), 'w.rot': V3(88, 0, 35)}),
                 (1.0, {'w.pos': V3(-0.2, -1.5, 0.04), 'w.rot': V3(88, 0, 35)})])

        def fn(t):
            u = warp(t, [(0.62, down)])
            p = anim(u)
            p.update(wk(u))
            p['w.drop'] = 1.0 if u > 0.305 else 0.0
            return p
        return Act(fn, legs='fk', arms='fk')

    def anim_fortify(self, ctx):
        B = self.base
        guard = {'chest': V3(-8, 0, -6), 'spine': V3(-6, 0, 0), 'head': V3(10, 0, 0), 'h.neck': V3(-8, 0, 0),
                 'h.head': V3(6, 0, 0), 'h.fl': V3(-6, 0, 0, 0, 0), 'h.hl': V3(8, 0, 0, 0, 0)}
        w = self.weapon
        if w in ('spear', 'cav_spear', 'javelin', 'lance'):
            guard.update({'hand_r': V3(0.30, 0.10, 1.10), 'w_main': V3(-82, 0, 0)})
        elif w in ('sword', 'long_sword', 'sabre'):
            guard.update({'hand_r': V3(0.30, 0.34, 1.26), 'w_main': V3(-40, 0, -10)})
        elif w == 'rifle':
            guard.update({'hand_r': V3(0.22, 0.20, 1.06), 'w_main': V3(-72, 0, 8), 'hand_l': V3(0.0, 0.48, 1.12)})
        if self.offhand in ('shield_round', 'shield_oval', 'shield_kite'):
            guard.update({'hand_l': V3(-0.16, 0.44, 1.14)})
        if self.offhand == 'bow':
            guard.update({'hand_l': V3(-0.24, 0.48, 1.16), 'w_off': V3(-10, 0, -10)})
        anim = _k(B, [(0.0, {}), (0.65, guard), (1.0, guard)])
        return Act(anim)

    def anim_fidget(self, ctx):
        """The horse tosses its head and paws the ground; the rider looks around."""
        B = self.base
        k = [(0.0, {}),
             (0.18, {'h.neck': V3(14, 0, 8), 'h.head': V3(-14, 0, 0), 'head': V3(6, 0, 30)}),
             (0.34, {'h.neck': V3(-12, 0, -6), 'h.head': V3(18, 0, 0), 'h.fr': V3(40, -70, -20, 0, 0)}),
             (0.48, {'h.fr': V3(10, -10, 0, 0, 0), 'head': V3(6, 0, -30), 'h.tail': V3(0, 25, 0)}),
             (0.62, {'h.fr': V3(36, -66, -20, 0, 0), 'h.neck': V3(-6, 0, 8), 'h.tail': V3(0, -25, 0)}),
             (0.80, {'h.fr': V3(0, 0, 0, 0, 0), 'h.neck': V3(2, 0, 0), 'h.head': V3(0, 0, 0), 'head': V3(6, 0, 0),
                     'h.tail': V3(0, 0, 0)}),
             (1.0, {c: B[c] for c in ('h.neck', 'h.head', 'h.fr', 'head', 'h.tail')})]
        anim = _k(B, k)
        return Act(anim)

    def anim_victory(self, ctx):
        B = self.base
        w = self.weapon
        up = {'hand_r': V3(0.30, 0.16, 2.02), 'w_main': V3(8, -10, 0), 'chest': V3(8, 0, 0), 'head': V3(-10, 0, 0),
              'elbow_r': V3(0.8, -0.3, 0.2)}
        if w in ('spear', 'cav_spear', 'javelin', 'lance', 'flag_pole'):
            up.update({'hand_r': V3(0.32, 0.16, 1.80), 'w_main': V3(-14, -6, 0)})
        elif w == 'rifle':
            up.update({'hand_r': V3(0.30, 0.16, 1.86), 'w_main': V3(-6, -8, 0)})
        if self.offhand == 'bow':
            up.update({'hand_l': V3(-0.30, 0.16, 1.92), 'w_off': V3(0, 20, 0), 'elbow_l': V3(-0.8, -0.3, 0.2)})
            if w is None:
                up.update({'hand_r': V3(0.40, 0.10, 1.40)})
        if self.p.get('victory_rear', True):
            rear = dict(REAR_LEGS, **{'h.rot': V3(34, 0, 0), 'h.ground': 1.0, 'h.neck': V3(10, 0, 0), 'h.head': V3(-12, 0, 0), 'h.tail': V3(15, 0, 0),
                                      'spine': V3(-16, 0, 0), 'chest': V3(-6, 0, 0)})
            k = [(0.0, {}), (0.18, dict(up, **{'h.neck': V3(10, 0, 0)})), (0.40, dict(up, **rear)), (0.62, dict(up, **rear)),
                 (0.84, dict(up, **{'h.rot': V3(0, 0, 0), 'h.fl': V3(10, -10, 0, 0, 0)})),
                 (1.0, {c: B[c] for c in set(up) | set(rear)})]
        else:
            half = {c: B[c] * 0.4 + np.asarray(v, np.float32) * 0.6 for c, v in up.items()}
            k = [(0.0, {}), (0.22, dict(up, **{'h.neck': V3(8, 0, 0), 'h.head': V3(-8, 0, 0)})), (0.42, half), (0.62, up),
                 (0.84, half), (1.0, {c: B[c] for c in up})]
        anim = _k(B, k)
        return Act(anim)

    # -- the model
    def frame(self, action, f, n, ctx):
        act = self.action(action, ctx)
        t = rig.frame_times(n, act.loop)[f]
        pose = act.fn(t)
        pose['_dog_t'] = t
        return self.pose_mesh(pose, act, action)

    def rider_transform(self, pose, frames):
        att = frames['body'] @ T(*SEAT) @ M.scale(self.rs) @ T(0, 0, -(H.PELVIS_H - PELVIS_SEAT) * 1.0)
        w = float(pose.get('r.free', 0.0))
        if w <= 1e-4:
            return att
        # attached pelvis position / rotation
        pel_att = (att @ np.array([0, 0, H.PELVIS_H, 1], np.float32))[:3]
        rot_att = np.asarray(pose['h.rot'], np.float32)
        pel = pel_att * (1 - w) + np.asarray(pose['r.pos'], np.float32) * w
        rot = rot_att * (1 - w) + np.asarray(pose['r.rot'], np.float32) * w
        return T(*pel) @ euler(rot) @ M.scale(self.rs) @ T(0, 0, -H.PELVIS_H)

    def pose_mesh(self, pose, act, action):
        meshes, frames = self.horse.pose(pose)
        R = self.rider_transform(pose, frames)
        dropped = float(pose.get('w.drop', 0.0)) > 0.5 and self.weapon is not None
        rm = self.rider.mesh(pose, act, None if dropped else self.weapon)
        meshes.append(rm.transformed(R))
        if dropped:
            wm = self.rider.prop_mesh(self.weapon)
            meshes.append(wm.transformed(T(*pose['w.pos']) @ euler(pose['w.rot']) @ M.scale(self.rs)))
        # reins from the bit to the rider's rein hand (while mounted)
        if float(pose.get('r.free', 0.0)) < 0.3 and act.arms == 'ik':
            hand = (R @ np.append(np.asarray(pose['hand_l'], np.float32), 1))[:3]
            if self.offhand == 'bow' or (self.offhand in ('shield_round', 'shield_oval', 'shield_kite')):
                hand = (R @ np.array([0.0, 0.40, 1.0, 1], np.float32))[:3]
            for sx in (1, -1):
                bit = (frames['head'] @ np.array([sx * 0.07, 0.38, -0.08, 1], np.float32))[:3]
                rm2 = self.horse.m_bridle if self.p.get('horse', {}).get('reins') == 'bridle' else self.horse.m_tack
                meshes.append(M.capsule(bit, hand + V3(sx * 0.03, 0, 0), 0.013, 0.013, rm2, seg=4, rings=1))
        if self.escort == 'dog':
            meshes.append(self.dog.mesh(action, pose, act))
        return M.Mesh.concat(meshes)


# ------------------------------------------------------------ the war dog (Conquistador)

class Dog:
    """A war dog standing (or trotting) at the horse's right side."""

    def __init__(self, mats, p):
        self.p = p
        mm = lambda name, col, **kw: P.mat(mats, name, col, **kw)  # noqa: E731
        c = mm('dog', p.get('coat', '#8a6236'), spec=0.08)
        d = mm('dog2', p.get('saddle', '#2a2420'), spec=0.08)
        col = mm('dog_collar', '#ffffff', tint=True)
        self.m, self.m2 = c, d
        self.place = np.asarray(p.get('place', (0.95, 0.35, 0.0)), np.float32)
        self.s = p.get('scale', 1.3)
        self.body = [M.ellipsoid(0.11, 0.18, 0.13, c, (0, 0.14, 0.50), seg=12),
                     M.ellipsoid(0.095, 0.17, 0.11, c, (0, -0.16, 0.50), seg=12),
                     M.ellipsoid(0.085, 0.20, 0.09, c, (0, -0.01, 0.48), seg=12),
                     M.ellipsoid(0.105, 0.26, 0.07, d, (0, -0.04, 0.565), seg=12),
                     M.capsule((0, 0.22, 0.56), (0, 0.34, 0.70), 0.07, 0.055, c, seg=10, rings=2),
                     tube_y([(0.24, 0.075, 0.07, 0.61), (0.30, 0.075, 0.07, 0.65)], col, seg=10, cap0=False, cap1=False)]
        hd = [M.ellipsoid(0.065, 0.08, 0.065, c, (0, 0.0, 0.0), seg=10),
              tube_y([(0.03, 0.045, 0.045, -0.02), (0.14, 0.03, 0.032, -0.035), (0.16, 0.018, 0.02, -0.035)], d, seg=8)]
        for sx in (1, -1):
            hd.append(M.capsule((sx * 0.035, -0.02, 0.04), (sx * 0.05, -0.04, 0.13), 0.022, 0.006, d, seg=6, rings=1))
            hd.append(M.ellipsoid(0.01, 0.01, 0.01, mm('h_eye', '#141010'), (sx * 0.035, 0.055, 0.02), seg=5))
        self.head = hd
        self.tail = M.capsule((0, -0.30, 0.54), (0, -0.48, 0.40), 0.03, 0.015, c, seg=6, rings=1)

    def mesh(self, action, pose, act):
        run = action in ('RUN', 'WALK')
        ph = float(pose.get('_dog_t', 0.0))
        W = T(*self.place) @ M.scale(self.s)
        bob = 0.02 * abs(math.sin(2 * math.pi * ph)) if run else 0.0
        out = [m.transformed(W @ T(0, 0, bob)) for m in self.body]
        Hh = W @ T(0, 0.37, 0.74 + bob) @ M.rot_x(8)
        out += [m.transformed(Hh) for m in self.head]
        out.append(self.tail.transformed(W))
        legs = {(0.07, 0.17): 0.0, (-0.07, 0.17): 0.5, (0.065, -0.20): 0.5, (-0.065, -0.20): 0.0}
        for (x, y), off in legs.items():
            a = 28 * math.sin(2 * math.pi * (ph + off)) if run else 0.0
            hind = y < 0
            J = W @ T(x, y, 0.46 + bob) @ M.rot_x(a + (12 if hind else 0))
            out.append(M.capsule((0, 0, 0.04), (0, 0, -0.22), 0.05, 0.03, self.m, seg=7, rings=1).transformed(J))
            J2 = J @ T(0, 0, -0.22) @ M.rot_x(-(12 if hind else 0) - max(0.0, a) * 0.9 - (12 if hind else 0) * 0)
            out.append(M.capsule((0, 0, 0), (0, 0, -0.21), 0.028, 0.024, self.m, seg=7, rings=1).transformed(J2))
            out.append(M.ellipsoid(0.03, 0.045, 0.02, self.m2, (0, 0.02, -0.215), seg=6).transformed(J2))
        return M.Mesh.concat(out)


# ------------------------------------------------------------ horse-only tracks (chariot teams)

def horse_track(action, ctx, side=1, phase=0.0):
    """(fn(t) -> horse pose channels, loops) for a harnessed horse. `side`
    (+1 right, -1 left) sets which way it falls; `phase` offsets its gait."""
    B = horse_rest()
    if action == 'DEFAULT':
        def fn(t):
            p = dict(B)
            s, c = math.sin(2 * math.pi * (t + phase)), math.cos(2 * math.pi * (t + phase))
            p['h.neck'] = V3(2.5 * s, 0, 2 * c)
            p['h.head'] = V3(-3 * s, 0, 0)
            p['h.tail'] = V3(3 * c, 8 * s, 0)
            return p
        return fn, True
    if action in ('RUN', 'WALK'):
        def fn(t):
            u = t + phase
            p = dict(B)
            p.update(gallop_legs(u % 1.0))
            ph = 2 * math.pi * u
            p['h.pos'] = V3(0, 0, 0.03 * math.cos(ph - 0.6))
            p['h.rot'] = V3(4 * math.sin(ph + 0.8), 0, 0)
            p['h.neck'] = V3(-2 - 7 * math.sin(ph + 0.8), 0, 0)
            p['h.head'] = V3(4 + 6 * math.sin(ph), 0, 0)
            p['h.tail'] = V3(-18 + 8 * math.sin(ph), 0, 0)
            return p
        return fn, True
    if action == 'DEATH':
        down = _ev(ctx, 'down', 0.5, 0.3, 0.8)
        Bd = dict(B, **{'h.pivot': V3(side * 0.25, 0.0, 0)})
        k = [(0.0, {}),
             (0.25, {'h.neck': V3(16, 0, side * 10), 'h.head': V3(-18, 0, 0), 'h.rot': V3(8, side * 4, 0),
                     'h.fl': V3(30, -40, 0, 0, 0), 'h.fr': V3(-10, 0, 0, 0, 0)}),
             (0.45, {'h.rot': V3(-14, side * 20, 0), 'h.fl': V3(-20, -110, -20, 0, 0), 'h.fr': V3(-30, -100, -20, 0, 0),
                     'h.hl': V3(10, 20, 0, 0, 0), 'h.hr': V3(12, 18, 0, 0, 0), 'h.neck': V3(-10, 0, side * 10),
                     'h.ground': 1.0}),
             (0.62, {'h.rot': V3(-4, side * 70, 0), 'h.pos': V3(-side * 0.55, 0, 0), 'h.ground': 0.0,
                     'h.fl': V3(30, -50, -20, 0, 0), 'h.fr': V3(10, -60, -20, 0, 0), 'h.hl': V3(20, 10, 10, 0, 0),
                     'h.hr': V3(-10, 20, 20, 0, 0), 'h.neck': V3(-10, 0, side * 6)}),
             (0.80, {'h.rot': V3(0, side * 90, 0), 'h.pos': V3(-side * 0.95, 0, 0), 'h.fl': V3(35, -35, -8, 0, 0),
                     'h.fr': V3(22, -45, -8, 0, 0), 'h.hl': V3(8, 18, 8, 0, 0), 'h.hr': V3(-14, 22, 12, 0, 0),
                     'h.neck': V3(-24, 0, -side * 14), 'h.head': V3(12, 0, 0), 'h.tail': V3(-20, 0, 0)}),
             (1.0, {'h.rot': V3(0, side * 90, 0), 'h.pos': V3(-side * 0.95, 0, 0), 'h.neck': V3(-26, 0, -side * 16)})]
        anim = _k(Bd, k)
        return (lambda t: anim(warp(t, [(0.62, down)]))), False
    if action.startswith('ATTACK'):
        st = _ev(ctx, 'reach_max', 0.5, 0.3, 0.8)
        hit = {'h.neck': V3(-12, 0, 0), 'h.head': V3(10, 0, 0), 'h.fr': V3(26, -50, -10, 0, 0), 'h.pos': V3(0, 0.06, 0)}
        anim = _k(B, [(0.0, {}), (0.3, {'h.neck': V3(10, 0, 0), 'h.head': V3(-10, 0, 0)}), (0.5, hit), (0.7, hit),
                      (1.0, {c: B[c] for c in hit})])
        return (lambda t: anim(warp(t, [(0.5, st)]))), False
    if action == 'FORTIFY':
        g = {'h.neck': V3(-8, 0, 0), 'h.head': V3(6, 0, 0), 'h.fl': V3(-6, 0, 0, 0, 0), 'h.hl': V3(8, 0, 0, 0, 0)}
        anim = _k(B, [(0.0, {}), (0.65, g), (1.0, g)])
        return anim, False
    if action == 'FIDGET':
        anim = _k(B, [(0.0, {}), (0.2 + phase, {'h.neck': V3(14, 0, 8), 'h.head': V3(-14, 0, 0)}),
                      (0.4 + phase, {'h.neck': V3(-12, 0, -6), 'h.head': V3(18, 0, 0), 'h.fr': V3(40, -70, -20, 0, 0)}),
                      (0.65 + phase, {'h.fr': V3(0, 0, 0, 0, 0), 'h.neck': V3(2, 0, 0), 'h.head': V3(0, 0, 0)}),
                      (1.0, {'h.neck': B['h.neck'], 'h.head': B['h.head'], 'h.fr': B['h.fr']})])
        return anim, False
    if action == 'VICTORY':
        up = {'h.neck': V3(14, 0, 0), 'h.head': V3(-16, 0, 0), 'h.fr': V3(45, -80, -20, 0, 0), 'h.rot': V3(6, 0, 0)}
        anim = _k(B, [(0.0, {}), (0.25, up), (0.45, {'h.fr': V3(0, 0, 0, 0, 0), 'h.neck': V3(4, 0, 0)}),
                      (0.65, dict(up, **{'h.fr': V3(0, 0, 0, 0, 0), 'h.fl': V3(45, -80, -20, 0, 0)})),
                      (0.85, {'h.fl': V3(0, 0, 0, 0, 0), 'h.neck': V3(0, 0, 0), 'h.head': V3(0, 0, 0), 'h.rot': V3(0, 0, 0)}),
                      (1.0, {c: B[c] for c in ('h.neck', 'h.head', 'h.fr', 'h.fl', 'h.rot')})])
        return anim, False
    return horse_track('DEFAULT', ctx, side, phase)


# ------------------------------------------------------------ chariots

def ring_mesh(R, r, m, k=18):
    """A torus-like ring of capsules in the y-z plane (a wheel rim)."""
    out = []
    for i in range(k):
        a0, a1 = 2 * math.pi * i / k, 2 * math.pi * (i + 1) / k
        out.append(M.capsule((0, R * math.cos(a0), R * math.sin(a0)), (0, R * math.cos(a1), R * math.sin(a1)),
                             r, r, m, seg=6, rings=1))
    return M.Mesh.concat(out)


def wheel(R, spokes, rim_m, spoke_m, hub_m, tire_r=0.03):
    parts = [ring_mesh(R, tire_r, rim_m, k=20)]
    for i in range(spokes):
        a = 2 * math.pi * i / spokes
        parts.append(M.capsule((0, 0, 0), (0, R * math.cos(a), R * math.sin(a)), 0.016, 0.014, spoke_m, seg=5, rings=1))
    parts.append(M.cylinder(0.06, 0.05, 0.16, hub_m, seg=10, z0=-0.08).transformed(M.rot_y(90)))
    return M.Mesh.concat(parts)


def curved_wall(rx, ry, cy, a0, a1, z0, z1, m, n=12, lean=0.0):
    """A thin vertical sheet along an elliptical arc (angles from +x through
    +y), from z0 to z1; `lean` widens its top outward."""
    angs = np.linspace(a0, a1, n + 1)
    V = []
    for a in angs:
        c, s = math.cos(a), math.sin(a)
        V.append((rx * c, cy + ry * s, z0))
        V.append((rx * c * (1 + lean), cy + ry * s * (1 + lean), z1))
    V = np.asarray(V, np.float32)
    faces = []
    for i in range(n):
        a, b, c2, d = 2 * i, 2 * i + 1, 2 * i + 2, 2 * i + 3
        faces += [(a, c2, d), (a, d, b)]
    return M.from_indexed(V, np.asarray(faces), m)


class Car:
    """The chariot car on its axle (car space: axle center on the ground at
    the origin, facing +y)."""

    def __init__(self, mats, p):
        self.p = p
        mm = lambda name, col, **kw: P.mat(mats, name, col, **kw)  # noqa: E731
        kind = p.get('kind', 'light')
        self.R = R = p.get('wheel_r', 0.46)
        self.track = W = p.get('track', 0.62)
        body = mm('car', p.get('body', '#ffffff'), tint=p.get('body_tint', True), spec=0.15)
        trim = mm('car_trim', p.get('trim', '#d8b040'), spec=0.7, gloss=22, metal=True)
        wood = mm('car_wood', p.get('wood', '#8a5a30'), spec=0.1)
        rim = mm('wheel_rim', p.get('rim', p.get('trim', '#d8b040')), spec=0.5, gloss=18, metal=p.get('rim_metal', True))
        spoke = mm('wheel_spoke', p.get('spoke', p.get('trim', '#d8b040')), spec=0.3, metal=p.get('rim_metal', True))
        self.wheel = wheel(R, p.get('spokes', 6), rim, spoke, wood)
        floor = R + 0.04
        self.floor = floor
        parts = [M.rounded_box(2 * W - 0.12, 0.62, 0.05, wood, (0, 0.02, floor - 0.02), r=0.02, seg=8)]
        parts.append(M.capsule((-W - 0.06, 0, R), (W + 0.06, 0, R), 0.03, 0.03, wood, seg=8, rings=1))   # axle
        if kind == 'light':
            h = p.get('wall_h', 0.55)
            # a D-shaped breastwork: open at the back, curving around the front
            parts.append(curved_wall(W - 0.06, 0.32, -0.02, -0.15, math.pi + 0.15, floor, floor + h, body, n=16, lean=0.06))
            k = 16
            top = []
            for i in range(k + 1):
                a = -0.15 + (math.pi + 0.3) * i / k
                top.append(((W - 0.06) * 1.06 * math.cos(a), -0.02 + 0.32 * 1.06 * math.sin(a), floor + h))
            for a2, b2 in zip(top[:-1], top[1:]):
                parts.append(M.capsule(a2, b2, 0.022, 0.022, trim, seg=6, rings=1))
            for sx in (1, -1):
                parts.append(M.capsule((sx * (W - 0.06) * 1.06, -0.06, floor + h), (sx * (W - 0.1), -0.30, floor + 0.05),
                                       0.018, 0.018, trim, seg=6, rings=1))
        else:   # 'heavy': a wooden box with emblem panels
            h = p.get('wall_h', 0.62)
            emb = mm('car_emblem', '#ffffff', tint=True)
            for sx in (1, -1):
                parts.append(M.rounded_box(0.05, 0.70, h, wood, (sx * (W - 0.05), 0.0, floor + h / 2), r=0.015, seg=6))
                parts.append(M.rounded_box(0.02, 0.44, h * 0.55, emb, (sx * (W - 0.02), 0.0, floor + h * 0.5), r=0.01, seg=6))
                parts.append(M.capsule((sx * (W - 0.05), -0.36, floor + h), (sx * (W - 0.05), 0.36, floor + h), 0.025, 0.025,
                                       trim, seg=6, rings=1))
            parts.append(M.rounded_box(2 * W - 0.1, 0.05, h, wood, (0, 0.34, floor + h / 2), r=0.015, seg=6))
            parts.append(M.rounded_box(2 * W * 0.6, 0.02, h * 0.55, emb, (0, 0.37, floor + h * 0.5), r=0.01, seg=6))
            parts.append(M.capsule((-W + 0.05, 0.36, floor + h), (W - 0.05, 0.36, floor + h), 0.025, 0.025, trim, seg=6, rings=1))
        self.parts = parts
        self.m_pole = wood
        self.m_trim = trim

    def mesh(self, Tc, spin):
        out = [m.transformed(Tc) for m in self.parts]
        for sx in (1, -1):
            out.append(self.wheel.transformed(Tc @ T(sx * self.track, 0, self.R) @ M.rot_x(spin)))
        return out


@archetype('chariot')
class Chariot:
    """A chariot: one or two harnessed horses, the car and its crew (humanoids
    standing in the car, animated with the humanoid library)."""

    def __init__(self, unit, params):
        self.unit = unit
        self.p = params
        self.mats = M.Materials()
        hp = params.get('horse', {})
        self.horse = Horse(self.mats, hp)
        n = params.get('horses', 2)
        self.horse_x = [0.0] if n == 1 else [-0.36, 0.36]
        self.horse_y = params.get('horse_y', 0.55)
        self.car = Car(self.mats, params.get('car', {}))
        self.axle_y = params.get('axle_y', -1.35)
        self.crew = []
        for i, c in enumerate(params.get('crew', [])):
            self.crew.append((Crew(unit, dict(c), self.mats, f'c{i}:'), np.asarray(c.get('pos', (0, 0)), np.float32), c))
        self.cs = params.get('crew_scale', RIDER_SCALE)

    def loops(self, action):
        return action in ('DEFAULT', 'RUN', 'WALK')

    def frame(self, action, f, n, ctx):
        loop = self.loops(action)
        t = rig.frame_times(n, loop)[f]
        meshes = []
        bits, bodies = [], []
        for i, x in enumerate(self.horse_x):
            side = -1 if x <= 0 else 1
            fn, _ = horse_track(action, ctx, side=side, phase=0.06 * i)
            if action == 'DEATH' and i == 1:
                hp = fn(max(0.0, t - 0.08))   # the second horse falls a moment later
            else:
                hp = fn(t)
            hp['h.pos'] = np.asarray(hp['h.pos'], np.float32) + V3(x, self.horse_y, 0)
            ms, fr = self.horse.pose(hp)
            meshes += ms
            bits.append([(fr['head'] @ np.array([sx * 0.07, 0.38, -0.08, 1], np.float32))[:3] for sx in (1, -1)])
            bodies.append(fr['body'])
        Tc, spin = self.car_transform(action, ctx, t)
        meshes += self.car.mesh(Tc, spin)
        # pole and yoke (two horses) or shafts (one)
        front = (Tc @ np.array([0, 0.36, self.car.floor + 0.02, 1], np.float32))[:3]
        pm = self.car.m_pole
        if len(self.horse_x) == 2:
            yk = [(Bh @ np.array([0, 0.40, 1.60, 1], np.float32))[:3] for Bh in bodies]
            mid = (yk[0] + yk[1]) / 2
            meshes.append(M.capsule(front, mid - V3(0, 0.05, 0.12), 0.035, 0.03, pm, seg=7, rings=1))
            meshes.append(M.capsule(mid - V3(0, 0.05, 0.12), mid, 0.03, 0.03, pm, seg=7, rings=1))
            meshes.append(M.capsule(yk[0], yk[1], 0.03, 0.03, pm, seg=7, rings=1))
        else:
            Bh = bodies[0]
            for sx in (1, -1):
                a = (Tc @ np.array([sx * 0.30, 0.30, self.car.floor + 0.02, 1], np.float32))[:3]
                b = (Bh @ np.array([sx * 0.30, 0.45, 1.24, 1], np.float32))[:3]
                meshes.append(M.capsule(a, b, 0.03, 0.026, pm, seg=7, rings=1))
            rx, rz, zc = barrel_at(0.40)
            meshes.append(tube_y([(0.36, rx + 0.02, rz + 0.02, zc), (0.44, rx + 0.02, rz + 0.02, zc)], self.horse.m_tack,
                                 seg=14, cap0=False, cap1=False).transformed(Bh))
        # the crew
        hands = None
        for crew, pos, c in self.crew:
            h = crew.h
            act = h.action(action, ctx)
            tt = rig.frame_times(n, act.loop)[f]
            pose = dict(act.fn(tt))
            if action in ('RUN', 'WALK'):
                pose['root_pos'] = np.asarray(pose['root_pos'], np.float32) + V3(0, 0, 0.02 * math.sin(4 * math.pi * t))
            weapon = H.WORK_TOOLS.get(h.anim_name(action), h.weapon)
            Tm = Tc @ T(pos[0], pos[1], self.car.floor) @ M.scale(self.cs)
            m = h.pose_mesh(pose, act, weapon)
            if len(m):
                m.M = crew._sync()[m.M]
            meshes.append(m.transformed(Tm))
            if c.get('driver') and act.arms == 'ik':
                hands = [(Tm @ np.append(np.asarray(pose['hand_l'], np.float32), 1))[:3],
                         (Tm @ np.append(np.asarray(pose['hand_r'], np.float32), 1))[:3]]
        if hands is not None and action != 'DEATH':
            for hb in bits:
                for j, b in enumerate(hb):
                    meshes.append(M.capsule(b, hands[j], 0.011, 0.011, self.horse.m_tack, seg=4, rings=1))
        return M.Mesh.concat(meshes)

    def car_transform(self, action, ctx, t):
        base = T(0, self.axle_y, 0)
        if action in ('RUN', 'WALK'):
            return base @ T(0, 0, 0.015 * abs(math.sin(2 * math.pi * t))), -720.0 * t
        if action == 'DEATH':
            down = _ev(ctx, 'down', 0.5, 0.3, 0.8)
            u = warp(t, [(0.62, down)])
            k = _k({'rot': V3(0, 0, 0), 'pos': V3(0, 0, 0)},
                   [(0.0, {}), (0.3, {'rot': V3(-6, 0, 0)}), (0.5, {'rot': V3(-12, 18, 8), 'pos': V3(0.05, 0.1, 0)}),
                    (0.7, {'rot': V3(-10, 40, 12), 'pos': V3(0.1, 0.2, 0)}), (0.85, {'rot': V3(-9, 44, 12), 'pos': V3(0.12, 0.2, 0)}),
                    (1.0, {'rot': V3(-9, 43, 12), 'pos': V3(0.12, 0.2, 0)})])
            q = k(u)
            piv = V3(self.car.track, 0, 0)
            return base @ T(*q['pos']) @ T(*piv) @ euler(q['rot']) @ T(*(-piv)), 0.0
        return base, 0.0


# ------------------------------------------------------------ the war elephant

ELE_DZ = -0.30
ELE_L = 1.22
ELE_BODY = [(y * ELE_L, rx, rz, zc + ELE_DZ) for y, rx, rz, zc in
            [(-1.10, 0.10, 0.12, 1.92), (-1.02, 0.44, 0.42, 1.88), (-0.80, 0.66, 0.60, 1.84), (-0.40, 0.74, 0.66, 1.82),
             (0.10, 0.76, 0.68, 1.84), (0.55, 0.72, 0.68, 1.92), (0.85, 0.56, 0.60, 1.98), (1.00, 0.34, 0.42, 2.03),
             (1.06, 0.08, 0.12, 2.03)]]
ELE_LEGS = {'fl': (-0.44, 0.74), 'fr': (0.44, 0.74), 'hl': (-0.44, -0.80), 'hr': (0.44, -0.80)}
ELE_SEGS = {'f': [(0.60, 0.0, 0.27, 0.22), (0.50, 0.0, 0.21, 0.21)], 'h': [(0.58, 6.0, 0.28, 0.22), (0.50, -6.0, 0.21, 0.21)]}
ELE_FOOT = 0.10


class Elephant:
    def __init__(self, mats, p):
        self.p = p
        self.mats = mats
        mm = lambda name, col, **kw: P.mat(mats, name, col, **kw)  # noqa: E731
        c = mm('ele_skin', p.get('skin', '#8a8070'), spec=0.06)
        dark = mm('ele_dark', p.get('skin_dark', '#6a6052'), spec=0.05)
        ivory = mm('ivory', '#ece4cc', spec=0.3, gloss=20)
        gold = mm('gold', p.get('gold', '#d8b040'), spec=0.75, gloss=24, metal=True)
        steel = mm('shield_steel', '#b4b8be', spec=0.85, gloss=30, metal=True)
        band = mm('tusk_band', '#ffffff', tint=True)
        cloth = mm('blanket', '#ffffff', tint=True)
        wood = mm('howdah_wood', p.get('wood', '#5a3a22'), spec=0.1)
        self.m_skin = c
        self.leg_root_z = {}
        for leg in ELE_LEGS:
            ang, z = 0.0, 0.0
            for L, a, *_ in ELE_SEGS[leg[0]]:
                ang += a
                z += L * math.cos(math.radians(ang))
            self.leg_root_z[leg] = z + ELE_FOOT
        self.leg_meshes = {}
        for kind, segs in ELE_SEGS.items():
            ms = []
            for L, a, r0, r1 in segs:
                ms.append(M.Mesh.concat([M.capsule((0, 0, 0), (0, 0, -L), r0, r1, c, seg=12, rings=2),
                                         M.sphere(r1 * 1.05, c, (0, 0, -L), seg=10)]))
            ms.append(M.lathe([(0.0, 0.0005), (0.0, 0.25), (ELE_FOOT, 0.23), (ELE_FOOT + 0.01, 0.0005)], dark, seg=14)
                      .transformed(T(0, 0.01, -ELE_FOOT)))
            self.leg_meshes[kind] = ms
        body = [tube_y(ELE_BODY, c, seg=20)]
        for sx in (1, -1):
            body.append(M.ellipsoid(0.30, 0.50, 0.52, c, (sx * 0.42, -0.76, 1.76 + ELE_DZ), seg=14))
            body.append(M.ellipsoid(0.30, 0.42, 0.52, c, (sx * 0.42, 0.72, 1.82 + ELE_DZ), seg=14))
        # caparison (civ color, gold trim) over the middle of the back
        cy0, cy1 = p.get('cloth_span', (-0.70, 0.56))
        cdrop = p.get('cloth_drop', 0.48)
        hem = hem_fn(10, 16, cdrop, p.get('hem', 1))
        body.append(drape(cy0, cy1, lambda j, i, e: gold if hem(j, i, e) else cloth, g=0.04, drop=cdrop, ny=10, na=16,
                          flare=0.04, table=ELE_BODY))
        # howdah: a wooden tower under a civ-colored canopy, with shields
        top = 1.84 + 0.68 + ELE_DZ
        hw = 0.50
        hd = [M.rounded_box(2 * hw, 1.10, 0.10, wood, (0, -0.05, top + 0.05), r=0.03, seg=8)]
        for sx in (1, -1):
            hd.append(M.rounded_box(0.07, 1.10, 0.62, wood, (sx * hw, -0.05, top + 0.38), r=0.02, seg=6))
        for sy in (1, -1):
            hd.append(M.rounded_box(2 * hw, 0.07, 0.62, wood, (0, -0.05 + sy * 0.53, top + 0.38), r=0.02, seg=6))
        for sx in (1, -1):
            for sy in (1, -1):
                hd.append(M.cylinder(0.04, 0.04, 0.22, wood, seg=6, z0=top + 0.66).transformed(T(sx * (hw - 0.03), -0.05 + sy * 0.50, 0)))
        ch = p.get('canopy_h', 0.24)
        cw = p.get('canopy_w', 1.0)
        hd.append(M.rounded_box((2 * hw + 0.12) * cw, 1.22 * cw, ch, cloth, (0, -0.05, top + 0.88 + ch / 2), r=0.04, seg=8))
        hd.append(M.rounded_box(2 * hw + 0.16, 1.26, 0.05, gold, (0, -0.05, top + 0.88), r=0.02, seg=8))
        if p.get('canopy_band'):   # a gold band around the top of the canopy
            hd.append(M.rounded_box((2 * hw + 0.12) * cw + 0.03, 1.22 * cw + 0.03, 0.04, gold, (0, -0.05, top + 0.88 + ch - 0.03),
                                    r=0.02, seg=8))
        for sx in (1, -1):
            sh = M.disc(0.27, 0.27, 0.03, steel, seg=20, dome=0.09).transformed(M.rot_z(90 * sx))
            hd.append(sh.transformed(T(sx * (hw + 0.06), -0.05, top + 0.40)))
            hd.append(ring_mesh(0.27, 0.025, gold, k=18).transformed(T(sx * (hw + 0.07), -0.05, top + 0.40)))
        for k2, dz in enumerate((0.0, 0.12)):   # spears racked along the right side, points forward
            sp = PM.cav_spear(mats, length=2.2, grip=1.0)
            hd.append(sp.transformed(T(hw - 0.12 + 0.06 * k2, -0.10, top + 0.72 + dz) @ M.rot_x(-72)))
        self.body_parts = body + hd
        # head (head space: origin at the neck joint, looking +y)
        self.head_root = V3(0, 1.18, 2.05 + ELE_DZ)
        hp = [M.ellipsoid(0.50, 0.50, 0.58, c, (0, 0.18, 0.10), seg=16),
              M.ellipsoid(0.34, 0.27, 0.32, c, (0, 0.36, 0.38), seg=12)]
        for sx in (1, -1):
            ear = M.ellipsoid(0.07, 0.48, 0.58, c, (0, 0, 0), seg=12)
            hp.append(ear.transformed(T(sx * 0.52, 0.0, 0.06) @ M.rot_z(sx * 30) @ M.rot_x(-8)))
            hp.append(M.ellipsoid(0.03, 0.035, 0.03, mm('h_eye', '#141010'), (sx * 0.26, 0.48, 0.20), seg=6))
            # tusks: curving forward and up, a gold tip and a civ-colored band
            pts = [(sx * 0.19, 0.48, -0.14), (sx * 0.22, 0.66, -0.46), (sx * 0.22, 0.92, -0.68), (sx * 0.20, 1.20, -0.74)]
            rr = [0.075, 0.062, 0.05, 0.03]
            for i in range(3):
                hp.append(M.capsule(pts[i], pts[i + 1], rr[i], rr[i + 1], gold if i == 2 else ivory, seg=8, rings=1))
            a, b = np.asarray(pts[1], np.float32), np.asarray(pts[2], np.float32)
            dvec = (b - a) / np.linalg.norm(b - a)
            hp.append(M.cylinder(0.064, 0.064, 1.0, band, seg=10).transformed(M.look_along(a + dvec * 0.02, a + dvec * 0.10)))
        hp.append(M.disc(0.17, 0.17, 0.03, steel, seg=18, dome=0.06).transformed(T(0, 0.58, 0.22) @ M.rot_z(180) @ M.rot_x(-20)))
        self.head_parts = hp
        self.trunk_root = V3(0, 0.50, 0.08)
        self.trunk_seg = [0.26, 0.25, 0.23, 0.21, 0.18, 0.15]
        self.trunk_r = [0.18, 0.15, 0.125, 0.10, 0.085, 0.07, 0.055]
        self.tail_root = V3(0, -1.30, 2.0 + ELE_DZ)
        self.tail = [M.capsule((0, 0, 0), (0, -0.08, -0.65), 0.04, 0.025, c, seg=7, rings=1),
                     M.ellipsoid(0.04, 0.04, 0.09, dark, (0, -0.09, -0.70), seg=6)]

    def hoof_points(self, B, hp):
        out = []
        for leg, (x, y) in ELE_LEGS.items():
            d = hp['h.' + leg]
            J = B @ T(x, y, self.leg_root_z[leg])
            for i, (L, a, *_) in enumerate(ELE_SEGS[leg[0]]):
                J = J @ M.rot_x(a + float(d[i])) @ T(0, 0, -L)
            for py in (-0.15, 0.15):
                out.append((J @ np.array([0, py, -ELE_FOOT, 1], np.float32))[:3])
        return np.asarray(out)

    def pose(self, hp):
        pos, rot, piv = hp['h.pos'], hp['h.rot'], hp['h.pivot']
        B = T(*pos) @ T(*piv) @ euler(rot) @ T(*(-piv))
        g = float(hp.get('h.ground', 0.0))
        if g > 0:
            B = T(0, 0, -g * min(self.hoof_points(B, hp)[:, 2])) @ B
        out = [m.transformed(B) for m in self.body_parts]
        Hd = B @ T(*self.head_root) @ euler(hp['h.head'])
        out += [m.transformed(Hd) for m in self.head_parts]
        # trunk: a chain hanging from the face, curling by h.trunk (degrees, + curls it forward)
        curl = float(hp.get('h.trunk', 0.0))
        J = Hd @ T(*self.trunk_root) @ M.rot_x(-12)
        for i, L in enumerate(self.trunk_seg):
            J = J @ M.rot_x(curl * (0.6 + 0.15 * i) + (6 if i > 3 else 0))
            out.append(M.capsule((0, 0, 0), (0, 0, -L), self.trunk_r[i], self.trunk_r[i + 1], self.m_skin, seg=10,
                                 rings=1).transformed(J))
            J = J @ T(0, 0, -L)
        Tl = B @ T(*self.tail_root) @ euler(hp['h.tail'])
        out += [m.transformed(Tl) for m in self.tail]
        for leg, (x, y) in ELE_LEGS.items():
            segs = ELE_SEGS[leg[0]]
            d = hp['h.' + leg]
            J = B @ T(x, y, self.leg_root_z[leg])
            ms = self.leg_meshes[leg[0]]
            tot = 0.0
            for i, (L, a, *_) in enumerate(segs):
                J = J @ M.rot_x(a + float(d[i]))
                tot += a + float(d[i])
                out.append(ms[i].transformed(J))
                J = J @ T(0, 0, -L)
            J = J @ M.rot_x(-tot * 0.7)
            out.append(ms[-1].transformed(J))
        return out, {'body': B, 'head': Hd}


@archetype('elephant')
class WarElephant:
    def __init__(self, unit, params):
        self.unit = unit
        self.p = params
        self.mats = M.Materials()
        self.ele = Elephant(self.mats, params.get('elephant', {}))

    def loops(self, action):
        return action in ('DEFAULT', 'RUN', 'WALK')

    def base(self):
        b = horse_rest()
        b['h.trunk'] = np.float32(0)
        b['h.pivot'] = V3(0, -0.80, 0)
        return b

    def track(self, action, ctx):
        B = self.base()
        if action == 'DEFAULT':
            def fn(t):
                p = dict(B)
                s, c = math.sin(2 * math.pi * t), math.cos(2 * math.pi * t)
                p['h.head'] = V3(2 * s, 0, 2 * c)
                p['h.trunk'] = np.float32(4 + 5 * s)
                p['h.tail'] = V3(0, 10 * c, 0)
                return p
            return fn, True
        if action in ('RUN', 'WALK'):
            def fn(t):
                p = dict(B)
                for k2, v in walk_legs(t, 1.0).items():   # two-segment legs: the knee bends less
                    p[k2] = V3(v[0] * 1.9, v[1] * 0.7 if k2[2] == 'f' else -v[2] * 0.6, 0, 0, 0)
                ph = 2 * math.pi * t
                p['h.ground'] = 1.0
                p['h.rot'] = V3(0, 2.5 * math.sin(ph), 1.5 * math.sin(ph))
                p['h.head'] = V3(3 * math.sin(2 * ph), 0, 0)
                p['h.trunk'] = np.float32(6 + 8 * math.sin(ph))
                p['h.tail'] = V3(0, 12 * math.sin(ph), 0)
                return p
            return fn, True
        if action.startswith('ATTACK'):
            st = _ev(ctx, 'reach_max', 0.5, 0.3, 0.8)
            up = {'h.head': V3(30, 0, 0), 'h.trunk': -30.0, 'h.rot': V3(8, 0, 0), 'h.fr': V3(24, -20, 0, 0, 0)}
            hit = {'h.head': V3(-24, 0, 0), 'h.trunk': 14.0, 'h.pos': V3(0, 0.45, 0), 'h.rot': V3(-5, 0, 0),
                   'h.fr': V3(20, 0, 0, 0, 0), 'h.hl': V3(-14, 0, 0, 0, 0)}
            anim = _k(dict(B, **{'h.ground': 1.0}), [(0.0, {}), (0.3, up), (0.52, hit), (0.66, hit),
                                                     (1.0, {c: B[c] for c in set(up) | set(hit)})])
            return (lambda t: anim(warp(t, [(0.52, st)]))), False
        if action == 'DEATH':
            down = min(_ev(ctx, 'down', 0.55, 0.3, 0.8), 0.62)
            Bd = dict(B, **{'h.pivot': V3(0.55, -0.80, 0)})
            k = [(0.0, {}),
                 (0.25, {'h.rot': V3(16, 0, 0), 'h.head': V3(24, 0, 0), 'h.trunk': -28.0, 'h.fl': V3(30, -10, 0, 0, 0),
                         'h.fr': V3(40, -20, 0, 0, 0), 'h.ground': 1.0}),
                 (0.45, {'h.rot': V3(4, 25, -6), 'h.head': V3(10, 0, -10), 'h.trunk': -10.0, 'h.fl': V3(10, -40, 0, 0, 0),
                         'h.fr': V3(-10, -30, 0, 0, 0)}),
                 (0.65, {'h.rot': V3(0, 70, -4), 'h.pos': V3(-0.9, 0, 0), 'h.ground': 0.0, 'h.head': V3(-6, 0, -16),
                         'h.trunk': 18.0, 'h.fl': V3(10, -10, 0, 0, 0), 'h.fr': V3(20, -10, 0, 0, 0)}),
                 (0.82, {'h.rot': V3(0, 88, -4), 'h.pos': V3(-1.45, 0, 0), 'h.head': V3(-10, 0, -20), 'h.trunk': 22.0,
                         'h.fl': V3(20, -6, 0, 0, 0), 'h.fr': V3(30, -10, 0, 0, 0), 'h.hl': V3(10, 0, 0, 0, 0)}),
                 (1.0, {'h.rot': V3(0, 88, -4), 'h.pos': V3(-1.45, 0, 0), 'h.trunk': 24.0})]
            anim = _k(Bd, k)
            return (lambda t: anim(warp(t, [(0.65, down)]))), False
        if action == 'FORTIFY':
            g = {'h.head': V3(-8, 0, 0), 'h.trunk': 20.0, 'h.fl': V3(-6, 0, 0, 0, 0), 'h.hr': V3(8, 0, 0, 0, 0)}
            return _k(B, [(0.0, {}), (0.65, g), (1.0, g)]), False
        if action == 'FIDGET':
            anim = _k(B, [(0.0, {}), (0.2, {'h.head': V3(6, 0, 14), 'h.trunk': -14.0}),
                          (0.45, {'h.head': V3(-4, 0, -12), 'h.trunk': 18.0, 'h.fl': V3(16, -24, 0, 0, 0)}),
                          (0.65, {'h.fl': V3(0, 0, 0, 0, 0), 'h.head': V3(0, 0, 4), 'h.trunk': 6.0}),
                          (1.0, {'h.head': B['h.head'], 'h.trunk': B['h.trunk'], 'h.fl': B['h.fl']})])
            return anim, False
        if action == 'VICTORY':
            rear = {'h.rot': V3(14, 0, 0), 'h.head': V3(24, 0, 0), 'h.trunk': -30.0, 'h.fl': V3(40, -40, 0, 0, 0),
                    'h.fr': V3(30, -30, 0, 0, 0)}
            anim = _k(dict(B, **{'h.ground': 1.0}),
                      [(0.0, {}), (0.3, rear), (0.6, dict(rear, **{'h.trunk': -36.0})),
                       (0.85, {'h.rot': V3(0, 0, 0), 'h.head': V3(4, 0, 0), 'h.trunk': 0.0, 'h.fl': V3(0, 0, 0, 0, 0),
                               'h.fr': V3(0, 0, 0, 0, 0)}),
                       (1.0, {c: B[c] for c in rear})])
            return anim, False
        return self.track('DEFAULT', ctx)

    def frame(self, action, f, n, ctx):
        fn, loop = self.track(action, ctx)
        t = rig.frame_times(n, loop)[f]
        ms, _ = self.ele.pose(fn(t))
        return M.Mesh.concat(ms)


# ------------------------------------------------------------ the Crusader (a knight on foot)

@archetype('foot_knight')
class FootKnight(H.Humanoid):
    """A humanoid with the mounted units' extra gear (mail sleeves and
    trousers, head gear) and a civ-colored cross on its surcoat."""

    def __init__(self, unit, params):
        super().__init__(unit, params)
        self.parts += rider_extras(self.body, params)
        if params.get('cross'):
            cm = P.mat(self.mats, 'cross', '#ffffff', tint=True)
            tw = H.TORSO_W * H.build_params(params).get('bulk', 1.0)
            k = params.get('cross_scale', 1.0)
            for sy in (1, -1):
                y = sy * (0.122 * (1 + (tw - 1) * 0.6) + 0.03)
                self.parts.append(('chest', M.rounded_box(0.10 * k, 0.02, min(0.36 * k, 0.42), cm, (0, y, 0.12), r=0.008, seg=6), None))
                self.parts.append(('chest', M.rounded_box(min(0.36 * k, 0.40), 0.02, 0.09 * k, cm, (0, y * 0.98, 0.19), r=0.008, seg=6), None))
