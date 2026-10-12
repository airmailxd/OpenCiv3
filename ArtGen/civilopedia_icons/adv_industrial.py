"""Advance icons of the industrial age (registered with advances.icon)."""
import math

import numpy as np

import kit3d as K
import mesh as M
from advances import icon


def flag_mesh(k, w, h, mat, waves=1.5, amp=0.08, n=24, T=None):
    """A waving cloth in the x-z plane, attached at x = 0."""
    verts, faces = [], []
    for j in range(n + 1):
        for i in range(n + 1):
            u, v = i / n, j / n
            x = u * w
            y = amp * math.sin(2 * math.pi * waves * u) * u
            z = v * h + 0.05 * math.sin(math.pi * u) * u
            verts.append((x, y, z))
    for j in range(n):
        for i in range(n):
            a = j * (n + 1) + i
            faces += [(a, a + 1, a + n + 2), (a, a + n + 2, a + n + 1)]
    m = M.from_indexed(np.array(verts), np.array(faces), k._mat(mat))
    return k.add(m, T)


@icon('TECH_Nationalism', yaw=-20, pitch=4)
def _(k):
    # a flag on a pole
    k.cyl((0, 0, -1.2), (0, 0, 1.0), 0.04, 'dark_wood')
    k.sph((0, 0, 1.03), 0.07, 'gold')
    flag_mesh(k, 1.4, 0.9, 'red', T=M.translate(0.03, 0, 0.05))
    im, d = K.canvas(160)
    d.polygon(K.star_points(80, 80, 60, 24), fill=255)
    k.relief([(K.mask_of(im), 'gold', 0.015, 1.5)], 0.42, M.translate(0.42, -0.06, 0.5))


@icon('TECH_Steam_Power', yaw=-35, pitch=4, ground=True)
def _(k):
    # a steam locomotive
    k.cyl((-0.2, -0.95, 0.55), (-0.2, 0.55, 0.55), 0.32, 'black', seg=28)
    k.cyl((-0.2, -1.0, 0.55), (-0.2, -0.95, 0.55), 0.26, 'dark_metal', seg=28)
    k.box((0.9, 0.7, 0.85), 'dark_red', center=(-0.2, 0.85, 0.75))
    k.box((1.0, 0.8, 0.08), 'black', center=(-0.2, 0.85, 1.2))
    k.box((0.3, 0.05, 0.25), 'glass', center=(-0.2, 0.49, 0.95))
    k.lathe([(0, 0.08), (0.3, 0.1), (0.42, 0.16), (0.45, 0.001)], 'black', M.translate(-0.2, -0.7, 0.8), seg=16)
    k.sph((-0.2, -0.2, 0.88), 0.13, 'brass')
    k.add(M.cone(0.42, 0.35, k.m('dark_metal'), seg=4), M.translate(-0.2, -1.15, 0.12) @ M.rot_x(-60) @ M.scale(1, 1, 1))
    for y in (-0.6, -0.1, 0.4):
        for x in (-0.55, 0.15):
            k.cyl((x, y, 0.25), (x + (0.06 if x > 0 else -0.06), y, 0.25), 0.22 if y > -0.6 else 0.15, 'dark_red', seg=20)
    k.cyl((-0.6, -0.6, 0.27), (-0.6, 0.4, 0.27), 0.03, 'steel')
    for i in range(3):
        k.sph((-0.2 + 0.1 * i, -0.75 - 0.05 * i, 1.4 + 0.25 * i), 0.14 + 0.06 * i, k.m('steam', '#e8e6e0', spec=0.0))


@icon('TECH_Medicine', yaw=-18, pitch=6, ground=True)
def _(k):
    # a mortar and pestle with herbs
    k.lathe([(0, 0.32), (0.06, 0.42), (0.3, 0.5), (0.55, 0.55), (0.58, 0.5), (0.25, 0.4), (0.12, 0.001)], 'marble', seg=36)
    k.cyl((0.0, -0.1, 0.25), (0.45, 0.25, 1.05), 0.07, 'marble', r1=0.05)
    k.sph((0.0, -0.1, 0.25), 0.1, 'marble')
    for (x, y, a) in ((-0.65, -0.35, 20), (-0.5, -0.55, -30), (0.62, -0.45, 60)):
        k.add(M.ellipsoid(0.16, 0.06, 0.02, k.m('leaf')), M.translate(x, y, 0.03) @ M.rot_z(a))


