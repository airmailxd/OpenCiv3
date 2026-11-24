"""Realism-pass builders for the Helicopter and the three missiles (ICBM,
Tactical Nuke, Cruise Missile), registered over aircraft.py's entries
(aircraft.AIRCRAFT.update(SPECS) at the end of aircraft.py). The fixed-wing
units and aircraft.py's helpers stay with the air builder; this module only
uses them.

Model space as aircraft.py: meters, facing +Y, z up; the root's z is the
flying altitude (0 for the standing missiles). The standing missiles have a
single DEFAULT direction (row 0), so their asymmetric details (raceway,
mast, erector) sit where row 0's camera sees them: row 0 turns the model
by +135 deg, the camera looks from -Y, so model angle THETA_FRONT faces the
camera, a little to the lit (left) side.

Ground fixtures (pads, masts, erectors) are a separate part marked no_fit:
the fit sizes the missile alone (as the shipped one), and root['fit_scale']
brings the whole footprint back near the shipped unit's.
"""
import math

from mathutils import Vector

import aircraft as A
import props
import vkit
from props import R, T

for _k, _v in {
    'am_white': ('#c6c4ba', 0.5, 0.15, 'metal'),      # missile skin: off-white, never paper white
    'am_s1': ('#8c8a84', 0.5, 0.15, 'metal'),          # the ICBM's stages, a tone apart each (1 darkest)
    'am_s2': ('#c4c2b8', 0.5, 0.15, 'metal'),
    'am_nose': ('#e2e0d8', 0.45, 0.1, 'metal'),        # the re-entry shroud, the lightest thing on the sheet
    'am_dark': ('#303236', 0.5, 0.4, 'metal'),         # interstage rings, raceways, nozzles
    'am_concrete': ('#7a766a', 0.9, 0.0, 'metal'),     # the launch pad
    'am_trench': ('#1e1e1c', 0.9, 0.0, 'metal'),       # the flame trench
    'am_steel': ('#5e6062', 0.5, 0.8, 'metal'),        # launch ring, clamps, mast
    'am_mast': ('#8a8478', 0.6, 0.3, 'metal'),         # the umbilical mast (lighter, so it reads against the grass)
    'am_mgrey': ('#b4b2a8', 0.5, 0.2, 'metal'),        # the cruise missile's mid body
    'am_mgrey_f': ('#c2c0b6', 0.5, 0.2, 'metal'),      # its forward section
    'am_mgrey_a': ('#8e8c84', 0.5, 0.2, 'metal'),      # its aft section
    'am_radome': ('#d8d6ce', 0.4, 0.1, 'metal'),       # its light rounded nose
    'am_wing': ('#a2a098', 0.5, 0.2, 'metal'),         # its wings (light)
    'am_blade': ('#2a2c2e', 0.55, 0.3, 'metal'),       # rotor blades
    'am_glass': ('#6e8290', 0.12, 0.5, 'polished'),    # cockpit glazing, light enough to read against olive
    'am_rv': ('#34373a', 0.5, 0.3, 'metal'),           # the Pershing's dark re-entry vehicle
    'am_od': ('#5c6042', 0.6, 0.2, 'metal'),           # olive drab
    'am_edge': ('#a0a282', 0.55, 0.2, 'metal'),        # light olive: the helicopter's top shoulder
    'am_wband': ('#8a8c6c', 0.55, 0.2, 'metal'),       # its window band
    'am_od_l': ('#7c8058', 0.6, 0.2, 'metal'),         # a lighter olive (the Tactical Nuke)
    'am_rim': ('#c0c09a', 0.5, 0.2, 'metal'),          # light rims and edge highlights
    'am_heat': ('#6e5638', 0.4, 0.9, 'metal'),         # heat tint at a nozzle
    'am_truss': ('#3c3e40', 0.55, 0.7, 'metal'),       # dark lattice steel
}.items():
    props.DEFAULTS.setdefault(_k, _v)

AM_WEAR = {'am_white': {'grime': 0.25}, 'am_s1': {'grime': 0.3}, 'am_s2': {'grime': 0.25},
           'am_concrete': {'grime': 0.6}, 'am_mgrey': {'grime': 0.2}, 'am_mgrey_a': {'grime': 0.3, 'soot': 0.2},
           'am_blade': {'grime': 0.2}, 'am_steel': {'grime': 0.3}, 'am_mast': {'grime': 0.4}, 'am_od': {'grime': 0.3}}

