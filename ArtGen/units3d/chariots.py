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
# (the realism pass, batch 2) matte painted-miniature metals and the chariots' own leathers
props.DEFAULTS.setdefault('ch_gilt', ('#86683e', 0.68, 0.35, 'metal'))      # rails, posts, linchpins: dull gilt bronze
props.DEFAULTS.setdefault('ch_sun', ('#a8844c', 0.6, 0.4, 'metal'))       # the emblems, a touch brighter
props.DEFAULTS.setdefault('ch_bronze', ('#8a6a40', 0.6, 0.55, 'metal'))    # helmets, shield rims, rosettes
props.DEFAULTS.setdefault('ch_tyre', ('#a8916a', 0.85, 0.0, 'leather'))   # rawhide tyres
props.DEFAULTS.setdefault('ch_hide', ('#8a6a44', 0.8, 0.0, 'leather'))    # a hide shield face
props.DEFAULTS.setdefault('ch_band', ('#d4c4a0', 0.8, 0.0, 'leather'))    # the panels' natural-leather band
props.DEFAULTS.setdefault('ch_case', ('#9a6a34', 0.65, 0.0, 'leather'))   # the Egyptian bow case
props.DEFAULTS.setdefault('ch_tail', ('#241a14', 0.9, 0.0, 'rope'))       # a helmet's horsehair tail / crest
props.DEFAULTS.setdefault('ch_bronze_hi', ('#b89252', 0.5, 0.55, 'metal'))   # (batch 3) a brighter helmet bronze
props.DEFAULTS.setdefault('ch_border', ('#d8c8a0', 0.75, 0.0, 'wood'))   # (batch 3) the breastwork's light painted rails
props.DEFAULTS.setdefault('ch_quiver', ('#b08050', 0.7, 0.0, 'leather'))   # a light leather quiver
props.DEFAULTS.setdefault('ch_civ_leather', ('#ffffff', 0.7, 0.0, 'leather'))   # civ-painted leather (tint)


def _horses(root, entry):
    """The team, side by side for two horses; returns their world vertices."""
    h = dict(entry.get('horse', {}))
    n = TEAM.get(entry.get('_unit'), entry.get('horses', 1))
    c = dict(mounted.HORSE_COLORS)
    for k in ('coat', 'mane', 'points', 'muzzle'):
        if h.get(k):
            c[k] = h[k]
    c.update(CHARIOT_COAT.get(entry.get('_unit'), {}))
    xs = [0.0] if n == 1 else [-TEAM_X, TEAM_X]
    allv, heads = [], []
    c0 = c
    for i, x in enumerate(xs):
        c = dict(c0, **(TEAM_COATS.get(entry.get('_unit'), [{}] * len(xs))[i]))   # a mixed team (Three Man Chariot)
        sub = vkit.root(f'horse_{i}')
        sub.parent = root
        body = mounted.horse_asset(sub, dict(h, **c), neck_raise=NECK_RAISE, head_flex=HEAD_FLEX, ear_scale=EAR_SCALE, shape=HORSE_SHAPE.get(entry.get('_unit'), HORSE_SHAPE_DEFAULT))
        sub.scale = (HORSE_K, HORSE_K, HORSE_K)
        bpy.context.view_layer.update()
        # chariot horses wear a breast collar and a light cloth, never a saddle
        hh = dict(h, saddle_visible=False, **dict(HARNESS, **TACK.get(entry.get('_unit'), {})))
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


def _strip(rings):
    """A bmesh skinned through two open polylines (props.loft joins each ring's last point to its first)."""
    bm = props.bmesh.new()
    a, b = ([bm.verts.new(p) for p in ring] for ring in rings)
    for i in range(len(a) - 1):
        bm.faces.new([a[i], a[i + 1], b[i + 1], b[i]])
    props.bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def _depth(unit, car):
    return DEPTH.get(unit, 1.15 if car.get('kind') == 'heavy' else 0.9) * CAR_K


