"""Government icons: a symbolic object per government (kit3d) on a grey
backdrop."""
import math

import numpy as np

import kit3d as K
import mesh as M

BACKDROP = ('e4e2dc', '8c8a84')
ICONS = {}


def icon(key, yaw=-20.0, pitch=0.0, fill=0.8, ground=False):
    def deco(fn):
        ICONS[key] = dict(fn=fn, yaw=yaw, pitch=pitch, fill=fill, ground=ground)
        return fn
    return deco


def make(gov):
    spec = ICONS[gov['civilopediaEntry']]
    k = K.Kit()
    spec['fn'](k)
    return K.compose(k, BACKDROP[0], BACKDROP[1], spec['yaw'], spec['pitch'], spec['fill'], spec['ground'])


def _link(k, c, upright, mat='iron', gap=None, tilt=0.0):
    """An oval chain link (long axis along x) centered at c: upright (in the
    x-z plane, facing the camera) or lying flat (in the x-y plane). gap:
    (from, to) angles in degrees left open (a broken link)."""
    T = M.translate(*c) @ M.rot_y(tilt) @ (M.rot_x(90) if upright else np.eye(4, dtype=np.float32))
    a0, a1 = (0, 360) if gap is None else (gap[1], gap[0] + 360)
    n = 28
    pts = []
    for i in range(n + 1):
        a = math.radians(a0 + (a1 - a0) * i / n)
        p = T @ np.array([0.42 * math.cos(a), 0.24 * math.sin(a), 0, 1])
        pts.append(p[:3])
    for p0, p1 in zip(pts[:-1], pts[1:]):
        k.cyl(p0, p1, 0.075, mat, seg=12)
        k.sph(p1, 0.075, mat, seg=12)
    if gap is not None:
        k.sph(pts[0], 0.075, mat, seg=12)


@icon('GOVT_Anarchy', yaw=-8, pitch=8)
def _(k):
    # a broken chain: its links pulled apart
    _link(k, (-1.15, 0, 0.3), True, tilt=-20)
    _link(k, (-0.62, 0, 0.12), False, tilt=-20)
    _link(k, (-0.12, 0, -0.05), True, gap=(-20, 25), tilt=-20)
    _link(k, (0.6, 0, -0.08), True, gap=(160, 200), tilt=15)
    _link(k, (1.12, 0, 0.08), False, tilt=15)
    spark = k.m('spark', '#f0b040', spec=0.0)
    for (x, z, r) in ((0.25, 0.25, 0.04), (0.3, -0.3, 0.03), (0.18, 0.05, 0.05), (0.35, 0.08, 0.03)):
        k.sph((x, -0.2, z), r, spark, seg=8)


@icon('GOVT_Despotism', yaw=-20, pitch=4)
def _(k):
    # a flail: a spiked iron ball on a chain
    k.cyl((-0.9, 0, -0.9), (-0.25, 0, -0.1), 0.07, 'dark_wood')
    k.cyl((-0.3, 0, -0.16), (-0.18, 0, -0.02), 0.1, 'iron')
    pts = [np.array((-0.18, 0, -0.02))]
    for i in range(1, 6):
        t = i / 5
        pts.append(np.array((-0.18 + 0.55 * t, 0, -0.02 + 0.5 * t - 0.25 * math.sin(math.pi * t))))
    for p0, p1 in zip(pts[:-1], pts[1:]):
        k.torus(0.05, 0.018, 'iron', M.translate(*(p0 + p1) / 2) @ M.rot_y(30), seg=12, tube=6)
    c = np.array((0.55, 0, 0.42))
    k.sph(c, 0.3, 'iron', seg=24)
    rng = np.random.default_rng(3)
    for i in range(16):
        v = rng.normal(size=3)
        v /= np.linalg.norm(v)
        k.add(M.cone(0.07, 0.2, k.m('steel'), seg=8), M.look_along(c + v * 0.27, c + v * 0.47))


