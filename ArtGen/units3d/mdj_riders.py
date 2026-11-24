"""The Medieval Japan riders (the realism pass on the 39 NumPy units): a
late-Heian to Kamakura mounted samurai in o-yoroi on a small native horse.

New parts only (the mounted family's shared code is untouched; mounted.py
calls these for the units in its MDJ tables):

- hat 'o_kabuto' (riders.HEADGEAR): a dark iron hoshi-kabuto, its wide
  flaring shikoro of laced lames with big fukigaeshi turnbacks, a gold
  kuwagata or a small gold maedate;
- o_yoroi(arm, k, p): the armour's sode (big laced shoulder boards) and
  kusazuri (the laced skirt panels), built on the posed, seated rider;
- abumi(root, arm, seat, k, p): the lacquered slipper stirrups;
- kura(p, ys, top, p_): the Japanese saddle's high lacquered arches (asset_tack's 'kura');
- props 'yumi' (the tall asymmetric bow, held a third of the way up) and
  'ebira' (the open box quiver, arrows fanned up behind the right shoulder);
- RIDE_POSES 'yabusame' (the bow drawn, shooting to the left) and 'tachi'
  (the sword held out low to the right, the point forward).

Spaces: as riders.py (world, the unit facing +y, his left -x; rider metres
times k, the rider's scale).
"""
import math

from mathutils import Vector

import props
import riders
from props import R, T

props.DEFAULTS.setdefault('yoroi_lacquer', ('#1e1a16', 0.32, 0.0, 'leather'))   # black lacquered iron and rawhide lames
props.DEFAULTS.setdefault('yoroi_hem', ('#e6dcc4', 0.8, 0.0, 'rope'))            # the hishinui: pale cross-knots on the hem lame
props.DEFAULTS.setdefault('yoroi_gold', ('#c8a048', 0.35, 1.0, 'metal'))
props.DEFAULTS.setdefault('kabuto_iron', ('#2a2826', 0.38, 0.85, 'metal'))       # the bowl: dark lacquered iron
props.DEFAULTS.setdefault('yumi_lacquer', ('#1e1612', 0.4, 0.0, None))           # the yumi: black lacquered bamboo
props.DEFAULTS.setdefault('yumi_rattan', ('#a88a5c', 0.6, 0.0, None))   # (R3) pale rattan bands that read at 1x
props.DEFAULTS.setdefault('yumi_grip', ('#141010', 0.7, 0.0, 'leather'))   # (R3) a dark grip wrap, apart from the dun neck            # its pale rattan bindings
props.DEFAULTS.setdefault('ebira_wood', ('#141010', 0.5, 0.0, 'leather'))
props.DEFAULTS.setdefault('taka_feather', ('#e8e2d2', 0.85, 0.0, None))          # eagle-feather fletching, pale with dark bars
props.DEFAULTS.setdefault('taka_bar', ('#2a2420', 0.85, 0.0, None))


# ------------------------------------------------------------ the kabuto

