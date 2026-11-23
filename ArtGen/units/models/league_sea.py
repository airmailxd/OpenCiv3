"""The League modes' ships (catalog/league_mdj.py): MDJ's Warship, drawn
as a Japanese atakebune, the Sengoku period's floating fortress (Oda
Nobunaga's at Kizugawaguchi, 1578; the Tokugawa Atake Maru).

Registers the ship archetype's design 'atakebune' (models/ships.py): a
broad wasen hull with a raked stem and a square transom; its deck boxed in
by a plank fortress (tate-ita walls between black pillars, two rows of
ports for arquebuses and bows); a two-storey castle tower (yagura) of white
plaster and black timber under tiled hip-and-gable (irimoya) roofs with
upturned eaves, gold shachi on the ridge and a civ-colored curtain (maku)
round its foot; one mast with a big matting sail (horizontal battens,
vertical cloth strips, a civ-colored mon: three scales in a ring);
civ-colored nobori banners along the walls and a streamer at the stern;
sculling oars under the walls. It fights with arquebus volleys from the
ports and arrows, like the Galley it is calibrated against.

Also holds `paint`, which the other round-2 ships and planes here use to
draw panels, planks and seams onto a part (models/modern_mod_sea_air.py,
models/league_air.py).
"""
import math

import numpy as np

import mesh as M
from models import vparts as VP
from models.ship_designs import design, Hull, deck_at
from models.ships import box, rod


# ------------------------------------------------------------ shared helpers

def paint(mesh, fn, max_edge=0.02):
    """Subdivides a mesh to max_edge and recolors each triangle by
    fn(centers (n, 3), current material ids (n,)) -> material ids."""
    m = VP.subdivide(mesh, max_edge)
    m.M = np.asarray(fn(m.V.mean(1), m.M.copy()), np.int32)
    return m


def tri(a, b, c, mat, thick=0.004):
    """A thin triangular plate."""
    return M.plate([a, b, c], mat, thick=thick)


# ------------------------------------------------------------ the atakebune

def irimoya(b, c, hx, hy, h, roof='roof', ridge='roof_dark', gable='wood_black', up=0.035, eave=0.018, gold=None):
    """A tiled hip-and-gable roof whose ridge runs along y: the eaves at
    (x +- hx, y +- hy) curve up at the corners, the slope steepens toward
    the ridge, small gables close its upper ends, the ridge carries end
    tiles (onigawara) and, if given, gold shachi."""
    x0, y0, z0 = c
    rings = [(z0 - eave, hx, hy), (z0, hx, hy)]
    n = 7
    for i in range(1, n + 1):
        u = i / n
        rings.append((z0 + h * u ** 1.6, max(hx * (1 - u), 0.006), max(hy - hx * u * 0.92, 0.01)))
    sq = []
    for z, ax, ay in rings:
        fx, fy = M.superellipse(ax, ay, 10.0)
        sq.append((z, fx, fy))
    m = M.loft(sq, b.m(roof), seg=32, smooth=False)
    # the tiles: ribs of round tiles running down the slope, the eave's tile ends lighter
    mr, md = b.m(roof), b.m(ridge)
    mrb = b.m('roof_b', '#3a3c42')
    me = b.m('roof_edge', '#8c8e92')

    def ribs(C, ids):
        side = np.abs(C[:, 0]) / hx > np.abs(C[:, 1]) / hy - (hy - hx) / hy
        along = np.where(side, C[:, 1], C[:, 0])
        ids[(ids == mr) & (np.floor(along / 0.022) % 2 == 1)] = mrb
        ids[C[:, 2] < z0 - eave * 0.5] = md
        ids[(C[:, 2] >= z0 - eave * 0.5) & (C[:, 2] < z0 + 0.001)] = me
        return ids
    m = paint(m, ribs, max_edge=0.014)
    # upturned eaves: lift the low corners
    V = m.V
    cx = np.clip(np.abs(V[..., 0]) / hx, 0, 1)
    cy = np.clip(np.abs(V[..., 1]) / hy, 0, 1)
    low = np.clip(1 - (V[..., 2] - (z0 - eave)) / (h * 0.5), 0, 1)
    V[..., 2] += up * np.maximum(cx, cy) ** 6 * (cx * cy) ** 1.5 * low
    b.add(m.transformed(M.translate(x0, y0, 0)))
    # the gables at the ridge's ends
    zg0, zg1 = z0 + h * 0.5 ** 1.6, z0 + h
    for s in (-1, 1):
        yg = y0 + s * (hy - hx * 0.5 * 0.92 - 0.004)
        wg = hx * 0.5
        b.add(tri((x0 - wg * 0.9, yg, zg0), (x0 + wg * 0.9, yg, zg0), (x0, yg, zg1 + 0.004), b.m(gable), 0.006))
    # the ridge and its end tiles
    ry = hy - hx * 0.92
    b.add(box((x0, y0, z0 + h + 0.008), (0.03, 2 * ry + 0.02, 0.022), md, bevel=0.005))
    for s in (-1, 1):
        b.add(box((x0, y0 + s * (ry + 0.012), z0 + h + 0.018), (0.04, 0.02, 0.04), md, bevel=0.006))
        if gold:
            # shachi: a gold fish standing on its head at each end of the ridge
            fy = y0 + s * (ry + 0.012)
            b.add(rod((x0, fy, z0 + h + 0.03), (x0, fy + s * 0.012, z0 + h + 0.07), 0.012, b.m(gold), seg=6, r1=0.007))
            b.add(rod((x0, fy + s * 0.012, z0 + h + 0.07), (x0, fy - s * 0.008, z0 + h + 0.085), 0.008, b.m(gold),
                      seg=5, r1=0.003))


