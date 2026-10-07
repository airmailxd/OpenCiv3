"""Gear for the gunpowder and modern foot units (catalog/foot_modern.py):
firearms, the musket rest, launchers, packs, parachutes and work props.

Held props follow models/props.py: the grip (the right hand) at the origin,
the long axis +z (the muzzle end). A firearm's top (sights) faces -y, so with
the usual pitch of -90 (the barrel level, pointing forward) the sights are up.

GUNS describes each firearm for the animations: where the muzzle is (z), where
the left hand holds it (`support`: (y, z) in prop space) and the butt (z).
"""
import math

import numpy as np

import mesh as M
from models.props import mat, steel, wood, leather


def gunmetal(mats):
    return mat(mats, 'gunmetal', '#36373a', spec=0.55, gloss=22, metal=True)


def stock(mats):
    return mat(mats, 'stock', '#6a4426', spec=0.12, gloss=14)


def olive(mats):
    return mat(mats, 'olive', '#5a5e3e', spec=0.08)


# muzzle z, left-hand support (y, z), butt z (the stock's end, behind the grip)
GUNS = {
    'matchlock': {'muzzle': 1.08, 'support': (0.07, 0.46), 'butt': -0.36},
    'musket': {'muzzle': 1.02, 'support': (0.02, 0.36), 'butt': -0.36},
    'rifle_bayonet': {'muzzle': 0.98, 'support': (0.02, 0.36), 'butt': -0.34},
    'garand': {'muzzle': 0.86, 'support': (0.02, 0.32), 'butt': -0.32},
    'ak': {'muzzle': 0.64, 'support': (0.03, 0.28), 'butt': -0.30},
    'm16': {'muzzle': 0.64, 'support': (0.03, 0.28), 'butt': -0.30},
    'carbine': {'muzzle': 0.54, 'support': (0.03, 0.25), 'butt': -0.26},
    'thompson': {'muzzle': 0.52, 'support': (0.06, 0.22), 'butt': -0.30},
}


def _barrel(mats, z0, z1, r=0.014, m=None):
    return M.cylinder(r, r * 0.92, z1 - z0, gunmetal(mats) if m is None else m, seg=8, z0=z0)


def _stock_wood(mats, butt, z_front, depth=0.075, wrist=0.032, m=None):
    """A long rifle stock from the butt (z = butt) to the fore-end (z_front),
    the butt plate deep (toward +y, the bottom), narrowing at the wrist."""
    m = stock(mats) if m is None else m
    prof = [(butt, 0.0, depth), (butt + 0.05, 0.0, depth * 0.95), (-0.05, 0.004, wrist * 1.4),
            (0.02, 0.0, wrist * 1.15), (0.12, 0.004, 0.034), (z_front, 0.006, 0.026)]
    parts = []
    for (za, ya, ha), (zb, yb, hb) in zip(prof[:-1], prof[1:]):
        # a slab of half-height h (in y) and half-width 0.022 between za and zb
        pts = []
        for z, y, h in ((za, ya, ha), (zb, yb, hb)):
            pts.append((z, y - 0.012, y + h - 0.012))
        (z0, b0, t0), (z1, b1, t1) = pts
        poly = [(b0, z0), (b1, z1), (t1, z1), (t0, z0)]
        slab = M.extrude([(x, z) for x, z in poly], 0.042, m)
        # extrude: shape in the x-z plane, thickness along y -> turn x into y
        parts.append(slab.transformed(M.rot_z(90)))
    return M.Mesh.concat(parts)


