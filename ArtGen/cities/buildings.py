"""The building kinds a city is made of. Each kind draws itself into a footprint
rectangle (x0, y0, x1, y1) up to a height h, in a culture/era palette `P`."""
import math
import numpy as np
from render3d import (PLAIN, STUCCO, WINDOWS, ROOF_TILE, THATCH, SLATE, GLASS, BRICK,
                      METAL, DOME, STONE, WATER, FOLIAGE, WOOD, GOLD, RUBBLE, PAVING)
from geom import (box, gable, hip, lathe, dome_profile, dome, cylinder, cone, crenellate,
                  parapet, curved_roof, column_row)


def jit(rng, col, amt=0.05):
    f = 1 + rng.uniform(-amt, amt)
    hue = rng.uniform(-amt, amt) * 0.4
    c = np.array(col, float) * f
    c = c + np.array([hue, 0, -hue])
    return tuple(np.clip(c, 0, 1))


def pick(rng, cols, amt=0.05):
    return jit(rng, cols[rng.integers(len(cols))], amt)


def darker(c, f=0.8):
    return tuple(np.clip(np.array(c) * f, 0, 1))


def mix(a, b, t):
    return tuple(np.array(a) * (1 - t) + np.array(b) * t)


# --------------------------------------------------------------------- houses
def flat_house(sc, r, h, P, rng, dome_chance=0.0, tier_chance=0.35):
    x0, y0, x1, y1 = r
    wall = pick(rng, P['walls'])
    b = sc.new_building(wspace=rng.uniform(6, 9), floor=rng.uniform(8, 10))
    w, d = x1 - x0, y1 - y0
    roof = mix(wall, (1, 0.97, 0.9), 0.12)
    if h > 16 and rng.random() < tier_chance and min(w, d) > 14:
        h1 = h * rng.uniform(0.5, 0.65)
        box(sc, x0, y0, x1, y1, 0, h1, wall, STUCCO, b, top=roof)
        parapet(sc, x0, y0, x1, y1, h1, 1.4, wall, STUCCO, b)
        # Upper storey on the back part.
        fx = rng.uniform(0.45, 0.65); fy = rng.uniform(0.45, 0.65)
        ux1 = x0 + w * fx if rng.random() < 0.5 else x1
        uy1 = y0 + d * fy
        b2 = sc.new_building(wspace=7, floor=9, z0=0)
        box(sc, x0 + 1, y0 + 1, ux1 - 1, uy1, 0, h - 1.4, wall, STUCCO, b2, top=roof)
        parapet(sc, x0 + 1, y0 + 1, ux1 - 1, uy1, h - 1.4, 1.4, wall, STUCCO, b2)
        top_z, tr = h - 1.4, (x0 + 1, y0 + 1, ux1 - 1, uy1)
    else:
        hb = h - 1.4
        box(sc, x0, y0, x1, y1, 0, hb, wall, STUCCO, b, top=roof)
        parapet(sc, x0, y0, x1, y1, hb, 1.4, wall, STUCCO, b)
        top_z, tr = hb, r
    tx0, ty0, tx1, ty1 = tr
    tw, td = tx1 - tx0, ty1 - ty0
    if rng.random() < dome_chance and min(tw, td) > 9:
        rr = min(tw, td) * 0.3
        dome(sc, (tx0 + tx1) / 2, (ty0 + ty1) / 2, rr, top_z, pick(rng, P['dome_small']), DOME, b, h=rr * 0.95, seg=18)
    elif rng.random() < 0.5 and min(tw, td) > 10:
        # A small stair house on the roof.
        sx = tx0 + rng.uniform(1.5, tw * 0.4); sy = ty0 + rng.uniform(1.5, td * 0.4)
        box(sc, sx, sy, sx + min(6, tw * 0.3), sy + min(6, td * 0.3), top_z, top_z + 4.5, wall, STUCCO, b, top=roof)


def gable_house(sc, r, h, P, rng, roofs=None, walls=None, wallmat=STUCCO, roofmat=None, roof_frac=None, over=1.6, windows=None):
    x0, y0, x1, y1 = r
    w, d = x1 - x0, y1 - y0
    wall = pick(rng, walls or P['walls'])
    roof = pick(rng, roofs or P['roofs'], 0.07)
    rm = P.get('roofmat', ROOF_TILE) if roofmat is None else roofmat
    axis = 'x' if w >= d else 'y'
    span = min(w, d)
    rh = min(span * rng.uniform(0.45, 0.6), h * 0.6) if roof_frac is None else h * roof_frac
    wh = h - rh
    params = dict(wspace=rng.uniform(5.5, 7.5), floor=rng.uniform(8, 9.5), wtop=wh)
    if windows:
        params.update(windows)
    b = sc.new_building(**params)
    box(sc, x0, y0, x1, y1, 0, wh, wall, wallmat, b)
    gable(sc, x0, y0, x1, y1, wh, rh, roof, rm, b, axis=axis, over=over, gable_col=wall, gable_mat=wallmat)
    return b


def hip_house(sc, r, h, P, rng, roofs=None, walls=None, wallmat=STUCCO, roofmat=None, windows=None):
    x0, y0, x1, y1 = r
    wall = pick(rng, walls or P['walls'])
    roof = pick(rng, roofs or P['roofs'], 0.07)
    rm = P.get('roofmat', ROOF_TILE) if roofmat is None else roofmat
    rh = min(min(x1 - x0, y1 - y0) * 0.42, h * 0.55)
    wh = h - rh
    params = dict(wspace=rng.uniform(5.5, 7.5), floor=rng.uniform(8, 9.5), wtop=wh)
    if windows:
        params.update(windows)
    b = sc.new_building(**params)
    box(sc, x0, y0, x1, y1, 0, wh, wall, wallmat, b)
    hip(sc, x0, y0, x1, y1, wh, rh, roof, rm, b, over=1.6)
    return b


def round_hut(sc, r, h, P, rng, beehive=False, tiers=1):
    x0, y0, x1, y1 = r
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    rad = min(x1 - x0, y1 - y0) / 2
    thatch = pick(rng, P['thatch'], 0.06)
    b = sc.new_building()
    if beehive:
        lathe(sc, cx, cy, dome_profile(rad, 0, h, k=10), thatch, THATCH, b, seg=20)
        return
    wall = pick(rng, P.get('hutwalls', P['walls']))
    wh = h * 0.35
    cylinder(sc, cx, cy, rad * 0.82, 0, wh, wall, WOOD, b, seg=18)
    if tiers == 1:
        lathe(sc, cx, cy, [(rad, wh - 1.5), (rad * 0.55, wh + (h - wh) * 0.55), (0.0, h)], thatch, THATCH, b, seg=20)
    else:
        z = wh - 1.5
        rr = rad
        for t in range(tiers):
            top = z + (h - wh) / tiers * 1.15
            lathe(sc, cx, cy, [(rr, z), (rr * 0.45, top)], thatch, THATCH, b, seg=20)
            z = top - (h - wh) / tiers * 0.35
            rr *= 0.66
        lathe(sc, cx, cy, [(rr * 0.8, z), (0.0, h)], thatch, THATCH, b, seg=16)


