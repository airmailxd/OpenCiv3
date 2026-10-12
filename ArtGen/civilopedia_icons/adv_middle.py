"""Advance icons of the middle ages (registered with advances.icon)."""
import math

import numpy as np

import kit3d as K
import mesh as M
from advances import icon


@icon('TECH_Monotheism', yaw=-8, pitch=8)
def _(k):
    # a rose window of stained glass in a stone ring
    n = 220
    im_r, dr = K.canvas(n)
    dr.ellipse((6, 6, n - 6, n - 6), fill=255)
    ring = K.mask_of(im_r)
    glass = {}
    cols = [('#c0503e', 'gl_red'), ('#4f74b0', 'gl_blue'), ('#d8b04a', 'gl_gold'), ('#5f9a62', 'gl_green')]
    masks = [K.canvas(n) for _ in cols]
    c = n / 2
    for i in range(12):
        a = 2 * math.pi * i / 12
        x, y = c + 70 * math.cos(a), c + 70 * math.sin(a)
        masks[i % 2][1].ellipse((x - 20, y - 20, x + 20, y + 20), fill=255)
        a2 = a + math.pi / 12
        x2, y2 = c + 40 * math.cos(a2), c + 40 * math.sin(a2)
        masks[2][1].ellipse((x2 - 10, y2 - 10, x2 + 10, y2 + 10), fill=255)
    masks[3][1].ellipse((c - 20, c - 20, c + 20, c + 20), fill=255)
    layers = [(ring, 'stone', 0.12, 5.0)]
    for (hexc, name), (im, _) in zip(cols, masks):
        layers.append((K.mask_of(im), k.m(name, '#' + hexc.lstrip('#'), spec=0.7, gloss=30), -0.04, 1.5))
    k.relief(layers, 2.0)


@icon('TECH_Feudalism', yaw=-28, pitch=4, ground=True)
def _(k):
    # a castle keep with corner turrets
    k.box((1.0, 1.0, 1.2), 'stone', center=(0, 0, 0.6))
    for sx in (-1, 1):
        for sy in (-1, 1):
            k.cyl((sx * 0.5, sy * 0.5, 0), (sx * 0.5, sy * 0.5, 1.5), 0.2, 'dark_stone', seg=16)
            k.add(M.cone(0.26, 0.4, k.m('dark_red'), seg=16), M.translate(sx * 0.5, sy * 0.5, 1.5))
    for i in range(4):
        x = -0.3 + i * 0.2
        k.box((0.12, 1.0, 0.14), 'stone', center=(x, 0, 1.27))
        k.box((1.0, 0.12, 0.14), 'stone', center=(0, x, 1.27))
    k.box((0.3, 0.05, 0.45), 'dark_wood', center=(0, -0.51, 0.22))


@icon('TECH_Engineering', yaw=-15, pitch=10)
def _(k):
    # drafting dividers over a set square
    k.extrude([(-0.9, -0.6), (0.6, -0.6), (-0.9, 0.7)], 0.05, 'light_wood', M.translate(0, 0.1, 0))
    k.extrude([(-0.72, -0.45), (0.2, -0.45), (-0.72, 0.38)], 0.07, k.m('hole', '#c9b48a'), M.translate(0, 0.09, 0))
    top = np.array((0.15, -0.1, 0.9))
    for s in (-1, 1):
        foot = np.array((0.15 + s * 0.55, -0.1, -0.75))
        k.cyl(top, foot, 0.05, 'steel', r1=0.02)
    k.sph(top, 0.1, 'brass')
    k.cyl(top, top + (0, 0, 0.25), 0.03, 'brass')


