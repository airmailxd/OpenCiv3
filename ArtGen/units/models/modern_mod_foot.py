"""Modern's foot units (catalog/modern_mod_foot.py): the soldier of
models/foot_modern.py with gear of its own, and the Battalion's machine-gun
team.

Archetypes:
    'mod_soldier'  foot_modern's 'soldier' plus:
        hat     'shako' | 'turban' | 'balaclava' | 'power' (a full helmet with
                a visor) as well as the soldier's
        armor   'power': plates over the chest, shoulders, thighs and shins
                (material 'plate', usually civ-colored), dark gauntlets,
                knees and boots ('armor_dark'), and a power pack
        back    'tanks' (flamethrower fuel) | 'rockets' (spare RPG rounds)
        gun     also a modern_mod_foot_gear.GUNS name
        flash   also 'laser' (a beam), 'flame' (a jet of fire), 'rocket'
                (backblast, the round in flight, the blast where it lands)
        kneel_fire  bool: fires kneeling
        beam_len, flame_len  meters
    'hmg_team'   a machine gunner and his ammunition bearer: on the march
                 the gun on the gunner's shoulder and a can in each of the
                 bearer's hands; in action the gun on its tripod, the gunner
                 kneeling behind it, the bearer beside him feeding the belt.
"""
import math

import numpy as np

import mesh as M
import render as R
import rig
from models import archetype
from models import humanoid as H
from models import foot_modern as FM
from models import props_foot_modern as G
from models import modern_mod_foot_gear as MG
from models.foot_ancient import finish_colors

A = H.ANKLE_H
_v = FM._v


# ------------------------------------------------------------ the body

