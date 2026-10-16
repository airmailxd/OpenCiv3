"""Armored vehicles for the Blender remaster: Tank, Panzer, Modern Armor,
Mech Infantry, Mobile SAM, Radar Artillery.

    root = armor.build('Tank', colors)

A config-driven tracked vehicle: a hull with a sloped glacis and engine
deck, two track runs (road wheels, drive sprocket, idler, track plates)
or side skirts over them, a turret with its gun and mantlet, hatches,
machine guns, stowage, and Civ3's civ markings (stars, crosses, chevrons,
a turret ring band, side patches). Model space as every unit: facing +Y,
ground at z = 0. Camouflage is face-keyed over subdivided panels.
"""
import math

import bpy
from mathutils import Matrix, Vector

import props
import vkit
from props import R, T

props.DEFAULTS.setdefault('armor_paint', ('#7a6e4a', 0.7, 0.3, 'metal'))
props.DEFAULTS.setdefault('armor_camo2', ('#5a5236', 0.7, 0.3, 'metal'))
props.DEFAULTS.setdefault('armor_camo3', ('#9a8e64', 0.7, 0.3, 'metal'))
props.DEFAULTS.setdefault('track', ('#2e2c2a', 0.6, 0.8, 'metal'))
props.DEFAULTS.setdefault('rubber', ('#1e1e1e', 0.85, 0.0, 'leather'))
props.DEFAULTS.setdefault('gunmetal', ('#3a3c3e', 0.45, 1.0, 'metal'))
props.DEFAULTS.setdefault('missile_white', ('#e8e6e0', 0.5, 0.2, 'metal'))
props.DEFAULTS.setdefault('missile_red', ('#a8302a', 0.5, 0.2, 'metal'))
WEAR = {'armor_paint': {'grime': 0.5, 'soot': 0.25, 'edges': 0.5, 'rust': 0.08}, 'armor_camo2': {'grime': 0.5},
        'armor_camo3': {'grime': 0.45}, 'track': {'grime': 0.7, 'rust': 0.2}, 'rubber': {'grime': 0.6},
        'gunmetal': {'grime': 0.5, 'soot': 0.4}}


def _sub(bm, cuts=5):
    props.bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=cuts, use_grid_fill=True)
    return bm


def _camo(kind, cell=0.28):
    """Face keys for the vehicle's paint: None (plain), 'blotch' (two-tone
    blotches) or 'desert' (tan with brown and pale patches)."""
    if not kind:
        return None

    def keys(f):
        c = f.calc_center_median()
        n = math.sin(c.x / cell * 1.7 + 0.6) + math.sin(c.y / cell * 1.3 + 1.1) + math.sin(c.z / cell * 2.3 + c.y * 3)
        if n > 0.8:
            return ('armor_camo2', False)
        if n < -0.9:
            return ('armor_camo3', False)
        return None
    return keys


def _star(p, at, normal_axis, size):
    """A five-pointed civ star painted on a flat face (normal along +-X or +Z)."""
    x, y, z = at
    pts = []
    for i in range(10):
        a = math.pi / 2 + i * math.pi / 5
        r = size if i % 2 == 0 else size * 0.4
        pts.append((r * math.cos(a), r * math.sin(a)))
    if normal_axis in ('x', '-x'):
        star = props.slab(pts, 0.004, 'X')   # outline in (y, z)
        p.add(star, 'tint', T(x, y, z), tint=True)
    else:
        star = props.slab([(u, v) for u, v in pts], 0.004, 'Y')   # outline in (x, z), turned flat
        p.add(star, 'tint', T(x, y, z) @ R('X', 90), tint=True)


def _cross(p, at, size):
    """A civ cross (Balkenkreuz-like) on a hull side (normal along X)."""
    x, y, z = at
    for w, h in ((size * 0.28, size), (size, size * 0.28)):
        p.add(props.box(0.004, w, h), 'tint', T(x, y, z), tint=True)


def _chevron(p, at, size, side):
    """A civ chevron ('^') painted on a turret side (normal along X)."""
    x, y, z = at
    for sy in (-1, 1):
        p.add(props.box(0.004, size * 0.7, size * 0.18), 'tint', T(x, y + sy * size * 0.24, z) @ R('X', sy * 35), tint=True)


