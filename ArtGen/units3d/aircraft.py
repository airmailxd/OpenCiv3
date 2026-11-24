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
import json
import math
import os

from mathutils import Vector, noise

import props
import vkit
from props import R, T

for _k, _v in {'air_paint': ('#7c8078', 0.55, 0.35, 'metal'), 'air_under': ('#a8aca4', 0.55, 0.35, 'metal'),
               'air_dark': ('#3a3e42', 0.5, 0.5, 'metal'), 'glass': ('#2a3644', 0.08, 0.6, 'polished'), 'glass_bright': ('#b8ccd8', 0.1, 0.3, 'polished'),
               'glass_frame': ('#4a4e52', 0.5, 0.6, 'metal'), 'prop_black': ('#1a1a1c', 0.5, 0.3, 'metal'),
               'prop_tip': ('#d8b020', 0.5, 0.2, 'metal'), 'mark_white': ('#e6e4dc', 0.55, 0.1, 'metal'),
               'mark_red': ('#a8302a', 0.55, 0.1, 'metal'), 'gunmetal': ('#3a3c3e', 0.45, 1.0, 'metal'),
               'rubber': ('#1e1e1e', 0.85, 0.0, 'leather'), 'port': ('#0e0c0a', 0.9, 0.0, 'polished'),
               'afterburner': ('#ffb444', 0.5, 0.0, 'flame'), 'nozzle': ('#5a5650', 0.4, 1.0, 'metal'),
               'missile_white': ('#ecebe6', 0.45, 0.2, 'metal'), 'radome': ('#5a5e62', 0.5, 0.2, 'metal')}.items():
    props.DEFAULTS.setdefault(_k, _v)
props.DEFAULTS.setdefault('air_line', ('#4a4e4a', 0.55, 0.35, 'metal'))   # panel lines (a unit's cfg 'line' sets the colour)
props.DEFAULTS.setdefault('air_panel', ('#6a6e6a', 0.55, 0.35, 'metal'))  # a second panel tone (cfg 'panel')
props.DEFAULTS.setdefault('air_soot', ('#2c2a26', 0.7, 0.2, 'metal'))     # exhaust staining
WEAR = {'air_paint': {'grime': 0.3, 'soot': 0.15, 'edges': 0.35}, 'air_under': {'grime': 0.3},
        'air_dark': {'grime': 0.3}, 'nozzle': {'soot': 0.6, 'grime': 0.4}, 'prop_black': {'grime': 0.2}}


# Build-round knobs: AIR_OPTS='{"key": value}' overrides a unit's defaults (variants rendered side by side)
OPTS = json.loads(os.environ.get('AIR_OPTS', '{}'))


def opt(key, default):
    return OPTS.get(key, default)


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


def body(p, stations, key='air_paint', keys=None, seg=28, step=0.25, power=2.0, smooth=True, x=0.0):
    """A lofted fuselage through superellipse stations (y, z, half_w,
    half_h[, power]) running nose to tail; zero-size stations close it.
    x: its centre line's x (an engine nacelle's)."""
    rings = []
    for st in resample(stations, step):
        y, z, hw, hh = st[:4]
        pw = st[4] if len(st) > 4 else power
        ring = []
        for k in range(seg):
            a = 2 * math.pi * k / seg
            c, s = math.cos(a), math.sin(a)
            ring.append((x + hw * math.copysign(abs(c) ** (2 / pw), c), y, z + hh * math.copysign(abs(s) ** (2 / pw), s)))
        rings.append(ring)
    p.add(props.loft(rings), key, smooth=smooth, keys=keys)


def wing(p, stations, key='air_paint', keys=None, seg=12, side=1, vertical=False, matrix=None, step=None, smooth=True):
    """A lofted wing / fin through airfoil stations (s, y_le, chord, offset,
    thickness_ratio) from root to tip: s runs along the span (x times side,
    or z when vertical), offset is the chord line's z (x when vertical).
    seg=3 with smooth=False gives a faceted diamond section (a ridge at
    mid-chord)."""
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
    p.add(bm, key, matrix, smooth=smooth, keys=keys)


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


def lines(positions, width, axis='y', test=None):
    """Panel lines: faces whose centre lies within width/2 of one of the
    positions along an axis (and pass `test`), for a ('air_line', False)
    region."""
    def f(c, n):
        v = getattr(c, axis)
        if axis == 'x':
            v = abs(v)
        return any(abs(v - q) < width / 2 for q in positions) and (test is None or test(c, n))
    return f


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


def prop(p, at, r, blades=3, key='prop_black', tips=False, hub=0.2, spin=15.0, axis='Y', hub_key='air_dark', blade_w=0.17,
         spinner=1.0, pitch=18.0, thick=0.05, tip_in=0.0):
    """A propeller at `at` facing +Y: spinner and blades (a little pitched).
    hub_key 'tint' paints the spinner in the civ colour; spinner stretches it.
    pitch (degrees) and thick (m): the blades' pitch and thickness (a
    coarser pitch shows the blades edge-on); tip_in pulls the painted tips in
    (a fraction of r) so they end inside the blade."""
    m = T(*at)
    p.add(props.lathe([(0.0, hub), (hub * 1.6 * spinner, hub * 0.6), (hub * 2.2 * spinner, 0.0)], seg=16), hub_key, m @ R('X', -90),
          smooth=True, tint=hub_key == 'tint')
    for k in range(blades):
        a = spin + 360.0 * k / blades
        bm = m @ R('Y', a) @ T(0, hub * 0.4, 0)
        p.add(props.box(r * blade_w, thick, r * 0.92), key, bm @ T(0, 0, r / 2 + hub * 0.5) @ R('Z', pitch), bevel=0.01)
        if tips:
            p.add(props.box(r * 0.102, thick * 0.84, r * 0.1), 'prop_tip', bm @ T(0, 0, r + hub * 0.5 - r * (0.05 + tip_in)) @ R('Z', pitch))


def canopy(p, at, rx, ry, rz, frames=3, frame_r=0.02, spine=False, frame_key='glass_frame'):
    """spine: also a frame along the top, nose to tail (the framed hoods);
    frame_key: the frames' material."""
    x, y, z = at
    p.add(props.ellipsoid(rx, ry, rz, seg=20), 'glass', T(x, y, z), smooth=True)
    if spine:
        arc = [(x, y + ry * 0.98 * math.cos(math.pi * i / 16), z + rz * 1.02 * math.sin(math.pi * i / 16)) for i in range(1, 16)]
        p.add(props.tube(arc, frame_r, seg=5, caps=False), frame_key, smooth=True)
    for k in range(frames):
        yy = y + ry * (-0.6 + 1.2 * k / max(1, frames - 1))
        f = math.sqrt(max(0.0, 1 - ((yy - y) / ry) ** 2))
        ring = [(x + rx * f * 1.02 * math.cos(math.pi * i / 12), yy, z + rz * f * 1.02 * math.sin(math.pi * i / 12)) for i in range(13)]
        p.add(props.tube(ring, frame_r, seg=5, caps=False), frame_key, smooth=True)


# ---------------------------------------------------------------- units

def roundel(cx, cy, r, ring, top=True):
    """A civ disc with a white ring round it, on a wing (in plan): two
    regions, the disc first."""
    return [('tint', True, oval(cx, cy, r, r, top=top)), ('mark_white', False, oval(cx, cy, r + ring, r + ring, top=top))]


def side_roundel(cy, cz, r, ring):
    return [('tint', True, side_oval(cy, cz, r, r)), ('mark_white', False, side_oval(cy, cz, r + ring, r + ring))]