@icon('TECH_Theology', yaw=-10, pitch=6, ground=True)
def _(k):
    # a church bell under a little wooden beam
    k.lathe([(0, 0.62), (0.08, 0.6), (0.25, 0.48), (0.55, 0.36), (0.85, 0.3), (0.98, 0.2), (1.02, 0.001)],
            'bronze', M.translate(0, 0, 0.2), seg=40)
    k.lathe([(0, 0.6), (0.06, 0.64)], 'bronze', M.translate(0, 0, 0.2), seg=40)
    k.sph((0, 0, 0.25), 0.12, 'iron')
    k.box((1.6, 0.18, 0.18), 'dark_wood', center=(0, 0, 1.35))
    for s in (-1, 1):
        k.box((0.16, 0.16, 1.4), 'dark_wood', center=(s * 0.75, 0, 0.7))


@icon('TECH_Chivalry', yaw=-25, pitch=4, ground=True)
def _(k):
    # a knight's great helm with a plume
    k.lathe([(0, 0.42), (0.7, 0.42), (0.9, 0.38), (1.0, 0.25), (1.03, 0.001)], 'steel', seg=32)
    im, d = K.canvas(160)
    d.rectangle((20, 66, 140, 76), fill=255)
    d.rectangle((75, 66, 85, 120), fill=255)
    for y in (90, 100, 110):
        for x in range(50, 112, 12):
            d.ellipse((x, y, x + 4, y + 4), fill=255)
    slit = K.mask_of(im)
    k.relief([(slit, 'black', 0.03, 1.0)], 0.85, M.translate(0, -0.42, 0.5))
    for i in range(5):
        k.sph((0.05 * i - 0.1, 0.1 + 0.08 * i, 1.08 + 0.05 * math.sin(i)), 0.14 - 0.012 * i, 'red', seg=14, scale=(0.8, 1.3, 1))


@icon('TECH_Invention', yaw=-20, pitch=6)
def _(k):
    # two meshing gears
    k.extrude(K.gear_poly(12, 0.62, 0.5, 0.45), 0.16, 'brass', M.translate(-0.3, 0, 0.2))
    k.cyl((-0.3, -0.1, 0.2), (-0.3, 0.1, 0.2), 0.14, 'dark_metal')
    k.extrude(K.gear_poly(8, 0.42, 0.32, 0.45, rot=0.2), 0.14, 'bronze', M.translate(0.6, 0.0, -0.42))
    k.cyl((0.6, -0.1, -0.42), (0.6, 0.1, -0.42), 0.1, 'dark_metal')


@icon('TECH_Printing_Press', yaw=-28, pitch=4, ground=True)
def _(k):
    # a wooden screw press
    for s in (-1, 1):
        k.box((0.16, 0.5, 1.7), 'dark_wood', center=(s * 0.6, 0, 0.85))
    k.box((1.4, 0.5, 0.18), 'dark_wood', center=(0, 0, 1.6))
    k.box((1.3, 0.7, 0.12), 'wood', center=(0, 0, 0.5))
    k.box((1.0, 0.55, 0.04), 'paper', center=(0, 0, 0.58))
    k.box((0.8, 0.5, 0.14), 'wood', center=(0, 0, 0.95))
    k.cyl((0, 0, 1.02), (0, 0, 1.6), 0.07, 'iron')
    for i in range(6):
        k.torus(0.08, 0.02, 'iron', M.translate(0, 0, 1.1 + i * 0.08))
    k.cyl((-0.45, 0, 1.25), (0.45, 0, 1.25), 0.035, 'iron')


@icon('TECH_Music_Theory', yaw=-12, pitch=6)
def _(k):
    # notes on a staff
    n = 220
    im, d = K.canvas(n)
    for i in range(5):
        y = 70 + i * 22
        d.line((8, y, n - 8, y), fill=255, width=3)
    staff = K.mask_of(im)
    im2, d2 = K.canvas(n)
    for (x, y) in ((50, 158), (120, 136), (178, 114)):
        d2.ellipse((x - 16, y - 12, x + 16, y + 12), fill=255)
        d2.rectangle((x + 11, y - 92, x + 17, y), fill=255)
    d2.polygon([(128 + 3, 44), (194, 22), (194, 38), (128 + 3, 60)], fill=255)
    d2.polygon([(56, 66), (90, 82), (90, 96), (56, 82)], fill=255)
    notes = K.mask_of(im2)
    k.relief([(staff & ~notes, 'ink', 0.03, 1.0), (notes, 'black', 0.12, 3.0)], 2.0)


