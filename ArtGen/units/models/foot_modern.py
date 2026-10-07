"""Gunpowder and modern foot units (catalog/foot_modern.py): musketmen,
riflemen, infantry, marines, paratroopers, the TOW team, and modern settlers
and workers.

Archetypes:
    'soldier'   a Humanoid (models/humanoid.py) with uniforms (trousers,
                boots, coats, vests, webbing, camouflage), period headgear,
                firearms held in both hands, and firearm animations with
                muzzle flashes and powder smoke.
    'tow_team'  two soldiers with a TOW launcher on a tripod.

Catalog parameters on top of the humanoid ones (see humanoid's docstring):
    hat         'morion' | 'cavalier' | 'kepi' | 'm1' | 'pasgt' | 'para' |
                'bandana' | 'boonie' | None   (materials 'hat', 'plume',
                'hatband', 'visor', 'helmet_net')
    plume, hatband, net   bool
    legs        'trousers' | 'breeches' (with 'stockings')   (material 'trousers')
    feet        'shoes' | 'boots' | 'tall_boots'   (material 'boots'); gaiters: bool
    coat        skirt length of a coat below the belt (m) or None (material 'coat')
    vest        'jerkin' | 'flak' | 'bib' | None   (material 'vest' / 'trousers' for bib;
                bib_back: bool adds the overalls' back panel)
    straps      list of 'bandolier' | 'crossbelt' | 'webbing' | 'harness' | 'braces'
    kneebands   bool (material 'kneeband')
    camo        {material: [patch colors]}: camouflage patches over that garment
    pack        'field' | 'para' | 'hiker' | None
    gun         a props_foot_modern.GUNS name; offhand may be 'musket_rest'
    hold        'port' | 'shoulder' | 'vertical' | 'hip' | 'low' | 'across' (the carry pose)
    aim         'shoulder' | 'hip' | 'rest' (Musketman: on the forked rest)
    shots       {INI action: [frames with a muzzle flash]} (from the original)
    flash       'burst' | 'musket'; smoke: 'heavy' | 'light' | None
    fortify     'aim' | 'guard' | 'kneel'      victory: 'raise' | 'hat' | 'fire_air' | 'fist'
    poses       {name: {channel: value}} overrides of the named poses
"""
import math

import numpy as np

import mesh as M
import render as R
import rig
from models import archetype
from models import humanoid as H
from models import props as P
from models import props_foot_modern as G
from models.foot_ancient import finish_colors

A = H.ANKLE_H


def _v(x):
    return np.asarray(x, np.float32)


def _orthonormal(Rm):
    u, _, vt = np.linalg.svd(Rm)
    out = u @ vt
    if np.linalg.det(out) < 0:
        u[:, -1] *= -1
        out = u @ vt
    return out.astype(np.float32)


# ------------------------------------------------------------ the body

