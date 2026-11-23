"""The League modes' aircraft (catalog/league_air.py): Modern's Fighter,
which the mod draws as a Grumman F4F-4 Wildcat (its Art/Units/Fighter).

Registers the aircraft archetype's model 'wildcat' (models/aircraft.py):
the stubby barrel of a fuselage with its deep belly (the main wheels folded
into its sides), a big round cowling with the engine's cylinders inside and
its cowl flaps, a framed greenhouse canopy set high, small windows behind
it, square-tipped wings set at mid-height with three .50s in each leading
edge, a squared tailplane and a tall rounded fin with its rudder, an
antenna mast and its wire, the tail hook and tail wheel, exhaust stubs.
Blue-grey above, light grey below (the 1942 scheme), panel lines; the civ
color in the national insignia (a star on a disc with bars) on the wings
and the fuselage sides and on the cowling's lip.

Also holds the helpers the round-2 planes share (models/modern_mod_sea_air.py's
P-38): `paintn` (pattern by position and normal), `insignia`.
"""
import math

import numpy as np

import mesh as M
from models import archetype
from models import aircraft as AC
from models import vparts as VP


# ------------------------------------------------------------ shared helpers

def paintn(mesh, fn, max_edge=0.1):
    """Subdivides a mesh until no edge exceeds max_edge and recolors each
    triangle by fn(centers, face normals, ids) -> ids."""
    m = mesh
    for _ in range(6):
        e = np.linalg.norm(m.V - np.roll(m.V, 1, axis=1), axis=-1).max()
        if e <= max_edge:
            break
        m = VP.subdivide(m, max_edge)
    C = m.V.mean(1)
    N = m.N.mean(1)
    N /= np.linalg.norm(N, axis=-1, keepdims=True) + 1e-9
    m.M = np.asarray(fn(C, N, m.M.copy()), np.int32)
    return m


def _in_star(px, py, R, r_in=0.4, rot=90.0):
    """Point-in-five-pointed-star test (arrays), points up at rot degrees."""
    ang = np.radians(rot + np.arange(10) * 36.0)
    rad = np.where(np.arange(10) % 2 == 0, R, R * r_in)
    vx, vy = rad * np.cos(ang), rad * np.sin(ang)
    inside = np.zeros(np.shape(px), bool)
    j = 9
    for i in range(10):
        c = ((vy[i] > py) != (vy[j] > py)) & (px < (vx[j] - vx[i]) * (py - vy[i]) / (vy[j] - vy[i] + 1e-12) + vx[i])
        inside ^= c
        j = i
    return inside


def insignia(r, disc, star, bar_k=1.0, thick=0.02, seg=24):
    """The national insignia in the plane z = 0 (facing +z), centered: a disc
    (the civ color) with a white five-pointed star, white bars to each side
    along x with a civ-colored outline. bar_k = 0 leaves the bars off."""
    parts = [M.cylinder(r, r, thick, disc, seg=seg, z0=-thick / 2)]
    if bar_k > 0:
        bl, bh = r * 0.95 * bar_k, r * 0.5
        for s in (-1, 1):
            parts.append(M.box(bl + 0.06, bh + 0.08, thick * 0.9, disc, center=(s * (r * 0.75 + bl / 2), 0, 0)))
            parts.append(M.box(bl, bh, thick * 1.1, star, center=(s * (r * 0.75 + bl / 2 + 0.02), 0, 0)))
    m = M.Mesh.concat(parts)

    def f(C, N, ids):
        on = (np.hypot(C[:, 0], C[:, 1]) < r * 0.98) & (np.abs(N[:, 2]) > 0.5)
        ids[on & _in_star(C[:, 0], C[:, 1], r * 0.86)] = star
        return ids
    return paintn(m, f, max_edge=r * 0.07)


def two_tone(top, under, line=None, z_split=0.0, lines=None):
    """A painter: `under` on faces turned down (and below z_split on round
    bodies), `top` elsewhere; `lines` (C, N) -> bool marks panel lines in
    `line`."""
    def f(C, N, ids):
        sel = ids == top
        ids[sel & ((N[:, 2] < -0.25) | ((C[:, 2] < z_split) & (N[:, 2] < 0.35)))] = under
        if lines is not None and line is not None:
            ids[(ids == top) & lines(C, N)] = line
        return ids
    return f


