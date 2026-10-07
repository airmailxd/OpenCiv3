"""Building blocks for the city scenes: boxes, roofs, domes, towers... as triangles
added to a render3d.Scene. Coordinates are world units (pixels of the 2x art),
x and y on the ground and z up. Colors are 0..1 RGB."""
import math
import numpy as np
from render3d import (PLAIN, STUCCO, WINDOWS, ROOF_TILE, THATCH, SLATE, GLASS, BRICK,
                      METAL, DOME, STONE, WATER, FOLIAGE, WOOD, GOLD, RUBBLE, PAVING)


def box(sc, x0, y0, x1, y1, z0, z1, col, mat, bid, top=None, topmat=None):
    """An axis aligned box; `top` / `topmat` give the roof face its own color/material."""
    if x1 < x0: x0, x1 = x1, x0
    if y1 < y0: y0, y1 = y1, y0
    top = col if top is None else top
    topmat = mat if topmat is None else topmat
    p = lambda x, y, z: (x, y, z)
    # Only the faces the camera can see (+x, +y, +z), plus the others for shadows.
    sc.quad(p(x0, y0, z1), p(x1, y0, z1), p(x1, y1, z1), p(x0, y1, z1), top, topmat, bid)
    sc.quad(p(x1, y0, z0), p(x1, y1, z0), p(x1, y1, z1), p(x1, y0, z1), col, mat, bid)
    sc.quad(p(x0, y1, z0), p(x1, y1, z0), p(x1, y1, z1), p(x0, y1, z1), col, mat, bid)
    sc.quad(p(x0, y0, z0), p(x0, y1, z0), p(x0, y1, z1), p(x0, y0, z1), col, mat, bid)
    sc.quad(p(x0, y0, z0), p(x1, y0, z0), p(x1, y0, z1), p(x0, y0, z1), col, mat, bid)


def gable(sc, x0, y0, x1, y1, z0, h, col, mat, bid, axis='x', over=1.5, gable_col=None, gable_mat=None):
    """A pitched roof over the rectangle, ridge along `axis`, eaves overhanging by `over`.
    The triangular gable ends use gable_col/gable_mat (the wall's)."""
    gc = col if gable_col is None else gable_col
    gm = mat if gable_mat is None else gable_mat
    if axis == 'x':
        ym = (y0 + y1) / 2
        a, b = y0 - over, y1 + over
        drop = h * over / ((y1 - y0) / 2)
        sc.quad((x0 - over * 0.5, a, z0 - drop), (x1 + over * 0.5, a, z0 - drop), (x1 + over * 0.5, ym, z0 + h), (x0 - over * 0.5, ym, z0 + h), col, mat, bid)
        sc.quad((x0 - over * 0.5, b, z0 - drop), (x1 + over * 0.5, b, z0 - drop), (x1 + over * 0.5, ym, z0 + h), (x0 - over * 0.5, ym, z0 + h), col, mat, bid)
        for x in (x0, x1):
            sc.tri((x, y0, z0), (x, y1, z0), (x, ym, z0 + h), gc, gm, bid)
    else:
        xm = (x0 + x1) / 2
        a, b = x0 - over, x1 + over
        drop = h * over / ((x1 - x0) / 2)
        sc.quad((a, y0 - over * 0.5, z0 - drop), (a, y1 + over * 0.5, z0 - drop), (xm, y1 + over * 0.5, z0 + h), (xm, y0 - over * 0.5, z0 + h), col, mat, bid)
        sc.quad((b, y0 - over * 0.5, z0 - drop), (b, y1 + over * 0.5, z0 - drop), (xm, y1 + over * 0.5, z0 + h), (xm, y0 - over * 0.5, z0 + h), col, mat, bid)
        for y in (y0, y1):
            sc.tri((x0, y, z0), (x1, y, z0), (xm, y, z0 + h), gc, gm, bid)


def hip(sc, x0, y0, x1, y1, z0, h, col, mat, bid, over=1.5, ridge=None):
    """A hipped roof (a pyramid when the rectangle is square or ridge=0)."""
    x0 -= over; y0 -= over; x1 += over; y1 += over
    z0 -= h * over / max(1e-6, min(x1 - x0, y1 - y0) / 2)
    w, d = x1 - x0, y1 - y0
    if ridge is None:
        ridge = abs(w - d)
    xm, ym = (x0 + x1) / 2, (y0 + y1) / 2
    zt = z0 + h
    if w >= d:
        r0, r1 = (xm - ridge / 2, ym, zt), (xm + ridge / 2, ym, zt)
        sc.quad((x0, y0, z0), (x1, y0, z0), r1, r0, col, mat, bid)
        sc.quad((x0, y1, z0), (x1, y1, z0), r1, r0, col, mat, bid)
        sc.tri((x0, y0, z0), (x0, y1, z0), r0, col, mat, bid)
        sc.tri((x1, y0, z0), (x1, y1, z0), r1, col, mat, bid)
    else:
        r0, r1 = (xm, ym - ridge / 2, zt), (xm, ym + ridge / 2, zt)
        sc.quad((x0, y0, z0), (x0, y1, z0), r1, r0, col, mat, bid)
        sc.quad((x1, y0, z0), (x1, y1, z0), r1, r0, col, mat, bid)
        sc.tri((x0, y0, z0), (x1, y0, z0), r0, col, mat, bid)
        sc.tri((x0, y1, z0), (x1, y1, z0), r1, col, mat, bid)


