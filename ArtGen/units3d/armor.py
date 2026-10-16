"""Armored vehicles for the Blender remaster: Tank, Panzer, Modern Armor,
Mech Infantry, Mobile SAM, Radar Artillery.

    root = armor.build('Tank', colors)

A shared chassis (a belt of track links wrapped round the road wheels,
sprocket, idler and return rollers; a lower hull between the tracks; a
full-width upper hull over them with its glacis; fenders, optional side
skirts) and one builder per unit for its turret or superstructure and its
kit: hatches, periscopes, lights, tools, jerrycans, grilles, smoke
launchers, antennas, machine guns. Camouflage is Perlin noise keyed per
face over finely gridded panels; Civ3's civ markings (stars, crosses, E
brackets, C arcs, chevrons, patches) are bold painted plates. Model space as
every unit: facing +Y, ground at z = 0.
"""
import math

import bpy
from mathutils import Vector, noise

import props
import vkit
from props import R, T

bmesh = props.bmesh

for _k, _v in {'armor_paint': ('#7a6e4a', 0.7, 0.3, 'metal'), 'armor_camo2': ('#5a5236', 0.7, 0.3, 'metal'),
               'armor_camo3': ('#9a8e64', 0.7, 0.3, 'metal'), 'armor_camo4': ('#4a4434', 0.7, 0.3, 'metal'),
               'armor_shade': ('#4a4430', 0.75, 0.3, 'metal'), 'turret_paint': ('#4a4c4e', 0.65, 0.4, 'metal'),
               'track': ('#3a3632', 0.6, 0.8, 'metal'), 'track_pin': ('#6a6660', 0.4, 1.0, 'metal'),
               'rubber': ('#1e1e1e', 0.85, 0.0, 'leather'), 'gunmetal': ('#3a3c3e', 0.45, 1.0, 'metal'),
               'canvas': ('#7a7254', 0.9, 0.0, 'leather'), 'mark_red': ('#8a3424', 0.6, 0.1, 'metal'),
               'mark_white': ('#e4e2da', 0.6, 0.1, 'metal'), 'lens': ('#e8e4c8', 0.2, 0.3, 'polished'),
               'missile_white': ('#ecebe6', 0.45, 0.2, 'metal'), 'missile_red': ('#b0302a', 0.45, 0.2, 'metal'),
               'port': ('#0e0c0a', 0.9, 0.0, 'polished')}.items():
    props.DEFAULTS.setdefault(_k, _v)
WEAR = {'armor_paint': {'grime': 0.5, 'soot': 0.25, 'edges': 0.5, 'rust': 0.06}, 'armor_camo2': {'grime': 0.5},
        'armor_camo3': {'grime': 0.45}, 'armor_camo4': {'grime': 0.5}, 'armor_shade': {'grime': 0.6, 'soot': 0.4},
        'turret_paint': {'grime': 0.45, 'edges': 0.4}, 'track': {'grime': 0.8, 'rust': 0.25},
        'rubber': {'grime': 0.6}, 'gunmetal': {'grime': 0.5, 'soot': 0.4}, 'canvas': {'grime': 0.5}}
PAINT = ('armor_paint', 'armor_camo2', 'armor_camo3', 'armor_camo4')


def _hex(c, f):
    c = c.lstrip('#')
    return '#' + ''.join(f'{max(0, min(255, int(int(c[i:i + 2], 16) * f))):02x}' for i in (0, 2, 4))


# ---------------------------------------------------------------- geometry

def _grid(bm, cell):
    """Cut a mesh into roughly cell-sized faces along each axis (so camo and
    weathering have faces to land on)."""
    for axis in range(3):
        cs = [v.co[axis] for v in bm.verts]
        lo, hi = min(cs), max(cs)
        n = int((hi - lo) / cell)
        no = Vector([1.0 if i == axis else 0.0 for i in range(3)])
        for i in range(1, n + 1):
            co = Vector((0, 0, 0))
            co[axis] = lo + (hi - lo) * i / (n + 1)
            bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=co, plane_no=no)
    return bm


def plate(sx, sy, sz, cell=0.12, front=0.0, front_low=0.0, rear=0.0, rear_low=0.0, taper=0.0,
          plan_front=0.0, plan_rear=0.0, taper_low=0.0):
    """A gridded box shaped into armor: front/rear pull the top edge back
    (a glacis / a sloped rear plate), *_low pull the bottom edge in (a nose),
    taper leans the sides in toward the top (taper_low toward the bottom),
    plan_* narrow the front/rear in plan (turret cheeks)."""
    bm = _grid(props.box(sx, sy, sz), cell)
    hx, hy, hz = sx / 2, sy / 2, sz / 2
    for v in bm.verts:
        t = (v.co.z + hz) / sz
        f = v.co.y / hy
        if f > 0:
            v.co.x *= 1 - plan_front * f
            v.co.y *= 1 - front * t - front_low * (1 - t)
        else:
            v.co.x *= 1 + plan_rear * f
            v.co.y *= 1 - rear * t - rear_low * (1 - t)
        v.co.x *= 1 - taper * t - taper_low * (1 - t)
    return bm


def _q(normal):
    return Vector(normal).normalized().to_track_quat('Z', 'Y').to_matrix().to_4x4()


def _hull2d(pts):
    pts = sorted(set(pts))
    if len(pts) < 3:
        return pts

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lower, upper = [], []
    for q in pts:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], q) <= 0:
            lower.pop()
        lower.append(q)
    for q in reversed(pts):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], q) <= 0:
            upper.pop()
        upper.append(q)
    return lower[:-1] + upper[:-1]   # counter-clockwise


# ---------------------------------------------------------------- markings

def _shape(kind, s):
    if kind == 'star':
        return [[((s if i % 2 == 0 else s * 0.42) * math.cos(math.pi / 2 + i * math.pi / 5),
                  (s if i % 2 == 0 else s * 0.42) * math.sin(math.pi / 2 + i * math.pi / 5)) for i in range(10)]]
    if kind == 'cross':
        a = s * 0.3
        return [[(-a, s), (a, s), (a, a), (s, a), (s, -a), (a, -a), (a, -s), (-a, -s), (-a, -a), (-s, -a), (-s, a), (-a, a)]]
    if kind == 'E':   # a bracket with a middle bar
        w = s * 0.32
        return [[(-0.7 * s, -s), (0.7 * s, -s), (0.7 * s, -s + w), (-0.7 * s + w, -s + w), (-0.7 * s + w, -w / 2),
                 (0.35 * s, -w / 2), (0.35 * s, w / 2), (-0.7 * s + w, w / 2), (-0.7 * s + w, s - w), (0.7 * s, s - w),
                 (0.7 * s, s), (-0.7 * s, s)]]
    if kind == 'C':   # a thick arc opening forward
        out, inn = [], []
        for i in range(17):
            a = math.radians(40 + 280 * i / 16)
            out.append((s * math.cos(a), s * math.sin(a)))
            inn.append((s * 0.6 * math.cos(a), s * 0.6 * math.sin(a)))
        # two halves keep each n-gon simple
        return [out[:9] + inn[:9][::-1], out[8:] + inn[8:][::-1]]
    if kind == 'chevron':
        return [[(0, 0.6 * s), (s, -0.45 * s), (0.58 * s, -0.45 * s), (0, 0.1 * s)],
                [(0, 0.6 * s), (0, 0.1 * s), (-0.58 * s, -0.45 * s), (-s, -0.45 * s)]]
    if kind == 'rect':
        w, h = s
        return [[(-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)]]
    raise KeyError(kind)


def mark(p, kind, s, at, normal, key='tint', rot=0.0, thick=0.008):
    """A painted marking: an outline in the face's plane, its up toward +Z
    (or +Y on a deck)."""
    tint = key == 'tint'
    at = Vector(at) + Vector(normal).normalized() * thick * 0.6   # proud of the face
    m = T(*at) @ _q(normal) @ R('Z', rot) @ R('X', -90)
    for poly in _shape(kind, s):
        p.add(props.slab(poly, thick, 'Y'), key, m, tint=tint)


