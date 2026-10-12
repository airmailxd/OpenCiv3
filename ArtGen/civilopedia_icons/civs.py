"""Civilization icons: a heraldic shield in the civilization's color (the
standalone civ colors, by the ruleset's primaryColorIndex) bearing a simple
symbol of the civilization, raised in relief (kit3d)."""
import math

import numpy as np
from PIL import Image, ImageDraw

import common as C
import kit3d as K
import mesh as M

N = 240
BACKDROP = ('f1e8d2', 'a8916a')
SYMBOLS = {}


def symbol(key):
    def deco(fn):
        SYMBOLS[key] = fn
        return fn
    return deco


def _c():
    im = Image.new('L', (N, N), 0)
    return im, ImageDraw.Draw(im)


def _mask(im):
    return np.asarray(im, np.uint8) > 127


def _xy():
    yy, xx = np.mgrid[0:N, 0:N] + 0.5
    return xx, yy


def shield_mask(inset=0):
    im, d = _c()
    i = inset
    pts = [(28 + i, 22 + i), (212 - i, 22 + i), (212 - i, 120), (200 - i * 0.8, 160), (170 - i * 0.6, 195 - i * 0.5),
           (120, 226 - i * 1.2), (70 + i * 0.6, 195 - i * 0.5), (40 + i * 0.8, 160), (28 + i, 120)]
    d.polygon(pts, fill=255)
    return _mask(im)


def make(civ):
    key = civ['civilopediaEntry']
    base = C.mute(C.hex_rgb(C.CIV_COLORS[civ['primaryColorIndex'] % len(C.CIV_COLORS)]), 0.72, 0.9)
    lum = float(base @ np.array([0.3, 0.55, 0.15]))
    sym_col = '#efe4c4' if lum < 0.6 else '#3c3226'
    k = K.Kit()
    field = k.m('field', '#%02x%02x%02x' % tuple(int(v * 255) for v in base), spec=0.2, gloss=14)
    outer = shield_mask(0)
    inner = shield_mask(12)
    sym = SYMBOLS[key]() & shield_mask(22)
    k.relief([(outer, 'gold', 0.1, 4.0), (inner, field, -0.03, 2.0), (sym, k.m('sym', sym_col, spec=0.3), 0.07, 2.5)], 2.0)
    return K.compose(k, BACKDROP[0], BACKDROP[1], yaw=-10, pitch=8, fill=0.84)


# ---------------------------------------------------------------- symbols

def poly(d, pts, fill=255):
    d.polygon([(float(x), float(y)) for x, y in pts], fill=fill)


@symbol('RACE_ROMANS')
def _():
    # a laurel wreath around SPQR-like bars (an eagle standard would be busier)
    im, d = _c()
    c = 118
    for side in (-1, 1):
        for i in range(8):
            t = i / 7
            a = math.pi / 2 + side * (0.35 + t * 2.2)
            x, y = 120 + 62 * math.cos(a), c + 62 * math.sin(a)
            tang = a + side * math.pi / 2
            for off in (-1, 1):
                ang = tang + off * 0.8
                pts = [(x + (s * 18) * math.cos(ang) - (w * 6) * math.sin(ang), y + (s * 18) * math.sin(ang) + (w * 6) * math.cos(ang))
                       for s, w in ((0, 0), (0.5, 1), (1, 0), (0.5, -1))]
                poly(d, pts)
    m = _mask(im)
    return m | K.text_mask('SPQR', N, 0.17, dy=-0.01)


@symbol('RACE_EGYPTIANS')
def _():
    # an ankh
    im, d = _c()
    d.ellipse((88, 40, 152, 112), fill=255)
    d.ellipse((104, 58, 136, 98), fill=0)
    d.rectangle((112, 104, 128, 200), fill=255)
    d.rectangle((70, 112, 170, 128), fill=255)
    return _mask(im)


@symbol('RACE_GREEKS')
def _():
    # an Ionic column
    im, d = _c()
    d.rectangle((84, 186, 156, 198), fill=255)
    d.rectangle((92, 176, 148, 186), fill=255)
    for x in range(98, 142, 9):
        d.rectangle((x, 72, x + 6, 176), fill=255)
    d.rectangle((86, 60, 154, 72), fill=255)
    for cx in (84, 156):
        d.ellipse((cx - 14, 46, cx + 14, 74), fill=255)
        d.ellipse((cx - 6, 54, cx + 6, 66), fill=0)
    d.rectangle((80, 44, 160, 52), fill=255)
    return _mask(im)


