"""Irrigation: Art/Terrain/irrigation.pcx and its desert, plains and tundra versions.

Each is a 4x4 grid of 128x64 tiles (the index is the set of diagonal neighbors
with irrigation); all four share one layout and differ only in color. The
original's patches of irrigated field become smooth-edged fields with crop strips,
and its water channels, which run along the two iso axes, are found and redrawn as
straight, neat channels at the same positions, so that they line up with the
neighbors' as before.
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

# Per variant: field color, the crop strips' second color, the field edge.
VARIANTS = {
    'Art/Terrain/irrigation.pcx': dict(field=(142, 150, 86), strip=(116, 134, 70), edge=(94, 100, 60)),
    'Art/Terrain/irrigation DESETT.pcx': dict(field=(206, 192, 146), strip=(164, 166, 112), edge=(162, 140, 100)),
    'Art/Terrain/irrigation PLAINS.pcx': dict(field=(196, 178, 116), strip=(154, 154, 94), edge=(142, 124, 80)),
    'Art/Terrain/irrigation TUNDRA.pcx': dict(field=(204, 208, 196), strip=(170, 182, 162), edge=(146, 152, 138)),
}
WATER = np.array([120, 156, 162]) / 255
WATER_DEEP = np.array([80, 118, 136]) / 255
WATER_SHINE = np.array([186, 202, 206]) / 255
DIKE = np.array([104, 98, 74]) / 255


def ground(x, y):
    """Screen (1x) to ground coordinates: channels run along u or along v."""
    return 0.5 * x + y, -0.5 * x + y


def find_channels(chan):
    """Straight channel segments in a cell's channel mask: a list of
    (axis, c, a0, a1): along `axis` ('u' runs with constant v, or 'v' runs with
    constant u) at ground coordinate c, from a0 to a1."""
    h, w = chan.shape
    ys, xs = np.nonzero(chan)
    u, v = ground(xs + 0.5, ys + 0.5)
    segs = []
    for axis, along, across in (('u', u, v), ('v', v, u)):
        # Bin across-coordinate at 1 ground unit; each pixel spans ~1.1 units.
        lo = np.floor(across.min()) - 1
        bins = np.floor(across - lo).astype(int)
        nb = bins.max() + 2
        rows = [[] for _ in range(nb)]
        for b, a in zip(bins, along):
            rows[b].append(a)
        # Runs per row (gaps up to 2.5 units bridged).
        runs = []
        for b in range(nb):
            if not rows[b]:
                continue
            a = np.sort(rows[b])
            start = a[0]
            for i in range(1, len(a) + 1):
                if i == len(a) or a[i] - a[i - 1] > 2.5:
                    if a[i - 1] - start >= 5:
                        runs.append((b, start, a[i - 1]))
                    if i < len(a):
                        start = a[i]
        # Merge runs in adjacent rows that overlap into one channel.
        runs.sort()
        used = [False] * len(runs)
        for i, (b, a0, a1) in enumerate(runs):
            if used[i]:
                continue
            group = [(b, a0, a1)]
            used[i] = True
            changed = True
            while changed:
                changed = False
                for j, (b2, c0, c1) in enumerate(runs):
                    if used[j]:
                        continue
                    if any(abs(b2 - g[0]) <= 1 and c0 < g[2] - 2 and c1 > g[1] + 2 for g in group):
                        group.append((b2, c0, c1)); used[j] = True; changed = True
            bs = np.array([g[0] for g in group], float)
            wts = np.array([g[2] - g[1] for g in group])
            c = lo + np.average(bs, weights=wts) + 0.5
            a0 = min(g[1] for g in group); a1 = max(g[2] for g in group)
            if a1 - a0 >= 6 and len(group) <= 4:
                segs.append((axis, c, a0 - 0.6, a1 + 0.6))
    return segs


def channel_line(seg):
    axis, c, a0, a1 = seg
    # Back to screen (1x): x = u - v, y = (u + v) / 2.
    if axis == 'u':
        u = np.array([a0, a1]); v = np.array([c, c])
    else:
        v = np.array([a0, a1]); u = np.array([c, c])
    return np.stack([u - v, (u + v) / 2], 1)


def field_mask(alpha):
    """The original's field patches with smooth edges (render resolution), kept
    straight along the tile's edges where the original reaches them."""
    h, w = CH * R, CW * R
    up = np.kron(alpha.astype(float), np.ones((R, R)))
    dia = lw.diamond_mask(w, h)
    inside = (dia > 0.5).astype(float)
    sig = 1.3 * R
    num = ndimage.gaussian_filter(up * inside, sig)
    den = ndimage.gaussian_filter(inside, sig)
    sm = num / np.maximum(den, 1e-6)
    m = lw.smoothstep(0.42, 0.58, sm)
    return m * dia


