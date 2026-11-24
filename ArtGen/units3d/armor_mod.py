"""The League's modern-mod vehicles (the Civ Players League's Modern 2.4)
remade for the realism pass, in the armour family's style (armor.py):

    'Bradley'     an M2A3 Bradley: the Mech Infantry's M2 hull and turret
                  (armor._bradley) with the A3's kit on top: BRAT reactive
                  tiles in two rows on the side armour and on the glacis, the
                  commander's independent viewer on the turret roof.
    'Laser Tank'  a near-future directed-energy tank on a modern MBT
                  chassis: a low wedge hull on skirted tracks, a low
                  faceted turret, and on a pylon above it the beam director:
                  a long armoured housing finned along its top and flanks,
                  capacitor drums at its root, a ringed barrel and a big
                  dark lens in a civ collar.
    'Convoy'      an up-armoured M1151 Humvee: the wide low body on four
                  big tyres, the armoured cab with thick-glass doors, the
                  brush guard, the cargo deck, the O-GPK gunner's shield with
                  its windows, an M2 and the gunner.
    'Iron Dome'   an Iron Dome battery's two launcher trailers, deployed:
                  each a twin-axle trailer on four outrigger jacks with its
                  box of twenty Tamir cells raised steeply on its rams.

The Bradley and the Laser Tank use armor.chassis (tracks); the Convoy and the
Iron Dome are built in metres (a scaled Part, K units per metre, the armour
models' scale) with their own wheels. Model space as every unit: facing +Y,
ground at z = 0.
"""
import copy
import math

from mathutils import Matrix, Vector

import armor
import props
import vkit
from armor import _hex, mark, plate
from props import R, S, T

K = 1.86 / 6.55   # model units per metre (the Mech Infantry's M2: 1.86 units for 6.55 m)

for _k, _v in {'lens_glass': ('#1c2a36', 0.12, 0.2, 'polished'), 'rim_paint': ('#5a5440', 0.7, 0.2, 'metal'),
               'tile_paint': ('#b8a67c', 0.7, 0.3, 'metal'), 'cell_cover': ('#dedad0', 0.6, 0.0, 'metal'), 'cell_cap': ('#8a877e', 0.6, 0.0, 'metal'),
               'vest': ('#5c5a42', 0.85, 0.0, 'leather'), 'uniform': ('#8a7e5e', 0.85, 0.0, 'leather'),
               'skin_tone': ('#b08060', 0.6, 0.0, None), 'fin_steel': ('#6c6a60', 0.55, 0.6, 'metal')}.items():
    props.DEFAULTS.setdefault(_k, _v)


class Metric:
    """A Part seen in metres: every add is scaled by K about the origin
    (bevels are given in metres too: they act before the transform)."""

    def __init__(self, part):
        self.p = part
        self.root, self.colors, self.weather = part.root, part.colors, part.weather

    def add(self, part, key, matrix=None, **kw):
        self.p.add(part, key, S(K) @ (matrix if matrix is not None else Matrix.Identity(4)), **kw)

    def finish(self):
        return self.p.finish()


def _box(p, size, key, at, rot=None, bevel=0.0, tint=False):
    m = T(*at) @ (rot if rot is not None else Matrix.Identity(4))
    p.add(props.box(*size), key, m, bevel=bevel, tint=tint)


def tyre(p, x, y, z, r, w, side, rim_key='rim_paint', lugs=0, grooves=3):
    """An off-road tyre round X with a dished rim, beadlock bolts, a hub
    and tread lugs (metres)."""
    rot = R('Y', 90 * side)
    m = T(x, y, z) @ rot
    p.add(props.lathe([(-w / 2, r * 0.66), (-w / 2, r * 0.9), (-w * 0.42, r), (w * 0.42, r), (w / 2, r * 0.9),
                       (w / 2, r * 0.66)], seg=28), 'rubber', m, smooth=True)
    p.add(props.lathe([(w / 2 - 0.06, r * 0.67), (w / 2 + 0.005, r * 0.66), (w / 2 - 0.02, r * 0.5),
                       (w / 2 - 0.01, r * 0.3)], seg=20), rim_key, m, smooth=True)
    p.add(props.lathe([(w / 2 - 0.02, r * 0.22), (w / 2 + 0.05, r * 0.16)], seg=12), 'gunmetal', m, smooth=True)
    for k in range(8):   # the beadlock ring's bolts
        a = 2 * math.pi * k / 8
        p.add(props.box(0.035, 0.035, 0.03), 'gunmetal', m @ T(r * 0.58 * math.cos(a), r * 0.58 * math.sin(a), w / 2 - 0.005))
    for k in range(grooves):   # the tread's circumferential grooves (dark rings proud of the crown)
        zz = -w * 0.3 + w * 0.6 * k / max(grooves - 1, 1)
        p.add(props.lathe([(zz - 0.012, r + 0.004), (zz + 0.012, r + 0.004)], seg=28), 'port', m, smooth=True)
    for k in range(lugs):   # the tread: lugs across the crown, staggered
        a = 360.0 * k / lugs
        off = (w * 0.18) * (1 if k % 2 else -1)
        p.add(props.box(r * 0.2, w * 0.44, 0.016), 'rubber',
              T(x, y, z) @ R('X', a) @ T(off, 0, r - 0.002) @ R('Z', 90), bevel=0.006)


# ---------------------------------------------------------------- the Bradley (M2A3)

