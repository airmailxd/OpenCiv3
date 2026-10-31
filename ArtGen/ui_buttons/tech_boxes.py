"""The boxes of the science advisor's tree for standalone mode, every size in
every era.

    python ArtGen/ui_buttons/build.py   (with the buttons; or tech_boxes.py alone)

writes C7/ModernArt/Art/Advisors/TechBoxesAllSizes.png, laid out like Civ3's
Art/Advisors/techboxes.pcx: a column per state (known, in progress, possible,
blocked), a block of rows per era, and in each block the small, medium, large
and long boxes, at the places and sizes of Civ3's, with twice the pixels like
all of ModernArt.

The standalone asset pack's techboxes.png (C7/Assets/Art/Advisors) only has
the small boxes of the Ancient era; the rest of it is blank. So an advance
with more than one thing to show (a medium, large or long box) had no box at
all in standalone mode, only its name and icons on the parchment, and its
"not required" sign floated where the box's corner should have been (owner's
report, 2026-10-31). The bigger boxes here are the pack's small boxes made
wider and taller by repeating, mirrored, a strip of the parchment part of
the box, so the frame, the title bar and the colored column stay as they are.
The pack has one set of colors, so every era gets the same boxes.
C7/Lua/standalone/textures.lua points the tech boxes at the sheet.

(Not named techboxes.png: ModernGraphics finds remade art ignoring case, and
would then show this in place of Civ3's own boxes with the overhaul on.)
"""
import os
import sys

from PIL import Image

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
PACK = os.path.join(REPO, 'C7', 'Assets', 'Art', 'Advisors', 'techboxes.png')
OUT = os.path.join(REPO, 'C7', 'ModernArt', 'Art', 'Advisors', 'TechBoxesAllSizes.png')

SCALE = 2                          # ModernArt has twice the pixels
SHEET = (1000, 1483)               # Civ3's techboxes.pcx
STATE_X = (0, 189, 378, 567)       # known, in progress, possible, blocked
ERA_Y = (0, 356, 712, 1068)        # ancient, middle, industrial, modern

# Where each size's crop region starts (C7/Lua/civ3/textures/tech_boxes.lua),
# and the box inside it, as Civ3's Ancient boxes are: left, top, right, bottom.
CROP = {'small': (1, 1), 'medium': (1, 84), 'large': (1, 167), 'long': (1, 274)}
BOX = {
    'small': (2, 8, 100, 72),
    'medium': (0, 4, 159, 74),
    'large': (0, 3, 161, 104),
    'long': (0, 4, 188, 74),
}

# In the pack's small box (98x64), the columns and rows that are parchment
# from the title bar or the colored column down or across to the frame, which
# are repeated to make it bigger.
STRETCH_X = (60, 88)
STRETCH_Y = (38, 56)


def fill(strip, length, axis):
    """The strip repeated along an axis to a length, every other copy
    mirrored so the copies join without a seam."""
    w, h = strip.size
    size = (length, h) if axis == 0 else (w, length)
    out = Image.new('RGBA', size, (0, 0, 0, 0))
    flipped = strip.transpose(Image.FLIP_LEFT_RIGHT if axis == 0 else Image.FLIP_TOP_BOTTOM)
    step = w if axis == 0 else h
    pos, i = 0, 0
    while pos < length:
        out.paste(strip if i % 2 == 0 else flipped, (pos, 0) if axis == 0 else (0, pos))
        pos += step
        i += 1
    return out


def stretch(box, width, height, axis):
    """The box made longer along one axis, through its stretch strip."""
    w, h = box.size
    a, b = STRETCH_X if axis == 0 else STRETCH_Y
    length = width if axis == 0 else height
    if axis == 0:
        head, strip, tail = box.crop((0, 0, a, h)), box.crop((a, 0, b, h)), box.crop((b, 0, w, h))
        out = Image.new('RGBA', (width, h), (0, 0, 0, 0))
        out.paste(head, (0, 0))
        out.paste(fill(strip, length - a - (w - b), 0), (a, 0))
        out.paste(tail, (length - (w - b), 0))
    else:
        head, strip, tail = box.crop((0, 0, w, a)), box.crop((0, a, w, b)), box.crop((0, b, w, h))
        out = Image.new('RGBA', (w, height), (0, 0, 0, 0))
        out.paste(head, (0, 0))
        out.paste(fill(strip, length - a - (h - b), 1), (0, a))
        out.paste(tail, (0, length - (h - b)))
    return out


def main(pack=PACK):
    if not os.path.exists(pack):
        print('skipped the tech boxes: no asset pack at', pack)
        return
    src = Image.open(pack).convert('RGBA')
    sheet = Image.new('RGBA', SHEET, (0, 0, 0, 0))
    cx, cy = CROP['small']
    l, t, r, b = BOX['small']
    for sx in STATE_X:
        small = src.crop((sx + cx + l, cy + t, sx + cx + r, cy + b))
        for size, (cx2, cy2) in CROP.items():
            l2, t2, r2, b2 = BOX[size]
            box = stretch(stretch(small, r2 - l2, small.height, 0), r2 - l2, b2 - t2, 1)
            for ey in ERA_Y:
                sheet.paste(box, (sx + cx2 + l2, ey + cy2 + t2))
    sheet = sheet.resize((SHEET[0] * SCALE, SHEET[1] * SCALE), Image.NEAREST)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    sheet.save(OUT, optimize=True)
    print('wrote', OUT)


if __name__ == '__main__':
    # The asset pack's techboxes.png can be given, for a checkout without
    # the C7/Assets submodule.
    main(*sys.argv[1:2])
