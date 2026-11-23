"""Gear for the line units of round 2 (models/line_foot.py): the Militia's
Charleville musket and Napoleonic kit, the Skirmisher's M14 and M1956 load
bearing kit, the Japanese rifleman's Type 38 rifle and IJA leather gear, and
the Battalion's water-cooled Browning M1917A1 with its tripod, ammunition
cans and condenser can.

Held props follow models/props.py: the grip (the right hand) at the origin,
the long axis +z (the muzzle end), the top (sights) toward -y, the bottom +y,
the right side +x. GUNS gives each firearm's muzzle (z), where the left hand
holds it (support: (y, z)) and its butt (z), like props_foot_modern.GUNS.
Worn gear is in the space of the bone named in its docstring.
"""
import math

import numpy as np

import mesh as M
from models.props import mat, steel
from models import props_foot_modern as G

GUNS = {
    'charleville': {'muzzle': 1.00, 'support': (0.02, 0.40), 'butt': -0.36},
    'm14': {'muzzle': 0.80, 'support': (0.03, 0.34), 'butt': -0.34},
    'type38': {'muzzle': 1.02, 'support': (0.02, 0.40), 'butt': -0.36},
    'm1917': {'muzzle': 0.94, 'support': (-0.02, 0.16), 'butt': -0.18},
}


def iron(mats):
    return mat(mats, 'iron', '#7c8086', spec=0.45, gloss=20, metal=True)


def brass(mats):
    return mat(mats, 'brass', '#b8955a', spec=0.45, gloss=20, metal=True)


def _strap(mats, pts, r, m):
    return M.Mesh.concat([M.capsule(a, b, r, r, m, seg=5, rings=1) for a, b in zip(pts[:-1], pts[1:])])


# ------------------------------------------------------------ firearms

def charleville(mats, muzzle=1.00, bayonet=0.40, order=0.0):
    """The French infantry musket of 1777 (An IX): a long walnut stock, an
    iron barrel held by three bands (the upper one double-strapped), the
    flintlock on the right (cock, frizzen, pan), the iron ramrod under the
    barrel, a buff sling, and the socket bayonet with its offset blade.
    order: the grip moved that far up the barrel (order arms: the hand
    holds the barrel and the butt rests on the ground)."""
    s = G.stock(mats)
    ir = iron(mats)
    st = steel(mats)
    parts = [G._stock_wood(mats, -0.36, 0.90, depth=0.085, m=s)]
    parts.append(M.box(0.044, 0.088, 0.014, ir, (0, 0.03, -0.356)))                              # butt plate
    parts.append(G._barrel(mats, -0.06, muzzle, 0.016, ir))
    for z, h in ((0.44, 0.022), (0.66, 0.02), (0.84, 0.06)):
        parts.append(M.cylinder(0.027, 0.027, h, ir, seg=10, z0=z).transformed(M.translate(0, 0.008, 0)))
    parts.append(M.box(0.008, 0.034, 0.12, ir, (0.024, 0.002, 0.02)))                            # lock plate
    parts.append(_strap(mats, [(0.03, 0.0, -0.035), (0.032, -0.03, -0.03), (0.03, -0.05, -0.005)], 0.008, ir))  # cock
    parts.append(M.box(0.012, 0.036, 0.012, ir, (0.03, -0.03, 0.05)))                           # frizzen
    parts.append(M.box(0.016, 0.012, 0.03, ir, (0.03, -0.012, 0.035)))                          # pan
    parts.append(_strap(mats, [(0, 0.026, -0.10), (0, 0.06, -0.07), (0, 0.062, -0.02), (0, 0.026, 0.02)], 0.006, ir))  # guard
    parts.append(M.cylinder(0.005, 0.005, muzzle - 0.10, st, seg=5, z0=0.08).transformed(M.translate(0, 0.03, 0)))  # ramrod
    # the buff sling, slack under the stock, from the middle band to the guard
    sl = mat(mats, 'sling', '#e8e2d2', spec=0.05)
    parts.append(_strap(mats, [(0, 0.035, 0.66), (0, 0.045, 0.36), (0, 0.06, 0.06), (0, 0.075, -0.10)], 0.009, sl))
    if bayonet:
        parts.append(M.cylinder(0.02, 0.02, 0.07, st, seg=10, z0=muzzle - 0.07))                  # socket
        parts.append(M.capsule((0.0, 0.02, muzzle - 0.01), (0.026, 0.03, muzzle + 0.03), 0.007, 0.007, st, seg=5, rings=1))
        parts.append(M.blade(bayonet, 0.026, 0.010, st, tip=0.18, z0=muzzle + 0.02).transformed(M.translate(0.026, 0.03, 0)))
    m = M.Mesh.concat(parts)
    if order:
        m = m.transformed(M.translate(0, 0, -order))
    return m