def cross_mark(p, s, at, normal):
    """A red-brown cross with a white outline (the Panzer's)."""
    n = Vector(normal).normalized()
    mark(p, 'cross', s * 1.25, at, normal, 'mark_white', thick=0.006)
    mark(p, 'cross', s, Vector(at) + n * 0.004, normal, 'mark_red', thick=0.006)


# ---------------------------------------------------------------- kit

def wheel(p, x, y, z, r, w, side, face='armor_shade', rubber=True, dual=True, teeth=0, holes=0):
    """A road wheel around X (a pair of discs either side of the guide horns
    when dual), its rubber tyre, dished face and hub; teeth make a sprocket."""
    rot = R('Y', 90 * side)
    offs = (-w * 0.27, w * 0.27) if dual else (0.0,)
    ww = w * (0.36 if dual else 0.8)
    for dx in offs:
        m = T(x + dx, y, z) @ rot
        if rubber:
            p.add(props.lathe([(-ww / 2, r * 0.96), (ww / 2, r * 0.96)], seg=20), 'rubber', m, smooth=True)
        p.add(props.lathe([(-ww / 2 - 0.004, r * 0.8), (ww / 2 + 0.004, r * 0.8), (ww / 2 + 0.012, r * 0.55)], seg=20),
              face, m, smooth=True)
        p.add(props.lathe([(ww / 2, r * 0.26), (ww / 2 + 0.03, r * 0.2)], seg=12), 'gunmetal', m, smooth=True)
        for k in range(holes):
            a = 2 * math.pi * k / holes
            p.add(props.lathe([(0, r * 0.1), (0.01, r * 0.1)], seg=8), 'armor_shade',
                  m @ T(r * 0.5 * math.cos(a), r * 0.5 * math.sin(a), ww / 2 + 0.008), smooth=True)
    for k in range(teeth):
        a = 360.0 * k / teeth
        p.add(props.box(w * 0.5, r * 0.22, r * 0.3), 'gunmetal', T(x, y, z) @ R('X', a) @ T(0, 0, r * 0.92), bevel=0.004)


def track(p, x, circles, tw, th=0.03, pitch=0.045):
    """One track: link plates (with cleats outside and guide horns inside)
    following the belt round the circles (cy, cz, r) of its wheels."""
    pts = []
    for cy, cz, r in circles:
        rr = r + th / 2
        for k in range(48):
            a = 2 * math.pi * k / 48
            pts.append((round(cy + rr * math.cos(a), 5), round(cz + rr * math.sin(a), 5)))
    loop = _hull2d(pts)
    segs = [(loop[i], loop[(i + 1) % len(loop)]) for i in range(len(loop))]
    total = sum(math.dist(a, b) for a, b in segs)
    n = max(8, int(total / pitch))
    step = total / n
    acc, i, pos = 0.0, 0, 0.0
    for k in range(n):
        d = k * step
        while acc + math.dist(*segs[i]) < d:
            acc += math.dist(*segs[i])
            i += 1
        (ay, az), (by, bz) = segs[i]
        seg_len = math.dist(*segs[i]) or 1e-6
        f = (d - acc) / seg_len
        py, pz = ay + (by - ay) * f, az + (bz - az) * f
        ang = math.degrees(math.atan2(bz - az, by - ay))
        m = T(x, py, pz) @ R('X', ang)   # local Y along the belt, local +Z inward
        p.add(props.box(tw, step * 0.84, th), 'track', m, bevel=0.004)
        p.add(props.box(tw * 0.96, step * 0.24, 0.012), 'track_pin', m @ T(0, -step * 0.2, -th / 2 - 0.004))
        if k % 2 == 0:
            p.add(props.box(0.022, step * 0.5, 0.03), 'track', m @ T(0, 0, th / 2 + 0.012))


def chassis(p, cfg, camo):
    """The tracks, lower and upper hull, fenders and skirts. Returns the
    numbers the turret builders need."""
    L, W, tw, rw = cfg['L'], cfg['W'], cfg['tw'], cfg['rw']
    th = 0.03
    xs = W / 2 - tw / 2 - 0.01
    n = cfg['wheels']
    y0 = -L / 2 + cfg.get('rear_gap', 0.22)
    y1 = L / 2 - cfg.get('front_gap', 0.22)
    road = [y0 + (y1 - y0) * i / (n - 1) for i in range(n)]
    zr = rw + th
    fy, fz, fr = cfg.get('front_wheel', (L / 2 - 0.1, zr + 0.08, 0.09))
    ry, rz, rr = cfg.get('rear_wheel', (-L / 2 + 0.1, zr + 0.06, 0.085))
    top = cfg.get('top', max(fz + fr, rz + rr, zr + rw) + 0.01)
    rollers = cfg.get('rollers', 3)
    roll_r = 0.035
    roll = [y0 + 0.1 + (y1 - y0 - 0.2) * (i + 0.5) / rollers for i in range(rollers)] if rollers else []
    circles = [(y, zr, rw) for y in road] + [(fy, fz, fr), (ry, rz, rr)] + [(y, top - roll_r, roll_r) for y in roll]
    sprocket = cfg.get('sprocket', 'front')
    for sx in (-1, 1):
        x = sx * xs
        track(p, x, circles, tw)
        if cfg.get('bogies'):   # Sherman VVSS: wheel pairs on brackets, a roller on each
            for i in range(0, n, 2):
                yc = (road[i] + road[i + 1]) / 2
                p.add(props.box(tw * 0.34, (road[i + 1] - road[i]) * 0.62, rw * 1.25), 'armor_paint',
                      T(x, yc, zr + rw * 0.35), bevel=0.012)
                for dy in (-0.05, 0.05):
                    vkit.rod(p, (x + sx * tw * 0.08, yc + dy, zr + rw * 0.4), (x + sx * tw * 0.08, yc + dy, zr + rw * 1.05),
                             0.022, 'gunmetal', seg=8)
        for y in road:
            wheel(p, x, y, zr, rw, tw, sx, holes=cfg.get('holes', 0))
        wheel(p, x, fy, fz, fr, tw, sx, rubber=False, teeth=12 if sprocket == 'front' else 0, face='armor_shade')
        wheel(p, x, ry, rz, rr, tw, sx, rubber=sprocket == 'front', teeth=12 if sprocket == 'rear' else 0, face='armor_shade')
        for y in roll:
            wheel(p, x, y, top - roll_r, roll_r, tw * 0.6, sx, dual=False)
    z_f = top + th / 2 + 0.008
    H = cfg['H']
    Lh, hy = cfg.get('Lh', L * 0.96), cfg.get('hull_y', 0.0)
    # the lower hull between the tracks (its nose sloped)
    low_h = z_f - rw * 0.7
    p.add(plate(W - 2 * tw - 0.04, Lh * 0.96, low_h, front_low=cfg.get('nose', 0.12), rear_low=0.06),
          'armor_paint', T(0, hy, rw * 0.7 + low_h / 2), bevel=0.01, keys=camo)
    # the upper hull over the tracks
    up = cfg.get('upper', {})
    p.add(plate(W, Lh, H, **up), 'armor_paint', T(0, hy, z_f + H / 2), bevel=0.014, keys=camo)
    # fenders over the track ends beyond the hull
    for sx in (-1, 1):
        p.add(props.box(tw + 0.02, L * 1.0, 0.014), 'armor_paint', T(sx * xs, 0, z_f - 0.004), keys=camo)
        for yy in (L / 2, -L / 2):   # the mudguard lips
            p.add(props.box(tw + 0.02, 0.012, 0.06), 'armor_paint', T(sx * xs, yy, z_f - 0.03), bevel=0.003)
    if cfg.get('skirt'):
        sh = z_f + 0.01 - rw * 1.0
        for sx in (-1, 1):
            p.add(plate(0.025, L * 0.98, sh, front_low=0.05, rear_low=0.03), 'armor_paint',
                  T(sx * (W / 2 + 0.014), 0, rw * 1.0 + sh / 2), bevel=0.006, keys=camo)
            for k in range(1, 7):   # panel seams
                p.add(props.box(0.006, 0.008, sh * 0.94), 'armor_shade',
                      T(sx * (W / 2 + 0.028), -L * 0.49 + L * 0.98 * k / 7, rw * 1.0 + sh / 2))
            for k in range(14):   # bolt heads along the top
                p.add(props.box(0.008, 0.014, 0.014), 'gunmetal',
                      T(sx * (W / 2 + 0.03), -L * 0.47 + L * 0.94 * k / 13, z_f - 0.01))
    deck = z_f + H
    fr_ = up.get('front', 0.0)
    glacis_y = hy + Lh / 2 * (1 - fr_)   # where the glacis meets the deck
    gn = Vector((0, H, Lh / 2 * fr_)).normalized() if fr_ else Vector((0, 1, 0))
    return dict(L=L, W=W, H=H, Lh=Lh, hy=hy, z_f=z_f, deck=deck, glacis_y=glacis_y, gn=gn, tw=tw, xs=xs)


