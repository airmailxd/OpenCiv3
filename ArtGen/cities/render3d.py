"""A small software renderer for isometric city sprites.

World units are pixels of the 2x art: a map tile is 128x128 world units on the
ground, and the projection is the Civ3 2:1 isometric one:

    sx = cx + (x - y)        sy = cy + (x + y) / 2 - z

so the camera looks along -(1, 1, 1) and sees the +x (screen lower right), +y
(screen lower left) faces and the tops of things. A scene is a list of
triangles with a material id, a color and a building id; it is rasterized
with a z-buffer at a supersampled resolution, lit from the upper left with a
shadow map, soft ambient occlusion from a top-down height map, procedural
surface texture (windows, roof tiles, thatch, brick...) and a thin darker
contour, then box-filtered down to the output size.
"""
import math
import numpy as np

# ---------------------------------------------------------------- materials
PLAIN, STUCCO, WINDOWS, ROOF_TILE, THATCH, SLATE, GLASS, BRICK, METAL, DOME, STONE, \
    WATER, FOLIAGE, WOOD, GOLD, RUBBLE, PAVING = range(17)

# Toward the light: from the left, a little up (north-west on the map), high.
_EL = math.radians(48)
_H = np.array([-0.82, 0.57])
_H = _H / np.linalg.norm(_H)
LIGHT = np.array([math.cos(_EL) * _H[0], math.cos(_EL) * _H[1], math.sin(_EL)])
VIEW = np.array([1.0, 1.0, 1.0]) / math.sqrt(3)


class Scene:
    """Collects triangles. Each triangle: 3 vertices, optional 3 vertex normals,
    a color (linear 0..1 RGB), a material and the id of the building it's part of."""

    def __init__(self):
        self.v = []      # (3,3)
        self.n = []      # (3,3) or None
        self.col = []
        self.mat = []
        self.bid = []
        self.buildings = []   # per building params dict

    def new_building(self, **params):
        self.buildings.append(params)
        return len(self.buildings) - 1

    def tri(self, a, b, c, col, mat, bid, normals=None):
        self.v.append((a, b, c))
        self.n.append(normals)
        self.col.append(col)
        self.mat.append(mat)
        self.bid.append(bid)

    def quad(self, a, b, c, d, col, mat, bid, normals=None):
        if normals is None:
            self.tri(a, b, c, col, mat, bid)
            self.tri(a, c, d, col, mat, bid)
        else:
            na, nb, nc, nd = normals
            self.tri(a, b, c, col, mat, bid, (na, nb, nc))
            self.tri(a, c, d, col, mat, bid, (na, nc, nd))

    def arrays(self):
        V = np.array(self.v, np.float64).reshape(-1, 3, 3)
        N = np.zeros_like(V)
        flat = np.zeros(len(self.v), bool)
        for i, n in enumerate(self.n):
            if n is None:
                flat[i] = True
            else:
                N[i] = n
        fn = np.cross(V[:, 1] - V[:, 0], V[:, 2] - V[:, 0])
        ln = np.linalg.norm(fn, axis=1, keepdims=True)
        fn = fn / np.maximum(ln, 1e-12)
        # Every visible face of a closed solid faces the viewer.
        fn *= np.where((fn @ VIEW) < 0, -1, 1)[:, None]
        N[flat] = fn[flat][:, None, :]
        N /= np.maximum(np.linalg.norm(N, axis=2, keepdims=True), 1e-12)
        keep = ln[:, 0] > 1e-9
        return (V[keep], N[keep], np.array(self.col, np.float64)[keep],
                np.array(self.mat, np.int32)[keep], np.array(self.bid, np.int32)[keep], fn[keep])


