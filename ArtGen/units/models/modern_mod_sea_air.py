"""Modern's (C7/Lua/modern) own ships and plane: the Pirate, the Troop Ship
and the Fighter/Bomber (catalog/modern_mod_sea_air.py), calibrated against
the mod's pictures (civ3art.MOD_UNIT_ORIGINALS) but drawn from these models.

The ships are designs of the 'ship' archetype (models/ships.py), the plane a
builder of the 'aircraft' archetype (models/aircraft.py); both are
registered here, so the stock modules stay as they are.

* 'steam_junk' (the Pirate): a low grey steam raider with a ram bow, a
  wooden deck, one funnel, a bow gun, and three masts of junk sails
  (battened lug sails, braced to show their face) in civ-colored panels.
* 'troopship' (the Troop Ship): a long steamer with a black-topped grey
  hull, a rust deck, civ-colored hatch covers, a white bridge house amidships
  with lifeboats, one tall black funnel, and derricks fore and aft.
* 'p38' (the Fighter/Bomber): a twin-boom, twin-engine prop fighter-bomber
  in olive drab: a central cockpit pod, the booms ending in two fins joined
  by the tailplane, civ-colored bands round the booms and on the wing tips,
  a bomb under each wing root.
"""
import math

import numpy as np

import mesh as M
from models import aircraft as AC
from models import vparts as VP
from models.ship_designs import design, steel_hull, deck_at, funnel, deck_patch, lattice_mast
from models.ships import rod, box


# ===================================================================== ships

def _panels(mats, n, tint_first=False, base='sail', tint='sail_tint'):
    """A sail's material by (u, v): n horizontal panels, alternately the civ
    color and the plain cloth, with a dark batten line between them."""
    b, t, d = mats[base], mats[tint], mats['batten']

    def f(u, v):
        x = v * n
        k = int(min(n - 1, x))
        if 0 < k and x - k < 0.045:
            return d
        return t if (k % 2 == 0) == tint_first else b
    return f


def junk_sail(b, y, z_bot, h, w, rise=0.12, n=4, brace=22.0, billow=0.05):
    """A junk (battened lug) sail on the mast at y, its yard slanting up
    aft-ward to starboard; braced so its face shows from the side as Civ3's
    sails do."""
    br = math.radians(brace)
    ca, sa = math.cos(br), math.sin(br)

    def rot(px, pz):
        return (px * ca, y + px * sa, pz)
    top_l, top_r = rot(-w * 0.42, z_bot + h), rot(w * 0.58, z_bot + h + rise)
    bot_r, bot_l = rot(w * 0.62, z_bot), rot(-w * 0.46, z_bot)
    b.add(rod(rot(-w * 0.46, z_bot + h - 0.005), rot(w * 0.62, z_bot + h + rise + 0.005), 0.011, b.m('wood_dark'), seg=6))
    b.add(rod(rot(-w * 0.48, z_bot), rot(w * 0.64, z_bot), 0.009, b.m('wood_dark'), seg=6))
    b.sails.append(dict(kind='square', corners=[top_l, top_r, bot_r, bot_l], nu=10, nv=12,
                        mat_fn=_panels(b.mats, n), dir=np.array([-sa, ca, 0], np.float32),
                        amount=billow, h=h, w=w, foot=0.01))


