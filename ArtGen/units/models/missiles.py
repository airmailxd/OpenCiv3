"""Missiles: the Cruise Missile (flies like an aircraft) and the ICBM and
Tactical Nuke (standing rockets that launch, fly, and explode in a
mushroom cloud).

Several of these flics are single-direction effects drawn at odd places
of the 240x240 unit box (the ICBM's trail and launch flics sit at its right
edge). Their content is placed where the original's is: per frame, from
the original's bounding box (models/vehicles.original_profile), converted
to world space with the unit's calibration. Things in the sky are 'lifted'
along the view direction (which moves only their ground shadow, out of the
cell), so they cast no stray shadow on the ground.
"""
import math

import numpy as np

import mesh as M
import rig
from models import archetype
from models import vparts as VP
from models import vfx as FX
from models import aircraft as AC
from models.vehicles import VehicleBase, original_profile, _orient_y, SIN_E, COS_E

LIFT = 400.0


def lift(mesh, amount=LIFT):
    """Moves a world-space mesh along the view direction: it stays put on
    screen but its ground shadow leaves the cell."""
    if mesh is None or not len(mesh):
        return mesh
    return mesh.transformed(M.translate(0, -amount * COS_E / SIN_E, amount))


NUKE_COLORS = {'nuke_cream': '#f6e6b4', 'nuke_peach': '#f4caa0', 'nuke_tan': '#c8a888', 'nuke_gray': '#8a8682', 'nuke_dark': '#5a5654'}


def add_nuke_mats(mats):
    for k, v in NUKE_COLORS.items():
        mats.add(k, v, spec=0.0, gloss=6)
    FX.GLOW.setdefault('nuke_peach', 0.45)
    FX.GLOW.setdefault('nuke_cream', 0.6)


# ------------------------------------------------------------ the cruise missile

def build_cruise(V):
    """A cruise missile: a slim white body with civ-colored bands, short
    pop-out wings, a cruciform tail and a belly intake."""
    m = V.mats
    L, r = 6.2, 0.32
    body = [AC.fuselage([(-L / 2, 0.22), (-L / 2 + 0.3, r), (L / 2 - 1.0, r), (L / 2 - 0.4, r * 0.8), (L / 2, 0.02)],
                        m['paint'], seg=14)]
    for y in (-0.6, 1.2):
        body.append(AC.fuselage([(y, r * 1.04), (y + 0.4, r * 1.04)], m['band'], seg=14))
    body.append(M.box(0.35, 0.9, 0.25, m['paint_dark'], center=(0, -L / 2 + 1.0, -r)))
    body.append(AC.fuselage([(-L / 2 - 0.1, 0.18), (-L / 2 + 0.05, 0.2)], m['steel'], seg=10))
    wings = []
    for sx in (-1, 1):
        wings.append(AC.wing([(0.2, 0.5), (1.9, 0.0), (1.9, -0.3), (0.2, -0.1)], 0.06, m['paint_dark'], z=0.0, side=sx))
    tail = []
    for a in (0, 90, 180, 270):
        tail.append(M.box(0.04, 0.55, 0.65, m['paint_dark'], center=(0, -L / 2 + 0.4, r + 0.25)).transformed(M.rot_y(a)))
    parts = {'body': M.Mesh.concat(body), 'wing_l': wings[1], 'wing_r': wings[0], 'tail': M.Mesh.concat(tail)}
    return dict(parts=parts, props=[], guns=[], pylons=[], length=L, exhaust=[(0, -L / 2 - 0.1, 0)])


AC.BUILDERS['cruise'] = build_cruise
AC.PAINTS['cruise'] = {'paint': '#d8d8d4', 'paint_dark': '#5a5c5e', 'band': '#e8e8e8', 'steel': ('#3a3a3a', 0.5, 24),
                       'glass': ('#3a4a58', 0.6, 30), 'bomb': '#555555'}