@icon('TECH_Education', yaw=-25, pitch=-8, ground=True)
def _(k):
    # a mortarboard on a stack of books
    for i, (c, rot) in enumerate((('dark_red', 0), ('navy', 6), ('dark_green', -4))):
        k.box((1.3, 0.85, 0.2), c, center=(0, 0, 0.1 + i * 0.21), T=M.rot_z(rot), bevel=0.02)
        k.box((1.24, 0.8, 0.16), 'paper', center=(0.04, 0, 0.1 + i * 0.21), T=M.rot_z(rot))
    k.lathe([(0, 0.32), (0.25, 0.3)], 'black', M.translate(0, 0, 0.63), seg=24)
    k.box((1.1, 1.1, 0.05), 'black', center=(0, 0, 0.9), T=M.rot_z(45))
    k.sph((0, 0, 0.94), 0.05, 'gold')
    k.cyl((0, 0, 0.93), (0.42, -0.42, 0.9), 0.015, 'gold')
    k.cyl((0.42, -0.42, 0.9), (0.42, -0.42, 0.6), 0.02, 'gold')
    k.lathe([(0, 0.02), (0.15, 0.05), (0.18, 0.001)], 'gold', M.translate(0.42, -0.42, 0.48), seg=10)


@icon('TECH_Gunpowder', yaw=-25, pitch=6, ground=True)
def _(k):
    # a powder keg with a lit fuse
    prof = [(0, 0.001), (0.0, 0.4), (0.4, 0.48), (0.7, 0.5), (1.0, 0.48), (1.4, 0.4), (1.4, 0.001)]
    k.lathe(prof, 'wood', seg=36)
    for z in (0.15, 0.45, 0.95, 1.25):
        r = 0.4 + 0.1 * math.sin(math.pi * z / 1.4) + 0.012
        k.torus(r, 0.03, 'iron', M.translate(0, 0, z), seg=36)
    k.lathe([(1.4, 0.36), (1.42, 0.001)], 'dark_wood', seg=36)
    k.cyl((0.1, 0, 1.4), (0.35, -0.1, 1.75), 0.025, 'rope')
    k.sph((0.37, -0.11, 1.8), 0.08, 'flame')
    k.sph((0.37, -0.15, 1.8), 0.04, 'flame_core')
    k.sph((0.55, -0.7, 0.12), 0.12, 'black')
    k.sph((0.28, -0.8, 0.12), 0.12, 'black')
    k.sph((0.42, -0.75, 0.32), 0.12, 'black')


@icon('TECH_Banking', yaw=-28, pitch=6, ground=True)
def _(k):
    # a stack of gold bars
    for r, n in enumerate((3, 2, 1)):
        for i in range(n):
            x = (i - (n - 1) / 2) * 0.62
            k.add(M.loft([(0, 0.42, 0.25), (0.22, 0.34, 0.18)], k.m('gold'), seg=4, phase=math.pi / 4, smooth=False),
                  M.translate(x, 0, r * 0.23))


@icon('TECH_Astronomy', yaw=-30, pitch=4, ground=True)
def _(k):
    # a brass telescope on a tripod
    p = np.array((0.0, 0.0, 1.0))
    for a in (90, 210, 330):
        r = math.radians(a)
        k.cyl(p, (0.6 * math.cos(r), 0.6 * math.sin(r), 0), 0.03, 'dark_wood')
    d = np.array((0.75, 0.3, 0.55))
    d /= np.linalg.norm(d)
    k.cyl(p - d * 0.7, p + d * 0.9, 0.1, 'brass', r1=0.13)
    k.cyl(p - d * 0.95, p - d * 0.7, 0.06, 'brass')
    k.cyl(p + d * 0.9, p + d * 1.0, 0.15, 'dark_metal')
    k.sph(p, 0.07, 'dark_metal')


