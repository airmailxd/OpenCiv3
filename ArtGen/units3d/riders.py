"""The horse riders' dress and headgear (the PC session's units).

The old catalog's rider entries use keys the foot units don't (a
'long_sleeved' top, 'pants', a 'coat_skirt', 'sleeves': 'mail', and
'headgear' kinds such as a shako or a papakha). translate() maps an entry
onto what units.build_footman understands; headgear() builds the hat as a
props.Prop and fixes it to the rider's head bone.

Headgear space: meters, +Z up from the brow line, +Y toward the face; a head
is about 0.1 m in radius.
"""
import math

from mathutils import Matrix, Vector

import props
from props import R, T, S

HEADGEAR = {}


def _calm(hexcol, sat):
    """A hex color with its saturation scaled by `sat`."""
    import colorsys
    h = hexcol.lstrip('#')
    r, g, b = (int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))
    hh, ll, ss = colorsys.rgb_to_hls(r, g, b)
    r, g, b = colorsys.hls_to_rgb(hh, ll, ss * sat)
    return '#%02x%02x%02x' % (round(r * 255), round(g * 255), round(b * 255))


def hat(name):
    def deco(fn):
        HEADGEAR[name] = fn
        return fn
    return deco


def translate(r):
    """A rider entry rewritten in the foot units' vocabulary."""
    e = dict(r)
    c = dict(e.get('colors', {}))
    tint = list(e.get('tint', []))
    if e.get('top') == 'long_sleeved':
        e['top'] = 'sleeved'
    if 'top' not in tint and e.get('top') in ('sleeved', 'tunic', 'mantle', 'vest') and c.get('coat'):
        c['top'] = c['coat']
    if e.get('pants') == 'pants':
        e['bottom'] = 'long'
        if c.get('pants'):
            c['bottom'] = c['pants']
    elif e.get('bottom') == 'none':
        e['bottom'] = None
    if e.get('sleeves') == 'mail':
        e['collar'] = 'mail'
        c.setdefault('mail', '#6e7074')
    # (bare skin is calmed in units.build_footman, for every unit)
    e['colors'] = c
    e['tint'] = tint
    e['_headgear'] = e.pop('headgear', None)
    return e


HAT_PITCH = 14.0   # degrees forward, with front_fix=True (the Knight's); front_fix='level' levels the hat


def headgear(entry, arm, front_fix=False):
    """Build the entry's headgear (if it has one of HEADGEAR's kinds) and fix
    it to the head bone. Returns the object or None.

    front_fix: the hat's +Y (its front) to the face. Without it the hat goes
    on back to front (the head bone's +Z is the face, and R('X', -90) turns
    the hat's +Y to -Z): a peak, a crest's fall or a helm's eye slits end up
    behind the head. Off by default; every rider (mounted.seat_rider), chariot
    crewman (chariots._crew) and siege crewman (crews.populate) passes it."""
    import human as H
    kind = entry.get('_headgear')
    if kind not in HEADGEAR:
        return None
    c = dict(entry.get('colors', {}))
    tinted = 'hat' in entry.get('tint', []) or 'helmet' in entry.get('tint', [])
    p = props.Prop(f'headgear_{kind}', c, {'hat': {'grime': 0.5}, 'helmet': {'grime': 0.4, 'rust': 0.15},
                                          'tint': {'grime': 0.45}})
    # 'headgear_params': keyword options for hats that take them (feather_headdress, modern_helmet)
    HEADGEAR[kind](p, c, tinted, 'crest' in entry.get('tint', []) or 'plume' in entry.get('tint', []),
                   **entry.get('headgear_params', {}))
    obj = p.finish()
    s = entry.get('headgear_scale', 1.0)
    sx, sy, sz = (s, s, s) if isinstance(s, (int, float)) else s
    k = 1.2   # a little larger than life, so the headgear keeps its silhouette at 1x
    sx, sy, sz = sx * k, sy * k, sz * k
    # the head bone points up the head; its tail is near the crown. Hat +Z -> bone +Y
    # (front_fix also levels the hat: the head bone leans about 15 degrees back when the face is level)
    pitch = HAT_PITCH if front_fix is True else front_fix   # front_fix may give its own pitch (degrees)
    if front_fix == 'level':   # the pitch that stands the hat's crown straight up in the world (the pose's head)
        H._update()
        pb = arm.pose.bones['head']
        m = (arm.matrix_world @ pb.matrix).to_3x3()
        v = (R('Y', 180) @ R('X', -90)).to_3x3() @ Vector((0.0, 0.0, 1.0))
        pitch = max((p * 0.5 for p in range(-120, 121)),
                    key=lambda a: (m @ (R('X', a).to_3x3() @ v)).normalized().z)
    if front_fix and entry.get('headgear_tilt'):   # a unit's extra pitch (degrees forward; negative tips it back)
        pitch += entry['headgear_tilt']
    turn = R('X', pitch) @ R('Y', 180) @ R('X', -90) if front_fix else R('X', -90)
    lift = entry.get('headgear_lift', 0.0)   # up the head bone (a helmet sitting higher, off the eyes)
    H.attach(obj, arm, 'head', Matrix.Translation((0, -0.015 + lift, 0.012)) @ turn @ S(sx, sy, sz))
    return obj


# ------------------------------------------------------------ the hats

props.DEFAULTS.setdefault('hat', ('#3a2e26', 0.85, 0.0, 'cloth'))
props.DEFAULTS.setdefault('fur', ('#3a2e26', 0.95, 0.0, 'rope'))
props.DEFAULTS.setdefault('helmet', ('#9a9ca2', 0.45, 1.0, 'metal'))
props.DEFAULTS.setdefault('brass', ('#a88a4a', 0.4, 1.0, 'metal'))
props.DEFAULTS.setdefault('plume', ('#e8e4da', 0.9, 0.0, 'cloth'))
props.DEFAULTS.setdefault('mail', ('#6e7074', 0.5, 1.0, 'metal'))
props.DEFAULTS.setdefault('crest', ('#e8e4da', 0.9, 0.0, 'rope'))
props.DEFAULTS.setdefault('turban_white', ('#ece8de', 0.85, 0.0, 'cloth'))
props.DEFAULTS.setdefault('jewel', ('#b0202a', 0.15, 0.0, 'metal'))
props.DEFAULTS.setdefault('lacquer', ('#1a1816', 0.3, 0.0, 'leather'))
props.DEFAULTS.setdefault('feather', ('#e8e4da', 0.9, 0.0, 'cloth'))
props.DEFAULTS.setdefault('feather_red', ('#a8302a', 0.9, 0.0, 'cloth'))
props.DEFAULTS.setdefault('feather_green', ('#2e7a4a', 0.9, 0.0, 'cloth'))
props.DEFAULTS.setdefault('gold', ('#c8a048', 0.35, 1.0, 'metal'))
props.DEFAULTS.setdefault('papakha_fur', ('#241e1a', 0.97, 0.0, 'rope'))   # black lambskin
props.DEFAULTS.setdefault('papakha_fur_hi', ('#5e5046', 0.97, 0.0, 'rope'))   # its lighter grey-brown curls
props.DEFAULTS.setdefault('hussar_peak', ('#3a2a1c', 0.3, 0.0, 'leather'))   # a lacquered leather peak (it catches the light)


def _k(tinted, key):
    return ('tint', True) if tinted else (key, False)


@hat('modern_helmet')
def _modern_helmet(p, c, tinted, crest, band=False, net=False, camo=False, **more):
    props.modern_helmet(p, band=band, net=net, camo=camo, **more)   # more: paint, band_w (realism infantry)


@hat('papakha')
def papakha(p, c, tinted, _, low=False, crown=False):
    """A Cossack's tall fur hat; low=True: Civ3's Cossack's low tan fur drum
    with a civ cloth top crossed by a gold braid."""
    if low:
        p.add(props.lathe([(0.0, 0.116), (0.012, 0.128), (0.062, 0.13), (0.075, 0.122)], seg=28), 'fur', smooth=True)
        key, t = _k(tinted, 'hat')
        p.add(props.ellipsoid(0.12, 0.12, 0.085, seg=24), key, T(0, 0, 0.065), smooth=True, tint=t)   # the civ crown, over the head
        for a in (0, 90):   # the braided cross over the top
            p.add(props.tube([(-0.1, 0, 0.11), (0, 0, 0.152), (0.1, 0, 0.11)], 0.008, seg=6), 'gold',
                  T(0, 0, 0) @ R('Z', a), smooth=True)
        return
    if crown:   # (the realism pass) a tall shaggy dark papakha, the civ colour only on a small cloth crown patch
        key, t = _k(tinted, 'hat')
        p.add(props.lathe([(0.0, 0.114), (0.03, 0.12), (0.12, 0.13), (0.2, 0.138), (0.228, 0.128), (0.245, 0.0)], seg=28),
              'papakha_fur', smooth=True)   # (the crown flares a little wider than the band)
        for i in range(12):   # tufts breaking the top edge (an uneven outline at 1x)
            a_ = math.radians(i * 30 + (i % 2) * 11)
            rr = 0.02 + 0.006 * ((i * 7) % 3)
            p.add(props.ellipsoid(rr, rr, rr * 0.9, seg=6), 'papakha_fur_hi' if i % 4 == 1 else 'papakha_fur',
                  T(0.128 * math.cos(a_), 0.128 * math.sin(a_), 0.215 + 0.012 * (i % 3)), smooth=True)
        for i in range(26):   # curls of the lambskin round the sides and the brim, some lighter (an irregular outline)
            a_ = math.radians(i * 360 / 26 + (i % 3) * 5)
            z_ = 0.02 + 0.055 * (i % 4) + 0.01 * ((i * 7) % 3)
            rr = 0.017 + 0.005 * ((i * 5) % 3)
            p.add(props.ellipsoid(rr, rr, rr * 1.2, seg=6), 'papakha_fur_hi' if i % 3 == 0 else 'papakha_fur',
                  T(0.126 * math.cos(a_), 0.126 * math.sin(a_), z_), smooth=True)
        p.add(props.ellipsoid(0.085, 0.085, 0.022, seg=20), key, T(0, 0, 0.24), smooth=True, tint=t)   # the cloth crown
        for a_ in (0, 90):
            p.add(props.tube([(-0.075, 0, 0.245), (0, 0, 0.264), (0.075, 0, 0.245)], 0.006, seg=6), 'gold', R('Z', a_), smooth=True)
        return
    key, t = _k(tinted, 'fur')
    p.add(props.lathe([(0.0, 0.112), (0.02, 0.118), (0.17, 0.12), (0.19, 0.11), (0.2, 0.0)], seg=24), key,
          smooth=True, tint=t)


@hat('shako')
def shako(p, c, tinted, plume_tinted, cords=False, peak='leather_dark', peak_tilt=-20.0, peak_w=0.09, straight=False, plume_k=1.0, band='leather_dark', cord_down=True, swag_z=0.13):
    """A hussar's shako: a flaring cylinder, a peak in front, a plume;
    cords=True adds the gold top band, cockade and cords swagged across the front
    (at eye level once the hat faces forward); cords='high' swags them across the
    shako's upper front instead, with the raquettes at the side and a cord down
    behind the ear to the shoulder (the realism pass: the low swags read as a visor).
    peak: the peak's material key; peak_tilt: its pitch (degrees, more negative = up);
    peak_w: its half width."""
    key, t = _k(tinted, 'hat')
    if cords:
        if straight:   # an open gold ring round the top: the crown stays felt (a capped band read as a gold dome)
            p.add(props.tube([(0, 0, 0.17), (0, 0, 0.2)], [0.123, 0.125], seg=24, caps=False), 'gold', smooth=True)
        else:
            p.add(props.lathe([(0.17, 0.123), (0.2, 0.125)], seg=24), 'gold', smooth=True)   # the top band
        p.add(props.ellipsoid(0.03, 0.012, 0.03, seg=12), 'gold', T(0, 0.12, 0.165), smooth=True)   # the cockade
    if cords == 'high':
        for k in range(2):   # two cords swagged across the upper front, from raquette to raquette
            z = swag_z - 0.03 * k   # swag_z: the upper swag's height (higher keeps the cords off a short shako's brow)
            pts = [(-0.115, 0.045, z + 0.025), (-0.06, 0.112, z - 0.01), (0.0, 0.124, z - 0.022), (0.06, 0.112, z - 0.01),
                   (0.115, 0.045, z + 0.025)]
            p.add(props.tube(pts, 0.011, seg=6), 'gold', smooth=True)
        for sx in (-1, 1):   # the raquettes (flat woven cord ends) at each side
            p.add(props.ellipsoid(0.012, 0.028, 0.034, seg=10), 'gold', T(sx * 0.122, 0.04, swag_z + 0.02), smooth=True)
        if cord_down:
            p.add(props.tube([(0.12, 0.02, 0.12), (0.135, -0.02, 0.02), (0.13, -0.07, -0.1), (0.12, -0.1, -0.2)],
                             [0.01, 0.01, 0.009, 0.007], seg=6), 'gold', smooth=True)   # the cord down behind the ear
    elif cords:
        for k in range(2):   # two cords swagged across the front, ending in flounders at the side
            z = 0.06 - 0.035 * k
            pts = [(-0.11, 0.05, z + 0.06), (-0.06, 0.11, z), (0.0, 0.125, z - 0.015), (0.06, 0.11, z), (0.11, 0.05, z + 0.06)]
            p.add(props.tube(pts, 0.009, seg=6), 'gold', smooth=True)
        p.add(props.tube([(0.115, 0.03, 0.12), (0.13, 0.0, 0.0), (0.12, -0.04, -0.12)], [0.009, 0.009, 0.006], seg=6),
              'gold', smooth=True)   # the cord down to the shoulder
    if straight:   # a straight-sided felt cylinder (the realism pass), its top band and plume as before
        p.add(props.lathe([(0.0, 0.112), (0.19, 0.12), (0.2, 0.0)], seg=24), key, smooth=True, tint=t)
    else:
        p.add(props.lathe([(0.0, 0.105), (0.19, 0.12), (0.2, 0.0)], seg=24), key, smooth=True, tint=t)
    bw_ = 0.11 if band == 'leather_dark' else 0.118   # a coloured band stands proud of the felt
    p.add(props.lathe([(0.0, bw_), (0.02 if band == 'leather_dark' else 0.03, bw_)], seg=24), band, smooth=True)   # band: the bottom band's material
    pi_ = 0.07 if peak_w == 0.09 else peak_w * 0.78
    visor = props.slab([(-peak_w, 0.0), (peak_w, 0.0), (pi_, 0.05), (-pi_, 0.05)], 0.008, 'X')
    p.add(visor, peak, T(0, 0.1, 0.01) @ R('Z', 90) @ R('X', peak_tilt), bevel=0.002)
    pk, pt = _k(plume_tinted, 'plume')
    if cords:   # a taller plume, bushy at the top
        pz = lambda z: 0.18 + (z - 0.18) * plume_k   # plume_k: a taller plume (the realism pass)
        p.add(props.tube([(0, 0.08, 0.18), (0, 0.085, pz(0.24)), (0, 0.078, pz(0.29)), (0, 0.068, pz(0.31))],
                         [0.016, 0.03 * min(plume_k, 1.3), 0.026 * min(plume_k, 1.3), 0.006], seg=8), pk, smooth=True, tint=pt)
        return
    p.add(props.tube([(0, 0.07, 0.19), (0, 0.075, 0.3), (0, 0.06, 0.36)], [0.022, 0.02, 0.005], seg=8), pk,
          smooth=True, tint=pt)


