"""The regular modern foot units of round 2 (catalogs modern_mod_foot.py and
league_foot.py): the RPG Trooper, the Special Forces, the Super Trooper and
the Combat Engineer. Their gear is in models/regulars_gear.py.

Archetype 'regular_soldier': league_foot's 'league_soldier' (so every
mod_soldier / soldier parameter works) plus

    hat       'covered' (a round steel helmet in a cloth cover) |
              'fast' (a high-cut helmet with rails, the NVG flipped up, a
              headset) | 'exo' (the power armour's closed helmet)
    goggles   bool: goggles on the helmet's brow
    respirator  bool: a half-mask respirator with two filters
    armor     'exo': the Super Trooper's segmented plates (materials 'plate'
              for the civ-coloured chest and pauldrons, 'armor_grey' for the
              limb plates, 'armor_dark' for the joints and suit) and the leg
              pistons
    kit       list of 'rpg_rig' | 'rocket_carrier' | 'plate_carrier' |
              'assault_pack' | 'kneepads' | 'gloves' | 'holster' |
              'tank_pack' | 'satchel' | 'cutters' | 'flak_pouches' | 'flag'
    gun       also a regulars_gear.GUNS name
    glows     bool: the armour's visor and lights drawn glowing
"""
import math

import numpy as np

import mesh as M
import render as R
from models import archetype
from models import humanoid as H
from models import foot_modern as FM
from models import league_foot as LF
from models import modern_mod_foot as MF
from models import regulars_gear as RG
from models.props import mat

_v = FM._v


def _bar(a, b, r, m, seg=6):
    return M.capsule(a, b, r, r, m, seg=seg, rings=1)