def m14(mats, muzzle=0.80):
    """The US M14 (1959): a walnut stock with its fore-end, the dark upper
    handguard, receiver and bolt, a straight 20-round box magazine, the gas
    cylinder under the barrel, the slotted flash suppressor and an olive web
    sling."""
    s = G.stock(mats)
    g = G.gunmetal(mats)
    hg = mat(mats, 'handguard', '#3a3226', spec=0.1)
    parts = [G._stock_wood(mats, -0.34, 0.50, depth=0.082, wrist=0.032, m=s)]
    parts.append(M.box(0.044, 0.09, 0.014, g, (0, 0.028, -0.336)))                              # butt plate
    parts.append(M.box(0.034, 0.046, 0.26, g, (0, -0.008, 0.06)))                                # receiver
    parts.append(M.box(0.012, 0.03, 0.04, g, (0, -0.04, -0.06)))                                 # rear sight
    parts.append(M.box(0.02, 0.016, 0.07, g, (0.022, -0.016, 0.08)))                             # operating rod handle
    mg = mat(mats, 'magazine', '#18181a', spec=0.2, gloss=14)
    parts.append(M.box(0.034, 0.13, 0.08, mg, (0, 0.095, 0.12)).transformed(M.rot_x(4)))         # magazine
    parts.append(M.box(0.038, 0.014, 0.086, g, (0, 0.162, 0.13)))                                # its floorplate
    parts.append(_strap(mats, [(0, 0.03, -0.05), (0, 0.06, -0.02), (0, 0.03, 0.03)], 0.006, g))    # trigger guard
    parts.append(M.box(0.03, 0.022, 0.24, hg, (0, -0.026, 0.33)))                                # upper handguard
    parts.append(G._barrel(mats, 0.18, muzzle - 0.07, 0.012, g))
    parts.append(M.cylinder(0.013, 0.013, 0.16, g, seg=8, z0=0.48).transformed(M.translate(0, 0.024, 0)))  # gas cylinder
    parts.append(M.cylinder(0.022, 0.022, 0.025, g, seg=8, z0=0.50).transformed(M.translate(0, 0.012, 0)))  # front band
    parts.append(M.cylinder(0.018, 0.016, 0.08, g, seg=8, z0=muzzle - 0.08))                     # flash suppressor
    for z in (muzzle - 0.06, muzzle - 0.03):
        parts.append(M.cylinder(0.0185, 0.0185, 0.008, mat(mats, 'gun_dark', '#1e1e20'), seg=8, z0=z))
    parts.append(M.box(0.008, 0.03, 0.02, g, (0, -0.028, muzzle - 0.095)))                      # front sight
    web = mat(mats, 'web', '#5e5e42', spec=0.04)
    parts.append(_strap(mats, [(0, 0.05, 0.50), (0, 0.11, 0.22), (0, 0.11, -0.06), (0, 0.06, -0.26)], 0.01, web))
    return M.Mesh.concat(parts)


