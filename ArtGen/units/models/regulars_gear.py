"""Gear for the regular modern foot units of round 2 (models/regulars_foot.py):
the RPG Trooper's RPG-7 and rocket carrier, the Special Forces' suppressed
carbine and radio, the Super Trooper's pulse rifle and power pack, the Combat
Engineer's flamethrower, tank pack and tools.

Held props follow models/props.py: the grip (the right hand) at the origin,
the long axis +z (the muzzle end), the top (sights) toward -y. GUNS gives each
firearm's muzzle (z), where the left hand holds it (support: (y, z)) and its
butt (z). Guns are thickened 1.6x across (x, y) when held, like the stock
ones, so they are drawn slim here.

Worn gear is in chest bone space (z up from the bottom of the chest, the
shoulders at z ~0.22-0.33, the back at y ~ -0.15) unless said otherwise.
"""
import math

import mesh as M
from models.props import mat, steel
from models import props_foot_modern as G

GUNS = {
    'rpg7': {'muzzle': 0.80, 'support': (0.06, 0.24), 'butt': -0.58},
    'sf_carbine': {'muzzle': 0.66, 'support': (0.07, 0.26), 'butt': -0.27},
    'pulse_rifle': {'muzzle': 0.62, 'support': (0.06, 0.30), 'butt': -0.22},
    'flame_gun': {'muzzle': 0.70, 'support': (0.07, 0.40), 'butt': -0.16},
    'rpg7_empty': {'muzzle': 0.80, 'support': (0.06, 0.24), 'butt': -0.58},
}


def _poly(mats):
    return mat(mats, 'polymer', '#26272b', spec=0.25, gloss=16)


def _strap(mats, name='strap', col='#3a3628'):
    return mat(mats, name, col, spec=0.04)


def _bar(a, b, r, m, seg=6):
    return M.capsule(a, b, r, r, m, seg=seg, rings=1)


# ------------------------------------------------------------ the RPG-7

def rpg7(mats, muzzle=0.80, loaded=True):
    """An RPG-7 loaded with a PG-7V round: a steel tube (its axis 0.045 above
    the grip) from the flared venturi at the back to the muzzle; two wooden
    heat guards; a pistol grip with its trigger guard and a front grip
    under the tube; the optical sight box on the left; a sling. In the
    muzzle the round: the booster stalk with fins, then the big cone."""
    g = G.gunmetal(mats)
    dk = mat(mats, 'gun_dark', '#1e1f22', spec=0.3, gloss=16)
    w = mat(mats, 'stock', '#7a4e2a', spec=0.14, gloss=14)
    war = mat(mats, 'warhead', '#4e5640', spec=0.25, gloss=16)
    tip = mat(mats, 'warhead_tip', '#8a8670', spec=0.5, gloss=22, metal=True)
    y = -0.045
    parts = []
    T = M.translate(0, y, 0)
    parts.append(M.cylinder(0.022, 0.022, 0.80, g, seg=12, z0=-0.42).transformed(T))                 # tube
    # the venturi: a flared bell at the back, a ring before it
    parts.append(M.lathe([(-0.60, 0.044), (-0.585, 0.046), (-0.53, 0.032), (-0.47, 0.024), (-0.42, 0.023)], dk,
                         seg=12).transformed(T))
    parts.append(M.cylinder(0.028, 0.028, 0.018, g, seg=12, z0=-0.47).transformed(T))
    # wooden heat guards (front one long, rear one short), each with a steel band
    parts.append(M.cylinder(0.031, 0.031, 0.20, w, seg=12, z0=0.02).transformed(T))
    parts.append(M.cylinder(0.031, 0.031, 0.12, w, seg=12, z0=-0.20).transformed(T))
    for z in (0.02, 0.21, -0.20, -0.09):
        parts.append(M.cylinder(0.033, 0.033, 0.012, g, seg=12, z0=z).transformed(T))
    parts.append(M.cylinder(0.026, 0.026, 0.03, dk, seg=12, z0=0.355).transformed(T))                # muzzle ring
    # the pistol grip, raked back, its trigger guard; the front grip
    parts.append(M.box(0.026, 0.085, 0.034, dk, (0, 0.03, -0.005)).transformed(M.rot_x(-14)))
    parts.append(M.box(0.008, 0.03, 0.05, g, (0, -0.005, 0.045)))
    parts.append(_bar((0, -0.02, 0.012), (0, 0.022, 0.03), 0.005, g, seg=4))
    parts.append(_bar((0, 0.022, 0.03), (0, 0.02, 0.07), 0.005, g, seg=4))
    parts.append(M.box(0.026, 0.07, 0.03, dk, (0, 0.025, 0.24)))
    # the sight box on the left (-x), with its lens and a rubber eye cup at the back
    parts.append(M.box(0.026, 0.04, 0.09, dk, (-0.04, y - 0.01, -0.03)))
    parts.append(M.cylinder(0.011, 0.011, 0.03, dk, seg=8, z0=-0.105).transformed(M.translate(-0.04, y - 0.012, 0)))
    parts.append(M.cylinder(0.009, 0.009, 0.006, mat(mats, 'lens', '#8ab0c8', spec=0.9, gloss=40), seg=8,
                            z0=0.015).transformed(M.translate(-0.04, y - 0.012, 0)))
    # iron sights on top
    parts.append(M.box(0.006, 0.026, 0.012, g, (0, y - 0.03, 0.30)))
    parts.append(M.box(0.012, 0.022, 0.014, g, (0, y - 0.03, -0.05)))
    # the sling along the underside
    st = _strap(mats, 'sling', '#3c3a2c')
    parts.append(_bar((0.0, y + 0.03, -0.38), (0.0, y + 0.07, -0.05), 0.006, st, seg=4))
    parts.append(_bar((0.0, y + 0.07, 0.10), (0.0, y + 0.035, 0.33), 0.006, st, seg=4))
    # the round: booster stalk with fins, then the cone of the warhead
    if not loaded:
        return M.Mesh.concat(parts)
    parts.append(M.cylinder(0.017, 0.017, 0.10, war, seg=10, z0=0.38).transformed(T))
    for k in range(4):
        parts.append(M.box(0.004, 0.034, 0.05, dk, (0, 0, 0.0)).transformed(
            T @ M.translate(0, 0, 0.41) @ M.rot_z(45 + 90 * k) @ M.translate(0, 0.02, 0)))
    parts.append(M.lathe([(0.47, 0.018), (0.50, 0.03), (0.54, 0.046), (0.60, 0.05), (0.66, 0.048),
                          (0.72, 0.03), (0.77, 0.014), (muzzle - 0.012, 0.01)], war, seg=14).transformed(T))
    parts.append(M.cylinder(0.052, 0.052, 0.012, dk, seg=14, z0=0.585).transformed(T))                # body seam
    parts.append(M.lathe([(muzzle - 0.04, 0.011), (muzzle - 0.01, 0.009), (muzzle, 0.0001)], tip, seg=8).transformed(T))
    return M.Mesh.concat(parts)