def hatch(p, x, y, z, r, key='armor_paint', normal=(0, 0, 1), periscopes=0):
    m = T(x, y, z) @ _q(normal)
    p.add(props.lathe([(0, r), (0.022, r * 0.96), (0.032, r * 0.7)], seg=18), key, m, smooth=True)
    p.add(props.box(r * 0.5, 0.03, 0.02), 'gunmetal', m @ T(0, -r * 0.9, 0.012), bevel=0.004)   # hinge
    p.add(props.box(r * 0.12, r * 0.9, 0.012), 'gunmetal', m @ T(r * 0.3, 0, 0.036))           # handle
    for k in range(periscopes):
        a = 2 * math.pi * k / periscopes
        periscope(p, m @ Vector((r * 1.1 * math.cos(a), r * 1.1 * math.sin(a), 0.0)), math.degrees(a) - 90)


def periscope(p, at, yaw):
    m = T(*at) @ R('Z', yaw)
    p.add(props.box(0.05, 0.035, 0.035), 'armor_shade', m @ T(0, 0, 0.017), bevel=0.004)
    p.add(props.box(0.04, 0.004, 0.016), 'port', m @ T(0, 0.018, 0.02))


def light(p, x, y, z, r=0.028, guard=True):
    p.add(props.lathe([(0, r), (0.05, r), (0.055, r * 0.85)], seg=12), 'armor_shade', T(x, y, z) @ R('X', -90), smooth=True)
    p.add(props.lathe([(0, r * 0.8), (0.004, r * 0.75)], seg=12), 'lens', T(x, y + 0.054, z) @ R('X', -90), smooth=True)
    if guard:
        vkit.rod(p, (x - r * 1.4, y - 0.01, z - r * 1.3), (x - r * 1.4, y + 0.075, z + r * 0.4), 0.005, 'gunmetal', seg=5)
        vkit.rod(p, (x + r * 1.4, y - 0.01, z - r * 1.3), (x + r * 1.4, y + 0.075, z + r * 0.4), 0.005, 'gunmetal', seg=5)
        vkit.rod(p, (x - r * 1.4, y + 0.075, z + r * 0.4), (x + r * 1.4, y + 0.075, z + r * 0.4), 0.005, 'gunmetal', seg=5)


def jerrycan(p, x, y, z, yaw=0.0, key='armor_camo2'):
    m = T(x, y, z) @ R('Z', yaw)
    p.add(props.box(0.1, 0.04, 0.13), key, m @ T(0, 0, 0.065), bevel=0.008)
    p.add(props.box(0.06, 0.044, 0.008), 'armor_shade', m @ T(0, 0, 0.07))
    p.add(props.box(0.02, 0.02, 0.025), 'gunmetal', m @ T(0.03, 0, 0.14))


def tools(p, x, y0, y1, z, side):
    """A shovel, an axe and a crowbar strapped along a hull side."""
    L = y1 - y0
    vkit.rod(p, (x, y0, z), (x, y0 + L * 0.6, z), 0.01, 'wood', seg=6)
    p.add(props.box(0.012, 0.09, 0.07), 'gunmetal', T(x, y0 + L * 0.66, z), bevel=0.004)          # the spade
    vkit.rod(p, (x, y0 + L * 0.3, z - 0.03), (x, y1, z - 0.03), 0.009, 'wood', seg=6)
    p.add(props.box(0.012, 0.03, 0.07), 'gunmetal', T(x, y1 - 0.02, z - 0.02), bevel=0.003)       # the axe head
    vkit.rod(p, (x, y0 - 0.02, z + 0.03), (x, y1 - L * 0.2, z + 0.03), 0.006, 'gunmetal', seg=5)
    for yy in (y0 + L * 0.15, y0 + L * 0.5, y0 + L * 0.85):   # the brackets
        p.add(props.box(0.02, 0.012, 0.09), 'armor_shade', T(x - side * 0.004, yy, z))


def grille(p, cx, cy, sx, sy, z, n=8, key='armor_shade'):
    """Engine-deck louvres: a sunk frame with n slats across."""
    p.add(props.box(sx, sy, 0.006), 'gunmetal', T(cx, cy, z + 0.003))
    for k in range(n):
        p.add(props.box(sx * 0.94, sy / n * 0.45, 0.014), key, T(cx, cy - sy / 2 + sy * (k + 0.5) / n, z + 0.008))


def mg(p, at, yaw=0.0, length=0.32, pitch=4.0, shield=False):
    """A pintle machine gun: receiver, barrel, ammo box (optional shield)."""
    m = T(*at) @ R('Z', yaw)
    vkit.rod(p, m @ Vector((0, 0, -0.08)), m @ Vector((0, 0, 0.0)), 0.012, 'gunmetal', seg=6)
    g = m @ R('X', pitch)
    p.add(props.box(0.04, 0.14, 0.05), 'gunmetal', g @ T(0, 0, 0.02), bevel=0.006)
    p.add(props.lathe([(0, 0.011), (length, 0.009)], seg=8), 'gunmetal', g @ T(0, 0.07, 0.025) @ R('X', -90), smooth=True)
    p.add(props.box(0.05, 0.06, 0.05), 'armor_camo2', g @ T(0.045, -0.01, 0.0), bevel=0.006)
    if shield:
        p.add(props.box(0.18, 0.012, 0.12), 'armor_paint', g @ T(0, 0.12, 0.04), bevel=0.004)


def antenna(p, x, y, z, h=0.65):
    p.add(props.lathe([(0, 0.022), (0.04, 0.018)], seg=10), 'gunmetal', T(x, y, z), smooth=True)
    vkit.rod(p, (x, y, z + 0.04), (x - 0.02, y - 0.04, z + h), 0.0045, 'gunmetal', seg=4)


def smoke(p, at, yaw, n=4):
    """A bank of smoke-grenade tubes."""
    m = T(*at) @ R('Z', yaw)
    for k in range(n):
        p.add(props.lathe([(0, 0.018), (0.07, 0.018)], seg=8), 'armor_shade',
              m @ T(-0.04 * (n - 1) / 2 + 0.04 * k, 0, 0) @ R('X', -60), smooth=True)


def gun(p, at, length, r, muzzle=None, sleeve=False, elev=2.0):
    """A main gun along +Y from its trunnion: barrel, an optional fume
    extractor / thermal sleeve and muzzle brake."""
    m = T(*at) @ R('X', -90 + elev)
    p.add(props.lathe([(0, r * 1.15), (length * 0.25, r), (length, r * 0.9)], seg=14), 'armor_paint', m, smooth=True)
    if sleeve:
        p.add(props.lathe([(length * 0.45, r * 0.9), (length * 0.5, r * 1.45), (length * 0.62, r * 1.45),
                           (length * 0.66, r * 0.9)], seg=14), 'armor_shade', m, smooth=True)
        for k in range(3):
            p.add(props.lathe([(length * (0.2 + 0.1 * k), r * 1.08), (length * (0.21 + 0.1 * k), r * 1.08)], seg=14),
                  'armor_shade', m, smooth=True)
    if muzzle == 'brake':
        p.add(props.lathe([(length, r * 1.5), (length + 0.05, r * 1.5), (length + 0.055, r * 1.1),
                           (length + 0.085, r * 1.1), (length + 0.09, r * 1.5), (length + 0.13, r * 1.5)], seg=14),
              'gunmetal', m, smooth=True)
    elif muzzle == 'ring':
        p.add(props.lathe([(length - 0.02, r * 1.2), (length, r * 1.2)], seg=14), 'gunmetal', m, smooth=True)