def _car(root, entry, y_axle, xs):
    """The car: two spoked wheels on an axle, a floor, a curved breastwork
    open at the back, and a pole running forward between the horses to a yoke."""
    car = dict(entry.get('car', {}))
    if entry.get('_unit') == 'chariot':   # (batch 2) dark wooden wheels, rawhide tyres, six spokes (was grey iron tyres)
        car.update(rim='#3e2a1a', spoke='#3e2a1a', spokes=6)
    heavy = car.get('kind') == 'heavy'
    gold = CAR_GOLD.get(entry.get('_unit'), False)
    c = {'car_wood': '#4e3420' if heavy else car.get('wood', '#7a5638'), 'car_body': CAR_FRAME,
         'trim': car.get('trim', '#c4a462'), 'wheel': car.get('trim', '#c4a462') if gold else car.get('spoke', '#6a4a2e'),
         'felloe': car.get('trim', '#c4a462') if gold else car.get('rim', '#5a4028'), 'rim': '#3a3430', 'hub': '#3a3430',
         'ch_tyre': '#a8916a'}
    p = vkit.part(root, 'car', c, {'car_wood': {'grime': 0.55, 'soot': 0.2}, 'car_body': {'grime': 0.5},
                                   'tint': {'grime': 0.5, 'soot': 0.15}})
    r = car.get('wheel_r', 0.62 if heavy else 0.55) * CAR_K
    track = (car.get('track', 0.5 if not heavy else 0.7) + (0.08 if heavy else 0.0)) * CAR_K * TRACK_K.get(entry.get('_unit'), 1.0)
    width = track * 2 - 0.12
    depth = _depth(entry.get('_unit'), car)   # (batch 3) tighter: the crew fill the car
    zf = r + 0.04                                     # the floor sits just above the axle
    for sx in (-1, 1):
        vkit.spoked_wheel(p, r, 0.09, 'felloe', 'wheel', 'hub', n_spokes=car.get('spokes', 6),
                          center=(sx * track, y_axle, r), spoke_scale=1.3,
                          tire_key=TYRE.get(entry.get('_unit'), 'rim') if car.get('rim_metal', True) else None)
    vkit.rod(p, (-track - 0.06, y_axle, r), (track + 0.06, y_axle, r), 0.035, 'car_wood', seg=10)
    unit = entry.get('_unit')
    yc = y_axle + 0.06 + CAR_FWD.get(unit, 0.0) * depth   # the floor's centre (Egyptian cars: the axle at the back)
    p.add(props.box(width, depth, 0.05), 'car_wood', T(0, yc, zf), bevel=0.01)
    # breastwork (the realism pass): a wooden frame round the front and sides, its sides sloping down to the open
    # back; civ-painted leather panels on its outer face only (the inside reads as wood from behind), gold-trimmed
    wall_h = max(car.get('wall_h', 0.5), 0.5) * CAR_K * WALL_K * WALL_BY_UNIT.get(unit, 1.0)
    # a U of 17 points: down the right side from the back, round the front corners, back up the left
    yb, yfr = yc - depth * 0.45, yc + depth / 2
    lo = SLOPE.get(unit, 0.6)

    def hz(y):   # the wall's height over the floor at y: full at the front, `lo` of it at the back
        t = min(1.0, max(0.0, (y - yb) / (yfr - yb) / 0.75))
        return wall_h * (lo + (1 - lo) * t * t * (3 - 2 * t))
    hw, cr = width / 2, 0.12
    pts = [(hw, yb + (yfr - cr - yb) * i / 5, zf) for i in range(6)]
    pts += [(hw - cr + cr * math.cos(math.pi / 2 * i / 3), yfr - cr + cr * math.sin(math.pi / 2 * i / 3), zf) for i in range(1, 3)]
    pts += [(hw * (1 - 2 * i / 2), yfr, zf) for i in range(3)][1:2]
    pts += [(-x, y, z) for x, y, z in reversed(pts[:8])]

    def outward(i, d):   # pts[i] pushed d out of the car (the polyline's normal in XY)
        x0, y0, _ = pts[max(0, i - 1)]
        x1, y1, _ = pts[min(len(pts) - 1, i + 1)]
        tx, ty = x1 - x0, y1 - y0
        n = math.hypot(tx, ty) or 1.0
        nx, ny = ty / n, -tx / n
        if nx * pts[i][0] + ny * (pts[i][1] - yc) < 0:
            nx, ny = -nx, -ny
        return pts[i][0] + nx * d, pts[i][1] + ny * d
    rings = [[(x, y, zf + 0.02) for x, y, _ in pts], [(x * 1.02, y + 0.01, zf + hz(y)) for x, y, _ in pts]]
    wall = _strip(rings)   # (the realism pass) open at the back: props.loft closes its rings, so the back was boarded
    props.bmesh.ops.solidify(wall, geom=wall.faces[:], thickness=0.025)
    p.add(wall, 'car_body', smooth=True)
    # the civ panels: the outer skin, inset from the rails, stopping short of the open back
    # (batch 2) two civ-painted leather fields split by a natural-leather band with bronze rosettes
    band = lambda y: zf + hz(y) * BAND_Z.get(unit, 0.55)   # noqa: E731  the band's centre
    bw = 0.045
    for lo_, hi_ in ((lambda y: zf + 0.06, lambda y: band(y) - bw), (lambda y: band(y) + bw, lambda y: zf + hz(y) - 0.05)):
        skin = [[(*outward(i, 0.03), z_of(pts[i][1])) for i in range(1, len(pts) - 1)] for z_of in (lo_, hi_)]
        p.add(_strip(skin), 'ch_civ_leather', smooth=True, tint=True)
    skin = [[(*outward(i, 0.035), z_of(pts[i][1])) for i in range(1, len(pts) - 1)]
            for z_of in (lambda y: band(y) - bw, lambda y: band(y) + bw)]
    p.add(_strip(skin), 'ch_band', smooth=True)
    if STRIPS.get(unit):   # (batch 3) vertical natural-leather strips split the long civ side into bays
        for i in range(2, len(pts) - 1, 2):
            x0, y0 = outward(i, 0.036)
            z0, z1 = zf + 0.06, zf + hz(pts[i][1]) - 0.05
            (xa, ya, _), (xb, yb2, _) = pts[max(0, i - 1)], pts[min(len(pts) - 1, i + 1)]
            tx, ty = xb - xa, yb2 - ya
            n_ = math.hypot(tx, ty) or 1.0
            tx, ty = tx / n_ * 0.05, ty / n_ * 0.05
            p.add(_strip([[(x0 - tx, y0 - ty, z0), (x0 + tx, y0 + ty, z0)], [(x0 - tx, y0 - ty, z1), (x0 + tx, y0 + ty, z1)]]),
                  'ch_band', smooth=True)
    for i in range(2, len(pts) - 1, 2):   # bronze rosettes along the band
        x, y = outward(i, 0.05)
        nx, ny = x - pts[i][0], y - pts[i][1]
        p.add(props.ellipsoid(0.035, 0.035, 0.014, seg=12), 'ch_bronze',
              T(x, y, band(pts[i][1])) @ R('Z', math.degrees(math.atan2(ny, nx)) - 90) @ R('X', 90), smooth=True)
    p.add(props.tube(rings[1], 0.03, seg=6), 'ch_border', smooth=True)
    # gold-capped posts and a bottom rail over the panels
    out = [(*outward(i, 0.05), z) for i, (x, y, z) in enumerate(pts)]
    for i in range(0, len(out), 2):
        x, y, _ = out[i]
        vkit.rod(p, (x, y, zf + 0.03), (x, y, zf + hz(pts[i][1])), 0.018, 'ch_gilt', seg=6)
    p.add(props.tube(out, 0.026, seg=6), 'ch_border', T(0, 0, 0.04), smooth=True)   # the bottom rail
    # a side emblem on both flanks
    em = EMBLEM.get(unit)
    if em:
        for sx in (-1, 1):
            m = T(sx * (width / 2 + 0.085), (yb + yfr) / 2 + 0.05, zf + hz((yb + yfr) / 2) * 0.55) @ R('Y', sx * 90)   # mid-panel, above the wheel
            if em == 'sun':   # a bold gold winged sun disc filling the panel (Civ3's Three Man Chariot)
                L_ = yfr - yb
                rd = wall_h * 0.2
                p.add(props.ellipsoid(rd, rd, 0.04, seg=24), 'ch_sun', m, smooth=True)
                p.add(props.tube([(rd * 1.1 * math.cos(2 * math.pi * k / 32), rd * 1.1 * math.sin(2 * math.pi * k / 32), 0.0)
                                  for k in range(33)], 0.02, seg=6), 'ch_sun', m, smooth=True)
                for wx in (-1, 1):
                    for row, (dy, ln) in enumerate(((0.06, 0.5), (0.0, 0.44), (-0.06, 0.36))):   # three rows of feathers
                        # in the panel's own plane: (y along the car, z up), thickness along X
                        wing = props.slab([(0.0, dy + 0.035), (wx * L_ * ln * 0.7, dy + 0.06 - 0.02 * row),
                                           (wx * L_ * ln * 0.66, dy - 0.02), (0.0, dy - 0.03)], 0.02, 'X')
                        p.add(wing, 'ch_sun', T(sx * (width / 2 + 0.085 + 0.01 * row), (yb + yfr) / 2 + 0.05 + wx * rd * 0.9,
                                                  zf + hz((yb + yfr) / 2) * 0.55), bevel=0.004)
            else:             # a gold cartouche outline
                ring = [(0.135 * math.cos(2 * math.pi * k / 24), 0.3 * math.sin(2 * math.pi * k / 24), 0.0) for k in range(25)]
                p.add(props.tube(ring, 0.03, seg=6), 'ch_sun', m, smooth=True)
                p.add(props.ellipsoid(0.06, 0.06, 0.018, seg=12), 'ch_sun', m, smooth=True)   # a sun disc inside it
    # bronze linchpins at the axle ends
    for sx in (-1, 1):
        p.add(props.ellipsoid(0.035, 0.035, 0.05, seg=10), 'ch_gilt', T(sx * (track + 0.09), y_axle, r + 0.04), smooth=True)
    # (batch 2) the side kit, per unit and side: 'bowcase' (the Egyptian angled bow case), 'quiver', 'shield' (hung)
    for sx, kind in SIDE_KIT.get(unit, ()):
        ym = yc + depth * 0.2
        if kind in ('bowcase', 'quiver'):
            big = kind == 'bowcase'
            m = T(sx * (width / 2 + 0.1), ym, zf + hz(ym) * 0.55) @ R('X', -42 if big else -30)
            m = m @ R('Y', sx * 18)   # (batch 3) tilted out from the panel so it reads
            p.add(props.ellipsoid(0.15 if big else 0.09, 0.1 if big else 0.09, 0.52 if big else 0.44, seg=14),
                  'ch_case' if big else 'ch_quiver', m, smooth=True)
            p.add(props.lathe([(0.0, 0.07 if big else 0.06), (0.04, 0.075 if big else 0.065)], seg=12), 'ch_gilt',
                  m @ T(0, 0, 0.36 if big else 0.27), smooth=True)
            n_ = 2 if big else 4
            for k in range(n_):   # the bow's ends / fletched arrows out of the mouth
                o = k - (n_ - 1) / 2
                p.add(props.tube([(0.025 * o, 0.0, 0.3), (0.03 * o, 0.0, 0.52)], [0.018, 0.026], seg=5),
                      'car_wood' if big else 'plume', m, smooth=True)
        elif kind == 'shield':   # a round hide shield with a bronze rim and boss, hung on the side near the back
            yb_ = yb + depth * 0.3
            m = T(sx * (width / 2 + 0.07), yb_, zf + hz(yb_) * 0.5) @ R('Y', sx * 90) @ R('X', 8)
            p.add(props.ellipsoid(0.34, 0.34, 0.035, seg=24), 'ch_hide', m, smooth=True)   # (batch 3) a hide face
            ring = lambda r_, z_: [(r_ * math.cos(2 * math.pi * k / 40), r_ * math.sin(2 * math.pi * k / 40), z_) for k in range(41)]   # noqa: E731
            p.add(props.tube(ring(0.21, 0.03), 0.04, seg=6, scale_xy=(1.0, 0.4)), 'ch_civ_leather', m, smooth=True, tint=True)   # a civ ring
            p.add(props.tube(ring(0.34, 0.0), 0.04, seg=8), 'ch_tyre', m, smooth=True)   # the rawhide rim
            p.add(props.ellipsoid(0.08, 0.08, 0.05, seg=14), 'ch_bronze', m @ T(0, 0, 0.03), smooth=True)
    # the pole forward between the horses to a yoke at their withers
    y_yoke = 0.45
    wit = entry.get('_withers', [])
    if len(xs) > 1 and wit:
        # (batch 2) a bowed yoke resting on the yoke saddles at the withers, and the pole rising to it
        zy = sum(w[2] for w in wit) / len(wit) + 0.12
        yy = sum(w[1] for w in wit) / len(wit)
        p.add(props.tube([(xs[0] - 0.24, yy, zy - 0.07), (xs[0], yy, zy), (0, yy, zy + 0.06), (xs[1], yy, zy),
                          (xs[1] + 0.24, yy, zy - 0.07)], 0.05, seg=10), 'car_wood', smooth=True)
        vkit.rod(p, (0, yfr - 0.3, zf - 0.04), (0, yy, zy + 0.04), 0.055, 'car_wood', seg=10)   # a pole that reads at 1x
    else:
        vkit.rod(p, (0, yfr - 0.3, zf - 0.04), (0, y_yoke, 1.18), 0.055, 'car_wood', seg=10)
    # yoke saddles: civ pads at each horse's withers, gold-finialed
    for x, yw, yz in entry.get('_withers', []):
        p.add(props.ellipsoid(0.2, 0.16, 0.09, seg=14), 'tint', T(x, yw, yz + 0.03), smooth=True, tint=True)
        p.add(props.ellipsoid(0.21, 0.17, 0.025, seg=14), 'ch_gilt', T(x, yw, yz - 0.02), smooth=True)   # its gold edge
        if FINIALS.get(unit):
            for fx in (-0.14, 0.14):
                vkit.rod(p, (x + fx, yw, yz + 0.05), (x + fx * 1.2, yw, yz + 0.3), 0.018, 'ch_gilt', seg=6)
                p.add(props.ellipsoid(0.045, 0.045, 0.045, seg=10), 'ch_gilt', T(x + fx * 1.2, yw, yz + 0.33), smooth=True)
    if len(xs) == 1:   # one horse: shafts down both sides (was a for-else: the shafts ran beside every team's pole)
        for sx in (-1, 1):
            vkit.rod(p, (sx * 0.28, yfr, zf), (sx * 0.3, y_yoke + 0.1, 1.15), 0.03, 'car_wood', seg=8)
    p.finish()
    entry['_car_front'] = (yfr, zf + hz(yfr))
    return zf + 0.025, yc


