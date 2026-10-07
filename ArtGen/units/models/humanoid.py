"""Foot units: a stylized low-poly human with clothing, gear and weapons, and
the animation library for Civ3's foot unit actions.

The body is rigged on a small skeleton (see SKELETON_OFFSETS). Arms and legs
are posed by two-bone IK: an action gives hand grip and ankle positions in
model space (x = right, y = forward, z = up, meters) and the weapon's
orientation, and the limbs reach them. Actions that throw the body around
(deaths) pose the limbs directly (FK) instead.

Catalog parameters (all optional; see catalog/ancient.py for examples):
    colors      {material: '#rrggbb'} overrides: skin, hair, top, bottom, belt,
                wraps, boots, helmet, crest, cuirass, wood, steel, ...
    tint        material names drawn in the civ color (default: top, bracers)
    hair        'long' | 'short' | 'none';  beard: bool
    helmet      None | 'crested' | 'horned' | 'hood' | 'cap' | 'headband' | 'wolf'
                ('wolf': a wolf's head worn as a hood; use with back='pelt')
    top         'bare' | 'tunic' | 'sleeved'    cuirass: bool
    bottom      'loincloth' | 'skirt' | 'kilt' | 'long'
    bracers, wraps ('wraps' | 'boots' | 'greaves' | None), sandals
    weapon      'stone_axe' | 'axe' | 'club' | 'spear' | 'sword' | 'staff' | 'shovel' | None
    offhand     None | 'shield_oval' | 'shield_round' | 'bow'
    back        None | 'quiver' | 'backpack' | 'pelt' (hide over the shoulders/back)
    pack_strap  bool: with 'backpack', a strap across the chest
    build       a BUILDS name ('normal', 'lean', 'muscular', 'brawny') or a dict
                {'build': 'muscular', 'height': 1.0, 'bulk': 1.0, 'muscle': 0..1.5};
                'muscle' adds pecs/traps/deltoids/biceps/triceps/forearms/quads/calves
                and broadens chest and shoulders (default: none, unchanged)
    actions     {INI action: animation name} overrides (see DEFAULT_ACTIONS)
"""
import math

import numpy as np

import mesh as M
import rig
from models import archetype
from models import props as P

# ------------------------------------------------------------ the skeleton

PELVIS_H = 0.92
SKELETON_OFFSETS = [
    ('pelvis', None, (0, 0, PELVIS_H)),
    ('spine', 'pelvis', (0, 0, 0.08)),
    ('chest', 'spine', (0, 0, 0.20)),
    ('neck', 'chest', (0, 0, 0.29)),
    ('head', 'neck', (0, 0, 0.07)),
    ('arm_r', 'chest', (0.27, -0.01, 0.215)),
    ('forearm_r', 'arm_r', (0, 0, -0.29)),
    ('hand_r', 'forearm_r', (0, 0, -0.25)),
    ('arm_l', 'chest', (-0.27, -0.01, 0.215)),
    ('forearm_l', 'arm_l', (0, 0, -0.29)),
    ('hand_l', 'forearm_l', (0, 0, -0.25)),
    ('thigh_r', 'pelvis', (0.11, 0, -0.05)),
    ('shin_r', 'thigh_r', (0, 0, -0.40)),
    ('foot_r', 'shin_r', (0, 0, -0.39)),
    ('thigh_l', 'pelvis', (-0.11, 0, -0.05)),
    ('shin_l', 'thigh_l', (0, 0, -0.40)),
    ('foot_l', 'shin_l', (0, 0, -0.39)),
]
UPPER_ARM, FOREARM, GRIP = 0.29, 0.25, 0.065
# Civ3's figures are chunky: broad torsos, thick limbs, big hands, feet and heads.
TORSO_W, LIMB, HAND, FOOT, HEAD = 1.3, 1.35, 1.35, 1.25, 1.12
THIGH, SHIN = 0.40, 0.39
ANKLE_H = PELVIS_H - 0.05 - THIGH - SHIN   # 0.08

DEFAULT_COLORS = {
    'skin': '#c98a58', 'hair': '#4a2e1c', 'top': '#e8e8e8', 'bottom': '#7a5232',
    'belt': '#4a2e1c', 'wraps': '#6a4428', 'wraps_band': '#3e2616', 'boots': '#5a3a22',
    'helmet': '#9a9aa0', 'crest': '#e8e8e8', 'cuirass': '#c09040', 'bracers': '#e8e8e8',
    'eyes': '#2a1a12', 'lips': '#a0583a', 'sandal': '#5a3a22',
}


def _mk_skeleton(height=1.0, shoulders=0.0):
    """The skeleton; `shoulders` (meters) widens the shoulder joints."""
    s = rig.Skeleton()
    for name, parent, off in SKELETON_OFFSETS:
        off = np.asarray(off, np.float32) * height
        if name in ('arm_r', 'arm_l') and shoulders:
            off = off + np.array([np.sign(off[0]) * shoulders, 0, 0], np.float32)
        s.add(name, parent, off)
    return s


# Named body builds for the catalog's 'build' parameter (a name or a dict).
# 'muscle' adds shaped muscle volumes and broadens chest and shoulders.
BUILDS = {
    'normal': {},
    'muscular': {'muscle': 1.0, 'bulk': 1.03},
    'brawny': {'muscle': 1.3, 'bulk': 1.06},
    'lean': {'muscle': 0.5},
}


def build_params(p):
    """The catalog 'build' as a dict: a BUILDS name, or a dict that may name
    a base build too, e.g. {'build': 'muscular', 'height': 1.05}."""
    b = p.get('build', {})
    if isinstance(b, str):
        return dict(BUILDS[b])
    b = dict(b)
    base = b.pop('build', None)
    out = dict(BUILDS[base]) if base else {}
    out.update(b)
    return out


def muscle_amount(p):
    return float(build_params(p).get('muscle', 0.0))


# ------------------------------------------------------------ IK

def _normalize(v):
    return v / (np.linalg.norm(v) + 1e-9)


def two_bone(S, T, a, b, pole, bend_forward):
    """World rotations (3x3) of a two-bone limb whose bones point down their
    local -z, from root S toward target T, the middle joint pointing toward
    `pole` (a direction). bend_forward: the joint bends the lower bone
    forward (+y, an elbow) rather than back (a knee)."""
    d = T - S
    L = float(np.linalg.norm(d))
    L = min(max(L, abs(a - b) + 1e-3), a + b - 1e-4)
    dirv = _normalize(d)
    p = pole - np.dot(pole, dirv) * dirv
    if np.linalg.norm(p) < 1e-6:
        p = np.array([0, 1, 0], np.float32) - dirv[1] * dirv
    n = _normalize(p)
    cosA = (a * a + L * L - b * b) / (2 * a * L)
    sinA = math.sqrt(max(0.0, 1 - cosA * cosA))
    E = S + a * (cosA * dirv + sinA * n)
    End = S + L * dirv
    z_u = _normalize(S - E)
    # the bend axis is shared; its sign makes positive pitch bend the joint the right way
    y_pref = -n if bend_forward else n
    x = _normalize(np.cross(y_pref, z_u))
    y_u = np.cross(z_u, x)
    z_l = _normalize(E - End)
    y_l = np.cross(z_l, x)
    Ru = np.stack([x, y_u, z_u], 1)
    Rl = np.stack([x, y_l, z_l], 1)
    return Ru, Rl, E, End


# ------------------------------------------------------------ the body

