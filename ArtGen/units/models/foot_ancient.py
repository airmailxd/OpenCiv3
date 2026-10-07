"""The 'footman' archetype: the humanoid with more headgear, garments, held
props and a few extra animations, for the ancient and medieval foot units
(catalog/foot_ancient.py).

It subclasses models.humanoid.Humanoid and only adds to it:

    gear        list of worn-gear names (or (name, {kwargs})) from GEAR below:
                helmets, hair styles, garments, armor pieces; drawn with
                materials that the catalog can recolor ('colors') and tint
                ('tint') like the humanoid's own.
    props       {humanoid prop name: (my prop name, {kwargs})}: replaces the
                mesh of a prop the humanoid knows (e.g. 'sword' -> 'scimitar',
                'shield_round' -> 'aspis'), keeping its rest pose and handling.
    offhand_mesh  (prop name, {kwargs}) for an offhand bow (it is redrawn per
                frame with the draw).
    twohand     float: the left hand holds the weapon this far below the
                right hand along the haft (two-handed axes, pikes, katanas).
    twohand_actions  INI actions where the two-handed grip applies (default all
                but DEATH).
    sheathed    INI actions in which the weapon is not in the hand.
    finish      {material: 'metal' | 'gloss' | 'matte'}: shading of a material.
    attack_throw  the 'attack_throw' animation: the right-hand weapon (a
                javelin) is hurled and a new one is taken from the left hand.
"""
import math

import numpy as np

import mesh as M
import rig
from models import archetype
from models import humanoid as H
from models import props as P
from models import props_foot_ancient as PF

HEAD = H.HEAD


def dir_euler(d):
    """w_main/w_off channel (pitch, roll, yaw) pointing a prop's +z along d."""
    d = np.asarray(d, np.float32)
    d = d / (np.linalg.norm(d) + 1e-9)
    p = math.degrees(math.acos(float(np.clip(d[2], -1, 1))))
    y = math.degrees(math.atan2(float(d[0]), float(-d[1])))
    return (p, 0.0, y)


def _rgb(c):
    if isinstance(c, str):
        c = c.lstrip('#')
        return tuple(int(c[i:i + 2], 16) for i in (0, 2, 4))
    return tuple(int(v) for v in c)


def finish_colors(model, p):
    """Muted color pass on a model's material table (call after each frame,
    since props add materials lazily): civ-colored materials get the gray
    `tint_shade` (the original flics' civ shades average ~180/255, darker than
    a white material renders), the others are desaturated toward their
    luminance by `desat` (1 = unchanged), so a unit is never more saturated
    than its original. Idempotent; a material re-added with a new color is
    picked up again."""
    mats = model.mats
    shade = _rgb(p.get('tint_shade', '#e2e2e2'))
    ds = float(p.get('desat', 1.0))
    keep = set(p.get('keep_saturation', ()))
    done = model.__dict__.setdefault('_finished_cols', {})
    for i, name in enumerate(mats.names):
        col = tuple(mats.color[i])
        if done.get(name) == col:
            continue
        if mats.tint[i]:
            new = shade
        elif ds != 1.0 and name not in keep:
            L = 0.299 * col[0] + 0.587 * col[1] + 0.114 * col[2]
            new = tuple(int(round(min(255, max(0, L + (c - L) * ds)))) for c in col)
        else:
            new = col
        mats.color[i] = new
        done[name] = new


# ------------------------------------------------------------ worn gear

class Gear:
    """What a gear function needs: materials and the figure's proportions."""

    def __init__(self, fig):
        self.fig = fig
        self.p = fig.p
        self.mats = fig.mats
        bp = H.build_params(fig.p)
        bulk = bp.get('bulk', 1.0)
        self.tw = H.TORSO_W * bulk
        self.lb = H.LIMB * bulk
        tw = self.tw
        rings = [(0.0, 0.165, 0.105, 0, 0.0), (0.10, 0.19, 0.12, 0, 0.012), (0.19, 0.205, 0.122, 0, 0.008),
                 (0.25, 0.185, 0.108, 0, -0.005), (0.30, 0.10, 0.07, 0, -0.01), (0.33, 0.05, 0.05, 0, -0.01)]
        rings = [(z, rx * tw, ry * (1 + (tw - 1) * 0.6), dx, dy) for z, rx, ry, dx, dy in rings]
        mus = H.muscle_amount(fig.p)
        if mus > 0:
            rings = [(z, rx * (1 + 0.09 * mus * (0.10 <= z <= 0.26)), ry * (1 + 0.10 * mus * (0.05 <= z <= 0.26)),
                      dx, dy + 0.012 * mus * (0.08 <= z <= 0.22)) for z, rx, ry, dx, dy in rings]
        self.chest = rings
        self.mus = mus

    def m(self, name, color, tint=False, finish=None):
        """A material by name: the catalog's color/tint win over the defaults."""
        fig = self.fig
        is_tint = name in fig.body.tint or (tint and name not in fig.p.get('colors', {}))
        if name in self.mats and (name in fig.p.get('colors', {}) or name in fig.body.tint):
            return self.mats[name]
        if name in self.mats:
            return self.mats[name]
        col = fig.p.get('colors', {}).get(name, color)
        fin = fig.p.get('finish', {}).get(name, finish)
        return _add(self.mats, name, col, is_tint, fin)


def _add(mats, name, col, tint, finish):
    if tint:
        return mats.add(name, '#ffffff', tint=True, spec=0.05)
    if finish == 'metal':
        return mats.add(name, col, spec=0.85, gloss=28, metal=True)
    if finish == 'gloss':
        return mats.add(name, col, spec=0.35, gloss=18)
    return mats.add(name, col, spec=0.05, gloss=10)


def _h(mesh):
    return mesh.transformed(M.scale(HEAD))


def _plume(g, mat, base, tip, r0=0.03, n=5, spread=0.03):
    """A bunch of feathers from base to tip (head space)."""
    out = []
    base, tip = np.asarray(base, np.float32), np.asarray(tip, np.float32)
    for i in range(n):
        a = 2 * math.pi * i / n
        off = np.array([spread * math.cos(a), spread * math.sin(a) * 0.6, 0.0], np.float32)
        out.append(M.capsule(base, tip + off * 2.2, r0, r0 * 0.25, mat, seg=6, rings=1))
    return M.Mesh.concat(out)


def g_corinthian(g, crest='transverse'):
    """A bronze Corinthian helmet covering most of the face, with a tall
    civ-colored crest on a stalk (hoplite)."""
    hm = g.m('helmet', '#c89a48', finish='metal')
    cm = g.m('crest', '#ffffff', tint=True)
    dark = g.m('visor', '#20160c')
    parts = [H._dome(0.118, 0.13, 0.15, hm, (0, 0.0, 0.11), -0.75)]
    # eye slits and the nose guard
    parts.append(M.rounded_box(0.13, 0.03, 0.03, dark, (0, 0.118, 0.13), r=0.01, seg=6))
    parts.append(M.rounded_box(0.026, 0.03, 0.10, hm, (0, 0.135, 0.10), r=0.01, seg=6))
    parts.append(M.rounded_box(0.05, 0.03, 0.07, dark, (0, 0.12, 0.02), r=0.012, seg=6))
    for sx in (1, -1):
        parts.append(M.ellipsoid(0.03, 0.06, 0.07, hm, (sx * 0.075, 0.08, 0.04), seg=8))
    # the crest: a stalk and a tall arching brush front to back
    parts.append(M.cylinder(0.02, 0.02, 0.05, hm, seg=6, z0=0.25))
    # the long cheek pieces closing over the jaw
    for sx in (1, -1):
        parts.append(M.rounded_box(0.025, 0.07, 0.13, hm, (sx * 0.06, 0.10, 0.03), r=0.012, seg=6)
                     .transformed(M.rot_z(-sx * 25)))
    prof = [(-0.20, 0.0), (-0.16, 0.10), (-0.05, 0.15), (0.08, 0.14), (0.17, 0.08), (0.19, 0.0)]
    parts.append(M.extrude(prof, 0.05, cm).transformed(M.translate(0, -0.02, 0.27) @ M.rot_z(90)))
    return [('head', _h(M.Mesh.concat(parts)), None)]


