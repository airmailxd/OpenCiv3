"""Terrain yield markers: Art/Terrain/tnt.pcx (3 columns x 6 rows of 128x64 tiles).

Tiny markers scattered on a tile: leafy sprouts (food), blue-white ore chips
(shields), white stones (the bonus grassland marker the game draws), grass tufts
and round shrubs. Each original marker is found and redrawn as a small shaded
object at the same place and size.
"""
import os
import sys

import numpy as np
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, '..', 'common'))
import civ3art  # noqa: E402
import linework as lw  # noqa: E402

CW, CH = 128, 64
S = civ3art.SCALE
SS = 4
R = S * SS
L2 = np.array([-0.6, -0.7]) / np.hypot(0.6, 0.7)


def blob(img, X, Y, cx, cy, rx, ry, color, rot=0.0, gloss=0.3, outline=0.45):
    """A shaded ellipsoid (1x units) composited onto img (premultiplied, render res)."""
    c, s = np.cos(rot), np.sin(rot)
    dx, dy = X - cx, Y - cy
    u = (dx * c + dy * s) / rx
    v = (-dx * s + dy * c) / ry
    d = np.sqrt(u * u + v * v)
    aa = 1.0 / (R * min(rx, ry))
    a = lw.smoothstep(1 + aa, 1 - aa, d)
    if not a.any():
        return img
    nz = np.sqrt(np.clip(1 - d * d, 0, 1))
    # Normal in screen space: rotate (u, v) back.
    nx = u * c - v * s
    ny = u * s + v * c
    lam = np.clip(-(nx * L2[0] + ny * L2[1]) * 0.75 + nz * 0.55, 0, 1.2)
    col = np.asarray(color, float)[None, None] * (0.45 + 0.7 * lam[..., None])
    spec = np.clip(-(nx * L2[0] + ny * L2[1]) * 0.6 + nz * 0.8, 0, 1) ** 12 * gloss
    col = col + (1 - col) * spec[..., None]
    rim = lw.smoothstep(1 - 3 * aa, 1, d) * outline
    col = col * (1 - rim[..., None] * 0.6)
    return lw.over(img, lw.premul(np.clip(col, 0, 1), a))


def shadow(img, X, Y, cx, cy, rx, ry, alpha=0.35):
    d = np.hypot((X - cx) / rx, (Y - cy) / ry)
    a = lw.smoothstep(1.0, 0.3, d) * alpha
    return lw.over(img, lw.premul((0, 0, 0), a))


def sprout(img, X, Y, cx, cy, size, rng):
    img = shadow(img, X, Y, cx + 0.8, cy + 1.2, size * 0.75, size * 0.35)
    g = np.array([112, 158, 76]) / 255
    for ang in (-2.4, -0.8, 0.8, 2.4)[:3 + rng.randint(2)]:
        a = ang + rng.uniform(-0.25, 0.25)
        lx, ly = cx + np.cos(a - np.pi / 2) * size * 0.32, cy + np.sin(a - np.pi / 2) * size * 0.2
        img = blob(img, X, Y, lx, ly, size * 0.36, size * 0.2, g * rng.uniform(0.9, 1.1), rot=a - np.pi / 2, gloss=0.25)
    img = blob(img, X, Y, cx, cy - size * 0.05, size * 0.14, size * 0.12, np.array([150, 182, 106]) / 255, gloss=0.2)
    return img


def chip(img, X, Y, cx, cy, size, color, rng):
    img = shadow(img, X, Y, cx + 0.9, cy + 1.1, size * 0.7, size * 0.32)
    rot = rng.uniform(-0.6, -0.2)
    img = blob(img, X, Y, cx, cy, size * 0.55, size * 0.32, color, rot=rot, gloss=0.6)
    img = blob(img, X, Y, cx + size * 0.28, cy + size * 0.12, size * 0.3, size * 0.22, color * 0.92, rot=rot, gloss=0.6)
    return img


def tuft(img, X, Y, cx, cy, size, rng):
    img = shadow(img, X, Y, cx + 0.6, cy + 0.8, size * 0.6, size * 0.25, 0.25)
    g = np.array([104, 136, 72]) / 255
    for k in range(5):
        a = -np.pi / 2 + (k - 2) * 0.38 + rng.uniform(-0.1, 0.1)
        lx, ly = cx + np.cos(a) * size * 0.3, cy + np.sin(a) * size * 0.3 + size * 0.1
        img = blob(img, X, Y, lx, ly, size * 0.34, size * 0.09, g * rng.uniform(0.85, 1.15), rot=a, gloss=0.1, outline=0.3)
    return img


def bush(img, X, Y, cx, cy, size, rng):
    img = shadow(img, X, Y, cx + 1.0, cy + 1.0, size * 0.7, size * 0.35)
    g = np.array([136, 154, 88]) / 255
    img = blob(img, X, Y, cx, cy, size * 0.55, size * 0.42, g * 0.85, gloss=0.15)
    for k in range(4):
        a = k * 1.6 + rng.uniform(0, 0.5)
        img = blob(img, X, Y, cx + np.cos(a) * size * 0.22 - 0.3, cy + np.sin(a) * size * 0.14 - 0.4, size * 0.26, size * 0.22, g * rng.uniform(0.95, 1.15), gloss=0.15, outline=0.2)
    return img


def draw_cell(cell, row, seed):
    h, w = CH * R, CW * R
    rng = np.random.RandomState(seed)
    img = np.zeros((h, w, 4))
    op = cell[..., 3] > 0
    lab, n = ndimage.label(op, structure=np.ones((3, 3)))
    yy, xx = np.mgrid[0:h, 0:w].astype(float) + 0.5
    X, Y = xx / R, yy / R
    items = []
    for i in range(1, n + 1):
        ys, xs = np.nonzero(lab == i)
        if len(xs) < 4:
            continue
        rgb = cell[ys, xs, :3].astype(float).mean(0)
        size = max(xs.max() - xs.min() + 1, (ys.max() - ys.min() + 1) * 1.4)
        items.append((ys.mean(), xs.mean() + 0.5, ys.mean() + 0.5, size, rgb))
    for _, cx, cy, size, rgb in sorted(items):
        sat = rgb.max() - rgb.min()
        if rgb[2] > rgb[1] - 10 and rgb[2] > rgb[0] + 15:
            img = chip(img, X, Y, cx, cy, size, np.array([160, 178, 196]) / 255, rng)
        elif sat < 45:
            img = chip(img, X, Y, cx, cy, size, np.array([214, 214, 204]) / 255, rng)
        elif row == 4:
            img = tuft(img, X, Y, cx, cy, size, rng)
        elif row == 5:
            img = bush(img, X, Y, cx, cy, size, rng)
        else:
            img = sprout(img, X, Y, cx, cy, size, rng)
    return lw.downsample(img, SS)


def build():
    rel = 'Art/Terrain/tnt.pcx'
    orig = civ3art.load_original(rel)
    out = np.zeros((orig.shape[0] * S, orig.shape[1] * S, 4))
    for r in range(6):
        for c in range(3):
            cell = orig[r * CH:(r + 1) * CH, c * CW:(c + 1) * CW]
            if not (cell[..., 3] > 0).any():
                continue
            out[r * CH * S:(r + 1) * CH * S, c * CW * S:(c + 1) * CW * S] = draw_cell(cell, r, 7000 + r * 3 + c)
    rgba = lw.to_rgba8(out)
    civ3art.save_modern(rel, rgba)
    civ3art.save_preview('tnt', civ3art.upscale_nearest(orig), rgba, background=(105, 145, 50))
    return rgba


if __name__ == '__main__':
    build()
