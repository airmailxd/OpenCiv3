"""Aircraft for the Blender remaster: Fighter (Spitfire), Bomber (B-17),
Helicopter (Chinook), Jet Fighter (MiG-29), F-15, Stealth Fighter,
Stealth Bomber.

    root = aircraft.build('Fighter')

Real-size models (meters), facing +Y, flying at the unit's altitude (the
root's z, so the ground shadow falls as far below as on the originals).
Fuselages are lofted superellipse stations (resampled finely so painted
marks follow the skin), wings and fins lofted airfoil sections. The paint
is props' painted camo (props.CAMO) with a lighter underside; Civ3's civ
markings (roundels, bands, stripes, blotches, letters) are face keys on the
skin, so they lie exactly on the curved surfaces.
"""
import math

from mathutils import Vector, noise

import props
import vkit
from props import R, T

for _k, _v in {'air_paint': ('#7c8078', 0.55, 0.35, 'metal'), 'air_under': ('#a8aca4', 0.55, 0.35, 'metal'),
               'air_dark': ('#3a3e42', 0.5, 0.5, 'metal'), 'glass': ('#2a3644', 0.08, 0.6, 'polished'),
               'glass_frame': ('#4a4e52', 0.5, 0.6, 'metal'), 'prop_black': ('#1a1a1c', 0.5, 0.3, 'metal'),
               'prop_tip': ('#d8b020', 0.5, 0.2, 'metal'), 'mark_white': ('#e6e4dc', 0.55, 0.1, 'metal'),
               'mark_red': ('#a8302a', 0.55, 0.1, 'metal'), 'gunmetal': ('#3a3c3e', 0.45, 1.0, 'metal'),
               'rubber': ('#1e1e1e', 0.85, 0.0, 'leather'), 'port': ('#0e0c0a', 0.9, 0.0, 'polished'),
               'afterburner': ('#ffb444', 0.5, 0.0, 'flame'), 'nozzle': ('#5a5650', 0.4, 1.0, 'metal'),
               'missile_white': ('#ecebe6', 0.45, 0.2, 'metal'), 'radome': ('#5a5e62', 0.5, 0.2, 'metal')}.items():
    props.DEFAULTS.setdefault(_k, _v)
WEAR = {'air_paint': {'grime': 0.3, 'soot': 0.15, 'edges': 0.35}, 'air_under': {'grime': 0.3},
        'air_dark': {'grime': 0.3}, 'nozzle': {'soot': 0.6, 'grime': 0.4}, 'prop_black': {'grime': 0.2}}


# ---------------------------------------------------------------- skin

def _lerp(a, b, t):
    return a + (b - a) * t


def resample(stations, step):
    """Stations (y, z, half_w, half_h[, power]) linearly resampled every
    `step` meters along y (from nose to tail)."""
    out = [stations[0]]
    for a, b in zip(stations[:-1], stations[1:]):
        n = max(1, int(abs(a[0] - b[0]) / step))
        for i in range(1, n + 1):
            t = i / n
            out.append(tuple(_lerp(x, y, t) for x, y in zip(a, b)))
    return out


def body(p, stations, key='air_paint', keys=None, seg=28, step=0.25, power=2.0):
    """A lofted fuselage through superellipse stations (y, z, half_w,
    half_h[, power]) running nose to tail; zero-size stations close it."""
    rings = []
    for st in resample(stations, step):
        y, z, hw, hh = st[:4]
        pw = st[4] if len(st) > 4 else power
        ring = []
        for k in range(seg):
            a = 2 * math.pi * k / seg
            c, s = math.cos(a), math.sin(a)
            ring.append((hw * math.copysign(abs(c) ** (2 / pw), c), y, z + hh * math.copysign(abs(s) ** (2 / pw), s)))
        rings.append(ring)
    p.add(props.loft(rings), key, smooth=True, keys=keys)


def wing(p, stations, key='air_paint', keys=None, seg=12, side=1, vertical=False, matrix=None, step=None):
    """A lofted wing / fin through airfoil stations (s, y_le, chord, offset,
    thickness_ratio) from root to tip: s runs along the span (x times side,
    or z when vertical), offset is the chord line's z (x when vertical)."""
    if step:
        stations = resample_span(stations, step)
    rings = []
    for s, y_le, c, off, th in stations:
        ring = []
        ts = [(1 - math.cos(math.pi * k / (seg - 1))) / 2 for k in range(seg)]
        half = [0.5 * th * c * 2.6 * math.sqrt(t) * (1 - t) for t in ts]
        pts = [(ts[k], half[k]) for k in range(seg)] + [(ts[k], -half[k]) for k in range(seg - 2, 0, -1)]
        for t, h in pts:
            y = y_le - t * c
            if vertical:
                ring.append((off + h, y, s))
            else:
                ring.append((side * s, y, off + h))
        rings.append(ring)
    bm = props.loft(rings)
    p.add(bm, key, matrix, smooth=True, keys=keys)