class Body:
    """Builds the parts of a humanoid from catalog parameters."""

    def __init__(self, p):
        self.p = p
        self.mats = M.Materials()
        colors = dict(DEFAULT_COLORS)
        colors.update(p.get('colors', {}))
        tint = set(p.get('tint', ['top', 'bracers']))
        self.tint = tint
        for name, col in colors.items():
            spec = {'skin': 0.12, 'helmet': 0.7, 'cuirass': 0.8}.get(name, 0.06)
            gloss = {'helmet': 26, 'cuirass': 24}.get(name, 10)
            metal = name in p.get('metal', ['helmet', 'cuirass']) and name not in tint
            self.mats.add(name, '#ffffff' if name in tint else col, tint=name in tint,
                          spec=spec, gloss=gloss, metal=metal)
        # props' default materials, recolorable too
        for name in ('wood', 'steel', 'stone', 'leather', 'bronze', 'rope', 'shield_face', 'shield_emblem',
                     'shield_rim', 'pack', 'bedroll', 'pot', 'sack', 'fletch', 'string'):
            if name in colors:
                self.mats.add(name, '#ffffff' if name in tint else colors[name], tint=name in tint,
                              spec=0.8 if name in ('steel', 'bronze') else 0.06,
                              gloss=28 if name in ('steel', 'bronze') else 10,
                              metal=name in ('steel', 'bronze'))
            elif name in tint:
                self.mats.add(name, '#ffffff', tint=True)

    def m(self, name):
        return self.mats[name]

    def parts(self):
        """[(bone, mesh, tag)] of the whole figure, in bone space."""
        p = self.p
        bulk = build_params(p).get('bulk', 1.0)
        tw = TORSO_W * bulk   # torso width
        lb = LIMB * bulk      # limb thickness
        skin = self.m('skin')
        out = []

        def add(bone, mesh, tag=None):
            out.append((bone, mesh, tag))

        def addl(bone, mesh, tag=None):   # limb clothing, made for LIMB = 1
            add(bone, mesh.transformed(M.scale(lb, lb, 1)), tag)

        def addt(bone, mesh, tag=None):   # waist clothing, made for TORSO_W = 1
            add(bone, mesh.transformed(M.scale(tw, 1 + (tw - 1) * 0.8, 1)), tag)

        # -- pelvis and torso
        add('pelvis', M.ellipsoid(0.165 * tw, 0.12 * tw, 0.12, skin, (0, 0, -0.01), seg=14))
        add('spine', M.loft([(0.0, 0.15 * tw, 0.105 * tw), (0.12, 0.148 * tw, 0.105 * tw),
                             (0.22, 0.165 * tw, 0.112 * tw)], skin, seg=14, cap_bottom=False, cap_top=False))
        chest_rings = [(0.0, 0.165, 0.105, 0, 0.0), (0.10, 0.19, 0.12, 0, 0.012), (0.19, 0.205, 0.122, 0, 0.008),
                       (0.25, 0.185, 0.108, 0, -0.005), (0.30, 0.10, 0.07, 0, -0.01), (0.33, 0.05, 0.05, 0, -0.01)]
        chest_rings = [(z, rx * tw, ry * (1 + (tw - 1) * 0.6), dx, dy) for z, rx, ry, dx, dy in chest_rings]
        mus = muscle_amount(p)
        if mus > 0:  # a broader, deeper chest over a narrow waist (the V of a heroic build)
            chest_rings = [(z, rx * (1 + 0.09 * mus * (0.10 <= z <= 0.26)), ry * (1 + 0.10 * mus * (0.05 <= z <= 0.26)),
                            dx, dy + 0.012 * mus * (0.08 <= z <= 0.22)) for z, rx, ry, dx, dy in chest_rings]
        add('chest', M.loft(chest_rings, skin, seg=16, cap_bottom=False))
        add('neck', M.cylinder(0.058, 0.052, 0.12, skin, seg=10, z0=-0.04))

        top = p.get('top', 'bare')
        if top in ('tunic', 'sleeved', 'long_sleeved', 'vest'):
            tm = self.m('top')
            g = 0.014
            rings = [(z, rx + g, ry + g, dx, dy) for z, rx, ry, dx, dy in chest_rings[:-1]]
            if top == 'vest':
                rings = rings[:4]
            add('chest', M.loft(rings, tm, seg=16, cap_bottom=False, cap_top=top != 'vest'))
            add('spine', M.loft([(-0.02, 0.165 * tw + g, 0.117 * tw + g), (0.12, 0.162 * tw + g, 0.117 * tw + g),
                                 (0.23, 0.178 * tw + g, 0.125 * tw + g)], tm, seg=14, cap_bottom=False, cap_top=False))
            if top in ('sleeved', 'long_sleeved'):
                for side in 'rl':
                    addl(f'arm_{side}', M.capsule((0, 0, 0.0), (0, 0, -0.17), 0.078, 0.07, tm, seg=10, rings=2))
                    if top == 'long_sleeved':
                        addl(f'forearm_{side}', M.capsule((0, 0, 0.0), (0, 0, -0.2), 0.064, 0.055, tm, seg=10, rings=2))
        if top == 'strap':
            # a one-shoulder tunic: around the body up to the chest, a strap over the left shoulder
            tm = self.m('top')
            g = 0.014
            rings = [(z, rx + g, ry + g, dx, dy) for z, rx, ry, dx, dy in chest_rings[:3]]
            add('chest', M.loft(rings, tm, seg=16, cap_bottom=False, cap_top=False))
            add('spine', M.loft([(-0.02, 0.165 * tw + g, 0.117 * tw + g), (0.12, 0.162 * tw + g, 0.117 * tw + g),
                                 (0.23, 0.178 * tw + g, 0.125 * tw + g)], tm, seg=14, cap_bottom=False, cap_top=False))
            ry3 = chest_rings[2][2] + g
            for sy in (1, -1):
                add('chest', M.capsule((-0.02 * tw, sy * ry3 * 0.95, 0.17), (-0.15 * tw, sy * 0.05, 0.31), 0.05 * tw,
                                       0.04 * tw, tm, seg=8, rings=1))
        elif top == 'mantle':
            # a short cape over the shoulders and upper chest
            tm = self.m('top')
            g = 0.02
            rings = [(z, rx + g, ry + g, dx, dy) for z, rx, ry, dx, dy in chest_rings[1:]]
            rings[0] = (rings[0][0], rings[0][1] * 1.02, rings[0][2] * 1.04, 0, rings[0][4])
            add('chest', M.loft(rings, tm, seg=16, cap_bottom=False, cap_top=True))
            for side in 'rl':
                addl(f'arm_{side}', M.capsule((0, 0, 0.0), (0, 0, -0.10), 0.082, 0.078, tm, seg=10, rings=2))
        if p.get('cuirass'):
            cm = self.m('cuirass')
            g = 0.026
            rings = [(z, rx + g, ry + g, dx, dy) for z, rx, ry, dx, dy in chest_rings[:4]]
            add('chest', M.loft(rings, cm, seg=16, cap_bottom=False, cap_top=True))
        if p.get('sash'):
            sm = self.m(p['sash']) if p['sash'] in self.mats else self.m('belt')
            add('chest', M.capsule((0.17, 0.12, 0.27), (-0.16, 0.135, -0.02), 0.03, 0.03, sm, seg=6, rings=1))
            add('chest', M.capsule((0.17, -0.11, 0.27), (-0.16, -0.12, -0.02), 0.03, 0.03, sm, seg=6, rings=1))

        # -- pelvis clothing
        bottom = p.get('bottom', 'loincloth')
        bm = self.m('bottom')
        if bottom == 'loincloth':
            addt('pelvis', M.loft([(-0.16, 0.185, 0.14), (0.0, 0.178, 0.128), (0.10, 0.168, 0.12)], bm, seg=14,
                                 cap_bottom=False, cap_top=False))
            addt('pelvis', M.rounded_box(0.2, 0.035, 0.34, bm, (0, 0.125, -0.2), r=0.014, seg=8, p=4)
                .transformed(M.rot_x(-6)))
            addt('pelvis', M.rounded_box(0.22, 0.035, 0.30, bm, (0, -0.125, -0.18), r=0.014, seg=8, p=4)
                .transformed(M.rot_x(6)))
        elif bottom in ('skirt', 'kilt', 'long'):
            low = {'skirt': -0.21, 'kilt': -0.30, 'long': -0.52}[bottom]
            flare = {'skirt': 0.05, 'kilt': 0.06, 'long': 0.10}[bottom]
            addt('pelvis', M.loft([(low, 0.19 + flare, 0.15 + flare, 0, 0.01), (low * 0.5, 0.19 + flare * 0.5, 0.145 + flare * 0.5),
                                  (0.0, 0.182, 0.13), (0.11, 0.17, 0.12)], bm, seg=18, cap_bottom=False, cap_top=False))
        bt = self.m('belt')
        addt('pelvis', M.loft([(0.06, 0.183, 0.132), (0.12, 0.176, 0.127)], bt, seg=16,
                             cap_bottom=False, cap_top=False))

        # -- head
        self._head(add)

        # -- arms
        for side, sx in (('r', 1), ('l', -1)):
            add(f'arm_{side}', M.sphere(0.075 * lb, skin, (0, 0, -0.01), seg=10))
            add(f'arm_{side}', M.capsule((0, 0, 0), (0, 0, -UPPER_ARM), 0.064 * lb, 0.05 * lb, skin, seg=10, rings=2))
            add(f'forearm_{side}', M.capsule((0, 0, 0), (0, 0, -FOREARM), 0.05 * lb, 0.04 * lb, skin, seg=10, rings=2))
            add(f'hand_{side}', M.ellipsoid(0.042 * HAND, 0.052 * HAND, 0.062 * HAND, skin, (0, 0.008, -0.055), seg=9))
            add(f'hand_{side}', M.ellipsoid(0.02, 0.022, 0.035, skin, (-sx * 0.03, 0.035, -0.04), seg=6))
            if p.get('bracers'):
                bm2 = self.m('bracers')
                addl(f'forearm_{side}', M.loft([(-0.235, 0.05, 0.05), (-0.2, 0.054, 0.054), (-0.06, 0.06, 0.06),
                                                   (-0.04, 0.056, 0.056)], bm2, seg=10, cap_bottom=True, cap_top=True))
        # -- legs
        wraps = p.get('wraps')
        for side, sx in (('r', 1), ('l', -1)):
            add(f'thigh_{side}', M.capsule((0, 0, 0), (0, 0, -THIGH), 0.088 * lb, 0.064 * lb, skin, seg=10, rings=2))
            add(f'shin_{side}', M.capsule((0, 0, 0), (0, 0, -SHIN), 0.062 * lb, 0.046 * lb, skin, seg=10, rings=2))
            # calf muscle
            add(f'shin_{side}', M.ellipsoid(0.058 * lb, 0.058 * lb, 0.12, skin, (0, -0.012, -0.13), seg=9))
            footm = self.m('boots') if wraps in ('boots', 'greaves') else (self.m('sandal') if p.get('sandals') else skin)
            add(f'foot_{side}', M.rounded_box(0.095 * FOOT, 0.23 * FOOT, 0.075, footm, (0, 0.055, -0.042), r=0.03, seg=12, p=3))
            if wraps == 'wraps':
                wm, wb = self.m('wraps'), self.m('wraps_band')
                addl(f'shin_{side}', M.loft([(-0.40, 0.056, 0.056), (-0.24, 0.066, 0.068, 0, -0.008),
                                            (-0.10, 0.074, 0.076, 0, -0.012), (-0.02, 0.07, 0.07)], wm, seg=12,
                                           cap_bottom=False, cap_top=False))
                for z, r in ((-0.33, 0.062), (-0.22, 0.072), (-0.11, 0.079)):
                    addl(f'shin_{side}', M.cylinder(r, r, 0.022, wb, seg=12, z0=z))
                addl(f'foot_{side}', M.rounded_box(0.1, 0.235, 0.08, wm, (0, 0.055, -0.04), r=0.03, seg=12, p=3))
            elif wraps == 'boots':
                bm3 = self.m('boots')
                addl(f'shin_{side}', M.loft([(-0.42, 0.06, 0.06), (-0.2, 0.068, 0.07), (-0.08, 0.072, 0.074)], bm3,
                                           seg=12, cap_bottom=False, cap_top=True))
            elif wraps == 'greaves':
                gm = self.m('boots')
                addl(f'shin_{side}', M.loft([(-0.40, 0.058, 0.06), (-0.22, 0.066, 0.072, 0, 0.004),
                                            (-0.04, 0.07, 0.074)], gm, seg=12, cap_bottom=False, cap_top=True))
            if p.get('sandals'):
                sm = self.m('sandal')
                addl(f'shin_{side}', M.cylinder(0.05, 0.05, 0.02, sm, seg=10, z0=-0.40))

        if mus > 0:
            self._muscles(add, addl, mus, chest_rings, tw, lb, top, wraps)

        # -- worn gear
        back = p.get('back')
        if back == 'quiver':
            add('chest', P.quiver(self.mats))
        elif back == 'backpack':
            add('chest', P.backpack(self.mats))
            if p.get('pack_strap', False):
                # a strap across the chest from the right hip to over the left shoulder, holding the pack
                st = P.leather(self.mats)
                ry1, ry2 = chest_rings[1][2] + 0.03, chest_rings[2][2] + 0.03
                rx1 = chest_rings[1][1]
                pts = [(rx1 * 0.85, ry1 * 0.55, -0.02), (rx1 * 0.35, ry1 * 1.0, 0.09), (-rx1 * 0.25, ry2 * 1.0, 0.20),
                       (-rx1 * 0.62, ry2 * 0.62, 0.31), (-rx1 * 0.7, 0.0, 0.34)]
                for a, b in zip(pts[:-1], pts[1:]):
                    add('chest', M.capsule(a, b, 0.028, 0.028, st, seg=6, rings=1))
        elif back == 'pelt':
            # an animal hide over the shoulders and down the back, its forelegs hanging in front
            pm = P.mat(self.mats, 'pelt', '#8a8580', spec=0.03)
            rings = [(z, rx + 0.03, ry + 0.035, dx, dy) for z, rx, ry, dx, dy in chest_rings[:5]]
            rings = [(-0.32, 0.20 * tw, 0.16 * tw), (-0.16, 0.19 * tw, 0.15 * tw)] + rings
            add('chest', _shell(rings, pm, math.pi * 0.9, math.pi * 2.1, seg=14))
            for sx in (1, -1):
                x0 = sx * chest_rings[3][1] * 0.7
                add('chest', M.capsule((x0, 0.0, 0.31), (x0 * 1.05, chest_rings[2][2] + 0.03, 0.18), 0.04, 0.032, pm,
                                       seg=7, rings=1))
                add('chest', M.capsule((x0 * 1.05, chest_rings[2][2] + 0.03, 0.18),
                                       (x0 * 0.9, chest_rings[1][2] + 0.035, 0.0), 0.032, 0.026, pm, seg=7, rings=1))
        return out

    def _muscles(self, add, addl, mus, chest_rings, tw, lb, top, wraps):
        """Shaped muscle volumes for a brawny build ('build': 'muscular'):
        pecs, traps, deltoids, biceps, triceps, forearms, quads and calves.
        Where clothing covers a muscle, the volume takes the clothing's
        material so the garment bulges with it instead of being pierced."""
        skin = self.m('skin')
        k = 0.85 + 0.15 * mus          # overall size of the volumes
        tm = self.m('top') if 'top' in self.mats else skin
        chest_bare = top in ('bare', None)
        sleeve = top in ('sleeved', 'long_sleeved', 'mantle')
        ry2 = chest_rings[2][2]
        if chest_bare:
            for sx in (1, -1):
                add('chest', M.ellipsoid(0.10 * tw * k, 0.05 * k, 0.075 * k, skin,
                                         (sx * 0.088 * tw, ry2 * 0.80 + 0.012 * mus, 0.165), seg=12))
        if top in ('bare', None, 'strap'):
            add('chest', M.ellipsoid(0.15 * tw * k, 0.065 * k, 0.065 * k, skin, (0, -0.012, 0.275), seg=12))
        for side, sx in (('r', 1), ('l', -1)):
            dm = tm if sleeve else skin
            # deltoid caps the shoulder; biceps in front, triceps behind; forearm bulge below the elbow
            add(f'arm_{side}', M.ellipsoid(0.088 * lb * k, 0.09 * lb * k, 0.11 * k, dm, (sx * 0.006, 0, -0.04), seg=12))
            am = tm if top == 'long_sleeved' else skin
            add(f'arm_{side}', M.ellipsoid(0.056 * lb * k, 0.062 * lb * k, 0.095 * k, am, (0, 0.022, -0.155), seg=10))
            add(f'arm_{side}', M.ellipsoid(0.056 * lb * k, 0.058 * lb * k, 0.10 * k, am, (0, -0.02, -0.12), seg=10))
            add(f'forearm_{side}', M.ellipsoid(0.054 * lb * k, 0.056 * lb * k, 0.095 * k, am, (0, 0.008, -0.075), seg=10))
            add(f'thigh_{side}', M.ellipsoid(0.08 * lb * k, 0.078 * lb * k, 0.15 * k, skin, (0, 0.022, -0.17), seg=10))
            cm = {'wraps': self.m('wraps') if 'wraps' in self.mats else skin,
                  'boots': self.m('boots'), 'greaves': self.m('boots')}.get(wraps, skin)
            add(f'shin_{side}', M.ellipsoid(0.064 * lb * k, 0.068 * lb * k, 0.125 * k, cm, (0, -0.02, -0.13), seg=10))

    def _head(self, add0):
        def add(bone, mesh, tag=None):
            add0(bone, mesh.transformed(M.scale(HEAD)), tag)
        p = self.p
        skin = self.m('skin')
        hm = self.m('hair')
        # skull, jaw, nose, brows, ears
        add('head', M.ellipsoid(0.098, 0.112, 0.122, skin, (0, 0.012, 0.115), seg=14))
        add('head', M.ellipsoid(0.078, 0.08, 0.06, skin, (0, 0.04, 0.05), seg=12))
        add('head', M.ellipsoid(0.018, 0.03, 0.03, skin, (0, 0.125, 0.10), seg=6))
        add('head', M.ellipsoid(0.07, 0.03, 0.018, skin, (0, 0.102, 0.142), seg=8))
        for sx in (1, -1):
            add('head', M.ellipsoid(0.02, 0.03, 0.035, skin, (sx * 0.097, 0.0, 0.105), seg=6))
            add('head', M.ellipsoid(0.017, 0.008, 0.011, self.m('eyes'), (sx * 0.038, 0.108, 0.122), seg=6))
        hair = p.get('hair', 'short')
        helmet = p.get('helmet')
        if hair != 'none' and helmet not in ('hood',):
            add('head', M.ellipsoid(0.106, 0.118, 0.12, hm, (0, -0.004, 0.135), seg=14).transformed(M.translate(0, 0, 0))
                if hair != 'long' else M.ellipsoid(0.112, 0.124, 0.13, hm, (0, -0.012, 0.13), seg=14))
            if hair == 'long':
                add('head', M.loft([(-0.16, 0.08, 0.05, 0, -0.10), (-0.02, 0.11, 0.07, 0, -0.08), (0.10, 0.115, 0.09, 0, -0.05),
                                    (0.18, 0.09, 0.08, 0, -0.03)], hm, seg=12))
        if p.get('beard'):
            add('head', M.ellipsoid(0.085, 0.07, 0.07, hm, (0, 0.05, 0.035), seg=12))
        if helmet == 'crested':
            hm2 = self.m('helmet')
            add('head', _dome(0.112, 0.126, 0.13, hm2, (0, 0.0, 0.11), 0.0))
            cm = self.m('crest')
            add('head', M.extrude([(-0.1, 0.0), (0.12, 0.0), (0.1, 0.07), (0.0, 0.1), (-0.12, 0.05)], 0.035, cm)
                .transformed(M.translate(0, 0, 0.2) @ M.rot_z(90)))
            for sx in (1, -1):
                add('head', M.rounded_box(0.018, 0.07, 0.1, hm2, (sx * 0.1, 0.04, 0.07), r=0.008, seg=8))
        elif helmet == 'horned':
            hm2 = self.m('helmet')
            add('head', _dome(0.114, 0.126, 0.15, hm2, (0, 0.0, 0.105), -0.02))
            add('head', M.cone(0.045, 0.09, hm2, seg=8, z0=0.24))
            add('head', M.rounded_box(0.022, 0.03, 0.09, hm2, (0, 0.125, 0.11), r=0.008, seg=6))  # nose guard
            for sx in (1, -1):
                add('head', M.rounded_box(0.02, 0.08, 0.11, hm2, (sx * 0.1, 0.05, 0.06), r=0.008, seg=8))
                horn = M.capsule((sx * 0.08, 0.0, 0.2), (sx * 0.14, 0.02, 0.31), 0.025, 0.006, hm2, seg=6, rings=1)
                add('head', horn)
        elif helmet == 'hood':
            hm2 = self.m('helmet')
            add('head', M.ellipsoid(0.118, 0.13, 0.142, hm2, (0, -0.012, 0.12), seg=14))
            add('head', M.loft([(-0.08, 0.14, 0.12), (0.02, 0.12, 0.11), (0.08, 0.11, 0.1)], hm2, seg=14,
                               cap_bottom=False, cap_top=False))
        elif helmet == 'cap':
            hm2 = self.m('helmet')
            add('head', _dome(0.112, 0.124, 0.12, hm2, (0, -0.01, 0.135), 0.0))
            add('head', M.cylinder(0.118, 0.118, 0.03, hm2, seg=14, z0=0.12).transformed(M.scale(1, 1.08, 1)))
        elif helmet == 'headband':
            hm2 = self.m('helmet')
            add('head', M.cylinder(0.114, 0.114, 0.045, hm2, seg=14, z0=0.14).transformed(M.scale(1, 1.08, 1)))
        elif helmet == 'wolf':
            # a wolf's head worn as a hood: skull over the head, muzzle over the brow, ears up
            # (pair with 'back': 'pelt' for the hide down the back)
            pm = P.mat(self.mats, 'pelt', '#8a8580', spec=0.03)
            pd = P.mat(self.mats, 'pelt_dark', '#4e4a46', spec=0.03)
            add('head', _dome(0.122, 0.134, 0.14, pm, (0, -0.008, 0.11), -0.03))
            add('head', M.ellipsoid(0.125, 0.11, 0.13, pm, (0, -0.07, 0.07), seg=12))
            add('head', M.ellipsoid(0.085, 0.10, 0.065, pm, (0, 0.05, 0.235), seg=12))
            add('head', M.capsule((0, 0.08, 0.235), (0, 0.25, 0.205), 0.058, 0.03, pm, seg=10, rings=2))
            add('head', M.sphere(0.022, pd, (0, 0.275, 0.205), seg=8))
            for sx in (1, -1):
                add('head', M.capsule((sx * 0.065, 0.0, 0.26), (sx * 0.085, -0.015, 0.36), 0.036, 0.006, pm, seg=7,
                                      rings=1))
                add('head', M.ellipsoid(0.014, 0.01, 0.01, pd, (sx * 0.042, 0.14, 0.255), seg=6))


