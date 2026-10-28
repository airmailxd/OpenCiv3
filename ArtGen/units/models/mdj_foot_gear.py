"""Gear and props of MDJ's (Medieval Japan) foot units and leader
(catalog/mdj_foot.py): samurai armor (kabuto, sode, kusazuri, suneate), the
ashigaru's jingasa and sashimono, the sohei's head wrap and naginata, and a
leader's folding fan.

The g_<name> functions are gear of the 'footman' archetype
(models/foot_ancient.py), the other public functions its props (see
foot_ancient.register_extra at the bottom). 'mdj_royal' is the 'royal'
leader (models/leaders.py) with a prop of this module in the hands and extra
parts on the body.
"""
import math
import sys

import numpy as np

import mesh as M
from models import archetype
from models import humanoid as H
from models import props as P
from models.props_foot_ancient import strip_blade

HEAD = H.HEAD


def _h(mesh):
    return mesh.transformed(M.scale(HEAD))


def _rad(a0, a1):
    return math.radians(a0), math.radians(a1)


# ------------------------------------------------------------ headgear (head space)

def g_kabuto(g, horns=0.24, spread=0.12, shikoro='shikoro', lames=3, flare=0.075, low=0.0):
    """A samurai's kabuto: a steel bowl, a wide neck guard of stepped,
    civ-colored lames round the back and sides, turned-back flaps at the
    front, and tall gold kuwagata horns from the brow."""
    hm = g.m('helmet', '#8a8a90', finish='metal')
    sm = g.m(shikoro, '#ffffff', tint=True)
    lm = g.m('lacing', '#2a2420')
    gm = g.m('gold', '#c8a848', finish='metal')
    parts = [H._dome(0.122, 0.134, 0.14, hm, (0, -0.005, 0.135), -0.03)]
    # the brim band of the bowl
    parts.append(M.cylinder(0.128, 0.128, 0.03, hm, seg=18, z0=0.115).transformed(M.scale(1, 1.07, 1)))
    # the neck guard: stepped lames, each lower and wider, open at the face
    a0, a1 = _rad(150, 390)
    top_z = 0.13
    step = (0.17 + low) / lames
    for k in range(lames):
        z0 = top_z - k * step
        r0 = 0.13 + flare * k / lames
        r1 = 0.13 + flare * (k + 1) / lames
        rings = [(z0 + 0.01, r0, r0 * 1.07), (z0 - step - 0.005, r1 + 0.012, (r1 + 0.012) * 1.07)]
        parts.append(H._shell(rings, sm, a0, a1, seg=16))
        parts.append(H._shell([(z0 - step + 0.01, r1 + 0.008, (r1 + 0.008) * 1.07),
                               (z0 - step - 0.004, r1 + 0.014, (r1 + 0.014) * 1.07)], lm, a0, a1, seg=16))
    # fukigaeshi: the lames' ends turned back beside the face
    for sx in (1, -1):
        parts.append(M.rounded_box(0.03, 0.07, 0.10, sm, (sx * 0.14, 0.07, 0.10), r=0.012, seg=6)
                     .transformed(M.rot_z(sx * 20)))
    if horns:
        # a gold crest plate on the brow and the two flat horns rising in a V
        parts.append(M.ellipsoid(0.04, 0.02, 0.035, gm, (0, 0.14, 0.165), seg=8))
        for sx in (1, -1):
            prof = [(0.0, 0.0), (0.045, 0.0), (0.06 + spread * 0.5, horns * 0.6), (spread + 0.02, horns),
                    (spread - 0.01, horns), (0.03 + spread * 0.45, horns * 0.62)]
            parts.append(M.extrude(prof, 0.014, gm).transformed(M.translate(sx * 0.012, 0.15, 0.165)
                                                                  @ M.scale(sx, 1, 1) @ M.rot_x(-8)))
    return [('head', _h(M.Mesh.concat(parts)), None)]


