"""From Blender's passes to the game's unit files (runs in the ArtGen venv).

The Blender side (bscene.render_directions) writes, per direction row, on a
canvas twice the size of the game's 240x240 unit canvas:

    unit_<row>.png    the unit, RGBA (straight alpha), sRGB
    tint_<row>.png    the `tint` AOV: the part of each pixel in the civ color
    shadow_<row>.png  the shadow catcher pass (1 = no shadow)

compose() turns them into the game's two layers, like the old software
renderer (ArtGen/units/render.py) did: the base layer (the unit's own
colors, a soft dark contour and the ground shadow) and the tint layer (gray
shading where the civ color goes, drawn over the base). write_unit() then
writes the sheets: for every flic of the unit, eight rows, each the flic's
cell cut from the canvas, and the unit's metadata. A flic has one column (the
still stands for every frame, see UnitArtMetadata.SheetColumn), or, when
render_anim.py rendered it (anim/<flic>/f<frame>/), a column per frame.
"""
import json
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'units'))

from render import Style, _blur, _blur_rgb, _maxfilter, _u8  # noqa: E402

CANVAS = 240          # the game's unit canvas (1x)
SCALE = 2             # the sheets have twice the pixels
SHADOW_GAIN = 0.38    # shadow catcher darkness -> shadow alpha (full shadow ~ 0.4)
CAVITY = 0.55         # how much the short-distance AO darkens crevices
SHARPEN = 0.6         # unsharp mask (local contrast) amount, radius ~1 px at 2x


def _load(path):
    a = np.asarray(Image.open(path)).astype(np.float32)
    return a / (65535.0 if a.max() > 255 else 255.0)


def _linear(x):
    return np.where(x <= 0.04045, x / 12.92, ((x + 0.055) / 1.055) ** 2.4)


def _gray(a):
    return a[..., 0] if a.ndim == 3 else a


def compose(folder, row, desat=1.0, style=Style):
    """The base and tint layers (uint8 RGBA, canvas size) of one direction."""
    u = _load(os.path.join(folder, f'unit_{row:04d}.png'))
    rgb, a = u[..., :3], u[..., 3]
    # the originals' local contrast: dark crevices between parts and crisp
    # small-scale shading (smooth mid-tone gradients read as CG)
    ao_path = os.path.join(folder, f'ao_{row:04d}.png')
    if os.path.exists(ao_path):
        ao = np.clip(_linear(_gray(_load(ao_path))), 0, 1)
        rgb = rgb * (1 - CAVITY + CAVITY * ao)[..., None]
    if SHARPEN:
        soft = _blur_rgb(rgb * a[..., None], 1.0) / np.maximum(_blur(a, 1.0), 1e-6)[..., None]
        rgb = np.clip(rgb + SHARPEN * (rgb - soft) * (a > 0.5)[..., None], 0, 1)
    t = _linear(_gray(_load(os.path.join(folder, f'tint_{row:04d}.png'))))
    s = _linear(_gray(_load(os.path.join(folder, f'shadow_{row:04d}.png'))))
    a_t = np.clip(np.minimum(t, a), 0, 1)
    a_b = np.clip(a - a_t, 0, 1)
    # tinted parts render white: where a pixel is only partly tinted (the
    # anti-aliased edge of a civ-color part), its color still holds some of
    # that white, which showed as a white piping round every tinted part.
    # Those pixels take the color of their untinted neighbors instead.
    rgb0 = rgb
    f = a_t / np.maximum(a, 1e-6)
    edge = (f > 0.02) & (a > 0.02)
    if edge.any():
        pure = ((f <= 0.02) & (a > 0.5)).astype(np.float32)
        w = _blur(pure, 2.0)
        near_b = _blur_rgb(rgb * pure[..., None], 2.0) / np.maximum(w, 1e-6)[..., None]
        fill = np.where((w > 0.02)[..., None], near_b, rgb * 0.35)
        rgb = np.where(edge[..., None], fill, rgb)
    if desat != 1.0:
        lum = rgb.mean(-1, keepdims=True)
        rgb = np.clip(lum + (rgb - lum) * desat, 0, 1)
    c_b = rgb * a_b[..., None]                       # premultiplied
    tgray = rgb0.mean(-1)                            # the tint layer is gray shading
    c_t = tgray * a_t

    # outer contour: a soft dark ring just outside the silhouette
    a_all = np.clip(a_b + a_t, 0, 1)
    ring = np.clip(_maxfilter(a_all, 1) - a_all, 0, 1)
    ring = np.minimum(ring, _blur(a_all, 0.7) * 2.0)
    col_all = (c_b + c_t[..., None] * 0.5) / np.maximum(a_all, 1e-6)[..., None]
    near = _blur_rgb(col_all * a_all[..., None], 1.0) / np.maximum(_blur(a_all, 1.0), 1e-6)[..., None]
    ring_a = ring * style.contour_alpha
    ring_c = near * style.contour_dark

    # ground shadow, from the shadow catcher, only where the unit isn't
    # (the shadow catcher pass is noisy: soften it, as the old renderer blurred its shadow)
    sh = np.clip(_blur((1.0 - s) * SHADOW_GAIN, 1.6), 0, style.shadow_alpha + 0.05)

    rest = np.clip(1 - a_all, 0, 1)
    under_a = 1 - (1 - ring_a) * (1 - sh)
    under_c = ring_c * ring_a[..., None] / np.maximum(under_a, 1e-6)[..., None]
    base_a = a_b + under_a * rest
    base_cp = c_b + under_c * (under_a * rest)[..., None]
    k = 1.0 / np.maximum(1 - a_t, 1e-6)
    base_a2 = np.where(a_t > 0.995, 0, np.clip(base_a * k, 0, 1))
    base_c2 = np.where((a_t > 0.995)[..., None], 0, base_cp * k[..., None])
    base = np.zeros(a.shape + (4,), np.float32)
    base[..., :3] = base_c2 / np.maximum(base_a2, 1e-6)[..., None]
    base[..., 3] = base_a2
    tint = np.zeros(a.shape + (4,), np.float32)
    tint[..., :3] = (c_t / np.maximum(a_t, 1e-6))[..., None]
    tint[..., 3] = a_t
    return _u8(base), _u8(tint)