def resample_span(stations, step):
    out = [stations[0]]
    for a, b in zip(stations[:-1], stations[1:]):
        n = max(1, int(abs(b[0] - a[0]) / step))
        for i in range(1, n + 1):
            out.append(tuple(_lerp(x, y, i / n) for x, y in zip(a, b)))
    return out


def paint_keys(regions, under=True):
    """Face keys: the first region whose test passes on the face center
    (and normal) picks (key, tint); else the underside paint below."""
    def keys(f):
        c, n = f.calc_center_median(), f.normal
        for key, tint, test in regions:
            if test(c, n):
                return (key, tint)
        if under and n.z < -0.35:
            return ('air_under', False)
        return None
    return keys


def oval(cx, cy, rx, ry, top=None):
    """A test for an oval on a wing (in plan); top=True upper faces only,
    False lower, None both."""
    def test(c, n):
        if top is True and n.z < 0.2 or top is False and n.z > -0.2:
            return False
        return ((c.x - cx) / rx) ** 2 + ((c.y - cy) / ry) ** 2 < 1
    return test


def side_oval(cy, cz, ry, rz):
    def test(c, n):
        return abs(n.x) > 0.5 and ((c.y - cy) / ry) ** 2 + ((c.z - cz) / rz) ** 2 < 1
    return test


def band(y0, y1):
    return lambda c, n: y0 < c.y < y1


def blotches(scale, th, seed, zmin=-99.0):
    """Irregular civ blotches (the Jet Fighter's)."""
    return lambda c, n: c.z > zmin and noise.noise(c * scale + Vector((seed, seed * 0.7, seed * 1.3))) > th


def letters(text, y0, z0, h, side_x=True):
    """Blocky stencil letters along a fuselage side (3x5 cells, h tall)."""
    font = {'D': ['110', '101', '101', '101', '110'], 'N': ['101', '111', '111', '111', '101'],
            'A': ['010', '101', '111', '101', '101'], 'K': ['101', '110', '100', '110', '101']}
    cell = h / 5

    def test(c, n):
        if abs(n.x) < 0.6:
            return False
        # letters read nose-to-tail on the left, tail-to-nose on the right: mirror on +x
        u = (y0 - c.y) if c.x < 0 else (c.y - (y0 - len(text) * 4 * cell))
        v = (z0 + h - c.z)
        col, row = int(u / cell), int(v / cell)
        if row < 0 or row >= 5 or u < 0:
            return False
        li, cc = col // 4, col % 4
        if li >= len(text) or cc == 3:
            return False
        if c.x > 0:
            li = len(text) - 1 - li
        return font[text[li]][row][cc] == '1'
    return test


def prop(p, at, r, blades=3, key='prop_black', tips=False, hub=0.2, spin=15.0, axis='Y'):
    """A propeller at `at` facing +Y: spinner and blades (a little pitched)."""
    m = T(*at)
    p.add(props.lathe([(0.0, hub), (hub * 1.6, hub * 0.6), (hub * 2.2, 0.0)], seg=16), 'air_dark', m @ R('X', -90), smooth=True)
    for k in range(blades):
        a = spin + 360.0 * k / blades
        bm = m @ R('Y', a) @ T(0, hub * 0.4, 0)
        p.add(props.box(r * 0.1, 0.04, r), key, bm @ T(0, 0, r / 2 + hub * 0.5) @ R('Z', 18), bevel=0.01)
        if tips:
            p.add(props.box(r * 0.102, 0.042, r * 0.1), 'prop_tip', bm @ T(0, 0, r + hub * 0.5 - r * 0.05) @ R('Z', 18))


def canopy(p, at, rx, ry, rz, frames=3):
    x, y, z = at
    p.add(props.ellipsoid(rx, ry, rz, seg=20), 'glass', T(x, y, z), smooth=True)
    for k in range(frames):
        yy = y + ry * (-0.6 + 1.2 * k / max(1, frames - 1))
        f = math.sqrt(max(0.0, 1 - ((yy - y) / ry) ** 2))
        ring = [(x + rx * f * 1.02 * math.cos(math.pi * i / 12), yy, z + rz * f * 1.02 * math.sin(math.pi * i / 12)) for i in range(13)]
        p.add(props.tube(ring, 0.02, seg=5, caps=False), 'glass_frame', smooth=True)


