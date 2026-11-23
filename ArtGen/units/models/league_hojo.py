"""MDJ's Hojo General (catalog/league_mdj.py): a Sengoku commander of the
Later Hojo of Odawara, on foot.

The 'hojo_general' archetype is the 'footman' (models/foot_ancient.py) with
gear of its own, registered as footman gear (prefix 'hojo_') so the shared
files stay as they are:

  * hojo_kabuto   a ribbed black helmet bowl with a gilt finial ring, a wide
                  shikoro of four lames laced in red, turnbacks with the
                  crest, tall gold kuwagata and a gold maedate disc between
                  them bearing the mitsu-uroko
  * hojo_menpo    a red-lacquered half mask: cheeks, chin, nose, a grey
                  moustache, and the throat guard of laced lames under it
  * hojo_jinbaori the commander's sleeveless campaign surcoat (civ colour):
                  a yoke over the shoulders, open in front, flaring skirts
                  split at the back, a black collar band down the front
                  edges, a pale hem, the Hojo crest large on the back (painted
                  into the cloth)
  * hojo_kote     armored sleeves: dark cloth, a black plate on each
                  forearm with a gilt edge, a hand guard
  * hojo_daisho   the katana and wakizashi in black scabbards in the sash
  * hojo_sashimono  (optional) a back banner with the crest

and the props 'hojo_gunbai' (the lacquered war fan) and the katana. In the
actions named by the catalog's 'fan_actions' the right hand holds the gunbai
instead of the weapon (a general directing, not fighting).

The Hojo crest, the mitsu-uroko, is three triangles (scales) set in a
triangle: one above, two below.
"""
import math
import sys

import numpy as np

import mesh as M
from models import archetype
from models import foot_ancient as FA
from models import humanoid as H
from models import props as P
from models.league_mounted import grid_mesh

HEAD = H.HEAD
T = M.translate


def _h(mesh):
    return mesh.transformed(M.scale(HEAD))


def in_tri(x, y, cx, cy, s):
    """Whether (x, y) lies in the upward equilateral triangle of side s whose
    centroid is (cx, cy)."""
    h = s * math.sqrt(3) / 2
    y0 = cy - h / 3                      # the base
    if y < y0 or y > y0 + h:
        return False
    half = s / 2 * (1 - (y - y0) / h)
    return abs(x - cx) <= half


def in_uroko(x, y, s, gap=0.12):
    """Whether (x, y) lies on the mitsu-uroko of overall side s centred on
    the origin: three triangles of side s/2 (shrunk by `gap` so the scales
    stand apart)."""
    h = s * math.sqrt(3) / 2
    t = s / 2 * (1 - gap)
    y0 = -h / 3
    for cx, cy in ((0.0, y0 + h / 2 + h / 6), (-s / 4, y0 + h / 6), (s / 4, y0 + h / 6)):
        if in_tri(x, y, cx, cy, t):
            return True
    return False


# ------------------------------------------------------------ head (head space, before HEAD)