@riders.hat('o_kabuto')
def o_kabuto(p, c, tinted, _, kuwagata=0.0, maedate=False, lames=4, flare=0.45, lace=True, turnback=1.0, bowl=1.0):
    """A hoshi-kabuto: a round dark iron bowl with raised plate ridges and a
    small gold hachiman-za ring at its crown; a wide shikoro of `lames` laced
    lames flaring down and out round the back and sides (the lacing civ when
    `lace`); fukigaeshi turnbacks at its front ends; a gold kuwagata (its
    height factor; 0 none) or a small gold maedate disc."""
    lk, lt = ('tint', True) if lace else ('yoroi_lacquer', False)
    b = bowl
    p.add(props.lathe([(-0.01, 0.124 * b), (0.06, 0.122 * b), (0.11, 0.095 * b), (0.145, 0.05 * b), (0.158, 0.0)], seg=28),
          'kabuto_iron', smooth=True)
    for i in range(12):   # the plate ridges (suji) down the bowl
        a = 2 * math.pi * i / 12
        pts = [(0.126 * b * math.sin(a) * s, 0.126 * b * math.cos(a) * s, -0.005 + 0.15 * (1 - s ** 2) ** 0.5)
               for s in (1.0, 0.92, 0.75, 0.5, 0.2)]
        p.add(props.tube(pts, 0.004, seg=4), 'kabuto_iron', smooth=True)
    p.add(props.lathe([(0.145, 0.03), (0.16, 0.02)], seg=14), 'yoroi_gold', smooth=True)        # the hachiman-za
    p.add(props.lathe([(-0.012, 0.128 * b), (0.012, 0.128 * b)], seg=28), 'yoroi_gold', smooth=True)   # the koshimaki band
    # the visor (mabizashi): a short lacquered peak over the brow
    vis = props.slab([(-0.1, 0.0), (0.1, 0.0), (0.08, 0.045), (-0.08, 0.045)], 0.008, 'X')
    p.add(vis, 'yoroi_lacquer', T(0, 0.118 * b, 0.0) @ R('Z', 90) @ R('X', -28), bevel=0.002)
    # the shikoro: lames hanging from the bowl's rim, each wider and flatter (umbrella-like, o-yoroi)
    a0, a1 = 62, 298          # degrees round from the face (0): open at the front
    zt = -0.005
    r_prev = 0.13 * b
    for i in range(lames):
        h = 0.048
        r_top = r_prev
        r_bot = r_top + 0.035 * flare * (1 + 0.25 * i)
        z_bot = zt - h * (1 - 0.1 * i)
        ring_t, ring_b = [], []
        for kk in range(21):
            a = math.radians(a0 + (a1 - a0) * kk / 20)
            ring_t.append((r_top * math.sin(a), r_top * math.cos(a), zt))
            ring_b.append((r_bot * math.sin(a), r_bot * math.cos(a), z_bot))
        bm = props.loft([ring_t, ring_b], False, False)
        props.bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.008)
        p.add(bm, lk, smooth=True, tint=lt)
        edge = [(r_bot * 1.01 * math.sin(math.radians(a0 + (a1 - a0) * kk / 20)),
                 r_bot * 1.01 * math.cos(math.radians(a0 + (a1 - a0) * kk / 20)), z_bot + 0.004) for kk in range(21)]
        p.add(props.tube(edge, 0.006, seg=5, scale_xy=(1.0, 0.6)), 'yoroi_lacquer', smooth=True)   # the lame's lacquered edge
        zt = z_bot + 0.012
        r_prev = r_bot - 0.004
    # the fukigaeshi: the top lames' front ends turned back, broad flaps beside the face
    for sx in (-1, 1):
        a = math.radians(a0 if sx > 0 else a1)
        x0, y0 = 0.135 * b * math.sin(a), 0.135 * b * math.cos(a)
        w, hgt = 0.075 * turnback, 0.085 * turnback
        flap = props.slab([(0.0, 0.0), (w, 0.01), (w * 0.95, -hgt), (0.0, -hgt * 0.8)], 0.01, 'X')
        p.add(flap, 'yoroi_lacquer', T(x0 + sx * 0.012, y0, 0.0) @ R('Z', -sx * 25) @ R('Y', sx * 12), bevel=0.003)
        p.add(props.tube([(x0 + sx * 0.02, y0 + 0.005, -0.004), (x0 + sx * 0.03, y0 + w * 0.9, -0.004)], 0.006, seg=5),
              'yoroi_gold', smooth=True)                                                           # its gilt edge
    if kuwagata:
        kq = kuwagata
        p.add(props.box(0.05, 0.012, 0.04), 'yoroi_gold', T(0, 0.128 * b, 0.035), bevel=0.003)   # the base plate
        for sx in (-1, 1):   # broad flat gold blades rising in a tall V
            p.add(props.tube([(sx * 0.012, 0.134 * b, 0.04), (sx * 0.05 * kq, 0.145 * b, 0.12 * kq),
                              (sx * 0.085 * kq, 0.142 * b, 0.22 * kq), (sx * 0.1 * kq, 0.13 * b, 0.28 * kq)],
                             [0.02, 0.028, 0.026, 0.02], seg=6, scale_xy=(1.0, 0.55)), 'yoroi_gold', smooth=True)   # blunt tips: one piece at 1x
    elif maedate:
        p.add(props.lathe([(0.0, 0.03), (0.008, 0.03)], seg=20), 'yoroi_gold', T(0, 0.134 * b, 0.05) @ R('X', -80), smooth=True)


# ------------------------------------------------------------ o-yoroi