class SoldierBody(H.Body):
    """The humanoid body plus uniforms, headgear and packs."""

    def __init__(self, p):
        bp = dict(p)
        bp['helmet'] = None
        bp.setdefault('bottom', 'none')
        bp['back'] = None
        bp['wraps'] = None
        super().__init__(bp)
        self.full = p

    def mm(self, name, default, **kw):
        if name in self.mats:
            return self.mats[name]
        col = self.full.get('colors', {}).get(name, default)
        t = name in self.tint
        return self.mats.add(name, '#ffffff' if t else col, tint=t, **kw)

    def parts(self):
        out = super().parts()
        p = self.full
        bulk = H.build_params(p).get('bulk', 1.0)
        tw = H.TORSO_W * bulk
        lb = H.LIMB * bulk
        rng = np.random.default_rng(abs(hash(p.get('_seed', 'soldier'))) % (2 ** 32))
        rng = np.random.default_rng(7)

        def add(bone, mesh, tag=None):
            out.append((bone, mesh, tag))

        def addt(bone, mesh, tag=None):
            add(bone, mesh.transformed(M.scale(tw, 1 + (tw - 1) * 0.8, 1)), tag)

        chest_rings = [(0.0, 0.165, 0.105, 0, 0.0), (0.10, 0.19, 0.12, 0, 0.012), (0.19, 0.205, 0.122, 0, 0.008),
                       (0.25, 0.185, 0.108, 0, -0.005), (0.30, 0.10, 0.07, 0, -0.01), (0.33, 0.05, 0.05, 0, -0.01)]
        chest_rings = [(z, rx * tw, ry * (1 + (tw - 1) * 0.6), dx, dy) for z, rx, ry, dx, dy in chest_rings]
        camo = p.get('camo', {})

        cdens = float(p.get('camo_density', 2.0))
        csize = float(p.get('camo_size', 0.034))

        def patches(bone, z0, z1, rx, ry, garment, n, size=None, dy=0.0, rx1=None, ry1=None):
            """Camouflage blotches on a roughly elliptic-cylindrical garment."""
            if garment not in camo:
                return
            cols = camo[garment]
            mids = [self.mm(f'camo_{garment}_{i}', c, spec=0.04) for i, c in enumerate(cols)]
            rx1 = rx if rx1 is None else rx1
            ry1 = ry if ry1 is None else ry1
            size = csize if size is None else size * csize / 0.05
            for i in range(int(n * cdens)):
                a = rng.uniform(0, 2 * math.pi)
                u = rng.uniform(0, 1)
                z = z0 + (z1 - z0) * u
                ex, ey = rx + (rx1 - rx) * u, ry + (ry1 - ry) * u
                x, y = ex * math.cos(a), ey * math.sin(a) + dy
                nrm = math.degrees(math.atan2(math.sin(a) / ey, math.cos(a) / ex))
                s = size * rng.uniform(0.7, 1.3)
                blob = M.ellipsoid(0.006, s * rng.uniform(0.9, 1.8), s * rng.uniform(0.5, 1.0),
                                   mids[i % len(mids)], seg=7)
                add(bone, blob.transformed(M.translate(x, y, z) @ M.rot_z(nrm) @ M.rot_x(rng.uniform(-40, 40))))

        # -- legs
        legs = p.get('legs')
        tm = None
        if legs in ('trousers', 'breeches'):
            tm = self.mm('trousers', '#6a6a5a')
            addt('pelvis', M.loft([(-0.17, 0.182, 0.136), (0.0, 0.186, 0.133), (0.105, 0.173, 0.125)], tm, seg=16,
                                  cap_bottom=True, cap_top=False))
            patches('pelvis', -0.15, 0.08, 0.186 * tw, 0.135 * tw, 'trousers', 14)
            for side in 'rl':
                if legs == 'trousers':
                    add(f'thigh_{side}', M.capsule((0, 0, 0.03), (0, 0, -H.THIGH), 0.097 * lb, 0.074 * lb, tm, seg=12, rings=2))
                    add(f'shin_{side}', M.capsule((0, 0, 0.0), (0, 0, -H.SHIN + 0.05), 0.074 * lb, 0.062 * lb, tm, seg=12, rings=2))
                    add(f'shin_{side}', M.ellipsoid(0.066 * lb, 0.066 * lb, 0.13, tm, (0, -0.01, -0.14), seg=10))
                    patches(f'thigh_{side}', -0.02, -0.36, 0.097 * lb, 0.097 * lb, 'trousers', 9, rx1=0.078 * lb, ry1=0.078 * lb)
                    patches(f'shin_{side}', -0.04, -0.30, 0.074 * lb, 0.074 * lb, 'trousers', 7, rx1=0.066 * lb, ry1=0.066 * lb)
                    if p.get('cargo'):
                        sx = 1 if side == 'r' else -1
                        add(f'thigh_{side}', M.rounded_box(0.04, 0.11, 0.12, tm, (sx * 0.095 * lb, 0.0, -0.2), r=0.015, seg=6, p=4))
                else:
                    sm = self.mm('stockings', '#9a948a')
                    add(f'thigh_{side}', M.capsule((0, 0, 0.03), (0, 0, -H.THIGH + 0.02), 0.104 * lb, 0.088 * lb, tm, seg=12, rings=2))
                    add(f'shin_{side}', M.capsule((0, 0, 0.02), (0, 0, -0.08), 0.084 * lb, 0.074 * lb, tm, seg=12, rings=2))
                    add(f'shin_{side}', M.capsule((0, 0, -0.04), (0, 0, -H.SHIN + 0.05), 0.066 * lb, 0.052 * lb, sm, seg=12, rings=2))
                    add(f'shin_{side}', M.ellipsoid(0.062 * lb, 0.062 * lb, 0.12, sm, (0, -0.01, -0.15), seg=10))
                    if p.get('garters', True):
                        add(f'shin_{side}', M.cylinder(0.08 * lb, 0.08 * lb, 0.03, self.mm('garter', '#3a3a3a'), seg=12, z0=-0.1))
        if p.get('kneebands'):
            km = self.mm('kneeband', '#ffffff')
            for side in 'rl':
                add(f'shin_{side}', M.cylinder(0.078 * lb, 0.076 * lb, 0.06, km, seg=12, z0=-0.09))
        # -- feet
        feet = p.get('feet', 'boots')
        bm = self.mm('boots', '#2a2420')
        for side in 'rl':
            add(f'foot_{side}', M.rounded_box(0.11 * H.FOOT, 0.25 * H.FOOT, 0.085, bm, (0, 0.058, -0.04), r=0.032, seg=12, p=3))
            top = {'shoes': -0.37, 'boots': -0.24, 'tall_boots': -0.04}[feet]
            rings = [(-0.42, 0.064 * lb, 0.07 * lb), (top, 0.072 * lb, 0.074 * lb)]
            if feet == 'tall_boots':
                rings = [(-0.42, 0.064 * lb, 0.07 * lb), (-0.2, 0.074 * lb, 0.076 * lb), (-0.02, 0.092 * lb, 0.094 * lb)]
            add(f'shin_{side}', M.loft(rings, bm, seg=12, cap_bottom=False, cap_top=True))
            if p.get('gaiters'):
                gm = self.mm('gaiters', '#c8c4b4')
                add(f'shin_{side}', M.loft([(-0.40, 0.07 * lb, 0.074 * lb), (-0.18, 0.076 * lb, 0.078 * lb)], gm, seg=12,
                                           cap_bottom=False, cap_top=True))

        # -- coat skirt, vests
        coat = p.get('coat')
        if coat:
            cm = self.mm('coat', '#6a2a2a')
            low = -float(coat)
            fl = 0.03 + 0.08 * float(coat)
            addt('pelvis', M.loft([(low, 0.19 + fl, 0.15 + fl, 0, 0.0), (low * 0.5, 0.19 + fl * 0.5, 0.145 + fl * 0.5),
                                   (0.0, 0.186, 0.134), (0.12, 0.176, 0.127)], cm, seg=18, cap_bottom=False, cap_top=False))
            patches('pelvis', low, 0.05, 0.2 * tw, 0.16 * tw, 'coat', 10)
        top = p.get('top')
        if top in ('long_sleeved', 'sleeved', 'tunic'):
            ch = chest_rings
            patches('chest', 0.0, 0.26, ch[2][1] + 0.016, ch[2][2] + 0.016, 'top', 18)
            patches('spine', -0.02, 0.2, 0.17 * tw, 0.125 * tw, 'top', 8)
            for side in 'rl':
                patches(f'arm_{side}', -0.02, -0.16, 0.078 * lb, 0.078 * lb, 'top', 5)
                if top == 'long_sleeved':
                    patches(f'forearm_{side}', -0.02, -0.18, 0.064 * lb, 0.064 * lb, 'top', 4)
        vest = p.get('vest')
        if vest in ('jerkin', 'flak'):
            vm = self.mm('vest', '#6a5a4a')
            g = 0.032 if vest == 'jerkin' else 0.045
            rings = [(z, rx + g, ry + g * 1.2, dx, dy) for z, rx, ry, dx, dy in chest_rings[:4]]
            rings = [(-0.06, chest_rings[0][1] + g, chest_rings[0][2] + g, 0, 0)] + rings
            add('chest', M.loft(rings, vm, seg=16, cap_bottom=False, cap_top=True))
            add('spine', M.loft([(0.0, 0.165 * tw + g, 0.118 * tw + g), (0.22, 0.18 * tw + g, 0.126 * tw + g)], vm,
                                seg=14, cap_bottom=False, cap_top=False))
            patches('chest', -0.04, 0.24, chest_rings[2][1] + g, chest_rings[2][2] + g * 1.2, 'vest', 22, size=0.055)
            if vest == 'jerkin':
                low = -float(p.get('vest_skirt', 0.08))
                fl = 0.02 + 0.08 * (-low)
                addt('pelvis', M.loft([(low, 0.205 + fl, 0.16 + fl), (0.12, 0.185, 0.135)], vm, seg=16, cap_bottom=False,
                                      cap_top=False))
            else:
                add('chest', M.loft([(0.27, 0.13 * tw, 0.095), (0.33, 0.085 * tw, 0.075)], vm, seg=14, cap_bottom=False,
                                    cap_top=False))
        elif vest == 'bib':
            vm = self.mm('trousers', '#6a5a40')
            ry = chest_rings[1][2] + 0.02
            add('chest', M.rounded_box(0.2 * tw, 0.03, 0.24, vm, (0, ry, 0.08), r=0.012, seg=6, p=4))
            add('chest', M.rounded_box(0.07, 0.03, 0.06, vm, (0.03, ry + 0.02, 0.12), r=0.01, seg=6, p=4))  # pocket
            if p.get('bib_back'):
                # overalls: the back panel too, up to the shoulder blades
                add('chest', M.rounded_box(0.21 * tw, 0.03, 0.24, vm, (0, -ry, 0.08),
                                           r=0.012, seg=6, p=4))
        straps = p.get('straps', [])
        sm = self.mm('strap', '#4a3a2a')
        r1 = chest_rings[1]
        r2 = chest_rings[2]

        def band(pts, rr=0.024, mat=sm, bone='chest'):
            for a, b in zip(pts[:-1], pts[1:]):
                add(bone, M.capsule(a, b, rr, rr, mat, seg=6, rings=1))

        def diag(sx, front=True, extra=0.0):
            s = 1 if front else -1
            g = 0.03 + extra
            return [(sx * r1[1] * 0.85, s * (r1[2] * 0.6 + g), -0.04), (sx * r1[1] * 0.3, s * (r1[2] + g), 0.08),
                    (-sx * r2[1] * 0.25, s * (r2[2] + g), 0.2), (-sx * r2[1] * 0.62, s * (r2[2] * 0.62 + g), 0.31),
                    (-sx * r2[1] * 0.72, 0.0, 0.345)]
        for st in straps:
            if st in ('bandolier', 'crossbelt'):
                # over the right shoulder to the left hip
                band(diag(-1, True, 0.02 if st == 'bandolier' else 0.0))
                band(diag(-1, False, 0.02 if st == 'bandolier' else 0.0))
                if st == 'bandolier':
                    bt = self.mm('bottles', '#d8d0b8')
                    for (x, y, z) in diag(-1, True, 0.05)[:4]:
                        add('chest', M.cylinder(0.022, 0.022, 0.07, bt, seg=7, z0=z - 0.07).transformed(M.translate(x, y, 0)))
            elif st == 'crossbelt_l':
                band(diag(1, True))
                band(diag(1, False))
            elif st in ('webbing', 'harness', 'braces'):
                wm = self.mm('strap', '#4a3a2a')
                for sx in (1, -1):
                    x = sx * r1[1] * 0.45
                    band([(x, r1[2] + 0.03, -0.04), (x * 0.95, r2[2] + 0.035, 0.18), (x * 0.9, r2[2] * 0.6 + 0.02, 0.31),
                          (x * 0.9, 0.0, 0.345), (x * 0.9, -r2[2] * 0.6 - 0.02, 0.31), (x * 0.95, -r2[2] - 0.03, 0.16),
                          (x, -r1[2] - 0.03, -0.04)], 0.022, wm)
                if st == 'harness':
                    band([(-r1[1] * 0.5, r1[2] + 0.035, 0.05), (r1[1] * 0.5, r1[2] + 0.035, 0.05)], 0.02, wm)
                if st == 'webbing':
                    pm = self.mm('pouch', '#6a6450')
                    for sx in (1, -1):
                        addt('pelvis', M.rounded_box(0.08, 0.05, 0.08, pm, (sx * 0.1, 0.15, 0.06), r=0.012, seg=6, p=4))
        if p.get('flag_patch'):
            for side, sx in (('l', -1),):
                fm = self.mm('flag_white', '#e8e4dc')
                fr = self.mm('flag_red', '#b02a2a')
                add(f'arm_{side}', M.rounded_box(0.012, 0.075, 0.06, fm, (sx * 0.085, 0.0, -0.07), r=0.004, seg=4))
                for k in range(3):
                    add(f'arm_{side}', M.box(0.014, 0.077, 0.011, fr, (sx * 0.086, 0.0, -0.088 + k * 0.02)))
        # -- packs
        pk = p.get('pack')
        if pk == 'field':
            add('chest', G.field_pack(self.mats), 'backpack')
        elif pk == 'para':
            add('chest', G.para_pack(self.mats), 'backpack')
        elif pk == 'hiker':
            add('chest', G.hiker_pack(self.mats), 'backpack')
        # -- headgear
        self._hat(add)
        return out

    def _hat(self, add0):
        def add(mesh, tag='hat'):
            add0('head', mesh.transformed(M.scale(H.HEAD)), tag)
        p = self.full
        hat = p.get('hat')
        if hat is None:
            return
        if hat == 'morion':
            hm = self.mm('helmet', '#a0a0a6', spec=0.7, gloss=26, metal=True)
            add(H._dome(0.118, 0.13, 0.17, hm, (0, 0.0, 0.125), -0.01))
            # the boat-shaped brim, rising to points in front and behind
            ang = np.linspace(0, 2 * math.pi, 33)[:-1]
            rings = []
            for k, (r_add, dz) in enumerate(((0.0, 0.0), (0.05, 0.012))):
                pts = []
                for a in ang:
                    rx = 0.118 + r_add * 0.55
                    ry = 0.13 + r_add * 1.25
                    rise = 0.075 * abs(math.sin(a)) ** 3 * (k == 1)
                    pts.append((rx * math.cos(a), ry * math.sin(a), 0.115 + dz + rise))
                rings.append(pts)
            v = np.array(rings, np.float32).reshape(-1, 3)
            v2 = v.copy()
            v2[:, 2] -= 0.014
            faces = M.grid_faces(1, len(ang), closed_u=True)
            add(M.from_indexed(v, faces, hm))
            add(M.from_indexed(v2, faces, hm))
            add(M.extrude([(-0.12, 0.0), (0.12, 0.0), (0.07, 0.085), (-0.06, 0.09)], 0.02, hm)
                .transformed(M.translate(0, 0, 0.26) @ M.rot_z(90)))
            if p.get('plume', True):
                pm = self.mm('plume', '#ffffff')
                pts = [(0.0, -0.04, 0.29), (0.0, -0.08, 0.40), (0.02, -0.15, 0.46), (0.03, -0.22, 0.45)]
                for i, (a, b) in enumerate(zip(pts[:-1], pts[1:])):
                    add(M.capsule(a, b, 0.045 - 0.01 * i, 0.04 - 0.01 * i, pm, seg=8, rings=1))
                add(M.ellipsoid(0.04, 0.05, 0.045, pm, (0.015, -0.12, 0.44), seg=8))
        elif hat == 'cavalier':
            hm = self.mm('hat', '#5a3a30')
            add(M.loft([(0.13, 0.11, 0.12), (0.22, 0.105, 0.115), (0.27, 0.085, 0.095), (0.29, 0.04, 0.05)], hm, seg=16,
                       cap_bottom=False, cap_top=True))
            brim = M.loft([(0.0, 0.25, 0.25), (0.022, 0.25, 0.25)], hm, seg=24)
            # the brim turned up on the left side
            add(brim.transformed(M.translate(0, 0, 0.14) @ M.rot_y(-14)))
            if p.get('plume', True):
                pm = self.mm('plume', '#ffffff')
                pts = [(-0.08, 0.06, 0.24), (-0.16, -0.02, 0.33), (-0.2, -0.13, 0.33), (-0.19, -0.22, 0.27)]
                for i, (a, b) in enumerate(zip(pts[:-1], pts[1:])):
                    add(M.capsule(a, b, 0.05 - 0.01 * i, 0.045 - 0.01 * i, pm, seg=8, rings=1))
        elif hat == 'kepi':
            hm = self.mm('hat', '#ffffff')
            add(M.loft([(0.10, 0.117, 0.127, 0, -0.004), (0.17, 0.11, 0.12, 0, 0.01), (0.22, 0.095, 0.104, 0, 0.03),
                        (0.245, 0.085, 0.09, 0, 0.045)], hm, seg=16, cap_bottom=False, cap_top=True)
                .transformed(M.translate(0, 0, 0.245) @ M.rot_x(-10) @ M.translate(0, 0, -0.245)))
            vm = self.mm('visor', '#1e1c1a', spec=0.4, gloss=20)
            add(M.ellipsoid(0.105, 0.075, 0.013, vm, (0, 0.13, 0.105), seg=12).transformed(
                M.translate(0, 0.13, 0.105) @ M.rot_x(14) @ M.translate(0, -0.13, -0.105)))
            add(M.cylinder(0.12, 0.12, 0.02, vm, seg=16, z0=0.09).transformed(M.scale(1, 1.06, 1)))
        elif hat in ('m1', 'pasgt', 'para'):
            hm = self.mm('helmet', '#6a6a5a', spec=0.25, gloss=14)
            rx, ry, rz = {'m1': (0.135, 0.148, 0.155), 'pasgt': (0.138, 0.15, 0.16), 'para': (0.13, 0.142, 0.155)}[hat]
            cut = {'m1': -0.03, 'pasgt': -0.06, 'para': -0.02}[hat]
            add(H._dome(rx, ry, rz, hm, (0, -0.005, 0.12), cut))
            zr = 0.12 + cut
            if hat == 'm1':
                add(M.loft([(zr - 0.005, rx + 0.02, ry + 0.02), (zr + 0.01, rx + 0.005, ry + 0.005)], hm, seg=18,
                           cap_bottom=True, cap_top=False).transformed(M.translate(0, -0.005, 0)))
            elif hat == 'pasgt':
                add(M.loft([(zr - 0.01, rx + 0.012, ry + 0.02, 0, -0.01), (zr + 0.02, rx + 0.004, ry + 0.004)], hm, seg=18,
                           cap_bottom=True, cap_top=False))
            else:
                for sx in (1, -1):
                    add(M.ellipsoid(0.03, 0.06, 0.07, hm, (sx * 0.115, 0.0, 0.07), seg=8))
                sm = self.mm('strap', '#3a3a30')
                add(M.capsule((0.1, 0.03, 0.04), (0.0, 0.08, -0.02), 0.012, 0.012, sm, seg=4, rings=1))
                add(M.capsule((-0.1, 0.03, 0.04), (0.0, 0.08, -0.02), 0.012, 0.012, sm, seg=4, rings=1))
            if p.get('hatband'):
                bm = self.mm('hatband', '#ffffff')
                add(M.cylinder(rx * 1.0, rx * 0.95, 0.05, bm, seg=18, z0=zr + 0.04).transformed(M.scale(1, ry / rx, 1)))
            if p.get('net'):
                nm = self.mm('helmet_net', '#8a8a5a')
                r2 = np.random.default_rng(3)
                for _ in range(26):
                    a = r2.uniform(0, 2 * math.pi)
                    e = r2.uniform(0.05, 1.2)
                    x, y, z = rx * math.cos(e) * math.cos(a), ry * math.cos(e) * math.sin(a), 0.12 + rz * math.sin(e)
                    add(M.ellipsoid(0.03, 0.03, 0.03, nm, (x, y - 0.005, z), seg=5))
        elif hat == 'bandana':
            hm = self.mm('hat', '#ffffff')
            add(M.loft([(0.1, 0.118, 0.13), (0.17, 0.118, 0.13), (0.2, 0.112, 0.124)], hm, seg=16, cap_bottom=False,
                       cap_top=False).transformed(M.translate(0, -0.006, 0) @ M.rot_x(-8)))
            add(M.capsule((0.0, -0.13, 0.14), (0.03, -0.2, 0.06), 0.03, 0.018, hm, seg=6, rings=1))
            add(M.capsule((0.0, -0.13, 0.14), (-0.03, -0.19, 0.03), 0.028, 0.016, hm, seg=6, rings=1))
        elif hat == 'boonie':
            hm = self.mm('hat', '#ffffff')
            add(H._dome(0.118, 0.13, 0.13, hm, (0, -0.005, 0.13), -0.01))
            add(M.loft([(0.09, 0.25, 0.26), (0.125, 0.135, 0.145), (0.135, 0.12, 0.132)], hm, seg=22, cap_bottom=False,
                       cap_top=False))
            add(M.loft([(0.075, 0.25, 0.26), (0.087, 0.25, 0.26)], hm, seg=22, cap_bottom=False, cap_top=False))