THETA_FRONT = -225.0 + 15.0   # model angle (deg) that row 0's camera sees, nudged toward the key light


def _xy(theta_deg, r):
    a = math.radians(theta_deg)
    return r * math.cos(a), r * math.sin(a)


def _setup(p):
    p.weather.update(AM_WEAR)


def _cyl(p, z0, z1, r, key, seg=40, keys=None, step=0.2, r1=None, tint=False):
    """A (slightly tapering, r -> r1) cylinder along Z, finely ringed."""
    r1 = r if r1 is None else r1
    n = max(2, int((z1 - z0) / step))
    p.add(props.lathe([(z0 + (z1 - z0) * i / n, r + (r1 - r) * i / n) for i in range(n + 1)], seg=seg), key,
          smooth=True, keys=keys, tint=tint)


def _band(p, z0, z1, r, key='am_dark', tint=False, seg=40):
    p.add(props.lathe([(z0, r), (z1, r)], seg=seg), key, smooth=True, tint=tint)


def _ring(p, z, rr, tube_r, key, seg=32):
    """A horizontal ring (torus-like tube) of radius rr at height z."""
    pts = [(rr * math.cos(2 * math.pi * k / seg), rr * math.sin(2 * math.pi * k / seg), z) for k in range(seg + 1)]
    p.add(props.tube(pts, tube_r, seg=8), key, smooth=True)


def _fixture(p, name='pad'):
    return vkit.part(p.root, name, p.colors, p.weather)


def _done_fixture(q):
    obj = q.finish()
    obj['no_fit'] = True
    return obj


# ---------------------------------------------------------------- helicopter

BLADE_R = 8.2   # true 9.1: shorter so the fit gives the fuselage more of the box