def _bradley_a3(p, cfg, d, camo):
    """armor._bradley, then the A3's kit: BRAT tiles (two rows on each side's
    armour, a row on the glacis; one column each side and the TOW box's outer
    face in civ colour) and the commander's independent viewer."""
    armor._bradley(p, cfg, d, camo)
    W, H, hy, Lh, z_f, L, deck = d['W'], d['H'], d['hy'], d['Lh'], d['z_f'], d['L'], d['deck']
    up = cfg.get('upper', {})
    fr_ = up.get('front', 0.0)
    yt, yb = hy + Lh / 2 * (1 - fr_), hy + Lh / 2
    sy0, sy1 = cfg.get('skirt_y', (-L / 2 + 0.12, L / 2 - 0.12))
    za, zc = z_f + 0.02, deck - 0.02
    n = cfg.get('brat_n', 8)
    y_end = min(sy1, yt) - 0.01
    pitch = (y_end - sy0) / n
    rows = cfg.get('brat_rows', 2)
    rh = (zc - za) / rows
    civ_cols = cfg.get('brat_civ', (5,))
    for sgn in (-1, 1):
        xa = sgn * (W / 2 + 0.014 + 0.011 + cfg.get('brat_t', 0.03) / 2)
        for r_ in range(rows):
            zc_ = za + rh * (r_ + 0.5)
            for k in range(n):
                yc = sy0 + pitch * (k + 0.5)
                civ = k in civ_cols
                p.add(props.box(cfg.get('brat_t', 0.03), pitch * 0.88, rh * 0.84), 'tint' if civ else cfg.get('brat_key', 'tile_paint'),
                      T(xa, yc, zc_), bevel=0.005, tint=civ)
                for dz in (-rh * 0.28, rh * 0.28):   # the tile's bolts
                    p.add(props.box(0.008, 0.012, 0.012), 'shade_dark',
                          T(xa + sgn * cfg.get('brat_t', 0.03) / 2, yc, zc_ + dz))
        # the dark gap line between the rows and above the skirts
        p.add(props.box(0.008, y_end - sy0, 0.01), 'port', T(sgn * (W / 2 + 0.03), (sy0 + y_end) / 2, za + rh))
    # the glacis row: four tiles laid on the upper glacis
    gn = d['gn']
    for k in range(cfg.get('brat_glacis', 4)):
        x = -W * 0.36 + W * 0.72 * (k + 0.5) / cfg.get('brat_glacis', 4)
        if x < -W * 0.1:
            continue   # (the driver's hatch side stays clear)
        t = 0.35
        q = Vector((x, yt + (yb - yt) * t, deck - H * t)) + gn * 0.016
        p.add(props.box(W * 0.16, 0.026, H * 0.42), cfg.get('brat_key', 'tile_paint'),
              T(*q) @ armor._q(gn) @ R('X', 90), bevel=0.005)
    # the commander's independent viewer: a sight head on a post, turret rear right
    tx, ty = cfg.get('turret_x', 0.08), hy + cfg.get('turret_dy', 0.04)
    roof = deck + 0.012 + cfg.get('turret_h', 0.17)
    cx, cy = tx + cfg.get('turret_w', 0.56) * 0.28, ty - cfg.get('turret_l', 0.6) * 0.3
    vkit.rod(p, (cx, cy, roof - 0.01), (cx, cy, roof + 0.06), 0.016, 'shade_dark', seg=10)
    p.add(props.box(0.08, 0.07, 0.06), 'armor_turret', T(cx, cy, roof + 0.085), bevel=0.008)
    p.add(props.box(0.06, 0.01, 0.034), 'port', T(cx, cy + 0.036, roof + 0.088))
    p.add(props.box(0.084, 0.074, 0.008), 'armor_edge', T(cx, cy, roof + 0.118))
    # the TOW box's outer face: a civ panel
    bxw, bxl, bxh = cfg.get('tow_w', 0.13), cfg.get('tow_l', 0.44), cfg.get('tow_h', 0.13)
    tw_ = cfg.get('turret_w', 0.56)
    bx = tx - tw_ / 2 - bxw / 2 + 0.01
    roof_t = roof + cfg.get('tow_up', 0.0)
    if cfg.get('tow_civ'):
        p.add(props.box(0.008, bxl * cfg['tow_civ'], bxh * 0.62), 'tint', T(bx - bxw / 2 - 0.003, ty - 0.02, roof_t - bxh / 2 - 0.01),
              tint=True)


# ---------------------------------------------------------------- the Laser Tank