def g_hojo_kabuto(g, horns=0.40, spread=0.17):
    hm = g.m('helmet', '#2a2826', finish='gloss')
    sm = g.m('shikoro', '#2a2826', finish='gloss')
    lm = g.m('lacing', '#8a2a22')
    gm = g.m('gold', '#c4a050', finish='metal')
    dk = g.m('mon_dark', '#16140f')
    parts = [H._dome(0.124, 0.136, 0.15, hm, (0, -0.004, 0.13), -0.03)]
    # the ribs of the suji-bachi bowl, meridians from the brim to the top
    for k in range(12):
        a = 2 * math.pi * k / 12
        pts = []
        for t in np.linspace(0.05, 1.35, 6):
            r = math.cos(t)
            pts.append((0.127 * r * math.cos(a), -0.004 + 0.139 * r * math.sin(a), 0.13 + 0.153 * math.sin(t)))
        for p0, p1 in zip(pts[:-1], pts[1:]):
            parts.append(M.capsule(p0, p1, 0.006, 0.006, hm, seg=4, rings=1))
    # the gilt finial ring at the top (tehen) and the brim band
    parts.append(M.lathe([(0.0, 0.028), (0.008, 0.032), (0.016, 0.028)], gm, seg=12).transformed(T(0, -0.004, 0.272)))
    parts.append(M.cylinder(0.132, 0.132, 0.026, hm, seg=20, z0=0.112).transformed(M.scale(1, 1.07, 1)))
    # the shikoro: four lames stepping out and down round the back and sides, red lacing at each edge
    a0, a1 = math.radians(152), math.radians(388)
    top_z, step, lames = 0.125, 0.040, 3
    for k in range(lames):
        z0 = top_z - k * step
        r0 = 0.135 + 0.016 * k
        r1 = 0.135 + 0.016 * (k + 1)
        parts.append(H._shell([(z0 + 0.008, r0, r0 * 1.07), (z0 - step - 0.004, r1 + 0.012, (r1 + 0.012) * 1.07)], sm,
                              a0, a1, seg=18))
        parts.append(H._shell([(z0 - step + 0.008, r1 + 0.009, (r1 + 0.009) * 1.07),
                               (z0 - step - 0.006, r1 + 0.015, (r1 + 0.015) * 1.07)], lm, a0, a1, seg=18))
        # vertical lacing lines down each lame
        for a in np.linspace(a0 + 0.2, a1 - 0.2, 9):
            rr = (r0 + r1) / 2 + 0.012
            parts.append(M.box(0.008, 0.008, step * 0.8, lm,
                               (rr * math.cos(a), rr * 1.07 * math.sin(a), z0 - step / 2)).transformed(M.rot_z(0)))
    # fukigaeshi: the turnbacks beside the face, each with a gilt crest
    for sx in (1, -1):
        parts.append(M.rounded_box(0.035, 0.08, 0.10, sm, (sx * 0.15, 0.07, 0.095), r=0.012, seg=6)
                     .transformed(M.rot_z(sx * 22)))
        parts.append(M.disc(0.022, 0.022, 0.006, gm, seg=10).transformed(
            M.rot_z(sx * 22) @ T(sx * 0.17, 0.07, 0.095) @ M.rot_z(sx * 90)))
    # the kuwagata: two tall flat gold horns rising in a V from a brow plate
    parts.append(M.rounded_box(0.10, 0.02, 0.05, gm, (0, 0.145, 0.155), r=0.01, seg=6))
    for sx in (1, -1):
        prof = [(0.0, 0.0), (0.05, 0.0), (0.06 + spread * 0.55, horns * 0.55), (spread + 0.03, horns),
                (spread - 0.015, horns * 0.98), (0.03 + spread * 0.45, horns * 0.58)]
        parts.append(M.extrude(prof, 0.014, gm).transformed(T(sx * 0.012, 0.15, 0.15) @ M.scale(sx, 1, 1)
                                                              @ M.rot_x(-12)))
    # the maedate between them: a gold disc bearing the crest
    disc_c = (0, 0.165, 0.245)
    parts.append(M.disc(0.055, 0.055, 0.01, gm, seg=18).transformed(T(*disc_c) @ M.rot_x(-12) @ M.rot_z(180)))
    s = 0.075
    hgt = s * math.sqrt(3) / 2
    for cx, cz in ((0.0, hgt / 6 + hgt / 2 - hgt / 3), (-s / 4, -hgt / 3 + hgt / 6), (s / 4, -hgt / 3 + hgt / 6)):
        t = s / 2 * 0.86
        th = t * math.sqrt(3) / 2
        tri = [(cx - t / 2, cz - th / 3), (cx + t / 2, cz - th / 3), (cx, cz + 2 * th / 3)]
        parts.append(M.extrude(tri, 0.006, dk).transformed(T(disc_c[0], disc_c[1] + 0.008, disc_c[2]) @ M.rot_x(-12)))
    return [('head', _h(M.Mesh.concat(parts)), None)]


