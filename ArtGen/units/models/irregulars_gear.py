"""Gear of the irregulars (models/irregulars.py): the Insurgent's PKM, the
Militant's Lee-Enfield, the Partisans' PPSh-41 and the Rebel's Molotov
cocktail and bottle bag.

Held props follow models/props.py: the grip (the right hand) at the origin,
the long axis +z (the muzzle end), the top (sights) toward -y, the gun's
right side +x. GUNS gives each firearm's muzzle (z), where the left hand
holds it (support: (y, z)) and its butt (z), like props_foot_modern.GUNS.
Guns are drawn slim: the soldier thickens them 1.6x across (gun_thick).
"""
import math

import numpy as np

import mesh as M
from models.props import mat, steel
from models import props_foot_modern as G

GUNS = {
    'pkm': {'muzzle': 0.80, 'support': (-0.07, 0.10), 'butt': -0.36},
    'enfield': {'muzzle': 0.80, 'support': (0.03, 0.34), 'butt': -0.36},
    'ppsh41': {'muzzle': 0.50, 'support': (0.05, 0.22), 'butt': -0.36},
    'molotov': {'muzzle': 0.20, 'support': (0.0, 0.0), 'butt': -0.08},
    'shotgun': {'muzzle': 0.74, 'support': (0.03, 0.22), 'butt': -0.36},
}


def _brass(mats):
    return mat(mats, 'brass', '#c49a48', spec=0.7, gloss=26, metal=True)


def _dark(mats):
    return mat(mats, 'gun_dark', '#1c1d20', spec=0.3, gloss=16)


def _bar(a, b, r, m, seg=6):
    return M.capsule(a, b, r, r, m, seg=seg, rings=1)


# ------------------------------------------------------------ the PKM

def pkm(mats, muzzle=0.80):
    """A PKM machine gun: a long stamped receiver with its top cover, a
    skeleton butt, a pistol grip, a long fluted barrel over the gas tube,
    a carrying handle on the barrel, a slotted flash hider, the bipod folded
    under the barrel and the belt box hanging under the receiver with the
    belt running up into the feed."""
    g = G.gunmetal(mats)
    d = _dark(mats)
    w = G.stock(mats)
    box = mat(mats, 'ammo_box', '#4e5236', spec=0.2, gloss=14)
    br = _brass(mats)
    parts = []
    # receiver and top cover
    parts.append(M.box(0.040, 0.060, 0.30, g, (0, 0.0, 0.07)))
    parts.append(M.box(0.046, 0.022, 0.20, g, (0, -0.036, 0.10)))                    # top cover
    parts.append(M.box(0.048, 0.008, 0.012, d, (0, -0.048, 0.02)))                   # cover latch
    parts.append(M.box(0.010, 0.020, 0.07, d, (0, -0.052, -0.04)))                   # rear sight block
    # pistol grip (wood) and trigger guard
    parts.append(M.box(0.030, 0.085, 0.038, w, (0, 0.06, -0.02)).transformed(M.rot_x(-14)))
    parts.append(_bar((0, 0.04, -0.01), (0, 0.055, 0.05), 0.005, g))
    # skeleton butt: upper and lower bars to a deep butt plate, all wood
    parts.append(_bar((0, -0.01, -0.07), (0, -0.01, -0.33), 0.016, w))
    parts.append(_bar((0, 0.085, -0.045), (0, 0.075, -0.32), 0.013, w))
    parts.append(M.rounded_box(0.034, 0.13, 0.035, d, (0, 0.035, -0.345), r=0.01, seg=6, p=4))
    # barrel, gas tube and gas block, handle, front sight, flash hider
    parts.append(M.cylinder(0.016, 0.014, 0.52, g, seg=10, z0=0.22))
    for z in (0.27, 0.33, 0.39):
        parts.append(M.cylinder(0.019, 0.019, 0.025, d, seg=10, z0=z))                # barrel flutes / rings
    parts.append(M.cylinder(0.010, 0.010, 0.36, g, seg=6, z0=0.20).transformed(M.translate(0, 0.026, 0)))
    parts.append(M.box(0.026, 0.05, 0.04, g, (0, 0.012, 0.57)))                      # gas block
    parts.append(_bar((0, -0.02, 0.26), (0, -0.07, 0.30), 0.008, d))                  # carrying handle
    parts.append(_bar((0, -0.07, 0.30), (0, -0.07, 0.38), 0.010, w))
    parts.append(_bar((0, -0.07, 0.38), (0, -0.02, 0.40), 0.008, d))
    parts.append(M.box(0.008, 0.04, 0.02, g, (0, -0.03, muzzle - 0.10)))             # front sight
    parts.append(M.cylinder(0.021, 0.021, 0.07, d, seg=8, z0=muzzle - 0.07))         # flash hider
    for k in range(4):
        a = k * 90 + 45
        parts.append(M.box(0.006, 0.006, 0.05, g, (0.022 * math.cos(math.radians(a)), 0.022 * math.sin(math.radians(a)),
                                                    muzzle - 0.035)))
    # the bipod, folded forward under the barrel
    for sx in (1, -1):
        parts.append(_bar((sx * 0.012, 0.03, 0.60), (sx * 0.05, 0.13, 0.70), 0.008, g))
        parts.append(M.box(0.016, 0.016, 0.02, g, (sx * 0.05, 0.135, 0.705)))
    # the loose end of the belt out of the box's mouth, hanging down
    for k in range(6):
        y = 0.17 + 0.03 * k
        parts.append(M.box(0.04, 0.012, 0.014, br, (0.03, y, 0.12 - 0.012 * k)))
        parts.append(M.box(0.044, 0.006, 0.016, d, (0.03, y + 0.008, 0.12 - 0.012 * k)))
    # the belt box under the receiver (offset to the right, as on the gun)
    parts.append(M.rounded_box(0.065, 0.12, 0.11, box, (0.012, 0.115, 0.08), r=0.012, seg=6, p=4))
    parts.append(M.box(0.070, 0.012, 0.115, d, (0.012, 0.058, 0.08)))               # its lid
    parts.append(M.box(0.012, 0.03, 0.06, d, (0.05, 0.10, 0.08)))                    # its hinge plate
    # the belt from the box into the feed on the right
    for k in range(5):
        u = k / 4
        y = 0.055 - 0.07 * u
        x = 0.028 + 0.02 * math.sin(u * math.pi)
        parts.append(M.box(0.012, 0.012, 0.04, br, (x, y, 0.08 + 0.006 * k)).transformed(M.translate(0, 0, 0)))
        parts.append(M.box(0.014, 0.004, 0.044, d, (x + 0.002, y + 0.004, 0.08 + 0.006 * k)))
    return M.Mesh.concat(parts)


