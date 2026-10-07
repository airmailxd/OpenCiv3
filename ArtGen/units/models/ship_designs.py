"""Ship designs: one builder per naval unit, used by the 'ship' archetype
(models/ships.py). Each design(b, params) adds the hull, the static parts,
the sails, flags, oars, turrets and guns, and sets b.weapon.

Units: model space, bow +y, starboard +x, waterline z = 0, 'display meters'
(the archetype fits the original's length, so only proportions matter).
"""
import math

import numpy as np

import mesh as M
from models.ships import Hull, rod, box, blob, grid_mesh

DESIGNS = {}


def design(name):
    def deco(fn):
        DESIGNS[name] = fn
        return fn
    return deco


def arc_rods(b, pts, r0, r1, mat_name, seg=6):
    pts = [np.asarray(p, np.float32) for p in pts]
    n = len(pts) - 1
    for i in range(n):
        ra = r0 + (r1 - r0) * i / n
        rb = r0 + (r1 - r0) * (i + 1) / n
        b.add(rod(pts[i], pts[i + 1], ra, b.m(mat_name), seg=seg, r1=rb))
        b.add(M.sphere(rb, b.m(mat_name), center=pts[i + 1], seg=seg))


def jet(mats, size):
    """A small carrier jet (for the carrier's attack), nose +y."""
    m = mats['steel_light']
    s = size
    body = M.loft([(-0.5 * s, 0.05 * s, 0.05 * s), (0.0, 0.07 * s, 0.08 * s), (0.4 * s, 0.05 * s, 0.05 * s),
                   (0.55 * s, 0.01 * s, 0.01 * s)], m, seg=8).transformed(M.rot_x(-90))
    wing = M.plate([(-0.4 * s, -0.05 * s, 0), (0.4 * s, -0.05 * s, 0), (0.08 * s, 0.2 * s, 0), (-0.08 * s, 0.2 * s, 0)],
                   m, thick=0.004)
    tail = M.plate([(0, -0.5 * s, 0.0), (0, -0.3 * s, 0.0), (0, -0.45 * s, 0.18 * s)], m, thick=0.004)
    return M.Mesh.concat([body, wing, tail])


# ===================================================================== ancient

@design('galley')
def galley(b, p):
    hull = b.set_hull(Hull(b.mats, L=2.2, B=0.46, D=0.16, draft=0.1, hb=0.04, sheer_stern=0.14, sheer_bow=0.06,
                           bow_p=1.5, stern_p=1.6, n=2.2, stripes=[(0.0, 0.025, 'wood_light'),
                                                                     (0.075, 0.095, 'wood_dark')]))
    # curled stern post and a short stem
    arc_rods(b, [(0, -1.08, 0.26), (0, -1.18, 0.38), (0, -1.23, 0.52), (0, -1.2, 0.64), (0, -1.12, 0.7),
                 (0, -1.05, 0.67), (0, -1.05, 0.6)], 0.035, 0.015, 'wood')
    arc_rods(b, [(0, 1.08, 0.2), (0, 1.14, 0.28), (0, 1.12, 0.36)], 0.03, 0.016, 'wood')
    # bronze ram at the waterline
    b.add(rod((0, 1.0, 0.04), (0, 1.3, 0.04), 0.055, b.m('gold'), seg=8, r1=0.006))
    # the stern awning (civ color)
    b.add(box((0, -0.8, 0.25), (0.3, 0.26, 0.12), b.m('tint'), bevel=0.025))
    # steering oars
    for sx in (-1, 1):
        b.add(rod((sx * 0.17, -0.86, 0.26), (sx * 0.25, -1.12, -0.05), 0.014, b.m('wood_light'), seg=5))
    top = b.mast(0.05, 0.13, 1.32, r=0.032, top='finial')
    b.square_sail(0.08, 1.33, 1.6, 1.6, 0.8, 'pisces', billow=0.14, nu=22, nv=14)
    b.set_oars(10, -0.62, 0.66, 0.46, dip=32)
    b.weapon = dict(kind='arrows', src=(0.0, 0.1, 0.3), dir=(1.0, 0.25, 0.1), reach=1.4, n=6, volleys=3, gap=0.22,
                    t=0.1, flight=0.3)


# ===================================================================== helpers

def deck_at(hull, y):
    return hull.deck_z(hull.s_of(y))


def ports(b, hull, ys, z, size=0.05, mat_name='black', both=True):
    """Gun ports: small dark squares on the hull sides."""
    for y in ys:
        x = hull.side_x(y, z)
        for sx in ((-1, 1) if both else (1,)):
            b.add(box((sx * (x + 0.002), y, z), (0.008, size, size * 0.8), b.m(mat_name)))


def cannons(b, hull, ys, z, size=0.045, length=0.12, mat_name='iron'):
    """Deck guns along both sides poking over the rail."""
    for i, y in enumerate(ys):
        x = hull.side_x(y, z)
        for sx in (-1, 1):
            a = (sx * (x - length * 0.6), y, z)
            e = (sx * (x + length * 0.35), y, z)
            b.add(rod(a, e, size * 0.5, b.m(mat_name), seg=6, r1=size * 0.4))


def square_mast(b, y, z0, sails, gap=0.025, extra=0.18, r=0.03, flag=None, x=0.0, billow=0.11, top='crow',
                mat_name='wood_dark'):
    """A mast with stacked square sails [(w, h, emblem), ...] bottom to top,
    starting at z0 (the course's foot). Returns the mast top."""
    rh, rw = b.p.get('rig_h', 1.0), b.p.get('rig_w', 1.0)
    deck = deck_at(b.hull, y) if b.hull is not None else z0 - 0.15
    z = deck + (z0 - deck) * rh
    gap *= rh
    tops = []
    for i, (w, h, em) in enumerate(sails):
        w, h = w * rw, h * rh
        ztop = z + h
        b.square_sail(y, ztop, w * 0.92, w, h, em, billow=billow * (1 - 0.12 * i), x=x,
                      nu=max(8, int(w * 14)), nv=max(6, int(h * 18)))
        tops.append(ztop)
        z = ztop + gap
    mt = b.mast(y, deck, (z - deck + extra * rh) / rh, r=r, top=None, x=x, mat_name=mat_name)
    if top == 'crow' and len(sails) > 1:
        zc = tops[0] + gap * 0.5
        b.add(M.cylinder(r * 2.6, r * 2.6, 0.02, b.m('wood'), seg=10, z0=zc).transformed(M.translate(x, y, 0)))
    if flag:
        b.flag((x, y, mt[2] + 0.005), w=flag[0], h=flag[1], phase=y * 3)
    return mt


def jib(b, tack, head, foot_frac=0.55, emblem_kind='none', base='sail', tint='sail_tint', billow=0.04):
    tack, head = np.asarray(tack, np.float32), np.asarray(head, np.float32)
    clew = np.array([0, tack[1] + (head[1] - tack[1]) * foot_frac, tack[2] + (head[2] - tack[2]) * 0.12], np.float32)
    b.fore_aft_sail(tack, head, clew, emblem_kind=emblem_kind, billow=billow, base=base, tint=tint, nu=8, nv=8)
    b.line(tack, head, r=0.006)


