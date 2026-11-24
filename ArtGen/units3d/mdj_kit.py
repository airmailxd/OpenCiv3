"""The MDJ (Medieval Japan) units' own props and hats (Blender side), beside
yoroi.py's armour: the yari, the naginata, the tanegashima matchlock, the
yumi, the ebira quiver, the jingasa, the sohei's katsuo-zukin, a saihai.

Importing it registers the props in props.BUILDERS and the hats in
riders.HEADGEAR (an entry's 'kits': ['mdj_kit'] imports it before the unit is
built, see units.build_footman). Props follow props.py's space: the grip at
the origin, the long axis along +Z with the working end up, a blade's edge
toward +Y. Hats follow riders.py's: origin at the brow, +Y front, +Z up.
Everything is a little thicker than life, so it reads at 1x.
"""
import math

import bmesh

import props as P
import riders
from props import T, R, S, box, ellipsoid, lathe, loft, slab, tube

for _key, _val in (('lacquer', ('#1e1614', 0.35, 0.0, 'leather')), ('lacquer_red', ('#6a2418', 0.35, 0.0, 'leather')),
                   ('mon', ('#ece8de', 0.8, 0.0, 'cloth')), ('bamboo', ('#8a7240', 0.5, 0.0, 'wood')),
                   ('cord', ('#d8d0bc', 0.8, 0.0, 'rope')), ('match', ('#6a5a44', 0.9, 0.0, 'rope')),
                   ('paper', ('#ece6d6', 0.85, 0.0, 'cloth')), ('rattan', ('#c8b488', 0.7, 0.0, 'rope'))):
    P.DEFAULTS.setdefault(_key, _val)


def _blade(p, z0, length, width, curve=0.0, key='steel', thick=0.005):
    """A single-edged blade from z0 up `length`, its edge toward +Y, bowing back
    (-Y) by `curve` at the tip; a diamond section with a ridge line."""
    rings = []
    n = 14
    for i in range(n + 1):
        t = i / n
        z = z0 + length * t
        bow = -curve * t ** 1.6
        hw = width / 2 * (1.0 - 0.15 * t if t < 0.82 else (1 - t) / 0.18 * 0.85 + 0.02)
        back = thick * (1 - 0.5 * t)
        rings.append([(0.0, bow - hw, z), (back, bow + hw * 0.4, z), (0.0, bow + hw, z), (-back, bow + hw * 0.4, z)])
    p.add(loft(rings), key, bevel=0.0006, angle=50.0)


def yari(p, length=3.2, grip=1.1, head=0.36, width=0.05, scale=1.0, collar=True):
    """A su-yari: a straight, narrow, triangular-section blade `head` m long on a
    long black-lacquered shaft (red-brown 'wood' when the entry says so), a
    gilt-rimmed collar (habaki and a lacquered sheath ring) under the blade, a
    binding where the fist holds it and an iron butt cap (ishizuki)."""
    L = length * scale
    z0, z1 = -grip, L - grip
    r = P.SHAFT_R
    p.add(tube([(0, 0, z0 + 0.05), (0, 0, z1 - head - 0.08)], [r + 0.003, r + 0.001], seg=10), 'wood', bevel=0.002,
          smooth=True)
    # the blade: three faces (the ridge to the back), long and narrow, bright
    prof = [(0.0, width * 0.5), (head * 0.15, width * 0.55), (head * 0.7, width * 0.38), (head, 0.0)]
    p.add(lathe(prof, seg=3, scale_xy=(0.55, 1.0)), 'steel', T(0, 0, z1 - head) @ R('Z', 90), bevel=0.001)
    # the tang's collar: a gilt habaki and a lacquered ring (the blade's sheath seat)
    p.add(lathe([(0.0, 0.016), (0.04, 0.019)], seg=10), 'gold', T(0, 0, z1 - head - 0.04), smooth=True)
    if collar:
        p.add(lathe([(0.0, r + 0.006), (0.09, r + 0.006), (0.1, r + 0.003)], seg=12), 'iron',
              T(0, 0, z1 - head - 0.14), smooth=True)
    # the grip binding and the butt cap
    p.add(tube([(0, 0, -0.12), (0, 0, 0.14)], r + 0.0035, seg=12), 'cord', smooth=True)
    P._wrap(p, -0.12, 0.14, r + 0.004, 'leather_dark', turns=12)
    p.add(lathe([(0.0, r + 0.003), (0.08, r + 0.004), (0.1, 0.0)], seg=10), 'iron', T(0, 0, z0 + 0.1) @ R('X', 180),
          smooth=True)