def _shell(rings, mat, a0, a1, seg=12):
    """An open surface between angles a0..a1 of stacked elliptical rings
    (z, rx, ry[, dx, dy]), top to bottom or bottom to top: capes, pelts."""
    ang = np.linspace(a0, a1, seg + 1)
    pts = []
    for r in rings:
        z, rx, ry = r[0], r[1], r[2]
        dx, dy = (r[3], r[4]) if len(r) > 4 else (0.0, 0.0)
        pts.append(np.stack([dx + rx * np.cos(ang), dy + ry * np.sin(ang), np.full(len(ang), z)], -1))
    v = np.concatenate(pts).astype(np.float32)
    faces = M.grid_faces(len(rings) - 1, seg + 1, closed_u=False)
    return M.from_indexed(v, faces, mat, smooth=True)


def _dome(rx, ry, rz, mat, c, z_cut):
    """The top of an ellipsoid above z_cut (relative to its center): a helmet."""
    rings = []
    k = 7
    t0 = math.asin(max(-1, min(1, z_cut / rz)))
    for i in range(k + 1):
        t = t0 + (math.pi / 2 - t0) * i / k
        r = math.cos(t)
        rings.append((c[2] + rz * math.sin(t), max(rx * r, 1e-4), max(ry * r, 1e-4), c[0], c[1]))
    return M.loft(rings, mat, seg=16, cap_bottom=False, cap_top=False)


