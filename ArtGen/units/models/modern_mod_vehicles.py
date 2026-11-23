"""The land vehicles of the Civ Players League's Modern 2.4 (C7/Lua/modern),
each under an art name of its own (catalog/modern_mod_vehicles.py) and
calibrated against the mod's own pictures (see civ3art.MOD_UNIT_ORIGINALS).
Round 2: each is drawn from a real vehicle (or, for the laser tank, the
genre's), part by part, in the stock armor's style (models/vehicles.py).

    'laser_tank'  the Laser Tank: an original design, a low wedge-hulled tank
                  on one long track a side behind bolted skirts, a low
                  turret, and above it the beam projector that makes its
                  silhouette: a long raised emitter housing finned along its
                  top and flanks, capacitor drums at its root, a ringed lens
                  head. Its beam glows, casts no shadow and has soft edges;
                  the lens goes dark when it dies.
    'light_ifv'   the Bradley: an M2A3 Bradley in olive drab with reactive
                  armor tiles (BRAT) on its sides and nose, the turret with
                  its 25 mm Bushmaster, the twin TOW box on the left, the
                  commander's viewer, smoke dischargers and a stowage rack.
    'humvee'      the Convoy: an up-armored M1151 Humvee in woodland
                  camouflage: slotted grille, brush guard, thick-glassed doors,
                  a cargo deck with jerrycans, an O-GPK gunner's shield with
                  its windows and an M2 machine gun, a gunner in a
                  civ-colored helmet and a civ-colored marker panel on the
                  hood.
    'battery'     the Iron Dome: a Tamir launcher on a 6x6 truck in IDF sinai
                  gray: a cab-over cab, outrigger jacks, and the launcher box
                  of twenty interceptor cells raised steeply on its rams.

All are the 'mod_armor' archetype: models/vehicles.Armor (assembling, the
idle rock, the turret scan, firing, the explosion and the charred wreck) with
builders of their own and the behaviours above on top.
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


# ------------------------------------------------------------ small parts

def _soften(img):
    """A 3x3 blur of an RGBA layer's alpha (color kept where it was), for
    the glow of the laser's beam."""
    a = img[..., 3].astype(np.float32)
    p = np.pad(a, 1, mode='edge')
    blur = sum(p[1 + dy:1 + dy + a.shape[0], 1 + dx:1 + dx + a.shape[1]] for dy in (-1, 0, 1) for dx in (-1, 0, 1)) / 9
    out = img.copy()
    rgb = img[..., :3].astype(np.float32)
    # spread the color into the newly covered pixels from their neighbours
    pr = np.pad(rgb * a[..., None], ((1, 1), (1, 1), (0, 0)), mode='edge')
    num = sum(pr[1 + dy:1 + dy + a.shape[0], 1 + dx:1 + dx + a.shape[1]] for dy in (-1, 0, 1) for dx in (-1, 0, 1))
    col = num / np.maximum(blur[..., None] * 9, 1e-3)
    out[..., :3] = np.clip(col, 0, 255).astype(np.uint8)
    out[..., 3] = np.clip(np.maximum(blur, a * 0.85), 0, 255).astype(np.uint8)
    return out


def _over(top, under):
    """RGBA `top` composited over `under` (uint8, straight alpha)."""
    ta = top[..., 3:4].astype(np.float32) / 255
    ua = under[..., 3:4].astype(np.float32) / 255
    oa = ta + ua * (1 - ta)
    rgb = (top[..., :3] * ta + under[..., :3] * ua * (1 - ta)) / np.maximum(oa, 1e-6)
    return np.concatenate([np.clip(rgb, 0, 255), oa * 255], -1).astype(np.uint8)


def bx(sx, sy, sz, mat, c, bevel=0.0):
    return M.box(sx, sy, sz, mat, center=c, bevel=bevel)


def slats(x0, x1, y0, y1, z, n, mat, along='x', w=0.05, h=0.05):
    """n parallel bars over a rectangle (a grille seen from above)."""
    out = []
    for i in range(n):
        u = (i + 0.5) / n
        if along == 'x':     # bars run along x, spaced in y
            out.append(bx(x1 - x0, w, h, mat, ((x0 + x1) / 2, y0 + (y1 - y0) * u, z)))
        else:
            out.append(bx(w, y1 - y0, h, mat, (x0 + (x1 - x0) * u, (y0 + y1) / 2, z)))
    return out


def side_bolts(x, y0, y1, z, n, mat, r=0.035):
    """A row of bolt heads on a side face at x (along y)."""
    return [bx(0.03, r * 2, r * 2, mat, (x, y0 + (y1 - y0) * (i + 0.5) / n, z)) for i in range(n)]


def smoke_cluster(mats, mat, p, yaw, n=4, pitch=30, r=0.055, L=0.26):
    """A row of smoke-grenade tubes on a bracket, pointing forward-up and out."""
    T = M.translate(*p) @ M.rot_z(yaw)
    parts = [bx(0.12, 0.2, n * r * 2.1, mat, (0, -0.05, 0))]
    for i in range(n):
        z = (i - (n - 1) / 2) * r * 2.1
        a = np.array([0, 0, z])
        d = np.array([0, math.cos(math.radians(pitch)), math.sin(math.radians(pitch))]) * L
        parts.append(VP.tube(a, a + d, r, r, mat, seg=8))
    return M.Mesh.concat(parts).transformed(T)


def antenna(mat, base_mat, p, h, lean=(0.0, -0.08)):
    x, y, z = p
    return M.Mesh.concat([M.cylinder(0.06, 0.05, 0.1, base_mat, seg=8, z0=z).transformed(M.translate(x, y, 0)),
                          VP.tube((x, y, z + 0.08), (x + lean[0], y + lean[1], z + h), 0.014, 0.008, mat, seg=4)])


def headlight(mats, p, guard=True, frame='paint_dark', lens='light', s=1.0):
    """A square headlight housing with a lens and a little wire guard."""
    x, y, z = p
    parts = [bx(0.24 * s, 0.1, 0.2 * s, mats[frame], (x, y, z)),
             bx(0.17 * s, 0.03, 0.14 * s, mats[lens], (x, y + 0.055, z))]
    if guard:
        for dx in (-0.07, 0.0, 0.07):
            parts.append(bx(0.02, 0.1, 0.22 * s, mats[frame], (x + dx * s, y + 0.1, z)))
    return parts


def jerrycan(mats, p, yaw=0, mat='can'):
    x, y, z = p
    c = M.Mesh.concat([bx(0.17, 0.35, 0.47, mats[mat], (0, 0, 0.235), bevel=0.02),
                       bx(0.18, 0.2, 0.03, mats[mat], (0, 0, 0.3)),
                       bx(0.05, 0.14, 0.06, mats['steel_dark'], (0, 0.08, 0.5))])
    return c.transformed(M.translate(x, y, z) @ M.rot_z(yaw))


def bag(mats, p, size, mat='canvas', straps=2):
    """A stowage bag: a soft rounded box with straps around it."""
    x, y, z = p
    sx, sy, sz = size
    parts = [M.rounded_box(sx, sy, sz, mats[mat], center=(x, y, z), r=min(sx, sy, sz) * 0.35)]
    for i in range(straps):
        u = (i + 1) / (straps + 1)
        parts.append(bx(sx * 1.04, 0.04, sz * 1.04, mats['strap'], (x - sx / 2 + sx * u, y, z)) if sx > sy else
                     bx(sx * 1.04, 0.04, sz * 1.04, mats['strap'], (x, y - sy / 2 + sy * u, z)))
    return parts


# ------------------------------------------------------------ running gear

class Track2(V.Track):
    """A Track with a toothed drive sprocket, an idler and return rollers
    that turn with it, and rubber-tired road wheels."""

    def __init__(self, wheels, width, x, sprocket=None, idler=None, rollers=(), **kw):
        allw = list(wheels) + ([sprocket] if sprocket else []) + ([idler] if idler else [])
        super().__init__(allw, width, x, **kw)
        self.road = list(wheels)
        self.sprocket = sprocket
        self.idler = idler
        self.rollers = list(rollers)

    def mesh(self, mats, phase, belt='track', link='track_link', wheel='wheel', hub='wheel_hub', side=1):
        vis = self.visible
        self.visible = False
        out = [super().mesh(mats, phase, belt, link, wheel, hub, side)]
        self.visible = vis
        if not vis:
            return out[0]
        xo = self.x + side * self.width * 0.42
        for y, z, r in self.road:
            ang = -math.degrees(phase / r)
            out.append(VP.axle_x(r * 0.93, self.width * 0.3, mats['tire'], seg=16, center=(xo - side * 0.02, y, z)))
            out.append(VP.disc_wheel(r * 0.74, self.width * 0.34, mats[wheel], mats[hub], angle=ang,
                                     center=(xo, y, z), bolts=6))
        for y, z, r in self.rollers:
            ang = -math.degrees(phase / r)
            out.append(VP.disc_wheel(r, self.width * 0.3, mats[wheel], mats[hub], angle=ang,
                                     center=(xo - side * 0.05, y, z), bolts=3))
        for spec, teeth in ((self.sprocket, 11), (self.idler, 0)):
            if not spec:
                continue
            y, z, r = spec
            ang = -math.degrees(phase / r)
            out.append(VP.disc_wheel(r * 0.8, self.width * 0.32, mats[wheel], mats[hub], angle=ang,
                                     center=(xo, y, z), bolts=6 if teeth else 4))
            for i in range(teeth):
                a = math.radians(ang + i * 360 / teeth)
                out.append(M.box(self.width * 0.3, r * 0.2, r * 0.26, mats[wheel]).transformed(
                    M.translate(xo, y + math.cos(a) * r * 0.86, z + math.sin(a) * r * 0.86) @ M.rot_x(math.degrees(a) + 90)))
        return M.Mesh.concat(out)


