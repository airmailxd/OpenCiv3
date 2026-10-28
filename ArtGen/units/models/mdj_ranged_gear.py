"""Gear, props and two small archetypes for MDJ's (Medieval Japan's) ranged
and civil foot units (catalog/mdj_ranged.py): the Oda Arquebusier, the
Stone Crossbow, Otomo no Makuta (a samurai archer), the Medieval Japanese
Settler and Worker.

g_<name> functions are worn gear and the other public functions props for
the 'footman' archetype (models/foot_ancient.py; registered at the end of
this module). Gear functions take a foot_ancient.Gear and return
[(bone, mesh, tag)]; props take the material table and return a Mesh in
prop space (the grip at the origin, the long axis +z).

Archetypes:
    'mdj_footman'      the footman plus a crossbow: the prop is redrawn per
                       frame spanned or loose ('span' pose channel) with or
                       without its bolt ('bolt'), and the 'attack_crossbow'
                       animation (kneel, raise, aim, loose the bolt, span
                       again) and 'fortify_kneel' (kneeling at the ready).
    'ashigaru_gunner'  the gunpowder 'soldier' (models/foot_modern.py) with
                       footman gear (jingasa, lacquered armor, katana) and
                       the Japanese matchlock ('tanegashima').
"""
import math
import sys

import numpy as np

import mesh as M
import rig
from models import archetype
from models import humanoid as H
from models import foot_ancient as FA
from models import foot_modern as FM
from models import props_foot_modern as G
from models.props import mat, wood, steel, leather

HEAD = H.HEAD
A = H.ANKLE_H


def _h(mesh):
    return mesh.transformed(M.scale(HEAD))


def _v(x):
    return np.asarray(x, np.float32)


# ------------------------------------------------------------ headgear

def g_jingasa(g, mat_name='hat', color='#3c3c3e', brim=0.25, height=0.20, finish='gloss', tint=False):
    """An ashigaru's jingasa: a shallow pointed cone of lacquered iron over
    the head, its wide brim sloping down all round."""
    hm = g.m(mat_name, color, tint=tint, finish=finish)
    z0 = 0.15
    top = M.lathe([(z0 - 0.035, brim), (z0 - 0.02, brim * 0.97), (z0 + height * 0.45, brim * 0.55),
                   (z0 + height * 0.85, brim * 0.16), (z0 + height, 0.004)], hm, seg=22)
    under = M.lathe([(z0 + 0.04, 0.10), (z0 - 0.042, brim * 0.99)], hm, seg=22)
    band = M.cylinder(0.112, 0.112, 0.05, g.m('hat_band', '#1e1c1c'), seg=14, z0=0.11).transformed(M.scale(1, 1.08, 1))
    return [('head', _h(M.Mesh.concat([top, under, band])), 'hat')]


def g_kasa(g, mat_name='hat', color='#ffffff', tint=True, brim=0.24, height=0.15):
    """A worker's wide, rounded straw-and-cloth hat, low over the brow."""
    hm = g.m(mat_name, color, tint=tint)
    z0 = 0.13
    top = M.lathe([(z0 - 0.02, brim), (z0 + 0.005, brim * 0.98), (z0 + height * 0.45, brim * 0.80),
                   (z0 + height * 0.80, brim * 0.50), (z0 + height, 0.004)], hm, seg=20)
    under = M.lathe([(z0 + 0.05, 0.10), (z0 - 0.025, brim * 0.99)], hm, seg=20)
    return [('head', _h(M.Mesh.concat([top, under]).transformed(M.translate(0, 0.01, 0) @ M.rot_x(-6))), 'hat')]


def g_soft_cap(g, mat_name='cap', color='#ffffff', tint=True, height=0.17, lean=0.05):
    """A tall soft cloth cap rounded to a peak that leans forward (the
    crossbowman's)."""
    cm = g.m(mat_name, color, tint=tint)
    rings = [(0.10, 0.118, 0.13, 0, -0.01), (0.17, 0.12, 0.13, 0, -0.008), (0.24, 0.10, 0.11, 0, lean * 0.3),
             (0.24 + height * 0.5, 0.07, 0.075, 0, lean * 0.7), (0.24 + height * 0.85, 0.035, 0.04, 0, lean),
             (0.24 + height, 0.004, 0.004, 0, lean * 1.1)]
    return [('head', _h(M.loft(rings, cm, seg=16, cap_bottom=False, cap_top=True)), 'hat')]