# ---------------------------------------------------------------- units

def fighter(p):
    """A Spitfire: an elliptical low wing, the slim fuselage with its bubble
    canopy, a four-blade prop on the long nose, exhaust stubs, wing cannons
    and the underwing radiator; grey and green camo, a lighter underside,
    big civ ovals on the wings and fuselage, the white tail band."""
    regions = [('tint', True, oval(3.0, 0.65, 1.15, 0.8)), ('tint', True, oval(-3.0, 0.65, 1.15, 0.8)),
               ('tint', True, side_oval(-1.3, 0.25, 0.55, 0.45)),
               ('mark_white', False, band(-2.75, -2.4))]
    keys = paint_keys(regions)
    body(p, [(4.45, 0.02, 0.34, 0.4), (3.6, 0.02, 0.43, 0.52), (2.4, 0.04, 0.47, 0.6), (1.0, 0.04, 0.48, 0.62),
             (-0.6, 0.08, 0.42, 0.56), (-2.4, 0.18, 0.3, 0.42), (-3.9, 0.28, 0.14, 0.24), (-4.55, 0.32, 0.02, 0.06)],
         keys=keys, step=0.2)
    p.add(props.ellipsoid(0.36, 1.3, 0.3, seg=20), 'air_paint', T(0, -1.2, 0.45), smooth=True, keys=keys)   # the spine behind the canopy
    canopy(p, (0, 0.55, 0.6), 0.3, 0.75, 0.32)
    span = 5.6

    def chord(f):
        return max(0.35, 2.45 * math.sqrt(max(0.0, 1 - f * f)))
    st = []
    for i in range(15):
        f = i / 14 * 0.985
        c = chord(f)
        st.append((0.35 + f * span, 1.55 - (2.45 - c) * 0.3, c, -0.32 + f * span * math.tan(math.radians(6)), 0.13))
    for side in (-1, 1):
        wing(p, st, keys=keys, side=side)
        for dx in (1.6, 2.1):   # cannons in the leading edge
            vkit.rod(p, (side * dx, 1.4, -0.22), (side * dx, 2.1, -0.22), 0.035, 'gunmetal', seg=6)
        for k in range(3):   # exhaust stubs
            p.add(props.box(0.08, 0.14, 0.08), 'air_dark', T(side * 0.42, 3.3 - 0.22 * k, 0.24) @ R('Y', side * 20), bevel=0.01)
    p.add(props.box(0.32, 1.0, 0.24), 'air_under', T(0.95, 0.4, -0.52), bevel=0.06)   # the radiator
    p.add(props.box(0.26, 0.6, 0.2), 'air_under', T(-0.95, 0.5, -0.5), bevel=0.05)    # the oil cooler
    tst = [(0.1 + f * 1.55, -3.5 - (1.15 - max(0.3, 1.15 * math.sqrt(max(0, 1 - f * f)))) * 0.3,
            max(0.3, 1.15 * math.sqrt(max(0, 1 - f * f))), 0.28, 0.1) for f in [i / 8 * 0.98 for i in range(9)]]
    for side in (-1, 1):
        wing(p, tst, keys=keys, side=side)
    fst = [(0.3 + f * 1.45, -3.15 - f * 0.5, max(0.35, 1.4 * math.sqrt(max(0, 1 - f * f))), 0.0, 0.1)
           for f in [i / 8 * 0.98 for i in range(9)]]
    wing(p, fst, keys=keys, vertical=True)
    prop(p, (0, 4.45, 0.02), 1.6, blades=4, hub=0.26)


