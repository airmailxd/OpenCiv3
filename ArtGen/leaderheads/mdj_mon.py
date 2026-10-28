"""The leader heads of MDJ's clans (C7/Lua/mdj/ruleset.json) for standalone
mode: each clan's banner, a mon (family crest) on dark cloth, as MDJ's own
leader heads are (the Sengoku conquest's Art/Advisors/<clan>_all.pcx, one
picture repeated for every era and mood). Unlike the other civilizations'
heads (lifelike.py, build.py) these are remakes of the originals, like the
rest of ArtGen: the same banner, its mon and its folds, at twice the pixels
and without the cloth's speckle.

    python ArtGen/leaderheads/mdj_mon.py            # every clan
    python ArtGen/leaderheads/mdj_mon.py RACE_ODA   # just these

Writes C7/ModernArt/Art/Leaderheads/<clan>.png (230x230, twice Civ3's
115x115), named by the clan's Civilopedia entry (RACE_ODA: Oda.png), as MDJ's
civilization names have slashes in them ("Oda (Mil/Ind)"); the standalone
textures (C7/Lua/standalone/textures.lua) find them by that entry. A contact
sheet, originals above remakes, goes to ArtGen/_previews/leaderheads_mdj.png.

How a banner is remade: its pixels are sorted into a few flat colors (the
cloth, the mon, and any color within it, like the birds of the Date and
Uesugi crests) by k-means on their color with the folds' shading taken out;
each region's outline is traced at twice the pixels, smooth and
anti-aliased, and filled with the region's median color; and the folds come
back as the original's shading, blurred so the cloth is calm.
"""
import json
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, os.path.join(REPO, 'ArtGen', 'common'))
import civ3art  # noqa: E402

RULESET = os.path.join(REPO, 'C7', 'Lua', 'mdj', 'ruleset.json')
OUT = os.path.join(REPO, 'C7', 'ModernArt', 'Art', 'Leaderheads')
CELL = 115
SCALE = 2
# How many flat colors each banner has: the cloth and its mon, and for a few
# a color within the mon (the white sparrows of the Uesugi and Date crests,
# and the Date's gray bamboo).
COLORS = {'RACE_UESUGI': 3, 'RACE_DATE': 4}
DEFAULT_COLORS = 2
# How much a pixel's brightness against its surroundings counts, and its
# brightness itself, in telling regions apart (see features).
DEFAULT_WEIGHTS = (0.25, 0.8)
# Banners whose mon is white on the gray cloth, which its folds light as
# brightly as parts of the mon: the mon is where a pixel's brightness, plus
# half again how much brighter it is than its surroundings (blurred by 4
# pixels), is over the threshold, less specks under the area out near the
# banner's edge (the folds' highlights; the mon is in the middle).
# Nothing beyond the share of the banner's radius is the mon.
WHITE_ON_GRAY = {'RACE_TOKUGAWA': (4, 0.5, 0.62, 80, 0.7)}
MERGE_DISTANCE = 0.05
# Saturation never more than the original's (ArtGen/README.md, "The look").
SATURATION = 0.92


def clans():
    rules = json.load(open(RULESET, encoding='utf-8'))
    for c in rules['civilizations']:
        if c.get('leaderArtFile') and c.get('civilopediaEntry', '').startswith('RACE_'):
            yield c['civilopediaEntry'], c['leaderArtFile'].replace(chr(92), '/')


def file_name(entry):
    return entry.split('_', 1)[1].capitalize() + '.png'


def original(rel):
    """The first cell (Civ3 shows the same banner in every one) as float RGB
    and its mask (magenta is transparent)."""
    idx, pal = civ3art.read_pcx(civ3art.media_path(rel))
    rgb = pal[idx][:CELL, :CELL].astype(np.float32) / 255
    mask = ~((rgb[..., 0] > 0.98) & (rgb[..., 1] < 0.02) & (rgb[..., 2] > 0.98))
    return rgb, mask


def kmeans(x, k, iters=30, seed=1):
    rng = np.random.default_rng(seed)
    centers = x[rng.choice(len(x), k, replace=False)]
    for _ in range(iters):
        labels = np.argmin(((x[:, None] - centers[None]) ** 2).sum(-1), 1)
        for i in range(k):
            if (labels == i).any():
                centers[i] = x[labels == i].mean(0)
    return centers, labels


