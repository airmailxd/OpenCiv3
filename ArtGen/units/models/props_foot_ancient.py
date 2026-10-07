"""Hand-held props for the ancient and medieval foot units (catalog/foot_ancient.py).

Same conventions as models/props.py: grip at the origin, long axis +z (the
business end up), a blade's edge toward +y; shields are discs in the x-z
plane facing -y. Materials come from props.mat(), so catalogs can recolor them.
"""
import math

import numpy as np

import mesh as M
from models.props import mat, wood, steel, leather, bronze, haft, rope, bow, spear


def _ring(mats, rx, rz, r, m, k=22, y=0.0):
    out = []
    for i in range(k):
        a0, a1 = 2 * math.pi * i / k, 2 * math.pi * (i + 1) / k
        out.append(M.capsule((rx * math.cos(a0), y, rz * math.sin(a0)), (rx * math.cos(a1), y, rz * math.sin(a1)),
                             r, r, m, seg=5, rings=1))
    return out


def _on_dome(pts2d, w, h, dome, depth=0.025, lift=0.006):
    """Maps 2D points (x, z) onto the front (-y) of a domed disc."""
    out = []
    for x, z in pts2d:
        s2 = (x / (w / 2)) ** 2 + (z / (h / 2)) ** 2
        out.append((x, -depth / 2 - dome * max(0.0, 1 - s2) - lift, z))
    return out


def _decal(mats, poly, m, w, h, dome, lift=0.006):
    """A flat polygon (x, z) laid on a domed shield face (fan from its centroid)."""
    p = np.asarray(poly, np.float32)
    c = p.mean(0)
    tris, norms = [], []
    P3 = _on_dome([tuple(v) for v in p], w, h, dome, lift=lift)
    C3 = _on_dome([tuple(c)], w, h, dome, lift=lift)[0]
    for i in range(len(p)):
        a, b = P3[i], P3[(i + 1) % len(p)]
        tris.append([C3, b, a])
        norms.append([(0, -1, 0)] * 3)
    return M.Mesh(np.array(tris, np.float32), np.array(norms, np.float32), np.full(len(tris), m, np.int32))


# ------------------------------------------------------------ shields

def aspis(mats, r=0.56, dome=0.11):
    """The hoplite's great round bronze shield: a broad (civ-colored) rim and a
    painted device in the middle."""
    face = mat(mats, 'shield_face', '#c8a050', spec=0.7, gloss=22, metal=True)
    rim = mat(mats, 'shield_rim', '#ffffff', tint=True)
    em = mat(mats, 'shield_emblem', '#8a2a1a', spec=0.1)
    parts = [M.disc(r, r, 0.03, face, seg=28, dome=dome)]
    # the rim: a flat band around the edge
    parts += _ring(mats, r * 0.96, r * 0.96, 0.034, rim, k=28, y=-0.02)
    # the device, like the original's: darker embossed bands across the middle
    # and a red lower segment
    dark = mat(mats, 'shield_dark', '#6a4818', spec=0.3, gloss=14)
    parts.append(_decal(mats, [(-0.30, 0.10), (0.30, 0.10), (0.24, 0.20), (-0.24, 0.20)], dark, 2 * r, 2 * r, dome))
    parts.append(_decal(mats, [(-0.36, 0.06), (0.36, 0.06), (0.36, -0.04), (-0.36, -0.04)], dark, 2 * r, 2 * r, dome))
    # a red band low across the field
    parts.append(_decal(mats, [(-0.30, -0.16), (0.30, -0.16), (0.26, -0.26), (-0.26, -0.26)], em, 2 * r, 2 * r, dome))
    return M.Mesh.concat(parts)


def round_painted(mats, r=0.34, dome=0.05, rays=8, boss=False):
    """A round shield, light face with a civ-colored star/sunburst device and a
    metal rim (Libyan mercenary)."""
    face = mat(mats, 'shield_face', '#e8e4d8', spec=0.08)
    rim = mat(mats, 'shield_rim', '#c8a050', spec=0.7, gloss=22, metal=True)
    em = mat(mats, 'shield_emblem', '#ffffff', tint=True)
    parts = [M.disc(r, r, 0.03, face, seg=26, dome=dome)]
    parts += _ring(mats, r, r, 0.02, rim, k=26)
    pts = []
    for i in range(rays * 2):
        a = math.pi * i / rays
        rr = r * (0.78 if i % 2 == 0 else 0.26)
        pts.append((rr * math.cos(a), rr * math.sin(a)))
    parts.append(_decal(mats, pts, em, 2 * r, 2 * r, dome))
    if boss:
        parts.append(M.ellipsoid(0.06, 0.04, 0.06, bronze(mats), (0, -dome - 0.03, 0), seg=10))
    return M.Mesh.concat(parts)