def spanker(b, y, z0, z1, w, emblem_kind='none', peak_rise=0.12, billow=0.05):
    """A gaff sail aft of a mast at y: luff on the mast from z0 to z1."""
    d, rh = deck_at(b.hull, y), b.p.get('rig_h', 1.0)
    z0, z1, peak_rise = d + (z0 - d) * rh, d + (z1 - d) * rh, peak_rise * rh
    b.fore_aft_sail((0, y - 0.02, z0), (0, y - 0.02, z1), (0, y - w, z0 + 0.02), peak=(0, y - w * 0.9, z1 + peak_rise),
                    emblem_kind=emblem_kind, billow=billow)
    b.add(rod((0, y, z0), (0, y - w * 1.05, z0 + 0.01), 0.012, b.m('wood_dark')))
    b.add(rod((0, y, z1), (0, y - w * 0.95, z1 + peak_rise), 0.011, b.m('wood_dark')))


def lateen(b, y, z_low, z_high, front, back, depth, emblem_kind='none', billow=0.07):
    """A lateen sail: a long yard slanting up aft, the sail below it."""
    d, rh, rw = deck_at(b.hull, y), b.p.get('rig_h', 1.0), b.p.get('rig_w', 1.0)
    z_low, z_high, depth = d + (z_low - d) * rh, d + (z_high - d) * rh, depth * rh
    front, back = front * rw, back * rw
    fr = np.array([0, y + front, z_low], np.float32)
    bk = np.array([0, y - back, z_high], np.float32)
    clew = np.array([0, y - back * 0.35, z_low - depth], np.float32)
    b.add(rod(fr, bk, 0.012, b.m('wood_dark'), seg=6))
    b.fore_aft_sail(clew, fr, bk, peak=bk, emblem_kind=emblem_kind, billow=billow)


def shrouds(b, y, z_top, x_half, z_rail, n=3, dy=0.06):
    z_top = z_rail + (z_top - z_rail) * b.p.get('rig_h', 1.0)
    for sx in (-1, 1):
        for k in range(n):
            b.line((0, y, z_top), (sx * x_half, y - dy * (k - (n - 1) / 2) - 0.08, z_rail), r=0.004)


def castle(b, hull, y0, y1, h, mat_name='wood', top_mat='deck', rail=0.04, inset=0.0):
    """A raised deck (fore/stern castle) following the hull's beam between y0 and y1."""
    ys = np.linspace(y0, y1, 9)
    pts = []
    for y in ys:
        s = hull.s_of(y)
        pts.append((max(hull.half_beam(s) - inset, 0.02), y, hull.deck_z(s)))
    pts = np.array(pts, np.float32)
    z_top = float(pts[:, 2].max()) + h
    rows = []
    for (x, y, zd) in pts:
        rows.append([(-x, y, zd), (-x, y, z_top), (x, y, z_top), (x, y, zd)])
    P = np.array(rows, np.float32)
    side = b.m(mat_name)
    q = np.full((len(ys) - 1, 3), side, np.int32)
    q[:, 1] = b.m(top_mat)
    b.add(grid_mesh(P, q, smooth=False))
    for k in (0, -1):
        x, y, zd = pts[k]
        b.add(box((0, y, (zd + z_top) / 2), (2 * x, 0.012, z_top - zd), side))
    if rail:
        for k in (0, -1):
            x, y, zd = pts[k]
            b.add(box((0, y, z_top + rail / 2), (2 * x, 0.012, rail), b.m('rail')))
        for sx in (-1, 1):
            b.add(rod((sx * pts[0, 0], pts[0, 1], z_top + rail), (sx * pts[-1, 0], pts[-1, 1], z_top + rail), 0.01,
                      b.m('rail'), seg=4))
    return z_top


def funnel(b, y, z0, h, rx, ry, mat_name='steel', band=None, cap='black', x=0.0, rake=0.0):
    b.add(M.loft([(z0, rx, ry, x, y), (z0 + h, rx * 0.95, ry * 0.95, x, y - rake * h)], b.m(mat_name), seg=14))
    b.add(M.loft([(z0 + h - 0.001, rx * 0.8, ry * 0.8, x, y - rake * h),
                  (z0 + h + 0.004, rx * 0.8, ry * 0.8, x, y - rake * h)], b.m(cap), seg=14))
    if band:
        za = z0 + h * band[0]
        zb = za + h * band[1]
        b.add(M.loft([(za, rx * 1.04, ry * 1.04, x, y - rake * (za - z0)),
                      (zb, rx * 1.04, ry * 1.04, x, y - rake * (zb - z0))], b.m('tint'), seg=14))


def lattice_mast(b, y, z0, h, x=0.0, yard=0.25, r=0.012, mat_name='steel_dark', radar=None):
    top = np.array([x, y, z0 + h], np.float32)
    b.add(rod((x, y, z0), top, r, b.m(mat_name), seg=6, r1=r * 0.6))
    b.add(rod((x - yard / 2, y, z0 + h * 0.8), (x + yard / 2, y, z0 + h * 0.8), r * 0.6, b.m(mat_name), seg=4))
    for sx in (-1, 1):
        b.add(rod((x + sx * 0.06, y - 0.05, z0), (x, y, z0 + h * 0.6), r * 0.6, b.m(mat_name), seg=4))
    if radar:
        m = M.rounded_box(radar, 0.02, radar * 0.3, b.m('steel_light'), r=0.006, seg=8)
        b.spinner(m, (x, y, z0 + h * 0.92), speed=1.0)
    return top


def broadside(b, hull, ys, z, size=1.2, step=0.03):
    for i, y in enumerate(ys):
        x = hull.side_x(y, z)
        for sx in (-1, 1):
            b.gun((sx * (x + 0.02), y, z), (sx, 0.12, 0.03), delay=i * step, size=size)


def edge_normal(a, c):
    d = c - a
    n = np.array([d[1], -d[0], 0], np.float32)
    return n / (np.linalg.norm(n) + 1e-9)


# ===================================================================== more ancient

@design('curragh')
def curragh(b, p):
    b.set_hull(Hull(b.mats, L=1.7, B=0.44, D=0.12, draft=0.08, hb=0.03, sheer_bow=0.13, sheer_stern=0.06,
                    bow_p=1.4, stern_p=1.6, n=2.0, rake_bow=0.12, stripes=[(0.0, 0.02, 'wood_light'),
                                                                         (0.06, 0.075, 'wood_dark')]))
    b.add(rod((0, 0.8, 0.2), (0, 1.12, 0.3), 0.016, b.m('wood_dark'), seg=5))      # bowsprit
    b.add(rod((0.12, -0.7, 0.2), (0.24, -1.0, -0.04), 0.014, b.m('wood_light'), seg=5))  # steering oar
    mt = square_mast(b, -0.12, 0.5, [(0.95, 0.52, 'curragh')], extra=0.16, r=0.024, flag=(0.13, 0.07), top=None)
    square_mast(b, 0.3, 0.32, [(0.72, 0.4, 'curragh')], extra=0.14, r=0.02, top=None)
    b.line(mt, (0, 1.1, 0.3))
    b.line(mt, (0, -0.85, 0.22))
    b.weapon = dict(kind='arrows', src=(0.0, 0.0, 0.3), dir=(1.0, 0.35, 0.12), reach=2.0, n=5, volleys=2, gap=0.3,
                    t=0.2, flight=0.4)


