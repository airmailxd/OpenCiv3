"""Modern's (C7/Lua/modern) own ships and plane: the Pirate, the Troop Ship
and the Fighter/Bomber (catalog/modern_mod_sea_air.py), calibrated against
the mod's pictures (civ3art.MOD_UNIT_ORIGINALS) but drawn from these models.

The ships are designs of the 'ship' archetype (models/ships.py), the plane a
builder of the 'aircraft' archetype (models/aircraft.py); both are
registered here, so the stock modules stay as they are.

* 'steam_junk' (the Pirate): a South China Sea pirate's steam junk (the mod's
  picture): a tarred junk hull with gunports, paddle boxes and a funnel, plain
  fan-battened junk sails, the black flag; see its builder.
* 'troopship' (the Troop Ship): a WWII attack transport; see its builder.
* 'p38' (the Fighter/Bomber): a P-38 Lightning fighter-bomber; see its builder.
"""
import math

import numpy as np

import mesh as M
from models import aircraft as AC
from models import vparts as VP
from models.ship_designs import design, steel_hull, deck_at, funnel, lattice_mast
from models.ships import rod, box
from models.league_sea import paint


# ===================================================================== ship fittings

def cowl_vent(b, x, y, z, h=0.1, r=0.018, face=0.0, mat='steel_light', mouth='black'):
    """A ship's cowl ventilator: a pipe with a bell mouth turned to `face` (deg)."""
    b.add(M.cylinder(r, r, h, b.m(mat), seg=8, z0=z).transformed(M.translate(x, y, 0)))
    T = M.translate(x, y, z + h) @ M.rot_z(face) @ M.rot_x(-70)
    b.add(M.cylinder(r * 1.05, r * 1.9, r * 2.2, b.m(mat), seg=10).transformed(T))
    b.add(M.cylinder(r * 1.6, r * 1.6, 0.002, b.m(mouth), seg=10, z0=r * 2.2).transformed(T))


def boat(b, c, L=0.22, B=0.07, mat='white', yaw=0.0):
    """A ship's boat: a lofted shell with its gunwale and a thwart."""
    x, y, z = c
    rings = [(0.0, B * 0.18, L * 0.32), (0.025, B * 0.45, L * 0.48), (0.04, B * 0.5, L * 0.5)]
    sh = M.loft(rings, b.m(mat), seg=14)
    gun = M.loft([(0.036, B * 0.52, L * 0.51), (0.044, B * 0.52, L * 0.51)], b.m('wood_dark', '#4a3a2a'), seg=14,
                 cap_bottom=False)
    th = M.box(B * 0.9, 0.012, 0.006, b.m('wood_light', '#b8a27c'), center=(0, 0, 0.035))
    b.add(M.Mesh.concat([sh, gun, th]).transformed(M.translate(x, y, z) @ M.rot_z(yaw)))


def davits(b, x, y0, y1, z0, z1, mat='steel_dark'):
    for y in (y0, y1):
        b.add(rod((x * 0.8, y, z0), (x * 0.95, y, z1), 0.006, b.m(mat), seg=4))
        b.add(rod((x * 0.95, y, z1), (x * 1.08, y, z1 - 0.01), 0.005, b.m(mat), seg=4))


# ===================================================================== the Pirate

def fan_sail(b, y, z_bot, h, w, n=7, brace=22.0, billow=0.035, tint=True, base='sail', alt='sail_tint',
             batten="batten", rise=0.12, fwd=0.56):
    """A junk sail whose battens fan out from the foot of its luff: n panels
    (each a sail of its own, bellying between two battens), alternately the
    civ color and the plain cloth, the plain ones tipped with it at the rim (or
    all `base`, with `alt` tips on every other panel, when tint is False);
    the battens as spars over the seams, the yard along the head; the
    sheets from the leech's batten ends converging to one block. The sail is
    set across the ship, braced so its face shows in the side views; its
    plane runs from the luff (fwd * w to port of the mast) to the leech."""
    br = math.radians(brace)
    ca, sa = math.cos(br), math.sin(br)

    def P(px, pz):
        return np.array([px * ca, y + px * sa, pz], np.float32)
    # the hub: low on the luff side; the rim: up the luff, along the head (rising), down the leech
    hub = (-w * fwd * 0.25, z_bot)
    luff = [(-w * fwd, z_bot + h * k) for k in (0.2, 0.55, 0.85)]
    lx = -w * fwd + w * (1 + fwd * 0.2)
    head = [(-w * fwd + (lx + w * fwd) * t, z_bot + h * 0.92 + rise * t + 0.03 * math.sin(math.pi * t))
            for t in (0.0, 0.33, 0.66, 1.0)]
    leech = [(lx + 0.05 * w * math.sin(math.pi * k), z_bot + (h * 0.92 + rise) * (1 - k)) for k in (0.3, 0.6, 0.88)]
    R = np.array(luff + head + leech, np.float32)
    seg = np.linalg.norm(np.diff(R, axis=0), axis=1)
    s = np.concatenate([[0], np.cumsum(seg)])
    ts = np.linspace(0, s[-1], n + 1)
    pts = np.stack([np.interp(ts, s, R[:, 0]), np.interp(ts, s, R[:, 1])], -1)
    nrm = np.array([-sa, ca, 0], np.float32)
    mats = b.mats
    mb, ma, md = mats[base], mats[alt], mats[batten]
    hub3 = P(*hub)
    for i in range(n):
        a, c = pts[i], pts[i + 1]
        # the panel's inner end: a short piece of the boom near the hub
        ia = P(hub[0] + (a[0] - hub[0]) * 0.06, hub[1] + (a[1] - hub[1]) * 0.06)
        ic = P(hub[0] + (c[0] - hub[0]) * 0.06, hub[1] + (c[1] - hub[1]) * 0.06)
        if tint:
            # rays of civ color on every other panel, the plain panels tipped with it at the rim
            def f(u, v, i=i):
                if v > 0.985:
                    return md
                if i % 2 == 0:
                    return ma if v < 0.74 else mb
                return ma if v > 0.84 else mb
        else:
            def f(u, v, i=i):
                if v > 0.985:
                    return md
                return ma if (v > 0.8 and i % 2 == 0) else mb
        b.sails.append(dict(kind='square', corners=[ia, ic, P(*c), P(*a)], nu=4, nv=10, mat_fn=f, dir=nrm,
                            amount=billow, h=h, w=w / n, foot=0.0))
    # battens over the seams, sheets from the leech's batten ends to a block aft of the mast
    blk = P(hub[0] + w * 0.35, z_bot - 0.12) - np.array([0, 0.15, 0], np.float32)
    for i in range(n + 1):
        e = P(*pts[i])
        b.add(rod(hub3, e + (e - hub3) * 0.03, 0.0065, b.m('wood_dark'), seg=4))
        if i >= n // 2:
            b.line(e, blk, r=0.0025)
    # the yard along the head
    hp = [P(*q) for q in [(-w * fwd, z_bot + h * 0.92), (lx, z_bot + h * 0.92 + rise)]]
    b.add(rod(hp[0] - (hp[1] - hp[0]) * 0.04, hp[1] + (hp[1] - hp[0]) * 0.04, 0.01, b.m('wood_dark'), seg=5))
    return hub3, blk


