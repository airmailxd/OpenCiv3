"""Aircraft: Fighter, Jet Fighter, F-15, Stealth Fighter, Bomber, Stealth
Bomber and Helicopter.

Civ3 draws aircraft flying, with their shadow on the ground well below.
The model is the plane at an altitude above its ground point (z = 0); the
altitude and the ground point are fitted to the original's DEFAULT frame
(the plane where the original's plane is, the renderer's shadow near where
the original's shadow is). Propellers and rotors spin, planes bob and bank a
little in flight, fighters fire guns or missiles, bombers' bomb runs drop a
stick of bombs that explode on the ground, deaths blow the plane apart and
the pieces tumble down to a fireball on the ground.

Catalog: 'model' (builder name), 'colors', 'tint', 'weapon' ('guns' |
'missile'), 'fire' (normalized time of the shot).
"""
import math

import numpy as np

import mesh as M
import rig
import render as R
from models import archetype
from models import vparts as VP
from models import vfx as FX
from models.vehicles import VehicleBase, flash_time, _orient_y, SIN_E, COS_E


def fuselage(profile, mat, seg=14, sx=1.0, sz=1.0):
    """A body of revolution along +y from a profile [(y, r), ...] (r may be
    0 at the ends), squashed by sx (width) and sz (height)."""
    m = M.lathe([(y, r) for y, r in profile], mat, seg)
    # lathe builds along z: turn z -> y
    return m.transformed(M.scale(sx, 1, sz) @ M.rot_x(-90))


def wing(poly, thick, mat, z=0.0, dihedral=0.0, side=1):
    """A wing from its top-view outline [(x, y), ...] (x >= 0 for the right
    wing; mirrored for side = -1), with a dihedral angle (degrees)."""
    w = VP.prism(poly, thick, mat, z0=-thick / 2)
    w = w.transformed(M.translate(0, 0, z) @ M.rot_y(-dihedral))
    if side < 0:
        w = VP.mirror_x(w)
    return w


def roundel(r, mat, ring=None, thick=0.012):
    parts = [M.cylinder(r, r, thick, mat, seg=16)]
    if ring is not None:
        parts.append(M.cylinder(r * 0.55, r * 0.55, thick * 1.5, ring, seg=14))
    return M.Mesh.concat(parts)


# ------------------------------------------------------------ builders
# Each returns a dict: parts {name: mesh} ('body', 'wing_l', 'wing_r', 'tail'),
# props [(center, radius, blades, axis)], guns [muzzle points], pylons
# [missile mount points], length (m).

def build_fighter(V):
    """A WWII fighter (Spitfire-like): elliptical wings with civ-colored
    roundels and bands, a three-bladed propeller."""
    m = V.mats
    body = [fuselage([(-4.6, 0.02), (-4.2, 0.22), (-2.0, 0.5), (0.5, 0.68), (2.3, 0.62), (3.0, 0.5), (3.15, 0.0)],
                     m['paint'], sx=0.85, sz=1.0)]
    body.append(VP.icosphere(0.42, m['glass'], (0, 0.2, 0.55), sub=2, smooth=True, squash=(0.8, 1.6, 0.7)))
    body.append(fuselage([(3.05, 0.25), (3.4, 0.2), (3.75, 0.0)], m['steel']))         # spinner
    body.append(M.box(0.12, 4.0, 0.06, m['band'], center=(0, -1.6, 0.6)))
    for sx in (-1, 1):
        body.append(M.box(0.05, 0.6, 0.5, m['band'], center=(sx * 0.48, -2.6, 0.0)))
    ws = []
    poly = [(0.3, 1.6), (2.5, 1.35), (4.2, 0.9), (5.3, 0.3), (5.5, -0.2), (4.6, -0.6), (2.5, -0.85), (0.3, -0.95)]
    for side, name in ((1, 'wing_r'), (-1, 'wing_l')):
        parts = [VP.camo(wing(poly, 0.16, m['paint'], z=-0.3, dihedral=6, side=side), m['paint'],
                         [(0.12, m['paint_dark'])], max_edge=0.3, seed=4 + side, scale=1.1)]
        rd = roundel(0.55, m['band'], m['paint_dark']).transformed(M.translate(side * 3.6, 0.15, -0.3 + 0.08 + 3.6 * 0.105))
        parts.append(rd)
        parts.append(VP.tube((side * 1.8, 1.4, -0.32), (side * 1.8, 2.1, -0.32), 0.04, 0.035, m['steel'], seg=6))
        ws.append((name, M.Mesh.concat(parts)))
    tail = [wing([(0.1, -3.6), (1.6, -3.85), (1.8, -4.2), (0.1, -4.45)], 0.1, m['paint'], z=0.15),
            wing([(0.1, -3.6), (1.6, -3.85), (1.8, -4.2), (0.1, -4.45)], 0.1, m['paint'], z=0.15, side=-1),
            VP.side_prism([(-3.6, 0.4), (-4.2, 1.55), (-4.6, 1.5), (-4.65, 0.2)], 0.1, m['paint'])]
    parts = dict(ws)
    parts['body'] = VP.camo(M.Mesh.concat(body), m['paint'], [(0.2, m['paint_dark'])], max_edge=0.3, seed=9, scale=1.1)
    parts['tail'] = M.Mesh.concat(tail)
    return dict(parts=parts, props=[((0, 3.45, 0.0), 1.45, 3, 'y')], guns=[(sx * 1.8, 2.15, -0.32) for sx in (-1, 1)],
                pylons=[], length=8.4)


