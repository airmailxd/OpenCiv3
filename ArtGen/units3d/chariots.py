"""Chariots for the Blender remaster: chariot, War Chariot, Three Man Chariot.

    root = chariots.build('War Chariot', entry)

One or two CC0 asset horses (mounted.horse_asset) with a harness yoked to a
pole, a two-wheeled car behind them with a breastwork (civ-colored unless
the catalog's car says body_tint False), and the crew standing in the car
(units.build_footman, planted on the car's floor; riders.translate and
riders.headgear dress them). Model space as every unit: facing +Y, ground at
z = 0; the catalog's crew positions are relative to the car's axle.
"""
import math

import bpy
from mathutils import Vector

import mounted
import props
import riders
import vkit
from props import R, T

props.DEFAULTS.setdefault('car_wood', ('#7a5638', 0.62, 0.0, 'wood'))
props.DEFAULTS.setdefault('car_body', ('#8a6a48', 0.62, 0.0, 'wood'))
props.DEFAULTS.setdefault('trim', ('#c4a462', 0.4, 1.0, 'metal'))
props.DEFAULTS.setdefault('rim', ('#4a3a30', 0.55, 1.0, 'metal'))


def _horses(root, entry):
    """The team, side by side for two horses; returns their world vertices."""
    h = dict(entry.get('horse', {}))
    n = TEAM.get(entry.get('_unit'), entry.get('horses', 1))
    c = dict(mounted.HORSE_COLORS)
    for k in ('coat', 'mane', 'points', 'muzzle'):
        if h.get(k):
            c[k] = h[k]
    c.update(CHARIOT_COAT.get(entry.get('_unit'), {}))
    xs = [0.0] if n == 1 else [-0.42, 0.42]
    allv = []
    for i, x in enumerate(xs):
        sub = vkit.root(f'horse_{i}')
        sub.parent = root
        body = mounted.horse_asset(sub, dict(h, **c))
        sub.scale = (HORSE_K, HORSE_K, HORSE_K)
        bpy.context.view_layer.update()
        # chariot horses wear a breast collar and a light cloth, never a saddle
        hh = dict(h, saddle_visible=False, **TACK.get(entry.get('_unit'), {}))
        if hh.get('blanket') == 'saddle_cloth':
            hh['cloth_drop'] = min(hh.get('cloth_drop', 0.2), 0.12)
        mounted.asset_tack(sub, body, c, hh)   # fitted to the horse at the origin, then moved into the team
        sub.location.x = x
        bpy.context.view_layer.update()
        allv += mounted._world_verts(body)
    return allv, xs


def _car(root, entry, y_axle, xs):
    """The car: two spoked wheels on an axle, a floor, a curved breastwork
    open at the back, and a pole running forward between the horses to a yoke."""
    car = dict(entry.get('car', {}))
    heavy = car.get('kind') == 'heavy'
    gold = CAR_GOLD.get(entry.get('_unit'), False)
    c = {'car_wood': '#4e3420' if heavy else car.get('wood', '#7a5638'), 'car_body': car.get('body', '#8a6a48'),
         'trim': car.get('trim', '#c4a462'), 'wheel': car.get('trim', '#c4a462') if gold else car.get('spoke', '#6a4a2e'),
         'felloe': car.get('trim', '#c4a462') if gold else car.get('rim', '#5a4028'), 'rim': '#3a3430', 'hub': '#3a3430'}
    p = vkit.part(root, 'car', c, {'car_wood': {'grime': 0.55, 'soot': 0.2}, 'car_body': {'grime': 0.5},
                                   'tint': {'grime': 0.5, 'soot': 0.15}})
    r = car.get('wheel_r', 0.62 if heavy else 0.55) * CAR_K
    track = (car.get('track', 0.5 if not heavy else 0.7) + (0.08 if heavy else 0.0)) * CAR_K
    width = track * 2 - 0.12
    depth = (0.75 if heavy else 0.6) * CAR_K
    zf = r + 0.04                                     # the floor sits just above the axle
    for sx in (-1, 1):
        vkit.spoked_wheel(p, r, 0.09, 'felloe', 'wheel', 'hub', n_spokes=car.get('spokes', 6),
                          center=(sx * track, y_axle, r), tire_key='rim' if car.get('rim_metal', True) else None)
    vkit.rod(p, (-track - 0.06, y_axle, r), (track + 0.06, y_axle, r), 0.035, 'car_wood', seg=10)
    p.add(props.box(width, depth, 0.05), 'car_wood', T(0, y_axle + 0.06, zf), bevel=0.01)
    # breastwork: a U of boards round the front and sides, its top edge trimmed
    tinted = True   # all three of Civ3's chariot cars carry civ-colored panels
    wall_h = car.get('wall_h', 0.5) * CAR_K
    key = 'tint' if tinted else 'car_body'
    pts = []
    for i in range(17):
        a = math.pi * i / 16
        pts.append((width / 2 * math.cos(a), y_axle + 0.06 + depth / 2 * math.sin(a) ** 0.6 - 0.02, zf))
    rings = [[(x, y, zf + 0.02) for x, y, _ in pts], [(x * 1.04, y + 0.01, zf + wall_h) for x, y, _ in pts]]
    wall = props.loft(rings, False, False)
    props.bmesh.ops.solidify(wall, geom=wall.faces[:], thickness=0.025)
    p.add(wall, key, smooth=True, tint=tinted)
    p.add(props.tube(rings[1], 0.02, seg=6), 'trim', smooth=True)
    # the pole forward between the horses to a yoke at their withers
    y_yoke = 0.45
    vkit.rod(p, (0, y_axle + depth / 2, zf - 0.02), (0, y_yoke, 1.18), 0.04, 'car_wood', seg=10)
    if len(xs) > 1:
        vkit.rod(p, (xs[0] - 0.18, y_yoke, 1.25), (xs[1] + 0.18, y_yoke, 1.25), 0.035, 'car_wood', seg=10)
    else:   # one horse: shafts down both sides instead of a pole
        for sx in (-1, 1):
            vkit.rod(p, (sx * 0.28, y_axle + depth / 2, zf), (sx * 0.3, y_yoke + 0.1, 1.15), 0.03, 'car_wood', seg=8)
    p.finish()
    return zf + 0.025