def wooden_round(mats, r=0.27, dome=0.03):
    """A plain wooden round shield with a steel rim and boss (medieval infantry)."""
    face = mat(mats, 'shield_face', '#b08850', spec=0.06)
    rim = mat(mats, 'shield_rim', '#8a8a8e', spec=0.7, gloss=22, metal=True)
    parts = [M.disc(r, r, 0.03, face, seg=24, dome=dome)]
    parts += _ring(mats, r, r, 0.016, rim, k=22)
    # planks
    pl = mat(mats, 'shield_plank', '#8a6638', spec=0.04)
    for x in (-0.09, 0.0, 0.09):
        h = math.sqrt(max(r * r - x * x, 0)) * 0.95
        parts.append(_decal(mats, [(x - 0.004, -h), (x + 0.004, -h), (x + 0.004, h), (x - 0.004, h)], pl, 2 * r, 2 * r, dome))
    parts.append(M.ellipsoid(0.055, 0.04, 0.055, steel(mats), (0, -dome - 0.03, 0), seg=10))
    return M.Mesh.concat(parts)


def scutum(mats, w=0.62, h=1.02, curve=0.16):
    """The legionary's tall curved rectangular shield: civ-colored face with a
    metal edge, boss and a winged device."""
    face = mat(mats, 'shield_face', '#ffffff', tint=True)
    rim = mat(mats, 'shield_rim', '#8a8a8e', spec=0.7, gloss=22, metal=True)
    em = mat(mats, 'shield_emblem', '#c8c4b8', spec=0.1)
    n = 9
    rings = []
    # build as a loft of a curved slab: columns across x, curving back (+y) at the sides
    xs = np.linspace(-w / 2, w / 2, n)

    def ycurve(x):
        return -curve * (1 - (2 * x / w) ** 2) + curve * 0.0

    tris, norms = [], []
    zs = np.linspace(-h / 2, h / 2, 7)
    for a, b in zip(xs[:-1], xs[1:]):
        for z0, z1 in zip(zs[:-1], zs[1:]):
            ya, yb = ycurve(a), ycurve(b)
            na = np.array([2 * curve * 2 * a / w * 2 / w * 0.5, -1, 0]); na /= np.linalg.norm(na)
            nb = np.array([2 * curve * 2 * b / w * 2 / w * 0.5, -1, 0]); nb /= np.linalg.norm(nb)
            q = [(a, ya, z0), (b, yb, z0), (b, yb, z1), (a, ya, z1)]
            tris += [[q[0], q[1], q[2]], [q[0], q[2], q[3]]]
            norms += [[na, nb, nb], [na, nb, na]]
            # back face
            qb = [(x, y + 0.025, z) for x, y, z in q]
            tris += [[qb[0], qb[2], qb[1]], [qb[0], qb[3], qb[2]]]
            norms += [[-na, -nb, -nb], [-na, -na, -nb]]
    parts = [M.Mesh(np.array(tris, np.float32), np.array(norms, np.float32), np.full(len(tris), face, np.int32))]
    # edge
    for x in (-w / 2, w / 2):
        parts.append(M.capsule((x, ycurve(x) + 0.012, -h / 2), (x, ycurve(x) + 0.012, h / 2), 0.016, 0.016, rim, seg=6,
                               rings=1))
    for z in (-h / 2, h / 2):
        for a, b in zip(xs[:-1], xs[1:]):
            parts.append(M.capsule((a, ycurve(a) + 0.012, z), (b, ycurve(b) + 0.012, z), 0.016, 0.016, rim, seg=5, rings=1))
    # the device: wings and lightning bolts (light) around a boss
    for sx in (1, -1):
        for sz in (1, -1):
            pts = [(sx * 0.07, sz * 0.05), (sx * 0.24, sz * 0.30), (sx * 0.17, sz * 0.36), (sx * 0.05, sz * 0.12)]
            P3 = [(x, ycurve(x) - 0.006, z) for x, z in pts]
            c = np.mean(P3, 0)
            for i in range(4):
                a3, b3 = P3[i], P3[(i + 1) % 4]
                pass
            tri = [[c, P3[i], P3[(i + 1) % 4]] for i in range(4)]
            parts.append(M.Mesh(np.array(tri, np.float32), np.array([[(0, -1, 0)] * 3] * 4, np.float32),
                                np.full(4, em, np.int32)))
    parts.append(M.rounded_box(0.06, 0.04, h * 0.94, em, (0, -curve - 0.012, 0), r=0.012, seg=6))
    parts.append(M.ellipsoid(0.08, 0.05, 0.08, steel(mats), (0, -curve - 0.03, 0), seg=10))
    return M.Mesh.concat(parts)


