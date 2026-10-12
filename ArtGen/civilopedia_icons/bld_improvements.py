"""City improvement icons (registered with improvements.bicon). The camera
sees the +x (lower right) and +y (lower left) faces: fronts face those ways,
and tall parts stand at the back (-x, -y)."""
import math

import numpy as np

import buildings as BD
import geom as G
from bld3d import lin, plinth, water_plinth, PAVE, GRASS
from improvements import (bicon, P, nb, box, cyl, cone, sphere, seg_box, beam, flag, tree, columns,
                          PLAIN, STUCCO, WINDOWS, ROOF_TILE, THATCH, SLATE, GLASS, BRICK, METAL, DOME, STONE, WATER,
                          FOLIAGE, WOOD, GOLD, RUBBLE, PAVING)

MARBLE = P['marble']
CONCRETE = lin('c6c2b8')
DARK = lin('4a4a4e')
STEEL = lin('9a9ea2')
RED = lin('b0473c')


def portico(sc, x0, y0, x1, y1, z0, h, n, axis='x', col=MARBLE, roof=None):
    """Columns under a pediment along the +x or +y face of a rectangle."""
    b = nb(sc)
    if axis == 'x':
        G.column_row(sc, x1, y0, x1, y1, z0, z0 + h, 1.0, n, col, PLAIN, b)
    else:
        G.column_row(sc, x0, y1, x1, y1, z0, z0 + h, 1.0, n, col, PLAIN, b)
    return b


@bicon('BLDG_Palace')
def _(sc, rng):
    plinth(sc, -34, -30, 34, 30)
    wall, roof = lin('e2d6b6'), lin('5f6e78')
    b = nb(sc, wspace=6, floor=10)
    G.box(sc, -14, -14, 14, 14, 0, 18, wall, WINDOWS, b)
    G.lathe(sc, 0, 0, [(9, 18), (9, 22)], wall, STUCCO, b, seg=28)
    G.dome(sc, 0, 0, 9, 22, lin('7a96a0'), DOME, b, h=10, seg=32)
    G.lathe(sc, 0, 0, [(1.2, 32), (1.2, 35), (0.0, 37)], P['gold'], GOLD, b, seg=8)
    for (x0, y0, x1, y1) in ((-30, -10, -14, 10), (14, -10, 30, 10)):
        b2 = nb(sc, wspace=6, floor=10)
        G.box(sc, x0, y0, x1, y1, 0, 12, wall, WINDOWS, b2)
        G.hip(sc, x0, y0, x1, y1, 12, 5, roof, SLATE, b2, over=0.8)
    G.column_row(sc, 15, -10, 15, 10, 0, 14, 1.0, 6, MARBLE, PLAIN, nb(sc))
    G.box(sc, 14, -11, 17, 11, 14, 16, MARBLE, PLAIN, nb(sc))
    BD.fountain(sc, 24, 20, 5, P)


@bicon('BLDG_Barracks')
def _(sc, rng):
    plinth(sc, -26, -22, 26, 22)
    BD.gable_house(sc, (-20, -16, 14, -2), 15, P, rng, roofs=[lin('6a6a5e')], walls=[lin('b8a888')], roofmat=SLATE)
    BD.gable_house(sc, (-20, 2, -8, 18), 12, P, rng, roofs=[lin('6a6a5e')], walls=[lin('b8a888')], roofmat=SLATE)
    G.box(sc, -4, 2, 22, 18, 0, 0.3, lin('c8bea4'), PAVING, nb(sc))
    for x in range(-1, 21, 5):
        for y in (6, 11):
            G.box(sc, x, y, x + 2, y + 2, 0, 1.2, lin('6a5038'), WOOD, nb(sc))
    flag(sc, 18, -12, 0, 26, RED)