def rasterize(P, D, W, H, NV=None):
    """P: (T,3,2) pixel coords, D: (T,3) depth (larger = nearer). Returns
    depth buffer, triangle id buffer and (if NV given, (T,3,3)) interpolated normals."""
    zb = np.full((H, W), -np.inf, np.float64)
    ib = np.full((H, W), -1, np.int32)
    nb = np.zeros((H, W, 3), np.float64) if NV is not None else None
    xmin = np.clip(np.floor(P[:, :, 0].min(1) - 0.5).astype(int), 0, W)
    xmax = np.clip(np.ceil(P[:, :, 0].max(1) + 0.5).astype(int), 0, W)
    ymin = np.clip(np.floor(P[:, :, 1].min(1) - 0.5).astype(int), 0, H)
    ymax = np.clip(np.ceil(P[:, :, 1].max(1) + 0.5).astype(int), 0, H)
    for t in range(len(P)):
        x0, x1, y0, y1 = xmin[t], xmax[t], ymin[t], ymax[t]
        if x1 <= x0 or y1 <= y0:
            continue
        (ax, ay), (bx, by), (cx, cy) = P[t]
        area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax)
        if abs(area) < 1e-9:
            continue
        xs = np.arange(x0, x1) + 0.5
        ys = (np.arange(y0, y1) + 0.5)[:, None]
        w0 = ((bx - xs) * (cy - ys) - (by - ys) * (cx - xs)) / area
        w1 = ((cx - xs) * (ay - ys) - (cy - ys) * (ax - xs)) / area
        w2 = 1.0 - w0 - w1
        e = -1e-6
        inside = (w0 >= e) & (w1 >= e) & (w2 >= e)
        if not inside.any():
            continue
        d = w0 * D[t, 0] + w1 * D[t, 1] + w2 * D[t, 2]
        sub = zb[y0:y1, x0:x1]
        m = inside & (d > sub)
        if not m.any():
            continue
        sub[m] = d[m]
        ib[y0:y1, x0:x1][m] = t
        if nb is not None:
            n = (w0[..., None] * NV[t, 0] + w1[..., None] * NV[t, 1] + w2[..., None] * NV[t, 2])
            nb[y0:y1, x0:x1][m] = n[m]
    return zb, ib, nb


def _blur(a, r):
    """Separable box blur (3 passes ~ gaussian) with edge clamping."""
    if r <= 0:
        return a
    out = a.astype(np.float64)
    for _ in range(3):
        for ax in (0, 1):
            pad = [(0, 0)] * out.ndim
            pad[ax] = (r + 1, r)
            p = np.pad(out, pad, mode='edge')
            c = np.cumsum(p, axis=ax)
            hi = np.take(c, np.arange(2 * r + 1, c.shape[ax]), axis=ax)
            lo = np.take(c, np.arange(0, c.shape[ax] - 2 * r - 1), axis=ax)
            out = (hi - lo) / (2 * r + 1)
    return out


def _hash2(ix, iy, seed=0):
    h = (ix.astype(np.int64) * 374761393 + iy.astype(np.int64) * 668265263 + seed * 2246822519) & 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((h ^ (h >> 16)) & 0xFFFF) / 65535.0


def value_noise(x, y, seed=0):
    ix, iy = np.floor(x), np.floor(y)
    fx, fy = x - ix, y - iy
    fx = fx * fx * (3 - 2 * fx)
    fy = fy * fy * (3 - 2 * fy)
    a = _hash2(ix, iy, seed); b = _hash2(ix + 1, iy, seed)
    c = _hash2(ix, iy + 1, seed); d = _hash2(ix + 1, iy + 1, seed)
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def fbm(x, y, seed=0, octaves=3):
    s, amp, tot = 0.0, 1.0, 0.0
    for o in range(octaves):
        s = s + amp * value_noise(x, y, seed + o * 17)
        tot += amp
        x, y, amp = x * 2.03, y * 2.03, amp * 0.5
    return s / tot


def _smoothpulse(u, period, width, aa):
    """1 inside [0,width) of each period (centered), soft edges of size aa."""
    f = np.mod(u, period) - (period - width) / 2
    return np.clip(np.minimum(f, width - f) / aa + 0.5, 0, 1)


