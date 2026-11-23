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
            if bone.startswith(('arm_', 'forearm_')) and sleeve is not None:
                new = sleeve
            elif bone.startswith(('thigh_', 'shin_')) and legs is not None:
                new = legs
            if new is not None and len(mesh) and np.all(mesh.M == skin):
                mesh = mesh.with_material(new)
            res.append((bone, mesh, tag))
        return res

    # -- the power armour
    def _exo(self, add, rings, tw, lb):
        pl = self.mm('plate', '#3c4aa0', spec=0.55, gloss=26)
        gr = self.mm('armor_grey', '#8a8e96', spec=0.55, gloss=26, metal=True)
        dk = self.mm('armor_dark', '#3a3d45', spec=0.45, gloss=22, metal=True)
        glow = self.mm('glow', '#60e4ff')
        g = 0.05
        # the chest plate: over the chest rings, a seam down the middle, a step under the pecs
        # the suit's yoke over the shoulders (dark), the chest plate a shell
        # over the front (civ color), the back plate (grey) behind
        cr = [(z, rx + g, ry + g * 1.25, dx, dy) for z, rx, ry, dx, dy in rings[:4]]
        add('chest', M.loft([(z, rx + 0.025, ry + 0.03, dx, dy) for z, rx, ry, dx, dy in rings[1:5]], dk, seg=18,
                            cap_bottom=False, cap_top=True))
        front = [(-0.03, rings[0][1] + g * 0.6, rings[0][2] + g, 0, 0)] + cr[1:] + [(0.29, rings[4][1] + 0.07, rings[4][2] + 0.06, 0, 0)]
        add('chest', H._shell(front, pl, math.radians(12), math.radians(168), seg=16))
        add('chest', H._shell([(z, rx + 0.006, ry + 0.006, dx, dy) for z, rx, ry, dx, dy in front], dk,
                              math.radians(8), math.radians(12), seg=1))
        add('chest', H._shell([(z, rx + 0.006, ry + 0.006, dx, dy) for z, rx, ry, dx, dy in front], dk,
                              math.radians(168), math.radians(172), seg=1))
        back = [(-0.02, rings[0][1] + g * 0.6, rings[0][2] + g, 0, 0)] + cr[1:3] + [(0.27, rings[3][1] + 0.03, rings[3][2] + 0.05, 0, 0)]
        add('chest', H._shell(back, gr, math.radians(195), math.radians(345), seg=14))
        add('chest', M.loft([(0.26, 0.15, 0.12), (0.36, 0.115, 0.10)], dk, seg=16,
                            cap_bottom=False, cap_top=True))                                             # collar
        yf = rings[1][2] + rings[1][4] + g * 1.25
        add('chest', M.box(0.018, 0.03, 0.26, dk, (0, yf + 0.004, 0.11)))                               # seam
        add('chest', M.box(rings[1][1] * 1.4, 0.02, 0.016, dk, (0, yf - 0.004, 0.06)))
        for sx in (1, -1):
            add('chest', M.box(0.05, 0.012, 0.016, glow, (sx * 0.09, yf + 0.004, 0.18)))                # chest lights
        # the abdomen: three grey bands with dark gaps
        for i, z in enumerate((-0.04, 0.04, 0.12)):
            add('spine', M.cylinder(0.165 * tw + 0.04, 0.17 * tw + 0.04, 0.065, gr if i != 1 else dk, seg=16, z0=z)
                .transformed(M.scale(1, 0.78, 1)))
        # the belt and tassets
        add('pelvis', M.loft([(-0.04, 0.19 * tw + 0.035, 0.14 * tw + 0.035), (0.10, 0.19 * tw + 0.03, 0.14 * tw + 0.03)],
                             dk, seg=16, cap_bottom=True, cap_top=False))
        for ang in (-50, -20, 20, 50, 180):
            a = math.radians(ang + 90)
            x, y = 0.19 * tw * math.cos(a) * 1.08, 0.14 * tw * math.sin(a) * 1.15
            add('pelvis', M.rounded_box(0.12, 0.025, 0.14, gr, (0, 0, -0.07), r=0.01, seg=6, p=4)
                .transformed(M.translate(x, y, 0.0) @ M.rot_z(ang) @ M.rot_x(-8)))
        for side, sx in (('r', 1), ('l', -1)):
            # pauldrons: a broad curved plate over the shoulder in the civ
            # color, a grey lame below it, angled out; a dark edge between
            T1 = M.translate(sx * 0.04, 0.0, 0.07) @ M.rot_y(sx * 32)
            add(f'arm_{side}', H._dome(0.16, 0.165, 0.075, pl, (0, 0, -0.03), -0.025).transformed(T1))
            add(f'arm_{side}', M.loft([(-0.07, 0.162, 0.167), (-0.05, 0.16, 0.165)], dk, seg=16,
                                      cap_bottom=True, cap_top=False).transformed(T1))
            T2 = M.translate(sx * 0.075, 0.0, -0.05) @ M.rot_y(sx * 40)
            add(f'arm_{side}', H._dome(0.13, 0.145, 0.06, gr, (0, 0, -0.03), -0.015).transformed(T2))
            add(f'arm_{side}', M.box(0.012, 0.10, 0.035, glow, (0, 0, 0.0)).transformed(
                T1 @ M.translate(sx * 0.11, 0.0, 0.0)))
            add(f'arm_{side}', M.capsule((0, 0, -0.08), (0, 0, -0.24), 0.074 * lb + 0.012, 0.066 * lb + 0.012, gr,
                                         seg=12, rings=2))
            add(f'forearm_{side}', M.sphere(0.06 * lb + 0.012, dk, (0, -0.01, 0.0), seg=10))           # elbow
            add(f'forearm_{side}', M.loft([(-0.22, 0.056 * lb + 0.012, 0.056 * lb + 0.012),
                                           (-0.12, 0.066 * lb + 0.016, 0.066 * lb + 0.016),
                                           (-0.04, 0.064 * lb + 0.012, 0.064 * lb + 0.012)], gr, seg=12))
            add(f'forearm_{side}', M.box(0.03, 0.012, 0.05, glow, (sx * -0.04, 0.0, -0.14)).transformed(
                M.translate(0, 0, 0)))
            add(f'hand_{side}', M.ellipsoid(0.05 * H.HAND, 0.06 * H.HAND, 0.068 * H.HAND, dk, (0, 0.008, -0.055), seg=9))
            # legs: thigh plate, knee cop, shin plate, boot armour, pistons on the outside
            add(f'thigh_{side}', M.capsule((0, 0, 0.0), (0, 0, -H.THIGH + 0.04), 0.1 * lb, 0.08 * lb, dk, seg=12, rings=2))
            add(f'thigh_{side}', M.rounded_box(0.20, 0.09, 0.28, gr, (0, 0.085, -0.17), r=0.035, seg=8, p=3))
            add(f'thigh_{side}', M.rounded_box(0.06, 0.16, 0.22, gr, (sx * 0.135, 0.0, -0.15), r=0.025, seg=8, p=3))
            add(f'thigh_{side}', M.box(0.21, 0.02, 0.014, dk, (0, 0.13, -0.10)))
            add(f'shin_{side}', M.sphere(0.072 * lb + 0.02, dk, (0, 0.0, 0.0), seg=12))
            add(f'shin_{side}', M.rounded_box(0.15, 0.08, 0.13, gr, (0, 0.095, -0.01), r=0.03, seg=8, p=3))
            add(f'shin_{side}', M.capsule((0, 0, -0.05), (0, 0, -H.SHIN + 0.12), 0.08 * lb, 0.07 * lb, dk, seg=12, rings=2))
            add(f'shin_{side}', M.rounded_box(0.17, 0.09, 0.24, gr, (0, 0.065, -0.19), r=0.035, seg=8, p=3))
            add(f'shin_{side}', M.box(0.04, 0.012, 0.02, glow, (sx * 0.04, 0.11, -0.12)))
            add(f'foot_{side}', M.rounded_box(0.14 * H.FOOT, 0.29 * H.FOOT, 0.12, dk, (0, 0.05, -0.03), r=0.035, seg=10, p=3))
            add(f'foot_{side}', M.rounded_box(0.12 * H.FOOT, 0.10, 0.07, gr, (0, 0.17, -0.02), r=0.025, seg=8, p=3))
            ox = sx * (0.1 * lb + 0.02)
            add(f'thigh_{side}', M.cylinder(0.03, 0.03, 0.05, dk, seg=10).transformed(M.translate(ox, 0, -0.03) @ M.rot_y(90)))
            add(f'thigh_{side}', M.cylinder(0.024, 0.024, 0.20, dk, seg=8, z0=-0.22).transformed(M.translate(ox, -0.01, 0)))
            add(f'thigh_{side}', M.cylinder(0.014, 0.014, 0.18, self.mm('piston', '#70747c', spec=0.7, gloss=30, metal=True), seg=8, z0=-0.38).transformed(M.translate(ox, -0.01, 0)))
            ox2 = sx * (0.08 * lb + 0.02)
            add(f'shin_{side}', M.cylinder(0.028, 0.028, 0.05, dk, seg=10).transformed(M.translate(ox2, 0, 0.0) @ M.rot_y(90)))
            add(f'shin_{side}', M.cylinder(0.022, 0.022, 0.18, dk, seg=8, z0=-0.20).transformed(M.translate(ox2, -0.01, 0)))
            add(f'shin_{side}', M.cylinder(0.013, 0.013, 0.16, self.mm('piston', '#70747c', spec=0.7, gloss=30, metal=True), seg=8, z0=-0.34).transformed(M.translate(ox2, -0.01, 0)))
        add('chest', RG.power_pack(self.mats), 'backpack')

    def _hat(self, add0):
        def add(mesh, tag='hat'):
            add0('head', mesh.transformed(M.scale(H.HEAD)), tag)
        p = self.full
        hat = p.get('hat')
        if hat == 'covered':
            hm = self.mm('helmet', '#6a6448', spec=0.1, gloss=10)
            add(H._dome(0.137, 0.15, 0.158, hm, (0, -0.005, 0.12), -0.035))
            add(M.loft([(0.08, 0.152, 0.166), (0.096, 0.142, 0.156)], hm, seg=20, cap_bottom=True, cap_top=False))
            sm = self.mm('helmet_band', '#4e4a34')
            add(M.cylinder(0.14, 0.136, 0.028, sm, seg=20, z0=0.15).transformed(M.scale(1, 1.08, 1)))
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
            gr = self.mm('armor_grey', '#8a8e96', spec=0.55, gloss=26, metal=True)
            dk = self.mm('armor_dark', '#3a3d45', spec=0.45, gloss=22, metal=True)
            pl = self.mm('plate', '#3c4aa0', spec=0.55, gloss=26)
            glow = self.mm('glow', '#60e4ff')
            add(M.ellipsoid(0.13, 0.145, 0.155, gr, (0, -0.005, 0.13), seg=18))
            add(M.rounded_box(0.045, 0.27, 0.05, pl, (0, -0.02, 0.27), r=0.018, seg=8, p=3))                    # crest fin
            add(M.rounded_box(0.24, 0.05, 0.035, dk, (0, 0.11, 0.18), r=0.014, seg=8, p=3))                 # brow ridge
            add(M.rounded_box(0.20, 0.06, 0.04, dk, (0, 0.105, 0.135), r=0.015, seg=8, p=3))               # visor frame
            add(M.rounded_box(0.17, 0.05, 0.028, glow, (0, 0.118, 0.137), r=0.012, seg=8, p=3))            # visor
            add(M.rounded_box(0.03, 0.05, 0.07, glow, (0, 0.118, 0.10), r=0.01, seg=6, p=3))               # its stem
            add(M.rounded_box(0.14, 0.08, 0.08, dk, (0, 0.10, 0.035), r=0.03, seg=8, p=3))                 # jaw
            for sx in (1, -1):
                add(M.cylinder(0.028, 0.028, 0.03, gr, seg=10).transformed(
                    M.translate(sx * 0.06, 0.13, 0.03) @ M.rot_x(-90) @ M.rot_y(sx * 30)))                 # filters
                add(M.cylinder(0.045, 0.045, 0.04, dk, seg=12).transformed(
                    M.translate(sx * 0.125, -0.01, 0.12) @ M.rot_y(90)))                                    # ear pods
            add(M.capsule((-0.13, -0.04, 0.16), (-0.15, -0.10, 0.36), 0.01, 0.005, dk, seg=5, rings=1))    # antenna
            add(M.cylinder(0.11, 0.13, 0.10, dk, seg=14, z0=-0.07))                                        # gorget
        else:
            super()._hat(add0)
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
