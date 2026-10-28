"""MDJ's Siege Crossbow (catalog/mdj_ranged.py): a great crossbow on a low
four-wheeled cart, its wide bow braced by a wooden frame, worked by one
crewman behind it. See models/siege.py for the crewed-engine base.

Attack: the crewman pulls the trigger, the string snaps forward and the bolt
flies; he winds the winch to span the bow again and a new bolt is laid on.
Death: a hit smashes it, the bow and the frame fall in and the crewman dies.

Catalog parameters as for siege.py ('crew' takes footman parameters, so its
'gear' works too), and 'fire' (the shot's normalized time in ATTACK1).
"""
import math

import numpy as np

import mesh as M
import rig
from models import archetype
from models import humanoid as HU
from models import foot_ancient as FA
from models import vparts as VP
from models import vfx as FX
from models import siege as SG
from models.siege import Engine, place, local
from models.vehicles import _orient_y

PAINT = {'wood': '#b08a5c', 'wood_dark': '#6e5038', 'frame': '#c49a68', 'panel': '#e8e8e8', 'band': '#e8e8e8',
         'wheel': '#8a6440', 'hub': '#4a3a2c', 'iron': ('#5a5856', 0.5, 22), 'string': '#e8e0c8',
         'bolt': '#d0b888', 'tip': ('#9a9a9e', 0.6, 30)}


class FootCrew(SG.Crew):
    """A crewman built as a footman (so it takes footman gear)."""

    def __init__(self, params):
        self.h = FA.Footman('crew', dict(params))
        self.mats = self.h.mats
        self.base = dict(self.h.base)


