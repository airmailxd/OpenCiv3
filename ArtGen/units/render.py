"""A small numpy software renderer for the unit sprites.

Orthographic camera matching Civ3's unit view: looking north, tilted down 30
degrees (the map's 2:1 isometric diamond), world x = east, y = north, z = up.
A unit model faces +y in its own space; `facing_matrix` turns it to one of
the flic's 8 directions.

render_cell() rasterizes a mesh with supersampling into a G-buffer (fully
vectorized: triangles are binned by screen size and their fragments resolved
by sorting), shades it (NW key light, sky ambient, fill, rim, specular,
screen-space ambient occlusion), draws a thin darker contour, casts a soft
ground shadow down-right, and returns the base and tint layers at the sheet's
resolution (2x the flic's).
"""
import math

import numpy as np
from scipy import ndimage

ELEVATION = 30.0          # camera tilt (degrees)
SIN_E, COS_E = math.sin(math.radians(ELEVATION)), math.cos(math.radians(ELEVATION))
# Toward the camera (south, up).
VIEW = np.array([0.0, -COS_E, SIN_E], np.float32)

# Key light from the northwest, high: lit faces face up-left on screen and the
# ground shadow falls down-right.
_KEY_EL = math.radians(52)
KEY = np.array([-math.cos(_KEY_EL) * 0.7071, math.cos(_KEY_EL) * 0.7071, math.sin(_KEY_EL)], np.float32)
# Soft fill from the front right (the camera side), and a rim light from behind.
FILL = np.array([0.55, -0.65, 0.35], np.float32); FILL /= np.linalg.norm(FILL)
RIM = np.array([0.35, 0.85, 0.25], np.float32); RIM /= np.linalg.norm(RIM)
# The ground shadow is cast from the same direction as the key light, but a
# little higher, so units' shadows stay compact on the map.
_SH_EL = math.radians(64)
SHADOW_DIR = np.array([-math.cos(_SH_EL) * 0.7071, math.cos(_SH_EL) * 0.7071, math.sin(_SH_EL)], np.float32)

# Flic rows: SW, S, SE, E, NE, N, NW, W, as unit vectors on the ground.
DIR_VECTORS = [(-1, -1), (0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0)]


def facing_deg(row):
    """Yaw (degrees, + = counter-clockwise from above) turning a model that
    faces +y (north) to face the direction of flic row `row`."""
    dx, dy = DIR_VECTORS[row]
    # model +y -> (dx, dy): angle of (dx, dy) minus 90 degrees
    return math.degrees(math.atan2(dy, dx)) - 90.0


def facing_matrix(row):
    a = math.radians(facing_deg(row))
    c, s = math.cos(a), math.sin(a)
    T = np.eye(4, dtype=np.float32)
    T[0:2, 0:2] = [[c, -s], [s, c]]
    return T


class Camera:
    """Projects world points to sheet pixels. `ppm` is pixels per meter at the
    sheet resolution (2x), `anchor` the pixel of the world origin."""

    def __init__(self, ppm, anchor):
        self.ppm = ppm
        self.anchor = anchor

    def project(self, P):
        x, y, z = P[..., 0], P[..., 1], P[..., 2]
        sx = self.anchor[0] + x * self.ppm
        sy = self.anchor[1] - (y * SIN_E + z * COS_E) * self.ppm
        depth = -y * COS_E + z * SIN_E   # larger = closer to the camera
        return np.stack([sx, sy, depth], -1)


# ------------------------------------------------------------ rasterizer

_CLASSES = [2, 4, 8, 12, 16, 24, 32, 48, 64, 96, 128, 192, 256, 384, 512, 768, 1024]


