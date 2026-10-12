"""Terrain and resource icons: views of the remade map art (landscape.py)."""
import numpy as np

import common as C
import landscape as L
from landscape import Patch, Tile


def _river_line(p, i0=-4, i1=4, j=0):
    """A river along the northeast edges of tiles (i, j): it runs from the
    upper left to the lower right through the middle of the view."""
    for i in range(i0, i1 + 1):
        p.river((i, j), (i, j - 1))


def terrain_patch(key):
    """The patch of map for a terrain entry, and the diorama's shift (the
    rivers run along tile edges, so their dioramas are centered on a corner
    where four tiles meet)."""
    t = key[len('TERR_'):].lower()
    shift = (0, 0)
    if t in ('desert', 'plains', 'grassland', 'tundra'):
        p = Patch(Tile(t))
    elif t == 'flood_plain':
        p = Patch(Tile('desert'))
        _river_line(p)
        for i in range(-4, 5):
            p.set((i, 0), base='desert', overlay='floodplain')
            p.set((i, -1), base='desert', overlay='floodplain')
        shift = (128, 0)
    elif t in ('hills', 'mountains', 'volcano'):
        p = Patch(Tile('grassland' if t != 'volcano' else 'plains'))
        p.set((0, 0), base=p.default.base, overlay=t)
    elif t in ('forest', 'jungle', 'marsh'):
        p = Patch(Tile('grassland', t))
    elif t in ('coast', 'sea', 'ocean'):
        p = Patch(Tile(t))
        if t == 'coast':
            for ij in [(-1, -1), (-1, 0), (-2, -1), (-1, -2), (-2, -2), (-2, 0), (-1, 1), (-2, 1)]:
                p.set(ij, base='grassland')
    elif t == 'river':
        p = Patch(Tile('grassland'))
        _river_line(p)
        shift = (128, 0)
    elif t == 'fresh_water_lake':
        p = Patch(Tile('grassland'))
        p.set((0, 0), base='coast')
    elif t == 'grassland_with_shield':
        p = Patch(Tile('grassland'))
        p.set((0, 0), base='grassland', bonus=True)
    else:
        raise KeyError(key)
    return p, shift


def terrain_icon(key):
    p, shift = terrain_patch(key)
    extra = None
    if p[(0, 0)].bonus:
        # The bonus shield is tiny on the map: show it bigger.
        p[(0, 0)].bonus = False
        spr = L.cell('Terrain/tnt.png', 0, 3, L.TW, L.TH)
        ys, xs = np.nonzero(spr[..., 3] > 0.05)
        part = spr[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
        k = 3.2
        big = C.resize(part, (int(part.shape[1] * k), int(part.shape[0] * k)))

        def extra(cv, c):
            cv.paste(big, c[0] - big.shape[1] / 2, c[1] - big.shape[0] * 0.75)
    large = L.diorama(p, C.LARGE, seed=3, shift=shift, extra=extra)
    small = L.diorama(p, C.SMALL * 4, zoom=0.46 * 4 * C.SMALL / C.LARGE * 1.12, seed=3, shift=shift, extra=extra)
    small = C.sharpen(C.downsample(small, 4), 0.5, 0.6)
    return _finish(large), _finish(small)


def _finish(img):
    img = img.copy()
    img[..., 3] = 1
    img[..., :3] = C.frame_edges(img[..., :3])
    return img


# ---------------------------------------------------------------- resources

# Where each resource is shown (its usual terrain).
RESOURCE_TERRAIN = {
    'GOOD_Horses': ('plains', None), 'GOOD_Iron': ('grassland', 'hills'), 'GOOD_Saltpeter': ('desert', None),
    'GOOD_Coal': ('grassland', 'hills'), 'GOOD_Oil': ('desert', None), 'GOOD_Rubber': ('grassland', 'jungle'),
    'GOOD_Aluminum': ('desert', None), 'GOOD_Uranium': ('tundra', None), 'GOOD_Wine': ('grassland', 'hills'),
    'GOOD_Furs': ('tundra', 'forest'), 'GOOD_Dye': ('grassland', 'jungle'), 'GOOD_Incense': ('desert', None),
    'GOOD_Spice': ('grassland', 'jungle'), 'GOOD_Ivory': ('plains', None), 'GOOD_Silk': ('grassland', 'forest'),
    'GOOD_Diamonds': ('grassland', 'jungle'), 'GOOD_Whales': ('sea', None), 'GOOD_Game': ('grassland', 'forest'),
    'GOOD_Fish': ('coast', None), 'GOOD_Cattle': ('grassland', None), 'GOOD_Wheat': ('plains', None),
    'GOOD_Gold': ('grassland', 'mountains'), 'GOOD_Sugar': ('grassland', None), 'GOOD_Bananas': ('grassland', 'jungle'),
    'GOOD_Oasis': ('desert', None), 'GOOD_Tobacco': ('grassland', None),
}


def resource_icon(res):
    key = res['civilopediaEntry']
    base, overlay = RESOURCE_TERRAIN.get(key, ('grassland', None))
    # The terrain stays a plain backdrop (its features would hide the
    # resource); hills, forests and the like tint it a little instead.
    p = Patch(Tile(base))
    if base in L.LAND and overlay in ('forest', 'jungle'):
        p = Patch(Tile(base))
        for ij in [(-1, -1), (-2, -1), (-1, -2), (-2, -2), (-2, 0), (0, -2)]:
            p.set(ij, base=base, overlay=overlay)
    elif base in L.LAND and overlay in ('hills', 'mountains'):
        for ij in [(-1, -1), (-2, -1), (-1, -2), (-2, -2)]:
            p.set(ij, base=base, overlay=overlay)
    p.set((0, 0), base=base, resource=res['icon'])
    # Fit the view to the resource's sprite.
    r = res['icon']
    spr = L.cell('resources.png', r % 6, r // 6, 100, 100)
    ys, xs = np.nonzero(spr[..., 3] > 0.25)
    bx, by = (xs.min() + xs.max() + 1) / 2 - 50, (ys.min() + ys.max() + 1) / 2 - 50
    ext = max(xs.max() - xs.min() + 1, ys.max() - ys.min() + 1)
    zl = min(1.5, C.LARGE * 0.64 / ext)
    zs = min(2.4, C.SMALL * 4 * 0.86 / ext)
    large = L.view(p, C.LARGE, zl, (bx, by + 4), seed=5)
    small = L.view(p, C.SMALL * 4, zs, (bx, by + 2), seed=5)
    small = C.sharpen(C.downsample(small, 4), 0.5, 0.6)
    return _finish(large), _finish(small)
