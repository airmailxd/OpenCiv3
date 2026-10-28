"""Shared helpers for the Civilopedia icons: paths, compositing, backdrops,
the small icons and saving.

Every icon is an opaque square picture (like Civ3's Civilopedia icons): a
large 128x128 one and a small 32x32 one, drawn at 4x and box-filtered down
where it helps.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ARTGEN = os.path.dirname(HERE)
REPO = os.path.dirname(ARTGEN)
sys.path.insert(0, os.path.join(ARTGEN, 'common'))
sys.path.insert(0, os.path.join(ARTGEN, 'units'))
sys.path.append(os.path.join(ARTGEN, 'cities'))

PEDIA = os.path.join(REPO, 'C7', 'Civilopedia')
ICONS_REL = 'Art/Civilopedia/Icons'
ICONS = os.path.join(PEDIA, *ICONS_REL.split('/'))
MODERN = os.path.join(REPO, 'C7', 'ModernArt')
PREVIEWS = os.path.join(ARTGEN, '_previews', 'civilopedia')
RULESET = os.path.join(REPO, 'C7', 'Lua', 'civ3', 'ruleset.json')
MDJ_RULESET = os.path.join(REPO, 'C7', 'Lua', 'mdj', 'ruleset.json')

LARGE, SMALL = 128, 32

# The standalone civ colors (C7/Lua/standalone/textures.lua), by color index.
CIV_COLORS = [
    'F0F8FF', 'E6194B', 'F58231', 'FFE119', '3CB44B', '4363D8', '000075', 'FABED4',
    '911EB4', '9A6324', 'AAFFC3', '42D4F4', 'F032E6', '808000', 'DCBEFF', 'A9A9A9',
    '008080', 'FFD700', '800000', '00FF00', 'FFC0CB', '4682B4', 'D2B48C', 'FF7F50',
    '6A5ACD', '2E8B57', 'DAA520', 'C71585', '556B2F', '8B4513', 'B0C4DE', '696969',
]


def hex_rgb(h):
    h = h.lstrip('#')
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], np.float32) / 255


def load_ruleset():
    """The bundled rules: Civ3's, and the entries of MDJ's (C7/Lua/mdj) Civ3's
    haven't got (one icon per entry: MDJ's Masonry is Civ3's), with MDJ's
    resources pictured as standalone mode pictures them (see
    C7/Lua/standalone/ruleset.lua)."""
    import json
    with open(RULESET, encoding='utf-8') as f:
        rules = json.load(f)
    if os.path.exists(MDJ_RULESET):
        with open(MDJ_RULESET, encoding='utf-8') as f:
            mdj = json.load(f)
        own_icons = {'GOOD_Jade': 26, 'GOOD_Sake': 27}
        for r in mdj.get('resources', []):
            if r.get('civilopediaEntry') in own_icons:
                r['icon'] = own_icons[r['civilopediaEntry']]
        for section in ('unitPrototypes', 'techs', 'buildings', 'governments', 'resources', 'civilizations'):
            have = {e.get('civilopediaEntry') for e in rules.get(section, [])}
            for e in mdj.get(section, []):
                key = e.get('civilopediaEntry')
                if key and key not in have and not (section == 'techs' and e.get('eraCivilopediaName') == 'Hidden'):
                    have.add(key)
                    rules[section].append(e)
    return rules


def file_stem(name):
    """A file name for an entry: letters, digits and underscores."""
    out = []
    for ch in name:
        out.append(ch if ch.isalnum() else '_')
    s = ''.join(out)
    while '__' in s:
        s = s.replace('__', '_')
    return s.strip('_')


# ---------------------------------------------------------------- color

def mute(rgb, sat=0.8, val=1.0):
    """Lowers an sRGB color's saturation (0..1 floats) and scales its value."""
    rgb = np.asarray(rgb, np.float32)
    lum = rgb @ np.array([0.3, 0.55, 0.15], np.float32)
    return np.clip((lum + (rgb - lum) * sat) * val, 0, 1)


def mix(a, b, t):
    return np.asarray(a, np.float32) * (1 - t) + np.asarray(b, np.float32) * t


def over(top, bottom):
    """Straight-alpha float RGBA `top` over `bottom` (HxWx4, 0..1)."""
    ta = top[..., 3:4]
    ba = bottom[..., 3:4]
    a = ta + ba * (1 - ta)
    rgb = (top[..., :3] * ta + bottom[..., :3] * ba * (1 - ta)) / np.maximum(a, 1e-6)
    return np.concatenate([rgb, a], -1)


def to_float(img):
    if isinstance(img, Image.Image):
        img = np.asarray(img.convert('RGBA'))
    return np.asarray(img, np.float32) / 255


def to_image(rgba):
    a = np.clip(np.asarray(rgba, np.float32), 0, 1)
    if a.shape[-1] == 3:
        a = np.concatenate([a, np.ones(a.shape[:2] + (1,), np.float32)], -1)
    return Image.fromarray((a * 255 + 0.5).astype(np.uint8), 'RGBA')


