"""Rivers: Art/Terrain/deltaRivers.pcx and Art/Terrain/mtnRivers.pcx.

Each is a 4x4 grid of 128x64 cells centered on a point where four tiles meet.
A cell's index is the set of tile edges at that point with a river (bit 0 up-left,
1 up-right, 2 down-left, 3 down-right); each river arm runs from near the point to
the middle of its edge, where the next cell's arm continues. deltaRivers cells
1, 2, 4 and 8 are river mouths fanning out into the sea.

The paths are traced from the originals and drawn anew as clean water with soft
banks: a deeper middle, shallow edges, the bank on the far side of the light in
shade, and a few soft highlights.
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
K = R / S  # render pixels per 2x pixel

# Connection points by bit, and the direction the river continues there.
POINTS = [((32, 16), (-2, -1)), ((96, 16), (2, -1)), ((32, 48), (-2, 1)), ((96, 48), (2, 1))]

# Muted, natural water (the original's river blues are grey-blue, ~(99,140,160)).
DEEP = np.array([76, 112, 134]) / 255
SHALLOW = np.array([116, 148, 158]) / 255
SHINE = np.array([186, 202, 206]) / 255
BANK_LIT = np.array([116, 120, 88]) / 255
BANK_DARK = np.array([70, 78, 58]) / 255
MOUTH = np.array([140, 168, 168]) / 255

LIGHT = np.array([-1.0, -1.0]) / np.sqrt(2)  # toward the light, screen (x, y)


def conns(index):
    return [POINTS[b] for b in range(4) if index >> b & 1]


def river_lines(alpha, index):
    cs = conns(index)
    lines, _ = lw.traced_lines(alpha, cs, sigma=1.6, straight=7.0, center_weight=2.0)
    return lines


def delta_lines(alpha, index):
    """A river mouth: a branching tree from the river's entry point out to the tips
    of the original's fan of channels."""
    p, d = POINTS[[1, 2, 4, 8].index(index)]
    mask = ndimage.binary_closing(np.pad(alpha, 3), iterations=1)[3:-3, 3:-3] | alpha
    skel = lw.thin(mask)
    nb = ndimage.convolve(skel.astype(int), np.ones((3, 3), int), mode='constant') - 1
    ys, xs = np.nonzero(skel & (nb == 1))
    tips = [(x, y) for x, y in zip(xs, ys) if np.hypot(x - p[0], (y - p[1]) * 2) > 14]
    # Keep tips that are well apart.
    tips.sort(key=lambda t: -np.hypot(t[0] - p[0], (t[1] - p[1]) * 2))
    kept = []
    for t in tips:
        if all(np.hypot(t[0] - k[0], t[1] - k[1]) > 6 for k in kept):
            kept.append(t)
    lines, _ = lw.trace_cell(alpha, [p] + kept, hub_at=p, center_weight=1.0)
    out = []
    for L in lines:
        L = lw.smooth(L + 0.5, 1.4)
        if np.hypot(*(L[0] - p)) < 6:
            L = lw.straighten_end(L, p, d, 6.0)
        out.append(L)
    return out


