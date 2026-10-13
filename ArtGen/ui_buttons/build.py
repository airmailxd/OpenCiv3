"""Unit order buttons for standalone mode that the asset pack lacks.

The standalone asset pack's Art/interface/NormButtons.png has blank buttons
where Civ3's has Build Outpost, Build Colony, Build Airfield and Build Radar
Tower. Each of these is drawn as a
file of its own, Art/interface/<order>.png in C7/ModernArt: the normal, hover
and pressed buttons side by side, each 64x64 (twice Civ3's 32x32 button, as
all the remade art is). C7/Lua/standalone/textures.lua points the orders'
buttons at them.

It also draws the air missions' and the airdrop's buttons (see
air_missions.py), which are blank in the asset pack too.

The buttons copy the asset pack's look: a golden ball with the order drawn on
it in dark brown.
"""
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'common'))
import civ3art  # noqa: E402

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import air_missions  # noqa: E402

CELL = 64          # one button at 2x
SS = 4             # supersampling
S = CELL * SS

GLYPH = (58, 36, 14, 255)


def ball(brightness):
    """A golden ball lit from the upper left, as big as the cell."""
    img = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    px = img.load()
    c = S / 2
    r = S / 2 - 2 * SS
    lx, ly = -0.45, -0.55
    for y in range(S):
        for x in range(S):
            dx, dy = (x + 0.5 - c) / r, (y + 0.5 - c) / r
            d2 = dx * dx + dy * dy
            if d2 > 1:
                continue
            dz = math.sqrt(1 - d2)
            light = max(0.0, -(dx * lx + dy * ly) + dz * 0.7)
            shade = 0.55 + 0.45 * light
            rim = 1 - 0.25 * max(0.0, math.sqrt(d2) - 0.85) / 0.15
            k = shade * rim * brightness
            px[x, y] = (min(255, int(214 * k)), min(255, int(160 * k)), min(255, int(62 * k)), 255)
    return img


def outpost_glyph(draw):
    """A watchtower on a mound: legs, a platform and a pointed roof."""
    u = S / 64
    draw.polygon([(14 * u, 50 * u), (50 * u, 50 * u), (44 * u, 45 * u), (20 * u, 45 * u)], fill=GLYPH)
    for x0, x1 in ((23, 26), (38, 41)):
        draw.polygon([(x0 * u, 46 * u), ((x0 + 3) * u, 46 * u), ((x1 + 1) * u, 26 * u), ((x1 - 2) * u, 26 * u)], fill=GLYPH)
    draw.line([(25 * u, 38 * u), (39 * u, 32 * u)], fill=GLYPH, width=int(2 * u))
    draw.line([(25 * u, 32 * u), (39 * u, 38 * u)], fill=GLYPH, width=int(2 * u))
    draw.rectangle([20 * u, 21 * u, 44 * u, 27 * u], fill=GLYPH)
    draw.polygon([(17 * u, 21 * u), (47 * u, 21 * u), (32 * u, 9 * u)], fill=GLYPH)


def colony_glyph(draw):
    """Two huts on a piece of land, with a crate of goods."""
    u = S / 64
    draw.polygon([(10 * u, 48 * u), (54 * u, 48 * u), (48 * u, 43 * u), (16 * u, 43 * u)], fill=GLYPH)
    # the big hut
    draw.rectangle([15 * u, 30 * u, 31 * u, 44 * u], fill=GLYPH)
    draw.polygon([(12 * u, 31 * u), (34 * u, 31 * u), (23 * u, 19 * u)], fill=GLYPH)
    # the small hut
    draw.rectangle([35 * u, 35 * u, 47 * u, 44 * u], fill=GLYPH)
    draw.polygon([(33 * u, 36 * u), (49 * u, 36 * u), (41 * u, 27 * u)], fill=GLYPH)
    # a crate
    draw.rectangle([44 * u, 39 * u, 52 * u, 46 * u], fill=GLYPH)


