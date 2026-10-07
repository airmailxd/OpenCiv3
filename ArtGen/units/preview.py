"""Side-by-side previews: the original (upscaled 2x) next to the remake.

    python preview.py Warrior                  # every flic, a few frames, all 8 directions
    python preview.py Warrior DEFAULT RUN      # just these actions
    python preview.py Warrior --frames=0,5,10 --dirs=1,2,3
    python preview.py Warrior --zoom=1         # also at the 1x size the game shows

Writes ArtGen/_previews/units/<unit>_<ACTION>.png: for each direction, a row
of [original | remake] pairs per frame, over a grass color, both with a
civ color applied to the tint layer like the game does.
"""
import os
import sys
import time

import numpy as np
from PIL import Image, ImageDraw

import flic as F
import civ3art
import catalog
import framework

BG = (96, 132, 62)
CIV = (60, 110, 235)


def tinted(base, tint, civ=CIV):
    """The game's look: base, then the tint layer multiplied by the civ color."""
    out = base.astype(np.float32) / 255
    t = tint.astype(np.float32) / 255
    tc = t[..., :3] * np.array(civ, np.float32) / 255
    a = t[..., 3:4]
    rgb = out[..., :3] * out[..., 3:4] * (1 - a) + tc * a
    alpha = out[..., 3:4] * (1 - a) + a
    res = np.concatenate([rgb / np.maximum(alpha, 1e-6), alpha], -1)
    return (res * 255).clip(0, 255).astype(np.uint8)


def original_frame(fl, d, f):
    idx = fl.frames[d, f]
    base, tm, sh = F.split_frame(idx, fl.palette)
    tint = np.zeros_like(base)
    lum = fl.palette[idx].astype(np.float32).max(-1)
    # the game uses the ntp00 palette shades; approximate with the flic's own brightness
    v = np.clip(lum / 255 * 1.25, 0, 1) * 255
    tint[tm, 0] = tint[tm, 1] = tint[tm, 2] = v[tm]
    tint[tm, 3] = 255
    shm = sh > 0
    base[shm] = 0
    base[shm, 3] = (sh[shm] * 255).astype(np.uint8)
    return tinted(base, tint)


def on_bg(rgba):
    bg = Image.new('RGBA', (rgba.shape[1], rgba.shape[0]), BG + (255,))
    return Image.alpha_composite(bg, Image.fromarray(rgba))


def preview(unit, actions=None, frames=None, dirs=None, zoom1=False, job=None, scale=1):
    entry = catalog.load()[unit]
    job = job or framework.UnitJob(unit, entry)
    out_paths = []
    for action, name in job.flics().items():
        if actions and action not in actions:
            continue
        fl = F.load_unit_flic(unit, name)
        fr = frames if frames else sorted(set(np.linspace(0, fl.n_frames - 1, min(fl.n_frames, 6)).round().astype(int)))
        fr = [f for f in fr if f < fl.n_frames]
        ds = dirs if dirs is not None else list(range(fl.n_anims))
        t0 = time.time()
        base, tint, _ = job.render_flic(action, name, rows=ds, cols=fr)
        dt = time.time() - t0
        W, H = fl.width * 2, fl.height * 2
        cells = []
        for d in ds:
            row = []
            for f in fr:
                o = civ3art.upscale_nearest(original_frame(fl, d, f))
                r = tinted(base[d * H:(d + 1) * H, f * W:(f + 1) * W], tint[d * H:(d + 1) * H, f * W:(f + 1) * W])
                row.append((o, r))
            cells.append(row)
        pad = 6
        cw = W * 2 + pad
        img = Image.new('RGBA', (len(fr) * (cw + 3 * pad), len(ds) * (H + pad) + 14), (40, 40, 40, 255))
        dr = ImageDraw.Draw(img)
        dr.text((2, 1), f'{unit} {action} {name}  {fl.n_frames}f x {fl.n_anims}d  {fl.width}x{fl.height}  '
                        f'(original | remake)  render {dt / max(1, len(ds) * len(fr)):.2f}s/frame', fill=(255, 255, 255, 255))
        for i, row in enumerate(cells):
            for j, (o, r) in enumerate(row):
                x = j * (cw + 3 * pad)
                y = 14 + i * (H + pad)
                img.alpha_composite(on_bg(o), (x, y))
                img.alpha_composite(on_bg(r), (x + W + pad // 2, y))
        if scale != 1:
            img = img.resize((img.width * scale, img.height * scale), Image.NEAREST)
        p = os.path.join(civ3art.PREVIEWS, 'units', f'{unit}_{action}.png')
        os.makedirs(os.path.dirname(p), exist_ok=True)
        img.save(p)
        out_paths.append(p)
        if zoom1:
            small = img.resize((img.width // 2, img.height // 2), Image.LANCZOS)
            small.save(p.replace('.png', '_1x.png'))
        print(p, f'{dt:.1f}s')
    return out_paths


def showcase(units, out='showcase', action='DEFAULT', dirs=(0, 1, 2, 3), frame=0, scale=2):
    """One image: for each unit a row of [original | remake] at a few
    directions (2x, then scaled up by `scale` for viewing)."""
    cat = catalog.load()
    rows = []
    for unit in units:
        job = framework.UnitJob(unit, cat[unit])
        name = job.flics()[action]
        fl = F.load_unit_flic(unit, name)
        base, tint, _ = job.render_flic(action, name, rows=list(dirs), cols=[frame])
        W, H = fl.width * 2, fl.height * 2
        cells = []
        for d in dirs:
            o = on_bg(civ3art.upscale_nearest(original_frame(fl, d, frame)))
            r = on_bg(tinted(base[d * H:(d + 1) * H, frame * W:(frame + 1) * W],
                             tint[d * H:(d + 1) * H, frame * W:(frame + 1) * W]))
            cells += [o, r]
        rows.append(cells)
    cw = max(c.width for r in rows for c in r)
    ch = max(c.height for r in rows for c in r)
    img = Image.new('RGBA', (len(rows[0]) * (cw + 4), len(rows) * (ch + 4)), (40, 40, 40, 255))
    for i, r in enumerate(rows):
        for j, c in enumerate(r):
            img.alpha_composite(c, (j * (cw + 4) + (cw - c.width) // 2, i * (ch + 4) + (ch - c.height) // 2))
    img = img.resize((img.width * scale, img.height * scale), Image.NEAREST)
    p = os.path.join(civ3art.PREVIEWS, 'units', f'{out}.png')
    img.save(p)
    print(p)
    return p


if __name__ == '__main__' and '--showcase' in sys.argv:
    us = [a for a in sys.argv[1:] if not a.startswith('--')]
    showcase(us)
    sys.exit(0)

if __name__ == '__main__':
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    opts = dict(a[2:].split('=', 1) if '=' in a else (a[2:], '1') for a in sys.argv[1:] if a.startswith('--'))
    frames = [int(v) for v in opts['frames'].split(',')] if 'frames' in opts else None
    dirs = [int(v) for v in opts['dirs'].split(',')] if 'dirs' in opts else None
    preview(args[0], args[1:] or None, frames, dirs, zoom1='zoom' in opts, scale=int(opts.get('scale', 1)))

