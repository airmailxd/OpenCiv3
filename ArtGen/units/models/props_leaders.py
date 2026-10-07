"""Gear for the leaders, kings, armies and the Holy Relic (see models/leaders.py).

Conventions follow models/props.py:
  * held props: grip at the origin, long axis +z (the business end up);
  * headgear: head-bone space *before* the head scale (humanoid.HEAD), the
    skull centered at (0, 0.012, 0.115) with radii (0.098, 0.112, 0.122),
    the face toward +y;
  * worn gear on other bones: that bone's space.
Materials are looked up by name and created with a default color when the
catalog didn't define them (props.mat), so every color is overridable.
"""
import math

import numpy as np

import mesh as M
from models import props as P

mat = P.mat


def gold(mats):
    return mat(mats, 'gold', '#d8b048', spec=0.9, gloss=26, metal=True)


def silver(mats):
    return mat(mats, 'silver', '#c8ccd2', spec=0.9, gloss=30, metal=True)


def m_(mats, name, color, **kw):
    """A material by name (created with `color` if missing)."""
    return mat(mats, name, color, **kw)


# ------------------------------------------------------------ surfaces

def sheet(pts, mats_of_face, smooth=True):
    """A quad grid surface from a (nv, nu, 3) array of points; mats_of_face is
    an (nv-1, nu-1) int array of material ids (or one id)."""
    pts = np.asarray(pts, np.float32)
    nv, nu = pts.shape[:2]
    v = pts.reshape(-1, 3)
    f = []
    fm = []
    mf = np.broadcast_to(np.asarray(mats_of_face, np.int32), (nv - 1, nu - 1))
    for i in range(nv - 1):
        for j in range(nu - 1):
            a, b, c, d = i * nu + j, i * nu + j + 1, (i + 1) * nu + j, (i + 1) * nu + j + 1
            f += [(a, c, d), (a, d, b)]
            fm += [mf[i, j], mf[i, j]]
    f = np.array(f, np.int64)
    m = M.from_indexed(v, f, 0, smooth=smooth)
    # drop degenerate triangles
    V = m.V
    area = np.linalg.norm(np.cross(V[:, 1] - V[:, 0], V[:, 2] - V[:, 0]), axis=-1)
    keep = area > 1e-10
    return M.Mesh(m.V[keep], m.N[keep], np.asarray(fm, np.int32)[keep])


def arc_loft(rings, mat_id, a0=0.0, a1=360.0, seg=16, mat_fn=None):
    """Like mesh.loft but over the angle range [a0, a1] degrees only (0 = +x,
    90 = front +y), open at the sides: collars, plastrons, open coats.
    rings: (z, rx, ry[, dx, dy]). mat_fn(i_ring, angle_deg) -> material id
    overrides per face."""
    closed = abs((a1 - a0) - 360) < 1e-6
    n = seg if closed else seg + 1
    ang = np.radians(np.linspace(a0, a1, n, endpoint=not closed))
    pts = []
    for r in rings:
        z, rx, ry = r[0], r[1], r[2]
        dx, dy = (r[3], r[4]) if len(r) > 4 else (0.0, 0.0)
        pts.append(np.stack([dx + rx * np.cos(ang), dy + ry * np.sin(ang), np.full(n, z)], -1))
    pts = np.array(pts)
    if closed:
        pts = np.concatenate([pts, pts[:, :1]], 1)
    mids = np.degrees((ang[:-1] + ang[1:]) / 2) if not closed else np.degrees(
        np.concatenate([(ang[:-1] + ang[1:]) / 2, [ang[-1] + math.pi / seg]]))
    mf = np.full((len(rings) - 1, pts.shape[1] - 1), mat_id, np.int32)
    if mat_fn is not None:
        for i in range(len(rings) - 1):
            for j in range(pts.shape[1] - 1):
                mf[i, j] = mat_fn(i, mids[j] if j < len(mids) else mids[-1])
    return sheet(pts, mf)


def torus(R, r, mat_id, seg=18, sides=8, z=0.0, sy=1.0):
    """A ring (bands, rims, turban wraps) around z, at height z."""
    prof = []
    for k in range(sides + 1):
        t = 2 * math.pi * k / sides
        prof.append((z + r * math.sin(t), R + r * math.cos(t)))
    pts = []
    ang = np.linspace(0, 2 * math.pi, seg + 1)
    for zz, rr in prof:
        pts.append(np.stack([rr * np.cos(ang), sy * rr * np.sin(ang), np.full(seg + 1, zz)], -1))
    return sheet(np.array(pts), mat_id)


# ------------------------------------------------------------ held props

def crook(mats, length=1.75, grip=1.0, r=0.02, hook=0.07):
    """A shepherd's crook (the ancient leader's staff): a pole with a hooked top."""
    w = P.wood(mats)
    z0, z1 = -grip, length - grip - 0.12
    parts = [M.cylinder(r, r * 0.9, z1 - z0, w, seg=8, z0=z0)]
    # the hook: an arc curling forward (+y) then down
    pts = []
    for i in range(9):
        a = math.radians(-90 + 200 * i / 8)
        pts.append((0, 0.07 + 0.07 * math.sin(a), z1 + 0.07 * math.cos(a) + 0.0))
    pts = [(0, 0.0, z1)] + [(0, hook - hook * math.cos(math.radians(200 * i / 8)), z1 + hook * math.sin(math.radians(200 * i / 8)) + 0.0)
                            for i in range(1, 9)]
    for a, b in zip(pts[:-1], pts[1:]):
        parts.append(M.capsule(a, b, r * 0.9, r * 0.85, w, seg=7, rings=1))
    return M.Mesh.concat(parts)


def scepter(mats, length=0.62, grip=0.18, head='orb', m='gold'):
    """A short golden scepter: shaft and a knob, orb-and-cross or eagle-ish head."""
    g = gold(mats) if m == 'gold' else mat(mats, m, '#d8b048', spec=0.9, gloss=26, metal=True)
    parts = [M.cylinder(0.018, 0.015, length, g, seg=8, z0=-grip)]
    top = length - grip
    parts.append(M.sphere(0.03, g, (0, 0, -grip), seg=8))
    if head == 'orb':
        parts.append(M.sphere(0.05, g, (0, 0, top + 0.03), seg=10))
        parts.append(M.box(0.016, 0.016, 0.08, g, (0, 0, top + 0.11)))
        parts.append(M.box(0.06, 0.016, 0.016, g, (0, 0, top + 0.12)))
    elif head == 'fleur':
        parts.append(M.sphere(0.035, g, (0, 0, top + 0.02), seg=8))
        parts.append(M.cone(0.03, 0.12, g, seg=6, z0=top + 0.04))
        for s in (1, -1):
            parts.append(M.capsule((0, 0, top + 0.05), (s * 0.06, 0, top + 0.12), 0.016, 0.006, g, seg=5, rings=1))
    elif head == 'eagle':
        parts.append(M.sphere(0.035, g, (0, 0, top + 0.02), seg=8))
        parts.append(M.ellipsoid(0.03, 0.05, 0.04, g, (0, 0.01, top + 0.08), seg=8))
        for s in (1, -1):
            parts.append(M.extrude([(0, 0), (0.12, 0.06), (0.1, 0.1), (0.0, 0.05)], 0.012, g)
                         .transformed(M.translate(0, 0, top + 0.07) @ M.scale(s, 1, 1)))
    elif head == 'mace':
        parts.append(M.ellipsoid(0.06, 0.06, 0.08, g, (0, 0, top + 0.04), seg=10))
        for a in range(0, 360, 60):
            parts.append(M.cone(0.02, 0.05, g, seg=5).transformed(
                M.translate(0, 0, top + 0.04) @ M.rot_z(a) @ M.rot_y(90)))
    return M.Mesh.concat(parts)


