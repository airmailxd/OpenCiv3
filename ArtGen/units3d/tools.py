"""Work tools for the animated Worker (props.make names; prop space as in
props.py: the grip at the origin, the long axis along +Z with the working end
up, a blade's edge or a pick's point toward +Y).

    import tools        # registers the builders in props.BUILDERS
    obj = props.make('pickaxe', colors)
"""
import math

import props
from props import SHAFT_R, T, R, box, ellipsoid, lathe, slab, tube

props.DEFAULTS.setdefault('soil', ('#5a4030', 0.9, 0.0, 'stone'))
props.DEFAULTS.setdefault('leaf', ('#4e6a2e', 0.7, 0.0, None))
props.DEFAULTS.setdefault('bark', ('#5a4430', 0.8, 0.0, 'wood'))


def pickaxe(p, length=0.9, scale=1.0):
    """A pickaxe: an ash haft and a curved iron head across its top, a point
    toward +Y and a flat chisel end toward -Y, on a socket."""
    L = length * scale
    p.add(tube([(0, 0, -0.12), (0, 0, L)], [SHAFT_R + 0.002, SHAFT_R + 0.001], seg=10), 'wood', bevel=0.002, smooth=True)
    arc = [(0.0, 0.3 * u, L + 0.03 - 0.07 * u * u) for u in [i / 6 - 1 for i in range(13)]]
    radii = [max(0.006, 0.026 * (1 - abs(u) ** 1.6)) for u in [i / 6 - 1 for i in range(13)]]
    p.add(tube(arc, radii, seg=10, scale_xy=(0.8, 1.0)), 'iron', smooth=True)
    p.add(box(0.05, 0.012, 0.03), 'iron', T(0, -0.29, L - 0.035), bevel=0.003)     # the chisel end
    p.add(lathe([(0.0, 0.032), (0.07, 0.032)], seg=12), 'iron', T(0, 0, L - 0.04), smooth=True)   # the eye


def felling_axe(p, length=0.85, scale=1.0):
    """A woodsman's axe: a long haft, a wedge head with a flared edge toward
    +Y and a square poll behind."""
    L = length * scale
    p.add(tube([(0, 0, -0.12), (0, 0, L)], [SHAFT_R + 0.001, SHAFT_R], seg=10), 'wood', bevel=0.002, smooth=True)
    blade = slab([(-0.05, -0.03), (0.04, -0.03), (0.15, -0.07), (0.17, -0.05), (0.17, 0.05), (0.15, 0.07),
                  (0.04, 0.03), (-0.05, 0.03)], 0.03, 'X')
    for v in blade.verts:   # thin toward the edge
        v.co.x *= max(0.15, 1.0 - 0.85 * max(0.0, v.co.y) / 0.17)
    p.add(blade, 'steel', T(0, 0, L - 0.04), bevel=0.002)


def machete(p, length=0.62, scale=1.0):
    """A machete: a wooden grip with brass rivets and a long single-edged
    blade, widening toward the tip, its edge toward +Y."""
    L = length * scale
    p.add(tube([(0, 0, -0.12), (0, 0, 0.03)], [0.016, 0.018], seg=10, scale_xy=(0.7, 1.0)), 'wood', smooth=True)
    for z in (-0.08, -0.02):
        p.add(ellipsoid(0.019, 0.005, 0.005, seg=8), 'bronze', T(0, 0, z), smooth=True)
    blade = slab([(-0.012, 0.02), (0.018, 0.02), (0.05, L * 0.7), (0.045, L), (0.0, L + 0.02), (-0.015, L * 0.75)],
                 0.004, 'X')
    p.add(blade, 'steel', bevel=0.001)


def hoe(p, length=1.25, scale=1.0):
    """A digging hoe: a long haft and a broad iron blade set at a right angle
    at its top, reaching toward +Y."""
    L = length * scale
    p.add(tube([(0, 0, -0.2), (0, 0, L)], [SHAFT_R + 0.001, SHAFT_R], seg=10), 'wood', bevel=0.002, smooth=True)
    p.add(lathe([(0.0, 0.03), (0.08, 0.03)], seg=12), 'iron', T(0, 0, L - 0.05), smooth=True)
    p.add(tube([(0, 0, L), (0, 0.06, L + 0.01)], 0.014, seg=8), 'iron', smooth=True)       # the neck
    p.add(box(0.17, 0.2, 0.008), 'steel', T(0, 0.15, L - 0.005) @ R('X', 20), bevel=0.002)