def _crew(root, entry, y_car, z_floor):
    """The crew standing in the car, facing forward."""
    import addon_utils
    addon_utils.enable('bl_ext.user_default.mpfb', default_set=True)
    import remaster
    import units
    import human as H
    made = []
    for i, m in enumerate(entry.get('crew', [])):
        e = riders.translate(dict(m, archetype='footman'))
        e = remaster.apply(f"{entry.get('_unit')}:crew{i}", e)
        e.pop('stance', None)
        # (realism/all) no weight shift: the foot branch's posture system (units.STANCE_POSTURE) postdates the chariot
        # finals, which were posed without it; an entry may still ask for one with its own 'posture'
        e.setdefault('posture', False)
        croot = units.build_footman(e)
        arm = next(o for o in croot.children_recursive if o.type == 'ARMATURE')
        riders.headgear(e, arm, front_fix=HAT_PITCH_BY_UNIT.get(entry.get('_unit'), 'level'))   # facing forward
        mounted.mark_poles(entry.get('_unit'), e.get('weapon'))   # a crewman's spear out of the fit (as the riders')
        if entry.get('_unit') in FIT_POLES:   # (the realism pass) this unit's spear stays in: the stock footprint
            for o in croot.children_recursive:
                if 'no_fit' in o:
                    del o['no_fit']
        x, y = CREW_POS[entry['_unit']][i] if entry.get('_unit') in CREW_POS else m.get('pos', (0.0, 0.0))
        croot.location = (x, y_car + y, croot.location.z + z_floor)
        croot.parent = root
        k = CREW_SCALE
        croot.scale = (k, k, k)
        H._update()
        made.append((m, croot))
    return made