class Wheels:
    """One side's road wheels on a wheeled vehicle, standing in for a Track
    (same interface): wheels [(y, z, r)], tire width, x of the tire's center.
    Knobbly tires with a tread that turns, dished rims with a bead-lock ring
    of bolts, and a hub."""

    def __init__(self, wheels, width, x, rim_k=0.6):
        self.wheels = wheels
        self.width = width
        self.x = x
        self.rim_k = rim_k
        self.L = 2 * math.pi * wheels[0][2]

    def mesh(self, mats, phase, side=1, **kw):
        parts = []
        w = self.width
        for y, z, r in self.wheels:
            ang = -math.degrees(phase / r)
            c = (self.x, y, z)
            # the tire: a fat cylinder with rounded shoulders, staggered tread blocks round it
            parts.append(VP.axle_x(r * 0.95, w, mats['tire'], seg=20, center=c))
            parts.append(VP.axle_x(r, w * 0.78, mats['tire'], seg=20, center=c))
            for i in range(16):
                a = math.radians(ang + i * 360 / 16)
                sx = 1 if i % 2 else -1
                parts.append(M.box(w * 0.45, r * 0.16, r * 0.08, mats['tire_dark']).transformed(
                    M.translate(self.x + sx * w * 0.2, y + math.cos(a) * r, z + math.sin(a) * r) @ M.rot_x(math.degrees(a) + 90)))
            # the rim (dished inward), its bead-lock ring of bolts and the hub
            xo = self.x + side * w * 0.5
            parts.append(VP.axle_x(r * self.rim_k, 0.04, mats['rim'], seg=16, center=(xo - side * 0.05, y, z)))
            parts.append(VP.ring_x(r * self.rim_k * 1.02, r * self.rim_k * 0.84, 0.05, mats['rim_dark'], seg=16,
                                   center=(xo - side * 0.02, y, z)))
            for i in range(8):
                a = math.radians(ang + i * 45)
                parts.append(bx(0.05, 0.045, 0.045, mats['wheel_hub'],
                                (xo, y + math.cos(a) * r * self.rim_k * 0.93, z + math.sin(a) * r * self.rim_k * 0.93)))
            parts.append(VP.axle_x(r * 0.24, 0.16, mats['wheel_hub'], seg=10, center=(xo - side * 0.04, y, z)))
            for i in range(6):
                a = math.radians(ang + i * 60)
                parts.append(bx(0.04, 0.04, 0.04, mats['rim_dark'],
                                (xo + side * 0.04, y + math.cos(a) * r * 0.32, z + math.sin(a) * r * 0.32)))
        return M.Mesh.concat(parts)


# ------------------------------------------------------------ the laser tank

def build_laser_tank(Vh):
    """A low wedge hull on one long track a side behind armored skirts, a low
    turret with a broken-up roof, and above it the beam projector: a long
    raised emitter housing on a pylon, finned along its top and flanks, with
    capacitor drums and cable bundles at its root and a ringed lens head."""
    m = Vh.mats
    P, D, L = m['paint'], m['paint_dark'], m['paint_lt']
    parts = []
    # the hull: a low wedge, a long shallow glacis to a narrow nose, sponsons over the tracks
    sec = lambda zt, wt, zs=0.98: [(-1.0, 0.45), (1.0, 0.45), (1.0, 0.58), (1.52, 0.58), (1.52, zs), (wt, zt), (-wt, zt), (-1.52, zs), (-1.52, 0.58), (-1.0, 0.58)]
    hull = VP.sweep([
        (-3.15, sec(1.18, 0.95, 0.88)),
        (-2.95, sec(1.32, 1.0, 0.9)),
        (0.6, sec(1.34, 1.0, 0.9)),
        (2.75, sec(0.86, 0.95, 0.8)),
        (3.25, [(-1.0, 0.58), (1.0, 0.58), (1.0, 0.6), (1.35, 0.6), (1.35, 0.68), (0.9, 0.74), (-0.9, 0.74), (-1.35, 0.68), (-1.35, 0.6), (-1.0, 0.6)]),
    ], P)
    hull = VP.camo(hull, P, [(0.38, m['paint_mid'])], max_edge=0.3, seed=71, scale=0.42)
    parts.append(hull)
    # glacis: two raised armor plates with a seam between, the driver's hatch with
    # periscopes, sensor heads, headlights, tow eyes
    for sx in (-1, 1):
        parts.append(VP.sweep([(0.75, [(sx * 0.08, 1.35), (sx * 0.95, 1.35), (sx * 0.95, 1.4), (sx * 0.08, 1.4)]),
                               (2.6, [(sx * 0.08, 0.9), (sx * 0.9, 0.9), (sx * 0.9, 0.95), (sx * 0.08, 0.95)])], L))
        parts.append(bx(0.3, 0.26, 0.18, D, (sx * 0.7, 2.45, 1.0), bevel=0.03))
        parts.append(bx(0.22, 0.03, 0.09, m['glass'], (sx * 0.7, 2.59, 1.01)))
        parts += headlight(m, (sx * 1.05, 3.0, 0.72), guard=False, s=0.8)
        parts.append(bx(0.12, 0.14, 0.12, D, (sx * 0.5, 3.22, 0.62)))
    parts.append(bx(0.48, 0.48, 0.06, D, (-0.55, 1.15, 1.43), bevel=0.02))
    parts += [bx(0.11, 0.06, 0.07, m['glass'], (-0.55 + dx, 1.42, 1.45)) for dx in (-0.14, 0.0, 0.14)]
    # the engine deck: a long grille of slats each side of a central spine, exhausts at the back
    for sx in (-1, 1):
        parts.append(bx(0.85, 1.6, 0.04, D, (sx * 0.62, -2.15, 1.34)))
        parts += slats(sx * 0.62 - 0.38, sx * 0.62 + 0.38, -2.85, -1.45, 1.38, 8, m['steel_dark'], along='x', w=0.06, h=0.04)
        parts.append(bx(0.5, 0.1, 0.26, m['steel_dark'], (sx * 0.6, -3.18, 0.95)))
        parts += [bx(0.46, 0.04, 0.03, D, (sx * 0.6, -3.24, 0.86 + k * 0.08)) for k in range(3)]
        parts.append(bx(0.14, 0.05, 0.1, m['lamp_red'], (sx * 1.2, -3.13, 1.02)))
    parts.append(bx(0.22, 1.7, 0.1, D, (0, -2.15, 1.38)))
    # skirts over the tracks: five bolted plates a side, a civ stripe on the front plates
    for sx in (-1, 1):
        for y0, y1 in ((-3.0, -1.85), (-1.8, -0.65), (-0.6, 0.55), (0.6, 1.75), (1.8, 2.75)):
            parts.append(bx(0.07, y1 - y0, 0.42, P, (sx * 1.56, (y0 + y1) / 2, 0.8)))
            parts += side_bolts(sx * 1.6, y0 + 0.1, y1 - 0.1, 0.98, 4, D, r=0.025)
        parts.append(bx(0.04, 1.7, 0.16, m['decal'], (sx * 1.605, 0.75, 0.72)))
        parts.append(bx(0.06, 5.75, 0.05, D, (sx * 1.56, -0.12, 1.02)))
        # a tow cable along the hull side
        parts.append(VP.tube((sx * 1.4, -2.6, 1.2), (sx * 1.4, 0.4, 1.2), 0.035, 0.035, m['steel_dark'], seg=6))
    hull_mesh = M.Mesh.concat(parts)
    wheels = [(y, 0.3, 0.29) for y in (-2.45, -1.7, -0.95, -0.2, 0.55, 1.3, 2.05)]
    tracks = [Track2(wheels, 0.6, sx * 1.22, sprocket=(-2.95, 0.5, 0.3), idler=(2.75, 0.5, 0.28),
                     rollers=[(-1.3, 0.8, 0.08), (0.95, 0.8, 0.08)],
                     link=0.19, visible_wheels=True, top=[(0.0, 0.8, 0.08)]) for sx in (-1, 1)]

    # the turret: low and wide, faceted; its roof broken by raised plates, a
    # hatch, a sight head, a vent grille and a stowage bin
    tsec = lambda w, z0, z1, wt: [(-w, z0), (w, z0), (w, z1 - 0.15), (wt, z1), (-wt, z1), (-w, z1 - 0.15)]
    tur = VP.sweep([
        (-1.6, tsec(1.0, 0.0, 0.5, 0.82)),
        (0.7, tsec(1.18, 0.0, 0.56, 1.0)),
        (1.4, tsec(0.7, 0.04, 0.46, 0.55)),
    ], P)
    tur = VP.camo(tur, P, [(0.38, m['paint_mid'])], max_edge=0.3, seed=72, scale=0.42)
    tp = [tur]
    for sx in (-1, 1):
        tp.append(bx(0.05, 1.4, 0.3, m['decal'], (sx * 1.17, -0.35, 0.26)))
        tp += side_bolts(sx * 1.2, -1.4, -0.9, 0.18, 2, D, r=0.025)
        tp += side_bolts(sx * 1.2, 0.2, 0.6, 0.18, 2, D, r=0.025)
        tp.append(smoke_cluster(m, D, (sx * 1.0, 0.65, 0.42), yaw=-sx * 25, n=3))
        tp.append(bx(0.7, 0.9, 0.05, L, (sx * 0.55, -0.85, 0.58)))
    tp.append(M.cylinder(0.24, 0.24, 0.06, D, seg=12, z0=0.56).transformed(M.translate(-0.62, 0.15, 0)))
    tp.append(bx(0.34, 0.3, 0.22, D, (0.68, 0.25, 0.68), bevel=0.03))
    tp.append(bx(0.26, 0.03, 0.1, m['glass'], (0.68, 0.41, 0.7)))
    tp.append(bx(0.8, 0.55, 0.04, m['steel_dark'], (0, -1.3, 0.57)))
    tp += slats(-0.36, 0.36, -1.52, -1.08, 0.6, 5, D, along='x', w=0.05, h=0.03)
    tp.append(bx(1.7, 0.5, 0.36, D, (0, -1.85, 0.22), bevel=0.04))
    tp += [bx(0.04, 0.52, 0.38, m['steel_dark'], (x, -1.85, 0.22)) for x in (-0.5, 0.5)]
    tp.append(antenna(m['steel'], D, (0.85, -1.5, 0.5), 0.9))
    # the pylon that carries the emitter, and the cable bundles into it
    tp.append(bx(0.5, 0.9, 0.34, D, (0, 0.1, 0.68), bevel=0.04))
    for sx in (-1, 1):
        tp.append(VP.tube((sx * 0.32, -0.6, 0.6), (sx * 0.22, 0.0, 0.85), 0.05, 0.05, m['steel_dark'], seg=6))
    turret = M.Mesh.concat(tp)

    # the emitter: a long chamfered housing, cooling fins along its top and
    # flanks, capacitor drums at its root, a ringed barrel, a civ collar, the lens
    hsec = [(-0.3, -0.22), (0.3, -0.22), (0.34, -0.1), (0.34, 0.14), (0.24, 0.26), (-0.24, 0.26), (-0.34, 0.14), (-0.34, -0.1)]
    hous = VP.sweep([(-1.1, [(x * 0.9, z * 0.9) for x, z in hsec]), (-0.95, hsec), (2.0, hsec),
                     (2.25, [(x * 0.8, z * 0.8) for x, z in hsec])], P)
    g = [hous]
    for i in range(11):
        y = -0.75 + i * 0.25
        g.append(bx(0.5, 0.05, 0.14, m['steel'], (0, y, 0.32)))
        for sx in (-1, 1):
            g.append(bx(0.12, 0.05, 0.26, m['steel'], (sx * 0.4, y, 0.02)))
    g.append(bx(0.72, 0.06, 0.06, D, (0, -0.85, 0.3)))
    for sx in (-1, 1):
        g.append(VP.tube((sx * 0.5, -1.0, -0.12), (sx * 0.5, -0.2, -0.12), 0.13, 0.13, D, seg=12))
        g.append(VP.tube((sx * 0.5, -1.04, -0.12), (sx * 0.5, -0.98, -0.12), 0.14, 0.14, m['steel_dark'], seg=12))
    g.append(VP.tube((0, 2.2, 0.02), (0, 2.75, 0.02), 0.17, 0.15, D, seg=14))
    for y in (2.3, 2.45, 2.6):
        g.append(VP.tube((0, y, 0.02), (0, y + 0.05, 0.02), 0.22, 0.22, m['steel'], seg=14))
    g.append(VP.tube((0, 2.75, 0.02), (0, 2.85, 0.02), 0.24, 0.24, m['decal'], seg=14))
    g.append(VP.tube((0, 2.85, 0.02), (0, 2.9, 0.02), 0.2, 0.2, m['steel_dark'], seg=14))
    g.append(VP.tube((0, 2.88, 0.02), (0, 2.93, 0.02), 0.14, 0.14, m['fx_beam'], seg=12))
    gun = M.Mesh.concat(g)
    return dict(hull=hull_mesh, tracks=tracks, turret=turret, turret_pos=(0, -0.35, 1.34), gun=gun,
                gun_pos=(0, 0.35, 1.02), muzzle=(0, 2.93, 0.02), recoil=0.0, scan=40, laser=True,
                blast=(0, -0.3, 2.0))


