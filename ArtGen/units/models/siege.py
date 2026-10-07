"""Siege engines and towed guns with their crews: Catapult, Trebuchet,
Hwacha, Cannon, Artillery (a WWII field gun) and Flak (a quad AA gun).

The engines are rigid parts (frames, wheels, throwing arms, barrels) moved
per frame; the crews are models/humanoid.py figures posed directly (hand
and foot targets in the engine's space) or with the humanoid animation
library (deaths, victories). Shots come with muzzle flashes and smoke
(models/vfx.py); deaths smash the engine (it collapses or blows up) while
the crewman falls.

Catalog parameters: 'crew' (humanoid parameters for the crewman), 'colors',
'tint' (engine materials drawn in the civ color), 'fire' (the shot's
normalized time in ATTACK1; measured from the original by default).
"""
import math

import numpy as np

import mesh as M
import rig
from models import archetype
from models import humanoid as HU
from models import vparts as VP
from models import vfx as FX
from models.vehicles import VehicleBase, flash_time, _orient_y

HEAD = HU.HEAD


class _Ctx:
    """A stand-in action context for the crew's humanoid animations."""

    def __init__(self, n=15, events=None):
        self.n = n
        self.events = events or {}

    def event(self, name, default=None):
        return self.events.get(name, default)


# ------------------------------------------------------------ hats (head bone space)

def hat_shako(mats, body, plume):
    S = M.scale(HEAD)
    parts = [M.cylinder(0.118, 0.13, 0.2, body, seg=14, z0=0.15).transformed(M.scale(1, 1.08, 1)),
             M.box(0.2, 0.08, 0.02, body, center=(0, 0.13, 0.15)),
             M.capsule((0, 0.10, 0.33), (0, 0.06, 0.48), 0.03, 0.045, plume, seg=8, rings=2)]
    return M.Mesh.concat(parts).transformed(S)


def hat_steel(mats, m):
    S = M.scale(HEAD)
    dome = M.ellipsoid(0.135, 0.145, 0.12, m, (0, 0.0, 0.15), seg=14)
    brim = M.cylinder(0.165, 0.16, 0.025, m, seg=16, z0=0.12).transformed(M.scale(1, 1.06, 1))
    return M.Mesh.concat([dome, brim]).transformed(S)


def hat_gat(mats, m, band):
    """A wide-brimmed conical hat (Korean, Chinese)."""
    S = M.scale(HEAD)
    crown = M.cylinder(0.11, 0.09, 0.16, m, seg=14, z0=0.17)
    brim = M.cylinder(0.24, 0.20, 0.03, m, seg=18, z0=0.16)
    b = M.cylinder(0.113, 0.11, 0.03, band, seg=14, z0=0.18)
    return M.Mesh.concat([crown, brim, b]).transformed(S)


def hat_cap(mats, m):
    S = M.scale(HEAD)
    dome = M.ellipsoid(0.12, 0.13, 0.1, m, (0, -0.005, 0.16), seg=14)
    rim = M.cylinder(0.125, 0.125, 0.04, m, seg=14, z0=0.12).transformed(M.scale(1, 1.08, 1))
    return M.Mesh.concat([dome, rim]).transformed(S)


