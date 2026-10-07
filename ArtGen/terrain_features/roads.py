"""Roads (Art/Terrain/roads.pcx) and railroads (Art/Terrain/railroads.pcx).

Both sheets are 16x16 grids of 128x64 cells; a cell's index is the set of
neighbors with a road (bit 0 NE, 1 E, 2 SE, 3 S, 4 SW, 5 W, 6 NW, 7 N). Each
cell's paths are traced from the original (see linework.traced_lines) and drawn
anew: roads as packed dirt tracks, railroads as steel rails on wooden ties over a
gravel bed. Ends at the neighbors' connection points are exact, so pieces join.
"""
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, '..', 'common'))
import civ3art  # noqa: E402
import linework as lw  # noqa: E402

CW, CH = 128, 64      # cell size at 1x
S = civ3art.SCALE     # 2
SS = 3                # supersampling
R = S * SS            # 1x -> render resolution


def cell_conns(index):
    return [lw.ROAD_POINTS[b] for b in range(8) if index >> b & 1]


def lone_cross(alpha):
    """Index 0 (no neighbors): a short cross where the original's is."""
    ys, xs = np.nonzero(alpha)
    if len(xs) == 0:
        cx, cy = 64.0, 32.0
    else:
        cx, cy = xs.mean() + 0.5, ys.mean() + 0.5
    a = 9.0
    return [np.array([[cx - a, cy - a / 2], [cx + a, cy + a / 2]]),
            np.array([[cx - a, cy + a / 2], [cx + a, cy - a / 2]])]