def near(v, xs, w):
    """True where v is within w of any of xs."""
    out = np.zeros(np.shape(v), bool)
    for x in xs:
        out |= np.abs(v - x) < w
    return out


def canopy_frames(c, radii, ys, mat, r=0.03, k=1.03):
    """Frame hoops over an ellipsoid canopy (center c, radii) at the given y
    offsets, and the spine bar along its top."""
    cx, cy, cz = c
    rx, ry, rz = radii
    out = []
    for dy in ys:
        s = math.sqrt(max(0.0, 1 - (dy / ry) ** 2)) * k
        pts = [(cx + rx * s * math.cos(t), cy + dy, cz + rz * s * math.sin(t)) for t in np.linspace(0.05, math.pi - 0.05, 9)]
        for i in range(len(pts) - 1):
            out.append(VP.tube(pts[i], pts[i + 1], r, r, mat, seg=4))
    out.append(VP.tube((cx, cy - ry * 0.85, cz + rz * 0.55 * k), (cx, cy + ry * 0.8, cz + rz * 0.62 * k), r, r, mat,
                       seg=4))
    out.append(VP.tube((cx, cy - ry * 0.4, cz + rz * k), (cx, cy + ry * 0.4, cz + rz * k), r, r, mat, seg=4))
    return out


# ------------------------------------------------------------ the Wildcat

def edged(painter, top, under, le=None, w=0.14):
    """A wing painter whose edges show the light under-colour: the edge
    faces themselves and a feathered strip along the outline on top (the
    1942 scheme's soft demarcation wrapping round the leading edge and tips)."""
    def f(C, N, ids):
        ids = painter(C, N, ids)
        ids[(np.abs(N[:, 2]) < 0.45) & (ids == top)] = under
        if le is not None:
            ids[(ids == top) & (C[:, 1] > le(np.abs(C[:, 0])) - w)] = under
        return ids
    return f