def lug_sail(b, y, z_bot, h, w, n=6, brace=-115.0, billow=0.022, fwd=0.32, rise=0.16, drop=0.06, base='sail'):
    """A junk's battened lug sail: broad, its battens running across it and
    fanning a little (close together at the luff on the mast, spread at
    the leech), the head yard peaked up toward the leech, the cloth set flat
    and bellying only slightly between the battens; dark battens as spars
    over the seams, a dark boltrope round the edge, the sheets from the
    batten ends converging to one block on deck. Set across the ship and
    braced so its face shows in the side views."""
    br = math.radians(brace)
    ca, sa = math.cos(br), math.sin(br)

    def P(px, pz):
        return np.array([px * ca, y + px * sa, pz], np.float32)
    xl, xr = -w * fwd, w * (1 - fwd)
    # batten ends: the luff from 0.08h to 0.92h, the leech from -drop to h + rise; the leech bows out
    luff = [(xl, z_bot + h * (0.08 + 0.84 * k / n)) for k in range(n + 1)]
    leech = [(xr + 0.13 * w * math.sin(math.pi * k / n), z_bot - drop + (h + rise + drop) * k / n) for k in range(n + 1)]
    md, mb = b.mats['batten'], b.mats[base]
    nrm = np.array([-sa, ca, 0], np.float32)

    def f(u, v):
        if v > 0.985 or v < 0.012:
            return md
        return mb
    for i in range(n):
        A, B_ = P(*luff[i]), P(*luff[i + 1])
        C_, D = P(*leech[i + 1]), P(*leech[i])
        b.sails.append(dict(kind='square', corners=[A, B_, C_, D], nu=4, nv=8, mat_fn=f, dir=nrm, amount=billow,
                            h=h / n, w=w, foot=0.0))
    blk = P(xr * 0.6, z_bot - 0.14) - np.array([0, 0.18, 0], np.float32)
    for k in range(n + 1):
        a, e = P(*luff[k]), P(*leech[k])
        d = (e - a) / (np.linalg.norm(e - a) + 1e-9)
        r = 0.011 if k in (0, n) else 0.008
        b.add(rod(a - d * 0.02, e + d * 0.03, r, b.m('batten'), seg=5))
        if k <= n - 2:
            b.line(e, blk, r=0.0025)
    return P(*leech[n])