def cowhide(mats, w=0.52, h=1.10, dome=0.05):
    """The Zulu's tall oval cowhide shield: white with dark patches, a stick
    down the middle."""
    face = mat(mats, 'shield_face', '#ece8e0', spec=0.04)
    dark = mat(mats, 'shield_dark', '#2a2622', spec=0.04)
    parts = [M.disc(w / 2, h / 2, 0.025, face, seg=24, dome=dome)]
    rng = np.random.default_rng(7)
    for cx, cz, rx, rz in ((-0.10, 0.28, 0.10, 0.12), (0.12, 0.05, 0.09, 0.14), (-0.06, -0.25, 0.12, 0.10),
                           (0.10, -0.38, 0.06, 0.06), (0.04, 0.42, 0.06, 0.05)):
        pts = []
        for a in np.linspace(0, 2 * math.pi, 11)[:-1]:
            j = 0.75 + 0.35 * rng.random()
            pts.append((cx + rx * j * math.cos(a), cz + rz * j * math.sin(a)))
        parts.append(_decal(mats, pts, dark, w, h, dome))
    parts.append(M.cylinder(0.014, 0.014, h * 1.18, wood(mats), seg=6, z0=-h * 0.59).transformed(
        M.translate(0, -dome - 0.02, 0)))
    # lacing down the middle
    lm = mat(mats, 'shield_dark', '#2a2622')
    for z in np.linspace(-h * 0.38, h * 0.38, 6):
        parts.append(M.box(0.07, 0.012, 0.025, lm, (0, -0.0125 - dome * max(0.0, 1 - (z / (h / 2)) ** 2) - 0.006, z)))
    return M.Mesh.concat(parts)


def chimalli(mats, r=0.25, dome=0.03):
    """An Aztec round shield: civ-colored face, a gold center disc and ring,
    feathers hanging from the bottom."""
    face = mat(mats, 'shield_face', '#ffffff', tint=True)
    gold = mat(mats, 'shield_gold', '#e0b040', spec=0.3, gloss=16)
    parts = [M.disc(r, r, 0.03, face, seg=22, dome=dome)]
    parts += _ring(mats, r * 0.98, r * 0.98, 0.018, gold, k=20)
    parts.append(_decal(mats, [(0.09 * math.cos(a), 0.09 * math.sin(a)) for a in np.linspace(0, 2 * math.pi, 13)[:-1]],
                        gold, 2 * r, 2 * r, dome))
    fm = mat(mats, 'feather', '#ffffff', tint=True)
    for i, x in enumerate(np.linspace(-0.13, 0.13, 5)):
        z0 = -math.sqrt(max(r * r - x * x, 0)) * 0.95
        parts.append(M.ellipsoid(0.025, 0.008, 0.08, fm if i % 2 else gold, (x, 0.0, z0 - 0.07), seg=6))
    return M.Mesh.concat(parts)


# ------------------------------------------------------------ weapons

def battle_axe(mats, length=1.25, grip=0.55, scale=1.0):
    """A long two-handed bearded axe: haft from -grip, a broad steel blade on
    top (`scale` enlarges the blade)."""
    s = steel(mats)
    parts = [haft(mats, -grip, length - grip, 0.022)]
    top = length - grip
    k = scale
    head = M.extrude([(-0.03, -0.06 * k), (0.05, -0.06 * k), (0.24 * k, -0.20 * k), (0.27 * k, -0.02 * k),
                      (0.28 * k, 0.08 * k), (0.22 * k, 0.14 * k), (0.05, 0.05 * k), (-0.03, 0.05 * k)], 0.024, s)
    parts.append(head.transformed(M.translate(0, 0, top - 0.10) @ M.rot_z(90)))
    parts.append(M.cylinder(0.03, 0.03, 0.12, s, seg=8, z0=top - 0.15))
    parts.append(M.cylinder(0.024, 0.024, 0.16, leather(mats), seg=8, z0=-grip + 0.02))
    return M.Mesh.concat(parts)