# ------------------------------------------------------------ the Bradley

def build_light_ifv(Vh):
    m = Vh.mats
    P, D = m['paint'], m['paint_dark']
    parts = []
    # hull: slab sides, a steep upper glacis down to a lower nose plate
    sec = lambda zt, wt: [(-1.05, 0.44), (1.05, 0.44), (1.05, 0.58), (1.45, 0.58), (1.45, zt - 0.22), (wt, zt), (-wt, zt), (-1.45, zt - 0.22), (-1.45, 0.58), (-1.05, 0.58)]
    hull = VP.sweep([
        (-3.0, sec(1.78, 1.32)),
        (1.45, sec(1.8, 1.32)),
        (2.55, sec(1.3, 1.32)),
        (3.05, [(-1.05, 0.58), (1.05, 0.58), (1.05, 0.62), (1.42, 0.62), (1.42, 0.92), (1.3, 1.05), (-1.3, 1.05), (-1.42, 0.92), (-1.42, 0.62), (-1.05, 0.62)]),
    ], P)
    hull = VP.camo(hull, P, [(0.25, D), (0.8, m['paint_black'])], max_edge=0.22, seed=81, scale=0.55)
    parts.append(hull)
    # the trim vane folded on the nose (a ribbed plate) and tow shackles
    parts.append(VP.sweep([(2.62, [(-1.2, 0.68), (1.2, 0.68), (1.2, 1.2), (-1.2, 1.2)]),
                           (2.72, [(-1.2, 0.68), (1.2, 0.68), (1.2, 1.15), (-1.2, 1.15)])], D)
                 .transformed(M.translate(0, 0.12, 0)))
    parts += [bx(2.3, 0.05, 0.04, P, (0, 2.86, 0.76 + k * 0.12)) for k in range(4)]
    for sx in (-1, 1):
        parts.append(VP.ring_x(0.08, 0.04, 0.05, m['steel_dark'], center=(sx * 0.7, 3.06, 0.66)))
        parts += headlight(m, (sx * 1.18, 2.7, 1.32), guard=True)
    # the driver's hatch (front left) with periscopes; the engine grille (front right)
    parts.append(M.cylinder(0.27, 0.27, 0.07, D, seg=12, z0=1.8).transformed(M.translate(-0.75, 1.35, 0)))
    parts += [bx(0.14, 0.08, 0.08, m['glass'], (-0.75 + 0.26 * math.cos(a), 1.35 + 0.26 * math.sin(a), 1.83))
              for a in (math.radians(v) for v in (30, 70, 110, 150))]
    parts.append(bx(0.95, 0.85, 0.04, D, (0.6, 1.7, 1.8)))
    parts += slats(0.15, 1.05, 1.32, 2.08, 1.83, 6, m['steel_dark'], along='x', w=0.06, h=0.04)
    # the exhaust on the right flank, a cargo hatch on the roof behind the turret
    parts.append(bx(0.14, 0.5, 0.3, m['steel_dark'], (1.5, 1.85, 1.45)))
    parts.append(bx(1.5, 1.2, 0.06, D, (0, -2.1, 1.8), bevel=0.02))
    parts += [VP.axle_x(0.05, 0.22, m['steel_dark'], center=(sx * 0.5, -1.5, 1.84)) for sx in (-1, 1)]
    # reactive armor tiles (BRAT): two rows of tiles on each side, a row on the nose
    for sx in (-1, 1):
        for z in (1.06, 1.42):
            for i in range(8):
                y = -2.55 + i * 0.58 + 0.29
                parts.append(bx(0.14, 0.52, 0.32, m['tile'], (sx * 1.53, y, z), bevel=0.025))
                parts.append(bx(0.02, 0.1, 0.06, m['steel_dark'], (sx * 1.61, y, z + 0.1)))
        # the skirt below the tiles, its bolted lower edge, a civ-colored flash
        parts.append(bx(0.07, 5.2, 0.24, D, (sx * 1.49, -0.25, 0.74)))
        parts += side_bolts(sx * 1.53, -2.7, 2.2, 0.7, 12, m['steel_dark'], r=0.022)
        parts.append(bx(0.04, 0.32, 0.18, m['decal'], (sx * 1.535, 2.35, 0.75)))
    for i in range(4):
        parts.append(bx(0.52, 0.12, 0.3, m['tile'], (-0.82 + i * 0.55, 2.25, 1.47), bevel=0.025).transformed(
            M.translate(0, 2.25, 1.47) @ M.rot_x(-24) @ M.translate(0, -2.25, -1.47)))
    # the rear: the ramp with its door outline and hinges, tail lights, bags
    parts.append(bx(1.7, 0.08, 1.12, D, (0, -3.04, 1.12)))
    parts.append(bx(0.6, 0.04, 0.8, P, (0.45, -3.09, 1.2)))
    parts += [bx(0.3, 0.06, 0.1, m['steel_dark'], (sx * 0.55, -3.06, 0.6)) for sx in (-1, 1)]
    parts += [bx(0.14, 0.04, 0.1, m['lamp_red'], (sx * 1.25, -3.02, 1.55)) for sx in (-1, 1)]
    for sx in (-1, 1):
        parts.append(bx(0.22, 0.5, 0.5, m['canvas'], (sx * 1.2, -3.05, 1.1), bevel=0.05))
        parts.append(bx(0.24, 0.05, 0.52, m['strap'], (sx * 1.2, -3.05, 1.1)))
    hull_mesh = M.Mesh.concat(parts)
    wheels = [(y, 0.32, 0.31) for y in (-2.15, -1.38, -0.61, 0.16, 0.93, 1.7)]
    tracks = [Track2(wheels, 0.56, sx * 1.2, sprocket=(2.55, 0.66, 0.3), idler=(-2.75, 0.52, 0.3),
                     rollers=[(-1.8, 0.86, 0.09), (-0.2, 0.86, 0.09), (1.3, 0.86, 0.09)],
                     link=0.19, visible_wheels=True, top=[(0.0, 0.86, 0.09)]) for sx in (-1, 1)]

    # the turret: angular, spaced armor on its faces, set to the right of center
    tsec = lambda w, z1, wt: [(-w, 0.0), (w, 0.0), (w, z1 - 0.12), (wt, z1), (-wt, z1), (-w, z1 - 0.12)]
    tur = VP.sweep([(-1.05, tsec(0.92, 0.6, 0.8)), (0.55, tsec(0.95, 0.62, 0.82)), (1.1, tsec(0.62, 0.48, 0.5))], P)
    tur = VP.camo(tur, P, [(0.25, D), (0.8, m['paint_black'])], max_edge=0.2, seed=82, scale=0.55)
    tp = [tur]
    for sx in (-1, 1):
        tp.append(bx(0.06, 1.1, 0.42, P, (sx * 0.98, -0.25, 0.3)))
        tp += side_bolts(sx * 1.02, -0.7, 0.2, 0.46, 4, D, r=0.024)
    tp.append(smoke_cluster(m, D, (0.75, 0.95, 0.45), yaw=-30, n=4, r=0.05))
    tp.append(smoke_cluster(m, D, (-0.75, 0.95, 0.45), yaw=30, n=4, r=0.05))
    # the TOW launcher: a twin-tube box on an arm on the turret's left
    tp += [bx(0.46, 1.4, 0.42, m['tow'], (-1.25, -0.15, 0.42), bevel=0.04),
           bx(0.48, 0.08, 0.44, D, (-1.25, 0.58, 0.42)),
           bx(0.48, 0.08, 0.44, D, (-1.25, -0.88, 0.42)),
           bx(0.2, 0.4, 0.2, m['steel_dark'], (-1.03, -0.15, 0.3)),
           bx(0.04, 1.3, 0.36, m['decal'], (-1.49, -0.15, 0.42))]
    for dz in (-0.1, 0.1):
        tp.append(VP.axle_x(0.08, 0.06, m['steel_dark'], seg=10).transformed(M.translate(-1.25, 0.62, 0.42 + dz) @ M.rot_z(90)))
    # the gunner's sight with its armored doors, the commander's viewer (CIV)
    tp.append(bx(0.34, 0.42, 0.28, D, (-0.45, 0.35, 0.74), bevel=0.03))
    tp.append(bx(0.26, 0.04, 0.16, m['glass'], (-0.45, 0.57, 0.76)))
    tp += [bx(0.04, 0.3, 0.24, D, (-0.45 + sx * 0.19, 0.4, 0.76)) for sx in (-1, 1)]
    tp.append(bx(0.32, 0.36, 0.34, P, (0.55, -0.55, 0.78), bevel=0.04))
    tp.append(bx(0.24, 0.04, 0.14, m['glass'], (0.55, -0.36, 0.84)))
    tp.append(M.cylinder(0.24, 0.24, 0.05, D, seg=12, z0=0.62).transformed(M.translate(0.15, -0.2, 0)))
    # the bustle rack with bags and a rolled tarp, two antennas
    tp.append(bx(1.7, 0.5, 0.05, m['steel_dark'], (0, -1.32, 0.2)))
    tp += [bx(0.04, 0.04, 0.34, m['steel_dark'], (sx * 0.83, -1.55, 0.36)) for sx in (-1, 0, 1)]
    tp += bag(m, (-0.45, -1.3, 0.38), (0.6, 0.42, 0.32))
    tp += bag(m, (0.25, -1.32, 0.36), (0.55, 0.4, 0.28), mat='canvas_dk')
    tp.append(VP.axle_x(0.12, 1.4, m['canvas'], seg=10, center=(0, -1.48, 0.6)))
    tp.append(antenna(m['steel'], D, (0.7, -1.0, 0.62), 0.6))
    tp.append(antenna(m['steel'], D, (-0.7, -1.0, 0.62), 0.5))
    turret = M.Mesh.concat(tp).transformed(M.scale(1.12))
    # the 25 mm Bushmaster: mantlet, barrel jacket, long thin barrel, muzzle brake; the coax beside it
    gun = M.Mesh.concat([bx(0.36, 0.3, 0.34, P, (0, 0.0, 0), bevel=0.03),
                         VP.tube((0, 0.1, 0), (0, 0.65, 0), 0.085, 0.08, D, seg=10),
                         VP.tube((0, 0.6, 0), (0, 1.85, 0), 0.045, 0.042, m['steel'], seg=8),
                         VP.tube((0, 1.74, 0), (0, 1.94, 0), 0.065, 0.065, m['steel_dark'], seg=8),
                         VP.tube((0.2, 0.1, -0.05), (0.2, 0.6, -0.05), 0.03, 0.03, m['steel'], seg=6)])
    return dict(hull=hull_mesh, tracks=tracks, turret=turret, turret_pos=(0.25, 0.2, 1.8), gun=gun.transformed(M.scale(1.12)),
                gun_pos=(0, 1.1, 0.4), muzzle=(0, 1.94, 0), recoil=0.12, scan=40, burst=True)