def _lame_band(pts_top, down, depth, n_down=1):
    """A lame: a strip hanging `down` (a vector) from the points along its
    top edge, `depth` thick (pushed back along the strip's inward normal)."""
    rings = []
    for i, q in enumerate(pts_top):
        q = Vector(q)
        a = q
        b = q + down
        prv = Vector(pts_top[max(0, i - 1)])
        nxt = Vector(pts_top[min(len(pts_top) - 1, i + 1)])
        along = (nxt - prv).normalized()
        inward = down.normalized().cross(along).normalized() * depth
        rings.append([a, b, b + inward, a + inward])
    return props.loft(rings)


def _panel(pr, top_pts, down, n, lace_key, lace_tint, k, overlap=0.18, hem=True, edge=True, outward=None, lace_frac=None):
    """A laced plate panel: n lames, each `down` tall, stepping out by
    `outward` per lame (a vector), the lacing (civ) on each lame with a
    lacquered edge at its top and a pale cross-knot hem on the last one."""
    outward = outward if outward is not None else Vector((0, 0, 0))
    for j in range(n):
        off = down * (j * (1 - overlap)) + outward * j
        pts = [Vector(q) + off for q in top_pts]
        if lace_frac:   # dark lacquered lames, the lacing a band across each one's upper part (the part the next lame leaves showing)
            pr.add(_lame_band(pts, down, 0.01 * k), 'yoroi_lacquer', smooth=True)
            pr.add(_lame_band([q + down * 0.06 + outward * 0.1 for q in pts], down * lace_frac, 0.012 * k), lace_key,
                   smooth=True, tint=lace_tint)
        else:
            pr.add(_lame_band(pts, down, 0.01 * k), lace_key, smooth=True, tint=lace_tint)
        if edge:
            pr.add(props.tube([q + down * 0.06 + outward * 0.15 for q in pts], 0.0075 * k, seg=5, scale_xy=(1.0, 0.5)),
                   'yoroi_lacquer', smooth=True)
        if hem and j == n - 1:
            pr.add(props.tube([q + down * 0.8 + outward * 0.2 for q in pts], 0.0085 * k, seg=5, scale_xy=(1.0, 0.55)),
                   'yoroi_hem', smooth=True)


