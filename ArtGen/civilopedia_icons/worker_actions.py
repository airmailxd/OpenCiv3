"""Worker action icons: a tile of the remade map (see landscape.diorama)
with the improvement the action makes, and the worker (its 3D model, in the
labor pose the map shows) at work on it."""
import numpy as np

import common as C
import landscape as L
from landscape import Patch, Tile
import units as U

# The worker's size on the tile (pixels of the 2x art per meter).
WORKER_PPM = 62.0


def _worker(action, t, at=(-70, 22), yaw=150.0, folder='Worker'):
    def draw(cv, c):
        W = H = 320
        ax, ay = c[0] + at[0], c[1] + at[1]
        img = U.render_unit_at(folder, action, t, WORKER_PPM, (W / 2, H * 0.8), W, H, yaw=yaw)
        cv.paste(img, ax - W / 2, ay - H * 0.8)
    return draw


def _then(*fns):
    def draw(cv, c):
        for f in fns:
            if f is not None:
                f(cv, c)
    return draw


def _scene_sprite(build, W=360, H=300, oy=0.62):
    """A small 3D object made with the city renderer (ArtGen/cities), in 2x
    art pixels, pasted with its ground origin on the tile's center."""
    import render3d
    sc = render3d.Scene()
    build(sc)
    img = render3d.render(sc, W, H, W / 2, H * oy, ss=4)

    def draw(cv, c):
        cv.paste(img.astype(np.float32), c[0] - W / 2, c[1] - H * oy)
    return draw


def _lin(h):
    return tuple(C.hex_rgb(h))


def _airfield(sc):
    import render3d as R3
    from geom import box, lathe, cylinder
    bid = sc.new_building(wspace=6, floor=8)
    # the runway, corner to corner, with a taxiway apron
    asphalt, mark = _lin('6e6e68'), _lin('d8d6cc')
    w = 13
    for (x0, y0, x1, y1, col) in [(-62, -w, 62, w, asphalt), (8, w, 30, 40, asphalt)]:
        box(sc, x0, y0, x1, y1, 0, 0.8, col, R3.PAVING, bid)
    for k in range(-5, 6):
        box(sc, k * 10 - 3, -1.2, k * 10 + 3, 1.2, 0.8, 0.95, mark, R3.PLAIN, bid)
    # a round-roofed hangar
    b2 = sc.new_building(wspace=6, floor=8)
    import math
    # a half cylinder along x
    n = 10
    for i in range(n):
        a0, a1 = math.pi * i / n, math.pi * (i + 1) / n
        r = 13
        y0, z0 = -r * math.cos(a0), r * math.sin(a0)
        y1, z1 = -r * math.cos(a1), r * math.sin(a1)
        col = _lin('9a9c96')
        X0, X1 = 4, 36
        Y = 34
        sc.quad((X0, Y + y0, z0), (X1, Y + y0, z0), (X1, Y + y1, z1), (X0, Y + y1, z1), col, R3.METAL, b2)
    for X in (4, 36):
        for i in range(n):
            a0, a1 = math.pi * i / n, math.pi * (i + 1) / n
            sc.tri((X, 34, 0), (X, 34 - 13 * math.cos(a0), 13 * math.sin(a0)),
                   (X, 34 - 13 * math.cos(a1), 13 * math.sin(a1)), _lin('8a8c86'), R3.METAL, b2)
    # a control tower
    b3 = sc.new_building(wspace=5, floor=7)
    box(sc, -40, 22, -32, 30, 0, 26, _lin('c9c2b0'), R3.STUCCO, b3)
    box(sc, -42, 20, -30, 32, 26, 33, _lin('5d7480'), R3.GLASS, b3, top=_lin('6a6a66'))