def _tracks(p, L, W, wheel_r, n_wheels, skirt=False, track_w=0.16):
    """Both track runs: a loop of plates round the road wheels, sprocket and
    idler; optionally armored side skirts hiding the upper run."""
    for sx in (-1, 1):
        x = sx * (W / 2 - track_w / 2)
        # the track loop (an elongated stadium in the YZ plane)
        pts = []
        r = wheel_r * 1.05
        y0, y1 = -L / 2 + r, L / 2 - r
        for i in range(25):
            a = math.pi / 2 + math.pi * i / 24
            pts.append((y0 + r * math.cos(a), r + r * math.sin(a)))
        for i in range(25):
            a = -math.pi / 2 + math.pi * i / 24
            pts.append((y1 + r * math.cos(a), r + r * math.sin(a)))
        loop = [(x, py, pz) for py, pz in pts] + [(x, pts[0][0], pts[0][1])]
        p.add(props.tube(loop, track_w / 2, seg=4, scale_xy=(1.0, 0.18)), 'track', smooth=False)
        for i in range(n_wheels):   # road wheels with rubber tyres
            y = y0 + (y1 - y0) * i / max(1, n_wheels - 1)
            p.add(props.lathe([(0.0, wheel_r * 0.95), (track_w * 0.8, wheel_r * 0.95)], seg=16), 'rubber',
                  T(x - track_w * 0.4, y, wheel_r) @ R('Y', 90), smooth=True)
            p.add(props.ellipsoid(wheel_r * 0.5, wheel_r * 0.5, 0.02, seg=12), 'gunmetal',
                  T(x + sx * track_w * 0.42, y, wheel_r) @ R('Y', 90), smooth=True)
        for y in (y0 - r * 0.1, y1 + r * 0.1):   # sprocket and idler, a little raised
            p.add(props.lathe([(0.0, wheel_r * 0.8), (track_w * 0.8, wheel_r * 0.8)], seg=14), 'gunmetal',
                  T(x - track_w * 0.4, y, r * 1.1) @ R('Y', 90), smooth=True)
        if skirt:
            p.add(props.box(0.02, L * 0.92, wheel_r * 1.3), 'armor_paint', T(sx * (W / 2 + 0.012), 0, wheel_r * 1.35), bevel=0.006)


