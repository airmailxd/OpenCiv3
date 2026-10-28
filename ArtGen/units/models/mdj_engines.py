"""Medieval Japan's engines (catalog/mdj_mounted.py), built on the siege and
gun archetypes (models/siege.py, models/guns.py) without changing them:

  * 'mdj_catapult' (Medieval Japanese Catapult): a torsion catapult on a low
    planked chassis with four small wheels, its A-frame carrying a very wide
    crossbar whose ends sweep forward like a bow's limbs, lashed in the civ
    color, with cords from the tips to the arm. The crewman in a civ-colored
    jingasa stands at the rear holding the stone in the cup. The shot, the
    wreck and the rest are the Catapult's.
  * 'fire_cannon' (Fire Cannon): a long iron gun lying on a low four-wheeled
    box carriage of stacked beams with civ-colored iron straps, its gunner
    standing behind the breech. Firing, recoil, smoke and the wreck are the
    Cannon's.

Crew parameters: 'jingasa', a wide, flat conical hat in the 'hat' material
(add 'hat' to the crew's colors and, for the civ color, its tint); 'pants',
trousers in that material (models/mounted.py's rider trousers).
"""
import math

import numpy as np

import mesh as M
from models import archetype
from models import humanoid as HU
from models import vparts as VP
from models.siege import Catapult, Engine, place, local
from models.guns import Cannon


def jingasa(m, r=0.25, h=0.09, z=0.17):
    """A flat conical hat (head bone space, after the head scale)."""
    hat = M.lathe([(0.0, 0.0005), (0.0, r), (0.012, r), (h, 0.04), (h + 0.012, 0.0005)], m, seg=18)
    return hat.transformed(M.scale(HU.HEAD) @ M.translate(0, 0.0, z))


def _add_jingasa(crew, params):
    """The crew's extra gear: the jingasa, and trousers ('pants', as the
    mounted riders wear them) under a short coat."""
    if params.get('jingasa') and 'hat' in crew.mats:
        crew.h.parts.append(('head', jingasa(crew.mats['hat']), 'hat'))
    if params.get('pants'):
        from models.mounted import rider_extras
        crew.h.parts += rider_extras(crew.h.body, {k: params[k] for k in ('pants', 'build') if k in params})


# ------------------------------------------------------------ the catapult

MDJ_CATAPULT_PAINT = {'wood': '#c49a6a', 'wood_dark': '#5e4634', 'plank': '#6e5440', 'band': '#e8e8e8',
                      'rope': '#a89070', 'cord': '#2e2a26', 'iron': ('#6a6a6c', 0.5, 22), 'stone': '#8a8686',
                      'wheel': '#7a5a3e', 'beam': '#c49a6a', 'side_rail': '#6e5440'}