def bomber(p):
    """A B-17: the long fuselage with the glass nose, cockpit glazing, top,
    ball, chin and tail turrets and waist windows; four radials in their
    nacelles with three-blade props; the big fin with its dorsal fillet;
    olive drab with darker mottling, a grey underside; civ tailplanes, wing
    roundels, nose cheek stripes, white code letters."""
    regions = [('glass', False, lambda c, n: c.y > 10.0),
               ('tint', True, lambda c, n: abs(c.x) > 0.8 and -12.5 < c.y < -7.9 and abs(c.z - 0.75) < 0.4),
               ('tint', True, oval(11.0, 1.4, 1.2, 1.0, top=True)), ('tint', True, oval(-11.0, 1.4, 1.2, 1.0, top=True)),
               ('tint', True, lambda c, n: abs(n.x) > 0.5 and 8.4 < c.y < 9.9 and -0.25 < c.z < 0.05),
               ('mark_white', False, letters('DN', -0.8, -0.1, 1.0))]
    keys = paint_keys(regions)
    body(p, [(11.45, -0.15, 0.08, 0.08), (11.0, -0.12, 0.72, 0.76), (9.9, -0.02, 1.0, 1.1), (7.0, 0.1, 1.15, 1.35),
             (2.0, 0.15, 1.2, 1.42), (-3.0, 0.25, 1.05, 1.25), (-7.0, 0.45, 0.72, 0.88), (-10.0, 0.65, 0.38, 0.48),
             (-11.3, 0.72, 0.16, 0.2)], keys=keys, step=0.25, seg=32)
    canopy(p, (0, 7.6, 1.2), 0.75, 1.3, 0.55, frames=4)
    # turrets: top (behind the cockpit), ball (under the waist), chin, tail; their guns
    for at, r, guns, gz in (((0, 6.2, 1.45), 0.5, 2, 0.15), ((0, 0.8, -1.32), 0.55, 2, 0.0),
                            ((0, 10.6, -0.85), 0.38, 2, 0.0)):
        p.add(props.ellipsoid(r, r, r * 0.85, seg=18), 'glass' if at[2] > 0 else 'air_dark', T(*at), smooth=True)
        for dx in (-0.14, 0.14):
            vkit.rod(p, (at[0] + dx, at[1], at[2] + gz), (at[0] + dx, at[1] + 1.6, at[2] + gz), 0.035, 'gunmetal', seg=6)
    for dx in (-0.12, 0.12):
        vkit.rod(p, (dx, -11.3, 0.72), (dx, -12.4, 0.72), 0.035, 'gunmetal', seg=6)
    for side in (-1, 1):   # waist windows and their guns, cheek guns
        p.add(props.box(0.04, 1.1, 0.55), 'port', T(side * 1.07, -3.2, 0.55))
        vkit.rod(p, (side * 1.05, -3.2, 0.55), (side * 1.9, -2.6, 0.5), 0.035, 'gunmetal', seg=6)
        vkit.rod(p, (side * 0.9, 9.6, 0.1), (side * 1.3, 10.6, 0.05), 0.035, 'gunmetal', seg=6)
    # wings
    span = 15.8
    st = [(1.0 + f * span, _lerp(4.5, 2.1, f), _lerp(6.0, 2.1, f), -0.25 + f * span * math.tan(math.radians(4.5)), _lerp(0.17, 0.11, f))
          for f in [i / 16 for i in range(17)]]
    for side in (-1, 1):
        wing(p, st, keys=keys, side=side)
        for x in (4.6, 9.0):   # nacelles, cowlings, props
            f = (x - 1.0) / span
            le, wz = _lerp(4.5, 2.1, f), -0.25 + f * span * math.tan(math.radians(4.5))
            body(p, [(le + 2.0, wz - 0.05, 0.62, 0.62), (le + 1.1, wz - 0.05, 0.75, 0.75), (le - 1.5, wz - 0.1, 0.7, 0.66),
                     (le - 3.4, wz - 0.05, 0.3, 0.28)], keys=keys, seg=20, step=0.3,)
            p.add(props.lathe([(0.0, 0.76), (0.5, 0.78)], seg=20), 'air_dark', T(side * x, le + 1.6, wz - 0.05) @ R('X', -90), smooth=True)
            p.add(props.lathe([(0.0, 0.5), (0.05, 0.5)], seg=16), 'port', T(side * x, le + 2.06, wz - 0.05) @ R('X', -90), smooth=True)
            prop(p, (side * x, le + 2.1, wz - 0.05), 1.75, blades=3, tips=True, hub=0.28, spin=30 * side)
            p.add(props.box(0.25, 0.9, 0.2), 'nozzle', T(side * x, le - 1.8, wz - 0.7), bevel=0.05)   # the turbo
    # tailplane and the fin with its dorsal fillet
    tst = [(0.3 + f * 6.7, _lerp(-8.3, -9.4, f), _lerp(3.6, 1.4, f), 0.72, 0.1) for f in [i / 8 for i in range(9)]]
    for side in (-1, 1):
        wing(p, tst, keys=keys, side=side)
    fst = [(1.0, -2.6, 8.4, 0.0, 0.05), (1.6, -5.6, 5.8, 0.0, 0.08), (2.6, -7.6, 4.0, 0.0, 0.1),
           (5.2, -9.0, 2.6, 0.0, 0.1), (6.4, -9.8, 1.4, 0.0, 0.1)]
    wing(p, fst, keys=keys, vertical=True)


