"""The land vehicles of the Civ Players League's Modern 2.4 (C7/Lua/modern),
each under an art name of its own (catalog/modern_mod_vehicles.py) and
calibrated against the mod's own pictures (see civ3art.MOD_UNIT_ORIGINALS):

    'laser_tank'  the Laser Tank: a light gray futuristic tank on four track
                  pods under civ-colored guards, a low domed turret with a
                  coiled laser emitter; it fires a civ-colored beam.
    'light_ifv'   the Bradley: a low desert-tan infantry fighting vehicle with
                  side skirts, a small offset turret with a slim 25 mm
                  autocannon (fired in bursts) and a TOW launcher box.
    'humvee'      the Convoy: an olive 4x4 utility truck with a hard top and a
                  roof ring where a gunner with a civ-colored helmet works a
                  machine gun.
    'battery'     the Iron Dome: an anti-missile battery on an outrigger
                  stand: a turntable with a radar on a mast between two pods
                  of interceptor canisters, which swivels and launches.

All are the 'mod_armor' archetype: models/vehicles.Armor (assembling, the
idle rock, the turret scan, firing, the explosion and the charred wreck) with
builders of its own and the behaviours above on top.
"""
import math

import numpy as np

import mesh as M
import rig
from models import archetype
from models import vehicles as V
from models import vparts as VP
from models import vfx as FX

# The laser's beam: a bright core and a civ-colored sheath, both glowing.
FX.GLOW.setdefault('fx_beam', 0.95)
FX.GLOW.setdefault('beam', 0.85)


# ------------------------------------------------------------ wheels

class Wheels:
    """One side's road wheels, standing in for a Track (same interface):
    wheels [(y, z, r)], tire width, x of their inner face."""

    def __init__(self, wheels, width, x):
        self.wheels = wheels
        self.width = width
        self.x = x
        self.L = 2 * math.pi * wheels[0][2]

    def mesh(self, mats, phase, side=1, **kw):
        parts = []
        for y, z, r in self.wheels:
            ang = -math.degrees(phase / r)
            parts.append(VP.disc_wheel(r * 0.62, self.width * 1.02, mats['rim'], mats['wheel_hub'], angle=ang,
                                       center=(self.x, y, z), bolts=5))
            parts.append(VP.axle_x(r, self.width, mats['tire'], seg=18, center=(self.x, y, z)))
            # tread blocks on the tire, which show it turning too
            for i in range(10):
                a = math.radians(ang + i * 36)
                parts.append(M.box(self.width * 1.04, r * 0.16, r * 0.1, mats['tire_dark']).transformed(
                    M.translate(self.x, y + math.cos(a) * r, z + math.sin(a) * r) @ M.rot_x(math.degrees(a) + 90)))
        return M.Mesh.concat(parts)


# ------------------------------------------------------------ builders