def mallet(p, length=0.5, scale=1.0):
    """A heavy wooden mallet: a short handle and a round head bound in iron
    bands, its faces along Y."""
    L = length * scale
    p.add(tube([(0, 0, -0.1), (0, 0, L)], [SHAFT_R, SHAFT_R], seg=10), 'wood', bevel=0.002, smooth=True)
    p.add(tube([(0, -0.11, L), (0, 0.11, L)], 0.055, seg=14), 'wood', bevel=0.003, smooth=True)
    for y in (-0.085, 0.085):
        p.add(tube([(0, y - 0.012, L), (0, y + 0.012, L)], 0.058, seg=14), 'iron', smooth=True)


def seedling(p, scale=1.0):
    """A sapling with its root ball, carried in both hands: the ball at the
    origin, the stem up +Z with a few leafy sprays."""
    s = scale
    p.add(ellipsoid(0.09 * s, 0.09 * s, 0.075 * s, seg=14), 'soil', smooth=True)
    p.add(tube([(0, 0, 0.05 * s), (0.01 * s, 0.0, 0.42 * s)], [0.012 * s, 0.006 * s], seg=8), 'bark', smooth=True)
    for i, (a, z, r) in enumerate([(0, 0.44, 0.07), (110, 0.34, 0.06), (230, 0.27, 0.055), (40, 0.22, 0.05)]):
        x, y = math.cos(math.radians(a)) * 0.04 * s, math.sin(math.radians(a)) * 0.04 * s
        p.add(ellipsoid(r * s, r * 0.8 * s, r * 0.6 * s, seg=10), 'leaf', T(x, y, z * s), smooth=True)


def mattock(p, length=1.2, scale=1.0):
    """A grub hoe: a long haft and a short heavy blade at a right angle,
    reaching toward +Y, with a narrow pick behind."""
    L = length * scale
    p.add(tube([(0, 0, -0.2), (0, 0, L)], [SHAFT_R + 0.002, SHAFT_R + 0.001], seg=10), 'wood', bevel=0.002, smooth=True)
    p.add(lathe([(0.0, 0.032), (0.08, 0.032)], seg=12), 'iron', T(0, 0, L - 0.05), smooth=True)
    p.add(box(0.1, 0.17, 0.016), 'iron', T(0, 0.1, L - 0.02) @ R('X', 25), bevel=0.003)
    p.add(tube([(0, -0.02, L - 0.01), (0, -0.16, L - 0.05)], [0.016, 0.005], seg=8), 'iron', smooth=True)


def stake(p, length=1.1, scale=1.0):
    """A sharpened wooden stake standing on its point: the point at the
    origin, the flat top at +Z."""
    L = length * scale
    p.add(tube([(0, 0, 0.0), (0, 0, 0.12)], [0.004, 0.035], seg=10), 'wood', smooth=True)
    p.add(tube([(0, 0, 0.12), (0, 0, L)], [0.035, 0.034], seg=10), 'bark', bevel=0.003, smooth=True)
    p.add(tube([(0, 0, L - 0.08), (0, 0, L - 0.05)], 0.037, seg=10), 'rope', smooth=True)


def seed_basket(p, scale=1.0):
    """A small woven seed basket carried at the waist: the handle grip at the
    origin, the basket hanging below (-Z), seed heaped in it."""
    s = scale
    p.add(lathe([(-0.2 * s, 0.0), (-0.2 * s, 0.07 * s), (-0.1 * s, 0.11 * s), (-0.07 * s, 0.115 * s)], seg=16),
          'rope', smooth=True)
    p.add(ellipsoid(0.1 * s, 0.1 * s, 0.025 * s, seg=12), 'soil', T(0, 0, -0.08 * s), smooth=True)
    arc = [(0.1 * s * math.cos(math.pi * t), 0.0, -0.07 * s + 0.08 * s * math.sin(math.pi * t)) for t in [i / 10 for i in range(11)]]
    p.add(tube(arc, 0.008 * s, seg=6), 'rope', smooth=True)


def seed_spray(p, count=26, spread=0.35, scale=1.0, seed=7):
    """A loose spray of seed in the air: small grains scattered about the origin."""
    import random
    rnd = random.Random(seed)
    for _ in range(count):
        x, y, z = (rnd.uniform(-spread, spread) * scale, rnd.uniform(-spread, spread) * 0.6 * scale,
                   rnd.uniform(-spread, spread) * 0.5 * scale)
        p.add(ellipsoid(0.012, 0.012, 0.009, seg=6), 'soil', T(x, y, z))


for _name, _fn in (('mattock', mattock), ('stake', stake), ('seed_basket', seed_basket), ('seed_spray', seed_spray),
                   ('pickaxe', pickaxe), ('felling_axe', felling_axe), ('machete', machete), ('hoe', hoe),
                   ('mallet', mallet), ('seedling', seedling)):
    props.BUILDERS.setdefault(_name, _fn)
