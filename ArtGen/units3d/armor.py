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
               'canvas': ('#7a7254', 0.9, 0.0, 'leather'), 'canvas_dark': ('#5a5438', 0.9, 0.0, 'leather'), 'mark_red': ('#8a3424', 0.6, 0.1, 'metal'),
               'mark_white': ('#e4e2da', 0.6, 0.1, 'metal'), 'lens': ('#e8e4c8', 0.2, 0.3, 'polished'),
               'missile_white': ('#ecebe6', 0.45, 0.2, 'metal'), 'missile_red': ('#b0302a', 0.45, 0.2, 'metal'),
               'port': ('#0e0c0a', 0.9, 0.0, 'polished'), 'armor_edge': ('#a8aa80', 0.7, 0.3, 'metal'), 'gun_olive': ('#2e2d24', 0.8, 0.0, 'metal'), 'deck_dark': ('#5a5240', 0.85, 0.0, 'metal'),
               'shade_dark': ('#3a3428', 0.85, 0.0, 'metal'), 'gun_edge': ('#767256', 0.75, 0.0, 'metal'), 'tow_tan': ('#c09454', 0.65, 0.3, 'metal'),
               'armor_turret': ('#7a6e4a', 0.7, 0.3, 'metal'), 'mark_black': ('#1c1b18', 0.7, 0.1, 'metal'),
               'muzzle_light': ('#e8dfc0', 0.6, 0.0, 'metal'), 'skirt_dirt': ('#a08a64', 0.8, 0.0, 'metal'), 'brake_steel': ('#4a483c', 0.7, 0.0, 'metal')}.items():
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
            inn.append((s * 0.4 * math.cos(a), s * 0.4 * math.sin(a)))   # a thick, bold arc
        # two halves keep each n-gon simple
        return [out[:9] + inn[:9][::-1], out[8:] + inn[8:][::-1]]
    if kind == 'bracket':   # a bold open bracket (the Panzer's civ mark)
        w = s * 0.4
        return [[(-0.6 * s, -s), (0.6 * s, -s), (0.6 * s, -s + w), (-0.6 * s + w, -s + w), (-0.6 * s + w, s - w),
                 (0.6 * s, s - w), (0.6 * s, s), (-0.6 * s, s)]]
    if kind == 'tri':   # a filled triangle pointing up
        return [[(0, 0.75 * s), (s, -0.55 * s), (-s, -0.55 * s)]]
    if kind == 'chevron':
        return [[(0, 0.6 * s), (s, -0.45 * s), (0.4 * s, -0.45 * s), (0, -0.05 * s)],
                [(0, 0.6 * s), (0, -0.05 * s), (-0.4 * s, -0.45 * s), (-s, -0.45 * s)]]
    if kind == 'vee':   # a bold inverted V (the 1991 recognition chevron), two thick arms
        return [[(0, 0.7 * s), (0, -0.05 * s), (-0.4 * s, -0.5 * s), (-s, -0.5 * s)],
                [(0, 0.7 * s), (s, -0.5 * s), (0.4 * s, -0.5 * s), (0, -0.05 * s)]]
    if kind == 'rect':
        w, h = s
        return [[(-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)]]
    if kind == 'cross_w':   # a straight cross with arms of half-width a * s (the Balkenkreuz's parts)
        s, a = s
        a *= s
        return [[(-a, s), (a, s), (a, a), (s, a), (s, -a), (a, -a), (a, -s), (-a, -s), (-a, -a), (-s, -a), (-s, a), (-a, a)]]
    if kind == 'rhomb':   # a flat lozenge (a tactical sign)
        w, h = s
        return [[(0, h / 2), (-w / 2, 0), (0, -h / 2), (w / 2, 0)]]
    raise KeyError(kind)


_SEGS = {'0': 'abcdef', '1': 'bc', '2': 'abged', '3': 'abgcd', '4': 'fgbc', '5': 'afgcd', '6': 'afgedc', '7': 'abc',
         '8': 'abcdefg', '9': 'abcdfg'}


def _digits(text, h, w=None, t=None, gap=None, grow=0.0):
    """Block numerals (seven segments, squared), centred: a list of rect
    outlines in the plane, x across, y up; grow widens every stroke (an
    outline drawn behind)."""
    w = w or h * 0.58
    t = t or h * 0.24
    gap = h * 0.2 if gap is None else gap
    seg = {'a': (0, h / 2 - t / 2, w, t), 'g': (0, 0, w, t), 'd': (0, -h / 2 + t / 2, w, t),
           'f': (-w / 2 + t / 2, h / 4, t, h / 2), 'b': (w / 2 - t / 2, h / 4, t, h / 2),
           'e': (-w / 2 + t / 2, -h / 4, t, h / 2), 'c': (w / 2 - t / 2, -h / 4, t, h / 2)}
    n = len(text)
    x0 = -(n * w + (n - 1) * gap) / 2 + w / 2
    polys = []
    for i, ch in enumerate(text):
        cx = x0 + i * (w + gap)
        for k in _SEGS[ch]:
            sx, sy, sw, sh = seg[k]
            sw, sh = sw + 2 * grow, sh + 2 * grow
            polys.append([(cx + sx - sw / 2, sy - sh / 2), (cx + sx + sw / 2, sy - sh / 2),
                          (cx + sx + sw / 2, sy + sh / 2), (cx + sx - sw / 2, sy + sh / 2)])
    return polys


def mark(p, kind, s, at, normal, key='tint', rot=0.0, thick=0.008, polys=None):
    """A painted marking: an outline in the face's plane, its up toward +Z
    (or +Y on a deck); polys: outlines given directly instead of a kind."""
    tint = key == 'tint'
    at = Vector(at) + Vector(normal).normalized() * thick * 0.6   # proud of the face
    m = T(*at) @ _q(normal) @ R('Z', rot) @ R('X', -90)
    for poly in polys or _shape(kind, s):
        p.add(props.slab(poly, thick, 'Y'), key, m, tint=tint)


def number(p, text, h, at, normal, key='tint', outline='mark_black', o=0.008, stroke=0.24, width=0.58):
    """A painted turret number: block numerals in key over a thin dark
    outline."""
    if outline:
        mark(p, None, None, at, normal, outline, thick=0.005, polys=_digits(text, h, w=h * width, t=h * stroke, grow=o))
    mark(p, None, None, at, normal, key, thick=0.009, polys=_digits(text, h, w=h * width, t=h * stroke))


def balkenkreuz(p, s, at, normal):
    """The German cross: a black straight cross over a wider white one."""
    mark(p, 'cross_w', (s, 0.42), at, normal, 'mark_white', thick=0.005)
    mark(p, 'cross_w', (s * 0.98, 0.19), at, normal, 'mark_black', thick=0.009)


def cross_mark(p, s, at, normal):
    """A red-brown cross with a white outline (the Panzer's)."""
    n = Vector(normal).normalized()
    mark(p, 'cross', s * 1.25, at, normal, 'mark_white', thick=0.006)
    mark(p, 'cross', s, Vector(at) + n * 0.004, normal, 'mark_red', thick=0.006)


# ---------------------------------------------------------------- kit

def wheel(p, x, y, z, r, w, side, face='armor_shade', rubber=True, dual=True, teeth=0, holes=0, hole_r=0.1,
          hole_key='armor_shade', hub_key='gunmetal'):
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
        p.add(props.lathe([(ww / 2, r * 0.26), (ww / 2 + 0.03, r * 0.2)], seg=12), hub_key, m, smooth=True)
        for k in range(holes):
            a = 2 * math.pi * k / holes
            p.add(props.lathe([(0, r * hole_r), (0.01, r * hole_r)], seg=8), hole_key,
                  m @ T(r * 0.5 * math.cos(a), r * 0.5 * math.sin(a), ww / 2 + 0.008), smooth=True)
    for k in range(teeth):
        a = 360.0 * k / teeth
        p.add(props.box(w * 0.5, r * 0.22, r * 0.3), 'gunmetal', T(x, y, z) @ R('X', a) @ T(0, 0, r * 0.92), bevel=0.004)


def track(p, x, circles, tw, th=0.03, pitch=0.045, style=None):
    """One track: link plates (with cleats outside and guide horns inside)
    following the belt round the circles (cy, cz, r) of its wheels.
    style='chevron' (the Sherman's steel T54E1-type links): a guide horn on
    every link, a raised chevron cleat outside and the end connectors proud
    of both edges at each pin, so the links read one by one from the side."""
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
        if style == 'chevron':
            p.add(props.box(tw * 0.9, step * 0.78, th), 'track', m, bevel=0.004)
            for sxx in (-1, 1):   # the end connectors, proud of the edges, over the pins
                p.add(props.box(0.018, step * 0.42, th * 1.35), 'track_pin',
                      m @ T(sxx * (tw * 0.45 + 0.008), -step * 0.5, 0), bevel=0.003)
                # the chevron cleat: two bars angled back from the centre line
                p.add(props.box(tw * 0.36, 0.012, 0.012), 'track',
                      m @ T(sxx * tw * 0.19, 0, -th / 2 - 0.005) @ R('Z', sxx * 22), bevel=0.002)
            p.add(props.box(0.024, step * 0.45, 0.032), 'track', m @ T(0, 0, th / 2 + 0.013), bevel=0.003)
            continue
        p.add(props.box(tw, step * 0.84, th), 'track', m, bevel=0.004)
        p.add(props.box(tw * 0.96, step * 0.24, 0.012), 'track_pin', m @ T(0, -step * 0.2, -th / 2 - 0.004))
        if k % 2 == 0:
            p.add(props.box(0.022, step * 0.5, 0.03), 'track', m @ T(0, 0, th / 2 + 0.012))


def vvss_bogie(p, x, yc, zr, rw, tw, side, roll_z, roll_r, pair, key='armor_paint', spring='armor_shade', chunk=1.0):
    """A Sherman VVSS bogie: the cast bracket bolted to the hull between its
    two road wheels, the two wheel arms, the two vertical volute-spring
    housings between the wheels, and the bracket's arm up to the trailing
    return roller."""
    xo = x + side * tw * 0.08
    top = zr + rw * 1.05
    # the bracket: a casting across the wheel pair's top
    p.add(props.box(tw * 0.42 * chunk, pair * 1.25, rw * 0.55 * chunk), key, T(x, yc, top - rw * 0.2), bevel=0.012)
    # the wheel arms: from the bracket down to each axle
    for dy in (-pair, pair):
        vkit.beam(p, (xo, yc + dy * 0.25, top - rw * 0.3), (xo, yc + dy, zr), 0.035 * chunk, 0.03 * chunk, spring,
                  bevel=0.006)
    # the volute springs: two short drums between the wheels
    for dy in (-0.035, 0.035):
        rs = 0.027 * chunk
        p.add(props.lathe([(0, rs), (rw * 0.55, rs), (rw * 0.6, rs * 0.75)], seg=10), spring,
              T(xo + side * 0.012 * chunk, yc + dy, zr + rw * 0.1), smooth=True)
        for k in range(3):   # the coils
            p.add(props.lathe([(0, rs + 0.004), (0.008, rs + 0.004)], seg=10), 'gunmetal',
                  T(xo + side * 0.012 * chunk, yc + dy, zr + rw * (0.18 + 0.15 * k)), smooth=True)
    # the post up to the return roller
    ry = yc - rw * 0.45
    p.add(props.box(tw * 0.22 * chunk, 0.04 * chunk, roll_z - top + 0.01), key, T(x, ry, (roll_z + top) / 2 - 0.005),
          bevel=0.006)


def leaf_bogie(p, x, yc, zr, rw, tw, side, pair, key='armor_shade'):
    """A Panzer IV bogie: the bracket bolted to the hull between its two
    road wheels, the two swing arms down to the axles and the quarter-
    elliptic leaf-spring pack across the top."""
    xi = x - side * tw * 0.15
    top = zr + rw * 0.95
    p.add(props.box(tw * 0.35, pair * 0.9, rw * 0.7), key, T(xi, yc, top - rw * 0.25), bevel=0.01)
    for dy in (-pair, pair):
        vkit.beam(p, (xi, yc + dy * 0.3, top - rw * 0.35), (xi, yc + dy, zr), 0.04, 0.03, key, bevel=0.006)
    for k in range(3):   # the leaves, shorter going up
        p.add(props.box(tw * 0.25, pair * (1.6 - 0.35 * k), 0.012), 'gunmetal', T(xi, yc, top + 0.012 * k), bevel=0.003)


def chassis(p, cfg, camo):
    """The tracks, lower and upper hull, fenders and skirts. Returns the
    numbers the turret builders need."""
    L, W, tw, rw = cfg['L'], cfg['W'], cfg['tw'], cfg['rw']
    th = cfg.get('track_th', 0.03)
    xs = W / 2 - tw / 2 - 0.01
    n = cfg['wheels']
    y0 = -L / 2 + cfg.get('rear_gap', 0.22)
    y1 = L / 2 - cfg.get('front_gap', 0.22)
    road = [y0 + (y1 - y0) * i / (n - 1) for i in range(n)]
    vvss = cfg.get('vvss')   # (bogie centres along y): Sherman VVSS, a wheel pair per bogie
    leaf = cfg.get('leaf')   # (bogie centres along y): Panzer IV leaf-spring bogies, a wheel pair each
    if vvss or leaf:
        pair = rw + cfg.get('pair_gap', 0.012)
        road = [yc + dy for yc in (vvss or leaf) for dy in (-pair, pair)]
    zr = rw + th
    fy, fz, fr = cfg.get('front_wheel', (L / 2 - 0.1, zr + 0.08, 0.09))
    ry, rz, rr = cfg.get('rear_wheel', (-L / 2 + 0.1, zr + 0.06, 0.085))
    top = cfg.get('top', max(fz + fr, rz + rr, zr + rw) + 0.01)
    rollers = cfg.get('rollers', 3)
    roll_r = cfg.get('roll_r', 0.035)
    roll = [y0 + 0.1 + (y1 - y0 - 0.2) * (i + 0.5) / rollers for i in range(rollers)] if rollers else []
    if vvss:   # the return roller rides on top of each bogie bracket, trailing
        roll = [yc - rw * 0.45 for yc in vvss]
    circles = [(y, zr, rw) for y in road] + [(fy, fz, fr), (ry, rz, rr)] + [(y, top - roll_r, roll_r) for y in roll]
    sprocket = cfg.get('sprocket', 'front')
    for sx in (-1, 1):
        x = sx * xs
        track(p, x, circles, tw, th=th, style=cfg.get('track_style'))
        if vvss:
            for yc in vvss:
                vvss_bogie(p, x, yc, zr, rw, tw, sx, top - roll_r, roll_r, pair, key=cfg.get('bogie_key', 'armor_paint'),
                           spring=cfg.get('spring_key', 'armor_shade'), chunk=cfg.get('bogie_chunk', 1.0))
        for yc in leaf or ():
            leaf_bogie(p, x, yc, zr, rw, tw, sx, pair, key=cfg.get('bogie_key', 'armor_shade'))
        for y in road:
            wheel(p, x, y, zr, rw, tw, sx, face=cfg.get('wheel_face', 'armor_shade'), holes=cfg.get('holes', 0),
                  hole_r=cfg.get('hole_r', 0.1), hole_key=cfg.get('hole_key', 'armor_shade'), hub_key=cfg.get('hub_key', 'gunmetal'))
        wheel(p, x, fy, fz, fr, tw, sx, rubber=False, teeth=cfg.get('teeth', 12) if sprocket == 'front' else 0,
              face='armor_shade')
        wheel(p, x, ry, rz, rr, tw, sx, rubber=sprocket == 'front', teeth=12 if sprocket == 'rear' else 0, face='armor_shade')
        for y in roll:
            wheel(p, x, y, top - roll_r, roll_r, tw * (0.45 if vvss else 0.6), sx, dual=False)
    z_f = top + th / 2 + 0.008
    H = cfg['H']
    Lh, hy = cfg.get('Lh', L * 0.96), cfg.get('hull_y', 0.0)
    # the lower hull between the tracks (its nose sloped)
    belly = cfg.get('belly', rw * 0.7)   # the hull's underside (lower: no daylight between the road wheels)
    low_h = z_f - belly
    p.add(plate(W - 2 * tw - 0.04, Lh * 0.96, low_h, front_low=cfg.get('nose', 0.12), rear_low=0.06),
          'armor_paint', T(0, hy, belly + low_h / 2), bevel=0.01, keys=camo)
    # the upper hull over the tracks
    up = cfg.get('upper', {})
    ul = cfg.get('upper_len')   # the upper hull shorter than the lower, set back from the nose (the Panzer IV's front)
    Lu, uy = (Lh * ul, hy - Lh * (1 - ul) / 2) if ul else (Lh, hy)
    p.add(plate(W, Lu, H, **up), 'armor_paint', T(0, uy, z_f + H / 2), bevel=0.014, keys=camo)
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
    glacis_y = uy + Lu / 2 * (1 - fr_)   # where the glacis meets the deck
    gn = Vector((0, H, Lu / 2 * fr_)).normalized() if fr_ else Vector((0, 1, 0))
    return dict(L=L, W=W, H=H, Lh=Lh, hy=hy, z_f=z_f, deck=deck, glacis_y=glacis_y, gn=gn, tw=tw, xs=xs, Lu=Lu, uy=uy,
                zr=zr, rw=rw, belly=belly)


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


def grille(p, cx, cy, sx, sy, z, n=8, key='armor_shade', frame_key='gunmetal'):
    """Engine-deck louvres: a sunk frame with n slats across."""
    p.add(props.box(sx, sy, 0.006), frame_key, T(cx, cy, z + 0.003))
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


def antenna(p, x, y, z, h=0.65, r=0.0045):
    p.add(props.lathe([(0, 0.022), (0.04, 0.018)], seg=10), 'gunmetal', T(x, y, z), smooth=True)
    vkit.rod(p, (x, y, z + 0.04), (x - 0.02, y - 0.04, z + h), r, 'gunmetal', seg=4)


def smoke(p, at, yaw, n=4):
    """A bank of smoke-grenade tubes."""
    m = T(*at) @ R('Z', yaw)
    for k in range(n):
        p.add(props.lathe([(0, 0.018), (0.07, 0.018)], seg=8), 'armor_shade',
              m @ T(-0.04 * (n - 1) / 2 + 0.04 * k, 0, 0) @ R('X', -60), smooth=True)


def gun(p, at, length, r, muzzle=None, sleeve=False, elev=2.0, key='armor_paint'):
    """A main gun along +Y from its trunnion: barrel, an optional fume
    extractor / thermal sleeve and muzzle brake."""
    m = T(*at) @ R('X', -90 + elev)
    p.add(props.lathe([(0, r * 1.15), (length * 0.25, r), (length, r * 0.9)], seg=14), key, m, smooth=True)
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

def _half_disc(r, thick, n=10):
    """A half-disc leaf (the x <= 0 half of a circle of radius r), flat in
    XY with its thickness along Z: (bmesh, the matrix laying it flat)."""
    pts = [(-r * math.sin(math.pi * k / n), r * math.cos(math.pi * k / n)) for k in range(n + 1)]
    return props.slab(pts, thick, 'Y'), R('X', 90)


def split_hatch(p, at, r, open_deg=100.0, key='armor_paint', yaw=0.0):
    """The Sherman commander's split hatch: a cast ring, the right leaf shut,
    the left leaf thrown open about its hinge on the outer edge, hinge
    knuckles, a periscope in the shut leaf and a grab handle."""
    m = T(*at) @ R('Z', yaw)
    p.add(props.lathe([(0, r * 1.18), (0.03, r * 1.14), (0.04, r * 1.0)], seg=24), key, m, smooth=True)
    zt = 0.045
    leaf, lr = _half_disc(r, 0.018)
    shut, _ = _half_disc(r, 0.018)
    p.add(shut, key, m @ T(0, 0, zt) @ R('Z', 180) @ lr, bevel=0.004)
    p.add(leaf, key, m @ T(-r, 0, zt) @ R('Y', -open_deg) @ T(r, 0, 0) @ lr, bevel=0.004)
    for sx in (-1, 1):   # the hinge knuckles at both outer edges
        for dy in (-r * 0.45, r * 0.45):
            vkit.rod(p, m @ Vector((sx * r * 1.02, dy - 0.022, zt)), m @ Vector((sx * r * 1.02, dy + 0.022, zt)),
                     0.012, 'gunmetal', seg=8)
    periscope(p, m @ Vector((r * 0.5, r * 0.2, zt + 0.008)), yaw)
    p.add(props.box(0.012, r * 0.7, 0.012), 'gunmetal', m @ T(r * 0.2, -r * 0.3, zt + 0.016))


def rim_hatch(p, at, r, key='armor_paint', normal=(0, 0, 1), yaw=0.0, periscope_at=None, rim_key='armor_shade'):
    """A hull or loader's hatch: the raised rim it seats in, the domed lid,
    two hinge knuckles at the back, a grab handle; optionally a periscope
    set in the lid."""
    m = T(*at) @ _q(normal) @ R('Z', yaw)
    p.add(props.lathe([(0, r * 1.2), (0.016, r * 1.18), (0.02, r * 1.05)], seg=20), rim_key, m, smooth=True)
    p.add(props.lathe([(0.0, r), (0.02, r * 0.98), (0.032, r * 0.75), (0.036, r * 0.3)], seg=20), key, m, smooth=True)
    for dx in (-r * 0.45, r * 0.45):
        vkit.rod(p, m @ Vector((dx - 0.025, -r * 1.05, 0.016)), m @ Vector((dx + 0.025, -r * 1.05, 0.016)),
                 0.013, 'gunmetal', seg=8)
    vkit.rod(p, m @ Vector((-r * 0.35, r * 0.55, 0.04)), m @ Vector((r * 0.35, r * 0.55, 0.04)), 0.007, 'gunmetal', seg=6)
    if periscope_at is not None:
        periscope(p, m @ Vector((periscope_at[0], periscope_at[1], 0.03)), yaw)