def type38(mats, muzzle=1.02, bayonet=0.44):
    """The Arisaka Type 38 (1905): a very long two-piece stock, the wooden
    handguard over the barrel, the sliding dust cover on the receiver, the
    straight bolt handle with its ball, two bands, a brown leather sling and
    the Type 30 bayonet with its hooked quillon."""
    s = G.stock(mats)
    g = G.gunmetal(mats)
    st = steel(mats)
    parts = [G._stock_wood(mats, -0.36, 0.90, depth=0.08, m=s)]
    parts.append(M.box(0.044, 0.084, 0.014, g, (0, 0.028, -0.356)))
    parts.append(G._barrel(mats, -0.02, muzzle, 0.013, g))
    parts.append(M.box(0.026, 0.022, 0.66, s, (0, -0.024, 0.48)))                               # upper handguard
    parts.append(M.box(0.032, 0.04, 0.18, g, (0, -0.006, 0.02)))                                 # receiver
    parts.append(M.rounded_box(0.036, 0.026, 0.12, g, (0, -0.03, 0.0), r=0.008, seg=6, p=3))     # dust cover
    parts.append(M.capsule((0.016, -0.012, -0.05), (0.06, 0.0, -0.06), 0.007, 0.007, g, seg=5, rings=1))  # bolt handle
    parts.append(M.sphere(0.014, g, (0.064, 0.002, -0.062), seg=6))
    parts.append(M.box(0.03, 0.012, 0.09, g, (0, 0.048, 0.04)))                                  # floorplate
    parts.append(_strap(mats, [(0, 0.03, -0.06), (0, 0.06, -0.03), (0, 0.03, 0.02)], 0.006, g))
    for z in (0.52, 0.86):
        parts.append(M.cylinder(0.025, 0.025, 0.022, g, seg=8, z0=z).transformed(M.translate(0, 0.006, 0)))
    parts.append(M.box(0.008, 0.026, 0.02, g, (0, -0.026, muzzle - 0.03)))
    sl = mat(mats, 'sling', '#5a3e24', spec=0.06)
    parts.append(_strap(mats, [(0, 0.04, 0.52), (0, 0.10, 0.26), (0, 0.10, -0.04), (0, 0.06, -0.24)], 0.009, sl))
    if bayonet:
        parts.append(M.box(0.022, 0.034, 0.08, st, (0, 0.03, muzzle - 0.06)))                    # hilt
        parts.append(_strap(mats, [(0, 0.045, muzzle - 0.03), (0, 0.075, muzzle - 0.04), (0, 0.085, muzzle - 0.07)],
                            0.006, st))                                                           # hooked quillon
        parts.append(M.blade(bayonet, 0.03, 0.010, st, tip=0.14, z0=muzzle - 0.02).transformed(M.translate(0, 0.03, 0)))
    return M.Mesh.concat(parts)