def rasterize(S, W, H):
    """Rasterizes screen-space triangles S (T,3,3: x, y, depth) into a WxH
    grid (pixel centers at +0.5). Returns (tri index map HxW, -1 = empty,
    barycentric weights HxWx3, depth HxW)."""
    T = len(S)
    tri = np.full(H * W, -1, np.int64)
    bary = np.zeros((H * W, 3), np.float32)
    zbuf = np.full(H * W, -np.inf, np.float32)
    if T == 0:
        return tri.reshape(H, W), bary.reshape(H, W, 3), zbuf.reshape(H, W)
    x, y, z = S[..., 0], S[..., 1], S[..., 2]
    x0 = np.clip(np.floor(x.min(1) - 0.5), 0, W).astype(np.int64)
    x1 = np.clip(np.ceil(x.max(1) - 0.5), -1, W - 1).astype(np.int64)
    y0 = np.clip(np.floor(y.min(1) - 0.5), 0, H).astype(np.int64)
    y1 = np.clip(np.ceil(y.max(1) - 0.5), -1, H - 1).astype(np.int64)
    bw, bh = x1 - x0 + 1, y1 - y0 + 1
    area = (x[:, 1] - x[:, 0]) * (y[:, 2] - y[:, 0]) - (x[:, 2] - x[:, 0]) * (y[:, 1] - y[:, 0])
    ok = (bw > 0) & (bh > 0) & (np.abs(area) > 1e-9)
    size = np.maximum(bw, bh)
    pix_all, dep_all, tri_all, b_all = [], [], [], []
    prev = 0
    for c in _CLASSES:
        sel = np.nonzero(ok & (size > prev) & (size <= c))[0]
        prev = c
        if len(sel) == 0:
            continue
        # Bound the memory of one batch.
        step = max(1, int(4_000_000 // (c * c)))
        for s0 in range(0, len(sel), step):
            ids = sel[s0:s0 + step]
            gx = x0[ids, None, None] + np.arange(c)[None, None, :]
            gy = y0[ids, None, None] + np.arange(c)[None, :, None]
            px = gx + 0.5
            py = gy + 0.5
            X, Y = x[ids], y[ids]
            inv = 1.0 / area[ids]
            # barycentrics via edge functions
            e0 = ((X[:, 2, None, None] - X[:, 1, None, None]) * (py - Y[:, 1, None, None])
                  - (Y[:, 2, None, None] - Y[:, 1, None, None]) * (px - X[:, 1, None, None])) * inv[:, None, None]
            e1 = ((X[:, 0, None, None] - X[:, 2, None, None]) * (py - Y[:, 2, None, None])
                  - (Y[:, 0, None, None] - Y[:, 2, None, None]) * (px - X[:, 2, None, None])) * inv[:, None, None]
            e2 = 1.0 - e0 - e1
            eps = -1e-4
            inside = (e0 >= eps) & (e1 >= eps) & (e2 >= eps) & (gx <= x1[ids, None, None]) & (gy <= y1[ids, None, None])
            ti, yy, xx = np.nonzero(inside)
            if len(ti) == 0:
                continue
            b0, b1, b2 = e0[ti, yy, xx], e1[ti, yy, xx], e2[ti, yy, xx]
            Z = z[ids]
            d = b0 * Z[ti, 0] + b1 * Z[ti, 1] + b2 * Z[ti, 2]
            pix_all.append(gy[ti, yy, 0] * W + gx[ti, 0, xx])
            dep_all.append(d.astype(np.float32))
            tri_all.append(ids[ti])
            b_all.append(np.stack([b0, b1, b2], -1).astype(np.float32))
    if not pix_all:
        return tri.reshape(H, W), bary.reshape(H, W, 3), zbuf.reshape(H, W)
    pix = np.concatenate(pix_all)
    dep = np.concatenate(dep_all)
    tr = np.concatenate(tri_all)
    bb = np.concatenate(b_all)
    order = np.lexsort((-dep, pix))
    pix_s = pix[order]
    first = np.ones(len(pix_s), bool)
    first[1:] = pix_s[1:] != pix_s[:-1]
    win = order[first]
    tri[pix[win]] = tr[win]
    bary[pix[win]] = bb[win]
    zbuf[pix[win]] = dep[win]
    return tri.reshape(H, W), bary.reshape(H, W, 3), zbuf.reshape(H, W)


def coverage(S, W, H):
    """Just the covered pixels of screen-space triangles (for shadows)."""
    tri, _, _ = rasterize(S, W, H)
    return tri >= 0


# ------------------------------------------------------------ image helpers

def _blur(a, radius):
    if radius <= 0:
        return a
    return ndimage.gaussian_filter(a.astype(np.float32), radius, mode='nearest')


def _downsample(a, f):
    H, W = a.shape[:2]
    return a.reshape(H // f, f, W // f, f, *a.shape[2:]).mean(axis=(1, 3))


def _maxfilter(a, r):
    yy, xx = np.mgrid[-r:r + 1, -r:r + 1]
    return ndimage.grey_dilation(a, footprint=(xx * xx + yy * yy) <= r * r + r, mode='constant')


def _to_srgb(lin):
    return np.clip(lin, 0, 1) ** (1 / 2.2)


# ------------------------------------------------------------ the render

class Style:
    """Shading constants shared by every unit (one look for the whole set)."""
    ss = 3                  # supersampling factor
    ambient = 0.30
    sky = 0.16              # extra ambient on up-facing surfaces
    key = 0.95
    fill = 0.22
    rim = 0.38
    wrap = 0.25             # soft wrap-around diffuse
    ssao = 0.45
    ground_ao = 0.22        # darkening near the ground
    contour_alpha = 0.5     # outer contour (fraction of a darkened local color)
    contour_dark = 0.35
    inner_line = 0.30       # darkening along depth breaks inside the sprite
    inner_depth = 0.10      # meters of depth difference that make a line
    shadow_alpha = 0.40
    shadow_blur = 1.6       # at the sheet resolution
    saturation = 1.08


def _shadow_verts(V):
    Vs = V.copy()
    h = np.maximum(Vs[..., 2], 0)
    Vs[..., 0] -= SHADOW_DIR[0] / SHADOW_DIR[2] * h
    Vs[..., 1] -= SHADOW_DIR[1] / SHADOW_DIR[2] * h
    Vs[..., 2] = 0
    return Vs


def render_cell(mesh, mats, cam, W, H, style=Style, shadow=True):
    """Renders a world-space mesh into a WxH cell (sheet resolution).
    Returns (base RGBA uint8, tint RGBA uint8). Only the part of the cell the
    sprite and its shadow cover is rendered (then pasted into the cell)."""
    base = np.zeros((H, W, 4), np.uint8)
    tint = np.zeros((H, W, 4), np.uint8)
    if not len(mesh):
        return base, tint
    P = cam.project(mesh.V).reshape(-1, 3)
    if shadow:
        P = np.concatenate([P, cam.project(_shadow_verts(mesh.V)).reshape(-1, 3)])
    m = 6
    x0 = int(max(0, math.floor(P[:, 0].min()) - m)); x1 = int(min(W, math.ceil(P[:, 0].max()) + m))
    y0 = int(max(0, math.floor(P[:, 1].min()) - m)); y1 = int(min(H, math.ceil(P[:, 1].max()) + m))
    if x1 <= x0 or y1 <= y0:
        return base, tint
    sub = Camera(cam.ppm, (cam.anchor[0] - x0, cam.anchor[1] - y0))
    # The shadow fades out near the cell's edges instead of being cut off.
    yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float32)
    d = np.minimum(np.minimum(xx + 0.5, W - xx - 0.5), np.minimum(yy + 0.5, H - yy - 0.5))
    fade = np.clip(d / 6.0, 0, 1) ** 1.5
    b, t = _render(mesh, mats, sub, x1 - x0, y1 - y0, style, shadow, fade)
    base[y0:y1, x0:x1] = b
    tint[y0:y1, x0:x1] = t
    return base, tint


def _render(mesh, mats, cam, W, H, style=Style, shadow=True, fade=None):
    ss = style.ss
    Ws, Hs = W * ss, H * ss
    lin_col, is_tint, spec_k, gloss, metal = mats

    hi = Camera(cam.ppm * ss, (cam.anchor[0] * ss, cam.anchor[1] * ss))
    S = hi.project(mesh.V) if len(mesh) else np.zeros((0, 3, 3), np.float32)
    tri, bary, zbuf = rasterize(S, Ws, Hs)
    cov = tri >= 0

    rgb = np.zeros((Hs, Ws, 3), np.float32)
    tint_v = np.zeros((Hs, Ws), np.float32)
    tmask = np.zeros((Hs, Ws), bool)
    if cov.any():
        t = tri[cov]
        b = bary[cov]
        N = (mesh.N[t] * b[..., None]).sum(1)
        N /= np.linalg.norm(N, axis=-1, keepdims=True) + 1e-9
        Vt = mesh.V[t]
        P = (Vt * b[..., None]).sum(1)
        # geometric normal facing the camera
        g = np.cross(Vt[:, 1] - Vt[:, 0], Vt[:, 2] - Vt[:, 0])
        g /= np.linalg.norm(g, axis=-1, keepdims=True) + 1e-9
        g *= np.sign((g @ VIEW))[:, None] + (g @ VIEW == 0)[:, None]
        flip = (N * g).sum(-1) < 0
        N[flip] *= -1
        m = mesh.M[t]

        ndl = N @ KEY
        diff = np.clip((ndl + style.wrap) / (1 + style.wrap), 0, 1)
        fill = np.clip(N @ FILL, 0, 1)
        sky = np.clip(N[:, 2], 0, 1)
        ndv = np.clip(N @ VIEW, 0, 1)
        rim = (1 - ndv) ** 3 * np.clip(N @ RIM + 0.3, 0, 1)
        Hv = KEY + VIEW
        Hv /= np.linalg.norm(Hv)
        spec = np.clip(N @ Hv, 0, 1) ** gloss[m] * spec_k[m] * (ndl > 0)

        # ambient occlusion: near the ground, and screen-space from the depth buffer
        zz = np.where(cov, zbuf, np.nan)
        zfill = np.where(cov, zbuf, np.nanmin(zz) - 0.3 if np.isfinite(np.nanmin(zz)) else 0)
        r = 5 * ss / 3
        zb = _blur(zfill, r)
        cavity = np.clip((zb - zfill) * 3.0, 0, 1)[cov]   # neighbors nearer => occluded
        ao = 1 - style.ssao * cavity
        ao *= 1 - style.ground_ao * np.clip(1 - P[:, 2] / 0.35, 0, 1)

        light = (style.ambient + style.sky * sky) * ao + style.key * diff * (0.6 + 0.4 * ao) + style.fill * fill * ao
        albedo = lin_col[m]
        # metals: darker albedo, strong colored specular
        col = albedo * light[:, None] + (spec[:, None] * np.where(metal[m, None], albedo * 0.6 + 0.5, 1.0))
        col += style.rim * rim[:, None] * (0.55 + 0.45 * albedo)
        rgb[cov] = col
        tm = is_tint[m]
        tmask[cov] = tm
        # The tint layer: gray shading of a light material (the game multiplies by the civ color).
        tv = (albedo.mean(-1) * light + spec + style.rim * rim * 0.6)
        tint_v[cov] = tv

        # inner lines along depth breaks (the far side of the break darkens)
        zc = np.where(cov, zbuf, -1e3)
        brk = np.zeros((Hs, Ws), bool)
        for dy, dx in ((0, 1), (1, 0), (0, -1), (-1, 0)):
            nb = np.roll(np.roll(zc, dy, 0), dx, 1)
            brk |= (nb - zc > style.inner_depth) & cov
        lw = max(1, int(round(ss * 0.5)))
        brk = _maxfilter(brk.astype(np.float32), lw) > 0
        brk &= cov
        dark = 1 - style.inner_line * brk
        rgb *= dark[..., None]
        tint_v *= dark

    # resolve the supersamples
    base_cov = (cov & ~tmask).astype(np.float32)
    tint_cov = (cov & tmask).astype(np.float32)
    srgb = _to_srgb(rgb)
    if style.saturation != 1:
        lum = srgb.mean(-1, keepdims=True)
        srgb = np.clip(lum + (srgb - lum) * style.saturation, 0, 1)
    tgray = _to_srgb(tint_v)
    a_b = _downsample(base_cov, ss)
    a_t = _downsample(tint_cov, ss)
    c_b = _downsample(srgb * base_cov[..., None], ss)        # premultiplied
    c_t = _downsample(tgray * tint_cov, ss)

    # outer contour: a soft dark ring just outside the silhouette
    a_all = np.clip(a_b + a_t, 0, 1)
    ring = np.clip(_maxfilter(a_all, 1) - a_all, 0, 1)
    ring = np.minimum(ring, _blur(a_all, 0.7) * 2.0)
    col_all = (c_b + c_t[..., None] * 0.5) / np.maximum(a_all, 1e-6)[..., None]
    near = _blur_rgb(col_all * a_all[..., None], 1.0) / np.maximum(_blur(a_all, 1.0), 1e-6)[..., None]
    ring_a = ring * style.contour_alpha
    ring_c = near * style.contour_dark

    # ground shadow
    sh = np.zeros((H, W), np.float32)
    if shadow and len(mesh):
        Ss = hi.project(_shadow_verts(mesh.V))
        m2 = coverage(Ss, Ws, Hs).astype(np.float32)
        sh = _downsample(m2, ss)
        sh = np.clip(_blur(sh, style.shadow_blur), 0, 1) * style.shadow_alpha
        if fade is not None:
            sh *= fade

    # compose the base layer: unit base colors over (contour over shadow)
    rest = np.clip(1 - a_all, 0, 1)
    under_a = 1 - (1 - ring_a) * (1 - sh)                 # contour over shadow (both dark)
    under_c = ring_c * ring_a[..., None] / np.maximum(under_a, 1e-6)[..., None]
    base_a = a_b + under_a * rest
    base_cp = c_b + under_c * (under_a * rest)[..., None]  # premultiplied
    # The tint layer is drawn over the base: compensate so the mix is right.
    k = 1.0 / np.maximum(1 - a_t, 1e-6)
    base_a2 = np.where(a_t > 0.995, 0, np.clip(base_a * k, 0, 1))
    base_c2 = np.where((a_t > 0.995)[..., None], 0, base_cp * k[..., None])
    base = np.zeros((H, W, 4), np.float32)
    base[..., :3] = base_c2 / np.maximum(base_a2, 1e-6)[..., None]
    base[..., 3] = base_a2
    tint = np.zeros((H, W, 4), np.float32)
    tint[..., :3] = (c_t / np.maximum(a_t, 1e-6))[..., None]
    tint[..., 3] = a_t
    return _u8(base), _u8(tint)


def _blur_rgb(a, r):
    return np.stack([_blur(a[..., i], r) for i in range(a.shape[-1])], -1)


def _u8(a):
    out = np.zeros(a.shape, np.uint8)
    out[..., :3] = np.clip(a[..., :3] * 255 + 0.5, 0, 255)
    out[..., 3] = np.clip(a[..., 3] * 255 + 0.5, 0, 255)
    out[out[..., 3] == 0] = 0
    return out
