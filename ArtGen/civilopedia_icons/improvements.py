"""Building icons (city improvements, wonders and small wonders): each
building as a small 3D scene on a block of ground, drawn with the city
renderer (bld3d). Spaceship parts are objects in space (kit3d).

World units are the city art's (a map tile is 128 units across; a house is
about 15). Building kinds from ArtGen/cities/buildings.py are reused where
they fit."""
import math

import numpy as np

import common as C
import bld3d as B3
from bld3d import lin, plinth, water_plinth
from bld3d import PLAIN, STUCCO, WINDOWS, ROOF_TILE, THATCH, SLATE, GLASS, BRICK, METAL, DOME, STONE, WATER, FOLIAGE, WOOD, GOLD, RUBBLE, PAVING
import geom as G
import buildings as BD
import render3d as R3

ICONS = {}

P = dict(
    walls=[lin('d6c8a6'), lin('cdbd9a'), lin('ddd0b2')],
    roofs=[lin('a5543c'), lin('9a5038')],
    stone=[lin('c4baa4'), lin('bab09a')],
    marble=lin('e8e2d2'),
    dome_big=[lin('8a9ca2')],
    dome_small=[lin('c88a5a')],
    gold=lin('d8b050'),
    leaves=[lin('5f7f35'), lin('6a8a3c')],
    brick=lin('9a4e3a'),
    factory_walls=[lin('a85a44'), lin('9c5642')],
    factory_roofs=[lin('6c6c6a')],
    metal=[lin('a0a4a6')],
    hutwalls=[lin('b89a70')],
    thatch=[lin('b8a060')],
    modern_walls=[lin('cfcac0'), lin('c4c0b8')],
    glass=[lin('7d9cb0'), lin('86a4b4')],
    roofmat=ROOF_TILE,
    podium=lin('a8a294'),
    wdark=0.3,
)


def bicon(key, fill=0.86, center=(0.5, 0.53)):
    def deco(fn):
        ICONS[key] = dict(fn=fn, fill=fill, center=center)
        return fn
    return deco


# ---------------------------------------------------------------- helpers

def nb(sc, **kw):
    return sc.new_building(**kw)


def box(sc, x0, y0, x1, y1, z0, z1, col, mat=PLAIN, b=None, top=None, topmat=None):
    b = nb(sc) if b is None else b
    G.box(sc, x0, y0, x1, y1, z0, z1, col, mat, b, top=top, topmat=topmat)
    return b


def cyl(sc, cx, cy, r, z0, z1, col, mat=PLAIN, b=None, seg=20, top=None):
    b = nb(sc) if b is None else b
    G.lathe(sc, cx, cy, [(r, z0), (r, z1)], col, mat, b, seg=seg, smooth=True, cap=True)
    return b


def cone(sc, cx, cy, r, z0, h, col, mat=PLAIN, b=None, seg=20):
    b = nb(sc) if b is None else b
    G.lathe(sc, cx, cy, [(r, z0), (0.0, z0 + h)], col, mat, b, seg=seg)
    return b


def sphere(sc, cx, cy, cz, r, col, mat=PLAIN, b=None, seg=20):
    b = nb(sc) if b is None else b
    prof = [(r * math.sin(t), cz - r * math.cos(t)) for t in np.linspace(0, math.pi, 12)]
    prof[0] = (0.0, cz - r)
    prof[-1] = (0.0, cz + r)
    G.lathe(sc, cx, cy, prof, col, mat, b, seg=seg)
    return b


def seg_box(sc, a, b_, w, h, col, mat=PLAIN, bid=None, z0=0.0):
    """A wall or beam of width w and height h along the ground from a to b."""
    bid = nb(sc) if bid is None else bid
    ax, ay = a
    bx, by = b_
    dx, dy = bx - ax, by - ay
    L = math.hypot(dx, dy)
    nx, ny = -dy / L * w / 2, dx / L * w / 2
    p = [(ax + nx, ay + ny), (bx + nx, by + ny), (bx - nx, by - ny), (ax - nx, ay - ny)]
    for (x0, y0), (x1, y1) in zip(p, p[1:] + p[:1]):
        sc.quad((x0, y0, z0), (x1, y1, z0), (x1, y1, z0 + h), (x0, y0, z0 + h), col, mat, bid)
    sc.quad(*[(x, y, z0 + h) for x, y in p], col, mat, bid)
    return bid