def tall_scepter(mats, length=1.6, grip=0.95, head='eagle'):
    """A long ceremonial staff (gold), e.g. Caesar's eagle standard."""
    return scepter(mats, length=length, grip=grip, head=head)


def cane(mats, length=0.92, grip=0.86, m='cane'):
    """A walking cane, held at its top (the tip down: long axis -z)."""
    c = mat(mats, m, '#3a2a1e', spec=0.3, gloss=20)
    parts = [M.cylinder(0.014, 0.012, length, c, seg=7, z0=-grip)]
    parts.append(M.sphere(0.024, gold(mats), (0, 0, length - grip), seg=8))
    return M.Mesh.concat(parts)


def walking_stick(mats, length=1.5, grip=0.95):
    """A long plain bamboo stick (Gandhi)."""
    c = mat(mats, 'stick', '#8a6038', spec=0.05)
    return M.cylinder(0.016, 0.014, length, c, seg=7, z0=-grip)


def flail(mats):
    """Egyptian crook (heka) in gold and blue bands."""
    g = gold(mats)
    b = mat(mats, 'band', '#ffffff', tint=True)
    parts = []
    z = -0.2
    for i in range(8):
        parts.append(M.cylinder(0.018, 0.018, 0.06, g if i % 2 == 0 else b, seg=8, z0=z + i * 0.06))
    top = z + 8 * 0.06
    pts = [(0, 0.0, top)] + [(0, 0.06 - 0.06 * math.cos(math.radians(200 * i / 7)),
                              top + 0.06 * math.sin(math.radians(200 * i / 7))) for i in range(1, 8)]
    for a, c in zip(pts[:-1], pts[1:]):
        parts.append(M.capsule(a, c, 0.017, 0.016, g, seg=7, rings=1))
    return M.Mesh.concat(parts)


def longsword(mats, length=0.95, grip=0.0, guard=0.24):
    """A long knightly sword (also held point down, resting)."""
    return P.sword(mats, length=length, width=0.06, guard=guard)


def katana(mats, length=0.82):
    s = P.steel(mats)
    parts = [M.cylinder(0.02, 0.02, 0.26, mat(mats, 'hilt', '#2a2a30'), seg=8, z0=-0.13)]
    parts.append(M.cylinder(0.05, 0.05, 0.012, gold(mats), seg=10, z0=0.13))
    b = M.blade(length, 0.035, 0.012, s, tip=0.08, z0=0.14)
    # a gentle curve: bend back progressively
    V = b.V.copy()
    zz = (V[..., 2] - 0.14) / length
    V[..., 0] -= 0.06 * zz ** 2
    parts.append(M.Mesh(V, b.N, b.M).transformed(M.rot_z(90)))
    return M.Mesh.concat(parts)


def scimitar(mats, length=0.7):
    s = P.steel(mats)
    parts = [M.cylinder(0.02, 0.02, 0.13, P.leather(mats), seg=8, z0=-0.07)]
    parts.append(M.rounded_box(0.03, 0.14, 0.025, gold(mats), (0, 0, 0.07), r=0.008, seg=8))
    poly = [(-0.022, 0.08)]
    n = 7
    for i in range(n + 1):
        u = i / n
        poly.append((-0.022 + 0.09 * u ** 2, 0.08 + length * u))
    for i in range(n, -1, -1):
        u = i / n
        poly.append((0.022 + 0.09 * u ** 2 + 0.03 * math.sin(math.pi * u), 0.08 + length * u * 0.97))
    b = M.extrude(poly, 0.01, s)
    parts.append(b.transformed(M.rot_z(90)))
    return M.Mesh.concat(parts)


def spear_feathered(mats, length=2.0, grip=0.9):
    """An Iroquois/Zulu style spear with feathers tied below the point."""
    parts = [P.spear(mats, length=length, grip=grip)]
    f = mat(mats, 'feather', '#e8e2d0', spec=0.0)
    f2 = mat(mats, 'feather2', '#b0583a', spec=0.0)
    top = length - grip - 0.3
    for i, a in enumerate((0, 120, 240)):
        fl = M.extrude([(0.0, 0.0), (0.03, -0.04), (0.035, -0.2), (0.0, -0.24), (-0.02, -0.12)], 0.006, f if i else f2)
        parts.append(fl.transformed(M.translate(0, 0, top) @ M.rot_z(a) @ M.translate(0.02, 0, 0)))
    return M.Mesh.concat(parts)


def iklwa(mats, length=1.2, grip=0.5):
    """A Zulu short stabbing spear with a long blade."""
    s = P.steel(mats)
    parts = [P.haft(mats, -grip, length - grip - 0.3, 0.016)]
    b = M.blade(0.34, 0.07, 0.014, s, tip=0.35, z0=length - grip - 0.32)
    parts.append(b)
    return M.Mesh.concat(parts)


def knobkerrie(mats, length=0.6):
    w = P.wood(mats)
    return M.Mesh.concat([M.cylinder(0.016, 0.02, length, w, seg=7, z0=-0.1),
                          M.sphere(0.055, w, (0, 0, length - 0.1), seg=9)])


def cowhide_shield(mats, w=0.5, h=0.95):
    """A tall oval Nguni shield: hide in the civ color with white patches, a
    stick behind it."""
    t = mat(mats, 'hide', '#ffffff', tint=True)
    wpatch = mat(mats, 'hide_patch', '#efe8dc')
    ang = np.linspace(0, 2 * math.pi, 23)
    parts = []
    rings = []
    for k in range(5):
        s = 1 - k / 4
        rings.append([(w / 2 * s * math.cos(a), -0.04 * (1 - s * s), h / 2 * s * math.sin(a)) for a in ang])
    pts = np.array(rings)
    mf = np.full((4, 22), t, np.int32)
    mf[1:3, 3:7] = wpatch
    mf[0:2, 14:18] = wpatch
    parts.append(sheet(pts, mf))
    parts.append(P.haft(mats, -h / 2 - 0.1, h / 2 + 0.12, 0.014).transformed(M.translate(0, 0.02, 0)))
    return M.Mesh.concat(parts)


def axe_gold(mats, length=0.7):
    """A ceremonial axe/mace with a gold crescent head."""
    parts = [P.haft(mats, -0.15, length, 0.018)]
    head = M.extrude([(-0.02, -0.03), (0.06, -0.05), (0.15, -0.12), (0.19, 0.0),
                      (0.15, 0.12), (0.06, 0.05), (-0.02, 0.03)], 0.02, gold(mats))
    parts.append(head.transformed(M.translate(0, 0, length - 0.1) @ M.rot_z(90)))
    return M.Mesh.concat(parts)


def feather_standard(mats, length=2.3, grip=1.0):
    """Pachacuti's tall standard: a pole topped with a spray of long feathers
    (red, blue, green), like a palm."""
    w = mat(mats, 'pole', '#5a3a24')
    parts = [M.cylinder(0.02, 0.018, length - 0.05, w, seg=8, z0=-grip)]
    top = length - grip
    parts.append(M.sphere(0.04, gold(mats), (0, 0, top - 0.05), seg=8))
    cols = [mat(mats, 'plume_r', '#c83a3a', spec=0.02), mat(mats, 'plume_b', '#ffffff', tint=True, spec=0.02),
            mat(mats, 'plume_g', '#3a9a6a', spec=0.02)]
    for i in range(11):
        a = 360 * i / 11
        elev = 25 + 18 * ((i * 7) % 3)
        L = 0.42 + 0.06 * ((i * 5) % 3)
        fe = M.extrude([(0, -0.012), (L * 0.5, -0.04), (L, 0.0), (L * 0.5, 0.04), (0, 0.012)], 0.008, cols[i % 3])
        # leaf along +x drooping: rotate up by elev then around z
        fe = fe.transformed(M.rot_y(-elev))
        V = fe.V.copy()
        V[..., 2] -= 0.35 * np.maximum(V[..., 0], 0) ** 2   # droop
        fe = M.Mesh(V, fe.N, fe.M)
        parts.append(fe.transformed(M.translate(0, 0, top - 0.02) @ M.rot_z(a)))
    return M.Mesh.concat(parts)


