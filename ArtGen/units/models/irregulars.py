"""The irregulars (Modern's Insurgent, Militant and Rebel, and the
Partisans): models/league_foot.py's soldier with the clothes, headgear and
gear of fighters without a uniform (gear in models/irregulars_gear.py).

Archetype 'irregular': 'league_soldier' plus

    hat       'shemagh' (wound over the head and face, eyes showing, a tail
              down the back; material 'hat', edge stripes 'hat_stripe') |
              'pakol' (the rolled wool cap) | 'ushanka' (fur, the ear flaps
              tied up; 'ribbon': material of a ribbon across its front)
              as well as the soldier's
    chinstraps  bool: an old helmet's straps hanging loose
    beard     'full' | 'trimmed' (with a moustache; the humanoid beard too)
    jacket    {'collar': bool, 'zip': bool, 'hem': bool, 'buttons': n}
    rolled    bool: short sleeves rolled at the elbow (forearms bare)
    rig       'chicom' (three magazine pouches across the chest, grenade
              pouches at the sides, straps crossed behind; material 'rig')
    mg_belt   bool: a machine-gun belt over the left shoulder
    knees     bool: knee patches on the trousers
    sneakers  bool: trainers (white soles 'sole', a side stripe)
    kameez    skirt length (m) of a long shirt split at the sides (material 'top')
    shalwar   bool: baggy trousers gathered at the ankle (material 'trousers')
    sandals   bool: leather sandals over bare feet (set 'boots' to the skin color)
    patu      material of a wool shawl over the shoulders and down the back
    quilted   bool: the jacket, its skirt and sleeves quilted in channels
    skatka    material of a rolled greatcoat over the left shoulder
    bag       'bread' (a canvas bag at the right hip) | 'bottles' (a bag of
              bottles at the left hip)
    grenade   bool: a stick grenade in the belt
    boot_folds  bool: the creases of soft tall boots at the ankle
    shirt     {'collar': bool, 'pocket': bool, 'buttons': n} on the top
    waistcoat material of an open waistcoat (None: none)
    turnups   bool: turn-ups at the trouser bottoms
    belt_over bool: a belt with a buckle worn over the jacket or coat

    jeans, buckle, seams   jeans' seams, pockets and folds; a belt buckle; the
              jacket's seams and the cloth bunched over the belt
    sash      material of a waist sash knotted on the left hip
    kameez_folds  bool: folds down the kameez panels
    armband2, kerchief  materials of an armband (left arm) and a neckerchief
    sack, f1, shell_belt  a sack pack; an F1 grenade on the belt; a belt of shells
    run_hold  pose channels of the carry while running (the gun up at port)

    gun       also an irregulars_gear.GUNS name
    offhand   'molotov': the burning bottle in the left hand; ATTACK 'fire'
              throws it overarm, it bursts into flame where it lands, and he
              takes the next one from his bag

Every irregular dies its own way (anim_slump: struck, the knees go, he
slumps onto his left side, the gun held throughout) and runs with the gun
raised (anim_run_ready).
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
from models import irregulars_gear as IG
from models.props import mat

A = H.ANKLE_H
_v = FM._v


def _rings(p):
    """The torso's chest rings as the humanoid builds them (muscle included)."""
    bp = H.build_params(p)
    bulk = bp.get('bulk', 1.0)
    tw = H.TORSO_W * bulk
    lb = H.LIMB * bulk
    cr = [(0.0, 0.165, 0.105, 0, 0.0), (0.10, 0.19, 0.12, 0, 0.012), (0.19, 0.205, 0.122, 0, 0.008),
          (0.25, 0.185, 0.108, 0, -0.005), (0.30, 0.10, 0.07, 0, -0.01), (0.33, 0.05, 0.05, 0, -0.01)]
    cr = [(z, rx * tw, ry * (1 + (tw - 1) * 0.6), dx, dy) for z, rx, ry, dx, dy in cr]
    mus = H.muscle_amount(p)
    if mus > 0:
        cr = [(z, rx * (1 + 0.09 * mus * (0.10 <= z <= 0.26)), ry * (1 + 0.10 * mus * (0.05 <= z <= 0.26)),
               dx, dy + 0.012 * mus * (0.08 <= z <= 0.22)) for z, rx, ry, dx, dy in cr]
    return tw, lb, cr


def _ring_at(cr, z):
    """(rx, ry, dy) of the chest rings at height z (clamped)."""
    zs = [r[0] for r in cr]
    if z <= zs[0]:
        r = cr[0]
        return r[1], r[2], r[4]
    for a, b in zip(cr[:-1], cr[1:]):
        if a[0] <= z <= b[0]:
            u = (z - a[0]) / (b[0] - a[0])
            return a[1] + (b[1] - a[1]) * u, a[2] + (b[2] - a[2]) * u, a[4] + (b[4] - a[4]) * u
    r = cr[-1]
    return r[1], r[2], r[4]


def _on(cr, z, ang_deg, g=0.0):
    rx, ry, dy = _ring_at(cr, z)
    a = math.radians(ang_deg)
    return (rx + g) * math.cos(a), (ry + g) * math.sin(a) + dy, z


def _band(pts, r, m):
    return [M.capsule(a, b, r, r, m, seg=6, rings=1) for a, b in zip(pts[:-1], pts[1:])]


def _strip(a, b, w, t, m, up=(0, 0, 1)):
    """A flat strip (width w, thickness t) from a to b, its face turned to
    `up` as far as it can be."""
    a, b = _v(a), _v(b)
    d = b - a
    L = float(np.linalg.norm(d))
    z = d / max(L, 1e-6)
    upv = _v(up)
    x = np.cross(upv, z)
    if np.linalg.norm(x) < 1e-4:
        x = np.cross(_v((0, 1, 0)), z)
    x = x / np.linalg.norm(x)
    y = np.cross(z, x)
    T = np.eye(4, dtype=np.float32)
    T[:3, 0], T[:3, 1], T[:3, 2], T[:3, 3] = x, y, z, (a + b) / 2
    return M.rounded_box(w, t, L + t, m, (0, 0, 0), r=min(t, w) * 0.45, seg=4, p=4).transformed(T)


# ------------------------------------------------------------ the body

