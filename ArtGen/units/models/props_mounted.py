"""Props and worn gear for the mounted units (riders, charioteers, elephant
crews): cavalry weapons and headgear, horse tack. Same conventions as
props.py: held props have their grip at the origin and their long axis +z;
head gear is in the humanoid's head bone space (before its HEAD scale).
"""
import math

import numpy as np

import mesh as M
from models.props import mat, wood, steel, leather, bronze, haft


def tube_y(rings, m, seg=12, cap0=True, cap1=True):
    """A smooth tube along +y from rings (y, rx, rz[, zc]): rx the half-width,
    rz the half-height, zc the height of the center."""
    lr = [(r[0], r[1], r[2], 0.0, -(r[3] if len(r) > 3 else 0.0)) for r in rings]
    return M.loft(lr, m, seg, cap0, cap1).transformed(M.rot_x(-90))


def lance(mats, length=3.0, grip=0.9, pennant=False):
    """A long cavalry lance: a slim tapering shaft with a steel point."""
    z0, z1 = -grip, length - grip
    parts = [M.cylinder(0.024, 0.014, z1 - z0 - 0.2, wood(mats), seg=8, z0=z0)]
    s = steel(mats)
    tip = M.lathe([(0, 0.018), (0.03, 0.03), (0.12, 0.034), (0.26, 0.0005)], s, seg=8)
    parts.append(tip.transformed(M.translate(0, 0, z1 - 0.26) @ M.scale(1.0, 0.5, 1.0)))
    # a vamplate (hand guard) just in front of the grip
    parts.append(M.lathe([(0.02, 0.02), (0.06, 0.06), (0.10, 0.025)], s, seg=10))
    if pennant:
        pm = mat(mats, 'pennant', '#ffffff', tint=True)
        parts.append(M.extrude([(0.0, 0.0), (0.0, 0.22), (-0.34, 0.11)], 0.008, pm)
                     .transformed(M.translate(0, 0, z1 - 0.55) @ M.rot_z(90)))
    return M.Mesh.concat(parts)


def cav_spear(mats, length=2.5, grip=1.0):
    from models.props import spear
    return spear(mats, length=length, grip=grip)


def javelin(mats, length=1.7, grip=0.7):
    z0, z1 = -grip, length - grip
    parts = [haft(mats, z0, z1 - 0.14, 0.015)]
    tipm = M.lathe([(0, 0.016), (0.04, 0.03), (0.16, 0.0005)], steel(mats), seg=8)
    parts.append(tipm.transformed(M.translate(0, 0, z1 - 0.16) @ M.scale(1.0, 0.45, 1.0)))
    return M.Mesh.concat(parts)


def sabre(mats, length=0.82, curve=0.10, width=0.045, guard='bow'):
    """A curved cavalry sabre: the blade sweeps back (-y) toward its tip, its
    edge facing +y."""
    s = steel(mats)
    parts = [M.cylinder(0.019, 0.019, 0.13, leather(mats), seg=8, z0=-0.07)]
    b = bronze(mats)
    parts.append(M.sphere(0.026, b, (0, 0, -0.08), seg=8))
    parts.append(M.rounded_box(0.03, 0.12, 0.026, b, (0, 0.0, 0.07), r=0.01, seg=8))
    if guard == 'bow':   # a knuckle bow from the guard to the pommel
        parts.append(M.capsule((0, 0.06, 0.07), (0, 0.07, -0.05), 0.008, 0.008, b, seg=5, rings=1))
    n = 7
    pts = []
    for i in range(n + 1):
        u = i / n
        z = 0.08 + u * length
        y = -curve * u * u
        w = width * (1 - 0.65 * u ** 3)
        pts.append((y, z, w))
    tris, norms = [], []
    th = 0.007
    for (y0, z0, w0), (y1, z1, w1) in zip(pts[:-1], pts[1:]):
        # a flat blade (thin in x), edge toward +y, spine toward -y
        q = [(-th, y0 - w0 * 0.5, z0), (th, y0 - w0 * 0.5, z0), (0, y0 + w0 * 0.5, z0),
             (-th, y1 - w1 * 0.5, z1), (th, y1 - w1 * 0.5, z1), (0, y1 + w1 * 0.5, z1)]
        for a, b2, c, d in ((0, 2, 5, 3), (2, 1, 4, 5), (1, 0, 3, 4)):
            tris += [[q[a], q[b2], q[c]], [q[a], q[c], q[d]]]
    V = np.array(tris, np.float32)
    nrm = np.cross(V[:, 1] - V[:, 0], V[:, 2] - V[:, 0])
    nrm /= np.linalg.norm(nrm, axis=-1, keepdims=True) + 1e-9
    N = np.repeat(nrm[:, None], 3, 1)
    parts.append(M.Mesh(V, N, np.full(len(V), s, np.int32)))
    tip = pts[-1]
    parts.append(M.cone(0.006, 0.05, s, seg=4, z0=0).transformed(M.translate(0, tip[0], tip[1])))
    return M.Mesh.concat(parts)