def _crew(root, entry, y_axle, z_floor):
    """The crew standing in the car, facing forward."""
    import addon_utils
    addon_utils.enable('bl_ext.user_default.mpfb', default_set=True)
    import remaster
    import units
    import human as H
    for i, m in enumerate(entry.get('crew', [])):
        e = riders.translate(dict(m, archetype='footman'))
        e = remaster.apply(f"{entry.get('_unit')}:crew{i}", e)
        e.pop('stance', None)
        croot = units.build_footman(e)
        arm = next(o for o in croot.children_recursive if o.type == 'ARMATURE')
        riders.headgear(e, arm)
        x, y = m.get('pos', (0.0, 0.0))
        croot.location = (x, y_axle + 0.06 + y, croot.location.z + z_floor)
        croot.parent = root
        k = CREW_SCALE
        croot.scale = (k, k, k)
        H._update()


CREW_SCALE = 1.22
CAR_K = 1.3   # Civ3's cars are big next to the horses
HORSE_K = 0.92
# per unit, over the catalog's horse tack (as the originals show them)
TACK = {'War Chariot': {'blanket': None, 'neck_cover': 'cloth', 'plume': 'fan', 'plume_tint': True},
        'Three Man Chariot': {'caparison_over': False, 'trim_tint': True, 'plume': True, 'plume_tint': True}}
CAR_GOLD = {'chariot': True, 'War Chariot': True, 'Three Man Chariot': True}
# Civ3's 'chariot' is drawn with a pair of horses
TEAM = {'chariot': 2}
# coats read from the Civ3 originals
CHARIOT_COAT = {
    'chariot': {'coat': '#7a4a28', 'mane': '#1a120c', 'points': '#20160e'},
    'War Chariot': {'coat': '#a8865a', 'mane': '#3a2c20', 'points': '#2e2418'},
    'Three Man Chariot': {'coat': '#c8c8c4', 'mane': '#9a9a98', 'points': '#a8a8a4'},
}


def build(unit, entry, crew=True):
    entry = dict(entry, _unit=unit)
    root = vkit.root(unit)
    verts, xs = _horses(root, entry)
    y_rump = min(v.y for v in verts)
    car = entry.get('car', {})
    r = car.get('wheel_r', 0.62 if car.get('kind') == 'heavy' else 0.55) * CAR_K
    y_axle = y_rump - r - 0.2
    z_floor = _car(root, entry, y_axle, xs)
    if crew:
        _crew(root, entry, y_axle, z_floor)
    return root


UNITS = ['chariot', 'War Chariot', 'Three Man Chariot']
