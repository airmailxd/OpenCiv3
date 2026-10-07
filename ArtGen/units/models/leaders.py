"""Leaders, kings, army standard bearers and the Holy Relic.

`royal` is the humanoid figure (models/humanoid.py, unchanged) dressed by
catalog parameters in robes, gowns, coats, capes and headgear, holding one of
the props in models/props_leaders.py. Long garments (robes, gowns, coat
skirts, capes) are not rigid parts: they are rebuilt every frame around the
posed legs (each ring of the skirt follows the legs and widens to cover a
stride) and lie down on the ground (trains, a fallen figure).

Catalog parameters, on top of the humanoid ones (hair, beard, top, cuirass,
bottom, wraps, sandals, bracers, sash, colors, tint, build, stance, actions):
    robe      {mat, hem (height above the ground), top_r, hem_r (rx, ry),
               flare_k, open (front gap, degrees), edge (trim material),
               edge_deg, bands [(u0, u1, mat)], train, seg} or a list of them
               (outer layers after inner ones)
    cape      {mat, lining, hem, width, wrap, bands, edge, edge_deg, train}
    pants     material name (trousers on the legs); flare: bell bottoms
    shoes     material name (shoes over the feet)
    front     [(mat, half_angle, z0, z1)] panels over the torso front
              (shirt fronts, vests, plastrons), chest space z
    collar    'ruff' | 'broad' | 'fur' | 'high' (with collar_mat)
    shoulders 'epaulettes' | 'pauldrons'
    headgear  [(function name in props_leaders, {kwargs})]
    hairdo    function name in props_leaders (replaces the humanoid hair)
    beard2    props_leaders.beard kind
    face      ['glasses' | 'sunglasses' | 'eyepatch']
    held_r, held_l   prop function names (props_leaders or props), with
              held_r_kw / held_l_kw kwargs; grips: see GRIP_KIND
    companion 'dog'
    gesture   'raise' | 'wave' | 'point' | 'present' (fidget style), gesture_hand 'l'|'r'

Actions: idle, run, death, gesture (FIDGET/FORTIFY), command (an army's
ATTACK: urges the troops on), and the humanoid attack_*/victory/fortify for
the Shogun.
"""
import math

import numpy as np

import mesh as M
import rig
from models import archetype
from models import humanoid as H
from models import props as P
from models import props_leaders as PL

# How each held prop is carried at rest.
GRIP_KIND = {
    'crook': 'pole', 'walking_stick': 'pole', 'feather_standard': 'pole', 'spear_feathered': 'pole',
    'tall_scepter': 'pole', 'spear': 'pole', 'staff': 'pole', 'banner': 'banner',
    'scepter': 'scepter', 'flail': 'scepter', 'orb': 'orb', 'flower': 'scepter', 'axe_gold': 'scepter',
    'cane': 'cane', 'longsword': 'sword_down', 'katana': 'sword', 'scimitar': 'sword', 'sword': 'sword',
    'iklwa': 'pole_short', 'knobkerrie': 'scepter', 'cowhide_shield': 'shield', 'book': 'orb', 'binoculars': 'orb',
    'pipe': 'orb', 'briefcase': 'case',
}

TINT_SHADE = '#dcdcdc'
METALS = ('gold','silver', 'armor', 'steel', 'bronze', 'mail', 'helmet', 'cuirass')


def _rest(kind, side):
    """Rest grip (hand position, prop orientation) for a prop kind."""
    sx = 1 if side == 'r' else -1
    if kind == 'pole':
        return {'hand': (sx * 0.36, 0.12, 1.0), 'w': (-3, sx * 2, 0), 'elbow': (sx * 0.6, -0.8, -0.3)}
    if kind == 'pole_short':
        return {'hand': (sx * 0.38, 0.14, 0.86), 'w': (-20, 0, 0)}
    if kind == 'banner':
        return {'hand': (sx * 0.12, 0.26, 1.02), 'w': (-2, 0, 0), 'elbow': (sx * 0.7, -0.7, -0.3)}
    if kind == 'scepter':
        return {'hand': (sx * 0.30, 0.22, 1.02), 'w': (-25, sx * 12, 0), 'elbow': (sx * 0.5, -0.8, -0.3)}
    if kind == 'orb':
        return {'hand': (sx * 0.26, 0.26, 1.06), 'w': (0, 0, 0), 'elbow': (sx * 0.5, -0.8, -0.3)}
    if kind == 'cane':
        return {'hand': (sx * 0.36, 0.16, 0.86), 'w': (8, sx * 3, 0)}
    if kind == 'sword_down':
        return {'hand': (sx * 0.34, 0.18, 0.86), 'w': (-160, sx * -12, 0)}
    if kind == 'sword':
        return {'hand': (sx * 0.38, 0.12, 0.86), 'w': (-145, 0, sx * -20)}
    if kind == 'shield':
        return {'hand': (sx * 0.34, 0.18, 0.95), 'w': (0, 0, 0), 'elbow': (sx * 0.6, -0.7, -0.4)}
    if kind == 'case':
        return {'hand': (sx * 0.40, 0.04, 0.84), 'w': (0, 0, 0)}
    return {'hand': (sx * 0.40, 0.06, 0.84), 'w': (-150, 0, 0)}


def chest_rings(tw):
    rings = [(0.0, 0.165, 0.105, 0, 0.0), (0.10, 0.19, 0.12, 0, 0.012), (0.19, 0.205, 0.122, 0, 0.008),
             (0.25, 0.185, 0.108, 0, -0.005), (0.30, 0.10, 0.07, 0, -0.01), (0.33, 0.05, 0.05, 0, -0.01)]
    return [(z, rx * tw, ry * (1 + (tw - 1) * 0.6), dx, dy) for z, rx, ry, dx, dy in rings]


def _smax(a, b, k=0.03):
    """A smooth maximum."""
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0, 1)
    return a * (1 - h) + b * h + k * h * (1 - h)


def _inv(T):
    return np.linalg.inv(T).astype(np.float32)


def _pt(T, p):
    return (T[:3, :3] @ np.asarray(p, np.float32)) + T[:3, 3]


