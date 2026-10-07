"""Remake of Art/resources.pcx: the strategic/luxury/bonus resource icons.

The original is a 6x6 grid of 50x50 cells (26 used). Each icon is drawn anew
with a small SDF shape toolkit (ellipses, polygons, tapered strokes, bezier
paths) and per-shape lighting from a pseudo-normal, supersampled 4x and box
filtered down to the 2x output (100x100 cells). Every subject keeps the
original's place, size, silhouette and colors within its cell.
"""
import os
import sys
import math

import numpy as np
from scipy import ndimage as ndi

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'common'))
import civ3art  # noqa: E402

REL = 'Art/resources.pcx'
CELL = 50          # original cell size
SSF = 4            # supersampling factor on top of the 2x output
SS = civ3art.SCALE * SSF   # supersampled pixels per original pixel
N = CELL * SS
_c = (np.arange(N) + 0.5) / SS
X, Y = np.meshgrid(_c, _c)

_L = np.array([-0.52, -0.68, 0.88])
LIGHT = _L / np.linalg.norm(_L)
_H = LIGHT + np.array([0, 0, 1.0])
HALF = _H / np.linalg.norm(_H)
_RIM = np.array([-0.6, -0.8])


# ---------------------------------------------------------------- colors
def hexc(h):
    h = h.lstrip('#')
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], float) / 255.0


def mix(a, b, t):
    t = np.asarray(t, float)
    if t.ndim:
        t = t[..., None]
    return np.asarray(a) * (1 - t) + np.asarray(b) * t


def sstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def full(c):
    c = np.asarray(c, float)
    return np.broadcast_to(c, (N, N, 3)) if c.ndim == 1 else c


def smooth_noise(seed, scale, octaves=2):
    """Smooth value noise in [-1, 1] with features about `scale` original px."""
    rng = np.random.default_rng(seed)
    out = np.zeros((N, N))
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        s = scale * SS / (2 ** o)
        z = ndi.gaussian_filter(rng.standard_normal((N, N)), s, mode='wrap')
        z /= z.std() + 1e-9
        out += amp * z
        tot += amp
        amp *= 0.5
    return np.clip(out / tot / 2.0, -1, 1)


# ---------------------------------------------------------------- SDFs (units = original px)
def sd_circle(cx, cy, r):
    return np.hypot(X - cx, Y - cy) - r


def sd_ellipse(cx, cy, rx, ry, ang=0.0):
    x, y = X - cx, Y - cy
    if ang:
        a = math.radians(ang)
        c, s = math.cos(a), math.sin(a)
        x, y = x * c + y * s, -x * s + y * c
    k0 = np.hypot(x / rx, y / ry)
    k1 = np.hypot(x / rx ** 2, y / ry ** 2)
    return k0 * (k0 - 1) / np.maximum(k1, 1e-6)


def sd_seg(a, b, ra, rb=None):
    rb = ra if rb is None else rb
    ax, ay = a
    bx, by = b
    ex, ey = bx - ax, by - ay
    wx, wy = X - ax, Y - ay
    h = np.clip((wx * ex + wy * ey) / max(ex * ex + ey * ey, 1e-9), 0, 1)
    return np.hypot(wx - ex * h, wy - ey * h) - (ra + (rb - ra) * h)


def sd_path(pts, radii):
    pts = [tuple(p) for p in pts]
    if np.isscalar(radii):
        radii = [radii] * len(pts)
    d = None
    for i in range(len(pts) - 1):
        s = sd_seg(pts[i], pts[i + 1], radii[i], radii[i + 1])
        d = s if d is None else np.minimum(d, s)
    return d


def bez(*p, n=20):
    p = np.array(p, float)
    t = np.linspace(0, 1, n)[:, None]
    if len(p) == 3:
        return (1 - t) ** 2 * p[0] + 2 * (1 - t) * t * p[1] + t ** 2 * p[2]
    return ((1 - t) ** 3 * p[0] + 3 * (1 - t) ** 2 * t * p[1]
            + 3 * (1 - t) * t ** 2 * p[2] + t ** 3 * p[3])


def spline(pts, n=8):
    """Catmull-Rom curve through pts with n samples per span."""
    p = np.array(pts, float)
    p = np.vstack([2 * p[0] - p[1], p, 2 * p[-1] - p[-2]])
    out = []
    for i in range(1, len(p) - 2):
        p0, p1, p2, p3 = p[i - 1], p[i], p[i + 1], p[i + 2]
        for t in np.linspace(0, 1, n, endpoint=False):
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                              + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    out.append(p[-2])
    return np.array(out)


def limb(pts, radii, n=8):
    """A smooth tapered limb through pts with a radius per point."""
    c = spline(pts, n)
    r = np.interp(np.linspace(0, len(pts) - 1, len(c)), np.arange(len(pts)), radii)
    return sd_path(c, list(r))


def taper(n, r0, r1, power=1.0):
    t = np.linspace(0, 1, n) ** power
    return list(r0 + (r1 - r0) * t)


def sd_poly(pts):
    v = np.array(pts, float)
    d = (X - v[0, 0]) ** 2 + (Y - v[0, 1]) ** 2
    s = np.ones_like(X)
    n = len(v)
    for i in range(n):
        a, b = v[i], v[i - 1]
        ex, ey = b[0] - a[0], b[1] - a[1]
        wx, wy = X - a[0], Y - a[1]
        h = np.clip((wx * ex + wy * ey) / max(ex * ex + ey * ey, 1e-9), 0, 1)
        d = np.minimum(d, (wx - ex * h) ** 2 + (wy - ey * h) ** 2)
        c1 = Y >= a[1]
        c2 = Y < b[1]
        c3 = ex * wy > ey * wx
        flip = (c1 & c2 & c3) | (~c1 & ~c2 & ~c3)
        s = np.where(flip, -s, s)
    return s * np.sqrt(d)


def smin(a, b, k=1.0):
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0, 1)
    return b * (1 - h) + a * h - k * h * (1 - h)


def union(*ds, k=0.0):
    out = ds[0]
    for d in ds[1:]:
        out = smin(out, d, k) if k else np.minimum(out, d)
    return out


def cut(a, b):
    return np.maximum(a, -b)


# ---------------------------------------------------------------- lighting
def _norm(nx, ny, nz):
    l = np.sqrt(nx * nx + ny * ny + nz * nz) + 1e-9
    return nx / l, ny / l, nz / l


def bulge(d, R, k=1.0):
    """Normal of a puffy height field rising over R units from the edge."""
    t = np.clip(-d / R, 0, 1)
    h = R * np.sqrt(1 - (1 - t) ** 2) * k
    gy, gx = np.gradient(h, 1.0 / SS)
    return _norm(-gx, -gy, np.ones_like(gx))


def tilt(n, tx, ty):
    return _norm(n[0] + tx, n[1] + ty, n[2])


def shade(base, n, d, amb=0.46, dif=0.72, spec=0.22, shin=22, rim=0.22,
          edge=0.42, ew=0.42, spec_col=(1, 1, 1)):
    base = full(base)
    nx, ny, nz = n
    lam = np.clip(nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2], 0, 1)
    sp = np.clip(nx * HALF[0] + ny * HALF[1] + nz * HALF[2], 0, 1) ** shin * spec
    col = base * (amb + dif * lam)[..., None] + sp[..., None] * np.asarray(spec_col)
    inner = np.maximum(-d, 0)
    if rim:
        face = np.clip((nx * _RIM[0] + ny * _RIM[1]) / (np.hypot(nx, ny) + 1e-6), 0, 1)
        band = sstep(0.2, 0.45, inner) * (1 - sstep(0.7, 1.3, inner))
        col = col + (rim * band * face)[..., None] * (0.5 + 0.5 * base)
    if edge:
        col = col * (1 - edge * (1 - sstep(0, ew, inner)))[..., None]
    return np.clip(col, 0, 1)


# ---------------------------------------------------------------- canvases
class Canvas:
    """Premultiplied RGBA accumulation at the supersampled resolution."""

    def __init__(self):
        self.P = np.zeros((N, N, 3))
        self.A = np.zeros((N, N))

    @staticmethod
    def cover(d, soft=None):
        if soft:
            return np.clip(0.5 - d / soft, 0, 1)
        return np.clip(0.5 - d * SS, 0, 1)

    def put(self, d, col, alpha=1.0, soft=None):
        cov = self.cover(d, soft) * alpha
        self.P = full(col) * cov[..., None] + self.P * (1 - cov[..., None])
        self.A = cov + self.A * (1 - cov)

    def atop(self, d, col, alpha=1.0, soft=None):
        """Paints only over what is already there (spots, markings, inner shade)."""
        cov = self.cover(d, soft) * alpha
        self.P = full(col) * (cov * self.A)[..., None] + self.P * (1 - cov[..., None])

    def part(self, d, base, R=1.5, n=None, alpha=1.0, soft=None, **kw):
        if n is None:
            n = bulge(d, R)
        self.put(d, shade(base, n, d, **kw), alpha, soft)
        return n


class Shadow:
    """Black ground shadow (alpha only), blurred when finished."""

    def __init__(self):
        self.A = np.zeros((N, N))

    def ellipse(self, cx, cy, rx, ry, a=1.0, ang=0.0):
        cov = np.clip(0.5 - sd_ellipse(cx, cy, rx, ry, ang) * SS, 0, 1) * a
        self.A = np.maximum(self.A, cov)

    def shape(self, d, a=1.0):
        self.A = np.maximum(self.A, np.clip(0.5 - d * SS, 0, 1) * a)

    def drop(self, cv, dx=1.2, dy=0.9, a=1.0, squash=None):
        A = cv.A
        if squash:
            # flatten toward a ground line (y0), keeping x
            y0, f = squash
            src_y = y0 + (Y - y0) / f
            A = ndi.map_coordinates(cv.A, [src_y * SS - 0.5, X * SS - 0.5], order=1, cval=0)
        A = ndi.shift(A, (dy * SS, dx * SS), order=1, cval=0)
        self.A = np.maximum(self.A, A * a)


_DISK = {}


def _disk(r):
    if r not in _DISK:
        y, x = np.mgrid[-r:r + 1, -r:r + 1]
        _DISK[r] = (x * x + y * y) <= r * r + 0.5
    return _DISK[r]


def finish(layers, shadow=None, shadow_alpha=0.42, blur=0.9):
    """Composites [(canvas, outline_strength)] back to front over the shadow,
    adds each canvas's thin dark contour and box-filters to the 2x cell."""
    P = np.zeros((N, N, 3))
    A = np.zeros((N, N))
    if shadow is not None:
        A = ndi.gaussian_filter(shadow.A, blur * SS) * shadow_alpha
    for cv, ol in layers:
        if ol:
            dil = ndi.grey_dilation(cv.A, footprint=_disk(5))
            dil = ndi.gaussian_filter(dil, 1.0)
            ga = ndi.gaussian_filter(cv.A, 6) + 1e-6
            gp = np.stack([ndi.gaussian_filter(cv.P[..., i], 6) for i in range(3)], -1)
            oc = np.clip(gp / ga[..., None], 0, 1) * 0.28
            oa = dil * ol
            P = oc * oa[..., None] + P * (1 - oa[..., None])
            A = oa + A * (1 - oa)
        P = cv.P + P * (1 - cv.A[..., None])
        A = cv.A + A * (1 - cv.A)
    o = civ3art.SCALE * CELL
    P = P.reshape(o, SSF, o, SSF, 3).mean((1, 3))
    A = A.reshape(o, SSF, o, SSF).mean((1, 3))
    return np.concatenate([P, A[..., None]], -1)


def facet_normal(d, cx, cy, seed, k=7, strength=0.55, R=1.3, up=0.0):
    """Bulge normal broken into flat-ish facets (Voronoi cells) for rocks and crystals."""
    rng = np.random.default_rng(seed)
    iy, ix = np.nonzero(d < -0.3)
    pick = rng.choice(len(iy), size=min(k, len(iy)), replace=False)
    sx, sy = X[iy[pick], ix[pick]], Y[iy[pick], ix[pick]]
    dist = np.stack([np.hypot(X - a, Y - b) for a, b in zip(sx, sy)])
    order = np.argsort(dist, 0)
    i0 = order[0]
    d0 = np.take_along_axis(dist, order[:1], 0)[0]
    d1 = np.take_along_axis(dist, order[1:2], 0)[0]
    dx, dy = sx - cx, sy - cy - up
    ln = np.hypot(dx, dy) + 1e-6
    jx, jy = rng.uniform(-0.25, 0.25, len(sx)), rng.uniform(-0.25, 0.25, len(sx))
    tx = (dx / ln * np.minimum(ln / 3.0, 1) + jx) * strength
    ty = (dy / ln * np.minimum(ln / 3.0, 1) + jy) * strength
    nb = bulge(d, R)
    n = _norm(nb[0] * 0.5 + tx[i0], nb[1] * 0.5 + ty[i0], nb[2])
    ridge = 1 - sstep(0.0, 0.3, d1 - d0)
    return n, ridge


