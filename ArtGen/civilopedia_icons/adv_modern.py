"""Advance icons of the modern era (registered with advances.icon)."""
import math

import numpy as np

import kit3d as K
import mesh as M
from advances import icon


@icon('TECH_Rocketry', yaw=-20, pitch=6)
def _(k):
    # a rocket with fins
    k.lathe([(0, 0.24), (1.4, 0.24), (1.75, 0.18), (2.0, 0.08), (2.1, 0.001)], 'white', seg=32)
    for z in (0.5, 1.2):
        k.lathe([(z, 0.245), (z + 0.12, 0.245)], 'red', seg=32)
    k.lathe([(1.75, 0.18), (2.0, 0.08), (2.1, 0.001)], 'red', M.translate(0, 0, 0.003), seg=32)
    for a in (0, 120, 240):
        k.extrude([(0.2, 0.0), (0.55, -0.15), (0.55, 0.15), (0.2, 0.55)], 0.04, 'red', M.rot_z(a) @ M.translate(0, 0, 0))
    k.lathe([(-0.15, 0.12), (0.0, 0.18)], 'dark_metal', seg=20)
    k.lathe([(-0.55, 0.001), (-0.35, 0.12), (-0.15, 0.14)], 'flame', seg=16)


@icon('TECH_Fission', yaw=0, pitch=0)
def _(k):
    # a nucleus splitting in two, struck by a neutron
    rng = np.random.default_rng(4)
    for cx, n in ((-0.45, 12), (0.45, 12)):
        for i in range(n):
            v = rng.normal(size=3)
            v = v / np.linalg.norm(v) * 0.24 * rng.random() ** 0.3
            k.sph((cx + v[0], v[1], v[2]), 0.12, ['red', 'blue'][i % 2], seg=12)
    k.sph((0, -0.2, 0.85), 0.11, 'grey', seg=12)
    for a in (30, 150, 270):
        r = math.radians(a)
        k.cyl((0.85 * math.cos(r), 0, 0.85 * math.sin(r)), (1.05 * math.cos(r), 0, 1.05 * math.sin(r)), 0.03, k.m('ray', '#f0c050'))


@icon('TECH_Computers', yaw=-25, pitch=4, ground=True)
def _(k):
    # a monitor and a keyboard
    k.box((1.3, 0.9, 1.0), k.m('plastic', '#d8d4c8'), center=(0, 0.2, 0.75), bevel=0.05)
    k.box((1.1, 0.05, 0.8), k.m('screen', '#3d6f8a', spec=0.6, gloss=30), center=(0, -0.26, 0.77))
    im, d = K.canvas(160)
    for i, y in enumerate(range(30, 130, 18)):
        d.line((24, y, 24 + 40 + (i * 37) % 80, y), fill=255, width=6)
    k.relief([(K.mask_of(im), k.m('text', '#a8e0b0', spec=0.0), 0.01, 1.0)], 1.0, M.translate(0, -0.29, 0.77))
    k.box((0.3, 0.3, 0.25), 'plastic', center=(0, 0.2, 0.12))
    k.box((1.3, 0.5, 0.08), 'plastic', center=(0, -0.65, 0.04), T=M.rot_x(-5))
    for r in range(3):
        for c in range(9):
            k.box((0.11, 0.11, 0.04), k.m('key', '#9c988e'), center=(-0.52 + c * 0.13, -0.78 + r * 0.13, 0.1))


@icon('TECH_Recycling', yaw=-8, pitch=8)
def _(k):
    # three chasing arrows
    n = 240
    im, d = K.canvas(n)
    c = n / 2
    for i in range(3):
        a0 = 90 + i * 120
        d.arc((c - 90, c - 90, c + 90, c + 90), a0 + 12, a0 + 92, fill=255, width=34)
        a = math.radians(a0 + 92)
        tip = (c + 90 * math.cos(a + 0.35), c + 90 * math.sin(a + 0.35))
        p1 = (c + 58 * math.cos(a), c + 58 * math.sin(a))
        p2 = (c + 122 * math.cos(a), c + 122 * math.sin(a))
        d.polygon([p1, p2, tip], fill=255)
    k.relief([(K.mask_of(im), 'green', 0.18, 6.0)], 2.0)