def deck_tools(p, x, y0, y1, z, flip=False):
    """Pioneer tools lying on the deck along +Y: a shovel, an axe and a
    crowbar side by side, held by brackets."""
    s = -1 if flip else 1
    L = y1 - y0
    xs = [x - s * 0.035, x, x + s * 0.035]
    vkit.rod(p, (xs[0], y0, z + 0.012), (xs[0], y0 + L * 0.68, z + 0.012), 0.011, 'wood', seg=6)      # the shovel
    p.add(props.box(0.075, 0.1, 0.012), 'gunmetal', T(xs[0], y0 + L * 0.76, z + 0.01), bevel=0.004)
    vkit.rod(p, (xs[1], y0 + L * 0.2, z + 0.012), (xs[1], y1, z + 0.012), 0.01, 'wood', seg=6)          # the axe
    p.add(props.box(0.07, 0.03, 0.014), 'gunmetal', T(xs[1] - s * 0.015, y1 - 0.02, z + 0.012), bevel=0.003)
    vkit.rod(p, (xs[2], y0, z + 0.008), (xs[2], y1 - L * 0.15, z + 0.008), 0.007, 'gunmetal', seg=5)   # the crowbar
    for yy in (y0 + L * 0.2, y0 + L * 0.55, y0 + L * 0.88):   # the brackets across
        p.add(props.box(0.13, 0.014, 0.012), 'armor_shade', T(x, yy, z + 0.022))


def cloth_panel(w, l, thick=0.008, folds=5, amp=0.006, nx=24, ny=10):
    """A cloth panel lying flat (w across X, l along Y, its underside at
    z = 0) with soft folds running along Y and its edges settled lower."""
    rings = []
    for i in range(nx + 1):
        x = -w / 2 + w * i / nx
        ex = 1 - abs(2 * i / nx - 1) ** 6
        top, bot = [], []
        for j in range(ny + 1):
            y = -l / 2 + l * j / ny
            ey = 1 - abs(2 * j / ny - 1) ** 6
            z = amp * (1 + math.sin(folds * math.pi * i / nx + 0.7 * math.sin(3.1 * j / ny))) * ex * ey
            top.append((x, y, z + thick))
            bot.append((x, y, z))
        rings.append(top + bot[::-1])
    return props.loft(rings)


def bedroll(p, a, b, r, key='canvas', straps=3):
    """A rolled tarp or bedroll from a to b, with its straps."""
    vkit.rod(p, a, b, r, key, seg=12)
    a, b = Vector(a), Vector(b)
    u = (b - a).normalized()
    for k in range(straps):
        c = a + (b - a) * (k + 1) / (straps + 1)
        vkit.rod(p, c - u * 0.008, c + u * 0.008, r * 1.08, 'leather_dark', seg=12)


def _tank(p, cfg, d, camo):
    """An M4 Sherman (75 mm, VVSS): the cast turret with its rounded
    bustle and the wide civ band, the M34A1 mantlet and the 75 mm gun, the
    commander's split hatch (one leaf open) and the .50 cal, the loader's
    hatch, a bedroll on the turret side; the glacis with the drivers'
    hatches, the bow MG, lights with guards, spare track links and a civ
    star; the three-piece transmission housing at the nose; the engine deck
    with its grille doors, a rolled tarp and the civ air panel; jerrycans on
    the rear plate; civ stars on the sides; a lighter worn lip along the
    sponsons' foot. The barrel is its own object (no shadow on the ground,
    left out of the fit: see render_vehicles.screen_points)."""
    deck, W, H, hy, Lh, z_f = d['deck'], d['W'], d['H'], d['hy'], d['Lh'], d['z_f']
    up = cfg.get('upper', {})
    fr, fl, rr = up.get('front', 0.0), up.get('front_low', 0.0), up.get('rear', 0.0)
    yt, yb = hy + Lh / 2 * (1 - fr), hy + Lh / 2 * (1 - fl)   # the glacis' top and bottom edges
    gn = Vector((0, H, yb - yt)).normalized()
    rn = Vector((0, -H, Lh / 2 * rr)).normalized()

    def glacis(x, t, off=0.0):   # a point on the glacis, t = 0 at its top edge, 1 at its foot
        return Vector((x, yt + (yb - yt) * t, deck - H * t)) + gn * off

    def rear(x, t, off=0.0):     # a point on the rear plate, t = 0 at its foot, 1 at the deck
        return Vector((x, hy - Lh / 2 * (1 - rr * t), z_f + H * t)) + rn * off

    # ---- the turret
    ty, rx, ry, rz = cfg.get('turret_y', 0.0), 0.36, 0.37, cfg.get('turret_rz', 0.3)
    flat = cfg.get('roof_flat', 0.6)          # how much of the dome above mid-height is kept (lower: flatter)
    bustle = cfg.get('bustle', 1.25)          # how far the bustle is drawn back
    over = cfg.get('bustle_lift', 0.0)        # the bustle's underside lifted clear of the deck (an overhang)
    tur = props.ellipsoid(rx, ry, rz, seg=32)
    for v in tur.verts:
        z = v.co.z
        if z < 0:
            v.co.z *= 0.2
            if over and v.co.y < -ry * 0.3:   # under the bustle: lifted, so it overhangs the deck
                v.co.y = -ry * 0.3 + (v.co.y + ry * 0.3) * bustle
                if v.co.y < -ry * 0.8:
                    v.co.z = rz * over
            continue
        u = min(z / rz, 0.999)
        target = 1 - 0.22 * u if u <= 0.5 else 0.89 * math.sqrt(max(0.0, 1 - ((u - 0.5) / 0.5) ** 2))
        k = target / max(math.sqrt(1 - u * u), 1e-3)
        v.co.x *= k
        v.co.y *= k
        if u > 0.5:
            v.co.z = rz * 0.5 + (z - rz * 0.5) * flat   # a flatter roof with a rounded edge
        if v.co.y < -ry * 0.3:   # the bustle drawn back
            v.co.y = -ry * 0.3 + (v.co.y + ry * 0.3) * bustle
            if over and v.co.y < -ry * 0.8 and v.co.z < rz * over:
                v.co.z = rz * over
        if v.co.y > ry * 0.75:   # the flat front behind the mantlet
            v.co.y = ry * 0.75 + (v.co.y - ry * 0.75) * 0.4
    tz = deck + 0.045
    b0, b1 = cfg.get('band') or (1.0, 0.0)   # band=None: no band
    front = cfg.get('band_front', 2.0)   # faces whose heading's cosine (from +Y) exceeds this stay paint

    dash = cfg.get('band_dash')   # (dash, gap) in degrees round the turret: a broken stripe
    clear = cfg.get('band_clear', 0.0)   # degrees kept clear either side of 90 (round the turret stars)

    def band(f):
        c = f.calc_center_median()
        if tz + b0 < c.z < tz + b1 and (c.y - ty) / max(math.hypot(c.x, c.y - ty), 1e-6) < front:
            a = math.degrees(math.atan2(c.x, c.y - ty))
            if abs(abs(a) - 90.0) < clear or abs(a) > cfg.get('band_rear', 181.0):
                return None
            if dash and (abs(a) - 90.0 + clear) % (dash[0] + dash[1]) >= dash[0]:
                return None
            return ('tint', True)
        return None
    p.add(tur, 'armor_paint', T(0, ty, tz), smooth=True, keys=band)
    p.add(props.lathe([(0, rx * 0.93), (0.05, rx * 0.93)], seg=32), 'armor_shade', T(0, ty, deck - 0.005), smooth=True)
    if cfg.get('turret_cast'):   # the cast turret's lip at its ring and the parting seam round its walls
        sy_ = ry / rx
        p.add(props.lathe([(0, rx * 1.035), (0.016, rx * 1.035), (0.026, rx * 0.99)], seg=40), 'armor_paint',
              T(0, ty, tz - 0.024) @ props.S(1.0, sy_, 1.0), smooth=True)
        zs_ = rz * 0.36
        rs_ = rx * (1 - 0.22 * zs_ / rz) + 0.008
        p.add(props.lathe([(-0.007, rs_), (0.007, rs_ + 0.002)], seg=40), 'shade_dark',
              T(0, ty, tz + zs_) @ props.S(1.0, sy_, 1.0), smooth=True)
    if cfg.get('turret_shade'):   # a dark shade line where the turret meets the hull roof
        p.add(props.lathe([(0, rx * 1.03), (0.022, rx * 1.0)], seg=40), 'shade_dark', T(0, ty, deck), smooth=True)
    roof = tz + rz * (0.5 + 0.5 * flat)
    fy = ty + ry * 0.79
    # the M34A1 mantlet: rotor shield, the gun shield with its cheeks, the gun, the coax, the sight
    gz = tz + 0.1
    mk = cfg.get('mantlet_key', 'armor_paint')
    if cfg.get('mantlet_block'):   # a crisp cast block where the gun leaves the turret
        p.add(props.box(0.38, 0.08, 0.16), mk, T(0, fy + 0.005, gz), bevel=0.008)
    else:
        p.add(props.box(0.36, 0.05, 0.17), mk, T(0, fy, gz), bevel=0.02)
    p.add(plate(0.22, 0.08, 0.14, cell=0.04, taper=0.12), mk, T(0.0, fy + 0.055, gz), bevel=0.025)
    for sx in (-1, 1):
        p.add(props.box(0.035, 0.1, 0.15), mk, T(sx * 0.13, fy + 0.05, gz), bevel=0.012)
    gp = vkit.part(p.root, 'gun', p.colors, p.weather)
    gl, gr = cfg.get('gun_len', 0.74), cfg.get('gun_r', 0.042)
    gm = T(0, fy + 0.09, gz) @ R('X', -90 + 2.0)   # the muzzle collar and the dark bore
    tube = cfg.get('gun_tube')
    if tube:   # a plain tube: no smooth-shaded cap (it read as a rounded blob), a flat face, a recessed bore
        gk = cfg.get('gun_key', 'armor_paint')
        gp.add(props.tube([(0, 0, 0), (0, 0, gl * 0.25), (0, 0, gl)], [gr * 1.15, gr, gr * 0.92], seg=16, caps=False),
               gk, gm, smooth=True)
        n = 16
        ring = lambda r, z: [(r * math.cos(2 * math.pi * k / n), r * math.sin(2 * math.pi * k / n), z) for k in range(n)]
        gp.add(props.loft([ring(gr * 0.92, gl), ring(gr * 0.58, gl)], False, False), gk, gm)
        gp.add(props.tube([(0, 0, gl - 0.05), (0, 0, gl)], gr * 0.58, seg=12, caps=True), 'port', gm)
    else:
        gun(gp, (0, fy + 0.09, gz), gl, gr, muzzle=None, key=cfg.get('gun_key', 'armor_paint'))
    if tube:
        gp.add(props.tube([(0, 0, gl - 0.03), (0, 0, gl - 0.002)], gr * cfg.get('muzzle_r', 1.08), seg=16, caps=False),
               cfg.get('muzzle_key', 'armor_shade'), gm, smooth=True)
    elif cfg.get('muzzle_slim'):
        gp.add(props.lathe([(gl - 0.03, gr * 0.9), (gl - 0.024, gr * 1.1), (gl, gr * 1.1), (gl + 0.004, gr * 0.95)],
                           seg=16), cfg.get('muzzle_key', 'armor_shade'), gm, smooth=True)
    else:
        gp.add(props.lathe([(gl - 0.07, gr * 0.95), (gl - 0.06, gr * 1.3), (gl + 0.004, gr * 1.3), (gl + 0.01, gr * 1.1)],
                           seg=16), cfg.get('muzzle_key', 'armor_shade'), gm, smooth=True)
    if not tube:
        gp.add(props.lathe([(gl + 0.008, gr * 0.62), (gl + 0.012, gr * 0.62)], seg=12), 'port', gm, smooth=True)
    if cfg.get('gun_highlight'):   # a light top edge so the barrel reads when foreshortened
        gp.add(props.lathe([(0.02, gr * 0.16), (gl - 0.07, gr * 0.14)], seg=6), 'gun_edge',
               gm @ T(0, -gr * 0.9, 0), smooth=True)
        gp.add(props.lathe([(-0.01, gr * 1.5), (0.03, gr * 1.45)], seg=16), 'shade_dark', gm, smooth=True)   # the root's shadow
    gobj = gp.finish()
    gobj['no_fit'] = bool(cfg.get('gun_no_fit'))
    if cfg.get('gun_no_shadow'):   # its long thin shadow read as a stray line at 1x
        gobj.visible_shadow = False
    vkit.rod(p, (0.075, fy + 0.08, gz + 0.01), (0.075, fy + 0.15, gz + 0.01), 0.011, 'gunmetal', seg=6)
    p.add(props.box(0.03, 0.012, 0.03), 'port', T(-0.075, fy + 0.1, gz + 0.03))
    # the roof: the commander's split hatch (open), the .50 cal behind it, the loader's hatch, periscopes, rings
    simple = cfg.get('simple_roof')   # at 1x the small roof kit was dark speckle: one ring, the MG
    if simple:
        hrim = cfg.get('hatch_rim', 'armor_edge')
        if cfg.get('cupola'):   # the commander's small vision cupola: a raised drum, dark vision slits, a lid
            cz = roof - 0.012
            p.add(props.lathe([(0, 0.12), (0.045, 0.115), (0.055, 0.1)], seg=24), hrim, T(0.12, ty - 0.06, cz), smooth=True)
            for k in range(6):
                a = math.radians(30 + 60 * k)
                p.add(props.box(0.04, 0.008, 0.016), 'port',
                      T(0.12 + 0.117 * math.cos(a), ty - 0.06 + 0.117 * math.sin(a), cz + 0.028) @ R('Z', math.degrees(a) + 90))
            rim_hatch(p, (0.12, ty - 0.06, cz + 0.05), 0.085, rim_key=hrim, key=cfg.get('hatch_key', 'armor_paint'))
        else:
            rim_hatch(p, (0.12, ty - 0.06, roof - 0.012), 0.11, rim_key=hrim, key=cfg.get('hatch_key', 'armor_paint'))
        if cfg.get('loader_hatch'):
            rim_hatch(p, (-0.15, ty - 0.02, roof - 0.022), cfg['loader_hatch'], rim_key=cfg.get('hatch_rim', 'armor_edge'),
                      key=cfg.get('hatch_key', 'armor_paint'))
    else:
        split_hatch(p, (0.13, ty - 0.08, roof - 0.01), 0.1, open_deg=cfg.get('hatch_open', 100.0))
    mg(p, (0.13, ty - 0.27, roof + 0.07), yaw=10, length=0.42, pitch=8)
    if not simple:
        rim_hatch(p, (-0.15, ty - 0.02, roof - 0.012), 0.07, yaw=90)
        periscope(p, (-0.15, ty + 0.13, roof - 0.005), 0)
        periscope(p, (0.04, ty + 0.17, roof - 0.005), 0)
    for x in (() if simple else (-0.24, 0.24)):   # lifting rings
        vkit.rod(p, (x, ty + 0.16, roof - 0.03), (x, ty + 0.21, roof - 0.03), 0.013, 'gunmetal', seg=6)
    if cfg.get('turret_star'):   # a civ star on each turret side, set into the cast wall
        zs = cfg.get('turret_star_z', 0.085)
        u = zs / rz
        rxe = rx * (1 - 0.22 * u) * 0.995
        sn_ = Vector((1.0, 0.0, 0.22 * rx / rz)).normalized()
        for sx in (-1, 1):
            # set a little into the curved wall so the points don't stand off it
            mark(p, 'star', cfg['turret_star'], (sx * (rxe - 0.008), ty - 0.04, tz + zs), (sx * sn_.x, 0.0, sn_.z))
    # the pistol port on the left side, an optional antenna on the bustle
    pn = Vector((-1, -0.25, 0.15)).normalized()
    p.add(props.lathe([(0, 0.034), (0.014, 0.03)], seg=14), 'armor_paint',
          T(-rx * 0.86, ty - 0.12, tz + 0.08) @ _q(pn), smooth=True)
    if cfg.get('antenna'):   # (off on the Tank: its height counts in the fit and shrinks the whole tank)
        antenna(p, -0.2, ty - ry * 1.05, roof - 0.07, h=cfg['antenna'])
    # a bedroll strapped on the turret's left side
    if not simple:
        bedroll(p, (-rx * 0.92, ty - 0.24, tz + 0.15), (-rx * 0.97, ty + 0.06, tz + 0.15), 0.038, key='canvas_dark')
    # ---- the hull front: the drivers' hatches, bow MG, lights, spare links, a star; the transmission housing
    hx = cfg.get('hull_hatch_x', 0.24)
    for x in (-hx, hx):
        if cfg.get('hull_hatches'):   # the drivers' hatches as larger, light-rimmed shapes
            rim_hatch(p, (x, yt - cfg['hull_hatches'] - 0.004, deck), cfg['hull_hatches'], yaw=0,
                      rim_key=cfg.get('hatch_rim', 'armor_edge'),
                      key=cfg.get('hatch_key', 'armor_paint'))
        else:
            rim_hatch(p, (x, yt - 0.075, deck), 0.065, yaw=0, periscope_at=(0.0, 0.1))
    bm = glacis(0.21, 0.55, 0.012)
    bk = cfg.get('bow_key', 'armor_paint')
    br = 0.05 * cfg.get('bow_scale', 1.0)
    p.add(props.ellipsoid(br, br, br, seg=14), bk, T(*bm), smooth=True)
    vkit.rod(p, bm, bm + Vector((0, 0.14, -0.01)) * cfg.get('bow_scale', 1.0), 0.011 * cfg.get('bow_scale', 1.0),
             'gun_olive' if bk != 'armor_paint' else 'gunmetal', seg=6)
    if cfg.get('glacis_joint'):   # the bolted joint where the glacis meets the cast nose: a dark line, a lit flange
        p.add(props.box(W - 2 * d['tw'] + 0.06, 0.02, 0.018), 'shade_dark', T(0, yb + 0.004, z_f - 0.006), bevel=0.004)
        p.add(props.box(W - 2 * d['tw'] + 0.06, 0.012, 0.01), 'armor_edge', T(0, yb + 0.012, z_f + 0.008), bevel=0.003)
    for sx in (-1, 1):
        q = glacis(sx * 0.4, 0.35, -0.012)
        light(p, q.x, q.y, q.z + 0.03, r=0.03)
    for k in range(3):   # spare track links bolted across the lower glacis, under the gun
        q = glacis(-0.02, 0.66 + 0.11 * k, 0.012)
        lm = T(*q) @ _q(gn) @ R('Z', 90)
        p.add(props.box(0.05, 0.2, 0.024), 'track', lm, bevel=0.004)
        for dy in (-0.105, 0.105):
            p.add(props.box(0.02, 0.018, 0.034), 'track_pin', lm @ T(0, dy, 0), bevel=0.003)
    vkit.rod(p, glacis(-0.02, 0.6, 0.03), glacis(-0.02, 0.98, 0.03), 0.008, 'gunmetal', seg=5)   # the retaining bar
    mark(p, 'star', cfg.get('glacis_star', 0.12), glacis(-0.22, 0.42, 0.002), gn)
    hw = W - 2 * d['tw'] - 0.02
    hz, hr = z_f - 0.03, 0.11
    hcy = yb - 0.02
    p.add(props.lathe([(-hw / 2, hr), (hw / 2, hr)], seg=20), 'armor_paint', T(0, hcy, hz) @ R('Y', 90), smooth=True)
    for x in (-hw * 0.18, hw * 0.18):   # the bolted flanges of the three-piece housing
        p.add(props.lathe([(-0.012, hr * 1.07), (0.012, hr * 1.07)], seg=20), 'armor_paint',
              T(x, hcy, hz) @ R('Y', 90), smooth=True)
    for sx in (-1, 1):   # the tow eyes
        p.add(props.lathe([(0, 0.024), (0.035, 0.02)], seg=8), 'gunmetal',
              T(sx * hw * 0.38, hcy + hr * 0.75, hz - hr * 0.55) @ R('X', -60), smooth=True)
    if cfg.get('hull_edges'):   # a worn highlight along the deck's side edges and weld seams on the sponsons
        ry0_ = hy - Lh / 2 * (1 - rr)
        for sx in (-1, 1):
            p.add(props.box(0.016, yt - ry0_, 0.014), 'armor_edge', T(sx * (W / 2 - 0.002), (yt + ry0_) / 2, deck), bevel=0.004)
            for yy in (hy - Lh * 0.18, hy + Lh * 0.2):
                p.add(props.box(0.008, 0.01, H * 0.9), 'armor_shade', T(sx * (W / 2 + 0.003), yy, z_f + H * 0.5))
            p.add(props.box(0.008, Lh * 0.9, 0.01), 'armor_shade', T(sx * (W / 2 + 0.003), hy, z_f + H * 0.82))
        for yy in (yt, ry0_):   # the breaks where the glacis and the rear plate meet the deck
            p.add(props.box(W * 0.98, 0.016, 0.014), 'armor_edge', T(0, yy, deck), bevel=0.004)
    # ---- the hull sides: a lighter worn lip along the sponsons' foot (the hull's edge over the tracks), civ stars
    if cfg.get('sponson_lip'):
        for sx in (-1, 1):
            p.add(props.box(0.018, Lh * 0.985, 0.034), cfg.get('lip_key', 'armor_edge'),
                  T(sx * (W / 2 + 0.007), hy, z_f + 0.012), bevel=0.005)
    if cfg.get('shadow_line'):   # the dark gap under the hull's side overhang, over the track's top run
        for sx in (-1, 1):
            p.add(props.box(0.012, Lh * 0.96, 0.02), 'port', T(sx * (W / 2 + 0.004), hy, z_f - 0.012))
    if cfg.get('side_tools'):   # a shovel and a pick strapped on each hull side, ahead of the star
        for sx in (-1, 1):
            x = sx * (W / 2 + 0.016)
            y0, y1 = hy + Lh * 0.1, hy + Lh * 0.42
            zt = z_f + H * 0.66
            vkit.rod(p, (x, y0, zt), (x, y1 - 0.08, zt), 0.014, 'wood_dark', seg=6)            # the shovel
            p.add(props.box(0.014, 0.1, 0.075), 'armor_edge', T(x, y1 - 0.03, zt), bevel=0.006)
            zp = z_f + H * 0.32
            vkit.rod(p, (x, y0 + 0.02, zp), (x, y1, zp), 0.013, 'wood_dark', seg=6)          # the pick
            p.add(props.box(0.016, 0.03, 0.16), 'shade_dark', T(x, y1 - 0.035, zp), bevel=0.005)
            for yy in (y0 + 0.06, y1 - 0.12):   # the straps
                p.add(props.box(0.01, 0.014, H * 0.62), 'leather_dark', T(x - sx * 0.002, yy, z_f + H * 0.49))
    for sx in (-1, 1):
        mark(p, 'star', cfg.get('side_star', 0.105), (sx * (W / 2 + 0.004), hy - Lh * cfg.get('side_star_y', 0.02),
                                                      z_f + H * 0.48), (sx, 0, 0))
    # ---- the engine deck: a rolled tarp, the grille doors (no small kit: at 1x it was orange specks)
    ry0 = hy - Lh / 2 * (1 - rr)   # the deck's rear edge
    by = ty - ry * 1.22
    if cfg.get('tarp', True):
        bedroll(p, (-0.3, by, deck + 0.05), (0.3, by, deck + 0.05), 0.05, key='canvas')
        gy_ = (by - 0.06 + ry0) / 2
    else:   # no roll: the grilles centred between the bustle's overhang and the deck's rear edge
        gy_ = (ty - ry * (0.3 + 0.7 * bustle) + ry0) / 2 + 0.01
    if cfg.get('deck_grilles'):   # two big louvred doors in dark frames, readable at 1x
        for sx in (-1, 1):
            p.add(props.box(W * 0.36, 0.17, 0.014), 'armor_edge', T(sx * W * 0.21, gy_ - 0.01, deck + 0.005), bevel=0.004)
            grille(p, sx * W * 0.21, gy_ - 0.01, W * 0.32, 0.14, deck + 0.01, n=5, key='armor_shade', frame_key='deck_dark')
        if cfg.get('deck_bundle'):   # a tarp-wrapped bundle strapped over the left door
            stowage(p, (-W * 0.24, gy_ - 0.01, deck + 0.012), cfg['deck_bundle'], key='canvas', straps=2)
    else:
        grille(p, 0, gy_, W * 0.46, 0.11, deck, n=6)
    if cfg.get('air_panel'):   # a civ air-recognition panel tied over the engine deck, its ties at the corners
        pw, pl = cfg['air_panel']
        p.add(cloth_panel(pw, pl), 'tint', T(0, gy_ - 0.01, deck + 0.016), tint=True, smooth=True)
        for sx in (-1, 1):
            for dy in (-pl / 2, pl / 2):
                p.add(props.box(0.02, 0.02, 0.03), 'leather_dark', T(sx * pw / 2, gy_ - 0.01 + dy, deck + 0.012))
    # ---- the rear: jerrycans in a rack, the exhaust deflector
    if cfg.get('rear_plain'):   # the rear plate: two access doors' seams and two tow hooks, no boxes
        for x in (-0.2, 0.2):
            a, b = rear(x, 0.15, 0.004), rear(x, 0.9, 0.004)
            vkit.beam(p, a, b, 0.008, 0.008, 'armor_shade')
            p.add(props.lathe([(0, 0.03), (0.04, 0.024)], seg=10), 'gunmetal',
                  T(*rear(x * 1.6, 0.1, 0.0)) @ _q(rn), smooth=True)
        vkit.beam(p, rear(-0.36, 0.92, 0.004), rear(0.36, 0.92, 0.004), 0.008, 0.008, 'armor_shade')
        if cfg.get('rear_star'):
            mark(p, 'star', cfg['rear_star'], rear(0.0, 0.5, 0.002), rn)
    for x in (() if cfg.get('rear_plain') else (-0.3, -0.18, 0.18, 0.3)):
        q = rear(x, 0.12)
        p.add(props.box(0.11, 0.06, 0.012), 'armor_shade', T(x, q.y - 0.02, q.z - 0.012))
        jerrycan(p, x, q.y - 0.035, q.z - 0.006, yaw=0, key='armor_camo2')
    p.add(props.box(W * 0.62, 0.03, 0.12), 'armor_shade', T(0, hy - Lh / 2 - 0.02, z_f - 0.04) @ R('X', -20), bevel=0.006)
    if cfg.get('air_cleaners'):   # the two air-cleaner drums on the rear plate, dark caps
        for sx in (-1, 1):
            q = rear(sx * 0.33, 0.5, 0.055)
            p.add(props.lathe([(-0.08, 0.05), (0.08, 0.05)], seg=16), 'armor_camo2', T(*q), smooth=True)
            p.add(props.lathe([(0.08, 0.054), (0.1, 0.05), (0.104, 0.03)], seg=16), 'shade_dark', T(*q), smooth=True)
            p.add(props.box(0.03, 0.06, 0.012), 'shade_dark', T(q.x, q.y + 0.03, q.z - 0.04))