def _bbox(alpha, threshold=4):
    ys, xs = np.nonzero(alpha > threshold)
    if not len(xs):
        return None
    return xs.min(), ys.min(), xs.max() + 1, ys.max() + 1


def _extent(frames, margin):
    """The sprite's extent over all frames and directions (canvas pixels at 2x)."""
    boxes = [b for layers in frames for b in (_bbox(np.maximum(base[..., 3], tint[..., 3])) for base, tint in layers) if b]
    return (min(b[0] for b in boxes) - margin * SCALE, min(b[1] for b in boxes) - margin * SCALE,
            max(b[2] for b in boxes) + margin * SCALE, max(b[3] for b in boxes) + margin * SCALE)


def anim_frames(anim_dir, name):
    """The frame folders render_anim.py wrote for flic `name`, in order, or []."""
    d = os.path.join(anim_dir, name) if anim_dir else None
    if not d or not os.path.isdir(d):
        return []
    return [os.path.join(d, f) for f in sorted(os.listdir(d)) if f.startswith('f')]


def write_unit(unit, folder, calibration, out_dir, desat=1.0, margin=2, anim_dir=None):
    """Writes the unit's sheets and metadata into out_dir/<unit>/.
    `calibration` is the unit's calibration JSON (its flics, with headers);
    `anim_dir` holds render_anim.py's frames of the animated flics."""
    still = [[compose(folder, row, desat) for row in range(8)]]
    os.makedirs(out_dir, exist_ok=True)
    meta = {'actions': {}, 'flics': {}}
    grown = []
    for flic_file, f in sorted(calibration['flics'].items()):
        name = os.path.splitext(flic_file)[0]
        for action in f['actions']:
            meta['actions'][action] = name
        dirs = anim_frames(anim_dir, name)
        frames = [[compose(d, row, desat) for row in range(8)] for d in dirs] if dirs else still
        x0, y0, x1, y1 = _extent(frames, margin)
        # the flic's cell, grown (in whole 1x pixels) to hold the still
        l, t = f['offset_left'], f['offset_top']
        r, b = l + f['width'], t + f['height']
        L = min(l, x0 // SCALE)
        T = min(t, y0 // SCALE)
        R = max(r, -(-x1 // SCALE))
        Bm = max(b, -(-y1 // SCALE))
        L, T = int(max(0, L)), int(max(0, T))
        R, Bm = int(min(CANVAS, R)), int(min(CANVAS, Bm))
        if (L, T, R, Bm) != (l, t, r, b):
            grown.append(name)
        w, h = R - L, Bm - T
        box = (L * SCALE, T * SCALE, R * SCALE, Bm * SCALE)
        for kind, idx in (('', 0), ('_tint', 1)):
            sheet = Image.new('RGBA', (w * SCALE * len(frames), h * SCALE * 8))
            for col, layers in enumerate(frames):
                for row in range(8):
                    cell = Image.fromarray(layers[row][idx], 'RGBA').crop(box)
                    sheet.paste(cell, (col * w * SCALE, row * h * SCALE))
            sheet.save(os.path.join(out_dir, f'{name}{kind}.png'), optimize=True)
        meta['flics'][name] = {
            'size': [w, h], 'directions': f['directions'], 'frames': f['frames'],
            'frameMs': f['frameMs'], 'offset': [L, T], 'canvas': [CANVAS, CANVAS],
        }
    if 'DEFAULT' not in meta['actions']:
        raise ValueError(f'{unit}: no DEFAULT flic in its calibration')
    with open(os.path.join(out_dir, f'{unit}.json'), 'w', newline='\n') as fh:
        fh.write(_metadata_json(meta))
    return meta, grown


def _metadata_json(meta):
    """Like ArtGen/units/metadata.py: one line per flic."""
    lines = ['{', '  "actions": {']
    acts = list(meta['actions'].items())
    for i, (k, v) in enumerate(acts):
        lines.append(f'    "{k}": "{v}"' + (',' if i < len(acts) - 1 else ''))
    lines += ['  },', '  "flics": {']
    fl = list(meta['flics'].items())
    for i, (k, v) in enumerate(fl):
        lines.append(f'    "{k}": ' + json.dumps(v, separators=(', ', ': ')) + (',' if i < len(fl) - 1 else ''))
    lines += ['  }', '}', '']
    return '\n'.join(lines)