def longhouse(sc, r, h, P, rng):
    """A long hall roofed with thatch down to the ground (rounded ends)."""
    x0, y0, x1, y1 = r
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    w, d = x1 - x0, y1 - y0
    thatch = pick(rng, P['thatch'], 0.06)
    b = sc.new_building()
    rad = min(w, d) / 2
    L = max(w, d) / 2 - rad
    ax = w >= d
    # The straight middle part: an extruded rounded gable.
    prof = []
    k = 10
    for i in range(k + 1):
        t = i / k * np.pi
        prof.append((math.cos(t) * rad, h * (math.sin(t) ** 0.7)))
    for i in range(k):
        (u0, z0), (u1, z1) = prof[i], prof[i + 1]
        # normals of the profile
        def nrm(u, z):
            nu, nz = u / rad, z / h * 0.8
            l = math.hypot(nu, nz) + 1e-9
            return nu / l, nz / l
        n0, n1 = nrm(u0, z0), nrm(u1, z1)
        if ax:
            A, B, C, D = (cx - L, cy + u0, z0), (cx + L, cy + u0, z0), (cx + L, cy + u1, z1), (cx - L, cy + u1, z1)
            na, nb = (0, n0[0], n0[1]), (0, n1[0], n1[1])
        else:
            A, B, C, D = (cx + u0, cy - L, z0), (cx + u0, cy + L, z0), (cx + u1, cy + L, z1), (cx + u1, cy - L, z1)
            na, nb = (n0[0], 0, n0[1]), (n1[0], 0, n1[1])
        sc.quad(A, B, C, D, thatch, THATCH, b, (na, na, nb, nb))
    # Rounded ends: half domes.
    for s in (-1, 1):
        ex, ey = (cx + s * L, cy) if ax else (cx, cy + s * L)
        prof2 = [(rad * math.cos(t), h * math.sin(t) ** 0.7) for t in np.linspace(0, np.pi / 2, 8)]
        prof2[-1] = (0.0, h)
        lathe(sc, ex, ey, prof2, thatch, THATCH, b, seg=20)


# ------------------------------------------------------------------ landmarks
def domed_hall(sc, r, h, P, rng, dome_col=None, turrets=True, onion=0.0):
    """A square hall with a big dome (mosque, church, capitol)."""
    x0, y0, x1, y1 = r
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    w, d = x1 - x0, y1 - y0
    wall = pick(rng, P['walls'])
    dc = dome_col or pick(rng, P['dome_big'], 0.03)
    rad = min(w, d) * 0.36
    dh = rad * (1.0 + 0.25 * onion)
    hb = max(6.0, h - dh * (1 + 0.35 * onion) - 3.5)
    b = sc.new_building(wspace=8, floor=11, archw=1)
    box(sc, x0, y0, x1, y1, 0, hb, wall, STUCCO, b, top=mix(wall, (1, 1, 1), 0.1))
    parapet(sc, x0, y0, x1, y1, hb, 1.4, wall, STUCCO, b)
    # Drum and dome.
    lathe(sc, cx, cy, [(rad * 1.04, hb), (rad * 1.04, hb + 3.5)], wall, STUCCO, b, seg=24, cap=False)
    lathe(sc, cx, cy, dome_profile(rad, hb + 3.5, dh, onion=onion), dc, DOME, b, seg=28)
    top = hb + 3.5 + dh * (1 + 0.35 * onion)
    cylinder(sc, cx, cy, 0.5, top - 0.5, min(h, top + 3), P.get('gold', (0.85, 0.7, 0.3)), GOLD, b, seg=6)
    if turrets and min(w, d) > 20:
        for (tx, ty) in ((x0 + 2.5, y1 - 2.5), (x1 - 2.5, y1 - 2.5), (x1 - 2.5, y0 + 2.5)):
            cylinder(sc, tx, ty, 2.0, hb, hb + 4, wall, STUCCO, b, seg=10)
            dome(sc, tx, ty, 2.0, hb + 4, dc, DOME, b, h=2.2, seg=10)


def minaret(sc, r, h, P, rng, cap_col=None):
    x0, y0, x1, y1 = r
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    rad = min(x1 - x0, y1 - y0) / 2
    wall = pick(rng, P['walls'], 0.03)
    cap = cap_col or pick(rng, P['dome_big'], 0.03)
    b = sc.new_building()
    base_h = min(h * 0.15, 6)
    box(sc, cx - rad, cy - rad, cx + rad, cy + rad, 0, base_h, wall, STUCCO, b)
    sr = rad * 0.72
    capz = h - max(5.0, h * 0.13)
    lathe(sc, cx, cy, [(sr, base_h), (sr * 0.9, capz)], wall, STONE, b, seg=14, cap=True)
    for f in (0.62, 0.86):
        z = base_h + (capz - base_h) * f
        lathe(sc, cx, cy, [(sr * 0.9, z - 1.6), (sr * 1.45, z), (sr * 1.45, z + 0.9), (sr * 0.9, z + 0.9)], mix(wall, (1, 1, 1), 0.12), STUCCO, b, seg=14, smooth=False, cap=False)
    lathe(sc, cx, cy, [(sr * 0.95, capz), (sr * 0.95, capz + 0.8), (0.0, h)], cap, DOME, b, seg=14)


def round_tower(sc, r, h, P, rng, roof='cren', wallcol=None, mat=STONE, roofcol=None):
    x0, y0, x1, y1 = r
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    rad = min(x1 - x0, y1 - y0) / 2
    wall = wallcol or pick(rng, P['stone'] if 'stone' in P else P['walls'], 0.04)
    b = sc.new_building(wspace=8, floor=10)
    if roof == 'cren':
        top = h - 2.5
        lathe(sc, cx, cy, [(rad * 0.92, 0), (rad * 0.92, top - 2), (rad, top - 1), (rad, top)], wall, mat, b, seg=20)
        n = 10
        for i in range(n):
            a = 2 * np.pi * (i + 0.25) / n
            px, py = cx + math.cos(a) * rad * 0.9, cy + math.sin(a) * rad * 0.9
            box(sc, px - 1.0, py - 1.0, px + 1.0, py + 1.0, top, h, wall, mat, b)
    else:
        rc = roofcol or pick(rng, P['roofs'], 0.05)
        wh = h * 0.68
        lathe(sc, cx, cy, [(rad * 0.9, 0), (rad * 0.9, wh)], wall, mat, b, seg=20, cap=False)
        lathe(sc, cx, cy, [(rad * 1.05, wh - 0.5), (0.0, h)], rc, P.get('roofmat', SLATE), b, seg=20)