def _wall(p, a, b, z0, z1, t, key, bevel=0.003, lean=0.0, center=(0.0, 0.0)):
    """A thin plate from a to b (plan points), z0 to z1, t thick; lean tips
    its top in toward center by that many degrees."""
    (x0, y0), (x1, y1) = a, b
    ln = math.hypot(x1 - x0, y1 - y0)
    yaw = math.degrees(math.atan2(-(x1 - x0), y1 - y0))
    mx, my = (x0 + x1) / 2, (y0 + y1) / 2
    out = math.cos(math.radians(yaw)) * (mx - center[0]) + math.sin(math.radians(yaw)) * (my - center[1])
    tilt = -lean if out > 0 else lean
    p.add(props.box(t, ln, z1 - z0), key, T(mx, my, (z0 + z1) / 2) @ R('Z', yaw) @ R('Y', tilt), bevel=bevel)


def _panzer(p, cfg, d, camo):
    """A Panzer IV Ausf. H: the hexagonal turret wrapped in its spaced
    skirt (turret numbers in civ colour on its sides and rear), the cupola
    at its back, the long 75 mm L/48 with its double-baffle brake (its own
    object, out of the fit, no shadow); the stepped front (the vertical
    driver's plate with visor and ball MG over the transmission deck and
    the sloped nose with spare links, civ tactical signs); hull skirts of
    five plates a side on a rail (a Balkenkreuz on the middle one); the
    engine deck's louvred hatches; the big muffler on the rear plate."""
    deck, W, H, z_f = d['deck'], d['W'], d['H'], d['z_f']
    hy, Lh, uy, Lu = d['hy'], d['Lh'], d['uy'], d['Lu']
    yF = uy + Lu / 2                  # the driver's plate (vertical)
    yR = uy - Lu / 2                  # the upper hull's rear plate
    yN = hy + Lh * 0.96 / 2           # the nose's top edge (the lower hull)
    tk = 'armor_turret' if cfg.get('turret_tone') else 'armor_paint'
    # ---- the turret: hexagonal in plan, sloped sides, a flat roof
    ty = hy + cfg.get('turret_dy', 0.02)
    tw_, tl, th_ = cfg.get('turret_w', 0.6), cfg.get('turret_l', 0.6), cfg.get('turret_h', 0.21)
    tz0 = deck + 0.012
    p.add(plate(tw_, tl, th_, cell=0.05, taper=cfg.get('turret_taper', 0.16), plan_front=0.3, front=cfg.get('turret_front', 0.1),
                rear=0.03), tk,
          T(0, ty, tz0 + th_ / 2), bevel=0.012)
    roof = tz0 + th_
    p.add(props.lathe([(0, tw_ * 0.47), (0.02, tw_ * 0.47)], seg=28), 'shade_dark', T(0, ty, deck - 0.006), smooth=True)
    # the stowage bin on the turret rear (inside the skirt)
    p.add(props.box(tw_ * 0.7, 0.1, 0.13), tk, T(0, ty - tl / 2 - 0.045, roof - 0.085), bevel=0.01)
    # the turret skirt: side plates, the angled front ends, the rear wrapping the bin; brackets
    sx_, sf, sb = cfg.get('skirt_x', 0.4), ty + tl * 0.22, ty - tl / 2 - 0.12
    z0, z1 = tz0 + 0.005, roof - cfg.get('skirt_drop', 0.01)
    st = cfg.get('skirt_t', 0.012)
    tl_ = cfg.get('skirt_lean', 0.0)
    cc = (0.0, ty)
    for sgn in (-1, 1):
        _wall(p, (sgn * sx_, sb + 0.05), (sgn * sx_, sf), z0, z1, st, tk, lean=tl_, center=cc)
        _wall(p, (sgn * sx_, sf), (sgn * (sx_ - 0.07), sf + 0.09), z0, z1, st, tk, lean=tl_, center=cc)
        _wall(p, (sgn * sx_, sb + 0.05), (sgn * (sx_ - 0.05), sb), z0, z1, st, tk, lean=tl_, center=cc)
        bz = z0 + cfg.get('bracket_z', 0.17) * (z1 - z0)
        for yy in (sf - 0.05, ty - 0.05, sb + 0.1):   # the brackets out from the turret wall (low: hidden from above)
            p.add(props.box(sx_ - tw_ * 0.4, 0.025, 0.02), 'shade_dark', T(sgn * (sx_ + tw_ * 0.4) / 2, yy, bz))
        # the side doors' seams; a light top edge
        for yy in (ty + 0.02, ty - 0.16):
            p.add(props.box(st * 1.3, 0.008, (z1 - z0) * 0.92), 'shade_dark', T(sgn * sx_, yy, (z0 + z1) / 2))
        if not tl_ and cfg.get('tskirt_edge', True):
            p.add(props.box(st * 1.4, sf - sb - 0.04, 0.01), 'armor_edge', T(sgn * sx_, (sf + sb) / 2 + 0.02, z1 - 0.002))
    _wall(p, (-(sx_ - 0.05), sb), (sx_ - 0.05, sb), z0, z1, st, tk, lean=tl_, center=cc)
    if not tl_ and cfg.get('tskirt_edge', True):
        p.add(props.box((sx_ - 0.05) * 2, st * 1.4, 0.01), 'armor_edge', T(0, sb, z1 - 0.002))
    if cfg.get('skirt_lid'):   # close the gap from above: a flange from the turret's roof edge out to the skirt
        tt = (z1 - tz0) / th_
        xi = tw_ / 2 * (1 - cfg.get('turret_taper', 0.16) * tt) + 0.012
        for sgn in (-1, 1):
            if cfg.get('shadow_gaps'):   # a dark seam where the flange meets the turret
                p.add(props.box(0.016, sf - sb - 0.05, 0.014), 'port', T(sgn * (xi - 0.004), (sf + sb + 0.05) / 2, z1 - 0.006))
            p.add(props.box(sx_ - xi + st / 2, sf - sb - 0.05, 0.012), tk, T(sgn * (xi + sx_ + st / 2) / 2, (sf + sb + 0.05) / 2, z1 - 0.006),
                  bevel=0.003)
        yi = ty - tl / 2 * (1 - 0.03 * tt) - 0.012
        p.add(props.box(2 * (sx_ - 0.05), yi - sb, 0.012), tk, T(0, (yi + sb) / 2, z1 - 0.006), bevel=0.003)
    if cfg.get('corner_plate'):   # a civ number plate on each angled front end of the turret skirt (head-on colour)
        for sgn in (-1, 1):
            n_ = Vector((sgn * 0.09, 0.07, 0.0)).normalized()
            c_ = Vector((sgn * (sx_ - 0.035), sf + 0.045, (z0 + z1) / 2)) + n_ * (st / 2 + 0.001)
            mark(p, 'rect', cfg['corner_plate'], c_, n_)
    # turret numbers (civ): both skirt sides and the rear
    nh = cfg.get('num_h', 0.11)
    for sgn in (-1, 1):
        ln_ = math.radians(tl_)
        number(p, cfg.get('number', '421'), nh, (sgn * (sx_ + st / 2), ty - 0.07, (z0 + z1) / 2),
               (sgn * math.cos(ln_), 0, math.sin(ln_)),
               stroke=cfg.get('num_stroke', 0.24), width=cfg.get('num_w', 0.58))
    number(p, cfg.get('number', '421'), nh, (0, sb - st / 2, (z0 + z1) / 2),
           (0, -math.cos(math.radians(tl_)), math.sin(math.radians(tl_))), stroke=cfg.get('num_stroke', 0.24),
           width=cfg.get('num_w', 0.58))
    # the cupola at the rear centre: a drum with vision slits, its lid; a ventilator
    cy = ty - tl * 0.3
    ch_ = cfg.get('cupola_h', 0.06)
    p.add(props.lathe([(0, 0.09), (ch_, 0.085), (ch_ + 0.01, 0.07)], seg=24), cfg.get('cupola_key', tk), T(0, cy, roof - 0.01),
          smooth=True)
    for k in range(6):
        a = math.radians(30 + 30 * k)
        p.add(props.box(0.035, 0.008, 0.018), 'port',
              T(0.086 * math.cos(a), cy + 0.086 * math.sin(a), roof + ch_ - 0.03) @ R('Z', math.degrees(a) + 90))
    p.add(props.lathe([(0, 0.075), (0.015, 0.072), (0.03, 0.03)], seg=20), 'armor_camo2', T(0, cy, roof + ch_),
          smooth=True)
    p.add(props.lathe([(0, 0.03), (0.02, 0.03), (0.035, 0.012)], seg=12), 'shade_dark', T(0.12, ty + 0.12, roof - 0.004),
          smooth=True)
    # the mantlet, the coax and sight ports; the gun
    fy = ty + tl / 2 * 0.95
    gz = tz0 + th_ * 0.5
    mk = cfg.get('mantlet_key', tk)
    mw, mh = cfg.get('mantlet', (0.27, 0.15))
    p.add(props.box(mw, 0.08, mh), mk, T(0, fy + 0.015, gz), bevel=0.012)
    p.add(props.box(mw, 0.02, 0.012), 'armor_edge', T(0, fy + 0.045, gz + mh / 2 - 0.003), bevel=0.003)   # the light top catch
    for sgn in (-1, 1):   # light side catches
        p.add(props.box(0.012, 0.02, mh * 0.9), 'armor_edge', T(sgn * (mw / 2 - 0.003), fy + 0.045, gz), bevel=0.003)
    p.add(props.box(0.03, 0.012, 0.022), 'port', T(-0.08, fy + 0.048, gz + 0.015))                 # the coax
    p.add(props.box(0.026, 0.012, 0.026), 'port', T(0.08, fy + 0.048, gz + 0.025))                 # the sight
    for sgn in (-1, 1):   # the vision ports either side of the mantlet
        p.add(props.box(0.05, 0.012, 0.016), 'port', T(sgn * 0.2, ty + tl / 2 * 0.86, gz + 0.04) @ R('Z', -sgn * 25))
    gp = vkit.part(p.root, 'gun', p.colors, p.weather)
    gl, gr = cfg.get('gun_len', 0.76), cfg.get('gun_r', 0.034)
    gm = T(0, fy + 0.04, gz) @ R('X', -90 + 1.5)
    gk = cfg.get('gun_key', 'gun_olive')
    gp.add(props.tube([(0, 0, 0), (0, 0, 0.12), (0, 0, 0.16), (0, 0, gl)], [gr * cfg.get('sleeve', 1.45)] * 2 + [gr * 1.05, gr * 0.9],
                      seg=16, caps=False), gk, gm, smooth=True)
    # the double-baffle brake: two chambers split by a dark groove, a light front ring, the dark bore
    rb, bl = gr * cfg.get('brake_r', 1.75), cfg.get('brake_len', 0.085)
    if cfg.get('brake_stepped'):   # two separate chambers on a bare neck: a stepped tip at 1x, not a ball
        prof = [(gl - 0.01, gr * 0.9), (gl - 0.004, rb * 0.86), (gl + bl * 0.36, rb * 0.86), (gl + bl * 0.37, gr * 0.85),
                (gl + bl * 0.6, gr * 0.85), (gl + bl * 0.61, rb), (gl + bl, rb)]
    else:
        prof = [(gl - 0.01, gr * 0.9), (gl - 0.004, rb), (gl + bl * 0.42, rb), (gl + bl * 0.46, rb * 0.72),
                (gl + bl * 0.56, rb * 0.72), (gl + bl * 0.6, rb), (gl + bl, rb)]
    gp.add(props.lathe(prof, seg=cfg.get('brake_seg', 16)), cfg.get('brake_key', 'shade_dark'), gm,
           smooth=cfg.get('brake_seg', 16) > 8)
    if cfg.get('brake_lit'):   # a light top edge on each chamber: the brake reads as a stepped block
        for f0, f1, rr_ in ((0.02, 0.34, rb * 0.86), (0.63, 0.98, rb)):
            gp.add(props.box(rr_ * 0.9, 0.012, bl * (f1 - f0)), 'muzzle_light', gm @ T(0, -rr_ * 0.97, gl + bl * (f0 + f1) / 2))
    for sgn in (-1, 1):   # the baffle windows on the sides
        for f0 in (0.2, 0.75):
            gp.add(props.box(0.006, bl * 0.22, rb * 1.1), 'port', gm @ T(sgn * rb * 0.97, 0, gl + bl * f0) @ R('X', 90))
    n = 16
    ring = lambda r, z: [(r * math.cos(2 * math.pi * k / n), r * math.sin(2 * math.pi * k / n), z) for k in range(n)]
    gp.add(props.loft([ring(rb, gl + bl), ring(rb * 0.55, gl + bl)], False, False), 'muzzle_light', gm)
    gp.add(props.tube([(0, 0, gl + bl - 0.03), (0, 0, gl + bl - 0.002)], rb * 0.55, seg=12, caps=True), 'port', gm)
    gp.add(props.lathe([(0.17, gr * 0.17), (gl - 0.02, gr * 0.15)], seg=6), 'gun_edge', gm @ T(0, -gr * 0.88, 0),
           smooth=True)   # the light top edge
    gobj = gp.finish()
    gobj['no_fit'] = True
    gobj.visible_shadow = False
    # ---- the superstructure: the driver's plate (visor left, ball MG right), the hatches ahead of the turret
    vs = cfg.get('front_scale', 1.0)
    fx_ = cfg.get('front_x', 0.24)   # the visor (left) and the MG ball (right) off the centre line
    p.add(props.box(0.17 * vs, 0.03 * vs, 0.06 * vs), 'armor_paint', T(-fx_, yF + 0.012 * vs, z_f + H * 0.6), bevel=0.008)
    p.add(props.box(0.12 * vs, 0.012, 0.014 * vs), 'port', T(-fx_, yF + 0.024 * vs + 0.006, z_f + H * 0.64))
    if vs != 1.0:   # the MG's dark mount ring
        p.add(props.lathe([(0, 0.052 * vs), (0.012, 0.049 * vs)], seg=16), 'shade_dark', T(fx_, yF, z_f + H * 0.55) @ R('X', -90), smooth=True)
    p.add(props.ellipsoid(0.055 * vs, 0.035 * vs, 0.055 * vs, seg=14), cfg.get('ball_key', 'armor_paint'), T(fx_, yF + 0.005, z_f + H * 0.55),
          smooth=True)
    if cfg.get('final_drives'):   # the round final-drive housings at the nose's corners, ahead of the sprockets
        fx = W / 2 - d['tw'] - 0.02
        fyw, fzw, _ = cfg['front_wheel']
        for sgn in (-1, 1):
            fr_ = cfg.get('fd_r', 0.06)
            p.add(props.lathe([(-0.05, fr_), (0.03, fr_), (0.05, fr_ * 0.75)], seg=18), cfg.get('fd_key', 'armor_turret'),
                  T(sgn * fx, fyw - 0.02, fzw) @ R('Y', 90 * sgn), smooth=True)
    vkit.rod(p, (fx_, yF + 0.02, z_f + H * 0.55), (fx_, yF + 0.11, z_f + H * 0.55), 0.012, 'gun_olive', seg=6)
    for sgn in (-1, 1):
        rim_hatch(p, (sgn * 0.27, yF - 0.11, deck), 0.07, rim_key='armor_camo2', key='armor_paint')
    if cfg.get('hull_faces'):   # the superstructure's vertical faces in the darker paint, against the lit deck
        rr_ = cfg.get('upper', {}).get('rear', 0.0)
        zc, hh = z_f + H / 2, H / 2 - 0.01
        for sgn in (-1, 1):
            pts = [(yF - 0.01, -hh), (yF - 0.01, hh), (yR + Lu / 2 * rr_ + 0.012, hh), (yR + 0.012, -hh)]
            poly = [((y - uy) * sgn, v) for y, v in pts]
            if sgn < 0:
                poly = poly[::-1]
            mark(p, None, None, (sgn * (W / 2 + 0.001), uy, zc), (sgn, 0, 0), key='armor_turret', thick=0.004, polys=[poly])
        mark(p, 'rect', (W - 0.03, H - 0.02), (0, yF + 0.001, zc), (0, 1, 0), key='armor_turret', thick=0.004)
    # light edges along the deck's sides and front
    for sgn in (-1, 1):
        p.add(props.box(0.016, Lu * 0.98, 0.014), 'armor_edge', T(sgn * (W / 2 - 0.002), uy, deck), bevel=0.004)
    p.add(props.box(W * 0.98, 0.016, 0.014), 'armor_edge', T(0, yF, deck), bevel=0.004)
    # ---- the transmission deck and the nose: two brake hatches, spare links, tow shackles
    td = (yF + yN) / 2
    for sgn in (-1, 1):
        p.add(props.box(0.24, (yN - yF) * 0.6, 0.012), 'armor_paint', T(sgn * 0.17, td, z_f + 0.006), bevel=0.004)
        p.add(props.box(0.24, 0.012, 0.016), 'shade_dark', T(sgn * 0.17, td - (yN - yF) * 0.3, z_f + 0.008))
    nose = cfg.get('nose', 0.5)
    low_h = z_f - d['belly']
    nn = Vector((0, low_h, Lh * 0.96 / 2 * nose)).normalized()     # the nose plate's normal

    def on_nose(x, t, off=0.0):   # t = 0 at the nose's top edge, 1 at its foot
        return Vector((x, yN - Lh * 0.96 / 2 * nose * t, z_f - low_h * t)) + nn * off
    for k in range(cfg.get('links', 5)):   # the spare links across the upper nose
        q = on_nose(-0.24 + 0.12 * k, 0.28, 0.014)
        lm = T(*q) @ _q(nn)
        lw, ll = cfg.get('link', (0.1, 0.06))
        p.add(props.box(lw, ll, 0.026), 'track', lm, bevel=0.004)
        p.add(props.box(0.022, 0.07, 0.03), 'track_pin', lm @ T(0, 0, 0.004), bevel=0.003)
    vkit.rod(p, on_nose(-0.32, 0.28, 0.03), on_nose(0.32, 0.28, 0.03), 0.007, 'gunmetal', seg=5)
    if cfg.get('bow_edge'):   # the light edge where the nose plate meets the transmission deck
        p.add(props.box(W - 2 * d['tw'] - 0.04, 0.018, 0.016), 'armor_edge', T(0, yN - 0.004, z_f), bevel=0.004)
    for sgn in (-1, 1):
        q = on_nose(sgn * 0.3, 0.8, 0.0)
        p.add(props.lathe([(0, 0.03), (0.04, 0.024)], seg=10), 'gunmetal', T(*q) @ _q(nn), smooth=True)
    ts = cfg.get('tac_sign', (0.13, 0.09))
    for sgn in (-1, 1):   # the civ tactical signs: front corners of the driver's plate
        mark(p, 'rhomb', ts, (sgn * cfg.get('tac_x', 0.4), yF + 0.002, z_f + H * 0.55), (0, 1, 0))
    # ---- the hull skirts: a rail on brackets, five plates a side, the front one angled in, a Balkenkreuz
    xs_ = W / 2 + cfg.get('hull_skirt_gap', 0.05)
    zt, zb = z_f + H * cfg.get('skirt_top', 1.0) - 0.012, d['zr'] + d['rw'] * cfg.get('skirt_low', 0.3)
    ys0, ys1 = cfg.get('skirt_y', (-0.8, 0.66))
    npl = cfg.get('skirt_n', 5)
    plen = (ys1 - ys0) / npl
    for sgn in (-1, 1):
        vkit.rod(p, (sgn * (xs_ - 0.012), ys0, zt + 0.005), (sgn * (xs_ - 0.012), ys1, zt + 0.005), 0.009, 'shade_dark', seg=6)
        for yy in ([ys0 + plen * (k + 0.5) for k in range(npl)] if cfg.get('hangers') else (ys0 + 0.05, (ys0 + ys1) / 2, ys1 - 0.06)):
            p.add(props.box(xs_ - W / 2, 0.03, 0.02), 'shade_dark', T(sgn * (W / 2 + xs_) / 2, yy, zt))
            if cfg.get('hangers'):   # the hanger strap down to the plate's hook
                p.add(props.box(0.012, 0.03, 0.05), 'shade_dark', T(sgn * (xs_ - 0.008), yy, zt + 0.02))
        lost = cfg.get('skirt_lost', {}).get(sgn)   # a plate lost on campaign: the side and the running gear show
        for k in range(npl):
            a, b = ys0 + plen * k + cfg.get('plate_gap', 0.007), ys0 + plen * (k + 1) - cfg.get('plate_gap', 0.007)
            if k == lost:
                for yy in (a + 0.04, b - 0.04):   # the empty hangers on the rail
                    p.add(props.box(0.012, 0.03, 0.05), 'shade_dark', T(sgn * xs_, yy, zt - 0.02))
                continue
            x1 = sgn * (xs_ - (0.05 if k == npl - 1 else 0.0))   # the front plate angled in toward the nose
            yaw = math.degrees(math.atan2(-(x1 - sgn * xs_), b - a))
            tilt = (2.5, -1.0, 3.0, 0.5, 1.5, 1.0, -0.5)[k] * sgn * cfg.get('skirt_tilt', 1.0)   # hung from the rail: each plate swings a little its own way
            p.add(props.box(0.012, math.hypot(x1 - sgn * xs_, b - a), zt - zb), cfg.get('skirt_key', 'armor_paint'),
                  T((sgn * xs_ + x1) / 2, (a + b) / 2, zt) @ R('Z', yaw) @ R('Y', tilt) @ T(0, 0, -(zt - zb) / 2), bevel=0.003)
            if cfg.get('skirt_edge', True):
                p.add(props.box(0.016, b - a, 0.012), 'armor_edge',
                      T(sgn * (xs_ - (0.025 if k == npl - 1 else 0)), (a + b) / 2, zt), bevel=0.003)
            if cfg.get('skirt_seams') and k != npl - 1:   # a dark seam across each plate, a bent rear-bottom corner on one
                zs_ = zt - (zt - zb) * 0.35
                p.add(props.box(0.006, b - a, 0.008), 'shade_dark',
                      T(sgn * (xs_ + 0.007 + math.sin(math.radians(sgn * tilt)) * (zt - zs_)), (a + b) / 2, zs_))
                if k == cfg.get('bent', -9):
                    p.add(props.box(0.012, 0.07, 0.07), 'armor_paint',
                          T(sgn * (xs_ + 0.02), a + 0.03, zb + 0.03) @ R('Z', sgn * 25) @ R('Y', sgn * 20), bevel=0.003)
        if cfg.get('shadow_gaps'):   # the dark gap between the hull side and the skirts, seen from above
            p.add(props.box(xs_ - W / 2 - 0.012, ys1 - ys0, 0.008), 'port', T(sgn * (W / 2 + xs_ - 0.012) / 2, (ys0 + ys1) / 2, zt - 0.03))
        if cfg.get('rail_line'):   # a thin dark line under the rail, along the plates' straight top edge
            p.add(props.box(0.008, ys1 - ys0 - 0.06, 0.012), 'port', T(sgn * (xs_ + 0.008), (ys0 + ys1) / 2 - 0.03, zt - 0.014))
        balkenkreuz(p, cfg.get('cross', 0.075), (sgn * (xs_ + 0.006), ys0 + plen * 2.5, (zt + zb) / 2 + 0.01), (sgn, 0, 0))
        if cfg.get('skirt_sign'):   # a civ tactical sign on the front skirt plate (angled in with it)
            yaw_n = Vector((sgn * plen, 0.05, 0.0)).normalized()
            mark(p, 'rhomb', cfg['skirt_sign'], (sgn * (xs_ - 0.025 + 0.012), ys0 + plen * (npl - 0.5), (zt + zb) / 2 + 0.01), yaw_n)
    # ---- the engine deck: two louvred hatches in dark frames; the rear: the muffler, a cross, the tactical signs
    gy_ = (ty - tl / 2 - 0.13 + yR) / 2
    for sgn in (-1, 1):
        p.add(props.box(W * 0.36, 0.17, 0.014), 'armor_edge', T(sgn * W * 0.2, gy_, deck + 0.005), bevel=0.004)
        grille(p, sgn * W * 0.2, gy_, W * 0.32, 0.14, deck + 0.01, n=5, key='armor_shade', frame_key='deck_dark')
    if cfg.get('deck_links') == 'rack':   # one clean rack of links per side, in a light frame
        for sgn in (-1, 1):
            x_ = sgn * (W / 2 - 0.06)
            p.add(props.box(0.1, 0.36, 0.012), 'armor_edge', T(x_, gy_, deck + 0.006), bevel=0.003)
            p.add(props.box(0.075, 0.33, 0.024), 'track', T(x_, gy_, deck + 0.014), bevel=0.004)
    elif cfg.get('deck_links'):   # spare track links racked along both deck edges beside the grilles
        for sgn in (-1, 1):
            for k in range(3):
                yy = gy_ - 0.11 + 0.11 * k
                p.add(props.box(0.075, 0.095, 0.022), 'track', T(sgn * (W / 2 - 0.06), yy, deck + 0.011), bevel=0.004)
                p.add(props.box(0.09, 0.022, 0.03), 'track_pin', T(sgn * (W / 2 - 0.06), yy - 0.045, deck + 0.014), bevel=0.003)
            vkit.rod(p, (sgn * (W / 2 - 0.06), gy_ - 0.18, deck + 0.03), (sgn * (W / 2 - 0.06), gy_ + 0.18, deck + 0.03), 0.007,
                     'gunmetal', seg=5)
    rr = cfg.get('upper', {}).get('rear', 0.0)
    rn = Vector((0, -H, Lu / 2 * rr)).normalized()
    rz = z_f + H * 0.5
    balkenkreuz(p, cfg.get('cross', 0.075) * 0.85, (0.24, yR + Lu / 2 * rr * 0.5 - 0.004, rz), rn)
    for sgn in (-1, 1):
        mark(p, 'rhomb', cfg.get('rear_sign', ts), (sgn * 0.42, yR + Lu / 2 * rr * 0.5 - 0.004, rz + 0.02), rn)
    mz, my = z_f - 0.06, hy - Lh * 0.96 / 2 - 0.05
    p.add(props.lathe([(-0.26, 0.065), (0.12, 0.065)], seg=18), 'shade_dark', T(-0.06, my, mz) @ R('Y', 90), smooth=True)
    for x in (-0.32, 0.06):   # its end caps
        p.add(props.lathe([(0, 0.07), (0.015, 0.07)], seg=18), 'gunmetal', T(x, my, mz) @ R('Y', 90), smooth=True)
    for x in (-0.22, 0.02):   # the tailpipes
        vkit.rod(p, (x, my - 0.04, mz + 0.03), (x, my - 0.09, mz + 0.06), 0.016, 'shade_dark', seg=8)