def rpg7_empty(mats, muzzle=0.80):
    """The RPG-7 after the shot: the tube without its round."""
    return rpg7(mats, muzzle, loaded=False)


def rpg_rocket(mats):
    """The PG-7 round in flight (along +z, its tip at the origin): the cone,
    the stalk, the sustainer and four fins opened out at the tail."""
    war = mat(mats, 'warhead', '#4e5640', spec=0.25, gloss=16)
    dk = mat(mats, 'gun_dark', '#1e1f22', spec=0.3, gloss=16)
    parts = [M.lathe([(-0.33, 0.018), (-0.30, 0.03), (-0.26, 0.046), (-0.20, 0.05), (-0.14, 0.048),
                      (-0.08, 0.03), (-0.03, 0.014), (0.0, 0.0001)], war, seg=12)]
    parts.append(M.cylinder(0.017, 0.017, 0.22, war, seg=8, z0=-0.55))
    parts.append(M.cylinder(0.022, 0.022, 0.08, dk, seg=8, z0=-0.55))
    for k in range(4):
        parts.append(M.box(0.006, 0.07, 0.07, dk, (0, 0.045, 0)).transformed(
            M.translate(0, 0, -0.52) @ M.rot_z(45 + 90 * k)))
    return M.Mesh.concat(parts)


def rocket_carrier(mats):
    """A three-tube rocket carrier on the back (chest space): three canvas
    tubes side by side, each with a spare round standing in it warhead up,
    the cones over the shoulder line but below the helmet; a flap, two
    compression straps round the tubes and two shoulder straps."""
    c = mat(mats, 'carrier', '#6a5e42', spec=0.04)
    cd = mat(mats, 'carrier_dark', '#4e4430', spec=0.04)
    war = mat(mats, 'warhead', '#4e5640', spec=0.25, gloss=16)
    tip = mat(mats, 'warhead_tip', '#8a8670', spec=0.5, gloss=22, metal=True)
    dk = mat(mats, 'gun_dark', '#1e1f22', spec=0.3, gloss=16)
    st = _strap(mats)
    yc = -0.22
    parts = [M.rounded_box(0.30, 0.04, 0.30, cd, (0, -0.17, 0.05), r=0.012, seg=6, p=4)]           # back panel
    for k, x in enumerate((-0.095, 0.0, 0.095)):
        y = yc - (0.015 if k == 1 else 0.0)
        parts.append(M.cylinder(0.046, 0.046, 0.32, c, seg=12, z0=-0.13).transformed(M.translate(x, y, 0)))   # tube
        parts.append(M.cylinder(0.049, 0.049, 0.02, cd, seg=12, z0=0.17).transformed(M.translate(x, y, 0)))   # rim
        h = 0.0 if k == 1 else -0.03
        T = M.translate(x, y, 0.19 + h)
        parts.append(M.cylinder(0.017, 0.017, 0.04, war, seg=8, z0=0.0).transformed(T))
        parts.append(M.lathe([(0.03, 0.02), (0.06, 0.04), (0.10, 0.046), (0.14, 0.042), (0.18, 0.026),
                              (0.21, 0.01), (0.225, 0.0001)], war, seg=12).transformed(T))
        parts.append(M.cylinder(0.048, 0.048, 0.008, dk, seg=12, z0=0.10).transformed(T))
        parts.append(M.lathe([(0.20, 0.011), (0.22, 0.006), (0.23, 0.0001)], tip, seg=8).transformed(T))
    for z in (-0.06, 0.10):
        parts.append(M.box(0.31, 0.11, 0.03, cd, (0, yc - 0.005, z)))                                 # straps round them
    for x in (-0.10, 0.10):
        parts.append(_bar((x, -0.16, 0.24), (x * 1.05, 0.02, 0.33), 0.018, st))
        parts.append(_bar((x * 1.05, 0.02, 0.33), (x * 1.2, 0.15, 0.22), 0.018, st))
    return M.Mesh.concat(parts)