# ------------------------------------------------------------ poses

def _dd(**kw):
    return kw


HOLDS = {
    # rifle across the body, muzzle up to the left (port arms)
    'port': {'hand_r': (0.20, 0.16, 0.96), 'w_main': (-14, -38, 0), 'support': 1.0, 'elbow_l': (-0.5, -0.5, -0.7)},
    # Musketman: the musket's barrel on the right shoulder, held at the wrist of the stock
    'shoulder': {'hand_r': (0.12, 0.22, 1.12), 'w_main': (34, 22, 0), 'support': 0.0, 'elbow_r': (0.6, -0.6, -0.5)},
    # upright in front of the right side, muzzle up (Guerilla)
    'vertical': {'hand_r': (0.30, 0.14, 0.96), 'w_main': (-6, 6, 0), 'support': 0.0},
    # level at the hip, pointing forward (Infantry)
    'hip': {'hand_r': (0.24, 0.12, 0.98), 'w_main': (-96, 0, -14), 'support': 1.0, 'elbow_l': (-0.5, -0.4, -0.8)},
    # low ready, angled down (Marine)
    'low': {'hand_r': (0.22, 0.16, 1.04), 'w_main': (-112, 0, -6), 'support': 1.0, 'elbow_l': (-0.5, -0.4, -0.8)},
    # across the chest, pointing forward-left (paratroopers)
    'across': {'hand_r': (0.20, 0.20, 1.04), 'w_main': (-98, 0, 40), 'support': 1.0, 'elbow_l': (-0.5, -0.4, -0.8)},
}

AIMS = {
    'shoulder': {'hand_r': (0.17, 0.32, 1.37), 'w_main': (-90, 0, 3), 'support': 1.0, 'follow_rot': 0.0, 'head': (-6, 0, -4),
                 'elbow_r': (0.9, -0.2, -0.3), 'elbow_l': (-0.3, -0.2, -1.0),
                 'foot_l': (-0.17, 0.20, A), 'foot_r': (0.19, -0.14, A), 'fyaw_r': -30.0, 'chest': (0, 0, -6),
                 'root_pos': (0, 0.02, -0.03)},
    'hip': {'hand_r': (0.22, 0.24, 1.02), 'w_main': (-94, 0, -4), 'support': 1.0, 'follow_rot': 0.0, 'head': (4, 0, 0),
            'elbow_l': (-0.4, -0.3, -0.9), 'foot_l': (-0.18, 0.22, A), 'foot_r': (0.2, -0.16, A), 'fyaw_r': -25.0,
            'root_pos': (0, 0.02, -0.07), 'chest': (-4, 0, -4), 'spine': (-4, 0, 0)},
    # Musketman: crouched, the barrel laid in the fork of the rest planted ahead
    'rest': {'hand_r': (0.17, 0.30, 1.27), 'w_main': (-90, 0, 3), 'support': 1.0, 'follow_rot': 0.0, 'head': (-8, 0, -4),
             'w_off': (0, 0, 0), 'elbow_l': (-0.6, -0.5, -0.6),
             'elbow_r': (0.9, -0.2, -0.3), 'foot_l': (-0.2, 0.26, A), 'foot_r': (0.22, -0.2, A), 'fyaw_r': -35.0,
             'root_pos': (0, 0.0, -0.12), 'chest': (-8, 0, -4), 'spine': (-6, 0, 0)},
}

