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

from mathutils import Matrix

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


def catapult(colors, k=0.82, arm=ARM_SHOW):
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
        vkit.beam(f, (x, -L / 2 + 0.15, zr + 0.19), (x, L / 2 - 0.25, zr + 0.19), 0.13 * TH, 0.13 * TH, 'beam',
                  tint=True)
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
        for y0, y1 in ((-1.3, -0.5), (-0.4, 0.65), (0.75, 1.35)):
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
    vkit.beam(a, (0, 0.15, 0), (0, -2.0, 0), 0.25, 0.25, 'wood', bevel=0.025)
    for y in (-0.4, -1.2, -1.85):
        a.add(props.box(0.28, 0.06, 0.28), 'iron', T(0, y, 0), bevel=0.01)
    cup = props.lathe([(0.0, 0.01), (0.04, 0.3), (0.2, 0.38), (0.26, 0.38)], seg=24)
    a.add(cup, 'iron', T(0, -2.1, 0.02), smooth=True)
    a.add(props.lathe([(0.0, 0.39), (0.03, 0.39)], seg=24), 'steel', T(0, -2.1, 0.27), smooth=True)          # pale rim
    a.add(props.lathe([(0.0, 0.2), (0.1, 0.3), (0.17, 0.34)], seg=24), 'beam', T(0, -2.1, 0.09), smooth=True,
          tint=True)                                                                                         # civ lining
    # a rough-hewn boulder: few big facets, knocked about
    stone = props.ellipsoid(0.3, 0.28, 0.26, seg=8)
    props.jitter(stone, 0.06, seed=3)
    a.add(stone, 'stone', T(0, -2.1, 0.32), bevel=0.01)   # a big boulder sitting proud of the cup
    arm_obj = a.finish()
    arm_obj.matrix_parent_inverse = Matrix.Identity(4)
    arm_obj.location = (0, -0.5, zr + 0.22)
    arm_obj.rotation_euler = (math.radians(-arm), 0, 0)

    # the whole engine is compact next to its crew (as the old model)
    root.scale = (k, k, k)
    # crew: a winchman behind, a loader at the right side
    vkit.crew(root, 0, (-0.35 / k, -2.0, 0.0), yaw=0.0)
    vkit.crew(root, 1, (1.05, -0.9, 0.0), yaw=90.0)
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


def cannon(colors, elev=4.0):
    """A Napoleonic field gun: a cast-iron barrel with reinforce rings and a
    cascabel on a two-cheeked carriage with a trail, two big spoked wheels
    with civ-colored spokes, an iron axle; a gunner with a rammer."""
    c = dict(CANNON_COLORS, **colors)
    root = vkit.root('Cannon')
    f = vkit.part(root, 'carriage', c, CANNON_WEAR)
    side = [(0.55, 0.62), (0.3, 1.0), (-0.3, 1.02), (-1.3, 0.26), (-1.38, 0.0), (-1.1, 0.0)]   # a short trail
    for sx in (-1, 1):
        cheek = props.slab([(y, z) for y, z in side], 0.12, 'X')
        f.add(cheek, 'beam', T(sx * 0.3, 0, 0), bevel=0.015, tint=True)   # the trail painted in the civ color
        # iron straps on the cheeks
        for y, z in ((0.35, 0.78), (0.0, 0.95), (-0.45, 0.72), (-0.95, 0.42)):
            f.add(props.box(0.13, 0.05, 0.2), 'hub', T(sx * 0.3, y, z), bevel=0.008)
            for dz in (-0.06, 0.06):
                f.add(props.ellipsoid(0.012, 0.012, 0.012, seg=8), 'hub', T(sx * 0.37, y, z + dz), smooth=True)   # rivets
        # long iron straps along the trail's top edge, bolted down
        for (y0, z0), (y1, z1) in zip(side[1:4], side[2:5]):
            vkit.beam(f, (sx * 0.3, y0, z0 + 0.015), (sx * 0.3, y1, z1 + 0.015), 0.135, 0.035, 'hub', bevel=0.006)
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
    f.finish()

    w = vkit.part(root, 'wheels', c, CANNON_WEAR)
    for sx in (-1, 1):
        vkit.spoked_wheel(w, 0.72, 0.13, 'felloe', 'wheel', 'hub', n_spokes=10, center=(sx * 0.62, 0, 0.72),
                          tire_key='rim', tint_spokes=True, tint_rim=False, spoke_scale=1.6)
        w.add(props.lathe([(0.0, 0.11), (0.08, 0.09), (0.12, 0.04)], seg=16), 'hub', T(sx * 0.7, 0, 0.72) @ R('Y', sx * 90),
              smooth=True)                                                                                  # hub cap
        vkit.rod(w, (sx * 0.83, 0.0, 0.72), (sx * 0.83, 0.0, 0.62), 0.012, 'iron', seg=6)                  # linchpin
    w.finish()

    b = vkit.part(root, 'barrel', c, CANNON_WEAR)
    # breech, base ring, two reinforce rings, the chase and a flared muzzle
    prof = [(0.0, 0.19), (0.05, 0.25), (0.12, 0.25), (0.14, 0.22), (0.32, 0.22), (0.34, 0.25), (0.4, 0.25),
            (0.42, 0.21), (1.0, 0.185), (1.02, 0.215), (1.08, 0.215), (1.1, 0.18), (1.95, 0.135),
            (2.05, 0.15), (2.14, 0.19), (2.24, 0.19), (2.24, 0.075), (2.0, 0.075)]
    prof = [(z * BARREL_K, r) for z, r in prof]   # Civ3's gun is long and slender, well past the wheels
    b.add(props.lathe(prof, seg=24), 'gun_iron', T(0, 0, -0.75), bevel=0.0, smooth=True)
    b.add(props.ellipsoid(0.11, 0.11, 0.11, seg=16), 'gun_iron', T(0, 0, -0.82), smooth=True)   # cascabel
    # the base ring, reinforce rings, chase ring and muzzle swell stand proud in blackened iron
    for z0, r in ((0.02, 0.265), (0.34, 0.262), (1.0, 0.228), (1.55, 0.18), (2.12, 0.205)):
        b.add(props.lathe([(0.0, r), (0.07, r)], seg=24), 'band', T(0, 0, -0.75 + z0 * BARREL_K), smooth=True)
    vkit.rod(b, (-0.3, 0, 0.0), (0.3, 0, 0.0), 0.06, 'iron', seg=12)                       # trunnions
    for sx in (-1, 1):                                                                     # lifting dolphins
        b.add(props.tube([(sx * 0.08, -0.17, -0.12), (sx * 0.1, -0.24, 0.0), (sx * 0.08, -0.17, 0.12)], 0.025, seg=8),
              'gun_iron', T(0, 0, -0.1), smooth=True)
    bo = b.finish()
    bo.matrix_parent_inverse = Matrix.Identity(4)
    bo.location = (0, 0.0, 1.1)
    bo.rotation_euler = (math.radians(-90 + elev), 0, 0)
    # the gunner with his rammer stands behind the right wheel
    vkit.crew(root, 0, (0.55, -1.45, 0.0), yaw=15.0)
    return root


BUILDERS = {'Catapult': catapult, 'Cannon': cannon}


def build(unit, colors=None, **params):
    return BUILDERS[unit](colors or {}, **params)
