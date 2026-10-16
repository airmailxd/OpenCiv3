"""Contact sheet of riders.HEADGEAR (each hat on a plain head-sized sphere).

    blender -b --factory-startup --python-exit-code 1 --python ArtGen/units3d/hats_preview.py -- out.png [kind ...]
"""
import math
import os
import sys

import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import bscene  # noqa: E402
import props  # noqa: E402
import riders  # noqa: E402

CELL = 256
CIV = (0.24, 0.43, 0.92)


def main():
    argv = sys.argv[sys.argv.index('--') + 1:]
    out = os.path.abspath(argv[0])
    kinds = argv[1:] or sorted(riders.HEADGEAR)
    sc = bscene.reset_scene()
    bscene.use_gpu(sc)
    bscene.lights()
    bscene.ground()
    tmp = os.path.join(os.path.dirname(out), '_hat_cells')
    os.makedirs(tmp, exist_ok=True)
    paths = []
    for kind in kinds:
        head = bpy.data.objects.new('head', bpy.data.meshes.new('head'))
        bm = props.ellipsoid(0.1, 0.11, 0.13, seg=20)
        bm.to_mesh(head.data)
        bm.free()
        head.data.materials.append(props.material('hide', {'hide': '#b08a68'}))
        bpy.context.collection.objects.link(head)
        head.location = (0, 0, 0.3)
        # 'kind:key=val,key=val' passes options to hats that take them
        name, _, opts = kind.partition(':')
        params = {k: (v if v not in ('True', 'False') else v == 'True')
                  for k, v in (o.split('=') for o in opts.split(',') if o)}
        p = props.Prop(name, {})
        riders.HEADGEAR[name](p, {}, True, True, **params)
        hat = p.finish()
        hat.location = (0, 0, 0.36)
        for m in hat.data.materials:
            if m.name.endswith('_tint'):
                props._principled(m).inputs['Base Color'].default_value = (*CIV, 1)
        for o in [o for o in bpy.data.objects if o.type == 'CAMERA']:
            bpy.data.objects.remove(o, do_unlink=True)
        bscene.camera(CELL, CELL, 380.0, (CELL / 2, CELL / 2 + 0.42 * 380.0 * math.cos(math.radians(30))))
        sc.render.filepath = os.path.join(tmp, f"{kind.replace(':', '_').replace('=', '-').replace(',', '_')}.png")
        bpy.ops.render.render(write_still=True)
        paths.append(sc.render.filepath)
        bpy.data.objects.remove(hat, do_unlink=True)
        bpy.data.objects.remove(head, do_unlink=True)
    bg = np.array([96 / 255, 132 / 255, 62 / 255, 1.0], np.float32)
    canvas = np.tile(bg, (CELL, CELL * len(paths), 1))
    for i, path in enumerate(paths):
        img = bpy.data.images.load(path)
        px = np.array(img.pixels[:], np.float32).reshape(CELL, CELL, 4)
        a = px[..., 3:4]
        canvas[:, i * CELL:(i + 1) * CELL] = np.concatenate([px[..., :3] * a + bg[:3] * (1 - a), np.ones_like(a)], -1)
        bpy.data.images.remove(img)
    img = bpy.data.images.new('hats', CELL * len(paths), CELL, alpha=True)
    img.pixels = canvas.ravel()
    img.filepath_raw = out
    img.file_format = 'PNG'
    img.save()
    print('HATS', out)


if __name__ == '__main__':
    main()