def long_sword(mats, length=0.85):
    from models.props import sword
    return sword(mats, length=length, width=0.06, guard=0.22)


def rifle(mats, length=1.15, grip=0.32):
    """A musket/carbine held at the wrist of the stock: the barrel runs up
    +z from the grip, the stock below it, the lock and trigger facing +y."""
    w = mat(mats, 'stock', '#6a4426', spec=0.1)
    s = mat(mats, 'gunmetal', '#4a4c50', spec=0.7, gloss=26, metal=True)
    parts = []
    # stock: from the butt (below the grip) up to the fore-end
    parts.append(M.rounded_box(0.045, 0.11, 0.30, w, (0, 0.03, -0.20), r=0.018, seg=8, p=3))
    parts.append(M.rounded_box(0.04, 0.05, 0.12, w, (0, 0.0, -0.02), r=0.015, seg=8, p=3))
    parts.append(M.rounded_box(0.04, 0.05, length - grip - 0.25, w, (0, 0.0, 0.06 + (length - grip - 0.25) / 2),
                               r=0.014, seg=8, p=3))
    parts.append(M.cylinder(0.014, 0.012, length - grip, s, seg=8, z0=0.04).transformed(M.translate(0, -0.012, 0)))
    parts.append(M.rounded_box(0.03, 0.03, 0.06, s, (0, 0.01, 0.05), r=0.008, seg=6))
    return M.Mesh.concat(parts)


def flag_pole(mats, length=2.2, grip=0.8):
    return haft(mats, -grip, length - grip, 0.018)


# ------------------------------------------------------------ head gear (head bone space, pre HEAD scale)

def _dome(rx, ry, rz, m, c, z_cut=0.0, k=7):
    rings = []
    t0 = math.asin(max(-1, min(1, z_cut / rz)))
    for i in range(k + 1):
        t = t0 + (math.pi / 2 - t0) * i / k
        r = math.cos(t)
        rings.append((c[2] + rz * math.sin(t), max(rx * r, 1e-4), max(ry * r, 1e-4), c[0], c[1]))
    return M.loft(rings, m, seg=16, cap_bottom=False, cap_top=False)


def hat_cone(mats, m='hat', brim=0.16, h=0.16):
    """A conical (Chinese/Mongol) cap with a rolled brim."""
    hm = mat(mats, m, '#ffffff')
    parts = [M.lathe([(0.0, 0.0005), (0.0, brim), (0.02, brim), (0.04, 0.115), (h, 0.03), (h + 0.02, 0.0005)], hm, seg=16)
             .transformed(M.translate(0, -0.005, 0.15))]
    parts.append(M.sphere(0.02, mat(mats, 'hat_knob', '#c8a040', spec=0.6, gloss=20, metal=True), (0, -0.005, 0.15 + h + 0.02), seg=8))
    return M.Mesh.concat(parts)


def fur_hat(mats, m='hat', fur='fur'):
    """Mongol hat: a pointed crown with a turned-up fur brim."""
    hm = mat(mats, m, '#ffffff')
    fm = mat(mats, fur, '#6a4a2a', spec=0.02)
    parts = [M.lathe([(0.0, 0.0005), (0.0, 0.12), (0.10, 0.10), (0.20, 0.03), (0.23, 0.0005)], hm, seg=14)
             .transformed(M.translate(0, -0.01, 0.15))]
    parts.append(M.lathe([(0.0, 0.118), (0.02, 0.135), (0.07, 0.13), (0.075, 0.105)], fm, seg=14)
                 .transformed(M.translate(0, -0.01, 0.12)))
    return M.Mesh.concat(parts)


def great_helm(mats, m='helmet'):
    """A flat-topped great helm with a visor slit and breaths."""
    hm = mat(mats, m, '#9a9aa0', spec=0.8, gloss=26, metal=True)
    dark = mat(mats, 'slit', '#141414', spec=0.0)
    parts = [M.loft([(-0.02, 0.122, 0.13), (0.10, 0.125, 0.135), (0.22, 0.12, 0.13), (0.27, 0.10, 0.11), (0.28, 0.0005, 0.0005)],
                    hm, seg=16, cap_bottom=True, cap_top=False).transformed(M.translate(0, 0.008, 0))]
    parts.append(M.box(0.16, 0.02, 0.018, dark, (0, 0.136, 0.14)))
    parts.append(M.box(0.018, 0.02, 0.16, hm, (0, 0.14, 0.07)))
    return M.Mesh.concat(parts)