def _abrams(p, cfg, d, camo):
    """An M1A1 Abrams: the low wedge turret (cheeks angled hard back from
    a narrow front, flat sides) in a darker paint, the long bustle with a
    stowage basket, the gunner's sight doghouse, the commander's .50 and
    the loader's MG, smoke banks on the cheeks; the long 120 mm with its
    thermal sleeve, bore evacuator and muzzle sensor (its own object, out
    of the fit, no shadow); the shallow glacis with the driver's hatch;
    full-length skirts with heavier forward sections, a dark foot and a
    shadow line; the rear deck's grilles and the exhaust grille; civ
    inverted-V chevrons on the turret sides, the skirts, the glacis and
    the rear plate. Antennas on their own part, out of the fit."""
    deck, W, H, hy, Lh, z_f, L = d['deck'], d['W'], d['H'], d['hy'], d['Lh'], d['z_f'], d['L']
    tk = 'armor_turret' if cfg.get('turret_tone') else 'armor_paint'
    up = cfg.get('upper', {})
    fr_, rr_ = up.get('front', 0.0), up.get('rear', 0.0)
    yt, yb = hy + Lh / 2 * (1 - fr_), hy + Lh / 2       # the glacis' top and foot
    gn = d['gn']

    def glacis(x, t, off=0.0):   # t = 0 at the glacis' top edge, 1 at its foot
        return Vector((x, yt + (yb - yt) * t, deck - H * t)) + gn * off
    yR = hy - Lh / 2 * (1 - rr_)                      # the rear deck edge
    rn = Vector((0, -H, Lh / 2 * rr_)).normalized()
    # ---- the turret
    ty = hy + cfg.get('turret_dy', -0.12)
    tw_, tl, th_ = cfg.get('turret_w', 0.9), cfg.get('turret_l', 1.15), cfg.get('turret_h', 0.22)
    tz0 = deck + 0.012
    pf = cfg.get('plan_front', 0.5)
    tp_, tf_ = cfg.get('t_taper', 0.06), cfg.get('t_front', 0.14)
    p.add(plate(tw_, tl, th_, cell=0.05, taper=tp_, taper_low=0.02, plan_front=pf, front=tf_, rear=0.03), tk,
          T(0, ty, tz0 + th_ / 2), bevel=0.01)
    roof = tz0 + th_
    if cfg.get('turret_edges'):   # hard lit edges round the roof: the wedge's facets read
        hw = tw_ / 2 * (1 - tp_)
        yr_, yf_ = ty - tl / 2 * 0.97, ty + tl / 2 * (1 - tf_)
        pts = [(-hw, yr_), (-hw, ty), (-hw * (1 - pf), yf_), (hw * (1 - pf), yf_), (hw, ty), (hw, yr_)]
        for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
            vkit.rod(p, (x0, y0, roof + 0.002), (x1, y1, roof + 0.002), 0.008, cfg.get('edge_key', 'armor_edge'), seg=6)
    sr = cfg.get('turret_shadow', (0.9, 0.8))
    p.add(props.box(tw_ * sr[0], tl * sr[1], 0.02), 'shade_dark', T(0, ty - tl * 0.05, deck + 0.004))   # its shadow ring on the deck
    yf = ty + tl / 2                                   # the turret's front (the gun shield's plane)
    gz = tz0 + th_ * 0.45
    # the gun shield: a dark recess between the cheeks, the mantlet proud of it
    p.add(props.box(tw_ * (1 - pf) * 0.8, 0.02, th_ * 0.8), 'shade_dark', T(0, yf - 0.004, tz0 + th_ * 0.45))
    p.add(props.box(0.2, 0.06, th_ * 0.62), tk, T(0, yf + 0.012, gz), bevel=0.01)
    p.add(props.box(0.2, 0.016, 0.012), 'armor_edge', T(0, yf + 0.04, gz + th_ * 0.31 - 0.004), bevel=0.003)   # a lit top edge
    p.add(props.box(0.04, 0.012, 0.03), 'port', T(0.07, yf + 0.045, gz + 0.02))                 # the coax port
    # the gun: sleeve, joints, bore evacuator, muzzle reference sensor; a light top edge
    gp = vkit.part(p.root, 'gun', p.colors, p.weather)
    gl, gr = cfg.get('gun_len', 1.15), cfg.get('gun_r', 0.045)
    gm = T(0, yf + 0.04, gz) @ R('X', -90 + 1.0)
    gk = cfg.get('gun_key', 'gun_olive')
    gp.add(props.tube([(0, 0, 0), (0, 0, 0.1), (0, 0, 0.14), (0, 0, gl)], [gr * 1.6, gr * 1.6, gr * 1.08, gr * 0.95],
                      seg=16, caps=False), gk, gm, smooth=True)
    for f in (0.24, 0.62, 0.84):   # the thermal sleeve's band joints
        gp.add(props.lathe([(gl * f - 0.008, gr * 1.12), (gl * f + 0.008, gr * 1.12)], seg=16), 'shade_dark', gm, smooth=True)
    ev = cfg.get('evacuator', 0.42)
    gp.add(props.lathe([(gl * ev - 0.05, gr * 1.0), (gl * ev - 0.035, gr * 1.4), (gl * ev + 0.035, gr * 1.4),
                        (gl * ev + 0.05, gr * 1.0)], seg=16), cfg.get('evac_key', 'brake_steel'), gm, smooth=True)
    n = 16
    ring = lambda r, z: [(r * math.cos(2 * math.pi * k / n), r * math.sin(2 * math.pi * k / n), z) for k in range(n)]
    gp.add(props.tube([(0, 0, gl - 0.04), (0, 0, gl)], gr * 1.05, seg=16, caps=False), 'brake_steel', gm, smooth=True)
    gp.add(props.loft([ring(gr * 1.05, gl), ring(gr * 0.6, gl)], False, False), 'gun_edge', gm)   # the muzzle face
    gp.add(props.tube([(0, 0, gl - 0.03), (0, 0, gl - 0.002)], gr * 0.6, seg=12, caps=True), 'port', gm)
    gp.add(props.box(0.04, 0.03, 0.05), 'brake_steel', gm @ T(0, -gr * 1.3, gl - 0.04), bevel=0.004)   # the MRS on top
    gp.add(props.lathe([(0.15, gr * 0.17), (gl - 0.06, gr * 0.15)], seg=6), 'gun_edge', gm @ T(0, -gr * 0.95, 0),
           smooth=True)
    gobj = gp.finish()
    gobj['no_fit'] = True
    gobj.visible_shadow = False
    # the roof: the gunner's sight doghouse, the commander's cupola and .50, the loader's hatch and MG
    p.add(props.box(0.17, 0.16, 0.1), tk, T(0.21, ty + tl * 0.3, roof + 0.045), bevel=0.012)
    p.add(props.box(0.12, 0.012, 0.05), 'port', T(0.21, ty + tl * 0.3 + 0.082, roof + 0.055))
    cx, cyy = 0.2, ty - tl * 0.12
    p.add(props.lathe([(0, 0.105), (0.05, 0.1), (0.06, 0.085)], seg=24), tk, T(cx, cyy, roof - 0.005), smooth=True)
    for k in range(6):
        a = math.radians(60 * k + 30)
        p.add(props.box(0.035, 0.008, 0.016), 'port',
              T(cx + 0.102 * math.cos(a), cyy + 0.102 * math.sin(a), roof + 0.03) @ R('Z', math.degrees(a) + 90))
    mg(p, (cx, cyy + 0.06, roof + 0.13), yaw=0, length=0.34, pitch=4)
    rim_hatch(p, (-0.2, ty - tl * 0.1, roof), 0.085, rim_key=cfg.get('roof_rim', 'armor_camo2'), key=tk)
    if cfg.get('cheek_lines'):   # a dark line under each cheek's top edge and down its front corner: the wedge's facets
        hw = tw_ / 2 * (1 - tp_)
        yf_ = ty + tl / 2 * (1 - tf_)
        for sgn in (-1, 1):
            a_, b_ = Vector((sgn * hw * (1 - pf), yf_, roof - 0.018)), Vector((sgn * hw, ty, roof - 0.018))
            out = Vector((sgn * (b_.y - a_.y), -(abs(b_.x - a_.x)), 0)).normalized() * 0.006
            vkit.rod(p, a_ + out, b_ + out, 0.007, 'shade_dark', seg=6)
            vkit.rod(p, Vector((sgn * hw * (1 - pf), yf_ + 0.01, roof - 0.01)), Vector((sgn * hw * (1 - pf) * 1.02, yf_ + 0.035, tz0 + 0.02)),
                     0.007, 'shade_dark', seg=6)
    mg(p, (-0.31, ty - tl * 0.02, roof + 0.1), yaw=-8, length=0.26, pitch=4)
    for sgn in (-1, 1):   # the smoke grenade banks on the cheeks
        p.add(props.box(0.12, 0.06, 0.05), 'shade_dark', T(sgn * tw_ * 0.34, ty + tl * 0.26, roof - 0.015) @ R('Z', -sgn * 25),
              bevel=0.006)
    # the bustle rack: a dark rail frame holding bags and boxes in mid canvas tones
    yb0 = ty - tl / 2 + 0.01
    rl = cfg.get('rack_l', 0.2)
    rz0, rz1 = roof - th_ * cfg.get('rack_drop', 0.75), roof - 0.01
    for sgn in (-1, 1):
        for zz in (rz0, rz1):
            vkit.rod(p, (sgn * tw_ * 0.47, yb0, zz), (sgn * tw_ * 0.47, yb0 - rl, zz), 0.01, 'shade_dark', seg=6)
        vkit.rod(p, (sgn * tw_ * 0.47, yb0 - rl, rz0), (sgn * tw_ * 0.47, yb0 - rl, rz1), 0.01, 'shade_dark', seg=6)
    for zz in (rz0, rz1):
        vkit.rod(p, (-tw_ * 0.47, yb0 - rl, zz), (tw_ * 0.47, yb0 - rl, zz), 0.01, 'shade_dark', seg=6)
    p.add(props.box(tw_ * 0.94, rl, 0.012), 'shade_dark', T(0, yb0 - rl / 2, rz0))
    bx = cfg.get('rack_x', (-0.29, 0.0, 0.28))
    if cfg.get('rack_posts'):   # an open rail frame: posts along the rear and the sides
        for k in range(1, 6):
            x_ = -tw_ * 0.47 + tw_ * 0.94 * k / 6
            vkit.rod(p, (x_, yb0 - rl, rz0), (x_, yb0 - rl, rz1), 0.007, 'shade_dark', seg=5)
        for sgn in (-1, 1):
            vkit.rod(p, (sgn * tw_ * 0.47, yb0 - rl / 2, rz0), (sgn * tw_ * 0.47, yb0 - rl / 2, rz1), 0.007, 'shade_dark', seg=5)
    for x, w_, h_, key in ((bx[0], cfg.get('pack_w', 0.26), 0.15, 'canvas_dark'), (bx[1], cfg.get('pack_w', 0.26), 0.12, 'canvas'),
                           (bx[2], cfg.get('pack_w', 0.24), 0.14, 'canvas_dark')):
        stowage(p, (x, yb0 - rl / 2, rz0 + 0.006), (w_, rl * cfg.get('pack_d', 0.85), h_), key=key, straps=1)
    if cfg.get('chev_rack'):   # a civ chevron on the middle bag's rear (the view from behind)
        pd_ = rl * cfg.get('pack_d', 0.85) / 2
        mark(p, cfg.get('chev_kind', 'chevron'), cfg['chev_rack'], (bx[1], yb0 - rl / 2 - pd_ - 0.002, rz0 + 0.065), (0, -1, 0))
        for x in ((bx[0], bx[2]) if cfg.get('chev_rack_sides') else ()):
            mark(p, cfg.get('chev_kind', 'chevron'), cfg['chev_rack_sides'], (x, yb0 - rl / 2 - pd_ - 0.002, rz0 + 0.07),
                 (0, -1, 0))
    # antennas at the turret's rear corners: their own part, out of the fit, no shadow
    if cfg.get('antennas'):
        ap = vkit.part(p.root, 'antennas', p.colors, p.weather)
        for sgn in (-1, 1):
            antenna(ap, sgn * tw_ * 0.38, ty - tl * 0.36, roof, cfg['antennas'])
        aobj = ap.finish()
        aobj['no_fit'] = True
        aobj.visible_shadow = False
    # civ chevrons on the turret sides
    cs = cfg.get('chev_turret', 0.1)
    for sgn in (-1, 1):
        nrm = Vector((sgn, 0.0, tp_ * tw_ / 2 / th_)).normalized()
        mark(p, cfg.get('chev_kind', 'chevron'), cs, (sgn * tw_ / 2 * (1 - tp_ * 0.5) + sgn * 0.002, ty - tl * 0.12, tz0 + th_ * 0.5), nrm)
    # ---- the hull: the driver's hatch and periscopes, headlights, glacis chevrons
    rim_hatch(p, (0, yt - 0.07, deck), cfg.get('drv_hatch', 0.07), rim_key=cfg.get('drv_rim', 'armor_camo2'), key='armor_paint')
    for k in range(3):
        periscope(p, (-0.07 + 0.07 * k, yt + 0.0, deck), 0)
    for sgn in (-1, 1):
        q = glacis(sgn * W * 0.4, 0.55, 0.0)
        light(p, q.x, q.y, q.z + 0.025, r=0.024, guard=True)
        mark(p, cfg.get('chev_kind', 'chevron'), cfg.get('chev_glacis', 0.09), glacis(sgn * 0.24, 0.45, 0.002), gn)
    if cfg.get('deck_lines'):   # panel-line shadows on the deck and glacis, a tow cable, the front fender lips
        for yy in (yt - 0.18, (ty - tl / 2 + yR) / 2 + 0.12):
            p.add(props.box(W * 0.9, 0.01, 0.006), 'shade_dark', T(0, yy, deck + 0.002))
        for sgn in (-1, 1):
            a_, b_ = glacis(sgn * 0.42, 0.1, 0.004), glacis(sgn * 0.42, 0.9, 0.004)
            vkit.rod(p, a_, b_, 0.006, 'shade_dark', seg=5)
            c0, c1 = Vector((sgn * (W / 2 - 0.05), yt - 0.05, deck + 0.012)), Vector((sgn * (W / 2 - 0.05), yR + 0.1, deck + 0.012))
            vkit.rod(p, c0, c1, 0.011, 'gunmetal', seg=6)   # the tow cable along the deck edge
            p.add(props.box(d['tw'] + 0.02, 0.02, 0.06), 'shade_dark', T(sgn * d['xs'], L / 2 - 0.005, z_f - 0.03))
    if cfg.get('hull_edges'):   # lit edges where the glacis meets the deck and along the deck's sides
        p.add(props.box(W * 0.96, 0.016, 0.012), 'armor_edge', T(0, yt, deck), bevel=0.004)
        p.add(props.box(W * 0.98, 0.018, 0.014), 'armor_edge', T(0, yb, z_f), bevel=0.004)   # the bow's lit edge
        if cfg.get('bow_dark'):   # a darker underside band on the nose
            p.add(props.box(W - 2 * d['tw'] - 0.02, 0.012, (z_f - d['belly']) * 0.6), 'shade_dark',
                  T(0, yb + 0.004, d['belly'] + (z_f - d['belly']) * 0.3))
        for sgn in (-1, 1):
            p.add(props.box(0.016, yt - yR, 0.012), 'armor_edge', T(sgn * (W / 2 - 0.004), (yt + yR) / 2, deck), bevel=0.004)
    # the rear deck: the big grille doors; the rear plate: exhaust grille, tail lights, a chevron
    gyy = (ty - tl / 2 - rl + yR) / 2
    p.add(props.box(W * 0.82, max(0.1, ty - tl / 2 - rl - yR - 0.02), 0.012), 'deck_dark', T(0, gyy, deck + 0.004))
    grille(p, 0, gyy, W * 0.76, max(0.08, ty - tl / 2 - rl - yR - 0.06), deck + 0.008, n=6, key='armor_shade', frame_key='deck_dark')
    rq = Vector((0, yR - 0.004, z_f + H * 0.5))
    p.add(props.box(W * 0.55, 0.012, H * 0.7), 'deck_dark', T(rq.x - 0.12, rq.y - 0.002, rq.z))
    for k in range(5):
        p.add(props.box(W * 0.53, 0.01, 0.008), 'armor_shade', T(rq.x - 0.12, rq.y - 0.009, rq.z - H * 0.28 + H * 0.14 * k))
    for sgn in (-1, 1):
        p.add(props.box(0.04, 0.012, 0.03), 'mark_red', T(sgn * W * 0.44, rq.y - 0.006, rq.z + H * 0.2))
    mark(p, cfg.get('chev_kind', 'chevron'), cfg.get('chev_rear', 0.07), (W * 0.33, rq.y - 0.006, rq.z), (0, -1, 0))
    # ---- the skirts: a full-length row, heavier forward sections, a dark foot, a shadow line at the top
    zt = z_f + H * cfg.get('skirt_top', 0.4)
    zb = d['zr'] + d['rw'] * cfg.get('skirt_low', 0.5)
    sy0, sy1 = cfg.get('skirt_y', (-L / 2 + 0.1, L / 2 - 0.02))
    xs_ = W / 2 + 0.016
    sk = cfg.get('skirt_key', tk)
    heavy = cfg.get('skirt_heavy', 0.55)   # the length of the heavier forward section
    npan = cfg.get('skirt_panels', 0)
    for sgn in (-1, 1):
        if npan:   # separate panels with dark gaps, a lit bottom lip each; the heavy forward pair thicker and proud
            gap = cfg.get('panel_gap', 0.014)
            pl = (sy1 - heavy - sy0) / npan
            if cfg.get('skirt_back'):   # a dark backing behind the panels: every gap reads as a dark seam
                p.add(props.box(0.006, sy1 - sy0, zt - zb), 'port', T(sgn * (xs_ - 0.016), (sy0 + sy1) / 2, (zt + zb) / 2))
            for k in range(npan):
                a, b = sy0 + pl * k + gap / 2, sy0 + pl * (k + 1) - gap / 2
                p.add(props.box(0.024, b - a, zt - zb), sk, T(sgn * xs_, (a + b) / 2, (zt + zb) / 2), bevel=0.004)
                p.add(props.box(0.03, b - a, 0.012), cfg.get('lip_key', 'armor_edge'), T(sgn * (xs_ + 0.004), (a + b) / 2, zb + 0.004),
                      bevel=0.003)
                if cfg.get('skirt_dirt'):   # the lower third dirtier and darker
                    p.add(props.box(0.004, b - a - 0.004, (zt - zb) * 0.34), 'skirt_dirt',
                          T(sgn * (xs_ + 0.0135), (a + b) / 2, zb + 0.01 + (zt - zb) * 0.17))
            for k in range(2):
                a, b = sy1 - heavy + heavy / 2 * k + gap / 2, sy1 - heavy + heavy / 2 * (k + 1) - gap / 2
                p.add(plate(0.05, b - a, zt - zb + 0.01, cell=0.06, front_low=0.3 if k else 0.0), sk,
                      T(sgn * (xs_ + 0.013), (a + b) / 2, (zt + zb) / 2 + 0.005), bevel=0.006)
                p.add(props.box(0.056, (b - a) * (0.85 if k else 1.0), 0.012), 'armor_edge',
                      T(sgn * (xs_ + 0.016), (a + b) / 2 - (0.015 if k else 0.0), zb + 0.006), bevel=0.003)
        else:
            p.add(plate(0.024, sy1 - heavy - sy0, zt - zb, cell=0.06), sk, T(sgn * xs_, (sy0 + sy1 - heavy) / 2, (zt + zb) / 2),
                  bevel=0.004)
            p.add(plate(0.05, heavy, zt - zb + 0.01, cell=0.06, front_low=0.25), sk,
                  T(sgn * (xs_ + 0.013), sy1 - heavy / 2, (zt + zb) / 2 + 0.005), bevel=0.006)
            for k in range(1, 5):   # the panel seams
                yy = sy0 + (sy1 - heavy - sy0) * k / 5
                p.add(props.box(0.006, 0.008, (zt - zb) * 0.92), 'shade_dark', T(sgn * (xs_ + 0.013), yy, (zt + zb) / 2))
            p.add(props.box(0.006, 0.01, zt - zb), 'shade_dark', T(sgn * (xs_ + 0.04), sy1 - heavy, (zt + zb) / 2))
            p.add(props.box(0.03, sy1 - sy0, 0.022), 'rubber', T(sgn * (xs_ + 0.004), (sy0 + sy1) / 2, zb - 0.006))   # the dark foot
        p.add(props.box(0.02, sy1 - sy0, 0.016), 'port', T(sgn * (W / 2 + 0.002), (sy0 + sy1) / 2, zt + 0.006))   # shadow line
        mark(p, cfg.get('chev_kind', 'chevron'), cfg.get('chev_skirt', 0.12), (sgn * (xs_ + 0.014), hy - 0.15, (zt + zb) / 2), (sgn, 0, 0))
        if cfg.get('chev_front'):   # a small chevron on the forward section's front end (head-on colour)
            mark(p, cfg.get('chev_kind', 'chevron'), cfg['chev_front'], (sgn * (xs_ + 0.03), sy1 + 0.006, (zt + zb) / 2 + 0.01), (0, 1, 0))