def helicopter(p):
    """A CH-47D Chinook in olive drab with a high-contrast woodland camo
    (big dark-green and brown blotches that survive 1x) and a darker belly:
    the long boxy fuselage with light framed cockpit glazing and chin
    windows, the front pylon and the tall aft pylon with the two engines on
    its flanks, the long fuel pods, the rear ramp with its hinge and edges,
    the gear, two big three-blade rotors with broad, thick dark blades.
    Civ colour: panels on both pylons' flanks, the pylon tops, a band round
    the fuselage behind the cockpit, the hub caps."""
    _setup(p)
    p.root['fit_scale'] = 0.92   # shorter blades let the fit draw the body bigger: keep the shipped footprint

    def glass(c, n):
        return (c.y > 5.9 and c.z > 0.02 and n.y > -0.3) or (c.y > 6.55 and -0.9 < c.z < -0.08 and n.z < 0.45)

    band_y = (3.7, 5.0)
    regions = [('am_glass', False, glass),
               ('tint', True, lambda c, n: band_y[0] < c.y < band_y[1] and n.z > -0.35),
               # light value breaks that survive 1x: the rounded top shoulder and the window band
               ('am_edge', False, lambda c, n: 0.3 < n.z < 0.85 and c.z > 0.55),
               ('am_wband', False, lambda c, n: abs(n.x) > 0.75 and 0.12 < c.z < 0.8 and -4.7 < c.y < 5.9)]
    keys = A.paint_keys(regions)
    A.body(p, [(7.95, 0.25, 0.25, 0.25, 2.6), (7.5, 0.15, 0.85, 0.9, 2.8), (6.4, 0.1, 1.2, 1.2, 3.5),
               (5.0, 0.1, 1.27, 1.27, 4.0), (-4.0, 0.1, 1.27, 1.27, 4.0), (-5.6, 0.35, 1.22, 1.05, 3.6),
               (-7.6, 0.9, 0.8, 0.45, 3.0), (-8.0, 1.0, 0.1, 0.1, 2.5)], keys=keys, step=0.2, seg=32)
    # the front pylon hump (taller) and the tall aft pylon; tops civ
    A.body(p, [(6.9, 1.25, 0.05, 0.05), (6.5, 1.55, 0.6, 0.55, 3.0), (4.4, 1.6, 0.62, 0.55, 3.0), (3.4, 1.3, 0.1, 0.1)],
           keys=A.paint_keys([('tint', True, lambda c, n: n.z > 0.5 and c.z > 1.6)]), step=0.15, seg=24)
    pylon = A.paint_keys([('tint', True, lambda c, n: c.z > 1.6 and n.z > 0.5 and -8.3 < c.y < -4.3)])
    # the aft pylon raised (the Chinook's stepped back: its hub clearly above the front one)
    A.body(p, [(-3.4, 1.35, 0.1, 0.1), (-4.6, 2.05, 0.64, 0.75, 3.0), (-6.4, 2.8, 0.68, 1.5, 3.2), (-7.7, 3.15, 0.46, 1.3, 3.0),
               (-8.05, 3.25, 0.05, 0.5)], keys=pylon, step=0.15, seg=24)
    p.add(props.box(0.9, 8.6, 0.32), 'tint', T(0, 0.0, 1.36), bevel=0.12, tint=True)   # the tunnel on the spine: civ, seen from every side
    for side in (-1, 1):
        # civ panels on the pylons' flanks, proud of the skin
        p.add(props.box(0.06, 1.7, 0.42), 'tint', T(side * 0.6, 5.45, 1.62), bevel=0.02, tint=True)
        p.add(props.box(0.06, 2.0, 0.95), 'tint', T(side * 0.62, -6.6, 3.55) @ R('X', -12), bevel=0.02, tint=True)
        # the engines on the aft pylon's flanks: dark intake rings, dark exhausts
        ex, ey, ez = side * 1.12, -4.2, 2.25
        p.add(props.lathe([(0, 0.5), (0.4, 0.52), (2.6, 0.5), (3.2, 0.34)], seg=18), 'air_paint',
              T(ex, ey, ez) @ R('X', 90), smooth=True)
        p.add(props.lathe([(-0.02, 0.53), (0.12, 0.53)], seg=18), 'am_dark', T(ex, ey, ez) @ R('X', 90), smooth=True)
        p.add(props.lathe([(0, 0.36), (0.03, 0.36)], seg=16), 'port', T(ex, ey + 0.02, ez) @ R('X', -90), smooth=True)
        p.add(props.lathe([(3.1, 0.36), (3.7, 0.3)], seg=16), 'nozzle', T(ex, ey, ez) @ R('X', 90), smooth=True)
        p.add(props.box(0.3, 2.2, 0.4), 'air_paint', T(side * 0.75, ey - 1.4, ez - 0.1), bevel=0.08)   # the engine pylon
        # the long fuel pods along the lower sides: fuller, with dark nose/tail caps and a seam
        sx = side * 1.42
        p.add(props.lathe([(0, 0.0), (0.5, 0.5), (7.0, 0.5), (7.8, 0.0)], seg=20, scale_xy=(1.0, 0.8)), 'air_paint',
              T(sx, -3.6, -0.7) @ R('X', -90), smooth=True)
        p.add(props.lathe([(7.55, 0.26), (7.85, 0.0)], seg=14), 'am_dark', T(sx, -3.6, -0.7) @ R('X', -90), smooth=True)
        p.add(props.lathe([(2.5, 0.51), (2.58, 0.51)], seg=20, scale_xy=(1.0, 0.8)), 'am_dark', T(sx, -3.6, -0.7) @ R('X', -90), smooth=True)
        for y in [-4.0 + 1.1 * k for k in range(9)]:   # the round windows, framed light
            if band_y[0] - 0.3 < y < band_y[1] + 0.3:
                continue
            p.add(props.lathe([(0, 0.24), (0.03, 0.24)], seg=14), 'am_mgrey', T(side * 1.28, y, 0.45) @ R('Y', side * 90), smooth=True)
            p.add(props.lathe([(0.03, 0.17), (0.05, 0.17)], seg=14), 'port', T(side * 1.28, y, 0.45) @ R('Y', side * 90), smooth=True)
        p.add(props.box(0.04, 1.0, 1.5), 'am_dark', T(side * 1.285, 5.6, 0.05))   # the crew door outline (dark)
        for y, z in ((3.7, -1.15), (-3.3, -1.15)):   # landing gear: strut and tyre
            vkit.rod(p, (side * 1.0, y, -0.95), (side * 1.18, y, z), 0.11, 'am_steel', seg=8)
            p.add(props.lathe([(-0.17, 0.34), (0.17, 0.34)], seg=16), 'rubber', T(side * 1.18, y, z - 0.18) @ R('Y', 90), smooth=True)
        # the ramp's side edges, along the aft belly's lower corners
        vkit.rod(p, (side * 0.98, -5.6, -0.6), (side * 0.6, -7.6, 0.5), 0.08, 'am_dark', seg=6)
    # the ramp hinge across the belly where the aft fuselage rises
    vkit.rod(p, (-1.0, -5.6, -0.6), (1.0, -5.6, -0.6), 0.1, 'am_dark', seg=8)
    p.add(props.box(1.6, 1.9, 0.06), 'air_under', T(0, -6.6, -0.16) @ R('X', -30), bevel=0.02)   # the ramp plate
    # the rotors: masts, hubs (civ caps), broad blades with a slight droop
    for at, spin in (((0, 5.4, 2.08), 25.0), ((0, -6.9, 4.5), 85.0)):
        x, y, z = at
        vkit.rod(p, (x, y, z - 0.55), (x, y, z + 0.1), 0.22, 'am_dark', seg=12)
        p.add(props.lathe([(0, 0.6), (0.22, 0.58), (0.4, 0.38), (0.52, 0.0)], seg=18), 'tint', T(x, y, z + 0.1), smooth=True, tint=True)
        for k in range(3):
            a = math.radians(spin + 120 * k)
            d = Vector((math.cos(a), math.sin(a), 0.0))
            root = Vector(at) + Vector((0, 0, 0.18))
            tip = Vector(at) + d * BLADE_R + Vector((0, 0, -0.3))
            m, length = vkit.align(root, tip)
            p.add(props.box(0.78, 0.2, length), 'am_blade', m @ T(0, 0, length / 2), bevel=0.02)