def rock(cv, d, cx, cy, base, seed, k=7, strength=0.55, R=1.3, ridge_light=0.12, **kw):
    n, ridge = facet_normal(d, cx, cy, seed, k, strength, R)
    col = shade(base, n, d, **kw)
    col = np.clip(col * (1 + ridge_light * ridge)[..., None], 0, 1)
    cv.put(d, col)


# ================================================================= icons
def icon_horse():
    cv, sh = Canvas(), Shadow()
    sh.ellipse(26.6, 32.4, 8.0, 2.4, 0.7)
    for hx, hy in ((20.2, 29.9), (23.8, 30.6), (28.0, 34.0), (31.7, 34.6)):
        sh.ellipse(hx + 0.7, hy + 0.2, 1.5, 0.6, 1.0)
    coat = hexc('#a85a2a')
    hi = hexc('#d48a44')
    dark = hexc('#3e1f10')
    # far legs and tail (behind the body, in shade)
    far = mix(coat * 0.6, dark * 0.85, sstep(24, 28, Y))
    cv.part(limb([(21.0, 21.0), (20.6, 24.2), (19.6, 26.4), (20.0, 29.2), (20.2, 29.8)], [1.6, 1.0, 0.62, 0.5, 0.6]), far, R=0.8, edge=0.3)
    cv.part(limb([(28.4, 22.6), (28.0, 27.6), (27.9, 32.8), (28.0, 33.8)], [1.4, 0.75, 0.5, 0.62]), far, R=0.8, edge=0.3)
    tail = bez((20.2, 17.4), (17.8, 16.6), (16.6, 19.4), (16.4, 23.2), n=16)
    td = sd_path(tail, taper(16, 1.25, 0.5))
    cv.part(td, dark * 1.2, R=0.9)
    cv.atop(np.maximum(sd_path(tail[2:12], 0.18), td + 0.3), hexc('#7a4a2a'), 0.6)
    # body, neck and near legs fused into one form
    body = union(sd_ellipse(22.0, 19.3, 3.7, 3.5), sd_ellipse(25.8, 20.0, 4.8, 3.5),
                 sd_ellipse(29.6, 20.8, 2.9, 3.5), k=1.6)
    neck = sd_path(bez((29.2, 20.2), (30.4, 17.6), (31.2, 15.4), (32.0, 13.8), n=10), taper(10, 2.7, 1.6))
    hind = limb([(23.4, 20.5), (24.0, 23.8), (22.9, 26.6), (23.6, 29.4), (23.8, 30.3)], [2.3, 1.25, 0.7, 0.52, 0.6])
    fore = limb([(30.8, 21.0), (31.1, 25.0), (31.1, 28.4), (31.5, 33.5), (31.7, 34.3)], [1.9, 1.05, 0.72, 0.52, 0.62])
    torso = union(body, neck, k=1.2)
    torso = union(torso, hind, fore, k=0.9)
    tone = mix(coat, hi, sstep(22.5, 16.5, Y) * 0.65)
    tone = mix(tone, coat * 0.75, sstep(21.0, 24.0, Y) * 0.5)
    tone = mix(tone, dark, sstep(25.5, 29.5, Y) * 0.85)
    cv.part(torso, tone, R=2.6, spec=0.18, shin=16, edge=0.38)
    # muscle hints
    cv.atop(np.maximum(np.abs(sd_ellipse(22.2, 19.6, 3.0, 2.8)) - 0.15, torso + 0.5), dark, 0.18, soft=0.4)
    cv.atop(np.maximum(sd_ellipse(29.4, 18.8, 1.4, 2.2, 30), torso + 0.5), hi, 0.3, soft=0.6)
    # ears, head
    ears = union(sd_poly([(30.9, 13.8), (31.2, 11.0), (32.3, 13.2)]),
                 sd_poly([(32.7, 13.3), (33.6, 10.9), (34.0, 13.6)]))
    cv.part(ears, coat * 0.85, R=0.5)
    head = union(sd_seg((32.4, 14.0), (33.7, 18.4), 1.65, 1.15), sd_ellipse(32.3, 15.6, 1.7, 1.9), k=0.6)
    cv.part(head, mix(coat, hi, 0.25), R=1.3, edge=0.35)
    mane = sd_path(bez((31.8, 12.6), (30.4, 14.8), (29.4, 17.2), (27.6, 18.6), n=12), taper(12, 0.95, 0.4))
    cv.part(mane, dark * 1.1, R=0.6)
    cv.part(sd_ellipse(32.6, 12.6, 0.9, 0.6), dark * 1.1, R=0.4)
    cv.atop(sd_seg((33.1, 13.8), (33.9, 17.8), 0.4, 0.62), hexc('#f2dcaa'), 0.95)
    cv.atop(sd_circle(31.9, 15.0, 0.42), hexc('#140a06'))
    cv.atop(sd_circle(32.05, 14.85, 0.14), hexc('#ffffff'), 0.8)
    cv.atop(sd_ellipse(33.6, 18.9, 1.2, 0.7), dark, 0.7)
    # white sock and hooves
    cv.atop(np.maximum(sd_seg((31.4, 31.4), (31.7, 34.0), 0.9), fore + 0.2), hexc('#ecd8b0'), 0.9)
    for hx, hy in ((23.8, 30.4), (31.7, 34.4), (20.2, 29.9), (28.0, 33.9)):
        cv.part(sd_ellipse(hx, hy, 0.85, 0.5), hexc('#22160e'), R=0.4, edge=0.2)
    return finish([(cv, 0.6)], sh)


def icon_iron():
    cv, sh = Canvas(), Shadow()
    base = hexc('#56625f')
    chunks = [
        (sd_poly([(12.5, 18.0), (15.0, 15.2), (19.5, 15.0), (22.4, 17.2), (21.6, 21.6), (17.0, 23.0), (13.2, 21.4)]), 17.3, 18.8, 11),
        (sd_poly([(20.8, 20.5), (23.6, 18.0), (29.0, 18.4), (33.6, 21.2), (34.2, 26.0), (31.0, 29.5), (24.8, 29.6), (21.0, 26.6)]), 27.5, 23.5, 12),
        (sd_poly([(16.0, 31.0), (18.4, 28.2), (23.6, 27.6), (28.6, 29.0), (29.0, 32.6), (25.4, 35.0), (19.0, 35.0)]), 22.5, 31.5, 13),
    ]
    for d, cx, cy, s in chunks:
        sh.shape(d + 0.2)
    sh.A = ndi.shift(sh.A, (1.0 * SS, 1.3 * SS), order=1)
    for i, (d, cx, cy, s) in enumerate(chunks):
        d = d + 0.0 * smooth_noise(s, 1.5)
        n, ridge = facet_normal(d, cx, cy, s, k=8, strength=0.7, R=1.0)
        tint = mix(base, hexc('#6f7f6a'), 0.5 + 0.5 * smooth_noise(s + 50, 2.5))
        col = shade(tint, n, d, spec=0.85, shin=14, spec_col=(0.75, 0.95, 1.0), amb=0.4)
        col = np.clip(col * (1 + 0.18 * ridge)[..., None], 0, 1)
        cv.put(d, col)
        # rusty/metallic flecks
        rng = np.random.default_rng(s)
        for _ in range(5):
            px, py = cx + rng.uniform(-4, 4), cy + rng.uniform(-3, 3)
            fl = sd_ellipse(px, py, rng.uniform(0.5, 0.9), rng.uniform(0.35, 0.6), rng.uniform(0, 180))
            c = hexc('#a7743e') if rng.random() < 0.4 else hexc('#c8f0ee')
            cv.atop(np.maximum(fl, d + 0.4), c, 0.8)
    return finish([(cv, 0.6)], sh)


def icon_saltpeter():
    cv, sh = Canvas(), Shadow()
    lumps = [
        (sd_poly([(16.6, 24.0), (18.0, 20.6), (21.6, 19.4), (24.4, 21.0), (24.6, 25.2), (21.0, 27.2), (17.6, 26.6)]), 20.6, 23.4, 21),
        (sd_poly([(25.0, 21.0), (27.6, 17.4), (31.0, 17.0), (33.4, 19.6), (33.2, 24.0), (29.4, 26.4), (25.6, 25.4)]), 29.2, 21.6, 22),
        (sd_poly([(20.6, 19.6), (22.4, 16.0), (26.0, 15.4), (28.6, 17.6), (27.6, 21.6), (23.6, 22.4)]), 24.6, 18.8, 23),
        (sd_poly([(21.0, 25.0), (23.6, 22.6), (27.6, 23.0), (29.6, 25.6), (27.4, 28.0), (22.6, 28.0)]), 25.4, 25.6, 24),
        (sd_poly([(17.0, 29.0), (18.6, 27.0), (21.0, 27.2), (21.8, 29.6), (19.6, 31.2), (17.6, 30.8)]), 19.4, 29.2, 25),
        (sd_poly([(28.6, 28.0), (30.4, 26.4), (34.0, 26.6), (35.6, 28.0), (33.6, 29.6), (29.6, 29.6)]), 32.0, 28.0, 26),
    ]
    for d, cx, cy, s in lumps:
        sh.shape(d)
    sh.A = ndi.shift(sh.A, (1.0 * SS, 1.2 * SS), order=1)
    for d, cx, cy, s in lumps:
        d = d + 0.12 * smooth_noise(s, 0.8)
        n, ridge = facet_normal(d, cx, cy, s, k=8, strength=0.7, R=0.9)
        tint = mix(hexc('#c4c4be'), hexc('#a8b0be'), 0.5 + 0.5 * smooth_noise(s + 9, 1.5))
        col = shade(tint, n, d, amb=0.55, dif=0.6, spec=0.45, shin=20, rim=0.3, edge=0.3)
        col = np.clip(col + (0.08 * ridge)[..., None], 0, 1)
        cv.put(d, col)
        rng = np.random.default_rng(s + 100)
        for _ in range(4):
            px, py = cx + rng.uniform(-2.5, 2.5), cy + rng.uniform(-2, 2)
            cv.atop(np.maximum(sd_circle(px, py, rng.uniform(0.3, 0.45)), d + 0.5),
                    [hexc('#ffffff'), hexc('#f2dcea'), hexc('#9ea2aa')][rng.integers(3)], 0.8)
    return finish([(cv, 0.5)], sh)


def icon_coal():
    cv, sh = Canvas(), Shadow()
    lumps = [
        (sd_poly([(20.0, 16.2), (23.5, 14.2), (28.0, 15.0), (29.2, 18.4), (25.5, 20.5), (20.5, 19.6)]), 24.5, 17.3),
        (sd_poly([(13.0, 19.6), (15.0, 17.8), (19.5, 17.6), (21.6, 20.0), (19.8, 23.2), (14.5, 22.8)]), 17.3, 20.4),
        (sd_poly([(25.0, 18.6), (29.5, 17.4), (34.6, 18.6), (36.0, 21.4), (34.0, 24.0), (27.0, 24.2), (24.6, 21.6)]), 30.0, 20.8),
        (sd_poly([(22.5, 24.8), (26.0, 23.2), (30.6, 24.6), (31.2, 28.0), (28.0, 30.0), (23.6, 29.4)]), 27.0, 26.8),
        (sd_poly([(17.0, 27.0), (19.0, 26.3), (20.4, 27.4), (19.6, 28.6), (17.4, 28.4)]), 18.7, 27.4),
        (sd_poly([(18.4, 31.0), (20.8, 29.6), (23.0, 30.8), (22.2, 33.0), (19.0, 33.0)]), 20.6, 31.4),
        (sd_poly([(30.4, 34.3), (31.8, 33.4), (33.2, 34.4), (32.6, 36.5), (30.8, 36.2)]), 31.8, 35.0),
    ]
    for d, cx, cy in lumps:
        sh.shape(d)
    sh.A = ndi.shift(sh.A, (0.9 * SS, 1.2 * SS), order=1)
    for i, (d, cx, cy) in enumerate(lumps):
        n, ridge = facet_normal(d, cx, cy, 30 + i, k=7, strength=0.75, R=0.9)
        col = shade(hexc('#26201f'), n, d, amb=0.5, dif=0.75, spec=0.75, shin=26,
                    spec_col=(0.75, 0.8, 0.92), edge=0.25, rim=0.3)
        col = np.clip(col + (0.05 * ridge)[..., None], 0, 1)
        cv.put(d, col)
    return finish([(cv, 0.35)], sh)


