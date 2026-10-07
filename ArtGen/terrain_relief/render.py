"""Rendering toolkit for the relief and vegetation overlays.

Everything is drawn at SS (4) pixels per original pixel, which is twice the
size of the final art, and box-filtered down by 2 at the end for antialiasing.
Images are premultiplied float RGBA (HxWx4, 0..1) while being drawn.
"""
import os
import sys

import numpy as np
import scipy.ndimage as ndi

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'common'))
import civ3art  # noqa: E402

SS = 4            # render pixels per original pixel
DOWN = SS // civ3art.SCALE  # box-filter factor from render size to final size

# Light from the upper left (northwest), high in the sky. Screen space: x right,
# y down, z towards the viewer.
LIGHT = np.array([-0.55, -0.62, 0.86])
LIGHT = LIGHT / np.linalg.norm(LIGHT)


# ---------------------------------------------------------------- noise

def value_noise(shape, cell, seed, aniso=(1.0, 1.0)):
    """Smooth value noise in 0..1 with features about `cell` pixels wide
    (stretched by aniso=(y, x))."""
    h, w = shape
    cy, cx = cell * aniso[0], cell * aniso[1]
    gh, gw = int(h / cy) + 5, int(w / cx) + 5
    g = np.random.default_rng(seed).random((gh, gw))
    z = ndi.zoom(g, (cy, cx), order=3, mode='nearest', grid_mode=False)
    oy, ox = int(cy * 2), int(cx * 2)
    return z[oy:oy + h, ox:ox + w]


def fbm(shape, cell, seed, octaves=4, gain=0.5, aniso=(1.0, 1.0), ridged=False):
    """Fractal noise, roughly 0..1."""
    total = np.zeros(shape)
    amp, norm = 1.0, 0.0
    for o in range(octaves):
        n = value_noise(shape, max(cell / 2 ** o, 1.5), seed * 31 + o * 7 + 1, aniso)
        if ridged:
            n = 1.0 - np.abs(2.0 * n - 1.0)
            n = n * n
        total += amp * n
        norm += amp
        amp *= gain
    return total / norm


# ---------------------------------------------------------------- color

def srgb(c):
    return np.asarray(c, float) / 255.0


def saturate(rgb, amount=1.1):
    rgb = np.asarray(rgb, float)
    lum = rgb[..., :3] @ np.array([0.299, 0.587, 0.114])
    return np.clip(lum[..., None] + (rgb - lum[..., None]) * amount, 0, 1)


def lerp(a, b, t):
    t = np.asarray(t, float)
    if t.ndim and np.ndim(a) and np.shape(a)[-1] == 3 and t.shape[-1:] not in ((3,), (1,)):
        t = t[..., None]
    return a + (b - a) * t


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def median_color(rgba, mask, default=(128, 128, 128)):
    sel = rgba[..., :3][mask & (rgba[..., 3] > 0)]
    if len(sel) < 8:
        return srgb(default)
    return srgb(np.median(sel, 0))


# ---------------------------------------------------------------- masks

def upsample(a, f=SS):
    return np.repeat(np.repeat(np.asarray(a, float), f, 0), f, 1)


def smooth_mask(alpha_bool, sigma=0.7):
    """The original's footprint at render size, smoothed: 0..1 with the edge at 0.5."""
    return ndi.gaussian_filter(upsample(alpha_bool), sigma * SS)


def coverage(m, width=1.0):
    """Antialiased coverage of the region m > 0.5 (about `width` render px of ramp)."""
    gy, gx = np.gradient(m)
    g = np.maximum(np.hypot(gx, gy), 1e-4)
    return np.clip((m - 0.5) / (g * width) + 0.5, 0, 1)


def shift(a, dy, dx):
    out = np.zeros_like(a)
    h, w = a.shape[:2]
    ys, yd = (slice(0, h - dy), slice(dy, h)) if dy >= 0 else (slice(-dy, h), slice(0, h + dy))
    xs, xd = (slice(0, w - dx), slice(dx, w)) if dx >= 0 else (slice(-dx, w), slice(0, w + dx))
    out[yd, xd] = a[ys, xs]
    return out


# ---------------------------------------------------------------- shading

def normals(height, strength=1.0):
    gy, gx = np.gradient(height)
    n = np.stack([-gx * strength, -gy * strength, np.ones_like(height)], -1)
    return n / np.linalg.norm(n, axis=-1, keepdims=True)


def lambert(n, light=LIGHT):
    return np.clip(n @ light, 0, 1)


def cavity(height, sigma):
    """Positive in hollows, negative on ridges (for ambient occlusion)."""
    return ndi.gaussian_filter(height, sigma) - height


# ---------------------------------------------------------------- compositing

def premul(rgb, a):
    return np.concatenate([rgb * a[..., None], a[..., None]], -1)


def over(dst, src):
    """src over dst, both premultiplied (in place on dst)."""
    dst *= (1.0 - src[..., 3:4])
    dst += src
    return dst


def stamp(canvas, spr, x, y):
    """Composites premultiplied sprite `spr` over `canvas` with its top-left at (x, y)."""
    h, w = spr.shape[:2]
    H, W = canvas.shape[:2]
    x0, y0 = max(x, 0), max(y, 0)
    x1, y1 = min(x + w, W), min(y + h, H)
    if x0 >= x1 or y0 >= y1:
        return
    s = spr[y0 - y:y1 - y, x0 - x:x1 - x]
    d = canvas[y0:y1, x0:x1]
    d *= (1.0 - s[..., 3:4])
    d += s


def shadow_stamp(shadow, spr_alpha, x, y):
    """Accumulates a shadow alpha (max-ish union) into `shadow`."""
    h, w = spr_alpha.shape
    H, W = shadow.shape
    x0, y0 = max(x, 0), max(y, 0)
    x1, y1 = min(x + w, W), min(y + h, H)
    if x0 >= x1 or y0 >= y1:
        return
    s = spr_alpha[y0 - y:y1 - y, x0 - x:x1 - x]
    d = shadow[y0:y1, x0:x1]
    d[...] = 1 - (1 - d) * (1 - s)


def downsample(canvas, f=DOWN):
    """Box-filters a premultiplied render down by f and returns straight-alpha uint8 RGBA."""
    h, w = canvas.shape[:2]
    c = canvas.reshape(h // f, f, w // f, f, 4).mean((1, 3))
    a = c[..., 3:4]
    rgb = np.where(a > 1e-5, c[..., :3] / np.maximum(a, 1e-5), 0)
    out = np.concatenate([rgb, a], -1)
    return (np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8)


def to_png_preview(rgba_u8, bg=(112, 140, 80)):
    """Flattens onto a flat ground color for previews."""
    a = rgba_u8[..., 3:4] / 255.0
    out = rgba_u8[..., :3] * a + np.array(bg) * (1 - a)
    return np.concatenate([out, np.full(a.shape, 255)], -1).astype(np.uint8)