def stowage(p, at, size, key='canvas', yaw=0.0, straps=2):
    m = T(*at) @ R('Z', yaw)
    sx, sy, sz = size
    p.add(props.box(sx, sy, sz), key, m @ T(0, 0, sz / 2), bevel=min(sx, sy, sz) * 0.25)
    for k in range(straps):
        x = -sx / 2 + sx * (k + 1) / (straps + 1)
        p.add(props.box(0.012, sy * 1.04, sz * 1.04), 'armor_shade', m @ T(x, 0, sz / 2))


def camo_keys(scheme):
    """scheme: [(key, freq, threshold, seed)], the first whose noise passes
    paints the face; None leaves the base paint."""
    if not scheme:
        return None

    def keys(f):
        c = f.calc_center_median()
        for key, freq, th, seed in scheme:
            if noise.noise(c * freq + Vector((seed, seed * 1.7, seed * 0.3))) > th:
                return (key, False)
        return None
    return keys


def band_keys(z0, z1, inner):
    def keys(f):
        if z0 < f.calc_center_median().z < z1:
            return ('tint', True)
        return inner(f) if inner else None
    return keys


# ---------------------------------------------------------------- units

def _tank(p, cfg, d, camo):
    """An M4 Sherman: the cast turret with the wide civ band, a stubby
    75 mm gun in its mantlet, the commander's cupola and .50 cal, the
    glacis with hatches, lights and bow MG, an engine deck, tools and kit."""
    deck, W, H, hy, Lh = d['deck'], d['W'], d['H'], d['hy'], d['Lh']
    ty = hy + 0.02
    rx, ry, rz = 0.4, 0.42, 0.33
    tur = props.ellipsoid(rx, ry, rz, seg=32)
    for v in tur.verts:
        if v.co.z < 0:
            v.co.z *= 0.2
        if v.co.z > rz * 0.72:
            v.co.z = rz * 0.72 + (v.co.z - rz * 0.72) * 0.25
        if v.co.y < -ry * 0.4:   # the bustle drawn back and low
            v.co.y *= 1.12
    tz = deck + 0.04
    p.add(tur, 'armor_paint', T(0, ty, tz), smooth=True, keys=band_keys(tz + 0.07, tz + 0.2, camo))
    p.add(props.lathe([(0, rx * 0.95), (0.045, rx * 0.95)], seg=32), 'armor_shade', T(0, ty, deck), smooth=True)
    roof = tz + rz * 0.74
    # mantlet, gun and coax
    p.add(plate(0.3, 0.1, 0.17, cell=0.04, taper=0.1), 'armor_paint', T(0, ty + ry * 0.95, tz + 0.1), bevel=0.03, keys=camo)
    gun(p, (0, ty + ry * 0.95 + 0.05, tz + 0.1), 0.72, 0.038, muzzle='ring')
    vkit.rod(p, (0.09, ty + ry + 0.03, tz + 0.11), (0.09, ty + ry + 0.12, tz + 0.11), 0.01, 'gunmetal', seg=6)
    # cupola, loader's hatch, periscopes, .50 cal, lifting rings
    hatch(p, 0.13, ty - 0.08, roof, 0.1, periscopes=6)
    hatch(p, -0.14, ty - 0.02, roof - 0.005, 0.075)
    mg(p, (0.13, ty - 0.2, roof + 0.11), yaw=15, length=0.36, pitch=6)
    for x in (-0.2, 0.22):
        vkit.rod(p, (x, ty + 0.2, roof - 0.02), (x, ty + 0.25, roof - 0.02), 0.012, 'gunmetal', seg=6)
    # glacis: driver's and bow gunner's hatches, the bow MG ball, lights with guards, lifting eyes
    gy = d['glacis_y']
    for x in (-0.2, 0.2):
        hatch(p, x, gy - 0.08, deck, 0.07, periscopes=0)
        periscope(p, (x, gy - 0.01, deck), 0)
    gn = d['gn']
    p.add(props.ellipsoid(0.045, 0.03, 0.045, seg=12), 'armor_paint', T(0.18, hy + Lh / 2 - 0.12, d['z_f'] + H * 0.45) @ _q(gn), smooth=True)
    vkit.rod(p, (0.18, hy + Lh / 2 - 0.1, d['z_f'] + H * 0.45), (0.18, hy + Lh / 2 + 0.03, d['z_f'] + H * 0.43), 0.01, 'gunmetal', seg=6)
    for x in (-W * 0.36, W * 0.36):
        light(p, x, hy + Lh / 2 - 0.08, d['z_f'] + H * 0.3)
    for k in range(3):   # spare track links across the nose
        q = T(0, hy + Lh / 2 + 0.005, d['z_f'] - 0.035) @ R('X', 70)
        p.add(props.box(W * 0.12, 0.05, 0.02), 'track', q @ T(-W * 0.14 + W * 0.14 * k, 0, 0), bevel=0.004)
    # engine deck: grille doors, the stowage box, a tarp roll, jerrycans
    grille(p, 0, hy - Lh * 0.3, W * 0.5, Lh * 0.18, deck, n=9)
    for x in (-W * 0.13, W * 0.13):
        p.add(props.box(W * 0.22, Lh * 0.12, 0.012), 'armor_paint', T(x, hy - Lh * 0.12, deck + 0.006), keys=camo)
    stowage(p, (0, hy - Lh * 0.45, deck), (W * 0.42, 0.12, 0.07), key='armor_camo2', straps=2)
    jerrycan(p, W * 0.38, hy - Lh * 0.3, deck)
    jerrycan(p, W * 0.38, hy - Lh * 0.22, deck)
    stowage(p, (-W * 0.35, hy - Lh * 0.3, deck), (0.16, 0.22, 0.09), key='armor_camo2', straps=1)
    for sx in (-1, 1):
        tools(p, sx * (W / 2 + 0.012), hy - Lh * 0.3, hy + Lh * 0.15, d['z_f'] + H * 0.78, sx)
    # exhaust deflector at the rear
    p.add(props.box(W * 0.6, 0.03, 0.12), 'armor_shade', T(0, hy - Lh / 2 - 0.02, d['z_f'] - 0.02) @ R('X', -20), bevel=0.006)
    # civ stars: big on both hull sides and the glacis corners
    for sx in (-1, 1):
        mark(p, 'star', 0.13, (sx * (W / 2 + 0.005), hy - Lh * 0.02, d['z_f'] + H * 0.5), (sx, 0, 0))
        mark(p, 'star', 0.08, (sx * W * 0.34, gy + (hy + Lh / 2 - gy) * 0.45 + 0.005, d['z_f'] + H * 0.55 + 0.005), gn)


