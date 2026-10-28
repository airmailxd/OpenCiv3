"""Advance icons of MDJ's rules (C7/Lua/mdj, Medieval Japan), for those of
its Civilopedia entries Civ3's rules haven't got (registered with
advances.icon). Several of MDJ's advances share an entry, and so an icon:
TECH_Portuguese is both Ninjutsu and Contact: Portuguese, TECH_Suieijutsu
both Ronins and Ocean Trade, TECH_Heihojutsu both Rifling and Heihojutsu, and
TECH_Diplomatic both Metal Working and Barrier Tech 3.
"""
import math

import numpy as np

import kit3d as K
import mesh as M
from advances import icon


def _katana(k, base, tip, mat_blade='steel', handle=0.32, guard=True, sheath=False):
    """A katana from its handle's end (base) to its point (tip), gently
    curved; in its black scabbard if sheath."""
    base, tip = np.asarray(base, float), np.asarray(tip, float)
    axis = tip - base
    ln = np.linalg.norm(axis)
    up = np.array([0.0, 0.0, 1.0])
    side = np.cross(axis, up)
    side = side / (np.linalg.norm(side) + 1e-9)
    bend = np.cross(side, axis / ln)
    h_end = base + axis * (handle / ln)
    k.cyl(base, h_end, 0.045, 'navy', seg=12)
    for i in range(4):
        t = (i + 0.5) / 4
        k.sph(base + (h_end - base) * t, 0.05, 'white', seg=10, scale=(1, 1, 0.6))
    if guard:
        k.add(M.cylinder(0.11, 0.11, 0.03, k.m('dark_metal'), seg=18), M.look_along(h_end, h_end + axis / ln * 0.03))
    n = 8
    prev = h_end + axis / ln * 0.03
    for i in range(1, n + 1):
        t = i / n
        p = h_end + (tip - h_end) * t + bend * (0.08 * math.sin(math.pi * t * 0.9) * (ln / 1.6))
        r = (0.05 if sheath else 0.032) * (1 - 0.55 * t ** 3)
        k.cyl(prev, p, r, 'black' if sheath else mat_blade, r1=r * (0.96 if i < n else 0.2), seg=10)
        prev = p


@icon('TECH_Bujutsu', yaw=-10, pitch=6)
def _(k):
    # the martial arts: two katanas crossed
    _katana(k, (-0.7, 0, -0.65), (0.75, 0, 0.75))
    _katana(k, (0.7, 0.05, -0.65), (-0.75, 0.05, 0.75))


@icon('TECH_Kenjutsu', yaw=-18, pitch=8, ground=True)
def _(k):
    # swordsmanship: a katana on its lacquered stand
    k.box((1.6, 0.36, 0.08), 'black', center=(0, 0, 0.04), bevel=0.02)
    for x in (-0.55, 0.55):
        k.box((0.1, 0.3, 0.55), 'black', center=(x, 0, 0.35), bevel=0.02)
        k.box((0.16, 0.3, 0.05), 'dark_red', center=(x, 0, 0.62))
    _katana(k, (-0.82, 0, 0.7), (0.9, 0, 0.72))


@icon('TECH_Kyujutsu', yaw=-12, pitch=4)
def _(k):
    # archery: the tall yumi, gripped below its middle, and an arrow
    pts = []
    for i in range(17):
        t = i / 16
        z = -0.85 + 1.9 * t
        x = 0.28 * math.sin(math.pi * t) - 0.06 * math.sin(2 * math.pi * t)
        pts.append(np.array((x, 0.0, z)))
    for a, b in zip(pts, pts[1:]):
        k.cyl(a, b, 0.035, 'dark_wood', seg=10)
    k.cyl(pts[0], pts[-1], 0.007, 'cloth', seg=6)
    k.cyl(pts[6] + (0, 0, -0.08), pts[6] + (0, 0, 0.1), 0.05, 'red', seg=12)
    k.cyl((-0.55, -0.02, 0.05), (0.62, -0.02, 0.05), 0.016, 'light_wood', seg=8)
    k.add(M.cone(0.04, 0.12, k.m('steel'), seg=10), M.look_along((0.62, -0.02, 0.05), (0.74, -0.02, 0.05)))
    for s in (-1, 1):
        k.extrude([(-0.55, 0.05), (-0.42, 0.05 + 0.06 * s), (-0.36, 0.05)], 0.01, 'white', M.translate(0, -0.02, 0))


