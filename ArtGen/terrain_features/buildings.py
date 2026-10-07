"""Remake of Art/Terrain/TerrainBuildings.pcx: fortresses, colonies, the barbarian
camp, the mine and barbed wire, drawn with a small software renderer.

The sheet is a 4x4 grid of iso tile cells (128x64, 256x128 at 2x):
    col 0: fortress (ancient keep, medieval castle, star fort, bunker)
    col 1: colony by era (huts, timber houses, oil field, quonset/crane)
    col 2: barbarian camp, mine, (empty), barbed wire ring
    col 3: keep with stakes (barricade), castle with moat, star fort with moat,
           bunker inside barbed wire

World units are pixels of the 2x art. A map tile is 128x128 world units on the
ground and the projection is the Civ3 2:1 isometric one,
    sx = 128 + (x - y),   sy = 64 + (x + y) / 2 - z   (inside a 256x128 cell),
so the camera sees the +x (screen lower right) and +y (screen lower left)
faces. Scenes are lists of triangles rasterized with a z-buffer at 4x
supersampling, lit from the upper left (north-west) with a shadow map, ambient
occlusion from a top-down height map, procedural surface texture and a thin
darker contour, then box-filtered down.
"""
import math
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'common'))
import civ3art  # noqa: E402

REL = 'Art/Terrain/TerrainBuildings.pcx'
CW, CH = 256, 128       # a cell at 2x
SS = 4                  # supersampling

_EL = math.radians(48)
_H = np.array([-0.82, 0.57]) / np.linalg.norm([-0.82, 0.57])
LIGHT = np.array([math.cos(_EL) * _H[0], math.cos(_EL) * _H[1], math.sin(_EL)])
VIEW = np.array([1.0, 1.0, 1.0]) / math.sqrt(3)

# materials
(PLAIN, STONE, EARTH, WOOD, THATCH, SLATE, METAL, CONCRETE, CANVAS, FOLIAGE,
 WIRE, DARK, ROCK, PLANKS, CORRUGATED, STEEL) = range(16)


def rgb(*c):
    return np.array(c, np.float64) / 255.0


def G(px, py):
    """World ground (x, y) of a point given in the original 1x cell's pixels."""
    return ((px - 64) + 2 * (py - 32), 2 * (py - 32) - (px - 64))


# ------------------------------------------------------------------ noise
def _hash(ix, iy, iz, seed):
    h = (ix * 73856093) ^ (iy * 19349663) ^ (iz * 83492791) ^ (seed * 2654435761)
    h = h & 0xffffffff
    h = ((h ^ (h >> 13)) * 1274126177) & 0xffffffff
    h = h ^ (h >> 16)
    return (h & 0xffff) / 65535.0


def vnoise(x, y, z=None, seed=0):
    if z is None:
        z = np.zeros_like(x)
    xi, yi, zi = np.floor(x), np.floor(y), np.floor(z)
    fx, fy, fz = x - xi, y - yi, z - zi
    fx, fy, fz = [f * f * (3 - 2 * f) for f in (fx, fy, fz)]
    xi, yi, zi = xi.astype(np.int64), yi.astype(np.int64), zi.astype(np.int64)
    out = 0
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                w = (fx if dx else 1 - fx) * (fy if dy else 1 - fy) * (fz if dz else 1 - fz)
                out = out + w * _hash(xi + dx, yi + dy, zi + dz, seed)
    return out


def fbm(x, y, z=None, seed=0, octaves=3):
    tot, amp, norm = 0, 1.0, 0
    for o in range(octaves):
        f = 2 ** o
        tot = tot + amp * vnoise(x * f, y * f, None if z is None else z * f, seed + 17 * o)
        norm += amp
        amp *= 0.5
    return tot / norm


# ------------------------------------------------------------------ geometry
def _ear_clip(pts):
    """Triangulates a simple 2D polygon; returns index triples."""
    n = len(pts)
    idx = list(range(n))
    area = sum(pts[i][0] * pts[(i + 1) % n][1] - pts[(i + 1) % n][0] * pts[i][1] for i in range(n))
    sgn = 1 if area > 0 else -1
    tris = []

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])

    guard = 0
    while len(idx) > 3 and guard < 10000:
        guard += 1
        m = len(idx)
        for k in range(m):
            i0, i1, i2 = idx[(k - 1) % m], idx[k], idx[(k + 1) % m]
            a, b, c = pts[i0], pts[i1], pts[i2]
            if cross(a, b, c) * sgn <= 1e-9:
                continue
            ok = True
            for j in idx:
                if j in (i0, i1, i2):
                    continue
                p = pts[j]
                if (cross(a, b, p) * sgn >= 0 and cross(b, c, p) * sgn >= 0 and cross(c, a, p) * sgn >= 0):
                    ok = False
                    break
            if ok:
                tris.append((i0, i1, i2))
                idx.pop(k)
                break
        else:
            break
    if len(idx) == 3:
        tris.append(tuple(idx))
    return tris


