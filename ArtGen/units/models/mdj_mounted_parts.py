"""Medieval Japan's mounted samurai (catalog/mdj_mounted.py): the horse
archer Shoni no Kagesuke and the Nakamaro Horseman.

They are the 'horse_rider' archetype (models/mounted.py) with a few
Japanese parts on top, kept in this module so the shared files stay as they
are:

  * head gear, registered in props_mounted.HEADGEAR at import: 'kabuto_kuwagata'
    (a lacquered helmet bowl, a flaring neck guard and the tall gold V of
    kuwagata horns) and 'kabuto_maedate' (the same bowl with a small gold
    crest on its brow);
  * a held 'katana' (props_mounted is where riders look up their weapons);
  * the 'mdj_horse_rider' archetype: the horse_rider plus sode (big shoulder
    plates), thick civ-colored cords with tassels around the horse's chest
    and hindquarters (the munagai and shirigai), and an optional long
    asymmetric yumi in place of the short bow.

Catalog parameters on top of horse_rider's:
    'sode'      material name of the shoulder plates (None: none)
    'kusazuri'  material name of the armored skirt (None: none)
    'cords'     material name of the horse's chest and crupper cords (None: none)
    'yumi'      {'height': m, 'grip': fraction from the bottom} or None
"""
import contextlib
import math

import numpy as np

import mesh as M
from models import archetype
from models import humanoid as H
from models import mounted as MT
from models import props as P
from models import props_mounted as PM
from models.props_mounted import tube_y

mat = P.mat


def _gold(mats):
    return mat(mats, 'gold', '#c4a45e', spec=0.8, gloss=24, metal=True)


# ------------------------------------------------------------ head gear (head bone space, pre HEAD scale)

def _kabuto_base(mats, m):
    """The helmet bowl and its shikoro (a neck guard of lames flaring out and
    down over the back and sides of the neck), with small turnbacks."""
    hm = mat(mats, m, '#34322e', spec=0.5, gloss=22)
    parts = [PM._dome(0.124, 0.134, 0.135, hm, (0, -0.005, 0.12), 0.02)]
    # the shikoro: three lames, each a little wider and lower, open at the face
    for i, (z, r) in enumerate(((0.135, 0.132), (0.095, 0.150), (0.055, 0.168))):
        rings = [(z, r, r * 1.04, 0, -0.015 * i), (z - 0.045, r + 0.02, (r + 0.02) * 1.04, 0, -0.015 * i - 0.01)]
        parts.append(_arc_band(rings, hm, 160, 380))
    # fukigaeshi: the turnbacks beside the face
    for s in (1, -1):
        parts.append(M.rounded_box(0.03, 0.07, 0.09, hm, (s * 0.15, 0.07, 0.125), r=0.01, seg=6)
                     .transformed(M.rot_z(s * 20)))
    return parts


def _arc_band(rings, m, a0, a1, seg=14):
    """An open band between two rings (z, rx, ry, dx, dy) over angles a0..a1
    (degrees, 90 = the face, +y), double-sided by being a thin shell."""
    ang = np.radians(np.linspace(a0, a1, seg + 1))
    pts = []
    for z, rx, ry, dx, dy in rings:
        pts.append([(dx + rx * math.cos(a), dy + ry * math.sin(a), z) for a in ang])
    V = np.asarray(pts, np.float32)
    nu = V.shape[1]
    faces = []
    for i in range(nu - 1):
        a, b, c, d = i, i + 1, nu + i, nu + i + 1
        faces += [(a, c, d), (a, d, b), (a, d, c), (a, b, d)]
    return M.from_indexed(V.reshape(-1, 3), np.asarray(faces), m)


def kabuto_kuwagata(mats, m='helmet', gold='gold'):
    """A samurai helmet with the tall gold kuwagata: two flat horns rising
    in a V from a plate on the brow (the Nakamaro Horseman)."""
    g = _gold(mats) if gold == 'gold' else mat(mats, gold, '#b09a62', spec=0.8, gloss=24, metal=True)
    parts = _kabuto_base(mats, m)
    parts.append(M.rounded_box(0.09, 0.025, 0.05, g, (0, 0.135, 0.17), r=0.008, seg=6))
    for s in (1, -1):
        horn = M.extrude([(0.0, 0.0), (0.045, 0.0), (0.12, 0.30), (0.095, 0.33)], 0.022, g)
        parts.append(horn.transformed(M.translate(s * 0.02, 0.14, 0.17) @ M.rot_x(-6) @ M.scale(s, 1, 1)))
    return M.Mesh.concat(parts)