def _bradley(p, cfg, d, camo):
    """An M2A2 Bradley (ODS): the tall slab hull with bolted spaced-armour
    panels on its sides and armoured skirts below them (a second, darker
    paint), the steep glacis with the trim vane, the driver's hatch and
    the engine grille; the small angular two-man turret set right of the
    centre line with the 25 mm Bushmaster (its own object, out of the
    fit, no shadow), the twin TOW box on its left, hatches, the sight and
    smoke banks; the cargo hatch and the big rear ramp; civ inverted-V
    chevrons on the side armour, the turret, the ramp and the nose.
    Antennas on their own part, out of the fit."""
    deck, W, H, hy, Lh, z_f, L = d['deck'], d['W'], d['H'], d['hy'], d['Lh'], d['z_f'], d['L']
    up = cfg.get('upper', {})
    fr_, rr_ = up.get('front', 0.0), up.get('rear', 0.0)
    yt, yb = hy + Lh / 2 * (1 - fr_), hy + Lh / 2
    gn = d['gn']
    vk = cfg.get('chev_kind', 'vee')
    sk = 'armor_turret' if cfg.get('turret_tone') else 'armor_paint'   # the second, darker paint: armour panels, skirts

    def glacis(x, t, off=0.0):
        return Vector((x, yt + (yb - yt) * t, deck - H * t)) + gn * off
    yR = hy - Lh / 2 * (1 - rr_)
    # ---- the turret: small, angular, right of centre
    tx, ty = cfg.get('turret_x', 0.08), hy + cfg.get('turret_dy', 0.04)
    tw_, tl, th_ = cfg.get('turret_w', 0.56), cfg.get('turret_l', 0.6), cfg.get('turret_h', 0.17)
    tz0 = deck + 0.012
    tp_, tf_, tr_, pf_ = cfg.get('t_taper', 0.2), cfg.get('t_front', 0.3), cfg.get('t_rear', 0.12), cfg.get('t_plan', 0.32)
    p.add(plate(tw_, tl, th_, cell=0.04, taper=tp_, plan_front=pf_, front=tf_, rear=tr_), cfg.get('turret_key', 'armor_paint'),
          T(tx, ty, tz0 + th_ / 2), bevel=0.008)
    roof = tz0 + th_
    sr_ = cfg.get('turret_ring', 1.02)
    p.add(props.box(tw_ * sr_, tl * (sr_ - 0.07), 0.016), 'shade_dark', T(tx, ty, deck + 0.004))   # its shadow on the deck
    hw = tw_ / 2 * (1 - tp_)
    yfr, yrr = ty + tl / 2 * (1 - tf_), ty - tl / 2 * (1 - tr_)
    hf = hw * (1 - pf_ * (1 - tf_))
    for (x0, y0), (x1, y1) in (((-hw, yrr), (-hw, ty)), ((-hw, ty), (-hf, yfr)), ((-hf, yfr), (hf, yfr)),
                               ((hf, yfr), (hw, ty)), ((hw, ty), (hw, yrr))):
        vkit.rod(p, (tx + x0, y0, roof + 0.002), (tx + x1, y1, roof + 0.002), cfg.get('roof_edge_r', 0.006), cfg.get('roof_edge_key', 'muzzle_light'), seg=6)   # lit roof edges
    # the gun: a mantlet block, the barrel shroud, the thin barrel, the flash suppressor
    fy = ty + tl / 2 * (1 - tf_ * 0.5)
    gz = tz0 + th_ * 0.45
    mw_ = cfg.get('mantlet_w', 0.12)
    p.add(props.box(mw_, 0.1, 0.09), sk, T(tx, fy + 0.02, gz), bevel=0.01)   # the gun mantlet, the darker paint
    p.add(props.box(mw_, 0.012, 0.01), 'armor_edge', T(tx, fy + 0.07, gz + 0.042))
    gp = vkit.part(p.root, 'gun', p.colors, p.weather)
    gm = T(tx, fy + 0.07, gz) @ R('X', -90 + 1.5)
    gl, gr = cfg.get('gun_len', 0.62), cfg.get('gun_r', 0.022)
    gp.add(props.tube([(0, 0, 0), (0, 0, 0.2)], gr * 1.9, seg=12, caps=False), 'gun_olive', gm, smooth=True)   # the shroud
    gp.add(props.tube([(0, 0, 0.2), (0, 0, gl)], [gr * 1.15, gr], seg=12, caps=False), 'gun_olive', gm, smooth=True)
    gp.add(props.tube([(0, 0, gl - 0.005), (0, 0, gl + 0.06)], gr * 1.5, seg=12, caps=False), 'brake_steel', gm, smooth=True)
    n = 12
    ring = lambda r, z: [(r * math.cos(2 * math.pi * k / n), r * math.sin(2 * math.pi * k / n), z) for k in range(n)]
    gp.add(props.loft([ring(gr * 1.5, gl + 0.06), ring(gr * 0.7, gl + 0.06)], False, False), 'gun_edge', gm)
    gp.add(props.lathe([(0.02, gr * 0.3), (gl - 0.02, gr * 0.25)], seg=6), 'gun_edge', gm @ T(0, -gr * 1.0, 0), smooth=True)
    gobj = gp.finish()
    gobj['no_fit'] = True
    gobj.visible_shadow = False
    vkit.rod(p, (tx + 0.08, fy + 0.04, gz + 0.01), (tx + 0.08, fy + 0.14, gz + 0.01), 0.009, 'gunmetal', seg=6)   # the coax
    # the TOW box folded on the turret's left side, two dark tube ends
    bxw, bxl, bxh = cfg.get('tow_w', 0.13), cfg.get('tow_l', 0.44), cfg.get('tow_h', 0.13)
    bx = tx - tw_ / 2 - bxw / 2 + 0.01
    roof_t = roof + cfg.get('tow_up', 0.0)   # the box's top (raised so it shows in profile over the turret)
    p.add(props.box(bxw, bxl, bxh), cfg.get('tow_key', 'armor_paint'), T(bx, ty - 0.02, roof_t - bxh / 2 - 0.01), bevel=0.012)
    p.add(props.box(bxw * 1.02, 0.012, bxh * 1.02), 'shade_dark', T(bx, ty - 0.02 - bxl * 0.25, roof_t - bxh / 2 - 0.01))
    for z in (roof_t - 0.01 - bxh * 0.27, roof_t - 0.01 - bxh * 0.73):
        if cfg.get('tow_rims'):
            rr_ = cfg.get('tow_rim_r', 0.044)
            p.add(props.lathe([(0, rr_), (0.012, rr_)], seg=16), cfg.get('tow_rim_key', 'gun_edge'), T(bx, ty - 0.02 + bxl / 2 + 0.004, z) @ R('X', -90),
                  smooth=True)
        p.add(props.lathe([(0, 0.036), (0.016, 0.036)], seg=14), 'port', T(bx, ty - 0.02 + bxl / 2 + 0.006, z) @ R('X', -90),
              smooth=True)
    if not cfg.get('tow_rims'):
        p.add(props.box(bxw * 0.9, bxl * 0.9, 0.01), 'armor_edge', T(bx, ty - 0.02, roof - 0.008))   # its lit top
    # roof: the commander's and gunner's hatches, the sight head, smoke banks
    if cfg.get('flat_hatches'):   # flat square hatches in dark outlines; the commander's ring of vision blocks
        for hx, hy_, hs in ((tx + 0.1, ty - 0.08, 0.12), (tx - 0.1, ty - 0.1, 0.1)):
            p.add(props.box(hs + 0.02, hs + 0.02, 0.006), 'shade_dark', T(hx, hy_, roof + 0.002))
            p.add(props.box(hs, hs, 0.012), 'armor_paint', T(hx, hy_, roof + 0.006), bevel=0.003)
            p.add(props.box(hs * 0.15, hs * 0.6, 0.012), 'gunmetal', T(hx + hs * 0.25, hy_, roof + 0.014))
        for k in range(7):
            a_ = math.radians(-60 + 40 * k)
            p.add(props.box(0.03, 0.02, 0.025), 'shade_dark',
                  T(tx + 0.1 + 0.095 * math.sin(a_), ty - 0.08 + 0.095 * math.cos(a_), roof + 0.012) @ R('Z', -math.degrees(a_)))
    else:
        rim_hatch(p, (tx + 0.13, ty - 0.1, roof), 0.075, rim_key='shade_dark', key='armor_paint')
        rim_hatch(p, (tx - 0.1, ty - 0.12, roof), 0.065, rim_key='shade_dark', key='armor_paint')
    p.add(props.box(0.1, 0.09, 0.08), 'armor_paint', T(tx - 0.09, ty + 0.12, roof + 0.035), bevel=0.01)
    p.add(props.box(0.08, 0.008, 0.04), 'port', T(tx - 0.09, ty + 0.166, roof + 0.045))
    for sgn in (-1, 1):
        p.add(props.box(0.1, 0.05, 0.045), 'shade_dark', T(tx + sgn * tw_ * 0.33, ty + tl * 0.3, roof - 0.03) @ R('Z', -sgn * 25),
              bevel=0.005)
    for sgn in (-1, 1):
        mark(p, vk, cfg.get('chev_turret', 0.08), (tx + sgn * (tw_ / 2 * (1 - tp_ * 0.5) + 0.003), ty - 0.12, tz0 + th_ * 0.5),
             Vector((sgn, 0.0, tp_ * tw_ / 2 / th_)).normalized())   # on the sloped wall itself
    if cfg.get('antennas'):
        ap = vkit.part(p.root, 'antennas', p.colors, p.weather)
        for sgn in (-1, 1):
            antenna(ap, sgn * W * 0.38, yR + 0.12, deck, cfg['antennas'], r=cfg.get('antenna_r', 0.0045))
        aobj = ap.finish()
        aobj['no_fit'] = True
        aobj.visible_shadow = False
    # ---- the hull front: the trim vane on the nose, the driver's hatch, the engine grille, lights, a chevron
    p.add(props.box(W * 0.72, 0.022, (z_f - d['belly']) * 0.8), 'armor_paint',
          T(0, yb + 0.03, d['belly'] + (z_f - d['belly']) * 0.5) @ R('X', -28), bevel=0.006)   # the trim vane
    for k in range(5):
        p.add(props.box(0.008, 0.026, (z_f - d['belly']) * 0.7), 'shade_dark',
              T(-W * 0.28 + W * 0.14 * k, yb + 0.042, d['belly'] + (z_f - d['belly']) * 0.5) @ R('X', -28))
    p.add(props.box(W * 0.98, 0.018, 0.014), 'armor_edge', T(0, yt, deck), bevel=0.004)
    rim_hatch(p, (-W * 0.28, yt - 0.08, deck), 0.07, rim_key='shade_dark', key='armor_paint')
    for k in range(3):
        periscope(p, (-W * 0.28 - 0.07 + 0.07 * k, yt - 0.005, deck), 0)
    grille(p, W * 0.26, yt - 0.12, W * 0.3, 0.14, deck, n=5, key='armor_shade', frame_key='deck_dark')
    for sgn in (-1, 1):
        q = glacis(sgn * W * 0.42, 0.75, 0.0)
        light(p, q.x, q.y, q.z + 0.02, r=0.022, guard=True)
    for x_ in (() if not cfg.get('chev_glacis') else (-W * 0.22, W * 0.22) if cfg.get('chev_glacis2') else (-W * 0.24,)):
        mark(p, vk, cfg['chev_glacis'], glacis(x_, 0.55 if x_ < 0 else 0.75, 0.002), gn)
    # ---- the deck: the cargo hatch, panel lines; the rear: the ramp with its door, lights, a chevron
    ch_y = (ty - tl / 2 + yR) / 2
    p.add(props.box(W * 0.46, 0.26, 0.012), 'armor_paint', T(0, ch_y, deck + 0.006), bevel=0.003)
    for sgn in (-1, 1):
        p.add(props.box(0.012, 0.26, 0.012), 'shade_dark', T(sgn * W * 0.23, ch_y, deck + 0.008))
    p.add(props.box(W * 0.46, 0.012, 0.012), 'shade_dark', T(0, ch_y - 0.13, deck + 0.008))
    if cfg.get('deck_kit'):   # stowage on the rear deck's corners
        stowage(p, (W * 0.36, ch_y - 0.02, deck), (0.16, 0.26, 0.08), key='canvas_dark', straps=2)
        stowage(p, (-W * 0.36, ch_y + 0.02, deck), (0.14, 0.18, 0.07), key='canvas', straps=1)
    rz = z_f + H * 0.5
    rq = Vector((0, yR - 0.006, rz))
    if cfg.get('ramp_detail'):   # a dark frame round the ramp and its hinge line along the foot
        p.add(props.box(W * 0.7, 0.01, H * 0.9), 'shade_dark', T(rq.x, rq.y + 0.004, rq.z))
        vkit.rod(p, (-W * 0.32, rq.y - 0.012, rq.z - H * 0.43), (W * 0.32, rq.y - 0.012, rq.z - H * 0.43), 0.012, 'gunmetal', seg=8)
    p.add(props.box(W * 0.66, 0.014, H * 0.86), 'armor_paint', T(rq.x, rq.y, rq.z), bevel=0.004)   # the ramp
    for sgn in (-1, 1):
        p.add(props.box(0.012, 0.016, H * 0.86), 'shade_dark', T(sgn * W * 0.33, rq.y - 0.002, rq.z))
    p.add(props.box(0.14, 0.018, H * 0.5), 'shade_dark', T(-W * 0.14, rq.y - 0.004, rq.z - H * 0.1))   # its door's outline
    p.add(props.box(0.12, 0.02, H * 0.44), 'armor_paint', T(-W * 0.14, rq.y - 0.008, rq.z - H * 0.1))
    for sgn in (-1, 1):
        p.add(props.box(0.04, 0.012, 0.03), 'mark_red', T(sgn * W * 0.44, rq.y - 0.004, rq.z + H * 0.3))
    mark(p, vk, cfg.get('chev_rear', 0.1), (W * 0.15, rq.y - 0.01, rq.z), (0, -1, 0))
    # ---- the sides: bolted armour panels on the hull, armoured skirts below them (the darker paint)
    zt = z_f + H * 0.02
    zb = d['zr'] + d['rw'] * cfg.get('skirt_low', 0.85)
    sy0, sy1 = cfg.get('skirt_y', (-L / 2 + 0.12, L / 2 - 0.12))
    np_ = cfg.get('panels', 5)
    pl = (sy1 - sy0) / np_
    gap = 0.018
    for sgn in (-1, 1):
        xa = sgn * (W / 2 + 0.014)
        za, zc = z_f + 0.02, deck - 0.02

        def ztop(y):   # the hull's top line: the deck, then down the glacis toward the nose
            return zc if y <= yt else max(za + 0.03, deck - H * (y - yt) / (yb - yt) - 0.02)
        for k in range(np_):   # the bolted side armour over the hull side, its top following the hull's line
            a, b = sy0 + pl * k + gap / 2, sy0 + pl * (k + 1) - gap / 2
            ys = [a] + ([yt] if a < yt < b else []) + [b]
            outline = [(a, za), (b, za)] + [(y, ztop(y)) for y in reversed(ys)]
            p.add(props.slab(outline, 0.022, 'X'), sk, T(xa, 0, 0))
            p.add(props.box(0.006, (b - a) * 0.86, 0.008), 'shade_dark', T(xa + sgn * 0.012, (a + b) / 2, za + 0.016))
            if cfg.get('panel_detail'):   # a horizontal seam, raised lit vertical edges
                zs_ = za + (min(ztop(a), ztop(b)) - za) * 0.48
                p.add(props.box(0.006, (b - a) * 0.96, 0.01), 'shade_dark', T(xa + sgn * 0.012, (a + b) / 2, zs_))
                for y_ in (a + 0.006, b - 0.006):
                    vkit.rod(p, (xa + sgn * 0.011, y_, za + 0.01), (xa + sgn * 0.011, y_, ztop(y_) - 0.01), 0.005, 'armor_edge', seg=5)
            for y0_, y1_ in zip(ys, ys[1:]):   # the lit top edge and its bolt row, along the same line
                q0, q1 = Vector((xa, y0_, ztop(y0_) + 0.002)), Vector((xa, y1_, ztop(y1_) + 0.002))
                vkit.rod(p, q0, q1, 0.007, 'armor_edge', seg=6)
                vkit.rod(p, q0 + Vector((sgn * 0.012, 0, -0.018)), q1 + Vector((sgn * 0.012, 0, -0.018)), 0.004, 'shade_dark', seg=4)
        if cfg.get('upper_slope'):   # the sloped upper plate from the deck's edge out to the armour: it takes the top light
            x_in, x_out = sgn * (W / 2 - 0.03), xa + sgn * 0.012
            ln_ = min(yt, sy1) - sy0
            poly = [(x_in, deck + 0.003), (x_out, zc - 0.03), (x_out, zc - 0.046), (x_in, deck - 0.013)]
            if sgn < 0:
                poly = poly[::-1]
            p.add(props.slab(poly, ln_, 'Y'), 'armor_paint', T(0, sy0 + ln_ / 2, 0))
            p.add(props.box(0.012, ln_, 0.012), 'shade_dark', T(x_out, sy0 + ln_ / 2, zc - 0.05))   # its dark lower lip
        back = [(sy0, za), (sy1, za)] + [(y, ztop(y) - 0.004) for y in (sy1, max(sy0, min(sy1, yt)), sy0)]
        p.add(props.slab(back, 0.006, 'X'), 'port', T(sgn * (W / 2 + 0.002), 0, 0))   # the dark backing in the gaps
        xs_ = sgn * (W / 2 + 0.02)
        for k in range(np_ + 1):   # the skirts: panels under the armour
            pl2 = (sy1 - sy0) / (np_ + 1)
            a, b = sy0 + pl2 * k + gap / 2, sy0 + pl2 * (k + 1) - gap / 2
            p.add(props.box(0.022, b - a, zt - zb), cfg.get('skirt_key', sk), T(xs_, (a + b) / 2, (zt + zb) / 2), bevel=0.004)
            df = cfg.get('dirt_f', 0.4)
            p.add(props.box(0.004, b - a - 0.004, (zt - zb) * df), 'skirt_dirt',
                  T(xs_ + sgn * 0.0125, (a + b) / 2, zb + (zt - zb) * df / 2))
        p.add(props.box(0.006, sy1 - sy0, zt - zb), 'port', T(sgn * (W / 2 + 0.004), (sy0 + sy1) / 2, (zt + zb) / 2))
        p.add(props.box(0.012, sy1 - sy0, 0.014), 'port', T(xs_ + sgn * 0.004, (sy0 + sy1) / 2, zt + 0.004))   # the shadow line
        mark(p, vk, cfg.get('chev_side', 0.12), (xa + sgn * 0.0125, hy - 0.05, (z_f + deck) / 2), (sgn, 0, 0))
        if cfg.get('chev_tow') and sgn < 0:   # on the TOW box's outer face
            mark(p, vk, cfg['chev_tow'], (bx - bxw / 2 - 0.002, ty - 0.02, roof - bxh / 2 - 0.01), (-1, 0, 0))