def g_jingasa(g, r=0.22, apex=0.29, mat='jingasa'):
    """The ashigaru's jingasa: a wide, shallow conical hat (civ-colored),
    and its chin cord."""
    hm = g.m(mat, '#ffffff', tint=True)
    parts = [M.lathe([(0.145, 0.11), (0.155, r * 0.98), (0.165, r), (0.18, r * 0.92), (apex - 0.03, 0.06),
                      (apex, 0.0005)], hm, seg=22)]
    parts.append(M.ellipsoid(0.105, 0.115, 0.08, hm, (0, -0.005, 0.15), seg=12))
    parts.append(M.lathe([(apex - 0.035, 0.05), (apex - 0.01, 0.03), (apex + 0.005, 0.0005)],
                         g.m('jingasa_knob', '#c8c4b8'), seg=10))
    cm = g.m('cord', '#2a2420')
    for sx in (1, -1):
        parts.append(M.capsule((sx * 0.095, 0.03, 0.15), (sx * 0.05, 0.07, 0.0), 0.007, 0.007, cm, seg=4, rings=1))
    return [('head', _h(M.Mesh.concat(parts)), None)]


def g_hachimaki(g, mat='hachimaki', tail=True):
    """A cloth band round the brow (civ-colored), tied at the back with its
    ends flying up and back."""
    bm = g.m(mat, '#ffffff', tint=True)
    parts = [M.cylinder(0.116, 0.116, 0.035, bm, seg=16, z0=0.15).transformed(M.scale(1, 1.08, 1))]
    parts.append(M.ellipsoid(0.03, 0.025, 0.025, bm, (0, -0.125, 0.17), seg=6))
    if tail:
        parts.append(M.capsule((0.0, -0.13, 0.18), (0.05, -0.19, 0.30), 0.02, 0.012, bm, seg=5, rings=1))
        parts.append(M.capsule((0.05, -0.19, 0.30), (0.12, -0.20, 0.40), 0.012, 0.006, bm, seg=5, rings=1))
        parts.append(M.capsule((0.0, -0.13, 0.17), (-0.06, -0.22, 0.24), 0.018, 0.008, bm, seg=5, rings=1))
    return [('head', _h(M.Mesh.concat(parts)), None)]


def g_zukin(g, mat='zukin'):
    """The sohei's katsuo-zukin: a white cloth wrapped over the head and
    round the face and throat, falling in a wide cowl over the shoulders."""
    wm = g.m(mat, '#e4e2de')
    sk = g.m('skin', '#c98a58')
    ey = g.m('eyes', '#2a1a12')
    head = [M.ellipsoid(0.118, 0.13, 0.142, wm, (0, -0.012, 0.12), seg=16)]
    # the opening: the face shows from the brow to the mouth
    head.append(M.ellipsoid(0.072, 0.035, 0.06, sk, (0, 0.105, 0.11), seg=10))
    head.append(M.ellipsoid(0.018, 0.03, 0.025, sk, (0, 0.135, 0.10), seg=6))
    for sx in (1, -1):
        head.append(M.ellipsoid(0.016, 0.008, 0.01, ey, (sx * 0.035, 0.135, 0.125), seg=6))
    # the cloth tied over the crown in a little peak
    head.append(M.ellipsoid(0.05, 0.05, 0.05, wm, (0, -0.02, 0.255), seg=8))
    c = g.chest
    cowl = M.loft([(c[2][0] + 0.02, c[2][1] + 0.045, c[2][2] + 0.05), (c[3][0], c[3][1] + 0.05, c[3][2] + 0.05),
                   (c[4][0], c[4][1] + 0.06, c[4][2] + 0.04), (c[5][0] + 0.04, 0.08, 0.07)], wm, seg=18,
                  cap_bottom=False, cap_top=False)
    return [('head', _h(M.Mesh.concat(head)), None), ('chest', cowl, None)]


# ------------------------------------------------------------ armor

def _lame_bands(g, mat, color, zs, gap, h=0.012, rings=None):
    lm = g.m(mat, color)
    rings = rings or g.chest
    out = []
    for z in zs:
        rx = float(np.interp(z, [c[0] for c in rings], [c[1] for c in rings])) + gap
        ry = float(np.interp(z, [c[0] for c in rings], [c[2] for c in rings])) + gap
        out.append(('chest', M.loft([(z, rx, ry), (z + h, rx, ry)], lm, seg=16, cap_bottom=False, cap_top=False), None))
    return out