def build_laser_tank(Vh):
    """A wedge-shaped light gray hull on four short track pods under
    civ-colored guards, a low dome turret with a civ-colored ring, a coiled
    emitter with a glowing lens, sensor fins."""
    m = Vh.mats
    sec = lambda zt, wt, zb=0.62: [(-1.05, zb), (1.05, zb), (1.25, 0.85), (wt, zt), (-wt, zt), (-1.25, 0.85)]
    hull = VP.sweep([
        (-2.7, sec(1.30, 0.95)),
        (-2.45, sec(1.45, 1.05)),
        (1.2, sec(1.45, 1.05)),
        (2.55, sec(1.0, 0.9)),
        (2.95, [(-0.8, 0.75), (0.8, 0.75), (0.9, 0.8), (0.75, 0.88), (-0.75, 0.88), (-0.9, 0.8)]),
    ], m['paint'])
    # a dark sensor strip across the nose, a vent on the deck, panel lines
    strip = M.box(1.3, 0.06, 0.1, m['paint_dark'], center=(0, 2.62, 0.98))
    vent = M.box(1.1, 0.8, 0.05, m['paint_dark'], center=(0, -1.8, 1.46))
    lines = [M.box(2.12, 0.04, 0.03, m['paint_dark'], center=(0, y, 1.46)) for y in (-0.9, 1.0)]
    # four track pods, each under a civ-colored guard
    tracks, guards = [], []
    for sx in (-1, 1):
        for y0 in (-1.55, 1.45):
            wheels = [(y0 - 0.75, 0.38, 0.32), (y0, 0.3, 0.28), (y0 + 0.75, 0.38, 0.32)]
            tracks.append(V.Track(wheels, 0.62, sx * 1.42, link=0.18, visible_wheels=False))
            g = VP.sweep([(y0 - 1.15, [(-0.36, 0.62), (0.36, 0.62), (0.3, 0.9), (-0.3, 0.9)]),
                          (y0 - 0.85, [(-0.38, 0.55), (0.38, 0.55), (0.34, 0.98), (-0.34, 0.98)]),
                          (y0 + 0.85, [(-0.38, 0.55), (0.38, 0.55), (0.34, 0.98), (-0.34, 0.98)]),
                          (y0 + 1.15, [(-0.36, 0.62), (0.36, 0.62), (0.3, 0.9), (-0.3, 0.9)])], m['decal'])
            guards.append(g.transformed(M.translate(sx * 1.42, 0, 0)))
            # the pod's strut to the hull and a round light hub on its side
            guards.append(M.box(0.3, 0.5, 0.22, m['paint_dark'], center=(sx * 1.12, y0, 0.78)))
            guards.append(VP.axle_x(0.15, 0.06, m['paint'], center=(sx * 1.78, y0, 0.72)))
    hull = M.Mesh.concat([hull, strip, vent] + lines + guards)
    # the dome turret
    prof = [(0.0, 1.05), (0.1, 1.08), (0.3, 1.02), (0.5, 0.86), (0.66, 0.6), (0.76, 0.3), (0.8, 0.0)]
    dome = M.lathe(prof, m['paint'], seg=22).transformed(M.scale(1.0, 1.08, 1.0))
    ring = M.lathe([(0.1, 1.095), (0.24, 1.07)], m['decal'], seg=22).transformed(M.scale(1.0, 1.08, 1.0))
    cap = M.cylinder(0.32, 0.26, 0.12, m['paint_dark'], seg=12, z0=0.74).transformed(M.translate(-0.25, -0.35, 0))
    fins = [VP.prism([(-0.3, 0.0), (0.25, 0.0), (0.0, 0.55)], 0.05, m['paint_dark']).transformed(
        M.translate(sx * 0.62, -0.75, 0.52) @ M.rot_y(90) @ M.rot_z(90)) for sx in (-1, 1)]
    ant = VP.tube((0.45, -0.6, 0.6), (0.48, -0.7, 1.55), 0.014, 0.008, m['steel'], seg=4)
    turret = M.Mesh.concat([dome, ring, cap, ant] + fins)
    # the emitter: a thick housing, three coils, a thin barrel and a glowing lens
    gun = [M.box(0.5, 0.55, 0.42, m['paint'], center=(0, 0.1, 0), bevel=0.06),
           VP.tube((0, 0.3, 0), (0, 1.9, 0), 0.11, 0.09, m['paint'], seg=12),
           VP.tube((0, 1.85, 0), (0, 2.15, 0), 0.15, 0.15, m['paint_dark'], seg=12)]
    for y in (0.75, 1.1, 1.45):
        gun.append(VP.tube((0, y, 0), (0, y + 0.14, 0), 0.17, 0.17, m['decal'], seg=12))
    gun.append(VP.tube((0, 2.14, 0), (0, 2.2, 0), 0.1, 0.1, m['fx_beam'], seg=12))
    return dict(hull=hull, tracks=tracks, turret=turret, turret_pos=(0, -0.15, 1.45), gun=M.Mesh.concat(gun),
                gun_pos=(0, 0.75, 0.4), muzzle=(0, 2.2, 0), recoil=0.0, scan=40, laser=True,
                blast=(0, -0.15, 2.0))


