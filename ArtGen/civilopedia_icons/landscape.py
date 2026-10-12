"""A tiny map renderer: lays out a few tiles of the remade map art
(C7/ModernArt/Art/Terrain, resources.png) the way the game draws them, so
terrain, resource and worker action icons are views of the real map art.

Tiles are on a diamond grid: tile (i, j) is centered at
(i - j) * 128, (i + j) * 64 in the 2x art's pixels, so +i is southeast and
+j southwest. Sprite placement follows C7/MapView.cs and C7/Map/*.cs.
"""
import functools
import os

import numpy as np
from PIL import Image

import common as C

TW, TH = 256, 128            # a tile at 2x
LAND = ('grassland', 'plains', 'desert', 'tundra')
WATER = ('coast', 'sea', 'ocean')

# (i, j) offsets of the neighbors.
NB = {'N': (-1, -1), 'NW': (-1, 0), 'NE': (0, -1), 'S': (1, 1), 'SE': (1, 0), 'SW': (0, 1), 'E': (1, -1), 'W': (-1, 1)}


@functools.lru_cache(None)
def sheet(name):
    path = os.path.join(C.MODERN, 'Art', *name.split('/'))
    return C.to_float(Image.open(path))


def cell(name, col, row, w, h, x0=0, y0=0):
    s = sheet(name)
    return s[y0 + row * h:y0 + (row + 1) * h, x0 + col * w:x0 + (col + 1) * w]


def _hash(*a):
    h = 2166136261
    for v in a:
        h = ((h ^ (int(v) & 0xffffffff)) * 16777619) & 0xffffffff
    return h


class Tile:
    def __init__(self, base='grassland', overlay=None, **kw):
        self.base = base
        self.overlay = overlay   # hills, mountains, volcano, forest, jungle, marsh, floodplain
        self.pine = kw.pop('pine', False)
        self.snow = kw.pop('snow', False)
        self.road = kw.pop('road', False)
        self.rail = kw.pop('rail', False)
        self.irrigation = kw.pop('irrigation', False)
        self.bonus = kw.pop('bonus', False)
        self.resource = kw.pop('resource', None)
        self.building = kw.pop('building', None)     # (col, row) in TerrainBuildings
        self.pollution = kw.pop('pollution', False)
        self.extra = kw


class Patch:
    """A small map: a default tile everywhere, with some tiles set."""

    def __init__(self, default=None, radius=4):
        self.default = default or Tile()
        self.tiles = {}
        self.rivers = set()      # frozenset({(i, j), (i2, j2)}) of diagonal neighbors
        self.r = radius

    def __getitem__(self, ij):
        return self.tiles.get(ij, self.default)

    def set(self, ij, **kw):
        self.tiles[ij] = Tile(**kw)
        return self.tiles[ij]

    def nb(self, ij, d):
        di, dj = NB[d]
        return (ij[0] + di, ij[1] + dj)

    def river(self, a, b):
        self.rivers.add(frozenset((a, b)))

    def has_river(self, a, d):
        return frozenset((a, self.nb(a, d))) in self.rivers

    def coords(self):
        r = self.r
        out = [(i, j) for i in range(-r, r + 1) for j in range(-r, r + 1)]
        return sorted(out, key=lambda ij: (ij[0] + ij[1], ij[0] - ij[1]))


# ---------------------------------------------------------------- base terrain

FILES = ['xtgc', 'xpgc', 'xdgc', 'xdpc', 'xdgp', 'xggc', 'wCSO', 'wSSS', 'wOOO']
_START = {'xtgc': 'tundra', 'xpgc': 'plains', 'xdgc': 'desert', 'xdpc': 'desert', 'xdgp': 'desert', 'wCSO': 'coast'}
_MIDDLE = {'xtgc': 'grassland', 'xpgc': 'grassland', 'xdgc': 'grassland', 'xdpc': 'plains', 'xdgp': 'grassland', 'wCSO': 'sea'}


def _base_type(t):
    return t.base