def o_yoroi(arm, k, p=None):
    """The o-yoroi's sode and kusazuri on the posed, seated rider (fixed to
    spine_03 and the pelvis). The dou itself is the rider's cuirass (laced
    rows, remaster 'patterns'). p:
    'sode' (True), 'sode_w'/'sode_n'/'sode_h' (a board's width, lames and
    lame height, rider metres), 'sode_out' (degrees the boards swing out from
    the arm), 'swing_l'/'swing_r' (degrees a board swings back round the
    shoulder: the bow arm's), 'kusazuri' (True), 'kz_n'/'kz_h' (lames, lame
    height), 'kz_r' (the waist radius), 'kz_span' (degrees each panel
    covers), 'kz_tilt' {front, side, back} (degrees off vertical),
    'tint' (True: civ lacing; else the 'lace' colour)."""
    import human as H
    p = dict(p or {})
    H._update()
    mw = arm.matrix_world
    pel = mw @ arm.pose.bones['pelvis'].head
    sp1 = mw @ arm.pose.bones['spine_01'].head
    if not p.get('tint', True):
        props.DEFAULTS.setdefault('yoroi_lace', (p.get('lace', '#7a2a1c'), 0.8, 0.0, 'rope'))
    lk, lt = ('tint', True) if p.get('tint', True) else ('yoroi_lace', False)
    out = []
    if p.get('kusazuri', True):
        pr = props.Prop('kusazuri', {}, {'tint': {'grime': 0.35}})
        zt = (pel.z + sp1.z) / 2 + p.get('kz_z', 0.02) * k
        n, hl = p.get('kz_n', 5), p.get('kz_h', 0.095) * k
        r0 = p.get('kz_r', 0.16) * k
        span = p.get('kz_span', 40.0)
        tilt = dict({'front': 62.0, 'side': 18.0, 'back': 22.0}, **p.get('kz_tilt', {}))
        for name, ac in (('front', 0.0), ('side', 90.0), ('back', 180.0), ('side', 270.0)):
            t = math.radians(tilt[name])
            dir_ = Vector((math.sin(math.radians(ac)), math.cos(math.radians(ac)), 0.0))
            down = (dir_ * math.sin(t) + Vector((0, 0, -math.cos(t)))) * hl
            top = []
            sp_ = span * (1.15 if name == 'front' else 1.0)
            for i in range(9):
                a = math.radians(ac - sp_ / 2 + sp_ * i / 8)
                top.append(Vector((pel.x + r0 * math.sin(a), pel.y + r0 * math.cos(a), zt)))
            _panel(pr, top, down, n, lk, lt, k, outward=dir_ * 0.012 * k, lace_frac=p.get('lace_frac'))
        o = pr.finish()
        w = o.matrix_world.copy()
        o.parent, o.parent_type, o.parent_bone = arm, 'BONE', 'pelvis'
        o.matrix_world = w
        out.append(o)
    if p.get('sode', True):
        pr = props.Prop('sode', {}, {'tint': {'grime': 0.35}})
        sw, sn, sh_ = p.get('sode_w', 0.28) * k, p.get('sode_n', 6), p.get('sode_h', 0.064) * k
        for side, sx in (('l', -1.0), ('r', 1.0)):
            ua = arm.pose.bones['upperarm_' + side]
            sh = mw @ ua.head
            lat = Vector((sx, 0.0, 0.0))
            sw_back = math.radians(p.get('swing_' + side, 0.0))
            # the board's plane: across it 'along' (front to back), down it hangs out by sode_out degrees
            along = Vector((sx * math.sin(sw_back), math.cos(sw_back), 0.0)) * -1.0   # front to back
            side_n = Vector((sx * math.cos(sw_back), -math.sin(sw_back) * sx * sx, 0.0)).normalized()
            so = math.radians(p.get('sode_out', 14.0))
            down = (Vector((0, 0, -math.cos(so))) + side_n * math.sin(so)) * sh_
            c0 = sh + lat * p.get('sode_x', 0.06) * k + Vector((0, 0, p.get('sode_z', 0.05) * k))
            top = []
            for i in range(9):
                u = -0.5 + i / 8
                bow_ = 0.03 * k * (1 - (2 * u) ** 2)   # curved round the arm
                top.append(c0 + along * (u * sw + p.get('sode_back', 0.02) * k) + side_n * bow_)
            # the kanmuri-ita: a solid lacquered top plate
            pr.add(_lame_band([q + Vector((0, 0, 0.012 * k)) for q in top], Vector((0, 0, -0.03 * k)), 0.012 * k),
                   'yoroi_lacquer', smooth=True)
            _panel(pr, top, down, sn, lk, lt, k, overlap=0.15, outward=side_n * 0.006 * k, lace_frac=p.get('lace_frac'))
        o = pr.finish()
        w = o.matrix_world.copy()
        o.parent, o.parent_type, o.parent_bone = arm, 'BONE', 'spine_03'
        o.matrix_world = w
        out.append(o)
    H._update()
    return out


# ------------------------------------------------------------ stirrups and saddle

def abumi(root, arm, seat, k, p=None):
    """The Japanese slipper stirrups (abumi) under the posed feet: a long
    flat tread under the whole foot, a hooded toe, a swan neck rising to the
    leathers from the saddle; black lacquer (inside red, unseen)."""
    import vkit
    p = dict(p or {})
    mw = arm.matrix_world
    pr = vkit.part(root, 'abumi', {'yoroi_lacquer': p.get('color', '#1e1a16')})
    for s, sx in (('l', -1.0), ('r', 1.0)):
        pb = arm.pose.bones['foot_' + s]
        heel, toe = mw @ pb.head, mw @ pb.tail
        fwd = Vector((toe.x - heel.x, toe.y - heel.y, 0.0))
        fwd = fwd.normalized() if fwd.length > 1e-6 else Vector((0, 1, 0))
        z = min(heel.z, toe.z) - 0.035 * k
        mid = (heel + toe) / 2
        c = Vector((mid.x, mid.y, z))
        L, W = 0.3 * k, 0.085 * k
        lat = Vector((fwd.y, -fwd.x, 0.0))
        # the tread: a long shallow tray
        tray = [c + fwd * (u * L / 2) for u in (-1.0, -0.6, 0.0, 0.6, 1.0)]
        pr.add(props.tube(tray, [W * 0.75, W, W, W * 0.9, W * 0.6], seg=8, scale_xy=(1.0, 0.25)), 'yoroi_lacquer', smooth=True)
        # the hood over the toes
        hood = [c + fwd * (L * 0.18) + Vector((0, 0, 0.02 * k)), c + fwd * (L * 0.4) + Vector((0, 0, 0.06 * k)),
                c + fwd * (L * 0.5) + Vector((0, 0, 0.03 * k))]
        pr.add(props.tube(hood, [W * 0.95, W * 0.9, W * 0.6], seg=10, scale_xy=(1.0, 0.75)), 'yoroi_lacquer', smooth=True)
        # the neck: from the tread's middle up the inside of the foot to the leathers
        top = Vector(seat) + Vector((sx * 0.2, 0.02, -0.06))
        neck0 = c + fwd * (L * 0.05) - lat * sx * 0.0 + Vector((0, 0, 0.04 * k))
        neck1 = neck0 + Vector((0, 0, 0.16 * k))
        pr.add(props.tube([neck0, neck1], 0.016 * k, seg=6), 'yoroi_lacquer', smooth=True)
        pr.add(props.tube([neck1, top], 0.013 * k, seg=6, scale_xy=(1.0, 0.4)), 'leather_dark', smooth=True)
    return pr.finish()