@archetype('cruise_missile')
class CruiseMissile(AC.Aircraft):
    """Flies like an aircraft (DEFAULT: cruising with a flickering jet;
    RUN: with a smoke trail; ATTACK1: noses over into a dive). DEATH, a
    single-direction flic, is the impact: the missile streaks down into a
    fireball that throws out smoke-trailed debris and leaves a column of
    smoke."""

    loop_actions = ('DEFAULT', 'RUN')

    def __init__(self, unit, params):
        # (not a copy: the framework reads the fitted 'scale' and 'anchor' back from this dict)
        params['model'] = 'cruise'
        super().__init__(unit, params)
        add_nuke_mats(self.mats)

    def jet(self, T, t, trail=0):
        m = self.mats
        ex = self.b['exhaust'][0]
        p = (T @ np.array([*ex, 1.0], np.float32))[:3]
        back = T[:3, :3] @ np.array([0, -1.0, 0], np.float32)
        out = [FX.plume(m, p, back, 0.7 + 0.15 * math.sin(t * 40), 0.16, t=t, seed=1, smoke=False)]
        for j in range(trail):
            k = (j + (t * trail) % 1.0) / trail
            out.append(FX.puff(m, p + back * (1.2 + k * 7.0), 0.22 + 0.4 * k, 'smoke_white' if k < 0.5 else 'smoke_light',
                               seed=j + 3))
        return M.Mesh.concat(out)

    def _frame(self, action, f, n, ctx):
        t = self.times(action, f, n)
        m = self.mats
        if action == 'DEFAULT':
            T = self.flight(bob=0.05 * math.sin(2 * math.pi * t))
            return FX.FxMesh(self.plane().transformed(T) + self.jet(T, t), m)
        if action in ('RUN', 'WALK'):
            T = self.flight(bob=0.04 * math.sin(2 * math.pi * t))
            return FX.FxMesh(self.plane().transformed(T) + self.jet(T, t, trail=6), m)
        if action.startswith('ATTACK'):
            pitch = -40 * rig.smoothstep(t)
            T = self.flight(pitch=pitch, dy=1.5 * t, dz=-1.2 * t * t)
            return FX.FxMesh(self.plane().transformed(T) + self.jet(T, t, trail=6), m)
        if action == 'DEATH':
            return self.impact(t, ctx)
        return FX.FxMesh(self.plane().transformed(self.flight()), m)

    def impact(self, t, ctx):
        m = self.mats
        ti = 0.12                      # the hit
        world = []
        size = 2.6
        # where the original's impact is: its last frame's smoke cloud
        b = original_profile(ctx.flic)['bbox'][0, -1] if ctx is not None else None
        if b is not None and b[2] > b[0]:
            g = self.to_world((b[0] + b[2]) / 2, b[3] - 4, 0.0)
            size = max(1.5, self.meters(b[2] - b[0]) * 0.32)
        else:
            g = np.zeros(3, np.float32)
        if t < ti:
            u = t / ti
            T = M.translate(0, 0, self.alt * 1.6 * (1 - u)) @ M.rot_x(-70)
            world.append(self.plane().transformed(T))
            world.append(self.jet(T, t, trail=4))
        else:
            age = (t - ti) / (1 - ti)
            world.append(FX.explosion(m, (0, 0, 0.6), size, min(1.0, age / 0.5) * 0.55, seed=3, n=10, smoke_dark=0.3))
            # the smoke cloud that takes over
            if age > 0.2:
                k = min(1.0, (age - 0.2) / 0.4)
                for i in range(7):
                    a = i * 2.3
                    p = (math.cos(a) * size * 0.5 * k, math.sin(a) * size * 0.35 * k, 0.8 + size * (0.4 + 0.25 * (i % 3)) * k)
                    r = size * (0.35 + 0.15 * (i % 2)) * k * (1 - max(0.0, age - 0.85) / 0.15 * 0.5)
                    world.append(FX.puff(m, p, r, 'smoke_dark' if i % 3 else 'smoke', seed=50 + i))
            # smoke-trailed debris arcing out
            if age < 0.6:
                for j in range(2):
                    sx = -1 if j == 0 else 1
                    for s in range(6):
                        u = age / 0.6 * (s + 1) / 6
                        p = (sx * u * size * 2.2, -u * size * 0.4, size * 2.6 * u - size * 2.2 * u * u + 0.4)
                        world.append(FX.puff(m, p, 0.18 + 0.12 * s / 6, 'smoke_dark', seed=70 + s + 7 * j))
        return FX.FxMesh(None, m, world=M.Mesh.concat(world).transformed(M.translate(*g)))


# ------------------------------------------------------------ the ICBM / tactical nuke