@archetype('mdj_catapult')
class MdjCatapult(Catapult):
    REST = 12.0
    UP = 112.0
    SHOW = 12.0          # the map shows it cocked, the crewman loading the stone, as in the original

    def __init__(self, unit, params):
        Engine.__init__(self, unit, params, MDJ_CATAPULT_PAINT, metal=('iron',))
        _add_jingasa(self.crew, params.get('crew', {}))
        m = self.mats
        L, W, zr = 2.9, 1.1, 0.42
        parts = []
        # the chassis: two heavy side rails and a planked bed between them
        for sx in (-1, 1):
            parts.append(VP.bar((sx * W / 2, -L / 2, zr), (sx * W / 2, L / 2, zr), 0.18, 0.26, m['plank']))
        for i in range(5):
            y = -L / 2 + 0.25 + i * (L - 0.5) / 4
            parts.append(VP.bar((-W / 2, y, zr + 0.02), (W / 2, y, zr + 0.02), 0.16, 0.20, m['plank']))
        parts.append(M.box(W - 0.1, L - 0.3, 0.12, m['wood_dark'], center=(0, 0, zr - 0.05)))
        # civ-colored iron straps on the rails
        for sx in (-1, 1):
            for y in (-1.1, 0.0, 1.1):
                parts.append(M.box(0.22, 0.12, 0.30, m['band'], center=(sx * W / 2, y, zr)))
        # the A-frame
        top = (0.42, 1.62)
        for sx in (-1, 1):
            parts.append(VP.bar((sx * W / 2, 1.25, zr + 0.1), (sx * W / 2, top[0], top[1]), 0.13, 0.13, m['wood']))
            parts.append(VP.bar((sx * W / 2, -0.30, zr + 0.1), (sx * W / 2, top[0] - 0.05, top[1] - 0.04), 0.12, 0.12, m['wood']))
            parts.append(M.box(0.17, 0.16, 0.14, m['band'], center=(sx * W / 2, top[0], top[1] - 0.04)))
        # the wide crossbar: straight across the frame, its ends sweeping forward and up
        y0, z0 = top[0] - 0.02, top[1] + 0.06
        segs = [(0.0, 0.0, 0.0), (0.62, 0.0, 0.0), (1.25, 0.16, 0.05), (1.78, 0.42, 0.12)]
        for sx in (-1, 1):
            for a, b in zip(segs[:-1], segs[1:]):
                parts.append(VP.bar((sx * a[0], y0 + a[1], z0 + a[2]), (sx * b[0], y0 + b[1], z0 + b[2]), 0.15, 0.15, m['beam']))
            for (x, dy, dz) in ((0.36, 0.0, 0.0), (1.0, 0.09, 0.03), (1.66, 0.35, 0.10)):
                parts.append(M.box(0.12, 0.2, 0.2, m['band'], center=(sx * x, y0 + dy, z0 + dz)))
        self.tips = [np.array((sx * 1.74, y0 + 0.40, z0 + 0.11), np.float32) for sx in (-1, 1)]
        parts.append(VP.bar((-0.26, top[0] - 0.12, z0 - 0.02), (0.26, top[0] - 0.12, z0 - 0.02), 0.12, 0.14, m['rope']))   # pad
        # the torsion bundle
        parts.append(VP.axle_x(0.18, W - 0.1, m['rope'], seg=12, center=(0, -0.45, zr + 0.24)))
        for sx in (-1, 1):
            parts.append(VP.axle_x(0.21, 0.06, m['band'], seg=12, center=(sx * (W / 2 - 0.02), -0.45, zr + 0.24)))
        self.frame_mesh = M.Mesh.concat(parts)
        self.wheels = [(sx * (W / 2 + 0.14), y, 0.30) for sx in (-1, 1) for y in (-1.0, 1.0)]
        self.pivot = np.array([0, -0.45, zr + 0.24], np.float32)
        arm = [VP.bar((0, 0.15, 0), (0, -2.0, 0), 0.17, 0.17, m['wood'])]
        arm.append(M.box(0.2, 0.12, 0.22, m['band'], center=(0, -1.0, 0)))
        cup = M.lathe([(0.0, 0.01), (0.03, 0.22), (0.15, 0.28), (0.19, 0.28)], m['wood_dark'], seg=14).transformed(
            M.translate(0, -2.1, 0.02))
        arm.append(cup)
        self.arm = M.Mesh.concat(arm)
        self.stone = VP.icosphere(0.2, m['stone'], sub=1, jitter=0.12, seed=3)
        self.k = 0.80
        self.K = M.scale(self.k)
        self.crew_T = place(0.0, -2.45, 0.0, 0)
        self.autofit()

    def wheels_mesh(self, ang=0.0):
        m = self.mats
        return M.Mesh.concat([VP.spoked_wheel(r, 0.13, m, m['wheel'], m['wood_dark'], m['iron'], n_spokes=6,
                                              angle=ang, center=(x, y, r)) for x, y, r in self.wheels])

    def engine(self, ang, wheel=0.0, stone=True, char=False):
        out = super().engine(ang, wheel, stone, char)
        # the cords from the crossbar's tips to the arm
        A = self.arm_T(ang)
        p = (A @ np.array([0, -1.0, 0.0, 1], np.float32))[:3]
        cords = [VP.tube(tip, p, 0.025, 0.025, self.mats['cord'], seg=5) for tip in self.tips]
        return out + M.Mesh.concat(cords).transformed(self.K)

    def load_pose(self, s=0.0, lift=0.0):
        """Hands on the stone in the cup, bent over it."""
        c = self.cup_point(self.REST) + np.array([0, 0, lift], np.float32)
        hr = local(self.crew_T, c + np.array([0.17, -0.05, 0.02], np.float32))
        hl = local(self.crew_T, c + np.array([-0.17, -0.05, 0.02], np.float32))
        return self.crew.pose(hand_r=hr, hand_l=hl, spine=(-14, 0, 0), chest=(-8, 0, 0), head=(14, 0, 3 * s),
                              root_pos=(0, 0, -0.04 + 0.01 * s), elbow_r=(0.7, -0.6, -0.4), elbow_l=(-0.7, -0.6, -0.4),
                              foot_r=(0.18, 0.08, HU.ANKLE_H), foot_l=(-0.18, -0.2, HU.ANKLE_H))

    def idle(self, t, ctx):
        s = math.sin(2 * math.pi * t)
        return M.Mesh.concat([self.engine(self.SHOW), self.crew.mesh(self.load_pose(s), self.crew_T)])

    def fidget(self, t, ctx):
        s = math.sin(2 * math.pi * t)
        pose = self.load_pose(s)
        pose['head'] = np.array([10, 0, 25 * s], np.float32)
        return M.Mesh.concat([self.engine(self.SHOW), self.crew.mesh(pose, self.crew_T)])

    def fortify(self, t, ctx):
        import rig
        k = rig.smoothstep(min(1.0, t / 0.6))
        pose = self.load_pose(0.0)
        pose['root_pos'] = np.array([0, 0, -0.04 - 0.22 * k], np.float32)
        pose['spine'] = np.array([-14 - 14 * k, 0, 0], np.float32)
        return M.Mesh.concat([self.engine(self.SHOW), self.crew.mesh(pose, self.crew_T)])