def draw_cell(alpha, chan, pal, seed):
    h, w = CH * R, CW * R
    field = field_mask(alpha)
    yy, xx = np.mgrid[0:h, 0:w].astype(float) + 0.5
    u, v = ground(xx / R, yy / R)  # 1x ground units
    noise = lw.value_noise((h, w), 7 * R, seed)
    # Crop strips: rows along u, about 3 ground units apart, with plots varying in tone.
    strips = 0.5 + 0.5 * np.sin(2 * np.pi * v / 3.2)
    strips = lw.smoothstep(0.35, 0.65, strips)
    plots = lw.value_noise((h, w), 14 * R, seed + 3, octaves=1)
    fc = np.array(pal['field']) / 255
    sc = np.array(pal['strip']) / 255
    col = fc[None, None] * (1 - strips[..., None] * 0.45) + sc[None, None] * strips[..., None] * 0.45
    col = col * (0.9 + 0.2 * plots[..., None]) * (0.96 + 0.08 * noise[..., None])
    # A darker rim where the field meets the land around it.
    inner = ndimage.gaussian_filter(field, 0.9 * R)
    rim = np.clip((field - inner) * 3.0, 0, 1) * (lw.diamond_mask(w, h) > 0.999)
    ec = np.array(pal['edge']) / 255
    col = col * (1 - rim[..., None]) + ec[None, None] * rim[..., None]
    img = lw.premul(np.clip(col, 0, 1), field * 0.94)

    # Channels.
    segs = find_channels(chan)
    dist = np.full((h, w), 1e9)
    perp_sign = np.zeros((h, w))
    for sgm in segs:
        L = channel_line(sgm)
        f = lw.StrokeField((h, w), L, R, margin=6 * R)
        y0, y1, x0, x1 = f.box
        m = f.dist < dist[y0:y1, x0:x1]
        dist[y0:y1, x0:x1] = np.where(m, f.dist, dist[y0:y1, x0:x1])
    k = R / S
    half = 1.9 * k
    dike = lw.smoothstep(half + 1.6 * k, half + 0.4 * k, dist)
    water = lw.smoothstep(half + 0.5, half - 0.5, dist)
    x = np.clip(dist / half, 0, 1)
    wc = WATER_DEEP[None, None] * (1 - x[..., None] ** 2) + WATER[None, None] * x[..., None] ** 2
    # Shine on the water's upper edge, shade on the lower (light from above).
    gy = np.gradient(np.minimum(dist, 10 * k), axis=0)
    shine = lw.smoothstep(0.3, 0.9, x) * np.clip(gy, 0, 1) * 0.6
    wc = wc + (WATER_SHINE - wc) * shine[..., None]
    img = lw.over(img, lw.premul(DIKE, dike * 0.4))
    img = lw.over(img, lw.premul(np.clip(wc, 0, 1), water))
    return lw.downsample(img, SS), segs


def build_sheet(rel, pal):
    orig = civ3art.load_original(rel)
    rgb = orig[..., :3].astype(int)
    out = np.zeros((orig.shape[0] * S, orig.shape[1] * S, 4))
    all_segs = {}
    for i in range(16):
        r, c = divmod(i, 4)
        sl = (slice(r * CH, (r + 1) * CH), slice(c * CW, (c + 1) * CW))
        alpha = orig[sl][..., 3] > 0
        chan = (rgb[sl][..., 2] > rgb[sl][..., 0] + 40) & alpha
        cell, segs = draw_cell(alpha, chan, pal, 4000 + i)
        all_segs[i] = segs
        out[r * CH * S:(r + 1) * CH * S, c * CW * S:(c + 1) * CW * S] = cell
    rgba = lw.to_rgba8(out)
    civ3art.save_modern(rel, rgba)
    civ3art.save_preview(os.path.basename(rel)[:-4].replace(' ', '_'), civ3art.upscale_nearest(orig), rgba)
    return rgba


def build():
    for rel, pal in VARIANTS.items():
        build_sheet(rel, pal)


if __name__ == '__main__':
    build()