def tracked(name, cfg, colors):
    c = dict({'armor_paint': cfg.get('paint', '#7a6e4a'), 'armor_camo2': cfg.get('camo2', '#5a5236'),
              'armor_camo3': cfg.get('camo3', '#9a8e64')}, **colors)
    root = vkit.root(name)
    p = vkit.part(root, 'hull', c, WEAR)
    L, W, H = cfg['L'], cfg['W'], cfg['H']
    wheel_r = cfg.get('wheel_r', 0.12)
    camo = _camo(cfg.get('camo'))
    _tracks(p, L, W, wheel_r, cfg.get('wheels', 5), skirt=cfg.get('skirt', False))
    z0 = wheel_r * 1.4
    # the hull: a box with a sloped glacis at the front and a lower engine deck behind
    hull = _sub(props.box(W - 0.32, L * 0.96, H), 4)
    for v in hull.verts:
        if v.co.y > L * 0.2 and v.co.z > 0:
            v.co.y -= (v.co.y - L * 0.2) * cfg.get('glacis', 0.5) * (v.co.z / (H / 2))
    p.add(hull, 'armor_paint', T(0, 0, z0 + H / 2), bevel=0.02, keys=camo)
    sponson = _sub(props.box(W, L * 0.88, H * 0.42), 3)   # over the tracks
    p.add(sponson, 'armor_paint', T(0, -L * 0.02, z0 + H * 0.8), bevel=0.015, keys=camo)
    deck_z = z0 + H
    # the turret: a cast dome, a boxy turret or a mount, its gun with the mantlet
    tz = deck_z + cfg.get('turret_z', 0.0)
    ty = cfg.get('turret_y', 0.0)
    kind = cfg.get('turret', 'dome')
    tr = cfg.get('turret_r', 0.28)
    if kind == 'dome':
        tur = _sub(props.ellipsoid(tr, tr * 1.1, tr * 0.62, seg=20), 1)
        p.add(tur, 'armor_paint', T(0, ty, tz + tr * 0.3), smooth=True, keys=camo)
    elif kind == 'box':
        tur = _sub(props.box(tr * 2, tr * 2.3, tr * 0.7), 3)
        for v in tur.verts:   # sloped faces
            if v.co.z > 0:
                v.co.x *= 0.85
                if v.co.y > 0:
                    v.co.y *= 0.8
        p.add(tur, 'armor_paint', T(0, ty, tz + tr * 0.35), bevel=0.02, keys=camo)
    if kind in ('dome', 'box'):
        if cfg.get('turret_ring'):   # the civ band round the turret
            p.add(props.lathe([(0.0, tr * 1.02), (0.05, tr * 1.02)], seg=24), 'tint', T(0, ty, tz + 0.02), smooth=True, tint=True)
        gl = cfg.get('gun_len', 0.9)
        p.add(props.box(tr * 0.7, 0.1, tr * 0.42), 'armor_paint', T(0, ty + tr * 1.05, tz + tr * 0.32), bevel=0.02)  # mantlet
        p.add(props.lathe([(0.0, cfg.get('gun_r', 0.035)), (gl, cfg.get('gun_r', 0.035) * 0.8)], seg=12), 'armor_paint',
              T(0, ty + tr * 1.05, tz + tr * 0.32) @ R('X', -90 + cfg.get('gun_elev', 3)), smooth=True)
        if cfg.get('muzzle'):
            p.add(props.lathe([(0.0, 0.045), (0.08, 0.045)], seg=12), 'gunmetal',
                  T(0, ty + tr * 1.05 + gl, tz + tr * 0.32) @ R('X', -90), smooth=True)
        # the commander's hatch and a machine gun on the roof
        p.add(props.lathe([(0.0, 0.07), (0.04, 0.065)], seg=14), 'armor_paint', T(-tr * 0.35, ty - tr * 0.2, tz + tr * 0.62), smooth=True)
        vkit.rod(p, (tr * 0.3, ty - tr * 0.1, tz + tr * 0.66), (tr * 0.3, ty + tr * 0.5, tz + tr * 0.7), 0.012, 'gunmetal', seg=6)
        if cfg.get('antenna', True):
            vkit.rod(p, (-tr * 0.5, ty - tr * 0.7, tz + tr * 0.6), (-tr * 0.55, ty - tr * 0.8, tz + tr * 0.6 + 0.6), 0.004, 'gunmetal', seg=4)
    # stowage: a box and jerrycans on the engine deck, tools on the sponsons
    p.add(props.box(W * 0.5, 0.16, 0.08), 'armor_camo2', T(0, -L * 0.42, deck_z + 0.04), bevel=0.02)
    for sx in (-1, 1):
        vkit.rod(p, (sx * W * 0.46, -L * 0.2, deck_z - H * 0.15), (sx * W * 0.46, L * 0.2, deck_z - H * 0.15), 0.01, 'wood', seg=6)
    # civ markings
    mk = cfg.get('marks', ())
    for sx in (-1, 1):
        xs = sx * (W / 2 + 0.006)
        if 'star' in mk:
            _star(p, (xs, -L * 0.05, z0 + H * 0.8), 'x', 0.09)
        if 'cross' in mk:
            _cross(p, (xs, -L * 0.1, z0 + H * 0.8), 0.14)
        if 'chevron' in mk and kind in ('dome', 'box'):
            _chevron(p, (sx * tr * 0.95, ty, tz + tr * 0.35), 0.16, sx)
        if 'side_patch' in mk:
            p.add(props.box(0.006, 0.2, 0.08), 'tint', T(xs, L * 0.3, z0 + H * 0.8), tint=True)
    if 'star' in mk:
        _star(p, (0, L * 0.38, deck_z + 0.01), 'z', 0.1)
    if 'deck_patch' in mk:
        p.add(props.box(W * 0.5, 0.18, 0.006), 'tint', T(0, -L * 0.3, deck_z + 0.004), tint=True)
    for extra in cfg.get('extras', []):
        extra(p, cfg, deck_z, c)
    p.finish()
    return root


def _sam_extras(p, cfg, deck_z, c):
    """Mobile SAM: a launcher rail on a turntable, four white missiles with red
    nose bands and civ fins, raised at an angle."""
    m = T(0, -0.1, deck_z + 0.18) @ R('X', 35)
    p.add(props.lathe([(0.0, 0.2), (0.08, 0.2)], seg=20), 'armor_paint', T(0, -0.1, deck_z), smooth=True)
    p.add(props.box(0.5, 0.12, 0.14), 'armor_paint', m, bevel=0.02)
    for i, (x, z) in enumerate(((-0.14, 0.11), (0.14, 0.11), (-0.14, -0.05), (0.14, -0.05))):
        mm = m @ T(x, 0.0, z)
        p.add(props.lathe([(-0.55, 0.035), (0.45, 0.035), (0.55, 0.0)], seg=12), 'missile_white', mm @ R('X', -90), smooth=True)
        p.add(props.lathe([(0.0, 0.037), (0.06, 0.037)], seg=12), 'missile_red', mm @ T(0, 0.38, 0) @ R('X', -90), smooth=True)
        for a in (0, 90):
            p.add(props.box(0.005, 0.1, 0.1), 'tint', mm @ T(0, -0.5, 0) @ R('Y', a), tint=True)