class IrregularBody(LF.LeagueBody):
    def parts(self):
        out = super().parts()
        p = self.full
        tw, lb, cr = _rings(p)

        def add(bone, mesh, tag=None):
            out.append((bone, mesh, tag))

        top = p.get('top')
        # -- under a sleeve the skin's shoulder ball would show through the
        # sleeve's top: leave it out (the sleeve covers the joint)
        if top in ('sleeved', 'long_sleeved'):
            skin = self.mats['skin']
            keep = []
            for bone, m, tag in out:
                if bone in ('arm_r', 'arm_l') and len(m) and (m.M == skin).all():
                    V = m.V.reshape(-1, 3)
                    if V[:, 2].max() > 0.05:          # the ball, centered at the joint
                        continue
                keep.append((bone, m, tag))
            out[:] = keep
        # -- arms: rolled sleeves
        if p.get('rolled'):
            tm = self.mm('top', '#888888')
            for side in 'rl':
                add(f'arm_{side}', M.capsule((0, 0, -0.12), (0, 0, -0.19), 0.084 * lb, 0.082 * lb, tm, seg=12, rings=1))
                add(f'arm_{side}', M.cylinder(0.086 * lb, 0.086 * lb, 0.03, tm, seg=12, z0=-0.17))
        # -- jacket details
        jk = p.get('jacket', {})
        if jk:
            jm = self.mm('top', '#555555')
            dk = self.mm('seam', '#26262a')
            if jk.get('collar'):
                add('chest', M.loft([(0.30, 0.105 * tw * 0.9, 0.085), (0.36, 0.088 * tw, 0.074), (0.39, 0.084 * tw, 0.072)],
                                    jm, seg=14, cap_bottom=False, cap_top=False).transformed(M.translate(0, -0.006, 0)))
            if jk.get('zip'):
                pts = [_on(cr, z, 90, 0.018) for z in (0.31, 0.22, 0.12, 0.02)]
                out.extend(('chest', m, None) for m in _band(pts, 0.007, dk))
            if jk.get('hem'):
                add('pelvis', M.loft([(0.02, 0.196 * tw, 0.146 * tw), (0.07, 0.19 * tw, 0.14 * tw), (0.12, 0.18 * tw, 0.132 * tw)],
                                     jm, seg=16, cap_bottom=False, cap_top=False))
                add('pelvis', M.cylinder(0.198 * tw, 0.198 * tw, 0.02, dk, seg=16, z0=0.015).transformed(M.scale(1, 0.75, 1)))
            nb = int(jk.get('buttons', 0))
            if nb:
                bm = self.mm('button', '#1c1a18', spec=0.4, gloss=20)
                for k in range(nb):
                    z = 0.30 - k * (0.30 / max(nb - 1, 1))
                    add('chest', M.sphere(0.013, bm, _on(cr, z, 90, 0.03), seg=6))
        # -- the shirt's collar, placket, pocket
        sh = p.get('shirt')
        if sh:
            tm = self.mm('top', '#ffffff')
            if sh.get('collar'):
                for sx in (1, -1):
                    # a collar point: a small flat wing turned down over the shirt front
                    add('chest', M.rounded_box(0.06, 0.012, 0.055, tm, (0, 0, 0), r=0.005, seg=4, p=4).transformed(
                        M.translate(sx * 0.045, 0.085, 0.325) @ M.rot_z(sx * 25) @ M.rot_x(-35) @ M.rot_y(sx * 30)))
                add('chest', M.loft([(0.31, 0.10 * tw * 0.85, 0.078), (0.355, 0.075 * tw, 0.068)], tm, seg=14,
                                    cap_bottom=False, cap_top=False))
            nb = int(sh.get('buttons', 0))
            if nb:
                pk = self.mm('placket', '#ffffff') if 'placket' in self.tint else tm
                pts = [_on(cr, z, 90, 0.016) for z in (0.30, 0.18, 0.06, -0.02)]
                out.extend(('chest', m, None) for m in _band(pts, 0.012, pk))
                bm = self.mm('button', '#e8e4da', spec=0.3, gloss=18)
                for k in range(nb):
                    z = 0.28 - k * 0.28 / max(nb - 1, 1)
                    add('chest', M.sphere(0.011, bm, _on(cr, z, 90, 0.03), seg=6))
            if sh.get('pocket'):
                x, y, z = _on(cr, 0.17, 118, 0.012)
                add('chest', M.rounded_box(0.075, 0.016, 0.075, tm, (x, y, z), r=0.006, seg=4, p=4).transformed(
                    M.translate(x, y, z) @ M.rot_z(28) @ M.translate(-x, -y, -z)))
        # -- an open waistcoat
        if p.get('waistcoat'):
            wm = self.mm(p['waistcoat'], '#3a3430')
            wd = self.mm('waistcoat_edge', '#24201c')
            g = 0.03
            rings = [(-0.10, cr[0][1] + g + 0.01, cr[0][2] + g + 0.01, 0, 0)] + \
                    [(z, rx + g, ry + g, dx, dy) for z, rx, ry, dx, dy in cr[:4]]
            for a0, a1 in ((math.radians(-72), math.radians(76)), (math.radians(104), math.radians(252))):
                add('chest', H._shell(rings, wm, a0, a1, seg=12))
            # the back, over the spine
            add('chest', H._shell(rings[:3], wm, math.radians(240), math.radians(300), seg=6))
            add('spine', M.loft([(0.0, 0.168 * tw + g, 0.12 * tw + g), (0.20, 0.18 * tw + g, 0.126 * tw + g)], wm,
                                seg=14, cap_bottom=False, cap_top=False))
            # its edges, pockets and buttons
            for sx, ang in ((1, 76), (-1, 104)):
                pts = [_on(cr, z, ang, g + 0.004) for z in (-0.10, 0.06, 0.18, 0.25)]
                pts.append((sx * 0.10, 0.03, 0.34))
                out.extend(('chest', m, None) for m in _band(pts, 0.01, wd))
                x, y, z = _on(cr, 0.02, 90 - sx * 40, g + 0.006)
                add('chest', M.rounded_box(0.07, 0.012, 0.012, wd, (x, y, z), r=0.005, seg=4, p=4).transformed(
                    M.translate(x, y, z) @ M.rot_z(-sx * 40) @ M.translate(-x, -y, -z)))
            # the back's cinch strap and buckle
            for sx in (1, -1):
                add('chest', _strip(_on(cr, 0.03, 270 + sx * 38, g + 0.004), _on(cr, 0.03, 270 + sx * 6, g + 0.006),
                                    0.03, 0.008, wd, up=(0, -1, 0)))
            add('chest', M.box(0.03, 0.012, 0.035, self.mm('buckle', '#8a8478', spec=0.7, gloss=26, metal=True),
                               _on(cr, 0.03, 270, g + 0.01)))
            bm = self.mm('wbutton', '#8a7a58', spec=0.6, gloss=24, metal=True)
            for z in (0.20, 0.10, 0.0, -0.08):
                add('chest', M.sphere(0.012, bm, _on(cr, z, 77, g + 0.006), seg=6))
        # -- the quilted jacket
        if p.get('quilted'):
            qm = self.mm('top', '#5a5a4a')
            cm = self.mm('coat', '#5a5a4a') if p.get('coat') else qm
            for ang in range(0, 360, 24):
                if 80 <= ang <= 100:
                    continue
                pts = [_on(cr, z, ang, 0.018) for z in (-0.02, 0.10, 0.19, 0.25)]
                out.extend(('chest', m, None) for m in _band(pts, 0.013, qm))
                a = math.radians(ang)
                for bone, z0, z1, rx, ry in (('spine', -0.02, 0.20, 0.168 * tw + 0.018, 0.12 * tw + 0.018),):
                    add(bone, M.capsule((rx * math.cos(a), ry * math.sin(a), z0), (rx * math.cos(a), ry * math.sin(a), z1),
                                        0.013, 0.013, qm, seg=6, rings=1))
            if p.get('coat'):
                low = -float(p['coat'])
                fl = 0.03 + 0.08 * float(p['coat'])
                for ang in range(0, 360, 24):
                    if 80 <= ang <= 100:
                        continue
                    a = math.radians(ang)
                    r0 = ((0.19 + fl) * tw, (0.15 + fl) * (1 + (tw - 1) * 0.8))
                    r1 = (0.186 * tw, 0.134 * (1 + (tw - 1) * 0.8))
                    add('pelvis', M.capsule((r0[0] * math.cos(a) * 1.02, r0[1] * math.sin(a) * 1.02, low + 0.03),
                                            (r1[0] * math.cos(a) * 1.03, r1[1] * math.sin(a) * 1.03, 0.0), 0.012, 0.012,
                                            cm, seg=6, rings=1))
            for side in 'rl':
                for z in (-0.05, -0.11, -0.16):
                    add(f'arm_{side}', M.cylinder(0.079 * lb, 0.075 * lb, 0.014, qm, seg=12, z0=z))
                for z in (-0.05, -0.11, -0.17):
                    add(f'forearm_{side}', M.cylinder(0.064 * lb, 0.06 * lb, 0.014, qm, seg=12, z0=z))
                add(f'forearm_{side}', M.cylinder(0.064 * lb, 0.066 * lb, 0.04, qm, seg=12, z0=-0.225))   # cuff
            add('chest', M.loft([(0.29, 0.11 * tw, 0.088), (0.35, 0.09 * tw, 0.078), (0.38, 0.088 * tw, 0.076)],
                                qm, seg=14, cap_bottom=False, cap_top=False))
        # -- a belt over the coat
        if p.get('belt_over'):
            bt = self.mm('belt', '#2a2018')
            k = 1.0 + (0.06 if p.get('coat') else 0.02)
            add('pelvis', M.loft([(0.05, 0.193 * tw * k, 0.142 * tw * k), (0.11, 0.186 * tw * k, 0.137 * tw * k)], bt,
                                 seg=18, cap_bottom=False, cap_top=False))
            bk = self.mm('buckle', '#8a8478', spec=0.7, gloss=26, metal=True)
            add('pelvis', M.rounded_box(0.06, 0.02, 0.055, bk, (0, 0.138 * tw * k + 0.012, 0.08), r=0.008, seg=4, p=4))
        # -- trousers: knee patches, turn-ups
        if p.get('knees'):
            km = self.mm('kneepad', '#2e2e2a')
            for side in 'rl':
                add(f'shin_{side}', M.rounded_box(0.10, 0.04, 0.11, km, (0, 0.06 * lb, -0.01), r=0.018, seg=6, p=3))
        if p.get('knee_patch'):
            km = self.mm('patch', '#7a6a52')
            add('shin_l', M.rounded_box(0.09, 0.03, 0.10, km, (0.0, 0.066 * lb, -0.02), r=0.008, seg=4, p=4).transformed(
                M.rot_z(8)))
            add('thigh_r', M.rounded_box(0.07, 0.022, 0.08, km, (0.0, -0.088 * lb, -0.04), r=0.008, seg=4, p=4))
        if p.get('turnups'):
            tm = self.mm('trousers', '#555555')
            for side in 'rl':
                add(f'shin_{side}', M.cylinder(0.072 * lb, 0.074 * lb, 0.035, tm, seg=12, z0=-0.355))
        if p.get('boot_folds'):
            bm = self.mm('boots', '#2a2420')
            for side in 'rl':
                for z, r in ((-0.33, 0.071), (-0.27, 0.075), (-0.21, 0.077)):
                    add(f'shin_{side}', M.cylinder(r * lb, r * lb, 0.022, bm, seg=12, z0=z))
        # -- trainers
        if p.get('sneakers'):
            so = self.mm('sole', '#e4e2dc')
            st = self.mm('stripe', '#d8d6d0')
            for side, sx in (('r', 1), ('l', -1)):
                add(f'foot_{side}', M.rounded_box(0.12 * H.FOOT, 0.265 * H.FOOT, 0.03, so, (0, 0.06, -0.08), r=0.012, seg=8, p=3))
                add(f'foot_{side}', M.rounded_box(0.115 * H.FOOT, 0.07, 0.04, so, (0, 0.058 + 0.12 * H.FOOT, -0.06),
                                                  r=0.015, seg=6, p=3))
                for s2 in (1, -1):
                    add(f'foot_{side}', _strip((s2 * 0.058 * H.FOOT, -0.04, -0.07), (s2 * 0.058 * H.FOOT, 0.10, -0.02),
                                               0.016, 0.006, st, up=(s2, 0, 0)))
        # -- sandals over bare feet
        if p.get('sandals'):
            lm = self.mm('sandal', '#4a3220')
            for side in 'rl':
                add(f'foot_{side}', M.rounded_box(0.125 * H.FOOT, 0.27 * H.FOOT, 0.025, lm, (0, 0.058, -0.085), r=0.01, seg=8, p=3))
                for yy in (0.0, 0.09):
                    add(f'foot_{side}', M.cylinder(0.066 * H.FOOT, 0.066 * H.FOOT, 0.02, lm, seg=10, z0=-0.05).transformed(
                        M.translate(0, yy + 0.04, 0) @ M.scale(1.0, 0.6, 1.0)))
        # -- shalwar
        if p.get('shalwar'):
            sm = self.mm('trousers', '#b0a890')
            add('pelvis', M.loft([(-0.18, 0.21 * tw, 0.155 * tw), (0.0, 0.198 * tw, 0.145 * tw), (0.10, 0.18 * tw, 0.13 * tw)],
                                 sm, seg=16, cap_bottom=True, cap_top=False))
            for side in 'rl':
                add(f'thigh_{side}', M.capsule((0, 0.01, 0.0), (0, 0.01, -H.THIGH + 0.02), 0.13 * lb, 0.105 * lb, sm, seg=12, rings=2))
                add(f'shin_{side}', M.loft([(-0.34, 0.06 * lb, 0.062 * lb), (-0.28, 0.084 * lb, 0.088 * lb, 0, 0.006),
                                            (-0.16, 0.096 * lb, 0.10 * lb, 0, 0.01), (-0.02, 0.098 * lb, 0.10 * lb, 0, 0.006),
                                            (0.04, 0.09 * lb, 0.09 * lb)], sm, seg=12, cap_bottom=True, cap_top=True))
                # folds round the shin, gathered toward the ankle
                for z, r in ((-0.30, 0.08), (-0.25, 0.091), (-0.20, 0.098)):
                    add(f'shin_{side}', M.cylinder(r * lb, r * lb * 0.97, 0.018, sm, seg=12, z0=z))
                add(f'shin_{side}', M.cylinder(0.064 * lb, 0.064 * lb, 0.03, self.mm('cuff', '#9a927a'), seg=12, z0=-0.37))
        # -- kameez: the waist all round, the skirt in four panels split at the sides
        km = p.get('kameez')
        if km:
            tm = self.mm('top', '#d8d0b8')
            low = -float(km)
            sx_ = tw
            add('pelvis', M.loft([(-0.06, 0.232 * sx_, 0.178 * sx_), (0.04, 0.21 * sx_, 0.16 * sx_), (0.12, 0.19 * sx_, 0.14 * sx_)],
                                 tm, seg=18, cap_bottom=False, cap_top=False))
            rings = [(low, 0.262 * sx_, 0.215 * sx_), ((low - 0.06) * 0.5, 0.25 * sx_, 0.198 * sx_), (-0.06, 0.232 * sx_, 0.178 * sx_)]
            hem = self.mm('hem', '#c4bca2')
            for side, off in (('r', 0.11), ('l', -0.11)):
                for a0, a1 in (((14, 90) if side == 'r' else (90, 166)), ((270, 346) if side == 'r' else (194, 270))):
                    sh_ = H._shell(rings, tm, math.radians(a0), math.radians(a1), seg=8)
                    sh_ = sh_.transformed(M.translate(-off, 0, 0.05))
                    add(f'thigh_{side}', sh_)
                    hm_ = H._shell([(low, 0.264 * sx_, 0.217 * sx_), (low + 0.03, 0.263 * sx_, 0.216 * sx_)], hem,
                                   math.radians(a0), math.radians(a1), seg=8)
                    add(f'thigh_{side}', hm_.transformed(M.translate(-off, 0, 0.05)))
        # -- tonal patches (wear, dirt, sun-fading) on the kameez panels and the shalwar
        tonal = p.get('tonal', {})
        rng = np.random.default_rng(23)

        def blobs(bone, garment, n, pts_fn):
            mids = [self.mm(f'tonal_{garment}_{i}', c, spec=0.04) for i, c in enumerate(tonal[garment])]
            for i in range(n):
                (x, y, z), nrm = pts_fn(rng)
                sz = 0.03 * rng.uniform(0.7, 1.4)
                add(bone, M.ellipsoid(0.005, sz * rng.uniform(1.0, 1.8), sz * rng.uniform(0.6, 1.1), mids[i % len(mids)],
                                      seg=7).transformed(M.translate(x, y, z) @ M.rot_z(nrm) @ M.rot_x(rng.uniform(-40, 40))))
        if 'kameez' in tonal and km:
            low = -float(km)
            for side, off, angs in (('r', 0.11, (20, 85, 275, 340)), ('l', -0.11, (95, 160, 200, 265))):
                def fn(r, off=off, angs=angs):
                    a = math.radians(r.uniform(angs[0], angs[1]) if r.uniform() < 0.5 else r.uniform(angs[2], angs[3]))
                    u = r.uniform(0, 1)
                    z = low + 0.04 + (-0.08 - low) * u
                    rx = (0.262 - 0.03 * u) * tw + 0.004
                    ry = (0.215 - 0.037 * u) * tw + 0.004
                    return (rx * math.cos(a) - off, ry * math.sin(a), z + 0.05), math.degrees(math.atan2(math.sin(a) / ry, math.cos(a) / rx))
                blobs(f'thigh_{side}', 'kameez', 14, fn)
        if 'shalwar' in tonal and p.get('shalwar'):
            for side in 'rl':
                def fn(r):
                    a = r.uniform(0, 2 * math.pi)
                    z = r.uniform(-0.30, -0.06)
                    rr = 0.10 * lb + 0.004
                    return (rr * math.cos(a), rr * math.sin(a), z), math.degrees(a)
                blobs(f'shin_{side}', 'shalwar', 7, fn)
        # -- patu shawl over the shoulders and down the back
        if p.get('patu'):
            pm = self.mm(p['patu'], '#8a7a62')
            pb = self.mm('patu_border', '#3a3026')
            g = 0.03
            top_rings = [(0.16, cr[2][1] + g, cr[2][2] + g, 0, cr[2][4]), (0.25, cr[3][1] + g, cr[3][2] + g, 0, cr[3][4]),
                         (0.30, cr[4][1] + g + 0.03, cr[4][2] + g + 0.015, 0, cr[4][4]),
                         (0.335, cr[5][1] + g + 0.03, cr[5][2] + g + 0.025, 0, cr[5][4])]
            add('chest', H._shell(top_rings, pm, math.radians(112), math.radians(428), seg=18))
            back = [(-0.30, cr[0][1] + 0.07, cr[0][2] + 0.075), (-0.10, cr[0][1] + 0.05, cr[0][2] + 0.06),
                    (0.08, cr[1][1] + g, cr[1][2] + g, 0, cr[1][4]), (0.17, cr[2][1] + g, cr[2][2] + g, 0, cr[2][4])]
            add('chest', H._shell(back, pm, math.radians(198), math.radians(342), seg=14))
            # the border stripes along the back's hem and the folds down it
            add('chest', H._shell([(-0.29, cr[0][1] + 0.074, cr[0][2] + 0.079), (-0.25, cr[0][1] + 0.07, cr[0][2] + 0.076)],
                                  pb, math.radians(198), math.radians(342), seg=14))
            for ang in (215, 240, 262, 285, 305, 328):
                a = math.radians(ang)
                pts = [((cr[0][1] + 0.07) * math.cos(a), (cr[0][2] + 0.074) * math.sin(a), -0.215),
                       ((cr[0][1] + 0.06) * math.cos(a), (cr[0][2] + 0.066) * math.sin(a), -0.10),
                       ((cr[1][1] + g + 0.006) * math.cos(a), (cr[1][2] + g + 0.006) * math.sin(a), 0.10)]
                out.extend(('chest', m, None) for m in _band(pts, 0.02, pm))
            # a woven stripe across the back above the border
            add('chest', H._shell([(-0.20, cr[0][1] + 0.067, cr[0][2] + 0.073), (-0.185, cr[0][1] + 0.066, cr[0][2] + 0.072)],
                                  pb, math.radians(198), math.radians(342), seg=14))
            # the two ends hanging down the chest
            for sx in (1, -1):
                x = sx * cr[2][1] * 0.62
                a = _on(cr, 0.27, 90 - sx * 50, g)
                b = (x, _on(cr, 0.10, 90 - sx * 52, g)[1], 0.08)
                c = (x * 1.02, _on(cr, 0.0, 90 - sx * 54, g)[1] + 0.02, -0.22)
                add('chest', _strip(a, b, 0.13, 0.025, pm, up=(0, 1, 0)))
                add('chest', _strip(b, c, 0.13, 0.025, pm, up=(0, 1, 0)))
                add('chest', _strip((c[0], c[1] + 0.003, c[2] + 0.03), (c[0], c[1] + 0.003, c[2] - 0.01), 0.135, 0.026, pb,
                                    up=(0, 1, 0)))
                for k in range(4):
                    fx = c[0] + (k - 1.5) * 0.03
                    add('chest', M.capsule((fx, c[1], c[2] - 0.01), (fx, c[1] + 0.005, c[2] - 0.04), 0.007, 0.005, pb,
                                           seg=4, rings=1))
        # -- the chicom chest rig
        if p.get('rig') == 'chicom':
            rm = self.mm('rig', '#7e7656')
            rd = self.mm('rig_dark', '#5e5840')
            g = 0.03 + (0.03 if p.get('vest') else 0.0)
            z0 = 0.06
            x0, y0, _ = _on(cr, z0, 90, g)
            add('chest', M.rounded_box(0.36, 0.022, 0.17, rm, (0, y0 - 0.004, z0), r=0.01, seg=6, p=4))
            for k, x in enumerate((-0.085, 0.0, 0.085)):
                add('chest', M.rounded_box(0.074, 0.05, 0.15, rm, (x, y0 + 0.026, z0 - 0.005), r=0.014, seg=6, p=3.5))
                add('chest', M.rounded_box(0.078, 0.054, 0.045, rd, (x, y0 + 0.028, z0 + 0.06), r=0.012, seg=6, p=3.5))
                add('chest', M.cylinder(0.008, 0.008, 0.02, self.mm('toggle', '#3a2a1c'), seg=6, z0=-0.01).transformed(
                    M.translate(x, y0 + 0.057, z0 + 0.045) @ M.rot_x(90)))
            for sx in (1, -1):
                add('chest', M.rounded_box(0.06, 0.05, 0.08, rm, (sx * 0.175, y0 - 0.01, z0 - 0.03), r=0.014, seg=6, p=3.5)
                    .transformed(M.translate(sx * 0.175, y0 - 0.01, z0) @ M.rot_z(sx * 28) @ M.translate(-sx * 0.175, -(y0 - 0.01), -z0)))
                # straps over the shoulders, crossing behind
                pts = [(sx * 0.14, y0, z0 + 0.08), _on(cr, 0.22, 90 - sx * 42, g * 0.6), (sx * 0.13, 0.0, 0.345),
                       _on(cr, 0.22, 270 + sx * 40, 0.02), (-sx * 0.10, _on(cr, 0.06, 270, 0.02)[1], 0.06),
                       (-sx * 0.20, _on(cr, 0.0, 270 - sx * 40, 0.02)[1], 0.0)]
                out.extend(('chest', m, None) for m in _band(pts, 0.016, rd))
            # the waist strap round the back
            ry = cr[0][2] + 0.02
            add('chest', M.loft([(-0.01, cr[0][1] + 0.022, ry), (0.02, cr[0][1] + 0.022, ry)], rd, seg=16,
                                cap_bottom=False, cap_top=False))
        # -- a machine-gun belt over the left shoulder to the right hip
        if p.get('mg_belt'):
            g = 0.035 + (0.03 if p.get('rig') else 0.0)
            front = [_on(cr, 0.31, 120, 0.02), _on(cr, 0.24, 110, g), _on(cr, 0.14, 80, g), _on(cr, 0.04, 40, g),
                     _on(cr, -0.04, 15, g)]
            front = [(-0.13, 0.0, 0.355)] + front
            backp = [(-0.13, 0.0, 0.355), _on(cr, 0.28, 235, 0.03), _on(cr, 0.16, 260, 0.03), _on(cr, 0.05, 300, 0.03),
                     _on(cr, -0.04, 340, 0.03)]
            add('chest', IG.belt_links(self.mats, front, r=0.026, every=0.032))
            add('chest', IG.belt_links(self.mats, backp, r=0.026, every=0.04))
        # -- the rolled greatcoat
        if p.get('skatka'):
            sk = self.mm(p['skatka'], '#6a6a5e')
            tie = self.mm('tie', '#3a3226')
            g = 0.05 + (0.02 if p.get('quilted') else 0.0)
            front = [(-0.16, 0.0, 0.37), _on(cr, 0.30, 130, g), _on(cr, 0.20, 105, g), _on(cr, 0.08, 62, g),
                     _on(cr, -0.04, 28, g), (cr[0][1] + g * 0.9, 0.0, -0.10)]
            backp = [(-0.16, 0.0, 0.37), _on(cr, 0.28, 228, g), _on(cr, 0.16, 258, g), _on(cr, 0.04, 300, g),
                     _on(cr, -0.05, 334, g), (cr[0][1] + g * 0.9, 0.0, -0.10)]
            for pts in (front, backp):
                out.extend(('chest', m, None) for m in _band(pts, 0.05, sk))
            for pts, i in ((front, 2), (front, 4), (backp, 2)):
                a, b = _v(pts[i - 1]), _v(pts[i])
                c = (a + b) / 2
                d = (b - a) / np.linalg.norm(b - a)
                add('chest', M.capsule(tuple(c - d * 0.012), tuple(c + d * 0.012), 0.056, 0.056, tie, seg=10, rings=1))
        # -- jeans: outer seams, back pockets, stacked folds at the ankle, a belt with a buckle
        if p.get('jeans'):
            sm = self.mm('seam_light', '#8a8270')
            tm = self.mm('trousers', '#555555')
            for side, sx in (('r', 1), ('l', -1)):
                add(f'thigh_{side}', M.capsule((sx * 0.094 * lb, 0.0, -0.02), (sx * 0.076 * lb, 0.0, -0.38), 0.006, 0.006,
                                               sm, seg=4, rings=1))
                add(f'shin_{side}', M.capsule((sx * 0.072 * lb, 0.0, -0.02), (sx * 0.063 * lb, 0.0, -0.30), 0.006, 0.006,
                                              sm, seg=4, rings=1))
                for z, r in ((-0.31, 0.068), (-0.27, 0.071)):
                    add(f'shin_{side}', M.cylinder(r * lb, r * lb, 0.02, tm, seg=12, z0=z))
                add('pelvis', M.rounded_box(0.09, 0.016, 0.09, tm, (sx * 0.09 * tw, -0.132 * tw - 0.004, -0.02), r=0.008,
                                            seg=4, p=4))
                add('pelvis', _strip((sx * 0.135 * tw, -0.13 * tw - 0.012, 0.025), (sx * 0.05 * tw, -0.135 * tw - 0.012, 0.025),
                                     0.01, 0.004, sm, up=(0, -1, 0)))
        if p.get('buckle'):
            bk = self.mm('buckle', '#9a9488', spec=0.7, gloss=26, metal=True)
            add('pelvis', M.rounded_box(0.06, 0.02, 0.05, bk, (0, 0.13 * tw + 0.012, 0.09), r=0.008, seg=4, p=4))
            for sx in (1, -1):
                add('pelvis', M.box(0.012, 0.02, 0.07, self.mm('belt', '#2a2018'), (sx * 0.11 * tw, 0.12 * tw + 0.012, 0.07)))
        # -- the jacket's seams and the cloth bunched over the belt
        if p.get('seams'):
            dk = self.mm('seam', '#26262a')
            jm = self.mm('top', '#555555')
            for sx in (1, -1):
                pts = [_on(cr, z, 180 if sx < 0 else 0, 0.016) for z in (-0.04, 0.10, 0.20)]
                out.extend(('chest', m, None) for m in _band(pts, 0.006, dk))
                pts = [_on(cr, 0.24, 270 + sx * 15, 0.018), _on(cr, 0.22, 270 + sx * 60, 0.018)]
                out.extend(('chest', m, None) for m in _band(pts, 0.006, dk))
            for ang in (60, 120, 240, 300):
                a = math.radians(ang)
                add('spine', M.ellipsoid(0.05, 0.02, 0.03, jm, (0.18 * tw * math.cos(a), 0.13 * tw * math.sin(a), 0.03), seg=8)
                    .transformed(M.translate(0.18 * tw * math.cos(a), 0.13 * tw * math.sin(a), 0.03) @ M.rot_z(ang + 90)
                                 @ M.translate(-0.18 * tw * math.cos(a), -0.13 * tw * math.sin(a), -0.03)))
        # -- a sash round the waist, knotted on the left hip, its ends hanging
        if p.get('sash'):
            sm = self.mm(p['sash'], '#a03028')
            k = 1.0 + (0.17 if p.get('kameez') else 0.02)
            add('pelvis', M.loft([(0.02, 0.193 * tw * k, 0.142 * tw * k), (0.06, 0.197 * tw * k, 0.146 * tw * k),
                                  (0.10, 0.19 * tw * k, 0.14 * tw * k), (0.13, 0.182 * tw * k, 0.134 * tw * k)], sm,
                                 seg=18, cap_bottom=False, cap_top=False))
            x = -0.17 * tw * k
            y = 0.08 * tw * k
            add('pelvis', M.ellipsoid(0.04, 0.035, 0.035, sm, (x, y, 0.07), seg=8))
            add('pelvis', _strip((x, y + 0.02, 0.06), (x - 0.03, y + 0.03, -0.14), 0.05, 0.012, sm, up=(-0.5, 1, 0)))
            add('pelvis', _strip((x + 0.02, y + 0.025, 0.05), (x + 0.03, y + 0.04, -0.10), 0.045, 0.012, sm, up=(-0.5, 1, 0)))
        # -- folds down the kameez panels
        if km and p.get('kameez_folds'):
            tm = self.mm('top', '#d8d0b8')
            low = -float(km)
            for side, off, angs in (('r', 0.11, (40, 70, 290, 320)), ('l', -0.11, (110, 140, 220, 250))):
                for ang in angs:
                    a = math.radians(ang)
                    pts = [((0.262 * tw + 0.002) * math.cos(a) - off, (0.215 * tw + 0.002) * math.sin(a), low + 0.06),
                           ((0.236 * tw + 0.002) * math.cos(a) - off, (0.182 * tw + 0.002) * math.sin(a), -0.02)]
                    out.extend((f'thigh_{side}', m, None) for m in _band(pts, 0.011, tm))
        # -- an armband round the left upper arm; a neckerchief knotted in front
        if p.get('armband2'):
            am = self.mm(p['armband2'], '#ffffff')
            add('arm_l', M.cylinder(0.083 * lb, 0.081 * lb, 0.07, am, seg=12, z0=-0.13))
        if p.get('kerchief'):
            km_ = self.mm(p['kerchief'], '#ffffff')
            add('chest', M.loft([(0.29, 0.125 * tw * 0.85, 0.09), (0.33, 0.10 * tw * 0.85, 0.08), (0.36, 0.08 * tw, 0.07)], km_,
                                seg=14, cap_bottom=False, cap_top=False))
            add('chest', M.ellipsoid(0.035, 0.03, 0.03, km_, (0.0, 0.095, 0.30), seg=8))
            add('chest', M.extrude([(-0.05, 0.0), (0.05, 0.0), (0.0, -0.09)], 0.012, km_).transformed(
                M.translate(0, 0.10, 0.29) @ M.rot_x(-12)))
            # its point hanging down the back
            add('chest', M.extrude([(-0.09, 0.0), (0.09, 0.0), (0.0, -0.13)], 0.012, km_).transformed(
                M.translate(0, -0.085, 0.31) @ M.rot_x(14)))
        # -- a sack pack (veshmeshok): a drawstring sack on the back with its straps
        if p.get('sack'):
            sm = self.mm('sack', '#8a8064')
            sd = self.mm('sack_dark', '#6a6248')
            g = 0.04 + (0.02 if p.get('quilted') else 0.0)
            yb = -(cr[1][2] + g)
            add('chest', M.ellipsoid(0.15, 0.09, 0.15, sm, (0, yb - 0.07, 0.08), seg=12), 'backpack')
            add('chest', M.ellipsoid(0.10, 0.06, 0.06, sm, (0, yb - 0.06, 0.22), seg=10), 'backpack')
            add('chest', M.cylinder(0.03, 0.05, 0.05, sd, seg=8, z0=0.24).transformed(M.translate(0, yb - 0.05, 0)), 'backpack')
            for sx in (1, -1):
                pts = [(sx * 0.03, yb - 0.04, 0.27), (sx * 0.11, -0.02, 0.35), _on(cr, 0.22, 90 - sx * 45, g),
                       _on(cr, 0.08, 90 - sx * 60, g), (sx * 0.14, yb - 0.02, -0.04)]
                out.extend(('chest', m, None) for m in _band(pts, 0.013, sd))
        # -- grenades on the belt: an F1 at the right front
        if p.get('f1'):
            gm = self.mm('grenade', '#4a5038', spec=0.3, gloss=16)
            k = 1.06 if p.get('coat') else 1.0
            x, y = 0.13 * tw, 0.13 * tw * k + 0.035
            add('pelvis', M.ellipsoid(0.035, 0.035, 0.045, gm, (x, y, 0.04), seg=10))
            for z in (0.02, 0.05):
                add('pelvis', M.cylinder(0.037, 0.037, 0.008, self.mm('grenade_dark', '#343a28'), seg=10, z0=z)
                    .transformed(M.translate(x, y, 0)))
            add('pelvis', M.cylinder(0.012, 0.012, 0.03, self.mm('buckle', '#9a9488', spec=0.7, gloss=26, metal=True),
                                     seg=6, z0=0.085).transformed(M.translate(x, y, 0)))
        # -- a cartridge belt of shotgun shells round the waist
        if p.get('shell_belt'):
            bt = self.mm('belt', '#2a2018')
            sh = self.mm('bottles', '#a83a2a', spec=0.2, gloss=14)
            br = self.mm('shell_brass', '#b8964a', spec=0.7, gloss=26, metal=True)
            add('pelvis', M.loft([(0.04, 0.192 * tw, 0.142 * tw), (0.12, 0.186 * tw, 0.137 * tw)], bt, seg=18,
                                 cap_bottom=False, cap_top=False))
            for ang in range(20, 170, 13):
                a_ = math.radians(ang)
                x, y = 0.195 * tw * math.cos(a_), 0.145 * tw * math.sin(a_)
                add('pelvis', M.cylinder(0.014, 0.014, 0.06, sh, seg=6, z0=0.06).transformed(M.translate(x, y, 0)))
                add('pelvis', M.cylinder(0.015, 0.015, 0.015, br, seg=6, z0=0.045).transformed(M.translate(x, y, 0)))
        # -- bags, grenade
        bag = p.get('bag')
        if bag == 'bottles':
            add('pelvis', IG.bottle_bag(self.mats).transformed(M.translate(-0.25 * tw * 0.98, 0.02, 0.0) @ M.rot_z(-8)))
            # its strap from the right shoulder
            sm = self.mm('bag_strap', '#5a4e36')
            g = 0.03 + (0.03 if p.get('waistcoat') else 0.0)
            pts = [(0.14, 0.0, 0.35), _on(cr, 0.24, 60, g), _on(cr, 0.12, 100, g), _on(cr, 0.0, 150, g),
                   (-cr[0][1] - 0.04, 0.02, -0.12)]
            out.extend(('chest', m, None) for m in _band(pts, 0.012, sm))
            pts = [(0.14, 0.0, 0.35), _on(cr, 0.24, 300, g), _on(cr, 0.10, 250, g), _on(cr, 0.0, 210, g),
                   (-cr[0][1] - 0.04, 0.0, -0.12)]
            out.extend(('chest', m, None) for m in _band(pts, 0.012, sm))
        elif bag == 'bread':
            bm = self.mm('bag', '#7a7058')
            bd = self.mm('bag_dark', '#5a5240')
            x = 0.23 * tw * (1.08 if p.get('coat') else 1.0)
            add('pelvis', M.rounded_box(0.07, 0.20, 0.17, bm, (x, -0.05, -0.08), r=0.03, seg=8, p=3.5).transformed(
                M.translate(x, -0.05, 0) @ M.rot_z(12) @ M.translate(-x, 0.05, 0)))
            add('pelvis', M.rounded_box(0.075, 0.205, 0.07, bd, (x + 0.003, -0.05, -0.02), r=0.02, seg=6, p=4).transformed(
                M.translate(x, -0.05, 0) @ M.rot_z(12) @ M.translate(-x, 0.05, 0)))
            sm = self.mm('bag_strap', '#4a4232')
            g = 0.05 + (0.02 if p.get('quilted') else 0.0)
            pts = [(-0.13, 0.0, 0.36), _on(cr, 0.26, 118, g), _on(cr, 0.12, 70, g), _on(cr, -0.02, 20, g),
                   (cr[0][1] + 0.02, -0.04, -0.12)]
            out.extend(('chest', m, None) for m in _band(pts, 0.012, sm))
        if p.get('grenade'):
            gm = self.mm('grenade', '#4a5038', spec=0.3, gloss=16)
            hw = self.mm('stock', '#6a4426')
            k = 1.06 if p.get('coat') else 1.0
            x, y = -0.19 * tw, 0.10 * tw * k + 0.03
            add('pelvis', M.cylinder(0.04, 0.04, 0.11, gm, seg=10, z0=0.10).transformed(M.translate(x, y + 0.02, 0)))
            add('pelvis', M.cylinder(0.016, 0.016, 0.20, hw, seg=8, z0=-0.08).transformed(M.translate(x, y + 0.012, 0)))
            add('pelvis', M.cylinder(0.02, 0.02, 0.02, gm, seg=8, z0=-0.09).transformed(M.translate(x, y + 0.012, 0)))
        return out

    def _hat(self, add0):
        def add(mesh, tag='hat'):
            add0('head', mesh.transformed(M.scale(H.HEAD)), tag)
        p = self.full
        hat = p.get('hat')
        if hat == 'shemagh':
            hm = self.mm('hat', '#d8d0c0', spec=0.03)
            st = self.mm('hat_stripe', '#ece8de', spec=0.03)
            # the crown, the back and sides down to the neck
            add(M.ellipsoid(0.114, 0.124, 0.105, hm, (0, -0.012, 0.172), seg=16))
            add(M.ellipsoid(0.116, 0.112, 0.125, hm, (0, -0.032, 0.094), seg=16))
            add(M.cylinder(0.082, 0.09, 0.11, hm, seg=12, z0=-0.07))
            # the wound turns: tilted bands round the crown, each with its stripe
            for k, (z, tx, ty, r) in enumerate(((0.20, -6, 4, 0.12), (0.225, 10, -6, 0.11), (0.178, 2, -10, 0.122))):
                T = M.translate(0, -0.012, z) @ M.rot_x(tx) @ M.rot_y(ty)
                add(M.loft([(-0.016, r, r * 1.07), (0.0, r + 0.008, r * 1.07 + 0.008), (0.016, r, r * 1.07)], hm, seg=18,
                           cap_bottom=False, cap_top=False).transformed(T))
                if k < 2:
                    add(M.loft([(-0.004, r + 0.007, r * 1.07 + 0.007), (0.004, r + 0.007, r * 1.07 + 0.007)], st, seg=18,
                               cap_bottom=False, cap_top=False).transformed(T))
            # the veil over the nose and mouth, down over the throat
            # the loose end wound round the jaw and throat, the mouth and chin open
            add(M.loft([(-0.07, 0.085, 0.09), (-0.02, 0.095, 0.10, 0, -0.005), (0.03, 0.10, 0.10, 0, -0.012)], hm, seg=14,
                       cap_bottom=True, cap_top=False))
            add(M.ellipsoid(0.07, 0.05, 0.035, hm, (0, 0.06, -0.035), seg=10))
            # the check: dark stripes along the turns and across the crown
            for z in (0.125, 0.215):
                add(M.loft([(z - 0.005, 0.116, 0.126), (z + 0.005, 0.116, 0.126)], st, seg=18, cap_bottom=False,
                           cap_top=False).transformed(M.translate(0, -0.012, 0)))
            # the knot behind and the tail down the back
            add(M.ellipsoid(0.04, 0.035, 0.035, hm, (0.03, -0.135, 0.11), seg=10))
            pts = [(0.03, -0.14, 0.10), (0.05, -0.17, 0.0), (0.07, -0.19, -0.11), (0.08, -0.20, -0.20)]
            for i, (a, b) in enumerate(zip(pts[:-1], pts[1:])):
                add(_strip(a, b, 0.085 - 0.01 * i, 0.02, hm, up=(0, -1, 0)))
            add(_strip((0.08, -0.198, -0.19), (0.081, -0.2, -0.22), 0.066, 0.022, st, up=(0, -1, 0)))
            for i, (a, b) in enumerate(zip(pts[:-1], pts[1:])):
                add(_strip(a, b, 0.012, 0.024, st, up=(0, -1, 0)))
            for k in range(5):
                fx_ = 0.055 + 0.012 * k
                add(M.capsule((fx_, -0.20, -0.215), (fx_, -0.205, -0.25), 0.005, 0.004, hm, seg=4, rings=1))
        elif hat == 'pakol':
            hm = self.mm('hat', '#8a7c62', spec=0.03)
            hd = self.mm('hat_seam', '#6e624c', spec=0.03)
            # the rolled brim: a fat ring round the head
            add(M.loft([(0.105, 0.116, 0.128), (0.12, 0.134, 0.146), (0.15, 0.145, 0.157), (0.18, 0.14, 0.152),
                        (0.195, 0.124, 0.136)], hm, seg=20, cap_bottom=True, cap_top=False))
            # the flat top, wider than the roll's inside, a seam where they meet
            add(M.loft([(0.19, 0.132, 0.144), (0.215, 0.142, 0.154), (0.235, 0.138, 0.15), (0.245, 0.11, 0.12),
                        (0.248, 0.0, 0.0)], hm, seg=20, cap_bottom=False, cap_top=False))
            add(M.loft([(0.188, 0.135, 0.147), (0.198, 0.135, 0.147)], hd, seg=20, cap_bottom=False, cap_top=False))
            # the roll's lower edge, and a fold where it's rolled
            add(M.loft([(0.104, 0.118, 0.13), (0.112, 0.124, 0.136)], hd, seg=20, cap_bottom=False, cap_top=False))
        elif hat == 'ushanka':
            lift = M.translate(0, -0.012, 0.04) @ M.rot_x(-7)
            add0_ = add

            def add(mesh, tag='hat'):
                add0_(mesh.transformed(lift), tag)
            fm = self.mm('hat', '#5e4e3c', spec=0.02)
            fd = self.mm('fur_dark', '#46392c', spec=0.02)
            cm = self.mm('hat_top', '#3e3e36', spec=0.08)
            # the crown
            add(H._dome(0.118, 0.128, 0.11, cm, (0, -0.008, 0.17), 0.0))
            # the fur band round it: tufted, two rings
            add(M.loft([(0.10, 0.128, 0.14), (0.12, 0.145, 0.157), (0.17, 0.15, 0.162), (0.20, 0.142, 0.154),
                        (0.21, 0.13, 0.142)], fm, seg=20, cap_bottom=True, cap_top=False))
            # tufts along the band's top edge and the flap's edge (the fur's nap)
            rng = np.random.default_rng(11)
            for k in range(26):
                a = 2 * math.pi * k / 26 + rng.uniform(-0.05, 0.05)
                add(M.ellipsoid(0.026, 0.026, 0.02, fm if k % 3 else fd,
                                (0.142 * math.cos(a), 0.152 * math.sin(a) - 0.008, 0.20 + rng.uniform(-0.006, 0.006)), seg=6))
            # the front flap turned up
            add(M.rounded_box(0.22, 0.05, 0.09, fm, (0, 0.0, 0.04), r=0.024, seg=8, p=3).transformed(
                M.translate(0, 0.128, 0.165) @ M.rot_x(-12)))
            add(M.rounded_box(0.20, 0.02, 0.012, fd, (0, 0.0, 0.0), r=0.006, seg=4, p=4).transformed(
                M.translate(0, 0.15, 0.168) @ M.rot_x(-12)))
            # the ear flaps folded up the sides against the crown and tied over it
            for sx in (1, -1):
                add(M.rounded_box(0.04, 0.14, 0.075, fm, (0, 0, 0.03), r=0.02, seg=8, p=3).transformed(
                    M.translate(sx * 0.132, -0.025, 0.17) @ M.rot_y(sx * 30)))
            tie = self.mm('tie', '#2a2218')
            add(M.capsule((0.11, -0.025, 0.235), (0.0, -0.02, 0.283), 0.008, 0.008, tie, seg=4, rings=1))
            add(M.capsule((-0.11, -0.025, 0.235), (0.0, -0.02, 0.283), 0.008, 0.008, tie, seg=4, rings=1))
            add(M.sphere(0.014, tie, (0.0, -0.02, 0.286), seg=6))
            rb = p.get('ribbon')
            if rb:
                rm = self.mm(rb, '#c03028')
                add(M.rounded_box(0.23, 0.014, 0.034, rm, (0, 0, 0), r=0.006, seg=4, p=4).transformed(
                    M.translate(0, 0.158, 0.205) @ M.rot_x(-12) @ M.rot_y(-28)))
        else:
            super()._hat(add0)
        if p.get('chinstraps'):
            sm = self.mm('chinstrap', '#3a3428')
            for sx in (1, -1):
                add(M.capsule((sx * 0.128, 0.0, 0.1), (sx * 0.12, 0.03, 0.0), 0.011, 0.011, sm, seg=4, rings=1))
                add(M.capsule((sx * 0.12, 0.03, 0.0), (sx * 0.10, 0.02, -0.05), 0.011, 0.011, sm, seg=4, rings=1))
                add(M.box(0.018, 0.024, 0.02, self.mm('buckle', '#8a8478', spec=0.7, gloss=26, metal=True),
                          (sx * 0.10, 0.02, -0.055)))
        bd = p.get('beard_style')
        if bd:
            hm = self.mm('hair', '#2a2018')
            full = bd == 'full'
            # moustache, cheeks and chin under the humanoid's beard
            # a moustache under the nose, the beard along the jaw, the chin's tuft
            # below the mouth (the lips show between them)
            add(M.capsule((-0.045, 0.112, 0.078), (0.0, 0.122, 0.082), 0.013, 0.011, hm, seg=6, rings=1), None)
            add(M.capsule((0.045, 0.112, 0.078), (0.0, 0.122, 0.082), 0.013, 0.011, hm, seg=6, rings=1), None)
            add(M.ellipsoid(0.026, 0.012, 0.009, self.mm('lips', '#9a5a40'), (0, 0.118, 0.058), seg=6), None)
            for sx in (1, -1):
                add(M.ellipsoid(0.026, 0.06, 0.07 if full else 0.05, hm, (sx * 0.072, 0.04, 0.06), seg=8), None)
            add(M.ellipsoid(0.07 if full else 0.055, 0.055, 0.055 if full else 0.04, hm,
                            (0, 0.08, -0.005 if full else 0.01), seg=10), None)
            if full:
                add(M.ellipsoid(0.045, 0.04, 0.035, hm, (0, 0.085, -0.06), seg=8), None)