def g_do(g, mat='do', color='#ffffff', tint=True, lacing='lacing', lacing_color='#2a2420',
         zs=(0.035, 0.09, 0.145, 0.20), finish=None, belt=None, belt_color='#e2ded2'):
    """A laced cuirass (do) over the torso, with rows of lacing between its
    lames (and a cloth belt round its bottom edge, when `belt` names its
    material)."""
    dm = g.m(mat, color, tint=tint, finish=finish)
    gg = 0.03
    rings = [(z, rx + gg, ry + gg, dx, dy) for z, rx, ry, dx, dy in g.chest[:4]]
    out = [('chest', M.loft(rings, dm, seg=16, cap_bottom=False, cap_top=True), None)]
    out.append(('spine', M.loft([(-0.03, 0.168 * g.tw + gg, 0.122 * g.tw + gg), (0.22, 0.178 * g.tw + gg, 0.128 * g.tw + gg)],
                                dm, seg=14, cap_bottom=False, cap_top=False), None))
    if belt:
        bm = g.m(belt, belt_color)
        out.append(('spine', M.loft([(-0.04, 0.172 * g.tw + gg + 0.01, 0.126 * g.tw + gg + 0.01),
                                     (0.03, 0.17 * g.tw + gg + 0.01, 0.124 * g.tw + gg + 0.01)], bm, seg=16,
                                    cap_bottom=False, cap_top=False), None))
    if lacing:
        out += _lame_bands(g, lacing, lacing_color, zs, gg + 0.003)
        lm = g.m(lacing, lacing_color)
        for z in (0.06, 0.15):
            out.append(('spine', M.loft([(z, 0.17 * g.tw + gg + 0.003, 0.124 * g.tw + gg + 0.003),
                                         (z + 0.012, 0.17 * g.tw + gg + 0.003, 0.124 * g.tw + gg + 0.003)], lm, seg=14,
                                        cap_bottom=False, cap_top=False), None))
    return out


def g_sode(g, mat='sode', color='#ffffff', tint=True, length=0.24, width=1.0, lacing='lacing',
           lacing_color='#2a2420', lames=3):
    """Big rectangular shoulder guards (sode) hanging over the upper arms."""
    sm = g.m(mat, color, tint=tint)
    lm = g.m(lacing, lacing_color) if lacing else None
    lb = g.lb
    out = []
    for side in 'rl':
        c = 0.0 if side == 'r' else 180.0
        a0, a1 = _rad(c - 95 * width, c + 95 * width)
        r0, r1 = 0.095 * lb, 0.125 * lb
        rings = [(0.05, r0, r0 * 1.05), (0.05 - length, r1, r1 * 1.08)]
        out.append((f'arm_{side}', H._shell(rings, sm, a0, a1, seg=10), None))
        out.append((f'arm_{side}', H._shell([(0.06, r0 * 0.7, r0 * 0.7), (0.05, r0, r0 * 1.05)], sm, a0, a1, seg=10), None))
        if lm is not None:
            for k in range(1, lames + 1):
                z = 0.05 - length * k / lames
                r = r0 + (r1 - r0) * k / lames + 0.004
                out.append((f'arm_{side}', H._shell([(z + 0.012, r, r * 1.06), (z, r + 0.002, (r + 0.002) * 1.07)], lm,
                                                    a0, a1, seg=10), None))
    return out


def g_kusazuri(g, mat='kusazuri', color='#ffffff', tint=True, n=6, length=0.26, z0=0.08, flare=0.06,
               lacing='lacing', lacing_color='#2a2420', lames=3):
    """The armored skirt (kusazuri): panels of laced lames hanging from the
    waist all round."""
    km = g.m(mat, color, tint=tint)
    lm = g.m(lacing, lacing_color) if lacing else None
    tw = g.tw
    out = []
    span = 360.0 / n
    for i in range(n):
        c = 90.0 + span * i
        a0, a1 = _rad(c - span * 0.47, c + span * 0.47)
        rx0, ry0 = 0.192 * tw, 0.148 * tw
        top = (z0, rx0, ry0)
        bot = (z0 - length, rx0 + flare, ry0 + flare)
        out.append(('pelvis', H._shell([bot, top], km, a0, a1, seg=5), None))
        if lm is not None:
            for k in range(1, lames + 1):
                u = k / lames
                z = z0 - length * u
                rx, ry = rx0 + flare * u + 0.004, ry0 + flare * u + 0.004
                out.append(('pelvis', H._shell([(z, rx, ry), (z + 0.013, rx - flare * 0.05, ry - flare * 0.05)], lm,
                                               a0, a1, seg=5), None))
    return out


