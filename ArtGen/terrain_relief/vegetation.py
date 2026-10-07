"""Forests, jungles, marsh and flood plains.

Each cell of an original sheet is redrawn from scratch: the original's alpha
(its footprint) and colors only say where vegetation goes and what color it is.
"""
import numpy as np
import scipy.ndimage as ndi

import civ3art
import trees as T
from render import (SS, srgb, saturate, lerp, smoothstep, upsample, fbm, premul, over,
                    downsample, coverage, value_noise, shift)


# ---------------------------------------------------------------- palettes

def muted(c, chroma):
    """Scales a color's chroma around its luminance (below 1 greys it)."""
    c = np.asarray(c, float)
    lum = c @ np.array([0.3, 0.59, 0.11])
    return np.clip(lum + (c - lum) * chroma, 0, 1)


def leaf_palette(base, sat=1.18, warm=0.0):
    leaf = saturate(base, sat)
    dark = np.clip(leaf * np.array([0.30, 0.36, 0.40]), 0, 1)
    light = np.clip(leaf * 1.45 + np.array([0.10, 0.10, 0.02]) + warm * np.array([0.06, 0.03, -0.03]), 0, 1)
    return leaf, dark, light


HUES = np.array([[1.0, 1.0, 1.0], [0.78, 0.92, 1.05], [1.12, 1.06, 0.8], [0.9, 0.97, 0.9]])


def vary(r, c):
    """A tree's own shade of its palette color."""
    return np.clip(c * HUES[r.integers(len(HUES))] * r.uniform(0.86, 1.1), 0, 1)


def make_libraries(seed, broad=None, pine=None, jungle=None, scale=1.0, n=24):
    """Sprite libraries for the tree kinds whose colors are given."""
    libs = {}
    if broad is not None:
        leaf, dark, light = leaf_palette(broad)
        trunk = srgb((112, 70, 30))
        libs['broad'] = T.Library(lambda r: T.broadleaf(r, 16.5 * scale * r.uniform(0.78, 1.2), vary(r, leaf), dark, light, trunk), n, seed + 1)
    if pine is not None:
        leaf, dark, light = leaf_palette(pine, sat=1.1)
        light = np.clip(lerp(light, srgb((190, 190, 90)), 0.35), 0, 1)
        trunk = srgb((90, 60, 35))
        libs['pine'] = T.Library(lambda r: T.pine(r, 12.5 * scale * r.uniform(0.85, 1.1), 42 * scale * r.uniform(0.85, 1.15), vary(r, leaf), dark, light, trunk), n, seed + 2)
    if jungle is not None:
        leaf, dark, light = leaf_palette(jungle, sat=1.25)
        light = np.clip(light + np.array([0.05, 0.05, 0]), 0, 1)
        trunk = srgb((160, 95, 35))
        libs['palm'] = T.Library(lambda r: T.palm(r, 16 * scale * r.uniform(0.85, 1.15), vary(r, leaf), dark, light, trunk), n, seed + 3)
        bush = np.clip(leaf * np.array([0.72, 0.8, 0.6]), 0, 1)
        libs['bush'] = T.Library(lambda r: T.broadleaf(r, 11 * scale * r.uniform(0.85, 1.2), vary(r, bush), dark, light, trunk * 1.2), n, seed + 4)
    return libs


# ---------------------------------------------------------------- planting

def plant(canvas, shadow, dens, kind_at, libs, rng, scale=1.0, thresholds=(0.45, 0.45, 0.2),
          spacing=1.0):
    """Plants trees where `dens` (render-size, 0..1) is high, back to front.
    kind_at(x, y) gives 'broad', 'pine', 'palm' or 'bush' for a base point."""
    h, w = dens.shape
    R = 15 * scale

    def d(x, y):
        xi, yi = int(np.clip(x, 0, w - 1)), int(np.clip(y, 0, h - 1))
        return dens[yi, xi]

    def accept(x, y):
        k = kind_at(x, y)
        tall = 2.9 * R if k == 'pine' else (2.3 * R if k == 'palm' else 2.2 * R)
        return (d(x, y - 0.25 * R) > thresholds[0] and d(x, y - 0.5 * tall) > thresholds[1]
                and d(x, y - 0.85 * tall) > thresholds[2])

    pts = T.poisson_points(rng, accept, h + int(R), w, R * 1.45 * spacing, R * 0.8 * spacing)
    pts.sort(key=lambda p: p[1])
    placed = []
    for x, y in pts:
        k = kind_at(x, y)
        spr = libs[k].pick(rng)
        T.draw_shadow(shadow, spr, x, y, 0.85)
        placed.append((x, y, spr, rng.uniform(0.9, 1.08)))
    for x, y, spr, tint in placed:
        T.draw_sprite(canvas, spr, x, y, tint)
    return placed