def build_light_ifv(Vh):
    """A low tan hull with a steep glacis and side skirts (a thin civ-colored
    stripe on each), a small angular turret set to the left with a slim
    autocannon and a TOW box on its right (civ-colored cap)."""
    m = Vh.mats
    sec = lambda zt, wt: [(-1.0, 0.42), (1.0, 0.42), (1.0, 0.55), (1.42, 0.55), (1.42, zt - 0.22), (wt, zt), (-wt, zt), (-1.42, zt - 0.22), (-1.42, 0.55), (-1.0, 0.55)]
    hull = VP.sweep([
        (-2.75, sec(1.30, 1.22)),
        (1.2, sec(1.36, 1.25)),
        (2.55, sec(0.98, 1.2)),
        (2.85, [(-1.0, 0.55), (1.0, 0.55), (1.0, 0.6), (1.38, 0.6), (1.38, 0.72), (1.2, 0.82), (-1.2, 0.82), (-1.38, 0.72), (-1.38, 0.6), (-1.0, 0.6)]),
    ], m['paint'])
    skirts = [M.box(0.08, 4.9, 0.5, m['paint_dark'], center=(sx * 1.47, -0.05, 0.78)) for sx in (-1, 1)]
    stripes = [M.box(0.04, 1.3, 0.12, m['decal'], center=(sx * 1.52, 0.9, 0.86)) for sx in (-1, 1)]
    hatch = M.box(0.5, 0.55, 0.06, m['paint_dark'], center=(0.75, 1.4, 1.28))
    grille = M.box(0.9, 0.9, 0.05, m['paint_dark'], center=(0.55, -1.9, 1.32))
    door = M.box(1.3, 0.06, 0.6, m['paint_dark'], center=(0, -2.77, 0.95))
    lights = [VP.axle_x(0.07, 0.06, m['steel'], center=(sx * 0.95, 2.62, 0.95)) for sx in (-1, 1)]
    hull = VP.camo(hull, m['paint'], [(0.45, m['paint_dark'])], max_edge=0.25, seed=61, scale=0.5)
    hull = M.Mesh.concat([hull, hatch, grille, door] + skirts + stripes + lights)
    wheels = [(-2.45, 0.46, 0.3)] + [(y, 0.3, 0.28) for y in (-1.8, -1.1, -0.4, 0.3, 1.0, 1.65)] + [(2.45, 0.6, 0.28)]
    tracks = [V.Track(wheels, 0.52, sx * 1.16, link=0.2, visible_wheels=True, top=[(0.0, 0.7, 0.1)]) for sx in (-1, 1)]
    tsec = lambda w, z1, wt: [(-w, 0.0), (w, 0.0), (wt, z1), (-wt, z1)]
    tur = VP.sweep([(-0.95, tsec(0.78, 0.62, 0.62)), (0.55, tsec(0.82, 0.66, 0.68)), (1.0, tsec(0.55, 0.48, 0.45))], m['paint'])
    tur = VP.camo(tur, m['paint'], [(0.45, m['paint_dark'])], max_edge=0.25, seed=62, scale=0.5)
    tow = M.box(0.36, 1.1, 0.34, m['paint_dark'], center=(1.0, -0.15, 0.42), bevel=0.03)
    tow_cap = M.box(0.38, 0.06, 0.36, m['decal'], center=(1.0, 0.42, 0.42))
    sight = M.box(0.28, 0.28, 0.22, m['paint_dark'], center=(-0.4, 0.1, 0.74))
    ant = VP.tube((-0.45, -0.6, 0.5), (-0.46, -0.65, 1.35), 0.012, 0.008, m['steel'], seg=4)
    turret = M.Mesh.concat([tur, tow, tow_cap, sight, ant])
    gun = M.Mesh.concat([VP.tube((0, -0.1, 0), (0, 2.5, 0), 0.06, 0.055, m['steel'], seg=8),
                         VP.tube((0, 2.3, 0), (0, 2.55, 0), 0.08, 0.08, m['steel'], seg=8),
                         M.box(0.34, 0.36, 0.32, m['paint'], center=(0, 0.0, 0), bevel=0.03)])
    return dict(hull=hull, tracks=tracks, turret=turret, turret_pos=(-0.2, 0.3, 1.34), gun=gun,
                gun_pos=(0, 1.0, 0.33), muzzle=(0, 2.55, 0), recoil=0.12, scan=40, burst=True)