@design('steam_junk')
def steam_junk(b, p):
    b.m('batten', '#7a7266')
    hull = steel_hull(b, 3.0, 0.62, 0.17, draft=0.12, rake=-0.12, transom=0.55, n=2.8, side='steel', deck='deck_wood',
                      bow_p=1.6, stern_p=2.4, sheer_bow=0.05, stripes=[(0.0, 0.012, 'steel_light')],
                      bands=((-0.004, 0.022, 'black'),))
    zd = deck_at(hull, 0.0)
    # paddle boxes amidships, their rims in the civ color
    for sx in (-1, 1):
        x = sx * (hull.half_beam(hull.s_of(-0.05)) + 0.05)
        disc = M.cylinder(0.2, 0.2, 0.1, b.m('steel_dark'), seg=18, z0=-0.05).transformed(M.rot_y(90))
        rim = M.cylinder(0.215, 0.215, 0.06, b.m('tint'), seg=18, z0=-0.03).transformed(M.rot_y(90))
        b.add(M.Mesh.concat([disc, rim]).transformed(M.translate(x, -0.05, zd - 0.02)))
    # a raised steel forecastle and a deckhouse aft
    b.add(box((0, 1.08, zd + 0.035), (0.34, 0.42, 0.07), b.m('steel'), bevel=0.01))
    b.add(box((0, -0.95, zd + 0.07), (0.36, 0.4, 0.14), b.m('steel_dark'), bevel=0.012))
    b.add(box((0, -0.95, zd + 0.145), (0.3, 0.32, 0.012), b.m('deck_wood'), bevel=0.004))
    b.add(box((0, -0.745, zd + 0.09), (0.26, 0.01, 0.035), b.m('glass')))
    # the funnel between the main and mizzen masts, smoking
    funnel(b, -0.36, zd, 0.46, 0.065, 0.065, mat_name='black', cap='black', rake=0.05)
    b.smoke_stacks.append(np.array([0, -0.38, zd + 0.48], np.float32))
    b.add(box((0, -0.36, zd + 0.04), (0.24, 0.24, 0.08), b.m('steel_dark'), bevel=0.01))
    # three masts of junk sails, the tallest amidships
    for y, h, w, z0 in ((0.8, 0.85, 0.66, 0.14), (0.14, 1.12, 0.84, 0.14), (-0.7, 0.8, 0.6, 0.24)):
        top = b.mast(y, zd, h + z0 + 0.2, r=0.022, mat_name='wood_dark')
        junk_sail(b, y, zd + z0, h, w, rise=0.16, n=5)
        if y == 0.14:
            b.flag(top, w=0.18, h=0.08, pole=False)
    # stays from the bow to the foremast and from the masts aft
    b.line((0, 1.5, zd + 0.03), (0, 0.8, zd + 1.15))
    b.line((0, 0.14, zd + 1.42), (0, -0.7, zd + 1.2))
    b.line((0, -0.7, zd + 1.2), (0, -1.45, zd + 0.12))
    # the bow gun
    b.turret((0, 1.12, zd + 0.07), shape='box', size=0.14, h=0.07, barrel=0.2, n=1, rest=0.0, aim=0.0,
             mat_name='steel_dark', gun_mat='black', flash=1.1, elev=6)
    b.weapon = dict(kind='turrets', t=0.18)
    b.extra_wake = 0.9