def rpg_rig(mats, ry=0.15, rx=0.25):
    """A chest rig over the shirt (chest space): a khaki panel across the
    belly, three tall booster pouches with flaps, a grenade pouch, a
    shoulder harness and a drag handle at the back."""
    c = mat(mats, 'rig', '#7a6c4a', spec=0.04)
    cd = mat(mats, 'rig_dark', '#5c5038', spec=0.04)
    bk = mat(mats, 'buckle', '#2a2a26', spec=0.3, gloss=16)
    y0 = ry + 0.035
    parts = [M.rounded_box(rx * 1.8, 0.05, 0.22, c, (0, y0 - 0.012, 0.03), r=0.02, seg=8, p=4)]
    for k, x in enumerate((-0.10, 0.0, 0.10)):
        parts.append(M.rounded_box(0.075, 0.06, 0.15, c, (x, y0 + 0.04, 0.05), r=0.02, seg=8, p=3))   # pouch
        parts.append(M.rounded_box(0.08, 0.066, 0.035, cd, (x, y0 + 0.042, 0.12), r=0.012, seg=6, p=3))  # flap
        parts.append(M.box(0.016, 0.01, 0.02, bk, (x, y0 + 0.077, 0.10)))                             # snap
    parts.append(M.rounded_box(0.06, 0.05, 0.08, cd, (0.20, y0 + 0.0, 0.03), r=0.015, seg=6, p=3))   # grenade
    parts.append(M.rounded_box(0.06, 0.05, 0.08, cd, (-0.20, y0 + 0.0, 0.03), r=0.015, seg=6, p=3))
    # the harness: up over the shoulders and down the back to an X
    for sx in (1, -1):
        x = sx * 0.12
        parts.append(_bar((x, y0, 0.10), (x * 0.95, ry * 0.95 + 0.02, 0.24), 0.02, cd))
        parts.append(_bar((x * 0.95, ry * 0.95 + 0.02, 0.24), (x * 0.9, 0.0, 0.335), 0.02, cd))
        parts.append(_bar((x * 0.9, 0.0, 0.335), (x * 0.95, -ry * 0.9, 0.24), 0.02, cd))
        parts.append(_bar((x * 0.95, -ry * 0.9, 0.24), (-x * 0.9, -ry - 0.015, 0.06), 0.018, cd))
    return M.Mesh.concat(parts)


# ------------------------------------------------------------ Special Forces

def sf_carbine(mats, muzzle=0.66):
    """A suppressed short carbine (M4 / HK416 pattern): a collapsible stock
    on its buffer tube, the receiver with a long top rail, a free-float
    handguard, a fat suppressor; a red-dot sight with a magnifier behind
    it, a laser box on the rail's left, a vertical grip, a curved magazine,
    a sling."""
    p = _poly(mats)
    g = G.gunmetal(mats)
    tan = mat(mats, 'gun_tan', '#8a7a58', spec=0.15, gloss=14)
    lens = mat(mats, 'lens', '#8ab0c8', spec=0.9, gloss=40)
    parts = []
    parts.append(M.box(0.034, 0.075, 0.12, p, (0, 0.008, -0.21)))                                   # stock
    parts.append(M.box(0.036, 0.03, 0.05, p, (0, -0.02, -0.17)))                                    # cheek riser
    parts.append(M.cylinder(0.014, 0.014, 0.14, g, seg=8, z0=-0.16).transformed(M.translate(0, -0.006, 0)))
    parts.append(M.box(0.042, 0.06, 0.22, g, (0, 0.0, 0.03)))                                      # receiver
    parts.append(M.box(0.022, 0.012, 0.42, g, (0, -0.036, 0.12)))                                   # top rail
    parts.append(M.box(0.04, 0.05, 0.20, tan, (0, 0.0, 0.24)))                                     # handguard
    for z in (0.18, 0.24, 0.30):
        parts.append(M.box(0.042, 0.008, 0.012, g, (0, 0.024, z)))                                  # vents
    parts.append(M.cylinder(0.008, 0.008, 0.04, g, seg=6, z0=0.34))                                 # barrel stub
    parts.append(M.cylinder(0.022, 0.022, muzzle - 0.37, p, seg=12, z0=0.37))                       # suppressor
    parts.append(M.cylinder(0.024, 0.024, 0.02, g, seg=12, z0=0.37))
    parts.append(M.cylinder(0.024, 0.024, 0.014, g, seg=12, z0=muzzle - 0.014))
    # optic: a red dot (box with lens) and a magnifier tube behind
    parts.append(M.rounded_box(0.034, 0.04, 0.05, p, (0, -0.062, 0.10), r=0.008, seg=6, p=4))
    parts.append(M.cylinder(0.012, 0.012, 0.004, lens, seg=8, z0=0.124).transformed(M.translate(0, -0.066, 0)))
    parts.append(M.cylinder(0.014, 0.014, 0.06, p, seg=8, z0=0.0).transformed(M.translate(0, -0.066, 0)))
    parts.append(M.box(0.018, 0.012, 0.03, g, (0, -0.045, 0.03)))
    # laser box on the left of the rail, vertical grip under the handguard
    parts.append(M.box(0.024, 0.03, 0.06, tan, (-0.034, -0.02, 0.28)))
    parts.append(M.box(0.022, 0.06, 0.026, p, (0, 0.055, 0.26)))
    # magazine (curved), pistol grip, trigger guard
    for i in range(3):
        parts.append(M.box(0.026, 0.05, 0.045, tan, (0, 0.045 + 0.04 * i, 0.09 + 0.012 * i * i)))
    parts.append(M.box(0.028, 0.075, 0.034, p, (0, 0.05, -0.04)).transformed(M.rot_x(-16)))
    parts.append(M.box(0.008, 0.008, 0.06, g, (0, 0.045, 0.02)))
    sl = _strap(mats, 'sling', '#3a3628')
    parts.append(_bar((0.018, 0.03, -0.26), (0.02, 0.06, 0.0), 0.006, sl, seg=4))
    parts.append(_bar((0.02, 0.06, 0.0), (0.018, 0.02, 0.32), 0.006, sl, seg=4))
    return M.Mesh.concat(parts)