def icon_oil():
    cv, sh = Canvas(), Shadow()
    pool = union(sd_ellipse(22.6, 29.0, 9.4, 4.2), sd_ellipse(28.0, 30.0, 6.0, 3.6), sd_ellipse(17.0, 28.6, 3.8, 2.6), k=1.6)
    pool = pool + 0.45 * smooth_noise(41, 1.2)
    edge_t = np.clip(-pool / 1.4, 0, 1)
    water = mix(hexc('#2c8e9c'), hexc('#1a4e5e'), edge_t)
    flat = _norm(np.zeros_like(X), np.full_like(X, -0.2), np.ones_like(X))
    cv.put(pool, shade(water, flat, pool, amb=0.85, dif=0.25, rim=0.0, edge=0.3))
    oil = pool + 1.0 + 0.3 * smooth_noise(42, 0.8)
    sheen = 0.5 + 0.5 * np.sin((X * 0.8 + Y * 2.4) * 0.8 + 2.5 * smooth_noise(43, 1.5))
    oilc = mix(hexc('#140e0a'), hexc('#3c2a1c'), sheen * 0.7)
    cv.put(oil, oilc, soft=0.6)
    rainbow = mix(mix(hexc('#8a5ac8'), hexc('#3fb39a'), sstep(17, 24, X)), hexc('#d8b04a'), sstep(24, 30, X))
    cv.atop(np.maximum(sd_ellipse(21.0, 28.6, 4.2, 1.3, -6), oil + 0.6), rainbow, 0.35, soft=1.0)
    cv.atop(sd_ellipse(20.4, 28.0, 2.6, 0.45, -6), hexc('#e8f4ff'), 0.35, soft=0.35)
    ring = np.abs(sd_ellipse(24.6, 29.8, 2.4, 0.9)) - 0.3
    cv.atop(ring, hexc('#5a4a3c'), 0.8)
    cv.atop(np.abs(sd_ellipse(24.6, 29.6, 2.4, 0.8)) - 0.12, hexc('#a89a8a'), 0.45)
    drop = union(sd_circle(24.6, 19.8, 2.5), sd_poly([(24.6, 10.8), (22.5, 18.8), (26.7, 18.8)]), k=1.4)
    dc = mix(hexc('#3a4846'), hexc('#100e0c'), sstep(12, 22, Y))
    n = bulge(drop, 2.4)
    col = shade(dc, n, drop, amb=0.6, dif=0.6, spec=1.3, shin=30, rim=0.6, spec_col=(0.85, 1.0, 0.98))
    cv.put(drop, col)
    cv.atop(sd_ellipse(23.4, 18.8, 0.5, 1.5, 12), hexc('#ffffff'), 0.85)
    cv.atop(sd_seg((24.2, 13.4), (23.9, 16.2), 0.22, 0.35), hexc('#d8e8e2'), 0.55)
    cv.atop(np.maximum(sd_ellipse(25.6, 21.4, 1.2, 0.5, -20), drop + 0.3), hexc('#3a8a8a'), 0.6)
    return finish([(cv, 0.5)], sh, shadow_alpha=0.3)


def _tire(cv, cx, cy, rx, ry, depth, hole, seed):
    rub = hexc('#2f2a2e')
    # tread: the outer ellipse swept back along the axle (to the upper left)
    steps = 14
    tread = None
    for k in range(steps + 1):
        f = k / steps
        e = sd_ellipse(cx - depth * f, cy - depth * 0.15 * f, rx, ry)
        tread = e if tread is None else np.minimum(tread, e)
    ang = np.arctan2((Y - cy) / ry, (X - cx) / rx)
    groove = 0.5 + 0.5 * np.cos(ang * 16)
    nt = _norm(np.cos(ang) * 0.9 - 0.35, np.sin(ang) * 0.9, np.full_like(X, 0.45))
    tc = rub * (0.8 + 0.35 * groove)[..., None]
    cv.put(tread, shade(tc, nt, tread, amb=0.5, dif=0.65, spec=0.25, shin=8, edge=0.3))
    # sidewall (front face): a torus-like ring
    face = sd_ellipse(cx, cy, rx, ry)
    hl = sd_ellipse(cx + 0.2, cy + 0.1, rx * hole, ry * hole)
    ring = cut(face, hl)
    w = rx * (1 - hole) / 2
    n = bulge(ring, w * 0.95)
    side = mix(hexc('#3d373c'), hexc('#25212a'), 0.5 + 0.5 * smooth_noise(seed, 2))
    cv.put(ring, shade(side, n, ring, amb=0.55, dif=0.75, spec=0.35, shin=10, rim=0.35))
    # lettering band highlight on the sidewall
    band = np.abs(sd_ellipse(cx, cy, rx * (1 + hole) / 2, ry * (1 + hole) / 2)) - 0.18
    cv.atop(np.maximum(band, -ring - 0.3), hexc('#6c6670'), 0.35)
    # inside of the hole: the far inner wall
    inner = cut(hl, sd_ellipse(cx - depth + 0.2, cy - depth * 0.15, rx * hole, ry * hole))
    cv.put(np.maximum(inner, hl), hexc('#17141a'))


def icon_rubber():
    cv, sh = Canvas(), Shadow()
    sh.ellipse(27.4, 32.0, 11.0, 2.2, 0.8)
    _tire(cv, 21.8, 23.4, 5.4, 6.6, 2.6, 0.42, 51)
    _tire(cv, 33.0, 25.2, 6.0, 7.6, 3.0, 0.42, 52)
    return finish([(cv, 0.5)], sh)


def icon_aluminum():
    cv, sh = Canvas(), Shadow()
    cx, r = 25.6, 6.2
    top, bot = 15.6, 33.2
    ery = 2.3
    sh.ellipse(cx + 2.0, bot + 0.6, r + 0.8, ery + 0.3, 0.9)
    body = union(np.maximum(np.abs(X - cx) - r, np.maximum(top - Y, Y - bot)),
                 sd_ellipse(cx, bot, r, ery))
    body = np.maximum(body, np.minimum(sd_ellipse(cx, top, r, ery), top - Y))
    nx = np.clip((X - cx) / r, -0.999, 0.999)
    n = _norm(nx, np.zeros_like(X) - 0.05, np.sqrt(1 - nx ** 2))
    refl = 0.78 + 0.22 * np.cos(nx * 5.5 + 0.8)
    silver = mix(hexc('#b9bec8'), hexc('#a8a2b8'), sstep(top, bot, Y) * 0.5) * refl[..., None]
    cv.put(body, shade(silver, n, body, amb=0.55, dif=0.6, spec=0.9, shin=16, rim=0.3))
    # rolled rims
    for yy, a in ((bot - 1.6, 0.35), (top + 1.4, 0.25)):
        cv.atop(np.abs(sd_ellipse(cx, yy, r, ery)) - 0.22, hexc('#5c5e6a'), a)
    # lid
    lid = sd_ellipse(cx, top, r - 0.2, ery - 0.1)
    nl = _norm(np.zeros_like(X), np.full_like(X, -0.55), np.ones_like(X))
    cv.put(lid, shade(hexc('#d4d7de'), nl, lid, amb=0.6, dif=0.5, spec=0.3, edge=0.5, rim=0))
    cv.atop(np.abs(lid + 0.5) - 0.25, hexc('#f4f6fa'), 0.6)
    cv.atop(sd_ellipse(cx + 0.6, top + 0.3, 1.7, 0.85), hexc('#3a3238'))
    cv.atop(sd_ellipse(cx - 1.2, top - 0.4, 1.0, 0.45), hexc('#9a9ca6'), 0.8)
    return finish([(cv, 0.55)], sh)


def icon_uranium():
    cv, sh = Canvas(), Shadow()
    glow = Canvas()
    gd = union(sd_ellipse(24.0, 23.4, 8.0, 4.4), sd_ellipse(33.6, 21.6, 3.6, 2.6), k=2)
    glow.put(gd, hexc('#7dff52'), 0.3, soft=3.0)
    green = hexc('#3fd832')
    lumps = [
        (sd_poly([(19.0, 21.4), (20.6, 18.4), (24.0, 17.6), (26.4, 19.4), (25.6, 23.0), (21.4, 24.2)]), 22.6, 20.8, 71, 1.0),
        (sd_poly([(24.6, 21.6), (26.6, 19.6), (29.6, 20.2), (30.4, 23.0), (28.0, 25.0), (25.2, 24.4)]), 27.6, 22.4, 73, 0.9),
        (sd_poly([(31.0, 20.0), (32.6, 18.2), (35.4, 18.6), (36.4, 21.0), (34.6, 22.6), (31.8, 22.2)]), 33.6, 20.4, 72, 0.95),
        (sd_poly([(20.6, 26.6), (22.6, 24.8), (25.6, 25.4), (25.8, 27.8), (23.0, 29.2), (20.8, 28.4)]), 23.2, 27.0, 74, 0.85),
        (sd_poly([(16.0, 24.4), (17.6, 23.0), (19.4, 23.8), (19.0, 25.8), (16.8, 26.0)]), 17.6, 24.6, 77, 0.8),
        (sd_circle(12.6, 26.4, 0.9), 12.6, 26.4, 75, 0.8),
        (sd_circle(35.4, 25.2, 1.0), 35.4, 25.2, 76, 0.8),
        (sd_circle(28.8, 27.6, 0.8), 28.8, 27.6, 78, 0.8),
    ]
    for d, cx, cy, s, k in lumps:
        sh.shape(d, 0.9)
    sh.A = ndi.shift(sh.A, (0.8 * SS, 1.0 * SS), order=1)
    for d, cx, cy, s, k in lumps:
        n, ridge = facet_normal(d, cx, cy, s, k=6, strength=0.6, R=1.0)
        col = shade(green * k, n, d, amb=0.6, dif=0.6, spec=0.5, shin=14, rim=0.35, edge=0.5, ew=0.5)
        inner = np.clip(-d / 1.8, 0, 1)
        col = np.clip(col + (inner * 0.3)[..., None] * hexc('#e0ffb8') + (0.12 * ridge)[..., None], 0, 1)
        cv.put(d, col)
        # dark ore veins
        rng = np.random.default_rng(s)
        for _ in range(2):
            px, py = cx + rng.uniform(-1.5, 1.5), cy + rng.uniform(-1.2, 1.2)
            cv.atop(np.maximum(sd_ellipse(px, py, 0.6, 0.4, rng.uniform(0, 180)), d + 0.5), hexc('#1e4a1a'), 0.6)
    return finish([(glow, 0), (cv, 0.5)], sh, shadow_alpha=0.32)


def icon_wine():
    cv, sh = Canvas(), Shadow()
    sh.ellipse(26.4, 34.6, 6.0, 1.8, 0.7)
    # stem and leaves (behind)
    leafc = hexc('#4aa83a')
    stem = sd_path(bez((24.6, 17.0), (25.0, 14.0), (26.0, 12.0), (26.4, 9.6), n=10), 0.42)
    cv.part(stem, hexc('#6a7a2a'), R=0.4)
    for pts, s in (([(26.0, 12.6), (28.6, 9.6), (32.0, 10.6), (35.8, 12.6), (32.0, 13.6), (28.6, 14.2)], 1),
                   ([(25.2, 14.0), (23.0, 12.6), (20.6, 13.6), (22.4, 15.2)], 2)):
        d = sd_poly(pts)
        n = bulge(d, 1.6)
        n = tilt(n, -0.15, -0.3)
        col = shade(mix(leafc, hexc('#78c84a'), sstep(36, 26, X) * 0.5), n, d, amb=0.5, rim=0.2)
        cv.put(d, col)
    cv.atop(sd_path([(26.2, 12.6), (29.5, 11.8), (34.5, 12.4)], 0.18), hexc('#2f6e24'), 0.7)
    grapes = [  # back to front
        (23.8, 18.4, 2.2), (28.2, 18.2, 2.3), (31.2, 22.6, 2.1),
        (21.4, 21.6, 2.2), (25.8, 21.2, 2.4), (29.2, 21.8, 2.2),
        (23.4, 24.6, 2.3), (27.4, 24.8, 2.3), (19.8, 25.8, 2.1),
        (25.0, 28.0, 2.2), (21.6, 29.0, 2.1), (28.2, 28.4, 1.9),
        (27.0, 33.0, 2.1),
    ]
    stalk = sd_path([(25.0, 18.0), (25.5, 27.0), (26.8, 31.2)], 0.3)
    cv.part(stalk, hexc('#5a4a2a'), R=0.3)
    for i, (gx, gy, r) in enumerate(grapes):
        d = sd_circle(gx, gy, r)
        depth = 0.75 + 0.25 * (i / len(grapes))
        base = mix(hexc('#6a2a86'), hexc('#8e3a8e'), (gy - 18) / 16) * depth
        n = bulge(d, r)
        col = shade(base, n, d, amb=0.4, dif=0.8, spec=0.7, shin=26, rim=0.35, edge=0.6, ew=0.35,
                    spec_col=(1.0, 0.9, 1.0))
        cv.put(d, col)
        cv.atop(np.maximum(sd_circle(gx - r * 0.38, gy - r * 0.42, r * 0.28), d), hexc('#f2d0f4'), 0.55, soft=0.25)
    return finish([(cv, 0.6)], sh)