def kura(p, ys, top, h):
    """The Japanese saddle (kura) on asset_tack's part `p`: a wooden tree
    with a tall arched front (maewa) and a taller, back-raked rear arch
    (shizuwa), lacquered (`h['kura']` the colour key), with gilt rims."""
    key = h.get('kura')
    gold = h.get('kura_rim', 'yoroi_gold')
    for y, lean, hgt, w in ((ys + 0.19, 0.18, 0.11, 0.15), (ys - 0.21, -0.4, 0.14, 0.17)):
        rows = []
        for kk in range(11):
            u = -1 + 2 * kk / 10
            a = math.acos(max(-1.0, min(1.0, u)))
            x = w * math.cos(a)
            z = top + 0.04 + hgt * math.sin(a) ** 0.7
            yy = y + lean * (z - top) * 0.4
            rows.append((x, yy, z))
        inner = [(x * 0.7, yy, top + 0.02 + (z - top) * 0.35) for x, yy, z in rows]
        rings = [[Vector(a), Vector(b)] for a, b in zip(rows, inner)]
        bm = props.loft([[r[0] for r in rings], [r[1] for r in rings]], False, False)
        props.bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.035)
        p.add(bm, key, smooth=True)
        p.add(props.tube([Vector(q) + Vector((0, 0, 0.006)) for q in rows], 0.008, seg=6), gold, smooth=True)


# ------------------------------------------------------------ props

def yumi(p, height=2.2, grip=0.33, draw=0.0, bend=0.11, recurve=0.06, thick=1.0, scale=1.0, string_r=0.004, bands=7):
    """The yumi: a tall laminated bamboo longbow held a third of the way up
    (`grip`), so the upper limb towers above the archer; black lacquer with
    pale rattan bindings (shigeto), a leather grip, a pale string. The
    origin is the grip; limbs along Z bending back toward the archer (-Y)."""
    h = height * scale
    n = 25
    zg = -h / 2 + grip * h            # the grip's height on the symmetric bow; it is moved to the origin
    pts = []
    for i in range(n):
        u = -1 + 2 * i / (n - 1)
        z = u * h / 2 * (1 - 0.05 * draw)
        # the asymmetric curve: the upper limb longer and flatter about the grip
        ug = (z - zg) / (h / 2 - zg if z > zg else h / 2 + zg)
        y = -(bend + 0.08 * draw) * ug * ug
        if abs(u) > 0.86:
            y += recurve * ((abs(u) - 0.86) / 0.14) ** 2
        pts.append((0.0, y, z - zg))
    radii = [(0.007 + 0.01 * (1 - abs(-1 + 2 * i / (n - 1))) ** 0.6) * thick for i in range(n)]
    p.add(props.tube(pts, radii, seg=10, scale_xy=(1.0, 0.8)), 'yumi_lacquer', smooth=True)
    for j in range(bands):   # rattan bindings along both limbs
        i = int(round(2 + (n - 5) * j / max(1, bands - 1)))
        if abs(pts[i][2]) < 0.1:
            continue
        q0, q1 = Vector(pts[i]), Vector(pts[min(n - 1, i + 1)])
        mid = q0.lerp(q1, 0.3)
        d = (q1 - q0).normalized() * 0.011
        p.add(props.tube([mid - d, mid + d], radii[i] * 1.2, seg=10, scale_xy=(1.0, 0.85)), 'yumi_rattan', smooth=True)
    p.add(props.tube([(0, -0.002, -0.07), (0, -0.002, 0.08)], 0.019 * thick, seg=12), 'yumi_grip', smooth=True)
    top, bot = pts[-1], pts[0]
    mid = (0.0, -0.04 - 0.55 * draw, 0.0)
    p.add(props.tube([bot, mid, top], string_r, seg=5), 'string', smooth=True)