@symbol('RACE_BABYLON')
def _():
    # a ziggurat
    im, d = _c()
    for i, (w, y) in enumerate(((120, 176), (92, 148), (64, 120), (36, 92))):
        d.rectangle((120 - w / 2, y, 120 + w / 2, y + 24), fill=255)
    d.rectangle((113, 92, 127, 200), fill=0)
    d.rectangle((116, 96, 124, 200), fill=255)
    return _mask(im)


def _eagle(d, heads=1):
    # a spread eagle, wings up
    poly(d, [(120, 80), (138, 96), (136, 150), (120, 176), (104, 150), (102, 96)])     # body
    for s in (-1, 1):
        pts = [(120 + s * 14, 100), (120 + s * 40, 70), (120 + s * 70, 52), (120 + s * 82, 70), (120 + s * 72, 84),
               (120 + s * 84, 96), (120 + s * 70, 108), (120 + s * 80, 122), (120 + s * 60, 130), (120 + s * 64, 142),
               (120 + s * 36, 136), (120 + s * 16, 130)]
        poly(d, pts)
        poly(d, [(120 + s * 6, 170), (120 + s * 26, 196), (120 + s * 14, 198), (120 + s * 2, 180)])  # tail/legs
    if heads == 1:
        d.ellipse((108, 58, 132, 84), fill=255)
        poly(d, [(130, 66), (144, 72), (130, 78)])
    else:
        for s in (-1, 1):
            d.ellipse((120 + s * 14 - 11, 58, 120 + s * 14 + 11, 80), fill=255)
            poly(d, [(120 + s * 22, 64), (120 + s * 36, 70), (120 + s * 22, 76)])


@symbol('RACE_GERMANS')
def _():
    im, d = _c()
    _eagle(d, 1)
    return _mask(im)


@symbol('RACE_HATTI')
def _():
    im, d = _c()
    _eagle(d, 2)
    return _mask(im)


@symbol('RACE_RUSSIAN')
def _():
    # an onion dome with a cross
    im, d = _c()
    pts = []
    for i in range(41):
        t = i / 40
        y = 160 - t * 90
        w = 50 * math.sin(math.pi * min(1, t * 1.25)) ** 0.9 * (1 - t) ** 0.35
        pts.append((120 + w, y))
    pts += [(240 - x, y) for x, y in pts[::-1]]
    poly(d, pts)
    d.rectangle((84, 160, 156, 200), fill=255)
    d.rectangle((96, 172, 106, 200), fill=0)
    d.rectangle((134, 172, 144, 200), fill=0)
    d.rectangle((116, 40, 124, 74), fill=255)
    d.rectangle((106, 50, 134, 56), fill=255)
    return _mask(im)


@symbol('RACE_CHINESE')
def _():
    # a pagoda
    im, d = _c()
    for i, (w, y) in enumerate(((110, 160), (86, 120), (64, 84))):
        poly(d, [(120 - w / 2 - 10, y), (120 - w / 2, y - 8), (120 + w / 2, y - 8), (120 + w / 2 + 10, y), (120 + w / 2 - 6, y + 6),
                 (120 - w / 2 + 6, y + 6)])
        d.rectangle((120 - w / 2 + 12, y + 6, 120 + w / 2 - 12, y + 34), fill=255)
    d.rectangle((116, 54, 124, 78), fill=255)
    d.rectangle((70, 196, 170, 204), fill=255)
    return _mask(im)


@symbol('RACE_AMERICAN')
def _():
    # a star over stripes
    im, d = _c()
    poly(d, K.star_points(120, 92, 46, 18))
    for y in (150, 172, 194):
        d.rectangle((60, y, 180, y + 11), fill=255)
    return _mask(im)


@symbol('RACE_JAPANESE')
def _():
    # a torii gate before the sun
    im, d = _c()
    d.ellipse((76, 44, 164, 132), fill=255)
    d.ellipse((84, 52, 156, 124), fill=0)
    poly(d, [(56, 92), (184, 92), (176, 104), (64, 104)])
    d.rectangle((70, 118, 170, 126), fill=255)
    d.rectangle((84, 100, 96, 200), fill=255)
    d.rectangle((144, 100, 156, 200), fill=255)
    return _mask(im)