class Crew:
    """A crewman: a humanoid posed in its own space (facing +y), placed by
    a transform into the engine's model space."""

    def __init__(self, params):
        p = dict(params)
        hat = p.pop('hat', None)
        self.h = HU.Humanoid('crew', p)
        self.mats = self.h.mats
        m = self.mats
        if hat:
            kind = hat[0]
            if kind == 'shako':
                mesh = hat_shako(m, VP_mat(m, 'hat', hat[1]), VP_mat(m, 'plume', hat[2]))
            elif kind == 'steel':
                mesh = hat_steel(m, VP_mat(m, 'hat', hat[1]))
            elif kind == 'gat':
                mesh = hat_gat(m, VP_mat(m, 'hat', hat[1]), VP_mat(m, 'hatband', hat[2]))
            else:
                mesh = hat_cap(m, VP_mat(m, 'hat', hat[1]))
            self.h.parts.append(('head', mesh, 'hat'))
        self.base = dict(self.h.base)

    def pose(self, **over):
        p = dict(self.base)
        for k, v in over.items():
            p[k] = np.asarray(v, np.float32)
        return p

    def mesh(self, pose, T, weapon=None, legs='ik', arms='ik'):
        act = HU.Action(None, legs=legs, arms=arms)
        return self.h.pose_mesh(pose, act, weapon).transformed(T)

    def anim(self, action, t, T, n=15, events=None, weapon=None):
        """A pose from the humanoid library (INI action names: DEATH,
        VICTORY, FORTIFY, FIDGET, RUN, DEFAULT)."""
        act = self.h.action(action, _Ctx(n, events))
        pose = act.fn(t)
        return self.h.pose_mesh(pose, act, weapon).transformed(T)

    def run_pose(self, t, hands=None, lean=-16):
        """Legs running in place (the humanoid run), hands on a bar."""
        act = self.h.action('RUN', _Ctx(10))
        p = act.fn(t)
        if hands is not None:
            p['hand_r'], p['hand_l'] = np.asarray(hands[0], np.float32), np.asarray(hands[1], np.float32)
            p['elbow_r'] = np.array([0.6, -0.5, -0.6], np.float32)
            p['elbow_l'] = np.array([-0.6, -0.5, -0.6], np.float32)
        p['root_rot'] = np.array([lean, 0, p['root_rot'][2]], np.float32)
        return p


def VP_mat(mats, name, color):
    if name not in mats:
        mats.add(name, color, spec=0.06, gloss=10)
    return mats[name]


def place(x, y, z=0.0, yaw=0.0):
    return M.translate(x, y, z) @ M.rot_z(yaw)


def inv(T):
    return np.linalg.inv(T).astype(np.float32)


def local(T, p):
    """A point in the engine's space -> the crewman's space."""
    return (inv(T) @ np.array([*p, 1.0], np.float32))[:3]


# ------------------------------------------------------------ the base

class Engine(VehicleBase):
    """Shared by the crewed engines: materials (the crew's table), crew,
    and a ground shake helper."""

    loop_actions = ('DEFAULT', 'RUN', 'WALK', 'FIDGET')

    def __init__(self, unit, params, paints, metal=()):
        super().__init__(unit, params)
        self.crew = Crew(params['crew']) if params.get('crew') else None
        if self.crew is not None:
            self.mats = self.crew.mats
        self.paint(paints, metal=metal)

    def fire_t(self, ctx, default=0.3):
        tf = self.p.get('fire')
        return flash_time(ctx, default) if tf is None else tf

    def frame(self, action, f, n, ctx):
        t = self.times(action, f, n)
        fn = {'DEFAULT': self.idle, 'RUN': self.run, 'WALK': self.run, 'ATTACK1': self.attack,
              'DEATH': self.death, 'FORTIFY': self.fortify, 'FIDGET': self.fidget,
              'VICTORY': self.victory}.get(action, self.idle)
        out = fn(t, ctx)
        if isinstance(out, FX.FxMesh):
            return out
        return FX.FxMesh(out, self.mats)

    def fortify(self, t, ctx):
        return self.idle(t, ctx)

    def fidget(self, t, ctx):
        return self.idle(t, ctx)

    def victory(self, t, ctx):
        return self.idle(t, ctx)


# ------------------------------------------------------------ the catapult

CATAPULT_PAINT = {'wood': '#b09468', 'wood_dark': '#74583a', 'beam': '#e8e8e8', 'side_rail': '#8e7250', 'rope': '#a89870',
                  'iron': ('#6e6e6c', 0.5, 22), 'stone': '#8c8884', 'wheel': '#6e4628'}