def base_sprite(patch, ij, seed=0):
    """The file and image index of a tile's base sprite, as
    C7Engine/TerrainTextureFiles.cs picks them."""
    nbs = [patch[patch.nb(ij, 'N')].base, patch[patch.nb(ij, 'NW')].base, patch[patch.nb(ij, 'NE')].base, patch[ij].base]
    counts = {k: nbs.count(k) for k in set(nbs)}
    water = sum(counts.get(k, 0) for k in WATER)
    uniq = len(counts)
    if water == 4:
        if counts.get('ocean', 0) == 4:
            return 'wOOO', _hash(seed, *ij) % 81
        if counts.get('sea', 0) == 4:
            return 'wSSS', _hash(seed, *ij) % 81
        f = 'wCSO'
    elif counts.get('tundra', 0):
        f = 'xtgc'
    elif uniq >= 3:
        c, d, p, g = (counts.get(k, 0) > 0 for k in ('coast', 'desert', 'plains', 'grassland'))
        f = 'xdgp' if not c else 'xdpc' if not g else 'xpgc' if not d else 'xdgc'
    elif uniq == 2:
        c, d, p, g = (counts.get(k, 0) > 0 for k in ('coast', 'desert', 'plains', 'grassland'))
        if g and c or p and c:
            f = 'xpgc'
        elif d and c:
            f = 'xdpc'
        elif g and p:
            f = 'xpgc'
        elif g and d:
            f = 'xdgc'
        else:
            f = 'xdpc'
    else:
        f = {'grassland': 'xdgc', 'plains': 'xpgc', 'desert': 'xdgp'}.get(nbs[0], 'xpgc')
    w = [(0, 1, 2), (0, 3, 6), (0, 9, 18), (0, 27, 54)]
    idx = 0
    for k, t in enumerate(nbs):
        if t == _START[f]:
            idx += w[k][0]
        elif t == _MIDDLE[f]:
            idx += w[k][1]
        else:
            idx += w[k][2]
    return f, idx


# ---------------------------------------------------------------- drawing

class Canvas:
    def __init__(self, w=1536, h=1152):
        self.img = np.zeros((h, w, 4), np.float32)
        self.ox, self.oy = w // 2, h // 2

    def center(self, ij):
        i, j = ij
        return self.ox + (i - j) * 128, self.oy + (i + j) * 64

    def paste(self, spr, x, y, alpha=1.0):
        """Alpha-composites a sprite with its top-left at (x, y)."""
        x, y = int(round(x)), int(round(y))
        H, W = self.img.shape[:2]
        h, w = spr.shape[:2]
        x0, y0, x1, y1 = max(0, x), max(0, y), min(W, x + w), min(H, y + h)
        if x1 <= x0 or y1 <= y0:
            return
        s = spr[y0 - y:y1 - y, x0 - x:x1 - x].copy()
        if alpha != 1.0:
            s[..., 3] *= alpha
        self.img[y0:y1, x0:x1] = C.over(s, self.img[y0:y1, x0:x1])

    def crop(self, cx, cy, size):
        x0, y0 = int(round(cx - size / 2)), int(round(cy - size / 2))
        return self.img[y0:y0 + size, x0:x0 + size].copy()


def _hilly(t):
    return t.overlay in ('hills', 'mountains', 'volcano')