def _gunner(m):
    """A gunner standing in the roof ring (turret space, facing +y): torso,
    arms to the gun's grips, a head in a civ-colored helmet."""
    parts = [M.capsule((0, -0.15, 0.0), (0, -0.12, 0.55), 0.2, 0.22, m['uniform'], seg=10),
             M.sphere(0.13, m['skin'], center=(0, -0.08, 0.74), seg=10),
             M.lathe([(0.0, 0.17), (0.06, 0.17), (0.14, 0.13), (0.2, 0.06), (0.22, 0.0)], m['helmet'], seg=12)
             .transformed(M.translate(0, -0.1, 0.76))]
    for sx in (-1, 1):
        parts.append(M.capsule((sx * 0.2, -0.12, 0.45), (sx * 0.14, 0.22, 0.32), 0.06, 0.055, m['uniform'], seg=8))
    return M.Mesh.concat(parts)


def build_humvee(Vh):
    """An olive 4x4 truck: a wide low body, flat hood with a grille, a boxy
    hard-top cabin with dark windows, big wheels, a roof ring with a gunner
    in a civ-colored helmet behind a machine gun; civ-colored stars on the
    doors."""
    m = Vh.mats
    body = VP.sweep([(-2.35, [(-1.12, 0.55), (1.12, 0.55), (1.12, 1.18), (-1.12, 1.18)]),
                     (2.1, [(-1.12, 0.55), (1.12, 0.55), (1.12, 1.18), (-1.12, 1.18)]),
                     (2.4, [(-1.0, 0.6), (1.0, 0.6), (1.0, 1.05), (-1.0, 1.05)])], m['paint'])
    hood = VP.sweep([(0.45, [(-1.02, 1.17), (1.02, 1.17), (0.95, 1.36), (-0.95, 1.36)]),
                     (2.2, [(-1.02, 1.17), (1.02, 1.17), (0.95, 1.3), (-0.95, 1.3)])], m['paint'])
    cab = VP.sweep([(-2.3, [(-1.08, 1.17), (1.08, 1.17), (1.0, 2.0), (-1.0, 2.0)]),
                    (0.2, [(-1.08, 1.17), (1.08, 1.17), (1.0, 2.0), (-1.0, 2.0)]),
                    (0.6, [(-1.08, 1.17), (1.08, 1.17), (0.98, 1.92), (-0.98, 1.92)])], m['paint'])
    body = VP.camo(M.Mesh.concat([body, hood, cab]), m['paint'], [(0.35, m['paint_dark'])], max_edge=0.3, seed=51, scale=0.5)
    glass = [M.box(0.82, 0.05, 0.42, m['glass'], center=(sx * 0.48, 0.58, 1.66)) for sx in (-1, 1)]
    glass += [M.box(0.05, 0.6, 0.36, m['glass'], center=(sx * 1.09, y, 1.68)) for sx in (-1, 1) for y in (-0.05, -0.85)]
    grille = M.box(1.5, 0.05, 0.4, m['paint_dark'], center=(0, 2.41, 0.84))
    bumper = M.box(2.1, 0.18, 0.18, m['paint_dark'], center=(0, 2.45, 0.55))
    lights = [M.box(0.18, 0.05, 0.12, m['light'], center=(sx * 0.82, 2.42, 1.0)) for sx in (-1, 1)]
    arches = [M.box(0.12, 1.25, 0.12, m['paint_dark'], center=(sx * 1.15, y, 1.12)) for sx in (-1, 1) for y in (-1.55, 1.45)]
    stars = [V.decal_on_side(V.star(0.2, m['decal']), sx, -0.45, 0.88, 1.13) for sx in (-1, 1)]
    hull = M.Mesh.concat([body, grille, bumper] + glass + lights + arches + stars)
    wh = [(-1.55, 0.45, 0.45), (1.45, 0.45, 0.45)]
    tracks = [Wheels(wh, 0.34, sx * 1.0) for sx in (-1, 1)]
    # the roof ring with its gunner, and a shield in front of the gun
    ring = M.lathe([(0.0, 0.5), (0.1, 0.52), (0.14, 0.45), (0.14, 0.38)], m['paint_dark'], seg=16)
    shield = VP.sweep([(0.42, [(-0.4, 0.05), (0.4, 0.05), (0.36, 0.55), (-0.36, 0.55)]),
                       (0.48, [(-0.4, 0.05), (0.4, 0.05), (0.36, 0.55), (-0.36, 0.55)])], m['paint'])
    turret = M.Mesh.concat([ring, shield, _gunner(m)])
    gun = M.Mesh.concat([VP.tube((0, -0.25, 0), (0, 1.0, 0), 0.04, 0.035, m['steel'], seg=8),
                         M.box(0.16, 0.45, 0.16, m['steel'], center=(0, -0.05, 0)),
                         M.box(0.12, 0.18, 0.14, m['olive_box'], center=(0.15, 0.0, -0.05))])
    return dict(hull=hull, tracks=tracks, turret=turret, turret_pos=(0, -0.55, 2.0), gun=gun,
                gun_pos=(0, 0.25, 0.42), muzzle=(0, 1.0, 0), recoil=0.05, scan=50, burst=True,
                wheeled=True, blast=(0, 0.0, 1.5))