@hat('hat_cone')
def hat_cone(p, c, tinted, _, tassel=False, height=1.0, brim=None, brim_wide=False):
    """A tall conical felt cap with a thick white fleece brim (Civ3's Rider);
    tassel: a red horsehair tassel on the top (the realism pass); height: the cone's."""
    key, t = _k(tinted, 'hat')
    top = 0.03 + 0.25 * height
    p.add(props.lathe([(0.03, 0.11), (top, 0.0)], seg=24), key, smooth=True, tint=t)
    if brim:   # (the realism pass) a slimmer, darker fur brim
        props.DEFAULTS.setdefault('hat_fur', (brim, 0.95, 0.0, 'rope'))
        c.setdefault('hat_fur', brim)
        prof = ([(-0.015, 0.118), (0.0, 0.152), (0.05, 0.158), (0.078, 0.124)] if brim_wide else   # (C4) a wide thick fur roll
                [(-0.005, 0.118), (0.008, 0.13), (0.035, 0.131), (0.05, 0.12)])
        p.add(props.lathe(prof, seg=24), 'hat_fur', smooth=True)
    else:
        p.add(props.lathe([(-0.01, 0.12), (0.01, 0.138), (0.05, 0.14), (0.07, 0.122)], seg=24), 'plume', smooth=True)
    if tassel:
        props.DEFAULTS.setdefault('feather_red', ('#a8302a', 0.9, 0.0, 'cloth'))
        p.add(props.ellipsoid(0.012, 0.012, 0.012, seg=8), 'gold', T(0, 0, top), smooth=True)
        tk = tassel if isinstance(tassel, (int, float)) and not isinstance(tassel, bool) else 1.0   # a number: its size
        p.add(props.lathe([(top - 0.005, 0.0), (top + 0.01, 0.035 * tk), (top - 0.07 * tk, 0.05 * tk), (top - 0.11 * tk, 0.0)],
                          seg=12), 'feather_red', smooth=True)   # the tassel falling round the top of the cone
        if brim_wide:   # (C4) a dark edge where the tassel ends, so it reads on a red civ
            props.DEFAULTS.setdefault('tassel_edge', ('#2a0c0a', 0.9, 0.0, 'cloth'))
            zr = top - 0.085 * tk
            rr_ = 0.11 * (top - zr) / (top - 0.03) + 0.006
            p.add(props.lathe([(zr - 0.012, rr_ + 0.004), (zr + 0.012, rr_ - 0.002)], seg=16), 'tassel_edge', smooth=True)


@hat('wild_shock')
def wild_shock(p, c, tinted, _, tufts=46, reach=0.085):
    """Einstein's hair (SciLeader) as meshes, not particles (those read as
    a ball over the face): a cap over the back of the scalp and a shock of
    curled tufts standing out from the sides, back and crown, none in front
    of the face (a receding hairline)."""
    props.DEFAULTS.setdefault('hair_shock', ('#ecebe6', 0.85, 0.0, 'cloth'))
    p.add(props.ellipsoid(0.106, 0.112, 0.1, seg=20), 'hair_shock', T(0, -0.025, 0.025), smooth=True)
    for sx in (-1, 1):   # soft puffs over the ears, swept back
        p.add(props.ellipsoid(0.06, 0.085, 0.07, seg=16), 'hair_shock', T(sx * 0.105, -0.045, 0.02), smooth=True)
    p.add(props.ellipsoid(0.1, 0.07, 0.075, seg=16), 'hair_shock', T(0, -0.1, 0.03), smooth=True)   # and at the back
    rnd = props.random.Random(5)
    for i in range(tufts):
        az = math.radians(rnd.uniform(80, 280))            # from the front (+Y): the sides and back, clear of the brow
        el = math.radians(rnd.uniform(-10, 62))
        d = Vector((math.sin(az) * math.cos(el), math.cos(az) * math.cos(el), math.sin(el)))
        base = Vector((0, -0.015, 0.02)) + Vector((d.x * 0.098, d.y * 0.105, d.z * 0.095))
        out = (d + Vector((rnd.uniform(-0.2, 0.2), rnd.uniform(-0.45, -0.05), rnd.uniform(0.05, 0.35)))).normalized()   # swept back and up
        L = reach * rnd.uniform(0.6, 1.25)
        mid = base + out * L * 0.55 + Vector((rnd.uniform(-0.02, 0.02), rnd.uniform(-0.02, 0.02), 0.015))
        tip = base + out * L + Vector((rnd.uniform(-0.03, 0.03), rnd.uniform(-0.03, 0.03), rnd.uniform(-0.01, 0.03)))
        p.add(props.tube([tuple(base), tuple(mid), tuple(tip)], [0.036, 0.034, 0.016], seg=10), 'hair_shock', smooth=True)   # round, soft curls


@hat('fur_hat')
def fur_hat(p, c, tinted, _, spike=False):
    """A Mongol hat: a thick fur brim and a rounded crown; spike=True makes it
    the Keshik's helmet: a bronze crown with a spike and a plume tuft."""
    key, t = _k(tinted, 'hat')
    if spike:
        p.add(props.lathe([(0.0, 0.112), (0.08, 0.1), (0.15, 0.05), (0.2, 0.012)], seg=24), 'bronze', T(0, 0, 0.02), smooth=True)
        vkit_spike = props.tube([(0, 0, 0.2), (0, 0, 0.3)], [0.012, 0.003], seg=6)
        p.add(vkit_spike, 'bronze', smooth=True)
        p.add(props.tube([(0, 0, 0.27), (0, -0.04, 0.33), (0, -0.1, 0.32)], [0.015, 0.02, 0.005], seg=6), key, smooth=True, tint=t)
    else:
        p.add(props.ellipsoid(0.105, 0.105, 0.14, seg=20), key, T(0, 0, 0.04), smooth=True, tint=t)
    p.add(props.lathe([(0.0, 0.13), (0.03, 0.145), (0.07, 0.13)], seg=24), 'fur', smooth=True)


@hat('dragoon_helm')
def dragoon_helm(p, c, tinted, crest_tinted, crest=1.0, mane=False, peak=None):
    """A dragoon's brass helmet: a dome, a peak and a horsehair crest
    (crest: the crest's thickness, so a fuller one reads at 1x)."""
    props.DEFAULTS.setdefault('helm_brass', ('#a8843a', 0.55, 0.8, 'metal'))   # a duller brass than the trim
    # the skull stops at the brow, so the face shows (Civ3's open dragoon helmet)
    p.add(props.lathe([(0.0, 0.114), (0.06, 0.112), (0.12, 0.085), (0.16, 0.04), (0.175, 0.0)], seg=24),
          'helm_brass', T(0, -0.01, 0.0), smooth=True)
    p.add(props.lathe([(-0.005, 0.118), (0.02, 0.118)], seg=24), 'leather_dark', T(0, -0.01, 0.0), smooth=True)   # the band
    if peak == 'brass':   # (the realism pass) a curved brass peak over the brow and a rounded neck guard behind
        def brim(a0, a1, r0, r1, drop, n=14):
            rings = [[], []]
            for i in range(n + 1):
                a = math.radians(a0 + (a1 - a0) * i / n)
                rings[0].append((r0 * math.sin(a), r0 * math.cos(a), 0.0))
                rings[1].append((r1 * math.sin(a), r1 * math.cos(a) * 1.15, -drop))
            bm = props.loft(rings, False, False)
            props.bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.006)
            return bm
        p.add(brim(-62, 62, 0.112, 0.16, 0.025), 'helm_brass', T(0, -0.01, -0.002), smooth=True)        # the peak
        p.add(brim(125, 235, 0.112, 0.15, 0.05), 'helm_brass', T(0, -0.01, 0.0), smooth=True)          # the neck guard
    else:
        neck = props.slab([(-0.1, 0.0), (0.1, 0.0), (0.085, -0.09), (-0.085, -0.09)], 0.008, 'X')
        p.add(neck, 'helm_brass', T(0, -0.115, 0.01) @ R('Z', 90) @ R('X', 15), bevel=0.002)   # a neck guard
        visor = props.slab([(-0.09, 0.0), (0.09, 0.0), (0.07, 0.06), (-0.07, 0.06)], 0.008, 'X')
        p.add(visor, 'leather_dark', T(0, 0.1, 0.0) @ R('Z', 90) @ R('X', -20), bevel=0.002)   # the peak
    comb = props.slab([(0.09, 0.1), (0.04, 0.2), (-0.06, 0.2), (-0.12, 0.1)], 0.02, 'X')
    p.add(comb, 'helm_brass', bevel=0.004)
    ck, ct = _k(crest_tinted, 'crest')
    path = [(0, 0.08, 0.2), (0, 0.0, 0.23), (0, -0.08, 0.22), (0, -0.15, 0.15), (0, -0.18, 0.06)]   # on the comb
    p.add(props.tube(path, [r * crest for r in (0.022, 0.032, 0.034, 0.03, 0.012)], seg=8, scale_xy=(0.6, 1.0)), ck, smooth=True, tint=ct)
    if mane:   # a black horsehair mane from the comb down the back of the neck
        props.DEFAULTS.setdefault('horsehair', ('#141210', 0.8, 0.0, 'rope'))
        if peak == 'brass':   # (pass 6) narrower, down the back only
            p.add(props.tube([(0, -0.1, 0.2), (0, -0.2, 0.08), (0, -0.23, -0.1), (0, -0.21, -0.3), (0, -0.18, -0.44)],
                             [0.03, 0.04, 0.038, 0.03, 0.01], seg=8, scale_xy=(0.7, 0.8)), 'horsehair', smooth=True)
        else:
            p.add(props.tube([(0, -0.08, 0.2), (0, -0.19, 0.08), (0, -0.22, -0.12), (0, -0.2, -0.32), (0, -0.16, -0.46)],
                             [0.035, 0.055, 0.055, 0.045, 0.012], seg=8, scale_xy=(1.0, 0.6)), 'horsehair', smooth=True)


@hat('morion')
def morion(p, c, tinted, _):
    """A conquistador's morion: a steel dome with a comb, the brim upswept fore and aft."""
    p.add(props.ellipsoid(0.11, 0.115, 0.12, seg=20), 'helmet', T(0, 0, 0.03), smooth=True)
    comb = props.slab([(-0.11, 0.05), (-0.07, 0.2), (0.0, 0.23), (0.07, 0.2), (0.11, 0.05)], 0.014, 'X')   # a tall comb
    p.add(comb, 'helmet', bevel=0.003)
    brim = []
    for i in range(41):
        a = 2 * math.pi * i / 40
        up = 0.12 * abs(math.sin(a)) ** 4   # swept up into points front and back (+-Y)
        brim.append([(0.11 * math.cos(a) * 0.95, 0.115 * math.sin(a) * 0.95, 0.02),
                     (0.14 * math.cos(a), 0.21 * math.sin(a), 0.02 + up)])
    rings = [[r[0] for r in brim], [r[1] for r in brim]]
    p.add(props.loft(rings, False, False), 'helmet', smooth=True)


@hat('great_helm')
def great_helm(p, c, tinted, _, crest=None, top=0.09, bottom=-0.23):
    """A 13th-century great helm (heaume): a tall flat-sided barrel about 1.4x
    taller than wide, a slightly domed flat top, the eye slits a third of the
    way down either side of a vertical bar (a cross with the brow band),
    breaths below; crest='horns' adds curved horns, 'plume' a civ plume.
    top/bottom: the barrel's ends from the brow line (a rider's helm, worn
    by headgear() on the bone, needs a higher top to close over the crown)."""
    W, D = 0.115, 0.125               # half width and half depth
    z0, z1 = bottom, top               # 0.32 tall against 0.23 wide
    rings = []
    for z in (z0, z0 + 0.02, -0.1, 0.0, z1 - 0.01, z1):
        k = 1.0 if z < z1 - 0.005 else 0.92
        ring = []
        for i in range(32):
            a = 2 * math.pi * i / 32
            ca, sa = math.cos(a), math.sin(a)
            ring.append((W * k * math.copysign(abs(ca) ** 0.45, ca), D * k * math.copysign(abs(sa) ** 0.45, sa), z))
        rings.append(ring)
    p.add(props.loft(rings, False, False), 'helmet', smooth=False)
    p.add(props.ellipsoid(W * 0.95, D * 0.95, 0.03, seg=20), 'helmet', T(0, 0, z1), smooth=True)   # the domed top plate
    ze = z1 - 0.35 / 3                 # the eye line
    for sx in (-1, 1):
        p.add(props.box(0.085, 0.02, 0.016), 'leather_dark', T(sx * 0.058, D + 0.002, ze))         # the eye slits
        for k in range(4):                                                                        # breaths
            p.add(props.box(0.01, 0.02, 0.006), 'leather_dark', T(sx * (0.035 + 0.02 * k), D + 0.002, ze - 0.075))
            p.add(props.box(0.01, 0.02, 0.006), 'leather_dark', T(sx * (0.035 + 0.02 * k), D + 0.002, ze - 0.1))
    p.add(props.box(0.026, 0.018, 0.27), 'gold', T(0, D + 0.006, z0 + 0.15), bevel=0.003)          # the cross: vertical bar
    p.add(props.box(0.25, 0.018, 0.024), 'gold', T(0, D + 0.006, ze + 0.028), bevel=0.003)          # and the brow band over the slits
    for sx in (-1, 1):                                                                            # rivets along the bar
        for k in range(4):
            p.add(props.ellipsoid(0.007, 0.007, 0.007, seg=6), 'helmet', T(sx * 0.02, D + 0.012, z0 + 0.04 + 0.06 * k), smooth=True)
    if crest == 'horns':
        for sx in (-1, 1):
            p.add(props.tube([(sx * 0.08, 0, z1), (sx * 0.15, 0, z1 + 0.07), (sx * 0.16, 0.02, z1 + 0.17), (sx * 0.13, 0.04, z1 + 0.23)],
                             [0.03, 0.025, 0.016, 0.004], seg=8), 'helmet', smooth=True)
    elif crest == 'plume':
        key, t_ = _k(True, 'plume')
        p.add(props.tube([(0, 0, z1), (0, -0.04, z1 + 0.14), (0, -0.14, z1 + 0.18)], [0.03, 0.03, 0.006], seg=8), key, smooth=True, tint=t_)


@hat('spiked_helm')
def spiked_helm(p, c, tinted, plume_tinted, open_face=False, nasal=False, peak=False, plume_tip=0.004, spike_tip=0.0, plume_r=1.0):
    """A sipahi's helmet: a tall pointed steel bowl with a spike, a mail aventail and a plume.
    (the realism pass) open_face: the aventail round the sides and back only, the face open; nasal: a nasal bar;
    peak: a small peak over the brow (the chichak)."""
    p.add(props.lathe([(-0.01, 0.115), (0.08, 0.11), (0.17, 0.05), (0.24, 0.008 + spike_tip), (0.3, spike_tip)], seg=24), 'helmet',
          smooth=True)   # spike_tip: (the realism pass) a blunt spike, one piece at 2x
    if open_face:
        rings = []
        for z, r in ((0.0, 0.118), (-0.17, 0.132)):
            rings.append([(r * math.sin(math.radians(a)), r * math.cos(math.radians(a)) - 0.01, z)
                          for a in range(55, 306, 10)])
        bm = props.loft(rings, False, False)
        props.bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.008)
        p.add(bm, 'mail', smooth=True)
    else:
        p.add(props.lathe([(-0.16, 0.13), (0.0, 0.118)], seg=24), 'mail', T(0, -0.01, 0), smooth=True)
    if nasal:
        p.add(props.tube([(0, 0.118, 0.03), (0, 0.128, -0.075)], [0.009, 0.007], seg=6), 'helmet', smooth=True)
    if peak:
        p.add(props.ellipsoid(0.09, 0.05, 0.01, seg=16), 'helmet', T(0, 0.1, 0.005) @ R('X', -8), smooth=True)
    pk, pt = _k(plume_tinted, 'plume')
    p.add(props.tube([(0, 0, 0.22), (0, -0.05, 0.3), (0, -0.14, 0.31)], [0.02 * plume_r, 0.016 * plume_r, plume_tip], seg=6), pk,
          smooth=True, tint=pt)