def _laser(p, cfg, d, camo):
    """The hull deck kit (driver's hatch, sensor heads, lights, a long grille
    deck, skirts with civ panels), the low faceted turret in the darker paint
    and on its pylon the beam director."""
    deck, W, H, hy, Lh, z_f, L = d['deck'], d['W'], d['H'], d['hy'], d['Lh'], d['z_f'], d['L']
    up = cfg.get('upper', {})
    fr_, rr_ = up.get('front', 0.0), up.get('rear', 0.0)
    yt, yb = hy + Lh / 2 * (1 - fr_), hy + Lh / 2
    gn = d['gn']
    yR = hy - Lh / 2 * (1 - rr_)
    tk = 'armor_turret'

    def glacis(x, t, off=0.0):
        return Vector((x, yt + (yb - yt) * t, deck - H * t)) + gn * off
    # ---- hull: the glacis' lit edge, two raised glacis plates, the driver's hatch, sensors, lights
    p.add(props.box(W * 0.98, 0.016, 0.014), 'armor_edge', T(0, yt, deck), bevel=0.004)
    for sgn in (-1, 1):
        q = glacis(sgn * W * 0.24, 0.45, 0.006)
        p.add(props.box(W * 0.36, (yb - yt) * 0.75 / max(gn.z, 0.3), 0.012), 'armor_paint', T(*q) @ armor._q(gn), bevel=0.004)
        q = glacis(sgn * W * 0.4, 0.82, 0.0)
        armor.light(p, q.x, q.y, q.z + 0.012, r=0.02, guard=False)
        p.add(props.box(0.06, 0.05, 0.04), 'shade_dark', T(sgn * W * 0.3, yt - 0.03, deck + 0.02), bevel=0.006)   # sensor heads
        p.add(props.box(0.04, 0.006, 0.02), 'lens_glass', T(sgn * W * 0.3, yt - 0.004, deck + 0.022))
    armor.rim_hatch(p, (-W * 0.0, yt - 0.06, deck), 0.055, rim_key='shade_dark', key='armor_paint')
    # the long engine deck: grilles each side of a spine, exhausts at the back
    for sgn in (-1, 1):
        armor.grille(p, sgn * W * 0.22, yR + 0.3, W * 0.3, 0.42, deck, n=7, key='armor_shade', frame_key='deck_dark')
        p.add(props.box(0.16, 0.02, 0.05), 'gunmetal', T(sgn * W * 0.22, yR - 0.008, deck - H * 0.4), bevel=0.004)
        p.add(props.box(0.035, 0.012, 0.025), 'mark_red', T(sgn * W * 0.44, yR - 0.008, deck - H * 0.2))
    p.add(props.box(0.06, 0.46, 0.02), 'armor_shade', T(0, yR + 0.3, deck + 0.01))
    # ---- skirts: bolted panels over the tracks, a civ panel forward, a dark foot
    zt_ = z_f + 0.01
    zb = d['zr'] + d['rw'] * cfg.get('skirt_low', 0.8)
    sy0, sy1 = -L / 2 + 0.06, L / 2 - 0.1
    npn = cfg.get('skirt_panels', 6)
    pl = (sy1 - sy0) / npn
    for sgn in (-1, 1):
        xs = sgn * (W / 2 + 0.016)
        for k in range(npn):
            a, b = sy0 + pl * k + 0.006, sy0 + pl * (k + 1) - 0.006
            p.add(props.box(0.024, b - a, zt_ - zb), cfg.get('skirt_key', 'armor_paint'), T(xs, (a + b) / 2, (zt_ + zb) / 2), bevel=0.004)
            for j in range(3):
                p.add(props.box(0.006, 0.012, 0.012), 'shade_dark', T(xs + sgn * 0.013, a + (b - a) * (j + 0.5) / 3, zt_ - 0.014))
            p.add(props.box(0.004, b - a - 0.004, (zt_ - zb) * 0.4), 'skirt_dirt', T(xs + sgn * 0.0125, (a + b) / 2, zb + (zt_ - zb) * 0.2))
        p.add(props.box(0.006, sy1 - sy0, zt_ - zb), 'port', T(sgn * (W / 2 + 0.003), (sy0 + sy1) / 2, (zt_ + zb) / 2))
        cs = cfg.get('skirt_civ', (npn - 2,))
        for k in cs:   # a painted civ band across a forward skirt panel
            a, b = sy0 + pl * k + 0.006, sy0 + pl * (k + 1) - 0.006
            p.add(props.box(0.004, b - a, (zt_ - zb) * cfg.get('skirt_band', 0.36)), 'tint',
                  T(xs + sgn * 0.0135, (a + b) / 2, zb + (zt_ - zb) * 0.62), tint=True)
    # ---- the turret: massive and faceted on a wide ring; a rear bustle
    ty = hy + cfg.get('turret_dy', -0.12)
    tw_, tl, th_ = cfg.get('turret_w', 0.86), cfg.get('turret_l', 0.92), cfg.get('turret_h', 0.14)
    tp_, pf_, tf_ = cfg.get('t_taper', 0.14), cfg.get('t_plan', 0.42), cfg.get('t_front', 0.3)
    tz0 = deck + 0.01 + cfg.get('ring_h', 0.0)
    if cfg.get('ring_h'):   # the turret ring: a wide dark collar on the deck under the turret
        p.add(props.lathe([(0, tw_ * 0.47), (cfg['ring_h'] + 0.012, tw_ * 0.45)], seg=32), 'shade_dark', T(0, ty, deck - 0.002), smooth=True)
        p.add(props.lathe([(cfg['ring_h'] * 0.5, tw_ * 0.475), (cfg['ring_h'] * 0.5 + 0.008, tw_ * 0.475)], seg=32), 'gunmetal', T(0, ty, deck), smooth=True)
    p.add(plate(tw_, tl, th_, cell=0.05, taper=tp_, plan_front=pf_, front=tf_, rear=0.08), tk, T(0, ty, tz0 + th_ / 2), bevel=0.01)
    roof = tz0 + th_
    p.add(props.box(tw_ * 0.9, tl * 0.8, 0.02), 'shade_dark', T(0, ty - tl * 0.05, deck + 0.004))
    hw = tw_ / 2 * (1 - tp_)
    yr_, yf_ = ty - tl / 2 * 0.92, ty + tl / 2 * (1 - tf_)
    hf = hw * (1 - pf_ * (1 - tf_))
    pts = [(-hw, yr_), (-hw, ty), (-hf, yf_), (hf, yf_), (hw, ty), (hw, yr_)]
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        vkit.rod(p, (x0, y0, roof + 0.002), (x1, y1, roof + 0.002), 0.007, 'armor_edge', seg=6)
    bz_ = tz0 + th_ * 0.45
    p.add(props.box(tw_ * 0.8, 0.2, th_ * 0.8), tk, T(0, ty - tl / 2 - 0.07, bz_), bevel=0.01)   # the bustle
    for k in range(3):
        p.add(props.box(0.008, 0.012, th_ * 0.6), 'shade_dark', T(-tw_ * 0.3 + tw_ * 0.3 * k, ty - tl / 2 - 0.172, bz_))
    if cfg.get('bustle_civ'):   # a civ panel on the bustle's back (seen from behind)
        p.add(props.box(tw_ * 0.8 * cfg['bustle_civ'], 0.008, th_ * 0.4), 'tint', T(0, ty - tl / 2 - 0.174, bz_ + th_ * 0.08), tint=True)
    for sgn in (-1, 1):   # the civ panel on each side, smoke banks, the capacitor drums on the bustle
        n_ = Vector((sgn, 0, tp_ * tw_ / 2 / th_)).normalized()
        c = Vector((sgn * (tw_ / 2 * (1 - tp_ * 0.5) + 0.003), ty - 0.06, tz0 + th_ * 0.5))
        if cfg.get('turret_band'):
            p.add(props.box(tl * cfg.get('band_l', 0.5), th_ * cfg.get('band_h', 0.5), 0.008), 'tint', T(*c) @ armor._q(n_), tint=True)
        if cfg.get('cheek_civ'):   # a civ panel on each front cheek (seen head-on)
            ex, ey = hf - hw, yf_ - ty
            nn = Vector((sgn * abs(ey), abs(ex), tp_ * tw_ / 2 / th_ * 0.5)).normalized()
            cc = Vector((sgn * (hw + hf) / 2, (ty + yf_) / 2, tz0 + th_ * 0.5)) + nn * 0.006
            p.add(props.box(math.hypot(ex, ey) * 0.7, th_ * 0.45, 0.008), 'tint', T(*cc) @ armor._q(nn), tint=True)
        armor.smoke(p, (sgn * tw_ * 0.34, ty + tl * 0.22, roof - 0.01), -sgn * 30, n=3)
        m = T(sgn * tw_ * 0.33, ty - tl / 2 - 0.17, bz_ + th_ * 0.62) @ R('X', -90)   # capacitor drums on the bustle
        p.add(props.lathe([(0, 0.04), (0.2, 0.04)], seg=16), 'armor_shade', m, smooth=True)
        for z in (0.03, 0.1, 0.17):
            p.add(props.lathe([(z, 0.045), (z + 0.012, 0.045)], seg=16), 'gunmetal', m, smooth=True)
    armor.rim_hatch(p, (tw_ * 0.22, ty - 0.16, roof), 0.06, rim_key='shade_dark', key=tk)
    p.add(props.box(0.1, 0.09, 0.08), tk, T(-tw_ * 0.22, ty - 0.05, roof + 0.04), bevel=0.008)   # the gunner's sight
    p.add(props.box(0.08, 0.006, 0.04), 'lens_glass', T(-tw_ * 0.22, ty - 0.004, roof + 0.045))
    # the heat exchanger on the roof's rear: a finned block
    p.add(props.box(tw_ * 0.4, 0.2, 0.05), 'shade_dark', T(0, ty - tl * 0.3, roof + 0.025), bevel=0.006)
    for k in range(7):
        p.add(props.box(tw_ * 0.38, 0.01, 0.03), 'fin_steel', T(0, ty - tl * 0.3 - 0.08 + 0.16 * k / 6, roof + 0.06))
    # ---- the emitter: a mantlet, a long thick ringed housing out past the hull front, the wide lens aperture
    gz = tz0 + th_ * 0.5
    mw = cfg.get('mantlet_w', 0.28)
    p.add(props.box(mw, 0.12, th_ * 0.8), 'armor_shade', T(0, yf_ + 0.03, gz), bevel=0.012)
    p.add(props.box(mw, 0.014, 0.012), 'armor_edge', T(0, yf_ + 0.09, gz + th_ * 0.4 - 0.006))
    ep = vkit.part(p.root, 'emitter', p.colors, p.weather)
    gm = T(0, yf_ + 0.08, gz) @ R('X', -90 + cfg.get('elev', 1.5))
    gl, gr = cfg.get('emit_len', 0.8), cfg.get('emit_r', 0.06)
    ep.add(props.lathe([(0, gr * 1.25), (0.12, gr * 1.2), (0.14, gr), (gl, gr * 0.92)], seg=10), tk, gm)   # the housing (faceted)
    nc = cfg.get('collars', 5)
    j0, j1 = cfg.get('jacket', (0.2, gl - 0.1))   # the ribs: a heat-sink jacket over this stretch
    for k in range(nc):   # cooling collars along it
        z = j0 + (j1 - j0) * k / max(nc - 1, 1)
        ep.add(props.lathe([(z, gr * 1.18), (z + 0.022, gr * 1.18)], seg=10), 'fin_steel', gm)
    ep.add(props.lathe([(0.02, gr * 0.3), (gl - 0.05, gr * 0.25)], seg=6), 'armor_edge', gm @ T(0, -gr * 0.98, 0), smooth=True)   # a lit top edge
    lr = cfg.get('lens_r', 0.11)
    ep.add(props.lathe([(gl, gr * 0.95), (gl + 0.06, lr * 0.95)], seg=20), 'armor_shade', gm, smooth=True)   # the flare
    ep.add(props.lathe([(gl + 0.06, lr), (gl + 0.1, lr)], seg=20), 'tint', gm, smooth=True, tint=True)        # the civ collar
    ep.add(props.lathe([(gl + 0.1, lr * 1.04), (gl + 0.125, lr * 1.04), (gl + 0.128, lr * 0.86)], seg=20), 'gunmetal', gm, smooth=True)
    ep.add(props.lathe([(gl + 0.105, lr * cfg.get('lens_k', 0.84)), (gl + 0.122, lr * cfg.get('lens_k', 0.84) * 0.95)], seg=20),
           'lens_glass', gm, smooth=True)
    ep.add(props.lathe([(gl + 0.122, lr * 0.3), (gl + 0.124, lr * 0.22)], seg=12), 'lens', gm @ T(lr * 0.25, -lr * 0.25, 0), smooth=True)
    eobj = ep.finish()
    if cfg.get('emit_no_fit'):
        eobj['no_fit'] = True
    if cfg.get('emit_no_shadow'):   # (as the armour finals' guns: its cut shadow read as a stick on the ground)
        eobj.visible_shadow = False
    if cfg.get('antennas'):
        ap = vkit.part(p.root, 'antennas', p.colors, p.weather)
        for sgn in (-1, 1):
            armor.antenna(ap, sgn * tw_ * 0.36, ty - tl * 0.38, roof, cfg['antennas'])
        a = ap.finish()
        a['no_fit'] = True
        a.visible_shadow = False