@icon('TECH_Space_Flight', yaw=-25, pitch=4, ground=True)
def _(k):
    # an astronaut's helmet
    k.sph((0, 0, 0.75), 0.75, 'white', seg=32)
    T = M.translate(0, -0.48, 0.82) @ M.rot_x(90)
    k.add(M.disc(0.5, 0.42, 0.04, k.m('visor', '#c8a050', spec=0.9, gloss=50), seg=40, dome=0.18), M.translate(0, -0.55, 0.82))
    k.torus(0.52, 0.05, 'silver', T @ M.scale(1, 0.84, 1), seg=40)
    k.lathe([(0, 0.6), (0.18, 0.62), (0.2, 0.001)], 'silver', seg=36)
    k.box((0.25, 0.1, 0.12), 'red', center=(0.45, -0.45, 0.25))


@icon('TECH_Nuclear_Power', yaw=-20, pitch=4, ground=True)
def _(k):
    # a cooling tower with steam
    prof = []
    for i in range(13):
        z = 1.5 * i / 12
        r = 0.62 + 0.36 * ((z - 1.1) / 1.1) ** 2
        prof.append((z, r))
    k.lathe(prof, k.m('concrete', '#c4c0b6'), seg=48)
    k.lathe([(1.5, 0.64), (1.48, 0.001)], 'dark_stone', seg=48)
    steam = k.m('steam', '#f2f0ec', spec=0.0)
    for (x, y, z, r) in ((0.0, 0.0, 1.62, 0.45), (-0.3, 0.05, 1.75, 0.35), (0.3, 0.05, 1.78, 0.38), (0.1, 0.1, 2.05, 0.42),
                         (0.4, 0.15, 2.25, 0.32)):
        k.sph((x, y, z), r, steam, seg=18)
    k.box((0.5, 0.4, 0.35), 'concrete', center=(1.05, -0.3, 0.17))


@icon('TECH_Superconductor', yaw=-25, pitch=12, ground=True)
def _(k):
    # a magnet levitating over a superconductor
    k.cyl((0, 0, 0), (0, 0, 0.25), 0.8, 'dark_metal', seg=40)
    k.cyl((0, 0, 0.25), (0, 0, 0.3), 0.75, k.m('frost', '#cfe4ee', spec=0.5), seg=40)
    k.box((0.5, 0.5, 0.2), 'silver', center=(0, 0, 0.8), bevel=0.03)
    for r in (0.4, 0.6):
        k.torus(r, 0.012, k.m('field', '#78b8e8', spec=0.0), M.translate(0, 0, 0.55), seg=40, tube=6)


@icon('TECH_Miniaturization', yaw=-25, pitch=20)
def _(k):
    # a microchip
    k.box((1.2, 1.2, 0.14), 'black', center=(0, 0, 0.0), bevel=0.02)
    k.box((0.5, 0.5, 0.03), 'silver', center=(0, 0, 0.08))
    for i in range(7):
        t = -0.48 + i * 0.16
        for (x, y, sx, sy) in ((t, 0.68, 0.06, 0.18), (t, -0.68, 0.06, 0.18), (0.68, t, 0.18, 0.06), (-0.68, t, 0.18, 0.06)):
            k.box((sx, sy, 0.04), 'gold', center=(x, y, -0.04))


@icon('TECH_Ecology', yaw=-8, pitch=6)
def _(k):
    # a green leaf
    n = 240
    im, d = K.canvas(n)
    pts = []
    for i in range(41):
        t = i / 40
        w = math.sin(math.pi * t) ** 0.8 * 70
        pts.append((120 + w, 220 - t * 200))
    pts += [(120 - math.sin(math.pi * (i / 40)) ** 0.8 * 70, 220 - (i / 40) * 200) for i in range(40, -1, -1)]
    d.polygon(pts, fill=255)
    leaf = K.mask_of(im)
    im2, d2 = K.canvas(n)
    d2.line((120, 228, 120, 40), fill=255, width=6)
    for j in range(5):
        y = 190 - j * 34
        d2.line((120, y, 160, y - 34), fill=255, width=3)
        d2.line((120, y, 80, y - 34), fill=255, width=3)
    veins = K.mask_of(im2) & leaf
    k.relief([(leaf, 'leaf', 0.12, 10.0), (veins, k.m('vein', '#a8c878'), 0.02, 1.0)], 2.0, M.rot_y(-20))


