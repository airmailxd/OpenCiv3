"""Renders a foot unit's animated actions (Blender side):

    blender -b --factory-startup --python-exit-code 1 --python ArtGen/units3d/render_anim.py -- \
        <Unit> <out folder> [--actions workerMine,workerRun] [--frames 0,5] [--rows 0,...] [--samples N]

Builds the unit once (as render_unit.py, without its weapon) and, for each
action of anim.ACTIONS[<Unit>], or of the templates for its weapon class
(anim.template_actions), all by default,, poses every frame and renders
it in the 8 directions into <out>/<action>/f<frame:03d>/ (the passes
compose.py reads).
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
    import bpy
    import anim
    import remaster
    print('device', B.use_gpu(scene))
    scene.cycles.samples = int(opts.get('--samples', 128))
    B.lights()
    B.ground()
    B.tint_aov()
    catalog = json.load(open(os.path.join(HERE, '..', '_previews', 'units3d', 'catalog.json')))
    entry = remaster.apply(unit, catalog[unit])
    cal = json.load(open(os.path.join(HERE, 'calibration', f'{unit}.json')))
    own = unit not in anim.ACTIONS
    actions = anim.ACTIONS[unit] if not own else anim.template_actions(entry, cal)
    if not actions:
        raise SystemExit(f'{unit}: no animated actions (its weapon class has no templates)')
    root, ctx = anim.setup(entry, own_gear=own)
    k = entry.get('unit_scale', 1.0)
    if k != 1.0:
        root.scale = tuple(v * k for v in root.scale)
    B.camera(480, 480, cal['fit']['ppm_2x'], tuple(c * 2 for c in cal['anchor']))
    B.outputs(out)
    node = next(n for n in scene.node_tree.nodes if n.bl_idname == 'CompositorNodeOutputFile')
    rows = [int(r) for r in opts.get('--rows', '0,1,2,3,4,5,6,7').split(',')]
    names = opts['--actions'].split(',') if '--actions' in opts else list(actions)
    for name in names:
        action = actions[name]
        frames = [int(f) for f in opts['--frames'].split(',')] if '--frames' in opts else range(action['frames'])
        for f in frames:
            anim.apply(ctx, action, f)
            node.base_path = os.path.join(out, name, f'f{f:03d}')
            B.render_directions(root, rows=rows)
            print(f'RENDERED {unit} {name} f{f}', flush=True)
    bpy.ops.wm.quit_blender()


if __name__ == '__main__':
    main(sys.argv[sys.argv.index('--') + 1:])