def church(sc, r, h, P, rng, walls=None, roofs=None, spire_col=None, mat=STONE):
    """A nave with a pitched roof and a tower with a spire at one end."""
    x0, y0, x1, y1 = r
    w, d = x1 - x0, y1 - y0
    wall = pick(rng, walls or P.get('stone', P['walls']), 0.04)
    roof = pick(rng, roofs or P['roofs'], 0.05)
    sp = spire_col or roof
    b = sc.new_building(wspace=6, floor=14)
    nave_h = h * 0.42
    if w >= d:
        tw = min(d * 0.8, w * 0.35)
        tx0, tx1 = x0, x0 + tw
        ty0, ty1 = (y0 + y1) / 2 - tw / 2, (y0 + y1) / 2 + tw / 2
        box(sc, x0 + tw * 0.6, y0, x1, y1, 0, nave_h * 0.62, wall, mat, b)
        gable(sc, x0 + tw * 0.6, y0, x1, y1, nave_h * 0.62, nave_h * 0.38 + d * 0.15, roof, P.get('roofmat', SLATE), b, axis='x', gable_col=wall, gable_mat=mat)
    else:
        tw = min(w * 0.8, d * 0.35)
        ty0, ty1 = y0, y0 + tw
        tx0, tx1 = (x0 + x1) / 2 - tw / 2, (x0 + x1) / 2 + tw / 2
        box(sc, x0, y0 + tw * 0.6, x1, y1, 0, nave_h * 0.62, wall, mat, b)
        gable(sc, x0, y0 + tw * 0.6, x1, y1, nave_h * 0.62, nave_h * 0.38 + w * 0.15, roof, P.get('roofmat', SLATE), b, axis='y', gable_col=wall, gable_mat=mat)
    th = h * 0.6
    box(sc, tx0, ty0, tx1, ty1, 0, th, wall, mat, b)
    hip(sc, tx0, ty0, tx1, ty1, th, h - th, sp, P.get('roofmat', SLATE), b, over=0.6, ridge=0)


def temple(sc, r, h, P, rng):
    """A classical temple: podium, columns all round, pediment roof."""
    x0, y0, x1, y1 = r
    w, d = x1 - x0, y1 - y0
    marble = P.get('marble', (0.93, 0.9, 0.82))
    roof = pick(rng, P['roofs'], 0.04)
    b = sc.new_building(wspace=6, floor=12)
    pod = h * 0.12
    box(sc, x0, y0, x1, y1, 0, pod, darker(marble, 0.92), STONE, b)
    ch = h * 0.5
    inset = 2.2
    # Cella (dark interior visible between columns).
    box(sc, x0 + 4.5, y0 + 4.5, x1 - 4.5, y1 - 4.5, pod, pod + ch, darker(marble, 0.8), STUCCO, b)
    cr = 1.05
    nx = max(3, int((x1 - x0) / 4.2)); ny = max(3, int((y1 - y0) / 4.2))
    column_row(sc, x0 + inset, y1 - inset, x1 - inset, y1 - inset, pod, pod + ch, cr, nx, marble, PLAIN, b)
    column_row(sc, x1 - inset, y0 + inset, x1 - inset, y1 - inset, pod, pod + ch, cr, ny, marble, PLAIN, b)
    column_row(sc, x0 + inset, y0 + inset, x1 - inset, y0 + inset, pod, pod + ch, cr, nx, marble, PLAIN, b)
    column_row(sc, x0 + inset, y0 + inset, x0 + inset, y1 - inset, pod, pod + ch, cr, ny, marble, PLAIN, b)
    ent = 2.2
    box(sc, x0 + 0.8, y0 + 0.8, x1 - 0.8, y1 - 0.8, pod + ch, pod + ch + ent, marble, PLAIN, b)
    gable(sc, x0 + 0.8, y0 + 0.8, x1 - 0.8, y1 - 0.8, pod + ch + ent, h - (pod + ch + ent), roof, P.get('roofmat', ROOF_TILE), b,
          axis='x' if w >= d else 'y', over=1.0, gable_col=marble, gable_mat=PLAIN)


def rotunda(sc, r, h, P, rng, dome_col=None):
    x0, y0, x1, y1 = r
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    rad = min(x1 - x0, y1 - y0) / 2
    wall = P.get('marble', pick(rng, P['walls']))
    dc = dome_col or pick(rng, P['dome_big'], 0.03)
    b = sc.new_building(wspace=6, floor=10, rcx=cx, rcy=cy)
    dh = rad * 0.45
    wh = h - dh
    lathe(sc, cx, cy, [(rad, 0), (rad, wh - 2), (rad * 1.03, wh - 2), (rad * 1.03, wh)], wall, STONE, b, seg=28, smooth=False)
    lathe(sc, cx, cy, dome_profile(rad * 0.96, wh, dh), dc, DOME, b, seg=28)


def duomo(sc, r, h, P, rng):
    """A ribbed, pointed dome on an octagonal drum over a cross-shaped church."""
    x0, y0, x1, y1 = r
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    w, d = x1 - x0, y1 - y0
    marble = P.get('marble', (0.9, 0.86, 0.76))
    roof = P.get('dome_rib', (0.78, 0.42, 0.25))
    b = sc.new_building(wspace=7, floor=12)
    bh = h * 0.28
    box(sc, x0 + w * 0.2, y0, x1 - w * 0.2, y1, 0, bh, marble, STUCCO, b)
    gable(sc, x0 + w * 0.2, y0, x1 - w * 0.2, y1, bh, bh * 0.4, roof, ROOF_TILE, b, axis='y', over=0.8, gable_col=marble, gable_mat=STUCCO)
    box(sc, x0, y0 + d * 0.2, x1, y1 - d * 0.2, 0, bh, marble, STUCCO, b)
    gable(sc, x0, y0 + d * 0.2, x1, y1 - d * 0.2, bh, bh * 0.4, roof, ROOF_TILE, b, axis='x', over=0.8, gable_col=marble, gable_mat=STUCCO)
    rad = min(w, d) * 0.3
    dz = bh + bh * 0.3
    lathe(sc, cx, cy, [(rad * 1.05, 0), (rad * 1.05, dz + 4)], marble, STUCCO, b, seg=8, smooth=False)
    lan = 4.0
    dh = h - dz - 4 - lan
    prof = []
    for i in range(9):
        t = i / 8 * np.pi / 2
        prof.append((rad * math.cos(t) ** 0.8, dz + 4 + dh * math.sin(t) ** 1.25))
    prof[-1] = (rad * 0.18, dz + 4 + dh)
    lathe(sc, cx, cy, prof, roof, ROOF_TILE, b, seg=8, smooth=True)
    lathe(sc, cx, cy, [(rad * 0.18, dz + 4 + dh), (rad * 0.16, h - 1.5), (0.0, h)], marble, PLAIN, b, seg=8)