# ---------------------------------------------------------------- the Convoy (M1151 Humvee)

def _none_chassis(p, cfg, camo):
    return {}


def _gunner(p, at):
    """The gunner standing in the roof hatch behind the shield (metres):
    shoulders in a plate carrier, arms forward to the gun, a helmet."""
    x, y, z = at
    p.add(props.box(0.44, 0.3, 0.42), 'vest', T(x, y, z + 0.2), bevel=0.06)
    p.add(props.box(0.46, 0.06, 0.2), 'canvas_dark', T(x, y + 0.16, z + 0.2), bevel=0.02)   # pouches
    for sgn in (-1, 1):
        vkit.rod(p, (x + sgn * 0.22, y, z + 0.34), (x + sgn * 0.2, y + 0.3, z + 0.22), 0.06, 'uniform', seg=8)
        vkit.rod(p, (x + sgn * 0.2, y + 0.3, z + 0.22), (x + sgn * 0.1, y + 0.5, z + 0.3), 0.05, 'uniform', seg=8)
    p.add(props.ellipsoid(0.1, 0.11, 0.12, seg=12), 'skin_tone', T(x, y + 0.02, z + 0.56))
    p.add(props.ellipsoid(0.15, 0.16, 0.11, seg=14), 'canvas_dark', T(x, y, z + 0.62))   # the helmet
    p.add(props.box(0.2, 0.04, 0.05), 'gunmetal', T(x, y + 0.12, z + 0.6))                   # goggles