def helicopter(p):
    """A CH-47 Chinook: the long boxy fuselage with its glazed nose, the
    front pylon and the tall rear pylon with the engines on its flanks, two
    big three-blade rotors, the fuel sponsons, round windows and doors,
    the landing gear; pale grey with a darker belly, the civ spine stripe,
    rotor hubs, door panels and sponson stripes."""
    regions = [('glass', False, lambda c, n: c.y > 6.3 and c.z > 0.15 and n.y > -0.2),
               ('tint', True, lambda c, n: n.z > 0.7 and abs(c.x) < 0.55 and -4.8 < c.y < 5.3),
               ('tint', True, lambda c, n: abs(n.x) > 0.6 and (2.6 < c.y < 3.6 or -3.0 < c.y < -2.0) and -0.6 < c.z < 0.75),
               ('tint', True, lambda c, n: abs(c.x) > 1.3 and n.z > 0.3 and c.z < -0.2)]
    keys = paint_keys(regions)
    body(p, [(7.95, 0.25, 0.25, 0.25, 2.6), (7.5, 0.15, 0.85, 0.9, 2.8), (6.4, 0.1, 1.2, 1.2, 3.5),
             (5.0, 0.1, 1.27, 1.27, 4.0), (-4.0, 0.1, 1.27, 1.27, 4.0), (-5.6, 0.35, 1.22, 1.05, 3.6),
             (-7.6, 0.9, 0.8, 0.45, 3.0), (-8.0, 1.0, 0.1, 0.1, 2.5)], keys=keys, step=0.2, seg=32)
    # the front pylon hump and the rear pylon
    body(p, [(6.7, 1.3, 0.05, 0.05), (6.4, 1.45, 0.5, 0.4, 3.0), (4.6, 1.5, 0.55, 0.45, 3.0), (3.6, 1.3, 0.1, 0.1)],
         keys=keys, step=0.15, seg=24)
    body(p, [(-3.6, 1.35, 0.1, 0.1), (-4.6, 1.85, 0.62, 0.55, 3.0), (-6.4, 2.35, 0.66, 1.05, 3.2), (-7.7, 2.7, 0.45, 0.85, 3.0),
             (-8.05, 2.8, 0.05, 0.3)], keys=keys, step=0.15, seg=24)
    p.add(props.box(0.9, 8.8, 0.32), 'air_paint', T(0, 0.2, 1.36), bevel=0.12, keys=keys)   # the tunnel fairing on the spine
    for side in (-1, 1):
        # the engines on the rear pylon's flanks
        p.add(props.lathe([(0, 0.46), (2.8, 0.47), (3.2, 0.3)], seg=16), 'air_paint',
              T(side * 1.05, -4.35, 2.3) @ R('X', 90), smooth=True, keys=keys)
        p.add(props.lathe([(0, 0.34), (0.02, 0.34)], seg=16), 'port', T(side * 1.05, -4.33, 2.3) @ R('X', -90), smooth=True)
        # the fuel sponsons along the lower sides
        p.add(props.lathe([(0, 0.0), (0.4, 0.42), (7.2, 0.42), (7.8, 0.0)], seg=18, scale_xy=(1.0, 0.75)), 'air_paint',
              T(side * 1.32, -3.4, -0.75) @ R('X', -90), smooth=True, keys=keys)
        for y in [-4.0 + 1.1 * k for k in range(8)]:   # the round windows
            if 2.4 < y < 3.8 or -3.2 < y < -1.8:
                continue
            p.add(props.lathe([(0, 0.17), (0.02, 0.17)], seg=14), 'port', T(side * 1.28, y, 0.4) @ R('Y', side * 90), smooth=True)
        for y, z in ((3.7, -1.15), (-3.3, -1.15)):   # landing gear
            vkit.rod(p, (side * 1.0, y, -0.95), (side * 1.15, y, z), 0.08, 'gunmetal', seg=8)
            p.add(props.lathe([(-0.15, 0.32), (0.15, 0.32)], seg=16), 'rubber', T(side * 1.15, y, z - 0.18) @ R('Y', 90), smooth=True)
    # the rotors: hubs (civ), blades drooping a little
    for at, spin in (((0, 5.6, 1.98), 20.0), ((0, -6.9, 3.6), 80.0)):
        x, y, z = at
        vkit.rod(p, (x, y, z - 0.45), (x, y, z), 0.18, 'air_dark', seg=12)
        p.add(props.lathe([(0, 0.34), (0.28, 0.3), (0.42, 0.12)], seg=16), 'tint', T(x, y, z), smooth=True, tint=True)
        for k in range(3):
            a = math.radians(spin + 120 * k)
            d = Vector((math.cos(a), math.sin(a), 0.0))
            tip = Vector(at) + d * 9.1 + Vector((0, 0, -0.35))
            m, length = vkit.align(Vector(at) + Vector((0, 0, 0.2)), tip)
            p.add(props.box(0.64, 0.07, length), 'air_dark', m @ T(0, 0, length / 2), bevel=0.02)
    # the nose: windscreen frames and the probe
    vkit.rod(p, (0.5, 7.2, 0.0), (0.5, 9.3, -0.05), 0.04, 'gunmetal', seg=6)