def triumphal_arch(sc, r, h, P, rng):
    x0, y0, x1, y1 = r
    w, d = x1 - x0, y1 - y0
    marble = P.get('marble', (0.9, 0.86, 0.76))
    b = sc.new_building()
    if w >= d:
        pw = w * 0.3
        box(sc, x0, y0, x0 + pw, y1, 0, h * 0.7, marble, STONE, b)
        box(sc, x1 - pw, y0, x1, y1, 0, h * 0.7, marble, STONE, b)
    else:
        pw = d * 0.3
        box(sc, x0, y0, x1, y0 + pw, 0, h * 0.7, marble, STONE, b)
        box(sc, x0, y1 - pw, x1, y1, 0, h * 0.7, marble, STONE, b)
    box(sc, x0, y0, x1, y1, h * 0.7, h, marble, STONE, b)


def pagoda(sc, r, h, P, rng, tiers=None):
    x0, y0, x1, y1 = r
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    half = min(x1 - x0, y1 - y0) / 2 - 3
    wall = pick(rng, P.get('hutwalls', P['walls']))
    roof = pick(rng, P['roofs'], 0.04)
    b = sc.new_building()
    tiers = tiers or max(2, min(5, int(h / 11)))
    th = (h - 4) / tiers
    z = 0
    for t in range(tiers):
        s = half * (1 - 0.15 * t)
        wh = th * 0.5
        box(sc, cx - s * 0.8, cy - s * 0.8, cx + s * 0.8, cy + s * 0.8, z, z + wh, wall, WOOD, b)
        curved_roof(sc, cx - s * 0.8, cy - s * 0.8, cx + s * 0.8, cy + s * 0.8, z + wh, th * 0.5, roof, SLATE, b, over=3.0, lift=1.6, n=8)
        z += th
    cylinder(sc, cx, cy, 0.5, z - th * 0.3, h, P.get('gold', (0.85, 0.7, 0.3)), GOLD, b, seg=6)


def asian_hall(sc, r, h, P, rng, two_tier=None, roofs=None, walls=None, podium=True):
    x0, y0, x1, y1 = r
    w, d = x1 - x0, y1 - y0
    wall = pick(rng, walls or P.get('hutwalls', P['walls']))
    roof = pick(rng, roofs or P['roofs'], 0.05)
    b = sc.new_building(wspace=4, floor=8, wtop=1e9)
    o = 2.5
    pz = 2.0 if podium else 0.0
    if podium:
        box(sc, x0, y0, x1, y1, 0, pz, P.get('podium', (0.62, 0.6, 0.55)), STONE, b)
    two = (h > 22 and min(w, d) > 18) if two_tier is None else two_tier
    if two:
        wh = (h - pz) * 0.3
        box(sc, x0 + o, y0 + o, x1 - o, y1 - o, pz, pz + wh, wall, WOOD, b)
        curved_roof(sc, x0 + o, y0 + o, x1 - o, y1 - o, pz + wh, (h - pz) * 0.18, roof, SLATE, b, over=2.5, lift=1.4)
        z2 = pz + wh + (h - pz) * 0.12
        i = min(w, d) * 0.22
        box(sc, x0 + o + i, y0 + o + i, x1 - o - i, y1 - o - i, z2, z2 + (h - pz) * 0.2, wall, WOOD, b)
        z3 = z2 + (h - pz) * 0.2
        curved_roof(sc, x0 + o + i, y0 + o + i, x1 - o - i, y1 - o - i, z3, h - z3 - 1.5, roof, SLATE, b, over=3.0, lift=1.8)
    else:
        wh = (h - pz) * 0.45
        box(sc, x0 + o, y0 + o, x1 - o, y1 - o, pz, pz + wh, wall, WOOD, b)
        curved_roof(sc, x0 + o, y0 + o, x1 - o, y1 - o, pz + wh, h - pz - wh - 1.5, roof, SLATE, b, over=o, lift=1.5)


# ----------------------------------------------------------------- industrial
def chimney(sc, cx, cy, rad, z0, h, col, b):
    lathe(sc, cx, cy, [(rad, z0), (rad * 0.78, h - 1.2), (rad * 0.9, h - 1.0), (rad * 0.9, h)], col, BRICK, b, seg=12)
    lathe(sc, cx, cy, [(rad * 0.62, h + 0.001), (0.0, h + 0.002)], (0.08, 0.07, 0.07), PLAIN, b, seg=12, cap=False)


def factory(sc, r, h, P, rng, chimneys=None):
    x0, y0, x1, y1 = r
    w, d = x1 - x0, y1 - y0
    wall = pick(rng, P.get('factory_walls', P['walls']))
    roof = pick(rng, P.get('factory_roofs', P['roofs']), 0.05)
    b = sc.new_building(wspace=5, floor=8)
    bh = min(h * 0.38, 16)
    box(sc, x0, y0, x1, y1, 0, bh * 0.7, wall, BRICK if P.get('brick_factory') else WINDOWS, b)
    # Sawtooth roof as a run of small gables.
    ax = 'x' if w < d else 'y'
    n = max(1, int((w if ax == 'y' else d) / 9))
    for i in range(n):
        if ax == 'x':
            gy0 = y0 + d * i / n; gy1 = y0 + d * (i + 1) / n
            gable(sc, x0, gy0, x1, gy1, bh * 0.7, bh * 0.3, roof, SLATE, b, axis='x', over=0.4, gable_col=wall, gable_mat=STUCCO)
        else:
            gx0 = x0 + w * i / n; gx1 = x0 + w * (i + 1) / n
            gable(sc, gx0, y0, gx1, y1, bh * 0.7, bh * 0.3, roof, SLATE, b, axis='y', over=0.4, gable_col=wall, gable_mat=STUCCO)
    nc = chimneys if chimneys is not None else rng.integers(1, 4)
    brick = P.get('brick', (0.62, 0.3, 0.22))
    for i in range(nc):
        f = (i + 0.5) / nc
        if w >= d:
            px, py = x0 + 4 + (w - 8) * f, y0 + 4
        else:
            px, py = x0 + 4, y0 + 4 + (d - 8) * f
        chimney(sc, px, py, 3.4, 0, h * rng.uniform(0.85, 1.0), jit(rng, brick, 0.05), b)


