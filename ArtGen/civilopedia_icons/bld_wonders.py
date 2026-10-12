"""Wonder and small wonder icons (registered with improvements.bicon)."""
import math

import numpy as np

import buildings as BD
import geom as G
from bld3d import lin, plinth, water_plinth, GRASS
from improvements import (bicon, P, nb, sphere, seg_box, beam, flag, tree,
                          PLAIN, STUCCO, WINDOWS, ROOF_TILE, THATCH, SLATE, GLASS, BRICK, METAL, DOME, STONE, WATER,
                          FOLIAGE, WOOD, GOLD, RUBBLE, PAVING)

MARBLE = P['marble']
GOLDC = P['gold']
BRONZE = lin('a8743e')
CONCRETE = lin('c6c2b8')
STEEL = lin('9a9ea2')
RED = lin('b0473c')
SAND = lin('d8c690')


def pyramid(sc, cx, cy, s, h, col, b, steps=0):
    if steps:
        for i in range(steps):
            t0 = i / steps
            ss = s * (1 - t0)
            G.box(sc, cx - ss, cy - ss, cx + ss, cy + ss, h * t0, h * (i + 1) / steps, col, STONE, b)
        return
    pts = [(cx - s, cy - s, 0), (cx + s, cy - s, 0), (cx + s, cy + s, 0), (cx - s, cy + s, 0)]
    for p0, p1 in zip(pts, pts[1:] + pts[:1]):
        sc.tri(p0, p1, (cx, cy, h), col, STONE, b)


def figure(sc, x, y, z, h, col, mat, b, arm_up=True, seated=False):
    """A stylized standing (or seated) statue of height h, facing +x+y."""
    k = h / 10.0
    if seated:
        G.box(sc, x - 2 * k, y - 2.5 * k, x + 2 * k, y + 2.5 * k, z, z + 3 * k, col, mat, b)          # throne/lap
        G.box(sc, x - 2.5 * k, y - 2.8 * k, x - 1 * k, y + 2.8 * k, z, z + 7 * k, col, mat, b)     # back
        G.lathe(sc, x - 0.5 * k, y, [(1.6 * k, z + 3 * k), (1.3 * k, z + 6.5 * k), (0.9 * k, z + 7 * k)], col, mat, b, seg=10)
        sphere(sc, x - 0.3 * k, y, z + 7.9 * k, 1.0 * k, col, mat, b, seg=10)
        for s in (-1, 1):
            G.box(sc, x + 0.0 * k, y + s * 1.6 * k - 0.6 * k, x + 3.2 * k, y + s * 1.6 * k + 0.6 * k, z + 2.6 * k, z + 3.6 * k, col, mat, b)
            G.box(sc, x + 2.6 * k, y + s * 1.2 * k - 0.6 * k, x + 3.6 * k, y + s * 1.2 * k + 0.6 * k, z, z + 3 * k, col, mat, b)
        beam(sc, (x + 1.5 * k, y + 2.6 * k, z), (x + 1.5 * k, y + 2.6 * k, z + 10 * k), 0.3 * k, GOLDC, GOLD, b)
        return
    for s in (-1, 1):
        beam(sc, (x, y + s * 0.7 * k, z), (x, y + s * 0.7 * k, z + 4.6 * k), 0.6 * k, col, mat, b, seg=8)
    G.lathe(sc, x, y, [(1.3 * k, z + 4.4 * k), (1.6 * k, z + 6.8 * k), (1.0 * k, z + 7.8 * k)], col, mat, b, seg=10, sx=0.8)
    sphere(sc, x, y, z + 8.6 * k, 0.8 * k, col, mat, b, seg=10)
    beam(sc, (x, y - 1.4 * k, z + 7.2 * k), (x + 0.3 * k, y - 1.8 * k, z + 4.6 * k), 0.4 * k, col, mat, b, seg=6)
    if arm_up:
        beam(sc, (x, y + 1.4 * k, z + 7.2 * k), (x, y + 1.8 * k, z + 10.4 * k), 0.4 * k, col, mat, b, seg=6)
        G.lathe(sc, x, y + 1.8 * k, [(0.6 * k, z + 10.4 * k), (0.9 * k, z + 11 * k), (0.0, z + 12 * k)], lin('f0b040'), PLAIN, b, seg=8)
    else:
        beam(sc, (x, y + 1.4 * k, z + 7.2 * k), (x + 0.3 * k, y + 1.8 * k, z + 4.6 * k), 0.4 * k, col, mat, b, seg=6)


# ================================================================ great wonders

@bicon('BLDG_Pyramids', fill=0.9)
def _(sc, rng):
    plinth(sc, -40, -40, 40, 40, top=SAND)
    b = nb(sc)
    pyramid(sc, -6, -6, 28, 36, lin('dcc68c'), b)
    pyramid(sc, 26, 20, 9, 12, lin('d2bc84'), b)
    pyramid(sc, 22, -24, 7, 9, lin('d2bc84'), b)


@bicon('BLDG_Hanging_Gardens', fill=0.88)
def _(sc, rng):
    plinth(sc, -34, -34, 34, 34, top=SAND)
    wall = lin('c9ae7c')
    for i, s in enumerate((28, 21, 14, 8)):
        b = nb(sc, arch=5.0, archh=5.0)
        z0, z1 = i * 9, i * 9 + 9
        G.box(sc, -s, -s, s, s, z0, z1, wall, STUCCO, b, top=lin('5f7f35'), topmat=FOLIAGE)
        for k in range(int(s / 3)):
            x = -s + 3 + k * 6
            G.lathe(sc, x, s, [(1.6, z1 - 1), (2.4, z1 - 3), (0.0, z1 - 6)], lin('4f7a30'), FOLIAGE, nb(sc), seg=8)
            G.lathe(sc, s, x, [(1.6, z1 - 1), (2.4, z1 - 3), (0.0, z1 - 6)], lin('4f7a30'), FOLIAGE, nb(sc), seg=8)
    for (x, y, z) in ((-20, -20, 9), (16, -18, 9), (-18, 14, 9), (-10, -12, 18), (8, -10, 18), (0, 0, 36)):
        b = nb(sc)
        G.lathe(sc, x, y, [(0.5, z), (0.4, z + 5)], lin('6a4a30'), WOOD, b, seg=6)
        G.lathe(sc, x, y, [(0.0, z + 4), (3.5, z + 6), (2.5, z + 9), (0.0, z + 10)], lin('5a7a32'), FOLIAGE, b, seg=10)


