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
props.DEFAULTS.setdefault('turban_white', ('#ece8de', 0.85, 0.0, 'cloth'))
props.DEFAULTS.setdefault('jewel', ('#b0202a', 0.15, 0.0, 'metal'))
props.DEFAULTS.setdefault('lacquer', ('#1a1816', 0.3, 0.0, 'leather'))
props.DEFAULTS.setdefault('feather', ('#e8e4da', 0.9, 0.0, 'cloth'))
props.DEFAULTS.setdefault('feather_red', ('#a8302a', 0.9, 0.0, 'cloth'))
props.DEFAULTS.setdefault('feather_green', ('#2e7a4a', 0.9, 0.0, 'cloth'))
props.DEFAULTS.setdefault('gold', ('#c8a048', 0.35, 1.0, 'metal'))


def _k(tinted, key):
    return ('tint', True) if tinted else (key, False)


@hat('modern_helmet')
def _modern_helmet(p, c, tinted, crest, band=False, net=False, camo=False):
    props.modern_helmet(p, band=band, net=net, camo=camo)


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
def great_helm(p, c, tinted, _, crest=None):
    """A 13th-century great helm (heaume): a tall flat-sided barrel about 1.4x
    taller than wide, a slightly domed flat top, the eye slits a third of the
    way down either side of a vertical bar (a cross with the brow band),
    breaths below; crest='horns' adds curved horns, 'plume' a civ plume."""
    W, D = 0.115, 0.125               # half width and half depth
    z0, z1 = -0.23, 0.09               # 0.32 tall against 0.23 wide
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
def cavalier(p, c, tinted, plume_tinted, plume=1.0):
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
                         [0.016 * k, 0.026 * k, 0.022 * k, 0.005], seg=6, scale_xy=(1.0, 0.6)), pk, smooth=True, tint=pt)


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
def attic_helm(p, c, tinted, crest_tinted):
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
    path = [(0, 0.15, 0.2), (0, 0.06, 0.26), (0, -0.05, 0.26), (0, -0.15, 0.18), (0, -0.2, 0.05), (0, -0.21, -0.05)]
    p.add(props.tube(path, [0.02, 0.045, 0.05, 0.045, 0.03, 0.01], seg=8, scale_xy=(0.45, 1.0)), ck, smooth=True, tint=ct)


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
    n = 19
    for i in range(n):
        a = math.radians(-160 + 320 * i / (n - 1))
        base = (0.115 * math.sin(a), 0.115 * math.cos(a), 0.03)
        tip = (0.26 * math.sin(a), 0.2 * math.cos(a) - 0.07, 0.3)
        mid = tuple(b + 0.8 * (t - b) for b, t in zip(base, tip))
        p.add(props.tube([base, mid], [0.018, 0.03], seg=5, scale_xy=(1.0, 0.3)), 'feather', smooth=True)
        p.add(props.tube([mid, tip], [0.03, 0.012], seg=5, scale_xy=(1.0, 0.3)), 'tint', smooth=True, tint=True)
    for j in range(10):   # the trailer: pairs of feathers down the back
        z = 0.0 - 0.09 * j
        for sx in (-1, 1):
            pts = [(sx * 0.03, -0.12 - 0.01 * j, z), (sx * 0.1, -0.16 - 0.01 * j, z - 0.03)]
            p.add(props.tube(pts, [0.016, 0.022], seg=5, scale_xy=(1.0, 0.3)), 'feather' if j % 3 else 'tint',
                  smooth=True, tint=j % 3 == 0)


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
