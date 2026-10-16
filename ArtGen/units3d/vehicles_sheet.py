"""Comparison sheet for the Blender vehicle remakes (runs in the ArtGen venv).

    python ArtGen/units3d/vehicles_sheet.py Catapult Cannon Galley Caravel

For each unit, three rows of its 8 directions at 2x: the original Civ3 art
(DEFAULT frame 0), the current software remake and the new Blender remake
(render_vehicles.py's passes through compose.py), all cut to the DEFAULT
flic's cell, tinted with a civ blue, over grass. Writes
ArtGen/_previews/units3d/vehicles_sheet.png (gitignored: it shows Civ3's
copyrighted art, so it stays local).
"""
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'units'))
sys.path.insert(0, HERE)

import flic as F  # noqa: E402  (first: puts ArtGen/common on the path)
import civ3art  # noqa: E402
import catalog  # noqa: E402
import framework  # noqa: E402
import preview as P  # noqa: E402
import compose  # noqa: E402

LABEL_W = 80


def unit_rows(unit):
    job = framework.UnitJob(unit, catalog.load()[unit])
    name = job.flics()['DEFAULT']
    fl = F.load_unit_flic(unit, name)
    base, tint, _ = job.render_flic('DEFAULT', name, cols=[0])
    W, H = fl.width * 2, fl.height * 2
    x0, y0 = fl.offset_left * 2, fl.offset_top * 2
    folder = os.path.join(HERE, '..', '_previews', 'units3d', unit)
    orig, old, new = [], [], []
    for d in range(8):
        orig.append(P.on_bg(civ3art.upscale_nearest(P.original_frame(fl, d, 0))))
        old.append(P.on_bg(P.tinted(base[d * H:(d + 1) * H, 0:W], tint[d * H:(d + 1) * H, 0:W])))
        if not os.path.exists(os.path.join(folder, 'unit_0000.png')):   # not rendered yet: a blank cell
            new.append(Image.new('RGBA', (W, H), (60, 60, 60, 255)))
            continue
        b, t = compose.compose(folder, d)
        new.append(P.on_bg(P.tinted(b[y0:y0 + H, x0:x0 + W], t[y0:y0 + H, x0:x0 + W])))
    return W, H, [('original', orig), ('current', old), ('Blender', new)]


def main(units):
    blocks = []
    for u in units:
        W, H, rows = unit_rows(u)
        img = Image.new('RGBA', (LABEL_W + 8 * (W + 4), 3 * (H + 4) + 16), (40, 40, 40, 255))
        dr = ImageDraw.Draw(img)
        dr.text((2, 2), u, fill=(255, 255, 255, 255))
        for r, (label, cells) in enumerate(rows):
            y = 16 + r * (H + 4)
            dr.text((2, y + H // 2), label, fill=(255, 255, 255, 255))
            for d, c in enumerate(cells):
                img.alpha_composite(c, (LABEL_W + d * (W + 4), y))
        blocks.append(img)
    w = max(b.width for b in blocks)
    h = sum(b.height + 6 for b in blocks)
    sheet = Image.new('RGBA', (w, h), (40, 40, 40, 255))
    y = 0
    for b in blocks:
        sheet.alpha_composite(b, (0, y))
        y += b.height + 6
    out = os.path.join(HERE, '..', '_previews', 'units3d', 'vehicles_sheet.png')
    sheet.save(out)
    print(out, sheet.size)


if __name__ == '__main__':
    main(sys.argv[1:])