def ebira(p, arrows=7, scale=1.0, side=1.0, drop=0.52, fan=1.0):
    """The ebira, worn on the back at the right hip (back gear convention:
    the origin at the shoulders, behind is -Y, the rider's right +X): an
    open lacquered box frame low behind the right hip, its arrows standing up
    in a fan, the eagle-feather fletching (pale, barred dark) high behind the
    right shoulder."""
    s = scale
    bx, by, bz = 0.15 * side * s, -0.16 * s, -drop * s
    p.add(props.box(0.15 * s, 0.07 * s, 0.05 * s), 'ebira_wood', T(bx, by, bz - 0.12 * s), bevel=0.006)   # the box
    for dx in (-0.06, 0.06):   # the frame's side posts
        p.add(props.tube([(bx + dx * s, by, bz - 0.12 * s), (bx + dx * s * 0.8, by + 0.01 * s, bz + 0.12 * s)], 0.009 * s, seg=6),
              'ebira_wood', smooth=True)
    p.add(props.tube([(bx - 0.06 * s, by + 0.01 * s, bz + 0.1 * s), (bx + 0.06 * s, by + 0.01 * s, bz + 0.1 * s)],
                     0.01 * s, seg=6), 'ebira_wood', smooth=True)                         # the cross bar holding the shafts
    for i in range(arrows):
        f = -1 + 2 * i / max(1, arrows - 1)
        a = math.radians(12 * fan * f)
        base = Vector((bx + 0.045 * s * f, by - 0.005 * s * abs(f), bz - 0.1 * s))
        d = Vector((math.sin(a) * side, -0.12, math.cos(a))).normalized()
        tip = base + d * 0.86 * s
        p.add(props.tube([base, tip], 0.0065 * s, seg=5), 'wood', smooth=True)
        for fv in (0, 90):   # two crossed vanes: pale feather with dark bars
            vane = props.tube([tip - d * 0.17 * s, tip - d * 0.01 * s], [0.012 * s, 0.026 * s], seg=4,
                              scale_xy=(1.0, 0.18) if fv == 0 else (0.18, 1.0))
            p.add(vane, 'taka_feather', smooth=True)
        for bar in (0.05, 0.11):
            q = tip - d * bar * s
            p.add(props.tube([q - d * 0.012 * s, q + d * 0.012 * s], 0.024 * s, seg=6, scale_xy=(1.0, 0.25)), 'taka_bar', smooth=True)


def tachi(p, length=0.66, curve=0.06, width=0.034, scale=1.0, back=0.008, bright=False):
    """A tachi: a deeply curved single-edged blade (the curve near the
    hilt), matte steel with a dark line along the edge (the hamon read at
    1x), a round iron tsuba, a long dark-wrapped grip; held at the origin,
    the blade along +Z, its edge +Y."""
    props.DEFAULTS.setdefault('tachi_steel', ('#8e949c', 0.42, 0.85, 'metal'))
    props.DEFAULTS.setdefault('tachi_edge', ('#2e3034', 0.5, 0.6, 'metal'))
    props.DEFAULTS.setdefault('tachi_iron', ('#2a2826', 0.5, 0.8, 'metal'))
    props.DEFAULTS.setdefault('tachi_grip', ('#1e1a18', 0.7, 0.0, 'leather'))
    L = length * scale
    n = 14
    rings, edge = [], []
    for i in range(n + 1):
        t = i / n
        z = 0.06 + L * t
        bow = -curve * scale * (1 - (1 - t) ** 2)   # the koshi-zori: the bend nearest the hilt
        hw = width / 2 * scale * (1.0 - 0.3 * t if t < 0.88 else max(0.45, (1 - t) / 0.12 * 0.7))
        bk = back * scale * (1 - 0.5 * t)
        rings.append([(0.0, bow - hw, z), (bk, bow + hw * 0.5, z), (0.0, bow + hw, z), (-bk, bow + hw * 0.5, z)])
        edge.append((0.0, bow + hw * 0.92, z))
    if bright:   # (R3) lit polished steel: it reads at 1x against the horse
        props.DEFAULTS.setdefault('tachi_bright', ('#d4d8de', 0.22, 0.9, 'metal'))
    p.add(props.loft(rings), 'tachi_bright' if bright else 'tachi_steel', bevel=0.0006, angle=50.0)
    p.add(props.tube(edge[:-1], 0.0045 * scale, seg=5, scale_xy=(1.0, 0.6)), 'tachi_edge', smooth=True)
    p.add(props.lathe([(0.0, 0.042), (0.01, 0.042)], seg=20), 'tachi_iron', T(0, 0, 0.05), bevel=0.002)   # the tsuba
    p.add(props.tube([(0, 0, -0.24), (0, 0, 0.05)], [0.017, 0.019], seg=10, scale_xy=(0.8, 1.0)), 'tachi_grip', smooth=True)
    p.add(props.ellipsoid(0.02, 0.017, 0.014, seg=12), 'tachi_iron', T(0, 0, -0.245), smooth=True)   # the kabuto-gane