@archetype('catapult')
class Catapult(Engine):
    """A wheeled torsion catapult. The arm rests cocked back with a stone
    in its cup; the crewman cranks the winch, the arm whips up against the
    padded crossbar and the stone flies; then he winds it back down."""

    REST, UP = 22.0, 128.0          # arm elevation (degrees) of its cup end, measured from the rear horizontal
    SHOW = 72.0                     # standing/fortified (what the map shows): the arm raised, cup and stone up high, as in Civ3

    def __init__(self, unit, params):
        super().__init__(unit, params, CATAPULT_PAINT, metal=('iron',))
        m = self.mats
        L, W, zr = 3.0, 1.15, 0.5
        parts = []
        for sx in (-1, 1):
            parts.append(VP.bar((sx * W / 2, -L / 2, zr), (sx * W / 2, L / 2, zr), 0.17, 0.2, m['side_rail']))
            parts.append(VP.bar((sx * W / 2, -L / 2 + 0.15, zr + 0.19), (sx * W / 2, L / 2 - 0.25, zr + 0.19), 0.13, 0.13, m['wood']))
            # the A-frame holding the crossbar
            parts.append(VP.bar((sx * W / 2, 0.75, zr + 0.1), (sx * W / 2, 0.38, 1.55), 0.13, 0.13, m['wood']))
            parts.append(VP.bar((sx * W / 2, -0.25, zr + 0.1), (sx * W / 2, 0.32, 1.52), 0.11, 0.11, m['wood']))
            parts.append(VP.bar((sx * W / 2, 1.3, zr + 0.1), (sx * W / 2, 0.42, 1.5), 0.1, 0.1, m['wood']))
        for y in (-1.35, -0.45, 0.7, 1.4):
            parts.append(VP.bar((-W / 2, y, zr), (W / 2, y, zr), 0.14, 0.14, m['wood']))
        parts.append(VP.bar((-W / 2 - 0.12, 0.36, 1.58), (W / 2 + 0.12, 0.36, 1.58), 0.22, 0.22, m['beam']))   # crossbar
        parts.append(VP.bar((-0.28, 0.24, 1.56), (0.28, 0.24, 1.56), 0.12, 0.14, m['rope']))                # pad
        # torsion bundle and the winch at the rear
        parts.append(VP.axle_x(0.19, W - 0.1, m['rope'], seg=12, center=(0, -0.5, zr + 0.22)))
        parts.append(VP.axle_x(0.12, W + 0.3, m['wood_dark'], seg=10, center=(0, -1.3, zr + 0.28)))
        for sx in (-1, 1):
            parts.append(VP.axle_x(0.2, 0.05, m['iron'], seg=14, center=(sx * (W / 2 + 0.12), -1.3, zr + 0.28)))
            parts.append(VP.bar((sx * (W / 2 + 0.16), -1.3, zr + 0.28), (sx * (W / 2 + 0.16), -1.3 + 0.0, zr + 0.55), 0.05, 0.05, m['iron']))
        self.frame_mesh = M.Mesh.concat(parts)
        self.wheels = [(sx * (W / 2 + 0.16), y, 0.42) for sx in (-1, 1) for y in (-1.05, 1.05)]
        self.pivot = np.array([0, -0.5, zr + 0.22], np.float32)
        arm = [VP.bar((0, 0.15, 0), (0, -2.0, 0), 0.17, 0.17, m['wood'])]
        cup = M.lathe([(0.0, 0.01), (0.03, 0.24), (0.17, 0.3), (0.21, 0.3)], m['iron'], seg=14).transformed(
            M.translate(0, -2.1, 0.02))
        arm.append(cup)
        self.arm = M.Mesh.concat(arm)
        self.stone = VP.icosphere(0.2, m['stone'], sub=1, jitter=0.12, seed=3)
        self.k = 0.82                      # the engine is compact next to its crewman
        self.K = M.scale(self.k)
        self.crew_T = place(0.0, -2.0 * self.k, 0.0, 0)
        self.autofit()

    def wheels_mesh(self, ang=0.0):
        m = self.mats
        return M.Mesh.concat([VP.spoked_wheel(r, 0.14, m, m['wheel'], m['wood_dark'], m['iron'], n_spokes=6,
                                              angle=ang, center=(x, y, r)) for x, y, r in self.wheels])

    def arm_T(self, ang):
        # ang: elevation of the arm's free end toward the rear (from the horizontal)
        return M.translate(*self.pivot) @ M.rot_x(-ang)

    def cup_point(self, ang):
        return (self.K @ self.arm_T(ang) @ np.array([0, -2.1, 0.2, 1], np.float32))[:3]

    def engine(self, ang, wheel=0.0, stone=True, char=False):
        parts = [self.frame_mesh, self.wheels_mesh(wheel), self.arm.transformed(self.arm_T(ang))]
        out = M.Mesh.concat(parts).transformed(self.K)
        if stone:
            out = out + self.stone.transformed(M.translate(*self.cup_point(ang)))
        return out

    def crank_pose(self, t, speed=1.0):
        c = np.array([0.0, -1.3, 0.78], np.float32) * self.k
        a = 2 * math.pi * t * speed
        T = self.crew_T
        hr = c + np.array([0.75, 0.25 * math.cos(a) - 0.05, 0.25 * math.sin(a)], np.float32)
        hl = c + np.array([-0.75, 0.25 * math.cos(a + math.pi) - 0.05, 0.25 * math.sin(a + math.pi)], np.float32)
        hr = local(T, hr)
        hl = local(T, hl)
        hr[0] = 0.42; hl[0] = -0.42
        return self.crew.pose(hand_r=hr, hand_l=hl, spine=(-14, 0, 0), chest=(-10, 0, 0), head=(18, 0, 0),
                              root_pos=(0, 0.02, -0.08 + 0.02 * math.sin(a)),
                              foot_r=(0.2, 0.05, HU.ANKLE_H), foot_l=(-0.2, -0.25, HU.ANKLE_H),
                              elbow_r=(0.7, -0.6, -0.3), elbow_l=(-0.7, -0.6, -0.3))

    def idle(self, t, ctx):
        s = math.sin(2 * math.pi * t)
        pose = self.crew.pose(hand_r=(0.3, 0.3, 1.0 + 0.02 * s), hand_l=(-0.3, 0.3, 1.0 + 0.02 * s),
                              root_pos=(0, 0, -0.03 + 0.01 * s), spine=(-8, 0, 0), head=(6, 0, 4 * s))
        return M.Mesh.concat([self.engine(self.SHOW), self.crew.mesh(pose, self.crew_T)])

    def run(self, t, ctx):
        ang = -360 * t * 1.0
        bar = [local(self.crew_T, (0.35, -1.25, 0.85)), local(self.crew_T, (-0.35, -1.25, 0.85))]
        pose = self.crew.run_pose(t, bar)
        bob = 0.015 * math.sin(4 * math.pi * t)
        eng = self.engine(self.REST, wheel=ang).transformed(M.translate(0, 0, bob))
        return M.Mesh.concat([eng, self.crew.mesh(pose, self.crew_T @ M.translate(0, 0.25, 0))])

    def attack(self, t, ctx):
        """Crank, release (the arm whips up), the stone flies, wind back."""
        tr = self.fire_t(ctx, 0.36)
        m = self.mats
        fx = []
        if t < tr:
            ang = self.REST - 4 * (t / tr)
            pose = self.crank_pose(t / tr * 2)
            stone = True
        else:
            u = (t - tr)
            swing = min(1.0, u / 0.06)
            back = rig.smoothstep(min(1.0, max(0.0, (u - 0.12) / (1 - tr - 0.12))))
            ang = (self.REST - 4) + (self.UP - self.REST + 4) * swing
            ang = ang + (self.REST - self.UP) * back
            if swing >= 1.0 and u < 0.14:
                ang += 6 * math.sin(u * 60) * math.exp(-u * 20)   # shudders against the crossbar
            pose = self.crank_pose(0.5 + u * 1.5) if u > 0.1 else self.crew.pose(
                hand_r=(0.3, 0.25, 1.1), hand_l=(-0.3, 0.25, 1.1), spine=(4, 0, 0), head=(-10, 0, 0))
            stone = False
            # the stone in flight, forward and up out of the frame
            if u < 0.5:
                p0 = self.cup_point(self.UP)
                v = np.array([0, 9.0, 6.5], np.float32)
                pos = p0 + v * u * 1.3 + np.array([0, 0, -4.9 * (u * 1.3) ** 2], np.float32)
                fx.append(self.stone.transformed(M.translate(*pos)))
        eng = self.engine(ang, stone=stone)
        return M.Mesh.concat([eng, self.crew.mesh(pose, self.crew_T)] + fx)

    def death(self, t, ctx):
        """Hit: dust and splinters, the frame collapses, the crewman falls."""
        m = self.mats
        hit = 0.12
        u = max(0.0, t - hit)
        k = rig.smoothstep(min(1.0, u / 0.45))
        parts = []
        # the frame sags and breaks: the A-frame and crossbar fall to the side
        parts.append(self.frame_mesh.transformed(M.translate(0, 0, -0.25 * k) @ M.rot_y(9 * k) @ M.rot_x(-4 * k)))
        parts.append(self.wheels_mesh(0).transformed(M.translate(0.4 * k, 0, 0) @ M.rot_y(-25 * k)))
        parts.append(self.arm.transformed(M.translate(0, 0, -0.3 * k) @ self.arm_T(self.REST + 30 * k) @ M.rot_y(30 * k)))
        crew = self.crew.anim('DEATH', min(1.0, t * 1.15), self.crew_T, n=19)
        fx = []
        if t >= hit:
            fx.append(FX.explosion(m, (0, 0.2, 0.9), 1.3, u / 0.6, seed=9, n=8, smoke_dark=0.1))
            fx.append(FX.debris(m, (0, 0.2, 1.0), 0.8, min(u / 0.7, 1.0), seed=3, n=10, names=('wood', 'wood_dark')))
        if u > 0.35:
            fx.append(FX.smoke_column(m, (0, 0.2, 0.6), 0.8, u, seed=4, n=4, color=0.45, height=2.4))
        mesh = M.Mesh.concat(parts).transformed(self.K)
        if u > 0.2:
            mesh = VP.recolor(mesh, {m['wood']: m['wood_dark']})
        return M.Mesh.concat([mesh, crew] + fx)

    def fortify(self, t, ctx):
        k = rig.smoothstep(min(1.0, t / 0.6))
        pose = self.crew.pose(root_pos=(0, 0, -0.38 * k), spine=(-20 * k, 0, 0), hand_r=(0.3, 0.32, 0.9 - 0.3 * k),
                              hand_l=(-0.3, 0.32, 0.9 - 0.3 * k), foot_r=(0.22, 0.2, HU.ANKLE_H),
                              foot_l=(-0.22, -0.3, HU.ANKLE_H))
        return M.Mesh.concat([self.engine(self.SHOW), self.crew.mesh(pose, self.crew_T)])

    def fidget(self, t, ctx):
        s = math.sin(2 * math.pi * t)
        pose = self.crank_pose(0.15 * s)
        return M.Mesh.concat([self.engine(self.SHOW), self.crew.mesh(pose, self.crew_T)])

    def victory(self, t, ctx):
        T = self.crew_T @ M.translate(0.9, 0.2, 0) @ M.rot_z(-30)
        crew = self.crew.anim('VICTORY', t, T)
        return M.Mesh.concat([self.engine(self.REST), crew])


