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

from mathutils import Matrix

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


def headgear(entry, arm):
    """Build the entry's headgear (if it has one of HEADGEAR's kinds) and fix
    it to the head bone. Returns the object or None."""
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
    H.attach(obj, arm, 'head', Matrix.Translation((0, -0.015, 0.012)) @ R('X', -90) @ S(sx, sy, sz))
    return obj


# ------------------------------------------------------------ the hats

props.DEFAULTS.setdefault('hat', ('#3a2e26', 0.85, 0.0, 'cloth'))
props.DEFAULTS.setdefault('fur', ('#3a2e26', 0.95, 0.0, 'rope'))
props.DEFAULTS.setdefault('helmet', ('#9a9ca2', 0.45, 1.0, 'metal'))
props.DEFAULTS.setdefault('brass', ('#a88a4a', 0.4, 1.0, 'metal'))
props.DEFAULTS.setdefault('plume', ('#e8e4da', 0.9, 0.0, 'cloth'))
props.DEFAULTS.setdefault('mail', ('#6e7074', 0.5, 1.0, 'metal'))
props.DEFAULTS.setdefault('crest', ('#e8e4da', 0.9, 0.0, 'rope'))


def _k(tinted, key):
    return ('tint', True) if tinted else (key, False)


@hat('modern_helmet')
def _modern_helmet(p, c, tinted, crest, band=False, net=False):
    props.modern_helmet(p, band=band, net=net)


@hat('papakha')
def papakha(p, c, tinted, _):
    """A Cossack's tall fur hat."""
    key, t = _k(tinted, 'fur')
    p.add(props.lathe([(0.0, 0.112), (0.02, 0.118), (0.17, 0.12), (0.19, 0.11), (0.2, 0.0)], seg=24), key,
          smooth=True, tint=t)


@hat('shako')
def shako(p, c, tinted, plume_tinted):
    """A hussar's shako: a flaring cylinder, a peak in front, a plume."""
    key, t = _k(tinted, 'hat')
    p.add(props.lathe([(0.0, 0.105), (0.19, 0.12), (0.2, 0.0)], seg=24), key, smooth=True, tint=t)
    p.add(props.lathe([(0.0, 0.11), (0.02, 0.11)], seg=24), 'leather_dark', smooth=True)
    visor = props.slab([(-0.09, 0.0), (0.09, 0.0), (0.07, 0.05), (-0.07, 0.05)], 0.008, 'X')
    p.add(visor, 'leather_dark', T(0, 0.1, 0.01) @ R('Z', 90) @ R('X', -20), bevel=0.002)
    pk, pt = _k(plume_tinted, 'plume')
    p.add(props.tube([(0, 0.07, 0.19), (0, 0.075, 0.3), (0, 0.06, 0.36)], [0.022, 0.02, 0.005], seg=8), pk,
          smooth=True, tint=pt)


@hat('hat_cone')
def hat_cone(p, c, tinted, _):
    """A tall conical felt cap with a thick white fleece brim (Civ3's Rider)."""
    key, t = _k(tinted, 'hat')
    p.add(props.lathe([(0.03, 0.11), (0.28, 0.0)], seg=24), key, smooth=True, tint=t)
    p.add(props.lathe([(-0.01, 0.12), (0.01, 0.138), (0.05, 0.14), (0.07, 0.122)], seg=24), 'plume', smooth=True)


@hat('fur_hat')
def fur_hat(p, c, tinted, _):
    """A Mongol hat: a thick fur brim and a rounded crown."""
    key, t = _k(tinted, 'hat')
    p.add(props.ellipsoid(0.105, 0.105, 0.14, seg=20), key, T(0, 0, 0.04), smooth=True, tint=t)
    p.add(props.lathe([(0.0, 0.13), (0.03, 0.14), (0.07, 0.13)], seg=24), 'fur', smooth=True)


@hat('dragoon_helm')
def dragoon_helm(p, c, tinted, crest_tinted):
    """A dragoon's brass helmet: a dome, a peak and a horsehair crest."""
    p.add(props.ellipsoid(0.112, 0.118, 0.13, seg=20), 'brass', T(0, 0, 0.02), smooth=True)
    visor = props.slab([(-0.09, 0.0), (0.09, 0.0), (0.06, 0.05), (-0.06, 0.05)], 0.008, 'X')
    p.add(visor, 'leather_dark', T(0, 0.1, 0.0) @ R('Z', 90) @ R('X', -25), bevel=0.002)
    ck, ct = _k(crest_tinted, 'crest')
    path = [(0, 0.1, 0.12), (0, 0.02, 0.17), (0, -0.08, 0.14), (0, -0.16, 0.02), (0, -0.18, -0.12)]
    p.add(props.tube(path, [0.025, 0.03, 0.03, 0.025, 0.01], seg=8, scale_xy=(0.6, 1.0)), ck, smooth=True, tint=ct)


