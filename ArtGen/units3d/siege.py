"""Siege engines for the Blender remaster: Catapult and Cannon.

    root = siege.build('Catapult', colors)

build(unit, colors, **params) returns a root empty holding the engine's mesh
parts and its crew empties ('crew_<n>', where the Mac session's human bodies
stand). Model space: meters, the engine faces +Y, ground at z = 0. The pose is
the still the map shows (DEFAULT/FORTIFY).

Proportions follow the old software models (ArtGen/units/models/siege.py and
guns.py); render_vehicles.py fits the size and position to the original's
outline. Tint goes only where Civ3's originals put the civ color: the
catapult's crossbar and front bar, the cannon's wheel spokes.
"""
import math

import bpy
from mathutils import Matrix, Vector

import props
import vkit
from props import R, T

# ------------------------------------------------------------ catapult

# Darker, battle-scarred timbers (stzur: imposing, not toy-like): oiled
# brown wood with soot and scorch marks, blackened rusty iron, grimy rope.
CATAPULT_COLORS = {'wood': '#b08e5a', 'wood_dark': '#6a4c2e', 'side_rail': '#9a7a4c', 'rope': '#a08a60',
                   'coil': '#ddd2b4', 'iron': '#4a4846', 'stone': '#9a8a74', 'wheel': '#5e4430', 'felloe': '#4e3824'}
CATAPULT_WEAR = {'wood': {'grime': 0.55, 'soot': 0.55}, 'side_rail': {'grime': 0.6, 'soot': 0.5},
                 'wood_dark': {'grime': 0.6, 'soot': 0.4}, 'wheel': {'grime': 0.6, 'soot': 0.3},
                 'felloe': {'grime': 0.7}, 'iron': {'grime': 0.5, 'rust': 0.35}, 'rope': {'grime': 0.65},
                 'stone': {'grime': 0.5, 'soot': 0.3}}

ARM_SHOW = 62.0    # the arm's cup end raised toward the rear, cup and stone high, as in Civ3
TH = 1.35          # timbers are heavy: every frame member this much thicker than the old model's


def catapult(colors, k=0.82, arm=ARM_SHOW, panels=True, rails_tint=True, cup_rim='steel', arm_len=2.1, stone=1.0,
             crew=None, cup=1.0, cup_level=False):
    """A wheeled torsion catapult (mangonel): two side rails on four spoked
    wheels, an A-frame carrying the padded crossbar, a twisted-rope torsion
    bundle with the throwing arm and its iron cup, a winch at the rear."""
    c = dict(CATAPULT_COLORS, **colors)
    root = vkit.root('Catapult')
    L, W, zr = 3.0, 1.15, 0.5
    f = vkit.part(root, 'frame', c, CATAPULT_WEAR)
    for sx in (-1, 1):
        x = sx * W / 2
        vkit.beam(f, (x, -L / 2, zr), (x, L / 2, zr), 0.17 * TH, 0.2 * TH, 'side_rail', bevel=0.02)
        # the upper side rails carry the civ color, as in the original
        vkit.beam(f, (x, -L / 2 + 0.15, zr + 0.19), (x, L / 2 - 0.25, zr + 0.19), 0.13 * TH, 0.13 * TH,
                  'beam' if rails_tint else 'side_rail', tint=rails_tint)
        # the A-frame holding the crossbar
        vkit.beam(f, (x, 0.75, zr + 0.1), (x, 0.38, 1.55), 0.13 * TH, 0.13 * TH, 'wood')
        vkit.beam(f, (x, -0.25, zr + 0.1), (x, 0.32, 1.52), 0.11 * TH, 0.11 * TH, 'wood')
        vkit.beam(f, (x, 1.3, zr + 0.1), (x, 0.42, 1.5), 0.1 * TH, 0.1 * TH, 'wood')
        # iron straps at the joints
        for y, z in ((0.75, zr + 0.12), (-0.25, zr + 0.12), (1.3, zr + 0.12), (0.38, 1.5)):
            f.add(props.box(0.15, 0.06, 0.16), 'iron', T(x, y, z), bevel=0.01)
        # iron corner plates wrapping the rail ends, and bands along the rails, each riveted
        for y in (-L / 2 + 0.1, L / 2 - 0.1, -0.85, 0.15, 1.0):
            f.add(props.box(0.27, 0.12 if abs(y) > 1.3 else 0.06, 0.3), 'iron', T(x, y, zr + 0.05), bevel=0.008)
            for dz in (-0.07, 0.07):
                f.add(props.ellipsoid(0.016, 0.016, 0.016, seg=6), 'iron', T(x + sx * 0.14, y, zr + 0.05 + dz),
                      smooth=True)
    for y in (-1.35, -0.45, 0.7):
        vkit.beam(f, (-W / 2, y, zr), (W / 2, y, zr), 0.14 * TH, 0.14 * TH, 'wood')
    # the front bar and the crossbar are civ-colored in the original
    vkit.beam(f, (-W / 2 - 0.05, 1.4, zr), (W / 2 + 0.05, 1.4, zr), 0.16 * TH, 0.16 * TH, 'beam', tint=True)
    vkit.beam(f, (-W / 2 - 0.12, 0.36, 1.58), (W / 2 + 0.12, 0.36, 1.58), 0.22 * TH, 0.22 * TH, 'beam', bevel=0.02, tint=True)
    vkit.beam(f, (-0.28, 0.24, 1.56), (0.28, 0.24, 1.56), 0.12 * TH, 0.14 * TH, 'rope')          # padding
    # the torsion bundle: twisted rope skeins between the rails
    for i in range(7):
        a = 2 * math.pi * i / 7
        pts = [(-W / 2 + 0.06 + (W - 0.12) * t / 10,
                -0.5 + 0.11 * math.cos(a + 2.4 * t / 10 * math.pi),
                zr + 0.22 + 0.11 * math.sin(a + 2.4 * t / 10 * math.pi)) for t in range(11)]
        f.add(props.tube(pts, 0.035, seg=6), 'rope', smooth=True)
    # the winch at the rear with its iron ratchet wheels and handles
    vkit.rod(f, (-W / 2 - 0.15, -1.3, zr + 0.28), (W / 2 + 0.15, -1.3, zr + 0.28), 0.12, 'wood_dark', seg=12)
    # a fat winch drum thickly wound with pale rope (the white roll at the rear in Civ3)
    vkit.rod(f, (-0.42, -1.3, zr + 0.28), (0.42, -1.3, zr + 0.28), 0.17, 'wood_dark', seg=16)
    for layer, rr in enumerate((0.19, 0.225)):
        coil = [(-0.4 + 0.8 * i / 160, -1.3 + rr * math.cos(i * 0.62 + layer), zr + 0.28 + rr * math.sin(i * 0.62 + layer))
                for i in range(161)]
        f.add(props.tube(coil, 0.022, seg=5), 'coil', smooth=True)
    for sx in (-1, 1):   # the drum's flanges
        vkit.ring_x(f, 0.26, 0.02, 0.03, 'wood_dark', center=(sx * 0.43, -1.3, zr + 0.28), seg=20)
    for sx in (-1, 1):
        vkit.ring_x(f, 0.2, 0.05, 0.05, 'iron', center=(sx * (W / 2 + 0.12), -1.3, zr + 0.28), seg=24)
        vkit.beam(f, (sx * (W / 2 + 0.17), -1.3, zr + 0.28), (sx * (W / 2 + 0.17), -1.3, zr + 0.62), 0.05, 0.05, 'iron',
                  bevel=0.008)
    vkit.rod(f, (0, -1.3, zr + 0.3), (0, -0.62, zr + 0.36), 0.018, 'rope', seg=6)   # the winch line to the arm
    for sx in (-1, 1):
        x = sx * W / 2
        # civ-colored boards filling the side panels between the rails (as in Civ3)
        for y0, y1 in ((-1.3, -0.5), (-0.4, 0.65), (0.75, 1.35)) if panels else ():
            f.add(props.box(0.035, y1 - y0, 0.17), 'beam', T(x * 1.01, (y0 + y1) / 2, zr + 0.1), bevel=0.006, tint=True)
        # a toothed iron cog (the ratchet) on the winch axle, and a crank handle sticking out
        cx = sx * (W / 2 + 0.24)
        teeth = 16
        for i in range(teeth):
            a = 2 * math.pi * i / teeth
            f.add(props.box(0.04, 0.05, 0.06), 'iron',
                  T(cx, -1.3 + 0.27 * math.cos(a), zr + 0.28 + 0.27 * math.sin(a)) @ R('X', -math.degrees(a)), bevel=0.006)
        vkit.ring_x(f, 0.25, 0.08, 0.05, 'iron', center=(cx, -1.3, zr + 0.28), seg=28)
        vkit.rod(f, (cx, -1.3, zr + 0.28), (cx + sx * 0.18, -1.3, zr + 0.28), 0.025, 'iron', seg=8)
        vkit.rod(f, (cx + sx * 0.18, -1.3, zr + 0.28), (cx + sx * 0.18, -1.3, zr + 0.6), 0.022, 'iron', seg=8)
        vkit.rod(f, (cx + sx * 0.18, -1.3, zr + 0.6), (cx + sx * 0.3, -1.3, zr + 0.6), 0.03, 'wood_dark', seg=8)
    # rope lashing where the arm strikes, iron brackets with rivets at the A-frame feet
    for j in range(9):
        f.add(props.lathe([(0.0, 0.17), (0.02, 0.17)], seg=14), 'rope', T(-0.2 + 0.05 * j, 0.36, 1.58) @ R('Y', 90),
              smooth=True)
    # a pile of spare stones on the frame
    for i, (dx, dy) in enumerate(((0.15, -0.95), (-0.15, -0.9), (0.0, -1.05))):
        st = props.ellipsoid(0.13, 0.12, 0.11, seg=8)
        props.jitter(st, 0.03, seed=10 + i)
        f.add(st, 'stone', T(dx, dy, zr + 0.2 + (0.08 if i == 2 else 0.0)), bevel=0.008)
    f.finish()

    w = vkit.part(root, 'wheels', c, CATAPULT_WEAR)
    for sx in (-1, 1):
        for y in (-1.05, 1.05):
            vkit.spoked_wheel(w, 0.42, 0.14, 'felloe', 'wheel', 'iron', n_spokes=6,
                              center=(sx * (W / 2 + 0.16), y, 0.42), tire_key='iron')
    w.finish()

    # the throwing arm, pivoting in the torsion bundle, its cup end raised to the rear
    a = vkit.part(root, 'arm', c, CATAPULT_WEAR)
    sa = arm_len / 2.1   # arm_len: the cup's distance from the pivot (a shorter arm cocked back over the winch)
    vkit.beam(a, (0, 0.15, 0), (0, -2.0 * sa, 0), 0.25, 0.25, 'wood', bevel=0.025)
    for y in (-0.4, -1.2, -1.85):
        a.add(props.box(0.28, 0.06, 0.28), 'iron', T(0, y * sa, 0), bevel=0.01)
    yc = -2.1 * sa
    cs = props.S(cup)   # cup > 1: a bigger spoon cup
    if cup_level:   # the cup set on the arm so it opens straight up at this arm angle (the boulder seen in it)
        cs = R('X', arm) @ cs
    cp = props.lathe([(0.0, 0.01), (0.04, 0.3), (0.2, 0.38), (0.26, 0.38)], seg=24)
    a.add(cp, 'iron', T(0, yc, 0.02) @ cs, smooth=True)
    a.add(props.lathe([(0.0, 0.39), (0.03, 0.39)], seg=24), cup_rim, T(0, yc, 0.02) @ cs @ T(0, 0, 0.25), smooth=True)   # rim
    a.add(props.lathe([(0.0, 0.2), (0.1, 0.3), (0.17, 0.34)], seg=24), 'beam', T(0, yc, 0.02) @ cs @ T(0, 0, 0.07),
          smooth=True, tint=True)                                                                            # civ lining
    # a rough-hewn boulder: few big facets, knocked about (stone < 1: smaller, down in the cup)
    st = props.ellipsoid(0.3 * stone, 0.28 * stone, 0.26 * stone, seg=8)
    props.jitter(st, 0.06 * stone, seed=3)
    a.add(st, 'stone', T(0, yc, 0.02) @ (R('X', arm) if cup_level else props.S(1.0))
          @ T(0, 0, (0.3 - 0.26 * (1 - stone)) * (cup if cup != 1.0 else 1.0)), bevel=0.01)   # a big boulder sitting proud of the cup
    arm_obj = a.finish()
    arm_obj.matrix_parent_inverse = Matrix.Identity(4)
    arm_obj.location = (0, -0.5, zr + 0.22)
    arm_obj.rotation_euler = (math.radians(-arm), 0, 0)

    # the whole engine is compact next to its crew (as the old model)
    root.scale = (k, k, k)
    # crew: a winchman behind, a loader at the right side
    for i, (pos, yaw) in enumerate(crew or (((-0.35 / k, -2.0, 0.0), 0.0), ((1.05, -0.9, 0.0), 90.0))):
        vkit.crew(root, i, tuple(pos), yaw=yaw)
    return root