def _jet_common(p, keys, cfg):
    """A twin-engine, twin-fin jet: the nose and cockpit, the blended
    body with its leading-edge extensions, swept wings, twin canted fins
    and tailplanes, the engine nacelles with intakes and glowing nozzles,
    missiles on pylons."""
    L = cfg['L']
    body(p, cfg['nose'], keys=keys, step=0.2, seg=28)
    p.add(props.lathe([(0, 0.3), (0.55, 0.16), (0.85, 0.0)], seg=18), 'radome', T(0, cfg['nose'][0][0] - 0.2, cfg['nose'][0][1]) @ R('X', -90), smooth=True)
    canopy(p, cfg['canopy'], *cfg['canopy_r'], frames=2)
    # the flat centre body between the engines
    p.add(props.box(cfg['body_w'], cfg['body_l'], cfg['body_h']), 'air_paint', T(0, cfg['body_y'], cfg['body_z']), bevel=0.25, keys=keys)
    for side in (-1, 1):
        ex = side * cfg['eng_x']
        y0, y1, r = cfg['eng_y0'], cfg['eng_y1'], cfg['eng_r']
        p.add(props.lathe([(0, r * 1.02), (y0 - y1 - 0.6, r), (y0 - y1, r * 0.82)], seg=20, scale_xy=(1.0, 1.05)), 'air_paint',
              T(ex, y0, cfg['eng_z']) @ R('X', 90), smooth=True, keys=keys)
        p.add(props.box(r * 1.9, 0.08, r * 2.0), 'port', T(ex, y0 + 0.02, cfg['eng_z']))      # the intake mouth
        p.add(props.box(r * 2.1, 0.5, r * 2.2), 'air_paint', T(ex, y0 - 0.2, cfg['eng_z']), bevel=0.06, keys=keys)
        p.add(props.lathe([(0, r * 0.82), (0.7, r * 0.72)], seg=20), 'nozzle', T(ex, y1, cfg['eng_z']) @ R('X', 90), smooth=True)
        p.add(props.lathe([(0, r * 0.6), (0.02, r * 0.6)], seg=16), 'afterburner', T(ex, y1 - 0.69, cfg['eng_z']) @ R('X', 90), smooth=True)
        wing(p, cfg['wing'], keys=keys, side=side, step=0.45)
        wing(p, cfg['lerx'], keys=keys, side=side)
        wing(p, cfg['tail'], keys=keys, side=side)
        fx = side * cfg['fin_x']
        wing(p, cfg['fin'], keys=keys, vertical=True,
             matrix=T(fx, 0, cfg['fin_z']) @ R('Y', side * cfg['fin_cant']))
        for k, (mx, my, ml) in enumerate(cfg['missiles']):   # missiles on pylons under the wings
            x = side * mx
            wz = cfg['wing'][0][3] - 0.25
            p.add(props.box(0.08, ml * 0.4, 0.2), 'air_under', T(x, my - ml * 0.3, wz))
            p.add(props.lathe([(0, 0.0), (0.35, 0.1), (ml, 0.1)], seg=12), 'missile_white', T(x, my, wz - 0.22) @ R('X', 90), smooth=True)
            for a in (45, 135):
                p.add(props.box(0.01, 0.35, 0.42), 'air_dark', T(x, my - ml + 0.2, wz - 0.22) @ R('Y', a))


def jet_fighter(p):
    """A MiG-29: pale blue-grey splinter camo with civ blotches."""
    cfg = dict(L=17.3, nose=[(8.3, 0.3, 0.18, 0.18), (7.4, 0.3, 0.42, 0.42), (5.6, 0.35, 0.62, 0.66),
                             (4.0, 0.42, 0.72, 0.72), (2.0, 0.42, 0.62, 0.5), (0.0, 0.4, 0.45, 0.4), (-2.0, 0.4, 0.2, 0.2)],
               canopy=(0, 4.6, 0.95), canopy_r=(0.42, 1.25, 0.42),
               body_w=2.6, body_l=10.0, body_h=0.5, body_y=-2.4, body_z=0.2,
               eng_x=0.95, eng_y0=2.8, eng_y1=-7.9, eng_r=0.62, eng_z=-0.25,
               wing=[(1.3, 1.2, 5.6, 0.1, 0.07), (5.7, -2.4, 1.6, 0.0, 0.05)],
               lerx=[(0.8, 4.8, 4.0, 0.25, 0.04), (1.6, 1.4, 1.4, 0.15, 0.04)],
               tail=[(1.3, -5.0, 2.6, 0.05, 0.05), (3.9, -6.6, 1.2, 0.05, 0.05)],
               fin=[(0.0, -3.6, 3.0, 0.0, 0.06), (3.1, -5.8, 1.2, 0.0, 0.05)], fin_x=1.35, fin_z=0.4, fin_cant=8,
               missiles=[(3.2, 0.4, 3.0), (4.4, -0.4, 2.6)])
    regions = [('tint', True, blotches(0.55, 0.32, 3.0, zmin=0.0))]
    _jet_common(p, paint_keys(regions), cfg)