@archetype('royal')
class Royal(H.Humanoid):
    def __init__(self, unit, params):
        p = dict(params)
        bp = dict(p)
        bp['weapon'] = None
        bp['offhand'] = None
        if p.get('robe') or p.get('pants'):
            bp.setdefault('bottom', 'none')
        if p.get('hairdo'):
            bp['hair'] = 'none'
        # every civ-colored material exists before the body is built
        bp['colors'] = dict(p.get('colors', {}))
        for name in p.get('tint', []):
            bp['colors'].setdefault(name, '#ffffff')
        super().__init__(unit, bp)
        self.cp = p
        self.tw = H.TORSO_W * p.get('build', {}).get('bulk', 1.0)
        self.lb = H.LIMB * p.get('build', {}).get('bulk', 1.0)
        colors = p.get('colors', {})
        tint = set(p.get('tint', []))
        # civ-colored parts are shaded a little below white, like the
        # originals' tint shades (ntp00): the civ color stays an accent
        shade = p.get('tint_shade', TINT_SHADE)
        for name in tint:
            i = self.mats.add(name, shade, tint=True)
            if name in METALS:
                self.mats.spec[i], self.mats.gloss[i] = 0.5, 24
        for name in METALS:
            if name in colors and name not in tint:
                self.mats.add(name, colors[name], spec=0.85, gloss=26, metal=True)
        if p.get('belt') is None and 'belt' in p:
            self.parts = [pt for pt in self.parts if not (len(pt[1]) and pt[1].M[0] == self.mats['belt'])]
        self.held = {'r': p.get('held_r'), 'l': p.get('held_l')}
        self.kind = {s: GRIP_KIND.get(self.held[s]) if self.held[s] else None for s in 'rl'}
        # the rest pose
        self.weapon = None
        self.offhand = None
        B = H.stance(None, None)
        for s in 'rl':
            if self.kind[s]:
                r = _rest(self.kind[s], s)
                B['hand_' + s] = np.asarray(r['hand'], np.float32)
                B['w_main' if s == 'r' else 'w_off'] = np.asarray(r['w'], np.float32)
                if 'elbow' in r:
                    B['elbow_' + s] = np.asarray(r['elbow'], np.float32)
        if self.kind['r'] == 'banner':
            hr = B['hand_r']
            B['hand_l'] = hr + np.array([-0.02, 0.0, 0.32], np.float32)
            B['elbow_l'] = np.array([-0.7, -0.6, -0.2], np.float32)
        # arms hang a little away from a wide robe
        if not self.kind['r']:
            B['hand_r'] = np.array([0.42, 0.08, 0.86], np.float32)
        if not self.kind['l']:
            B['hand_l'] = np.array([-0.42, 0.06, 0.86], np.float32)
        B.update({k: np.asarray(v, np.float32) for k, v in p.get('stance', {}).items()})
        self.base = B
        self.actions = {'DEFAULT': 'idle', 'RUN': 'run', 'WALK': 'run', 'DEATH': 'death', 'FIDGET': 'gesture',
                        'FORTIFY': 'gesture', 'ATTACK1': 'command', 'ATTACK2': 'command', 'ATTACK3': 'command',
                        'VICTORY': 'victory'}
        self.actions.update(p.get('actions', {}))
        self._add_parts()

    # ------------------------------------------------------------ static parts
    def m(self, name, default='#ffffff', **kw):
        colors = self.cp.get('colors', {})
        if name in self.mats:
            return self.mats[name]
        tint = name in set(self.cp.get('tint', []))
        if not tint and name in ('gold', 'silver'):
            return getattr(PL, name)(self.mats)
        return self.mats.add(name, self.cp.get('tint_shade', TINT_SHADE) if tint else colors.get(name, default),
                             tint=tint, **kw)

    def _add_parts(self):
        p = self.cp
        tw, lb = self.tw, self.lb
        add = self.parts.append
        hd = H.HEAD

        def head(mesh):
            add(('head', mesh.transformed(M.scale(hd)), 'head'))

        if p.get('hairdo'):
            head(getattr(PL, p['hairdo'])(self.mats))
        if p.get('beard2'):
            head(PL.beard(self.mats, p['beard2']))
        for f in p.get('face', []):
            if f == 'glasses':
                head(PL.glasses(self.mats))
            elif f == 'sunglasses':
                head(PL.glasses(self.mats, dark=True))
            elif f == 'eyepatch':
                head(PL.eyepatch(self.mats))
        for name, kw in p.get('headgear', []):
            head(getattr(PL, name)(self.mats, **kw))
        rings = chest_rings(tw)
        for fr in p.get('front', []):
            mname, half, z0, z1 = fr[:4]
            c = fr[4] if len(fr) > 4 else 90
            g = 0.03 if len(fr) <= 5 else fr[5]
            rr = [(z, rx + g, ry + g, dx, dy) for z, rx, ry, dx, dy in rings if z0 <= z <= z1]
            if len(rr) >= 2:
                add(('chest', PL.arc_loft(rr, self.m(mname), c - half, c + half, seg=max(2, int(half / 3))), None))
            if z0 < 0:
                sp = [(-0.02, 0.165 * tw + g, 0.117 * tw + g), (0.12, 0.162 * tw + g, 0.117 * tw + g),
                      (0.23, 0.178 * tw + g, 0.125 * tw + g)]
                add(('spine', PL.arc_loft(sp, self.m(mname), c - half, c + half, seg=max(2, int(half / 3))), None))
        collar = p.get('collar')
        if collar == 'ruff':
            add(('chest', PL.ruff(self.mats, p.get('collar_mat', 'ruff')), None))
        elif collar == 'broad':
            add(('chest', PL.broad_collar(self.mats, p.get('collar_mat', 'gold'), mats2=p.get('collar_mat2'),
                                          **p.get('collar_kw', {})), None))
        elif collar == 'fur':
            add(('chest', PL.torus(0.13 * tw, 0.045, self.m(p.get('collar_mat', 'fur'), '#e8e4dc'), seg=18, sides=8,
                                   z=0.29, sy=0.8), None))
        elif collar == 'high':
            add(('chest', M.cylinder(0.075, 0.07, 0.07, self.m(p.get('collar_mat', 'top')), seg=12, z0=0.29), None))
        sh = p.get('shoulders')
        for side, sx in (('r', 1), ('l', -1)):
            if sh == 'epaulettes':
                add((f'arm_{side}', PL.epaulette(self.mats, p.get('shoulder_mat', 'gold')).transformed(M.scale(sx, 1, 1)), None))
            elif sh == 'pauldrons':
                add((f'arm_{side}', PL.pauldron(self.mats, p.get('shoulder_mat', 'armor')).transformed(M.scale(sx, 1, 1)), None))
        pants = p.get('pants')
        if pants:
            pm = self.m(pants)
            fl = p.get('pants_flare', 0.0)
            for side in 'rl':
                add((f'thigh_{side}', M.capsule((0, 0, 0.02), (0, 0, -H.THIGH), 0.1 * lb, 0.076 * lb, pm, seg=10, rings=2), None))
                add((f'shin_{side}', M.loft([(-H.SHIN + 0.04, 0.062 * lb + fl, 0.064 * lb + fl), (-0.2, 0.066 * lb, 0.068 * lb),
                                             (0.0, 0.074 * lb, 0.074 * lb)], pm, seg=10), None))
            add(('pelvis', M.ellipsoid(0.18 * tw, 0.135 * tw, 0.12, pm, (0, 0, -0.02), seg=14), None))
        shoes = p.get('shoes')
        if shoes:
            sm = self.m(shoes, '#2a2420', spec=0.3, gloss=20)
            for side in 'rl':
                add((f'foot_{side}', M.rounded_box(0.105 * H.FOOT, 0.245 * H.FOOT, 0.085, sm, (0, 0.06, -0.04), r=0.03, seg=12, p=3), None))
        if p.get('arm_bands'):
            bm = self.m(p['arm_bands'])
            for side in 'rl':
                add((f'forearm_{side}', M.cylinder(0.058 * lb, 0.056 * lb, 0.06, bm, seg=10, z0=-0.22), None))
        if p.get('cuffs'):
            cm = self.m(p['cuffs'])
            for side in 'rl':
                add((f'forearm_{side}', M.loft([(-0.24, 0.07 * lb, 0.07 * lb), (-0.13, 0.065 * lb, 0.065 * lb)], cm, seg=10,
                                               cap_bottom=False, cap_top=False), None))
        if p.get('sleeves'):
            # full sleeves (wide for robes) in a material
            sm = self.m(p['sleeves'])
            wide = p.get('sleeve_wide', 0.0)
            for side in 'rl':
                add((f'arm_{side}', M.capsule((0, 0, 0.0), (0, 0, -H.UPPER_ARM), 0.07 * lb, 0.062 * lb, sm, seg=10, rings=2), None))
                add((f'forearm_{side}', M.loft([(-H.FOREARM + 0.03, 0.065 * lb + wide, 0.065 * lb + wide * 1.2, 0, wide * 0.4),
                                                (-0.12, 0.062 * lb + wide * 0.5, 0.062 * lb + wide * 0.5),
                                                (0.02, 0.064 * lb, 0.064 * lb)], sm, seg=10, cap_bottom=False), None))
                if p.get('sleeve_cuff'):
                    add((f'forearm_{side}', M.loft([(-H.FOREARM + 0.02, 0.07 * lb + wide, 0.07 * lb + wide * 1.2, 0, wide * 0.4),
                                                    (-H.FOREARM + 0.08, 0.068 * lb + wide * 0.85, 0.068 * lb + wide, 0, wide * 0.3)],
                                                   self.m(p['sleeve_cuff']), seg=10, cap_bottom=False, cap_top=False), None))
        # legs under a floor-length robe take the robe's color (no skin
        # flashes through it in a stride)
        robes = p.get('robe')
        robes = robes if isinstance(robes, list) else ([robes] if robes else [])
        long_robe = next((r for r in robes if r.get('hem', 1) < 0.2), None)
        leg_mat = p.get('leg_mat') or (long_robe.get('mat', 'robe') if long_robe and not p.get('pants') else None)
        if leg_mat:
            skin, lm = self.mats['skin'], self.m(leg_mat)
            self.parts = [(b, M.Mesh(m.V, m.N, np.where(m.M == skin, lm, m.M)) if b.startswith(('thigh', 'shin')) else m, t)
                          for b, m, t in self.parts]
        for (bone, fn, kw, T) in p.get('extras', []):
            mesh = getattr(PL, fn)(self.mats, **kw)
            add((bone, mesh.transformed(M.compose(*[getattr(M, t[0])(*t[1:]) for t in T])), None))

    # ------------------------------------------------------------ animation
    def anim_name(self, action):
        return self.actions.get(action, 'idle')

    def loops(self, action):
        return self.anim_name(action) in ('idle', 'run')

    def action(self, action, ctx):
        name = self.anim_name(action)
        B = self.base
        kr = self.kind['r']
        w_like = {'pole': 'staff', 'banner': 'staff', 'pole_short': 'spear', 'sword': 'sword', 'sword_down': 'sword',
                  'scepter': 'staff', 'cane': 'staff', 'orb': 'staff'}.get(kr)
        if name == 'idle':
            return H.anim_idle(B, amount=self.cp.get('idle_amount', 1.0))
        if name == 'run':
            a = H.anim_run(B, w_like, 'shield_round' if self.kind['l'] else None, ctx.n if ctx else 10)
            if self.cp.get('run') != 'full' and (self.cp.get('robe') or self.cp.get('stately')):
                return _soften_run(a, self.cp.get('run_lean', 0.8), self.cp.get('run_crouch', 0.10), B)
            return a
        if name == 'death':
            return H.anim_death(B, None, None, ctx)
        if name == 'death_kneel':
            return anim_death_kneel(B, ctx)
        if name == 'gesture':
            return anim_gesture(B, self.cp.get('gesture', 'raise'), self.cp.get('gesture_hand', 'l' if kr else 'r'), ctx,
                                self.cp.get('gesture_w'))
        if name == 'command':
            return anim_command(B, kr, ctx)
        if name.startswith('attack_'):
            return H.anim_attack(B, 'sword', None, name[7:], ctx)
        if name == 'victory':
            return H.anim_victory(B, 'sword' if kr == 'sword' else None, None, ctx)
        if name == 'fortify':
            return H.anim_fortify(B, 'sword' if kr == 'sword' else None, None, ctx)
        raise ValueError(f'{self.unit}: unknown animation {name}')

    def frame(self, action, f, n, ctx):
        extra = None
        if ctx is not None and ctx.flic.n_anims == 1 and n % 8 == 0 and n >= 16:
            # a flic with all eight directions in one row (the Shogun's attack)
            per = n // 8
            d, f, n = f // per, f % per, per
            from render import facing_matrix
            extra = _inv(facing_matrix(0)) @ self._one_row_shift(ctx.flic, d) @ facing_matrix(d)
            ctx = None
        act = self.action(action, ctx)
        t = rig.frame_times(n, act.loop)[f]
        pose = act.fn(t)
        pose.setdefault('t', t)
        name = self.anim_name(action)
        mesh = self.pose_full(pose, act, name, t)
        return mesh if extra is None else mesh.transformed(extra)

    def _one_row_shift(self, fl, d):
        """A one-row flic's frames don't use the unit's usual placement: move
        the figure (along the ground) to where the original stands in them."""
        import framework
        import render as R
        import flic as F
        if not hasattr(self, '_calib'):
            ax, ay, oh = framework.original_stance(self.unit)
            ppm = framework.BASE_PPM * framework.SCALE
            mh = framework.model_extent(self, ppm)
            self._calib = (ax, ay, ppm * (oh - 0.5) / max(mh, 1e-3) * self.cp.get('scale', 1.0))
        ax, ay, ppm = self._calib
        per = fl.n_frames // 8
        idx = fl.frames[0, d * per]
        m = ~F.shadow_mask(idx) & (idx != 255)
        ys, xs = np.nonzero(m)
        if len(ys) == 0:
            return np.eye(4, dtype=np.float32)
        by = ys.max()
        tx, ty = xs[ys >= by - 3].mean(), by - 1.5        # where the original stands, in the frame
        cx, cy = ax - fl.offset_left, ay - fl.offset_top  # where the camera puts the origin
        dx = (tx - cx) * framework.SCALE / ppm
        dy = -(ty - cy) * framework.SCALE / (ppm * R.SIN_E)
        return M.translate(dx, dy, 0)

    # ------------------------------------------------------------ posing
    def pose_full(self, pose, act, name, t):
        W = self.world(pose, act)
        meshes = []
        hide = set(act.hide)
        for bone, m, tag in self.parts:
            if tag in hide:
                continue
            meshes.append(m.transformed(W[bone]))
        for side, chan in (('r', 'w_main'), ('l', 'w_off')):
            item = self.held[side]
            if not item:
                continue
            Wh = W[f'hand_{side}']
            grip = Wh[:3, 3] + Wh[:3, :3] @ np.array([0, 0.01, -H.GRIP], np.float32)
            kw = dict(self.cp.get(f'held_{side}_kw', {}))
            if item == 'banner':
                kw['phase'] = float(pose.get('t', 0.0)) * (2.0 if name == 'run' else 1.0)
                kw['amp'] = 1.6 if name == 'run' else 1.0
                mesh = PL.banner(self.mats, **kw)
            else:
                key = (item, tuple(sorted(kw.items())))
                if key not in self._props:
                    fn = getattr(PL, item, None) or getattr(P, item)
                    self._props[key] = fn(self.mats, **kw)
                mesh = self._props[key]
            w = np.asarray(pose.get(chan, (0, 0, 0)), np.float32)
            if name == 'death' and self.kind[side] not in ('banner',):
                # dropped: it tips over and lies on the ground beside the hand
                u = float(np.clip((t - 0.2) / 0.35, 0, 1))
                u = u * u * (3 - 2 * u)
                sx = 1 if side == 'r' else -1
                w = w * (1 - u) + np.array([0, sx * 90, 0], np.float32) * u
                grip = grip * (1 - u) + np.array([grip[0] + sx * 0.15, grip[1], 0.05], np.float32) * u
            R = M.euler(*w)
            if self.kind[side] == 'shield':
                R = R @ M.rot_z(90 if side == 'l' else -90)
            pm = mesh.transformed(M.translate(*grip) @ R)
            if name.startswith('death'):
                pm = _ground(pm)
            meshes.append(pm)
        flow = float(pose.get('cape_flow', 0.0))
        if name == 'run':
            flow = 0.12
        robes = self.cp.get('robe')
        if robes:
            for spec in (robes if isinstance(robes, list) else [robes]):
                meshes.append(self.robe_mesh(spec, W, flow * 0.3))
        if self.cp.get('cape'):
            meshes.append(self.cape_mesh(self.cp['cape'], W, flow))
        if self.cp.get('companion') == 'dog':
            meshes.append(self.dog_mesh(name, t))
        return M.Mesh.concat(meshes)

    def world(self, pose, act):
        """Bone world transforms for a pose (humanoid.pose_mesh's solver)."""
        h = self.h
        sk = self.skel
        root_pos = np.asarray(pose.get('root_pos', (0, 0, 0)), np.float32)
        base_pose = {'root_pos': root_pos, 'root_rot': pose.get('root_rot', (0, 0, 0))}
        for b in ('spine', 'chest', 'neck', 'head'):
            base_pose[b] = pose.get(b, (0, 0, 0))
        base_pose['pelvis'] = np.zeros(3, np.float32)
        limb_fk = {}
        for b in ('arm_r', 'forearm_r', 'arm_l', 'forearm_l', 'thigh_r', 'shin_r', 'thigh_l', 'shin_l',
                  'foot_r', 'foot_l', 'hand_r', 'hand_l'):
            limb_fk[b] = pose.get(b, (0, 0, 0))
        W0 = sk.world(dict(base_pose, **limb_fk))
        over = {}
        if act.legs == 'ik':
            for side in 'rl':
                hip = W0[f'thigh_{side}'][:3, 3]
                target = np.asarray(pose[f'foot_{side}'], np.float32).copy()
                pf = W0['pelvis'][:3, 1]
                pole = H._normalize(pf + np.array([0.15 if side == 'r' else -0.15, 0, 0], np.float32))
                Ru, Rl, _, _ = H.two_bone(hip, target, H.THIGH * h, H.SHIN * h, pole, bend_forward=False)
                over[f'thigh_{side}'] = Ru
                over[f'shin_{side}'] = Rl
                yaw = float(pose.get(f'fyaw_{side}', 0.0)) + float(np.degrees(np.arctan2(-pf[0], pf[1])))
                over[f'foot_{side}'] = (M.rot_z(yaw) @ M.rot_x(float(pose.get(f'toe_{side}', 0.0))))[:3, :3]
        if act.arms == 'ik':
            for side in 'rl':
                sh = W0[f'arm_{side}'][:3, 3]
                grip = np.asarray(pose[f'hand_{side}'], np.float32) * np.array([1, 1, h], np.float32)
                pole = H._normalize(np.asarray(pose.get(f'elbow_{side}', (0, -1, -0.3)), np.float32))
                d = H._normalize(grip - sh)
                wrist = grip - d * H.GRIP
                Ru, Rl, _, _ = H.two_bone(sh, wrist, H.UPPER_ARM * h, H.FOREARM * h, pole, bend_forward=True)
                over[f'arm_{side}'] = Ru
                over[f'forearm_{side}'] = Rl
                over[f'hand_{side}'] = Rl
        return H._world_with(sk, dict(base_pose, **limb_fk), over)

    # ------------------------------------------------------------ garments
    def _legs_local(self, W):
        """Hip, knee and ankle of both legs in pelvis space."""
        Pi = _inv(W['pelvis'])
        out = {}
        for s in 'rl':
            out[s] = [_pt(Pi, W[f'{b}_{s}'][:3, 3]) for b in ('thigh', 'shin', 'foot')]
        return out

    @staticmethod
    def _along(poly, f):
        """The point at fraction f (0..1) of a polyline's length."""
        seg = [np.linalg.norm(b - a) for a, b in zip(poly[:-1], poly[1:])]
        L = sum(seg)
        d = f * L
        for (a, b), s in zip(zip(poly[:-1], poly[1:]), seg):
            if d <= s or s == seg[-1]:
                u = min(1.0, d / max(s, 1e-6))
                return a + (b - a) * u
            d -= s
        return poly[-1]

    def robe_mesh(self, spec, W, flow=0.0):
        tw = self.tw
        mat = self.m(spec.get('mat', 'robe'))
        top = spec.get('top', 0.11)
        hem = spec.get('hem', 0.05)
        hem_local = hem - H.PELVIS_H
        L = top - hem_local
        rt = spec.get('top_r', (0.19, 0.145))
        rh = spec.get('hem_r', (0.27, 0.24))
        k = spec.get('flare_k', 1.4)
        bands = spec.get('bands', [])
        seg = spec.get('seg', 26)
        gap = spec.get('open', 0.0)
        train = spec.get('train', 0.0)
        # long robes cover the legs in a stride; short coats and tabards part
        cover = spec.get('cover', 1.0 if hem < 0.3 else 0.3)
        legs = self._legs_local(W)
        samples = np.array([self._along(legs[sd], q) for sd in 'rl' for q in np.linspace(0.15, 1, 12)])
        oc = spec.get('open_at', 90)
        a0, a1 = oc + gap / 2, oc + 360 - gap / 2
        closed = gap <= 0
        ang = np.radians(np.linspace(a0, a1, seg + 1))
        cs, sn = np.cos(ang), np.sin(ang)
        rings = []
        # rings: evenly spaced, plus the band edges (sharp stripes)
        us = set(np.round(np.linspace(0, 1, spec.get('rings', 10)) ** 0.9, 4))
        for b0, b1, _ in bands:
            us |= {round(b0, 4), round(b1, 4)}
        us = np.array(sorted(u for u in us if 0 <= u <= 1))
        nr = len(us)
        for u in us:
            zl = top - u * L
            f = np.clip((-0.05 - zl) / (H.THIGH + H.SHIN), 0, 1)
            pr = self._along(legs['r'], f)
            pl = self._along(legs['l'], f)
            mid = (pr + pl) / 2
            w = min(1.0, f * 1.1) ** 1.2
            cx = mid[0] * w
            cy = mid[1] * w - flow * u * u
            rx = rt[0] * tw / 1.3 + (rh[0] - rt[0]) * u ** k
            ry = rt[1] * tw / 1.3 + (rh[1] - rt[1]) * u ** k
            leg_r = 0.1 if f > 0 else 0.0
            # cover both legs around the ring's own center: the leg points at
            # this height (bent knees reach forward above their place on the leg)
            near = samples[np.abs(samples[:, 2] - zl) < 0.2]
            near = np.vstack([near, pr[None], pl[None]])
            sx = np.abs(near[:, 0] - cx).max() + leg_r + 0.03
            sy = np.abs(near[:, 1] - cy).max() + leg_r + 0.03
            if f > 0.05:
                rx = float(_smax(rx, rx + (sx - rx) * cover))
                ry = float(_smax(ry, ry + (sy - ry) * cover))
            ryv = np.where(sn < 0, ry * (1 + train * u ** 2), ry)
            # the hem follows the legs' height a little in a stride
            zc = zl
            if f > 0:
                zc = zl * (1 - 0.35 * w) + mid[2] * 0.35 * w
            rings.append(np.stack([cx + rx * cs, cy + ryv * sn, np.full_like(cs, zc)], -1))
        pts = np.array(rings, np.float32)
        # world space, lying on the ground where it reaches it
        Wp = W['pelvis']
        pts = pts @ Wp[:3, :3].T + Wp[:3, 3]
        # a figure lying down: the cloth slumps onto the ground instead of
        # keeping its standing cone shape
        up = float(Wp[2, 2])
        if up < 0.8:
            sq = 0.45 + 0.55 * max(0.0, up) / 0.8
            k = np.clip((us[:, None] - 0.15) / 0.5, 0, 1)
            pts[..., 2] = pts[..., 2] * (1 - k * (1 - sq))
        pts[..., 2] = np.maximum(pts[..., 2], 0.012 + 0.004 * np.arange(nr)[:, None] / nr)
        mf = np.full((nr - 1, seg), mat, np.int32)
        for (b0, b1, bm) in spec.get('bands', []):
            sel = (us[1:] > b0 + 0.01) & (us[:-1] < b1 - 0.01)
            mf[sel, :] = self.m(bm)
        if spec.get('edge') and not closed:
            ed = spec.get('edge_deg', 14)
            nseg = max(1, int(round(ed / ((360 - gap) / seg))))
            mf[:, :nseg] = self.m(spec['edge'])
            mf[:, -nseg:] = self.m(spec['edge'])
        if spec.get('stripes'):
            sm, period, width = spec['stripes']
            j = np.arange(seg)
            mf[:, (j % period) < width] = self.m(sm)
            for (b0, b1, bm) in spec.get('bands', []):
                sel = (us[1:] > b0 + 0.01) & (us[:-1] < b1 - 0.01)
                mf[sel, :] = self.m(bm)
        if spec.get('panel'):
            # a front panel (a tabard's or an apron's) of another material
            pm, half = spec['panel']
            angm = np.degrees((ang[:-1] + ang[1:]) / 2) % 360
            sel = np.abs(((angm - 90 + 180) % 360) - 180) < half
            mf[:, sel] = self.m(pm)
            if spec.get('panel_back'):
                selb = np.abs(((angm - 270 + 180) % 360) - 180) < half
                mf[:, selb] = self.m(pm)
        return PL.sheet(pts, mf)

    def cape_mesh(self, spec, W, flow=0.0):
        tw = self.tw
        mat = self.m(spec.get('mat', 'cape'))
        wrap = spec.get('wrap', 20)
        hem = spec.get('hem', 0.08)
        width = spec.get('width', 1.0)
        train = spec.get('train', 0.0)
        seg = 20
        ang = np.radians(np.linspace(180 - wrap, 360 + wrap, seg + 1))
        cs, sn = np.cos(ang), np.sin(ang)
        Wc, Wp = W['chest'], W['pelvis']
        rings = []
        # shoulders (chest space)
        for z, rx, ry, dy in ((0.31, 0.12, 0.08, -0.01), (0.26, 0.21, 0.13, -0.03), (0.14, 0.235, 0.15, -0.05)):
            pts = np.stack([rx * tw * width * cs, dy + ry * tw * sn, np.full_like(cs, z)], -1)
            rings.append(pts @ Wc[:3, :3].T + Wc[:3, 3])
        # below: hanging from the back, around the hips and legs (pelvis space)
        legs = self._legs_local(W)
        top_l = 0.18
        hem_local = hem - H.PELVIS_H
        n = 7
        for i in range(1, n + 1):
            u = i / n
            zl = top_l + (hem_local - top_l) * u
            f = np.clip((-0.05 - zl) / (H.THIGH + H.SHIN), 0, 1)
            pr = self._along(legs['r'], f)
            pl = self._along(legs['l'], f)
            rx = (0.24 + 0.1 * u) * tw * width / 1.3 * 1.3
            ry = 0.17 + 0.08 * u
            sy = abs(pr[1] - pl[1]) / 2 + 0.12
            ry = float(_smax(ry, sy * (f > 0.05)))
            cy = -0.05 - flow * u * 1.5 + min(pr[1], pl[1]) * 0.0
            ryv = np.where(sn < 0, ry * (1 + train * u ** 2), ry * 0.4)
            pts = np.stack([rx * cs, cy + ryv * sn, np.full_like(cs, zl + flow * u * u * 0.8)], -1)
            rings.append(pts @ Wp[:3, :3].T + Wp[:3, 3])
        pts = np.array(rings, np.float32)
        pts[..., 2] = np.maximum(pts[..., 2], 0.014)
        nr = len(rings)
        mf = np.full((nr - 1, seg), mat, np.int32)
        for (b0, b1, bm) in spec.get('bands', []):
            us = (np.arange(nr - 1) + 0.5) / (nr - 1)
            mf[(us >= b0) & (us <= b1), :] = self.m(bm)
        if spec.get('edge'):
            mf[:, :1] = self.m(spec['edge'])
            mf[:, -1:] = self.m(spec['edge'])
        if spec.get('emblem'):
            em, (i0, i1), half = spec['emblem']
            mid = seg // 2
            mf[i0:i1, mid - half:mid + half] = self.m(em)
        return PL.sheet(pts, mf)

    def dog_mesh(self, name, t):
        c = self.cp.get('companion_at', (0.62, 0.18, 0.0, 0.0))
        x, y, z, yaw = c
        look = 15 * math.sin(2 * math.pi * t) if name in ('idle', 'gesture') else 0
        dog = PL.dog(self.mats, phase=t, look=look)
        if name == 'run':
            dog = dog.transformed(M.translate(0, 0, 0.03 * abs(math.sin(2 * math.pi * t))))
        if name == 'death':
            # the dog bounds away, then lies down
            u = min(1.0, t / 0.5)
            w = float(np.clip((t - 0.5) / 0.25, 0, 1))
            dog = dog.transformed(M.translate(-0.8 * u, -0.6 * u, 0.18 * math.sin(math.pi * u) - 0.32 * w)
                                  @ M.rot_z(70 * u))
            dog = _ground(dog)
        return dog.transformed(M.translate(x, y, z) @ M.rot_z(yaw))


