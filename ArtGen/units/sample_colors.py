"""Dominant colors of a unit's original art, for picking catalog colors.

    python sample_colors.py Warrior [k=8]

Prints the k main colors (k-means in RGB) of the non-civ-colored pixels of the
unit's default flic, with their share, and the mean shade of the civ-colored
pixels. Also writes ArtGen/_previews/units/colors_<unit>.png (swatches).
"""
import os
import sys

import numpy as np
from PIL import Image

import flic  # noqa: F401  (puts ArtGen/common on the path)
import civ3art


def dominant(unit, k=8, iters=20):
    name = flic.unit_flics(unit)['DEFAULT']
    f = flic.load_unit_flic(unit, name)
    px = []
    tint_lum = []
    for d in range(f.n_anims):
        base, tint, sh = f.layers(d, 0)
        px.append(base[base[..., 3] > 0][:, :3])
        tint_lum.append(f.palette[f.frames[d, 0][tint]].mean(-1))
    X = np.concatenate(px).astype(np.float32)
    rng = np.random.default_rng(0)
    C = X[rng.choice(len(X), k, replace=False)]
    for _ in range(iters):
        lab = ((X[:, None] - C[None]) ** 2).sum(-1).argmin(1)
        for j in range(k):
            if (lab == j).any():
                C[j] = X[lab == j].mean(0)
    share = np.bincount(lab, minlength=k) / len(X)
    order = np.argsort(-share)
    return [(tuple(int(v) for v in C[j]), float(share[j])) for j in order], float(np.concatenate(tint_lum).mean())


if __name__ == '__main__':
    unit = sys.argv[1]
    k = int(sys.argv[2]) if len(sys.argv) > 2 else 8
    cols, tl = dominant(unit, k)
    for c, s in cols:
        print('#%02x%02x%02x  %4.1f%%' % (*c, s * 100))
    print('civ-colored pixels mean shade: %.0f' % tl)
    sw = np.zeros((40, 40 * len(cols), 3), np.uint8)
    for i, (c, s) in enumerate(cols):
        sw[:, i * 40:(i + 1) * 40] = c
    p = os.path.join(civ3art.PREVIEWS, 'units', f'colors_{unit}.png')
    os.makedirs(os.path.dirname(p), exist_ok=True)
    Image.fromarray(sw).save(p)
