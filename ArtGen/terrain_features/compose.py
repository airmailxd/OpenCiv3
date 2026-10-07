"""Test layouts: the remade linear features composed on an iso grid the way the game
places them, to check that pieces join across tiles. Writes to ArtGen/_previews."""
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, '..', 'common'))
import civ3art  # noqa: E402

DIRS = {  # flag bit -> (dx, dy) in tile coordinates (x+y even)
    0: (1, -1), 1: (2, 0), 2: (1, 1), 3: (0, 2), 4: (-1, 1), 5: (-2, 0), 6: (-1, -1), 7: (0, -2)}


def base_map(nx, ny, s, colors=((111, 154, 53), (98, 140, 46))):
    """A canvas with plain diamonds for tiles (x in 0..nx, y in 0..ny, x+y even)."""
    w, h = (nx + 1) * 64 * s, (ny + 1) * 32 * s
    img = np.zeros((h, w, 4), np.uint8)
    yy, xx = np.mgrid[0:h, 0:w]
    tx = (xx + 0.5) / (64 * s) - 1; ty = (yy + 0.5) / (32 * s) - 1
    a = np.round((tx + ty) / 2); b = np.round((ty - tx) / 2)
    c = np.where(((a + b) % 2 == 0)[..., None], np.array(colors[0]), np.array(colors[1]))
    img[..., :3] = c
    img[..., 3] = 255
    return img


def paste(canvas, sprite, x, y):
    """Alpha-composites sprite (RGBA uint8) with its top-left at (x, y)."""
    H, W = canvas.shape[:2]
    h, w = sprite.shape[:2]
    x0, y0 = max(0, x), max(0, y)
    x1, y1 = min(W, x + w), min(H, y + h)
    if x1 <= x0 or y1 <= y0:
        return
    s = sprite[y0 - y:y1 - y, x0 - x:x1 - x].astype(float) / 255
    d = canvas[y0:y1, x0:x1].astype(float) / 255
    a = s[..., 3:4]
    d[..., :3] = s[..., :3] * a + d[..., :3] * (1 - a)
    d[..., 3:4] = a + d[..., 3:4] * (1 - a)
    canvas[y0:y1, x0:x1] = (d * 255 + 0.5).astype(np.uint8)


def compose_roads(sheet, tiles, nx, ny, s, base=None):
    """Draws the road-like sheet (16x16 cells) for the set of tiles {(x, y)}."""
    cw, ch = 128 * s, 64 * s
    img = base_map(nx, ny, s) if base is None else base.copy()
    for (x, y) in sorted(tiles, key=lambda t: (t[1], t[0])):
        idx = 0
        for b, (dx, dy) in DIRS.items():
            if (x + dx, y + dy) in tiles:
                idx |= 1 << b
        r, c = divmod(idx, 16)
        cell = sheet[r * ch:(r + 1) * ch, c * cw:(c + 1) * cw]
        paste(img, cell, x * 64 * s + 64 * s - cw // 2, y * 32 * s + 32 * s - ch // 2)
    return img


def random_network(nx, ny, seed, n=40):
    rng = np.random.RandomState(seed)
    tiles = set()
    x, y = nx // 2 + (ny // 2 + nx // 2) % 2, ny // 2
    x = x if (x + y) % 2 == 0 else x + 1
    for _ in range(n):
        tiles.add((x, y))
        b = rng.randint(8)
        dx, dy = DIRS[b]
        if 1 <= x + dx <= nx - 1 and 1 <= y + dy <= ny - 1:
            x, y = x + dx, y + dy
    return tiles


def compose_rivers(narrow, broad, nx, ny, s, seed, steps=60, base=None):
    """Random river network along tile edges; cells centered on tile corners."""
    rng = np.random.RandomState(seed)
    edges = set()
    X, Y = nx // 2 + 1, ny // 2
    if (X + Y) % 2 == 0:
        X += 1
    for _ in range(steps):
        dx, dy = [(-1, -1), (1, -1), (-1, 1), (1, 1)][rng.randint(4)]
        if 1 <= X + dx <= nx and 1 <= Y + dy <= ny - 1:
            edges.add(frozenset([(X, Y), (X + dx, Y + dy)]))
            X, Y = X + dx, Y + dy
    pts = {p for e in edges for p in e}
    img = base_map(nx, ny, s) if base is None else base.copy()
    cw, ch = 128 * s, 64 * s
    for (X, Y) in sorted(pts, key=lambda p: (p[1], p[0])):
        idx = 0
        for b, (dx, dy) in enumerate([(-1, -1), (1, -1), (-1, 1), (1, 1)]):
            if frozenset([(X, Y), (X + dx, Y + dy)]) in edges:
                idx |= 1 << b
        sheet = broad if idx in (1, 2, 4, 8) else narrow
        r, c = divmod(idx, 4)
        cell = sheet[r * ch:(r + 1) * ch, c * cw:(c + 1) * cw]
        # Corner (X, Y) is at screen (X*64, Y*32) in tile units, offset like base_map's tiles.
        paste(img, cell, X * 64 * s + 64 * s - cw // 2 - 64 * s, Y * 32 * s + 32 * s - ch // 2)
    return img
