"""Contact sheets of a unit's original flics, for studying their motion.

    python inspect_original.py Warrior [direction-row=2] [scale=3]

Writes ArtGen/_previews/units/orig_<unit>_d<row>.png: one strip per flic
(every frame of the chosen direction), each frame shown in its 240x240 placement.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

import flic
import civ3art

BG = (92, 128, 60, 255)


def frame_in_240(f, d, fr, civ=(60, 110, 230)):
    im = np.zeros((240, 240, 4), np.uint8)
    c = flic.composite(f.frames[d, fr], f.palette, civ)
    im[f.offset_top:f.offset_top + f.height, f.offset_left:f.offset_left + f.width] = c
    return im


def strip(f, d, crop=(84, 70, 156, 136)):
    x0, y0, x1, y1 = crop
    frames = [frame_in_240(f, d, fr)[y0:y1, x0:x1] for fr in range(f.n_frames)]
    return np.concatenate(frames, 1)


def main(unit, d=2, scale=3):
    rows = []
    names = []
    for action, name in flic.unit_flics(unit).items():
        f = flic.load_unit_flic(unit, name)
        rows.append(strip(f, d))
        names.append(f'{action} {name} {f.n_frames}f {f.speed}ms')
    w = max(r.shape[1] for r in rows)
    h = sum(r.shape[0] + 12 for r in rows)
    out = Image.new('RGBA', (w * scale, h * scale), BG)
    dr = ImageDraw.Draw(out)
    y = 0
    for r, n in zip(rows, names):
        dr.text((2, y * scale), n, fill=(255, 255, 255, 255))
        y += 12
        im = Image.fromarray(r).resize((r.shape[1] * scale, r.shape[0] * scale), Image.NEAREST)
        out.alpha_composite(im, (0, y * scale))
        y += r.shape[0]
    p = os.path.join(civ3art.PREVIEWS, 'units', f'orig_{unit}_d{d}.png')
    os.makedirs(os.path.dirname(p), exist_ok=True)
    out.save(p)
    print(p)


if __name__ == '__main__':
    main(sys.argv[1], int(sys.argv[2]) if len(sys.argv) > 2 else 2, int(sys.argv[3]) if len(sys.argv) > 3 else 2)