def _ground(mesh, z=0.012):
    V = mesh.V.copy()
    V[..., 2] = np.maximum(V[..., 2], z)
    return M.Mesh(V, mesh.N, mesh.M)


def _soften_run(a, k, crouch=0.10, base=None):
    """A hunched, shuffling run for robed figures (shorter strides, knees
    bent; Civ3's leaders scurry rather than sprint)."""
    fn0 = a.fn

    def fn(t):
        p = fn0(t)
        p['root_rot'] = np.asarray(p['root_rot'], np.float32) * np.array([k, 1, 1], np.float32)
        p['root_pos'] = np.asarray(p['root_pos'], np.float32) - np.array([0, 0.04, crouch], np.float32)
        p['head'] = np.asarray(p['head'], np.float32) * k
        for s in 'rl':
            if base is not None:
                p['hand_' + s] = base['hand_' + s] + 0.45 * (np.asarray(p['hand_' + s], np.float32) - base['hand_' + s])
            f = np.asarray(p['foot_' + s], np.float32).copy()
            f[1] *= 0.75
            f[2] = H.ANKLE_H + (f[2] - H.ANKLE_H) * 0.8
            p['foot_' + s] = f
        return p
    return H.Action(fn, loop=True)


def anim_gesture(base, style, hand, ctx, w=None):
    """A leader's fidget: raises the free hand in an orator's gesture (twice),
    looks around, and settles back."""
    B = base
    sx = 1 if hand == 'r' else -1
    hc = 'hand_' + hand
    ec = 'elbow_' + hand
    if style == 'wave':
        up = {hc: (sx * 0.42, 0.18, 1.78), ec: (sx * 1.0, -0.2, -0.3)}
        side = {hc: (sx * 0.55, 0.12, 1.72), ec: (sx * 1.0, -0.2, -0.3)}
        k = [(0.0, {}), (0.22, dict(up, head=(-6, 0, sx * -12))), (0.4, side), (0.55, up), (0.7, side),
             (0.85, {hc: B[hc] + (0, 0.06, 0.1), 'head': (0, 0, 0)}), (1.0, {hc: B[hc], ec: B[ec]})]
    elif style == 'point':
        out = {hc: (sx * 0.3, 0.72, 1.5), ec: (sx * 0.8, -0.3, -0.4), 'chest': (-4, 0, sx * -10), 'head': (4, 0, sx * -6)}
        k = [(0.0, {}), (0.3, out), (0.55, dict(out, **{hc: (sx * 0.38, 0.68, 1.58)})), (0.7, out),
             (1.0, {hc: B[hc], ec: B[ec], 'chest': (0, 0, 0), 'head': (0, 0, 0)})]
    elif style == 'present':
        # both arms open, presenting (a queen greeting her subjects)
        o = 'l' if hand == 'r' else 'r'
        out = {hc: (sx * 0.52, 0.36, 1.32), ec: (sx * 0.8, -0.4, -0.4), 'head': (-4, 0, 0)}
        k = [(0.0, {}), (0.3, out), (0.5, dict(out, **{hc: (sx * 0.56, 0.32, 1.4)}), ), (0.7, out),
             (1.0, {hc: B[hc], ec: B[ec], 'head': (0, 0, 0)})]
    elif style == 'kneel':
        # down on one knee, arms flung wide and up (a showman's finish)
        kneel = {'root_pos': (0, -0.04, -0.42), 'foot_l': (-0.2, 0.32, H.ANKLE_H), 'foot_r': (0.18, -0.34, 0.1),
                 'toe_r': 40.0, 'spine': (6, 0, 0), 'chest': (8, 0, 0), 'head': (-12, 0, 0),
                 'hand_r': (0.7, 0.12, 1.45), 'elbow_r': (1.0, -0.2, -0.3), 'hand_l': (-0.7, 0.12, 1.45),
                 'elbow_l': (-1.0, -0.2, -0.3)}
        k = [(0.0, {}), (0.3, kneel), (0.55, dict(kneel, hand_r=(0.62, 0.1, 1.62), hand_l=(-0.62, 0.1, 1.62))),
             (0.75, kneel), (1.0, {c: B[c] for c in kneel if c in B})]
        if 'toe_r' in B:
            k[-1][1]['toe_r'] = B['toe_r']
    else:  # 'raise': out to the side, then up high, then down
        out = {hc: (sx * 0.62, 0.32, 1.38), ec: (sx * 0.9, -0.4, -0.3), 'chest': (0, 0, sx * -8), 'head': (-4, 0, sx * -14)}
        up = {hc: (sx * 0.46, 0.34, 1.86), ec: (sx * 1.0, -0.2, -0.2), 'chest': (4, 0, sx * -6), 'head': (-10, 0, sx * -8)}
        if w is not None:
            # the held prop raised too (a sword held aloft)
            wc = 'w_main' if hand == 'r' else 'w_off'
            out = dict(out, **{wc: w})
            up = dict(up, **{wc: w, hc: (sx * 0.3, 0.14, 2.06), ec: (sx * 1.0, -0.1, -0.2)})
        k = [(0.0, {}), (0.18, out), (0.36, up), (0.5, out), (0.66, up), (0.82, out),
             (1.0, {hc: B[hc], ec: B[ec], 'chest': (0, 0, 0), 'head': (0, 0, 0),
                    **({('w_main' if hand == 'r' else 'w_off'): B['w_main' if hand == 'r' else 'w_off']} if w is not None else {})})]
    anim = H.keys(B, k)
    return H.Action(anim)


