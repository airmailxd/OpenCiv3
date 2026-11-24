"""Renders a numpy-foot unit's animated actions (Blender side), as render_anim.py does for the catalog's foot
units, from npf_units.ENTRIES (these units have no old-catalog entry):

    blender -b --factory-startup --python-exit-code 1 --python ArtGen/units3d/npf_render_anim.py -- \
        <Unit> <out folder> [--actions a,b] [--frames 0,5] [--rows 0,...] [--samples N] [--list]

The actions are anim.py's: anim.unit_actions picks the templates of the entry's weapon class (by its stance,
as for every foot unit; the npf firearms, NPF_GUNS, take the gun templates). The human is built as npf_render.py builds the still: npf_units' flat bands and its
limb armour, its unit_scale, and the camera at the calibration's ppm times the entry's fit_scale.
A team entry (the Battalion) has no animation path: it exits with code 3. --list prints the actions and exits.
Writes <out>/<action>/f<frame:03d>/ (the passes compose.py reads), as render_anim.py does.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import bscene as B  # noqa: E402

NPF_GUNS = ('rpg', 'carbine', 'pkm', 'ak', 'smg', 'shotgun', 'm14', 'musket', 'flame', 'pulse')


def main(argv):
    unit, out = argv[0], argv[1]
    flags = [a for a in argv[2:] if a == '--list']
    rest = [a for a in argv[2:] if a != '--list']
    opts = dict(zip(rest[0::2], rest[1::2]))
    scene = B.reset_scene()
    import addon_utils
    addon_utils.enable('bl_ext.user_default.mpfb', default_set=True)
    import bpy
    import anim
    import garments as G
    import npf_units
    npf_units.register()
    entry = npf_units.entry(unit)
    if entry.get('team'):
        print(f'{unit}: a team entry, no animation path')
        sys.exit(3)
    cal = json.load(open(os.path.join(HERE, 'calibration', f'{unit}.json')))
    # every npf weapon is a firearm under its own prop name (rpg, ak, pkm, ...), which anim.weapon_class doesn't
    # know as a gun (units.GUNS): here it takes the gun templates (a melee swing of a rifle, or no attack at all, otherwise)
    _class = anim.weapon_class
    anim.weapon_class = lambda e: 'gun' if e.get('weapon') in NPF_GUNS else _class(e)
    actions, own = anim.unit_actions(unit, entry, cal, HERE)
    if flags:
        print('ACTIONS ' + json.dumps({k: v['frames'] for k, v in actions.items()}))
        bpy.ops.wm.quit_blender()
        return
    if not actions:
        raise SystemExit(f'{unit}: no animated actions')
    print('device', B.use_gpu(scene))
    scene.cycles.samples = int(opts.get('--samples', 128))
    B.lights()
    B.ground()
    B.tint_aov()
    orig = G.band

    def flat(*a, **k):   # npf_units.build's flat bands
        k.setdefault('offset', npf_units.FLAT_BAND)
        return orig(*a, **k)
    G.band = flat
    try:
        root, ctx = anim.setup(entry, own_gear=own)
        if entry.get('npf_armour'):
            npf_units._armour(entry, root)
    finally:
        G.band = orig
    k = entry.get('unit_scale', 1.0)
    if k != 1.0:
        root.scale = tuple(v * k for v in root.scale)
    B.camera(480, 480, cal['fit']['ppm_2x'] * entry.get('fit_scale', 1.0), tuple(c * 2 for c in cal['anchor']))
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