def icon_furs():
    cv, sh = Canvas(), Shadow()
    sh.ellipse(25.6, 30.2, 10.0, 2.4, 0.7, ang=-14)
    fur = hexc('#5c3a1a')
    dark = hexc('#2a180c')
    gold = hexc('#d29a46')
    fuzz = smooth_noise(92, 0.35)
    # far legs
    cv.part(limb([(22.4, 27.6), (23.2, 30.2), (23.0, 31.4)], [0.85, 0.6, 0.5]), dark * 0.9, R=0.6, edge=0.3)
    cv.part(limb([(30.0, 23.4), (30.8, 26.6), (30.4, 28.4)], [0.9, 0.6, 0.5]), dark * 0.9, R=0.6, edge=0.3)
    # bushy tail
    tail = bez((30.0, 21.8), (33.0, 19.6), (36.0, 20.6), (38.8, 19.2), n=16)
    prof = [1.3 + 0.9 * math.sin(t * math.pi) ** 0.8 for t in np.linspace(0, 1, 16)]
    prof[-1] = 0.5
    td = sd_path(tail, prof) + 0.35 * fuzz
    cv.part(td, mix(dark * 1.3, dark * 0.7, sstep(31, 37, X)), R=1.4, edge=0.3)
    # body with near legs
    body = sd_path(bez((18.8, 27.6), (22.0, 24.2), (27.0, 22.6), (31.0, 21.8), n=14), taper(14, 2.3, 2.6))
    body = union(body, sd_ellipse(29.0, 23.0, 3.3, 2.7, -20), k=1.2)
    fore = limb([(20.6, 28.0), (19.8, 31.0), (20.2, 32.4)], [1.2, 0.68, 0.56])
    hind = limb([(28.0, 23.8), (27.8, 27.4), (28.4, 28.8)], [1.6, 0.7, 0.56])
    body = union(body, fore, hind, k=0.7) + 0.18 * fuzz
    side = Y + (X - 24) * 0.45
    tone = mix(fur, gold, sstep(25.8, 21.8, side) * 0.75)
    tone = mix(tone, dark * 1.2, sstep(25.4, 28.6, side) * 0.85)
    streak = 0.5 + 0.5 * np.sin((X * 0.45 - Y) * 5.0 + 3 * fuzz)
    tone = tone * (0.9 + 0.12 * streak)[..., None]
    cv.part(body, tone, R=2.2, spec=0.2, shin=14, edge=0.38)
    # head
    cv.part(sd_circle(19.0, 25.4, 0.85), fur * 0.8, R=0.5)
    head = union(sd_ellipse(17.6, 27.8, 2.6, 2.1, -20), sd_seg((16.6, 28.4), (14.6, 29.4), 1.3, 0.65), k=0.8)
    cv.part(head, mix(fur, gold, 0.35), R=1.4)
    cv.atop(np.maximum(sd_ellipse(16.5, 29.0, 1.8, 1.0, -20), head + 0.15), hexc('#f2e6cc'), 0.95)
    cv.atop(sd_circle(14.7, 29.5, 0.5), hexc('#120a06'))
    cv.atop(sd_circle(17.1, 27.3, 0.38), hexc('#120a06'))
    cv.atop(sd_circle(17.2, 27.2, 0.12), hexc('#ffffff'), 0.8)
    for hx, hy in ((20.2, 32.4), (28.4, 28.8)):
        cv.atop(sd_ellipse(hx, hy, 0.8, 0.45), dark * 0.6)
    return finish([(cv, 0.6)], sh)


def icon_dyes():
    cv, sh = Canvas(), Shadow()
    sh.ellipse(26.0, 33.0, 8.4, 2.4, 0.85)
    cx, cy, rx, ry = 24.6, 26.6, 7.6, 3.2
    bowl_out = union(np.maximum(sd_ellipse(cx, cy + 0.6, rx, 7.0), Y - (cy + 6.6)),
                     sd_ellipse(cx, cy, rx, ry))
    bowl_out = np.maximum(bowl_out, cy - ry - Y)
    nb = bulge(bowl_out, 3.0)
    wood = mix(hexc('#c99c62'), hexc('#7a4e2c'), sstep(cy, cy + 6, Y))
    grain = 0.92 + 0.08 * np.sin(Y * 6.0 + 2 * smooth_noise(101, 1.0))
    cv.put(bowl_out, shade(wood * grain[..., None], nb, bowl_out, amb=0.5, spec=0.15))
    rimd = np.abs(sd_ellipse(cx, cy, rx - 0.5, ry - 0.4)) - 0.5
    cv.part(rimd, hexc('#e4c48e'), R=0.5, rim=0)
    liquid = sd_ellipse(cx, cy + 0.1, rx - 1.1, ry - 0.9)
    cv.put(liquid, mix(hexc('#e83cb8'), hexc('#a01e78'), sstep(cy - 2, cy + 2, Y)))
    cv.atop(sd_ellipse(cx - 3.0, cy - 0.7, 1.6, 0.4), hexc('#ffa6e6'), 0.6)
    # flap of cloth hanging over to the right (behind the rising part)
    flap = sd_poly([(28.0, 12.0), (31.0, 10.8), (34.6, 11.8), (37.0, 14.4), (38.8, 19.0), (38.4, 22.4),
                    (37.0, 21.0), (35.6, 22.2), (34.2, 18.4), (32.2, 15.8), (29.6, 14.6)])
    flap = flap + 0.15 * smooth_noise(102, 0.6) - 0.3
    pleat = np.sin((X * 0.75 - Y * 0.55) * 2.6)
    n = bulge(flap, 0.9)
    n = tilt(n, 0.35 * pleat * 0.55, 0.35 * pleat * -0.75)
    fc = mix(hexc('#f2c2e4'), hexc('#a6a8c8'), sstep(33.0, 37.5, X))
    cv.put(flap, shade(fc, n, flap, amb=0.55, dif=0.55, spec=0.12, rim=0.25, edge=0.45))
    # the part rising out of the dye
    rise = sd_poly([(23.4, 27.2), (23.8, 21.6), (24.6, 15.6), (26.4, 12.0), (29.4, 11.2), (31.4, 12.8),
                    (30.6, 17.0), (30.0, 21.6), (30.2, 27.2)])
    rise = rise + 0.15 * smooth_noise(103, 0.6) - 0.2
    pleat = np.sin((X + (Y - 20) * 0.12) * 2.2 + 0.4)
    n = bulge(rise, 1.0)
    n = tilt(n, 0.4 * pleat, 0)
    rc = mix(hexc('#e83abc'), hexc('#f6d2ec'), sstep(24.0, 14.0, Y))
    cv.put(rise, shade(rc, n, rise, amb=0.58, dif=0.55, spec=0.15, rim=0.3, edge=0.45))
    # rounded fold at the top
    fold = sd_path(bez((25.4, 13.6), (27.0, 11.0), (29.8, 10.6), (31.6, 12.0), n=10), 1.0)
    cv.part(fold, hexc('#f8e2f2'), R=1.0, edge=0.3, rim=0.3)
    cv.atop(np.maximum(np.abs(sd_ellipse(cx, cy, rx - 1.1, ry - 0.9)) - 0.25, rise + 0.0), hexc('#b0208a'), 0.6)
    for px, py in ((23.6, 28.4), (27.4, 29.2)):
        cv.atop(sd_ellipse(px, py, 0.5, 0.9), hexc('#d82ca8'), 0.7)
    return finish([(cv, 0.6)], sh)


def icon_incense():
    smoke, cv, sh = Canvas(), Canvas(), Shadow()
    sh.ellipse(29.6, 39.0, 5.0, 1.6, 0.9)
    puffs = [(28.8, 27.6, 1.4), (28.2, 25.2, 2.0), (29.8, 22.4, 2.9), (31.6, 19.2, 3.5), (29.0, 15.8, 3.3),
             (24.6, 14.6, 3.2), (20.2, 13.6, 3.4), (16.4, 11.4, 3.2), (14.8, 7.8, 2.6), (17.6, 4.8, 2.7),
             (21.4, 3.8, 2.0), (24.6, 7.0, 2.4), (23.0, 10.6, 2.6), (13.2, 14.0, 2.0), (33.0, 14.6, 2.2)]
    d = None
    for x, y, r in puffs:
        e = sd_circle(x, y, r)
        d = e if d is None else smin(d, e, 1.6)
    wob = smooth_noise(111, 1.2)
    d = d + 0.6 * wob
    n = bulge(d, 3.4)
    sc = mix(hexc('#faf6dc'), hexc('#d2c67a'), sstep(-0.5, 0.7, wob) * 0.7)
    sc = mix(sc, hexc('#c8bc6a'), sstep(10, 2, Y) * 0.3)
    col = shade(sc, n, d, amb=0.72, dif=0.36, spec=0.0, rim=0.2, edge=0.15, ew=0.8)
    a = 0.88 - 0.25 * sstep(24, 3, Y)
    smoke.put(d, col, a, soft=0.35)
    for pts in (bez((22.0, 14.0), (18.0, 12.6), (18.0, 8.0), (21.0, 7.4), n=12),
                bez((30.6, 22.0), (33.4, 20.0), (32.4, 16.6), (29.4, 17.0), n=12)):
        smoke.atop(np.maximum(sd_path(pts, 0.3), d + 0.6), hexc('#a8a060'), 0.45)
    brass = hexc('#c9952e')
    foot = sd_ellipse(29.4, 38.2, 3.6, 1.4)
    cv.part(union(foot, sd_seg((29.4, 33.6), (29.4, 37.6), 1.1, 1.6), k=0.6), mix(brass, hexc('#7a4a18'), 0.35), R=1.0, spec=0.6, shin=14)
    cup = union(np.maximum(sd_ellipse(29.0, 30.2, 5.0, 4.6), Y - 34.6), sd_ellipse(29.0, 30.2, 5.0, 1.8))
    cup = np.maximum(cup, 28.4 - Y)
    cc = mix(hexc('#ecc04a'), hexc('#8a521a'), sstep(30, 35, Y) * 0.7 + sstep(26, 33, X) * 0.3)
    cv.part(cup, cc, R=2.4, spec=0.9, shin=18, spec_col=(1, 0.95, 0.75))
    cv.part(np.abs(sd_ellipse(29.0, 30.2, 4.6, 1.5)) - 0.4, hexc('#f2d070'), R=0.4, rim=0)
    cv.put(sd_ellipse(29.0, 30.3, 4.0, 1.1), hexc('#4a2a14'))
    cv.atop(sd_ellipse(28.6, 30.2, 1.6, 0.6), hexc('#f07a2a'), 0.9, soft=0.4)
    cv.atop(np.abs(sd_ellipse(29.0, 32.6, 4.6, 1.4)) - 0.2, hexc('#6a3a12'), 0.5)
    return finish([(smoke, 0.15), (cv, 0.6)], sh)


def _pile(cv, cx, cy, rx, ry, h, base, seed, flecks=()):
    d = sd_ellipse(cx, cy, rx, ry)
    d = d + 0.35 * smooth_noise(seed, 1.5)
    # a cone-ish heap: height falls off from a peak above center
    px, py = cx - rx * 0.1, cy - ry * 0.25
    rr = np.hypot((X - px) / rx, (Y - py) / ry)
    hgt = h * np.clip(1 - rr, 0, 1) ** 0.8
    gy, gx = np.gradient(hgt, 1.0 / SS)
    nb = bulge(d, 1.2)
    n = _norm(-gx + nb[0] * 0.5, -gy + nb[1] * 0.5, np.ones_like(X))
    cv.put(d, shade(base, n, d, amb=0.5, dif=0.7, spec=0.1, rim=0.2, edge=0.35))
    rng = np.random.default_rng(seed)
    for c, cnt in flecks:
        for _ in range(cnt):
            a = rng.uniform(0, 2 * np.pi)
            r = math.sqrt(rng.uniform(0, 0.7))
            fx, fy = cx + math.cos(a) * r * rx, cy + math.sin(a) * r * ry
            cv.atop(np.maximum(sd_circle(fx, fy, rng.uniform(0.35, 0.55)), d + 0.4), c, 0.8)
    return d


def icon_spices():
    cv, sh = Canvas(), Shadow()
    for cx, cy, rx, ry in ((31.2, 27.4, 7.2, 2.2), (23.6, 32.6, 6.6, 2.2), (33.6, 38.2, 7.4, 2.4)):
        sh.ellipse(cx + 1.2, cy + 0.3, rx, ry)
    heap(cv, 31.0, 26.4, 6.8, 1.9, 4.6, hexc('#dc7e2a'), 121,
         flecks=((hexc('#9a3418'), 7), (hexc('#f4bc64'), 6), (hexc('#e85a7a'), 2)))
    heap(cv, 23.4, 31.6, 6.4, 1.9, 4.4, hexc('#efebe4'), 122,
         flecks=((hexc('#8a8680'), 7), (hexc('#ffffff'), 4)))
    cv.part(sd_ellipse(15.8, 33.2, 1.5, 0.7), hexc('#a8a49c'), R=0.5)
    cv.part(sd_ellipse(18.2, 34.0, 0.9, 0.5), hexc('#c8c4bc'), R=0.4)
    heap(cv, 33.4, 37.2, 7.2, 2.0, 5.0, hexc('#c8c050'), 123,
         flecks=((hexc('#6e7e26'), 8), (hexc('#f2e6a0'), 5), (hexc('#e07a8a'), 2)))
    return finish([(cv, 0.55)], sh)