def build_jet(V, kind='mig'):
    """A twin-engine jet fighter. 'mig' (the Civ3 Jet Fighter, a MiG-29
    look: blended body, twin canted fins) or 'f15' (bigger square intakes,
    upright twin fins)."""
    m = V.mats
    L = 9.0
    body = [fuselage([(-L * 0.42, 0.25), (-L * 0.1, 0.62), (L * 0.15, 0.6), (L * 0.32, 0.45), (L * 0.45, 0.28),
                      (L * 0.55, 0.0)], m['paint'], sx=1.1, sz=0.75)]
    body.append(VP.icosphere(0.42, m['glass'], (0, L * 0.3, 0.42), sub=2, smooth=True, squash=(0.75, 1.8, 0.7)))
    # engine nacelles and intakes along the belly
    for sx in (-1, 1):
        body.append(fuselage([(-L * 0.45, 0.42), (-L * 0.1, 0.5), (L * 0.18, 0.45), (L * 0.2, 0.0)], m['paint_dark'],
                             sx=0.9, sz=0.8).transformed(M.translate(sx * 0.75, 0, -0.25)))
        if kind == 'f15':   # the F-15's big square intakes beside the cockpit
            body.append(M.box(0.85, 1.6, 0.95, m['paint_dark'], center=(sx * 0.95, L * 0.12, -0.05)))
            body.append(M.box(0.7, 0.05, 0.75, m['steel_dark'], center=(sx * 0.95, L * 0.12 + 0.81, -0.05)))
        else:
            body.append(M.box(0.75, 0.3, 0.6, m['paint_dark'], center=(sx * 0.75, L * 0.16, -0.25)))
        body.append(VP.axle_x(0.32, 0.01, m['steel'], center=(0, 0, 0)).transformed(
            M.translate(sx * 0.75, -L * 0.46, -0.25) @ M.rot_z(90)))
    # wings: a broad delta-ish shape
    if kind == 'f15':
        poly = [(0.6, L * 0.12), (1.2, L * 0.1), (5.6, -L * 0.08), (5.7, -L * 0.2), (1.2, -L * 0.24), (0.6, -L * 0.24)]
    else:
        poly = [(0.6, L * 0.22), (1.4, L * 0.12), (5.3, -L * 0.1), (5.3, -L * 0.2), (1.4, -L * 0.24), (0.6, -L * 0.24)]
    ws = {}
    for side, name in ((1, 'wing_r'), (-1, 'wing_l')):
        parts = [VP.camo(wing(poly, 0.12, m['paint'], z=0.05, side=side), m['paint'],
                         [(0.15 if kind == 'mig' else 0.3, m['paint_dark'])], max_edge=0.6, seed=7 + side, scale=0.5)]
        parts.append(roundel(0.5, m['band'], m['paint_dark']).transformed(M.translate(side * 3.3, -L * 0.12, 0.13)))
        parts.append(M.box(1.0, 0.3, 0.02, m['band'], center=(side * 4.6, -L * 0.17, 0.12)))
        ws[name] = M.Mesh.concat(parts)
    fins = []
    for sx in (-1, 1):
        cant = 12 if kind == 'mig' else 0
        fh = 2.0 if kind == 'mig' else 2.7
        fin = VP.side_prism([(-L * 0.2, 0.3), (-L * 0.38, fh), (-L * 0.45, fh), (-L * 0.47, 0.3)], 0.1, m['paint'])
        band = VP.side_prism([(-L * 0.36, fh - 0.4), (-L * 0.385, fh + 0.02), (-L * 0.45, fh + 0.02), (-L * 0.45, fh - 0.4)],
                             0.12, m['band'])
        fins.append(M.Mesh.concat([fin, band]).transformed(M.translate(sx * 0.95, 0, 0) @ M.rot_y(sx * cant)))
        fins.append(wing([(0.8, -L * 0.36), (2.6, -L * 0.45), (2.6, -L * 0.52), (0.8, -L * 0.5)], 0.08, m['paint'],
                         z=-0.1, side=sx))
    parts = dict(ws)
    parts['body'] = VP.camo(M.Mesh.concat(body), m['paint'], [(0.15 if kind == 'mig' else 0.3, m['paint_dark'])],
                            max_edge=0.6, seed=11, scale=0.5)
    parts['tail'] = M.Mesh.concat(fins)
    return dict(parts=parts, props=[], guns=[(0.5, L * 0.35, 0.1)],
                pylons=[(sx * 2.4, -L * 0.05, -0.35) for sx in (-1, 1)], length=L, exhaust=[(sx * 0.75, -L * 0.47, -0.25) for sx in (-1, 1)])