# ------------------------------------------------------------ props in the hands

def make_prop(name, mats, **kw):
    if name is None:
        return None
    return getattr(P, name)(mats, **kw)


# Where each prop is gripped and how it hangs at rest (model space orientation).
SHOULDER_Z = PELVIS_H + 0.08 + 0.20 + 0.215     # 1.475


# ------------------------------------------------------------ animation

class Action:
    """An animation: fn(t) -> pose channels. legs/arms: 'ik' or 'fk'."""

    def __init__(self, fn, loop=False, legs='ik', arms='ik', hide=()):
        self.fn, self.loop, self.legs, self.arms, self.hide = fn, loop, legs, arms, hide


def stance(weapon, offhand):
    """The rest pose for a figure holding its gear: channels in model space."""
    p = {
        'root_pos': (0, 0, 0), 'root_rot': (0, 0, 0),
        'spine': (0, 0, 0), 'chest': (0, 0, 0), 'neck': (0, 0, 0), 'head': (0, 0, 0),
        'foot_r': (0.17, 0.03, ANKLE_H), 'foot_l': (-0.17, -0.03, ANKLE_H),
        'toe_r': 0.0, 'toe_l': 0.0, 'fyaw_r': -12.0, 'fyaw_l': 12.0,
        'hand_r': (0.40, 0.06, 0.84), 'hand_l': (-0.40, 0.02, 0.84),
        'elbow_r': (0.35, -1.0, -0.2), 'elbow_l': (-0.35, -1.0, -0.2),
        'w_main': (-150, 0, 0), 'w_off': (0, 0, 0), 'draw': 0.0,
    }
    if weapon in ('stone_axe', 'axe', 'club', 'hammer', 'machete'):
        p.update({'hand_r': (0.38, 0.16, 0.86), 'w_main': (-108, 0, -30)})
    elif weapon == 'sword':
        p.update({'hand_r': (0.38, 0.14, 0.86), 'w_main': (-140, 0, -20)})
    elif weapon in ('spear', 'staff'):
        p.update({'hand_r': (0.34, 0.14, 0.98), 'w_main': (-4, 0, 0), 'elbow_r': (0.6, -0.8, -0.3)})
    elif weapon == 'shovel':
        p.update({'hand_r': (0.30, 0.22, 0.92), 'w_main': (-80, 0, 18), 'hand_l': (-0.02, 0.42, 1.00)})
    if offhand == 'shield_oval':
        p.update({'hand_l': (-0.34, 0.22, 0.98), 'elbow_l': (-0.6, -0.7, -0.4)})
    elif offhand == 'shield_round':
        p.update({'hand_l': (-0.36, 0.20, 0.96), 'elbow_l': (-0.6, -0.7, -0.4)})
    elif offhand == 'bow':
        p.update({'hand_l': (-0.36, 0.14, 0.88), 'w_off': (-20, 0, 0)})
    return {k: np.asarray(v, np.float32) for k, v in p.items()}


def keys(base, ks, loop=False):
    return rig.Keys(ks, loop=loop, base=base)


def _warp(t, pairs):
    """Piecewise-linear time warp through (from, to) pairs, from 0 to 1."""
    pts = [(0.0, 0.0)] + sorted(pairs) + [(1.0, 1.0)]
    for (a0, b0), (a1, b1) in zip(pts[:-1], pts[1:]):
        if t <= a1:
            u = 0 if a1 <= a0 else (t - a0) / (a1 - a0)
            return b0 + (b1 - b0) * u
    return 1.0