def chimney_stack(sc, r, h, P, rng):
    x0, y0, x1, y1 = r
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    b = sc.new_building()
    rad = min(x1 - x0, y1 - y0) / 2
    box(sc, cx - rad, cy - rad, cx + rad, cy + rad, 0, 4, pick(rng, P['walls']), STONE, b)
    chimney(sc, cx, cy, rad * 0.75, 0, h, jit(rng, P.get('brick', (0.62, 0.3, 0.22)), 0.05), b)


def gas_tank(sc, r, h, P, rng, sphere=None):
    x0, y0, x1, y1 = r
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    rad = min(x1 - x0, y1 - y0) / 2
    col = pick(rng, P.get('metal', [(0.62, 0.63, 0.62)]), 0.05)
    b = sc.new_building()
    sph = rng.random() < 0.4 if sphere is None else sphere
    if sph:
        rr = min(rad, h / 2.2)
        prof = [(rr * math.sin(t), h - rr + rr * -math.cos(t)) for t in np.linspace(0, np.pi, 14)]
        prof = [(abs(rv), z) for rv, z in prof]
        prof = [(0.0, h - 2 * rr)] + prof[1:-1] + [(0.0, h)]
        lathe(sc, cx, cy, prof, col, METAL, b, seg=20)
        for a in (0.8, 2.4, 3.9, 5.5):
            px, py = cx + math.cos(a) * rr * 0.7, cy + math.sin(a) * rr * 0.7
            box(sc, px - 0.6, py - 0.6, px + 0.6, py + 0.6, 0, h - rr, darker(col, 0.7), METAL, b)
    else:
        wh = h - rad * 0.3
        lathe(sc, cx, cy, [(rad, 0), (rad, wh)], col, METAL, b, seg=22, cap=False)
        lathe(sc, cx, cy, dome_profile(rad, wh, rad * 0.3), mix(col, (1, 1, 1), 0.08), METAL, b, seg=22)


def warehouse(sc, r, h, P, rng):
    x0, y0, x1, y1 = r
    gable_house(sc, r, h, P, rng, roofs=P.get('factory_roofs', P['roofs']), walls=P.get('factory_walls', P['walls']),
                wallmat=WINDOWS, roofmat=SLATE, roof_frac=0.3, over=0.5, windows=dict(wspace=5, floor=7, wwidth=2.4, wheight=3.5))


# --------------------------------------------------------------------- modern
def apartment(sc, r, h, P, rng, walls=None, roofcol=None, glass=False, setback=None):
    x0, y0, x1, y1 = r
    w, d = x1 - x0, y1 - y0
    wall = pick(rng, walls or P.get('modern_walls', P['walls']), 0.04)
    ws = rng.uniform(4.0, 5.5)
    ribbon = rng.random() < 0.4
    b = sc.new_building(wspace=ws, floor=rng.uniform(5.5, 7.0), wwidth=(ws * 0.86 if ribbon else rng.uniform(2.2, 3.0)),
                        wheight=rng.uniform(2.6, 3.4), wdark=P.get('wdark', rng.uniform(0.28, 0.4)), z0=0.0)
    mat = GLASS if glass else WINDOWS
    roof_t = mix(wall, (0.55, 0.55, 0.53), 0.55) if roofcol is None else roofcol
    sb = (h > 40 and rng.random() < 0.6) if setback is None else setback
    if sb:
        h1 = h * rng.uniform(0.55, 0.7)
        box(sc, x0, y0, x1, y1, 0, h1, wall, mat, b, top=roof_t, topmat=PLAIN)
        i = min(w, d) * 0.18
        b2 = sc.new_building(**sc.buildings[b])
        box(sc, x0 + i, y0 + i, x1 - i * 0.5, y1 - i * 0.5, h1, h - 2, wall, mat, b2, top=roof_t, topmat=PLAIN)
        parapet(sc, x0 + i, y0 + i, x1 - i * 0.5, y1 - i * 0.5, h - 2, 1.2, mix(wall, (1, 1, 1), 0.1), PLAIN, b2, t=0.8)
        top, tr = h - 2, (x0 + i, y0 + i, x1 - i * 0.5, y1 - i * 0.5)
    else:
        box(sc, x0, y0, x1, y1, 0, h - 1.2, wall, mat, b, top=roof_t, topmat=PLAIN)
        parapet(sc, x0, y0, x1, y1, h - 1.2, 1.2, mix(wall, (1, 1, 1), 0.1), PLAIN, b, t=0.8)
        top, tr = h - 1.2, r
    tx0, ty0, tx1, ty1 = tr
    if rng.random() < 0.6 and min(tx1 - tx0, ty1 - ty0) > 8:
        mx = tx0 + (tx1 - tx0) * rng.uniform(0.2, 0.5)
        my = ty0 + (ty1 - ty0) * rng.uniform(0.2, 0.5)
        box(sc, mx, my, mx + 4.5, my + 4, top, top + 3, (0.6, 0.6, 0.58), PLAIN, b)


def tower_block(sc, r, h, P, rng, crown=None, glass=None):
    """A modern high-rise, maybe with a pyramid crown."""
    x0, y0, x1, y1 = r
    w, d = x1 - x0, y1 - y0
    g = rng.random() < P.get('glass_chance', 0.35) if glass is None else glass
    wall = pick(rng, P['glass'] if g else P.get('modern_walls', P['walls']), 0.04)
    b = sc.new_building(wspace=rng.uniform(3.6, 4.6), floor=rng.uniform(4.8, 6.0), wwidth=2.4, wheight=3.2, wdark=P.get('wdark', 0.32))
    mat = GLASS if g else WINDOWS
    cr = crown if crown is not None else (P.get('crown') if rng.random() < P.get('crown_chance', 0.0) else None)
    ch = min(w, d) * 0.45 if cr is not None else 0
    bodyh = h - ch - 1
    # Two setbacks.
    h1 = bodyh * rng.uniform(0.6, 0.8)
    box(sc, x0, y0, x1, y1, 0, h1, wall, mat, b, top=mix(wall, (0.55, 0.56, 0.56), 0.55), topmat=PLAIN)
    i = min(w, d) * 0.15
    b2 = sc.new_building(**sc.buildings[b])
    box(sc, x0 + i, y0 + i, x1 - i, y1 - i, h1, bodyh, wall, mat, b2, top=mix(wall, (0.55, 0.56, 0.56), 0.55), topmat=PLAIN)
    if cr is not None:
        hip(sc, x0 + i, y0 + i, x1 - i, y1 - i, bodyh, ch, cr, METAL, b2, over=0.3, ridge=0)
    else:
        parapet(sc, x0 + i, y0 + i, x1 - i, y1 - i, bodyh, 1.0, mix(wall, (1, 1, 1), 0.1), PLAIN, b2, t=0.7)
        if rng.random() < 0.5:
            cylinder(sc, (x0 + x1) / 2, (y0 + y1) / 2, 0.35, bodyh, h, (0.3, 0.3, 0.3), METAL, b2, seg=5)


