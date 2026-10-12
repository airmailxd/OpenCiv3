"""Advance icons of the ancient era (registered with advances.icon)."""
import math

import numpy as np

import kit3d as K
import mesh as M
from advances import icon


@icon('TECH_Warrior_Code', yaw=-15, pitch=5)
def _(k):
    # a round hide shield with two crossed spears behind it
    for s in (-1, 1):
        a, b = np.array((s * 0.9, 0.15, -0.9)), np.array((-s * 0.9, 0.15, 0.95))
        k.cyl(a, b, 0.035, 'wood')
        d = (b - a) / np.linalg.norm(b - a)
        k.add(M.cone(0.08, 1.0, k.m('bronze'), seg=10), M.look_along(b, b + d * 0.34))
    k.add(M.disc(0.62, 0.62, 0.06, k.m('leather'), seg=32, dome=0.12))
    k.torus(0.6, 0.04, 'bronze', M.translate(0, -0.03, 0) @ M.rot_x(90))
    k.sph((0, -0.17, 0), 0.12, 'bronze')


@icon('TECH_Ceremonial_Burial', yaw=-28, pitch=4, ground=True)
def _(k):
    # a dolmen: two standing stones and a capstone
    k.box((0.32, 0.5, 1.0), 'dark_stone', center=(-0.55, 0, 0.5), bevel=0.06)
    k.box((0.32, 0.5, 0.95), 'stone', center=(0.55, 0, 0.47), bevel=0.06)
    k.box((1.7, 0.75, 0.28), 'stone', center=(0.0, 0, 1.08), T=M.rot_y(-4), bevel=0.08)
    k.box((0.25, 0.4, 0.55), 'dark_stone', center=(1.15, -0.4, 0.27), T=M.rot_z(20), bevel=0.05)


@icon('TECH_Iron_Working', yaw=-25, pitch=6, ground=True)
def _(k):
    # an anvil with a hammer resting on it
    k.box((0.7, 0.5, 0.35), 'dark_wood', center=(0, 0, 0.175))
    k.box((0.42, 0.32, 0.3), 'iron', center=(0, 0, 0.5))
    k.box((1.1, 0.42, 0.2), 'iron', center=(0.05, 0, 0.75), bevel=0.02)
    k.lathe([(0, 0.21), (0.35, 0.12), (0.5, 0.001)], 'iron', M.translate(0.6, 0, 0.75) @ M.rot_y(90), seg=20)
    k.box((0.18, 0.2, 0.32), 'dark_metal', center=(-0.15, -0.05, 0.98), T=M.rot_z(30))
    k.cyl((-0.15, -0.05, 0.98), (0.5, -0.5, 0.92), 0.035, 'wood')


@icon('TECH_Writing', yaw=-15, pitch=12)
def _(k):
    # a scroll and a quill
    k.box((1.4, 0.04, 1.0), 'parchment', center=(0, 0.05, 0))
    for z in (0.56, -0.56):
        k.cyl((-0.78, 0.05, z), (0.78, 0.05, z), 0.09, 'parchment')
        for x in (-0.84, 0.84):
            k.cyl((x * 0.95, 0.05, z), (x * 1.04, 0.05, z), 0.05, 'dark_wood')
    im, d = K.canvas(128)
    for i, y in enumerate(range(28, 104, 14)):
        d.line((22, y, 106 - (i % 3) * 14, y), fill=255, width=4)
    k.relief([(K.mask_of(im), 'ink', 0.01, 1.0)], 1.25, M.translate(0, 0.02, 0))
    quill = [(0, 0), (0.08, 0.25), (0.1, 0.7), (0.05, 1.1), (0, 1.2), (-0.06, 1.0), (-0.07, 0.5), (-0.03, 0.15)]
    k.extrude(quill, 0.01, 'white', M.translate(0.45, -0.12, -0.6) @ M.rot_y(-30))