def regions(features, colors_in, mask, k):
    """A label per pixel (-1 outside the banner) and each label's color:
    the pixels sorted by their features, each label's color the median of
    its pixels' colors."""
    x = features[mask]
    centers, labels = kmeans(x, k)
    # Merge colors too close to tell apart.
    merged = list(range(len(centers)))
    for i in range(len(centers)):
        for j in range(i):
            if merged[j] == j and np.linalg.norm(centers[i] - centers[j]) < MERGE_DISTANCE:
                merged[i] = j
                break
    labels = np.array([merged[l] for l in labels])
    kept = sorted(set(labels))
    remap = {l: n for n, l in enumerate(kept)}
    labels = np.array([remap[l] for l in labels])
    out = np.full(mask.shape, -1)
    out[mask] = labels
    out = despeckle(out, len(kept))
    c = colors_in[mask]
    labels = out[mask]
    colors = [np.median(c[labels == n], 0) for n in range(len(kept))]
    return out, colors


def despeckle(labels, n):
    """A pixel of a region that none of its eight neighbors is in (the
    cloth's weave) takes the region most of them are in; lines a pixel wide
    stay."""
    padded = np.pad(labels, 1, constant_values=-1)
    h, w = labels.shape
    counts = np.zeros((n, h, w), np.int32)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            if dy == 0 and dx == 0:
                continue
            nb = padded[1 + dy:1 + dy + h, 1 + dx:1 + dx + w]
            for i in range(n):
                counts[i] += nb == i
    own = np.take_along_axis(counts, np.clip(labels, 0, None)[None], 0)[0]
    lonely = (labels >= 0) & (own == 0)
    out = labels.copy()
    out[lonely] = np.argmax(counts, 0)[lonely]
    return out


def white_on_gray(den, mask, blur, gain, threshold, min_area, within):
    """Labels for a white mon on gray cloth (see WHITE_ON_GRAY): 0 the
    cloth, 1 the mon."""
    lum = den.mean(-1)
    weight = mask.astype(np.float32)
    local = ndimage.gaussian_filter(lum * weight, blur) / np.maximum(ndimage.gaussian_filter(weight, blur), 1e-4)
    mon = mask & (lum + gain * (lum - local) > threshold)
    cy, cx = ndimage.center_of_mass(mask)
    radius = np.sqrt(mask.sum() / np.pi)
    yy, xx = np.mgrid[:mask.shape[0], :mask.shape[1]]
    mon &= np.hypot(yy - cy, xx - cx) < within * radius
    parts, n = ndimage.label(mon)
    sizes = ndimage.sum(mon, parts, range(1, n + 1))
    centers = ndimage.center_of_mass(mon, parts, range(1, n + 1))
    keep = [i + 1 for i, (size, (y, x)) in enumerate(zip(sizes, centers))
            if size >= min_area or np.hypot(y - cy, x - cx) < 0.55 * radius]
    mon &= np.isin(parts, keep)
    labels = np.where(mask, mon.astype(int), -1)
    labels = despeckle(labels, 2)
    colors = [np.median(den[labels == i], 0) for i in range(2)]
    return labels, colors


def features(den, mask, weights):
    """What tells a banner's regions apart whatever the folds: a pixel's
    chromaticity (its color with its brightness taken out), and its
    brightness against its surroundings' (white and black parts of a mon
    against the gray cloth), on a like scale."""
    lum = den.mean(-1)
    chroma = den / np.maximum(lum[..., None], 0.02) / 3
    weight = mask.astype(np.float32)
    local = ndimage.gaussian_filter(lum * weight, 6) / np.maximum(ndimage.gaussian_filter(weight, 6), 1e-4)
    contrast = np.log(np.maximum(lum, 0.02) / np.maximum(local, 0.02))
    saturation = den.max(-1) - den.min(-1)
    w_contrast, w_lum = weights
    return np.dstack([chroma * 1.5, (contrast * w_contrast)[..., None], (saturation * 1.2)[..., None], (lum * w_lum)[..., None]])