def plate_carrier(mats, ry=0.15, rx=0.25):
    """A plate carrier (chest space): front and back plates in their bags, a
    cummerbund round the sides, padded shoulder straps; on the front flap
    three rifle magazines in open pouches, an admin pouch above, a small
    pouch each side; a drag handle at the back."""
    c = mat(mats, 'carrier', '#7a6a4c', spec=0.05)
    cd = mat(mats, 'carrier_dark', '#5e5038', spec=0.05)
    tan = mat(mats, 'gun_tan', '#8a7a58', spec=0.15, gloss=14)
    bk = mat(mats, 'buckle', '#2a2a26', spec=0.3, gloss=16)
    yf = ry + 0.045
    parts = []
    parts.append(M.rounded_box(rx * 1.45, 0.05, 0.30, c, (0, yf - 0.012, 0.10), r=0.025, seg=8, p=4))   # front
    parts.append(M.rounded_box(rx * 1.5, 0.05, 0.32, c, (0, -yf + 0.006, 0.12), r=0.025, seg=8, p=4))   # back
    # cummerbund: a band round the ribs joining them
    parts.append(M.loft([(-0.03, rx + 0.03, ry + 0.035), (0.10, rx + 0.045, ry + 0.045)], cd, seg=18,
                        cap_bottom=False, cap_top=False))
    # shoulder pads
    for sx in (1, -1):
        parts.append(M.rounded_box(0.09, 0.32, 0.035, cd, (sx * 0.13, 0.0, 0.33), r=0.012, seg=6, p=4)
                     .transformed(M.translate(0, 0, 0)))
    # magazine pouches with the mags' tops showing
    for x in (-0.085, 0.0, 0.085):
        parts.append(M.rounded_box(0.07, 0.045, 0.12, cd, (x, yf + 0.04, 0.01), r=0.012, seg=6, p=4))
        parts.append(M.box(0.05, 0.03, 0.04, tan, (x, yf + 0.04, 0.085)))
        parts.append(M.box(0.055, 0.035, 0.012, bk, (x, yf + 0.04, 0.108)))
    parts.append(M.rounded_box(0.16, 0.035, 0.07, cd, (0, yf + 0.02, 0.15), r=0.012, seg=6, p=4))    # admin
    parts.append(M.box(0.10, 0.01, 0.035, mat(mats, 'patch', '#2e2e2a'), (0, yf + 0.042, 0.16)))     # its velcro
    for sx in (1, -1):
        parts.append(M.rounded_box(0.06, 0.045, 0.08, cd, (sx * 0.17, yf + 0.0, 0.0), r=0.012, seg=6, p=4))
    parts.append(_bar((-0.04, -yf - 0.03, 0.27), (0.04, -yf - 0.03, 0.27), 0.012, bk, seg=4))         # drag handle
    return M.Mesh.concat(parts)


def assault_pack(mats, ry=0.15):
    """A small assault pack on the back (chest space), a hydration tube over
    the right shoulder, and the radio on its left with the whip antenna
    rising high behind the head."""
    c = mat(mats, 'carrier', '#7a6a4c', spec=0.05)
    cd = mat(mats, 'carrier_dark', '#5e5038', spec=0.05)
    dk = mat(mats, 'gun_dark', '#1e1f22', spec=0.3, gloss=16)
    ant = mat(mats, 'antenna', '#1a1a1c', spec=0.2)
    y0 = -ry - 0.10
    parts = [M.rounded_box(0.26, 0.11, 0.28, c, (0.02, y0 - 0.02, 0.10), r=0.04, seg=8, p=3)]
    parts.append(M.rounded_box(0.20, 0.04, 0.14, cd, (0.03, y0 - 0.085, 0.06), r=0.015, seg=6, p=3))  # pocket
    parts.append(M.box(0.22, 0.01, 0.02, cd, (0.02, y0 - 0.075, 0.20)))                                # compression
    # the radio: a dark box on the left with a knob, the antenna up and a little back
    parts.append(M.rounded_box(0.07, 0.07, 0.17, cd, (-0.16, y0 + 0.02, 0.15), r=0.015, seg=6, p=3))
    parts.append(M.box(0.055, 0.05, 0.05, dk, (-0.16, y0 + 0.02, 0.25)))
    parts.append(M.cylinder(0.011, 0.011, 0.03, dk, seg=6, z0=0.275).transformed(M.translate(-0.145, y0 + 0.02, 0)))
    parts.append(M.capsule((-0.175, y0 + 0.02, 0.27), (-0.20, y0 - 0.06, 0.95), 0.009, 0.004, ant, seg=5, rings=1))
    # the hydration tube over the right shoulder
    tube = mat(mats, 'tube', '#2a2a26', spec=0.2)
    parts.append(_bar((0.12, y0 + 0.04, 0.24), (0.15, 0.0, 0.345), 0.009, tube, seg=5))
    parts.append(_bar((0.15, 0.0, 0.345), (0.13, ry + 0.06, 0.24), 0.009, tube, seg=5))
    return M.Mesh.concat(parts)