def g_hair_tie(g, mat_name='hair_tie', color='#ffffff', tint=True):
    """A cloth tied round the topknot on the crown, its ends fluttering back."""
    bm = g.m(mat_name, color, tint=tint)
    parts = [M.ellipsoid(0.055, 0.065, 0.04, bm, (0, -0.03, 0.255), seg=10),
             M.capsule((0, -0.07, 0.26), (0.05, -0.17, 0.30), 0.026, 0.01, bm, seg=6, rings=1),
             M.capsule((0, -0.07, 0.26), (-0.04, -0.18, 0.26), 0.024, 0.01, bm, seg=6, rings=1)]
    return [('head', _h(M.Mesh.concat(parts)), None)]


def g_kabuto(g, mat_name='kabuto', color='#3a3c3e', crest='crest', tassels=True):
    """A samurai's kabuto: a ridged iron bowl, a broad flaring neck guard
    (shikoro) in lacquered lames, turned-back wings at the front and a tall
    V of civ-colored horns (kuwagata) with cords hanging from the crown."""
    hm = g.m(mat_name, color, finish='metal')
    cm = g.m(crest, '#ffffff', tint=True)
    lm = g.m('lacing', '#ffffff', tint=True)
    parts = [H._dome(0.124, 0.134, 0.14, hm, (0, -0.005, 0.125), 0.0)]
    # ridges on the bowl, front to back
    for a in (-30, 0, 30):
        parts.append(M.capsule((0, 0.13, 0.13), (0, 0.02, 0.27), 0.008, 0.008, hm, seg=5, rings=1)
                     .transformed(M.rot_y(a)))
    parts.append(M.cylinder(0.128, 0.13, 0.03, hm, seg=18, z0=0.115).transformed(M.scale(1, 1.08, 1)))
    # the neck guard: three lames flaring down and out, laced with civ-colored cord
    for k in range(3):
        z = 0.12 - 0.045 * k
        r0, r1 = 0.132 + 0.02 * k, 0.15 + 0.022 * k
        parts.append(H._shell([(z, r0, r0 * 1.02, 0, -0.02), (z - 0.05, r1, r1 * 1.02, 0, -0.03)], hm,
                              math.pi * 0.9, math.pi * 2.1, seg=16))
        parts.append(H._shell([(z - 0.045, r1 + 0.003, r1 * 1.02 + 0.003, 0, -0.03),
                               (z - 0.052, r1 + 0.003, r1 * 1.02 + 0.003, 0, -0.03)], lm,
                              math.pi * 0.9, math.pi * 2.1, seg=16))
    # fukigaeshi: the wings turned back beside the face
    for sx in (1, -1):
        parts.append(M.rounded_box(0.025, 0.08, 0.07, hm, (sx * 0.15, 0.06, 0.11), r=0.01, seg=6)
                     .transformed(M.rot_z(sx * 25)))
    # the horns: a tall V from the brow
    for sx in (1, -1):
        horn = M.extrude([(0.0, 0.0), (0.035, 0.0), (0.11, 0.26), (0.085, 0.27)], 0.014, cm)
        parts.append(horn.transformed(M.translate(sx * 0.015, 0.135, 0.17) @ M.scale(sx, 1, 1) @ M.rot_x(-8)))
    parts.append(M.ellipsoid(0.03, 0.02, 0.03, cm, (0, 0.14, 0.17), seg=8))
    if tassels:
        for sx in (1, -1):
            parts.append(M.capsule((sx * 0.03, -0.08, 0.25), (sx * 0.09, -0.15, 0.36), 0.016, 0.008, cm,
                                   seg=5, rings=1))
    return [('head', _h(M.Mesh.concat(parts)), 'hat')]


# ------------------------------------------------------------ armor and clothes