@design('dromon')
def dromon(b, p):
    hull = b.set_hull(Hull(b.mats, L=2.7, B=0.52, D=0.17, draft=0.1, hb=0.04, sheer_stern=0.14, sheer_bow=0.06,
                           bow_p=1.5, stern_p=1.6, n=2.2, stripes=[(0.0, 0.025, 'wood_light'),
                                                                     (0.08, 0.1, 'wood_dark')]))
    arc_rods(b, [(0, -1.33, 0.3), (0, -1.43, 0.44), (0, -1.47, 0.58), (0, -1.43, 0.7), (0, -1.35, 0.74),
                 (0, -1.29, 0.7)], 0.04, 0.018, 'gold')
    b.add(rod((0, 1.22, 0.05), (0, 1.5, 0.05), 0.05, b.m('gold'), seg=8, r1=0.008))   # ram
    for (y, w, d) in ((0.95, 0.36, 0.28), (-0.98, 0.4, 0.32)):    # castles (civ color, gold trim)
        z = deck_at(hull, y)
        b.add(box((0, y, z + 0.13), (w, d, 0.26), b.m('tint'), bevel=0.02))
        b.add(box((0, y, z + 0.27), (w + 0.03, d + 0.03, 0.03), b.m('gold'), bevel=0.008))
        for sx in (-1, 1):
            for sy in (-1, 1):
                b.add(rod((sx * w / 2, y + sy * d / 2, z), (sx * w / 2, y + sy * d / 2, z + 0.3), 0.012,
                          b.m('gold'), seg=5))
    zb = deck_at(hull, 1.1) + 0.2      # the siphon: a bronze tube from the bow castle
    b.add(rod((0, 1.0, zb), (0, 1.32, zb + 0.02), 0.025, b.m('gold'), seg=8, r1=0.03))
    b.mast(0.05, 0.17, 1.95, r=0.036, top='finial')
    for zz in (0.5, 1.78, 1.95):
        b.add(M.cylinder(0.042, 0.042, 0.06, b.m('tint'), seg=10, z0=zz).transformed(M.translate(0, 0.05, 0)))
    b.square_sail(0.08, 1.72, 1.65, 1.65, 1.0, 'flame', billow=0.15, nu=22, nv=16)
    b.set_oars(12, -0.78, 0.78, 0.5, dip=32)
    b.weapon = dict(kind='fire', src=(0, 1.2, zb + 0.02), dirs=[(0, 1, -0.05), (0.9, 0.45, -0.05), (-0.9, 0.45, -0.05)],
                    reach=2.3, dur=0.3, t=0.08)


# ===================================================================== sail

@design('caravel')
def caravel(b, p):
    hull = b.set_hull(Hull(b.mats, L=2.5, B=0.66, D=0.27, draft=0.14, hb=0.06, sheer_stern=0.16, sheer_bow=0.1,
                           bow_p=1.6, stern_p=2.4, transom=0.45, rake_bow=0.18, n=2.2,
                           stripes=[(0.0, 0.03, 'wood_light'), (0.12, 0.145, 'wood_light'), (0.22, 0.25, 'wood_dark')]))
    castle(b, hull, -1.25, -0.72, 0.13)
    b.add(rod((0, 1.12, 0.36), (0, 1.62, 0.52), 0.022, b.m('wood_dark'), seg=6))    # bowsprit
    square_mast(b, 0.52, 0.5, [(0.72, 0.4, 'pattee')], extra=0.12, r=0.026, top=None)
    mt = square_mast(b, 0.0, 0.48, [(1.15, 0.6, 'pattee'), (0.66, 0.3, 'none')], extra=0.16, r=0.032,
                     flag=(0.22, 0.13))
    b.mast(-0.7, deck_at(hull, -0.7), 1.1, r=0.022)
    lateen(b, -0.7, 0.62, 1.32, 0.3, 0.6, 0.62, 'pattee')
    shrouds(b, 0.0, 1.2, 0.33, 0.27)
    b.line(mt, (0, 1.6, 0.52))
    for i, y in enumerate((0.75, 0.45)):
        for sx in (-1, 1):
            b.gun((sx * 0.3, y, 0.3), (sx * 0.35, 1, 0.05), delay=i * 0.05, size=0.9)
    b.weapon = dict(kind='cannons', t=0.2)


@design('carrack')
def carrack(b, p):
    hull = b.set_hull(Hull(b.mats, L=3.2, B=0.86, D=0.4, draft=0.16, hb=0.08, sheer_stern=0.16, sheer_bow=0.14,
                           bow_p=1.7, stern_p=2.6, transom=0.55, rake_bow=0.25, n=2.4,
                           stripes=[(0.0, 0.04, 'wood_light'), (0.17, 0.2, 'wood_dark'), (0.3, 0.34, 'wood_light')]))
    castle(b, hull, -1.62, -0.85, 0.3)
    castle(b, hull, -1.62, -1.15, 0.5, inset=0.04)
    castle(b, hull, 1.0, 1.55, 0.34)
    ports(b, hull, np.linspace(-0.6, 0.7, 5), 0.2, size=0.08)
    b.add(rod((0, 1.5, 0.5), (0, 2.05, 0.68), 0.026, b.m('wood_dark'), seg=6))   # bowsprit
    square_mast(b, 0.85, 0.72, [(0.9, 0.48, 'cross_square'), (0.5, 0.26, 'none')], extra=0.14, r=0.03, flag=(0.2, 0.08))
    mt = square_mast(b, 0.0, 0.72, [(1.36, 0.72, 'cross_square'), (0.82, 0.42, 'none')], extra=0.2, r=0.038,
                     flag=(0.3, 0.16))
    mz = b.mast(-0.85, deck_at(hull, -0.85), 1.75, r=0.026, top='crow')
    lateen(b, -0.85, 1.05, 1.65, 0.28, 0.65, 0.55)
    b.flag(mz, w=0.2, h=0.08, phase=1.0)
    shrouds(b, 0.0, 1.55, 0.44, 0.35)
    shrouds(b, 0.85, 1.25, 0.4, 0.36)
    b.line(mt, (0, 2.0, 0.68))
    broadside(b, hull, np.linspace(0.5, -0.4, 4), 0.2, step=0.04)
    b.weapon = dict(kind='cannons', t=0.2)