# ------------------------------------------------------------ the fire cannon

FIRE_CANNON_PAINT = {'iron': ('#626468', 0.55, 26), 'carriage': '#c09a6c', 'carriage_dark': '#6e5038',
                     'strap': '#e8e8e8', 'wheel': '#8a6444', 'felloe': '#5a4030', 'rim': ('#4a3a30', 0.2, 12),
                     'hub': ('#2a2a2c', 0.4, 20), 'muzzle': ('#cfc6b0', 0.3, 18)}


@archetype('fire_cannon')
class FireCannon(Cannon):
    def __init__(self, unit, params):
        Engine.__init__(self, unit, params, FIRE_CANNON_PAINT, metal=('iron', 'hub'))
        _add_jingasa(self.crew, params.get('crew', {}))
        m = self.mats
        P_ = []
        # the box carriage: two cheeks of three stacked beams, stepped down toward the rear
        steps = [(-0.78, 0.86, 0.52), (-0.78, 0.74, 0.70), (-0.50, 0.62, 0.86)]
        for sx in (-1, 1):
            for i, (y0, y1, z1) in enumerate(steps):
                z0 = 0.28 if i == 0 else steps[i - 1][2]
                P_.append(M.box(0.14, y1 - y0, z1 - z0 - 0.025, m['carriage'],
                                center=(sx * 0.36, (y0 + y1) / 2, (z0 + z1) / 2)))
                P_.append(M.box(0.145, y1 - y0 - 0.02, 0.025, m['carriage_dark'],
                                center=(sx * 0.36, (y0 + y1) / 2, z1 - 0.012)))
            # civ-colored iron straps over the cheeks
            for y in (-0.70, 0.78):
                P_.append(M.box(0.17, 0.07, 0.62, m['strap'], center=(sx * 0.36, y, 0.56)))
        # transoms and axletrees
        P_.append(M.box(0.6, 0.14, 0.34, m['carriage_dark'], center=(0, 0.80, 0.44)))
        P_.append(M.box(0.6, 0.14, 0.24, m['carriage_dark'], center=(0, -0.72, 0.40)))
        for y in (-0.52, 0.56):
            P_.append(VP.bar((-0.52, y, 0.24), (0.52, y, 0.24), 0.16, 0.14, m['carriage_dark']))
        self.frame_mesh = M.Mesh.concat(P_)
        # the long gun: breech, a long tapering chase, a swelling muzzle with a pale face
        b = M.lathe([(0.0, 0.0005), (0.0, 0.17), (0.08, 0.19), (0.30, 0.18), (0.34, 0.15), (1.6, 0.125), (2.6, 0.10),
                     (2.66, 0.13), (2.80, 0.12), (2.80, 0.06), (2.6, 0.06)], m['iron'], seg=14)
        face = M.lathe([(2.78, 0.0005), (2.79, 0.122), (2.83, 0.11), (2.84, 0.0005)], m['muzzle'], seg=14)
        cas = VP.icosphere(0.09, m['iron'], (0, 0, -0.04), sub=1)
        rings = [M.lathe([(z, 0.0005), (z, r), (z + 0.05, r), (z + 0.05, 0.0005)], m['iron'], seg=14)
                 for z, r in ((0.95, 0.15), (1.9, 0.125))]
        G = M.translate(0, 0, -1.05)
        self.barrel = M.Mesh.concat([x.transformed(G) for x in [b, face, cas] + rings]).transformed(M.rot_x(-90))
        self.trun = np.array([0, 0.05, 0.98], np.float32)
        self.muzzle = np.array([0, 1.8, 0], np.float32)
        self.crew_T = place(0.0, -1.55, 0.0, 0)
        self.autofit()

    def wheels(self, ang=0.0):
        m = self.mats
        return M.Mesh.concat([VP.spoked_wheel(0.26, 0.12, m, m['felloe'], m['wheel'], m['hub'], n_spokes=4, angle=ang,
                                              center=(sx * 0.50, y, 0.26)) for sx in (-1, 1) for y in (-0.52, 0.56)])

    def engine(self, wheel=0.0, recoil=0.0, elev=0.0, rock=0.0):
        return super().engine(wheel, recoil, elev, rock)

    def stand(self, s=0.0, **kw):
        p = dict(hand_r=(0.30, 0.12, 1.0), w_main=(-4, 0, 0), elbow_r=(0.6, -0.8, -0.3),
                 hand_l=(-0.30, 0.02, 0.88), head=(4, 0, 8 * s), root_pos=(0, 0, 0.005 * s))
        p.update(kw)
        return self.crew.pose(**p)

    def run(self, t, ctx):
        T = place(0.0, -1.62, 0)
        pose = self.crew.run_pose(t, [local(T, (0.16, -1.08, 0.98)), local(T, (-0.16, -1.08, 0.98))], lean=-22)
        eng, _ = self.engine(wheel=-360 * t * 2)
        return M.Mesh.concat([eng, self.crew.mesh(pose, T)])