# ------------------------------------------------------------ the trebuchet

TREBUCHET_PAINT = {'wood': '#b89c6e', 'wood_dark': '#7e6040', 'band': '#e8e8e8', 'rope': '#d8d0b8',
                   'iron': ('#6a6a68', 0.5, 22), 'stone': '#6c6a68', 'wheel': '#7a5430', 'box': '#b08a52'}


@archetype('trebuchet')
class Trebuchet(Engine):
    """A wheeled counterweight trebuchet. At rest the long arm lies back
    along the frame with the counterweight box raised; the throw swings
    the arm over the top, the sling whips the stone off forward, the arm
    rebounds and is slowly winched back down. The crewman kneels by his pile
    of stones."""

    REST = 224.0      # elevation of the long end from the forward horizontal (down to the rear)

    def __init__(self, unit, params):
        super().__init__(unit, params, TREBUCHET_PAINT, metal=('iron',))
        m = self.mats
        L, W, zr = 3.6, 1.3, 0.5
        P_ = []
        for sx in (-1, 1):
            P_.append(VP.bar((sx * W / 2, -L / 2, zr), (sx * W / 2, L / 2, zr), 0.18, 0.2, m['wood']))
            for y in (-1.2, 1.2):
                P_.append(M.box(0.2, 0.24, 0.22, m['band'], center=(sx * W / 2, y, zr)))
            # the towers: two legs up to the axle, braced
            P_.append(VP.bar((sx * W / 2, -1.25, zr), (sx * W / 2 * 0.8, 0.15, 2.55), 0.15, 0.15, m['wood']))
            P_.append(VP.bar((sx * W / 2, 1.45, zr), (sx * W / 2 * 0.8, 0.25, 2.55), 0.15, 0.15, m['wood']))
            P_.append(VP.bar((sx * W / 2, -0.6, 1.4), (sx * W / 2, 0.95, 1.4), 0.1, 0.1, m['wood']))
            P_.append(M.box(0.18, 0.18, 0.2, m['band'], center=(sx * W / 2 * 0.8, 0.2, 2.5)))
            P_.append(M.box(0.16, 0.2, 0.2, m['band'], center=(sx * W / 2 * 0.92, -0.62, 1.45)))
        for y in (-1.7, -0.6, 0.6, 1.7):
            P_.append(VP.bar((-W / 2, y, zr), (W / 2, y, zr), 0.15, 0.15, m['wood']))
        P_.append(VP.axle_x(0.08, W * 0.85, m['iron'], center=(0, 0.2, 2.55)))
        # the winch at the rear
        P_.append(VP.axle_x(0.14, W, m['wood_dark'], center=(0, -1.45, zr + 0.3)))
        for sx in (-1, 1):
            P_.append(VP.spoked_wheel(0.3, 0.06, m, m['wood_dark'], m['wood'], m['iron'], n_spokes=4,
                                      center=(sx * (W / 2 + 0.1), -1.45, zr + 0.3)))
        self.frame_mesh = M.Mesh.concat(P_)
        self.wheels = [(sx * (W / 2 + 0.15), y, 0.36) for sx in (-1, 1) for y in (-1.25, 1.25)]
        self.pivot = np.array([0, 0.2, 2.55], np.float32)
        self.long, self.short = 3.5, 1.0
        arm = [VP.bar((0, -self.short, 0), (0, self.long, 0), 0.16, 0.18, m['wood'])]
        for y in (-0.6, 0.9, 1.9, 2.9):
            arm.append(M.box(0.2, 0.18, 0.22, m['band'], center=(0, y, 0)))
        self.arm = M.Mesh.concat(arm)
        box = [M.box(0.95, 0.95, 0.9, m['box'], center=(0, 0, -0.75), bevel=0.03)]
        for z in (-0.45, -1.05):
            box.append(M.box(1.0, 1.0, 0.1, m['wood_dark'], center=(0, 0, z)))
        box.append(VP.bar((-0.4, 0, -0.3), (0.0, 0, 0.0), 0.06, 0.06, m['iron']))
        box.append(VP.bar((0.4, 0, -0.3), (0.0, 0, 0.0), 0.06, 0.06, m['iron']))
        self.box = M.Mesh.concat(box)
        self.ball = VP.icosphere(0.22, m['stone'], sub=1, jitter=0.1, seed=5)
        self.pile = M.Mesh.concat([VP.icosphere(0.2, m['stone'], (x, y, 0.19), sub=1, jitter=0.1, seed=i)
                                   for i, (x, y) in enumerate([(0, 0), (0.38, 0.05), (0.18, 0.33), (0.2, 0.12)])]
                                  ).transformed(M.translate(1.35, -2.1, 0))
        self.crew_T = place(1.25, -1.45, 0.0, 70)
        self.k = 1.0
        self.K = M.scale(self.k)
        self.autofit()

    def arm_T(self, phi):
        return M.translate(*self.pivot) @ M.rot_x(phi)

    def tip(self, phi):
        return (self.arm_T(phi) @ np.array([0, self.long, 0, 1], np.float32))[:3]

    def wheels_mesh(self, ang=0.0):
        m = self.mats
        return M.Mesh.concat([VP.spoked_wheel(r, 0.16, m, m['wheel'], m['wood_dark'], m['iron'], n_spokes=6,
                                              angle=ang, center=(x, y, r)) for x, y, r in self.wheels])

    def engine(self, phi, wheel=0.0, ball=True, sling_dir=None):
        A = self.arm_T(phi)
        parts = [self.frame_mesh, self.wheels_mesh(wheel), self.arm.transformed(A)]
        short_end = (A @ np.array([0, -self.short, 0, 1], np.float32))[:3]
        parts.append(self.box.transformed(M.translate(*short_end)))       # hangs plumb
        tip = (A @ np.array([0, self.long, 0, 1], np.float32))[:3]
        if sling_dir is None:
            sling_dir = np.array([0, 0, -1.0], np.float32)
        end = tip + np.asarray(sling_dir, np.float32) * 0.9
        end[2] = max(end[2], 0.25)
        m = self.mats
        parts.append(VP.tube(tip, end, 0.025, 0.025, m['rope'], seg=5))
        if ball:
            parts.append(self.ball.transformed(M.translate(*end)))
        parts.append(self.pile)
        return M.Mesh.concat(parts).transformed(self.K)

    def kneel(self, s=0.0):
        return self.crew.pose(root_pos=(0, 0.05, -0.42), spine=(-12, 0, 0), head=(10, 0, 6 * s),
                              foot_r=(0.18, 0.32, HU.ANKLE_H), foot_l=(-0.18, -0.38, HU.ANKLE_H + 0.05),
                              toe_l=-50.0, hand_r=(0.3, 0.42, 0.62), hand_l=(-0.28, 0.4, 0.62))

    def idle(self, t, ctx):
        s = math.sin(2 * math.pi * t)
        pose = self.crew.pose(head=(4, 0, 10 * s), hand_r=(0.2, 0.18, 0.95), hand_l=(-0.2, 0.18, 0.95),
                              root_pos=(0, 0, 0.005 * s))
        return M.Mesh.concat([self.engine(self.REST), self.crew.mesh(pose, self.crew_T)])

    def run(self, t, ctx):
        T = place(0.0, -2.0 * self.k - 0.5, 0)
        bar = [local(T, (0.35, -1.75 * self.k, 0.95)), local(T, (-0.35, -1.75 * self.k, 0.95))]
        pose = self.crew.run_pose(t, bar)
        eng = self.engine(self.REST, wheel=-360 * t)
        return M.Mesh.concat([eng, self.crew.mesh(pose, T)])

    def attack(self, t, ctx):
        tr = self.fire_t(ctx, 0.22)    # the release
        keys = [(0.0, self.REST), (tr * 0.55, 95.0), (tr, -5.0), (tr + 0.08, 30.0), (tr + 0.2, 80.0),
                (tr + 0.3, 86.0), (1.0, self.REST)]
        phi = float(np.interp(t, [k[0] for k in keys], [k[1] for k in keys]))
        fx = []
        ball = t < tr
        if t < tr:
            # the sling trails behind the swing
            u = t / tr
            sd = np.array([0, -math.sin(math.radians(phi)) * 0.6 - 0.3 * u,
                           -math.cos(math.radians(phi * 0.5)) * 0.5], np.float32)
            sd = sd / (np.linalg.norm(sd) + 1e-9)
        else:
            sd = np.array([0, 0.3, -1.0], np.float32)
            sd /= np.linalg.norm(sd)
            u = t - tr
            if u < 0.4:
                p0 = self.tip(-5.0) * self.k
                pos = p0 + np.array([0, 18.0 * u, 4.0 * u - 6 * u * u], np.float32)
                fx.append(self.ball.transformed(M.translate(*pos)))
        eng = self.engine(phi, ball=ball, sling_dir=sd)
        return M.Mesh.concat([eng, self.crew.mesh(self.kneel(), self.crew_T)] + fx)

    def death(self, t, ctx):
        m = self.mats
        hit = 0.1
        u = max(0.0, t - hit)
        k = rig.smoothstep(min(1.0, u / 0.5))
        parts = [self.frame_mesh.transformed(M.translate(0, 0, -0.35 * k) @ M.rot_y(8 * k)),
                 self.wheels_mesh(0).transformed(M.translate(0.3 * k, 0, 0) @ M.rot_y(-20 * k))]
        A = M.translate(0, 0, -1.6 * k) @ M.translate(*self.pivot) @ M.rot_y(70 * k) @ M.rot_x(self.REST + 30 * k)
        parts.append(self.arm.transformed(A))
        parts.append(self.box.transformed(M.translate(0.3 * k, 0.6, 2.0 - 1.45 * k) @ M.rot_z(25 * k)))
        parts.append(self.pile)
        mesh = M.Mesh.concat(parts).transformed(self.K)
        if u > 0.25:
            mesh = VP.recolor(mesh, {m['wood']: m['wood_dark']})
        crew = self.crew.anim('DEATH', min(1.0, t * 1.1), self.crew_T, n=20)
        fx = []
        if t >= hit:
            fx.append(FX.explosion(m, (0, 0.2, 1.4), 1.4, u / 0.6, seed=9, n=8, smoke_dark=0.1))
            fx.append(FX.debris(m, (0, 0.2, 1.6), 0.9, min(u / 0.7, 1.0), seed=3, n=12,
                                names=('wood', 'band', 'wood_dark')))
        return M.Mesh.concat([mesh, crew] + fx)

    def fortify(self, t, ctx):
        k = rig.smoothstep(min(1.0, t / 0.7))
        p0 = self.crew.pose()
        p1 = self.kneel()
        pose = {c: p0[c] * (1 - k) + p1[c] * k if c in p1 else p0[c] for c in p0}
        return M.Mesh.concat([self.engine(self.REST), self.crew.mesh(pose, self.crew_T)])

    def fidget(self, t, ctx):
        s = math.sin(2 * math.pi * t)
        pose = self.crew.pose(head=(10, 0, 25 * s), chest=(0, 0, 10 * s), hand_r=(0.25, 0.1, 0.9),
                              hand_l=(-0.25, 0.1, 0.9))
        return M.Mesh.concat([self.engine(self.REST), self.crew.mesh(pose, self.crew_T)])

    def victory(self, t, ctx):
        return M.Mesh.concat([self.engine(self.REST), self.crew.anim('VICTORY', t, self.crew_T)])