def three_master(b, hull, ys, stacks, z0s, emblems, flags=True, jib_tint='none', bowsprit=None, skip=(),
                 scale=1.0):
    """Square-rigged masts at ys (fore, main, mizzen) with sail stacks
    [(w, h), ...] from z0s, emblems per sail; jibs on a bowsprit."""
    tops = []
    for k, (y, stack, z0, ems) in enumerate(zip(ys, stacks, z0s, emblems)):
        if k in skip:
            tops.append(None)
            continue
        sails = [(w, h, em) for (w, h), em in zip(stack, ems)]
        tops.append(square_mast(b, y, z0, sails, extra=0.16 * scale, r=0.034 * scale,
                                flag=(0.24 * scale, 0.07 * scale) if flags else None))
    if bowsprit:
        a, e = bowsprit
        b.add(rod(a, e, 0.026 * scale, b.m('wood_dark'), seg=6))
        jib(b, e, (0, ys[0], tops[0][2] * 0.84), foot_frac=0.55, emblem_kind=jib_tint)
        jib(b, (0, (a[1] + e[1]) / 2, (a[2] + e[2]) / 2), (0, ys[0], tops[0][2] * 0.68), foot_frac=0.45,
            emblem_kind=jib_tint)
    return tops


@design('galleon')
def galleon(b, p):
    hull = b.set_hull(Hull(b.mats, L=3.2, B=0.82, D=0.4, draft=0.16, hb=0.08, sheer_stern=0.2, sheer_bow=0.1,
                           bow_p=1.7, stern_p=2.6, transom=0.6, rake_bow=0.28, n=2.4,
                           stripes=[(0.0, 0.04, 'gold'), (0.14, 0.17, 'gold'), (0.28, 0.31, 'wood_dark')]))
    castle(b, hull, -1.62, -0.8, 0.22)
    castle(b, hull, -1.62, -1.2, 0.36, inset=0.03)
    castle(b, hull, 1.0, 1.4, 0.14)
    ports(b, hull, np.linspace(-0.6, 0.8, 6), 0.22, size=0.075)
    b.add(box((0, -1.61, 0.52), (0.5, 0.02, 0.2), b.m('gold'), bevel=0.01))  # stern gallery
    three_master(b, hull, (0.85, 0.05, -0.85), [[(0.95, 0.4), (0.68, 0.32)], [(1.22, 0.45), (0.9, 0.38), (0.55, 0.26)], []],
                 (0.62, 0.62, 0.9), [['stripes', 'stripes'], ['stripes', 'stripes', 'stripes'], []],
                 bowsprit=((0, 1.45, 0.48), (0, 2.0, 0.7)), skip=(2,))
    mz = b.mast(-0.85, deck_at(hull, -0.85), 1.7, r=0.026, top='crow')
    lateen(b, -0.85, 0.95, 1.62, 0.3, 0.7, 0.6)
    b.flag(mz, w=0.22, h=0.08)
    shrouds(b, 0.05, 1.5, 0.42, 0.33)
    shrouds(b, 0.85, 1.2, 0.4, 0.34)
    broadside(b, hull, np.linspace(0.6, -0.5, 5), 0.22)
    b.weapon = dict(kind='cannons', t=0.18)


def frigate_rig(b, hull, emb, jib_tint, mizzen_em, deck_guns=True, scale=1.0, ys=(0.95, 0.0, -0.9),
                lateen_mizzen=False, z_ports=0.13):
    if deck_guns:
        cannons(b, hull, np.linspace(-0.7, 0.7, 5), deck_at(hull, 0) + 0.06)
    L2 = hull.L / 2
    tops = three_master(b, hull, ys,
                        [[(0.95 * scale, 0.4 * scale), (0.75 * scale, 0.36 * scale), (0.5 * scale, 0.24 * scale)],
                         [(1.12 * scale, 0.46 * scale), (0.9 * scale, 0.4 * scale), (0.6 * scale, 0.28 * scale)],
                         [(0.8 * scale, 0.34 * scale), (0.6 * scale, 0.3 * scale)]],
                        (0.62 * scale, 0.62 * scale, 0.72 * scale), emb,
                        bowsprit=((0, L2 - 0.05, deck_at(hull, L2 - 0.1) + 0.06),
                                  (0, L2 + 0.6 * scale, deck_at(hull, L2) + 0.28 * scale)),
                        jib_tint=jib_tint, skip=(2,) if lateen_mizzen else (), scale=scale)
    zm = deck_at(hull, ys[2])
    if lateen_mizzen:
        mz = b.mast(ys[2], zm, 1.55 * scale, r=0.026, top=None)
        lateen(b, ys[2], 0.8 * scale, 1.5 * scale, 0.3 * scale, 0.65 * scale, 0.6 * scale, mizzen_em)
        b.flag(mz, w=0.2, h=0.07)
    else:
        spanker(b, ys[2], zm + 0.2, 1.05 * scale, 0.6 * scale, mizzen_em)
    for k, y in enumerate(ys[:2]):
        zr = deck_at(hull, y) + hull.hb
        shrouds(b, y, zr + (tops[k][2] * 0.8 - zr) / b.p.get('rig_h', 1.0), hull.half_beam(hull.s_of(y)) * 0.98, zr)
    broadside(b, hull, np.linspace(0.7, -0.7, 6), z_ports, step=0.025)
    b.weapon = dict(kind='cannons', t=0.1)


@design('frigate')
def frigate(b, p):
    hull = b.set_hull(Hull(b.mats, L=2.9, B=0.72, D=0.34, draft=0.15, hb=0.08, sheer_stern=0.08, sheer_bow=0.08,
                           bow_p=1.8, stern_p=2.6, transom=0.6, rake_bow=0.2, n=2.4,
                           stripes=[(0.0, 0.035, 'wood_dark'), (0.13, 0.22, 'wood_dark'), (0.22, 0.25, 'wood_light')]))
    ports(b, hull, np.linspace(-0.9, 0.9, 8), 0.24, size=0.065)
    castle(b, hull, -1.45, -0.95, 0.08)
    frigate_rig(b, hull, [['quatrefoil', 'none', 'none'], ['quatrefoil', 'none', 'none'], ['none', 'none']],
                'stripes', 'quatrefoil', z_ports=0.24)


@design('privateer')
def privateer(b, p):
    hull = b.set_hull(Hull(b.mats, L=2.7, B=0.62, D=0.3, draft=0.14, hb=0.08, sheer_stern=0.1, sheer_bow=0.08,
                           bow_p=1.8, stern_p=2.6, transom=0.55, rake_bow=0.2, n=2.4,
                           stripes=[(0.0, 0.035, 'wood_dark'), (0.15, 0.185, 'wood_light')]))
    ports(b, hull, np.linspace(-0.8, 0.8, 7), 0.22, size=0.06)
    castle(b, hull, -1.35, -0.9, 0.08)
    frigate_rig(b, hull, [['none', 'skull', 'none'], ['skull', 'none', 'none'], ['none', 'none']],
                'none', 'skull', scale=0.92, ys=(0.85, 0.0, -0.8), lateen_mizzen=True, z_ports=0.22)