def _back_to(B, chans):
    return {c: B[c] for c in chans if c in B}


def anim_idle(base, loop=True, amount=1.0):
    def fn(t):
        s = math.sin(2 * math.pi * t)
        c = math.cos(2 * math.pi * t)
        p = dict(base)
        p['root_pos'] = base['root_pos'] + np.array([0.004 * c, 0, -0.006 * (1 - s) * amount], np.float32)
        p['chest'] = base['chest'] + np.array([1.5 * s * amount, 0, 0], np.float32)
        p['head'] = base['head'] + np.array([-1.0 * s, 0, 2.0 * c * amount], np.float32)
        p['hand_r'] = base['hand_r'] + np.array([0, 0.008 * s, 0.006 * s], np.float32) * amount
        p['hand_l'] = base['hand_l'] + np.array([0, -0.008 * s, 0.006 * s], np.float32) * amount
        return p
    return Action(fn, loop=loop)


def anim_run(base, weapon, offhand, n):
    """A loping run in place, leaning into it with long strides."""
    stride, lift = 0.40, 0.22

    def pump(s):
        return np.array([0, 0.24 * s, 0.05 + 0.06 * max(0.0, s)], np.float32)

    def fn(t):
        ph = 2 * math.pi * t
        p = dict(base)
        p['root_rot'] = np.array([-16, 0, 5 * math.sin(ph)], np.float32)
        p['root_pos'] = np.array([0, 0.10, -0.10 + 0.06 * abs(math.cos(ph))], np.float32)
        p['spine'] = np.array([-4, 0, -5 * math.sin(ph)], np.float32)
        p['chest'] = np.array([-2, 0, -7 * math.sin(ph)], np.float32)
        p['head'] = np.array([14, 0, 0], np.float32)
        for side, sx, off in (('r', 1, 0.0), ('l', -1, math.pi)):
            q = ph + off
            y = stride * math.sin(q) + 0.08
            z = ANKLE_H + lift * max(0.0, math.cos(q)) ** 1.5
            p['foot_' + side] = np.array([sx * 0.13, y, z], np.float32)
            p['toe_' + side] = np.float32(-30 * max(0.0, math.cos(q)) + 12 * max(0.0, -math.sin(q)))
            p['fyaw_' + side] = np.float32(-sx * 6)
        sw = math.sin(ph)
        hr = base['hand_r'] + (pump(-sw) if weapon not in ('spear', 'staff', 'shovel')
                               else np.array([0, -0.06 * sw, 0.04], np.float32))
        if offhand is None or offhand == 'bow':
            hl = base['hand_l'] + pump(sw) + np.array([0.06, 0, 0], np.float32)
        else:
            hl = base['hand_l'] + np.array([0, 0.05 * sw, 0.04], np.float32)
        if weapon == 'shovel':
            hl = base['hand_l'] + np.array([0, -0.06 * sw, 0.04], np.float32)
        p['hand_r'], p['hand_l'] = hr, hl
        if weapon in ('stone_axe', 'axe', 'club', 'sword', 'hammer', 'machete'):
            p['w_main'] = base['w_main'] + np.array([-25 * sw, 0, 0], np.float32)
        return p
    return Action(fn, loop=True)


def _lead(depth=1.0):
    return {'foot_l': (-0.18, 0.20 + 0.25 * depth, ANKLE_H), 'foot_r': (0.18, -0.12 - 0.18 * depth, ANKLE_H),
            'toe_r': 18.0, 'fyaw_r': -25.0}


ATTACK_REST = ('hand_r', 'hand_l', 'w_main', 'w_off', 'chest', 'spine', 'root_pos', 'root_rot', 'head', 'elbow_r',
               'elbow_l', 'foot_l', 'foot_r', 'toe_r', 'fyaw_r', 'draw')


def anim_attack(base, weapon, offhand, style, ctx):
    """Weapon attacks. style: 'chop' (a one-handed diagonal overhead blow),
    'lunge' (the same, stepping deep into a low strike), 'slash' (a flat
    sideways cut), 'thrust' (spear), 'shoot' (bow)."""
    B = base
    strike_t = ctx.event('reach_max', 0.55) if ctx is not None else 0.55
    strike_t = min(max(strike_t or 0.55, 0.35), 0.8)
    rest = ATTACK_REST
    if style in ('chop', 'lunge'):
        # Overhead, then down on the weapon side, stepping in with the right foot.
        d = 1.0 if style == 'lunge' else 0.6
        step = {'foot_r': (0.20, 0.20 + 0.28 * d, ANKLE_H), 'foot_l': (-0.20, -0.10 - 0.20 * d, ANKLE_H),
                'toe_l': 20.0, 'fyaw_l': 25.0}
        strike = {'hand_r': (0.30, 0.58 + 0.28 * d, 1.0 - 0.28 * d), 'w_main': (-98 - 14 * d, 0, -28),
                  'chest': (-12 - 10 * d, 0, -12), 'spine': (-8 - 8 * d, 0, -6),
                  'root_pos': (0.06, 0.10 + 0.16 * d, -0.08 - 0.20 * d), 'hand_l': (-0.50, -0.18, 1.08),
                  'head': (16, 0, 0), 'elbow_r': (0.8, -0.6, -0.2), 'elbow_l': (-0.6, -0.6, 0.2)}
        k = [
            (0.0, {}),
            (0.16, {'hand_r': (0.40, -0.10, 1.55), 'w_main': (40, 0, 30), 'chest': (6, 0, 12), 'spine': (2, 0, 6),
                    'root_pos': (0, -0.05, -0.03), 'hand_l': (-0.40, 0.30, 1.15), 'elbow_r': (0.9, -0.3, -0.2)}),
            (0.36, {'hand_r': (0.16, -0.04, 1.95), 'w_main': (78, 0, 55), 'chest': (10, 0, 16), 'spine': (5, 0, 8),
                    'root_pos': (0, -0.06, 0.02), 'hand_l': (-0.42, 0.40, 1.32), 'head': (-10, 0, 0),
                    'elbow_r': (0.9, -0.2, 0.3)}),
            (0.56, dict(step, **strike)),
            (0.70, dict(step, **dict(strike, hand_r=(0.32, 0.52 + 0.26 * d, 0.86 - 0.30 * d),
                                     w_main=(-120 - 16 * d, 0, -34)))),
            (0.86, {'foot_l': B['foot_l'], 'foot_r': B['foot_r'], 'toe_l': 0.0, 'fyaw_l': B['fyaw_l'],
                    'root_pos': (0, 0.03, -0.03), 'chest': (0, 0, -4), 'spine': (0, 0, -2)}),
            (1.0, _back_to(B, rest + ('toe_l', 'fyaw_l'))),
        ]
        warp = [(0.56, max(0.3, strike_t - 0.07))]
    elif style == 'slash':
        k = [
            (0.0, {}),
            (0.22, {'hand_r': (0.55, -0.05, 1.32), 'w_main': (-80, 0, -130), 'chest': (2, 0, -38), 'spine': (0, 0, -16),
                    'hand_l': (-0.30, 0.35, 1.15), 'elbow_r': (0.6, -0.6, -0.4), 'root_pos': (0, -0.04, -0.06)}),
            (0.50, dict(_lead(0.7), **{'hand_r': (0.05, 0.78, 1.15), 'w_main': (-88, 0, 10), 'chest': (-8, 0, 0),
                                       'spine': (-6, 0, 0), 'root_pos': (0, 0.16, -0.14), 'hand_l': (-0.45, -0.1, 1.0)})),
            (0.66, dict(_lead(0.7), **{'hand_r': (-0.40, 0.45, 1.05), 'w_main': (-90, 0, 85), 'chest': (-8, 0, 36),
                                       'spine': (-6, 0, 16), 'root_pos': (0, 0.16, -0.14)})),
            (0.85, {'foot_l': B['foot_l'], 'foot_r': B['foot_r'], 'toe_r': 0.0, 'fyaw_r': B['fyaw_r'],
                    'root_pos': (0, 0.03, -0.03), 'chest': (0, 0, 10), 'spine': (0, 0, 5)}),
            (1.0, _back_to(B, rest)),
        ]
        warp = [(0.50, strike_t)]
    elif style == 'thrust':
        # From a crouched guard (spear level at the hip, shield forward): draw back, lunge, recover.
        ready = {'hand_r': (0.30, 0.02, 0.98), 'w_main': (-91, 0, 2), 'root_pos': (0, 0.0, -0.12),
                 'foot_l': (-0.18, 0.24, ANKLE_H), 'foot_r': (0.18, -0.20, ANKLE_H), 'fyaw_r': -25.0,
                 'chest': (-6, 0, -10), 'spine': (-4, 0, -4), 'elbow_r': (0.8, -0.6, -0.2)}
        if offhand in ('shield_oval', 'shield_round'):
            ready['hand_l'] = (-0.20, 0.40, 1.04)
        k = [
            (0.0, ready),
            (0.28, dict(ready, hand_r=(0.32, -0.30, 1.02), chest=(-2, 0, -24), spine=(0, 0, -10),
                        root_pos=(0, -0.06, -0.14))),
            (0.50, dict(ready, **dict(_lead(1.0), hand_r=(0.14, 0.78, 0.98), w_main=(-92, 0, 3), chest=(-14, 0, 10),
                                      spine=(-10, 0, 4), root_pos=(0, 0.22, -0.22)))),
            (0.62, dict(ready, **dict(_lead(1.0), hand_r=(0.12, 0.80, 0.96), w_main=(-93, 0, 3), chest=(-14, 0, 10),
                                      spine=(-10, 0, 4), root_pos=(0, 0.22, -0.22)))),
            (0.85, dict(ready, hand_r=(0.28, 0.10, 0.98))),
            (1.0, ready),
        ]
        warp = [(0.50, strike_t)]
    elif style == 'shoot':
        aim = {'hand_l': (-0.06, 0.68, 1.36), 'w_off': (-4, 6, 0), 'chest': (0, 0, -50), 'spine': (0, 0, -22),
               'head': (0, 0, 55), 'foot_l': (-0.15, 0.24, ANKLE_H), 'foot_r': (0.16, -0.16, ANKLE_H),
               'fyaw_r': -40.0, 'elbow_l': (-1.0, -0.2, -0.5)}
        k = [
            (0.0, {}),
            (0.22, dict(aim, **{'hand_r': (0.02, 0.58, 1.38), 'draw': 0.0, 'elbow_r': (0.8, -0.3, 0.1)})),
            (0.52, dict(aim, **{'hand_r': (0.12, 0.08, 1.46), 'draw': 1.0, 'elbow_r': (1.0, -0.4, 0.3)})),
            (0.62, dict(aim, **{'hand_r': (0.14, 0.04, 1.46), 'draw': 1.0, 'elbow_r': (1.0, -0.4, 0.3)})),
            (0.68, dict(aim, **{'hand_r': (0.30, -0.12, 1.36), 'draw': 0.0})),
            (0.86, {'draw': 0.0, 'chest': (0, 0, -20), 'spine': (0, 0, -8), 'head': (0, 0, 20)}),
            (1.0, _back_to(B, rest)),
        ]
        rel = ctx.event('reach_max', 0.62) if ctx is not None else 0.62
        warp = [(0.65, min(max(rel or 0.62, 0.45), 0.8))]
    else:
        raise ValueError(style)
    anim = keys(B, k)
    return Action(lambda t: anim(_warp(t, warp)))


