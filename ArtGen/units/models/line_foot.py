"""Round 2 of four foot units (catalog/modern_mod_foot.py and
catalog/league_foot.py): the Militia (a French line fusilier of 1806-12),
the Skirmisher (a US rifleman of the 1960s with the M14), the Japanese
rifleman (IJA, 1941-44, Type 38 and Type 98 field cap) and the Battalion (a
US machine-gun team of 1944 with the water-cooled Browning M1917A1).

Archetypes:
    'line_soldier'  models/league_foot.py's 'league_soldier' plus
        coat_cut   'habit': the habit-veste's lapels, collar, cuffs and flaps,
                   epaulettes, buttons and the tails with their turnbacks
                   (materials 'lapels', 'facings', 'turnbacks', 'epaulette')
        jacket     'jungle' (slanted chest pockets, open collar, rolled
                   sleeves, the skirt worn out) | 'm41' (the M1941 field
                   jacket: collar, button line, pocket flaps, shoulder tabs)
                   | 'type98' (stand-and-fall collar with red tabs, four
                   pockets, brass buttons)
        leg_wear   'gaiters_long' (black, to the knee, buttoned) |
                   'leggings' (M1938 canvas, laced) | 'puttees' (wound) |
                   'jungle_boots' (a canvas upper over the black boot)
        kit        list of line_gear worn pieces: 'calfskin', 'giberne',
                   'briquet', 'm1956', 'towel', 'ija_belt', 'ija_pack',
                   'ija_canteen', 'holster', 'cartridge_belt', 'canteen_ww2'
        hat        also 'shako1806' | 'm1_cover' (Vietnam: leaf-pattern cover,
                   band, a bottle under it) | 'm1_net' (WWII: net, chinstrap
                   over the brim) | 'senboshi' (IJA field cap with its star
                   and four neck flaps: material 'havelock')
        gun        also a line_gear.GUNS name
        order_grip m: the carry pose holds the gun that far up the barrel
                   (pose channel 'order' 0..1 slides it: order arms)
        poses      as foot_modern, plus 'march' (the carry while running)
    'line_mg_team'  the gunner with the M1917A1 on his shoulder and his
                    assistant with the folded tripod and a can; in action
                    the gun on its tripod, the gunner behind it, the
                    assistant feeding the belt from an open can, the hose to
                    the condenser can.
"""
import math

import numpy as np

import mesh as M
import render as R
import rig
from models import archetype
from models import humanoid as H
from models import foot_modern as FM
from models import league_foot as LF
from models import line_gear as LG
from models.foot_ancient import finish_colors

A = H.ANKLE_H
_v = FM._v


def _chest_rings(p):
    """The humanoid's chest rings for these parameters (as humanoid.Body)."""
    bulk = H.build_params(p).get('bulk', 1.0)
    tw = H.TORSO_W * bulk
    rings = [(0.0, 0.165, 0.105, 0, 0.0), (0.10, 0.19, 0.12, 0, 0.012), (0.19, 0.205, 0.122, 0, 0.008),
             (0.25, 0.185, 0.108, 0, -0.005), (0.30, 0.10, 0.07, 0, -0.01), (0.33, 0.05, 0.05, 0, -0.01)]
    rings = [(z, rx * tw, ry * (1 + (tw - 1) * 0.6), dx, dy) for z, rx, ry, dx, dy in rings]
    mus = H.muscle_amount(p)
    if mus > 0:
        rings = [(z, rx * (1 + 0.09 * mus * (0.10 <= z <= 0.26)), ry * (1 + 0.10 * mus * (0.05 <= z <= 0.26)),
                  dx, dy + 0.012 * mus * (0.08 <= z <= 0.22)) for z, rx, ry, dx, dy in rings]
    return rings


def _ring_at(rings, z):
    zs = [r[0] for r in rings]
    z = min(max(z, zs[0]), zs[-1])
    for a, b in zip(rings[:-1], rings[1:]):
        if a[0] <= z <= b[0]:
            u = (z - a[0]) / max(b[0] - a[0], 1e-6)
            return tuple(a[i] + (b[i] - a[i]) * u for i in range(5))
    return rings[-1]


def _surface(rings, z, ang, g=0.0):
    """A point on the ringed surface at height z and angle ang (+y front = pi/2), g outside it."""
    _, rx, ry, dx, dy = _ring_at(rings, z)
    return (dx + (rx + g) * math.cos(ang), dy + (ry + g) * math.sin(ang), z)


def _panel(rings, zs, halfw, mat, g, center=math.pi / 2, n=6):
    """A strip of surface over the rings between heights zs, `halfw(z)` radians
    either side of `center`."""
    pts = []
    for z in zs:
        w = halfw(z)
        for a in np.linspace(center - w, center + w, n):
            pts.append(_surface(rings, z, a, g))
    v = np.array(pts, np.float32)
    faces = M.grid_faces(len(zs) - 1, n, closed_u=False)
    return M.from_indexed(v, faces, mat, smooth=True)


def _sector(rings, a0, a1, mat, g=0.0, seg=10):
    rr = [(r[0], r[1] + g, r[2] + g) + tuple(r[3:5]) for r in rings]
    return H._shell(rr, mat, a0, a1, seg=seg)


def _flap(mat, c, w, h, t=0.022, tilt=0.0, yaw=0.0):
    """A pocket or flap: a small rounded slab facing +y at c."""
    m = M.rounded_box(w, t, h, mat, (0, 0, 0), r=min(0.008, t * 0.45), seg=6, p=4)
    return m.transformed(M.translate(*c) @ M.rot_z(yaw) @ M.rot_y(tilt))


# ------------------------------------------------------------ the body