def build_wildcat(V):
    m = V.mats
    L = 8.8
    paint, under, line = m['paint'], m['under'], m['line']
    # the fuselage: a stubby barrel, deepest behind the cowling
    prof = [(-4.95, 0.08), (-4.6, 0.27), (-3.4, 0.45), (-2.0, 0.57), (-0.6, 0.65), (0.8, 0.69), (2.0, 0.7), (2.35, 0.7)]
    fus = AC.fuselage(prof, paint, seg=18, sx=0.82, sz=1.06)
    belly = M.ellipsoid(0.5, 1.75, 0.42, paint, center=(0, 1.1, -0.42), seg=16)
    frames = lambda C, N: near(C[:, 1], (-3.6, -2.7, -1.3, 1.6), 0.035) & (C[:, 2] > -0.3)
    tt_body = two_tone(paint, under, line, z_split=0.08, lines=frames)

    def body_paint(C, N, ids):
        ids = tt_body(C, N, ids)
        # exhaust staining: a dark streak along each side aft of the stubs
        st = (np.abs(C[:, 0]) > 0.3) & (C[:, 1] < 2.0) & (C[:, 1] > 0.2) &              (np.abs(C[:, 2] + 0.28 - (2.0 - C[:, 1]) * 0.05) < 0.07 + (2.0 - C[:, 1]) * 0.04)
        ids[st] = m['stain']
        return ids
    body = [paintn(M.Mesh.concat([fus, belly]), body_paint, 0.1)]
    # a civ-colored band round the rear fuselage
    body.append(AC.fuselage([(-3.15, 0.485), (-2.9, 0.505)], m['band'], seg=18, sx=0.84, sz=1.09))
    # the main wheels folded into the belly's sides
    for sx in (-1, 1):
        body.append(VP.axle_x(0.3, 0.08, m['tire'], center=(sx * 0.44, 1.45, -0.5)))
        body.append(VP.axle_x(0.14, 0.1, m['steel'], center=(sx * 0.45, 1.45, -0.5)))
    # the cowling: its lip in the civ color, the engine's face and cylinders inside, the cowl flaps
    body.append(AC.fuselage([(2.3, 0.72), (3.25, 0.74), (3.4, 0.7)], paint, seg=22))
    body.append(AC.fuselage([(3.3, 0.745), (3.46, 0.7)], m['line_dark'], seg=22))
    body.append(AC.fuselage([(3.2, 0.58), (3.25, 0.0)], m['steel'], seg=18))
    for i in range(14):
        a = 2 * math.pi * i / 14
        body.append(M.box(0.12, 0.12, 0.16, m['steel'], center=(0.46 * math.cos(a), 3.3, 0.46 * math.sin(a))).transformed(
            M.translate(0, 0, 0)))
    flaps = AC.fuselage([(2.15, 0.69), (2.38, 0.73)], m['paint'], seg=22)

    def slats(C, N, ids):
        a = np.degrees(np.arctan2(C[:, 2], C[:, 0]))
        ids[np.floor(a / 15) % 2 == 1] = line
        return ids
    body.append(paintn(flaps, slats, 0.12))
    body.append(AC.fuselage([(3.3, 0.2), (3.55, 0.16), (3.7, 0.0)], m['steel']))         # prop hub
    # exhaust stubs under the cowl's rear
    for sx in (-1, 1):
        for k in range(2):
            body.append(VP.tube((sx * 0.5, 2.2 - k * 0.18, -0.32), (sx * 0.62, 1.95 - k * 0.18, -0.36), 0.05, 0.045,
                                m['exhaust'], seg=6))
    # the canopy: a framed greenhouse, the windscreen, small windows behind, the spine
    can = VP.icosphere(0.4, m['glass'], (0, 0.05, 0.68), sub=2, smooth=True, squash=(0.82, 2.1, 0.72))
    body.append(can)
    body += canopy_frames((0, 0.05, 0.68), (0.328, 0.84, 0.288), (-0.5, -0.12, 0.28, 0.62), m['line_dark'])
    spine = AC.fuselage([(-4.0, 0.05), (-2.6, 0.2), (-0.9, 0.3)], paint, sx=1.0, sz=1.0).transformed(
        M.translate(0, 0, 0.5))
    body.append(paintn(spine, two_tone(paint, under), 0.2))
    for sx in (-1, 1):
        body.append(M.box(0.02, 0.3, 0.16, m['glass_dark'], center=(sx * 0.42, -1.05, 0.42)))
    # the insignia on the fuselage's sides
    for sx in (-1, 1):
        body.append(insignia(0.34, m['band'], m['star'], bar_k=0.4).transformed(
            M.translate(sx * 0.49, -2.3, 0.02) @ M.rot_y(sx * 90) @ M.rot_z(90)))
    # the antenna mast behind the canopy, its wire to the fin, the tail hook and tail wheel
    body.append(VP.tube((0, -0.95, 0.75), (0, -1.15, 1.15), 0.04, 0.025, m['line_dark'], seg=5))
    body.append(VP.tube((0, -1.15, 1.15), (0, -4.45, 1.75), 0.012, 0.012, m['line_dark'], seg=4))
    body.append(VP.tube((0, -4.2, -0.22), (0, -4.75, -0.35), 0.03, 0.03, m['steel'], seg=5))
    body.append(VP.axle_x(0.13, 0.08, m['tire'], center=(0, -4.0, -0.36)))

    # the wings: set at mid-height, square tips, panel lines, ailerons and flaps
    poly = [(0.35, 1.32), (5.5, 0.8), (5.8, 0.62), (5.8, -0.22), (5.6, -0.38), (0.35, -1.3)]
    wl = lambda C, N: (near(np.abs(C[:, 0]), (1.6, 2.9, 4.2), 0.04)
                       | ((np.abs(C[:, 0]) > 3.6) & near(C[:, 1], (-0.06 - 0.12 * (np.abs(C[:, 0]) - 3.6),), 0.04))
                       | ((np.abs(C[:, 0]) < 3.6) & near(C[:, 1] + 0.16 * np.abs(C[:, 0]), (-0.62,), 0.04)))
    ws = {}
    for side, name in ((1, 'wing_r'), (-1, 'wing_l')):
        w = AC.wing(poly, 0.2, paint, z=-0.12, dihedral=4, side=side)
        yle = lambda x: np.minimum(1.32 - (x - 0.35) * (0.52 / 5.15), np.where(x > 5.5, 0.8 - (x - 5.5) * 0.6, 9.0))
        parts = [paintn(w, edged(two_tone(paint, under, line, lines=wl), paint, under, le=yle), 0.1)]
        zt = -0.12 + 0.105 + 4.2 * math.tan(math.radians(4))
        parts.append(insignia(0.52, m['band'], m['star'], bar_k=0.4).transformed(
            M.translate(side * 4.15, 0.22, zt) @ M.rot_z(90)))
        for k, x in enumerate((2.7, 2.95, 3.2)):
            yle = 1.32 - (x - 0.35) * (0.52 / 5.15)
            zg = -0.15 + x * math.tan(math.radians(4))
            parts.append(VP.tube((side * x, yle - 0.2, zg), (side * x, yle + 0.42, zg), 0.045, 0.04, m['steel'], seg=6))
        if side > 0:
            parts.append(VP.tube((5.3, 0.6, 0.25), (5.3, 1.2, 0.25), 0.025, 0.02, m['steel'], seg=5))     # pitot
        ws[name] = M.Mesh.concat(parts)
    # the tail: squared tailplane with elevators, a tall rounded fin with its rudder
    tp = [(0.1, -3.7), (2.0, -3.92), (2.15, -4.08), (2.15, -4.42), (0.1, -4.62)]
    el = lambda C, N: near(C[:, 1], (-4.18,), 0.035)
    tail = []
    for s in (1, -1):
        tail.append(paintn(AC.wing(tp, 0.12, paint, z=0.12, side=s), edged(two_tone(paint, under, line, lines=el), paint, under), 0.12))
    fin = VP.side_prism([(-3.5, 0.45), (-3.95, 1.45), (-4.2, 1.82), (-4.55, 1.9), (-4.88, 1.65), (-5.0, 1.2),
                         (-4.98, 0.25)], 0.14, paint)
    rl = lambda C, N: near(C[:, 1], (-4.42,), 0.035) & (C[:, 2] > 0.35)
    tail.append(paintn(fin, two_tone(paint, under, line, lines=rl), 0.12))
    parts = dict(ws)
    # the walkway: a dark anti-slip strip on each wing root beside the cockpit
    for side, name in ((1, 'wing_r'), (-1, 'wing_l')):
        ws[name] = M.Mesh.concat([ws[name], M.box(0.42, 1.5, 0.02, m['walk'],
                                                  center=(side * 0.82, -0.1, -0.12 + 0.11 + 0.82 * 0.07))])
    # the barrel: the body and tail shortened and filled out (the wings keep their span)
    S = M.scale(1.1, 0.9, 1.08)
    parts['body'] = M.Mesh.concat(body).transformed(S)
    parts['tail'] = M.Mesh.concat(tail).transformed(S)
    gx = 2.95
    return dict(parts=parts, props=[((0, 3.62 * 0.9, 0.0), 1.45, 3, 'y')],
                guns=[(sx * gx, 1.32 - (gx - 0.35) * 0.1 + 0.45, -0.15 + gx * 0.07) for sx in (-1, 1)],
                pylons=[], length=L)