def anim_death(base, weapon, offhand, ctx):
    """Struck, staggers back, knees buckle and falls on the back."""
    down = ctx.event('down', 0.55) if ctx is not None else 0.55
    down = min(max(down or 0.55, 0.35), 0.85)
    fk = {
        'thigh_r': (0, -4, 0), 'shin_r': (0, 0, 0), 'thigh_l': (0, 4, 0), 'shin_l': (0, 0, 0),
        'arm_r': (10, -18, 0), 'forearm_r': (25, 0, 0), 'arm_l': (5, 18, 0), 'forearm_l': (20, 0, 0),
        'root_pos': (0, 0, 0), 'root_rot': (0, 0, 0), 'spine': (0, 0, 0), 'chest': (0, 0, 0), 'head': (0, 0, 0),
    }
    k = [
        (0.0, {}),
        (0.18, {'root_rot': (12, 0, 6), 'root_pos': (0, -0.06, -0.02), 'chest': (12, 0, 8), 'head': (22, 0, 0),
                'arm_r': (50, -60, 0), 'forearm_r': (50, 0, 0), 'arm_l': (45, 60, 0), 'forearm_l': (60, 0, 0),
                'thigh_r': (10, -4, 0), 'shin_r': (-18, 0, 0)}),
        (0.38, {'root_rot': (30, 0, 10), 'root_pos': (0, -0.22, -0.28), 'chest': (14, 0, 8), 'head': (24, 0, 0),
                'thigh_r': (62, -8, 0), 'shin_r': (-72, 0, 0), 'thigh_l': (44, 8, 0), 'shin_l': (-62, 0, 0),
                'arm_r': (80, -75, 0), 'forearm_r': (30, 0, 0), 'arm_l': (70, 75, 0), 'forearm_l': (40, 0, 0)}),
        (0.62, {'root_rot': (80, 0, 8), 'root_pos': (0, -0.55, -0.72), 'chest': (6, 0, 4), 'head': (10, 0, 10),
                'thigh_r': (40, -10, 0), 'shin_r': (-40, 0, 0), 'thigh_l': (30, 12, 0), 'shin_l': (-30, 0, 0),
                'arm_r': (140, -60, 0), 'forearm_r': (10, 0, 0), 'arm_l': (130, 60, 0), 'forearm_l': (15, 0, 0)}),
        (0.78, {'root_rot': (92, 0, 6), 'root_pos': (0, -0.62, -0.79), 'chest': (-2, 0, 2), 'head': (-10, 0, 18),
                'thigh_r': (14, -14, 0), 'shin_r': (-12, 0, 0), 'thigh_l': (8, 16, 0), 'shin_l': (-16, 0, 0),
                'arm_r': (160, -80, 0), 'forearm_r': (5, 0, 0), 'arm_l': (160, 80, 0), 'forearm_l': (10, 0, 0)}),
        (1.0, {'root_rot': (90, 0, 6), 'root_pos': (0, -0.62, -0.78), 'chest': (0, 0, 2), 'head': (-6, 0, 22),
               'thigh_r': (10, -14, 0), 'shin_r': (-10, 0, 0), 'thigh_l': (6, 16, 0), 'shin_l': (-14, 0, 0),
               'arm_r': (160, -85, 0), 'forearm_r': (8, 0, 0), 'arm_l': (160, 85, 0), 'forearm_l': (10, 0, 0)}),
    ]
    anim = keys(dict(base, **{c: np.asarray(v, np.float32) for c, v in fk.items()}), k)
    return Action(lambda t: anim(_warp(t, [(0.62, down)])), legs='fk', arms='fk')


def anim_fortify(base, weapon, offhand, ctx):
    """Takes a ready, guarded stance (and holds it at the end)."""
    B = base
    guard = {'root_pos': (0, -0.02, -0.12), 'foot_l': (-0.18, 0.18, ANKLE_H), 'foot_r': (0.17, -0.12, ANKLE_H),
             'chest': (-4, 0, 8), 'spine': (-4, 0, 4), 'head': (4, 0, -6)}
    if weapon in ('spear',):
        guard.update({'hand_r': (0.22, 0.12, 1.25), 'w_main': (-70, 0, 0)})
    elif weapon in ('stone_axe', 'axe', 'club', 'sword'):
        guard.update({'hand_r': (0.25, 0.30, 1.25), 'w_main': (-30, -20, -10)})
    if offhand in ('shield_oval', 'shield_round'):
        guard.update({'hand_l': (-0.18, 0.40, 1.10)})
    if offhand == 'bow':
        guard.update({'hand_l': (-0.22, 0.42, 1.05), 'w_off': (-40, 0, -20), 'hand_r': (0.1, 0.30, 1.05)})
    k = [(0.0, {}), (0.6, guard), (1.0, guard)]
    anim = keys(B, k)
    return Action(anim)


def anim_fidget(base, weapon, offhand, ctx):
    """Looks around, shifts its weight and resettles its gear."""
    B = base
    k = [
        (0.0, {}),
        (0.2, {'head': (0, 0, 35), 'chest': (0, 0, 8), 'root_pos': (0.03, 0, -0.01)}),
        (0.45, {'head': (-6, 0, 30), 'hand_r': B['hand_r'] + (0.0, 0.08, 0.16), 'w_main': B['w_main'] + (25, 0, 0)}),
        (0.65, {'head': (4, 0, -35), 'chest': (0, 0, -8), 'root_pos': (-0.03, 0, -0.01)}),
        (0.85, {'head': (0, 0, -10), 'hand_r': B['hand_r'], 'w_main': B['w_main']}),
        (1.0, {'head': (0, 0, 0), 'chest': (0, 0, 0), 'root_pos': (0, 0, 0)}),
    ]
    anim = keys(B, k)
    return Action(anim)