@hat('head_wrap')
def head_wrap(p, c, tinted, _, wrapped=False, white=False):
    """A turban: a wound cloth around a cap. wrapped: (the realism pass) layered,
    crossing wraps over a cloth crown and a tail hanging at the back, no plume."""
    key, t = _k(tinted, 'hat')
    if wrapped:
        wk, wt = key, t
        if white:   # (the realism pass) white wraps, the civ only on one band and the tail end
            props.DEFAULTS.setdefault('turban_white', ('#ece8de', 0.85, 0.0, 'cloth'))
            wk, wt = 'turban_white', False
        for i in range(5):
            z = 0.005 + 0.026 * i
            r = 0.13 - 0.011 * i
            k_, t_ = (key, t) if (white and i == 1) else (wk, wt)
            p.add(props.lathe([(0.0, r), (0.034, r - 0.004)], seg=28), k_,
                  T(0, 0, z) @ R('Z', 37 * i) @ R('X', 9 * (-1) ** i), smooth=True, tint=t_)
        p.add(props.ellipsoid(0.09, 0.095, 0.055, seg=18), wk, T(0, -0.01, 0.12), smooth=True, tint=wt)
        p.add(props.tube([(0.02, -0.11, 0.06), (0.05, -0.17, -0.06), (0.06, -0.17, -0.22)], [0.04, 0.036, 0.03], seg=10,
                         scale_xy=(1.0, 0.35)), key, smooth=True, tint=t)   # the tail end
        return
    for i in range(5):
        z = 0.01 + 0.03 * i
        r = 0.12 - 0.008 * i
        p.add(props.lathe([(0.0, r), (0.028, r)], seg=24), key, T(0, 0, z) @ R('X', 6 * (-1) ** i),
              smooth=True, tint=t)
    # a white crown and a small white plume above the wound civ-colored cloth (Civ3's Ansar)
    p.add(props.ellipsoid(0.095, 0.095, 0.075, seg=18), 'plume', T(0, 0, 0.13), smooth=True)
    p.add(props.tube([(0, 0.01, 0.19), (0, -0.01, 0.27), (0, -0.05, 0.31)], [0.026, 0.022, 0.006], seg=8), 'plume',
          smooth=True)


@hat('feather_bonnet')
def feather_bonnet(p, c, tinted, _, trailer=True, n=17, width=0.05, tip_cord=False, dark_tips=False, tip_plumes=False, spread=1.0, cone=0.0, clean=False):
    """A plains-style war bonnet as Civ3's Mounted Warrior wears: a beaded band,
    a halo of flat feathers swept back, white with civ-colored tips, and
    (trailer) a long tail of feathers down the back to the saddle."""
    p.add(props.lathe([(0.0, 0.114), (0.04, 0.114)], seg=24), 'tint', smooth=True, tint=True)
    for k, a in enumerate((-90, 90)):   # beaded rosettes at the temples, with ermine drops
        s = math.radians(a)
        p.add(props.ellipsoid(0.03, 0.012, 0.03, seg=12), 'plume', T(0.118 * math.sin(s), 0.0, 0.02) @ R('Z', -a), smooth=True)
        p.add(props.tube([(0.12 * math.sin(s), 0.0, 0.0), (0.13 * math.sin(s), 0.0, -0.14)], [0.014, 0.01], seg=6),
              'plume', smooth=True)
    fk = 'plume'
    if clean:   # (the realism pass) near-white feathers without the cloth mottle, crisp dark tips
        props.DEFAULTS.setdefault('feather_white', ('#f6f4ee', 0.8, 0.0, None))
        props.DEFAULTS.setdefault('feather_dark', ('#201812', 0.8, 0.0, None))
        fk = 'feather_white'
    for i in range(n):   # n, width: (the realism pass) more, wider feathers stay one piece at 2x
        a = math.radians(60 + 240 * i / (n - 1))   # round the sides and back (from +Y, the front): the face stays open
        base = (0.11 * math.sin(a), 0.11 * math.cos(a), 0.03)
        up = 0.17 * max(0.0, -math.cos(a))   # tallest at the back, so the halo rises over the head seen from the front
        mid = (0.19 * math.sin(a), 0.16 * math.cos(a) - 0.03, 0.2 + up * 0.5)
        tip = (0.27 * math.sin(a) * spread, (0.21 * math.cos(a) - 0.06) * spread, 0.33 + up + 0.06 * (spread - 1) * 4)   # fanned out round the head (spread: wider and taller)
        if cone:   # (the realism pass) a shallow cone: the side feathers swept back and up, out of the halo's plane
            sa = abs(math.sin(a))
            mid = (mid[0] * (1 - 0.15 * cone), mid[1] - 0.1 * cone * sa, mid[2])
            tip = (tip[0] * (1 - 0.2 * cone), tip[1] - 0.22 * cone * sa, tip[2] - 0.02 * cone * sa)
        props.feather_card(p, base, mid, (math.sin(a), math.cos(a), 0.0), width, fk, curl=-0.08)
        if dark_tips:   # (the realism pass) eagle feathers: white, dark only on the outer fifth; the civ colour on the browband
            props.DEFAULTS.setdefault('feather_dark', ('#2a2018', 0.85, 0.0, 'cloth'))
            props.feather_card(p, mid, tip, (math.sin(a), math.cos(a), 0.0), width, fk, curl=-0.08,
                               tip_key='feather_dark', tip_frac=0.38)   # the dark outer fifth of the whole feather, on one card
            if tip_plumes:   # a small civ fluff at each feather's tip
                p.add(props.ellipsoid(0.03, 0.03, 0.04, seg=8), 'tint', T(*tip), smooth=True, tint=True)
        else:
            props.feather_card(p, mid, tip, (math.sin(a), math.cos(a), 0.0), width, 'tint', tint=True, curl=-0.08)
    if tip_cord:   # a thong through the feathers near their tips (the realism pass: the fan stays one piece at 2x)
        cord = []
        for i in range(n):
            a = math.radians(60 + 240 * i / (n - 1))
            up = 0.17 * max(0.0, -math.cos(a))
            cord.append((0.24 * math.sin(a), 0.19 * math.cos(a) - 0.05, 0.29 + up * 0.92))
        p.add(props.tube(cord, 0.012, seg=6), 'tint', smooth=True, tint=True)
    if trailer == 'short':   # (the realism pass) a short trailer to the shoulder blades, its feathers overlapping (one piece)
        for j in range(5):
            z = -0.02 - 0.085 * j
            y = -0.14 - 0.03 * min(j, 2)
            for sx in (-1, 1):
                props.feather_card(p, (sx * 0.015, y, z), (sx * 0.1, y - 0.05, z - 0.11), (sx, -0.6, 0.0), 0.06, fk,
                                   curl=-0.05, **({'tip_key': 'feather_dark', 'tip_frac': 0.3} if dark_tips else {}))
        p.add(props.tube([(0, -0.12, 0.02), (0, -0.17, -0.2), (0, -0.21, -0.42)], 0.024, seg=6), 'tint', smooth=True, tint=True)
    elif trailer:
        for j in range(9):   # the trailer: pairs of feathers down a band behind the back
            z = -0.05 - 0.13 * j
            y = -0.15 - 0.05 * min(j, 3)
            for sx in (-1, 1):
                base = (sx * 0.02, y, z)
                tip = (sx * 0.15, y - 0.06, z - 0.08)
                key, t = (('tint', True) if j % 2 else ('plume', False))
                props.feather_card(p, base, tip, (sx, -0.6, 0.0), 0.045, key, tint=t, curl=-0.05)
        p.add(props.tube([(0, -0.12, 0.0), (0, -0.2, -0.3), (0, -0.25, -1.1)], 0.02, seg=6), 'tint', smooth=True, tint=True)


@hat('khepresh')
def khepresh(p, c, tinted, _):
    """The Egyptian blue war crown: a tall bulbous cap swelling to the back."""
    key, t = _k(tinted, 'hat')
    crown = props.ellipsoid(0.11, 0.15, 0.17, seg=20)
    p.add(crown, key, T(0, -0.04, 0.09), smooth=True, tint=t)
    p.add(props.lathe([(0.0, 0.112), (0.025, 0.112)], seg=24), 'gold', smooth=True)


# ------------------------------------------------------------ hats for the foot units (Mac session)

props.DEFAULTS.setdefault('tint', ('#ffffff', 0.7, 0.0, 'cloth'))
props.DEFAULTS.setdefault('felt', ('#1e1a18', 0.85, 0.0, 'cloth'))
props.DEFAULTS.setdefault('khaki', ('#8a7a56', 0.85, 0.0, 'cloth'))
props.DEFAULTS.setdefault('hardhat', ('#d8a630', 0.45, 0.0, None))


def _brim(p, r_in, r_out, z, key, up=None, tint=False, seg=40, tilt=0.0):
    """A hat brim: a flat ring from r_in to r_out at height z; up(a) lifts the
    outer edge at angle a (0 = front, +Y)."""
    rings = [[], []]
    for k in range(seg):
        a = 2 * math.pi * k / seg
        lift = up(a) if up else 0.0
        rings[0].append((r_in * math.sin(a), r_in * math.cos(a), z))
        rings[1].append((r_out * math.sin(a), r_out * math.cos(a), z + lift + tilt * math.cos(a)))
    rings = [r + [r[0]] for r in rings]
    bm = props.loft(rings, False, False)
    props.bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.008)
    p.add(bm, key, smooth=True, tint=tint)


@hat('fan_headdress')
def fan_headdress(p, c, tinted, _, fan_tint=True, rim_key='feather', n=17):
    """Chasquis Scout: a broad upright fan behind the head, its field in the
    civ color, a white-and-gold rim; a band round the brow. (fan_tint=False,
    the ranged family's option: the fan of natural white feathers; rim_key:
    the rim's colour key.)"""
    p.add(props.lathe([(0.0, 0.112), (0.035, 0.112)], seg=24), 'gold', smooth=True)
    fk, ft = ('tint', True) if fan_tint else ('feather', False)
    for i in range(n):
        a = math.radians(-80 + 160 * i / (n - 1))
        base = (0.0, -0.06, 0.06)
        tip = (0.26 * math.sin(a), -0.08, 0.06 + 0.26 * math.cos(a))
        p.add(props.tube([base, tip], [0.02, 0.03], seg=5, scale_xy=(1.0, 0.25)), fk, smooth=True, tint=ft)
    rim = [(0.29 * math.sin(math.radians(a)), -0.082, 0.06 + 0.29 * math.cos(math.radians(a))) for a in range(-82, 83, 6)]
    p.add(props.tube(rim, 0.014, seg=6), rim_key, smooth=True, tint=rim_key == 'tint')
    p.add(props.tube([(x * 1.04, y, z) for x, y, z in rim], 0.008, seg=6), 'gold', smooth=True)