@icon('TECH_Chemistry', yaw=-10, pitch=4, ground=True)
def _(k):
    # a conical flask with green liquid and a test tube
    glass = k.m('flask', '#cfe0e4', spec=0.9, gloss=40)
    k.lathe([(0, 0.5), (0.05, 0.52), (0.5, 0.3), (0.85, 0.12), (1.3, 0.12), (1.35, 0.15), (1.37, 0.001)], glass, seg=36)
    k.lathe([(0.02, 0.51), (0.06, 0.53), (0.42, 0.335), (0.43, 0.001)], k.m('liquid', '#6fae58', spec=0.5, gloss=30),
            M.translate(0, -0.012, 0), seg=36)
    for i, (x, z, r) in enumerate(((0.05, 0.62, 0.05), (-0.08, 0.75, 0.035), (0.04, 0.95, 0.04))):
        k.sph((x, -0.05, z), r, k.m('bubble', '#d8f0c8', spec=0.5))
    k.lathe([(0, 0.08), (0.9, 0.08), (0.95, 0.1), (0.96, 0.001)], glass, M.translate(0.75, 0.2, 0) @ M.rot_y(14), seg=20)
    k.lathe([(0, 0.075), (0.35, 0.075), (0.36, 0.001)], k.m('liquid2', '#b05a8a', spec=0.5), M.translate(0.75, 0.19, 0) @ M.rot_y(14), seg=20)


@icon('TECH_Democracy', yaw=-28, pitch=-6, ground=True)
def _(k):
    # a ballot box with a ballot going in
    k.box((1.1, 0.8, 0.9), 'dark_wood', center=(0, 0, 0.45), bevel=0.03)
    k.box((1.16, 0.86, 0.08), 'wood', center=(0, 0, 0.92))
    k.box((0.5, 0.06, 0.02), 'black', center=(0, 0, 0.965))
    k.box((0.4, 0.03, 0.5), 'paper', center=(0, 0, 1.15), T=M.rot_y(4))
    im, d = K.canvas(100)
    d.line((30, 50, 45, 65), fill=255, width=8)
    d.line((45, 65, 72, 30), fill=255, width=8)
    k.relief([(K.mask_of(im), 'red', 0.01, 1.0)], 0.36, M.translate(0, -0.02, 1.17))
    im2, d2 = K.canvas(160)
    d2.polygon(K.star_points(80, 80, 60, 24), fill=255)
    k.relief([(K.mask_of(im2), 'gold', 0.03, 2.0)], 0.5, M.translate(-0.1, -0.41, 0.45) @ M.rot_z(0))


@icon('TECH_Economics', yaw=-12, pitch=4, ground=True)
def _(k):
    # a balance scale weighing coins against goods
    k.lathe([(0, 0.4), (0.06, 0.38), (0.1, 0.1), (1.4, 0.06), (1.45, 0.001)], 'brass', seg=24)
    k.cyl((-0.95, 0, 1.38), (0.95, 0, 1.38), 0.035, 'brass')
    k.sph((0, 0, 1.45), 0.07, 'brass')
    for s, dz in ((-1, -0.08), (1, 0.08)):
        x = s * 0.9
        top = (x, 0, 1.38 + dz * 0)
        for a in (0, 120, 240):
            r = math.radians(a)
            k.cyl(top, (x + 0.28 * math.cos(r), 0.28 * math.sin(r), 0.75 + dz), 0.008, 'brass')
        k.lathe([(0, 0.001), (0.0, 0.2), (0.06, 0.32), (0.08, 0.001)], 'brass', M.translate(x, 0, 0.72 + dz), seg=24)
    for i in range(4):
        k.cyl((-0.9, 0, 0.66 + 0.06 * i + 0.1), (-0.9, 0, 0.71 + 0.06 * i + 0.1), 0.15, 'gold', seg=20)
    k.box((0.3, 0.25, 0.22), 'cloth', center=(0.9, 0, 0.98), T=M.rot_z(15), bevel=0.03)