def anim_death_kneel(base, ctx):
    """A standard bearer's death: struck, he sinks to his knees and slumps
    over, the standard still planted upright in his hands."""
    B = base
    down = ctx.event('down', 0.5) if ctx is not None else 0.5
    down = min(max(down or 0.5, 0.3), 0.8)
    hr = np.asarray(B['hand_r'], np.float32)
    kneel = {'root_pos': (0, -0.08, -0.46), 'foot_l': (-0.17, 0.30, H.ANKLE_H), 'foot_r': (0.15, -0.34, 0.1),
             'toe_r': 40.0, 'spine': (-12, 0, 0), 'chest': (-8, 0, 0), 'head': (14, 0, 0),
             'hand_r': hr + (0, 0.02, -0.4), 'hand_l': hr + (-0.02, 0.02, -0.12), 'w_main': (-4, 0, 0)}
    slump = dict(kneel, root_pos=(0, -0.14, -0.6), foot_r=(0.15, -0.42, 0.06), spine=(-26, 0, 6), chest=(-16, 0, 4),
                 head=(32, 0, 10), hand_r=hr + (0, 0.04, -0.55), hand_l=hr + (-0.02, 0.04, -0.3), w_main=(-8, 0, 4))
    k = [(0.0, {}),
         (0.2, {'root_rot': (8, 0, 0), 'chest': (10, 0, 6), 'head': (-14, 0, 0), 'root_pos': (0, -0.04, -0.02)}),
         (0.55, kneel), (0.8, slump), (1.0, slump)]
    anim = H.keys(dict(B, root_rot=np.zeros(3, np.float32)), k)
    return H.Action(lambda t: anim(H._warp(t, [(0.55, down)])))