def _mlrs_extras(p, cfg, deck_z, c):
    """Radar Artillery (MLRS): the cab forward, a big boxy launcher pod with a
    grid of tube ends at the rear, a civ chevron on the pod."""
    p.add(props.box(0.62, 0.42, 0.26), 'armor_paint', T(0, 0.62, deck_z + 0.13), bevel=0.02)   # the cab
    p.add(props.box(0.5, 0.02, 0.1), 'port', T(0, 0.835, deck_z + 0.18))                       # its windows
    pod = T(0, -0.3, deck_z + 0.24) @ R('X', 12)
    p.add(props.box(0.62, 0.95, 0.4), 'armor_paint', pod, bevel=0.025)
    for i in range(3):
        for j in range(2):
            p.add(props.lathe([(0.0, 0.045), (0.02, 0.045)], seg=10), 'gunmetal',
                  pod @ T(-0.2 + 0.2 * i, -0.48, -0.08 + 0.16 * j) @ R('X', 90), smooth=True)
    for sx in (-1, 1):
        _chevron(p, (sx * 0.315, -0.3, deck_z + 0.3), 0.22, sx)


def _ifv_extras(p, cfg, deck_z, c):
    """Mech Infantry (IFV): a small two-man turret offset left with a long
    autocannon and a box launcher, the rear troop door."""
    p.add(props.box(0.4, 0.42, 0.18), 'armor_paint', T(-0.08, 0.05, deck_z + 0.09), bevel=0.02)
    p.add(props.lathe([(0.0, 0.025), (0.7, 0.02)], seg=10), 'armor_paint', T(-0.08, 0.27, deck_z + 0.12) @ R('X', -88), smooth=True)
    p.add(props.box(0.12, 0.24, 0.1), 'armor_paint', T(0.18, 0.05, deck_z + 0.14), bevel=0.01)   # the launcher box
    p.add(props.box(0.4, 0.02, 0.24), 'armor_camo2', T(0, -cfg['L'] / 2 * 0.97, deck_z - 0.1), bevel=0.01)   # rear door
    for sx in (-1, 1):
        p.add(props.box(0.006, 0.14, 0.06), 'tint', T(sx * 0.21, 0.05, deck_z + 0.12), tint=True)


ARMOR = {
    # an olive-tan Sherman: a cast dome turret with a civ ring, civ stars on the hull sides and deck
    'Tank': dict(L=1.9, W=0.95, H=0.34, wheel_r=0.12, wheels=6, glacis=0.55, turret='dome', turret_r=0.32,
                 turret_y=0.05, gun_len=0.85, paint='#8a7a4e', camo2='#6a5e3a', turret_ring=True,
                 marks=('star', 'deck_patch')),
    # a Panzer IV in winter camo: a boxy turret, side skirts, civ crosses and side marks
    'Panzer': dict(L=2.0, W=0.98, H=0.34, wheel_r=0.1, wheels=8, glacis=0.3, turret='box', turret_r=0.28,
                   turret_y=0.05, gun_len=1.15, muzzle=True, skirt=True, camo='blotch',
                   paint='#a8a890', camo2='#6a6e52', camo3='#d0d0c0', marks=('cross', 'side_patch')),
    # an Abrams in desert camo: a long flat angular turret, a long gun, skirts, civ chevrons
    'Modern Armor': dict(L=2.3, W=1.1, H=0.3, wheel_r=0.11, wheels=7, glacis=0.7, turret='box', turret_r=0.38,
                         turret_y=-0.05, gun_len=1.35, gun_r=0.04, skirt=True, camo='blotch',
                         paint='#c0aa7a', camo2='#8a7450', camo3='#d8c8a0', marks=('chevron', 'side_patch')),
    # a Bradley-type IFV in desert camo
    'Mech Infantry': dict(L=2.1, W=1.05, H=0.42, wheel_r=0.1, wheels=6, glacis=0.5, turret='none',
                          skirt=True, camo='blotch', paint='#c0aa7a', camo2='#8a7450', camo3='#d8c8a0',
                          marks=('side_patch', 'deck_patch'), extras=[_ifv_extras]),
    # a tracked SAM carrier in olive
    'Mobile SAM': dict(L=1.9, W=0.95, H=0.36, wheel_r=0.1, wheels=5, glacis=0.4, turret='none',
                       paint='#5a5e48', camo2='#44483a', marks=('side_patch',), extras=[_sam_extras]),
    # an MLRS in desert tan
    'Radar Artillery': dict(L=2.0, W=0.98, H=0.3, wheel_r=0.1, wheels=6, glacis=0.2, turret='none',
                            camo='blotch', paint='#c4a46e', camo2='#9a7e50', camo3='#dcc898', marks=(),
                            extras=[_mlrs_extras]),
}

props.DEFAULTS.setdefault('port', ('#0e0c0a', 0.9, 0.0, 'polished'))


def build(unit, colors=None, **params):
    return tracked(unit, ARMOR[unit], colors or {})


UNITS = list(ARMOR)