def binoculars(mats):
    c = mat(mats, 'binoc', '#2a2a2a', spec=0.3)
    return M.Mesh.concat([M.cylinder(0.025, 0.025, 0.12, c, seg=8, z0=-0.06).transformed(M.translate(0.03, 0, 0)),
                          M.cylinder(0.025, 0.025, 0.12, c, seg=8, z0=-0.06).transformed(M.translate(-0.03, 0, 0))])


def orb(mats):
    g = gold(mats)
    return M.Mesh.concat([M.sphere(0.065, g, (0, 0, 0.04), seg=12),
                          M.box(0.016, 0.016, 0.08, g, (0, 0, 0.13)), M.box(0.06, 0.016, 0.016, g, (0, 0, 0.14))])


def book(mats):
    c = mat(mats, 'book', '#5a2a20')
    pg = mat(mats, 'paper', '#ece4d0')
    return M.Mesh.concat([M.rounded_box(0.05, 0.18, 0.24, c, (0, 0, 0.05), r=0.01, seg=8),
                          M.box(0.04, 0.17, 0.22, pg, (0.008, 0.006, 0.05))])


def flower(mats):
    st = mat(mats, 'stem', '#4a7a3a')
    pe = mat(mats, 'petal', '#ffffff', tint=True)
    return M.Mesh.concat([M.cylinder(0.008, 0.008, 0.3, st, seg=5, z0=-0.05),
                          M.sphere(0.05, pe, (0, 0, 0.27), seg=8)])


def pipe(mats):
    c = mat(mats, 'pipe', '#4a3020')
    return M.Mesh.concat([M.capsule((0, 0, 0), (0, 0.12, 0.02), 0.01, 0.01, c, seg=5, rings=1),
                          M.cylinder(0.022, 0.024, 0.05, c, seg=8, z0=0.0).transformed(M.translate(0, 0.13, 0.0))])


def briefcase(mats):
    c = mat(mats, 'case', '#6a4a2e')
    return M.Mesh.concat([M.rounded_box(0.08, 0.32, 0.24, c, (0, 0, -0.16), r=0.02, seg=8),
                          M.capsule((0, -0.05, -0.03), (0, 0.05, -0.03), 0.01, 0.01, c, seg=5, rings=1)])


# ------------------------------------------------------------ banners (armies)

def banner(mats, kind='ancient', phase=0.0, amp=1.0, grip=0.95, length=2.22):
    """A standard on a pole held at `grip` above the butt. kind: 'ancient'
    (a hanging cloth on a crossbar with stripes), 'middle' (a square flag,
    quartered), 'industrial' (a waving flag with roundels), 'modern' (a waving
    flag with diagonal crosses). The field is the civ color; the patterns
    use fixed colors like the originals. phase animates the cloth."""
    w = mat(mats, 'pole', '#6a4a2c')
    field = mat(mats, 'flag', '#ffffff', tint=True, spec=0.02)
    red = mat(mats, 'flag_red', '#a82c38', spec=0.02)
    green = mat(mats, 'flag_green', '#3a9a40', spec=0.02)
    cyan = mat(mats, 'flag_cyan', '#2aa69a', spec=0.02)
    parts = []
    L = length
    top = L - grip
    parts.append(M.cylinder(0.02, 0.018, L, w, seg=8, z0=-grip))
    if kind == 'ancient':
        # a crossbar facing forward with the cloth hanging under it (a vexillum),
        # spread across x
        bw = 0.78
        parts.append(M.cylinder(0.016, 0.016, bw, w, seg=6, z0=-bw / 2).transformed(M.translate(0, 0, top - 0.06) @ M.rot_y(90)))
        for s in (1, -1):
            parts.append(M.sphere(0.025, gold(mats), (s * bw / 2, 0, top - 0.06), seg=6))
        parts.append(M.cone(0.03, 0.09, gold(mats), seg=6, z0=top))
        nu, nv = 13, 8
        H = 0.62
        pts = np.zeros((nv, nu, 3), np.float32)
        for i in range(nv):
            for j in range(nu):
                u = j / (nu - 1)
                v = i / (nv - 1)
                x = (u - 0.5) * (bw - 0.04)
                z = top - 0.08 - v * H
                y = 0.03 * amp * math.sin(2 * math.pi * (u * 1.2 + phase)) * (0.3 + v) + 0.04 * v * math.sin(2 * math.pi * phase)
                pts[i, j] = (x, y, z)
        mf = np.full((nv - 1, nu - 1), field, np.int32)
        for j, c in ((2, green), (5, red), (7, red), (9, cyan)):
            mf[1:6, j] = c
        mf[-1, ::2] = field
        parts.append(sheet(pts, mf))
        return M.Mesh.concat(parts)
    # a flag fixed along the pole, flying toward +x (the bearer's right), waving
    fw, fh = (0.74, 0.7) if kind == 'middle' else (0.84, 0.58)
    parts.append(M.sphere(0.03, gold(mats), (0, 0, top + 0.02), seg=8))
    nu, nv = 14, 9
    pts = np.zeros((nv, nu, 3), np.float32)
    for i in range(nv):
        for j in range(nu):
            u = j / (nu - 1)
            v = i / (nv - 1)
            x = -u * fw   # flying to the bearer's left, like the originals
            z = top - 0.04 - v * fh - 0.10 * u * u * (0.6 if kind == 'middle' else 1.0)
            y = 0.07 * amp * u * math.sin(2 * math.pi * (1.3 * u - phase)) + 0.02 * u * math.sin(2 * math.pi * (v - phase))
            pts[i, j] = (x, y, z)
    mf = np.full((nv - 1, nu - 1), field, np.int32)
    if kind == 'middle':
        mf[0:4, 0:6] = red
        mf[0:4, 7:13] = green
        mf[4:8, 7:13] = cyan
        mf[5:8, 1:5] = red
        # a swallowtail: a notch in the fly
        pts[3:6, -3:, 0] = pts[3:6, -4:-3, 0]
    elif kind == 'industrial':
        for (ci, cj, c) in ((2, 4, red), (5, 9, green), (2, 10, red), (6, 3, cyan)):
            mf[ci:ci + 2, cj:cj + 2] = c
    elif kind == 'modern':
        for j in range(nu - 1):
            i1 = int(round(j * (nv - 2) / (nu - 2)))
            mf[i1, j] = red
            mf[nv - 2 - i1, j] = green if j % 2 else cyan
        mf[3:5, :] = red
        mf[:, 5:7] = red
    parts.append(sheet(pts, mf))
    return M.Mesh.concat(parts)


# ------------------------------------------------------------ headgear (head space, unscaled)