# ------------------------------------------------------------ the soldier

def _glow(pos, rad, col, alpha=1.0):
    return {'kind': 'glow', 'pos': _v(pos), 'rad': rad, 'col': col, 'alpha': alpha}


@archetype('irregular')
class Irregular(LF.LeagueSoldier):
    def __init__(self, unit, params):
        super().__init__(unit, params)
        bp = dict(params)
        bp['weapon'] = params.get('weapon', params.get('gun'))
        bp['offhand'] = params.get('offhand')
        self.body = IrregularBody(bp)
        self.mats = self.body.mats
        self.parts = self.body.parts()
        self._props = {}
        if self.gun in IG.GUNS:
            self.gspec = IG.GUNS[self.gun]
        self.thrower = params.get('offhand') == 'molotov'

    def prop(self, name, **kw):
        if name not in IG.GUNS:
            return super().prop(name, **kw)
        key = (name, tuple(sorted(kw.items())))
        if key not in self._props:
            m = getattr(IG, name)(self.mats, **kw)
            if name != 'molotov':
                k = float(self.p.get('gun_thick', 1.6))
                m = m.transformed(M.scale(k, k, 1.0))
            self._props[key] = m
        return self._props[key]

    def action(self, action, ctx):
        name = self.anim_name(action)
        if name not in ('death', 'run') and not (self.thrower and name == 'fire'):
            return super().action(action, ctx)
        key = (action, ctx.n if ctx else None)
        if key not in self._cache:
            n = ctx.n if ctx else 15
            if name == 'death':
                self._cache[key] = self.anim_slump(ctx)
            elif name == 'run':
                self._cache[key] = self.anim_run_ready(n)
            else:
                self._cache[key] = self.anim_throw(ctx)
        return self._cache[key]

    # -- running: the gun held up at port (seen from behind too)
    def anim_run_ready(self, n):
        B = dict(self.base)
        B.update({k: _v(v) for k, v in self.p.get('run_hold', {}).items()})
        base = H.anim_run(B, 'spear', 'shield_round', n)

        def fn(t):
            p = base.fn(t)
            s = math.sin(2 * math.pi * t)
            p['hand_r'] = B['hand_r'] + _v((0, -0.03 * s, 0.03 + 0.02 * abs(s)))
            p['hand_l'] = B['hand_l'] + _v((0, 0.03 * s, 0.03))
            p['follow'] = _v(1.0)
            return p
        return FM.Act(fn, loop=True)

    # -- dying: struck, the knees go, he slumps onto his left side, the gun still in his hands
    def anim_slump(self, ctx):
        B = self.base
        down = ctx.event('down', 0.6) if ctx is not None else 0.6
        down = min(max(down or 0.6, 0.4), 0.85)
        fk = {'thigh_r': (0, 0, 0), 'shin_r': (0, 0, 0), 'thigh_l': (0, 0, 0), 'shin_l': (0, 0, 0),
              'root_rot': (0, 0, 0)}
        z = {c: B[c] for c in ('root_pos', 'chest', 'spine', 'head') if c in B}
        k = [
            (0.0, {}),
            (0.14, {'root_rot': (10, -4, 0), 'root_pos': (0, -0.06, -0.03), 'chest': (12, 0, -8), 'head': (16, 0, 0),
                    'thigh_r': (4, 0, 0), 'shin_r': (-6, 0, 0)}),
            (0.34, {'root_rot': (-14, -8, 0), 'root_pos': (0, 0.02, -0.30), 'chest': (-12, 0, -6), 'head': (12, 0, 0),
                    'thigh_r': (45, 0, 0), 'shin_r': (-82, 0, 0), 'thigh_l': (32, 0, 0), 'shin_l': (-64, 0, 0)}),
            (0.55, {'root_rot': (-20, -24, 0), 'root_pos': (-0.04, 0.05, -0.47), 'chest': (-14, 0, -4),
                    'head': (14, -6, 0), 'thigh_r': (22, 0, 0), 'shin_r': (-98, 0, 0), 'thigh_l': (18, 0, 0),
                    'shin_l': (-96, 0, 0)}),
            (0.76, {'root_rot': (-12, -66, 0), 'root_pos': (-0.30, 0.05, -0.64), 'chest': (-6, 0, 6),
                    'head': (6, -14, 0), 'thigh_r': (48, 0, 0), 'shin_r': (-74, 0, 0), 'thigh_l': (36, 0, 0),
                    'shin_l': (-58, 0, 0), 'elbow_r': (0.2, -0.4, 1.0), 'elbow_l': (-0.4, -0.6, 0.6)}),
            (0.88, {'root_rot': (-6, -86, 0), 'root_pos': (-0.42, 0.05, -0.75), 'chest': (0, -4, 6),
                    'head': (4, -20, 0), 'thigh_r': (52, 0, 0), 'shin_r': (-70, 0, 0), 'thigh_l': (34, 0, 0),
                    'shin_l': (-56, 0, 0), 'elbow_r': (0.2, -0.4, 1.0), 'elbow_l': (-0.4, -0.6, 0.6)}),
            (1.0, {'root_rot': (-6, -88, 0), 'root_pos': (-0.43, 0.05, -0.76), 'chest': (0, -4, 8),
                   'head': (6, -24, 0), 'thigh_r': (54, 0, 0), 'shin_r': (-72, 0, 0), 'thigh_l': (34, 0, 0),
                   'shin_l': (-56, 0, 0), 'elbow_r': (0.2, -0.4, 1.0), 'elbow_l': (-0.4, -0.6, 0.6)}),
        ]
        hold = {c: B[c] for c in ('hand_r', 'hand_l', 'w_main', 'w_off') if c in B}
        k = [(t, dict({c: _v(v) for c, v in d.items()})) for t, d in k]
        base = dict(B, **{c: _v(v) for c, v in fk.items()})
        base.update(z)
        anim = H.keys(base, k)

        def fn(t):
            p = anim(H._warp(t, [(0.76, down)]))
            for c, v in hold.items():
                p[c] = v
            # the hands loosen a little as he goes down
            u = min(1.0, max(0.0, (t - 0.3) / 0.5))
            p['hand_r'] = p['hand_r'] + _v((0.0, -0.04, -0.10)) * u
            p['hand_l'] = p['hand_l'] + _v((0.0, -0.04, -0.10)) * u
            p['follow'] = _v(1.0)
            p['dead_t'] = _v(t)
            return p
        return FM.Act(fn, legs='fk', arms='ik')

    # -- the thrower: the bottle in the left hand
    def anim_throw(self, ctx):
        """Winds up with the bottle behind his head, steps in and throws it
        overarm with the left hand, follows through, and takes the next
        bottle from the bag at his left hip; the gun stays on his shoulder."""
        B = self.base
        st = ctx.event('reach_max', 0.45) if ctx is not None else 0.45
        st = min(max(st or 0.45, 0.35), 0.6)
        wind = {'hand_l': (-0.30, -0.26, 1.66), 'w_off': (55, 0, 0), 'elbow_l': (-0.9, -0.2, 0.4),
                'chest': (8, 0, 28), 'spine': (4, 0, 12), 'head': (-4, 0, -22), 'root_pos': (0, -0.07, -0.03),
                'foot_r': (0.17, 0.30, A), 'foot_l': (-0.19, -0.18, A), 'fyaw_l': 35.0, 'toe_l': 0.0, 'in_hand': 1.0}
        rel = {'hand_l': (-0.18, 0.40, 1.66), 'w_off': (-55, 0, 0), 'elbow_l': (-0.8, -0.4, 0.2),
               'chest': (-12, 0, -18), 'spine': (-8, 0, -8), 'head': (-2, 0, 6), 'root_pos': (0, 0.10, -0.06),
               'foot_r': (0.17, 0.32, A), 'foot_l': (-0.19, -0.16, A), 'fyaw_l': 25.0, 'toe_l': 25.0, 'in_hand': 1.0}
        foll = {'hand_l': (0.04, 0.40, 1.02), 'w_off': (-120, 0, 0), 'elbow_l': (-0.6, -0.6, -0.2),
                'chest': (-18, 0, -26), 'spine': (-10, 0, -10), 'head': (-4, 0, 12),
                'root_pos': (0, 0.12, -0.09), 'foot_r': (0.17, 0.32, A), 'foot_l': (-0.19, -0.12, A + 0.03),
                'toe_l': 35.0, 'fyaw_l': 20.0, 'in_hand': 0.0}
        bag = {'hand_l': (-0.32, 0.06, 0.90), 'w_off': (10, 0, 0), 'elbow_l': (-0.6, -0.6, -0.3),
               'chest': (-6, 0, -10), 'spine': (-4, 0, -6), 'head': (10, 0, -24), 'root_pos': (0, 0.04, -0.05),
               'foot_r': (0.17, 0.20, A), 'foot_l': (-0.18, -0.06, A), 'toe_l': 0.0, 'fyaw_l': 15.0, 'in_hand': 0.0}
        rest = {c: B[c] for c in ('hand_l', 'w_off', 'elbow_l', 'chest', 'spine', 'head',
                                  'root_pos', 'foot_l', 'foot_r', 'fyaw_l', 'toe_l') if c in B}
        rest['in_hand'] = 1.0
        k = [(0.0, {'in_hand': 1.0}), (0.22, wind), (0.30, wind), (0.45, rel), (0.58, foll), (0.74, bag),
             (0.80, dict(bag, in_hand=1.0)), (1.0, rest)]
        k = [(t, {c: _v(v) for c, v in d.items()}) for t, d in k]
        anim = H.keys(dict(B, in_hand=_v(1.0), fyaw_l=_v(B.get('fyaw_l', 12.0)), toe_l=_v(0.0)), k)

        def fn(t):
            tt = H._warp(t, [(0.45, st)])
            p = anim(tt)
            p['in_hand'] = _v(1.0 if (tt < 0.455 or tt >= 0.78) else 0.0)
            p['throw_t'] = _v(tt)
            return p
        act = FM.Act(fn)
        act.release = 0.455
        return act

    def _flight(self, act, n, tt):
        key = ('release', id(act))
        if key not in self._cache:
            p0 = act.fn(act.release - 1e-3)
            _, Ts, _ = self._solve(p0, act)
            land = _v(self.p.get('land', (-0.10, 1.25, 0.06)))
            self._cache[key] = (Ts['l'].copy(), land)
        T0, land = self._cache[key]
        fly = float(self.p.get('fly_t', 0.20))
        u = (tt - act.release) / fly
        if u < 0:
            return None, None
        if u >= 1:
            return None, (tt - act.release - fly)
        p0 = T0[:3, 3]
        pos = p0 * (1 - u) + land * u
        pos[2] = p0[2] * (1 - u) + land[2] * u + 0.75 * 4 * u * (1 - u)
        T = M.translate(*pos) @ M.rot_x(-60 - 400 * u) @ M.translate(0, 0, -0.05)
        return T, None

    def frame(self, action, f, n, ctx):
        if not self.thrower:
            return super().frame(action, f, n, ctx)
        self._n = n
        act = self.action(action, ctx)
        t = rig.frame_times(n, act.loop)[f]
        pose = act.fn(t)
        in_hand = float(pose.get('in_hand', 1.0)) > 0.5
        dead_t = float(pose.get('dead_t', -1.0))
        dropped = dead_t >= 0.30
        mesh, info = self.pose_full(pose, act, self.weapon, 'molotov' if (in_hand and not dropped) else None)
        extra = []
        fx = []
        flames = []
        if in_hand and not dropped:
            T = info['T']['l']
            flames.append((T[:3, :3] @ _v(IG.FLAME_AT) + T[:3, 3], 1.0))
        if dropped:
            # the bottle fell from his hand as he went down and burns where it lies
            key = ('dropbottle', id(act))
            if key not in self._cache:
                _, Ts, _ = self._solve(act.fn(0.30), act)
                p0 = Ts['l'][:3, 3].copy()
                self._cache[key] = (p0, _v((p0[0] - 0.22, p0[1] + 0.30, 0.04)))
            p0, land = self._cache[key]
            u = min(1.0, (dead_t - 0.30) / 0.15)
            pos = p0 * (1 - u) + land * u
            pos[2] = p0[2] * (1 - u * u) + land[2] * u * u
            T = M.translate(*pos) @ M.rot_z(30) @ M.rot_x(-90 * u)
            extra.append(self.prop('molotov').transformed(T))
            flames.append((pos + _v((0, 0.0, 0.05 + 0.15 * (1 - u))), 0.9))
        tt = float(pose.get('throw_t', -1.0))
        if tt >= 0:
            T, since = self._flight(act, n, tt)
            if T is not None:
                extra.append(self.prop('molotov').transformed(T))
                flames.append((T[:3, :3] @ _v(IG.FLAME_AT) + T[:3, 3], 0.8))
            if since is not None:
                land = self._cache[('release', id(act))][1]
                fx += self._burst(land, since, f)
                life = max(1e-3, 1.0 - act.release - float(self.p.get('fly_t', 0.20)))
                u = since / life
                rng = np.random.default_rng(5)
                for j in range(7):
                    off = _v((rng.uniform(-0.26, 0.26), rng.uniform(-0.2, 0.2), 0.0))
                    s = (2.4 + 1.8 * min(1.0, u * 4)) * (0.6 + 0.4 * rng.uniform())
                    s *= max(0.0, 1.0 - u * 0.55)
                    if s > 0.2:
                        extra.append(IG.flame(self.mats, k=f + j * 3, size=s).transformed(M.translate(*(land + off))))
        for pos, s in flames:
            extra.append(IG.flame(self.mats, k=f, size=s * 1.5).transformed(M.translate(*pos)))
            fx.append(_glow(pos + _v((0, 0, 0.06 * s)), 0.08 * s, (1.0, 0.70, 0.28), 0.55))
        if extra:
            mesh = M.Mesh.concat([mesh] + extra)
        self._fx = fx
        self._local = mesh
        from models.foot_ancient import finish_colors
        finish_colors(self, dict(self.p, keep_saturation=('flame_o', 'flame_y', 'glass', 'petrol')))
        return mesh

    def _burst(self, land, since, f):
        fx = []
        if since < 0.08:
            fx.append(_glow(land + _v((0, 0, 0.12)), 0.26, (1.0, 0.62, 0.18), 0.85))
            fx.append(_glow(land + _v((0, 0, 0.10)), 0.13, (1.0, 0.92, 0.6), 1.0))
        u = min(1.0, since / 0.4)
        fx.append(_glow(land + _v((0, 0, 0.10)), 0.30 * (1 - 0.5 * u), (1.0, 0.55, 0.16), 0.55 * (1 - 0.6 * u)))
        fx.append({'kind': 'smoke', 'pos': land + _v((0.05, 0.0, 0.35 + 0.4 * u)), 'rad': 0.14 + 0.16 * u,
                   'alpha': 0.45 * (1 - u) + 0.15, 'seed': 3})
        return fx

    def effects(self, action, f, n, act, info, ctx):
        if self.thrower:
            return []
        return super().effects(action, f, n, act, info, ctx)

    def render_cell(self, mesh, mats, cam, W, H_):
        b, t = R.render_cell(mesh, mats, cam, W, H_)
        if not self._fx:
            return b, t
        std = [e for e in self._fx if e['kind'] in ('flash', 'smoke')]
        own = [e for e in self._fx if e['kind'] == 'glow']
        yaw = FM._facing_of(self._local, mesh)
        if std:
            b, t = FM.draw_effects(b, t, std, yaw, cam)
        if own:
            b, t = draw_glows(b, t, own, yaw, cam)
        return b, t