def _radar(sc):
    import math
    import render3d as R3
    from geom import box, cylinder
    bid = sc.new_building(wspace=6, floor=8)
    steel = _lin('8c8f8a')
    # a hut
    box(sc, 14, 10, 34, 28, 0, 10, _lin('c8c0aa'), R3.STUCCO, bid, top=_lin('7b7a70'))
    # a lattice tower: four legs leaning in, with cross braces
    b2 = sc.new_building(wspace=6, floor=8)
    H = 58
    base, topr = 11, 4
    legs = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
    def leg(sx, sy, z):
        r = base + (topr - base) * z / H
        return (sx * r - 10, sy * r - 8, z)
    for sx, sy in legs:
        for k in range(8):
            z0, z1 = H * k / 8, H * (k + 1) / 8
            a, b = leg(sx, sy, z0), leg(sx, sy, z1)
            box(sc, a[0] - 1.1, a[1] - 1.1, a[0] + 1.1, a[1] + 1.1, z0, z1, steel, R3.METAL, b2)
    for k in range(1, 8, 2):
        z = H * k / 8
        for (s0, s1) in zip(legs, legs[1:] + legs[:1]):
            a, b = leg(*s0, z), leg(*s1, z)
            x0, x1 = sorted((a[0], b[0]))
            y0, y1 = sorted((a[1], b[1]))
            box(sc, x0 - 0.6, y0 - 0.6, x1 + 0.6, y1 + 0.6, z - 0.6, z + 0.6, steel, R3.METAL, b2)
    # platform and the dish
    box(sc, -17, -15, -3, -1, H, H + 2, _lin('6e716c'), R3.METAL, b2)
    b3 = sc.new_building(wspace=6, floor=8)
    dish = _lin('dedcd4')
    n = 16
    cx, cy, cz = -10, -8, H + 12
    # a shallow dish facing southwest-ish (toward the camera), tilted up
    nrm = np.array([-0.45, 0.65, 0.6]); nrm /= np.linalg.norm(nrm)
    u = np.cross(nrm, [0, 0, 1.0]); u /= np.linalg.norm(u)
    v = np.cross(nrm, u)
    R = 11
    pts = [np.array([cx, cy, cz]) + R * (math.cos(2 * math.pi * i / n) * u + math.sin(2 * math.pi * i / n) * v) - 2.5 * nrm
           for i in range(n)]
    ctr = np.array([cx, cy, cz])
    for i in range(n):
        sc.tri(tuple(ctr), tuple(pts[i]), tuple(pts[(i + 1) % n]), dish, R3.METAL, b3)
    box(sc, cx - 1.2, cy - 1.2, cx + 1.2, cy + 1.2, H + 2, cz - 3, steel, R3.METAL, b3)


def action_patch(key):
    """(patch, extra drawing) for a worker action."""
    t = key[len('TFRM_'):]
    p = Patch(Tile('grassland'))
    extra = None
    if t == 'Mine':
        p.set((0, 0), base='grassland', overlay='hills', building=(2, 1), bscale=1.7, boffset=(14, 4))
        extra = _worker('MINE', 0.3, at=(-44, 34))
    elif t == 'Irrigation':
        p = Patch(Tile('grassland', irrigation=True))
        extra = _worker('IRRIGATE', 0.3, at=(-30, 30))
    elif t == 'Fortress':
        p.set((0, 0), base='grassland', building=(0, 1), bscale=1.45, boffset=(18, -6))
        extra = _worker('FORTRESS', 0.35, at=(-58, 30))
    elif t == 'Road':
        for i in range(-3, 4):
            p.set((i, 0), base='grassland', road=True)
        extra = _worker('ROAD', 0.3, at=(36, -10))
    elif t == 'Railroad':
        for i in range(-3, 4):
            p.set((i, 0), base='grassland', rail=True)
        extra = _worker('ROAD', 0.3, at=(36, -10))
    elif t == 'Plant_Forest':
        p.set((0, 0), base='grassland', overlay='forest_small')
        extra = _worker('PLANT', 0.35, at=(-30, 40))
    elif t == 'Clear_Forest':
        p.set((0, 0), base='grassland', overlay='forest')
        extra = _worker('FOREST', 0.3, at=(-34, 46))
    elif t == 'Clear_Wetlands':
        p.set((0, 0), base='grassland', overlay='marsh')
        extra = _worker('JUNGLE', 0.3, at=(-34, 46))
    elif t == 'Clear_Damage':
        p.set((0, 0), base='grassland', pollution=3)
        extra = _worker('ROAD', 0.3, at=(-40, 34))
    elif t == 'Airfield':
        extra = _scene_sprite(_airfield)
    elif t == 'Radar_Tower':
        extra = _scene_sprite(_radar, H=360, oy=0.7)
    elif t == 'Outpost':
        p.set((0, 0), base='grassland', building=(0, 0), bscale=1.5, boffset=(16, -6))
        extra = _worker('FORTRESS', 0.35, at=(-58, 30))
    elif t == 'Barricades':
        p.set((0, 0), base='grassland', building=(3, 0), bscale=1.45, boffset=(0, -4))
    elif t == 'Build_Colony':
        p.set((0, 0), base='grassland', building=(1, 1), bscale=1.4, boffset=(16, -4))
        extra = _worker('FORTRESS', 0.35, at=(-60, 32))
    else:
        raise KeyError(key)
    return p, extra


def icon(key):
    p, extra = action_patch(key)
    large = L.diorama(p, C.LARGE, seed=4, extra=extra)
    small = L.diorama(p, C.SMALL * 4, zoom=0.46 * 4 * C.SMALL / C.LARGE * 1.12, seed=4, extra=extra)
    small = C.sharpen(C.downsample(small, 4), 0.5, 0.6)
    for img in (large, small):
        img[..., 3] = 1
        img[..., :3] = C.frame_edges(img[..., :3])
    return large, small