@design('man_o_war')
def man_o_war(b, p):
    hull = b.set_hull(Hull(b.mats, L=3.4, B=0.86, D=0.5, draft=0.16, hb=0.08, sheer_stern=0.08, sheer_bow=0.08,
                           bow_p=1.9, stern_p=2.8, transom=0.65, rake_bow=0.22, n=2.6,
                           stripes=[(0.0, 0.04, 'wood_dark'), (0.1, 0.15, 'gold'), (0.15, 0.25, 'black'),
                                    (0.25, 0.3, 'gold'), (0.3, 0.4, 'black'), (0.4, 0.45, 'gold')]))
    for y in np.linspace(-1.1, 1.1, 9):     # two decks of guns in their ports
        for z in (0.21, 0.36):
            x = hull.side_x(y, z)
            for sx in (-1, 1):
                b.add(box((sx * (x + 0.002), y, z), (0.008, 0.075, 0.065), b.m('black')))
                b.add(rod((sx * x, y, z), (sx * (x + 0.05), y, z), 0.02, b.m('iron'), seg=5))
    castle(b, hull, -1.7, -1.05, 0.1)
    b.add(box((0, -1.71, 0.47), (0.6, 0.02, 0.16), b.m('gold'), bevel=0.01))
    frigate_rig(b, hull, [['crest', 'none', 'none'], ['crest', 'none', 'none'], ['none', 'none']],
                'stripes', 'crest', scale=1.08, ys=(1.1, 0.0, -1.05), z_ports=0.36)


# ===================================================================== steam and steel

def steel_hull(b, L, B, D, draft=0.12, rake=0.15, transom=0.6, n=3.2, side='steel', deck='deck_steel',
               bow_p=1.9, stern_p=3.0, sheer_bow=0.05, bands=((-0.004, 0.018, 'black'),), **kw):
    kw.setdefault('planks', 0.03)
    kw.setdefault('stripes', [(0.0, 0.008, 'steel_light')])
    return b.set_hull(Hull(b.mats, L=L, B=B, D=D, draft=draft, hb=0.012, bt=0.006, sheer_bow=sheer_bow,
                           bow_p=bow_p, stern_p=stern_p, transom=transom, rake_bow=rake, n=n, side=side, deck=deck,
                           rail=side, inner=side, bands=bands, **kw))


def deck_patch(b, hull, y, w, d, dz=0.008, mat_name='tint', x=0.0):
    b.add(box((x, y, deck_at(hull, y) + dz), (w, d, 0.012), b.m(mat_name), bevel=0.004))


@design('ironclad')
def ironclad(b, p):
    hull = steel_hull(b, 3.2, 0.72, 0.07, draft=0.14, rake=-0.05, transom=0.0, n=2.2, side='steel', deck='steel',
                      bow_p=1.4, stern_p=1.4, sheer_bow=0.0, bands=())
    z = 0.08
    for y in (1.3, -1.3):                       # civ-colored bow and stern plates
        s = hull.s_of(y)
        b.add(box((0, y, z + 0.006), (hull.half_beam(s) * 1.6, 0.3, 0.012), b.m('tint'), bevel=0.004))
    b.turret((0, 0.25, z), shape='drum', size=0.42, h=0.17, barrel=0.16, n=2, rest=0.0, aim=0.0,
             mat_name='steel_dark', gun_mat='black', recoil=0.04, flash=1.4, elev=2)
    b.add(box((0, 0.95, z + 0.05), (0.14, 0.12, 0.1), b.m('steel_dark'), bevel=0.01))    # pilot house
    funnel(b, -0.45, z, 0.26, 0.05, 0.05, mat_name='black', cap='black')
    b.add(box((0, -0.3, z + 0.03), (0.22, 0.2, 0.06), b.m('steel_dark'), bevel=0.01))
    b.add(rod((0, -1.45, z), (0, -1.45, z + 0.32), 0.008, b.m('wood_dark')))
    b.flag((0, -1.45, z + 0.32), w=0.16, h=0.09, pole=False)
    b.weapon = dict(kind='turrets', t=0.15)
    b.extra_wake = 0.8


@design('transport')
def transport(b, p):
    hull = steel_hull(b, 2.2, 0.76, 0.22, draft=0.1, rake=0.04, transom=0.85, n=4.5, side='steel_dark',
                      deck='deck_steel', bow_p=3.0, stern_p=4.0, sheer_bow=0.03, stripes=[(0.06, 0.09, 'wood')], planks=0.0)
    zd = 0.235
    for y in (0.55, 0.0):                       # tarp-covered cargo
        b.add(box((0, y, zd + 0.08), (0.62, 0.5, 0.16), b.m('tarp', '#7a9a48'), bevel=0.05))
    for (x, y, w, d) in ((-0.15, 0.6, 0.3, 0.22), (0.12, -0.05, 0.28, 0.24)):
        b.add(box((x, y, zd + 0.162), (w, d, 0.012), b.m('tint'), bevel=0.004))
    for y in np.linspace(-0.2, 0.8, 6):
        b.add(rod((-0.31, y, zd + 0.16), (0.31, y, zd + 0.16), 0.006, b.m('steel_dark'), seg=4))
    b.add(box((0, -0.72, zd + 0.11), (0.6, 0.36, 0.22), b.m('steel_light'), bevel=0.02))     # bridge
    b.add(box((0, -0.68, zd + 0.27), (0.4, 0.22, 0.1), b.m('steel_light'), bevel=0.015))
    b.add(box((0, -0.565, zd + 0.27), (0.38, 0.01, 0.035), b.m('glass')))
    b.add(box((0, -0.72, zd + 0.33), (0.3, 0.12, 0.02), b.m('tint'), bevel=0.005))
    lattice_mast(b, -0.78, zd + 0.32, 0.22, yard=0.18)
    b.add(rod((0.2, -0.45, zd), (0.05, 0.1, zd + 0.42), 0.012, b.m('steel_dark')))      # crane
    b.turret((0, -0.6, zd + 0.33), shape='box', size=0.08, h=0.05, barrel=0.1, n=1, rest=0.0, aim=0.0,
             mat_name='steel', gun_mat='black', flash=0.9, elev=15)
    b.weapon = dict(kind='turrets', t=0.12)