@icon('TECH_Bajutsu', yaw=-20, pitch=4)
def _(k):
    # horsemanship: a mounted samurai
    k.unit = 'Nakamaro Horseman'


@icon('TECH_Naginatajutsu', yaw=-14, pitch=6)
def _(k):
    # the naginata: a curved blade on a long pole
    a, b = np.array((-0.75, 0.0, -0.9)), np.array((0.45, 0.0, 0.55))
    k.cyl(a, b, 0.032, 'dark_red', seg=10)
    k.cyl(a, a + (b - a) * 0.06, 0.04, 'dark_metal', seg=10)
    k.add(M.cylinder(0.08, 0.08, 0.03, k.m('dark_metal'), seg=16), M.look_along(b, b + (b - a) * 0.03))
    prev = b
    d = (b - a) / np.linalg.norm(b - a)
    side = np.array((d[2], 0.0, -d[0]))
    for i in range(1, 9):
        t = i / 8
        p = b + d * 0.62 * t + side * 0.14 * t ** 2
        k.cyl(prev, p, 0.05 * (1 - 0.7 * t), 'steel', r1=0.05 * (1 - 0.7 * t) * 0.9, seg=10)
        prev = p


@icon('TECH_Iajutsu', yaw=-12, pitch=6)
def _(k):
    # drawing the sword: a katana half out of its scabbard
    _katana(k, (0.25, 0.04, -0.2), (1.0, 0.04, 0.55), sheath=True, guard=False, handle=0.01)
    _katana(k, (-0.95, 0, -0.78), (0.35, 0, 0.0))


@icon('TECH_Heihojutsu', yaw=-10, pitch=6)
def _(k):
    # strategy: a commander's gunbai war fan with its mon
    n = 200
    im, d = K.canvas(n)
    d.ellipse((20, 10, 180, 160), fill=255)
    d.rectangle((88, 150, 112, 196), fill=255)
    fan = K.mask_of(im)
    im2, d2 = K.canvas(n)
    d2.ellipse((60, 45, 140, 125), fill=255)
    d2.ellipse((72, 57, 128, 113), fill=0)
    for i in range(8):
        a = 2 * math.pi * i / 8
        d2.line((100, 85, 100 + 28 * math.cos(a), 85 + 28 * math.sin(a)), fill=255, width=5)
    mon = K.mask_of(im2) & fan
    k.relief([(fan, 'black', 0.08, 3.0), (mon, 'gold', 0.04, 1.5)], 1.6)


@icon('TECH_Hojutsu', yaw=-14, pitch=6)
def _(k):
    # gunnery: a tanegashima matchlock
    k.extrude([(-0.95, -0.15), (-0.35, 0.0), (0.55, 0.06), (0.55, -0.04), (-0.3, -0.1), (-0.92, -0.32)], 0.1, 'dark_wood')
    k.cyl((0.2, 0, 0.04), (1.05, 0, 0.06), 0.035, 'iron', seg=12)
    k.cyl((1.0, 0, 0.06), (1.06, 0, 0.065), 0.045, 'iron', seg=12)
    k.box((0.18, 0.1, 0.07), 'brass', center=(-0.12, -0.02, 0.0))
    k.cyl((-0.18, -0.06, 0.02), (-0.08, -0.06, 0.16), 0.012, 'brass', seg=8)
    for i in range(10):
        a = i / 9
        k.sph((-0.05 + 0.08 * math.cos(a * 6), -0.12, 0.2 + 0.06 * math.sin(a * 6)), 0.018, 'rope', seg=8)


@icon('TECH_Bushido', yaw=-24, pitch=8, ground=True)
def _(k):
    # the way of the warrior: a samurai's kabuto with its crest
    k.lathe([(0.3, 0.001), (0.3, 0.42), (0.55, 0.43), (0.78, 0.36), (0.9, 0.2), (0.93, 0.001)], 'black', seg=32)
    for z in (0.08, 0.17, 0.26):
        k.torus(0.62 + 0.15 * (0.3 - z) / 0.3, 0.035, 'navy', M.translate(0, 0.08, z) @ M.scale(1.0, 0.92, 0.45), seg=32)
    k.lathe([(0.0, 0.001), (0.0, 0.64), (0.1, 0.66), (0.24, 0.56), (0.32, 0.43)], 'dark_metal', M.translate(0, 0.02, 0.05), seg=28)
    for s in (-1, 1):
        k.extrude([(0, 0), (0.08 * s, 0.25), (0.25 * s, 0.62), (0.18 * s, 0.66), (0.02 * s, 0.3)], 0.03, 'gold',
                  M.translate(0, -0.42, 0.62))
    k.sph((0, -0.42, 0.6), 0.06, 'gold', seg=12)


