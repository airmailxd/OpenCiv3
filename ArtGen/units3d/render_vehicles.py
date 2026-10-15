"""Render a non-human unit model (siege.py, ships.py) in its 8 directions.

    blender -b --factory-startup --python-exit-code 1 --python ArtGen/units3d/render_vehicles.py -- Catapult [Galley ...]

For each unit: build the model, fit it to the original (the scale and anchor
that make its projected bounding box match the original DEFAULT frame 0's
box in every direction, on average; numbers from calibration/<Unit>.json),
then render the passes with bscene on the 480x480 canvas (2x the game's
240x240 unit space) into ArtGen/_previews/units3d/<Unit>/ (gitignored),
where compose.py and vehicles_sheet.py pick them up. fit.json there records
the ppm and anchor used.

The fit counts each crew empty as a 0.5 x 0.35 x 1.75 m stand-in (the
original's crew is in its outline; the bodies come later), and ships only
above the waterline (below it the shadow-catcher ground hides the hull).
"""
import json
import math
import os
import statistics
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import bscene as B  # noqa: E402
import mounted  # noqa: E402
import ships  # noqa: E402
import siege  # noqa: E402

CANVAS = 480
SHIPS = set(ships.BUILDERS)


def build(unit):
    if unit in SHIPS:
        return ships.build(unit)
    if unit in mounted.BUILDERS or unit in mounted.HORSE_RIDERS:
        catalog = json.load(open(os.path.join(HERE, '..', '_previews', 'units3d', 'catalog.json'), encoding='utf-8'))
        return mounted.build(unit, entry=catalog.get(unit), rider=True)
    return siege.build(unit)


def screen_points(root, waterline):
    """World-space points of the model as the camera sees it (meshes, plus a
    stand-in box per crew empty)."""
    pts = []
    dg = bpy.context.evaluated_depsgraph_get()
    for o in root.children_recursive:
        if o.type == 'MESH':
            ev = o.evaluated_get(dg)
            mw = ev.matrix_world
            for v in ev.data.vertices:
                w = mw @ v.co
                if not waterline or w.z >= 0.0:
                    pts.append(w)
        elif o.type == 'EMPTY' and o.name.startswith('crew_'):
            c = o.matrix_world.translation
            for dx in (-0.25, 0.25):
                for dy in (-0.18, 0.18):
                    for z in (0.0, 1.75):
                        pts.append(Vector((c.x + dx, c.y + dy, z)))
        elif o.type == 'EMPTY' and o.name.startswith('rider'):
            c = o.matrix_world.translation   # a seated rider: hips at the seat, head ~0.95 m above
            for dx in (-0.25, 0.25):
                for dy in (-0.2, 0.2):
                    for z in (c.z - 0.3, c.z + 0.95):
                        pts.append(Vector((c.x + dx, c.y + dy, z)))
    return pts


def extent(pts):
    xs = [p.x for p in pts]
    us = [p.y * B.SIN_E + p.z * B.COS_E for p in pts]
    return min(xs), max(xs), min(us), max(us)


def fit(unit, root, waterline):
    """(ppm, anchor) at 2x so the model's box matches the original's."""
    cal = json.load(open(os.path.join(HERE, 'calibration', f'{unit}.json'), encoding='utf-8'))
    base = root.rotation_euler.z
    ppms, boxes = [], []
    for d in cal['per_direction']:
        if d is None:
            continue
        root.rotation_euler.z = base + math.radians(B.facing_deg(d['direction']))
        bpy.context.view_layer.update()
        x0, x1, u0, u1 = extent(screen_points(root, waterline))
        bx0, by0, bx1, by1 = d['bbox']
        ow, oh = 2 * (bx1 + 1 - bx0), 2 * (by1 + 1 - by0)
        ppms.append((ow + oh) / max((x1 - x0) + (u1 - u0), 1e-6))
        boxes.append((x0, x1, u0, u1, 2 * bx0, 2 * (bx1 + 1), 2 * by0, 2 * (by1 + 1)))
    root.rotation_euler.z = base
    ppm = statistics.median(ppms)
    ax = statistics.median((X0 + X1) / 2 - ppm * (x0 + x1) / 2 for x0, x1, _, _, X0, X1, _, _ in boxes)
    ay = statistics.median((Y0 + Y1) / 2 + ppm * (u0 + u1) / 2 for _, _, u0, u1, _, _, Y0, Y1 in boxes)
    return ppm, (ax, ay), {'ppm_per_direction': [round(p, 3) for p in ppms],
                           'calibration_ppm_2x': cal['fit']['ppm_2x']}


def render(unit, use_calibration=False, tag=''):
    """use_calibration: render at the old remake's ppm and anchor from
    calibration/<Unit>.json instead of the fitted ones (for comparing which
    matches the original's size on the map); tag names the output folder."""
    out = os.path.abspath(os.path.join(HERE, '..', '_previews', 'units3d', unit + tag))
    os.makedirs(out, exist_ok=True)
    scene = B.reset_scene()
    device = B.use_gpu(scene)
    B.lights()
    B.ground()
    B.tint_aov()
    root = build(unit)
    waterline = unit in SHIPS
    ppm, anchor, info = fit(unit, root, waterline)
    if use_calibration:
        cal = json.load(open(os.path.join(HERE, 'calibration', f'{unit}.json'), encoding='utf-8'))
        ppm = cal['fit']['ppm_2x']
        a = cal['anchor_override'] or cal['anchor']
        anchor = (2 * a[0], 2 * a[1])
        info['source'] = 'calibration'
    B.camera(CANVAS, CANVAS, ppm, anchor)
    B.outputs(out)
    B.render_directions(root, range(8))
    json.dump({'unit': unit, 'ppm_2x': ppm, 'anchor_2x': anchor, 'canvas': CANVAS, **info},
              open(os.path.join(out, 'fit.json'), 'w', encoding='utf-8'), indent=2)
    print('RENDERED', unit, device, f'ppm {ppm:.2f} anchor ({anchor[0]:.1f}, {anchor[1]:.1f})', out)


def main():
    """Units to render; 'Unit:cal' renders at the calibration's ppm/anchor
    instead, into the folder '<Unit>_cal'."""
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    for arg in argv:
        unit, _, mode = arg.partition(':')
        render(unit, use_calibration=mode == 'cal', tag='_cal' if mode == 'cal' else '')


if __name__ == '__main__':
    main()
