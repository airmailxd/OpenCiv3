"""Modern's (C7/Lua/modern) own ships and plane: the Pirate, the Troop Ship
and the Fighter/Bomber (catalog/modern_mod_sea_air.py), calibrated against
the mod's pictures (civ3art.MOD_UNIT_ORIGINALS) but drawn from these models.

The ships are designs of the 'ship' archetype (models/ships.py), the plane a
builder of the 'aircraft' archetype (models/aircraft.py); both are
registered here, so the stock modules stay as they are.

* 'steam_junk' (the Pirate): the mod's picture, a South China Sea pirate's
  steam junk: a low grey iron hull with a ram bow and a planked deck; paddle
  wheels amidships in boxes with fanned vents and civ-colored rims; a casemate
  aft with ports and a civ-colored band, its own junk mast with a tan sail;
  three masts forward of it with fan-battened junk sails (the battens
  radiate from the foot, the cloth bellies between them, the panels
  alternately civ-colored and plain, the sheets fanning down to a block);
  one tall funnel; a gun turret on the foredeck, a bow chaser, cowl vents,
  a boat on davits, the anchor.
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
                if v > 0.965:
                    return md
                if i % 2 == 0:
                    return ma if v < 0.74 else mb
                return ma if v > 0.84 else mb
        else:
            def f(u, v, i=i):
                if v > 0.965:
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


@design('steam_junk')
def steam_junk(b, p):
    b.m('batten', '#3e3428')
    b.m('sail_old', '#b49a72')
    b.m('sail_old_b', '#8e7656')
    b.m('casemate', '#7a7e84')
    b.m('deck_b', '#5e4c3a')
    hull = steel_hull(b, 3.0, 0.62, 0.15, draft=0.12, rake=-0.13, transom=0.55, n=2.8, side='steel', deck='deck_wood',
                      bow_p=1.6, stern_p=2.4, sheer_bow=0.05,
                      stripes=[(0.0, 0.012, 'steel_light'), (0.05, 0.064, 'tint')],
                      bands=((-0.004, 0.024, 'black'),), planks=0.025)
    zd = deck_at(hull, 0.0)
    mdw, mdb = b.m('deck_wood'), b.m('deck_b')

    # the deck's planks
    def planks(C, ids):
        ids[(ids == mdw) & (np.floor(C[:, 0] / 0.04) % 2 == 1)] = mdb
        return ids
    b.static[-1] = paint(b.static[-1], planks, max_edge=0.05)

    # the paddle boxes amidships: a drum each side, fanned vents, a civ-colored rim
    yp = -0.08
    hb = hull.half_beam(hull.s_of(yp))
    for sx in (-1, 1):
        x = sx * (hb + 0.045)
        drum = M.cylinder(0.21, 0.21, 0.09, b.m('steel_dark'), seg=28, z0=-0.045).transformed(M.rot_y(90))
        mblk = b.m('black')

        def vents(C, ids):
            ang = np.degrees(np.arctan2(C[:, 2], C[:, 1]))
            r = np.hypot(C[:, 1], C[:, 2])
            side = np.abs(C[:, 0]) > 0.04
            ids[side & (r > 0.06) & (r < 0.19) & (np.floor((ang + 7.5) / 15) % 2 == 1)] = mblk
            return ids
        drum = paint(drum, vents, max_edge=0.018)
        rim = M.cylinder(0.225, 0.225, 0.1, b.m('tint'), seg=28, z0=-0.05, caps=False).transformed(M.rot_y(90))
        hubc = M.cylinder(0.045, 0.045, 0.1, b.m('steel_light'), seg=12, z0=-0.05).transformed(M.rot_y(90))
        b.add(M.Mesh.concat([drum, rim, hubc]).transformed(M.translate(x, yp, zd - 0.03)))
        # the sponson walk between box and hull
        b.add(box((sx * (hb + 0.02), yp, zd + 0.005), (0.06, 0.5, 0.012), mdw, bevel=0.003))
    b.add(rod((-hb - 0.09, yp, zd - 0.03), (hb + 0.09, yp, zd - 0.03), 0.02, b.m('steel_dark'), seg=8))

    # the casemate aft: grey plate, ports, a civ-colored band, a rail on top
    yc0, yc1 = -1.18, -0.42
    zc = zd + 0.15
    hw = hull.half_beam(hull.s_of(-0.8)) * 0.86
    cm = box((0, (yc0 + yc1) / 2, zd + 0.075), (2 * hw, yc1 - yc0, 0.15), b.m('casemate'), bevel=0.012)
    # the wheelhouse on the casemate's fore end, its windows round three sides, a compass binnacle
    b.add(box((0, yc1 - 0.1, zc + 0.06), (0.2, 0.16, 0.12), b.m('steel_light'), bevel=0.01))
    b.add(box((0, yc1 - 0.1, zc + 0.125), (0.24, 0.2, 0.012), b.m('steel_dark'), bevel=0.003))
    b.add(box((0, yc1 - 0.019, zc + 0.08), (0.17, 0.004, 0.03), b.m('glass')))
    for sx in (-1, 1):
        b.add(box((sx * 0.101, yc1 - 0.1, zc + 0.08), (0.004, 0.12, 0.03), b.m('glass')))
    mpl, mt = b.m('plate'), b.m('tint')

    def plates(C, ids):
        ids[np.floor((C[:, 1] - yc0) / 0.11) % 2 == 1] = mpl
        ids[np.abs(C[:, 2] - (zd + 0.115)) < 0.012] = mt
        return ids
    b.add(paint(cm, plates, max_edge=0.02))
    for y in np.linspace(yc0 + 0.1, yc1 - 0.1, 6):
        for sx in (-1, 1):
            b.add(box((sx * (hw + 0.002), y, zd + 0.06), (0.008, 0.035, 0.03), b.m('black')))
    b.add(box((0, -0.95, zc + 0.05), (hw * 1.3, 0.34, 0.1), b.m('casemate'), bevel=0.01))
    b.add(box((0, -0.775, zc + 0.065), (hw * 1.1, 0.01, 0.025), b.m('glass')))
    for sx in (-1, 1):
        b.add(rod((sx * hw, yc0 + 0.02, zc + 0.03), (sx * hw, yc1 - 0.02, zc + 0.03), 0.004, b.m('steel_dark'), seg=4))
        for y in np.linspace(yc0 + 0.02, yc1 - 0.02, 7):
            b.add(rod((sx * hw, y, zc), (sx * hw, y, zc + 0.03), 0.003, b.m('steel_dark'), seg=4))

    # the funnel between the paddle boxes, black with a civ band, smoking; cowl vents round it
    funnel(b, -0.2, zd, 0.62, 0.07, 0.075, mat_name='black', cap='black', rake=0.06, band=(0.72, 0.1))
    b.add(box((0, -0.2, zd + 0.04), (0.2, 0.22, 0.08), b.m('steel_dark'), bevel=0.01))
    b.smoke_stacks.append(np.array([0, -0.24, zd + 0.64], np.float32))
    for (x, y, f) in ((0.14, -0.3, 180), (-0.14, -0.3, 180), (0.12, 0.25, 0), (-0.12, 0.25, 0)):
        cowl_vent(b, x, y, zd, h=0.09, face=f)
    b.add(rod((0.03, -0.12, zd + 0.3), (0.03, -0.12, zd + 0.42), 0.008, b.m('gold'), seg=6))   # whistle

    # the turret on the foredeck and a bow chaser in the stem
    b.add(box((0, 0.98, zd + 0.02), (0.22, 0.22, 0.04), b.m('steel_dark'), bevel=0.01))
    b.turret((0, 0.98, zd + 0.04), shape='round', size=0.2, h=0.08, barrel=0.2, n=1, rest=0.0, aim=0.0,
             mat_name='steel', gun_mat='black', flash=1.1, elev=6)
    b.add(rod((0, 1.32, zd + 0.05), (0, 1.47, zd + 0.06), 0.012, b.m('black'), seg=6))
    # the anchor at the bow
    ya = 1.18
    xa = hull.half_beam(hull.s_of(ya)) + 0.01
    b.add(rod((xa, ya, zd - 0.02), (xa + 0.01, ya + 0.02, zd - 0.12), 0.007, b.m('iron'), seg=4))
    for s_ in (-1, 1):
        b.add(rod((xa + 0.01, ya + 0.02, zd - 0.12), (xa + 0.01, ya + 0.02 + 0.035 * s_, zd - 0.09), 0.006,
                  b.m('iron'), seg=4))
    # a boat on davits beside the casemate
    davits(b, hw + 0.06, -0.62, -0.38, zd + 0.02, zd + 0.17)
    boat(b, (hw + 0.07, -0.5, zd + 0.09), L=0.26, B=0.07, mat='steel_light')

    # three fan-battened junk sails forward and a tan one on the casemate
    rig = [(0.7, 0.18, 0.62, 0.66), (0.22, 0.2, 0.8, 0.82), (-0.32, 0.26, 0.66, 0.66)]
    tops = []
    for y, z0, h, w in rig:
        zm = deck_at(hull, y)
        tops.append(b.mast(y, zm, z0 + h + 0.22, r=0.02, mat_name='wood_dark'))
        fan_sail(b, y, zm + z0, h, w, n=7)
    ym = -0.92
    top = b.mast(ym, zc + 0.1, 0.62, r=0.018, mat_name='wood_dark')
    tops.append(top)
    fan_sail(b, ym, zc + 0.2, 0.4, 0.42, n=5, tint=False, base='sail_old', alt='sail_old_b', rise=0.08)
    b.flag(tops[1] + np.array([0, 0, 0.005], np.float32), w=0.2, h=0.09, pole=False)
    b.flag(top + np.array([0, 0, 0.005], np.float32), w=0.12, h=0.06, pole=False, phase=1.0)
    # stays: from the stem to the foremast, between the mastheads, down to the stern; shrouds
    b.line((0, 1.48, zd + 0.07), tops[0], r=0.004)
    for i in range(len(tops) - 1):
        b.line(tops[i], tops[i + 1], r=0.003)
    b.line(tops[-1], (0, -1.46, zd + 0.1), r=0.003)
    for k, y in enumerate((0.7, 0.22, -0.32)):
        for sx in (-1, 1):
            b.line(tops[k] - np.array([0, 0, 0.1], np.float32),
                   (sx * hull.half_beam(hull.s_of(y)) * 0.95, y - 0.12, deck_at(hull, y) + 0.01), r=0.0025)
    b.weapon = dict(kind='turrets', t=0.18)
    b.extra_wake = 0.9


# ===================================================================== the Troop Ship

def lcvp(b, c, yaw=0.0, k=1.0):
    """A landing craft (LCVP, a Higgins boat): a square-bowed box hull, its
    ramp at the bow, the coxswain's stand aft, painted dark grey."""
    x, y, z = c
    L, B, H = 0.2 * k, 0.07 * k, 0.045 * k
    md, ml = b.m('lcvp', '#4e5258'), b.m('steel_light')
    parts = [M.rounded_box(B, L, H, md, center=(0, 0, H / 2), r=0.008 * k, seg=8),
             M.box(B * 0.82, L * 0.7, 0.004, b.m('black'), center=(0, -L * 0.05, H + 0.001)),
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
    b.add(box((0, y, zd + 0.043), (w, l, 0.008), b.m('tint' if tint else 'tarp', '#6a6e5a')))
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
    hull = steel_hull(b, 3.6, 0.6, 0.18, draft=0.1, rake=0.22, transom=0.45, n=3.6, side='steel', deck='deck_steel',
                      bow_p=2.2, stern_p=3.0, sheer_bow=0.1, planks=0.0,
                      stripes=[(0.0, 0.012, 'steel_light')], bands=((-0.004, 0.03, 'black'),))
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
    for y in np.linspace(-0.55, 0.45, 12):
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
    funnel(b, yh - 0.2, z0 + 0.22, 0.34, 0.085, 0.11, mat_name='house', cap='black', rake=0.08, band=(0.55, 0.16))
    b.smoke_stacks.append(np.array([0, yh - 0.24, z0 + 0.58], np.float32))
    # cowl vents round the funnel
    for (x, y) in ((0.15, yh - 0.36), (-0.15, yh - 0.36)):
        cowl_vent(b, x, y, z0 + 0.12, h=0.08, face=0)
    # landing craft in davits along both sides of the house
    for y in np.linspace(yh - 0.36, yh + 0.36, 4):
        for sx in (-1, 1):
            xo = sx * (0.23 + 0.06)
            b.add(rod((sx * 0.21, y - 0.07, z0 + 0.02), (sx * 0.3, y - 0.07, z0 + 0.19), 0.005, b.m('steel_dark'), seg=4))
            b.add(rod((sx * 0.21, y + 0.07, z0 + 0.02), (sx * 0.3, y + 0.07, z0 + 0.19), 0.005, b.m('steel_dark'), seg=4))
            lcvp(b, (xo, y, z0 + 0.09), yaw=0.0, k=0.9)

    # hatches fore and aft under king posts and booms; boats stacked on two of them
    for y, l in ((0.92, 0.26), (0.55, 0.3), (-0.72, 0.3), (-1.08, 0.26)):
        hatch(b, y, zd(y), 0.34, l)
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
            ids[(np.abs(C[:, 0]) > 7.0) & (N[:, 2] > -0.25)] = m['band']     # civ-colored tips
            ids[near(np.abs(C[:, 0]), (7.0,), 0.05) & (N[:, 2] > -0.25)] = m['star']
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
        parts.append(insignia(0.36, m['band'], m['star'], bar_k=0.5).transformed(
            M.translate(x + side * 0.38, -3.0, zbm + 0.05) @ M.rot_y(side * 90) @ M.rot_z(90)))
        props.append(((x, 4.2, zbm), 1.75, 3, 'y'))
        # the bomb under the wing root, on its pylon
        xbm = side * 1.35
        parts.append(M.box(0.12, 0.9, 0.32, paint, center=(xbm, 0.3, -0.38)))
        bomb = M.Mesh.concat([VP.tube((0, -0.8, 0), (0, 0.6, 0), 0.27, 0.27, m['bomb'], seg=10),
                              VP.tube((0, 0.6, 0), (0, 1.0, 0), 0.27, 0.05, m['bomb'], seg=10),
                              VP.tube((0, -1.25, 0), (0, -0.8, 0), 0.1, 0.27, m['bomb'], seg=10),
                              M.box(0.62, 0.3, 0.04, m['bomb'], center=(0, -1.15, 0)),
                              M.box(0.04, 0.3, 0.62, m['bomb'], center=(0, -1.15, 0)),
                              VP.tube((0, 0.15, 0), (0, 0.25, 0), 0.28, 0.28, m['bomb_band'], seg=10)])
        parts.append(bomb.transformed(M.translate(xbm, 0.3, -0.8)))
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
                             'bomb': '#3c3e38', 'bomb_band': '#c0a030', 'tire': '#222222'})