# ------------------------------------------------------------ cannon

# A blackened iron barrel with powder burns on a worn carriage.
BARREL_K = 1.25
CANNON_COLORS = {'gun_iron': '#707478', 'band': '#2a2c2e', 'iron': '#34363a', 'carriage': '#5a4430', 'carriage_dark': '#3e2e20', 'wheel': '#e8e8e8',
                 'felloe': '#6a4a2c', 'rim': '#9a9c9e', 'hub': '#5a5c5e'}
CANNON_WEAR = {'gun_iron': {'grime': 0.45, 'soot': 0.45, 'edges': 0.5}, 'band': {'grime': 0.5, 'soot': 0.5, 'edges': 0.7}, 'iron': {'grime': 0.5, 'soot': 0.6, 'rust': 0.2}, 'carriage': {'grime': 0.6, 'soot': 0.3},
               'carriage_dark': {'grime': 0.6}, 'felloe': {'grime': 0.65}, 'rim': {'grime': 0.5, 'rust': 0.3},
               'hub': {'grime': 0.5, 'rust': 0.25}}
props.DEFAULTS.setdefault('rim', ('#4a3a30', 0.55, 1.0, 'metal'))   # the wheels' iron tires
props.DEFAULTS.setdefault('gun_iron', ('#3b3e40', 0.5, 1.0, 'metal'))   # the barrel: dark, matte cast iron
props.DEFAULTS.setdefault('coil', ('#ddd2b4', 0.85, 0.0, 'rope'))      # the catapult's wound winch rope
props.DEFAULTS.setdefault('band', ('#2a2c2e', 0.45, 1.0, 'metal'))      # its blackened reinforce rings


def cannon(colors, elev=4.0, tint_spokes=True, spoke_scale=1.6, trail_tint=True, barrel_k=BARREL_K, cheek=0.12,
           rim_tint=False, k=1.0, capsquares=False):
    """A Napoleonic field gun: a cast-iron barrel with reinforce rings and a
    cascabel on a two-cheeked carriage with a trail, two big spoked wheels
    with civ-colored spokes, an iron axle; a gunner with a rammer."""
    c = dict(CANNON_COLORS, **colors)
    root = vkit.root('Cannon')
    f = vkit.part(root, 'carriage', c, CANNON_WEAR)
    side = [(0.55, 0.62), (0.3, 1.0), (-0.3, 1.02), (-1.3, 0.26), (-1.38, 0.0), (-1.1, 0.0)]   # a short trail
    for sx in (-1, 1):
        ch = props.slab([(y, z) for y, z in side], cheek, 'X')
        f.add(ch, 'beam' if trail_tint else 'carriage', T(sx * 0.3, 0, 0), bevel=0.015, tint=trail_tint)   # the trail painted in the civ color
        # iron straps on the cheeks
        for y, z in ((0.35, 0.78), (0.0, 0.95), (-0.45, 0.72), (-0.95, 0.42)):
            f.add(props.box(cheek + 0.01, 0.05, 0.2), 'hub', T(sx * 0.3, y, z), bevel=0.008)
            for dz in (-0.06, 0.06):
                f.add(props.ellipsoid(0.012, 0.012, 0.012, seg=8), 'hub', T(sx * (0.31 + cheek / 2), y, z + dz), smooth=True)   # rivets
        # long iron straps along the trail's top edge, bolted down
        for (y0, z0), (y1, z1) in zip(side[1:4], side[2:5]):
            vkit.beam(f, (sx * 0.3, y0, z0 + 0.015), (sx * 0.3, y1, z1 + 0.015), cheek + 0.015, 0.035, 'hub', bevel=0.006)
            for t in (0.25, 0.75):
                f.add(props.ellipsoid(0.018, 0.018, 0.018, seg=8), 'hub',
                      T(sx * 0.3, y0 + (y1 - y0) * t, z0 + (z1 - z0) * t + 0.04), smooth=True)
    vkit.beam(f, (-0.3, -1.28, 0.12), (0.3, -1.28, 0.12), 0.25, 0.2, 'carriage_dark', bevel=0.02)   # trail transom
    f.add(props.box(0.4, 0.05, 0.22), 'hub', T(0, -1.4, 0.04) @ R('X', -20), bevel=0.01)          # trail spade
    vkit.beam(f, (-0.3, -0.4, 0.68), (0.3, -0.4, 0.68), 0.14, 0.14, 'carriage_dark')               # transom
    vkit.rod(f, (-0.78, 0, 0.72), (0.78, 0, 0.72), 0.07, 'hub', seg=12)                            # axle
    vkit.rod(f, (0, -1.34, 0.24), (0, -1.46, 0.5), 0.03, 'hub', seg=8)                             # trail handle
    f.add(props.box(0.25, 0.16, 0.12), 'carriage_dark', T(0, -0.55, 0.86) @ R('X', 12), bevel=0.01)  # elevating quoin
    vkit.rod(f, (-0.22, -1.1, 0.55), (0.22, 0.4, 0.55), 0.016, 'wood', seg=8)                      # rammer on the trail
    f.add(props.ellipsoid(0.05, 0.05, 0.08, seg=10), 'rope', T(0.22, 0.45, 0.55) @ R('X', 90), smooth=True)  # its sponge
    vkit.rod(f, (0.4, -1.2, 0.0), (0.4, -1.2, 0.28), 0.12, 'wood_dark', seg=12)                    # water bucket
    if capsquares:   # iron capsquares strapped over the trunnions on the cheeks' tops, bolted
        for sx in (-1, 1):
            f.add(props.box(cheek + 0.04, 0.2, 0.07), 'hub', T(sx * 0.3, 0.0, 1.16), bevel=0.012)
            for dy in (-0.07, 0.07):
                f.add(props.ellipsoid(0.02, 0.02, 0.02, seg=8), 'iron', T(sx * 0.3, dy, 1.2), smooth=True)
    f.finish()

    w = vkit.part(root, 'wheels', c, CANNON_WEAR)
    for sx in (-1, 1):
        vkit.spoked_wheel(w, 0.72, 0.13, 'felloe', 'wheel', 'hub', n_spokes=10, center=(sx * 0.62, 0, 0.72),
                          tire_key='rim', tint_spokes=tint_spokes, tint_rim=rim_tint, spoke_scale=spoke_scale)
        w.add(props.lathe([(0.0, 0.11), (0.08, 0.09), (0.12, 0.04)], seg=16), 'hub', T(sx * 0.7, 0, 0.72) @ R('Y', sx * 90),
              smooth=True)                                                                                  # hub cap
        vkit.rod(w, (sx * 0.83, 0.0, 0.72), (sx * 0.83, 0.0, 0.62), 0.012, 'iron', seg=6)                  # linchpin
    w.finish()

    b = vkit.part(root, 'barrel', c, CANNON_WEAR)
    # breech, base ring, two reinforce rings, the chase and a flared muzzle
    prof = [(0.0, 0.19), (0.05, 0.25), (0.12, 0.25), (0.14, 0.22), (0.32, 0.22), (0.34, 0.25), (0.4, 0.25),
            (0.42, 0.21), (1.0, 0.185), (1.02, 0.215), (1.08, 0.215), (1.1, 0.18), (1.95, 0.135),
            (2.05, 0.15), (2.14, 0.19), (2.24, 0.19), (2.24, 0.075), (2.0, 0.075)]
    prof = [(z * barrel_k, r) for z, r in prof]   # Civ3's gun is long and slender, well past the wheels
    b.add(props.lathe(prof, seg=24), 'gun_iron', T(0, 0, -0.75), bevel=0.0, smooth=True)
    b.add(props.ellipsoid(0.11, 0.11, 0.11, seg=16), 'gun_iron', T(0, 0, -0.82), smooth=True)   # cascabel
    # the base ring, reinforce rings, chase ring and muzzle swell stand proud in blackened iron
    for z0, r in ((0.02, 0.265), (0.34, 0.262), (1.0, 0.228), (1.55, 0.18), (2.12, 0.205)):
        b.add(props.lathe([(0.0, r), (0.07, r)], seg=24), 'band', T(0, 0, -0.75 + z0 * barrel_k), smooth=True)
    vkit.rod(b, (-0.3, 0, 0.0), (0.3, 0, 0.0), 0.06, 'iron', seg=12)                       # trunnions
    for sx in (-1, 1):                                                                     # lifting dolphins
        b.add(props.tube([(sx * 0.08, -0.17, -0.12), (sx * 0.1, -0.24, 0.0), (sx * 0.08, -0.17, 0.12)], 0.025, seg=8),
              'gun_iron', T(0, 0, -0.1), smooth=True)
    bo = b.finish()
    bo.matrix_parent_inverse = Matrix.Identity(4)
    bo.location = (0, 0.0, 1.1)
    bo.rotation_euler = (math.radians(-90 + elev), 0, 0)
    root.scale = (k, k, k)
    # the gunner with his rammer stands behind the right wheel
    vkit.crew(root, 0, (0.55, -1.45, 0.0), yaw=15.0)
    return root


# ------------------------------------------------------------ trebuchet

TREBUCHET_COLORS = dict(CATAPULT_COLORS, box='#b89868', box_dark='#8a6c44')
props.DEFAULTS.setdefault('box', ('#b89868', 0.62, 0.0, 'wood'))        # the counterweight box's planks
props.DEFAULTS.setdefault('box_dark', ('#8a6c44', 0.62, 0.0, 'wood'))
props.DEFAULTS.setdefault('shot', ('#3e3c3a', 0.45, 1.0, 'metal'))      # round shot
TREBUCHET_WEAR = dict(CATAPULT_WEAR, box={'grime': 0.55, 'soot': 0.4}, box_dark={'grime': 0.6})


def _bracket(f, at, size=0.2, tint=True):
    """A civ-painted iron bracket wrapping a joint (Civ3 marks every joint of
    the trebuchet in the civ color), with rivets."""
    x, y, z = at
    f.add(props.box(size * 1.15, size, size), 'beam' if tint else 'iron', T(x, y, z), bevel=0.012, tint=tint)
    for dx in (-0.4, 0.4):
        f.add(props.ellipsoid(0.016, 0.016, 0.016, seg=6), 'iron', T(x + dx * size, y - size * 0.52, z), smooth=True)