def crown(mats, m='gold', r=0.108, h=0.06, z=0.175, points=8, spike=0.07, jewels='jewel', arches=False, ry=1.1):
    g = gold(mats) if m == 'gold' else mat(mats, m, '#d8b048', spec=0.9, gloss=26, metal=True)
    parts = [M.cylinder(r, r * 1.04, h, g, seg=18, z0=z).transformed(M.scale(1, ry, 1))]
    for i in range(points):
        a = 2 * math.pi * (i + 0.5) / points
        x, y = r * 1.03 * math.cos(a), r * 1.03 * ry * math.sin(a)
        parts.append(M.cone(0.022, spike, g, seg=5, z0=0).transformed(M.translate(x, y, z + h - 0.005)))
        parts.append(M.sphere(0.012, g, (x, y, z + h + spike - 0.005), seg=5))
    if jewels:
        jm = mat(mats, jewels, '#c02838', spec=0.6, gloss=30)
        for a in (90, 30, 150, 210, 270, 330):
            ar = math.radians(a)
            parts.append(M.sphere(0.014, jm, (r * 1.05 * math.cos(ar), r * 1.05 * ry * math.sin(ar), z + h * 0.5), seg=6))
    if arches:
        cap = mat(mats, 'crown_cap', '#ffffff', tint=True)
        parts.append(M.ellipsoid(r * 0.95, r * 0.95 * ry, 0.07, cap, (0, 0, z + h), seg=14))
        parts.append(M.sphere(0.022, g, (0, 0, z + h + 0.075), seg=8))
        parts.append(M.box(0.012, 0.012, 0.06, g, (0, 0, z + h + 0.12)))
        parts.append(M.box(0.04, 0.012, 0.012, g, (0, 0, z + h + 0.125)))
    return M.Mesh.concat(parts)


def tall_crown(mats, m='gold', r=0.11, h=0.2, z=0.16, flare=1.15, top='dome', band=None):
    """A tall cylindrical crown/cap (Byzantine stemma, Sumerian, Babylonian polos)."""
    g = gold(mats) if m == 'gold' else mat(mats, m, '#d8b048', spec=0.6, gloss=24)
    parts = [M.loft([(z, r, r * 1.08), (z + h * 0.5, r * (1 + (flare - 1) * 0.6), r * 1.08 * (1 + (flare - 1) * 0.6)),
                     (z + h, r * flare, r * 1.08 * flare)], g, seg=18, cap_bottom=False, cap_top=top != 'open')]
    if top == 'dome':
        parts.append(M.ellipsoid(r * flare * 0.9, r * flare * 0.95, 0.05, g, (0, 0, z + h), seg=14))
    if band:
        b = mat(mats, band, '#ffffff', tint=band == 'crown_band')
        parts.append(M.cylinder(r * 1.04, r * 1.06, 0.04, b, seg=18, z0=z + 0.01).transformed(M.scale(1, 1.08, 1)))
        parts.append(M.cylinder(r * flare * 1.01, r * flare * 1.02, 0.03, b, seg=18, z0=z + h - 0.03).transformed(M.scale(1, 1.08, 1)))
    return M.Mesh.concat(parts)


def circlet(mats, m='gold', r=0.112, z=0.17, h=0.025):
    g = gold(mats) if m == 'gold' else mat(mats, m, '#d8b048', spec=0.6, gloss=24)
    return M.cylinder(r, r, h, g, seg=18, z0=z).transformed(M.scale(1, 1.1, 1))


def top_hat(mats, m='hat', band='hat_band'):
    hm = mat(mats, m, '#1e1e22', spec=0.25, gloss=16)
    parts = [M.cylinder(0.175, 0.175, 0.016, hm, seg=20, z0=0.2).transformed(M.scale(1, 1.1, 1)),
             M.cylinder(0.1, 0.108, 0.24, hm, seg=18, z0=0.205).transformed(M.scale(1, 1.1, 1))]
    if band:
        b = mat(mats, band, '#ffffff', tint=True)
        parts.append(M.cylinder(0.104, 0.106, 0.04, b, seg=18, z0=0.218).transformed(M.scale(1, 1.1, 1)))
    return M.Mesh.concat(parts)


def turban(mats, m='turban', big=1.0, jewel=None, plume=None, tall=0.0):
    t = mat(mats, m, '#ece8dc')
    parts = [M.ellipsoid(0.125 * big, 0.135 * big, 0.085 * big + tall * 0.5, t, (0, -0.005, 0.2 + tall * 0.3), seg=16)]
    for k in range(3):
        parts.append(torus(0.108 * big, 0.026 * big, t, seg=18, sides=6, z=0.16 + 0.035 * k * big, sy=1.08)
                     .transformed(M.rot_x(-6 + 6 * k)))
    if tall:
        parts.append(M.ellipsoid(0.09, 0.095, tall, t, (0, 0, 0.24 + tall * 0.6), seg=14))
    if jewel:
        parts.append(M.sphere(0.022, gold(mats), (0, 0.135 * big, 0.205), seg=7))
    if plume:
        pm = mat(mats, plume, '#ffffff')
        parts.append(M.capsule((0, 0.11, 0.22), (0, 0.06, 0.38 + tall), 0.024, 0.012, pm, seg=6, rings=1))
    return M.Mesh.concat(parts)


def nemes(mats, stripe_a='nemes_a', stripe_b='nemes_b'):
    """The Egyptian royal striped head cloth, with lappets in front of the
    shoulders and the uraeus at the brow."""
    a = mat(mats, stripe_a, '#ffffff', tint=True)
    b = mat(mats, stripe_b, '#d8b048', spec=0.5, gloss=20)
    def mf(i, ang):
        return a if i % 2 == 0 else b
    rings = [(0.08, 0.12, 0.125, 0, -0.01), (0.14, 0.118, 0.13, 0, -0.005), (0.2, 0.112, 0.128),
             (0.25, 0.09, 0.105), (0.28, 0.05, 0.06), (0.29, 0.005, 0.005)]
    parts = [arc_loft(rings, a, -60, 240, seg=18, mat_fn=mf)]
    # side lappets falling forward over the chest
    for s in (1, -1):
        pts = []
        for i in range(6):
            v = i / 5
            zc = 0.12 - v * 0.34
            x = s * (0.125 + 0.03 * v)
            pts.append([(x - s * 0.0, 0.02 + v * 0.06, zc), (x + s * 0.0, 0.10 + v * 0.06, zc)])
        pts = np.array(pts, np.float32)
        mfa = np.array([[a if i % 2 == 0 else b] for i in range(5)], np.int32)
        parts.append(sheet(pts, mfa, smooth=False))
    # the back tail
    parts.append(M.loft([(-0.12, 0.05, 0.03, 0, -0.1), (0.05, 0.1, 0.05, 0, -0.1), (0.15, 0.1, 0.06, 0, -0.07)], b, seg=10))
    # brow band and cobra
    g = gold(mats)
    parts.append(M.cylinder(0.123, 0.123, 0.025, g, seg=18, z0=0.15).transformed(M.scale(1, 1.06, 1)))
    parts.append(M.capsule((0, 0.13, 0.17), (0, 0.15, 0.24), 0.016, 0.012, g, seg=6, rings=1))
    return M.Mesh.concat(parts)


def bicorne(mats, m='hat', trim='gold', sideways=True):
    """A Napoleonic bicorne: a tall flattened half-moon worn sideways."""
    hm = mat(mats, m, '#1e1e22', spec=0.2)
    poly = [(-0.24, 0.0), (-0.16, 0.08), (0.0, 0.13), (0.16, 0.08), (0.24, 0.0), (0.0, 0.02)]
    body = M.extrude(poly, 0.12, hm)
    parts = [body.transformed(M.translate(0, -0.01, 0.2) @ (M.rot_z(0) if sideways else M.rot_z(90)))]
    parts.append(M.ellipsoid(0.105, 0.112, 0.06, hm, (0, 0, 0.2), seg=12))
    g = gold(mats) if trim == 'gold' else mat(mats, trim, '#ffffff', tint=True)
    parts.append(M.sphere(0.03, g, (0.0, 0.065, 0.27), seg=8))
    return M.Mesh.concat(parts)