@design('steam_junk')
def steam_junk(b, p):
    """The Pirate: a South China Sea pirate's steam junk (the mod's picture
    is a paddle junk). A tarred wooden junk hull: a flat raked transom bow
    with painted eyes, a high square stern that overhangs aft with its
    gallery and great rudder, ochre wales and one civ-colored band; a row of
    gunports down each side with the guns run out; paddle wheels amidships
    under raised half-round paddle boxes with fanned vents and civ rims; a
    tall thick black funnel with two civ bands, smoking; a deckhouse with a
    vaulted roof; a swivel gun forward; three broad battened lug sails of
    plain tan canvas and a smaller one aft; the black flag at the main
    masthead, civ pennants, the ensign at the stern."""
    from models.ship_designs import castle
    from models.ships import Hull, emblem, clip_below
    b.m('batten', '#3a2c20')
    b.m('tar', '#3e3228')
    b.m('ochre', '#9a6a34')
    b.m('red', '#8a3426')
    b.m('flag_black', '#1c1a1a')
    b.m('bone', '#e8e2d2')
    hull = b.set_hull(Hull(b.mats, L=3.0, B=0.7, D=0.2, draft=0.12, hb=0.05, sheer_stern=0.36, sheer_bow=0.16,
                           bow_p=3.2, bow_e=0.45, stern_p=3.0, transom=0.72, rake_bow=0.3, rake_stern=0.3, n=2.8,
                           side='wood', deck='deck_wood', rail='ochre', planks=0.024,
                           stripes=[(0.0, 0.022, 'ochre'), (0.045, 0.115, 'tint'), (0.15, 0.17, 'ochre')]))
    zd = deck_at(hull, 0.0)
    mdw, mdb = b.m('deck_wood'), b.m('deck_b', '#5e4c3a')

    def planks(C, ids):
        ids[(ids == mdw) & (np.floor(C[:, 0] / 0.04) % 2 == 1)] = mdb
        return ids
    b.static[-1] = paint(b.static[-1], planks, max_edge=0.05)
    # the flat transom bow: a raked board across the bow, ochre-edged, with the eyes beside it
    yb = 1.5
    zb = deck_at(hull, 1.4)
    bow = [(-0.2, yb - 0.08, 0.02), (0.2, yb - 0.08, 0.02), (0.26, yb + 0.12, zb + 0.08), (-0.26, yb + 0.12, zb + 0.08)]
    b.add(M.plate(bow, b.m('wood'), thick=0.02))
    b.add(rod((-0.27, yb + 0.12, zb + 0.09), (0.27, yb + 0.12, zb + 0.09), 0.014, b.m('ochre'), seg=5))
    for sx in (-1, 1):
        y = 1.22
        z = deck_at(hull, y) - 0.06
        x = hull.side_x(y, z) + 0.004
        eye = M.Mesh.concat([M.cylinder(0.05, 0.05, 0.004, b.m('bone'), seg=14),
                             M.cylinder(0.024, 0.024, 0.006, b.m('black'), seg=10)])
        b.add(eye.transformed(M.translate(sx * x, y, z) @ M.rot_z(sx * -18) @ M.rot_y(sx * 90)))
    # the high square stern: a raised castle, its tall transom overhanging aft, gallery windows, the rudder
    zc = castle(b, hull, -1.55, -0.95, 0.16, mat_name='wood', top_mat='deck_wood', rail=0.04)
    hw = hull.half_beam(hull.s_of(-1.5)) + 0.03
    b.add(box((0, -1.68, zc - 0.02), (2 * hw + 0.08, 0.03, 0.44), b.m('wood'), bevel=0.008))
    b.add(box((0, -1.695, zc + 0.2), (2 * hw + 0.1, 0.02, 0.025), b.m('ochre'), bevel=0.004))
    # the stern gallery: a balcony across the transom, its rail and posts
    b.add(box((0, -1.73, zc - 0.13), (2 * hw - 0.02, 0.08, 0.012), b.m('deck_wood'), bevel=0.003))
    b.add(rod((-hw + 0.01, -1.765, zc - 0.08), (hw - 0.01, -1.765, zc - 0.08), 0.005, b.m('ochre'), seg=4))
    for x in np.linspace(-hw + 0.01, hw - 0.01, 7):
        b.add(rod((x, -1.765, zc - 0.13), (x, -1.765, zc - 0.08), 0.004, b.m('ochre'), seg=4))
    # the castle's top: planking, a hatch, a rail with stanchions round it
    pl = b.m('deck_b', '#5e4c3a')

    def tplanks(C, ids):
        ids[np.floor(C[:, 0] / 0.035) % 2 == 1] = pl
        return ids
    b.add(paint(box((0, -1.27, zc + 0.003), (2 * hw - 0.08, 0.52, 0.004), mdw), tplanks, max_edge=0.04))
    b.add(box((0.0, -1.1, zc + 0.015), (0.12, 0.1, 0.02), b.m('tar'), bevel=0.004))
    for sx in (-1, 1):
        b.add(rod((sx * (hw - 0.04), -1.6, zc + 0.07), (sx * (hw - 0.06), -0.97, zc + 0.07), 0.005, b.m('ochre'), seg=4))
        for y in np.linspace(-1.6, -0.97, 6):
            b.add(rod((sx * (hw - 0.05), y, zc), (sx * (hw - 0.05), y, zc + 0.07), 0.004, b.m('ochre'), seg=4))
    b.add(box((0, -1.675, zc - 0.07), (2 * hw - 0.04, 0.012, 0.09), b.m('red'), bevel=0.004))
    for x in (-0.12, -0.04, 0.04, 0.12):
        b.add(box((x, -1.683, zc - 0.07), (0.05, 0.008, 0.06), b.m('black')))
    for y in np.linspace(-1.42, -1.02, 4):
        for sx in (-1, 1):
            b.add(box((sx * (hull.side_x(y, zc - 0.06) + 0.004), y, zc - 0.06), (0.008, 0.05, 0.035), b.m('red')))
    b.add(box((0, -1.62, 0.12), (0.035, 0.18, 0.32), b.m('tar'), bevel=0.008))
    b.add(rod((0, -1.55, zc + 0.02), (0, -1.25, zc + 0.05), 0.01, b.m('wood_light', '#b8a27c'), seg=5))
    # gunports with the muzzles run out, both sides
    zg = zd - 0.05
    gys = np.linspace(-0.8, 0.85, 7)
    for y in gys:
        x = hull.side_x(y, zg)
        for sx in (-1, 1):
            b.add(box((sx * (x + 0.003), y, zg), (0.008, 0.085, 0.07), b.m('red')))
            b.add(box((sx * (x + 0.005), y, zg), (0.008, 0.055, 0.045), b.m('black')))
            b.add(rod((sx * (x - 0.02), y, zg), (sx * (x + 0.05), y, zg + 0.003), 0.013, b.m('iron'), seg=6, r1=0.011))
    for i, y in enumerate(gys[::-1]):
        x = hull.side_x(y, zg)
        b.gun((x + 0.05, y, zg), (1.0, 0.1, 0.03), delay=i * 0.03, size=1.0)
    # the paddle boxes: a raised half-round housing each side, on the waterline and up past the deck,
    # fanned vents on its face, a civ rim; nothing below the water
    yp = -0.33
    hb = hull.half_beam(hull.s_of(yp))
    R0 = 0.45
    mo, mr, mt = b.m('ochre'), b.m('black'), b.m('tint')
    for sx in (-1, 1):
        x = sx * (hb + 0.08)
        hous = M.cylinder(R0, R0, 0.15, b.m('tar'), seg=36, z0=-0.075).transformed(M.rot_y(90))

        def vents(C, ids):
            ang = np.degrees(np.arctan2(C[:, 2], C[:, 1]))
            r = np.hypot(C[:, 1], C[:, 2])
            face = np.abs(C[:, 0]) > 0.07
            ids[face & (r > 0.1) & (r < R0 * 0.86) & (np.floor((ang + 6) / 12) % 2 == 1)] = mr
            ids[face & (r >= R0 * 0.88)] = mo
            ids[~face] = mt
            ids[face & (r < 0.09)] = mo
            return ids
        hous = clip_below(paint(hous, vents, max_edge=0.016), 0.0)
        b.add(hous.transformed(M.translate(x, yp, 0.0)))
        b.add(box((sx * (hb + 0.03), yp, zd + 0.005), (0.09, 0.8, 0.012), mdw, bevel=0.003))
    # the funnel between the paddle boxes: tall, thick, black with two civ bands, smoking
    funnel(b, -0.33, zd, 0.95, 0.11, 0.115, mat_name='black', cap='black', rake=0.05)
    for z0f, hf in ((0.56, 0.07), (0.72, 0.07)):
        za = zd + 0.95 * z0f
        b.add(M.loft([(za, 0.116, 0.121, 0, -0.33 - 0.05 * 0.95 * z0f),
                      (za + 0.95 * hf, 0.115, 0.12, 0, -0.33 - 0.05 * 0.95 * (z0f + hf))], b.m('tint'), seg=16))
    b.add(box((0, -0.33, zd + 0.04), (0.28, 0.28, 0.08), b.m('tar'), bevel=0.01))
    b.smoke_stacks.append(np.array([0, -0.38, zd + 0.97], np.float32))
    b.add(rod((0.07, -0.0, zd + 0.3), (0.07, -0.0, zd + 0.4), 0.008, b.m('gold'), seg=6))
    # the deckhouse forward of the castle, its vaulted roof and doors
    b.add(box((0, 0.06, zd + 0.06), (0.3, 0.2, 0.12), b.m('house_wood', '#8a7050'), bevel=0.01))
    for k in range(7):
        a0, a1 = math.pi * k / 7, math.pi * (k + 1) / 7
        p0 = (0.18 * math.cos(a0), zd + 0.12 + 0.06 * math.sin(a0))
        p1 = (0.18 * math.cos(a1), zd + 0.12 + 0.06 * math.sin(a1))
        b.add(M.plate([(p0[0], -0.05, p0[1]), (p1[0], -0.05, p1[1]), (p1[0], 0.17, p1[1]), (p0[0], 0.17, p0[1])],
                      b.m('tar' if k % 2 else 'roof_b', '#54463a'), thick=0.01))
    b.add(box((0, 0.165, zd + 0.05), (0.07, 0.006, 0.08), b.m('red')))
    # the swivel gun forward, the anchor and its windlass
    yg = 1.32
    b.add(M.cylinder(0.04, 0.05, 0.06, b.m('tar'), seg=10, z0=deck_at(hull, yg)).transformed(M.translate(0, yg, 0)))
    b.turret((0, yg, deck_at(hull, yg) + 0.06), shape='box', size=0.08, h=0.04, barrel=0.16, n=1, rest=0.0,
             aim=0.0, mat_name='iron', gun_mat='black', flash=1.0, elev=6)
    ya = 1.25
    xa = hull.half_beam(hull.s_of(ya)) + 0.01
    b.add(rod((xa, ya, deck_at(hull, ya) - 0.02), (xa + 0.01, ya + 0.02, deck_at(hull, ya) - 0.14), 0.008,
              b.m('iron'), seg=4))
    for s_ in (-1, 1):
        b.add(rod((xa + 0.01, ya + 0.02, deck_at(hull, ya) - 0.14), (xa + 0.01, ya + 0.02 + 0.04 * s_,
                                                                     deck_at(hull, ya) - 0.1), 0.007, b.m('iron'), seg=4))
    b.add(M.cylinder(0.025, 0.025, 0.16, b.m('wood'), seg=8, z0=-0.08).transformed(
        M.translate(0, 1.18, deck_at(hull, 1.18) + 0.04) @ M.rot_y(90)))
    # a hatch and bitts on the foredeck
    b.add(box((0, 0.74, deck_at(hull, 0.74) + 0.015), (0.18, 0.16, 0.03), b.m('tar'), bevel=0.004))
    b.add(box((0, 0.74, deck_at(hull, 0.74) + 0.032), (0.15, 0.13, 0.006), b.m('wood_light', '#b8a27c')))
    for sx in (-1, 1):
        b.add(M.cylinder(0.014, 0.014, 0.05, b.m('tar'), seg=6, z0=deck_at(hull, 1.4)).transformed(M.translate(sx * 0.12, 1.4, 0)))

    # three broad battened lug sails forward, a smaller one on the castle
    rig = [(1.0, 0.16, 0.66, 0.9), (0.5, 0.2, 0.92, 1.05), (-0.64, 0.24, 0.56, 0.78)]
    tops = []
    for y, z0, h, w in rig:
        zm = deck_at(hull, y)
        tops.append(b.mast(y, zm, z0 + h + 0.18, r=0.021, mat_name='wood_dark'))
        lug_sail(b, y, zm + z0, h, w, n=6)
    ym = -1.22
    top = b.mast(ym, zc, 0.6, r=0.018, mat_name='wood_dark')
    tops.append(top)
    lug_sail(b, ym, zc + 0.12, 0.42, 0.55, n=4, rise=0.1)
    # the black flag at the main masthead (a skull over crossed bones), civ pennants, the ensign
    skull = emblem('skull', 1.4)
    fb, fw = b.mats['flag_black'], b.mats['bone']
    mt_ = tops[1]
    fwid, fh = 0.32, 0.22
    b.add(rod(mt_, mt_ + np.array([0, 0, 0.06], np.float32), 0.006, b.m('wood_dark'), seg=4))
    b.sails.append(dict(kind='fa', corners=[mt_ + (0, 0, 0.055), mt_ + (0, -fwid, 0.045), mt_ + (0, -fwid, 0.045 - fh),
                                            mt_ + (0, 0, 0.055 - fh)],
                        nu=14, nv=10, mat_fn=lambda u, v: fw if skull(2 * u - 1, 2 * v - 1) else fb,
                        dir=np.array([1.0, 0, 0], np.float32), amount=0.012))
    b.flag(tops[0] + np.array([0, 0, 0.005], np.float32), w=0.36, h=0.06, pole=False, phase=0.4)
    b.flag(tops[2] + np.array([0, 0, 0.005], np.float32), w=0.28, h=0.05, pole=False, phase=1.1)
    b.flag((0, -1.66, zc + 0.36), w=0.24, h=0.15, phase=0.7)       # the ensign at the stern
    # stays: stem to foremast, between the mastheads, down to the stern; shrouds
    b.line((0, 1.62, zb + 0.1), tops[0], r=0.004)
    for i in range(len(tops) - 1):
        b.line(tops[i], tops[i + 1], r=0.003)
    b.line(tops[-1], (0, -1.66, zc + 0.08), r=0.003)
    for k, y in enumerate((1.0, 0.5, -0.64)):
        for sx in (-1, 1):
            b.line(tops[k] - np.array([0, 0, 0.1], np.float32),
                   (sx * hull.half_beam(hull.s_of(y)) * 0.97, y - 0.12, deck_at(hull, y) + hull.hb), r=0.0025)
    b.weapon = dict(kind='cannons', t=0.15)
    b.extra_wake = 0.9