def _reins(root, entry, crews):
    """(the realism pass) The reins: from each horse's bit back to the driver's left fist (REINS_TO 'hand'), round
    his waist (the Egyptian archer's, 'waist'), or tied to the front rail with no crew; thick enough to stay one piece."""
    unit = entry.get('_unit')
    yfr, ztop = entry['_car_front']
    to, end = REINS_TO.get(unit, 'hand'), None
    if crews:
        m, croot = next(((m, c) for m, c in crews if m.get('driver')), crews[0])
        arm = next(o for o in croot.children_recursive if o.type == 'ARMATURE')
        if to == 'waist':
            pb = arm.pose.bones['spine_01']
            end = (arm.matrix_world @ pb.head).lerp(arm.matrix_world @ pb.tail, 0.3) + Vector((0, 0.1, 0))
        else:
            pb = arm.pose.bones['hand_l']
            end = (arm.matrix_world @ pb.head).lerp(arm.matrix_world @ pb.tail, 0.6)
    if end is None:
        end = Vector((0.0, yfr, ztop))
    p = vkit.part(root, 'reins', {'leather': '#2a1e16'}, {'leather': {'grime': 0.4}})
    for hd in entry.get('_heads', []):
        for sx in (-1, 1):
            a = hd + Vector((sx * 0.04, -0.12, -0.08))   # (batch 2) starting inside the head: the tip broke off at the nose in 8
            b = end + Vector((sx * 0.03, 0, 0))
            m_ = a.lerp(b, 0.5) + Vector((0, 0, -0.1))
            p.add(props.tube([a, m_, b], REINS_R, seg=6), 'leather', smooth=True)
    p.finish()



