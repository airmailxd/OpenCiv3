"""Spaceship part icons: each part as an object in space (kit3d), registered
with improvements.space."""
import math

import numpy as np

import kit3d as K
import mesh as M
from improvements import space

HULL = '#d8dad8'


def _ring(k, z, r, mat, tube=0.04):
    k.torus(r, tube, mat, M.translate(0, 0, z), seg=40)


@space('BLDG_SS_Thrusters')
def _(k):
    # a thruster block: four nozzles firing toward the viewer
    k.m('hull', HULL, spec=0.3)
    T = M.rot_x(90)          # model +z (the thrust) toward the camera
    k.lathe([(-1.4, 0.001), (-1.35, 0.85), (-0.2, 0.85), (-0.1, 0.8), (0.0, 0.001)], 'hull', T, seg=36)
    for (x, z) in ((-0.38, -0.38), (0.38, -0.38), (-0.38, 0.38), (0.38, 0.38)):
        Tn = M.translate(x, 0, z) @ T
        k.lathe([(0.0, 0.1), (0.15, 0.12), (0.55, 0.3), (0.6, 0.31)], 'dark_metal', Tn, seg=24)
        k.lathe([(0.56, 0.27), (0.58, 0.001)], k.m('glow', '#f0a050', spec=0.0), Tn, seg=24)
    for a in range(0, 360, 90):
        r = math.radians(a + 45)
        k.box((0.12, 1.2, 0.12), 'steel', center=(0.86 * math.cos(r), 0.6, 0.86 * math.sin(r)))


@space('BLDG_SS_Engine')
def _(k):
    # a big rocket engine bell with its turbopumps
    k.lathe([(0.0, 0.35), (0.3, 0.3), (0.5, 0.36), (1.4, 0.75), (1.45, 0.76)], 'dark_metal', M.rot_x(180) @ M.translate(0, 0, -1.0), seg=36)
    k.cyl((0, 0, 1.0), (0, 0, 1.6), 0.5, 'steel', seg=28)
    for a in (0, 120, 240):
        r = math.radians(a)
        k.cyl((0.6 * math.cos(r), 0.6 * math.sin(r), 0.9), (0.6 * math.cos(r), 0.6 * math.sin(r), 1.5), 0.15, 'copper')
        k.cyl((0.6 * math.cos(r), 0.6 * math.sin(r), 1.2), (0.25 * math.cos(r), 0.25 * math.sin(r), 0.8), 0.06, 'copper')
    k.lathe([(-0.48, 0.74), (-0.45, 0.001)], k.m('glow', '#f0a050', spec=0.0), seg=36)


@space('BLDG_SS_Landing_Docking_Bay')
def _(k):
    # a docking ring with its hatch
    k.m('hull', HULL, spec=0.3)
    k.cyl((0, 0, 0), (0, 0, 1.0), 0.8, 'hull', seg=36)
    _ring(k, 1.0, 0.72, 'steel', 0.08)
    k.cyl((0, 0, 1.0), (0, 0, 1.08), 0.55, 'dark_metal', seg=32)
    for a in range(0, 360, 60):
        r = math.radians(a)
        k.box((0.12, 0.12, 0.2), 'yellow', center=(0.66 * math.cos(r), 0.66 * math.sin(r), 1.12))
    k.box((1.9, 0.5, 0.08), k.m('panel', '#2f4f8a', spec=0.6, gloss=30), center=(0, 0, 0.4), T=M.rot_z(90) @ M.rot_y(90))


@space('BLDG_SS_Cockpit')
def _(k):
    # the command capsule with its windows
    k.m('hull', HULL, spec=0.3)
    k.lathe([(0, 0.95), (0.1, 0.95), (0.9, 0.45), (1.1, 0.3), (1.15, 0.001)], 'hull', seg=40)
    k.lathe([(-0.25, 0.85), (0, 0.95)], 'dark_metal', seg=40)
    for a in (-30, 0, 30):
        r = math.radians(a - 90)
        c = np.array((0.68 * math.cos(r), 0.68 * math.sin(r), 0.55))
        k.box((0.22, 0.05, 0.16), k.m('window', '#3d6f8a', spec=0.8, gloss=40), center=c, T=M.rot_z(a))


@space('BLDG_SS_Fuel_Cells')
def _(k):
    # a bundle of fuel tanks
    for (x, y, c) in ((-0.42, 0, 'white'), (0.42, 0, 'white'), (0, -0.4, 'orange'), (0, 0.4, 'white')):
        k.lathe([(0, 0.001), (0.12, 0.25), (0.3, 0.36), (1.5, 0.36), (1.68, 0.25), (1.8, 0.001)], c, M.translate(x, y, 0), seg=28)
    for z in (0.4, 1.4):
        _ring(k, z, 0.82, 'steel', 0.035)