def downsample(rgba, f):
    """Box filter (premultiplied) by an integer factor."""
    H, W = rgba.shape[:2]
    pm = rgba[..., :3] * rgba[..., 3:4]
    pm = pm.reshape(H // f, f, W // f, f, 3).mean((1, 3))
    a = rgba[..., 3].reshape(H // f, f, W // f, f).mean((1, 3))
    rgb = pm / np.maximum(a, 1e-6)[..., None]
    return np.concatenate([rgb, a[..., None]], -1)


def resize(rgba, size):
    """High quality resize of a float RGBA image (premultiplied Lanczos)."""
    img = np.asarray(rgba, np.float32)
    pm = np.concatenate([img[..., :3] * img[..., 3:4], img[..., 3:4]], -1)
    chans = []
    for c in range(4):
        im = Image.fromarray(pm[..., c], 'F').resize(size, Image.LANCZOS)
        chans.append(np.asarray(im, np.float32))
    out = np.stack(chans, -1)
    a = np.clip(out[..., 3], 0, 1)
    rgb = np.clip(out[..., :3] / np.maximum(a, 1e-6)[..., None], 0, 1)
    return np.concatenate([rgb, a[..., None]], -1)


def sharpen(rgba, amount=0.5, radius=0.8):
    from scipy import ndimage
    out = rgba.copy()
    for c in range(3):
        bl = ndimage.gaussian_filter(rgba[..., c], radius)
        out[..., c] = np.clip(rgba[..., c] + (rgba[..., c] - bl) * amount, 0, 1)
    return out


def blur(a, r):
    from scipy import ndimage
    if r <= 0:
        return a
    return ndimage.gaussian_filter(np.asarray(a, np.float32), r, mode='nearest')


# ---------------------------------------------------------------- backdrops

def vgradient(n, top, bottom, gamma=1.0):
    t = (np.linspace(0, 1, n, dtype=np.float32) ** gamma)[:, None, None]
    return np.broadcast_to(mix(top, bottom, t), (n, n, 3)).copy()


def radial(n, cx, cy, rx, ry=None):
    ry = rx if ry is None else ry
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32) + 0.5
    return np.sqrt(((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2)


def vignette(rgb, strength=0.18, n=None):
    n = rgb.shape[0]
    d = radial(n, n / 2, n / 2, n * 0.72)
    return rgb * (1 - strength * np.clip(d, 0, 1.4) ** 2)[..., None]


def noise(n, scale, seed, octaves=3):
    """Smooth value noise in 0..1 (n x n)."""
    rng = np.random.default_rng(seed)
    out = np.zeros((n, n), np.float32)
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        cells = max(2, int(n / scale * 2 ** o))
        g = rng.random((cells + 1, cells + 1)).astype(np.float32)
        im = Image.fromarray(g, 'F').resize((n, n), Image.BICUBIC)
        out += np.asarray(im) * amp
        tot += amp
        amp *= 0.5
    return np.clip(out / tot, 0, 1)


def frame_edges(rgb, dark=0.22, light=0.10):
    """A subtle bevel on the picture's edge: light top/left, dark bottom/right."""
    n = rgb.shape[0]
    out = rgb.copy()
    w = max(1, n // 64)
    out[:w, :] = out[:w, :] * (1 - light) + light
    out[:, :w] = out[:, :w] * (1 - light) + light
    out[-w:, :] *= 1 - dark
    out[:, -w:] *= 1 - dark
    return out


# ---------------------------------------------------------------- output

def small_from_large(large_rgba, sharpen_amount=0.6):
    """The 32x32 icon from a 128x128 picture."""
    s = resize(large_rgba, (SMALL, SMALL))
    return sharpen(s, sharpen_amount, 0.6)


def save_png(rgba, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img = to_image(rgba) if not isinstance(rgba, Image.Image) else rgba
    if img.mode == 'RGBA' and img.getextrema()[3][0] == 255:
        img = img.convert('RGB')
    img.save(path, optimize=True)


def save_icon(section, stem, large, small=None):
    """Writes <section>/<stem>_large.png and _small.png; returns their paths
    relative to C7/Civilopedia."""
    if small is None:
        small = small_from_large(large)
    assert large.shape[:2] == (LARGE, LARGE), (stem, large.shape)
    assert small.shape[:2] == (SMALL, SMALL), (stem, small.shape)
    rel_l = f'{ICONS_REL}/{section}/{stem}_large.png'
    rel_s = f'{ICONS_REL}/{section}/{stem}_small.png'
    save_png(large, os.path.join(PEDIA, *rel_l.split('/')))
    save_png(small, os.path.join(PEDIA, *rel_s.split('/')))
    return rel_l, rel_s


def contact_sheet(name, items, cols=8, label=True):
    """items: list of (title, large rgba, small rgba). Writes
    ArtGen/_previews/civilopedia/<name>.png: each large icon with its small
    one (at 1x and 2x) under it."""
    from PIL import ImageDraw
    cw, ch = LARGE + 8, LARGE + SMALL * 2 + 22
    rows = (len(items) + cols - 1) // cols
    sheet = Image.new('RGB', (cols * cw + 8, rows * ch + 8), (226, 214, 186))
    d = ImageDraw.Draw(sheet)
    for i, (title, lg, sm) in enumerate(items):
        x, y = 8 + (i % cols) * cw, 8 + (i // cols) * ch
        sheet.paste(to_image(lg).convert('RGB'), (x, y))
        s = to_image(sm).convert('RGB')
        sheet.paste(s, (x, y + LARGE + 4))
        sheet.paste(s.resize((SMALL * 2, SMALL * 2), Image.NEAREST), (x + SMALL + 6, y + LARGE + 4))
        if label:
            d.text((x, y + LARGE + SMALL * 2 + 6), title[:22], fill=(40, 30, 20))
    os.makedirs(PREVIEWS, exist_ok=True)
    path = os.path.join(PREVIEWS, name + '.png')
    sheet.save(path)
    return path