def g_do(g, mat_name='do', color='#2c2c2e', lacing='lacing', lacing_color='#ffffff', lacing_tint=True, rows=4,
         finish='gloss'):
    """A lacquered cuirass (do) from the waist to the armpits, in horizontal
    lames laced together (the lacing civ-colored when the original's is)."""
    dm = g.m(mat_name, color, finish=finish)
    lm = g.m(lacing, lacing_color, tint=lacing_tint)
    gg = 0.032
    rings = [(z, rx + gg, ry + gg, dx, dy) for z, rx, ry, dx, dy in g.chest[:4]]
    rings = [(-0.07, g.chest[0][1] + gg, g.chest[0][2] + gg, 0, 0)] + rings
    out = [('chest', M.loft(rings, dm, seg=16, cap_bottom=False, cap_top=True), None)]
    out.append(('spine', M.loft([(-0.02, 0.165 * g.tw + gg, 0.118 * g.tw + gg), (0.22, 0.18 * g.tw + gg, 0.126 * g.tw + gg)],
                                dm, seg=14, cap_bottom=False, cap_top=False), None))
    zs = [r[0] for r in rings]
    for i in range(rows):
        z = -0.04 + 0.27 * (i + 0.5) / rows
        rx = float(np.interp(z, zs, [r[1] for r in rings])) + 0.004
        ry = float(np.interp(z, zs, [r[2] for r in rings])) + 0.004
        out.append(('chest', M.loft([(z, rx, ry), (z + 0.014, rx, ry)], lm, seg=16, cap_bottom=False, cap_top=False),
                    None))
    # shoulder straps over to the back plate
    for sx in (1, -1):
        out.append(('chest', M.capsule((sx * 0.11 * g.tw, rings[3][2] * 0.8, 0.26), (sx * 0.12 * g.tw, -rings[3][2] * 0.8, 0.26),
                                       0.035, 0.035, dm, seg=6, rings=1), None))
    return out


def g_kusazuri(g, mat_name='do', color='#2c2c2e', lacing='lacing', lacing_color='#ffffff', lacing_tint=True, n=7,
               length=0.24, width=0.13, finish='gloss', spread=1.0, splay=10):
    """The armored skirt: tassets of laced lames hanging from the waist."""
    dm = g.m(mat_name, color, finish=finish)
    lm = g.m(lacing, lacing_color, tint=lacing_tint)
    out = []
    for i in range(n):
        a = 2 * math.pi * (i + 0.5) / n
        rx, ry = 0.205 * g.tw * spread, 0.16 * g.tw * spread
        c = (rx * math.cos(a), ry * math.sin(a), 0.03 - length / 2)
        T = M.translate(*c) @ M.rot_z(math.degrees(a) - 90) @ M.rot_x(-splay)
        out.append(('pelvis', M.rounded_box(width, 0.022, length, dm, (0, 0, 0), r=0.008, seg=6, p=4).transformed(T), None))
        for k in range(2):
            z = length * (0.15 - 0.33 * (k + 1))
            out.append(('pelvis', M.box(width * 1.02, 0.026, 0.014, lm, (0, 0, z)).transformed(T), None))
    return out


def g_sode(g, mat_name='do', color='#2c2c2e', lacing='lacing', lacing_color='#ffffff', lacing_tint=True, size=1.0,
           finish='gloss'):
    """Big square shoulder guards (o-sode) hanging over the upper arms."""
    dm = g.m(mat_name, color, finish=finish)
    lm = g.m(lacing, lacing_color, tint=lacing_tint)
    out = []
    s = size
    for side, sx in (('r', 1), ('l', -1)):
        x = sx * 0.105 * g.lb
        T = M.translate(x, 0, -0.06) @ M.rot_y(sx * 8)
        out.append((f'arm_{side}', M.rounded_box(0.03, 0.24 * s, 0.27 * s, dm, (0, 0, -0.05 * s), r=0.01, seg=6, p=4)
                    .transformed(T), None))
        for k in range(3):
            out.append((f'arm_{side}', M.box(0.036, 0.245 * s, 0.014, lm, (0, 0, 0.04 * s - 0.075 * s * (k + 0.5)))
                        .transformed(T), None))
        out.append((f'arm_{side}', H._dome(0.085 * g.lb, 0.09 * g.lb, 0.07, dm, (0, 0, -0.03), 0.0), None))
    return out