def naginata(p, length=2.15, grip=0.95, blade=0.6, width=0.058, curve=0.07, scale=1.0):
    """A naginata: a long curved blade (edge toward +Y, bowing back) on an oval
    lacquered shaft, a round iron tsuba, a gilt habaki, the top of the shaft
    bound in iron bands (dogane), and an iron butt (ishizuki)."""
    L = length * scale
    z0, z1 = -grip, L - grip
    zb = z1 - blade
    p.add(tube([(0, 0, z0 + 0.05), (0, 0, zb)], [P.SHAFT_R + 0.004, P.SHAFT_R + 0.002], seg=10, scale_xy=(0.85, 1.0)),
          'wood', smooth=True)
    _blade(p, zb + 0.03, blade, width, curve=curve)
    p.add(box(0.016, width * 0.95, 0.035), 'gold', T(0, 0.004, zb + 0.02), bevel=0.002)                   # habaki
    p.add(lathe([(0.0, 0.045), (0.009, 0.045)], seg=18), 'iron', T(0, 0, zb - 0.006), bevel=0.002)       # tsuba
    for z in (zb - 0.06, zb - 0.16):                                                                       # dogane
        p.add(lathe([(0.0, P.SHAFT_R + 0.006), (0.025, P.SHAFT_R + 0.006)], seg=10), 'iron', T(0, 0, z), smooth=True)
    p.add(tube([(0, 0, -0.12), (0, 0, 0.12)], P.SHAFT_R + 0.005, seg=12), 'cord', smooth=True)
    P._wrap(p, -0.12, 0.12, P.SHAFT_R + 0.0055, 'leather_dark', turns=10)
    p.add(lathe([(0.0, P.SHAFT_R + 0.004), (0.1, P.SHAFT_R + 0.004), (0.12, 0.004)], seg=10), 'iron',
          T(0, 0, z0 + 0.12) @ R('X', 180), smooth=True)


def tanegashima(p, length=1.3, scale=1.0, match=True):
    """A tanegashima (matchlock arquebus) held at the wrist (origin), muzzle +Z,
    sights toward +Y: a slim octagonal iron barrel pinned along a full-length
    stock of warm oak to near the muzzle, the short curved butt of the Japanese
    pattern (held to the cheek), a brass serpentine and pan with its cover on
    the right side, brass barrel pins, and a coil of slow match."""
    s = scale
    L = length * s
    # the stock: a long narrow fore-stock, a deeper wrist and the short down-curved butt
    stock = slab([(-0.05, -0.3), (-0.012, -0.3), (0.018, -0.12), (0.016, 0.0), (0.014, L - 0.14), (-0.012, L - 0.14),
                  (-0.016, 0.06), (-0.03, -0.04), (-0.06, -0.22)], 0.04, 'X')
    p.add(stock, 'wood', bevel=0.005)
    # the barrel: octagonal, thick at the breech, to the muzzle, with a front sight
    p.add(tube([(0, 0.024, -0.04), (0, 0.024, L)], [0.016 * s, 0.013 * s], seg=8), 'iron', bevel=0.001)
    p.add(lathe([(0.0, 0.017 * s), (0.03, 0.017 * s)], seg=8), 'iron', T(0, 0.024, L - 0.03))      # the muzzle swell
    p.add(box(0.006, 0.012, 0.02), 'iron', T(0, 0.04, L - 0.06))
    # the lock on the right side (-X): the brass plate, the serpentine and the pan with its cover
    p.add(box(0.006, 0.03, 0.12), 'bronze', T(-0.021, 0.005, -0.01), bevel=0.002)
    p.add(tube([(-0.026, 0.0, -0.04), (-0.03, 0.03, 0.0), (-0.03, 0.035, 0.03)], 0.005, seg=6), 'bronze', smooth=True)
    p.add(box(0.016, 0.02, 0.03), 'bronze', T(-0.026, 0.026, 0.045), bevel=0.003)
    # brass pins through the stock
    for z in (0.25, 0.55, L - 0.35):
        p.add(tube([(-0.024, 0.008, z), (0.024, 0.008, z)], 0.004, seg=6), 'bronze', smooth=True)
    # the ramrod under the barrel
    p.add(tube([(0, -0.02, 0.12), (0, -0.02, L - 0.16)], 0.004, seg=6), 'wood', smooth=True)
    if match:
        ring = [(-0.035 + 0.025 * math.cos(2 * math.pi * k / 12), 0.0 + 0.025 * math.sin(2 * math.pi * k / 12),
                 0.1 + 0.004 * k) for k in range(25)]
        p.add(tube(ring, 0.0045, seg=5), 'match', smooth=True)


