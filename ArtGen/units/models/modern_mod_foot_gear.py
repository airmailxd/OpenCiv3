"""Gear for Modern's foot units (catalog/modern_mod_foot.py): the Super
Trooper's laser rifle, the Combat Engineer's flamethrower, the RPG, a
submachine gun, an AK and a musket with bayonets, an M1 carbine, the
Battalion's machine gun, and a stick.

Held props follow models/props.py: the grip (the right hand) at the origin,
the long axis +z (the muzzle end), the top (sights) toward -y. GUNS gives,
like props_foot_modern.GUNS, each firearm's muzzle (z), where the left hand
holds it (support: (y, z)) and its butt (z).
"""
import math

import mesh as M
from models.props import mat, steel, wood
from models import props_foot_modern as G

GUNS = {
    'laser_rifle': {'muzzle': 0.66, 'support': (0.04, 0.30), 'butt': -0.30},
    'flamethrower': {'muzzle': 0.74, 'support': (0.05, 0.40), 'butt': -0.12},
    'rpg': {'muzzle': 0.66, 'support': (0.04, 0.22), 'butt': -0.50},
    'smg': {'muzzle': 0.40, 'support': (0.05, 0.20), 'butt': -0.24},
    'ak_bayonet': {'muzzle': 0.64, 'support': (0.03, 0.28), 'butt': -0.30},
    'musket_bayonet': {'muzzle': 1.00, 'support': (0.02, 0.36), 'butt': -0.36},
    'm1_carbine': {'muzzle': 0.60, 'support': (0.02, 0.26), 'butt': -0.28},
    'hmg': {'muzzle': 1.00, 'support': (-0.02, 0.18), 'butt': -0.18},
}


def _rot_to_y(m):
    """A mesh made along +z (prop space) turned so its long axis is +y."""
    return m.transformed(M.rot_x(-90))


def laser_rifle(mats, muzzle=0.66):
    """A bulky futuristic rifle: a long slab body, a thick emitter barrel
    ending in a pale lens, a power cell under it."""
    body = mat(mats, 'rifle_body', '#3a3d44', spec=0.45, gloss=22, metal=True)
    dark = mat(mats, 'rifle_dark', '#202226', spec=0.3, gloss=18)
    lens = mat(mats, 'lens', '#cfe4f4', spec=0.9, gloss=40)
    parts = [M.rounded_box(0.06, 0.09, 0.46, body, (0, 0.0, 0.10), r=0.02, seg=8, p=4)]
    parts.append(M.rounded_box(0.05, 0.07, 0.18, dark, (0, 0.02, -0.20), r=0.02, seg=8, p=4))     # stock
    parts.append(M.cylinder(0.032, 0.028, muzzle - 0.30, body, seg=10, z0=0.30))                   # emitter
    for z in (0.40, 0.50):
        parts.append(M.cylinder(0.040, 0.040, 0.03, dark, seg=10, z0=z))                            # cooling rings
    parts.append(M.cylinder(0.026, 0.026, 0.02, lens, seg=10, z0=muzzle - 0.02))
    parts.append(M.rounded_box(0.045, 0.09, 0.10, dark, (0, 0.08, 0.12), r=0.015, seg=6, p=4))   # power cell
    parts.append(M.box(0.03, 0.08, 0.035, dark, (0, 0.05, -0.01)).transformed(M.rot_x(-15)))       # grip
    parts.append(M.rounded_box(0.03, 0.03, 0.10, dark, (0, -0.06, 0.08), r=0.01, seg=6, p=4))     # sight
    return M.Mesh.concat(parts)