def icon_ivory():
    cv, sh = Canvas(), Shadow()
    sh.ellipse(28.4, 33.0, 9.4, 2.6, 0.7)
    for fx, fy in ((23.6, 33.6), (27.4, 32.8), (32.6, 31.8), (34.2, 30.8)):
        sh.ellipse(fx + 0.7, fy + 0.2, 2.0, 0.7, 1.0)
    skin = hexc('#958479')
    dark = hexc('#4a3e3c')
    hi = hexc('#c2aea2')
    # far legs
    far = mix(skin * 0.62, dark * 0.9, sstep(27, 32, Y) * 0.6)
    cv.part(limb([(33.6, 22.0), (34.4, 26.0), (34.2, 30.6)], [2.0, 1.6, 1.55]), far, R=1.2, edge=0.3)
    cv.part(limb([(27.0, 25.0), (27.4, 29.0), (27.4, 32.6)], [1.9, 1.6, 1.55]), far, R=1.2, edge=0.3)
    # body with near legs
    body = union(sd_ellipse(29.4, 19.0, 7.2, 6.0, -12), sd_ellipse(24.6, 22.6, 4.2, 4.2), k=2.0)
    leg1 = limb([(32.0, 22.0), (32.4, 27.0), (32.6, 31.6)], [2.4, 1.75, 1.7])
    leg2 = limb([(23.6, 24.0), (23.6, 29.0), (23.6, 33.4)], [2.3, 1.75, 1.7])
    torso = union(body, leg1, leg2, k=1.0)
    tone = mix(skin, hi, sstep(22, 14, Y) * 0.55)
    tone = mix(tone, skin * 0.75, sstep(26, 31, Y) * 0.6)
    cv.part(torso, tone, R=3.6, spec=0.1, edge=0.38)
    for k in range(3):
        cv.atop(np.maximum(np.abs(sd_ellipse(27.4 + k * 2.6, 21.0, 3.6, 5.6)) - 0.12, body + 0.8), dark, 0.18)
    for lx, ly in ((32.6, 28.4), (23.6, 30.0)):
        for j in range(2):
            cv.atop(np.maximum(np.abs(Y - (ly + j * 1.1)) - 0.1, torso + 0.4) + np.maximum(np.abs(X - lx) - 1.5, 0), dark, 0.25)
    # toenails
    for fx, fy in ((23.6, 33.6), (32.6, 31.8)):
        for j in (-0.9, 0.0, 0.9):
            cv.atop(sd_ellipse(fx + j, fy + 0.6, 0.38, 0.3), hexc('#e8dccc'), 0.9)
    cv.part(sd_path(bez((36.4, 18.4), (37.4, 20.6), (37.0, 23.4), n=8), [0.4] * 8), dark, R=0.3)
    # head, ear, trunk, tusks
    head = union(sd_ellipse(21.0, 22.2, 3.2, 3.6), sd_ellipse(20.6, 25.4, 2.0, 2.4), k=1.0)
    cv.part(head, mix(skin, hi, 0.35), R=2.4)
    ear = union(sd_ellipse(24.2, 21.6, 3.0, 4.4, 8), sd_ellipse(24.8, 19.2, 2.6, 2.1), k=1.0)
    n = tilt(bulge(ear, 1.4), 0.25, 0)
    cv.put(ear, shade(mix(skin * 0.95, hexc('#b49890'), 0.4), n, ear, rim=0.2, edge=0.5))
    cv.atop(np.maximum(sd_ellipse(24.6, 21.8, 1.8, 3.0, 8), ear + 0.7), hexc('#a87a74'), 0.35, soft=0.6)
    trunk_pts = list(bez((20.4, 26.0), (20.6, 30.0), (19.6, 33.0), (18.6, 34.6), n=14)) + [(17.6, 34.2)]
    trunk = sd_path(trunk_pts, taper(15, 1.45, 0.55))
    cv.part(trunk, skin * 0.98, R=1.0)
    for k in range(4):
        yy = 28.4 + k * 1.4
        cv.atop(np.maximum(np.abs(Y - yy) - 0.12, trunk + 0.3), dark, 0.3)
    for pts in (bez((19.0, 27.0), (18.0, 29.4), (16.8, 31.0), (15.6, 31.4), n=10),
                bez((22.0, 27.4), (22.2, 30.0), (21.6, 32.0), (20.6, 33.0), n=10)):
        cv.part(sd_path(pts, taper(10, 0.72, 0.3)), hexc('#f6eed8'), R=0.6, spec=0.5, edge=0.35)
    cv.atop(sd_circle(20.0, 22.2, 0.42), hexc('#1a1210'))
    return finish([(cv, 0.6)], sh)


def icon_silks():
    cv, sh = Canvas(), Shadow()
    sh.ellipse(23.6, 31.0, 4.4, 1.6, 0.9)
    sh.ellipse(33.4, 28.0, 4.2, 1.6, 0.9)
    thread = list(bez((22.8, 30.2), (18.6, 31.6), (18.4, 34.6), (22.6, 34.0), n=14))
    thread += list(bez((22.6, 34.0), (26.6, 33.4), (29.6, 32.4), (33.4, 32.2), n=12))[1:]
    thread += list(bez((33.4, 32.2), (37.8, 31.8), (37.0, 36.2), (32.8, 36.4), n=12))[1:]
    thread += list(bez((32.8, 36.4), (29.8, 36.6), (31.8, 39.4), (29.6, 40.8), n=10))[1:]
    td = sd_path(thread, 0.45)
    sh.shape(td + 0.1, 0.6)
    cv.part(td, hexc('#62d2f4'), R=0.4, rim=0, edge=0.3)
    # orange spool (behind, right)
    cx, top, bot, r = 33.2, 15.0, 27.6, 4.0
    body = np.maximum(np.abs(X - cx) - r, np.maximum(top - Y, Y - bot))
    body = union(body, sd_ellipse(cx, bot, r, 1.4), sd_ellipse(cx, top, r, 1.4))
    nx = np.clip((X - cx) / r, -0.999, 0.999)
    n = _norm(nx, np.zeros_like(X), np.sqrt(1 - nx ** 2))
    wind = 0.88 + 0.12 * np.sin(Y * 5.0 + nx * 1.5)
    oc = mix(hexc('#f47a3a'), hexc('#c84a22'), sstep(top, bot, Y) * 0.4) * wind[..., None]
    cv.put(body, shade(oc, n, body, amb=0.5, dif=0.65, spec=0.3, shin=10))
    lid = sd_ellipse(cx, top, r - 0.1, 1.3)
    cv.put(lid, shade(hexc('#f79e66'), _norm(np.zeros_like(X), np.full_like(X, -0.6), np.ones_like(X)), lid, rim=0))
    cv.atop(np.abs(sd_ellipse(cx, top, r * 0.6, 0.8)) - 0.12, hexc('#c85a2a'), 0.5)
    peg = union(sd_seg((cx, top), (cx, 12.6), 1.0), sd_ellipse(cx, 12.6, 1.0, 0.5))
    cv.part(peg, hexc('#5a3a22'), R=0.8)
    # blue roll (front, left): a fat cocoon of wound thread
    roll = union(sd_ellipse(22.2, 23.8, 3.9, 6.6, -6), sd_ellipse(22.6, 28.8, 3.3, 1.6), k=1.0)
    n = bulge(roll, 3.6)
    wind = 0.86 + 0.14 * np.sin((Y - 0.3 * X) * 4.4)
    bc = mix(hexc('#50c8f6'), hexc('#2a8ad0'), sstep(19, 30, Y) * 0.5) * wind[..., None]
    cv.put(roll, shade(bc, n, roll, amb=0.5, dif=0.7, spec=0.5, shin=16, rim=0.3))
    cv.atop(np.maximum(sd_ellipse(22.6, 30.0, 2.2, 0.9), roll + 0.4), hexc('#1a4e7a'), 0.5)
    return finish([(cv, 0.6)], sh)


def icon_gems():
    cv, sh = Canvas(), Shadow()
    dirt = union(sd_ellipse(22.6, 27.0, 7.6, 2.8), sd_ellipse(28.4, 26.6, 4.0, 2.0), k=1.5)
    dirt = dirt + 0.5 * smooth_noise(150, 1.0)
    sh.ellipse(25.0, 28.2, 8.4, 2.6, 0.6)
    sh.ellipse(33.4, 32.6, 2.4, 1.0, 0.9)
    n = bulge(dirt, 1.0)
    dc = mix(hexc('#8a6a3a'), hexc('#b4924c'), 0.5 + 0.5 * smooth_noise(151, 1.0))
    cv.put(dirt, shade(dc, n, dirt, amb=0.55, spec=0.0, rim=0.1, edge=0.3))
    # grey host rocks
    rocks = [
        (sd_poly([(16.4, 25.0), (17.8, 21.6), (21.6, 20.6), (24.4, 22.6), (24.0, 26.6), (19.6, 27.6)]), 20.4, 24.2, 161),
        (sd_poly([(25.6, 24.4), (27.0, 21.6), (30.6, 21.2), (32.6, 23.6), (31.4, 26.6), (27.0, 27.0)]), 29.0, 24.2, 162),
        (sd_poly([(30.6, 31.0), (31.6, 29.0), (34.2, 28.8), (35.2, 31.0), (33.6, 33.0), (31.4, 32.8)]), 32.8, 30.8, 164),
    ]
    for d, cx, cy, s in rocks:
        rock(cv, d, cx, cy, hexc('#8e8c90'), s, k=6, strength=0.6, R=0.9, spec=0.3, amb=0.5)
    pale = hexc('#d6dcec')
    for base, tip, w, c, s in (((24.0, 22.4), (21.6, 12.8), 2.0, pale, 1), ((27.6, 22.6), (31.6, 15.0), 1.8, hexc('#cfd2e6'), 2),
                               ((21.2, 23.0), (17.6, 17.4), 1.5, hexc('#c4cce0'), 3), ((25.8, 24.6), (25.6, 16.6), 2.1, hexc('#e4e8f4'), 4),
                               ((29.6, 24.8), (34.6, 20.4), 1.3, hexc('#d0c4e4'), 5), ((32.6, 30.6), (33.4, 26.6), 1.1, pale, 6)):
        crystal(cv, base, tip, w, c, s)
    # a couple of coloured stones in the dirt
    for gx, gy, c in ((18.6, 27.4, '#d23a5a'), (29.2, 27.0, '#3a7ad8')):
        d = sd_poly([(gx - 1.1, gy), (gx, gy - 1.1), (gx + 1.1, gy), (gx, gy + 0.9)])
        n, ridge = facet_normal(d, gx, gy, int(gx * 10), k=4, strength=0.8, R=0.5)
        cv.put(d, shade(hexc(c), n, d, amb=0.65, spec=1.0, shin=20, edge=0.3))
    return finish([(cv, 0.55)], sh)


def icon_whales():
    cv = Canvas()
    foam = Canvas()
    foam.put(sd_ellipse(24.0, 17.6, 4.4, 1.4), hexc('#e8f6fa'), 0.5, soft=0.8)
    foam.put(np.abs(sd_ellipse(24.0, 17.6, 5.8, 1.9)) - 0.25, hexc('#ffffff'), 0.45, soft=0.3)
    stock = sd_path(bez((23.8, 17.8), (24.4, 14.6), (25.6, 12.0), (27.0, 9.8), n=12), taper(12, 1.9, 1.5))
    left = sd_poly([(27.4, 9.0), (24.0, 6.6), (20.4, 5.4), (16.6, 4.0), (17.6, 6.0), (19.8, 8.2), (23.0, 10.4), (26.0, 12.2)])
    right = sd_poly([(26.6, 8.8), (30.4, 7.4), (35.0, 6.4), (39.8, 5.2), (39.0, 6.8), (36.4, 9.0), (32.4, 11.0), (28.4, 12.2)])
    fluke = union(left, right, k=0.6)
    fluke = union(fluke, stock, sd_circle(27.0, 10.2, 1.8), k=1.0)
    upper = sstep(11.0, 6.5, Y + np.abs(X - 27.0) * 0.12)
    col0 = mix(hexc('#2e2a3a'), hexc('#8e8ea0'), upper)
    n = bulge(fluke, 1.6)
    cv.put(fluke, shade(col0, n, fluke, amb=0.5, dif=0.75, spec=0.7, shin=18, rim=0.4))
    # pale trailing edges along the top of the lobes, notch at the middle
    for pts in ([(17.2, 4.5), (20.6, 5.8), (24.2, 7.2), (26.4, 8.6)], [(27.6, 8.6), (30.6, 7.6), (35.0, 6.6), (39.2, 5.5)]):
        cv.atop(np.maximum(sd_path(pts, 0.6), fluke + 0.3), hexc('#eceef4'), 0.75)
    cv.atop(sd_circle(27.0, 8.7, 0.5), hexc('#2a2632'), 0.8)
    cv.atop(np.maximum(sd_path([(19.6, 7.6), (24.0, 10.2)], 0.35), fluke + 0.6), hexc('#6a4a74'), 0.35)
    cv.atop(np.maximum(sd_path([(30.4, 10.0), (35.0, 8.4)], 0.35), fluke + 0.6), hexc('#6a4a74'), 0.35)
    for x, y in ((18.4, 7.8), (37.6, 8.6), (21.2, 10.0), (33.8, 11.2)):
        cv.put(sd_ellipse(x, y, 0.35, 0.6), hexc('#d8f0f8'), 0.8)
    return finish([(foam, 0), (cv, 0.6)], None)


