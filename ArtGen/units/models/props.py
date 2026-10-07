"""Hand-held props and gear shared by the archetypes.

Every function takes the material table and returns a Mesh in prop space:
held props have their grip at the origin and their long axis along +z (the
business end up); a blade's edge faces +y. Gear worn on the body (quiver,
backpack) is in the space of the bone it is attached to.

Materials are looked up by name and created with default colors if missing,
so a catalog entry can recolor any of them (see humanoid.DEFAULT_COLORS).
"""
import math

import numpy as np

import mesh as M


def mat(mats, name, color, **kw):
    return mats[name] if name in mats else mats.add(name, color, **kw)


def wood(mats):
    return mat(mats, 'wood', '#7a5434', spec=0.06)


def steel(mats):
    return mat(mats, 'steel', '#b8bcc2', spec=0.9, gloss=30, metal=True)


def stone(mats):
    return mat(mats, 'stone', '#8e8a86', spec=0.05)


def leather(mats):
    return mat(mats, 'leather', '#5c3a24', spec=0.08)


def bronze(mats):
    return mat(mats, 'bronze', '#c09040', spec=0.8, gloss=24, metal=True)


def rope(mats):
    return mat(mats, 'rope', '#b89a68', spec=0.03)


def haft(mats, z0, z1, r=0.018, m=None, seg=8):
    return M.cylinder(r, r * 0.9, z1 - z0, wood(mats) if m is None else m, seg=seg, z0=z0)


# ------------------------------------------------------------ weapons

def stone_axe(mats, length=0.72):
    """A warrior's hafted stone axe: wooden handle, knapped stone head lashed
    near the top, the edge facing +y."""
    parts = [haft(mats, -0.12, length, 0.019)]
    head = M.extrude([(-0.03, -0.07), (0.04, -0.095), (0.19, -0.09), (0.215, 0.0),
                      (0.19, 0.09), (0.04, 0.095), (-0.03, 0.07)], 0.05, stone(mats))
    # extrude makes the shape in the x-z plane; turn it so the edge faces +y
    parts.append(head.transformed(M.translate(0, 0, length - 0.09) @ M.rot_z(90)))
    parts.append(M.cylinder(0.026, 0.026, 0.08, rope(mats), seg=8, z0=length - 0.13))
    return M.Mesh.concat(parts)


def axe(mats, length=0.7):
    """An iron axe with a crescent blade."""
    parts = [haft(mats, -0.12, length, 0.018)]
    head = M.extrude([(-0.02, -0.03), (0.06, -0.04), (0.16, -0.10), (0.19, 0.0),
                      (0.16, 0.10), (0.06, 0.04), (-0.02, 0.03)], 0.02, steel(mats))
    parts.append(head.transformed(M.translate(0, 0, length - 0.08) @ M.rot_z(90)))
    return M.Mesh.concat(parts)


def club(mats, length=0.6):
    parts = [M.cylinder(0.022, 0.05, length, wood(mats), seg=9, z0=-0.1)]
    parts.append(M.ellipsoid(0.055, 0.055, 0.08, wood(mats), (0, 0, length - 0.1), seg=9))
    return M.Mesh.concat(parts)


def spear(mats, length=2.1, grip=0.75, tip='leaf'):
    """A long spear held `grip` meters from its butt; steel leaf tip on top."""
    z0, z1 = -grip, length - grip
    parts = [haft(mats, z0, z1 - 0.18, 0.017)]
    s = steel(mats)
    tipm = M.lathe([(0, 0.022), (0.02, 0.028), (0.08, 0.045), (0.16, 0.025), (0.24, 0.0005)], s, seg=8)
    tipm = tipm.transformed(M.translate(0, 0, z1 - 0.22) @ M.scale(1.0, 0.45, 1.0))
    parts.append(tipm)
    parts.append(M.cylinder(0.022, 0.02, 0.05, s, seg=8, z0=z1 - 0.24))
    parts.append(M.cylinder(0.02, 0.016, 0.06, s, seg=8, z0=z0))
    return M.Mesh.concat(parts)