@icon('TECH_Navigation', yaw=-15, pitch=8)
def _(k):
    # a ship's wheel
    T = M.rot_x(90)
    k.torus(0.72, 0.07, 'wood', T, seg=40)
    k.torus(0.42, 0.04, 'wood', T, seg=32)
    k.cyl((0, -0.12, 0), (0, 0.12, 0), 0.16, 'brass')
    for i in range(8):
        a = 2 * math.pi * i / 8
        c, s = math.cos(a), math.sin(a)
        k.cyl((0.12 * c, 0, 0.12 * s), (0.78 * c, 0, 0.78 * s), 0.035, 'wood')
        k.add(M.lathe([(0, 0.04), (0.05, 0.06), (0.18, 0.05), (0.26, 0.07), (0.3, 0.001)], k.m('dark_wood'), seg=12),
              M.look_along((0.78 * c, 0, 0.78 * s), (1.0 * c, 0, 1.0 * s)) @ M.scale(1, 1, 3.3))


@icon('TECH_Physics', yaw=-22, pitch=4, ground=True)
def _(k):
    # Newton's cradle
    k.box((1.8, 0.8, 0.08), 'dark_wood', center=(0, 0, 0.04))
    for sx in (-0.8, 0.8):
        for sy in (-0.32, 0.32):
            k.cyl((sx, sy, 0.08), (sx, sy, 1.3), 0.03, 'steel')
        k.cyl((sx, -0.32, 1.3), (sx, 0.32, 1.3), 0.03, 'steel')
    k.cyl((-0.8, -0.32, 1.3), (0.8, -0.32, 1.3), 0.03, 'steel')
    k.cyl((-0.8, 0.32, 1.3), (0.8, 0.32, 1.3), 0.03, 'steel')
    for i in range(5):
        x = -0.4 + i * 0.2
        c = np.array((x, 0, 0.45))
        if i == 0:
            c = np.array((x - 0.45, 0, 0.6))
        for sy in (-0.32, 0.32):
            k.cyl(c, (x, sy, 1.3), 0.005, 'grey')
        k.sph(c, 0.1, 'silver', seg=18)


@icon('TECH_Metallurgy', yaw=-30, pitch=4, ground=True)
def _(k):
    # a cast bronze cannon on its carriage, with shot
    T = M.translate(0.0, -0.2, 0.62) @ M.rot_x(-80)          # barrel along -y (toward the viewer), raised a little
    k.lathe([(-0.75, 0.001), (-0.72, 0.17), (-0.6, 0.2), (-0.5, 0.2), (-0.45, 0.24), (0.0, 0.21), (0.9, 0.15),
             (0.95, 0.18), (1.05, 0.18), (1.07, 0.11), (1.0, 0.07)], 'bronze', T, seg=32)
    k.sph(np.array((0.0, 0.0, 0.62)) + (T[:3, :3] @ np.array((0, 0, -0.82))), 0.08, 'bronze')
    k.cyl((-0.28, -0.25, 0.62), (0.28, -0.25, 0.62), 0.06, 'bronze')
    for s in (-1, 1):
        k.extrude([(-0.55, 0.0), (0.55, 0.0), (0.45, 0.25), (-0.1, 0.62), (-0.5, 0.45)], 0.08, 'dark_wood',
                  M.translate(s * 0.26, 0.1, 0) @ M.rot_z(90))
        k.cyl((s * 0.36, 0.0, 0.3), (s * 0.46, 0.0, 0.3), 0.3, 'dark_wood', seg=24)
        k.torus(0.3, 0.025, 'iron', M.translate(s * 0.47, 0.0, 0.3) @ M.rot_y(90), seg=24, tube=6)
    k.cyl((-0.42, 0, 0.3), (0.42, 0, 0.3), 0.05, 'iron')
    for (x, y, z) in ((0.75, -0.55, 0.1), (0.95, -0.4, 0.1), (0.85, -0.47, 0.27)):
        k.sph((x, y, z), 0.1, 'black', seg=14)