def trebuchet(colors, k=0.8, arm=38.0, brackets='civ', piles=((1.25, -0.6), (-1.3, 1.0)), post=0.08, wheel_r=0.46,
              arm_key='wood', arm_long=3.0, arm_bands=(0.6, -0.5, -1.4, -2.3), band_w=0.08, sling=1.3, box=1.0,
              crew=None, sling_r=0.014, taper=False, arm_iron=(), box_band=0.07, short=0.85,
              base=1.0, arm_yaw=0.0):
    """arm < 0: at rest, the long end raised high to the rear, the box hanging low between the towers and the sling
    hanging from the arm's tip with its pouch and shot (arm > 0: cocked, as before)."""
    """A wheeled counterweight trebuchet, cocked: a long base on four spoked
    wheels, two braced A-frame towers carrying the axle, the throwing arm
    with its plank counterweight box raised high over the front and its long
    end pulled down to the rear with the sling, a winch, spare stone balls.
    Every joint carries a civ-colored bracket, as in Civ3."""
    c = dict(TREBUCHET_COLORS, **colors)

    def _bracket_(f_, at, size=0.2):   # brackets='iron': plain iron joints (the civ colour elsewhere)
        _bracket(f_, at, size, tint=brackets == 'civ' or (brackets == 'mixed' and abs(size - 0.24) < 1e-6))   # mixed: the cross-tie joints only
    root = vkit.root('Trebuchet')
    L, W, zr, za = 3.6, 1.4, 0.55, 2.7
    f = vkit.part(root, 'frame', c, TREBUCHET_WEAR)
    for sx in (-1, 1):
        x = sx * W / 2
        vkit.beam(f, (x, -L / 2, zr), (x, L / 2, zr), 0.2 * TH, 0.22 * TH, 'side_rail', bevel=0.02)
        vkit.beam(f, (x, -L / 2 + 0.2, zr + 0.24), (x, L / 2 - 0.2, zr + 0.24), 0.13 * TH, 0.13 * TH, 'beam', tint=True)
        # the tower: two raking legs and an upright post meeting at the axle block
        for y0 in (-1.25, 1.25):
            vkit.beam(f, (x, y0, zr + 0.1), (x, 0.0, za - 0.05), 0.16 * TH, 0.16 * TH, 'wood', bevel=0.015)
            _bracket_(f, (x, y0, zr + 0.2), 0.28)
        vkit.beam(f, (x, 0.0, zr + 0.1), (x, 0.0, za), 0.18 * TH, 0.18 * TH, 'wood', bevel=0.015)
        for z in (zr + 0.7, zr + 1.35):   # cross ties between the legs
            t = (z - zr) / (za - zr)
            vkit.beam(f, (x, -1.25 * (1 - t), z), (x, 1.25 * (1 - t), z), 0.11 * TH, 0.11 * TH, 'wood')
            for y in (-1.25 * (1 - t), 0.0, 1.25 * (1 - t)):
                _bracket_(f, (x, y, z), 0.24)   # chunky civ blocks at every joint, as the original
        f.add(props.box(0.32, 0.36, 0.3), 'wood_dark', T(x, 0.0, za), bevel=0.02)          # the axle block
        _bracket_(f, (x, 0.0, za - 0.22), 0.22)
        # long diagonal braces from the base ends to the towers, and knee braces at the legs' feet
        for sy in (-1, 1):
            vkit.beam(f, (x, sy * (L / 2 - 0.1), zr + 0.1), (x, sy * 0.55, zr + 1.35), 0.1 * TH, 0.1 * TH, 'wood')
            _bracket_(f, (x, sy * 0.55, zr + 1.35), 0.24)
            vkit.beam(f, (x, sy * 0.45, zr + 0.1), (x, sy * 0.95, zr + 0.55), 0.09 * TH, 0.09 * TH, 'wood')
            _bracket_(f, (x, sy * 0.95, zr + 0.5), 0.22)
        # brass caps on the axle's ends
        f.add(props.lathe([(0.0, 0.11), (0.05, 0.1), (0.08, 0.05), (0.09, 0.0)], seg=16), 'brass',
              T(sx * (W / 2 + 0.2), 0.0, za) @ R('Y', sx * 90), smooth=True)
    for y in (-L / 2 + 0.15, -0.9, 0.0, 0.9, L / 2 - 0.15):
        vkit.beam(f, (-W / 2, y, zr), (W / 2, y, zr), 0.15 * TH, 0.15 * TH, 'wood')
        for sx in (-1, 1):
            _bracket_(f, (sx * W / 2, y, zr), 0.17)
    vkit.beam(f, (-W / 2 - 0.1, 0.0, za + 0.18), (W / 2 + 0.1, 0.0, za + 0.18), 0.16, 0.16, 'beam', tint=True)  # cap
    vkit.rod(f, (-W / 2 - 0.2, 0.0, za), (W / 2 + 0.2, 0.0, za), 0.07, 'iron', seg=12)                    # axle
    # the winch at the rear: a drum wound with pale rope between two spoked hand wheels
    yw = -L / 2 + 0.45
    vkit.rod(f, (-W / 2, yw, zr + 0.35), (W / 2, yw, zr + 0.35), 0.13, 'wood_dark', seg=14)
    for layer, rr in enumerate((0.15, 0.18)):
        coil = [(-0.4 + 0.8 * i / 140, yw + rr * math.cos(i * 0.62 + layer), zr + 0.35 + rr * math.sin(i * 0.62 + layer))
                for i in range(141)]
        f.add(props.tube(coil, 0.02, seg=5), 'coil', smooth=True)
    for sx in (-1, 1):
        vkit.spoked_wheel(f, 0.34, 0.05, 'wood_dark', 'wood', 'iron', n_spokes=6,
                          center=(sx * (W / 2 + 0.16), yw, zr + 0.35))
    # the trough the sling and its stone lie in, between the rails at the rear
    f.add(props.box(0.42, 1.1, 0.06), 'wood_dark', T(0, -1.05, zr + 0.12), bevel=0.01)
    for sx in (-1, 1):
        f.add(props.box(0.05, 1.1, 0.14), 'wood', T(sx * 0.22, -1.05, zr + 0.18), bevel=0.01)
    # dark round shot piled on both sides (so a pile shows from every direction)
    pile = ((0.0, 0.0, 0.0), (0.3, 0.05, 0.0), (0.15, 0.27, 0.0), (0.15, 0.1, 0.24), (-0.12, 0.25, 0.0))
    for ox, oy in piles:
        for i, (dx, dy, dz) in enumerate(pile):
            f.add(props.ellipsoid(0.15, 0.15, 0.15, seg=14), 'shot', T(ox + dx, oy + dy, 0.15 + dz), smooth=True)
    fo = f.finish()
    if base != 1.0:   # base < 1: the whole frame (towers too) narrowed and shortened in plan, its height kept
        fo.scale = (base, base, 1.0)

    w = vkit.part(root, 'wheels', c, TREBUCHET_WEAR)
    for sx in (-1, 1):
        for y in (-1.2, 1.2):
            vkit.spoked_wheel(w, wheel_r, 0.15, 'felloe', 'wheel', 'iron', n_spokes=8,
                              center=(sx * (W / 2 + 0.18) * base, y * base, wheel_r), tire_key='iron')
    w.finish()

    # the arm about the axle: the short end forward and up carrying the box, the long end down to the rear
    a = vkit.part(root, 'arm', c, TREBUCHET_WEAR)
    if taper:   # heavy at the axle, slimmer toward the sling end
        vkit.beam(a, (0, short, 0), (0, -arm_long * 0.45, 0), 0.3, 0.32, arm_key, bevel=0.025)
        vkit.beam(a, (0, -arm_long * 0.4, 0), (0, -arm_long, 0), 0.2, 0.2, arm_key, bevel=0.02)
    else:
        vkit.beam(a, (0, short, 0), (0, -arm_long, 0), 0.24, 0.26, arm_key, bevel=0.025)
    for y in arm_iron:   # plain iron bands
        a.add(props.box(0.34, 0.06, 0.34), 'iron', T(0, y, 0), bevel=0.01)
    for y in arm_bands:
        a.add(props.box(0.3, band_w, 0.3), 'beam', T(0, y, 0), bevel=0.01, tint=True)   # civ-painted iron bands
    sling_end = (0, -arm_long, 0)
    arm_obj = a.finish()
    arm_obj.matrix_parent_inverse = Matrix.Identity(4)
    arm_obj.location = (0, 0.0, za)
    arm_obj.rotation_euler = (math.radians(arm), 0, math.radians(arm_yaw))   # the long end down to the rear, the box end up

    # the counterweight box, hanging plumb from the arm's short end on its hinge
    bpy.context.view_layer.update()
    tip = arm_obj.matrix_world @ Vector((0, short - 0.05, 0))
    tip = root.matrix_world.inverted() @ tip
    b = vkit.part(root, 'box', c, TREBUCHET_WEAR)
    bw, bd, bh = 1.0 * box, 0.95 * box, 0.85 * box
    zb = tip.z - 0.2 - bh / 2
    b.add(props.box(bw, bd, bh), 'box', T(tip.x, tip.y, zb), bevel=0.02)
    for i in range(1, 6):   # plank seams
        z = zb - bh / 2 + bh * i / 6
        b.add(props.box(bw + 0.012, bd + 0.012, 0.012), 'box_dark', T(tip.x, tip.y, z), bevel=0.002)
    for sx in (-1, 1):   # corner posts and civ-painted iron bands round it
        for sy in (-1, 1):
            b.add(props.box(post, post, bh + 0.04), 'wood_dark', T(tip.x + sx * bw / 2, tip.y + sy * bd / 2, zb), bevel=0.01)
    for z in (zb - bh * 0.32, zb + bh * 0.32):
        b.add(props.box(bw + 0.03, bd + 0.03, box_band), 'beam', T(tip.x, tip.y, z), bevel=0.008, tint=True)
    for sx in (-1, 1):   # the hangers from the hinge pin down to the box
        vkit.beam(b, (tip.x + sx * 0.3, tip.y, tip.z), (tip.x + sx * 0.4, tip.y, zb + bh / 2), 0.07, 0.07, 'iron', bevel=0.006)
    vkit.rod(b, (tip.x - 0.45, tip.y, tip.z), (tip.x + 0.45, tip.y, tip.z), 0.04, 'iron', seg=10)
    for sx in (-1, 1):   # brass caps on the hinge pin and brass corner caps on the box's top
        b.add(props.ellipsoid(0.06, 0.06, 0.06, seg=12), 'brass', T(tip.x + sx * 0.47, tip.y, tip.z), smooth=True)
        for sy in (-1, 1):
            b.add(props.box(0.12, 0.12, 0.05), 'brass', T(tip.x + sx * bw / 2, tip.y + sy * bd / 2, zb + bh / 2 + 0.02),
                  bevel=0.01)
    # the sling from the arm's long end to the stone resting in the trough
    end = root.matrix_world.inverted() @ (arm_obj.matrix_world @ Vector(sling_end))
    stone_at = Vector((0, -1.25 * base, zr + 0.32)) if arm > 0 else Vector((0, end.y - 0.12, end.z - sling))   # hanging free
    for sx in (-1, 1):
        vkit.rod(b, tuple(end), (sx * 0.12, stone_at.y, stone_at.z + 0.05), sling_r, 'rope', seg=5)
    st = props.ellipsoid(0.2, 0.2, 0.19, seg=12)
    props.jitter(st, 0.02, seed=7)
    b.add(st, 'stone', T(*stone_at), smooth=True)
    b.add(props.ellipsoid(0.24, 0.26, 0.1, seg=12), 'rope', T(stone_at.x, stone_at.y, stone_at.z - 0.1), smooth=True)  # pouch
    # the winch rope from the drum up to the arm's end, holding it down
    if arm > 0:
        vkit.rod(b, (0, yw, zr + 0.5), tuple(end), 0.016, 'rope', seg=5)
    b.finish()

    root.scale = (k, k, k)
    for i, (pos, yaw) in enumerate(crew or (((-1.1, 0.4, 0.0), 90.0), ((1.15, 1.3, 0.0), -90.0))):
        vkit.crew(root, i, tuple(pos), yaw=yaw)
    return root


