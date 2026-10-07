"""Rearranges unit sheets built before the core/animations split (see
framework.save_sheets): moves full sheets that aren't worker loops to
C7/ModernArtAnimations, and writes the one-column stills of standing/fortified
flics into C7/ModernArt. Safe to run again."""
import os
import shutil
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import flic as F  # noqa: E402
import civ3art  # noqa: E402


def find_sheet(unit, file):
    """The sheet, in ModernArt or (already moved) ModernArtAnimations."""
    for root in (civ3art.MODERN_ART, civ3art.MODERN_ART_ANIMATIONS):
        p = os.path.join(root, 'Art', 'Units', unit, file)
        if os.path.exists(p):
            return p
    return None


moved = stills = 0
roots = [os.path.join(r, 'Art', 'Units') for r in (civ3art.MODERN_ART, civ3art.MODERN_ART_ANIMATIONS)]
units = sorted({u for r in roots if os.path.isdir(r) for u in os.listdir(r)})
for unit in units:
    flics = F.unit_flics(unit)
    files = sorted({f for r in roots if os.path.isdir(os.path.join(r, unit)) for f in os.listdir(os.path.join(r, unit))})
    for file in files:
        stem = os.path.splitext(file)[0]
        stem = stem[:-5] if stem.endswith('_tint') else stem
        name = next((n for n in flics.values() if os.path.splitext(os.path.basename(n))[0].lower() == stem.lower()), None)
        if name is None:
            continue
        kind = F.flic_kind(unit, name)
        core = os.path.join(civ3art.MODERN_ART, 'Art', 'Units', unit, file)
        full = os.path.join(civ3art.MODERN_ART_ANIMATIONS, 'Art', 'Units', unit, file)
        fl = F.load_unit_flic(unit, name)
        W = fl.width * civ3art.SCALE
        if kind == 'loop':
            if not os.path.exists(core) and os.path.exists(full):
                os.makedirs(os.path.dirname(core), exist_ok=True)
                shutil.move(full, core)
                moved += 1
            continue
        # Make sure the full sheet is in ModernArtAnimations.
        if os.path.exists(core) and Image.open(core).size[0] > W:
            os.makedirs(os.path.dirname(full), exist_ok=True)
            shutil.move(core, full)
            moved += 1
        if kind == 'still' and os.path.exists(full) and not os.path.exists(core):
            sheet = np.asarray(Image.open(full).convert('RGBA'))
            os.makedirs(os.path.dirname(core), exist_ok=True)
            Image.fromarray(np.ascontiguousarray(sheet[:, -W:]), 'RGBA').save(core, compress_level=9)
            stills += 1
print(f'moved {moved} sheets, wrote {stills} stills')
