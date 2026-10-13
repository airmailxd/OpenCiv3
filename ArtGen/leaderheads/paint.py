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


def subject_mask(a):
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
    m = np.maximum(m, np.clip((busy - busy_bg * 1.6) / (busy_bg * 2 + 0.05), 0, 1))
    y, x = np.mgrid[0:n, 0:n].astype(np.float32)
    oval = np.exp(-(((x - n / 2) / (n * 0.30)) ** 2 + ((y - n * 0.50) / (n * 0.40)) ** 4))
    m = np.maximum(m, oval)
    # close small holes, pull the edge in a little (so no rim of the old
    # background is left around the sitter), then soften it
    m = nd.grey_closing(m, size=max(3, n // 30))
    m = nd.grey_erosion(m, size=max(3, n // 60))
    return np.clip(nd.gaussian_filter(m, n / 120), 0, 1)


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


def brush_texture(lum, n, seed, length=None, direction=None):
    """Noise smeared along the contours (or one direction): short strokes
    that follow forms."""
    rng = np.random.default_rng(seed)
    noise = rng.random((n, n)).astype(np.float32)
    noise = nd.gaussian_filter(noise, 0.7)
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


def paint(img, seed=0, strength=1.0):
    """The painted finish on a PIL RGB picture (returns float RGB 0..1)."""
    a = np.asarray(img, np.float32) / 255
    n = a.shape[0]
    mask = subject_mask(a)
    # strokes: light smoothing on the subject (the face keeps its detail),
    # broader in the background
    fine = kuwahara(a, max(2, n // 150))
    broad = kuwahara(a, max(4, n // 70))
    m3 = mask[..., None]
    # on the subject, half the stroke-smoothed picture and half the real one
    p = (fine * 0.6 + a * 0.4) * m3 + broad * (1 - m3)
    # one palette: a gradient map by brightness, with the picture's own
    # color kept on top (pictures in color stay in color, but share a key)
    lum = _luma(np.clip(p, 0, 1))
    mapped = _gradient_map(lum)
    chroma = p - lum[..., None]
    sat = float(np.abs(chroma).mean())
    if sat > 0.03:
        # the shared key, with the picture's own (somewhat calmer) color
        p = (mapped * 0.55 + lum[..., None] * 0.45) + chroma * 0.75
    else:                                        # black and white: all gradient map
        p = mapped
    # one backdrop
    bd = backdrop(n, seed)
    bg_share = (1 - mask)[..., None] * 0.92 * strength
    p = p * (1 - bg_share) + bd * bg_share
    # brush texture: faint strokes along the forms on the subject, a
    # crisscrossed brushed ground behind
    lp = _luma(np.clip(p, 0, 1))
    tex_fig = brush_texture(lp, n, seed)
    tex_bg = brush_texture(lp, n, seed + 1, length=max(6, n // 25), direction=0.9)
    p = p * (1 + (tex_fig * 0.022 * mask + tex_bg * 0.035 * (1 - mask)) * strength)[..., None]
    # impasto: the strokes catch the light from the upper left a little
    height = nd.gaussian_filter(tex_fig * mask + tex_bg * (1 - mask), 0.8)
    shade = nd.sobel(height, 1) * -0.7 + nd.sobel(height, 0) * -0.7
    p = p * (1 + 0.010 * strength * np.clip(shade, -3, 3))[..., None]
    # canvas weave
    y, x = np.mgrid[0:n, 0:n].astype(np.float32)
    weave = np.sin(x * 2.1) * np.sin(y * 2.1)
    p = p * (1 + 0.012 * strength * weave)[..., None]
    return np.clip(p, 0, 1)