class RegBody(LF.LeagueBody):
    def _rings(self):
        p = self.full
        bulk = H.build_params(p).get('bulk', 1.0)
        tw = H.TORSO_W * bulk
        mus = H.muscle_amount(p)
        rings = [(0.0, 0.165, 0.105, 0, 0.0), (0.10, 0.19, 0.12, 0, 0.012), (0.19, 0.205, 0.122, 0, 0.008),
                 (0.25, 0.185, 0.108, 0, -0.005), (0.30, 0.10, 0.07, 0, -0.01), (0.33, 0.05, 0.05, 0, -0.01)]
        rings = [(z, rx * tw, ry * (1 + (tw - 1) * 0.6), dx, dy) for z, rx, ry, dx, dy in rings]
        if mus > 0:
            rings = [(z, rx * (1 + 0.09 * mus * (0.10 <= z <= 0.26)), ry * (1 + 0.10 * mus * (0.05 <= z <= 0.26)),
                      dx, dy + 0.012 * mus * (0.08 <= z <= 0.22)) for z, rx, ry, dx, dy in rings]
        return rings, tw, H.LIMB * bulk

    def parts(self):
        out = super().parts()
        p = self.full
        rings, tw, lb = self._rings()
        rx, ry = rings[1][1], rings[1][2] + rings[1][4]
        vest = p.get('vest')
        if vest == 'flak':
            ry += 0.045
            rx += 0.045

        def add(bone, mesh, tag=None):
            out.append((bone, mesh, tag))

        kit = p.get('kit', [])
        sm = self.mm('strap', '#3a3628')
        if 'rpg_rig' in kit:
            add('chest', RG.rpg_rig(self.mats, ry=ry, rx=rx))
        if 'rocket_carrier' in kit:
            add('chest', RG.rocket_carrier(self.mats), 'backpack')
        if 'plate_carrier' in kit:
            add('chest', RG.plate_carrier(self.mats, ry=ry, rx=rx))
        if 'assault_pack' in kit:
            add('chest', RG.assault_pack(self.mats, ry=ry), 'backpack')
        if 'tank_pack' in kit:
            self.mm('pack_band', '#8a8a6a')
            add('chest', RG.tank_pack(self.mats), 'backpack')
        if 'flak_pouches' in kit:
            pm = self.mm('pouch', '#5a5a44')
            bk = mat(self.mats, 'buckle', '#2a2a26', spec=0.3, gloss=16)
            yf = ry + 0.02
            # a collar round the neck, two pouches each side of the front, a light on the left strap
            add('chest', M.loft([(0.27, rings[3][1] * 0.75, rings[3][2] + 0.03), (0.33, 0.13, 0.11)],
                                self.mm('vest', '#606650'), seg=16, cap_bottom=False, cap_top=False))
            for sx in (1, -1):
                for k, z in enumerate((0.02, 0.10)):
                    add('chest', M.rounded_box(0.075, 0.05, 0.075, pm, (sx * 0.10, yf + 0.025, z), r=0.012, seg=6, p=4))
                    add('chest', M.box(0.08, 0.054, 0.018, bk, (sx * 0.10, yf + 0.026, z + 0.032)))
            add('chest', M.rounded_box(0.035, 0.03, 0.05, bk, (-0.12, yf + 0.0, 0.21), r=0.008, seg=6, p=4))
        if 'satchel' in kit:
            add('pelvis', RG.satchel(self.mats))
            # its strap: from the left hip over the right shoulder
            r1, r2 = rings[1], rings[2]
            g = 0.06 if vest == 'flak' else 0.03
            pts = [(-r1[1] * 0.9, r1[2] * 0.4 + g, -0.10), (-r1[1] * 0.35, r1[2] + g, 0.08),
                   (r2[1] * 0.25, r2[2] + g, 0.2), (r2[1] * 0.62, r2[2] * 0.62 + g, 0.31), (r2[1] * 0.72, 0.0, 0.35),
                   (r2[1] * 0.5, -r2[2] - g, 0.25), (-r1[1] * 0.4, -r1[2] - g, 0.0), (-r1[1] * 0.9, -r1[2] * 0.3, -0.10)]
            st = self.mm('satchel_strap', '#4e4830')
            for a, b in zip(pts[:-1], pts[1:]):
                add('chest', _bar(a, b, 0.02, st))
        if 'belt_kit' in kit:
            # ammunition pouches either side of the buckle, a canteen behind the right hip, a butt pack
            pm = self.mm('pouch', '#6a5e42')
            bk = mat(self.mats, 'buckle', '#2a2a26', spec=0.3, gloss=16)
            cm = self.mm('canteen', '#4e5236', spec=0.1)
            T = M.scale(tw, 1 + (tw - 1) * 0.8, 1)
            for sx in (1, -1):
                add('pelvis', M.rounded_box(0.07, 0.05, 0.08, pm, (sx * 0.085, 0.15, 0.055), r=0.012, seg=6, p=4).transformed(T))
                add('pelvis', M.box(0.075, 0.054, 0.02, bk, (sx * 0.085, 0.15, 0.09)).transformed(T))
            add('pelvis', M.box(0.05, 0.02, 0.04, bk, (0, 0.185, 0.09)).transformed(T))
            add('pelvis', M.rounded_box(0.06, 0.09, 0.11, cm, (0.15, -0.10, 0.04), r=0.025, seg=8, p=3).transformed(T))
            add('pelvis', M.rounded_box(0.16, 0.06, 0.09, pm, (0, -0.16, 0.05), r=0.015, seg=6, p=4).transformed(T))
        if 'etool' in kit:
            add('pelvis', RG.entrenching_tool(self.mats))
        if 'cutters' in kit:
            add('pelvis', RG.wire_cutters(self.mats))
        if 'kneepads' in kit:
            km = self.mm('kneepad', '#3a362c')
            for side in 'rl':
                add(f'shin_{side}', M.rounded_box(0.11, 0.06, 0.12, km, (0, 0.06, -0.03), r=0.03, seg=8, p=3))
                add(f'shin_{side}', M.cylinder(0.072 * lb, 0.072 * lb, 0.025, km, seg=12, z0=-0.10))
        if 'gloves' in kit:
            gm = self.mm('glove', '#2e2c28')
            for side, sx in (('r', 1), ('l', -1)):
                add(f'hand_{side}', M.ellipsoid(0.047 * H.HAND, 0.057 * H.HAND, 0.066 * H.HAND, gm, (0, 0.008, -0.055), seg=9))
                add(f'hand_{side}', M.ellipsoid(0.024, 0.026, 0.038, gm, (-sx * 0.03, 0.035, -0.04), seg=6))
                add(f'forearm_{side}', M.cylinder(0.05 * lb, 0.05 * lb, 0.05, gm, seg=10, z0=-0.26))
                if p.get('top') == 'long_sleeved':
                    # the sleeve's cuff over the glove's gauntlet, and an elbow patch: the sleeve breaks at the
                    # elbow like the stock soldier's
                    cm = self.mm('cuff', '#3a3830')
                    add(f'forearm_{side}', M.cylinder(0.058 * lb, 0.057 * lb, 0.03, cm, seg=12, z0=-0.215))
                    add(f'arm_{side}', M.cylinder(0.072 * lb, 0.07 * lb, 0.04, cm, seg=12, z0=-0.20))
        if 'holster' in kit:
            hm = self.mm('holster', '#3a3428')
            dk = mat(self.mats, 'gun_dark', '#1e1f22', spec=0.3, gloss=16)
            add('thigh_r', M.rounded_box(0.05, 0.09, 0.17, hm, (0.105 * lb, 0.02, -0.17), r=0.015, seg=6, p=4))
            add('thigh_r', M.box(0.04, 0.05, 0.06, dk, (0.105 * lb, 0.0, -0.06)))
            add('thigh_r', M.cylinder(0.098 * lb + 0.004, 0.096 * lb + 0.004, 0.025, hm, seg=12, z0=-0.13))
            add('thigh_r', M.cylinder(0.09 * lb + 0.004, 0.088 * lb + 0.004, 0.025, hm, seg=12, z0=-0.25))
        if 'flag' in kit:
            fm = self.mm('flag_patch', '#4a4a40')
            add('arm_l', M.rounded_box(0.012, 0.07, 0.055, fm, (-0.083 * lb / 1.35 * 1.35 - 0.004, 0.0, -0.07), r=0.004, seg=4))
        if p.get('armor') == 'exo':
            self._exo(add, rings, tw, lb)
        return self._cover_skin(out)

    def _cover_skin(self, out):
        """The body's skin-colored joint spheres and muscle volumes poke out
        of long sleeves and trousers (the shoulder balls, the quads of a
        muscular build): draw those in the garment's material."""
        p = self.full
        skin = self.mats['skin']
        sleeve = self.mats['top'] if p.get('top') == 'long_sleeved' and 'top' in self.mats else None
        legs = self.mats['trousers'] if p.get('legs') == 'trousers' and 'trousers' in self.mats else None
        res = []
        for bone, mesh, tag in out:
            new = None
            if p.get('armor') == 'exo' and bone.startswith(('arm_', 'forearm_', 'hand_')):
                new = self.mats['armor_dark']      # no skin anywhere in the suit
            elif bone.startswith('arm_') and sleeve is not None and len(mesh) and mesh.V[..., 2].min() > -0.12:
                new = sleeve        # the shoulder ball only: the stock sleeve's elbow break stays
            elif bone.startswith(('thigh_', 'shin_')) and legs is not None:
                new = legs
            if new is not None and len(mesh) and np.all(mesh.M == skin):
                mesh = mesh.with_material(new)
            res.append((bone, mesh, tag))
        return res

    # -- the power armour
    def _exo(self, add, rings, tw, lb):
        """Plates of a warm gunmetal over a darker suit: the chest plate (with
        panel lines), the back plate and the upper-arm plates in the civ
        color; pauldrons of three lames seated on the shoulder joint;
        armoured upper arms, elbows, vambraces and gauntlets; a segmented
        waist ring and hip plates; front and back plates on thighs and shins,
        big knee guards. No lights but the pack's and the visor."""
        pl = self.mm('plate', '#3c4aa0', spec=0.45, gloss=22)
        gm = self.mm('armor_grey', '#7c7a6e', spec=0.45, gloss=22, metal=True)
        g2 = self.mm('armor_grey2', '#64625a', spec=0.45, gloss=22, metal=True)
        dk = self.mm('armor_dark', '#3a3934', spec=0.35, gloss=18, metal=True)
        g = 0.045
        # the suit's yoke over the shoulders, the chest plate a shell over the
        # front (narrower than the chest: grey side plates under the arms),
        # panel lines on it, the back plate behind, a collar
        cr = [(z, rx + g, ry + g * 1.2, dx, dy) for z, rx, ry, dx, dy in rings[:4]]
        add('chest', M.loft([(z, rx + 0.022, ry + 0.026, dx, dy) for z, rx, ry, dx, dy in rings[1:5]], dk, seg=18,
                            cap_bottom=False, cap_top=True))
        front = [(-0.02, rings[0][1] + g * 0.6, rings[0][2] + g, 0, 0)] + cr[1:] + \
                [(0.29, rings[4][1] + 0.06, rings[4][2] + 0.05, 0, 0)]
        add('chest', H._shell(front, pl, math.radians(34), math.radians(146), seg=14))
        for a0, a1 in ((8, 34), (146, 172)):
            add('chest', H._shell([(z, rx - 0.004, ry - 0.004, dx, dy) for z, rx, ry, dx, dy in front[:4]], g2,
                                  math.radians(a0), math.radians(a1), seg=4))
        yf = rings[1][2] + rings[1][4] + g * 1.2
        add('chest', M.box(rings[1][1] * 1.05, 0.02, 0.016, dk, (0, yf + 0.008, 0.16)))              # panel lines
        add('chest', M.box(rings[1][1] * 0.9, 0.02, 0.016, dk, (0, yf - 0.004, 0.06)))
        for sx in (1, -1):
            add('chest', M.box(0.016, 0.02, 0.09, dk, (sx * 0.09, yf + 0.004, 0.11)))
        back = [(-0.02, rings[0][1] + g * 0.6, rings[0][2] + g, 0, 0)] + cr[1:3] + \
               [(0.27, rings[3][1] + 0.03, rings[3][2] + 0.04, 0, 0)]
        add('chest', H._shell(back, pl, math.radians(200), math.radians(340), seg=14))
        add('chest', M.loft([(0.26, 0.15, 0.12), (0.36, 0.115, 0.10)], dk, seg=16, cap_bottom=False, cap_top=True))
        # the abdomen: bands, gunmetal and dark
        for i, z in enumerate((-0.04, 0.035, 0.11)):
            add('spine', M.cylinder(0.165 * tw + 0.035, 0.17 * tw + 0.035, 0.06, gm if i != 1 else dk, seg=16, z0=z)
                .transformed(M.scale(1, 0.78, 1)))
        # the waist ring in eight segments with gaps, over a dark belt; hip plates all round
        add('pelvis', M.loft([(-0.02, 0.19 * tw + 0.02, 0.14 * tw + 0.02), (0.10, 0.19 * tw + 0.018, 0.14 * tw + 0.018)],
                             dk, seg=16, cap_bottom=True, cap_top=False))
        for k in range(8):
            ang = 22.5 + 45 * k
            a = math.radians(ang)
            x, y = (0.19 * tw + 0.035) * math.cos(a), (0.14 * tw + 0.035) * math.sin(a)
            add('pelvis', M.rounded_box(0.09, 0.03, 0.09, g2, (0, 0, 0.05), r=0.01, seg=6, p=4).transformed(
                M.translate(x, y, 0) @ M.rot_z(ang - 90)))
        for ang in (-60, -25, 25, 60, 90, -90, 150, 180, 210):
            a = math.radians(ang + 90)
            x, y = 0.19 * tw * math.cos(a) * 1.12, 0.14 * tw * math.sin(a) * 1.2
            T = M.translate(x, y, 0.0) @ M.rot_z(ang) @ M.rot_x(-8)
            add('pelvis', M.rounded_box(0.12, 0.028, 0.15, gm, (0, 0, -0.075), r=0.01, seg=6, p=4).transformed(T))
        for side, sx in (('r', 1), ('l', -1)):
            # pauldrons: three lames seated on the shoulder joint, stepping down and out
            for k in range(3):
                T = M.translate(sx * (0.025 + 0.03 * k), 0.0, 0.10 - 0.055 * k) @ M.rot_y(sx * (14 + 13 * k))
                add(f'arm_{side}', M.rounded_box(0.24 - 0.025 * k, 0.27 - 0.025 * k, 0.05, gm if k != 1 else g2,
                                                 (0, 0, 0), r=0.016, seg=6, p=4).transformed(T))
            # armoured upper arm (civ color), a dark elbow cop, a gunmetal vambrace, a gauntlet
            add(f'arm_{side}', M.capsule((0, 0, -0.03), (0, 0, -0.25), 0.072 * lb + 0.012, 0.066 * lb + 0.012, pl,
                                         seg=12, rings=2))
            add(f'forearm_{side}', M.rounded_box(0.11, 0.12, 0.09, dk, (0, -0.02, 0.0), r=0.025, seg=6, p=3))
            add(f'forearm_{side}', M.loft([(-0.24, 0.062 * lb + 0.016, 0.062 * lb + 0.016),
                                           (-0.12, 0.074 * lb + 0.022, 0.074 * lb + 0.022),
                                           (-0.03, 0.07 * lb + 0.018, 0.07 * lb + 0.018)], gm, seg=12))
            add(f'hand_{side}', M.ellipsoid(0.052 * H.HAND, 0.062 * H.HAND, 0.07 * H.HAND, dk, (0, 0.008, -0.055), seg=9))
            add(f'hand_{side}', M.rounded_box(0.07, 0.03, 0.07, g2, (0, -0.045, -0.05), r=0.012, seg=6, p=4))
            # legs: front and back plates proud of the suit, big knee guards
            rt, rs = 0.095 * lb, 0.088 * lb
            add(f'thigh_{side}', M.capsule((0, 0, 0.0), (0, 0, -H.THIGH + 0.04), rt, 0.082 * lb, dk, seg=12, rings=2))
            add(f'thigh_{side}', M.rounded_box(0.21, 0.07, 0.28, gm, (0, rt + 0.01, -0.17), r=0.03, seg=8, p=3))
            add(f'thigh_{side}', M.rounded_box(0.19, 0.06, 0.25, g2, (0, -rt - 0.005, -0.17), r=0.028, seg=8, p=3))
            add(f'thigh_{side}', M.rounded_box(0.05, 0.16, 0.22, g2, (sx * (rt + 0.005), 0.0, -0.16), r=0.02, seg=8, p=3))
            add(f'shin_{side}', M.capsule((0, -0.01, 0.02), (0, -0.01, -0.06), 0.075 * lb, 0.075 * lb, dk, seg=10, rings=2))
            add(f'shin_{side}', M.rounded_box(0.19, 0.09, 0.16, gm, (0, rs + 0.03, -0.01), r=0.035, seg=8, p=3))   # knee
            add(f'shin_{side}', M.capsule((0, 0, -0.05), (0, 0, -H.SHIN + 0.12), rs, 0.078 * lb, dk, seg=12, rings=2))
            add(f'shin_{side}', M.rounded_box(0.19, 0.07, 0.24, gm, (0, rs + 0.005, -0.19), r=0.03, seg=8, p=3))   # shin
            add(f'shin_{side}', M.rounded_box(0.17, 0.06, 0.22, g2, (0, -rs - 0.005, -0.17), r=0.028, seg=8, p=3))  # calf
            add(f'foot_{side}', M.rounded_box(0.135 * H.FOOT, 0.28 * H.FOOT, 0.11, dk, (0, 0.05, -0.035), r=0.03, seg=10, p=3))
            add(f'foot_{side}', M.rounded_box(0.12 * H.FOOT, 0.10, 0.065, gm, (0, 0.165, -0.025), r=0.022, seg=8, p=3))
        add('chest', RG.power_pack(self.mats).transformed(M.translate(0, -0.25, 0.14) @ M.scale(1.25) @ M.translate(0, 0.25, -0.14)
                                                       @ M.translate(0, 0.02, 0)), 'backpack')

    def _hat(self, add0):
        def add(mesh, tag='hat'):
            add0('head', mesh.transformed(M.scale(H.HEAD)), tag)
        p = self.full
        hat = p.get('hat')
        if hat == 'covered':
            hm = self.mm('helmet', '#6a6448', spec=0.1, gloss=10)
            # the stock PASGT profile (foot_modern's 'pasgt'), in a cloth cover with a band
            rx, ry, rz, cut = 0.138, 0.15, 0.16, -0.06
            add(H._dome(rx, ry, rz, hm, (0, -0.005, 0.12), cut))
            zr = 0.12 + cut
            add(M.loft([(zr - 0.01, rx + 0.012, ry + 0.02, 0, -0.01), (zr + 0.02, rx + 0.004, ry + 0.004)], hm, seg=18,
                       cap_bottom=True, cap_top=False))
            sm = self.mm('helmet_band', '#4e4a34')
            add(M.cylinder(rx * 0.99, rx * 0.95, 0.03, sm, seg=20, z0=zr + 0.05).transformed(M.scale(1, ry / rx, 1)))
            # the cover's seams over the top
            for ang in (0, 90):
                add(M.capsule((-0.12, 0, 0.17), (0.12, 0, 0.17), 0.006, 0.006, sm, seg=4, rings=1).transformed(
                    M.translate(0, -0.005, 0.0) @ M.rot_z(ang)).transformed(M.translate(0, 0, 0.09)))
            if p.get('goggles'):
                add(RG.goggles(self.mats, self.mm('goggle_strap', '#2a2824'),
                               self.mm('goggle_lens', '#5a4a2a', spec=0.9, gloss=40), z=0.185, r=0.137))
        elif hat == 'fast':
            hm = self.mm('helmet', '#8a7a58', spec=0.15, gloss=12)
            rail = self.mm('helmet_rail', '#2e2e2c', spec=0.3, gloss=16)
            nv = self.mm('nvg', '#1c1d20', spec=0.35, gloss=20)
            add(RG.nvg_helmet(self.mats, hm, rail, nv))
            add(RG.headset(self.mats, self.mm('headset', '#3a3a34', spec=0.2, gloss=14)))
        elif hat == 'exo':
            # a closed helmet: the shell, a face plate with a T-shaped visor
            # cut, cheek pieces swept forward, a neck guard, a civ-colored
            # stripe over the crown, the visor the one light
            gm = self.mm('armor_grey', '#7c7a6e', spec=0.45, gloss=22, metal=True)
            dk = self.mm('armor_dark', '#3a3934', spec=0.35, gloss=18, metal=True)
            wr = self.mm('armor_wear', '#5e5c54', spec=0.45, gloss=22, metal=True)
            pl = self.mm('plate', '#3c4aa0', spec=0.45, gloss=22)
            glow = self.mm('glow', '#7ad8e8')
            add(H._dome(0.135, 0.15, 0.165, gm, (0, -0.01, 0.115), -0.04))
            # seams: a raised ridge from brow to nape, a ring groove round the crown
            add(M.capsule((0, 0.13, 0.20), (0, 0.06, 0.27), 0.016, 0.016, dk, seg=6, rings=1))
            add(M.capsule((0, 0.06, 0.27), (0, -0.08, 0.27), 0.016, 0.016, dk, seg=6, rings=1))
            add(M.capsule((0, -0.08, 0.27), (0, -0.15, 0.19), 0.016, 0.016, dk, seg=6, rings=1))
            add(M.cylinder(0.128, 0.128, 0.014, dk, seg=20, z0=0.19).transformed(M.translate(0, -0.01, 0) @ M.scale(1, 1.1, 1)))
            # the visor plate: a broad brow-to-cheek plate standing out over the face
            add(M.rounded_box(0.26, 0.07, 0.085, gm, (0, 0.115, 0.15), r=0.025, seg=8, p=3))
            add(M.rounded_box(0.25, 0.10, 0.10, gm, (0, -0.10, 0.05), r=0.03, seg=8, p=3)
                .transformed(M.rot_x(-12)))                                                               # neck guard
            add(M.rounded_box(0.18, 0.07, 0.15, dk, (0, 0.085, 0.085), r=0.03, seg=8, p=3))             # face plate
            add(M.rounded_box(0.19, 0.02, 0.03, glow, (0, 0.152, 0.145), r=0.008, seg=6, p=4))          # visor cut
            add(M.rounded_box(0.03, 0.02, 0.07, dk, (0, 0.12, 0.085), r=0.008, seg=6, p=4).transformed(
                M.translate(0, 0, 0)))                                                                    # the T's stem
            add(M.rounded_box(0.20, 0.03, 0.02, wr, (0, 0.118, 0.165), r=0.008, seg=6, p=4))             # brow edge
            for sx in (1, -1):
                add(M.rounded_box(0.05, 0.15, 0.14, gm, (0, 0, 0), r=0.018, seg=6, p=3).transformed(
                    M.translate(sx * 0.125, 0.06, 0.07) @ M.rot_z(sx * -20)))                            # cheek piece
                add(M.cylinder(0.022, 0.022, 0.025, dk, seg=8).transformed(
                    M.translate(sx * 0.05, 0.13, 0.03) @ M.rot_x(-90)))                                    # breather
            add(M.capsule((-0.13, -0.05, 0.15), (-0.14, -0.10, 0.30), 0.009, 0.005, dk, seg=5, rings=1))   # antenna
            add(M.cylinder(0.11, 0.13, 0.10, dk, seg=14, z0=-0.07))                                        # gorget
        else:
            super()._hat(add0)
            if p.get('goggles') and hat in ('m1', 'pasgt'):
                add(RG.goggles(self.mats, self.mm('goggle_strap', '#2a2824'),
                               self.mm('goggle_lens', '#5a4a2a', spec=0.9, gloss=40), z=0.155, r=0.132))
        if p.get('respirator'):
            add(RG.respirator(self.mats, self.mm('mask', '#2a2a28', spec=0.2, gloss=14),
                              self.mm('filter', '#5a5c4a', spec=0.25, gloss=14)))