def long_sword(mats, length=0.86, width=0.06):
    """A Celtic long sword with a bronze hilt."""
    s = steel(mats)
    parts = [M.cylinder(0.02, 0.02, 0.15, leather(mats), seg=8, z0=-0.08)]
    parts.append(M.sphere(0.032, bronze(mats), (0, 0, -0.09), seg=8))
    parts.append(M.rounded_box(0.03, 0.12, 0.025, bronze(mats), (0, 0, 0.075), r=0.01, seg=8))
    parts.append(M.blade(length, width, 0.012, s, tip=0.12, z0=0.085).transformed(M.rot_z(90)))
    return M.Mesh.concat(parts)


def gladius(mats):
    from models.props import sword
    return sword(mats, length=0.52, width=0.075, guard=0.1)


def strip_blade(pts, widths, thick, m, offset=0.0):
    """A thin blade along a curve in the y-z plane: pts [(y, z)] along its
    spine, widths per point (measured toward +y of the curve's normal when
    offset=0.5, centered when 0), diamond cross-section `thick` across x."""
    pts = np.asarray(pts, np.float32)
    n = len(pts)
    tan = np.gradient(pts, axis=0)
    tan /= np.linalg.norm(tan, axis=1, keepdims=True) + 1e-9
    nrm = np.stack([tan[:, 1], -tan[:, 0]], 1)   # +y-ish side of a +z curve
    V = []
    for i in range(n):
        w = widths[i]
        c = pts[i] + nrm[i] * w * offset
        a = c + nrm[i] * w / 2
        b = c - nrm[i] * w / 2
        t = thick / 2 * (1.0 if w > 1e-4 else 0.0)
        V += [(0, a[0], a[1]), (t, c[0], c[1]), (0, b[0], b[1]), (-t, c[0], c[1])]
    V = np.array(V, np.float32)
    F = []
    for i in range(n - 1):
        for j in range(4):
            a0, a1 = 4 * i + j, 4 * i + (j + 1) % 4
            b0, b1 = a0 + 4, a1 + 4
            F += [(a0, a1, b1), (a0, b1, b0)]
    F = np.array(F)
    vv = V[F]
    area = np.linalg.norm(np.cross(vv[:, 1] - vv[:, 0], vv[:, 2] - vv[:, 0]), axis=-1)
    tris = vv[area > 1e-10]
    nn = np.cross(tris[:, 1] - tris[:, 0], tris[:, 2] - tris[:, 0])
    nn /= np.linalg.norm(nn, axis=1, keepdims=True) + 1e-12
    # orient normals away from the spine plane x = 0 (faces are |x|-signed)
    cx = tris[:, :, 0].mean(1)
    flip = (nn[:, 0] * np.sign(cx + 1e-9)) < 0
    nn[flip] *= -1
    tris[flip] = tris[flip][:, ::-1]
    N = np.repeat(nn[:, None], 3, 1)
    return M.Mesh(tris.astype(np.float32), N.astype(np.float32), np.full(len(tris), m, np.int32))


def scimitar(mats, length=0.78, width=0.065, curve=0.17):
    """A curved sabre: the blade sweeps back from the edge (+y), widening
    toward the point."""
    s = steel(mats)
    parts = [M.cylinder(0.02, 0.02, 0.13, leather(mats), seg=8, z0=-0.07)]
    parts.append(M.rounded_box(0.035, 0.13, 0.025, bronze(mats), (0, 0, 0.07), r=0.01, seg=8))
    k = 10
    us = np.linspace(0, 1, k + 1)
    pts = [(-curve * u * u, 0.08 + length * u) for u in us]
    widths = [width * (0.8 + 0.6 * u) * (1 - u ** 5) + 0.002 for u in us]
    parts.append(strip_blade(pts, widths, 0.014, s, offset=0.0))
    return M.Mesh.concat(parts)