@icon('TECH_Communism', yaw=-10, pitch=6)
def _(k):
    # a red star
    im, d = K.canvas(220)
    d.polygon(K.star_points(110, 116, 106, 42), fill=255)
    star = K.mask_of(im)
    im2, d2 = K.canvas(220)
    d2.polygon(K.star_points(110, 116, 76, 30), fill=255)
    k.relief([(star, 'red', 0.12, 3.0), (K.mask_of(im2), 'red', 0.16, 30.0)], 2.0)


@icon('TECH_Industrialization', yaw=-30, pitch=4, ground=True)
def _(k):
    # a factory with a saw-tooth roof and smokestacks
    k.box((1.6, 1.0, 0.6), k.m('brick', '#9a5a44'), center=(0, 0, 0.3))
    for i in range(4):
        x0 = -0.8 + i * 0.4
        k.extrude([(x0, 0), (x0 + 0.4, 0), (x0 + 0.4, 0.28)], 1.0, k.m('roof', '#6e6e6a'), M.translate(0, 0, 0.6))
        k.extrude([(x0 + 0.39, 0.0), (x0 + 0.4, 0.0), (x0 + 0.4, 0.27), (x0 + 0.39, 0.27)], 1.0, 'glass', M.translate(0, -0.005, 0.6))
    for x, h in ((0.5, 1.9), (0.85, 1.6)):
        k.cyl((x, 0.3, 0), (x, 0.3, h), 0.12, 'brick', r1=0.09)
        k.cyl((x, 0.3, h - 0.08), (x, 0.3, h), 0.11, 'dark_metal')
        for j in range(3):
            k.sph((x - 0.05 * j, 0.3 + 0.12 * j, h + 0.18 + 0.2 * j), 0.13 + 0.05 * j, k.m('smoke', '#b8b4ac', spec=0.0))
    for i in range(4):
        k.box((0.22, 0.03, 0.22), 'glass', center=(-0.6 + i * 0.4, -0.51, 0.3))


@icon('TECH_Electricity', yaw=0, pitch=4)
def _(k):
    # a light bulb
    glass = k.m('bulb', '#f4ecc4', spec=0.8, gloss=40)
    k.lathe([(0.0, 0.001), (0.02, 0.18), (0.12, 0.26), (0.3, 0.3), (0.55, 0.5), (0.8, 0.55), (1.05, 0.48), (1.22, 0.3),
             (1.28, 0.001)], glass, M.translate(0, 0, 0.5), seg=40)
    for i in range(5):
        k.torus(0.2, 0.03, 'silver', M.translate(0, 0, 0.12 + i * 0.075), seg=24)
    k.lathe([(0, 0.2), (0.45, 0.2), (0.48, 0.001)], 'silver', M.translate(0, 0, 0.1), seg=24)
    k.lathe([(0, 0.08), (0.1, 0.001)], 'dark_metal', M.translate(0, 0, 0.0), seg=16)
    k.cyl((-0.08, -0.02, 0.6), (-0.1, -0.02, 1.05), 0.012, 'dark_metal')
    k.cyl((0.08, -0.02, 0.6), (0.1, -0.02, 1.05), 0.012, 'dark_metal')
    k.cyl((-0.1, -0.02, 1.05), (0.1, -0.02, 1.05), 0.02, k.m('filament', '#fff2a0', spec=0.0))
    for a in range(-60, 61, 30):
        r = math.radians(a + 90)
        c, s = math.cos(r), math.sin(r)
        k.cyl((0.75 * c, -0.3, 1.25 + 0.75 * s), (1.0 * c, -0.3, 1.25 + 1.0 * s), 0.035, k.m('ray', '#f0d060', spec=0.0))