def draw_glows(base, tint, fx, yaw, cam):
    """Soft additive-looking glows over the sprite, or under it where the
    glow is behind the figure."""
    Hh, Ww = base.shape[:2]
    c, s = math.cos(yaw), math.sin(yaw)
    Rz = np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]], np.float32)
    ref = cam.project(Rz @ _v((0, 0, 1.1)))
    yy, xx = np.mgrid[0:Hh, 0:Ww].astype(np.float32) + 0.5
    over_p = np.zeros((Hh, Ww, 3), np.float32)
    over_a = np.zeros((Hh, Ww), np.float32)
    under_p = np.zeros((Hh, Ww, 3), np.float32)
    under_a = np.zeros((Hh, Ww), np.float32)
    for e in fx:
        sp = cam.project(Rz @ _v(e['pos']))
        behind = sp[2] < ref[2] - 0.12
        r = max(e['rad'] * cam.ppm, 1.5)
        d2 = ((xx - sp[0]) ** 2 + (yy - sp[1]) ** 2) / (r * r)
        a = np.clip(1.4 * np.exp(-2.4 * d2 ** 1.2), 0, 1) * e.get('alpha', 1.0)
        a = np.where(a > 0.03, a, 0)
        tp, ta = (under_p, under_a) if behind else (over_p, over_a)
        tp[:] = np.asarray(e['col'], np.float32) * a[..., None] + tp * (1 - a[..., None])
        ta[:] = a + ta * (1 - a)
    bf = base.astype(np.float32) / 255
    tf = tint.astype(np.float32) / 255
    Ba = bf[..., 3]
    Bp = bf[..., :3] * Ba[..., None]
    cover = np.maximum(Ba, tf[..., 3])
    Bp = Bp + under_p * (1 - cover)[..., None]
    Ba = Ba + under_a * (1 - cover)
    Bp = over_p + Bp * (1 - over_a[..., None])
    Ba = over_a + Ba * (1 - over_a)
    out_b = np.zeros_like(bf)
    out_b[..., :3] = Bp / np.maximum(Ba, 1e-6)[..., None]
    out_b[..., 3] = Ba
    out_t = tf.copy()
    out_t[..., 3] = tf[..., 3] * (1 - over_a)
    return R._u8(out_b), R._u8(out_t)