class LineBody(LF.LeagueBody):
    def parts(self):
        for name in self.tint:
            self.mm(name, '#ffffff')
        out = super().parts()
        p = self.full
        bulk = H.build_params(p).get('bulk', 1.0)
        tw = H.TORSO_W * bulk
        lb = H.LIMB * bulk
        ch = _chest_rings(p)
        pel = [(-0.40, 0.27, 0.215), (-0.17, 0.182 * tw, 0.136 * 1.24), (0.0, 0.186 * tw, 0.133 * 1.24),
               (0.105, 0.173 * tw, 0.125 * 1.24)]

        def add(bone, mesh, tag=None):
            out.append((bone, mesh, tag))

        top = self.mm('top', '#6a6a5a')
        brass = LG.brass(self.mats)

        # -- coats and jackets
        if p.get('coat_cut') == 'habit':
            lap = self.mm('lapels', '#ece8de')
            fac = self.mm('facings', '#b02a24')
            turn = self.mm('turnbacks', '#b02a24')
            w = {0.0: 0.22, 0.10: 0.32, 0.19: 0.38, 0.25: 0.38, 0.29: 0.30}

            def hw(z):
                ks = sorted(w)
                for a, b in zip(ks[:-1], ks[1:]):
                    if a <= z <= b:
                        return w[a] + (w[b] - w[a]) * (z - a) / (b - a)
                return w[ks[-1]]
            zs = [0.0, 0.05, 0.10, 0.15, 0.19, 0.23, 0.27, 0.29]
            add('chest', _panel(ch, zs, hw, lap, 0.022))
            # the lapels' red piping and buttons
            for side in (1, -1):
                edge = [_surface(ch, z, math.pi / 2 + side * hw(z), 0.026) for z in zs]
                for a, b in zip(edge[:-1], edge[1:]):
                    add('chest', M.capsule(a, b, 0.008, 0.008, fac, seg=5, rings=1))
                for z in (0.05, 0.12, 0.19, 0.25):
                    add('chest', M.sphere(0.011, brass, _surface(ch, z, math.pi / 2 + side * (hw(z) - 0.09), 0.03), seg=6))
            add('chest', M.loft([(0.27, 0.105, 0.092, 0, 0.0), (0.355, 0.088, 0.08, 0, -0.004)], fac, seg=14,
                                cap_bottom=False, cap_top=False))                                    # the collar
            for side, sx in (('r', 1), ('l', -1)):
                # epaulettes: a pad and its fringe
                ep = self.mm('epaulette', '#b02a24')
                add(f'arm_{side}', M.ellipsoid(0.085, 0.075, 0.03, ep, (sx * 0.01, 0.0, 0.085), seg=10))
                if p.get('fringe', True):
                    add(f'arm_{side}', M.loft([(0.02, 0.112, 0.10), (0.075, 0.09, 0.08)], ep, seg=12, cap_bottom=False,
                                              cap_top=False).transformed(M.translate(sx * 0.012, 0, 0)))
                else:
                    add(f'arm_{side}', M.cylinder(0.088, 0.088, 0.012, fac, seg=12, z0=0.085).transformed(
                        M.translate(sx * 0.01, 0, 0) @ M.scale(1, 0.85, 1)))
                add(f'chest', M.rounded_box(0.13, 0.05, 0.016, top, (sx * 0.17, -0.01, 0.315), r=0.006, seg=4, p=4))
                # cuffs and their white flaps
                add(f'forearm_{side}', M.cylinder(0.062 * lb, 0.064 * lb, 0.065, fac, seg=12, z0=-0.215))
                add(f'forearm_{side}', M.rounded_box(0.014, 0.05, 0.06, lap, (sx * 0.064 * lb, 0.0, -0.18), r=0.005, seg=4))
            # the tails, open in front, their red turnbacks at the edges
            tails = [(0.10, 0.25, 0.18), (-0.06, 0.262, 0.198), (-0.22, 0.27, 0.212), (-0.40, 0.27, 0.215)]
            add('pelvis', _sector(tails, math.pi + 0.50, 2 * math.pi - 0.50, top, 0.0, seg=12))
            b = 1.5 * math.pi
            for a0, a1 in ((math.pi + 0.50, math.pi + 0.78), (2 * math.pi - 0.78, 2 * math.pi - 0.50),
                           (b - 0.16, b - 0.03), (b + 0.03, b + 0.16)):
                add('pelvis', _sector(tails[1:], a0, a1, turn, 0.008, seg=4))
            for sx in (1, -1):
                add('pelvis', M.sphere(0.012, brass, (sx * 0.09, -0.205, 0.06), seg=6))            # tail buttons
            # the waistcoat's points below the coat, its buttons, and the breeches' fall
            wc = self.mm('waistcoat', '#f0ece2')
            add('pelvis', _sector([(0.11, 0.186 * tw + 0.012, 0.133 * 1.24 + 0.012), (0.03, 0.188 * tw + 0.014, 0.136 * 1.24 + 0.014)],
                                  math.pi / 2 - 0.36, math.pi / 2 + 0.36, wc, 0.0, seg=8))
            for sx in (1, -1):
                add('pelvis', M.capsule((sx * 0.20, 0.13, 0.03), (0.0, 0.178, -0.0), 0.007, 0.007, self.mm('seam', '#b8b2a4'), seg=4, rings=1))
            for z in (0.09, 0.05):
                add('pelvis', M.sphere(0.01, self.mm('bone_button', '#d8d2c4'), (0.0, 0.133 * 1.24 + 0.02, z), seg=5))
            fl = self.mm('seam', '#b8b2a4')
            for sx in (1, -1):
                add('pelvis', M.capsule((sx * 0.10, 0.17, -0.02), (sx * 0.10, 0.16, -0.13), 0.006, 0.006, fl, seg=4, rings=1))
            add('pelvis', M.capsule((-0.10, 0.168, -0.02), (0.10, 0.168, -0.02), 0.006, 0.006, fl, seg=4, rings=1))
            for sx in (1, -1):
                for z in (-0.04, -0.09):
                    add('pelvis', M.sphere(0.009, self.mm('bone_button', '#d8d2c4'), (sx * 0.085, 0.172, z), seg=5))
            for side, sx in (('r', 1), ('l', -1)):
                add(f'thigh_{side}', M.capsule((sx * 0.13, 0.0, 0.0), (sx * 0.10, 0.0, -0.36), 0.007, 0.007, fl, seg=4, rings=1))
                add(f'thigh_{side}', M.cylinder(0.098 * lb, 0.094 * lb, 0.03, self.mm('seam', '#b8b2a4'), seg=12, z0=-0.37))
        jk = p.get('jacket')
        if jk in ('jungle', 'm41', 'type98'):
            dk = self.mm('jacket_dark', '#2a2a22') if jk != 'type98' else brass
            if p.get('sleeve_mat'):
                top = self.mm(p['sleeve_mat'], '#5a6040')
            # the skirt worn over the trousers
            low = {'jungle': -0.15, 'm41': -0.12, 'type98': -0.14}[jk]
            add('pelvis', M.loft([(low, 0.198 * tw, 0.150 * 1.24), (-0.02, 0.193 * tw, 0.143 * 1.24),
                                  (0.10, 0.182 * tw, 0.134 * 1.24)], top, seg=16, cap_bottom=False, cap_top=False))
            bt = self.mm('belt', '#4a3a2a')
            add('pelvis', M.loft([(0.02, 0.200 * tw, 0.150 * 1.24), (0.075, 0.195 * tw, 0.146 * 1.24)], bt, seg=18,
                                 cap_bottom=False, cap_top=False))
            add('pelvis', M.rounded_box(0.06, 0.02, 0.055, LG.brass(self.mats) if jk == 'type98' else self.mm('buckle', '#6a6a5a', spec=0.25, gloss=16, metal=True),
                                        (0, 0.152 * 1.24 + 0.012, 0.048), r=0.006, seg=4, p=4))
            for sx in (1, -1):
                if jk == 'jungle':
                    # slanted chest pockets with flaps, bellows pockets below
                    x = sx * 0.09 * tw
                    c = _surface(ch, 0.17, math.atan2(1, sx * 0.55), 0.024)
                    add('chest', _flap(top, c, 0.10, 0.10, 0.024, tilt=-sx * 14))
                    add('chest', _flap(top, (c[0], c[1] + 0.012, c[2] + 0.05), 0.11, 0.035, 0.022, tilt=-sx * 14))
                    add('chest', M.sphere(0.009, dk, (c[0], c[1] + 0.026, c[2] + 0.045), seg=5))
                    add('pelvis', _flap(top, (sx * 0.12 * tw, 0.19, -0.07), 0.11, 0.11, 0.026))
                    add('pelvis', _flap(top, (sx * 0.12 * tw, 0.205, -0.02), 0.12, 0.035, 0.022))
                    add('chest', M.capsule((c[0] - 0.05, c[1] + 0.006, c[2] + 0.03), (c[0] + 0.05, c[1] + 0.006, c[2] + 0.03 + sx * 0.0),
                                           0.006, 0.006, dk, seg=4, rings=1))
                    # the name and US ARMY tapes over the pockets
                    tp = self.mm('tape', '#3e3e2a')
                    add('chest', _flap(tp, (c[0], c[1] + 0.008, c[2] + 0.088), 0.115, 0.03, 0.016, tilt=-sx * 14))
                    if sx > 0:
                        # the angle-head flashlight clipped to the right suspender
                        fl = self.mm('flashlight', '#4a5236', spec=0.1, gloss=12)
                        f0 = _surface(ch, 0.20, math.pi / 2 - 0.62, 0.05)
                        add('chest', M.rounded_box(0.045, 0.04, 0.10, fl, f0, r=0.01, seg=6, p=4))
                        add('chest', M.cylinder(0.026, 0.026, 0.05, fl, seg=10, z0=0.0).transformed(
                            M.translate(f0[0], f0[1] + 0.01, f0[2] + 0.05) @ M.rot_x(-90)))
                        add('chest', M.cylinder(0.022, 0.022, 0.006, self.mm('lens', '#d8d8c0', spec=0.4, gloss=20), seg=10,
                                                z0=0.0).transformed(M.translate(f0[0], f0[1] + 0.06, f0[2] + 0.05) @ M.rot_x(-90)))
                    else:
                        f0 = _surface(ch, 0.21, math.pi / 2 + 0.62, 0.045)
                        add('chest', M.rounded_box(0.06, 0.04, 0.07, self.mm('web_dark', '#4a4a34'), f0, r=0.01, seg=6, p=4))
                    if sx < 0:
                        add('arm_l', M.rounded_box(0.012, 0.06, 0.065, self.mm('patch', '#3a3a2a'), (-0.085 * lb / 1.35 * 1.35 - 0.006, 0.0, -0.06), r=0.006, seg=4))
                    # the open collar's points
                    add('chest', M.extrude([(-0.04, 0.0), (0.04, 0.0), (0.0, -0.09)], 0.02, top).transformed(
                        M.translate(sx * 0.07, 0.095, 0.33) @ M.rot_z(sx * 22) @ M.rot_x(-30)))
                elif jk == 'm41':
                    add('pelvis', _flap(self.mm('pocket_flap', '#4e4c36'), (sx * 0.16 * tw, 0.178, 0.02), 0.11, 0.04, 0.024,
                                        yaw=sx * 30))
                    add('chest', M.rounded_box(0.12, 0.05, 0.016, top, (sx * 0.17, -0.01, 0.315), r=0.006, seg=4, p=4))
                    add('chest', M.extrude([(-0.045, 0.0), (0.045, 0.0), (0.0, -0.08)], 0.02, top).transformed(
                        M.translate(sx * 0.07, 0.09, 0.335) @ M.rot_z(sx * 25) @ M.rot_x(-35)))
                else:
                    # four pockets with flaps; the stand-and-fall collar with its red tabs
                    c = _surface(ch, 0.16, math.atan2(1, sx * 0.45), 0.024)
                    pf = self.mm('pocket_flap', '#6a663e')
                    add('chest', _flap(top, c, 0.10, 0.09, 0.024))
                    add('chest', _flap(pf, (c[0], c[1] + 0.012, c[2] + 0.05), 0.115, 0.04, 0.026))
                    add('chest', M.sphere(0.014, brass, (c[0], c[1] + 0.03, c[2] + 0.04), seg=6))
                    add('pelvis', _flap(top, (sx * 0.13 * tw, 0.19, -0.04), 0.11, 0.10, 0.026))
                    add('pelvis', _flap(self.mm('pocket_flap', '#6a663e'), (sx * 0.13 * tw, 0.207, 0.015), 0.12, 0.04, 0.026))
                    add('chest', M.rounded_box(0.016, 0.05, 0.045, self.mm('collar_tab', '#a8302a'),
                                               (sx * 0.103, 0.035, 0.33), r=0.005, seg=4).transformed(M.rot_z(sx * 20)))
            if jk == 'type98':
                add('chest', M.loft([(0.28, 0.105, 0.092, 0, 0.0), (0.33, 0.092, 0.084, 0, -0.004)], top, seg=14,
                                    cap_bottom=False, cap_top=False))
            if jk == 'jungle':
                # the placket down the front, in the fatigues' olive
                add('chest', _panel(ch, [0.0, 0.08, 0.16, 0.24, 0.30], lambda z: 0.07, top, 0.02, n=3))
            # the button line down the front
            for z in (0.02, 0.09, 0.16, 0.23):
                add('chest', M.sphere(0.014 if jk == 'type98' else 0.011, dk, _surface(ch, z, math.pi / 2, 0.022), seg=6))
            # side seams and pockets on the trousers / breeches
            sm_ = self.mm('trouser_seam', '#3e3a2a')
            for side, sx in (('r', 1), ('l', -1)):
                r0 = 0.10 * lb if p.get('legs') == 'breeches' else 0.094 * lb
                add(f'thigh_{side}', M.capsule((sx * r0, 0.0, 0.0), (sx * r0 * 0.82, 0.0, -0.36), 0.0065, 0.0065, sm_, seg=4, rings=1))
                add(f'thigh_{side}', M.capsule((sx * 0.05, 0.085 * lb, -0.02), (sx * 0.085, 0.07 * lb, -0.09), 0.0065, 0.0065, sm_,
                                               seg=4, rings=1))
                if p.get('legs') == 'trousers':
                    add(f'shin_{side}', M.capsule((sx * 0.074 * lb, 0.0, 0.0), (sx * 0.064 * lb, 0.0, -0.30), 0.0065, 0.0065, sm_,
                                                  seg=4, rings=1))
            if p.get('rolled'):
                for side in 'rl':
                    add(f'arm_{side}', M.capsule((0, 0, -0.15), (0, 0, -0.185), 0.076 * lb, 0.076 * lb, top, seg=12, rings=1))
        # -- leg wear
        lw = p.get('leg_wear')
        for side, sx in (('r', 1), ('l', -1)):
            if lw == 'gaiters_long':
                gm = self.mm('gaiters', '#1c1a18')
                add(f'shin_{side}', M.loft([(-0.40, 0.068 * lb, 0.074 * lb), (-0.24, 0.074 * lb, 0.08 * lb),
                                            (-0.04, 0.078 * lb, 0.082 * lb), (0.0, 0.082 * lb, 0.086 * lb)], gm, seg=12,
                                           cap_bottom=False, cap_top=True))
                for z in (-0.34, -0.27, -0.20, -0.13, -0.06):
                    add(f'shin_{side}', M.sphere(0.009, self.mm('gaiter_button', '#3a3634'), (sx * 0.078 * lb, 0.012, z), seg=5))
                add(f'foot_{side}', M.rounded_box(0.10 * H.FOOT, 0.1, 0.03, gm, (0, 0.03, -0.01), r=0.01, seg=6, p=4))
            elif lw == 'leggings':
                lg = self.mm('leggings', '#a8987a')
                add(f'shin_{side}', M.loft([(-0.41, 0.07 * lb, 0.076 * lb), (-0.28, 0.074 * lb, 0.078 * lb),
                                            (-0.16, 0.078 * lb, 0.08 * lb)], lg, seg=12, cap_bottom=False, cap_top=True))
                hk = self.mm('lace', '#3a3428')
                for z in (-0.38, -0.33, -0.28, -0.23, -0.18):
                    add(f'shin_{side}', M.sphere(0.008, hk, (sx * 0.074 * lb, 0.02, z), seg=4))
                add(f'shin_{side}', M.capsule((sx * 0.074 * lb, 0.02, -0.38), (sx * 0.074 * lb, 0.02, -0.18), 0.004, 0.004, hk,
                                              seg=4, rings=1))
                add(f'foot_{side}', M.rounded_box(0.035, 0.04, 0.09, hk, (0, 0.0, -0.06), r=0.008, seg=4))  # instep strap
            elif lw == 'puttees':
                pm = self.mm('puttees', '#7a6e4a')
                add(f'shin_{side}', M.loft([(-0.40, 0.066 * lb, 0.07 * lb), (-0.22, 0.072 * lb, 0.076 * lb),
                                            (-0.06, 0.07 * lb, 0.072 * lb)], pm, seg=12, cap_bottom=False, cap_top=True))
                for k, z in enumerate(np.linspace(-0.37, -0.09, 7)):
                    r = (0.070 + 0.003 * math.sin(k)) * lb
                    add(f'shin_{side}', M.cylinder(r + 0.007, r + 0.007, 0.024, pm, seg=12, z0=-0.012).transformed(
                        M.translate(0, 0, z) @ M.rot_x(14) @ M.rot_y(sx * 6)))
                add(f'shin_{side}', M.cylinder(0.074 * lb, 0.074 * lb, 0.012, self.mm('puttee_tie', '#4a3a26'), seg=12, z0=-0.075))
            elif lw == 'jungle_boots':
                cv = self.mm('canvas', '#4e5636')
                add(f'shin_{side}', M.loft([(-0.36, 0.075 * lb, 0.08 * lb), (-0.24, 0.077 * lb, 0.08 * lb)], cv, seg=12,
                                           cap_bottom=False, cap_top=True))
                for z in (-0.34, -0.30, -0.26):
                    add(f'shin_{side}', M.box(0.05, 0.012, 0.01, self.mm('lace', '#1e1e1a'), (0, 0.08 * lb, z)))
                tm = self.mm('trousers', '#5a5e40')
                add(f'shin_{side}', M.ellipsoid(0.08 * lb, 0.082 * lb, 0.05, tm, (0, 0, -0.22), seg=10))       # the bloused cuff
        # -- kit
        for k in p.get('kit', []):
            if k == 'calfskin':
                add('chest', LG.calfskin_knapsack(self.mats), 'backpack')
            elif k == 'giberne':
                add('pelvis', LG.giberne(self.mats))
            elif k == 'briquet':
                add('pelvis', LG.briquet(self.mats))
            elif k == 'm1956':
                add('pelvis', LG.m1956_belt_kit(self.mats, tw))
            elif k == 'bandoliers':
                bm = self.mm('bandolier', '#7a7c56')
                for sx in (1, -1):
                    pts = [(sx * ch[1][1] * 0.85, ch[1][2] * 0.6 + 0.05, -0.04), (sx * ch[1][1] * 0.3, ch[1][2] + 0.05, 0.08),
                           (-sx * ch[2][1] * 0.25, ch[2][2] + 0.05, 0.2), (-sx * ch[2][1] * 0.62, ch[2][2] * 0.62 + 0.05, 0.31),
                           (-sx * ch[2][1] * 0.72, 0.0, 0.345)]
                    for a_, b_ in zip(pts[:-1], pts[1:]):
                        add('chest', M.capsule(a_, b_, 0.034, 0.034, bm, seg=6, rings=1))
                    for a_ in pts[:3]:
                        add('chest', M.rounded_box(0.06, 0.04, 0.05, bm, a_, r=0.01, seg=4, p=4))
            elif k == 'rucksack':
                rk = self.mm('rucksack', '#5e6444')
                rd = self.mm('web_dark', '#4a4a34')
                add('chest', M.rounded_box(0.30, 0.14, 0.28, rk, (0, -0.215, 0.13), r=0.05, seg=10, p=3.5), 'backpack')
                add('chest', M.rounded_box(0.26, 0.06, 0.10, rk, (0, -0.29, 0.06), r=0.02, seg=6, p=4), 'backpack')
                for sx in (1, -1):
                    add('chest', M.rounded_box(0.07, 0.07, 0.14, rk, (sx * 0.18, -0.21, 0.08), r=0.02, seg=6, p=4), 'backpack')
                    add('chest', M.capsule((sx * 0.1, -0.15, 0.27), (sx * 0.1, 0.07, 0.30), 0.016, 0.016, rd, seg=5, rings=1),
                        'backpack')
                add('chest', M.box(0.31, 0.02, 0.02, rd, (0, -0.29, 0.20)), 'backpack')
            elif k == 'combat_pack':
                # the M1956 combat field pack worn high between the suspenders
                rk = self.mm('rucksack', '#5e6444')
                rd = self.mm('web_dark', '#4a4a34')
                add('chest', M.rounded_box(0.22, 0.12, 0.17, rk, (0, -0.205, 0.17), r=0.035, seg=8, p=3.5), 'backpack')
                add('chest', M.rounded_box(0.225, 0.05, 0.06, rd, (0, -0.255, 0.235), r=0.015, seg=6, p=4), 'backpack')
                for sx in (1, -1):
                    add('chest', M.box(0.02, 0.13, 0.17, rd, (sx * 0.07, -0.21, 0.17)), 'backpack')
            elif k == 'etool':
                et = self.mm('etool', '#4e5434')
                add('pelvis', M.rounded_box(0.12, 0.05, 0.20, et, (0.0, -0.29, -0.10), r=0.015, seg=6, p=4))
                add('pelvis', M.cylinder(0.016, 0.016, 0.10, self.mm('etool_wood', '#7a5a34'), seg=6, z0=0.0).transformed(
                    M.translate(0.0, -0.29, -0.0)))
                cv = self.mm('canteen_cover', '#6a6a4c')
                add('pelvis', M.rounded_box(0.13, 0.08, 0.17, cv, (-0.23 * tw, -0.08, -0.06), r=0.03, seg=8, p=3))
            elif k == 'towel':
                add('chest', LG.neck_towel(self.mats))
            elif k == 'ija_belt':
                add('pelvis', LG.ija_belt_kit(self.mats, tw))
            elif k == 'ija_pack':
                add('chest', LG.ija_knapsack(self.mats), 'backpack')
            elif k == 'ija_canteen':
                add('pelvis', LG.ija_canteen(self.mats))
                st = self.mm('strap', '#5a4a30')
                # the canteen's strap over the left shoulder, the bread bag's over the right
                for sx in (1, -1):
                    pts = [_surface(ch, 0.0, math.pi / 2 + sx * 0.75, 0.035), _surface(ch, 0.14, math.pi / 2 + sx * 0.25, 0.035),
                           _surface(ch, 0.26, math.pi / 2 - sx * 0.45, 0.03), (-sx * 0.15, 0.0, 0.335),
                           _surface(ch, 0.24, -math.pi / 2 + sx * 0.45, 0.03), _surface(ch, 0.06, -math.pi / 2 - sx * 0.6, 0.035)]
                    for a, b in zip(pts[:-1], pts[1:]):
                        add('chest', M.capsule(a, b, 0.014, 0.014, st, seg=5, rings=1))
            elif k == 'holster':
                hl = self.mm('holster', '#6a4226')
                T = M.translate(0.29, 0.03, -0.02) @ M.rot_x(-8) @ M.rot_y(6)
                add('pelvis', M.Mesh.concat([M.rounded_box(0.05, 0.08, 0.17, hl, (0, 0, -0.06), r=0.015, seg=6, p=4),
                                             M.rounded_box(0.056, 0.09, 0.06, hl, (0, 0.0, 0.03), r=0.012, seg=6, p=4),
                                             M.sphere(0.009, LG.brass(self.mats), (0.03, 0.0, 0.02), seg=5)]).transformed(T))
            elif k == 'cartridge_belt':
                pm = self.mm('pouch', '#7a7458')
                for a in np.linspace(math.pi / 2 - 1.15, math.pi / 2 + 1.15, 6):
                    c = (0.225 * tw * math.cos(a), 0.175 * 1.24 * math.sin(a), 0.05)
                    add('pelvis', M.rounded_box(0.07, 0.045, 0.075, pm, (0, 0, 0), r=0.01, seg=6, p=4).transformed(
                        M.translate(*c) @ M.rot_z(math.degrees(a) - 90)))
            elif k == 'canteen_ww2':
                cv = self.mm('canteen_cover', '#7a7458')
                add('pelvis', M.rounded_box(0.13, 0.08, 0.16, cv, (0.20 * tw, -0.17, -0.06), r=0.03, seg=8, p=3))
                add('pelvis', M.rounded_box(0.08, 0.04, 0.07, self.mm('first_aid', '#6a6a50'), (-0.10 * tw, 0.235, 0.04),
                                            r=0.012, seg=6, p=4))
        return self._cover_skin(out)

    def _cover_skin(self, out):
        """The body's skin-coloured joint spheres and limb volumes show through
        sleeves and trousers (as on art2-regulars): the shoulder ball takes the
        sleeve's colour and shrinks to the sleeve's size; under long sleeves the
        whole arm does; under trousers or breeches the thigh's, under trousers
        the shin's."""
        p = self.full
        skin = self.mats['skin']
        top = p.get('top')
        sleeve = self.mats['top'] if top in ('sleeved', 'long_sleeved') and 'top' in self.mats else None
        legs = self.mats['trousers'] if p.get('legs') in ('trousers', 'breeches') and 'trousers' in self.mats else None
        res = []
        for bone, mesh, tag in out:
            if len(mesh) and np.all(mesh.M == skin):
                z = mesh.V[..., 2]
                if bone.startswith('arm_') and sleeve is not None:
                    if top == 'long_sleeved' or z.min() > -0.12:
                        mesh = mesh.with_material(sleeve)
                    if z.min() > -0.12 and z.max() < 0.12:
                        c = mesh.V.reshape(-1, 3).mean(0)
                        mesh = mesh.transformed(M.translate(*c) @ M.scale(0.86) @ M.translate(*(-c)))
                elif bone.startswith('forearm_') and top == 'long_sleeved' and sleeve is not None:
                    mesh = mesh.with_material(sleeve)
                elif bone.startswith('thigh_') and legs is not None:
                    mesh = mesh.with_material(legs)
                elif bone.startswith('shin_') and legs is not None and p.get('legs') == 'trousers':
                    mesh = mesh.with_material(legs)
            res.append((bone, mesh, tag))
        if p.get('sleeve_mat') and 'top' in self.mats:
            tm, sm = self.mats['top'], self.mm(p['sleeve_mat'], '#5a6040')
            res = [(b, m.with_material(sm) if b.startswith('arm_') and len(m) and np.all(m.M == tm) else m, t)
                   for b, m, t in res]
        return res

    def _hat(self, add0):
        def add(mesh, tag='hat'):
            add0('head', mesh.transformed(M.scale(H.HEAD)), tag)
        p = self.full
        hat = p.get('hat')
        if hat == 'shako1806':
            hm = self.mm('shako', '#1c1c20', spec=0.18, gloss=14)
            lm = self.mm('shako_leather', '#121214', spec=0.18, gloss=16)
            br = LG.brass(self.mats)
            add(M.loft([(0.165, 0.114, 0.124, 0, -0.008), (0.27, 0.122, 0.132, 0, -0.014), (0.38, 0.132, 0.142, 0, -0.02)],
                       hm, seg=20, cap_bottom=False, cap_top=True))
            add(M.cylinder(0.135, 0.135, 0.025, lm, seg=20, z0=0.365).transformed(M.translate(0, -0.02, 0) @ M.scale(1, 1.07, 1)))
            add(M.cylinder(0.118, 0.12, 0.03, lm, seg=20, z0=0.165).transformed(M.translate(0, -0.008, 0) @ M.scale(1, 1.08, 1)))
            for sx in (1, -1):
                # the leather side chevrons
                for a, b in (((sx * 0.124, 0.06, 0.17), (sx * 0.13, -0.01, 0.30)), ((sx * 0.13, -0.01, 0.30), (sx * 0.124, -0.09, 0.17))):
                    add(M.capsule(a, b, 0.009, 0.009, lm, seg=5, rings=1))
                # the chinscales from the rosette to the chin
                add(M.cylinder(0.022, 0.022, 0.012, br, seg=10, z0=-0.006).transformed(M.translate(sx * 0.12, 0.02, 0.16) @ M.rot_y(90)))
                for k in range(5):
                    u = (k + 0.5) / 5
                    c = (sx * (0.118 - 0.03 * u), 0.02 + 0.07 * u, 0.15 - 0.13 * u)
                    add(M.box(0.012, 0.026, 0.026, br, c))
                # the cords' hanging tassels on the right
            add(M.ellipsoid(0.095, 0.055, 0.011, lm, (0, 0.125, 0.168), seg=12).transformed(
                M.translate(0, 0.125, 0.168) @ M.rot_x(20) @ M.translate(0, -0.125, -0.168)))     # the peak
            add(M.extrude([(0.0, -0.055), (0.045, 0.0), (0.0, 0.06), (-0.045, 0.0)], 0.012, br)
                .transformed(M.translate(0, 0.137, 0.25) @ M.rot_x(90) @ M.rot_x(-5)))           # the plate
            add(M.extrude([(-0.02, -0.02), (0.02, -0.02), (0.025, 0.015), (0.0, 0.03), (-0.025, 0.015)], 0.016, br)
                .transformed(M.translate(0, 0.142, 0.25) @ M.rot_x(90)))
            ck = self.mm('cockade', '#b02a24')
            add(M.cylinder(0.034, 0.034, 0.01, ck, seg=12, z0=0.0).transformed(M.translate(0, 0.145, 0.33) @ M.rot_x(-90)))
            add(M.cylinder(0.02, 0.02, 0.01, self.mm('lapels', '#ece8de'), seg=10, z0=0.0).transformed(
                M.translate(0, 0.152, 0.33) @ M.rot_x(-90)))
            pm = self.mm('plume', '#b02a24')
            add(M.ellipsoid(0.042, 0.042, 0.055, pm, (0, 0.12, 0.41), seg=10))
            cord = self.mm('cord', '#e8e4dc')
            fest = [(-0.13, 0.03, 0.31), (-0.10, 0.11, 0.24), (0.0, 0.15, 0.21), (0.10, 0.11, 0.24), (0.13, 0.03, 0.31)]
            for a, b in zip(fest[:-1], fest[1:]):
                add(M.capsule(a, b, 0.008, 0.008, cord, seg=4, rings=1))
            add(M.capsule((0.13, 0.03, 0.31), (0.16, 0.0, 0.12), 0.008, 0.008, cord, seg=4, rings=1))
            add(M.ellipsoid(0.02, 0.02, 0.035, cord, (0.162, -0.002, 0.10), seg=6))
        elif hat in ('m1_cover', 'm1_net'):
            hm = self.mm('helmet', '#5e6a3e', spec=0.06 if hat == 'm1_cover' else 0.12, gloss=12)
            rx, ry, rz, cut = 0.137, 0.150, 0.157, -0.03
            lift = 0.022 if hat == 'm1_cover' else 0.0
            add(H._dome(rx, ry, rz, hm, (0, -0.005, 0.12 + lift), cut))
            zr = 0.12 + cut + lift
            add(M.loft([(zr - 0.006, rx + 0.022, ry + 0.022), (zr + 0.012, rx + 0.004, ry + 0.004)], hm, seg=20,
                       cap_bottom=True, cap_top=False).transformed(M.translate(0, -0.005, 0)))
            r2 = np.random.default_rng(11)
            if hat == 'm1_cover':
                cols = [self.mm('leaf_brown', '#7a5a30', spec=0.04), self.mm('leaf_dark', '#2c3620', spec=0.04)]
                for i in range(40):
                    a = r2.uniform(0, 2 * math.pi)
                    e = r2.uniform(0.05, 1.25)
                    c = (1.002 * rx * math.cos(e) * math.cos(a), 1.002 * ry * math.cos(e) * math.sin(a) - 0.005,
                         0.12 + lift + rz * math.sin(e))
                    s = r2.uniform(0.028, 0.042)
                    add(M.ellipsoid(0.006, s, s * r2.uniform(0.45, 0.8), cols[i % 2], (0, 0, 0), seg=7).transformed(
                        M.translate(*c) @ M.rot_z(math.degrees(a)) @ M.rot_y(-math.degrees(e)) @ M.rot_x(r2.uniform(0, 180))))
                bd = self.mm('band', '#1c1c18')
                add(M.cylinder(rx * 0.99, rx * 0.97, 0.036, bd, seg=20, z0=zr + 0.026).transformed(
                    M.translate(0, -0.005, 0) @ M.scale(1, ry / rx, 1)))
                add(M.cylinder(0.017, 0.017, 0.075, self.mm('bottle', '#bcb88e', spec=0.1, gloss=12), seg=8, z0=0.0)
                    .transformed(M.translate(-0.115, 0.05, zr + 0.01) @ M.rot_y(-12)))
                add(M.cylinder(0.012, 0.012, 0.016, self.mm('band', '#2a2a22'), seg=6, z0=0.0)
                    .transformed(M.translate(-0.115 - 0.016, 0.05, zr + 0.085) @ M.rot_y(-12)))
            else:
                nm = self.mm('helmet_net', '#3e3e2c')
                for k in range(10):
                    a = 2 * math.pi * k / 10
                    pts = [(rx * 1.01 * math.cos(e) * math.cos(a), ry * 1.01 * math.cos(e) * math.sin(a) - 0.005,
                            0.12 + rz * 1.01 * math.sin(e)) for e in np.linspace(-0.15, 1.35, 5)]
                    for q0, q1 in zip(pts[:-1], pts[1:]):
                        add(M.capsule(q0, q1, 0.0045, 0.0045, nm, seg=4, rings=1))
                for e in (0.35, 0.8):
                    add(M.cylinder(rx * math.cos(e) * 1.012, rx * math.cos(e) * 1.012, 0.008, nm, seg=20,
                                   z0=0.12 + rz * math.sin(e)).transformed(M.translate(0, -0.005, 0) @ M.scale(1, ry / rx, 1)))
                sm = self.mm('chinstrap', '#5a5440')
                pts = [(rx * 0.98, 0.0, zr + 0.01), (rx * 0.75, -ry * 0.8, zr + 0.025), (0.0, -ry - 0.018, zr + 0.03),
                       (-rx * 0.75, -ry * 0.8, zr + 0.025), (-rx * 0.98, 0.0, zr + 0.01)]
                for q0, q1 in zip(pts[:-1], pts[1:]):
                    add(M.capsule(q0, q1, 0.009, 0.009, sm, seg=4, rings=1))
        elif hat == 'senboshi':
            hm = self.mm('hat', '#7e784c')
            # the crown: straight sides, a flat top with its seam
            add(M.loft([(0.115, 0.118, 0.13, 0, -0.006), (0.20, 0.123, 0.135, 0, -0.01), (0.245, 0.117, 0.129, 0, -0.012),
                        (0.27, 0.095, 0.105, 0, -0.012), (0.282, 0.05, 0.055, 0, -0.012)], hm, seg=18, cap_bottom=True,
                       cap_top=True))
            bd = self.mm('hatband', '#ffffff')
            add(M.cylinder(0.128, 0.128, 0.072, bd, seg=18, z0=0.104).transformed(M.translate(0, -0.006, 0) @ M.scale(1, 1.08, 1)))
            vm = self.mm('visor', '#7a7448', spec=0.06, gloss=10)
            add(M.ellipsoid(0.095, 0.065, 0.013, vm, (0, 0.135, 0.118), seg=14).transformed(
                M.translate(0, 0.135, 0.118) @ M.rot_x(26) @ M.translate(0, -0.135, -0.118)))
            add(M.extrude([(0.0, 0.03), (0.009, 0.009), (0.03, 0.009), (0.013, -0.004), (0.019, -0.026), (0.0, -0.012),
                           (-0.019, -0.026), (-0.013, -0.004), (-0.03, 0.009), (-0.009, 0.009)], 0.012,
                          self.mm('star', '#d8b040', spec=0.3, gloss=16))
                .transformed(M.translate(0, 0.142, 0.19) @ M.rot_x(90)))
            fm = self.mm('havelock', '#7e784c')
            for x, ry_ in ((-0.115, 66), (-0.04, 16), (0.04, -16), (0.115, -66)):
                add(M.rounded_box(0.072, 0.012, 0.17, fm, (0, 0, -0.08), r=0.005, seg=4, p=4).transformed(
                    M.translate(x * 0.95, -0.115 + abs(x) * 0.32, 0.12) @ M.rot_z(ry_) @ M.rot_x(-16)))
            sm = self.mm('chinstrap', '#4a3626')
            for sx in (1, -1):
                add(M.capsule((sx * 0.118, 0.04, 0.12), (sx * 0.05, 0.10, -0.005), 0.007, 0.007, sm, seg=4, rings=1))
        else:
            super()._hat(add0)