def lathe(sc, cx, cy, profile, col, mat, bid, seg=24, sx=1.0, sy=1.0, smooth=True, cap=True):
    """A solid of revolution around the vertical axis at (cx, cy).
    profile: [(r, z), ...] from bottom to top."""
    prof = np.array(profile, float)
    angs = np.linspace(0, 2 * np.pi, seg + 1)
    ca, sa = np.cos(angs), np.sin(angs)
    # Vertex normals from the profile's tangent.
    n = len(prof)
    tang = np.zeros((n, 2))
    for i in range(n):
        a = prof[max(i - 1, 0)]; b = prof[min(i + 1, n - 1)]
        tang[i] = b - a
    pn = np.stack([tang[:, 1], -tang[:, 0]], 1)  # (dr, dz) rotated: outward
    pn /= np.maximum(np.linalg.norm(pn, axis=1, keepdims=True), 1e-9)
    for i in range(n - 1):
        (r0, z0), (r1, z1) = prof[i], prof[i + 1]
        # Sharp creases between segments that turn a lot use flat normals.
        seg_t = prof[i + 1] - prof[i]
        sn = np.array([seg_t[1], -seg_t[0]]); sn /= max(np.linalg.norm(sn), 1e-9)
        na, nb = pn[i], pn[i + 1]
        if not smooth or np.dot(na, sn) < 0.85:
            na = sn
        if not smooth or np.dot(nb, sn) < 0.85:
            nb = sn
        for j in range(seg):
            c0, s0, c1, s1 = ca[j], sa[j], ca[j + 1], sa[j + 1]
            A = (cx + r0 * c0 * sx, cy + r0 * s0 * sy, z0)
            Bv = (cx + r0 * c1 * sx, cy + r0 * s1 * sy, z0)
            Cv = (cx + r1 * c1 * sx, cy + r1 * s1 * sy, z1)
            Dv = (cx + r1 * c0 * sx, cy + r1 * s0 * sy, z1)
            nA = (na[0] * c0, na[0] * s0, na[1]); nB = (na[0] * c1, na[0] * s1, na[1])
            nC = (nb[0] * c1, nb[0] * s1, nb[1]); nD = (nb[0] * c0, nb[0] * s0, nb[1])
            if r0 < 1e-6:
                sc.tri(A, Cv, Dv, col, mat, bid, (nA, nC, nD))
            elif r1 < 1e-6:
                sc.tri(A, Bv, Cv, col, mat, bid, (nA, nB, nC))
            else:
                sc.quad(A, Bv, Cv, Dv, col, mat, bid, (nA, nB, nC, nD))
    if cap and prof[-1, 0] > 1e-6:
        r, z = prof[-1]
        for j in range(seg):
            sc.tri((cx, cy, z), (cx + r * ca[j] * sx, cy + r * sa[j] * sy, z),
                   (cx + r * ca[j + 1] * sx, cy + r * sa[j + 1] * sy, z), col, mat, bid)


def dome_profile(r, z0, h=None, k=12, onion=0.0, drum=0.0):
    """Profile of a dome of radius r starting at z0 (h: height, default r).
    onion > 0 bulges it out and gives it a pointed top."""
    h = r if h is None else h
    pts = [(r, z0 - 0.01)] if drum <= 0 else [(r, z0 - drum), (r, z0)]
    for i in range(1, k + 1):
        t = i / k * (np.pi / 2)
        rr = r * math.cos(t) * (1 + onion * math.sin(2 * t) * 0.8)
        zz = z0 + h * math.sin(t) ** (1 + onion)
        pts.append((rr, zz))
    if onion > 0:
        pts[-1] = (0.0, z0 + h * (1 + 0.35 * onion))
    else:
        pts[-1] = (0.0, z0 + h)
    return pts


def dome(sc, cx, cy, r, z0, col, mat, bid, h=None, onion=0.0, seg=24, ribs=False):
    lathe(sc, cx, cy, dome_profile(r, z0, h, onion=onion), col, mat, bid, seg=seg)