class Scene:
    def __init__(self):
        self.V, self.N, self.C, self.M, self.O = [], [], [], [], []
        self.decals = []
        self.oid = 0
        self.cur = 0
        self.off = None     # optional (dx, dy) shift of everything added

    def obj(self):
        self.oid += 1
        self.cur = self.oid
        return self.oid

    def tri(self, a, b, c, col, mat, normals=None):
        if self.off is not None:
            ox, oy = self.off
            a, b, c = [(p[0] + ox, p[1] + oy, p[2]) for p in (a, b, c)]
        self.V.append((a, b, c))
        self.N.append(normals)
        self.C.append(np.asarray(col, float))
        self.M.append(mat)
        self.O.append(self.cur)

    def quad(self, a, b, c, d, col, mat, normals=None):
        if normals is None:
            self.tri(a, b, c, col, mat)
            self.tri(a, c, d, col, mat)
        else:
            na, nb, nc, nd = normals
            self.tri(a, b, c, col, mat, (na, nb, nc))
            self.tri(a, c, d, col, mat, (na, nc, nd))

    def flat_poly(self, pts2, z, col, mat):
        for i, j, k in _ear_clip(pts2):
            self.tri((*pts2[i], z), (*pts2[j], z), (*pts2[k], z), col, mat)

    def prism(self, poly, z0, z1, col, mat, top=None, top_col=None, top_mat=None, cap=True):
        """Extrudes a 2D polygon from z0 to z1; `top` is an optional polygon (same
        vertex count) for the top outline, for sloped (battered) walls."""
        top = poly if top is None else top
        n = len(poly)
        for i in range(n):
            j = (i + 1) % n
            self.quad((*poly[i], z0), (*poly[j], z0), (*top[j], z1), (*top[i], z1), col, mat)
        if cap:
            self.flat_poly(top, z1, col if top_col is None else top_col, mat if top_mat is None else top_mat)

    def box(self, x0, y0, z0, x1, y1, z1, col, mat, top_col=None, top_mat=None, inset=0.0):
        p = [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]
        t = [(x0 + inset, y0 + inset), (x1 - inset, y0 + inset), (x1 - inset, y1 - inset), (x0 + inset, y1 - inset)]
        self.prism(p, z0, z1, col, mat, top=t, top_col=top_col, top_mat=top_mat)

    def cbox(self, cx, cy, sx, sy, z0, z1, col, mat, **kw):
        self.box(cx - sx / 2, cy - sy / 2, z0, cx + sx / 2, cy + sy / 2, z1, col, mat, **kw)

    def cylinder(self, cx, cy, r, z0, z1, col, mat, n=28, r1=None, top=True, top_col=None, top_mat=None, ry=None):
        r1 = r if r1 is None else r1
        ry = 1.0 if ry is None else ry
        a = np.linspace(0, 2 * np.pi, n + 1)
        slope = (r - r1) / max(z1 - z0, 1e-6)
        for i in range(n):
            pts, nrm = [], []
            for k in (i, i + 1):
                c, s = math.cos(a[k]), math.sin(a[k])
                nn = np.array([c, s / ry, slope]); nn /= np.linalg.norm(nn)
                pts.append(((cx + r * c, cy + r * s * ry, z0), (cx + r1 * c, cy + r1 * s * ry, z1)))
                nrm.append(nn)
            (b0, t0), (b1, t1) = pts
            self.quad(b0, b1, t1, t0, col, mat, (nrm[0], nrm[1], nrm[1], nrm[0]))
        if top and r1 > 0:
            ring = [(cx + r1 * math.cos(t), cy + r1 * math.sin(t) * ry) for t in a[:-1]]
            self.flat_poly(ring, z1, col if top_col is None else top_col, mat if top_mat is None else top_mat)

    def cone(self, cx, cy, r, z0, h, col, mat, n=28, ry=1.0):
        a = np.linspace(0, 2 * np.pi, n + 1)
        apex = (cx, cy, z0 + h)
        for i in range(n):
            ps, ns = [], []
            for k in (i, i + 1):
                c, s = math.cos(a[k]), math.sin(a[k])
                ps.append((cx + r * c, cy + r * s * ry, z0))
                nn = np.array([c * h, s * h / ry, r]); ns.append(nn / np.linalg.norm(nn))
            na = (ns[0] + ns[1]); na /= np.linalg.norm(na)
            self.tri(ps[0], ps[1], apex, col, mat, (ns[0], ns[1], na))

    def dome(self, cx, cy, rx, ry, z0, h, col, mat, n=28, m=10):
        a = np.linspace(0, 2 * np.pi, n + 1)
        e = np.linspace(0, np.pi / 2, m + 1)

        def P(i, j):
            c, s = math.cos(a[i]), math.sin(a[i])
            ce, se = math.cos(e[j]), math.sin(e[j])
            p = (cx + rx * ce * c, cy + ry * ce * s, z0 + h * se)
            nn = np.array([ce * c / rx, ce * s / ry, se / h]); nn /= np.linalg.norm(nn)
            return p, nn
        for i in range(n):
            for j in range(m):
                (p0, n0), (p1, n1), (p2, n2), (p3, n3) = P(i, j), P(i + 1, j), P(i + 1, j + 1), P(i, j + 1)
                self.quad(p0, p1, p2, p3, col, mat, (n0, n1, n2, n3))

    def hcyl(self, x0, y0, along, length, r, z0, col, mat, n=20, end_col=None, end_mat=None, ends=True):
        """Half cylinder lying on the ground (a quonset hut), axis from (x0, y0) along
        unit vector `along` for `length`."""
        ax = np.array([along[0], along[1], 0.0]); ax /= np.linalg.norm(ax)
        side = np.array([-ax[1], ax[0], 0.0])
        up = np.array([0, 0, 1.0])
        base = np.array([x0, y0, z0])
        t = np.linspace(0, np.pi, n + 1)
        for i in range(n):
            ps, ns = [], []
            for k in (i, i + 1):
                d = side * math.cos(t[k]) + up * math.sin(t[k])
                ps.append(d * r); ns.append(d)
            a0, a1 = base + ps[0], base + ps[1]
            b0, b1 = a0 + ax * length, a1 + ax * length
            self.quad(tuple(a0), tuple(a1), tuple(b1), tuple(b0), col, mat, (ns[0], ns[1], ns[1], ns[0]))
        if ends:
            for e in (0, length):
                ring = [base + ax * e + (side * math.cos(tt) + up * math.sin(tt)) * r for tt in t]
                c0 = base + ax * e
                for i in range(n):
                    self.tri(tuple(c0), tuple(ring[i]), tuple(ring[i + 1]),
                             col if end_col is None else end_col, mat if end_mat is None else end_mat)

    def gable(self, x0, y0, x1, y1, z0, hr, col, mat, along='x', gable_col=None, gable_mat=None, over=0.0):
        """A gable roof over the rectangle, eaves at z0, ridge hr higher, running
        along x or y. `over` extends the eaves sideways."""
        gc = col if gable_col is None else gable_col
        gm = mat if gable_mat is None else gable_mat
        if along == 'x':
            ym = (y0 + y1) / 2
            ya, yb = y0 - over, y1 + over
            r0, r1 = (x0, ym, z0 + hr), (x1, ym, z0 + hr)
            self.quad((x0, ya, z0), (x1, ya, z0), r1, r0, col, mat)
            self.quad((x0, yb, z0), (x1, yb, z0), r1, r0, col, mat)
            self.tri((x0, y0, z0), (x0, y1, z0), r0, gc, gm)
            self.tri((x1, y0, z0), (x1, y1, z0), r1, gc, gm)
        else:
            xm = (x0 + x1) / 2
            xa, xb = x0 - over, x1 + over
            r0, r1 = (xm, y0, z0 + hr), (xm, y1, z0 + hr)
            self.quad((xa, y0, z0), (xa, y1, z0), r1, r0, col, mat)
            self.quad((xb, y0, z0), (xb, y1, z0), r1, r0, col, mat)
            self.tri((x0, y0, z0), (x1, y0, z0), r0, gc, gm)
            self.tri((x0, y1, z0), (x1, y1, z0), r1, gc, gm)

    def house(self, cx, cy, sx, sy, h, hr, wall_col, wall_mat, roof_col, roof_mat, along='x', over=1.0):
        self.cbox(cx, cy, sx, sy, 0, h, wall_col, wall_mat)
        self.gable(cx - sx / 2 - (over if along == 'x' else 0), cy - sy / 2 - (over if along == 'y' else 0),
                   cx + sx / 2 + (over if along == 'x' else 0), cy + sy / 2 + (over if along == 'y' else 0),
                   h, hr, roof_col, roof_mat, along=along, gable_col=wall_col, gable_mat=wall_mat, over=over)

    def beam(self, p0, p1, w, col, mat, w1=None):
        """A square-section beam between two 3D points (w1 tapers the far end)."""
        p0, p1 = np.asarray(p0, float), np.asarray(p1, float)
        d = p1 - p0
        L = np.linalg.norm(d)
        if L < 1e-6:
            return
        d /= L
        ref = np.array([0, 0, 1.0]) if abs(d[2]) < 0.9 else np.array([1.0, 0, 0])
        u = np.cross(d, ref); u /= np.linalg.norm(u)
        v = np.cross(d, u)
        w1 = w if w1 is None else w1
        c0 = [p0 + (u * a + v * b) * w / 2 for a, b in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        c1 = [p1 + (u * a + v * b) * w1 / 2 for a, b in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        for i in range(4):
            j = (i + 1) % 4
            self.quad(tuple(c0[i]), tuple(c0[j]), tuple(c1[j]), tuple(c1[i]), col, mat)
        self.quad(*[tuple(c) for c in c1], col, mat) if w1 > 0 else None
        self.quad(*[tuple(c) for c in c0], col, mat)

    def heightfield(self, f, x0, x1, y0, y1, step, col, mat):
        xs = np.arange(x0, x1 + step / 2, step)
        ys = np.arange(y0, y1 + step / 2, step)
        X, Y = np.meshgrid(xs, ys)
        Z = np.maximum(f(X, Y), 0)
        gy, gx = np.gradient(Z, step)
        Nn = np.dstack([-gx, -gy, np.ones_like(Z)])
        Nn /= np.linalg.norm(Nn, axis=2, keepdims=True)
        for j in range(len(ys) - 1):
            for i in range(len(xs) - 1):
                zz = Z[j:j + 2, i:i + 2]
                if zz.max() <= 0.05:
                    continue
                p = [(X[j, i], Y[j, i], Z[j, i]), (X[j, i + 1], Y[j, i + 1], Z[j, i + 1]),
                     (X[j + 1, i + 1], Y[j + 1, i + 1], Z[j + 1, i + 1]), (X[j + 1, i], Y[j + 1, i], Z[j + 1, i])]
                n = [Nn[j, i], Nn[j, i + 1], Nn[j + 1, i + 1], Nn[j + 1, i]]
                self.quad(*p, col, mat, n)

    def ribbon(self, pts, nrms, w, col, mat):
        """A thin ribbon (wire) along a 3D polyline, facing the camera."""
        pts = np.asarray(pts, float)
        for i in range(len(pts) - 1):
            a, b = pts[i], pts[i + 1]
            d = b - a
            off = np.cross(d, VIEW)
            ln = np.linalg.norm(off)
            if ln < 1e-9:
                continue
            off = off / ln * w / 2
            self.quad(tuple(a - off), tuple(b - off), tuple(b + off), tuple(a + off), col, mat,
                      (nrms[i], nrms[i + 1], nrms[i + 1], nrms[i]))

    def sprism(self, poly, z0, z1, col, mat, top=None, top_col=None, top_mat=None, cap=True):
        """Like prism, but with smooth (averaged) side normals, for rounded outlines."""
        top = poly if top is None else top
        P = np.asarray(poly, float)
        T = np.asarray(top, float)
        n = len(P)
        area = sum(P[i, 0] * P[(i + 1) % n, 1] - P[(i + 1) % n, 0] * P[i, 1] for i in range(n))
        sgn = 1 if area > 0 else -1
        nv = []
        for i in range(n):
            e0 = P[i] - P[i - 1]
            e1 = P[(i + 1) % n] - P[i]
            m = np.array([e0[1], -e0[0]]) / np.linalg.norm(e0) + np.array([e1[1], -e1[0]]) / np.linalg.norm(e1)
            m = m / np.linalg.norm(m) * sgn
            slope = (P[i] - T[i]) @ m / max(z1 - z0, 1e-6)
            v = np.array([m[0], m[1], slope]); nv.append(v / np.linalg.norm(v))
        for i in range(n):
            j = (i + 1) % n
            self.quad((*P[i], z0), (*P[j], z0), (*T[j], z1), (*T[i], z1), col, mat, (nv[i], nv[j], nv[j], nv[i]))
        if cap:
            self.flat_poly([tuple(p) for p in T], z1, col if top_col is None else top_col,
                           mat if top_mat is None else top_mat)

    def decal(self, fn):
        """A ground decal: fn(x, y) -> (rgb (...,3), alpha (...)) drawn on z=0."""
        if self.off is not None:
            ox, oy = self.off
            self.decals.append(lambda x, y, f=fn: f(x - ox, y - oy))
            return
        self.decals.append(fn)

    def arrays(self):
        V = np.array(self.V, np.float64).reshape(-1, 3, 3)
        fn = np.cross(V[:, 1] - V[:, 0], V[:, 2] - V[:, 0])
        ln = np.linalg.norm(fn, axis=1, keepdims=True)
        fn = fn / np.maximum(ln, 1e-12)
        fn *= np.where((fn @ VIEW) < 0, -1, 1)[:, None]
        N = np.repeat(fn[:, None, :], 3, axis=1)
        for i, n in enumerate(self.N):
            if n is not None:
                N[i] = np.asarray(n, float)
        keep = ln[:, 0] > 1e-9
        return V[keep], N[keep], np.array(self.C)[keep], np.array(self.M)[keep], np.array(self.O)[keep]


# ------------------------------------------------------------------ raster
def rasterize(P2, D, W, H, want_bary=True):
    zb = np.full((H, W), -np.inf)
    tid = np.full((H, W), -1, np.int32)
    bary = np.zeros((H, W, 2), np.float32) if want_bary else None
    for i in range(len(P2)):
        (x0, y0), (x1, y1), (x2, y2) = P2[i]
        mnx = max(int(math.floor(min(x0, x1, x2) - 0.5)), 0)
        mxx = min(int(math.ceil(max(x0, x1, x2) - 0.5)), W - 1)
        mny = max(int(math.floor(min(y0, y1, y2) - 0.5)), 0)
        mxy = min(int(math.ceil(max(y0, y1, y2) - 0.5)), H - 1)
        if mnx > mxx or mny > mxy:
            continue
        det = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2)
        if abs(det) < 1e-9:
            continue
        X, Y = np.meshgrid(np.arange(mnx, mxx + 1) + 0.5, np.arange(mny, mxy + 1) + 0.5)
        l0 = ((y1 - y2) * (X - x2) + (x2 - x1) * (Y - y2)) / det
        l1 = ((y2 - y0) * (X - x2) + (x0 - x2) * (Y - y2)) / det
        l2 = 1 - l0 - l1
        e = -1e-7
        inside = (l0 >= e) & (l1 >= e) & (l2 >= e)
        if not inside.any():
            continue
        d = l0 * D[i, 0] + l1 * D[i, 1] + l2 * D[i, 2]
        sub = zb[mny:mxy + 1, mnx:mxx + 1]
        upd = inside & (d > sub)
        sub[upd] = d[upd]
        tid[mny:mxy + 1, mnx:mxx + 1][upd] = i
        if want_bary:
            bs = bary[mny:mxy + 1, mnx:mxx + 1]
            bs[..., 0][upd] = l0[upd]
            bs[..., 1][upd] = l1[upd]
    return zb, tid, bary


def screen(V):
    sx = (CW / 2 + V[..., 0] - V[..., 1]) * SS
    sy = (CH / 2 + (V[..., 0] + V[..., 1]) / 2 - V[..., 2]) * SS
    return np.stack([sx, sy], -1)


# ------------------------------------------------------------------ texture
def _tangent(nrm):
    t = np.stack([-nrm[:, 1], nrm[:, 0], np.zeros(len(nrm))], -1)
    ln = np.linalg.norm(t, axis=1, keepdims=True)
    t = np.where(ln > 1e-3, t / np.maximum(ln, 1e-9), np.array([1.0, 0, 0]))
    return t


def _lines(u, period, width):
    """1 on thin lines every `period`, smooth."""
    f = np.abs((u / period) % 1.0 - 0.5) * period   # distance to mid
    d = period / 2 - f                               # distance to nearest line
    return np.clip(1 - d / width, 0, 1)


def texture(col, mat, P, nrm, fn):
    """Multiplies albedo by a procedural surface pattern per material."""
    x, y, z = P[:, 0], P[:, 1], P[:, 2]
    t = _tangent(fn)
    u = (P * t).sum(1)
    vert = np.abs(fn[:, 2]) < 0.6
    out = col.copy()
    k = np.ones(len(P))
    n1 = fbm(x * 0.12, y * 0.12, z * 0.12, seed=3)
    k *= 0.95 + 0.10 * n1

    m = mat == STONE
    if m.any():
        row = np.floor(z[m] / 4.2)
        uu = u[m] + (row % 2) * 3.5
        joint_h = _lines(z[m] + 2.1, 4.2, 0.55)
        joint_v = _lines(uu, 7.0, 0.5)
        blk = np.floor(uu / 7.0)
        rnd = _hash(blk.astype(np.int64), row.astype(np.int64), np.zeros_like(blk, np.int64), 11)
        top = ~vert[m]
        jt = np.maximum(_lines(x[m], 7, 0.5), _lines(y[m] + 3.5 * (np.floor(x[m] / 7) % 2), 7, 0.5))
        rt = _hash(np.floor(x[m] / 7).astype(np.int64), np.floor(y[m] / 7).astype(np.int64), np.zeros_like(blk, np.int64), 5)
        joint = np.where(top, jt * 0.6, np.maximum(joint_h, joint_v))
        rr = np.where(top, rt, rnd)
        k[m] *= (0.93 + 0.12 * rr) * (1 - 0.22 * joint)
    m = mat == ROCK
    if m.any():
        k[m] *= 0.85 + 0.3 * fbm(x[m] * 0.35, y[m] * 0.35, z[m] * 0.35, seed=9)
    m = mat == EARTH
    if m.any():
        g = fbm(x[m] * 0.09, y[m] * 0.09, None, seed=21, octaves=3)
        grass = np.clip((g - 0.45) * 4, 0, 1)[:, None]
        green = np.array([0.42, 0.55, 0.20])
        out[m] = out[m] * (1 - 0.6 * grass) + green * 0.6 * grass
        k[m] *= 0.92 + 0.14 * fbm(x[m] * 0.4, y[m] * 0.4, None, seed=8)
    m = (mat == WOOD) | (mat == PLANKS)
    if m.any():
        # planks run vertically on walls, along x on flat surfaces
        uu = np.where(vert[m], u[m], x[m] + y[m] * 0.0)
        k[m] *= (1 - 0.25 * _lines(uu, 3.0, 0.45)) * (0.92 + 0.12 * vnoise(uu * 0.4, z[m] * 0.08, None, seed=4))
    m = mat == THATCH
    if m.any():
        s = vnoise(u[m] * 0.9, z[m] * 0.15, None, seed=6)
        k[m] *= (0.86 + 0.22 * s) * (1 - 0.15 * _lines(z[m], 3.5, 0.4))
    m = mat == SLATE
    if m.any():
        row = np.floor(z[m] / 2.6)
        k[m] *= (1 - 0.25 * _lines(z[m], 2.6, 0.35)) * (1 - 0.15 * _lines(u[m] + (row % 2) * 2.5, 5, 0.35)) \
            * (0.94 + 0.1 * _hash(np.floor((u[m] + (row % 2) * 2.5) / 5).astype(np.int64), row.astype(np.int64), 0, 2))
    m = mat == CORRUGATED
    if m.any():
        # ribs run across the hut (perpendicular to its axis); u is along the axis
        k[m] *= 0.9 + 0.14 * np.sin(P[m, 0] * 0.0 + _axis_coord[m] * 2 * np.pi / 3.2)
    m = mat == CONCRETE
    if m.any():
        stain = fbm(x[m] * 0.08, y[m] * 0.08, z[m] * 0.08, seed=31)
        k[m] *= 0.9 + 0.16 * stain
        moss = np.clip((fbm(x[m] * 0.15, y[m] * 0.15, z[m] * 0.15, seed=33) - 0.55) * 3, 0, 1)[:, None]
        out[m] = out[m] * (1 - 0.45 * moss) + np.array([0.55, 0.62, 0.22]) * 0.45 * moss
    m = mat == FOLIAGE
    if m.any():
        k[m] *= 0.75 + 0.45 * fbm(x[m] * 0.5, y[m] * 0.5, z[m] * 0.5, seed=12)
    m = mat == CANVAS
    if m.any():
        k[m] *= 1 - 0.12 * _lines(u[m], 6.0, 0.4)
    return np.clip(out * k[:, None], 0, 1)


_axis_coord = None


# ------------------------------------------------------------------ render
def render(scene, contour=0.5):
    global _axis_coord
    V, N, C, M, O = scene.arrays()
    W, H = CW * SS, CH * SS
    P2 = screen(V)
    D = V.sum(2)
    zb, tid, bary = rasterize(P2, D, W, H)
    geo = tid >= 0

    # world position of every pixel (ground plane where there is no geometry)
    xs = (np.arange(W) + 0.5) / SS - CW / 2
    ys = (np.arange(H) + 0.5) / SS - CH / 2
    U, Vv = np.meshgrid(xs, ys)           # U = x - y, Vv = (x + y)/2 - z
    zz = np.where(geo, (zb - 2 * Vv) / 3, 0)  # x + y + z = d, x + y = 2(Vv + z)
    s = 2 * (Vv + zz)
    PX, PY = (s + U) / 2, (s - U) / 2
    Pw = np.dstack([PX, PY, zz])

    # ---- shadow map along the light
    e1 = np.cross(LIGHT, [0, 0, 1.0]); e1 /= np.linalg.norm(e1)
    e2 = np.cross(e1, LIGHT)
    res = 0.25
    la, lb = V @ e1, V @ e2
    a0, b0 = la.min() - 4, lb.min() - 4
    SW = int((la.max() - a0 + 4) / res) + 1
    SH = int((lb.max() - b0 + 4) / res) + 1
    sm, _, _ = rasterize(np.stack([(la - a0) / res, (lb - b0) / res], -1), V @ LIGHT, SW, SH, want_bary=False)
    pa = ((Pw @ e1) - a0) / res
    pb = ((Pw @ e2) - b0) / res
    pd = Pw @ LIGHT
    lit = np.zeros((H, W))
    taps = [(-1, -1), (0, -1), (1, -1), (-1, 0), (0, 0), (1, 0), (-1, 1), (0, 1), (1, 1)]
    for da, db in taps:
        ia = np.clip(np.floor(pa + da * 1.5).astype(int), 0, SW - 1)
        ib = np.clip(np.floor(pb + db * 1.5).astype(int), 0, SH - 1)
        lit += (pd >= sm[ib, ia] - 0.9)
    lit /= len(taps)

    # ---- top-down height map for ambient occlusion
    hres = 0.5
    hm, _, _ = rasterize(np.stack([(V[..., 0] + 128) / hres, (V[..., 1] + 128) / hres], -1), V[..., 2],
                         int(256 / hres), int(256 / hres), want_bary=False)
    hm = np.maximum(np.where(np.isfinite(hm), hm, 0), 0)
    hm = np.where(hm > 2.5, hm, 0)   # flat ground patches don't occlude
    hb = ndimage.gaussian_filter(hm, 5 / hres) * 0.6 + ndimage.gaussian_filter(hm, 2 / hres) * 0.4
    hi = np.clip(((PX + 128) / hres).astype(int), 0, hm.shape[1] - 1)
    hj = np.clip(((PY + 128) / hres).astype(int), 0, hm.shape[0] - 1)
    hbp = hb[hj, hi]

    out = np.zeros((H, W, 4))
    # ---- geometry
    g = np.nonzero(geo)
    t = tid[g]
    l0, l1 = bary[g][:, 0], bary[g][:, 1]
    l2 = 1 - l0 - l1
    nrm = N[t, 0] * l0[:, None] + N[t, 1] * l1[:, None] + N[t, 2] * l2[:, None]
    nrm /= np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), 1e-9)
    fnn = np.cross(V[t, 1] - V[t, 0], V[t, 2] - V[t, 0])
    fnn /= np.maximum(np.linalg.norm(fnn, axis=1, keepdims=True), 1e-9)
    P = Pw[g]
    mat = M[t]
    # axis coordinate for corrugation: along the tangent of the face normal
    tg = _tangent(np.where(np.abs(fnn[:, 2:3]) > 0.9, np.array([0, 1.0, 0]), fnn))
    _axis_coord = (P * np.stack([tg[:, 1], -tg[:, 0], np.zeros(len(tg))], -1)).sum(1)
    _axis_coord = scene_axis_coord(scene, P, t, mat)
    alb = texture(C[t], mat, P, nrm, fnn)
    ndl = np.clip(nrm @ LIGHT, 0, 1)
    sh = lit[g]
    shade = 0.50 + 0.10 * nrm[:, 2] + 0.62 * ndl * sh
    ao = np.clip((hbp[g] - P[:, 2]) / 12, 0, 1)
    shade *= 1 - 0.38 * ao
    shade *= 1 - 0.18 * np.exp(-P[:, 2] / 2.5)
    col = alb * shade[:, None]
    # specular for metal / water-like
    hv = LIGHT + VIEW; hv /= np.linalg.norm(hv)
    spec = np.clip(nrm @ hv, 0, 1) ** 24 * sh
    ks = np.where(np.isin(mat, [METAL, STEEL, CORRUGATED, WIRE]), 0.35, 0.05)
    col += (ks * spec)[:, None]
    # subtle rim light on the lit side
    rim = (1 - np.clip(nrm @ VIEW, 0, 1)) ** 3 * np.clip(nrm @ np.array([LIGHT[0], LIGHT[1], 0]), 0, 1)
    col += 0.10 * rim[:, None]
    out[g[0], g[1], :3] = np.clip(col, 0, 1)
    out[g[0], g[1], 3] = 1

    # ---- contour: silhouette (inside) and edges in front of farther surfaces
    dist = ndimage.distance_transform_edt(geo)
    edge = np.clip((1.15 * SS + 0.5 - dist) / (0.6 * SS), 0, 1) * geo
    zf = np.where(geo, zb, -1e9)
    nb = ndimage.grey_dilation(zf, size=(2 * SS + 1, 2 * SS + 1))
    behind = geo & (nb - zf > 7)
    oid = np.zeros((H, W), int); oid[g] = O[t]
    edge = np.maximum(edge, behind * 0.8)
    # thin things (stakes, poles, wire) would turn all contour: weaken it there
    thick = ndimage.maximum_filter(dist, size=2 * SS + 1)
    edge *= np.clip((thick - 1.5) / (1.6 * SS), 0.3, 1)
    wire = np.zeros((H, W), bool); wire[g] = mat == WIRE
    edge *= np.where(wire, 0.25, 1)
    out[..., :3] *= (1 - contour * edge)[..., None]

    # ---- ground: decals, shadows and occlusion on the ground plane
    ng = ~geo
    gx, gy = PX[ng], PY[ng]
    dec_rgb = np.zeros((len(gx), 3))
    dec_a = np.zeros(len(gx))
    for fn in scene.decals:
        c, a = fn(gx, gy)
        dec_rgb = dec_rgb * (1 - a[:, None]) + c * a[:, None]
        dec_a = dec_a + a * (1 - dec_a)
    shad = 1 - lit[ng]
    aog = 0.42 * (1 - np.exp(-hbp[ng] / 7))
    dark = 1 - (1 - 0.42 * shad) * (1 - aog)
    # keep shadows from spilling far over the neighbouring tiles
    dia = (np.abs(gx - gy) + np.abs(gx + gy)) / (CW / 2)
    dark *= np.clip((1.12 - dia) / 0.2, 0, 1)
    a_tot = dec_a + (1 - dec_a) * dark
    prem = dec_rgb * (1 - dark)[:, None]
    out[ng] = np.concatenate([prem, a_tot[:, None]], 1)
    out[g[0], g[1], :3] *= 1  # geometry already premultiplied (alpha 1)

    # ---- downsample (premultiplied box filter)
    o = out.reshape(CH, SS, CW, SS, 4).mean((1, 3))
    a = o[..., 3:4]
    rgbo = np.where(a > 1e-6, o[..., :3] / np.maximum(a, 1e-6), 0)
    return np.clip(np.dstack([rgbo, a]) * 255 + 0.5, 0, 255).astype(np.uint8)