def sword(mats, length=0.62, width=0.065, guard=0.16, blade_mat=None):
    """A short sword: leather grip, crossguard, pommel, steel blade along +z
    with the flat facing x (edges toward +y/-y)."""
    s = steel(mats) if blade_mat is None else blade_mat
    parts = [M.cylinder(0.02, 0.02, 0.13, leather(mats), seg=8, z0=-0.07)]
    parts.append(M.sphere(0.03, bronze(mats), (0, 0, -0.08), seg=8))
    parts.append(M.rounded_box(0.035, guard, 0.03, bronze(mats), (0, 0, 0.07), r=0.01, seg=8))
    b = M.blade(length, width, 0.014, s, tip=0.18, z0=0.08)
    parts.append(b.transformed(M.rot_z(90)))
    return M.Mesh.concat(parts)


def bow(mats, height=1.25, draw=0.0, bend=0.13, string=True):
    """A bow held at its grip: limbs up and down along z, curving back toward
    the archer (-y). `draw` (0..1) pulls the string back (to -y) and bends the
    limbs a bit more."""
    w = wood(mats)
    n = 9
    pts = []
    bend = bend + 0.08 * draw
    for i in range(n):
        u = -1 + 2 * i / (n - 1)
        z = u * height / 2 * (1 - 0.06 * draw)
        y = -bend * u * u
        pts.append((0, y, z))
    parts = []
    for a, b in zip(pts[:-1], pts[1:]):
        r = 0.022 - 0.01 * abs(a[2]) / (height / 2)
        parts.append(M.capsule(a, b, r, r * 0.9, w, seg=7, rings=1))
    parts.append(M.cylinder(0.026, 0.026, 0.12, leather(mats), seg=8, z0=-0.06))
    if string:
        sm = mat(mats, 'string', '#e8e0c8', spec=0.0)
        top, bot = pts[-1], pts[0]
        mid = (0, top[1] - 0.05 - 0.55 * draw, 0)
        parts.append(M.capsule(bot, mid, 0.005, 0.005, sm, seg=4, rings=1))
        parts.append(M.capsule(mid, top, 0.005, 0.005, sm, seg=4, rings=1))
    return M.Mesh.concat(parts)


def arrow(mats, length=0.75):
    """An arrow along +z, its nock at the origin."""
    parts = [M.cylinder(0.007, 0.007, length - 0.05, wood(mats), seg=5)]
    parts.append(M.cone(0.016, 0.06, steel(mats), seg=6, z0=length - 0.05))
    f = mat(mats, 'fletch', '#e8e2d0', spec=0.0)
    for a in (0, 120, 240):
        fl = M.extrude([(0.0, 0.0), (0.035, 0.03), (0.035, 0.12), (0.0, 0.11)], 0.004, f)
        parts.append(fl.transformed(M.rot_z(a) @ M.translate(0, 0, 0.02)))
    return M.Mesh.concat(parts)


# ------------------------------------------------------------ shields

def shield_oval(mats, w=0.58, h=0.96, face='shield_face', emblem='shield_emblem', dome=0.06):
    """A big oval shield facing -y (its front), centered at the origin, with a
    civ-colored cross emblem and a boss (like Civ3's Spearman)."""
    fm = mat(mats, face, '#d2bf92', spec=0.05)
    rimm = mat(mats, 'shield_rim', '#6a4a2c', spec=0.1)
    em = mat(mats, emblem, '#ffffff', tint=True, spec=0.05)
    parts = [M.disc(w / 2, h / 2, 0.025, fm, seg=24, dome=dome)]
    # rim: a ring of capsules around the edge
    ring = []
    k = 20
    for i in range(k):
        a0, a1 = 2 * math.pi * i / k, 2 * math.pi * (i + 1) / k
        p0 = (w / 2 * math.cos(a0), 0.0, h / 2 * math.sin(a0))
        p1 = (w / 2 * math.cos(a1), 0.0, h / 2 * math.sin(a1))
        ring.append(M.capsule(p0, p1, 0.016, 0.016, rimm, seg=5, rings=1))
    parts += ring
    # the emblem: two crossing bands that follow the dome
    for ang in (45, -45):
        L = 0.5 * math.hypot(w * math.cos(math.radians(ang)), h * math.sin(math.radians(ang))) * 0.92
        band = _domed_band(L * 1.06, 0.10, w, h, dome, em)
        parts.append(band.transformed(M.rot_y(ang)))
    parts.append(M.ellipsoid(0.06, 0.04, 0.06, mat(mats, 'bronze', '#c09040', spec=0.8, gloss=24, metal=True),
                             (0, -dome - 0.02, 0), seg=10))
    return M.Mesh.concat(parts)