def g_galea(g):
    """A Roman galea: steel bowl, cheek guards, flared neck guard, brow band and
    a civ-colored crest."""
    hm = g.m('helmet', '#a8a8b0', finish='metal')
    cm = g.m('crest', '#ffffff', tint=True)
    br = g.m('helmet_trim', '#c09040', finish='metal')
    parts = [H._dome(0.116, 0.128, 0.14, hm, (0, -0.005, 0.12), 0.0)]
    parts.append(M.cylinder(0.122, 0.122, 0.025, br, seg=16, z0=0.115).transformed(M.scale(1, 1.08, 1)))
    for sx in (1, -1):
        parts.append(M.rounded_box(0.018, 0.08, 0.12, hm, (sx * 0.105, 0.05, 0.06), r=0.01, seg=8))
    parts.append(M.loft([(0.06, 0.14, 0.08, 0, -0.12), (0.10, 0.12, 0.07, 0, -0.10), (0.14, 0.10, 0.05, 0, -0.08)], hm,
                        seg=12, cap_bottom=False, cap_top=False))
    parts.append(M.extrude([(-0.12, 0.0), (0.12, 0.0), (0.10, 0.06), (0.0, 0.08), (-0.10, 0.06)], 0.045, cm)
                 .transformed(M.translate(0, 0, 0.24) @ M.rot_z(90)))
    return [('head', _h(M.Mesh.concat(parts)), None)]


def g_conical(g, nasal=True):
    """A pointed steel helmet with a brow band and a nose guard (Berserk)."""
    hm = g.m('helmet', '#9a9aa2', finish='metal')
    parts = [M.lathe([(0.11, 0.122), (0.16, 0.118), (0.22, 0.09), (0.28, 0.04), (0.31, 0.003)], hm, seg=16)
             .transformed(M.scale(1, 1.08, 1))]
    parts.append(M.cylinder(0.126, 0.126, 0.028, hm, seg=16, z0=0.11).transformed(M.scale(1, 1.08, 1)))
    if nasal:
        parts.append(M.rounded_box(0.022, 0.025, 0.09, hm, (0, 0.13, 0.095), r=0.008, seg=6))
    return [('head', _h(M.Mesh.concat(parts)), None)]


def g_tiara(g):
    """The Persian soft cap: a white cloth cap with flaps down the cheeks
    and the back of the neck (Immortals)."""
    cm = g.m('cap_cloth', '#ece8da')
    parts = [M.loft([(0.155, 0.114, 0.124, 0, -0.012), (0.20, 0.12, 0.128, 0, -0.014), (0.25, 0.108, 0.118, 0, -0.012),
                     (0.285, 0.07, 0.08, 0, -0.012), (0.30, 0.02, 0.02, 0, -0.01)], cm, seg=14, cap_bottom=False)]
    # flaps over the ears and the back of the neck (open at the face)
    parts.append(H._shell([(0.15, 0.122, 0.132, 0, -0.01), (0.07, 0.118, 0.125, 0, -0.03), (-0.02, 0.105, 0.10, 0, -0.05)],
                          cm, math.pi * 0.92, math.pi * 2.08, seg=14))
    parts.append(H._shell([(0.15, 0.118, 0.128, 0, -0.01), (0.07, 0.114, 0.121, 0, -0.03), (-0.02, 0.101, 0.096, 0, -0.05)],
                          cm, math.pi * 0.92, math.pi * 2.08, seg=14).transformed(M.scale(1, 1, 1)))
    return [('head', _h(M.Mesh.concat(parts)), None)]


def g_feather_crown(g, height=0.16, n=9, band='headband'):
    """A headband with a crown of feathers standing up around the head (Impi)."""
    fm = g.m('feather', '#ffffff', tint=True)
    bm = g.m(band, '#3a2a1c')
    parts = [M.cylinder(0.118, 0.118, 0.04, bm, seg=14, z0=0.14).transformed(M.scale(1, 1.08, 1))]
    for i in range(n):
        a = 2 * math.pi * i / n
        x, y = 0.10 * math.cos(a), 0.11 * math.sin(a)
        tip = (x * 1.6, y * 1.4 - 0.02, 0.18 + height)
        parts.append(M.capsule((x, y, 0.17), tip, 0.035, 0.012, fm, seg=6, rings=1))
    parts.append(M.ellipsoid(0.11, 0.11, 0.07, fm, (0, -0.01, 0.25), seg=10))
    return [('head', _h(M.Mesh.concat(parts)), None)]


def g_jaguar(g):
    """A jaguar's head worn as a helmet: the spotted skull over the head, the
    upper jaw and teeth over the brow, and a feather plume at the back."""
    pm = g.m('jaguar', '#d88a3a')
    sp = g.m('jaguar_spot', '#3a2214')
    th = g.m('teeth', '#f0ead8')
    mo = g.m('jaguar_mouth', '#a8302a')
    fm = g.m('feather', '#ffffff', tint=True)
    parts = [H._dome(0.124, 0.136, 0.15, pm, (0, -0.01, 0.11), -0.45)]
    # the upper jaw: a muzzle over the forehead with teeth hanging down
    parts.append(M.ellipsoid(0.10, 0.10, 0.06, pm, (0, 0.10, 0.215), seg=12))
    parts.append(M.ellipsoid(0.085, 0.07, 0.03, mo, (0, 0.11, 0.17), seg=10))
    for sx in (1, -1):
        parts.append(M.cone(0.012, 0.04, th, seg=5).transformed(M.translate(sx * 0.05, 0.16, 0.19) @ M.rot_x(180)))
        parts.append(M.ellipsoid(0.03, 0.02, 0.035, pm, (sx * 0.08, 0.0, 0.31), seg=6))  # ears
        parts.append(M.ellipsoid(0.016, 0.01, 0.01, g.m('eyes_cat', '#e0c040'), (sx * 0.05, 0.155, 0.245), seg=6))
    parts.append(M.ellipsoid(0.03, 0.02, 0.02, sp, (0, 0.19, 0.225), seg=6))  # nose
    rng = np.random.default_rng(3)
    for i in range(16):
        a = rng.uniform(0, 2 * math.pi)
        e = rng.uniform(0.15, 1.2)
        x, y, z = 0.126 * math.cos(a) * math.cos(e), 0.138 * math.sin(a) * math.cos(e), 0.11 + 0.15 * math.sin(e)
        if y > 0.08 and z < 0.22:
            continue
        parts.append(M.ellipsoid(0.016, 0.016, 0.016, sp, (x * 1.02, y * 1.02 - 0.01, z), seg=5))
    parts.append(_plume(g, fm, (0, -0.10, 0.26), (0, -0.22, 0.48), r0=0.04, n=5, spread=0.05))
    return [('head', _h(M.Mesh.concat(parts)), None)]


def g_feather_headdress(g, tall=0.30, face_paint=True):
    """A Mesoamerican feather headdress: a gold band and a fan of gold and
    civ-colored feathers rising up and back (javelin thrower)."""
    fm = g.m('feather', '#ffffff', tint=True)
    gm = g.m('gold', '#e8b830', finish='gloss')
    parts = [M.cylinder(0.12, 0.12, 0.05, gm, seg=14, z0=0.14).transformed(M.scale(1, 1.08, 1))]
    for i, a in enumerate(np.linspace(-60, 60, 7)):
        r = math.radians(a)
        tip = (0.15 * math.sin(r), -0.06 - 0.10 * math.cos(r) * 0.5, 0.19 + tall * (0.75 + 0.25 * math.cos(r)))
        parts.append(M.capsule((0.06 * math.sin(r), -0.04, 0.20), tip, 0.03, 0.012, gm if i % 2 else fm, seg=6,
                               rings=1))
    parts.append(M.ellipsoid(0.04, 0.03, 0.04, fm, (0, 0.125, 0.17), seg=8))
    if face_paint:
        parts.append(M.rounded_box(0.17, 0.03, 0.05, g.m('paint', '#a02a20'), (0, 0.098, 0.125), r=0.015, seg=6))
    return [('head', _h(M.Mesh.concat(parts)), None)]