@hat('morion')
def morion(p, c, tinted, _):
    """A conquistador's morion: a steel dome with a comb, the brim upswept fore and aft."""
    p.add(props.ellipsoid(0.11, 0.115, 0.12, seg=20), 'helmet', T(0, 0, 0.03), smooth=True)
    comb = props.slab([(-0.1, 0.06), (-0.06, 0.15), (0.06, 0.15), (0.1, 0.06)], 0.012, 'X')
    p.add(comb, 'helmet', bevel=0.003)
    brim = []
    for i in range(41):
        a = 2 * math.pi * i / 40
        up = 0.07 * abs(math.cos(a)) ** 2
        brim.append([(0.11 * math.cos(a) * 0.95, 0.115 * math.sin(a) * 0.95, 0.02),
                     (0.17 * math.cos(a), 0.2 * math.sin(a), 0.02 + up)])
    rings = [[r[0] for r in brim], [r[1] for r in brim]]
    p.add(props.loft(rings, False, False), 'helmet', smooth=True)


@hat('great_helm')
def great_helm(p, c, tinted, _):
    """A knight's great helm: a flat-topped steel pot over the whole head, a dark eye slit."""
    p.add(props.lathe([(-0.2, 0.118), (0.06, 0.124), (0.08, 0.11), (0.09, 0.0)], seg=24), 'helmet',
          smooth=True)
    p.add(props.box(0.16, 0.02, 0.018), 'leather_dark', T(0, 0.118, -0.04), bevel=0.003)


@hat('spiked_helm')
def spiked_helm(p, c, tinted, plume_tinted):
    """A sipahi's helmet: a tall pointed steel bowl with a spike, a mail aventail and a plume."""
    p.add(props.lathe([(-0.01, 0.115), (0.08, 0.11), (0.17, 0.05), (0.24, 0.008), (0.3, 0.0)], seg=24), 'helmet',
          smooth=True)
    p.add(props.lathe([(-0.16, 0.13), (0.0, 0.118)], seg=24), 'mail', T(0, -0.01, 0), smooth=True)
    pk, pt = _k(plume_tinted, 'plume')
    p.add(props.tube([(0, 0, 0.22), (0, -0.05, 0.3), (0, -0.14, 0.31)], [0.02, 0.016, 0.004], seg=6), pk,
          smooth=True, tint=pt)


@hat('head_wrap')
def head_wrap(p, c, tinted, _):
    """A turban: a wound cloth around a cap."""
    key, t = _k(tinted, 'hat')
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
def feather_bonnet(p, c, tinted, _):
    """A plains-style war bonnet as Civ3's Mounted Warrior wears: a beaded band
    and a crown of upright feathers, alternately civ-colored and white."""
    p.add(props.lathe([(0.0, 0.112), (0.035, 0.112)], seg=24), 'tint', smooth=True, tint=True)
    for i in range(15):
        a = math.radians(-150 + 300 * i / 14)
        base = (0.11 * math.sin(a), 0.11 * math.cos(a), 0.02)
        tip = (0.2 * math.sin(a), 0.16 * math.cos(a) - 0.05, 0.24)
        key, t = (('tint', True) if i % 2 else ('plume', False))
        p.add(props.tube([base, tip], [0.016, 0.006], seg=5, scale_xy=(1.0, 0.35)), key, smooth=True, tint=t)


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
def fan_headdress(p, c, tinted, _):
    """Chasquis Scout: a broad upright fan behind the head, its field in the
    civ color, a white-and-gold rim; a band round the brow."""
    p.add(props.lathe([(0.0, 0.112), (0.035, 0.112)], seg=24), 'gold', smooth=True)
    n = 17
    for i in range(n):
        a = math.radians(-80 + 160 * i / (n - 1))
        base = (0.0, -0.06, 0.06)
        tip = (0.26 * math.sin(a), -0.08, 0.06 + 0.26 * math.cos(a))
        p.add(props.tube([base, tip], [0.02, 0.03], seg=5, scale_xy=(1.0, 0.25)), 'tint', smooth=True, tint=True)
    rim = [(0.29 * math.sin(math.radians(a)), -0.082, 0.06 + 0.29 * math.cos(math.radians(a))) for a in range(-82, 83, 6)]
    p.add(props.tube(rim, 0.014, seg=6), 'feather', smooth=True)
    p.add(props.tube([(x * 1.04, y, z) for x, y, z in rim], 0.008, seg=6), 'gold', smooth=True)