def build_rocket(mats, L, r, stages=3):
    """A ballistic missile standing on its tail along +z from z = 0:
    stages with civ-colored rings, a white nose cone, small fins."""
    parts = []
    z = 0.0
    seg_l = (L * 0.82) / stages
    for i in range(stages):
        rr = r * (1 - 0.08 * i)
        parts.append(M.cylinder(rr, rr, seg_l, mats['paint'], seg=14, z0=z))
        parts.append(M.cylinder(rr * 1.04, rr * 1.04, seg_l * 0.18, mats['band'], seg=14, z0=z + seg_l * 0.22))
        parts.append(M.cylinder(rr * 1.02, rr * 1.02, seg_l * 0.05, mats['paint_dark'], seg=14, z0=z + seg_l * 0.95))
        z += seg_l
    rn = r * (1 - 0.08 * (stages - 1))
    parts.append(M.lathe([(z, rn), (z + L * 0.1, rn * 0.75), (z + L * 0.17, rn * 0.25), (z + L * 0.18, 0.0)],
                         mats['nose'], seg=14))
    parts.append(M.cylinder(rn * 0.5, rn * 0.4, L * 0.03, mats['band'], seg=12, z0=z + L * 0.12))
    for a in (45, 135, 225, 315):
        parts.append(VP.side_prism([(0, 0), (0, L * 0.12), (-r * 0.2, L * 0.12), (-r * 1.1, 0.0)], 0.05,
                                   mats['paint_dark']).transformed(M.rot_z(a) @ M.translate(r, 0, 0) @ M.rot_z(90)))
    parts.append(M.cylinder(r * 0.6, r * 0.8, L * 0.03, mats['steel'], seg=12, z0=-L * 0.03))
    return M.Mesh.concat(parts)


ROCKET_PAINT = {'paint': '#d4d4d2', 'paint_dark': '#6a6c70', 'band': '#e8e8e8', 'nose': '#f2f2ee',
                'steel': ('#3a3a3c', 0.5, 24), 'pad': '#6a6862'}