def g_attic(g, crest=False):
    """A gilded Greek-style helmet with a brow peak and cheek pieces (Libyan
    mercenary)."""
    hm = g.m('helmet', '#d8b050', finish='metal')
    parts = [H._dome(0.118, 0.13, 0.15, hm, (0, -0.005, 0.11), -0.1)]
    parts.append(M.ellipsoid(0.12, 0.05, 0.02, hm, (0, 0.10, 0.165), seg=10))
    for sx in (1, -1):
        parts.append(M.rounded_box(0.02, 0.07, 0.11, hm, (sx * 0.105, 0.06, 0.06), r=0.01, seg=8))
    parts.append(M.loft([(0.04, 0.13, 0.09, 0, -0.10), (0.10, 0.12, 0.08, 0, -0.08)], hm, seg=12, cap_bottom=False,
                        cap_top=False))
    if crest:
        cm = g.m('crest', '#ffffff', tint=True)
        parts.append(M.extrude([(-0.12, 0.0), (0.12, 0.0), (0.0, 0.08)], 0.04, cm).transformed(M.translate(0, 0, 0.25) @ M.rot_z(90)))
    return [('head', _h(M.Mesh.concat(parts)), None)]


def g_kettle(g, brim=0.05, comb=False, peaks=False):
    """A steel kettle hat / morion: a bowl and a brim (curving up front and
    back for a morion, with a comb on top)."""
    hm = g.m('helmet', '#a8a8b0', finish='metal')
    parts = [H._dome(0.12, 0.13, 0.15, hm, (0, -0.005, 0.13), 0.0)]
    k = 20
    rings = []
    r0x, r0y = 0.125, 0.135
    for i in range(k):
        a = 2 * math.pi * i / k
        a1 = 2 * math.pi * (i + 1) / k
        up0 = (0.07 * abs(math.sin(a)) ** 2) if peaks else 0.0
        up1 = (0.07 * abs(math.sin(a1)) ** 2) if peaks else 0.0
        ex0 = (1.25 if peaks else 1.0)
        p0 = ((r0x + brim * 0.8) * math.cos(a), (r0y + brim * ex0 * (1 + 0.6 * abs(math.sin(a)) * peaks)) * math.sin(a),
              0.13 + up0)
        p1 = ((r0x + brim * 0.8) * math.cos(a1), (r0y + brim * ex0 * (1 + 0.6 * abs(math.sin(a1)) * peaks)) * math.sin(a1),
              0.13 + up1)
        rings.append(M.plate([(r0x * math.cos(a) * 0.95, r0y * math.sin(a) * 0.95, 0.13), p0, p1,
                              (r0x * math.cos(a1) * 0.95, r0y * math.sin(a1) * 0.95, 0.13)], hm, thick=0.008))
    parts += rings
    if comb:
        parts.append(M.extrude([(-0.11, 0.0), (0.11, 0.0), (0.06, 0.06), (-0.06, 0.06)], 0.014, hm)
                     .transformed(M.translate(0, 0, 0.25) @ M.rot_z(90)))
    return [('head', _h(M.Mesh.concat(parts)), None)]


def g_plume(g, base=(0.0, -0.06, 0.27), tip=(0.0, -0.22, 0.42), n=5, r0=0.04, spread=0.04, mat='plume'):
    """A big civ-colored feather plume on a helmet."""
    fm = g.m(mat, '#ffffff', tint=True)
    return [('head', _h(_plume(g, fm, base, tip, r0=r0, n=n, spread=spread)), None)]


def g_coif(g):
    """A mail coif over head, neck and shoulders, under a pointed steel cap
    (medieval infantry)."""
    mm = g.m('mail', '#8a8a92', finish='metal')
    hm = g.m('helmet', '#b0b0b8', finish='metal')
    parts = [M.ellipsoid(0.118, 0.13, 0.14, mm, (0, -0.012, 0.12), seg=14)]
    parts.append(M.loft([(-0.12, 0.16, 0.13), (-0.05, 0.12, 0.11), (0.04, 0.11, 0.115)], mm, seg=14, cap_bottom=False,
                        cap_top=False))
    parts.append(M.lathe([(0.15, 0.124), (0.20, 0.11), (0.26, 0.07), (0.30, 0.02), (0.315, 0.002)], hm, seg=14)
                 .transformed(M.scale(1, 1.06, 1)))
    parts.append(M.rounded_box(0.022, 0.025, 0.09, hm, (0, 0.135, 0.10), r=0.008, seg=6))
    # the face opening: skin shows through a hole we fake with a skin-colored oval
    return [('head', _h(M.Mesh.concat(parts)), None)]


def g_topknot(g):
    """A samurai's chonmage: shaved crown, hair at the sides and back, a
    topknot laid forward over the crown."""
    hm = g.m('hair', '#1a1614')
    parts = [M.ellipsoid(0.104, 0.116, 0.10, hm, (0, -0.02, 0.13), seg=14)]
    parts.append(M.capsule((0, -0.05, 0.25), (0, 0.06, 0.25), 0.022, 0.018, hm, seg=8, rings=2))
    parts.append(M.ellipsoid(0.03, 0.03, 0.03, hm, (0, -0.06, 0.24), seg=8))
    return [('head', _h(M.Mesh.concat(parts)), None)]


def g_ninja_hood(g):
    """A full hood and mask, only the eyes showing."""
    hm = g.m('hood', '#2c3020')
    sk = g.m('skin', '#c98a58')
    parts = [M.ellipsoid(0.112, 0.124, 0.135, hm, (0, 0.008, 0.115), seg=14)]
    parts.append(M.loft([(-0.08, 0.11, 0.10), (0.02, 0.105, 0.11), (0.08, 0.1, 0.11)], hm, seg=14, cap_bottom=False,
                        cap_top=False))
    parts.append(M.rounded_box(0.12, 0.03, 0.035, sk, (0, 0.112, 0.125), r=0.012, seg=6))
    for sx in (1, -1):
        parts.append(M.ellipsoid(0.016, 0.008, 0.01, g.m('eyes', '#2a1a12'), (sx * 0.035, 0.128, 0.127), seg=6))
    return [('head', _h(M.Mesh.concat(parts)), None)]


def g_cowl(g):
    """A monk's deep pointed cowl (inquisitor), the face in its shadow."""
    hm = g.m('hood', '#4a4a4c')
    sh = g.m('cowl_inside', '#141416')
    parts = [M.ellipsoid(0.13, 0.14, 0.15, hm, (0, -0.02, 0.125), seg=16)]
    parts.append(M.capsule((0, -0.06, 0.22), (0, -0.16, 0.30), 0.06, 0.01, hm, seg=8, rings=1))
    parts.append(M.loft([(-0.10, 0.17, 0.16), (-0.02, 0.14, 0.13), (0.06, 0.13, 0.12)], hm, seg=14, cap_bottom=False,
                        cap_top=False))
    # the shadowed opening
    parts.append(M.ellipsoid(0.085, 0.03, 0.10, sh, (0, 0.105, 0.10), seg=10))
    return [('head', _h(M.Mesh.concat(parts)), None)]


def g_ponytail(g):
    """A bald head with a tuft and a long tail of hair at the back (scout)."""
    hm = g.m('hair', '#2a1e14')
    parts = [M.ellipsoid(0.04, 0.04, 0.03, hm, (0, -0.08, 0.20), seg=8)]
    parts.append(M.capsule((0, -0.09, 0.20), (0, -0.16, 0.05), 0.03, 0.022, hm, seg=8, rings=1))
    parts.append(M.capsule((0, -0.16, 0.05), (0, -0.14, -0.12), 0.022, 0.012, hm, seg=8, rings=1))
    return [('head', _h(M.Mesh.concat(parts)), None)]


def g_spiky(g):
    """Limed hair combed up and back in spikes, and a drooping moustache (Gaul)."""
    hm = g.m('hair', '#d07a30')
    parts = [M.ellipsoid(0.108, 0.12, 0.11, hm, (0, -0.01, 0.14), seg=14)]
    for x, y, z, tx, ty, tz in ((0, 0.06, 0.22, 0, 0.0, 0.36), (0.06, 0.03, 0.22, 0.12, -0.04, 0.33),
                                (-0.06, 0.03, 0.22, -0.12, -0.04, 0.33), (0.03, -0.04, 0.23, 0.06, -0.14, 0.34),
                                (-0.03, -0.04, 0.23, -0.06, -0.14, 0.34), (0, -0.08, 0.18, 0, -0.2, 0.26),
                                (0.08, -0.06, 0.16, 0.16, -0.14, 0.22), (-0.08, -0.06, 0.16, -0.16, -0.14, 0.22)):
        parts.append(M.capsule((x, y, z), (tx, ty, tz), 0.05, 0.008, hm, seg=7, rings=1))
    for sx in (1, -1):
        parts.append(M.capsule((sx * 0.01, 0.12, 0.075), (sx * 0.06, 0.10, 0.02), 0.014, 0.008, hm, seg=6, rings=1))
    return [('head', _h(M.Mesh.concat(parts)), None)]