def belt_links(mats, pts, r=0.022, every=0.05):
    """A loose ammunition belt along a polyline (cartridges side by side,
    their bullets pointing one way): worn over the shoulder."""
    br = _brass(mats)
    d = _dark(mats)
    pts = [np.asarray(p, np.float32) for p in pts]
    out = []
    for a, b in zip(pts[:-1], pts[1:]):
        L = float(np.linalg.norm(b - a))
        n = max(1, int(L / every))
        t = (b - a) / max(L, 1e-6)
        for i in range(n):
            c = a + (b - a) * (i + 0.5) / n
            # a cartridge across the belt: from c outwards (perpendicular in the belt plane)
            side = np.cross(t, np.array([0, 0, 1], np.float32))
            if np.linalg.norm(side) < 1e-3:
                side = np.array([1, 0, 0], np.float32)
            side = side / np.linalg.norm(side)
            out.append(M.capsule(tuple(c - side * r), tuple(c + side * r * 0.6), 0.011, 0.007, br, seg=5, rings=1))
        out.append(M.capsule(tuple(a), tuple(b), 0.008, 0.008, d, seg=4, rings=1))
    return M.Mesh.concat(out)


# ------------------------------------------------------------ the Lee-Enfield

def enfield(mats, muzzle=0.80):
    """A Lee-Enfield No.4 .303: wooden butt and a fore-end nearly to the
    muzzle with its upper handguard, the barrel bands, the nose cap and the
    bare muzzle, the receiver with the bolt knob on the right, the aperture
    sight, the box magazine and a sling."""
    s = G.stock(mats)
    sd = mat(mats, 'stock_dark', '#4e3018', spec=0.12, gloss=14)
    g = G.gunmetal(mats)
    st = steel(mats)
    sl = mat(mats, 'sling_web', '#a89a72', spec=0.05)
    d = mat(mats, 'gun_dark', '#1c1d20')
    parts = [G._stock_wood(mats, -0.36, 0.02, depth=0.075, wrist=0.03, m=s)]
    parts.append(M.box(0.008, 0.078, 0.012, sd, (0, 0.032, -0.355)))                   # butt plate
    # the fore-end, deep at the receiver, slimming to the nose cap
    parts.append(M.box(0.042, 0.058, 0.30, s, (0, 0.018, 0.21)))                      # deep at the receiver
    parts.append(M.box(0.036, 0.044, 0.34, s, (0, 0.012, 0.51)))                      # the step, slimmer forward
    parts.append(M.box(0.034, 0.018, 0.50, sd, (0, -0.022, 0.43)))                    # upper handguard
    parts.append(M.box(0.042, 0.054, 0.012, g, (0, 0.012, 0.36)))                     # bands
    parts.append(M.box(0.042, 0.054, 0.012, g, (0, 0.012, 0.58)))
    parts.append(M.box(0.042, 0.052, 0.07, d, (0, 0.006, 0.70)))                      # nose cap
    parts.append(M.box(0.012, 0.03, 0.03, d, (0, -0.03, 0.72)))                        # its sight ears
    parts.append(M.cylinder(0.011, 0.011, muzzle - 0.68, g, seg=8, z0=0.68))          # the bare muzzle
    parts.append(M.box(0.006, 0.026, 0.016, g, (0, -0.026, 0.715)))                   # front sight
    # receiver, bolt and knob, aperture sight, magazine, trigger guard
    parts.append(M.box(0.036, 0.040, 0.17, g, (0, -0.012, 0.04)))
    d = mat(mats, 'gun_dark', '#1c1d20')
    parts.append(M.box(0.022, 0.016, 0.07, d, (0, -0.036, 0.0)))                      # bolt body
    parts.append(_bar((0.012, -0.028, -0.02), (0.058, -0.004, -0.03), 0.007, d))
    parts.append(M.sphere(0.02, d, (0.064, 0.0, -0.032), seg=8))                      # bolt knob
    parts.append(M.box(0.016, 0.030, 0.022, g, (0, -0.048, -0.03)))                   # rear sight
    parts.append(M.box(0.032, 0.075, 0.085, d, (0, 0.06, 0.075)))                     # magazine
    parts.append(M.box(0.034, 0.012, 0.09, mat(mats, 'gun_dark', '#1c1d20'), (0, 0.10, 0.075)))
    parts.append(_bar((0, 0.04, -0.01), (0, 0.06, 0.035), 0.005, g))
    # the sling, slack under the gun from the front swivel to the butt
    pts = [(0, 0.05, 0.56), (0, 0.06, 0.36), (0, 0.07, 0.10), (0, 0.075, -0.14), (0, 0.07, -0.26)]
    for a, b in zip(pts[:-1], pts[1:]):
        parts.append(M.capsule(a, b, 0.009, 0.009, sl, seg=4, rings=1).transformed(M.scale(1.5, 1, 1)))
    for z in (0.56, -0.26):
        parts.append(M.box(0.012, 0.03, 0.02, g, (0, 0.05, z)))                        # swivels
    return M.Mesh.concat(parts)