def render(scene, W, H, cx, cy, ss=4, ground=None, shadow_strength=0.42, contour=0.45):
    """Renders the scene into an (H, W) RGBA float image (0..1, straight alpha).
    (cx, cy): screen position of the world origin in output pixels.
    ground: optional function(x, y, shadow, ao) -> (rgb (...,3), alpha (...)) for
    the ground plane under the city; the default draws only shadow and AO."""
    V, N, C, M, B, FN = scene.arrays()
    SW, SH = W * ss, H * ss
    P = np.stack([cx + V[..., 0] - V[..., 1], cy + (V[..., 0] + V[..., 1]) / 2 - V[..., 2]], -1) * ss
    Dep = V.sum(-1)
    zb, ib, nb = rasterize(P, Dep, SW, SH, N)
    cover = ib >= 0

    # World position of every pixel (on the ground plane where nothing was drawn).
    px = (np.arange(SW) + 0.5) / ss - cx
    py = (np.arange(SH) + 0.5)[:, None] / ss - cy
    sxw = np.broadcast_to(px[None, :], (SH, SW))
    syw = np.broadcast_to(py, (SH, SW))
    s = np.where(cover, (syw + zb) / 1.5, 2 * syw)
    wz = np.where(cover, zb - s, 0.0)
    wx = (s + sxw) / 2
    wy = (s - sxw) / 2
    nrm = np.where(cover[..., None], nb, np.array([0, 0, 1.0]))
    nrm = nrm / np.maximum(np.linalg.norm(nrm, axis=-1, keepdims=True), 1e-9)

    # ---------------- shadow map (orthographic, from the light)
    L = LIGHT
    up = np.array([0, 0, 1.0])
    lu = np.cross(up, L); lu /= np.linalg.norm(lu)
    lv = np.cross(L, lu)
    res = 3.0
    lx = V @ lu; ly = V @ lv; ld = V @ L
    ox, oy = lx.min() - 8, ly.min() - 8
    SMW = int((lx.max() - ox + 8) * res) + 1
    SMH = int((ly.max() - oy + 8) * res) + 1
    smz, _, _ = rasterize(np.stack([(lx - ox) * res, (ly - oy) * res], -1), ld, SMW, SMH)
    P3 = np.stack([wx, wy, wz], -1)
    qx = ((P3 @ lu) - ox) * res
    qy = ((P3 @ lv) - oy) * res
    qd = P3 @ L
    lit = np.zeros((SH, SW))
    offs = [(0, 0), (1.5, 0), (-1.5, 0), (0, 1.5), (0, -1.5), (1.1, 1.1), (-1.1, 1.1), (1.1, -1.1), (-1.1, -1.1)]
    for ddx, ddy in offs:
        ix = np.clip((qx + ddx).astype(int), 0, SMW - 1)
        iy = np.clip((qy + ddy).astype(int), 0, SMH - 1)
        inb = ((qx + ddx) >= 0) & ((qx + ddx) < SMW) & ((qy + ddy) >= 0) & ((qy + ddy) < SMH)
        occ = inb & (smz[iy, ix] > qd + 0.9)
        lit += ~occ
    lit /= len(offs)

    # ---------------- top-down height map for AO
    gx0 = min(V[..., 0].min(), wx.min()) - 20
    gy0 = min(V[..., 1].min(), wy.min()) - 20
    GW = int(max(V[..., 0].max(), wx.max()) - gx0 + 20) + 1
    GH = int(max(V[..., 1].max(), wy.max()) - gy0 + 20) + 1
    hz, _, _ = rasterize(np.stack([V[..., 0] - gx0, V[..., 1] - gy0], -1), V[..., 2], GW, GH)
    hz = np.where(np.isfinite(hz), np.maximum(hz, 0), 0)
    occ_g = (hz > 0.5).astype(float)
    ao = np.ones((SH, SW))
    gi = np.clip((wx - gx0).astype(int), 0, GW - 1)
    gj = np.clip((wy - gy0).astype(int), 0, GH - 1)
    for r, wgt in ((3, 0.5), (8, 0.35), (16, 0.2)):
        hb = _blur(hz, r)
        # How much the surroundings rise above this point.
        rise = np.clip((hb[gj, gi] - wz) / (2.2 * r), 0, 1)
        ao -= wgt * rise
    ao = np.clip(ao, 0.25, 1)
    # Walls darken toward their base.
    ao *= np.where(cover, 1 - 0.28 * np.exp(-wz / 5.0) * (1 - np.abs(nrm[..., 2])), 1)

    # ---------------- surface color
    albedo = np.where(cover[..., None], C[np.maximum(ib, 0)], 0.0)
    mat = np.where(cover, M[np.maximum(ib, 0)], -1)
    bid = np.where(cover, B[np.maximum(ib, 0)], -1)
    spec = np.zeros((SH, SW)); gloss = np.full((SH, SW), 16.0)
    albedo, spec, gloss, emis = texture(scene, albedo, mat, bid, wx, wy, wz, nrm, ss, spec, gloss)

    ndl = np.clip(nrm @ L, 0, 1)
    sky = 0.55 + 0.45 * nrm[..., 2]
    # Light from the sky is a bit cool, the sun a bit warm.
    amb = (0.40 * sky + 0.20)[..., None] * np.array([0.95, 0.98, 1.05]) * ao[..., None]
    sun = (0.80 * ndl * lit)[..., None] * np.array([1.05, 1.0, 0.92])
    hvec = L + VIEW; hvec /= np.linalg.norm(hvec)
    sp = (spec * np.clip(nrm @ hvec, 0, 1) ** gloss * lit)[..., None]
    # Rim light on the upper left edges of rounded things.
    rim = (0.10 * np.clip(1 - (nrm @ VIEW), 0, 1) ** 2 * np.clip(nrm @ L, 0, 1))[..., None]
    rgb = albedo * (amb + sun + rim) + sp + emis[..., None] * albedo
    lum = rgb @ np.array([0.3, 0.55, 0.15])
    rgb = lum[..., None] + (rgb - lum[..., None]) * 1.08

    # ---------------- contour: silhouette and depth/building discontinuities
    edge = np.zeros((SH, SW))
    rr = max(1, int(round(ss * 1.0)))
    for dx, dy in [(rr, 0), (-rr, 0), (0, rr), (0, -rr)]:
        zs = np.roll(np.roll(zb, dy, 0), dx, 1)
        bs = np.roll(np.roll(bid, dy, 0), dx, 1)
        cs = np.roll(np.roll(cover, dy, 0), dx, 1)
        # This pixel is behind a neighbor that is much nearer: draw the line on the far side.
        jump = cover & cs & (np.nan_to_num(zs - zb, nan=0, posinf=0, neginf=0) > 6)
        diffb = cover & cs & (bs != bid) & (zs > zb)
        edge = np.maximum(edge, np.where(jump, 1.0, 0) + np.where(diffb, 0.6, 0))
        sil = cover & ~cs
        edge = np.maximum(edge, sil * 1.0)
    edge = np.clip(edge, 0, 1)
    rgb = rgb * (1 - contour * edge[..., None] * 0.9)

    alpha = cover.astype(float)
    # ---------------- ground: shadow, AO and optional ground color
    gshade = (1 - lit) * shadow_strength + (1 - ao) * 0.55
    gshade = np.clip(gshade, 0, 0.75)
    if ground is not None:
        grgb, ga = ground(wx, wy, lit, ao)
        gr = grgb * (0.55 + 0.45 * lit[..., None]) * (0.6 + 0.4 * ao[..., None])
        # Composite: ground color under, then the shadow on top of bare ground.
        ga_tot = ga + (1 - ga) * gshade
        gcol = np.where(ga_tot[..., None] > 1e-6, gr * (ga / np.maximum(ga_tot, 1e-6))[..., None], 0)
    else:
        ga_tot = gshade
        gcol = np.zeros_like(rgb)
    out_rgb = np.where(cover[..., None], rgb, gcol)
    out_a = np.where(cover, 1.0, ga_tot)

    # Downsample (premultiplied box filter).
    pm = out_rgb * out_a[..., None]
    pm = pm.reshape(H, ss, W, ss, 3).mean((1, 3))
    a = out_a.reshape(H, ss, W, ss).mean((1, 3))
    rgb = np.where(a[..., None] > 1e-6, pm / np.maximum(a, 1e-6)[..., None], 0)
    return np.concatenate([np.clip(rgb, 0, 1), a[..., None]], -1)