def anim_victory(base, weapon, offhand, ctx):
    """Raises the weapon (or a fist) high, twice."""
    B = base
    up = {'hand_r': (0.25, 0.12, 2.05), 'w_main': (8, -10, 0), 'chest': (8, 0, 0), 'head': (-14, 0, 0),
          'hand_l': (-0.42, 0.05, 1.45) if offhand is None else B['hand_l'] + (0, 0.05, 0.2),
          'elbow_r': (0.8, -0.3, 0.2), 'elbow_l': (-0.8, -0.3, 0.0)}
    if weapon in ('spear', 'staff'):
        up.update({'hand_r': (0.30, 0.15, 1.75), 'w_main': (-12, -15, 0)})
    if offhand == 'bow':
        up.update({'hand_l': (-0.32, 0.10, 2.0), 'w_off': (0, 20, 0), 'hand_r': (0.42, 0.05, 1.45)})
    half = {k2: (np.asarray(v, np.float32) * 0.65 + np.asarray(B[k2], np.float32) * 0.35) for k2, v in up.items()}
    k = [(0.0, {}), (0.22, dict(up, root_pos=(0, 0, 0.03))), (0.42, dict(half, root_pos=(0, 0, -0.03))),
         (0.62, dict(up, root_pos=(0, 0, 0.04))), (0.84, half), (1.0, {k2: B[k2] for k2 in up})]
    anim = keys(B, k)
    return Action(anim)


def anim_work(base, tool_motion, ctx, loop=True):
    """Worker/settler labor: a two-handed tool swung in a cycle.
    tool_motion: 'dig' (shovel), 'pick' (overhead pick), 'hoe', 'hammer'
    (kneeling), 'chop' (axe at a tree), 'machete', 'sow'."""
    B = dict(base)
    bend = {'root_pos': (0, -0.02, -0.08), 'spine': (-14, 0, 0), 'chest': (-10, 0, 0), 'head': (10, 0, 0),
            'foot_l': (-0.16, 0.16, ANKLE_H), 'foot_r': (0.15, -0.10, ANKLE_H)}
    if tool_motion == 'dig':
        k = [
            (0.0, dict(bend, hand_r=(0.18, 0.12, 1.05), hand_l=(0.02, 0.30, 0.78), w_main=(-150, 0, 0))),
            (0.25, dict(bend, hand_r=(0.16, 0.20, 0.82), hand_l=(0.03, 0.40, 0.55), w_main=(-158, 0, 0),
                        root_pos=(0, 0.02, -0.14))),
            (0.5, dict(bend, hand_r=(0.16, 0.06, 0.78), hand_l=(0.03, 0.36, 0.62), w_main=(-120, 0, 0),
                       root_pos=(0, -0.02, -0.12))),
            (0.75, dict(bend, hand_r=(0.20, 0.10, 1.10), hand_l=(0.30, 0.42, 1.05), w_main=(-85, 0, 40),
                        spine=(-6, 0, 20), chest=(-4, 0, 20))),
        ]
    elif tool_motion in ('pick', 'chop'):
        side = tool_motion == 'chop'
        up = dict(bend, hand_r=(0.14, -0.05, 1.82), hand_l=(0.06, -0.02, 1.70), w_main=(70, 0, 0),
                  spine=(4, 0, 15 if side else 0), chest=(6, 0, 10 if side else 0))
        down = dict(bend, hand_r=(0.05, 0.50, 0.75 if not side else 1.0), hand_l=(0.02, 0.42, 0.80 if not side else 1.02),
                    w_main=(-120 if not side else -90, 0, 30 if side else 0),
                    spine=(-22, 0, -10 if side else 0), chest=(-14, 0, -10 if side else 0), root_pos=(0, 0.02, -0.14))
        k = [(0.0, dict(down)), (0.35, up), (0.55, up), (0.78, down), (0.9, down)]
    elif tool_motion == 'hoe':
        up = dict(bend, hand_r=(0.18, 0.15, 1.42), hand_l=(0.08, 0.42, 1.40), w_main=(-30, 0, 0))
        down = dict(bend, hand_r=(0.15, 0.25, 0.95), hand_l=(0.06, 0.50, 0.85), w_main=(-150, 0, 0),
                    spine=(-24, 0, 0), chest=(-12, 0, 0), root_pos=(0, 0.0, -0.12))
        k = [(0.0, down), (0.4, up), (0.7, down), (0.85, dict(down, hand_r=(0.15, 0.10, 0.95), hand_l=(0.06, 0.32, 0.88)))]
    elif tool_motion == 'hammer':
        kneel = {'root_pos': (0, -0.05, -0.45), 'foot_l': (-0.16, 0.30, ANKLE_H), 'foot_r': (0.14, -0.30, 0.10),
                 'toe_r': 40.0, 'spine': (-20, 0, 0), 'chest': (-14, 0, 0), 'head': (16, 0, 0),
                 'hand_l': (-0.08, 0.42, 0.42)}
        k = [(0.0, dict(kneel, hand_r=(0.18, 0.30, 0.95), w_main=(30, 0, 0))),
             (0.35, dict(kneel, hand_r=(0.15, 0.40, 0.48), w_main=(-90, 0, 0))),
             (0.5, dict(kneel, hand_r=(0.15, 0.40, 0.50), w_main=(-85, 0, 0))),
             (0.8, dict(kneel, hand_r=(0.20, 0.28, 0.98), w_main=(35, 0, 0)))]
    elif tool_motion == 'machete':
        k = [(0.0, dict(bend, hand_r=(0.40, 0.20, 1.45), w_main=(30, -50, -20), hand_l=(-0.2, 0.35, 1.0))),
             (0.4, dict(bend, hand_r=(-0.10, 0.50, 0.85), w_main=(-110, 50, 30), hand_l=(-0.3, 0.2, 0.95),
                        chest=(-14, 0, -20))),
             (0.6, dict(bend, hand_r=(-0.12, 0.45, 0.85), w_main=(-120, 55, 35), hand_l=(-0.3, 0.2, 0.95),
                        chest=(-14, 0, -20))),
             (0.85, dict(bend, hand_r=(0.38, 0.22, 1.40), w_main=(25, -45, -20), hand_l=(-0.2, 0.35, 1.0)))]
    elif tool_motion == 'sow':
        walk = dict(bend, spine=(-6, 0, 0), chest=(-4, 0, 0))
        k = [(0.0, dict(walk, hand_r=(0.25, 0.25, 1.05), hand_l=(-0.22, 0.15, 1.0))),
             (0.3, dict(walk, hand_r=(0.05, 0.22, 1.02), chest=(-4, 0, 15))),
             (0.55, dict(walk, hand_r=(0.55, 0.40, 1.05), chest=(-4, 0, -25), elbow_r=(0.5, -0.7, 0.0))),
             (0.8, dict(walk, hand_r=(0.32, 0.30, 1.05)))]
    else:
        raise ValueError(tool_motion)
    # Work off to the right side, turned a little that way, like the originals:
    # a tool swung straight toward the camera would hide behind the body.
    side = {'hammer': 0.10, 'sow': 0.0}.get(tool_motion, 0.18)
    for _, kk in k:
        for c, dx in (('hand_r', side), ('hand_l', side * 0.7)):
            if c in kk:
                kk[c] = np.asarray(kk[c], np.float32) + np.array([dx, 0, 0], np.float32)
        if 'w_main' in kk:
            kk['w_main'] = np.asarray(kk['w_main'], np.float32) + np.array([0, 0, -18 * side / 0.18], np.float32)
        kk['chest'] = np.asarray(kk.get('chest', (0, 0, 0)), np.float32) + np.array([0, 0, -10 * side / 0.18], np.float32)
    anim = keys(B, k, loop=loop)
    return Action(anim, loop=loop)


def anim_build(base, ctx):
    """A settler takes off its pack and crouches to set up camp (one-shot)."""
    B = base
    crouch = {'root_pos': (0, 0.0, -0.40), 'foot_l': (-0.18, 0.20, ANKLE_H), 'foot_r': (0.17, -0.18, ANKLE_H),
              'spine': (-26, 0, 0), 'chest': (-14, 0, 0), 'head': (18, 0, 0),
              'hand_l': (-0.18, 0.45, 0.35), 'toe_r': 25.0}
    k = [(0.0, {}),
         (0.25, {'hand_l': (-0.20, 0.0, 1.45), 'chest': (4, 0, -14), 'head': (-4, 0, -10)}),
         (0.5, {'hand_l': (-0.30, -0.15, 1.30), 'chest': (0, 0, -24), 'pack': 1.0}),
         (0.75, dict(crouch, pack=1.0)),
         (1.0, dict(crouch, pack=1.0, hand_l=(-0.10, 0.48, 0.38)))]
    anim = keys(dict(B, pack=np.float32(0)), k)
    return Action(anim)


def anim_captured(base, ctx):
    """Hands raised, cowering a little (a captured civilian)."""
    B = base
    up = {'hand_l': (-0.30, 0.18, 1.75), 'elbow_l': (-0.8, -0.3, -0.2), 'head': (12, 0, 0), 'chest': (-6, 0, 0),
          'root_pos': (0, -0.04, -0.06)}
    k = [(0.0, {}), (0.3, up), (0.5, dict(up, head=(12, 0, 20))), (0.7, dict(up, head=(12, 0, -20))), (1.0, up)]
    anim = keys(B, k)
    return Action(anim)


# ------------------------------------------------------------ the archetype