# ------------------------------------------------------------ the PPSh-41

def ppsh41(mats, muzzle=0.50):
    """A PPSh-41: a wooden stock from the butt to under the receiver, the
    round receiver, the barrel jacket with rows of cooling slots ending in
    the slanted compensator, the 71-round drum in front of the trigger
    guard, the bolt handle on the right and a sling."""
    s = G.stock(mats)
    g = G.gunmetal(mats)
    d = _dark(mats)
    sl = mat(mats, 'sling', '#6a5a3a', spec=0.05)
    parts = [G._stock_wood(mats, -0.36, 0.0, depth=0.08, wrist=0.032, m=s)]
    parts.append(M.box(0.040, 0.05, 0.16, s, (0, 0.016, 0.06)))                        # fore-stock under the receiver
    parts.append(M.box(0.008, 0.084, 0.012, d, (0, 0.036, -0.355)))                    # butt plate
    parts.append(M.cylinder(0.026, 0.026, 0.20, g, seg=12, z0=0.0).transformed(M.translate(0, -0.016, 0)))
    parts.append(M.box(0.014, 0.02, 0.03, g, (0, -0.045, 0.02)))                       # rear sight
    parts.append(_bar((0.02, -0.02, 0.08), (0.05, -0.02, 0.08), 0.007, d))            # bolt handle
    # the jacket and its slots, the compensator cut at a slant
    parts.append(M.cylinder(0.024, 0.024, muzzle - 0.24, g, seg=12, z0=0.20).transformed(M.translate(0, -0.016, 0)))
    for z in (0.235, 0.285, 0.335, 0.385):
        for a in (0, 45, 135, 180, 225, 270, 315):
            ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
            parts.append(M.box(0.009, 0.009, 0.03, d, (0.0245 * ca, -0.016 + 0.0245 * sa, z)).transformed(
                M.translate(0.0245 * ca, -0.016 + 0.0245 * sa, z) @ M.rot_z(a)
                @ M.translate(-0.0245 * ca, 0.016 - 0.0245 * sa, -z)))
    parts.append(M.box(0.050, 0.030, 0.035, g, (0, -0.016, muzzle - 0.02)).transformed(
        M.translate(0, -0.016, muzzle - 0.02) @ M.rot_x(-28) @ M.translate(0, 0.016, -(muzzle - 0.02))))
    parts.append(M.box(0.008, 0.02, 0.02, g, (0, -0.048, muzzle - 0.06)))              # front sight
    # the drum: a flat disc, its face to the side, a rim and the winding key
    drum = mat(mats, 'drum', '#55565c', spec=0.55, gloss=22, metal=True)
    # (drawn squashed in y: the soldier thickens guns 1.6x across, which makes it round)
    D = M.translate(0, 0.082, 0.15) @ M.scale(1, 0.66, 1) @ M.rot_y(90)
    parts.append(M.cylinder(0.10, 0.10, 0.056, drum, seg=22, z0=-0.028).transformed(D))
    parts.append(M.cylinder(0.103, 0.103, 0.012, d, seg=22, z0=-0.006).transformed(D))
    parts.append(M.cylinder(0.095, 0.095, 0.006, d, seg=22, z0=0.027).transformed(D))
    # the front plate (a lighter dished cover), its rim, the winding key and its lever
    plate = mat(mats, 'drum_plate', '#7a7c82', spec=0.6, gloss=24, metal=True)
    parts.append(M.cylinder(0.085, 0.07, 0.012, plate, seg=22, z0=0.03).transformed(D))
    parts.append(M.cylinder(0.085, 0.07, 0.012, plate, seg=22, z0=-0.042).transformed(D))
    parts.append(M.cylinder(0.02, 0.02, 0.02, steel(mats), seg=8, z0=0.035).transformed(D))
    parts.append(M.box(0.008, 0.012, 0.06, steel(mats), (0.05, 0.0, 0.025)).transformed(D @ M.rot_y(-90)))
    parts.append(M.box(0.03, 0.03, 0.04, g, (0, 0.03, 0.15)))                          # drum's neck
    parts.append(_bar((0, 0.035, -0.02), (0, 0.05, 0.05), 0.005, g))                  # trigger guard
    # the sling
    pts = [(-0.02, 0.03, 0.18), (-0.025, 0.045, 0.05), (-0.025, 0.06, -0.12), (-0.02, 0.07, -0.28)]
    for a, b in zip(pts[:-1], pts[1:]):
        parts.append(M.capsule(a, b, 0.006, 0.006, sl, seg=4, rings=1))
    return M.Mesh.concat(parts)