@archetype('siege_crossbow')
class SiegeCrossbow(Engine):
    def __init__(self, unit, params):
        p = dict(params)
        crew = p.pop('crew', None)
        super().__init__(unit, p, {}, metal=())
        self.p = params
        self.crew = FootCrew(crew)
        self.mats = self.crew.mats
        self.paint(PAINT, metal=('iron', 'tip'))
        m = self.mats
        W, L, zb = 0.50, 1.15, 0.42          # half width, half length, chassis height
        self.zr = zr = 0.92                  # the rail (stock) height
        P_ = []
        for sx in (-1, 1):
            P_.append(VP.bar((sx * W, -L, zb), (sx * W, L, zb), 0.13, 0.15, m['wood']))
            # civ-colored cloth panels on the chassis sides
            P_.append(M.box(0.03, 0.55, 0.13, m['panel'], center=(sx * (W + 0.08), -0.25, zb)))
            # posts up to the rail
            for y in (-0.85, 0.75):
                P_.append(VP.bar((sx * W, y, zb), (0.0, y, zr - 0.05), 0.09, 0.09, m['wood']))
        for y in (-L + 0.08, -0.3, 0.45, L - 0.08):
            P_.append(VP.bar((-W, y, zb), (W, y, zb), 0.11, 0.11, m['wood']))
        # the rail along the middle, its civ-colored bindings
        P_.append(VP.bar((0, -L - 0.05, zr), (0, L + 0.25, zr), 0.16, 0.13, m['wood_dark']))
        for y in (-0.85, 0.75):
            P_.append(M.box(0.2, 0.12, 0.17, m['band'], center=(0, y, zr)))
        # the bracing frame of the bow: wide wings out to the bow tips
        self.span = span = 1.25
        yb, yt = L + 0.05, 0.55              # the bow's middle and its tips (y)
        self.bow_mid, self.tips_y = yb, yt
        for sx in (-1, 1):
            a = (sx * W, -0.3, zb + 0.06)
            b = (sx * (span - 0.08), yt - 0.05, zr - 0.15)
            c = (sx * W, L, zb + 0.06)
            P_.append(VP.bar(a, b, 0.08, 0.08, m['frame']))
            P_.append(VP.bar(b, c, 0.08, 0.08, m['frame']))
            # more rails above, like a fence round the wings
            for dz in (0.16, 0.30):
                a2 = (a[0], a[1] + 0.05, a[2] + dz)
                c2 = (c[0], c[1] - 0.05, c[2] + dz)
                b2 = (b[0] * (1 - 0.05 * dz / 0.16), b[1], b[2] + dz * 0.45)
                P_.append(VP.bar(a2, b2, 0.06, 0.06, m['frame']))
                P_.append(VP.bar(b2, c2, 0.06, 0.06, m['frame']))
            P_.append(VP.bar(a, (a[0], a[1] + 0.05, a[2] + 0.32), 0.07, 0.07, m['frame']))
            P_.append(VP.bar(c, (c[0], c[1] - 0.05, c[2] + 0.32), 0.07, 0.07, m['frame']))
            P_.append(VP.bar(b, (sx * (span - 0.08), yt - 0.05, zr - 0.02), 0.07, 0.07, m['frame']))
            P_.append(VP.bar((sx * W, 0.45, zb + 0.06), (sx * (span - 0.25), 0.40, zr - 0.20), 0.07, 0.07, m['frame']))
        # the winch at the rear, its handles
        P_.append(VP.axle_x(0.07, 2 * W + 0.1, m['wood_dark'], center=(0, -L + 0.1, zr - 0.18)))
        for sx in (-1, 1):
            P_.append(VP.bar((sx * (W + 0.05), -L + 0.1, zr - 0.18), (sx * (W + 0.05), -L - 0.1, zr - 0.02), 0.05, 0.05,
                             m['iron']))
        self.frame_mesh = M.Mesh.concat(P_)
        # the bow: two thick limbs from the front of the rail back to the tips
        bow = []
        n = 6
        self.tips = []
        for sx in (-1, 1):
            pts = [(sx * span * i / (n - 1), yb - (yb - yt) * (i / (n - 1)) ** 1.6, zr + 0.02) for i in range(n)]
            for i, (a, b) in enumerate(zip(pts[:-1], pts[1:])):
                w = 0.13 - 0.07 * i / (n - 1)
                bow.append(VP.bar(a, b, w, w * 0.8, m['wood_dark']))
            self.tips.append(np.array(pts[-1], np.float32))
            bow.append(M.box(0.12, 0.1, 0.16, m['band'], center=(sx * 0.16, yb - 0.01, zr + 0.02)))
        bow.append(M.box(0.24, 0.2, 0.2, m['iron'], center=(0, yb, zr + 0.02)))
        self.bow = M.Mesh.concat(bow)
        self.nut = np.array([0, -0.75, zr + 0.08], np.float32)
        self.wheels_at = [(sx * (W + 0.1), y, 0.26) for sx in (-1, 1) for y in (-0.75, 0.75)]
        self.crew_T = place(0.0, -L - 0.55, 0.0, 0)
        self.autofit()

    # -- parts
    def wheels(self, ang=0.0):
        m = self.mats
        return M.Mesh.concat([VP.disc_wheel(0.26, 0.12, m['wheel'], m['hub'], angle=ang, center=c, bolts=4)
                              for c in self.wheels_at])

    def string(self, span):
        m = self.mats
        loose = np.array([0, self.tips_y + 0.05, self.zr + 0.06], np.float32)
        mid = self.nut * span + loose * (1 - span)
        return M.Mesh.concat([VP.tube(tp + np.array([0, 0, 0.04], np.float32), mid, 0.014, 0.014, m['string'], seg=4)
                              for tp in self.tips])

    def bolt(self, at=None, d=None):
        m = self.mats
        b = M.Mesh.concat([VP.tube((0, 0, 0), (0, 1.25, 0), 0.03, 0.03, m['bolt'], seg=6),
                           VP.tube((0, 1.25, 0), (0, 1.45, 0), 0.06, 0.004, m['tip'], seg=6),
                           M.box(0.01, 0.16, 0.09, m['bolt'], center=(0, 0.1, 0.04))])
        if at is None:
            return b.transformed(M.translate(*(self.nut + np.array([0, 0.0, 0.04], np.float32))))
        return b.transformed(_orient_y(at, d))

    def engine(self, span=1.0, bolt=True, wheel=0.0):
        parts = [self.frame_mesh, self.bow, self.wheels(wheel), self.string(span)]
        if bolt:
            parts.append(self.bolt())
        return M.Mesh.concat(parts)

    def frame(self, action, f, n, ctx):
        out = super().frame(action, f, n, ctx)
        FA.finish_colors(self.crew.h, self.crew.h.p)
        return out

    # -- crew poses (engine space -> the crewman's)
    def at_winch(self, T=None, **kw):
        T = self.crew_T if T is None else T
        hr = local(T, (0.32, -1.18, self.zr - 0.08))
        hl = local(T, (-0.32, -1.18, self.zr - 0.08))
        kw.setdefault('spine', (-10, 0, 0))
        kw.setdefault('head', (8, 0, 0))
        return self.crew.pose(hand_r=hr, hand_l=hl, elbow_r=(0.7, -0.6, -0.3), elbow_l=(-0.7, -0.6, -0.3), **kw)

    def crank(self, t):
        c = np.array([0.0, -1.05, self.zr - 0.18], np.float32)
        a = 2 * math.pi * t
        T = self.crew_T
        hr = local(T, c + np.array([0.6, 0.18 * math.cos(a) - 0.12, 0.18 * math.sin(a)], np.float32))
        hl = local(T, c + np.array([-0.6, 0.18 * math.cos(a + math.pi) - 0.12, 0.18 * math.sin(a + math.pi)], np.float32))
        hr[0], hl[0] = 0.40, -0.40
        return self.crew.pose(hand_r=hr, hand_l=hl, spine=(-16, 0, 0), chest=(-8, 0, 0), head=(16, 0, 0),
                              root_pos=(0, 0.02, -0.10 + 0.02 * math.sin(a)),
                              foot_r=(0.2, 0.05, HU.ANKLE_H), foot_l=(-0.2, -0.25, HU.ANKLE_H),
                              elbow_r=(0.7, -0.6, -0.3), elbow_l=(-0.7, -0.6, -0.3))

    # -- actions
    def idle(self, t, ctx):
        s = math.sin(2 * math.pi * t)
        pose = self.at_winch(head=(6, 0, 6 * s), root_pos=(0, 0, -0.02 + 0.008 * s))
        return M.Mesh.concat([self.engine(), self.crew.mesh(pose, self.crew_T)])

    def run(self, t, ctx):
        T = self.crew_T @ M.translate(0, 0.08, 0)
        pose = self.crew.run_pose(t, [local(T, (0.32, -1.2, self.zr - 0.08)), local(T, (-0.32, -1.2, self.zr - 0.08))],
                                  lean=-12)
        bob = 0.01 * math.sin(4 * math.pi * t)
        return M.Mesh.concat([self.engine(wheel=-360 * t).transformed(M.translate(0, 0, bob)), self.crew.mesh(pose, T)])

    def attack(self, t, ctx):
        tf = float(self.p.get('fire', 0.1))
        fx = []
        if t < tf:
            k = t / max(tf, 1e-3)
            pose = self.at_winch(spine=(-14 * k, 0, 0), root_pos=(0, 0.03 * k, -0.04 * k))
            span, bolt = 1.0, True
        else:
            u = t - tf
            wind0, wind1 = tf + 0.12, 0.88
            if t < wind0:
                pose = self.at_winch(spine=(4, 0, 0), head=(-6, 0, 0), root_pos=(0, -0.04, 0))
                span = 0.0
            else:
                w = min(1.0, (t - wind0) / (wind1 - wind0))
                pose = self.crank(w * 3.0)
                span = rig.smoothstep(w)
            bolt = t >= 0.92
            if u < 0.25:
                d = np.array([0, 1.0, 0.03], np.float32)
                d /= np.linalg.norm(d)
                p0 = self.nut + np.array([0, 0.9, 0.04], np.float32)
                fx.append(self.bolt(p0 + d * (u * 22.0), d))
        eng = self.engine(span=span, bolt=bolt)
        return M.Mesh.concat([eng, self.crew.mesh(pose, self.crew_T)] + fx)

    def death(self, t, ctx):
        m = self.mats
        hit = 0.08
        u = max(0.0, t - hit)
        k = rig.smoothstep(min(1.0, u / 0.45))
        parts = [self.frame_mesh.transformed(M.translate(0, 0, -0.18 * k) @ M.rot_y(6 * k) @ M.rot_x(-3 * k)),
                 self.wheels(0).transformed(M.translate(0.25 * k, 0, -0.05 * k) @ M.rot_y(-15 * k))]
        # the bow drops off the front, its string broken
        parts.append(self.bow.transformed(M.translate(0, 0.25 * k, -(self.zr - 0.08) * k) @
                                          M.translate(0, self.bow_mid, self.zr) @ M.rot_x(-10 * k) @
                                          M.translate(0, -self.bow_mid, -self.zr)))
        if k < 0.05:
            parts.append(self.string(1.0))
            parts.append(self.bolt())
        crew = self.crew.anim('DEATH', min(1.0, t * 1.15), self.crew_T, n=15)
        fx = []
        if t >= hit:
            # a cloud of dust and splinters (no fire: it's smashed, not burnt)
            a = u / 0.6
            if a < 1.0:
                rng = np.random.default_rng(5)
                for i in range(6):
                    off = rng.uniform(-0.5, 0.5, 3) * np.array([1.2, 1.4, 0.4]) + np.array([0, 0, 0.9 + 0.5 * a])
                    r = (0.25 + 0.25 * rng.uniform()) * (0.5 + a) * (1 - a ** 2)
                    if r > 0.03:
                        fx.append(FX.puff(m, off, r, 'smoke_light' if i % 2 else 'dust', seed=i))
            fx.append(FX.debris(m, (0, 0.2, 0.9), 0.7, min(u / 0.7, 1.0), seed=8, n=8, names=('wood', 'wood_dark')))
        mesh = M.Mesh.concat(parts)
        if u > 0.2:
            mesh = VP.recolor(mesh, {m['wood']: m['wood_dark']})
        return M.Mesh.concat([mesh, crew] + fx)

    def fortify(self, t, ctx):
        k = rig.smoothstep(min(1.0, t / 0.6))
        T = self.crew_T @ M.translate(0.35, 0.1, 0)
        pose = self.crew.pose(root_pos=(0, 0.05, -0.36 * k), spine=(-15 * k, 0, 0), hand_r=(0.3, 0.3, 0.85 - 0.25 * k),
                              hand_l=(-0.3, 0.3, 0.85 - 0.25 * k), foot_r=(0.2, 0.25, HU.ANKLE_H),
                              foot_l=(-0.2, -0.3, HU.ANKLE_H))
        return M.Mesh.concat([self.engine(), self.crew.mesh(pose, T)])

    def fidget(self, t, ctx):
        s = math.sin(2 * math.pi * t)
        pose = self.at_winch(head=(8, 0, 28 * s), chest=(0, 0, 8 * s))
        return M.Mesh.concat([self.engine(), self.crew.mesh(pose, self.crew_T)])

    def victory(self, t, ctx):
        T = self.crew_T @ M.translate(0.75, 0.1, 0) @ M.rot_z(-25)
        return M.Mesh.concat([self.engine(), self.crew.anim('VICTORY', t, T)])