def cylinder(sc, cx, cy, r, z0, z1, col, mat, bid, seg=16, top=None, topmat=None):
    lathe(sc, cx, cy, [(r, z0), (r, z1)], col, mat, bid, seg=seg, smooth=True, cap=False)
    t = col if top is None else top
    tm = mat if topmat is None else topmat
    lathe(sc, cx, cy, [(r, z1), (0.0, z1 + 0.001)], t, tm, bid, seg=seg, smooth=False, cap=False)


def cone(sc, cx, cy, r, z0, h, col, mat, bid, seg=16):
    lathe(sc, cx, cy, [(r, z0), (0.0, z0 + h)], col, mat, bid, seg=seg, smooth=True, cap=False)


def crenellate(sc, x0, y0, x1, y1, z, h, col, mat, bid, step=4.0, w=2.2, t=1.6, sides='xyXY'):
    """Merlons along the rim of a rectangle at height z."""
    def run(ax, ay, bx, by):
        L = math.hypot(bx - ax, by - ay)
        n = max(1, int(L / step))
        for i in range(n):
            f0 = (i + 0.5) / n - w / 2 / L
            f1 = (i + 0.5) / n + w / 2 / L
            px0, py0 = ax + (bx - ax) * f0, ay + (by - ay) * f0
            px1, py1 = ax + (bx - ax) * f1, ay + (by - ay) * f1
            if abs(by - ay) < 1e-6:
                box(sc, px0, py0 - t / 2, px1, py0 + t / 2, z, z + h, col, mat, bid)
            else:
                box(sc, px0 - t / 2, py0, px0 + t / 2, py1, z, z + h, col, mat, bid)
    if 'x' in sides: run(x0, y0, x1, y0)
    if 'X' in sides: run(x0, y1, x1, y1)
    if 'y' in sides: run(x0, y0, x0, y1)
    if 'Y' in sides: run(x1, y0, x1, y1)


def parapet(sc, x0, y0, x1, y1, z, h, col, mat, bid, t=1.2):
    """A low rim wall around a flat roof."""
    box(sc, x0, y0, x1, y0 + t, z, z + h, col, mat, bid)
    box(sc, x0, y1 - t, x1, y1, z, z + h, col, mat, bid)
    box(sc, x0, y0, x0 + t, y1, z, z + h, col, mat, bid)
    box(sc, x1 - t, y0, x1, y1, z, z + h, col, mat, bid)


def curved_roof(sc, x0, y0, x1, y1, z0, h, col, mat, bid, over=3.0, lift=2.5, n=10, ridge_frac=0.45):
    """An East Asian hipped roof: concave slopes and upturned eave corners,
    built as a grid mesh over the (overhanging) rectangle."""
    x0 -= over; y0 -= over; x1 += over; y1 += over
    w, d = x1 - x0, y1 - y0
    long_x = w >= d
    us = np.linspace(0, 1, n + 1)
    pts = np.zeros((n + 1, n + 1, 3))
    half = min(w, d) / 2
    for i, u in enumerate(us):
        for j, v in enumerate(us):
            x = x0 + u * w; y = y0 + v * d
            # distance to the boundary, with the ridge along the long side
            dx = min(x - x0, x1 - x); dy = min(y - y0, y1 - y)
            if long_x:
                ex = max(0.0, (w - d) / 2 * ridge_frac + 0.0)
                dist = min(dy, dx * (half / max(half, 1e-6)))
            else:
                dist = min(dx, dy)
            t = np.clip(dist / half, 0, 1)
            zz = z0 + h * (1 - (1 - t) ** 1.8)
            # upturned corners
            cu = (1 - np.clip(dx / (half * 0.9), 0, 1)) * (1 - np.clip(dy / (half * 0.9), 0, 1))
            zz += lift * cu ** 2 * 2.2
            pts[i, j] = (x, y, zz)
    for i in range(n):
        for j in range(n):
            a, b, c, e = pts[i, j], pts[i + 1, j], pts[i + 1, j + 1], pts[i, j + 1]
            sc.tri(tuple(a), tuple(b), tuple(c), col, mat, bid)
            sc.tri(tuple(a), tuple(c), tuple(e), col, mat, bid)
    # Underside, so the overhang has thickness for shadows.
    sc.quad((x0, y0, z0 - 0.8), (x1, y0, z0 - 0.8), (x1, y1, z0 - 0.8), (x0, y1, z0 - 0.8), col, mat, bid)


def column_row(sc, x0, y0, x1, y1, z0, z1, r, n, col, mat, bid, seg=8):
    for i in range(n):
        f = (i + 0.5) / n
        cylinder(sc, x0 + (x1 - x0) * f, y0 + (y1 - y0) * f, r, z0, z1, col, mat, bid, seg=seg)