def _panzer(p, cfg, d, camo):
    """A Panzer IV: a boxy turret with sloped sides, the rear cupola and a
    stowage bin, a 75 mm gun with its double-baffle brake; the hull's
    vertical driver's plate with visor, ball MG and spare track links, spare
    road wheels, Notek light, tools and the muffler; red-brown crosses and
    the civ E marks front, rear and on the sides."""
    deck, W, H, hy, Lh, z_f = d['deck'], d['W'], d['H'], d['hy'], d['Lh'], d['z_f']
    ty = hy - 0.02
    tw_, tl, th_ = 0.72, 0.74, 0.28
    p.add(plate(tw_, tl, th_, cell=0.045, taper=0.18, plan_front=0.22, front=0.18, rear=0.05), 'armor_paint',
          T(0, ty, deck + 0.02 + th_ / 2), bevel=0.012, keys=camo)
    p.add(props.lathe([(0, 0.27), (0.03, 0.27)], seg=28), 'armor_shade', T(0, ty, deck), smooth=True)
    roof = deck + 0.02 + th_
    # the bin on the turret rear
    p.add(plate(0.5, 0.2, 0.17, cell=0.045, rear_low=0.15), 'armor_paint', T(0, ty - tl / 2 - 0.09, roof - 0.11),
          bevel=0.01, keys=camo)
    # mantlet, gun, coax
    p.add(plate(0.24, 0.1, 0.15, cell=0.04), 'armor_paint', T(0, ty + tl / 2 * 0.82 + 0.03, deck + 0.13), bevel=0.02, keys=camo)
    gun(p, (0, ty + tl / 2 * 0.82 + 0.08, deck + 0.13), 0.62, 0.044, muzzle='brake')
    vkit.rod(p, (-0.08, ty + tl / 2 * 0.82 + 0.06, deck + 0.13), (-0.08, ty + tl / 2 * 0.82 + 0.13, deck + 0.13), 0.009, 'gunmetal', seg=6)
    # the cupola at the rear, side hatches, vision ports
    p.add(props.lathe([(0, 0.1), (0.07, 0.095), (0.08, 0.07)], seg=20), 'armor_paint', T(0, ty - tl * 0.3, roof), smooth=True)
    for k in range(5):
        a = math.radians(-50 + 25 * k + 90)
        p.add(props.box(0.035, 0.006, 0.02), 'port', T(0.1 * math.cos(a), ty - tl * 0.3 + 0.1 * math.sin(a), roof + 0.045) @ R('Z', math.degrees(a) - 90))
    hatch(p, 0.0, ty - tl * 0.3, roof + 0.08, 0.075)
    hatch(p, 0.14, ty + 0.1, roof, 0.06)
    for sx in (-1, 1):
        p.add(props.box(0.012, 0.13, 0.15), 'armor_paint', T(sx * tw_ * 0.43, ty - 0.05, deck + 0.14) @ R('Y', -sx * 10), bevel=0.004)
        p.add(props.box(0.006, 0.04, 0.012), 'port', T(sx * tw_ * 0.4, ty + 0.18, deck + 0.17) @ R('Y', -sx * 10))
    # the driver's plate: visor, ball MG; spare links; the glacis below with spare road wheels
    fy = hy + Lh / 2
    p.add(props.box(0.18, 0.012, 0.045), 'armor_shade', T(-0.18, fy + 0.006, z_f + H * 0.6), bevel=0.004)
    p.add(props.box(0.12, 0.014, 0.012), 'port', T(-0.18, fy + 0.013, z_f + H * 0.62))
    p.add(props.ellipsoid(0.045, 0.03, 0.045, seg=12), 'armor_paint', T(0.18, fy, z_f + H * 0.55), smooth=True)
    vkit.rod(p, (0.18, fy, z_f + H * 0.55), (0.18, fy + 0.1, z_f + H * 0.55), 0.009, 'gunmetal', seg=6)
    for k in range(4):
        p.add(props.box(0.09, 0.02, 0.05), 'track', T(-0.25 + 0.165 * k, fy + 0.012, z_f + H * 0.18), bevel=0.004)
    light(p, -W * 0.38, fy - 0.04, deck + 0.03, r=0.025, guard=False)
    # engine deck hatches and louvres, tools, jerrycans; the muffler at the rear
    for x in (-W * 0.16, W * 0.16):
        p.add(props.box(W * 0.26, Lh * 0.16, 0.012), 'armor_paint', T(x, hy - Lh * 0.3, deck + 0.006), keys=camo)
        grille(p, x, hy - Lh * 0.3, W * 0.2, Lh * 0.08, deck + 0.012, n=5)
    for sx in (-1, 1):
        tools(p, sx * (W / 2 - 0.05), hy - Lh * 0.35, hy + Lh * 0.2, z_f + 0.02, sx)
    jerrycan(p, W * 0.42, hy - Lh * 0.44, z_f + 0.008, yaw=90)
    jerrycan(p, -W * 0.42, hy - Lh * 0.44, z_f + 0.008, yaw=90)
    p.add(props.lathe([(-W * 0.22, 0.06), (W * 0.22, 0.06)], seg=14), 'armor_shade',
          T(0, hy - Lh / 2 - 0.07, z_f - 0.02) @ R('Y', 90), smooth=True)
    # markings: crosses on the hull sides and the turret, civ E's front, rear and sides
    for sx in (-1, 1):
        cross_mark(p, 0.075, (sx * (W / 2 + 0.005), hy + Lh * 0.08, z_f + H * 0.5), (sx, 0, 0))
        cross_mark(p, 0.05, (sx * tw_ * 0.47, ty - 0.12, deck + 0.13), (sx, 0, 0.18))
        mark(p, 'E', 0.11, (sx * W * 0.34, fy + 0.008, z_f + H * 0.55), (0, 1, 0), rot=0)
        mark(p, 'E', 0.1, (sx * W * 0.3, hy - Lh / 2 - 0.006, z_f + H * 0.55), (0, -1, 0))
        mark(p, 'E', 0.1, (sx * (W / 2 + 0.005), hy - Lh * 0.36, z_f + H * 0.5), (sx, 0, 0))
        cross_mark(p, 0.05, (sx * W * 0.12, fy + 0.008, z_f + H * 0.5), (0, 1, 0))