def g_suneate(g, mat_name='suneate', color='#2a2a2c', finish='gloss', band=None):
    """Lacquered shin guards from the ankle to below the knee."""
    sm = g.m(mat_name, color, finish=finish)
    lb = g.lb
    out = []
    for side in 'rl':
        out.append((f'shin_{side}', M.loft([(-0.38, 0.06 * lb, 0.066 * lb, 0, 0.004), (-0.22, 0.068 * lb, 0.076 * lb, 0, 0.008),
                                            (-0.06, 0.072 * lb, 0.08 * lb, 0, 0.008), (-0.03, 0.066 * lb, 0.07 * lb)],
                                           sm, seg=12, cap_bottom=False, cap_top=True), None))
        if band:
            bm = g.m(band, '#e8e4dc')
            for z in (-0.33, -0.10):
                out.append((f'shin_{side}', M.cylinder(0.072 * lb, 0.072 * lb, 0.022, bm, seg=12, z0=z), None))
    return out


def g_obi(g, mat_name='obi', color='#ffffff', tint=False, z=0.02, h=0.07):
    """A wide sash round the waist (over the kimono or the cuirass)."""
    om = g.m(mat_name, color, tint=tint)
    return [('pelvis', M.loft([(z, 0.19 * g.tw, 0.15 * g.tw), (z + h, 0.188 * g.tw, 0.145 * g.tw)], om, seg=16,
                              cap_bottom=False, cap_top=False), None)]


def g_tool_pouch(g, mat_name='pouch', color='#5a4630', side=-1):
    """A pouch at the hip (the crossbowman's bolt bag)."""
    pm = g.m(mat_name, color)
    return [('pelvis', M.rounded_box(0.10, 0.16, 0.18, pm, (side * 0.21 * g.tw, -0.06, -0.12), r=0.03, seg=8, p=3)
             .transformed(M.rot_z(side * 8)), None)]


def g_ebira(g, mat_name='ebira', color='#3a2c20', arrows=7):
    """A samurai's box quiver on the back (right hip to over the right
    shoulder) with a fan of white-fletched arrows."""
    em = g.m(mat_name, color)
    fm = g.m('fletch', '#ece6d6')
    wm = g.m('shaft', '#b89a68')
    parts = [M.rounded_box(0.16, 0.08, 0.30, em, (0, 0, 0.0), r=0.02, seg=6, p=4)]
    for i in range(arrows):
        x = -0.06 + 0.12 * i / (arrows - 1)
        a = (i - (arrows - 1) / 2) * 5
        sh = M.cylinder(0.007, 0.007, 0.42, wm, seg=4, z0=0.1)
        fl = M.box(0.01, 0.035, 0.10, fm, (0, 0, 0.47))
        parts.append(M.Mesh.concat([sh, fl]).transformed(M.translate(x, 0, 0) @ M.rot_y(a)))
    m = M.Mesh.concat(parts)
    return [('chest', m.transformed(M.translate(0.10, -0.20, -0.05) @ M.rot_y(-22) @ M.rot_x(-8)), None)]


# ------------------------------------------------------------ props