def matchlock(mats, muzzle=1.08):
    """A long matchlock musket: heavy wooden stock with a straight butt, iron barrel."""
    s = stock(mats)
    parts = [_stock_wood(mats, -0.36, 0.86, depth=0.085, m=s)]
    parts.append(_barrel(mats, 0.0, muzzle, 0.017))
    parts.append(M.cylinder(0.021, 0.021, 0.03, gunmetal(mats), seg=8, z0=muzzle - 0.03))
    # the lock and the serpentine (match holder) on the right side
    parts.append(M.box(0.02, 0.03, 0.09, gunmetal(mats), (0.028, 0.0, 0.03)))
    parts.append(M.capsule((0.03, 0.0, -0.01), (0.03, -0.05, 0.04), 0.007, 0.006, gunmetal(mats), seg=5, rings=1))
    return M.Mesh.concat(parts)


def musket(mats, muzzle=1.02):
    """A 17th-century musket with a flaring, polished muzzle (the Musketeer's
    bright muzzle in the original) and brass fittings."""
    s = stock(mats)
    parts = [_stock_wood(mats, -0.36, 0.84, depth=0.08, m=s)]
    parts.append(_barrel(mats, 0.0, muzzle - 0.1, 0.016, steel(mats)))
    parts.append(M.lathe([(muzzle - 0.12, 0.017), (muzzle - 0.04, 0.026), (muzzle, 0.042)], steel(mats), seg=10))
    b = mat(mats, 'bronze', '#c09040', spec=0.8, gloss=24, metal=True)
    for z in (0.3, 0.6):
        parts.append(M.cylinder(0.024, 0.024, 0.025, b, seg=8, z0=z))
    parts.append(M.box(0.02, 0.03, 0.09, b, (0.028, 0.0, 0.03)))
    return M.Mesh.concat(parts)


def rifle_bayonet(mats, muzzle=0.98, bayonet=0.36):
    """A 19th-century rifle-musket: long wooden stock to near the muzzle, steel
    barrel bands, a long socket bayonet."""
    s = stock(mats)
    st = steel(mats)
    parts = [_stock_wood(mats, -0.34, 0.86, depth=0.078, m=s)]
    parts.append(_barrel(mats, 0.0, muzzle, 0.014, st))
    for z in (0.35, 0.6, 0.84):
        parts.append(M.cylinder(0.026, 0.026, 0.02, st, seg=8, z0=z).transformed(M.translate(0, 0.008, 0)))
    parts.append(M.box(0.02, 0.03, 0.08, gunmetal(mats), (0.026, 0.0, 0.03)))
    if bayonet:
        parts.append(M.cylinder(0.018, 0.018, 0.05, st, seg=8, z0=muzzle - 0.05).transformed(M.translate(0, 0.022, 0)))
        bl = M.blade(bayonet, 0.022, 0.008, st, tip=0.12, z0=muzzle - 0.02)
        parts.append(bl.transformed(M.translate(0, 0.03, 0)))
    return M.Mesh.concat(parts)


def garand(mats, muzzle=0.86):
    """A WWII battle rifle: wooden stock and handguard, dark steel."""
    s = stock(mats)
    g = gunmetal(mats)
    parts = [_stock_wood(mats, -0.32, 0.66, depth=0.08, m=s)]
    parts.append(_barrel(mats, 0.0, muzzle, 0.014, g))
    parts.append(M.box(0.036, 0.04, 0.2, g, (0, -0.004, 0.06)))               # receiver
    parts.append(M.box(0.03, 0.025, 0.36, s, (0, -0.02, 0.42)))                # upper handguard
    parts.append(M.box(0.01, 0.03, 0.04, g, (0, -0.03, muzzle - 0.04)))       # front sight
    return M.Mesh.concat(parts)