CREW_SCALE = 1.22
MATTE_ROUGH, MATTE_METAL = 0.58, 0.55
# (batch 2)
TEAM_X = 0.5   # the pair's half spacing (was 0.42: the two heads and manes merged into one mass)
EAR_SCALE = 0.75   # the ear tips broke off as 1-px specks
NECK_RAISE, HEAD_FLEX = -24.0, 14.0   # mounted.horse_asset: lower, flexed necks (were -38, 0): the crew shows over them in 1-3
HARNESS = {'breast_strap': 'leather_dark', 'girth': True}   # the yoke saddles' breast straps and girths (every team)
TYRE = {'chariot': 'ch_tyre', 'War Chariot': 'ch_tyre', 'Three Man Chariot': 'ch_tyre'}   # rawhide tyres
BAND_Z = {}
DEPTH = {'chariot': 0.78, 'War Chariot': 0.78, 'Three Man Chariot': 0.95}   # (batch 3) the car's depth over CAR_K (were 0.9 / 1.15)
TRACK_K = {'Three Man Chariot': 0.86}   # (batch 3) the heavy car narrower: three men fill it
STRIPS = {'Three Man Chariot': True}   # the panels' leather band height, over the wall's (default 0.55)
SIDE_KIT = {'chariot': [(1, 'quiver'), (-1, 'shield')], 'War Chariot': [(1, 'bowcase'), (-1, 'quiver')],
            'Three Man Chariot': [(1, 'quiver'), (-1, 'quiver')]}