props.BUILDERS.setdefault('tachi', tachi)
props.BUILDERS.setdefault('yumi', yumi)
props.BUILDERS.setdefault('ebira', ebira)


# ------------------------------------------------------------ poses

# the yabusame shot: upright, standing a little in the long stirrups, the torso turned to the left, the yumi
# held up at arm's length out to the left (umate, the archer's left), the right hand drawn back past the ear
riders.RIDE_POSES.setdefault('yabusame', {
    'spine': [('spine_01', (0, 0.06, 1)), ('spine_02', (0, 0.1, 1)), ('spine_03', (0, 0.12, 1))],
    'twist': [('spine_02', 10.0), ('spine_03', 16.0)],
    'clavicle_l': (1, -0.12, 0.12), 'clavicle_r': (-1, 0.12, 0.12),
    'neck': (0, 0.1, 1), 'head': (0, 0.3, 1), 'head_twist': 30.0,
    'thigh': (0.3, -0.5, -0.8), 'calf': (0.08, 0.12, -1), 'foot': (0.18, -1, 0.2),
    'hand_l': (0.5, -0.28, 0.62), 'hand_r': (-0.06, -0.12, 0.7), 'elbow_l': 0.15, 'elbow_r': 0.95,
})
# (round 2) the ready hold: the yumi upright at arm's length out to the left front at chest height, the right hand
# on the string at the nock beside it, the arrow nocked and pointing forward-left: the face clear in every view
riders.RIDE_POSES.setdefault('yumi_ready', {
    'spine': [('spine_01', (0, 0.08, 1)), ('spine_02', (0, 0.12, 1)), ('spine_03', (0, 0.14, 1))],
    'twist': [('spine_02', 6.0), ('spine_03', 10.0)],
    'clavicle_l': (1, -0.14, 0.08), 'clavicle_r': (-1, 0.1, 0.04),
    'neck': (0, 0.1, 1), 'head': (0, 0.3, 1), 'head_twist': 12.0,
    'thigh': (0.3, -0.5, -0.8), 'calf': (0.08, 0.12, -1), 'foot': (0.18, -1, 0.2),
    'hand_l': (0.74, -0.06, 0.56), 'hand_r': (0.36, -0.16, 0.46), 'elbow_l': 0.15, 'elbow_r': 0.7,   # (R3) the bow further out to the side
})
# the tachi held out low to the right, its point forward (the shipped sprite's carry); reins in the left hand
riders.RIDE_POSES.setdefault('tachi', {
    'spine': [('spine_01', (0, 0.08, 1)), ('spine_02', (0, 0.12, 1)), ('spine_03', (0, 0.14, 1))],
    'twist': [('spine_02', -6.0), ('spine_03', -10.0)],
    'clavicle_l': (1, -0.16, 0.04), 'clavicle_r': (-1, 0.0, 0.1),
    'neck': (0, 0.12, 1), 'head': (0, 0.3, 1), 'head_twist': 6.0,
    'thigh': (0.3, -0.5, -0.8), 'calf': (0.08, 0.12, -1), 'foot': (0.18, -1, 0.2),
    'hand_l': (0.08, -0.32, 0.12), 'hand_r': (-0.52, -0.08, 0.5), 'elbow_l': 0.45, 'elbow_r': 0.6,   # (round 2) the sword hand at the chest, out right
})
