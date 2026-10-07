"""Shared helpers for the ArtGen scripts, which draw the remade ("modern") map art.

Reads the original Civ3 art for reference and writes the remade art into
C7/ModernArt, where the game's Graphics Overhaul setting picks it up. See
ArtGen/README.md for the rules every piece of remade art follows.
"""
import os
import numpy as np
from PIL import Image

SCALE = 2  # The remade art has twice the resolution of the original.

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
MODERN_ART = os.path.join(REPO, 'C7', 'ModernArt')
# Unit animations the game only plays with animations on. They're kept out of
# git (see C7/.gitignore) and the game uses them when they're present.
MODERN_ART_ANIMATIONS = os.path.join(REPO, 'C7', 'ModernArtAnimations')
PREVIEWS = os.path.join(REPO, 'ArtGen', '_previews')


def civ3_root():
    """The Civ3 install folder: $CIV3_HOME, else the registry's install path."""
    env = os.environ.get('CIV3_HOME')
    if env:
        return env
    import winreg
    for key in (r'SOFTWARE\WOW6432Node\Infogrames Interactive\Civilization III',
                r'SOFTWARE\Infogrames Interactive\Civilization III'):
        try:
            with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, key) as k:
                return winreg.QueryValueEx(k, 'Install_Path')[0]
        except OSError:
            pass
    raise RuntimeError('Civ3 not found: set CIV3_HOME')


def _find_ignoring_case(root, rel):
    path = root
    for part in rel.replace(chr(92), '/').split('/'):
        if not os.path.isdir(path):
            return None
        match = next((e for e in os.listdir(path) if e.lower() == part.lower()), None)
        if match is None:
            return None
        path = os.path.join(path, match)
    return path


def media_path(rel):
    """The original file the game uses for a media path such as 'Art/Terrain/xtgc.pcx'
    (Conquests first, then Play the World, then the base game, like the game does)."""
    root = civ3_root()
    for base in ('Conquests', 'civ3PTW', ''):
        found = _find_ignoring_case(root, f'{base}/{rel}' if base else rel)
        if found:
            return found
    raise FileNotFoundError(rel)


def read_pcx(path):
    """Returns (indices HxW uint8, palette 256x3 uint8) of an 8-bit PCX."""
    data = open(path, 'rb').read()
    x0, y0, x1, y1 = np.frombuffer(data[4:12], '<u2')
    w, h = int(x1 - x0 + 1), int(y1 - y0 + 1)
    bytes_per_line = int(np.frombuffer(data[66:68], '<u2')[0])
    out = bytearray(bytes_per_line * h)
    i, o, end = 128, 0, len(data) - 769
    while o < len(out) and i < end:
        b = data[i]; i += 1
        if b >= 0xC0:
            n = b & 0x3F
            v = data[i]; i += 1
            out[o:o + n] = bytes([v]) * n
            o += n
        else:
            out[o] = b; o += 1
    idx = np.frombuffer(bytes(out), np.uint8).reshape(h, bytes_per_line)[:, :w].copy()
    pal = np.frombuffer(data[-768:], np.uint8).reshape(256, 3).copy()
    return idx, pal


def load_original(rel, transparent=(254, 255), shadows=False):
    """The original art at a media path as an RGBA uint8 array (HxWx4), decoded
    like the game does: palette indexes 254 and 255 are transparent, and with
    shadows=True indexes 240-255 are black shadow of decreasing opacity."""
    idx, pal = read_pcx(media_path(rel))
    rgba = np.zeros(idx.shape + (4,), np.uint8)
    rgba[..., :3] = pal[idx]
    rgba[..., 3] = 255
    for t in transparent:
        rgba[idx == t, 3] = 0
    if shadows:
        for i in range(240, 256):
            m = idx == i
            rgba[m] = (0, 0, 0, (255 - i) * 16)
    return rgba


def original_size(rel):
    idx, _ = read_pcx(media_path(rel))
    return idx.shape[1], idx.shape[0]


def modern_path(rel):
    return os.path.join(MODERN_ART, os.path.splitext(rel.replace(chr(92), '/'))[0] + '.png')


def save_modern(rel, rgba, suffix=''):
    """Writes the remade art for the media path `rel` (e.g. 'Art/Terrain/xtgc.pcx').
    `rgba` (HxWx4 uint8, or a PIL image) must be exactly twice the original's size."""
    img = rgba if isinstance(rgba, Image.Image) else Image.fromarray(np.asarray(rgba, np.uint8), 'RGBA')
    if not suffix:
        w, h = original_size(rel)
        assert img.size == (w * SCALE, h * SCALE), f'{rel}: {img.size} is not 2x of {(w, h)}'
    path = os.path.splitext(modern_path(rel))[0] + suffix + '.png'
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path, optimize=True)
    return path


def save_preview(name, *images, background=(40, 40, 40)):
    """Saves images side by side (e.g. the original upscaled 2x next to the remake)
    into ArtGen/_previews for checking by eye."""
    ims = [im if isinstance(im, Image.Image) else Image.fromarray(np.asarray(im, np.uint8)) for im in images]
    ims = [im.convert('RGBA') for im in ims]
    w = sum(im.width for im in ims) + 10 * (len(ims) - 1)
    h = max(im.height for im in ims)
    out = Image.new('RGBA', (w, h), background + (255,))
    x = 0
    for im in ims:
        out.alpha_composite(im, (x, 0))
        x += im.width + 10
    os.makedirs(PREVIEWS, exist_ok=True)
    path = os.path.join(PREVIEWS, name + '.png')
    out.save(path)
    return path


def upscale_nearest(rgba, factor=SCALE):
    return np.repeat(np.repeat(np.asarray(rgba), factor, 0), factor, 1)