CREW_POS = {'Three Man Chariot': [(0.36, 0.22), (0.02, -0.3), (-0.38, 0.18)], 'War Chariot': [(0.0, 0.06)]}   # driver front right, spearman behind, shield-bearer front left
CAR_FRAME = '#5e4028'   # (the realism pass) the breastwork's wooden frame, seen inside from behind (the catalog's tan read as card)
FIT_POLES = {'chariot'}   # the shipped glaive was in the fit; out of it the chariot grew 28 % past the stock's footprint
REINS_R = 0.02   # (the realism pass) 0.012 broke into specks at 2x
REINS_TO = {'War Chariot': 'waist'}   # the Egyptian archer drives with the reins tied round his waist
CAR_FWD = {'War Chariot': 0.42, 'Three Man Chariot': -0.12}   # (batch 2) the Hittite axle under the car's middle   # the floor's centre ahead of the axle, in car depths (Egypt: the axle at the back)
SLOPE = {'War Chariot': 0.7, 'chariot': 0.72, 'Three Man Chariot': 0.8}   # the breastwork's height at the open back, over its front height (default 0.6)
# a unit's crew hat pitch (degrees forward, riders.headgear front_fix); default 'level' (the crown straight up)
HAT_PITCH_BY_UNIT = {}
WALL_BY_UNIT = {'Three Man Chariot': 0.85}   # (the realism pass) the heavy car's wall hid its crew
WALL_K = 1.55   # the breastwork waist-high on the (scaled) crew, as Civ3's cars
CAR_K = 1.3   # Civ3's cars are big next to the horses
HORSE_K = 0.97   # (the realism pass) Bronze Age chariot horses were small, ~13-14 hands; was 1.05
# the team's build (mounted.horse_asset shape): shorter legs than the riders' warhorses, a longer neck
HORSE_SHAPE_DEFAULT = {'leggy': 1.0, 'neck': 1.05}
HORSE_SHAPE = {}
# per unit, over the catalog's horse tack (as the originals show them)
TACK = {# (the realism pass) a civ horse cloth over the back and a head plume, as the Egyptian reliefs; the
        # fitted neck sleeves and the tall fans read as blue buckets on the necks
        'War Chariot': {'blanket': 'saddle_cloth', 'blanket_tint': True, 'neck_cover': None, 'plume': 'fan', 'plume_tint': True},
        # fine silver scale trappers with a civ border
        'Three Man Chariot': {'blanket': 'saddle_cloth', 'blanket_tint': False, 'blanket_color': '#8a6c44', 'cloth_frac': 1.0,
                              'cloth_back': 0.6, 'border_tint': True, 'cloth_border': 0.05, 'cloth_round': 0.1,
                              'plume': False, 'neck_cover': None, 'chanfron': None},   # (batch 3) a bronze-brown trapper over the body,
        # civ-bordered with a clean hem: the scale barding's drape let the coat poke through its top edge (read as spikes and
        # organ pipes); no steel neck lames or face plates, no plumes   # (batch 3) a continuous bronze
        # scale coat with a straight hem: no curved hem (its pleats read as organ pipes), no steel neck lames or face plates, no plumes   # (batch 2) bronze, bigger rows
        # civ head plumes and a dark bridle; the yoke pads (chariots._car) instead of a big blanket
        'chariot': {'blanket': None, 'plume': False, 'bridle': '#3a2a1e'}}   # (batch 2) no plumes: it read as the War Chariot's twin
