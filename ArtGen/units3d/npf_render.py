"""Renders one numpy-foot unit's still in the 8 directions (Blender side), as render_unit.py does for the
catalog's foot units, from npf_units.ENTRIES (these units have no old-catalog entry on this branch):

    blender -b --factory-startup --python-exit-code 1 --python ArtGen/units3d/npf_render.py -- \
        <Unit> <out folder> [--samples N] [--rows 0,1,...] [--zoom Z --ay Y]

UNIT_OVERRIDE (JSON) merges keys over the entry, for variant renders.
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
    import npf_units
    npf_units.register()
    print('device', B.use_gpu(scene))
    scene.cycles.samples = int(opts.get('--samples', 128))
    B.lights()
    B.ground()
    B.tint_aov()
    entry = npf_units.entry(unit)
    if os.environ.get('UNIT_OVERRIDE'):
        entry.update(json.loads(os.environ['UNIT_OVERRIDE']))
    root = npf_units.build(entry)
    k = entry.get('unit_scale', 1.0)
    if k != 1.0:
        root.scale = tuple(v * k for v in root.scale)
    cal = json.load(open(os.path.join(HERE, 'calibration', f'{unit}.json')))
    ppm = cal['fit']['ppm_2x'] * entry.get('fit_scale', 1.0)
    if '--zoom' in opts:
        B.camera(480, 480, ppm * float(opts['--zoom']), (240, float(opts.get('--ay', 445))))
    else:
        B.camera(480, 480, ppm, tuple(c * 2 for c in cal['anchor']))
    B.outputs(out)
    rows = [int(r) for r in opts.get('--rows', '0,1,2,3,4,5,6,7').split(',')]
    B.render_directions(root, rows=rows)


if __name__ == '__main__':
    main(sys.argv[sys.argv.index('--') + 1:])
