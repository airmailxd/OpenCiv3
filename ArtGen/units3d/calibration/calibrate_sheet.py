"""Calibration numbers from an installed sheet (for units with no Civ3 FLC:
the NumPy-made League / MDJ units remade in units3d).

    python ArtGen/units3d/calibration/calibrate_sheet.py <unit_dir> [...]

<unit_dir> holds <Unit>.json and its flic PNGs (2x sheets, one column per
frame, 8 direction rows). Writes ArtGen/units3d/calibration/<Unit>.json in
calibrate.py's format: per_direction from the DEFAULT sheet's frame 0
(opaque = alpha > 127, so the soft alpha-102 shadow is left out, as the FLC
shadow indices are), the anchor as the median foot point, and each flic's
header from the metadata (the flic names stay the sheet's own).
"""
import json
import os
import statistics
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SCALE = 2


def calibrate(unit_dir):
    unit = os.path.basename(os.path.normpath(unit_dir))
    meta = json.load(open(os.path.join(unit_dir, f'{unit}.json'), encoding='utf-8'))
    dflt = meta['actions']['DEFAULT']
    h = meta['flics'][dflt]
    (w, hh), (ol, ot) = h['size'], h['offset']
    a = np.asarray(Image.open(os.path.join(unit_dir, f'{dflt}.png')).convert('RGBA'))[..., 3]
    tp = os.path.join(unit_dir, f'{dflt}_tint.png')
    if os.path.exists(tp):
        a = np.maximum(a, np.asarray(Image.open(tp).convert('RGBA'))[..., 3])
    per = []
    for d in range(h['directions']):
        m = a[d * hh * SCALE:(d + 1) * hh * SCALE, :w * SCALE] > 127
        ys, xs = np.nonzero(m)
        ys, xs = ys / SCALE, xs / SCALE
        by = int(ys.max())
        per.append({
            'direction': d,
            'foot': [round(float(xs[ys >= by - 3].mean()) + ol, 2), by + ot],
            'top': int(ys.min()) + ot,
            'height': by - int(ys.min()) + 1,
            'bbox': [int(xs.min()) + ol, int(ys.min()) + ot, int(xs.max()) + ol, by + ot],
        })
    acts = {}
    for k, v in meta['actions'].items():
        acts.setdefault(v, []).append(k)
    flics = {}
    for name, f in meta['flics'].items():
        flics[name + '.flc'] = {
            'actions': sorted(acts.get(name, [])), 'kind': 'still' if f['frames'] == 1 else 'combat',
            'width': f['size'][0], 'height': f['size'][1], 'offset_left': f['offset'][0], 'offset_top': f['offset'][1],
            'orig_width': f['canvas'][0], 'orig_height': f['canvas'][1],
            'directions': f['directions'], 'frames': f['frames'], 'frameMs': f['frameMs'],
        }
    ax = statistics.median(p['foot'][0] for p in per)
    ay = statistics.median(p['foot'][1] for p in per)
    return {
        'unit': unit,
        'source': 'installed NumPy sheet (art-base), DEFAULT frame 0 (numbers only)',
        'unit_space': [240, 240],
        'scale': SCALE,
        'anchor': [round(ax, 2), round(ay, 2)],
        'anchor_override': None,
        'standing_height': statistics.median(p['height'] for p in per),
        'per_direction': per,
        'fit': {'ppm_2x': None},
        'default_flic': dflt + '.flc',
        'flics': flics,
    }


def main(dirs):
    for d in dirs:
        data = calibrate(d)
        path = os.path.join(HERE, f"{data['unit']}.json")
        with open(path, 'w', encoding='utf-8', newline='\n') as f:
            json.dump(data, f, indent=2)
            f.write('\n')
        print(path, 'anchor', data['anchor'], 'height', data['standing_height'], 'flics', len(data['flics']))


if __name__ == '__main__':
    main(sys.argv[1:])