def tricorne(mats, m='hat', trim='gold', wide=1.0):
    hm = mat(mats, m, '#1e1e22', spec=0.2)
    g = gold(mats) if trim == 'gold' else mat(mats, trim, '#d8b048')
    parts = [M.ellipsoid(0.11, 0.118, 0.09, hm, (0, 0, 0.21), seg=14)]
    # three upturned brims
    for a in (90, 210, 330):
        ar = math.radians(a)
        c = (0.15 * wide * math.cos(ar), 0.15 * wide * math.sin(ar), 0.22)
        brim = M.rounded_box(0.22 * wide, 0.02, 0.09, hm, (0, 0, 0), r=0.008, seg=8)
        parts.append(brim.transformed(M.translate(*c) @ M.rot_z(a - 90) @ M.rot_x(-20)))
        parts.append(M.rounded_box(0.22 * wide, 0.024, 0.014, g, (0, 0, 0.045), r=0.005, seg=6)
                     .transformed(M.translate(*c) @ M.rot_z(a - 90) @ M.rot_x(-20)))
    return M.Mesh.concat(parts)


def chaperon(mats, m='hat', trim='gold'):
    """Henry the Navigator's big round hat (a chaperon), dark with a gold edge."""
    hm = mat(mats, m, '#1e1e22', spec=0.15)
    g = gold(mats)
    parts = [M.ellipsoid(0.23, 0.24, 0.09, hm, (0, -0.01, 0.22), seg=18),
             M.ellipsoid(0.17, 0.18, 0.12, hm, (0, -0.02, 0.27), seg=14),
             torus(0.22, 0.025, g, seg=20, sides=6, z=0.2, sy=1.06)]
    return M.Mesh.concat(parts)


def pickelhaube(mats, m='helmet', trim='gold'):
    hm = mat(mats, m, '#2a2a2e', spec=0.6, gloss=24, metal=True)
    g = gold(mats)
    parts = [M.ellipsoid(0.118, 0.13, 0.12, hm, (0, 0.0, 0.17), seg=16)]
    parts.append(M.cylinder(0.122, 0.122, 0.02, hm, seg=16, z0=0.13).transformed(M.scale(1, 1.15, 1) @ M.translate(0, 0.01, 0)))
    parts.append(M.cylinder(0.02, 0.012, 0.03, g, seg=8, z0=0.28))
    parts.append(M.cone(0.018, 0.1, g, seg=8, z0=0.31))
    parts.append(M.extrude([(-0.04, 0.0), (0.04, 0.0), (0.05, 0.06), (0.0, 0.09), (-0.05, 0.06)], 0.01, g)
                 .transformed(M.translate(0, 0.115, 0.18) @ M.rot_x(-15)))
    return M.Mesh.concat(parts)


def peaked_cap(mats, m='cap', band='cap_band', visor='visor', badge=True, crown_r=0.135):
    """A military peaked (officer's) cap / Mao cap."""
    cm = mat(mats, m, '#3a4a32')
    bm = mat(mats, band, '#2a2a2a') if band else cm
    vm = mat(mats, visor, '#1a1a1a', spec=0.4, gloss=20)
    parts = [M.cylinder(0.112, 0.112, 0.06, bm, seg=18, z0=0.17).transformed(M.scale(1, 1.08, 1)),
             M.loft([(0.225, 0.112, 0.12), (0.27, crown_r, crown_r * 1.08), (0.29, crown_r * 0.98, crown_r * 1.06)], cm, seg=18)]
    parts.append(M.ellipsoid(0.1, 0.075, 0.012, vm, (0, 0.12, 0.175), seg=12).transformed(M.translate(0, 0, 0)))
    if badge:
        parts.append(M.sphere(0.018, gold(mats), (0, 0.128, 0.215), seg=6))
    return M.Mesh.concat(parts)


def beret(mats, m='hat', feather=None, tilt=-12):
    hm = mat(mats, m, '#1e1e22')
    parts = [M.ellipsoid(0.16, 0.16, 0.045, hm, (0, 0, 0.0), seg=16).transformed(M.translate(0.01, 0, 0.23) @ M.rot_y(tilt))]
    parts.append(M.cylinder(0.11, 0.11, 0.04, hm, seg=16, z0=0.18).transformed(M.scale(1, 1.08, 1)))
    if feather:
        f = mat(mats, feather, '#f2f0ea')
        parts.append(M.capsule((-0.08, -0.02, 0.25), (-0.2, -0.12, 0.36), 0.025, 0.008, f, seg=6, rings=1))
    return M.Mesh.concat(parts)


def fur_hat(mats, m='fur', top='hat_top'):
    """A Mongol hat: a fur rim with a tall pointed (civ-colored) crown."""
    fm = mat(mats, m, '#6a5a48')
    tm = mat(mats, top, '#ffffff', tint=True)
    parts = [torus(0.112, 0.04, fm, seg=18, sides=8, z=0.19, sy=1.08),
             M.loft([(0.18, 0.1, 0.108), (0.26, 0.095, 0.1), (0.33, 0.05, 0.05), (0.36, 0.01, 0.01)], tm, seg=14),
             M.sphere(0.02, gold(mats), (0, 0, 0.37), seg=6)]
    return M.Mesh.concat(parts)


def eboshi(mats, m='hat'):
    """A tall black lacquered court cap (Japanese), leaning back."""
    hm = mat(mats, m, '#18181c', spec=0.4, gloss=20)
    parts = [M.ellipsoid(0.1, 0.11, 0.07, hm, (0, -0.01, 0.21), seg=14),
             M.loft([(0.22, 0.06, 0.07), (0.32, 0.05, 0.06), (0.4, 0.035, 0.05)], hm, seg=12)
             .transformed(M.translate(0, -0.03, 0) @ M.rot_x(-14))]
    return M.Mesh.concat(parts)


def helmet_round(mats, m='helmet', nose=True, crest=None, wings=False):
    hm = mat(mats, m, '#9a9aa0', spec=0.7, gloss=24, metal=True)
    parts = [M.ellipsoid(0.118, 0.128, 0.13, hm, (0, 0.0, 0.14), seg=16)]
    parts.append(M.cylinder(0.12, 0.12, 0.03, hm, seg=16, z0=0.13).transformed(M.scale(1, 1.08, 1)))
    if nose:
        parts.append(M.rounded_box(0.022, 0.03, 0.09, hm, (0, 0.13, 0.11), r=0.008, seg=6))
    if crest:
        parts.append(M.extrude([(-0.1, 0.0), (0.12, 0.0), (0.1, 0.07), (0.0, 0.1), (-0.12, 0.05)], 0.03,
                               mat(mats, crest, '#ffffff', tint=True)).transformed(M.translate(0, 0, 0.22) @ M.rot_z(90)))
    if wings:
        w = mat(mats, 'wing', '#e8e4dc')
        for s in (1, -1):
            parts.append(M.extrude([(0, 0), (0.12, 0.04), (0.14, 0.12), (0.04, 0.06)], 0.01, w)
                         .transformed(M.translate(s * 0.11, 0, 0.2) @ M.scale(s, 1, 1) @ M.rot_z(-20)))
    return M.Mesh.concat(parts)