def yumi(mats, draw=0.0, top=1.30, bottom=0.66, bend=0.11, thick=1.6, m='yumi', band='yumi_band', tip='bow_tip'):
    """The Japanese longbow: very tall and asymmetric (the grip a third of
    the way up), its limbs curving back to the archer (-y) and their tips
    flicking forward again. `draw` pulls the string back."""
    wm = mat(mats, m, '#b08a50', spec=0.2, gloss=14)
    bm = mat(mats, band, '#2a2220', spec=0.1)
    tm = mats[tip] if tip in mats else mat(mats, tip, '#2a2220', spec=0.1)
    bend = bend + 0.08 * draw
    n = 13
    pts = []
    for i in range(n):
        u = -1 + 2 * i / (n - 1)
        L = top if u >= 0 else bottom
        z = u * L * (1 - 0.05 * draw)
        a = abs(u)
        y = -bend * (L / top) * (a ** 1.6) * 1.6 + 0.10 * max(0.0, a - 0.86) ** 1.0 * (L / top)
        pts.append((0.0, y, z))
    parts = []
    for i, (a, b) in enumerate(zip(pts[:-1], pts[1:])):
        r = 0.016 * thick * (1 - 0.45 * abs(a[2]) / top)
        mm = tm if i in (0, n - 2) else wm
        parts.append(M.capsule(a, b, r, r * 0.92, mm, seg=6, rings=1))
    parts.append(M.cylinder(0.024 * thick * 0.8, 0.024 * thick * 0.8, 0.13, bm, seg=8, z0=-0.07))
    for zb in (0.45, -0.32, 0.9):
        parts.append(M.cylinder(0.019 * thick * 0.8, 0.019 * thick * 0.8, 0.025, bm, seg=6, z0=zb).transformed(
            M.translate(0, float(np.interp(zb, [p[2] for p in pts], [p[1] for p in pts])), 0)))
    sm = mat(mats, 'string', '#e8e0c8', spec=0.0)
    t_, b_ = pts[-1], pts[0]
    mid = (0, min(t_[1], b_[1]) - 0.04 - 0.58 * draw, 0.02)
    parts.append(M.capsule(b_, mid, 0.005, 0.005, sm, seg=4, rings=1))
    parts.append(M.capsule(mid, t_, 0.005, 0.005, sm, seg=4, rings=1))
    return M.Mesh.concat(parts)


def crossbow(mats, spanned=1.0, bolt=True, length=0.82, span=0.80, thick=1.5, m='stock', prod='prod', band='band'):
    """A crossbow held at its trigger grip: the stock along +z (forward),
    its top (the bolt groove) at -y, the bow (prod) across the front, the
    string drawn back to the nut (`spanned` 1) or loose (0)."""
    sm = mat(mats, m, '#6a4a30', spec=0.1)
    pm = mat(mats, prod, '#4e3a2a', spec=0.12)
    bm = mats[band] if band in mats else mat(mats, band, '#2a2420')
    st = steel(mats)
    k = thick
    parts = [M.box(0.045 * k, 0.055 * k, length, sm, (0, 0.005, length / 2 - 0.26))]
    # the butt, deeper below (+y)
    parts.append(M.box(0.05 * k, 0.09 * k, 0.16, sm, (0, 0.03, -0.20)))
    zf = length - 0.30
    # the prod: two limbs from the front, sweeping back to the tips
    tips = []
    for sx in (1, -1):
        pts = []
        for i in range(6):
            u = i / 5
            pts.append((sx * span / 2 * u, -0.005, zf - 0.10 * u * u))
        for a, b in zip(pts[:-1], pts[1:]):
            parts.append(M.capsule(a, b, 0.024 * k * (1 - 0.4 * abs(a[0]) / (span / 2)), 0.02 * k, pm, seg=6, rings=1))
        tips.append(pts[-1])
    parts.append(M.box(0.09 * k, 0.07 * k, 0.07, bm, (0, 0.0, zf)))     # the binding at the prod
    parts.append(M.box(0.05 * k, 0.03, 0.05, st, (0, 0.05, 0.02)))     # trigger plate
    parts.append(M.capsule((0, 0.04, 0.0), (0, 0.17, -0.05), 0.012, 0.01, st, seg=5, rings=1))  # trigger lever
    nut = (0.0, -0.04, 0.06)
    loose = (0.0, -0.01, zf - 0.10)
    s = float(np.clip(spanned, 0, 1))
    mid = tuple(_v(nut) * s + _v(loose) * (1 - s))
    strm = mat(mats, 'string', '#e8e0c8', spec=0.0)
    for tp in tips:
        parts.append(M.capsule(tp, mid, 0.006, 0.006, strm, seg=4, rings=1))
    if bolt:
        bt = mat(mats, 'bolt', '#c8b088', spec=0.05)
        z0 = mid[2]
        parts.append(M.cylinder(0.01, 0.01, 0.40, bt, seg=5, z0=0.0).transformed(M.translate(0, -0.05, z0)))
        parts.append(M.cone(0.018, 0.06, st, seg=6, z0=0.40).transformed(M.translate(0, -0.05, z0)))
    return M.Mesh.concat(parts)


