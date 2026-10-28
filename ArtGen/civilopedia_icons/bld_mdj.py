"""Building icons of MDJ's rules (C7/Lua/mdj, Medieval Japan), for those of
its Civilopedia entries Civ3's rules haven't got (registered with
improvements.bicon). In MDJ BLDG_Council is the Statue of Zeus, a great
wonder; the others are improvements. The camera sees the +x (lower right)
and +y (lower left) faces, as in bld_improvements.py."""
import buildings as BD
import geom as G
from bld3d import lin, plinth, PAVE
from improvements import bicon, P, nb, flag, tree, PLAIN, SLATE, WOOD, GOLD, STONE, PAVING

# Japanese palette: dark tiled roofs, white plaster and dark timber, the
# vermilion of shrines.
JP = dict(P, roofs=[lin('4a5258'), lin('454c52')], hutwalls=[lin('e6dfcf'), lin('dcd4c0')], podium=lin('a8a294'))
VERMILION = lin('b8503a')
TIMBER = lin('4a3624')
STONE_C = lin('a8a294')
PAPER = lin('efe2b8')


@bicon('BLDG_Geisha')
def _(sc, rng):
    # a teahouse of the pleasure quarter, paper lanterns at its door
    plinth(sc, -26, -20, 26, 20, top=lin('c2b48e'), mat=PAVING)
    BD.asian_hall(sc, (-20, -14, 16, 12), 22, JP, rng, two_tier=True, podium=False)
    for x in (-8, 4):
        b = nb(sc)
        G.lathe(sc, x, 14, [(0.25, 0), (0.25, 9)], TIMBER, WOOD, b, seg=6)
        G.lathe(sc, x, 14, [(1.2, 9), (1.8, 10.5), (1.8, 12.5), (1.2, 14)], VERMILION, PLAIN, b, seg=12)


@bicon('BLDG_Fief')
def _(sc, rng):
    # a fief's seat: a small castle keep on its sloped stone base
    plinth(sc, -26, -24, 26, 24)
    b = nb(sc)
    G.box(sc, -16, -16, 16, 16, 0, 8, STONE_C, STONE, b)
    BD.asian_hall(sc, (-14, -14, 14, 14), 36, dict(JP, podium=STONE_C), rng, two_tier=True, podium=False)
    flag(sc, 20, 18, 0, 22, VERMILION)


@bicon('BLDG_Shrine')
def _(sc, rng):
    # a shrine of the sea's kami: a vermilion torii before a small hall
    plinth(sc, -28, -24, 28, 24, top=lin('9aa86a'))
    BD.asian_hall(sc, (-22, -20, 2, 0), 18, dict(JP, hutwalls=[lin('d8cdb2')]), rng, two_tier=False)
    b = nb(sc)
    for y in (6, 20):
        G.lathe(sc, 14, y, [(1.1, 0), (0.9, 20)], VERMILION, PLAIN, b, seg=10)
    G.box(sc, 12, 2, 16, 24, 15, 16.5, VERMILION, PLAIN, b)
    G.box(sc, 11, 0, 17, 26, 19.5, 21.5, lin('2e2a28'), PLAIN, b)
    tree(sc, -20, 16, 18, rng)


@bicon('BLDG_Decree')
def _(sc, rng):
    # an imperial decree: a gate hall, the edict posted on a board before it
    plinth(sc, -28, -22, 28, 22, top=lin('c2b48e'), mat=PAVING)
    BD.asian_hall(sc, (-24, -18, 12, 4), 26, JP, rng, two_tier=False)
    b = nb(sc)
    for y in (8, 20):
        G.lathe(sc, 18, y, [(0.6, 0), (0.6, 13)], TIMBER, WOOD, b, seg=6)
    G.box(sc, 17.5, 7, 18.5, 21, 6, 13, PAPER, PLAIN, b)
    G.box(sc, 17, 6, 19, 22, 13, 14.5, lin('3a3a3e'), SLATE, b)
    for y in (10, 12.5, 15, 17.5):
        G.box(sc, 18.6, y, 18.8, y + 0.6, 7, 12, lin('2a2420'), PLAIN, b)


@bicon('BLDG_Council')
def _(sc, rng):
    # (MDJ's Statue of Zeus) a great council hall of the daimyo, two-roofed,
    # gilt at the ridge, a banner at each corner
    plinth(sc, -32, -28, 32, 28, top=lin('c2b48e'), mat=PAVING)
    BD.asian_hall(sc, (-26, -22, 20, 14), 34, dict(JP, podium=STONE_C), rng, two_tier=True)
    b = nb(sc)
    G.box(sc, -12, -6, 6, -4, 33, 34.5, lin('c9a85a'), GOLD, b)
    for (x, y) in ((24, -18), (24, 18), (-24, 18)):
        flag(sc, x, y, 0, 24, VERMILION)
