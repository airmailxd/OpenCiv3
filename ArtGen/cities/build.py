"""Builds the remade city sprites (Art/Cities) into C7/ModernArt.

    python ArtGen/cities/build.py [culture ...] [--preview] [--only=era,size]
"""
import os
import sys
import time
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, '..', 'common'))
import civ3art  # noqa: E402
import city  # noqa: E402
import styles  # noqa: E402

CULTURES = ['MIDEAST', 'EURO', 'ROMAN', 'ASIAN', 'AMER']

# Each cell's mean saturation is kept at most this fraction of its original's.
MUTE = 0.85


def _mean_sat(rgb, a):
    mx, mn = rgb.max(-1), rgb.min(-1)
    m = (a > 0.78) & (mx > 0.12)
    return float(((mx - mn) / np.maximum(mx, 1e-6))[m].mean()) if m.any() else 0.0


def mute(rgba, sheet, cells):
    """Pulls each cell's colors toward grey (keeping brightness) until its mean
    saturation is at most MUTE times the original cell's. cells: (x, y, w, h) at 1x."""
    orig = civ3art.load_original(sheet, shadows=True) / 255.0
    out = rgba.copy()
    for x, y, w, h in cells:
        o = orig[y:y + h, x:x + w]
        cell = out[2 * y:2 * (y + h), 2 * x:2 * (x + w)]
        a = cell[..., 3] / 255.0
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


def original_mask(sheet, x, y, w=166, h=95):
    a = civ3art.load_original(sheet)[y:y + h, x:x + w, 3] > 0
    return np.repeat(np.repeat(a, 2, 0), 2, 1)


def build_culture(name, preview=True, only=None, ss=4):
    eras = getattr(styles, name)
    sheet = f'Art/Cities/r{name}.pcx'
    W, H = civ3art.original_size(sheet)
    out = np.zeros((H * 2, W * 2, 4))
    for e, style in enumerate(eras):
        for s in range(3):
            if only and (e, s) not in only:
                continue
            t = time.time()
            mask = original_mask(sheet, s * 166, e * 95)
            style.setdefault('hscale', [1.1, 1.15, 1.3, 1.35][e])
            img, fit = city.render_cell(style, s, mask, seed=1000 * e + 10 * s + sum(map(ord, name)) % 7, ss=ss)
            out[e * 190:(e + 1) * 190, s * 332:(s + 1) * 332] = img
            print(f'{name} era {e} size {s}: coverage {fit.coverage():.2f}, {time.time() - t:.1f}s', flush=True)
    rgba = (np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8)
    rgba = mute(rgba, sheet, [(s * 166, e * 95, 166, 95) for e in range(len(eras)) for s in range(3)])
    if not only:
        civ3art.save_modern(sheet, rgba)
    if preview:
        orig = civ3art.upscale_nearest(civ3art.load_original(sheet))
        civ3art.save_preview(f'cities_{name}', orig, rgba, background=(84, 112, 52))
        for e in range(len(eras)):
            o = orig[e * 190:(e + 1) * 190]
            m = rgba[e * 190:(e + 1) * 190]
            both = np.concatenate([o, np.zeros((6,) + o.shape[1:], np.uint8), m], 0)
            civ3art.save_preview(f'cities_{name}_era{e}', both, background=(84, 112, 52))
    return rgba


def build_walls(name, preview=True, ss=4):
    import buildings as bd
    eras = getattr(styles, name)
    sheet = f'Art/Cities/{name}WALL.pcx'
    W, H = civ3art.original_size(sheet)
    out = np.zeros((H * 2, W * 2, 4))
    R = 54.0
    for e, style in enumerate(eras):
        col, mat, towers, cren = styles.WALLS[name][e]
        style.setdefault('hscale', [1.1, 1.15, 1.3, 1.35][e])
        wh = 12.0 if e < 2 else 14.0

        def pre(sc, fit, P, rng, col=col, mat=mat, towers=towers, cren=cren, wh=wh):
            bd.wall_ring(sc, R, wh, col, mat, P, rng, towers=towers, gate=True, cren=cren)
            t = 6.0
            for r in ((-R - 6, -R - 6, R + 6, -R + t), (-R - 6, R - t, R + 6, R + 6),
                      (-R - 6, -R - 6, -R + t, R + 6), (R - t, -R - 6, R + 6, R + 6)):
                fit.rects.append(r)
        mask = original_mask(sheet, 0, e * 95)
        img, fit = city.render_cell(style, 0, mask, seed=500 + 1000 * e + sum(map(ord, name)) % 7, ss=ss,
                                    pre=pre, bounds=(-R + 5, -R + 5, R - 5, R - 5))
        out[e * 190:(e + 1) * 190, 0:332] = img
        print(f'{name} walls era {e}: {fit.coverage():.2f}', flush=True)
    rgba = (np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8)
    rgba = mute(rgba, sheet, [(0, e * 95, 166, 95) for e in range(len(eras))])
    civ3art.save_modern(sheet, rgba)
    if preview:
        orig = civ3art.upscale_nearest(civ3art.load_original(sheet))
        civ3art.save_preview(f'walls_{name}', orig, rgba, background=(84, 112, 52))
    return rgba


def build_ruins(preview=True, ss=4):
    sheet = 'Art/Cities/DESTROY.pcx'
    W, H = civ3art.original_size(sheet)
    out = np.zeros((H * 2, W * 2, 4))
    for s in range(3):
        mask = original_mask(sheet, s * 167, 0, w=166)
        img, fit = city.render_cell(styles.RUINS, s, mask, seed=77 + s, ss=ss)
        out[:, s * 334:s * 334 + 332] = img
        print(f'ruins {s}: {fit.coverage():.2f}', flush=True)
    rgba = (np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8)
    rgba = mute(rgba, sheet, [(s * 167, 0, 166, H) for s in range(3)])
    civ3art.save_modern(sheet, rgba)
    if preview:
        orig = civ3art.upscale_nearest(civ3art.load_original(sheet))
        both = np.concatenate([orig, np.zeros((6,) + orig.shape[1:], np.uint8), rgba], 0)
        civ3art.save_preview('ruins', both, background=(84, 112, 52))
    return rgba


if __name__ == '__main__':
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    only = None
    for a in sys.argv[1:]:
        if a.startswith('--only='):
            only = [tuple(int(v) for v in p.split(',')) for p in a[7:].split(';')]
    for name in args or CULTURES + ['WALLS', 'RUINS']:
        if name == 'RUINS':
            build_ruins()
        elif name == 'WALLS':
            for c in CULTURES:
                build_walls(c)
        elif name.endswith('WALL'):
            build_walls(name[:-4])
        elif hasattr(styles, name):
            build_culture(name, only=only)