@design('destroyer')
def destroyer(b, p):
    hull = steel_hull(b, 4.3, 0.52, 0.14, rake=0.2, sheer_bow=0.06)
    zd = lambda y: deck_at(hull, y)
    deck_patch(b, hull, 1.4, 0.26, 0.3)
    deck_patch(b, hull, -1.9, 0.36, 0.3)
    b.turret((0, 1.12, zd(1.12)), size=0.16, barrel=0.22, n=1, rest=0, aim=0, flash=1.0, mat_name='steel_light')
    b.add(box((0, 0.82, zd(0.82) + 0.03), (0.2, 0.2, 0.06), b.m('steel'), bevel=0.01))
    b.turret((0, 0.82, zd(0.82) + 0.06), size=0.16, barrel=0.22, n=1, rest=0, aim=0, delay=0.05, mat_name='steel_light')
    b.add(box((0, -1.2, zd(-1.2) + 0.03), (0.2, 0.2, 0.06), b.m('steel'), bevel=0.01))
    b.turret((0, -1.2, zd(-1.2) + 0.06), size=0.16, barrel=0.22, n=1, rest=180, aim=150, delay=0.1, mat_name='steel_light')
    b.turret((0, -1.5, zd(-1.5)), size=0.16, barrel=0.22, n=1, rest=180, aim=-150, delay=0.15, mat_name='steel_light')
    b.add(box((0, 0.3, zd(0.3) + 0.1), (0.36, 0.55, 0.2), b.m('steel'), bevel=0.02))        # superstructure
    b.add(box((0, 0.42, zd(0.4) + 0.25), (0.28, 0.24, 0.1), b.m('steel_light'), bevel=0.015))
    b.add(box((0, 0.545, zd(0.4) + 0.27), (0.24, 0.01, 0.03), b.m('glass')))
    for sx in (-1, 1):
        b.add(box((sx * 0.183, 0.25, zd(0.3) + 0.1), (0.006, 0.22, 0.08), b.m('tint')))
    lattice_mast(b, 0.3, zd(0.3) + 0.3, 0.32, yard=0.24, radar=0.14)
    funnel(b, -0.2, zd(-0.2), 0.26, 0.09, 0.13, mat_name='steel', cap='black', rake=0.1)
    b.add(box((0, -0.55, zd(-0.55) + 0.08), (0.3, 0.4, 0.16), b.m('steel'), bevel=0.02))
    lattice_mast(b, -0.55, zd(-0.55) + 0.16, 0.24, yard=0.16, radar=0.1)
    for y in (-0.85, -0.0):                    # torpedo tubes
        for sx in (-1, 1):
            b.add(rod((sx * 0.14, y, zd(y) + 0.04), (sx * 0.14, y + 0.25, zd(y) + 0.06), 0.022, b.m('steel_dark')))
    b.flag((0, -2.05, zd(-2.0) + 0.22), w=0.14, h=0.08)
    clutter(b, hull, -1.0, 0.6, 14, seed=1)
    clutter(b, hull, 0.15, 0.45, 5, seed=2, xw=0.5, z_on=lambda y: zd(0.3) + 0.2)
    b.weapon = dict(kind='turrets', t=0.15)


@design('cruiser')
def cruiser(b, p):
    hull = steel_hull(b, 5.1, 0.56, 0.15, rake=0.25, deck='deck_wood', sheer_bow=0.07)
    zd = lambda y: deck_at(hull, y)
    b.turret((0, 1.55, zd(1.55)), size=0.22, barrel=0.3, n=3, rest=0, aim=0, flash=1.2, mat_name='steel_light')
    b.add(box((0, 1.15, zd(1.15) + 0.035), (0.24, 0.24, 0.07), b.m('steel'), bevel=0.01))
    b.turret((0, 1.15, zd(1.15) + 0.07), size=0.22, barrel=0.3, n=3, rest=0, aim=0, delay=0.06, flash=1.2, mat_name='steel_light')
    b.turret((0, -1.45, zd(-1.45)), size=0.22, barrel=0.3, n=3, rest=180, aim=145, delay=0.12, flash=1.2, mat_name='steel_light')
    b.add(box((0, 0.55, zd(0.55) + 0.1), (0.34, 0.5, 0.2), b.m('steel'), bevel=0.02))
    b.add(box((0, 0.62, zd(0.6) + 0.27), (0.24, 0.28, 0.14), b.m('steel_light'), bevel=0.02))
    b.add(box((0, 0.62, zd(0.6) + 0.355), (0.25, 0.29, 0.03), b.m('steel_dark'), bevel=0.006))
    b.add(box((0, 0.765, zd(0.6) + 0.29), (0.2, 0.01, 0.03), b.m('glass')))
    lattice_mast(b, 0.55, zd(0.55) + 0.37, 0.4, yard=0.26, radar=0.12)
    funnel(b, 0.05, zd(0.05), 0.3, 0.07, 0.1, band=(0.6, 0.18), rake=0.12)
    funnel(b, -0.4, zd(-0.4), 0.3, 0.07, 0.1, band=(0.6, 0.18), rake=0.12)
    b.add(box((0, -0.2, zd(-0.2) + 0.06), (0.3, 0.9, 0.12), b.m('steel'), bevel=0.02))
    b.add(box((0, -0.85, zd(-0.85) + 0.1), (0.28, 0.32, 0.2), b.m('steel'), bevel=0.02))
    lattice_mast(b, -0.85, zd(-0.85) + 0.2, 0.3, yard=0.18)
    for y in (-0.15, 0.25):
        for sx in (-1, 1):
            b.turret((sx * 0.2, y, zd(y)), size=0.09, barrel=0.12, n=2, rest=sx * -90, aim=sx * -40, delay=0.08,
                     flash=0.7)
    deck_patch(b, hull, 2.2, 0.2, 0.25)
    b.flag((0, -2.4, zd(-2.4) + 0.22), w=0.14, h=0.08)
    clutter(b, hull, -1.1, 0.9, 18, seed=3)
    clutter(b, hull, -0.6, 0.2, 6, seed=4, xw=0.4, z_on=lambda y: zd(-0.2) + 0.12)
    b.weapon = dict(kind='turrets', t=0.05)


