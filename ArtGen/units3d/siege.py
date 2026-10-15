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

CATAPULT_COLORS = {'wood': '#b09468', 'wood_dark': '#74583a', 'side_rail': '#8e7250', 'rope': '#a89870',
                   'iron': '#6e6e6c', 'stone': '#8c8884', 'wheel': '#6e4628', 'felloe': '#6e4628'}

ARM_SHOW = 72.0    # the arm's cup end raised 72 degrees toward the rear, cup and stone high, as in Civ3


def catapult(colors, k=0.82, arm=ARM_SHOW):
    """A wheeled torsion catapult (mangonel): two side rails on four spoked
    wheels, an A-frame carrying the padded crossbar, a twisted-rope torsion
    bundle with the throwing arm and its iron cup, a winch at the rear."""
    c = dict(CATAPULT_COLORS, **colors)
    root = vkit.root('Catapult')
    L, W, zr = 3.0, 1.15, 0.5
    f = vkit.part(root, 'frame', c)
    for sx in (-1, 1):
        x = sx * W / 2
        vkit.beam(f, (x, -L / 2, zr), (x, L / 2, zr), 0.17, 0.2, 'side_rail', bevel=0.02)
        vkit.beam(f, (x, -L / 2 + 0.15, zr + 0.19), (x, L / 2 - 0.25, zr + 0.19), 0.13, 0.13, 'wood')
        # the A-frame holding the crossbar
        vkit.beam(f, (x, 0.75, zr + 0.1), (x, 0.38, 1.55), 0.13, 0.13, 'wood')
        vkit.beam(f, (x, -0.25, zr + 0.1), (x, 0.32, 1.52), 0.11, 0.11, 'wood')
        vkit.beam(f, (x, 1.3, zr + 0.1), (x, 0.42, 1.5), 0.1, 0.1, 'wood')
        # iron straps at the joints
        for y, z in ((0.75, zr + 0.12), (-0.25, zr + 0.12), (1.3, zr + 0.12), (0.38, 1.5)):
            f.add(props.box(0.15, 0.06, 0.16), 'iron', T(x, y, z), bevel=0.01)
    for y in (-1.35, -0.45, 0.7):
        vkit.beam(f, (-W / 2, y, zr), (W / 2, y, zr), 0.14, 0.14, 'wood')
    # the front bar and the crossbar are civ-colored in the original
    vkit.beam(f, (-W / 2 - 0.05, 1.4, zr), (W / 2 + 0.05, 1.4, zr), 0.16, 0.16, 'beam', tint=True)
    vkit.beam(f, (-W / 2 - 0.12, 0.36, 1.58), (W / 2 + 0.12, 0.36, 1.58), 0.22, 0.22, 'beam', bevel=0.02, tint=True)
    vkit.beam(f, (-0.28, 0.24, 1.56), (0.28, 0.24, 1.56), 0.12, 0.14, 'rope')          # padding
    # the torsion bundle: twisted rope skeins between the rails
    for i in range(7):
        a = 2 * math.pi * i / 7
        pts = [(-W / 2 + 0.06 + (W - 0.12) * t / 10,
                -0.5 + 0.11 * math.cos(a + 2.4 * t / 10 * math.pi),
                zr + 0.22 + 0.11 * math.sin(a + 2.4 * t / 10 * math.pi)) for t in range(11)]
        f.add(props.tube(pts, 0.035, seg=6), 'rope', smooth=True)
    # the winch at the rear with its iron ratchet wheels and handles
    vkit.rod(f, (-W / 2 - 0.15, -1.3, zr + 0.28), (W / 2 + 0.15, -1.3, zr + 0.28), 0.12, 'wood_dark', seg=12)
    for sx in (-1, 1):
        vkit.ring_x(f, 0.2, 0.05, 0.05, 'iron', center=(sx * (W / 2 + 0.12), -1.3, zr + 0.28), seg=24)
        vkit.beam(f, (sx * (W / 2 + 0.17), -1.3, zr + 0.28), (sx * (W / 2 + 0.17), -1.3, zr + 0.62), 0.05, 0.05, 'iron',
                  bevel=0.008)
    vkit.rod(f, (0, -1.3, zr + 0.3), (0, -0.62, zr + 0.36), 0.018, 'rope', seg=6)   # the winch line to the arm
    f.finish()

    w = vkit.part(root, 'wheels', c)
    for sx in (-1, 1):
        for y in (-1.05, 1.05):
            vkit.spoked_wheel(w, 0.42, 0.14, 'felloe', 'wheel', 'iron', n_spokes=6,
                              center=(sx * (W / 2 + 0.16), y, 0.42), tire_key='iron')
    w.finish()

    # the throwing arm, pivoting in the torsion bundle, its cup end raised to the rear
    a = vkit.part(root, 'arm', c)
    vkit.beam(a, (0, 0.15, 0), (0, -2.0, 0), 0.17, 0.17, 'wood', bevel=0.02)
    for y in (-0.4, -1.2, -1.85):
        a.add(props.box(0.2, 0.05, 0.2), 'iron', T(0, y, 0), bevel=0.01)
    cup = props.lathe([(0.0, 0.01), (0.03, 0.24), (0.17, 0.3), (0.21, 0.3)], seg=20)
    a.add(cup, 'iron', T(0, -2.1, 0.02), smooth=True)
    stone = props.ellipsoid(0.2, 0.2, 0.19, seg=12)
    props.jitter(stone, 0.025, seed=3)
    a.add(stone, 'stone', T(0, -2.1, 0.22), smooth=False)
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