def _humvee(p0, cfg, d, camo):
    p = Metric(p0)
    tub = 'armor_paint'
    # ---- running gear: four big tyres, axles, the frame under the body
    r, tw = 0.48, 0.4
    wy = (1.62, -1.72)
    for y in wy:
        for sgn in (-1, 1):
            tyre(p, sgn * 0.9, y, r, r, tw, sgn)
        vkit.rod(p, (-0.8, y, r), (0.8, y, r), 0.07, 'gunmetal', seg=8)
        p.add(props.box(0.34, 0.3, 0.26), 'gunmetal', T(0, y, r), bevel=0.03)   # the differential
    for sgn in (-1, 1):
        p.add(props.box(0.12, 4.2, 0.18), 'gunmetal', T(sgn * 0.45, -0.05, 0.56))
    p.add(props.box(1.4, 4.4, 0.3), 'shade_dark', T(0, -0.05, 0.5))   # the shadowed underbody between the wheels
    # ---- the body: a low wide tub, the sloped hood with the fenders, the rear fenders
    side = [(-2.38, 0.62), (2.2, 0.62), (2.42, 0.72), (2.42, 1.12), (0.7, 1.12), (-2.38, 1.12)]
    p.add(props.slab(side, 1.96, 'X'), tub, bevel=0.03)
    hood = [(0.72, 1.0), (2.4, 1.0), (2.44, 1.16), (2.3, 1.24), (0.72, 1.34)]
    p.add(props.slab(hood, 2.18, 'X'), tub, bevel=0.035)
    p.add(props.box(0.08, 1.55, 0.02), 'shade_dark', T(0, 1.55, 1.30) @ R('X', -5.5))   # the hood's centre seam (a bulge)
    for sgn in (-1, 1):
        p.add(props.box(0.02, 1.5, 0.02), 'shade_dark', T(sgn * 0.72, 1.55, 1.30) @ R('X', -5.5))
        p.add(props.box(0.2, 1.28, 0.2), tub, T(sgn * 1.0, -1.72, 1.1), bevel=0.03)   # the rear fenders
        p.add(props.box(0.26, 1.3, 0.04), 'shade_dark', T(sgn * 1.0, -1.72, 0.985))   # their dark underside
        p.add(props.box(0.28, 1.6, 0.04), 'shade_dark', T(sgn * 0.98, 1.58, 0.985))
    # the grille: a dark panel with seven slots, round lamps in square bezels, the bumper and brush guard
    p.add(props.box(1.3, 0.04, 0.36), 'shade_dark', T(0, 2.43, 0.94))
    for k in range(7):
        p.add(props.box(0.07, 0.03, 0.3), 'port', T(-0.42 + 0.14 * k, 2.452, 0.94))
    for sgn in (-1, 1):
        p.add(props.box(0.26, 0.06, 0.24), 'armor_shade', T(sgn * 0.84, 2.43, 0.95), bevel=0.01)
        p.add(props.lathe([(0, 0.08), (0.02, 0.075)], seg=14), 'lens', T(sgn * 0.84, 2.462, 0.95) @ R('X', -90), smooth=True)
        p.add(props.box(0.09, 0.03, 0.06), 'mark_red', T(sgn * 1.0, 2.44, 1.15))
    p.add(props.box(2.1, 0.22, 0.2), 'gunmetal', T(0, 2.58, 0.66), bevel=0.02)
    p.add(props.box(0.5, 0.26, 0.24), 'armor_shade', T(0, 2.6, 0.82), bevel=0.02)   # the winch
    for sgn in (-1, 1):
        vkit.rod(p, (sgn * 0.62, 2.62, 0.72), (sgn * 0.62, 2.62, 1.24), 0.035, 'gunmetal', seg=8)
        vkit.rod(p, (sgn * 0.62, 2.62, 1.24), (sgn * 0.3, 2.5, 1.26), 0.035, 'gunmetal', seg=8)
    vkit.rod(p, (-0.3, 2.5, 1.26), (0.3, 2.5, 1.26), 0.035, 'gunmetal', seg=8)
    # the civ marker panel on the hood
    p.add(props.box(cfg.get('panel_w', 0.7), cfg.get('panel_l', 0.55), 0.02), 'tint',
          T(0, 1.72, 1.29) @ R('X', -5.5), tint=True)
    for sgn in (-1, 1):
        p.add(props.box(0.04, 0.6, 0.03), 'canvas_dark', T(sgn * cfg.get('panel_w', 0.7) * 0.4, 1.72, 1.305) @ R('X', -5.5))
    # ---- the armoured cab: near-vertical sides, a raked two-pane windscreen
    cab = [(-1.08, 1.12), (0.72, 1.12), (0.72, 1.34), (0.55, 1.96), (-1.08, 1.96)]
    p.add(props.slab(cab, 2.12, 'X'), tub, bevel=0.03)
    wn = Vector((0, 0.62, 0.17)).normalized()
    for sgn in (-1, 1):
        m = T(sgn * 0.5, 0.64, 1.65) @ armor._q(wn)
        p.add(props.box(0.84, 0.5, 0.04), 'shade_dark', m @ T(0, 0, 0.015))
        p.add(props.box(0.68, 0.36, 0.04), 'port', m @ T(0, 0.02, 0.03))
    p.add(props.box(2.16, 0.08, 0.06), 'armor_edge', T(0, -0.25, 1.97))   # the roof's lit edge (front to back below)
    for sgn in (-1, 1):
        xs = sgn * 1.06
        p.add(props.box(0.02, 1.8, 0.03), 'armor_edge', T(sgn * 1.065, -0.26, 1.955))
        for y0, y1 in ((-0.12, 0.66), (-1.02, -0.16)):   # the doors: outline, a thick small window, a handle
            p.add(props.box(0.02, 0.02, 0.8), 'shade_dark', T(xs + sgn * 0.01, y0, 1.5))
            p.add(props.box(0.02, y1 - y0, 0.02), 'shade_dark', T(xs + sgn * 0.01, (y0 + y1) / 2, 1.12))
            p.add(props.box(0.06, (y1 - y0) * 0.7, 0.34), 'shade_dark', T(xs + sgn * 0.02, (y0 + y1) / 2 + 0.02, 1.68), bevel=0.01)
            p.add(props.box(0.04, (y1 - y0) * 0.6, 0.26), 'port', T(xs + sgn * 0.045, (y0 + y1) / 2 + 0.02, 1.68))
            p.add(props.box(0.04, 0.12, 0.04), 'gunmetal', T(xs + sgn * 0.03, y0 + 0.12, 1.38))
            if cfg.get('door_civ'):
                p.add(props.box(0.01, (y1 - y0) * 0.62, 0.2), 'tint', T(xs + sgn * 0.02, (y0 + y1) / 2 + 0.02, 1.3), tint=True)
        p.add(props.box(0.08, 0.12, 0.2), 'gunmetal', T(sgn * 1.17, 0.5, 1.72))   # the mirrors
        vkit.rod(p, (sgn * 1.06, 0.55, 1.78), (sgn * 1.15, 0.52, 1.75), 0.02, 'gunmetal', seg=6)
    # ---- the cargo deck: low sides, a tailgate, jerrycans, ammo cans, a bedroll
    p.add(props.box(1.96, 1.3, 0.04), 'deck_dark', T(0, -1.73, 1.14))
    for sgn in (-1, 1):
        p.add(props.box(0.08, 1.3, 0.3), tub, T(sgn * 0.96, -1.73, 1.27), bevel=0.01)
    p.add(props.box(2.0, 0.08, 0.32), tub, T(0, -2.38, 1.27), bevel=0.01)
    if cfg.get('tailgate_civ'):   # a civ panel on the tailgate
        p.add(props.box(cfg['tailgate_civ'], 0.02, 0.18), 'tint', T(0, -2.43, 1.27), tint=True)
    for sgn in (-1, 1):
        p.add(props.box(0.12, 0.04, 0.1), 'mark_red', T(sgn * 0.82, -2.43, 1.0))
    for k in range(3):
        p.add(props.box(0.18, 0.34, 0.46), 'armor_camo2', T(-0.66 + 0.22 * k, -2.16, 1.37), bevel=0.025)
        p.add(props.box(0.05, 0.12, 0.06), 'gunmetal', T(-0.66 + 0.22 * k, -2.06, 1.62))
    for yy in (-2.12, -1.8):
        p.add(props.box(0.46, 0.28, 0.26), 'armor_shade', T(0.5, yy, 1.29), bevel=0.02)
        p.add(props.box(0.48, 0.05, 0.04), 'gunmetal', T(0.5, yy, 1.43))
    armor.bedroll(p, (-0.8, -1.3, 1.36), (0.8, -1.3, 1.36), 0.17, key='canvas', straps=3)
    # ---- the O-GPK turret: a ring on the roof, a faceted front shield with windows, side wings
    gy = cfg.get('ring_y', -0.35)
    rz = 1.96
    p.add(props.lathe([(0, 0.66), (0.08, 0.66), (0.1, 0.6)], seg=24), 'armor_shade', T(0, gy, rz), smooth=True)
    for yaw, w, xo in ((0.0, 0.76, 0.0), (36.0, 0.46, 0.56), (-36.0, 0.46, -0.56)):
        m = T(xo, gy + 0.55 - abs(xo) * 0.5, rz + 0.05) @ R('Z', yaw)
        p.add(props.box(w, 0.06, 0.66), tub, m @ T(0, 0, 0.33), bevel=0.012)
        p.add(props.box(w * 0.7, 0.03, 0.26), 'shade_dark', m @ T(0, 0.035, 0.42))
        p.add(props.box(w * 0.6, 0.03, 0.2), 'port', m @ T(0, 0.05, 0.42))
        p.add(props.box(w + 0.01, 0.07, 0.03), 'armor_edge', m @ T(0, 0, 0.665))
        if cfg.get('shield_civ') and xo == 0.0:
            p.add(props.box(w * 0.9, 0.02, 0.12), 'tint', m @ T(0, 0.045, 0.12), tint=True)
    for sgn in (-1, 1):
        m = T(sgn * 0.78, gy + 0.02, rz + 0.05) @ R('Z', sgn * -10)
        p.add(props.box(0.06, 0.62, 0.48), tub, m @ T(0, 0, 0.24), bevel=0.01)
        p.add(props.box(0.07, 0.62, 0.03), 'armor_edge', m @ T(0, 0, 0.48))
    _gunner(p, (0.0, gy - 0.15, rz - 0.05))
    # the M2 on its pintle: receiver, barrel jacket, barrel, flash hider, ammo can
    gm = T(0.0, gy + 0.22, rz + 0.5)
    p.add(props.box(0.18, 0.62, 0.2), 'gunmetal', gm, bevel=0.015)
    p.add(props.box(0.2, 0.26, 0.2), 'armor_camo2', gm @ T(-0.2, -0.02, -0.05), bevel=0.015)
    vkit.rod(p, gm @ Vector((0, 0.3, 0.02)), gm @ Vector((0, 0.72, 0.02)), 0.06, 'gunmetal', seg=10)
    vkit.rod(p, gm @ Vector((0, 0.7, 0.02)), gm @ Vector((0, 1.3, 0.04)), 0.032, 'gunmetal', seg=8)
    vkit.rod(p, gm @ Vector((0, 1.24, 0.04)), gm @ Vector((0, 1.38, 0.04)), 0.05, 'gunmetal', seg=8)
    vkit.rod(p, gm @ Vector((0, 0.0, -0.08)), gm @ Vector((0, 0.0, -0.45)), 0.04, 'gunmetal', seg=6)
    # antennas (their own part, out of the fit)
    if cfg.get('antennas'):
        ap = Metric(vkit.part(p0.root, 'antennas', p0.colors, p0.weather))
        for sgn in (-1, 1):
            p_ = (sgn * 0.88, -2.3, 1.42)
            ap.add(props.lathe([(0, 0.06), (0.1, 0.05)], seg=10), 'gunmetal', T(*p_), smooth=True)
            vkit.rod(ap, (p_[0], p_[1], p_[2] + 0.1), (p_[0] - 0.02 * sgn, p_[1] - 0.25, p_[2] + cfg['antennas']), 0.014, 'gunmetal', seg=4)
        a = ap.finish()
        a['no_fit'] = True
        a.visible_shadow = False