CAR_GOLD = {}   # (batch 3) no gilt wheels: matte wood with bronze fittings (were the War and Three Man Chariots')   # the chariot: grey iron tires, dark spokes
LATTICE_X = {'chariot': True}
EMBLEM = {'War Chariot': 'cartouche', 'Three Man Chariot': 'sun'}
QUIVER = {'chariot': 'side', 'War Chariot': 'side', 'Three Man Chariot': 'corners'}
FINIALS = {'War Chariot': True, 'Three Man Chariot': True}
YOKE_Z = {}
# per-horse coats: Civ3's Three Man Chariot team is one grey and one dark
TEAM_COATS = {'chariot': [{'coat': '#7a4a28', 'mane': '#1a120c', 'points': '#20160e', 'coat_contrast': 1.3},
                        {'coat': '#a2602e', 'mane': '#3a2010', 'points': '#2a1a10', 'coat_contrast': 1.3}],   # (batch 3) a bay and a chestnut: the dark bay merged
              'War Chariot': [{'coat': '#9a6c44', 'mane': '#241a12', 'points': '#241a12', 'coat_contrast': 1.3},
                              {'coat': '#8a4a26', 'mane': '#d0b080', 'points': '#5a3018', 'coat_contrast': 1.3}],   # a light bay, a flaxen chestnut
              'Three Man Chariot': [{'coat': '#c8c8c4', 'mane': '#9a9a98', 'points': '#a8a8a4'},
                                    {'coat': '#4a3a2e', 'mane': '#1a1410', 'points': '#201812'}]}