@icon('GOVT_Monarchy', yaw=-28, pitch=4, ground=True)
def _(k):
    # a throne
    gold, red = 'gold', 'dark_red'
    k.box((1.0, 0.9, 0.12), 'dark_wood', center=(0, 0, 0.06))
    k.box((0.9, 0.8, 0.45), gold, center=(0, 0, 0.35), bevel=0.02)
    k.box((0.82, 0.72, 0.1), red, center=(0, -0.02, 0.62), bevel=0.04)
    k.box((0.9, 0.16, 1.5), gold, center=(0, 0.38, 1.1), bevel=0.02)
    k.box((0.66, 0.05, 1.1), red, center=(0, 0.29, 1.08))
    k.extrude([(-0.45, 0), (0.45, 0), (0.3, 0.2), (0, 0.42), (-0.3, 0.2)], 0.16, gold, M.translate(0, 0.38, 1.85))
    for s in (-1, 1):
        k.box((0.1, 0.8, 0.12), gold, center=(s * 0.45, 0, 0.85))
        k.box((0.1, 0.1, 0.3), gold, center=(s * 0.45, -0.35, 0.7))
        k.sph((s * 0.45, -0.38, 0.92), 0.07, gold)
    k.sph((0, 0.4, 2.3), 0.08, 'red')


@icon('GOVT_Communism', yaw=-10, pitch=8)
def _(k):
    # a hammer and sickle on a red disc
    n = 240
    im, d = K.canvas(n)
    d.ellipse((6, 6, n - 6, n - 6), fill=255)
    disc = K.mask_of(im)
    yy, xx = np.mgrid[0:n, 0:n] + 0.5
    # the sickle: a crescent blade opening to the upper right, and its handle
    outer = (xx - 118) ** 2 + (yy - 112) ** 2 < 72 ** 2
    inner = (xx - 134) ** 2 + (yy - 96) ** 2 < 62 ** 2
    ang = np.degrees(np.arctan2(yy - 112, xx - 118))
    blade = outer & ~inner & ~((ang > -75) & (ang < 10))
    s, ds = K.canvas(n)
    ds.polygon([(66, 156), (80, 170), (52, 200), (38, 186)], fill=255)
    # the hammer: a handle from the lower right up to a head at the upper left
    h, dh = K.canvas(n)
    dh.polygon([(178, 190), (190, 178), (98, 86), (86, 98)], fill=255)
    dh.polygon([(62, 98), (98, 62), (116, 80), (80, 116)], fill=255)
    sym = (blade | K.mask_of(s) | K.mask_of(h)) & disc
    k.relief([(disc, 'dark_red', 0.08, 4.0), (sym, 'gold', 0.08, 3.0)], 2.0)


@icon('GOVT_Republic', yaw=-10, pitch=8)
def _(k):
    # a laurel wreath
    n = 240
    im, d = K.canvas(n)
    c = n / 2
    def leaf(x, y, ang, L=22, W=8):
        pts = []
        for i in range(13):
            t = i / 12
            w = W * math.sin(math.pi * t)
            pts.append((t * L, w))
        pts += [(t * L, -W * math.sin(math.pi * t)) for t in (i / 12 for i in range(12, -1, -1))]
        ca, sa = math.cos(ang), math.sin(ang)
        d.polygon([(x + px * ca - py * sa, y + px * sa + py * ca) for px, py in pts], fill=255)

    R0 = 84
    for side in (-1, 1):
        # the stem: an arc up each side, from the bottom
        for i in range(10):
            t = i / 9
            a = math.pi / 2 + side * (0.25 + t * 2.4)   # screen angle (y down) from the bottom center
            x, y = c + R0 * math.cos(a), c + 6 + R0 * math.sin(a)
            tang = a + side * math.pi / 2                 # direction along the stem, upward
            d.ellipse((x - 5, y - 5, x + 5, y + 5), fill=255)
            for off in (-1, 1):
                leaf(x, y, tang + off * 0.75)
    wreath = K.mask_of(im)
    r, dr = K.canvas(n)
    dr.polygon([(c - 18, c + 92), (c + 18, c + 92), (c + 34, c + 120), (c + 12, c + 104), (c - 12, c + 104), (c - 34, c + 120)], fill=255)
    k.relief([(wreath, 'gold', 0.14, 5.0), (K.mask_of(r), 'red', 0.08, 3.0)], 2.0)