# ---------------------------------------------------------------- cruise missile

def cruise_missile(p):
    """A BGM-109 Tomahawk in flight, heading read by value: the light
    rounded radome in front, a lighter forward section, the mid body, a
    darker aft band between the wings and the tail, the dark cross of tail
    fins and the nozzle (with a faint heat tint) as the only dark end; a
    grey underside. Short grey pop-out wings (so the diagonals don't read
    as a small aircraft), the dark belly intake, panel seams. Two wide civ
    bands, fore and aft of the wings."""
    _setup(p)
    p.root['fit_scale'] = 0.98
    r = 0.28
    regions = [('tint', True, A.band(1.0, 1.9)), ('tint', True, A.band(-1.95, -1.25)),
               ('air_under', False, lambda c, n: n.z < -0.45),
               ('am_mgrey_f', False, lambda c, n: c.y > 0.05), ('am_mgrey_a', False, lambda c, n: c.y < -0.95)]
    keys = A.paint_keys(regions, under=False)
    prof = [(-2.95, r * 0.84)] + [(-2.8 + 0.1 * i, r) for i in range(53)] + [(2.5, r)]
    p.add(props.lathe(prof, seg=28), 'am_mgrey', R('X', -90), smooth=True, keys=keys)
    nose = [(2.5, r), (2.7, r * 0.98), (2.9, r * 0.9), (3.05, r * 0.76), (3.17, r * 0.55), (3.25, r * 0.28), (3.28, 0.0)]
    p.add(props.lathe(nose, seg=28), 'am_radome', R('X', -90), smooth=True)
    p.add(props.lathe([(2.47, r * 1.015), (2.53, r * 1.015)], seg=28), 'am_dark', R('X', -90), smooth=True)
    for y in (0.05, -0.95, -2.35):   # panel seams
        p.add(props.lathe([(y - 0.025, r * 1.01), (y + 0.025, r * 1.01)], seg=28), 'am_dark', R('X', -90), smooth=True)
    p.add(props.lathe([(-3.3, r * 0.62), (-3.1, r * 0.72), (-2.95, r * 0.84)], seg=20), 'nozzle', R('X', -90), smooth=True)
    p.add(props.lathe([(-3.33, r * 0.6), (-3.29, r * 0.64)], seg=20), 'am_heat', R('X', -90), smooth=True)   # the heat-tinted lip
    p.add(props.lathe([(-3.34, r * 0.4), (-3.33, r * 0.4)], seg=16), 'port', R('X', -90), smooth=True)
    for side in (-1, 1):   # the wings: shorter and grey
        A.wing(p, [(0.2, 0.55, 0.58, -0.06, 0.14), (1.45, 0.28, 0.4, -0.06, 0.12)], 'am_wing', side=side)
    for a in (45, 135, 225, 315):   # the tail fins: swept, thick, dark
        ring0 = [(-0.04, -2.2, r * 0.7), (0.04, -2.2, r * 0.7), (0.04, -2.95, r * 0.7), (-0.04, -2.95, r * 0.7)]
        ring1 = [(-0.035, -2.6, r + 0.46), (0.035, -2.6, r + 0.46), (0.035, -3.0, r + 0.46), (-0.035, -3.0, r + 0.46)]
        p.add(props.loft([ring0, ring1]), 'am_dark', R('Y', a), bevel=0.01)
    # the belly intake: a dark scoop, a black mouth
    p.add(props.box(0.3, 0.95, 0.2), 'am_dark', T(0, -0.95, -r - 0.06), bevel=0.05)
    p.add(props.box(0.24, 0.05, 0.13), 'port', T(0, -0.47, -r - 0.07))