def round_highrise(sc, r, h, P, rng):
    x0, y0, x1, y1 = r
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    rad = min(x1 - x0, y1 - y0) / 2
    wall = pick(rng, P.get('modern_walls', P['walls']), 0.04)
    b = sc.new_building(wspace=4.0, floor=5.2, wwidth=2.6, wheight=3.0, wdark=P.get('wdark', 0.3), rcx=cx, rcy=cy)
    lathe(sc, cx, cy, [(rad, 0), (rad, h - 2)], wall, WINDOWS, b, seg=26, cap=True)
    lathe(sc, cx, cy, [(rad * 0.85, h - 2), (rad * 0.85, h)], mix(wall, (1, 1, 1), 0.1), PLAIN, b, seg=26)


# ---------------------------------------------------------------- small stuff
def fountain(sc, cx, cy, rad, P):
    b = sc.new_building()
    stone = P.get('marble', (0.8, 0.78, 0.72))
    lathe(sc, cx, cy, [(rad, 0), (rad, 2.0), (rad - 1.2, 2.0), (rad - 1.2, 1.2)], stone, STONE, b, seg=24, smooth=False)
    lathe(sc, cx, cy, [(rad - 1.2, 1.2), (0.0, 1.21)], (0.35, 0.6, 0.75), WATER, b, seg=24, cap=False)
    cylinder(sc, cx, cy, 1.0, 1.2, 4.5, stone, STONE, b, seg=10)
    lathe(sc, cx, cy, [(2.6, 4.0), (2.6, 4.6), (0.0, 4.61)], stone, STONE, b, seg=14, smooth=False)


def statue(sc, cx, cy, h, P):
    b = sc.new_building()
    stone = P.get('marble', (0.85, 0.82, 0.75))
    box(sc, cx - 2.5, cy - 2.5, cx + 2.5, cy + 2.5, 0, h * 0.45, stone, STONE, b)
    lathe(sc, cx, cy, [(1.3, h * 0.45), (1.0, h * 0.8), (0.9, h * 0.85), (0.0, h)], darker(stone, 0.9), PLAIN, b, seg=10)


def obelisk(sc, cx, cy, h, P):
    b = sc.new_building()
    stone = P.get('marble', (0.85, 0.82, 0.75))
    box(sc, cx - 2.2, cy - 2.2, cx + 2.2, cy + 2.2, 0, 2, stone, STONE, b)
    lathe(sc, cx, cy, [(1.6, 2), (1.1, h - 2.5), (0.0, h)], stone, PLAIN, b, seg=4, smooth=False)


def tree(sc, cx, cy, h, P, rng, palm=False):
    b = sc.new_building()
    trunk = (0.42, 0.3, 0.2)
    leaf = pick(rng, P.get('leaves', [(0.36, 0.5, 0.2)]), 0.08)
    if palm:
        lathe(sc, cx, cy, [(0.7, 0), (0.5, h - 2)], trunk, WOOD, b, seg=6)
        for i in range(7):
            a = 2 * np.pi * i / 7 + rng.uniform(-0.2, 0.2)
            ex, ey = cx + math.cos(a) * 5.0, cy + math.sin(a) * 5.0
            px, py = -math.sin(a) * 1.2, math.cos(a) * 1.2
            sc.tri((cx, cy, h - 1), (ex + px, ey + py, h - 3.5), (ex - px, ey - py, h - 3.5), leaf, FOLIAGE, b)
        return
    lathe(sc, cx, cy, [(0.8, 0), (0.6, h * 0.45)], trunk, WOOD, b, seg=6)
    rr = h * 0.33
    lathe(sc, cx, cy, [(0.0, h * 0.32)] + [(rr * math.sin(t), h * 0.32 + rr * (1 - math.cos(t)) * 1.0) for t in np.linspace(0.2, np.pi, 9)][:-1] + [(0.0, h)], leaf, FOLIAGE, b, seg=12)


def block(sc, r, h, P, rng, arcade=0.3, dome_chance=0.15, parts=None, roof_kind='flat'):
    """A block of joined houses of different heights (flat roofs and parapets),
    maybe with an arcade on the ground floor and a small dome."""
    x0, y0, x1, y1 = r
    w, d = x1 - x0, y1 - y0
    n = parts or (1 if max(w, d) < 18 else (2 if max(w, d) < 30 else 3))
    along_x = w >= d
    cuts = np.sort(rng.uniform(0.3, 0.7, n - 1)) if n > 1 else []
    edges = [0.0] + list(cuts) + [1.0]
    wall = pick(rng, P['walls'])
    hs = [h * rng.uniform(0.55, 1.0) for _ in range(n)]
    hs[rng.integers(n)] = h
    for i in range(n):
        if along_x:
            r2 = (x0 + w * edges[i], y0, x0 + w * edges[i + 1], y1)
        else:
            r2 = (x0, y0 + d * edges[i], x1, y0 + d * edges[i + 1])
        c = jit(rng, wall, 0.04) if rng.random() < 0.7 else pick(rng, P['walls'])
        a, bb, cc, dd = r2
        # Slight setbacks keep the joints readable.
        if i > 0:
            if along_x:
                b0 = rng.uniform(0, 2); a, bb, dd = a, bb + b0, dd - rng.uniform(0, 2)
            else:
                b0 = rng.uniform(0, 2); a, cc = a + b0, cc - rng.uniform(0, 2)
        hh = hs[i]
        arc = rng.random() < arcade and hh > 14
        b = sc.new_building(wspace=rng.uniform(6, 9), floor=rng.uniform(8.5, 10.5),
                            arch=(rng.uniform(5, 6.5) if arc else 0), archh=rng.uniform(6.5, 8.5))
        roofc = mix(c, (1, 0.97, 0.9), 0.12)
        box(sc, a, bb, cc, dd, 0, hh - 1.3, c, STUCCO, b, top=roofc)
        parapet(sc, a, bb, cc, dd, hh - 1.3, 1.3, c, STUCCO, b)
        if rng.random() < dome_chance and min(cc - a, dd - bb) > 10:
            rr = min(cc - a, dd - bb) * 0.28
            dome(sc, (a + cc) / 2, (bb + dd) / 2, rr, hh - 1.3, pick(rng, P['dome_small']), DOME, b, h=rr * 0.95, seg=18)
        elif rng.random() < 0.35 and min(cc - a, dd - bb) > 12:
            sx = a + 2; sy = bb + 2
            box(sc, sx, sy, sx + 5, sy + 5, hh - 1.3, hh + 3, c, STUCCO, b, top=roofc)