def interceptor(m, length=1.5, r=0.07):
    """A slim white interceptor with a dark nose and fins, along +y from 0."""
    body = VP.tube((0, 0, 0), (0, length * 0.8, 0), r, r, m['missile'], seg=8)
    nose = VP.tube((0, length * 0.8, 0), (0, length, 0), r, 0.01, m['steel_dark'], seg=8)
    fins = [M.box(0.015, 0.22, r * 2.8, m['steel_dark']).transformed(M.rot_y(a) @ M.translate(0, 0.12, r * 1.2))
            for a in (45, 135, 225, 315)]
    return M.Mesh.concat([body, nose] + fins)


def build_battery(Vh):
    """An outrigger stand (four legs on pads) with a turntable carrying a
    housing and a radar on a mast between two launcher pods, each of four
    interceptor canisters (civ-colored panels on the pods' sides)."""
    m = Vh.mats
    legs = []
    for a in (45, 135, 225, 315):
        r = math.radians(a)
        tip = (math.cos(r) * 1.7, math.sin(r) * 1.7, 0.12)
        legs.append(VP.bar((0, 0, 0.62), tip, 0.26, 0.2, m['paint_dark']))
        legs.append(M.cylinder(0.24, 0.24, 0.08, m['steel_dark'], seg=10).transformed(M.translate(tip[0], tip[1], 0.0)))
        legs.append(VP.bar((math.cos(r) * 1.3, math.sin(r) * 1.3, 0.3), (math.cos(r) * 1.3, math.sin(r) * 1.3, 0.0),
                           0.08, 0.08, m['steel_dark']))
    base = M.cylinder(0.75, 0.62, 0.55, m['paint'], seg=14, z0=0.35)
    box = M.box(1.1, 1.0, 0.35, m['paint'], center=(0, -0.2, 0.55), bevel=0.04)
    hull = M.Mesh.concat([base, box] + legs)
    tbl = M.cylinder(0.7, 0.7, 0.14, m['paint_dark'], seg=16)
    housing = M.box(1.0, 1.2, 1.0, m['paint'], center=(0, -0.05, 0.6), bevel=0.06)
    mast = VP.tube((0, -0.2, 1.05), (0, -0.2, 1.7), 0.05, 0.05, m['steel_dark'], seg=6)
    dish = M.disc(0.55, 0.38, 0.1, m['radar'], seg=16, dome=0.15).transformed(M.translate(0, -0.2, 1.85) @ M.rot_x(-12))
    lens = M.box(0.36, 0.12, 0.26, m['glass'], center=(0, 0.56, 0.8))
    turret = M.Mesh.concat([tbl, housing, mast, dish, lens])
    pods, tubes = [], []
    for sx in (-1, 1):
        cx = sx * 1.0
        pods.append(M.box(0.8, 2.0, 0.8, m['paint'], center=(cx, 0.0, 0.0), bevel=0.05))
        pods.append(M.box(0.03, 1.4, 0.46, m['decal'], center=(cx + sx * 0.41, -0.1, 0.0)))
        for i, (dx, dz) in enumerate([(-0.19, -0.19), (0.19, -0.19), (-0.19, 0.19), (0.19, 0.19)]):
            p = (cx + dx, 1.01, dz)
            pods.append(VP.axle_x(0.14, 0.04, m['steel_dark'], seg=10).transformed(M.translate(*p) @ M.rot_z(90)))
            pods.append(VP.axle_x(0.09, 0.06, m['missile'], seg=8).transformed(M.translate(p[0], 1.03, p[2]) @ M.rot_z(90)))
            tubes.append((cx + dx, -0.6, dz))
    gun = M.Mesh.concat(pods + [VP.axle_x(0.1, 2.4, m['steel_dark'], center=(0, 0, 0))])
    return dict(hull=hull, tracks=[], turret=turret, turret_pos=(0, 0, 0.9), gun=gun,
                gun_pos=(0, 0.0, 0.7), muzzle=(0, 1.05, 0), recoil=0.0, scan=55, battery=True, rest_pitch=12,
                tubes=tubes, blast=(0, 0, 1.2))