def texture(scene, albedo, mat, bid, wx, wy, wz, nrm, ss, spec, gloss):
    """Procedural surface detail by material."""
    emis = np.zeros(mat.shape)
    aa = 0.6  # edge softness in world units (a bit more than a pixel at 2x)
    nz = np.abs(nrm[..., 2])
    vert = nz < 0.35
    # Coordinate along a wall: walls facing +-x run along y, and the other way.
    along = np.where(np.abs(nrm[..., 0]) > np.abs(nrm[..., 1]), wy, wx)
    if any('rcx' in b for b in scene.buildings):
        rcx = np.array([b.get('rcx', np.nan) for b in scene.buildings] + [np.nan])[np.where(bid >= 0, bid, len(scene.buildings))]
        rcy = np.array([b.get('rcy', np.nan) for b in scene.buildings] + [np.nan])[np.where(bid >= 0, bid, len(scene.buildings))]
        rnd = np.isfinite(rcx)
        dx, dy = wx - np.nan_to_num(rcx), wy - np.nan_to_num(rcy)
        along = np.where(rnd, np.arctan2(dy, dx) * np.hypot(dx, dy), along)
    n1 = fbm(wx * 0.25 + wz * 0.13, wy * 0.25 - wz * 0.11, 3)
    n2 = fbm(wx * 1.1 + wz * 0.7, wy * 1.1 + wz * 0.5, 9, 2)
    a = albedo.copy()

    # Building parameters per pixel.
    nb = len(scene.buildings)
    def bparam(key, default):
        arr = np.array([b.get(key, default) for b in scene.buildings] + [default], np.float64)
        return arr[np.where(bid >= 0, bid, nb)]

    def tint(m, f):
        a[m] = a[m] * f[m][..., None] if np.ndim(f) else a[m] * f

    grain = 0.92 + 0.16 * n1 + 0.06 * (n2 - 0.5)
    tint(mat >= 0, grain)

    # Windows on walls.
    m = (mat == WINDOWS) & vert
    if m.any():
        fh = bparam('floor', 9.0); per = bparam('wspace', 7.0); ww = bparam('wwidth', 3.0)
        wh = bparam('wheight', 4.5); z0 = bparam('z0', 0.0); top = bparam('wtop', 1e9)
        wcol = bparam('wdark', 0.35)
        zz = wz - z0
        rows = _smoothpulse(zz - fh * 0.5, fh, wh, aa) * (zz > fh * 0.35) * (wz < top - 2.0)
        cols = _smoothpulse(along, per, ww, aa)
        win = rows * cols
        f = 1 - win * (1 - wcol)
        # A lit sill under each window.
        sill = _smoothpulse(zz - fh * 0.5 - wh / 2 - 0.6, fh, 0.8, aa) * cols * (zz > fh * 0.35) * (wz < top - 2.0)
        f = f + 0.25 * sill
        tint(m, f)
        spec[m] = np.maximum(spec[m], 0.15 * win[m])

    # Small dark openings (doors and slit windows) on stucco / mud brick.
    m = (mat == STUCCO) & vert
    if m.any():
        per = bparam('wspace', 9.0); fh = bparam('floor', 10.0); z0 = bparam('z0', 0.0)
        top = bparam('wtop', 1e9)
        zz = wz - z0
        rnd = _hash2(np.floor(along / per), np.floor(zz / fh), 5)
        rows = _smoothpulse(zz - fh * 0.55, fh, 3.2, aa) * (zz > fh * 0.3) * (wz < top - 2.5)
        cols = _smoothpulse(along, per, 2.2, aa) * (rnd > 0.35)
        win = rows * cols
        tint(m, 1 - 0.55 * win)
        # A faint band at each floor line.
        band = _smoothpulse(zz, fh, 0.7, aa) * (zz > 1)
        tint(m, 1 - 0.08 * band)

    # Arcades: round-topped openings along the ground floor.
    arch = bparam('arch', 0.0)
    m = (arch > 0) & vert & ((mat == STUCCO) | (mat == STONE) | (mat == WINDOWS) | (mat == BRICK))
    if m.any():
        per = np.where(arch > 0, arch, 1.0); aw = per * 0.62; ah = bparam('archh', 9.0)
        z0 = bparam('z0', 0.0)
        f = np.mod(along, per) - per / 2
        half = aw / 2
        topz = ah - half + np.sqrt(np.clip(half * half - f * f, 0, None))
        inside = np.clip((half - np.abs(f)) / aa, 0, 1) * np.clip((z0 + topz - wz) / aa, 0, 1) * (wz > z0 + 0.3)
        # The opening is dark, with a lit inner edge on its left.
        tint(m, 1 - 0.72 * inside)

    # Brick: fine courses.
    m = (mat == BRICK) & vert
    if m.any():
        course = _smoothpulse(wz, 2.0, 0.45, 0.35)
        tint(m, 1 - 0.12 * course + 0.08 * (n2 - 0.5))
    m = mat == BRICK
    if m.any():
        tint(m, 0.95 + 0.1 * _hash2(np.floor(along / 3), np.floor(wz / 2), 2))

    # Pitched roof tiles: rows across the slope, and ridges down it.
    for mm, rowp, colp, depth in ((ROOF_TILE, 2.6, 2.6, 0.22), (SLATE, 2.2, 3.5, 0.13), (THATCH, 1.2, 0.0, 0.10)):
        m = (mat == mm) & ~vert
        if not m.any():
            continue
        # Direction down the slope (horizontal part of the normal).
        hx, hy = nrm[..., 0], nrm[..., 1]
        hl = np.sqrt(hx * hx + hy * hy) + 1e-9
        flat = hl < 0.08
        down = (wx * hx + wy * hy) / hl  # distance down the slope
        across = (-wx * hy + wy * hx) / hl
        rowsig = 0.5 + 0.5 * np.cos(2 * np.pi * (down - wz * 0.3) / rowp)
        if colp > 0:
            colsig = 0.5 + 0.5 * np.cos(2 * np.pi * (across + 0.5 * rowp * np.floor(down / rowp)) / colp)
            sig = 1 - depth * (rowsig ** 3) - depth * 0.6 * (colsig ** 8)
        else:
            streak = fbm(across * 1.6, down * 0.15, 21, 2)
            sig = 1 - depth * rowsig ** 2 + 0.25 * (streak - 0.5)
        sig = np.where(flat, 1.0, sig)
        tint(m, sig)
    m = (mat == THATCH)
    if m.any():
        tint(m, 0.9 + 0.2 * fbm(wx * 0.9 + wz * 0.4, wy * 0.9 - wz * 0.4, 13, 2))

    # Glass: cool reflective with mullions.
    m = (mat == GLASS)
    if m.any():
        fh = bparam('floor', 6.0); per = bparam('wspace', 4.0)
        refl = np.clip(0.5 + 0.5 * (wz / 80.0) + 0.25 * (n1 - 0.5), 0, 1.2)
        tint(m, 0.75 + 0.35 * refl)
        mull = np.maximum(_smoothpulse(wz, fh, 0.7, aa), _smoothpulse(along, per, 0.6, aa))
        tint(m & vert, 1 - 0.3 * mull)
        spec[m] = 0.6; gloss[m] = 30

    m = (mat == METAL) | (mat == DOME)
    if m.any():
        spec[m] = np.where(mat[m] == METAL, 0.25, 0.18); gloss[m] = 20
    m = (mat == GOLD)
    if m.any():
        spec[m] = 0.7; gloss[m] = 12
    m = (mat == WATER)
    if m.any():
        spec[m] = 0.8; gloss[m] = 40
    m = (mat == FOLIAGE)
    if m.any():
        tint(m, 0.7 + 0.5 * fbm(wx * 0.6 + wz, wy * 0.6 - wz, 31, 2))
    m = (mat == STONE)
    if m.any():
        blk = np.maximum(_smoothpulse(wz, 3.0, 0.4, 0.4),
                         _smoothpulse(along + 2.5 * np.floor(wz / 3.0), 5.0, 0.4, 0.4))
        tint(m & vert, 1 - 0.12 * blk)
        tint(m, 0.92 + 0.16 * _hash2(np.floor(along / 5 + 0.5 * np.floor(wz / 3)), np.floor(wz / 3), 4))
    m = (mat == RUBBLE)
    if m.any():
        tint(m, 0.8 + 0.4 * fbm(wx * 0.7 + wz * 0.5, wy * 0.7, 41, 2))
    m = (mat == WOOD)
    if m.any():
        tint(m & vert, 1 - 0.12 * _smoothpulse(along, 2.0, 0.4, 0.4))
    return a, spec, gloss, emis