def ak(mats, muzzle=0.64):
    """An assault rifle with wooden furniture and a curved magazine."""
    s = stock(mats)
    g = gunmetal(mats)
    parts = [_stock_wood(mats, -0.30, -0.02, depth=0.07, wrist=0.03, m=s)]
    parts.append(M.box(0.04, 0.055, 0.28, g, (0, 0.0, 0.08)))                  # receiver
    parts.append(M.box(0.042, 0.05, 0.16, s, (0, 0.005, 0.30)))                # handguard
    parts.append(_barrel(mats, 0.2, muzzle, 0.012, g))
    parts.append(M.box(0.008, 0.03, 0.03, g, (0, -0.03, muzzle - 0.05)))
    # curved magazine below (+y)
    mag = []
    for i in range(4):
        a = math.radians(8 + 9 * i)
        y0 = 0.03 + 0.045 * i
        mag.append(M.box(0.03, 0.05, 0.05, g, (0, y0, 0.15 + 0.045 * math.sin(a) * i)))
    parts += mag
    # pistol grip
    parts.append(M.box(0.03, 0.08, 0.035, g, (0, 0.05, -0.005)).transformed(M.rot_x(-15)))
    return M.Mesh.concat(parts)


def m16(mats, muzzle=0.64):
    """A black modern rifle with a carry handle, a straight magazine and a
    fixed stock."""
    g = mat(mats, 'polymer', '#24252a', spec=0.25, gloss=16)
    gm = gunmetal(mats)
    parts = [M.box(0.04, 0.07, 0.26, g, (0, 0.012, -0.17))]                    # stock
    parts.append(M.box(0.042, 0.06, 0.24, gm, (0, 0.0, 0.08)))                 # receiver
    parts.append(M.box(0.016, 0.02, 0.12, gm, (0, -0.045, 0.06)))              # carry handle
    parts.append(M.cylinder(0.024, 0.024, 0.22, g, seg=8, z0=0.2))              # handguard
    parts.append(_barrel(mats, 0.4, muzzle, 0.009, gm))
    parts.append(M.box(0.008, 0.04, 0.02, gm, (0, -0.03, muzzle - 0.08)))      # front sight
    parts.append(M.box(0.03, 0.10, 0.04, gm, (0, 0.07, 0.12)))                 # magazine
    parts.append(M.box(0.03, 0.07, 0.035, g, (0, 0.05, -0.01)).transformed(M.rot_x(-15)))
    return M.Mesh.concat(parts)


def carbine(mats, muzzle=0.54):
    """A compact black carbine with a folding-style stock."""
    g = mat(mats, 'polymer', '#24252a', spec=0.25, gloss=16)
    gm = gunmetal(mats)
    parts = [M.box(0.03, 0.06, 0.2, g, (0, 0.01, -0.14))]
    parts.append(M.box(0.042, 0.06, 0.22, gm, (0, 0.0, 0.07)))
    parts.append(M.cylinder(0.024, 0.024, 0.16, g, seg=8, z0=0.18))
    parts.append(_barrel(mats, 0.32, muzzle, 0.009, gm))
    parts.append(M.box(0.03, 0.09, 0.04, gm, (0, 0.065, 0.11)))
    parts.append(M.box(0.03, 0.07, 0.035, g, (0, 0.05, -0.01)).transformed(M.rot_x(-15)))
    return M.Mesh.concat(parts)


def thompson(mats, muzzle=0.52):
    """A WWII submachine gun: wooden stock, foregrip and pistol grip, finned barrel."""
    s = stock(mats)
    g = gunmetal(mats)
    parts = [_stock_wood(mats, -0.30, -0.04, depth=0.07, wrist=0.03, m=s)]
    parts.append(M.box(0.045, 0.06, 0.24, g, (0, 0.0, 0.08)))
    parts.append(M.cylinder(0.022, 0.02, 0.2, g, seg=8, z0=0.2))
    parts.append(_barrel(mats, 0.38, muzzle, 0.012, g))
    parts.append(M.box(0.03, 0.12, 0.04, g, (0, 0.08, 0.12)))                  # stick magazine
    parts.append(M.box(0.03, 0.08, 0.035, s, (0, 0.05, -0.005)).transformed(M.rot_x(-15)))
    parts.append(M.box(0.03, 0.07, 0.035, s, (0, 0.05, 0.24)))                 # foregrip
    return M.Mesh.concat(parts)