def g_suneate(g, mat='suneate', color='#ffffff', tint=True, ties='tie', ties_color='#2a2420', top=-0.05,
              bottom=-0.33):
    """Shin guards over the front and sides of the shins, tied behind."""
    sm = g.m(mat, color, tint=tint)
    tm = g.m(ties, ties_color) if ties else None
    lb = g.lb
    out = []
    a0, a1 = _rad(-20, 200)
    for side in 'rl':
        rings = [(bottom, 0.064 * lb, 0.066 * lb), (top, 0.08 * lb, 0.082 * lb)]
        out.append((f'shin_{side}', H._shell(rings, sm, a0, a1, seg=10), None))
        if tm is not None:
            for z, r in ((top - 0.02, 0.078), (bottom + 0.04, 0.066)):
                out.append((f'shin_{side}', M.cylinder(r * lb, r * lb, 0.02, tm, seg=10, z0=z), None))
    return out


def g_shin_wraps(g, mat='kyahan', color='#8a8a8c', ties='tie', ties_color='#3a3632', top=-0.04, bottom=-0.36):
    """Cloth gaiters (kyahan) round the shins."""
    wm = g.m(mat, color)
    tm = g.m(ties, ties_color) if ties else None
    lb = g.lb
    out = []
    for side in 'rl':
        out.append((f'shin_{side}', M.loft([(bottom, 0.058 * lb, 0.06 * lb), (-0.2, 0.068 * lb, 0.07 * lb),
                                            (top, 0.074 * lb, 0.076 * lb)], wm, seg=12, cap_bottom=False,
                                           cap_top=False), None))
        if tm is not None:
            for z in (top - 0.015, bottom + 0.03):
                out.append((f'shin_{side}', M.cylinder(0.072 * lb, 0.072 * lb, 0.015, tm, seg=10, z0=z), None))
    return out


def g_tekko(g, mat='tekko', color='#808084'):
    """Cloth arm guards over the forearms (sohei)."""
    tm = g.m(mat, color)
    lb = g.lb
    out = []
    for side in 'rl':
        out.append((f'forearm_{side}', M.loft([(-H.FOREARM + 0.04, 0.05 * lb, 0.05 * lb), (-0.12, 0.058 * lb, 0.058 * lb),
                                               (0.0, 0.062 * lb, 0.062 * lb)], tm, seg=10), None))
    return out


def g_scarf(g, mat='scarf', color='#ffffff', tint=True):
    """A cloth scarf wound round the neck and over the shoulders."""
    sm = g.m(mat, color, tint=tint)
    c = g.chest
    ring = M.loft([(c[3][0] + 0.01, c[3][1] * 0.85 + 0.03, c[3][2] + 0.04), (c[4][0], c[4][1] + 0.06, c[4][2] + 0.05),
                   (c[5][0] + 0.04, 0.085, 0.075)], sm, seg=16, cap_bottom=False, cap_top=False)
    out = [('chest', ring, None)]
    # one end hanging down the chest
    out.append(('chest', M.rounded_box(0.06, 0.02, 0.18, sm, (0.06, c[2][2] + 0.035, 0.18), r=0.008, seg=6, p=4)
                .transformed(M.rot_y(-8)), None))
    return out