def build_f117(V):
    """A faceted stealth fighter: a flat arrowhead of facets, swept wings,
    a V tail, civ-colored chevrons on top."""
    m = V.mats
    L = 8.5
    body = VP.sweep([(-L * 0.5, [(-1.1, -0.2), (1.1, -0.2), (0.6, 0.35), (-0.6, 0.35)]),
                     (-L * 0.1, [(-1.4, -0.3), (1.4, -0.3), (0.0, 0.85), (0.0, 0.85)]),
                     (L * 0.25, [(-0.6, -0.25), (0.6, -0.25), (0.0, 0.55), (0.0, 0.55)]),
                     (L * 0.5, [(-0.02, -0.02), (0.02, -0.02), (0.0, 0.02), (0.0, 0.02)])], m['paint'])
    canopy = VP.sweep([(L * 0.05, [(-0.45, 0.45), (0.45, 0.45), (0.0, 0.75), (0.0, 0.75)]),
                       (L * 0.28, [(-0.05, 0.3), (0.05, 0.3), (0.0, 0.35), (0.0, 0.35)])], m['glass'])
    ws = {}
    poly = [(0.8, L * 0.18), (5.2, -L * 0.36), (4.4, -L * 0.42), (0.8, -L * 0.3)]
    for side, name in ((1, 'wing_r'), (-1, 'wing_l')):
        chev = wing([(0.9, L * 0.03), (2.4, -L * 0.17), (2.1, -L * 0.21), (0.9, -L * 0.04)], 0.02, m['band'], z=0.1,
                    side=side)
        ws[name] = M.Mesh.concat([wing(poly, 0.12, m['paint'], z=0.0, side=side), chev])
    tail = []
    for sx in (-1, 1):
        tail.append(VP.side_prism([(-L * 0.3, 0.2), (-L * 0.45, 1.5), (-L * 0.52, 1.5), (-L * 0.48, 0.2)], 0.08,
                                  m['paint']).transformed(M.translate(sx * 0.4, 0, 0) @ M.rot_y(sx * 40)))
    parts = dict(ws)
    parts['body'] = M.Mesh.concat([body, canopy])
    parts['tail'] = M.Mesh.concat(tail)
    return dict(parts=parts, props=[], guns=[], pylons=[(sx * 0.6, 0.0, -0.35) for sx in (-1, 1)], length=L,
                exhaust=[(0, -L * 0.5, 0.1)])


def build_b17(V):
    """A four-engined WWII heavy bomber (a B-17): long round fuselage, a
    tall fin, four propellers, olive drab with civ-colored roundels and
    tail markings, a glazed nose."""
    m = V.mats
    L = 18.0
    body = [fuselage([(-L * 0.5, 0.1), (-L * 0.42, 0.45), (-L * 0.2, 0.95), (L * 0.15, 1.05), (L * 0.38, 0.95),
                      (L * 0.46, 0.7), (L * 0.5, 0.0)], m['paint'], sx=0.9, sz=1.0)]
    body.append(fuselage([(L * 0.44, 0.75), (L * 0.48, 0.6), (L * 0.505, 0.0)], m['glass']))
    body.append(VP.icosphere(0.5, m['glass'], (0, L * 0.3, 0.85), sub=2, smooth=True, squash=(0.9, 1.4, 0.6)))
    body.append(VP.icosphere(0.42, m['glass'], (0, L * 0.12, 1.0), sub=2, smooth=True))     # top turret
    body.append(VP.icosphere(0.4, m['glass'], (0, -L * 0.02, -0.95), sub=2, smooth=True))   # ball turret
    for sx in (-1, 1):
        body.append(VP.side_prism([(L * 0.05, -0.1), (L * 0.05, 0.3), (L * 0.0, 0.3), (L * 0.0, -0.1)], 0.04,
                                  m['band']).transformed(M.translate(sx * 0.95, 0, 0)))
    ws = {}
    poly = [(0.8, L * 0.17), (12.5, L * 0.07), (15.5, L * 0.04), (15.6, -L * 0.04), (12.5, -L * 0.07), (0.8, -L * 0.1)]
    props = []
    for side, name in ((1, 'wing_r'), (-1, 'wing_l')):
        parts = [wing(poly, 0.3, m['paint'], z=-0.2, dihedral=4, side=side)]
        for k, x in enumerate((3.6, 7.0)):
            nac = fuselage([(L * 0.02, 0.45), (L * 0.18, 0.55), (L * 0.25, 0.5), (L * 0.27, 0.0)], m['paint_dark'])
            parts.append(nac.transformed(M.translate(side * x, 0, -0.1 + x * 0.07)))
            props.append(((side * x, L * 0.28, -0.1 + x * 0.07), 1.6, 3, 'y'))
        parts.append(roundel(0.9, m['band'], m['paint_dark']).transformed(M.translate(side * 11.0, 0.0, -0.2 + 11 * 0.07 + 0.17)))
        ws[name] = M.Mesh.concat(parts)
    tail = [wing([(0.2, -L * 0.36), (4.8, -L * 0.43), (4.8, -L * 0.48), (0.2, -L * 0.5)], 0.18, m['paint'], z=0.25),
            wing([(0.2, -L * 0.36), (4.8, -L * 0.43), (4.8, -L * 0.48), (0.2, -L * 0.5)], 0.18, m['paint'], z=0.25, side=-1),
            VP.side_prism([(-L * 0.18, 0.7), (-L * 0.38, 3.9), (-L * 0.47, 4.0), (-L * 0.5, 0.4)], 0.2, m['paint']),
            VP.side_prism([(-L * 0.36, 3.2), (-L * 0.385, 3.92), (-L * 0.47, 4.02), (-L * 0.475, 3.2)], 0.24, m['band'])]
    for sx in (-1, 1):
        tail.append(M.box(0.9, 0.25, 0.04, m['band'], center=(sx * 4.3, -L * 0.455, 0.36)))
    parts = dict(ws)
    parts['body'] = M.Mesh.concat(body)
    parts['tail'] = M.Mesh.concat(tail)
    return dict(parts=parts, props=props, guns=[(0, L * 0.5, -0.2), (0, L * 0.12, 1.3), (0, -L * 0.5, 0.2)],
                pylons=[], length=L, bay=(0, 0.0, -0.8))


