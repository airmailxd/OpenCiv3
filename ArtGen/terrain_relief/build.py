"""Builds the remade relief and vegetation overlays: hills, mountains, volcanoes
(bare, forested, jungle and snowy), forests, jungles, pines, marsh and flood plains.

Usage: python ArtGen/terrain_relief/build.py [--preview] [name ...]
Writes C7/ModernArt/Art/Terrain/*.png; with --preview also side-by-side images
(original 2x | remake) into ArtGen/_previews/relief_*.png.
"""
import os
import sys
from multiprocessing import Pool

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import render  # noqa: E402,F401  (puts ArtGen/common on the path)
import numpy as np  # noqa: E402

import civ3art  # noqa: E402
import relief  # noqa: E402
import vegetation  # noqa: E402
from render import to_png_preview  # noqa: E402

T = 'Art/Terrain/'

# name -> (builder, args); seeds are fixed so a rebuild gives the same art.
JOBS = {
    'xhills':            ('relief', (T + 'xhills.pcx', 'hill', 128, 72, 1, None, None)),
    'hill forests':      ('relief', (T + 'hill forests.pcx', 'hill', 128, 72, 1, T + 'xhills.pcx', 'forest')),
    'hill jungle':       ('relief', (T + 'hill jungle.pcx', 'hill', 128, 72, 1, T + 'xhills.pcx', 'jungle')),
    'Mountains':         ('relief', (T + 'Mountains.pcx', 'mountain', 128, 88, 2, None, None)),
    'Mountains-snow':    ('relief', (T + 'Mountains-snow.pcx', 'snow', 128, 88, 2, None, None)),
    'mountain forests':  ('relief', (T + 'mountain forests.pcx', 'mountain', 128, 88, 2, T + 'Mountains.pcx', 'forest')),
    'mountain jungles':  ('relief', (T + 'mountain jungles.pcx', 'mountain', 128, 88, 2, T + 'Mountains.pcx', 'jungle')),
    'Volcanos':          ('relief', (T + 'Volcanos.pcx', 'volcano', 128, 88, 3, None, None)),
    'Volcanos forests':  ('relief', (T + 'Volcanos forests.pcx', 'volcano', 128, 88, 3, T + 'Volcanos.pcx', 'forest')),
    'Volcanos jungles':  ('relief', (T + 'Volcanos jungles.pcx', 'volcano', 128, 88, 3, T + 'Volcanos.pcx', 'jungle')),
    'grassland forests': ('forest', (T + 'grassland forests.pcx', 11)),
    'plains forests':    ('forest', (T + 'plains forests.pcx', 12)),
    'tundra forests':    ('forest', (T + 'tundra forests.pcx', 13)),
    'marsh':             ('marsh', (T + 'marsh.pcx', 14)),
    'floodplains':       ('flood', (T + 'floodplains.pcx', 15)),
}


def run(name, preview):
    kind, args = JOBS[name]
    if kind == 'relief':
        rel, k, cw, ch, seed, base, veg = args
        orig, out = relief.relief_sheet(rel, k, cw, ch, seed, base_rel=base, veg=veg)
    elif kind == 'forest':
        rel, seed = args
        orig, out = vegetation.forest_sheet(rel, seed)
    elif kind == 'marsh':
        rel, seed = args
        orig, out = vegetation.marsh_sheet(rel, seed)
    else:
        rel, seed = args
        orig, out = vegetation.floodplain_sheet(rel, seed)
    path = civ3art.save_modern(rel, out)
    if preview:
        o = to_png_preview(civ3art.upscale_nearest(orig))
        n = to_png_preview(out)
        civ3art.save_preview('relief_' + name.replace(' ', '_'), o, n)
    return path


def _job(a):
    return run(*a)


if __name__ == '__main__':
    preview = '--preview' in sys.argv
    names = [a for a in sys.argv[1:] if not a.startswith('--')] or list(JOBS)
    with Pool(min(len(names), os.cpu_count() or 4)) as pool:
        for p in pool.imap_unordered(_job, [(n, preview) for n in names]):
            print('wrote', p, flush=True)