def g_obi_tails(g, mat='obi', color='#ffffff', tint=True, length=0.40, width=0.08):
    """A sash knotted at the front with its two ends hanging to the knees."""
    bm = g.m(mat, color, tint=tint)
    tw = g.tw
    out = [('pelvis', M.loft([(0.05, 0.19 * tw, 0.142 * tw), (0.13, 0.182 * tw, 0.136 * tw)], bm, seg=18,
                             cap_bottom=False, cap_top=False), None)]
    out.append(('pelvis', M.ellipsoid(0.05, 0.03, 0.04, bm, (0.0, 0.15 * tw, 0.09), seg=8), None))
    for dx, rz in ((-0.03, 6), (0.04, -6)):
        out.append(('pelvis', M.rounded_box(width, 0.016, length, bm, (dx, 0.16 * tw, 0.06 - length / 2), r=0.006, seg=6,
                                            p=4).transformed(M.rot_x(-7) @ M.rot_y(rz)), None))
    return out


def g_kesa(g, mat='kesa', color='#2e2a26'):
    """A monk's dark kesa: a wide cloth over the chest and back, under the
    cowl, and a panel hanging in front from the waist."""
    km = g.m(mat, color)
    gg = 0.032
    rings = [(z, rx + gg, ry + gg, dx, dy) for z, rx, ry, dx, dy in g.chest[:4]]
    out = [('chest', M.loft(rings, km, seg=16, cap_bottom=False, cap_top=True), None)]
    out.append(('spine', M.loft([(-0.04, 0.17 * g.tw + gg, 0.124 * g.tw + gg), (0.22, 0.178 * g.tw + gg, 0.128 * g.tw + gg)],
                                km, seg=14, cap_bottom=False, cap_top=False), None))
    out.append(('pelvis', M.rounded_box(0.26 * g.tw, 0.018, 0.22, km, (0, 0.17 * g.tw, -0.04), r=0.008, seg=6, p=4)
                .transformed(M.rot_x(-6)), None))
    return out


def g_sashimono(g, mat='sashimono', height=0.62, width=0.30, pole=1.12, mon='mon'):
    """The ashigaru's back banner: a pole strapped to the back of the armor,
    rising above the head, with a civ-colored flag on a crossbar, hanging
    behind the pole (in the front-back plane), a pale crest on it."""
    fm = g.m(mat, '#ffffff', tint=True)
    wm = g.m('pole', '#8a6a44')
    mm = g.m(mon, '#dedad0')
    yb = -(g.chest[2][2] + 0.07)
    out = [M.cylinder(0.016, 0.014, pole + 0.12, wm, seg=8, z0=-0.12).transformed(M.translate(0, yb, 0))]
    # the holder strapped to the back
    out.append(M.rounded_box(0.05, 0.04, 0.16, wm, (0, yb + 0.02, 0.02), r=0.01, seg=6))
    out.append(M.rounded_box(0.05, 0.04, 0.06, wm, (0, yb + 0.02, 0.26), r=0.01, seg=6))
    top = pole
    # the crossbar at the top, reaching back over the flag
    out.append(M.capsule((0, yb + 0.02, top - 0.02), (0, yb - width - 0.03, top - 0.02), 0.012, 0.012, wm, seg=6, rings=1))
    flag = M.box(0.012, width, height, fm, (0, yb - width / 2 - 0.01, top - 0.04 - height / 2))
    out.append(flag)
    for sx in (1, -1):
        out.append(M.disc(0.075, 0.075, 0.004, mm, seg=16).transformed(
            M.translate(sx * 0.008, yb - width / 2 - 0.01, top - 0.04 - height * 0.42) @ M.rot_z(sx * 90)))
    return [('chest', M.Mesh.concat(out), None)]


def g_katana_back(g):
    """A katana in its scabbard thrust through the belt at the left hip, the
    hilt forward and up, the scabbard reaching back and a little down (worn
    over a skirt of armor, a little further out)."""
    from models.props_foot_ancient import katana_sheathed
    return [('pelvis', katana_sheathed(g.mats).transformed(M.translate(-0.25 * g.tw, 0.04, 0.06) @ M.rot_y(-10)
                                                            @ M.rot_x(104)), None)]


# ------------------------------------------------------------ props (grip at the origin, long axis +z)