def upscale_masks(labels, n, size):
    """Each region's soft mask at the new size: its one-hot mask blurred and
    resized, so outlines come out smooth; then normalized to sum to one."""
    soft = []
    for i in range(n):
        m = (labels == i).astype(np.float32)
        m = ndimage.gaussian_filter(m, 0.7)
        soft.append(np.asarray(Image.fromarray(m).resize((size, size), Image.BICUBIC)))
    soft = np.clip(np.stack(soft, -1), 0, None)
    # Sharpen the transitions back to about a pixel wide, then normalize.
    soft = soft ** 4
    return soft / np.maximum(soft.sum(-1, keepdims=True), 1e-6)


def saturate(c, amount):
    gray = c.mean(-1, keepdims=True)
    return gray + (c - gray) * amount


def remake(entry, rel):
    rgb, mask = original(rel)
    # A touch of blur against the weave before the colors are told apart,
    # little enough to keep the mon's thin lines (see also despeckle).
    den = ndimage.gaussian_filter(rgb, (0.5, 0.5, 0))
    if entry in WHITE_ON_GRAY:
        labels, colors = white_on_gray(den, mask, *WHITE_ON_GRAY[entry])
    else:
        labels, colors = regions(features(den, mask, DEFAULT_WEIGHTS), den, mask, COLORS.get(entry, DEFAULT_COLORS))
    lum = den.mean(-1)
    # The folds: each pixel's brightness against its region's color,
    # low-passed, so they cross the mon's outlines unbroken.
    region_lum = np.array([c.mean() for c in colors])
    ratio = np.where(mask, lum / np.maximum(region_lum[np.clip(labels, 0, None)], 0.02), 0)
    weight = mask.astype(np.float32)
    shade = ndimage.gaussian_filter(ratio * weight, 2.5) / np.maximum(ndimage.gaussian_filter(weight, 2.5), 1e-4)
    shade = np.where(mask, shade, 1)
    size = CELL * SCALE
    soft = upscale_masks(labels, len(colors), size)
    flat = (soft[..., :, None] * np.stack(colors)[None, None]).sum(2)
    shade2 = np.asarray(Image.fromarray(shade.astype(np.float32)).resize((size, size), Image.BICUBIC))
    shade2 = ndimage.gaussian_filter(shade2, 2.0)
    # Gentle contrast: the folds a little softer than the original's.
    shade2 = 1 + (shade2 - 1) * 0.8
    color = saturate(np.clip(flat * shade2[..., None], 0, 1), SATURATION)
    # The banner's outline, smooth, and a thin darker rim to read on any
    # background.
    alpha = ndimage.gaussian_filter(mask.astype(np.float32), 0.8)
    alpha = np.asarray(Image.fromarray(alpha).resize((size, size), Image.BICUBIC))
    alpha = np.clip((alpha - 0.5) * 3 + 0.5, 0, 1)
    rim = np.clip(alpha - ndimage.grey_erosion(alpha, size=(5, 5)), 0, 1)
    color = color * (1 - 0.45 * rim[..., None])
    out = np.dstack([color, alpha])
    return (np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8), (np.dstack([rgb, mask]) * 255).astype(np.uint8)


def main(argv):
    wanted = set(argv)
    os.makedirs(OUT, exist_ok=True)
    pairs = []
    for entry, rel in clans():
        if wanted and entry not in wanted:
            continue
        new, old = remake(entry, rel)
        path = os.path.join(OUT, file_name(entry))
        Image.fromarray(new, 'RGBA').save(path, optimize=True)
        print(f'{entry}: {path}')
        pairs.append((old, new))
    if pairs:
        bg = np.array([92, 128, 60, 255], np.uint8)

        def on_bg(im, scale):
            im = np.asarray(Image.fromarray(im, 'RGBA').resize((CELL * SCALE,) * 2, Image.NEAREST if scale else Image.BILINEAR))
            a = im[..., 3:4] / 255
            return (im[..., :3] * a + bg[:3] * (1 - a)).astype(np.uint8)
        top = np.concatenate([on_bg(o, True) for o, _ in pairs], 1)
        bottom = np.concatenate([on_bg(n, False) for _, n in pairs], 1)
        os.makedirs(civ3art.PREVIEWS, exist_ok=True)
        Image.fromarray(np.concatenate([top, bottom], 0)).save(os.path.join(civ3art.PREVIEWS, 'leaderheads_mdj.png'))


if __name__ == '__main__':
    main(sys.argv[1:])