def katana(mats, size=1.0):
    s = steel(mats)
    parts = [M.cylinder(0.019, 0.019, 0.26, mat(mats, 'hilt', '#1e1a1e'), seg=8, z0=-0.2)]
    parts.append(M.cylinder(0.05, 0.05, 0.012, mat(mats, 'tsuba', '#3a3430', spec=0.4, metal=True), seg=10, z0=0.06))
    us = np.linspace(0, 1, 9)
    pts = [(-0.06 * u * u, 0.07 + 0.82 * u) for u in us]
    widths = [0.034 * size * (1 - u ** 8) + 0.002 for u in us]
    parts.append(strip_blade(pts, widths, 0.012, s).transformed(M.scale(1, 1, size)))
    return M.Mesh.concat(parts)


def kama(mats, size=1.0):
    """A sickle: a light wooden handle and a hooked blade at the top, reaching
    forward (+y) and curving down."""
    s = steel(mats)
    parts = [M.cylinder(0.018, 0.02, 0.40, mat(mats, 'wood_light', '#c8a060'), seg=8, z0=-0.10)]
    us = np.linspace(0, 1, 9)
    pts = [(0.20 * u, 0.29 + 0.06 * math.sin(math.pi * u * 0.8) - 0.07 * u * u) for u in us]
    widths = [0.045 * (1 - u ** 3) + 0.002 for u in us]
    parts.append(strip_blade(pts, widths, 0.012, s, offset=0.3))
    return M.Mesh.concat(parts).transformed(M.scale(size))


def katana_sheathed(mats):
    """A katana in its black scabbard (worn at the left hip, pelvis space)."""
    sc = mat(mats, 'scabbard', '#16141a', spec=0.4, gloss=20)
    hilt = mat(mats, 'hilt', '#1e1a1e')
    parts = [M.capsule((0, 0, 0), (0, 0, 0.74), 0.024, 0.02, sc, seg=8, rings=1),
             M.cylinder(0.019, 0.019, 0.24, hilt, seg=8, z0=-0.27),
             M.cylinder(0.045, 0.045, 0.012, mat(mats, 'tsuba', '#3a3430', spec=0.4, metal=True), seg=10, z0=-0.03)]
    return M.Mesh.concat(parts)


def kama(mats, size=1.0):
    """A sickle: a wooden handle and a hooked blade at the top, pointing +y."""
    s = steel(mats)
    parts = [M.cylinder(0.018, 0.02, 0.40, mat(mats, 'wood_light', '#c8a060'), seg=8, z0=-0.10)]
    pts = []
    for i in range(8):
        a = math.radians(-10 + 110 * i / 7)
        pts.append((0.15 * math.sin(a), 0.30 + 0.12 * (1 - math.cos(a)) * 0.6 + 0.0))
    inner = [(x * 0.72, z - 0.012) for x, z in pts[::-1]]
    poly = pts + inner
    blade = M.extrude([(-x, z) for x, z in poly][::-1], 0.01, s)
    parts.append(blade.transformed(M.rot_z(-90)))
    return M.Mesh.concat(parts)


def flail(mats, length=0.55, swing=0.0):
    """A flail: a handle, a short chain and a spiked steel ball hanging from it
    (its chain hangs straight along +z from the top of the handle)."""
    s = steel(mats)
    parts = [haft(mats, -0.1, length - 0.1, 0.02), M.cylinder(0.028, 0.028, 0.05, s, seg=8, z0=length - 0.12)]
    top = length - 0.08
    for i in range(4):
        parts.append(M.ellipsoid(0.012, 0.012, 0.025, s, (0, 0, top + 0.04 * i + 0.02), seg=6))
    c = (0, 0, top + 0.24)
    parts.append(M.sphere(0.06, s, c, seg=10))
    for d in ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0.7, 0.7, 0), (-0.7, 0.7, 0), (0.7, -0.7, 0),
              (-0.7, -0.7, 0)):
        d = np.array(d, np.float32)
        parts.append(M.cone(0.022, 0.05, s, seg=5).transformed(M.translate(*c) @ M.look_along((0, 0, 0), d) @
                                                              M.translate(0, 0, 0.05)))
    return M.Mesh.concat(parts)