def mon_sail(mats, aspect, battens=6, strips=9):
    """The atakebune's sail by (u, v): vertical cloth strips in two tones,
    dark battens across, a dark boltrope round the edge and the civ-colored
    mon (a ring round three scales) in the middle."""
    base, alt, dark, tint = mats['sail'], mats['sail_b'], mats['batten'], mats['sail_tint']

    def f(u, v):
        x, y = 2 * u - 1, 2 * v - 1
        X, Y = x * aspect, y
        if u < 0.02 or u > 0.98 or v > 0.975:
            return dark
        # the mon: a ring and three scales (mitsu-uroko)
        # (a civ-colored disc, the scales left in the cloth, a cloth ring inside its edge)
        r = math.hypot(X, Y + 0.02)
        if r < 0.66:
            if 0.5 < r < 0.56:
                return base
            for cx, cy, s in ((0.0, -0.17, 0.3), (-0.17, 0.13, 0.3), (0.17, 0.13, 0.3)):
                dy = Y - cy
                if -s * 0.5 < dy < s * 0.5 and abs(X - cx) < (dy + s * 0.5) * 0.62:
                    return base
            return tint
        bt = v * battens
        if 0.12 < v and bt - math.floor(bt) < 0.1:
            return dark
        return alt if int(u * strips) % 2 else base
    return f