@icon('TECH_Ronin', yaw=-16, pitch=10, ground=True)
def _(k):
    # the masterless: a straw hat and a sword
    k.lathe([(0.0, 0.001), (0.0, 0.8), (0.04, 0.82), (0.28, 0.42), (0.42, 0.1), (0.45, 0.001)], 'light_wood',
            M.translate(0.25, 0.25, 0.1), seg=36)
    for z, r in ((0.12, 0.66), (0.22, 0.49)):
        k.torus(r, 0.012, 'dark_wood', M.translate(0.25, 0.25, 0.1 + z), seg=36)
    _katana(k, (-0.9, -0.2, 0.05), (0.9, -0.3, 0.12), sheath=True)


@icon('TECH_Suieijutsu', yaw=-8, pitch=4)
def _(k):
    # the art of the sea: seigaiha, the waves of the blue sea, scale upon
    # scale of rings
    n = 220
    rows = [(150, (40, 110, 180)), (110, (75, 145)), (70, (110,))]
    blue_im, bd = K.canvas(n)
    white_im, wd = K.canvas(n)
    for cy, xs in rows:
        for cx in xs:
            for i, r in enumerate((44, 34, 24, 14)):
                fill = 255 if i % 2 == 0 else 0
                bd.pieslice((cx - r, cy - r, cx + r, cy + r), 180, 360, fill=fill)
                wd.pieslice((cx - r, cy - r, cx + r, cy + r), 180, 360, fill=255 - fill)
            bd.rectangle((cx - 44, cy, cx + 44, cy + 2), fill=0)
            wd.rectangle((cx - 44, cy, cx + 44, cy + 2), fill=0)
    outline_im, od = K.canvas(n)
    for cy, xs in rows:
        for cx in xs:
            od.pieslice((cx - 44, cy - 44, cx + 44, cy + 44), 180, 360, fill=255)
    shape = K.mask_of(outline_im)
    k.relief([(shape, 'navy', 0.06, 2.0), (K.mask_of(white_im) & shape, 'white', 0.03, 1.0)], 1.8)


@icon('TECH_Portuguese', yaw=-24, pitch=4)
def _(k):
    # contact with the Portuguese: their carrack
    k.unit = 'Caravel'


@icon('TECH_Trade', yaw=-18, pitch=10, ground=True)
def _(k):
    # akinai, commerce: gold koban coins, oval, in a little stack
    for i, (x, y) in enumerate(((0.0, 0.0), (0.04, 0.02), (-0.03, 0.01), (0.02, -0.02))):
        k.lathe([(0, 0.001), (0, 0.52), (0.04, 0.55), (0.06, 0.001)], 'gold', M.translate(x, y, i * 0.07) @ M.scale(0.75, 1.0, 1.0), seg=32)
    k.lathe([(0, 0.001), (0, 0.52), (0.04, 0.55), (0.06, 0.001)], 'gold',
            M.translate(0.75, -0.2, 0.32) @ M.rot_y(70) @ M.scale(0.75, 1.0, 1.0), seg=32)


@icon('TECH_Diplomatic', yaw=-22, pitch=8, ground=True)
def _(k):
    # metalworking: a smith's anvil and hammer
    k.box((0.5, 0.36, 0.3), 'iron', center=(0, 0, 0.15))
    k.box((0.28, 0.24, 0.3), 'iron', center=(0, 0, 0.45))
    k.box((1.05, 0.34, 0.18), 'iron', center=(0.05, 0, 0.66), bevel=0.02)
    k.add(M.cone(0.15, 0.45, k.m('iron'), seg=16), M.look_along((-0.47, 0, 0.66), (-0.92, 0, 0.66)))
    k.cyl((0.15, -0.35, 0.78), (0.85, -0.7, 0.95), 0.03, 'wood', seg=10)
    k.box((0.26, 0.12, 0.12), 'dark_metal', center=(0.15, -0.35, 0.8))
