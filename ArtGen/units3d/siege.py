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
CATAPULT_COLORS = {'wood': '#6a4c2e', 'wood_dark': '#3b2716', 'side_rail': '#4e3824', 'rope': '#8a7350',
                   'iron': '#2e2b28', 'stone': '#7a7670', 'wheel': '#4e3824', 'felloe': '#3b2716'}
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
    # a fat winch drum wound with rope
    vkit.rod(f, (-0.3, -1.3, zr + 0.28), (0.3, -1.3, zr + 0.28), 0.17, 'wood_dark', seg=16)
    coil = [(-0.28 + 0.56 * i / 120, -1.3 + 0.185 * math.cos(i * 0.6), zr + 0.28 + 0.185 * math.sin(i * 0.6))
            for i in range(121)]
    f.add(props.tube(coil, 0.016, seg=5), 'rope', smooth=True)
    for sx in (-1, 1):
        vkit.ring_x(f, 0.2, 0.05, 0.05, 'iron', center=(sx * (W / 2 + 0.12), -1.3, zr + 0.28), seg=24)
        vkit.beam(f, (sx * (W / 2 + 0.17), -1.3, zr + 0.28), (sx * (W / 2 + 0.17), -1.3, zr + 0.62), 0.05, 0.05, 'iron',
                  bevel=0.008)
    vkit.rod(f, (0, -1.3, zr + 0.3), (0, -0.62, zr + 0.36), 0.018, 'rope', seg=6)   # the winch line to the arm
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
    cup = props.lathe([(0.0, 0.01), (0.03, 0.24), (0.17, 0.3), (0.21, 0.3)], seg=20)
    a.add(cup, 'iron', T(0, -2.1, 0.02), smooth=True)
    # a rough-hewn boulder: few big facets, knocked about
    stone = props.ellipsoid(0.22, 0.2, 0.19, seg=8)
    props.jitter(stone, 0.045, seed=3)
    a.add(stone, 'stone', T(0, -2.1, 0.22), bevel=0.01)
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
CANNON_COLORS = {'gun_iron': '#3b3e40', 'iron': '#34363a', 'carriage': '#4a3a2a', 'carriage_dark': '#33281c', 'wheel': '#e8e8e8',
                 'felloe': '#3e2e22', 'rim': '#2e2a26', 'hub': '#222224'}
CANNON_WEAR = {'gun_iron': {'grime': 0.35, 'soot': 0.4, 'rust': 0.15, 'edges': 0.5}, 'iron': {'grime': 0.5, 'soot': 0.6, 'rust': 0.2}, 'carriage': {'grime': 0.6, 'soot': 0.3},
               'carriage_dark': {'grime': 0.6}, 'felloe': {'grime': 0.65}, 'rim': {'grime': 0.5, 'rust': 0.3},
               'hub': {'grime': 0.5, 'rust': 0.25}}
props.DEFAULTS.setdefault('rim', ('#4a3a30', 0.55, 1.0, 'metal'))   # the wheels' iron tires
props.DEFAULTS.setdefault('gun_iron', ('#3b3e40', 0.5, 1.0, 'metal'))   # the barrel: dark, matte cast iron


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
        f.add(cheek, 'carriage', T(sx * 0.3, 0, 0), bevel=0.015)
        # iron straps on the cheeks
        for y, z in ((0.2, 0.86), (-0.7, 0.62)):
            f.add(props.box(0.13, 0.05, 0.2), 'hub', T(sx * 0.3, y, z), bevel=0.008)
    vkit.beam(f, (-0.3, -1.28, 0.12), (0.3, -1.28, 0.12), 0.25, 0.2, 'carriage_dark', bevel=0.02)   # trail transom
    f.add(props.box(0.4, 0.05, 0.22), 'hub', T(0, -1.4, 0.04) @ R('X', -20), bevel=0.01)          # trail spade
    vkit.beam(f, (-0.3, -0.4, 0.68), (0.3, -0.4, 0.68), 0.14, 0.14, 'carriage_dark')               # transom
    vkit.rod(f, (-0.78, 0, 0.72), (0.78, 0, 0.72), 0.07, 'hub', seg=12)                            # axle
    vkit.rod(f, (0, -1.34, 0.24), (0, -1.46, 0.5), 0.03, 'hub', seg=8)                             # trail handle
    f.finish()

    w = vkit.part(root, 'wheels', c, CANNON_WEAR)
    for sx in (-1, 1):
        vkit.spoked_wheel(w, 0.72, 0.13, 'wheel', 'wheel', 'hub', n_spokes=10, center=(sx * 0.62, 0, 0.72),
                          tire_key='rim', tint_spokes=True, tint_rim=True, spoke_scale=1.6)
    w.finish()

    b = vkit.part(root, 'barrel', c, CANNON_WEAR)
    # breech, base ring, two reinforce rings, the chase and a flared muzzle
    prof = [(0.0, 0.19), (0.05, 0.25), (0.12, 0.25), (0.14, 0.22), (0.32, 0.22), (0.34, 0.25), (0.4, 0.25),
            (0.42, 0.21), (1.0, 0.185), (1.02, 0.215), (1.08, 0.215), (1.1, 0.18), (1.95, 0.135),
            (2.05, 0.15), (2.14, 0.19), (2.24, 0.19), (2.24, 0.075), (2.0, 0.075)]
    b.add(props.lathe(prof, seg=24), 'gun_iron', T(0, 0, -0.75), bevel=0.0, smooth=True)
    b.add(props.ellipsoid(0.11, 0.11, 0.11, seg=16), 'gun_iron', T(0, 0, -0.82), smooth=True)   # cascabel
    vkit.rod(b, (-0.3, 0, 0.0), (0.3, 0, 0.0), 0.06, 'iron', seg=12)                       # trunnions
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
