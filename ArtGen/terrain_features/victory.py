"""Remake of Art/Terrain/x_victory.pcx: Conquests' Victory Location marker, drawn
with the renderer from buildings.py.

The original is one iso tile cell (128x64, 256x128 at 2x) with a white stone
obelisk banded in gold on a stepped plinth, standing toward the tile's right
corner. Standalone mode's asset pack has no marker at all, so Victory Locations
(Victory Point Scoring) weren't shown on its map.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'common'))
import civ3art  # noqa: E402
from buildings import Scene, render, rgb, on_grass, STONE, METAL  # noqa: E402

REL = 'Art/Terrain/x_victory.pcx'

MARBLE = rgb(232, 228, 216)
MARBLE_SHADE = rgb(212, 206, 192)
GOLD = rgb(214, 172, 72)

# Toward the right corner of the tile (+x is screen lower right, +y lower left).
X, Y = 30, -20


def obelisk(s):
    # Two square steps of the plinth, then a pedestal.
    s.obj()
    s.cbox(X, Y, 34, 34, 0, 5, MARBLE_SHADE, STONE)
    s.obj()
    s.cbox(X, Y, 26, 26, 5, 10, MARBLE, STONE)
    s.obj()
    s.cbox(X, Y, 16, 16, 10, 18, MARBLE, STONE)
    # The shaft, tapering, banded in gold near the top, with a pointed cap.
    s.obj()
    s.cylinder(X, Y, 8.5, 18, 52, MARBLE, STONE, n=4, r1=6.2)
    s.obj()
    s.cylinder(X, Y, 6.9, 44, 49, GOLD, METAL, n=4, r1=6.6)
    s.obj()
    s.cone(X, Y, 6.2, 52, 8, MARBLE, STONE, n=4)


def build():
    s = Scene()
    obelisk(s)
    sheet = render(s)
    civ3art.save_modern(REL, sheet)
    orig1 = civ3art.load_original(REL, shadows=True)
    civ3art.save_preview('x_victory', on_grass(civ3art.upscale_nearest(orig1)), on_grass(sheet))
    return sheet


if __name__ == '__main__':
    build()
