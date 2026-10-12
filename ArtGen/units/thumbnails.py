"""Draws each remade unit's thumbnail, the small picture of the unit in
lists (city screen production and garrison, right-click menus, the military
advisor), for standalone mode, where Civ3's Art/Units/units_32.pcx isn't
there:

    C7/ModernArt/Art/Units/<unit>/<unit>_32.png

    python ArtGen/units/thumbnails.py                  # every catalogued unit
    python ArtGen/units/thumbnails.py Warrior Archer   # just these

A thumbnail is the unit's own 3D model (the one its map sprites are rendered
from), standing and turned a little left of the camera like Civ3's
thumbnails, on a transparent background. Like all of ModernArt it has twice
the pixels it's shown with: 64x64 for a 32x32 thumbnail. Its civ-colored
parts are in the same blue as the Civilopedia's unit icons. The standalone
textures (C7/Lua/standalone/textures.lua) pick the thumbnail of the art a
player's unit is drawn with, so it changes with the era like the unit does.

A contact sheet goes to ArtGen/_previews/units/thumbnails.png.
"""
import multiprocessing as mp
import os
import sys

for _v in ('OMP_NUM_THREADS', 'OPENBLAS_NUM_THREADS', 'MKL_NUM_THREADS'):
    os.environ.setdefault(_v, '1')

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), 'civilopedia_icons'))
sys.path.insert(0, HERE)

import numpy as np  # noqa: E402
import common as C  # noqa: E402  (ArtGen/civilopedia_icons; puts units and common on the path)
import civ3art  # noqa: E402
import catalog  # noqa: E402
import units as icons  # noqa: E402  (the Civilopedia's unit renderer)

SIZE = 32 * civ3art.SCALE


def thumbnail_path(unit):
    return os.path.join(civ3art.MODERN_ART, 'Art', 'Units', unit, unit + '_32.png')


def kinds():
    """'land', 'sea' or 'air' for each unit folder the bundled rules use
    (how the model is framed: standing on the ground or centered)."""
    out = {}
    for proto in C.load_ruleset()['unitPrototypes']:
        art = proto['art']['mainArt']
        for folder in [art['defaultName']] + list((art.get('variations') or {}).values()):
            out.setdefault(folder.lower(), icons.unit_kind(proto))
    return out


def make(unit, kind):
    """The thumbnail of a unit folder (float RGBA, SIZE x SIZE)."""
    # Drawn at twice the size and box-filtered down, like the Civilopedia's
    # small icons, so thin parts (spears, masts) stay crisp but smooth.
    big = icons.render_unit(unit, SIZE * 2, icons.DEFAULT_TINT, kind, fill=0.94)
    return C.sharpen(C.downsample(big, 2), 0.3, 0.6)


def _job(task):
    unit, kind = task
    try:
        img = make(unit, kind)
        C.save_png(img, thumbnail_path(unit))
        return unit, img, None
    except Exception:
        import traceback
        return unit, None, traceback.format_exc()


def main(argv):
    opts = dict(a[2:].split('=', 1) if '=' in a else (a[2:], '1') for a in argv if a.startswith('--'))
    units = [a for a in argv if not a.startswith('--')] or sorted(catalog.load(), key=str.lower)
    by_folder = kinds()
    tasks = [(u, by_folder.get(u.lower(), 'land')) for u in units]
    n = int(opts.get('jobs', os.cpu_count() or 4))
    done, failed = [], []
    with mp.Pool(n) as pool:
        for unit, img, err in pool.imap(_job, tasks):
            if err:
                print(f'  FAILED {unit}:\n{err}', flush=True)
                failed.append(unit)
            else:
                done.append((unit, img))
    # A contact sheet on a mid gray, to check the thumbnails at a glance.
    cols = 16
    rows = (len(done) + cols - 1) // cols
    sheet = np.zeros((rows * SIZE, cols * SIZE, 4), np.float32)
    sheet[..., :3] = 0.55
    sheet[..., 3] = 1
    for i, (_, img) in enumerate(done):
        r, c = divmod(i, cols)
        cell = sheet[r * SIZE:(r + 1) * SIZE, c * SIZE:(c + 1) * SIZE]
        cell[:] = C.over(img, cell)
    preview = os.path.join(civ3art.PREVIEWS, 'units', 'thumbnails.png')
    C.save_png(sheet, preview)
    print(f'{len(done)} thumbnails, {len(failed)} failed; preview: {preview}')
    if failed:
        sys.exit(1)


if __name__ == '__main__':
    main(sys.argv[1:])