# ===================================================================== the Troop Ship

def lcvp(b, c, yaw=0.0, k=1.0):
    """A landing craft (LCVP, a Higgins boat): a square-bowed box hull, its
    ramp at the bow, the coxswain's stand aft, painted dark grey."""
    x, y, z = c
    L, B, H = 0.2 * k, 0.07 * k, 0.045 * k
    md, ml = b.m('lcvp', '#3e4c60'), b.m('ramp', '#b8bcc0')
    parts = [M.rounded_box(B, L, H, md, center=(0, 0, H / 2), r=0.008 * k, seg=8),
             M.box(B * 0.78, L * 0.72, 0.004, b.m('lcvp_in', '#2e3238'), center=(0, -L * 0.04, H + 0.001)),
             M.box(B * 0.95, 0.01 * k, H * 1.05, ml, center=(0, L / 2 - 0.004, H * 0.55)),
             M.box(B * 0.4, 0.03 * k, 0.025 * k, md, center=(0, -L * 0.38, H + 0.012 * k))]
    b.add(M.Mesh.concat(parts).transformed(M.translate(x, y, z) @ M.rot_z(yaw)))


def kingposts(b, y, zd, h, span, booms, mat='steel_dark'):
    """A pair of king posts joined by a crosstree, each with a cargo boom
    reaching over a hatch (booms: +1 forward, -1 aft), topping lifts to the
    post heads."""
    m = b.m(mat)
    heads = []
    for sx in (-1, 1):
        top = np.array([sx * span, y, zd + h], np.float32)
        b.add(rod((sx * span, y, zd), top, 0.014, m, seg=6, r1=0.01))
        b.add(M.cylinder(0.018, 0.018, 0.01, m, seg=8, z0=zd + h).transformed(M.translate(sx * span, y, 0)))
        heads.append(top)
    b.add(rod((-span, y, zd + h * 0.78), (span, y, zd + h * 0.78), 0.01, m, seg=4))
    b.add(box((0, y, zd + h * 0.78), (0.05, 0.04, 0.03), m))
    for sx in (-1, 1):
        foot = np.array([sx * span * 0.9, y, zd + 0.05], np.float32)
        tip = np.array([sx * span * 0.75, y + booms * 0.34, zd + h * 0.62], np.float32)
        b.add(rod(foot, tip, 0.007, m, seg=4, r1=0.005))
        b.line(heads[(sx + 1) // 2], tip, r=0.0025)
        b.line(tip, tip + np.array([0, 0, -0.12], np.float32), r=0.002)


def hatch(b, y, zd, w, l, tint=True):
    b.add(box((0, y, zd + 0.02), (w + 0.03, l + 0.03, 0.04), b.m('coaming', '#56585c')))
    b.add(box((0, y, zd + 0.043), (w, l, 0.008), b.m('tint' if tint else 'tarp', '#72766a')))
    for k in range(1, 4):
        b.add(box((0, y - l / 2 + l * k / 4, zd + 0.048), (w + 0.01, 0.006, 0.004), b.m('coaming')))


@design('troopship')
def troopship(b, p):
    """A WWII attack transport (an APA of the Haskell class, 1944): a long
    grey hull with a raised forecastle and a cruiser stern; a three-tier
    house amidships with rows of portholes, the bridge and its wings, a
    radar mast; one stout funnel with a civ-colored band; landing craft
    (LCVPs) in davits along both sides of the house and stacked on the
    hatches; king posts with cargo booms over civ-colored hatches fore and
    aft; a 5-inch gun aft, a 3-inch forward, twin 40 mm guns in tubs."""
    b.m('house', '#9ca2a8')
    b.m('house_dark', '#7e848a')
    hull = steel_hull(b, 3.6, 0.7, 0.21, draft=0.1, rake=0.22, transom=0.45, n=3.6, side='steel', deck='deck_steel',
                      bow_p=2.2, stern_p=3.0, sheer_bow=0.1, planks=0.0,
                      stripes=[(0.0, 0.012, 'steel_light')], bands=((-0.004, 0.03, 'black'), (0.03, 0.042, 'steel_light')))
    zd = lambda y: deck_at(hull, y)
    z0 = zd(0.0)
    hb = lambda y: hull.half_beam(hull.s_of(y))
    # hull plating: faint strakes
    mst, mpl = b.m('steel'), b.m('plate')

    def strakes(C, ids):
        ids[(ids == mst) & (np.floor((C[:, 1] + 2) / 0.22) % 2 == 1) & (C[:, 2] > 0.02)] = mpl
        return ids
    b.static[-1] = paint(b.static[-1], strakes, max_edge=0.06)
    # the forecastle: a raised deck at the bow with a breakwater
    yf0 = 1.15
    b.add(box((0, 1.45, zd(1.3) + 0.03), (hb(1.3) * 1.7, 0.6, 0.06), b.m('deck_steel'), bevel=0.01))
    b.add(box((0, yf0 + 0.02, zd(yf0) + 0.06), (hb(yf0) * 1.8, 0.04, 0.12), b.m('steel'), bevel=0.006))
    # portholes along the hull under the house
    for y in np.linspace(-1.3, 1.1, 30):
        for sx in (-1, 1):
            b.add(box((sx * (hull.side_x(y, z0 - 0.05) + 0.002), y, z0 - 0.05), (0.006, 0.014, 0.014), b.m('black')))

    # the house amidships: three tiers, portholes and windows, the bridge with its wings
    yh = -0.06
    tiers = [(0.46, 0.96, 0.12, 0.0), (0.4, 0.66, 0.1, 0.06), (0.34, 0.3, 0.08, 0.2)]
    z = z0
    for i, (w, l, h, dy) in enumerate(tiers):
        b.add(box((0, yh + dy, z + h / 2), (w, l, h), b.m('house'), bevel=0.008))
        b.add(box((0, yh + dy, z + h + 0.004), (w + 0.02, l + 0.02, 0.008), b.m('house_dark'), bevel=0.003))
        # a row of portholes / windows down each side and across the front
        n = int(l / 0.06)
        for y in np.linspace(yh + dy - l / 2 + 0.04, yh + dy + l / 2 - 0.04, n):
            for sx in (-1, 1):
                b.add(box((sx * (w / 2 + 0.002), y, z + h * 0.6), (0.005, 0.016, 0.016 if i < 2 else 0.024),
                          b.m('black' if i < 2 else 'glass')))
        for x in np.linspace(-w / 2 + 0.04, w / 2 - 0.04, int(w / 0.06)):
            b.add(box((x, yh + dy + l / 2 + 0.002, z + h * 0.6), (0.016, 0.005, 0.016 if i < 2 else 0.026),
                      b.m('black' if i < 2 else 'glass')))
        z += h + 0.008
    zb = z
    # the bridge wings and the radar mast
    zw = z0 + 0.12 + 0.1 + 0.016
    b.add(box((0, yh + 0.3, zw + 0.004), (0.62, 0.08, 0.012), b.m('house_dark'), bevel=0.003))
    lattice_mast(b, yh + 0.12, zb, 0.36, yard=0.2, radar=0.1)
    # one stout funnel aft of the bridge, a civ band, smoking
    funnel(b, yh - 0.2, z0 + 0.22, 0.34, 0.085, 0.11, mat_name='house', cap='black', rake=0.08, band=(0.52, 0.16))
    # cowl vents round the funnel
    for (x, y) in ((0.15, yh - 0.36), (-0.15, yh - 0.36)):
        cowl_vent(b, x, y, z0 + 0.12, h=0.08, face=0)
    # landing craft in davits along both sides of the house
    for y in np.linspace(yh - 0.36, yh + 0.36, 4):
        for sx in (-1, 1):
            # swung out over the side in their davits, so they show against the hull and the sea
            xo = sx * (hb(y) + 0.045)
            for dy in (-0.09, 0.09):
                b.add(rod((sx * (hb(y) - 0.08), y + dy, z0 + 0.005), (sx * (hb(y) + 0.05), y + dy, z0 + 0.19), 0.006,
                          b.m('steel_dark'), seg=4))
                b.line((sx * (hb(y) + 0.05), y + dy, z0 + 0.19), (xo, y + dy * 0.8, z0 + 0.16), r=0.002)
            lcvp(b, (xo, y, z0 + 0.1), yaw=0.0, k=1.4)

    # hatches fore and aft under king posts and booms; boats stacked on two of them
    for y, l, t in ((0.92, 0.26, True), (0.55, 0.3, False), (-0.72, 0.3, False), (-1.08, 0.26, True)):
        hatch(b, y, zd(y), 0.34, l, tint=t)
    lcvp(b, (0.0, 0.55, zd(0.55) + 0.05), k=1.0)
    lcvp(b, (0.0, -0.72, zd(-0.72) + 0.05), k=1.0)
    kingposts(b, 0.74, zd(0.74), 0.5, 0.11, 1)
    kingposts(b, -0.9, zd(-0.9), 0.46, 0.11, -1)
    b.line((0, 1.74, zd(1.7) + 0.12), (0.0, 0.74, zd(0.74) + 0.39), r=0.003)
    b.line((0, -0.9, zd(-0.9) + 0.36), (0, -1.72, zd(-1.7) + 0.12), r=0.003)

    # guns: a 5-inch aft, a 3-inch on the forecastle, twin 40 mm in tubs at the house's corners
    b.add(M.cylinder(0.1, 0.1, 0.03, b.m('steel_dark'), seg=16, z0=zd(-1.45)).transformed(M.translate(0, -1.45, 0)))
    b.turret((0, -1.45, zd(-1.45) + 0.03), shape='box', size=0.12, h=0.07, barrel=0.18, n=1, rest=180.0, aim=180.0,
             mat_name='house', gun_mat='black', flash=1.0, elev=12)
    b.turret((0, 1.42, zd(1.3) + 0.06), shape='round', size=0.08, h=0.03, barrel=0.12, n=1, rest=0.0, aim=0.0,
             mat_name='house', gun_mat='black', flash=0.8, elev=10, delay=0.05)
    for (x, y) in ((0.19, yh + 0.46), (-0.19, yh + 0.46), (0.19, yh - 0.5), (-0.19, yh - 0.5)):
        b.add(M.cylinder(0.04, 0.04, 0.04, b.m('house_dark'), seg=12, z0=z0 + 0.12).transformed(M.translate(x, y, 0)))
        for dx in (-0.01, 0.01):
            b.add(rod((x + dx, y, z0 + 0.17), (x + dx * 1.5, y + 0.07, z0 + 0.2), 0.004, b.m('black'), seg=4))
    # cargo winches beside the hatches, bitts and the anchor windlass on the forecastle, life rafts on the house
    for y in (0.74, -0.9):
        for dy in (-0.07, 0.07):
            yy = y + dy
            b.add(box((0, yy, zd(yy) + 0.025), (0.08, 0.05, 0.05), b.m('steel_dark'), bevel=0.006))
            b.add(M.cylinder(0.02, 0.02, 0.12, b.m('house_dark'), seg=8, z0=-0.06).transformed(
                M.translate(0, yy, zd(yy) + 0.05) @ M.rot_y(90)))
    zf = zd(1.3) + 0.06
    for sx in (-1, 1):
        b.add(M.cylinder(0.035, 0.035, 0.04, b.m('steel_dark'), seg=10, z0=zf).transformed(M.translate(sx * 0.07, 1.6, 0)))
        b.add(M.cylinder(0.022, 0.022, 0.06, b.m('house_dark'), seg=8, z0=-0.03).transformed(
            M.translate(sx * 0.07, 1.6, zf + 0.04) @ M.rot_y(90)))
        for y in (1.3, 1.68):
            for dy in (0.0, 0.04):
                b.add(M.cylinder(0.012, 0.012, 0.035, b.m('black'), seg=6, z0=zf).transformed(
                    M.translate(sx * 0.14, y + dy, 0)))
        b.line((sx * 0.07, 1.6, zf + 0.02), (sx * 0.09, 1.75, zf - 0.02), r=0.006, mat_name='iron')
    for y in (yh - 0.05, yh + 0.2):
        for sx in (-1, 1):
            b.add(box((sx * 0.205, y, z0 + 0.175), (0.012, 0.09, 0.05), b.m('raft', '#c8b890'), bevel=0.004))
    # the anchor in its hawse, the jack staff and the ensign
    for sx in (-1, 1):
        ya = 1.55
        b.add(box((sx * (hull.side_x(ya, zd(ya) - 0.04) + 0.003), ya, zd(ya) - 0.04), (0.008, 0.05, 0.04), b.m('black')))
    b.flag((0, 1.78, zd(1.75) + 0.2), w=0.1, h=0.06)
    b.flag((0, -1.76, zd(-1.75) + 0.22), w=0.13, h=0.08)
    b.weapon = dict(kind='turrets', t=0.15)


# ===================================================================== the plane

def build_p38(V):
    """A Lockheed P-38J Lightning as a fighter-bomber, in olive drab over
    neutral grey: the central gondola with the guns in its nose and a framed
    bubble canopy; two booms, each an Allison engine behind its spinner with
    the chin intakes, the turbo-supercharger's silver fairing on top behind
    the wing, the radiator scoops on its flanks and an oval fin above and
    below; the tailplane joining the fins; a tapered wing with panel lines,
    flaps and ailerons; a 1,000 lb bomb on a pylon under each wing root;
    the national insignia (the civ color) on the wings and the booms, and
    civ-colored bands round the booms ahead of the fins."""
    from models.league_air import paintn, insignia, two_tone, near, canopy_frames
    m = V.mats
    L = 11.5
    paint, under, line = m['paint'], m['under'], m['line']
    bx = 2.45                    # the booms' axis
    zb = 0.0
    # the gondola: the nose with its guns, the canopy, the tapered tail behind the wing
    gon = AC.fuselage([(-1.9, 0.08), (-1.4, 0.42), (-0.2, 0.7), (1.4, 0.72), (2.8, 0.6), (3.6, 0.4), (4.0, 0.12)],
                      paint, seg=16, sx=0.88, sz=1.0)
    gl = lambda C, N: near(C[:, 1], (2.2, 0.9), 0.035) & (C[:, 2] > -0.25)
    body = [paintn(gon, two_tone(paint, under, line, z_split=-0.2, lines=gl), 0.14)]
    for dx, dz, r, l in ((-0.12, 0.12, 0.05, 0.55), (0.12, 0.12, 0.05, 0.55), (-0.2, 0.0, 0.045, 0.45),
                         (0.2, 0.0, 0.045, 0.45), (0.0, -0.1, 0.07, 0.6)):
        body.append(VP.tube((dx, 3.85, dz), (dx, 3.85 + l, dz), r, r * 0.9, m['steel'], seg=6))
    body.append(VP.icosphere(0.42, m['glass'], (0, 0.95, 0.62), sub=2, smooth=True, squash=(0.78, 1.9, 0.78)))
    body += canopy_frames((0, 0.95, 0.62), (0.33, 0.8, 0.33), (-0.45, 0.0, 0.42), m['line_dark'])
    body.append(M.box(0.36, 0.05, 0.3, m['line_dark'], center=(0, 1.6, 0.68)))          # armoured windscreen
    # the wing: one tapered panel each side, through the gondola and the booms
    poly = [(0.0, 1.35), (bx + 1.0, 1.3), (7.3, 0.62), (7.9, 0.35), (7.9, -0.3), (7.4, -0.48), (bx + 1.0, -0.95),
            (0.0, -0.95)]
    wl = lambda C, N: (near(np.abs(C[:, 0]), (1.3, bx + 1.25, 4.6, 6.0), 0.045)
                       | ((np.abs(C[:, 0]) > 4.8) & near(C[:, 1], (-0.12 - 0.08 * (np.abs(C[:, 0]) - 4.8),), 0.045))
                       | ((np.abs(C[:, 0]) < 4.8) & (np.abs(C[:, 0]) > 0.75) & near(C[:, 1], (-0.5,), 0.045)))
    ws = {}
    props = []
    for side, name in ((1, 'wing_r'), (-1, 'wing_l')):
        w = AC.wing(poly, 0.24, paint, z=-0.12, dihedral=2.5, side=side)
        tt = two_tone(paint, under, line, lines=wl)

        def wingf(C, N, ids, tt=tt):
            ids = tt(C, N, ids)
            ids[(np.abs(C[:, 0]) > 7.25) & (N[:, 2] > -0.25)] = m['band']     # civ-colored tips
            ids[near(np.abs(C[:, 0]), (7.25,), 0.05) & (N[:, 2] > -0.25)] = m['star']
            return ids
        parts = [paintn(w, wingf, 0.16)]
        zt = -0.12 + 0.125 + 5.9 * math.tan(math.radians(2.5))
        parts.append(insignia(0.6, m['band'], m['star'], bar_k=0.5).transformed(
            M.translate(side * 5.5, 0.2, zt) @ M.rot_z(90)))
        # the boom: spinner, engine nacelle, turbo, radiators, the long tail boom
        x = side * bx
        zbm = zb + bx * math.tan(math.radians(2.5)) - 0.05
        boom = AC.fuselage([(-8.0, 0.16), (-7.2, 0.3), (-5.0, 0.42), (-2.6, 0.55), (-0.4, 0.7), (1.6, 0.72),
                            (3.2, 0.62), (3.75, 0.5)], paint, seg=16, sx=0.84, sz=1.0)
        bl = lambda C, N: near(C[:, 1], (2.9, 0.2, -2.0, -4.0, -6.0), 0.035) & (C[:, 2] > -0.2)
        parts.append(paintn(boom, two_tone(paint, under, line, z_split=-0.15, lines=bl), 0.14).transformed(
            M.translate(x, 0, zbm)))
        parts.append(AC.fuselage([(3.7, 0.42), (4.05, 0.32), (4.35, 0.0)], m['steel'], seg=14).transformed(
            M.translate(x, 0, zbm)))
        # chin intakes under the spinner
        for dx in (-0.2, 0.2):
            parts.append(M.box(0.24, 0.5, 0.18, paint, center=(x + dx, 3.3, zbm - 0.5)))
            parts.append(M.box(0.18, 0.04, 0.12, m['line_dark'], center=(x + dx, 3.56, zbm - 0.5)))
        # the turbo-supercharger's silver fairing on top behind the wing
        parts.append(VP.icosphere(0.32, m['metal'], (x, -1.6, zbm + 0.62), sub=1, smooth=True,
                                  squash=(0.9, 1.7, 0.45)))
        parts.append(M.cylinder(0.17, 0.17, 0.03, m['line_dark'], seg=12, z0=zbm + 0.74).transformed(
            M.translate(x, -1.6, 0)))
        # the radiator scoops on the boom's flanks
        for sx in (-1, 1):
            parts.append(M.rounded_box(0.22, 1.3, 0.42, paint, center=(x + sx * 0.4, -4.3, zbm + 0.02), r=0.08, seg=8))
            parts.append(M.box(0.18, 0.04, 0.32, m['line_dark'], center=(x + sx * 0.43, -3.64, zbm + 0.02)))
        # the civ-colored band round the boom ahead of the fins, the insignia on its flank
        parts.append(AC.fuselage([(-6.1, 0.37), (-5.6, 0.39)], m['band'], seg=16, sx=0.87, sz=1.03).transformed(
            M.translate(x, 0, zbm)))
        props.append(((x, 4.2, zbm), 1.75, 3, 'y'))
        # the bomb under the wing root, on its pylon
        xbm = side * 1.35
        parts.append(M.box(0.12, 1.2, 0.32, paint, center=(xbm, 0.9, -0.38)))
        # a dark teardrop (1,000 lb) with a yellow nose band and box fins
        bomb = M.Mesh.concat([AC.fuselage([(-1.45, 0.08), (-1.1, 0.24), (-0.4, 0.44), (0.4, 0.46), (0.95, 0.34),
                                           (1.25, 0.12), (1.32, 0.0)], m['bomb'], seg=14),
                              AC.fuselage([(0.78, 0.4), (0.9, 0.36)], m['bomb_band'], seg=14),
                              M.box(0.78, 0.36, 0.05, m['bomb'], center=(0, -1.3, 0)),
                              M.box(0.05, 0.36, 0.78, m['bomb'], center=(0, -1.3, 0))])
        parts.append(bomb.transformed(M.translate(xbm, 1.55, -0.95)))
        ws[name] = M.Mesh.concat(parts)
    # the tail: an oval fin above and below each boom, the tailplane between them
    tail = []
    zt = zb + bx * math.tan(math.radians(2.5)) - 0.05
    fin = [(-6.3, -0.2), (-6.5, 0.9), (-6.75, 1.5), (-7.1, 1.75), (-7.55, 1.7), (-7.95, 1.3), (-8.05, 0.0),
           (-7.95, -0.75), (-7.55, -0.95), (-7.0, -0.8), (-6.5, -0.4)]
    rl = lambda C, N: near(C[:, 1], (-7.45,), 0.04) & (np.abs(C[:, 2] - zt) > 0.3)
    for sx in (-1, 1):
        f = VP.side_prism(fin, 0.16, paint).transformed(M.translate(sx * bx, 0, zt))
        tail.append(paintn(f, two_tone(paint, under, line, lines=rl), 0.14))
    tpl = [(0.0, -6.55), (bx + 0.7, -6.6), (bx + 0.95, -6.85), (bx + 0.95, -7.5), (0.0, -7.6)]
    el = lambda C, N: near(C[:, 1], (-7.05,), 0.04)
    for s in (1, -1):
        tail.append(paintn(AC.wing(tpl, 0.13, paint, z=zt + 0.3, side=s), two_tone(paint, under, line, lines=el), 0.14))
    parts = dict(ws)
    parts['body'] = M.Mesh.concat(body)
    parts['tail'] = M.Mesh.concat(tail)
    return dict(parts=parts, props=props, guns=[(sx * 0.15, 4.5, 0.06) for sx in (-1, 1)], pylons=[], length=L)


AC.BUILDERS.setdefault('p38', build_p38)
AC.PAINTS.setdefault('p38', {'paint': '#5f6442', 'under': '#8e9290', 'line': '#454a2c', 'line_dark': '#2a2c26',
                             'paint_dark': '#4c5134', 'band': '#e8e8e8', 'star': '#f2f2f0',
                             'glass': ('#9ab8c8', 0.7, 40), 'steel': ('#3a3a3a', 0.5, 24),
                             'metal': ('#b8bcc0', 0.6, 30), 'blade': '#262626', 'blade_tip': '#d8c040',
                             'bomb': '#2e302a', 'bomb_band': '#d0b038', 'tire': '#222222'})