@icon('TECH_Synthetic_Fibers', yaw=-20, pitch=6, ground=True)
def _(k):
    # a spool of thread
    k.lathe([(0, 0.5), (0.12, 0.5), (0.12, 0.2), (1.0, 0.2), (1.0, 0.5), (1.12, 0.5), (1.12, 0.001)], 'light_wood', seg=36)
    k.lathe([(0.12, 0.38), (0.2, 0.4), (0.9, 0.4), (1.0, 0.38)], k.m('thread', '#4f8fc0', spec=0.3), seg=36)
    for i in range(12):
        k.torus(0.4, 0.012, k.m('thread_dark', '#3d78a8'), M.translate(0, 0, 0.18 + i * 0.065), seg=36, tube=5)
    k.cyl((0.4, -0.05, 0.6), (1.0, -0.6, 0.0), 0.012, 'thread')


@icon('TECH_Satellites', yaw=-30, pitch=10)
def _(k):
    # a satellite with solar panels
    k.box((0.5, 0.5, 0.6), 'gold', center=(0, 0, 0), bevel=0.02)
    for s in (-1, 1):
        k.cyl((s * 0.25, 0, 0), (s * 0.45, 0, 0), 0.03, 'silver')
        k.box((0.9, 0.04, 0.45), k.m('panel', '#2f4f8a', spec=0.6, gloss=30), center=(s * 0.92, 0, 0))
        for i in range(1, 4):
            k.box((0.01, 0.05, 0.45), 'silver', center=(s * (0.47 + 0.225 * i), 0, 0))
    k.lathe([(0, 0.001), (0.12, 0.25), (0.15, 0.27)], 'white', M.translate(0, -0.25, 0.42) @ M.rot_x(60), seg=24)
    k.cyl((0, 0, 0.3), (0, -0.15, 0.48), 0.02, 'silver')


@icon('TECH_The_Laser', yaw=-25, pitch=4, ground=True)
def _(k):
    # a laser on a stand, with its beam
    k.box((0.5, 0.5, 0.1), 'dark_metal', center=(-0.5, 0, 0.05))
    k.cyl((-0.5, 0, 0.1), (-0.5, 0, 0.55), 0.06, 'silver')
    k.cyl((-0.9, 0, 0.65), (0.0, 0, 0.65), 0.17, 'steel', seg=24)
    k.cyl((0.0, 0, 0.65), (0.1, 0, 0.65), 0.1, 'dark_metal', seg=24)
    k.cyl((0.1, 0, 0.65), (1.4, 0, 0.65), 0.035, k.m('beam', '#ff5040', spec=0.0), seg=12)
    k.sph((1.4, 0, 0.65), 0.08, k.m('spot', '#ffb0a0', spec=0.0), seg=12)


@icon('TECH_Genetics', yaw=-15, pitch=0)
def _(k):
    # the double helix
    n = 26
    for i in range(n):
        t = i / (n - 1)
        z = -1.0 + 2.0 * t
        a = t * 2.4 * math.pi
        p1 = np.array((0.4 * math.cos(a), 0.4 * math.sin(a), z))
        p2 = np.array((0.4 * math.cos(a + math.pi), 0.4 * math.sin(a + math.pi), z))
        k.sph(p1, 0.08, 'blue', seg=12)
        k.sph(p2, 0.08, 'red', seg=12)
        if i % 2 == 0:
            mid = (p1 + p2) / 2
            k.cyl(p1, mid, 0.025, 'yellow')
            k.cyl(mid, p2, 0.025, 'green')
        if i < n - 1:
            t2 = (i + 1) / (n - 1)
            a2 = t2 * 2.4 * math.pi
            z2 = -1.0 + 2.0 * t2
            k.cyl(p1, (0.4 * math.cos(a2), 0.4 * math.sin(a2), z2), 0.04, 'blue')
            k.cyl(p2, (0.4 * math.cos(a2 + math.pi), 0.4 * math.sin(a2 + math.pi), z2), 0.04, 'red')