def f15(p):
    """An F-15: big wings, boxy intakes, twin upright fins; grey with civ
    stripes along the wings and fin tips."""
    cfg = dict(L=19.4, nose=[(9.6, 0.4, 0.18, 0.18), (8.6, 0.4, 0.45, 0.45), (6.6, 0.45, 0.72, 0.76),
                             (4.6, 0.55, 0.8, 0.8), (2.0, 0.55, 0.6, 0.5), (0.0, 0.5, 0.45, 0.4), (-2.0, 0.5, 0.2, 0.2)],
               canopy=(0, 5.4, 1.15), canopy_r=(0.48, 1.5, 0.5),
               body_w=2.8, body_l=11.0, body_h=0.6, body_y=-2.6, body_z=0.25,
               eng_x=1.1, eng_y0=3.6, eng_y1=-8.6, eng_r=0.72, eng_z=-0.1,
               wing=[(1.6, 1.8, 7.0, 0.15, 0.06), (6.5, -3.4, 1.6, 0.05, 0.05)],
               lerx=[(1.3, 3.6, 1.8, 0.2, 0.04), (1.8, 2.0, 0.6, 0.18, 0.04)],
               tail=[(1.6, -5.6, 3.0, 0.1, 0.05), (4.4, -7.6, 1.4, 0.1, 0.05)],
               fin=[(0.0, -4.4, 3.4, 0.0, 0.06), (3.2, -6.6, 1.4, 0.0, 0.05)], fin_x=1.4, fin_z=0.55, fin_cant=2,
               missiles=[(3.6, 0.6, 3.6), (5.0, -0.6, 2.9)])
    regions = [('tint', True, lambda c, n: n.z > 0.2 and abs(c.x) > 2.0 and (abs(c.y - (1.4 - (abs(c.x) - 1.6) * 1.02)) < 0.35)),
               ('tint', True, lambda c, n: abs(c.z) > 3.0),
               ('tint', True, lambda c, n: n.z > 0.5 and abs(c.x) < 0.3 and -3.0 < c.y < 3.5 and c.z > 0.6)]
    _jet_common(p, paint_keys(regions), cfg)


def stealth_fighter(p):
    """A faceted delta stealth fighter with twin canted V tails, dark grey,
    a civ chevron across the top, civ fin tips."""
    regions = [('tint', True, lambda c, n: n.z > 0.3 and abs(abs(c.x) * 0.9 - (c.y + 1.2)) < 0.45 and c.y > -2.0 and abs(c.x) < 4.0),
               ('tint', True, lambda c, n: c.z > 2.4)]
    keys = paint_keys(regions)
    # the faceted body: a flat diamond loft with power < 2 (hard chines)
    body(p, [(9.0, 0.2, 0.05, 0.03, 1.2), (6.0, 0.25, 0.9, 0.45, 1.3), (2.0, 0.3, 1.6, 0.65, 1.3),
             (-3.0, 0.3, 1.5, 0.5, 1.3), (-6.0, 0.25, 0.8, 0.3, 1.3), (-7.2, 0.25, 0.3, 0.12, 1.3)], 'air_paint', keys=keys, step=0.25, seg=16)
    canopy(p, (0, 3.6, 0.85), 0.5, 1.4, 0.38, frames=2)
    for side in (-1, 1):
        wing(p, [(1.2, 4.0, 9.0, 0.2, 0.05), (6.6, -3.2, 2.2, 0.1, 0.04)], keys=keys, side=side, step=0.4)
        wing(p, [(0.0, -3.4, 3.2, 0.0, 0.05), (2.8, -5.6, 1.4, 0.0, 0.05)], keys=keys, vertical=True,
             matrix=T(side * 1.2, 0, 0.45) @ R('Y', side * 40))
        p.add(props.box(1.1, 0.06, 0.4), 'port', T(side * 1.25, 4.5, 0.2) @ R('Z', side * 25))   # the gridded intakes
        p.add(props.box(1.4, 0.4, 0.14), 'nozzle', T(side * 0.8, -6.8, 0.25))                   # the slot exhausts


