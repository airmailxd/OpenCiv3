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
props.DEFAULTS.setdefault('sun_gold', ('#e8c050', 0.3, 1.0, 'metal'))   # a brighter gold for the panel emblems


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
    allv, heads = [], []
    c0 = c
    for i, x in enumerate(xs):
        c = dict(c0, **(TEAM_COATS.get(entry.get('_unit'), [{}] * len(xs))[i]))   # a mixed team (Three Man Chariot)
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
        wv = mounted._world_verts(body)
        allv += wv
        feet = [v for v in wv if v.z < 0.15]
        yf_, yh_ = max(v.y for v in feet), min(v.y for v in feet)
        yw = yh_ + 0.72 * (yf_ - yh_)
        top = max(v.z for v in wv if abs(v.y - yw) < 0.08 and abs(v.x - x) < 0.12)
        entry.setdefault('_withers', []).append((x, yw, top))
        ytip = max(v.y for v in wv)
        head = [v for v in wv if v.y > ytip - 0.3]
        heads.append(sum(head, Vector()) / len(head))   # the head, where the reins start
    entry['_heads'] = heads
    return allv, xs


def _car(root, entry, y_axle, xs):
    """The car: two spoked wheels on an axle, a floor, a curved breastwork
    open at the back, and a pole running forward between the horses to a yoke."""
    car = dict(entry.get('car', {}))
    if entry.get('_unit') == 'chariot':   # Civ3's: light grey iron tires, dark spokes
        car.update(rim='#8a8c90', spoke='#3a2a1e')
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
    depth = (1.15 if heavy else 0.9) * CAR_K   # long side panels, as Civ3's cars
    zf = r + 0.04                                     # the floor sits just above the axle
    for sx in (-1, 1):
        vkit.spoked_wheel(p, r, 0.09, 'felloe', 'wheel', 'hub', n_spokes=car.get('spokes', 6),
                          center=(sx * track, y_axle, r), tire_key='rim' if car.get('rim_metal', True) else None)
    vkit.rod(p, (-track - 0.06, y_axle, r), (track + 0.06, y_axle, r), 0.035, 'car_wood', seg=10)
    p.add(props.box(width, depth, 0.05), 'car_wood', T(0, y_axle + 0.06, zf), bevel=0.01)
    # breastwork: a U of boards round the front and sides, its top edge trimmed
    tinted = True   # all three of Civ3's chariot cars carry civ-colored panels
    wall_h = max(car.get('wall_h', 0.5), 0.5) * CAR_K * WALL_K
    key = 'tint' if tinted else 'car_body'
    # a U of 17 points: down the right side from the back, round the front corners, back up the left
    yb, yfr = y_axle + 0.06 - depth * 0.45, y_axle + 0.06 + depth / 2
    hw, cr = width / 2, 0.12
    pts = [(hw, yb + (yfr - cr - yb) * i / 5, zf) for i in range(6)]
    pts += [(hw - cr + cr * math.cos(math.pi / 2 * i / 3), yfr - cr + cr * math.sin(math.pi / 2 * i / 3), zf) for i in range(1, 3)]
    pts += [(hw * (1 - 2 * i / 2), yfr, zf) for i in range(3)][1:2]
    pts += [(-x, y, z) for x, y, z in reversed(pts[:8])]
    rings = [[(x, y, zf + 0.02) for x, y, _ in pts], [(x * 1.02, y + 0.01, zf + wall_h) for x, y, _ in pts]]
    wall = props.loft(rings, False, False)
    props.bmesh.ops.solidify(wall, geom=wall.faces[:], thickness=0.025)
    p.add(wall, key, smooth=True, tint=tinted)
    p.add(props.tube(rings[1], 0.02, seg=6), 'trim', smooth=True)
    unit = entry.get('_unit')
    # a gold lattice over the panels: posts and a mid rail (Civ3's chariot frames)
    # clear of the panel's thickness: out from the sides and the front
    out = [(x + (math.copysign(0.06, x) if abs(x) > hw - cr - 0.01 else 0.0), y + (0.06 if y > yfr - cr - 0.01 else 0.0), z)
           for x, y, z in pts]
    mid = [(x, y, zf + wall_h * 0.5) for x, y, _ in out]
    p.add(props.tube(mid, 0.014, seg=6), 'trim', smooth=True)
    for i in range(0, len(out), 2):
        x, y, _ = out[i]
        vkit.rod(p, (x, y, zf + 0.03), (x * 1.01, y, zf + wall_h), 0.016, 'trim', seg=6)
    p.add(props.tube(out, 0.018, seg=6), 'trim', T(0, 0, 0.04), smooth=True)   # the bottom rail
    if LATTICE_X.get(unit):   # diagonal braces in each bay
        for i in range(0, len(out) - 2, 2):
            (x0, y0, _), (x1, y1, _) = out[i], out[i + 2]
            vkit.rod(p, (x0, y0, zf + 0.04), (x1, y1, zf + wall_h * 0.5), 0.009, 'trim', seg=5)
            vkit.rod(p, (x0, y0, zf + wall_h * 0.5), (x1, y1, zf + wall_h - 0.02), 0.009, 'trim', seg=5)
    # a side emblem on both flanks
    em = EMBLEM.get(unit)
    if em:
        for sx in (-1, 1):
            m = T(sx * (width / 2 + 0.085), (yb + yfr) / 2 + 0.05, zf + wall_h * 0.6) @ R('Y', sx * 90)   # mid-panel, above the wheel
            if em == 'sun':   # a bold gold winged sun disc filling the panel (Civ3's Three Man Chariot)
                L_ = yfr - yb
                rd = wall_h * 0.2
                p.add(props.ellipsoid(rd, rd, 0.04, seg=24), 'sun_gold', m, smooth=True)
                p.add(props.tube([(rd * 1.1 * math.cos(2 * math.pi * k / 32), rd * 1.1 * math.sin(2 * math.pi * k / 32), 0.0)
                                  for k in range(33)], 0.02, seg=6), 'sun_gold', m, smooth=True)
                for wx in (-1, 1):
                    for row, (dy, ln) in enumerate(((0.06, 0.5), (0.0, 0.44), (-0.06, 0.36))):   # three rows of feathers
                        # in the panel's own plane: (y along the car, z up), thickness along X
                        wing = props.slab([(0.0, dy + 0.035), (wx * L_ * ln * 0.7, dy + 0.06 - 0.02 * row),
                                           (wx * L_ * ln * 0.66, dy - 0.02), (0.0, dy - 0.03)], 0.02, 'X')
                        p.add(wing, 'sun_gold', T(sx * (width / 2 + 0.085 + 0.01 * row), (yb + yfr) / 2 + 0.05 + wx * rd * 0.9,
                                                  zf + wall_h * 0.6), bevel=0.004)
            else:             # a gold cartouche outline
                ring = [(0.135 * math.cos(2 * math.pi * k / 24), 0.3 * math.sin(2 * math.pi * k / 24), 0.0) for k in range(25)]
                p.add(props.tube(ring, 0.03, seg=6), 'sun_gold', m, smooth=True)
                p.add(props.ellipsoid(0.06, 0.06, 0.018, seg=12), 'sun_gold', m, smooth=True)   # a sun disc inside it
    # bronze linchpins at the axle ends
    for sx in (-1, 1):
        p.add(props.ellipsoid(0.035, 0.035, 0.05, seg=10), 'trim', T(sx * (track + 0.09), y_axle, r + 0.04), smooth=True)
    # a quiver / bow case slung diagonally on the side of the car
    if QUIVER.get(unit) == 'side':
        m = T(width / 2 + 0.12, y_axle + 0.06 + depth * 0.25, zf + wall_h * 0.6) @ R('X', -40)
        p.add(props.ellipsoid(0.07, 0.08, 0.4, seg=12), 'leather', m, smooth=True)
        p.add(props.lathe([(0.0, 0.055), (0.03, 0.06)], seg=12), 'trim', m @ T(0, 0, 0.28), smooth=True)
        for k in range(4):   # fletched arrows out of its mouth
            p.add(props.tube([(0.012 * (k - 1.5), 0.0, 0.25), (0.015 * (k - 1.5), 0.0, 0.46)], [0.006, 0.014], seg=4),
                  'plume', m, smooth=True)
    elif QUIVER.get(unit) == 'corners':
        for sx in (-1, 1):
            m = T(sx * width / 2, y_axle + 0.06 + depth / 2 - 0.08, zf + wall_h * 0.6) @ R('X', 20) @ R('Y', sx * -12)
            p.add(props.ellipsoid(0.055, 0.06, 0.3, seg=12), 'tint', m, smooth=True, tint=True)
            p.add(props.lathe([(0.0, 0.06), (0.03, 0.065)], seg=12), 'trim', m @ T(0, 0, 0.27), smooth=True)
            for k in range(3):
                p.add(props.tube([(0.015 * (k - 1), 0.0, 0.25), (0.018 * (k - 1), 0.0, 0.45)], 0.007, seg=4), 'car_wood', m, smooth=True)
    # the pole forward between the horses to a yoke at their withers
    y_yoke = 0.45
    vkit.rod(p, (0, y_axle + depth / 2, zf - 0.02), (0, y_yoke, 1.18), 0.055, 'car_wood', seg=10)   # a pole that reads at 1x
    if len(xs) > 1:
        vkit.rod(p, (xs[0] - 0.18, y_yoke, 1.25), (xs[1] + 0.18, y_yoke, 1.25), 0.045, 'car_wood', seg=10)
    # yoke saddles: civ pads at each horse's withers, gold-finialed
    for x, yw, yz in entry.get('_withers', []):
        p.add(props.ellipsoid(0.2, 0.16, 0.09, seg=14), 'tint', T(x, yw, yz + 0.03), smooth=True, tint=True)
        p.add(props.ellipsoid(0.21, 0.17, 0.025, seg=14), 'trim', T(x, yw, yz - 0.02), smooth=True)   # its gold edge
        if FINIALS.get(unit):
            for fx in (-0.14, 0.14):
                vkit.rod(p, (x + fx, yw, yz + 0.05), (x + fx * 1.2, yw, yz + 0.3), 0.018, 'trim', seg=6)
                p.add(props.ellipsoid(0.045, 0.045, 0.045, seg=10), 'trim', T(x + fx * 1.2, yw, yz + 0.33), smooth=True)
    # reins from the bits back to the driver's hands at the front of the car
    for hd in entry.get('_heads', []):
        for sx in (-1, 1):
            a = hd + Vector((sx * 0.08, -0.05, -0.1))
            b = Vector((sx * 0.1, y_axle + depth / 2 - 0.1, zf + wall_h + 0.35))
            m_ = a.lerp(b, 0.5) + Vector((0, 0, -0.12))
            p.add(props.tube([a, m_, b], 0.012, seg=5), 'leather', smooth=True)
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
WALL_K = 1.7   # the breastwork waist-high on the (scaled) crew, as Civ3's cars
CAR_K = 1.3   # Civ3's cars are big next to the horses
HORSE_K = 1.05
# per unit, over the catalog's horse tack (as the originals show them)
TACK = {'War Chariot': {'blanket': None, 'neck_cover': 'cloth_sleeve', 'sleeve_spots': False, 'sleeve_trim': True,
                        'plume': 'fan', 'plume_tint': True},   # a fitted civ neck cloth, gold-edged
        # fine silver scale trappers with a civ border
        'Three Man Chariot': {'caparison_over': False, 'trim_tint': True, 'plume': True, 'plume_tint': True,
                              'barding_steel': '#c8ccd2', 'barding_dark': '#8e929a', 'scale_period': 0.045, 'barding_curve': 0.62},
        # civ head plumes and a dark bridle; the yoke pads (chariots._car) instead of a big blanket
        'chariot': {'blanket': None, 'plume': True, 'plume_tint': True, 'bridle': '#3a2a1e'}}
CAR_GOLD = {'chariot': False, 'War Chariot': True, 'Three Man Chariot': True}   # the chariot: grey iron tires, dark spokes
LATTICE_X = {'chariot': True}
EMBLEM = {'War Chariot': 'cartouche', 'Three Man Chariot': 'sun'}
QUIVER = {'chariot': 'side', 'War Chariot': 'side', 'Three Man Chariot': 'corners'}
FINIALS = {'War Chariot': True, 'Three Man Chariot': True}
YOKE_Z = {}
# per-horse coats: Civ3's Three Man Chariot team is one grey and one dark
TEAM_COATS = {'Three Man Chariot': [{'coat': '#c8c8c4', 'mane': '#9a9a98', 'points': '#a8a8a4'},
                                    {'coat': '#4a3a2e', 'mane': '#1a1410', 'points': '#201812'}]}
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