def g_long_beard(g, length=0.26, width=0.09):
    """A long squared beard down the chest (Enkidu, Sumer)."""
    hm = g.m('hair', '#1e1814')
    b = M.loft([(-length, width * 0.9, 0.05, 0, 0.12), (-length * 0.5, width, 0.06, 0, 0.11),
                (-0.02, width * 1.0, 0.07, 0, 0.08), (0.08, 0.09, 0.07, 0, 0.05)], hm, seg=12)
    return [('head', _h(b), None)]


def g_braids(g, length=0.30):
    """A full beard ending in two long braids down the chest, long hair
    behind the ears (Berserk)."""
    hm = g.m('hair', '#d8b040')
    parts = [M.ellipsoid(0.09, 0.075, 0.085, hm, (0, 0.06, 0.03), seg=12)]
    for sx in (1, -1):
        parts.append(M.capsule((sx * 0.045, 0.11, -0.02), (sx * 0.06, 0.20, -length), 0.032, 0.022, hm, seg=7, rings=1))
        # long hair down behind the ears
        parts.append(M.capsule((sx * 0.09, -0.03, 0.12), (sx * 0.11, -0.05, -0.08), 0.035, 0.02, hm, seg=7, rings=1))
    return [('head', _h(M.Mesh.concat(parts)), None)]


def g_streamers(g, mat='headband'):
    """A headband with two ribbons flying from the back."""
    bm = g.m(mat, '#ffffff', tint=True)
    parts = [M.cylinder(0.118, 0.118, 0.04, bm, seg=14, z0=0.15).transformed(M.scale(1, 1.08, 1))]
    for sx in (1, -1):
        parts.append(M.capsule((sx * 0.02, -0.12, 0.17), (sx * 0.10, -0.24, 0.30), 0.016, 0.01, bm, seg=5, rings=1))
    return [('head', _h(M.Mesh.concat(parts)), None)]


def g_inca_headdress(g):
    """A Chasqui's feathered cap: a band and a tall crest of white, gold and
    civ-colored feathers."""
    fm = g.m('feather', '#ffffff', tint=True)
    wm = g.m('feather_white', '#ece8e0')
    gm = g.m('gold', '#e8b830', finish='gloss')
    parts = [M.cylinder(0.12, 0.12, 0.05, gm, seg=14, z0=0.14).transformed(M.scale(1, 1.08, 1))]
    parts.append(H._dome(0.112, 0.124, 0.12, fm, (0, -0.01, 0.15), 0.0))
    for i, a in enumerate(np.linspace(-50, 50, 6)):
        r = math.radians(a)
        parts.append(M.capsule((0.05 * math.sin(r), 0.0, 0.24), (0.11 * math.sin(r), -0.03, 0.42 - 0.04 * abs(math.sin(r))),
                               0.028, 0.01, wm if i % 2 else fm, seg=6, rings=1))
    for sx in (1, -1):
        parts.append(M.ellipsoid(0.02, 0.02, 0.04, gm, (sx * 0.11, 0.0, 0.06), seg=6))  # ear spools
    return [('head', _h(M.Mesh.concat(parts)), None)]


# -- body

def g_trousers(g, mat='trousers', baggy=1.0, color='#ffffff', tint=False, stripes=None, checks=None, knee=True):
    """Trousers: fuller thighs and shins (baggy > 1 balloons them)."""
    tm = g.m(mat, color, tint=tint)
    lb = g.lb
    out = []
    for side in 'rl':
        out.append((f'thigh_{side}', M.capsule((0, 0, 0.02), (0, 0, -H.THIGH), 0.10 * lb * baggy, 0.075 * lb * baggy, tm,
                                                seg=12, rings=2), None))
        top = M.loft([(-0.04, 0.17 * g.tw, 0.125 * g.tw), (0.06, 0.168 * g.tw, 0.122 * g.tw)], tm, seg=14,
                     cap_bottom=False, cap_top=False)
        out.append(('pelvis', M.ellipsoid(0.175 * g.tw, 0.13 * g.tw, 0.11, tm, (0, 0, -0.03), seg=14), None))
        if knee:
            out.append((f'shin_{side}', M.capsule((0, 0, 0.02), (0, 0, -SHIN_COVER), 0.078 * lb * baggy, 0.06 * lb * baggy,
                                                   tm, seg=12, rings=2), None))
        if stripes:
            sm = g.m(stripes, '#d8b880')
            for a in (0, 90, 180, 270):
                r = math.radians(a)
                for bone, z0, z1, r0, r1 in ((f'thigh_{side}', 0.0, -H.THIGH, 0.10, 0.076),
                                             (f'shin_{side}', 0.0, -SHIN_COVER, 0.079, 0.061)):
                    if bone.startswith('shin') and not knee:
                        continue
                    k0, k1 = r0 * lb * baggy * 1.01, r1 * lb * baggy * 1.01
                    out.append((bone, M.capsule((k0 * math.cos(r), k0 * math.sin(r), z0 - 0.02),
                                                (k1 * math.cos(r), k1 * math.sin(r), z1 + 0.02), 0.012, 0.012, sm,
                                                seg=5, rings=1), None))
        if checks:
            cm = g.m(checks, '#1a1a30')
            for z, r in ((-0.08, 0.098), (-0.2, 0.09), (-0.32, 0.08)):
                out.append((f'thigh_{side}', M.cylinder(r * lb * baggy * 1.02, r * lb * baggy * 1.02, 0.03, cm, seg=12,
                                                         z0=z), None))
            for a in (45, 135, 225, 315):
                rr = math.radians(a)
                k0, k1 = 0.101 * lb * baggy, 0.077 * lb * baggy
                out.append((f'thigh_{side}', M.capsule((k0 * math.cos(rr), k0 * math.sin(rr), -0.02),
                                                        (k1 * math.cos(rr), k1 * math.sin(rr), -H.THIGH + 0.03), 0.014,
                                                        0.014, cm, seg=5, rings=1), None))
            if knee:
                for z, r in ((-0.06, 0.077), (-0.18, 0.07)):
                    out.append((f'shin_{side}', M.cylinder(r * lb * baggy * 1.03, r * lb * baggy * 1.03, 0.025, cm,
                                                            seg=12, z0=z), None))
    return out


SHIN_COVER = 0.30


def g_sleeves(g, mat='sleeve', color='#ffffff', tint=False, forearm=True, wide=1.0, puffy=1.0, finish=None):
    """Sleeves over upper arms (and forearms), optionally puffed or wide at
    the cuff (kimono)."""
    sm = g.m(mat, color, tint=tint, finish=finish)
    lb = g.lb
    out = []
    for side in 'rl':
        out.append((f'arm_{side}', M.sphere(0.085 * lb * puffy, sm, (0, 0, -0.01), seg=10), None))
        out.append((f'arm_{side}', M.capsule((0, 0, 0), (0, 0, -H.UPPER_ARM + 0.02), 0.074 * lb * puffy,
                                              0.06 * lb * max(1.0, puffy * 0.9), sm, seg=10, rings=2), None))
        if forearm:
            if wide > 1.0:
                out.append((f'forearm_{side}', M.loft([(-0.20, 0.06 * wide * lb, 0.075 * wide * lb, 0, -0.03 * wide),
                                                        (-0.08, 0.065 * wide * lb, 0.07 * wide * lb, 0, -0.02 * wide),
                                                        (0.02, 0.06 * lb, 0.06 * lb)], sm, seg=10), None))
            else:
                out.append((f'forearm_{side}', M.capsule((0, 0, 0), (0, 0, -H.FOREARM + 0.04), 0.058 * lb, 0.048 * lb,
                                                          sm, seg=10, rings=2), None))
    return out