# ---------------------------------------------------------------- ICBM

def _truss(q, base, top, w, key, member=0.22, bay=1.3, ang=0.0):
    """An open square lattice tower from z=base to z=top, w wide (centres of
    the corner posts), turned ang deg about Z at (0, 0); returns the posts'
    corner points. Corner posts, a horizontal frame each bay and one
    diagonal per face per bay; members thick enough to stay solid at 1x."""
    h = w / 2
    corners = [(-h, -h), (h, -h), (h, h), (-h, h)]
    rot = lambda x, y: (x * math.cos(math.radians(ang)) - y * math.sin(math.radians(ang)),
                        x * math.sin(math.radians(ang)) + y * math.cos(math.radians(ang)))
    C = [rot(x, y) for x, y in corners]
    for x, y in C:
        vkit.beam(q, (x, y, base), (x, y, top), member, member, key, bevel=0.01)
    n = max(1, int((top - base) / bay))
    for k in range(n + 1):
        z = base + (top - base) * k / n
        for i in range(4):
            (x0, y0), (x1, y1) = C[i], C[(i + 1) % 4]
            vkit.beam(q, (x0, y0, z), (x1, y1, z), member * 0.8, member * 0.8, key, bevel=0.01)
        if k < n:
            z1 = base + (top - base) * (k + 1) / n
            for i in range(4):
                (x0, y0), (x1, y1) = C[i], C[(i + 1) % 4]
                if k % 2:
                    (x0, y0), (x1, y1) = (x1, y1), (x0, y0)
                vkit.beam(q, (x0, y0, z), (x1, y1, z1), member * 0.7, member * 0.7, key, bevel=0.01)
    return C