@icon('TECH_Stealth', yaw=-15, pitch=-35)
def _(k):
    # a flying wing
    pts = [(0, 0.55), (1.2, -0.2), (1.0, -0.35), (0.7, -0.2), (0.45, -0.35), (0.2, -0.2), (0, -0.35)]
    poly = pts + [(-x, z) for x, z in pts[::-1][:-1]]
    poly = poly[:-1] if poly[-1] == poly[0] else poly
    k.extrude(poly[::-1], 0.08, k.m('stealth', '#4a4e56', spec=0.3), M.rot_x(-90))
    k.lathe([(0, 0.18), (0.2, 0.14), (0.45, 0.001)], 'stealth', M.translate(0, 0.0, 0.0) @ M.rot_x(-90) @ M.scale(1.3, 0.5, 1))


@icon('TECH_Smart_Weapons', yaw=-8, pitch=6)
def _(k):
    # a targeting reticle
    n = 240
    im, d = K.canvas(n)
    c = n / 2
    d.ellipse((c - 100, c - 100, c + 100, c + 100), outline=255, width=14)
    d.ellipse((c - 55, c - 55, c + 55, c + 55), outline=255, width=8)
    for (x0, y0, x1, y1) in ((c - 116, c - 5, c - 30, c + 5), (c + 30, c - 5, c + 116, c + 5),
                             (c - 5, c - 116, c + 5, c - 30), (c - 5, c + 30, c + 5, c + 116)):
        d.rectangle((x0, y0, x1, y1), fill=255)
    ring = K.mask_of(im)
    im2, d2 = K.canvas(n)
    d2.ellipse((c - 12, c - 12, c + 12, c + 12), fill=255)
    k.relief([(ring, 'dark_metal', 0.1, 3.0), (K.mask_of(im2), 'red', 0.14, 5.0)], 2.0)


@icon('TECH_Robotics', yaw=-35, pitch=4, ground=True)
def _(k):
    # an industrial robot arm
    k.cyl((0, 0, 0), (0, 0, 0.2), 0.45, 'dark_metal', seg=28)
    k.cyl((0, 0, 0.2), (0, 0, 0.45), 0.3, k.m('robot', '#e0a030', spec=0.3), seg=28)
    p1 = np.array((0, 0, 0.5))
    p2 = np.array((0.2, -0.1, 1.35))
    p3 = np.array((0.95, -0.4, 1.15))
    k.sph(p1, 0.2, 'dark_metal')
    k.cyl(p1, p2, 0.14, 'robot')
    k.sph(p2, 0.17, 'dark_metal')
    k.cyl(p2, p3, 0.11, 'robot')
    k.sph(p3, 0.12, 'dark_metal')
    d = np.array((0.3, -0.1, -0.6))
    d /= np.linalg.norm(d)
    k.cyl(p3, p3 + d * 0.25, 0.08, 'steel')
    for s in (-1, 1):
        q = p3 + d * 0.25
        k.box((0.05, 0.05, 0.25), 'steel', center=q + d * 0.12 + np.array((s * 0.08, 0, 0)))


@icon('TECH_Integrated_Defense', yaw=-25, pitch=4, ground=True)
def _(k):
    # a radar dish on its mount
    k.box((0.8, 0.8, 0.3), k.m('concrete', '#c4c0b6'), center=(0, 0, 0.15))
    k.cyl((0, 0, 0.3), (0, 0, 0.75), 0.1, 'steel')
    T = M.translate(0, 0, 1.05) @ M.rot_x(55)
    prof = [(0, 0.001)] + [(0.35 * (i / 10) ** 2, 0.85 * i / 10) for i in range(1, 11)]
    k.lathe(prof, k.m('dish', '#e8e6e0', spec=0.3), T, seg=40)
    tip = (T @ np.array([0, 0, 0.85, 1.0]))[:3]
    base = (T @ np.array([0, 0, 0.0, 1.0]))[:3]
    k.cyl(base, tip, 0.025, 'steel')
    k.sph(tip, 0.06, 'dark_metal')