def g_hojo_menpo(g):
    """A red-lacquered half mask over the cheeks and chin, its nose and a
    grey moustache, and the throat guard (yodare-kake) of laced lames."""
    rm = g.m('menpo', '#8a2a20', finish='gloss')
    hair = g.m('whisker', '#b8b4ac')
    lm = g.m('lacing', '#8a2a22')
    sm = g.m('shikoro', '#2a2826', finish='gloss')
    parts = []
    rings = [(0.0, 0.082, 0.088, 0, 0.035), (0.05, 0.092, 0.094, 0, 0.04), (0.10, 0.10, 0.098, 0, 0.035)]
    parts.append(H._shell(rings, rm, math.radians(-15), math.radians(195), seg=14))
    parts.append(M.capsule((0, 0.13, 0.11), (0, 0.142, 0.085), 0.018, 0.014, rm, seg=6, rings=1))
    for sx in (1, -1):
        parts.append(M.capsule((sx * 0.01, 0.135, 0.075), (sx * 0.06, 0.122, 0.06), 0.012, 0.006, hair, seg=5, rings=1))
    # the mouth slot
    parts.append(M.box(0.05, 0.01, 0.008, g.m('slit', '#121010'), (0, 0.137, 0.045)))
    # the throat guard: three lames below the chin, laced
    for k in range(3):
        z = -0.01 - 0.035 * k
        r = 0.09 + 0.012 * k
        parts.append(H._shell([(z, r, r * 1.02, 0, 0.03), (z - 0.03, r + 0.008, (r + 0.008) * 1.02, 0, 0.03)], sm,
                              math.radians(-20), math.radians(200), seg=12))
        parts.append(H._shell([(z - 0.028, r + 0.01, (r + 0.01) * 1.02, 0, 0.03), (z - 0.036, r + 0.012, (r + 0.012) * 1.02, 0, 0.03)],
                              lm, math.radians(-20), math.radians(200), seg=12))
    return [('head', _h(M.Mesh.concat(parts)), None)]


# ------------------------------------------------------------ the jinbaori