@hat('feather_headdress')
def feather_headdress(p, c, tinted, _, style=None, cascade=None, front=None, crest=None):
    """Tall upright feathers in a crown (Impi, Aztec, Mayan, Iroquois):
    alternately civ-colored and natural, on a headband. style='bushy' (Impi):
    many short feathers, all civ-colored, in two rows splaying into a round
    bushy crown; style='crown' (King Zulu): a dense untinted fan of cream
    feathers and fur round the head. cascade='back' (King Mayan): a long
    fall of white feathers down the back to the waist. crest='tint': the whole
    upright fan civ-colored (King Mayan's solid crest)."""
    if front:   # small red and green feathers at the front (King Mayan)
        for i, key in enumerate(('feather_green', 'feather_red', 'feather_green', 'feather_red', 'feather_green',
                                 'feather_red', 'feather_green')):
            a = math.radians(-54 + 18 * i)   # tufts big enough to read at 1x
            base = (0.11 * math.sin(a), 0.11 * math.cos(a), 0.03)
            mid = (0.15 * math.sin(a), 0.17 * math.cos(a), 0.13)
            tip = (0.19 * math.sin(a), 0.2 * math.cos(a), 0.25)
            p.add(props.tube([base, mid, tip], [0.03, 0.05, 0.03], seg=6, scale_xy=(1.0, 0.45)), key, smooth=True)
    if cascade == 'back':
        for i in range(9):
            x = -0.09 + 0.18 * i / 8
            pts = [(x, -0.1, 0.12), (x * 1.3, -0.2, 0.02), (x * 1.5, -0.25, -0.35), (x * 1.6, -0.26, -0.75),
                   (x * 1.7, -0.24, -1.05)]   # down to the waist
            p.add(props.tube(pts, [0.03, 0.045, 0.045, 0.04, 0.025], seg=6, scale_xy=(1.0, 0.35)), 'feather', smooth=True)
    if style == 'crown':
        p.add(props.lathe([(0.0, 0.118), (0.05, 0.12)], seg=24), 'fur', smooth=True)
        for row, (n, lift, spread, h) in enumerate(((22, 0.02, 0.22, 0.13), (16, 0.05, 0.17, 0.17))):
            for i in range(n):
                a = 2 * math.pi * (i + 0.5 * row) / n
                base = (0.115 * math.sin(a), 0.115 * math.cos(a), lift)
                tip = (spread * math.sin(a), spread * math.cos(a), lift + h)
                p.add(props.tube([base, tip], [0.03, 0.022], seg=6, scale_xy=(1.0, 0.45)), 'feather', smooth=True)
        return
    p.add(props.lathe([(0.0, 0.114), (0.04, 0.114)], seg=24), 'tint', smooth=True, tint=True)
    if style == 'bushy':
        rnd = props.random.Random(17)   # a little irregular, not a brush
        for row, (n, lift, spread, h) in enumerate(((18, 0.02, 0.2, 0.15), (13, 0.06, 0.14, 0.17))):
            for i in range(n):
                a = 2 * math.pi * (i + 0.5 * row + rnd.uniform(-0.25, 0.25)) / n
                base = (0.11 * math.sin(a), 0.11 * math.cos(a), lift)
                sp, hh = spread * rnd.uniform(0.85, 1.15), h * rnd.uniform(0.75, 1.2)
                tip = (sp * math.sin(a), sp * math.cos(a), lift + hh)
                # a flat tapered card curling outward (tubes read as red sausages)
                props.feather_card(p, base, tip, (math.sin(a), math.cos(a), 0.0), 0.075, 'tint', tint=True, curl=-0.18)
        return
    if style == 'fan':   # King Aztec: a dense upright fan behind the head, gold, civ, red and teal in turn
        props.DEFAULTS.setdefault('feather_teal', ('#2e8a86', 0.7, 0.0, 'cloth'))
        p.add(props.lathe([(0.0, 0.116), (0.06, 0.118)], seg=24), 'gold', smooth=True)   # the gold band
        props.DEFAULTS['feather_gold'] = ('#e8c048', 0.5, 0.0, 'polished')
        cycle = (('feather_gold', False), ('tint', True), ('feather_gold', False), ('feather_red', False), ('tint', True), ('feather_gold', False))
        for row, (n, h, r0, lift) in enumerate(((17, 0.44, 0.11, 0.03), (13, 0.32, 0.105, 0.06))):
            for i in range(n):
                a = math.radians(-100 + 200 * i / (n - 1)) + math.pi   # round the sides and back
                base = (0.1 * math.sin(a), 0.1 * math.cos(a) - 0.02, lift)
                spread = 0.02 + 0.2 * abs(math.sin(a - math.pi)) ** 1.5   # the back feathers stand upright (outward ones foreshortened to a gap)
                tip = ((r0 + spread) * math.sin(a), (r0 + spread * 0.6) * math.cos(a) - 0.06, lift + h * (1 - 0.3 * abs(math.sin(a - math.pi))))   # tallest at the back: one fan
                key, t = cycle[(i + row * 3) % 6]
                props.feather_card(p, base, tip, (math.sin(a), math.cos(a), 0.0), 0.08, key, tint=t, curl=-0.12)
        for i in range(7):   # the centre back column, filling the V between the two sides
            a = math.pi + math.radians(-27 + 9 * i)
            base = (0.1 * math.sin(a), 0.1 * math.cos(a) - 0.02, 0.04)
            tip = (0.12 * math.sin(a), 0.13 * math.cos(a) - 0.03, 0.6)   # as tall as the wing tips, straight up
            key, t = cycle[i % 6]
            props.feather_card(p, base, tip, (math.sin(a), math.cos(a), 0.0), 0.11, key, tint=t, curl=-0.04)
        return
    n = 11
    for i in range(n):
        a = math.radians(-140 + 280 * i / (n - 1))
        base = (0.11 * math.sin(a), 0.11 * math.cos(a) - 0.01, 0.03)
        h = 0.3 - 0.06 * abs(i - n // 2) / (n // 2)
        tip = (0.16 * math.sin(a), 0.14 * math.cos(a) - 0.06, 0.03 + h)
        key, t = (('tint', True) if i % 2 or crest == 'tint' else ('feather', False))   # crest='tint': all civ
        p.add(props.tube([base, tip], [0.024, 0.04], seg=5, scale_xy=(1.0, 0.3)), key, smooth=True, tint=t)


@hat('tricorne')
def tricorne(p, c, tinted, _):
    """A tricorne: a low crown and a brim turned up into three points."""
    key, t = _k(tinted, 'felt')
    p.add(props.lathe([(0.0, 0.112), (0.08, 0.105), (0.1, 0.0)], seg=24), key, smooth=True, tint=t)
    _brim(p, 0.11, 0.2, 0.01, key, up=lambda a: 0.09 * (0.5 + 0.5 * math.cos(3 * a)), tint=t)


@hat('bicorne')
def bicorne(p, c, tinted, plume_tinted):
    """A Napoleonic bicorne worn sideways: a tall crescent of felt, a cockade."""
    key, t = _k(tinted, 'felt')
    p.add(props.lathe([(0.0, 0.11), (0.05, 0.105), (0.07, 0.0)], seg=24), key, smooth=True, tint=t)
    shape = props.slab([(-0.24, 0.0), (0.24, 0.0), (0.18, 0.06), (0.08, 0.16), (0.0, 0.18), (-0.08, 0.16), (-0.18, 0.06)],
                       0.05, 'Y')
    p.add(shape, key, T(0, 0, 0.02), bevel=0.01, tint=t)
    ck, ct = _k(plume_tinted, 'plume')
    p.add(props.lathe([(0.0, 0.03), (0.01, 0.03)], seg=16), ck, T(0.1, 0.03, 0.1) @ R('X', 90), smooth=True, tint=ct)


props.DEFAULTS.setdefault('straw', ('#d8c08a', 0.9, 0.0, 'rope'))


@hat('straw_hat')
def straw_hat(p, c, tinted, _):
    """A wide, shallow conical straw hat (the Hwacha crew's): its slopes catch
    the light from every side, so it reads tan from behind too; a dark band
    and chin cord."""
    p.add(props.lathe([(0.0, 0.25), (0.02, 0.22), (0.09, 0.12), (0.15, 0.03), (0.16, 0.0)], seg=32), 'straw', smooth=True)
    p.add(props.lathe([(0.0, 0.25), (0.006, 0.252)], seg=32), 'straw', smooth=True)
    p.add(props.lathe([(0.01, 0.105), (0.03, 0.1)], seg=24), 'leather_dark', smooth=True)
    for sx in (-1, 1):
        p.add(props.tube([(sx * 0.09, 0.0, 0.0), (sx * 0.07, 0.03, -0.12), (0.0, 0.06, -0.2)], 0.005, seg=4),
              'leather_dark', smooth=True)


props.DEFAULTS.setdefault('jingasa_lacquer', ('#1c1612', 0.35, 0.0, 'leather'))   # black-lacquered iron or hide


@hat('jingasa')
def jingasa(p, c, tinted, _, brim=0.23, rise=0.09):
    """An ashigaru's jingasa: a wide, shallow cone of black-lacquered iron or
    hide (or the civ colour when the entry tints its 'hat'), a low crown cap,
    a thin rim, the chin cords."""
    key, t = _k(tinted, 'jingasa_lacquer')
    p.add(props.lathe([(0.0, brim), (0.012, brim - 0.02), (rise * 0.75, 0.1), (rise, 0.035), (rise + 0.006, 0.0)], seg=32),
          key, smooth=True, tint=t)
    p.add(props.lathe([(-0.004, brim + 0.003), (0.01, brim + 0.003)], seg=32), 'leather_dark', smooth=True)   # the rim
    p.add(props.lathe([(0.01, 0.106), (0.04, 0.1)], seg=24), 'leather_dark', smooth=True)                 # the inner band
    for sx in (-1, 1):
        p.add(props.tube([(sx * 0.09, 0.0, 0.0), (sx * 0.07, 0.03, -0.12), (0.0, 0.06, -0.2)], 0.005, seg=4),
              'leather_dark', smooth=True)


@hat('gat')
def gat(p, c, tinted, _, brim=0.27):
    """A Joseon gat: a tall flat-topped crown and a very wide flat brim of
    lacquered horsehair or split bamboo, tied under the chin."""
    p.add(props.lathe([(0.0, 0.085), (0.13, 0.08), (0.14, 0.0)], seg=24), 'hat', smooth=True)
    p.add(props.lathe([(0.0, 0.08), (0.01, brim), (0.016, brim), (0.008, 0.08)], seg=32), 'hat', smooth=True)
    p.add(props.lathe([(0.012, 0.087), (0.035, 0.085)], seg=24), 'leather_dark', smooth=True)   # the band
    for sx in (-1, 1):   # the chin cords
        p.add(props.tube([(sx * 0.085, 0.0, 0.0), (sx * 0.07, 0.03, -0.12), (0.0, 0.06, -0.2)], 0.005, seg=4),
              'leather_dark', smooth=True)


@hat('cavalier')
def cavalier(p, c, tinted, plume_tinted, plume=1.0, tip=0.005):
    """A cavalier's hat (Musketeer): a low crown, a wide soft brim pinned up
    on the left side, and a big plume sweeping back over the pinned side."""
    key, t = _k(tinted, 'felt')
    p.add(props.lathe([(0.0, 0.112), (0.09, 0.108), (0.12, 0.09), (0.13, 0.0)], seg=24), key, smooth=True, tint=t)
    # the brim: wide and drooping a little, but swept up against the crown on the left (-X)
    _brim(p, 0.11, 0.25, 0.0, key, up=lambda a: (0.16 * max(0.0, -math.cos(a)) ** 3) - 0.02 * max(0.0, math.cos(a)),
          tint=t)
    p.add(props.ellipsoid(0.03, 0.03, 0.03, seg=10), 'brass', T(-0.13, 0.0, 0.08), smooth=True)   # the pin
    pk, pt = _k(plume_tinted, 'plume')
    k = plume   # the plume's size (Musketeer: bigger)
    # an ostrich plume: several soft fronds fanning from the pin, each a little different, so it reads as feathers
    rnd = props.random.Random(23)
    for j in range(9):   # many thin fronds spread wide, so they separate at 1x
        dx, dz = rnd.uniform(-0.07, 0.07), rnd.uniform(-0.04, 0.05)
        p.add(props.tube([(-0.12, 0.03, 0.1), (-0.12 - 0.04 * k + dx, -0.06 * k, 0.1 + (0.12 + dz) * k),
                          (-0.12 - 0.02 * k + dx * 1.5, -0.22 * k, 0.1 + (0.14 + dz) * k), (-0.1 + dx * 2, -0.34 * k, 0.1 + (0.04 + dz) * k)],
                         [0.016 * k, 0.026 * k, 0.022 * k, tip], seg=6, scale_xy=(1.0, 0.6)), pk, smooth=True, tint=pt)   # tip: the frond ends' radius (realism infantry: thicker, no specks)


@hat('top_hat')
def top_hat(p, c, tinted, _):
    """A top hat: a tall cylinder, a narrow curled brim and a civ-colored band."""
    p.add(props.lathe([(0.0, 0.11), (0.2, 0.115), (0.21, 0.0)], seg=24), 'felt', smooth=True)
    _brim(p, 0.11, 0.16, 0.0, 'felt', up=lambda a: 0.02 * abs(math.sin(a)))
    p.add(props.lathe([(0.01, 0.113), (0.045, 0.113)], seg=24), 'tint', smooth=True, tint=True)


@hat('kepi')
def kepi(p, c, tinted, _, band=False, slope=8.0, peak=1.0, tall=1.0):
    """A kepi: a flat-topped cap sloping forward, with a short leather peak. (realism infantry) band: the crown
    in the 'hat' colour with a civ band round its base ('dark': the crown civ, if tinted, over a band in the
    'felt' colour, as the French kepi's red crown over its dark band); slope: degrees the crown tips forward; peak: the
    peak's size; tall: the crown's height (a taller, smaller-topped crown reads as a kepi, not a beret)."""
    key, t = _k(tinted and band is not True, 'hat')
    crown = props.lathe([(0.0, 0.11), (0.1 * tall, 0.1 - 0.012 * (tall - 1.0) * 4), (0.11 * tall, 0.0)], seg=24)
    p.add(crown, key, R('X', slope), smooth=True, tint=t)
    if band:
        bk, bt = ('tint', True) if band is True else ('felt', False)
        p.add(props.lathe([(0.0, 0.114), (0.04, 0.112)], seg=24), bk, R('X', slope), smooth=True, tint=bt)
    visor = props.slab([(-0.08 * peak, 0.0), (0.08 * peak, 0.0), (0.06 * peak, 0.05 * peak), (-0.06 * peak, 0.05 * peak)],
                       0.008, 'X')
    p.add(visor, 'leather_dark', T(0, 0.1, 0.0) @ R('Z', 90) @ R('X', -15), bevel=0.002)


@hat('beret')
def beret(p, c, tinted, _):
    """A beret: a soft flat disc tipped to one side."""
    key, t = _k(tinted, 'hat')
    p.add(props.ellipsoid(0.13, 0.12, 0.04, seg=20), key, T(0.02, 0, 0.06) @ R('Y', -12), smooth=True, tint=t)
    p.add(props.lathe([(0.0, 0.11), (0.02, 0.11)], seg=24), 'leather_dark', smooth=True)


@hat('boonie')
def boonie(p, c, tinted, _):
    """A slouch hat with one side of the brim pinned up (Aussie style)."""
    key, t = _k(tinted, 'khaki')
    p.add(props.lathe([(0.0, 0.112), (0.09, 0.105), (0.11, 0.0)], seg=24), key, smooth=True, tint=t)
    _brim(p, 0.11, 0.2, 0.0, key, up=lambda a: 0.1 if math.sin(a) > 0.6 else -0.015, tint=t)


@hat('hard_hat')
def hard_hat(p, c, tinted, _):
    """A worker's hard hat: a ribbed shell with a short peak."""
    key, t = _k(tinted, 'hardhat')
    p.add(props.lathe([(0.0, 0.122), (0.06, 0.115), (0.12, 0.07), (0.15, 0.0)], seg=24), key, smooth=True, tint=t)
    _brim(p, 0.12, 0.15, 0.0, key, up=lambda a: 0.0, tint=t, tilt=0.01)


@hat('turban')
def turban(p, c, tinted, _):
    """A large round wound turban."""
    key, t = _k(tinted, 'hat')
    for i in range(6):
        r = 0.135 - 0.006 * i
        p.add(props.lathe([(0.0, r), (0.034, r)], seg=24), key, T(0, -0.005 * i, 0.01 + 0.03 * i) @ R('X', 7 * (-1) ** i),
              smooth=True, tint=t)
    p.add(props.ellipsoid(0.12, 0.12, 0.08, seg=18), key, T(0, -0.01, 0.17), smooth=True, tint=t)


@hat('turban_aigrette')
def turban_aigrette(p, c, tinted, plume_tinted):
    """King Ottoman: a large round white turban, a jeweled aigrette at its
    front holding a tall plume (part civ-colored)."""
    for i in range(6):
        r = 0.15 - 0.006 * i
        p.add(props.lathe([(0.0, r), (0.04, r)], seg=28), 'turban_white', T(0, -0.005 * i, 0.01 + 0.035 * i) @ R('X', 7 * (-1) ** i),
              smooth=True)
    p.add(props.ellipsoid(0.135, 0.135, 0.09, seg=20), 'turban_white', T(0, -0.01, 0.2), smooth=True)
    p.add(props.ellipsoid(0.03, 0.015, 0.04, seg=12), 'gold', T(0, 0.15, 0.13), smooth=True)          # the jewel's setting
    p.add(props.ellipsoid(0.017, 0.012, 0.022, seg=10), 'jewel', T(0, 0.162, 0.13), smooth=True)
    pk, pt = _k(plume_tinted, 'plume')
    p.add(props.tube([(0, 0.15, 0.16), (0, 0.15, 0.28), (0, 0.11, 0.38), (0, 0.05, 0.42)], [0.022, 0.03, 0.024, 0.006],
                     seg=8, scale_xy=(0.6, 1.0)), pk, smooth=True, tint=pt)


props.DEFAULTS.setdefault('pelt', ('#c87436', 0.9, 0.0, 'rope'))
props.DEFAULTS.setdefault('pelt_dark', ('#2a1810', 0.9, 0.0, 'rope'))


def _rosettes(cell=0.06):
    """A face-key function: a jaguar pelt's dark rosettes."""
    def keys(f):
        c = f.calc_center_median()
        u, v = c.x / cell + 0.37 * c.z / cell, c.z / cell + 0.21 * c.y / cell
        du, dv = u - math.floor(u) - 0.5, v - math.floor(v) - 0.5
        return ('pelt_dark', False) if math.hypot(du, dv) < 0.24 else None   # solid spots: rings break up at this mesh size
    return keys


@hat('jaguar_head')
def jaguar_head(p, c, tinted, _):
    """The Jaguar Warrior's headdress: a jaguar's head worn over his own, its
    spotted hide over the crown, the cat's face over the brow (muzzle,
    nose, a fang row, round ears) and the pelt hanging behind; a spray of
    civ-colored feathers rising from the top."""
    spots = _rosettes()
    p.add(props.ellipsoid(0.135, 0.15, 0.13, seg=22), 'pelt', T(0, -0.01, 0.06), smooth=True, keys=spots)
    p.add(props.ellipsoid(0.085, 0.07, 0.06, seg=16), 'pelt', T(0, 0.12, 0.04), smooth=True, keys=spots)   # the muzzle
    p.add(props.ellipsoid(0.03, 0.02, 0.02, seg=10), 'pelt_dark', T(0, 0.185, 0.06), smooth=True)          # the nose
    for sx in (-1, 1):
        p.add(props.ellipsoid(0.022, 0.012, 0.016, seg=10), 'turban_white', T(sx * 0.045, 0.17, 0.01), smooth=True)  # fangs
        p.add(props.ellipsoid(0.02, 0.012, 0.014, seg=10), 'gold', T(sx * 0.055, 0.14, 0.085), smooth=True)        # eyes
        p.add(props.ellipsoid(0.04, 0.02, 0.04, seg=12), 'pelt', T(sx * 0.1, 0.0, 0.17), smooth=True)              # ears
    # the hide hanging down the back of the neck
    p.add(props.tube([(0, -0.1, 0.02), (0, -0.15, -0.1), (0, -0.14, -0.24)], [0.11, 0.1, 0.07], seg=12,
                     scale_xy=(1.0, 0.35)), 'pelt', smooth=True, keys=spots)
    rnd = props.random.Random(9)
    for i in range(9):   # the civ-colored feather spray on top
        a = math.radians(-60 + 120 * i / 8)
        base = (0.03 * math.sin(a), -0.03, 0.17)
        h = rnd.uniform(0.2, 0.28)
        tip = (0.16 * math.sin(a), -0.08 - 0.05 * math.cos(a), 0.17 + h)
        p.add(props.tube([base, tip], [0.022, 0.03], seg=5, scale_xy=(1.0, 0.35)), 'tint', smooth=True, tint=True)


@hat('cap_plume')
def cap_plume(p, c, tinted, plume_tinted):
    """The Longbowman's cap: a big pale rounded steel cap, a narrow brim,
    a civ-colored plume curling back from its side."""
    p.add(props.lathe([(-0.01, 0.13), (0.04, 0.132), (0.1, 0.11), (0.15, 0.06), (0.17, 0.0)], seg=26), 'helmet',
          smooth=True)
    _brim(p, 0.125, 0.16, -0.005, 'helmet', up=lambda a: 0.01)
    pk, pt = _k(True if plume_tinted is None else plume_tinted, 'plume')
    p.add(props.tube([(0.1, 0.02, 0.1), (0.13, -0.04, 0.22), (0.08, -0.16, 0.27), (0.0, -0.24, 0.2), (-0.02, -0.25, 0.12)],
                     [0.025, 0.04, 0.04, 0.03, 0.01], seg=8, scale_xy=(0.7, 1.0)), 'tint', smooth=True, tint=True)


@hat('attic_helm')
def attic_helm(p, c, tinted, crest_tinted, crest_seated=False):
    """The Libyan (Numidian) Mercenary's Hellenistic Attic helmet: a full rounded
    bronze skull, a brow peak jutting forward over the face, large hinged
    cheek guards shaped to the jaw, a flared neck guard, and a tall
    horsehair crest standing on a stilt from brow to nape."""
    p.add(props.lathe([(-0.04, 0.124), (0.04, 0.126), (0.11, 0.1), (0.16, 0.05), (0.18, 0.0)], seg=28), 'bronze', smooth=True)
    peak = props.ellipsoid(0.13, 0.07, 0.022, seg=20)
    p.add(peak, 'bronze', T(0, 0.075, 0.035) @ R('X', 12), smooth=True)                          # the brow peak
    p.add(props.lathe([(0.0, 0.129), (0.018, 0.129)], seg=28), 'gold', T(0, 0.0, 0.0), smooth=True)   # a gilt rim band
    for sx in (-1, 1):   # big hinged cheek guards down to the jaw, a hinge pin at the top
        guard = props.slab([(0.0, 0.02), (0.09, 0.02), (0.085, -0.09), (0.04, -0.16), (0.0, -0.13), (-0.02, -0.04)], 0.012, 'X')
        p.add(guard, 'bronze', T(sx * 0.122, 0.0, 0.0) @ R('Z', sx * 10), bevel=0.003)
        p.add(props.ellipsoid(0.012, 0.012, 0.012, seg=8), 'gold', T(sx * 0.13, 0.04, 0.015), smooth=True)
    p.add(props.lathe([(-0.1, 0.155), (-0.03, 0.128)], seg=28), 'bronze', T(0, -0.025, 0), smooth=True)   # the neck guard
    vkit_stilt = props.tube([(0, 0.02, 0.17), (0, 0.0, 0.22)], 0.012, seg=6)
    p.add(vkit_stilt, 'bronze', smooth=True)
    ck, ct = _k(crest_tinted, 'crest')
    if crest_seated:
        # (the Libyan's critic round: the crest seated on the crown, no sky under it from the side; realism/all: an
        # option, the Libyan Mercenary's entry asks for it, so the Ancient Cavalry keeps the tall crest of its final)
        path = [(0, 0.1, 0.15), (0, 0.04, 0.205), (0, -0.05, 0.205), (0, -0.11, 0.155), (0, -0.16, 0.065), (0, -0.18, -0.04)]
        p.add(props.tube(path, [0.022, 0.05, 0.055, 0.05, 0.035, 0.012], seg=8, scale_xy=(0.45, 1.0)), ck, smooth=True, tint=ct)
    else:
        path = [(0, 0.15, 0.2), (0, 0.06, 0.26), (0, -0.05, 0.26), (0, -0.15, 0.18), (0, -0.2, 0.05), (0, -0.21, -0.05)]
        p.add(props.tube(path, [0.02, 0.045, 0.05, 0.045, 0.03, 0.01], seg=8, scale_xy=(0.45, 1.0)), ck, smooth=True, tint=ct)


@hat('boeotian')
def boeotian(p, c, tinted, _, brim=1.0, key='bronze', rim='bronze', droop=0.0):
    """A Boeotian cavalry helmet (the realism pass; the Horseman): a bronze
    dome and a broad brim flaring down and out all round, folded into a few
    soft pleats, narrower over the face so the eyes stay lit, a small ridge
    on top. brim: the brim's width."""
    props.DEFAULTS.setdefault('bronze', ('#a8783a', 0.4, 1.0, 'metal'))
    props.DEFAULTS.setdefault('aged_bronze', ('#8a6a3e', 0.62, 0.7, 'metal'))   # a duller, weathered bronze (key='aged_bronze')
    props.DEFAULTS.setdefault('olive_bronze', ('#6e5a34', 0.38, 0.9, 'metal'))   # a darker olive bronze that keeps its highlight
    p.add(props.ellipsoid(0.112, 0.118, 0.13, seg=24), key, T(0, 0, 0.03), smooth=True)
    p.add(props.tube([(0, 0.1, 0.1), (0, 0.0, 0.165), (0, -0.1, 0.11)], 0.012, seg=6), key, smooth=True)   # the ridge
    rings = [[], [], []]
    for i in range(48):
        a = 2 * math.pi * i / 48
        front = max(0.0, math.sin(a)) ** 2                 # +Y: over the face
        w = (0.075 - 0.058 * front) * brim                   # narrower in front (the eyes stay lit)
        pleat = 0.012 * math.cos(a * 6)                      # soft folds round the brim
        r0x, r0y = 0.112 * math.cos(a), 0.118 * math.sin(a)
        rr = 1.0 + (w + pleat) / 0.115
        rings[0].append((r0x * 0.98, r0y * 0.98, 0.03))
        rings[1].append((r0x * (1 + (rr - 1) * 0.55), r0y * (1 + (rr - 1) * 0.55), -0.005 + 0.3 * pleat))
        rings[2].append((r0x * rr, r0y * rr, -0.045 - droop + 0.02 * front + pleat))   # droop: the brim turned further down
    p.add(props.loft(rings, False, False), key, smooth=True)
    p.add(props.tube(rings[2] + [rings[2][0]], 0.006, seg=6), rim, smooth=True)   # a rolled rim (rim: a brighter edge)


@hat('viking_bowl')
def viking_bowl(p, c, tinted, _):
    """King Viking: a round bowl helmet, civ-colored when tinted, with a gold
    brow rim, a gold crest strip over the top and a gold nasal."""
    key, t = ('tint', True) if tinted else ('helmet', False)
    p.add(props.lathe([(-0.005, 0.12), (0.06, 0.116), (0.12, 0.08), (0.155, 0.0)], seg=24), key, smooth=True, tint=t)
    p.add(props.lathe([(-0.01, 0.124), (0.02, 0.124)], seg=24), 'gold', smooth=True)
    arc = [(0, 0.124 * math.cos(a), 0.155 * math.sin(a)) for a in [math.pi * k / 12 for k in range(13)]]
    p.add(props.tube(arc, 0.014, seg=6, scale_xy=(1.0, 0.5)), 'gold', smooth=True)
    p.add(props.box(0.028, 0.016, 0.1), 'gold', T(0, 0.124, -0.045), bevel=0.004)


@hat('eboshi')
def eboshi(p, c, tinted, _):
    """King Japanese: a tall court cap (kanmuri), its crown civ-colored, an
    upright stiff tail rising behind, a dark band."""
    key, t = ('tint', True) if tinted else ('lacquer', False)
    p.add(props.lathe([(0.0, 0.11), (0.06, 0.1), (0.12, 0.085), (0.14, 0.0)], seg=24), key, smooth=True, tint=t)
    p.add(props.box(0.05, 0.03, 0.22), key, T(0, -0.07, 0.2) @ R('X', 12), bevel=0.012, tint=t)   # the upright tail
    p.add(props.lathe([(0.0, 0.113), (0.03, 0.113)], seg=24), 'lacquer', smooth=True)


@hat('round_hat')
def round_hat(p, c, tinted, _):
    """King Portugal: a dark round hat, a low crown and a wide, flat, even
    brim edged in gold."""
    p.add(props.lathe([(0.0, 0.114), (0.07, 0.11), (0.09, 0.0)], seg=28), 'hat', smooth=True)
    _brim(p, 0.11, 0.21, 0.0, 'hat')
    ring = [(0.21 * math.sin(2 * math.pi * k / 40), 0.21 * math.cos(2 * math.pi * k / 40), 0.004) for k in range(41)]
    p.add(props.tube(ring, 0.008, seg=6), 'gold', smooth=True)
    p.add(props.lathe([(0.005, 0.112), (0.025, 0.112)], seg=28), 'gold', smooth=True)


@hat('peaked_cap')
def peaked_cap(p, c, tinted, _):
    """A military peaked (visor) cap: a stiff crown flaring to a wide flat top,
    a civ-colored band, a black leather peak and chinstrap, a gold badge."""
    p.add(props.lathe([(0.0, 0.113), (0.05, 0.118), (0.085, 0.14), (0.1, 0.14), (0.105, 0.0)], seg=28), 'hat', smooth=True)
    p.add(props.lathe([(0.0, 0.116), (0.04, 0.118)], seg=28), 'tint', smooth=True, tint=True)
    visor = props.slab([(-0.1, 0.0), (0.1, 0.0), (0.075, 0.07), (-0.075, 0.07)], 0.008, 'X')
    p.add(visor, 'leather_dark', T(0, 0.1, 0.0) @ R('Z', 90) @ R('X', -15), bevel=0.002)
    p.add(props.lathe([(0.0, 0.119), (0.008, 0.119)], seg=28), 'leather_dark', T(0, 0.005, 0.0), smooth=True)
    p.add(props.ellipsoid(0.02, 0.008, 0.02, seg=10), 'gold', T(0, 0.125, 0.07), smooth=True)


@hat('pickelhaube')
def pickelhaube(p, c, tinted, _):
    """King German: a silver pickelhaube, a tall fluted spike on a cross base,
    a gold eagle front plate, front and rear peaks."""
    p.add(props.lathe([(-0.01, 0.12), (0.06, 0.118), (0.13, 0.08), (0.16, 0.0)], seg=24), 'helmet', smooth=True)
    p.add(props.lathe([(0.0, 0.03), (0.02, 0.022), (0.1, 0.008), (0.13, 0.0)], seg=12), 'gold', T(0, 0, 0.15), smooth=True)
    for a in (0, 90):
        p.add(props.box(0.09, 0.015, 0.015), 'gold', T(0, 0, 0.152) @ R('Z', a), bevel=0.004)
    plate = props.slab([(-0.05, 0.0), (0.05, 0.0), (0.04, 0.07), (0.0, 0.09), (-0.04, 0.07)], 0.006, 'Y')
    p.add(plate, 'gold', T(0, 0.118, 0.02) @ R('X', -12), bevel=0.002)
    for sy, ang in ((1, -18), (-1, 22)):
        peak = props.slab([(-0.09, 0.0), (0.09, 0.0), (0.06, 0.05), (-0.06, 0.05)], 0.006, 'X')
        p.add(peak, 'helmet', T(0, sy * 0.11, -0.01) @ R('Z', 90 * sy) @ R('X', ang), bevel=0.002)


@hat('kabuto')
def kabuto(p, c, tinted, _):
    """The Shogun's kabuto: a civ-colored lacquered bowl, gold kuwagata horns
    rising from a gold front plate, a flared multi-lame neck guard, side
    turnbacks."""
    key, t = ('tint', True) if tinted else ('lacquer', False)
    p.add(props.lathe([(-0.01, 0.122), (0.07, 0.118), (0.13, 0.07), (0.15, 0.0)], seg=24), key, smooth=True, tint=t)
    for i in range(4):   # the shikoro: lames flaring down and out behind
        r0 = 0.125 + 0.02 * i
        ring = []
        for k in range(17):
            a = math.radians(70 + 220 * k / 16)
            ring.append((r0 * math.sin(a), r0 * math.cos(a), -0.02 - 0.035 * i))
        rings = [ring, [(x * 1.12, y * 1.12, z - 0.035) for x, y, z in ring]]
        bm = props.loft(rings, False, False)
        props.bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.008)
        p.add(bm, key if i % 2 == 0 else 'lacquer', smooth=True, tint=t and i % 2 == 0)
    p.add(props.box(0.06, 0.012, 0.05), 'gold', T(0, 0.125, 0.05), bevel=0.003)                       # maedate plate
    for sx in (-1, 1):
        # the kuwagata: broad flat gold blades sweeping up and out in a V
        p.add(props.tube([(sx * 0.02, 0.13, 0.06), (sx * 0.08, 0.145, 0.13), (sx * 0.13, 0.14, 0.2), (sx * 0.14, 0.13, 0.25)],
                         [0.022, 0.03, 0.026, 0.008], seg=6, scale_xy=(1.0, 0.22)), 'gold', smooth=True)
        p.add(props.box(0.012, 0.06, 0.07), key, T(sx * 0.14, 0.07, -0.03) @ R('Z', sx * 30), bevel=0.003, tint=t)  # fukigaeshi