@icon('TECH_Mysticism', yaw=0, pitch=4, ground=True)
def _(k):
    # a crystal ball on a bronze stand
    k.lathe([(0, 0.4), (0.05, 0.42), (0.12, 0.3), (0.3, 0.18), (0.4, 0.28), (0.42, 0.001)], 'bronze', seg=32)
    k.sph((0, 0, 0.82), 0.45, k.m('crystal', '#9fb8d8', spec=0.9, gloss=40), seg=32)
    k.sph((0.06, 0.0, 0.8), 0.17, k.m('mist', '#d8c8f0', spec=0.2), seg=16, scale=(1.4, 1, 0.8))
    k.sph((-0.15, -0.38, 0.95), 0.08, k.m('glint', '#f4f0ff', spec=0.2), seg=12)


@icon('TECH_Mathematics', yaw=-22, pitch=8)
def _(k):
    # an abacus
    k.box((1.6, 0.14, 0.12), 'dark_wood', center=(0, 0, 0.62))
    k.box((1.6, 0.14, 0.12), 'dark_wood', center=(0, 0, -0.62))
    k.box((0.12, 0.14, 1.36), 'dark_wood', center=(-0.8, 0, 0))
    k.box((0.12, 0.14, 1.36), 'dark_wood', center=(0.8, 0, 0))
    k.box((1.5, 0.1, 0.06), 'dark_wood', center=(0, 0, 0.25))
    for r, z in enumerate((0.45, 0.05, -0.17, -0.39)):
        k.cyl((-0.74, 0, z), (0.74, 0, z), 0.015, 'brass')
        n = [2, 4, 3, 5][r]
        for i in range(n):
            x = -0.62 + i * 0.17 + (0.35 if r % 2 else 0)
            k.sph((x, 0, z), 0.09, ['red', 'yellow'][(r + i) % 2], seg=14, scale=(0.8, 1, 1))


@icon('TECH_Philosophy', yaw=-12, pitch=8)
def _(k):
    # a silver coin with the owl of wisdom and an olive sprig
    n = 240
    im, d = K.canvas(n)
    d.ellipse((6, 6, n - 6, n - 6), fill=255)
    coin = K.mask_of(im)
    o, do = K.canvas(n)
    do.ellipse((78, 92, 162, 196), fill=255)          # body
    do.ellipse((80, 44, 160, 120), fill=255)          # head
    do.polygon([(84, 60), (90, 30), (104, 52)], fill=255)    # ears
    do.polygon([(156, 60), (150, 30), (136, 52)], fill=255)
    do.polygon([(100, 196), (112, 214), (118, 196), (128, 214), (140, 196)], fill=255)  # feet
    owl = K.mask_of(o)
    e, de = K.canvas(n)
    for x in (100, 140):
        de.ellipse((x - 16, 66, x + 16, 98), fill=255)
    eyes = K.mask_of(e)
    p, dp = K.canvas(n)
    for x in (100, 140):
        dp.ellipse((x - 6, 76, x + 6, 88), fill=255)
    pupils = K.mask_of(p)
    s, ds = K.canvas(n)
    ds.arc((150, 110, 240, 220), 200, 280, fill=255, width=4)
    for (x, y) in ((168, 140), (180, 126), (196, 116), (164, 160)):
        ds.ellipse((x - 9, y - 4, x + 9, y + 4), fill=255)
    sprig = K.mask_of(s) & coin & ~owl
    k.relief([(coin, 'silver', 0.1, 4.0), (owl, 'silver', 0.07, 5.0), (eyes, 'silver', -0.03, 2.0),
              (pupils, 'dark_metal', 0.02, 1.0), (sprig, 'silver', 0.03, 1.5)], 2.0)