def icon_game():
    cv, sh = Canvas(), Shadow()
    sh.ellipse(29.6, 33.4, 8.6, 2.3, 0.7)
    for hx, hy in ((23.0, 35.4), (26.4, 35.0), (33.0, 31.2), (36.0, 33.4)):
        sh.ellipse(hx + 0.6, hy + 0.2, 1.1, 0.45, 1.0)
    coat = hexc('#86654f')
    hi = hexc('#b48c6a')
    dark = hexc('#3a2a22')
    far = mix(coat * 0.62, dark * 0.9, sstep(25, 29, Y))
    cv.part(limb([(26.6, 23.6), (26.2, 27.6), (26.2, 31.4), (26.4, 35.0)], [1.0, 0.55, 0.36, 0.36]), far, R=0.5, edge=0.3)
    cv.part(limb([(32.6, 22.0), (33.6, 25.6), (32.8, 27.8), (33.0, 31.2)], [1.4, 0.75, 0.4, 0.36]), far, R=0.5, edge=0.3)
    body = union(sd_ellipse(30.2, 20.8, 6.2, 3.3, -10), sd_ellipse(35.2, 19.6, 2.7, 3.0),
                 sd_ellipse(25.6, 21.8, 2.6, 3.4), k=1.6)
    neck = sd_path(bez((25.2, 21.0), (24.4, 19.0), (23.8, 17.6), (23.4, 16.2), n=8), taper(8, 2.2, 1.2))
    fore = limb([(24.4, 22.4), (24.0, 26.4), (23.4, 29.4), (23.0, 35.2)], [1.4, 0.75, 0.42, 0.36])
    hind = limb([(35.6, 20.0), (36.6, 24.4), (35.6, 27.6), (35.9, 33.2)], [2.1, 0.95, 0.42, 0.36])
    torso = union(body, neck, k=1.2)
    torso = union(torso, fore, hind, k=0.8)
    tone = mix(coat, hi, sstep(22.5, 17.5, Y) * 0.55)
    tone = mix(tone, dark * 1.15, sstep(25.6, 22.4, X) * 0.65)
    tone = mix(tone, dark * 1.1, sstep(26.0, 30.0, Y) * 0.8)
    cv.part(torso, tone, R=2.2, spec=0.12, edge=0.38)
    # pale belly and white rump patch
    cv.atop(np.maximum(sd_ellipse(30.4, 23.6, 4.4, 1.0, -10), torso + 0.3), hexc('#e8dccb'), 0.5, soft=0.5)
    cv.part(sd_ellipse(37.0, 16.8, 0.95, 1.6, 22), hexc('#f6f2ea'), R=0.7)
    # head
    ears = union(sd_ellipse(21.2, 14.6, 1.25, 0.5, -28), sd_ellipse(25.2, 14.6, 1.25, 0.5, 28))
    cv.part(ears, coat * 0.95, R=0.4)
    cv.atop(union(sd_ellipse(21.3, 14.6, 0.7, 0.25, -28), sd_ellipse(25.1, 14.6, 0.7, 0.25, 28)), hexc('#d8b8a8'), 0.6)
    head = union(sd_ellipse(23.2, 15.6, 1.55, 1.45), sd_seg((23.1, 16.0), (22.5, 18.6), 1.15, 0.65), k=0.7)
    cv.part(head, mix(coat, hi, 0.3), R=1.1)
    cv.atop(sd_ellipse(23.7, 19.6, 0.95, 0.75), hexc('#f4eee4'), 0.95)
    cv.atop(sd_circle(22.45, 18.75, 0.42), hexc('#120c0a'))
    for ex in (22.4, 24.0):
        cv.atop(sd_circle(ex, 15.9, 0.3), hexc('#120c0a'))
    ant = hexc('#8a7058')
    for side in (-1, 1):
        bx = 23.2 + side * 0.55
        main = bez((bx, 14.6), (bx + side * 0.9, 13.0), (bx + side * 0.4, 11.2), (bx + side * 1.0, 9.8), n=10)
        cv.part(sd_path(main, taper(10, 0.38, 0.2)), ant, R=0.3, edge=0.2)
        cv.part(sd_path([main[3], (main[3][0] + side * 1.3, main[3][1] - 0.9)], [0.26, 0.18]), ant, R=0.3, edge=0.2)
        cv.part(sd_path([main[6], (main[6][0] - side * 0.9, main[6][1] - 1.0)], [0.24, 0.16]), ant, R=0.3, edge=0.2)
    for hx, hy in ((23.0, 35.2), (35.9, 33.2), (26.4, 35.0), (33.0, 31.2)):
        cv.part(sd_ellipse(hx, hy, 0.55, 0.4), hexc('#1c1410'), R=0.3, edge=0.2)
    return finish([(cv, 0.6)], sh)


def icon_fish():
    cv = Canvas()
    spine = bez((25.6, 24.6), (21.6, 15.0), (27.4, 9.6), (39.2, 8.2), n=24)
    prof = [0.9 + 2.1 * math.sin(min(t * 1.25, 1.0) * math.pi * 0.75) * (1 - t) ** 0.35 for t in np.linspace(0, 1, 24)]
    prof[-1] = 0.5
    body = sd_path(spine, prof)
    tail = sd_poly([(38.6, 8.4), (41.6, 3.2), (43.0, 3.6), (41.2, 8.0), (43.4, 11.2), (41.8, 11.4)])
    dorsal = sd_poly([(27.0, 10.0), (29.0, 6.8), (32.0, 7.2), (31.2, 9.0)])
    pect = sd_poly([(24.6, 17.4), (27.8, 18.4), (25.8, 19.6)])
    fin = hexc('#9aa832')
    cv.part(union(dorsal, tail), fin, R=0.7)
    t_along = np.clip((X - 22) / 18 + (14 - Y) / 30, 0, 1)
    # belly (left/lower edge) pale, back (upper/right) olive green
    side_t = sstep(-2.0, 2.0, (X - 23.5) * 0.8 - (Y - 15) * 0.25 + (12 - Y) * 0.3)
    tone = mix(hexc('#f4ec9a'), hexc('#a8b236'), side_t)
    tone = mix(tone, hexc('#6e8a2a'), sstep(0.4, 1.0, t_along) * 0.4)
    n = bulge(body, 2.2)
    cv.put(body, shade(tone, n, body, amb=0.5, dif=0.7, spec=0.7, shin=22, rim=0.35))
    # scale shimmer and lateral line
    lat = sd_path(bez((24.6, 22.0), (23.2, 15.0), (28.0, 11.2), (37.0, 9.0), n=16), 0.18)
    cv.atop(np.maximum(lat, body + 0.6), hexc('#5a7a20'), 0.45)
    cv.atop(np.maximum(sd_path(bez((22.6, 19.0), (22.0, 15.0), (24.0, 12.6), n=10), 0.5), body + 0.4), hexc('#ffffff'), 0.55)
    cv.part(pect, fin, R=0.5)
    cv.atop(sd_circle(25.2, 22.4, 0.62), hexc('#f8f4d8'))
    cv.atop(sd_circle(25.3, 22.5, 0.36), hexc('#141408'))
    cv.atop(sd_path([(26.0, 24.4), (26.6, 23.0)], 0.15), hexc('#4a5014'), 0.6)
    # water drops falling off it
    for x, y, r in ((23.0, 26.6, 0.4), (29.4, 14.2, 0.35), (36.0, 12.2, 0.35)):
        cv.put(sd_ellipse(x, y, r, r * 1.5), hexc('#c8ecf4'), 0.8)
    return finish([(cv, 0.6)], None)


def icon_cattle():
    cv, sh = Canvas(), Shadow()
    sh.ellipse(28.6, 33.8, 9.0, 2.5, 0.7)
    for hx, hy in ((21.4, 32.2), (24.6, 30.8), (33.0, 35.8), (36.0, 33.2)):
        sh.ellipse(hx + 0.6, hy + 0.2, 1.3, 0.5, 1.0)
    coat = hexc('#93603c')
    white = hexc('#f0ebe4')
    dark = hexc('#3a2418')
    far = mix(coat * 0.6, dark * 0.9, sstep(27, 31, Y))
    cv.part(limb([(24.4, 25.4), (24.4, 28.6), (24.6, 30.8)], [1.3, 0.75, 0.62]), far, R=0.7, edge=0.3)
    cv.part(limb([(35.4, 26.8), (36.2, 29.6), (36.0, 33.0)], [1.5, 0.75, 0.62]), far, R=0.7, edge=0.3)
    body = union(sd_ellipse(28.6, 22.6, 7.6, 4.6, 22), sd_ellipse(34.4, 25.2, 3.8, 4.2),
                 sd_ellipse(23.6, 21.0, 3.4, 4.0), k=2.0)
    neck = sd_path(bez((23.6, 21.6), (22.0, 19.6), (20.8, 17.6), (19.6, 16.0), n=8), taper(8, 3.0, 2.0))
    fore = limb([(22.2, 23.6), (21.8, 27.6), (21.5, 31.6), (21.4, 32.2)], [1.9, 0.95, 0.66, 0.7])
    hind = limb([(32.6, 26.0), (33.6, 29.2), (32.8, 32.0), (33.0, 35.6)], [2.4, 1.2, 0.68, 0.7])
    torso = union(body, neck, k=1.2)
    torso = union(torso, fore, hind, k=0.8)
    wob = 0.5 * smooth_noise(191, 0.8)
    patches = union(sd_ellipse(24.6, 19.4, 1.9, 1.3, -20), sd_ellipse(30.4, 21.0, 3.0, 1.3, 22),
                    sd_ellipse(35.6, 24.6, 1.6, 2.2, 10), sd_ellipse(27.0, 25.6, 2.2, 1.4, 22)) + wob
    tone = mix(coat, white, sstep(0.3, -0.3, patches))
    tone = mix(tone, white * 0.96, sstep(0.6, -0.8, sd_ellipse(30.0, 26.4, 5.6, 1.8, 22)) * 0.85)
    tone = mix(tone, mix(coat, dark, 0.4), sstep(29.0, 32.5, Y + (X - 27) * 0.1) * 0.8)
    cv.part(torso, tone, R=3.0, spec=0.12, edge=0.38)
    cv.part(sd_ellipse(29.8, 27.9, 1.4, 0.8), hexc('#e2a8a0'), R=0.6)
    tail = sd_path(bez((37.6, 22.6), (38.4, 26.0), (37.8, 29.0), (37.6, 30.4), n=10), 0.32)
    cv.part(tail, dark, R=0.3)
    cv.part(sd_ellipse(37.6, 31.0, 0.6, 1.0), hexc('#a8522a'), R=0.4)
    # head: broad and boxy, white face, short horns
    cv.part(union(sd_ellipse(17.2, 15.2, 1.3, 0.6, -15), sd_ellipse(21.6, 14.8, 1.3, 0.6, 15)), coat * 0.9, R=0.4)
    for pts in ([(18.6, 13.8), (17.6, 13.0), (17.0, 12.0)], [(20.4, 13.6), (21.4, 13.0), (21.8, 12.0)]):
        cv.part(sd_path(pts, [0.5, 0.36, 0.2]), hexc('#ece2cc'), R=0.3, edge=0.3)
    head = union(sd_ellipse(19.4, 15.2, 2.0, 1.8), sd_seg((19.2, 15.8), (17.6, 18.0), 1.6, 1.3), k=0.8)
    cv.part(head, mix(coat, white, 0.1), R=1.4)
    cv.atop(sd_seg((19.6, 13.8), (18.0, 17.4), 0.95, 1.0), white, 0.95)
    cv.atop(sd_ellipse(17.3, 18.4, 1.45, 1.0, 20), hexc('#e0aaa4'), 0.95)
    cv.atop(sd_circle(16.8, 18.6, 0.24), hexc('#5a2a28'), 0.9)
    cv.atop(sd_circle(17.9, 18.8, 0.24), hexc('#5a2a28'), 0.9)
    cv.atop(sd_circle(18.4, 15.2, 0.4), hexc('#140c08'))
    cv.atop(sd_circle(20.6, 15.0, 0.36), hexc('#140c08'))
    for hx, hy in ((21.4, 32.2), (33.0, 35.6), (24.6, 30.8), (36.0, 33.0)):
        cv.part(sd_ellipse(hx, hy, 0.8, 0.5), hexc('#1c120c'), R=0.3, edge=0.2)
    return finish([(cv, 0.6)], sh)