# ---------------------------------------------------------------- the Iron Dome battery

def _launcher_trailer(p, ox, oy, cfg, yaw=0.0):
    """One Iron Dome launcher trailer, deployed (metres, about (ox, oy)): the
    twin-axle trailer and its drawbar, four outrigger jacks on pads, the
    launcher box hinged at the rear and raised on two rams, twenty cell
    covers in its top face, civ panels on its sides."""
    B = T(ox, oy, 0) @ R('Z', yaw)

    def add(bm, key, m=None, **kw):
        p.add(bm, key, B @ (m if m is not None else Matrix.Identity(4)), **kw)

    def rod(a, b, r, key, seg=8):
        vkit.rod(p, B @ Vector(a), B @ Vector(b), r, key, seg=seg)
    bz = cfg.get('bed_z', 1.05)  # the bed's top
    # wheels, axles, frame, bed, mudguards
    wr, ww = cfg.get('wheel', (0.56, 0.46))
    for y in (-1.45, -0.3):
        for sgn in (-1, 1):
            tyre(p, *(B @ Vector((sgn * 1.0, y, wr))), wr, ww, sgn)
        rod((-0.9, y, wr), (0.9, y, wr), 0.1, 'gunmetal')
    for sgn in (-1, 1):
        add(props.box(0.3, 5.4, 0.45), 'gunmetal', T(sgn * 0.5, 0.0, 0.85))
    add(props.box(2.4, 5.2, 0.3), 'armor_paint', T(0, 0.0, bz - 0.15), bevel=0.03)
    for sgn in (-1, 1):   # the deep side beams of the bed, a dark lip under them
        for y0, y1 in ((-2.6, -2.15), (0.4, 2.6)):   # (open over the wheels)
            bd = cfg.get('beam_d', 0.5)   # the side beams' depth
            add(props.box(0.1, y1 - y0, bd), 'armor_paint', T(sgn * 1.2, (y0 + y1) / 2, bz - bd / 2), bevel=0.02)
            add(props.box(0.12, y1 - y0, 0.05), 'shade_dark', T(sgn * 1.2, (y0 + y1) / 2, bz - bd))
        for y in (-2.4, 1.0, 2.0):
            add(props.box(0.04, 0.06, 0.44), 'shade_dark', T(sgn * 1.26, y, bz - 0.25))
    add(props.box(2.42, 0.06, 0.5), 'armor_shade', T(0, -2.62, bz - 0.25))
    if cfg.get('rear_civ'):   # a civ band across the bed's tail
        add(props.box(cfg['rear_civ'], 0.03, 0.2), 'tint', T(0, -2.66, bz - 0.2), tint=True)
    # the drawbar (an A-frame) to the towing eye, the parking leg
    for sgn in (-1, 1):
        rod((sgn * 0.5, 2.5, 0.85), (0.0, 3.9, 0.75), cfg.get('drawbar_r', 0.13), 'armor_shade')
    rod((0.0, 3.9, 0.75), (0.0, 4.25, 0.75), cfg.get('drawbar_r', 0.13) * 0.85, 'gunmetal')
    add(props.lathe([(0, 0.17), (0.09, 0.17)], seg=14), 'gunmetal', T(0, 4.34, 0.75) @ R('Y', 90), smooth=True)
    if cfg.get('drawbar_civ'):   # a civ sleeve on the drawbar
        vkit.rod(p, B @ Vector((0.0, 3.92, 0.75)), B @ Vector((0.0, 3.92 + cfg['drawbar_civ'], 0.75)), 0.15, 'tint', seg=12, tint=True)
    rod((0.0, 3.55, 0.75), (0.0, 3.55, 0.1), 0.09, 'gunmetal')
    add(props.box(0.4, 0.4, 0.08), 'gunmetal', T(0, 3.55, 0.05))
    # the front: an electronics cabinet and the generator, a cable reel
    add(props.box(1.9, 0.9, 0.85), 'armor_turret', T(0, 2.05, bz + 0.425), bevel=0.03)
    for k in range(5):
        add(props.box(1.7, 0.02, 0.03), 'shade_dark', T(0, 2.505, bz + 0.2 + 0.12 * k))
    for sgn in (-1, 1):
        add(props.box(0.02, 0.6, 0.6), 'shade_dark', T(sgn * 0.955, 2.05, bz + 0.45))
        add(props.box(0.03, 0.06, 0.12), 'gunmetal', T(sgn * 0.97, 1.85, bz + 0.45))
    add(props.box(1.94, 0.94, 0.04), 'shade_dark', T(0, 2.05, bz + 0.87))
    # the outriggers: four arms out to jacks on round pads
    for sgn in (-1, 1):
        for y in (2.35, -2.35):
            a, b = (sgn * 1.1, y, bz - 0.15), (sgn * 1.85, y, bz - 0.15)
            add(props.box(0.85, 0.32, 0.32), 'armor_shade', T(sgn * 1.55, y, bz - 0.2), bevel=0.02)
            add(props.box(0.3, 0.3, 0.4), 'armor_shade', T(sgn * 1.9, y, bz - 0.3), bevel=0.02)       # the jack's housing
            lr_ = cfg.get('leg_r', 0.12)
            rod((sgn * 1.9, y, bz - 0.4), (sgn * 1.9, y, 0.12), lr_, 'gunmetal', seg=10)
            rod((sgn * 1.9, y, bz - 0.4), (sgn * 1.9, y, bz - 0.9), lr_ * 1.4, 'armor_shade', seg=10)   # the jack's outer tube
            add(props.lathe([(0, 0.38), (0.1, 0.34)], seg=16), 'armor_shade', T(sgn * 1.9, y, 0.0), smooth=True)
    # the launcher box: hinged at the rear, raised on its rams
    elev = cfg.get('elev', 62.0)
    Lb, Wb, Hb = cfg.get('box', (3.3, 1.75, 1.45))
    piv = Vector((0, -2.35, bz + 0.3))
    G = T(*piv) @ R('X', elev)   # box space: along +Y from the hinge, its belly at z = 0
    add(props.box(Wb, Lb, Hb), 'armor_turret', G @ T(0, Lb / 2, Hb / 2), bevel=0.04)
    for sgn in (-1, 1):   # side ribs, the civ panel, an access door
        for y in (0.25, 1.15, 2.05, 2.95):
            add(props.box(0.05, 0.08, Hb), 'shade_dark', G @ T(sgn * (Wb / 2 + 0.02), y, Hb / 2))
        add(props.box(0.05, Lb, 0.07), 'armor_edge', G @ T(sgn * (Wb / 2 + 0.02), Lb / 2, Hb - 0.02))
        add(props.box(0.04, cfg.get('civ_l', 0.82), Hb * cfg.get('civ_h', 0.62)), 'tint',
            G @ T(sgn * (Wb / 2 + 0.025), cfg.get('civ_y', 1.6), Hb * 0.5), tint=True)
        add(props.box(0.03, 0.6, 0.5), 'shade_dark', G @ T(sgn * (Wb / 2 + 0.015), 2.5, Hb * 0.5))
    # the top face: a frame and 4 x 5 cells with their covers
    add(props.box(Wb + 0.08, 0.1, Hb + 0.08), 'shade_dark', G @ T(0, Lb + 0.02, Hb / 2))
    fc = cfg.get('face_civ')
    if fc:   # a civ frame round the cell face (seen from behind)
        for zz in (-fc / 2, Hb + fc / 2):
            add(props.box(Wb + 0.1 + 2 * fc, 0.1, fc), 'tint', G @ T(0, Lb + 0.03, zz), tint=True)
        for sgn in (-1, 1):
            add(props.box(fc, 0.1, Hb), 'tint', G @ T(sgn * (Wb / 2 + 0.05 + fc / 2), Lb + 0.03, Hb / 2), tint=True)
    cap = cfg.get('cell_key', 'cell_cover')
    for i in range(4):
        for j in range(5):
            x = -Wb / 2 + Wb * (i + 0.5) / 4
            z = Hb * (j + 0.5) / 5
            if cfg.get('dark_cells'):   # a dark cell mouth with a grey cap set in it
                add(props.box(Wb / 4 - 0.05, 0.04, Hb / 5 - 0.05), 'port', G @ T(x, Lb + 0.075, z))
                add(props.lathe([(0, min(Wb / 4, Hb / 5) * 0.32), (0.03, min(Wb / 4, Hb / 5) * 0.3)], seg=14), cap,
                    G @ T(x, Lb + 0.08, z) @ R('X', -90), smooth=True)
            else:
                add(props.box(Wb / 4 - 0.07, 0.06, Hb / 5 - 0.07), cap, G @ T(x, Lb + 0.06, z), bevel=0.01)
    # its back (the hinge end) and belly rails
    add(props.box(Wb, 0.06, Hb), 'armor_shade', G @ T(0, -0.02, Hb / 2))
    if cfg.get('box_ribs'):   # ribs across both broad faces, a junction box and cable runs on the belly
        for y in (0.45, 1.3, 2.15, 3.0):
            for zz in (-0.035, Hb + 0.035):
                add(props.box(Wb + 0.04, 0.09, 0.07), 'shade_dark', G @ T(0, y, zz))
        add(props.box(0.5, 0.36, 0.16), 'gunmetal', G @ T(-0.55, 0.75, -0.09), bevel=0.02)
        for dx in (-0.62, -0.48):
            add(props.box(0.06, Lb * 0.55, 0.06), 'gunmetal', G @ T(dx, 0.75 + Lb * 0.3, -0.05))
        for x in (-0.6, 0.0, 0.6):   # ribs on the hinge end
            add(props.box(0.08, 0.08, Hb), 'shade_dark', G @ T(x, -0.08, Hb / 2))
    if cfg.get('slab_civ'):   # civ panels on the box's two broad faces (its top and its belly, between the rails)
        w_, l_ = cfg['slab_civ']
        add(props.box(Wb * w_, Lb * l_, 0.02), 'tint', G @ T(0, Lb * 0.55, Hb + 0.012), tint=True)
        add(props.box(0.7 * w_ / 0.6 * 0.6, Lb * l_, 0.02), 'tint', G @ T(0, Lb * 0.55, -0.012), tint=True)
    if cfg.get('back_civ'):   # a civ band across the box's back (the hinge end)
        add(props.box(Wb * 0.8, 0.02, Hb * cfg['back_civ']), 'tint', G @ T(0, -0.06, Hb * 0.55), tint=True)
    if cfg.get('cab_civ'):    # a civ band across the cabinet's front
        add(props.box(1.5, 0.02, cfg['cab_civ']), 'tint', T(0, 2.51, bz + 0.62), tint=True)
    for sgn in (-1, 1):
        add(props.box(0.14, Lb * 0.9, 0.12), 'gunmetal', G @ T(sgn * 0.5, Lb * 0.45, -0.04))
        add(props.box(0.3, 0.5, 0.5), 'gunmetal', T(sgn * 0.62, piv.y + 0.1, bz + 0.2))   # the hinge brackets
    # the rams: from the bed up to the box's belly
    for sgn in (-1, 1):
        a = Vector((sgn * 0.5, 0.6, bz + 0.05))
        b = G @ Vector((sgn * 0.5, Lb * 0.5, -0.06))
        mid = a + (b - a) * 0.55
        rod(tuple(a), tuple(mid), 0.16, 'armor_shade', seg=12)
        rod(tuple(mid - (b - a) * 0.05), tuple(b), 0.09, 'fin_steel', seg=10)
        add(props.box(0.3, 0.3, 0.12), 'gunmetal', T(sgn * 0.5, 0.6, bz + 0.04))
    add(props.box(0.18, 0.04, 0.1), 'mark_red', T(0.9, -2.62, 0.9))
    add(props.box(0.18, 0.04, 0.1), 'mark_red', T(-0.9, -2.62, 0.9))


