"""Calibration numbers from the original Civ3 unit art, for the Blender remaster.

    python ArtGen/units3d/calibration/calibrate.py Warrior Spearman ...

Writes ArtGen/units3d/calibration/<Unit>.json with numbers only (no pixels,
since Civ3's art is copyrighted and stays out of git):

- anchor: the ground anchor of the original DEFAULT frame 0, the median foot
  point over the directions, in the game's 240x240 unit space at 1x
  (framework.original_stance);
- standing_height: the original's standing height in 1x pixels (same source);
- per_direction: each direction's foot point, top and opaque bounding box at 1x
  in unit space, so a renderer can check its silhouette direction by direction;
- fit: what the current procedural remake fitted (framework.UnitJob): its
  pixels per meter at 2x, the fit factor against BASE_PPM and the catalog scale;
- flics: each flic of the unit's INI with its header (frame size, offset,
  original size, directions, frames, ms per frame), the actions that play
  it and its kind (still / move / combat / loop / animation).
"""
import json
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'units'))

import flic as F  # noqa: E402  (first: puts ArtGen/common on the path)
import civ3art  # noqa: E402
import catalog  # noqa: E402
import framework  # noqa: E402


def direction_numbers(fl):
    """Foot point, top and bounding box of frame 0 per direction (1x, unit space)."""
    out = []
    for d in range(fl.n_anims):
        idx = fl.frames[d, 0]
        m = ~F.shadow_mask(idx) & (idx != 255)
        ys, xs = np.nonzero(m)
        if len(ys) == 0:
            out.append(None)
            continue
        by = int(ys.max())
        out.append({
            'direction': d,
            'foot': [round(float(xs[ys >= by - 3].mean()) + fl.offset_left, 2), by + fl.offset_top],
            'top': int(ys.min()) + fl.offset_top,
            'height': by - int(ys.min()) + 1,
            'bbox': [int(xs.min()) + fl.offset_left, int(ys.min()) + fl.offset_top,
                     int(xs.max()) + fl.offset_left, by + fl.offset_top],
        })
    return out


def calibrate(unit):
    entry = catalog.load()[unit]
    ax, ay, oh = framework.original_stance(unit)
    job = framework.UnitJob(unit, entry)
    flics = framework.F.unit_flics(unit)
    default = F.load_unit_flic(unit, flics['DEFAULT'])
    headers = {}
    for name in sorted(set(flics.values()), key=str.lower):
        fl = F.load_unit_flic(unit, name)
        headers[name] = {
            'actions': sorted(F.flic_actions(unit, name)),
            'kind': F.flic_kind(unit, name),
            'width': fl.width, 'height': fl.height,
            'offset_left': fl.offset_left, 'offset_top': fl.offset_top,
            'orig_width': fl.orig_width, 'orig_height': fl.orig_height,
            'directions': fl.n_anims, 'frames': fl.n_frames, 'frameMs': fl.speed,
        }
    return {
        'unit': unit,
        'source': 'Civ3 Complete original art, DEFAULT flic frame 0 (numbers only)',
        'unit_space': [240, 240],
        'scale': civ3art.SCALE,
        'anchor': [round(ax, 2), round(ay, 2)],
        'anchor_override': entry.get('anchor'),
        'standing_height': oh,
        'per_direction': direction_numbers(default),
        'fit': {
            'base_ppm': framework.BASE_PPM,
            'catalog_scale': entry.get('scale', 1.0),
            'ppm_2x': round(job.ppm, 4),
            'fit': round(job.ppm / (framework.BASE_PPM * civ3art.SCALE) / entry.get('scale', 1.0), 4),
            'archetype': entry.get('archetype'),
        },
        'default_flic': flics['DEFAULT'],
        'flics': headers,
    }


def main(units):
    for unit in units:
        data = calibrate(unit)
        path = os.path.join(HERE, f'{unit}.json')
        with open(path, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=2)
            f.write('\n')
        print(path, 'anchor', data['anchor'], 'height', data['standing_height'], 'flics', len(data['flics']))


if __name__ == '__main__':
    main(sys.argv[1:])