def icon_wheat():
    cv, sh = Canvas(), Shadow()
    sh.ellipse(27.6, 35.4, 7.8, 2.0, 0.8)
    straw = hexc('#dcae62')
    tie_y = 20.8
    rng = np.random.default_rng(200)
    # lower stalks: bundled at the tie, flaring out toward the ground
    for k in range(19):
        f = k / 18
        bx = 19.4 + f * 15.0 + rng.uniform(-0.3, 0.3)
        top_x = 24.0 + f * 4.4
        pts = bez((top_x, tie_y), (top_x + (bx - top_x) * 0.15, tie_y + 7), (bx - (f - 0.5) * 1.0, 31.0), (bx, 35.6), n=12)
        lit = 1 - abs(f - 0.3) * 1.1
        c = np.clip(straw * (0.78 + 0.32 * lit) * (0.94 + 0.08 * rng.random()), 0, 1)
        cv.part(sd_path(pts, 0.72), c, R=0.6, edge=0.4, rim=0.1)
    cv.atop(np.maximum(np.abs(Y - 35.5) - 0.45, sd_ellipse(27.0, 35.4, 8.2, 1.4)), hexc('#8a6436'), 0.5)
    # upper stalks and ears radiating from the tie
    ears = []
    for k in range(15):
        a = math.radians(-165 + k * (150 / 14)) + rng.uniform(-0.05, 0.05)
        L = 5.4 + 2.4 * math.sin(k / 14 * math.pi) + rng.uniform(-0.5, 0.5)
        ears.append((a, L))
    ears.sort(key=lambda e: abs(e[0] + math.pi / 2), reverse=True)
    for a, L in ears:
        ca, sa = math.cos(a), math.sin(a)
        ox = 26.2 + ca * 1.0
        cv.part(sd_seg((ox, tie_y), (26.2 + ca * L * 0.5, tie_y + sa * L * 0.5), 0.4), straw * 0.95, R=0.4)
        ex, ey = 26.2 + ca * L * 0.45, tie_y + sa * L * 0.45
        x1, y1 = 26.2 + ca * (L + 2.0), tie_y + sa * (L + 2.0)
        ear = sd_seg((ex, ey), (x1, y1), 1.25, 0.55)
        n = bulge(ear, 1.0)
        col = shade(hexc('#f2c66c'), n, ear, amb=0.5, dif=0.72, rim=0.25, edge=0.45, spec=0.15)
        cv.put(ear, col)
        # kernels in two rows
        for j in range(4):
            t = 0.12 + j * 0.24
            gx, gy = ex + (x1 - ex) * t, ey + (y1 - ey) * t
            for side in (-1, 1):
                kx, ky = gx - sa * 0.55 * side, gy + ca * 0.55 * side
                cv.atop(np.maximum(np.abs(sd_ellipse(kx, ky, 0.55, 0.42, math.degrees(a))) - 0.07, ear + 0.15), hexc('#a8742e'), 0.5)
        cv.part(sd_seg((x1, y1), (x1 + ca * 1.6, y1 + sa * 1.6), 0.1), hexc('#ecc880'), R=0.1, edge=0)
    # the twisted straw binding
    band = sd_seg((22.8, tie_y + 0.3), (29.6, tie_y + 0.3), 1.15)
    twist = 0.5 + 0.5 * np.sin((X - Y * 0.8) * 2.6)
    bc = mix(hexc('#a8462a'), hexc('#6a2a18'), twist * 0.7)
    cv.part(band, bc, R=0.9, spec=0.2)
    return finish([(cv, 0.55)], sh)


def icon_gold():
    cv, sh = Canvas(), Shadow()
    gold = hexc('#eab42a')
    nuggets = [
        (union(sd_ellipse(22.0, 22.0, 4.0, 3.0, 10), sd_ellipse(26.0, 18.8, 4.6, 3.6, -10),
               sd_ellipse(29.6, 21.4, 2.6, 2.2), k=1.4), 25.0, 20.4, 2.8, 211),
        (union(sd_ellipse(13.4, 25.2, 1.6, 1.1), sd_ellipse(14.8, 25.0, 1.1, 0.9), k=0.5), 14.0, 25.2, 1.0, 212),
        (sd_ellipse(28.4, 22.6, 1.5, 1.0), 28.4, 22.6, 0.9, 213),
        (union(sd_ellipse(34.4, 22.6, 1.7, 1.2, 10), sd_ellipse(36.0, 22.2, 1.2, 1.0), k=0.5), 35.0, 22.6, 1.1, 214),
        (union(sd_ellipse(30.6, 27.2, 1.9, 1.4, -15), sd_ellipse(32.6, 26.6, 1.4, 1.1), k=0.6), 31.2, 27.0, 1.2, 215),
        (sd_ellipse(17.6, 28.6, 1.0, 0.8), 17.6, 28.6, 0.7, 216),
    ]
    for d, cx, cy, R, s in nuggets:
        sh.shape(d - 0.1)
    sh.A = ndi.shift(sh.A, (0.9 * SS, 1.2 * SS), order=1)
    for d, cx, cy, R, s in nuggets:
        lump = smooth_noise(s, 0.7)
        d = d + 0.4 * lump * min(R, 1.6)
        n = bulge(d, R)
        n = tilt(n, 0.45 * smooth_noise(s + 7, 0.6), 0.45 * smooth_noise(s + 9, 0.6))
        col = shade(gold, n, d, amb=0.4, dif=0.85, spec=1.1, shin=16, rim=0.35,
                    spec_col=(1.0, 0.97, 0.8))
        pit = sstep(-0.1, -0.45, lump)
        col = mix(col, col * hexc('#a0581c'), pit * 0.6)
        cv.put(d, np.clip(col, 0, 1))
    return finish([(cv, 0.55)], sh)


def icon_bananas():
    cv, sh = Canvas(), Shadow()
    sh.ellipse(27.4, 32.4, 9.4, 2.0, 0.8)
    yel = hexc('#ecc43a')
    stalk_pts = bez((19.4, 8.8), (19.8, 13.0), (19.4, 18.0), (19.6, 22.0), n=12)
    stalk = sd_path(stalk_pts, taper(12, 1.1, 0.9))
    cv.part(stalk, hexc('#6a4a24'), R=0.8)
    fingers = []
    back = [(11.0, (31.0, 22.0)), (12.0, (34.6, 25.4)), (14.0, (36.6, 28.6)), (17.0, (35.4, 31.4))]
    front = [(12.0, (20.4, 31.8)), (12.4, (23.8, 32.8)), (13.2, (27.6, 33.0)), (14.6, (31.2, 32.2)),
             (17.4, (33.8, 30.2))]
    for row, lst, shade_k in ((0, back, 0.8), (1, front, 1.0)):
        for sy, (ex, ey) in lst:
            sx = 19.6 + 0.3 * row
            mx = sx + (ex - sx) * 0.25 + 2.8
            my = sy + (ey - sy) * 0.4 - 1.0
            pts = bez((sx, sy), (mx, my), (ex, ey), n=14)
            fingers.append((row, ex, pts, shade_k))
    fingers.sort(key=lambda e: (e[0], -e[1]))
    for row, ex, pts, k in fingers:
        prof = [0.8 + 0.95 * math.sin(t * math.pi) ** 0.5 for t in np.linspace(0, 1, 14)]
        prof[-1] = 0.5
        d = sd_path(pts, prof)
        t = np.clip(np.hypot(X - pts[0][0], Y - pts[0][1]) / (np.hypot(*(pts[-1] - pts[0])) + 1e-6), 0, 1)
        c = mix(hexc('#7a8a2a'), yel, sstep(0.05, 0.35, t))
        c = mix(c, hexc('#5a3a1a'), sstep(0.93, 1.0, t))
        cv.part(d, c * k, R=1.2, amb=0.45, dif=0.72, spec=0.35, shin=14, rim=0.25, edge=0.5)
        cv.atop(np.maximum(sd_path(pts[3:-2], 0.14), d + 0.6), hexc('#c08a24'), 0.45)
    cv.part(sd_ellipse(19.5, 8.8, 1.0, 0.65), hexc('#4a3218'), R=0.4)
    return finish([(cv, 0.55)], sh)


def _palm(cv, base, top, crown_r, seed, lean=0.0):
    bx, by = base
    tx, ty = top
    trunk = bez((bx, by), (bx + lean, (by + ty) / 2), (tx, ty), n=12)
    d = sd_path(trunk, taper(12, 0.9, 0.55))
    rings = 0.85 + 0.15 * np.sin(Y * 4.0)
    cv.part(d, hexc('#7a6448') * rings[..., None], R=0.7)
    rng = np.random.default_rng(seed)
    angs = [200, 235, 270, 305, 340, 15, 160, 120, 60]
    for i, a0 in enumerate(angs):
        a = math.radians(a0 + rng.uniform(-8, 8))
        L = crown_r * rng.uniform(0.85, 1.1)
        ex, ey = tx + math.cos(a) * L, ty + math.sin(a) * L * 0.7 + L * 0.35
        mx, my = tx + math.cos(a) * L * 0.5, ty + math.sin(a) * L * 0.4 - L * 0.12
        pts = bez((tx, ty), (mx, my), (ex, ey), n=10)
        prof = [0.35 + 0.9 * math.sin(t * math.pi) ** 0.7 * (crown_r / 6) for t in np.linspace(0, 1, 10)]
        prof[-1] = 0.2
        fd = sd_path(pts, prof)
        c = mix(hexc('#3c7a2a'), hexc('#6aa83a'), 0.5 + 0.5 * math.sin(a0))
        cv.part(fd, c * (0.85 if a0 in (160, 120, 60) else 1.0), R=0.6, rim=0.25, edge=0.45)
        cv.atop(np.maximum(sd_path(pts[1:-1], 0.12), fd + 0.3), hexc('#24501c'), 0.6)
    cv.part(sd_circle(tx, ty + 0.4, 0.8), hexc('#5a4a2a'), R=0.6)


def icon_oasis():
    cv, sh = Canvas(), Shadow()
    sand = union(sd_ellipse(31.0, 31.2, 17.6, 6.8), sd_ellipse(12.0, 27.6, 9.0, 2.6), k=3.0)
    sand = sand + 0.5 * smooth_noise(230, 1.2)
    n = bulge(sand, 1.2)
    sc = mix(hexc('#e2c88a'), hexc('#f2e2aa'), 0.5 + 0.5 * smooth_noise(231, 2.0))
    cv.put(sand, shade(sc, n, sand, amb=0.6, dif=0.5, spec=0, rim=0.15, edge=0.25))
    water = sd_ellipse(32.0, 31.4, 13.8, 4.8) + 0.4 * smooth_noise(232, 1.5)
    depth = np.clip(-water / 3.0, 0, 1)
    wc = mix(hexc('#7fb8d8'), hexc('#3a86c0'), depth)
    ripple = 0.5 + 0.5 * np.sin(X * 1.6 + Y * 3.0 + 2 * smooth_noise(233, 1.5))
    wc = mix(wc, hexc('#bfe6f6'), sstep(0.8, 1.0, ripple) * 0.6)
    cv.put(water, wc)
    cv.atop(np.maximum(np.abs(water + 0.2) - 0.3, -water - 0.6), hexc('#2a5a7a'), 0.35)
    cv.atop(np.maximum(water + 0.0, -(water + 1.0)), hexc('#d8f0f8'), 0.25)
    # bushes
    for cx, cy, rx, ry, s in ((14.4, 27.0, 3.6, 1.6, 234), (43.4, 32.4, 3.4, 3.6, 235), (41.4, 36.0, 3.0, 1.6, 236)):
        d = sd_ellipse(cx, cy, rx, ry) + 0.6 * smooth_noise(s, 0.6)
        sh.shape(d, 0.8)
        cv.part(d, mix(hexc('#2a8a2a'), hexc('#4cb43a'), 0.5 + 0.5 * smooth_noise(s + 1, 0.5)), R=1.0, rim=0.3)
    # palm shadows on the sand
    sh.ellipse(19.0, 27.0, 4.0, 1.0, 0.7, ang=-10)
    sh.ellipse(45.0, 35.8, 3.0, 0.9, 0.7)
    _palm(cv, (8.8, 25.6), (8.6, 18.6), 3.6, 237, lean=0.6)
    _palm(cv, (16.4, 27.0), (17.6, 11.4), 5.6, 238, lean=-1.2)
    _palm(cv, (43.0, 36.4), (44.0, 20.6), 5.0, 239, lean=-1.4)
    return finish([(cv, 0.5)], sh, shadow_alpha=0.35)