def musket_rest(mats, length=1.17, grip=1.10):
    """The forked rest a matchlock musket is fired from (held by the left
    hand `grip` meters above its foot; the fork on top)."""
    w = mat(mats, 'rest', '#2e2620', spec=0.08)
    st = gunmetal(mats)
    z0, z1 = -grip, length - grip
    parts = [M.cylinder(0.024, 0.02, length - 0.04, w, seg=7, z0=z0)]
    parts.append(M.cylinder(0.005, 0.015, 0.06, st, seg=6, z0=z0 - 0.05))
    for sx in (1, -1):
        parts.append(M.capsule((0, 0, z1 - 0.04), (sx * 0.055, 0, z1 + 0.06), 0.01, 0.008, st, seg=5, rings=1))
    return M.Mesh.concat(parts)


# ------------------------------------------------------------ worn gear

def field_pack(mats, size=1.0, m='pack', strap='strap'):
    """A soldier's pack (chest bone space) with a rolled blanket on top."""
    s = size
    pk = mat(mats, m, '#6e6650', spec=0.04)
    st = mat(mats, strap, '#5a5240', spec=0.05)
    parts = [M.rounded_box(0.30 * s, 0.15 * s, 0.30 * s, pk, (0, -0.22, 0.12), r=0.05, seg=12, p=3.5)]
    parts.append(M.cylinder(0.05 * s, 0.05 * s, 0.34 * s, pk, seg=10).transformed(
        M.translate(-0.17 * s, -0.2, 0.30) @ M.rot_y(90)))
    for x in (-0.1, 0.1):
        parts.append(M.capsule((x, -0.14, 0.26), (x, 0.08, 0.28), 0.017, 0.017, st, seg=5, rings=1))
        parts.append(M.capsule((x, 0.12, 0.25), (x * 1.1, 0.135, 0.0), 0.016, 0.016, st, seg=5, rings=1))
    return M.Mesh.concat(parts)


def para_pack(mats, size=1.0, m='pack'):
    """A big jump pack (chest bone space), rounded, with side pouches."""
    s = size
    pk = mat(mats, m, '#5a5e3e', spec=0.04)
    st = mat(mats, 'strap', '#3e3e30', spec=0.05)
    parts = [M.rounded_box(0.36 * s, 0.20 * s, 0.42 * s, pk, (0, -0.25, 0.08), r=0.08, seg=12, p=3.0)]
    for sx in (1, -1):
        parts.append(M.rounded_box(0.08, 0.14, 0.2, pk, (sx * 0.2 * s, -0.22, 0.0), r=0.03, seg=8, p=3))
    for x in (-0.11, 0.11):
        parts.append(M.capsule((x, -0.14, 0.27), (x, 0.08, 0.29), 0.018, 0.018, st, seg=5, rings=1))
        parts.append(M.capsule((x, 0.12, 0.26), (x * 1.1, 0.14, -0.02), 0.017, 0.017, st, seg=5, rings=1))
    return M.Mesh.concat(parts)


def hiker_pack(mats, size=1.0):
    """The modern settler's tall framed backpack (chest bone space), rising
    above the head, a bedroll on top and a pocket at the back."""
    s = size
    pk = mat(mats, 'pack', '#6a4a2e', spec=0.05)
    roll = mat(mats, 'bedroll', '#4a3a2a', spec=0.03)
    st = mat(mats, 'strap', '#3a2a1c', spec=0.05)
    parts = [M.rounded_box(0.36 * s, 0.26 * s, 0.64 * s, pk, (0, -0.29, 0.19), r=0.09, seg=12, p=3.0)]
    parts.append(M.rounded_box(0.26 * s, 0.08, 0.26 * s, pk, (0, -0.45, 0.10), r=0.03, seg=8, p=3))
    parts.append(M.cylinder(0.085 * s, 0.085 * s, 0.44 * s, roll, seg=12).transformed(
        M.translate(-0.22 * s, -0.28, 0.56) @ M.rot_y(90)))
    for x in (-0.1, 0.1):
        parts.append(M.capsule((x, -0.15, 0.3), (x, 0.08, 0.29), 0.018, 0.018, st, seg=5, rings=1))
        parts.append(M.capsule((x, 0.12, 0.26), (x * 1.1, 0.135, -0.02), 0.017, 0.017, st, seg=5, rings=1))
    return M.Mesh.concat(parts)


