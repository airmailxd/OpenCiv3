"""Reading the original Civ3 unit animations (.flc) and unit .INI files.

A port of ConvertCiv3Media/ReadFlic.cs, plus the game's split of a frame into
its layers (C7/Textures/PCXToGodot.cs ByteArrayWithTintToImage):
  * palette index < 16, or even and < 64: civ-color tint layer
  * 240..255: black shadow of opacity (255 - index) * 16 / 255 (255 is the
    transparent background), as PCXToGodot.loadPalette with shadows
  * 224..239: also treated as shadow by the game (rarely used)
  * everything else: the unit's own colors.
"""
import os
import struct
import sys
from dataclasses import dataclass, field

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'common'))
import civ3art  # noqa: E402

# The flic rows are directions in this order.
DIRECTIONS = ['SW', 'S', 'SE', 'E', 'NE', 'N', 'NW', 'W']

# The action keys of a unit .INI [Animations] section that the game uses.
ACTIONS = ['DEFAULT', 'WALK', 'RUN', 'ATTACK1', 'ATTACK2', 'ATTACK3', 'DEFEND', 'DEATH', 'DEAD',
           'FORTIFY', 'FORTIFYHOLD', 'FIDGET', 'VICTORY', 'TURNLEFT', 'TURNRIGHT', 'BUILD', 'ROAD',
           'MINE', 'IRRIGATE', 'FORTRESS', 'CAPTURE', 'JUNGLE', 'FOREST', 'PLANT', 'STOP_AT_LAST_FRAME']


@dataclass
class Flic:
    path: str
    width: int
    height: int
    speed: int            # ms per frame
    offset_left: int
    offset_top: int
    orig_width: int
    orig_height: int
    n_anims: int          # directions (rows)
    n_frames: int         # frames per direction (columns)
    palette: np.ndarray   # 256x3 uint8
    frames: np.ndarray    # n_anims x n_frames x H x W uint8 palette indexes

    def layers(self, d, f):
        """(base RGBA, tint mask bool, tint shade 0..1, shadow alpha 0..1) of one frame."""
        return split_frame(self.frames[d, f], self.palette)


def _u16(b, o):
    return struct.unpack_from('<H', b, o)[0]


def _i32(b, o):
    return struct.unpack_from('<i', b, o)[0]


def read_flic(path):
    b = open(path, 'rb').read()
    if _u16(b, 4) != 0xAF12:
        raise ValueError(f'{path}: not a flic')
    nframes = _u16(b, 6)
    w, h = _u16(b, 8), _u16(b, 10)
    speed = _i32(b, 16)
    first = _i32(b, 80)
    n_anims, per = _u16(b, 0x60), _u16(b, 0x62)
    if n_anims == 0:
        n_anims, per = 1, nframes
    pal = np.zeros((256, 3), np.uint8)
    frames = np.zeros((n_anims, per, h, w), np.uint8)
    off = first
    for a in range(n_anims):
        for f in range(per):
            frame = frames[a, f].reshape(-1)
            if f > 0:
                frame[:] = frames[a, f - 1].reshape(-1)
            clen = _i32(b, off)
            nsub = _u16(b, off + 6)
            cend = off + clen
            so = off + 16
            for _ in range(nsub):
                if cend - so < 6:
                    break
                slen = _i32(b, so)
                stype = _u16(b, so + 4)
                valid = 6 <= slen <= cend - so
                if not valid:
                    if stype != 4:
                        break
                    slen = cend - so
                data = b[so + 6: so + slen]
                if stype == 4:
                    _color256(data, pal)
                elif stype == 15:
                    _byte_run(data, frame, w, h)
                elif stype == 7:
                    _delta_flc(data, frame, w, h)
                elif stype == 16:
                    frame[:] = np.frombuffer(data[:w * h], np.uint8)
                elif stype == 13:
                    frame[:] = 0
                if not valid:
                    break
                so += slen
            off = cend
        off += _i32(b, off)  # ring frame
    return Flic(path, w, h, speed, _u16(b, 100), _u16(b, 102), _u16(b, 104), _u16(b, 106),
                n_anims, per, pal, frames)


def _color256(d, pal):
    n = _u16(d, 0)
    h, c = 2, 0
    for _ in range(n):
        c += d[h]
        cnt = d[h + 1] or 256
        h += 2
        pal[c:c + cnt] = np.frombuffer(d[h:h + cnt * 3], np.uint8).reshape(cnt, 3)
        h += cnt * 3
        c += cnt


def _byte_run(d, frame, w, h):
    head = 0
    for y in range(h):
        head += 1
        x = 0
        row = y * w
        while x < w:
            t = d[head]
            t = t - 256 if t > 127 else t
            head += 1
            if t < 0:
                frame[row + x: row + x - t] = np.frombuffer(d[head:head - t], np.uint8)
                head -= t
                x -= t
            else:
                frame[row + x: row + x + t] = d[head]
                x += t
                head += 1