@design('troopship')
def troopship(b, p):
    hull = steel_hull(b, 3.6, 0.6, 0.2, draft=0.1, rake=0.14, transom=0.7, n=4.0, side='steel', deck='deck',
                      bow_p=3.0, stern_p=4.0, sheer_bow=0.06, planks=0.0,
                      stripes=[(0.0, 0.03, 'black'), (0.03, 0.04, 'steel_light')],
                      bands=((-0.004, 0.03, 'bottom'),))
    zd = deck_at(hull, 0.0)
    # four civ-colored hatch covers on raised coamings, two forward and two aft
    for y in (1.18, 0.7, -0.74, -1.2):
        b.add(box((0, y, zd + 0.025), (0.4, 0.34, 0.05), b.m('coaming', '#56524c')))
        b.add(box((0, y, zd + 0.055), (0.36, 0.3, 0.012), b.m('tint')))
        b.add(box((0, y, zd + 0.062), (0.36, 0.012, 0.006), b.m('coaming')))
    # the bridge house amidships, two decks, with lifeboats beside it
    b.add(box((0, 0.0, zd + 0.08), (0.46, 0.66, 0.16), b.m('house', '#d6d2c6'), bevel=0.01))
    b.add(box((0, 0.14, zd + 0.2), (0.38, 0.3, 0.1), b.m('house'), bevel=0.01))
    b.add(box((0, 0.295, zd + 0.21), (0.32, 0.01, 0.035), b.m('glass')))
    b.add(box((0, 0.14, zd + 0.255), (0.44, 0.34, 0.012), b.m('steel_dark'), bevel=0.003))
    for sx in (-1, 1):
        boat = M.loft([(0.0, 0.035, 0.1, 0, 0), (0.04, 0.045, 0.12, 0, 0), (0.05, 0.042, 0.11, 0, 0)], b.m('white'),
                      seg=10)
        b.add(boat.transformed(M.translate(sx * 0.17, -0.2, zd + 0.16)))
    # one tall black funnel with a steel collar, smoking
    funnel(b, -0.16, zd + 0.16, 0.56, 0.085, 0.1, mat_name='black', cap='black', rake=0.06)
    b.add(M.loft([(zd + 0.16, 0.1, 0.115, 0, -0.16), (zd + 0.24, 0.097, 0.112, 0, -0.165)], b.m('steel_dark'), seg=14))
    b.smoke_stacks.append(np.array([0, -0.2, zd + 0.74], np.float32))
    # derricks: a king post fore and aft, each with two booms over the hatches
    for y, sgn in ((0.94, 1), (-0.97, -1)):
        top = np.array([0, y, zd + 0.6], np.float32)
        b.add(rod((0, y, zd), top, 0.018, b.m('steel_dark'), seg=6, r1=0.012))
        b.add(rod((-0.1, y, zd + 0.4), (0.1, y, zd + 0.4), 0.01, b.m('steel_dark'), seg=4))
        for sx in (-1, 1):
            foot = np.array([sx * 0.08, y, zd + 0.06], np.float32)
            tip = np.array([sx * 0.09, y + sgn * 0.3, zd + 0.36], np.float32)
            b.add(rod(foot, tip, 0.009, b.m('steel_dark'), seg=4))
            b.line(top, tip, r=0.003)
    b.line((0, 1.75, zd + 0.1), (0, 0.94, zd + 0.6), r=0.003)
    b.line((0, -0.97, zd + 0.6), (0, -1.72, zd + 0.1), r=0.003)
    b.flag((0, -1.74, zd + 0.24), w=0.12, h=0.07)
    # a light gun on the stern
    b.turret((0, -1.5, zd + 0.02), shape='box', size=0.1, h=0.05, barrel=0.13, n=1, rest=180.0, aim=180.0,
             mat_name='steel', gun_mat='black', flash=0.9, elev=12)
    b.weapon = dict(kind='turrets', t=0.15)


# ===================================================================== the plane