def m1917(mats, muzzle=0.94, belt=False):
    """The Browning M1917A1 heavy machine gun: the square receiver with the
    pistol grip at its back, the feed block and the folded rear sight on top,
    the big water jacket (its filler cap on top, the steam tube's fitting
    below, the carrying handle), the muzzle booster. belt: the cloth belt of
    cartridges hanging from the feedway on the left."""
    g = G.gunmetal(mats)
    j = mat(mats, 'jacket', '#4a4c3e', spec=0.2, gloss=14, metal=True)
    parts = [M.box(0.086, 0.11, 0.32, g, (0, 0.0, 0.02))]                                      # receiver
    parts.append(M.box(0.09, 0.016, 0.30, mat(mats, 'gun_dark', '#1e1e20'), (0, -0.056, 0.02)))  # cover seam
    parts.append(M.box(0.06, 0.09, 0.03, g, (0, 0.0, -0.152)))                                   # back plate
    parts.append(M.box(0.03, 0.09, 0.04, g, (0, 0.08, -0.15)).transformed(M.rot_x(-14)))         # pistol grip
    parts.append(M.box(0.016, 0.03, 0.03, g, (0, 0.06, -0.10)))                                  # trigger
    parts.append(M.box(0.10, 0.04, 0.09, g, (-0.01, -0.07, 0.07)))                               # feed block
    parts.append(M.box(0.012, 0.05, 0.02, g, (0, -0.08, -0.08)))                                 # rear sight leaf
    parts.append(M.box(0.10, 0.12, 0.05, g, (0, 0.0, 0.20)))                                    # trunnion block
    parts.append(M.cylinder(0.058, 0.058, 0.56, j, seg=16, z0=0.22))                             # water jacket
    for z in (0.22, 0.76):
        parts.append(M.cylinder(0.062, 0.062, 0.025, g, seg=16, z0=z))
    parts.append(M.lathe([(0.785, 0.058), (0.80, 0.045), (0.82, 0.026)], g, seg=14))              # front cap
    parts.append(M.cylinder(0.016, 0.016, 0.06, g, seg=8, z0=0.0).transformed(M.translate(0, -0.05, 0.72) @ M.rot_x(90)))
    parts.append(M.cylinder(0.022, 0.022, 0.016, g, seg=8, z0=0.0).transformed(M.translate(0, -0.12, 0.72) @ M.rot_x(90)))  # filler cap
    parts.append(M.cylinder(0.012, 0.012, 0.05, g, seg=6, z0=0.0).transformed(M.translate(0, 0.055, 0.26) @ M.rot_x(-90)))  # steam fitting
    parts.append(_strap(mats, [(0, -0.058, 0.40), (0, -0.10, 0.44), (0, -0.10, 0.54), (0, -0.058, 0.58)], 0.009, g))  # handle
    parts.append(M.cylinder(0.022, 0.03, muzzle - 0.82, g, seg=10, z0=0.82))                     # muzzle booster
    if belt:
        cl = mat(mats, 'belt_cloth', '#8a7e5e', spec=0.03)
        br = brass(mats)
        pts = [(-0.06, -0.06, 0.07), (-0.14, -0.04, 0.07), (-0.20, 0.04, 0.07), (-0.23, 0.14, 0.07)]
        for a, b in zip(pts[:-1], pts[1:]):
            a, b = np.array(a), np.array(b)
            for k in range(3):
                u = (k + 0.5) / 3
                c = a + (b - a) * u
                parts.append(M.box(0.022, 0.022, 0.07, cl, tuple(c)))
                parts.append(M.capsule(tuple(c + (0, 0, -0.035)), tuple(c + (0, 0, 0.04)), 0.008, 0.006, br, seg=5, rings=1))
    return M.Mesh.concat(parts)


# ------------------------------------------------------------ the machine gun's kit (world space, on the ground)

def m1917_tripod(mats, h=0.42, folded=False):
    """The M1917A1 tripod: the head and its pintle at height h, a long front
    leg forward, two rear legs splayed back, the traversing dial between
    them and the elevating screw. folded: the legs brought together along
    -z under the head (carried)."""
    o = mat(mats, 'olive_dark', '#3e4230', spec=0.15)
    g = G.gunmetal(mats)
    parts = [M.box(0.12, 0.10, 0.06, o, (0, 0, h - 0.05)), M.cylinder(0.025, 0.025, 0.06, g, seg=8, z0=h - 0.02)]
    if folded:
        for dx, dy, L in ((0.0, 0.03, 0.95), (0.05, -0.03, 0.80), (-0.05, -0.03, 0.80)):
            parts.append(M.capsule((dx * 0.4, dy * 0.4, h - 0.08), (dx, dy, h - 0.08 - L), 0.022, 0.02, o, seg=6, rings=1))
            parts.append(M.box(0.05, 0.03, 0.03, g, (dx, dy, h - 0.08 - L)))
        parts.append(M.box(0.16, 0.025, 0.025, g, (0, -0.05, h - 0.40)))
        return M.Mesh.concat(parts)
    feet = [(0.0, 0.62, 0.0), (0.40, -0.40, 0.0), (-0.40, -0.40, 0.0)]
    for x, y, z in feet:
        parts.append(M.capsule((x * 0.08, y * 0.08, h - 0.08), (x, y, 0.03), 0.022, 0.019, o, seg=6, rings=1))
        parts.append(M.box(0.06, 0.08, 0.02, g, (x, y, 0.012)))
    # the traversing dial: an arc between the rear legs, and the elevating screw up to the gun's back
    arc = [(math.sin(a) * 0.26, -0.27 - 0.06 * math.cos(a) + 0.06, 0.20) for a in np.linspace(-0.9, 0.9, 6)]
    parts.append(_strap(mats, arc, 0.012, g))
    parts.append(M.capsule((0.0, -0.27, 0.20), (0.0, -0.12, h - 0.06), 0.014, 0.014, g, seg=6, rings=1))
    return M.Mesh.concat(parts)