BUILDERS = {'laser_tank': build_laser_tank, 'light_ifv': build_light_ifv, 'humvee': build_humvee,
            'battery': build_battery}

PAINTS = {
    'laser_tank': {'paint': '#d4d6d2', 'paint_dark': '#7e848a', 'track': '#3a3c40', 'track_link': '#5a5e64',
                   'wheel': '#4a4c50', 'wheel_hub': '#2a2c2e', 'steel': ('#8a8e94', 0.5, 24), 'decal': '#f0f0f0',
                   'fx_beam': '#eef8ff', 'beam': '#f4f4f4'},
    'light_ifv': {'paint': '#ae9870', 'paint_dark': '#8a7756', 'track': '#2e2c28', 'track_link': '#4e4a44',
                  'wheel': '#4c463a', 'wheel_hub': '#262420', 'steel': ('#5a5a58', 0.5, 24), 'decal': '#f0f0f0'},
    'humvee': {'paint': '#66703f', 'paint_dark': '#48502e', 'tire': '#2a2a28', 'tire_dark': '#1c1c1a',
               'rim': '#55603a', 'wheel_hub': '#2e3222', 'steel': ('#4a4a48', 0.5, 24), 'glass': ('#38424a', 0.5, 30),
               'light': '#e8e4d0', 'uniform': '#5e6644', 'skin': '#c89a74', 'helmet': '#f0f0f0',
               'olive_box': '#4e5634', 'decal': '#f0f0f0'},
    'battery': {'paint': '#7c806a', 'paint_dark': '#5a5e4c', 'steel_dark': '#34363a', 'radar': '#cfd2c8',
                'glass': ('#2e3a44', 0.5, 30), 'missile': '#e8e6e0', 'decal': '#f0f0f0'},
}


