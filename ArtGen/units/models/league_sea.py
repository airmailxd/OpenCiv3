"""The League modes' ships (catalog/league_mdj.py): MDJ's Warship, drawn
as a Japanese atakebune.

Registers the ship archetype's design 'atakebune' (models/ships.py): a
broad, flat wooden hull whose deck is walled in by a boxy armored house of
dark planks with rows of loopholes, a civ-colored curtain band along the
top of the walls, a two-storey tower with hip roofs amidships, one mast
with a square mat sail, a civ-colored banner at the stern and oars along
both sides. It shoots arrows like the Galley it is calibrated against.
"""
import numpy as np

import mesh as M
from models.ship_designs import design, Hull, deck_at
from models.ships import box, rod


def _roof(b, c, w, l, h, mat_name):
    """A hip roof with eaves flaring out: a stack of shrinking slabs."""
    x, y, z = c
    for i, k in enumerate((1.0, 0.82, 0.62, 0.40, 0.18)):
        b.add(box((x, y, z + h * i / 5), (w * k + 0.05 * (i == 0), l * k + 0.05 * (i == 0), h / 5 + 0.004),
                  b.m(mat_name), bevel=0.004))


@design('atakebune')
def atakebune(b, p):
    hull = b.set_hull(Hull(b.mats, L=2.3, B=0.66, D=0.14, draft=0.1, hb=0.03, sheer_stern=0.05, sheer_bow=0.05,
                           bow_p=2.4, stern_p=3.2, n=3.0, stripes=[(0.0, 0.02, 'wood_light'), (0.07, 0.09, 'wood_dark')]))
    zd = deck_at(hull, 0.0)
    wall_h = 0.24
    # the armored house: dark plank walls over most of the deck
    b.add(box((0, -0.06, zd + wall_h / 2), (0.60, 1.62, wall_h), b.m('wood_dark'), bevel=0.01))
    b.add(box((0, -0.06, zd + wall_h + 0.006), (0.56, 1.58, 0.012), b.m('deck'), bevel=0.003))
    # the curtain band in the civ color round the top of the walls
    b.add(box((0, -0.06, zd + wall_h - 0.035), (0.612, 1.632, 0.05), b.m('tint'), bevel=0.004))
    # loopholes: small dark squares along both walls
    for y in np.linspace(-0.75, 0.62, 8):
        for sx in (-1, 1):
            b.add(box((sx * 0.303, y, zd + 0.06), (0.008, 0.035, 0.028), b.m('black', '#141210')))
    # the tower amidships: two storeys, each with a hip roof
    yt = -0.30
    b.add(box((0, yt, zd + wall_h + 0.08), (0.34, 0.44, 0.16), b.m('wood'), bevel=0.008))
    _roof(b, (0, yt, zd + wall_h + 0.165), 0.46, 0.56, 0.08, 'roof')
    b.add(box((0, yt, zd + wall_h + 0.29), (0.20, 0.24, 0.10), b.m('wood'), bevel=0.006))
    _roof(b, (0, yt, zd + wall_h + 0.345), 0.30, 0.34, 0.07, 'roof')
    b.add(box((0, yt + 0.226, zd + wall_h + 0.08), (0.22, 0.008, 0.04), b.m('black')))
    b.add(M.sphere(0.022, b.m('gold'), center=(0, yt, zd + wall_h + 0.43), seg=8))
    # the mast and its mat sail ahead of the tower
    b.mast(0.42, zd + wall_h, 1.15, r=0.03, top='finial')
    b.square_sail(0.42, zd + wall_h + 1.1, 1.0, 1.0, 0.72, 'none', billow=0.12, nu=18, nv=12)
    # the stern banner in the civ color
    b.flag((0, -0.95, zd + wall_h + 0.5), w=0.14, h=0.22, droop=0.4)
    b.add(rod((0, -0.95, zd + wall_h), (0, -0.95, zd + wall_h + 0.5), 0.01, b.m('wood_dark'), seg=5))
    # a blunt bow plate and the steering oar
    b.add(box((0, 1.0, zd + 0.06), (0.36, 0.06, 0.14), b.m('wood_dark'), bevel=0.01))
    b.add(rod((0.0, -1.1, zd + 0.05), (0.0, -1.3, -0.06), 0.016, b.m('wood_light'), seg=5))
    b.set_oars(12, -0.70, 0.62, 0.50, dip=30)
    b.weapon = dict(kind='arrows', src=(0.0, -0.2, zd + 0.35), dir=(1.0, 0.25, 0.1), reach=1.4, n=6, volleys=3,
                    gap=0.22, t=0.1, flight=0.3)