def ammo_can(mats, lid_open=False):
    """The M1 ammunition can (.30 cal): olive steel, the hinged lid with its
    latch, the folding handle and a yellow stencil; standing on the ground
    (z = 0), long along y."""
    o = mat(mats, 'can', '#4a5036', spec=0.14, gloss=12, metal=True)
    g = G.gunmetal(mats)
    y = mat(mats, 'stencil', '#c8b45a', spec=0.05)
    parts = [M.rounded_box(0.10, 0.28, 0.18, o, (0, 0, 0.09), r=0.008, seg=6, p=5)]
    if lid_open:
        parts.append(M.box(0.11, 0.29, 0.014, o, (0.0, 0.0, 0.18)).transformed(
            M.translate(-0.055, 0, 0.18) @ M.rot_y(-100) @ M.translate(0.055, 0, -0.18)))
    else:
        parts.append(M.box(0.11, 0.29, 0.016, o, (0, 0, 0.186)))
        parts.append(M.box(0.02, 0.10, 0.012, g, (0, 0, 0.198)))                                  # handle
    parts.append(M.box(0.03, 0.03, 0.06, g, (0, 0.145, 0.15)))                                    # latch
    parts.append(M.box(0.006, 0.16, 0.03, y, (0.052, 0.0, 0.11)))                                 # stencil
    return M.Mesh.concat(parts)


def ammo_can_carried(mats):
    """The can held by its handle (grip at the origin, hanging down -z)."""
    return ammo_can(mats).transformed(M.translate(0, 0, -0.21))


def condenser_can(mats):
    """The condenser can the jacket's steam tube runs into: a squat round
    can with a screw cap, on the ground."""
    o = mat(mats, 'can', '#4a5036', spec=0.14, gloss=12, metal=True)
    g = G.gunmetal(mats)
    return M.Mesh.concat([M.cylinder(0.10, 0.10, 0.26, o, seg=14, z0=0.0),
                          M.cylinder(0.104, 0.104, 0.02, g, seg=14, z0=0.24),
                          M.cylinder(0.035, 0.035, 0.04, g, seg=8, z0=0.26)])


def hose(mats, pts):
    """A flexible hose through the given world points."""
    return _strap(mats, pts, 0.018, mat(mats, 'hose', '#2a2a26', spec=0.15))


# ------------------------------------------------------------ worn gear: the Militia (Napoleonic)

def calfskin_knapsack(mats):
    """(chest) The French knapsack of calfskin, hair out (brown and white
    patches), its leather border, the rolled greatcoat strapped on top, and
    the buff shoulder straps."""
    hide = mat(mats, 'hide', '#7a4e2c', spec=0.03)
    pale = mat(mats, 'hide_pale', '#a88c68', spec=0.03)
    lb = mat(mats, 'leather_dark', '#2e2420', spec=0.12)
    gc = mat(mats, 'greatcoat', '#8a7c66', spec=0.03)
    buff = mat(mats, 'strap', '#e8e4dc')
    parts = [M.rounded_box(0.34, 0.13, 0.30, hide, (0, -0.215, 0.12), r=0.03, seg=10, p=4)]
    for (x, z, rx, rz) in ((-0.06, 0.15, 0.09, 0.07), (0.09, 0.05, 0.07, 0.06)):
        parts.append(M.ellipsoid(rx, 0.012, rz, pale, (x, -0.282, z), seg=8))
    parts.append(M.box(0.35, 0.02, 0.02, lb, (0, -0.28, 0.265)))                                  # flap edge
    parts.append(M.cylinder(0.062, 0.062, 0.42, gc, seg=12).transformed(M.translate(-0.21, -0.20, 0.33) @ M.rot_y(90)))
    for x in (-0.12, 0.0, 0.12):
        parts.append(M.cylinder(0.066, 0.066, 0.016, buff, seg=12).transformed(M.translate(x, -0.20, 0.33) @ M.rot_y(90)))
    for x in (-0.11, 0.11):
        parts.append(M.capsule((x, -0.15, 0.28), (x, 0.06, 0.31), 0.018, 0.018, buff, seg=5, rings=1))
        parts.append(M.capsule((x, 0.10, 0.27), (x * 1.25, 0.13, 0.02), 0.017, 0.017, buff, seg=5, rings=1))
    return M.Mesh.concat(parts)