def scatter_tufts(canvas, lib, mask_fn, rng, h, w, spacing):
    pts = T.poisson_points(rng, mask_fn, h, w, spacing, spacing * 0.6, jitter=0.5)
    pts.sort(key=lambda p: p[1])
    for x, y in pts:
        T.draw_sprite(canvas, lib.pick(rng), x, y, rng.uniform(0.85, 1.1))


# ---------------------------------------------------------------- forest sheets

def pine_mask(rgba):
    r, g, b = [rgba[..., i].astype(float) for i in range(3)]
    return (b > 0.48 * g) & (g < 130) & (b > 30) & (rgba[..., 3] > 0)


def fringe_color(cell):
    """The color of the sparse ground dots around a clump (the original's 'skirt')."""
    a = cell[..., 3] > 0
    dens = ndi.gaussian_filter(a.astype(float), 1.5)
    m = a & (dens < 0.55)
    return civ3art_median(cell, m, (170, 170, 70))


def civ3art_median(cell, m, default):
    sel = cell[..., :3][m]
    if len(sel) < 10:
        return srgb(default)
    lum = sel.astype(float) @ np.array([0.3, 0.59, 0.11])
    # the light dots, not the shadowed ones
    sel = sel[lum >= np.median(lum)]
    return srgb(np.median(sel, 0))


def render_ground(cell, fringe, rng, dark_floor, seed, tuft_lib, wet=False):
    """The forest floor: a soft dark patch under the canopy with a skirt of grass tufts."""
    a = cell[..., 3] > 0
    h, w = a.shape[0] * SS, a.shape[1] * SS
    dens = ndi.gaussian_filter(upsample(ndi.gaussian_filter(a.astype(float), 1.3)), SS * 0.6)
    n = fbm((h, w), 10, seed, 3)
    canvas = np.zeros((h, w, 4))
    floor_a = smoothstep(0.25, 0.75, dens + (n - 0.5) * 0.25) * 0.55
    col = lerp(fringe * 0.8, dark_floor, smoothstep(0.4, 0.9, dens))
    over(canvas, premul(col, floor_a))
    edge = (dens > 0.12) & (dens < 0.6)
    scatter_tufts(canvas, tuft_lib, lambda x, y: edge[int(y), int(x)] and n[int(y), int(x)] > 0.35,
                  rng, h, w, 7)
    return canvas, dens


