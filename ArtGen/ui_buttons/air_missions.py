"""The unit command buttons of the air missions and the airdrop for standalone
mode, which has no Civ3 art: the standalone asset pack's button sheet
(Art/interface/NormButtons.png) has only blank buttons where Civ3's has them.

    python ArtGen/ui_buttons/air_missions.py

writes C7/ModernArt/Art/interface/AirMissionButtons.png: a column per button
(bombing, precision strike, recon, air superiority, re-base, airdrop) and a
row per state (normal, hover, pressed), each cell 32x32 like Civ3's buttons,
with twice the pixels like all of ModernArt. C7/Lua/standalone/textures.lua
points the buttons at it.
"""
import os

from PIL import Image, ImageDraw

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
OUT = os.path.join(REPO, 'C7', 'ModernArt', 'Art', 'interface', 'AirMissionButtons.png')

CELL = 64          # 32x32 buttons, twice the pixels
SS = 4             # supersampling
N = CELL * SS
INK = (62, 36, 10, 255)

# Ball colors per state: (rim, body, highlight), like the asset pack's gold.
STATES = [
    ((120, 80, 20), (184, 134, 42), (236, 196, 96)),   # normal
    ((140, 96, 28), (210, 160, 60), (252, 220, 130)),  # hover
    ((100, 60, 14), (160, 104, 28), (214, 160, 70)),   # pressed
]


def ball(state):
    rim, body, high = STATES[state]
    img = Image.new('RGBA', (N, N), (0, 0, 0, 0))
    px = img.load()
    c = N / 2
    r = N / 2 - SS
    hx, hy = c - r * 0.35, c - r * 0.4
    for y in range(N):
        for x in range(N):
            d = ((x - c) ** 2 + (y - c) ** 2) ** 0.5
            if d > r:
                continue
            h = max(0.0, 1 - ((x - hx) ** 2 + (y - hy) ** 2) ** 0.5 / (r * 1.1))
            edge = min(1.0, max(0.0, (d - r * 0.82) / (r * 0.18)))
            col = [body[i] + (high[i] - body[i]) * h * h for i in range(3)]
            col = [col[i] + (rim[i] - col[i]) * edge for i in range(3)]
            px[x, y] = (int(col[0]), int(col[1]), int(col[2]), 255)
    return img


def s(v):
    """A coordinate on a 64-pixel cell, supersampled."""
    return v * SS


def plane(d, cx, cy, size):
    """A top-down aircraft pointing up, centered on (cx, cy)."""
    k = size / 20
    body = [(cx - 1.5 * k, cy - 9 * k), (cx + 1.5 * k, cy - 9 * k), (cx + 1.5 * k, cy + 9 * k), (cx - 1.5 * k, cy + 9 * k)]
    wings = [(cx, cy - 3 * k), (cx + 10 * k, cy + 3 * k), (cx + 10 * k, cy + 5 * k), (cx, cy + 2 * k),
             (cx - 10 * k, cy + 5 * k), (cx - 10 * k, cy + 3 * k)]
    tail = [(cx, cy + 6 * k), (cx + 4 * k, cy + 9 * k), (cx + 4 * k, cy + 10 * k), (cx, cy + 9 * k),
            (cx - 4 * k, cy + 10 * k), (cx - 4 * k, cy + 9 * k)]
    for poly in (body, wings, tail):
        d.polygon([(s(x), s(y)) for x, y in poly], fill=INK)


def bomb(d, cx, cy, size):
    """A bomb falling, nose down."""
    k = size / 20
    d.ellipse([s(cx - 5 * k), s(cy - 6 * k), s(cx + 5 * k), s(cy + 9 * k)], fill=INK)
    d.polygon([(s(cx - 6 * k), s(cy - 12 * k)), (s(cx + 6 * k), s(cy - 12 * k)), (s(cx + 2 * k), s(cy - 4 * k)),
               (s(cx - 2 * k), s(cy - 4 * k))], fill=INK)


def draw_bombing(d):
    bomb(d, 25, 30, 18)
    bomb(d, 39, 34, 18)


def draw_precision(d):
    w = s(3)
    d.ellipse([s(16), s(16), s(48), s(48)], outline=INK, width=w)
    d.ellipse([s(25), s(25), s(39), s(39)], outline=INK, width=w)
    for a, b in (((32, 10), (32, 22)), ((32, 42), (32, 54)), ((10, 32), (22, 32)), ((42, 32), (54, 32))):
        d.line([(s(a[0]), s(a[1])), (s(b[0]), s(b[1]))], fill=INK, width=w)
    d.ellipse([s(30), s(30), s(34), s(34)], fill=INK)


def draw_recon(d):
    # An eye.
    d.chord([s(12), s(18), s(52), s(54)], 200, 340, fill=INK)
    d.chord([s(12), s(10), s(52), s(46)], 20, 160, fill=INK)
    d.ellipse([s(20), s(24), s(44), s(40)], fill=(0, 0, 0, 0))
    d.ellipse([s(26), s(26), s(38), s(38)], fill=INK)


def draw_air_superiority(d):
    plane(d, 32, 30, 24)
    w = s(3)
    d.arc([s(10), s(10), s(54), s(54)], 200, 340, fill=INK, width=w)


def draw_rebase(d):
    plane(d, 24, 32, 18)
    w = s(3)
    d.line([(s(36), s(32)), (s(52), s(32))], fill=INK, width=w)
    d.polygon([(s(54), s(32)), (s(46), s(26)), (s(46), s(38))], fill=INK)


def draw_airdrop(d):
    d.pieslice([s(14), s(12), s(50), s(44)], 180, 360, fill=INK)
    w = s(2)
    for x in (15, 24, 40, 49):
        d.line([(s(x), s(28)), (s(32), s(44))], fill=INK, width=w)
    d.ellipse([s(29), s(42), s(35), s(48)], fill=INK)
    d.line([(s(32), s(47)), (s(32), s(54))], fill=INK, width=s(3))


BUTTONS = [draw_bombing, draw_precision, draw_recon, draw_air_superiority, draw_rebase, draw_airdrop]


def main():
    sheet = Image.new('RGBA', (CELL * len(BUTTONS), CELL * len(STATES)), (0, 0, 0, 0))
    for row in range(len(STATES)):
        base = ball(row)
        for col, draw in enumerate(BUTTONS):
            img = base.copy()
            glyph = Image.new('RGBA', (N, N), (0, 0, 0, 0))
            draw(ImageDraw.Draw(glyph))
            img.alpha_composite(glyph)
            sheet.alpha_composite(img.resize((CELL, CELL), Image.LANCZOS), (col * CELL, row * CELL))
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    sheet.save(OUT, optimize=True)
    print('wrote', OUT)


if __name__ == '__main__':
    main()