@symbol('RACE_FRENCH')
def _():
    # a fleur-de-lis
    im, d = _c()
    # the middle petal: a tall pointed leaf
    pts = []
    for i in range(21):
        t = i / 20
        pts.append((120 + 22 * math.sin(math.pi * t) ** 0.8, 138 - 100 * t))
    pts += [(240 - x, y) for x, y in pts[::-1]]
    poly(d, pts)
    # the side petals: curling out and down from the band
    for s in (-1, 1):
        outer, inner = [], []
        for i in range(25):
            t = i / 24
            a = math.pi * 1.25 * t
            outer.append((120 + s * (14 + 42 * math.sin(a) + 14 * t), 134 - 52 * math.sin(a * 0.8) + 30 * t ** 2))
            inner.append((120 + s * (14 + 26 * math.sin(a) + 10 * t), 134 - 30 * math.sin(a * 0.8) + 26 * t ** 2))
        poly(d, outer + inner[::-1])
    d.rectangle((86, 136, 154, 150), fill=255)
    for s in (-1, 1):
        poly(d, [(120 + s * 4, 150), (120 + s * 30, 168), (120 + s * 36, 196), (120 + s * 16, 178), (120, 172)])
    poly(d, [(112, 150), (128, 150), (124, 196), (116, 196)])
    return _mask(im)


@symbol('RACE_INDIAN')
def _():
    # a wheel with 24 spokes
    xx, yy = _xy()
    r = np.hypot(xx - 120, yy - 120)
    m = (r < 70) & (r > 58)
    m |= r < 14
    a = np.degrees(np.arctan2(yy - 120, xx - 120)) % 15
    m |= (r < 60) & ((a < 2.2) | (a > 12.8))
    return m


@symbol('RACE_PERSIAN')
def _():
    # a winged disc
    im, d = _c()
    d.ellipse((96, 92, 144, 140), fill=255)
    d.ellipse((106, 102, 134, 130), fill=0)
    d.ellipse((112, 108, 128, 124), fill=255)
    for s in (-1, 1):
        for i in range(4):
            y = 100 + i * 10
            poly(d, [(120 + s * 26, y), (120 + s * (96 - i * 12), y - 10 + i * 2), (120 + s * (92 - i * 12), y + 6), (120 + s * 26, y + 8)])
    poly(d, [(108, 142), (132, 142), (142, 186), (120, 176), (98, 186)])
    return _mask(im)


@symbol('RACE_AZTECS')
def _():
    # a sun stone
    xx, yy = _xy()
    r = np.hypot(xx - 120, yy - 120)
    a = np.arctan2(yy - 120, xx - 120)
    m = (r < 76) & (r > 66)
    m |= (r < 54) & (r > 46)
    m |= (r < 30)
    rays = (np.cos(8 * a) > 0.55) & (r < 66) & (r > 54)
    m |= rays
    m &= ~((r < 22) & (r > 16))
    return m


@symbol('RACE_ZULU')
def _():
    # an oval shield with crossed spears
    im, d = _c()
    for s in (-1, 1):
        d.line((120 - s * 70, 196, 120 + s * 60, 52), fill=255, width=7)
        x, y = 120 + s * 60, 52
        poly(d, [(x + s * 6, y - 22), (x + s * 12, y + 4), (x - s * 4, y + 6)])
    d.ellipse((90, 50, 150, 190), fill=255)
    d.ellipse((98, 58, 142, 182), fill=0)
    d.rectangle((116, 46, 124, 194), fill=255)
    for y in (80, 104, 128, 152):
        d.ellipse((104, y, 114, y + 10), fill=255)
        d.ellipse((126, y + 6, 136, y + 16), fill=255)
    return _mask(im)


@symbol('RACE_IROQUOIS')
def _():
    # the great white pine
    im, d = _c()
    for i, (w, y) in enumerate(((44, 62), (64, 96), (84, 130), (100, 164))):
        poly(d, [(120, y - 30), (120 + w / 2 + 10, y + 14), (120 - w / 2 - 10, y + 14)])
    d.rectangle((112, 176, 128, 204), fill=255)
    return _mask(im)


@symbol('RACE_ENGLISH')
def _():
    # a rose
    # a heraldic rose: five outer petals, five inner ones, a seeded center
    xx, yy = _xy()
    m = np.zeros((N, N), bool)
    cut = np.zeros((N, N), bool)
    for k in range(5):
        a = math.radians(-90 + 72 * k)
        cx, cy = 120 + 40 * math.cos(a), 120 + 40 * math.sin(a)
        r = np.hypot(xx - cx, yy - cy)
        m |= r < 32
        cut |= (r > 32) & (r < 36)
    for k in range(5):
        a = math.radians(-54 + 72 * k)
        cx, cy = 120 + 20 * math.cos(a), 120 + 20 * math.sin(a)
        r = np.hypot(xx - cx, yy - cy)
        cut |= (r > 18) & (r < 22)
    r0 = np.hypot(xx - 120, yy - 120)
    cut |= (r0 > 12) & (r0 < 16)
    return m & ~cut