def morion(mats, m='helmet'):
    """A Spanish morion: a combed dome with a brim curving up front and back."""
    hm = mat(mats, m, '#9a9aa0', spec=0.8, gloss=26, metal=True)
    parts = [_dome(0.112, 0.126, 0.13, hm, (0, 0.0, 0.13), 0.0)]
    # brim: an ellipse, its ends (front and back) raised
    ang = np.linspace(0, 2 * np.pi, 24, endpoint=False)
    tris, norms = [], []
    rin, rout = (0.11, 0.124), (0.15, 0.20)
    for a0, a1 in zip(ang, np.roll(ang, -1)):
        def P(a, r, up):
            y = r[1] * math.sin(a)
            return (r[0] * math.cos(a), y, 0.135 + up * (abs(math.sin(a)) ** 2) * 0.05)
        q = [P(a0, rin, 0.2), P(a1, rin, 0.2), P(a1, rout, 1.0), P(a0, rout, 1.0)]
        tris += [[q[0], q[1], q[2]], [q[0], q[2], q[3]]]
    V = np.array(tris, np.float32)
    N = np.zeros_like(V)
    N[..., 2] = 1
    parts.append(M.Mesh(V, N, np.full(len(V), hm, np.int32)))
    parts.append(M.extrude([(-0.12, 0.0), (0.12, 0.0), (0.09, 0.07), (0.0, 0.09), (-0.09, 0.07)], 0.018, hm)
                 .transformed(M.translate(0, 0, 0.23) @ M.rot_z(90)))
    return M.Mesh.concat(parts)


def spiked_helm(mats, m='helmet', plume='plume'):
    """A Turkish spiked helmet (a conical dome with a spike) and a white
    plume, with a mail aventail hanging to the shoulders."""
    hm = mat(mats, m, '#9a9aa0', spec=0.8, gloss=26, metal=True)
    pm = mat(mats, plume, '#f2f0e8', spec=0.02)
    parts = [M.lathe([(0.0, 0.122), (0.06, 0.12), (0.13, 0.08), (0.19, 0.02), (0.26, 0.004)], hm, seg=16)
             .transformed(M.translate(0, 0.0, 0.12))]
    parts.append(M.loft([(-0.03, 0.135, 0.14), (0.06, 0.122, 0.13), (0.13, 0.12, 0.128)], mat(mats, 'mail', '#7c7e84', spec=0.4, gloss=14, metal=True),
                        seg=16, cap_bottom=False, cap_top=False).transformed(M.translate(0, -0.01, 0)))
    parts.append(M.capsule((0, -0.02, 0.33), (0, -0.16, 0.40), 0.03, 0.012, pm, seg=6, rings=1))
    parts.append(M.capsule((0, -0.16, 0.40), (0, -0.26, 0.33), 0.012, 0.006, pm, seg=6, rings=1))
    return M.Mesh.concat(parts)


def shako(mats, m='hat', plume='plume', badge='badge'):
    """A hussar's tall shako (or busby) with a plume on top."""
    hm = mat(mats, m, '#ffffff')
    pm = mat(mats, plume, '#d8b040', spec=0.1)
    bm = mat(mats, badge, '#d8b040', spec=0.7, gloss=22, metal=True)
    parts = [M.loft([(0.12, 0.112, 0.124), (0.33, 0.118, 0.13), (0.34, 0.0005, 0.0005)], hm, seg=16, cap_bottom=True, cap_top=False)]
    parts.append(M.cylinder(0.125, 0.125, 0.025, bm, seg=16, z0=0.12).transformed(M.scale(1, 1.1, 1)))
    parts.append(M.capsule((0, 0.10, 0.32), (0, 0.10, 0.48), 0.028, 0.02, pm, seg=6, rings=2))
    return M.Mesh.concat(parts)