@hat('feather_headdress')
def feather_headdress(p, c, tinted, _, style=None):
    """Tall upright feathers in a crown (Impi, Aztec, Mayan, Iroquois):
    alternately civ-colored and natural, on a headband. style='bushy' (Impi):
    many short feathers, all civ-colored, in two rows splaying into a round
    bushy crown."""
    p.add(props.lathe([(0.0, 0.114), (0.04, 0.114)], seg=24), 'tint', smooth=True, tint=True)
    if style == 'bushy':
        for row, (n, lift, spread, h) in enumerate(((18, 0.02, 0.2, 0.15), (13, 0.06, 0.14, 0.17))):
            for i in range(n):
                a = 2 * math.pi * (i + 0.5 * row) / n
                base = (0.11 * math.sin(a), 0.11 * math.cos(a), lift)
                tip = (spread * math.sin(a), spread * math.cos(a), lift + h)
                mid = tuple(0.5 * (b + t) for b, t in zip(base, tip))
                mid = (mid[0] * 1.08, mid[1] * 1.08, mid[2] + 0.02)   # curving outward
                p.add(props.tube([base, mid, tip], [0.03, 0.05, 0.028], seg=6, scale_xy=(1.0, 0.5)), 'tint',
                      smooth=True, tint=True)
        return
    n = 11
    for i in range(n):
        a = math.radians(-140 + 280 * i / (n - 1))
        base = (0.11 * math.sin(a), 0.11 * math.cos(a) - 0.01, 0.03)
        h = 0.3 - 0.06 * abs(i - n // 2) / (n // 2)
        tip = (0.16 * math.sin(a), 0.14 * math.cos(a) - 0.06, 0.03 + h)
        key, t = (('tint', True) if i % 2 else ('feather', False))
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


@hat('cavalier')
def cavalier(p, c, tinted, plume_tinted):
    """A cavalier's hat (Musketeer): a low crown, a wide soft brim pinned up
    on the left side, and a big plume sweeping back over the pinned side."""
    key, t = _k(tinted, 'felt')
    p.add(props.lathe([(0.0, 0.112), (0.09, 0.108), (0.12, 0.09), (0.13, 0.0)], seg=24), key, smooth=True, tint=t)
    # the brim: wide and drooping a little, but swept up against the crown on the left (-X)
    _brim(p, 0.11, 0.25, 0.0, key, up=lambda a: (0.16 * max(0.0, -math.cos(a)) ** 3) - 0.02 * max(0.0, math.cos(a)),
          tint=t)
    p.add(props.ellipsoid(0.03, 0.03, 0.03, seg=10), 'brass', T(-0.13, 0.0, 0.08), smooth=True)   # the pin
    pk, pt = _k(plume_tinted, 'plume')
    p.add(props.tube([(-0.12, 0.03, 0.1), (-0.16, -0.06, 0.22), (-0.14, -0.22, 0.24), (-0.1, -0.34, 0.14)],
                     [0.04, 0.06, 0.05, 0.01], seg=10, scale_xy=(1.0, 0.7)), pk, smooth=True, tint=pt)


@hat('top_hat')
def top_hat(p, c, tinted, _):
    """A top hat: a tall cylinder, a narrow curled brim and a civ-colored band."""
    p.add(props.lathe([(0.0, 0.11), (0.2, 0.115), (0.21, 0.0)], seg=24), 'felt', smooth=True)
    _brim(p, 0.11, 0.16, 0.0, 'felt', up=lambda a: 0.02 * abs(math.sin(a)))
    p.add(props.lathe([(0.01, 0.113), (0.045, 0.113)], seg=24), 'tint', smooth=True, tint=True)


@hat('kepi')
def kepi(p, c, tinted, _):
    """A kepi: a flat-topped cap sloping forward, with a short leather peak."""
    key, t = _k(tinted, 'hat')
    crown = props.lathe([(0.0, 0.11), (0.1, 0.1), (0.11, 0.0)], seg=24)
    p.add(crown, key, R('X', 8), smooth=True, tint=t)
    visor = props.slab([(-0.08, 0.0), (0.08, 0.0), (0.06, 0.05), (-0.06, 0.05)], 0.008, 'X')
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


@hat('conical_helmet')
def conical_helmet(p, c, tinted, _):
    """A Norse conical helmet with a nasal guard."""
    p.add(props.lathe([(-0.005, 0.118), (0.06, 0.11), (0.15, 0.04), (0.2, 0.0)], seg=24), 'helmet', smooth=True)
    p.add(props.box(0.022, 0.012, 0.09), 'helmet', T(0, 0.118, -0.04), bevel=0.004)
    p.add(props.lathe([(-0.005, 0.12), (0.012, 0.12)], seg=24), 'bronze', smooth=True)


@hat('morion_big')
def morion_big(p, c, tinted, plume_tinted):
    """The Explorer's morion: a high comb and a wide brim swept up front and
    back, with a big plume."""
    morion(p, c, tinted, plume_tinted)
    pk, pt = _k(plume_tinted, 'plume')
    # a big ostrich plume, twice the old one, sweeping low back over the helmet and curling down behind
    p.add(props.tube([(0, 0.0, 0.14), (0, -0.12, 0.22), (0, -0.3, 0.26), (0, -0.48, 0.2), (0, -0.56, 0.06)],
                     [0.05, 0.07, 0.065, 0.045, 0.012], seg=10, scale_xy=(0.7, 1.0)), pk, smooth=True, tint=pt)