def flamethrower(mats, muzzle=0.74):
    """The flame gun: a tube with two grips, a fuel valve block at the back
    and a wide nozzle with its igniter."""
    g = G.gunmetal(mats)
    o = mat(mats, 'olive', '#5a5e3e', spec=0.08)
    parts = [M.cylinder(0.026, 0.026, muzzle + 0.10, g, seg=10, z0=-0.10)]
    parts.append(M.rounded_box(0.07, 0.09, 0.16, o, (0, 0.02, -0.02), r=0.02, seg=8, p=4))          # valve block
    parts.append(M.box(0.03, 0.09, 0.035, g, (0, 0.06, 0.0)).transformed(M.rot_x(-12)))             # rear grip
    parts.append(M.box(0.03, 0.09, 0.035, g, (0, 0.07, 0.40)))                                      # front grip
    parts.append(M.lathe([(muzzle - 0.10, 0.03), (muzzle - 0.02, 0.042), (muzzle, 0.044)], g, seg=10))
    parts.append(M.cylinder(0.012, 0.012, 0.10, steel(mats), seg=6, z0=muzzle - 0.14).transformed(
        M.translate(0, -0.045, 0)))                                                                 # igniter
    return M.Mesh.concat(parts)


def rpg(mats, muzzle=0.66):
    """An RPG-7: a long tube (to z = -0.5 behind the grip) with a flared
    venturi at the back, wooden heat shields, and the bulbous warhead in front."""
    g = G.gunmetal(mats)
    w = G.stock(mats)
    war = mat(mats, 'warhead', '#5a6048', spec=0.2, gloss=14)
    parts = [M.cylinder(0.034, 0.034, 0.84, g, seg=10, z0=-0.46)]
    parts.append(M.lathe([(-0.52, 0.055), (-0.46, 0.038), (-0.40, 0.034)], g, seg=10))              # venturi
    parts.append(M.cylinder(0.044, 0.044, 0.22, w, seg=10, z0=-0.02))                               # heat shield
    parts.append(M.box(0.03, 0.08, 0.035, g, (0, 0.05, 0.0)).transformed(M.rot_x(-12)))             # grip
    parts.append(M.box(0.03, 0.07, 0.035, g, (0, 0.05, 0.22)))
    parts.append(M.box(0.02, 0.05, 0.06, g, (0.03, -0.05, 0.12)))                                   # sight
    # the warhead: a stalk, then a cone and a bulb
    parts.append(M.cylinder(0.022, 0.022, 0.10, war, seg=8, z0=0.36))
    parts.append(M.lathe([(0.44, 0.03), (0.50, 0.07), (0.58, 0.07), (muzzle - 0.01, 0.022), (muzzle, 0.0)], war, seg=12))
    return M.Mesh.concat(parts)


def rpg_round(mats):
    """An RPG round in flight (along +y)."""
    war = mat(mats, 'warhead', '#5a6048', spec=0.2, gloss=14)
    return _rot_to_y(M.Mesh.concat([M.cylinder(0.022, 0.022, 0.30, war, seg=8, z0=-0.30),
                                    M.lathe([(0.0, 0.03), (0.06, 0.07), (0.14, 0.07), (0.22, 0.0)], war, seg=10)]))


def smg(mats, muzzle=0.40):
    """A compact black submachine gun with a short stock and a straight magazine."""
    p = mat(mats, 'polymer', '#24252a', spec=0.25, gloss=16)
    g = G.gunmetal(mats)
    parts = [M.box(0.03, 0.05, 0.18, p, (0, 0.005, -0.15))]
    parts.append(M.box(0.045, 0.065, 0.26, g, (0, 0.0, 0.08)))
    parts.append(M.cylinder(0.022, 0.022, 0.10, p, seg=8, z0=0.21))
    parts.append(M.cylinder(0.011, 0.011, muzzle - 0.30, g, seg=8, z0=0.30))
    parts.append(M.box(0.028, 0.12, 0.035, g, (0, 0.08, 0.10)).transformed(M.rot_x(6)))
    parts.append(M.box(0.03, 0.08, 0.035, p, (0, 0.05, -0.01)).transformed(M.rot_x(-15)))
    return M.Mesh.concat(parts)


def _bayonet(mats, muzzle, length=0.26, dy=0.03):
    st = steel(mats)
    return M.Mesh.concat([M.cylinder(0.016, 0.016, 0.04, st, seg=8, z0=muzzle - 0.04).transformed(M.translate(0, dy * 0.7, 0)),
                          M.blade(length, 0.022, 0.008, st, tip=0.12, z0=muzzle - 0.02).transformed(M.translate(0, dy, 0))])


def ak_bayonet(mats, muzzle=0.64):
    return M.Mesh.concat([G.ak(mats, muzzle), _bayonet(mats, muzzle, 0.22, 0.03)])


