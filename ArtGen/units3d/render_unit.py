"""Renders one unit's still in the 8 directions (Blender side):

    blender -b --factory-startup --python-exit-code 1 --python ArtGen/units3d/render_unit.py -- \
        <Unit> <out folder> [--samples N] [--rows 0,1,...] [--zoom Z --ay Y]

Reads the unit's catalog entry (ArtGen/_previews/units3d/catalog.json, from
catalog_json.py) with remaster.py's changes, and its calibration
(calibration/<Unit>.json) for the pixels per meter and the anchor. Writes the
passes compose.py reads.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import bscene as B  # noqa: E402


def main(argv):
    unit, out = argv[0], argv[1]
    opts = dict(zip(argv[2::2], argv[3::2]))
    scene = B.reset_scene()
    import addon_utils
    addon_utils.enable('bl_ext.user_default.mpfb', default_set=True)
    import remaster
    import units
    print('device', B.use_gpu(scene))
    scene.cycles.samples = int(opts.get('--samples', 128))
    B.lights()
    B.ground()
    B.tint_aov()
    catalog = json.load(open(os.path.join(HERE, '..', '_previews', 'units3d', 'catalog.json')))
    entry = remaster.apply(unit, catalog[unit])
    if os.environ.get('UNIT_OVERRIDE'):
        # for tests: keys merged over the entry (JSON; lists stand for tuples)
        entry.update(json.loads(os.environ['UNIT_OVERRIDE']))
    root = units.build_footman(entry)
    k = entry.get('unit_scale', 1.0)           # about the feet (the root's origin is on the ground)
    if k != 1.0:
        root.scale = tuple(v * k for v in root.scale)
    cal = json.load(open(os.path.join(HERE, 'calibration', f'{unit}.json')))
    ppm = cal['fit']['ppm_2x']
    if '--zoom' in opts:
        B.camera(480, 480, ppm * float(opts['--zoom']), (240, float(opts.get('--ay', 445))))
    else:
        B.camera(480, 480, ppm, tuple(c * 2 for c in cal['anchor']))
    B.outputs(out)
    rows = [int(r) for r in opts.get('--rows', '0,1,2,3,4,5,6,7').split(',')]
    B.render_directions(root, rows=rows)


if __name__ == '__main__':
    main(sys.argv[sys.argv.index('--') + 1:])