class ModBody(FM.SoldierBody):
    def parts(self):
        out = super().parts()
        p = self.full
        bulk = H.build_params(p).get('bulk', 1.0)
        tw = H.TORSO_W * bulk
        lb = H.LIMB * bulk

        def add(bone, mesh, tag=None):
            out.append((bone, mesh, tag))

        if p.get('armor') == 'power':
            pl = self.mm('plate', '#3c4aa0', spec=0.55, gloss=26)
            dk = self.mm('armor_dark', '#34363c', spec=0.45, gloss=22, metal=True)
            cr = [(0.0, 0.17, 0.11), (0.10, 0.20, 0.13), (0.19, 0.215, 0.13), (0.26, 0.19, 0.115), (0.31, 0.11, 0.08)]
            g = 0.05
            add('chest', M.loft([(z, rx * tw + g, ry * (1 + (tw - 1) * 0.6) + g * 1.2, 0, 0.01) for z, rx, ry in cr],
                                pl, seg=16, cap_bottom=True, cap_top=True))
            # the abdomen's segmented plates
            for i, z in enumerate((-0.02, 0.07, 0.16)):
                add('spine', M.cylinder(0.17 * tw + 0.035, 0.17 * tw + 0.03, 0.075, dk, seg=14, z0=z).transformed(
                    M.scale(1, 0.75, 1)))
            add('pelvis', M.loft([(-0.14, 0.19 * tw + 0.03, 0.14 * tw + 0.03), (0.06, 0.19 * tw + 0.03, 0.14 * tw + 0.03)],
                                 dk, seg=14, cap_bottom=True, cap_top=False))
            for side, sx in (('r', 1), ('l', -1)):
                # pauldrons: big domes over the shoulders
                add(f'arm_{side}', M.ellipsoid(0.13 * lb / 1.35 + 0.03, 0.13, 0.11, pl, (sx * 0.02, 0.0, 0.02), seg=12))
                add(f'arm_{side}', M.capsule((0, 0, -0.05), (0, 0, -0.22), 0.08 * lb + 0.012, 0.072 * lb + 0.012, dk, seg=10, rings=2))
                add(f'forearm_{side}', M.capsule((0, 0, -0.02), (0, 0, -0.22), 0.068 * lb + 0.014, 0.06 * lb + 0.014, dk, seg=10, rings=2))
                add(f'thigh_{side}', M.capsule((0, 0.01, -0.02), (0, 0.01, -H.THIGH + 0.06), 0.098 * lb + 0.018, 0.08 * lb + 0.018, pl, seg=12, rings=2))
                add(f'shin_{side}', M.sphere(0.07 * lb + 0.02, dk, (0, 0.03, 0.0), seg=10))                     # knee
                add(f'shin_{side}', M.capsule((0, 0.01, -0.07), (0, 0.01, -H.SHIN + 0.14), 0.075 * lb + 0.018, 0.066 * lb + 0.018, pl, seg=12, rings=2))
                add(f'foot_{side}', M.rounded_box(0.13 * H.FOOT, 0.28 * H.FOOT, 0.11, dk, (0, 0.05, -0.03), r=0.035, seg=10, p=3))
            add('chest', MG.power_pack(self.mats), 'backpack')
        back = p.get('back_gear')
        if back == 'tanks':
            add('chest', MG.fuel_tanks(self.mats), 'backpack')
        elif back == 'rockets':
            add('chest', MG.rocket_bag(self.mats), 'backpack')
        return out

    def _hat(self, add0):
        def add(mesh, tag='hat'):
            add0('head', mesh.transformed(M.scale(H.HEAD)), tag)
        p = self.full
        hat = p.get('hat')
        if hat == 'shako':
            hm = self.mm('hat', '#1e1e22', spec=0.25, gloss=18)
            # a tall cylinder, a little wider at the top, tilted back a touch
            add(M.loft([(0.10, 0.118, 0.128), (0.20, 0.122, 0.132), (0.33, 0.13, 0.14)], hm, seg=18,
                       cap_bottom=False, cap_top=True).transformed(M.rot_x(-4)))
            vm = self.mm('visor', '#141416', spec=0.5, gloss=26)
            add(M.ellipsoid(0.11, 0.07, 0.012, vm, (0, 0.13, 0.11), seg=12).transformed(
                M.translate(0, 0.13, 0.11) @ M.rot_x(18) @ M.translate(0, -0.13, -0.11)))
            bm = self.mm('badge', '#c8a050', spec=0.8, gloss=28, metal=True)
            add(M.extrude([(-0.045, 0.0), (0.045, 0.0), (0.035, 0.07), (0.0, 0.09), (-0.035, 0.07)], 0.01, bm)
                .transformed(M.translate(0, 0.137, 0.17) @ M.rot_x(90) @ M.rot_x(-4)))
            add(M.cylinder(0.131, 0.131, 0.025, self.mm('cord', '#e8e4dc'), seg=18, z0=0.30).transformed(M.rot_x(-4)))
            pm = self.mm('plume', '#a83228')
            add(M.ellipsoid(0.045, 0.045, 0.07, pm, (0, 0.11, 0.39), seg=10))
            add(M.capsule((0, 0.11, 0.33), (0, 0.11, 0.44), 0.02, 0.03, pm, seg=8, rings=1))
        elif hat == 'turban':
            hm = self.mm('hat', '#ffffff')
            for k, (z, r) in enumerate(((0.12, 0.128), (0.17, 0.13), (0.22, 0.118))):
                add(M.ellipsoid(r, r * 1.1, 0.05, hm, (0, -0.008, z), seg=14).transformed(
                    M.translate(0, 0, z) @ M.rot_y(8 if k % 2 else -8) @ M.translate(0, 0, -z)))
            add(M.ellipsoid(0.1, 0.11, 0.06, hm, (0, -0.01, 0.255), seg=12))
            # the loose end over the left shoulder
            add(M.capsule((-0.10, -0.08, 0.14), (-0.14, -0.10, -0.06), 0.035, 0.03, hm, seg=6, rings=1))
        elif hat == 'balaclava':
            hm = self.mm('hat', '#26272a', spec=0.04)
            add(M.ellipsoid(0.106, 0.122, 0.13, hm, (0, 0.006, 0.12), seg=14))
            add(M.ellipsoid(0.09, 0.095, 0.08, hm, (0, 0.03, 0.045), seg=12))     # over the jaw
            add(M.cylinder(0.07, 0.075, 0.10, hm, seg=10, z0=-0.06))               # the neck
            sk = self.mm('skin', '#c8a07a')
            add(M.ellipsoid(0.07, 0.03, 0.022, sk, (0, 0.104, 0.122), seg=10))    # the eye slit
            for sx in (1, -1):
                add(M.ellipsoid(0.016, 0.01, 0.011, self.mm('eyes', '#2a1a12'), (sx * 0.035, 0.124, 0.122), seg=6))
        elif hat == 'power':
            hm = self.mm('helmet', '#3c4aa0', spec=0.55, gloss=26)
            add(M.ellipsoid(0.122, 0.136, 0.15, hm, (0, -0.005, 0.125), seg=16))
            vm = self.mm('visor', '#9ac4dc', spec=0.9, gloss=40)
            add(M.rounded_box(0.16, 0.05, 0.045, vm, (0, 0.118, 0.13), r=0.02, seg=8, p=3))
            dk = self.mm('armor_dark', '#34363c', spec=0.45, gloss=22, metal=True)
            add(M.rounded_box(0.12, 0.06, 0.07, dk, (0, 0.11, 0.035), r=0.025, seg=8, p=3))   # the jaw guard
            add(M.cylinder(0.10, 0.12, 0.10, dk, seg=12, z0=-0.07))                          # the gorget
            for sx in (1, -1):
                add(M.cylinder(0.035, 0.035, 0.03, dk, seg=10).transformed(
                    M.translate(sx * 0.122, 0.0, 0.11) @ M.rot_y(90)))
        else:
            super()._hat(add0)