# ------------------------------------------------------------ the Molotov cocktail

def molotov(mats):
    """A bottle held by its body, the neck up (+z): green glass, a rag
    stuffed into the neck and hanging over it. The flame is drawn by the
    soldier (a mesh and a glow at FLAME_AT)."""
    gl = mat(mats, 'glass', '#3e6a3a', spec=0.9, gloss=40)
    liq = mat(mats, 'petrol', '#b08a3a', spec=0.6, gloss=30)
    rag = mat(mats, 'rag', '#d8d0bc', spec=0.03)
    parts = [M.lathe([(-0.07, 0.0), (-0.07, 0.034), (-0.06, 0.038), (0.06, 0.038), (0.09, 0.03), (0.12, 0.014),
                      (0.17, 0.012), (0.175, 0.0)], gl, seg=12)]
    parts.append(M.cylinder(0.036, 0.036, 0.05, liq, seg=12, z0=-0.065))              # the petrol seen through it
    parts.append(M.ellipsoid(0.02, 0.02, 0.035, rag, (0.0, 0.0, 0.19), seg=8))
    parts.append(M.capsule((0.0, 0.0, 0.19), (0.03, 0.01, 0.12), 0.014, 0.009, rag, seg=6, rings=1))
    parts.append(M.capsule((0.0, 0.0, 0.19), (-0.02, -0.02, 0.14), 0.012, 0.008, rag, seg=6, rings=1))
    return M.Mesh.concat(parts)


FLAME_AT = (0.0, 0.0, 0.22)


