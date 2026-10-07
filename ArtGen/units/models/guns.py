"""Gunpowder engines: the Hwacha (a Korean rocket-arrow cart), the Cannon
(a Napoleonic field gun with its gunner), Artillery (a WWII field gun with
split trails) and Flak (a quad 20 mm anti-aircraft gun with its gunner).
See models/siege.py for the crew and engine base."""
import math

import numpy as np

import mesh as M
import rig
from models import archetype
from models import humanoid as HU
from models import vparts as VP
from models import vfx as FX
from models.siege import Engine, place, local
from models.vehicles import _orient_y


def _key(t, keys):
    return float(np.interp(t, [k[0] for k in keys], [k[1] for k in keys]))


# ------------------------------------------------------------ hwacha

HWACHA_PAINT = {'wood': '#bca074', 'wood_dark': '#76593a', 'rail': '#e8e8e8', 'wheel': '#a88a5a', 'box_trim': '#76593a',
                'iron': ('#5a5856', 0.5, 22), 'arrow': '#e8dcb0', 'tip': ('#9a9a9e', 0.6, 30),
                'fletch': '#f0ece0', 'hole': '#3a2a1a'}


@archetype('hwacha')
class Hwacha(Engine):
    """A two-wheeled cart with a launcher box of rocket arrows tilted up
    over its axle and a civ-colored frame. Attack: the crewman lights the
    fuse, the arrows streak off in volleys with smoke."""

    def __init__(self, unit, params):
        super().__init__(unit, params, HWACHA_PAINT, metal=('iron', 'tip'))
        m = self.mats
        P_ = []
        W = 1.05
        for sx in (-1, 1):
            P_.append(VP.bar((sx * W / 2, -1.55, 0.95), (sx * W / 2, 1.55, 0.62), 0.12, 0.14, m['rail']))
            P_.append(VP.bar((sx * W / 2, 0.55, 0.72), (sx * W / 2, 1.55, 0.62), 0.1, 0.22, m['rail']))
        for y in (-0.4, 0.5, 1.0, 1.5):
            z = 0.95 - (y + 1.55) / 3.1 * 0.33
            P_.append(VP.bar((-W / 2, y, z), (W / 2, y, z), 0.1, 0.1, m['box_trim']))
        # the tray of spare arrows at the front
        for i in range(7):
            x = -0.36 + i * 0.12
            P_.append(VP.tube((x, 0.45, 0.8), (x, 1.5, 0.68), 0.025, 0.025, m['arrow'], seg=5))
        P_.append(VP.axle_x(0.07, W + 0.4, m['iron'], center=(0, 0.0, 0.62)))
        # the launcher box (its own transform, tilted back)
        box = [M.box(1.05, 0.55, 0.78, m['wood'], center=(0, 0, 0.39), bevel=0.02)]
        for sx in (-1, 1):
            box.append(M.box(0.08, 0.6, 0.84, m['box_trim'], center=(sx * 0.54, 0, 0.4)))
        box.append(M.box(1.15, 0.6, 0.07, m['box_trim'], center=(0, 0, 0.8)))
        self.holes = []
        for i in range(6):
            for j in range(4):
                x, z = -0.4 + i * 0.16, 0.12 + j * 0.18
                box.append(M.box(0.08, 0.02, 0.08, m['hole'], center=(x, 0.28, z)))
                self.holes.append((x, 0.3, z))
        self.box = M.Mesh.concat(box)
        self.box_T = M.translate(0, -0.15, 0.95) @ M.rot_x(32)
        self.arrows = M.Mesh.concat([self.arrow().transformed(M.translate(*h)) for h in self.holes[::2]])
        self.frame_mesh = M.Mesh.concat(P_)
        self.crew_T = place(0.0, -2.05, 0.0, 0)
        self.autofit()

    def arrow(self):
        m = self.mats
        return M.Mesh.concat([VP.tube((0, -0.35, 0), (0, 0.32, 0), 0.018, 0.018, m['arrow'], seg=5),
                              VP.tube((0, 0.32, 0), (0, 0.46, 0), 0.04, 0.003, m['tip'], seg=5)])

    def wheels(self, ang=0.0):
        m = self.mats
        return M.Mesh.concat([VP.spoked_wheel(0.62, 0.1, m, m['wheel'], m['wood_dark'], m['iron'], n_spokes=12,
                                              angle=ang, center=(sx * 0.78, 0.0, 0.62)) for sx in (-1, 1)])

    def engine(self, wheel=0.0, arrows=True, tilt=0.0):
        parts = [self.frame_mesh, self.wheels(wheel), self.box.transformed(self.box_T @ M.rot_x(tilt))]
        if arrows:
            parts.append(self.arrows.transformed(self.box_T @ M.rot_x(tilt)))
        return M.Mesh.concat(parts)

    def handle_pose(self, T, **kw):
        hr = local(T, (0.5, -1.45, 0.98))
        hl = local(T, (-0.5, -1.45, 0.98))
        return self.crew.pose(hand_r=hr, hand_l=hl, elbow_r=(0.7, -0.6, -0.3), elbow_l=(-0.7, -0.6, -0.3), **kw)

    def idle(self, t, ctx):
        s = math.sin(2 * math.pi * t)
        pose = self.handle_pose(self.crew_T, head=(4, 0, 8 * s), root_pos=(0, 0, -0.01 + 0.008 * s))
        return M.Mesh.concat([self.engine(), self.crew.mesh(pose, self.crew_T)])

    def run(self, t, ctx):
        T = self.crew_T @ M.translate(0, 0.1, 0)
        pose = self.crew.run_pose(t, [local(T, (0.5, -1.45, 0.98)), local(T, (-0.5, -1.45, 0.98))], lean=-12)
        bob = 0.01 * math.sin(4 * math.pi * t)
        return M.Mesh.concat([self.engine(wheel=-360 * t).transformed(M.translate(0, 0, bob)),
                              self.crew.mesh(pose, T)])

    def attack(self, t, ctx):
        tf = self.fire_t(ctx, 0.15)
        m = self.mats
        T = self.crew_T @ M.translate(0.55, 0.45, 0) @ M.rot_z(-15)
        # crouch beside the box with the fuse, then lean away
        k = rig.smoothstep(min(1.0, t / max(tf, 0.05)))
        back = rig.smoothstep(min(1.0, max(0.0, (t - tf) / 0.15)))
        fuse = local(T, (0.15, -0.55, 1.05))
        pose = self.crew.pose(root_pos=(0, 0.05 - 0.15 * back, -0.32 * k), spine=(-20 * k + 10 * back, 0, 0),
                              hand_r=fuse * k + np.array([0.35, 0.2, 0.95], np.float32) * (1 - k) * 1.0
                              if back < 0.5 else np.array([0.3, 0.1, 1.2], np.float32),
                              hand_l=(-0.3, 0.2, 0.75 - 0.15 * k), foot_r=(0.2, 0.25, HU.ANKLE_H),
                              foot_l=(-0.2, -0.3, HU.ANKLE_H), head=(10 * k - 20 * back, 0, 0))
        fx = []
        n = len(self.holes)
        launched = 0
        B = self.box_T
        fwd = B[:3, :3] @ np.array([0, 1, 0], np.float32)
        for i, h in enumerate(self.holes[::2]):
            ti = tf + (i % 6) * 0.025 + (i // 6) * 0.01
            u = t - ti
            if u < 0:
                continue
            launched += 1
            if u < 0.35:
                p = (B @ np.array([*h, 1.0], np.float32))[:3] + fwd * (u * 30.0) + np.array([0, 0, -u * u * 4], np.float32)
                fx.append(self.arrow().transformed(_orient_y(p, fwd)))
                fx.append(FX.puff(m, p - fwd * 0.5, 0.09, 'fx_yellow', seed=i))
            if u < 0.5:
                p0 = (B @ np.array([*h, 1.0], np.float32))[:3]
                fx.append(FX.puff(m, p0 + fwd * (0.4 + u * 2.0) + np.array([0, 0, u * 0.6], np.float32),
                                  0.16 * (1 + 2 * u) * (1 - (u / 0.5) ** 2), 'smoke_light', seed=i + 20))
        if 0 <= t - tf < 0.08:
            fx.append(FX.puff(m, (B @ np.array([0, -0.4, 0.4, 1.0], np.float32))[:3], 0.3, 'fx_yellow', seed=2))
        if t < tf and t > tf * 0.5:
            fx.append(FX.puff(m, (T @ np.array([*fuse, 1.0], np.float32))[:3], 0.06, 'fx_yellow', seed=3))
        eng = M.Mesh.concat([self.frame_mesh, self.wheels(0), self.box.transformed(B)])
        if launched == 0:
            eng = eng + self.arrows.transformed(B)
        return M.Mesh.concat([eng, self.crew.mesh(pose, T)] + fx)

    def death(self, t, ctx):
        m = self.mats
        hit = 0.08
        u = max(0.0, t - hit)
        k = rig.smoothstep(min(1.0, u / 0.4))
        # the cart tips over backward onto its handles' end, the box breaks loose
        cart = M.Mesh.concat([self.frame_mesh, self.wheels(0)]).transformed(
            M.translate(0.5 * k, 0.3 * k, 0) @ M.rot_z(-20 * k) @ M.translate(0, 0, 0.62) @ M.rot_x(-14 * k) @ M.translate(0, 0, -0.62))
        box = self.box.transformed(M.translate(0.3 * k, 0.6 * k, -0.6 * k) @ self.box_T @ M.rot_x(-40 * k))
        T = self.crew_T @ M.translate(-0.9 * k, -0.6 * k, 0.6 * math.sin(math.pi * min(1.0, u / 0.5)))
        crew = self.crew.anim('DEATH', min(1.0, t * 1.2), T, n=15)
        fx = []
        if t >= hit:
            fx.append(FX.explosion(m, (0, 0.0, 1.1), 1.1, u / 0.5, seed=4, n=8, smoke_dark=0.2))
            fx.append(FX.debris(m, (0, 0.2, 1.2), 0.7, min(u / 0.7, 1.0), seed=6, n=8, names=('wood', 'arrow')))
        mesh = M.Mesh.concat([cart, box])
        if u > 0.2:
            mesh = VP.recolor(mesh, {m['wood']: m['wood_dark']})
        return M.Mesh.concat([mesh, crew] + fx)

    def fortify(self, t, ctx):
        k = rig.smoothstep(min(1.0, t / 0.6))
        T = self.crew_T @ M.translate(0.55, 0.3, 0)
        pose = self.crew.pose(root_pos=(0, 0.05, -0.38 * k), spine=(-15 * k, 0, 0), hand_r=(0.3, 0.3, 0.85 - 0.25 * k),
                              hand_l=(-0.3, 0.3, 0.85 - 0.25 * k), foot_r=(0.2, 0.25, HU.ANKLE_H),
                              foot_l=(-0.2, -0.3, HU.ANKLE_H))
        return M.Mesh.concat([self.engine(), self.crew.mesh(pose, T)])

    def fidget(self, t, ctx):
        s = math.sin(2 * math.pi * t)
        pose = self.handle_pose(self.crew_T, head=(8, 0, 25 * s), chest=(0, 0, 8 * s))
        return M.Mesh.concat([self.engine(), self.crew.mesh(pose, self.crew_T)])

    def victory(self, t, ctx):
        # a leap with the fists up (the original turns a somersault)
        T = self.crew_T @ M.translate(0.6, 0.0, 0.5 * math.sin(math.pi * min(1.0, t / 0.6)) if t < 0.6 else 0.0)
        return M.Mesh.concat([self.engine(), self.crew.anim('VICTORY', t, T)])


# ------------------------------------------------------------ cannon

CANNON_PAINT = {'iron': ('#56585c', 0.55, 26), 'carriage': '#3a4048', 'carriage_dark': '#262a30',
                'wheel': '#e8e8e8', 'felloe': '#4e3c2e', 'rim': ('#4a3a30', 0.2, 12), 'hub': ('#2a2a2c', 0.4, 20),
                'brass': ('#b08a40', 0.7, 28)}


@archetype('cannon')
class Cannon(Engine):
    """A Napoleonic field gun on a two-wheeled carriage with civ-colored
    wheels and its gunner with a rammer. Attack: a big flash, recoil and a
    cloud of white powder smoke."""

    def __init__(self, unit, params):
        super().__init__(unit, params, CANNON_PAINT, metal=('iron', 'hub', 'brass'))
        m = self.mats
        P_ = []
        for sx in (-1, 1):
            P_.append(VP.side_prism([(0.55, 0.62), (0.3, 1.0), (-0.3, 1.02), (-2.0, 0.22), (-2.1, 0.0), (-1.7, 0.0)],
                                    0.12, m['carriage'], x0=sx * 0.3))
        P_.append(VP.bar((-0.3, -1.95, 0.12), (0.3, -1.95, 0.12), 0.25, 0.2, m['carriage_dark']))
        P_.append(VP.axle_x(0.07, 1.5, m['hub'], center=(0, 0.0, 0.7)))
        self.frame_mesh = M.Mesh.concat(P_)
        b = M.lathe([(0.0, 0.22), (0.25, 0.24), (0.3, 0.2), (1.2, 0.17), (2.1, 0.13), (2.16, 0.16), (2.24, 0.15),
                     (2.24, 0.07), (2.0, 0.07)], m['iron'], seg=14)
        cas = VP.icosphere(0.12, m['iron'], (0, 0, -0.05), sub=1)
        self.barrel = M.Mesh.concat([b, cas]).transformed(M.rot_x(-90) @ M.translate(0, 0, -0.75))
        self.barrel = M.Mesh.concat([b.transformed(M.translate(0, 0, -0.75)), cas.transformed(M.translate(0, 0, -0.78))]
                                    ).transformed(M.rot_x(-90))
        self.trun = np.array([0, 0.0, 1.1], np.float32)
        self.muzzle = np.array([0, 1.5, 0], np.float32)
        self.crew_T = place(0.55, -1.75, 0.0, 15)
        self.autofit()

    def wheels(self, ang=0.0):
        m = self.mats
        return M.Mesh.concat([VP.spoked_wheel(0.72, 0.1, m, m['felloe'], m['wheel'], m['hub'], n_spokes=12, angle=ang,
                                              center=(sx * 0.62, 0, 0.72), tire=m['rim']) for sx in (-1, 1)])

    def engine(self, wheel=0.0, recoil=0.0, elev=4.0, rock=0.0):
        G = M.translate(*self.trun) @ M.rot_x(elev)
        parts = [self.frame_mesh, self.wheels(wheel), self.barrel.transformed(G)]
        return M.Mesh.concat(parts).transformed(M.translate(0, -recoil, 0) @ M.translate(0, -2.0, 0) @ M.rot_x(rock)
                                                @ M.translate(0, 2.0, 0)), G

    def stand(self, s=0.0, **kw):
        p = dict(hand_r=(0.32, 0.18, 1.05), w_main=(-6, 0, 0), elbow_r=(0.6, -0.8, -0.3),
                 hand_l=(-0.38, 0.05, 0.86), head=(4, 0, 8 * s), root_pos=(0, 0, 0.005 * s))
        p.update(kw)
        return self.crew.pose(**p)

    def idle(self, t, ctx):
        s = math.sin(2 * math.pi * t)
        eng, _ = self.engine()
        return M.Mesh.concat([eng, self.crew.mesh(self.stand(s), self.crew_T, weapon='staff')])

    def run(self, t, ctx):
        T = place(0.0, -2.55, 0)
        pose = self.crew.run_pose(t, [local(T, (0.25, -2.1, 0.55)), local(T, (-0.25, -2.1, 0.55))], lean=-22)
        eng, _ = self.engine(wheel=-360 * t)
        return M.Mesh.concat([eng, self.crew.mesh(pose, T)])

    def attack(self, t, ctx):
        tf = self.fire_t(ctx, 0.07)
        m = self.mats
        dt = t - tf
        recoil = 0.0
        if dt >= 0:
            recoil = 0.55 * (1 - math.exp(-dt * 30)) * (1 - rig.smoothstep(min(1.0, max(0.0, (dt - 0.15) / 0.6))))
        eng, G = self.engine(recoil=recoil, rock=-3 * math.exp(-max(dt, 0) * 12) if dt >= 0 else 0)
        fx = []
        mz = (M.translate(0, -recoil, 0) @ G @ np.array([*self.muzzle, 1.0], np.float32))[:3]
        fwd = G[:3, :3] @ np.array([0, 1, 0], np.float32)
        if 0 <= dt < 0.09:
            fx.append(FX.muzzle_flash(m, mz, fwd, 1.5, seed=1, prongs=6))
            eng = VP.recolor(eng, self.lit())
        if dt >= 0:
            fx.append(FX.smoke_puffs(m, mz + fwd * 0.3, fwd, 1.3, min(dt / 0.45, 1.0), seed=2, n=6, color=0.0))
        # the gunner flinches away and covers his ears, then steps back up
        k = math.exp(-max(dt, 0) * 5) if dt >= 0 else 0
        pose = self.stand(0, root_pos=(0, -0.08 * k, -0.15 * k), spine=(-12 * k, 0, -10 * k), head=(16 * k, 0, -25 * k))
        return M.Mesh.concat([eng, self.crew.mesh(pose, self.crew_T, weapon='staff')] + fx)

    def death(self, t, ctx):
        m = self.mats
        hit = 0.06
        u = max(0.0, t - hit)
        k = rig.smoothstep(min(1.0, u / 0.35))
        eng, _ = self.engine(elev=4 - 18 * k)
        eng = eng.transformed(M.translate(0, -0.3 * k, -0.2 * k) @ M.rot_y(-12 * k))
        if u > 0.15:
            eng = VP.recolor(eng, self.charred())
        T = self.crew_T @ M.translate(0.4 * k, -0.5 * k, 0)
        crew = self.crew.anim('DEATH', min(1.0, t * 1.1), T, n=19, weapon='staff')
        fx = []
        if t >= hit:
            fx.append(FX.explosion(m, (0.0, 0.2, 1.0), 1.4, u / 0.55, seed=4, n=9, smoke_dark=0.05))
        if u > 0.3:
            fx.append(FX.smoke_column(m, (0, 0.0, 0.8), 0.6, u, seed=4, n=4, color=0.35, height=2.4))
        return M.Mesh.concat([eng, crew] + fx)

    def fortify(self, t, ctx):
        k = rig.smoothstep(min(1.0, t / 0.6))
        pose = self.stand(0, root_pos=(0, 0.05, -0.4 * k), spine=(-12 * k, 0, 0), foot_r=(0.2, 0.25, HU.ANKLE_H),
                          foot_l=(-0.2, -0.3, HU.ANKLE_H), hand_r=(0.3, 0.28, 1.0 - 0.3 * k))
        eng, _ = self.engine()
        return M.Mesh.concat([eng, self.crew.mesh(pose, self.crew_T, weapon='staff')])

    def fidget(self, t, ctx):
        s = math.sin(2 * math.pi * t)
        pose = self.stand(0, head=(8, 0, 30 * s), chest=(0, 0, 10 * s), hand_r=(0.32, 0.18 + 0.05 * s, 1.05))
        eng, _ = self.engine()
        return M.Mesh.concat([eng, self.crew.mesh(pose, self.crew_T, weapon='staff')])

    def victory(self, t, ctx):
        eng, _ = self.engine()
        return M.Mesh.concat([eng, self.crew.anim('VICTORY', t, self.crew_T, weapon='staff')])


# ------------------------------------------------------------ WWII field gun

FIELDGUN_PAINT = {'paint': '#6c7246', 'paint_dark': '#4a4f32', 'tire': '#262624', 'rim': ('#4a4e38', 0.3, 16),
                  'steel': ('#3e403e', 0.5, 24), 'spade': '#e8e8e8', 'shield_top': '#e8e8e8'}


@archetype('fieldgun')
class FieldGun(Engine):
    """A WWII field gun: long barrel with a muzzle brake over a sloped
    shield, two rubber-tired wheels, split trails ending in civ-colored
    spades. No crew (as in Civ3). Attack: recoil along the cradle, flash,
    blast smoke and dust; death: blown apart, a wheel rolling off."""

    loop_actions = ('RUN',)

    def __init__(self, unit, params):
        super().__init__(unit, params, FIELDGUN_PAINT, metal=('steel',))
        m = self.mats
        self.trun = np.array([0, 0.15, 0.95], np.float32)
        sh = [(-1.1, 0.25), (1.1, 0.25), (1.05, 1.6), (0.6, 1.74), (-0.6, 1.74), (-1.05, 1.6)]
        shield = VP.sweep([(0.32, sh), (0.38, sh)], m['paint'])
        top = M.box(1.25, 0.09, 0.12, m['shield_top'], center=(0, 0.35, 1.76))
        notch = M.box(0.3, 0.1, 0.3, m['paint_dark'], center=(0, 0.36, 1.0))
        cradle = M.box(0.32, 1.4, 0.3, m['paint_dark'], center=(0, -0.1, 0.0), bevel=0.04)
        self.static = M.Mesh.concat([shield.transformed(M.rot_x(-10)), top.transformed(M.rot_x(-10)), notch,
                                     VP.axle_x(0.08, 1.7, m['steel'], center=(0, 0.1, 0.55)),
                                     M.box(0.5, 0.6, 0.35, m['paint'], center=(0, 0.05, 0.7), bevel=0.04)])
        barrel = M.Mesh.concat([VP.tube((0, -0.5, 0), (0, 2.45, 0), 0.12, 0.095, m['paint'], seg=10),
                                VP.tube((0, 2.35, 0), (0, 2.7, 0), 0.15, 0.15, m['steel'], seg=8),
                                M.box(0.4, 0.14, 0.2, m['steel'], center=(0, 2.62, 0)),
                                VP.tube((0, -0.6, 0), (0, -0.2, 0), 0.13, 0.13, m['paint_dark'], seg=10)])
        self.cradle, self.barrel = cradle, barrel
        self.muzzle = np.array([0, 2.8, 0], np.float32)
        self.autofit()

    def trails(self, spread):
        m = self.mats
        out = []
        for sx in (-1, 1):
            a = math.radians(spread)
            end = np.array([sx * 2.1 * math.sin(a), -2.1 * math.cos(a), 0.12], np.float32)
            st = np.array([sx * 0.2, -0.1, 0.5], np.float32)
            out.append(VP.bar(st, end, 0.14, 0.16, m['paint']))
            out.append(M.box(0.45, 0.08, 0.32, m['spade'], center=(end[0], end[1] - 0.05, 0.16)))
            out.append(VP.bar(end + np.array([0, 0.25, 0.05]), end + np.array([0, -0.15, 0.05]), 0.18, 0.06, m['spade']))
        return M.Mesh.concat(out)

    def wheels(self, ang=0.0):
        m = self.mats
        return M.Mesh.concat([VP.disc_wheel(0.55, 0.24, m['rim'], m['steel'], angle=ang, center=(sx * 0.85, 0.1, 0.55),
                                            tire=m['tire']) for sx in (-1, 1)])

    def engine(self, wheel=0.0, recoil=0.0, elev=6.0, spread=16.0, yaw=0.0):
        G = M.translate(*self.trun) @ M.rot_x(elev)
        parts = [self.static, self.wheels(wheel), self.trails(spread), self.cradle.transformed(G),
                 self.barrel.transformed(G @ M.translate(0, -recoil, 0))]
        return M.Mesh.concat(parts), G

    def idle(self, t, ctx):
        return self.engine()[0]

    def run(self, t, ctx):
        # towed: trails closed and lifted, wheels turning
        e, _ = self.engine(wheel=-360 * t, spread=3.0, elev=2.0)
        return e.transformed(M.translate(0, 0, 0.01 * math.sin(4 * math.pi * t)))

    def attack(self, t, ctx):
        tf = self.fire_t(ctx, 0.07)
        m = self.mats
        dt = t - tf
        rec = 0.0
        if dt >= 0:
            rec = 0.6 * (1 - math.exp(-dt * 40)) * (1 - rig.smoothstep(min(1.0, max(0.0, (dt - 0.1) / 0.4))))
        rock = -2.5 * math.exp(-max(dt, 0) * 10) * (dt >= 0)
        e, G = self.engine(recoil=rec)
        e = e.transformed(M.translate(0, -2.6, 0) @ M.rot_x(rock) @ M.translate(0, 2.6, 0))
        fx = []
        mz = (G @ np.array([*self.muzzle, 1.0], np.float32))[:3]
        fwd = G[:3, :3] @ np.array([0, 1, 0], np.float32)
        if 0 <= dt < 0.09:
            fx.append(FX.muzzle_flash(m, mz, fwd, 1.6, seed=1, prongs=6))
            e = VP.recolor(e, self.lit())
        if dt >= 0:
            fx.append(FX.smoke_puffs(m, mz + fwd * 0.4, fwd, 1.2, min(dt / 0.5, 1.0), seed=2, n=5, color=0.35))
            # blast dust kicked up around the muzzle on the ground
            if dt < 0.4:
                for i, sx in enumerate((-1, 1)):
                    fx.append(FX.puff(m, mz * np.array([1, 1, 0]) + np.array([sx * (0.6 + dt * 2), 0.3, 0.25]),
                                      0.35 * (1 - dt / 0.4), 'dust', seed=7 + i))
        return M.Mesh.concat([e] + fx)

    def death(self, t, ctx):
        """Blown apart: fireball, the gun slumps and a wheel rolls away."""
        m = self.mats
        hit = 0.08
        u = max(0.0, t - hit)
        k = rig.smoothstep(min(1.0, u / 0.4))
        G = M.translate(*self.trun) @ M.rot_x(6 - 20 * k)
        roll = min(1.0, u / 0.9)
        wpos = np.array([0.8 + 2.4 * roll, 0.1 - 0.4 * roll, 0.48], np.float32)
        wl = VP.disc_wheel(0.48, 0.2, m['rim'], m['steel'], angle=-400 * roll, center=(0, 0, 0), tire=m['tire'])
        wl = wl.transformed(M.translate(*wpos) @ M.rot_z(-15 * roll) @ M.rot_y(min(1.0, max(0.0, (u - 0.6) / 0.3)) * 80))
        if roll > 0.95:
            wl = wl.transformed(M.translate(0, 0, -0.3))
        other = VP.disc_wheel(0.48, 0.2, m['rim'], m['steel'], center=(-0.8, 0.1, 0.48), tire=m['tire'])
        body = M.Mesh.concat([self.static, other, self.trails(16), self.cradle.transformed(G),
                              self.barrel.transformed(G)])
        body = body.transformed(M.translate(0.6 * k, 0, 0) @ M.rot_y(18 * k) @ M.translate(0, 0, -0.15 * k))
        if u > 0.12:
            body = VP.recolor(body, self.charred())
            wl = VP.recolor(wl, self.charred())
        fx = []
        if t >= hit:
            fx.append(FX.explosion(m, (0, 0.2, 0.9), 1.3, u / 0.5, seed=5, n=9))
            fx.append(FX.debris(m, (0, 0.2, 0.9), 0.7, min(u / 0.6, 1.0), seed=6, n=8))
        if u > 0.35:
            fx.append(FX.smoke_column(m, (0.2, 0.0, 0.7), 0.6, u, seed=8, n=4, color=0.85, height=2.4))
        return M.Mesh.concat([body, wl] + fx)

    def fortify(self, t, ctx):
        # dig in: the trails spread wide and the barrel drops level
        k = rig.smoothstep(min(1.0, t / 0.8))
        return self.engine(spread=16 + 10 * k, elev=6 - 4 * k)[0].transformed(M.translate(0, 0, -0.05 * k))

    def fidget(self, t, ctx):
        e = 6 + 8 * (1 - math.cos(2 * math.pi * t)) / 2
        return self.engine(elev=e)[0]


# ------------------------------------------------------------ flak

FLAK_PAINT = {'paint': '#5c6250', 'paint_dark': '#3c4034', 'steel': ('#2e302e', 0.5, 24),
              'hub': '#e8e8e8', 'tire': '#222220', 'shield': '#4e5444', 'mag': '#7a7e70', 'band': '#e8e8e8'}


@archetype('flak')
class Flak(Engine):
    """A quad 20 mm anti-aircraft gun on a cruciform platform with its
    seated gunner; the transport wheels stand off at the sides. Attack: the
    barrels elevate and fire alternating bursts with tracers into the sky;
    run: on its wheels, a soldier walking beside."""

    def __init__(self, unit, params):
        super().__init__(unit, params, FLAK_PAINT, metal=('steel',))
        m = self.mats
        P_ = []
        for a in (0, 90, 180, 270):
            R = M.rot_z(a)
            P_.append(VP.bar((0, 0, 0.3), (0, 1.45, 0.22), 0.22, 0.14, m['paint']).transformed(R))
            P_.append(M.cylinder(0.14, 0.17, 0.12, m['paint_dark'], seg=8, z0=0.0).transformed(R @ M.translate(0, 1.4, 0)))
        P_.append(M.cylinder(0.6, 0.6, 0.18, m['paint_dark'], seg=16, z0=0.3))
        # the transport frame: a triangular tow bar behind, between the wheels
        for sx in (-1, 1):
            P_.append(VP.bar((sx * 1.6, -0.4, 0.45), (0, -2.5, 0.35), 0.1, 0.1, m['paint']))
            P_.append(VP.bar((sx * 1.6, -0.4, 0.45), (sx * 0.6, -0.1, 0.35), 0.1, 0.1, m['paint']))
        P_.append(VP.bar((-0.9, -1.5, 0.4), (0.9, -1.5, 0.4), 0.08, 0.08, m['paint']))
        P_.append(VP.bar((0, -2.5, 0.35), (0, -2.85, 0.3), 0.12, 0.12, m['paint_dark']))
        self.base = M.Mesh.concat(P_)
        # the traversing mount: pedestal, seat, side shields
        T_ = [M.cylinder(0.55, 0.5, 0.3, m['paint'], seg=14, z0=0.45),
              M.box(0.5, 0.45, 0.1, m['paint_dark'], center=(0, -0.85, 1.05)),
              VP.bar((0, -0.3, 0.6), (0, -0.85, 1.02), 0.12, 0.12, m['paint_dark']),
              M.box(0.6, 0.12, 0.08, m['paint_dark'], center=(0, -0.2, 0.95))]
        for sx in (-1, 1):
            T_.append(VP.side_prism([(-0.4, 0.95), (0.55, 0.95), (0.45, 1.9), (-0.3, 1.85)], 0.06, m['shield'], x0=sx * 0.78))
            T_.append(M.box(0.08, 0.95, 0.12, m['band'], center=(sx * 0.81, 0.08, 1.9)))
        self.mount = M.Mesh.concat(T_)
        # the elevating cradle with four barrels (2 x 2) and magazines
        G_ = [M.box(1.3, 0.8, 0.5, m['paint_dark'], center=(0, 0.1, 0.0), bevel=0.04)]
        self.muzzles = []
        for sx in (-1, 1):
            for sz in (-1, 1):
                x, z = sx * 0.4, sz * 0.18 + 0.05
                G_.append(VP.tube((x, 0.0, z), (x, 2.3, z), 0.05, 0.045, m['steel'], seg=8))
                G_.append(VP.tube((x, 2.15, z), (x, 2.4, z), 0.07, 0.07, m['steel'], seg=8))
                G_.append(M.box(0.12, 0.28, 0.3, m['mag'], center=(x * 1.45, 0.1, z + 0.3 * (sz > 0))))
                self.muzzles.append((x, 2.45, z))
        self.guns = M.Mesh.concat(G_)
        self.trun = np.array([0, 0.25, 1.2], np.float32)
        self.wheels_off = [(sx * 1.75, -0.4, 0.5) for sx in (-1, 1)]
        self.crew_T = place(0.0, -0.85, 0.62, 0)
        self.autofit()

    def wheel(self, center, ang=0.0, yaw=0.0):
        m = self.mats
        return VP.disc_wheel(0.5, 0.26, m['hub'], m['steel'], angle=ang, center=(0, 0, 0), tire=m['tire']).transformed(
            M.translate(*center) @ M.rot_z(yaw))

    def gun(self, yaw=0.0, elev=35.0, recoil=(0, 0, 0, 0), wheels='side', wheel_ang=0.0):
        R = M.rot_z(yaw)
        G = R @ M.translate(*self.trun) @ M.rot_x(elev)
        parts = [self.base, self.mount.transformed(R), self.guns.transformed(G)]
        if wheels == 'side':
            parts += [self.wheel(c, yaw=90 * 0) for c in self.wheels_off]
        else:
            parts += [self.wheel((sx * 1.2, 0.0, 0.5), ang=wheel_ang) for sx in (-1, 1)]
        return M.Mesh.concat(parts), G, R

    def gunner(self, R, G, s=0.0, **kw):
        """The gunner on his seat, hands on the cradle's grips."""
        T = R @ self.crew_T
        hr = local(T, (G @ np.array([0.3, -0.45, -0.15, 1.0], np.float32))[:3])
        hl = local(T, (G @ np.array([-0.3, -0.45, -0.15, 1.0], np.float32))[:3])
        p = dict(root_pos=(0, 0.0, -0.45), spine=(6, 0, 0), head=(-6 + 3 * s, 0, 0),
                 foot_r=(0.2, 0.5, 0.3), foot_l=(-0.2, 0.5, 0.3), hand_r=hr, hand_l=hl,
                 elbow_r=(0.7, -0.5, -0.4), elbow_l=(-0.7, -0.5, -0.4))
        p.update(kw)
        return self.crew.mesh(self.crew.pose(**p), T)

    def idle(self, t, ctx):
        s = math.sin(2 * math.pi * t)
        e, G, R = self.gun(elev=35 + 1.5 * s)
        return M.Mesh.concat([e, self.gunner(R, G, s)])

    def run(self, t, ctx):
        e, G, R = self.gun(elev=75, wheels='under', wheel_ang=-360 * t)
        e = e.transformed(M.translate(0, 0, 0.2))
        T = place(1.9, -0.2, 0)
        pose = self.crew.run_pose(t, None, lean=-6)
        walker = self.crew.mesh(pose, T)
        return M.Mesh.concat([e, self.gunner(R, G).transformed(M.translate(0, 0, 0.2)), walker])

    def attack(self, t, ctx):
        tf = self.fire_t(ctx, 0.08)
        m = self.mats
        up = rig.smoothstep(min(1.0, t / max(tf, 0.05)))
        elev = 35 + 30 * up - 30 * rig.smoothstep(min(1.0, max(0.0, (t - 0.55) / 0.4)))
        e, G, R = self.gun(elev=elev)
        fx = []
        fwd = G[:3, :3] @ np.array([0, 1, 0], np.float32)
        shoot = tf <= t < 0.5
        if shoot:
            k = int((t - tf) / 0.035)
            for i, mz in enumerate(self.muzzles):
                if (i + k) % 2 == 0:
                    p = (G @ np.array([*mz, 1.0], np.float32))[:3]
                    fx.append(FX.muzzle_flash(m, p, fwd, 0.55, seed=i + k, prongs=4))
            # tracers streaking up
            for j in range(3):
                d = ((t - tf) * 3 + j / 3) % 1.0
                mz = self.muzzles[(j + k) % 4]
                p = (G @ np.array([*mz, 1.0], np.float32))[:3] + fwd * (1.0 + d * 9)
                fx.append(VP.tube(p, p + fwd * 0.9, 0.03, 0.03, m['fx_yellow'], seg=4))
            if (t - tf) < 0.06:
                e = VP.recolor(e, self.lit())
        if t > tf:
            fx.append(FX.smoke_puffs(m, (G @ np.array([0, 2.6, 0.05, 1.0], np.float32))[:3], fwd, 0.6,
                                     min((t - tf) / 0.7, 1.0), seed=3, n=4, color=0.1))
        return M.Mesh.concat([e, self.gunner(R, G)] + fx)

    def death(self, t, ctx):
        """A hit on the mount: blast, the gunner is thrown, the guns sag
        and the shields buckle askew."""
        m = self.mats
        hit = 0.1
        u = max(0.0, t - hit)
        k = rig.smoothstep(min(1.0, u / 0.4))
        e, G, R = self.gun(elev=35 - 30 * k, yaw=25 * k)
        e = e.transformed(M.translate(0, 0, 0) @ M.rot_y(6 * k))
        if u > 0.15:
            e = VP.recolor(e, self.charred())
        T = R @ self.crew_T @ M.translate(-0.8 * k, -0.9 * k, -0.42 * k + 0.9 * math.sin(math.pi * min(1.0, u / 0.45)))
        crew = self.crew.anim('DEATH', min(1.0, t * 1.15), T, n=15)
        fx = []
        if t >= hit:
            fx.append(FX.explosion(m, (0, -0.2, 1.3), 1.2, u / 0.5, seed=3, n=8))
            fx.append(FX.debris(m, (0, -0.2, 1.3), 0.6, min(u / 0.6, 1.0), seed=6, n=8))
        if u > 0.35:
            fx.append(FX.smoke_column(m, (0, 0, 1.0), 0.6, u, seed=8, n=4, color=0.8, height=2.4))
        return M.Mesh.concat([e, crew] + fx)

    def fortify(self, t, ctx):
        yaw = 30 * math.sin(2 * math.pi * t)
        e, G, R = self.gun(yaw=yaw, elev=35)
        return M.Mesh.concat([e, self.gunner(R, G)])

    def fidget(self, t, ctx):
        s = math.sin(2 * math.pi * t)
        e, G, R = self.gun(elev=35 + 10 * s)
        return M.Mesh.concat([e, self.gunner(R, G, s, head=(-10, 0, 25 * s))])

    def victory(self, t, ctx):
        e, G, R = self.gun(elev=35)
        k = math.sin(math.pi * t)
        mesh = self.gunner(R, G, hand_r=(0.3, 0.1, 1.55 + 0.35 * k), elbow_r=(0.8, -0.3, 0.2),
                           head=(-15 * k, 0, 0), spine=(10 * k, 0, 0))
        return M.Mesh.concat([e, mesh])