def _sam(p, cfg, d, camo):
    """An M48 Chaparral: the M730 carrier's low hull with the cab built into
    its front (a sloped faceted front, a framed windscreen, dark side
    windows, a roof lip with a hatch ring, mirrors), the engine grille and
    exhaust behind it, fender lips, side bins, the rear door and tow hooks;
    the M54 launch station on the rear deck: a recessed bearing ring, a
    pedestal, the turret block with the gunner's glazed canopy, trunnions
    out to two separate arms with two long slim MIM-72 missiles each (light
    grey, dark seeker noses, rail shoes, canards and tail fins with dark
    tips, a civ band); civ inverted-V chevrons on the cab sides and front,
    the turret block's sides and the rear."""
    deck, W, H, hy, Lh, z_f, L = d['deck'], d['W'], d['H'], d['hy'], d['Lh'], d['z_f'], d['L']
    vk = cfg.get('chev_kind', 'vee')
    sk = 'armor_turret' if cfg.get('turret_tone') else 'armor_paint'
    yF, yR = hy + Lh / 2, hy - Lh / 2
    # ---- the cab: built into the hull's front, a sloped faceted front, a roof lip
    cl, ch = cfg.get('cab_l', 0.44), cfg.get('cab_h', 0.18)
    cf = cfg.get('cab_front', 0.4)
    cy = yF - cl / 2 - 0.01
    p.add(plate(W, cl, ch, cell=0.05, front=cf, taper=0.06), 'armor_paint', T(0, cy, deck + ch / 2), bevel=0.012)
    croof = deck + ch
    wn = Vector((0, ch, cl / 2 * cf)).normalized()
    yw = cy + cl / 2 * (1 - cf * 0.5)
    zw = deck + ch * 0.5
    for x in (-W * 0.22, W * 0.22):   # the windscreen: two panes in dark frames on the sloped front
        m = T(x, yw, zw) @ _q(wn)
        p.add(props.box(W * 0.38, ch * 0.62 / max(wn.y, 0.3), 0.012), 'shade_dark', m @ T(0, 0, 0.004))
        p.add(props.box(W * 0.33, ch * 0.5 / max(wn.y, 0.3), 0.012), 'port', m @ T(0, 0, 0.008))
    p.add(props.box(0.03, 0.02, ch * 0.9), 'shade_dark', T(0, yw, zw))   # the centre pillar
    for sgn in (-1, 1):
        xs = sgn * (W / 2 * (1 - 0.06 * 0.5) + 0.004)
        if cfg.get('cab_strip'):   # one dark window strip along the cab's side
            p.add(props.box(0.014, cl * 0.7, ch * 0.42), 'port', T(xs + sgn * 0.002, cy - cl * 0.05, deck + ch * 0.62))
        else:
            for yy in (cy - cl * 0.22, cy + cl * 0.12):   # the side windows
                p.add(props.box(0.012, cl * 0.24, ch * 0.4), 'shade_dark', T(xs, yy, deck + ch * 0.62))
                p.add(props.box(0.016, cl * 0.2, ch * 0.32), 'port', T(xs + sgn * 0.002, yy, deck + ch * 0.62))
        p.add(props.box(0.012, 0.012, ch * 0.75), 'shade_dark', T(xs, cy - cl * 0.36, deck + ch * 0.45))   # the door seam
        if cfg.get('mirrors', True):
            vkit.rod(p, (sgn * W * 0.5, yw - 0.02, croof - 0.02), (sgn * (W * 0.5 + 0.06), yw + 0.02, croof - 0.02),
                     cfg.get('mirror_r', 0.006), 'gunmetal', seg=5)   # the mirror arm and its mirror
            p.add(props.box(0.012, 0.05, 0.07), 'shade_dark', T(sgn * (W * 0.5 + 0.065), yw + 0.025, croof - 0.04))
        mark(p, vk, cfg.get('chev_cab', 0.1), (xs + sgn * 0.006, cy - cl * 0.05, deck + ch * 0.22), (sgn, 0, 0))
    yr0, yr1 = cy - cl / 2, cy + cl / 2 * (1 - cf)
    p.add(props.box(W * 1.02, yr1 - yr0 + 0.03, 0.014), cfg.get('cab_lip_key', 'armor_edge'), T(0, (yr0 + yr1) / 2, croof + 0.002),
          bevel=0.004)   # the lip
    p.add(props.box(W * 0.94, yr1 - yr0 - 0.02, 0.012), cfg.get('cab_roof_key', 'armor_paint'), T(0, (yr0 + yr1) / 2, croof + 0.01),
          bevel=0.003)
    rim_hatch(p, (W * 0.22, (yr0 + yr1) / 2, croof + 0.012), 0.07, rim_key='shade_dark', key=cfg.get('cab_hatch_key', 'armor_paint'))
    if cfg.get('screen_band'):   # one dark band across the sloped front: the windscreen at 1x
        p.add(props.box(W * 0.86, ch * 0.5 / max(wn.y, 0.3), 0.01), 'port', T(0, yw, zw) @ _q(wn) @ T(0, 0, 0.002))
    if cfg.get('chev_front'):
        for x_ in (-W * 0.3, W * 0.3):
            mark(p, vk, cfg['chev_front'], (x_, yF + 0.006, z_f + H * 0.5), (0, 1, 0))
    for sgn in (-1, 1):   # headlights in guards on the nose, tow hooks
        light(p, sgn * W * 0.38, yF - 0.01, deck - 0.02, r=0.022, guard=True)
        p.add(props.lathe([(0, 0.025), (0.03, 0.02)], seg=8), 'gunmetal', T(sgn * W * 0.22, yF + 0.01, z_f - 0.03) @ R('X', -90),
              smooth=True)
    # ---- the hull: the engine grille and exhaust behind the cab, fender lips, dark side faces, bins
    gy = cy - cl / 2 - 0.1
    p.add(props.box(W * 0.6, 0.15, 0.01), 'armor_edge', T(0, gy, deck + 0.005), bevel=0.003)
    grille(p, 0, gy, W * 0.55, 0.13, deck + 0.008, n=5, key='armor_shade', frame_key='deck_dark')
    vkit.rod(p, (W * 0.38, gy + 0.02, deck), (W * 0.38, gy + 0.02, deck + 0.12), 0.02, 'shade_dark', seg=8)   # the exhaust stack
    p.add(props.lathe([(0, 0.026), (0.02, 0.026)], seg=10), 'port', T(W * 0.38, gy + 0.02, deck + 0.12), smooth=True)
    for sgn in (-1, 1):
        p.add(props.box(0.03, L * 0.96, 0.016), 'armor_edge', T(sgn * (W / 2 - 0.005), 0, z_f + 0.004), bevel=0.004)   # fender lip
        p.add(props.box(0.012, L * 0.96, 0.02), 'port', T(sgn * (W / 2 + 0.006), 0, z_f - 0.012))   # its shadow
        if cfg.get('dark_sides'):   # the hull's vertical sides in the darker paint (steel in shade, not printed card)
            mark(p, 'rect', (Lh * 0.9 - cl, H * 0.8), (sgn * (W / 2 + 0.001), hy - cl / 2, z_f + H / 2), (sgn, 0, 0), key=sk,
                 thick=0.004)
        stowage(p, (sgn * W * 0.38, yR + 0.12, deck), (0.16, 0.18, 0.08), key='canvas_dark' if sgn < 0 else 'deck_dark', straps=1)
        if cfg.get('hull_detail'):
            for k in range(1, 5):   # vertical panel seams on the hull side
                p.add(props.box(0.006, 0.012, H * 0.8), 'shade_dark', T(sgn * (W / 2 + 0.004), yR + (Lh - cl) * k / 5, z_f + H / 2))
                p.add(props.box(0.006, 0.008, H * 0.8), 'armor_edge', T(sgn * (W / 2 + 0.004), yR + (Lh - cl) * k / 5 + 0.012, z_f + H / 2))
            vkit.rod(p, (sgn * (W / 2 - 0.004), yR + 0.02, deck), (sgn * (W / 2 - 0.004), cy - cl / 2, deck), 0.007, 'armor_edge', seg=5)
            for k in range(3):   # boxes along the fender
                yy = yR + 0.3 + 0.22 * k
                p.add(props.box(0.06, 0.16, 0.06), 'deck_dark', T(sgn * (W / 2 - 0.03), yy, z_f + 0.03), bevel=0.006)
                p.add(props.box(0.064, 0.012, 0.064), 'shade_dark', T(sgn * (W / 2 - 0.03), yy + 0.06, z_f + 0.03))
    if cfg.get('hull_detail'):   # two rectangular hatches on the rear deck, framed
        for sgn in (-1, 1):
            hx, hy_ = sgn * W * 0.13, yR + 0.14
            p.add(props.box(0.18, 0.16, 0.006), 'shade_dark', T(hx, hy_, deck + 0.002))
            p.add(props.box(0.16, 0.14, 0.012), 'armor_paint', T(hx, hy_, deck + 0.006), bevel=0.003)
    # ---- the rear: a door, tow hooks, tail lights, two chevrons
    rq = Vector((0, yR - 0.006, z_f + H * 0.5))
    p.add(props.box(W * 0.34, 0.012, H * 0.8), 'shade_dark', T(0, rq.y, rq.z))
    p.add(props.box(W * 0.3, 0.016, H * 0.7), 'armor_paint', T(0, rq.y - 0.003, rq.z))
    for sgn in (-1, 1):
        p.add(props.box(0.04, 0.012, 0.03), 'mark_red', T(sgn * W * 0.42, rq.y - 0.004, rq.z + H * 0.2))
        p.add(props.lathe([(0, 0.025), (0.03, 0.02)], seg=8), 'gunmetal', T(sgn * W * 0.3, rq.y - 0.01, z_f - 0.03) @ R('X', 90),
              smooth=True)
        mark(p, vk, cfg.get('chev_rear', 0.1), (sgn * W * 0.3, rq.y - 0.008, rq.z), (0, -1, 0))
    # ---- the launch station: bearing ring, pedestal, turret block with the canopy, trunnions, two arms
    sy = hy + cfg.get('station_dy', -0.28)
    p.add(props.lathe([(0, 0.3), (0.03, 0.3)], seg=28), 'shade_dark', T(0, sy, deck), smooth=True)          # the recess
    p.add(props.lathe([(0, 0.27), (0.012, 0.27)], seg=28), 'armor_edge', T(0, sy, deck + 0.03), smooth=True)   # the bearing
    p.add(props.lathe([(0, 0.24), (0.07, 0.22)], seg=24), sk, T(0, sy, deck + 0.042), smooth=True)          # the pedestal
    if cfg.get('drum_bands'):   # turned metal: a lit band at the top, a dark one at the foot
        p.add(props.lathe([(0, 0.226), (0.012, 0.224)], seg=28), 'armor_edge', T(0, sy, deck + 0.1), smooth=True)
        p.add(props.lathe([(0, 0.245), (0.018, 0.243)], seg=28), 'shade_dark', T(0, sy, deck + 0.042), smooth=True)
    gw, gl_, gh = cfg.get('gcab', (0.3, 0.36, 0.24))
    gz0 = deck + 0.11
    p.add(plate(gw, gl_, gh, cell=0.04, front=0.2, taper=0.1), sk, T(0, sy, gz0 + gh / 2), bevel=0.01)
    groof = gz0 + gh
    p.add(props.box(gw * 0.92, gl_ * 0.82, 0.012), 'armor_edge', T(0, sy - 0.01, groof + 0.002), bevel=0.003)
    # the gunner's canopy: a dark-glazed box on top, framed
    p.add(props.box(gw * 0.6, gl_ * 0.5, 0.09), 'shade_dark', T(0, sy + 0.02, groof + 0.05), bevel=0.012)
    for f, (sx_, sy_) in (((0, 1), (gw * 0.52, 0.012)), ((1, 0), (0.012, gl_ * 0.42)), ((-1, 0), (0.012, gl_ * 0.42))):
        p.add(props.box(sx_, sy_, 0.055), 'port',
              T(f[0] * gw * 0.3 + f[0] * 0.004, sy + 0.02 + f[1] * (gl_ * 0.25 + 0.004), groof + 0.055))
    p.add(props.box(gw * 0.62, gl_ * 0.52, 0.012), 'armor_edge', T(0, sy + 0.02, groof + 0.098), bevel=0.003)   # its lit roof
    for sgn in (-1, 1):
        mark(p, vk, cfg.get('chev_station', 0.1), (sgn * (gw / 2 * 0.95 + 0.008), sy - 0.04, gz0 + gh * 0.4), (sgn, 0, 0.1))
    elev = cfg.get('elev', 25.0)
    mr, ml = cfg.get('missile_r', 0.034), cfg.get('missile_l', 0.95)
    mk = cfg.get('missile_key', 'missile_white')
    tz = gz0 + gh * 0.55
    hull_p = p
    lp = vkit.part(p.root, 'launcher', p.colors, p.weather) if cfg.get('launcher_no_fit') else p
    for sgn in (-1, 1):
        p = hull_p
        ax = sgn * (gw / 2 + cfg.get('arm_out', 0.15))
        vkit.rod(p, (sgn * gw / 2 * 0.9, sy, tz), (ax, sy, tz), 0.03, 'shade_dark', seg=10)          # the trunnion
        p.add(props.lathe([(0, 0.045), (0.02, 0.045)], seg=14), 'gunmetal', T(ax - sgn * 0.03, sy, tz) @ R('Y', 90), smooth=True)
        p = lp
        piv = T(ax, sy, tz) @ R('Z', -sgn * cfg.get('toe', 0.0)) @ R('X', elev)
        fwd = cfg.get('missile_fwd', 0.0)   # the missiles' centre ahead of the trunnion, along the rails
        if cfg.get('actuators'):   # the elevation ram from the block's foot up to the arm
            a_ = Vector((sgn * gw / 2 * 0.9, sy - 0.08, gz0 + 0.03))
            b_ = piv @ Vector((0.0, -0.12, 0.0))
            vkit.rod(p, a_, b_, 0.014, 'gunmetal', seg=8)
        sp = cfg.get('missile_gap', 0.075)
        p.add(props.box(0.03, ml * 0.7, 0.07), 'shade_dark', piv @ T(0, 0.03 + fwd, 0.0), bevel=0.006)    # the arm
        for z in (sp, -sp):   # two missiles per arm, on rail shoes
            mm = piv @ T(0, fwd, z)
            rail = mm @ R('X', -90)
            for yy in (-ml * 0.25, ml * 0.15):
                p.add(props.box(0.02, 0.05, abs(z) - mr + 0.004), 'shade_dark', mm @ T(0, yy, -math.copysign((abs(z) - mr) / 2, z)))
            p.add(props.lathe([(-ml / 2, mr), (ml / 2 - 0.12, mr), (ml / 2 - 0.06, mr * 0.75), (ml / 2, mr * 0.2)], seg=16),
                  mk, rail, smooth=True)
            nl = cfg.get('nose_l', 0.09)
            p.add(props.lathe([(ml / 2 - nl, mr * 0.96), (ml / 2 - 0.06, mr * 0.75), (ml / 2, mr * 0.2), (ml / 2 + 0.004, 0.0)],
                              seg=16), cfg.get('nose_key', 'shade_dark'), rail @ T(0, 0, 0.001), smooth=True)
            if cfg.get('missile_band'):
                p.add(props.lathe([(-ml * 0.05, mr * 1.05), (-ml * 0.05 + cfg['missile_band'], mr * 1.05)], seg=16), 'tint',
                      rail, tint=True, smooth=True)
            for a in (45, 135):   # tail fins and canards, dark tips
                p.add(props.box(0.006, 0.13, mr * 5.6), mk, mm @ T(0, -ml / 2 + 0.07, 0) @ R('Y', a), bevel=0.002)
                p.add(props.box(0.007, 0.03, mr * 5.8), 'shade_dark', mm @ T(0, -ml / 2 + 0.01, 0) @ R('Y', a))
                p.add(props.box(0.005, 0.05, mr * 3.8), mk, mm @ T(0, ml / 2 - 0.22, 0) @ R('Y', a), bevel=0.002)
    p = hull_p
    if lp is not hull_p:
        lobj = lp.finish()
        lobj['no_fit'] = True   # sized like the other vehicles by its hull, as the guns are