@hat('nemes')
def nemes(p, c, tinted, _):
    """King Egyptian: the nemes, a striped headcloth (gold and civ-colored
    stripes) over the crown, flaring behind the ears into lappets down the
    chest, a gold uraeus at the brow, a tail behind."""
    def stripes(f):
        cz = f.calc_center_median().z
        return ('tint', True) if int(math.floor(cz / 0.025)) % 2 else None
    p.add(props.lathe([(-0.02, 0.122), (0.06, 0.12), (0.12, 0.08), (0.15, 0.0)], seg=24), 'gold', smooth=True, keys=stripes)
    for sx in (-1, 1):   # the lappets: wide flaps falling forward over the chest
        rings = []
        for j in range(8):
            z = 0.0 - 0.06 * j
            w = 0.05 + 0.012 * min(j, 4)
            x = sx * (0.13 + 0.012 * min(j, 3))
            y = 0.02 + 0.012 * j
            rings.append([(x - w * 0.5 * sx * 0 , y - w, z), (x, y, z), (x, y + w, z)])
        bm = props.loft(rings, False, False)
        props.bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.012)
        p.add(bm, 'gold', smooth=True, keys=stripes)
    p.add(props.tube([(0, -0.1, -0.02), (0, -0.13, -0.1), (0, -0.12, -0.2)], [0.04, 0.03, 0.02], seg=8), 'gold',
          smooth=True, keys=stripes)                                                                  # the queue behind
    p.add(props.lathe([(-0.015, 0.124), (0.01, 0.124)], seg=24), 'gold', smooth=True)                 # the brow band
    p.add(props.tube([(0, 0.124, 0.0), (0, 0.135, 0.04), (0, 0.128, 0.07)], [0.012, 0.014, 0.008], seg=6), 'gold',
          smooth=True)                                                                                # the uraeus


@hat('sumer_crown')
def sumer_crown(p, c, tinted, _):
    """King Sumeria: a tall white conical crown on a gold band, a gold knob on top."""
    p.add(props.lathe([(0.0, 0.118), (0.12, 0.1), (0.3, 0.05), (0.36, 0.0)], seg=24), 'turban_white', smooth=True)
    p.add(props.lathe([(-0.005, 0.12), (0.04, 0.12)], seg=24), 'gold', smooth=True)
    p.add(props.ellipsoid(0.025, 0.025, 0.025, seg=12), 'gold', T(0, 0, 0.36), smooth=True)


@hat('war_bonnet')
def war_bonnet(p, c, tinted, _):
    """King Iroquois: a big war bonnet, a fan of white feathers with
    civ-colored tips standing round the head, a civ headband, and a long
    trailer of feathers down the back."""
    p.add(props.lathe([(0.0, 0.115), (0.04, 0.115)], seg=24), 'tint', smooth=True, tint=True)
    props.DEFAULTS.setdefault('feather_band', ('#a8582e', 0.85, 0.0, 'cloth'))   # the red-orange band
    n = 21
    for i in range(n):
        a = math.radians(-125 + 250 * i / (n - 1))   # from +Y (the front) round over the brow and the sides
        base = (0.115 * math.sin(a), 0.115 * math.cos(a), 0.03)
        # swept back and up, the side ones splaying out: one crown over the head
        tip = (0.3 * math.sin(a), 0.06 * math.cos(a) - 0.3, 0.25 - 0.06 * abs(math.sin(a)))   # swept back and out from the brow
        nrm = (math.sin(a), math.cos(a) + 0.3, 0.2)
        b1 = tuple(b + 0.45 * (t - b) for b, t in zip(base, tip))
        b2 = tuple(b + 0.62 * (t - b) for b, t in zip(base, tip))
        b3 = tuple(b + 0.86 * (t - b) for b, t in zip(base, tip))
        props.feather_card(p, base, b1, nrm, 0.05, 'feather', curl=-0.05)
        props.feather_card(p, b1, b2, nrm, 0.055, 'feather_band', curl=-0.05)
        props.feather_card(p, b2, b3, nrm, 0.055, 'feather', curl=-0.05)
        props.feather_card(p, b3, tip, nrm, 0.05, 'tint', tint=True, curl=-0.05)
    p.add(props.tube([(0, -0.2, 0.06), (0, -0.33, -0.25), (0, -0.36, -0.95)], [0.035, 0.04, 0.035], seg=8,
                     scale_xy=(1.0, 0.4)), 'tint', smooth=True, tint=True)   # the trailer's civ band, out clear of the hair
    for j in range(12):   # the trailer: wide white feathers down the back to the waist, red-banded every third
        z = 0.0 - 0.08 * j
        y = -0.3 - 0.06 * min(j, 4) / 4
        for sx in (-1, 1):
            key = 'feather_band' if j % 3 == 1 else 'feather'
            props.feather_card(p, (sx * 0.02, y, z), (sx * 0.2, y - 0.04, z - 0.06), (sx * 0.2, -1.0, 0.0), 0.07, key, curl=-0.03)