def musket_bayonet(mats, muzzle=1.00):
    """A flintlock musket of the line, steel barrel and bands, a long socket bayonet."""
    s = G.stock(mats)
    st = steel(mats)
    parts = [G._stock_wood(mats, -0.36, 0.90, depth=0.08, m=s)]
    parts.append(G._barrel(mats, 0.0, muzzle, 0.015, st))
    for z in (0.40, 0.70, 0.90):
        parts.append(M.cylinder(0.026, 0.026, 0.02, st, seg=8, z0=z).transformed(M.translate(0, 0.008, 0)))
    parts.append(M.box(0.02, 0.035, 0.07, st, (0.028, 0.0, 0.03)))                                  # the lock
    parts.append(_bayonet(mats, muzzle, 0.40, 0.024))
    return M.Mesh.concat(parts)


def m1_carbine(mats, muzzle=0.60):
    """A light WWII carbine: a slim wooden stock and handguard, a short
    magazine, a sling."""
    s = G.stock(mats)
    g = G.gunmetal(mats)
    parts = [G._stock_wood(mats, -0.28, 0.46, depth=0.07, wrist=0.028, m=s)]
    parts.append(G._barrel(mats, 0.0, muzzle, 0.012, g))
    parts.append(M.box(0.034, 0.04, 0.16, g, (0, -0.004, 0.05)))
    parts.append(M.box(0.026, 0.022, 0.24, s, (0, -0.02, 0.32)))
    parts.append(M.box(0.026, 0.06, 0.035, g, (0, 0.05, 0.10)))
    parts.append(M.capsule((0, 0.05, -0.22), (0, 0.06, 0.36), 0.008, 0.008, mat(mats, 'strap', '#4a3a2a'), seg=4, rings=1))
    return M.Mesh.concat(parts)


def hmg(mats, muzzle=1.00):
    """A belt-fed machine gun: a square receiver, a long barrel in a
    perforated jacket, a carrying handle, an ammunition can on the left and
    a short belt."""
    g = G.gunmetal(mats)
    o = mat(mats, 'olive', '#5a5e3e', spec=0.08)
    parts = [M.box(0.08, 0.10, 0.30, g, (0, 0.0, 0.02))]                                          # receiver
    parts.append(M.box(0.04, 0.05, 0.12, g, (0, 0.0, -0.13)))                                       # back plate
    for sx in (1, -1):
        parts.append(M.capsule((sx * 0.025, 0.02, -0.18), (sx * 0.03, 0.05, -0.24), 0.012, 0.012, g, seg=5, rings=1))
    parts.append(M.cylinder(0.034, 0.034, 0.44, g, seg=10, z0=0.16))                                # jacket
    for z in (0.22, 0.30, 0.38, 0.46, 0.54):
        parts.append(M.cylinder(0.038, 0.038, 0.02, mat(mats, 'olive_dark', '#3e4230'), seg=10, z0=z))
    parts.append(M.cylinder(0.014, 0.014, muzzle - 0.58, g, seg=8, z0=0.58))
    parts.append(M.cylinder(0.022, 0.016, 0.05, g, seg=8, z0=muzzle - 0.05))
    parts.append(M.capsule((0, -0.06, 0.02), (0, -0.09, 0.10), 0.01, 0.01, g, seg=4, rings=1))      # handle
    parts.append(M.capsule((0, -0.09, 0.10), (0, -0.06, 0.18), 0.01, 0.01, g, seg=4, rings=1))
    parts.append(M.rounded_box(0.10, 0.12, 0.14, o, (-0.10, 0.04, 0.04), r=0.012, seg=6, p=4))      # ammo can
    for i in range(4):
        parts.append(M.box(0.04, 0.016, 0.024, mat(mats, 'brass', '#b8963e', spec=0.7, gloss=24, metal=True),
                           (-0.045, 0.03 + 0.02 * i, 0.05)))
    return M.Mesh.concat(parts)


