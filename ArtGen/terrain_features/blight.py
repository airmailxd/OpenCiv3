"""Pollution (Art/Terrain/pollution.pcx) and craters (Art/Terrain/craters.pcx).

Both are 5x5 grids of 128x64 tiles. Pollution: rows 0-1 are ten single-tile
variants, rows 2-4 the fifteen ways a polluted tile joins polluted diagonal
neighbors (the sludge reaches the shared tile edges there). The original's
patches become glossy, smooth-edged pools of sludge with the same outline,
kept straight along tile edges so they join their neighbors.

Craters: each original crater (a dark pit in a blob of churned earth) is found
and redrawn as a shaded bowl with a raised rim lit from the north-west and a
scatter of ejecta, at the same place and size.
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
SS = 3
R = S * SS

LIGHT3 = np.array([-0.55, -0.62, 0.56])
LIGHT3 = LIGHT3 / np.linalg.norm(LIGHT3)


def shade_height(hgt, strength=1.0):
    """Lambert shading of a height field (render resolution) lit from the NW;
    returns the shade relative to flat ground (1.0 = flat)."""
    gy, gx = np.gradient(hgt)
    nx, ny, nz = -gx * strength, -gy * strength, np.ones_like(hgt)
    n = np.sqrt(nx * nx + ny * ny + nz * nz)
    lam = (nx * LIGHT3[0] + ny * LIGHT3[1] + nz * LIGHT3[2]) / n
    return lam / LIGHT3[2], (nx / n, ny / n, nz / n)


# ---------------------------------------------------------------- pollution

SLUDGE = np.array([188, 134, 52]) / 255
SLUDGE_DARK = np.array([112, 76, 34]) / 255
SLUDGE_HI = np.array([228, 198, 132]) / 255


def smooth_patch(alpha, sigma=1.1, lo=0.42, hi=0.58):
    h, w = CH * R, CW * R
    up = np.kron(alpha.astype(float), np.ones((R, R)))
    dia = lw.diamond_mask(w, h)
    inside = (dia > 0.5).astype(float)
    num = ndimage.gaussian_filter(up * inside, sigma * R)
    den = ndimage.gaussian_filter(inside, sigma * R)
    sm = num / np.maximum(den, 1e-6)
    return lw.smoothstep(lo, hi, sm) * dia, sm


def draw_pollution(alpha, seed):
    h, w = CH * R, CW * R
    m, sm = smooth_patch(alpha)
    # Height: thicker towards the middle of each pool, with soft lumps.
    inside = ndimage.distance_transform_edt(m > 0.5)
    noise = lw.value_noise((h, w), 5 * R, seed)
    hgt = np.clip(inside / (2.5 * R), 0, 1) ** 0.6 * (0.7 + 0.6 * noise)
    hgt = ndimage.gaussian_filter(hgt, 0.8 * R) * 2.2 * R
    lam, (nx, ny, nz) = shade_height(hgt, 1.0)
    depth = np.clip(inside / (3.0 * R), 0, 1)
    col = SLUDGE_DARK[None, None] * (1 - depth[..., None]) * 0.6 + SLUDGE[None, None] * (0.4 + 0.6 * depth[..., None])
    col = col * np.clip(0.75 + 0.35 * lam, 0.5, 1.3)[..., None]
    # Glossy highlight: reflection of the light.
    hv = LIGHT3 + np.array([0, 0, 1.0]); hv = hv / np.linalg.norm(hv)
    spec = np.clip(nx * hv[0] + ny * hv[1] + nz * hv[2], 0, 1) ** 40
    col = col + (SLUDGE_HI - col) * np.clip(spec * 0.9, 0, 1)[..., None]
    # Dark crust at the rim.
    rim = 1 - lw.smoothstep(0.0, 1.2 * R, inside)
    col = col * (1 - 0.45 * rim[..., None])
    img = lw.premul(np.clip(col, 0, 1), m)
    return lw.downsample(img, SS)


# ---------------------------------------------------------------- craters

EARTH = np.array([150, 124, 96]) / 255
EARTH_LIGHT = np.array([222, 192, 146]) / 255
PIT = np.array([44, 38, 40]) / 255


def find_craters(cell_rgba):
    """(cx, cy, r) of each crater in a 1x cell: its dark pit's centroid and a
    radius from the pit's size."""
    rgb = cell_rgba[..., :3].astype(int)
    op = cell_rgba[..., 3] > 0
    dark = op & (rgb.sum(-1) < 210)
    dark = ndimage.binary_closing(dark, iterations=1)
    lab, n = ndimage.label(dark)
    out = []
    for i in range(1, n + 1):
        ys, xs = np.nonzero(lab == i)
        if len(xs) < 6:
            continue
        rx = (xs.max() - xs.min() + 1) / 2
        out.append((xs.mean() + 0.5, ys.mean() + 0.5 - 0.5, max(rx, 2.0)))
    return out