# ------------------------------------------------------------ the soldier

def _beam_fx(pos, d, length, width, col, alpha=1.0):
    return {'kind': 'beam', 'a': pos, 'b': pos + d * length, 'width': width, 'col': col, 'alpha': alpha}


def _glow_fx(pos, rad, col, alpha=1.0):
    return {'kind': 'glow', 'pos': pos, 'rad': rad, 'col': col, 'alpha': alpha}


@archetype('mod_soldier')
class ModSoldier(FM.Soldier):
    def __init__(self, unit, params):
        super().__init__(unit, params)
        bp = dict(params)
        bp['weapon'] = params.get('weapon', params.get('gun'))
        bp['offhand'] = params.get('offhand')
        self.body = ModBody(bp)
        self.mats = self.body.mats
        self.parts = self.body.parts()
        self._props = {}
        if self.gun in MG.GUNS:
            self.gspec = MG.GUNS[self.gun]
        if params.get('kneel_fire'):
            self.poses['aim'] = FM._kneel(self.poses['aim'])
        if params.get('stoop'):
            s = float(params['stoop'])
            self.base['spine'] = _v((-s, 0, 0))
            self.base['chest'] = _v((-s * 0.6, 0, 0))
            self.base['head'] = _v((s * 1.2, 0, 0))

    def prop(self, name, **kw):
        fn = getattr(MG, name, None)
        if fn is None or name.startswith('_'):
            return super().prop(name, **kw)
        key = (name, tuple(sorted(kw.items())))
        if key not in self._props:
            m = fn(self.mats, **kw)
            if name in MG.GUNS:
                k = float(self.p.get('gun_thick', 1.6))
                m = m.transformed(M.scale(k, k, 1.0))
            self._props[key] = m
        return self._props[key]

    def frame(self, action, f, n, ctx):
        self._n = n
        return super().frame(action, f, n, ctx)

    def effects(self, action, f, n, act, info, ctx):
        style = self.p.get('flash', 'burst')
        if style not in ('laser', 'flame', 'rocket'):
            return super().effects(action, f, n, act, info, ctx)
        shots = self.shots(action)
        if not shots or not self.gun:
            return []
        fx = []
        pos, d = self.muzzle(info)
        if style == 'laser':
            if f in shots:
                k = shots.index(f)
                first = k == 0 or shots[k - 1] != f - 1
                L = float(self.p.get('beam_len', 1.6)) * (0.6 if first else 1.0)
                fx.append(_beam_fx(pos, d, L, 0.07, (0.62, 0.86, 1.0)))
                fx.append(_beam_fx(pos, d, L, 0.025, (0.95, 0.98, 1.0)))
                fx.append(_glow_fx(pos, 0.11, (0.75, 0.92, 1.0)))
        elif style == 'flame':
            s0, s1 = shots[0], shots[-1]
            if s0 <= f <= s1 + 1:
                grow = min(1.0, (f - s0 + 1) / 4.0)
                tail = max(0.0, (f - (s1 - 2)) / 4.0)      # the jet leaves the nozzle at the end
                Lmax = float(self.p.get('flame_len', 1.9))
                a, b = Lmax * min(1.0, tail), Lmax * grow
                k = 28
                rng = np.random.default_rng(f * 7 + 3)
                for i in range(k):
                    s = a + (b - a) * i / (k - 1)
                    if s < 0.02:
                        continue
                    u = s / Lmax
                    c = pos + d * s + _v((0, 0, -0.10 * u * u + 0.10 * u)) + _v(rng.uniform(-0.04, 0.04, 3)) * (0.5 + u)
                    fx.append(_glow_fx(c, 0.08 + 0.20 * u, (0.98, 0.62 - 0.22 * u, 0.16), 0.9 - 0.3 * u))
                    fx.append(_glow_fx(c, 0.05 + 0.11 * u, (1.0, 0.93 - 0.2 * u, 0.62 - 0.35 * u), 1.0 - 0.35 * u))
                if b > 1.0:
                    fx.append({'kind': 'smoke', 'pos': pos + d * b + _v((0, 0, 0.18)), 'rad': 0.16, 'alpha': 0.35,
                               'seed': f})
        elif style == 'rocket':
            s0 = shots[0]
            back = pos - d * (self.gspec['muzzle'] - self.gspec['butt'])
            if f == s0:
                fx.append({'kind': 'flash', 'pos': pos, 'dir': d, 'size': 0.32})
                fx.append({'kind': 'flash', 'pos': back, 'dir': -d, 'size': 0.55})
            if f >= s0:
                age = f - s0
                u = min(1.0, age / 6.0)
                for j, (off, rr) in enumerate(((0.3, 0.2), (0.6, 0.17))):
                    fx.append({'kind': 'smoke', 'pos': back - d * off * (0.6 + u) + _v((0, 0, 0.1 * u)),
                               'rad': rr * (0.7 + u), 'alpha': 0.55 * (1 - u) ** 1.3, 'seed': j + 1})
            land = float(self.p.get('impact', 3.2))
            fly = int(self.p.get('flight', 4))
            if s0 < f <= s0 + fly:
                s = land * (f - s0) / fly
                c = pos + d * s
                fx.append({'kind': 'round', 'pos': c, 'dir': d})
                fx.append(_glow_fx(c - d * 0.12, 0.07, (1.0, 0.85, 0.5)))
            if s0 + fly < f:
                age = f - s0 - fly
                hit = _v(pos + d * land)
                hit[2] = 0.15
                life = n - 1 - (s0 + fly)
                u = age / max(life, 1)
                if age <= 3:
                    r = 0.28 + 0.13 * age
                    for j, off in enumerate(((0, 0, 0.08), (0.16, 0.05, 0.02), (-0.14, -0.06, 0.04), (0.02, -0.12, 0.2))):
                        c = hit + _v(off) * (0.6 + 0.3 * age) + _v((0, 0, 0.1 * age))
                        fx.append(_glow_fx(c, r * (0.9 if j else 1.1), (0.95, 0.45, 0.12), 1.0 - 0.2 * age))
                        fx.append(_glow_fx(c, r * 0.5, (1.0, 0.88, 0.55), 1.0 - 0.28 * age))
                fx.append({'kind': 'smoke', 'pos': hit + _v((0, 0, 0.25 + 0.3 * u)), 'rad': 0.24 * (0.6 + u),
                           'alpha': 0.55 * (1 - u) ** 1.2, 'seed': 7})
        return fx

    def render_cell(self, mesh, mats, cam, W, H_):
        b, t = R.render_cell(mesh, mats, cam, W, H_)
        if not self._fx:
            return b, t
        return draw_all(b, t, self._fx, FM._facing_of(self._local, mesh), cam, self.mats)