def stick(mats, length=1.0):
    """A stout walking stick, the grip a third from the top, a knob there."""
    w = mat(mats, 'stick', '#6a4a2c', spec=0.08)
    return M.Mesh.concat([M.cylinder(0.022, 0.018, length, w, seg=8, z0=-0.3),
                          M.sphere(0.035, w, (0, 0, length - 0.31), seg=8)])


# ------------------------------------------------------------ worn gear (chest bone space)

def fuel_tanks(mats):
    """Two fuel cylinders and a pressure ball between them, on a back frame."""
    t = mat(mats, 'tank', '#6a6c5c', spec=0.35, gloss=18, metal=True)
    g = G.gunmetal(mats)
    parts = []
    for sx in (1, -1):
        parts.append(M.capsule((sx * 0.085, -0.24, -0.16), (sx * 0.085, -0.24, 0.30), 0.075, 0.075, t, seg=12, rings=3))
        parts.append(M.cylinder(0.02, 0.02, 0.05, g, seg=6, z0=0.36).transformed(M.translate(sx * 0.085, -0.24, 0)))
    parts.append(M.sphere(0.06, t, (0, -0.32, 0.06), seg=10))
    parts.append(M.box(0.22, 0.03, 0.04, g, (0, -0.17, 0.22)))
    parts.append(M.box(0.22, 0.03, 0.04, g, (0, -0.17, -0.06)))
    for x in (-0.1, 0.1):
        parts.append(M.capsule((x, -0.15, 0.27), (x, 0.08, 0.29), 0.017, 0.017, mat(mats, 'strap', '#3e3e30'), seg=5, rings=1))
    # the hose: from the bottom of the right tank round the hip
    parts.append(M.capsule((0.085, -0.24, -0.22), (0.20, -0.12, -0.34), 0.016, 0.016, g, seg=5, rings=1))
    parts.append(M.capsule((0.20, -0.12, -0.34), (0.24, 0.08, -0.30), 0.016, 0.016, g, seg=5, rings=1))
    return M.Mesh.concat(parts)


def rocket_bag(mats):
    """A canvas carrier with two spare RPG rounds, their heads up."""
    c = mat(mats, 'pack', '#6a5a40', spec=0.04)
    war = mat(mats, 'warhead', '#5a6048', spec=0.2, gloss=14)
    parts = [M.rounded_box(0.30, 0.13, 0.30, c, (0, -0.21, 0.08), r=0.04, seg=10, p=3.5)]
    for sx in (1, -1):
        parts.append(M.cylinder(0.02, 0.02, 0.16, war, seg=8, z0=0.22).transformed(M.translate(sx * 0.08, -0.22, 0)))
        parts.append(M.lathe([(0.36, 0.03), (0.40, 0.065), (0.47, 0.065), (0.55, 0.0)], war, seg=10).transformed(
            M.translate(sx * 0.08, -0.22, 0)))
    for x in (-0.1, 0.1):
        parts.append(M.capsule((x, -0.15, 0.26), (x, 0.08, 0.28), 0.017, 0.017, mat(mats, 'strap', '#3e3e30'), seg=5, rings=1))
    return M.Mesh.concat(parts)


def power_pack(mats):
    """The power armour's back unit: a rounded box with two vents."""
    d = mat(mats, 'armor_dark', '#34363c', spec=0.45, gloss=22, metal=True)
    v = mat(mats, 'vent', '#1c1d20', spec=0.2)
    parts = [M.rounded_box(0.36, 0.18, 0.36, d, (0, -0.27, 0.12), r=0.06, seg=10, p=3.5)]
    for sx in (1, -1):
        parts.append(M.cylinder(0.045, 0.05, 0.12, v, seg=10, z0=0.26).transformed(M.translate(sx * 0.1, -0.30, 0)))
    return M.Mesh.concat(parts)


def ammo_crate(mats):
    """An ammunition can carried by its handle (grip at the origin, hanging down -z)."""
    o = mat(mats, 'can', '#4a5036', spec=0.25, gloss=16)
    g = G.gunmetal(mats)
    return M.Mesh.concat([M.box(0.13, 0.30, 0.20, o, (0, 0, -0.13)),
                          M.box(0.14, 0.31, 0.025, g, (0, 0, -0.03)),
                          M.box(0.03, 0.12, 0.025, g, (0, 0, -0.005))])
