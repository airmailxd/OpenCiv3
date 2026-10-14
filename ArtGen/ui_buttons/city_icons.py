"""The icons drawn by the player's cities on the map for their barracks,
airport and harbor, for standalone mode, which has no Civ3 art.

    python ArtGen/ui_buttons/build.py   (with the buttons; or city_icons.py alone)

writes C7/ModernArt/Art/Cities/CityBuildingIcons.png: the barracks, airport
and harbor icons side by side, each 25x17 like Civ3's (Art/Cities/barracks.pcx
and airAndHarb.pcx), with twice the pixels like all of ModernArt. Like Civ3's
they're a white diamond edged in dark purple with the building's sign on it in
black: a sword, a plane and an anchor. Before, standalone mode showed the
buildings' own pictures there, shrunk until they couldn't be made out.
C7/Lua/standalone/textures.lua points the icons at the sheet.
"""
import os

from PIL import Image, ImageDraw

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
OUT = os.path.join(REPO, 'C7', 'ModernArt', 'Art', 'Cities', 'CityBuildingIcons.png')

W, H = 25, 17      # one icon, as Civ3 shows it
SCALE = 2          # ModernArt has twice the pixels
SS = 8             # supersampling
U = SCALE * SS     # pixels per Civ3 pixel while drawing

EDGE = (60, 20, 60, 255)
FILL = (250, 250, 250, 255)
SHADE = (205, 205, 215, 255)
INK = (16, 16, 16, 255)


def p(x, y):
    return (x * U, y * U)


def diamond(draw):
    cx, cy = W / 2, H / 2
    outer = [p(cx, 0.3), p(W - 0.3, cy), p(cx, H - 0.3), p(0.3, cy)]
    draw.polygon(outer, fill=EDGE)
    inner = [p(cx, 1.5), p(W - 2.0, cy), p(cx, H - 1.5), p(2.0, cy)]
    draw.polygon(inner, fill=FILL)
    # A little shade along the lower edges, as if the plate were lit from above.
    draw.polygon([p(2.0, cy), p(cx, H - 1.5), p(W - 2.0, cy), p(cx, H - 3.0)], fill=SHADE)


def sword(draw):
    # Lying across the diamond, the hilt to the left, as Civ3's is.
    draw.polygon([p(9.5, 7.6), p(20.5, 7.6), p(22.0, 8.5), p(20.5, 9.4), p(9.5, 9.4)], fill=INK)
    draw.rectangle([p(8.3, 5.3), p(9.6, 11.7)], fill=INK)        # the guard
    draw.rectangle([p(4.6, 7.9), p(8.4, 9.1)], fill=INK)         # the grip
    draw.ellipse([p(3.3, 7.4), p(5.1, 9.6)], fill=INK)           # the pommel


def plane(draw):
    # Seen from above, flying to the right, as Civ3's is.
    draw.polygon([p(4.8, 7.7), p(18.0, 7.7), p(20.8, 8.5), p(18.0, 9.3), p(4.8, 9.3)], fill=INK)  # the fuselage
    for sign in (-1, 1):
        y = lambda d: 8.5 + sign * d
        draw.polygon([p(10.0, y(0.5)), p(15.2, y(0.5)), p(13.2, y(5.6)), p(11.4, y(5.6))], fill=INK)  # a wing
        draw.polygon([p(4.6, y(0.5)), p(7.8, y(0.5)), p(6.2, y(3.0)), p(4.6, y(3.0))], fill=INK)      # a tailplane


def anchor(draw):
    cx = W / 2
    draw.ellipse([p(cx - 1.2, 1.9), p(cx + 1.2, 4.3)], outline=INK, width=int(0.8 * U))   # the ring
    draw.rectangle([p(cx - 0.6, 4.0), p(cx + 0.6, 14.2)], fill=INK)                        # the shank
    draw.rectangle([p(cx - 3.0, 5.6), p(cx + 3.0, 6.7)], fill=INK)                         # the stock
    # The arms, curving up to the flukes.
    draw.arc([p(cx - 5.0, 6.6), p(cx + 5.0, 14.4)], 20, 160, fill=INK, width=int(1.1 * U))
    draw.polygon([p(cx - 5.4, 9.2), p(cx - 3.2, 10.2), p(cx - 5.0, 11.4)], fill=INK)
    draw.polygon([p(cx + 5.4, 9.2), p(cx + 3.2, 10.2), p(cx + 5.0, 11.4)], fill=INK)


def icon(sign):
    img = Image.new('RGBA', (W * U, H * U), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    diamond(draw)
    sign(draw)
    return img.resize((W * SCALE, H * SCALE), Image.LANCZOS)


def main():
    signs = [sword, plane, anchor]
    sheet = Image.new('RGBA', (W * SCALE * len(signs), H * SCALE), (0, 0, 0, 0))
    for i, sign in enumerate(signs):
        sheet.paste(icon(sign), (i * W * SCALE, 0))
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    sheet.save(OUT, optimize=True)
    print('wrote', OUT)


if __name__ == '__main__':
    main()