def icbm(p):
    """A three-stage silo ICBM (Minuteman III / Peacekeeper) on a launch
    stand wider than the missile: a concrete pad with a dark flame trench,
    a steel launch ring on four legs with four hold-down clamps, and a dark
    open lattice umbilical tower on a broad base with two swing arms to the
    missile. The stages step in with skirts, a distinct grey each (the
    first clearly darker), dark interstage rings with lit lips; four
    nozzles below the aft skirt; a raceway conduit; the bright re-entry
    shroud with a dark base ring and dark tip. Civ colour: the aft skirt,
    a band at the top of stage 1, the whole third stage."""
    _setup(p)
    r = 0.95
    p.root['fit_scale'] = 0.8   # the stand and tower add area
    # --- the launch stand (out of the fit)
    q = _fixture(p)
    hp, rp = 0.4, r * 2.6
    q.add(props.lathe([(0.0, rp), (hp * 0.7, rp), (hp, rp * 0.94)], seg=8), 'am_concrete', R('Z', 22.5), bevel=0.02)
    q.add(props.lathe([(hp, rp * 0.94), (hp + 0.01, 0.0)], seg=8), 'am_concrete', R('Z', 22.5))
    q.add(props.box(r * 0.9, rp * 1.7, 0.04), 'am_trench', T(0, 0, hp + 0.01) @ R('Z', THETA_FRONT + 90))   # the flame trench
    zr = hp + 0.75
    _ring(q, zr, r * 1.22, 0.16, 'am_steel')
    for k in range(4):
        a = 45.0 + 90.0 * k
        x0, y0 = _xy(a, r * 1.45)
        x1, y1 = _xy(a, r * 1.22)
        vkit.beam(q, (x0, y0, hp), (x1, y1, zr), 0.26, 0.26, 'am_steel', bevel=0.02)       # the ring's legs
        x2, y2 = _xy(a, r * 1.02)
        vkit.beam(q, (x1, y1, zr), (x2, y2, zr + 0.35), 0.3, 0.24, 'am_steel', bevel=0.02)  # hold-down clamps
    # the umbilical tower: an open dark lattice on a broad footing, to the side the camera sees in profile
    ma = THETA_FRONT + 100
    mx, my = _xy(ma, r + 1.25)
    H, W = 11.4, 1.1
    q.add(props.box(W + 0.7, W + 0.7, 0.25), 'am_concrete', T(mx, my, hp + 0.12) @ R('Z', ma), bevel=0.03)   # its footing
    tq = vkit.part(p.root, 'tower', p.colors, p.weather)
    _truss(tq, hp + 0.25, H, W, 'am_truss', member=0.24, bay=1.35, ang=ma)
    tq.add(props.box(W + 0.5, W + 0.5, 0.25), 'am_truss', T(0, 0, H + 0.1) @ R('Z', ma), bevel=0.02)   # its head platform
    tobj = tq.finish()
    tobj.location = (mx, my, 0.0)
    tobj['no_fit'] = True
    # the two swing arms: lattice-like (a lit top chord, a dark lower chord, struts between) out to the missile
    for zz, rr in ((6.4, r * 0.92), (10.4, r * 0.8)):
        ax, ay = _xy(ma, rr * 0.97)
        bx, by = _xy(ma, r + 1.25 - W / 2)
        vkit.beam(q, (bx, by, zz), (ax, ay, zz), 0.26, 0.26, 'am_rim', bevel=0.02)
        vkit.beam(q, (bx, by, zz - 0.55), (ax, ay, zz - 0.2), 0.22, 0.22, 'am_truss', bevel=0.02)
        mx2, my2 = (ax + bx) / 2, (ay + by) / 2
        vkit.beam(q, (mx2, my2, zz), (mx2, my2, zz - 0.4), 0.18, 0.18, 'am_truss', bevel=0.01)
    _done_fixture(q)
    # --- the missile
    zb = zr - 0.05
    for k in range(4):   # nozzles below the aft skirt
        x, y = _xy(45 + 90 * k, r * 0.42)
        p.add(props.lathe([(zb - 0.55, r * 0.32), (zb - 0.1, r * 0.22), (zb + 0.1, r * 0.2)], seg=14), 'nozzle', T(x, y, 0), smooth=True)
    s1 = (zb, zb + 7.0)
    p.add(props.lathe([(s1[0], r * 1.07), (s1[0] + 0.9, r * 1.0)], seg=40), 'tint', smooth=True, tint=True)   # civ aft skirt
    _cyl(p, s1[0] + 0.9, s1[1], r, 'am_s1', r1=r * 0.98)
    _band(p, s1[1] - 1.7, s1[1] - 0.05, r * 0.985, 'tint', tint=True)                 # civ band, top of stage 1

    def interstage(z0, z1, ra, rb):
        """A dark ring with depth: a lit lip on top, the dark band, a lit lip below."""
        p.add(props.lathe([(z0, ra * 1.03), (z0 + 0.06, ra * 1.03)], seg=40), 'am_rim', smooth=True)
        p.add(props.lathe([(z0 + 0.06, ra * 1.02), (z1 - 0.06, rb * 1.02)], seg=40), 'am_dark', smooth=True)
        p.add(props.lathe([(z1 - 0.06, rb * 1.03), (z1, rb * 1.03)], seg=40), 'am_rim', smooth=True)

    interstage(s1[1] - 0.05, s1[1] + 0.4, r * 0.98, r * 0.95)
    s2 = (s1[1] + 0.4, s1[1] + 4.4)
    p.add(props.lathe([(s2[0], r * 0.97), (s2[0] + 0.35, r * 0.93)], seg=40), 'am_s2', smooth=True)   # skirt step
    _cyl(p, s2[0] + 0.35, s2[1], r * 0.93, 'am_s2', r1=r * 0.9)
    interstage(s2[1], s2[1] + 0.55, r * 0.9, r * 0.8)                                  # darker band under the civ stage
    s3 = (s2[1] + 0.55, s2[1] + 2.25)
    _cyl(p, s3[0], s3[1], r * 0.8, 'tint', r1=r * 0.78, tint=True)                     # civ third stage
    _band(p, s3[1], s3[1] + 0.25, r * 0.8)                                              # the shroud's dark base ring
    zc = s3[1] + 0.25
    prof = [(zc, r * 0.8)] + [(zc + 2.9 * t, r * 0.8 * math.cos(t * math.pi / 2) ** 0.7) for t in (0.15, 0.35, 0.55, 0.75, 0.9)]
    p.add(props.lathe(prof, seg=36), 'am_nose', smooth=True)
    p.add(props.lathe([prof[-1], (zc + 3.05, 0.0)], seg=20), 'am_dark', smooth=True)
    for zz in (s1[0] + 2.7, s1[0] + 4.5):                                               # panel seams
        _band(p, zz - 0.04, zz + 0.04, r * 0.995)
    # the raceway conduit, per stage on the camera's side (touching each stage's skin)
    ra = THETA_FRONT + 35
    for z0, z1, rr in ((s1[0] + 0.95, s1[1] - 0.1, r * 0.99), (s2[0] + 0.4, s2[1] - 0.05, r * 0.915)):
        x, y = _xy(ra, rr + 0.06)
        vkit.rod(p, (x, y, z0), (x, y, z1), 0.13, 'am_dark', seg=8)