GUARD = {'hand_r': (0.24, 0.10, 1.00), 'w_main': (-70, 0, 8), 'support': 1.0, 'follow_rot': 0.0, 'elbow_l': (-0.5, -0.4, -0.8),
         'root_pos': (0, -0.02, -0.10), 'foot_l': (-0.18, 0.2, A), 'foot_r': (0.18, -0.14, A), 'fyaw_r': -25.0,
         'chest': (-4, 0, 4), 'spine': (-4, 0, 0)}


def _kneel(aim):
    k = dict(aim)
    k.update({'root_pos': (0.0, -0.08, -0.42), 'foot_l': (-0.17, 0.30, A), 'foot_r': (0.18, -0.36, 0.11),
              'toe_r': 45.0, 'fyaw_r': -20.0})
    h = _v(k['hand_r']) + _v((0, 0, -0.36))
    k['hand_r'] = tuple(h)
    if 'hand_l' in k:
        k['hand_l'] = tuple(_v(k['hand_l']) + _v((0, 0, -0.36)))
    return k


# ------------------------------------------------------------ the soldier

class Act(H.Action):
    def __init__(self, fn, loop=False, legs='ik', arms='ik', hide=(), drop_t=None, extras=None):
        super().__init__(fn, loop=loop, legs=legs, arms=arms, hide=hide)
        self.drop_t = drop_t
        self.extras = extras