def macuahuitl(mats, length=0.85):
    """The Aztec obsidian-edged war club: a flat wooden paddle with black
    blades along both edges."""
    w = wood(mats)
    ob = mat(mats, 'obsidian', '#1a1820', spec=0.6, gloss=30)
    parts = [M.cylinder(0.02, 0.022, 0.16, w, seg=8, z0=-0.09)]
    parts.append(M.extrude([(-0.025, 0.06), (0.025, 0.06), (0.05, 0.25), (0.05, length), (0.0, length + 0.04),
                            (-0.05, length), (-0.05, 0.25)], 0.026, w).transformed(M.rot_z(90)))
    for sx in (1, -1):
        for i in range(6):
            z = 0.28 + i * (length - 0.32) / 5
            parts.append(M.extrude([(0, -0.03), (0.035, 0.0), (0, 0.03)], 0.012, ob).transformed(
                M.translate(0, sx * 0.05, z) @ M.rot_z(90 if sx > 0 else -90)))
    return M.Mesh.concat(parts)


def iklwa(mats):
    """The Zulu short stabbing spear: a short haft and a long broad blade."""
    s = steel(mats)
    parts = [haft(mats, -0.32, 0.55, 0.018)]
    tip = M.lathe([(0, 0.02), (0.03, 0.04), (0.14, 0.05), (0.26, 0.03), (0.34, 0.0005)], s, seg=8)
    parts.append(tip.transformed(M.translate(0, 0, 0.53) @ M.scale(1.0, 0.35, 1.0)))
    parts.append(M.cylinder(0.022, 0.022, 0.06, rope(mats), seg=8, z0=0.5))
    return M.Mesh.concat(parts)


def javelin(mats, length=1.5, grip=0.6):
    """A light throwing spear with feathered decorations near the grip."""
    s = steel(mats)
    z0, z1 = -grip, length - grip
    parts = [haft(mats, z0, z1 - 0.12, 0.013)]
    parts.append(M.lathe([(0, 0.016), (0.03, 0.026), (0.10, 0.012), (0.15, 0.0005)], s, seg=7)
                 .transformed(M.translate(0, 0, z1 - 0.15) @ M.scale(1, 0.5, 1)))
    fm = mat(mats, 'feather', '#ffffff', tint=True)
    parts.append(M.cylinder(0.02, 0.02, 0.05, fm, seg=7, z0=z1 - 0.32))
    return M.Mesh.concat(parts)


def javelin_bundle(mats, n=3, length=1.6):
    """Several javelins held together in the left hand, butts down."""
    parts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        j = javelin(mats, length=length, grip=0.75)
        parts.append(j.transformed(M.translate(0.03 * math.cos(a), 0.03 * math.sin(a), 0.02 * i) @ M.rot_x(3 * math.sin(a))
                                   @ M.rot_y(3 * math.cos(a))))
    return M.Mesh.concat(parts)


def pike(mats, length=3.0, grip=1.15, halberd=False, shaft=None):
    """A long pike with a narrow steel head (and lugs); halberd adds an axe
    blade and back spike."""
    s = steel(mats)
    z0, z1 = -grip, length - grip
    sm = mat(mats, shaft, '#ffffff', tint=True) if shaft else None
    parts = [haft(mats, z0, z1 - 0.25, 0.019, m=sm)]
    tip = M.lathe([(0, 0.02), (0.03, 0.03), (0.12, 0.026), (0.3, 0.0005)], s, seg=8)
    parts.append(tip.transformed(M.translate(0, 0, z1 - 0.3) @ M.scale(1, 0.45, 1)))
    parts.append(M.cylinder(0.025, 0.022, 0.12, s, seg=8, z0=z1 - 0.38))
    if halberd:
        parts.append(M.extrude([(0.0, -0.08), (0.18, -0.12), (0.2, 0.04), (0.0, 0.05)], 0.012, s)
                     .transformed(M.translate(0, 0, z1 - 0.33) @ M.rot_z(90)))
        parts.append(M.extrude([(0.0, -0.02), (-0.12, 0.0), (0.0, 0.025)], 0.012, s)
                     .transformed(M.translate(0, 0, z1 - 0.33) @ M.rot_z(90)))
    else:
        for sx in (1, -1):
            parts.append(M.capsule((0, 0, z1 - 0.34), (sx * 0.07, 0, z1 - 0.30), 0.01, 0.006, s, seg=5, rings=1))
    return M.Mesh.concat(parts)