def airfield_glyph(draw):
    """A control tower beside a runway, with a plane above it, as Civ3's
    button has."""
    u = S / 64
    # the runway, seen at an angle
    draw.polygon([(8 * u, 50 * u), (46 * u, 50 * u), (56 * u, 42 * u), (18 * u, 42 * u)], fill=GLYPH)
    # the control tower: a shaft with a cab on top
    draw.rectangle([14 * u, 24 * u, 19 * u, 44 * u], fill=GLYPH)
    draw.polygon([(10 * u, 24 * u), (23 * u, 24 * u), (21 * u, 17 * u), (12 * u, 17 * u)], fill=GLYPH)
    # a plane: fuselage, wings and tail
    draw.polygon([(26 * u, 27 * u), (54 * u, 22 * u), (56 * u, 24 * u), (28 * u, 30 * u)], fill=GLYPH)
    draw.polygon([(38 * u, 25 * u), (44 * u, 24 * u), (36 * u, 12 * u), (33 * u, 13 * u)], fill=GLYPH)
    draw.polygon([(38 * u, 27 * u), (44 * u, 26 * u), (40 * u, 36 * u), (37 * u, 36 * u)], fill=GLYPH)
    draw.polygon([(26 * u, 27 * u), (29 * u, 27 * u), (27 * u, 20 * u), (25 * u, 20 * u)], fill=GLYPH)


def radar_tower_glyph(draw):
    """A radar dish on a lattice tower."""
    u = S / 64
    draw.polygon([(16 * u, 52 * u), (48 * u, 52 * u), (44 * u, 47 * u), (20 * u, 47 * u)], fill=GLYPH)
    # the tower's legs and braces
    for x0, x1 in ((24, 29), (40, 35)):
        draw.line([(x0 * u, 48 * u), (x1 * u, 28 * u)], fill=GLYPH, width=int(3 * u))
    for y0, y1 in ((46, 39), (39, 32)):
        draw.line([(25 * u, y0 * u), (38 * u, y1 * u)], fill=GLYPH, width=int(2 * u))
        draw.line([(39 * u, y0 * u), (26 * u, y1 * u)], fill=GLYPH, width=int(2 * u))
    # the dish, a crescent turned up to the sky (a disc with a smaller
    # one cut out of it toward the upper right), and its feed
    draw.ellipse([13 * u, 5 * u, 45 * u, 37 * u], fill=GLYPH)
    draw.ellipse([19 * u, 1 * u, 49 * u, 31 * u], fill=(0, 0, 0, 0))
    draw.line([(26 * u, 24 * u), (40 * u, 12 * u)], fill=GLYPH, width=int(2 * u))
    draw.ellipse([38 * u, 8 * u, 45 * u, 15 * u], fill=GLYPH)


def button(glyph, brightness, offset):
    img = ball(brightness)
    layer = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    glyph(ImageDraw.Draw(layer))
    # A pressed button's picture sits a little lower.
    layer = layer.transform(layer.size, Image.AFFINE, (1, 0, 0, 0, 1, -offset * SS))
    # A faint light edge under the picture, as if pressed into the metal.
    edge = layer.split()[3].filter(ImageFilter.GaussianBlur(SS))
    light = Image.new('RGBA', (S, S), (255, 230, 160, 0))
    light.putalpha(edge.point(lambda a: a // 3))
    img.alpha_composite(light, (SS, SS))
    img.alpha_composite(layer)
    return img.resize((CELL, CELL), Image.LANCZOS)


def build(name, glyph):
    sheet = Image.new('RGBA', (CELL * 3, CELL), (0, 0, 0, 0))
    for i, (brightness, offset) in enumerate(((1.0, 0), (1.15, 0), (0.85, 1))):
        sheet.paste(button(glyph, brightness, offset), (CELL * i, 0))
    path = os.path.join(civ3art.MODERN_ART, 'Art', 'interface', name + '.png')
    os.makedirs(os.path.dirname(path), exist_ok=True)
    sheet.save(path, optimize=True)
    print('wrote', path)


if __name__ == '__main__':
    build('unit_build_outpost', outpost_glyph)
    build('unit_build_colony', colony_glyph)
    build('unit_build_airfield', airfield_glyph)
    build('unit_build_radar_tower', radar_tower_glyph)
    air_missions.main()