def stealth_bomber(p):
    """A B-2: the flying wing with its sawtooth trailing edge, the raised
    centre body with the cockpit windows and engine humps, dark grey; civ
    panels near the wing tips."""
    regions = [('tint', True, lambda c, n: n.z > 0.2 and 16.0 < abs(c.x) < 21.0 and abs(c.y - (2.0 - (abs(c.x) - 1.0) * 0.62 - 1.6)) < 1.0),
               ('port', False, lambda c, n: c.y > 6.6 and c.z > 0.9 and n.y > 0.2)]
    keys = paint_keys(regions)
    # the centre body
    body(p, [(10.0, 0.2, 0.2, 0.08), (8.5, 0.35, 2.0, 0.85), (5.0, 0.4, 3.6, 1.2), (0.0, 0.35, 4.6, 0.9), (-4.5, 0.2, 3.0, 0.4),
             (-6.4, 0.15, 1.0, 0.15)], keys=keys, step=0.3, seg=24)
    # the wing: leading edge swept 33 degrees to the tips, a sawtooth trailing edge (W in plan)
    span = 26.0
    te = [(0.0, -6.4), (6.0, -2.3), (11.0, -6.0), (17.0, -2.0), (span, -11.2)]
    st = []
    for i in range(27):
        s = 1.0 + (span - 1.0) * i / 26
        le = 9.0 - (s - 1.0) * math.tan(math.radians(33))
        for (s0, t0), (s1, t1) in zip(te[:-1], te[1:]):
            if s0 <= s <= s1:
                tey = _lerp(t0, t1, (s - s0) / (s1 - s0))
        st.append((s, le, max(0.6, le - tey), 0.2 - (s - 1) * 0.01, _lerp(0.1, 0.05, i / 26)))
    for side in (-1, 1):
        wing(p, st, keys=keys, side=side)
        for dx in (2.0, 3.2):   # the engine humps and their intakes
            p.add(props.ellipsoid(0.75, 3.0, 0.45, seg=16), 'air_paint', T(side * dx, 2.0, 0.9), smooth=True, keys=keys)
            p.add(props.box(0.9, 0.08, 0.2), 'port', T(side * dx, 4.6, 1.15) @ R('X', -15))
            p.add(props.box(1.0, 0.6, 0.06), 'nozzle', T(side * dx, -2.8, 0.75))


AIRCRAFT = {
    'Fighter': dict(build=fighter, alt=6.4, paint='#6c7068', under='#a6aaa2',
                    camo=[('#4a5838', 2.2, 0.5, 1.0)]),
    'Bomber': dict(build=bomber, alt=9.3, paint='#5e6440', under='#8a8c84',
                   camo=[('#4a5232', 1.1, 0.54, 2.0), ('#727a4c', 1.5, 0.58, 5.0)]),
    'Helicopter': dict(build=helicopter, alt=9.9, paint='#c6cacd', under='#7a7e84',
                       camo=[('#e2e4e6', 0.9, 0.53, 3.0)]),
    'Jet Fighter': dict(build=jet_fighter, alt=6.1, paint='#a6b2be', under='#c4ccd4',
                        camo=[('#7e8c9c', 1.2, 0.52, 4.0), ('#cad2da', 1.6, 0.58, 9.0)]),
    'F-15': dict(build=f15, alt=6.4, paint='#a0a6ae', under='#b8bec4',
                 camo=[('#8a929c', 1.0, 0.54, 6.0)]),
    'Stealth Fighter': dict(build=stealth_fighter, alt=5.8, paint='#3e4246', under='#4a4e52',
                            camo=[('#565a5e', 1.4, 0.56, 7.0)]),
    'Stealth Bomber': dict(build=stealth_bomber, alt=9.3, paint='#3e4246', under='#4a4e52',
                           camo=[('#565a5e', 0.8, 0.56, 8.0)]),
}
UNITS = list(AIRCRAFT)


def build(unit, colors=None):
    cfg = AIRCRAFT[unit]
    c = {'air_paint': cfg['paint'], 'air_under': cfg['under']}
    c.update(colors or {})
    root = vkit.root(unit)
    props.CAMO.clear()
    props.CAMO['air_paint'] = [(hx, s, th, seed) for hx, s, th, seed in cfg['camo']]
    p = vkit.part(root, 'airframe', c, WEAR)
    cfg['build'](p)
    p.finish()
    props.CAMO.clear()
    root.location.z = cfg['alt']
    return root