@archetype('rocket')
class Rocket(VehicleBase):
    """ICBM / Tactical Nuke. DEFAULT: standing on its pad, venting a little
    vapor; CAPTURE (launch): exhaust smoke billows around the pad, then it
    climbs on a bright plume; RUN (trail): in flight on its plume, up (row 0)
    or nose down (row 1); DEATH (bomb): the nuclear blast, a white fireball
    rising into a mushroom cloud over a ground ring; VICTORY (death): it is
    shot down, a fiery burst."""

    loop_actions = ('DEFAULT', 'RUN')

    def __init__(self, unit, params):
        super().__init__(unit, params)
        self.paint(ROCKET_PAINT, metal=('steel',))
        add_nuke_mats(self.mats)
        self.L = params.get('length', 8.0)
        self.r = params.get('radius', 0.55)
        self.rocket = build_rocket(self.mats, self.L, self.r, params.get('stages', 3))
        self.autofit()

    # -- placing things where the original has them
    def screen_to_world(self, X, Y, lifted=True):
        """World point drawn at 240-space pixel (X, Y) (on the vertical
        plane through the ground point), lifted out of shadow range."""
        c = self.cal()
        k = c['S'] / c['ppm']
        p = np.array([(X - c['ax']) * k, 0.0, (c['ay'] - Y) * k / COS_E], np.float32)
        if lifted:
            p += np.array([0, -LIFT * COS_E / SIN_E, LIFT], np.float32)
        return p

    def bbox(self, ctx, f, d=0):
        p = original_profile(ctx.flic)
        return p['bbox'][min(d, ctx.flic.n_anims - 1), f]

    def union_bbox(self, ctx):
        b = original_profile(ctx.flic)['bbox'].reshape(-1, 4)
        b = b[(b[:, 2] > b[:, 0])]
        return np.array([b[:, 0].min(), b[:, 1].min(), b[:, 2].max(), b[:, 3].max()], np.float32)

    def frame(self, action, f, n, ctx):
        t = self.times(action, f, n)
        m = self.mats
        if action == 'DEFAULT' or ctx is None:
            vent = []
            if ctx is not None and n > 1:
                for i in range(3):
                    ph = (t + i / 3) % 1.0
                    vent.append(FX.puff(m, (self.r * 1.3 + ph * 0.6, -0.2, 0.2 + ph * 1.2), 0.15 + 0.2 * ph * (1 - ph) * 3,
                                        'smoke_white', seed=i))
            return FX.FxMesh(self.rocket + M.Mesh.concat(vent), m)
        if action == 'RUN':
            return FX.FxMesh(None, m, per_dir=lambda d: self.trail(ctx, f, d, t))
        if action == 'CAPTURE':
            return FX.FxMesh(None, m, world=self.launch(ctx, f, t))
        if action == 'DEATH':
            return FX.FxMesh(None, m, world=self.nuke(ctx, t))
        if action == 'VICTORY':
            return FX.FxMesh(None, m, world=self.shot_down(ctx, t))
        return FX.FxMesh(self.rocket, m)

    def in_flight(self, nose, down=False, plume_len=0.0, t=0.0):
        """The rocket with its nose at world point `nose` (pointing up, or
        down), with an exhaust plume of plume_len meters."""
        m = self.mats
        if down:
            T = M.translate(*nose) @ M.rot_x(180) @ M.translate(0, 0, -self.L * 1.18)
            T = M.translate(*nose) @ M.rot_x(180) @ M.translate(0, 0, -self.L * 1.0)
        else:
            T = M.translate(*nose) @ M.translate(0, 0, -self.L * 1.0)
        out = [self.rocket.transformed(T)]
        tail = (T @ np.array([0, 0, -self.L * 0.03, 1.0], np.float32))[:3]
        d = np.array([0, 0, 1.0 if down else -1.0], np.float32)
        if plume_len > 0:
            out.append(VP.tube(tail, tail + d * plume_len * 0.25, self.r * 0.85, self.r * 0.6, m['fx_white'], seg=10))
            out.append(VP.tube(tail + d * plume_len * 0.15, tail + d * plume_len * 0.55, self.r * 0.8, self.r * 1.1,
                               m['fx_yellow'], seg=10))
            n = max(10, int(plume_len / (self.r * 1.1)))
            rng = np.random.default_rng(int(t * 997) % 1000)
            for i in range(n):
                k = (i + 0.5) / n
                if k < 0.3:
                    continue
                c = tail + d * plume_len * k + np.array([rng.normal() * 0.15, 0, 0]) * self.r
                rad = self.r * (1.1 + 1.3 * k) * (0.9 + 0.2 * rng.random())
                name = 'fx_yellow' if k < 0.5 else ('nuke_cream' if k < 0.85 else 'smoke_white')
                out.append(FX.puff(m, c, rad, name, seed=i + int(t * 50)))
        return M.Mesh.concat(out)

    def trail(self, ctx, f, d, t):
        b = self.bbox(ctx, f, d)
        if b[2] <= b[0]:
            return None
        xc = (b[0] + b[2]) / 2
        c = self.cal()
        ppm1 = c['ppm'] / c['S']
        if d == 0:          # climbing: nose at the top, plume down to the bottom
            nose = self.screen_to_world(xc, b[1])
            plume = (b[3] - b[1]) / (ppm1 * COS_E) - self.L * 1.03
            return self.in_flight(nose, False, max(0.0, plume), t)
        # falling, nose down at the bottom, the plume above
        nose = self.screen_to_world(xc, b[3])
        plume = (b[3] - b[1]) / (ppm1 * COS_E) - self.L * 1.03
        return self.in_flight(nose, True, max(0.0, plume) * 0.6, t)

    def launch(self, ctx, f, t):
        """The pad billows with exhaust smoke, then the rocket climbs."""
        m = self.mats
        fl = ctx.flic
        b0 = self.bbox(ctx, 0)
        b = self.bbox(ctx, f)
        xc = (b0[0] + b0[2]) / 2
        ground = self.to_world(xc, b0[3] - 2, 0.0)
        c = self.cal()
        ppm1 = c['ppm'] / c['S']
        # the nose: the original's top edge once it moves, else on the pad
        nose_y = b[1]
        h0 = self.L * 1.18
        z_nose = max(h0, (b0[3] - nose_y) / (ppm1 * COS_E))
        climb = z_nose - h0
        out = []
        nose = ground + np.array([0, 0, z_nose], np.float32)
        out.append(self.in_flight(nose, False, min(climb + self.L * 0.4 * min(t * 4, 1.0), self.L * 4), t))
        # billowing smoke around the pad, spreading and then thinning
        k = min(1.0, t / 0.5)
        fade = 1 - max(0.0, (t - 0.75) / 0.25)
        for i in range(9):
            a = i * 2 * math.pi / 9 + 0.3
            rr = self.r * (1.5 + 2.6 * k)
            p = ground + np.array([math.cos(a) * rr * 1.2, math.sin(a) * rr * 0.8, self.r * (0.8 + 0.6 * (i % 2)) * (0.6 + k)])
            out.append(FX.puff(m, p, self.r * (0.9 + 1.4 * k) * fade, 'nuke_peach' if (i + int(t * 20)) % 3 == 0 else 'smoke_white',
                               seed=i + 3))
        return M.Mesh.concat(out)

    def nuke(self, ctx, t):
        """The mushroom cloud, sized to the original's."""
        m = self.mats
        ub = self.union_bbox(ctx)
        c = self.cal()
        ppm1 = c['ppm'] / c['S']
        W = (ub[2] - ub[0]) / ppm1                        # meters across
        R = W * 0.5
        # the ground zero: the ring's front edge at the original's bottom
        gy = ub[3] - 0.62 * R * SIN_E * ppm1
        g = self.to_world((ub[0] + ub[2]) / 2, gy, 0.0)
        H = (gy - ub[1]) / (ppm1 * COS_E)                 # ground to the top of the cloud
        out = []
        # 1) the fireball: a white ball growing on the ground
        if t < 0.3:
            u = t / 0.3
            rad = R * (0.25 + 0.5 * u)
            out.append(FX.puff(m, g + np.array([0, 0, rad * 0.8]), rad, 'fx_white', seed=1, jitter=0.12))
            for i in range(6):
                a = i * 1.05
                out.append(FX.puff(m, g + np.array([math.cos(a) * rad * 0.6, math.sin(a) * rad * 0.4, rad * (0.6 + 0.3 * (i % 2))]),
                                   rad * 0.55, 'fx_white' if u < 0.7 else 'fx_yellow', seed=10 + i))
        # 2) the cap rising on a stem, over a ground ring
        if t >= 0.22:
            u = min(1.0, (t - 0.22) / 0.3)          # forming
            age = max(0.0, (t - 0.45) / 0.55)       # cooling, darkening, thinning
            fade = 1 - rig.smoothstep(max(0.0, (t - 0.8) / 0.2))
            cap_z = (H - 0.62 * R) * (0.55 + 0.45 * u)
            cap_c = 'fx_white' if t < 0.32 else ('nuke_peach' if age < 0.25 else ('nuke_tan' if age < 0.5 else
                                                                                ('nuke_gray' if age < 0.75 else 'nuke_dark')))
            for i in range(11):
                a = i * 2 * math.pi / 11
                rr = R * 0.55 * (0.6 + 0.4 * u)
                p = g + np.array([math.cos(a) * rr, math.sin(a) * rr * 0.75, cap_z + R * 0.12 * math.sin(3 * a)])
                out.append(FX.puff(m, p, R * 0.36 * (0.7 + 0.3 * u) * fade, cap_c, seed=20 + i))
            out.append(FX.puff(m, g + np.array([0, 0, cap_z + R * 0.22]), R * 0.45 * (0.7 + 0.3 * u) * fade, cap_c, seed=40))
            # the stem
            for i in range(4):
                z = cap_z * (0.2 + 0.2 * i)
                out.append(FX.puff(m, g + np.array([0, 0, z]), R * 0.16 * u * fade,
                                   'fx_yellow' if age < 0.15 else ('nuke_tan' if age < 0.5 else 'nuke_gray'), seed=50 + i))
            # the ground ring of dust spreading out
            ring_r = R * (0.45 + 0.5 * u + 0.15 * age)
            ring_c = 'smoke_white' if age < 0.2 else ('smoke_light' if age < 0.5 else ('smoke' if age < 0.8 else 'smoke_dark'))
            for i in range(20):
                a = i * 2 * math.pi / 20
                p = g + np.array([math.cos(a) * ring_r, math.sin(a) * ring_r * 0.95, R * 0.12])
                out.append(FX.puff(m, p, R * 0.21 * (0.6 + 0.4 * u) * (1 - 0.5 * max(0.0, age - 0.6) / 0.4) * max(fade, 0.35),
                                   ring_c, seed=60 + i, squash=(1.2, 1.2, 0.8)))
        return M.Mesh.concat(out)

    def shot_down(self, ctx, t):
        """A fiery burst in the air where the original has it."""
        m = self.mats
        prof = original_profile(ctx.flic)
        f = int(np.argmax(prof['opaque'][0]))
        ub = prof['bbox'][0, f]
        c = self.cal()
        ppm1 = c['ppm'] / c['S']
        size = (ub[2] - ub[0]) / ppm1 * 0.4
        cen = self.screen_to_world((ub[0] + ub[2]) / 2, (ub[1] + ub[3]) / 2)
        out = [FX.explosion(m, cen, size, t * 1.05, seed=7, n=12, rise=0.1, smoke=False, spread=1.3)]
        # burning fragments falling out of it
        for i in range(6):
            a = i * 1.1
            u = t
            p = cen + np.array([math.cos(a) * size * 1.6 * u, 0, -size * 2.5 * u * u + math.sin(a) * size * 0.5 * u])
            r = size * 0.18 * (1 - u)
            out.append(FX.puff(m, p, r, 'fx_orange' if u < 0.5 else 'fx_red', seed=80 + i))
        return M.Mesh.concat(out)