@symbol('RACE_MONGOLS')
def _():
    # a drawn composite bow and arrow
    im, d = _c()
    d.arc((60, 44, 180, 196), 110, 250, fill=255, width=10)
    d.line((92, 50, 92, 190), fill=255, width=3)
    d.line((88, 120, 196, 120), fill=255, width=6)
    poly(d, [(196, 106), (220, 120), (196, 134)])
    poly(d, [(84, 108), (100, 120), (84, 132), (72, 120)])
    return _mask(im)


@symbol('RACE_SPANISH')
def _():
    # a castle
    im, d = _c()
    d.rectangle((70, 110, 170, 196), fill=255)
    d.rectangle((100, 70, 140, 110), fill=255)
    for x in range(70, 171, 20):
        d.rectangle((x, 98, x + 10, 112), fill=255)
    for x in (100, 116, 132):
        d.rectangle((x, 58, x + 8, 72), fill=255)
    d.rectangle((108, 150, 132, 196), fill=0)
    d.ellipse((108, 140, 132, 162), fill=0)
    for x in (84, 148):
        d.rectangle((x, 126, x + 8, 140), fill=0)
    return _mask(im)


@symbol('RACE_VIKINGS')
def _():
    # a longship with its dragon prow
    im, d = _c()
    pts = []
    for i in range(21):
        t = i / 20
        x = 52 + 136 * t
        y = 160 + 26 * math.sin(math.pi * t)
        pts.append((x, y))
    poly(d, [(40, 116), (52, 140), (60, 160)] + pts + [(188, 160), (196, 140), (206, 112), (196, 108), (190, 136), (180, 156), (60, 156),
                                                   (50, 136), (48, 112)])
    d.ellipse((34, 104, 52, 120), fill=255)
    d.rectangle((116, 56, 124, 158), fill=255)
    poly(d, [(80, 64), (160, 64), (156, 132), (84, 132)])
    for x in range(72, 176, 16):
        d.ellipse((x, 148, x + 12, 160), fill=0)
    return _mask(im)


@symbol('RACE_OTTOMANS')
def _():
    xx, yy = _xy()
    m = (np.hypot(xx - 108, yy - 120) < 62) & ~(np.hypot(xx - 124, yy - 120) < 52)
    im, d = _c()
    poly(d, K.star_points(158, 120, 24, 10, rot=180))
    return m | _mask(im)


@symbol('RACE_CELTS')
def _():
    # a triskele
    xx, yy = _xy()
    m = np.zeros((N, N), bool)
    for k in range(3):
        a0 = 2 * math.pi * k / 3 - math.pi / 2
        cx, cy = 120 + 28 * math.cos(a0), 120 + 28 * math.sin(a0)
        r = np.hypot(xx - cx, yy - cy)
        th = (np.arctan2(yy - cy, xx - cx) - a0) % (2 * math.pi)
        for turn in range(2):
            rr = 6 + 7.5 * (th + 2 * math.pi * turn) / math.pi
            m |= (np.abs(r - rr) < 4.5) & (rr < 48)
    m |= np.hypot(xx - 120, yy - 120) < 9
    return m


@symbol('RACE_ARABS')
def _():
    # an eight-pointed star
    im, d = _c()
    for rot in (0, 45):
        a = [math.radians(rot + 45 + 90 * i) for i in range(4)]
        poly(d, [(120 + 76 * math.cos(t), 120 + 76 * math.sin(t)) for t in a])
    d.ellipse((96, 96, 144, 144), fill=0)
    d.ellipse((104, 104, 136, 136), fill=255)
    return _mask(im)


@symbol('RACE_CARTHAGINIANS')
def _():
    # the sign of Tanit
    im, d = _c()
    poly(d, [(120, 100), (170, 196), (70, 196)])
    d.rectangle((66, 92, 174, 104), fill=255)
    d.rectangle((66, 72, 78, 104), fill=255)
    d.rectangle((162, 72, 174, 104), fill=255)
    d.ellipse((96, 40, 144, 88), fill=255)
    return _mask(im)