def _abrams(p, cfg, d, camo):
    """An M1 Abrams: the low, wide, angular turret with its bustle rack,
    the commander's .50 cal and the loader's MG, CITV, smoke banks, a wind
    sensor and two antennas; the long 120 mm gun with thermal sleeve; the
    long glacis with the driver's hatch; civ C arcs on the turret."""
    deck, W, H, hy, Lh, z_f = d['deck'], d['W'], d['H'], d['hy'], d['Lh'], d['z_f']
    ty = hy - 0.12
    tw_, tl, th_ = W * 0.9, 1.0, 0.22
    p.add(plate(tw_, tl, th_, cell=0.045, taper=0.12, taper_low=0.04, plan_front=0.35, front=0.22, rear=0.04),
          'armor_paint', T(0, ty, deck + 0.015 + th_ / 2), bevel=0.01, keys=camo)
    roof = deck + 0.015 + th_
    # the bustle rack: a rail frame with bags and boxes
    by = ty - tl / 2 - 0.12
    for sx in (-1, 1):
        vkit.rod(p, (sx * tw_ * 0.45, ty - tl / 2 + 0.02, roof - 0.02), (sx * tw_ * 0.45, by - 0.1, roof - 0.02), 0.01, 'gunmetal', seg=6)
    vkit.rod(p, (-tw_ * 0.45, by - 0.1, roof - 0.02), (tw_ * 0.45, by - 0.1, roof - 0.02), 0.01, 'gunmetal', seg=6)
    p.add(props.box(tw_ * 0.88, 0.22, 0.012), 'gunmetal', T(0, by, roof - th_ * 0.55))
    stowage(p, (-tw_ * 0.22, by, roof - th_ * 0.55), (tw_ * 0.4, 0.2, 0.13), straps=2)
    stowage(p, (tw_ * 0.22, by, roof - th_ * 0.55), (tw_ * 0.4, 0.2, 0.11), key='armor_camo2', straps=1)
    # mantlet, the gun with sleeve, coax
    fy = ty + tl / 2 * 0.78
    p.add(plate(0.26, 0.12, 0.14, cell=0.04), 'armor_paint', T(0, fy, deck + 0.12), bevel=0.012, keys=camo)
    gun(p, (0, fy + 0.05, deck + 0.12), 1.15, 0.04, sleeve=True)
    # roof: commander's cupola with .50 cal, loader's hatch with MG and shield, CITV, wind sensor, antennas
    hatch(p, 0.17, ty - 0.08, roof, 0.1, periscopes=6)
    mg(p, (0.17, ty + 0.04, roof + 0.13), yaw=0, length=0.4, pitch=5)
    hatch(p, -0.18, ty - 0.05, roof, 0.085)
    mg(p, (-0.18, ty + 0.08, roof + 0.11), yaw=-10, length=0.34, pitch=5, shield=True)
    p.add(props.box(0.09, 0.09, 0.12), 'armor_paint', T(-0.02, ty + 0.12, roof + 0.06), bevel=0.01, keys=camo)
    p.add(props.box(0.06, 0.008, 0.05), 'port', T(-0.02, ty + 0.166, roof + 0.08))
    p.add(props.box(0.1, 0.07, 0.08), 'armor_shade', T(0.05, ty + 0.3, roof + 0.04), bevel=0.01)   # gunner's sight
    p.add(props.box(0.08, 0.006, 0.04), 'port', T(0.05, ty + 0.337, roof + 0.05))
    vkit.rod(p, (0.0, ty - tl / 2 + 0.06, roof), (0.0, ty - tl / 2 + 0.06, roof + 0.14), 0.006, 'gunmetal', seg=5)
    for sx in (-1, 1):
        antenna(p, sx * tw_ * 0.4, ty - tl / 2 + 0.08, roof, 0.75)
        smoke(p, (sx * tw_ * 0.36, ty + tl * 0.22, roof - 0.01), sx * 20, n=6)
    # the hull: driver's hatch, lights, deck grilles and kit
    gy = d['glacis_y']
    hatch(p, 0, gy - 0.06, deck, 0.07)
    for k in range(3):
        periscope(p, (-0.07 + 0.07 * k, gy - 0.0, deck), 0)
    for sx in (-1, 1):
        light(p, sx * W * 0.4, gy + 0.06, deck - 0.04, r=0.022, guard=True)
    grille(p, 0, hy - Lh * 0.42, W * 0.8, Lh * 0.1, deck, n=6)
    for x in (-W * 0.2, W * 0.2):
        p.add(props.box(W * 0.36, Lh * 0.18, 0.012), 'armor_paint', T(x, hy - Lh * 0.26, deck + 0.006), keys=camo)
    p.add(props.box(W * 0.6, 0.025, 0.1), 'armor_shade', T(0, hy - Lh / 2 - 0.01, z_f + H * 0.4))   # the exhaust grille
    # civ C arcs: turret sides and cheeks; a civ panel on each skirt's rear
    for sx in (-1, 1):
        sxn = Vector((sx, 0.0, 0.12)).normalized()
        mark(p, 'C', 0.11, (sx * tw_ * 0.47, ty - 0.08, deck + 0.015 + th_ * 0.5), sxn, rot=0)
        ch = Vector((sx * 0.9, 1.0, 0.4)).normalized()
        mark(p, 'C', 0.075, (sx * tw_ * 0.3, ty + tl * 0.38, deck + 0.015 + th_ * 0.45), ch)
        mark(p, 'rect', (0.16, 0.07), (sx * (W / 2 + 0.03), -d['L'] * 0.38, d['z_f'] - 0.05), (sx, 0, 0))


def _bradley(p, cfg, d, camo):
    """An M2 Bradley: a tall box hull with the steep glacis and trim vane,
    the dark grey two-man turret with the 25 mm chain gun and the TOW box;
    the rear ramp, smoke banks, civ patches on the turret top and the hull
    corners."""
    deck, W, H, hy, Lh, z_f = d['deck'], d['W'], d['H'], d['hy'], d['Lh'], d['z_f']
    tx, ty = 0.06, hy + 0.0
    tw_, tl, th_ = 0.58, 0.62, 0.2
    p.add(plate(tw_, tl, th_, cell=0.045, taper=0.22, plan_front=0.25, front=0.2, rear=0.1), 'turret_paint',
          T(tx, ty, deck + 0.015 + th_ / 2), bevel=0.01)
    p.add(props.lathe([(0, 0.27), (0.02, 0.27)], seg=28), 'armor_shade', T(tx, ty, deck), smooth=True)
    roof = deck + 0.015 + th_
    # the chain gun in its shroud, coax
    fy = ty + tl / 2 * 0.75
    p.add(props.box(0.12, 0.14, 0.09), 'turret_paint', T(tx, fy + 0.03, deck + 0.11), bevel=0.01)
    vkit.rod(p, (tx, fy + 0.08, deck + 0.11), (tx, fy + 0.3, deck + 0.115), 0.028, 'gunmetal', seg=10)
    vkit.rod(p, (tx, fy + 0.3, deck + 0.115), (tx, fy + 0.85, deck + 0.13), 0.014, 'gunmetal', seg=8)
    p.add(props.lathe([(0, 0.022), (0.05, 0.022)], seg=10), 'gunmetal', T(tx, fy + 0.85, deck + 0.13) @ R('X', -89), smooth=True)
    vkit.rod(p, (tx + 0.08, fy + 0.06, deck + 0.1), (tx + 0.08, fy + 0.2, deck + 0.1), 0.008, 'gunmetal', seg=6)
    # the TOW launcher box on the turret's left
    p.add(props.box(0.14, 0.42, 0.13), 'turret_paint', T(tx - tw_ * 0.5 - 0.06, ty - 0.02, roof - 0.07), bevel=0.012)
    for z in (roof - 0.04, roof - 0.1):
        p.add(props.lathe([(0, 0.032), (0.006, 0.032)], seg=12), 'port', T(tx - tw_ * 0.5 - 0.06, ty + 0.19, z) @ R('X', -90), smooth=True)
    # hatches, periscopes, sight, smoke banks
    hatch(p, tx + 0.13, ty - 0.08, roof, 0.08, key='turret_paint', periscopes=5)
    hatch(p, tx - 0.12, ty - 0.1, roof, 0.07, key='turret_paint')
    p.add(props.box(0.1, 0.08, 0.08), 'armor_shade', T(tx - 0.08, ty + 0.16, roof + 0.04), bevel=0.01)
    p.add(props.box(0.08, 0.006, 0.04), 'port', T(tx - 0.08, ty + 0.201, roof + 0.05))
    for sx in (-1, 1):
        smoke(p, (tx + sx * tw_ * 0.4, ty + tl * 0.26, roof - 0.04), sx * 25, n=4)
    antenna(p, -W * 0.38, hy - Lh * 0.42, deck, 0.7)
    antenna(p, W * 0.38, hy - Lh * 0.42, deck, 0.55)
    # the hull: trim vane, driver's hatch and periscopes, lights, engine grilles; cargo hatch, ramp
    gy = d['glacis_y']
    gn = d['gn']
    fyh = hy + Lh / 2
    p.add(props.box(W * 0.7, 0.02, H * 0.38), 'armor_camo2', T(0, fyh - 0.02, z_f + H * 0.3) @ R('X', -25), bevel=0.006)
    for k in range(4):
        p.add(props.box(0.008, 0.024, H * 0.34), 'armor_shade', T(-W * 0.24 + W * 0.16 * k, fyh - 0.005, z_f + H * 0.3) @ R('X', -25))
    hatch(p, -W * 0.3, gy - 0.06, deck, 0.07)
    for k in range(3):
        periscope(p, (-W * 0.3 - 0.07 + 0.07 * k, gy - 0.0, deck), 0)
    grille(p, W * 0.28, gy - 0.12, W * 0.32, Lh * 0.12, deck, n=6)
    for sx in (-1, 1):
        light(p, sx * W * 0.42, fyh - 0.06, z_f + H * 0.62, r=0.024, guard=True)
    p.add(props.box(W * 0.5, Lh * 0.22, 0.012), 'armor_paint', T(0, hy - Lh * 0.33, deck + 0.006), keys=camo)   # the cargo hatch
    for sx in (-1, 1):
        p.add(props.box(0.02, Lh * 0.22, 0.02), 'gunmetal', T(sx * W * 0.25, hy - Lh * 0.33, deck + 0.012))
    rq = T(0, hy - Lh / 2 - 0.006, z_f + H * 0.45)
    p.add(props.box(W * 0.62, 0.012, H * 0.8), 'armor_paint', rq, keys=camo)   # the ramp
    p.add(props.box(0.12, 0.016, 0.16), 'armor_shade', rq @ T(-W * 0.12, 0, 0))   # its door
    stowage(p, (W * 0.38, hy - Lh * 0.38, deck), (0.14, 0.28, 0.07), key='armor_camo2', straps=2)
    # civ patches: the turret top and the four hull corners
    mark(p, 'rect', (0.2, 0.14), (tx - 0.02, ty - 0.2, roof + 0.004), (0, 0, 1))
    for sx in (-1, 1):
        mark(p, 'rect', (0.16, 0.1), (sx * W * 0.38, gy - 0.18, deck + 0.004), (0, 0, 1))
        mark(p, 'rect', (0.16, 0.1), (sx * W * 0.38, hy - Lh * 0.44, deck + 0.004), (0, 0, 1))
        mark(p, 'rect', (0.18, 0.09), (sx * (W / 2 + 0.03), d['L'] * 0.38, z_f - 0.05), (sx, 0, 0))