def flying_bolt(mats):
    bt = mat(mats, 'bolt', '#c8b088', spec=0.05)
    return M.Mesh.concat([M.cylinder(0.012, 0.012, 0.40, bt, seg=5, z0=-0.4),
                          M.cone(0.02, 0.06, steel(mats), seg=6, z0=0.0)])


def tanegashima(mats, muzzle=1.12):
    """The Japanese matchlock: a long octagonal barrel bedded in a stock
    that runs nearly to the muzzle, a short slim butt held to the cheek,
    brass bands and a brass lock, the serpentine on the right."""
    s = mat(mats, 'stock', '#5e3020', spec=0.25, gloss=16)
    br = mat(mats, 'brass', '#a07a34', spec=0.7, gloss=24, metal=True)
    gm = G.gunmetal(mats)
    parts = [G._stock_wood(mats, -0.26, muzzle - 0.10, depth=0.055, wrist=0.03, m=s)]
    parts.append(M.cylinder(0.019, 0.017, muzzle + 0.02, gm, seg=8, z0=-0.02))
    parts.append(M.cylinder(0.026, 0.026, 0.05, br, seg=8, z0=muzzle - 0.05))
    for z in (0.28, 0.55, 0.82):
        parts.append(M.cylinder(0.03, 0.03, 0.022, br, seg=8, z0=z).transformed(M.translate(0, 0.006, 0)))
    parts.append(M.box(0.018, 0.035, 0.12, br, (0.028, 0.0, 0.03)))
    parts.append(M.capsule((0.032, 0.0, -0.01), (0.032, -0.06, 0.05), 0.008, 0.006, br, seg=5, rings=1))
    parts.append(M.box(0.04, 0.02, 0.04, br, (0.0, -0.022, 0.06)))     # the pan
    return M.Mesh.concat(parts)


GUN_SPECS = {'tanegashima': {'muzzle': 1.12, 'support': (0.06, 0.42), 'butt': -0.26}}


# ------------------------------------------------------------ the crossbowman

def _kneel(**kw):
    k = {'root_pos': (0, -0.05, -0.40), 'foot_l': (-0.18, 0.30, A), 'foot_r': (0.17, -0.34, 0.10), 'toe_r': 40.0,
         'fyaw_r': -20.0, 'spine': (-6, 0, 0), 'chest': (-4, 0, -10), 'head': (4, 0, 8)}
    k.update(kw)
    return k


def anim_crossbow(base, rel=0.6):
    """Kneels with the crossbow low, raises it and aims along the stock,
    looses the bolt (the string snaps forward), lowers it and spans it again
    (a fresh bolt laid in) as he kneels back at the ready."""
    B = base
    aim_dir = FA.dir_euler((0.0, 1.0, 0.03))
    low_dir = FA.dir_euler((0.25, 1.0, -0.35))
    low = _kneel(hand_r=(0.22, 0.20, 0.66), w_main=low_dir, elbow_r=(0.7, -0.6, -0.4), span=1.0, bolt=1.0)
    aim = _kneel(hand_r=(0.15, 0.24, 1.04), w_main=aim_dir, elbow_r=(0.9, -0.2, -0.3), head=(10, 0, -4),
                 chest=(-6, 0, -8), span=1.0, bolt=1.0)
    shot = dict(aim, span=0.0, bolt=0.0)
    kick = dict(shot, hand_r=(0.15, 0.16, 1.07), w_main=FA.dir_euler((0.0, 1.0, 0.12)), chest=(0, 0, -8))
    reload_ = _kneel(hand_r=(0.20, 0.30, 0.55), w_main=FA.dir_euler((0.15, 0.4, -1.0)), chest=(-14, 0, -6),
                     spine=(-14, 0, 0), head=(18, 0, 0), span=0.0, bolt=0.0)
    k = [(0.0, low), (0.30, aim), (rel - 0.001, aim), (rel, shot), (rel + 0.06, kick), (rel + 0.14, shot),
         (min(0.92, rel + 0.26), reload_), (min(0.96, rel + 0.33), dict(reload_, span=1.0, bolt=1.0)),
         (1.0, low)]
    anim = H.keys(dict(B, span=np.float32(1.0), bolt=np.float32(1.0)), k)

    def fn(t):
        p = anim(t)
        u = (t - rel) / 0.12
        p['bolt_fly'] = np.float32(u if 0.0 <= u < 1.0 else -1.0)
        return p
    return H.Action(fn)