def nvg_helmet(mats, hm, rail, nv):
    """A high-cut helmet (head space, before the head scale): a shell cut high
    over the ears, side rails, a shroud on the front with the binocular
    night-vision goggles flipped up, a counterweight pouch at the back."""
    parts = []
    from models.humanoid import _dome
    parts.append(_dome(0.13, 0.142, 0.15, hm, (0, -0.006, 0.13), -0.01))
    # rim band (a slightly larger flat ring)
    parts.append(M.loft([(0.118, 0.133, 0.146), (0.132, 0.131, 0.144)], hm, seg=18, cap_bottom=False, cap_top=False))
    for sx in (1, -1):
        parts.append(M.box(0.018, 0.13, 0.03, rail, (sx * 0.128, -0.01, 0.15)).transformed(
            M.translate(sx * 0.0, 0, 0)))
    # front shroud, the mount's arm, and the binocular NVG flipped up: the
    # housing over the brow, two tubes standing up off it, their lenses up
    parts.append(M.box(0.07, 0.025, 0.07, rail, (0, 0.142, 0.215)))
    parts.append(M.box(0.04, 0.05, 0.03, rail, (0, 0.17, 0.25)))
    T = M.translate(0, 0.20, 0.26) @ M.rot_x(48)
    parts.append(M.rounded_box(0.13, 0.05, 0.045, nv, (0, 0, 0), r=0.012, seg=6, p=4).transformed(T))
    lens = mat(mats, 'nvg_lens', '#3e5a4a', spec=0.9, gloss=40)
    for sx in (1, -1):
        parts.append(M.cylinder(0.028, 0.025, 0.08, nv, seg=10, z0=0.0).transformed(T @ M.translate(sx * 0.04, 0.0, 0.0)))
        parts.append(M.cylinder(0.022, 0.022, 0.006, lens, seg=10, z0=0.08).transformed(T @ M.translate(sx * 0.04, 0.0, 0.0)))
    parts.append(M.rounded_box(0.08, 0.04, 0.05, rail, (0, -0.15, 0.17), r=0.012, seg=6, p=3))     # counterweight
    return M.Mesh.concat(parts)


def headset(mats, cup):
    """Electronic ear defenders (head space): two thick cups over the ears
    under the helmet's cut, a boom mic on the right."""
    parts = []
    for sx in (1, -1):
        parts.append(M.cylinder(0.05, 0.048, 0.045, cup, seg=12, z0=0.0).transformed(
            M.translate(sx * 0.10, 0.0, 0.10) @ M.rot_y(90 * sx)))
    parts.append(_bar((0.13, 0.03, 0.08), (0.06, 0.12, 0.04), 0.008, cup, seg=4))
    return M.Mesh.concat(parts)


# ------------------------------------------------------------ Super Trooper

def pulse_rifle(mats, muzzle=0.62):
    """A heavy near-future assault rifle, two-handed: a skeleton stock, a
    deep receiver with its ejection port and charging handle, a box
    magazine ahead of the pistol grip, a vented handguard with an
    underslung grenade tube, a thick barrel with a muzzle brake, a box
    optic on the top rail. No lights."""
    body = mat(mats, 'rifle_body', '#4a4a42', spec=0.5, gloss=24, metal=True)
    dark = mat(mats, 'rifle_dark', '#1e1f1c', spec=0.3, gloss=18)
    wr = mat(mats, 'armor_wear', '#9a9684', spec=0.6, gloss=26, metal=True)
    parts = []
    parts.append(M.box(0.03, 0.022, 0.18, dark, (0, -0.03, -0.17)))                                 # stock: top bar
    parts.append(M.box(0.03, 0.022, 0.16, dark, (0, 0.04, -0.17)).transformed(M.rot_x(-8)))          # lower bar
    parts.append(M.rounded_box(0.04, 0.10, 0.035, dark, (0, 0.005, -0.27), r=0.01, seg=6, p=4))      # butt pad
    parts.append(M.rounded_box(0.052, 0.085, 0.28, body, (0, 0.0, 0.06), r=0.012, seg=6, p=4))      # receiver
    parts.append(M.box(0.008, 0.03, 0.07, dark, (0.027, -0.012, 0.06)))                              # ejection port
    parts.append(M.box(0.03, 0.012, 0.03, wr, (-0.03, -0.03, 0.15)))                                # charging handle
    parts.append(M.box(0.02, 0.014, 0.40, dark, (0, -0.05, 0.12)))                                   # top rail
    parts.append(M.rounded_box(0.034, 0.05, 0.10, dark, (0, -0.085, 0.07), r=0.01, seg=6, p=4))      # optic
    parts.append(M.box(0.036, 0.02, 0.02, dark, (0, -0.085, 0.125)))
    parts.append(M.box(0.036, 0.12, 0.05, body, (0, 0.09, 0.10)).transformed(M.rot_x(6)))           # magazine
    parts.append(M.box(0.038, 0.015, 0.052, wr, (0, 0.15, 0.106)).transformed(M.rot_x(6)))
    parts.append(M.rounded_box(0.034, 0.08, 0.036, dark, (0, 0.055, -0.02), r=0.01, seg=6, p=4)
                 .transformed(M.rot_x(-14)))                                                         # grip
    parts.append(M.rounded_box(0.05, 0.06, 0.20, dark, (0, 0.005, 0.30), r=0.012, seg=6, p=4))      # handguard
    for z in (0.24, 0.29, 0.34):
        parts.append(M.box(0.054, 0.012, 0.02, body, (0, 0.0, z)))
    parts.append(M.cylinder(0.022, 0.022, 0.16, body, seg=10, z0=0.22).transformed(M.translate(0, 0.055, 0)))  # grenade tube
    parts.append(M.cylinder(0.016, 0.016, muzzle - 0.40, body, seg=8, z0=0.40))                       # barrel
    glow = mat(mats, 'glow', '#7ad8e8', spec=0.0)
    for z in (0.42, 0.47, 0.52):
        parts.append(M.cylinder(0.036, 0.036, 0.026, glow, seg=12, z0=z))                             # the coil, lit
    parts.append(M.lathe([(muzzle - 0.07, 0.022), (muzzle - 0.02, 0.034), (muzzle, 0.036)], body, seg=12))  # emitter
    parts.append(M.cylinder(0.02, 0.02, 0.004, mat(mats, 'lens', '#5aa8b8', spec=0.9, gloss=40), seg=10, z0=muzzle))
    return M.Mesh.concat(parts)