CANNON_COLORS = {'iron': '#56585c', 'carriage': '#3a4048', 'carriage_dark': '#262a30', 'wheel': '#e8e8e8',
                 'felloe': '#4e3c2e', 'rim': '#4a3a30', 'hub': '#2a2a2c'}
props.DEFAULTS.setdefault('rim', ('#4a3a30', 0.55, 1.0, 'metal'))   # the wheels' iron tires


def cannon(colors, elev=4.0):
    """A Napoleonic field gun: a cast-iron barrel with reinforce rings and a
    cascabel on a two-cheeked carriage with a trail, two big spoked wheels
    with civ-colored spokes, an iron axle; a gunner with a rammer."""
    c = dict(CANNON_COLORS, **colors)
    root = vkit.root('Cannon')
    f = vkit.part(root, 'carriage', c)
    side = [(0.55, 0.62), (0.3, 1.0), (-0.3, 1.02), (-2.0, 0.22), (-2.1, 0.0), (-1.7, 0.0)]
    for sx in (-1, 1):
        cheek = props.slab([(y, z) for y, z in side], 0.12, 'X')
        f.add(cheek, 'carriage', T(sx * 0.3, 0, 0), bevel=0.015)
        # iron straps on the cheeks
        for y, z in ((0.2, 0.86), (-0.9, 0.62)):
            f.add(props.box(0.13, 0.05, 0.2), 'hub', T(sx * 0.3, y, z), bevel=0.008)
    vkit.beam(f, (-0.3, -1.95, 0.12), (0.3, -1.95, 0.12), 0.25, 0.2, 'carriage_dark', bevel=0.02)   # trail transom
    vkit.beam(f, (-0.3, -0.4, 0.68), (0.3, -0.4, 0.68), 0.14, 0.14, 'carriage_dark')               # transom
    vkit.rod(f, (-0.78, 0, 0.72), (0.78, 0, 0.72), 0.07, 'hub', seg=12)                            # axle
    vkit.rod(f, (0, -2.0, 0.24), (0, -2.12, 0.5), 0.03, 'hub', seg=8)                              # trail handle
    f.finish()

    w = vkit.part(root, 'wheels', c)
    for sx in (-1, 1):
        vkit.spoked_wheel(w, 0.72, 0.1, 'felloe', 'wheel', 'hub', n_spokes=12, center=(sx * 0.62, 0, 0.72),
                          tire_key='rim', tint_spokes=True)
    w.finish()

    b = vkit.part(root, 'barrel', c)
    prof = [(0.0, 0.2), (0.06, 0.24), (0.3, 0.24), (0.32, 0.2), (1.0, 0.18), (1.04, 0.2), (1.1, 0.18),
            (2.1, 0.13), (2.16, 0.16), (2.24, 0.15), (2.24, 0.07), (2.0, 0.07)]
    b.add(props.lathe(prof, seg=24), 'iron', T(0, 0, -0.75), bevel=0.0, smooth=True)
    b.add(props.ellipsoid(0.11, 0.11, 0.11, seg=16), 'iron', T(0, 0, -0.82), smooth=True)   # cascabel
    vkit.rod(b, (-0.3, 0, 0.0), (0.3, 0, 0.0), 0.06, 'iron', seg=12)                       # trunnions
    bo = b.finish()
    bo.matrix_parent_inverse = Matrix.Identity(4)
    bo.location = (0, 0.0, 1.1)
    bo.rotation_euler = (math.radians(-90 + elev), 0, 0)
    # the gunner with his rammer stands behind the right wheel
    vkit.crew(root, 0, (0.55, -1.75, 0.0), yaw=15.0)
    return root


BUILDERS = {'Catapult': catapult, 'Cannon': cannon}


def build(unit, colors=None, **params):
    return BUILDERS[unit](colors or {}, **params)