def yumi(p, height=2.0, below=0.65, bend=0.1, string=True, scale=1.0):
    """A yumi: the tall asymmetric Japanese bow, held a third of the way up (the
    grip at the origin, `below` m of bow under it), its limbs bending back
    toward the archer (-Y), the tips curling forward; black-lacquered with
    pale rattan bindings, a leather grip, the string."""
    h = height * scale
    lo, hi = -below * scale, h - below * scale
    n = 24
    pts = []
    for i in range(n + 1):
        t = i / n
        z = lo + (hi - lo) * t
        u = (z - lo) / (hi - lo) * 2 - 1          # -1 .. 1 along the bow
        y = -bend * (z / (h / 2)) ** 2             # the limbs back toward the archer from the grip
        tip = max(0.0, abs(u) - 0.85) / 0.15
        y += 0.06 * tip * tip                       # the tips curl forward again
        pts.append((0.0, y, z))
    radii = [0.009 + 0.009 * (1 - abs(-1 + 2 * i / n)) ** 0.6 for i in range(n + 1)]
    p.add(tube(pts, radii, seg=8, scale_xy=(1.0, 0.8)), 'lacquer', smooth=True)
    for i in range(2, n - 1, 3):                    # rattan bindings
        x, y, z = pts[i]
        p.add(lathe([(0.0, radii[i] + 0.002), (0.012, radii[i] + 0.002)], seg=8), 'rattan', T(x, y, z - 0.006),
              smooth=True)
    p.add(tube([(0, pts[n // 3][1], -0.08), (0, pts[n // 3][1], 0.06)], 0.02, seg=10), 'leather', smooth=True)
    if string:
        p.add(tube([pts[0], (0.0, min(pts[0][1], pts[-1][1]) - 0.02, 0.0), pts[-1]], 0.003, seg=5), 'string',
              smooth=True)


def ebira(p, arrows=8, scale=1.0):
    """An ebira: an open box quiver worn at the right hip, behind (origin at the
    top of its frame, the arrows rising +Z): a lacquered frame and a woven
    base box, the arrows standing in a fan with pale fletchings."""
    s = scale
    p.add(box(0.13 * s, 0.07 * s, 0.1 * s), 'lacquer', T(0, 0, -0.42 * s), bevel=0.006)
    for sx in (-1, 1):
        p.add(box(0.012 * s, 0.012 * s, 0.42 * s), 'lacquer', T(sx * 0.055 * s, 0.0, -0.21 * s), bevel=0.002)
    p.add(box(0.13 * s, 0.014 * s, 0.03 * s), 'lacquer', T(0, 0.0, -0.02 * s), bevel=0.003)
    for k in range(arrows):
        x = (-0.05 + 0.1 * k / max(1, arrows - 1)) * s
        a = (k - (arrows - 1) / 2) * 2.5
        p.add(tube([(x, 0.0, -0.4 * s), (x, 0.0, 0.3 * s)], 0.0055, seg=5), 'bamboo', R('Y', a), smooth=True)
        fl = slab([(0.0, 0.0), (0.022, 0.03), (0.024, 0.14), (0.0, 0.13)], 0.002, 'X')
        p.add(fl, 'fletch', R('Y', a) @ T(x, 0.0, 0.14 * s) @ R('Z', 90))


def saihai(p, length=0.45, scale=1.0):
    """A commander's saihai: a short lacquered baton (grip at the origin, +Z)
    with a tassel of paper strips hanging from its head."""
    s = scale
    p.add(tube([(0, 0, -0.08 * s), (0, 0, length * s)], 0.012, seg=8), 'lacquer', smooth=True)
    p.add(ellipsoid(0.018, 0.018, 0.02, seg=10), 'gold', T(0, 0, length * s), smooth=True)
    for k in range(10):
        a = 2 * math.pi * k / 10
        x, y = 0.03 * math.cos(a), 0.03 * math.sin(a)
        p.add(box(0.018, 0.003, 0.22 * s), 'paper', T(x, y, (length - 0.11) * s) @ R('Z', math.degrees(a)) @
              R('X', 8 * math.sin(a)), bevel=0.0005)


def hand_crossbow(p, length=0.75, span=0.8, scale=1.0, bolt=True):
    """A crossbow held at the wrist of its tiller (origin), the bow end +Z,
    the top (the bolt groove) toward +Y: a dark wooden tiller, a laminated
    bow (prod) across its head bound with cord, the string drawn back to the
    bronze nut, a bronze trigger under it, and a bolt in the groove."""
    s = scale
    L = length * s
    # the tiller: a squared stock, deeper at the butt
    tiller = slab([(-0.03, -0.22), (0.025, -0.22), (0.022, 0.0), (0.018, L), (-0.018, L), (-0.022, 0.08), (-0.05, -0.08)],
                  0.045, 'X')
    p.add(tiller, 'wood', bevel=0.004)
    # the bow across the head (along X), bending back toward the butt
    zb = L - 0.04
    pts = [(span / 2 * u, 0.0, zb - 0.09 * u * u) for u in [i / 8 - 1 for i in range(17)]]
    radii = [0.009 + 0.012 * (1 - abs(i / 8 - 1)) ** 0.7 for i in range(17)]
    p.add(tube(pts, radii, seg=8, scale_xy=(1.0, 1.4)), 'prod', smooth=True)
    p.add(box(0.06, 0.05, 0.06), 'cord', T(0, 0.0, zb), bevel=0.006)               # the binding
    # the string from the tips back to the nut
    nut = (0.0, 0.03, 0.06)
    p.add(tube([pts[0], nut, pts[-1]], 0.003, seg=5), 'string', smooth=True)
    p.add(box(0.03, 0.025, 0.03), 'bronze', T(0, 0.028, 0.06), bevel=0.004)        # the nut (lock)
    p.add(tube([(0, -0.015, 0.06), (0, -0.07, 0.0)], 0.007, seg=6), 'bronze', smooth=True)   # the trigger
    if bolt:
        p.add(tube([(0, 0.032, 0.07), (0, 0.032, L + 0.02)], 0.006, seg=6), 'wood', smooth=True)
        p.add(lathe([(0.0, 0.009), (0.05, 0.0)], seg=4), 'steel', T(0, 0.032, L + 0.02), bevel=0.0005)


P.DEFAULTS.setdefault('prod', ('#3a2a1e', 0.5, 0.0, 'wood'))


def gunbai(p, length=0.42, r=0.15, scale=1.0, tint=False):
    """A gunbai, the commander's war fan: a short lacquered handle (grip at the
    origin, +Z) and a broad, flat, rounded leaf of lacquered iron edged in
    gilt, a gilt sun disc on each face (civ with `tint`)."""
    s = scale
    p.add(tube([(0, 0, -0.08 * s), (0, 0, (length - 2 * r) * s)], 0.013, seg=8), 'lacquer', smooth=True)
    zc = (length - r) * s
    leaf = [(r * s * 0.95 * math.sin(2 * math.pi * k / 32), 0.0, zc + r * s * math.cos(2 * math.pi * k / 32)) for k in range(32)]
    bm = bmesh.new()
    bm.faces.new([bm.verts.new(v) for v in leaf])
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.008)
    p.add(bm, 'lacquer', bevel=0.002)
    p.add(tube(leaf + [leaf[0]], 0.006, seg=6), 'gold', smooth=True)
    key = 'tint' if tint else 'gold'
    for y in (-0.006, 0.006):
        p.add(lathe([(0.0, r * s * 0.42), (0.002, r * s * 0.42)], seg=24), key, T(0, y, zc) @ R('X', 90),
              smooth=True, tint=tint)


def kuwa(p, length=1.2, blade=0.3, width=0.2, scale=1.0):
    """A kuwa, the Japanese farmer's broad hoe: an oak haft (grip at the origin, +Z)
    and at its top a broad iron blade set nearly at a right angle, reaching
    toward +Y, its wide edge bright; a wooden head block where the blade is
    socketed."""
    L = length * scale
    p.add(tube([(0, 0, -0.25), (0, 0, L)], [P.SHAFT_R + 0.002, P.SHAFT_R + 0.001], seg=10), 'wood', bevel=0.002,
          smooth=True)
    p.add(box(0.07, 0.09, 0.07), 'wood', T(0, 0.03, L - 0.01), bevel=0.008)
    bl = box(width, blade, 0.012)
    p.add(bl, 'iron', T(0, 0.06 + blade / 2, L - 0.04) @ R('X', -18), bevel=0.003)
    p.add(box(width + 0.004, 0.035, 0.014), 'steel', T(0, 0.06 + blade - 0.005, L - 0.04 - blade * 0.31) @ R('X', -18),
          bevel=0.002)


for _name, _fn in (('hand_crossbow', hand_crossbow), ('gunbai', gunbai), ('kuwa', kuwa)):
    P.BUILDERS.setdefault(_name, _fn)

import tools  # noqa: E402,F401  (registers the work tools: the hoe for the Worker's still)


for _name, _fn in (('hand_crossbow', hand_crossbow), ('yari', yari), ('naginata', naginata), ('tanegashima', tanegashima), ('yumi', yumi),
                   ('ebira', ebira), ('saihai', saihai)):
    P.BUILDERS.setdefault(_name, _fn)


# ------------------------------------------------------------ hats

@riders.hat('jingasa')
def jingasa(p, c, tinted, _, brim=0.25, height=0.13, mon=True):
    """An ashigaru's jingasa: a wide, shallow cone of lacquered iron sitting on
    the head (its inner cushion and chin cords), its slopes catching the light
    all round, the civ colour (or a gilt one) as a mon on the front."""
    p.add(lathe([(0.02, brim), (0.035, brim * 0.98), (0.06 + height * 0.35, brim * 0.6), (0.06 + height, 0.0)],
                seg=36), 'lacquer', smooth=True)
    p.add(lathe([(0.016, brim + 0.004), (0.026, brim + 0.004)], seg=36), 'iron', smooth=True)    # the rolled rim
    p.add(lathe([(0.0, 0.11), (0.05, 0.11)], seg=24), 'cloth_wrap', smooth=True)                # the cushion band
    for sx in (-1, 1):
        p.add(tube([(sx * 0.1, 0.0, 0.01), (sx * 0.08, 0.02, -0.1), (0.0, 0.05, -0.18)], 0.005, seg=4), 'cord',
              smooth=True)
    if mon:
        key, t = ('tint', True) if tinted else ('gold', False)
        # on the front slope, lying along it
        slope = math.degrees(math.atan2(height * 0.65, brim * 0.6))
        p.add(lathe([(0.0, 0.05), (0.004, 0.05)], seg=20), key, T(0, brim * 0.45, 0.08 + height * 0.22) @
              R('X', -(90 - slope)), smooth=True, tint=t)


@riders.hat('zukin')
def zukin(p, c, tinted, _):
    """The sohei's katsuo-zukin: a white cloth wrapped round the head and over
    the lower face, its ends hanging behind; the eyes' slit left open."""
    key = 'zukin'
    P.DEFAULTS.setdefault('zukin', ('#e6e2d8', 0.85, 0.0, 'cloth'))
    # the hood over the crown and down the sides, a little peaked at the front
    p.add(ellipsoid(0.118, 0.128, 0.122, seg=24), key, T(0, -0.008, 0.01), smooth=True)
    # the lower wrap over the mouth and chin, below the eye slit, close round the jaw
    p.add(ellipsoid(0.098, 0.1, 0.058, seg=24), key, T(0, 0.022, -0.108), smooth=True)
    p.add(ellipsoid(0.11, 0.115, 0.05, seg=24), key, T(0, -0.02, -0.12), smooth=True)   # the cloth round the neck
    for sx in (-1, 1):     # the ends hanging behind
        p.add(box(0.06, 0.012, 0.2), key, T(sx * 0.04, -0.13, -0.15) @ R('X', -10) @ R('Y', sx * 6), bevel=0.004)


@riders.hat('kasa')
def kasa(p, c, tinted, _, brim=0.27, height=0.11):
    """A farmer's sugegasa: a wide, shallow cone of sedge (in the civ colour when
    tinted), a dark ring at the crown, a pale chin cord."""
    key, t = ('tint', True) if tinted else ('straw', False)
    P.DEFAULTS.setdefault('straw', ('#a89060', 0.85, 0.0, 'rope'))
    p.add(lathe([(0.0, brim), (0.012, brim * 0.96), (0.03 + height * 0.5, brim * 0.5), (0.03 + height, 0.0)],
                seg=36), key, smooth=True, tint=t)
    for k in range(1, 4):   # the woven rings
        rr = brim * (1 - 0.22 * k)
        zz = 0.012 + (height + 0.018) * (1 - rr / brim)
        p.add(lathe([(zz - 0.002, rr + 0.003), (zz + 0.002, rr + 0.003)], seg=36), 'cord' if not t else 'tint',
              smooth=True, tint=t)
    p.add(lathe([(0.03 + height * 0.8, brim * 0.12), (0.03 + height + 0.006, 0.0)], seg=16), 'lacquer', smooth=True)
    p.add(lathe([(0.0, 0.11), (0.05, 0.11)], seg=24), 'cloth_wrap', smooth=True)
    for sx in (-1, 1):
        p.add(tube([(sx * 0.1, 0.0, 0.01), (sx * 0.08, 0.02, -0.1), (0.0, 0.05, -0.18)], 0.005, seg=4), 'cord',
              smooth=True)


@riders.hat('type90')
def type90(p, c, tinted, _, star=True):
    """The Japanese Army's Type 90 steel helmet under a cloth cover (the civ colour
    when tinted): a high rounded dome, a short flared brim all round, a small
    gilt star on the front, the chin strap."""
    key, t = ('tint', True) if tinted else ('helmet', False)
    p.add(lathe([(-0.025, 0.128), (0.0, 0.124), (0.06, 0.118), (0.12, 0.085), (0.155, 0.03), (0.162, 0.0)], seg=32),
          key, smooth=True, tint=t)
    p.add(lathe([(-0.03, 0.15), (-0.022, 0.13)], seg=32), key, smooth=True, tint=t)          # the flared rim
    for k in range(4):                                                                       # the cover's seams
        a = math.pi / 4 + k * math.pi / 2
        arc = [(0.125 * math.cos(a) * math.cos(e), 0.125 * math.sin(a) * math.cos(e), -0.01 + 0.16 * math.sin(e))
               for e in [i * math.pi / 2 / 6 for i in range(7)]]
        p.add(tube(arc, 0.003, seg=4), 'cord', smooth=True)
    if star:
        p.add(lathe([(0.0, 0.018), (0.004, 0.018)], seg=5), 'gold', T(0, 0.128, 0.05) @ R('X', -80), smooth=False)
    for sx in (-1, 1):
        p.add(tube([(sx * 0.11, 0.0, -0.02), (sx * 0.08, 0.03, -0.12), (0.0, 0.06, -0.17)], 0.005, seg=4),
              'leather_dark', smooth=True)


@riders.hat('hachimaki')
def hachimaki(p, c, tinted, _, knot_tails=True):
    """A hachimaki: a cloth band round the brow (civ colour), tied behind, the
    tails hanging."""
    key, t = ('tint', True) if tinted else ('cloth_wrap', False)
    p.add(lathe([(0.0, 0.118), (0.035, 0.112)], seg=28), key, T(0, -0.005, 0.0), smooth=True, tint=t)
    if knot_tails:
        p.add(ellipsoid(0.03, 0.022, 0.024, seg=10), key, T(0, -0.12, 0.016), smooth=True, tint=t)
        for sx in (-1, 1):
            p.add(box(0.03, 0.008, 0.13), key, T(sx * 0.025, -0.13, -0.05) @ R('Y', sx * 18), bevel=0.003, tint=t)