def _iron_dome(p0, cfg, d, camo):
    p = Metric(p0)
    for (ox, oy) in cfg.get('trailers', ((0.0, 0.0),)):
        _launcher_trailer(p, ox, oy, cfg)


# ---------------------------------------------------------------- configs

_WEAR_SAND = {'armor_paint': {'grime': 0.5, 'soot': 0.15, 'edges': 0.25, 'rust': 0.0, 'chips': 0.4,
                              'dust': 0.45, 'dust_h': 0.25, 'exhaust': 0.4, 'exhaust_y': -0.6,
                              'fade': 0.3, 'streaks': 0.5},
              'armor_shade': {'chips': 0.3, 'dust': 0.4, 'dust_h': 0.25},
              'rubber': {'dust': 0.6, 'dust_h': 0.25}, 'gunmetal': {'dust': 0.4, 'dust_h': 0.3},
              'shade_dark': {'edges': 0.0}}

_bradley_cfg = copy.deepcopy(armor.ARMOR['Mech Infantry'])
_bradley_cfg.update(
    paint='#c8b68a', camo2='#8a7452', camo4='#a89470',
    camo=[('armor_camo2', 4.0, 0.2, 21.0), ('armor_camo4', 4.5, 0.3, 33.0)], camo_soft=0.012,
    chev_tow=None, chev_side=0.001, tow_civ=0.7, brat_civ=(4, 5), antennas=0.26, antenna_r=0.011, turret_tone=0.8, fit_scale=0.93,
    colors={'armor_edge': '#d8cca4', 'track': '#26231f', 'gun_olive': '#24231c', 'skirt_dirt': '#5c4e3a',
            'tile_paint': '#bca878'},
    builder=_bradley_a3)