def power_pack(mats):
    """The armour's back unit: an armoured box in two layers, two round
    cooling vents with grilles, a status light strip, an antenna mast on the
    left and cables into the back plate."""
    d = mat(mats, 'armor_dark', '#3a3d45', spec=0.45, gloss=22, metal=True)
    pl = mat(mats, 'plate', '#3c4aa0', spec=0.45, gloss=22)
    v = mat(mats, 'vent', '#141518', spec=0.2)
    glow = mat(mats, 'glow', '#60e4ff', spec=0.0)
    parts = [M.rounded_box(0.36, 0.14, 0.34, d, (0, -0.25, 0.14), r=0.05, seg=8, p=3.5)]
    parts.append(M.rounded_box(0.28, 0.05, 0.24, pl, (0, -0.33, 0.12), r=0.02, seg=6, p=4))
    for sx in (1, -1):
        parts.append(M.cylinder(0.05, 0.055, 0.10, d, seg=12, z0=0.29).transformed(M.translate(sx * 0.11, -0.27, 0)))
        parts.append(M.cylinder(0.038, 0.038, 0.004, v, seg=12, z0=0.39).transformed(M.translate(sx * 0.11, -0.27, 0)))
    parts.append(M.box(0.12, 0.008, 0.02, glow, (0, -0.358, 0.20)))
    parts.append(M.capsule((-0.16, -0.27, 0.30), (-0.17, -0.30, 0.52), 0.012, 0.006, d, seg=5, rings=1))
    for sx in (1, -1):
        parts.append(_bar((sx * 0.17, -0.22, 0.02), (sx * 0.19, -0.14, -0.10), 0.016, v, seg=5))
    return M.Mesh.concat(parts)


# ------------------------------------------------------------ Combat Engineer

def flame_gun(mats, muzzle=0.70):
    """An M2/M9-pattern flame gun: a fat fuel tube, the valve block behind
    with its rear grip, trigger guard and the hose coupling, a second
    valve block with a knob at the front grip, a perforated heat shield,
    the igniter cylinder under the muzzle, a wide bell nozzle and its pilot
    flame."""
    g = G.gunmetal(mats)
    dk = mat(mats, 'gun_dark', '#1e1f22', spec=0.3, gloss=16)
    o = mat(mats, 'olive', '#56593c', spec=0.1)
    nz = mat(mats, 'nozzle', '#4a4844', spec=0.6, gloss=24, metal=True)
    pilot = mat(mats, 'pilot', '#ff9a34', spec=0.0)
    parts = [M.cylinder(0.024, 0.024, muzzle + 0.14, g, seg=10, z0=-0.14)]                          # fuel tube
    parts.append(M.rounded_box(0.07, 0.09, 0.18, o, (0, 0.01, -0.04), r=0.018, seg=6, p=4))        # rear valve block
    parts.append(M.cylinder(0.034, 0.034, 0.06, dk, seg=10, z0=-0.20))                              # hose coupling
    parts.append(M.box(0.03, 0.09, 0.036, dk, (0, 0.07, -0.01)).transformed(M.rot_x(-14)))          # rear grip
    parts.append(_bar((0, 0.035, 0.03), (0, 0.08, 0.06), 0.006, g, seg=4))                           # trigger guard
    parts.append(M.rounded_box(0.06, 0.07, 0.10, o, (0, 0.01, 0.36), r=0.015, seg=6, p=4))         # front valve block
    parts.append(M.cylinder(0.022, 0.022, 0.03, dk, seg=8, z0=0.0).transformed(
        M.translate(0.04, 0.0, 0.36) @ M.rot_y(90)))                                                 # its knob
    parts.append(M.box(0.03, 0.09, 0.036, dk, (0, 0.07, 0.38)))                                     # front grip
    parts.append(M.cylinder(0.036, 0.036, 0.18, o, seg=12, z0=0.42))                                # heat shield
    for z in (0.45, 0.51, 0.57):
        parts.append(M.cylinder(0.038, 0.038, 0.012, dk, seg=12, z0=z))
    parts.append(M.cylinder(0.024, 0.024, 0.12, g, seg=8, z0=muzzle - 0.16).transformed(M.translate(0, 0.048, 0)))
    parts.append(M.lathe([(muzzle - 0.10, 0.03), (muzzle - 0.04, 0.05), (muzzle, 0.062)], nz, seg=14))   # bell
    parts.append(M.ellipsoid(0.024, 0.03, 0.045, pilot, (0, 0.045, muzzle + 0.03), seg=8))          # pilot flame
    return M.Mesh.concat(parts)