def giberne(mats):
    """(pelvis) The cartridge box on the right buttock: black leather with
    the brass grenade badge on its flap."""
    bl = mat(mats, 'giberne', '#1c1a1a', spec=0.12, gloss=14)
    parts = [M.rounded_box(0.20, 0.075, 0.12, bl, (0.13, -0.215, 0.02), r=0.015, seg=6, p=4)]
    parts.append(M.rounded_box(0.21, 0.02, 0.085, bl, (0.13, -0.255, 0.04), r=0.01, seg=6, p=4))
    br = brass(mats)
    parts.append(M.sphere(0.022, br, (0.13, -0.268, 0.035), seg=8))
    parts.append(M.cone(0.014, 0.04, br, seg=6, z0=0.05).transformed(M.translate(0.13, -0.268, 0)))
    return M.Mesh.concat(parts)


def briquet(mats):
    """(pelvis) On the left hip: the short infantry sabre (briquet) in its
    black scabbard with its brass hilt and white knot, and the bayonet
    scabbard beside it, both hanging back from the frog."""
    bl = mat(mats, 'giberne', '#1c1a1a', spec=0.12, gloss=14)
    br = brass(mats)
    knot = mat(mats, 'strap', '#e8e4dc')
    T = M.translate(-0.21, -0.13, 0.02) @ M.rot_x(32)
    parts = [M.rounded_box(0.08, 0.05, 0.10, bl, (0.0, 0.0, -0.01), r=0.01, seg=6, p=4)]                # frog
    parts.append(M.capsule((0, -0.015, 0.0), (0, -0.015, -0.46), 0.02, 0.016, bl, seg=6, rings=1))       # sabre scabbard
    parts.append(M.sphere(0.018, br, (0, -0.015, -0.47), seg=6))
    parts.append(M.capsule((0, -0.02, 0.02), (0, -0.01, 0.13), 0.016, 0.016, br, seg=6, rings=1))       # grip
    parts.append(_strap(mats, [(0, -0.01, 0.13), (0, 0.04, 0.10), (0, 0.04, 0.03), (0, -0.0, 0.02)], 0.007, br))  # guard
    parts.append(M.ellipsoid(0.022, 0.022, 0.04, knot, (0.0, 0.03, 0.0), seg=6))                         # knot
    parts.append(M.capsule((0, 0.035, -0.02), (0, 0.035, -0.30), 0.017, 0.012, bl, seg=6, rings=1))      # bayonet scabbard
    return M.Mesh.concat(parts).transformed(T)


# ------------------------------------------------------------ worn gear: the Skirmisher (M1956)

