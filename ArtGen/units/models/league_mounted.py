"""The League modes' riders (catalog/league_mounted.py): Modern's
Roughrider and QC's Teutonic Knight.

They are the 'horse_rider' archetype (models/mounted.py) with a few parts
of their own, kept in this module so the shared files stay as they are:

  * head gear, registered in props_mounted.HEADGEAR at import: 'slouch_hat'
    (a creased crown and a wide soft brim, a band round it: material
    'hatband') and 'crested_helm' (the great helm with a fan-shaped crest
    on top: material 'crest');
  * a held 'shield_kite' (a heater shield: material 'shield_face', a plain
    cross on it in material 'shield_cross'), the name the mounted stances
    already know;
  * the 'league_horse_rider' archetype: the horse_rider plus

        'cross'        color of a cross on the rider's surcoat, front and
                       back (material 'cross'; None: none)
        'flank_cross'  color of a cross on each flank of the horse's
                       caparison, over the hindquarters (None: none)
        'neckerchief'  material name of a knotted kerchief at the rider's
                       neck (None: none)
        'pennant'      bool: the lance carries a pennant (material 'pennant',
                       civ-colored)
"""
import math

import mesh as M
from models import archetype
from models import humanoid as H
from models import mounted as MT
from models import props as P
from models import props_mounted as PM

mat = P.mat


# ------------------------------------------------------------ head gear (head space, like props_mounted's)

def slouch_hat(mats, m='hat', band='hatband'):
    """A campaign hat: a tall round crown pinched in front, a wide brim
    curling up a little at the sides."""
    hm = mat(mats, m, '#8a7454', spec=0.04)
    bm = mat(mats, band, '#3a2c20', spec=0.04)
    parts = [M.loft([(0.11, 0.132, 0.142), (0.20, 0.122, 0.132), (0.27, 0.10, 0.11), (0.30, 0.06, 0.07)], hm, seg=18,
                    cap_bottom=False, cap_top=True).transformed(M.translate(0, -0.006, 0))]
    # the crease down the middle of the crown
    parts.append(M.ellipsoid(0.025, 0.10, 0.03, mat(mats, 'hat_crease', '#5e4e38'), (0, -0.01, 0.30), seg=8))
    parts.append(M.cylinder(0.136, 0.136, 0.035, bm, seg=18, z0=0.11).transformed(M.scale(1, 1.06, 1)))
    # the brim: a ring, its sides turned up
    ang = [2 * math.pi * i / 28 for i in range(28)]
    rows = []
    for r_x, r_y, up in ((0.135, 0.145, 0.0), (0.29, 0.30, 0.05)):
        rows.append([(r_x * math.cos(a), r_y * math.sin(a) - 0.005, 0.11 + up * abs(math.cos(a)) ** 2) for a in ang])
    import numpy as np
    v = np.array(rows, np.float32).reshape(-1, 3)
    faces = M.grid_faces(1, len(ang), closed_u=True)
    top = M.from_indexed(v, faces, hm)
    v2 = v.copy()
    v2[:, 2] -= 0.016
    parts += [top, M.from_indexed(v2, faces, hm)]
    return M.Mesh.concat(parts)


def crested_helm(mats, m='helmet', crest='crest'):
    """The great helm with a fan-shaped crest standing on its top."""
    cm = mat(mats, crest, '#ffffff', spec=0.05)
    fan = [(0.0, 0.0)] + [(0.24 * math.cos(a), 0.22 * math.sin(a)) for a in
                          [math.radians(20 + 140 * i / 10) for i in range(11)]]
    parts = [PM.great_helm(mats, m)]
    parts.append(M.extrude(fan, 0.03, cm).transformed(M.translate(0, -0.01, 0.24) @ M.rot_z(90) @ M.rot_x(90)
                                                       @ M.translate(0, 0, -0.015)))
    return M.Mesh.concat(parts)


PM.HEADGEAR.setdefault('slouch_hat', slouch_hat)
PM.HEADGEAR.setdefault('crested_helm', crested_helm)