def g_bands(g, mat='armband', color='#ffffff', tint=True, where=('arm',), r=None):
    """Rings around limbs: 'arm' (upper arm), 'wrist', 'knee' (below the knee),
    'ankle'."""
    bm = g.m(mat, color, tint=tint)
    lb = g.lb
    out = []
    spec = {'arm': ('arm_{}', -0.07, 0.07, 0.07), 'wrist': ('forearm_{}', -0.21, 0.054, 0.05),
            'knee': ('shin_{}', -0.07, 0.07, 0.06), 'ankle': ('shin_{}', -0.37, 0.058, 0.05),
            'forearm': ('forearm_{}', -0.14, 0.064, 0.08)}
    for w in where:
        bone, z, rr, h = spec[w]
        rr = (r or rr) * lb
        for side in 'rl':
            out.append((bone.format(side), M.cylinder(rr, rr, h, bm, seg=12, z0=z - h / 2), None))
    return out


def g_tufts(g, mat='tufts', where=('knee', 'ankle')):
    """Fur/cow-tail tufts around the calves and ankles (Impi)."""
    tm = g.m(mat, '#ffffff', tint=True)
    lb = g.lb
    out = []
    for side in 'rl':
        for w in where:
            z = -0.08 if w == 'knee' else -0.34
            r = 0.075 if w == 'knee' else 0.06
            for i in range(8):
                a = 2 * math.pi * i / 8
                c = (r * lb * math.cos(a), r * lb * math.sin(a), z)
                out.append((f'shin_{side}', M.ellipsoid(0.03, 0.03, 0.06, tm, (c[0], c[1], c[2] - 0.03), seg=6), None))
    return out


def g_fur_vest(g, mat='fur', color='#5a4028', collar=True):
    """A shaggy fur vest over the torso, with a thick collar (Berserk)."""
    fm = g.m(mat, color)
    gg = 0.03
    rings = [(z, rx + gg, ry + gg, dx, dy) for z, rx, ry, dx, dy in g.chest[:4]]
    out = [('chest', M.loft(rings, fm, seg=16, cap_bottom=False, cap_top=True), None)]
    out.append(('spine', M.loft([(-0.02, 0.17 * g.tw + gg, 0.125 * g.tw + gg), (0.22, 0.18 * g.tw + gg, 0.13 * g.tw + gg)],
                                fm, seg=14, cap_bottom=False, cap_top=False), None))
    if collar:
        out.append(('chest', M.loft([(0.24, g.chest[3][1] + 0.05, g.chest[3][2] + 0.05), (0.30, 0.15 * g.tw, 0.12),
                                     (0.34, 0.09, 0.08)], fm, seg=14, cap_bottom=True, cap_top=False), None))
    rng = np.random.default_rng(11)
    for i in range(14):
        a = rng.uniform(0, 2 * math.pi)
        z = rng.uniform(0.0, 0.24)
        r = np.interp(z, [c[0] for c in g.chest[:4]], [c[1] for c in g.chest[:4]]) + gg
        ry = np.interp(z, [c[0] for c in g.chest[:4]], [c[2] for c in g.chest[:4]]) + gg
        out.append(('chest', M.ellipsoid(0.04, 0.03, 0.05, fm, (r * math.cos(a), ry * math.sin(a), z), seg=6), None))
    return out


def g_segmentata(g):
    """Lorica segmentata: horizontal steel bands round the torso and plates
    over the shoulders."""
    sm = g.m('plate', '#b8b8c0', finish='metal')
    out = []
    gg = 0.03
    for i, z in enumerate((0.0, 0.055, 0.11, 0.165, 0.215)):
        rx = float(np.interp(z, [c[0] for c in g.chest], [c[1] for c in g.chest])) + gg
        ry = float(np.interp(z, [c[0] for c in g.chest], [c[2] for c in g.chest])) + gg
        out.append(('chest', M.loft([(z - 0.002, rx * 0.985, ry * 0.985), (z + 0.045, rx, ry), (z + 0.05, rx * 0.97, ry * 0.97)],
                                    sm, seg=16, cap_bottom=False, cap_top=False), None))
    rx = g.chest[3][1] + gg
    ry = g.chest[3][2] + gg
    out.append(('chest', M.loft([(0.25, rx, ry), (0.29, rx * 0.75, ry * 0.85), (0.33, 0.07, 0.06)], sm, seg=16,
                                cap_bottom=False, cap_top=True), None))
    for side in 'rl':
        for k, z in enumerate((0.03, -0.03, -0.09)):
            out.append((f'arm_{side}', H._dome(0.09 * g.lb, 0.092 * g.lb, 0.07, sm, (0, 0, z - 0.03), 0.0), None))
    return out


def g_pauldrons(g, mat='plate', color='#b8b8c0', finish='metal', size=1.0):
    pm = g.m(mat, color, finish=finish)
    out = []
    for side in 'rl':
        out.append((f'arm_{side}', H._dome(0.095 * g.lb * size, 0.1 * g.lb * size, 0.09 * size, pm, (0, 0, -0.04), -0.02),
                    None))
    return out


def g_pteruges(g, mat='pteruges', color='#c8b088', n=14, length=0.20, z0=0.0, tint=False):
    """A skirt of hanging leather strips from the waist."""
    pm = g.m(mat, color, tint=tint)
    out = []
    for i in range(n):
        a = 2 * math.pi * (i + 0.5) / n
        rx, ry = 0.19 * g.tw, 0.145 * g.tw
        c = (rx * math.cos(a), ry * math.sin(a), z0 - length / 2)
        strip = M.rounded_box(0.06, 0.016, length, pm, (0, 0, 0), r=0.006, seg=6, p=4)
        out.append(('pelvis', strip.transformed(M.translate(*c) @ M.rot_z(math.degrees(a) - 90) @ M.rot_x(-6)), None))
    return out


def g_tassets(g, mat='plate', color='#b0b0b8'):
    """Plate tassets hanging over the thighs (pikeman)."""
    tm = g.m(mat, color, finish='metal')
    out = []
    for side, sx in (('r', 1), ('l', -1)):
        for k in range(3):
            z = -0.03 - 0.07 * k
            out.append((f'thigh_{side}', M.loft([(z - 0.075, 0.11 * g.lb, 0.11 * g.lb), (z, 0.105 * g.lb, 0.105 * g.lb)],
                                                 tm, seg=12, cap_bottom=False, cap_top=True), None))
    return out


def g_shackles(g):
    """Iron cuffs on the wrists and ankles."""
    im = g.m('iron', '#38383c', finish='metal')
    out = []
    for side in 'rl':
        out.append((f'forearm_{side}', M.cylinder(0.055, 0.055, 0.05, im, seg=10, z0=-0.23), None))
        out.append((f'shin_{side}', M.cylinder(0.06, 0.06, 0.045, im, seg=10, z0=-0.36), None))
    return out


def g_cross(g, mat='gold', color='#e0b848', on='chest', z=0.17, big=1.0):
    """A cross on the chest (gold; or civ-colored with mat='emblem')."""
    cm = g.m(mat, color, finish='metal', tint=mat == 'emblem')
    ry = float(np.interp(z, [c[0] for c in g.chest], [c[2] for c in g.chest])) + 0.03 + 0.012 * (big > 1)
    parts = [M.box(0.03 * big, 0.02, 0.15 * big, cm, (0, ry, z)), M.box(0.10 * big, 0.02, 0.03 * big, cm, (0, ry, z + 0.03))]
    return [(on, M.Mesh.concat(parts), None)]


def g_robe(g, mat='robe', color='#4a4a4c', length=0.80, flare=0.16):
    """A long robe from the waist to the ankles (inquisitor); it stays still
    (pelvis space), so it suits slow motion."""
    rm = g.m(mat, color)
    tw = g.tw
    return [('pelvis', M.loft([(-length, (0.19 + flare) * tw, (0.16 + flare) * tw, 0, -0.02),
                               (-length * 0.5, (0.19 + flare * 0.5) * tw, (0.15 + flare * 0.5) * tw),
                               (0.0, 0.185 * tw, 0.135 * tw), (0.12, 0.172 * tw, 0.125 * tw)], rm, seg=20,
                              cap_bottom=True, cap_top=False), None)]