# ------------------------------------------------------------ hwacha

HWACHA_COLORS = dict(CATAPULT_COLORS, wood='#d2ac72', wood_dark='#8a6840', wheel='#b89058', felloe='#9a7444',
                     fletch='#ece8dc', arrow_head='#3a3836', paper='#b89a6a')
props.DEFAULTS.setdefault('fletch', ('#e6e0d0', 0.9, 0.0, 'cloth'))
props.DEFAULTS.setdefault('arrow_head', ('#3a3836', 0.5, 1.0, 'metal'))
props.DEFAULTS.setdefault('paper', ('#c8463a', 0.8, 0.0, 'cloth'))   # the rockets' red paper powder tubes
HWACHA_WEAR = dict(CATAPULT_WEAR, fletch={'grime': 0.4}, paper={'grime': 0.5, 'soot': 0.4})


def hwacha(colors, k=1.0, tilt=32.0, shaft=0.014, fletch=(0.004, 0.16, 0.05), head=(0.02, 0.11, 0.0),
           bed_stripes=True, handle_z=0.45, nx=9, nz=5, head_key='steel', crew=None, stagger=False):
    """A Korean hwacha: a two-wheeled handcart with long civ-colored shafts
    running back into handles, a slatted bed, and on it a tilted launcher
    box of pale wood packed with rows of singijeon fire arrows (red paper
    powder tubes, dark iron heads, white fletching), propped up by a strut."""
    c = dict(HWACHA_COLORS, **colors)
    root = vkit.root('Hwacha')
    f = vkit.part(root, 'cart', c, HWACHA_WEAR)
    r, track, za = 0.66, 0.66, 0.66
    for sx in (-1, 1):
        # the shafts: forward of the axle to the front, back down into the handles
        vkit.beam(f, (sx * 0.42, 0.9, za + 0.12), (sx * 0.42, -1.75, handle_z), 0.09, 0.1, 'beam', bevel=0.012, tint=True)
        f.add(props.box(0.06, 0.16, 0.06), 'wood_dark', T(sx * 0.42, -1.82, handle_z - 0.02), bevel=0.012)       # handle grip
        vkit.beam(f, (sx * 0.42, 0.85, za + 0.12), (sx * 0.42, 0.8, 0.0), 0.07, 0.07, 'wood_dark')     # front leg
        f.add(props.box(0.2, 0.14, 0.12), 'iron', T(sx * 0.42, 0.0, za + 0.1), bevel=0.01)             # axle bearing
    for i in range(13):   # a wide civ-colored slatted bed over the shafts, as the original
        y = 0.82 - 0.2 * i
        z = za + 0.12 + (handle_z - za - 0.12) * max(0.0, -y) / 1.75 + 0.06
        if i % 2 or not bed_stripes:   # tan filler blocks between the blue rails, as the original (or all timber)
            f.add(props.box(1.1, 0.14, 0.05), 'wood', T(0, y, z + 0.005), bevel=0.008)
        else:
            f.add(props.box(1.16, 0.15, 0.06), 'beam', T(0, y, z + 0.01), bevel=0.008, tint=True)
    for sx in (-1, 1):   # pale side rails edging the bed
        vkit.beam(f, (sx * 0.6, 0.88, za + 0.2), (sx * 0.6, -1.65, handle_z + 0.08), 0.05, 0.07, 'wood', bevel=0.008)
    vkit.rod(f, (-track - 0.1, 0.0, za), (track + 0.1, 0.0, za), 0.04, 'iron', seg=10)
    f.finish()

    w = vkit.part(root, 'wheels', c, HWACHA_WEAR)
    for sx in (-1, 1):
        vkit.spoked_wheel(w, r, 0.1, 'felloe', 'wheel', 'iron', n_spokes=12, center=(sx * track, 0.0, za),
                          tire_key='iron')
        w.add(props.lathe([(0.0, 0.13), (0.1, 0.12), (0.16, 0.07), (0.19, 0.0)], seg=18), 'wood_dark',
              T(sx * (track - 0.04), 0.0, za) @ R('Y', sx * 90), smooth=True)                    # a heavy hub boss
        w.add(props.lathe([(0.0, 0.135), (0.03, 0.135)], seg=18), 'iron', T(sx * (track + 0.02), 0.0, za) @ R('Y', 90),
              smooth=True)
    w.finish()

    # the launcher box, hinged at its back on the bed, tilted up toward the front
    L = vkit.part(root, 'launcher', c, HWACHA_WEAR)
    bw, bd, bh = 0.86, 1.05, 0.52
    L.add(props.box(bw, bd, 0.04), 'wood', T(0, 0, 0.02), bevel=0.006)              # floor
    for sx in (-1, 1):                                                              # side boards with civ panels
        L.add(props.box(0.04, bd, bh), 'wood', T(sx * bw / 2, 0, bh / 2), bevel=0.006)
        L.add(props.box(0.012, bd * 0.8, bh * 0.6), 'beam', T(sx * (bw / 2 + 0.022), 0, bh / 2), bevel=0.003, tint=True)
    L.add(props.box(bw, 0.04, bh), 'wood', T(0, -bd / 2, bh / 2), bevel=0.006)      # back board, drilled
    for sy in (-1, 1):                                                  # civ-colored corner posts and top bars
        for sx in (-1, 1):
            L.add(props.box(0.08, 0.08, bh + 0.08), 'beam', T(sx * bw / 2, sy * bd / 2, bh / 2), bevel=0.01, tint=True)
        L.add(props.box(bw + 0.08, 0.08, 0.08), 'beam', T(0, sy * bd / 2, bh + 0.03), bevel=0.01, tint=True)
    # the fire arrows: rows and columns, each a shaft with a red powder tube, an iron head, white feathers
    for i in range(nx):
        for j in range(nz):
            x = -bw / 2 + 0.08 + (bw - 0.16) * i / (nx - 1)
            z = 0.08 + (bh - 0.12) * j / (nz - 1)
            y0, y1 = -bd / 2 - 0.2, bd / 2 + 0.95   # the arrows stand far out of the box's face
            if stagger:   # rows offset by half a column and arrows pushed out unevenly: no flat grille
                x += (j % 2) * 0.5 * (bw - 0.16) / (nx - 1) * (1 if i < nx - 1 else 0)
                y1 += 0.12 * ((i * 7 + j * 3) % 3)
            L.add(props.tube([(x, y0, z), (x, y1, z)], shaft, seg=5), 'wood', smooth=True)
            L.add(props.tube([(x, y1 - 0.34, z), (x, y1 - 0.1, z)], 0.024, seg=6), 'paper', smooth=True)
            L.add(props.lathe([(0.0, head[0]), (head[1], head[2])], seg=4), head_key, T(x, y1, z) @ R('X', -90), smooth=True)
            for a in (0, 90):
                L.add(props.box(*fletch), 'fletch', T(x, y0 + 0.08, z) @ R('Y', a), bevel=0.0)
    lo = L.finish()
    lo.matrix_parent_inverse = Matrix.Identity(4)
    lo.location = (0, -0.15, za + 0.24)
    lo.rotation_euler = (math.radians(tilt), 0, 0)
    s = vkit.part(root, 'strut', c, HWACHA_WEAR)   # the prop holding the box up at its tilt
    for sx in (-1, 1):
        vkit.beam(s, (sx * 0.3, -0.5, za + 0.2), (sx * 0.3, -0.62, za + 0.24 + 0.55 * math.sin(math.radians(tilt))),
                  0.05, 0.05, 'wood_dark')
    s.finish()

    root.scale = (k, k, k)
    for i, (pos, yaw) in enumerate(crew or (((-0.6, -1.9, 0.0), 0.0), ((0.9, -0.6, 0.0), -90.0))):
        vkit.crew(root, i, tuple(pos), yaw=yaw)
    return root


# ------------------------------------------------------------ artillery (a WWII field gun)

props.DEFAULTS.setdefault('camo', ('#5a6040', 0.7, 0.3, 'metal'))      # olive drab paint over steel
props.DEFAULTS.setdefault('camo2', ('#6a5034', 0.7, 0.3, 'metal'))     # brown blotches
props.DEFAULTS.setdefault('camo3', ('#3a4030', 0.7, 0.3, 'metal'))     # dark green blotches
props.DEFAULTS.setdefault('tyre', ('#1e1e1e', 0.85, 0.0, 'leather'))
props.DEFAULTS.setdefault('tread', ('#4c4c48', 0.9, 0.0, 'leather'))   # worn tread blocks, lighter than the tyre
props.DEFAULTS.setdefault('rim_steel', ('#a8aaa8', 0.4, 1.0, 'metal'))
ARTILLERY_COLORS = {'rim_steel': '#7a7c76'}
ARTILLERY_WEAR = {'camo': {'grime': 0.5, 'soot': 0.3, 'edges': 0.5}, 'camo2': {'grime': 0.5}, 'camo3': {'grime': 0.5},
                  'tyre': {'grime': 0.6}, 'rim_steel': {'grime': 0.4, 'rust': 0.2}, 'iron': {'grime': 0.5, 'soot': 0.5}}


def _camo(scale=0.35):
    """A face-key function: olive with brown and dark green blotches (Civ3's camouflage)."""
    def keys(f):
        c = f.calc_center_median()
        n = math.sin(c.x / scale * 1.7 + 1.3) + math.sin(c.y / scale * 1.3 - 0.7) + math.sin(c.z / scale * 2.1 + c.x)
        return ('camo2', False) if n > 0.5 else (('camo3', False) if n < -0.8 else None)
    return keys


def _sub(bm, cuts=6):
    """Subdivide a mesh's faces so per-face patterns (camo) have room to show."""
    props.bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=cuts, use_grid_fill=True)
    return bm