@design('atakebune')
def atakebune(b, p):
    b.m('wood_black', '#2e241c')
    b.m('plaster', '#e6e0d0')
    b.m('roof', '#55575c')
    b.m('roof_dark', '#2e3034')
    b.m('wall', '#7a5e42')
    b.m('wall_b', '#6c5238')
    b.m('sail_b', '#c6b68c')
    b.m('batten', '#5a4830')
    hull = b.set_hull(Hull(b.mats, L=2.45, B=0.64, D=0.13, draft=0.1, hb=0.02, sheer_stern=0.06, sheer_bow=0.1,
                           bow_p=2.0, stern_p=3.2, transom=0.62, rake_bow=0.16, n=3.2,
                           stripes=[(0.0, 0.022, 'wood_dark'), (0.06, 0.08, 'wood_dark')]))
    zd = deck_at(hull, 0.0)
    WX, WY, WH, wy0 = 0.35, 0.86, 0.24, -0.1         # the fortress: half-width, half-length, wall height, centre
    zb = zd + 0.005
    zt = zb + WH
    mwb = b.m('wood_black')
    # the fortress floor's edge overhanging the hull, and the deck above the walls
    b.add(box((0, wy0, zb + 0.012), (2 * WX + 0.03, 2 * WY + 0.03, 0.024), mwb, bevel=0.004))
    b.add(box((0, wy0, zt + 0.006), (2 * WX + 0.02, 2 * WY + 0.02, 0.016), b.m('deck'), bevel=0.003))
    for sx in (-1, 1):
        b.add(box((sx * (WX + 0.004), wy0, zt + 0.012), (0.03, 2 * WY + 0.04, 0.02), mwb, bevel=0.003))
    for sy in (-1, 1):
        b.add(box((0, wy0 + sy * (WY + 0.004), zt + 0.012), (2 * WX + 0.04, 0.03, 0.02), mwb, bevel=0.003))

    # the walls: vertical planks in two tones, ports in two rows, a black beam between
    mw, mwb2 = b.m('wall'), b.m('wall_b')
    walls = box((0, wy0, zb + WH / 2 + 0.012), (2 * WX, 2 * WY, WH - 0.012), mw)

    def planks(C, ids):
        along = np.where(np.abs(np.abs(C[:, 0]) - WX) < 0.003, C[:, 1], C[:, 0])
        ids[(np.floor(along / 0.055) % 2 == 1)] = mwb2
        ids[np.abs(C[:, 2] - (zb + WH * 0.52)) < 0.012] = mwb
        return ids
    b.add(paint(walls, planks, max_edge=0.03))
    # the pillars
    ys = np.linspace(wy0 - WY, wy0 + WY, 8)
    for y in ys:
        for sx in (-1, 1):
            b.add(box((sx * (WX + 0.004), y, zb + WH / 2 + 0.01), (0.022, 0.026, WH), mwb))
    for x in np.linspace(-WX, WX, 4):
        for sy in (-1, 1):
            b.add(box((x, wy0 + sy * (WY + 0.004), zb + WH / 2 + 0.01), (0.026, 0.022, WH), mwb))
    # the ports, between the pillars
    mb = b.m('black', '#141210')
    port_pts = []
    for i in range(len(ys) - 1):
        for k in (1, 2):
            y = ys[i] + (ys[i + 1] - ys[i]) * k / 3
            for sx in (-1, 1):
                b.add(box((sx * (WX + 0.003), y, zb + WH * 0.75), (0.008, 0.03, 0.03), mb))
                b.add(box((sx * (WX + 0.003), y, zb + WH * 0.3), (0.008, 0.026, 0.04), mb))
            port_pts.append(y)
    for x in (-0.2, 0.0, 0.2):
        for sy in (-1, 1):
            b.add(box((x, wy0 + sy * (WY + 0.003), zb + WH * 0.75), (0.03, 0.008, 0.03), mb))

    # a rail round the upper deck
    zr = zt + 0.022
    for sx in (-1, 1):
        b.add(rod((sx * WX, wy0 - WY, zr + 0.035), (sx * WX, wy0 + WY, zr + 0.035), 0.007, mwb, seg=4))
        for y in np.linspace(wy0 - WY, wy0 + WY, 10):
            b.add(rod((sx * WX, y, zr), (sx * WX, y, zr + 0.035), 0.005, mwb, seg=4))
    for sy in (-1, 1):
        b.add(rod((-WX, wy0 + sy * WY, zr + 0.035), (WX, wy0 + sy * WY, zr + 0.035), 0.007, mwb, seg=4))

    # the deck: planks along the ship, two hatches, a windlass at the bow, water casks
    def deck_planks(C, ids):
        ids[np.floor(C[:, 0] / 0.045) % 2 == 1] = b.m('deck_b', '#7c6648')
        return ids
    b.add(paint(box((0, wy0, zt + 0.0145), (2 * WX - 0.01, 2 * WY - 0.01, 0.002), b.m('deck')), deck_planks,
                max_edge=0.04))
    for y in (0.0, 0.48):
        b.add(box((0, y, zt + 0.022), (0.2, 0.16, 0.016), mwb, bevel=0.003))
        b.add(box((0, y, zt + 0.031), (0.17, 0.13, 0.006), b.m('wood_light'), bevel=0.002))
    b.add(M.cylinder(0.03, 0.03, 0.2, b.m('wood'), seg=10, z0=-0.1).transformed(
        M.translate(0, 0.62, zt + 0.05) @ M.rot_y(90)))
    for sx in (-1, 1):
        b.add(box((sx * 0.11, 0.62, zt + 0.035), (0.02, 0.05, 0.05), mwb))
    for (x, y) in ((0.22, -0.9), (0.15, -0.95), (-0.22, -0.92)):
        b.add(M.cylinder(0.035, 0.035, 0.06, b.m('wood_light'), seg=10, z0=zt + 0.015).transformed(M.translate(x, y, 0)))
        b.add(M.cylinder(0.037, 0.037, 0.01, mwb, seg=10, z0=zt + 0.04).transformed(M.translate(x, y, 0)))

    # the tower (yagura) aft of the mast: three storeys of white plaster and black
    # timber, each under its own irimoya roof, the top one with gold shachi
    yt = -0.42

    def storey(z0, hx, hy, hh, posts=0.11):
        m = box((0, yt, z0 + hh / 2), (2 * hx, 2 * hy, hh), b.m('plaster'))

        def timbers(C, ids):
            k = (C[:, 2] - z0) / hh
            ids[(k > 0.48) & (k < 0.7)] = mwb                        # the band of windows
            along = np.where(np.abs(np.abs(C[:, 0]) - hx) < 0.003, C[:, 1] - yt, C[:, 0])
            ids[(np.abs((along / posts) - np.round(along / posts)) < 0.08)] = mwb   # posts
            ids[(k < 0.1) | (k > 0.92)] = mwb
            return ids
        b.add(paint(m, timbers, max_edge=0.01))
        # lattice bars across the window band
        n = max(3, int(2 * hx / 0.035))
        for x in np.linspace(-hx + 0.015, hx - 0.015, n):
            for sy in (-1, 1):
                b.add(box((x, yt + sy * (hy + 0.002), z0 + hh * 0.59), (0.006, 0.004, hh * 0.2), b.m('plaster')))
        for y in np.linspace(yt - hy + 0.015, yt + hy - 0.015, max(3, int(2 * hy / 0.035))):
            for sx in (-1, 1):
                b.add(box((sx * (hx + 0.002), y, z0 + hh * 0.59), (0.004, 0.006, hh * 0.2), b.m('plaster')))
    T1x, T1y, T1h = 0.22, 0.29, 0.16
    z1 = zr
    storey(z1, T1x, T1y, T1h)
    irimoya(b, (0, yt, z1 + T1h + 0.014), T1x + 0.055, T1y + 0.055, 0.075, up=0.04)
    z2 = z1 + T1h + 0.05
    storey(z2, 0.16, 0.21, 0.12)
    irimoya(b, (0, yt, z2 + 0.12 + 0.012), 0.16 + 0.05, 0.21 + 0.05, 0.07, up=0.035)
    z3 = z2 + 0.12 + 0.05
    storey(z3, 0.1, 0.13, 0.1, posts=0.065)
    irimoya(b, (0, yt, z3 + 0.1 + 0.01), 0.1 + 0.045, 0.13 + 0.045, 0.085, up=0.03, gold='gold')
    # the curtain (maku) in the civ color round the tower's foot, white mon dots on it
    for sx in (-1, 1):
        b.add(box((sx * (T1x + 0.035), yt, zr + 0.03), (0.006, 2 * T1y + 0.07, 0.05), b.m('tint')))
        for y in (yt - 0.15, yt, yt + 0.15):
            b.add(M.cylinder(0.014, 0.014, 0.004, b.m('white'), seg=10).transformed(
                M.translate(sx * (T1x + 0.039), y, zr + 0.03) @ M.rot_y(90)))
    for x in (-0.12, 0.0, 0.12):
        b.add(rod((x, yt + T1y + 0.03, zr), (x, yt + T1y + 0.03, zr + 0.055), 0.004, mwb, seg=4))

    # the bow: a raised gun deck with a bronze cannon (o-zutsu), the stem post, an anchor
    yb = wy0 + WY
    zs = deck_at(hull, 1.1)
    b.add(rod((0, 1.0, zs - 0.02), (0, 1.3, zs + 0.16), 0.03, b.m('wood'), seg=6, r1=0.018))
    b.add(rod((0, 1.3, zs + 0.16), (0, 1.27, zs + 0.2), 0.018, b.m('wood_black'), seg=6, r1=0.01))
    b.add(box((0, yb + 0.03, zt - 0.06), (0.32, 0.012, 0.09), mwb))
    b.add(rod((0, yb - 0.08, zr + 0.025), (0, yb + 0.13, zr + 0.035), 0.022, b.m('bronze'), seg=8, r1=0.016))
    b.add(box((0, yb - 0.05, zr + 0.012), (0.07, 0.1, 0.03), b.m('wood'), bevel=0.004))
    ya = 1.06
    za = deck_at(hull, ya)
    b.add(rod((0.2, ya, za + 0.02), (0.24, ya + 0.02, za - 0.12), 0.008, b.m('iron'), seg=4))
    for s in (-1, 1):
        b.add(rod((0.24, ya + 0.02, za - 0.12), (0.24 + 0.03 * s, ya + 0.04, za - 0.09), 0.006, b.m('iron'), seg=4))
    # the stern: rudder and its tiller
    b.add(box((0, -1.24, 0.06), (0.03, 0.12, 0.2), b.m('wood_dark'), bevel=0.006))
    b.add(rod((0, -1.2, 0.15), (0, -1.05, zd + 0.1), 0.01, b.m('wood_light'), seg=5))

    # the mast forward of the tower, with its tabernacle and a sheave block at the head
    ym = 0.28
    mz = b.mast(ym, zr, 1.18, r=0.03, mat_name="wood_dark")
    b.add(box((0, ym, zr + 0.06), (0.08, 0.08, 0.12), mwb, bevel=0.006))
    b.add(box((0, ym, mz[2] - 0.03), (0.05, 0.05, 0.06), mwb, bevel=0.006))
    # the sail: billowed, braced, battened; its yard and boom
    ztop, sh, sw = mz[2] - 0.06, 0.76, 1.22
    b.square_sail(ym, ztop, sw, sw * 1.02, sh, 'none', billow=0.16, nu=44, nv=36)
    b.sails[-1]['mat_fn'] = mon_sail(b.mats, sw / sh)
    br = math.radians(b.p.get('brace', 0.0))
    ca, sa = math.cos(br), math.sin(br)
    lo = ztop - sh
    b.add(rod((-sw * 0.53 * ca, ym - sw * 0.53 * sa, lo + 0.02), (sw * 0.53 * ca, ym + sw * 0.53 * sa, lo + 0.02), 0.01,
              b.m('wood_dark'), seg=6))
    # sheets from the boom's ends to the deck, a forestay, the halyard, braces
    for s in (-1, 1):
        c = (s * sw * 0.5 * ca, ym + s * sw * 0.5 * sa + 0.06, lo + 0.04)
        b.line(c, (s * 0.3, ym - 0.32, zr + 0.03), r=0.004)
        b.line((s * sw * 0.52 * ca, ym + s * sw * 0.52 * sa, ztop + 0.01), (s * 0.3, wy0 - WY + 0.05, zr + 0.03), r=0.003)
    b.line(mz, (0, 1.28, zs + 0.18), r=0.005)
    b.line(mz, (0, wy0 - WY, zr + 0.04), r=0.004)

    # nobori: tall narrow banners on poles along the walls, a crossbar at the head
    for (x, y) in ((WX - 0.02, 0.5), (-WX + 0.02, 0.5), (WX - 0.02, -0.02), (-WX + 0.02, -0.02),
                   (WX - 0.02, -0.82), (-WX + 0.02, -0.82)):
        top = (x, y, zr + 0.38)
        b.add(rod((x, y, zr), top, 0.0055, b.m('wood_dark'), seg=4))
        b.add(rod(top, (x, y - 0.06, top[2]), 0.004, b.m('wood_dark'), seg=4))
        b.flag((x, y - 0.004, top[2] - 0.004), w=0.055, h=0.24, phase=y * 4 + x, pole=False)
    # the streamer (fukinagashi) at the stern
    b.add(rod((0, -1.0, zr), (0, -1.0, zr + 0.55), 0.007, b.m('wood_dark'), seg=5))
    b.flag((0, -1.0, zr + 0.55), w=0.2, h=0.05, phase=1.3, pole=False, droop=0.06)

    # sculling oars out under the walls
    b.set_oars(10, -0.82, 0.6, 0.42, z=zd - 0.01, dip=30)

    # arquebus volley from the upper ports (starboard), then arrows
    for i, y in enumerate(port_pts[::2]):
        b.gun((WX + 0.012, y, zb + WH * 0.75), (1.0, 0.08, 0.06), delay=i * 0.018, size=0.55)
    b.weapon = dict(kind='arrows', src=(0.25, -0.1, zt + 0.1), dir=(1.0, 0.25, 0.1), reach=1.4, n=6, volleys=2,
                    gap=0.25, t=0.1, flight=0.3, kick=False)