# ------------------------------------------------------------ the Humvee

def _gunner(m):
    """A gunner standing in the roof ring (turret space, facing +y): torso
    in a plate carrier, arms to the gun's spade grips, a head in a
    civ-colored helmet with goggles."""
    parts = [M.capsule((0, -0.22, -0.1), (0, -0.2, 0.45), 0.21, 0.23, m['uniform'], seg=10),
             bx(0.44, 0.34, 0.36, m['vest'], (0, -0.18, 0.3), bevel=0.05),
             bx(0.14, 0.06, 0.12, m['vest_dk'], (-0.1, -0.0, 0.26)),
             bx(0.14, 0.06, 0.12, m['vest_dk'], (0.1, -0.0, 0.26)),
             M.sphere(0.12, m['skin'], center=(0, -0.15, 0.63), seg=10),
             M.lathe([(0.0, 0.165), (0.05, 0.168), (0.12, 0.14), (0.18, 0.08), (0.205, 0.0)], m['helmet'], seg=12)
             .transformed(M.translate(0, -0.17, 0.64)),
             bx(0.2, 0.05, 0.05, m['steel_dark'], (0, -0.04, 0.66))]
    for sx in (-1, 1):
        parts.append(M.capsule((sx * 0.21, -0.18, 0.44), (sx * 0.16, 0.05, 0.25), 0.065, 0.06, m['uniform'], seg=8))
        parts.append(M.capsule((sx * 0.16, 0.05, 0.25), (sx * 0.1, 0.25, 0.32), 0.055, 0.05, m['uniform'], seg=8))
        parts.append(M.sphere(0.05, m['glove'], center=(sx * 0.09, 0.28, 0.33), seg=8))
    return M.Mesh.concat(parts)


