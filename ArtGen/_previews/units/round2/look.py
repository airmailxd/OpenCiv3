"""Look sheet for round 2: each unit's frame in all 8 directions, at 2x (the
sheet size) and at 1x (the game's size), on the game's green, units as rows.

    python look.py OUT.png Unit1 Unit2 ... [--action=DEFAULT] [--frame=0] [--zoom=2] [--dirs=0,1,...]
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
UNITS = os.path.normpath(os.path.join(HERE, '..', '..', '..', 'units'))
sys.path.insert(0, UNITS)
CWD = os.getcwd()
os.chdir(UNITS)

import numpy as np  # noqa: E402
from PIL import Image  # noqa: E402

import flic as F  # noqa: E402
import catalog  # noqa: E402
import framework  # noqa: E402
from preview import tinted  # noqa: E402

BG = (96, 132, 62, 255)


def cells(unit, action, frame, dirs):
    cat = catalog.load()
    job = framework.UnitJob(unit, cat[unit])
    name = job.flics()[action]
    fl = F.load_unit_flic(unit, name)
    frame = min(frame, fl.n_frames - 1)
    base, tint, _ = job.render_flic(action, name, rows=list(dirs), cols=[frame])
    W, H = fl.width * 2, fl.height * 2
    out = []
    for d in dirs:
        c = tinted(base[d * H:(d + 1) * H, frame * W:(frame + 1) * W], tint[d * H:(d + 1) * H, frame * W:(frame + 1) * W])
        # placed in the full 240x240 unit space (2x), so every unit's anchor
        # lands at the same spot of its cell, as on the map
        full = Image.new('RGBA', (480, 480), (0, 0, 0, 0))
        full.alpha_composite(Image.fromarray(c, 'RGBA'), (max(fl.offset_left * 2, 0), max(fl.offset_top * 2, 0)))
        out.append(full)
    return out


def crop_all(rows):
    """Crop every cell to the union bounding box of all cells (+ margin)."""
    x0 = y0 = 10 ** 9
    x1 = y1 = 0
    for r in rows:
        for c in r:
            bb = c.getbbox()
            if bb:
                x0, y0, x1, y1 = min(x0, bb[0]), min(y0, bb[1]), max(x1, bb[2]), max(y1, bb[3])
    m = 6
    return [[c.crop((max(x0 - m, 0), max(y0 - m, 0), x1 + m, y1 + m)) for c in r] for r in rows]


def sheet(rows, s):
    cw = max(c.width for r in rows for c in r) // s
    ch = max(c.height for r in rows for c in r) // s
    img = Image.new('RGBA', (len(rows[0]) * (cw + 2), len(rows) * (ch + 2)), (40, 40, 40, 255))
    for i, r in enumerate(rows):
        for j, c in enumerate(r):
            cc = c.resize((c.width // s, c.height // s), Image.LANCZOS) if s > 1 else c
            bg = Image.new('RGBA', (cw, ch), BG)
            bg.alpha_composite(cc, ((cw - cc.width) // 2, (ch - cc.height) // 2))
            img.alpha_composite(bg, (j * (cw + 2), i * (ch + 2)))
    return img


if __name__ == '__main__':
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    opts = dict(a[2:].split('=', 1) for a in sys.argv[1:] if a.startswith('--') and '=' in a)
    out = os.path.join(CWD, args[0])
    units = args[1:]
    action = opts.get('action', 'DEFAULT')
    frame = int(opts.get('frame', 0))
    dirs = [int(v) for v in opts.get('dirs', '0,1,2,3,4,5,6,7').split(',')]
    zoom = int(opts.get('zoom', 2))
    rows = crop_all([cells(u, action, frame, dirs) for u in units])
    two = sheet(rows, 1)
    one = sheet(rows, 2)
    w = max(two.width, one.width)
    img = Image.new('RGBA', (w, two.height + one.height + 6), (40, 40, 40, 255))
    img.alpha_composite(two, (0, 0))
    img.alpha_composite(one, (0, two.height + 6))
    if zoom > 1:
        img = img.resize((img.width * zoom, img.height * zoom), Image.NEAREST) if zoom > 1 and img.width < 1400 else img
    img.save(out)
    print(out, img.size)