def build_b2(V):
    """A flying-wing stealth bomber (a B-2): a single faceted wing with a
    raised center hump and sawtooth trailing edge, civ-colored panels."""
    m = V.mats
    S = 1.0
    outline = [(0, 7.0), (3.0, 4.4), (16.0, -4.6), (15.2, -5.6), (11.0, -3.2), (8.0, -5.6), (4.5, -3.0),
               (2.2, -5.4), (0, -3.6)]
    ws = {}
    for side, name in ((1, 'wing_r'), (-1, 'wing_l')):
        w = VP.prism([(x, y) for x, y in outline], 0.35, m['paint'], z0=-0.2)
        pan = VP.prism([(4.2, 1.0), (8.5, -1.9), (7.4, -2.6), (3.6, 0.0)], 0.04, m['band'], z0=0.16)
        if side < 0:
            w, pan = VP.mirror_x(w), VP.mirror_x(pan)
        ws[name] = M.Mesh.concat([w, pan])
    hump = VP.sweep([(-3.6, [(-2.2, 0.1), (2.2, 0.1), (1.6, 0.6), (-1.6, 0.6)]),
                     (2.0, [(-2.6, 0.1), (2.6, 0.1), (1.6, 1.25), (-1.6, 1.25)]),
                     (6.4, [(-0.6, 0.1), (0.6, 0.1), (0.3, 0.35), (-0.3, 0.35)])], m['paint_dark'])
    glass = VP.prism([(-0.7, 4.0), (0.7, 4.0), (0.5, 3.2), (-0.5, 3.2)], 0.05, m['glass'], z0=0.95)
    intakes = [M.box(1.0, 0.6, 0.3, m['steel'], center=(sx * 2.4, 0.8, 0.95)) for sx in (-1, 1)]
    parts = dict(ws)
    parts['body'] = M.Mesh.concat([hump, glass] + intakes)
    parts['tail'] = M.Mesh()
    return dict(parts=parts, props=[], guns=[], pylons=[], length=12.6, bay=(0, 0.0, -0.3))


