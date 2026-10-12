"""Advance icons: one symbolic object per advance (kit3d), on a backdrop
colored by era."""
import math

import numpy as np
from PIL import ImageDraw

import common as C
import kit3d as K
import mesh as M

ERA_COLORS = {
    'ERAS_Ancient_Times': ('e9d9b0', 'a78a58'),
    'ERAS_Middle_Ages': ('d9e2c8', '7f9668'),
    'ERAS_Industrial_Age': ('d8dfe6', '76889c'),
    'ERAS_Modern_Era': ('e2dbe8', '86789c'),
}

ICONS = {}


def icon(key, yaw=-20.0, pitch=0.0, fill=0.8, ground=False, small_fill=None):
    def deco(fn):
        ICONS[key] = dict(fn=fn, yaw=yaw, pitch=pitch, fill=fill, ground=ground, small_fill=small_fill)
        return fn
    return deco


def make(tech):
    key = tech['civilopediaEntry']
    spec = ICONS[key]
    k = K.Kit()
    spec['fn'](k)
    inner, outer = ERA_COLORS.get(tech.get('eraCivilopediaName'), ERA_COLORS['ERAS_Ancient_Times'])
    if getattr(k, 'unit', None):
        return unit_on_backdrop(k.unit, inner, outer, spec['fill'])
    return K.compose(k, inner, outer, spec['yaw'], spec['pitch'], spec['fill'], spec['ground'], spec['small_fill'])


def unit_on_backdrop(folder, inner, outer, fill=0.86):
    """A unit's model (ArtGen/units) on an advance's backdrop."""
    import units as U
    out = []
    for n, f in ((C.LARGE, fill), (C.SMALL * 2, min(0.95, fill * 1.08))):
        bg = K.radial_backdrop(n, inner, outer)
        u = U.render_unit(folder, n, U.DEFAULT_TINT, 'air', fill=f)
        img = C.over(u, bg)
        img[..., :3] = C.frame_edges(img[..., :3])
        out.append(img)
    return out[0], C.sharpen(C.downsample(out[1], 2), 0.35, 0.5)


# ================================================================ ancient

@icon('TECH_Bronze_Working', yaw=-15, pitch=10)
def _(k):
    # a labrys (double axe) of bronze on a wooden haft
    blade = [(0.0, 0.18), (0.55, 0.62), (0.68, 0.3), (0.7, 0.0), (0.68, -0.3), (0.55, -0.62), (0.0, -0.18)]
    k.extrude(blade, 0.08, 'bronze', M.translate(0.05, 0, 0.55))
    k.extrude([(-x, z) for x, z in blade][::-1], 0.08, 'bronze', M.translate(-0.05, 0, 0.55))
    k.cyl((0, 0, -0.75), (0, 0, 1.0), 0.06, 'wood')
    k.cyl((0, 0, 0.42), (0, 0, 0.68), 0.1, 'bronze')


@icon('TECH_Masonry', yaw=-30, pitch=5, ground=True)
def _(k):
    # stacked dressed stone blocks
    rng = np.random.default_rng(2)
    rows = [(-0.9, 3), (-0.45, 3), (0.0, 2), (0.45, 1)]
    for r, (x0, n) in enumerate([(0, 4), (0.25, 3), (0.5, 2), (0.75, 1)]):
        for i in range(n):
            c = (x0 + i * 0.52 - 0.8, 0, 0.17 + r * 0.33)
            shade = ['stone', 'dark_stone'][(i + r) % 2]
            k.box((0.5, 0.5, 0.31), shade, center=c, bevel=0.02)


@icon('TECH_Alphabet', yaw=-24, pitch=8)
def _(k):
    # letter tiles
    for i, (ch, x, z) in enumerate((('A', -0.62, 0.15), ('B', 0.0, -0.05), ('C', 0.62, 0.15))):
        k.box((0.56, 0.18, 0.62), 'light_wood', center=(x, 0.09, z), bevel=0.04)
        mask = K.text_mask(ch, 128, 0.78)
        k.relief([(mask, 'dark_red', 0.06, 2.5)], 0.56, M.translate(x, 0.0, z))


@icon('TECH_Pottery', yaw=0, ground=True)
def _(k):
    # an amphora
    prof = [(0, 0.001), (0.02, 0.12), (0.12, 0.2), (0.35, 0.38), (0.6, 0.44), (0.85, 0.38), (1.05, 0.22),
            (1.2, 0.13), (1.42, 0.12), (1.48, 0.15), (1.5, 0.001)]
    k.lathe(prof, 'terracotta', seg=36)
    for s in (-1, 1):
        k.add(M.capsule((s * 0.12, 0, 1.36), (s * 0.34, 0, 1.25), 0.035, 0.035, k.m('terracotta')))
        k.add(M.capsule((s * 0.34, 0, 1.25), (s * 0.3, 0, 0.98), 0.035, 0.035, k.m('terracotta')))
    # painted bands
    k.lathe([(0.62, 0.445), (0.66, 0.445), (0.7, 0.44)], 'ink', seg=36)
    k.lathe([(0.38, 0.385), (0.42, 0.395)], 'ink', seg=36)


@icon('TECH_The_Wheel', yaw=-28, pitch=8)
def _(k):
    T = M.rot_x(90)  # wheel in the x-z plane
    k.torus(0.9, 0.08, 'wood', T, seg=40)
    k.torus(0.9, 0.06, 'iron', M.translate(0, -0.02, 0) @ T @ M.scale(1.0, 1.0, 1.0), seg=40)
    k.cyl((0, -0.16, 0), (0, 0.16, 0), 0.17, 'dark_wood')
    for a in range(0, 360, 45):
        r = math.radians(a)
        k.cyl((0.15 * math.cos(r), 0, 0.15 * math.sin(r)), (0.86 * math.cos(r), 0, 0.86 * math.sin(r)), 0.045, 'wood', r1=0.035)


@icon('TECH_Currency', yaw=-10, pitch=-5, ground=True)
def _(k):
    for i in range(6):
        k.cyl((0.35 + 0.01 * (i % 2), 0.1, i * 0.09), (0.35 + 0.01 * (i % 2), 0.1, i * 0.09 + 0.08), 0.36, 'gold', seg=36)
    for i in range(3):
        k.cyl((-0.5, 0.35, i * 0.09), (-0.5, 0.35, i * 0.09 + 0.08), 0.36, 'gold', seg=36)
    # a coin standing up, with a head on it
    T = M.translate(-0.3, -0.25, 0.5) @ M.rot_z(20)
    k.add(M.cylinder(0.48, 0.48, 0.08, k.m('gold'), seg=40), T @ M.rot_x(90) @ M.translate(0, 0, -0.04))
    im, d = K.canvas(128)
    d.ellipse((14, 14, 114, 114), outline=255, width=6)
    d.ellipse((40, 30, 86, 76), fill=255)
    d.polygon([(46, 70), (82, 70), (92, 100), (36, 100)], fill=255)
    k.relief([(K.mask_of(im), 'gold', 0.04, 2.0)], 0.9, T @ M.translate(0, -0.045, 0))


# The other advances, by era (each module registers its icons with @icon).
import adv_ancient  # noqa: E402,F401
import adv_middle  # noqa: E402,F401
import adv_industrial  # noqa: E402,F401
import adv_modern  # noqa: E402,F401