@design('aegis')
def aegis(b, p):
    hull = steel_hull(b, 3.9, 0.52, 0.2, rake=0.22, sheer_bow=0.05, side='steel_dark', deck='deck_steel')
    zd = lambda y: deck_at(hull, y)
    b.turret((0, 1.25, zd(1.25)), shape='round', size=0.16, barrel=0.24, n=1, rest=0, aim=0, flash=1.0,
             mat_name='steel_light')
    for y0 in (0.85, -1.2):                    # vertical launch cells
        b.add(box((0, y0, zd(y0) + 0.02), (0.26, 0.3, 0.04), b.m('steel_dark'), bevel=0.01))
        for i in range(4):
            for j in range(3):
                b.add(box((-0.09 + j * 0.09, y0 - 0.11 + i * 0.075, zd(y0) + 0.042), (0.06, 0.05, 0.006),
                          b.m('black')))
    b.add(box((0, 0.2, zd(0.2) + 0.15), (0.44, 0.7, 0.3), b.m('steel_light'), bevel=0.02))     # deckhouse
    b.add(box((0, 0.3, zd(0.3) + 0.38), (0.34, 0.4, 0.18), b.m('steel_light'), bevel=0.02))
    for sx in (-1, 1):                         # civ-colored radar faces
        b.add(box((sx * 0.172, 0.4, zd(0.3) + 0.39), (0.006, 0.14, 0.12), b.m('tint')))
    b.add(box((0, 0.502, zd(0.3) + 0.39), (0.14, 0.006, 0.12), b.m('tint')))
    b.add(box((0, 0.556, zd(0.2) + 0.25), (0.3, 0.01, 0.04), b.m('glass')))
    lattice_mast(b, 0.25, zd(0.25) + 0.47, 0.42, yard=0.26, radar=0.12)
    for (x, y, z, r) in ((0.15, 0.05, 0.35, 0.05), (-0.15, -0.0, 0.35, 0.05), (0, -0.55, 0.32, 0.055)):
        b.add(M.sphere(r, b.m('white'), center=(x, y, zd(y) + z), seg=12))
        b.add(M.cylinder(r * 0.5, r * 0.5, z - r * 0.8, b.m('steel'), seg=8).transformed(M.translate(x, y, zd(y))))
    funnel(b, -0.3, zd(-0.3) + 0.1, 0.22, 0.1, 0.07, mat_name='steel_light', cap='black')
    b.add(box((0, -0.5, zd(-0.5) + 0.1), (0.4, 0.5, 0.2), b.m('steel_light'), bevel=0.02))
    b.add(box((0, -0.62, zd(-0.5) + 0.21), (0.3, 0.2, 0.02), b.m('tint'), bevel=0.005))
    lattice_mast(b, -0.55, zd(-0.55) + 0.2, 0.3, yard=0.16)
    deck_patch(b, hull, -1.75, 0.4, 0.3)
    deck_patch(b, hull, 1.7, 0.16, 0.2)
    clutter(b, hull, -0.9, 0.0, 8, seed=5, xw=0.7)
    clutter(b, hull, 1.45, 1.75, 3, seed=6, xw=0.5)
    clutter(b, hull, -0.1, 0.45, 5, seed=7, xw=0.45, z_on=lambda y: zd(0.2) + 0.3)
    b.weapon = dict(kind='turrets', t=0.18, extra=[dict(kind='missile', src=(0.0, 0.85, zd(0.85) + 0.05), dt=0.12,
                                                       dur=0.5)])


@design('battleship')
def battleship(b, p):
    hull = steel_hull(b, 5.0, 0.72, 0.16, rake=0.28, sheer_bow=0.08, bow_p=1.7, deck='deck_wood')
    zd = lambda y: deck_at(hull, y)
    deck_patch(b, hull, 2.05, 0.36, 0.36, dz=0.012)
    deck_patch(b, hull, -2.15, 0.5, 0.36, dz=0.012)
    b.turret((0, 1.45, zd(1.45)), size=0.3, barrel=0.42, n=3, rest=0, aim=0, flash=1.6, h=0.12, mat_name='steel_light')
    b.add(box((0, 1.0, zd(1.0) + 0.05), (0.32, 0.32, 0.1), b.m('steel'), bevel=0.01))
    b.turret((0, 1.0, zd(1.0) + 0.1), size=0.3, barrel=0.42, n=3, rest=0, aim=0, delay=0.07, flash=1.6, h=0.12, mat_name='steel_light')
    b.turret((0, -1.5, zd(-1.5)), size=0.3, barrel=0.42, n=3, rest=180, aim=140, delay=0.14, flash=1.6, h=0.12, mat_name='steel_light')
    b.add(box((0, 0.1, zd(0.1) + 0.09), (0.5, 1.4, 0.18), b.m('steel'), bevel=0.02))
    b.add(box((0, 0.35, zd(0.3) + 0.26), (0.34, 0.5, 0.18), b.m('steel_light'), bevel=0.02))
    b.add(box((0, 0.5, zd(0.5) + 0.43), (0.2, 0.24, 0.2), b.m('tint'), bevel=0.02))
    b.add(box((0, 0.625, zd(0.5) + 0.45), (0.16, 0.01, 0.03), b.m('glass')))
    lattice_mast(b, 0.45, zd(0.45) + 0.52, 0.32, yard=0.28, radar=0.14)
    funnel(b, 0.0, zd(0.0) + 0.18, 0.2, 0.09, 0.12, rake=0.1)
    funnel(b, -0.35, zd(-0.35) + 0.18, 0.18, 0.08, 0.11, rake=0.1)
    b.add(box((0, -0.65, zd(-0.65) + 0.16), (0.3, 0.3, 0.14), b.m('steel'), bevel=0.02))
    lattice_mast(b, -0.65, zd(-0.65) + 0.22, 0.26, yard=0.18)
    for y in (0.55, 0.1, -0.35):
        for sx in (-1, 1):
            b.turret((sx * 0.27, y, zd(y) + 0.18), size=0.1, barrel=0.12, n=2,
                     rest=sx * -90, aim=sx * -45, delay=0.1, flash=0.7, mat_name='steel_light')
    for y in (-0.95, 0.75):                    # armored box launchers
        for sx in (-1, 1):
            b.add(box((sx * 0.17, y, zd(y) + 0.06), (0.12, 0.26, 0.08), b.m('steel_light'), bevel=0.01))
    clutter(b, hull, -0.9, 0.7, 16, seed=8, xw=0.45, z_on=lambda y: zd(0.1) + 0.18)
    clutter(b, hull, -1.2, 1.25, 10, seed=9, xw=0.85)
    b.weapon = dict(kind='turrets', t=0.18)
    b.extra_wake = 1.1