def yari(mats, length=2.6, grip=0.95, tassel=None):
    """A straight spear with a long narrow blade (and a civ-colored tassel
    below the head when `tassel` names its material)."""
    s = P.steel(mats)
    z0, z1 = -grip, length - grip
    parts = [P.haft(mats, z0, z1 - 0.26, 0.017)]
    tip = M.lathe([(0, 0.018), (0.03, 0.024), (0.18, 0.018), (0.30, 0.0005)], s, seg=8)
    parts.append(tip.transformed(M.translate(0, 0, z1 - 0.30) @ M.scale(1, 0.5, 1)))
    parts.append(M.cylinder(0.024, 0.02, 0.07, P.mat(mats, 'collar_metal', '#4a4440', spec=0.4, metal=True), seg=8,
                            z0=z1 - 0.36))
    parts.append(M.cylinder(0.02, 0.016, 0.05, s, seg=8, z0=z0))
    if tassel:
        tm = P.mat(mats, tassel, '#ffffff', tint=True)
        parts.append(M.cylinder(0.028, 0.032, 0.10, tm, seg=8, z0=z1 - 0.47))
        for a in range(0, 360, 72):
            r = math.radians(a)
            parts.append(M.capsule((0.024 * math.cos(r), 0.024 * math.sin(r), z1 - 0.46),
                                   (0.05 * math.cos(r), 0.05 * math.sin(r), z1 - 0.58), 0.012, 0.006, tm, seg=5, rings=1))
    return M.Mesh.concat(parts)


def naginata(mats, length=1.9, grip=0.95, blade=0.58, width=0.12):
    """A naginata: a long haft and a curved single-edged blade, its edge
    forward (+y) and the tip sweeping back."""
    s = P.steel(mats)
    z0 = -grip
    zb = length - grip - blade
    parts = [P.haft(mats, z0, zb, 0.019, m=P.mat(mats, 'haft_dark', '#3a2c22', spec=0.1))]
    parts.append(M.cylinder(0.024, 0.024, 0.05, P.mat(mats, 'collar_metal', '#4a4440', spec=0.4, metal=True), seg=8,
                            z0=zb - 0.06))
    parts.append(M.cylinder(0.045, 0.045, 0.012, P.mat(mats, 'tsuba', '#3a3430', spec=0.4, metal=True), seg=10, z0=zb - 0.01))
    us = np.linspace(0, 1, 10)
    pts = [(0.10 * u * u, zb + blade * u) for u in us]
    widths = [width * (1 - u ** 4) * (0.8 + 0.2 * u) + 0.003 for u in us]
    parts.append(strip_blade(pts, widths, 0.012, s, offset=0.35))
    parts.append(M.cylinder(0.02, 0.016, 0.06, s, seg=8, z0=z0))
    return M.Mesh.concat(parts)


def folding_fan(mats, r=0.30, spread=150.0, m='fan', ribs='fan_rib'):
    """A big open folding fan (the grip at the pivot, the fan opening along
    +z, its face across x-z)."""
    fm = P.mat(mats, m, '#e8e2cc', spec=0.05)
    rm = P.mat(mats, ribs, '#3a2a1e', spec=0.1)
    k = 12
    angs = np.linspace(-spread / 2, spread / 2, k + 1)
    parts = []
    r0 = r * 0.35
    for i in range(k):
        a, b = math.radians(angs[i]), math.radians(angs[i + 1])
        dy = 0.006 if i % 2 else -0.006
        quad = [(r0 * math.sin(a), dy, r0 * math.cos(a)), (r * math.sin(a), 0.0, r * math.cos(a)),
                (r * math.sin(b), 0.0, r * math.cos(b)), (r0 * math.sin(b), -dy, r0 * math.cos(b))]
        parts.append(M.plate(quad, fm, thick=0.006))
    for a in (angs[0], angs[-1]):
        a = math.radians(a)
        parts.append(M.capsule((0, 0, 0), (r * math.sin(a), 0, r * math.cos(a)), 0.008, 0.006, rm, seg=5, rings=1))
    parts.append(M.sphere(0.018, rm, (0, 0, 0), seg=6))
    return M.Mesh.concat(parts)


# ------------------------------------------------------------ the leader: royal with an extra prop

from models import leaders as L  # noqa: E402