def _sam(p, cfg, d, camo):
    """A tracked SAM on an M113-type carrier: the box hull with its steep
    front, trim vane, stowage boxes and civ side panels; a turntable and a
    raised launcher with four big white missiles, red noses, civ fins."""
    deck, W, H, hy, Lh, z_f = d['deck'], d['W'], d['H'], d['hy'], d['Lh'], d['z_f']
    fyh = hy + Lh / 2
    gy = d['glacis_y']
    p.add(props.box(W * 0.7, 0.018, H * 0.35), 'armor_camo2', T(0, fyh - 0.02, z_f + H * 0.25) @ R('X', -30), bevel=0.006)
    hatch(p, -W * 0.28, gy - 0.08, deck, 0.07, periscopes=4)
    for sx in (-1, 1):
        light(p, sx * W * 0.4, fyh - 0.04, z_f + H * 0.55, r=0.022, guard=True)
    grille(p, W * 0.25, gy - 0.12, W * 0.32, Lh * 0.12, deck, n=6)
    for sx in (-1, 1):   # stowage boxes along the top edges
        for k in range(3):
            stowage(p, (sx * W * 0.4, hy - Lh * 0.42 + Lh * 0.13 * k, deck), (0.15, 0.12, 0.1),
                    key=('armor_camo2', 'armor_paint', 'canvas')[k], straps=1)
    jerrycan(p, -W * 0.25, hy - Lh / 2 - 0.03, z_f + H * 0.25, yaw=0)
    jerrycan(p, W * 0.25, hy - Lh / 2 - 0.03, z_f + H * 0.25, yaw=0)
    antenna(p, W * 0.38, gy - 0.1, deck, 0.6)
    # the launcher: turntable, post, cradle; four missiles at 2 x 2
    py_ = hy - Lh * 0.05
    p.add(props.lathe([(0, 0.24), (0.05, 0.24), (0.07, 0.2)], seg=24), 'armor_paint', T(0, py_, deck), smooth=True)
    p.add(plate(0.22, 0.26, 0.24, cell=0.04, taper=0.25), 'armor_paint', T(0, py_, deck + 0.07 + 0.12), bevel=0.012, keys=camo)
    piv = T(0, py_, deck + 0.34) @ R('X', 12)
    p.add(props.box(0.62, 0.2, 0.1), 'armor_paint', piv, bevel=0.012, keys=camo)
    for sx in (-1, 1):
        p.add(props.box(0.04, 0.5, 0.04), 'gunmetal', piv @ T(sx * 0.2, 0.1, 0.0), bevel=0.006)
    for x, z in ((-0.2, 0.12), (0.2, 0.12), (-0.2, -0.1), (0.2, -0.1)):
        mm = piv @ T(x, 0.12, z)
        rail = mm @ R('X', -90)
        p.add(props.lathe([(-0.62, 0.05), (0.42, 0.05), (0.5, 0.042), (0.6, 0.02), (0.64, 0.0)], seg=16),
              'missile_white', rail, smooth=True)
        p.add(props.lathe([(0.46, 0.048), (0.5, 0.042), (0.6, 0.02), (0.64, 0.0)], seg=16), 'missile_red',
              rail @ T(0, 0, 0.002), smooth=True)
        p.add(props.lathe([(-0.64, 0.035), (-0.6, 0.045)], seg=12), 'gunmetal', rail, smooth=True)
        for a in (45, 135):   # tail fins and canards, civ
            p.add(props.box(0.006, 0.17, 0.26), 'tint', mm @ T(0, -0.52, 0) @ R('Y', a), tint=True, bevel=0.002)
            p.add(props.box(0.005, 0.08, 0.15), 'tint', mm @ T(0, 0.28, 0) @ R('Y', a), tint=True, bevel=0.002)
    # civ side panels and a band on the nose
    for sx in (-1, 1):
        mark(p, 'rect', (0.42, 0.13), (sx * (W / 2 + 0.005), hy - Lh * 0.1, z_f + H * 0.55), (sx, 0, 0))
        mark(p, 'rect', (0.14, 0.1), (sx * (W / 2 + 0.005), hy + Lh * 0.32, z_f + H * 0.55), (sx, 0, 0))
    mark(p, 'rect', (W * 0.5, 0.06), (0, fyh + 0.004, z_f + H * 0.72), (0, 1, 0))