def draw_craters(cell_rgba, seed):
    h, w = CH * R, CW * R
    craters = find_craters(cell_rgba)
    yy, xx = np.mgrid[0:h, 0:w].astype(float) + 0.5
    X, Y = xx / R, yy / R
    rng = np.random.RandomState(seed)
    hgt = np.zeros((h, w))
    cover = np.zeros((h, w))
    pit = np.zeros((h, w))
    noise = lw.value_noise((h, w), 2.5 * R, seed, octaves=3)
    for (cx, cy, r) in craters:
        rr = r * 1.15
        d = np.hypot((X - cx) / rr, (Y - cy) / (rr * 0.5))  # iso ellipse, 1 at the bowl's edge
        bowl = -np.clip(1 - d ** 2, 0, 1) * 1.0
        rim = np.exp(-((d - 1.05) / 0.28) ** 2) * 0.75
        ejecta = np.exp(-((d - 1.4) / 0.5) ** 2) * 0.25 * noise
        hgt += (bowl + rim + ejecta) * rr * R * 0.55
        cover = np.maximum(cover, lw.smoothstep(2.05 + 0.35 * (noise - 0.5), 1.55, d))
        pit = np.maximum(pit, lw.smoothstep(0.85, 0.35, d))
    lam, _ = shade_height(ndimage.gaussian_filter(hgt, 0.5 * R), 1.0)
    col = EARTH[None, None] * (0.85 + 0.3 * noise[..., None])
    col = col + (EARTH_LIGHT - col) * np.clip((lam - 1.0) * 1.4, 0, 1)[..., None]
    col = col * np.clip(0.55 + 0.45 * lam, 0.25, 1.0)[..., None]
    col = col * (1 - pit[..., None] * 0.35) + PIT[None, None] * pit[..., None] * 0.35
    img = lw.premul(np.clip(col, 0, 1), cover)
    return lw.downsample(img, SS)


# ---------------------------------------------------------------- build

def build_sheet(rel, draw, seed0):
    orig = civ3art.load_original(rel)
    out = np.zeros((orig.shape[0] * S, orig.shape[1] * S, 4))
    for i in range(25):
        r, c = divmod(i, 5)
        cell = orig[r * CH:(r + 1) * CH, c * CW:(c + 1) * CW]
        if not (cell[..., 3] > 0).any():
            continue
        if draw is draw_pollution:
            img = draw(cell[..., 3] > 0, seed0 + i)
        else:
            img = draw(cell, seed0 + i)
        out[r * CH * S:(r + 1) * CH * S, c * CW * S:(c + 1) * CW * S] = img
    rgba = lw.to_rgba8(out)
    civ3art.save_modern(rel, rgba)
    civ3art.save_preview(os.path.basename(rel)[:-4], civ3art.upscale_nearest(orig), rgba)
    return rgba


def build():
    build_sheet('Art/Terrain/pollution.pcx', draw_pollution, 5000)
    build_sheet('Art/Terrain/craters.pcx', draw_craters, 6000)


if __name__ == '__main__':
    build()