def build_humvee(Vh):
    m = Vh.mats
    P, D = m['paint'], m['paint_dark']
    camo = lambda mesh, seed: VP.camo(mesh, P, [(0.25, m['paint_brown']), (0.75, m['paint_black'])], max_edge=0.2, seed=seed, scale=0.6)
    parts = []
    # the body tub between the wheels and the wide fenders over them
    tub = VP.sweep([(-2.4, [(-0.95, 0.6), (0.95, 0.6), (1.0, 1.15), (-1.0, 1.15)]),
                    (2.25, [(-0.95, 0.6), (0.95, 0.6), (1.0, 1.15), (-1.0, 1.15)]),
                    (2.45, [(-0.88, 0.66), (0.88, 0.66), (0.92, 1.1), (-0.92, 1.1)])], P)
    fend = []
    for sx in (-1, 1):
        fend.append(VP.sweep([(0.75, [(sx * 0.8, 0.98), (sx * 1.12, 0.98), (sx * 1.12, 1.3), (sx * 0.8, 1.3)]),
                              (2.3, [(sx * 0.8, 0.98), (sx * 1.12, 0.98), (sx * 1.12, 1.22), (sx * 0.8, 1.22)])], P))
        fend.append(VP.sweep([(-2.3, [(sx * 0.85, 0.98), (sx * 1.12, 0.98), (sx * 1.12, 1.2), (sx * 0.85, 1.2)]),
                              (-0.75, [(sx * 0.85, 0.98), (sx * 1.12, 0.98), (sx * 1.12, 1.2), (sx * 0.85, 1.2)])], P))
        fend.append(bx(0.22, 1.5, 0.5, P, (sx * 1.0, 0.0, 0.85)))
    hood = VP.sweep([(0.6, [(-0.82, 1.15), (0.82, 1.15), (0.82, 1.38), (0.0, 1.42), (-0.82, 1.38)]),
                     (2.3, [(-0.82, 1.1), (0.82, 1.1), (0.82, 1.28), (0.0, 1.31), (-0.82, 1.28)])], P)
    cab = VP.sweep([(-1.05, [(-1.08, 1.15), (1.08, 1.15), (1.02, 1.98), (-1.02, 1.98)]),
                    (0.45, [(-1.08, 1.15), (1.08, 1.15), (1.02, 1.98), (-1.02, 1.98)]),
                    (0.75, [(-1.08, 1.15), (1.08, 1.15), (1.0, 1.9), (-1.0, 1.9)])], P)
    parts.append(camo(M.Mesh.concat([tub, hood, cab] + fend), 51))
    # the grille: dark panel with seven vertical slots, round lamps in square bezels
    parts.append(bx(1.5, 0.05, 0.42, D, (0, 2.46, 0.9)))
    parts += [bx(0.07, 0.04, 0.34, m['steel_dark'], (-0.42 + i * 0.14, 2.49, 0.9)) for i in range(7)]
    for sx in (-1, 1):
        parts.append(bx(0.26, 0.06, 0.24, D, (sx * 0.72, 2.47, 0.98)))
        parts.append(VP.axle_x(0.08, 0.04, m['light'], seg=10).transformed(M.translate(sx * 0.72, 2.5, 0.98) @ M.rot_z(90)))
        parts.append(bx(0.1, 0.04, 0.06, m['lamp_amber'], (sx * 0.95, 2.36, 1.2)))
    # brush guard and bumper, winch and tow shackles
    parts.append(bx(2.0, 0.22, 0.2, m['steel_dark'], (0, 2.6, 0.6)))
    parts.append(bx(0.5, 0.25, 0.22, D, (0, 2.62, 0.78)))
    parts += [VP.ring_x(0.07, 0.035, 0.05, m['steel'], center=(sx * 0.65, 2.75, 0.6)) for sx in (-1, 1)]
    for sx in (-1, 1):
        parts.append(VP.tube((sx * 0.85, 2.62, 0.68), (sx * 0.85, 2.62, 1.22), 0.035, 0.035, m['steel_dark'], seg=6))
    parts.append(VP.tube((-0.85, 2.62, 1.2), (0.85, 2.62, 1.2), 0.035, 0.035, m['steel_dark'], seg=6))
    # a civ-colored marker panel on the hood, hood latches
    parts.append(bx(0.52, 0.36, 0.025, m['decal'], (0, 1.6, 1.41)))
    parts += [bx(0.04, 0.42, 0.035, m['strap'], (x, 1.6, 1.415)) for x in (-0.16, 0.16)]   # its tie-downs
    parts += [bx(0.04, 0.12, 0.08, m['steel_dark'], (sx * 0.85, 2.25, 1.22)) for sx in (-1, 1)]
    # windshield (two thick panes in frames), side windows in the doors, mirrors
    for sx in (-1, 1):
        parts.append(bx(0.86, 0.1, 0.48, D, (sx * 0.5, 0.74, 1.6)))
        parts.append(bx(0.74, 0.05, 0.38, m['glass'], (sx * 0.5, 0.8, 1.62)))
        for y in (0.0, -0.85):
            parts.append(bx(0.06, 0.6, 0.42, D, (sx * 1.07, y + 0.05, 1.62)))
            parts.append(bx(0.04, 0.48, 0.32, m['glass'], (sx * 1.1, y + 0.05, 1.64)))
            parts.append(bx(0.05, 0.14, 0.05, m['steel_dark'], (sx * 1.1, y - 0.25, 1.32)))
        parts += [bx(0.05, 0.03, 0.75, D, (sx * 1.09, y, 1.4)) for y in (0.45, -0.42, -1.04)]
        parts.append(bx(0.07, 0.12, 0.12, m['steel_dark'], (sx * 1.15, 0.55, 1.72)))
        parts.append(bx(0.05, 0.08, 0.24, m['steel_dark'], (sx * 1.24, 0.58, 1.8)))
    # the cargo deck behind the cab: low sides, tailgate, jerrycans, ammo boxes, a bedroll
    parts.append(bx(2.0, 1.3, 0.06, D, (0, -1.72, 1.16)))
    parts += [bx(0.08, 1.3, 0.25, P, (sx * 1.0, -1.72, 1.28)) for sx in (-1, 1)]
    parts.append(bx(2.08, 0.1, 0.32, P, (0, -2.4, 1.22)))
    parts += [bx(0.12, 0.05, 0.1, m['lamp_red'], (sx * 0.85, -2.46, 1.05)) for sx in (-1, 1)]
    parts += [jerrycan(m, (-0.7 + i * 0.22, -2.2, 1.18)) for i in range(3)]
    parts.append(bx(0.5, 0.3, 0.26, m['ammo'], (0.45, -2.15, 1.32), bevel=0.02))
    parts.append(bx(0.52, 0.06, 0.04, m['steel_dark'], (0.45, -2.15, 1.46)))
    parts.append(bx(0.5, 0.3, 0.26, m['ammo'], (0.45, -1.8, 1.32), bevel=0.02))
    parts += bag(m, (0.05, -1.35, 1.38), (1.7, 0.42, 0.38), straps=3)
        # one whip on the right rear corner, bent back and tied down low
    parts.append(M.cylinder(0.07, 0.06, 0.18, m['steel_dark'], seg=8, z0=1.15).transformed(M.translate(0.95, -2.3, 0)))
    parts.append(VP.tube((0.95, -2.3, 1.3), (0.95, -2.5, 1.7), 0.016, 0.014, m['steel_dark'], seg=4))
    parts.append(VP.tube((0.95, -2.5, 1.7), (0.93, -3.2, 1.85), 0.014, 0.01, m['steel_dark'], seg=4))
    # the chassis frame and axles seen under the body
    parts += [bx(0.14, 4.6, 0.16, m['steel_dark'], (sx * 0.5, 0.0, 0.5)) for sx in (-1, 1)]
    parts += [bx(1.7, 0.2, 0.2, m['steel_dark'], (0, y, 0.48)) for y in (-1.55, 1.5)]
    hull_mesh = M.Mesh.concat(parts)
    wh = [(-1.55, 0.48, 0.48), (1.5, 0.48, 0.48)]
    tracks = [Wheels(wh, 0.38, sx * 0.93) for sx in (-1, 1)]

    # the O-GPK turret on the roof ring: a faceted front shield with
    # ballistic windows, side wings, and the gunner
    tp = [M.lathe([(0.0, 0.58), (0.06, 0.6), (0.12, 0.56), (0.12, 0.48)], D, seg=18)]
    for yaw, w, xo in ((0, 0.7, 0.0), (35, 0.42, 0.52), (-35, 0.42, -0.52)):
        pane = M.Mesh.concat([bx(w, 0.06, 0.62, P, (0, 0, 0.44), bevel=0.01),
                              bx(w * 0.62, 0.05, 0.26, D, (0, 0.03, 0.55)),
                              bx(w * 0.55, 0.04, 0.2, m['glass'], (0, 0.045, 0.55))])
        yy = 0.55 - abs(xo) * 0.45
        tp.append(pane.transformed(M.translate(xo, yy, 0.05) @ M.rot_z(yaw)))
    for sx in (-1, 1):
        tp.append(bx(0.06, 0.62, 0.48, P, (0, 0, 0.36)).transformed(
            M.translate(sx * 0.7, -0.05, 0) @ M.rot_z(sx * -12)))
    tp.append(_gunner(m))
    turret = M.Mesh.concat(tp)
    # the M2: receiver, barrel jacket, barrel and flash hider, spade grips,
    # an ammo can on the left, on a pintle
    gun = M.Mesh.concat([bx(0.19, 0.6, 0.2, m['steel'], (0, -0.05, 0)),
                         VP.tube((0, 0.22, 0), (0, 0.65, 0), 0.068, 0.068, m['steel_dark'], seg=8),
                         VP.tube((0, 0.6, 0), (0, 1.25, 0), 0.04, 0.038, m['steel'], seg=8),
                         VP.tube((0, 1.2, 0), (0, 1.34, 0), 0.055, 0.05, m['steel_dark'], seg=8),
                         bx(0.18, 0.06, 0.1, m['steel_dark'], (0, -0.34, 0.0)),
                         bx(0.2, 0.25, 0.18, m['ammo'], (-0.18, 0.0, -0.06)),
                         VP.tube((0, -0.05, -0.08), (0, -0.05, -0.38), 0.04, 0.04, m['steel_dark'], seg=6)])
    return dict(hull=hull_mesh, tracks=tracks, turret=turret, turret_pos=(0, -0.45, 1.98), gun=gun,
                gun_pos=(0, 0.3, 0.52), muzzle=(0, 1.32, 0), recoil=0.05, scan=50, burst=True,
                wheeled=True, blast=(0, 0.0, 1.5))