def m1956_belt_kit(mats, tw):
    """(pelvis) The M1956 pistol belt's kit: two universal ammunition pouches
    in front with an M26 grenade clipped to the side of each, the first-aid
    pouch, the canteen in its cover on the right hip and the butt pack at
    the back."""
    web = mat(mats, 'web', '#5e5e42', spec=0.04)
    webd = mat(mats, 'web_dark', '#4a4a34', spec=0.04)
    gren = mat(mats, 'grenade', '#4c5236', spec=0.1, gloss=12)
    g = G.gunmetal(mats)
    parts = []
    for sx in (1, -1):
        x = sx * 0.12 * tw
        parts.append(M.rounded_box(0.10, 0.07, 0.12, web, (x, 0.215, 0.0), r=0.012, seg=6, p=4))
        parts.append(M.rounded_box(0.105, 0.08, 0.035, webd, (x, 0.22, 0.055), r=0.01, seg=6, p=4))      # flap
        gx = x + sx * 0.068
        parts.append(M.ellipsoid(0.034, 0.034, 0.045, gren, (gx, 0.20, -0.01), seg=8))
        parts.append(M.box(0.012, 0.018, 0.05, g, (gx + sx * 0.022, 0.21, 0.02)))                         # spoon
        parts.append(M.cylinder(0.012, 0.012, 0.02, g, seg=6, z0=0.03).transformed(M.translate(gx, 0.20, 0)))
    parts.append(M.rounded_box(0.07, 0.06, 0.08, webd, (0.0, 0.235, 0.02), r=0.01, seg=6, p=4))         # first aid
    cv = mat(mats, 'canteen_cover', '#6a6a4c', spec=0.03)
    parts.append(M.rounded_box(0.13, 0.08, 0.17, cv, (0.23 * tw, -0.08, -0.06), r=0.03, seg=8, p=3))
    parts.append(M.cylinder(0.022, 0.022, 0.03, mat(mats, 'polymer', '#24252a'), seg=8, z0=0.025).transformed(
        M.translate(0.23 * tw, -0.08, 0)))
    parts.append(M.rounded_box(0.22, 0.10, 0.14, web, (0.0, -0.225, -0.02), r=0.025, seg=8, p=3.5))      # butt pack
    parts.append(M.box(0.23, 0.02, 0.02, webd, (0.0, -0.278, 0.02)))
    return M.Mesh.concat(parts)


def neck_towel(mats):
    """(chest) The olive towel the grunt wears round his neck, its ends
    hanging over the chest."""
    tm = mat(mats, 'towel', '#6a6e48', spec=0.02)
    parts = [M.loft([(0.255, 0.215, 0.15), (0.30, 0.155, 0.11), (0.345, 0.098, 0.088)], tm, seg=18,
                    cap_bottom=False, cap_top=False)]
    for sx in (1, -1):
        parts.append(M.rounded_box(0.085, 0.03, 0.19, tm, (sx * 0.08, 0.172, 0.17), r=0.012, seg=6, p=4).transformed(
            M.translate(sx * 0.08, 0.17, 0.26) @ M.rot_y(sx * 8) @ M.rot_x(-10) @ M.translate(-sx * 0.08, -0.17, -0.26)))
    return M.Mesh.concat(parts)


# ------------------------------------------------------------ worn gear: the Japanese rifleman (IJA)

def ija_belt_kit(mats, tw):
    """(pelvis) The IJA leather belt's kit: two cartridge boxes in front, the
    big rear box, the bayonet in its black scabbard on the left hip."""
    lt = mat(mats, 'pouch', '#5a3e24', spec=0.1)
    ld = mat(mats, 'leather_dark', '#2e2420', spec=0.12)
    br = brass(mats)
    parts = []
    for sx in (1, -1):
        x = sx * 0.10 * tw
        parts.append(M.rounded_box(0.10, 0.06, 0.075, lt, (x, 0.21, 0.03), r=0.01, seg=6, p=4))
        parts.append(M.rounded_box(0.104, 0.068, 0.025, ld, (x, 0.213, 0.06), r=0.008, seg=6, p=4))
        parts.append(M.sphere(0.008, br, (x, 0.246, 0.045), seg=5))
    parts.append(M.rounded_box(0.18, 0.08, 0.10, lt, (0.0, -0.215, 0.02), r=0.012, seg=6, p=4))         # rear box
    parts.append(M.rounded_box(0.185, 0.088, 0.03, ld, (0.0, -0.218, 0.06), r=0.01, seg=6, p=4))
    T = M.translate(-0.27, -0.07, 0.04) @ M.rot_x(24)
    sc = mat(mats, 'scabbard', '#1e1c1a', spec=0.12, gloss=14)
    parts.append(M.Mesh.concat([M.rounded_box(0.05, 0.05, 0.08, lt, (0, 0, -0.01), r=0.01, seg=6, p=4),
                                M.capsule((0, 0, -0.02), (0, 0, -0.42), 0.017, 0.012, sc, seg=6, rings=1),
                                M.sphere(0.016, steel(mats), (0, 0, -0.43), seg=6)]).transformed(T))
    return M.Mesh.concat(parts)