def kabuto_maedate(mats, m='helmet', gold='gold'):
    """A samurai helmet with a small gold crest standing on its brow (Shoni
    no Kagesuke)."""
    g = _gold(mats) if gold == 'gold' else mat(mats, gold, '#b09a62', spec=0.8, gloss=24, metal=True)
    parts = _kabuto_base(mats, m)
    parts.append(M.rounded_box(0.05, 0.02, 0.04, g, (0, 0.13, 0.2), r=0.008, seg=6))
    parts.append(M.extrude([(-0.05, 0.0), (0.05, 0.0), (0.035, 0.06), (0.0, 0.13), (-0.035, 0.06)], 0.014, g)
                 .transformed(M.translate(0, 0.135, 0.22)))
    return M.Mesh.concat(parts)


PM.HEADGEAR['kabuto_kuwagata'] = kabuto_kuwagata
PM.HEADGEAR['kabuto_maedate'] = kabuto_maedate


# ------------------------------------------------------------ held weapons

def katana(mats, length=0.9):
    """A katana held at the hilt: a long two-hand grip, a round tsuba and a
    long, gently curved blade (its edge toward +y)."""
    from models.props_foot_ancient import strip_blade
    s = P.steel(mats)
    parts = [M.cylinder(0.02, 0.02, 0.30, mat(mats, 'hilt', '#1e1a1e'), seg=8, z0=-0.22)]
    parts.append(M.cylinder(0.055, 0.055, 0.014, mat(mats, 'tsuba', '#3a3430', spec=0.4, metal=True), seg=10, z0=0.07))
    us = np.linspace(0, 1, 9)
    pts = [(-0.07 * u * u, 0.085 + length * u) for u in us]
    widths = [0.038 * (1 - u ** 8) + 0.002 for u in us]
    parts.append(strip_blade(pts, widths, 0.012, s))
    return M.Mesh.concat(parts)


if not hasattr(PM, 'katana'):
    PM.katana = katana


def yumi(mats, height=2.0, grip=0.36, draw=0.0, bend=0.12, string=True):
    """The Japanese longbow: taller than the archer and gripped below its
    middle (the upper limb about twice the lower), lacquered dark with
    pale bindings. Same conventions as props.bow."""
    w = mat(mats, 'yumi', '#4a3224', spec=0.3, gloss=18)
    bind = mat(mats, 'yumi_bind', '#d8ccb0', spec=0.05)
    n = 11
    bend = bend + 0.08 * draw
    zb, zt = -height * grip, height * (1 - grip)
    pts = []
    for i in range(n):
        u = i / (n - 1)
        z = (zb + (zt - zb) * u) * (1 - 0.05 * draw)
        v = (z / (zt if z > 0 else -zb))          # -1 .. 1 along each limb
        y = -bend * v * v + 0.03 * math.sin(math.pi * u) * (1 if z > 0 else -1)
        pts.append((0, y, z))
    parts = []
    for a, b in zip(pts[:-1], pts[1:]):
        r = 0.02 - 0.009 * abs(a[2]) / max(zt, -zb)
        parts.append(M.capsule(a, b, r, r * 0.92, w, seg=7, rings=1))
    parts.append(M.cylinder(0.025, 0.025, 0.14, bind, seg=8, z0=-0.06))
    for zf in (0.45, 0.8):
        parts.append(M.cylinder(0.02, 0.02, 0.03, bind, seg=8, z0=zt * zf))
    if string:
        sm = mat(mats, 'string', '#e8e0c8', spec=0.0)
        top, bot = pts[-1], pts[0]
        mid = (0, top[1] - 0.05 - 0.55 * draw, 0)
        parts.append(M.capsule(bot, mid, 0.005, 0.005, sm, seg=4, rings=1))
        parts.append(M.capsule(mid, top, 0.005, 0.005, sm, seg=4, rings=1))
    return M.Mesh.concat(parts)


def ebira(mats):
    """A samurai's quiver for the back (chest bone space): a box of arrows
    worn upright at the right hip, its fletchings fanning out above the
    right shoulder."""
    lm = mat(mats, 'ebira', '#3a2a20', spec=0.15)
    f = mat(mats, 'fletch', '#e8e2d0', spec=0.0)
    parts = [M.rounded_box(0.16, 0.06, 0.16, lm, (0, 0, 0.0), r=0.015, seg=6),
             M.rounded_box(0.05, 0.03, 0.40, lm, (0.0, -0.01, 0.20), r=0.01, seg=6)]
    for i in range(7):
        a = -30 + 60 * i / 6
        stick = M.cylinder(0.006, 0.006, 0.55, P.wood(mats), seg=4, z0=0.0)
        fl = M.box(0.01, 0.035, 0.11, f, (0, 0, 0.52))
        parts.append(M.Mesh.concat([stick, fl]).transformed(M.translate(0, 0, 0.05) @ M.rot_y(a * 0.35)))
    m = M.Mesh.concat(parts)
    return m.transformed(M.translate(0.10, -0.17, -0.22) @ M.rot_y(-22))