@archetype('regular_soldier')
class RegularSoldier(LF.LeagueSoldier):
    def __init__(self, unit, params):
        super().__init__(unit, params)
        bp = dict(params)
        bp['weapon'] = params.get('weapon', params.get('gun'))
        bp['offhand'] = params.get('offhand')
        self.body = RegBody(bp)
        self.mats = self.body.mats
        self.parts = self.body.parts()
        self._props = {}
        if self.gun in RG.GUNS:
            self.gspec = RG.GUNS[self.gun]

    def prop(self, name, **kw):
        if name not in RG.GUNS:
            return super().prop(name, **kw)
        key = (name, tuple(sorted(kw.items())))
        if key not in self._props:
            k = float(self.p.get('gun_thick', 1.6))
            self._props[key] = getattr(RG, name)(self.mats, **kw).transformed(M.scale(k, k, 1.0))
        return self._props[key]

    # -- death: shot, the knees buckle, he sits back and falls on his back.
    # The legs stay on inverse kinematics with the feet on the ground all
    # the way down (no plank falling with the feet in the air); the arms go
    # slack from the unit's own standing pose (frame 0 is the default pose)
    # and the weapon, still in the right hand, turns over about five frames
    # to lie on the ground along his right side, muzzle to the feet.
    def action(self, action, ctx):
        if self.anim_name(action) != 'death' or not self.gun:
            return super().action(action, ctx)
        key = ('death_slack', action, ctx.n if ctx else None)
        if key in self._cache:
            return self._cache[key]
        B = dict(self.base)
        down = ctx.event('down', 0.55) if ctx is not None else 0.55
        down = min(max(down or 0.55, 0.35), 0.85)
        A_ = H.ANKLE_H
        fr, fl = tuple(B['foot_r']), tuple(B['foot_l'])
        slack = {'hand_r': (0.30, 0.06, 0.80), 'w_main': (172, 0, 4), 'support': 0.0,
                 'hand_l': (-0.36, 0.10, 0.92), 'elbow_r': (0.9, -0.3, -0.2), 'elbow_l': (-0.9, -0.2, -0.2)}
        mid = {'hand_r': tuple(0.5 * (_v(B['hand_r']) + _v(slack['hand_r']))),
               'w_main': tuple(_v(B['w_main'])),
               'support': 0.4 * float(B.get('support', 0.0)),
               'hand_l': (-0.34, 0.16, 1.05), 'elbow_r': (0.9, -0.3, -0.3), 'elbow_l': (-0.9, -0.3, -0.2)}
        k = [
            (0.0, {}),
            (0.14, dict({'root_rot': (8, 0, 4), 'root_pos': (0, -0.04, -0.03), 'chest': (10, 0, 6), 'head': (18, 0, 0),
                         'foot_r': fr, 'foot_l': fl})),
            (0.30, dict(mid, root_rot=(14, 0, 8), root_pos=(0, -0.10, -0.36), spine=(8, 0, 0), chest=(14, 0, 6),
                        head=(20, 0, 0), foot_r=fr, foot_l=fl)),
            (0.52, dict(slack, w_main=(-150, 0, 6), root_rot=(46, 0, 8), root_pos=(0, -0.32, -0.62), spine=(6, 0, 0), chest=(8, 0, 4),
                        head=(14, 0, 8), foot_r=(0.18, 0.06, A_), foot_l=(-0.17, 0.12, A_))),
            (0.74, dict(slack, root_rot=(86, 0, 6), root_pos=(0, -0.52, -0.75), spine=(0, 0, 0), chest=(0, 0, 2),
                        head=(-6, 0, 18), foot_r=(0.22, 0.22, A_), foot_l=(-0.18, 0.26, A_))),
            (1.0, dict(slack, root_rot=(90, 0, 6), root_pos=(0, -0.54, -0.77), spine=(0, 0, 0), chest=(0, 0, 2),
                       head=(-8, 0, 22), foot_r=(0.24, 0.24, A_), foot_l=(-0.18, 0.28, A_),
                       toe_r=-30.0, toe_l=-30.0)),
        ]
        anim = H.keys(B, k)

        def fn(t):
            p = anim(H._warp(t, [(0.52, down)]))
            p['follow'] = _v(1.0)
            p['follow_rot'] = _v(1.0)
            return p
        act = FM.Act(fn, legs='ik', arms='ik', drop_t=None)
        self._cache[key] = act
        return act

    # -- the RPG's shot: the round leaves the tube (the launcher is drawn
    # empty from the shot on) and flies as a finned rocket with a smoke
    # trail behind it, to the blast
    def frame(self, action, f, n, ctx):
        shots = self.shots(action)
        rocket = self.p.get('flash') == 'rocket' and shots and self.gun == 'rpg7'
        if not rocket or f < shots[0]:
            return super().frame(action, f, n, ctx)
        s0 = shots[0]
        act = self.action(action, ctx)
        pos, dvec = self._muzzle_at(action, s0, n, act)   # (cached while the launcher is still loaded)
        gun = self.weapon
        self.weapon = 'rpg7_empty'
        try:
            mesh = super().frame(action, f, n, ctx)
        finally:
            self.weapon = gun
        fly = int(self.p.get('flight', 4))
        land = float(self.p.get('impact', 3.2))
        if s0 <= f <= s0 + fly:
            dist = 0.15 + land * (f - s0) / fly
            tip = pos + dvec * dist
            T = M.look_along(tip - dvec, tip) if hasattr(M, 'look_along') else None
            k = float(self.p.get('gun_thick', 1.6))
            r = RG.rpg_rocket(self.mats).transformed(M.scale(k, k, 1.0))
            r = r.transformed(T @ M.translate(0, 0, 1.0))
            mesh = M.Mesh.concat([mesh, r])
            self._local = mesh
        return mesh

    def effects(self, action, f, n, act, info, ctx):
        fx = super().effects(action, f, n, act, info, ctx)
        shots = self.shots(action)
        if self.p.get('flash') != 'rocket' or not shots:
            return fx
        s0 = shots[0]
        fly = int(self.p.get('flight', 4))
        land = float(self.p.get('impact', 3.2))
        fx = [e for e in fx if e['kind'] != 'round' and not (e['kind'] == 'glow' and s0 < f <= s0 + fly
                                                              and e.get('rad', 1) < 0.1)]
        pos, dvec = self._muzzle_at(action, s0, n, act)
        if f > s0:
            # the trail: puffs from the muzzle to the rocket's tail, older ones thinner
            head = min(f - s0, fly) / fly * land + 0.15
            m = 12
            for i in range(m):
                s = head - 0.45 - i * (head / m)
                if s < 0.1:
                    continue
                age = (f - s0) - (fly * s / land)
                a = 0.5 * max(0.0, 1 - max(0.0, age) / 7.0)
                if a <= 0.03:
                    continue
                fx.append({'kind': 'smoke', 'pos': pos + dvec * s + _v((0, 0, 0.02 * i)), 'rad': 0.09 + 0.01 * i,
                           'alpha': a, 'seed': 11 + i})
        return fx

    def _solve(self, pose, act):
        return super()._solve(pose, act)

    # -- the attack eases out of the standing pose: over the first
    # `fire_blend` frames the firing pose is blended in from the default one
    def anim_fire(self, action, n):
        act = super().anim_fire(action, n)
        k = int(self.p.get('fire_blend', 0))
        if k <= 0:
            return act
        B = self.base
        tb = k / max(n - 1, 1)
        inner = act.fn

        def fn(t):
            p = inner(t)
            if t >= tb:
                return p
            w = t / tb
            w = w * w * (3 - 2 * w)
            return {c: (_v(B[c]) * (1 - w) + _v(v) * w) if c in B else v for c, v in p.items()}
        return FM.Act(fn, loop=act.loop, legs=act.legs, arms=act.arms, hide=act.hide)