def beam(sc, p0, p1, r, col, mat=METAL, bid=None, seg=6):
    """A thin round bar between two 3D points."""
    bid = nb(sc) if bid is None else bid
    p0, p1 = np.array(p0, float), np.array(p1, float)
    d = p1 - p0
    L = np.linalg.norm(d)
    d /= L
    u = np.cross(d, [0, 0, 1.0])
    if np.linalg.norm(u) < 1e-6:
        u = np.array([1.0, 0, 0])
    u /= np.linalg.norm(u)
    v = np.cross(d, u)
    ring = [u * math.cos(2 * math.pi * k / seg) + v * math.sin(2 * math.pi * k / seg) for k in range(seg)]
    for k in range(seg):
        a, b = ring[k] * r, ring[(k + 1) % seg] * r
        sc.quad(tuple(p0 + a), tuple(p0 + b), tuple(p1 + b), tuple(p1 + a), col, mat, bid, (tuple(a), tuple(b), tuple(b), tuple(a)))
    return bid


def flag(sc, x, y, z, h, col, w=7.0, fh=4.5):
    b = nb(sc)
    G.lathe(sc, x, y, [(0.35, z), (0.3, z + h)], lin('5a5048'), WOOD, b, seg=6)
    sc.quad((x, y, z + h - fh), (x + w * 0.7, y - w * 0.7, z + h - fh), (x + w * 0.7, y - w * 0.7, z + h), (x, y, z + h), col, PLAIN, b)
    return b


def statue(sc, folder, at, height, color='#a8743e', **kw):
    """A unit's model cast in bronze (or stone) standing at `at`, `height`
    world units tall: drawn over the scene by make()."""
    if not hasattr(sc, 'statues'):
        sc.statues = []
    sc.statues.append(dict(folder=folder, at=at, height=height, color=color, **kw))


def tree(sc, x, y, h, rng, palm=False):
    BD.tree(sc, x, y, h, P, rng, palm=palm)


def columns(sc, x0, y0, x1, y1, z0, z1, r, n, col=None):
    G.column_row(sc, x0, y0, x1, y1, z0, z1, r, n, col or P['marble'], PLAIN, nb(sc))


# ---------------------------------------------------------------- making the icons

BACKDROPS = {
    'improvement': ('c4cfd3', 'e8e4d6'),
    'wonder': ('e9d7a6', 'f1ead4'),
    'small_wonder': ('d2d2c4', 'ece8da'),
}


def backdrop(n, kind):
    top, bot = BACKDROPS[kind]
    rgb = C.vgradient(n, C.hex_rgb(top), C.hex_rgb(bot))
    rgb = C.vignette(rgb, 0.14)
    return np.concatenate([rgb, np.ones((n, n, 1), np.float32)], -1)


def scene_of(key):
    spec = ICONS[key]
    sc = R3.Scene()
    rng = np.random.default_rng(sum(map(ord, key)))
    ground = spec['fn'](sc, rng)
    return sc, spec, (ground if ground is not None else -4.0)


def make(building, kind='improvement'):
    key = building['civilopediaEntry']
    spec = ICONS[key]
    if spec.get('space'):
        return spec['fn'](None, None)
    sc, spec, gz = scene_of(key)
    statues = getattr(sc, 'statues', [])
    extra = []
    for s in statues:
        x, y, z = s['at']
        extra += [(x, y, z), (x, y, z + s['height'] * 1.1)]
    out = []
    for n, f, ss in ((C.LARGE, spec['fill'], 3), (C.SMALL * 2, min(0.95, spec['fill'] * 1.06), 3)):
        zoom, cx, cy = B3.fit(sc, n, f, spec['center'], extra)
        img = B3.render_zoom(sc, n, n, cx, cy, zoom, ss, ground_z=gz)
        for s in statues:
            import units
            ax, ay = B3.project(s['at'], zoom, cx, cy)
            # a figure 1.8 m tall is s['height'] world units
            ppm = zoom * s['height'] / 1.8
            fig = units.render_statue(s['folder'], ppm, (ax, ay), n, n, s.get('color', '#a8743e'), yaw=s.get('yaw', 150.0),
                                      spec=s.get('spec', 0.5), action=s.get('action', 'DEFAULT'))
            img = C.over(fig, img)
        img = C.over(img, backdrop(n, kind))
        img[..., :3] = C.frame_edges(img[..., :3])
        out.append(img)
    return out[0], C.sharpen(C.downsample(out[1], 2), 0.35, 0.5)


def space(key):
    """Registers a spaceship part drawn with kit3d: fn(kit)."""
    def deco(fn):
        def make_part(_sc, _rng):
            import kit3d as K
            k = K.Kit()
            fn(k)
            return K.compose(k, '3c4a66', '10141e', yaw=-28, pitch=8, fill=0.8)
        ICONS[key] = dict(fn=make_part, space=True)
        return fn
    return deco


# The buildings themselves (each module registers its icons).
import bld_improvements  # noqa: E402,F401
import bld_wonders  # noqa: E402,F401
import bld_space  # noqa: E402,F401
import bld_mdj  # noqa: E402,F401