@archetype('soldier')
class Soldier(H.Humanoid):
    def __init__(self, unit, params):
        params = dict(params)
        gun = params.get('gun')
        bp = dict(params)
        bp['weapon'] = params.get('weapon', gun)
        bp['offhand'] = params.get('offhand')
        super().__init__(unit, bp)
        self.p = params
        self.body = SoldierBody(bp)
        self.mats = self.body.mats
        self.parts = self.body.parts()
        self.gun = gun
        self.gspec = G.GUNS.get(gun, {'muzzle': 0.8, 'support': (0.02, 0.3), 'butt': -0.3})
        # rest pose: the humanoid stance (no weapon-specific pose), then the hold
        self.base = H.stance(None if gun else bp['weapon'], None)
        self.base['follow'] = _v(1.0 if gun else 0.0)
        self.base['support'] = _v(0.0)
        self.base['follow_rot'] = _v(1.0)
        self.poses = {'carry': dict(HOLDS.get(params.get('hold', 'port'), {})) if gun else {},
                      'aim': dict(AIMS[params.get('aim', 'shoulder')]) if gun else {},
                      'guard': dict(GUARD)}
        if params.get('offhand') == 'musket_rest':
            self.poses['carry'].update({'hand_l': (-0.36, 0.10, 1.10), 'w_off': (0, 4, 0), 'elbow_l': (-0.6, -0.8, -0.2)})
            self.poses['guard'].update({'hand_l': (-0.30, 0.25, 1.15), 'w_off': (-6, 4, 0)})
        for k, v in params.get('poses', {}).items():
            self.poses.setdefault(k, {}).update(v)
        self.base.update({k: _v(v) for k, v in self.poses['carry'].items()})
        self.base.update({k: _v(v) for k, v in params.get('stance', {}).items()})
        self._chest_rest = None
        self._cache = {}
        self._fx = []
        self._local = None

    # -- props: ours first, then the shared ones
    def prop(self, name, **kw):
        key = (name, tuple(sorted(kw.items())))
        if key not in self._props:
            fn = getattr(G, name, None) or getattr(P, name)
            m = fn(self.mats, **kw)
            if name in G.GUNS:
                # Civ3 draws guns chunky: thicken (not lengthen) them
                k = float(self.p.get('gun_thick', 1.6))
                m = m.transformed(M.scale(k, k, 1.0))
            self._props[key] = m
        return self._props[key]

    def loops(self, action):
        return self.anim_name(action) in ('idle', 'run') or self.anim_name(action).startswith('work_')

    # -- animations
    def P(self, name, **over):
        d = {k: _v(v) for k, v in self.poses[name].items()}
        d.update({k: _v(v) for k, v in over.items()})
        return d

    def shots(self, action):
        return list(self.p.get('shots', {}).get(action, []))

    def action(self, action, ctx):
        key = (action, ctx.n if ctx else None)
        if key in self._cache:
            return self._cache[key]
        name = self.anim_name(action)
        B = self.base
        n = ctx.n if ctx else 15
        if not self.gun:
            a0 = super().action(action, ctx)
            extras = None
            if name == 'build' and self.p.get('pack') == 'hiker':
                extras = self._pack_on_ground
            elif name == 'work_hammer':
                extras = self._bricks
            act = Act(a0.fn, loop=a0.loop, legs=a0.legs, arms=a0.arms, hide=a0.hide, extras=extras)
        elif name == 'idle':
            act = H.anim_idle(B)
        elif name == 'run':
            act = self.anim_run(n)
        elif name == 'fire':
            act = self.anim_fire(action, n)
        elif name == 'death':
            d = H.anim_death(B, None, None, ctx)
            act = Act(d.fn, legs=d.legs, arms=d.arms, drop_t=0.22)
        elif name == 'fortify':
            act = self.anim_fortify(n)
        elif name == 'fidget':
            act = self.anim_fidget()
        elif name == 'victory':
            act = self.anim_victory(action, n)
        elif name == 'drop':
            act = self.anim_drop(n)
        elif name == 'lunge':
            act = self.anim_lunge(ctx)
        else:
            a0 = super().action(action, ctx)
            act = Act(a0.fn, loop=a0.loop, legs=a0.legs, arms=a0.arms, hide=a0.hide)
        if not isinstance(act, Act):
            act = Act(act.fn, loop=act.loop, legs=act.legs, arms=act.arms, hide=act.hide)
        self._cache[key] = act
        return act

    def anim_run(self, n):
        base = H.anim_run(self.base, 'spear', 'shield_round', n)
        B = self.base

        def fn(t):
            p = base.fn(t)
            s = math.sin(2 * math.pi * t)
            p['hand_r'] = B['hand_r'] + _v((0, -0.03 * s, 0.03 + 0.02 * abs(s)))
            if 'hand_l' in self.poses['carry']:
                p['hand_l'] = B['hand_l'] + _v((0, 0.03 * s, 0.03))
            p['follow'] = _v(1.0)
            return p
        return Act(fn, loop=True)

    def _recoil(self, t, n, shots):
        """Recoil impulse (0..1) at normalized time t after the shots."""
        r = 0.0
        tau = 1.0 / max(n - 1, 1)
        for f in shots:
            ts = f / max(n - 1, 1)
            if t >= ts - 1e-6:
                r = max(r, math.exp(-(t - ts) / (1.2 * tau)))
        return r

    def anim_fire(self, action, n):
        B = self.base
        shots = self.shots(action)
        aim = self.P('aim')
        tf = (shots[0] / max(n - 1, 1)) if shots else 0.5
        tl = (shots[-1] / max(n - 1, 1)) if shots else 0.5
        start = self.p.get('fire_start', 'carry')
        end = self.p.get('fire_end', 'carry')
        if start == 'aim':
            if end == 'aim':
                k = [(0.0, aim), (1.0, aim)]
            else:
                ke = {c: B[c] for c in aim if c in B} if end == 'carry' else self.P(end)
                k = [(0.0, aim), (min(0.85, tl + 0.12), aim), (1.0, ke)]
        else:
            t_up = max(0.08, min(tf - 0.12, 0.4))
            t_dn = min(0.98, tl + 0.3)
            k0 = {} if start == 'carry' else self.P(start)
            k = [(0.0, k0), (t_up, aim), (max(t_up + 0.01, tf), aim), (min(t_dn - 0.05, max(tl + 0.12, tf + 0.01)), aim)]
            if end == 'aim':
                k.append((1.0, aim))
            elif end == 'carry':
                k.append((1.0, {c: B[c] for c in aim if c in B}))
            else:
                k.append((1.0, self.P(end)))
        anim = H.keys(B, k)

        def fn(t):
            p = anim(t)
            r = self._recoil(t, n, shots)
            if r > 0:
                p['hand_r'] = p['hand_r'] + _v((0, -0.05, 0.015)) * r
                p['w_main'] = p['w_main'] + _v((7, 0, 0)) * r
                p['chest'] = p.get('chest', _v((0, 0, 0))) + _v((5, 0, 0)) * r
                p['head'] = p.get('head', _v((0, 0, 0))) + _v((4, 0, 0)) * r
            return p
        return Act(fn)

    def anim_lunge(self, ctx):
        """A crouched rush forward with the weapon at the guard, a jab, and back."""
        B = self.base
        g = self.P('guard')
        st = ctx.event('reach_max', 0.5) if ctx is not None else 0.5
        st = min(max(st or 0.5, 0.35), 0.75)
        jab = dict(g)
        jab.update({'root_pos': _v((0, 0.30, -0.18)), 'hand_r': _v((0.20, 0.36, 1.02)), 'w_main': _v((-82, 0, 4)),
                    'foot_l': _v((-0.18, 0.62, A)), 'foot_r': _v((0.2, -0.16, A)), 'toe_r': _v(25.0),
                    'chest': _v((-14, 0, 6)), 'spine': _v((-10, 0, 0))})
        g = dict(g, toe_r=B['toe_r'])
        k = [(0.0, {}), (0.25, g), (0.5, jab), (0.62, jab), (0.85, g), (1.0, {c: B[c] for c in g if c in B})]
        anim = H.keys(B, k)
        return Act(lambda t: anim(H._warp(t, [(0.5, st)])))

    def anim_fortify(self, n):
        B = self.base
        kind = self.p.get('fortify', 'aim')
        tgt = {'aim': self.P('aim'), 'guard': self.P('guard'), 'kneel': {k: _v(v) for k, v in _kneel(self.poses['aim']).items()}}[kind]
        anim = H.keys(B, [(0.0, {}), (0.7, tgt), (1.0, tgt)])
        return Act(anim)

    def anim_fidget(self):
        B = self.base
        k = [(0.0, {}),
             (0.2, {'head': (0, 0, 35), 'chest': (0, 0, 8), 'root_pos': (0.03, 0, -0.01)}),
             (0.45, {'head': (-6, 0, 30), 'hand_r': B['hand_r'] + _v((0.0, 0.06, 0.08)), 'w_main': B['w_main'] + _v((10, 6, 0))}),
             (0.65, {'head': (4, 0, -35), 'chest': (0, 0, -8), 'root_pos': (-0.03, 0, -0.01)}),
             (0.85, {'head': (0, 0, -10), 'hand_r': B['hand_r'], 'w_main': B['w_main']}),
             (1.0, {'head': (0, 0, 0), 'chest': (0, 0, 0), 'root_pos': (0, 0, 0)})]
        return Act(H.keys(B, k))

    def anim_victory(self, action, n):
        B = self.base
        kind = self.p.get('victory', 'raise')
        shots = self.shots(action)
        hide = ()
        if kind == 'raise':
            up = {'hand_r': (0.28, 0.12, 1.92), 'w_main': (-92, 0, 88), 'support': 1.0, 'chest': (6, 0, 0),
                  'head': (-12, 0, 0), 'elbow_r': (0.8, -0.3, 0.2), 'elbow_l': (-0.8, -0.3, 0.2)}
            if self.p.get('offhand') == 'musket_rest':
                up = {'hand_r': (0.28, 0.12, 1.85), 'w_main': (-10, 10, 0), 'hand_l': (-0.36, 0.1, 1.6),
                      'w_off': (-5, -10, 0), 'chest': (6, 0, 0), 'head': (-12, 0, 0), 'elbow_r': (0.8, -0.3, 0.2)}
        elif kind == 'fire_air':
            up = {'hand_r': (0.24, 0.12, 1.55), 'w_main': (-8, -4, 0), 'support': 0.0, 'chest': (8, 0, 0),
                  'head': (-16, 0, 0), 'elbow_r': (0.8, -0.3, 0.2), 'hand_l': (-0.40, 0.05, 1.5),
                  'elbow_l': (-0.8, -0.3, 0.0)}
        elif kind == 'hat':
            up = {'support': 0.0, 'hand_l': (-0.30, 0.10, 2.0), 'elbow_l': (-0.8, -0.3, 0.2), 'head': (-10, 0, 0),
                  'chest': (4, 0, 0), 'hat_in_hand': 1.0}
            hide = ('hat',)
        else:   # fist
            up = {'support': 0.0, 'hand_l': (-0.34, 0.10, 1.92), 'elbow_l': (-0.8, -0.3, 0.2), 'head': (-10, 0, 0),
                  'chest': (4, 0, 0)}
        up = {c: _v(v) for c, v in up.items()}
        half = {c: v * 0.7 + _v(B.get(c, v)) * 0.3 for c, v in up.items()}
        if kind == 'hat':
            half['hat_in_hand'] = _v(1.0)
        rest = {c: _v(B[c]) if c in B else (_v(0.0) if c == 'hat_in_hand' else up[c]) for c in up}
        if kind == 'fire_air':
            k = [(0.0, {}), (0.2, up), (0.85, up), (1.0, rest)]
        else:
            k = [(0.0, {}), (0.22, dict(up, root_pos=(0, 0, 0.03))), (0.42, dict(half, root_pos=(0, 0, -0.03))),
                 (0.62, dict(up, root_pos=(0, 0, 0.04))), (0.84, half), (1.0, rest)]
        anim = H.keys(dict(B, hat_in_hand=_v(0.0)), k)

        def fn(t):
            p = anim(t)
            r = self._recoil(t, n, shots)
            if r > 0:
                p['hand_r'] = p['hand_r'] + _v((0, 0, -0.04)) * r
            return p
        act = Act(fn)
        if kind == 'hat':
            act.hat_switch = True
        return act

    def anim_drop(self, n):
        """Paratroopers' BUILD: a jump. Free fall high up, the canopy opens,
        drifts down, lands, the canopy collapses, and the soldier stands with
        his weapon ready."""
        B = self.base
        h0 = float(self.p.get('drop_height', 2.5))
        hang = {'hand_r': (0.20, 0.02, 1.86), 'hand_l': (-0.20, 0.02, 1.86), 'support': 0.0, 'follow': 1.0,
                'elbow_r': (0.8, -0.2, -0.2), 'elbow_l': (-0.8, -0.2, -0.2), 'head': (-8, 0, 0)}

        def legs(z, spread=0.0, fwd=0.06):
            return {'foot_r': (0.12 + spread, fwd, z + A + 0.04), 'foot_l': (-0.12 - spread, fwd - 0.06, z + A + 0.08),
                    'toe_r': -30.0, 'toe_l': -30.0}

        k = [
            (0.0, dict(hang, root_pos=(0, 0, h0), root_rot=(-35, 0, 0), chest=(-10, 0, 0), **legs(h0, 0.10, -0.15))),
            (0.09, dict(hang, root_pos=(0, 0, h0 * 0.72), root_rot=(0, 0, 0), chest=(0, 0, 0), **legs(h0 * 0.72, 0.06))),
            (0.18, dict(hang, root_pos=(0, 0, h0 * 0.42), **legs(h0 * 0.42, 0.04))),
            (0.40, dict(hang, root_pos=(0, 0, 0.12), **legs(0.12, 0.03))),
            (0.50, dict(hang, root_pos=(0, 0.0, -0.14), foot_r=(0.16, 0.04, A), foot_l=(-0.16, -0.04, A), toe_r=0.0, toe_l=0.0)),
            (0.70, {'root_pos': (0, 0, -0.04), 'hand_r': (0.30, 0.0, 1.2), 'hand_l': (-0.30, 0.0, 1.2),
                    'foot_r': B['foot_r'], 'foot_l': B['foot_l'], 'head': (0, 0, 0)}),
            (0.86, {c: B[c] for c in ('hand_r', 'hand_l', 'w_main', 'root_pos', 'head', 'elbow_r', 'elbow_l')}),
            (1.0, {}),
        ]
        anim = H.keys(dict(B, root_rot=_v((0, 0, 0)), chest=_v((0, 0, 0))), k)

        def fn(t):
            p = anim(t)
            p['support'] = _v(float(B['support']) if t >= 0.86 else 0.0)
            p['canopy_t'] = _v(t)
            return p
        act = Act(fn, extras=self._canopy)
        act.gun_from = 0.84
        return act

    # -- extras placed in the world per frame
    def _canopy(self, pose, W):
        t = float(pose.get('canopy_t', 1.0))
        if t >= 0.84:
            return []
        rz = float(_v(pose['root_pos'])[2])
        sh = (W['chest'][:3, 3] + _v((0, 0, 0.3)))
        cm = 'canopy'
        if t < 0.05:
            # still packed: a bundle on the back, the pilot chute out
            return [M.sphere(0.14, self.body.mm(cm, '#ececea'), (0, -0.25, rz + 1.6), seg=10)]
        open_ = min(1.0, (t - 0.04) / 0.12)
        hc = 1.05
        line = 0.45 + 0.75 * open_
        skirt = rz + 1.5 + line
        collapse = 0.0
        if t > 0.5:
            collapse = min(1.0, (t - 0.5) / 0.32)
            skirt = skirt * (1 - collapse ** 0.7) + 0.25 * collapse ** 0.7
        mesh = G.parachute(self.mats, radius=float(self.p.get('canopy_r', 1.25)), height=hc, open_=open_,
                           lines_to=tuple(sh - _v((0, 0.0, skirt))), collapse=collapse, m=cm)
        drift = _v((0.45 * collapse, -0.35 * collapse, 0.0))
        return [mesh.transformed(M.translate(*(_v((0, 0, skirt)) + drift)))]

    def _pack_on_ground(self, pose, W):
        if float(pose.get('pack', 0.0)) <= 0.5:
            return []
        return [G.hiker_pack(self.mats).transformed(M.translate(0.10, 0.55, -0.12) @ M.rot_x(-80))]

    def _bricks(self, pose, W):
        return [G.brick_pile(self.mats).transformed(M.translate(0.1, 0.62, 0) @ M.rot_z(8))]

    # -- posing
    def frame(self, action, f, n, ctx):
        act = self.action(action, ctx)
        t = rig.frame_times(n, act.loop)[f]
        pose = act.fn(t)
        weapon = self.weapon
        name = self.anim_name(action)
        if name in H.WORK_TOOLS:
            weapon = self.p.get('work_tools', {}).get(name, H.WORK_TOOLS[name])
        if getattr(act, 'gun_from', None) is not None and t < act.gun_from:
            weapon = None
        offhand = self.offhand
        dropped = act.drop_t is not None and t >= act.drop_t
        mesh, info = self.pose_full(pose, act, None if dropped else weapon, None if dropped else offhand)
        if dropped:
            mesh = M.Mesh.concat([mesh] + self._dropped(act, t, weapon))
        self._fx = self.effects(action, f, n, act, info, ctx)
        self._local = mesh
        finish_colors(self, self.p)
        return mesh

    def _solve(self, pose, act):
        """Bone world transforms for a pose, with the gun hand transform."""
        h = self.h
        sk = self.skel
        pose = dict(pose)
        root_pos = _v(pose.get('root_pos', (0, 0, 0)))
        base_pose = {'root_pos': root_pos, 'root_rot': pose.get('root_rot', (0, 0, 0))}
        for b in ('spine', 'chest', 'neck', 'head'):
            base_pose[b] = pose.get(b, (0, 0, 0))
        base_pose['pelvis'] = np.zeros(3, np.float32)
        limb_fk = {}
        for b in ('arm_r', 'forearm_r', 'arm_l', 'forearm_l', 'thigh_r', 'shin_r', 'thigh_l', 'shin_l',
                  'foot_r', 'foot_l', 'hand_r', 'hand_l'):
            limb_fk[b] = pose.get(b, (0, 0, 0))
        W0 = sk.world(dict(base_pose, **limb_fk))
        # hands follow the chest (targets keyed relative to an upright torso)
        if self._chest_rest is None:
            z = {k: np.zeros(3, np.float32) for k in base_pose}
            z['root_pos'] = np.zeros(3, np.float32)
            self._chest_rest = sk.world(dict(z, **{k: np.zeros(3, np.float32) for k in limb_fk}))['chest']
        fw = float(pose.get('follow', 0.0))
        D = W0['chest'] @ np.linalg.inv(self._chest_rest)
        Rm = M.euler(*_v(pose.get('w_main', (0, 0, 0))))[:3, :3]
        Ro = M.euler(*_v(pose.get('w_off', (0, 0, 0))))[:3, :3]

        def fol(pt):
            pt = _v(pt)
            q = D[:3, :3] @ pt + D[:3, 3]
            return pt * (1 - fw) + q * fw
        hr = fol(pose['hand_r'])
        hl = fol(pose['hand_l'])
        fr = fw * float(pose.get('follow_rot', 1.0))
        if fr > 0:
            Rm = _orthonormal(Rm * (1 - fr) + (D[:3, :3] @ Rm) * fr)
            Ro = _orthonormal(Ro * (1 - fr) + (D[:3, :3] @ Ro) * fr)
        s = float(pose.get('support', 0.0))
        if s > 0 and self.gun:
            sy, sz = self.gspec['support']
            sp = hr + Rm @ _v((0.0, sy, sz))
            hl = hl * (1 - s) + sp * s
        pose['hand_r'], pose['hand_l'] = hr, hl
        over = {}
        if act.legs == 'ik':
            for side in 'rl':
                hip = W0[f'thigh_{side}'][:3, 3]
                target = _v(pose[f'foot_{side}']).copy()
                target[2] = target[2] * h
                pf = W0['pelvis'][:3, 1]
                pole = H._normalize(pf + _v((0.15 if side == 'r' else -0.15, 0, 0)))
                Ru, Rl, _, _ = H.two_bone(hip, target, H.THIGH * h, H.SHIN * h, pole, bend_forward=False)
                over[f'thigh_{side}'] = Ru
                over[f'shin_{side}'] = Rl
                yaw = float(pose.get(f'fyaw_{side}', 0.0)) + float(np.degrees(np.arctan2(-pf[0], pf[1])))
                over[f'foot_{side}'] = (M.rot_z(yaw) @ M.rot_x(float(pose.get(f'toe_{side}', 0.0))))[:3, :3]
        if act.arms == 'ik':
            for side in 'rl':
                sh = W0[f'arm_{side}'][:3, 3]
                grip = _v(pose[f'hand_{side}']) * _v((1, 1, h))
                pole = H._normalize(_v(pose.get(f'elbow_{side}', (0, -1, -0.3))))
                d = H._normalize(grip - sh)
                wrist = grip - d * H.GRIP
                Ru, Rl, _, _ = H.two_bone(sh, wrist, H.UPPER_ARM * h, H.FOREARM * h, pole, bend_forward=True)
                over[f'arm_{side}'] = Ru
                over[f'forearm_{side}'] = Rl
                over[f'hand_{side}'] = Rl
        W = H._world_with(sk, dict(base_pose, **limb_fk), over)
        Ts = {}
        for side, chan in (('r', 'w_main'), ('l', 'w_off')):
            Wh = W[f'hand_{side}']
            grip = Wh[:3, 3] + Wh[:3, :3] @ _v((0, 0.01, -H.GRIP))
            if act.arms == 'ik':
                Rx = Rm if side == 'r' else Ro
            else:
                Rx = Wh[:3, :3] @ M.rot_x(-90)[:3, :3]
            T = np.eye(4, dtype=np.float32)
            T[:3, :3] = Rx
            T[:3, 3] = grip
            Ts[side] = T
        return W, Ts, pose

    def pose_full(self, pose, act, weapon, offhand='same'):
        if offhand == 'same':
            offhand = self.offhand
        W, Ts, pose = self._solve(pose, act)
        meshes = []
        hide = set(getattr(act, 'hide', ()))
        if float(pose.get('pack', 0.0)) > 0.5:
            hide.add('backpack')
        hat_hand = getattr(act, 'hat_switch', False) and float(pose.get('hat_in_hand', 0.0)) > 0.5
        for bone, m, tag in self.parts:
            if tag in hide and not (tag == 'hat' and not hat_hand):
                continue
            meshes.append(m.transformed(W[bone]))
        if hat_hand:
            Wh = W['hand_l']
            hat = M.Mesh.concat([m for b, m, tag in self.parts if tag == 'hat'])
            T = Wh @ M.translate(0.0, 0.06, -0.12) @ M.rot_x(-70) @ M.translate(0, 0, -0.2)
            meshes.append(hat.transformed(T))
        for side, item in (('r', weapon), ('l', offhand)):
            if item is None:
                continue
            meshes.append(self.prop(item).transformed(Ts[side]))
        if getattr(act, 'extras', None) is not None:
            meshes += act.extras(pose, W)
        return M.Mesh.concat(meshes), {'W': W, 'T': Ts, 'pose': pose}

    def _dropped(self, act, t, weapon):
        """The weapons fall from the hands at act.drop_t and land on the ground."""
        out = []
        key = ('drop', id(act))
        if key not in self._cache:
            p0 = act.fn(act.drop_t)
            _, Ts, _ = self._solve(p0, act)
            self._cache[key] = Ts
        Ts = self._cache[key]
        u = min(1.0, (t - act.drop_t) / 0.22)
        for side, item, dx in (('r', weapon, 0.25), ('l', self.offhand, -0.3)):
            if item is None:
                continue
            T0 = Ts[side]
            fwd = T0[:3, 2]
            yaw = math.degrees(math.atan2(-fwd[0], fwd[1])) + (25 if side == 'r' else -25)
            land = M.translate(T0[0, 3] + dx, T0[1, 3] + 0.15, 0.045) @ M.rot_z(yaw) @ M.rot_x(-90) @ M.rot_y(90)
            if item == 'musket_rest':
                land = M.translate(T0[0, 3] + dx, T0[1, 3] + 0.2, 0.03) @ M.rot_z(yaw) @ M.rot_x(-90)
            Rm = _orthonormal(T0[:3, :3] * (1 - u) + land[:3, :3] * u)
            pos = T0[:3, 3] * (1 - u) + land[:3, 3] * u
            pos[2] = T0[2, 3] * (1 - u * u) + land[2, 3] * u * u
            T = np.eye(4, dtype=np.float32)
            T[:3, :3] = Rm
            T[:3, 3] = pos
            out.append(self.prop(item).transformed(T))
        return out

    # -- muzzle flashes and smoke (drawn in 2D over the rendered cell)
    def muzzle(self, info):
        T = info['T']['r']
        L = self.gspec['muzzle']
        return T[:3, 3] + T[:3, 2] * L, T[:3, 2].copy()

    def effects(self, action, f, n, act, info, ctx):
        shots = self.shots(action)
        if not shots or not self.gun:
            return []
        fx = []
        style = self.p.get('flash', 'burst')
        smoke = self.p.get('smoke', 'light')
        fs = set(shots)
        runs = []
        for s in sorted(shots):
            if runs and s == runs[-1][-1] + 1:
                runs[-1].append(s)
            else:
                runs.append([s])
        size = float(self.p.get('flash_size', 0.32))
        if style == 'musket':
            for run in runs:
                if run[0] <= f <= run[-1] + 0:
                    k = f - run[0]
                    pos, d = self._muzzle_at(action, run[0], n, act)
                    fx.append({'kind': 'flash', 'pos': pos + d * (0.12 + 0.32 * k), 'dir': d,
                               'size': size * (1.0 + 0.25 * k), 'round': k > 0})
        elif f in fs:
            pos, d = self.muzzle(info)
            fx.append({'kind': 'flash', 'pos': pos, 'dir': d, 'size': size * (0.9 + 0.2 * ((f * 7) % 3) / 2)})
        if smoke:
            heavy = smoke == 'heavy'
            for run in runs:
                s0 = run[0]
                if f < s0 or (style != 'musket' and f == s0):
                    continue
                pos, d = self._muzzle_at(action, s0, n, act)
                age = f - s0
                life = (n - 1 - s0) + 1 if heavy else 5
                if age > life:
                    continue
                u = age / max(life, 1)
                push = (0.55 if heavy else 0.25) * (1 - math.exp(-age / 2.5))
                rad = (0.20 if heavy else 0.09) * (0.6 + 1.4 * u ** 0.5)
                alpha = (0.62 if heavy else 0.4) * (1 - u) ** 1.2
                c = pos + d * (0.2 + push) + _v((0, 0, 0.05 * age))
                fx.append({'kind': 'smoke', 'pos': c, 'rad': rad, 'alpha': alpha, 'seed': s0})
        return fx

    def _muzzle_at(self, action, f0, n, act):
        key = ('muzzle', action, f0, n)
        if key not in self._cache:
            t = rig.frame_times(n, act.loop)[f0]
            _, info = self.pose_full(act.fn(t), act, self.weapon)
            self._cache[key] = self.muzzle(info)
        return self._cache[key]

    def render_cell(self, mesh, mats, cam, W, H_):
        b, t = R.render_cell(mesh, mats, cam, W, H_)
        if not self._fx:
            return b, t
        return draw_effects(b, t, self._fx, _facing_of(self._local, mesh), cam)