# ------------------------------------------------------------ worn gear and tack

def sode(mats, m, side):
    """A big, slightly curved shoulder plate hanging off the shoulder (chest
    bone space), laced in horizontal lames."""
    sm = mat(mats, m, '#ffffff')
    s = 1 if side == 'r' else -1
    w, h = 0.26, 0.30
    rows = []
    for zf in np.linspace(0, 1, 5):
        z = 0.31 - h * zf
        row = []
        for yf in np.linspace(-1, 1, 6):
            y = yf * w / 2
            x = s * (0.385 + 0.07 * zf + 0.03 * (1 - yf * yf))
            row.append((x, y - 0.01, z))
        rows.append(row)
    V = np.asarray(rows, np.float32)
    nr, nc = V.shape[:2]
    faces = []
    for j in range(nr - 1):
        for i in range(nc - 1):
            a, b, c, d = j * nc + i, j * nc + i + 1, (j + 1) * nc + i, (j + 1) * nc + i + 1
            faces += [(a, c, d), (a, d, b), (a, d, c), (a, b, d)]
    plate = M.from_indexed(V.reshape(-1, 3), np.asarray(faces), sm)
    # thickness: a second shell a little outside
    outer = plate.transformed(M.translate(s * 0.018, 0, 0))
    return M.Mesh.concat([plate, outer])


def kusazuri(mats, m):
    """The armored skirt (pelvis bone space): four flaring panels of lames
    hanging from the waist over the hips and thighs."""
    km = mat(mats, m, '#ffffff')
    out = []
    for c in (90, 0, 270, 180):
        out.append(_arc_band([(0.06, 0.25, 0.20, 0, 0), (-0.27, 0.36, 0.31, 0, 0)], km, c - 40, c + 40, seg=6))
    return M.Mesh.concat(out)


def _chain(pts, r, m):
    return M.Mesh.concat([M.capsule(a, b, r, r, m, seg=7, rings=1) for a, b in zip(pts[:-1], pts[1:])])


def horse_cords(mats, m, r=0.035):
    """The thick cords of a samurai horse (body space): the munagai around
    the chest and the shirigai around the hindquarters, with big tassels."""
    cm = mat(mats, m, '#ffffff', tint=True)
    out = []
    front = [(0.22, 0.08, 1.46), (0.255, 0.38, 1.30), (0.19, 0.62, 1.18), (0.0, 0.76, 1.14)]
    front = front + [(-x, y, z) for x, y, z in reversed(front[:-1])]
    out.append(_chain(front, r, cm))
    rear = [(0.22, -0.30, 1.50), (0.29, -0.50, 1.36), (0.25, -0.76, 1.24), (0.0, -0.93, 1.20)]
    rear = rear + [(-x, y, z) for x, y, z in reversed(rear[:-1])]
    out.append(_chain(rear, r, cm))
    # tassels hanging from the cords
    for sx in (1, -1):
        for (x, y, z) in ((0.26, 0.42, 1.27), (0.28, -0.60, 1.30), (0.22, -0.82, 1.21)):
            out.append(M.capsule((sx * x, y, z), (sx * x * 1.05, y, z - 0.16), 0.03, 0.05, cm, seg=7, rings=2))
    return M.Mesh.concat(out)


@contextlib.contextmanager
def _swapped(mod, name, fn):
    old = getattr(mod, name)
    setattr(mod, name, fn)
    try:
        yield
    finally:
        setattr(mod, name, old)


@archetype('mdj_horse_rider')
class MdjHorseRider(MT.HorseRider):
    """horse_rider with sode, horse cords and the yumi (see the module doc)."""

    def __init__(self, unit, params):
        super().__init__(unit, params)
        p = params
        rp = p.get('rider', {})
        hm = self.rider.h.mats
        if p.get('sode'):
            for side in 'rl':
                self.rider.h.parts.append(('chest', sode(hm, p['sode'], side), None))
        if p.get('kusazuri'):
            bulk = H.build_params(rp).get('bulk', 1.0)
            self.rider.h.parts.append(('pelvis', kusazuri(hm, p['kusazuri']).transformed(M.scale(bulk, bulk, 1)), None))
        if rp.get('back_gear') == 'ebira':
            self.rider.h.parts.append(('chest', ebira(hm), None))
        if p.get('cords'):
            self.horse.back_parts.append(horse_cords(self.mats, p['cords']))
        self.yumi_kw = p.get('yumi')

    def pose_mesh(self, pose, act, action):
        if not self.yumi_kw:
            return super().pose_mesh(pose, act, action)
        kw = dict(self.yumi_kw)

        def bow(mats, draw=0.0, **_):
            return yumi(mats, draw=draw, **kw)
        with _swapped(P, 'bow', bow):
            return super().pose_mesh(pose, act, action)