def _mlrs(p, cfg, d, camo):
    """The M270 MLRS: the armoured cab forward (a sloped glazed front with
    a dark windscreen band, a darker roof with a hatch, headlights, side
    windows), the launcher-loader module behind it in a darker paint,
    raised a few degrees off its cradle: two pods split by a dark seam,
    ribbed sides, a lit top rim, the twelve tube mouths on its front face,
    the loader booms on top; a dark shadow gap under it; civ inverted-V
    chevrons on the box sides and rear, the cab sides and front."""
    deck, W, H, hy, Lh, z_f, L = d['deck'], d['W'], d['H'], d['hy'], d['Lh'], d['z_f'], d['L']
    vk = cfg.get('chev_kind', 'vee')
    sk = 'armor_turret' if cfg.get('turret_tone') else 'armor_paint'
    yF, yR = hy + Lh / 2, hy - Lh / 2
    # ---- the cab
    cl, ch, cf = cfg.get('cab_l', 0.5), cfg.get('cab_h', 0.26), cfg.get('cab_front', 0.4)
    cy = yF - cl / 2 - 0.01
    p.add(plate(W, cl, ch, cell=0.05, front=cf, taper=0.05), 'armor_paint', T(0, cy, deck + ch / 2), bevel=0.012)
    croof = deck + ch
    wn = Vector((0, ch, cl / 2 * cf)).normalized()
    yw, zw = cy + cl / 2 * (1 - cf * 0.55), deck + ch * 0.55
    if cfg.get('panes'):   # three framed panes on the angled front, a light frame between them
        ph = ch * 0.42 / max(wn.y, 0.3)
        p.add(props.box(W * 0.9, ph * 1.15, 0.01), 'armor_edge', T(0, yw, zw) @ _q(wn) @ T(0, 0, 0.002))
        for x in (-W * 0.29, 0.0, W * 0.29):
            p.add(props.box(W * 0.26, ph, 0.012), 'port', T(x, yw, zw) @ _q(wn) @ T(0, 0, 0.004))
    else:
        p.add(props.box(W * 0.86, ch * 0.42 / max(wn.y, 0.3), 0.012), 'port', T(0, yw, zw) @ _q(wn) @ T(0, 0, 0.003))   # the windscreen band
    for x in (() if cfg.get('panes') else (-W * 0.15, W * 0.15)):   # the pillars
        p.add(props.box(0.03, ch * 0.44 / max(wn.y, 0.3), 0.014), 'shade_dark', T(x, yw, zw) @ _q(wn) @ T(0, 0, 0.006))
    for sgn in (-1, 1):
        xs = sgn * (W / 2 * (1 - 0.05 * 0.55) + 0.004)
        p.add(props.box(0.014, cl * 0.36, ch * 0.3), 'port', T(xs, cy + cl * 0.05, deck + ch * 0.62))   # side window
        p.add(props.box(0.012, 0.012, ch * 0.8), 'shade_dark', T(xs, cy - cl * 0.25, deck + ch * 0.45))   # door seam
        if cfg.get('panes'):   # the door's outline
            p.add(props.box(0.012, 0.012, ch * 0.8), 'shade_dark', T(xs, cy + cl * 0.2, deck + ch * 0.45))
            p.add(props.box(0.012, cl * 0.45, 0.012), 'shade_dark', T(xs, cy - cl * 0.025, deck + ch * 0.85))
        mark(p, vk, cfg.get('chev_cab', 0.12), (xs + sgn * 0.006, cy - cl * 0.05, deck + ch * 0.25), (sgn, 0, 0))
        light(p, sgn * W * 0.4, yF - 0.005, z_f + H * 0.6, r=0.024, guard=True)
        p.add(props.lathe([(0, 0.025), (0.03, 0.02)], seg=8), 'gunmetal', T(sgn * W * 0.25, yF + 0.01, z_f - 0.03) @ R('X', -90),
              smooth=True)
    if cfg.get('cab_kit'):
        for k in range(cfg.get('louvres', 3)):   # louvres over the windscreen band
            nl_ = cfg.get('louvres', 3)
            p.add(props.box(W * 0.88, 0.012, 0.01), 'shade_dark',
                  T(0, yw, zw) @ _q(wn) @ T(0, -ch * 0.12 + ch * 0.24 * k / max(nl_ - 1, 1), 0.008))
        if cfg.get('mullions'):   # light frames between the panes, over the louvres
            for x in (-W * 0.145, W * 0.145, -W * 0.44, W * 0.44):
                p.add(props.box(0.03, ch * 0.5 / max(wn.y, 0.3), 0.012), 'armor_edge', T(x, yw, zw) @ _q(wn) @ T(0, 0, 0.012))
            p.add(props.box(W * 0.6, 0.02, ch * 0.4), 'port', T(0, cy - cl / 2 - 0.004, deck + ch * 0.72))   # the gap behind the cab
        for sgn in (-1, 1):
            xs2 = sgn * (W / 2 + 0.012)
            vkit.rod(p, (xs2, cy - cl * 0.3, deck + ch * 0.85), (xs2, cy + cl * 0.2, deck + ch * 0.85), 0.006, 'gunmetal', seg=5)
            if cfg.get('mirrors', True):
                vkit.rod(p, (sgn * W * 0.5, yF - 0.06, croof - 0.03), (sgn * (W * 0.5 + 0.06), yF - 0.03, croof - 0.03), 0.006,
                         'gunmetal', seg=5)
                p.add(props.box(0.012, 0.05, 0.07), 'shade_dark', T(sgn * (W * 0.5 + 0.065), yF - 0.025, croof - 0.05))
        for k in range(5):   # the nose grille's louvres
            p.add(props.box(W * 0.5, 0.012, 0.01), 'shade_dark', T(0, yF + 0.004, z_f + H * 0.15 + 0.022 * k) @ R('X', 20))
    yr0, yr1 = cy - cl / 2, cy + cl / 2 * (1 - cf)
    p.add(props.box(W * 0.96, yr1 - yr0 - 0.02, 0.012), cfg.get('cab_roof_key', 'armor_paint'), T(0, (yr0 + yr1) / 2, croof + 0.004),
          bevel=0.003)
    rim_hatch(p, (-W * 0.22, (yr0 + yr1) / 2, croof + 0.01), 0.07, rim_key=cfg.get('cab_hatch_rim', 'shade_dark'), key='shade_dark')
    if cfg.get('vision_blocks'):
        for k in range(5):
            a_ = math.radians(-60 + 30 * k)
            periscope(p, (-W * 0.22 + 0.11 * math.sin(a_), (yr0 + yr1) / 2 + 0.11 * math.cos(a_), croof + 0.008), -math.degrees(a_))
    p.add(props.box(0.012, yr1 - yr0, 0.012), 'shade_dark', T(W * 0.1, (yr0 + yr1) / 2, croof + 0.012))   # a roof seam
    if cfg.get('chev_front'):
        mark(p, vk, cfg['chev_front'], (W * 0.3, yF + 0.006, z_f + H * 0.45), (0, 1, 0))
    # ---- the launcher box: a cradle, raised a little, two pods, ribs, the tube grid
    bl, bh = cfg.get('box_l', 1.1), cfg.get('box_h', 0.32)
    bw = W * 0.96
    by = yR + bl / 2 + 0.02
    p.add(props.box(W * 0.8, bl * 0.8, 0.04), 'shade_dark', T(0, by, deck + 0.02))   # the cradle (and the shadow gap under the box)
    if cfg.get('box_mount'):   # the turntable and the pivot under the box, two cradle rails
        p.add(props.lathe([(0, 0.3), (0.05, 0.29)], seg=28), 'gunmetal', T(0, by + 0.08, deck + 0.005), smooth=True)
        for sgn in (-1, 1):
            p.add(props.box(0.05, bl * 0.9, 0.035), 'gunmetal', T(sgn * W * 0.3, by, deck + 0.05), bevel=0.006)
        vkit.rod(p, (-W * 0.42, by - bl / 2 + 0.06, deck + 0.06), (W * 0.42, by - bl / 2 + 0.06, deck + 0.06), 0.03, 'gunmetal', seg=10)
    elev = cfg.get('box_elev', 5.0)
    lift = cfg.get('box_lift', 0.05)
    pv = T(0, by - bl / 2, deck + lift) @ R('X', elev) @ T(0, bl / 2, bh / 2)   # hinged at its rear foot
    if cfg.get('mount_block'):   # the dark mount the box sits on: a block with the turntable ring, a shadow gap in front
        p.add(props.box(W * 0.66, bl * 0.62, lift - 0.01), cfg.get('mount_key', 'shade_dark'), T(0, by - bl * 0.12, deck + (lift - 0.01) / 2),
              bevel=0.008)
        if cfg.get('mount_key'):
            p.add(props.box(W * 0.67, bl * 0.63, 0.012), 'armor_edge', T(0, by - bl * 0.12, deck + lift - 0.012), bevel=0.003)
            p.add(props.box(W * 0.67, bl * 0.63, 0.016), 'shade_dark', T(0, by - bl * 0.12, deck + 0.01))
        p.add(props.lathe([(0, 0.27), (0.02, 0.27)], seg=28), 'gun_edge', T(0, by - bl * 0.12, deck + lift - 0.02), smooth=True)
    p.add(plate(bw, bl, bh, cell=0.06), sk, pv, bevel=0.01)
    p.add(props.box(0.014, bl * 1.002, bh * 1.002), 'shade_dark', pv)   # the seam between the pods
    if cfg.get('box_rim', True):
        p.add(props.box(bw * 1.01, bl * 0.98, 0.012), 'armor_edge', pv @ T(0, 0, bh / 2 + 0.002), bevel=0.003)   # lit top rim
        p.add(props.box(bw * 0.94, bl * 0.94, 0.014), sk, pv @ T(0, 0, bh / 2 + 0.006), bevel=0.003)
    else:   # a dark band round the box's foot where it meets the mount
        p.add(props.box(bw * 1.01, bl * 1.005, 0.03), 'shade_dark', pv @ T(0, 0, -bh / 2 + 0.012))
        if cfg.get('box_lid'):   # the lid in the lighter hull paint: the top takes the light, the sides stay in the darker paint
            if cfg.get('pod_split'):   # two lids, one per pod, a dark gap between; straps and hoist points
                for sgn in (-1, 1):
                    p.add(props.box(bw * 0.47, bl * 0.995, 0.016), 'armor_paint', pv @ T(sgn * bw * 0.255, 0, bh / 2 + 0.002),
                          bevel=0.006)
                    for f in (-0.3, 0.3):
                        p.add(props.box(bw * 0.47, 0.022, 0.02), 'shade_dark', pv @ T(sgn * bw * 0.255, bl * f, bh / 2 + 0.004))
                    for f in (-0.46, 0.46):
                        p.add(props.box(0.04, 0.04, 0.035), 'shade_dark', pv @ T(sgn * bw * 0.44, bl * f, bh / 2 + 0.016), bevel=0.005)
                p.add(props.box(0.05, bl * 1.002, 0.03), 'port', pv @ T(0, 0, bh / 2 - 0.004))
            else:
                p.add(props.box(bw * 0.995, bl * 0.995, 0.016), 'armor_paint', pv @ T(0, 0, bh / 2 + 0.002), bevel=0.006)
    for sgn in (-1, 1):
        for k in range(7):   # the framing ribs on the sides
            p.add(props.box(0.014, 0.018, bh * 0.92), 'shade_dark', pv @ T(sgn * (bw / 2 + 0.004), -bl / 2 + bl * (k + 0.5) / 7, 0))
        if cfg.get('box_rim', True):
            p.add(props.box(0.014, bl, 0.016), 'armor_edge', pv @ T(sgn * (bw / 2 + 0.004), 0, bh * 0.44))
        else:   # two pods a side: a vertical gap mid-length, a dark horizontal frame
            p.add(props.box(0.016, cfg.get('side_seam', 0.03), bh * 1.002), 'port', pv @ T(sgn * (bw / 2 + 0.004), 0, 0))
            p.add(props.box(0.016, bl * 0.98, 0.016), 'shade_dark', pv @ T(sgn * (bw / 2 + 0.004), 0, bh * 0.36))
            if cfg.get('box_kit'):
                p.add(props.box(0.006, bl * 0.99, bh * 0.3), 'skirt_dirt', pv @ T(sgn * (bw / 2 + 0.002), 0, -bh * 0.33))   # darker foot
                p.add(props.box(0.008, bl * 0.99, 0.012), 'armor_edge', pv @ T(sgn * (bw / 2 + 0.003), 0, bh * 0.47))   # lit top line
                vkit.rod(p, pv @ Vector((sgn * (bw / 2 + 0.02), -bl * 0.45, bh * 0.2)), pv @ Vector((sgn * (bw / 2 + 0.02), bl * 0.45, bh * 0.2)),
                         0.012, 'gunmetal', seg=6)   # the lifting-arm rail
                vkit.rod(p, pv @ Vector((sgn * (bw / 2 + 0.014), -bl * 0.4, -bh * 0.1)), pv @ Vector((sgn * (bw / 2 + 0.014), bl * 0.4, -bh * 0.1)),
                         0.007, 'shade_dark', seg=5)   # a cable run
                if cfg.get('hoists'):
                    p.add(props.box(0.008, bl * 0.98, 0.01), 'armor_edge', pv @ T(sgn * (bw / 2 + 0.006), 0, bh * 0.33))   # the rail's lit edge
                    for f in (-0.3, 0.3):   # the hoist cylinders down at the cradle
                        vkit.rod(p, pv @ Vector((sgn * (bw / 2 - 0.02), bl * f, -bh / 2 - 0.005)),
                                 pv @ Vector((sgn * (bw / 2 - 0.02), bl * f + 0.16, -bh / 2 - 0.005)), 0.025, 'gunmetal', seg=10)
        mark(p, vk, cfg.get('chev_box', 0.16), pv @ Vector((sgn * (bw / 2 + 0.012), -bl * 0.08, -bh * 0.05)), (sgn, 0, 0))
        for i in range(3):   # the tube mouths: 2 pods x 3 x 2 on the front face
            for j in range(2):
                aft = cfg.get('tubes_aft')
                q = pv @ Vector((sgn * bw * 0.25 + (i - 1) * bw * 0.14, (-bl / 2 - 0.003) if aft else (bl / 2 + 0.003),
                                 -bh * 0.2 + j * bh * 0.42))
                rx = (90 + elev) if aft else (-90 + elev)
                p.add(props.lathe([(0, 0.05), (0.008, 0.05)], seg=14), cfg.get('tube_rim', 'gun_edge'), T(*q) @ R('X', rx), smooth=True)
                p.add(props.lathe([(-cfg.get('bore_depth', 0.0), 0.038), (0.012, 0.038)], seg=14), 'port', T(*q) @ R('X', rx) @ T(0, 0, 0.002),
                      smooth=True)
                if cfg.get('tube_proud'):   # the tube ends standing proud of the face, so they show at the corners
                    p.add(props.lathe([(-cfg['tube_proud'], 0.05), (0.0, 0.05)], seg=14), 'shade_dark', T(*q) @ R('X', rx), smooth=True)
    # the loader booms on top, the rear face's chevron
    if cfg.get('loader_boom'):   # the loader boom and its hoist housing along the box's top front edge
        p.add(props.box(bw * 0.92, 0.07, 0.05), 'shade_dark', pv @ T(0, bl / 2 - 0.05, bh / 2 + 0.03), bevel=0.008)
        p.add(props.box(0.14, 0.12, 0.09), 'shade_dark', pv @ T(-bw * 0.32, bl / 2 - 0.07, bh / 2 + 0.05), bevel=0.01)
        p.add(props.box(bw * 0.92, 0.012, 0.01), 'gun_edge', pv @ T(0, bl / 2 - 0.05, bh / 2 + 0.057))
    for sgn in ((-1, 1) if cfg.get('booms', True) else ()):
        vkit.rod(p, pv @ Vector((sgn * bw * 0.4, -bl * 0.45, bh / 2 + 0.03)), pv @ Vector((sgn * bw * 0.4, bl * 0.3, bh / 2 + 0.03)),
                 0.014, 'shade_dark', seg=6)
        p.add(props.box(0.05, 0.06, 0.05), 'shade_dark', pv @ T(sgn * bw * 0.4, -bl * 0.45, bh / 2 + 0.03), bevel=0.006)
    if cfg.get('tubes_aft'):   # the cab-end face solid, braced: a dark frame and a cross; the pods' gap on the tube face
        f0 = pv @ Vector((0, bl / 2 + 0.004, 0))
        fm = T(*f0) @ R('X', elev)
        for sgn in (-1, 1):
            p.add(props.box(bw * 0.46, 0.012, 0.022), 'shade_dark', fm @ T(sgn * bw * 0.25, 0, bh * 0.38))
            p.add(props.box(bw * 0.46, 0.012, 0.022), 'shade_dark', fm @ T(sgn * bw * 0.25, 0, -bh * 0.38))
            p.add(props.box(0.02, 0.012, bh * 0.98), 'shade_dark', fm @ T(sgn * bw * 0.48, 0, 0))
            if cfg.get('front_panel'):
                p.add(props.box(bw * 0.42, 0.008, bh * 0.66), 'deck_dark', fm @ T(sgn * bw * 0.25, 0.001, 0))
                for bx_ in (-0.16, 0.16):
                    for bz_ in (-0.25, 0.25):
                        p.add(props.box(0.018, 0.016, 0.018), 'gunmetal', fm @ T(sgn * bw * 0.25 + bx_ * bw, 0.006, bz_ * bh))
                p.add(props.box(0.05, 0.02, 0.03), 'gun_edge', fm @ T(sgn * bw * 0.25, 0.008, -bh * 0.08))   # a latch
            else:
                p.add(props.box(0.018, 0.012, bh * 0.95), 'shade_dark', fm @ T(sgn * bw * 0.25, 0, 0) @ R('Y', sgn * 35))
        p.add(props.box(0.04, 0.02, bh * 1.002), 'port', pv @ T(0, -bl / 2 - 0.004, 0))
        if cfg.get('dark_tube_face'):
            for sgn in (-1, 1):
                p.add(props.box(bw * 0.44, 0.008, bh * 0.86), 'deck_dark', pv @ T(sgn * bw * 0.25, -bl / 2 - 0.002, 0))
                if cfg.get('pod_rim'):   # a lighter frame round each pod's tube face
                    for zz in (-bh * 0.45, bh * 0.45):
                        p.add(props.box(bw * 0.46, 0.014, 0.02), 'armor_edge', pv @ T(sgn * bw * 0.25, -bl / 2 - 0.006, zz))
                    for xx in (0.02, 0.48):
                        p.add(props.box(0.02, 0.014, bh * 0.92), 'armor_edge', pv @ T(sgn * bw * xx, -bl / 2 - 0.006, 0))
        mark(p, vk, cfg.get('chev_rear', 0.14), (W * 0.3, yR - 0.008, z_f + H * 0.5), (0, -1, 0))   # on the hull's rear plate
    else:
        mark(p, vk, cfg.get('chev_rear', 0.14), pv @ Vector((bw * 0.25, -bl / 2 - 0.006, 0)), (0, -1, 0))
    if cfg.get('chev_top'):   # a civ chevron on the box's roof (the stock's top mark)
        mark(p, vk, cfg['chev_top'], pv @ Vector((0, -bl * 0.1, bh / 2 + 0.014)), (pv.to_3x3() @ Vector((0, 0, 1))), rot=0)
    # ---- the hull: lit fender lips over a dark shadow, tail lights
    for sgn in (-1, 1):
        p.add(props.box(0.03, L * 0.96, 0.016), 'armor_edge', T(sgn * (W / 2 - 0.005), 0, z_f + 0.004), bevel=0.004)
        p.add(props.box(0.012, L * 0.96, 0.02), 'port', T(sgn * (W / 2 + 0.006), 0, z_f - 0.012))
        p.add(props.box(0.04, 0.012, 0.03), 'mark_red', T(sgn * W * 0.42, yR - 0.006, z_f + H * 0.6))
        if cfg.get('hull_seams'):   # dark seams with a light bevel on the hull side, a stowage box behind the cab
            for k in range(1, 6):
                yy = yR + (Lh - cl) * k / 6
                p.add(props.box(0.006, 0.012, H * 0.8), 'shade_dark', T(sgn * (W / 2 + 0.004), yy, z_f + H / 2))
                p.add(props.box(0.006, 0.008, H * 0.8), 'armor_edge', T(sgn * (W / 2 + 0.004), yy + 0.012, z_f + H / 2))
            p.add(props.box(0.08, 0.14, 0.07), 'deck_dark', T(sgn * (W / 2 - 0.05), cy - cl / 2 - 0.09, deck + 0.035), bevel=0.006)