def draw_all(b, t, fx, yaw, cam, mats=None):
    """foot_modern's flashes and smoke, then beams, glows and rounds in flight."""
    std = [e for e in fx if e['kind'] in ('flash', 'smoke')]
    own = [e for e in fx if e['kind'] not in ('flash', 'smoke')]
    if std:
        b, t = FM.draw_effects(b, t, std, yaw, cam)
    if not own:
        return b, t
    Hh, Ww = b.shape[:2]
    c, s = math.cos(yaw), math.sin(yaw)
    Rz = np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]], np.float32)
    yy, xx = np.mgrid[0:Hh, 0:Ww].astype(np.float32) + 0.5
    over_p = np.zeros((Hh, Ww, 3), np.float32)
    over_a = np.zeros((Hh, Ww), np.float32)
    ppm = cam.ppm

    def put(col, a):
        over_p[:] = np.asarray(col, np.float32) * a[..., None] + over_p * (1 - a[..., None])
        over_a[:] = a + over_a * (1 - a)

    for e in own:
        if e['kind'] == 'beam':
            pa, pb = cam.project(Rz @ _v(e['a'])), cam.project(Rz @ _v(e['b']))
            ab = np.array([pb[0] - pa[0], pb[1] - pa[1]], np.float32)
            L2 = float(ab @ ab) + 1e-6
            u = np.clip(((xx - pa[0]) * ab[0] + (yy - pa[1]) * ab[1]) / L2, 0, 1)
            dx, dy = xx - (pa[0] + u * ab[0]), yy - (pa[1] + u * ab[1])
            w = max(e['width'] * ppm, 1.2)
            a = np.exp(-(dx * dx + dy * dy) / (w * w)) * e.get('alpha', 1.0)
            a = np.where(a > 0.03, np.clip(a * 1.4, 0, 1), 0)
            put(e['col'], a)
        elif e['kind'] == 'glow':
            sp = cam.project(Rz @ _v(e['pos']))
            r = max(e['rad'] * ppm, 1.5)
            d2 = ((xx - sp[0]) ** 2 + (yy - sp[1]) ** 2) / (r * r)
            a = np.clip(1.6 * np.exp(-2.2 * d2 ** 1.4), 0, 1) * e.get('alpha', 1.0)
            a = np.where(a > 0.03, a, 0)
            put(e['col'], a)
        elif e['kind'] == 'round':
            p0 = cam.project(Rz @ _v(e['pos']))
            p1 = cam.project(Rz @ (_v(e['pos']) - _v(e['dir']) * 0.30))
            ab = np.array([p1[0] - p0[0], p1[1] - p0[1]], np.float32)
            L2 = float(ab @ ab) + 1e-6
            u = np.clip(((xx - p0[0]) * ab[0] + (yy - p0[1]) * ab[1]) / L2, 0, 1)
            dx, dy = xx - (p0[0] + u * ab[0]), yy - (p0[1] + u * ab[1])
            w = max(0.05 * ppm, 1.4)
            a = np.clip(1.5 - np.sqrt(dx * dx + dy * dy) / w, 0, 1)
            put((0.30, 0.32, 0.24), a)
    bf = b.astype(np.float32) / 255
    tf = t.astype(np.float32) / 255
    Ba = bf[..., 3]
    Bp = bf[..., :3] * Ba[..., None]
    Bp = over_p + Bp * (1 - over_a[..., None])
    Ba2 = over_a + Ba * (1 - over_a)
    out_b = np.zeros_like(bf)
    out_b[..., :3] = Bp / np.maximum(Ba2, 1e-6)[..., None]
    out_b[..., 3] = Ba2
    out_t = tf.copy()
    out_t[..., 3] = tf[..., 3] * (1 - over_a)
    return R._u8(out_b), R._u8(out_t)