props.DEFAULTS.setdefault('pearl', ('#f0ece0', 0.25, 0.0, 'polished'))


@hat('byz_crown')
def byz_crown(p, c, tinted, _, height=0.16):
    """King Byzantines (Theodora): a tall crown, its band civ-colored between gold
    rims, gold ribs over it, gems, a gold cross on top and pearl pendilia hanging
    at the temples."""
    h = height
    p.add(props.lathe([(0.0, 0.118), (h * 0.95, 0.128)], seg=28), 'tint', smooth=True, tint=True)
    for z in (0.0, h * 0.95):
        p.add(props.lathe([(z - 0.012, 0.124 + z * 0.06), (z + 0.016, 0.126 + z * 0.06)], seg=28), 'gold', smooth=True)
    for k in range(8):   # gold ribs with a gem on each
        a = 2 * math.pi * k / 8
        x, y = math.sin(a), math.cos(a)
        p.add(props.box(0.022, 0.01, h * 0.9), 'gold', T(x * 0.127, y * 0.127, h * 0.47) @ R('Z', -math.degrees(a)))
        p.add(props.ellipsoid(0.016, 0.01, 0.02, seg=10), 'jewel' if k % 2 else 'gold_gem',
              T(x * 0.134, y * 0.134, h * 0.5) @ R('Z', -math.degrees(a)), smooth=True)
    p.add(props.ellipsoid(0.12, 0.12, 0.04, seg=24), 'gold', T(0, 0, h * 0.95), smooth=True)   # the domed top
    p.add(props.box(0.014, 0.014, 0.08), 'gold', T(0, 0, h * 0.95 + 0.07))
    p.add(props.box(0.05, 0.014, 0.014), 'gold', T(0, 0, h * 0.95 + 0.08))
    for sx in (-1, 1):   # pearl pendilia at the temples
        for j in range(4):
            p.add(props.ellipsoid(0.013, 0.013, 0.013, seg=8), 'pearl', T(sx * 0.13, 0.03, -0.02 - 0.035 * j), smooth=True)


props.DEFAULTS.setdefault('gold_gem', ('#3a6ac8', 0.15, 0.0, 'polished'))


@hat('beret_feather')
def beret_feather(p, c, tinted, plume_tinted):
    """King Austria: a dark low beret with a white feather laid along its side."""
    p.add(props.ellipsoid(0.14, 0.13, 0.045, seg=22), 'hat', T(0.02, 0, 0.06) @ R('Y', -10), smooth=True)
    p.add(props.lathe([(0.0, 0.112), (0.025, 0.112)], seg=24), 'hat', smooth=True)
    pk, pt = _k(plume_tinted, 'plume')
    p.add(props.tube([(0.1, 0.08, 0.07), (0.15, -0.02, 0.1), (0.14, -0.16, 0.08), (0.1, -0.26, 0.03)],
                     [0.012, 0.035, 0.03, 0.006], seg=8, scale_xy=(0.4, 1.0)), pk, smooth=True, tint=pt)


@hat('conical_helmet')
def conical_helmet(p, c, tinted, _):
    """A Norse conical helmet with a nasal guard (the bowl civ-colored when the entry tints 'helmet')."""
    key, t = ('tint', True) if tinted else ('helmet', False)
    p.add(props.lathe([(-0.005, 0.118), (0.06, 0.11), (0.15, 0.04), (0.2, 0.0)], seg=24), key, smooth=True, tint=t)
    p.add(props.box(0.03, 0.016, 0.11), 'helmet', T(0, 0.12, -0.05), bevel=0.005)   # the nasal bar
    p.add(props.lathe([(-0.005, 0.12), (0.016, 0.12)], seg=24), 'bronze', smooth=True)
    for i in range(14):   # rivets round the brow band
        a = 2 * math.pi * i / 14
        p.add(props.ellipsoid(0.008, 0.008, 0.008, seg=6), 'helmet', T(0.124 * math.sin(a), 0.124 * math.cos(a), 0.005),
              smooth=True)


@hat('morion_big')
def morion_big(p, c, tinted, plume_tinted):
    """The Explorer's morion: a high comb and a wide brim swept up front and
    back, with a big plume."""
    morion(p, c, tinted, plume_tinted)
    pk, pt = _k(plume_tinted, 'plume')
    # a full ostrich plume sweeping back over the crown, ending at the helmet's rear brim
    p.add(props.tube([(0, 0.02, 0.14), (0, -0.08, 0.21), (0, -0.18, 0.2), (0, -0.24, 0.13), (0, -0.25, 0.08)],
                     [0.05, 0.07, 0.06, 0.04, 0.012], seg=10, scale_xy=(0.75, 1.0)), pk, smooth=True, tint=pt)


# ------------------------------------------------------------ realism/misc (new entries only)

@hat('slouch_hat')
def slouch_hat(p, c, tinted, _):
    """A felt slouch hat (Settler Modern Times): a modest brim turned down a little all round, a creased
    crown with a centre dent and two front pinches, a dark band; smaller than the boonie, so the face shows."""
    key, t = _k(tinted, 'khaki')
    rings = []
    for z, r, dent in ((0.0, 0.112, 0.0), (0.03, 0.11, 0.0), (0.075, 0.103, 0.0), (0.11, 0.092, 0.006), (0.128, 0.07, 0.012)):
        ring = []
        for i in range(32):
            a = 2 * math.pi * i / 32
            pinch = dent * 2.5 * max(0.0, math.cos(a)) ** 6 * (abs(math.sin(a)) > 0.2)   # the front pinches
            rr = r - pinch
            ring.append((rr * math.sin(a), rr * math.cos(a) * 1.08, z))
        rings.append(ring + [ring[0]])
    p.add(props.loft(rings, False, False), key, smooth=True, tint=t)
    # the dented top: dipping from the crown's rim to a crease down the middle
    p.add(props.ellipsoid(0.066, 0.075, 0.018, seg=20), key, T(0, 0, 0.118), smooth=True, tint=t)
    p.add(props.box(0.012, 0.12, 0.02), 'leather_dark', T(0, 0, 0.133))                       # the centre crease
    p.add(props.lathe([(0.004, 0.115), (0.03, 0.113)], seg=32, scale_xy=(1.0, 1.08)), 'leather_dark', smooth=True)   # the band
    _brim(p, 0.11, 0.165, 0.004, key, up=lambda a: -0.02 - 0.012 * math.cos(a), tint=t)


# ---- the ranged family's realism pass (new kinds; nothing above changed)

# matte iron for the new helmets (a mirror helmet reflects the dark sky as a black cap)
props.DEFAULTS.setdefault('iron_matte', ('#74726c', 0.58, 0.7, 'metal'))

@hat('petasos')
def petasos(p, c, tinted, _, brim=0.235, droop=0.035):
    """A Greek traveller's petasos (the Scout): a low rounded felt crown and a
    wide brim drooping a little all round; a band (civ-colored when the entry
    tints 'hat') and chin cords."""
    p.add(props.lathe([(-0.005, 0.119), (0.04, 0.117), (0.075, 0.1), (0.098, 0.06), (0.106, 0.0)], seg=28), 'hat',
          smooth=True)
    _brim(p, 0.114, brim, 0.0, 'hat', up=lambda a: -droop)
    _brim(p, brim - 0.012, brim + 0.004, -droop + 0.001, 'leather_dark', up=lambda a: -0.004)   # a stitched edge
    key, t = _k(tinted, 'leather_dark')
    p.add(props.lathe([(0.002, 0.121), (0.03, 0.119)], seg=28), key, smooth=True, tint=t)
    for sx in (-1, 1):   # the chin cords
        p.add(props.tube([(sx * 0.1, 0.0, 0.0), (sx * 0.085, 0.02, -0.1), (0.0, 0.05, -0.19)], 0.005, seg=4),
              'leather_dark', smooth=True)


@hat('capotain')
def capotain(p, c, tinted, plume_tinted, brim=0.2, plume=1.0):
    """A capotain (the Explorer, about 1600): a tall felt crown tapering to a
    flat top, a fairly wide brim dipping at the front; a band (civ-colored
    when the entry tints 'hat') with a buckle, a feather at the side (civ
    when the entry tints 'plume')."""
    p.add(props.lathe([(-0.005, 0.118), (0.06, 0.112), (0.13, 0.1), (0.165, 0.094), (0.175, 0.06), (0.178, 0.0)],
                      seg=28), 'hat', smooth=True)
    _brim(p, 0.114, brim, 0.0, 'hat', up=lambda a: 0.03 * max(0.0, -math.cos(a)) - 0.025 * max(0.0, math.cos(a)))
    key, t = _k(tinted, 'leather_dark')
    p.add(props.lathe([(0.004, 0.121), (0.04, 0.117)], seg=28), key, smooth=True, tint=t)
    p.add(props.box(0.035, 0.012, 0.03), 'brass', T(0.0, 0.122, 0.022), bevel=0.003)   # the buckle
    if plume:
        pk, pt = _k(plume_tinted, 'plume')
        k = plume
        p.add(props.tube([(0.11, 0.02, 0.03), (0.15, -0.01, 0.14 * k), (0.14, -0.08, 0.22 * k), (0.1, -0.17, 0.24 * k)],
                         [0.012, 0.032 * k, 0.028 * k, 0.006], seg=8, scale_xy=(0.45, 1.0)), pk, smooth=True, tint=pt)


@hat('assyrian_helm')
def assyrian_helm(p, c, tinted, _, height=0.27, flaps=True):
    """A Neo-Assyrian pointed helmet (the Bowman): a tall iron cone, a bronze
    brow band, and short ear flaps of the same iron."""
    key, t = ('tint', True) if tinted else ('iron_matte', False)
    h = height
    p.add(props.lathe([(-0.025, 0.124), (0.04, 0.12), (0.4 * h, 0.1), (0.7 * h, 0.055), (0.93 * h, 0.016), (h, 0.0)],
                      seg=28), key, smooth=True, tint=t)
    p.add(props.lathe([(-0.028, 0.127), (0.012, 0.126)], seg=28), 'bronze', smooth=True)
    if flaps:
        for sx in (-1, 1):
            flap = props.slab([(-0.045, 0.0), (0.045, 0.0), (0.04, -0.07), (0.0, -0.085), (-0.035, -0.06)], 0.008, 'X')
            p.add(flap, 'iron_matte', T(sx * 0.122, -0.005, -0.02), bevel=0.002)


@hat('kettle_hat')
def kettle_hat(p, c, tinted, _, brim=0.215, slope=0.05):
    """A kettle hat (chapel de fer; the Longbowman, about 1415): a rounded
    iron skull with a low ridge front to back, and a wide brim sloping down
    all round; a dark lining band showing under the brim."""
    key, t = ('tint', True) if tinted else ('iron_matte', False)
    p.add(props.lathe([(-0.01, 0.124), (0.05, 0.122), (0.1, 0.1), (0.14, 0.06), (0.16, 0.0)], seg=28), key,
          smooth=True, tint=t)
    ridge = props.slab([(-0.12, 0.08), (-0.07, 0.145), (0.0, 0.165), (0.07, 0.145), (0.12, 0.08)], 0.012, 'X')
    p.add(ridge, 'iron_matte', bevel=0.003)
    _brim(p, 0.12, brim, -0.005, key, up=lambda a: -slope, tint=t)
    p.add(props.lathe([(-0.012, 0.126), (0.006, 0.126)], seg=28), 'leather_dark', smooth=True)
# ------------------------------------------------------------ riding seats (the realism pass)