# ---------------------------------------------------------------- Tactical Nuke

def _fin(p, a, r, z0, root_c, tip_c, span, th, key, tint=False):
    """A cropped-delta fin on a vertical body at angle a (deg): root chord
    root_c from z0 up, the tip chord tip_c `span` out, trailing edge level."""
    rings = []
    for x, c in ((r * 0.9, root_c), (r + span, tip_c)):
        t = th / 2
        rings.append([(x, -t, z0), (x, t, z0), (x, t, z0 + c), (x, -t, z0 + c)])
    p.add(props.loft(rings), key, R('Z', a), bevel=0.01, tint=tint)


NUKE_ELEV = 62.0   # the erector's angle above horizontal


def tactical_nuke(p):
    """A Pershing on its M790-style erector-launcher trailer, the missile
    raised at an angle (the Pershing silhouette): a long flat bed on two
    axles of dual wheels, the gooseneck at the front with its landing legs,
    stabiliser jacks at the rear, the erector boom hinged at the rear with
    clamp rings round the missile and a hydraulic ram up from the bed. The
    missile: thick, a lighter olive with light stage rims, four big civ
    cropped-delta fins with light root strips, a wide civ band under the
    long dark re-entry vehicle with its vanes. All of it in the fit (the
    raised missile needs the height; the area grows, as the lead accepted).
    """
    _setup(p)
    r = 0.62
    p.root['fit_scale'] = 1.6   # tuned so the raised missile stands at least as tall as the shipped needle
    ang = THETA_FRONT - 90.0   # the trailer's axis: across the camera's view, so its profile shows
    M = R('Z', ang)

    def L(u, v, z):   # a point in the trailer frame (u forward, v across, z up)
        return tuple(M @ Vector((u, v, z)))

    # --- the trailer
    bed_z0, bed_z1 = 0.95, 1.4
    p.add(props.box(7.0, 2.3, bed_z1 - bed_z0), 'am_od_l', M @ T(-0.6, 0, (bed_z0 + bed_z1) / 2), bevel=0.05)
    for v in (-1.16, 1.16):   # the bed's lit edges
        p.add(props.box(7.0, 0.08, 0.1), 'am_rim', M @ T(-0.6, v, bed_z1 - 0.04), bevel=0.02)
    p.add(props.box(1.6, 2.0, 0.6), 'am_od_l', M @ T(3.4, 0, bed_z1 + 0.25), bevel=0.06)      # the gooseneck
    p.add(props.box(0.06, 1.2, 0.4), 'tint', M @ T(4.2, 0, bed_z1 + 0.25), tint=True)          # civ plate on its front
    for v in (-1.01, 1.01):
        p.add(props.box(1.2, 0.06, 0.4), 'tint', M @ T(3.4, v, bed_z1 + 0.25), tint=True)      # civ plates on its sides
    for v in (-0.8, 0.8):   # landing legs and pads
        vkit.beam(p, L(3.0, v, bed_z0), L(3.0, v, 0.1), 0.2, 0.2, 'am_dark', bevel=0.02)
        p.add(props.box(0.45, 0.45, 0.1), 'am_dark', M @ T(3.0, v, 0.05))
    for v in (-1.25, 1.25):   # the rear stabiliser jacks
        vkit.beam(p, L(-3.8, v * 0.85, bed_z0 + 0.1), L(-3.9, v * 1.15, 0.1), 0.22, 0.22, 'am_dark', bevel=0.02)
        p.add(props.box(0.5, 0.5, 0.1), 'am_dark', M @ T(-3.9, v * 1.15, 0.05))
    for u in (-1.5, -2.7):   # two axles of dual wheels
        vkit.rod(p, L(u, -1.4, 0.5), L(u, 1.4, 0.5), 0.1, 'am_dark', seg=8)
        for v in (-1.0, 1.0):
            p.add(props.lathe([(-0.22, 0.5), (0.22, 0.5)], seg=20), 'rubber', M @ T(u, v * 1.18, 0.5) @ R('X', 90), smooth=True)
            p.add(props.lathe([(0.22, 0.24), (0.25, 0.2)], seg=14), 'am_rim', M @ T(u, v * 1.18, 0.5) @ R('X', -90 * v), smooth=True)
        p.add(props.box(0.9, 2.5, 0.08), 'am_od_l', M @ T(u, 0, 1.02), bevel=0.02)            # the fenders
    # --- the missile on its erector, hinged at the rear
    pivot = Vector((-3.4, 0, bed_z1 + 0.35))
    tilt = 90.0 - NUKE_ELEV
    Mm = M @ T(*pivot) @ R('Y', tilt)    # R_y(+t) tips +Z toward +u: the nose rises over the bed toward the gooseneck
    q = vkit.part(p.root, 'missile', p.colors, p.weather)
    # the erector boom under the missile (+X in the missile frame points down-forward when tilted)
    q.add(props.box(0.32, 0.5, 6.8), 'am_od_l', T(r + 0.25, 0, 3.6), bevel=0.04)
    q.add(props.box(0.4, 0.6, 0.5), 'am_dark', T(r * 0.2 + 0.1, 0, 0.05), bevel=0.04)         # the hinge block
    for zz in (2.2, 5.6):   # clamp rings round the missile, tied to the boom
        _ring(q, zz, r * 1.04, 0.08, 'am_dark')
        q.add(props.box(0.3, 0.3, 0.16), 'am_dark', T(r + 0.12, 0, zz), bevel=0.02)
    z = 0.25
    q.add(props.lathe([(z - 0.35, r * 0.5), (z, r * 0.34)], seg=14), 'nozzle', smooth=True)
    s1 = (z, z + 4.0)
    _cyl(q, s1[0], s1[1], r, 'am_od_l', seg=32)
    for k in range(4):
        _fin(q, 90.0 * k + 45.0, r, s1[0] + 0.02, 1.9, 0.75, 0.95, 0.14, 'tint', tint=True)
        x, y = _xy(90.0 * k + 45.0, r * 1.01)   # a light strip along each fin's root
        q.add(props.box(0.16, 0.16, 1.9), 'am_rim', T(x, y, s1[0] + 0.97) @ R('Z', 90.0 * k + 45.0))
    _band(q, s1[1], s1[1] + 0.08, r * 1.04, 'am_rim', seg=32)
    _band(q, s1[1] + 0.08, s1[1] + 0.3, r * 1.03, seg=32)
    s2 = (s1[1] + 0.3, s1[1] + 2.6)
    _cyl(q, s2[0], s2[1], r * 0.98, 'am_od_l', seg=32)
    _band(q, s2[1], s2[1] + 0.07, r * 1.0, 'am_rim', seg=32)
    _band(q, s2[1] + 0.07, s2[1] + 1.27, r * 0.99, 'tint', tint=True, seg=32)            # civ band under the RV
    zr = s2[1] + 1.27
    prof = [(zr, r * 0.97), (zr + 0.4, r * 0.9), (zr + 1.2, r * 0.62), (zr + 1.9, r * 0.3), (zr + 2.2, 0.0)]
    q.add(props.lathe(prof, seg=32), 'am_rv', smooth=True)
    for k in range(4):   # the RV's vanes
        _fin(q, 90.0 * k, r * 0.9, zr + 0.08, 0.5, 0.28, 0.26, 0.08, 'am_rv')
    qobj = q.finish()
    qobj.matrix_basis = Mm
    # the hydraulic ram from the bed up to the boom's middle
    boom_mid = Mm @ Vector((r + 0.25, 0, 3.0))
    a = Vector(L(-1.2, 0, bed_z1))
    vkit.rod(p, tuple(a), tuple(boom_mid), 0.17, 'am_steel', seg=10)
    vkit.rod(p, tuple(a), tuple(a + (boom_mid - a) * 0.5), 0.24, 'am_dark', seg=10)   # the ram's cylinder


SPECS = {
    'Helicopter': dict(build=helicopter, alt=12.0, paint='#6c7050', under='#6a6c58',
                       camo=[('#3a4230', 0.3, 0.5, 3.0), ('#5e4a34', 0.34, 0.6, 9.0)]),
    'ICBM': dict(build=icbm, alt=0.0, paint='#d0cec4', under='#d0cec4', camo=[]),
    'Tactical Nuke': dict(build=tactical_nuke, alt=0.0, paint='#5c6042', under='#5c6042', camo=[]),
    'Cruise Missile': dict(build=cruise_missile, alt=4.6, paint='#b4b2a8', under='#8a8880', camo=[]),
}
