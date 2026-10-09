"""Remake of Art/Terrain/goodyhuts.pcx: the tribal villages of goody huts, drawn
with the renderer from buildings.py.

The sheet is a 3x3 grid of iso tile cells (128x64, 256x128 at 2x) holding eight
variations of a village, read left to right and top to bottom; the last cell is
empty. Like the originals, each is a cluster of round huts with domed thatch
roofs, here around a beaten-earth yard with a fire pit, a few bundles and
bushes. The layouts are random, from fixed seeds.
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'common'))
import civ3art  # noqa: E402
from buildings import (  # noqa: E402
    CW, CH, Scene, render, rgb, patch_decal, bushes, on_grass, _mean_sat,
    THATCH, PLAIN, WOOD, CANVAS, DARK, ROCK, DARKHOLE, GRASS2,
)

REL = 'Art/Terrain/goodyhuts.pcx'
COLS, ROWS = 3, 3
VARIANTS = 8

# The originals' straw and mud, a little greyer.
THATCH_COLS = [rgb(214, 178, 96), rgb(196, 162, 86), rgb(226, 194, 116), rgb(182, 150, 82)]
WALL_COLS = [rgb(170, 138, 92), rgb(156, 126, 84), rgb(184, 152, 104)]
YARD = rgb(178, 156, 98)


def round_hut(s, x, y, r, h, wall, roof, door_angle):
    """A round mud hut with a domed thatch roof overhanging its wall."""
    s.obj()
    wall_h = h * 0.38
    s.cylinder(x, y, r, 0, wall_h, wall, PLAIN, n=24, top=False)
    s.dome(x, y, r * 1.18, r * 1.18, wall_h * 0.85, h - wall_h * 0.85, roof, THATCH, n=24, m=8)
    # A dark doorway on the side facing the camera.
    dx, dy = math.cos(door_angle), math.sin(door_angle)
    s.beam((x + dx * (r - 0.6), y + dy * (r - 0.6), 0), (x + dx * (r - 0.6), y + dy * (r - 0.6), wall_h * 0.9),
           r * 0.42, DARKHOLE, DARK)
    # A little topknot of straw.
    s.obj()
    s.cone(x, y, r * 0.28, h - 0.6, r * 0.35, roof * 0.86, THATCH, n=10)


def village(s, seed):
    rng = np.random.default_rng(seed)
    huts = []
    # One big hut near the middle, then smaller ones around it, kept apart.
    want = int(rng.integers(5, 8))
    tries = 0
    while len(huts) < want and tries < 400:
        tries += 1
        if not huts:
            x, y, r = rng.uniform(-6, 6), rng.uniform(-6, 6), rng.uniform(10, 12)
        else:
            a = rng.uniform(0, 2 * math.pi)
            d = rng.uniform(16, 36)
            x, y, r = d * math.cos(a), d * math.sin(a), rng.uniform(6, 9.5)
        # Inside the tile's diamond, with room for the roof's overhang.
        if abs(x) + abs(y) > 50 - r:
            continue
        if any(math.hypot(x - hx, y - hy) < (r + hr) * 1.25 + 1.5 for hx, hy, hr in huts):
            continue
        huts.append((x, y, r))

    blobs = [(x, y, r * 1.9, r * 1.6) for x, y, r in huts]
    s.decal(patch_decal(blobs, YARD, seed=seed, grass=0.3, soft=4, alpha=0.8, col2=GRASS2))

    # Draw the back huts first; the renderer's z-buffer sorts the rest.
    for x, y, r in sorted(huts, key=lambda h: h[0] + h[1]):
        h = r * rng.uniform(1.45, 1.7)
        roof = THATCH_COLS[int(rng.integers(len(THATCH_COLS)))] * rng.uniform(0.95, 1.04)
        wall = WALL_COLS[int(rng.integers(len(WALL_COLS)))]
        # Doors face the camera, between +x and +y.
        round_hut(s, x, y, r, h, wall, roof, rng.uniform(0.15, 1.4))

    # A fire pit in an open spot of the yard.
    for _ in range(60):
        fx, fy = rng.uniform(-20, 20), rng.uniform(-20, 20)
        if all(math.hypot(fx - hx, fy - hy) > hr * 1.2 + 5 for hx, hy, hr in huts):
            s.obj()
            s.cylinder(fx, fy, 3.2, 0, 1.2, rgb(112, 102, 88), ROCK, n=12, top_col=rgb(52, 42, 32), top_mat=DARK)
            break

    # A couple of bundles and a few bushes around the edge.
    for _ in range(int(rng.integers(1, 3))):
        bx, by = rng.uniform(-30, 30), rng.uniform(-30, 30)
        if abs(bx) + abs(by) < 44 and all(math.hypot(bx - hx, by - hy) > hr * 1.2 + 4 for hx, hy, hr in huts):
            s.obj()
            s.cbox(bx, by, 4.5, 3.5, 0, 3, rgb(166, 144, 88), CANVAS)
    spots = []
    for _ in range(int(rng.integers(2, 5))):
        a = rng.uniform(0, 2 * math.pi)
        bx, by = 42 * math.cos(a), 42 * math.sin(a)
        if abs(bx) + abs(by) < 50 and all(math.hypot(bx - hx, by - hy) > hr * 1.2 + 5 for hx, hy, hr in huts):
            spots.append((bx, by, rng.uniform(3.5, 5.5)))
    if spots:
        bushes(s, spots)


# Kept at most this fraction of the originals' saturation, as in buildings.py.
MUTE = 0.9


def mute(sheet, orig):
    """Keeps the sheet's saturation under the originals' (averaged over the
    cells that have art)."""
    a = sheet[..., 3] / 255.0
    target = MUTE * _mean_sat(orig[..., :3] / 255.0, orig[..., 3] / 255.0)
    rgb_ = sheet[..., :3] / 255.0
    for _ in range(4):
        sat = _mean_sat(rgb_, a)
        if sat <= target + 1e-3:
            break
        lum = (rgb_ @ np.array([0.3, 0.55, 0.15]))[..., None]
        rgb_ = np.clip(lum + (rgb_ - lum) * (target / sat), 0, 1)
    out = sheet.copy()
    out[..., :3] = np.round(rgb_ * 255).astype(np.uint8)
    return out


def build_sheet():
    sheet = np.zeros((CH * ROWS, CW * COLS, 4), np.uint8)
    for i in range(VARIANTS):
        c, r = i % COLS, i // COLS
        s = Scene()
        village(s, seed=101 + 17 * i)
        sheet[r * CH:(r + 1) * CH, c * CW:(c + 1) * CW] = render(s)
    return sheet


def build():
    orig1 = civ3art.load_original(REL, shadows=True)
    sheet = mute(build_sheet(), orig1)
    civ3art.save_modern(REL, sheet)
    orig = civ3art.upscale_nearest(orig1)
    civ3art.save_preview('goodyhuts', orig, sheet)
    civ3art.save_preview('goodyhuts_1x', on_grass(orig1, cell=(CW // 2, CH // 2)), on_grass(sheet, 0.5))
    return sheet


if __name__ == '__main__':
    build()