# Per-unit riding poses, applied over units.STANCES['ride'] after the rider is
# built and seated (mounted.seat_rider, for the units in RIDE_POSE_BY_UNIT).
# Directions are in the rider's armature space (the human faces -y, his left
# is +x, +z up); the legs are given for the left side and mirrored. Hand
# targets are offsets in meters from the pelvis, reached with human.reach;
# twists are degrees about a bone's own axis (+: the chest turns to its left).
RIDE_POSES = {
    # an upright military seat: the torso vertical, a long stirrup (the thigh
    # about 45 degrees down round the barrel, the shin hanging under the knee),
    # the heels down; the bridle hand low over the pommel, the right hand at
    # the wrist of a carbine whose butt rests on the right thigh; the
    # shoulders counter-turned (the right one back), the head to the front
    'cavalry': {
        'spine': [('spine_01', (0, 0.1, 1)), ('spine_02', (0, 0.14, 1)), ('spine_03', (0, 0.16, 1))],
        'twist': [('spine_02', -6.0), ('spine_03', -8.0)],
        'clavicle_l': (1, -0.16, 0.04), 'clavicle_r': (-1, 0.16, 0.06),
        'neck': (0, 0.12, 1), 'head': (0, 0.3, 1), 'head_twist': 12.0,
        'thigh': (0.32, -0.66, -0.68), 'calf': (0.03, 0.1, -1), 'foot': (0.18, -1, 0.25),
        'hand_l': (0.05, -0.26, 0.14), 'hand_r': (-0.44, -0.12, 0.62), 'elbow_l': 0.4, 'elbow_r': 0.6,   # (pass 8 = pass 6; pass 4: (-0.46, -0.2, 0.58))
    },
    # a hussar at the charge: seated upright, a little forward over the pommel, a
    # long stirrup, the bridle hand low over the withers, the sword arm raised
    # high with the sabre standing up above the shako ("sabre haut")
    'hussar': {
        'spine': [('spine_01', (0, 0.06, 1)), ('spine_02', (0, 0.1, 1)), ('spine_03', (0, 0.12, 1))],
        'twist': [('spine_02', -5.0), ('spine_03', -8.0)],
        'clavicle_l': (1, -0.14, 0.02), 'clavicle_r': (-1, 0.05, 0.3),
        'neck': (0, 0.12, 1), 'head': (0, 0.3, 1), 'head_twist': 8.0,
        'thigh': (0.3, -0.62, -0.72), 'calf': (0.03, 0.12, -1), 'foot': (0.18, -1, 0.25),
        'hand_l': (0.06, -0.3, 0.16), 'hand_r': (-0.38, -0.34, 0.68), 'elbow_l': 0.4, 'elbow_r': 0.5,
    },
    # a classical horseman: no stirrups, the legs hanging long down the flanks, upright; the reins in the
    # left hand, the spear held underarm at the hip, forward along the horse (critic2: overhand crossed the head)
    'horseman': {
        'spine': [('spine_01', (0, 0.1, 1)), ('spine_02', (0, 0.14, 1)), ('spine_03', (0, 0.15, 1))],
        'twist': [('spine_02', -5.0), ('spine_03', -9.0)],
        'clavicle_l': (1, -0.12, 0.02), 'clavicle_r': (-1, 0.08, 0.3),
        'neck': (0, 0.12, 1), 'head': (0, 0.3, 1), 'head_twist': 6.0,
        'thigh': (0.3, -0.45, -0.85), 'calf': (0.08, 0.15, -1), 'foot': (0.15, -0.4, -1),
        'hand_l': (0.06, -0.3, 0.18), 'hand_r': (-0.36, -0.12, 0.36), 'elbow_l': 0.4, 'elbow_r': 0.5,
    },
    # a Roman auxiliary horseman: no stirrups, the legs long; the shield on the left forearm with the reins,
    # the right hand out at shoulder height, the javelin at the ready, forward and up beside the head (never across it)
    'ancient_cav': {
        'spine': [('spine_01', (0, 0.1, 1)), ('spine_02', (0, 0.15, 1)), ('spine_03', (0, 0.17, 1))],
        'twist': [('spine_02', -8.0), ('spine_03', -12.0)],
        'clavicle_l': (1, -0.12, 0.02), 'clavicle_r': (-1, 0.15, 0.32),
        'neck': (0, 0.12, 1), 'head': (0, 0.3, 1), 'head_twist': 10.0,
        'thigh': (0.3, -0.62, -0.72), 'calf': (0.06, 0.32, -1), 'foot': (0.15, -0.4, -1),   # (C4) knees up, the foot back at the girth
        'hand_l': (0.1, -0.3, 0.22), 'hand_r': (-0.5, 0.3, 0.72), 'elbow_l': 0.5, 'elbow_r': 0.9,   # (C6) the throwing hand above and behind the right shoulder, wide of the helmet
        'shield': {'face': (-0.9, 0.4, 0.0), 'out': 0.2, 'up': 0.02, 'lean': 0.1},
    },
    # a horse archer drawing: no saddle, the legs long round the barrel; the left arm straight out to the
    # front-left with the bow upright, the right hand drawn back to the cheek
    'bow_draw': {
        'spine': [('spine_01', (0, 0.08, 1)), ('spine_02', (0, 0.12, 1)), ('spine_03', (0, 0.14, 1))],
        'twist': [('spine_02', 10.0), ('spine_03', 16.0)],
        'clavicle_l': (1, -0.2, 0.12), 'clavicle_r': (-1, 0.12, 0.12),
        'neck': (0, 0.1, 1), 'head': (0, 0.3, 1), 'head_twist': 18.0,
        'thigh': (0.32, -0.45, -0.82), 'calf': (0.1, 0.18, -1), 'foot': (0.15, -0.4, -1),
        'hand_l': (0.42, -0.42, 0.6), 'hand_r': (-0.08, -0.14, 0.7), 'elbow_l': 0.15, 'elbow_r': 0.95,
    },
    # the Rider: the cavalry seat (stirrups), the dao raised up and out as the Cavalry's critic-approved carry
    'rider': {
        'spine': [('spine_01', (0, 0.1, 1)), ('spine_02', (0, 0.14, 1)), ('spine_03', (0, 0.16, 1))],
        'twist': [('spine_02', -6.0), ('spine_03', -8.0)],
        'clavicle_l': (1, -0.16, 0.04), 'clavicle_r': (-1, 0.16, 0.06),
        'neck': (0, 0.12, 1), 'head': (0, 0.3, 1), 'head_twist': 10.0,
        'thigh': (0.32, -0.66, -0.68), 'calf': (0.03, 0.1, -1), 'foot': (0.18, -1, 0.25),
        'hand_l': (0.05, -0.26, 0.14), 'hand_r': (-0.4, 0.1, 0.86), 'elbow_l': 0.4, 'elbow_r': 0.8,   # (Rider C5) the sword arm high and back
    },
    # (the realism pass) the Sipahi: the Rider's seat and raised sword arm, a kalkan on the left forearm
    'sipahi': {
        'spine': [('spine_01', (0, 0.1, 1)), ('spine_02', (0, 0.14, 1)), ('spine_03', (0, 0.16, 1))],
        'twist': [('spine_02', -6.0), ('spine_03', -8.0)],
        'clavicle_l': (1, -0.16, 0.04), 'clavicle_r': (-1, 0.16, 0.06),
        'neck': (0, 0.12, 1), 'head': (0, 0.3, 1), 'head_twist': 10.0,
        'thigh': (0.34, -0.8, -0.5), 'calf': (0.05, 0.3, -1), 'foot': (0.18, -1, 0.2),
        'hand_l': (0.1, -0.3, 0.22), 'hand_r': (-0.6, -0.05, 0.8), 'elbow_l': 0.5, 'elbow_r': 0.8,   # (Sipahi round 4) the sword hand out and forward of the helmet
        'shield': {'face': (-0.9, 0.4, 0.0), 'out': 0.2, 'up': 0.02, 'lean': 0.1},
    },
    # the backward (Parthian) shot: the torso turned in the saddle to the left rear, the bow arm out behind
    # at shoulder height, the right hand drawn to the cheek; short stirrups
    'bow_back': {
        'spine': [('spine_01', (0, 0.1, 1)), ('spine_02', (0, 0.14, 1)), ('spine_03', (0, 0.16, 1))],
        'twist': [('spine_01', 14.0), ('spine_02', 26.0), ('spine_03', 32.0)],   # (C2) turned further to the rear
        'clavicle_l': (1, 0.2, 0.12), 'clavicle_r': (-1, -0.1, 0.12),
        'neck': (0, 0.12, 1), 'head': (0, 0.3, 1), 'head_twist': 38.0,
        'thigh': (0.38, -0.92, -0.26), 'calf': (0.05, 0.52, -1), 'foot': (0.18, -1, 0.2),   # (C2) short stirrups, the knees up; (C3) higher
        'hand_l': (0.42, 0.5, 0.48), 'hand_r': (0.04, 0.12, 0.5), 'elbow_l': 0.15, 'elbow_r': 0.9,   # (C2) the bow at shoulder height, the arrow level
    },
    # a Cossack's forward seat: short stirrups, the knees up, sitting a little
    # forward over the withers; the bridle hand low, the pika held at the ready
    # in the right hand at chest height, its point forward and up
    'cossack': {
        'spine': [('spine_01', (0, 0.02, 1)), ('spine_02', (0, 0.05, 1)), ('spine_03', (0, 0.08, 1))],
        'twist': [('spine_02', -4.0), ('spine_03', -6.0)],
        'clavicle_l': (1, -0.14, 0.02), 'clavicle_r': (-1, -0.05, 0.08),
        'neck': (0, 0.12, 1), 'head': (0, 0.36, 1), 'head_twist': 6.0,
        'thigh': (0.34, -0.85, -0.42), 'calf': (0.05, 0.3, -1), 'foot': (0.18, -1, 0.2),
        'hand_l': (0.06, -0.34, 0.16), 'hand_r': (-0.58, -0.22, 0.42), 'elbow_l': 0.4, 'elbow_r': 0.8,   # (the hands off the chest: the gazyri show; the lance arm out and bent)
    },
    # a 13th-century knight's long seat: the legs nearly straight and braced
    # forward in long stirrups, the torso upright and a little back against
    # the lance's shock; the lance couched under the right arm across the
    # horse's neck to the left, the shield on the left forearm with the reins
    'knight': {
        'spine': [('spine_01', (0, 0.12, 1)), ('spine_02', (0, 0.16, 1)), ('spine_03', (0, 0.18, 1))],
        'twist': [('spine_03', -5.0)],
        'clavicle_l': (1, -0.12, 0.04), 'clavicle_r': (-1, 0.08, 0.04),
        'neck': (0, 0.1, 1), 'head': (0, 0.28, 1),
        'thigh': (0.4, -0.3, -0.9), 'calf': (0.12, -0.1, -1), 'foot': (0.2, -1, 0.22),
        'hand_l': (0.1, -0.3, 0.24), 'hand_r': (-0.18, -0.2, 0.3), 'elbow_l': 0.5, 'elbow_r': 0.7,
    },
    # (Knight pass 5) the same seat, the lance upright at rest: the right hand low by the thigh
    'knight_rest': {
        'spine': [('spine_01', (0, 0.12, 1)), ('spine_02', (0, 0.16, 1)), ('spine_03', (0, 0.18, 1))],
        'twist': [('spine_03', -5.0)],
        'clavicle_l': (1, -0.12, 0.04), 'clavicle_r': (-1, 0.04, 0.0),
        'neck': (0, 0.1, 1), 'head': (0, 0.28, 1),
        'thigh': (0.4, -0.3, -0.9), 'calf': (0.12, -0.1, -1), 'foot': (0.2, -1, 0.22),
        'hand_l': (0.1, -0.3, 0.24), 'hand_r': (-0.3, -0.16, 0.2), 'elbow_l': 0.5, 'elbow_r': 0.45,
        'shield': {'face': (-0.97, 0.25, 0.0), 'out': 0.24, 'up': 0.04, 'lean': 0.1},   # (critic7: out to the side: it shows from behind)
        # the heater hung upright on the left forearm, its face to the front-left (the unit's frame: +y forward, +x right)
        'shield': {'face': (-0.75, 0.65, 0.0), 'out': 0.2, 'up': -0.06, 'lean': 0.12},
    },
}


def place_shield(arm, obj, pose):
    """Re-hang a forearm shield after ride_pose (it followed the forearm,
    which now reaches for the reins): upright beside the left forearm, its
    face (the shield props' -Y) toward pose['shield']['face'] in the unit's
    frame, `out` m outward and `up` m up from the forearm's middle (the
    rider's meters), leaning back by `lean`; strapped to the forearm."""
    import human as H
    p = (RIDE_POSES[pose] if isinstance(pose, str) else pose).get('shield')
    if not p or obj is None:
        return
    pb = arm.pose.bones['lowerarm_l']
    mw = arm.matrix_world
    k = mw.to_scale()[0]
    mid = (mw @ pb.head).lerp(mw @ pb.tail, 0.5)
    face = Vector(p['face']).normalized()
    at = mid + Vector((-1.0, 0.0, 0.0)) * p.get('out', 0.1) * k + Vector((0, 0, p.get('up', 0.0) * k))
    H.hold(obj, arm, 'lowerarm_l', at, up=(-face.x * p.get('lean', 0.0), -face.y * p.get('lean', 0.0), 1.0),
           forward=tuple(-face), center=True)


def sling_carbine(arm, p):
    """A carbine slung across the back on its sling, from the right hip up to
    the left shoulder, the muzzle up (a dark line seen from behind); p:
    rifle() params and its 'wood'/'steel' colors. Fixed to the chest."""
    import human as H
    obj = props.make('rifle', {'wood': p.get('wood', '#3a2618'), 'steel': p.get('steel', '#2a2c30')},
                     length=p.get('length', 1.15), scale=p.get('scale', 1.0))
    obj.name = 'back_carbine'
    mw = arm.matrix_world
    sp = arm.pose.bones['spine_03']
    at = mw @ (sp.head + Vector((0.0, 0.17, -0.05 - p.get('drop', 0.0))))   # behind the shoulder blades (+y is the back); drop: lower
    up = mw.to_3x3() @ Vector((0.55, 0.05, 1.0))              # toward the left shoulder
    fwd = mw.to_3x3() @ Vector((0.0, 1.0, 0.0))
    H.hold(obj, arm, 'spine_03', at, up=tuple(up.normalized()), forward=tuple(fwd.normalized()), center=True)
    return obj


def gazyri(arm, k, p=None):
    """A cherkeska's gazyri (the realism pass; mounted.GAZYRI): two angled
    rows of cartridge tubes on the chest, one each side of the opening, each
    an ivory tube with a brass cap standing proud of the coat so it reads at
    1x. Built in world space on the posed chest (the unit facing +y, his left
    -x), fixed to spine_03. p: 'n' (tubes a side), 'ivory', 'brass', 'r'
    (tube radius, rider metres), 'depth' (the chest's distance in front of the
    spine bone), 'x0'/'x1' (the rows' inner and outer offsets), 'drop' (the
    rows' height span)."""
    import human as H
    p = dict(p or {})
    H._update()
    mw = arm.matrix_world
    sp = mw @ arm.pose.bones['spine_03'].head
    nk = mw @ arm.pose.bones['neck_01'].head
    props.DEFAULTS.setdefault('gazyri_ivory', (p.get('ivory', '#e6dcc4'), 0.5, 0.0, 'leather'))
    pr = props.Prop('gazyri', {'gazyri_ivory': p.get('ivory', '#e6dcc4'), 'gold': p.get('brass', '#c8a048')})
    n, r = p.get('n', 6), p.get('r', 0.016) * k
    top = nk.z - p.get('below', 0.14) * k   # the first tube's height below the neck
    for side in (-1, 1):
        for i in range(n):
            f = i / max(1, n - 1)
            x = side * (p.get('x0', 0.075) + (p.get('x1', 0.12) - p.get('x0', 0.075)) * f) * k
            z = top - p.get('drop', 0.15) * k * f
            # the chest bows back toward its sides
            y = sp.y + (p.get('depth', 0.13) - 0.25 * abs(x / k) ** 2 * 3.0) * k
            base = Vector((sp.x + x, y, z))
            tilt = Vector((side * 0.25, 0.15, 1.0)).normalized()
            pr.add(props.tube([base, base + tilt * 0.045 * k], r, seg=8), 'gazyri_ivory', smooth=True)
            if p.get('pocket'):   # a dark pocket band behind the tubes: dark gaps between the bright caps at 1x
                pr.add(props.tube([base - tilt * 0.005 * k + Vector((-side * 0.025 * k, -0.004 * k, 0)),
                                   base - tilt * 0.005 * k + Vector((side * 0.025 * k, -0.004 * k, 0))], r * 0.9, seg=6,
                                  scale_xy=(1.0, 0.5)), 'leather_dark', smooth=True)
            pr.add(props.ellipsoid(r * 1.15, r * 1.15, r * 0.8, seg=8), 'gold', T(*(base + tilt * 0.048 * k)), smooth=True)
    obj = pr.finish()
    world = obj.matrix_world.copy()
    obj.parent = arm
    obj.parent_type = 'BONE'
    obj.parent_bone = 'spine_03'
    obj.matrix_world = world
    H._update()
    return obj


def bashlyk(arm, k, p=None):
    """A Cossack's bashlyk (the realism pass; mounted.BASHLYK): the cloth hood
    hanging on the upper back, pointed at the bottom, with a ragged lower edge
    and folds, and its two long tails brought forward over the shoulders and
    hanging down the chest. Civ-coloured (or 'color'). Built in world space on
    the posed rider (the unit facing +y, his left -x), fixed to spine_03.
    p: 'width' (the hood's half width at the top, rider metres), 'length'
    (the hood's length down the back), 'tail' (the tails' length down the chest)."""
    import human as H
    p = dict(p or {})
    H._update()
    mw = arm.matrix_world
    nk = mw @ arm.pose.bones['neck_01'].head
    sp = mw @ arm.pose.bones['spine_03'].head
    tint = not p.get('color')
    props.DEFAULTS.setdefault('bashlyk', (p.get('color') or '#8a3a2a', 0.85, 0.0, 'cloth'))
    pr = props.Prop('bashlyk', {'bashlyk': p.get('color') or '#8a3a2a'}, {'tint': {'grime': 0.35}})
    key = 'tint' if tint else 'bashlyk'
    w, L = p.get('width', 0.13) * k, p.get('length', 0.3) * k
    back = sp.y - 0.15 * k        # the back's surface (the unit faces +y)
    nr, nc = 8, 10
    rings = []
    for j in range(nr + 1):
        t = j / nr
        half = w * (1.0 - t) ** 0.9 + 0.004 * k                       # pointed at the bottom
        row = []
        for i in range(nc + 1):
            u = i / nc * 2 - 1
            x = nk.x + u * half
            fold = 0.012 * k * math.sin(u * math.pi * 2.5) * t          # folds deepen toward the point
            rag = 0.012 * k * math.sin(i * 2.7 + j) * (t > 0.85)
            row.append(Vector((x, back - 0.02 * k * (1 - abs(u)) - fold, nk.z - 0.02 * k - L * t - rag)))
        inner = [q + Vector((0, 0.01 * k, 0)) for q in reversed(row)]
        rings.append(row + inner)
    pr.add(props.loft(rings), key, smooth=True, tint=tint)
    for side in (-1, 1):   # the tails: over the shoulder, down the chest
        sh = mw @ arm.pose.bones['upperarm_l' if side < 0 else 'upperarm_r'].head
        a0 = Vector((nk.x + side * 0.07 * k, back + 0.02 * k, nk.z))
        a1 = Vector((sh.x * 0.6 + nk.x * 0.4, sp.y, sh.z + 0.05 * k))
        a2 = Vector((nk.x + side * 0.11 * k, sp.y + 0.15 * k, sh.z - 0.1 * k))
        a3 = Vector((nk.x + side * 0.12 * k, sp.y + 0.17 * k, sh.z - p.get('tail', 0.32) * k))
        pr.add(props.tube([a0, a1, a2, a3], [0.03 * k, 0.034 * k, 0.03 * k, 0.026 * k], seg=8, scale_xy=(1.0, 0.35)), key,
               smooth=True, tint=tint)
    obj = pr.finish()
    world = obj.matrix_world.copy()
    obj.parent = arm
    obj.parent_type = 'BONE'
    obj.parent_bone = 'spine_03'
    obj.matrix_world = world
    H._update()
    return obj