@icon('TECH_Scientific_Method', yaw=-28, pitch=4, ground=True)
def _(k):
    # a microscope
    k.box((0.8, 0.9, 0.12), 'black', center=(0, 0, 0.06), bevel=0.03)
    # the arm, curving up from the back of the base
    k.extrude([(-0.1, 0.1), (0.12, 0.1), (0.1, 0.9), (0.0, 1.3), (-0.2, 1.3), (-0.12, 0.9)], 0.18, 'black',
              M.translate(0, 0.25, 0) @ M.rot_z(90))
    k.box((0.55, 0.5, 0.06), 'black', center=(0, -0.1, 0.55))
    d = np.array((0.0, -0.45, 0.9))
    d /= np.linalg.norm(d)
    p0 = np.array((0.0, 0.05, 1.25))
    k.cyl(p0 - d * 0.5, p0 + d * 0.35, 0.11, 'silver')
    k.cyl(p0 + d * 0.35, p0 + d * 0.55, 0.08, 'black')
    k.cyl(p0 - d * 0.5, p0 - d * 0.62, 0.07, 'silver')
    k.cyl((0.1, 0.25, 0.9), (0.25, 0.25, 0.9), 0.1, 'silver')
    k.box((0.25, 0.18, 0.02), 'glass', center=(0, -0.1, 0.59))


@icon('TECH_Sanitation', yaw=-25, pitch=6, ground=True)
def _(k):
    # water pipes with a valve wheel
    k.cyl((-1.0, 0, 0.3), (0.2, 0, 0.3), 0.16, 'copper', seg=24)
    k.sph((0.2, 0, 0.3), 0.17, 'copper', seg=20)
    k.cyl((0.2, 0, 0.3), (0.2, 0, 1.2), 0.16, 'copper', seg=24)
    for p in ((-0.7, 0, 0.3), (-0.2, 0, 0.3)):
        k.cyl((p[0] - 0.05, 0, 0.3), (p[0] + 0.05, 0, 0.3), 0.2, 'brass', seg=24)
    k.cyl((0.2, 0, 0.75), (0.2, -0.35, 0.75), 0.05, 'brass')
    k.torus(0.28, 0.035, 'red', M.translate(0.2, -0.38, 0.75) @ M.rot_x(90), seg=32)
    for a in (0, 60, 120):
        r = math.radians(a)
        k.cyl((0.2 - 0.28 * math.cos(r), -0.38, 0.75 - 0.28 * math.sin(r)), (0.2 + 0.28 * math.cos(r), -0.38, 0.75 + 0.28 * math.sin(r)), 0.025, 'red')
    for i in range(3):
        k.sph((-1.05, -0.05 * i, 0.15 - 0.12 * i), 0.07 - 0.01 * i, 'water', seg=12)


@icon('TECH_Espionage', yaw=-20, pitch=6)
def _(k):
    # a magnifying glass
    T = M.rot_x(90)
    k.torus(0.55, 0.07, 'dark_metal', T, seg=48)
    k.add(M.disc(0.52, 0.52, 0.03, k.m('lens', '#b8d4dc', spec=0.9, gloss=50), seg=40, dome=0.03))
    k.cyl((0.4, 0, -0.4), (0.95, 0, -0.95), 0.07, 'dark_wood', r1=0.08)
    k.cyl((0.4, 0, -0.4), (0.5, 0, -0.5), 0.08, 'brass')


@icon('TECH_The_Corporation', yaw=-28, pitch=4, ground=True)
def _(k):
    # a briefcase
    k.box((1.5, 0.45, 1.0), 'leather', center=(0, 0, 0.5), bevel=0.06)
    k.box((1.52, 0.47, 0.06), 'dark_wood', center=(0, 0, 0.82))
    for x in (-0.45, 0.45):
        k.box((0.14, 0.06, 0.12), 'brass', center=(x, -0.24, 0.8))
    T = M.translate(0, 0, 1.0) @ M.rot_x(90)
    k.torus(0.22, 0.045, 'black', T @ M.scale(1, 0.6, 1), seg=24)


@icon('TECH_Refining', yaw=-25, pitch=6, ground=True)
def _(k):
    # oil drums and a drop of oil
    for (x, y, c) in ((-0.45, 0.2, 'navy'), (0.35, 0.3, 'dark_red')):
        k.cyl((x, y, 0), (x, y, 1.1), 0.36, c, seg=32)
        for z in (0.35, 0.75):
            k.torus(0.365, 0.02, c, M.translate(x, y, z), seg=32)
        k.cyl((x + 0.15, y, 1.1), (x + 0.15, y, 1.14), 0.06, 'dark_metal')
    k.lathe([(0, 0.001), (0.05, 0.18), (0.15, 0.26), (0.3, 0.24), (0.45, 0.14), (0.62, 0.001)], 'black',
            M.translate(0.05, -0.45, 0), seg=28)