def _facing_of(local, mesh):
    """The yaw (radians) the framework turned the local mesh by."""
    if local is None or not len(local):
        return 0.0
    v0 = local.V.reshape(-1, 3)
    v1 = mesh.V.reshape(-1, 3)
    i = int(np.argmax(v0[:, 0] ** 2 + v0[:, 1] ** 2))
    return math.atan2(v1[i, 1], v1[i, 0]) - math.atan2(v0[i, 1], v0[i, 0])


def draw_effects(base, tint, fx, yaw, cam):
    """Muzzle flashes (glowing, additive-looking) and soft translucent powder
    smoke, composited over (or, when behind the body, under) the sprite."""
    Hh, Ww = base.shape[:2]
    c, s = math.cos(yaw), math.sin(yaw)
    Rz = np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]], np.float32)
    ref = cam.project(Rz @ _v((0, 0, 1.1)))
    yy, xx = np.mgrid[0:Hh, 0:Ww].astype(np.float32) + 0.5
    over_p = np.zeros((Hh, Ww, 3), np.float32)
    over_a = np.zeros((Hh, Ww), np.float32)
    under_p = np.zeros((Hh, Ww, 3), np.float32)
    under_a = np.zeros((Hh, Ww), np.float32)
    ppm = cam.ppm
    for e in sorted(fx, key=lambda e: e['kind'] != 'smoke'):
        p = Rz @ _v(e['pos'])
        sp = cam.project(p)
        behind = sp[2] < ref[2] - 0.15
        if e['kind'] == 'flash':
            d = Rz @ _v(e['dir'])
            d2 = np.array([d[0], -(d[1] * R.SIN_E + d[2] * R.COS_E)], np.float32)
            L = float(np.linalg.norm(d2)) * e['size'] * ppm
            w = max(e['size'] * ppm * 0.28, 2.2)
            dn = d2 / (np.linalg.norm(d2) + 1e-6)
            if e.get('round'):
                L = 0.0
                w *= 1.6
            cx, cy = sp[0] + dn[0] * L * 0.45, sp[1] + dn[1] * L * 0.45
            u = (xx - cx) * dn[0] + (yy - cy) * dn[1]
            v = -(xx - cx) * dn[1] + (yy - cy) * dn[0]
            a_len = max(L * 0.55, w)
            I1 = np.exp(-2.2 * (u / a_len) ** 2 - 2.2 * (v / w) ** 2)
            r0 = np.hypot(xx - sp[0], yy - sp[1])
            I2 = np.exp(-2.0 * (r0 / (w * 0.9)) ** 2)
            # four short spikes
            ang = np.arctan2(v, u)
            spike = np.exp(-2.0 * (np.hypot(u, v) / (a_len * 1.25)) ** 2) * np.clip(np.cos(4 * ang), 0, 1) ** 6 * 0.7
            I = np.clip(np.maximum(np.maximum(I1, I2), spike), 0, 1)
            col = np.where(I[..., None] > 0.6, np.array([1.0, 0.98, 0.86], np.float32),
                           np.where(I[..., None] > 0.3, np.array([1.0, 0.86, 0.42], np.float32),
                                    np.array([1.0, 0.58, 0.16], np.float32)))
            col = col * 1.0
            a = np.clip(I * 1.7, 0, 1) * (I > 0.04)
        else:
            rad = e['rad'] * ppm
            rng = np.random.default_rng(int(e.get('seed', 0)) * 31 + 5)
            a = np.zeros((Hh, Ww), np.float32)
            shade = np.zeros((Hh, Ww), np.float32)
            for j in range(5):
                ox, oy = (rng.uniform(-0.5, 0.5, 2) * rad) if j else (0.0, 0.0)
                rr = rad * (0.75 if j else 0.85) * rng.uniform(0.85, 1.1)
                dx, dy = xx - (sp[0] + ox), yy - (sp[1] + oy)
                q = np.clip(1 - (dx * dx + dy * dy) / (rr * rr), 0, 1) ** 1.3
                lit = np.clip(-(dx + dy) / (1.6 * rr), -1, 1)
                shade = np.where(q > a, lit, shade)
                a = np.maximum(a, q)
            col = np.array([0.86, 0.86, 0.84], np.float32)[None, None] * (0.86 + 0.16 * shade[..., None])
            a = a * e['alpha']
        tgt_p, tgt_a = (under_p, under_a) if behind else (over_p, over_a)
        tgt_p[:] = col * a[..., None] + tgt_p * (1 - a[..., None])
        tgt_a[:] = a + tgt_a * (1 - a)
    bf = base.astype(np.float32) / 255
    tf = tint.astype(np.float32) / 255
    Ba = bf[..., 3]
    Bp = bf[..., :3] * Ba[..., None]
    Ta = tf[..., 3]
    cover = np.maximum(Ba, Ta)
    k = under_a * (1 - cover)
    Bp = Bp + under_p * (1 - cover)[..., None]
    Ba = Ba + k
    Bp = over_p + Bp * (1 - over_a[..., None])
    Ba = over_a + Ba * (1 - over_a)
    Ta2 = Ta * (1 - over_a)
    out_b = np.zeros_like(bf)
    out_b[..., :3] = Bp / np.maximum(Ba, 1e-6)[..., None]
    out_b[..., 3] = Ba
    out_t = tf.copy()
    out_t[..., 3] = Ta2
    return R._u8(out_b), R._u8(out_t)