def render_water(lines, seed, half_width, entry=None, taper_to=None, taper_len=30.0, mouth=False):
    """Draws river water along the lines. half_width is in render pixels (ground
    units); with entry/taper_to, the width shrinks with distance from `entry`."""
    h, w = CH * R, CW * R
    dist = np.full((h, w), 1e9)
    for L in lines:
        f = lw.StrokeField((h, w), L, R, margin=int(8 * K + 2.5 * np.max(half_width)))
        y0, y1, x0, x1 = f.box
        dist[y0:y1, x0:x1] = np.minimum(dist[y0:y1, x0:x1], f.dist)
    yy, xx = np.mgrid[0:h, 0:w].astype(float) + 0.5
    if entry is not None:
        de = np.hypot(xx / R - entry[0], (yy / R - entry[1]) * 2) / taper_len
        t = np.clip(de, 0, 1)
        hw = half_width * (1 - t) + taper_to * t
    else:
        hw = np.full((h, w), half_width)
    noise = lw.value_noise((h, w), 6 * K, seed)
    hw = hw * (0.92 + 0.16 * noise)
    bank = 1.8 * K
    # Direction from the river's middle out to this pixel (screen), to tell the shaded bank.
    gy, gx = np.gradient(np.minimum(dist, 40 * K))
    gn = np.maximum(np.hypot(gx, gy), 1e-6)
    facing = (gx * LIGHT[0] + gy * LIGHT[1]) / gn  # > 0: on the light's side of the river
    water = lw.smoothstep(hw + 0.6 * K, hw - 0.6 * K, dist)
    banks = lw.smoothstep(hw + bank + 0.9 * K, hw + bank - 0.6 * K, dist)
    x = np.clip(dist / np.maximum(hw, 1e-6), 0, 1)
    # The bank: the one on the light's side faces away from it, in shade.
    shade = np.clip(0.5 + 0.5 * facing, 0, 1)
    bcol = BANK_LIT[None, None] * (1 - shade[..., None]) + BANK_DARK[None, None] * shade[..., None]
    # Water: deeper in the middle, shallow at the edges.
    depth = (1 - x ** 2)
    wcol = SHALLOW[None, None] * (1 - depth[..., None]) + DEEP[None, None] * depth[..., None]
    if mouth:
        wcol = wcol * 0.4 + MOUTH[None, None] * 0.6
    # Shade under the bank on the light's side; a soft sheen on the other side.
    under = lw.smoothstep(0.35, 0.95, x) * np.clip(facing, 0, 1)
    wcol = wcol * (1 - 0.28 * under[..., None])
    flow = lw.value_noise((h, w), 3 * K, seed + 7, octaves=2)
    sheen = lw.smoothstep(0.25, 0.85, x) * np.clip(-facing, 0, 1) * lw.smoothstep(0.55, 0.8, flow)
    glint = lw.smoothstep(0.7, 0.9, flow) * (1 - x) * 0.6
    hl = np.clip(sheen * 0.75 + glint * 0.5, 0, 1)
    wcol = wcol + (SHINE[None, None] - wcol) * hl[..., None]
    if mouth and entry is not None:
        # The fan fades into the sea: light silty channels, banks only near the river.
        fade = 1 - 0.55 * lw.smoothstep(0.15, 1.0, t)
        img = lw.premul(np.clip(bcol, 0, 1), banks * 0.9 * (1 - lw.smoothstep(0.0, 0.5, t)))
        img = lw.over(img, lw.premul(np.clip(wcol, 0, 1), water * fade))
    else:
        img = lw.premul(np.clip(bcol, 0, 1), banks * 0.92)
        img = lw.over(img, lw.premul(np.clip(wcol, 0, 1), water))
    return lw.downsample(img, SS)


def pond(alpha, seed):
    """Cell 0: a river that only touches the point - a small pool where the original's is."""
    ys, xs = np.nonzero(alpha)
    cx, cy = (xs.mean() + 0.5, ys.mean() + 0.5) if len(xs) else (64.0, 32.0)
    rx = max(5.0, (xs.max() - xs.min() + 1) * 0.5) if len(xs) else 8.0
    L1 = np.array([[cx - rx * 0.5, cy - rx * 0.05], [cx - rx * 0.1, cy + rx * 0.12], [cx + rx * 0.35, cy + rx * 0.02]])
    L2 = np.array([[cx - rx * 0.05, cy - rx * 0.1], [cx + rx * 0.45, cy + rx * 0.18]])
    return render_water([lw.smooth(L1, 2.0), L2], seed, half_width=rx * 0.42 * K)


def build_sheet(rel, deltas):
    orig = civ3art.load_original(rel)
    out = np.zeros((orig.shape[0] * S, orig.shape[1] * S, 4))
    for i in range(16):
        r, c = divmod(i, 4)
        alpha = orig[r * CH:(r + 1) * CH, c * CW:(c + 1) * CW, 3] > 0
        if not alpha.any():
            continue
        seed = (2000 if deltas else 3000) + i
        if i == 0:
            cell = pond(alpha, seed)
        elif deltas and i in (1, 2, 4, 8):
            p = POINTS[[1, 2, 4, 8].index(i)][0]
            lines = delta_lines(alpha, i)
            cell = render_water(lines, seed, half_width=3.6 * K, entry=p, taper_to=1.0 * K, taper_len=30.0, mouth=True)
        else:
            cell = render_water(river_lines(alpha, i), seed, half_width=3.6 * K)
        out[r * CH * S:(r + 1) * CH * S, c * CW * S:(c + 1) * CW * S] = cell
    rgba = lw.to_rgba8(out)
    civ3art.save_modern(rel, rgba)
    civ3art.save_preview(os.path.basename(rel)[:-4], civ3art.upscale_nearest(orig), rgba)
    return orig, rgba


def build():
    build_sheet('Art/Terrain/deltaRivers.pcx', True)
    build_sheet('Art/Terrain/mtnRivers.pcx', False)


if __name__ == '__main__':
    build()