def feather_ring(mats, n=13, mats_cycle=('feather', 'feather_tip'), r=0.12, z=0.2, length=0.32, spread=(-110, 110),
                 back=0.0, width=0.05, tilt=18, droop=0.0):
    """A ring of feathers on a band: a war bonnet (spread around the back) or
    a frontal crest. Feathers fan outward from the band."""
    defaults = {'plume_r': '#c83a3a', 'plume_g': '#3a9a6a', 'feather_o': '#d06020', 'zfeather': '#d8d0b8',
                'gold': '#d8b048', 'feather': '#f0ece2', 'plume_w': '#f0ece2'}
    cols = [gold(mats) if c == 'gold' else mat(mats, c, defaults.get(c, '#ffffff'), tint=c in ('feather_t', 'plume_t'), spec=0.02)
            for c in mats_cycle]
    band = mat(mats, 'hband', '#ffffff', tint=True)
    parts = [M.cylinder(r * 0.98, r, 0.04, band, seg=18, z0=z - 0.03).transformed(M.scale(1, 1.08, 1))]
    a0, a1 = spread
    for i in range(n):
        a = a0 + (a1 - a0) * i / max(1, n - 1)   # angle around the head; 0 = back (-y)
        ar = math.radians(a)
        dirx, diry = math.sin(ar), -math.cos(ar)
        f = M.extrude([(-width * 0.4, 0), (width * 0.5, 0), (width * 0.55, length * 0.85), (0.0, length), (-width * 0.5, length * 0.8)],
                      0.008, cols[i % len(cols)])
        T = (M.translate(dirx * r, diry * r * 1.08, z) @ M.rot_z(math.degrees(math.atan2(diry, dirx)) - 90)
             @ M.rot_x(-tilt - back * abs(math.cos(ar))))
        V = f.V.copy()
        V[..., 1] -= droop * (V[..., 2] / length) ** 2
        f = M.Mesh(V, f.N, f.M)
        parts.append(f.transformed(T))
        # a dark tip
    return M.Mesh.concat(parts)


def war_bonnet(mats):
    """Hiawatha's great headdress: a fan of white feathers with civ-colored
    and orange tips around the head, rising high."""
    parts = [feather_ring(mats, n=17, mats_cycle=('feather', 'feather_t', 'feather_o'), r=0.12, z=0.2,
                          length=0.36, spread=(-165, 165), tilt=30, back=25, width=0.07)]
    return M.Mesh.concat(parts)


def plume_crest(mats, colors=('plume_t', 'plume_w'), n=9, length=0.42, width=0.07, spread=(-60, 60), gold_band=True,
                lean=-20, z=0.22):
    """A tall fan of plumes rising from a band (Aztec, Mayan headdresses)."""
    parts = [feather_ring(mats, n=n, mats_cycle=colors, r=0.11, z=z, length=length, spread=spread, tilt=lean,
                          width=width, droop=0.05)]
    if gold_band:
        parts.append(M.cylinder(0.118, 0.122, 0.06, gold(mats), seg=18, z0=0.15).transformed(M.scale(1, 1.08, 1)))
    return M.Mesh.concat(parts)


def zulu_headdress(mats, length=0.2, n=13, width=0.09, tilt=12, mane=None):
    """A fur band and a crown of feathers; `mane` (a length) adds a second,
    longer ring flaring outward (Shaka's great feather headdress)."""
    f = mat(mats, 'fur', '#b8a888')
    parts = [torus(0.118, 0.045, f, seg=18, sides=8, z=0.17, sy=1.08)]
    parts.append(feather_ring(mats, n=n, mats_cycle=('zfeather',), r=0.11, z=0.2, length=length, spread=(-170, 170),
                              tilt=tilt, width=width))
    if mane:
        parts.append(feather_ring(mats, n=n + 2, mats_cycle=('zfeather', 'zfeather2'), r=0.12, z=0.17, length=mane,
                                  spread=(-175, 175), tilt=tilt + 30, width=width * 1.2, droop=0.04))
    return M.Mesh.concat(parts)


def headband(mats, m='hband', r=0.115, z=0.155, h=0.035):
    b = mat(mats, m, '#ffffff', tint=True)
    return M.cylinder(r, r, h, b, seg=18, z0=z).transformed(M.scale(1, 1.08, 1))


def veil(mats, m='veil', length=0.7):
    """A cloth falling from the crown down the back (worn under a crown)."""
    v = mat(mats, m, '#f0f0f0')
    rings = [(0.22, 0.11, 0.12, 0, -0.01), (0.1, 0.14, 0.12, 0, -0.04), (-0.1, 0.17, 0.12, 0, -0.08),
             (-0.1 - length * 0.5, 0.2, 0.13, 0, -0.12), (-0.1 - length, 0.22, 0.14, 0, -0.14)]
    return arc_loft(rings, v, 180 + 30, 360 - 30, seg=14)


# ------------------------------------------------------------ hair and faces (head space, unscaled)

def hair_cap(mats, m='hair', scale=1.0):
    h = mat(mats, m, '#3a2a1c')
    return M.ellipsoid(0.106 * scale, 0.118 * scale, 0.12, h, (0, -0.006, 0.135), seg=14)


def hair_bun(mats, m='hair'):
    h = mat(mats, m, '#3a2a1c')
    return M.Mesh.concat([hair_cap(mats, m), M.sphere(0.06, h, (0, -0.1, 0.2), seg=10)])


def hair_long_curly(mats, m='hair', length=0.32, width=1.0):
    """Long full hair (wigs, curls) falling to the shoulders."""
    h = mat(mats, m, '#4a2e1c')
    parts = [M.ellipsoid(0.118 * width, 0.126, 0.13, h, (0, -0.012, 0.14), seg=14)]
    parts.append(M.loft([(0.12 - length, 0.13 * width, 0.08, 0, -0.05), (0.02, 0.135 * width, 0.1, 0, -0.04),
                         (0.14, 0.12 * width, 0.11, 0, -0.03)], h, seg=14))
    for s in (1, -1):
        for k in range(3):
            parts.append(M.sphere(0.05, h, (s * 0.12 * width, -0.02 - 0.02 * k, 0.1 - length * (0.3 + 0.3 * k)), seg=8))
    return M.Mesh.concat(parts)


def hair_wild(mats, m='hair'):
    """Einstein's white mane."""
    h = mat(mats, m, '#e8e8e8')
    parts = [M.ellipsoid(0.11, 0.12, 0.11, h, (0, -0.01, 0.15), seg=12)]
    for (x, y, z, r) in ((0.1, -0.03, 0.17, 0.07), (-0.1, -0.03, 0.17, 0.07), (0.0, -0.08, 0.2, 0.08),
                         (0.08, -0.08, 0.1, 0.06), (-0.08, -0.08, 0.1, 0.06), (0.05, 0.02, 0.24, 0.06),
                         (-0.05, 0.0, 0.25, 0.06), (0.11, 0.02, 0.09, 0.045), (-0.11, 0.02, 0.09, 0.045)):
        parts.append(M.sphere(r, h, (x, y, z), seg=8))
    return M.Mesh.concat(parts)


def hair_pompadour(mats, m='hair'):
    h = mat(mats, m, '#141418')
    parts = [M.ellipsoid(0.11, 0.122, 0.125, h, (0, -0.01, 0.14), seg=14),
             M.ellipsoid(0.09, 0.08, 0.06, h, (0, 0.06, 0.23), seg=12)]
    for s in (1, -1):
        parts.append(M.rounded_box(0.02, 0.04, 0.1, h, (s * 0.1, 0.04, 0.07), r=0.008, seg=6))
    return M.Mesh.concat(parts)


def hair_queue(mats, m='hair'):
    """Short hair tied back in a queue (18th century)."""
    h = mat(mats, m, '#e0dcd4')
    parts = [M.ellipsoid(0.11, 0.122, 0.125, h, (0, -0.01, 0.135), seg=14)]
    for s in (1, -1):
        parts.append(M.capsule((s * 0.1, 0.0, 0.14), (s * 0.105, -0.02, 0.08), 0.03, 0.03, h, seg=6, rings=1))
    parts.append(M.capsule((0, -0.11, 0.1), (0, -0.14, -0.02), 0.025, 0.02, h, seg=6, rings=1))
    return M.Mesh.concat(parts)