def coat_skirt(arm, k, p=None):
    """A long coat's skirt for a seated rider (the realism pass; a Cossack's
    cherkeska): two panels hung from the waist, split at the front and over
    the saddle behind, each wrapping round its side from the front of the
    thigh to the back and falling outside the leg over the horse's flank to
    below the knee, with folds deepening toward the hem. Built in world space
    round the posed rider (the unit facing +y, his left -x), fixed to the
    pelvis. p: 'color' (the cloth), 'length' (waist to hem, rider metres),
    'out' (the hem's distance out from the centre, metres: outside the
    horse's barrel), 'fold' (fold depth), 'trim' (a hem band color, 'tint' or None),
    'tint' (the skirt itself in the civ colour: a knight's surcoat)."""
    import human as H
    p = dict(p or {})
    H._update()
    mw = arm.matrix_world
    pel = mw @ arm.pose.bones['pelvis'].head
    sp = mw @ arm.pose.bones['spine_01'].head
    zt = (pel.z + sp.z) / 2 + 0.02 * k           # the waist
    L = p.get('length', 0.72) * k
    out = p.get('out', 0.42)
    r0 = 0.15 * k
    col = p.get('color', '#2e2c2c')
    props.DEFAULTS.setdefault('coat_skirt', (col, 0.88, 0.0, 'cloth'))
    props.DEFAULTS.setdefault('skirt_trim', (col if p.get('trim') in (None, 'tint') else p['trim'], 0.85, 0.0, 'cloth'))
    pr = props.Prop('coat_skirt', {'coat_skirt': col, 'skirt_trim': col if p.get('trim') in (None, 'tint') else p['trim']},
                    {'coat_skirt': {'grime': 0.4}})
    nu, nv = 16, 12
    fold = p.get('fold', 0.03)
    for side in (-1, 1):   # -1: his left (-x), +1: his right
        def at(u, t, off=0.0, side=side):
            # u: 0 at the front edge ('a0' degrees off forward) to 1 at the back edge ('a1')
            a = math.radians(p.get('a0', 25) + (p.get('a1', 175) - p.get('a0', 25)) * u)
            flare = min(1.0, t / 0.32) ** 0.8
            rx = r0 + (out - r0) * flare
            ry = r0 + (0.32 * k + 0.1 - r0) * flare
            f = fold * t ** 1.3 * math.sin(u * math.pi * 6 + side)   # tube folds toward the hem
            x = side * (rx + f + off) * math.sin(a)
            y = (ry + f + off) * math.cos(a)
            hem = L * (1.0 - 0.1 * u + 0.015 * math.sin(u * 7 + side))
            return Vector((pel.x + x, pel.y + y, zt - hem * t))
        rings = []
        for j in range(nv + 1):
            t = j / nv
            outer = [at(i / nu, t) for i in range(nu + 1)]
            inner = [at(i / nu, t, -0.012) for i in range(nu, -1, -1)]
            rings.append(outer + inner)
        pr.add(props.loft(rings), 'tint' if p.get('tint') else 'coat_skirt', smooth=True, tint=bool(p.get('tint')))   # tint: a civ surcoat
        if p.get('trim'):   # a hem band: a color, or 'tint' (the civ colour)
            tt = p['trim'] == 'tint'
            pr.add(props.tube([at(i / nu, 1.0, 0.004) for i in range(nu + 1)], p.get('trim_r', 0.014) * k, seg=6,
                              scale_xy=(1.0, 0.45)), 'tint' if tt else 'skirt_trim', smooth=True, tint=tt)
            if p.get('edges'):   # the same band up the front and back edges (the split): a defined coat outline
                for u_ in (0.0, 1.0):
                    pr.add(props.tube([at(u_, j / nv, 0.004) for j in range(nv + 1)], p.get('trim_r', 0.014) * k * 0.7, seg=6,
                                      scale_xy=(1.0, 0.45)), 'tint' if tt else 'skirt_trim', smooth=True, tint=tt)
    obj = pr.finish()
    world = obj.matrix_world.copy()
    obj.parent = arm
    obj.parent_type = 'BONE'
    obj.parent_bone = 'pelvis'
    obj.matrix_world = world
    H._update()
    return obj


def sling_cape(arm, yaw=-30.0, roll=14.0, shift=(0.0, 0.0, 0.0), sleeve=None):
    """Swing the rider's cape (garments.cape, on spine_03) round the neck
    toward his left shoulder (yaw, degrees: negative swings the back toward
    his left, -x), lifting its left side (roll), so it hangs as a pelisse
    slung from the left shoulder (the realism pass; mounted.PELISSE with
    'cape'). shift: meters in the unit's frame (+x his right, +y forward)."""
    import human as H
    cape = next((o for o in arm.children if o.name.split('.')[0] == 'Cape'), None)
    if cape is None:
        return None
    H._update()
    nk = arm.matrix_world @ arm.pose.bones['neck_01'].head
    m = (Matrix.Translation(nk + Vector(shift)) @ Matrix.Rotation(math.radians(yaw), 4, 'Z')
         @ Matrix.Rotation(math.radians(roll), 4, 'Y') @ Matrix.Translation(-nk))
    cape.matrix_world = m @ cape.matrix_world
    H._update()
    if sleeve:   # the pelisse's empty sleeve hanging from the left shoulder behind the arm, a fur cuff at its end
        # sleeve: {'shade': civ shade 0..1, 'fur': color, 'k': rider scale}
        k = sleeve.get('k', 1.0)
        sh = arm.matrix_world @ arm.pose.bones['upperarm_l'].head
        s0 = sh + Vector((-0.07 * k, -0.06 * k, 0.03 * k))          # outside and behind the shoulder (his left is -x)
        s1 = s0 + Vector((-0.03 * k, -0.07 * k, -0.24 * k))
        s2 = s0 + Vector((-0.02 * k, -0.11 * k, -0.46 * k))
        g = sleeve.get('shade', 0.62)
        pr = props.Prop('pelisse_sleeve', {'tint': '#%02x%02x%02x' % ((round(255 * g),) * 3), 'pelisse_fur': sleeve.get('fur', '#3a2c22')},
                        {'tint': {'grime': 0.4}})
        props.DEFAULTS.setdefault('pelisse_fur', (sleeve.get('fur', '#3a2c22'), 0.95, 0.0, 'rope'))
        pr.add(props.tube([s0, s1, s2], [0.05 * k, 0.046 * k, 0.042 * k], seg=10), 'tint', smooth=True, tint=True)
        d = (s2 - s1).normalized()
        pr.add(props.tube([s2 - d * 0.02 * k, s2 + d * 0.05 * k], 0.056 * k, seg=10), 'pelisse_fur', smooth=True)
        obj = pr.finish()
        world = obj.matrix_world.copy()
        obj.parent = arm
        obj.parent_type = 'BONE'
        obj.parent_bone = 'spine_03'
        obj.matrix_world = world
        H._update()
    return cape


def pelisse(arm, k, p=None):
    """A hussar's pelisse slung over the left shoulder: the fur-trimmed jacket
    hanging from the shoulder down the back and the left side, its empty
    sleeve hanging behind the left arm, a fur edge round the hem and the
    front, gold braid across it (the realism pass; mounted.PELISSE). Built in
    world space round the posed rider (the unit facing +y, his left -x) and
    fixed to the chest. k: the rider's scale; p: 'length', 'fur' (color),
    'braid' (rows of gold braid), 'tint' (the cloth takes the civ color),
    'cloth' (its color when not tinted), 'out' (the side's distance out)."""
    import human as H
    p = dict(p or {})
    mw = arm.matrix_world
    bone = lambda n, end='head': mw @ getattr(arm.pose.bones[n], end)
    sh, nk, sp = bone('upperarm_l'), bone('neck_01'), bone('spine_03')
    tint = p.get('tint', True)
    props.DEFAULTS.setdefault('pelisse_fur', (p.get('fur', '#2c241e'), 0.95, 0.0, 'rope'))
    props.DEFAULTS.setdefault('pelisse_cloth', (p.get('cloth', '#3a4a6a'), 0.85, 0.0, 'cloth'))
    pr = props.Prop('pelisse', {'pelisse_fur': p.get('fur', '#2c241e'), 'gold': p.get('gold', '#d0a840')},
                    {'tint': {'grime': 0.4}, 'pelisse_cloth': {'grime': 0.4}})
    L, out = p.get('length', 0.5) * k, p.get('out', 0.21) * k
    cx, cy = nk.x - 0.02 * k, sp.y + 0.01 * k        # the arc's center: over the spine, a little left
    nu, nv = 14, 10
    th = 0.018 * k

    def at(u, t, r_off=0.0):
        a = math.radians(p.get('a0', -10) + (p.get('a1', 92) - p.get('a0', -10)) * u)   # from right of the spine round to the left side
        r = (p.get('back', 0.12) + (out / k - p.get('back', 0.12)) * math.sin(math.radians(90 * min(1.0, u * 1.15)))) * k
        r += p.get('flare', 0.04) * k * t + p.get('fold', 0.018) * k * math.sin(u * math.pi * 7) * t ** 0.7 + r_off   # flaring and folding toward the hem
        z0 = nk.z - p.get('drop', 0.03) * k            # the top edge at the back: slung diagonally from the left shoulder
        ztop = z0 + (sh.z + 0.06 * k - z0) * min(1.0, u * p.get('rise', 1.4)) ** p.get('rise_pow', 1.0)
        z = ztop - L * (1.0 - 0.18 * u + 0.04 * math.sin(u * math.pi * 3.5)) * t   # an uneven hem
        return Vector((cx - r * math.sin(a), cy - r * math.cos(a), z))
    rings = []
    for j in range(nv + 1):
        t = j / nv
        outer = [at(i / nu, t) for i in range(nu + 1)]
        inner = [at(i / nu, t, -th) for i in range(nu, -1, -1)]
        rings.append(outer + inner)
    key = 'tint' if tint else 'pelisse_cloth'
    pr.add(props.loft(rings), key, smooth=True, tint=tint)
    fr = p.get('fur_r', 0.026) * k
    hem = [at(i / nu, 1.0, 0.004 * k) for i in range(nu + 1)]
    pr.add(props.tube(hem, fr, seg=8), 'pelisse_fur', smooth=True)                       # the fur hem
    front = [at(1.0, j / nv, 0.004 * k) for j in range(nv + 1)]
    pr.add(props.tube(front, fr * 0.9, seg=8), 'pelisse_fur', smooth=True)              # the fur front edge
    back = [at(0.0, j / nv, 0.004 * k) for j in range(nv + 1)]
    pr.add(props.tube(back, fr * 0.8, seg=8), 'pelisse_fur', smooth=True)
    top = [at(i / nu, 0.0, 0.004 * k) for i in range(nu // 3)]
    pr.add(props.tube(top, fr * 0.85, seg=8), 'pelisse_fur', smooth=True)               # the fur collar (at the back)
    for b in range(p.get('braid', 3)):   # rows of gold braid across the back and side
        t = 0.3 + 0.2 * b
        row = [at(0.15 + 0.8 * i / 10, t, 0.006 * k) for i in range(11)]
        pr.add(props.tube(row, 0.009 * k, seg=5), 'gold', smooth=True)
    # the empty sleeve hanging from the shoulder behind the left arm, a fur cuff at its end
    s0 = at(0.82, 0.04, 0.03 * k)
    s1 = s0 + Vector((0.01 * k, -0.06 * k, -0.24 * k))
    s2 = s0 + Vector((0.02 * k, -0.1 * k, -0.46 * k))
    pr.add(props.tube([s0, s1, s2], [0.055 * k, 0.05 * k, 0.045 * k], seg=10), key, smooth=True, tint=tint)
    pr.add(props.tube([s2 + (s2 - s1).normalized() * -0.01 * k, s2 + (s2 - s1).normalized() * 0.04 * k],
                      0.055 * k, seg=10), 'pelisse_fur', smooth=True)
    obj = pr.finish()
    world = obj.matrix_world.copy()
    obj.parent = arm
    obj.parent_type = 'BONE'
    obj.parent_bone = 'spine_03'
    obj.matrix_world = world
    H._update()
    return obj


props.DEFAULTS.setdefault('plate', ('#8c8e94', 0.55, 1.0, 'metal'))   # matte harness steel


def leg_harness(arm, k):
    """A knight's leg harness over mail chausses: a knee cop (poleyn) and a
    greave down the front of each shin, matte steel, fixed to the calf
    bones (k: the rider's scale)."""
    import human as H
    mw = arm.matrix_world
    fwd = (mw.to_3x3() @ Vector((0.0, -1.0, 0.0))).normalized()   # the rider's front
    for s in ('l', 'r'):
        pb = arm.pose.bones['calf_' + s]
        a, b = mw @ pb.head, mw @ pb.tail
        L = (b - a).length / k
        p = props.Prop('leg_harness_' + s, {})
        p.add(props.ellipsoid(0.062, 0.05, 0.07, seg=14), 'plate', T(0, 0.035, 0.0), smooth=True)          # the poleyn
        p.add(props.ellipsoid(0.03, 0.012, 0.045, seg=10), 'plate', T(0.05 if s == 'l' else -0.05, 0.02, 0.0),
              smooth=True)                                                                                   # its side wing
        p.add(props.tube([(0, 0.022, 0.07), (0, 0.026, L * 0.5), (0, 0.018, L - 0.06)], [0.05, 0.048, 0.036], seg=12,
                         scale_xy=(1.0, 0.75)), 'plate', smooth=True)                                        # the greave
        obj = p.finish()
        obj.scale = (k, k, k)
        H.hold(obj, arm, 'calf_' + s, a, up=tuple((b - a).normalized()), forward=tuple(fwd))


def stirrups(root, arm, seat, k):
    """Stirrup irons under the balls of the feet on their leathers from the
    saddle's sides (a part of the unit, after the rider is posed)."""
    import vkit
    mw = arm.matrix_world
    p = vkit.part(root, 'stirrups', {})
    for s, sx in (('l', -1.0), ('r', 1.0)):   # the rider's left is the unit's -x
        pb = arm.pose.bones['foot_' + s]
        ball = (mw @ pb.head).lerp(mw @ pb.tail, 0.55) - Vector((0, 0, 0.045 * k))
        w = 0.06 * k
        iron = [ball + Vector((-w, 0, 0)), ball + Vector((-w, 0, 0.1 * k)), ball + Vector((0, 0, 0.15 * k)),
                ball + Vector((w, 0, 0.1 * k)), ball + Vector((w, 0, 0))]
        p.add(props.tube(iron + [iron[0]], 0.01 * k, seg=6), 'steel', smooth=True)
        top = Vector(seat) + Vector((sx * 0.2, 0.02, -0.06))
        p.add(props.tube([top, ball + Vector((0, 0, 0.15 * k))], 0.012 * k, seg=6, scale_xy=(1.0, 0.4)), 'leather_dark',
              smooth=True)
    return p.finish()


def _twist(arm, bone, degrees):
    """Turn a pose bone about its own long axis (its children follow)."""
    import human as H
    pb = arm.pose.bones[bone]
    pb.matrix = pb.matrix @ Matrix.Rotation(math.radians(degrees), 4, 'Y')
    H._update()


# (the realism pass) the Ansar Warrior: the Horseman's seat and underarm spear, a round shield out on the forearm
RIDE_POSES['ansar'] = dict(RIDE_POSES['horseman'], shield={'face': (-0.9, 0.4, 0.0), 'out': 0.2, 'up': 0.02, 'lean': 0.1})


def ride_pose(arm, pose):
    """Pose a built, seated rider in one of RIDE_POSES (a dict, or its name):
    the torso, its twist, the shoulders, the head, the legs (heels down) and
    the two hands to their targets. Props held in the hands or strapped to
    the forearm follow their bones."""
    import human as H
    p = RIDE_POSES[pose] if isinstance(pose, str) else pose
    H.pose(arm, p.get('spine', []))
    for bone, deg in p.get('twist', []):
        _twist(arm, bone, deg)
    aims = []
    if 'clavicle_l' in p:
        aims.append(('clavicle_l', p['clavicle_l']))
    if 'clavicle_r' in p:
        aims.append(('clavicle_r', p['clavicle_r']))
    if 'neck' in p:
        aims.append(('neck_01', p['neck']))
    if 'head' in p:
        aims.append(('head', p['head']))
    for bone in ('thigh', 'calf', 'foot'):
        if bone in p:
            x, y, z = p[bone]
            aims += [(bone + '_l', (x, y, z)), (bone + '_r', (-x, y, z))]
    H.pose(arm, aims)
    if p.get('head_twist'):
        _twist(arm, 'head', p['head_twist'])
    pelvis = arm.pose.bones['pelvis'].head.copy()
    for side in ('l', 'r'):
        off = p.get('hand_' + side)
        if off:
            H.reach(arm, side, arm.matrix_world @ (pelvis + Vector(off)), elbow_out=p.get('elbow_' + side, 0.35))
    H.fists(arm, sides='lr')