@archetype('mdj_royal')
class MdjRoyal(L.Royal):
    """The 'royal' leader, with:
        fan       (side 'r'|'l', {folding_fan kwargs}, (pitch, roll, yaw)):
                  a folding fan held in that hand, open upward and facing
                  forward with the chest, turned by the euler angles
        body_gear [(bone, function name in this module, {kwargs})]: extra
                  rigid parts (meshes in that bone's space, using the
                  royal's materials)
    """

    def __init__(self, unit, params):
        super().__init__(unit, params)
        for bone, fn, kw in params.get('body_gear', []):
            self.parts.append((bone, globals()[fn](self, **kw), None))

    def pose_full(self, pose, act, name, t):
        mesh = super().pose_full(pose, act, name, t)
        fan = self.cp.get('fan')
        if not fan:
            return mesh
        side, kw, ang = fan
        W = self.world(pose, act)
        Wh = W[f'hand_{side}']
        grip = Wh[:3, 3] + Wh[:3, :3] @ np.array([0, 0.03, -H.GRIP], np.float32)
        key = ('__fan', tuple(sorted(kw.items())))
        if key not in self._props:
            self._props[key] = folding_fan(self.mats, **kw)
        T = np.eye(4, dtype=np.float32)
        T[:3, :3] = W['chest'][:3, :3]
        fm = self._props[key].transformed(M.translate(*grip) @ T @ M.euler(*ang))
        if name.startswith('death'):
            # it falls from the hand and lies on the ground
            u = float(np.clip((t - 0.2) / 0.35, 0, 1))
            if u > 0:
                fl = self._props[key].transformed(M.translate(grip[0] + 0.2, grip[1], 0.03) @ M.rot_x(90))
                fm = fl if u >= 1 else fm.transformed(M.translate(0, 0, -grip[2] * u * 0.9))
        return M.Mesh.concat([mesh, fm])


def mantle(fig, m='mantle', trim='gold', r=0.30, drop=0.14, z=0.27, opening=100.0):
    """A stiff round cape over the shoulders, flaring out like a bell and
    open in front (`opening` degrees), with a gold edge (chest space, built
    from a royal's materials)."""
    mm = fig.m(m)
    tm = fig.m(trim, '#c8a848')
    a0, a1 = _rad(90 + opening / 2, 450 - opening / 2)
    rings = [(z - drop, r, r * 0.86, 0, -0.02), (z - drop * 0.4, r * 0.82, r * 0.72, 0, -0.015),
             (z, r * 0.52, r * 0.46), (z + 0.05, 0.11, 0.09)]
    parts = [H._shell(rings, mm, a0, a1, seg=24)]
    parts.append(H._shell([(z - drop - 0.012, r + 0.012, r * 0.86 + 0.012, 0, -0.02),
                           (z - drop + 0.025, r * 0.97 + 0.012, r * 0.84 + 0.012, 0, -0.02)], tm, a0, a1, seg=24))
    # gold edges down the front opening
    for a in (a0, a1):
        pts = [(rr[1] * math.cos(a) * 1.02, rr[2] * math.sin(a) * 1.02 + (rr[4] if len(rr) > 4 else 0), rr[0])
               for rr in rings[:3]]
        for p0, p1 in zip(pts[:-1], pts[1:]):
            parts.append(M.capsule(p0, p1, 0.014, 0.014, tm, seg=5, rings=1))
    return M.Mesh.concat(parts)


def tall_eboshi(fig, m='hat', band='gold', height=0.36):
    """A tall court cap standing straight up from the crown, a gold band
    round its base (head space, scaled to the head like the royal's
    headgear)."""
    hm = fig.m(m)
    bm = fig.m(band, '#c8a848')
    parts = [M.ellipsoid(0.104, 0.116, 0.085, hm, (0, -0.01, 0.185), seg=14),
             M.loft([(0.20, 0.075, 0.085), (0.20 + height * 0.5, 0.068, 0.078), (0.20 + height, 0.058, 0.07)], hm, seg=14)
             .transformed(M.translate(0, -0.02, 0) @ M.rot_x(-6)),
             M.cylinder(0.08, 0.08, 0.035, bm, seg=14, z0=0.215).transformed(M.translate(0, -0.02, 0) @ M.scale(1, 1.12, 1))]
    return M.Mesh.concat(parts).transformed(M.scale(HEAD))


from models import foot_ancient as FA  # noqa: E402

FA.register_extra(sys.modules[__name__])
