"""The symbols of MDJ's clans (C7/Lua/mdj, registered with civs.symbol):
each clan's mon, traced from its banner in the Sengoku conquest's leader
heads as ArtGen/leaderheads/mdj_mon.py traces it (every region of the banner
but its cloth), and centered on the shield."""
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

import civs
from civs import symbol, N

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'leaderheads'))
import mdj_mon  # noqa: E402

# The square of the shield the mon is fitted into (the shield's inner field).
BOX = (56, 44, 184, 172)


def mon_mask(entry, rel):
    rgb, mask = mdj_mon.original(rel)
    den = ndimage.gaussian_filter(rgb, (0.5, 0.5, 0))
    if entry in mdj_mon.WHITE_ON_GRAY:
        labels, colors = mdj_mon.white_on_gray(den, mask, *mdj_mon.WHITE_ON_GRAY[entry])
    else:
        feats = mdj_mon.features(den, mask, mdj_mon.DEFAULT_WEIGHTS)
        labels, colors = mdj_mon.regions(feats, den, mask, mdj_mon.COLORS.get(entry, mdj_mon.DEFAULT_COLORS))
    # The cloth is the darkest region (every banner's is dark gray), and any
    # region of nearly its color (its folds' lit side).
    dark = colors[int(np.argmin([float(np.mean(c)) for c in colors]))]
    cloth = [i for i, c in enumerate(colors) if np.linalg.norm(np.asarray(c) - dark) < 0.18]
    mon = (labels >= 0) & ~np.isin(labels, cloth)
    # Smooth outlines at the shield's size.
    x0, y0, x1, y1 = BOX
    m = ndimage.gaussian_filter(mon.astype(np.float32), 0.6)
    m = np.asarray(Image.fromarray(m).resize((x1 - x0, y1 - y0), Image.BICUBIC)) > 0.5
    out = np.zeros((N, N), bool)
    out[y0:y1, x0:x1] = m
    return out


def _register():
    for entry, rel in mdj_mon.clans():
        symbol(entry)(lambda entry=entry, rel=rel: mon_mask(entry, rel))


_register()