# ------------------------------------------------------------ death

def line_death(base, ctx, hold=None, delay=0.0, late_hands=False, head0=22.0):
    """humanoid.anim_death's fall (struck, staggers back, knees buckle, falls
    on the back), started from the figure's own default pose: the legs from
    its stance, the arms by IK following the chest, so frame 0 is the default
    frame; the hands bring the weapon across the chest (port) as he goes
    down, or fold over the belly with no weapon (hold=None). No arms flung
    out, the weapon never leaves the hands. delay: the fall starts later."""
    down = ctx.event('down', 0.55) if ctx is not None else 0.55
    down = min(max(down or 0.55, 0.35), 0.85)
    L = 0.79

    def leg(side):
        f = np.asarray(base[f'foot_{side}'], np.float32)
        hx = 0.11 if side == 'r' else -0.11
        return (math.degrees(math.atan2(float(f[1]), L)), -math.degrees(math.atan2(float(f[0]) - hx, L)), 0.0)
    fk = {'thigh_r': leg('r'), 'shin_r': (0, 0, 0), 'thigh_l': leg('l'), 'shin_l': (0, 0, 0), 'follow': 1.0,
          'death_t': 0.0}
    B = dict(base)
    B['root_rot'] = _v((0, 0, 0))
    if hold == 'port':
        hands = {'hand_r': (0.20, 0.16, 0.98), 'w_main': (-14, -38, 0), 'support': 1.0, 'follow_rot': 1.0,
                 'elbow_l': (-0.5, -0.5, -0.7), 'elbow_r': (0.5, -0.6, -0.4)}
    else:
        hands = {'hand_r': (0.12, 0.20, 1.00), 'hand_l': (-0.08, 0.20, 1.04), 'elbow_r': (0.6, -0.5, -0.4),
                 'elbow_l': (-0.6, -0.5, -0.4)}
    k = [
        (0.0, {}),
        (0.18, dict({} if late_hands else hands, root_rot=(12, 0, 6), root_pos=(0, -0.06, -0.02), chest=(12, 0, 8),
                    head=(head0, 0, 0), spine=(0, 0, 0), thigh_r=(10, -4, 0), shin_r=(-18, 0, 0), order=0.0)),
        (0.38, dict(hands, root_rot=(30, 0, 10), root_pos=(0, -0.22, -0.28), chest=(14, 0, 8), head=(24, 0, 0),
                    thigh_r=(62, -8, 0), shin_r=(-72, 0, 0), thigh_l=(44, 8, 0), shin_l=(-62, 0, 0))),
        (0.62, dict(hands, root_rot=(80, 0, 8), root_pos=(0, -0.55, -0.72), chest=(6, 0, 4), head=(10, 0, 10),
                    thigh_r=(40, -10, 0), shin_r=(-40, 0, 0), thigh_l=(30, 12, 0), shin_l=(-30, 0, 0))),
        (0.78, dict(hands, root_rot=(92, 0, 6), root_pos=(0, -0.62, -0.79), chest=(-2, 0, 2), head=(-10, 0, 18),
                    thigh_r=(14, -14, 0), shin_r=(-12, 0, 0), thigh_l=(8, 16, 0), shin_l=(-16, 0, 0))),
        (1.0, dict(hands, root_rot=(90, 0, 6), root_pos=(0, -0.62, -0.78), chest=(0, 0, 2), head=(-6, 0, 22),
                   thigh_r=(10, -14, 0), shin_r=(-10, 0, 0), thigh_l=(6, 16, 0), shin_l=(-14, 0, 0))),
    ]
    anim = H.keys(dict(B, **{c: _v(v) for c, v in fk.items()}), k)

    def fn(t):
        tt = min(1.0, max(0.0, (t - delay) / (1 - delay)))
        p = anim(H._warp(tt, [(0.62, down)]))
        p['death_t'] = _v(t)
        return p
    act = FM.Act(fn, legs='fk', arms='ik')
    act.flatten = True
    return act