@archetype('mod_armor')
class ModArmor(V.Armor):
    """vehicles.Armor with Modern's designs (see the module's docstring)."""

    def __init__(self, unit, params):
        V.VehicleBase.__init__(self, unit, params)
        kind = params['model']
        self.kind = kind
        self.paint(PAINTS[kind], metal=('steel', 'steel_dark'))
        self.b = BUILDERS[kind](self)
        self.track_L = self.b['tracks'][0].L if self.b['tracks'] else 1.0
        self.autofit()

    def frame(self, action, f, n, ctx):
        b = self.b
        t = self.times(action, f, n)
        if b.get('battery') and action not in ('DEATH',) and not action.startswith('ATTACK'):
            return self.swivel(t, action)
        if action == 'VICTORY' and b.get('laser'):
            return self.spin(t)
        if action in ('RUN', 'WALK') and b.get('wheeled'):
            return self.drive(t)
        if action.startswith('ATTACK') and b.get('laser'):
            return self.attack_laser(t, ctx)
        if action.startswith('ATTACK') and b.get('battery'):
            return self.attack_battery(t, ctx)
        return super().frame(action, f, n, ctx)

    def loops(self, action):
        if action == 'VICTORY':
            return False
        return super().loops(action)

    def drive(self, t):
        """Wheels turn two bolt spacings per loop; the body bobs."""
        r = self.b['tracks'][0].wheels[0][2]
        phase = t * r * math.radians(144)
        s = math.sin(2 * math.pi * t * 2)
        mesh, *_ = self.assemble(phase=phase, body=(0.025 + 0.02 * s, 0.8 * math.cos(2 * math.pi * t), 0.5 * s, 0, 0))
        return FX.FxMesh(mesh, self.mats)

    def spin(self, t):
        """The turret turns a full circle and the emitter lifts in salute."""
        k = rig.smoothstep(t)
        mesh, *_ = self.assemble(yaw=360 * k, pitch=18 * math.sin(math.pi * t))
        return FX.FxMesh(mesh, self.mats)

    def swivel(self, t, action):
        """The battery's turret sweeps the sky, its pods nodding."""
        amp = 50 if action in ('DEFAULT', 'FORTIFY', 'FIDGET') else 30
        yaw = amp * math.sin(2 * math.pi * t)
        pitch = self.b['rest_pitch'] + 6 * math.sin(4 * math.pi * t)
        mesh, *_ = self.assemble(yaw=yaw, pitch=pitch)
        return FX.FxMesh(mesh, self.mats)

    def attack_laser(self, t, ctx):
        """A civ-colored beam with a bright core: full at the shot, thinning
        away; the lens glows and the hull rocks a little."""
        b, mats = self.b, self.mats
        tf = self.p.get('fire', 0.08)
        dt = t - tf
        rock = -1.0 * math.exp(-max(dt, 0) * 6) * math.sin(max(dt, 0) * 20) if dt >= 0 else 0
        mesh, G, H, T = self.assemble(body=(0, rock, 0, 0, 0), pitch=2)
        fx = []
        if 0 <= dt < 0.85:
            k = 1.0 if dt < 0.45 else max(0.0, 1 - (dt - 0.45) / 0.4)
            length = self.p.get('beam', 5.5)
            p0 = (G @ np.array([*b['muzzle'], 1.0], np.float32))[:3]
            fwd = G[:3, :3] @ np.array([0, 1, 0], np.float32)
            r = 0.17 * (0.35 + 0.65 * k)
            fx.append(VP.tube(p0, p0 + fwd * length, r, r * 0.8, mats['beam'], seg=10))
            fx.append(VP.tube(p0 - fwd * 0.02, p0 + fwd * (length + 0.05), r * 0.5, r * 0.4, mats['fx_beam'], seg=8))
            if dt < 0.5:
                fx.append(FX.puff(mats, p0, 0.22 * k + 0.08, 'fx_beam', seed=3))
        return FX.FxMesh(M.Mesh.concat([mesh] + fx), mats)

    def attack_battery(self, t, ctx):
        """The pods swing up; two interceptors leave a moment apart, each with
        a flame and a white trail."""
        b, mats = self.b, self.mats
        tf = self.p.get('fire', None)
        if tf is None:
            tf = V.flash_time(ctx, 0.3)
        up = 40.0
        t_up = max(0.05, tf - 0.08)
        if t < t_up:
            pitch = b['rest_pitch'] + (up - b['rest_pitch']) * rig.smoothstep(t / t_up)
        elif t < 0.8:
            pitch = up
        else:
            pitch = up + (b['rest_pitch'] - up) * rig.smoothstep((t - 0.8) / 0.2)
        mesh, G, H, T = self.assemble(pitch=pitch)
        fx = []
        fwd = G[:3, :3] @ np.array([0, 1, 0], np.float32)
        for k, tube in enumerate((b['tubes'][3], b['tubes'][4])):
            u = t - tf - k * 0.18
            if u < 0:
                continue
            p0 = (G @ np.array([tube[0], 1.1, tube[2], 1.0], np.float32))[:3]
            dist = u * 30.0
            pos = p0 + fwd * dist
            if dist < 14:
                fx.append(interceptor(mats).transformed(V._orient_y(pos, fwd)))
                fx.append(FX.plume(mats, pos, -fwd, 0.8, 0.1, t=u, seed=k, smoke=False))
            for i in range(14):
                s = i / 14 * min(dist, 10)
                age = (dist - s) / 16.0
                if age < 1:
                    fx.append(FX.puff(mats, p0 + fwd * (s + 0.3), 0.3 * (1 + age) * (1 - age ** 2),
                                      'smoke_white' if age < 0.4 else 'smoke_light', seed=20 + i + 7 * k))
            if u < 0.1:
                fx.append(FX.muzzle_flash(mats, p0 - fwd * 0.1, -fwd, 0.5, seed=4 + k))
        return FX.FxMesh(M.Mesh.concat([mesh] + fx), mats)