@bicon('BLDG_Colossus', fill=0.9)
def _(sc, rng):
    water_plinth(sc, -30, -30, 30, 30)
    b = nb(sc)
    for (x, y) in ((-6, -10), (-6, 10)):
        G.box(sc, x - 5, y - 5, x + 5, y + 5, -1, 8, lin('c4baa4'), STONE, b)
    figure(sc, -6, 0, 8, 52, BRONZE, METAL, nb(sc))
    beam(sc, (-6, -10, 8), (-6, -2, 30), 2.4, BRONZE, METAL, nb(sc))
    beam(sc, (-6, 10, 8), (-6, 2, 30), 2.4, BRONZE, METAL, nb(sc))
    bs = nb(sc)
    G.lathe(sc, 18, 2, [(4, -1), (5, 2), (0.0, 2.1)], lin('7a4a30'), WOOD, bs, seg=12, sx=2.4, sy=0.6)
    G.lathe(sc, 18, 2, [(0.4, 2), (0.3, 16)], lin('5a4430'), WOOD, bs, seg=6)
    sc.quad((14, 2, 6), (22, 2, 6), (22, 2, 15), (14, 2, 15), lin('ece6d4'), PLAIN, bs)


@bicon('BLDG_Lighthouse', fill=0.9)
def _(sc, rng):
    plinth(sc, -24, -24, 24, 24, top=lin('c8bea4'), mat=PAVING)
    stone = lin('e2d8c0')
    b = nb(sc, wspace=6, floor=12)
    G.box(sc, -14, -14, 14, 14, 0, 34, stone, STONE, b)
    G.crenellate(sc, -14, -14, 14, 14, 34, 2.5, stone, STONE, b, step=4, w=2, t=1.4)
    G.lathe(sc, 0, 0, [(10, 34), (8, 54)], stone, STONE, nb(sc), seg=8, smooth=False)
    G.lathe(sc, 0, 0, [(5.5, 54), (5, 64)], stone, STONE, nb(sc), seg=16)
    bf = nb(sc)
    G.lathe(sc, 0, 0, [(4, 64), (5, 67), (2.5, 72), (0.0, 74)], lin('f0a040'), PLAIN, bf, seg=12)
    G.lathe(sc, 0, 0, [(2, 66), (2.5, 69), (0.0, 72)], lin('fff0b0'), PLAIN, bf, seg=10)
    figure(sc, 0, 0, 74, 9, BRONZE, METAL, nb(sc), arm_up=False)


@bicon('BLDG_Great_Library')
def _(sc, rng):
    plinth(sc, -34, -28, 34, 28)
    wall = lin('e0d6be')
    b = nb(sc, wspace=6, floor=12)
    G.box(sc, -28, -20, 20, 14, 0, 4, MARBLE, PLAIN, b)
    G.box(sc, -24, -16, 16, 10, 4, 22, wall, STONE, b)
    G.gable(sc, -26, -18, 18, 12, 24, 7, lin('b8705a'), ROOF_TILE, b, axis='x', over=1.0, gable_col=MARBLE, gable_mat=PLAIN)
    G.box(sc, -26, -18, 18, 12, 22, 24, MARBLE, PLAIN, b)
    G.column_row(sc, -24, 12, 16, 12, 4, 22, 1.0, 10, MARBLE, PLAIN, nb(sc))
    G.column_row(sc, 18, -16, 18, 10, 4, 22, 1.0, 6, MARBLE, PLAIN, nb(sc))
    for i in range(5):
        x, y = 22 + (i % 2) * 3, -10 + i * 4
        beam(sc, (x, y, 4.8), (x, y + 3, 4.8), 0.8, lin('e8dcb8'), PLAIN, nb(sc), seg=8)


@bicon('BLDG_Oracle', fill=0.88)
def _(sc, rng):
    plinth(sc, -28, -28, 28, 28)
    G.lathe(sc, 0, 0, [(26, 0), (18, 10), (0.0, 12)], lin('8a8a6a'), RUBBLE, nb(sc), seg=24)
    b = nb(sc, rcx=0, rcy=0)
    G.lathe(sc, 0, 0, [(14, 10), (14, 13), (12, 13), (12, 14)], MARBLE, STONE, b, seg=28, smooth=False)
    G.lathe(sc, 0, 0, [(8, 14), (8, 28)], lin('d8cfba'), STONE, b, seg=24)
    for i in range(14):
        a = 2 * math.pi * i / 14
        G.lathe(sc, 11 * math.cos(a), 11 * math.sin(a), [(0.8, 14), (0.7, 28)], MARBLE, PLAIN, nb(sc), seg=8)
    G.lathe(sc, 0, 0, [(12.5, 28), (12.5, 30), (0.0, 37)], lin('b8705a'), ROOF_TILE, nb(sc), seg=28)
    sphere(sc, 0, 0, 38, 1.2, GOLDC, GOLD)


