"""Contact sheet of the props library, close up and at game scale.

    blender -b --factory-startup --python-exit-code 1 --python ArtGen/units3d/props_preview.py -- [out.png] [prop ...]

Each prop is rendered with bscene's look (the game's orthographic camera,
key/fill/rim suns, sky and shadow catcher) twice: close up, filling a 320 px
cell, and at game scale (2x sheet resolution, 52 px per meter, so a 1.8 m man
is about 94 px tall). The sheet puts each prop in a column, the close up on
top and the middle of the game-scale render below at 2x with nearest-neighbor
pixels, all over a grass color. Tinted parts are shown in a civ blue.
Writes ArtGen/_previews/units3d/props.png (gitignored) unless an output path
is given.
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import bscene  # noqa: E402
import props  # noqa: E402

PPM_2X = 26.0 * 2 * 1.8 / 1.8  # pixels per meter at 2x for a 1.8 m man
CELL = 320
BG = (96, 132, 62)
CIV = (0.24, 0.43, 0.92)  # linear civ color shown on tinted parts in the preview

# name, params, rotation (degrees about X, Y, Z) to show it like the game holds it
SHOWN = [
    # bladed props turned so the camera (looking north) sees their flat side
    ('stone_axe', {}, (0, 0, 70)),
    ('spear', {'length': 2.3, 'grip': 0.95}, (0, 0, 60)),
    ('sword', {'length': 0.82, 'width': 0.085, 'guard': 0.2}, (0, 0, 70)),
    ('bow', {'height': 1.45}, (0, 0, 70)),
    ('arrow', {}, (0, 0, 30)),
    ('quiver', {}, (0, 0, 0)),
    ('staff', {}, (0, 0, 0)),
    ('shovel', {}, (0, 0, 0)),
    ('shield_round', {}, (0, 0, 0)),
    ('shield_oval', {}, (0, 0, 0)),
    ('backpack', {}, (0, 0, 180)),
    ('battle_axe', {}, (0, 0, 70)),
    ('star_mace', {}, (0, 0, 0)),
    ('macuahuitl', {}, (0, 0, 70)),
    ('flail', {}, (0, 0, 60)),
    ('scimitar', {}, (0, 0, 70)),
    ('kama', {}, (0, 0, 70)),
    ('katana', {}, (0, 0, 70)),
    ('katana_sheathed', {}, (0, 0, 70)),
    ('sheathed_sword', {'rapier': True}, (0, 0, 70)),
    ('javelin_bundle', {'shaft_tint': True, 'tassels': True}, (0, 0, 0)),
    ('partisan', {}, (0, 0, 60)),
    ('torch', {}, (0, 0, 0)),
    ('scutum', {'face_tint': True}, (0, 0, 0)),
    ('cowhide_shield', {}, (0, 0, 0)),
    ('chimalli', {'emblem': True}, (0, 0, 0)),
    ('plain_round', {}, (0, 0, 0)),
    ('shield_round', {'r': 0.3, 'tint': 'rim', 'emblem': 'aspis'}, (0, 0, 0)),
    ('jaguar_shield', {}, (0, 0, 0)),
    ('pike', {'shaft_tint': True}, (0, 0, 60)),
    ('book', {}, (0, 0, 30)),
    ('map', {}, (0, 0, 0)),
    ('gourd', {}, (0, 0, 0)),
    ('shackles', {}, (0, 0, 0)),
    ('musket', {'bayonet': True}, (0, 0, 70)),
    ('rifle_bolt', {'bayonet': True}, (0, 0, 70)),
    ('assault_rifle', {}, (0, 0, 70)),
    ('smg', {}, (0, 0, 70)),
    ('pistol_holster', {}, (0, 0, 70)),
    ('bazooka', {}, (0, 0, 70)),
    ('tow_launcher', {}, (0, 0, 30)),
    ('modern_helmet', {}, (0, 0, 0)),
    ('canteen', {}, (0, 0, 0)),
    ('mil_backpack', {}, (0, 0, 180)),
    ('scepter', {'head': 'orb'}, (0, 0, 0)),
    ('orb', {}, (0, 0, 0)),
    ('crook', {}, (0, 0, 60)),
    ('royal_flail', {}, (0, 0, 60)),
    ('cane', {}, (0, 0, 0)),
    ('swagger_stick', {}, (0, 0, 0)),
    ('eagle_standard', {}, (0, 0, 0)),
    ('war_standard', {'era': 'ancient'}, (0, 0, 0)),
    ('briefcase', {}, (0, 0, 60)),
    ('microphone', {}, (0, 0, 0)),
    ('pipe', {}, (0, 0, 60)),
    ('glasses', {}, (0, 0, 0)),
    ('sunglasses', {}, (0, 0, 0)),
    ('chute_pack', {}, (0, 0, 180)),
    ('para_pack', {}, (0, 0, 180)),
    ('ak', {}, (0, 0, 70)),
    ('musket_rest', {}, (0, 0, 0)),
    ('cartridge_box', {}, (180, 0, 0)),
    ('haversack', {}, (180, 0, 0)),
    ('bandolier_bottles', {}, (0, 0, 180)),
]

COLORS = {  # catalog colors from the pilot units
    'wood': '#5e4430', 'stone': '#8a8784', 'shield_face': '#c8b894', 'shield_rim': '#5a4028',
    'pack': '#76683e', 'leather': '#5e4430',
}


def show_tint(obj):
    """Preview only: give tinted materials the civ color so they read like in
    game (the real pipeline composites the tint layer instead)."""
    for m in obj.data.materials:
        if m.name.endswith('_tint'):
            pr = props._principled(m)
            if pr and not pr.inputs['Base Color'].is_linked:
                pr.inputs['Base Color'].default_value = (*CIV, 1)


def screen_box(obj):
    """The object's extent on screen (meters): x right, u up, per bscene's
    projection u = y sin30 + z cos30."""
    vs = [obj.matrix_world @ v.co for v in obj.data.vertices]
    xs = [v.x for v in vs]
    us = [v.y * bscene.SIN_E + v.z * bscene.COS_E for v in vs]
    return min(xs), max(xs), min(us), max(us)


def render(obj, ppm, path):
    """Render with a fresh bscene camera centering the object at `ppm`."""
    for o in [o for o in bpy.data.objects if o.type == 'CAMERA']:
        bpy.data.objects.remove(o, do_unlink=True)
    x0, x1, u0, u1 = screen_box(obj)
    cx, cu = (x0 + x1) / 2, (u0 + u1) / 2
    anchor = (CELL / 2 - cx * ppm, CELL / 2 + cu * ppm)
    bscene.camera(CELL, CELL, ppm, anchor)
    sc = bpy.context.scene
    sc.render.filepath = path
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
    sc = bscene.reset_scene()
    device = bscene.use_gpu(sc)
    bscene.lights()
    bscene.ground()
    cells = []
    for name, params, rot in shown:
        obj = props.make(name, COLORS, **params)
        obj.rotation_euler = tuple(math.radians(a) for a in rot)
        bpy.context.view_layer.update()
        # stand it on the ground: lowest point just above z = 0
        zmin = min((obj.matrix_world @ v.co).z for v in obj.data.vertices)
        obj.location.z -= zmin - 0.02
        bpy.context.view_layer.update()
        show_tint(obj)
        x0, x1, u0, u1 = screen_box(obj)
        close_ppm = 0.85 * CELL / max(x1 - x0, u1 - u0, 0.2)
        a = os.path.join(tmp, f'{name}_close.png')
        b = os.path.join(tmp, f'{name}_game.png')
        render(obj, close_ppm, a)
        render(obj, PPM_2X, b)
        cells.append((name, a, b))
        bpy.data.objects.remove(obj, do_unlink=True)
    sheet(cells, out)
    print('PROPS_PREVIEW', out, device)


def sheet(cells, out):
    """The contact sheet, composed with bpy images (Blender's Python has no
    PIL): one column per prop, the close up on top; below it the game-scale
    render's middle half at 2x with nearest-neighbor pixels; everything over
    grass (the renders have transparent film, the shadow in their alpha)."""
    cols = len(cells)
    W, H = CELL * cols, CELL * 2
    bg = np.array([*(c / 255 for c in BG), 1.0], np.float32)
    canvas = np.tile(bg, (H, W, 1))
    q = CELL // 4
    for i, (_, a, b) in enumerate(cells):
        for row, path in enumerate((a, b)):
            img = bpy.data.images.load(path)
            px = np.array(img.pixels[:], np.float32).reshape(img.size[1], img.size[0], 4)[:CELL, :CELL]
            bpy.data.images.remove(img)
            if row == 1:
                px = px[q:CELL - q, q:CELL - q].repeat(2, 0).repeat(2, 1)
            al = px[..., 3:4]
            px = np.concatenate([px[..., :3] * al + bg[:3] * (1 - al), np.ones_like(al)], -1)
            # bpy pixel rows go bottom-up; the close-up row is on top of the sheet
            y0 = (1 - row) * CELL
            canvas[y0:y0 + CELL, i * CELL:(i + 1) * CELL] = px
    img = bpy.data.images.new('props_sheet', W, H, alpha=True)
    img.pixels = canvas.ravel()
    img.filepath_raw = out
    img.file_format = 'PNG'
    img.save()


if __name__ == '__main__':
    main()