def tank_pack(mats):
    """Two fuel tanks and the pressure bottle between them on a back frame
    (chest space), bands round the tanks, caps and a valve on top, the
    shovel and the pick-mattock strapped on behind them with their heads up
    over the shoulders, and the hose from the left tank's foot round the
    right hip to the gun."""
    t = mat(mats, 'tank', '#5e6248', spec=0.35, gloss=18, metal=True)
    td = mat(mats, 'tank_dark', '#3e4230', spec=0.3, gloss=16, metal=True)
    g = G.gunmetal(mats)
    st = _strap(mats, 'strap', '#3a3628')
    w = mat(mats, 'haft', '#7a5634', spec=0.08)
    blade = mat(mats, 'tool_steel', '#b4b6b8', spec=0.7, gloss=26, metal=True)
    band = mat(mats, 'pack_band', '#c8c8c8')
    hose = mat(mats, 'hose', '#1e1e1c', spec=0.2)
    yb = -0.27
    parts = []
    # the frame: two rails and two cross bars
    for sx in (1, -1):
        parts.append(M.box(0.025, 0.025, 0.56, g, (sx * 0.14, yb + 0.07, 0.04)))
    for z in (-0.18, 0.24):
        parts.append(M.box(0.30, 0.025, 0.03, g, (0, yb + 0.07, z)))
    for sx in (1, -1):
        x = sx * 0.10
        parts.append(M.capsule((x, yb, -0.18), (x, yb, 0.33), 0.082, 0.082, t, seg=14, rings=3))
        for z in (-0.10, 0.24):
            parts.append(M.cylinder(0.086, 0.086, 0.025, td, seg=14, z0=z).transformed(M.translate(x, yb, 0)))
        parts.append(M.cylinder(0.087, 0.087, 0.075, band, seg=14, z0=0.145).transformed(M.translate(x, yb, 0)))
        parts.append(M.cylinder(0.034, 0.034, 0.05, g, seg=10, z0=0.40).transformed(M.translate(x, yb, 0)))
        parts.append(M.cylinder(0.05, 0.05, 0.016, td, seg=12, z0=0.45).transformed(M.translate(x, yb, 0)))   # valve wheel
        parts.append(M.cylinder(0.034, 0.034, 0.05, g, seg=10, z0=-0.30).transformed(M.translate(x, yb, 0)))  # bottom valve
    parts.append(M.sphere(0.065, t, (0, yb - 0.08, 0.06), seg=12))                                  # pressure bottle
    parts.append(M.cylinder(0.02, 0.02, 0.05, g, seg=8, z0=0.12).transformed(M.translate(0, yb - 0.08, 0)))
    parts.append(M.box(0.10, 0.03, 0.03, g, (0, yb - 0.04, -0.22)))                                  # manifold
    # the tools strapped flat on the back of the tanks, below the head: the
    # shovel slanting up to the right, its blade beside the right tank's
    # top; the pick-mattock across the small of the back, its head upright
    # at the left
    ys = yb - 0.105
    S = M.translate(0.0, ys - 0.03, 0.38) @ M.rot_y(180)
    parts.append(M.cylinder(0.017, 0.017, 0.36, w, seg=8, z0=0.0).transformed(S))
    parts.append(M.rounded_box(0.16, 0.024, 0.19, blade, (0, 0, 0.47), r=0.012, seg=6, p=3).transformed(S))
    parts.append(M.box(0.022, 0.026, 0.06, blade, (0, 0, 0.37)).transformed(S))
    parts.append(_bar((-0.05, 0, -0.02), (0.05, 0, -0.02), 0.013, w, seg=5).transformed(S))           # D-grip
    P_ = M.translate(0.25, ys - 0.05, -0.21) @ M.rot_y(-90)
    parts.append(M.cylinder(0.019, 0.021, 0.50, w, seg=8, z0=0.0).transformed(P_))
    parts.append(M.capsule((-0.17, 0, 0.50), (0.0, 0, 0.52), 0.016, 0.024, blade, seg=8, rings=1).transformed(P_))
    parts.append(M.capsule((0.0, 0, 0.52), (0.14, 0, 0.49), 0.024, 0.013, blade, seg=8, rings=1).transformed(P_))
    parts.append(M.box(0.05, 0.034, 0.024, blade, (0.15, 0, 0.485)).transformed(P_))                 # mattock blade
    for z, x in ((0.10, 0.0), (-0.12, 0.0)):
        parts.append(M.box(0.36, 0.03, 0.035, st, (x, ys - 0.012, z)))                               # tool straps
    # straps holding the tools, and the shoulder straps
    for x in (-0.10, 0.10):
        parts.append(_bar((x, -0.16, 0.26), (x * 1.05, 0.02, 0.335), 0.019, st))
        parts.append(_bar((x * 1.05, 0.02, 0.335), (x * 1.2, 0.16, 0.20), 0.019, st))
    # the hose: from under the tanks round the right hip to the front
    pts = [(0.0, yb - 0.02, -0.28), (0.16, yb + 0.04, -0.36), (0.26, -0.08, -0.40), (0.28, 0.08, -0.38),
           (0.24, 0.16, -0.30)]
    for a, b in zip(pts[:-1], pts[1:]):
        parts.append(_bar(a, b, 0.026, hose, seg=8))
    return M.Mesh.concat(parts)