def ammo_box(mats, m='olive'):
    o = olive(mats) if m == 'olive' else mat(mats, m, '#4e5236')
    g = gunmetal(mats)
    return M.Mesh.concat([M.rounded_box(0.28, 0.16, 0.2, o, (0, 0, 0.1), r=0.015, seg=8, p=4),
                          M.box(0.1, 0.02, 0.025, g, (0, 0, 0.21))])


def brick_pile(mats):
    """A low stack of bricks being laid (the worker's fortress)."""
    b = mat(mats, 'brick', '#9a4a32', spec=0.04)
    b2 = mat(mats, 'brick2', '#7e3a28', spec=0.04)
    parts = []
    for row in range(3):
        n = 4 - row
        for i in range(n):
            x = (i - (n - 1) / 2) * 0.24
            parts.append(M.rounded_box(0.22, 0.11, 0.075, b if (i + row) % 2 else b2,
                                       (x, 0.0, 0.04 + row * 0.08), r=0.01, seg=6, p=4))
            parts.append(M.rounded_box(0.22, 0.11, 0.075, b2 if (i + row) % 2 else b,
                                       (x + 0.05, 0.12, 0.04 + row * 0.08), r=0.01, seg=6, p=4))
    return M.Mesh.concat(parts)


def trowel(mats, length=0.25):
    st = steel(mats)
    return M.Mesh.concat([M.cylinder(0.018, 0.018, 0.1, wood(mats), seg=6, z0=-0.06),
                          M.extrude([(-0.05, 0.08), (0.05, 0.08), (0.0, 0.22)], 0.008, st)])


def sledgehammer(mats, length=0.8):
    return M.Mesh.concat([M.cylinder(0.018, 0.016, length + 0.15, wood(mats), seg=7, z0=-0.15),
                          M.rounded_box(0.08, 0.2, 0.08, gunmetal(mats), (0, 0, length), r=0.012, seg=8)])


# ------------------------------------------------------------ the TOW launcher

def tow_launcher(mats):
    """The TOW tube and sight unit, in launcher space: the tube along +y
    (forward), its pivot at the origin (on top of the tripod)."""
    o = olive(mats)
    od = mat(mats, 'olive_dark', '#3e4230', spec=0.1)
    g = gunmetal(mats)
    parts = [M.cylinder(0.085, 0.085, 1.30, o, seg=14, z0=-0.55).transformed(M.rot_x(-90) @ M.translate(0, 0, 0))]
    parts.append(M.cylinder(0.10, 0.10, 0.08, od, seg=14, z0=0.68).transformed(M.rot_x(-90)))
    parts.append(M.cylinder(0.095, 0.095, 0.06, od, seg=14, z0=-0.58).transformed(M.rot_x(-90)))
    # sight and guidance box below/behind
    parts.append(M.rounded_box(0.16, 0.30, 0.16, od, (0.0, -0.20, -0.12), r=0.02, seg=8, p=4))
    parts.append(M.cylinder(0.035, 0.035, 0.14, g, seg=8, z0=0).transformed(M.translate(-0.12, -0.18, -0.08) @ M.rot_x(-90)))
    parts.append(M.box(0.04, 0.05, 0.05, g, (0.0, 0.0, -0.08)))
    return M.Mesh.concat(parts)