def build_chinook(V):
    """A tandem-rotor transport helicopter (a CH-47): long boxy cabin with
    round windows, rear pylon, two three-bladed rotors, civ-colored stripe
    and rotor hubs, side fuel pods."""
    m = V.mats
    L = 15.0
    sec = lambda w, h, z0: [(-w, z0), (w, z0), (w, z0 + h * 0.85), (w * 0.8, z0 + h), (-w * 0.8, z0 + h), (-w, z0 + h * 0.85)]
    body = VP.sweep([(-L * 0.45, sec(0.9, 1.6, 0.35)), (-L * 0.3, sec(1.15, 2.1, -0.15)), (L * 0.3, sec(1.15, 2.1, -0.15)),
                     (L * 0.42, sec(1.0, 1.7, -0.05)), (L * 0.48, sec(0.6, 0.9, 0.1))], m['paint'])
    pylon_r = VP.sweep([(-L * 0.47, sec(0.45, 1.6, 1.4)), (-L * 0.3, sec(0.6, 1.6, 1.4))], m['paint'])
    pylon_f = M.box(1.0, 1.6, 0.6, m['paint'], center=(0, L * 0.38, 2.1), bevel=0.05)
    stripe = [M.box(0.04, L * 0.62, 0.32, m['band'], center=(sx * 1.16, 0.0, 1.3)) for sx in (-1, 1)]
    stripe.append(M.box(0.8, L * 0.62, 0.04, m['band'], center=(0, 0.0, 1.97)))
    wins = []
    for sx in (-1, 1):
        for i in range(5):
            wins.append(VP.axle_x(0.18, 0.04, m['glass'], center=(sx * 1.16, -L * 0.2 + i * 1.3, 0.85)))
        wins.append(M.Mesh.concat([fuselage([(-L * 0.25, 0.05), (-L * 0.2, 0.4), (L * 0.15, 0.4), (L * 0.2, 0.05)],
                                            m['paint_dark'])]).transformed(M.translate(sx * 1.35, 0, -0.1)))
    cock = M.box(1.6, 1.2, 0.55, m['glass'], center=(0, L * 0.44, 0.9))
    gear = [VP.axle_x(0.3, 0.25, m['tire'], center=(sx * 1.0, y, -0.45)) for sx in (-1, 1) for y in (-L * 0.32, L * 0.28)]
    parts = {'body': M.Mesh.concat([body, pylon_r, pylon_f, cock] + stripe + wins + gear),
             'tail': M.Mesh(), 'wing_l': M.Mesh(), 'wing_r': M.Mesh()}
    hubs = [(0, -L * 0.39, 3.1), (0, L * 0.38, 2.5)]
    for h in hubs:
        parts['body'] = parts['body'] + M.cylinder(0.32, 0.25, 0.35, m['band'], seg=10, z0=h[2] - 0.3).transformed(
            M.translate(h[0], h[1], 0))
    return dict(parts=parts, props=[(h, 6.2, 3, 'z') for h in hubs], guns=[(0.9, L * 0.4, 0.4)], pylons=[],
                length=L, heli=True)


BUILDERS = {'fighter': build_fighter, 'mig': lambda V: build_jet(V, 'mig'), 'f15': lambda V: build_jet(V, 'f15'),
            'f117': build_f117, 'b17': build_b17, 'b2': build_b2, 'chinook': build_chinook}

PAINTS = {
    'fighter': {'paint': '#5c605a', 'paint_dark': '#363a34', 'band': '#e8e8e8', 'glass': ('#9ab8c8', 0.7, 40),
                'steel': ('#3a3a3a', 0.5, 24), 'blade': '#262626', 'bomb': '#3c3e38', 'blade_tip': '#d8c040'},
    'mig': {'paint': '#93a0aa', 'paint_dark': '#66727e', 'band': '#e8e8e8', 'glass': ('#4a6070', 0.8, 40),
            'steel': ('#4a4a4c', 0.5, 24), 'missile': '#e6e4de', 'missile_tip': '#b02a22', 'steel_dark': '#2a2a2c',
            'bomb': '#5a5e5a'},
    'f15': {'paint': '#8a8e92', 'paint_dark': '#62666a', 'band': '#e8e8e8', 'glass': ('#4a6070', 0.8, 40),
            'steel': ('#4a4a4c', 0.5, 24), 'missile': '#e6e4de', 'missile_tip': '#b02a22', 'steel_dark': '#2a2a2c',
            'bomb': '#5a5e5a'},
    'f117': {'paint': '#4a4c50', 'paint_dark': '#2e3034', 'band': '#e8e8e8', 'glass': ('#2a3a48', 0.8, 40),
             'steel': ('#3a3a3c', 0.5, 24), 'missile': '#d8d6d0', 'missile_tip': '#3a3a3a', 'steel_dark': '#2a2a2c',
             'bomb': '#3a3c3e'},
    'b17': {'paint': '#5e6a3c', 'paint_dark': '#444c2c', 'band': '#e8e8e8', 'glass': ('#a8c4d0', 0.7, 40),
            'steel': ('#3a3a3a', 0.5, 24), 'blade': '#262626', 'blade_tip': '#d8c040', 'bomb': '#3c4034'},
    'b2': {'paint': '#55585e', 'paint_dark': '#44474c', 'band': '#e8e8e8', 'glass': ('#2a3440', 0.8, 40),
           'steel': ('#2e3034', 0.4, 20), 'bomb': '#4a4c4e', 'missile': '#d8d6d0', 'missile_tip': '#3a3a3a',
           'steel_dark': '#2a2a2c'},
    'chinook': {'paint': '#9ea2a6', 'paint_dark': '#6e7276', 'band': '#e8e8e8', 'glass': ('#3e5260', 0.8, 40),
                'steel': ('#3a3a3c', 0.5, 24), 'blade': '#2e2e30', 'blade_tip': '#d0d0d0', 'tire': '#222222'},
}


