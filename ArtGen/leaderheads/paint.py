"""The shared painted finish for the leader heads (build.py --unified):
turns a cropped photo, statue, mosaic or painting into a portrait that
matches the rest of the set, with plain image processing (no generated
pixels):

  1. a soft subject mask (how far each pixel's color is from the picture's
     border, plus a central oval), so the background can be repainted;
  2. a generalized Kuwahara filter (edge-preserving smoothing that turns
     fine texture into flat strokes but keeps edges such as eyes and lips);
  3. one palette for all: colors pulled toward a warm umber-to-cream
     gradient map by their brightness, with some of their own hue kept;
  4. one backdrop for all: a warm dark painted ground, lighter behind the
     head, replacing most of the source's own background;
  5. brush texture: noise smeared along the picture's own contours (line
     integral convolution), faint over faces and stronger in the backdrop,
     and a faint canvas weave.

Works on a picture twice the final size; build.py scales it down after.
"""
import numpy as np
from scipy import ndimage as nd

# the shared palette: a gradient map from shadow to light
PALETTE = np.array([
    (0.00, (0.07, 0.05, 0.04)),
    (0.25, (0.24, 0.16, 0.11)),
    (0.50, (0.52, 0.38, 0.27)),
    (0.75, (0.80, 0.66, 0.50)),
    (1.00, (0.98, 0.92, 0.80)),
], dtype=object)
# the backdrop: dark umber, warmer and lighter where the light falls
BACK_DARK = np.array([0.10, 0.075, 0.055], np.float32)
BACK_LIGHT = np.array([0.36, 0.27, 0.18], np.float32)


def _luma(a):
    return a @ np.array([0.299, 0.587, 0.114], np.float32)


def _gradient_map(lum):
    xs = np.array([p[0] for p in PALETTE], np.float32)
    cs = np.array([p[1] for p in PALETTE], np.float32)
    return np.stack([np.interp(lum, xs, cs[:, i]) for i in range(3)], -1).astype(np.float32)