def flame(mats, k=0, size=1.0):
    """A small licking flame (+z up): an inner yellow and an outer orange
    tongue, swaying with the frame k."""
    o = mat(mats, 'flame_o', '#ff8a1c', spec=0.0)
    y = mat(mats, 'flame_y', '#ffe27a', spec=0.0)
    sw = 0.012 * math.sin(k * 1.9)
    s = size
    parts = [M.lathe([(0.0, 0.026 * s), (0.03 * s, 0.03 * s), (0.08 * s, 0.016 * s), (0.12 * s, 0.0)], o, seg=8)
             .transformed(M.rot_y(8 * math.sin(k * 1.3)) @ M.translate(sw, 0, 0))]
    parts.append(M.lathe([(0.005 * s, 0.016 * s), (0.03 * s, 0.018 * s), (0.07 * s, 0.0)], y, seg=8)
                 .transformed(M.translate(sw * 0.5, 0.012, 0.0)))
    return M.Mesh.concat(parts)


def bottle_bag(mats):
    """A canvas shoulder bag at the left hip, two bottle necks with their
    rags out of it (hip space: carried on the pelvis, the bag's back on the
    hip at the origin)."""
    c = mat(mats, 'bag', '#7a6c4e', spec=0.04)
    cd = mat(mats, 'bag_dark', '#5a4e36', spec=0.04)
    gl = mat(mats, 'glass', '#3e6a3a', spec=0.9, gloss=40)
    rag = mat(mats, 'rag', '#d8d0bc', spec=0.03)
    parts = [M.rounded_box(0.07, 0.20, 0.20, c, (0, 0.0, -0.10), r=0.03, seg=8, p=3.5)]
    parts.append(M.rounded_box(0.075, 0.205, 0.08, cd, (0.002, 0.0, -0.035), r=0.02, seg=6, p=4))   # flap
    parts.append(M.box(0.01, 0.03, 0.02, mat(mats, 'buckle', '#8a8070', spec=0.6, gloss=24, metal=True),
                       (-0.038, 0.0, -0.07)))
    for dy, tilt in ((-0.05, -10), (0.05, 12)):
        T = M.translate(0, dy, 0.0) @ M.rot_x(tilt)
        parts.append(M.cylinder(0.016, 0.012, 0.09, gl, seg=8, z0=-0.02).transformed(T))
        parts.append(M.ellipsoid(0.016, 0.016, 0.025, rag, (0, 0, 0.08), seg=6).transformed(T))
    return M.Mesh.concat(parts)


# ------------------------------------------------------------ the shotgun

def shotgun(mats, muzzle=0.74):
    """An old side-by-side hunting gun: a wooden butt with its wrist, the
    two barrels side by side with a rib between them, the wooden fore-end
    under them, the action with two hammers, the trigger guard and a
    leather sling."""
    s = G.stock(mats)
    g = G.gunmetal(mats)
    st = steel(mats)
    sl = mat(mats, 'sling', '#5a4228', spec=0.05)
    parts = [G._stock_wood(mats, -0.36, 0.0, depth=0.075, wrist=0.028, m=s)]
    parts.append(M.box(0.044, 0.05, 0.08, g, (0, -0.004, 0.03)))                        # the action
    for sx in (1, -1):
        parts.append(M.cylinder(0.0135, 0.012, muzzle - 0.06, g, seg=10, z0=0.06).transformed(
            M.translate(sx * 0.013, -0.012, 0)))
        parts.append(M.cylinder(0.015, 0.015, 0.012, g, seg=10, z0=muzzle - 0.012).transformed(
            M.translate(sx * 0.013, -0.012, 0)))
        parts.append(_bar((sx * 0.014, -0.03, 0.0), (sx * 0.016, -0.05, 0.03), 0.006, g))     # hammers
    parts.append(M.box(0.006, 0.008, muzzle - 0.08, g, (0, -0.027, (muzzle + 0.08) / 2)))   # rib
    parts.append(M.rounded_box(0.036, 0.03, 0.20, s, (0, 0.012, 0.17), r=0.01, seg=6, p=4))  # fore-end
    parts.append(_bar((0, 0.03, -0.01), (0, 0.05, 0.05), 0.005, g))                        # trigger guard
    pts = [(0, 0.03, 0.40), (0, 0.04, 0.20), (0, 0.05, -0.02), (0, 0.065, -0.20), (0, 0.06, -0.28)]
    for z in (0.40, -0.28):
        parts.append(M.box(0.012, 0.024, 0.016, g, (0, 0.03 if z > 0 else 0.055, z)))
    for a, b in zip(pts[:-1], pts[1:]):
        parts.append(M.capsule(a, b, 0.008, 0.008, sl, seg=4, rings=1).transformed(M.scale(1.5, 1, 1)))
    return M.Mesh.concat(parts)