@icon('GOVT_Democracy', yaw=-24, pitch=4, ground=True)
def _(k):
    # a domed parliament
    k.box((2.2, 1.1, 0.12), 'marble', center=(0, 0, 0.06))
    k.box((2.0, 0.9, 0.55), 'marble', center=(0, 0, 0.4))
    for i in range(8):
        x = -0.84 + i * 0.24
        k.cyl((x, -0.5, 0.12), (x, -0.5, 0.68), 0.045, 'white', seg=10)
    k.box((2.05, 0.95, 0.08), 'white', center=(0, 0, 0.72))
    k.cyl((0, 0, 0.72), (0, 0, 1.05), 0.48, 'marble', seg=36)
    for i in range(12):
        a = 2 * math.pi * i / 12
        k.cyl((0.5 * math.cos(a), 0.5 * math.sin(a), 0.75), (0.5 * math.cos(a), 0.5 * math.sin(a), 1.05), 0.03, 'white', seg=8)
    k.lathe([(0, 0.46)] + [(0.5 * math.sin(math.pi / 2 * i / 10), 0.46 * math.cos(math.pi / 2 * i / 10)) for i in range(1, 11)],
            k.m('dome', '#d8d4c6', spec=0.3), M.translate(0, 0, 1.05), seg=40)
    k.cyl((0, 0, 1.53), (0, 0, 1.72), 0.07, 'white', seg=12)
    k.cyl((0, 0, 1.72), (0, 0, 1.95), 0.01, 'dark_metal')
    k.box((0.25, 0.01, 0.14), 'blue', center=(0.13, 0, 1.88))


@icon('GOVT_Fascism', yaw=-25, pitch=4, ground=True)
def _(k):
    # loudspeakers on a pole
    k.cyl((0, 0, 0), (0, 0, 1.6), 0.06, 'dark_metal')
    k.cyl((0, 0, 0), (0, 0, 0.1), 0.3, 'dark_metal')
    for (yaw, z) in ((112, 1.42), (-112, 1.42), (165, 1.08)):
        T = M.translate(0, 0, z) @ M.rot_z(yaw) @ M.rot_x(-80)
        k.lathe([(0.0, 0.06), (0.2, 0.08), (0.55, 0.32), (0.58, 0.33)], k.m('horn', '#7a7e70', spec=0.3), T @ M.translate(0, 0, 0.05),
                seg=28)
        k.lathe([(0.0, 0.1), (0.1, 0.12), (0.12, 0.001)], 'dark_metal', T, seg=16)


@icon('GOVT_Feudalism', yaw=-12, pitch=6)
def _(k):
    # a heraldic shield over two crossed swords
    for s in (-1, 1):
        a = np.array((s * 0.85, 0.15, -0.95))
        b = np.array((-s * 0.7, 0.15, 0.85))
        d = (b - a) / np.linalg.norm(b - a)
        k.add(M.blade(1.75, 0.12, 0.03, k.m('steel')), M.look_along(a + d * 0.35, a + d * 1.35))
        k.cyl(a, a + d * 0.32, 0.035, 'leather')
        g = a + d * 0.33
        perp = np.array((d[2], 0, -d[0]))
        k.cyl(g - perp * 0.18, g + perp * 0.18, 0.035, 'gold')
        k.sph(a - d * 0.02, 0.06, 'gold')
    n = 240
    im, d = K.canvas(n)
    d.polygon([(40, 30), (200, 30), (200, 120), (180, 170), (120, 220), (60, 170), (40, 120)], fill=255)
    shield = K.mask_of(im)
    im2, d2 = K.canvas(n)
    d2.polygon([(40, 150), (120, 70), (200, 150), (200, 186), (120, 106), (40, 186)], fill=255)
    chev = K.mask_of(im2) & shield
    k.relief([(shield, 'blue', 0.12, 4.0), (chev, 'gold', 0.04, 2.0)], 1.4, M.translate(0, -0.05, 0.0))