def fighter(p):
    """A Spitfire Mk IX: the elliptical low wing with its dihedral, the slim
    fuselage with the long Merlin nose, six exhaust stubs a side, the framed
    canopy and its rear fairing, the twin underwing radiators, wing cannons,
    the four-blade Rotol prop; RAF day-fighter paint (ocean grey with big
    dark-green disruptive patches, medium sea grey below), yellow
    leading-edge strips. Civ colour in three deliberate places: the upper
    wing roundels (white-ringed), the fuselage band with its side roundels,
    and the spinner."""
    rr, ring = opt('roundel_r', 0.62), opt('roundel_ring', 0.2)
    wx, wy = opt('roundel_x', 3.5), opt('roundel_y', 0.75)
    regions = roundel(wx, wy, rr, ring) + roundel(-wx, wy, rr, ring)
    regions += side_roundel(-1.25, 0.25, opt('side_r', 0.36), 0.1)
    regions += [('tint', True, band(-2.95, -2.95 + opt('band_w', 0.45)))]
    span = 5.6

    def chord(f):
        return max(0.35, 2.45 * math.sqrt(max(0.0, 1 - f * f)))

    def te_frac(c, k):   # is the face a wing face at fraction k of the chord from the trailing edge?
        x = abs(c.x)
        if x < 0.5 or x > 0.35 + span * 0.97:
            return None
        ch = chord((x - 0.35) / span)
        te = 1.55 - (2.45 - ch) * 0.3 - ch
        return abs(c.y - (te + k * ch)) < opt('line_w', 0.07) and abs(c.z - (-0.32 + (x - 0.35) * 0.105)) < 0.25
    if opt('lines', True):   # the aileron and flap hinge lines, the aileron ends, the wing's spar line, fuselage frames
        regions += [('air_line', False, lambda c, n: 3.0 < abs(c.x) < 5.3 and te_frac(c, 0.3)),
                    ('air_line', False, lambda c, n: 0.7 < abs(c.x) < 2.7 and te_frac(c, 0.22)),
                    ('air_line', False, lines((3.0, 5.3), 0.1, 'x', lambda c, n: any(te_frac(c, k / 10) for k in range(3)))),
                    ('air_line', False, lambda c, n: 0.7 < abs(c.x) < 5.0 and te_frac(c, 0.62)),
                    ('air_line', False, lines((2.7, -0.4, -1.9, -3.4), 0.07, 'y', lambda c, n: abs(c.x) < 0.6 and c.z > -0.2))]
    keys = paint_keys(regions)
    body(p, [(4.45, 0.0, 0.36, 0.44), (3.6, 0.0, 0.46, 0.6), (2.4, 0.02, 0.49, 0.66), (1.0, 0.04, 0.48, 0.64),
             (-0.6, 0.08, 0.42, 0.56), (-2.4, 0.18, 0.3, 0.42), (-3.9, 0.28, 0.14, 0.24), (-4.55, 0.32, 0.02, 0.06)],
         keys=keys, step=0.2)
    p.add(props.ellipsoid(0.34, 1.3, 0.3, seg=20), 'air_paint', T(0, -1.2, 0.45), smooth=True, keys=keys)   # the spine behind the canopy
    canopy(p, (0, 0.55, 0.6), 0.3, 0.75, 0.32)
    st = []
    for i in range(15):
        f = i / 14 * 0.985
        c = chord(f)
        st.append((0.35 + f * span, 1.55 - (2.45 - c) * 0.3, c, -0.32 + f * span * math.tan(math.radians(6)), 0.13))
    for side in (-1, 1):
        wing(p, st, keys=keys, side=side, seg=opt('wing_seg', 24), step=0.12)   # fine faces, so the roundels come out round
        for dx in (1.75, 2.3):   # the 20 mm cannons in the leading edge, then the .50s outboard
            gz = -0.32 + (dx - 0.35) * math.tan(math.radians(6))   # on the wing's chord line (they floated above it)
            vkit.rod(p, (side * dx, 1.0, gz), (side * dx, 2.1, gz), opt('gun_r', 0.065), 'gunmetal', seg=6)
        for k in range(6):   # exhaust stubs: a bold row along the Merlin's flanks
            p.add(props.box(0.13, 0.15, 0.14), 'air_dark', T(side * 0.47, 3.5 - 0.21 * k, 0.18) @ R('Y', side * 22), bevel=0.015)
        p.add(props.box(0.06, 1.3, 0.12), 'nozzle', T(side * 0.44, 2.98, 0.18) @ R('Y', side * 22))   # their sooty manifold
        # the radiators under each wing (Mk IX: symmetrical): deep boxes with dark intake mouths
        p.add(props.box(0.42, 1.3, 0.34), 'air_under', T(side * 1.05, 0.4, -0.58), bevel=0.07)
        p.add(props.box(0.36, 0.06, 0.24), 'port', T(side * 1.05, 1.06, -0.6))
    p.add(props.box(0.26, 0.9, 0.22), 'air_under', T(0, 2.7, -0.62), bevel=0.06)   # the carburettor intake under the nose
    p.add(props.box(0.2, 0.06, 0.14), 'port', T(0, 3.16, -0.63))
    tst = [(0.1 + f * 1.55, -3.5 - (1.15 - max(0.3, 1.15 * math.sqrt(max(0, 1 - f * f)))) * 0.3,
            max(0.3, 1.15 * math.sqrt(max(0, 1 - f * f))), 0.28, 0.1) for f in [i / 8 * 0.98 for i in range(9)]]
    for side in (-1, 1):
        wing(p, tst, keys=keys, side=side)
    fst = [(0.3 + f * 1.45, -3.15 - f * 0.5, max(0.35, 1.4 * math.sqrt(max(0, 1 - f * f))), 0.0, 0.1)
           for f in [i / 8 * 0.98 for i in range(9)]]
    wing(p, fst, keys=keys, vertical=True)
    p.add(props.box(0.04, 0.9, 0.5), 'air_dark', T(0, -4.25, 1.05))   # the rudder's hinge line
    prop(p, (0, 4.45, 0.02), 1.6, blades=4, hub=0.26, tips=True, hub_key=opt('spinner_key', 'tint'),
         blade_w=opt('blade_w', 0.18), spinner=1.3)


def bomber(p):
    """A B-17G: the long fuselage with the glass nose, the chin turret and
    the cheek guns in their blisters, cockpit glazing, the top turret, the
    ball turret, tail guns and waist windows; four Cyclone radials in deep
    cowlings ahead of the leading edge with three-blade props; the broad,
    tapered wing; the big fin with the long dorsal fillet; olive drab with
    panel lines, a neutral grey underside, black de-icer boots, white code
    letters. Civ colour: the white-ringed wing roundels and the fin's upper
    band (a bomb group's tail colour); the tailplanes stay olive drab."""
    rr, ring = opt('roundel_r', 1.3), opt('roundel_ring', 0.34)
    fz = opt('fin_band', 3.6)
    tip = opt('tail_tip', 99.0)
    span = 15.8
    le0, c0 = opt('root_le', 5.2), opt('root_chord', 7.2)

    def wle(f):   # the leading edge's y and the chord line's z at span fraction f
        return _lerp(le0, 2.1, f), -0.25 + f * span * math.tan(math.radians(4.5))
    regions = [('glass_bright', False, lambda c, n: c.y > 10.0)]
    regions += roundel(opt('roundel_x', 12.0), 1.2, rr, ring) + roundel(-opt('roundel_x', 12.0), 1.2, rr, ring)
    nac = (4.6, 9.0)
    if opt('soot', True):   # exhaust staining on the nacelles' rear flanks and bellies (the turbos' exhausts)
        def soot(c, n):
            x = abs(c.x)
            for nx in nac:
                if abs(x - nx) < 0.78:
                    le, wz = wle((nx - 1.0) / span)
                    return le - 3.4 < c.y < le - 0.6 and ((c.z < wz + 0.15 and (abs(n.x) > 0.3 or n.z < -0.2))
                                                          or (n.z > 0.3 and abs(x - nx) < 0.4 and c.y < le - 1.2))
            return False
        regions.append(('air_soot', False, soot))
    if opt('grey_sides', True):   # the neutral grey underside reaches up the fuselage's lower sides (B-17 demarcation)
        regions.append(('air_under', False, lambda c, n: abs(c.x) < 1.3 and n.z < -0.1 and -11.5 < c.y < 10.0))
    if opt('panels', True):   # tonal panels: the wings' leading-edge bays and alternate fuselage bays a lighter olive
        def panel(c, n):
            x = abs(c.x)
            if n.z < 0.2:
                return False
            if 1.3 < x < 16.6:
                le, wz = wle((x - 1.0) / span)
                ch = _lerp(c0, 2.1, (x - 1.0) / span)
                return abs(c.z - wz) < 0.45 and le - 0.25 * ch < c.y < le - 0.45 and (2.6 < x < 6.8 or 11.0 < x < 14.2)
            return x < 1.25 and (5.0 < c.y < 8.6 or -2.0 < c.y < 1.4 or -8.5 < c.y < -5.5)
        regions.append(('air_panel', False, panel))
    regions += [('tint', True, lambda c, n: abs(c.x) < 0.3 and c.z > fz and c.y < -5.0),             # the fin band
                ('tint', True, lambda c, n: abs(c.x) > tip and -12.5 < c.y < -7.9 and abs(c.z - 0.75) < 0.4),   # (tailplane tips: off)
                ('mark_white', False, letters('DN', -0.8, -0.1, 1.0))]
    if opt('boots', True):   # the black de-icer boots along the wings' leading edges (not across the nacelles)
        def boot(c, n):
            x = abs(c.x)
            if x < 1.4 or any(abs(x - nx) < 0.9 for nx in (4.6, 9.0)) or c.y < -5.0:
                return False
            le, wz = wle((x - 1.0) / span)
            return le - opt('boot_w', 0.45) < c.y < le + 0.2 and abs(c.z - wz) < 0.5
        regions.insert(1, ('rubber', False, boot))
    if opt('lines', True):   # panel lines: wing ribs and the aileron hinge, fuselage frames, the tailplanes' hinge
        def on_wing(c, n):
            x = abs(c.x)
            if x < 1.3 or x > 16.6:
                return None
            le, wz = wle((x - 1.0) / span)
            return le, _lerp(c0, 2.1, (x - 1.0) / span), abs(c.z - wz) < 0.45
        def rib(c, n):
            w = on_wing(c, n)
            return bool(w and w[2] and any(abs(abs(c.x) - q) < 0.13 for q in (2.6, 6.8, 11.0, 14.2)))
        def hinge(c, n):
            w = on_wing(c, n)
            return bool(w and w[2] and abs(c.x) > 10.0 and abs(c.y - (w[0] - w[1] * 0.75)) < 0.14)
        regions += [('air_line', False, rib), ('air_line', False, hinge),
                    ('air_line', False, lines((8.6, 5.0, 1.4, -2.0, -5.5, -8.5), 0.14, 'y', lambda c, n: abs(c.x) < 1.25 and c.z > -1.2))]
    keys = paint_keys(regions)
    body(p, [(11.45, -0.15, 0.08, 0.08), (11.0, -0.12, 0.72, 0.76), (9.9, -0.02, 1.0, 1.1), (7.0, 0.1, 1.15, 1.35),
             (2.0, 0.15, 1.2, 1.42), (-3.0, 0.25, 1.05, 1.25), (-7.0, 0.45, 0.72, 0.88), (-10.0, 0.65, 0.38, 0.48),
             (-11.3, 0.72, 0.16, 0.2)], keys=keys, step=0.25, seg=32)
    canopy(p, (0, 7.6, 1.2), 0.75, 1.3, 0.55, frames=4)
    # turrets: top (behind the cockpit, glazed), ball (under the waist), chin (under the nose); their twin .50s
    for at, r, gz, key in (((0, 6.2, 1.5), 0.62, 0.15, 'glass'), ((0, 0.8, -1.45), opt('ball_r', 0.82), 0.0, 'air_under'),
                           ((0, opt('chin_y', 11.1), -1.15), opt('chin_r', 0.75), 0.0, 'air_dark')):
        p.add(props.ellipsoid(r, r, r * 0.85, seg=18), key, T(*at), smooth=True)
        for dx in (-0.16, 0.16):
            vkit.rod(p, (at[0] + dx, at[1], at[2] + gz), (at[0] + dx, at[1] + 1.7, at[2] + gz), 0.045, 'gunmetal', seg=6)
    p.add(props.ellipsoid(0.4, 0.4, 0.34, seg=14), 'glass', T(0, 1.0, -1.45 - 0.45))   # the ball turret's sighting window
    p.add(props.lathe([(0.0, 0.66), (0.08, 0.66)], seg=20), 'air_dark', T(0, 6.2, 1.62))   # the top turret's frame ring
    p.add(props.lathe([(0.0, 0.5), (0.4, 0.5)], seg=18), 'air_under', T(0, opt('chin_y', 11.1), -0.72))    # the chin turret's mount
    for dx in (-0.12, 0.12):
        vkit.rod(p, (dx, -11.3, 0.72), (dx, -12.4, 0.72), 0.045, 'gunmetal', seg=6)
    for side in (-1, 1):   # waist windows and their guns; the cheek guns in their blisters
        p.add(props.box(0.04, 1.1, 0.55), 'port', T(side * 1.07, -3.2, 0.55))
        vkit.rod(p, (side * 1.05, -3.2, 0.55), (side * 1.9, -2.6, 0.5), 0.045, 'gunmetal', seg=6)
        p.add(props.ellipsoid(0.24, 0.5, 0.3, seg=14), 'glass_bright', T(side * 0.98, 9.4, 0.15), smooth=True)
        vkit.rod(p, (side * 1.05, 9.4, 0.12), (side * 1.4, 10.7, 0.05), 0.045, 'gunmetal', seg=6)
    # wings: broad at the root, tapered to the tips
    st = [(1.0 + f * span, wle(f)[0], _lerp(c0, 2.1, f), wle(f)[1], _lerp(0.17, 0.11, f)) for f in [i / 16 for i in range(17)]]
    for side in (-1, 1):
        wing(p, st, keys=keys, side=side, step=opt('wing_step', 0.15), seg=opt('wing_seg', 36))   # fine faces: round roundels
        for x in (4.6, 9.0):   # nacelles, deep cowlings ahead of the leading edge, cylinder ring, props
            le, wz = wle((x - 1.0) / span)
            cz = wz - 0.05
            body(p, [(le + 1.2, cz, 0.7, 0.7), (le - 1.5, wz - 0.1, 0.72, 0.68), (le - 3.6, wz - 0.05, 0.3, 0.28)],
                 keys=keys, seg=20, step=0.3, x=side * x)
            p.add(props.lathe([(0.0, 0.8), (0.4, 0.84), (1.3, 0.84), (1.5, 0.74)], seg=24), 'air_paint',
                  T(side * x, le + 1.1, cz) @ R('X', -90), smooth=True)   # the cowling
            p.add(props.lathe([(0.0, 0.66), (0.06, 0.66)], seg=20), 'nozzle', T(side * x, le + 2.57, cz) @ R('X', -90), smooth=True)
            p.add(props.lathe([(0.0, 0.34), (0.05, 0.34)], seg=16), 'air_dark', T(side * x, le + 2.62, cz) @ R('X', -90), smooth=True)
            prop(p, (side * x, le + 2.68, cz), 1.75, blades=3, tips=True, hub=0.28, spin=30 * side, blade_w=opt('blade_w', 0.26))
            p.add(props.box(0.25, 0.9, 0.2), 'nozzle', T(side * x, le - 1.8, wz - 0.7), bevel=0.05)   # the turbo
    # tailplane and the fin with its long dorsal fillet
    tst = [(0.3 + f * 6.7, _lerp(-8.3, -9.4, f), _lerp(3.6, 1.4, f), 0.72, 0.1) for f in [i / 8 for i in range(9)]]
    for side in (-1, 1):
        wing(p, tst, keys=keys, side=side, step=0.3, seg=16)
    body(p, [(-0.6, 1.45, 0.04, 0.04), (-3.0, 1.55, 0.26, 0.42), (-5.5, 1.6, 0.26, 0.85), (-7.5, 1.55, 0.22, 1.2),
             (-8.6, 1.5, 0.05, 0.05)], keys=keys, step=0.2, seg=16)   # the dorsal fillet
    fst = [(1.0, -2.6, 8.4, 0.0, 0.05), (1.6, -5.6, 5.8, 0.0, 0.08), (2.6, -7.6, 4.0, 0.0, 0.1),
           (5.2, -9.0, 2.6, 0.0, 0.1), (6.4, -9.8, 1.4, 0.0, 0.1)]
    wing(p, fst, keys=keys, vertical=True, step=0.25)