def shield_kite(mats, w=0.50, h=0.66, face='shield_face', cross='shield_cross'):
    """A heater shield facing -y, its grip at the origin: a flat top, the
    sides curving to a point, a cross from edge to edge."""
    fm = mat(mats, face, '#e6e2d8', spec=0.05)
    xm = mat(mats, cross, '#1c1c1e', spec=0.05)
    rm = mat(mats, 'shield_rim', '#6a6a70', spec=0.6, gloss=22, metal=True)
    out = [(-w / 2, h * 0.42), (w / 2, h * 0.42)]
    for i in range(1, 10):
        u = i / 10
        out.append((w / 2 * math.cos(u * math.pi / 2) ** 0.8, h * 0.42 - (h * 0.42 + h * 0.58) * u))
    out.append((0.0, -h * 0.58))
    for i in range(9, 0, -1):
        u = i / 10
        out.append((-w / 2 * math.cos(u * math.pi / 2) ** 0.8, h * 0.42 - (h * 0.42 + h * 0.58) * u))
    # the outline lies in x/z; extrude along y (thickness), the front at -y
    to_xz = M.rot_x(90)
    parts = [M.extrude(out, 0.03, fm).transformed(to_xz @ M.translate(0, 0, -0.015))]
    parts.append(M.extrude([(x * 1.04, z * 1.03) for x, z in out], 0.02, rm).transformed(
        M.translate(0, 0.012, 0) @ to_xz @ M.translate(0, 0, -0.01)))
    parts.append(M.box(0.09, 0.012, h * 0.86, xm, (0, -0.022, -0.08)))
    parts.append(M.box(w * 0.86, 0.012, 0.09, xm, (0, -0.023, 0.08)))
    return M.Mesh.concat(parts)


if not hasattr(PM, 'shield_kite'):
    PM.shield_kite = shield_kite


# ------------------------------------------------------------ the archetype

@archetype('league_horse_rider')
class LeagueHorseRider(MT.HorseRider):
    def __init__(self, unit, params):
        super().__init__(unit, params)
        p = params
        rp = p.get('rider', {})
        hm = self.rider.h.mats
        parts = self.rider.h.parts
        tw = H.TORSO_W * H.build_params(rp).get('bulk', 1.0)
        if p.get('cross'):
            cm = mat(hm, 'cross', p['cross'])
            for sy in (1, -1):
                y = sy * (0.122 * (1 + (tw - 1) * 0.6) + 0.035)
                parts.append(('chest', M.rounded_box(0.075, 0.02, 0.30, cm, (0, y, 0.11), r=0.008, seg=6), None))
                parts.append(('chest', M.rounded_box(0.26, 0.02, 0.07, cm, (0, y * 0.99, 0.17), r=0.008, seg=6), None))
        if p.get('neckerchief'):
            nm = mat(hm, p['neckerchief'], '#ffffff')
            parts.append(('chest', M.loft([(0.27, 0.125, 0.095), (0.31, 0.105, 0.085), (0.335, 0.08, 0.075)], nm,
                                          seg=16, cap_bottom=False, cap_top=False), None))
            # the knotted point hanging in front
            parts.append(('chest', M.extrude([(-0.07, 0.0), (0.07, 0.0), (0.0, -0.13)], 0.02, nm).transformed(
                M.translate(0, 0.118, 0.30) @ M.rot_x(90) @ M.rot_x(-8)), None))
        if p.get('flank_cross'):
            xm = mat(self.mats, 'flank_cross', p['flank_cross'], spec=0.04)
            for sx in (1, -1):
                for yc in (-0.55, 0.40):
                    rx, rz, zc = MT.barrel_at(yc)
                    x = sx * (rx + 0.06)
                    self.horse.back_parts.append(M.box(0.016, 0.085, 0.38, xm, (x, yc, zc - 0.12)))
                    self.horse.back_parts.append(M.box(0.016, 0.34, 0.085, xm, (x * 1.005, yc, zc - 0.06)))
        if p.get('pennant'):
            orig = self.rider.h.prop

            def prop(name, **kw):
                if name == 'lance':
                    kw = dict(kw, pennant=True)
                return orig(name, **kw)
            self.rider.h.prop = prop