@icon('TECH_Steel', yaw=-30, pitch=6, ground=True)
def _(k):
    # steel girders
    # each I-beam is three plates
    for (x, z, rot) in ((0, 0.25, 0), (0.0, 0.76, 90)):
        T = M.translate(x, 0, z) @ M.rot_z(rot)
        k.box((0.6, 2.0, 0.07), 'steel', center=(0, 0, -0.22), T=T)
        k.box((0.6, 2.0, 0.07), 'steel', center=(0, 0, 0.22), T=T)
        k.box((0.07, 2.0, 0.44), 'steel', center=(0, 0, 0), T=T)
    for i in range(5):
        k.sph((-0.25 + 0.12 * i, -0.99, 0.48), 0.025, 'dark_metal', seg=8)


@icon('TECH_Atomic_Theory', yaw=0, pitch=0)
def _(k):
    # an atom: nucleus and three electron orbits
    rng = np.random.default_rng(1)
    for i in range(14):
        v = rng.normal(size=3)
        v = v / np.linalg.norm(v) * 0.13 * rng.random() ** 0.3
        k.sph(v, 0.09, ['red', 'blue'][i % 2], seg=12)
    for j, rot in enumerate((0, 60, 120)):
        T = M.rot_y(rot) @ M.rot_x(70)
        k.torus(0.95, 0.02, 'steel', T @ M.scale(1, 0.42, 1), seg=64, tube=6)
        a = math.radians(40 + 90 * j)
        p = T @ np.array([0.95 * math.cos(a), 0.42 * 0.95 * math.sin(a), 0, 1.0])
        k.sph(p[:3], 0.08, k.m('electron', '#7ab0e0', spec=0.4), seg=12)


@icon('TECH_Combustion', yaw=-25, pitch=6, fill=0.86)
def _(k):
    # a spark plug, sparking
    T = M.rot_y(-50)
    k.lathe([(0.0, 0.07), (0.6, 0.07)], 'steel', T, seg=20)
    for i in range(9):
        k.torus(0.075, 0.018, 'steel', T @ M.translate(0, 0, 0.06 + i * 0.06), seg=20, tube=6)
    k.add(M.cylinder(0.17, 0.17, 0.2, k.m('steel'), seg=6, smooth=False), T @ M.translate(0, 0, 0.62))
    k.lathe([(0.82, 0.12), (0.84, 0.11), (1.0, 0.1), (1.6, 0.08), (1.65, 0.06), (1.7, 0.001)],
            k.m('porcelain', '#efece4', spec=0.4, gloss=24), T, seg=28)
    for z in (1.12, 1.2, 1.28, 1.36):
        k.torus(0.095, 0.012, 'porcelain', T @ M.translate(0, 0, z), seg=24, tube=6)
    k.lathe([(1.68, 0.035), (1.85, 0.03), (1.9, 0.001)], 'silver', T, seg=12)
    k.box((0.04, 0.04, 0.1), 'steel', center=(0, 0, -0.05), T=T)
    k.box((0.16, 0.04, 0.04), 'steel', center=(0.06, 0, -0.1), T=T)
    spark = k.m('spark', '#f6e070', spec=0.0)
    p = (T @ np.array([0, 0, -0.12, 1.0]))[:3]
    for a in range(0, 360, 45):
        r = math.radians(a)
        k.cyl(p + 0.08 * np.array((math.cos(r), 0, math.sin(r))), p + 0.22 * np.array((math.cos(r), 0, math.sin(r))), 0.018, spark)