def courtyard_house(sc, r, h, P, rng):
    x0, y0, x1, y1 = r
    w, d = x1 - x0, y1 - y0
    c = pick(rng, P['walls'])
    t = min(w, d) * 0.28
    b = sc.new_building(wspace=7, floor=9)
    roofc = mix(c, (1, 0.97, 0.9), 0.12)
    hb = h * rng.uniform(0.6, 0.8)
    box(sc, x0, y0, x1, y0 + t, 0, h - 1.2, c, STUCCO, b, top=roofc)
    parapet(sc, x0, y0, x1, y0 + t, h - 1.2, 1.2, c, STUCCO, b)
    box(sc, x0, y1 - t, x1, y1, 0, hb, c, STUCCO, b, top=roofc)
    box(sc, x0, y0 + t, x0 + t, y1 - t, 0, hb, c, STUCCO, b, top=roofc)
    box(sc, x1 - t, y0 + t, x1, y1 - t, 0, hb, c, STUCCO, b, top=roofc)
    # Courtyard floor.
    box(sc, x0 + t, y0 + t, x1 - t, y1 - t, 0, 0.4, darker(c, 0.85), STONE, b)


def domed_tower(sc, r, h, P, rng, dome_col=None, walls=None):
    """A round modern tower capped with a dome (a Middle-East modern landmark)."""
    x0, y0, x1, y1 = r
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    rad = min(x1 - x0, y1 - y0) / 2
    wall = pick(rng, walls or P.get('modern_walls', P['walls']), 0.03)
    dc = dome_col or pick(rng, P['dome_big'], 0.03)
    b = sc.new_building(wspace=4.2, floor=5.5, wwidth=2.4, wheight=3.0, wdark=0.3, rcx=cx, rcy=cy)
    b0 = sc.new_building(wspace=4.2, floor=5.5, wwidth=2.4, wheight=3.0, wdark=0.3, arch=6, archh=8)
    box(sc, x0, y0, x1, y1, 0, h * 0.25, wall, WINDOWS, b0)
    dh = rad * 0.75
    wh = h - dh - 3
    lathe(sc, cx, cy, [(rad * 0.82, 0), (rad * 0.82, wh), (rad * 0.9, wh), (rad * 0.9, wh + 1.5)], wall, WINDOWS, b, seg=26, smooth=False)
    lathe(sc, cx, cy, dome_profile(rad * 0.8, wh + 1.5, dh), dc, DOME, b, seg=26)
    cylinder(sc, cx, cy, 0.5, wh + dh, h, P.get('gold', (0.85, 0.7, 0.3)), GOLD, b, seg=6)


def tall_house(sc, r, h, P, rng, roof='hip', walls=None, roofs=None):
    """A multi-storey town house with windows and a pitched or flat roof (industrial era)."""
    x0, y0, x1, y1 = r
    w, d = x1 - x0, y1 - y0
    wall = pick(rng, walls or P['walls'])
    win = dict(wspace=rng.uniform(4.6, 6), floor=rng.uniform(7, 8.5), wwidth=2.2, wheight=4.2, wdark=0.3)
    if roof == 'flat':
        b = sc.new_building(**win)
        box(sc, x0, y0, x1, y1, 0, h - 1.4, wall, WINDOWS, b, top=mix(wall, (0.6, 0.6, 0.6), 0.4))
        parapet(sc, x0, y0, x1, y1, h - 1.4, 1.4, mix(wall, (1, 1, 1), 0.1), PLAIN, b, t=0.9)
        return
    rh = min(min(w, d) * 0.45, h * 0.4)
    roofc = pick(rng, roofs or P['roofs'], 0.05)
    b = sc.new_building(wtop=h - rh, **win)
    box(sc, x0, y0, x1, y1, 0, h - rh, wall, WINDOWS, b)
    if roof == 'hip':
        hip(sc, x0, y0, x1, y1, h - rh, rh, roofc, P.get('roofmat', ROOF_TILE), b, over=1.2)
    else:
        gable(sc, x0, y0, x1, y1, h - rh, rh, roofc, P.get('roofmat', ROOF_TILE), b, axis='x' if w >= d else 'y', over=1.2, gable_col=wall, gable_mat=WINDOWS)


def aframe(sc, r, h, P, rng):
    """A long house whose thatched roof comes down (almost) to the ground."""
    x0, y0, x1, y1 = r
    w, d = x1 - x0, y1 - y0
    thatch = pick(rng, P['thatch'], 0.07)
    wall = pick(rng, P.get('hutwalls', P['walls']))
    b = sc.new_building()
    wh = h * 0.15
    box(sc, x0 + 1, y0 + 1, x1 - 1, y1 - 1, 0, wh + 1, wall, WOOD, b)
    gable(sc, x0, y0, x1, y1, wh, h - wh, thatch, THATCH, b, axis='x' if w >= d else 'y', over=1.2, gable_col=wall, gable_mat=WOOD)


def keep(sc, r, h, P, rng, mat=STONE):
    """A square castle keep with battlements."""
    x0, y0, x1, y1 = r
    wall = pick(rng, P.get('stone', P['walls']), 0.04)
    b = sc.new_building(wspace=7, floor=11)
    box(sc, x0, y0, x1, y1, 0, h - 2.5, wall, mat, b, top=darker(wall, 0.85))
    crenellate(sc, x0 + 0.8, y0 + 0.8, x1 - 0.8, y1 - 0.8, h - 2.5, 2.5, wall, mat, b, step=3.6, w=1.9, t=1.6)