def _pneumatic_wheel(p, r, width, center, side, disc='rim_steel', keys=None, relief=False, tread='tyre'):
    """A gun-carriage road wheel: a fat dark tyre on a pressed steel disc with
    lightening holes and a hub; `side` +-1 is the outward face."""
    cx, cy, cz = center
    k = 32
    tyre = []
    for i in range(k + 1):
        a = 2 * math.pi * i / k
        tyre.append((cx, cy + (r - 0.06) * math.cos(a), cz + (r - 0.06) * math.sin(a)))
    p.add(props.tube(tyre, 0.065, seg=10, scale_xy=(width / 0.13, 1.0)), 'tyre', smooth=True)
    plate = props.ellipsoid(r - 0.1, r - 0.1, 0.03, seg=24)
    p.add(plate, disc, T(cx, cy, cz) @ R('Y', 90), smooth=True, keys=keys)
    for i in range(6):   # the disc's lightening holes, dark
        a = 2 * math.pi * i / 6
        p.add(props.ellipsoid(0.075, 0.075, 0.035, seg=10), 'tyre',
              T(cx + side * 0.008, cy + 0.2 * r * math.cos(a) * 2.2, cz + 0.2 * r * math.sin(a) * 2.2) @ R('Y', 90), smooth=True)
    p.add(props.lathe([(0.0, 0.08), (0.06, 0.06), (0.09, 0.0)], seg=16), 'iron', T(cx + side * 0.02, cy, cz) @ R('Y', side * 90),
          smooth=True)
    if relief:   # relief that reads at 1x: tread blocks round the tyre, a raised rim lip, hub nuts
        for i in range(18):
            a = 2 * math.pi * i / 18
            p.add(props.box(width * 1.04, 0.07, 0.035), tread, T(cx, cy + (r - 0.005) * math.cos(a), cz + (r - 0.005) * math.sin(a))
                  @ R('X', math.degrees(a) + 90), bevel=0.008)
        vkit.ring_x(p, r - 0.1, r - 0.15, 0.07, disc, center=(cx + side * 0.01, cy, cz), seg=28)
        for i in range(6):
            a = 2 * math.pi * i / 6 + 0.3
            p.add(props.ellipsoid(0.022, 0.022, 0.022, seg=8), 'iron',
                  T(cx + side * 0.06, cy + 0.12 * math.cos(a), cz + 0.12 * math.sin(a)), smooth=True)


def artillery(colors, k=1.0, elev=8.0, spread=11.0, disc='rim_steel', spade_tint=True, shield_civ='panel',
              shield_bevel=0.01, relief=False, crew=None, band=True, front_band=False, tread='tyre'):
    """shield_civ 'both': the civ panel on both faces of the shield and a civ band across its front."""
    """A WWII towed field gun in camouflage: a long barrel with a muzzle brake
    and recoil cylinders over it, a gun shield (its top edge civ-colored),
    two pressed-steel wheels with fat tyres, and the split trail opened wide
    behind, each leg ending in a civ-colored spade."""
    c = dict(ARTILLERY_COLORS, **colors)
    root = vkit.root('Artillery')
    za = 0.56
    f = vkit.part(root, 'carriage', c, ARTILLERY_WEAR)
    camo = _camo(0.25)
    f.add(_sub(props.box(0.7, 0.6, 0.32)), 'camo', T(0, 0.0, za + 0.12), bevel=0.03, keys=camo)            # the saddle
    vkit.rod(f, (-0.88, 0, za), (0.88, 0, za), 0.05, 'iron', seg=10)                                   # axle
    for sx in (-1, 1):
        # the trail legs, opened in a V behind the gun, each ending in a spade
        a = math.radians(spread) * sx
        end = (math.sin(a) * 1.9, -math.cos(a) * 1.9, 0.12)
        vkit.beam(f, (sx * 0.15, -0.15, za), end, 0.12, 0.15, 'camo', bevel=0.012)
        f.add(props.box(0.32, 0.06, 0.3), 'beam' if spade_tint else 'iron', T(end[0], end[1] - 0.03, 0.12) @ R('Z', math.degrees(a)),
              bevel=0.01, tint=spade_tint)
        vkit.rod(f, (end[0] * 0.92, end[1] * 0.92, 0.25), (end[0] * 0.92, end[1] * 0.92 - 0.1, 0.33), 0.018, 'iron', seg=6)
        # the gun shield: a sloped plate each side of the barrel, its top edge civ-colored
        sh = _sub(props.box(0.72, 0.04, 0.95), 8)
        f.add(sh, 'camo', T(sx * 0.42, 0.28, za + 0.5) @ R('X', -12) @ R('Z', -sx * 10), bevel=shield_bevel, keys=camo)
        f.add(props.box(0.74, 0.06, 0.1), 'beam', T(sx * 0.42, 0.24, za + 0.98) @ R('X', -12) @ R('Z', -sx * 10),
              bevel=0.01, tint=True)
        # a big civ-colored panel on the shield's face, as the original
        f.add(props.box(0.42, 0.02, 0.4), 'beam', T(sx * 0.42, 0.24, za + 0.62) @ R('X', -12) @ R('Z', -sx * 10), bevel=0.006, tint=True)
        if shield_civ == 'both':   # through the plate, so it shows on both faces: a panel and a band below it
            f.add(props.box(0.44, 0.075, 0.36), 'beam', T(sx * 0.42, 0.28, za + 0.66) @ R('X', -12) @ R('Z', -sx * 10),
                  bevel=0.008, tint=True)
            if front_band:   # a solid civ band across the shield's front face, up where the gun is seen from 1-3
                f.add(props.box(0.76, 0.05, 0.17), 'beam', T(sx * 0.42, 0.315, za + 0.88) @ R('X', -12) @ R('Z', -sx * 10),
                      bevel=0.008, tint=True)
            if shield_civ == 'both' and band:
                f.add(props.box(0.75, 0.075, 0.12), 'beam', T(sx * 0.42, 0.28, za + 0.3) @ R('X', -12) @ R('Z', -sx * 10),
                      bevel=0.008, tint=True)
    f.add(props.box(0.12, 0.035, 0.45), 'camo', T(0, 0.3, za + 0.12), bevel=0.01, keys=camo)          # lower shield
    f.finish()
    w = vkit.part(root, 'wheels', c, ARTILLERY_WEAR)
    for sx in (-1, 1):
        _pneumatic_wheel(w, za, 0.2, (sx * 0.86, 0.0, za), sx, disc=disc, relief=relief, tread=tread)
    w.finish()
    b = vkit.part(root, 'barrel', c, ARTILLERY_WEAR)
    b.add(props.lathe([(-0.6, 0.15), (0.3, 0.14), (0.36, 0.095), (1.7, 0.078)], seg=24), 'camo3', smooth=True)   # olive tube
    # a heavy dark double-baffle muzzle brake with its side ports
    b.add(props.lathe([(0.0, 0.1), (0.04, 0.135), (0.14, 0.135), (0.16, 0.11), (0.18, 0.135), (0.3, 0.135), (0.33, 0.07)], seg=16), 'gun_dark', T(0, 0, 1.68), smooth=True)
    for z in (1.77, 1.9):
        b.add(props.box(0.29, 0.06, 0.05), 'tyre', T(0, 0, z), bevel=0.004)
    for sx in (-1, 1):   # recoil and recuperator cylinders over the barrel
        b.add(props.lathe([(-0.6, 0.07), (0.75, 0.065), (0.8, 0.0)], seg=14), 'camo', T(sx * 0.07, 0.19, 0), smooth=True, keys=camo)
    b.add(_sub(props.box(0.36, 0.42, 0.95), 5), 'camo', T(0, 0.04, -0.15), bevel=0.025, keys=camo)   # the cradle
    b.add(_sub(props.box(0.3, 0.34, 0.4), 4), 'camo', T(0, 0.0, -0.75), bevel=0.02, keys=camo)       # the breech
    bo = b.finish()
    bo.matrix_parent_inverse = Matrix.Identity(4)
    bo.location = (0, 0.05, za + 0.36)
    bo.rotation_euler = (math.radians(-90 + elev), 0, 0)
    root.scale = (k, k, k)
    for i, (pos, yaw) in enumerate(crew or (((-0.42, -0.5, 0.0), 10.0),)):   # tucked in behind the shield at the breech
        vkit.crew(root, i, tuple(pos), yaw=yaw)
    return root


# ------------------------------------------------------------ flak (a quad 20 mm anti-aircraft gun)

props.DEFAULTS.setdefault('gun_dark', ('#2c2e2c', 0.45, 1.0, 'metal'))