@icon('TECH_Free_Artistry', yaw=-10, pitch=-30)
def _(k):
    # a painter's palette with paints and a brush
    im, d = K.canvas(220)
    d.ellipse((10, 30, 210, 190), fill=255)
    hole, dh = K.canvas(220)
    dh.ellipse((40, 120, 75, 155), fill=255)
    pal = K.mask_of(im) & ~K.mask_of(hole)
    paints = []
    for (x, y), name, col in (((70, 70), 'p_red', '#c0453a'), ((115, 55), 'p_yel', '#e0c040'), ((160, 70), 'p_blu', '#4a6fb5'),
                              ((175, 115), 'p_grn', '#4f9a50'), ((140, 150), 'p_wht', '#f0ece4')):
        imp, dp = K.canvas(220)
        dp.ellipse((x - 17, y - 14, x + 17, y + 14), fill=255)
        paints.append((K.mask_of(imp), k.m(name, col, spec=0.4, gloss=20), 0.06, 4.0))
    k.relief([(pal, 'light_wood', 0.06, 3.0)] + paints, 2.0, M.rot_x(-90))
    k.cyl((-0.2, 0.3, 0.15), (0.9, -0.6, 0.15), 0.035, 'dark_red')
    k.cyl((-0.35, 0.42, 0.15), (-0.2, 0.3, 0.15), 0.045, 'silver')
    k.lathe([(0, 0.045), (0.12, 0.05), (0.2, 0.001)], 'black', M.look_along((-0.35, 0.42, 0.15), (-0.5, 0.54, 0.15)) @ M.scale(1, 1, 5))


@icon('TECH_Theory_of_Gravity', yaw=-10, pitch=-20)
def _(k):
    # an orrery: the sun and planets on their orbits
    k.sph((0, 0, 0), 0.3, k.m('sun', '#f0c050', spec=0.3), seg=24)
    for r, (pc, pr, a) in zip((0.55, 0.85, 1.15), (('blue', 0.08, 30), ('red', 0.07, 160), ('purple', 0.11, 250))):
        k.torus(r, 0.012, 'brass', seg=64, tube=6)
        k.sph((r * math.cos(math.radians(a)), r * math.sin(math.radians(a)), 0), pr, pc, seg=16)


@icon('TECH_Magnetism', yaw=-10, pitch=6)
def _(k):
    # a horseshoe magnet
    n = 220
    im, d = K.canvas(n)
    d.arc((30, 20, 190, 180), 180, 360, fill=255, width=50)
    d.rectangle((30, 100, 80, 200), fill=255)
    d.rectangle((140, 100, 190, 200), fill=255)
    body = K.mask_of(im)
    im2, d2 = K.canvas(n)
    d2.rectangle((30, 160, 80, 200), fill=255)
    d2.rectangle((140, 160, 190, 200), fill=255)
    tips = K.mask_of(im2)
    k.relief([(body & ~tips, 'red', 0.3, 8.0), (tips, 'silver', 0.3, 8.0)], 2.0)
    for i in range(3):
        k.sph((-0.1 + 0.15 * i, -0.2, -1.15 - 0.05 * (i % 2)), 0.04, 'dark_metal', seg=8)


@icon('TECH_Military_Tradition', yaw=-12, pitch=6)
def _(k):
    # a medal on a ribbon
    n = 220
    im, d = K.canvas(n)
    d.polygon([(70, 4), (150, 4), (140, 92), (80, 92)], fill=255)
    rib = K.mask_of(im)
    im2, d2 = K.canvas(n)
    for x in (98, 112):
        d2.rectangle((x, 4, x + 8, 92), fill=255)
    stripes = K.mask_of(im2) & rib
    im3, d3 = K.canvas(n)
    d3.polygon(K.star_points(110, 148, 66, 30, 5), fill=255)
    star = K.mask_of(im3)
    im4, d4 = K.canvas(n)
    d4.ellipse((88, 126, 132, 170), fill=255)
    core = K.mask_of(im4)
    k.relief([(rib & ~stripes, 'red', 0.06, 2.0), (stripes, 'white', 0.06, 2.0), (star, 'gold', 0.16, 6.0),
              (core, 'red', 0.04, 2.0)], 2.0)