def anim_command(base, kind_r, ctx):
    """An army's attack: the standard bearer thrusts the banner forward and
    punches the free fist into the air, urging the troops on."""
    B = base
    strike = ctx.event('reach_max', 0.5) if ctx is not None else 0.5
    strike = min(max(strike or 0.5, 0.3), 0.8)
    if kind_r == 'banner':
        hr = B['hand_r']
        fwd = {'hand_r': hr + (0, 0.16, 0.06), 'w_main': (-14, 0, 0), 'chest': (-6, 0, 10), 'root_pos': (0, 0.04, -0.03),
               'hand_l': (-0.42, 0.34, 1.86), 'elbow_l': (-1.0, -0.2, -0.2), 'head': (-10, 0, 0)}
        mid = {'hand_l': (-0.5, 0.2, 1.45), 'elbow_l': (-1.0, -0.3, -0.3), 'chest': (0, 0, 4), 'w_main': (-6, 0, 0)}
        k = [(0.0, {}), (0.25, mid), (0.5, fwd), (0.65, dict(fwd, hand_l=(-0.44, 0.38, 1.8))), (0.85, mid),
             (1.0, {c: B[c] for c in ('hand_r', 'hand_l', 'w_main', 'chest', 'root_pos', 'elbow_l', 'head')})]
    else:
        fwd = {'hand_l': (-0.3, 0.6, 1.6), 'elbow_l': (-0.8, -0.3, -0.3), 'chest': (-6, 0, 12), 'head': (-4, 0, 0)}
        k = [(0.0, {}), (0.5, fwd), (0.7, fwd), (1.0, {c: B[c] for c in ('hand_l', 'elbow_l', 'chest', 'head')})]
    anim = H.keys(B, k)
    return H.Action(lambda t: anim(H._warp(t, [(0.5, strike)])))