def g_sash_ends(g, mat='belt'):
    """Two ends of the belt hanging at the front-left."""
    bm = g.m(mat, '#ffffff')
    out = []
    for dx in (0.0, 0.05):
        out.append(('pelvis', M.rounded_box(0.045, 0.012, 0.22, bm, (-0.08 * g.tw + dx, 0.14 * g.tw, -0.03),
                                            r=0.005, seg=6, p=4).transformed(M.rot_z(-12)), None))
    return out


def g_collar(g, mat='collar', color='#ece8e0'):
    """A V-shaped white collar of an undergarment (kimono)."""
    cm = g.m(mat, color)
    ry = g.chest[3][2] + 0.03
    out = []
    for sx in (1, -1):
        out.append(('chest', M.capsule((sx * 0.10, ry * 0.75, 0.30), (sx * 0.01, ry * 1.05, 0.10), 0.03, 0.03, cm,
                                       seg=6, rings=1), None))
    return out


def g_katana_hip(g):
    return [('pelvis', PF.katana_sheathed(g.mats).transformed(M.translate(-0.20 * g.tw, 0.04, 0.02) @ M.rot_y(-12)
                                                               @ M.rot_x(-120)), None)]


def g_scabbard(g, side='l'):
    """A sword scabbard hanging at the hip."""
    lm = P.leather(g.mats)
    sx = -1 if side == 'l' else 1
    m = M.capsule((0, 0, 0), (0, 0, -0.62), 0.03, 0.022, lm, seg=8, rings=1)
    return [('pelvis', m.transformed(M.translate(sx * 0.19 * g.tw, -0.02, 0.0) @ M.rot_x(25) @ M.rot_y(-sx * 8)), None)]


def g_loin_flap(g, mat='flap', color='#ffffff', tint=True, trim='gold', length=0.30):
    """A long decorated front flap (and back flap) over a loincloth."""
    fm = g.m(mat, color, tint=tint)
    out = []
    for y, s in ((0.14, 1), (-0.14, -1)):
        out.append(('pelvis', M.rounded_box(0.17 * g.tw, 0.02, length, fm, (0, y * g.tw, -length / 2 + 0.02), r=0.008,
                                            seg=6, p=4).transformed(M.rot_x(-s * 6)), None))
        if trim:
            tm = g.m(trim, '#e8b830', finish='gloss')
            out.append(('pelvis', M.rounded_box(0.175 * g.tw, 0.024, 0.04, tm, (0, y * g.tw, -length + 0.03), r=0.008,
                                                seg=6, p=4).transformed(M.rot_x(-s * 6)), None))
    return out


def g_chest_strap(g, mat='strap', color='#5a3a22', tint=False):
    """A strap diagonally across the chest from the right hip to the left
    shoulder."""
    st = g.m(mat, color, tint=tint)
    c = g.chest
    ry1, ry2 = c[1][2] + 0.03, c[2][2] + 0.03
    rx1 = c[1][1]
    pts = [(rx1 * 0.85, ry1 * 0.55, -0.02), (rx1 * 0.35, ry1 * 1.0, 0.09), (-rx1 * 0.25, ry2 * 1.0, 0.20),
           (-rx1 * 0.62, ry2 * 0.62, 0.31), (-rx1 * 0.7, 0.0, 0.34), (-rx1 * 0.6, -ry2 * 0.7, 0.28),
           (-rx1 * 0.1, -ry2 * 1.0, 0.14), (rx1 * 0.6, -ry1 * 0.8, 0.0)]
    return [('chest', M.Mesh.concat([M.capsule(a, b, 0.026, 0.026, st, seg=6, rings=1) for a, b in zip(pts[:-1], pts[1:])]),
             None)]


def g_tabard_panels(g, mat='top', length=0.42, color='#ffffff'):
    """A surcoat's front and back panels hanging from the waist to the knees."""
    tm = g.m(mat, color)
    out = []
    for y, s in ((0.145, 1), (-0.145, -1)):
        out.append(('pelvis', M.rounded_box(0.30 * g.tw, 0.022, length, tm, (0, y * g.tw, -length / 2 + 0.06), r=0.01,
                                            seg=8, p=4).transformed(M.rot_x(-s * 5)), None))
    return out


def g_cloak(g, mat='cloak', color='#d8c8a0', length=0.95, tint=False):
    """A cloak from the shoulders down the back."""
    cm = g.m(mat, color, tint=tint)
    c = g.chest
    rings = [(c[3][0] + 0.03, c[3][1] + 0.04, c[3][2] + 0.05), (c[1][0], c[2][1] + 0.06, c[2][2] + 0.07),
             (c[0][0] - 0.25, c[2][1] + 0.10, c[2][2] + 0.10, 0, -0.04), (c[0][0] - length + 0.3, c[2][1] + 0.14,
                                                                        c[2][2] + 0.10, 0, -0.10)]
    return [('chest', H._shell(rings, cm, math.pi * 1.02, math.pi * 1.98, seg=14), None)]


def g_pectoral(g, mat='pectoral', color='#ffffff', trim='gold'):
    """A broad collar / pectoral over the upper chest and shoulders (Aztec,
    Maya): civ-colored with a gold edge."""
    pm = g.m(mat, color, tint=True)
    c = g.chest
    rings = [(c[2][0] + 0.03, c[2][1] + 0.022, c[2][2] + 0.024), (c[3][0], c[3][1] + 0.024, c[3][2] + 0.026),
             (c[4][0], c[4][1] + 0.03, c[4][2] + 0.03), (c[5][0] + 0.02, 0.07, 0.065)]
    out = [('chest', M.loft(rings, pm, seg=16, cap_bottom=False, cap_top=False), None)]
    if trim:
        tm = g.m(trim, '#e8b830', finish='gloss')
        out.append(('chest', M.loft([(c[2][0] + 0.02, c[2][1] + 0.028, c[2][2] + 0.03),
                                     (c[2][0] + 0.045, c[2][1] + 0.026, c[2][2] + 0.028)], tm, seg=16,
                                    cap_bottom=False, cap_top=False), None))
    return out


def g_vest(g, mat='vest', color='#7a5a30'):
    """A sleeveless leather/scale vest from the waist to the neck."""
    vm = g.m(mat, color)
    gg = 0.03
    rings = [(z, rx + gg, ry + gg, dx, dy) for z, rx, ry, dx, dy in g.chest[:5]]
    out = [('chest', M.loft(rings, vm, seg=16, cap_bottom=False, cap_top=True), None)]
    out.append(('spine', M.loft([(-0.04, 0.17 * g.tw + gg, 0.125 * g.tw + gg), (0.22, 0.175 * g.tw + gg, 0.128 * g.tw + gg)],
                                vm, seg=14, cap_bottom=False, cap_top=False), None))
    # rows of scales: thin darker rings
    dm = g.m(mat + '_dark', '#3e2c16')
    for z in (0.04, 0.12, 0.20):
        rx = float(np.interp(z, [r[0] for r in rings], [r[1] for r in rings])) + 0.003
        ry = float(np.interp(z, [r[0] for r in rings], [r[2] for r in rings])) + 0.003
        out.append(('chest', M.loft([(z, rx, ry), (z + 0.012, rx, ry)], dm, seg=16, cap_bottom=False, cap_top=False), None))
    return out


def g_cap_knob(g):
    """A gold band and a knob on top of a cap (Babylonian bowman)."""
    gm = g.m('gold', '#e0b840', finish='gloss')
    parts = [M.cylinder(0.12, 0.12, 0.03, gm, seg=14, z0=0.125).transformed(M.scale(1, 1.08, 1)),
             M.ellipsoid(0.03, 0.03, 0.035, gm, (0, -0.01, 0.255), seg=8)]
    return [('head', _h(M.Mesh.concat(parts)), None)]


