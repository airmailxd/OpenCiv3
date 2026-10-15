"""Contact sheet of the props library, close up and at game scale.

    blender -b --factory-startup --python ArtGen/units3d/props_preview.py -- [out.png] [prop ...]

Each prop is rendered twice with Cycles: close up (filling a 320 px cell) and
at game scale (the remake's 2x scale, about 52 px per meter, so a 1.8 m man is
about 94 px tall), both in the game's isometric view. With bscene importable
the camera, lights and shadow catcher are the Mac's; without it a stand-in
(orthographic camera at the game's angle, a northwest sun, a grass ground)
lets the props be checked alone. Writes ArtGen/_previews/units3d/props.png
(gitignored) unless an output path is given.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import props  # noqa: E402

try:
    import bscene  # noqa: E402
except ImportError:
    bscene = None

PPM_2X = 26.0 * 2 * 1.8 / 1.8  # pixels per meter at 2x for a 1.8 m man
CELL = 320
BG = (96, 132, 62)

# name, params, rotation (degrees about X, Y, Z) to show it like the game holds it
SHOWN = [
    ('stone_axe', {}, (0, 0, 0)),
    ('spear', {'length': 2.3, 'grip': 0.95}, (0, 0, 0)),
    ('sword', {'length': 0.82, 'width': 0.085, 'guard': 0.2}, (0, 0, 0)),
    ('bow', {'height': 1.45}, (0, 0, 0)),
    ('arrow', {}, (0, 0, 0)),
    ('quiver', {}, (0, 0, 0)),
    ('staff', {}, (0, 0, 0)),
    ('shovel', {}, (0, 0, 0)),
    ('shield_round', {}, (0, 0, 0)),
    ('shield_oval', {}, (0, 0, 0)),
    ('backpack', {}, (0, 0, 180)),
]

COLORS = {  # catalog colors from the pilot units
    'wood': '#5e4430', 'stone': '#8a8784', 'shield_face': '#c8b894', 'shield_rim': '#5a4028',
    'pack': '#76683e', 'leather': '#5e4430',
}
CIV = (0.24, 0.43, 0.92)  # the civ color shown on tinted parts in the preview


def clear():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)


def setup_scene():
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'GPU'
    prefs = bpy.context.preferences.addons['cycles'].preferences
    for backend in ('OPTIX', 'CUDA'):
        try:
            prefs.compute_device_type = backend
            prefs.get_devices()
            if any(d.type == backend for d in prefs.devices):
                for d in prefs.devices:
                    d.use = d.type == backend
                break
        except TypeError:
            continue
    sc.cycles.samples = 96
    sc.cycles.use_denoising = True
    sc.render.film_transparent = False
    sc.render.resolution_x = sc.render.resolution_y = CELL
    sc.view_settings.view_transform = 'Standard'
    if bscene is not None and hasattr(bscene, 'setup'):
        return bscene.setup(sc)
    # stand-in: Civ3's isometric look, sun from the northwest
    world = bpy.data.worlds.new('w')
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.55, 0.62, 0.72, 1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.6
    sc.world = world
    sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN'))
    sun.data.energy = 3.2
    sun.data.angle = math.radians(8)
    sun.rotation_euler = (math.radians(50), 0, math.radians(135))  # light from the northwest, above
    sc.collection.objects.link(sun)
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
    cam.data.type = 'ORTHO'
    sc.collection.objects.link(cam)
    sc.camera = cam
    ground = bpy.data.meshes.new('ground')
    ground.from_pydata([(-20, -20, 0), (20, -20, 0), (20, 20, 0), (-20, 20, 0)], [], [(0, 1, 2, 3)])
    g = bpy.data.objects.new('ground', ground)
    gm = bpy.data.materials.new('grass')
    gm.use_nodes = True
    gm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (
        *(((c / 255 + 0.055) / 1.055) ** 2.4 for c in BG), 1)
    gm.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = 1.0
    ground.materials.append(gm)
    sc.collection.objects.link(g)
    return cam


def show_tint(obj):
    """Preview only: give tinted materials the civ color so they read like in
    game (the real pipeline composites the tint layer instead)."""
    for m in obj.data.materials:
        if m.name.endswith('_tint'):
            pr = props._principled(m)
            if pr and not pr.inputs['Base Color'].is_linked:
                pr.inputs['Base Color'].default_value = (*CIV, 1)


def frame_camera(cam, obj, ppm=None):
    """Point the isometric camera at the object; ortho scale from ppm, or to
    fit the object when ppm is None."""
    pts = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    center = sum(pts, Vector()) / 8
    elev, az = math.radians(30), math.radians(-45)
    d = Vector((math.cos(elev) * math.sin(az) * -1, -math.cos(elev) * math.cos(az), math.sin(elev)))
    cam.location = center + d * 20
    cam.rotation_euler = (math.radians(90) - elev, 0, -az)
    if ppm:
        cam.data.ortho_scale = CELL / ppm
    else:
        size = max((p - center).length for p in pts) * 2.15
        cam.data.ortho_scale = max(size, 0.3)


def render_cell(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    out = next((a for a in argv if a.endswith('.png')), None) or os.path.join(
        HERE, '..', '_previews', 'units3d', 'props.png')
    names = [a for a in argv if not a.endswith('.png')]
    shown = [s for s in SHOWN if not names or s[0] in names]
    out = os.path.abspath(out)
    tmp = os.path.join(os.path.dirname(out), '_cells')
    os.makedirs(tmp, exist_ok=True)
    clear()
    cam = setup_scene()
    cells = []
    for name, params, rot in shown:
        obj = props.make(name, COLORS, **params)
        obj.rotation_euler = tuple(math.radians(a) for a in rot)
        # stand it on the ground: lowest point at z = 0 (shields and gear float a little)
        bpy.context.view_layer.update()
        zmin = min((obj.matrix_world @ Vector(c)).z for c in obj.bound_box)
        obj.location.z -= zmin - 0.02
        bpy.context.view_layer.update()
        show_tint(obj)
        a = os.path.join(tmp, f'{name}_close.png')
        b = os.path.join(tmp, f'{name}_game.png')
        frame_camera(cam, obj)
        render_cell(a)
        frame_camera(cam, obj, PPM_2X)
        render_cell(b)
        cells.append((name, a, b))
        bpy.data.objects.remove(obj, do_unlink=True)
    sheet(cells, out)
    print('PROPS_PREVIEW', out)


def sheet(cells, out):
    """The contact sheet, composed with bpy images (Blender's Python has no
    PIL): one column per prop, the close up on top; below it the game-scale
    render's middle half shown at 2x with nearest-neighbor pixels, so its
    real pixels can be judged."""
    import numpy as np
    cols = len(cells)
    W, H = CELL * cols, CELL * 2
    canvas = np.zeros((H, W, 4), np.float32)
    canvas[..., 3] = 1
    q = CELL // 4
    for i, (_, a, b) in enumerate(cells):
        for row, path in enumerate((a, b)):
            img = bpy.data.images.load(path)
            px = np.array(img.pixels[:], np.float32).reshape(img.size[1], img.size[0], 4)[:CELL, :CELL]
            if row == 1:
                px = px[q:CELL - q, q:CELL - q].repeat(2, 0).repeat(2, 1)
            # bpy pixel rows go bottom-up; the close-up row is on top of the sheet
            y0 = (1 - row) * CELL
            canvas[y0:y0 + CELL, i * CELL:(i + 1) * CELL] = px
            bpy.data.images.remove(img)
    img = bpy.data.images.new('props_sheet', W, H, alpha=True)
    img.pixels = canvas.ravel()
    img.filepath_raw = out
    img.file_format = 'PNG'
    img.save()


if __name__ == '__main__':
    main()
