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
    HEADGEAR[kind](p, c, tinted, 'crest' in entry.get('tint', []) or 'plume' in entry.get('tint', []))
    obj = p.finish()
    s = entry.get('headgear_scale', 1.0)
    sx, sy, sz = (s, s, s) if isinstance(s, (int, float)) else s
    # the head bone points up the head; its tail is near the crown. Hat +Z -> bone +Y
    H.attach(obj, arm, 'head', Matrix.Translation((0, -0.04, 0.012)) @ R('X', -90) @ S(sx, sy, sz))
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
    """A tall conical felt cap with a fur brim."""
    key, t = _k(tinted, 'hat')
    p.add(props.lathe([(0.03, 0.11), (0.28, 0.0)], seg=24), key, smooth=True, tint=t)
    p.add(props.lathe([(0.0, 0.125), (0.05, 0.128)], seg=24), 'fur', smooth=True)


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
    p.add(props.ellipsoid(0.095, 0.095, 0.07, seg=18), key, T(0, 0, 0.13), smooth=True, tint=t)


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