@space('BLDG_SS_Life_Support_System')
def _(k):
    # a greenhouse sphere with pipes
    k.m('hull', HULL, spec=0.3)
    k.sph((0, 0, 0.8), 0.75, k.m('glass_dome', '#b8d8d0', spec=0.8, gloss=40), seg=32)
    k.lathe([(0, 0.6), (0.08, 0.001)], 'hull', M.translate(0, 0, 0.62), seg=32)
    for (x, y, h) in ((-0.2, -0.1, 0.4), (0.2, 0.15, 0.3), (0.05, -0.3, 0.35)):
        k.sph((x, y, 0.85 + h * 0.2), 0.2, 'leaf', seg=14)
    k.cyl((0, 0, 0.02), (0, 0, 0.3), 0.45, 'hull')
    for a in (30, 150, 270):
        r = math.radians(a)
        k.cyl((0.5 * math.cos(r), 0.5 * math.sin(r), 0.2), (1.1 * math.cos(r), 1.1 * math.sin(r), -0.1), 0.06, 'copper')


@space('BLDG_SS_Stasis_Chamber')
def _(k):
    # a sleeping pod with a glass lid
    k.m('hull', HULL, spec=0.3)
    k.box((0.8, 1.9, 0.4), 'hull', center=(0, 0, 0.2), bevel=0.08)
    k.add(M.ellipsoid(0.34, 0.85, 0.22, k.m('lid', '#a8d0e0', spec=0.9, gloss=40), center=(0, 0, 0.42), seg=24))
    k.add(M.ellipsoid(0.16, 0.5, 0.12, k.m('sleeper', '#c8b8d8', spec=0.1), center=(0, 0.05, 0.42), seg=16))
    for y in (-0.7, 0.7):
        k.box((0.84, 0.1, 0.1), k.m('light', '#7ab0e0', spec=0.0), center=(0, y, 0.4))


@space('BLDG_SS_Storage_Supply')
def _(k):
    # supply containers in a frame
    for (x, y, z, c) in ((-0.42, -0.42, 0.21, 'grey'), (0.42, -0.42, 0.21, 'white'), (-0.42, 0.42, 0.21, 'white'),
                         (0.42, 0.42, 0.21, 'grey'), (-0.42, -0.42, 0.63, 'white'), (0.42, 0.42, 0.63, 'white')):
        k.box((0.8, 0.8, 0.4), c, center=(x, y, z), bevel=0.04)
    for (x, y) in ((-0.85, -0.85), (0.85, -0.85), (-0.85, 0.85), (0.85, 0.85)):
        k.cyl((x, y, 0), (x, y, 1.0), 0.035, 'steel')
    for z in (0.0, 1.0):
        for (a, b) in (((-0.85, -0.85), (0.85, -0.85)), ((0.85, -0.85), (0.85, 0.85)), ((0.85, 0.85), (-0.85, 0.85)),
                       ((-0.85, 0.85), (-0.85, -0.85))):
            k.cyl((a[0], a[1], z), (b[0], b[1], z), 0.035, 'steel')


@space('BLDG_SS_Planetary_Party_Lounge')
def _(k):
    # a glass dome with colored lights inside
    k.m('hull', HULL, spec=0.3)
    k.cyl((0, 0, 0), (0, 0, 0.25), 1.0, 'hull', seg=40)
    k.lathe([(0.25, 0.95)] + [(0.25 + 0.95 * math.sin(t), 0.95 * math.cos(t)) for t in np.linspace(0.1, math.pi / 2, 10)],
            k.m('dome_glass', '#b8c8e0', spec=0.9, gloss=40), seg=40)
    for i, c in enumerate(('#e06080', '#60c0e0', '#f0d050', '#80e080', '#c080f0')):
        a = 2 * math.pi * i / 5
        k.sph((0.45 * math.cos(a), 0.45 * math.sin(a), 0.55), 0.12, k.m('l%d' % i, c, spec=0.0), seg=12)
    k.sph((0, 0, 0.9), 0.14, 'silver', seg=16)


@space('BLDG_SS_Exterior_Casing')
def _(k):
    # a curved hull panel with ribs
    k.m('hull', HULL, spec=0.35)
    verts, faces = [], []
    n = 16
    for j in range(n + 1):
        for i in range(n + 1):
            a = math.radians(-50 + 100 * i / n)
            verts.append((1.0 * math.sin(a), 1.0 - 1.0 * math.cos(a), -0.9 + 1.8 * j / n))
    for j in range(n):
        for i in range(n):
            q = j * (n + 1) + i
            faces += [(q, q + 1, q + n + 2), (q, q + n + 2, q + n + 1)]
    k.add(M.from_indexed(np.array(verts), np.array(faces), k.m('hull')))
    for z in (-0.6, 0.0, 0.6):
        for i in range(n):
            a0, a1 = math.radians(-50 + 100 * i / n), math.radians(-50 + 100 * (i + 1) / n)
            k.cyl((math.sin(a0) * 0.98, 1 - math.cos(a0) * 0.98, z), (math.sin(a1) * 0.98, 1 - math.cos(a1) * 0.98, z), 0.03, 'steel')