def _domed_band(half_len, half_w, w, h, dome, m, n=10):
    """A thin strip along x lying on the front of a domed disc."""
    tris, norms = [], []
    xs = np.linspace(-half_len, half_len, n)
    def surf(x, z):
        s2 = (x / (w / 2)) ** 2 + (z / (h / 2)) ** 2
        return -0.0125 - dome * max(0.0, 1 - s2) - 0.006
    for a, b in zip(xs[:-1], xs[1:]):
        q = [(a, surf(a, -half_w), -half_w), (b, surf(b, -half_w), -half_w),
             (b, surf(b, half_w), half_w), (a, surf(a, half_w), half_w)]
        tris += [[q[0], q[1], q[2]], [q[0], q[2], q[3]]]
        norms += [[(0, -1, 0)] * 3] * 2
    mm = M.Mesh(np.array(tris, np.float32), np.array(norms, np.float32), np.full(len(tris), m, np.int32))
    return mm


def shield_round(mats, r=0.24, face='shield_face', boss=True, dome=0.04):
    fm = mat(mats, face, '#8a5a32', spec=0.08)
    parts = [M.disc(r, r, 0.025, fm, seg=22, dome=dome)]
    rimm = mat(mats, 'shield_rim', '#6a4a2c', spec=0.1)
    k = 18
    for i in range(k):
        a0, a1 = 2 * math.pi * i / k, 2 * math.pi * (i + 1) / k
        parts.append(M.capsule((r * math.cos(a0), 0, r * math.sin(a0)), (r * math.cos(a1), 0, r * math.sin(a1)),
                               0.014, 0.014, rimm, seg=5, rings=1))
    if boss:
        parts.append(M.ellipsoid(0.07, 0.05, 0.07, bronze(mats), (0, -dome - 0.015, 0), seg=10))
    return M.Mesh.concat(parts)


# ------------------------------------------------------------ tools

def staff(mats, length=1.9, grip=1.05):
    return M.Mesh.concat([haft(mats, -grip, length - grip, 0.022, seg=8),
                          M.sphere(0.03, wood(mats), (0, 0, length - grip), seg=8)])


def shovel(mats, length=1.1, grip=0.0):
    """A shovel: shaft along +z from the grip, the spade at the far end
    (+z), facing +y."""
    parts = [haft(mats, -0.15, length - 0.25, 0.018)]
    blade = M.extrude([(-0.11, 0.0), (0.11, 0.0), (0.12, 0.18), (0.0, 0.28), (-0.12, 0.18)], 0.02, steel(mats))
    parts.append(blade.transformed(M.translate(0, 0, length - 0.28)))
    parts.append(M.cylinder(0.026, 0.02, 0.08, steel(mats), seg=8, z0=length - 0.32))
    return M.Mesh.concat(parts)


def pickaxe(mats, length=0.85):
    parts = [haft(mats, -0.15, length, 0.02)]
    head = M.extrude([(-0.03, -0.32), (0.02, -0.18), (0.035, 0.0), (0.02, 0.18), (-0.03, 0.32),
                      (-0.02, 0.0)], 0.035, steel(mats))
    parts.append(head.transformed(M.translate(0, 0, length - 0.02) @ M.rot_z(90) @ M.rot_y(90)))
    return M.Mesh.concat(parts)


def hoe(mats, length=1.2):
    parts = [haft(mats, -0.2, length, 0.018)]
    blade = M.box(0.16, 0.14, 0.012, steel(mats), (0, 0.06, length - 0.01))
    parts.append(blade)
    return M.Mesh.concat(parts)