def helicopter(p):
    """A CH-47 Chinook: the long boxy fuselage with its glazed nose, the
    front pylon and the tall rear pylon with the engines on its flanks, two
    big three-blade rotors, the fuel sponsons, round windows and doors,
    the landing gear; pale grey with a darker belly, the civ spine stripe,
    rotor hubs, door panels and sponson stripes."""
    regions = [('glass', False, lambda c, n: c.y > 6.3 and c.z > 0.15 and n.y > -0.2),
               ('tint', True, lambda c, n: n.z > 0.6 and abs(c.x) < 0.8 and -4.8 < c.y < 5.3),
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
    canopy(p, cfg['canopy'], *cfg['canopy_r'], frames=cfg.get('canopy_frames', 2), frame_r=cfg.get('frame_r', 0.02))
    if cfg.get('spine'):   # a dorsal spine / hump behind the canopy
        body(p, cfg['spine'], keys=keys, step=0.2, seg=24)
    # the flat centre body between the engines
    p.add(props.box(cfg['body_w'], cfg['body_l'], cfg['body_h']), 'air_paint', T(0, cfg['body_y'], cfg['body_z']), bevel=0.25, keys=keys)
    for side in (-1, 1):
        ex = side * cfg['eng_x']
        y0, y1, r = cfg['eng_y0'], cfg['eng_y1'], cfg['eng_r']
        p.add(props.lathe([(0, r * 1.02), (y0 - y1 - 0.6, r), (y0 - y1, r * 0.82)], seg=20, scale_xy=(1.0, 1.05)), 'air_paint',
              T(ex, y0, cfg['eng_z']) @ R('X', 90), smooth=True, keys=keys)
        intake = cfg.get('intake', 'plain')
        if intake == 'box':   # the F-15's square intakes, standing tall beside the cockpit, the mouths raked
            iz = cfg['eng_z'] + 0.2
            p.add(props.box(r * 2.0, 2.6, r * 2.7), 'air_paint', T(ex, y0 - 0.9, iz), bevel=0.05, keys=keys)
            p.add(props.box(r * 1.8, 0.1, r * 2.45), 'port', T(ex, y0 + 0.36, iz) @ R('X', -12))
        elif intake == 'under':   # the MiG's wedge intakes under the root extensions, mouths raked down and back
            iz = cfg['eng_z'] - cfg.get('intake_drop', 0.1)
            p.add(props.box(r * 1.9, 2.2, r * 1.8), 'air_paint', T(ex, y0 - 0.7, iz), bevel=0.06, keys=keys)
            p.add(props.box(r * 1.7, 0.1, r * 1.6), 'port', T(ex, y0 + 0.4, iz - 0.05) @ R('X', 25))
        else:
            p.add(props.box(r * 1.9, 0.08, r * 2.0), 'port', T(ex, y0 + 0.02, cfg['eng_z']))      # the intake mouth
            p.add(props.box(r * 2.1, 0.5, r * 2.2), 'air_paint', T(ex, y0 - 0.2, cfg['eng_z']), bevel=0.06, keys=keys)
        p.add(props.lathe([(0, r * 0.82), (0.7, r * 0.72)], seg=20), 'nozzle', T(ex, y1, cfg['eng_z']) @ R('X', 90), smooth=True)
        p.add(props.lathe([(0, r * 0.6), (0.02, r * 0.6)], seg=16), 'afterburner', T(ex, y1 - 0.69, cfg['eng_z']) @ R('X', 90), smooth=True)
        wing(p, cfg['wing'], keys=keys, side=side, step=cfg.get('wing_step', 0.45), seg=cfg.get('wing_seg', 12))
        wing(p, cfg['lerx'], keys=keys, side=side, step=cfg.get('lerx_step'))
        wing(p, cfg['tail'], keys=keys, side=side, step=cfg.get('tail_step'))
        fx = side * cfg['fin_x']
        wing(p, cfg['fin'], keys=keys, vertical=True, step=cfg.get('fin_step'),
             matrix=T(fx, 0, cfg['fin_z']) @ R('Y', side * cfg['fin_cant']))
        for k, (mx, my, ml) in enumerate(cfg['missiles']):   # missiles on pylons under the wings
            x = side * mx
            wz = cfg['wing'][0][3] - 0.25
            p.add(props.box(0.08, ml * 0.4, 0.2), 'air_under', T(x, my - ml * 0.3, wz))
            p.add(props.lathe([(0, 0.0), (0.35, 0.1), (ml, 0.1)], seg=12), 'missile_white', T(x, my, wz - 0.22) @ R('X', 90), smooth=True)
            for a in (45, 135):
                p.add(props.box(0.01, 0.35, 0.42), 'air_dark', T(x, my - ml + 0.2, wz - 0.22) @ R('Y', a))


def jet_fighter(p):
    """A MiG-29 Fulcrum: the drooped nose, the long root extensions rising
    into the dorsal hump behind the canopy, wedge intakes under them, the
    widely spaced engines with the open tunnel between them, twin fins
    canted outward on the nacelles; grey with blue-grey patches. Civ
    colour: the fins' upper halves (where the stars go) and the wing-tip
    panels."""
    cfg = dict(L=17.3, nose=[(8.3, 0.02, 0.17, 0.17), (7.4, 0.12, 0.4, 0.4), (5.6, 0.3, 0.6, 0.64),
                             (4.0, 0.42, 0.7, 0.7), (2.0, 0.42, 0.55, 0.5), (0.0, 0.4, 0.4, 0.38), (-2.0, 0.4, 0.2, 0.2)],
               canopy=(0, 4.6, 0.98), canopy_r=(0.42, 1.25, 0.44),
               spine=[(3.6, 0.7, 0.05, 0.05), (2.8, 0.85, 0.5, 0.42), (0.0, 0.8, 0.62, 0.4), (-3.5, 0.55, 0.5, 0.3),
                      (-6.0, 0.4, 0.05, 0.05)],
               body_w=opt('body_w', 1.1), body_l=10.0, body_h=0.5, body_y=-2.4, body_z=0.25,
               eng_x=opt('eng_x', 1.35), eng_y0=2.8, eng_y1=-7.9, eng_r=0.62, eng_z=-0.3, intake='under',
               wing=[(1.3, 1.2, 5.6, 0.1, 0.07), (5.7, -2.4, 1.6, 0.0, 0.05)],
               lerx=[(0.4, 5.6, 5.0, 0.3, 0.09), (1.9, 1.6, 1.6, 0.15, 0.05)],
               tail=[(1.5, -5.0, 2.6, 0.0, 0.05), (4.1, -6.6, 1.2, 0.0, 0.05)],
               fin=[(0.0, -3.6, 3.0, 0.0, 0.07), (3.1, -5.8, 1.2, 0.0, opt('fin_tip_th', 0.1))], fin_x=opt('fin_x', 1.6),
               fin_z=0.35, fin_cant=opt('fin_cant', 18),
               missiles=[(3.2, 0.4, 3.0), (4.4, -0.4, 2.6)])
    cfg.update(wing_step=0.2, wing_seg=20, fin_step=0.15, tail_step=0.2, lerx_step=0.2)
    fz, tx = opt('fin_civ_z', 1.9), opt('tip_civ_x', 4.6)
    # civ colour in two deliberate places: the fins' upper halves (where the stars go) and the wing-tip panels
    regions = [('tint', True, lambda c, n: c.z > fz and abs(c.x) > 0.9 and c.y < -3.0),
               ('tint', True, lambda c, n: abs(c.x) > tx and n.z > 0.2)]
    if opt('lines', True):   # panel lines: the wing's spar and aileron lines, fuselage frames
        regions += [('air_line', False, lines((2.4, 3.6), 0.12, 'x', lambda c, n: n.z > 0.3 and -2.0 < c.y < 1.0 and c.z < 0.4)),
                    ('air_line', False, lines((5.4, 3.0, 0.5, -2.5), 0.12, 'y', lambda c, n: abs(c.x) < 0.75 and c.z > 0.6))]
    _jet_common(p, paint_keys(regions), cfg)


def f15(p):
    """An F-15C Eagle: the big shoulder-mounted clipped-delta wing, the
    square intakes standing beside the cockpit, twin upright parallel fins,
    the bubble canopy; smooth two-tone light Mod Eagle grey. Civ colour on
    the fin caps and the wing tips."""
    cfg = dict(L=19.4, nose=[(9.6, 0.4, 0.18, 0.18), (8.6, 0.4, 0.45, 0.45), (6.6, 0.45, 0.72, 0.76),
                             (4.6, 0.55, 0.8, 0.8), (2.0, 0.55, 0.6, 0.5), (0.0, 0.5, 0.45, 0.4), (-2.0, 0.5, 0.2, 0.2)],
               canopy=(0, 5.4, 1.2), canopy_r=(0.5, 1.6, 0.55), canopy_frames=3, frame_r=0.05,
               body_w=2.9, body_l=11.0, body_h=0.6, body_y=-2.6, body_z=0.3,
               eng_x=1.15, eng_y0=3.6, eng_y1=-8.6, eng_r=0.72, eng_z=-0.1, intake='box',
               wing=[(1.6, 2.4, 8.0, 0.3, opt('wing_th', 0.09)), (6.6, -3.2, opt('tip_chord', 2.3), 0.25, opt('wing_th', 0.09))],
               lerx=[(1.3, 3.6, 1.8, 0.3, 0.06), (1.8, 2.4, 0.8, 0.28, 0.06)],
               tail=[(1.6, -5.6, 3.0, 0.1, 0.08), (4.4, -7.6, 1.4, 0.1, 0.08)],
               fin=[(0.0, -4.4, 3.6, 0.0, opt('fin_th', 0.13)), (opt('fin_h', 4.0), -6.9, 1.5, 0.0, opt('fin_th', 0.13) * 1.3)],
               fin_x=1.35, fin_z=0.55, fin_cant=0,
               missiles=[(3.6, 0.6, 3.6), (5.0, -0.6, 2.9)])
    fz, tx = opt('fin_civ_z', 2.6), opt('tip_civ_x', 5.1)
    # civ colour on the fin caps (a squadron band) and the wing tips
    regions = [('tint', True, lambda c, n: c.z > fz and abs(c.x) > 0.9 and c.y < -3.5),
               ('tint', True, lambda c, n: n.z > 0.2 and abs(c.x) > tx and c.y > -4.6 and -0.2 < c.z < 0.9)]
    if opt('lines', True):
        regions += [('air_line', False, lines((2.8, 4.4), 0.12, 'x', lambda c, n: n.z > 0.3 and -3.0 < c.y < 1.8 and c.z < 0.7)),
                    ('air_line', False, lines((6.4, 3.4, 0.6, -2.6), 0.12, 'y', lambda c, n: abs(c.x) < 0.8 and c.z > 0.7)),
                    ('air_panel', False, lambda c, n: n.z > 0.3 and 2.8 < abs(c.x) < 4.4 and -1.2 < c.y < 1.6 and c.z < 0.7),
                    ('air_panel', False, lambda c, n: n.z > 0.5 and abs(c.x) < 0.8 and 0.6 < c.y < 3.4 and c.z > 0.7)]
    cfg.update(wing_step=0.2, wing_seg=20, fin_step=0.15, tail_step=0.2)
    _jet_common(p, paint_keys(regions), cfg)


def facet_hull(p, stations, key='air_paint', keys=None):
    """A faceted hull through stations (y, z, chine_half_w, top_h,
    ridge_half_w, bottom_h): each ring is a hexagon (the chines, the flat
    top between two ridge lines, a flat belly), lofted with flat shading
    and no resampling, so every facet is one flat plane catching the light
    its own way. Returns the rings (for edge highlights)."""
    rings = []
    for y, z, hw, th, rw, bh in stations:
        rings.append([(hw, y, z), (rw, y, z + th), (-rw, y, z + th), (-hw, y, z), (-hw * 0.8, y, z - bh), (hw * 0.8, y, z - bh)])
    p.add(props.loft(rings), key, smooth=False, keys=keys)
    return rings


def stealth_fighter(p):
    """An F-117A Nighthawk: the faceted pyramid hull (flat facets meeting at
    hard chines and ridges, each its own light value), the stepped faceted
    canopy, thin wings on the 67.5 degree leading edge with the notched W
    trailing edge, gridded intakes on the shoulders, the platypus slot
    exhaust under its lip, two big V tails canted outward; near-black with
    grey edge highlights. Civ colour: the V tails, whole panels."""
    sweep = math.tan(math.radians(90 - 67.5))   # x per y along the leading edge
    nose = opt('nose_y', 10.0)
    tz = opt('tail_civ_z', -99.0)   # the tails' civ above this height (default: whole panels)
    keys = paint_keys([('tint', True, lambda c, n: n.z > 0.2 and abs(c.x) > opt('tip_civ_x', 99.0))], under=False)
    th = opt('hull_h', 1.5)   # the pyramid's height scale
    wd = opt('hull_w', 1.6)   # the pyramid's width scale: its chines reach the wing roots
    rings = facet_hull(p, [(nose, 0.35, 0.03, 0.02, 0.01, 0.01), (6.2, 0.35, 0.95 * wd, 0.75 * th, 0.25, 0.18),
                           (3.0, 0.35, 1.7 * wd, 1.25 * th, opt('ridge', 0.2), 0.25), (0.0, 0.35, 2.0 * wd, 1.35 * th, opt('ridge', 0.2), 0.28),
                           (-4.5, 0.35, 1.8 * wd, 1.0 * th, opt('ridge', 0.2), 0.25), (-8.6, 0.35, 1.1 * wd, 0.45 * th, 0.4, 0.15),
                           (-9.8, 0.35, 0.7, 0.18, 0.3, 0.08), (-9.82, 0.35, 0.02, 0.01, 0.01, 0.01)], keys=keys)
    if opt('edges', True):   # grey highlights along the chines and the ridge lines
        for j in (0, 1, 2, 3):
            pts = [r[j] for r in rings[1:-2]]
            p.add(props.tube(pts, opt('edge_r', 0.045), seg=5, caps=True), 'air_line', smooth=True)
    # the stepped faceted canopy: two flat panes a side and the windscreen, dark frames
    cz = 0.35 + 1.25 * th
    facet_hull(p, [(6.0, cz - 0.42, 0.05, 0.05, 0.02, 0.02), (4.7, cz - 0.15, 0.48, 0.32, 0.2, 0.05),
                   (3.4, cz - 0.02, 0.52, 0.36, 0.22, 0.05), (2.2, cz, 0.5, 0.2, 0.2, 0.05),
                   (1.6, cz, 0.05, 0.05, 0.02, 0.02)], 'glass')
    for y, z in ((4.7, cz + 0.17), (3.4, cz + 0.34)):
        p.add(props.box(1.0, 0.07, 0.06), 'air_dark', T(0, y, z))
    # the wings: thin plates on the nose's sweep line; the trailing edge notched (a W with the tail)
    root, tipx = 1.2, 6.6
    te_pts = [(root, -8.6), (opt('notch_x', 3.0), opt('notch_y', -4.9)), (tipx, -7.5)]
    st = []
    for i in range(23):
        x = root + (tipx - root) * i / 22
        le = nose - 1.2 - x / sweep
        for (x0, t0), (x1, t1) in zip(te_pts[:-1], te_pts[1:]):
            if x0 <= x <= x1:
                te = _lerp(t0, t1, (x - x0) / (x1 - x0))
        st.append((x, le, max(1.0, le - te), 0.3, opt('wing_th', 0.08)))
    for side in (-1, 1):
        wing(p, st, keys=keys, side=side, seg=3, smooth=False)   # faceted: two flat panels meeting at a spanwise ridge
        # the V tails (all-moving), canted outward
        tail_keys = paint_keys([('tint', True, lambda c, n: c.z > tz)], under=False)
        wing(p, [(0.0, -5.0, 3.6, 0.0, 0.07), (opt('tail_h', 3.4), -7.6, 1.5, 0.0, opt('tail_tip_th', 0.13))], keys=tail_keys, vertical=True,
             matrix=T(side * 1.05, 0, 0.35 + opt('tail_z', 0.55) * th) @ R('Y', side * opt('tail_cant', 45)), step=0.15)   # rooted in the hull
        # the gridded intakes on the shoulders, set into the upper chine facets
        p.add(props.box(0.95, 1.3, 0.05), 'port', T(side * 1.25, 2.2, 0.35 + 0.72 * th) @ R('Y', side * 38))
        for k in range(6):
            p.add(props.box(0.97, 0.035, 0.07), 'air_line', T(side * 1.25, 1.65 + 0.22 * k, 0.37 + 0.72 * th) @ R('Y', side * 38))
    # the platypus slot exhaust: a dark slot under a broad lip
    p.add(props.box(3.0, 0.7, 0.2), 'port', T(0, -9.4, 0.42))
    p.add(props.box(3.6, 0.9, 0.1), 'air_line', T(0, -9.6, 0.6), bevel=0.02)   # the lip, lighter so it reads


def stealth_bomber(p):
    """A B-2A Spirit: the flying wing, leading edge swept 33 degrees, the
    double-W sawtooth trailing edge (its edges parallel to the leading
    edges), the centre body blended into a hump with the four cockpit
    windows, the engine humps with their faired, lipped inlets and the
    exhaust troughs; dark grey with lighter leading-edge panels, elevon
    hinge lines and soft tone variation. Civ colour: a full-chord band
    across each outer wing."""
    span = 26.0
    tan33 = math.tan(math.radians(33))
    te = opt('te', [(0.0, -7.6), (5.5, -3.4), (11.5, -7.3), (15.0, -5.0), (span, -12.1)])

    def te_y(s):
        for (s0, t0), (s1, t1) in zip(te[:-1], te[1:]):
            if s0 <= s <= s1:
                return _lerp(t0, t1, (s - s0) / (s1 - s0))
        return te[-1][1]

    def le_y(s):
        return 9.0 - (s - 1.0) * tan33

    def outer_band(c, n):   # civ: a painted panel across each outer wing (edges on the face grid)
        x = abs(c.x)
        if n.z < 0.2 or not (opt('civ_s0', 19.6) < x < opt('civ_s1', 23.4)):
            return False
        ch = le_y(x) - te_y(x)
        return (le_y(x) - c.y) < opt('civ_frac', 1.0) * ch
    regions = [('tint', True, outer_band)]
    if opt('lines', True):
        def le_panel(c, n):   # the lighter leading-edge panels
            x = abs(c.x)
            return 4.0 < x < span and n.z > 0.1 and le_y(x) - opt('le_w', 0.7) < c.y and c.z < 1.0
        def bays(c, n):   # tonal panels: alternate spanwise bays a lighter charcoal
            x = abs(c.x)
            return n.z > 0.2 and (6.0 < x < 9.0 or 12.5 < x < 15.0) and c.z < 1.0
        def elevon(c, n):   # the elevons' hinge lines on the outer trailing edges
            x = abs(c.x)
            if not (11.8 < x < span - 0.6) or n.z < 0.2:
                return False
            ch = le_y(x) - te_y(x)
            return abs(c.y - (te_y(x) + 0.28 * ch)) < 0.13 or (12.0 < x < 12.25) or (19.6 < x < 19.85 and c.y < te_y(x) + 0.3 * ch)
        regions += [('air_line', False, le_panel), ('air_dark', False, elevon), ('air_panel', False, bays)]
    keys = paint_keys(regions)
    # the centre body, blended up into the hump
    body(p, [(10.0, 0.2, 0.2, 0.08), (8.5, 0.4, 2.0, 0.95), (5.5, 0.5, 3.2, opt('hump', 1.7)), (2.0, 0.45, 3.8, 1.2),
             (-2.0, 0.3, 3.4, 0.7), (-5.0, 0.2, 2.2, 0.35), (-7.4, 0.15, 0.6, 0.1)], keys=keys, step=0.3, seg=28)
    # the cockpit's four windows on the hump's front slope
    for wx in (-0.95, -0.33, 0.33, 0.95):   # (on the hump's surface: they sat under it before)
        p.add(props.box(0.56, 0.62, 0.08), 'port', T(wx, 7.0, opt('win_z', 1.78) - abs(wx) * 0.16) @ R('X', -38) @ R('Y', -wx * 12), bevel=0.01)
        p.add(props.box(0.64, 0.7, 0.05), 'air_line', T(wx, 7.0, opt('win_z', 1.78) - abs(wx) * 0.16 - 0.03) @ R('X', -38) @ R('Y', -wx * 12))
    # the wing
    st = []
    ns = opt('stations', 60)
    for i in range(ns + 1):
        sp = 1.0 + (span - 1.0) * i / ns
        le = le_y(sp)
        st.append((sp, le, max(0.6, le - te_y(sp)), 0.2 - (sp - 1) * 0.01, _lerp(opt('th_root', 0.17), opt('th_tip', 0.09), i / ns)))
    for side in (-1, 1):
        wing(p, st, keys=keys, side=side, seg=opt('wing_seg', 24))
        # the engine humps (one blended pair a side), their faired inlets with a lighter lip, the exhaust troughs
        ex = opt('eng_x', 2.3)   # the engines blended into the centre hump's flanks
        p.add(props.ellipsoid(1.7, 4.6, 0.95, seg=24), 'air_paint', T(side * ex, 1.8, 0.85), smooth=True, keys=keys)
        p.add(props.box(1.9, 0.5, 0.06), 'port', T(side * ex, 4.3, 1.42) @ R('X', -12))      # the flush inlet
        p.add(props.box(2.1, 0.16, 0.05), 'air_line', T(side * ex, 4.58, 1.47) @ R('X', -12))   # its lip
        p.add(props.box(2.2, 0.6, 0.06), 'nozzle', T(side * ex, -2.8, 0.75))
        if opt('troughs', True):   # the exhaust troughs behind the humps: dark, scorched recesses on the top skin
            p.add(props.box(2.25, 2.8, 0.05), 'nozzle', T(side * ex, -2.0, 0.66) @ R('X', 3), bevel=0.02)


def _rocket(p, r, stages, nose, base_band, patches=True, skirt=None):
    """A standing missile (+Z up): engine bells under the first stage, the
    stages with dark interstage rings and a raceway, a hatch, the white
    shroud with a civ band, the nose cone and the civ-tipped antenna."""
    regions = [('tint', True, lambda c, n: base_band[0] < c.z < base_band[1])]
    if patches:   # the civ panels scattered on the stages (as the original)
        regions.append(('tint', True, lambda c, n: base_band[1] < c.z < stages[-1][1] and
                        noise.noise(Vector((c.x * 1.2, c.y * 1.2, c.z * 0.5)) + Vector((3.0, 1.0, 7.0))) > 0.16))
    keys = paint_keys(regions, under=False)
    for k in range(4):   # the engine bells
        a = math.radians(45 + 90 * k)
        p.add(props.lathe([(0.0, r * 0.18), (stages[0][0], r * 0.32)], seg=12), 'nozzle',
              T(r * 0.45 * math.cos(a), r * 0.45 * math.sin(a), 0.0), smooth=True)
    if skirt:
        p.add(props.lathe([(skirt[0], r * 1.18), (skirt[1], r * 1.04)], seg=28), 'tint', smooth=True, tint=True)
    for i, (z0, z1, rr) in enumerate(stages):
        n = max(2, int((z1 - z0) / 0.2))   # fine rings, so the painted panels land on the skin
        p.add(props.lathe([(z0 + (z1 - z0) * i / n, r * rr) for i in range(n + 1)], seg=40), 'air_paint', smooth=True, keys=keys)
        for z in [z0 + 1.4 * (j + 1) for j in range(int((z1 - z0) / 1.4) - 0)][:-1]:   # the panel seams
            p.add(props.lathe([(z - 0.03, r * rr * 1.006), (z + 0.03, r * rr * 1.006)], seg=40), 'air_dark', smooth=True)
        if i:
            p.add(props.lathe([(z0 - 0.15, r * rr * 1.03), (z0 + 0.25, r * rr * 1.03)], seg=32), 'air_dark', smooth=True)
    zt = stages[-1][1]
    p.add(props.box(0.08 * r, 0.16 * r, zt - stages[0][0] - 0.4), 'air_dark', T(r * 1.0, 0, (zt + stages[0][0]) / 2))   # the raceway
    p.add(props.box(0.05, r * 0.35, r * 0.6), 'mark_red', T(-r * 0.99, 0.0, stages[0][1] * 0.62))                       # the hatch
    z0, band, cone, tip = nose
    p.add(props.lathe([(zt, r * 0.98), (zt + band, r * 0.98)], seg=32), 'tint', smooth=True, tint=True)
    prof = [(zt + band, r * 0.98)] + [(zt + band + cone * t, r * 0.98 * math.cos(t * math.pi / 2) ** 0.7) for t in (0.3, 0.55, 0.75, 0.9, 1.0)]
    p.add(props.lathe(prof, seg=32), 'mark_white', smooth=True)
    vkit.rod(p, (0, 0, zt + band + cone - 0.05), (0, 0, zt + band + cone + tip), r * 0.07, 'tint', seg=8, tint=True)


def icbm(p):
    """An ICBM on its pad: two grey stages with civ panels, white nose."""
    _rocket(p, 0.95, [(0.8, 8.0, 1.0), (8.4, 13.0, 0.95)], (None, 0.9, 2.4, 1.2), (0.8, 2.2))


def tactical_nuke(p):
    """A slim tactical missile on its tail: a civ skirt, two stages."""
    _rocket(p, 0.5, [(0.6, 6.0, 1.0), (6.3, 9.6, 0.96)], (None, 0.55, 1.5, 0.8), (0.6, 1.6), skirt=(0.6, 1.7))


def cruise_missile(p):
    """A Tomahawk in flight: the dark radome, white body with grey
    sections and two wide civ bands, the pop-out wings, cruciform tail
    fins and the exhaust."""
    regions = [('tint', True, band(0.2, 1.0)), ('tint', True, band(-1.7, -1.0)),
               ('air_paint', False, lambda c, n: 1.6 < c.y < 2.2 or -2.6 < c.y < -2.1)]
    keys = paint_keys(regions, under=False)
    prof = [(-3.0, 0.2)] + [(-2.8 + 0.1 * i, 0.26) for i in range(53)] + [(2.9, 0.2), (3.1, 0.1), (3.18, 0.0)]
    p.add(props.lathe(prof, seg=28),
          'mark_white', R('X', -90), smooth=True, keys=keys)
    p.add(props.lathe([(2.75, 0.215), (2.9, 0.2), (3.1, 0.1), (3.18, 0.0)], seg=28), 'radome', R('X', -90) @ T(0, 0, 0.003), smooth=True)
    p.add(props.lathe([(-3.15, 0.14), (-3.0, 0.2)], seg=20), 'nozzle', R('X', -90), smooth=True)
    for side in (-1, 1):   # the wings
        wing(p, [(0.15, 0.6, 0.55, 0.05, 0.08), (1.35, 0.25, 0.4, 0.05, 0.06)], 'air_dark', side=side)
    for a in (45, 135, 225, 315):   # the tail fins
        p.add(props.box(0.03, 0.5, 0.42), 'air_paint', R('Y', a) @ T(0, -2.7, 0.42), bevel=0.01)
    p.add(props.box(0.12, 0.3, 0.06), 'air_dark', T(0, -0.6, -0.27))   # the air scoop


# ---------------------------------------------------------------- the NumPy remakes (realism/numpy-air)

props.DEFAULTS.setdefault('bomb_olive', ('#2a2c20', 0.25, 0.7, 'metal'))   # US GP bombs: olive drab, a satin sheen (a light edge)
props.DEFAULTS.setdefault('bomb_band', ('#c8b040', 0.5, 0.2, 'metal'))    # the bombs' yellow nose band
props.DEFAULTS.setdefault('frame_light', ('#a8acae', 0.4, 0.6, 'metal'))  # bright canopy frames
props.DEFAULTS.setdefault('turbo', ('#8c8a84', 0.4, 1.0, 'metal'))        # bare-metal turbo-superchargers


def gp_bomb(p, at, length=1.75, r=0.26, key='bomb_olive', band=False):
    """A general-purpose bomb pointing +Y: an ogive nose, a parallel body,
    the tapered tail cone and its box fins."""
    x, y, z = at
    L = length
    prof = [(0.0, 0.0), (0.12 * L, r * 0.62), (0.28 * L, r), (0.62 * L, r), (0.86 * L, r * 0.45), (L, r * 0.36)]
    p.add(props.lathe([(L - a, b) for a, b in reversed(prof)], seg=14), key, T(x, y - L / 2, z) @ R('X', -90), smooth=True)
    for a in (45, 135):
        p.add(props.box(r * 2.4, L * 0.2, 0.025), 'air_dark', T(x, y - L * 0.43, z) @ R('Y', a))
    if band:   # the yellow nose band
        p.add(props.lathe([(0.0, r * 0.97), (L * 0.06, r * 1.0)], seg=14), 'bomb_band', T(x, y + L * 0.18, z) @ R('X', -90), smooth=True)


def p38(p):
    """A P-38J Lightning fighter-bomber: the central gondola with the
    bubble canopy and the nose guns, two Allison booms with chin
    radiators, side radiator scoops and the turbo-superchargers on top,
    twin oval fins joined by the tailplane, counter-rotating three-blade
    props, two 1,000 lb bombs on the centre-section pylons. Olive drab
    over neutral grey. Civ colour: white-ringed roundels on the outer
    wings, the fins' tops and the spinners."""
    bx = opt('boom_x', 2.44)
    span = 7.93
    rr, ring = opt('roundel_r', 0.74), opt('roundel_ring', 0.22)
    wx, wy = opt('roundel_x', 5.2), opt('roundel_y', 0.06)

    def le_y(x):   # the leading edge: straight over the centre section, a slight sweep outboard
        return 1.35 if x <= bx else _lerp(1.35, 0.8, (x - bx) / (span - bx))

    def chord(x):
        c = 3.0 if x <= bx else _lerp(3.0, 1.15, (x - bx) / (span - bx))
        f = (x - (span - 0.45)) / 0.45   # a rounded tip
        return c * math.sqrt(max(0.05, 1 - f * f)) if f > 0 else c

    def wz(x):   # the chord line: a flat centre section, 5 degrees of dihedral outboard
        return -0.2 + max(0.0, x - bx) * math.tan(math.radians(opt('dihedral', 5.0)))
    regions = roundel(wx, wy, rr, ring) + roundel(-wx, wy, rr, ring)
    on_boom = lambda c: abs(abs(c.x) - bx) < 0.56 and c.y > -6.3 and c.z < 0.75
    if opt('nose_dark', True):   # the dark gun cluster on the gondola's nose
        regions.append(('air_dark', False, lambda c, n: abs(c.x) < 0.5 and c.y > 3.0))
    if opt('nacelle_band', False):   # the dark nacelle bands (exhaust staining) behind the props
        regions.append(('air_soot', False, lambda c, n: on_boom(c) and 2.55 < c.y < 3.25 and c.z > -0.4))
    if opt('top_stripe', False):   # a lighter top edge on the booms and the gondola
        regions.append(('air_panel', False, lambda c, n: n.z > 0.8 and c.z > 0.3 and (on_boom(c) or abs(c.x) < 0.5)
                                                          and -6.0 < c.y < 2.5 and not (abs(c.x) < 0.5 and -0.2 < c.y < 2.0)))
    if opt('belly', True):   # the neutral grey below the booms' and the gondola's centre lines (a straight demarcation)
        regions.append(('air_under', False, lambda c, n: (on_boom(c) or abs(c.x) < 0.6) and c.z < -0.12 and n.z < 0.3))
    if opt('band_w', 0.0):   # (off: on the thin booms a band read as a dot; round 2 had it on by mistake)
        by0 = opt('band_y', -3.75)
        regions += [('tint', True, lambda c, n: abs(abs(c.x) - bx) < 0.5 and by0 - opt('band_w', 0.7) < c.y < by0 and c.z > -0.4)]
    if opt('boom_tone', True):   # one calm, darker olive over each boom (scoops and intakes in the same tone)
        regions.append(('air_panel', False, lambda c, n: abs(abs(c.x) - bx) < 0.49 and -6.3 < c.y < 3.3))
    fz = opt('fin_tint', None)   # (round 2: the fins' tops, which read as blobs)
    if fz is not None:
        regions += [('tint', True, lambda c, n: abs(abs(c.x) - bx) < 0.2 and c.y < -6.0 and c.z > fz)]
    ry0 = opt('rudder_band', -7.62)   # civ colour on a thin band down each rudder's trailing part
    if ry0 is not None:
        regions += [('tint', True, lambda c, n: abs(abs(c.x) - bx) < 0.2 and c.y < ry0 and c.z > opt('rudder_z', 0.35))]
    if opt('lines', True):   # flap and aileron hinge lines, the boom joints
        def hinge(c, n):
            x = abs(c.x)
            if x < 0.6 or x > span - 0.3 or abs(c.z - wz(x)) > 0.3 or abs(x - bx) < 0.5:
                return False
            return abs(c.y - (le_y(x) - chord(x) * 0.74)) < 0.07
        regions += [('air_line', False, hinge),
                    ('air_line', False, lambda c, n: abs(abs(c.x) - bx) < 0.5 and any(abs(c.y - q) < 0.05 for q in (2.1, -2.0, -4.2)))]
    keys = paint_keys(regions)
    # the gondola: nose guns ahead, the cockpit over the wing, a short tail cone
    body(p, [(3.55, 0.12, 0.1, 0.1), (3.35, 0.1, 0.36, 0.38), (2.8, 0.06, 0.5, 0.56), (1.6, 0.05, 0.56, 0.68),
             (0.2, 0.05, 0.56, 0.66), (-1.0, 0.05, 0.46, 0.5), (-1.9, 0.08, 0.2, 0.22), (-2.2, 0.1, 0.03, 0.04)],
         keys=keys, step=0.2)
    canopy(p, (0, 0.9, 0.55), 0.4, 1.05, 0.5, frames=opt('frames', 4), frame_r=opt('frame_r', 0.035), spine=opt('spine', True))
    for dx, dz, ln in ((-0.12, 0.26, 0.55), (0.12, 0.26, 0.55), (-0.2, 0.12, 0.45), (0.2, 0.12, 0.45), (0.0, -0.04, 0.75)):
        vkit.rod(p, (dx, 3.2, dz), (dx, 3.45 + ln, dz), opt('gun_r', 0.07) * (1.4 if dx == 0 else 1.0), 'gunmetal', seg=6)
    # the booms
    for side in (-1, 1):
        x = side * bx
        body(p, [(3.7, 0.05, 0.3, 0.3), (3.3, 0.02, 0.46, 0.52), (2.3, -0.02, 0.5, 0.62), (0.6, 0.0, 0.48, 0.6),
                 (-1.2, 0.1, 0.42, 0.5), (-2.6, 0.15, 0.32, 0.4), (-4.6, 0.2, 0.22, 0.28), (-7.0, 0.25, 0.17, 0.2),
                 (-7.9, 0.26, 0.08, 0.1), (-8.05, 0.26, 0.02, 0.03)], keys=keys, step=0.2, x=x)
        # the chin radiator intakes (the J's 'beard') under the spinner
        p.add(props.box(0.62, 1.1, 0.34), opt('scoop_key', 'air_panel'), T(x, 2.75, -0.58), bevel=0.1)
        p.add(props.box(0.5, 0.06, 0.18), 'port', T(x, 3.31, -0.58))
        # the coolant radiator scoops on both flanks behind the wing
        for s2 in (-1, 1):
            p.add(props.box(0.26, 1.5, 0.48), opt('scoop_key', 'air_panel'), T(x + s2 * 0.41, -3.1, 0.12) @ R('Z', s2 * 5), bevel=0.05)
            p.add(props.box(0.16, 0.06, 0.36), 'port', T(x + s2 * 0.42, -2.36, 0.12))
        # the turbo-supercharger on top of the boom over the trailing edge
        tr = opt('turbo_r', 0.3)
        p.add(props.lathe([(0.0, tr * 0.83), (tr * 0.5, tr), (tr * 0.83, 0.0)], seg=14), 'turbo', T(x, -0.9, 0.48), smooth=True)
        p.add(props.box(0.18, 0.7, 0.1), 'nozzle', T(x, -1.3, 0.48), bevel=0.03)
        prop(p, (x, 3.72, 0.05), 1.75, blades=3, tips=True, hub=opt('hub', 0.3), spin=25 * side, hub_key=opt('spinner_key', 'tint'),
             blade_w=opt('blade_w', 0.34), spinner=1.4, pitch=opt('pitch', 45.0), thick=opt('blade_t', 0.14), tip_in=0.05)
        # the twin oval fins (above and below the boom) and their rudders
        ft = opt('fin_top', 2.35)   # an oval fin, taller than wide
        fst = [(-0.72, -7.0, 0.5, x, 0.08), (-0.4, -6.72, 1.05, x, 0.1), (0.25, -6.5, 1.45, x, 0.11),
               (0.3 + (ft - 0.3) * 0.45, -6.42, 1.5, x, 0.11), (0.3 + (ft - 0.3) * 0.8, -6.55, 1.2, x, 0.1),
               (ft - 0.12, -6.78, 0.68, x, 0.08), (ft, -6.98, 0.28, x, 0.06)]
        wing(p, fst, keys=keys, vertical=True, step=0.12)
        if opt('fin_edge', True):   # a thin dark edge round the fin
            edge = [(x, y, s) for s, y, c, _, _ in fst] + [(x, y - c, s) for s, y, c, _, _ in reversed(fst)]
            p.add(props.tube(edge, 0.035, seg=5, caps=False), 'air_line', smooth=True)
        p.add(props.box(0.04, 0.05, 2.6), 'air_dark', T(x, -7.6, 0.75))   # the rudder's hinge line
    # the wing: one piece from the gondola to the tips, through the booms
    st = []
    xs = [0.45 + i * 0.12 for i in range(int((span - 0.45) / 0.12) + 1)] + [span]
    for x in xs:
        st.append((x, le_y(x), chord(x), wz(x), _lerp(0.16, 0.1, x / span)))
    for side in (-1, 1):
        wing(p, st, keys=keys, side=side, seg=opt('wing_seg', 24))
    # the tailplane between the booms, a little beyond them
    tst = [(f * (bx + 0.55), -6.55, _lerp(1.35, 1.0, f ** 3), 0.3, 0.1) for f in [i / 12 for i in range(13)]]
    for side in (-1, 1):
        wing(p, tst, keys=keys, side=side, step=0.15, seg=14)
    # the pylons and their bombs under the centre section
    if opt('bombs', True):
        for side in (-1, 1):
            bxx = side * opt('pylon_x', 1.25)   # mid-way between the gondola and the boom, inside the prop discs' reach
            bz, by = opt('bomb_z', -1.3), opt('bomb_y', 1.8)   # hung low and forward: the nose well ahead of the leading edge
            p.add(props.box(0.14, 1.4, -bz - 0.2), 'air_dark', T(bxx, 1.15, (bz - 0.2) / 2 - 0.05), bevel=0.03)
            gp_bomb(p, (bxx, by, bz), length=opt('bomb_l', 2.8), r=opt('bomb_r', 0.31), band=opt('bomb_band', True))


def wildcat(p):
    """An F4F-4 Wildcat: the barrel fuselage behind the big round cowling
    of the twin-row Wasp, the mid-set square-tipped wing with four .50s a
    side, the framed canopy and the faired turtle deck, the wheels in their
    wells in the fuselage flanks, the tall rounded fin; 1942 blue-grey over
    light grey. Civ colour: white-ringed roundels on the upper wings and
    the fuselage sides, and the cowling's front ring."""
    span = 5.79
    rr, ring = opt('roundel_r', 0.56), opt('roundel_ring', 0.18)
    wx, wy = opt('roundel_x', 4.1), opt('roundel_y', 0.4)

    def le_y(x):
        return _lerp(1.75, 1.15, (x - 0.5) / (span - 0.5))

    def chord(x):
        return _lerp(2.55, 1.45, (x - 0.5) / (span - 0.5))

    def wz(x):
        return -0.12 + (x - 0.5) * math.tan(math.radians(opt('dihedral', 5.5)))
    regions = roundel(wx, wy, rr, ring) + roundel(-wx, wy, rr, ring)
    regions += side_roundel(opt('side_y', -2.1), opt('side_z', 0.1), opt('side_r', 0.38), 0.1)
    regions += [('port', False, lambda c, n: abs(n.x) > 0.5 and ((c.y - 1.35) / 0.55) ** 2 + ((c.z + 0.5) / 0.3) ** 2 < 1)]  # wheel wells
    if opt('lines', True):   # aileron and flap hinges, fuselage frames
        def hinge(c, n):
            x = abs(c.x)
            if x < 0.7 or x > span - 0.1 or abs(c.z - wz(x)) > 0.3:
                return False
            return abs(c.y - (le_y(x) - chord(x) * 0.76)) < 0.07
        regions += [('air_line', False, hinge),
                    ('air_line', False, lines((2.15, -1.5, -3.3), 0.06, 'y', lambda c, n: abs(c.x) < 0.85 and c.z > -0.1))]
        if opt('more_lines', True):   # the wing's spar and outer-panel joints
            def wline(c, n):
                x = abs(c.x)
                if x < 0.9 or x > span - 0.1 or abs(c.z - wz(x)) > 0.3 or n.z < 0.3:
                    return False
                return abs(c.y - (le_y(x) - chord(x) * 0.3)) < 0.05 or abs(x - 2.2) < 0.05
            regions.append(('air_line', False, wline))
    if opt('te_paint', True):   # the trailing edges stay top colour (the underside's grey showed as a bright edge line)
        regions.append(('air_paint', False, lambda c, n: 0.9 < abs(c.x) < span + 0.1 and c.y > -2.5 and c.y < le_y(min(abs(c.x), span)) - chord(min(abs(c.x), span)) * 0.85))
    if opt('belly', True):   # light grey below a straight demarcation along the fuselage sides
        def bz(c):   # behind the wing the line drops under the narrowing belly (it showed as a light sliver)
            return opt('belly_z', -0.18) - (opt('belly_drop', 0.3) if c.y < opt('belly_y', -0.6) else 0.0)
        regions.append(('air_under', False, lambda c, n: abs(c.x) < 0.95 and c.z < bz(c) and n.z < 0.3))
        regions.append(('air_paint', False, lambda c, n: abs(c.x) < 0.95 and n.z < -0.35 and c.z >= bz(c)))
    keys = paint_keys(regions)
    body(p, [(2.4, -0.02, 0.8, 0.78), (1.6, -0.1, 0.78, 0.9), (0.4, -0.12, 0.76, 0.9), (-0.3, -0.04, 0.72, 0.8),
             (-1.4, 0.06, 0.56, 0.64), (-2.6, 0.16, 0.4, 0.5), (-3.8, 0.24, 0.24, 0.35), (-4.9, 0.3, 0.1, 0.18),
             (-5.15, 0.32, 0.02, 0.04)], keys=keys, step=0.12)
    p.add(props.ellipsoid(0.3, 1.5, 0.3, seg=18), 'air_paint', T(0, -1.3, 0.48), smooth=True, keys=keys)   # the turtle deck
    canopy(p, (0, 0.05, 0.7), 0.38, 0.88, 0.42, frames=opt('frames', 4), frame_r=opt('frame_r', 0.04), spine=opt('spine', True),
           frame_key=opt('frame_key', 'frame_light'))
    # the cowling (20 % fatter than the fuselage's front): the dark cowl-flap band at its back, the civ lip ring at
    # its front, the dark engine face (the cylinders and the gearbox) inside the lip
    cr, cz = opt('cowl_r', 0.86), -0.04
    p.add(props.lathe([(0.0, cr * 0.93), (0.45, cr * 0.99), (0.95, cr), (1.25, cr * 0.95)], seg=32), 'air_paint',
          T(0, 2.3, cz) @ R('X', -90), smooth=True)
    p.add(props.lathe([(0.0, cr * 0.9), (0.2, cr * 0.95)], seg=32), 'air_dark', T(0, 2.12, cz) @ R('X', -90), smooth=True)   # cowl flaps
    ck = opt('cowl_key', 'tint')
    lip = [(cr * 0.88 * math.cos(2 * math.pi * k / 40), 3.55, cz + cr * 0.88 * math.sin(2 * math.pi * k / 40)) for k in range(41)]
    p.add(props.tube(lip, opt('lip_r', 0.085), seg=8, caps=False), ck, smooth=True, tint=ck == 'tint')
    p.add(props.lathe([(0.0, cr * 0.82), (0.04, cr * 0.82)], seg=28), 'port', T(0, 3.5, cz) @ R('X', -90))
    p.add(props.lathe([(0.0, cr * 0.6), (0.1, cr * 0.55)], seg=24), 'air_dark', T(0, 3.52, cz) @ R('X', -90), smooth=True)   # cylinders
    p.add(props.lathe([(0.0, 0.3), (0.14, 0.26)], seg=18), 'nozzle', T(0, 3.58, cz) @ R('X', -90), smooth=True)   # the gearbox
    for side in (-1, 1):   # exhausts below the cowl flaps
        p.add(props.box(0.12, 0.35, 0.1), 'nozzle', T(side * 0.6, 2.05, -0.6), bevel=0.02)
    prop(p, (0, 3.78, cz), 1.52, blades=3, tips=True, hub=0.2, hub_key='air_dark', blade_w=opt('blade_w', 0.3), spinner=1.0,
         pitch=opt('pitch', 45.0), thick=opt('blade_t', 0.12), tip_in=0.05)
    st = []
    xs = [0.5 + i * 0.12 for i in range(int((span - 0.5) / 0.12) + 1)] + [span - 0.02, span + 0.08]
    for x in xs:
        tip = x > span
        xx = min(x, span)
        c = chord(xx) * (0.86 if tip else 1.0)   # the squared tip with rounded corners
        st.append((x, le_y(xx) - (0.06 if tip else 0.0), c, wz(x), 0.07 if tip else _lerp(0.15, 0.1, x / span)))
    for side in (-1, 1):
        wing(p, st, keys=keys, side=side, seg=opt('wing_seg', 24))
        for dx in (2.45, 2.7, 2.95):   # the .50s in the leading edge
            vkit.rod(p, (side * dx, le_y(dx) - 0.4, wz(dx)), (side * dx, le_y(dx) + 0.3, wz(dx)), 0.045, 'gunmetal', seg=6)
        p.add(props.ellipsoid(0.24, 0.24, 0.24, seg=14), 'rubber', T(side * 0.72, 1.35, -0.5))   # the tyre in its well
    tst = [(0.1 + f * 1.95, -3.75 - f * 0.25, _lerp(1.3, 0.75, f * f), 0.22, 0.1) for f in [i / 8 for i in range(9)]]
    for side in (-1, 1):
        wing(p, tst, keys=keys, side=side)
    fst = [(0.3, -3.55, 1.65, 0.0, 0.1), (0.9, -3.85, 1.4, 0.0, 0.1), (1.45, -4.15, 1.05, 0.0, 0.1),
           (1.8, -4.4, 0.62, 0.0, 0.09), (1.95, -4.6, 0.3, 0.0, 0.07)]
    wing(p, fst, keys=keys, vertical=True, step=0.15)
    p.add(props.box(0.04, 0.05, 1.5), 'air_dark', T(0, -4.68, 1.0))   # the rudder's hinge line


AIRCRAFT = {
    'Fighter': dict(build=fighter, alt=6.4, paint=opt('paint', '#666a6e'), under='#a4a8a8', line=opt('line', '#2e322e'),
                    camo=[(opt('camo_hex', '#323c2a'), opt('camo_scale', 0.32), opt('camo_th', 0.5), 1.0, 0.01)]),
    'Bomber': dict(build=bomber, alt=14.5, paint=opt('paint', '#575a38'), under='#86888a', line=opt('line', '#46492e'),
                   panel=opt('panel', '#63663f'),
                   camo=[(opt('camo_hex', '#65673f'), opt('camo_scale', 0.35), 0.52, 2.0, 0.15)]),   # a soft sun fade, not a camo
    'Helicopter': dict(build=helicopter, alt=12.0, paint='#acb0b4', under='#6e7278',
                       camo=[('#cfd2d4', 0.5, 0.53, 3.0)]),
    'Jet Fighter': dict(build=jet_fighter, alt=10.0, paint=opt('paint', '#7c868c'), under='#a8b0b4', line=opt('line', '#4a5664'),
                        camo=[(opt('camo_hex', '#56687e'), opt('camo_scale', 0.32), 0.5, 4.0, 0.01)]),
    'F-15': dict(build=f15, alt=10.5, paint=opt('paint', '#8c949c'), under='#b0b6ba', line=opt('line', '#5c646c'),
                 panel=opt('panel', '#7e868e'), radome='#3a3e44',
                 camo=[(opt('camo_hex', '#646c76'), opt('camo_scale', 0.15), opt('camo_th', 0.44), 6.0, opt('camo_soft', 0.03))]),
    'Stealth Fighter': dict(build=stealth_fighter, alt=9.5, paint=opt('paint', '#33363b'), under='#1e2024',
                            line=opt('line', '#74787e'), camo=[]),
    'Stealth Bomber': dict(build=stealth_bomber, alt=21.0, paint=opt('paint', '#33363a'), under='#2a2c30', panel=opt('panel', '#3b3e43'),
                           line=opt('line', '#5c6066'), camo=[(opt('camo_hex', '#3c4045'), 0.12, 0.5, 3.0, 0.3)]),
}
AIRCRAFT.update({
    'ICBM': dict(build=icbm, alt=0.0, paint='#8e9298', under='#8e9298', camo=[('#b4b8bc', 0.6, 0.52, 2.0)]),
    'Tactical Nuke': dict(build=tactical_nuke, alt=0.0, paint='#9a9ea4', under='#9a9ea4', camo=[('#c4c8cc', 0.9, 0.52, 4.0)]),
    'Cruise Missile': dict(build=cruise_missile, alt=4.6, paint='#9a9ea2', under='#9a9ea2', camo=[]),
})
AIRCRAFT.update({   # the NumPy remakes (League)
    'Fighter Bomber': dict(build=p38, alt=opt('alt', 9.0), paint=opt('paint', '#484b2e'), under=opt('under', '#9a9c9c'),
                           line=opt('line', '#2c2e1e'), panel=opt('panel', '#3c3f27'), fit_scale=opt('fit_scale', 1.094),
                           camo=[(opt('camo_hex', '#65673f'), opt('camo_scale', 0.35), 0.52, 2.0, 0.15)]),
    'Wildcat': dict(build=wildcat, alt=opt('alt', 6.6), paint=opt('paint', '#36404c'), under=opt('under', '#a2a8ac'),
                    line=opt('line', '#262e36'), fit_scale=opt('fit_scale', 1.13),
                    camo=[(opt('camo_hex', '#434e5b'), opt('camo_scale', 0.35), 0.52, 2.0, 0.15)]),
})
UNITS = list(AIRCRAFT)


def build(unit, colors=None):
    cfg = AIRCRAFT[unit]
    c = {'air_paint': cfg['paint'], 'air_under': cfg['under']}
    for k, key in (('line', 'air_line'), ('panel', 'air_panel'), ('radome', 'radome')):
        if k in cfg:
            c[key] = cfg[k]
    c.update(colors or {})
    root = vkit.root(unit)
    props.CAMO.clear()
    props.CAMO['air_paint'] = [tuple(layer) for layer in cfg['camo']]
    p = vkit.part(root, 'airframe', c, cfg.get('wear', WEAR))
    cfg['build'](p)
    p.finish()
    props.CAMO.clear()
    root.location.z = cfg['alt']
    if 'fit_scale' in cfg:   # (opt-in: render_vehicles.fit's scale on the fitted size)
        root['fit_scale'] = cfg['fit_scale']
    return root

# the realism pass's Helicopter and missiles (realism/air-missiles: airmiss.py) replace the entries above
import airmiss  # noqa: E402
AIRCRAFT.update(airmiss.SPECS)