def rapier(mats, length=0.85):
    s = steel(mats)
    parts = [M.cylinder(0.018, 0.018, 0.12, leather(mats), seg=8, z0=-0.06)]
    parts.append(M.sphere(0.028, s, (0, 0, -0.07), seg=8))
    parts.append(M.rounded_box(0.025, 0.16, 0.02, s, (0, 0, 0.065), r=0.008, seg=6))
    # a cup/knuckle guard
    parts.append(M.ellipsoid(0.05, 0.05, 0.03, s, (0, 0, 0.07), seg=10))
    parts.append(M.blade(length, 0.028, 0.01, s, tip=0.1, z0=0.08).transformed(M.rot_z(90)))
    return M.Mesh.concat(parts)


def star_mace(mats, length=0.62):
    """An Inca champi: a wooden shaft topped by a star-shaped bronze/stone head."""
    parts = [haft(mats, -0.12, length, 0.018)]
    hm = mat(mats, 'mace_head', '#3a3634', spec=0.3, gloss=14)
    c = (0, 0, length + 0.02)
    parts.append(M.cylinder(0.045, 0.045, 0.05, hm, seg=10, z0=length - 0.005))
    for i in range(6):
        a = 2 * math.pi * i / 6
        d = np.array((math.cos(a), math.sin(a), 0), np.float32)
        parts.append(M.cone(0.025, 0.06, hm, seg=5).transformed(M.translate(*c) @ M.look_along((0, 0, 0), d) @
                                                              M.translate(0, 0, 0.03)))
    return M.Mesh.concat(parts)


def torch(mats, length=0.55):
    """A torch: a wooden stick wrapped at the top, with a flame."""
    parts = [haft(mats, -0.12, length, 0.02)]
    parts.append(M.cylinder(0.034, 0.03, 0.10, mat(mats, 'rag', '#3a2a1c'), seg=8, z0=length - 0.06))
    fl = mat(mats, 'flame', '#ffb020', spec=0.0)
    fl2 = mat(mats, 'flame_core', '#fff0a0', spec=0.0)
    parts.append(M.lathe([(0, 0.04), (0.05, 0.055), (0.14, 0.035), (0.24, 0.0005)], fl, seg=9)
                 .transformed(M.translate(0, 0, length + 0.03)))
    parts.append(M.lathe([(0, 0.025), (0.04, 0.03), (0.12, 0.0005)], fl2, seg=7)
                 .transformed(M.translate(0, 0.0, length + 0.05)))
    return M.Mesh.concat(parts)


def book(mats):
    """A closed holy book held in the left hand: civ-colored cover, pages, a
    gold cross on the front."""
    cov = mat(mats, 'book', '#ffffff', tint=True)
    pg = mat(mats, 'pages', '#ece6d0', spec=0.02)
    gold = mat(mats, 'gold', '#e0b848', spec=0.6, gloss=22, metal=True)
    parts = [M.rounded_box(0.075, 0.27, 0.34, cov, (0, 0.06, 0.08), r=0.012, seg=6),
             M.box(0.06, 0.255, 0.012, pg, (0.0, 0.065, 0.25)), M.box(0.06, 0.012, 0.32, pg, (0.0, 0.198, 0.08))]
    parts.append(M.box(0.008, 0.025, 0.16, gold, (-0.041, 0.06, 0.09)))
    parts.append(M.box(0.008, 0.10, 0.025, gold, (-0.041, 0.06, 0.13)))
    return M.Mesh.concat(parts)


def gourd(mats):
    """A water gourd held by its neck."""
    g = mat(mats, 'gourd', '#c8a060', spec=0.15)
    return M.Mesh.concat([M.ellipsoid(0.08, 0.08, 0.10, g, (0, 0, -0.14), seg=10),
                          M.cylinder(0.025, 0.03, 0.08, g, seg=8, z0=-0.06),
                          M.cylinder(0.02, 0.02, 0.03, mat(mats, 'cork', '#4a3020'), seg=6, z0=0.02)])


def longbow(mats, draw=0.0):
    return bow(mats, height=1.85, draw=draw, bend=0.12)


def bold_bow(mats, height=1.4, draw=0.0, bend=0.14, thick=1.8):
    """A bow drawn chunky (Civ3's bows read at map scale): thicker limbs."""
    return bow(mats, height=height, draw=draw, bend=bend).transformed(M.scale(thick, 1.25, 1.0))


def flying_arrow(mats):
    from models.props import arrow
    return arrow(mats)


def spear_long(mats):
    return spear(mats, length=2.4, grip=0.85)