def hammer(mats, length=0.45):
    parts = [haft(mats, -0.08, length, 0.017)]
    parts.append(M.rounded_box(0.07, 0.16, 0.07, steel(mats), (0, 0, length), r=0.012, seg=8))
    return M.Mesh.concat(parts)


def machete(mats, length=0.5):
    parts = [M.cylinder(0.02, 0.02, 0.12, wood(mats), seg=8, z0=-0.06)]
    b = M.extrude([(-0.015, 0.06), (0.03, 0.06), (0.05, length - 0.1), (0.02, length), (-0.02, length - 0.05),
                   (-0.02, 0.06)], 0.01, steel(mats))
    parts.append(b.transformed(M.rot_z(90)))
    return M.Mesh.concat(parts)


def seed_bag(mats):
    c = mat(mats, 'sack', '#b8a072', spec=0.02)
    return M.Mesh.concat([M.ellipsoid(0.11, 0.08, 0.12, c, (0, 0, -0.1), seg=10),
                          M.cylinder(0.04, 0.05, 0.05, c, seg=8, z0=-0.0)])


# ------------------------------------------------------------ worn gear

def quiver(mats, arrows=6):
    """A quiver for the back (chest bone space): slung from the lower back up
    over the right shoulder, its arrows sticking out above."""
    lm = leather(mats)
    q = M.cylinder(0.055, 0.065, 0.55, lm, seg=10, z0=0.0)
    parts = [q, M.cylinder(0.068, 0.068, 0.04, mat(mats, 'leather_dark', '#3e2616'), seg=10, z0=0.5)]
    f = mat(mats, 'fletch', '#e8e2d0', spec=0.0)
    for i in range(arrows):
        a = 2 * math.pi * i / arrows
        x, y = 0.03 * math.cos(a), 0.03 * math.sin(a)
        parts.append(M.cylinder(0.006, 0.006, 0.12, wood(mats), seg=4, z0=0.5).transformed(M.translate(x, y, 0)))
        parts.append(M.box(0.008, 0.035, 0.08, f, (x, y, 0.64)))
    m = M.Mesh.concat(parts)
    return m.transformed(M.translate(0.06, -0.15, -0.25) @ M.rot_y(-28))


def backpack(mats, size=1.0):
    """A settler's big bundle (chest bone space): a sack with a bedroll on top
    and a pot and tool handles sticking out."""
    s = size
    sack = mat(mats, 'pack', '#7a6a40', spec=0.03)
    roll = mat(mats, 'bedroll', '#a89060', spec=0.02)
    parts = [M.rounded_box(0.34 * s, 0.22 * s, 0.44 * s, sack, (0, -0.24 * s, 0.08 * s), r=0.07, seg=14, p=3.5)]
    bed = M.cylinder(0.075 * s, 0.075 * s, 0.42 * s, roll, seg=12).transformed(
        M.translate(-0.21 * s, -0.24 * s, 0.36 * s) @ M.rot_y(90))
    parts.append(bed)
    parts.append(M.lathe([(0, 0.0005), (0.0, 0.07), (0.06, 0.085), (0.12, 0.06), (0.14, 0.065)],
                         mat(mats, 'pot', '#5a5650', spec=0.4, gloss=18, metal=True), seg=10)
                 .transformed(M.translate(0.13 * s, -0.33 * s, 0.22 * s) @ M.rot_x(-25)))
    parts.append(haft(mats, 0.0, 0.5 * s, 0.016).transformed(M.translate(-0.1 * s, -0.2 * s, 0.1 * s) @ M.rot_y(18)))
    parts.append(M.box(0.12, 0.02, 0.1, steel(mats), (0, 0, 0.5 * s)).transformed(
        M.translate(-0.1 * s, -0.2 * s, 0.1 * s) @ M.rot_y(18)))
    # straps over the shoulders
    st = leather(mats)
    for x in (-0.11, 0.11):
        parts.append(M.capsule((x, -0.1, 0.27), (x, 0.11, 0.2), 0.018, 0.018, st, seg=5, rings=1))
        parts.append(M.capsule((x, 0.11, 0.2), (x * 1.1, 0.12, 0.0), 0.016, 0.016, st, seg=5, rings=1))
    return M.Mesh.concat(parts)