def _delta_flc(d, frame, w, h):
    head = 0
    nlines = _u16(d, 0)
    head = 2
    y = 0
    for _ in range(nlines):
        while True:
            op = _u16(d, head)
            head += 2
            if op & 0xC000 == 0xC000:
                y -= op - 0x10000
            elif op & 0xC000 == 0x8000:
                frame[w * (y + 1) - 1] = op & 0xFF
            else:
                npk = op
                break
        row = w * y
        x = 0
        for _ in range(npk):
            x += d[head]
            nw = d[head + 1]
            nw = nw - 256 if nw > 127 else nw
            head += 2
            if nw > 0:
                frame[row + x: row + x + 2 * nw] = np.frombuffer(d[head:head + 2 * nw], np.uint8)
                head += 2 * nw
                x += 2 * nw
            else:
                c = -nw
                if c:
                    frame[row + x: row + x + 2 * c] = np.tile(np.frombuffer(d[head:head + 2], np.uint8), c)
                    x += 2 * c
                head += 2
        y += 1


def tint_mask(idx):
    return (idx < 16) | ((idx < 64) & (idx % 2 == 0))


def shadow_mask(idx):
    return (idx >= 224) & (idx <= 254)


def shadow_alpha(idx):
    i = idx.astype(np.float32)
    return np.where((idx >= 240) & (idx <= 254), (255 - i) * 16 / 255.0,
                    np.where((idx >= 224) & (idx < 240), (i - 224) / 15.0 * 0.4, 0.0))


def split_frame(idx, pal):
    """Layers of a frame: base RGBA (unit colors only), tint mask, shadow alpha."""
    tint = tint_mask(idx)
    shadow = shadow_mask(idx)
    opaque = ~tint & ~shadow & (idx != 255)
    base = np.zeros(idx.shape + (4,), np.uint8)
    base[..., :3] = pal[idx]
    base[..., 3] = np.where(opaque, 255, 0)
    sh = shadow_alpha(idx)
    return base, tint, sh


def composite(idx, pal, civ=(60, 110, 230)):
    """The frame as the game would show it, with a civ color, for previews."""
    base, tint, sh = split_frame(idx, pal)
    out = base.astype(np.float32)
    lum = pal[idx].astype(np.float32).mean(-1, keepdims=True) / 255.0
    # Tint pixels in the flic palette hold the civ-colored shades; use their luminance.
    out[tint, :3] = (np.array(civ, np.float32) * np.clip(lum[tint] * 1.6, 0, 1.2)).clip(0, 255)
    out[tint, 3] = 255
    m = sh > 0
    out[m] = 0
    out[m, 3] = sh[m] * 255
    return out.astype(np.uint8)


def read_ini(path):
    """{section: {key: value}} of a Civ3 .INI (keys upper-cased)."""
    out, sec = {}, None
    for line in open(path, 'r', encoding='latin-1'):
        line = line.strip()
        if line.startswith('[') and ']' in line:
            sec = line[1:line.index(']')].strip()
            out.setdefault(sec, {})
        elif '=' in line and sec is not None:
            k, v = line.split('=', 1)
            out[sec][k.strip().upper()] = v.strip()
    return out


def unit_ini_rel(unit):
    return f'Art/Units/{unit}/{unit}.INI'


def unit_flics(unit):
    """{ACTION: flic file name as the INI writes it} for a unit folder; the game
    resolves each through the media path lookup (Conquests, PTW, base)."""
    ini = read_ini(civ3art.media_path(unit_ini_rel(unit)))
    anims = ini.get('Animations', {})
    return {k: v for k, v in anims.items() if v and v.lower().endswith('.flc')}


# What the game shows with animations off (the art that's committed):
# a standing or fortified unit shows the last frame of its DEFAULT or FORTIFY
# flic, so only that frame is needed (a "still"); a worker at work plays its
# job's flic in a loop, so those are kept whole. Everything else is an
# optional extra, kept out of git in C7/ModernArtAnimations.
STILL_ACTIONS = {'DEFAULT', 'FORTIFY'}
LOOP_ACTIONS = {'ROAD', 'MINE', 'FORTRESS', 'IRRIGATE', 'JUNGLE', 'FOREST', 'PLANT'}


def flic_kind(unit, name):
    """'loop', 'still' or 'animation' (see above)."""
    actions = {a.upper() for a, n in unit_flics(unit).items() if n.lower() == name.lower()}
    if actions & LOOP_ACTIONS:
        return 'loop'
    if actions & STILL_ACTIONS:
        return 'still'
    return 'animation'


def flic_rel(unit, name):
    return f'Art/Units/{unit}/{name}'


def load_unit_flic(unit, name):
    return read_flic(civ3art.media_path(flic_rel(unit, name)))


def all_unit_folders():
    root = civ3art.civ3_root()
    seen = {}
    for base in ('Art', 'civ3PTW/Art', 'Conquests/Art'):
        d = os.path.join(root, base, 'Units')
        if not os.path.isdir(d):
            continue
        for e in os.listdir(d):
            if os.path.isdir(os.path.join(d, e)) and e.lower() != 'palettes':
                seen.setdefault(e.lower(), e)
    return sorted(seen.values(), key=str.lower)