# ------------------------------------------------------------ the Holy Relic

BEARER = {
    'colors': {'skin': '#b8957a', 'hair': '#3a2616', 'pants': '#4a4a50', 'boots': '#3a2a1e', 'belt': '#4a3020',
               'top': '#7c7e82'},
    'tint': ['bottom'],
    'hair': 'short', 'top': 'sleeved', 'bottom': 'skirt', 'pants': 'pants', 'wraps': 'boots',
}


def _remap(parts, src, dst):
    """Re-index part meshes from one material table into another."""
    lut = np.array([dst.add(n, c, tint=t, spec=s, gloss=g, metal=m) for n, c, t, s, g, m in
                    zip(src.names, src.color, src.tint, src.spec, src.gloss, src.metal)], np.int32)
    return [(b, M.Mesh(m.V, m.N, lut[m.M]), tag) for b, m, tag in parts]


@archetype('relic')
class HolyRelic:
    """Two bearers carrying the golden ark on two poles: the front bearer
    leads (the poles at his sides, behind him), the rear one follows (the
    poles in front of him)."""

    POLE_X, POLE_Z, GAP = 0.4, 0.9, 1.22

    def __init__(self, unit, params):
        self.unit = unit
        self.p = params
        fp = dict(BEARER, beard=True)
        fp['colors'] = dict(BEARER['colors'], **params.get('colors', {}))
        fp['stance'] = {'hand_r': (self.POLE_X, -0.06, self.POLE_Z), 'hand_l': (-self.POLE_X, -0.06, self.POLE_Z),
                        'elbow_r': (0.3, -1.0, 0.0), 'elbow_l': (-0.3, -1.0, 0.0)}
        rp = dict(fp, beard=False, hair='long')
        rp['stance'] = {'hand_r': (self.POLE_X, 0.2, self.POLE_Z), 'hand_l': (-self.POLE_X, 0.2, self.POLE_Z),
                        'elbow_r': (0.6, -0.8, -0.2), 'elbow_l': (-0.6, -0.8, -0.2)}
        self.front = Royal(unit, fp)
        self.rear = Royal(unit, rp)
        self.mats = self.front.mats
        self.rear.parts = _remap(self.rear.parts, self.rear.mats, self.mats)
        self.rear.mats = self.mats
        self.ark = PL.ark(self.mats)
        wood = P.wood(self.mats)
        g = PL.gold(self.mats)
        L = 2 * self.GAP + 0.55
        poles = []
        for sx in (1, -1):
            poles.append(M.cylinder(0.026, 0.026, L, wood, seg=8, z0=-L / 2).transformed(
                M.translate(sx * self.POLE_X, 0, 0) @ M.rot_x(-90)))
            for sy in (1, -1):
                poles.append(M.sphere(0.035, g, (sx * self.POLE_X, sy * L / 2, 0), seg=6))
                poles.append(M.box(0.1, 0.05, 0.05, g, (sx * (self.POLE_X - 0.05), sy * 0.4, 0)))
        self.poles = M.Mesh.concat(poles)

    def loops(self, action):
        return action in ('DEFAULT', 'RUN', 'WALK')

    def frame(self, action, f, n, ctx):
        loop = self.loops(action)
        t = rig.frame_times(n, loop)[f]
        out = []
        ark_z = self.POLE_Z - 0.45
        ark_T = M.translate(0, 0, ark_z)
        pole_T = M.translate(0, 0, self.POLE_Z)
        for who, y0, phase in ((self.front, self.GAP, 0.0), (self.rear, -self.GAP, 0.5)):
            B = who.base
            name = 'idle'
            if action == 'DEFAULT':
                act = H.anim_idle(B, amount=0.6)
                pose = act.fn((t + phase * 0.3) % 1.0)
                pose['hand_r'], pose['hand_l'] = B['hand_r'], B['hand_l']
            elif action in ('RUN', 'WALK'):
                act = _soften_run(H.anim_run(B, 'staff', 'shield_round', n), 0.5, 0.05, None)
                pose = act.fn((t + phase) % 1.0)
                bob = float(pose['root_pos'][2]) + 0.05
                pose['hand_r'] = B['hand_r'] + (0, 0, bob * 0.5)
                pose['hand_l'] = B['hand_l'] + (0, 0, bob * 0.5)
                name = 'run'
            elif action == 'DEATH':
                act = H.anim_death(B, None, None, ctx) if who is self.rear else _fall_forward(B, ctx)
                pose = act.fn(t)
                name = 'death'
            else:  # FIDGET / FORTIFY: they shift the load and look around
                act = anim_bearer_fidget(B, phase)
                pose = act.fn(t)
            mesh = who.pose_full(pose, act, name, t)
            out.append(mesh.transformed(M.translate(0, y0, 0)))
        if action == 'DEATH':
            down = ctx.event('down', 0.5) if ctx is not None else 0.5
            u = float(np.clip((t - 0.15) / max(0.2, (down or 0.5) - 0.1), 0, 1)) ** 2
            drop = ark_z * u
            tilt = 7 * math.sin(math.pi * min(1.0, u * 1.2)) + 3 * u
            ark_T = M.translate(0, 0, ark_z - drop) @ M.rot_x(tilt)
            pole_T = M.translate(0, 0, self.POLE_Z - drop) @ M.rot_x(tilt)
        elif action in ('RUN', 'WALK'):
            bob = 0.02 * math.sin(4 * math.pi * t)
            ark_T = M.translate(0, 0, ark_z + bob) @ M.rot_x(1.5 * math.sin(2 * math.pi * t))
            pole_T = M.translate(0, 0, self.POLE_Z + bob) @ M.rot_x(1.5 * math.sin(2 * math.pi * t))
        out.append(self.ark.transformed(ark_T))
        out.append(self.poles.transformed(pole_T))
        return M.Mesh.concat(out)