@symbol('RACE_KOREANS')
def _():
    # a taegeuk
    xx, yy = _xy()
    R = 66
    r = np.hypot(xx - 120, yy - 120)
    left = np.hypot(xx - (120 - R / 2), yy - 120) < R / 2
    right = np.hypot(xx - (120 + R / 2), yy - 120) < R / 2
    top = ((yy < 120) & ~left) | (right & (yy >= 120))
    m = (r < R) & top
    ring = (r > R + 4) & (r < R + 11)
    return m | ring


@symbol('RACE_SUMERIA')
def _():
    # the eight-rayed star of Inanna
    im, d = _c()
    d.ellipse((96, 96, 144, 144), fill=255)
    for i in range(8):
        a = math.radians(22.5 + 45 * i)
        b = math.radians(45 * i)
        pts = [(120 + 22 * math.cos(a - 0.3), 120 + 22 * math.sin(a - 0.3)), (120 + 80 * math.cos(a), 120 + 80 * math.sin(a)),
               (120 + 22 * math.cos(a + 0.3), 120 + 22 * math.sin(a + 0.3))]
        poly(d, pts)
    d.ellipse((108, 108, 132, 132), fill=0)
    return _mask(im)


@symbol('RACE_DUTCH')
def _():
    # a windmill
    im, d = _c()
    poly(d, [(104, 108), (136, 108), (148, 200), (92, 200)])
    poly(d, [(100, 110), (120, 88), (140, 110)])
    for k in range(4):
        a = math.radians(45 + 90 * k)
        ca, sa = math.cos(a), math.sin(a)
        cx, cy = 120, 100
        pts = [(cx + 10 * ca - 6 * sa, cy + 10 * sa + 6 * ca), (cx + 84 * ca - 6 * sa, cy + 84 * sa + 6 * ca),
               (cx + 84 * ca + 14 * sa, cy + 84 * sa - 14 * ca), (cx + 24 * ca + 14 * sa, cy + 24 * sa - 14 * ca)]
        poly(d, pts)
    d.ellipse((112, 92, 128, 108), fill=255)
    d.rectangle((112, 170, 128, 200), fill=0)
    return _mask(im)


@symbol('RACE_PORTUGAL')
def _():
    # an armillary sphere
    im, d = _c()
    d.ellipse((60, 52, 180, 172), outline=255, width=9)
    d.ellipse((60, 92, 180, 132), outline=255, width=8)
    d.ellipse((96, 52, 144, 172), outline=255, width=7)
    d.line((70, 80, 172, 140), fill=255, width=9)
    d.rectangle((114, 172, 126, 196), fill=255)
    d.rectangle((92, 192, 148, 202), fill=255)
    return _mask(im)


@symbol('RACE_BYZANTINES')
def _():
    # a cross in a circle, with four small crosses
    im, d = _c()
    d.ellipse((50, 50, 190, 190), outline=255, width=10)
    d.rectangle((112, 62, 128, 178), fill=255)
    d.rectangle((62, 112, 178, 128), fill=255)
    for (x, y) in ((88, 88), (152, 88), (88, 152), (152, 152)):
        d.rectangle((x - 3, y - 12, x + 3, y + 12), fill=255)
        d.rectangle((x - 12, y - 3, x + 12, y + 3), fill=255)
    return _mask(im)


@symbol('RACE_INCANS')
def _():
    # the chakana, the stepped Andean cross
    im, d = _c()
    s = 22
    for (x0, y0, x1, y1) in ((-1, -3, 1, 3), (-3, -1, 3, 1), (-2, -2, 2, 2)):
        d.rectangle((120 + x0 * s, 120 + y0 * s, 120 + x1 * s, 120 + y1 * s), fill=255)
    d.ellipse((120 - 18, 120 - 18, 120 + 18, 120 + 18), fill=0)
    return _mask(im)


@symbol('RACE_MAYANS')
def _():
    # a stepped pyramid with a temple on top
    im, d = _c()
    for i in range(5):
        w = 150 - i * 24
        y = 190 - i * 22
        d.rectangle((120 - w / 2, y - 22, 120 + w / 2, y), fill=255)
    d.rectangle((96, 58, 144, 82), fill=255)
    poly(d, [(92, 58), (120, 40), (148, 58)])
    d.rectangle((112, 66, 128, 82), fill=0)
    d.rectangle((110, 82, 130, 190), fill=0)
    for y in range(86, 190, 8):
        d.rectangle((112, y, 128, y + 4), fill=255)
    return _mask(im)