def subject_mask(a, oval_weight=1.0, erode=60, plain=None):
    """0 (background) to 1 (subject): color distance from the median of the
    picture's border, smoothed, with a central oval where the sitter is."""
    n = a.shape[0]
    b = max(2, n // 40)
    border = np.concatenate([a[:b].reshape(-1, 3), a[-b:].reshape(-1, 3), a[:, :b].reshape(-1, 3),
                             a[:, -b:].reshape(-1, 3)])
    ref = np.median(border, 0)
    spread = np.median(np.abs(border - ref).sum(-1)) + 0.04
    d = np.abs(nd.gaussian_filter(a, (n / 120, n / 120, 0)) - ref).sum(-1)
    m = np.clip((d - spread) / (2.5 * spread), 0, 1)
    # detail: backgrounds are mostly smooth, faces and clothes are not (this
    # finds a gray face before a gray backdrop)
    lum = _luma(a)
    g = np.hypot(nd.sobel(lum, 0), nd.sobel(lum, 1))
    busy = nd.gaussian_filter(g, n / 40)
    busy_bg = np.median(np.concatenate([busy[:b].ravel(), busy[-b:].ravel(), busy[:, :b].ravel(), busy[:, -b:].ravel()]))
    busy_m = np.clip((busy - busy_bg * 1.6) / (busy_bg * 2 + 0.05), 0, 1)
    m = np.maximum(m, busy_m)
    y, x = np.mgrid[0:n, 0:n].astype(np.float32)
    oval = np.exp(-(((x - n / 2) / (n * 0.30)) ** 2 + ((y - n * 0.50) / (n * 0.40)) ** 4))
    # the oval counts for less where a pixel matches an even backdrop (a
    # bright museum wall behind a bust would otherwise glow round its head)
    # (plain: how sure the backdrop is plain, if known; else from its spread)
    even = np.clip(1 - (spread - 0.04) / 0.12, 0, 1) if plain is None else plain
    close = np.clip(1 - d / (3 * spread), 0, 1) * even
    m = np.maximum(m, oval * oval_weight * (1 - 0.85 * close))
    # close small holes, pull the edge in a little (so no rim of the old
    # background is left around the sitter), then soften it
    m = nd.grey_closing(m, size=max(3, n // 30))
    m = nd.grey_erosion(m, size=max(3, n // erode))
    m = np.clip(nd.gaussian_filter(m, n / 120), 0, 1)
    # the old wall itself: smooth and the color of an even border, wherever it
    # is (the painted ground replaces it even inside the oval)
    # (measured close up, so the rim just outside the sitter's edge, which
    # the broad masks above take in, is found too)
    d_px = np.abs(nd.gaussian_filter(a, (1, 1, 0)) - ref).sum(-1)
    even_px = np.clip(1 - (spread - 0.04) / 0.25, 0, 1) if plain is None else plain
    close_px = np.clip(1.6 - d_px / spread, 0, 1) * even_px
    busy_px = nd.gaussian_filter(g, n / 150)
    busy_px = np.clip((busy_px - busy_bg * 1.6) / (busy_bg * 2 + 0.05), 0, 1)
    wall = nd.gaussian_filter(np.maximum(close * (1 - busy_m), close_px), 1.0)
    return m, np.clip(wall, 0, 1)


def kuwahara(a, r):
    """Generalized Kuwahara with soft round sectors: Gaussian-weighted means
    of eight neighborhoods around each pixel (offset by r in eight
    directions), each weighted by how even (low in variance) it is. Flat
    areas become strokes; an edge keeps the side each pixel belongs to.
    Soft weights instead of square boxes avoid blocky artifacts."""
    s = max(0.8, r * 0.6)
    mean = nd.gaussian_filter(a, (s, s, 0))
    sq = nd.gaussian_filter(a * a, (s, s, 0))
    var = np.clip(sq - mean * mean, 0, None).sum(-1)
    acc = np.zeros_like(a)
    wsum = np.zeros(a.shape[:2], np.float32)
    for k in range(8):
        ang = k * np.pi / 4
        off = (r * np.sin(ang) * 0.7, r * np.cos(ang) * 0.7)
        mq = nd.shift(mean, (off[0], off[1], 0), order=1, mode='nearest')
        vq = nd.shift(var, off, order=1, mode='nearest')
        w = 1.0 / (1e-4 + vq) ** 1.5
        acc += mq * w[..., None]
        wsum += w
    return acc / wsum[..., None]


def _orientation(lum, n):
    gx = nd.sobel(lum, 1)
    gy = nd.sobel(lum, 0)
    s = n / 60
    jxx = nd.gaussian_filter(gx * gx, s)
    jyy = nd.gaussian_filter(gy * gy, s)
    jxy = nd.gaussian_filter(gx * gy, s)
    # the direction of least change: along the contours
    theta = 0.5 * np.arctan2(2 * jxy, jxx - jyy) + np.pi / 2
    return np.cos(theta), np.sin(theta)


def brush_texture(lum, n, seed, length=None, direction=None, blur=0.7):
    """Noise smeared along the contours (or one direction): short strokes
    that follow forms."""
    rng = np.random.default_rng(seed)
    noise = rng.random((n, n)).astype(np.float32)
    noise = nd.gaussian_filter(noise, blur)
    if direction is None:
        cx, cy = _orientation(lum, n)
    else:
        # crisscross strokes, like a brushed ground: two directions by patches
        patch = nd.gaussian_filter(rng.random((n, n)).astype(np.float32), n / 16) > 0.5
        ang = np.where(patch, direction, direction + np.pi / 2.6).astype(np.float32)
        cx, cy = np.cos(ang), np.sin(ang)
    length = length or max(4, n // 40)
    y, x = np.mgrid[0:n, 0:n].astype(np.float32)
    acc = np.zeros((n, n), np.float32)
    for sgn in (1, -1):
        px, py = x.copy(), y.copy()
        for _ in range(length):
            ix = np.clip(px.round().astype(int), 0, n - 1)
            iy = np.clip(py.round().astype(int), 0, n - 1)
            acc += noise[iy, ix]
            dx, dy = cx[iy, ix], cy[iy, ix]
            px += sgn * dx
            py += sgn * dy
    t = acc / (2 * length)
    t = (t - t.mean()) / (t.std() + 1e-6)
    return t


def backdrop(n, seed):
    """The shared painted ground: dark umber, a soft light behind the head
    from the upper left, uneven like a scumbled underpainting."""
    rng = np.random.default_rng(seed + 7)
    y, x = np.mgrid[0:n, 0:n].astype(np.float32)
    glow = np.exp(-(((x - n * 0.28) / (n * 0.5)) ** 2 + ((y - n * 0.18) / (n * 0.5)) ** 2))
    blot = nd.gaussian_filter(rng.random((n, n)).astype(np.float32), n / 14)
    blot = (blot - blot.mean()) / (blot.std() + 1e-6)
    t = np.clip(glow * 0.6 + blot * 0.10, 0, 1)[..., None]
    return BACK_DARK * (1 - t) + BACK_LIGHT * t


def paint(img, seed=0, strength=1.0, oval=1.0, keep=0.4, broad=70, bg=0.92, color=0.75, erode=60, level=0.42,
          fixed=None, plain=None, contrast=1.0, texture=1.0, green=None, hand=None, soften=0,
          hand_only=False, local=0, feather=0, edge=0, shrink=0):
    """The painted finish on a PIL RGB picture (returns float RGB 0..1).
    Per picture: oval, how sure the center is the sitter (lower for a
    sitter before a bright wall, which would glow); keep, how much of the
    real picture is kept on the sitter (higher for fine detail like a
    mosaic); broad, the background strokes' size as a fraction (n // broad);
    bg, how much of the old background the painted ground replaces."""
    a = np.asarray(img, np.float32) / 255
    n = a.shape[0]
    if soften:
        # (a print: its hatching softened into tone first)
        a = nd.gaussian_filter(a, (soften, soften, 0))
    if contrast != 1.0:
        a = np.clip((a - a.mean()) * contrast + a.mean(), 0, 1)
    mask, wall = subject_mask(a, oval, erode, plain)
    if local:
        # local contrast: detail relative to the neighborhood amplified, so a
        # pale face on pale silk keeps its features
        base = nd.gaussian_filter(a, (n / 30, n / 30, 0))
        a = np.clip(base + (a - base) * local, 0, 1)
    if hand:
        # a hand-placed outline: polygons (in fractions of the side) of the
        # sitter; outside them is background whatever the mask thought
        from PIL import Image as _I, ImageDraw as _D
        im = _I.new('L', (n, n), 0)
        for poly in hand:
            _D.Draw(im).polygon([(x * n, y * n) for x, y in poly], fill=255)
        hm = nd.gaussian_filter(np.asarray(im, np.float32) / 255, n / 120)
        # inside the outline the wall the mask found is still ground, unless
        # the outline is all there is to go by (hand_only)
        mask = hm if hand_only else hm * (1 - wall)
        wall = 1 - hm if hand_only else np.maximum(wall, 1 - hm)
    if fixed:
        # a sitter the mask can't find (pale on pale paper): a fixed soft
        # silhouette, an oval head over wide shoulders, (cx, cy, rx, ry)
        cx, cy, rx, ry = fixed
        yy, xx = np.mgrid[0:n, 0:n].astype(np.float32) / n
        head = ((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2 < 1
        body = (yy > cy + ry * 0.6) & (np.abs(xx - cx) < rx * 1.9 + (yy - cy - ry * 0.6) * 1.2)
        mask = np.clip(nd.gaussian_filter((head | body).astype(np.float32), n / 50), 0, 1)
        wall = np.zeros_like(mask)
    if shrink:
        # the sitter's edge pulled in (pixels of the final tile)
        k = 2 * int(round(shrink * n / 230)) + 1
        mask = nd.grey_erosion(mask, size=k)
        wall = nd.grey_dilation(wall, size=k)
    if feather:
        # a softer sitter's edge, so it doesn't look cut out
        mask = nd.gaussian_filter(mask, feather * n / 230)
        wall = nd.gaussian_filter(wall, feather * n / 230)
    # strokes: light smoothing on the subject (the face keeps its detail),
    # broader in the background
    fine = kuwahara(a, max(2, n // 150))
    broad = kuwahara(a, max(2, n // broad))
    m3 = mask[..., None]
    # on the subject, half the stroke-smoothed picture and half the real one
    p = (fine * (1 - keep) + a * keep) * m3 + broad * (1 - m3)
    # one palette: a gradient map by brightness, with the picture's own
    # color kept on top (pictures in color stay in color, but share a key)
    lum = _luma(np.clip(p, 0, 1))
    mapped = _gradient_map(lum)
    chroma = p - lum[..., None]
    sat = float(np.abs(chroma).mean())
    if sat > 0.03:
        # the shared key, with the picture's own (somewhat calmer) color
        # (strong colors limited, so no face goes neon)
        c = np.linalg.norm(chroma, axis=-1, keepdims=True)
        chroma = chroma * np.minimum(1, 0.18 / (c + 1e-6))
        p = (mapped * 0.55 + lum[..., None] * 0.45) + chroma * color
    else:                                        # black and white: all gradient map
        p = mapped
    # one brightness: the sitter's mid-tones (weighted to the face) brought
    # to one level, before the ground goes in, so every ground is as dark
    if level:
        y, x = np.mgrid[0:n, 0:n].astype(np.float32)
        wgt = mask * np.exp(-(((x - n / 2) / (n * 0.3)) ** 2 + ((y - n * 0.45) / (n * 0.3)) ** 2))
        mean = float((_luma(np.clip(p, 0, 1)) * wgt).sum() / (wgt.sum() + 1e-6))
        g = np.clip(np.log(level) / np.log(max(1e-3, min(mean, 0.97))), 0.7, 1.45)
        p = np.clip(p, 0, 1) ** g
    # one backdrop
    bd = backdrop(n, seed)
    # (the ground takes over quickly past the sitter's edge, so no rim of the
    # old wall shows in the soft band between)
    bg_share = np.maximum(1 - mask, wall * 0.9)[..., None] * bg * strength
    # light rims: in the soft band at the sitter's edge, anything lighter
    # than both the sitter nearby and the ground is old wall: ground it
    core = mask > 0.7
    ring = nd.binary_dilation(core, iterations=max(2, n // 40)) & ~core
    band = ring & (mask < 0.6)
    lp0 = _luma(np.clip(p, 0, 1))
    inner = nd.grey_erosion(np.where(mask > 0.7, lp0, 0), size=3)
    local = nd.maximum_filter(np.where(mask > 0.7, lp0, 0), size=max(5, n // 25))
    lim = np.maximum(np.maximum(local, inner) * 0.95, _luma(bd))
    rim = band & (lp0 > lim)
    rimw = nd.gaussian_filter(rim.astype(np.float32), 1.5)[..., None]
    bg_share = np.maximum(bg_share, rimw * bg)
    p = p * (1 - bg_share) + bd * bg_share
    if edge:
        # halos: in the band just inside the sitter's edge (edge pixels of
        # the final tile wide), anything lighter than both the sitter's
        # inside nearby and the ground is the old background showing
        # through: darkened to the ground
        k = max(1, int(round(edge * n / 230)))
        core = mask > 0.5
        inner = nd.binary_erosion(core, iterations=k)
        band = core & ~inner
        lq = _luma(np.clip(p, 0, 1))
        size = max(5, 3 * k)
        num = nd.uniform_filter(lq * inner, size)
        den = nd.uniform_filter(inner.astype(np.float32), size)
        interior = np.where(den > 0.02, num / (den + 1e-6), 0)
        lim = np.maximum(interior, _luma(bd))
        amt = np.clip((lq - lim) / 0.08, 0, 1) * band
        # also just outside the edge, where the soft edge still holds wall
        outer = nd.binary_dilation(core, iterations=k) & ~core
        amt = np.maximum(amt, np.clip((lq - _luma(bd) - 0.03) / 0.08, 0, 1) * outer)
        amt = nd.gaussian_filter(amt, 1.0)[..., None]
        # (toward the ground as it is just outside, not the bare backdrop,
        # which is darker and would draw a dark line round the sitter)
        out = (mask < 0.1).astype(np.float32)
        gsize = max(9, 6 * k)
        gnum = nd.uniform_filter(p * out[..., None], (gsize, gsize, 1))
        gden = nd.uniform_filter(out, gsize)[..., None]
        ground = np.where(gden > 0.02, gnum / (gden + 1e-6), bd)
        target = np.minimum(ground, np.maximum(bd, ground))
        p = p * (1 - amt) + target * amt
    if green is not None:
        # (a green field, like Abu Bakr's roundel: dimmed and turned toward
        # an aged verdigris in the umber key)
        lg = _luma(np.clip(p, 0, 1))[..., None]
        isg = np.clip((p[..., 1] - np.maximum(p[..., 0], p[..., 2])) * 20, 0, 1)[..., None]
        verd = lg * np.array([1.0, 0.9, 0.62], np.float32) * 0.7
        p = p * (1 - isg) + (p * green + verd * (1 - green)) * isg
    # brush texture: faint strokes along the forms on the subject, a
    # crisscrossed brushed ground behind
    lp = _luma(np.clip(p, 0, 1))
    tex_fig = brush_texture(lp, n, seed)
    tex_bg = brush_texture(lp, n, seed + 1, length=max(10, n // 14), direction=0.9, blur=1.5)
    # (strokes on the sitter only where the source has detail: smooth skin
    # gets none, so no streaks across cheeks)
    det = nd.gaussian_filter(np.hypot(nd.sobel(_luma(a), 0), nd.sobel(_luma(a), 1)), 3)
    det = np.clip(det / (np.percentile(det, 75) + 1e-6), 0, 1)
    p = p * (1 + (tex_fig * 0.010 * texture * det * mask + tex_bg * 0.018 * (1 - mask)) * strength)[..., None]
    # impasto: the strokes catch the light from the upper left a little
    height = nd.gaussian_filter(tex_fig * mask + tex_bg * (1 - mask), 0.8)
    shade = nd.sobel(height, 1) * -0.7 + nd.sobel(height, 0) * -0.7
    p = p * (1 + 0.010 * strength * np.clip(shade, -3, 3))[..., None]
    # canvas weave
    y, x = np.mgrid[0:n, 0:n].astype(np.float32)
    weave = np.sin(x * 2.1) * np.sin(y * 2.1)
    p = p * (1 + 0.005 * strength * weave)[..., None]
    return np.clip(p, 0, 1)