@archetype('aircraft')
class Aircraft(VehicleBase):
    loop_actions = ('RUN', 'BUILD', 'FIDGET')

    def __init__(self, unit, params):
        super().__init__(unit, params)
        self.kind = params['model']
        self.paint(PAINTS[self.kind], metal=('steel', 'glass'))
        self.b = BUILDERS[self.kind](self)
        self.heli = bool(self.b.get('heli'))
        self.alt = 0.0
        # Civ3 shows aircraft from higher up than ground units: tip them
        # toward the camera so more of their top shows.
        self.tilt = params.get('tilt', 0.0)
        self.fit_flight()

    # -- the flight placement
    def fit_flight(self):
        """Scale and ground point from the original's DEFAULT frame: the
        plane where the plane is, and an altitude that puts the renderer's
        shadow (cast down-right from the NW light) about where the original's
        shadow is."""
        import flic as F
        self.autofit()            # scale + anchor with the plane at z = 0
        fl = F.load_unit_flic(self.unit, F.unit_flics(self.unit)['DEFAULT'])
        ds = []
        for d in range(fl.n_anims):
            idx = fl.frames[d, 0]
            sh = (idx >= 240) & (idx <= 254)
            op = (idx != 255) & ~F.shadow_mask(idx)
            if sh.sum() < 20 or op.sum() < 20:
                continue
            ds.append(np.nonzero(sh)[0].mean() - np.nonzero(op)[0].mean())
        D = float(np.median(ds)) if ds else 40.0
        sd = R.SHADOW_DIR
        k = sd[1] / sd[2] * SIN_E
        c = self.cal()
        ppm1 = c['ppm'] / c['S']
        alt_px = D / (COS_E + k)
        self.alt = alt_px / ppm1
        ax, ay = self.p['anchor']
        self.p['anchor'] = (ax, ay + alt_px * COS_E)
        self._cal = None

    # -- the model
    def props_mesh(self, ang, blur=False):
        m = self.mats
        out = []
        for c, r, n, axis in self.b['props']:
            blades = []
            for i in range(n):
                a = ang + i * 360.0 / n
                if axis == 'y':
                    bl = M.box(0.18 * (r / 1.45), 0.05, r, m['blade'], center=(0, 0, r / 2)).transformed(M.rot_y(a))
                    tip = M.box(0.19 * (r / 1.45), 0.06, r * 0.12, m['blade_tip'], center=(0, 0, r * 0.94)).transformed(M.rot_y(a))
                    blades += [bl, tip]
                else:
                    bl = M.box(0.5, r, 0.08, m['blade'], center=(0, r / 2, 0)).transformed(M.rot_z(a))
                    tip = M.box(0.52, r * 0.08, 0.09, m['blade_tip'], center=(0, r * 0.96, 0)).transformed(M.rot_z(a))
                    blades += [bl, tip]
            out.append(M.Mesh.concat(blades).transformed(M.translate(*c)))
        return M.Mesh.concat(out)

    def plane(self, prop_ang=0.0, hide=(), parts=None):
        P_ = self.b['parts']
        meshes = [P_[k] for k in ('body', 'wing_l', 'wing_r', 'tail') if k not in hide and len(P_[k])]
        if 'props' not in hide:
            meshes.append(self.props_mesh(prop_ang))
        return M.Mesh.concat(meshes)

    def flight(self, bob=0.0, roll=0.0, pitch=0.0, yaw=0.0, dz=0.0, dy=0.0, dx=0.0):
        alt = 0.0 if self._calibrating else self.alt
        return M.translate(dx, dy, alt + bob + dz) @ M.rot_z(yaw) @ M.rot_y(roll) @ M.rot_x(pitch)

    def fx(self, mesh, world=None, pivot_z=None):
        z = (0.0 if self._calibrating else self.alt) if pivot_z is None else pivot_z
        return FX.FxMesh(mesh, self.mats, world=world, tilt=(self.tilt, (0, 0, z)))

    def frame(self, action, f, n, ctx):
        out = self._frame(action, f, n, ctx)
        if out.tilt is None and action != 'VICTORY':
            out.tilt = (self.tilt, (0, 0, 0.0 if self._calibrating else self.alt))
        return out

    def _frame(self, action, f, n, ctx):
        t = self.times(action, f, n)
        if action == 'DEFAULT':
            return FX.FxMesh(self.plane(prop_ang=17).transformed(self.flight()), self.mats)
        if action in ('RUN', 'WALK'):
            return self.run(t)
        if action == 'BUILD' or action == 'FIDGET':
            return self.hover(t)
        if action.startswith('ATTACK'):
            return self.attack(t, ctx)
        if action == 'DEATH':
            return self.death(t, ctx)
        if action == 'VICTORY':
            return self.bomb(t, ctx)
        return FX.FxMesh(self.plane().transformed(self.flight()), self.mats)

    def spin(self, t, turns=1.0):
        # Props and rotors: an odd number of blades steps forward per frame;
        # a sixth of a turn per frame reads as a fast spin at 3 blades.
        return 360.0 * turns * t + 17

    def run(self, t):
        s = math.sin(2 * math.pi * t)
        T = self.flight(bob=0.06 * s, roll=2.0 * math.cos(2 * math.pi * t), pitch=0.6 * s)
        return FX.FxMesh(self.plane(prop_ang=self.spin(t, 0.6)).transformed(T), self.mats)

    def hover(self, t):
        """The helicopter's fidget: it sways and turns a little in place."""
        a = 2 * math.pi * t
        L = self.b['length']
        T = self.flight(bob=0.04 * L * math.sin(a), roll=7 * math.sin(a), pitch=-4 * math.cos(a),
                        yaw=25 * math.sin(a), dx=0.12 * L * math.sin(a), dy=0.08 * L * math.sin(2 * a))
        return FX.FxMesh(self.plane(prop_ang=self.spin(t, 1.3)).transformed(T), self.mats)

    def attack(self, t, ctx):
        m = self.mats
        T = self.flight(bob=0.05 * math.sin(2 * math.pi * t))
        mesh = self.plane(prop_ang=self.spin(t, 0.8)).transformed(T)
        fx = []
        weapon = self.p.get('weapon', 'guns')
        tf = self.p.get('fire')
        if tf is None:
            tf = flash_time(ctx, 0.15)
        if weapon == 'missile':
            dt = t - tf
            for i, py in enumerate(self.b['pylons'][:1]):
                p0 = (T @ np.array([*py, 1.0], np.float32))[:3]
                if dt < 0:
                    fx.append(self.missile().transformed(M.translate(*p0)))
                    continue
                fwd = np.array([0, 1.0, -0.25], np.float32)
                fwd /= np.linalg.norm(fwd)
                d = 3.0 * dt + 60.0 * dt * dt
                pos = p0 + fwd * d + np.array([0, 0, -1.5 * min(dt * 6, 1.0)], np.float32)
                fx.append(self.missile().transformed(_orient_y(pos, fwd)))
                fx.append(FX.plume(m, pos, -fwd, 1.2, 0.18, t=dt, seed=i, smoke=False))
                for j in range(7):
                    s = d * j / 7
                    age = (d - s) / 30.0
                    if age < 1 and s > 0.5:
                        fx.append(FX.puff(m, p0 + fwd * s + np.array([0, 0, -1.5 * min(dt * 6, 1.0) * s / max(d, 1e-3)]),
                                          0.25 * (1 + age) * (1 - age ** 2), 'smoke_white', seed=30 + j))
        else:
            # guns: alternating flashes at the muzzles, tracers forward
            for k, g in enumerate(self.b['guns']):
                p0 = (T @ np.array([*g, 1.0], np.float32))[:3]
                fwd = np.array([0, 1.0, -0.12], np.float32)
                if self.heli:
                    fwd = np.array([0.5, 0.9, -0.35], np.float32)
                fwd /= np.linalg.norm(fwd)
                ph = int(t * 20) + k
                if t > tf * 0.5 and ph % 2 == 0:
                    fx.append(FX.muzzle_flash(m, p0, fwd, 0.7, seed=ph, prongs=4))
                for j in range(2):
                    u = (t * 4 + j / 2 + k * 0.25) % 1.0
                    if t > tf * 0.5:
                        p = p0 + fwd * (1.5 + u * 14)
                        fx.append(VP.tube(p, p + fwd * 1.4, 0.05, 0.05, m['fx_yellow'], seg=4))
        return FX.FxMesh(M.Mesh.concat([mesh] + fx), m)

    def missile(self):
        m = self.mats
        L, r = 2.2, 0.1
        body = VP.tube((0, 0, 0), (0, L * 0.85, 0), r, r, m['missile'], seg=8)
        nose = VP.tube((0, L * 0.85, 0), (0, L, 0), r, 0.01, m['missile_tip'], seg=8)
        fins = [M.box(0.02, 0.3, r * 3, m['steel_dark']).transformed(M.rot_y(a) @ M.translate(0, 0.2, 0))
                for a in (45, 135)]
        return M.Mesh.concat([body, nose] + fins)

    def death(self, t, ctx):
        """Hit: a fireball bursts on the plane; it breaks apart and the
        pieces tumble down out of it; the smoke cloud hangs in the air and
        thins away (as in Civ3)."""
        m = self.mats
        tb = 0.06
        u = max(0.0, t - tb)
        fall = min(1.0, u / 0.3)
        alt = self.alt
        L = self.b['length']
        P_ = self.b['parts']
        char = u > 0.04
        pieces = []

        def piece(mesh, dx, spin_axis, spin, fwd):
            z = alt * (1 - fall * fall) - 0.5 * fall
            T = M.translate(dx * fall, fwd * fall, z) @ spin_axis(spin * fall)
            mm = mesh.transformed(T)
            return VP.recolor(mm, self.charred()) if char else mm
        if fall < 1.0:
            if self.heli:
                body = M.Mesh.concat([P_['body'], self.props_mesh(self.spin(t, 2.0))])
                pieces.append(piece(body, 0.0, M.rot_z, 200, L * 0.15))
            elif u < 0.03:
                pieces.append(piece(self.plane(), 0.0, M.rot_y, 0, 0))
            else:
                main = M.Mesh.concat([P_['body'], P_['tail'], P_['wing_r'], self.props_mesh(0)])
                pieces.append(piece(main, L * 0.08, M.rot_y, 120, L * 0.3))
                if len(P_['wing_l']):
                    pieces.append(piece(P_['wing_l'], -L * 0.35, M.rot_x, 260, -L * 0.1))
        fx = []
        if t >= tb:
            air = np.array([0, 0, alt], np.float32)
            fx.append(FX.explosion(m, air, L * 0.36, u / 0.85, seed=2, n=10, rise=0.15, smoke_dark=0.75))
            if fall < 1.0 and u > 0.03:
                fx.append(FX.puff(m, (0, L * 0.3 * fall, alt * (1 - fall * fall)), L * 0.07, 'fx_orange', seed=41))
        return self.fx(M.Mesh.concat(pieces + fx), pivot_z=alt)

    def bomb(self, t, ctx):
        """The bomb run (Civ3's VICTORY flic of bombers and fighters): a
        stick of bombs falls from above and bursts on the ground one after
        another where the original's explosions are (the plane itself is not
        in this flic)."""
        return FX.FxMesh(None, self.mats, per_dir=lambda d: self._bomb_dir(t, ctx, d))

    def _bomb_dir(self, t, ctx, d):
        from models.vehicles import original_profile
        m = self.mats
        n = 4 if self.kind in ('b17', 'b2') else 2
        th = flash_time(ctx, 0.3) if ctx is not None else 0.3
        th = max(0.12, min(th, 0.6))
        prof = original_profile(ctx.flic) if ctx is not None else None
        if prof is None:
            return None
        bb = prof['bbox'][min(d, ctx.flic.n_anims - 1)]
        f0 = int(round(th * (ctx.n - 1)))
        late = bb[f0:]
        late = late[late[:, 2] > late[:, 0]]
        if not len(late):
            return None
        ex = np.array([late[:, 0].min(), late[:, 1].min(), late[:, 2].max(), late[:, 3].max()])
        first = bb[0] if bb[0, 2] > bb[0, 0] else ex
        size = max(1.0, self.meters(ex[2] - ex[0]) * (0.22 if n == 4 else 0.3))
        ground = self.to_world((ex[0] + ex[2]) / 2, ex[3] - self.px(size) * 0.6, 0.0)
        start = self.to_world((first[0] + first[2]) / 2, (first[1] + first[3]) / 2, self.alt * 0.8)
        fx = []
        for i in range(n):
            ti = th + i * 0.04
            off = np.array([(i - (n - 1) / 2) * size * 0.9, (i - (n - 1) / 2) * size * 0.5, 0], np.float32)
            if t < ti:
                u = t / ti
                p = start + (ground + off - start) * np.array([u, u, u * u], np.float32)
                fwd = (ground + off - start) * np.array([1, 1, 2 * u], np.float32)
                fx.append(self.bomb_mesh().transformed(_orient_y(p, fwd)))
            else:
                age = (t - ti) / (1.0 - ti)
                fx.append(FX.explosion(m, ground + off + np.array([0, 0, size * 0.3]), size, age * 1.05, seed=10 + i,
                                       n=8, smoke_dark=0.55))
                if age < 0.5:
                    fx.append(FX.debris(m, ground + off, size * 0.5, min(age * 2, 1.0), seed=20 + i, n=5,
                                        names=('dust', 'char')))
        return M.Mesh.concat(fx)

    def bomb_mesh(self):
        m = self.mats
        k = self.b['length'] / 9.0
        return M.Mesh.concat([VP.tube((0, -0.5, 0), (0, 0.45, 0), 0.16, 0.16, m['bomb'], seg=8),
                              VP.tube((0, 0.45, 0), (0, 0.65, 0), 0.16, 0.03, m['bomb'], seg=8),
                              M.box(0.5, 0.2, 0.04, m['bomb'], center=(0, -0.5, 0)),
                              M.box(0.04, 0.2, 0.5, m['bomb'], center=(0, -0.5, 0))]).transformed(M.scale(max(1.0, k)))