# ------------------------------------------------------------ the TOW team

@archetype('tow_team')
class TowTeam:
    """A gunner kneeling at a TOW launcher on its tripod, and a loader beside
    him; ammunition boxes on the ground."""

    def __init__(self, unit, params):
        self.unit = unit
        self.p = params
        gp = dict(params.get('soldier', {}))
        gp['gun'] = None
        self.gunner = Soldier(unit, dict(gp, _seed='g'))
        self.loader = Soldier(unit, dict(gp, _seed='l'))
        # one material table: the loader's parts are remapped into the gunner's
        self.mats = self.gunner.mats
        self.loader.mats = self.mats
        self.loader.body.mats = self.mats
        self.loader.parts = self.gunner.parts
        self.mount = _v(params.get('mount', (0.05, 0.25, 0.0)))
        self.loader_at = _v(params.get('loader_at', (-0.80, -0.50, 0.0)))
        self._fx = []
        self._local = None
        self._acts = {}

    def loops(self, action):
        return action in ('DEFAULT', 'RUN')

    # poses of the two (model space of each soldier, facing +y)
    def _gunner_pose(self, action, t, n):
        B = self.gunner.base
        kneel = {'root_pos': (0.0, -0.05, -0.44), 'foot_l': (-0.17, 0.26, A), 'foot_r': (0.18, -0.38, 0.11),
                 'toe_r': 45.0, 'fyaw_r': -15.0, 'spine': (-14, 0, 0), 'chest': (-8, 0, 0), 'head': (6, 0, 0),
                 'hand_r': (0.12, 0.48, 0.86), 'hand_l': (-0.12, 0.42, 0.82), 'elbow_r': (0.8, -0.4, -0.4),
                 'elbow_l': (-0.8, -0.4, -0.4)}
        k = {c: _v(v) for c, v in kneel.items()}
        p = dict(B)
        p.update(k)
        p['follow'] = _v(0.0)
        p['support'] = _v(0.0)
        if action == 'DEFAULT' or action == 'FIDGET':
            s = math.sin(2 * math.pi * t)
            p['chest'] = p['chest'] + _v((1.2 * s, 0, 0))
        elif action == 'ATTACK1':
            r = 0.0
            tf = 2 / max(n - 1, 1)
            if t >= tf:
                r = math.exp(-(t - tf) / 0.12)
            p['root_pos'] = p['root_pos'] + _v((0, -0.04, 0)) * r
            p['head'] = p['head'] + _v((8, 0, 0)) * r
        elif action == 'FORTIFY':
            u = min(1.0, t / 0.7)
            p['root_pos'] = p['root_pos'] + _v((0, 0, -0.04)) * u
        return p

    def _loader_pose(self, action, t, n):
        L = self.loader
        B = dict(L.base)
        B['follow'] = _v(0.0)
        if action in ('DEFAULT', 'FIDGET', 'ATTACK1'):
            act = H.anim_idle(B)
            p = act.fn(t)
            if action == 'FIDGET':
                k = H.keys(B, [(0.0, {}), (0.25, {'head': (0, 0, 40), 'chest': (0, 0, 10)}),
                               (0.5, {'head': (-10, 0, 30), 'hand_l': (-0.18, 0.12, 1.62), 'elbow_l': (-0.8, -0.3, 0.2)}),
                               (0.75, {'head': (0, 0, -30), 'hand_l': B['hand_l']}), (1.0, {'head': (0, 0, 0)})])
                p = k(t)
            if action == 'ATTACK1':
                p['hand_l'] = _v((-0.2, 0.12, 1.45))
                p['hand_r'] = _v((0.22, 0.12, 1.45))
                p['head'] = _v((0, 0, 20))
                p['root_pos'] = _v((0, 0, -0.04))
            return p
        if action == 'FORTIFY':
            kneel = {'root_pos': (0.0, -0.05, -0.44 * min(1, t / 0.7)), 'foot_l': (-0.17, 0.26, A),
                     'foot_r': (0.18, -0.38 * min(1, t / 0.7) - 0.0, A + 0.03 * min(1, t / 0.7)),
                     'toe_r': 45.0 * min(1, t / 0.7), 'hand_r': (0.3, 0.3, 0.75), 'hand_l': (-0.25, 0.3, 0.8)}
            p = dict(B)
            p.update({c: _v(v) for c, v in kneel.items()})
            return p
        if action == 'VICTORY':
            act = H.anim_victory(B, None, None, None)
            return act.fn(t)
        return dict(B)

    def frame(self, action, f, n, ctx):
        mesh = self._frame(action, f, n, ctx)
        finish_colors(self, dict(self.p.get('soldier', {}), **{k: v for k, v in self.p.items() if k != 'soldier'}))
        return mesh

    def _frame(self, action, f, n, ctx):
        t_loop = action in ('DEFAULT', 'RUN')
        t = rig.frame_times(n, t_loop)[f]
        meshes = []
        self._fx = []
        G_ = self.gunner
        if action == 'RUN':
            # both run: the gunner with the tube on his shoulder, the loader with the tripod
            for who, off, ph, carry in ((G_, _v((0.38, 0.25, 0)), 0.0, 'tube'), (self.loader, _v((-0.4, -0.4, 0)), 0.5, 'tripod')):
                act = Act(H.anim_run(who.base, 'spear', 'shield_round', n).fn, loop=True)
                p = act.fn((t + ph) % 1.0)
                p['follow'] = _v(1.0)
                if carry == 'tube':
                    p['hand_r'] = _v((0.22, 0.28, 1.45))
                    p['hand_l'] = _v((0.05, 0.5, 1.45))
                else:
                    p['hand_r'] = _v((0.22, 0.30, 1.0))
                    p['hand_l'] = _v((-0.2, 0.30, 1.0))
                m, info = who.pose_full(p, act, None)
                W = info['W']
                if carry == 'tube':
                    C = W['chest']
                    T = C @ M.translate(0.24, 0.05, 0.36) @ M.rot_x(18)
                    m = M.Mesh.concat([m, G.tow_launcher(self.mats).transformed(T)])
                else:
                    pos = (info['T']['r'][:3, 3] + info['T']['l'][:3, 3]) / 2
                    m = M.Mesh.concat([m, G.tow_tripod(self.mats, h=0.5).transformed(M.translate(pos[0], pos[1] + 0.05, pos[2] - 0.5))])
                meshes.append(m.transformed(M.translate(*off)))
            mesh = M.Mesh.concat(meshes)
            self._local = mesh
            return mesh
        if action == 'DEATH':
            return self._death(t, n)
        # the launcher and boxes
        pitch = 0.0
        meshes.append(G.tow_tripod(self.mats).transformed(M.translate(*self.mount)))
        lt = M.translate(*(self.mount + _v((0, 0, 0.62)))) @ M.rot_x(pitch)
        recoil = 0.0
        if action == 'ATTACK1':
            tf = 2 / max(n - 1, 1)
            if t >= tf:
                recoil = math.exp(-(t - tf) / 0.1)
        meshes.append(G.tow_launcher(self.mats).transformed(lt @ M.translate(0, -0.04 * recoil, 0)))
        for pos, yaw in (((0.7, -0.15, 0), 20), ((-0.75, 0.45, 0), -15), ((0.85, 0.5, 0), 75)):
            meshes.append(G.ammo_box(self.mats).transformed(M.translate(*pos) @ M.rot_z(yaw)))
        # the gunner kneels behind the sight
        gp = self._gunner_pose(action, t, n)
        gm, _ = G_.pose_full(gp, Act(None), None)
        meshes.append(gm.transformed(M.translate(*(self.mount + _v((-0.18, -0.62, 0))))))
        lp = self._loader_pose(action, t, n)
        lm, _ = self.loader.pose_full(lp, Act(None), None)
        meshes.append(lm.transformed(M.translate(*self.loader_at) @ M.rot_z(-20)))
        if action == 'ATTACK1':
            # missile out of the tube, backblast behind
            f0 = 2
            front = self.mount + _v((0, 0.62 + 0.75, 0.62))
            back = self.mount + _v((0, -0.62, 0.62))
            if f >= f0:
                age = f - f0
                if age <= 1:
                    self._fx.append({'kind': 'flash', 'pos': front, 'dir': _v((0, 1, 0)), 'size': 0.6})
                    self._fx.append({'kind': 'flash', 'pos': back, 'dir': _v((0, -1, 0)), 'size': 0.8})
                if age <= 2:
                    meshes.append(G.tow_missile(self.mats).transformed(M.translate(*(front + _v((0, 0.5 + 2.2 * age, 0.02 * age))))))
                life = n - 1 - f0
                u = age / max(life, 1)
                for j, (off, rr) in enumerate(((_v((0, -0.5, 0.0)), 0.30), (_v((0.2, -0.9, -0.1)), 0.26), (_v((-0.2, -0.8, -0.05)), 0.24))):
                    self._fx.append({'kind': 'smoke', 'pos': back + off * (0.5 + u) + _v((0, 0, 0.1 * u)),
                                     'rad': rr * (0.6 + 1.2 * u ** 0.5), 'alpha': 0.6 * (1 - u) ** 1.2, 'seed': j + 1})
                if age >= 1:
                    self._fx.append({'kind': 'smoke', 'pos': front + _v((0, 0.3, 0)), 'rad': 0.16 * (0.6 + u),
                                     'alpha': 0.45 * (1 - u) ** 1.5, 'seed': 9})
        mesh = M.Mesh.concat(meshes)
        self._local = mesh
        return mesh

    def _death(self, t, n):
        meshes = []
        meshes.append(G.tow_tripod(self.mats).transformed(M.translate(*self.mount)))
        lt = M.translate(*(self.mount + _v((0, 0, 0.62))))
        tip = min(1.0, max(0.0, (t - 0.15) / 0.25))
        meshes.append(G.tow_launcher(self.mats).transformed(lt @ M.rot_x(-8 * tip)))
        for pos, yaw in (((0.7, -0.15, 0), 20), ((-0.75, 0.45, 0), -15), ((0.85, 0.5, 0), 75)):
            meshes.append(G.ammo_box(self.mats).transformed(M.translate(*pos) @ M.rot_z(yaw)))
        # both fall: the gunner from his knee, the loader backward
        for who, at, yaw, delay in ((self.gunner, self.mount + _v((-0.18, -0.62, 0)), 0, 0.0),
                                    (self.loader, self.loader_at, -20, 0.08)):
            d = H.anim_death(who.base, None, None, None)
            tt = min(1.0, max(0.0, (t - delay) / (1 - delay)))
            p = d.fn(tt)
            m, _ = who.pose_full(p, d, None)
            meshes.append(m.transformed(M.translate(*at) @ M.rot_z(yaw)))
        mesh = M.Mesh.concat(meshes)
        self._local = mesh
        return mesh

    def render_cell(self, mesh, mats, cam, W, H_):
        b, t = R.render_cell(mesh, mats, cam, W, H_)
        if not self._fx:
            return b, t
        return draw_effects(b, t, self._fx, _facing_of(self._local, mesh), cam)