def _mlrs(p, cfg, d, camo):
    """The M270 MLRS: an armored cab forward with its windows, wipers and
    louvred grille; the big launcher box over the rear, two pods split by a
    seam, framed sides, twelve tube ends at the front; civ chevrons on the
    box sides and top."""
    deck, W, H, hy, Lh, z_f = d['deck'], d['W'], d['H'], d['hy'], d['Lh'], d['z_f']
    fyh = hy + Lh / 2
    # the cab
    cl, ch = Lh * 0.3, 0.34
    cy = fyh - cl / 2
    p.add(plate(W, cl, ch, cell=0.045, front=0.28, taper=0.06), 'armor_paint', T(0, cy, deck + ch / 2), bevel=0.012, keys=camo)
    win_y = cy + cl / 2 * (1 - 0.28 * 0.6)
    wn = Vector((0, ch, cl / 2 * 0.28)).normalized()
    for k in range(3):   # the windscreens, framed
        x = -W * 0.3 + W * 0.3 * k
        p.add(props.box(W * 0.22, 0.012, ch * 0.32), 'armor_shade', T(x, win_y, deck + ch * 0.62) @ _q(wn) @ R('X', 90))
        p.add(props.box(W * 0.18, 0.016, ch * 0.24), 'port', T(x, win_y + 0.004, deck + ch * 0.62) @ _q(wn) @ R('X', 90))
    for sx in (-1, 1):
        p.add(props.box(0.016, cl * 0.4, ch * 0.24), 'port', T(sx * (W / 2 * 0.95), cy, deck + ch * 0.62))
    for k in range(6):   # louvres across the cab front below the windows
        p.add(props.box(W * 0.6, 0.02, 0.012), 'armor_shade', T(0, fyh + 0.004, z_f + H * 0.2 + 0.035 * k) @ R('X', 20))
    for sx in (-1, 1):
        light(p, sx * W * 0.42, fyh - 0.02, z_f + H * 0.6, r=0.022, guard=True)
    hatch(p, -W * 0.25, cy - cl * 0.1, deck + ch, 0.07)
    antenna(p, W * 0.4, cy - cl * 0.3, deck + ch, 0.6)
    antenna(p, -W * 0.4, cy - cl * 0.3, deck + ch, 0.5)
    # the launcher box
    bl, bh = Lh * 0.66, 0.44
    by = hy - Lh / 2 + bl / 2 + 0.01
    bz = deck + 0.05 + bh / 2
    p.add(props.box(W * 0.8, bl * 0.9, 0.05), 'gunmetal', T(0, by, deck + 0.025))   # the cradle
    p.add(plate(W * 0.98, bl, bh, cell=0.045), 'armor_paint', T(0, by, bz), bevel=0.01, keys=camo)
    p.add(props.box(0.012, bl * 1.002, bh * 1.002), 'armor_shade', T(0, by, bz))   # the seam between the pods
    for sx in (-1, 1):
        for k in range(7):   # the framing ribs
            p.add(props.box(0.016, 0.02, bh * 0.96), 'armor_shade', T(sx * W * 0.495, by - bl / 2 + bl * (k + 0.5) / 7, bz))
        p.add(props.box(0.016, bl, 0.02), 'armor_shade', T(sx * W * 0.495, by, bz + bh * 0.42))
        p.add(props.box(0.016, bl, 0.02), 'armor_shade', T(sx * W * 0.495, by, bz - bh * 0.42))
        p.add(props.box(0.012, bl * 0.24, bh * 0.3), 'armor_camo3', T(sx * W * 0.497, by - bl * 0.34, bz - bh * 0.1))
    for k in range(4):   # roof panels and the lifting points
        p.add(props.box(W * 0.96, 0.014, 0.008), 'armor_shade', T(0, by - bl * 0.36 + bl * 0.24 * k, bz + bh / 2 + 0.003))
    for x in (-W * 0.42, W * 0.42):
        for yy in (by - bl * 0.45, by + bl * 0.45):
            p.add(props.box(0.04, 0.04, 0.03), 'gunmetal', T(x, yy, bz + bh / 2 + 0.015))
    fz = by + bl / 2 + 0.004
    for sx in (-1, 1):   # 2 pods x 6 tubes, facing forward
        for i in range(3):
            for j in range(2):
                p.add(props.lathe([(0, 0.055), (0.008, 0.055)], seg=14), 'port',
                      T(sx * W * 0.25 + (i - 1) * W * 0.14, fz, bz - bh * 0.2 + j * bh * 0.4) @ R('X', -90), smooth=True)
                p.add(props.lathe([(0, 0.064), (0.006, 0.064)], seg=14), 'armor_shade',
                      T(sx * W * 0.25 + (i - 1) * W * 0.14, fz - 0.003, bz - bh * 0.2 + j * bh * 0.4) @ R('X', -90), smooth=True)
    # civ chevrons: big on the box sides and the top
    for sx in (-1, 1):
        mark(p, 'chevron', 0.21, (sx * (W * 0.49 + 0.012), by - bl * 0.05, bz), (sx, 0, 0))
    mark(p, 'chevron', 0.24, (0, by - bl * 0.05, bz + bh / 2 + 0.01), (0, 0, 1))
    mark(p, 'rect', (0.1, 0.06), (W * 0.3, cy - cl * 0.25, deck + ch + 0.004), (0, 0, 1))


ARMOR = {
    # an olive-tan Sherman with the wide civ band round the turret and big civ stars
    'Tank': dict(L=1.62, W=1.04, H=0.24, tw=0.22, rw=0.1, wheels=6, bogies=True, rollers=3,
                 front_wheel=(0.72, 0.25, 0.1), rear_wheel=(-0.74, 0.17, 0.095), holes=0,
                 upper=dict(front=0.62, front_low=0.06, rear=0.22), nose=0.45,
                 paint='#9a8456', camo=[('armor_camo2', 5.0, 0.45, 1.0)],
                 camo2='#7a6a44', camo3='#b09a68', builder=_tank),
    # a Panzer IV in pale green-cream mottle
    'Panzer': dict(L=1.68, W=1.06, H=0.3, tw=0.2, rw=0.075, wheels=8, rollers=4, holes=6,
                   front_wheel=(0.76, 0.21, 0.085), rear_wheel=(-0.78, 0.16, 0.08),
                   upper=dict(front=0.04, rear=0.08), nose=0.55,
                   paint='#a4aa80', camo=[('armor_camo2', 9.0, 0.18, 2.0), ('armor_camo3', 12.0, 0.3, 7.0),
                                          ('armor_camo4', 14.0, 0.42, 11.0)],
                   camo2='#7a8a5c', camo3='#c8c8a6', camo4='#5a6446', builder=_panzer),
    # an M1 Abrams in desert camo, side skirts
    'Modern Armor': dict(L=2.1, W=0.96, H=0.24, tw=0.2, rw=0.085, wheels=7, rollers=0, skirt=True, holes=6,
                         sprocket='rear', front_wheel=(0.96, 0.17, 0.08), rear_wheel=(-0.96, 0.16, 0.085),
                         upper=dict(front=0.62, rear=0.04), nose=0.3,
                         paint='#c8b282', camo=[('armor_camo2', 6.0, 0.22, 3.0), ('armor_camo3', 8.0, 0.3, 9.0)],
                         camo2='#8a7048', camo3='#e4d6b0', builder=_abrams),
    # an M2 Bradley in strong tan, brown and grey, a dark grey turret
    'Mech Infantry': dict(L=1.92, W=0.98, H=0.36, tw=0.2, rw=0.085, wheels=6, rollers=0, skirt=True, holes=6,
                          front_wheel=(0.86, 0.19, 0.085), rear_wheel=(-0.88, 0.17, 0.08),
                          upper=dict(front=0.42, rear=0.06), nose=0.35,
                          paint='#cab48a', camo=[('armor_camo2', 6.5, 0.18, 5.0), ('armor_camo4', 8.0, 0.3, 13.0),
                                                 ('armor_camo3', 9.0, 0.3, 2.0)],
                          camo2='#6e5a40', camo3='#e6dcc0', camo4='#8a8a80', turret_paint='#4e4f50', builder=_bradley),
    # a tracked SAM carrier in olive camo
    'Mobile SAM': dict(L=1.62, W=0.98, H=0.4, tw=0.19, rw=0.085, wheels=5, rollers=0, holes=0,
                       front_wheel=(0.74, 0.2, 0.09), rear_wheel=(-0.74, 0.17, 0.085),
                       upper=dict(front=0.3, rear=0.02), nose=0.3,
                       paint='#5e6246', camo=[('armor_camo3', 7.0, 0.22, 6.0), ('armor_camo4', 8.0, 0.3, 1.5)],
                       camo3='#8e8258', camo4='#3e4232', builder=_sam),
    # an MLRS in desert tan with grey panels
    'Radar Artillery': dict(L=1.8, W=1.0, H=0.22, tw=0.2, rw=0.08, wheels=6, rollers=0, holes=6,
                            front_wheel=(0.82, 0.18, 0.085), rear_wheel=(-0.84, 0.16, 0.08),
                            upper=dict(front=0.1, rear=0.04), nose=0.3,
                            paint='#c8a670', camo=[('armor_camo2', 6.0, 0.3, 4.0)],
                            camo2='#a08458', camo3='#9a968a', builder=_mlrs),
}


def tracked(name, cfg, colors):
    paint = cfg.get('paint', '#7a6e4a')
    c = {'armor_paint': paint, 'armor_camo2': cfg.get('camo2', _hex(paint, 0.75)),
         'armor_camo3': cfg.get('camo3', _hex(paint, 1.15)), 'armor_camo4': cfg.get('camo4', _hex(paint, 0.6)),
         'armor_shade': _hex(paint, 0.55), 'turret_paint': cfg.get('turret_paint', '#4a4c4e')}
    c.update(colors)
    root = vkit.root(name)
    p = vkit.part(root, 'hull', c, WEAR)
    # painted camo: smooth Perlin blobs in the paint's material (props.CAMO); no face keys
    props.CAMO.clear()
    if cfg.get('camo'):
        props.CAMO['armor_paint'] = [(c[k], f * 0.55, 0.5 + 0.22 * th, s) for k, f, th, s in cfg['camo']]
    camo = None
    d = chassis(p, cfg, camo)
    cfg['builder'](p, cfg, d, camo)
    p.finish()
    props.CAMO.clear()
    return root


def build(unit, colors=None, **params):
    return tracked(unit, ARMOR[unit], colors or {})


UNITS = list(ARMOR)