def flak(colors, k=1.0, elev=48.0, disc='rim_steel', trailer=True, rig=(0.12, 0.1), shield_band=False, crew=None,
         zp=0.42, gun_r=1.0, shield='drum', hider=1.0, hider_key='rim_steel', hinges=False, wing_band=True):
    """trailer=False: emplaced, firing from the cross base (no wheels, mudguards or drawbar); rig: the outriggers'
    section; shield_band: a civ band round the shield's face."""
    """A quadruple 20 mm anti-aircraft gun on its two-wheeled trailer: a
    cross-shaped platform with levelling jacks, the wheels with civ-colored
    hubs, a triangular lattice drawbar, a turning pedestal carrying four
    barrels in a 2x2 block raised high, their tall box magazines, the
    gunner's curved shield (civ-colored edges) and seat."""
    # Civ3's Flak: dark olive drab with soft camouflage shading
    c = dict({'camo': '#4c5238', 'camo2': '#5a5236', 'camo3': '#3a4030', 'rim_steel': '#6a6c66'}, **colors)
    root = vkit.root('Flak')
    f = vkit.part(root, 'trailer', c, ARTILLERY_WEAR)
    camo = _camo(0.3)
    oct_ = props.lathe([(0.0, 0.75), (0.06, 0.75)], seg=8)
    f.add(oct_, 'camo', T(0, 0, zp) @ R('Z', 22.5), keys=camo)
    f.add(props.ellipsoid(0.74, 0.74, 0.02, seg=8), 'camo', T(0, 0, zp + 0.06) @ R('Z', 22.5), keys=camo)
    for a in (0, 90, 180, 270):   # outriggers with levelling jacks
        ra = math.radians(a)
        tip = (1.15 * math.sin(ra), 1.15 * math.cos(ra), zp - 0.05)
        vkit.beam(f, (0.5 * math.sin(ra), 0.5 * math.cos(ra), zp), tip, rig[0], rig[1], 'camo', bevel=min(0.03, rig[1] * 0.2))
        vkit.rod(f, (tip[0], tip[1], tip[2] + 0.1), (tip[0], tip[1], 0.04), 0.025, 'iron', seg=8)
        f.add(props.ellipsoid(0.08, 0.08, 0.025, seg=12), 'iron', T(tip[0], tip[1], 0.03), smooth=True)
    for sx in (-1, 1) if trailer else ():   # the travelling wheels, raised, with civ hubs
        f.add(props.box(0.08, 0.5, 0.08), 'camo', T(sx * 0.82, -0.1, zp), bevel=0.01)
        _pneumatic_wheel(f, 0.44, 0.2, (sx * 0.98, -0.1, 0.44), sx, disc=disc)
        f.add(props.ellipsoid(0.18, 0.18, 0.04, seg=18), 'beam', T(sx * 1.1, -0.1, 0.44) @ R('Y', 90), smooth=True, tint=True)
        # an arched mudguard over the wheel, a civ-colored patch on its crown
        arc = [(sx * 0.98, -0.1 + 0.56 * math.cos(math.radians(a)), 0.44 + 0.56 * math.sin(math.radians(a)))
               for a in range(20, 165, 12)]
        guard = props.loft([[(x - 0.14, y, z) for x, y, z in arc], [(x + 0.14, y, z) for x, y, z in arc]], False, False)
        props.bmesh.ops.solidify(guard, geom=guard.faces[:], thickness=0.02)
        f.add(guard, 'camo', smooth=True, keys=camo)
        f.add(props.box(0.24, 0.3, 0.03), 'beam', T(sx * 0.98, -0.1, 1.01), bevel=0.008, tint=True)
    for sx in (-1, 1):
        # a bank of spare box magazines racked upright beside the mount
        for i in range(5):
            f.add(props.box(0.06, 0.2, 0.36), 'rim_steel', T(sx * 0.42 + sx * 0.07 * i * 0.0, 0.42 + 0.075 * i - 0.15,
                                                              zp + 0.24) @ R('Z', sx * 90), bevel=0.008)
    # the lattice drawbar behind: two rails converging to a towing eye, with cross bracing
    eye = (0.0, -2.0, 0.35)
    for sx in (-1, 1) if trailer else ():
        vkit.rod(f, (sx * 0.6, -0.55, zp), eye, 0.025, 'camo', seg=6)
    for t in (0.25, 0.5, 0.75) if trailer else ():
        y = -0.55 + (eye[1] + 0.55) * t
        hw = 0.6 * (1 - t)
        vkit.rod(f, (-hw, y, zp - 0.02 * t), (hw, y, zp - 0.02 * t), 0.018, 'camo', seg=6)
        if t < 0.75:
            y2 = -0.55 + (eye[1] + 0.55) * (t + 0.25)
            vkit.rod(f, (-hw, y, zp), (0.6 * (0.75 - t), y2, zp), 0.014, 'camo', seg=5)
            vkit.rod(f, (hw, y, zp), (-0.6 * (0.75 - t), y2, zp), 0.014, 'camo', seg=5)
    if trailer:
        f.add(props.lathe([(0.0, 0.06), (0.03, 0.06)], seg=12), 'iron', T(*eye), smooth=True)
    # the pedestal and the gunner's seat with a civ-colored back
    f.add(props.lathe([(0.0, 0.3), (0.25, 0.26), (0.3, 0.2)], seg=20), 'camo', T(0, 0, zp + 0.06), smooth=True, keys=camo)
    f.add(props.box(0.34, 0.3, 0.06), 'gun_dark', T(0, -0.55, zp + 0.55), bevel=0.02)
    f.add(props.box(0.34, 0.06, 0.32), 'beam', T(0, -0.72, zp + 0.74), bevel=0.02, tint=True)
    vkit.rod(f, (0, -0.4, zp + 0.36), (0, -0.55, zp + 0.52), 0.035, 'iron', seg=8)
    f.finish()

    # the gun cradle, raised: four barrels in a 2x2 block, box magazines standing on top, the shield in front
    g = vkit.part(root, 'guns', c, ARTILLERY_WEAR)
    g.add(_sub(props.box(0.66, 0.6, 0.42), 4), 'camo', T(0, -0.05, 0), bevel=0.03, keys=camo)         # the cradle
    for sx in (-1, 1):
        for sz in (-1, 1):
            x, z = sx * 0.17, sz * 0.1
            g.add(props.box(0.1, 0.5, 0.1), 'gun_dark', T(x, 0.0, z), bevel=0.012)                    # receivers
            g.add(props.lathe([(0.0, 0.05), (0.35, 0.048)], seg=12), 'camo', T(x, 0.25, z) @ R('X', -90), smooth=True)   # jackets
            g.add(props.lathe([(0.0, 0.032 * gun_r), (1.25, 0.03 * gun_r)], seg=10), 'gun_dark', T(x, 0.2, z) @ R('X', -90),
                  smooth=True)
            hr = gun_r * hider   # hider < 1: smaller flash hiders
            g.add(props.lathe([(0.0, 0.035 * hr), (0.06, 0.04 * hr), (0.2 * hider, 0.065 * hr)], seg=12), hider_key,
                  T(x, 1.42, z) @ R('X', -90), smooth=True)                                                                        # pale flared flash hiders
            g.add(props.box(0.05, 0.16, 0.32), 'gun_dark', T(x + sx * 0.08, -0.02, z + sz * 0.3), bevel=0.008)  # magazines
    g.add(props.box(0.3, 0.06, 0.12), 'brass', T(0, -0.36, 0.1), bevel=0.01)                          # brass sight box
    for sx in (-1, 1):   # trunnion arms down to the pedestal, and handwheels
        vkit.beam(g, (sx * 0.36, 0, 0), (sx * 0.36, -0.12, -0.42), 0.06, 0.12, 'camo', bevel=0.008)
        vkit.ring_x(g, 0.12, 0.02, 0.02, 'iron', center=(sx * 0.45, -0.3, -0.25), seg=16)
    go = g.finish()
    go.matrix_parent_inverse = Matrix.Identity(4)
    go.location = (0, 0.05, zp + 0.95)
    go.rotation_euler = (math.radians(elev), 0, 0)
    s = vkit.part(root, 'shield', c, ARTILLERY_WEAR)
    if shield == 'plate':   # the historical shield: a flat front plate and two wings angled back at its sides
        h0, h1 = zp + 0.5, zp + 1.22
        fw, wing, ang, y0 = 1.0, 0.45, 35.0, 0.42
        s.add(_sub(props.box(fw, 0.035, h1 - h0), 6), 'camo', T(0, y0, (h0 + h1) / 2), bevel=0.012, keys=camo)
        s.add(props.box(fw + 0.02, 0.05, 0.07), 'beam', T(0, y0, h1 - 0.02), bevel=0.01, tint=True)        # civ top edge
        if shield_band:
            s.add(props.box(fw + 0.02, 0.055, 0.15), 'beam', T(0, y0, h0 + 0.3), bevel=0.01, tint=True)    # civ band
        for sx in (-1, 1):
            hinge = (sx * fw / 2, y0)
            cx = hinge[0] + sx * math.cos(math.radians(ang)) * wing / 2
            cy = hinge[1] - math.sin(math.radians(ang)) * wing / 2
            m = T(cx, cy, (h0 + h1) / 2 - 0.04) @ R('Z', sx * ang)
            s.add(_sub(props.box(wing, 0.035, h1 - h0 - 0.08), 4), 'camo', m, bevel=0.012, keys=camo)
            s.add(props.box(wing + 0.02, 0.05, 0.07), 'beam', T(cx, cy, h1 - 0.1) @ R('Z', sx * ang), bevel=0.01, tint=True)
            if shield_band and wing_band:
                s.add(props.box(wing + 0.02, 0.055, 0.15), 'beam', T(cx, cy, h0 + 0.3) @ R('Z', sx * ang), bevel=0.01,
                      tint=True)
            if hinges:   # the wing joined to the plate: a hinge post over the seam and three hinge knuckles
                s.add(props.box(0.09, 0.09, h1 - h0 - 0.04), 'camo', T(hinge[0], hinge[1] - 0.01, (h0 + h1) / 2 - 0.02),
                      bevel=0.015, keys=camo)
                for z in (h0 + 0.1, (h0 + h1) / 2, h1 - 0.16):
                    s.add(props.lathe([(0.0, 0.055), (0.1, 0.055)], seg=12), 'gun_dark',
                          T(hinge[0], hinge[1] - 0.01, z - 0.05), smooth=True)
        # a sighting slot in the plate, dark
        s.add(props.box(0.3, 0.04, 0.08), 'gun_dark', T(0, y0 + 0.005, h1 - 0.2), bevel=0.004)
    else:
        pts = []
        for i in range(13):   # a curved shield wrapping the front and sides of the mount, its edges civ-colored
            a = math.radians(-70 + 140 * i / 12)
            pts.append((0.72 * math.sin(a), 0.25 + 0.55 * math.cos(a) - 0.35))
        rings = [[(x, y, zp + 0.55) for x, y in pts], [(x, y, zp + 1.25) for x, y in pts]]
        sh = props.loft(rings, False, False)
        props.bmesh.ops.solidify(sh, geom=sh.faces[:], thickness=0.03)
        s.add(sh, 'camo', smooth=True, keys=camo)
        s.add(props.tube(rings[1], 0.03, seg=6), 'beam', smooth=True, tint=True)
        for edge in (0, -1):
            s.add(props.tube([rings[0][edge], rings[1][edge]], 0.03, seg=6), 'beam', smooth=True, tint=True)
        if shield_band:
            mid = []
            for i in range(13):
                a = math.radians(-70 + 140 * i / 12)
                mid.append((0.75 * math.sin(a), 0.25 + 0.58 * math.cos(a) - 0.35, zp + 0.95))
            s.add(props.tube(mid, 0.05, seg=8), 'beam', smooth=True, tint=True)
    s.finish()
    root.scale = (k, k, k)
    for i, (pos, yaw) in enumerate(crew or (((0.0, -0.6, zp + 0.2), 0.0), ((0.9, 0.7, 0.0), -120.0))):
        vkit.crew(root, i, tuple(pos), yaw=yaw)   # the seated gunner's hips, down in his seat; the loader
    return root


# ------------------------------------------------------------ TOW Infantry (a two-man team at a launcher)