def satchel(mats):
    """A demolition satchel (pelvis space) hanging at the left hip from a
    strap over the right shoulder: a canvas bag with its flap and two
    buckles, the coiled fuse and the igniter on top."""
    c = mat(mats, 'satchel', '#6e6644', spec=0.04)
    cd = mat(mats, 'satchel_dark', '#4e4830', spec=0.04)
    bk = mat(mats, 'buckle', '#2a2a26', spec=0.3, gloss=16)
    fz = mat(mats, 'fuse', '#c8a040', spec=0.1)
    x, y = -0.26, 0.04
    parts = [M.rounded_box(0.08, 0.20, 0.16, c, (x, y, -0.06), r=0.025, seg=8, p=3)]
    parts.append(M.rounded_box(0.086, 0.206, 0.07, cd, (x, y, 0.0), r=0.015, seg=6, p=3))
    for dy in (-0.05, 0.05):
        parts.append(M.box(0.09, 0.016, 0.03, bk, (x, y + dy, -0.04)))
    parts.append(M.capsule((x, y + 0.06, 0.04), (x + 0.02, y + 0.10, 0.06), 0.01, 0.01, fz, seg=5, rings=1))
    for k in range(3):
        parts.append(M.cylinder(0.035 - 0.006 * k, 0.035 - 0.006 * k, 0.008, fz, seg=10, z0=0.04 + 0.009 * k)
                     .transformed(M.translate(x + 0.005, y - 0.04, 0)))
    return M.Mesh.concat(parts)


def wire_cutters(mats):
    """Wire cutters in a pouch on the right of the belt (pelvis space)."""
    cd = mat(mats, 'satchel_dark', '#4e4830', spec=0.04)
    g = G.gunmetal(mats)
    rd = mat(mats, 'grip_red', '#7a2a22', spec=0.1)
    x, y = 0.24, -0.05
    parts = [M.rounded_box(0.05, 0.07, 0.14, cd, (x, y, -0.04), r=0.012, seg=6, p=4)]
    parts.append(_bar((x, y - 0.012, 0.02), (x, y - 0.015, 0.12), 0.011, rd, seg=5))
    parts.append(_bar((x, y + 0.012, 0.02), (x, y + 0.015, 0.12), 0.011, rd, seg=5))
    parts.append(M.box(0.02, 0.03, 0.04, g, (x, y, 0.145)))
    return M.Mesh.concat(parts)


def respirator(mats, mm, can):
    """A half-mask respirator over the mouth and nose (head space, before the
    head scale), two filter cans at its cheeks and the head strap."""
    parts = [M.ellipsoid(0.065, 0.04, 0.05, mm, (0, 0.10, 0.06), seg=12)]
    for sx in (1, -1):
        parts.append(M.cylinder(0.024, 0.024, 0.022, can, seg=10, z0=0.0).transformed(
            M.translate(sx * 0.052, 0.118, 0.045) @ M.rot_y(70 * sx) @ M.rot_x(-15)))
        parts.append(_bar((sx * 0.07, 0.07, 0.07), (sx * 0.10, -0.01, 0.10), 0.01, mm, seg=4))
    return M.Mesh.concat(parts)


def goggles(mats, strap, lens, z=0.185, r=0.138):
    """Goggles pushed up on a helmet's brow (head space): the strap round
    the helmet and two lenses in their frames on the front."""
    parts = [M.cylinder(r + 0.004, r + 0.004, 0.022, strap, seg=20, z0=z - 0.011).transformed(M.scale(1, 1.08, 1))]
    for sx in (1, -1):
        parts.append(M.cylinder(0.032, 0.03, 0.02, strap, seg=10, z0=0.0).transformed(
            M.translate(sx * 0.045, r * 1.08 - 0.006, z) @ M.rot_x(-90)))
        parts.append(M.cylinder(0.026, 0.026, 0.006, lens, seg=10, z0=0.018).transformed(
            M.translate(sx * 0.045, r * 1.08 - 0.006, z) @ M.rot_x(-90)))
    return M.Mesh.concat(parts)


def entrenching_tool(mats):
    """A folding entrenching tool on the right hip (pelvis space), the bright
    blade down in its open carrier, the short haft up."""
    w = mat(mats, 'haft', '#7a5634', spec=0.08)
    blade = mat(mats, 'tool_steel', '#b4b6b8', spec=0.7, gloss=26, metal=True)
    c = mat(mats, 'satchel_dark', '#4e4830', spec=0.04)
    x, y = 0.25, -0.02
    parts = [M.rounded_box(0.025, 0.15, 0.17, blade, (x, y, -0.16), r=0.012, seg=6, p=3)]
    parts.append(M.box(0.03, 0.03, 0.05, blade, (x, y, -0.06)))
    parts.append(M.cylinder(0.018, 0.018, 0.22, w, seg=8, z0=-0.04).transformed(M.translate(x, y, 0)))
    parts.append(M.box(0.03, 0.09, 0.03, w, (x, y, 0.19)))
    parts.append(M.box(0.035, 0.12, 0.03, c, (x, y, -0.02)))
    return M.Mesh.concat(parts)