ARMOR = {
    # an olive-drab M4 Sherman (VVSS, 75 mm): the wide civ band round the turret, civ stars on the hull
    'Tank': dict(L=1.62, W=1.04, H=0.24, tw=0.22, rw=0.09, wheels=6, rollers=3, vvss=(-0.5, -0.01, 0.48), pair_gap=0.003, roll_r=0.042,
                 bogie_key='shade_dark', spring_key='shade_dark', bogie_chunk=1.3,
                 track_style='chevron', holes=6, hole_r=0.13, hole_key='gunmetal', teeth=13,
                 front_wheel=(0.72, 0.25, 0.1), rear_wheel=(-0.74, 0.17, 0.095),
                 upper=dict(front=0.56, front_low=0.04, rear=0.12), nose=0.45,
                 paint='#c4b68c', colors={'canvas': '#8a7a58', 'canvas_dark': '#6a5e40', 'armor_edge': '#d4c8a2'},
                 camo=[('armor_camo2', 5.0, 0.2, 3.0), ('armor_camo4', 6.5, 0.38, 8.0), ('armor_camo3', 3.0, 0.42, 5.0)],
                 camo_soft=0.045,
                 camo3='#7e6e52', gun_key='gun_olive', muzzle_key='armor_edge', muzzle_r=1.2, turret_cast=True, deck_grilles=True, hull_hatches=0.075,
                 deck_bundle=(0.3, 0.13, 0.1), lip_key='shade_dark', turret_shade=True,
                 gun_len=0.666, muzzle_slim=True, gun_tube=True, turret_rz=0.26, roof_flat=0.45, bustle=1.45, bustle_lift=0.12,
                 band_rear=158.0, mantlet_block=True, loader_hatch=0.075, hatch_key='armor_camo3', hatch_rim='armor_camo2', cupola=True,
                 bow_key='gun_olive', bow_scale=1.3, glacis_joint=True, shadow_line=True, side_tools=True,
                 air_cleaners=True,
                 camo2='#a2946c', camo4='#958260', turret_y=-0.04, tarp=False, hull_hatch_x=0.27, band=(0.0, 0.058), band_front=0.2,
                 turret_star=None, rear_star=0.15,
                 simple_roof=True, gun_highlight=True, hull_edges=True, rear_plain=True,
                 gun_r=0.056, mantlet_key='gun_olive', gun_no_fit=True, gun_no_shadow=True, sponson_lip=True, side_star=0.12, glacis_star=0.17,
                 belly=0.03, hatch_open=115.0,
                 wear={'armor_paint': {'grime': 0.3, 'soot': 0.1, 'edges': 0.2, 'rust': 0.0, 'chips': 0.45,
                                       'dust': 1.0, 'dust_h': 0.55, 'exhaust': 0.7, 'exhaust_y': -0.62,
                                       'fade': 0.2, 'streaks': 0.5},
                       'armor_shade': {'chips': 0.3, 'dust': 0.8, 'dust_h': 0.42, 'exhaust': 0.6, 'exhaust_y': -0.62},
                       'armor_camo2': {'dust': 0.5, 'dust_h': 0.42, 'chips': 0.4},
                       'track': {'dust': 0.6, 'dust_h': 0.3}, 'armor_edge': {'dust': 0.5, 'dust_h': 0.55}, 'track_pin': {'dust': 0.4, 'dust_h': 0.3},
                       'rubber': {'dust': 0.7, 'dust_h': 0.3}, 'gunmetal': {'dust': 0.5, 'dust_h': 0.42},
                       'canvas': {'dust': 0.3}, 'tint': {'chips': 0.3},
                       'gun_olive': {'edges': 0.0, 'grime': 0.25}, 'shade_dark': {'edges': 0.0}},
                 builder=_tank),
    # a Panzer IV Ausf. H: Dunkelgelb with sprayed olive and red-brown bands, Schuerzen, the long 75 mm
    'Panzer': dict(L=1.92, W=0.98, H=0.185, tskirt_edge=False, tw=0.2, rw=0.072, wheels=8, rollers=4, holes=0,
                   leaf=(-0.6, -0.2, 0.2, 0.6), pair_gap=0.004, wheel_face='armor_paint', bogie_key='shade_dark',
                   front_wheel=(0.8, 0.2, 0.085), rear_wheel=(-0.88, 0.17, 0.075), teeth=12,
                   upper=dict(front=0.0, rear=0.06), upper_len=0.84, nose=0.5, belly=0.06,
                   paint='#b49e6e', camo2='#737548', camo4='#84694c', deck_links='rack', shadow_gaps=True, brake_lit=True,
                   corner_plate=(0.075, 0.12), brake_key='gun_edge', front_x=0.2, tac_x=0.39, Lh=1.86, skirt_tilt=0.0, skirt_n=6, skirt_edge=False, rail_line=True,
                   skirt_lid=True, cupola_h=0.13, ball_key='armor_shade', fd_r=0.085, fd_key='armor_shade', hull_faces=True,
                   link=(0.11, 0.09), bow_edge=True,
                   camo=[('armor_camo2', 5.5, 0.07, 3.0, (1.0, 1.0, 0.45)), ('armor_camo4', 6.0, 0.24, 8.0, (1.0, 1.0, 0.45))],
                   skirt_low=1.2, skirt_top=0.35, hull_skirt_gap=0.05, skirt_y=(-0.72, 0.64), plate_gap=0.004, skirt_key='armor_turret',
                   track_th=0.04, front_scale=1.6, final_drives=True, brake_stepped=True, sleeve=1.7, hangers=True, 
                   turret_w=0.63, turret_l=0.68, turret_h=0.24, turret_taper=0.3, turret_front=0.28, skirt_t=0.02, skirt_lean=0.0, skirt_drop=0.03,
                   mantlet=(0.32, 0.18), gun_r=0.058, brake_r=1.7, brake_len=0.13, brake_seg=8, num_h=0.175, num_stroke=0.19, num_w=0.64, number='532', skirt_sign=(0.23, 0.16), tac_sign=(0.2, 0.17), rear_sign=(0.16, 0.11), skirt_x=0.39, skirt_lost={-1: 1, 1: 3},
                   camo_soft=0.04, turret_tone=0.74,
                   colors={'armor_edge': '#d8cca4', 'track': '#26231f'},
                   wear={'armor_paint': {'grime': 0.3, 'soot': 0.1, 'edges': 0.2, 'rust': 0.0, 'chips': 0.45,
                                         'dust': 1.0, 'dust_h': 0.5, 'exhaust': 0.7, 'exhaust_y': -0.66,
                                         'fade': 0.2, 'streaks': 0.5},
                         'armor_shade': {'chips': 0.3, 'dust': 0.8, 'dust_h': 0.4},
                         'track': {'dust': 0.25, 'dust_h': 0.25}, 'track_pin': {'dust': 0.2, 'dust_h': 0.25},
                         'rubber': {'dust': 0.7, 'dust_h': 0.3}, 'gunmetal': {'dust': 0.5, 'dust_h': 0.4},
                         'gun_olive': {'edges': 0.0, 'grime': 0.25}, 'shade_dark': {'edges': 0.0}},
                   builder=_panzer),
    # an M1A1 Abrams: desert tan with grey-brown and pale sand patches, the wedge turret, the long 120 mm
    'Modern Armor': dict(L=2.22, W=0.99, H=0.07, tw=0.2, rw=0.08, wheels=7, top=0.23, rollers=0, holes=6, hole_key='shade_dark',
                         sprocket='rear', front_wheel=(1.02, 0.15, 0.075), rear_wheel=(-1.02, 0.15, 0.078), track_th=0.04,
                         upper=dict(front=0.62, rear=0.08), nose=0.06, belly=0.06,
                         paint='#b09c72', camo2='#968460', camo3='#c2b48e',
                         camo=[('armor_camo2', 3.0, 0.2, 3.0), ('armor_camo3', 2.5, 0.45, 7.0)], camo_soft=0.08,
                         hub_key='muzzle_light', skirt_y=(-0.96, 0.98), deck_lines=True, lip_key='armor_camo2',
                         turret_dy=-0.04, t_taper=0.12, t_front=0.22, rack_posts=True, rack_l=0.26, rack_x=(-0.32, 0.0, 0.32),
                         pack_w=0.17, pack_d=0.7, cheek_lines=True, bow_dark=True, roof_rim='shade_dark',
                         skirt_panels=5, skirt_low=0.85, fit_scale=0.84, panel_gap=0.022, skirt_back=True, skirt_dirt=True,
                         turret_tone=0.98, turret_shadow=(1.06, 1.02), edge_key='muzzle_light', drv_hatch=0.085, drv_rim='shade_dark',
                         chev_rack_sides=0.12, turret_h=0.2, turret_edges=True, rack_drop=0.5, skirt_key='armor_paint', colors={'armor_edge': '#d8cca4', 'track': '#26231f', 'gun_olive': '#24231c', 'skirt_dirt': '#8c7858'},
                         antennas=0.32, hull_edges=True, chev_front=0.08, chev_rack=0.14, chev_turret=0.18, chev_skirt=0.165, chev_glacis=0.14,
                         chev_rear=0.12, plan_front=0.6, chev_kind='vee',
                         wear={'armor_paint': {'grime': 0.5, 'soot': 0.15, 'edges': 0.2, 'rust': 0.0, 'chips': 0.4,
                                               'dust': 1.0, 'dust_h': 0.45, 'exhaust': 0.7, 'exhaust_y': -0.9,
                                               'fade': 0.3, 'streaks': 0.5},
                               'armor_shade': {'chips': 0.3, 'dust': 0.8, 'dust_h': 0.4},
                               'track': {'dust': 0.25, 'dust_h': 0.25}, 'track_pin': {'dust': 0.2, 'dust_h': 0.25},
                               'rubber': {'dust': 0.6, 'dust_h': 0.3}, 'gunmetal': {'dust': 0.5, 'dust_h': 0.4},
                               'gun_olive': {'edges': 0.0, 'grime': 0.25}, 'shade_dark': {'edges': 0.0}},
                         builder=_abrams),
    # an M2A2 Bradley (ODS): house sand with hard dark-brown and olive-grey patches, bolted side armour, the 25 mm and TOW
    'Mech Infantry': dict(L=1.86, W=1.02, H=0.36, t_taper=0.3, t_front=0.45, t_rear=0.15, t_plan=0.25, turret_w=0.76,
                          turret_l=0.76, turret_h=0.18, mantlet_w=0.18, tow_key='deck_dark', turret_key='armor_turret',
                          turret_ring=1.1, tow_w=0.16, tow_h=0.16, tow_up=0.03, tow_l=0.5, tow_rim_key='muzzle_light',
                          panel_detail=True, upper_slope=True, ramp_detail=True, gun_len=0.76, tow_rims=True, flat_hatches=True,
                          deck_kit=True, tw=0.2, rw=0.085, wheels=6, rollers=0, holes=6, hole_key='shade_dark',
                          hub_key='muzzle_light', front_wheel=(0.82, 0.2, 0.085), rear_wheel=(-0.84, 0.18, 0.08), track_th=0.04,
                          upper=dict(front=0.45, rear=0.02), nose=0.4, belly=0.06,
                          paint='#cab688', camo2='#6e5c42', camo4='#7e7c66', skirt_key='skirt_dirt', dirt_f=0.6,
                          camo=[('armor_camo2', 4.5, 0.2, 5.0), ('armor_camo4', 5.0, 0.32, 13.0)], camo_soft=0.01,
                          turret_tone=0.7, colors={'armor_edge': '#d8cca4', 'track': '#26231f', 'gun_olive': '#24231c',
                                                   'skirt_dirt': '#5c4e3a'},
                          antennas=0.3, antenna_r=0.009, chev_kind='vee', chev_side=0.24, chev_turret=0.15, chev_rear=0.16, chev_glacis=0.16, chev_glacis2=False, chev_tow=0.1,
                          roof_edge_r=0.004, roof_edge_key='armor_edge', tow_rim_r=0.05, gun_r=0.03, turret_x=0.11,
                          wear={'armor_paint': {'grime': 0.5, 'soot': 0.15, 'edges': 0.25, 'rust': 0.0, 'chips': 0.4,
                                                'dust': 0.45, 'dust_h': 0.28, 'exhaust': 0.5, 'exhaust_y': 0.6,
                                                'fade': 0.3, 'streaks': 0.5},
                                'armor_shade': {'chips': 0.3, 'dust': 0.8, 'dust_h': 0.4},
                                'track': {'dust': 0.25, 'dust_h': 0.25}, 'track_pin': {'dust': 0.2, 'dust_h': 0.25},
                                'rubber': {'dust': 0.6, 'dust_h': 0.3}, 'gunmetal': {'dust': 0.5, 'dust_h': 0.4},
                                'gun_olive': {'edges': 0.0, 'grime': 0.25}, 'shade_dark': {'edges': 0.0}},
                          builder=_bradley),
    # an M48 Chaparral: house sand with hard dark-olive and field-drab bands, the cab forward, four missiles
    'Mobile SAM': dict(L=1.74, W=0.9, H=0.1, tw=0.19, rw=0.085, wheels=5, rollers=0, holes=6, hole_key='shade_dark', top=0.25,
                       hub_key='gunmetal', front_wheel=(0.78, 0.17, 0.08), rear_wheel=(-0.78, 0.16, 0.08), track_th=0.04,
                       upper=dict(front=0.0, rear=0.02), nose=0.4, belly=0.06,
                       paint='#c4b084', camo2='#5e5e44', camo4='#7a6a4e',
                       camo=[('armor_camo2', 4.5, 0.18, 6.0), ('armor_camo4', 5.0, 0.3, 1.5)], camo_soft=0.012,
                       turret_tone=0.78, colors={'armor_edge': '#d8cca4', 'track': '#26231f', 'missile_white': '#d4d0c2'},
                       chev_kind='vee', chev_cab=0.17, chev_station=0.14, chev_rear=0.11, missile_band=0.13, chev_front=0.13, cab_h=0.11, cab_front=0.5,
                       dark_sides=True, elev=46.0, missile_fwd=0.15, missile_gap=0.09, missile_r=0.04, arm_out=0.24,
                       cab_roof_key='skirt_dirt', cab_lip_key='shade_dark', cab_strip=True, mirror_r=0.016, launcher_no_fit=True, toe=5.0,
                       nose_l=0.13, cab_hatch_key='shade_dark', screen_band=True, hull_detail=True, actuators=True,
                       drum_bands=True,
                       wear={'armor_paint': {'grime': 0.5, 'soot': 0.15, 'edges': 0.25, 'rust': 0.0, 'chips': 0.4,
                                             'dust': 0.45, 'dust_h': 0.25, 'exhaust': 0.5, 'exhaust_y': 0.5,
                                             'fade': 0.3, 'streaks': 0.5},
                             'armor_shade': {'chips': 0.3, 'dust': 0.2, 'dust_h': 0.2},
                             'track': {'dust': 0.25, 'dust_h': 0.25}, 'track_pin': {'dust': 0.2, 'dust_h': 0.25},
                             'rubber': {'dust': 0.25, 'dust_h': 0.2}, 'gunmetal': {'dust': 0.5, 'dust_h': 0.4},
                             'shade_dark': {'edges': 0.0}},
                       builder=_sam),
    # an M270 MLRS: house sand with hard brown and olive-grey patches, the cab forward, the launcher box behind
    'Radar Artillery': dict(L=1.9, W=1.0, H=0.08, tw=0.2, rw=0.08, wheels=6, rollers=3, roll_r=0.03, holes=6,
                            hole_key='shade_dark', top=0.25, hub_key='gunmetal',
                            front_wheel=(0.84, 0.17, 0.08), rear_wheel=(-0.86, 0.16, 0.075), track_th=0.04,
                            upper=dict(front=0.25, rear=0.03), nose=0.45, belly=0.06,
                            paint='#c6b286', camo2='#6e5a40', camo4='#7c7a64',
                            camo=[('armor_camo2', 4.0, 0.2, 4.0), ('armor_camo4', 4.5, 0.32, 8.0)], camo_soft=0.012,
                            turret_tone=0.74, colors={'armor_edge': '#d8cca4', 'track': '#26231f', 'skirt_dirt': '#86704e'},
                            chev_kind='vee', chev_box=0.2, chev_cab=0.14, chev_rear=0.16, chev_front=0.17, chev_top=0.17, hull_seams=True,
                            box_mount=True, box_rim=False, box_lid=True, mirrors=False, box_lift=0.1, mount_block=True, pod_split=True,
                            side_seam=0.07, booms=False, dark_tube_face=True, tube_rim='shade_dark', vision_blocks=True,
                            cab_hatch_rim='gun_edge', box_elev=2.5, panes=True, mount_key='armor_turret', box_kit=True, tube_proud=0.05,
                            loader_boom=True, cab_h=0.34, louvres=2, mullions=True, front_panel=True, pod_rim=True, bore_depth=0.03, hoists=True, tubes_aft=True, cab_kit=True,
                            cab_roof_key='armor_turret',
                            wear={'armor_paint': {'grime': 0.5, 'soot': 0.15, 'edges': 0.25, 'rust': 0.0, 'chips': 0.4,
                                                  'dust': 0.45, 'dust_h': 0.25, 'exhaust': 0.5, 'exhaust_y': 0.5,
                                                  'fade': 0.3, 'streaks': 0.5},
                                  'armor_shade': {'chips': 0.3, 'dust': 0.2, 'dust_h': 0.2},
                                  'track': {'dust': 0.25, 'dust_h': 0.25}, 'track_pin': {'dust': 0.2, 'dust_h': 0.25},
                                  'rubber': {'dust': 0.25, 'dust_h': 0.2}, 'gunmetal': {'dust': 0.3, 'dust_h': 0.3},
                                  'shade_dark': {'edges': 0.0}},
                            builder=_mlrs),
}


def tracked(name, cfg, colors):
    paint = cfg.get('paint', '#7a6e4a')
    c = {'armor_paint': paint, 'armor_camo2': cfg.get('camo2', _hex(paint, 0.75)),
         'armor_camo3': cfg.get('camo3', _hex(paint, 1.15)), 'armor_camo4': cfg.get('camo4', _hex(paint, 0.6)),
         'armor_shade': _hex(paint, 0.55), 'turret_paint': cfg.get('turret_paint', '#4a4c4e')}
    c.update(cfg.get('colors', {}))
    c.update(colors)
    root = vkit.root(name)
    if cfg.get('fit_scale'):   # render_vehicles.fit multiplies its ppm by this (a bigger/smaller vehicle than the original)
        root['fit_scale'] = cfg['fit_scale']
    wear = {k: dict(v) for k, v in WEAR.items()}
    for k, v in cfg.get('wear', {}).items():   # a unit's own weathering on top (the Tank: dust and chips)
        wear.setdefault(k, {}).update(v)
    # painted camo: smooth Perlin blobs in the paint's material (props.CAMO); no face keys
    props.CAMO.clear()
    if cfg.get('camo'):
        soft = cfg.get('camo_soft')
        layers = []
        for e in cfg['camo']:   # (key, freq, threshold, seed[, stretch]): stretch draws the blobs out per axis
            k, f, th, s = e[:4]
            lay = (c[k], f * 0.55, 0.5 + 0.22 * th, s) + ((soft or 0.015,) if soft or len(e) > 4 else ())
            layers.append(lay + ((tuple(e[4]),) if len(e) > 4 else ()))
        props.CAMO['armor_paint'] = layers
        tone = cfg.get('turret_tone')   # the turret in its own, darker paint (a separate mass at 1x)
        if tone:
            c['armor_turret'] = _hex(c['armor_paint'], tone)
            props.CAMO['armor_turret'] = [(_hex(lay[0], tone),) + lay[1:] for lay in layers]
            wear['armor_turret'] = dict(wear.get('armor_paint', {}))
    p = vkit.part(root, 'hull', c, wear)
    camo = None
    d = cfg.get('chassis_fn', chassis)(p, cfg, camo)   # (opt-in: a wheeled unit's own running gear; default the tracks)
    cfg['builder'](p, cfg, d, camo)
    p.finish()
    props.CAMO.clear()
    return root


def build(unit, colors=None, **params):
    return tracked(unit, ARMOR[unit], colors or {})


UNITS = list(ARMOR)