def tow_team(colors, k=1.0):
    """TOW Infantry, as Civ3 composes it: a TOW launcher (an OD launch tube with end caps on a traversing unit,
    the boxy day sight/tracker and the night sight) on a low tripod; the gunner seated on a crate behind it at
    the sight (crew 0) and the assistant gunner seated on a missile crate at his left, spotting (crew 1).
    (realism infantry, round 2: a shorter, darker tube with end caps, a bigger sight/tracker, the men closer,
    and the team turned 22.5 degrees so no view sees the tube end-on.)"""
    # (round 3: a lighter OD than the uniforms, so the launcher separates from the men)
    c = dict({'olive': '#6c7352', 'olive_dark': '#4a4f36', 'gunmetal': '#3a3c3c', 'tow_cap': '#22241e'}, **colors)
    props.DEFAULTS.setdefault('tow_cap', ('#22241e', 0.8, 0.0, 'leather'))
    root = vkit.root('TOW Infantry')
    f = vkit.part(root, 'launcher', c, ARTILLERY_WEAR)
    zt = 0.62
    for a in (0, 125, 235):   # the tripod's splayed legs with feet
        d = Vector((math.sin(math.radians(a)), math.cos(math.radians(a)), 0))
        foot = d * 0.58
        vkit.rod(f, (0, 0, zt - 0.08), tuple(foot), 0.024, 'gunmetal', seg=8)
        f.add(props.box(0.08, 0.08, 0.02), 'gunmetal', T(foot.x, foot.y, 0.01), bevel=0.004)
    f.add(props.lathe([(0.0, 0.07), (0.12, 0.06)], seg=14), 'gunmetal', T(0, 0, zt - 0.1), smooth=True)   # pedestal
    f.add(props.box(0.22, 0.28, 0.17), 'olive', T(0, 0, zt + 0.08), bevel=0.015)                        # traversing unit
    # the launch tube, raised 7 degrees (so it shows its length even seen end-on), with a muzzle ring, a
    # breech ring and black end caps; the sights ride with it
    y0, y1, zc = -0.85, 1.05, zt + 0.25   # (round 3: longer and thicker, so it reads as a tube, not a stub)
    # (round 4) the tube traversed 22 degrees within the facing and raised 12, so the views along the facing
    # (S and N) still see its length; the muzzle stays forward with the unit's facing
    E = T(0, 0, zt + 0.12) @ R('Z', 22) @ R('X', 12) @ T(0, 0, -(zt + 0.12))
    f.add(props.lathe([(y0, 0.1), (y1, 0.095)], seg=20), 'olive', E @ T(0, 0.0, zc) @ R('X', -90), smooth=True)
    f.add(props.lathe([(0.0, 0.107), (0.07, 0.112)], seg=20), 'olive_dark', E @ T(0, y1 - 0.02, zc) @ R('X', -90), smooth=True)
    f.add(props.lathe([(0.0, 0.112), (0.06, 0.112)], seg=20), 'olive_dark', E @ T(0, y0, zc) @ R('X', -90), smooth=True)
    for y, sgn in ((y1 + 0.05, 1), (y0 - 0.005, -1)):   # the end caps
        f.add(props.lathe([(0.0, 0.0), (0.0, 0.11), (0.025, 0.11), (0.025, 0.0)], seg=20), 'tow_cap',
              E @ T(0, y, zc) @ R('X', -90 * sgn), smooth=True)
    for y in (-0.25, 0.3):   # the tube's clamp bands
        f.add(props.lathe([(0.0, 0.105), (0.03, 0.105)], seg=20), 'olive_dark', E @ T(0, y, zc) @ R('X', -90), smooth=True)
    # the day sight / tracker: a big box on the tube's right, its lens to the front, the eyepiece to the rear
    f.add(props.box(0.2, 0.36, 0.2), 'olive_dark', E @ T(0.2, -0.22, zc - 0.02), bevel=0.015)
    f.add(props.box(0.15, 0.03, 0.12), 'gunmetal', E @ T(0.2, -0.03, zc), bevel=0.006)                       # the lens hood
    f.add(props.lathe([(0.0, 0.035), (0.05, 0.035)], seg=12), 'gunmetal', E @ T(0.2, -0.4, zc) @ R('X', 90), smooth=True)
    # the night sight on the left, smaller, with its own hood
    f.add(props.box(0.15, 0.26, 0.15), 'olive', E @ T(-0.18, -0.2, zc - 0.04), bevel=0.012)
    f.add(props.box(0.12, 0.03, 0.1), 'gunmetal', E @ T(-0.18, -0.06, zc - 0.03), bevel=0.006)
    for x in (-0.06, 0.06):   # handgrips under the traversing unit
        vkit.rod(f, (x, -0.15, zt + 0.05), (x * 1.6, -0.3, zt - 0.05), 0.018, 'gunmetal', seg=6)
    # one missile crate at the gunner's left: the assistant gunner sits on it
    for (x, y, z, yaw) in ((-0.95, -0.32, 0.0, -30),):
        f.add(props.box(0.34, 0.52, 0.28), 'olive', T(x, y, z + 0.14) @ R('Z', yaw), bevel=0.02)
        f.add(props.box(0.36, 0.54, 0.05), 'gunmetal', T(x, y, z + 0.26) @ R('Z', yaw), bevel=0.01)   # the lid
        for dy in (-0.2, 0.2):   # the crate's end bands and handles
            f.add(props.box(0.36, 0.04, 0.3), 'olive_dark', T(x, y, z + 0.14) @ R('Z', yaw) @ T(0, dy, 0), bevel=0.006)
    f.finish()
    root.scale = (k, k, k)
    # the gunner sits on an ammo crate behind the launcher, at the sight
    f2 = vkit.part(root, 'seat', c, ARTILLERY_WEAR)
    f2.add(props.box(0.36, 0.5, 0.3), 'olive', T(0.3, -0.95, 0.15) @ R('Z', 22), bevel=0.02)
    f2.add(props.box(0.38, 0.52, 0.04), 'olive_dark', T(0.3, -0.95, 0.31) @ R('Z', 22), bevel=0.01)
    f2.finish()
    vkit.crew(root, 0, (0.3, -0.93, 0.42), yaw=22.0)    # his hips on the crate, behind the traversed sight
    vkit.crew(root, 1, (-0.95, -0.32, 0.4), yaw=-30.0)   # the assistant gunner, on the missile crate at his left
    # (round 4: the round-3 90-degree team turn undone: it aimed the launcher sideways to the facing)
    return root


# ------------------------------------------------------------ Holy Relic (the Ark, carried on its poles)

def holy_relic(colors, k=1.0):
    """The Holy Relic as Civ3 shows it: a gold ark (a framed chest with
    dark recessed panels between gold pilasters, a moulded rim, feet) under
    the mercy seat with two kneeling cherubim whose wings arch forward over
    it; four gold rings and two long carrying poles; a bearer at each end
    (crew 0 in front, crew 1 behind), both walking forward, poles in hand."""
    if RELIC_CARRIED:   # realism/misc: the Ark on the Levites' shoulders (holy_relic_carried, below)
        return holy_relic_carried(colors, k)
    c = dict({'gold': '#d0a038', 'ark_panel': '#7a4818', 'pole': '#7a6248'}, **colors)
    props.DEFAULTS.setdefault('ark_panel', ('#7a4818', 0.45, 0.6, 'metal'))
    props.DEFAULTS.setdefault('pole', ('#7a6248', 0.7, 0.0, 'wood'))
    props.DEFAULTS.setdefault('gold', ('#d0a038', 0.3, 1.0, 'metal'))
    root = vkit.root('Holy Relic')
    p = vkit.part(root, 'ark', c)
    L, W, H, z0 = 1.5, 0.82, 0.78, 0.42   # the chest: length along the poles, width, height, its floor
    zc = z0 + H / 2
    p.add(props.box(L, W, H), 'ark_panel', T(0, 0, zc) @ R('Z', 90), bevel=0.01)
    # pilasters and rails: gold frames round recessed panels on every face
    for sx in (-1, 1):
        for i in range(6):
            y = -L / 2 + L * i / 5
            p.add(props.box(0.035, 0.05, H * 0.98), 'gold', T(sx * (W / 2 + 0.012), y, zc), bevel=0.008)
        for z in (z0 + 0.04, z0 + H * 0.52, z0 + H - 0.05):
            p.add(props.box(0.03, L * 1.0, 0.045), 'gold', T(sx * (W / 2 + 0.015), 0, z), bevel=0.008)
    for sy in (-1, 1):
        for i in range(4):
            x = -W / 2 + W * i / 3
            p.add(props.box(0.05, 0.035, H * 0.98), 'gold', T(x, sy * (L / 2 + 0.012), zc), bevel=0.008)
        for z in (z0 + 0.04, z0 + H * 0.52, z0 + H - 0.05):
            p.add(props.box(W, 0.03, 0.045), 'gold', T(0, sy * (L / 2 + 0.015), z), bevel=0.008)
    for sx in (-1, 1):   # the feet
        for sy in (-1, 1):
            p.add(props.box(0.09, 0.09, 0.08), 'gold', T(sx * (W / 2 - 0.02), sy * (L / 2 - 0.02), z0 - 0.04), bevel=0.015)
    # the moulded crown rim and the mercy seat lid
    p.add(props.box(W + 0.07, L + 0.07, 0.05), 'gold', T(0, 0, z0 + H + 0.01), bevel=0.015)
    p.add(props.box(W + 0.02, L + 0.02, 0.06), 'gold', T(0, 0, z0 + H + 0.06), bevel=0.012)
    zl = z0 + H + 0.09
    # the two cherubim, kneeling at the ends, facing each other, wings arched over the seat
    for sy in (-1, 1):
        y = sy * (L / 2 - 0.22)
        face = R('Z', 180 if sy > 0 else 0)
        m = T(0, y, zl) @ face @ Matrix.Scale(1.35, 4)
        p.add(props.ellipsoid(0.1, 0.09, 0.08, seg=14), 'gold', m @ T(0, 0, 0.07), smooth=True)          # the knees
        p.add(props.ellipsoid(0.09, 0.08, 0.15, seg=14), 'gold', m @ T(0, -0.02, 0.2) @ R('X', 15), smooth=True)  # the body
        p.add(props.ellipsoid(0.06, 0.06, 0.07, seg=12), 'gold', m @ T(0, 0.04, 0.37), smooth=True)      # the head, bowed
        for sx in (-1, 1):
            wing = [(sx * 0.08, -0.02, 0.28), (sx * 0.2, 0.08, 0.42), (sx * 0.24, 0.24, 0.46), (sx * 0.16, 0.38, 0.38)]
            p.add(props.tube(wing, [0.03, 0.06, 0.06, 0.03], seg=8, scale_xy=(1.0, 0.3)), 'gold', m, smooth=True)
            for j in range(3):   # the long flight feathers under the arch
                a, b = Vector(wing[1 + j % 2]), Vector(wing[2 + j % 2]) + Vector((0, 0.02 * j, -0.12 - 0.04 * j))
                p.add(props.tube([tuple(a), tuple(b)], [0.025, 0.01], seg=6, scale_xy=(1.0, 0.4)), 'gold', m, smooth=True)
    # four rings low on the long sides and the two carrying poles through them
    zp = z0 + 0.3   # at the bearers' hands
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.add(props.lathe([(-0.015, 0.05), (0.015, 0.05)], seg=14), 'gold', T(sx * (W / 2 + 0.06), sy * L * 0.32, zp) @ R('X', 90),
                  smooth=True)
        vkit.rod(p, (sx * (W / 2 + 0.06), -1.85, zp), (sx * (W / 2 + 0.06), 1.85, zp), 0.028, 'pole', seg=10)
        for y in (-1.85, 1.85):   # gold caps on the pole ends
            p.add(props.lathe([(-0.03, 0.032), (0.03, 0.032)], seg=10), 'gold', T(sx * (W / 2 + 0.06), y, zp) @ R('X', 90), smooth=True)
    p.finish()
    root.scale = (k, k, k)
    vkit.crew(root, 0, (0.0, 1.5, 0.0), yaw=0.0)    # the front bearer, the poles behind his hands
    vkit.crew(root, 1, (0.0, -1.5, 0.0), yaw=0.0)   # the rear bearer, the poles ahead of him
    return root


# ------------------------------------------------------------ realism/misc: the Ark on the bearers' shoulders

RELIC_CARRIED = True     # False: the old holy_relic (two bearers, the poles at their hands)
RELIC_POLE_Z = 1.76      # the poles' centre height: on a bearer's shoulder (the crew body's shoulder top)
RELIC_BEARER_X = 0.62    # each bearer's centre from the ark's axis: the pole over his inner shoulder
RELIC_BEARER_Y = 1.3     # the front and rear pairs' distance from the ark's centre


def _cherub(p, m, s=1.0):
    """A kneeling cherub on the mercy seat, facing -Y (toward the other) in m's space: the shins and thighs
    folded under, an upright body, the head bowed, the arms forward with the hands together, and two
    wings rising from the shoulder blades, arching forward and up over the seat."""
    E, Tb = props.ellipsoid, props.tube
    for sx in (-1, 1):   # the shins on the lid and the thighs over them
        p.add(E(0.04 * s, 0.11 * s, 0.035 * s, seg=12), 'gold', m @ T(sx * 0.05 * s, 0.03 * s, 0.035 * s), smooth=True)
        p.add(E(0.045 * s, 0.1 * s, 0.045 * s, seg=12), 'gold', m @ T(sx * 0.05 * s, -0.02 * s, 0.09 * s) @ R('X', -12), smooth=True)
    p.add(E(0.08 * s, 0.06 * s, 0.14 * s, seg=16), 'gold', m @ T(0, 0.05 * s, 0.24 * s) @ R('X', 10), smooth=True)    # the body
    p.add(E(0.075 * s, 0.065 * s, 0.03 * s, seg=14), 'gold', m @ T(0, 0.04 * s, 0.13 * s), smooth=True)               # the girdle
    p.add(Tb([(0, 0.06 * s, 0.36 * s), (0, 0.05 * s, 0.4 * s)], 0.025 * s, seg=8), 'gold', m, smooth=True)            # the neck
    p.add(E(0.05 * s, 0.055 * s, 0.06 * s, seg=14), 'gold', m @ T(0, 0.02 * s, 0.44 * s) @ R('X', 25), smooth=True)   # the head, bowed
    for sx in (-1, 1):   # the arms forward, the hands together before the chest
        p.add(Tb([(sx * 0.08 * s, 0.06 * s, 0.33 * s), (sx * 0.07 * s, -0.02 * s, 0.25 * s), (sx * 0.015 * s, -0.08 * s, 0.28 * s)],
                 [0.024 * s, 0.02 * s, 0.017 * s], seg=8), 'gold', m, smooth=True)
    for sx in (-1, 1):   # the wings: up from the shoulder blades, then forward over the seat; a gap between the tips
        # (a3: low and forward, reaching toward the other cherub over the lid; they rose like ears before)
        path = [(sx * 0.06 * s, 0.1 * s, 0.34 * s), (sx * 0.17 * s, 0.07 * s, 0.45 * s), (sx * 0.21 * s, -0.08 * s, 0.5 * s),
                (sx * 0.18 * s, -0.25 * s, 0.48 * s), (sx * 0.12 * s, -0.38 * s, 0.42 * s)]
        p.add(Tb(path, [0.04 * s, 0.07 * s, 0.08 * s, 0.075 * s, 0.045 * s], seg=10, scale_xy=(0.55, 1.0)), 'gold', m, smooth=True)
        for j, t in enumerate((0.15, 0.35, 0.55)):   # the long flight feathers hanging from the arch
            a = Vector(path[1]).lerp(Vector(path[3]), t + 0.15)
            b = a + Vector((sx * 0.01 * s, -0.03 * s, -0.16 * s + 0.03 * j * s))
            p.add(Tb([tuple(a), tuple(b)], [0.022 * s, 0.006 * s], seg=6, scale_xy=(0.4, 1.0)), 'gold', m, smooth=True)