# ------------------------------------------------------------ the Iron Dome launcher truck

def interceptor(m, length=2.4, r=0.08):
    """A Tamir: a slim white body, a dark seeker nose, four canards near the
    front and four tail fins, along +y from 0."""
    body = VP.tube((0, 0, 0), (0, length * 0.84, 0), r, r, m['missile'], seg=8)
    nose = VP.tube((0, length * 0.84, 0), (0, length, 0), r, 0.015, m['steel_dark'], seg=8)
    fins = []
    for a in (45, 135, 225, 315):
        fins.append(M.box(0.015, 0.3, r * 3.0, m['steel_dark']).transformed(M.rot_y(a) @ M.translate(0, 0.17, r * 1.2)))
        fins.append(M.box(0.012, 0.14, r * 2.2, m['steel_dark']).transformed(M.rot_y(a) @ M.translate(0, length * 0.72, r * 0.9)))
    return M.Mesh.concat([body, nose] + fins)


IRON_PIVOT = (0.0, -3.0, 1.5)      # the launcher box's hinge (model space)


def build_battery(Vh):
    m = Vh.mats
    P, D = m['paint'], m['paint_dark']
    parts = []
    # the chassis: two rails, a deck, fuel tanks, battery boxes, mudguards
    parts += [bx(0.18, 6.6, 0.3, m['steel_dark'], (sx * 0.55, -0.2, 0.92)) for sx in (-1, 1)]
    parts.append(bx(2.3, 4.3, 0.12, P, (0, -1.45, 1.13)))
    parts += [bx(2.36, 0.08, 0.16, D, (0, y, 1.12)) for y in (-3.6, 0.7)]
    parts += [bx(0.06, 4.3, 0.16, D, (sx * 1.16, -1.45, 1.12)) for sx in (-1, 1)]
    for sx in (-1, 1):
        parts.append(VP.axle_x(0.26, 0.9, D, seg=12).transformed(M.translate(sx * 0.9, 0.9, 0.82) @ M.rot_z(90)))
        parts.append(bx(0.5, 0.6, 0.45, m['steel_dark'], (sx * 0.95, 1.7, 0.85)))
        parts += [bx(0.62, 0.8, 0.05, m['steel_dark'], (sx * 0.95, y, 1.2)) for y in (2.75, -1.25, -2.65)]
    # the cab, cab-over: a tall box with a chamfered roof edge, a two-pane
    # windscreen in a frame, a barred grille, doors with windows and steps
    cab = VP.sweep([(1.55, [(-1.2, 1.0), (1.2, 1.0), (1.2, 2.75), (-1.2, 2.75)]),
                    (3.4, [(-1.2, 1.0), (1.2, 1.0), (1.2, 2.75), (-1.2, 2.75)]),
                    (3.65, [(-1.2, 1.0), (1.2, 1.0), (1.2, 2.55), (-1.2, 2.55)])], P)
    cab = VP.camo(cab, P, [(0.45, m['paint_mid'])], max_edge=0.3, seed=91, scale=0.45)
    parts.append(cab)
    parts.append(bx(2.2, 0.05, 0.66, D, (0, 3.66, 2.12)))
    parts += [bx(1.0, 0.05, 0.52, m['glass'], (sx * 0.53, 3.68, 2.13)) for sx in (-1, 1)]
    parts += [bx(0.05, 0.03, 0.4, m['steel_dark'], (sx * 0.45, 3.71, 2.05)).transformed(
        M.translate(sx * 0.45, 3.71, 1.88) @ M.rot_y(sx * 35) @ M.translate(-sx * 0.45, -3.71, -1.88)) for sx in (-1, 1)]
    parts.append(bx(1.5, 0.05, 0.45, D, (0, 3.66, 1.52)))
    parts += [bx(1.4, 0.04, 0.05, m['steel_dark'], (0, 3.69, 1.36 + k * 0.1)) for k in range(4)]
    parts.append(bx(2.4, 0.28, 0.3, m['steel_dark'], (0, 3.72, 1.02)))
    parts += [bx(0.12, 0.04, 0.18, m['steel'], (sx * 0.2, 3.87, 0.98)) for sx in (-1, 1)]
    for sx in (-1, 1):
        parts += headlight(m, (sx * 0.95, 3.82, 1.08), guard=False)
        parts.append(bx(0.04, 0.75, 0.48, m['glass'], (sx * 1.21, 2.95, 2.3)))
        parts.append(bx(0.04, 0.95, 0.04, D, (sx * 1.215, 2.85, 2.58)))
        parts.append(bx(0.04, 0.04, 1.55, D, (sx * 1.215, 2.37, 1.8)))
        parts.append(bx(0.04, 0.12, 0.05, m['steel_dark'], (sx * 1.25, 2.5, 1.75)))
        parts += [bx(0.3, 0.35, 0.05, m['steel_dark'], (sx * 1.15, 3.0, z)) for z in (0.95, 1.3)]
        parts.append(bx(0.05, 0.05, 0.5, m['steel_dark'], (sx * 1.32, 3.5, 2.3)))
        parts.append(bx(0.06, 0.16, 0.38, m['steel_dark'], (sx * 1.42, 3.48, 2.38)))
        parts.append(bx(0.04, 0.55, 0.42, m['decal'], (sx * 1.215, 2.95, 1.7)))
    parts.append(bx(0.7, 0.7, 0.08, D, (-0.3, 2.3, 2.78)))
    # the spare wheel on the cab's back wall
    parts.append(VP.axle_x(0.5, 0.36, m['tire'], seg=20).transformed(M.translate(-0.45, 1.42, 1.95) @ M.rot_z(90)))
    parts.append(VP.axle_x(0.3, 0.4, m['rim'], seg=14).transformed(M.translate(-0.45, 1.42, 1.95) @ M.rot_z(90)))
    parts.append(VP.axle_x(0.1, 0.44, m['wheel_hub'], seg=8).transformed(M.translate(-0.45, 1.42, 1.95) @ M.rot_z(90)))
    for sx in (-1, 1):
        parts += [bx(0.045, 0.03, 1.4, D, (sx * 1.22, y, 1.72)) for y in (2.4, 3.42)]
    parts.append(bx(2.0, 0.12, 0.1, m['steel_dark'], (0, 3.3, 2.8)))
    parts += [bx(0.18, 0.1, 0.1, m['lamp_amber'], (sx * 0.7, 3.3, 2.88)) for sx in (-1, 1)]
    # behind the cab: a low generator set with louvred sides and a cable drum
    parts.append(bx(1.9, 0.68, 0.62, m['paint_dark'], (0.2, 0.88, 1.5), bevel=0.03))
    parts += [bx(0.04, 0.5, 0.04, m['steel_dark'], (0.2 + sx * 0.96, 0.88, 1.35 + k * 0.09)) for sx in (-1, 1) for k in range(4)]
    parts.append(bx(1.7, 0.5, 0.04, m['steel_dark'], (0.2, 0.88, 1.82)))
    parts.append(VP.tube((0.85, 0.95, 1.8), (0.85, 0.95, 2.2), 0.06, 0.06, m['steel_dark'], seg=8))
    parts.append(VP.axle_x(0.3, 0.7, m['cable'], seg=14, center=(0.0, 0.15, 1.5)))
    parts += [VP.axle_x(0.36, 0.05, m['steel_dark'], seg=14, center=(sx * 0.37, 0.15, 1.5)) for sx in (-1, 1)]
    # the launcher's base frame: the hinge brackets at the rear, the cradle in front
    for sx in (-1, 1):
        parts.append(bx(0.2, 0.6, 0.45, m['steel_dark'], (sx * 0.62, IRON_PIVOT[1] + 0.1, 1.38)))
        parts.append(bx(0.14, 0.12, 0.4, m['steel_dark'], (sx * 0.62, -0.55, 1.38)))
    parts.append(bx(1.4, 0.12, 0.1, m['steel_dark'], (0, -0.55, 1.55)))
    parts += [bx(0.12, 0.04, 0.12, m['lamp_red'], (sx * 1.0, -3.65, 1.0)) for sx in (-1, 1)]
    hull_mesh = M.Mesh.concat(parts)
    wh = [(-2.65, 0.55, 0.55), (-1.25, 0.55, 0.55), (2.75, 0.55, 0.55)]
    tracks = [Wheels(wh, 0.42, sx * 0.98, rim_k=0.58) for sx in (-1, 1)]

    # the launcher box (gun space: hinge at the origin, along +y): 4 x 5 cells,
    # frame rails, the twenty cell covers on its top, civ-colored side bands
    Lb, Wb, Hb = 3.5, 1.5, 1.2
    box = bx(Wb, Lb, Hb, P, (0, Lb / 2, Hb / 2 + 0.05), bevel=0.03)
    box = VP.camo(box, P, [(0.45, m['paint_mid'])], max_edge=0.3, seed=92, scale=0.45)
    g = [box]
    for sx in (-1, 1):
        g += [bx(0.06, Lb + 0.04, 0.1, D, (sx * (Wb / 2 + 0.02), Lb / 2, z)) for z in (0.12, Hb - 0.02)]
        g += [bx(0.06, 0.1, Hb, D, (sx * (Wb / 2 + 0.02), y, Hb / 2 + 0.05)) for y in (0.3, 1.3, 2.3, 3.3)]
        g.append(bx(0.04, 1.2, 0.3, m['decal'], (sx * (Wb / 2 + 0.03), 1.85, Hb * 0.62)))
        g.append(bx(0.07, 0.4, 0.3, m['steel_dark'], (sx * (Wb / 2 + 0.05), 0.3, 0.3)))
        g.append(bx(0.03, 0.7, 0.5, D, (sx * (Wb / 2 + 0.015), 0.8, 0.75)))            # an access door
        g.append(bx(0.04, 0.08, 0.12, m['steel_dark'], (sx * (Wb / 2 + 0.03), 1.08, 0.75)))
        g.append(bx(0.03, 0.55, 0.5, D, (sx * (Wb / 2 + 0.015), 2.85, 0.75)))
        g.append(VP.tube((sx * (Wb / 2 + 0.05), 0.2, Hb - 0.15), (sx * (Wb / 2 + 0.05), Lb - 0.2, Hb - 0.15), 0.04, 0.04,
                         m['cable'], seg=6))
    g.append(bx(Wb + 0.06, 0.08, Hb + 0.06, D, (0, Lb, Hb / 2 + 0.05)))
    cells = []
    for i in range(4):
        for j in range(5):
            x = -Wb / 2 + Wb * (i + 0.5) / 4
            z = 0.05 + Hb * (j + 0.5) / 5
            cells.append((x, Lb + 0.05, z))
            g.append(bx(Wb / 4 - 0.08, 0.05, Hb / 5 - 0.07, m['cover'], (x, Lb + 0.05, z)))
    g.append(bx(Wb, 0.1, Hb, D, (0, -0.03, Hb / 2 + 0.05)))
    # its back: two ribs across, a hatch with a handle, a junction box and its cable runs
    g += [bx(Wb + 0.04, 0.06, 0.07, m['steel_dark'], (0, -0.1, z)) for z in (0.35, 0.85)]
    g.append(bx(0.5, 0.04, 0.36, P, (-0.35, -0.1, 0.6)))
    g.append(bx(0.12, 0.05, 0.04, m['steel_dark'], (-0.35, -0.13, 0.66)))
    g.append(bx(0.34, 0.14, 0.26, m['steel_dark'], (0.4, -0.15, 0.6)))
    for dx in (0.32, 0.46):
        g.append(VP.tube((dx, -0.2, 0.47), (dx, -0.2, 0.12), 0.03, 0.03, m['cable'], seg=6))
        g.append(VP.tube((dx, -0.2, 0.12), (dx + 0.25, -0.2, 0.05), 0.03, 0.03, m['cable'], seg=6))
    # the box's under-frame: two rails and cross members on its belly
    g += [bx(0.12, Lb, 0.1, m['steel_dark'], (sx * 0.45, Lb / 2, 0.0)) for sx in (-1, 1)]
    g += [bx(Wb - 0.1, 0.1, 0.08, m['steel_dark'], (0, y, 0.0)) for y in (0.5, 1.6, 2.7)]
    gun = M.Mesh.concat(g)
    return dict(hull=hull_mesh, tracks=tracks, turret=M.Mesh(), turret_pos=IRON_PIVOT, gun=gun,
                gun_pos=(0, 0, 0), muzzle=(0, Lb, Hb / 2), recoil=0.0, scan=0, battery=True,
                rest_pitch=58, travel_pitch=7, tubes=cells, blast=(0, -1.0, 2.0), wheeled=True)