def forest_sheet(rel, seed, preview=None):
    orig = civ3art.load_original(rel)
    H, W = orig.shape[:2]
    out = np.zeros((H * 2, W * 2, 4), np.uint8)
    cw, ch = 128, 88
    for row in range(H // ch):
        for col in range((W + cw - 1) // cw):
            x0, y0 = col * cw, row * ch
            cell = orig[y0:y0 + ch, x0:x0 + cw]
            if not cell[..., 3].any():
                continue
            kind = 'jungle' if row < 4 else ('forest' if row < 8 else 'pine')
            rgba = forest_cell(cell, kind, seed * 1000 + row * 16 + col, rel)
            out[y0 * 2:(y0 + cell.shape[0]) * 2, x0 * 2:(x0 + cell.shape[1]) * 2] = rgba
    return orig, out


_lib_cache = {}


def forest_cell(cell, kind, seed, key):
    rng = np.random.default_rng(seed)
    a = cell[..., 3] > 0
    pm = pine_mask(cell)
    fr = fringe_color(cell)
    snowy = fr.mean() > 0.7 and fr.std() < 0.08
    # Colors of the canopy (the original's non-pine, non-skirt pixels).
    densc = ndi.gaussian_filter(a.astype(float), 1.5)
    core = a & (densc > 0.6)
    broad_c = civ3art_median_all(cell, core & ~pm, (105, 120, 30))
    pine_c = civ3art_median_all(cell, pm, (60, 85, 60)) if pm.sum() > 20 else srgb((62, 88, 62))
    ck = (key, kind)
    if ck not in _lib_cache:
        if kind == 'jungle':
            libs = make_libraries(seed, jungle=broad_c * np.array([1.0, 1.08, 1.0]) + np.array([0.02, 0.04, 0.0]))
        else:
            libs = make_libraries(seed, broad=broad_c * np.array([0.72, 0.86, 0.85]),
                                  pine=np.clip(pine_c * np.array([0.95, 1.0, 0.82]), 0, 1))
        tcol = fr if not snowy else srgb((236, 240, 244))
        libs['tuft'] = T.Library(lambda r: T.tuft(r, 9, tcol, tcol * 0.55, np.clip(tcol * 1.25, 0, 1)), 10, seed + 9)
        _lib_cache[ck] = libs
    libs = _lib_cache[ck]
    dark_floor = srgb((40, 48, 20)) if not snowy else srgb((205, 214, 224))
    canvas, dens = render_ground(cell, fr if not snowy else srgb((236, 240, 244)), rng, dark_floor, seed, libs['tuft'])
    h, w = dens.shape
    shadow = np.zeros((h, w))
    # Where the canopy is: dense parts of the original, ignoring the sparse skirt.
    canopy = ndi.gaussian_filter(upsample(ndi.gaussian_filter(a.astype(float), 1.0)), SS * 0.5)
    pfrac = ndi.gaussian_filter(upsample(ndi.gaussian_filter(pm.astype(float), 2.0)), SS)
    afrac = ndi.gaussian_filter(upsample(ndi.gaussian_filter(a.astype(float), 2.0)), SS)
    pratio = pfrac / np.maximum(afrac, 1e-3)
    jn = value_noise((h, w), 14, seed + 5)

    def kind_at(x, y):
        xi, yi = int(np.clip(x, 0, w - 1)), int(np.clip(y - 20, 0, h - 1))
        if kind == 'pine':
            return 'pine'
        if kind == 'jungle':
            return 'palm' if jn[yi, xi] > 0.42 else 'bush'
        return 'pine' if pratio[yi, xi] > 0.22 else 'broad'

    # A few small clearings break up the canopy.
    canopy = canopy * (1 - 0.35 * smoothstep(0.62, 0.8, value_noise((h, w), 26, seed + 6)))
    trees = np.zeros((h, w, 4))
    if kind == 'pine':
        plant(trees, shadow, canopy, kind_at, libs, rng, thresholds=(0.5, 0.45, 0.3), spacing=1.1)
    else:
        plant(trees, shadow, canopy, kind_at, libs, rng, thresholds=(0.55, 0.55, 0.35), spacing=0.9)
    sh = ndi.gaussian_filter(shadow, 1.5) * 0.42
    over(canvas, premul(np.zeros((h, w, 3)), sh))
    over(canvas, trees)
    return downsample(canvas)


def civ3art_median_all(cell, m, default):
    sel = cell[..., :3][m & (cell[..., 3] > 0)]
    if len(sel) < 10:
        return srgb(default)
    return srgb(np.median(sel, 0))


# ---------------------------------------------------------------- marsh

def water_mask(cell):
    r, g, b = [cell[..., i].astype(float) for i in range(3)]
    return (g - r > 35) & (b > r - 5) & (g > 120) & (cell[..., 3] > 0)


def marsh_sheet(rel, seed):
    orig = civ3art.load_original(rel)
    H, W = orig.shape[:2]
    out = np.zeros((H * 2, W * 2, 4), np.uint8)
    cw, ch = 128, 88
    for row in range(H // ch):
        for col in range((W + cw - 1) // cw):
            x0, y0 = col * cw, row * ch
            cell = orig[y0:y0 + ch, x0:x0 + cw]
            if not cell[..., 3].any():
                continue
            rgba = marsh_cell(cell, seed * 1000 + row * 16 + col, trees_ok=row >= 8)
            out[y0 * 2:(y0 + cell.shape[0]) * 2, x0 * 2:(x0 + cell.shape[1]) * 2] = rgba
    return orig, out


def marsh_cell(cell, seed, trees_ok=True):
    rng = np.random.default_rng(seed)
    a = cell[..., 3] > 0
    # The original's stray lines (a palette artifact) are thin; drop isolated dark pixels.
    wm = water_mask(cell)
    pm = pine_mask(cell) & ~wm
    has_trees = trees_ok and pm.sum() > 0.06 * a.sum()
    h, w = a.shape[0] * SS, a.shape[1] * SS
    dens = ndi.gaussian_filter(upsample(ndi.gaussian_filter(a.astype(float), 1.4)), SS * 0.6)
    reed_c = muted(civ3art_median(cell, a & ~wm & ~pm, (190, 190, 120)), 0.8)
    mud_c = muted(civ3art_median_all(cell, a & ~wm & ~pm, (120, 120, 60)), 0.75) * np.array([0.64, 0.68, 0.62])
    canvas = np.zeros((h, w, 4))
    n = fbm((h, w), 12, seed, 3)
    # wet ground
    ga = smoothstep(0.3, 0.75, dens + (n - 0.5) * 0.3) * 0.72
    gcol = lerp(mud_c * 0.8, mud_c * 1.15, n)
    over(canvas, premul(gcol, ga))
    # pools of open water
    wsm = ndi.gaussian_filter(upsample(ndi.gaussian_filter(wm.astype(float), 0.8)), SS * 0.5)
    wf = wsm + (fbm((h, w), 6, seed + 1, 2) - 0.5) * 0.12
    pool = smoothstep(0.2, 0.28, wf) * (dens > 0.3)
    yy = np.mgrid[0:h, 0:w][0].astype(float)
    sky = lerp(srgb((88, 140, 138)), srgb((160, 200, 192)), (0.5 + 0.5 * np.sin(yy * 0.09 + n * 4))[..., None] * 0.6)
    # dark bank on the far (upper) side of each pool, a bright lip on the near side
    gy = np.gradient(ndi.gaussian_filter(pool, 2.0), axis=0)
    sky = sky * (1 - np.clip(gy * 6, 0, 0.5))[..., None] + np.clip(-gy * 5, 0, 0.35)[..., None]
    shine = smoothstep(0.65, 0.9, fbm((h, w), 9, seed + 2, 2, aniso=(0.35, 1.0))) * 0.25
    over(canvas, premul(np.clip(sky + shine[..., None], 0, 1), pool * 0.92))
    # reeds
    libs = [T.Library(lambda r: T.tuft(r, r.uniform(10, 17), vary(r, reed_c), np.clip(reed_c * 0.45, 0, 1),
                                       np.clip(reed_c * 1.25 + 0.05, 0, 1), blades=r.integers(5, 9), spread=0.38,
                                       lean=r.normal(0, 0.1)), 16, seed + 3),
            T.Library(lambda r: T.tuft(r, r.uniform(8, 13), vary(r, np.clip(reed_c * np.array([0.7, 0.85, 0.55]), 0, 1)),
                                       np.clip(reed_c * 0.35, 0, 1), np.clip(reed_c * 1.1, 0, 1),
                                       blades=r.integers(4, 7), spread=0.5), 12, seed + 4)]
    pts = T.poisson_points(rng, lambda x, y: dens[int(y), int(x)] > 0.45 and pool[int(y), int(x)] < 0.3,
                           h, w, 9.5, 5.6, jitter=0.5)
    pts.sort(key=lambda p: p[1])
    shadow = np.zeros((h, w))
    reeds = np.zeros((h, w, 4))
    for x, y in pts:
        lib = libs[0] if rng.random() < 0.7 else libs[1]
        spr = lib.pick(rng)
        T.draw_sprite(reeds, spr, x, y, rng.uniform(0.85, 1.12))
    if has_trees:
        tl = make_libraries(seed, pine=np.clip(civ3art_median_all(cell, pm, (60, 85, 60)) * np.array([0.95, 1.0, 0.82]), 0, 1))
        pd = ndi.gaussian_filter(upsample(ndi.gaussian_filter(pm.astype(float), 1.5)), SS * 0.5)
        pd = pd / max(pd.max(), 1e-3)
        plant(reeds, shadow, pd, lambda x, y: 'pine', tl, rng, thresholds=(0.3, 0.25, 0.1), spacing=1.1)
    # reed shadows: a soft dark band under the reed mass
    rs = ndi.gaussian_filter(shift(reeds[..., 3], 3, 3), 2.5) * 0.3
    over(canvas, premul(np.zeros((h, w, 3)), np.clip(rs + ndi.gaussian_filter(shadow, 1.5) * 0.4, 0, 0.5)))
    over(canvas, reeds)
    return downsample(canvas)


# ---------------------------------------------------------------- flood plains

def floodplain_sheet(rel, seed):
    orig = civ3art.load_original(rel)
    H, W = orig.shape[:2]
    out = np.zeros((H * 2, W * 2, 4), np.uint8)
    cw, ch = 128, 64
    lush = civ3art_median_all(orig, (orig[..., 1].astype(int) > orig[..., 0].astype(int) + 8), (110, 140, 30))
    edge = civ3art_median(orig, orig[..., 3] > 0, (200, 190, 120))
    for row in range(H // ch):
        for col in range(W // cw):
            x0, y0 = col * cw, row * ch
            cell = orig[y0:y0 + ch, x0:x0 + cw]
            if not cell[..., 3].any():
                continue
            out[y0 * 2:(y0 + ch) * 2, x0 * 2:(x0 + cw) * 2] = floodplain_cell(cell, seed * 100 + row * 4 + col, lush, edge)
    return orig, out


def floodplain_cell(cell, seed, lush, edge_c):
    rng = np.random.default_rng(seed)
    a = cell[..., 3] > 0
    h, w = a.shape[0] * SS, a.shape[1] * SS
    dens = ndi.gaussian_filter(upsample(ndi.gaussian_filter(a.astype(float), 1.2)), SS * 0.5)
    n = fbm((h, w), 9, seed, 3)
    n2 = fbm((h, w), 22, seed + 1, 3)
    f = dens + (n - 0.5) * 0.35
    alpha = smoothstep(0.2, 0.36, f) * 0.95
    densb = ndi.gaussian_filter(upsample(ndi.gaussian_filter(a.astype(float), 3.0)), SS * 0.5)
    core = smoothstep(0.5, 0.85, densb + (n2 - 0.5) * 0.45 + (n - 0.5) * 0.2)
    # Muted: well under the original's saturation, like the grassland around it.
    green = muted(lush * np.array([0.95, 0.95, 1.0]), 0.62) * 0.92
    sand = muted(lerp(edge_c, srgb((222, 204, 150)), 0.45), 0.7)
    col = lerp(sand, green * (0.9 + 0.2 * n2)[..., None], core)
    # moist dark hollows and drier sunny patches, soft and low in contrast
    mud = muted(lerp(green, srgb((120, 100, 60)), 0.5), 0.8)
    col = lerp(col, mud, (smoothstep(0.55, 0.75, n2) * core * 0.6)[..., None])
    col = lerp(col, lerp(green, sand, 0.45), (smoothstep(0.6, 0.78, n) * core * 0.5)[..., None])
    canvas = np.zeros((h, w, 4))
    over(canvas, premul(col, alpha))
    lib = T.Library(lambda r: T.tuft(r, r.uniform(6, 9), vary(r, np.clip(green * 1.05, 0, 1)), np.clip(green * 0.55, 0, 1),
                                     np.clip(green * 1.22, 0, 1), blades=r.integers(3, 6)), 12, seed + 2)
    scatter_tufts(canvas, lib, lambda x, y: core[int(y), int(x)] > 0.6 and n[int(y), int(x)] > 0.45, rng, h, w, 9)
    return downsample(canvas)