def _fall_forward(base, ctx):
    """Struck from behind: pitches forward onto the face, arms flung ahead."""
    down = ctx.event('down', 0.55) if ctx is not None else 0.55
    down = min(max(down or 0.55, 0.35), 0.85)
    fk = {'thigh_r': (0, -4, 0), 'shin_r': (0, 0, 0), 'thigh_l': (0, 4, 0), 'shin_l': (0, 0, 0),
          'arm_r': (10, -10, 0), 'forearm_r': (20, 0, 0), 'arm_l': (10, 10, 0), 'forearm_l': (20, 0, 0),
          'root_pos': (0, 0, 0), 'root_rot': (0, 0, 0), 'spine': (0, 0, 0), 'chest': (0, 0, 0), 'head': (0, 0, 0)}
    k = [(0.0, {}),
         (0.2, {'root_rot': (-12, 0, 4), 'chest': (-14, 0, 0), 'head': (-20, 0, 0), 'arm_r': (60, -30, 0),
                'arm_l': (70, 30, 0), 'root_pos': (0, 0.05, -0.02)}),
         (0.45, {'root_rot': (-40, 0, 6), 'root_pos': (0, 0.3, -0.25), 'chest': (-10, 0, 0), 'head': (-10, 0, 0),
                 'thigh_r': (-10, -4, 0), 'shin_r': (30, 0, 0), 'thigh_l': (20, 4, 0), 'shin_l': (10, 0, 0),
                 'arm_r': (120, -30, 0), 'arm_l': (130, 30, 0)}),
         (0.65, {'root_rot': (-85, 0, 6), 'root_pos': (0, 0.68, -0.76), 'chest': (-4, 0, 0), 'head': (-30, 0, 10),
                 'thigh_r': (-4, -6, 0), 'shin_r': (10, 0, 0), 'thigh_l': (4, 6, 0), 'shin_l': (6, 0, 0),
                 'arm_r': (165, -30, 0), 'forearm_r': (10, 0, 0), 'arm_l': (165, 30, 0), 'forearm_l': (10, 0, 0)}),
         (1.0, {'root_rot': (-90, 0, 6), 'root_pos': (0, 0.7, -0.8), 'chest': (0, 0, 0), 'head': (-34, 0, 14),
                'thigh_r': (-4, -6, 0), 'shin_r': (8, 0, 0), 'thigh_l': (4, 6, 0), 'shin_l': (4, 0, 0),
                'arm_r': (170, -35, 0), 'forearm_r': (8, 0, 0), 'arm_l': (170, 35, 0), 'forearm_l': (8, 0, 0)})]
    anim = H.keys(dict(base, **{c: np.asarray(v, np.float32) for c, v in fk.items()}), k)
    return H.Action(lambda t: anim(H._warp(t, [(0.65, down)])), legs='fk', arms='fk')


def anim_bearer_fidget(base, phase):
    B = base
    s = 1 if phase == 0 else -1
    k = [(0.0, {}), (0.25, {'head': (0, 0, 35 * s), 'chest': (0, 0, 6 * s)}),
         (0.5, {'head': (-6, 0, 20 * s), 'root_pos': (0.03 * s, 0, -0.02)}),
         (0.75, {'head': (4, 0, -30 * s), 'chest': (0, 0, -6 * s), 'root_pos': (-0.02 * s, 0, -0.01)}),
         (1.0, {'head': (0, 0, 0), 'chest': (0, 0, 0), 'root_pos': (0, 0, 0)})]
    return H.Action(H.keys(B, k))