@bicon('BLDG_Great_Wall', fill=0.9)
def _(sc, rng):
    plinth(sc, -34, -34, 34, 34)
    # hills under the wall
    for (x, y, r, h) in ((-20, 16, 13, 9), (16, -18, 14, 12), (0, 0, 10, 5)):
        G.lathe(sc, x, y, [(r, 0), (r * 0.55, h * 0.8), (0.0, h)], lin('7d8f4e'), PLAIN, nb(sc), seg=20)
    stone = lin('aaa292')
    b = nb(sc)
    pts = [(-34, 26), (-20, 16), (-8, 8), (0, 0), (8, -8), (16, -18), (34, -26)]
    zs = [0, 9, 6, 5, 7, 12, 0]
    for i, ((x0, y0), (x1, y1)) in enumerate(zip(pts[:-1], pts[1:])):
        n = 5
        for k in range(n):
            t0, t1 = k / n, (k + 1) / n
            p0 = (x0 + (x1 - x0) * t0, y0 + (y1 - y0) * t0)
            p1 = (x0 + (x1 - x0) * t1, y0 + (y1 - y0) * t1)
            z = zs[i] + (zs[i + 1] - zs[i]) * (t0 + t1) / 2
            seg_box(sc, p0, p1, 6.5, 13, stone, STONE, b, z0=z - 3)
            mx, my = (p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2
            G.box(sc, mx - 1.1, my - 1.1, mx + 1.1, my + 1.1, z + 10, z + 12.5, stone, STONE, b)
    for (x, y), z in zip(pts[1:-1:2], zs[1:-1:2]):
        bt = nb(sc)
        G.box(sc, x - 6, y - 6, x + 6, y + 6, z - 3, z + 20, stone, STONE, bt)
        G.hip(sc, x - 6, y - 6, x + 6, y + 6, z + 20, 5, lin('6a5a4a'), SLATE, bt, over=1.2, ridge=0)


@bicon('BLDG_Art_of_War')
def _(sc, rng):
    plinth(sc, -30, -28, 30, 28)
    pal = dict(P, roofs=[lin('3f4f4a')], hutwalls=[lin('a8463c')], podium=lin('a8a294'))
    BD.asian_hall(sc, (-22, -20, 16, 12), 30, pal, rng, two_tier=True)
    for (x, y, c) in ((22, -16, lin('c0503e')), (22, -4, lin('d8b84a')), (22, 8, lin('3f5f9a')), (8, 20, lin('c0503e')), (-6, 20, lin('d8b84a'))):
        b = nb(sc)
        G.lathe(sc, x, y, [(0.35, 0), (0.3, 24)], lin('5a4430'), WOOD, b, seg=6)
        sc.quad((x, y, 12), (x, y, 23), (x + 3, y - 3, 23), (x + 3, y - 3, 12), c, PLAIN, b)


@bicon('BLDG_Sistine_Chapel')
def _(sc, rng):
    plinth(sc, -32, -22, 32, 22)
    brick = lin('b88a6a')
    b = nb(sc, wspace=7, floor=18, wwidth=2.4, wheight=7)
    G.box(sc, -26, -12, 24, 12, 0, 34, brick, WINDOWS, b)
    G.crenellate(sc, -26, -12, 24, 12, 34, 2.6, brick, BRICK, b, step=3.6, w=1.8, t=1.4)
    G.gable(sc, -24, -10, 22, 10, 34, 5, lin('9a6050'), ROOF_TILE, b, axis='x', over=0.2)
    G.box(sc, 24, -4, 28, 4, 0, 12, brick, BRICK, nb(sc))


@bicon('BLDG_Circumnavigation', fill=0.9)
def _(sc, rng):
    # a padrao (stone pillar with a cross) on a headland, and a carrack setting out
    water_plinth(sc, -32, -32, 32, 32)
    plinth(sc, -32, -32, -6, 32)
    b = nb(sc)
    G.box(sc, -22, -6, -14, 2, 0, 4, lin('c4baa4'), STONE, b)
    G.lathe(sc, -18, -2, [(2.4, 4), (2.0, 28)], lin('d8d0bc'), STONE, b, seg=8)
    G.box(sc, -19, -3, -17, -1, 28, 36, lin('d8d0bc'), STONE, b)
    G.box(sc, -19, -6, -17, 2, 32, 34, lin('d8d0bc'), STONE, b)
    bs = nb(sc)
    G.lathe(sc, 14, 6, [(5, -1), (6.5, 4), (6.5, 7), (0.0, 7.1)], lin('7a4a30'), WOOD, bs, seg=16, sx=2.6, sy=0.8)
    for (x, h) in ((6, 30), (14, 38), (22, 28)):
        G.lathe(sc, x, 6, [(0.5, 7), (0.4, h)], lin('5a4430'), WOOD, bs, seg=6)
        sc.quad((x, 0, h * 0.45), (x, 12, h * 0.45), (x, 12, h * 0.9), (x, 0, h * 0.9), lin('ece6d4'), PLAIN, bs)
    sc.quad((14, 1, 22), (14, 11, 22), (14, 11, 30), (14, 1, 30), lin('ece6d4'), PLAIN, bs)
    G.box(sc, 13.6, 4, 14.4, 8, 22, 30, lin('b0473c'), PLAIN, bs)


@bicon('BLDG_Solar_System')
def _(sc, rng):
    # an observatory: a domed tower with the slit open and its telescope
    plinth(sc, -24, -24, 24, 24)
    wall = lin('d8cdb4')
    b = nb(sc, wspace=6, floor=10)
    G.box(sc, -20, -18, 10, 10, 0, 12, wall, STUCCO, b)
    G.hip(sc, -20, -18, 10, 10, 12, 5, lin('9a6050'), ROOF_TILE, b, over=0.8)
    bt = nb(sc, rcx=10, rcy=4)
    G.lathe(sc, 10, 4, [(9, 0), (9, 26)], wall, STONE, bt, seg=24)
    G.dome(sc, 10, 4, 9.5, 26, lin('b8bcc0'), METAL, bt, h=9, seg=28)
    G.box(sc, 15, 2, 19.5, 6, 27, 34, lin('2a2a30'), PLAIN, nb(sc))
    beam(sc, (12, 4, 30), (24, 4, 40), 1.4, lin('c9a85a'), METAL, nb(sc), seg=10)


@bicon('BLDG_Great_Playhouse')
def _(sc, rng):
    # a round timber-framed theater with a thatched roof
    plinth(sc, -28, -28, 28, 28)
    b = nb(sc, rcx=0, rcy=0, wspace=5, floor=7)
    G.lathe(sc, 0, 0, [(22, 0), (22, 20)], lin('ece4d0'), STUCCO, b, seg=16, smooth=False)
    for i in range(16):
        a = 2 * math.pi * (i + 0.5) / 16
        G.box(sc, 21.6 * math.cos(a) - 0.7, 21.6 * math.sin(a) - 0.7, 21.6 * math.cos(a) + 0.7, 21.6 * math.sin(a) + 0.7, 0, 20,
              lin('5a4030'), WOOD, b)
    for z in (6.5, 13.5):
        G.lathe(sc, 0, 0, [(22.3, z), (22.3, z + 1)], lin('5a4030'), WOOD, b, seg=16, smooth=False)
    G.lathe(sc, 0, 0, [(23.5, 20), (14, 27), (13, 27)], lin('b59c58'), THATCH, nb(sc), seg=16, smooth=False)
    G.lathe(sc, 0, 0, [(13, 21), (0.0, 21)], lin('c8b88a'), PLAIN, nb(sc), seg=16)
    flag(sc, -12, -12, 27, 10, lin('c0503e'))


@bicon('BLDG_Inventor\'s_Workshop')
def _(sc, rng):
    plinth(sc, -28, -26, 28, 26)
    BD.hip_house(sc, (-24, -20, 4, 4), 22, P, rng, roofs=[lin('a5543c')], roofmat=ROOF_TILE)
    # a wooden flying machine on a frame
    b = nb(sc)
    for (x, y) in ((8, 6), (20, 6), (8, 18), (20, 18)):
        G.box(sc, x - 0.5, y - 0.5, x + 0.5, y + 0.5, 0, 12, lin('6a5038'), WOOD, b)
    cloth = lin('e2d4b0')
    for s in (-1, 1):
        sc.quad((14, 12, 13), (14 + s * 2, 12 + s * 16, 16), (14 + s * 6, 12 + s * 14, 13), (16, 12, 12.5), cloth, PLAIN, b)
        sc.quad((14, 12, 13), (14 + s * 16, 12 + s * 2, 16), (14 + s * 14, 12 + s * 6, 13), (14, 14, 12.5), cloth, PLAIN, b)
    beam(sc, (14, 12, 12), (14, 12, 15), 0.6, lin('6a5038'), WOOD, b)
    # a big gear wheel leaning on the wall
    bg = nb(sc)
    for i in range(16):
        a0, a1 = 2 * math.pi * i / 16, 2 * math.pi * (i + 1) / 16
        r = 7
        beam(sc, (6, -4 + r * math.cos(a0), 8 + r * math.sin(a0)), (6, -4 + r * math.cos(a1), 8 + r * math.sin(a1)), 0.9, lin('8a6040'), WOOD, bg)
        if i % 2 == 0:
            beam(sc, (6, -4 + r * math.cos(a0), 8 + r * math.sin(a0)), (6, -4 + 9 * math.cos(a0), 8 + 9 * math.sin(a0)), 0.7, lin('8a6040'), WOOD, bg)
    for i in range(4):
        a = math.pi * i / 4
        beam(sc, (6, -4 - 7 * math.cos(a), 8 - 7 * math.sin(a)), (6, -4 + 7 * math.cos(a), 8 + 7 * math.sin(a)), 0.6, lin('8a6040'), WOOD, bg)


@bicon('BLDG_Grand_Cathedral')
def _(sc, rng):
    # a baroque cathedral: twin bell towers and a great dome
    plinth(sc, -32, -26, 32, 26)
    stone, roof = lin('e2d8c0'), lin('6a7a80')
    b = nb(sc, wspace=6, floor=14, arch=6.0, archh=12.0)
    G.box(sc, -22, -12, 22, 12, 0, 26, stone, STONE, b)
    G.gable(sc, -22, -12, 22, 12, 26, 8, roof, SLATE, b, axis='x', over=0.8, gable_col=stone, gable_mat=STONE)
    bd = nb(sc)
    G.lathe(sc, -4, 0, [(9, 26), (9, 34)], stone, STONE, bd, seg=24)
    G.dome(sc, -4, 0, 9.5, 34, lin('6f8f8a'), DOME, bd, h=11, seg=28)
    G.lathe(sc, -4, 0, [(1.5, 45), (1.5, 49), (0.0, 51)], stone, STONE, bd, seg=8)
    for y in (-16, 8):
        bt = nb(sc, wspace=6, floor=10)
        G.box(sc, 18, y, 26, y + 8, 0, 40, stone, STONE, bt)
        G.lathe(sc, 22, y + 4, [(3.6, 40), (3.6, 44)], stone, STONE, bt, seg=12)
        G.dome(sc, 22, y + 4, 3.8, 44, lin('6f8f8a'), DOME, bt, h=4, onion=0.6, seg=12)
    sphere(sc, 26.2, 0, 20, 3.0, lin('5a6f9a'))


@bicon('BLDG_Great_University')
def _(sc, rng):
    plinth(sc, -32, -28, 32, 28)
    wall, roof = lin('c9b08a'), lin('5a5a5e')
    for (r, ax) in (((-26, -22, 24, -10), 'x'), ((-26, -10, -14, 22), 'y'), ((12, -10, 24, 22), 'y')):
        b = nb(sc, wspace=5, floor=9)
        G.box(sc, *r, 0, 16, wall, WINDOWS, b)
        G.gable(sc, *r, 16, 7, roof, SLATE, b, axis=ax, over=0.8)
    bt = nb(sc, wspace=5, floor=9)
    G.box(sc, -6, -24, 4, -14, 0, 42, wall, STONE, bt)
    G.crenellate(sc, -6, -24, 4, -14, 42, 3, wall, STONE, bt, step=3.0, w=1.6, t=1.2)
    for (x, y) in ((-6, -24), (4, -24), (-6, -14), (4, -14)):
        G.lathe(sc, x, y, [(1.2, 42), (0.0, 50)], wall, STONE, nb(sc), seg=6)
    sphere(sc, 4.2, -19, 34, 2.4, lin('f0ecdf'))
    G.box(sc, -14, -10, 12, 22, 0, 0.3, GRASS, PLAIN, nb(sc))
    tree(sc, 0, 8, 14, rng)


@bicon('BLDG_Trading_Company')
def _(sc, rng):
    plinth(sc, -30, -26, 30, 26)
    BD.tall_house(sc, (-24, -20, 4, 4), 30, dict(P, walls=[lin('a85a44')]), rng, roof='hip', walls=[lin('a85a44')], roofs=[lin('5a5a5e')])
    BD.warehouse(sc, (-24, 8, -4, 22), 14, P, rng)
    for i in range(6):
        x, y = 8 + (i % 3) * 5, 8 + (i // 3) * 6
        G.box(sc, x, y, x + 4, y + 4, 0, 4, lin('a8885a'), WOOD, nb(sc))
    sphere(sc, 22, -6, 1.8, 2.2, lin('8a6a48'))
    flag(sc, 8, -14, 0, 34, lin('c0503e'))


@bicon('BLDG_Universal_Suffrage')
def _(sc, rng):
    # a parliament with its clock tower
    plinth(sc, -34, -24, 34, 24)
    stone, roof = lin('d4c49c'), lin('5a6066')
    b = nb(sc, wspace=4, floor=8)
    G.box(sc, -28, -14, 24, 10, 0, 18, stone, WINDOWS, b)
    G.gable(sc, -28, -14, 24, 10, 18, 6, roof, SLATE, b, axis='x', over=0.4)
    for x in range(-26, 24, 6):
        G.lathe(sc, x, 10, [(0.7, 18), (0.0, 24)], stone, STONE, nb(sc), seg=4)
    bt = nb(sc, wspace=5, floor=10)
    G.box(sc, -34, -20, -24, -10, 0, 50, stone, WINDOWS, bt)
    G.box(sc, -35, -21, -23, -9, 50, 58, stone, STONE, bt)
    sphere(sc, -22.8, -15, 54, 3.4, lin('f0ecdf'))
    sphere(sc, -29, -8.8, 54, 3.4, lin('f0ecdf'))
    G.lathe(sc, -29, -15, [(6.5, 58), (0.0, 72)], roof, SLATE, bt, seg=4)


@bicon('BLDG_Hoover_Dam', fill=0.9)
def _(sc, rng):
    import bld_improvements as BI
    plinth(sc, -34, -34, 34, 34, top=lin('b89a6a'))
    # canyon walls on both sides
    for y0, y1 in ((-34, -24), (24, 34)):
        G.box(sc, -34, y0, 34, y1, 0, 30, lin('a8845c'), RUBBLE, nb(sc), top=lin('b89a6a'))
    BI.dam(sc, 30, R=24)
    for y in (-14, 14):
        G.box(sc, -9, y - 3, -3, y + 3, 30, 36, CONCRETE, STONE, nb(sc))
    G.box(sc, 2, -10, 14, 10, 0, 7, lin('b8b2a4'), WINDOWS, nb(sc))


@bicon('BLDG_Theory_of_Evolution')
def _(sc, rng):
    # a natural history museum, and a dinosaur skeleton on its lawn
    plinth(sc, -32, -28, 32, 28)
    wall = lin('b8907a')
    b = nb(sc, wspace=5, floor=10, arch=5.0, archh=8.0)
    G.box(sc, -26, -22, 16, -4, 0, 18, wall, BRICK, b)
    G.gable(sc, -26, -22, 16, -4, 18, 7, lin('5a6066'), SLATE, b, axis='x', over=0.6)
    for x in (-26, 10):
        bt = nb(sc)
        G.box(sc, x, -24, x + 8, -14, 0, 28, wall, BRICK, bt)
        G.lathe(sc, x + 4, -19, [(5.5, 28), (0.0, 36)], lin('5a6066'), SLATE, bt, seg=4)
    bone = lin('ece4cc')
    bs = nb(sc)
    pts = [(-14, 16, 6), (-6, 14, 9), (2, 12, 10), (10, 10, 9), (16, 9, 13), (20, 8, 16)]
    for p0, p1 in zip(pts[:-1], pts[1:]):
        beam(sc, p0, p1, 0.9, bone, PLAIN, bs, seg=6)
    sphere(sc, 21, 8, 17, 2.2, bone, PLAIN, bs, seg=8)
    for (x, y) in ((-2, 12), (8, 10)):
        for s in (-1, 1):
            beam(sc, (x, y + s * 1.5, 9.5), (x + 1, y + s * 2.5, 0), 0.7, bone, PLAIN, bs, seg=6)
    for x in range(-4, 10, 3):
        beam(sc, (x, 12 - (x + 4) * 0.15, 9.5), (x, 12 - (x + 4) * 0.15, 6), 0.4, bone, PLAIN, bs, seg=5)
    beam(sc, (-14, 16, 6), (-22, 20, 2), 0.7, bone, PLAIN, bs, seg=6)


@bicon('BLDG_United_Nations')
def _(sc, rng):
    plinth(sc, -32, -28, 32, 28, top=lin('a8a49a'), mat=PAVING)
    b = nb(sc, wspace=3.5, floor=4.5, wwidth=3.0, wheight=3.0)
    G.box(sc, -20, -12, -6, 12, 0, 64, lin('6f94a8'), GLASS, b, top=lin('b8bcc0'))
    G.box(sc, -21, -13, -5, -11.5, 0, 64, lin('d8d4cc'), PLAIN, b)
    G.box(sc, -21, 11.5, -5, 13, 0, 64, lin('d8d4cc'), PLAIN, b)
    bl = nb(sc)
    G.box(sc, 0, -18, 22, 4, 0, 10, lin('e2ded4'), WINDOWS, bl)
    G.lathe(sc, 11, -7, [(5, 10), (5, 11), (0.0, 15)], lin('d8d4cc'), DOME, bl, seg=20)
    for i in range(6):
        flag(sc, 26, -20 + i * 7, 0, 14, [lin('c0503e'), lin('3f6fb0'), lin('d8b84a'), lin('5b8a4a'), lin('ffffff'), lin('8a5aa0')][i], w=4, fh=3)
    sphere(sc, 8, 18, 6, 5, lin('3f6fb0'), METAL)


@bicon('BLDG_Manhattan_Project')
def _(sc, rng):
    plinth(sc, -30, -30, 30, 30, top=lin('b8a87a'))
    b = nb(sc)
    for (sx, sy) in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        beam(sc, (-6 + sx * 7, -6 + sy * 7, 0), (-6 + sx * 2, -6 + sy * 2, 40), 0.6, STEEL, METAL, b)
    for z in (10, 20, 30):
        t = z / 40
        r = 7 - 5 * t
        for (a, c) in (((-r, -r), (r, -r)), ((r, -r), (r, r)), ((r, r), (-r, r)), ((-r, r), (-r, -r))):
            beam(sc, (-6 + a[0], -6 + a[1], z), (-6 + c[0], -6 + c[1], z), 0.4, STEEL, METAL, b)
    G.box(sc, -10, -10, -2, -2, 40, 46, lin('9a9488'), METAL, b)
    for (r, x0, y0, x1, y1) in ((0, 4, -24, 24, -10), (0, 6, 4, 24, 20)):
        bh = nb(sc)
        n = 10
        for i in range(n):
            a0, a1 = math.pi * i / n, math.pi * (i + 1) / n
            R = (y1 - y0) / 2
            yc = (y0 + y1) / 2
            sc.quad((x0, yc - R * math.cos(a0), R * math.sin(a0)), (x1, yc - R * math.cos(a0), R * math.sin(a0)),
                    (x1, yc - R * math.cos(a1), R * math.sin(a1)), (x0, yc - R * math.cos(a1), R * math.sin(a1)), lin('8a8e86'), METAL, bh)
        for x in (x0, x1):
            for i in range(n):
                a0, a1 = math.pi * i / n, math.pi * (i + 1) / n
                R = (y1 - y0) / 2
                yc = (y0 + y1) / 2
                sc.tri((x, yc, 0), (x, yc - R * math.cos(a0), R * math.sin(a0)), (x, yc - R * math.cos(a1), R * math.sin(a1)),
                       lin('7a7e76'), METAL, bh)


@bicon('BLDG_Cure_for_Cancer')
def _(sc, rng):
    plinth(sc, -30, -26, 30, 26)
    b = nb(sc, wspace=4.5, floor=6, wwidth=3.0)
    G.box(sc, -24, -20, 10, 0, 0, 20, lin('ecebe6'), WINDOWS, b, top=lin('c8c8c4'))
    G.box(sc, -24, 0, -8, 18, 0, 14, lin('ecebe6'), WINDOWS, b, top=lin('c8c8c4'))
    bd = nb(sc)
    G.lathe(sc, 12, 10, [(12 * math.cos(t), 12 * math.sin(t) + 0.01) for t in np.linspace(0, math.pi / 2, 10)],
            lin('8fb4c8'), GLASS, bd, seg=28)
    # a double helix sculpture
    bh = nb(sc)
    for i in range(24):
        t = i / 23
        z = 2 + 26 * t
        a = t * 3 * math.pi
        for ph, c in ((0, lin('3f6fb0')), (math.pi, lin('c0503e'))):
            sphere(sc, 14 + 3 * math.cos(a + ph), -14 + 3 * math.sin(a + ph), z, 0.9, c, PLAIN, bh, seg=6)


@bicon('BLDG_Longevity')
def _(sc, rng):
    # the fountain of youth in a garden
    plinth(sc, -26, -26, 26, 26)
    b = nb(sc)
    G.lathe(sc, 0, 0, [(16, 0), (16, 3), (14, 3), (14, 1.5)], MARBLE, STONE, b, seg=32, smooth=False)
    G.lathe(sc, 0, 0, [(14, 1.5), (0.0, 1.51)], lin('6fa0c0'), WATER, b, seg=32)
    G.lathe(sc, 0, 0, [(2.5, 1.5), (1.8, 10)], MARBLE, STONE, b, seg=12)
    G.lathe(sc, 0, 0, [(0.0, 9), (7, 10), (7, 11), (0.0, 11.5)], MARBLE, STONE, b, seg=20)
    G.lathe(sc, 0, 0, [(1.2, 11), (0.9, 16)], MARBLE, STONE, b, seg=10)
    G.lathe(sc, 0, 0, [(0.0, 15.5), (3.5, 16.5), (0.0, 17.5)], MARBLE, STONE, b, seg=14)
    bw = nb(sc)
    for i in range(8):
        a = 2 * math.pi * i / 8
        beam(sc, (0, 0, 17), (5 * math.cos(a), 5 * math.sin(a), 12), 0.35, lin('cfe8f4'), WATER, bw, seg=5)
    for (x, y) in ((-20, -20), (20, -20), (-20, 20), (20, 20), (0, -22), (-22, 0)):
        tree(sc, x, y, 12, rng)
    for i in range(10):
        a = 2 * math.pi * i / 10
        sphere(sc, 20 * math.cos(a), 20 * math.sin(a), 1.2, 1.6, [lin('d86a8a'), lin('e8c040'), lin('f0f0f0')][i % 3])


@bicon('BLDG_SETI_Program', fill=0.88)
def _(sc, rng):
    plinth(sc, -32, -32, 32, 32)
    for (x, y, R) in ((-10, -8, 20), (20, 20, 9)):
        b = nb(sc)
        G.box(sc, x - 3, y - 3, x + 3, y + 3, 0, R * 0.8, lin('b8b4aa'), STONE, b)
        n = 10
        prof = [(R * i / n, R * 0.8 + 0.35 * R * (i / n) ** 2) for i in range(n + 1)]
        prof[0] = (0.0, R * 0.8)
        # the dish, tilted toward the sky to the southeast
        tri_ring = []
        nrm = np.array((0.45, 0.45, 0.77))
        nrm /= np.linalg.norm(nrm)
        u = np.cross(nrm, [0, 0, 1.0]); u /= np.linalg.norm(u)
        v = np.cross(nrm, u)
        c0 = np.array((x, y, R * 0.8 + 2))
        ring_n = 24
        rings = []
        for i in range(n + 1):
            rr, dz = R * i / n, 0.35 * R * (i / n) ** 2
            rings.append([c0 + rr * (math.cos(2 * math.pi * k / ring_n) * u + math.sin(2 * math.pi * k / ring_n) * v) + dz * nrm
                          for k in range(ring_n)])
        for i in range(n):
            for k in range(ring_n):
                a, bb = rings[i][k], rings[i][(k + 1) % ring_n]
                c, d = rings[i + 1][(k + 1) % ring_n], rings[i + 1][k]
                sc.quad(tuple(a), tuple(bb), tuple(c), tuple(d), lin('ecebe6'), METAL, b)
        tip = c0 + nrm * R * 0.6
        for k in range(0, ring_n, 8):
            beam(sc, tuple(rings[n][k]), tuple(tip), 0.3, STEEL, METAL, b)
        sphere(sc, *tip, 1.0, lin('5a5e62'), METAL, b, seg=8)


@bicon('BLDG_Internet')
def _(sc, rng):
    plinth(sc, -30, -26, 30, 26, top=lin('a8a49a'), mat=PAVING)
    BD.tower_block(sc, (-24, -20, -6, -2), 48, dict(P, glass=[lin('5f84a0')]), rng, glass=True)
    b = nb(sc, wspace=3.5, floor=4.5)
    G.box(sc, -2, -18, 22, 4, 0, 10, lin('3a4a5a'), GLASS, b, top=lin('9aa0a6'))
    for i in range(6):
        G.box(sc, 1 + i * 3.5, -16, 3 + i * 3.5, -12, 10, 13, lin('6a7076'), METAL, nb(sc))
    bt = nb(sc)
    G.lathe(sc, -15, -11, [(1.0, 48), (0.3, 66)], STEEL, METAL, bt, seg=6)
    for z in (54, 60):
        sphere(sc, -15, -11, z, 1.2, lin('c0503e'), PLAIN, bt, seg=8)
    for (x, y) in ((8, 14), (18, 12)):
        G.lathe(sc, x, y, [(0.8, 0), (0.8, 4)], STEEL, METAL, nb(sc), seg=8)
        G.lathe(sc, x, y, [(0.0, 4), (3, 5), (4.5, 7)], lin('e8e6e0'), METAL, nb(sc), seg=16)


@bicon('BLDG_Artemis')
def _(sc, rng):
    plinth(sc, -34, -24, 34, 24)
    BD.temple(sc, (-28, -16, 28, 16), 30, dict(P, roofs=[lin('b8705a')]), rng)


@bicon('BLDG_Zeus')
def _(sc, rng):
    # the colossal seated Zeus of gold and ivory, before the back wall of his temple
    plinth(sc, -26, -22, 26, 22)
    b = nb(sc)
    G.box(sc, -22, -18, 18, 18, 0, 3, MARBLE, PLAIN, b)
    G.box(sc, -22, -18, -18, 18, 3, 40, lin('d8ceb6'), STONE, b)
    G.box(sc, -22, -18, 18, -15, 3, 40, lin('d8ceb6'), STONE, b)
    G.box(sc, -22, -18, 18, 18, 40, 43, MARBLE, PLAIN, b)
    for (x, y) in ((15, -15), (15, 15), (-18, 15), (0, 15)):
        G.lathe(sc, x, y, [(1.4, 3), (1.2, 40)], MARBLE, PLAIN, nb(sc), seg=10)
    G.box(sc, -14, -8, 2, 8, 3, 8, lin('6a5e4a'), STONE, nb(sc))
    figure(sc, -6, 0, 8, 30, lin('ece2c8'), PLAIN, nb(sc), seated=True)
    G.box(sc, -9, -4.5, -3, 4.5, 13, 18, GOLDC, GOLD, nb(sc))
    sphere(sc, -6.3, 0, 31, 2.0, GOLDC, GOLD)


@bicon('BLDG_Mausoleum')
def _(sc, rng):
    plinth(sc, -28, -28, 28, 28)
    stone = lin('d8ceb6')
    b = nb(sc)
    G.box(sc, -18, -18, 18, 18, 0, 16, stone, STONE, b)
    G.box(sc, -16, -16, 16, 16, 16, 18, MARBLE, PLAIN, b)
    G.box(sc, -14, -14, 14, 14, 18, 30, lin('cfc4aa'), STUCCO, nb(sc))
    b2 = nb(sc)
    G.column_row(sc, -14, 15, 14, 15, 18, 30, 0.9, 7, MARBLE, PLAIN, b2)
    G.column_row(sc, 15, -14, 15, 14, 18, 30, 0.9, 7, MARBLE, PLAIN, b2)
    G.box(sc, -16, -16, 16, 16, 30, 32, MARBLE, PLAIN, b2)
    for i in range(6):
        s = 15 - i * 2.2
        G.box(sc, -s, -s, s, s, 32 + i * 2, 34 + i * 2, lin('cfc4aa'), STONE, b2)
    G.box(sc, -4, -3, 4, 3, 44, 47, BRONZE, METAL, nb(sc))
    for y in (-2, 0, 2):
        beam(sc, (3, y, 47), (6, y, 50), 0.7, BRONZE, METAL, nb(sc))


@bicon('BLDG_Knights_Templar')
def _(sc, rng):
    plinth(sc, -30, -30, 30, 30)
    stone = lin('b4ab98')
    BD.keep(sc, (-14, -14, 6, 6), 30, dict(P, stone=[stone]), rng)
    b = nb(sc)
    t = 3
    for (x0, y0, x1, y1) in ((-26, -26, 22, -26 + t), (-26, -26, -26 + t, 22), (-26, 22 - t, 22, 22), (22 - t, -26, 22, 22)):
        G.box(sc, x0, y0, x1, y1, 0, 10, stone, STONE, b)
    for (x, y) in ((-24, -24), (20, -24), (-24, 20), (20, 20)):
        BD.round_tower(sc, (x - 4, y - 4, x + 4, y + 4), 16, dict(P, stone=[stone]), rng)
    bf = nb(sc)
    G.lathe(sc, -4, -4, [(0.4, 30), (0.35, 46)], lin('5a4430'), WOOD, bf, seg=6)
    sc.quad((-4, -4, 37), (5, -13, 37), (5, -13, 45), (-4, -4, 45), lin('f0ece0'), PLAIN, bf)
    sc.quad((0, -8.2, 39.5), (1.5, -9.7, 39.5), (1.5, -9.7, 42.5), (0, -8.2, 42.5), RED, PLAIN, nb(sc))
    sc.quad((-1.2, -7.0, 40.5), (2.7, -10.9, 40.5), (2.7, -10.9, 41.5), (-1.2, -7.0, 41.5), RED, PLAIN, nb(sc))


# ================================================================ small wonders

@bicon('BLDG_Epic')
def _(sc, rng):
    plinth(sc, -26, -22, 26, 22)
    BD.triumphal_arch(sc, (-18, -8, 14, 8), 30, P, rng)
    figure(sc, -2, 0, 30, 14, BRONZE, METAL, nb(sc))


@bicon('BLDG_Great_Ironworks')
def _(sc, rng):
    plinth(sc, -30, -26, 30, 26, top=lin('8a8478'), mat=PAVING)
    b = nb(sc)
    G.box(sc, -24, -18, 8, 8, 0, 18, lin('6a5e56'), BRICK, b)
    G.gable(sc, -24, -18, 8, 8, 18, 6, lin('4a4a4e'), SLATE, b, axis='x', over=0.5)
    G.lathe(sc, 16, -8, [(9, 0), (7, 26), (5, 30)], lin('5a4e48'), BRICK, nb(sc), seg=16)
    G.lathe(sc, 16, -8, [(4.5, 30.01), (0.0, 30.02)], lin('f0a040'), PLAIN, nb(sc), seg=16)
    BD.chimney(sc, -18, -22, 3, 0, 46, lin('8a5a44'), nb(sc))
    G.box(sc, 8.1, -4, 8.4, 4, 2, 9, lin('f0a040'), PLAIN, nb(sc))
    for j in range(3):
        sphere(sc, -18 - j * 2, -22 - j, 49 + j * 4, 3 + j, lin('6a6864'))


@bicon('BLDG_Forbidden_Palace')
def _(sc, rng):
    plinth(sc, -32, -28, 32, 28)
    pal = dict(P, roofs=[lin('d8a830')], hutwalls=[lin('a83c30')], podium=lin('d8d0bc'))
    BD.asian_hall(sc, (-24, -18, 20, 12), 30, pal, rng, two_tier=True)
    b = nb(sc)
    G.box(sc, -28, 18, 28, 21, 0, 8, lin('a83c30'), STUCCO, b)
    G.box(sc, -28.5, 17.5, 28.5, 21.5, 8, 9.5, lin('d8a830'), ROOF_TILE, b)


@bicon('BLDG_Military_Academy')
def _(sc, rng):
    plinth(sc, -30, -26, 30, 26)
    stone = lin('a8a090')
    b = nb(sc, wspace=5, floor=9)
    G.box(sc, -24, -18, 16, 2, 0, 18, stone, WINDOWS, b)
    G.crenellate(sc, -24, -18, 16, 2, 18, 2.5, stone, STONE, b, step=3.5, w=1.8, t=1.4)
    for x in (-24, 8):
        bt = nb(sc)
        G.box(sc, x, -20, x + 8, -12, 0, 28, stone, STONE, bt)
        G.crenellate(sc, x, -20, x + 8, -12, 28, 2.5, stone, STONE, bt, step=3, w=1.6, t=1.2)
    G.box(sc, -20, 6, 20, 22, 0, 0.3, lin('c8bea4'), PAVING, nb(sc))
    flag(sc, 20, 10, 0, 28, lin('3f5f9a'))
    BD.statue(sc, -4, 14, 7, P)


@bicon('BLDG_Pentagon', fill=0.9)
def _(sc, rng):
    plinth(sc, -36, -36, 36, 36)
    b = nb(sc, wspace=4, floor=5)
    wall = lin('cfc9bc')
    for (R, h) in ((30, 12), (19, 12)):
        pts = [(R * math.cos(math.radians(-90 + 72 * i)), R * math.sin(math.radians(-90 + 72 * i))) for i in range(5)]
        for p0, p1 in zip(pts, pts[1:] + pts[:1]):
            seg_box(sc, p0, p1, 6, h, wall, WINDOWS, b)
    G.box(sc, -8, -8, 8, 8, 0, 0.3, GRASS, PLAIN, nb(sc))


@bicon('BLDG_Wall_Street')
def _(sc, rng):
    plinth(sc, -28, -28, 28, 28, top=lin('a8a49a'), mat=PAVING)
    BD.tower_block(sc, (-24, -24, -8, -8), 80, P, rng, glass=False, crown=lin('8a8e92'))
    BD.tower_block(sc, (-2, -24, 14, -10), 60, P, rng, glass=True)
    BD.tower_block(sc, (-24, -2, -10, 12), 54, P, rng, glass=True)
    b = nb(sc, wspace=6, floor=12)
    G.box(sc, 2, 2, 20, 20, 0, 18, lin('d8d0bc'), STONE, b)
    G.column_row(sc, 22, 4, 22, 18, 0, 16, 1.1, 5, MARBLE, PLAIN, nb(sc))
    G.box(sc, 20, 2, 23, 20, 16, 19, MARBLE, PLAIN, nb(sc))


@bicon('BLDG_Apollo_Project', fill=0.9)
def _(sc, rng):
    plinth(sc, -26, -26, 26, 26, top=lin('a8a49a'), mat=PAVING)
    b = nb(sc)
    # the launch tower
    for (sx, sy) in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        beam(sc, (-12 + sx * 3, -12 + sy * 3, 0), (-12 + sx * 3, -12 + sy * 3, 78), 0.4, lin('c0503e'), METAL, b)
    for z in range(6, 78, 8):
        for (a, c) in (((-3, -3), (3, -3)), ((3, -3), (3, 3)), ((3, 3), (-3, 3)), ((-3, 3), (-3, -3))):
            beam(sc, (-12 + a[0], -12 + a[1], z), (-12 + c[0], -12 + c[1], z), 0.3, lin('c0503e'), METAL, b)
    # the rocket
    br = nb(sc)
    G.lathe(sc, 0, 0, [(5, 2), (5, 44), (4.2, 50), (4.2, 60), (3, 64), (2.2, 70), (0.6, 76), (0.0, 80)],
            lin('f0efe8'), METAL, br, seg=24)
    for z in (14, 30, 46):
        G.lathe(sc, 0, 0, [(5.05, z), (5.05, z + 3)], lin('2a2a2e'), PLAIN, br, seg=24)
    for i in range(4):
        a = math.radians(45 + 90 * i)
        sc.quad((5 * math.cos(a), 5 * math.sin(a), 2), (9 * math.cos(a), 9 * math.sin(a), 0), (9 * math.cos(a), 9 * math.sin(a), 6),
                (5 * math.cos(a), 5 * math.sin(a), 12), lin('2a2a2e'), PLAIN, br)
    beam(sc, (-9, -9, 60), (-3, -3, 60), 0.6, lin('c0503e'), METAL, nb(sc))


@bicon('BLDG_SDI')
def _(sc, rng):
    plinth(sc, -28, -26, 28, 26)
    b = nb(sc)
    G.lathe(sc, -6, -6, [(12, 0), (12, 4)], lin('c8c4bc'), STONE, b, seg=28)
    G.lathe(sc, -6, -6, [(12.5, 4)] + [(13 * math.cos(t), 4 + 13 * math.sin(t) * 1.05) for t in np.linspace(0.15, math.pi / 2, 10)],
            lin('f0efe8'), DOME, b, seg=32)
    for (x, y) in ((16, -14), (16, 2), (2, 16)):
        bs = nb(sc)
        G.lathe(sc, x, y, [(3.5, 0), (3.5, 1)], lin('8a8e86'), METAL, bs, seg=16)
        G.lathe(sc, x, y, [(1.4, 1), (1.4, 14), (0.0, 18)], lin('ecebe6'), METAL, bs, seg=12)
        G.lathe(sc, x, y, [(1.45, 14), (0.0, 18.05)], lin('c0503e'), PLAIN, bs, seg=12)


@bicon('BLDG_Intelligence_Center')
def _(sc, rng):
    plinth(sc, -28, -26, 28, 26)
    b = nb(sc, wspace=3.5, floor=4.5)
    G.box(sc, -22, -20, 12, 10, 0, 22, lin('3e4652'), GLASS, b, top=lin('6a7076'))
    G.box(sc, -22.2, -20.2, 12.2, 10.2, 21, 23, lin('8a8e92'), PLAIN, b)
    for (x, y, s) in ((-14, -12, 1.0), (2, -12, 0.8)):
        bd = nb(sc)
        G.lathe(sc, x, y, [(0.7 * s, 23), (0.7 * s, 27)], lin('8a8e92'), METAL, bd, seg=8)
        G.lathe(sc, x, y, [(0.0, 27), (4 * s, 28.5), (6 * s, 31)], lin('e8e6e0'), METAL, bd, seg=16)
    bt = nb(sc)
    G.lathe(sc, 6, 4, [(0.6, 23), (0.2, 40)], STEEL, METAL, bt, seg=6)
    G.box(sc, 14, -8, 22, 8, 0, 3, lin('2a2a30'), PLAIN, nb(sc))


@bicon('BLDG_Battlefield_Medicine')
def _(sc, rng):
    plinth(sc, -28, -24, 28, 24, top=lin('8a8a6a'))
    tent = lin('d8d2bc')
    for (x0, x1, y0, y1) in ((-22, 6, -16, 0), (-12, 12, 6, 20)):
        b = nb(sc)
        G.gable(sc, x0, y0, x1, y1, 0, 12, tent, PLAIN, b, axis='x', over=0.5, gable_col=tent, gable_mat=PLAIN)
    bc = nb(sc)
    # red crosses on the tent roofs
    G.box(sc, 6.2, -10, 6.5, -6, 2, 10, RED, PLAIN, bc)
    G.box(sc, 6.2, -12, 6.5, -4, 4.5, 7.5, RED, PLAIN, bc)
    G.box(sc, 12.2, 11, 12.5, 15, 2, 10, RED, PLAIN, bc)
    G.box(sc, 12.2, 9, 12.5, 17, 4.5, 7.5, RED, PLAIN, bc)
    flag(sc, 18, -16, 0, 20, lin('f0ece0'))
    for i in range(3):
        G.box(sc, 16 + i * 0, -4 + i * 5, 24, -1 + i * 5, 0, 1.5, lin('6a7050'), PLAIN, nb(sc))


@bicon('BLDG_Secret_Police_HQ')
def _(sc, rng):
    plinth(sc, -28, -26, 28, 26, top=lin('8a8680'), mat=PAVING)
    stone = lin('6e6a66')
    b = nb(sc, wspace=4.5, floor=7, wdark=0.2)
    G.box(sc, -22, -20, 14, 12, 0, 28, stone, WINDOWS, b, top=lin('4a4846'))
    G.parapet(sc, -22, -20, 14, 12, 28, 2, stone, STONE, b)
    G.box(sc, 14, -6, 18, 6, 0, 8, lin('3a3836'), PLAIN, nb(sc))
    for (x, y) in ((-20, -18), (12, 10)):
        bl = nb(sc)
        G.lathe(sc, x, y, [(0.5, 30), (0.5, 34)], lin('3a3a3a'), METAL, bl, seg=6)
        beam(sc, (x, y, 34), (x + 3, y + 3, 33), 1.4, lin('3a3a3a'), METAL, bl, seg=8)
    G.lathe(sc, -4, -4, [(0.8, 30), (0.2, 50)], lin('3a3a3a'), METAL, nb(sc), seg=6)
    for i in range(8):
        G.box(sc, -26 + i * 6, 18, -25 + i * 6, 19, 0, 8, lin('3a3a3a'), METAL, nb(sc))
    G.box(sc, -26, 18.2, 20, 18.8, 6, 6.5, lin('3a3a3a'), METAL, nb(sc))