def anim_fortify_kneel(base):
    low = _kneel(hand_r=(0.22, 0.22, 0.66), w_main=FA.dir_euler((0.3, 1.0, -0.25)), elbow_r=(0.7, -0.6, -0.4))
    anim = H.keys(base, [(0.0, {}), (0.6, low), (1.0, low)])
    return H.Action(anim)


@archetype('mdj_footman')
class MdjFootman(FA.Footman):
    """The footman with a crossbow (catalog 'crossbow': the weapon name of
    a crossbow prop), spanned and loaded per frame by the 'span' and 'bolt'
    pose channels, and its animations."""

    def __init__(self, unit, params):
        super().__init__(unit, params)
        self._span = 1.0
        self._bolt = True

    def action(self, action, ctx):
        name = self.anim_name(action)
        if name == 'attack_crossbow':
            ev = self.p.get('events', {}).get(action, {})
            rel = ev.get('release', 0.6)
            return anim_crossbow(self.base, rel)
        if name == 'fortify_kneel':
            return anim_fortify_kneel(self.base)
        return super().action(action, ctx)

    def prop(self, name, **kw):
        if name is not None and name == self.p.get('crossbow') and not kw:
            kw = {'spanned': self._span, 'bolt': self._bolt}
        return super().prop(name, **kw)

    def _frame(self, action, f, n, ctx):
        act = self.action(action, ctx)
        t = rig.frame_times(n, act.loop)[f]
        pose = act.fn(t)
        self._span = round(float(np.clip(pose.get('span', 1.0), 0, 1)) * 4) / 4
        self._bolt = bool(float(pose.get('bolt', 1.0)) > 0.5)
        mesh = super()._frame(action, f, n, ctx)
        u = float(pose.get('bolt_fly', -1.0))
        if u >= 0.0:
            R = M.euler(*_v(pose['w_main']))
            d = R[:3, 2]
            p0 = _v(pose['hand_r']) * _v((1, 1, self.h)) + R[:3, :3] @ _v((0, -0.05, 0.55))
            pos = p0 + d * (0.2 + 2.6 * u)
            fly = self.prop('flying_bolt_p') if 'flying_bolt_p' in self.prop_over else flying_bolt(self.mats)
            mesh = M.Mesh.concat([mesh, fly.transformed(M.translate(*pos) @ M.euler(*FA.dir_euler(d)))])
        self._span, self._bolt = 1.0, True
        return mesh


# ------------------------------------------------------------ the arquebusier

@archetype('ashigaru_gunner')
class AshigaruGunner(FM.Soldier):
    """The gunpowder soldier with footman gear ('gear', as for the footman)
    and this module's guns (GUN_SPECS)."""

    def __init__(self, unit, params):
        super().__init__(unit, params)
        g = FA.Gear(self)
        for item in params.get('gear', []):
            name, kw = (item, {}) if isinstance(item, str) else item
            self.parts += FA.GEAR[name](g, **kw)
        if self.gun in GUN_SPECS:
            self.gspec = GUN_SPECS[self.gun]

    def prop(self, name, **kw):
        if name in GUN_SPECS:
            key = ('mdj', name, tuple(sorted(kw.items())))
            if key not in self._props:
                m = globals()[name](self.mats, **kw)
                if name in GUN_SPECS:
                    k = float(self.p.get('gun_thick', 1.6))
                    m = m.transformed(M.scale(k, k, 1.0))
                self._props[key] = m
            return self._props[key]
        return super().prop(name, **kw)


FA.register_extra(sys.modules[__name__])