def beard(mats, kind='full', m='hair'):
    h = mat(mats, m, '#4a2e1c')
    if kind == 'full':
        return M.ellipsoid(0.085, 0.07, 0.07, h, (0, 0.05, 0.035), seg=12)
    if kind == 'long':   # an old sage's long beard down onto the chest
        return M.Mesh.concat([M.ellipsoid(0.085, 0.07, 0.07, h, (0, 0.05, 0.04), seg=12),
                              M.ellipsoid(0.065, 0.05, 0.11, h, (0, 0.07, -0.04), seg=12),
                              M.ellipsoid(0.03, 0.02, 0.012, h, (0.03, 0.12, 0.085), seg=6),
                              M.ellipsoid(0.03, 0.02, 0.012, h, (-0.03, 0.12, 0.085), seg=6)])
    if kind == 'square':   # Mesopotamian: long, square, curled
        return M.Mesh.concat([M.ellipsoid(0.085, 0.07, 0.07, h, (0, 0.05, 0.04), seg=12),
                              M.rounded_box(0.13, 0.08, 0.2, h, (0, 0.07, -0.04), r=0.03, seg=10)])
    if kind == 'chin':     # Lincoln's chin curtain
        return M.Mesh.concat([M.ellipsoid(0.08, 0.065, 0.05, h, (0, 0.06, 0.02), seg=12)]
                             + [M.rounded_box(0.02, 0.05, 0.08, h, (s * 0.088, 0.04, 0.07), r=0.008, seg=6) for s in (1, -1)])
    if kind == 'mustache':
        return M.Mesh.concat([M.capsule((-0.05, 0.115, 0.075), (0.0, 0.128, 0.083), 0.014, 0.012, h, seg=6, rings=1),
                              M.capsule((0.05, 0.115, 0.075), (0.0, 0.128, 0.083), 0.014, 0.012, h, seg=6, rings=1)])
    if kind == 'big_mustache':
        return M.Mesh.concat([M.capsule((-0.075, 0.1, 0.06), (0.0, 0.13, 0.083), 0.018, 0.014, h, seg=6, rings=1),
                              M.capsule((0.075, 0.1, 0.06), (0.0, 0.13, 0.083), 0.018, 0.014, h, seg=6, rings=1)])
    if kind == 'goatee':
        return M.Mesh.concat([M.ellipsoid(0.04, 0.04, 0.05, h, (0, 0.085, 0.025), seg=8),
                              beard(mats, 'mustache', m)])
    raise ValueError(kind)


def glasses(mats, dark=False):
    g = mat(mats, 'glasses', '#101012' if dark else '#2a2a2a', spec=0.6, gloss=30)
    if dark:
        return M.Mesh.concat([M.rounded_box(0.06, 0.02, 0.04, g, (s * 0.04, 0.12, 0.125), r=0.01, seg=6) for s in (1, -1)])
    return M.Mesh.concat([torus(0.022, 0.005, g, seg=10, sides=4).transformed(M.translate(s * 0.04, 0.125, 0.125) @ M.rot_x(90))
                          for s in (1, -1)])


def eyepatch(mats):
    g = mat(mats, 'patch', '#141414')
    return M.Mesh.concat([M.ellipsoid(0.03, 0.012, 0.025, g, (0.04, 0.112, 0.125), seg=8),
                          torus(0.104, 0.006, g, seg=18, sides=4, z=0.15, sy=1.1).transformed(M.rot_y(-12))])


def topknot(mats, m='hair'):
    h = mat(mats, m, '#141010')
    g = gold(mats)
    return M.Mesh.concat([M.ellipsoid(0.1, 0.112, 0.105, h, (0, -0.01, 0.14), seg=12),
                          M.cylinder(0.035, 0.03, 0.07, h, seg=8, z0=0.23),
                          M.cylinder(0.04, 0.04, 0.02, g, seg=8, z0=0.24)])


# ------------------------------------------------------------ worn on the chest (chest space)

def sash_band(mats, m='sash_w', width=0.11, tilt=38, rx=0.25, ry=0.165, z=0.13, edge=None):
    """A wide sash across the torso from the right shoulder to the left hip
    (chest space; rx, ry: the torso's half widths there)."""
    sm = mat(mats, m, '#ffffff', tint=True)
    rings = [(-width / 2, rx, ry), (width / 2, rx, ry)]
    parts = [M.loft(rings, sm, seg=20, cap_bottom=False, cap_top=False)]
    if edge:
        em = gold(mats) if edge == 'gold' else mat(mats, edge, '#d8b048')
        for zz in (-width / 2, width / 2):
            parts.append(torus(rx, 0.008, em, seg=20, sides=4, z=zz, sy=ry / rx))
    return M.Mesh.concat(parts).transformed(M.translate(0, 0, z) @ M.rot_y(tilt))


def kabuto(mats, m='helm', crest='gold'):
    """A samurai helmet: a dome, a flaring neck guard and gold horns."""
    hm = mat(mats, m, '#ffffff', tint=True)
    g = gold(mats)
    from models.humanoid import _dome
    parts = [_dome(0.122, 0.132, 0.13, hm, (0, -0.005, 0.12), 0.03),
             arc_loft([(0.04, 0.19, 0.19, 0, -0.03), (0.1, 0.15, 0.15, 0, -0.015), (0.15, 0.123, 0.133)], hm,
                      165, 375, seg=12),
             torus(0.125, 0.012, g, seg=18, sides=4, z=0.15, sy=1.07)]
    for s in (1, -1):
        parts.append(M.extrude([(0.0, 0.0), (0.03, 0.0), (0.09, 0.2), (0.07, 0.21)], 0.012, g)
                     .transformed(M.translate(s * 0.02, 0.12, 0.2) @ M.scale(s, 1, 1)))
    parts.append(M.sphere(0.025, g, (0, 0.125, 0.21), seg=6))
    return M.Mesh.concat(parts)


def fur_band(mats, m='furc', z=-0.06):
    """A fur tuft below the knee (shin space)."""
    f = mat(mats, m, '#c8b090')
    return M.loft([(z - 0.12, 0.075, 0.075), (z - 0.04, 0.09, 0.09), (z, 0.07, 0.07)], f, seg=10)


def kataginu(mats, m='kata', width=0.36):
    """The stiff winged shoulders of a kamishimo (chest space)."""
    k = mat(mats, m, '#ffffff', tint=True)
    parts = []
    for s in (1, -1):
        poly = [(0.0, 0.0), (width, 0.02), (width + 0.02, 0.05), (0.0, 0.06)]
        w = M.extrude(poly, 0.26, k)
        parts.append(w.transformed(M.translate(0, -0.01, 0.24) @ M.scale(s, 1, 1)))
    return M.Mesh.concat(parts)


def ruff(mats, m='ruff', r=0.17, z=0.33):
    """An Elizabethan ruff: a big pleated disc around the neck."""
    w = mat(mats, m, '#f4f2ec')
    prof = []
    k = 24
    pts = []
    for i in range(3):
        rr = 0.06 + (r - 0.06) * i / 2
        ring = []
        for j in range(k + 1):
            a = 2 * math.pi * j / k
            wob = 0.012 * (1 if j % 2 else -1) * (i / 2)
            ring.append(((rr + wob) * math.cos(a), (rr + wob) * 1.05 * math.sin(a), z - 0.01 * i + (0.03 if i == 2 else 0) * max(0, -math.sin(a))))
        pts.append(ring)
    top = sheet(np.array(pts), w)
    pts2 = np.array(pts)
    pts2[..., 2] -= 0.04
    return M.Mesh.concat([top, sheet(pts2, w), arc_loft([(z - 0.04 - 0.0, r, r * 1.05), (z - 0.0, r, r * 1.05)], w, seg=k)])