def dragoon_helm(mats, m='helmet', crest='crest'):
    """A brass dragoon helmet with a fur crest over the top."""
    hm = mat(mats, m, '#c8a040', spec=0.8, gloss=24, metal=True)
    cm = mat(mats, crest, '#2a2018', spec=0.02)
    parts = [_dome(0.115, 0.13, 0.15, hm, (0, 0.0, 0.11), -0.01)]
    parts.append(M.lathe([(0.0, 0.0005), (0.0, 0.08), (0.012, 0.08), (0.015, 0.0005)], hm, seg=12)
                 .transformed(M.translate(0, 0.10, 0.11) @ M.rot_x(-75) @ M.scale(1.4, 1, 1)))
    parts.append(tube_y([(-0.17, 0.02, 0.03, 0.17), (-0.12, 0.04, 0.06, 0.22), (0.0, 0.045, 0.07, 0.27),
                         (0.10, 0.035, 0.05, 0.24), (0.14, 0.02, 0.03, 0.2)], cm, seg=8))
    return M.Mesh.concat(parts)


def papakha(mats, m='hat', fur='fur'):
    """A Cossack's cylindrical cap: a fur band and a cloth top."""
    hm = mat(mats, m, '#ffffff')
    fm = mat(mats, fur, '#3a2e26', spec=0.02)
    parts = [M.cylinder(0.125, 0.13, 0.13, fm, seg=16, z0=0.12).transformed(M.scale(1, 1.08, 1))]
    parts.append(M.cylinder(0.118, 0.11, 0.035, hm, seg=16, z0=0.25).transformed(M.scale(1, 1.08, 1)))
    return M.Mesh.concat(parts)


def feather_bonnet(mats, m='feathers', tips='feather_tips'):
    """A tall fan of feathers standing up behind the head (Mounted Warrior)."""
    fm = mat(mats, m, '#f4f2ea', spec=0.02)
    tm = mat(mats, tips, '#ffffff', tint=True)
    band = mat(mats, 'band', '#ffffff', tint=True)
    parts = [M.cylinder(0.118, 0.118, 0.05, band, seg=14, z0=0.13).transformed(M.scale(1, 1.08, 1))]
    n = 9
    for i in range(n):
        a = -70 + 140 * i / (n - 1)
        base = M.translate(0, -0.06, 0.17) @ M.rot_y(a) @ M.rot_x(18)
        feather = M.ellipsoid(0.035, 0.012, 0.13, fm, (0, 0, 0.17), seg=6)
        tip = M.ellipsoid(0.03, 0.012, 0.05, tm, (0, 0, 0.31), seg=6)
        parts.append(feather.transformed(base))
        parts.append(tip.transformed(base))
    return M.Mesh.concat(parts)


def head_wrap(mats, m='hat'):
    """A cloth wrap over the head and lower face (Cossack bashlyk)."""
    hm = mat(mats, m, '#ffffff')
    return M.ellipsoid(0.12, 0.13, 0.13, hm, (0, -0.005, 0.15), seg=14)


HEADGEAR = {'hat_cone': hat_cone, 'fur_hat': fur_hat, 'great_helm': great_helm, 'morion': morion,
            'spiked_helm': spiked_helm, 'shako': shako, 'dragoon_helm': dragoon_helm, 'papakha': papakha,
            'feather_bonnet': feather_bonnet, 'head_wrap': head_wrap}


def glaive(mats, length=2.7, grip=1.0):
    """A pole with a long curved blade (the chariot driver's weapon)."""
    z0, z1 = -grip, length - grip
    parts = [haft(mats, z0, z1 - 0.40, 0.018)]
    blade = M.extrude([(-0.02, 0.0), (0.03, 0.0), (0.07, 0.18), (0.06, 0.34), (0.0, 0.46), (-0.02, 0.30)], 0.012,
                      steel(mats))
    parts.append(blade.transformed(M.translate(0, 0, z1 - 0.44) @ M.rot_z(90)))
    parts.append(M.cylinder(0.024, 0.022, 0.06, bronze(mats), seg=8, z0=z1 - 0.46))
    return M.Mesh.concat(parts)


def khepresh(mats, m='hat'):
    """The Egyptian blue war crown: a tall bulging cap flaring at the back."""
    hm = mat(mats, m, '#ffffff')
    g = mat(mats, 'gold', '#d8b040', spec=0.7, gloss=22, metal=True)
    parts = [M.loft([(0.10, 0.118, 0.128, 0, 0.0), (0.20, 0.13, 0.14, 0, -0.02), (0.32, 0.12, 0.15, 0, -0.06),
                     (0.40, 0.085, 0.11, 0, -0.08), (0.43, 0.0005, 0.0005, 0, -0.08)], hm, seg=16, cap_bottom=False, cap_top=False)]
    parts.append(M.cylinder(0.122, 0.122, 0.025, g, seg=14, z0=0.11).transformed(M.scale(1, 1.06, 1)))
    return M.Mesh.concat(parts)


HEADGEAR['khepresh'] = khepresh