@icon('TECH_Code_of_Laws', yaw=-25, pitch=8, ground=True)
def _(k):
    # a judge's gavel resting on its sounding block, by a book of laws
    k.lathe([(0, 0.62), (0.16, 0.62), (0.18, 0.58), (0.2, 0.001)], 'dark_wood', seg=40)
    k.lathe([(0.2, 0.5), (0.22, 0.001)], 'wood', seg=40)
    T = M.translate(-0.05, 0.05, 0.45) @ M.rot_z(25) @ M.rot_y(90)
    k.lathe([(-0.42, 0.001), (-0.4, 0.2), (-0.36, 0.24), (0.36, 0.24), (0.4, 0.2), (0.42, 0.001)], 'dark_wood', T, seg=28)
    for zz in (-0.28, 0.28):
        k.lathe([(zz - 0.03, 0.25), (zz + 0.03, 0.25)], 'brass', T, seg=28)
    k.cyl((-0.05, 0.05, 0.45), (0.5, -1.0, 0.25), 0.08, 'wood', r1=0.1)
    k.sph((0.5, -1.0, 0.25), 0.1, 'wood')
    k.box((0.9, 0.65, 0.2), 'dark_red', center=(0.75, 0.55, 0.1), T=M.rot_z(-12), bevel=0.02)
    k.box((0.86, 0.6, 0.16), 'paper', center=(0.79, 0.55, 0.1), T=M.rot_z(-12))


@icon('TECH_Literature', yaw=-10, pitch=40)
def _(k):
    # an open book, lying open toward the viewer
    for s in (-1, 1):
        prof = [(0.0, 0.0), (0.1, 0.06), (0.4, 0.1), (0.75, 0.08), (0.8, 0.07)]
        top = [(s * x, z) for x, z in prof]
        bot = [(s * x, z - 0.05) for x, z in prof[::-1]]
        poly = top + bot
        if s < 0:
            poly = poly[::-1]
        k.extrude(poly, 1.0, 'paper', M.rot_x(90))
        cov = [(s * x, z - 0.06) for x, z in prof] + [(s * x, z - 0.1) for x, z in prof[::-1]]
        if s < 0:
            cov = cov[::-1]
        k.extrude(cov, 1.06, 'dark_red', M.rot_x(90))
    im, d = K.canvas(200)
    for side in (0, 1):
        for i, y in enumerate(range(40, 170, 16)):
            x0 = 24 + side * 104
            d.line((x0, y, x0 + 62 - (i % 3) * 10, y), fill=255, width=4)
    k.relief([(K.mask_of(im), 'ink', 0.004, 1.0)], 1.6, M.translate(0, 0, 0.11) @ M.rot_x(-90))


@icon('TECH_Map_Making', yaw=-12, pitch=-38)
def _(k):
    # an unrolled map with coasts and a compass rose
    w, h = 1.6, 1.1
    k.box((w, h, 0.02), 'parchment', center=(0, 0, 0))
    for x in (-w / 2, w / 2):
        k.cyl((x, -h / 2 - 0.02, 0.05), (x, h / 2 + 0.02, 0.05), 0.07, 'parchment')
    im, d = K.canvas(256)
    d.polygon([(30, 40), (110, 30), (140, 80), (120, 140), (70, 170), (40, 120)], fill=255)
    d.polygon([(160, 150), (220, 130), (235, 200), (180, 225)], fill=255)
    land = K.mask_of(im)
    im2, d2 = K.canvas(256)
    d2.polygon(K.star_points(195, 60, 34, 9, 4, rot=-90), fill=255)
    rose = K.mask_of(im2)
    k.relief([(land, k.m('mapland', '#a7b07a'), 0.012, 3.0), (rose, 'dark_red', 0.012, 1.5)], w * 0.98,
             M.translate(0, 0, 0.012) @ M.rot_x(-90) @ M.scale(1, 1, h / w))