def draw(patch, seed=0, objects_for=None, split=False):
    """Draws the patch. objects_for: the tiles whose features, buildings and
    resources are drawn (default all). With split, returns the ground and
    the objects on two canvases."""
    cv = Canvas()
    coords = patch.coords()
    # base tiles (each sprite's corners are the centers of N, NW, NE and the tile)
    for ij in coords:
        f, idx = base_sprite(patch, ij, seed)
        spr = cell(f'Terrain/{f}.png', idx % 9, idx // 9, TW, TH)
        cx, cy = cv.center(ij)
        cv.paste(spr, cx - 128, cy - 128)
    # flood plains, rivers, bonus shields, irrigation, roads
    for ij in coords:
        t = patch[ij]
        cx, cy = cv.center(ij)
        if t.overlay == 'floodplain':
            idx = (patch.has_river(ij, 'NW') and 1) | (patch.has_river(ij, 'NE') and 2) | \
                  (patch.has_river(ij, 'SW') and 4) | (patch.has_river(ij, 'SE') and 8)
            if idx:
                cv.paste(cell('Terrain/floodplains.png', idx % 4, idx // 4, TW, TH), cx - 128, cy - 64)
    for ij in coords:
        cx, cy = cv.center(ij)
        ne, e, se = patch.nb(ij, 'NE'), patch.nb(ij, 'E'), patch.nb(ij, 'SE')
        idx = 0
        if frozenset((ij, ne)) in patch.rivers:
            idx += 1
        if frozenset((e, ne)) in patch.rivers:
            idx += 2
        if frozenset((ij, se)) in patch.rivers:
            idx += 4
        if frozenset((se, e)) in patch.rivers:
            idx += 8
        if idx:
            water = any(patch[x].base in WATER for x in (ij, ne, e, se))
            name = 'Terrain/deltaRivers.png' if (idx in (1, 2, 4, 8) and water) or idx not in (1, 2, 4, 8) else 'Terrain/mtnRivers.png'
            cv.paste(cell(name, idx % 4, idx // 4, TW, TH), cx + 128 - 128, cy - 64)
    for ij in coords:
        t = patch[ij]
        cx, cy = cv.center(ij)
        if t.bonus:
            cv.paste(cell('Terrain/tnt.png', 0, 3, TW, TH), cx - 128, cy - 64)
        if t.irrigation:
            idx = 0
            for d, flag in (('NW', 1), ('NE', 2), ('SW', 4), ('SE', 8)):
                if patch[patch.nb(ij, d)].irrigation:
                    idx |= flag
            name = {'desert': 'irrigation DESETT', 'plains': 'irrigation PLAINS', 'tundra': 'irrigation TUNDRA'}.get(t.base, 'irrigation')
            cv.paste(cell(f'Terrain/{name}.png', idx % 4, idx // 4, TW, TH), cx - 128, cy - 64)
        if t.road or t.rail:
            ridx, railidx = 0, 0
            for d, flag in (('NE', 1), ('E', 2), ('SE', 4), ('S', 8), ('SW', 16), ('W', 32), ('NW', 64), ('N', 128)):
                n = patch[patch.nb(ij, d)]
                if n.road or n.rail:
                    ridx |= flag
                if n.rail:
                    railidx |= flag
            if not t.rail or ridx != railidx:
                cv.paste(cell('Terrain/roads.png', ridx & 15, ridx >> 4, TW, TH), cx - 128, cy - 64)
            if t.rail:
                cv.paste(cell('Terrain/railroads.png', railidx & 15, railidx >> 4, TW, 136), cx - 128, cy - 64 - 8)
        if t.pollution:
            # a single polluted tile: one of the ten variations in the first two rows
            k = int(t.pollution) % 10
            cv.paste(cell('Terrain/pollution.png', k % 5, k // 5, TW, TH), cx - 128, cy - 64)
    ground = cv
    if split:
        cv = Canvas()
    # features, back to front
    for ij in coords:
        if objects_for is not None and ij not in objects_for:
            continue
        t = patch[ij]
        cx, cy = cv.center(ij)
        h = _hash(seed, *ij)
        if t.overlay in ('forest', 'jungle', 'marsh', 'forest_small'):
            if t.overlay == 'forest_small':
                name, row0, cols = 'Terrain/grassland forests.png', 6, 5
            elif t.overlay == 'marsh':
                name, row0, cols = 'Terrain/marsh.png', 0, 4
            elif t.overlay == 'jungle':
                name, row0, cols = 'Terrain/grassland forests.png', 0, 4
            else:
                name = {'plains': 'Terrain/plains forests.png', 'tundra': 'Terrain/tundra forests.png'}.get(t.base, 'Terrain/grassland forests.png')
                row0, cols = (8, 6) if t.pine else (4, 4)
            spr = cell(name, h % cols, row0 + (h >> 8) % 2, 256, 176)
            cv.paste(spr, cx - 128, cy - 88 - 24)
        elif _hilly(t):
            idx = 0
            for d, flag in (('NW', 1), ('NE', 2), ('SW', 4), ('SE', 8)):
                if _hilly(patch[patch.nb(ij, d)]):
                    idx += flag
            if t.overlay == 'hills':
                cv.paste(cell('Terrain/xhills.png', idx % 4, idx // 4, 256, 144), cx - 128, cy - 72 - 8)
            else:
                name = 'Terrain/Volcanos.png' if t.overlay == 'volcano' else \
                    'Terrain/Mountains-snow.png' if t.snow else 'Terrain/Mountains.png'
                cv.paste(cell(name, idx % 4, idx // 4, 256, 176), cx - 128, cy - 88 - 24)
        if t.building is not None:
            col, row = t.building
            spr = cell('Terrain/TerrainBuildings.png', col, row, TW, TH)
            k = t.extra.get('bscale', 1.0)
            if k != 1.0:
                spr = C.resize(spr, (int(TW * k), int(TH * k)))
            dx, dy = t.extra.get('boffset', (0, 0))
            cv.paste(spr, cx - 128 * k + dx, cy - 64 * k + dy)
        if t.resource is not None:
            r = t.resource
            cv.paste(cell('resources.png', r % 6, r // 6, 100, 100), cx - 50, cy - 50)
    return (ground, cv) if split else cv


def view(patch, size=C.LARGE, zoom=0.5, offset=(0, 0), seed=0, ss=2):
    """The patch seen through a size x size window around tile (0, 0)'s
    center (plus `offset` in 2x art pixels), `zoom` icon pixels per 2x art
    pixel. Rendered at `ss` times the size and filtered down."""
    cv = draw(patch, seed)
    cx, cy = cv.center((0, 0))
    span = int(round(size / zoom))
    img = cv.crop(cx + offset[0], cy + offset[1], span)
    out = C.resize(img, (size, size))
    return out


# ---------------------------------------------------------------- dioramas

def _poly_mask(h, w, pts, ss=4):
    """An antialiased mask of a polygon (pixel coordinates)."""
    from PIL import ImageDraw
    im = Image.new('L', (w * ss, h * ss), 0)
    ImageDraw.Draw(im).polygon([(x * ss, y * ss) for x, y in pts], fill=255)
    im = im.resize((w, h), Image.BOX)
    return np.asarray(im, np.float32) / 255


SIDE = {'water': ('4c6f7c', '2f4a56', '5d8a98'), 'land': ('8a7656', '5e4e38', '6d7a3e')}


def diorama(patch, size=C.LARGE, zoom=0.46, depth=30, center_y=0.64, seed=0, backdrop=None, extra=None, shift=(0, 0)):
    """Tile (0, 0) as a little block of land seen from the map's angle: its
    ground (the patch's terrain, roads, rivers... cut to the tile's diamond),
    a slab of earth under it, and its own features, buildings and resources
    on top; over `backdrop` (a size x size RGBA, default a soft sky).
    extra(canvas, (cx, cy)) may draw more objects (in 2x art pixels) before
    the view is cut out. Returns size x size RGBA."""
    ground, objs = draw(patch, seed, objects_for={(0, 0)}, split=True)
    cx, cy = ground.center((0, 0))
    cx, cy = cx + shift[0], cy + shift[1]
    # Only the area around the tile matters: work on a window of the canvas.
    R = 400
    wx0, wy0 = cx - R, cy - R
    g = ground.img[wy0:wy0 + 2 * R, wx0:wx0 + 2 * R].copy()
    ob = objs.img[wy0:wy0 + 2 * R, wx0:wx0 + 2 * R].copy()
    cx, cy = R, R
    H, W = g.shape[:2]
    top = [(cx, cy - 64), (cx + 128, cy), (cx, cy + 64), (cx - 128, cy)]
    g[..., 3] *= _poly_mask(H, W, top)
    # the slab: left (lit a little) and right (shaded) faces
    water = patch[(0, 0)].base in WATER
    c_top, c_bot, c_band = (C.hex_rgb(c) for c in SIDE['water' if water else 'land'])
    slab = np.zeros_like(g)
    yy = np.arange(H, dtype=np.float32)[:, None]
    xx = np.arange(W, dtype=np.float32)[None, :]
    edge_y = np.where(xx < cx, cy + (xx - (cx - 128)) / 2, cy + 64 - (xx - cx) / 2)
    t = np.clip((yy - edge_y) / depth, 0, 1)
    band = np.clip(1 - (yy - edge_y) / 7.0, 0, 1)[..., None]
    for pts, k in (([(cx - 128, cy), (cx, cy + 64), (cx, cy + 64 + depth), (cx - 128, cy + depth)], 0.92),
                   ([(cx, cy + 64), (cx + 128, cy), (cx + 128, cy + depth), (cx, cy + 64 + depth)], 0.66)):
        fm = _poly_mask(H, W, pts)
        col = C.mix(c_top, c_bot, t[..., None] ** 0.8) * k
        col = col * (1 - 0.6 * band) + band * 0.6 * c_band * k
        layer = np.concatenate([np.broadcast_to(col, (H, W, 3)), fm[..., None]], -1)
        slab = C.over(layer, slab)
    # a soft shadow on the backdrop, down and to the right
    sh_pts = [(cx + 20, cy - 34), (cx + 168, cy + 40), (cx + 20, cy + 64 + depth + 30), (cx - 108, cy + depth + 10)]
    shadow = np.zeros_like(g)
    shadow[..., 3] = C.blur(_poly_mask(H, W, sh_pts), 14) * 0.30
    img = C.over(ob, C.over(g, C.over(slab, shadow)))
    if extra is not None:
        cvx = Canvas(W, H)
        cvx.img = img
        extra(cvx, (cx, cy))
        img = cvx.img
    span = int(round(size / zoom))
    vy = cy + 32 - (center_y - 0.5) * span   # the view's center
    x0, y0 = int(round(cx - span / 2)), int(round(vy - span / 2))
    crop = img[y0:y0 + span, x0:x0 + span]
    out = C.resize(crop, (size, size))
    if backdrop is None:
        backdrop = sky_backdrop(size)
    return C.over(out, backdrop)


def sky_backdrop(n):
    rgb = C.vgradient(n, C.hex_rgb('c3cdd0'), C.hex_rgb('e9e5d8'))
    rgb = C.vignette(rgb, 0.14)
    return np.concatenate([rgb, np.ones((n, n, 1), np.float32)], -1)