# INI action -> animation name, by default
DEFAULT_ACTIONS = {
    'DEFAULT': 'idle', 'RUN': 'run', 'WALK': 'run', 'ATTACK1': 'attack_chop', 'ATTACK2': 'attack_chop',
    'ATTACK3': 'attack_chop', 'DEATH': 'death', 'FORTIFY': 'fortify', 'FIDGET': 'fidget', 'VICTORY': 'victory',
    'BUILD': 'build', 'CAPTURE': 'captured', 'ROAD': 'work_dig', 'MINE': 'work_pick', 'IRRIGATE': 'work_hoe',
    'FORTRESS': 'work_hammer', 'JUNGLE': 'work_machete', 'FOREST': 'work_chop', 'PLANT': 'work_sow',
}
# The tool a worker holds for each labor (replacing its weapon).
WORK_TOOLS = {'work_dig': 'shovel', 'work_pick': 'pickaxe', 'work_hoe': 'hoe', 'work_hammer': 'hammer',
              'work_machete': 'machete', 'work_chop': 'axe', 'work_sow': 'seed_bag'}


@archetype('humanoid')
class Humanoid:
    def __init__(self, unit, params):
        self.unit = unit
        self.p = params
        self.body = Body(params)
        self.mats = self.body.mats
        h = build_params(params).get('height', 1.0)
        self.h = h
        self.skel = _mk_skeleton(h, shoulders=0.025 * muscle_amount(params))
        self.parts = self.body.parts()
        self.weapon = params.get('weapon')
        self.offhand = params.get('offhand')
        self.actions = dict(DEFAULT_ACTIONS)
        self.actions.update(params.get('actions', {}))
        self.base = stance(self.weapon, self.offhand)
        self.base.update({k: np.asarray(v, np.float32) for k, v in params.get('stance', {}).items()})
        self._props = {}

    # -- props (cached meshes)
    def prop(self, name, **kw):
        key = (name, tuple(sorted(kw.items())))
        if key not in self._props:
            self._props[key] = make_prop(name, self.mats, **kw)
        return self._props[key]

    def anim_name(self, action):
        return self.actions.get(action, 'idle')

    def loops(self, action):
        return self.anim_name(action) in ('idle', 'run') or self.anim_name(action).startswith('work_')

    def action(self, action, ctx):
        name = self.anim_name(action)
        w, o, B = self.weapon, self.offhand, self.base
        if name == 'idle':
            return anim_idle(B)
        if name == 'run':
            return anim_run(B, w, o, ctx.n if ctx else 10)
        if name.startswith('attack_'):
            return anim_attack(B, w, o, name[7:], ctx)
        if name == 'death':
            return anim_death(B, w, o, ctx)
        if name == 'fortify':
            return anim_fortify(B, w, o, ctx)
        if name == 'fidget':
            return anim_fidget(B, w, o, ctx)
        if name == 'victory':
            return anim_victory(B, w, o, ctx)
        if name == 'build':
            return anim_build(B, ctx)
        if name == 'captured':
            return anim_captured(B, ctx)
        if name.startswith('work_'):
            return anim_work(B, name[5:], ctx)
        raise ValueError(f'{self.unit}: unknown animation {name}')

    def frame(self, action, f, n, ctx):
        act = self.action(action, ctx)
        t = rig.frame_times(n, act.loop)[f]
        pose = act.fn(t)
        weapon = self.weapon
        name = self.anim_name(action)
        if name in WORK_TOOLS:
            weapon = WORK_TOOLS[name]
        return self.pose_mesh(pose, act, weapon)

    # -- posing
    def pose_mesh(self, pose, act, weapon):
        h = self.h
        sk = self.skel
        pose = dict(pose)
        root_pos = np.asarray(pose.get('root_pos', (0, 0, 0)), np.float32)
        fk = {}
        for b in ('pelvis', 'spine', 'chest', 'neck', 'head'):
            fk[b] = pose.get(b, (0, 0, 0))
        fk['pelvis'] = np.zeros(3, np.float32)
        base_pose = {'root_pos': root_pos, 'root_rot': pose.get('root_rot', (0, 0, 0))}
        base_pose.update({k: v for k, v in fk.items()})
        limb_fk = {}
        for b in ('arm_r', 'forearm_r', 'arm_l', 'forearm_l', 'thigh_r', 'shin_r', 'thigh_l', 'shin_l',
                  'foot_r', 'foot_l', 'hand_r', 'hand_l'):
            limb_fk[b] = pose.get(b, (0, 0, 0))
        # rest-pose arms hang slightly outward
        limb_fk.setdefault('arm_r', (0, -8, 0))
        W0 = sk.world(dict(base_pose, **limb_fk))
        over = {}
        if act.legs == 'ik':
            for side in 'rl':
                hip = W0[f'thigh_{side}'][:3, 3]
                target = np.asarray(pose[f'foot_{side}'], np.float32) * np.array([1, 1, 1], np.float32)
                target = target.copy()
                target[2] = target[2] * h if target[2] > ANKLE_H * 1.5 else target[2] * h
                pf = W0['pelvis'][:3, 1]
                pole = _normalize(pf + np.array([0.15 if side == 'r' else -0.15, 0, 0], np.float32))
                Ru, Rl, _, _ = two_bone(hip, target, THIGH * h, SHIN * h, pole, bend_forward=False)
                over[f'thigh_{side}'] = Ru
                over[f'shin_{side}'] = Rl
                yaw = float(pose.get(f'fyaw_{side}', 0.0))
                yaw += float(np.degrees(np.arctan2(-pf[0], pf[1])))
                over[f'foot_{side}'] = (M.rot_z(yaw) @ M.rot_x(float(pose.get(f'toe_{side}', 0.0))))[:3, :3]
        grip_pts = {}
        if act.arms == 'ik':
            for side in 'rl':
                sh = W0[f'arm_{side}'][:3, 3]
                grip = np.asarray(pose[f'hand_{side}'], np.float32) * np.array([1, 1, h], np.float32)
                pole = _normalize(np.asarray(pose.get(f'elbow_{side}', (0, -1, -0.3)), np.float32))
                # aim the wrist so that the hand's center lands on the grip
                d = _normalize(grip - sh)
                wrist = grip - d * GRIP
                Ru, Rl, _, end = two_bone(sh, wrist, UPPER_ARM * h, FOREARM * h, pole, bend_forward=True)
                over[f'arm_{side}'] = Ru
                over[f'forearm_{side}'] = Rl
                over[f'hand_{side}'] = Rl
        W = _world_with(sk, dict(base_pose, **limb_fk), over)
        meshes = []
        hide = set(act.hide)
        if float(pose.get('pack', 0.0)) > 0.5:
            hide.add('backpack')
        for bone, m, tag in self.parts:
            if tag in hide:
                continue
            meshes.append(m.transformed(W[bone]))
        # props in the hands
        for side, item, chan in (('r', weapon, 'w_main'), ('l', self.offhand, 'w_off')):
            if item is None:
                continue
            Wh = W[f'hand_{side}']
            grip = Wh[:3, 3] + Wh[:3, :3] @ np.array([0, 0.01, -GRIP], np.float32)
            if item in ('shield_oval', 'shield_round'):
                # strapped to the forearm, its face turned outward and forward
                Wf = W[f'forearm_{side}']
                A = np.eye(4, dtype=np.float32)
                A[:3, :3] = np.array([[0, 1, 0], [0, 0, 1], [1, 0, 0]], np.float32)
                off = M.translate(-0.085, 0.0, -0.13) @ A @ M.rot_z(-30) if side == 'l' else np.eye(4)
                meshes.append(self.prop(item).transformed(Wf @ off))
                continue
            if item == 'bow':
                draw = float(np.clip(pose.get('draw', 0.0), 0, 1))
                mesh = make_prop('bow', self.mats, draw=round(draw * 8) / 8)
            else:
                mesh = self.prop(item)
            R = M.euler(*np.asarray(pose.get(chan, (0, 0, 0)), np.float32))
            T = M.translate(*grip) @ R
            meshes.append(mesh.transformed(T))
        if float(pose.get('pack', 0.0)) > 0.5 and self.p.get('back') == 'backpack':
            meshes.append(P.backpack(self.mats).transformed(M.translate(0.15, 0.62, -0.17) @ M.rot_x(-80)))
        return M.Mesh.concat(meshes)


def _world_with(sk, pose, over):
    """Skeleton world transforms with some bones' world rotations overridden."""
    out = {}
    root_t = np.asarray(pose.get('root_pos', (0, 0, 0)), np.float32)
    root_r = pose.get('root_rot', (0, 0, 0))
    for name in sk.order:
        parent, off = sk.bones[name]
        r = pose.get(name, (0, 0, 0))
        if parent is None:
            T = M.translate(*root_t) @ M.translate(*off) @ M.euler(*root_r)
        else:
            T = out[parent] @ M.translate(*off) @ M.euler(*r)
        if name in over:
            T = T.copy()
            T[:3, :3] = over[name]
        out[name] = T
    return out
