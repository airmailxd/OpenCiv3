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
import aircraft  # noqa: E402
import armor  # noqa: E402
import armor_mod  # noqa: E402  (the League's modern-mod vehicles: Bradley, Laser Tank, Convoy, Iron Dome)
import chariots  # noqa: E402
import elephant  # noqa: E402
import mounted  # noqa: E402
import ships  # noqa: E402
import siege  # noqa: E402

CANVAS = 480
# (realism infantry) a unit's ppm times this after the box fit: the TOW team is fitted to the Civ3 original's box,
# then drawn smaller, to the stock Artillery's size (the lead, 2026-11-24). Units not listed: 1.0, as before.
PPM_SCALE = {'TOW Infantry': 0.89}
# (realism/all, opt-in) REALISM_PPM_SCALE='{"<Unit>": k, ...}' multiplies those units' ppm once more: the staging
# render shrinks a unit whose model would pass its installed cell boxes. Unset (the default): no change.
STAGE_SCALE = json.loads(os.environ.get('REALISM_PPM_SCALE') or '{}')
SHIPS = set(ships.BUILDERS)


def build(unit):
    if unit in SHIPS:
        return ships.build(unit)
    if unit in armor_mod.UNITS:
        return armor_mod.build(unit)
    if unit in armor.UNITS:
        return armor.build(unit)
    if unit in aircraft.UNITS:
        return aircraft.build(unit)
    if unit in elephant.UNITS:
        catalog = json.load(open(os.path.join(HERE, '..', '_previews', 'units3d', 'catalog.json'), encoding='utf-8'))
        return elephant.build(unit, catalog[unit])
    if unit in chariots.UNITS:
        catalog = json.load(open(os.path.join(HERE, '..', '_previews', 'units3d', 'catalog.json'), encoding='utf-8'))
        return chariots.build(unit, catalog[unit], crew=os.environ.get('NO_CREW') != '1')
    if unit in mounted.BUILDERS or unit in mounted.HORSE_RIDERS:
        catalog = json.load(open(os.path.join(HERE, '..', '_previews', 'units3d', 'catalog.json'), encoding='utf-8'))
        return mounted.build(unit, entry=catalog.get(unit), rider=True)
    # SIEGE_PARAMS (JSON) passes builder keyword overrides for a variant render; unset = the defaults
    root = siege.build(unit, **json.loads(os.environ.get('SIEGE_PARAMS') or '{}'))
    # the siege engines' crews (crews.py, reviewed); NO_CREW=1 leaves the empties bare
    if os.environ.get('NO_CREW') != '1':
        import crews
        catalog = json.load(open(os.path.join(HERE, '..', '_previews', 'units3d', 'catalog.json'), encoding='utf-8'))
        crews.populate(root, unit, catalog.get(unit, {}), catalog)
    return root


def screen_points(root, waterline):
    """World-space points of the model as the camera sees it (meshes, plus a
    stand-in box per crew empty)."""
    pts = []
    dg = bpy.context.evaluated_depsgraph_get()
    for o in root.children_recursive:
        if o.type == 'MESH' and o.get('no_fit'):
            continue   # a part the fit leaves out (the Tank's long barrel, a rider's lance: counted, it shrank the unit)
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


# (the realism pass) a unit's own scale on the fitted ppm: with a long pole out of the fit (mounted.mark_poles) the
# rest is fitted to the stock's box, which included the pole, so the horse and rider come out too big; measured
# against the stock sheet (scratch/footprint.py). Default 1.0.
FIT_SCALE = {'Conquistador': 0.9, 'Horseman': 0.82, 'Knight': 0.92, 'Sipahi': 1.08, 'Ansar Warrior': 0.95}   # (fit fix 2026-11-23: Horseman, Knight drew 1.56x, 1.24x the stock's pixels)
FIT_SCALE.update({'Shoni no Kagesuke': 0.97, 'Nakamaro Horseman': 1.02, 'Teutonic Knight': 0.92, 'Roughrider': 1.07})   # (the lead's rule: sized to the stock counterpart: Keshik, Knight, Knight, Cavalry)   # (numpy-mounted) the yumi out of the fit; sized to the shipped sprite's pixels


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
    # three opt-in scales, each 1.0 unless set: a model's own root['fit_scale'] (armor.py, aircraft.py, numpy_siege),
    # the riders' per-unit FIT_SCALE and the infantry branch's per-unit PPM_SCALE (the TOW team)
    ppm = statistics.median(ppms) * float(root.get('fit_scale', 1.0)) * FIT_SCALE.get(unit, 1.0) * PPM_SCALE.get(unit, 1.0) * STAGE_SCALE.get(unit, 1.0)
    ax = statistics.median((X0 + X1) / 2 - ppm * (x0 + x1) / 2 for x0, x1, _, _, X0, X1, _, _ in boxes)
    ay = statistics.median((Y0 + Y1) / 2 + ppm * (u0 + u1) / 2 for _, _, u0, u1, _, _, Y0, Y1 in boxes)
    return ppm, (ax, ay), {'ppm_per_direction': [round(p, 3) for p in ppms],
                           'calibration_ppm_2x': cal['fit']['ppm_2x']}


def render(unit, use_calibration=False, tag=''):
    """use_calibration: render at the old remake's ppm and anchor from
    calibration/<Unit>.json instead of the fitted ones (for comparing which
    matches the original's size on the map); tag names the output folder."""
    tag = tag + os.environ.get('RENDER_TAG', '')   # (opt-in: a variant's own folder, so variants render side by side)
    out = os.path.abspath(os.path.join(HERE, '..', '_previews', 'units3d', unit + tag))
    os.makedirs(out, exist_ok=True)
    scene = B.reset_scene()
    device = B.use_gpu(scene)
    key = B.lights()
    gp = B.ground()
    if unit in aircraft.UNITS:   # a big ground for the high shadow, softened like the originals' blurred ones
        gp.scale = (6.0, 6.0, 1.0)
        key.data.angle = math.radians(20.0)
    B.tint_aov()
    if os.environ.get('CRISP') == '1':   # crisper: no denoiser smoothing, more samples, a sharper pixel filter
        scene.cycles.use_denoising = False
        scene.cycles.samples = 768
        scene.render.filter_size = 0.7
    root = build(unit)
    waterline = unit in SHIPS
    ppm, anchor, info = fit(unit, root, waterline)
    keep = os.path.join(out, 'fit.json')
    if os.environ.get('KEEP_FIT') == '1' and os.path.exists(keep):   # re-render at the last fit (a local fix: nothing else moves)
        old = json.load(open(keep, encoding='utf-8'))
        ppm, anchor = old['ppm_2x'], tuple(old['anchor_2x'])
        info['source'] = 'kept'
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
        # RV_TAG names a variant's output folder (<Unit><tag>), so variants render side by side; unset = as before
        render(unit, use_calibration=mode == 'cal', tag=('_cal' if mode == 'cal' else '') + os.environ.get('RV_TAG', ''))


if __name__ == '__main__':
    main()