def _gun_lie_R(t):
    """The weapon's orientation through the fall: upright at the start, lying
    flat beside the body (muzzle toward the feet) at the end."""
    u = min(1.0, max(0.0, t / 0.75))
    u = u * u * (3 - 2 * u)
    d0 = np.array([0.0, 0.25, 1.0], np.float32)
    d1 = np.array([0.18, 1.0, 0.02], np.float32)
    z = d0 / np.linalg.norm(d0) * (1 - u) + d1 / np.linalg.norm(d1) * u
    z /= np.linalg.norm(z)
    x = np.cross(z, np.array([0, 0, 1.0], np.float32))
    if np.linalg.norm(x) < 1e-3:
        x = np.array([1.0, 0, 0], np.float32)
    x /= np.linalg.norm(x)
    y = np.cross(z, x)
    return np.stack([x, y, z], 1).astype(np.float32)


def _grounded(mesh):
    """Nothing below the ground: what a fall pushes under it lies flat on it."""
    V = mesh.V.copy()
    V[..., 2] = np.maximum(V[..., 2], 0.004)
    return M.Mesh(V, mesh.N, mesh.M)


# ------------------------------------------------------------ the soldier

@archetype('line_soldier')
class LineSoldier(LF.LeagueSoldier):
    def __init__(self, unit, params):
        super().__init__(unit, params)
        bp = dict(params)
        bp['weapon'] = params.get('weapon', params.get('gun'))
        bp['offhand'] = params.get('offhand')
        self.body = LineBody(bp)
        self.mats = self.body.mats
        self.parts = self.body.parts()
        self._props = {}
        if self.gun in LG.GUNS:
            self.gspec = LG.GUNS[self.gun]
        self.order_grip = float(params.get('order_grip', 0.0))
        o = 1.0 if self.order_grip else 0.0
        self.base['order'] = _v(o)
        for name in ('aim', 'guard'):
            self.poses[name].setdefault('order', 0.0)
        if 'march' in params.get('poses', {}):
            self.poses['march'].setdefault('order', 0.0)

    def prop(self, name, **kw):
        fn = getattr(LG, name, None) if name in LG.GUNS or name in ('ammo_can_carried',) else None
        if fn is None:
            return super().prop(name, **kw)
        key = (name, tuple(sorted(kw.items())))
        if key not in self._props:
            m = fn(self.mats, **kw)
            if name in LG.GUNS:
                k = float(self.p.get('gun_thick', 1.6))
                m = m.transformed(M.scale(k, k, 1.0))
            self._props[key] = m
        return self._props[key]

    def anim_run(self, n):
        if 'march' not in self.poses:
            return super().anim_run(n)
        saved = self.base
        self.base = dict(saved)
        self.base.update({k: _v(v) for k, v in self.poses['march'].items()})
        try:
            return super().anim_run(n)
        finally:
            self.base = saved

    def anim_lunge(self, ctx):
        if not self.p.get('thrust_level'):
            return super().anim_lunge(ctx)
        B = self.base
        g = self.P('guard')
        st = ctx.event('reach_max', 0.5) if ctx is not None else 0.5
        st = min(max(st or 0.5, 0.35), 0.7)
        jab = dict(g)
        jab.update({'root_pos': _v((0, 0.30, -0.14)), 'hand_r': _v((0.18, 0.44, 1.16)), 'w_main': _v((-74, 0, 16)),
                    'foot_l': _v((-0.18, 0.62, A)), 'foot_r': _v((0.2, -0.16, A)), 'toe_r': _v(25.0),
                    'chest': _v((-8, 0, 6)), 'spine': _v((-6, 0, 0))})
        g = dict(g, toe_r=B['toe_r'])
        k = [(0.0, {}), (0.25, g), (0.5, jab), (0.6, jab), (0.78, g), (1.0, {c: B[c] for c in g if c in B})]
        anim = H.keys(B, k)
        return FM.Act(lambda t: anim(H._warp(t, [(0.5, st)])))

    def frame(self, action, f, n, ctx):
        mesh = super().frame(action, f, n, ctx)
        if self.anim_name(action) == 'death':
            mesh = _grounded(mesh)
            self._local = mesh
        return mesh

    def action(self, action, ctx):
        if self.anim_name(action) == 'death':
            key = (action, ctx.n if ctx else None)
            if key not in self._cache:
                self._cache[key] = line_death(self.base, ctx, 'port' if self.gun else None,
                                              late_hands=bool(self.p.get('death_late_hands')),
                                              head0=float(self.p.get('death_head0', 22.0)))
            return self._cache[key]
        return super().action(action, ctx)

    def _order_T(self, T, pose):
        o = float(pose.get('order', 0.0)) * self.order_grip
        return T @ M.translate(0, 0, -o) if o > 1e-4 else T

    def pose_full(self, pose, act, weapon, offhand='same'):
        if getattr(act, 'flatten', False) and weapon is not None:
            mesh, info = super().pose_full(pose, act, None, offhand)
            T = self._order_T(info['T']['r'], info['pose']).copy()
            t = float(pose.get('death_t', 0.0))
            u = min(1.0, max(0.0, (t - 0.45) / 0.35))
            u = u * u * (3 - 2 * u)
            if u > 0:
                Rm = T[:3, :3]
                z = Rm[:, 2].copy()
                z[2] = 0.0
                if np.linalg.norm(z) < 1e-3:
                    z = _v((0, 1, 0))
                z /= np.linalg.norm(z)
                y = _v((0, 0, -1))
                x = np.cross(y, z)
                Rf = np.stack([x, y, z], 1).astype(np.float32)
                T[:3, :3] = FM._orthonormal(Rm * (1 - u) + Rf * u)
                # down onto the ground beside him
                T[2, 3] = T[2, 3] * (1 - u) + 0.06 * u
            info['T'] = dict(info['T'], r=T)
            return M.Mesh.concat([mesh, self.prop(weapon).transformed(T)]), info
        if getattr(act, 'gun_lie', False) and weapon is not None:
            mesh, info = super().pose_full(pose, act, None, offhand)
            Wh = info['W']['hand_r']
            T = np.eye(4, dtype=np.float32)
            T[:3, :3] = _gun_lie_R(float(pose.get('death_t', 1.0)))
            T[:3, 3] = Wh[:3, 3] + Wh[:3, :3] @ _v((0, 0.01, -H.GRIP))
            T = self._order_T(T, info['pose'])
            info['T'] = dict(info['T'], r=T)
            return M.Mesh.concat([mesh, self.prop(weapon).transformed(T)]), info
        if not self.order_grip or weapon is None or weapon != self.weapon or float(pose.get('order', 0.0)) < 1e-4:
            return super().pose_full(pose, act, weapon, offhand)
        mesh, info = super().pose_full(pose, act, None, offhand)
        T = self._order_T(info['T']['r'], info['pose'])
        return M.Mesh.concat([mesh, self.prop(weapon).transformed(T)]), info

    def _dropped(self, act, t, weapon):
        if not self.order_grip:
            return super()._dropped(act, t, weapon)
        key = ('drop', id(act))
        if key not in self._cache:
            p0 = act.fn(act.drop_t)
            _, Ts, _ = self._solve(p0, act)
            Ts = dict(Ts)
            Ts['r'] = self._order_T(Ts['r'], p0)
            self._cache[key] = Ts
        return super()._dropped(act, t, weapon)