def cell_lines(orig, index, row=None, col=None, sigma=2.0, skeleton=False):
    r, c = (index // 16, index % 16) if row is None else (row, col)
    alpha = orig[r * CH:(r + 1) * CH, c * CW:(c + 1) * CW, 3] > 0
    if index == 0:
        return lone_cross(alpha)
    if not alpha.any():
        return []
    if skeleton:
        return lw.skeleton_traced_lines(alpha, cell_conns(index), sigma=sigma)
    lines, _ = lw.traced_lines(alpha, cell_conns(index), sigma=sigma)
    return lines


# ---------------------------------------------------------------- roads

ROAD_DARK = np.array([92, 78, 58]) / 255
ROAD_MID = np.array([166, 144, 106]) / 255
ROAD_LIGHT = np.array([196, 174, 136]) / 255


def draw_road_cell(lines, seed):
    h, w = CH * R, CW * R
    dist = np.full((h, w), 1e9)
    for L in lines:
        f = lw.StrokeField((h, w), L, R, margin=10 * R)
        y0, y1, x0, x1 = f.box
        dist[y0:y1, x0:x1] = np.minimum(dist[y0:y1, x0:x1], f.dist)
    # Widths in ground units at render resolution (1 ground unit ~ 1 screen px * 0.7-1.4).
    k = R / S  # render px per 2x px
    half = 3.4 * k
    noise = lw.value_noise((h, w), 10 * k, seed)
    fine = lw.value_noise((h, w), 2.5 * k, seed + 1, octaves=2)
    # Soft shoulder: trampled darker earth around the track.
    shoulder = lw.smoothstep(half + 3.0 * k, half - 0.5 * k, dist) * 0.32
    body = lw.smoothstep(half + 0.6 * k, half - 0.6 * k + (noise - 0.5) * 0.8 * k, dist)
    x = np.clip(dist / half, 0, 1)
    crown = 1 - x ** 2  # lighter in the middle
    col = ROAD_MID[None, None] * (0.86 + 0.14 * crown[..., None])
    col = col * (0.92 + 0.16 * fine[..., None])
    # Darker worn edges.
    edge = lw.smoothstep(0.55, 1.0, x)
    col = col * (1 - 0.22 * edge[..., None])
    img = lw.premul((0.24, 0.20, 0.14), shoulder)
    img = lw.over(img, lw.premul(np.clip(col, 0, 1), body))
    return lw.downsample(img, SS)


# ---------------------------------------------------------------- railroads

BALLAST = np.array([148, 144, 136]) / 255
TIE = np.array([94, 82, 70]) / 255
STEEL = np.array([170, 172, 176]) / 255


def draw_rail_cell(lines, seed):
    h, w = CH * R, CW * R
    k = R / S
    fields = [lw.StrokeField((h, w), L, R, margin=12 * R) for L in lines]
    ballast = np.zeros((h, w)); ties = np.zeros((h, w)); tieshade = np.zeros((h, w))
    rail = np.zeros((h, w)); spec = np.zeros((h, w)); railshadow = np.zeros((h, w))
    gauge = 2.6 * k          # half the distance between the rails
    rw = 0.8 * k            # half the rail width
    for f in fields:
        y0, y1, x0, x1 = f.box
        sl = (slice(y0, y1), slice(x0, x1))
        d, p = f.dist, f.perp
        ballast[sl] = np.maximum(ballast[sl], lw.smoothstep(6.0 * k, 4.6 * k, d))
        # Ties: spacing fitted to the line's length so both ends land on a tie.
        sp0 = 3.3 * k
        n = max(1, round(f.length / sp0))
        sp = f.length / n
        ph = (f.s / sp + 0.5) % 1.0 - 0.5  # 0 at a tie's middle
        along = np.abs(ph) * sp
        tie = lw.smoothstep(0.85 * k + 0.5, 0.85 * k - 0.5, along) * lw.smoothstep(4.3 * k, 3.7 * k, np.abs(p))
        cut = lw.smoothstep(0.9 * k + 0.5, 0.9 * k - 0.5, f.past)
        tie = tie * cut
        ties[sl] = np.maximum(ties[sl], tie)
        tieshade[sl] = np.maximum(tieshade[sl], tie * (0.5 + 0.5 * np.clip(ph / 0.3, -1, 1)))
        for side in (-1, 1):
            off = np.abs(p - side * gauge)
            r = lw.smoothstep(rw + 0.5, rw - 0.5, off) * lw.smoothstep(0.5, -0.5, f.past)
            rail[sl] = np.maximum(rail[sl], r)
            spec[sl] = np.maximum(spec[sl], r * lw.smoothstep(rw * 0.75, 0.0, off))
    # Rails cast a small shadow down and to the right.
    dy, dx = int(round(0.6 * k)), int(round(0.9 * k))
    railshadow = np.zeros_like(rail)
    railshadow[dy:, dx:] = rail[:-dy or None, :-dx or None]
    noise = lw.value_noise((h, w), 1.6 * k, seed, octaves=2)
    bcol = BALLAST[None, None] * (0.86 + 0.24 * noise[..., None])
    soft = np.zeros((h, w))
    for f in fields:
        y0, y1, x0, x1 = f.box
        soft[y0:y1, x0:x1] = np.maximum(soft[y0:y1, x0:x1], lw.smoothstep(7.0 * k, 4.2 * k, f.dist))
    img = lw.premul((0.12, 0.11, 0.09), soft * 0.3)
    img = lw.over(img, lw.premul(np.clip(bcol, 0, 1), ballast * 0.95))
    tcol = TIE[None, None] * (0.8 + 0.35 * tieshade[..., None])
    img = lw.over(img, lw.premul(np.clip(tcol, 0, 1), ties))
    img = lw.over(img, lw.premul((0, 0, 0), railshadow * 0.45))
    rcol = STEEL[None, None] * 0.42 + (np.array([0.95, 0.95, 0.96]) - STEEL * 0.42)[None, None] * spec[..., None] ** 1.5
    img = lw.over(img, lw.premul(np.clip(rcol, 0, 1), rail))
    return lw.downsample(img, SS)


# ---------------------------------------------------------------- build

def build_sheet(rel, draw, rows=16, preview_cells=None):
    orig = civ3art.load_original(rel)
    H, W = orig.shape[:2]
    out = np.zeros((H * S, W * S, 4))
    for i in range(rows * 16):
        r, c = divmod(i, 16)
        lines = cell_lines(orig, i, skeleton=draw is draw_rail_cell)
        if not lines:
            continue
        out[r * CH * S:(r + 1) * CH * S, c * CW * S:(c + 1) * CW * S] = draw(lines, 1000 + i)
    rgba = lw.to_rgba8(out)
    civ3art.save_modern(rel, rgba)
    return orig, rgba


def build():
    results = {}
    for rel, draw in (('Art/Terrain/roads.pcx', draw_road_cell), ('Art/Terrain/railroads.pcx', draw_rail_cell)):
        orig, rgba = build_sheet(rel, draw)
        results[rel] = (orig, rgba)
        name = os.path.basename(rel)[:-4]
        civ3art.save_preview(name + '_tl', civ3art.upscale_nearest(orig[:256, :512]), rgba[:512, :1024])
    return results


if __name__ == '__main__':
    build()