def holy_relic_carried(colors, k=1.0):
    """The Ark carried as Numbers 4 and 1 Chronicles 15 tell it: on the shoulders of four Levites by its
    poles. A chest overlaid with gold on every face (recessed panels a deeper gold between gold pilasters
    and rails), a moulded crown rim, the mercy seat with two kneeling cherubim facing each other, wings
    arched forward over it with a gap between the tips; four rings high on the long sides and two long
    poles through them at the bearers' shoulder height. Crew 0-1 left (front, rear), 2-3 right."""
    c = dict({'gold': '#d0a038', 'ark_panel': '#a8822c', 'pole': '#6a5038'}, **colors)
    props.DEFAULTS.setdefault('ark_panel', ('#7a4818', 0.45, 0.6, 'metal'))
    props.DEFAULTS.setdefault('pole', ('#7a6248', 0.7, 0.0, 'wood'))
    props.DEFAULTS.setdefault('gold', ('#d0a038', 0.3, 1.0, 'metal'))
    root = vkit.root('Holy Relic')
    p = vkit.part(root, 'ark', c)
    L, W, H = 1.5, 0.82, 0.78
    zp = RELIC_POLE_Z
    z0 = zp - H * 0.72          # the rings high on the sides, so the chest rides between the bearers' heads
    zc = z0 + H / 2
    p.add(props.box(L, W, H), 'ark_panel', T(0, 0, zc) @ R('Z', 90), bevel=0.01)
    for sx in (-1, 1):
        for i in range(6):
            y = -L / 2 + L * i / 5
            p.add(props.box(0.045, 0.06, H * 0.98), 'gold', T(sx * (W / 2 + 0.014), y, zc), bevel=0.01)
        for z in (z0 + 0.04, z0 + H * 0.52, z0 + H - 0.05):
            p.add(props.box(0.035, L * 1.0, 0.055), 'gold', T(sx * (W / 2 + 0.017), 0, z), bevel=0.01)
    for sy in (-1, 1):
        for i in range(4):
            x = -W / 2 + W * i / 3
            p.add(props.box(0.06, 0.045, H * 0.98), 'gold', T(x, sy * (L / 2 + 0.014), zc), bevel=0.01)
        for z in (z0 + 0.04, z0 + H * 0.52, z0 + H - 0.05):
            p.add(props.box(W, 0.035, 0.055), 'gold', T(0, sy * (L / 2 + 0.017), z), bevel=0.01)
    for sx in (-1, 1):   # the feet
        for sy in (-1, 1):
            p.add(props.box(0.09, 0.09, 0.08), 'gold', T(sx * (W / 2 - 0.02), sy * (L / 2 - 0.02), z0 - 0.04), bevel=0.015)
    p.add(props.box(W + 0.07, L + 0.07, 0.05), 'gold', T(0, 0, z0 + H + 0.01), bevel=0.015)
    p.add(props.box(W + 0.02, L + 0.02, 0.06), 'gold', T(0, 0, z0 + H + 0.06), bevel=0.012)
    zl = z0 + H + 0.09
    for sy in (-1, 1):   # the cherubim at the two ends, facing each other
        _cherub(p, T(0, sy * (L / 2 - 0.24), zl) @ R('Z', 0 if sy > 0 else 180), s=1.15)
    xp = W / 2 + 0.06
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.add(props.lathe([(-0.015, 0.05), (0.015, 0.05)], seg=14), 'gold', T(sx * xp, sy * L * 0.32, zp) @ R('X', 90),
                  smooth=True)
        vkit.rod(p, (sx * xp, -1.85, zp), (sx * xp, 1.85, zp), 0.03, 'pole', seg=10)
        for y in (-1.85, 1.85):
            p.add(props.lathe([(-0.03, 0.034), (0.03, 0.034)], seg=10), 'gold', T(sx * xp, y, zp) @ R('X', 90), smooth=True)
    p.finish()
    root.scale = (k, k, k)
    X, Y = RELIC_BEARER_X, RELIC_BEARER_Y
    vkit.crew(root, 0, (-X, Y, 0.0), yaw=0.0)
    vkit.crew(root, 1, (-X, -Y, 0.0), yaw=0.0)
    vkit.crew(root, 2, (X, Y, 0.0), yaw=0.0)
    vkit.crew(root, 3, (X, -Y, 0.0), yaw=0.0)
    return root


BUILDERS = {'Catapult': catapult, 'Cannon': cannon, 'Trebuchet': trebuchet, 'Hwacha': hwacha, 'Artillery': artillery,
            'Flak': flak, 'TOW Infantry': tow_team, 'Holy Relic': holy_relic}


# The realism pass's choices per unit (notes/realism/<Unit>.md): builder keywords and colors laid over the
# builders' own defaults; explicit params (render_vehicles.py SIEGE_PARAMS) override them, colors key by key.
OAK = {'wood': '#8c5426', 'side_rail': '#764424', 'wood_dark': '#4e2e16', 'wheel': '#5e3a1e', 'felloe': '#422814',
       'stone': '#5e574e'}   # oiled, weathered oak: warm, a little saturated (the old pale pine read as a toy)
# batch 2/3 (critics: a rework, not a palette pass): crews at work (crews.CREW_R), working poses and new parts.
# Batch 3 (the last critic round): the values below; batch 2's are in notes/realism/<Unit>.md.
OAK_LIGHT = dict(OAK, wood='#9c6232', side_rail='#86502a')   # a shade lighter: parts the wheels and winch at 1x
REALISM = {
    # cocked but with the arm raised (54 deg) so arm and cup break above the frame; a bigger cup with a pale boulder;
    # the winchman on the left crank, the loader hauling the arm down against the frame
    'Catapult': {'panels': False, 'cup_rim': 'iron', 'arm': 54.0, 'arm_len': 1.7, 'stone': 0.85, 'cup': 1.3, 'cup_level': True,
                 'crew': (((-1.4, -1.3, 0.0), -90.0), ((0.62, -0.45, 0.0), 90.0)), 'crew_realism': True,
                 'colors': dict(OAK_LIGHT, stone='#a49c90')},
    # cocked, readable: the long end of a shorter (2.9 m, was 3.4), tapered dark arm down at the base with the sling and
    # shot lying in the trough; a big dark counterweight box hoisted high at the short end; iron joints, the civ
    # colour in few solid masses (the box's bands, one wide arm band, the rails, the cap); bigger wheels
    'Trebuchet': {'brackets': 'iron', 'piles': ((1.0, -1.0), (-1.0, 1.05)), 'post': 0.12, 'arm': 30.0,
                  'arm_key': 'side_rail', 'arm_long': 2.9, 'taper': True, 'arm_bands': (-0.7,), 'band_w': 0.4,
                  'arm_iron': (0.55, -1.5, -2.5), 'wheel_r': 0.58, 'box': 1.0, 'box_band': 0.24, 'k': 1.0,
                  'sling_r': 0.025,
                  'crew': (((-1.4, -1.35, 0.0), -90.0), ((0.78, -1.25, 0.0), 90.0)), 'crew_realism': True,
                  'colors': dict(OAK, box='#4a301c', box_dark='#24160c')},
    # handles at hip height for the man behind, a staggered sparse rack with bright heads; lighter wheels
    'Hwacha': {'shaft': 0.022, 'fletch': (0.008, 0.2, 0.08), 'head': (0.032, 0.09, 0.012), 'handle_z': 0.85,
               'nx': 7, 'nz': 4, 'stagger': True,
               'crew': (((0.0, -2.12, 0.0), 0.0), ((0.85, -0.75, 0.0), 90.0)), 'crew_realism': True,
               'colors': {'wood': '#a06a36', 'wood_dark': '#5e3a1c', 'wheel': '#a8763e', 'felloe': '#8a5a2c',
                          'steel': '#dfe3e6', 'fletch': '#f4f0e4'}},
    # a shorter bronze barrel, depressed a little; heavy natural-wood cheeks with capsquares; open spokes, civ felloes
    'Cannon': {'spoke_scale': 1.0, 'tint_spokes': False, 'rim_tint': True, 'barrel_k': 1.0, 'cheek': 0.24,
               'capsquares': True, 'k': 0.85, 'trail_tint': False, 'elev': -2.0, 'crew_realism': True,
               'colors': {'gun_iron': '#8a6a3e', 'band': '#5a4220', 'rim': '#3e3c3a', 'wheel': '#7a5434',
                          'carriage': '#6e4a2a'}},
    # a solid civ band across the shield's front, civ through the plates, tyre relief with paler tread; the gunner
    # laying the gun, bent over the breech
    'Artillery': {'disc': 'camo', 'shield_civ': 'both', 'shield_bevel': 0.03, 'relief': True, 'elev': 16.0, 'k': 0.9,
                  'front_band': True, 'tread': 'tread',
                  'crew': (((-0.55, -1.0, 0.0), 15.0),), 'crew_realism': True},
    # emplaced low on flat cross arms with short jack pads, a front plate with angled wings (civ edges and band),
    # thicker barrels; the mount scaled so the crew stands at the Infantry's size
    'Flak': {'disc': 'camo', 'trailer': False, 'rig': (0.22, 0.12), 'shield_band': True, 'elev': 68.0, 'k': 1.35,
             'zp': 0.22, 'gun_r': 1.6, 'shield': 'plate',
             # round 4 (the wings read detached in 2/6): hinge posts over the seams, one civ band (the plate's),
             # smaller dark flash hiders
             'hinges': True, 'wing_band': False, 'hider': 0.7, 'hider_key': 'gun_dark',
             'crew': (((0.0, -0.6, 0.42), 0.0), ((0.9, 0.7, 0.0), -120.0)), 'crew_realism': True},
}


def build(unit, colors=None, **params):
    base = dict(REALISM.get(unit, {}))
    cols = dict(base.pop('colors', {}), **(colors or {}), **params.pop('colors', {}))
    base.update(params)
    realism_crew = base.pop('crew_realism', False)
    root = BUILDERS[unit](cols, **base)
    if realism_crew:
        root['crew_realism'] = True   # crews.populate uses crews.CREW_R
    return root


import numpy_siege  # noqa: E402,F401  (the MDJ engines register their builders and REALISM entries here)
