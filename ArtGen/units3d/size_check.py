"""How well a render's on-map size matches the original (ArtGen venv).

    python ArtGen/units3d/size_check.py Caravel Caravel_cal Galley Galley_cal

For each render folder in ArtGen/_previews/units3d/, composes the 8
directions (compose.py) and compares the opaque bounding box of the base and
tint layers with the original's (calibration/<Unit>.json per_direction, at
2x): the mean absolute error of the four box edges, and the mean width and
height ratios (render / original). Numbers only, so the result can be shared.
"""
import json
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import compose  # noqa: E402

PREVIEWS = os.path.join(HERE, '..', '_previews', 'units3d')


def check(folder_name):
    unit = folder_name.split('_')[0]
    cal = json.load(open(os.path.join(HERE, 'calibration', f'{unit}.json'), encoding='utf-8'))
    folder = os.path.join(PREVIEWS, folder_name)
    errs, wr, hr = [], [], []
    for d in cal['per_direction']:
        if d is None:
            continue
        base, tint = compose.compose(folder, d['direction'])
        # the unit itself, not its ground shadow: opaque enough in either layer
        a = np.maximum(base[..., 3] * (base[..., :3].max(-1) > 0), tint[..., 3])
        ys, xs = np.nonzero(a > 128)
        if not len(xs):
            continue
        rx0, ry0, rx1, ry1 = xs.min(), ys.min(), xs.max() + 1, ys.max() + 1
        ox0, oy0, ox1, oy1 = (2 * v for v in d['bbox'])
        ox1, oy1 = ox1 + 2, oy1 + 2
        errs.append(np.mean(np.abs([rx0 - ox0, ry0 - oy0, rx1 - ox1, ry1 - oy1])))
        wr.append((rx1 - rx0) / (ox1 - ox0))
        hr.append((ry1 - ry0) / (oy1 - oy0))
    fit = json.load(open(os.path.join(folder, 'fit.json'), encoding='utf-8'))
    print(f'{folder_name:14s} ppm {fit["ppm_2x"]:6.2f}  edge error {np.mean(errs):5.1f} px (2x)  '
          f'width x{np.mean(wr):.3f}  height x{np.mean(hr):.3f}')


if __name__ == '__main__':
    for name in sys.argv[1:]:
        check(name)