@icon('TECH_Replaceable_Parts', yaw=-25, pitch=10, ground=True)
def _(k):
    # a bolt and a nut
    k.add(M.cylinder(0.3, 0.3, 0.22, k.m('steel'), seg=6, smooth=False), M.translate(-0.3, 0, 0))
    k.cyl((-0.3, 0, 0.22), (-0.3, 0, 1.4), 0.14, 'steel', seg=20)
    for i in range(14):
        k.torus(0.14, 0.02, 'steel', M.translate(-0.3, 0, 0.5 + i * 0.065), seg=20, tube=6)
    k.add(M.cylinder(0.32, 0.32, 0.25, k.m('steel'), seg=6, smooth=False), M.translate(0.55, -0.2, 0))
    k.cyl((0.55, -0.2, 0.0), (0.55, -0.2, 0.26), 0.15, 'dark_metal', seg=20)


@icon('TECH_Flight', yaw=-35, pitch=8)
def _(k):
    # a biplane
    for z in (0.0, 0.42):
        k.box((2.2, 0.38, 0.04), k.m('canvas', '#d8ceb0'), center=(0, 0, z), bevel=0.01)
    for x in (-0.9, -0.45, 0.45, 0.9):
        k.cyl((x, 0.05, 0.0), (x, 0.05, 0.42), 0.015, 'dark_wood')
    k.lathe([(0, 0.001), (0.1, 0.12), (0.5, 0.11), (1.4, 0.04), (1.45, 0.001)], 'canvas', M.translate(0, -0.35, 0.18) @ M.rot_x(-90))
    k.box((0.5, 0.25, 0.03), 'canvas', center=(0, 1.0, 0.18))
    k.box((0.03, 0.25, 0.3), 'canvas', center=(0, 1.0, 0.32))
    k.box((0.08, 0.04, 0.9), 'dark_wood', center=(0, -0.48, 0.18), T=M.rot_y(20))
    for x in (-0.35, 0.35):
        k.cyl((x, -0.1, 0.0), (x, -0.1, -0.35), 0.015, 'dark_metal')
        k.cyl((x - 0.03, -0.1, -0.38), (x + 0.03, -0.1, -0.38), 0.09, 'black')


@icon('TECH_Amphibious_Warfare', yaw=-35, pitch=6, ground=True)
def _(k):
    # a landing craft run up on the beach, its ramp down
    k.box((1.5, 0.7, 0.04), k.m('sand', '#d8c48c'), center=(0, -0.95, 0.02))
    k.box((1.5, 1.5, 0.04), 'water', center=(0, 0.15, 0.02))
    olive, dark = k.m('olive', '#6e7250'), k.m('olive_dark', '#4c5036')
    k.box((0.8, 1.3, 0.12), dark, center=(0, 0.25, 0.1))
    for s in (-1, 1):
        k.box((0.08, 1.3, 0.42), olive, center=(s * 0.4, 0.25, 0.25))
    k.box((0.8, 0.08, 0.42), olive, center=(0, 0.9, 0.25))
    k.box((0.3, 0.25, 0.25), olive, center=(0, 0.75, 0.55))
    k.box((0.76, 0.06, 0.62), olive, center=(0, -0.62, 0.17), T=M.rot_x(-62))
    for x in (-0.18, 0.08):
        k.cyl((x, 0.2, 0.16), (x, 0.2, 0.4), 0.06, k.m('soldier', '#5a5e40'))
        k.sph((x, 0.2, 0.46), 0.07, k.m('helmet', '#4a4e34'), seg=10)


@icon('TECH_Mass_Production', yaw=-30, pitch=8, ground=True)
def _(k):
    # identical crates on a conveyor belt
    k.box((2.4, 0.6, 0.06), 'black', center=(0, 0, 0.5))
    for i in range(9):
        x = -1.1 + i * 0.275
        k.cyl((x, -0.3, 0.44), (x, 0.3, 0.44), 0.06, 'steel')
    for s in (-1, 1):
        for x in (-1.0, 0.0, 1.0):
            k.box((0.06, 0.06, 0.5), 'dark_metal', center=(x, s * 0.28, 0.22))
    for i in range(3):
        k.box((0.42, 0.42, 0.42), 'light_wood', center=(-0.75 + i * 0.75, 0, 0.75), bevel=0.02)
        k.box((0.44, 0.06, 0.08), 'wood', center=(-0.75 + i * 0.75, -0.2, 0.75))