# ------------------------------------------------------------ the machine-gun team

@archetype('line_mg_team')
class LineMgTeam:
    """The gunner (right, a little ahead) and his assistant (left, behind)."""

    def __init__(self, unit, params):
        self.unit = unit
        self.p = params
        sp = dict(params.get('soldier', {}))
        gp = dict(sp, **params.get('gunner', {}))
        ap = dict(sp, **params.get('assistant', {}))
        gp['gun'] = None
        ap['gun'] = None
        self.gunner = LineSoldier(unit, gp)
        self.assistant = LineSoldier(unit, ap)
        # one material table: the assistant's materials are added to the gunner's
        self.mats = self.gunner.mats
        am = self.assistant.mats
        remap = {}
        for i, name in enumerate(am.names):
            if name not in self.mats:
                self.mats.add(name, am.color[i], tint=am.tint[i], spec=am.spec[i], gloss=am.gloss[i], metal=am.metal[i])
            remap[i] = self.mats.names.index(name)
        idx = np.array([remap[i] for i in range(len(am.names))], np.int32)
        self.assistant.parts = [(b, M.Mesh(m.V, m.N, idx[m.M]), t) for b, m, t in self.assistant.parts]
        self.assistant.mats = self.mats
        self.assistant.body.mats = self.mats
        self.gunner_at = _v(params.get('gunner_at', (0.38, 0.22, 0.0)))
        self.assistant_at = _v(params.get('assistant_at', (-0.44, -0.26, 0.0)))
        self.mount = _v(params.get('mount', (0.10, 0.30, 0.0)))
        self.tri_h = float(params.get('tripod_h', 0.40))
        self._fx = []
        self._local = None
        self._cache = {}

    def loops(self, action):
        return action in ('DEFAULT', 'RUN', 'FIDGET')

    def shots(self, action):
        return list(self.p.get('shots', {}).get(action, []))

    def _gun(self, belt=False):
        k = ('gun', belt)
        if k not in self._cache:
            self._cache[k] = LG.m1917(self.mats, belt=belt).transformed(M.scale(1.35, 1.35, 1.0))
        return self._cache[k]

    # -- the march (RUN): the gun on the gunner's right shoulder, his right hand on its jacket;
    # the assistant with the folded tripod over his left shoulder and a can in his right hand,
    # a quarter step out of phase
    TRIPOD_ON_SHOULDER = M.translate(-0.25, 0.10, 0.40) @ M.rot_x(-62) @ M.translate(0, 0, -0.40)
    GUN_ON_SHOULDER = M.translate(0.25, -0.14, 0.43) @ M.rot_z(-30) @ M.rot_x(-97)
    CHEST_REST = 0.92 + 0.08 + 0.20

    def _march(self, action, t, n):
        G_, A_ = self.gunner, self.assistant
        out = []
        # where the gunner's hand holds the jacket, in model space of an upright figure
        hold = (self.GUN_ON_SHOULDER @ _v((0, 0.06 * 1.35, 0.46, 1.0)).reshape(4, 1))[:3, 0] + _v((0, 0, self.CHEST_REST))
        for who, at, phase, role in ((G_, self.gunner_at, 0.0, 'gun'), (A_, self.assistant_at, 0.27, 'tripod')):
            p = H.anim_run(who.base, 'spear', 'shield_round', n).fn((t + phase) % 1.0)
            p['follow'] = _v(1.0)
            if role == 'gun':
                p['hand_r'] = _v(hold) + _v((0.0, 0.0, -0.03))
                p['elbow_r'] = _v((0.9, -0.2, -0.5))
            else:
                p['hand_l'] = _v((-0.20, 0.30, 1.48))
                p['elbow_l'] = _v((-0.9, -0.3, -0.4))
                p['hand_r'] = _v((0.34, 0.02, 0.80)) + (p['hand_r'] - who.base['hand_r']) * 0.4
            m, info = who.pose_full(p, FM.Act(None, loop=True), None, None)
            C = info['W']['chest']
            if role == 'gun':
                m = M.Mesh.concat([m, self._gun().transformed(C @ self.GUN_ON_SHOULDER)])
            else:
                m = M.Mesh.concat([m, LG.m1917_tripod(self.mats, h=0.40, folded=True).transformed(C @ self.TRIPOD_ON_SHOULDER)])
                T = info['T']['r']
                m = M.Mesh.concat([m, LG.ammo_can_carried(self.mats).transformed(M.translate(*T[:3, 3]) @ M.rot_z(8))])
            out.append(m.transformed(M.translate(*at)))
        return out

    # -- in action (also the default pose, like the stock TOW team): the gunner sits low behind
    # the gun, hands on the grip; the assistant kneels at the gun's left, facing the receiver,
    # feeding the belt from the open can between them
    GPOS = (-0.02, -0.86)
    APOS = (-0.80, -0.12)
    AYAW = -90.0

    def _gunner_pose(self, recoil=0.0):
        B = self.gunner.base
        k = {'root_pos': (0.0, -0.08, -0.58), 'foot_l': (-0.22, 0.26, A), 'foot_r': (0.22, -0.28, 0.10),
             'toe_r': 50.0, 'fyaw_r': -20.0, 'spine': (-22, 0, 0), 'chest': (-16, 0, 0), 'head': (16, 0, 0),
             'elbow_r': (0.8, -0.5, -0.4), 'elbow_l': (-0.8, -0.5, -0.4)}
        p = dict(B)
        p.update({c: _v(v) for c, v in k.items()})
        p['follow'] = _v(0.0)
        p['support'] = _v(0.0)
        p['root_pos'] = p['root_pos'] + _v((0, -0.045, 0.0)) * recoil
        p['chest'] = p['chest'] + _v((4, 0, 0)) * recoil
        return p

    def _to_local(self, world, at, yaw):
        c, s = math.cos(math.radians(-yaw)), math.sin(math.radians(-yaw))
        d = _v(world) - _v(at)
        return _v((c * d[0] - s * d[1], s * d[0] + c * d[1], d[2]))

    def _feed_points(self, GT):
        """The belt from the open can up into the feedway on the gun's left."""
        feed = (GT @ _v((-0.10 * 1.35, -0.05, 0.07, 1.0)).reshape(4, 1))[:3, 0]
        can = _v((self.mount[0] - 0.42, self.mount[1] - 0.12, 0.20))
        mid = (feed + can) / 2 + _v((-0.02, 0.0, -0.02))
        return feed, mid, can

    def _assistant_pose(self, t, action='DEFAULT', GT=None, f=0, n=15):
        B = self.assistant.base
        apos = _v((self.mount[0] + self.APOS[0], self.mount[1] + self.APOS[1], 0))
        feed, mid, can = self._feed_points(GT if GT is not None else self._gun_T())
        pull = 0.0
        if action in ('ATTACK1', 'FORTIFY'):
            pull = 0.5 + 0.5 * math.sin(2 * math.pi * t * 3)
        hl = self._to_local(feed + _v((-0.05, 0.0, 0.02)), apos, self.AYAW)
        hr = self._to_local(mid * (1 - 0.3 * pull) + can * 0.3 * pull + _v((0, 0, 0.03)), apos, self.AYAW)
        k = {'root_pos': (0.0, 0.0, -0.52), 'foot_l': (-0.18, 0.22, A), 'foot_r': (0.18, -0.34, 0.10),
             'toe_r': 50.0, 'fyaw_r': -10.0, 'spine': (-20, 0, 0), 'chest': (-10, 0, 0), 'head': (14, 0, 8),
             'hand_r': tuple(hr), 'hand_l': tuple(hl), 'elbow_r': (0.6, -0.6, -0.4), 'elbow_l': (-0.6, -0.6, -0.4)}
        if action == 'FIDGET':
            k['head'] = (6, 0, 8 + 40 * math.sin(math.pi * t))
        if action == 'VICTORY':
            u = max(0.0, math.sin(math.pi * min(1.0, t / 0.85)))
            k['hand_r'] = tuple(_v(hr) * (1 - u) + _v((0.30, 0.10, 1.90)) * u)
            k['elbow_r'] = (0.8, -0.3, 0.2)
            k['spine'] = (-20 + 18 * u, 0, 0)
        p = dict(B)
        p.update({c: _v(v) for c, v in k.items()})
        p['follow'] = _v(0.0)
        return p

    def _gun_T(self, recoil=0.0, tip=0.0):
        m = self.mount
        return M.translate(m[0], m[1] - 0.26 - 0.05 * recoil, self.tri_h + 0.09 - 0.07 * tip) @ M.rot_x(-90 - 14 * tip)

    def _ground_kit(self):
        m = self.mount
        out = [LG.m1917_tripod(self.mats, h=self.tri_h).transformed(M.translate(*m))]
        # the open can the belt comes from, beside the receiver; a full one behind the assistant
        out.append(LG.ammo_can(self.mats, lid_open=True).transformed(M.translate(m[0] - 0.42, m[1] - 0.12, 0) @ M.rot_z(10)))
        out.append(LG.ammo_can(self.mats).transformed(M.translate(m[0] - 1.15, m[1] - 0.55, 0) @ M.rot_z(70)))
        out.append(LG.ammo_can(self.mats).transformed(M.translate(m[0] - 1.22, m[1] - 0.25, 0) @ M.rot_z(80)))
        return out

    def _belt(self, pts):
        cl = self.gunner.body.mm('belt_cloth', '#8a7e5e')
        br = LG.brass(self.mats)
        out = []
        for a, b in zip(pts[:-1], pts[1:]):
            for k in range(4):
                u = (k + 0.5) / 4
                c = a + (b - a) * u
                out.append(M.box(0.05, 0.03, 0.03, cl, tuple(c)))
                out.append(M.capsule(tuple(c + _v((-0.04, 0, 0.012))), tuple(c + _v((0.04, 0, 0.012))), 0.008, 0.006, br,
                                     seg=5, rings=1))
        return M.Mesh.concat(out)

    def _deployed(self, action, t, f, n):
        meshes = self._ground_kit()
        shots = self.shots(action)
        recoil = self.gunner._recoil(t, n, shots) if shots else 0.0
        GT = self._gun_T(recoil)
        meshes.append(self._gun().transformed(GT))
        feed, mid, can = self._feed_points(GT)
        meshes.append(self._belt([feed, mid, can]))
        grip = (GT @ _v((0, 0.11, -0.15, 1.0)).reshape(4, 1))[:3, 0]
        gpos = _v((self.mount[0] + self.GPOS[0], self.mount[1] + self.GPOS[1], 0))
        p = self._gunner_pose(recoil)
        p['hand_r'] = grip - gpos + _v((0.03, 0, 0))
        p['hand_l'] = grip - gpos + _v((-0.09, 0.08, 0.06))
        if action == 'DEFAULT':
            p['chest'] = p['chest'] + _v((1.2 * math.sin(2 * math.pi * t), 0, 0))
        gm, _ = self.gunner.pose_full(p, FM.Act(None), None, None)
        meshes.append(gm.transformed(M.translate(*gpos)))
        apos = _v((self.mount[0] + self.APOS[0], self.mount[1] + self.APOS[1], 0))
        am, _ = self.assistant.pose_full(self._assistant_pose(t, action, GT, f, n), FM.Act(None), None, None)
        meshes.append(am.transformed(M.translate(*apos) @ M.rot_z(self.AYAW)))
        if f in shots:
            mz = (GT @ _v((0, 0, LG.GUNS['m1917']['muzzle'], 1.0)).reshape(4, 1))[:3, 0]
            self._fx.append({'kind': 'flash', 'pos': mz, 'dir': _v((0, 1, 0)), 'size': 0.30})
        if shots and f >= shots[0]:
            mz = (GT @ _v((0, 0, LG.GUNS['m1917']['muzzle'], 1.0)).reshape(4, 1))[:3, 0]
            age = f - shots[0]
            self._fx.append({'kind': 'smoke', 'pos': mz + _v((0, 0.15, 0.05 + 0.02 * age)), 'rad': 0.10 + 0.01 * min(age, 6),
                             'alpha': 0.3, 'seed': 3})
        return meshes

    def _death(self, t, n):
        """From the default pose: the gunner is hit first, lets go of the grip and slumps back
        and to his right, off the gun and clear of the tripod; the assistant, a beat later,
        falls back from the gun's side. The gun, no longer held, tips forward over a few frames."""
        meshes = self._ground_kit()
        tip = min(1.0, max(0.0, (t - 0.18) / 0.22))
        GT = self._gun_T(0.0, tip)
        meshes.append(self._gun().transformed(GT))
        feed, mid, can = self._feed_points(GT)
        meshes.append(self._belt([feed, mid, can]))
        grip = (self._gun_T() @ _v((0, 0.11, -0.15, 1.0)).reshape(4, 1))[:3, 0]
        gpos = _v((self.mount[0] + self.GPOS[0], self.mount[1] + self.GPOS[1], 0))
        K = self._gunner_pose()
        K['hand_r'] = grip - gpos + _v((0.03, 0, 0))
        K['hand_l'] = grip - gpos + _v((-0.09, 0.08, 0.06))
        belly = {'hand_r': (0.14, 0.24, 0.90), 'hand_l': (-0.04, 0.26, 0.94), 'follow': 1.0}
        kg = H.keys(K, [(0.0, {}),
                        (0.12, {'chest': (2, 0, 6), 'head': (26, 0, 0), 'root_pos': (0.0, -0.12, -0.56)}),
                        (0.34, dict(belly, root_rot=(46, 0, -20), root_pos=(0.12, -0.38, -0.68), chest=(6, 0, 4),
                                    spine=(0, 0, 0), head=(14, 0, -10), foot_r=(0.24, -0.20, A), foot_l=(-0.12, 0.06, A),
                                    toe_r=20.0)),
                        (0.52, dict(belly, root_rot=(84, 0, -26), root_pos=(0.24, -0.62, -0.78), chest=(0, 0, 0),
                                    head=(-4, 0, -18), foot_r=(0.30, -0.02, A), foot_l=(-0.06, 0.02, A), toe_r=0.0)),
                        (1.0, dict(belly, root_rot=(88, 0, -30), root_pos=(0.28, -0.66, -0.79), chest=(0, 0, 0),
                                   head=(-8, 0, -22), foot_r=(0.32, 0.00, A), foot_l=(-0.04, 0.04, A), toe_r=0.0))])
        apos = _v((self.mount[0] + self.APOS[0], self.mount[1] + self.APOS[1], 0))
        L = self._assistant_pose(0.0, 'DEFAULT', self._gun_T())
        ka = H.keys(L, [(0.0, {}), (0.30, {}),
                        (0.42, {'chest': (14, 0, -10), 'head': (24, 0, -10), 'root_pos': (0.0, -0.06, -0.50)}),
                        (0.66, dict(belly, root_rot=(55, 0, 6), root_pos=(0.0, -0.34, -0.66), chest=(6, 0, 0),
                                    spine=(0, 0, 0), head=(12, 0, 10), foot_r=(0.18, -0.05, A), toe_r=20.0)),
                        (0.84, dict(belly, root_rot=(86, 0, 10), root_pos=(0.0, -0.52, -0.78), chest=(0, 0, 0),
                                    head=(-6, 0, 18), foot_r=(0.18, 0.12, A), foot_l=(-0.18, 0.30, A), toe_r=0.0)),
                        (1.0, dict(belly, root_rot=(88, 0, 12), root_pos=(0.0, -0.54, -0.79), chest=(0, 0, 0),
                                   head=(-8, 0, 20), foot_r=(0.18, 0.14, A), foot_l=(-0.18, 0.32, A), toe_r=0.0))])
        gm, _ = self.gunner.pose_full(kg(t), FM.Act(None), None, None)
        meshes.append(gm.transformed(M.translate(*gpos)))
        am, _ = self.assistant.pose_full(ka(t), FM.Act(None), None, None)
        meshes.append(am.transformed(M.translate(*apos) @ M.rot_z(self.AYAW)))
        return meshes

    def frame(self, action, f, n, ctx):
        t = rig.frame_times(n, self.loops(action))[f]
        self._fx = []
        if action == 'RUN':
            meshes = self._march(action, t, n)
        elif action == 'DEATH':
            meshes = self._death(t, n)
        else:
            meshes = self._deployed(action, t, f, n)
        mesh = _grounded(M.Mesh.concat(meshes))
        self._local = mesh
        finish_colors(self, dict(self.p.get('soldier', {}), **{k: v for k, v in self.p.items() if k != 'soldier'}))
        return mesh

    def render_cell(self, mesh, mats, cam, W, H_):
        b, t = R.render_cell(mesh, mats, cam, W, H_)
        if not self._fx:
            return b, t
        return FM.draw_effects(b, t, self._fx, FM._facing_of(self._local, mesh), cam)