@design('carrier')
def carrier(b, p):
    steel_hull(b, 5.0, 0.7, 0.3, rake=0.3, sheer_bow=0.0, bow_p=1.6, stern_p=4.0, transom=0.8)
    zf = 0.36
    # the flight deck: wider than the hull, with the angled deck to port
    poly = [(-0.4, -2.55), (0.48, -2.55), (0.55, 0.6), (0.36, 2.35), (0.0, 2.6), (-0.3, 2.35), (-0.36, 1.2),
            (-0.72, 0.4), (-0.76, -0.3), (-0.46, -1.1)]
    P = np.array([(x, y, zf) for x, y in poly], np.float32)
    b.add(M.plate(P, b.m('flight', '#8a8c88'), thick=0.0))
    b.add(M.plate(P - (0, 0, 0.05), b.m('steel_dark'), thick=0.0))
    n = len(P)
    ms = b.m('steel')
    for i in range(n):
        a, c = P[i], P[(i + 1) % n]
        d = np.array([0, 0, 0.05], np.float32)
        en = edge_normal(a, c)
        b.add(M.Mesh(np.array([[a, c, c - d], [a, c - d, a - d]], np.float32),
                     np.repeat(np.repeat(en[None, None], 3, 1), 2, 0), np.full(2, ms, np.int32)))
    b.add(box((0, 0.0, (zf + 0.25) / 2), (0.74, 4.6, zf - 0.25), ms, bevel=0.02))
    lm = b.m('marking', '#e8e2c8')                  # deck markings
    for (x0, y0, x1, y1) in ((0.0, -2.3, 0.0, 2.3), (-0.05, -1.6, -0.55, 0.45), (0.25, 0.9, 0.12, 2.3),
                             (-0.12, 1.0, -0.2, 2.25)):
        b.add(rod((x0, y0, zf + 0.004), (x1, y1, zf + 0.004), 0.008, lm, seg=4))
    b.add(rod((-0.35, -2.0, zf + 0.004), (-0.6, 0.0, zf + 0.004), 0.006, b.m('mark_y', '#d8b040'), seg=4))
    for k in range(6):                              # civ-colored parking rows / elevators
        b.add(box((0.38, -1.9 + k * 0.32, zf + 0.008), (0.12, 0.2, 0.012), b.m('tint')))
    b.add(box((-0.62, -0.25, zf - 0.02), (0.18, 0.4, 0.04), b.m('tint')))
    b.add(box((0.36, -0.3, zf + 0.13), (0.14, 0.6, 0.26), ms, bevel=0.02))       # the island
    b.add(box((0.36, -0.22, zf + 0.31), (0.12, 0.34, 0.1), b.m('steel_light'), bevel=0.015))
    b.add(box((0.36, -0.05, zf + 0.31), (0.12, 0.01, 0.03), b.m('glass')))
    b.add(rod((0.36, -0.3, zf + 0.36), (0.36, -0.3, zf + 0.62), 0.012, b.m('steel_dark')))
    b.add(rod((0.26, -0.3, zf + 0.55), (0.46, -0.3, zf + 0.55), 0.008, b.m('steel_dark')))
    b.spinner(M.rounded_box(0.12, 0.02, 0.04, b.m('steel_light'), r=0.006, seg=8), (0.36, -0.3, zf + 0.63))
    for k, (x, y, yaw) in enumerate(((0.3, 0.4, 150), (0.3, 0.75, 150), (0.32, 1.1, 150), (-0.05, -2.1, 0),
                                     (0.2, -2.1, 0), (-0.25, -1.9, 0), (0.28, 1.5, 160))):
        b.add(jet(b.mats, 0.2).transformed(M.translate(x, y, zf + 0.02) @ M.rot_z(yaw)))
    for (x, y) in ((-0.05, 1.8), (0.15, 1.95), (-0.2, 2.1)):
        b.add(box((x, y, zf + 0.005), (0.1, 0.004, 0.006), b.m('black')))
    b.weapon = dict(kind='jet', y0=-0.9, y1=2.4, x=-0.1, z=zf + 0.02, t=0.15, dur=0.55, size=0.22)


def sub(b, L, B, tower_y, tower_l, tower_h, nuclear=False):
    hull = b.set_hull(Hull(b.mats, L=L, B=B, D=0.09, draft=0.14, hb=0.0, bow_p=2.2, stern_p=1.7,
                           bow_e=0.6, stern_e=0.75, n=2.0, side='hull_sub', deck='hull_sub', bulwark=False,
                           boot=None, camber=0.05))
    zd = 0.1
    if nuclear:
        b.add(M.loft([(zd - 0.04, B * 0.16, tower_l * 0.5, 0, tower_y),
                      (zd + tower_h, B * 0.13, tower_l * 0.42, 0, tower_y)], b.m('hull_sub'), seg=16))
        b.add(box((0, tower_y, zd + tower_h * 0.62), (B * 1.1, 0.09, 0.014), b.m('hull_sub')))  # sail planes
        b.add(box((0, tower_y, zd + tower_h + 0.006), (B * 0.24, tower_l * 0.7, 0.014), b.m('tint')))
        b.add(box((0, tower_y, zd + tower_h * 0.84), (B * 0.27, tower_l * 0.75, tower_h * 0.16), b.m('tint')))
        b.add(rod((0, tower_y - 0.03, zd + tower_h), (0, tower_y - 0.03, zd + tower_h + 0.12), 0.01,
                  b.m('steel_dark')))
        b.add(box((0, -L / 2 + 0.15, 0.08), (0.025, 0.16, 0.14), b.m('hull_sub')))   # rudder
    else:
        b.add(M.loft([(zd - 0.03, B * 0.22, tower_l * 0.5, 0, tower_y),
                      (zd + tower_h, B * 0.19, tower_l * 0.42, 0, tower_y)], b.m('hull_sub'), seg=14))
        b.add(M.loft([(zd + tower_h * 0.62, B * 0.2 + 0.004, tower_l * 0.445 + 0.004, 0, tower_y),
                      (zd + tower_h * 0.86, B * 0.195 + 0.004, tower_l * 0.43 + 0.004, 0, tower_y)], b.m('tint'), seg=14))
        b.add(M.loft([(zd + tower_h - 0.002, B * 0.19, tower_l * 0.42, 0, tower_y),
                      (zd + tower_h + 0.012, B * 0.19, tower_l * 0.42, 0, tower_y)], b.m('dark'), seg=14))
        for dy, h in ((-0.05, 0.28), (0.04, 0.2)):
            b.add(rod((0, tower_y + dy, zd + tower_h), (0, tower_y + dy, zd + tower_h + h), 0.012, b.m('steel_dark')))
        b.add(rod((0, tower_y + 0.4, zd), (0, tower_y + 0.4, zd + 0.07), 0.02, b.m('steel_dark')))   # deck gun
        b.add(rod((0, tower_y + 0.37, zd + 0.07), (0, tower_y + 0.62, zd + 0.08), 0.015, b.m('steel_dark')))
        b.add(box((0, 0.1, zd), (0.1, L * 0.6, 0.03), b.m('hull_sub_dark', '#4e7c6c')))   # casing
    b.weapon = dict(kind='torpedo', src=(0.0, L / 2 - 0.05, 0.0), reach=2.2, dur=0.55, t=0.12)
    b.extra_wake = 0.7
    return hull


@design('submarine')
def submarine(b, p):
    sub(b, 3.1, 0.42, 0.0, 0.5, 0.26)


@design('nuclear_sub')
def nuclear_sub(b, p):
    sub(b, 3.4, 0.36, 0.75, 0.42, 0.28, nuclear=True)


def clutter(b, hull, y0, y1, n, seed=0, xw=0.6, z_on=None, size=(0.05, 0.1), mats=('steel_dark', 'steel_light')):
    """Small deck fittings (vents, lockers, boats, winches): deterministic
    boxes and drums scattered over the deck between y0 and y1, within xw of
    the half beam. z_on(y) -> the surface height (default: the deck)."""
    rng = np.random.default_rng(seed)
    for i in range(n):
        y = rng.uniform(y0, y1)
        hb = hull.half_beam(hull.s_of(y)) * xw
        x = rng.uniform(-hb, hb)
        z = deck_at(hull, y) if z_on is None else z_on(y)
        s = rng.uniform(*size)
        m = b.m(mats[i % len(mats)])
        if rng.random() < 0.3:
            b.add(M.cylinder(s * 0.45, s * 0.45, s * 0.8, m, seg=8, z0=z).transformed(M.translate(x, y, 0)))
        else:
            b.add(box((x, y, z + s * 0.35), (s * rng.uniform(0.6, 1.4), s * rng.uniform(0.6, 1.6), s * 0.7), m))