def scene_axis_coord(scene, P, t, mat):
    ax = getattr(scene, 'corr_axis', None)
    if ax is None:
        return np.zeros(len(P))
    return P[:, 0] * ax[0] + P[:, 1] * ax[1]


# ------------------------------------------------------------------ helpers for scenes
def patch_decal(blobs, col, seed=0, grass=0.35, soft=3.0, rough=0.35, col2=None, alpha=0.95):
    """A soft-edged ground patch made of ellipses (cx, cy, rx, ry) in world units."""
    col = np.asarray(col, float)
    col2 = np.array([0.40, 0.52, 0.18]) if col2 is None else np.asarray(col2, float)

    def fn(x, y):
        f = np.full(x.shape, -1e9)
        for cx, cy, rx, ry in blobs:
            d = np.sqrt(((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2)
            f = np.maximum(f, (1 - d) * min(rx, ry))
        f = f + rough * 10 * (fbm(x * 0.09, y * 0.09, None, seed=seed + 1) - 0.5)
        a = np.clip(f / soft, 0, 1)
        n = fbm(x * 0.07, y * 0.07, None, seed=seed + 2)
        gm = np.clip((n - (0.55 - grass * 0.5)) * 3.5, 0, 1)[:, None]
        c = col * (1 - gm) + col2 * gm
        c = c * (0.9 + 0.18 * fbm(x * 0.35, y * 0.35, None, seed=seed + 3))[:, None]
        return np.clip(c, 0, 1), a * alpha
    return fn


def water_decal(inner, outer, water, bank, seed=0):
    """A moat between two signed fields (>0 inside, in world units)."""
    water, bank = np.asarray(water, float), np.asarray(bank, float)

    def fn(x, y):
        fo, fi = outer(x, y), inner(x, y)
        a = np.clip(fo / 1.2, 0, 1) * np.clip((1.5 - fi) / 1.2, 0, 1)
        rim = np.clip(1 - (fo - 1.5) / 1.5, 0, 1)
        rim_in = np.clip(1 - (-fi - 0.5) / 1.5, 0, 1)
        depth = np.clip(np.minimum(fo - 3, -fi - 2) / 6, 0, 1)
        w = water * (1.06 - 0.22 * depth)[:, None]
        w = w * (0.95 + 0.1 * fbm(x * 0.05 + y * 0.1, y * 0.05, None, seed=seed + 4))[:, None]
        b = np.maximum(rim, rim_in)[:, None]
        c = w * (1 - b) + bank * b
        return np.clip(c, 0, 1), a
    return fn


def rect_field(cx, cy, hx, hy, r=4.0):
    """Signed distance (positive inside) of a rounded rectangle."""
    def f(x, y):
        dx = np.abs(x - cx) - (hx - r)
        dy = np.abs(y - cy) - (hy - r)
        out = np.sqrt(np.maximum(dx, 0) ** 2 + np.maximum(dy, 0) ** 2) + np.minimum(np.maximum(dx, dy), 0)
        return r - out
    return f


def poly_field(poly):
    """Signed distance (positive inside) of a polygon."""
    P = np.asarray(poly, float)

    def f(x, y):
        d = np.full(x.shape, 1e9)
        inside = np.zeros(x.shape, bool)
        n = len(P)
        for i in range(n):
            a, b = P[i], P[(i + 1) % n]
            ab = b - a
            t = np.clip(((x - a[0]) * ab[0] + (y - a[1]) * ab[1]) / (ab @ ab), 0, 1)
            d = np.minimum(d, np.hypot(x - a[0] - t * ab[0], y - a[1] - t * ab[1]))
            cond = ((a[1] > y) != (b[1] > y)) & (x < (b[0] - a[0]) * (y - a[1]) / (b[1] - a[1] + 1e-12) + a[0])
            inside ^= cond
        return np.where(inside, d, -d)
    return f


def offset_poly(poly, d):
    """Moves every vertex of a polygon inward (d > 0) along its angle bisector."""
    P = np.asarray(poly, float)
    n = len(P)
    area = sum(P[i, 0] * P[(i + 1) % n, 1] - P[(i + 1) % n, 0] * P[i, 1] for i in range(n))
    sgn = 1 if area > 0 else -1
    out = []
    for i in range(n):
        a, b, c = P[i - 1], P[i], P[(i + 1) % n]
        e0 = (b - a) / np.linalg.norm(b - a)
        e1 = (c - b) / np.linalg.norm(c - b)
        n0 = np.array([-e0[1], e0[0]]) * sgn
        n1 = np.array([-e1[1], e1[0]]) * sgn
        m = n0 + n1
        m /= np.linalg.norm(m)
        out.append(b + m * d / max(m @ n0, 0.25))
    return [tuple(p) for p in out]


# palette (sampled from the original and slightly enriched)
KEEP_STONE = rgb(206, 194, 150)
CASTLE_STONE = rgb(158, 146, 110)
CASTLE_TOP = rgb(198, 180, 122)
EARTH_COL = rgb(160, 146, 98)
STAR_WALL = rgb(190, 172, 116)
STAR_TOP = rgb(214, 198, 140)
STAR_YARD = rgb(132, 122, 74)
GRAYSTONE = rgb(150, 144, 128)
BUNKER = rgb(166, 186, 152)
BERM = rgb(142, 136, 88)
WOOD_COL = rgb(178, 136, 78)
PALE_WOOD = rgb(216, 194, 128)
DARKHOLE = rgb(34, 28, 20)
WATER = rgb(112, 150, 152)
BANK = rgb(196, 180, 104)
WIRE_COL = rgb(226, 228, 230)
POST_COL = rgb(92, 110, 46)


# ------------------------------------------------------------------ the structures
def keep(s, cx, cy, side=19, h=32, col=KEEP_STONE):
    s.obj()
    hs = side / 2
    s.cbox(cx, cy, side, side, 0, h, col, STONE)
    p, top = 2.2, h + 3
    s.box(cx - hs, cy - hs, h, cx + hs, cy - hs + p, top, col, STONE)
    s.box(cx - hs, cy + hs - p, h, cx + hs, cy + hs, top, col, STONE)
    s.box(cx - hs, cy - hs + p, h, cx - hs + p, cy + hs - p, top, col, STONE)
    s.box(cx + hs - p, cy - hs + p, h, cx + hs, cy + hs - p, top, col, STONE)
    # a lower wing toward the front left and a buttress on the right
    s.obj()
    s.box(cx - 8, cy + hs - 1, 0, cx + 4, cy + hs + 17, 10, col, STONE)
    s.box(cx - 8, cy + hs + 14.5, 10, cx + 4, cy + hs + 17, 12.5, col, STONE)
    s.obj()
    s.box(cx + hs - 1, cy - 6, 0, cx + hs + 6, cy + 6, 15, col * 0.97, STONE)


def fortress_ancient(s, stakes=False):
    s.obj()
    s.box(-34, -22, 0, 25, 31, 3.0, EARTH_COL, EARTH, inset=2.0)
    kx, ky = G(63.5, 28.3)
    keep(s, kx, ky)
    if stakes:
        rng = np.random.default_rng(7)
        s.obj()
        cx, cy, hx, hy = -5, 4.5, 34, 31
        n = 46
        for i in range(n):
            t = i / n * 4
            side, f = int(t), t - int(t)
            f = f + rng.uniform(-0.04, 0.04)
            if side == 0:
                bx, by, ox, oy = cx - hx + 2 * hx * f, cy - hy, 0, -1
            elif side == 1:
                bx, by, ox, oy = cx + hx, cy - hy + 2 * hy * f, 1, 0
            elif side == 2:
                bx, by, ox, oy = cx + hx - 2 * hx * f, cy + hy, 0, 1
            else:
                bx, by, ox, oy = cx - hx, cy + hy - 2 * hy * f, -1, 0
            ln = rng.uniform(9, 13)
            el = math.radians(rng.uniform(16, 28))
            jit = rng.uniform(-0.35, 0.35)
            dx, dy = ox - oy * jit, oy + ox * jit
            nn = math.hypot(dx, dy)
            dx, dy = dx / nn, dy / nn
            p0 = (bx - dx * 5, by - dy * 5, 0)
            p1 = (bx + dx * ln * math.cos(el), by + dy * ln * math.cos(el), ln * math.sin(el))
            s.beam(p0, p1, 2.6, PALE_WOOD * rng.uniform(0.9, 1.08), WOOD, w1=0.3)


def rrect(cx, cy, hx, hy, r, n=6):
    """A rounded rectangle outline (counter-clockwise)."""
    pts = []
    for qx, qy, a0 in ((1, 1, 0), (-1, 1, 90), (-1, -1, 180), (1, -1, 270)):
        ox, oy = cx + qx * (hx - r), cy + qy * (hy - r)
        for k in range(n + 1):
            a = math.radians(a0 + 90 * k / n)
            pts.append((ox + r * math.cos(a), oy + r * math.sin(a)))
    return pts


def crenellated_top(s, x0, y0, x1, y1, z, col, mat, p=2.0, mh=3.0, step=4.0, sides=(0, 1, 2, 3)):
    """A parapet with merlons along the edges of a rectangle's top."""
    edges = [((x0, y0), (x1, y0)), ((x1, y0), (x1, y1)), ((x1, y1), (x0, y1)), ((x0, y1), (x0, y0))]
    for k in sides:
        (ax, ay), (bx, by) = edges[k]
        L = math.hypot(bx - ax, by - ay)
        dx, dy = (bx - ax) / L, (by - ay) / L
        nx, ny = dy, -dx   # inward for this winding (x0<x1, y0<y1)
        # low parapet strip
        q = [(ax, ay), (bx, by), (bx + nx * p, by + ny * p), (ax + nx * p, ay + ny * p)]
        s.prism(q, z, z + 1.2, col, mat)
        m = int(L / step)
        for i in range(m + 1):
            if i % 2:
                continue
            t0 = i * L / m - step * 0.35
            t1 = i * L / m + step * 0.35
            t0, t1 = max(t0, 0), min(t1, L)
            q = [(ax + dx * t0, ay + dy * t0), (ax + dx * t1, ay + dy * t1),
                 (ax + dx * t1 + nx * p, ay + dy * t1 + ny * p), (ax + dx * t0 + nx * p, ay + dy * t0 + ny * p)]
            s.prism(q, z, z + mh, col, mat)


def castle(s):
    s.obj()
    x0, y0, x1, y1, h = -7, -13, 9, 11, 40
    s.box(x0, y0, 0, x1, y1, h, CASTLE_STONE, STONE, top_col=CASTLE_TOP * 0.8)
    crenellated_top(s, x0, y0, x1, y1, h, CASTLE_TOP, STONE, p=2.2, mh=3.5, step=4)
    for cx, cy in ((x0 + 2, y0 + 2), (x1 - 2, y0 + 2), (x0 + 2, y1 - 2), (x1 - 2, y1 - 2)):
        s.cbox(cx, cy, 4.4, 4.4, h, h + 6, CASTLE_TOP, STONE)
    # front-left hall with a wall walk, a corner tower, and a right annex tower
    s.obj()
    s.box(-26, 11, 0, 10, 24, 13, CASTLE_STONE, STONE, top_col=CASTLE_TOP * 0.85)
    crenellated_top(s, -26, 11, 10, 24, 13, CASTLE_TOP, STONE, p=1.8, mh=2.6, step=3.6, sides=(1, 2, 3))
    s.obj()
    s.box(-33, 9, 0, -23, 26, 18, CASTLE_STONE, STONE, top_col=CASTLE_TOP * 0.85)
    crenellated_top(s, -33, 9, -23, 26, 18, CASTLE_TOP, STONE, p=1.6, mh=2.6, step=3.4)
    s.obj()
    s.box(20, -8, 0, 31, 5, 24, CASTLE_STONE, STONE, top_col=CASTLE_TOP * 0.85)
    crenellated_top(s, 20, -8, 31, 5, 24, CASTLE_TOP, STONE, p=1.6, mh=2.6, step=3.4)
    s.obj()
    s.box(8, -6, 0, 21, 3, 14, CASTLE_STONE * 0.95, STONE, top_col=CASTLE_TOP * 0.8)
    # door
    s.obj()
    s.box(-12, 23.6, 0, -6, 24.4, 7, DARKHOLE, DARK)


def fortress_medieval(s, moat=False):
    if moat:
        outer = rect_field(-1, 9, 40, 35, r=9)
        inner = rect_field(-1, 9, 31, 26, r=5)
        s.decal(water_decal(inner, outer, WATER, BANK, seed=5))
        s.obj()
        s.prism(rrect(-1, 9, 31, 26, 5), 0, 1.5, EARTH_COL, EARTH)
    else:
        s.decal(patch_decal([(-8, 10, 30, 22), (14, -2, 16, 14)], EARTH_COL, seed=2, grass=0.6, soft=3, alpha=0.8))
    castle(s)


def star_poly(tips, center, inner_r, blunt=4.0):
    """A star outline through the tip points (blunted) with notches between them."""
    cx, cy = center
    ang = [math.atan2(ty - cy, tx - cx) for tx, ty in tips]
    order = np.argsort(ang)
    tips = [tips[i] for i in order]
    ang = [ang[i] for i in order]
    pts = []
    n = len(tips)
    for i in range(n):
        tx, ty = tips[i]
        a = ang[i]
        tvx, tvy = -math.sin(a), math.cos(a)
        pts.append((tx - tvx * blunt, ty - tvy * blunt))
        pts.append((tx + tvx * blunt, ty + tvy * blunt))
        a0, a1 = ang[i], ang[(i + 1) % n]
        if a1 < a0:
            a1 += 2 * math.pi
        am = (a0 + a1) / 2
        for da in (-0.13, 0.13):
            pts.append((cx + inner_r * math.cos(am + da), cy + inner_r * math.sin(am + da)))
    return pts


def star_fort(s, tips, center, inner_r, h=15, batter=4.0, walk=8.0, yard=7.0):
    s.obj()
    outer = star_poly(tips, center, inner_r)
    top = offset_poly(outer, batter)
    inner = offset_poly(top, walk)
    s.sprism(outer, 0, h, STAR_WALL, STONE, top=top, cap=False)
    n = len(top)
    lip = offset_poly(top, 1.6)
    for i in range(n):
        j = (i + 1) % n
        # parapet lip on the outer edge, wall walk, inner wall down to the yard
        s.prism([top[i], top[j], lip[j], lip[i]], h, h + 1.8, STAR_TOP, PLAIN)
        s.quad((*lip[i], h), (*lip[j], h), (*inner[j], h), (*inner[i], h), STAR_TOP * 0.93, PLAIN)
        s.quad((*inner[i], h), (*inner[j], h), (*inner[j], yard), (*inner[i], yard), STAR_WALL * 0.8, STONE)
    s.flat_poly(inner, yard, STAR_YARD, EARTH)
    # central barracks: grey stone with a hipped roof
    s.obj()
    cx, cy = center
    s.box(cx - 9, cy - 8, yard, cx + 7, cy + 8, yard + 8, GRAYSTONE, STONE)
    rx0, ry0, rx1, ry1, zr = cx - 10, cy - 9, cx + 8, cy + 9, yard + 8
    apex0, apex1 = (cx - 1, cy - 3, zr + 8), (cx - 1, cy + 3, zr + 8)
    rc = rgb(132, 126, 112)
    s.tri((rx0, ry0, zr), (rx1, ry0, zr), apex0, rc, SLATE)
    s.tri((rx0, ry1, zr), (rx1, ry1, zr), apex1, rc, SLATE)
    s.quad((rx1, ry0, zr), (rx1, ry1, zr), apex1, apex0, rc, SLATE)
    s.quad((rx0, ry0, zr), (rx0, ry1, zr), apex1, apex0, rc, SLATE)
    return outer


STAR_TIPS = [(36, 35), (51, 18), (73, 17), (90, 33), (66, 46)]


def _star_layout(tips_px, lift=6):
    """Tip points measured on the original are tops of walls: drop them to the ground."""
    tips = [G(px, py + lift) for px, py in tips_px]
    center = (np.mean([t[0] for t in tips]), np.mean([t[1] for t in tips]))
    R = np.mean([math.hypot(t[0] - center[0], t[1] - center[1]) for t in tips])
    return tips, center, R


def fortress_industrial(s, moat=False):
    if moat:
        tips, center, R = _star_layout([(42, 37), (50, 16), (72, 14), (94, 36), (69, 49)])
        outer = star_fort(s, tips, center, R * 0.62)
        f = poly_field(outer)
        s.decal(water_decal(lambda x, y: f(x, y) + 1.0, lambda x, y: f(x, y) + 15.0, WATER, BANK, seed=6))
    else:
        tips, center, R = _star_layout(STAR_TIPS)
        star_fort(s, tips, center, R * 0.62)


def bunker(s):
    # a terraced earth and rock berm
    s.obj()
    cx, cy = -6, -6
    bx, by = cx + 4, cy + 6

    def berm(x, y):
        d = np.sqrt(((x - bx) / 36) ** 2 + ((y - by) / 33) ** 2)
        d = d * (1 + 0.22 * (fbm(x * 0.06, y * 0.06, None, seed=42) - 0.5))
        env = np.clip(1 - d ** 2.2, 0, 1)
        h = 10.0 * env ** 0.75 + 2.0 * env * (fbm(x * 0.1, y * 0.1, None, seed=41) - 0.5)
        # soft terraces read as layered rock
        t = h / 3.2
        f = t - np.floor(t)
        return 3.2 * (np.floor(t) + f ** 3 * (3 - 2 * f) * 0.6 + f * 0.4)
    s.heightfield(berm, bx - 40, bx + 40, by - 37, by + 37, 1.0, BERM, EARTH)
    # pillbox: base block, dark firing slit, roof slab
    s.obj()
    s.sprism(rrect(cx, cy, 22, 18, 7), 0, 12, BUNKER * 0.92, CONCRETE, top=rrect(cx, cy, 21, 17, 6.5))
    s.sprism(rrect(cx, cy, 19.5, 15.5, 6), 12, 14.2, rgb(40, 46, 34), DARK)
    s.sprism(rrect(cx + 1, cy - 1, 21, 17, 7), 14.2, 18, BUNKER, CONCRETE, top=rrect(cx + 1, cy - 1, 19, 15, 6))
    # a lower concrete wing toward the front left
    s.obj()
    s.sprism(rrect(cx - 4, cy + 20, 13, 7, 3.5), 0, 10, BUNKER * 0.88, CONCRETE, top=rrect(cx - 4, cy + 20, 12, 6, 3))
    s.sprism(rrect(cx - 4, cy + 20, 11.5, 5.5, 3), 10, 11.4, rgb(40, 46, 34), DARK)
    s.sprism(rrect(cx - 4, cy + 20, 12.5, 6.5, 3.2), 11.4, 13.5, BUNKER * 0.95, CONCRETE)


WIRE_POSTS = [(-40, 32), (-42, -42), (28, -40), (30, 34)]


def czech(s, x, y, size=6.5):
    s.obj()
    for a in (45, 135):
        r = math.radians(a)
        dx, dy = math.cos(r) * size, math.sin(r) * size
        s.beam((x - dx, y - dy, 0), (x + dx, y + dy, size * 1.2), 1.8, POST_COL, WOOD)
        s.beam((x + dx, y + dy, 0), (x - dx, y - dy, size * 1.2), 1.8, POST_COL * 0.9, WOOD)


def wire_ring(s, posts=WIRE_POSTS, r=6.0, pitch=5.2, zc=6.0):
    s.obj()
    n = len(posts)
    for i in range(n):
        a = np.array(posts[i], float)
        b = np.array(posts[(i + 1) % n], float)
        d = b - a
        L = np.linalg.norm(d)
        ax = np.array([d[0], d[1], 0]) / L
        side = np.array([-ax[1], ax[0], 0])
        up = np.array([0, 0, 1.0])
        t0, t1 = 6.0, L - 6.0
        steps = int((t1 - t0) / pitch * 24)
        ts = np.linspace(t0, t1, steps)
        th = (ts - t0) / pitch * 2 * np.pi
        pts, nrm = [], []
        for t, a_ in zip(ts, th):
            rad = side * math.cos(a_) + up * math.sin(a_)
            pts.append(np.array([a[0], a[1], zc]) + ax * t + rad * r)
            nrm.append(rad)
        s.ribbon(pts, nrm, 1.15, WIRE_COL, WIRE)
    for x, y in posts:
        czech(s, x, y)


GRASS2 = rgb(146, 166, 70)


def mine(s):
    s.off = (4, -4)
    cx, cy = -12, 4
    nx, ny = 0.92, 0.39     # the portal faces this way (screen lower right)
    px, py = cx + nx * 9.5, cy + ny * 9.5
    s.decal(patch_decal([(cx + 4, cy + 2, 17, 14), (18, 3, 18, 10)], rgb(156, 138, 84), seed=11, grass=0.2,
                        soft=3, alpha=0.85, col2=GRASS2))

    def mound(x, y):
        d = np.sqrt(((x - cx) / 15) ** 2 + ((y - cy) / 14) ** 2)
        env = np.clip(1 - d ** 2, 0, 1)
        h = 15 * env ** 0.9
        h += 2.0 * env * (fbm(x * 0.15, y * 0.15, None, seed=51) - 0.5)
        u = (x - px) * nx + (y - py) * ny
        v = -(x - px) * ny + (y - py) * nx
        cut = np.clip(1 - (np.abs(v) - 4.5) / 2.5, 0, 1) * np.clip((u + 3) / 2.5, 0, 1)
        return np.maximum(h - 22 * cut, 0)
    s.obj()
    s.heightfield(mound, cx - 17, cx + 17, cy - 16, cy + 16, 1.0, rgb(128, 124, 60), ROCK)
    # the shaft opening and its timber frame
    s.obj()
    o = np.array([px, py])
    nv = np.array([nx, ny]); tv = np.array([-ny, nx])
    w, h = 4.4, 9.5
    back = o + nv * 0.2
    a, b = back - tv * w, back + tv * w
    s.quad((*a, 0), (*b, 0), (*b, h), (*a, h), DARKHOLE, DARK)
    for sg in (-1, 1):
        q0, q1 = back + tv * w * sg, o + nv * 2.0 + tv * w * sg
        s.quad((*q0, 0), (*q1, 0), (*q1, h + 2), (*q0, h + 2), rgb(84, 72, 44), EARTH)
    s.quad((*a, h), (*b, h), (*(b + nv * 4.5), h), (*(a + nv * 4.5), h), rgb(84, 72, 44), EARTH)
    for sg in (-1, 1):
        p = o + tv * (w + 0.4) * sg + nv * 0.8
        s.beam((*p, 0), (*p, h + 0.6), 2.2, PALE_WOOD, WOOD)
    l0, l1 = o + tv * (w + 2.4) + nv * 0.8, o - tv * (w + 2.4) + nv * 0.8
    s.beam((*l0, h + 1.6), (*l1, h + 1.6), 2.6, PALE_WOOD, WOOD)
    # rails running out of the shaft to the ore heap and a cart
    s.obj()
    for sg in (-1, 1):
        r0 = o + tv * 1.8 * sg
        r1 = r0 + nv * 24
        s.beam((*r0, 0.4), (*r1, 0.4), 0.7, rgb(90, 86, 80), STEEL)
    s.obj()
    rx, ry = 14, -2

    def pile(x, y):
        d = np.sqrt(((x - rx) / 12) ** 2 + ((y - ry) / 8) ** 2)
        hh = 9 * np.clip(1 - d, 0, 0.9) ** 1.0
        lump = vnoise(x * 0.45, y * 0.45, None, seed=61)
        return hh * (0.65 + 0.6 * lump)
    s.heightfield(pile, rx - 13, rx + 13, ry - 10, ry + 10, 1.0, rgb(112, 126, 146), ROCK)
    for x, y, r in ((rx - 4, ry + 3, 3.2), (rx + 3, ry - 3, 2.8), (rx + 7, ry + 3, 2.4)):
        s.dome(x, y, r, r * 0.9, 0, r * 1.1, rgb(126, 138, 156), ROCK, n=12, m=5)
    s.obj()
    cart = o + nv * 19
    s.cbox(cart[0], cart[1], 6, 5, 1.6, 6, WOOD_COL, PLANKS)
    s.cbox(cart[0], cart[1], 5, 4, 6, 7.2, rgb(110, 124, 144), ROCK)
    for d in (-1.8, 1.8):
        wv = cart + nv * d
        s.cylinder(wv[0] + tv[0] * 2.7, wv[1] + tv[1] * 2.7, 1.5, 0, 1.8, rgb(60, 50, 40), DARK, n=10)
    for k, (x, y, r) in enumerate(((34, 6, 2.4), (28, -10, 1.8), (6, 16, 1.6))):
        s.dome(x, y, r, r * 0.9, 0, r * 0.9, rgb(130, 136, 140), ROCK, n=10, m=4)


def hut_dome(s, x, y, r, h, col, door=None):
    s.obj()
    s.dome(x, y, r, r, 0, h, col, THATCH)
    if door is not None:
        dx, dy = door
        s.beam((x + dx * (r - 1.5), y + dy * (r - 1.5), 0), (x + dx * (r - 1.5), y + dy * (r - 1.5), h * 0.45),
               3.6, DARKHOLE, DARK)


def barb_camp(s):
    s.off = (4, 5)
    s.decal(patch_decal([(-12, 10, 26, 22), (14, 4, 22, 16), (10, 30, 14, 10)], rgb(170, 152, 90), seed=21,
                        grass=0.25, soft=4, alpha=0.85, col2=GRASS2))
    hide = rgb(182, 162, 92)
    # big ribbed hide tent
    s.obj()
    tx, ty = -24, 8
    s.dome(tx, ty, 15, 10, 0, 18, hide, CANVAS, n=26, m=9)
    s.obj()
    for k in range(-2, 3):
        x = tx + k * 5.5
        rr = math.sqrt(max(1 - (k * 5.5 / 15) ** 2, 0))
        pts = [(x, ty + 10 * rr * math.cos(t) * 1.03, 18 * rr * math.sin(t) * 1.02 + 0.2) for t in np.linspace(0, np.pi, 14)]
        for i in range(len(pts) - 1):
            s.beam(pts[i], pts[i + 1], 1.1, rgb(118, 94, 54), WOOD)
    s.box(tx + 4, ty + 9.6, 0, tx + 9, ty + 10.2, 7, DARKHOLE, DARK)
    # dome huts
    hut_dome(s, 0, -5, 9, 15, rgb(152, 134, 74), door=(0.8, 0.6))
    hut_dome(s, 15, -7, 6.5, 9, rgb(178, 160, 94), door=(0.7, 0.7))
    hut_dome(s, 29, -24, 7, 10, rgb(196, 184, 120), door=(0.9, 0.4))
    # tripod pole frame with a hide over it
    s.obj()
    ax, ay = -4, 12
    for a in (20, 140, 260):
        r = math.radians(a)
        s.beam((ax + 8 * math.cos(r), ay + 8 * math.sin(r), 0),
               (ax - 1.5 * math.cos(r), ay - 1.5 * math.sin(r), 25), 1.4, PALE_WOOD, WOOD)
    s.cone(ax, ay, 5.8, 0, 10, hide * 0.92, CANVAS, n=16)
    # lean-to on the left
    s.obj()
    s.beam((-38, 24, 0), (-38, 24, 8), 1.5, WOOD_COL, WOOD)
    s.beam((-30, 36, 0), (-30, 36, 8), 1.5, WOOD_COL, WOOD)
    s.quad((-38, 24, 8.5), (-30, 36, 8.5), (-22, 31, 0.5), (-30, 19, 0.5), hide * 0.95, CANVAS)
    # logs, a fire pit and bundles in front
    s.obj()
    for i, (x, y) in enumerate(((4, 33), (9, 35), (6.5, 34))):
        z = 1.3 if i < 2 else 3.6
        s.beam((x - 7, y + 3, z), (x + 7, y - 3, z), 2.6, rgb(150, 112, 64), WOOD)
    s.obj()
    s.cylinder(16, 20, 3.6, 0, 1.4, rgb(110, 100, 86), ROCK, n=14, top_col=rgb(50, 40, 30), top_mat=DARK)
    s.obj()
    s.cbox(26, 22, 6, 5, 0, 4, rgb(170, 150, 90), CANVAS)
    s.cbox(30, 14, 5, 6, 0, 3.5, rgb(150, 126, 76), CANVAS)
    # a short palisade on the right
    s.obj()
    rng = np.random.default_rng(3)
    for t in np.linspace(0, 1, 7):
        x, y = 40 - 6 * t, -12 + 18 * t
        hh = rng.uniform(7, 9)
        s.beam((x, y, 0), (x, y, hh), 2.0, WOOD_COL * rng.uniform(0.85, 1.05), WOOD)
        s.beam((x, y, hh), (x, y, hh + 2.5), 2.0, WOOD_COL, WOOD, w1=0.1)


def bushes(s, spots, col=rgb(96, 128, 46)):
    s.obj()
    for x, y, r in spots:
        s.dome(x, y, r, r, 0, r * 1.1, col, FOLIAGE, n=14, m=5)


def field_rows(s, x0, y0, x1, y1, col, along='x', pitch=3.0):
    """A small tilled field: low furrows."""
    s.obj()
    if along == 'x':
        y = y0
        while y < y1:
            s.box(x0, y, 0, x1, y + pitch * 0.6, 1.0, col, EARTH)
            y += pitch
    else:
        x = x0
        while x < x1:
            s.box(x, y0, 0, x + pitch * 0.6, y1, 1.0, col, EARTH)
            x += pitch


def fence(s, pts, h=5, col=WOOD_COL, step=3.2):
    s.obj()
    for (x0, y0), (x1, y1) in zip(pts[:-1], pts[1:]):
        L = math.hypot(x1 - x0, y1 - y0)
        n = max(int(L / step), 1)
        for k in range(n + 1):
            t = k / n
            s.beam((x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, 0), (x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, h), 1.2, col, WOOD)
        s.beam((x0, y0, h * 0.75), (x1, y1, h * 0.75), 0.9, col, WOOD)


def colony_ancient(s):
    s.decal(patch_decal([(4, 10, 40, 32), (24, -22, 22, 18), (-22, 30, 18, 14), (26, 36, 16, 12)],
                        rgb(190, 172, 102), seed=31, grass=0.25, soft=4, alpha=0.85, col2=GRASS2))
    thatch = rgb(226, 190, 90)
    stone = rgb(136, 130, 108)
    # left stone shed
    s.obj()
    s.cbox(-20, 34, 18, 15, 0, 8, stone, STONE)
    s.gable(-30, 25.5, -10, 42.5, 8, 6, rgb(176, 154, 88), THATCH, along='x', gable_col=stone, gable_mat=STONE, over=1)
    s.box(-15, 41.6, 0, -11, 42.0, 5.5, DARKHOLE, DARK)
    # beehive hut
    s.obj()
    s.cylinder(-22, -2, 10.5, 0, 6, rgb(198, 168, 112), PLAIN, n=26, top=False)
    s.dome(-22, -2, 11.5, 11.5, 6, 14, rgb(230, 198, 132), THATCH)
    s.box(-22 + 8.5, -2 - 2.2, 0, -22 + 11.2, -2 + 2.2, 6, DARKHOLE, DARK)
    # gate frame
    s.obj()
    gc = rgb(156, 124, 72)
    for y in (-21, -11):
        s.beam((-8, y, 0), (-8, y, 22), 2.6, gc, WOOD)
    s.beam((-8, -24, 22.5), (-8, -8, 22.5), 2.8, gc, WOOD)
    # central timber house with yellow thatch
    s.obj()
    s.house(10, 14, 26, 20, 10, 10, rgb(180, 148, 92), WOOD, thatch, THATCH, along='x', over=1.5)
    s.box(23.1, 10, 0, 23.5, 17, 7, DARKHOLE, DARK)
    s.box(4, 24.1, 3, 9, 24.5, 7, DARKHOLE, DARK)
    # long house with a pale roof on the right
    s.obj()
    s.house(23, -19, 34, 15, 8, 8, rgb(164, 142, 96), WOOD, rgb(238, 228, 188), THATCH, along='x', over=1.5)
    # field and fence in front
    field_rows(s, 14, 30, 36, 46, rgb(130, 104, 62), along='x')
    fence(s, [(12, 48), (38, 48), (38, 28)], h=4.5)
    bushes(s, [(34, -44, 5.5), (40, -36, 4.5), (28, -48, 4.5), (-40, 20, 4)])
    s.obj()
    s.dome(46, -24, 2.8, 2.4, 0, 2.6, rgb(206, 192, 156), ROCK, n=10, m=4)


def paving_decal(blobs, seed=0, alpha=0.9):
    base = patch_decal(blobs, rgb(204, 190, 140), seed=seed, grass=0.35, soft=4, alpha=alpha, col2=GRASS2)

    def fn(x, y):
        c, a = base(x, y)
        j = np.maximum(_lines(x, 8, 0.45), _lines(y + 4 * (np.floor(x / 8) % 2), 8, 0.45))
        r = _hash(np.floor(x / 8).astype(np.int64), np.floor((y + 4 * (np.floor(x / 8) % 2)) / 8).astype(np.int64), 0, 3)
        return c * ((0.94 + 0.1 * r) * (1 - 0.18 * j))[:, None], a
    return fn


def colony_medieval(s):
    s.decal(patch_decal([(-30, 24, 18, 20), (24, -38, 22, 16), (-36, -4, 14, 14)], rgb(150, 160, 76), seed=34,
                        grass=0.8, soft=4, alpha=0.8, col2=GRASS2))
    s.decal(paving_decal([(4, 6, 40, 36)], seed=33, alpha=0.92))
    # round timber granary
    s.obj()
    s.cylinder(-24, -2, 10.5, 0, 32, rgb(206, 152, 92), WOOD, n=28, top_col=rgb(120, 96, 60), top_mat=PLANKS)
    s.cylinder(-24, -2, 11.2, 32, 34, rgb(170, 126, 74), WOOD, n=28, top=False)
    s.cylinder(-24, -2, 11.2, 8, 9.5, rgb(140, 104, 62), WOOD, n=28, top=False)
    # grey stone house with a slate roof
    s.obj()
    s.house(-4, -20, 18, 18, 11, 11, rgb(176, 168, 142), STONE, rgb(132, 132, 128), SLATE, along='y', over=1.5)
    s.box(5.1, -24, 0, 5.5, -18, 7, DARKHOLE, DARK)
    # timber frame (a house going up)
    s.obj()
    fc = rgb(200, 122, 66)
    X0, X1, Y0, Y1 = 10, 22, -22, -36
    for x, y in ((X0, Y0), (X0, Y1), (X1, Y0), (X1, Y1)):
        s.beam((x, y, 0), (x, y, 15), 1.7, fc, WOOD)
    for (x0, y0), (x1, y1) in (((X0, Y0), (X0, Y1)), ((X1, Y0), (X1, Y1)), ((X0, Y0), (X1, Y0)), ((X0, Y1), (X1, Y1))):
        s.beam((x0, y0, 15), (x1, y1, 15), 1.7, fc, WOOD)
    ym = (Y0 + Y1) / 2
    for x in (X0, X1):
        s.beam((x, Y0, 15), (x, ym, 23), 1.5, fc, WOOD)
        s.beam((x, Y1, 15), (x, ym, 23), 1.5, fc, WOOD)
    s.beam((X0, ym, 23), (X1, ym, 23), 1.5, fc, WOOD)
    s.beam((X1, Y0, 0), (X1, Y1, 15), 1.3, fc, WOOD)
    s.beam((X0, Y0, 0), (X1, Y0, 15), 1.3, fc, WOOD)
    # dark stone block on the right
    s.obj()
    s.box(18, -52, 0, 30, -36, 13, rgb(116, 112, 98), STONE)
    s.box(18, -52, 13, 30, -50, 15, rgb(116, 112, 98), STONE)
    # lean-to shed on the left
    s.obj()
    s.box(-38, 18, 0, -16, 38, 9, rgb(228, 184, 120), PLAIN)
    s.quad((-39, 17, 11.5), (-15, 17, 11.5), (-15, 39, 8), (-39, 39, 8), rgb(156, 126, 80), PLANKS)
    s.box(-28, 38.1, 0, -21, 38.5, 6.5, DARKHOLE, DARK)
    # steps of planks
    s.obj()
    for i in range(3):
        s.box(12 + i * 5, -8, 0, 36, 10, 7 - i * 2.2, rgb(210, 192, 150), PLANKS)
    # well
    s.obj()
    s.cylinder(24, 42, 7, 0, 4.5, rgb(178, 172, 152), STONE, n=26, top_col=rgb(178, 172, 152), top_mat=STONE)
    s.cylinder(24, 42, 4.4, 3.5, 4.55, rgb(30, 30, 30), DARK, n=26, top_col=rgb(28, 30, 30), top_mat=DARK)
    bushes(s, [(-42, 4, 5), (-38, -8, 4.5), (40, -40, 4.5), (-30, 44, 4)])


def colony_industrial(s):
    s.decal(patch_decal([(0, 4, 46, 40)], rgb(206, 192, 124), seed=36, grass=0.3, soft=5, alpha=0.85, col2=GRASS2))
    s.decal(patch_decal([(-14, 22, 22, 22), (26, -28, 22, 16), (22, 30, 18, 12)], rgb(214, 148, 104),
                        seed=35, grass=0.15, soft=3, alpha=0.9, col2=rgb(170, 150, 80)))
    roof = rgb(240, 222, 152)
    wall = rgb(200, 180, 124)
    s.obj()
    s.house(-8, -16, 24, 24, 12, 12, wall, PLAIN, roof, PLANKS, along='y', over=1.5)
    s.obj()
    s.house(-8, 28, 16, 28, 10, 9, wall, PLAIN, roof, PLANKS, along='x', over=1.5)
    s.obj()
    s.house(30, 42, 16, 14, 8, 7, wall, PLAIN, roof, PLANKS, along='y', over=1.0)
    steel = rgb(112, 118, 128)
    for x, y, h in ((-30, 34, 30), (-20, 10, 28), (-28, -28, 50)):
        s.obj()
        s.cylinder(x, y, 1.6, 0, h, steel, STEEL, n=10)
    # derrick
    s.obj()
    bx, by, hh, b, t = 28, 14, 68, 6.5, 1.6
    legs = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
    for sx, sy in legs:
        s.beam((bx + sx * b, by + sy * b, 0), (bx + sx * t, by + sy * t, hh), 1.3, steel, STEEL)
    for k in range(1, 9):
        z0, z1 = (k - 1) * hh / 8.5, k * hh / 8.5
        w0 = b + (t - b) * z0 / hh
        w1 = b + (t - b) * z1 / hh
        for i in range(4):
            (ax, ay), (cx, cy) = legs[i], legs[(i + 1) % 4]
            s.beam((bx + ax * w1, by + ay * w1, z1), (bx + cx * w1, by + cy * w1, z1), 0.8, steel, STEEL)
            s.beam((bx + ax * w0, by + ay * w0, z0), (bx + cx * w1, by + cy * w1, z1), 0.6, steel, STEEL)
    s.cbox(bx, by, 4, 4, hh, hh + 3, steel, STEEL)
    s.cbox(bx, by, 7, 7, 0, 2.5, rgb(90, 88, 84), CONCRETE)
    # pipe rack
    s.obj()
    for x in (12, 21, 30, 38):
        for y in (-20, -40):
            s.beam((x, y, 0), (x, y, 9), 1.3, steel, STEEL)
    for y in (-20, -25, -30, -35, -40):
        s.beam((10, y, 9.5), (40, y, 9.5), 1.7, steel, STEEL)
    for x in (12, 21, 30, 38):
        s.beam((x, -19, 9.5), (x, -41, 9.5), 1.5, steel, STEEL)
    bushes(s, [(44, -6, 4.5), (-44, 12, 5), (-36, 46, 4)], col=rgb(110, 140, 52))


def colony_modern(s):
    s.decal(patch_decal([(2, 8, 46, 40)], rgb(186, 176, 108), seed=37, grass=0.3, soft=4, alpha=0.85, col2=GRASS2))
    metal = rgb(198, 200, 198)
    s.obj()
    s.corr_axis = (0.0, 1.0)
    s.hcyl(-36, 42, (0, -1), 46, 13, 0, metal, CORRUGATED, n=22, end_col=rgb(150, 150, 146), end_mat=PLAIN)
    s.box(-40, 42.2, 0, -32, 42.8, 8.5, DARKHOLE, DARK)
    # grey warehouse behind
    s.obj()
    s.house(-14, -24, 22, 20, 13, 7, rgb(172, 168, 158), CONCRETE, rgb(116, 118, 120), SLATE, along='x', over=1)
    # barrels
    drum = rgb(150, 154, 158)
    s.obj()
    rng = np.random.default_rng(5)
    for i in range(4):
        for j in range(4):
            x, y = 2 + i * 7.5, -10 - j * 7.5 + (i % 2) * 2
            s.cylinder(x, y, 3.3, 0, 7.5, drum * rng.uniform(0.85, 1.05), METAL, n=16,
                       top_col=drum * 1.1, top_mat=METAL)
            s.cylinder(x, y, 3.4, 3.3, 4.0, drum * 0.7, METAL, n=16, top=False)
    for x, y in ((6, -16), (14, -14), (10, -24)):
        s.cylinder(x, y, 3.3, 7.5, 15, drum, METAL, n=16, top_col=drum * 1.1, top_mat=METAL)
    # crates on the right and concrete walls in front
    crate = rgb(216, 182, 110)
    s.obj()
    s.box(32, -34, 0, 44, -16, 10, crate, PLANKS)
    s.box(34, -14, 0, 44, -4, 7, crate * 0.95, PLANKS)
    conc = rgb(208, 194, 152)
    s.obj()
    s.box(12, 30, 0, 34, 44, 8, conc, CONCRETE)
    s.obj()
    s.box(32, 14, 0, 42, 30, 10, crate, PLANKS)
    s.obj()
    s.box(-8, 32, 0, 8, 48, 7, conc, CONCRETE)
    # crane
    s.obj()
    yel = rgb(238, 202, 44)
    mx, my = 16, 18
    s.cbox(mx, my, 8, 8, 0, 4, rgb(120, 110, 70), STEEL)
    s.cbox(mx, my, 3.6, 3.6, 4, 42, yel, PLAIN)
    s.cbox(mx, my, 5.5, 5.5, 42, 46, yel, PLAIN)
    s.beam((mx, my, 44), (12, -16, 60), 2.6, yel, PLAIN, w1=1.6)
    s.beam((mx, my, 44), (19, 30, 45), 2.6, yel, PLAIN)
    s.cbox(19, 30, 4.5, 4.5, 40, 46, rgb(100, 100, 96), CONCRETE)
    s.beam((12, -16, 59), (12, -16, 28), 0.4, rgb(60, 60, 60), STEEL)
    bushes(s, [(-46, 8, 4.5), (46, 4, 4.5), (-20, 48, 4)], col=rgb(110, 140, 52))


SCENES = {
    (0, 0): lambda s: fortress_ancient(s),
    (0, 1): lambda s: fortress_medieval(s),
    (0, 2): lambda s: fortress_industrial(s),
    (0, 3): lambda s: bunker(s),
    (1, 0): colony_ancient,
    (1, 1): colony_medieval,
    (1, 2): colony_industrial,
    (1, 3): colony_modern,
    (2, 0): barb_camp,
    (2, 1): mine,
    (2, 3): lambda s: wire_ring(s),
    (3, 0): lambda s: fortress_ancient(s, stakes=True),
    (3, 1): lambda s: fortress_medieval(s, moat=True),
    (3, 2): lambda s: fortress_industrial(s, moat=True),
    (3, 3): lambda s: (bunker(s), wire_ring(s, posts=[(-42, 34), (-46, -46), (30, -44), (32, 36)])),
}


# ------------------------------------------------------------------ sheet
def build_sheet(only=None):
    sheet = np.zeros((CH * 4, CW * 4, 4), np.uint8)
    for (c, r), fn in SCENES.items():
        if only is not None and (c, r) not in only:
            continue
        s = Scene()
        fn(s)
        if not s.V:
            continue
        sheet[r * CH:(r + 1) * CH, c * CW:(c + 1) * CW] = render(s)
    return mute_sheet(sheet)


# Each cell's mean saturation is kept at most this fraction of its original's.
MUTE = 0.9


def _mean_sat(rgb, a):
    mx, mn = rgb.max(-1), rgb.min(-1)
    m = (a > 0.78) & (mx > 0.12)
    return float(((mx - mn) / np.maximum(mx, 1e-6))[m].mean()) if m.any() else 0.0


def mute_sheet(sheet):
    orig = civ3art.load_original(REL, shadows=True)
    oc, oh = CW // 2, CH // 2
    out = sheet.copy()
    for r in range(sheet.shape[0] // CH):
        for c in range(sheet.shape[1] // CW):
            o = orig[r * oh:(r + 1) * oh, c * oc:(c + 1) * oc] / 255.0
            cell = out[r * CH:(r + 1) * CH, c * CW:(c + 1) * CW]
            a = cell[..., 3] / 255.0
            if not (a > 0).any():
                continue
            target = MUTE * _mean_sat(o[..., :3], o[..., 3])
            rgb = cell[..., :3] / 255.0
            for _ in range(4):
                sat = _mean_sat(rgb, a)
                if sat <= target + 1e-3:
                    break
                lum = (rgb @ np.array([0.3, 0.55, 0.15]))[..., None]
                rgb = np.clip(lum + (rgb - lum) * (target / sat), 0, 1)
            cell[..., :3] = np.round(rgb * 255).astype(np.uint8)
    return out


def on_grass(rgba, scale=1, cell=(CW, CH)):
    """Composites sprites on grass iso diamonds (shrunk with scale=0.5 to see them at 1x)."""
    rgba = np.asarray(rgba, np.uint8)
    h, w = rgba.shape[:2]
    cw, ch = cell
    bg = np.zeros((h, w, 4), np.uint8)
    yy, xx = np.mgrid[0:h, 0:w]
    cx = (xx % cw) - cw / 2 + 0.5
    cy = (yy % ch) - ch / 2 + 0.5
    inside = np.abs(cx) / (cw / 2) + np.abs(cy) / (ch / 2) <= 1
    bg[..., :3] = (60, 60, 64)
    bg[inside, :3] = (111, 154, 53)
    bg[..., 3] = 255
    im = Image.alpha_composite(Image.fromarray(bg), Image.fromarray(rgba))
    if scale != 1:
        im = im.resize((int(w * scale), int(h * scale)), Image.LANCZOS)
    return im


def build():
    sheet = build_sheet()
    civ3art.save_modern(REL, sheet)
    orig1 = civ3art.load_original(REL)
    orig = civ3art.upscale_nearest(orig1)
    civ3art.save_preview('terrain_buildings', orig, sheet)
    civ3art.save_preview('terrain_buildings_1x', on_grass(orig1, cell=(CW // 2, CH // 2)), on_grass(sheet, 0.5))
    civ3art.save_preview('terrain_buildings_2x_grass', on_grass(orig), on_grass(sheet))
    return sheet


if __name__ == '__main__':
    build()