def g_fur_cape(g, mat='pelt', color='#c8ac80'):
    """A shaggy sheepskin cape over both shoulders, open at the front, down to
    the thighs (Enkidu)."""
    pm = g.m(mat, color)
    c = g.chest
    rings = [(c[4][0] + 0.01, c[4][1] + 0.06, c[4][2] + 0.06), (c[3][0], c[3][1] + 0.07, c[3][2] + 0.07),
             (c[1][0], c[2][1] + 0.08, c[2][2] + 0.09), (c[0][0] - 0.30, c[2][1] + 0.10, c[2][2] + 0.10, 0, -0.04)]
    out = [('chest', H._shell(rings, pm, math.pi * 0.62, math.pi * 2.38, seg=18), None)]
    rng = np.random.default_rng(5)
    for i in range(12):
        a = rng.uniform(math.pi * 0.65, math.pi * 2.35)
        z = rng.uniform(-0.25, 0.25)
        rx = c[2][1] + 0.085
        ry = c[2][2] + 0.09
        out.append(('chest', M.ellipsoid(0.045, 0.035, 0.05, pm, (rx * math.cos(a), ry * math.sin(a), z), seg=6), None))
    return out


GEAR = {name[2:]: fn for name, fn in globals().items() if name.startswith('g_') and callable(fn)}


# ------------------------------------------------------------ extra animations

def anim_throw(base, ctx):
    """A javelin throw: wind back with the javelin over the shoulder, step in
    and hurl it overhand; the arm follows through, then takes a new one."""
    B = base
    rel = ctx.event('reach_max', 0.55) if ctx is not None else 0.55
    rel = min(max(rel or 0.55, 0.4), 0.75)
    aim = dir_euler((0.0, 1.0, 0.12))
    back = {'hand_r': (0.40, -0.32, 1.62), 'w_main': aim, 'chest': (4, 0, 40), 'spine': (2, 0, 18),
            'head': (0, 0, -30), 'hand_l': (-0.20, 0.45, 1.30), 'elbow_r': (0.9, -0.2, -0.2),
            'foot_l': (-0.18, 0.26, H.ANKLE_H), 'foot_r': (0.18, -0.20, H.ANKLE_H), 'fyaw_r': -35.0,
            'root_pos': (0, -0.08, -0.06)}
    release = dict(H._lead(0.9), **{'hand_r': (0.20, 0.55, 1.68), 'w_main': dir_euler((0.0, 1.0, 0.2)),
                                    'chest': (-14, 0, -20), 'spine': (-8, 0, -8), 'head': (6, 0, 10),
                                    'hand_l': (-0.42, -0.10, 1.10), 'root_pos': (0, 0.16, -0.12),
                                    'elbow_r': (0.8, -0.5, 0.2)})
    follow = dict(H._lead(0.9), **{'hand_r': (-0.05, 0.45, 0.95), 'w_main': dir_euler((0.0, 1.0, -0.6)),
                                   'chest': (-18, 0, -30), 'spine': (-10, 0, -10), 'hand_l': (-0.42, -0.15, 1.05),
                                   'root_pos': (0, 0.18, -0.16)})
    k = [(0.0, {}), (0.30, back), (0.42, back), (0.55, release), (0.70, follow),
         (0.86, {'hand_r': (0.0, 0.25, 1.05), 'hand_l': B['hand_l'], 'chest': (0, 0, 0), 'spine': (0, 0, 0),
                 'root_pos': (0, 0.05, -0.04), 'foot_l': B['foot_l'], 'foot_r': B['foot_r'], 'toe_r': 0.0,
                 'fyaw_r': B['fyaw_r'], 'w_main': B['w_main'], 'head': (0, 0, 0)}),
         (1.0, H._back_to(B, H.ATTACK_REST))]
    anim = H.keys(B, k)
    warp = [(0.55, rel)]

    def fn(t):
        tt = H._warp(t, warp)
        p = anim(tt)
        p['thrown'] = np.float32(tt)
        return p
    return H.Action(fn)


def anim_arms_crossed(base, loop=True):
    """Standing with the arms folded (samurai)."""
    B = dict(base)
    B.update({'hand_r': np.array((-0.13, 0.22, 1.24), np.float32), 'hand_l': np.array((0.14, 0.21, 1.20), np.float32),
              'elbow_r': np.array((1.0, -0.2, -0.5), np.float32), 'elbow_l': np.array((-1.0, -0.2, -0.5), np.float32)})
    return H.anim_idle(B, loop=loop, amount=0.6)


# ------------------------------------------------------------ the archetype