def battery_extras(Vh, G, H, deploy):
    """The parts of the launcher truck that follow the box and the jacks:
    two elevation rams from the deck to the box's belly, and four outrigger
    jacks (deploy 1 = feet on the ground, 0 = stowed up)."""
    m = Vh.mats
    out = []
    for sx in (-1, 1):
        a = (H @ np.array([sx * 0.45, -0.7, 1.25, 1.0], np.float32))[:3]
        b = (G @ np.array([sx * 0.45, 1.6, 0.1, 1.0], np.float32))[:3]
        mid = a + (b - a) * 0.55
        out.append(VP.tube(a, mid, 0.11, 0.11, m['paint_dark'], seg=10))
        out.append(VP.tube(mid - (b - a) * 0.05, b, 0.06, 0.06, m['steel'], seg=8))
    for sx in (-1, 1):
        for y in (-3.4, 1.6):
            top = (H @ np.array([sx * 1.15, y, 1.2, 1.0], np.float32))[:3]
            arm = (H @ np.array([sx * 1.55, y, 1.05, 1.0], np.float32))[:3]
            out.append(VP.bar(top, arm, 0.18, 0.18, m['paint_dark']))
            foot_z = 0.04 + (1 - deploy) * 0.6
            foot = np.array([arm[0], arm[1], foot_z], np.float32)
            out.append(VP.tube(arm, foot + np.array([0, 0, 0.1], np.float32), 0.08, 0.08, m['steel'], seg=8))
            out.append(M.cylinder(0.22, 0.24, 0.08, m['steel_dark'], seg=10, z0=foot_z - 0.04).transformed(
                M.translate(foot[0], foot[1], 0)))
    return M.Mesh.concat(out)


BUILDERS = {'laser_tank': build_laser_tank, 'light_ifv': build_light_ifv, 'humvee': build_humvee,
            'battery': build_battery}