def icon_sugar():
    """Sugar cane, like the original: a tall, dense tuft of long, narrow, upright
    grass-like leaves on a few short canes, over a flat spill of white sugar.
    No jointed stalks or drooping leaves showing, so it can't pass for bamboo."""
    cv, sh = Canvas(), Shadow()
    sh.ellipse(28.6, 39.6, 10.0, 2.2, 0.7)
    sh.ellipse(26.0, 36.8, 5.0, 1.6, 0.8)
    rng = np.random.default_rng(240)
    dark, light = hexc('#2c5228'), hexc('#6c9a48')

    def blade(x0, y0, ang, length, bend, k, width):
        a = math.radians(ang)
        ux, uy = math.cos(a), -math.sin(a)
        side = 1.0 if ux >= 0 else -1.0
        mx, my = x0 + ux * length * 0.55, y0 + uy * length * 0.55
        ex = x0 + ux * length + side * bend
        ey = y0 + uy * length + bend * 0.6
        pts = bez((x0, y0), (mx, my), (ex, ey), n=18)
        prof = [0.14 + width * math.sin(min(t * 1.25, 1.0) * math.pi * 0.5) * (1 - t) ** 0.7
                for t in np.linspace(0, 1, 18)]
        prof[-1] = 0.05
        d = sd_path(pts, prof)
        t = np.clip((y0 - Y) / max(length, 1), 0, 1)
        c = mix(dark, light, np.clip(rng.uniform(0.25, 0.75) + 0.3 * t, 0, 1)) * k
        n = bulge(d, 0.35)
        n = tilt(n, -0.25 * side, 0.0)
        cv.put(d, shade(c, n, d, amb=0.55, dif=0.6, spec=0.06, rim=0.18, edge=0.35, ew=0.3))

    # A few canes at the foot of the clump.
    for i, (bx, tx, ty) in enumerate(((23.6, 23.0, 27.0), (26.0, 25.8, 25.0), (28.4, 28.8, 26.0), (30.6, 31.4, 28.0))):
        d = sd_seg((bx, 37.6), (tx, ty), 0.85, 0.7)
        c = mix(hexc('#6e7040'), hexc('#4e5a32'), 0.5 * (i % 2))
        cv.part(d, c, R=0.8, spec=0.08, rim=0.15)
        for yy in (34.6, 31.4):
            cv.atop(np.maximum(np.abs(Y - yy) - 0.2, d + 0.15), hexc('#4a4428'), 0.45)
    # Leaves: a dense, upright tuft, back (darker) to front; the core stays dark.
    lr = np.random.default_rng(241)
    for layer, (k, nleaf) in enumerate(((0.62, 12), (0.82, 10), (1.0, 8))):
        for j in range(nleaf):
            f = (j + lr.uniform(0.1, 0.9)) / nleaf
            ang = 118 - 56 * f + lr.uniform(-4, 4)
            x0 = 23.8 + 5.6 * f + lr.uniform(-0.5, 0.5)
            y0 = 25.0 + layer * 2.2 + lr.uniform(0, 2.0)
            up = math.sin(math.radians(ang))
            L = (8.0 + 9.0 * up ** 3) * (1.0 - 0.12 * layer) + lr.uniform(-1.0, 1.0)
            blade(x0, y0, ang, L, 0.5 + 2.6 * (1 - up), k, 0.78)
    # The spill of sugar: a low, flat, slightly lumpy white heap in front.
    heap(cv, 29.0, 39.6, 9.4, 2.2, 1.9, hexc('#ebe9e2'), 241, amb=0.66, spec=0.12,
         flecks=((hexc('#fbfbf8'), 10), (hexc('#cfcbc0'), 6)), wob=0.4)
    return finish([(cv, 0.5)], sh)


def icon_tobacco():
    cv, sh = Canvas(), Shadow()
    sh.ellipse(25.0, 36.8, 9.0, 1.8, 0.55)
    leafc = hexc('#c88e36')
    leaves = [  # (top, ctrl, tip, width, colour shift) back to front
        ((23.4, 15.0), (34.0, 15.0), (34.4, 31.4), 4.0, 0.2),
        ((22.2, 15.0), (13.4, 16.6), (14.6, 34.6), 4.0, -0.1),
        ((22.8, 15.4), (30.0, 18.4), (27.6, 35.6), 3.8, 0.05),
        ((22.2, 15.4), (17.6, 22.0), (20.6, 36.6), 3.6, 0.15),
    ]
    for i, (a, m, e, w, sft) in enumerate(leaves):
        pts = bez(a, m, e, n=16)
        prof = [0.5 + w * math.sin(min(t * 1.1, 1) * math.pi) ** 0.75 for t in np.linspace(0, 1, 16)]
        prof[-1] = 0.3
        d = sd_path(pts, prof) + 0.12 * smooth_noise(280 + i, 0.5)
        wr = 0.5 + 0.5 * smooth_noise(250 + i, 0.8)
        c = mix(leafc, hexc('#e8b850'), wr * 0.55 + sft)
        c = mix(c, hexc('#6a4220'), sstep(0.55, 1.0, np.clip((Y - a[1]) / (e[1] - a[1]), 0, 1)) * 0.45)
        n = bulge(d, 1.8)
        n = tilt(n, 0.2 * smooth_noise(260 + i, 0.7), 0.2 * smooth_noise(270 + i, 0.7))
        cv.put(d, shade(c, n, d, amb=0.5, dif=0.7, spec=0.08, rim=0.2, edge=0.5))
        cv.atop(np.maximum(sd_path(pts[1:-1], taper(14, 0.35, 0.15)), d + 0.4), hexc('#6a4018'), 0.6)
        for j in range(3, 14, 3):
            p, q = pts[j], pts[j + 1]
            tx, ty = q - p
            ln = math.hypot(tx, ty) + 1e-6
            nx_, ny_ = -ty / ln, tx / ln
            for sgn in (-1, 1):
                v = sd_seg(tuple(p), (p[0] + (nx_ * sgn + tx / ln) * 2.4, p[1] + (ny_ * sgn + ty / ln) * 2.4), 0.13)
                cv.atop(np.maximum(v, d + 0.5), hexc('#8a5a24'), 0.4)
    tie = union(sd_ellipse(22.6, 15.6, 2.1, 1.1), sd_seg((22.6, 15.0), (22.4, 12.6), 0.6))
    cv.part(tie, hexc('#5a3a1e'), R=0.7)
    return finish([(cv, 0.6)], sh)


def heap(cv, cx, cy, rx, ry, h, base, seed, facets=0, fstr=0.35, flecks=(), wob=0.35, amb=0.5, spec=0.1, **kw):
    """A heap of powder seen from the isometric view: an elliptic footprint
    with a rounded cone rising h above it."""
    layers = []
    for i in range(7):
        f = i / 6
        layers.append(sd_ellipse(cx - rx * 0.06 * f, cy - h * f * 0.95, rx * (1 - 0.86 * f), ry * (1 - 0.4 * f)))
    d = union(*layers, k=h * 0.3 + 0.3)
    d = d + wob * smooth_noise(seed, 1.2)
    px, py = cx - rx * 0.06, cy - h * 0.85
    # cone height: distance from the apex, measured in the footprint's proportions
    rr = np.hypot((X - px) / rx, (Y - py) / (ry + h))
    hgt = h * 1.2 * np.clip(1 - rr, 0, 1)
    gy, gx = np.gradient(hgt, 1.0 / SS)
    nb = bulge(d, 1.0)
    nx, ny = -gx + nb[0] * 0.4, -gy * 0.5 + nb[1] * 0.4
    if facets:
        fn, ridge = facet_normal(d, cx, cy - h * 0.5, seed + 1, k=facets, strength=fstr, R=1.0)
        nx, ny = nx + fn[0] * 0.8, ny + fn[1] * 0.8
    n = _norm(nx, ny, np.ones_like(X))
    col = shade(base, n, d, amb=amb, dif=0.7, spec=spec, rim=0.22, edge=0.3, **kw)
    col = col * (1 - 0.18 * sstep(cy - ry * 0.3, cy + ry, Y))[..., None]
    grain = smooth_noise(seed + 5, 0.25)
    col = col * (1 + 0.06 * grain)[..., None]
    cv.put(d, np.clip(col, 0, 1))
    rng = np.random.default_rng(seed)
    for c, cnt in flecks:
        for _ in range(cnt):
            a = rng.uniform(0, 2 * np.pi)
            r = math.sqrt(rng.uniform(0, 0.7))
            fy0 = cy - h * 0.4
            fx, fy = cx + math.cos(a) * r * rx * 0.7, fy0 + math.sin(a) * r * (ry + h * 0.4)
            cv.atop(np.maximum(sd_circle(fx, fy, rng.uniform(0.32, 0.5)), d + 0.4), c, 0.75)
    return d


def ribbon_coords(path):
    """For each pixel: (fraction along the path, signed offset across it) of the nearest sample."""
    p = np.asarray(path)
    tang = np.gradient(p, axis=0)
    tang /= np.linalg.norm(tang, axis=1, keepdims=True) + 1e-9
    dd = np.stack([np.hypot(X - q[0], Y - q[1]) for q in p])
    i = np.argmin(dd, 0)
    ox, oy = X - p[i, 0], Y - p[i, 1]
    across = ox * -tang[i, 1] + oy * tang[i, 0]
    return i / (len(p) - 1), across, tang[i]


def crystal(cv, base, tip, w, col, seed):
    """A hexagonal crystal prism seen from the side: lit left face, mid front, dark right face."""
    bx, by = base
    tx, ty = tip
    ax, ay = tx - bx, ty - by
    ln = math.hypot(ax, ay)
    ux, uy = ax / ln, ay / ln          # along the crystal
    px, py = -uy, ux                    # across (pointing left when the crystal points up)
    neck = 0.72                          # where the point starts
    nxp, nyp = bx + ax * neck, by + ay * neck
    L = [(bx + px * w, by + py * w), (nxp + px * w, nyp + py * w), (tx, ty), (nxp - px * w, nyp - py * w), (bx - px * w, by - py * w)]
    d = sd_poly(L)
    # three faces split by two lines along the axis
    across = (X - bx) * px + (Y - by) * py
    face = np.where(across > w * 0.33, 0, np.where(across < -w * 0.33, 2, 1))
    along = (X - bx) * ux + (Y - by) * uy
    tipface = along > ln * neck - np.abs(across) * 0.0
    br = np.array([1.15, 0.85, 0.55])[face] * np.where(tipface, 1.12, 1.0)
    c = full(col) * br[..., None]
    g = np.clip((along / ln), 0, 1)
    c = mix(c, c * 0.75, (1 - g) * 0.5)
    n = (np.zeros_like(X), np.zeros_like(X), np.ones_like(X))
    out = shade(c, n, d, amb=0.95, dif=0.0, spec=0.0, rim=0.0, edge=0.4, ew=0.35)
    # glints on the lit face
    glint = np.maximum(np.abs(across - w * 0.62) - 0.12, np.abs(along - ln * 0.5) - ln * 0.28)
    out = np.where((glint < 0)[..., None], np.clip(out * 0.5 + 0.55, 0, 1), out)
    cv.put(d, out)
    for edge_off in (w * 0.33, -w * 0.33):
        line = np.maximum(np.abs(across - edge_off) - 0.1, d + 0.3)
        cv.atop(np.maximum(line, along - ln * neck), hexc('#ffffff') if edge_off > 0 else hexc('#40405a'), 0.35)


ICONS = [icon_horse, icon_iron, icon_saltpeter, icon_coal, icon_oil, icon_rubber,
         icon_aluminum, icon_uranium, icon_wine, icon_furs, icon_dyes, icon_incense,
         icon_spices, icon_ivory, icon_silks, icon_gems, icon_whales, icon_game,
         icon_fish, icon_cattle, icon_wheat, icon_gold, icon_bananas, icon_oasis,
         icon_sugar, icon_tobacco]


def render(only=None):
    o = civ3art.SCALE * CELL
    sheet = np.zeros((6 * o, 6 * o, 4))
    for i, fn in enumerate(ICONS):
        if only is not None and i not in only:
            continue
        r, c = divmod(i, 6)
        sheet[r * o:(r + 1) * o, c * o:(c + 1) * o] = fn()
    P, A = sheet[..., :3], sheet[..., 3]
    rgb = np.where(A[..., None] > 1e-4, P / np.maximum(A, 1e-4)[..., None], 0)
    orig = civ3art.load_original(REL, shadows=True)
    for i in range(len(ICONS)):
        if only is not None and i not in only:
            continue
        r, c = divmod(i, 6)
        sl = (slice(r * o, (r + 1) * o), slice(c * o, (c + 1) * o))
        oc = orig[r * CELL:(r + 1) * CELL, c * CELL:(c + 1) * CELL]
        rgb[sl] = mute(rgb[sl], A[sl], MUTE * mean_saturation(oc[..., :3] / 255.0, oc[..., 3] / 255.0))
    out = np.concatenate([np.clip(rgb, 0, 1), A[..., None]], -1)
    return np.round(out * 255).astype(np.uint8)


# Each icon's mean saturation is kept at most this fraction of its original's.
MUTE = 0.88


def mean_saturation(rgb, a):
    mx, mn = rgb.max(-1), rgb.min(-1)
    m = (a > 0.78) & (mx > 0.12)
    if not m.any():
        return 0.0
    return float(((mx - mn) / np.maximum(mx, 1e-6))[m].mean())


def mute(rgb, a, target):
    """Pulls the colors toward grey (keeping brightness) until the mean
    saturation is no more than `target`."""
    for _ in range(4):
        s = mean_saturation(rgb, a)
        if s <= target + 1e-3:
            break
        lum = (rgb @ np.array([0.3, 0.55, 0.15]))[..., None]
        rgb = np.clip(lum + (rgb - lum) * (target / s), 0, 1)
    return rgb


def build():
    rgba = render()
    return civ3art.save_modern(REL, rgba)


def previews(rgba):
    from PIL import Image
    orig = civ3art.load_original(REL)
    civ3art.save_preview('resources', civ3art.upscale_nearest(orig), rgba)
    im = Image.fromarray(rgba)
    small = im.resize((300, 300), Image.LANCZOS)
    tiles = []
    for bg in ((111, 154, 53), (217, 192, 138), (43, 138, 168)):
        b = Image.new('RGBA', (300, 300), bg + (255,))
        b.alpha_composite(small)
        tiles.append(b)
    return civ3art.save_preview('resources_1x', *tiles)


if __name__ == '__main__':
    path = build()
    from PIL import Image
    previews(np.asarray(Image.open(path)))
    print(path)