# Civ3's 'chariot' is drawn with a pair of horses
TEAM = {'chariot': 2}
# coats read from the Civ3 originals
CHARIOT_COAT = {
    'chariot': {'coat': '#7a4a28', 'mane': '#1a120c', 'points': '#20160e'},
    'War Chariot': {'coat': '#9a6c44', 'mane': '#241a12', 'points': '#241a12'},   # (the realism pass) a sleek light bay; the dun read shaggy, a llama
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
    y_axle -= CAR_FWD.get(unit, 0.0) * _depth(unit, car)   # the car's front stays put
    z_floor, y_car = _car(root, entry, y_axle, xs)
    crews = _crew(root, entry, y_car, z_floor) if crew else []
    _reins(root, entry, crews)
    _matte(root)
    return root


def _matte(root):
    """(batch 2) No chrome: every metal material on this unit (the trappers' scales, the crew's armour and helmets,
    blades) at a dull painted-miniature finish (roughness >= MATTE_ROUGH, metallic <= MATTE_METAL). Materials are
    copied per unit first, so nothing shared with another unit in the scene changes."""
    done = {}
    for o in root.children_recursive:
        if o.type != 'MESH':
            continue
        for slot in o.material_slots:
            m = slot.material
            if m is None or not m.use_nodes:
                continue
            pr = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
            if pr is None or pr.inputs['Metallic'].is_linked or pr.inputs['Metallic'].default_value < 0.5:
                continue
            if m.name not in done:
                mm = m.copy()
                q = next(n for n in mm.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
                q.inputs['Metallic'].default_value = min(q.inputs['Metallic'].default_value, MATTE_METAL)
                if not q.inputs['Roughness'].is_linked:
                    q.inputs['Roughness'].default_value = max(q.inputs['Roughness'].default_value, MATTE_ROUGH)
                done[m.name] = mm
            slot.material = done[m.name]


UNITS = ['chariot', 'War Chariot', 'Three Man Chariot']


# ------------------------------------------------------------ (batch 2) the chariot crews' own headgear
# registered into riders.HEADGEAR under new names: riders.py's own hats and their defaults are unchanged

@riders.hat('ch_khepresh')
def _khepresh_tall(p, c, tinted, _):
    """The blue war crown as the reliefs show it: tall, swelling and flaring up and back, with the ear flaps, gold
    disc studs and the uraeus (a rearing cobra) at the brow."""
    key, t = ('tint', True) if tinted else ('hat', False)
    rings = []
    # (batch 3) about 35 % lower than batch 2's (it read as a dunce cap): bulbous, swept back
    for z, rx, ry, yc in ((0.0, 0.118, 0.13, 0.0), (0.05, 0.13, 0.15, -0.02), (0.1, 0.135, 0.17, -0.05),
                          (0.15, 0.128, 0.18, -0.09), (0.2, 0.11, 0.16, -0.13), (0.23, 0.075, 0.11, -0.16), (0.245, 0.0, 0.0, -0.17)):
        rings.append([(rx * math.cos(2 * math.pi * k / 24), yc + ry * math.sin(2 * math.pi * k / 24), z) for k in range(24)])
    p.add(props.loft(rings, True, False), key, T(0, 0, 0.02), smooth=True, tint=t)
    for sx in (-1, 1):   # the ear flaps
        p.add(props.ellipsoid(0.03, 0.06, 0.07, seg=12), key, T(sx * 0.11, -0.02, -0.02), smooth=True, tint=t)
    for (a, z) in ((40, 0.08), (140, 0.08), (90, 0.14), (20, 0.15), (160, 0.15), (90, 0.04)):   # gold disc studs
        r_ = math.radians(a)
        p.add(props.ellipsoid(0.018, 0.018, 0.018, seg=8), 'ch_sun', T(0.13 * math.cos(r_), -0.04 + 0.13 * math.sin(r_) * 0.3 - 0.06, z),
              smooth=True)
    p.add(props.lathe([(0.0, 0.12), (0.03, 0.12)], seg=24), 'ch_sun', smooth=True)   # the gold band
    p.add(props.tube([(0, 0.125, 0.02), (0, 0.15, 0.08), (0, 0.14, 0.15), (0, 0.155, 0.18)], [0.016, 0.02, 0.022, 0.016], seg=8),
          'ch_sun', smooth=True)   # the uraeus, its hood at the top
    p.add(props.ellipsoid(0.03, 0.014, 0.035, seg=10), 'ch_sun', T(0, 0.15, 0.16), smooth=True)


@riders.hat('ch_hittite')
def _hittite_helm(p, c, tinted, _):
    """The Hittite charioteer's helmet: a tall straight bronze cone with cheek guards and a long dark horsehair tail
    hanging from the apex down the back (not an onion dome)."""
    p.add(props.lathe([(-0.02, 0.122), (0.06, 0.118), (0.16, 0.085), (0.26, 0.045), (0.33, 0.012), (0.35, 0.0)], seg=24),
          'ch_bronze', smooth=True)
    p.add(props.lathe([(-0.02, 0.126), (0.02, 0.126)], seg=24), 'ch_gilt', smooth=True)   # the rim
    for sx in (-1, 1):   # cheek guards
        p.add(props.ellipsoid(0.02, 0.05, 0.075, seg=10), 'ch_bronze', T(sx * 0.115, 0.03, -0.07), smooth=True)
    p.add(props.tube([(0, 0.0, 0.33), (0, -0.1, 0.32), (0, -0.19, 0.18), (0, -0.21, -0.02), (0, -0.2, -0.2)],
                     [0.022, 0.03, 0.032, 0.03, 0.02], seg=8), 'ch_tail', smooth=True)


@riders.hat('ch_crested')
def _crested_helm(p, c, tinted, _):
    """A Late Bronze Age warrior's helmet: a bronze dome with cheek pieces and a dark horsehair crest, front to back."""
    p.add(props.lathe([(-0.02, 0.124), (0.07, 0.118), (0.14, 0.085), (0.19, 0.035), (0.205, 0.0)], seg=24), 'ch_bronze_hi', smooth=True)
    for sx in (-1, 1):
        p.add(props.ellipsoid(0.02, 0.055, 0.075, seg=10), 'ch_bronze_hi', T(sx * 0.115, 0.03, -0.07), smooth=True)
    # (batch 3) a tall horsehair crest, front to back, seated on the dome (an arc on a stilt floated off it in 4 and 8)
    p.add(props.ellipsoid(0.04, 0.17, 0.1, seg=16), 'ch_tail', T(0, -0.02, 0.25), smooth=True)
    p.add(props.ellipsoid(0.035, 0.06, 0.12, seg=12), 'ch_tail', T(0, -0.15, 0.17) @ R('X', -25), smooth=True)   # its fall down the nape


def _eight_shield(p, scale=1.0):
    """(batch 3) The Hittite figure-of-eight shield: a hide board pinched at the waist, a rawhide rim, a bronze boss;
    props.shield_round's convention (the face to -Y, the arm strap at the origin)."""
    k = scale
    for zc, rx, rz in ((0.2, 0.22, 0.2), (-0.14, 0.27, 0.27)):
        p.add(props.domed_disc(rx * k, rz * k, 0.02, 0.04), 'ch_hide', T(0, -0.045, zc * k), smooth=True)
        p.add(props.tube([(rx * k * math.cos(2 * math.pi * i / 40), -0.06, zc * k + rz * k * math.sin(2 * math.pi * i / 40))
                          for i in range(41)], 0.02, seg=6), 'ch_tyre', smooth=True)
    p.add(props.ellipsoid(0.06 * k, 0.04, 0.06 * k, seg=14), 'ch_bronze', T(0, -0.1, -0.1 * k), smooth=True)


props.BUILDERS.setdefault('ch_eight', _eight_shield)