@bicon('BLDG_Granary')
def _(sc, rng):
    plinth(sc, -24, -22, 24, 22)
    for (x, y, r, h) in ((-8, -7, 9, 16), (11, -3, 7.5, 13), (-2, 12, 6.5, 11)):
        b = nb(sc)
        G.lathe(sc, x, y, [(r, 0), (r, h)], lin('c9ae7c'), STUCCO, b, seg=20)
        G.lathe(sc, x, y, [(r + 1.5, h), (0.0, h + r * 0.9)], lin('b59c58'), THATCH, b, seg=20)
        G.box(sc, x + r - 0.5, y - 1.5, x + r + 0.3, y + 1.5, 0, 4, lin('5a4430'), WOOD, b)
    for i in range(5):
        sphere(sc, 13 + (i % 3) * 3.6, 12 + (i // 3) * 3.4, 1.6, 1.9, lin('d8c8a0'))


@bicon('BLDG_Temple')
def _(sc, rng):
    plinth(sc, -24, -18, 24, 18)
    BD.temple(sc, (-17, -11, 17, 11), 24, P, rng)


@bicon('BLDG_Marketplace')
def _(sc, rng):
    plinth(sc, -26, -26, 26, 26, top=lin('c2b48e'), mat=PAVING)
    cols = [lin('b8463c'), lin('d8b84a'), lin('4a6fa5'), lin('5b8a4a'), lin('c87a3a'), lin('e0d8c8')]
    k = 0
    for gx in (-14, 2):
        for gy in (-14, 2):
            x0, y0 = gx, gy
            b = nb(sc)
            for (px, py) in ((x0, y0), (x0 + 11, y0), (x0, y0 + 11), (x0 + 11, y0 + 11)):
                G.box(sc, px, py, px + 0.8, py + 0.8, 0, 8, lin('6a5038'), WOOD, b)
            G.box(sc, x0, y0 + 2, x0 + 11.8, y0 + 9, 0, 3.2, lin('8a6a48'), WOOD, b)
            c = cols[k % len(cols)]
            sc.quad((x0 - 1, y0 - 1, 9.5), (x0 + 13, y0 - 1, 9.5), (x0 + 13, y0 + 13, 7.0), (x0 - 1, y0 + 13, 7.0), c, PLAIN, b)
            for i in range(3):
                sphere(sc, x0 + 3 + i * 3, y0 + 5.5, 4.2, 1.2, cols[(k + i + 1) % len(cols)])
            k += 2
    for (x, y) in ((18, -18), (20, -12), (-20, 18)):
        G.box(sc, x - 1.5, y - 1.5, x + 1.5, y + 1.5, 0, 3, lin('a8885a'), WOOD, nb(sc))


@bicon('BLDG_Library')
def _(sc, rng):
    plinth(sc, -28, -22, 28, 22)
    wall = lin('d8cba8')
    b = nb(sc, wspace=6, floor=12, arch=6.0, archh=9.0)
    G.box(sc, -20, -14, 16, 12, 0, 20, wall, STONE, b)
    G.parapet(sc, -20, -14, 16, 12, 20, 1.6, wall, STONE, b)
    G.box(sc, 16, -8, 22, 6, 0, 1.5, MARBLE, PLAIN, nb(sc))
    G.column_row(sc, 20, -7, 20, 5, 1.5, 16, 1.0, 4, MARBLE, PLAIN, nb(sc))
    bp = nb(sc)
    G.box(sc, 15, -8, 21.5, 6, 16, 18, MARBLE, PLAIN, bp)
    G.gable(sc, 15, -8, 21.5, 6, 18, 5, MARBLE, PLAIN, bp, axis='x', over=0.5)
    # an open book on a stand by the door
    G.box(sc, 22, 14, 24, 16, 0, 3, lin('6a5038'), WOOD, nb(sc))
    sc.quad((21, 13, 3.5), (25, 13, 4.5), (25, 17, 4.5), (21, 17, 3.5), lin('f0ece0'), PLAIN, nb(sc))


@bicon('BLDG_Courthouse')
def _(sc, rng):
    plinth(sc, -26, -22, 26, 22)
    wall = lin('d8cdb0')
    b = nb(sc, wspace=6, floor=11)
    G.box(sc, -18, -14, 14, 14, 0, 16, wall, WINDOWS, b)
    G.hip(sc, -18, -14, 14, 14, 16, 5, lin('6a5a4a'), SLATE, b, over=0.8)
    G.box(sc, 14, -7, 19, 7, 0, 1.2, MARBLE, PLAIN, nb(sc))
    G.column_row(sc, 18, -6, 18, 6, 1.2, 14, 0.9, 4, MARBLE, PLAIN, nb(sc))
    bp = nb(sc)
    G.box(sc, 13, -7, 19.5, 7, 14, 16, MARBLE, PLAIN, bp)
    G.gable(sc, 13, -7, 19.5, 7, 16, 4, MARBLE, PLAIN, bp, axis='x', over=0.5)
    # a clock tower
    bt = nb(sc)
    G.box(sc, -8, -6, 0, 2, 20, 32, wall, STONE, bt)
    G.hip(sc, -8, -6, 0, 2, 32, 7, lin('4f5a60'), SLATE, bt, over=0.5, ridge=0)
    sphere(sc, 0.2, -2, 27, 2.4, lin('f0ecdf'))


@bicon('BLDG_Walls')
def _(sc, rng):
    plinth(sc, -32, -32, 32, 32)
    stone = lin('b4ab98')
    b = nb(sc, wspace=12, floor=30)
    t = 4.0
    h = 14
    G.box(sc, -26, -26, 26, -26 + t, 0, h, stone, STONE, b)
    G.box(sc, -26, -26, -26 + t, 26, 0, h, stone, STONE, b)
    G.crenellate(sc, -26, -26 + t * 0.6, 26, -26 + t, h, 2.4, stone, STONE, b, step=4.0, w=2.0, t=1.6, sides='X')
    G.crenellate(sc, -26 + t * 0.6, -26, -26 + t, 26, h, 2.4, stone, STONE, b, step=4.0, w=2.0, t=1.6, sides='Y')
    for (cx, cy) in ((-24, -24), (26, -24), (-24, 26)):
        BD.round_tower(sc, (cx - 6, cy - 6, cx + 6, cy + 6), 22, dict(P, stone=[stone]), rng)
    # a gate in the left wall
    G.box(sc, -27, 2, -21, 12, 0, 20, stone, STONE, nb(sc))
    G.box(sc, -21.2, 4, -20.9, 10, 0, 9, lin('4a3a2a'), WOOD, nb(sc))


@bicon('BLDG_Aqueduct')
def _(sc, rng):
    plinth(sc, -36, -12, 36, 12)
    stone = lin('c2b498')
    b = nb(sc, arch=9.0, archh=12.0)
    G.box(sc, -34, -3, 34, 3, 0, 22, stone, STONE, b, top=lin('5f8ea8'), topmat=WATER)
    G.box(sc, -34, -3.4, 34, -2.6, 22, 23.5, stone, STONE, b)
    G.box(sc, -34, 2.6, 34, 3.4, 22, 23.5, stone, STONE, b)


@bicon('BLDG_Bank')
def _(sc, rng):
    plinth(sc, -26, -22, 26, 22)
    wall = lin('cfc6b2')
    b = nb(sc, wspace=6, floor=12)
    G.box(sc, -18, -15, 12, 15, 0, 22, wall, STONE, b)
    G.parapet(sc, -18, -15, 12, 15, 22, 2, wall, STONE, b)
    G.box(sc, 12, -12, 20, 12, 0, 2, MARBLE, PLAIN, nb(sc))
    G.column_row(sc, 18, -11, 18, 11, 2, 18, 1.3, 5, MARBLE, PLAIN, nb(sc))
    bp = nb(sc)
    G.box(sc, 11, -12.5, 20, 12.5, 18, 21, MARBLE, PLAIN, bp)
    G.gable(sc, 11, -12.5, 20, 12.5, 21, 6, MARBLE, PLAIN, bp, axis='x', over=0.5)
    # gold on the steps
    for i in range(3):
        G.box(sc, 22, 14 - i * 3.2, 25, 16 - i * 3.2, 0, 1.4, P['gold'], GOLD, nb(sc))


@bicon('BLDG_Cathedral')
def _(sc, rng):
    plinth(sc, -30, -22, 30, 22)
    stone, roof = lin('c8bea8'), lin('5f6a70')
    b = nb(sc, wspace=6, floor=14, arch=5.0, archh=10.0)
    G.box(sc, -18, -9, 26, 9, 0, 22, stone, STONE, b)
    G.gable(sc, -18, -9, 26, 9, 22, 12, roof, SLATE, b, axis='x', over=1.0, gable_col=stone, gable_mat=STONE)
    G.box(sc, -2, -16, 8, 16, 0, 20, stone, STONE, b)
    G.gable(sc, -2, -16, 8, 16, 20, 9, roof, SLATE, b, axis='y', over=1.0, gable_col=stone, gable_mat=STONE)
    for y in (-14, 4):
        bt = nb(sc)
        G.box(sc, -28, y, -18, y + 10, 0, 40, stone, STONE, bt)
        G.lathe(sc, -23, y + 5, [(6.5, 40), (0.0, 56)], roof, SLATE, bt, seg=4)
    sphere(sc, 26.2, 0, 26, 3.2, lin('5a6f9a'))


@bicon('BLDG_University')
def _(sc, rng):
    plinth(sc, -30, -26, 30, 26)
    wall, roof = lin('c79a7a'), lin('5a5a5e')
    for (r, ax) in (((-24, -20, 22, -8), 'x'), ((-24, -8, -12, 20), 'y')):
        b = nb(sc, wspace=5, floor=9)
        G.box(sc, *r, 0, 14, wall, WINDOWS, b)
        G.gable(sc, *r, 14, 6, roof, SLATE, b, axis=ax, over=0.8)
    G.box(sc, -12, -8, 22, 20, 0, 0.3, GRASS, PLAIN, nb(sc))
    BD.rotunda(sc, (0, 2, 16, 18), 24, dict(P, marble=lin('e2dccb'), dome_big=[lin('7c8c94')]), rng)
    bt = nb(sc)
    G.box(sc, -24, -20, -14, -10, 14, 30, wall, BRICK, bt)
    G.crenellate(sc, -24, -20, -14, -10, 30, 2.5, wall, BRICK, bt, step=3.0, w=1.6, t=1.2)


@bicon('BLDG_Colosseum')
def _(sc, rng):
    plinth(sc, -32, -28, 32, 28)
    stone = lin('cbb894')
    b = nb(sc, arch=4.5, archh=5.5, rcx=0, rcy=0)
    for (z0, z1, k) in ((0, 7, 1.0), (7, 14, 1.0), (14, 19, 0.98)):
        G.lathe(sc, 0, 0, [(25 * k, z0), (25 * k, z1)], stone, STONE, b, seg=40, sx=1.0, sy=0.82, cap=False)
    G.lathe(sc, 0, 0, [(25, 19), (18, 19), (12, 6), (0.0, 6)], lin('b4a07c'), STONE, nb(sc), seg=40, sx=1.0, sy=0.82)
    G.lathe(sc, 0, 0, [(12, 6.1), (0.0, 6.1)], lin('d8c48c'), PLAIN, nb(sc), seg=40, sx=1.0, sy=0.82)


@bicon('BLDG_Factory')
def _(sc, rng):
    plinth(sc, -28, -24, 28, 24)
    BD.factory(sc, (-22, -16, 18, 16), 46, dict(P, factory_walls=[lin('a85a44')]), rng, chimneys=2)
    for i in range(3):
        G.box(sc, 20, -10 + i * 6, 24, -6 + i * 6, 0, 4, lin('8a6a48'), WOOD, nb(sc))


@bicon('BLDG_Manufacturing_Plant')
def _(sc, rng):
    plinth(sc, -34, -30, 34, 30, top=lin('a8a49a'), mat=PAVING)
    wall, roof = lin('a8aeb2'), lin('6c7074')
    b = nb(sc, wspace=5, floor=8)
    G.box(sc, -28, -24, 20, 18, 0, 14, wall, WINDOWS, b)
    for i in range(6):
        y0 = -24 + i * 7
        G.gable(sc, -28, y0, 20, y0 + 7, 14, 4, roof, SLATE, b, axis='x', over=0.3, gable_col=wall, gable_mat=STUCCO)
    for x in (-22, -14):
        BD.chimney(sc, x, -26, 2.6, 0, 40, lin('8a8a86'), nb(sc))
    BD.gas_tank(sc, (20, 4, 30, 14), 12, P, rng, sphere=False)
    # a gantry crane
    bc = nb(sc)
    for y in (-26, 24):
        G.box(sc, 24, y, 26, y + 2, 0, 22, lin('d8a030'), METAL, bc)
    G.box(sc, 24, -26, 26, 26, 22, 24, lin('d8a030'), METAL, bc)


@bicon('BLDG_Recycling_Center')
def _(sc, rng):
    plinth(sc, -28, -24, 28, 24, top=lin('a8a49a'), mat=PAVING)
    b = nb(sc, wspace=5, floor=8)
    G.box(sc, -22, -18, 8, 6, 0, 14, lin('6f9a68'), WINDOWS, b, top=lin('5a7a58'))
    G.box(sc, -20, -16, -12, -8, 14, 18, lin('5a7a58'), PLAIN, b)
    cols = [lin('3f7fb0'), lin('d8b84a'), lin('5b8a4a')]
    for i, c in enumerate(cols):
        bb = nb(sc)
        G.box(sc, 12, -14 + i * 9, 20, -8 + i * 9, 0, 6, c, METAL, bb)
        G.box(sc, 11.5, -14.5 + i * 9, 20.5, -7.5 + i * 9, 6, 6.8, (c[0] * 0.8, c[1] * 0.8, c[2] * 0.8), METAL, bb)
    for i in range(10):
        x, y = rng.uniform(-18, 6), rng.uniform(10, 20)
        G.box(sc, x, y, x + 2.5, y + 2.5, 0, rng.uniform(1, 3), cols[i % 3], RUBBLE, nb(sc))


@bicon('BLDG_Coal_Plant')
def _(sc, rng):
    plinth(sc, -32, -28, 32, 28, top=lin('8a8478'), mat=PAVING)
    b = nb(sc, wspace=5, floor=8)
    G.box(sc, -24, -18, 10, 10, 0, 22, lin('8e6656'), BRICK, b)
    G.gable(sc, -24, -18, 10, 10, 22, 6, lin('5a5a5e'), SLATE, b, axis='x', over=0.5)
    for x in (-18, -6):
        BD.chimney(sc, x, -24, 3.2, 0, 52, lin('9a5a44'), nb(sc))
        for j in range(3):
            sphere(sc, x - j * 2.5, -24 - j * 1.5, 55 + j * 4, 3.2 + j * 1.2, lin('7a7874'))
    # the coal heap
    G.lathe(sc, 20, 16, [(10, 0), (5, 5), (0.0, 7)], lin('2e2c2a'), RUBBLE, nb(sc), seg=16)


def dam(sc, h, R=34):
    """A reservoir at the back (-x), held by a curved concrete dam whose
    face looks to +x, and the river running on from its foot."""
    G.box(sc, -R, -R, -12, R, 0, h - 3, lin('7a6e58'), RUBBLE, nb(sc), top=lin('4f7f9a'), topmat=WATER)
    b = nb(sc)
    pts = []
    for i in range(17):
        t = -1 + 2 * i / 16
        pts.append((-12 + 8 * (1 - t * t), R * t))
    for (x0, y0), (x1, y1) in zip(pts[:-1], pts[1:]):
        seg_box(sc, (x0, y0), (x1, y1), 4, h, CONCRETE, STONE, b)
    G.box(sc, -4, -6, R, 6, 0, 0.6, lin('5f8ea8'), WATER, nb(sc))


@bicon('BLDG_Hydro_Plant', fill=0.9)
def _(sc, rng):
    # a dam across a valley: water behind it, the river below
    plinth(sc, -34, -34, 34, 34)
    dam(sc, 22)
    G.box(sc, 4, -10, 14, 8, 0, 8, lin('b8b2a4'), WINDOWS, nb(sc, wspace=4, floor=6))
    for x in (6, 10):
        G.box(sc, x, -14, x + 2, -12, 0, 26, lin('8a8e92'), METAL, nb(sc))
    beam(sc, (7, -13, 26), (7, 20, 26), 0.3, lin('3a3a3e'), METAL, nb(sc))


@bicon('BLDG_Nuclear_Plant')
def _(sc, rng):
    plinth(sc, -34, -30, 34, 30, top=lin('a8a49a'), mat=PAVING)
    prof = []
    for i in range(13):
        z = 40 * i / 12
        r = 11 + 7 * ((z - 28) / 28) ** 2
        prof.append((r, z))
    for (x, y) in ((-18, -14), (2, -20)):
        G.lathe(sc, x, y, prof, CONCRETE, STONE, nb(sc), seg=32, cap=False)
        for j in range(3):
            sphere(sc, x - j * 2, y - j * 2, 44 + j * 4, 8 + j * 2, lin('f0eeea'))
    b = nb(sc)
    G.lathe(sc, 14, 12, [(10, 0), (10, 12)], lin('d8d4cc'), STUCCO, b, seg=28)
    G.dome(sc, 14, 12, 10, 12, lin('d8d4cc'), DOME, b, h=9, seg=28)
    G.box(sc, -14, 8, 4, 22, 0, 10, lin('b8b4aa'), WINDOWS, nb(sc, wspace=4, floor=5))


@bicon('BLDG_Hospital')
def _(sc, rng):
    plinth(sc, -28, -24, 28, 24)
    wall = lin('ecebe6')
    b = nb(sc, wspace=4.5, floor=6.5, wwidth=3.0)
    G.box(sc, -20, -16, 16, 4, 0, 26, wall, WINDOWS, b, top=lin('c8c8c4'))
    G.box(sc, -20, 4, -2, 18, 0, 16, wall, WINDOWS, b, top=lin('c8c8c4'))
    # red crosses
    for (x0, y0, x1, y1) in ((16.1, -9, 16.4, -3), (16.1, -11, 16.4, -1)):
        pass
    bc = nb(sc)
    G.box(sc, 16, -7.5, 16.6, -4.5, 13, 22, RED, PLAIN, bc)
    G.box(sc, 16, -10.5, 16.6, -1.5, 16, 19, RED, PLAIN, bc)
    G.box(sc, -6, -9, 0, -3, 26, 26.6, RED, PLAIN, bc)
    G.box(sc, -9, -6, 3, 0, 26, 26.6, RED, PLAIN, bc)
    G.box(sc, 18, 8, 26, 14, 0, 4, lin('f0f0ec'), PLAIN, nb(sc))


@bicon('BLDG_Lab')
def _(sc, rng):
    plinth(sc, -28, -24, 28, 24)
    b = nb(sc, wspace=4, floor=5)
    G.box(sc, -20, -16, 14, 10, 0, 18, lin('7d9cb0'), GLASS, b, top=lin('b8bcc0'))
    G.box(sc, -20, -16, -6, -2, 18, 24, lin('d0ccc4'), WINDOWS, b)
    bd = nb(sc)
    G.lathe(sc, 6, -4, [(6, 18), (6, 20)], lin('d0ccc4'), STUCCO, bd, seg=24)
    G.dome(sc, 6, -4, 6, 20, lin('e8e6e0'), DOME, bd, h=6, seg=24)
    G.box(sc, 1, -5, 11, -3, 22, 26.5, lin('3a3a3e'), PLAIN, bd)
    # a dish
    G.lathe(sc, 18, 16, [(1.0, 0), (1.0, 8)], STEEL, METAL, nb(sc), seg=8)
    G.lathe(sc, 18, 16, [(0.0, 8), (4, 9.5), (6, 12)], lin('e8e6e0'), METAL, nb(sc), seg=20)


@bicon('BLDG_Mass_Transit_System')
def _(sc, rng):
    plinth(sc, -34, -20, 34, 20, top=lin('a8a49a'), mat=PAVING)
    b = nb(sc)
    for x in range(-28, 30, 14):
        G.box(sc, x - 1.5, -1.5, x + 1.5, 1.5, 0, 14, CONCRETE, STONE, b)
    G.box(sc, -34, -3, 34, 3, 14, 16, CONCRETE, STONE, b)
    bt = nb(sc, wspace=3.5, floor=4, wwidth=2.6, wheight=2.2, wdark=0.25)
    G.box(sc, -24, -2.6, 16, 2.6, 16.5, 22, lin('e8e4da'), WINDOWS, bt, top=lin('d0ccc4'))
    G.box(sc, -24, -2.7, 16, 2.7, 17, 17.8, lin('3f7fb0'), PLAIN, bt)
    G.lathe(sc, 16, 0, [(2.6, 16.5), (2.6, 22)], lin('e8e4da'), PLAIN, bt, seg=12, sx=1.6, sy=1.0)
    BD.apartment(sc, (-30, -18, -18, -8), 34, P, rng)
    BD.apartment(sc, (6, 8, 20, 18), 26, P, rng)


@bicon('BLDG_SAM_Missile_Battery')
def _(sc, rng):
    plinth(sc, -26, -22, 26, 22, top=lin('8a8a6e'))
    G.box(sc, -14, -12, 14, 12, 0, 2, CONCRETE, PAVING, nb(sc))
    b = nb(sc)
    G.lathe(sc, 0, 0, [(4, 2), (4, 6)], lin('6a6e5a'), METAL, b, seg=16)
    G.box(sc, -6, -8, 6, 8, 6, 9, lin('6a6e5a'), METAL, b)
    for y in (-6, -2, 2, 6):
        bm = nb(sc)
        p0 = np.array((-4, y, 9.5))
        d = np.array((0.75, 0, 0.66))
        beam(sc, p0, p0 + d * 16, 1.1, lin('e8e6e0'), METAL, bm, seg=8)
        tip = p0 + d * 16
        beam(sc, tip, tip + d * 2.5, 0.6, RED, PLAIN, bm, seg=8)
    G.box(sc, 16, -16, 22, -10, 0, 5, lin('6a6e5a'), METAL, nb(sc))
    G.lathe(sc, 19, -13, [(0.0, 5), (3, 6), (4, 8)], lin('d8d8d0'), METAL, nb(sc), seg=16)


@bicon('BLDG_Coastal_Fortress')
def _(sc, rng):
    plinth(sc, -30, -30, 30, 30)
    G.box(sc, 4, -30, 30, 30, -0.7, -0.6, lin('5f8ea8'), WATER, nb(sc))
    G.box(sc, -30, 4, 30, 30, -0.7, -0.6, lin('5f8ea8'), WATER, nb(sc))
    stone = lin('a8a090')
    b = nb(sc)
    pts = [(-22, -22), (6, -22), (10, -6), (6, 8), (-8, 10), (-22, 6)]
    for p0, p1 in zip(pts, pts[1:] + pts[:1]):
        seg_box(sc, p0, p1, 4, 10, stone, STONE, b)
    G.box(sc, -20, -20, 4, 6, 0, 9, lin('8a8274'), PLAIN, b)
    for (x, y, dx, dy) in ((7, -12, 1, 0), (6, 0, 1, 0.2), (-2, 9, 0.2, 1), (-14, 7, 0, 1)):
        bm = nb(sc)
        p0 = np.array((x, y, 11.5))
        beam(sc, p0, p0 + np.array((dx, dy, 0.05)) * 7, 1.0, DARK, METAL, bm, seg=8)
    BD.round_tower(sc, (-26, -26, -16, -16), 20, dict(P, stone=[stone]), rng)


@bicon('BLDG_Solar_Plant')
def _(sc, rng):
    plinth(sc, -32, -28, 32, 28)
    for gx in range(-24, 25, 12):
        for gy in range(-20, 21, 10):
            b = nb(sc)
            G.box(sc, gx - 0.4, gy - 0.4, gx + 0.4, gy + 0.4, 0, 3, STEEL, METAL, b)
            sc.quad((gx - 5, gy - 3.5, 2.5), (gx + 5, gy - 3.5, 2.5), (gx + 5, gy + 3.5, 6.0), (gx - 5, gy + 3.5, 6.0),
                    lin('2f4f7a'), GLASS, b)
    G.box(sc, -30, -26, -20, -18, 0, 6, lin('d8d4cc'), WINDOWS, nb(sc))


@bicon('BLDG_Harbor', fill=0.9)
def _(sc, rng):
    water_plinth(sc, -32, -32, 32, 32)
    plinth(sc, -32, -32, -8, 32, h=4)
    b = nb(sc)
    G.box(sc, -8, -6, 24, 4, -0.5, 1.5, lin('8a6a48'), WOOD, b)
    for x in range(-6, 25, 6):
        for y in (-6, 3):
            G.box(sc, x, y, x + 1, y + 1, -4, 1.5, lin('5a4430'), WOOD, b)
    BD.warehouse(sc, (-28, -24, -12, -8), 12, P, rng)
    BD.gable_house(sc, (-28, 4, -14, 20), 13, P, rng)
    # a little boat
    bb = nb(sc)
    G.lathe(sc, 12, 16, [(5, -0.5), (6, 2), (0.0, 2.1)], lin('7a4a30'), WOOD, bb, seg=12, sx=1.0, sy=0.45)
    G.lathe(sc, 12, 16, [(0.4, 2), (0.3, 16)], lin('5a4430'), WOOD, bb, seg=6)
    sc.quad((12, 16, 5), (12, 16, 15), (16, 12, 7), (16, 12, 5), lin('ece6d4'), PLAIN, bb)
    # a crane
    bc = nb(sc)
    G.box(sc, 18, -4, 20, -2, 1.5, 22, lin('6a5038'), WOOD, bc)
    beam(sc, (19, -3, 22), (8, -3, 16), 0.6, lin('6a5038'), WOOD, bc)


@bicon('BLDG_Offshore_Platform', fill=0.88)
def _(sc, rng):
    water_plinth(sc, -32, -32, 32, 32)
    b = nb(sc)
    for (x, y) in ((-12, -12), (12, -12), (-12, 12), (12, 12)):
        G.lathe(sc, x, y, [(2, -4), (2, 10)], lin('d8a030'), METAL, b, seg=10)
    G.box(sc, -16, -16, 16, 16, 10, 14, lin('8a8e92'), METAL, b)
    G.box(sc, -14, -14, 0, 0, 14, 22, lin('d8d4cc'), WINDOWS, nb(sc, wspace=3.5, floor=4))
    bd = nb(sc)
    for (sx, sy) in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        beam(sc, (6 + sx * 5, 6 + sy * 5, 14), (6 + sx * 1, 6 + sy * 1, 44), 0.5, STEEL, METAL, bd)
    for z in (22, 30, 38):
        t = (z - 14) / 30
        r = 5 - 4 * t
        for (a, c) in (((-r, -r), (r, -r)), ((r, -r), (r, r)), ((r, r), (-r, r)), ((-r, r), (-r, -r))):
            beam(sc, (6 + a[0], 6 + a[1], z), (6 + c[0], 6 + c[1], z), 0.35, STEEL, METAL, bd)
    beam(sc, (-14, 14, 14), (-24, 24, 20), 0.6, STEEL, METAL, nb(sc))
    sphere(sc, -25, 25, 21, 2.2, lin('f0b040'))


@bicon('BLDG_Airport')
def _(sc, rng):
    plinth(sc, -34, -30, 34, 30, top=lin('8a9a58'))
    G.box(sc, -34, 6, 34, 18, 0, 0.4, lin('6e6e68'), PAVING, nb(sc))
    for x in range(-30, 31, 8):
        G.box(sc, x, 11.5, x + 4, 12.5, 0.4, 0.5, lin('e8e6dc'), PLAIN, nb(sc))
    b = nb(sc, wspace=4, floor=5)
    G.box(sc, -26, -22, 10, -6, 0, 9, lin('7d9cb0'), GLASS, b, top=lin('d0ccc4'))
    bt = nb(sc)
    G.lathe(sc, 18, -16, [(2.2, 0), (2.0, 24)], lin('d8d4cc'), STUCCO, bt, seg=16)
    G.lathe(sc, 18, -16, [(4, 24), (4.5, 28), (3.5, 29), (0.0, 30)], lin('5d7480'), GLASS, bt, seg=16)
    # a jet on the runway
    bj = nb(sc)
    beam(sc, (-12, 12, 3), (14, 12, 3), 1.8, lin('ecebe6'), METAL, bj, seg=10)
    sc.quad((-2, 12, 3), (4, 12, 3), (2, 26, 3.5), (-2, 26, 3.5), lin('d8d8d2'), METAL, bj)
    sc.quad((-2, 12, 3), (4, 12, 3), (2, -2, 3.5), (-2, -2, 3.5), lin('d8d8d2'), METAL, bj)
    sc.quad((-12, 12, 4), (-9, 12, 4), (-11, 12, 10), (-13, 12, 10), lin('3f7fb0'), METAL, bj)


@bicon('BLDG_Police_Station')
def _(sc, rng):
    plinth(sc, -26, -22, 26, 22)
    b = nb(sc, wspace=5, floor=7)
    G.box(sc, -18, -14, 12, 12, 0, 16, lin('d4d0c8'), WINDOWS, b, top=lin('b8b4ac'))
    G.box(sc, -18.2, -14.2, 12.2, 12.2, 12, 14, lin('2f4466'), PLAIN, b)
    G.box(sc, 12, -4, 15, 4, 0, 9, lin('2f4466'), PLAIN, nb(sc))
    sphere(sc, -4, -2, 18, 2.2, lin('4a7fd0'))
    G.lathe(sc, -4, -2, [(1, 16), (1, 17)], DARK, METAL, nb(sc), seg=8)
    # a patrol car
    bc = nb(sc)
    G.box(sc, 16, 10, 26, 15, 0.8, 4, lin('2a2a30'), METAL, bc)
    G.box(sc, 18, 10.5, 23, 14.5, 4, 6, lin('ecebe6'), METAL, bc)
    sphere(sc, 20.5, 12.5, 6.5, 0.9, lin('4a7fd0'))


@bicon('BLDG_Commercial_Dock', fill=0.9)
def _(sc, rng):
    water_plinth(sc, -32, -32, 32, 32)
    plinth(sc, -32, -32, 8, 32, h=4, top=lin('a8a49a'), mat=PAVING)
    cols = [lin('b8463c'), lin('3f7fb0'), lin('d8a030'), lin('5b8a4a'), lin('8a8e92')]
    k = 0
    for gx in (-26, -14):
        for gy in (-24, -14, -4, 6):
            for z in range(rng.integers(1, 4)):
                G.box(sc, gx, gy, gx + 10, gy + 8, z * 5, z * 5 + 4.8, cols[k % 5], METAL, nb(sc))
                k += 1
    bc = nb(sc)
    for y in (-14, 6):
        G.box(sc, 2, y, 4, y + 2, 0, 26, lin('d8a030'), METAL, bc)
    G.box(sc, -6, -14, 22, 8, 26, 28, lin('d8a030'), METAL, bc)
    # a cargo ship alongside
    bs = nb(sc)
    G.lathe(sc, 20, 0, [(8, -1), (9, 4), (0.0, 4.1)], lin('3a3a40'), METAL, bs, seg=16, sx=0.6, sy=3.0)
    for i in range(3):
        G.box(sc, 16, -16 + i * 9, 24, -9 + i * 9, 4, 9, cols[i + 1], METAL, nb(sc))


@bicon('BLDG_Stock_Exchange')
def _(sc, rng):
    plinth(sc, -26, -24, 26, 24)
    BD.tower_block(sc, (-22, -20, -6, -4), 56, P, rng, glass=True)
    wall = lin('d8d0bc')
    b = nb(sc, wspace=6, floor=12)
    G.box(sc, -12, -14, 12, 16, 0, 24, wall, STONE, b)
    G.box(sc, 12, -12, 18, 14, 0, 2, MARBLE, PLAIN, nb(sc))
    G.column_row(sc, 16, -11, 16, 13, 2, 20, 1.3, 6, MARBLE, PLAIN, nb(sc))
    bp = nb(sc)
    G.box(sc, 11, -12.5, 18, 14.5, 20, 23, MARBLE, PLAIN, bp)
    G.gable(sc, 11, -12.5, 18, 14.5, 23, 6, MARBLE, PLAIN, bp, axis='x', over=0.5)
    flag(sc, 0, 0, 24, 12, lin('3f6fb0'))


@bicon('BLDG_Civil_Defense')
def _(sc, rng):
    plinth(sc, -24, -22, 24, 22)
    b = nb(sc)
    G.lathe(sc, -2, 0, [(14, 0), (12, 6), (0.0, 8)], lin('9a9e88'), STONE, b, seg=24, sx=1.2, sy=1.0)
    G.box(sc, 13, -4, 17, 4, 0, 5, lin('5a5e50'), PLAIN, nb(sc))
    G.box(sc, 16.9, -2.5, 17.2, 2.5, 0, 4, lin('2a2a2e'), PLAIN, nb(sc))
    bs = nb(sc)
    G.lathe(sc, 6, 14, [(0.6, 0), (0.5, 28)], STEEL, METAL, bs, seg=6)
    for (dx, dy) in ((1, 0), (0, 1), (-1, 0), (0, -1)):
        beam(sc, (6, 14, 28), (6 + dx * 3, 14 + dy * 3, 28), 1.2, lin('d8d4cc'), METAL, bs, seg=8)
    G.lathe(sc, 6, 14, [(1.5, 27), (1.5, 30), (0.0, 31)], lin('d8d4cc'), METAL, bs, seg=12)
    G.box(sc, -16, 14, -8, 18, 0, 3, lin('d8c48c'), RUBBLE, nb(sc))


@bicon('BLDG_Wealth')
def _(sc, rng):
    plinth(sc, -22, -20, 22, 20)
    gold = P['gold']
    # a treasure chest overflowing with gold, and stacks of coins
    b = nb(sc)
    G.box(sc, -12, -8, 8, 6, 0, 9, lin('7a4e30'), WOOD, b)
    for x in (-12.2, 7.8):
        G.box(sc, x, -8.2, x + 0.6, 6.2, 0, 9.2, lin('c9a85a'), METAL, b)
    G.lathe(sc, -2, -1, [(8, 9), (5, 12), (0.0, 13)], gold, GOLD, nb(sc), seg=20, sx=1.25, sy=0.85)
    for (x, y, n) in ((12, 10, 5), (16, 3, 3), (6, 14, 2)):
        for i in range(n):
            G.lathe(sc, x, y, [(2.6, i * 1.1), (2.6, i * 1.1 + 1.0), (0.0, i * 1.1 + 1.01)], gold, GOLD, nb(sc), seg=16)
    for i in range(7):
        x, y = rng.uniform(-16, 14), rng.uniform(8, 18)
        G.lathe(sc, x, y, [(2.2, 0), (2.2, 0.5), (0.0, 0.51)], gold, GOLD, nb(sc), seg=12)