def g_hojo_jinbaori(g, mat='jinbaori', mon='mon', collar='collar_band', hem='jin_hem', length=0.50, flare=0.10,
                    gap=0.065, open_front=38.0, mon_size=0.24):
    """The commander's campaign surcoat in the civ colour: over the
    shoulders and the armor, open in front, the skirts flaring to the knees
    and split at the back; a black band down the front edges and round the
    neck, a pale hem, the crest large on the back."""
    jm = g.m(mat, '#ffffff', tint=True)
    mm = g.m(mon, '#ece6d4')
    cm = g.m(collar, '#1e1c1a')
    hm = g.m(hem, '#ece6d4')
    out = []
    chest = g.chest
    zs = [c[0] for c in chest]
    front = math.pi / 2
    of = math.radians(open_front)
    a0, a1 = front + of, front + 2 * math.pi - of
    nA = 48
    angs = np.linspace(a0, a1, nA + 1)

    # the body: rows from the waist up over the shoulders (chest space)
    zr = list(np.linspace(0.0, 0.27, 19)) + [0.29, 0.31, 0.325]
    rows = []
    for z in zr:
        rx = float(np.interp(z, zs, [c[1] for c in chest])) + gap
        ry = float(np.interp(z, zs, [c[2] for c in chest])) + gap
        dy = float(np.interp(z, zs, [c[4] for c in chest]))
        if z >= 0.23:     # the yoke stands out over the shoulders and sode
            rx = max(rx, 0.29 * g.tw / 1.3 - (z - 0.23) * 1.5)
        rows.append([(rx * math.cos(a), dy + ry * math.sin(a), z) for a in angs])
    Pb = np.asarray(rows, np.float32)
    back = 1.5 * math.pi

    def body_paint(i, j):
        a = 0.5 * (angs[j] + angs[j + 1])
        z = 0.5 * (zr[i] + zr[i + 1])
        if a < a0 + 0.16 or a > a1 - 0.16:
            return cm
        if i == len(zr) - 2:
            return cm
        # the crest on the back: x the arc length from the centre of the back
        rx = 0.5 * (Pb[i, j, 0] ** 2 + Pb[i, j, 1] ** 2) ** 0.5 + 0.5 * (Pb[i + 1, j, 0] ** 2 + Pb[i + 1, j, 1] ** 2) ** 0.5
        x = (a - back) * rx * 0.92
        if in_uroko(x, z - 0.14, mon_size):
            return mm
        return jm
    out.append(('chest', grid_mesh(Pb, body_paint), None))
    # the inside, a darker lining
    Pi = Pb.copy()
    Pi[..., :2] *= 0.985
    out.append(('chest', grid_mesh(Pi[:, ::-1], lambda i, j: g.m('lining', '#3a3430')), None))
    # the skirts (pelvis space): from the waist to the knees, flaring, split at the back
    tw = g.tw
    zk = [0.12, 0.02, -0.12, -0.26, -length + 0.06, -length]
    for half in (0, 1):
        s0, s1 = (a0, back - 0.05) if half == 0 else (back + 0.05, a1)
        hang = np.linspace(s0, s1, nA // 2 + 1)
        rows = []
        for z in zk:
            u = (0.12 - z) / (0.12 + length)
            rx = 0.195 * tw + gap * 0.8 + flare * u * 1.2
            ry = 0.150 * tw + gap * 0.8 + flare * u
            pl = 0.016 * u
            rows.append([((rx + pl * math.sin(7 * a)) * math.cos(a), (ry + pl * math.sin(7 * a)) * math.sin(a), z)
                         for a in hang])
        Ps = np.asarray(rows, np.float32)

        def skirt_paint(i, j, hang=hang):
            a = 0.5 * (hang[j] + hang[j + 1])
            if a < a0 + 0.16 or a > a1 - 0.16:
                return cm
            if i == len(zk) - 2:
                return hm
            return jm
        out.append(('pelvis', grid_mesh(Ps, skirt_paint), None))
        Pi = Ps.copy()
        Pi[..., :2] *= 0.98
        out.append(('pelvis', grid_mesh(Pi[:, ::-1], lambda i, j: g.m('lining', '#3a3430')), None))
    # the collar band round the back of the neck
    out.append(('chest', M.loft([(0.30, 0.13, 0.10, 0, -0.02), (0.345, 0.11, 0.09, 0, -0.025)], cm, seg=16,
                                cap_bottom=False, cap_top=False), None))
    return out


# ------------------------------------------------------------ arms, sash, swords

def g_hojo_do_mon(g, size=0.15):
    """The clan crest in gilt on the front of the do, painted on a thin
    plate following the chest."""
    gm = g.m('gold', '#c4a050', finish='metal')
    chest = g.chest
    zs = [c[0] for c in chest]
    gap = 0.042
    angs = np.linspace(math.pi / 2 - 0.55, math.pi / 2 + 0.55, 25)
    zr = np.linspace(0.06, 0.24, 19)
    rows = []
    for z in zr:
        rx = float(np.interp(z, zs, [c[1] for c in chest])) + gap
        ry = float(np.interp(z, zs, [c[2] for c in chest])) + gap
        dy = float(np.interp(z, zs, [c[4] for c in chest]))
        rows.append([(rx * math.cos(a), dy + ry * math.sin(a), z) for a in angs])
    Pts = np.asarray(rows, np.float32)
    tris = []
    for i in range(len(zr) - 1):
        for j in range(len(angs) - 1):
            x = 0.5 * (Pts[i, j, 0] + Pts[i, j + 1, 0])
            z = 0.5 * (zr[i] + zr[i + 1])
            if in_uroko(-x, z - 0.15, size):
                q = [Pts[i, j], Pts[i, j + 1], Pts[i + 1, j + 1], Pts[i + 1, j]]
                tris.append(M.plate(q, gm, thick=0.004))
    return [('chest', M.Mesh.concat(tris), None)] if tris else []


def g_hojo_kote(g, cloth='kote', plate='kote_plate'):
    """Armored sleeves: dark cloth over the arms, a lacquered plate along the
    top of each forearm with a gilt edge, a plate on the back of the hand."""
    cm = g.m(cloth, '#2e2a28')
    pm = g.m(plate, '#2a2826', finish='gloss')
    gm = g.m('gold', '#c4a050', finish='metal')
    lb = g.lb
    out = []
    for side in 'rl':
        out.append((f'arm_{side}', M.capsule((0, 0, 0), (0, 0, -H.UPPER_ARM + 0.02), 0.072 * lb, 0.058 * lb, cm, seg=10,
                                              rings=2), None))
        out.append((f'forearm_{side}', M.capsule((0, 0, 0), (0, 0, -H.FOREARM + 0.04), 0.058 * lb, 0.048 * lb, cm, seg=10,
                                                  rings=2), None))
        a0, a1 = math.radians(-60), math.radians(240)
        out.append((f'forearm_{side}', H._shell([(-0.20, 0.054 * lb, 0.054 * lb), (-0.03, 0.064 * lb, 0.064 * lb)], pm,
                                                 a0, a1, seg=10), None))
        out.append((f'forearm_{side}', H._shell([(-0.20, 0.056 * lb, 0.056 * lb), (-0.185, 0.058 * lb, 0.058 * lb)], gm,
                                                 a0, a1, seg=10), None))
        out.append((f'hand_{side}', M.rounded_box(0.05, 0.03, 0.06, pm, (0, -0.035, -0.04), r=0.01, seg=6), None))
        # plates on the upper arm, between the sode and the elbow
        for z in (-0.17, -0.21):
            out.append((f'arm_{side}', M.cylinder(0.066 * lb, 0.064 * lb, 0.025, pm, seg=10, z0=z), None))
    return out


def g_hojo_daisho(g):
    """The katana and the wakizashi thrust through the sash at the left hip."""
    from models.props_foot_ancient import katana_sheathed
    tw = g.tw
    k = katana_sheathed(g.mats)
    out = [('pelvis', k.transformed(T(-0.24 * tw, 0.06, 0.06) @ M.rot_z(-5) @ M.rot_x(128)), None)]
    out.append(('pelvis', k.transformed(T(-0.20 * tw, 0.10, 0.08) @ M.rot_z(-3) @ M.rot_x(118) @ M.scale(0.72)), None))
    return out


def g_hojo_obi(g, color='#d8cfb8'):
    """A pale sash knotted over the jinbaori's opening, its ends hanging."""
    bm = g.m('obi', color)
    tw = g.tw
    out = [('pelvis', M.loft([(0.07, 0.205 * tw, 0.16 * tw), (0.13, 0.20 * tw, 0.155 * tw)], bm, seg=20,
                             cap_bottom=False, cap_top=False), None)]
    out.append(('pelvis', M.ellipsoid(0.05, 0.03, 0.035, bm, (0.06, 0.16 * tw + 0.02, 0.10), seg=8), None))
    for dx, rz in ((0.04, 4), (0.08, -8)):
        out.append(('pelvis', M.rounded_box(0.05, 0.014, 0.22, bm, (dx, 0.16 * tw + 0.03, -0.02), r=0.006, seg=6, p=4)
                    .transformed(M.rot_y(rz)), None))
    return out


def g_hojo_sashimono(g, height=0.70, width=0.32, pole=1.35, mat='sashimono'):
    """A back banner: the pole in its holder on the back, a crossbar at the
    top, the civ-coloured banner with the crest in pale cloth."""
    fm = g.m(mat, '#ffffff', tint=True)
    mm = g.m('mon', '#ece6d4')
    wm = g.m('pole', '#2a2420')
    yb = -(g.chest[2][2] + 0.10)
    out = [M.cylinder(0.016, 0.014, pole + 0.14, wm, seg=8, z0=-0.14).transformed(T(0, yb, 0))]
    out.append(M.rounded_box(0.05, 0.04, 0.16, wm, (0, yb + 0.02, 0.02), r=0.01, seg=6))
    out.append(M.capsule((0, yb + 0.02, pole - 0.02), (0, yb - width - 0.03, pole - 0.02), 0.012, 0.012, wm, seg=6,
                         rings=1))
    ny, nx = 14, 10
    ys = np.linspace(yb - 0.01, yb - 0.01 - width, nx + 1)
    zs_ = np.linspace(pole - 0.04, pole - 0.04 - height, ny + 1)
    for side in (1, -1):
        Pts = np.asarray([[(side * 0.006, y, z) for y in (ys if side > 0 else ys[::-1])] for z in zs_], np.float32)

        def paint(i, j, side=side):
            y = 0.5 * (Pts[i, j, 1] + Pts[i, j + 1, 1])
            z = 0.5 * (zs_[i] + zs_[i + 1])
            cx = yb - 0.01 - width / 2
            return mm if in_uroko((y - cx) * side, z - (pole - 0.04 - height * 0.45), width * 0.62) else fm
        out.append(grid_mesh(Pts, paint))
    return [('chest', M.Mesh.concat(out), None)]


FA.register_extra(sys.modules[__name__])


# ------------------------------------------------------------ props (grip at the origin, long axis +z)

def hojo_gunbai(mats, size=1.0):
    """The gunbai: a lacquered iron war fan, a round blade on a short
    handle; black, a gilt rim, a red sun on both faces; a red cord and
    tassel from the pommel."""
    bm = P.mat(mats, 'gunbai', '#1e1c1a', spec=0.35, gloss=22)
    gm = P.mat(mats, 'gold', '#c4a050', spec=0.8, gloss=22, metal=True)
    rm = P.mat(mats, 'sun', '#a8281e', spec=0.2)
    cm = P.mat(mats, 'cord', '#8a2a22')
    parts = [M.cylinder(0.018, 0.02, 0.24, bm, seg=8, z0=-0.10)]
    parts.append(M.cylinder(0.026, 0.026, 0.02, gm, seg=8, z0=0.12))
    parts.append(M.sphere(0.022, gm, (0, 0, -0.11), seg=8))
    c = (0, 0, 0.33)
    parts.append(M.disc(0.20, 0.21, 0.016, bm, seg=24).transformed(T(*c)))
    parts.append(M.lathe([(-0.012, 0.20), (0.0, 0.212), (0.012, 0.20)], gm, seg=24).transformed(T(*c) @ M.rot_x(90)
                                                                                                @ M.scale(1, 1.05, 1)))
    for sy in (1, -1):
        parts.append(M.disc(0.085, 0.085, 0.004, rm, seg=18).transformed(T(0, -sy * 0.01, c[2] + 0.02) @ M.rot_z(
            0 if sy > 0 else 180)))
    parts.append(M.capsule((0, 0, -0.12), (0.02, 0.02, -0.24), 0.008, 0.008, cm, seg=4, rings=1))
    parts.append(M.capsule((0.02, 0.02, -0.24), (0.02, 0.02, -0.32), 0.022, 0.012, cm, seg=6, rings=1))
    return M.Mesh.concat(parts).transformed(M.scale(size))


# ------------------------------------------------------------ the archetype

@archetype('hojo_general')
class HojoGeneral(FA.Footman):
    """A footman whose right hand holds the gunbai in the catalog's
    'fan_actions' and the weapon (a katana) in the others."""

    def frame(self, action, f, n, ctx):
        if action in self.p.get('fan_actions', ()):
            w = self.weapon
            self.weapon = 'gunbai'
            try:
                return super().frame(action, f, n, ctx)
            finally:
                self.weapon = w
        return super().frame(action, f, n, ctx)