@archetype('footman')
class Footman(H.Humanoid):
    def __init__(self, unit, params):
        super().__init__(unit, params)
        g = Gear(self)
        self.gear = g
        for item in params.get('gear', []):
            name, kw = (item, {}) if isinstance(item, str) else item
            self.parts += GEAR[name](g, **kw)
        # finishes of materials the catalog names
        for name, fin in params.get('finish', {}).items():
            if name in self.mats and name not in self.body.tint:
                col = params.get('colors', {}).get(name, '#888888')
                _add(self.mats, name, col, False, fin)
        self.prop_over = dict(params.get('props', {}))
        self.twohand = params.get('twohand')
        self.twohand_actions = params.get('twohand_actions')
        self.sheathed = set(params.get('sheathed', ()))
        self.offhand_mesh = params.get('offhand_mesh')
        if self.offhand_mesh:
            self.prop_over['__bow'] = self.offhand_mesh
        self.prop_over.setdefault('__arrow', ('arrow', {}))

    def prop(self, name, **kw):
        m = self._prop(name, **kw)
        ws = self.p.get('weapon_scale')
        if name is not None and name != self.weapon and name in H.WORK_TOOLS.values():
            ws = self.p.get('tool_scale', ws)
        if ws and name is not None and (name in (self.weapon, self.offhand, '__bow') or name in H.WORK_TOOLS.values()):
            key = ('scaled', name, tuple(sorted(kw.items())))
            if key not in self._props:
                k = ws if isinstance(ws, (tuple, list)) else (ws, ws, ws)
                self._props[key] = m.transformed(M.scale(*k))
            return self._props[key]
        return m

    def _prop(self, name, **kw):
        if name in self.prop_over:
            pname, pkw = self.prop_over[name] if not isinstance(self.prop_over[name], str) else (self.prop_over[name], {})
            key = ('over', name, tuple(sorted(kw.items())))
            if key not in self._props:
                fn = getattr(PF, pname, None) or getattr(P, pname)
                self._props[key] = fn(self.mats, **dict(pkw, **kw))
            return self._props[key]
        return super().prop(name, **kw)

    def loops(self, action):
        return super().loops(action) or self.anim_name(action) == 'idle_crossed'

    def action(self, action, ctx):
        name = self.anim_name(action)
        ev = self.p.get('events', {}).get(action)
        if ev and ctx is not None:
            # catalog-fixed event times where the measured ones mislead
            base_ctx = ctx

            class _Ev:
                n = base_ctx.n

                def event(self, k, default=None):
                    return ev[k] if k in ev else base_ctx.event(k, default)
            ctx = _Ev()
        if name == 'attack_throw':
            return anim_throw(self.base, ctx)
        if name == 'idle_crossed':
            return anim_arms_crossed(self.base)
        if name == 'fortify_idle':
            return H.anim_idle(self.base, loop=False)
        if name == 'death' and ctx is not None:
            # The humanoid's fall can't come before 35% of the flic; some
            # originals drop at once, so remap time to fall when they do.
            d = ctx.event('down', 0.55)
            if d is not None and d < 0.35:
                class _Ctx:
                    def event(self, n, default=None):
                        return 0.35 if n == 'down' else ctx.event(n, default)
                act = H.anim_death(self.base, self.weapon, self.offhand, _Ctx())
                d = max(d, 0.08)
                fn0 = act.fn
                act.fn = lambda t, fn0=fn0, d=d: fn0(H._warp(t, [(d, 0.35)]))
                return act
        return super().action(action, ctx)

    def frame(self, action, f, n, ctx):
        mesh = self._frame(action, f, n, ctx)
        finish_colors(self, self.p)
        return mesh

    def _frame(self, action, f, n, ctx):
        act = self.action(action, ctx)
        t = rig.frame_times(n, act.loop)[f]
        pose = act.fn(t)
        name = self.anim_name(action)
        weapon = self.weapon
        if name in H.WORK_TOOLS:
            weapon = H.WORK_TOOLS[name]
        if action in self.sheathed:
            weapon = None
        extra = []
        if action in self.p.get('lock_feet', ()) and act.legs == 'ik':
            # long robes: keep the feet planted (no steps through the hem)
            for c in ('foot_l', 'foot_r', 'toe_l', 'toe_r', 'fyaw_l', 'fyaw_r'):
                pose[c] = self.base[c]
            rp = np.asarray(pose.get('root_pos', (0, 0, 0)), np.float32).copy()
            rp[:2] *= 0.35
            pose['root_pos'] = rp
        ov = self.p.get('overrides', {}).get(action)
        if ov:
            # catalog pose overrides: held throughout a loop, eased in over a one-shot
            w = 1.0 if act.loop else rig.smoothstep(min(1.0, t / 0.6))
            for c, v in ov.items():
                v = np.asarray(v, np.float32)
                pose[c] = v if c not in pose else np.asarray(pose[c], np.float32) * (1 - w) + v * w
        if (self.twohand and act.arms == 'ik' and weapon == self.weapon and weapon is not None
                and (self.twohand_actions is None and action != 'DEATH' or
                     self.twohand_actions is not None and action in self.twohand_actions)):
            R = M.euler(*np.asarray(pose['w_main'], np.float32))[:3, :3]
            pose['hand_l'] = np.asarray(pose['hand_r'], np.float32) - R @ np.array([0, 0, self.twohand], np.float32)
            pose['elbow_l'] = np.array((-0.4, -0.6, -0.6), np.float32)
        if 'thrown' in pose and weapon is not None:
            tt = float(pose['thrown'])
            t_rel, t_new = 0.55, 0.86
            if t_rel <= tt < t_new:
                # the javelin flies on from the release point
                u = (tt - t_rel) / (t_new - t_rel)
                d = 2.4 * u
                p0 = np.array((0.20, 0.55, 1.68), np.float32)
                pos = p0 + np.array((0.0, d, 0.25 * d - 0.6 * d * d * 0.35), np.float32)
                ang = dir_euler((0.0, 1.0, 0.2 - 0.5 * u))
                if u < 0.95:
                    extra.append(self.prop(weapon).transformed(M.translate(*pos) @ M.euler(*ang)))
                weapon = None
        if name == 'death' and weapon is not None and self.p.get('drop_weapon', True):
            # The weapon leaves the hand as the unit falls and lands beside it
            # (it would otherwise stay upright in the hand of a falling body).
            down = ctx.event('down', 0.55) if ctx is not None else 0.55
            down = min(max(down or 0.55, 0.35), 0.85)
            t0, t1 = 0.45 * down, 0.45 * down + 0.22
            if t >= t0:
                u = min(1.0, (t - t0) / (t1 - t0))
                u2 = u * u
                start = np.array((0.40, 0.10, 0.95), np.float32)
                end = np.array((0.45, -0.05, 0.035), np.float32)
                pos = start * (1 - u2) + end * u2 + np.array((0.12 * u * (1 - u), 0, 0.15 * u * (1 - u)), np.float32)
                w0 = np.asarray(self.base['w_main'], np.float32)
                w1 = np.array(dir_euler((1.0, -0.25, 0.0)), np.float32)
                ang = w0 * (1 - u) + w1 * u
                extra.append(self.prop(weapon).transformed(M.translate(*pos) @ M.euler(*ang)))
                weapon = None
        if action in self.p.get('shield_front', ()) and self.offhand in ('shield_round', 'shield_oval'):
            # the shield held square in front of the body (not turned with the forearm)
            Wl = self._left_world(pose, act)
            Rc = Wl['chest'][:3, :3]
            T = np.eye(4, dtype=np.float32)
            T[:3, :3] = Rc
            hp = Wl['hand_l'][:3, 3] + Rc @ np.array((-0.02, 0.10, 0.02), np.float32)
            sh = self.prop(self.offhand).transformed(M.translate(*hp) @ T @ M.rot_z(180 + 28))
            off = self.offhand
            self.offhand = None
            try:
                mesh = self.pose_mesh(pose, act, weapon)
            finally:
                self.offhand = off
            return M.Mesh.concat([mesh, sh] + extra)
        if name == 'attack_shoot' and self.offhand == 'bow':
            # an arrow nocked from the aim until the release, then flying off
            rel = ctx.event('reach_max', 0.62) if ctx is not None else 0.62
            rel = min(max(rel or 0.62, 0.45), 0.8)
            kt = H._warp(t, [(0.65, rel)])
            Wl = self._left_world(pose, act)['hand_l']
            bowp = Wl[:3, 3] + Wl[:3, :3] @ np.array([0, 0.01, -H.GRIP], np.float32)
            hr = np.asarray(pose['hand_r'], np.float32)
            arrow = self.prop('__arrow')
            if 0.16 <= kt < 0.66:
                d = bowp - hr
                d = d / (np.linalg.norm(d) + 1e-9)
                extra.append(arrow.transformed(M.translate(*(hr - d * 0.02)) @ M.euler(*dir_euler(d))))
                self._arrow_dir = d
            elif 0.66 <= kt < 0.82:
                d = getattr(self, '_arrow_dir', np.array((0, 1, 0.1), np.float32))
                u = (kt - 0.66) / 0.16
                extra.append(arrow.transformed(M.translate(*(bowp + d * (0.2 + 2.2 * u))) @ M.euler(*dir_euler(d))))
        if (name != 'attack_shoot' and self.offhand == 'bow' and float(pose.get('draw', 0.0)) > 0.5
                and self.p.get('nock_on_draw', True)):
            # a drawn bow (a fortified archer taking aim) holds a nocked arrow
            Wl = self._left_world(pose, act)['hand_l']
            bowp = Wl[:3, 3] + Wl[:3, :3] @ np.array([0, 0.01, -H.GRIP], np.float32)
            hr = np.asarray(pose['hand_r'], np.float32)
            d = bowp - hr
            d = d / (np.linalg.norm(d) + 1e-9)
            extra.append(self.prop('__arrow').transformed(M.translate(*(hr - d * 0.02)) @ M.euler(*dir_euler(d))))
        if self.offhand_mesh and self.offhand == 'bow':
            # draw the custom bow ourselves (the humanoid draws the standard one)
            draw = float(np.clip(pose.get('draw', 0.0), 0, 1))
            off = self.offhand
            self.offhand = None
            try:
                mesh = self.pose_mesh(pose, act, weapon)
            finally:
                self.offhand = off
            bow = self.prop('__bow', draw=round(draw * 8) / 8)
            W = self._left_world(pose, act)['hand_l']
            grip = W[:3, 3] + W[:3, :3] @ np.array([0, 0.01, -H.GRIP], np.float32)
            R = M.euler(*np.asarray(pose.get('w_off', (0, 0, 0)), np.float32))
            return M.Mesh.concat([mesh, bow.transformed(M.translate(*grip) @ R)] + extra)
        mesh = self.pose_mesh(pose, act, weapon)
        return M.Mesh.concat([mesh] + extra) if extra else mesh

    def _left_world(self, pose, act):
        """World transform of the left hand for a pose (re-derived like
        pose_mesh does, for props drawn outside it)."""
        # Pose a copy with nothing in the hands and find the left hand by IK.
        h = self.h
        sk = self.skel
        base_pose = {'root_pos': np.asarray(pose.get('root_pos', (0, 0, 0)), np.float32),
                     'root_rot': pose.get('root_rot', (0, 0, 0))}
        for b in ('spine', 'chest', 'neck', 'head'):
            base_pose[b] = pose.get(b, (0, 0, 0))
        base_pose['pelvis'] = np.zeros(3, np.float32)
        limb = {b: pose.get(b, (0, 0, 0)) for b in ('arm_l', 'forearm_l', 'hand_l')}
        W0 = sk.world(dict(base_pose, **limb))
        over = {}
        if act.arms == 'ik':
            sh = W0['arm_l'][:3, 3]
            grip = np.asarray(pose['hand_l'], np.float32) * np.array([1, 1, h], np.float32)
            pole = H._normalize(np.asarray(pose.get('elbow_l', (0, -1, -0.3)), np.float32))
            d = H._normalize(grip - sh)
            Ru, Rl, _, _ = H.two_bone(sh, grip - d * H.GRIP, H.UPPER_ARM * h, H.FOREARM * h, pole, bend_forward=True)
            over.update({'arm_l': Ru, 'forearm_l': Rl, 'hand_l': Rl})
        W = H._world_with(sk, dict(base_pose, **limb), over)
        return W