# ------------------------------------------------------------ the machine-gun team

@archetype('hmg_team')
class HmgTeam:
    """The gunner (right, ahead) and the bearer (left, behind)."""

    def __init__(self, unit, params):
        self.unit = unit
        self.p = params
        sp = dict(params.get('soldier', {}))
        self.gunner = ModSoldier(unit, dict(sp, gun='hmg', hold='shoulder', **params.get('gunner', {})))
        self.bearer = ModSoldier(unit, dict(sp, gun=None, **params.get('bearer', {})))
        self.mats = self.gunner.mats
        # one material table: the bearer's parts are remapped into the gunner's
        self.bearer.mats = self.mats
        self.bearer.body.mats = self.mats
        self.bearer.parts = self.gunner.parts
        self.gunner_at = _v(params.get('gunner_at', (0.32, 0.10, 0.0)))
        self.bearer_at = _v(params.get('bearer_at', (-0.42, -0.12, 0.0)))
        self.mount = _v(params.get('mount', (0.10, 0.45, 0.0)))
        self.gun_h = float(params.get('gun_h', 0.66))
        self._fx = []
        self._local = None

    def loops(self, action):
        return action in ('DEFAULT', 'RUN', 'FIDGET')

    def shots(self, action):
        return list(self.p.get('shots', {}).get(action, []))

    def _hmg(self):
        return self.gunner.prop('hmg')

    def _crate(self):
        return MG.ammo_crate(self.mats)

    def _carry_pose(self, who, t, n, run=False, phase=0.0):
        if run:
            p = H.anim_run(who.base, 'spear', 'shield_round', n).fn((t + phase) % 1.0)
        else:
            p = H.anim_idle(who.base).fn((t + phase) % 1.0)
        p['follow'] = _v(1.0)
        return p

    def _with_crates(self, mesh, info):
        out = [mesh]
        for side in 'rl':
            T = info['T'][side]
            out.append(self._crate().transformed(M.translate(*T[:3, 3]) @ M.rot_z(0)))
        return M.Mesh.concat(out)

    def _kneel_gunner(self, recoil=0.0):
        B = self.gunner.base
        k = {'root_pos': (0.0, -0.05, -0.44), 'foot_l': (-0.17, 0.24, A), 'foot_r': (0.18, -0.38, 0.11),
             'toe_r': 45.0, 'fyaw_r': -15.0, 'spine': (-16, 0, 0), 'chest': (-10, 0, 0), 'head': (8, 0, 0),
             'elbow_r': (0.8, -0.4, -0.4), 'elbow_l': (-0.8, -0.4, -0.4)}
        p = dict(B)
        p.update({c: _v(v) for c, v in k.items()})
        p['follow'] = _v(0.0)
        p['support'] = _v(0.0)
        p['root_pos'] = p['root_pos'] + _v((0, -0.03, 0)) * recoil
        return p

    def _kneel_bearer(self, t):
        B = self.bearer.base
        k = {'root_pos': (0.0, 0.0, -0.46), 'foot_l': (-0.17, 0.26, A), 'foot_r': (0.18, -0.36, 0.11),
             'toe_r': 45.0, 'fyaw_r': -10.0, 'spine': (-12, 0, 0), 'chest': (-6, 0, -10), 'head': (6, 0, 0),
             'hand_r': (0.34, 0.40, 0.62 + 0.02 * math.sin(2 * math.pi * t)), 'hand_l': (0.12, 0.42, 0.60),
             'elbow_r': (0.6, -0.6, -0.4), 'elbow_l': (-0.6, -0.6, -0.4)}
        p = dict(B)
        p.update({c: _v(v) for c, v in k.items()})
        p['follow'] = _v(0.0)
        return p

    def frame(self, action, f, n, ctx):
        mesh = self._frame(action, f, n, ctx)
        finish_colors(self, dict(self.p.get('soldier', {}), **{k: v for k, v in self.p.items() if k != 'soldier'}))
        return mesh

    def _deployed(self, t, f, n, action, setup=1.0):
        """The gun on its tripod, the gunner kneeling at it, the bearer feeding."""
        meshes = []
        Gn = self.gunner
        mount = self.mount
        meshes.append(G.tow_tripod(self.mats, h=self.gun_h - 0.06).transformed(M.translate(*mount)))
        shots = self.shots(action)
        recoil = Gn._recoil(t, n, shots) if shots else 0.0
        gz = self.gun_h
        gun_T = M.translate(mount[0], mount[1] - 0.04 * recoil, gz) @ M.rot_x(-90)
        meshes.append(self._hmg().transformed(gun_T))
        # the gunner behind, hands on the grip and the top cover
        gpos = mount + _v((-0.02, -0.70, 0))
        p = self._kneel_gunner(recoil)
        grip = _v((mount[0], mount[1] - 0.20, gz + 0.04)) - gpos
        p['hand_r'] = grip + _v((0.03, 0.0, 0.0))
        p['hand_l'] = grip + _v((-0.08, 0.16, 0.05))
        gm, _ = Gn.pose_full(p, FM.Act(None), None, None)
        meshes.append(gm.transformed(M.translate(*gpos)))
        # the bearer on the gun's left, a can by him
        bpos = mount + _v((-0.62, -0.25, 0))
        bp = self._kneel_bearer(t)
        bm, _ = self.bearer.pose_full(bp, FM.Act(None), None, None)
        meshes.append(bm.transformed(M.translate(*bpos) @ M.rot_z(-35)))
        meshes.append(self._crate().transformed(M.translate(*(mount + _v((-0.30, 0.05, 0.20)))) @ M.rot_z(20)))
        meshes.append(self._crate().transformed(M.translate(*(mount + _v((-0.95, 0.15, 0.20)))) @ M.rot_z(-50)))
        if f in shots:
            muzzle = _v((mount[0], mount[1] + MG.GUNS['hmg']['muzzle'], gz))
            self._fx.append({'kind': 'flash', 'pos': muzzle, 'dir': _v((0, 1, 0)), 'size': 0.30})
        return meshes

    def _frame(self, action, f, n, ctx):
        t_loop = self.loops(action)
        t = rig.frame_times(n, t_loop)[f]
        self._fx = []
        Gn, Be = self.gunner, self.bearer
        meshes = []
        if action in ('ATTACK1', 'FORTIFY'):
            meshes = self._deployed(t, f, n, action)
        elif action == 'DEATH':
            meshes = self._death(t, n)
        else:
            run = action == 'RUN'
            gp = self._carry_pose(Gn, t, n, run, 0.0)
            if action == 'VICTORY':
                vg = Gn.anim_victory('VICTORY', n).fn(t)
                gp = vg
            gm, _ = Gn.pose_full(gp, FM.Act(None), 'hmg', None)
            meshes.append(gm.transformed(M.translate(*self.gunner_at)))
            bp = self._carry_pose(Be, t, n, run, 0.5)
            if action == 'VICTORY':
                u = max(0.0, math.sin(math.pi * min(1.0, t / 0.9)))
                bp['hand_l'] = _v((-0.34, 0.10, 0.84 + 1.0 * u))
                bp['elbow_l'] = _v((-0.8, -0.3, 0.2))
            bp['hand_r'] = _v((0.36, 0.04, 0.80)) + (bp['hand_r'] - Be.base['hand_r']) * 0.5
            if action != 'VICTORY':
                bp['hand_l'] = _v((-0.36, 0.02, 0.80)) + (bp['hand_l'] - Be.base['hand_l']) * 0.5
            bm, info = Be.pose_full(bp, FM.Act(None), None, None)
            T = info['T']
            meshes.append(bm.transformed(M.translate(*self.bearer_at)))
            hands = ('r',) if action == 'VICTORY' else ('r', 'l')
            for side in hands:
                pos = T[side][:3, 3] + self.bearer_at
                meshes.append(self._crate().transformed(M.translate(*pos)))
        mesh = M.Mesh.concat(meshes)
        self._local = mesh
        return mesh

    def _death(self, t, n):
        meshes = []
        mount = self.mount
        meshes.append(G.tow_tripod(self.mats, h=self.gun_h - 0.06).transformed(M.translate(*mount)))
        tip = min(1.0, max(0.0, (t - 0.2) / 0.3))
        meshes.append(self._hmg().transformed(M.translate(mount[0], mount[1], self.gun_h - 0.05 * tip)
                                               @ M.rot_x(-90 - 12 * tip)))
        meshes.append(self._crate().transformed(M.translate(*(mount + _v((-0.30, 0.05, 0.20)))) @ M.rot_z(20)))
        for who, at, yaw, delay in ((self.gunner, mount + _v((-0.02, -0.70, 0)), 0, 0.0),
                                    (self.bearer, mount + _v((-0.62, -0.25, 0)), -35, 0.1)):
            d = H.anim_death(who.base, None, None, None)
            tt = min(1.0, max(0.0, (t - delay) / (1 - delay)))
            p = d.fn(tt)
            m, _ = who.pose_full(p, d, None, None)
            meshes.append(m.transformed(M.translate(*at) @ M.rot_z(yaw)))
        return meshes

    def render_cell(self, mesh, mats, cam, W, H_):
        b, t = R.render_cell(mesh, mats, cam, W, H_)
        if not self._fx:
            return b, t
        return draw_all(b, t, self._fx, FM._facing_of(self._local, mesh), cam)