def tow_tripod(mats, h=0.55):
    """The launcher's tripod (feet on the ground, the head at height h)."""
    o = mat(mats, 'olive_dark', '#3e4230', spec=0.1)
    g = gunmetal(mats)
    parts = [M.cylinder(0.04, 0.035, 0.12, o, seg=8, z0=h - 0.1)]
    for a in (90, 210, 330):
        x, y = 0.55 * math.cos(math.radians(a)), 0.55 * math.sin(math.radians(a))
        parts.append(M.capsule((0, 0, h - 0.06), (x, y, 0.02), 0.022, 0.018, g, seg=6, rings=1))
        parts.append(M.box(0.06, 0.06, 0.015, g, (x, y, 0.008)))
    return M.Mesh.concat(parts)


def tow_missile(mats):
    """The missile in flight (along +y)."""
    o = olive(mats)
    return M.Mesh.concat([M.cylinder(0.05, 0.05, 0.6, o, seg=10, z0=-0.3).transformed(M.rot_x(-90)),
                          M.cone(0.05, 0.12, o, seg=10, z0=0.3).transformed(M.rot_x(-90))])


# ------------------------------------------------------------ the parachute

def parachute(mats, radius=1.6, height=0.95, open_=1.0, lines_to=(0, 0, 0), collapse=0.0, m='canopy'):
    """A round canopy (in canopy space: its skirt ring at z = 0, the crown up),
    gored like a real parachute, with suspension lines down to `lines_to`.
    open_ (0..1) scales a half-open canopy; collapse (0..1) folds it down and
    sideways as it deflates on the ground."""
    c = mat(mats, m, '#ececea', spec=0.12, gloss=10)
    cd = mat(mats, m + '_vent', '#7a7a76', spec=0.02)
    ln = mat(mats, 'lines', '#c8c8c0', spec=0.0)
    r = radius * (0.35 + 0.65 * open_)
    h = height * (1.15 - 0.15 * open_) * (1 - 0.75 * collapse)
    gores = 16
    seg = gores * 4
    rings = []
    k = 9
    for i in range(k + 1):
        u = i / k                     # 0 skirt .. 1 crown
        z = h * math.sin(u * math.pi / 2) ** 0.8
        rr = r * math.cos(u * math.pi / 2 * 0.92) + 1e-3
        rr *= 1 + 0.25 * collapse * (1 - u)
        rings.append((z, rr, u))
    # gored surface: the rim bulges between the lines
    ang = np.linspace(0, 2 * math.pi, seg + 1)[:-1]
    bul = 1 + 0.045 * np.abs(np.sin(ang * gores / 2))
    pts = []
    vent = 0.12
    rings = [rg for rg in rings if rg[2] <= 1 - vent] + [(h * math.sin((1 - vent) * math.pi / 2) ** 0.8,
                                                         r * math.cos((1 - vent) * math.pi / 2 * 0.92), 1 - vent)]
    for z, rr, u in rings:
        sag = collapse * 0.25 * radius * (1 - u)
        pts.append(np.stack([rr * bul * np.cos(ang) + sag, rr * bul * np.sin(ang), np.full(seg, z)], -1))
    v = np.concatenate(pts).astype(np.float32)
    faces = M.grid_faces(len(rings) - 1, seg, closed_u=True)
    shell = M.from_indexed(v, faces, c, smooth=True)
    # double-sided: the renderer flips normals toward the camera, fine
    parts = [shell]
    zt, rt, _ = rings[-1]
    parts.append(M.cylinder(rt, rt, 0.01, cd, seg=16, z0=zt - 0.005))
    if collapse < 0.6:
        lt = np.asarray(lines_to, np.float32)
        for i in range(gores):
            a = 2 * math.pi * i / gores
            p0 = (r * math.cos(a), r * math.sin(a), 0.0)
            parts.append(M.capsule(p0, lt, 0.008, 0.008, ln, seg=4, rings=1))
    return M.Mesh.concat(parts)