def broad_collar(mats, m='gold', r0=0.08, r1=0.24, z=0.29, mats2=None):
    """A broad collar on the shoulders (Egyptian usekh, Aztec gold pectoral)."""
    g = gold(mats) if m == 'gold' else mat(mats, m, '#d8b048')
    b2 = mat(mats, mats2, '#ffffff', tint=True) if mats2 else g
    k = 24
    pts = []
    for i in range(4):
        rr = r0 + (r1 - r0) * i / 3
        ring = []
        for j in range(k + 1):
            a = 2 * math.pi * j / k
            zz = z + 0.03 - (0.10 * (i / 3) ** 1.5)
            ring.append((rr * math.cos(a), rr * 0.75 * math.sin(a) + 0.0, zz))
        pts.append(ring)
    mf = np.array([[g if i % 2 == 0 else b2] * k for i in range(3)], np.int32)
    return sheet(np.array(pts), mf)


def epaulette(mats, m='gold', fringe=True):
    """A shoulder board (arm bone space at the shoulder joint)."""
    g = gold(mats) if m == 'gold' else mat(mats, m, '#d8b048')
    parts = [M.ellipsoid(0.085, 0.09, 0.03, g, (0.01, 0, 0.05), seg=10)]
    if fringe:
        parts.append(M.cylinder(0.09, 0.095, 0.05, g, seg=12, z0=0.0).transformed(M.translate(0.02, 0, -0.01) @ M.scale(1, 1, 1)))
    return M.Mesh.concat(parts)


def pauldron(mats, m='armor', r=0.11):
    a = mat(mats, m, '#a8acb4', spec=0.8, gloss=26, metal=True)
    return M.Mesh.concat([M.ellipsoid(r, r * 1.05, r * 0.8, a, (0.02, 0, 0.0), seg=12),
                          M.ellipsoid(r * 1.02, r * 1.07, r * 0.4, a, (0.03, 0, -0.07), seg=12)])


# ------------------------------------------------------------ companions and the relic

def dog(mats, phase=0.0, sit=0.0, look=0.0):
    """A big grey hound standing (Charles V's dog), in its own space: facing +y,
    standing on z = 0, ~0.75 m at the shoulder. phase: idle breathing/tail."""
    fur = mat(mats, 'dog', '#8a8a88', spec=0.08)
    dark = mat(mats, 'dog_dark', '#4a4a4a', spec=0.05)
    collar = mat(mats, 'dog_collar', '#ffffff', tint=True)
    s = math.sin(2 * math.pi * phase)
    parts = []
    # body
    parts.append(M.ellipsoid(0.13, 0.36, 0.15, fur, (0, 0.0, 0.62 + 0.005 * s), seg=14))
    parts.append(M.ellipsoid(0.12, 0.16, 0.16, fur, (0, 0.22, 0.65 + 0.005 * s), seg=12))   # chest
    # neck and head
    hx = 0.0
    neck_top = np.array([hx, 0.42, 0.92])
    parts.append(M.capsule((0, 0.28, 0.7), tuple(neck_top), 0.08, 0.06, fur, seg=10, rings=2))
    head = M.Mesh.concat([M.ellipsoid(0.075, 0.1, 0.075, fur, (0, 0.02, 0.0), seg=12),
                          M.capsule((0, 0.06, -0.01), (0, 0.2, -0.04), 0.05, 0.035, fur, seg=10, rings=2),
                          M.sphere(0.022, dark, (0, 0.21, -0.03), seg=6)]
                         + [M.ellipsoid(0.02, 0.04, 0.06, dark, (sx * 0.055, -0.02, 0.05), seg=6).transformed(M.rot_y(sx * 25))
                            for sx in (1, -1)])
    parts.append(head.transformed(M.translate(*neck_top) @ M.rot_z(look) @ M.rot_x(-10)))
    parts.append(torus(0.075, 0.015, collar, seg=14, sides=4).transformed(M.translate(0, 0.37, 0.84) @ M.rot_x(-50)))
    # legs
    for x in (0.08, -0.08):
        for y, zt in ((0.24, 0.6), (-0.24, 0.6)):
            parts.append(M.capsule((x, y, zt), (x, y + 0.02, 0.07), 0.045, 0.028, fur, seg=8, rings=2))
            parts.append(M.ellipsoid(0.035, 0.055, 0.03, fur, (x, y + 0.05, 0.03), seg=8))
    # tail
    tw = 0.1 * s
    parts.append(M.capsule((0, -0.34, 0.68), (tw, -0.56, 0.5), 0.03, 0.015, fur, seg=6, rings=1))
    return M.Mesh.concat(parts)


def ark(mats):
    """The Holy Relic: a gilded chest with two kneeling winged figures on its
    lid, carried on two poles along its length (y). Origin at its bottom
    center; the poles at z = pole_z."""
    g = gold(mats)
    g2 = mat(mats, 'gold_dark', '#a8781c', spec=0.7, gloss=20, metal=True)
    glow = mat(mats, 'relic_glow', '#ffe890', spec=0.0)
    L, Wd, Hh = 1.3, 0.62, 0.66
    parts = []
    # the chest with corner posts, panels and a rim
    parts.append(M.rounded_box(Wd, L, Hh, g2, (0, 0, Hh / 2 + 0.08), r=0.02, seg=10))
    # golden grille panels (bars) on the sides
    for sx in (1, -1):
        for k in range(7):
            y = -L / 2 + 0.1 + k * (L - 0.2) / 6
            parts.append(M.cylinder(0.022, 0.022, Hh - 0.1, g, seg=6, z0=0.13).transformed(M.translate(sx * (Wd / 2 + 0.01), y, 0)))
        parts.append(M.box(0.03, L, 0.05, g, (sx * (Wd / 2 + 0.015), 0, 0.13)))
        parts.append(M.box(0.03, L, 0.05, g, (sx * (Wd / 2 + 0.015), 0, Hh + 0.05)))
    for sy in (1, -1):
        for k in range(4):
            x = -Wd / 2 + 0.1 + k * (Wd - 0.2) / 3
            parts.append(M.cylinder(0.022, 0.022, Hh - 0.1, g, seg=6, z0=0.13).transformed(M.translate(x, sy * (L / 2 + 0.01), 0)))
    # inner glow showing between the bars
    parts.append(M.box(Wd - 0.02, L - 0.02, Hh - 0.14, glow, (0, 0, Hh / 2 + 0.1)))
    # corner posts and feet
    for sx in (1, -1):
        for sy in (1, -1):
            parts.append(M.rounded_box(0.07, 0.07, Hh + 0.12, g, (sx * Wd / 2, sy * L / 2, (Hh + 0.12) / 2 + 0.02), r=0.015, seg=8))
            parts.append(M.sphere(0.05, g, (sx * Wd / 2, sy * L / 2, Hh + 0.16), seg=8))
    # the lid (mercy seat) with a rim
    parts.append(M.rounded_box(Wd + 0.1, L + 0.1, 0.06, g, (0, 0, Hh + 0.11), r=0.02, seg=10))
    # two kneeling cherubim facing each other, their wings arching up over the lid toward each other
    for sy in (1, -1):
        c = (0, sy * (L / 2 - 0.16), Hh + 0.14)
        fig = [M.ellipsoid(0.09, 0.085, 0.15, g, (0, 0, 0.13), seg=10),
               M.sphere(0.065, g, (0, -sy * 0.03, 0.32), seg=8)]
        poly = [(-0.05, 0.0), (0.08, 0.08), (0.24, 0.36), (0.44, 0.5), (0.4, 0.57), (0.14, 0.5), (-0.08, 0.28)]
        for sx in (1, -1):
            w = M.extrude(poly, 0.02, g).transformed(M.rot_z(90))   # poly x -> model y
            w = w.transformed(M.translate(sx * 0.1, 0, 0.12) @ M.scale(1, -sy, 1) @ M.rot_y(sx * -10))
            fig.append(w)
        parts.append(M.Mesh.concat(fig).transformed(M.translate(*c)))
    # rings for the poles
    return M.Mesh.concat(parts)