AC.BUILDERS.setdefault('wildcat', build_wildcat)
AC.PAINTS.setdefault('wildcat', {'paint': '#505a66', 'under': '#a4aab0', 'line': '#4a5464', 'line_dark': '#2c3038',
                                 'paint_dark': '#4a5468', 'band': '#e8e8e8', 'star': '#f2f2f0',
                                 'glass': ('#9ab8c8', 0.7, 40), 'glass_dark': ('#3a4a58', 0.7, 40),
                                 'steel': ('#3a3a3c', 0.5, 24), 'exhaust': '#3a2e28', 'tire': '#222222', 'walk': '#34383e', 'stain': '#3a3a3a',
                                 'blade': '#262626', 'blade_tip': '#d8c040', 'bomb': '#3c3e38'})


# ------------------------------------------------------------ the round-2 planes' death

@archetype('aircraft_r2')
class AircraftR2(AC.Aircraft):
    """The aircraft archetype with a death whose pieces stay what they were:
    hit, the plane breaks in two (the civ markings kept, the rest scorched),
    both pieces tumble down out of the fireball over several frames, reach
    the ground and shrink away under the smoke over the next few frames,
    instead of vanishing the moment they are clear of the cloud."""

    def __init__(self, unit, params):
        super().__init__(unit, params)
        # 'drop' (1x px): the plane a little lower in its cell than the fit puts it, so its tail
        # keeps clear of the cell's top in the default frame
        ax, ay = self.p['anchor']
        self.p['anchor'] = (ax, ay + params.get('drop', 0.0))
        self._cal = None

    def death(self, t, ctx):
        from models import vfx as FX
        m = self.mats
        tb = 0.06
        u = max(0.0, t - tb)
        T_FALL, T_GONE = 0.42, 0.62
        fall = min(1.0, u / T_FALL)
        alt = self.alt
        L = self.b['length']
        P_ = self.b['parts']
        char = {k: v for k, v in self.charred().items() if not self.mats.tint[k]} if u > 0.04 else None
        shrink = 1.0 - min(1.0, max(0.0, (u - T_FALL) / (T_GONE - T_FALL)))
        pieces = []

        def piece(mesh, dx, spin_axis, spin, fwd):
            z = alt * (1 - 0.55 * fall * fall)
            c = mesh.V.reshape(-1, 3).mean(0)
            Tm = (M.translate(dx * fall, fwd * fall, z) @ spin_axis(spin * fall) @ M.translate(*c)
                  @ M.scale(shrink) @ M.translate(*(-c)))
            mm = mesh.transformed(Tm)
            return VP.recolor(mm, char) if char else mm
        if shrink > 0.02:
            if u < 0.03:
                pieces.append(piece(self.plane(), 0.0, M.rot_y, 0, 0))
            else:
                main = M.Mesh.concat([P_['body'], P_['tail'], P_['wing_r'], self.props_mesh(0)])
                pieces.append(piece(main, L * 0.06, M.rot_y, 120, L * 0.14))
                if len(P_['wing_l']):
                    pieces.append(piece(P_['wing_l'], -L * 0.22, M.rot_x, 260, -L * 0.06))
        fx = []
        if t >= tb:
            air = np.array([0, 0, alt], np.float32)
            fx.append(FX.explosion(m, air, L * 0.36, u / 0.85, seed=2, n=10, rise=0.15, smoke_dark=0.75))
            if 0.03 < u < T_GONE:
                # the burning wreck trails fire and smoke on its way down
                zc = alt * (1 - 0.55 * fall * fall)
                fx.append(FX.puff(m, (L * 0.06 * fall, L * 0.14 * fall, zc), L * 0.07 * max(shrink, 0.3), 'fx_orange',
                                  seed=41))
                if fall >= 1.0:
                    fx.append(FX.puff(m, (L * 0.06, L * 0.14, zc + L * 0.05 * u), L * 0.1, 'smoke_dark', seed=43))
        return self.fx(M.Mesh.concat(pieces + fx), pivot_z=alt)

    def attack(self, t, ctx):
        """The stock gun attack with the tracers kept short (they ran out of
        the cell on the side views)."""
        from models import vfx as FX
        from models.vehicles import flash_time
        if self.p.get('weapon', 'guns') != 'guns':
            return super().attack(t, ctx)
        m = self.mats
        T = self.flight(bob=0.05 * math.sin(2 * math.pi * t))
        mesh = self.plane(prop_ang=self.spin(t, 0.8)).transformed(T)
        fx = []
        tf = self.p.get('fire')
        if tf is None:
            tf = flash_time(ctx, 0.15)
        reach = self.p.get('tracer_reach', 0.75) * self.b['length']
        for k, g in enumerate(self.b['guns']):
            p0 = (T @ np.array([*g, 1.0], np.float32))[:3]
            fwd = np.array([0, 1.0, -0.12], np.float32)
            fwd /= np.linalg.norm(fwd)
            ph = int(t * 20) + k
            if t > tf * 0.5 and ph % 2 == 0:
                fx.append(FX.muzzle_flash(m, p0, fwd, 0.7, seed=ph, prongs=4))
            for j in range(2):
                u = (t * 4 + j / 2 + k * 0.25) % 1.0
                if t > tf * 0.5:
                    p = p0 + fwd * (1.0 + u * reach)
                    fx.append(VP.tube(p, p + fwd * 1.0, 0.05, 0.05, m['fx_yellow'], seg=4))
        return FX.FxMesh(M.Mesh.concat([mesh] + fx), m)