UNITS_CFG = {
    'Bradley': _bradley_cfg,
    'Laser Tank': dict(L=1.88, W=1.02, H=0.12, tw=0.2, rw=0.075, wheels=6, top=0.22, rollers=0, holes=6, hole_key='shade_dark',
                       sprocket='rear', front_wheel=(0.84, 0.15, 0.075), rear_wheel=(-0.85, 0.15, 0.078), track_th=0.04,
                       upper=dict(front=0.5, rear=0.1), nose=0.2, belly=0.06,
                       paint='#b4a47c', camo2='#7e7058', camo3='#c8bc9a',
                       camo=[('armor_camo2', 3.5, 0.2, 41.0, (1.6, 1.0, 1.0)), ('armor_camo3', 3.0, 0.4, 47.0)], camo_soft=0.015,
                       turret_tone=0.82, hub_key='muzzle_light', skirt_low=0.55, skirt_key='armor_turret',
                       colors={'armor_edge': '#ddd2ae', 'track': '#26231f', 'skirt_dirt': '#8c7858'},
                       antennas=None, turret_band=True, skirt_civ=(2, 3), band_l=0.5, band_h=0.36, skirt_band=0.42,
                       turret_w=0.98, turret_l=0.9, turret_h=0.18, t_taper=0.12, t_plan=0.3, t_front=0.25, turret_dy=-0.1, ring_h=0.025,
                       bustle_civ=0.6, cheek_civ=True, fit_scale=0.93, emit_len=0.82, emit_r=0.093, lens_r=0.14, lens_k=0.9, emit_no_fit=True, emit_no_shadow=True, collars=6, jacket=(0.16, 0.42), elev=-6.0,
                       wear=_WEAR_SAND, builder=_laser),
    'Convoy': dict(chassis_fn=_none_chassis, paint='#b4a276', camo2='#7a6448', camo4='#958564',
                   camo=[('armor_camo2', 4.0, 0.18, 51.0), ('armor_camo4', 4.5, 0.28, 57.0)], camo_soft=0.012,
                   colors={'armor_edge': '#ddd2ae', 'rubber': '#262522', 'rim_paint': '#6e6448', 'canvas': '#8a7a58',
                           'canvas_dark': '#5e5440', 'deck_dark': '#5a5240'},
                   antennas=None, shield_civ=True, door_civ=True, tailgate_civ=0.9, fit_scale=0.89,
                   wear=_WEAR_SAND, builder=_humvee),
    'Iron Dome': dict(chassis_fn=_none_chassis, paint='#a09678', camo2='#7c745e', camo3='#b8ae90',
                      camo=[('armor_camo2', 3.5, 0.2, 61.0), ('armor_camo4', 4.0, 0.34, 67.0)], camo4='#6c6650',
                      camo_soft=0.015, turret_tone=0.74, fit_scale=1.05, drawbar_r=0.17, beam_d=0.72, leg_r=0.17, box_ribs=True,
                      trailers=((0.0, 0.0),), bed_z=1.45, back_civ=0.45, cab_civ=0.26, box=(3.4, 1.95, 1.6), civ_l=0.7, civ_h=0.6, slab_civ=(0.5, 0.2),
                      rear_civ=1.2, drawbar_civ=0.5, face_civ=0.045, dark_cells=True, cell_key='cell_cap',
                      colors={'armor_edge': '#d6ceb0', 'rubber': '#262522', 'rim_paint': '#6e6850'},
                      wear=_WEAR_SAND, builder=_iron_dome),
}
UNITS = list(UNITS_CFG)


def build(unit, colors=None, **params):
    cfg = dict(UNITS_CFG[unit])
    cfg.update(params)
    return armor.tracked(unit, cfg, colors or {})