def obox(sc, cx, cy, w, d, z0, h, ang, col, mat, bid, tilt=0.0):
    """A box rotated by `ang` about the vertical axis and tilted by `tilt` (radians) around its long axis."""
    ca, sa = math.cos(ang), math.sin(ang)
    ct, st = math.cos(tilt), math.sin(tilt)
    pts = []
    for (u, v, z) in ((-w / 2, -d / 2, 0), (w / 2, -d / 2, 0), (w / 2, d / 2, 0), (-w / 2, d / 2, 0),
                      (-w / 2, -d / 2, h), (w / 2, -d / 2, h), (w / 2, d / 2, h), (-w / 2, d / 2, h)):
        v2, z2 = v * ct - z * st, v * st + z * ct
        pts.append((cx + u * ca - v2 * sa, cy + u * sa + v2 * ca, z0 + z2))
    f = [(4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    for a, b2, c, e in f:
        sc.quad(pts[a], pts[b2], pts[c], pts[e], col, mat, bid)


def ruin(sc, r, h, P, rng):
    """A broken building: the stumps of its walls, with jagged tops."""
    x0, y0, x1, y1 = r
    stone = pick(rng, P['ruin'], 0.06)
    b = sc.new_building()
    t = 3.0
    for side in range(4):
        if rng.random() < 0.25:
            continue
        if side < 2:
            ya, yb = (y0, y0 + t) if side == 0 else (y1 - t, y1)
            n = max(2, int((x1 - x0) / 3.5))
            for i in range(n):
                hh = h * rng.uniform(0.1, 1.0) ** 1.6
                if rng.random() < 0.35:
                    continue
                box(sc, x0 + (x1 - x0) * i / n, ya, x0 + (x1 - x0) * (i + 1) / n, yb, 0, hh, jit(rng, stone, 0.08), STONE, b)
        else:
            xa, xb = (x0, x0 + t) if side == 2 else (x1 - t, x1)
            n = max(2, int((y1 - y0) / 3.5))
            for i in range(n):
                hh = h * rng.uniform(0.1, 1.0) ** 1.6
                if rng.random() < 0.35:
                    continue
                box(sc, xa, y0 + (y1 - y0) * i / n, xb, y0 + (y1 - y0) * (i + 1) / n, 0, hh, jit(rng, stone, 0.08), STONE, b)
    # Rubble heaps and fallen blocks inside and around.
    for i in range(int((x1 - x0) * (y1 - y0) / 28)):
        cx, cy = rng.uniform(x0 - 3, x1 + 3), rng.uniform(y0 - 3, y1 + 3)
        s = rng.uniform(2.0, 6.0)
        c = pick(rng, P['ruin'], 0.1)
        obox(sc, cx, cy, s * rng.uniform(1, 2.2), s, rng.uniform(-1, 0.5), s * rng.uniform(0.4, 0.9), rng.uniform(0, np.pi), c, RUBBLE, b,
             tilt=rng.uniform(-0.6, 0.6))
    if rng.random() < 0.5:
        # A charred beam.
        obox(sc, (x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) * 0.8, 1.4, 0.5, 1.4, rng.uniform(0, np.pi), (0.3, 0.24, 0.18), WOOD, b,
             tilt=0.0)


def wall_ring(sc, R, h, col, mat, P, rng, towers='round', gate=True, cren=True, thick=4.0, tower_col=None):
    """A city wall: a square of side 2R around the tile center, with corner towers."""
    b = sc.new_building(wspace=12, floor=30)
    t = thick
    tc = tower_col or col
    segs = [(-R, -R, R, -R + t), (-R, R - t, R, R), (-R, -R, -R + t, R), (R - t, -R, R, R)]
    for i, (a, bb, c, d) in enumerate(segs):
        if gate and i in (1, 3):
            # A gate in the middle of the two front walls.
            gw = 9.0
            if i == 1:
                box(sc, a, bb, -gw / 2, d, 0, h, col, mat, b, top=darker(col, 0.9))
                box(sc, gw / 2, bb, c, d, 0, h, col, mat, b, top=darker(col, 0.9))
                box(sc, -gw / 2, bb, gw / 2, d, h * 0.65, h, col, mat, b)
                if cren:
                    crenellate(sc, a, bb + t * 0.6, c, d, h, 2.2, col, mat, b, step=4.0, w=2.0, t=t * 0.4, sides='X')
                for gx in (-gw / 2 - 3, gw / 2 + 3):
                    box(sc, gx - 3, bb - 1.5, gx + 3, d + 1.5, 0, h + 4, tc, mat, b)
                    if cren:
                        crenellate(sc, gx - 3, bb - 1.5, gx + 3, d + 1.5, h + 4, 2.2, tc, mat, b, step=3.0, w=1.6, t=1.2)
            else:
                box(sc, a, bb, c, -gw / 2, 0, h, col, mat, b, top=darker(col, 0.9))
                box(sc, a, gw / 2, c, d, 0, h, col, mat, b, top=darker(col, 0.9))
                box(sc, a, -gw / 2, c, gw / 2, h * 0.65, h, col, mat, b)
                if cren:
                    crenellate(sc, a + t * 0.6, bb, c, d, h, 2.2, col, mat, b, step=4.0, w=2.0, t=t * 0.4, sides='Y')
                for gy in (-gw / 2 - 3, gw / 2 + 3):
                    box(sc, a - 1.5, gy - 3, c + 1.5, gy + 3, 0, h + 4, tc, mat, b)
                    if cren:
                        crenellate(sc, a - 1.5, gy - 3, c + 1.5, gy + 3, h + 4, 2.2, tc, mat, b, step=3.0, w=1.6, t=1.2)
        else:
            box(sc, a, bb, c, d, 0, h, col, mat, b, top=darker(col, 0.9))
            if cren:
                side = 'x' if i == 0 else ('X' if i == 1 else ('y' if i == 2 else 'Y'))
                if i == 0:
                    crenellate(sc, a, bb, c, d - t * 0.6, h, 2.2, col, mat, b, step=4.0, w=2.0, t=t * 0.4, sides='x')
                else:
                    crenellate(sc, a, bb, c - t * 0.6, d, h, 2.2, col, mat, b, step=4.0, w=2.0, t=t * 0.4, sides='y')
    for (cx, cy) in ((-R, -R), (R, -R), (-R, R), (R, R)):
        cx2, cy2 = cx * (R - t / 2) / R, cy * (R - t / 2) / R
        if towers == 'none':
            continue
        if towers == 'round':
            lathe(sc, cx2, cy2, [(5.2, 0), (5.2, h + 4), (5.8, h + 4), (5.8, h + 5.5)], tc, mat, b, seg=18, smooth=False)
            if cren:
                n = 8
                for k in range(n):
                    a = 2 * np.pi * (k + 0.5) / n
                    px, py = cx2 + math.cos(a) * 5.1, cy2 + math.sin(a) * 5.1
                    box(sc, px - 0.9, py - 0.9, px + 0.9, py + 0.9, h + 5.5, h + 7.5, tc, mat, b)
        else:
            box(sc, cx2 - 5, cy2 - 5, cx2 + 5, cy2 + 5, 0, h + 4.5, tc, mat, b, top=darker(tc, 0.9))
            if cren:
                crenellate(sc, cx2 - 5, cy2 - 5, cx2 + 5, cy2 + 5, h + 4.5, 2.2, tc, mat, b, step=3.3, w=1.7, t=1.3)