@icon('TECH_Horseback_Riding', yaw=-15, pitch=6)
def _(k):
    # a horseshoe
    n = 24
    ring = [(math.cos(math.radians(-35 + 250 * i / n)), math.sin(math.radians(-35 + 250 * i / n))) for i in range(n + 1)]
    outer = [(0.78 * c, 0.85 * s) for c, s in ring]
    inner = [(0.48 * c, 0.55 * s) for c, s in ring][::-1]
    im, d = K.canvas(200)
    d.polygon([(100 + 95 * x, 110 - 95 * z) for x, z in outer + inner], fill=255)
    mask = K.mask_of(im)
    holes, dh = K.canvas(200)
    for i in range(1, n, 3):
        c, s = ring[i]
        x, z = 0.64 * c, 0.71 * s
        dh.ellipse((100 + 95 * x - 5, 110 - 95 * z - 5, 100 + 95 * x + 5, 110 - 95 * z + 5), fill=255)
    k.relief([(mask, 'iron', 0.16, 6.0), (K.mask_of(holes) & mask, 'dark_metal', -0.08, 1.0)], 2.0)


@icon('TECH_Polytheism', yaw=-10, pitch=4)
def _(k):
    # a golden thunderbolt
    im, d = K.canvas(200)
    d.polygon([(118, 6), (52, 108), (96, 108), (72, 194), (150, 78), (104, 78), (136, 6)], fill=255)
    k.relief([(K.mask_of(im), 'gold', 0.2, 8.0)], 2.0)


@icon('TECH_The_Republic', yaw=-25, pitch=4, ground=True)
def _(k):
    # a temple front: columns under a pediment
    k.box((2.0, 0.9, 0.12), 'marble', center=(0, 0, 0.06))
    k.box((1.85, 0.8, 0.1), 'marble', center=(0, 0, 0.17))
    for i in range(5):
        x = -0.76 + i * 0.38
        k.lathe([(0.22, 0.1), (0.28, 0.085), (1.1, 0.075), (1.16, 0.1)], 'marble', M.translate(x, -0.3, 0), seg=16)
    k.box((1.95, 0.85, 0.14), 'marble', center=(0, 0, 1.23))
    k.extrude([(-1.0, 0), (1.0, 0), (0, 0.42)], 0.85, 'marble', M.translate(0, 0, 1.3))


@icon('TECH_Monarchy', yaw=-18, pitch=10, ground=True)
def _(k):
    # a crown
    k.lathe([(0, 0.62), (0.3, 0.62), (0.32, 0.6)], 'gold', seg=48)
    for i in range(8):
        a = 2 * math.pi * i / 8
        x, y = 0.6 * math.cos(a), 0.6 * math.sin(a)
        k.add(M.cone(0.13, 0.38, k.m('gold'), seg=10), M.translate(x, y, 0.3))
        k.sph((x * 1.02, y * 1.02, 0.7), 0.06, 'gold', seg=10)
        k.sph((x * 1.04, y * 1.04, 0.15), 0.065, ['red', 'blue'][i % 2], seg=10)
    k.lathe([(0.0, 0.58), (0.28, 0.5), (0.5, 0.001)], 'dark_red', M.translate(0, 0, 0.3), seg=32)


@icon('TECH_Construction', yaw=-22, pitch=4, ground=True)
def _(k):
    # a stone arch with its keystone
    n = 9
    R0, R1 = 0.55, 0.9
    for i in range(n):
        a0, a1 = math.pi * i / n, math.pi * (i + 1) / n
        poly = [(R1 * math.cos(a0), R1 * math.sin(a0)), (R1 * math.cos(a1), R1 * math.sin(a1)),
                (R0 * math.cos(a1), R0 * math.sin(a1)), (R0 * math.cos(a0), R0 * math.sin(a0))]
        k.extrude(poly[::-1], 0.5, 'stone' if i != n // 2 else 'marble', M.translate(0, 0, 0.9))
    for s in (-1, 1):
        for j in range(3):
            k.box((0.34, 0.5, 0.29), ['stone', 'dark_stone'][(j + (s > 0)) % 2], center=(s * 0.725, 0, 0.15 + j * 0.3), bevel=0.02)
