"""The League modes' aircraft (catalog/league_air.py): Modern's Fighter,
which the mod draws as a Grumman F4F Wildcat (its Art/Units/Fighter).

Registers the aircraft archetype's model 'wildcat' (models/aircraft.py):
a short barrel of a fuselage behind a big round engine cowling, a
greenhouse canopy set high, square-tipped wings set low on the body, a
squared tailplane and a tall rounded fin. Blue-grey above; the civ color
on the cowling's ring, the roundels and a band round the rear fuselage.
"""
import mesh as M
from models import aircraft as AC
from models import vparts as VP


def build_wildcat(V):
    m = V.mats
    L = 8.8
    body = [AC.fuselage([(-4.7, 0.05), (-4.3, 0.22), (-2.6, 0.48), (-0.8, 0.70), (1.0, 0.80), (2.3, 0.80), (2.9, 0.76)],
                        m['paint'], sx=0.92, sz=1.0)]
    # the engine cowling: a fat drum, its front ring in the civ color, the dark engine face inside
    body.append(AC.fuselage([(2.5, 0.84), (3.3, 0.84), (3.42, 0.78)], m['paint_dark'], seg=16))
    body.append(AC.fuselage([(3.05, 0.86), (3.38, 0.86)], m['band'], seg=16))
    body.append(AC.fuselage([(3.38, 0.62), (3.44, 0.0)], m['steel'], seg=14))
    body.append(AC.fuselage([(3.42, 0.22), (3.6, 0.2), (3.85, 0.0)], m['steel']))                  # spinner
    # the canopy, high on the back, and the spine behind it
    body.append(VP.icosphere(0.42, m['glass'], (0, 0.15, 0.72), sub=2, smooth=True, squash=(0.82, 1.9, 0.75)))
    body.append(M.box(0.32, 2.6, 0.30, m['paint'], center=(0, -1.6, 0.52)).transformed(M.rot_x(-4)))
    # a band round the rear fuselage
    body.append(AC.fuselage([(-2.3, 0.53), (-1.95, 0.57)], m['band'], sx=0.95, sz=1.03))
    # the wing: low on the body, straight and tapered, its tips cut square
    poly = [(0.35, 1.35), (2.6, 1.15), (5.55, 0.70), (5.75, 0.55), (5.75, -0.30), (5.5, -0.42), (2.6, -0.70), (0.35, -1.0)]
    ws = {}
    for side, name in ((1, 'wing_r'), (-1, 'wing_l')):
        parts = [VP.camo(AC.wing(poly, 0.20, m['paint'], z=-0.42, dihedral=5, side=side), m['paint'],
                         [(0.12, m['paint_dark'])], max_edge=0.35, seed=3 + side, scale=1.0)]
        parts.append(AC.roundel(0.68, m['band'], m['star']).transformed(
            M.translate(side * 4.2, 0.25, -0.42 + 0.11 + 4.2 * 0.087)))
        parts.append(VP.tube((side * 2.9, 1.1, -0.40), (side * 2.9, 1.7, -0.40), 0.04, 0.035, m['steel'], seg=6))
        ws[name] = M.Mesh.concat(parts)
    tp = [(0.1, -3.55), (1.7, -3.75), (2.0, -3.95), (2.0, -4.3), (0.1, -4.5)]
    tail = [AC.wing(tp, 0.10, m['paint'], z=0.05), AC.wing(tp, 0.10, m['paint'], z=0.05, side=-1),
            VP.side_prism([(-3.4, 0.4), (-3.9, 1.5), (-4.25, 1.75), (-4.6, 1.7), (-4.85, 1.3), (-4.8, 0.2)], 0.12, m['paint'])]
    parts = dict(ws)
    parts['body'] = VP.camo(M.Mesh.concat(body), m['paint'], [(0.15, m['paint_dark'])], max_edge=0.35, seed=8, scale=1.0)
    parts['tail'] = M.Mesh.concat(tail)
    return dict(parts=parts, props=[((0, 3.55, 0.0), 1.45, 3, 'y')], guns=[(sx * 2.9, 1.75, -0.40) for sx in (-1, 1)],
                pylons=[], length=L)


AC.BUILDERS.setdefault('wildcat', build_wildcat)
AC.PAINTS.setdefault('wildcat', {'paint': '#5e6a82', 'paint_dark': '#4a5468', 'band': '#e8e8e8', 'star': '#e8e8e8',
                                 'glass': ('#9ab8c8', 0.7, 40), 'steel': ('#3a3a3c', 0.5, 24), 'blade': '#262626',
                                 'blade_tip': '#d8c040', 'bomb': '#3c3e38'})