@icon('TECH_Electronics', yaw=-10, pitch=4, ground=True)
def _(k):
    # a vacuum tube
    glass = k.m('tube_glass', '#d0dce0', spec=0.8, gloss=40)
    k.lathe([(0, 0.32), (0.3, 0.32), (0.32, 0.001)], 'black', seg=32)
    for a in range(0, 360, 60):
        r = math.radians(a)
        k.cyl((0.18 * math.cos(r), 0.18 * math.sin(r), 0), (0.18 * math.cos(r), 0.18 * math.sin(r), -0.2), 0.02, 'silver')
    k.lathe([(0.3, 0.3), (1.3, 0.3), (1.45, 0.24), (1.55, 0.12), (1.6, 0.001)], glass, seg=36)
    k.cyl((0, 0, 0.45), (0, 0, 1.25), 0.15, 'dark_metal', seg=20)
    k.cyl((0, 0, 0.45), (0, 0, 1.25), 0.05, k.m('glow', '#f0a050', spec=0.0))
    for z in (0.5, 1.2):
        k.box((0.5, 0.04, 0.06), 'silver', center=(0, 0, z))


@icon('TECH_Motorized_Transportation', yaw=-35, pitch=6, ground=True)
def _(k):
    # an early automobile
    k.box((0.9, 1.9, 0.3), 'navy', center=(0, 0.0, 0.45), bevel=0.04)
    k.box((0.85, 0.7, 0.5), 'navy', center=(0, 0.35, 0.85), bevel=0.03)
    k.box((0.86, 0.05, 0.36), 'glass', center=(0, 0.0, 0.88))
    k.box((0.95, 0.75, 0.05), 'black', center=(0, 0.35, 1.12))
    k.box((0.7, 0.6, 0.25), 'black', center=(0, -0.72, 0.68), bevel=0.04)
    k.box((0.4, 0.05, 0.25), 'silver', center=(0, -1.0, 0.55))
    for x in (-0.48, 0.48):
        for y in (-0.6, 0.6):
            k.cyl((x - 0.06, y, 0.25), (x + 0.06, y, 0.25), 0.25, 'black', seg=24)
            k.cyl((x - 0.065 * np.sign(x) * -1, y, 0.25), (x + 0.07 * np.sign(x), y, 0.25), 0.12, 'silver', seg=16)
    for x in (-0.3, 0.3):
        k.sph((x, -0.98, 0.72), 0.08, 'brass', seg=12)


@icon('TECH_Advanced_Flight', yaw=-30, pitch=6)
def _(k):
    # a jet engine
    T = M.rot_x(-90)
    k.lathe([(0, 0.45), (0.15, 0.5), (1.2, 0.46), (1.5, 0.34), (1.6, 0.3)], 'silver', T, seg=40)
    k.lathe([(0, 0.44), (0.05, 0.001)], 'dark_metal', T @ M.translate(0, 0, 0.04), seg=40)
    k.lathe([(0, 0.15), (0.12, 0.07), (0.2, 0.001)], 'steel', T @ M.translate(0, 0, -0.05), seg=20)
    for i in range(14):
        a = 2 * math.pi * i / 14
        k.box((0.06, 0.01, 0.32), 'steel', center=(0.26 * math.cos(a), -0.02, 0.26 * math.sin(a)),
              T=M.rot_y(-math.degrees(a) + 90) @ M.rot_z(25))
    k.lathe([(1.5, 0.3), (1.75, 0.22)], 'dark_metal', T, seg=32)


@icon('TECH_Ironclads', fill=0.86)
def _(k):
    k.unit = 'Ironclad'


@icon('TECH_Fascism', yaw=-15, pitch=4)
def _(k):
    # the fasces: a bundle of rods bound around an axe
    for i in range(9):
        a = 2 * math.pi * i / 9
        x, y = 0.13 * math.cos(a), 0.13 * math.sin(a)
        k.cyl((x, y, -1.0), (x, y, 1.0), 0.05, 'light_wood', seg=10)
    for z in (-0.7, -0.2, 0.3, 0.75):
        k.torus(0.19, 0.03, 'red', M.translate(0, 0, z), seg=24)
    blade = [(0.18, 0.55), (0.62, 0.78), (0.72, 0.6), (0.7, 0.4), (0.6, 0.25), (0.18, 0.4)]
    k.extrude(blade, 0.05, 'steel', M.translate(0, 0, 0.3))