def ija_knapsack(mats):
    """(chest) The IJA knapsack (haiso) with the blanket and shelter half
    rolled round it in a horseshoe, the mess tin strapped on its back."""
    pk = mat(mats, 'pack', '#7a6e4a', spec=0.04)
    roll = mat(mats, 'blanket', '#ffffff', spec=0.03)
    st = mat(mats, 'strap', '#5a4a30', spec=0.06)
    tin = mat(mats, 'tin', '#8a8670', spec=0.25, gloss=16, metal=True)
    parts = [M.rounded_box(0.30, 0.13, 0.27, pk, (0, -0.21, 0.12), r=0.04, seg=10, p=3.5)]
    # the horseshoe roll: over the top and down both sides
    pts = [(-0.19, -0.21, -0.02), (-0.20, -0.21, 0.18), (-0.12, -0.21, 0.30), (0.0, -0.21, 0.32),
           (0.12, -0.21, 0.30), (0.20, -0.21, 0.18), (0.19, -0.21, -0.02)]
    for a, b in zip(pts[:-1], pts[1:]):
        a, b = np.array(a, np.float32), np.array(b, np.float32)
        d = b - a
        L = float(np.linalg.norm(d))
        # a roll of cloth: fat between the ties, pinched at them, a fold ridge along it
        seg = M.lathe([(0.0, 0.062), (0.2, 0.085), (0.5, 0.092), (0.8, 0.085), (1.0, 0.062)], roll, seg=12)
        parts.append(seg.transformed(M.look_along(tuple(a), tuple(b))))
        parts.append(M.capsule(tuple(a + d * 0.2 + (0, -0.084, 0)), tuple(a + d * 0.8 + (0, -0.084, 0)), 0.012, 0.012,
                               roll, seg=5, rings=1))
        # the tie at each joint
        parts.append(M.sphere(0.07, st, tuple(a), seg=10))
    parts.append(M.sphere(0.07, st, tuple(pts[-1]), seg=10))
    parts.append(M.box(0.31, 0.016, 0.02, st, (0, -0.21, 0.31)))
    parts.append(M.rounded_box(0.16, 0.05, 0.12, tin, (0, -0.29, 0.10), r=0.03, seg=8, p=3))           # mess tin
    for x in (-0.1, 0.1):
        parts.append(M.capsule((x, -0.15, 0.27), (x, 0.06, 0.30), 0.016, 0.016, st, seg=5, rings=1))
        parts.append(M.capsule((x, 0.10, 0.26), (x * 1.2, 0.13, 0.02), 0.015, 0.015, st, seg=5, rings=1))
    return M.Mesh.concat(parts)


def ija_canteen(mats):
    """(pelvis) The aluminium canteen in its harness on the right hip, the
    bread bag on the left, their straps going up (drawn on the chest)."""
    alu = mat(mats, 'canteen', '#7a7a5a', spec=0.2, gloss=14, metal=True)
    bag = mat(mats, 'breadbag', '#8a8060', spec=0.03)
    st = mat(mats, 'strap', '#5a4a30', spec=0.06)
    parts = [M.ellipsoid(0.07, 0.045, 0.09, alu, (0.30, 0.03, -0.08), seg=10),
             M.cylinder(0.016, 0.016, 0.04, mat(mats, 'cork', '#3a2a1c'), seg=6, z0=0.0).transformed(M.translate(0.30, 0.03, 0.0))]
    for z in (-0.04, -0.12):
        parts.append(M.cylinder(0.073, 0.073, 0.012, st, seg=10, z0=z).transformed(
            M.translate(0.30, 0.03, 0) @ M.scale(1, 0.66, 1)))
    parts.append(M.rounded_box(0.20, 0.08, 0.17, bag, (-0.27, 0.05, -0.10), r=0.03, seg=8, p=3))
    parts.append(M.rounded_box(0.205, 0.085, 0.06, bag, (-0.27, 0.055, -0.04), r=0.02, seg=6, p=3))
    return M.Mesh.concat(parts)