PAINTS = {
    'laser_tank': {'paint': '#9e9789', 'paint_mid': '#837c70', 'paint_lt': '#aea799', 'paint_dark': '#5e5c56',
                   'track': '#33353a', 'track_link': '#585c62', 'tire': '#2a2b2e', 'wheel': '#6a7076',
                   'wheel_hub': '#2a2c2e', 'steel': ('#8a9096', 0.5, 24), 'steel_dark': '#3c4044',
                   'glass': ('#2e4a5c', 0.6, 40), 'light': '#e8eef0', 'lamp_red': '#9a2a20', 'decal': '#f0f0f0',
                   'lens_off': ('#26343e', 0.5, 30),
                   'fx_beam': '#eef8ff', 'beam': '#f4f4f4'},
    'light_ifv': {'paint': '#b4aa8e', 'paint_dark': '#8c8270', 'paint_black': '#6a6254', 'tile': '#aca286',
                  'track': '#2a2926', 'track_link': '#4c4944', 'tire': '#232220', 'wheel': '#5a5446',
                  'wheel_hub': '#24251f', 'steel': ('#5a5a58', 0.5, 24), 'steel_dark': '#2e302a',
                  'glass': ('#2c3a40', 0.6, 40), 'light': '#e6e2cc', 'lamp_red': '#9a2a20', 'tow': '#9a907a',
                  'canvas': '#7a6c52', 'canvas_dk': '#5a5040', 'strap': '#2e2c24', 'decal': '#f0f0f0'},
    'humvee': {'paint': '#beb28e', 'paint_dark': '#948c6c', 'paint_brown': '#a68c6a', 'paint_black': '#76705e',
               'tire': '#3c3c38', 'tire_dark': '#2a2a27', 'rim': '#4e5636', 'rim_dark': '#2e3222',
               'wheel_hub': '#262a1e', 'steel': ('#4a4a48', 0.5, 24), 'steel_dark': '#2a2c26',
               'glass': ('#33414a', 0.6, 40), 'light': '#e8e4d0', 'lamp_amber': '#c88a2a', 'lamp_red': '#9a2a20',
               'uniform': '#6e6a4e', 'vest': '#5a5a40', 'vest_dk': '#44442f', 'skin': '#c89a74', 'glove': '#3a3428',
               'helmet': '#f0f0f0', 'ammo': '#4e5634', 'can': '#4a5232', 'canvas': '#6e6a4c', 'strap': '#2e2c24',
               'decal': '#f0f0f0'},
    'battery': {'paint': '#a49c84', 'paint_mid': '#888270', 'paint_dark': '#6c6658', 'steel': ('#7a7c78', 0.5, 24),
                'steel_dark': '#34363a', 'tire': '#2a2a28', 'tire_dark': '#1a1a18', 'rim': '#6e6b52',
                'rim_dark': '#45432f', 'wheel_hub': '#2a2a24', 'glass': ('#2e3a44', 0.6, 40), 'light': '#e8e4d0',
                'lamp_amber': '#c88a2a', 'lamp_red': '#9a2a20', 'cable': '#2a2a26', 'cover': '#e4e2da',
                'missile': '#e8e6e0', 'decal': '#f0f0f0'},
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
        self.deploy = 1.0
        self.pitch_bias = 0.0
        self.lens_off = False
        self._n_model = None
        self.autofit()

    def render_cell(self, mesh, mats, cam, W, H):
        """The laser's beam (its 'beam'/'fx_beam' triangles outside the lens)
        is drawn in a pass of its own: no ground shadow, no contour lines,
        its edges softened; the rest as usual."""
        import render as R
        ids = [self.mats['beam'], self.mats['fx_beam']] if self.b.get('laser') else []
        sel = np.isin(mesh.M, ids) & self.beam_mask(mesh) if ids else None
        if sel is None or not sel.any():
            return R.render_cell(mesh, mats, cam, W, H)
        body = M.Mesh(mesh.V[~sel], mesh.N[~sel], mesh.M[~sel])
        beam = M.Mesh(mesh.V[sel], mesh.N[sel], mesh.M[sel])
        b0, t0 = R.render_cell(body, mats, cam, W, H)

        class Glow(R.Style):
            contour_alpha = 0.0
            inner_line = 0.0
            ssao = 0.0
            ground_ao = 0.0
        b1, t1 = R.render_cell(beam, mats, cam, W, H, style=Glow, shadow=False)
        b1, t1 = _soften(b1), _soften(t1)
        return _over(b1, b0), _over(t1, t0)

    def beam_mask(self, mesh):
        """Triangles of the beam itself, not of the lens: the lens is part
        of the model mesh (the first `self._n_model` triangles)."""
        n = getattr(self, '_n_model', None)
        out = np.zeros(len(mesh.M), bool)
        if n is not None:
            out[n:] = True
        return out

    def assemble(self, *a, **kw):
        if self.pitch_bias:
            kw['pitch'] = kw.get('pitch', 0.0) + self.pitch_bias
        mesh, G, H, T = super().assemble(*a, **kw)
        if self.lens_off:
            mesh = VP.recolor(mesh, {self.mats['fx_beam']: self.mats['lens_off']})
        if self.b.get('battery'):
            ex = battery_extras(self, G, H, self.deploy)
            if kw.get('char'):
                ex = VP.recolor(ex, self.charred())
            mesh = M.Mesh.concat([mesh, ex])
        return mesh, G, H, T

    def frame(self, action, f, n, ctx):
        b = self.b
        t = self.times(action, f, n)
        self.deploy, self.pitch_bias, self.lens_off = 1.0, 0.0, False
        self._n_model = None
        if b.get('laser') and action == 'DEATH':
            self.lens_off = self.times(action, f, n) >= 0.12
        if b.get('battery'):
            if action in ('RUN', 'WALK'):
                self.deploy = 0.0
                return self.drive(t, pitch=b['travel_pitch'])
            if action.startswith('ATTACK'):
                return self.attack_battery(t, ctx)
            if action == 'DEATH':
                # the box stays raised while the truck is jolted and burns
                self.pitch_bias = b['rest_pitch']
                return self.death(t, ctx)
            return self.battery_idle(t, action)
        if action == 'VICTORY' and b.get('laser'):
            return self.spin(t)
        if action in ('RUN', 'WALK') and b.get('wheeled'):
            return self.drive(t)
        if action.startswith('ATTACK') and b.get('laser'):
            return self.attack_laser(t, ctx)
        if action == 'VICTORY':
            # the turret (or the gunner) sweeps round and back
            return self.fidget(t)
        return super().frame(action, f, n, ctx)

    def loops(self, action):
        if action == 'VICTORY':
            return False
        return super().loops(action)

    def drive(self, t, pitch=0.0):
        """Wheels turn a quarter turn per loop (the tread blocks and bolts
        show it); the body bobs."""
        r = self.b['tracks'][0].wheels[0][2]
        phase = t * r * math.radians(90)
        s = math.sin(2 * math.pi * t * 2)
        mesh, *_ = self.assemble(phase=phase, pitch=pitch,
                                 body=(0.025 + 0.02 * s, 0.6 * math.cos(2 * math.pi * t), 0.4 * s, 0, 0))
        return FX.FxMesh(mesh, self.mats)

    def spin(self, t):
        """The turret turns a full circle and the emitter lifts in salute."""
        k = rig.smoothstep(t)
        mesh, *_ = self.assemble(yaw=360 * k, pitch=18 * math.sin(math.pi * t))
        return FX.FxMesh(mesh, self.mats)

    def battery_idle(self, t, action):
        """The box stands raised; in FORTIFY/FIDGET it nods a little."""
        amp = 3.0 if action in ('FORTIFY', 'FIDGET') else 0.0
        pitch = self.b['rest_pitch'] + amp * math.sin(2 * math.pi * t)
        w = math.sin(2 * math.pi * t * 2)
        mesh, *_ = self.assemble(pitch=pitch, body=(0.004 * w, 0.0, 0, 0, 0))
        return FX.FxMesh(mesh, self.mats)

    def attack_laser(self, t, ctx):
        """A civ-colored beam with a bright core: full at the shot, thinning
        away; the lens glows and the hull rocks a little."""
        b, mats = self.b, self.mats
        tf = self.p.get('fire', 0.08)
        dt = t - tf
        rock = -1.0 * math.exp(-max(dt, 0) * 6) * math.sin(max(dt, 0) * 20) if dt >= 0 else 0
        mesh, G, H, T = self.assemble(body=(0, rock, 0, 0, 0), pitch=2)
        self._n_model = len(mesh)
        fx = []
        if 0 <= dt < 0.85:
            k = 1.0 if dt < 0.45 else max(0.0, 1 - (dt - 0.45) / 0.4)
            length = self.p.get('beam', 2.6)
            p0 = (G @ np.array([*b['muzzle'], 1.0], np.float32))[:3]
            fwd = G[:3, :3] @ np.array([0, 1, 0], np.float32)
            r = 0.15 * (0.35 + 0.65 * k)
            # a civ-colored sheath tapering to a point, a white core inside it
            fx.append(VP.tube(p0, p0 + fwd * length * 0.75, r, r * 0.55, mats['beam'], seg=10))
            fx.append(VP.tube(p0 + fwd * length * 0.75, p0 + fwd * length, r * 0.55, 0.005, mats['beam'], seg=10))
            fx.append(VP.tube(p0 - fwd * 0.02, p0 + fwd * length * 0.9, r * 0.45, 0.005, mats['fx_beam'], seg=8))
            if dt < 0.5:
                fx.append(FX.puff(mats, p0 + fwd * 0.05, 0.16 * k + 0.06, 'fx_beam', seed=3))
        return FX.FxMesh(M.Mesh.concat([mesh] + fx), mats)

    def attack_battery(self, t, ctx):
        """The box stands raised; two interceptors leave a moment apart from
        their cells, each with a flame and a white trail, turning up as they
        climb; a puff of smoke from the box's vent at each launch."""
        b, mats = self.b, self.mats
        tf = self.p.get('fire', None)
        if tf is None:
            tf = V.flash_time(ctx, 0.3)
        mesh, G, H, T = self.assemble(pitch=b['rest_pitch'])
        fx = []
        fwd = G[:3, :3] @ np.array([0, 1, 0], np.float32)
        for k, cell in enumerate((b['tubes'][7], b['tubes'][12])):
            u = t - tf - k * 0.2
            if u < 0:
                continue
            p0 = (G @ np.array([*cell, 1.0], np.float32))[:3]
            dist = u * 28.0
            up = np.array([0, 0, 1], np.float32)
            d = fwd * (1 - min(u * 2, 0.6)) + up * min(u * 2, 0.6)
            d = d / np.linalg.norm(d)
            pos = p0 + d * dist
            if dist < 3.0:
                fx.append(interceptor(mats).transformed(V._orient_y(pos, d)))
                fx.append(FX.plume(mats, pos, -d, 0.8, 0.1, t=u, seed=k, smoke=False))
            for i in range(10):
                s = i / 10 * min(dist, 2.8)
                age = (dist - s) / 12.0
                if age < 1:
                    fx.append(FX.puff(mats, p0 + d * (s + 0.3), 0.3 * (1 + age) * (1 - age ** 2),
                                      'smoke_white' if age < 0.4 else 'smoke_light', seed=20 + i + 7 * k))
            if u < 0.1:
                fx.append(FX.muzzle_flash(mats, p0 + fwd * 0.15, fwd, 0.3, seed=4 + k))
                fx.append(FX.puff(mats, (G @ np.array([cell[0], 0.3, cell[2], 1.0], np.float32))[:3] - fwd * 0.2,
                                  0.3, 'smoke_white', seed=9 + k))
        return FX.FxMesh(M.Mesh.concat([mesh] + fx), mats)
