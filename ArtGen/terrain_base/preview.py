"""Seam check: composes a random map from the base terrain sheets the way the game
does (same sheet/cell selection as C7Engine/TerrainTextureFiles.cs, same sprite
placement as MapView.cs TerrainLayer), with the original and the remade art.
Writes ArtGen/_previews/terrain_map_{orig,modern}_{1x,2x}.png."""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'common'))
import civ3art  # noqa: E402

FILES = ['xtgc', 'xpgc', 'xdgc', 'xdpc', 'xdgp', 'xggc', 'wCSO', 'wSSS', 'wOOO']
WATER = ('coast', 'sea', 'ocean')
KEYS = {'t': 'tundra', 'g': 'grassland', 'p': 'plains', 'd': 'desert', 'c': 'coast', 's': 'sea', 'o': 'ocean'}


def make_map(w, h, seed):
    """Terrain letters on tiles (x, y) with x + y even; w columns of x, h rows."""
    rng = np.random.default_rng(seed)
    from scipy.ndimage import gaussian_filter
    land = gaussian_filter(rng.normal(size=(h, w * 2)), 2.2)
    kind = gaussian_filter(rng.normal(size=(h, w * 2)), 1.6)
    kind2 = gaussian_filter(rng.normal(size=(h, w * 2)), 1.6)
    m = {}
    for y in range(h):
        for x in range(w * 2):
            if (x + y) % 2:
                continue
            if land[y, x] < -0.02:
                m[x, y] = 'o'
            elif kind[y, x] > 0.35 and y < h // 2:
                m[x, y] = 't'
            elif kind2[y, x] > 0.25:
                m[x, y] = 'd'
            elif kind2[y, x] < -0.2:
                m[x, y] = 'p'
            else:
                m[x, y] = 'g'

    def nb(x, y):
        return [(x + dx, y + dy) for dx, dy in ((0, -2), (0, 2), (-1, -1), (1, -1), (-1, 1), (1, 1), (-2, 0), (2, 0))]
    # Tundra only next to grassland/coast; water next to land is coast, then sea.
    for (x, y), t in list(m.items()):
        if t in 'pd' and any(m.get(p) == 't' for p in nb(x, y)):
            m[x, y] = 'g'
    for (x, y), t in list(m.items()):
        if t == 'o' and any(m.get(p, 'o') not in 'cso' for p in nb(x, y)):
            m[x, y] = 'c'
    for (x, y), t in list(m.items()):
        if t == 'o' and any(m.get(p) == 'c' for p in nb(x, y)):
            m[x, y] = 's'
    # Some sea patches turn into coast for variety.
    return m


def assign(m, rng):
    """(file, index) per tile, like TerrainTextureFiles.AssignTileTextureDetails."""
    out = {}
    for (x, y), t in m.items():
        nbs = [m.get((x, y - 2), 'c'), m.get((x - 1, y - 1), 'c'), m.get((x + 1, y - 1), 'c'), t]
        nbs = [KEYS[n] for n in nbs]
        cnt = {k: nbs.count(k) for k in KEYS.values()}
        waterN = cnt['ocean'] + cnt['sea'] + cnt['coast']
        uniq = len(set(nbs))
        if waterN == 4:
            if cnt['ocean'] == 4:
                out[x, y] = (8, int(rng.integers(81)))
                continue
            if cnt['sea'] == 4:
                out[x, y] = (7, int(rng.integers(81)))
                continue
            f = 6
        elif cnt['tundra']:
            f = 0
        elif uniq >= 3:
            hc, hd, hp, hg = cnt['coast'], cnt['desert'], cnt['plains'], cnt['grassland']
            f = 4 if not hc else 3 if not hg else 1 if not hd else 2
        elif uniq == 2:
            hd, hp, hg, hc = cnt['desert'], cnt['plains'], cnt['grassland'], cnt['coast']
            if hg and hc: f = 1
            elif hp and hc: f = 1
            elif hd and hc: f = 3
            elif hg and hp: f = 1
            elif hg and hd: f = 2
            elif hp and hd: f = 3
            else: f = None
        else:
            f = 2 if cnt['grassland'] else 1 if cnt['plains'] else 4 if cnt['desert'] else None
        if f is None:
            continue
        start, middle = {0: ('tundra', 'grassland'), 1: ('plains', 'grassland'), 2: ('desert', 'grassland'),
                         3: ('desert', 'plains'), 4: ('desert', 'grassland'), 6: ('coast', 'sea')}[f]
        loc = 0
        for i, n in enumerate(nbs):
            loc += 0 if n == start else (1 if n == middle else 2) * 3 ** i
        out[x, y] = (f, loc)
    return out


def compose(sheets, scale, m, tex, w, h):
    W, H = (w * 2) * 64 * scale + 128 * scale, h * 32 * scale + 64 * scale
    img = Image.new('RGBA', (W, H), (255, 0, 255, 255))
    cw, ch = 128 * scale, 64 * scale
    crops = {}
    for (x, y), (f, i) in sorted(tex.items(), key=lambda kv: kv[1][0]):
        key = (f, i)
        if key not in crops:
            cy, cx = divmod(i, 9)
            crops[key] = sheets[f].crop((cx * cw, cy * ch, cx * cw + cw, cy * ch + ch))
        # tile center (x*64, y*32) at 1x; sprite top-left = center - (64, 64)
        img.alpha_composite(crops[key], (x * 64 * scale - 64 * scale + 64 * scale, y * 32 * scale))
    return img


def main():
    w, h = 14, 40
    m = make_map(w, h, 7)
    tex = assign(m, np.random.default_rng(1))
    orig = [Image.fromarray(civ3art.load_original(f'Art/Terrain/{f}.pcx')) for f in FILES]
    modern = [Image.open(civ3art.modern_path(f'Art/Terrain/{f}.pcx')).convert('RGBA') for f in FILES]
    o1 = compose(orig, 1, m, tex, w, h)
    m2 = compose(modern, 2, m, tex, w, h)
    # The game draws the 2x art unfiltered at 1x: every other pixel.
    m1 = Image.fromarray(np.asarray(m2)[::2, ::2])
    os.makedirs(civ3art.PREVIEWS, exist_ok=True)
    o1.save(os.path.join(civ3art.PREVIEWS, 'terrain_map_orig_1x.png'))
    m1.save(os.path.join(civ3art.PREVIEWS, 'terrain_map_modern_1x.png'))
    m2.save(os.path.join(civ3art.PREVIEWS, 'terrain_map_modern_2x.png'))
    civ3art.save_preview('terrain_map_cmp_1x', o1, m1)
    # A 2x crop for close inspection.
    m2.crop((0, 0, 1200, 900)).save(os.path.join(civ3art.PREVIEWS, 'terrain_map_modern_2x_crop.png'))
    o1.resize((o1.width * 2, o1.height * 2), Image.NEAREST).crop((0, 0, 1200, 900)).save(
        os.path.join(civ3art.PREVIEWS, 'terrain_map_orig_2x_crop.png'))


if __name__ == '__main__':
    main()