def build_p38(V):
    """A twin-boom, twin-engine fighter-bomber (a P-38 Lightning)."""
    m = V.mats
    L = 11.5
    span = 15.8
    boom_x = 2.4
    # the central pod: cockpit and guns
    body = [AC.fuselage([(-2.6, 0.05), (-2.2, 0.45), (-0.4, 0.75), (1.4, 0.72), (3.0, 0.5), (3.9, 0.0)],
                        m['paint'], sx=0.9, sz=0.95)]
    body.append(VP.icosphere(0.5, m['glass'], (0, 0.9, 0.55), sub=2, smooth=True, squash=(0.78, 1.7, 0.75)))
    body.append(AC.fuselage([(3.5, 0.2), (3.95, 0.12), (4.2, 0.0)], m['paint_dark']).transformed(
        M.translate(0, 0, 0.05)))
    # the wing: one straight, tapered wing through the pod and the booms
    poly = [(0.0, 1.3), (boom_x + 0.9, 1.3), (span / 2 - 0.6, 0.6), (span / 2, 0.2), (span / 2, -0.35),
            (span / 2 - 0.6, -0.5), (boom_x + 0.9, -0.75), (0.0, -0.75)]
    ws = {}
    props = []
    for side, name in ((1, 'wing_r'), (-1, 'wing_l')):
        parts = [VP.camo(AC.wing(poly, 0.22, m['paint'], z=-0.05, dihedral=3, side=side), m['paint'],
                         [(0.18, m['paint_dark'])], max_edge=0.5, seed=5 + side, scale=0.8)]
        # civ-colored wing tip
        tip = [(span / 2 - 1.3, 0.62), (span / 2 - 0.6, 0.6), (span / 2, 0.2), (span / 2, -0.35),
               (span / 2 - 0.6, -0.5), (span / 2 - 1.3, -0.55)]
        parts.append(AC.wing(tip, 0.25, m['band'], z=-0.05, dihedral=3, side=side))
        # the boom: engine nacelle, supercharger, tail boom
        bx = side * boom_x
        boom = AC.fuselage([(-L * 0.62, 0.12), (-L * 0.45, 0.32), (-1.0, 0.55), (1.2, 0.68), (3.0, 0.62),
                            (3.6, 0.45), (3.75, 0.0)], m['paint'], sx=0.85, sz=1.0)
        parts.append(VP.camo(boom.transformed(M.translate(bx, 0, -0.05 + boom_x * 0.05)), m['paint'],
                             [(0.18, m['paint_dark'])], max_edge=0.5, seed=12 + side, scale=0.8))
        zb = -0.05 + boom_x * 0.05
        # civ-colored bands round the boom, ahead of the fins
        parts.append(AC.fuselage([(-L * 0.42, 0.34), (-L * 0.34, 0.37)], m['band'], sx=0.88, sz=1.03).transformed(
            M.translate(bx, 0, zb)))
        parts.append(AC.fuselage([(3.6, 0.22), (3.95, 0.15), (4.15, 0.0)], m['steel']).transformed(
            M.translate(bx, 0, zb)))           # spinner
        parts.append(M.box(0.25, 0.9, 0.22, m['paint_dark'], center=(bx, -0.4, zb + 0.6)))     # supercharger
        props.append(((bx, 4.0, zb), 1.55, 3, 'y'))
        # the bomb under the wing root, on its pylon
        parts.append(M.box(0.12, 0.7, 0.3, m['paint_dark'], center=(side * 1.25, 0.2, -0.3)))
        parts.append(M.Mesh.concat([VP.tube((0, -0.8, 0), (0, 0.7, 0), 0.26, 0.26, m['bomb'], seg=8),
                                    VP.tube((0, 0.7, 0), (0, 1.05, 0), 0.26, 0.04, m['bomb'], seg=8),
                                    VP.tube((0, -1.2, 0), (0, -0.8, 0), 0.12, 0.26, m['bomb'], seg=8),
                                    M.box(0.6, 0.12, 0.05, m['bomb'], center=(0, -1.15, 0)),
                                    M.box(0.05, 0.12, 0.6, m['bomb'], center=(0, -1.15, 0))]).transformed(
            M.translate(side * 1.25, 0.2, -0.65)))
        ws[name] = M.Mesh.concat(parts)
    # the tail: a fin on each boom, joined by the tailplane
    tail = []
    zt = -0.05 + boom_x * 0.05
    for sx in (-1, 1):
        fin = VP.side_prism([(-L * 0.5, -0.5), (-L * 0.52, 1.35), (-L * 0.6, 1.4), (-L * 0.65, -0.3)], 0.12, m['paint'])
        tail.append(fin.transformed(M.translate(sx * boom_x, 0, zt)))
        tail.append(VP.side_prism([(-L * 0.51, 0.95), (-L * 0.52, 1.37), (-L * 0.6, 1.42), (-L * 0.62, 0.95)], 0.14,
                                  m['band']).transformed(M.translate(sx * boom_x, 0, zt)))
    tail.append(AC.wing([(0.0, -L * 0.5), (boom_x + 0.2, -L * 0.5), (boom_x + 0.2, -L * 0.6), (0.0, -L * 0.6)], 0.12,
                        m['paint'], z=zt + 0.15))
    tail.append(AC.wing([(0.0, -L * 0.5), (boom_x + 0.2, -L * 0.5), (boom_x + 0.2, -L * 0.6), (0.0, -L * 0.6)], 0.12,
                        m['paint'], z=zt + 0.15, side=-1))
    parts = dict(ws)
    parts['body'] = VP.camo(M.Mesh.concat(body), m['paint'], [(0.18, m['paint_dark'])], max_edge=0.5, seed=9, scale=0.8)
    parts['tail'] = M.Mesh.concat(tail)
    return dict(parts=parts, props=props, guns=[(sx * 0.2, 4.0, 0.0) for sx in (-1, 1)], pylons=[], length=L)


AC.BUILDERS.setdefault('p38', build_p38)
AC.PAINTS.setdefault('p38', {'paint': '#72744e', 'paint_dark': '#55583a', 'band': '#e8e8e8',
                             'glass': ('#9ab8c8', 0.7, 40), 'steel': ('#3a3a3a', 0.5, 24), 'blade': '#262626',
                             'blade_tip': '#d8c040', 'bomb': '#3c3e38'})
