"""Ships for the Blender remaster: Galley and Caravel.

    root = ships.build('Galley', colors)

build(unit, colors, **params) returns a root empty holding the ship's mesh
parts. Model space as the old ship archetype (ArtGen/units/models/ships.py,
ship_designs.py): bow +Y, starboard +X, waterline z = 0, 'display meters'
(render_vehicles.py fits the size to the original, so only proportions
matter). The hull is modeled below the waterline too; the shadow-catcher
ground hides it, so the ship floats.

Tint goes only where Civ3's originals put the civ color: the galley's sail
emblem and side bands and its stern awning; the caravel's sail crosses and
pennants.
"""
import math

from mathutils import Vector

import props
import vkit
from props import R, T

SHIP_COLORS = {'wood': '#b89a5c', 'wood_dark': '#5e4628', 'wood_light': '#dcc088', 'deck': '#c0a470',
               'rail': '#5c4628', 'sail': '#f2e2b6', 'gold': '#b08a40', 'cordage': '#5a4a36'}
props.DEFAULTS.setdefault('hull_grime', ('#2e2a1e', 0.75, 0.0, 'wood'))   # weed and grime at the waterline
props.DEFAULTS.setdefault('sail_patch', ('#c4b48c', 0.9, 0.0, 'cloth'))   # patched sailcloth
props.DEFAULTS.setdefault('sail_seam', ('#b8a882', 0.9, 0.0, 'cloth'))    # the seams between sailcloth panels
props.DEFAULTS.setdefault('wood_alt', ('#5a3c22', 0.62, 0.0, 'wood'))     # every other hull plank, a shade darker
props.DEFAULTS.setdefault('oar', ('#4a3420', 0.62, 0.0, 'wood'))

# Weathered ships (stzur: imposing, not toy-like): tarred hulls, salt-stained
# and patched sails, dulled gilding, grimy rigging.
SHIP_WEAR = {'wood': {'grime': 0.4, 'soot': 0.1}, 'wood_alt': {'grime': 0.4, 'soot': 0.1}, 'wood_dark': {'grime': 0.5},
             'wood_light': {'grime': 0.45},
             'deck': {'grime': 0.6}, 'rail': {'grime': 0.6}, 'sail': {'grime': 0.45, 'stain': 0.55},
             'sail_patch': {'grime': 0.5, 'stain': 0.5}, 'gold': {'grime': 0.55, 'rust': 0.15},
             'hull_grime': {'grime': 0.6}, 'cordage': {'grime': 0.5}}


# ------------------------------------------------------------ hull

class Hull:
    """A smooth hull: half-beam b(s) and deck height along the length
    (s = -1 stern .. +1 bow), U-shaped sections down to the keel."""

    def __init__(self, L, B, D, draft, sheer_stern=0.1, sheer_bow=0.06, bow_p=1.5, stern_p=1.6, transom=0.0,
                 rake_bow=0.0, fullness=0.55, tumblehome=0.0, bow_transom=0.0):
        self.bow_transom = bow_transom   # a flat-ended (LST) bow: the beam doesn't close to a point
        self.L, self.B, self.D, self.draft = L, B, D, draft
        self.fullness = fullness       # section shape: lower is boxier, 1 is a round bilge
        self.tumblehome = tumblehome   # how much the sides lean in above the waterline
        self.sheer_stern, self.sheer_bow = sheer_stern, sheer_bow
        self.bow_p, self.stern_p, self.transom, self.rake_bow = bow_p, stern_p, transom, rake_bow

    def y(self, s):
        return s * self.L / 2

    def half_beam(self, s):
        p = self.bow_p if s > 0 else self.stern_p
        b = (max(0.0, 1.0 - abs(s) ** p)) ** 0.5
        if s < -0.5 and self.transom:  # a flat stern: the beam doesn't close to a point
            b = max(b, self.transom * min(1.0, (-s - 0.5) / 0.5) ** 0.5)
        if s > 0.5 and self.bow_transom:
            b = max(b, self.bow_transom * min(1.0, (s - 0.5) / 0.5) ** 0.5)
        return self.B / 2 * b

    def deck_z(self, s):
        return self.D + (self.sheer_bow if s > 0 else self.sheer_stern) * s * s

    def side_x(self, s, z):
        """The starboard hull surface's x at height z in section s."""
        pts = self.section(s, m=28)[:15]   # deck edge down to the keel, starboard
        for a, b in zip(pts, pts[1:]):
            if (a[2] - z) * (b[2] - z) <= 0 and a[2] != b[2]:
                t = (z - a[2]) / (b[2] - a[2])
                return a[0] + (b[0] - a[0]) * t
        return pts[0][0]

    def s_of(self, y):
        return max(-1.0, min(1.0, 2 * y / self.L))

    def deck_half(self, s):
        """Half-width at the deck edge (narrower than the beam with tumblehome)."""
        return self.section(s, m=2)[0][0]

    def section(self, s, m=14):
        """Starboard deck edge, down round the keel, up to the port deck edge."""
        b, zd = self.half_beam(s), self.deck_z(s)
        keel = -self.draft * (1 - 0.6 * abs(s) ** 3)
        # the stem rakes forward gradually over the bow's last part, so the bow sweeps up
        rake = self.rake_bow * max(0.0, (s - 0.6) / 0.4) ** 2
        pts = []
        for k in range(m + 1):
            a = math.pi * k / m
            ca = math.cos(a)
            x = b * math.copysign(abs(ca) ** self.fullness, ca)
            z = zd - (zd - keel) * math.sin(a) ** 0.8
            if self.tumblehome and z > 0.0:
                x *= 1.0 - self.tumblehome * (z / max(zd, 1e-6)) ** 1.5
            pts.append((x, self.y(s) + rake * max(z, 0.0) * 2.0, z))
        return pts


def add_hull(p, hull, stripes=(), n=36, plank=0.045, civ_ranges=()):
    """The hull shell with a flat deck closing it (deck faces get 'deck'),
    colored strakes: stripes = [(z0, z1, key), ...] on the sides, and planks
    `plank` high alternating 'wood' / 'wood_alt' so the sides read as
    planking, not noise."""
    ss = [-1 + 2 * (i / n) ** 1.0 for i in range(n + 1)]
    rings = [hull.section(s, m=32) for s in ss]
    bm = props.loft(rings)

    def keys(f):
        c = f.calc_center_median()
        if f.normal.z > 0.85:
            if any(y0 <= c.y <= y1 for y0, y1 in civ_ranges):   # civ-painted deck at bow and stern
                return ('tint', True)
            return ('deck', False)
        for z0, z1, key in stripes:
            if z0 <= c.z <= z1:
                return (key, False)
        if int(math.floor(c.z / plank)) % 2:
            return ('wood_alt', False)
        return None

    p.add(bm, 'wood', smooth=True, keys=keys)
    # gunwale rails
    for sx in (-1, 1):
        path = [(sx * hull.deck_half(s) * 0.98, hull.y(s), hull.deck_z(s) + 0.012) for s in ss[1:-1]]
        p.add(props.tube(path, 0.016, seg=6), 'rail', smooth=True)


# ------------------------------------------------------------ rig

def _patches(seed, n, w, h):
    """A few rectangular sailcloth patches (center x, center z, half w, half h)
    in sail meters around the sail's center."""
    import random
    rnd = random.Random(seed)
    return [(rnd.uniform(-0.35, 0.35) * w, rnd.uniform(-0.35, 0.35) * h, rnd.uniform(0.04, 0.08) * w,
             rnd.uniform(0.05, 0.1) * h) for _ in range(n)]


def sail(p, y, z_top, w_top, w_bot, h, emblem=None, billow=0.12, x=0.0, nu=40, nv=26, tint_borders=False,
         patches=3, seed=1):
    """A square sail hanging from its yard at z_top, billowing forward (+Y),
    the emblem (a mask in sail meters) in the civ color, with a few patches."""
    pts = _patches(seed, patches, w_top, h)
    rows = []
    for j in range(nv + 1):
        v = j / nv
        row = []
        for i in range(nu + 1):
            u = -1 + 2 * i / nu
            w = w_top + (w_bot - w_top) * v
            bulge = billow * (1 - u * u) * (0.25 + 0.75 * math.sin(math.pi * min(1.0, v * 1.05)))
            row.append((x + u * w / 2, y + bulge, z_top - v * h))
        rows.append(row)
    bm = props.bmesh.new()
    verts = [[bm.verts.new(c) for c in row] for row in rows]
    for j in range(nv):
        for i in range(nu):
            bm.faces.new((verts[j][i], verts[j][i + 1], verts[j + 1][i + 1], verts[j + 1][i]))
    mask = EMBLEMS.get(emblem)

    def keys(f):
        c = f.calc_center_median()
        u = (c.x - x) / (w_top / 2)
        pz = c.z - (z_top - h / 2)
        if tint_borders and abs(u) > 0.7:
            return ('tint', True)
        if mask and mask(c.x - x, pz, h):
            return ('tint', True)
        for px, pzc, hw, hh in pts:
            if abs(c.x - x - px) < hw and abs(pz - pzc) < hh:
                return ('sail_patch', False)
        panel = w_top / 7   # vertical cloth panels with darker seams
        if abs(((c.x - x) / panel) % 1.0 - 0.5) > 0.44:
            return ('sail_seam', False)
        return None

    p.add(bm, 'sail', smooth=True, keys=keys)
    # the yard and the boom (foot spar)
    vkit.rod(p, (x - w_top / 2 - 0.05, y, z_top + 0.01), (x + w_top / 2 + 0.05, y, z_top + 0.01), 0.018, 'wood_dark',
             seg=8)


def lateen(p, y, z_mast_top, yard_len, yard_ang, sail_drop, emblem=None):
    """A lateen sail: a long yard slung diagonally on the mizzen (high at the
    stern), a triangular sail below it."""
    a = math.radians(yard_ang)
    top = (0.0, y - math.cos(a) * yard_len * 0.55, z_mast_top + math.sin(a) * yard_len * 0.55)
    low = (0.0, y + math.cos(a) * yard_len * 0.45, z_mast_top - math.sin(a) * yard_len * 0.45)
    vkit.rod(p, low, top, 0.014, 'wood_dark', seg=8)
    foot = (0.0, y - 0.05, z_mast_top - sail_drop)
    n = 22
    bm = props.bmesh.new()
    grid = []
    for j in range(n + 1):
        v = j / n
        row = []
        for i in range(n - j + 1):
            u = i / n
            # barycentric over (low, top, foot)
            w0, w1 = 1 - u - v, u
            px = [low[k] * w0 + top[k] * w1 + foot[k] * v for k in range(3)]
            px[0] += 0.08 * math.sin(math.pi * u) * (1 - v)   # billow to starboard
            row.append(bm.verts.new(px))
        grid.append(row)
    for j in range(n):
        for i in range(n - j):
            bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i]))
            if i < n - j - 1:
                bm.faces.new((grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
    mask = EMBLEMS.get(emblem)
    cy = (low[1] + top[1] + foot[1]) / 3
    cz = (low[2] + top[2] + foot[2]) / 3

    def keys(f):
        c = f.calc_center_median()
        if mask and mask(-(c.y - cy), c.z - cz, sail_drop * 0.75):
            return ('tint', True)
        return None

    p.add(bm, 'sail', smooth=True, keys=keys)


def pennant(p, top, w, h, phase=0.0):
    """A civ-colored pennant streaming aft from a masthead."""
    n = 10
    bm = props.bmesh.new()
    rows = []
    for j in range(n + 1):
        t = j / n
        hh = h * (1 - t)
        # it waves enough to read even end-on (bow and stern views)
        rows.append([bm.verts.new((0.09 * t * math.sin(5 * t + phase), top[1] - w * t, top[2] - hh * k))
                     for k in (0.0, 1.0)])
    for j in range(n):
        bm.faces.new((rows[j][0], rows[j + 1][0], rows[j + 1][1], rows[j][1]))
    p.add(bm, 'tint', smooth=True, tint=True)


# ------------------------------------------------------------ emblems (x, z in sail meters around its center)

def _pisces(x, z, h):
    """The galley's emblem: two facing arcs ')(' joined by a bar."""
    s = 0.45 * h
    ex, ez = x / s, z / s
    for cx in (-1.6, 1.6):
        d = math.hypot(ex - cx, ez)
        if abs(d - 1.15) < 0.26 and abs(ex) < 0.95:
            return True
    return abs(ez) < 0.15 and abs(ex) < 0.75


def _pattee(x, z, h):
    """A cross pattee: four arms flaring outward."""
    s = 0.42 * h
    ex, ez = abs(x / s), abs(z / s)
    return (ex <= 1.0 and ez <= 0.16 + 0.42 * ex) or (ez <= 1.0 and ex <= 0.16 + 0.42 * ez)


def _disc_cross(x, z, h):
    """The caravel's emblem as in Civ3: a round disc with a cross on it (the
    cross left in sailcloth, the disc in the civ color)."""
    s = 0.5 * h
    ex, ez = x / s, z / s
    if math.hypot(ex, ez) > 1.0:
        return False
    ax, az = abs(ex), abs(ez)
    cross = (az <= 0.05 + 0.12 * ax) or (ax <= 0.05 + 0.12 * az)   # slim flared arms; the disc reads first
    return not cross


def _stripes(x, z, h):
    """The galleon's sails: two bold vertical civ stripes."""
    return 0.14 * h < abs(x) < 0.4 * h and abs(z) < 0.5 * h


def _quartered(x, z, h):
    """The carrack's emblem: a big civ square quartered by a white cross."""
    s = 0.46 * h
    return abs(x) < s and abs(z) < s and abs(x) > 0.07 * h and abs(z) > 0.07 * h


def _roundel(x, z, h):
    """The frigate's emblem: a filled civ roundel with a white cross pattee in it."""
    s = 0.44 * h
    ex, ez = x / s, z / s
    if math.hypot(ex, ez) > 1.0:
        return False
    ax, az = abs(ex), abs(ez)
    return not ((ax <= 0.62 and az <= 0.1 + 0.25 * ax) or (az <= 0.62 and ax <= 0.1 + 0.25 * az))


def _ring_cross(x, z, h):
    """The frigate's emblem: a ring with a cross inside it."""
    s = 0.42 * h
    ex, ez = x / s, z / s
    d = math.hypot(ex, ez)
    return 0.78 < d < 1.0 or (d < 0.8 and (abs(ex) < 0.12 or abs(ez) < 0.12))


def _skull(x, z, h):
    """The privateer's emblem: a skull over crossed bones."""
    s = 0.58 * h   # bold, filling most of the sail
    ex, ez = x / s, z / s
    skull = math.hypot(ex / 0.75, (ez - 0.35) / 0.65) < 1.0 and not (
        math.hypot(ex - 0.28, ez - 0.42) < 0.17 or math.hypot(ex + 0.28, ez - 0.42) < 0.17)   # the eye sockets
    jaw = abs(ex) < 0.4 and -0.15 < ez < 0.1
    bones = any(abs(ez + 0.55 - k * ex) < 0.11 and abs(ex) < 1.05 for k in (0.55, -0.55))
    knobs = any(math.hypot(ex - sx * 1.0, ez + 0.55 - k * sx * 1.0) < 0.16 for sx in (-1, 1) for k in (0.55, -0.55))
    return skull or jaw or bones or knobs


def _lion(x, z, h):
    """The man-o-war's emblem: a heraldic beast rampant (a stout body rearing,
    a maned head, raised forepaws, a curling tail), as a bold silhouette."""
    s = 0.82 * h   # the beast fills the sail, as the original's splash
    ex, ez = x / s, z / s
    body = math.hypot((ex + 0.05) / 0.38, (ez + 0.05) / 0.62) < 1.0
    head = math.hypot((ex - 0.25) / 0.32, (ez - 0.62) / 0.3) < 1.0
    paws = math.hypot((ex - 0.55) / 0.16, (ez - 0.25) / 0.32) < 1.0 or math.hypot((ex - 0.5) / 0.14, (ez + 0.1) / 0.24) < 1.0
    legs = math.hypot((ex + 0.05) / 0.36, (ez + 0.78) / 0.22) < 1.0
    tail = abs(math.hypot(ex + 0.55, ez - 0.1) - 0.3) < 0.07 and ex < -0.5
    return body or head or paws or legs or tail


EMBLEMS = {'pisces': _pisces, 'pattee': _pattee, 'disc_cross': _disc_cross, 'stripes': _stripes,
           'ring_cross': _ring_cross, 'skull': _skull, 'lion': _lion, 'quartered': _quartered, 'roundel': _roundel}


# ------------------------------------------------------------ galley

GALLEY_COLORS = {'wood': '#b08a52', 'wood_alt': '#9a7644', 'wood_dark': '#3a2a18', 'wood_light': '#d4b06a',
                 'deck': '#a08058', 'rail': '#5a4020', 'sail': '#e0d2ac', 'gold': '#c8a048', 'oar': '#7a5a34'}


def galley(colors):
    """A war galley: a dark tarred hull with dulled gilt strakes, a curled
    stern post, a heavy bronze ram, ten oars a side and a weathered, patched
    square sail with the civ emblem."""
    c = dict(SHIP_COLORS, **GALLEY_COLORS)
    c.update(colors)
    root = vkit.root('Galley')
    p = vkit.part(root, 'hull', c, SHIP_WEAR)
    hull = Hull(L=2.2, B=0.48, D=0.22, draft=0.1, sheer_stern=0.2, sheer_bow=0.1, bow_p=1.5, stern_p=1.6,
                fullness=0.8)
    add_hull(p, hull, stripes=[(-0.03, 0.015, 'hull_grime'), (0.015, 0.04, 'gold'),
                               (0.1, 0.125, 'wood_dark'), (0.17, 0.205, 'gold')], plank=0.04)
    # a curled stern post and a short stem
    curl = [(0, -1.06, 0.36), (0, -1.17, 0.5), (0, -1.22, 0.66), (0, -1.19, 0.8), (0, -1.1, 0.87), (0, -1.02, 0.84),
            (0, -1.02, 0.76)]
    p.add(props.tube(curl, [0.05 - 0.004 * i for i in range(len(curl))], seg=10), 'gold', smooth=True)
    p.add(props.tube([(0, 1.06, 0.28), (0, 1.14, 0.38), (0, 1.12, 0.47)], [0.035, 0.026, 0.018], seg=10), 'gold',
          smooth=True)
    # the bronze ram at the waterline: a heavy beak with three cutting fins
    p.add(props.lathe([(0.0, 0.06), (0.12, 0.055), (0.3, 0.04), (0.42, 0.006)], seg=12, scale_xy=(0.75, 1.0)), 'gold',
          T(0, 0.98, 0.04) @ R('X', -90), smooth=True)
    for dz in (-0.03, 0.0, 0.03):
        # a horizontal blade: outline (along, across) built upright, then laid flat
        fin = props.slab([(0.0, -0.065), (0.3, -0.045), (0.42, 0.0), (0.3, 0.045), (0.0, 0.065)], 0.008, 'X')
        p.add(fin, 'gold', T(0, 0.98, 0.04 + dz) @ R('Y', 90), bevel=0.002)
    # the stern canopy (civ color in the original): cloth arched over hoops on stout posts
    arch = []
    for yy in [-0.68 - 0.26 * i / 6 for i in range(7)]:
        z0 = hull.deck_z(hull.s_of(yy)) + 0.13
        arch.append([(0.15 * math.cos(math.pi * k / 10), yy, z0 + 0.07 * math.sin(math.pi * k / 10)) for k in range(11)])
    canopy = props.loft(arch, False, False)
    p.add(canopy, 'tint', smooth=True, tint=True,
          keys=lambda f: ('sail', False) if int((f.calc_center_median().x + 0.15) / 0.06) % 2 else None)
    for sx in (-1, 1):
        for yy in (-0.68, -0.94):
            z0 = hull.deck_z(hull.s_of(yy))
            vkit.rod(p, (sx * 0.15, yy, z0), (sx * 0.15, yy, z0 + 0.14), 0.013, 'wood_dark', seg=6)
        # steering oars
        vkit.rod(p, (sx * 0.17, -0.86, 0.26), (sx * 0.25, -1.12, -0.05), 0.014, 'wood_light', seg=8)
        p.add(props.box(0.012, 0.1, 0.16), 'wood_light', T(sx * 0.25, -1.12, -0.08) @ R('X', 25), bevel=0.003)
    # oars: ten a side, dipping into the water
    for i in range(10):
        y = -0.62 + 1.28 * i / 9
        s = hull.s_of(y)
        for sx in (-1, 1):
            a = (sx * hull.half_beam(s) * 0.95, y, hull.deck_z(s) + 0.01)
            e = (sx * (hull.half_beam(s) + 0.42), y - 0.04, -0.06)
            vkit.rod(p, a, e, 0.011, 'oar', seg=6)
            p.add(props.box(0.014, 0.045, 0.12), 'oar', T(*e) @ R('Y', -sx * 55), bevel=0.002)
    # a civ-colored eye on each side of the bow, dark oar ports, a girdling rope (hypozomata)
    for sx in (-1, 1):
        y = 0.86
        s = hull.s_of(y)
        x = sx * (hull.deck_half(s) + 0.004)
        p.add(props.ellipsoid(0.006, 0.05, 0.03, seg=12), 'tint', T(x, y, 0.13), smooth=True, tint=True)
        p.add(props.ellipsoid(0.007, 0.018, 0.018, seg=10), 'wood_dark', T(x * 1.01, y + 0.005, 0.13), smooth=True)
        for i in range(10):
            yy = -0.62 + 1.28 * i / 9
            ss = hull.s_of(yy)
            p.add(props.box(0.01, 0.035, 0.03), 'wood_dark', T(sx * hull.deck_half(ss) * 0.99, yy, hull.deck_z(ss) - 0.05),
                  bevel=0.004)
    path = [(hull.deck_half(hull.s_of(y)) * 1.01, y, hull.deck_z(hull.s_of(y)) - 0.02) for y in [-0.95 + 0.1 * i for i in range(20)]]
    for sx in (-1, 1):
        p.add(props.tube([(sx * x, y, z) for x, y, z in path], 0.009, seg=5), 'rope', smooth=True)
    # the mast with a gilded finial, the square sail with the emblem and banded edges
    vkit.rod(p, (0, 0.05, 0.2), (0, 0.05, 1.48), 0.036, 'wood_dark', seg=10)
    p.add(props.lathe([(0.0, 0.03), (0.03, 0.07), (0.1, 0.075), (0.14, 0.045), (0.18, 0.0)], seg=16), 'gold',
          T(0, 0.05, 1.47), smooth=True)
    sail(p, 0.08, 1.4, 1.6, 1.6, 0.8, 'pisces', billow=0.14, tint_borders=True)
    vkit.rod(p, (0, 0.05, 1.46), (0, 1.12, 0.47), 0.006, 'cordage', seg=4)      # forestay
    vkit.rod(p, (0, 0.05, 1.46), (0, -1.1, 0.72), 0.006, 'cordage', seg=4)      # backstay
    p.finish()
    return root


# ------------------------------------------------------------ galley (realism pass: a Greek war bireme)

# Real bireme proportions (notes/realism/Galley.md): L 2.2 = ~30 m, so 1 unit = 13.6 m.
BIREME = dict(
    L=2.2, B=0.37, D=0.145, draft=0.07, sheer_stern=0.22, sheer_bow=0.14,
    gallery_h=0.07,            # the oar gallery / fighting-deck screen over the hull
    gallery_y=(-0.78, 0.8),
    oars=11,                   # per bank per side
    oar_r=0.016, oar_reach=0.21, upper_tip_z=0.13, upper_sweep=-0.1, lower_sweep=-0.1, oar_r_low=0.012,   # parallel tiers, separated by height
    mast_y=0.08, mast_top=1.36, mast_r=0.026,
    sail_w=1.375, sail_h=0.88, yard_z=1.33, brace=0.0, billow=0.24,
    shields=0, shield_r=0.045, shield_tilt=62.0,   # faces turned up-and-out to catch the light
    aplustre_top=0.72,
    line_r=0.011,
    braces=False,
    stays=False, stay_r=0.016,   # off: the critics read them as booms (C11)
    gangway=0.05, side_gangway=0.03, well_depth=0.016,
    tar_z=0.035,               # the tarred band at the waterline
    sail_device='trident', sail_bands=0.75,
    civ_band=True,             # a civ stripe along the gallery (the colour in the profiles)
    fit_scale=1.0,
)

BIREME_COLORS = {
    'wood': '#82602e', 'wood_alt': '#7e5c2c', 'tar': '#2e261e', 'seam': '#3a2c1e',        # pitch-tarred planking
    'wood_dark': '#2a2018', 'wood_light': '#9c7c52',
    'gallery': '#7c5a30', 'deck': '#957444', 'deck_alt': '#907042', 'rail': '#3e2e1e',
    'wale': '#2a2018', 'sail': '#e2d3ae', 'sail_seam': '#bcac86', 'sail_patch': '#c6b690', 'sail_mid': '#cbbb95', 'sail_shade': '#bba982', 'brail': '#54442f',
    'oar': '#4a3626', 'oar_low': '#c4a474', 'wood_b': '#8a6430', 'wood_low': '#7e5e38', 'wood_low_alt': '#735432', 'post': '#6e5232', 'post_hl': '#c8a060', 'cordage': '#6e5c44', 'ram': '#a88240', 'shield': '#b8955a', 'well': '#6a5436', 'thwart': '#7c603c', 'shield_rim': '#3e2e1e',
    'eye_white': '#d2c6a8', 'eye_dark': '#1e1a16',
}
props.DEFAULTS.setdefault('gallery', ('#7e6040', 0.65, 0.0, 'wood'))
props.DEFAULTS.setdefault('deck_alt', ('#a08460', 0.7, 0.0, 'wood'))
props.DEFAULTS.setdefault('wale', ('#6a5034', 0.62, 0.0, 'wood'))
props.DEFAULTS.setdefault('ram', ('#c09a52', 0.33, 1.0, 'metal'))
props.DEFAULTS.setdefault('shield', ('#a8884e', 0.55, 0.0, 'leather'))   # a painted bronze-faced shield (dielectric: metal read black)
props.DEFAULTS.setdefault('eye_white', ('#d2c6a8', 0.7, 0.0, None))
props.DEFAULTS.setdefault('eye_dark', ('#1e1a16', 0.6, 0.0, None))
props.DEFAULTS.setdefault('well', ('#2a2219', 0.85, 0.0, 'wood'))
props.DEFAULTS.setdefault('thwart', ('#7a5e3c', 0.7, 0.0, 'wood'))
props.DEFAULTS.setdefault('tar', ('#2e261e', 0.7, 0.0, 'wood'))
props.DEFAULTS.setdefault('seam', ('#3a2c1e', 0.8, 0.0, None))
props.DEFAULTS.setdefault('oar_low', ('#5e4830', 0.62, 0.0, 'wood'))
props.DEFAULTS.setdefault('wood_low', ('#7e5e38', 0.62, 0.0, 'wood'))
props.DEFAULTS.setdefault('wood_low_alt', ('#735432', 0.62, 0.0, 'wood'))
props.DEFAULTS.setdefault('post', ('#6e5232', 0.6, 0.0, 'wood'))
props.DEFAULTS.setdefault('sail_mid', ('#cbbb95', 0.9, 0.0, 'cloth'))
props.DEFAULTS.setdefault('sail_shade', ('#bba982', 0.9, 0.0, 'cloth'))
props.DEFAULTS.setdefault('brail', ('#54442f', 0.85, 0.0, None))
props.DEFAULTS.setdefault('wood_b', ('#94703c', 0.62, 0.0, 'wood'))
props.DEFAULTS.setdefault('post_hl', ('#c8a060', 0.55, 0.0, 'wood'))
props.DEFAULTS.setdefault('oar_port', ('#141210', 0.9, 0.0, None))


def _square_sail(p, w, h, z_top, billow, emblem=None, bands=0.7, brails=6, line_r=0.009, seed=1):
    """A square sail (local: the mast on the z axis, the yard along x at
    z_top, the belly forward +Y), civ bands at its edges (|u| > bands) and an
    emblem; vertical cloth panels, a few patches, and brail lines running down
    its front."""
    pts = _patches(seed, 2, w, h)
    nu, nv = 40, 24

    def surf(u, v):
        bulge = billow * (1 - u * u) * (0.3 + 0.7 * math.sin(math.pi * min(1.0, v * 1.05)))
        return (u * w / 2, 0.03 + bulge, z_top - v * h)

    bm = props.bmesh.new()
    verts = [[bm.verts.new(surf(-1 + 2 * i / nu, j / nv)) for i in range(nu + 1)] for j in range(nv + 1)]
    for j in range(nv):
        for i in range(nu):
            bm.faces.new((verts[j][i], verts[j][i + 1], verts[j + 1][i + 1], verts[j + 1][i]))
    mask = EMBLEMS.get(emblem)

    def keys(f):
        c = f.calc_center_median()
        u = c.x / (w / 2)
        pz = c.z - (z_top - h / 2)
        if abs(u) > bands:
            return ('tint', True)
        if mask and mask(c.x, pz, h):
            return ('tint', True)
        for px, pzc, hw, hh in pts:
            if abs(c.x - px) < hw and abs(pz - pzc) < hh:
                return ('sail_patch', False)
        if abs((c.x / (w / 8)) % 1.0 - 0.5) > 0.42:
            return ('sail_seam', False)
        # the belly's light-to-shadow: lit in the middle, falling off toward the edges and the foot
        v = (z_top - c.z) / h
        k = abs(u) + 0.6 * max(0.0, v - 0.55)
        if k > 0.5:
            return ('sail_shade', False)
        if k > 0.25:
            return ('sail_mid', False)
        return None

    p.add(bm, 'sail', smooth=True, keys=keys)
    # the yard (two spars fished together, thicker at the middle) and the brails down the front
    vkit.rod(p, (-w / 2 - 0.04, 0.03, z_top + 0.012), (0, 0.03, z_top + 0.012), 0.013, 'wood_dark', r1=0.018, seg=8)
    vkit.rod(p, (0, 0.03, z_top + 0.012), (w / 2 + 0.04, 0.03, z_top + 0.012), 0.018, 'wood_dark', r1=0.013, seg=8)
    for sx in (-1, 1):   # dark yard-arm ends
        vkit.rod(p, (sx * (w / 2 - 0.02), 0.03, z_top + 0.012), (sx * (w / 2 + 0.05), 0.03, z_top + 0.012), 0.017, 'tar', seg=8)
    for k in range(brails):
        u = -0.84 + 1.68 * k / (brails - 1)
        path = []
        for j in range(9):
            x, y, z = surf(u, j / 8)
            path.append((x, y + 0.006, z))
        p.add(props.tube(path, line_r * 0.8, seg=5), 'brail', smooth=True)


def _trident(x, z, h):
    """A trident: a shaft with three tines on a crossbar (the galley's device)."""
    s = 0.5 * h   # bold strokes, the whole sail's height
    ex, ez = x / s, z / s
    t = 0.12
    shaft = abs(ex) < t and -0.96 < ez < 0.3
    bar = abs(ez - 0.25) < t and abs(ex) < 0.68
    tines = any(abs(ex - tx) < t and 0.25 < ez < 0.8 for tx in (-0.56, 0.56)) or (abs(ex) < t and ez < 0.9)
    heads = any(abs(ez - 0.82 + 0.8 * abs(ex - tx)) < 0.1 and abs(ex - tx) < 0.19 for tx in (-0.56, 0.0, 0.56))
    return shaft or bar or tines or heads


EMBLEMS['trident'] = _trident


def galley_bireme(colors, **kw):
    """The realism pass's Galley: a Greek war bireme (c. 550-480 BC). A long,
    low, pitch-black hull on a bronze ram, an oar gallery under the fighting
    deck with the marines' shields along its rail, two banks of oars, painted
    civ cheeks round the bow's eye, a curling aplustre over the stern with
    the helmsman's awning, steering oars, and one mast with a braced square
    sail (civ bands and device, brails)."""
    g = dict(BIREME, **kw)
    c = dict(SHIP_COLORS, **BIREME_COLORS)
    c.update(colors)
    root = vkit.root('Galley')
    root['fit_scale'] = g['fit_scale']
    p = vkit.part(root, 'hull', c, SHIP_WEAR)
    hull = Hull(L=g['L'], B=g['B'], D=g['D'], draft=g['draft'], sheer_stern=g['sheer_stern'],
                sheer_bow=g['sheer_bow'], bow_p=1.35, stern_p=1.5, fullness=0.85)
    gy0, gy1 = g['gallery_y']

    # hull shell: pitch planking, a weed line, a lit wale under the gallery, civ cheeks at the bow
    ss = [-1 + 2 * i / 44 for i in range(45)]
    bm = props.loft([hull.section(s, m=32) for s in ss])

    def hkeys(f):
        cc = f.calc_center_median()
        if f.normal.z > 0.85:
            return ('well', False) if gy0 + 0.02 < cc.y < gy1 - 0.02 else ('deck', False)
        if cc.z < 0.012:
            return ('hull_grime', False)
        if cc.z < g['tar_z']:
            return ('tar', False)
        zd = hull.deck_z(hull.s_of(cc.y))
        if cc.y > 0.74 and 0.025 < cc.z < zd - 0.012:
            return ('tint', True)
        low = cc.z < zd - 0.04   # below the wale: the hull's shaded lower strakes
        if int(math.floor(cc.z / 0.022)) % 2:
            return ('wood_low_alt' if low else 'wood_alt', False)
        if not low and int(math.floor((cc.y + 7 * cc.z) / 0.32)) % 3 == 0:   # plank butts: a little tone along the hull
            return ('wood_b', False)
        if cc.y < -0.94:   # the stern's shaded end
            return ('wood_low', False)
        return ('wood_low', False) if low else None

    p.add(bm, 'wood', smooth=True, keys=hkeys)
    # one continuous tarred wale, plank seams below it, a light gunwale cap over it
    for sx in (-1, 1):
        for zc, r, key, sxy in ((None, 0.016, 'wale', 0.45), (0.06, 0.0035, 'seam', 0.4), (0.04, 0.0035, 'seam', 0.4)):
            path = []
            for i in range(1, 60):
                s_ = -0.93 + 1.86 * i / 60
                z = hull.deck_z(s_) - 0.028 if zc is None else zc
                if hull.y(s_) > 0.74 and key == 'seam':
                    continue
                path.append((sx * (hull.side_x(s_, z) + 0.003), hull.y(s_), z))
            p.add(props.tube(path, r, seg=6, scale_xy=(1.0, sxy)), key, smooth=True)
        path = [(sx * (hull.deck_half(s_) + 0.004), hull.y(s_), hull.deck_z(s_) - 0.01) for s_ in [-0.95 + 1.9 * i / 60 for i in range(61)]]
        p.add(props.tube(path, 0.008, seg=6), 'wood_light', smooth=True)

    def deck(y):
        return hull.deck_z(hull.s_of(y))

    # the oar gallery: a screen wall each side standing on the deck edge, a fighting deck over it
    gh = g['gallery_h']
    n = 30
    ys = [gy0 + (gy1 - gy0) * i / n for i in range(n + 1)]
    for sx in (-1, 1):
        rings = []
        for y in ys:
            s = hull.s_of(y)
            xo = sx * (hull.deck_half(s) + 0.012)
            xi = xo - sx * 0.014
            z0 = deck(y) - 0.012
            rings.append([(xo, y, z0), (xo, y, z0 + gh * 0.5), (xo, y, z0 + gh), (xi, y, z0 + gh), (xi, y, z0)])
        p.add(props.loft(rings), 'gallery', bevel=0.003)
        if g['civ_band']:
            p.add(props.tube([(sx * (hull.deck_half(hull.s_of(y)) + 0.017), y, deck(y) - 0.012 + gh * 0.5) for y in ys[1:-1]],
                             0.026, seg=8, scale_xy=(1.0, 0.25)), 'tint', smooth=True, tint=True)
        # the rail cap along its top
        p.add(props.tube([(sx * (hull.deck_half(hull.s_of(y)) + 0.008), y, deck(y) - 0.012 + gh + 0.006) for y in ys],
                         0.012, seg=8, scale_xy=(1.0, 1.2)), 'wood_light', smooth=True)
    # the fighting deck: a central gangway and a side gangway inside each gallery, open rowing
    # wells between them (dark, crossed by the rowers' thwarts)
    gang = g['gangway']
    for x0, x1 in ((-gang, gang), (None, 1), (None, -1)):
        rings = []
        for y in ys:
            s = hull.s_of(y)
            zt = deck(y) - 0.012 + gh - 0.004
            if x0 is None:
                xo = x1 * (hull.deck_half(s) + 0.006)
                a, b = xo, xo - x1 * g['side_gangway']
            else:
                a, b = x1, x0
            rings.append([(a, y, zt), (b, y, zt), (b, y, zt - 0.012), (a, y, zt - 0.012)])
        p.add(props.loft(rings), 'deck',
              keys=lambda f: ('deck_alt', False) if f.normal.z > 0.85 and int(abs(f.calc_center_median().x) / 0.024) % 2 else None)
    for i in range(g['oars'] * 2):
        y = gy0 + 0.1 + (gy1 - gy0 - 0.2) * i / (g['oars'] * 2 - 1)
        s = hull.s_of(y)
        w = hull.deck_half(s) * 2
        p.add(props.box(w, 0.018, 0.012), 'thwart', T(0, y, deck(y) - 0.012 + gh - g['well_depth'] + 0.006), bevel=0.002)
    # the rowing wells' floor, shallow so they read as mid-brown planking at 1x, not black slots
    rings = []
    for y in ys:
        s = hull.s_of(y)
        xh = hull.deck_half(s)
        zt = deck(y) - 0.012 + gh - g['well_depth']
        rings.append([(xh, y, zt), (-xh, y, zt), (-xh, y, zt - 0.008), (xh, y, zt - 0.008)])
    p.add(props.loft(rings), 'well')
    # (no hatches: they read as black dashes on the gangway at 1x)

    # the marines' shields hung along the fighting-deck rail
    sr = g['shield_r']
    for k in range(g['shields']):
        y = gy0 + 0.08 + (gy1 - gy0 - 0.16) * k / (g['shields'] - 1)
        s = hull.s_of(y)
        for sx in (-1, 1):
            x = sx * (hull.deck_half(s) + 0.024)
            z = deck(y) - 0.012 + gh + 0.016
            disc = props.lathe([(0.0, sr), (0.006, sr * 0.95), (0.012, sr * 0.45), (0.014, 0.0)], seg=16)
            p.add(disc, 'shield', T(x, y, z) @ R('Y', sx * g['shield_tilt']), smooth=True)
            p.add(props.lathe([(0.0, sr * 1.08), (0.004, sr * 1.08)], seg=16), 'shield_rim',
                  T(x - sx * 0.002, y, z) @ R('Y', sx * g['shield_tilt']), smooth=True)

    # two banks of oars: the upper over the gallery's sill, the lower through ports in the hull
    no = g['oars']
    for bank in (0, 1):
        for i in range(no):
            y = -0.62 + 1.26 * (i + 0.5 * bank) / (no - 0.5)
            s = hull.s_of(y)
            for sx in (-1, 1):
                if bank == 0:
                    z = deck(y) + 0.02
                    a = (sx * (hull.deck_half(s) + 0.008), y, z)
                    e = (sx * (hull.deck_half(s) + g['oar_reach']), y + g['upper_sweep'], g['upper_tip_z'])   # mid-stroke: blades out of the water, swept forward
                    r = g['oar_r']
                else:
                    z = deck(y) - 0.062
                    xs = hull.side_x(s, z)
                    a = (sx * xs, y, z)
                    e = (sx * (xs + g['oar_reach'] * 0.62), y + 0.04, -0.01)
                    r = g['oar_r_low']
                    e = (sx * (xs + g['oar_reach'] * 0.62), y + g['lower_sweep'], -0.01)   # swept aft, in the water
                    p.add(props.box(0.012, 0.036, 0.03), 'oar_port', T(sx * (xs + 0.002), y, z), bevel=0.003)
                vkit.rod(p, a, e, r, 'oar' if bank == 0 else 'oar_low', seg=6)
                # the blade, its upper half above the water
                d = Vector(e) - Vector(a)
                d.normalize()
                m, _ = vkit.align(Vector(e) - d * 0.045, Vector(e) + d * 0.02)
                p.add(props.box(0.038, r * 0.9, 0.065), 'oar' if bank == 0 else 'oar_low', m, bevel=0.003)

    # the bronze ram at the waterline (three fins), the stem curving forward over it, the eye
    yb = hull.L / 2
    p.add(props.lathe([(0.0, 0.09), (0.1, 0.088), (0.2, 0.07), (0.27, 0.05), (0.28, 0.0)], seg=14, scale_xy=(0.7, 1.0)), 'ram',
          T(0, yb - 0.08, 0.05) @ R('X', -90), smooth=True)
    for dz in (-0.028, 0.0, 0.028):
        fin = props.slab([(0.0, -0.1), (0.24, -0.07), (0.31, -0.035), (0.31, 0.035), (0.24, 0.07), (0.0, 0.1)], 0.014, 'X')   # blunt: the trident-faced head
        p.add(fin, 'ram', T(0, yb - 0.08, 0.05 + dz) @ R('Y', 90), bevel=0.002)
    stem = [(0, yb - 0.02, 0.04), (0, yb + 0.01, 0.11), (0, yb + 0.02, 0.17), (0, yb + 0.06, 0.22), (0, yb + 0.1, 0.23)]
    p.add(props.tube(stem, [0.026, 0.024, 0.021, 0.017, 0.012], seg=10), 'wood_dark', smooth=True)
    p.add(props.tube(stem[:3], [0.031, 0.029, 0.025], seg=10), 'tint', smooth=True, tint=True)   # the painted stem: civ colour head-on
    p.add(props.lathe([(0.0, 0.022), (0.03, 0.018), (0.06, 0.0)], seg=10), 'ram', T(0, yb + 0.0, 0.13) @ R('X', -90),
          smooth=True)   # the proembolion: a small upper ram
    for sx in (-1, 1):
        y = yb - 0.15
        z = 0.085
        x = sx * (hull.side_x(hull.s_of(y), z) + 0.003)
        p.add(props.ellipsoid(0.006, 0.11, 0.07, seg=14), 'eye_white', T(x, y, z), smooth=True)   # a light ring
        p.add(props.ellipsoid(0.009, 0.074, 0.048, seg=14), 'eye_dark', T(x * 1.008, y + 0.004, z), smooth=True)   # the dark eye

    # the aplustre: the sternpost sweeping up and curling forward, with a fan of thinner timbers
    ys_ = -yb
    top = g['aplustre_top']
    curl = [(0, ys_ + 0.04, 0.12), (0, ys_ - 0.03, 0.22), (0, ys_ - 0.06, 0.33), (0, ys_ - 0.04, top - 0.03),
            (0, ys_ + 0.02, top), (0, ys_ + 0.08, top - 0.02), (0, ys_ + 0.09, top - 0.07)]
    p.add(props.tube(curl, [0.05, 0.045, 0.04, 0.034, 0.028, 0.022, 0.018], seg=12, scale_xy=(0.85, 1.3)), 'post', smooth=True)
    p.add(props.ellipsoid(0.028, 0.028, 0.028, seg=12), 'gold', T(*curl[-1]), smooth=True)   # its gilt finial
    # a warm highlight along the curl's outer edge (away from the curl's centre)
    cy, cz = ys_ + 0.03, top - 0.12
    hl = []
    for (x0, y0, z0), r0 in zip(curl[1:], [0.045, 0.04, 0.034, 0.028, 0.022, 0.018]):
        dy, dz = y0 - cy, z0 - cz
        n = math.hypot(dy, dz) or 1.0
        hl.append((0.0, y0 + dy / n * r0 * 0.55, z0 + dz / n * r0 * 0.55))
    p.add(props.tube(hl, 0.016, seg=8), 'post_hl', smooth=True)
    # the helmsman's awning (civ) on two hoops over the stern deck
    arch = []
    for yy in [-0.8 - 0.16 * i / 5 for i in range(6)]:
        z0 = deck(yy) + 0.08
        arch.append([(0.1 * math.cos(math.pi * k / 10), yy, z0 + 0.045 * math.sin(math.pi * k / 10)) for k in range(11)])
    p.add(props.loft(arch, False, False), 'tint', smooth=True, tint=True)
    for ring in (arch[0], arch[-1]):   # the hoops at its ends (a single-sheet edge broke up at 2x)
        p.add(props.tube([(x, y, z - 0.004) for x, y, z in ring], 0.01, seg=6), 'wood_dark', smooth=True)
    for sx in (-1, 1):
        for yy in (-0.8, -0.96):
            vkit.rod(p, (sx * 0.1, yy, deck(yy)), (sx * 0.1, yy, deck(yy) + 0.085), 0.01, 'wood_dark', seg=6)
        # steering oars on the quarters, with tillers
        a = (sx * (hull.deck_half(hull.s_of(-0.86)) + 0.02), -0.86, deck(-0.86) + 0.04)
        e = (sx * 0.16, -0.99, 0.0)   # steep, close along the quarter, short
        vkit.rod(p, a, e, 0.022, 'oar_low', seg=8)
        d = (Vector(e) - Vector(a)).normalized()
        m, _ = vkit.align(Vector(e) - d * 0.15, Vector(e) + d * 0.02)
        p.add(props.box(0.018, 0.075, 0.17), 'oar_low', m, bevel=0.004)   # its broad blade, the upper part above water
        vkit.rod(p, a, (sx * 0.06, -0.8, deck(-0.8) + 0.07), 0.009, 'wood_dark', seg=6)
    # the mast and its stays, fore to the stem and back to the stern
    my = g['mast_y']
    lr = g['line_r']
    vkit.rod(p, (0, my, deck(my)), (0, my, g['mast_top']), g['mast_r'], 'wood_dark', r1=g['mast_r'] * 0.75, seg=10)
    p.add(props.lathe([(0.0, 0.026), (0.03, 0.03), (0.05, 0.0)], seg=12), 'tar', T(0, my, g['mast_top'] - 0.01), smooth=True)   # the masthead cap
    p.add(props.tube([(0, yb - 0.06, 0.05 + 0.088), (0, yb + 0.06, 0.05 + 0.075), (0, yb + 0.16, 0.05 + 0.05)], 0.018, seg=8), 'gold', smooth=True)   # the ram's lit top ridge
    if g['stays']:
        for sx in (-1, 1):   # paired forestays to the bow's quarters (a centreline stay hid the trident head-on)
            yy = yb - 0.22
            vkit.rod(p, (0, my, g['mast_top'] - 0.02), (sx * (hull.deck_half(hull.s_of(yy)) + 0.005), yy, deck(yy) + 0.01),
                     g['stay_r'], 'cordage', seg=6)
        vkit.rod(p, (0, my, g['mast_top'] - 0.02), (0, -yb + 0.02, top - 0.01), g['stay_r'], 'cordage', seg=6)
    p.finish()

    # the rig (yard and sail), braced off square as a separate part
    r = vkit.part(root, 'rig', c, SHIP_WEAR)
    _square_sail(r, g['sail_w'], g['sail_h'], g['yard_z'], g['billow'], g['sail_device'], bands=g['sail_bands'], line_r=lr)
    o = r.finish()
    o.location = (0, my, 0)
    o.rotation_euler = (0, 0, math.radians(g['brace']))
    # the braces from the yard's ends aft to the quarters (in the hull's frame)
    if not g['braces']:
        return root
    q = vkit.part(root, 'braces', c, SHIP_WEAR)
    b = math.radians(g['brace'])
    for sx in (-1, 1):
        ex = sx * (g['sail_w'] / 2 + 0.02)
        end = (ex * math.cos(b) - 0.03 * math.sin(b), my + ex * math.sin(b) + 0.03 * math.cos(b), g['yard_z'] + 0.012)
        q.add(props.tube([end, (sx * 0.12, -0.7, deck(-0.7) + gh)], lr, seg=5), 'cordage', smooth=True)
    q.finish()
    return root


# ------------------------------------------------------------ caravel

def _castle(p, hull, y0, y1, h):
    """A raised deck (sterncastle) between y0 and y1, its sides following the hull."""
    n = 8
    zs = []
    rings = []
    for i in range(n + 1):
        y = y0 + (y1 - y0) * i / n
        s = hull.s_of(y)
        b = hull.deck_half(s) * 0.98
        z0 = hull.deck_z(s) - 0.02
        rings.append([(b, y, z0), (b, y, z0 + h), (-b, y, z0 + h), (-b, y, z0)])
        zs.append(z0 + h)
    bm = props.loft(rings)
    p.add(bm, 'wood', bevel=0.004, keys=lambda f: ('deck', False) if f.normal.z > 0.85 else None)
    path = [(r[0][0] * 0.98, r[0][1], r[1][2] + 0.03) for r in rings]
    p.add(props.tube(path, 0.012, seg=6), 'rail', smooth=True)
    p.add(props.tube([(-a[0], a[1], a[2]) for a in path], 0.012, seg=6), 'rail', smooth=True)
    return max(zs)


CARAVEL_COLORS = {'wood': '#5a3c22', 'wood_alt': '#4a301c', 'wood_dark': '#2a1e14', 'wood_light': '#a07c48',
                  'deck': '#7a5e3c', 'rail': '#2e2014', 'sail': '#d2c3a0', 'cordage': '#2b2118'}


def caravel(colors, rig_h=1.45, rig_w=1.75):
    """A caravel: a rich dark hull with curve, sheer and tumblehome, grime at
    the waterline, a sterncastle and bowsprit; square fore and main sails
    with the civ cross, a topsail and a lateen mizzen, weathered and patched;
    civ pennants."""
    c = dict(SHIP_COLORS, **CARAVEL_COLORS)
    c.update(colors)
    root = vkit.root('Caravel')
    p = vkit.part(root, 'hull', c, SHIP_WEAR)
    hull = Hull(L=2.5, B=0.66, D=0.2, draft=0.14, sheer_stern=0.3, sheer_bow=0.3, bow_p=1.3, stern_p=1.7,
                transom=0.25, rake_bow=0.3, fullness=1.0, tumblehome=0.2)
    # waterline grime and light wales give the hull its lines at 1x
    add_hull(p, hull, stripes=[(-0.03, 0.035, 'hull_grime'), (0.06, 0.085, 'gold'), (0.15, 0.175, 'gold'),
                               (0.3, 0.325, 'gold')])
    _castle(p, hull, -1.25, -0.6, 0.16)
    _castle(p, hull, -1.25, -0.92, 0.3)      # stepped: a poop deck above the sterncastle
    _castle(p, hull, 0.78, 1.08, 0.08)       # a low forecastle
    vkit.rod(p, (0, 1.12, 0.36), (0, 1.62, 0.52), 0.022, 'wood_dark', seg=8)          # bowsprit

    def deck(y):
        return hull.deck_z(hull.s_of(y))

    def mast(y, z_top, r):
        vkit.rod(p, (0, y, deck(y) - 0.05), (0, y, z_top), r, 'wood_dark', seg=10)

    # foremast: one square sail with the cross
    z = deck(0.52) + (0.5 - deck(0.52)) * rig_h
    h, w = 0.4 * rig_h, 0.72 * rig_w
    mast(0.52, z + h + 0.12, 0.026)
    sail(p, 0.54, z + h, w * 0.92, w, h, 'disc_cross', billow=0.22, seed=4)
    # mainmast: a course with the cross and a topsail, a pennant at the top
    z = deck(0.0) + (0.48 - deck(0.0)) * rig_h
    h1, w1 = 0.6 * rig_h, 1.15 * rig_w
    h2, w2 = 0.3 * rig_h, 0.66 * rig_w
    top = z + h1 + 0.025 * rig_h + h2 + 0.16
    mast(0.0, top, 0.032)
    sail(p, 0.02, z + h1, w1 * 0.92, w1, h1, 'disc_cross', billow=0.24, seed=2)
    sail(p, 0.02, z + h1 + 0.025 * rig_h + h2, w2 * 0.92, w2, h2, None, billow=0.09, seed=3, patches=2)
    pennant(p, (0, 0.0, top), 0.62, 0.26)   # Civ3's big blue masthead pennant
    # mizzen: a lateen sail with the cross
    mast(-0.7, deck(-0.7) + 1.1, 0.022)
    lateen(p, -0.7, deck(-0.7) + 1.0, 1.5, 30.0, 0.72, 'disc_cross')
    pennant(p, (0, -0.7, deck(-0.7) + 1.1), 0.3, 0.14, phase=1.0)
    # the forestay, and shroud ladders (shrouds with ratlines) down both sides to deadeyes on the channels
    vkit.rod(p, (0, 0.0, top - 0.1), (0, 1.6, 0.52), 0.007, 'cordage', seg=4)
    ztop = z + h1 + 0.02
    for sx in (-1, 1):
        xb = sx * hull.deck_half(hull.s_of(0.0)) * 1.04
        zb = deck(0.0) + 0.02
        legs = [((0.0, dy, ztop), (xb, dy * 1.6, zb)) for dy in (-0.12, -0.04, 0.04, 0.12)]
        for a, b in legs:
            vkit.rod(p, a, b, 0.006, 'cordage', seg=4)
            p.add(props.ellipsoid(0.02, 0.02, 0.02, seg=8), 'wood_dark', T(*b), smooth=True)
        for k in range(1, 9):
            t = k / 9
            pa = Vector(legs[0][0]).lerp(Vector(legs[0][1]), t)
            pb = Vector(legs[-1][0]).lerp(Vector(legs[-1][1]), t)
            vkit.rod(p, pa, pb, 0.004, 'cordage', seg=4)
        p.add(props.box(0.04, 0.5, 0.025), 'wood_dark', T(xb, 0.0, zb - 0.02), bevel=0.006)   # channel
    # the fighting top (crow's nest) on the mainmast
    p.add(props.lathe([(0.0, 0.1), (0.1, 0.11)], seg=16), 'wood_dark', T(0, 0.0, ztop + 0.05), smooth=True)
    p.add(props.lathe([(0.1, 0.112), (0.105, 0.0)], seg=16), 'wood_dark', T(0, 0.0, ztop + 0.05), smooth=True)
    # a balustrade on the sterncastle, a spritsail under the bowsprit, an anchor at the cathead
    zs = deck(-0.9) + 0.3
    for sx in (-1, 1):
        for k in range(7):
            yy = -1.2 + 0.08 * k
            xx = sx * hull.deck_half(hull.s_of(yy)) * 0.95
            vkit.rod(p, (xx, yy, zs), (xx, yy, zs + 0.08), 0.008, 'gold', seg=6)
        vkit.rod(p, (sx * hull.deck_half(hull.s_of(-1.2)) * 0.95, -1.2, zs + 0.08),
                 (sx * hull.deck_half(hull.s_of(-0.72)) * 0.95, -0.72, zs + 0.08), 0.01, 'gold', seg=6)
    sail(p, 1.42, 0.5, 0.36, 0.4, 0.22, None, billow=0.06, seed=7, patches=0)
    for sx in (1,):
        ax, ay = sx * hull.deck_half(hull.s_of(0.95)) * 1.05, 0.95
        vkit.rod(p, (ax, ay, deck(0.95) - 0.02), (ax, ay + 0.02, deck(0.95) - 0.22), 0.012, 'iron', seg=6)
        p.add(props.tube([(ax - 0.06, ay, deck(0.95) - 0.2), (ax, ay + 0.02, deck(0.95) - 0.25),
                          (ax + 0.06, ay, deck(0.95) - 0.2)], 0.01, seg=6), 'iron', smooth=True)
    p.finish()
    return root


# ------------------------------------------------------------ caravel (realism pass: a Portuguese caravela redonda)

# A caravela redonda of the 1490s (the Nina as re-rigged; notes/realism/Caravel.md): ~21 x 6.4 m,
# square fore- and mainsails, a lateen mizzen, a high aftercastle, a transom stern and a sternpost
# rudder. L 2.4 = 21 m (1 unit = 8.75 m).
REDONDA = dict(
    L=2.4, B=0.72, D=0.2, draft=0.14, sheer_stern=0.42, sheer_bow=0.32,
    fore=dict(y=0.62, top=1.2, w=0.92, h=0.58, yard=1.08),
    main=dict(y=0.0, top=1.64, w=1.29, h=0.8, yard=1.5, top_w=0.69, top_h=0.36),
    mizzen=dict(y=-0.6, top=1.2, yard=1.12, ang=32.0, drop=0.6),
    billow=0.24, device='pattee_big', fore_device='pattee', brace=32.0, pennant=(0.3, 0.14), bulwark_h=0.07,
    aft=((-0.99, -0.36, 0.22), (-0.99, -0.68, 0.54)), fwd=(0.6, 0.97, 0.13), rigging=True, stay_r=0.016,
    fit_scale=0.97,
)

REDONDA_COLORS = {
    'wood': '#4e3418', 'wood_alt': '#442c14', 'wood_low': '#36240f', 'wood_low_alt': '#301f0c',
    'wood_dark': '#3a2a1a', 'wale': '#161210', 'tar': '#2e261e', 'ash': '#b8945e', 'rail': '#3e2e1e',
    'deck': '#957444', 'deck_alt': '#8a6a3e', 'castle_face': '#8e6232',
    'sail': '#e2d3ae', 'sail_seam': '#c4b48e', 'sail_patch': '#d0c09a', 'sail_mid': '#cbbb95', 'sail_shade': '#bba982',
    'brail': '#54442f', 'gold': '#c8a048', 'iron': '#4a4a4c', 'cordage': '#2e2418',
}
for _k, _v in (('castle_face', ('#8a6436', 0.65, 0.0, 'wood')), ('ash', ('#b8945e', 0.6, 0.0, 'wood')),
               ('wood_low', ('#7e5e38', 0.62, 0.0, 'wood')), ('wood_low_alt', ('#735432', 0.62, 0.0, 'wood')),
               ('wale', ('#2a2018', 0.62, 0.0, 'wood')), ('tar', ('#2e261e', 0.7, 0.0, 'wood')),
               ('deck_alt', ('#907042', 0.7, 0.0, 'wood')), ('brail', ('#54442f', 0.85, 0.0, None)),
               ('sail_mid', ('#cbbb95', 0.9, 0.0, 'cloth')), ('sail_shade', ('#bba982', 0.9, 0.0, 'cloth'))):
    props.DEFAULTS.setdefault(_k, _v)


EMBLEMS['pattee_big'] = lambda x, z, h: _pattee(x, z, h * 1.15)   # the Order of Christ's cross, bold


def _cv_lateen(p, yard, ang, drop, billow, device, seg=24):
    """A lateen mizzen in local space (the yard crossing the masthead at the
    origin, its forward end low), the sail to a clew aft and low, bellying +x,
    the civ device in its middle."""
    a = math.radians(ang)
    fwd = (0.0, math.cos(a) * yard * 0.45, -math.sin(a) * yard * 0.45)
    aft = (0.0, -math.cos(a) * yard * 0.55, math.sin(a) * yard * 0.55)
    clew = (0.0, aft[1] * 0.45, -drop)
    bm = props.bmesh.new()
    grid = []
    for j in range(seg + 1):
        v = j / seg
        row = []
        for i in range(seg - j + 1):
            u = i / seg
            w0 = 1 - u - v
            pt = [fwd[k] * w0 + aft[k] * u + clew[k] * v for k in range(3)]
            pt[0] += billow * 4 * w0 * u * (1 - v) ** 0.5 + billow * 2.0 * v * (w0 + u) * 0.5
            row.append(bm.verts.new(pt))
        grid.append(row)
    for j in range(seg):
        for i in range(seg - j):
            bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i]))
            if i < seg - j - 1:
                bm.faces.new((grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
    mask = EMBLEMS.get(device)
    cy = (fwd[1] + aft[1] + clew[1]) / 3
    cz = (fwd[2] + aft[2] + clew[2]) / 3

    def keys(f):
        c = f.calc_center_median()
        if mask and mask(-(c.y - cy), c.z - cz, drop * 0.6):
            return ('tint', True)
        if abs((c.y / 0.11) % 1.0 - 0.5) > 0.44:
            return ('sail_seam', False)
        return ('sail_mid', False) if abs(c.x) < billow * 0.5 else None

    p.add(bm, 'sail', smooth=True, keys=keys)
    vkit.rod(p, fwd, aft, 0.016, 'wood_dark', seg=8)


def _cv_castle(p, hull, y0, y1, h, civ=False):
    """A raised castle between y0 and y1: solid painted sides following the hull,
    its deck on top, a dark band under a light rail cap (it reads as a tier at 1x)."""
    n = 10
    rings = []
    for i in range(n + 1):
        y = y0 + (y1 - y0) * i / n
        s = hull.s_of(y)
        b = hull.deck_half(s) * 0.98
        z0 = hull.deck_z(s) - 0.02
        rings.append([(b, y, z0), (b, y, z0 + h), (-b, y, z0 + h), (-b, y, z0)])
    p.add(props.loft(rings), 'castle_face', bevel=0.004,
          keys=lambda f: ('deck', False) if f.normal.z > 0.85 else None)
    for sx in (-1, 1):
        for dz, r, key in ((-0.03, 0.016 if civ else 0.011, 'tint' if civ else 'wale'), (0.012, 0.014, 'ash')):
            path = []
            for i in range(n + 1):
                y = y0 + (y1 - y0) * i / n
                s = hull.s_of(y)
                path.append((sx * (hull.deck_half(s) * 0.98 + 0.004), y, hull.deck_z(s) - 0.02 + h + dz))
            p.add(props.tube(path, r, seg=6, scale_xy=(1.0, 0.6)), key, smooth=True, tint=key == 'tint')


def _cv_shrouds(p, hull, y, z_top, legs=3, r=0.016):
    """Shrouds from a mast down to deadeyes on a channel at each rail (no ratlines:
    they break up at 2x)."""
    s = hull.s_of(y)
    zr = hull.deck_z(s) + 0.05
    for sx in (-1, 1):
        xb = sx * (hull.deck_half(s) + 0.035)
        for k in range(legs):
            dy = -0.06 - 0.09 * k
            b = (xb, y + dy, zr)
            vkit.rod(p, (0, y, z_top), b, r, 'cordage', seg=6)
            p.add(props.ellipsoid(0.022, 0.022, 0.026, seg=10), 'tar', T(*b), smooth=True)
        p.add(props.box(0.05, 0.36, 0.022), 'wood_dark', T(xb - sx * 0.01, y - 0.15, zr - 0.02), bevel=0.005)


def caravel_redonda(colors, **kw):
    """The realism pass's Caravel: a Portuguese caravela redonda (c. 1490): a
    short, beamy, strongly sheered hull in warm wood with tumblehome, a dark
    wale and a light gunwale cap, a high aftercastle over a transom with a
    sternpost rudder, a short bowsprit; square fore- and mainsails (a main
    topsail) with the civ cross pattee, a lateen mizzen with the cross; a civ
    pennant at the main truck."""
    g = dict(REDONDA, **kw)
    c = dict(SHIP_COLORS, **REDONDA_COLORS)
    c.update(colors)
    root = vkit.root('Caravel')
    root['fit_scale'] = g['fit_scale']
    p = vkit.part(root, 'hull', c, SHIP_WEAR)
    hull = Hull(L=g['L'], B=g['B'], D=g['D'], draft=g['draft'], sheer_stern=g['sheer_stern'],
                sheer_bow=g['sheer_bow'], bow_p=1.35, stern_p=2.2, transom=0.42, rake_bow=0.25, fullness=1.0,
                tumblehome=0.16)
    yb = g['L'] / 2

    def deck(y):
        return hull.deck_z(hull.s_of(y))

    ss = [-1 + 2 * i / 48 for i in range(49)]
    bm = props.loft([hull.section(s, m=32) for s in ss])

    def hkeys(f):
        cc = f.calc_center_median()
        if f.normal.z > 0.85:
            return ('deck_alt', False) if int(abs(cc.x) / 0.03) % 2 else ('deck', False)
        if cc.z < 0.012:
            return ('hull_grime', False)
        if cc.z < 0.04:
            return ('tar', False)
        zd = hull.deck_z(hull.s_of(cc.y))
        low = cc.z < zd - 0.08
        if int(math.floor(cc.z / 0.026)) % 2:
            return ('wood_low_alt' if low else 'wood_alt', False)
        return ('wood_low', False) if low else None

    p.add(bm, 'wood', smooth=True, keys=hkeys)
    for sx in (-1, 1):
        for dz, r in ((0.13, 0.022), (0.085, 0.028), (0.035, 0.022)):   # the main wale and a lighter one above it
            path = []
            for i in range(1, 60):
                s_ = -0.95 + 1.9 * i / 60
                z = hull.deck_z(s_) - dz
                path.append((sx * (hull.side_x(s_, z) + 0.004), hull.y(s_), z))
            p.add(props.tube(path, r, seg=6, scale_xy=(1.0, 0.45)), 'wale', smooth=True)
        # the waist bulwark (the hull's side carried up), its light cap
        bh = g['bulwark_h']
        ysb = [-yb * 0.95 + 1.9 * yb * i / 50 for i in range(51)]
        rings = []
        for y in ysb:
            s_ = hull.s_of(y)
            xo = sx * hull.deck_half(s_) * 0.995
            xi = xo - sx * 0.014
            z0 = deck(y) - 0.01
            rings.append([(xo, y, z0), (xo, y, z0 + bh), (xi, y, z0 + bh), (xi, y, z0)])
        p.add(props.loft(rings), 'wood', bevel=0.003)
        p.add(props.tube([(sx * (hull.deck_half(hull.s_of(y)) * 0.995 - sx * 0.007), y, deck(y) - 0.01 + bh + 0.006) for y in ysb],
                         0.014, seg=8), 'ash', smooth=True)
    # the stepped aftercastle (the upper tier with a civ band), a small raised forecastle; hatches
    for (y0, y1, h), civ in zip(g['aft'], (False, True)):
        _cv_castle(p, hull, y0 * yb, y1 * yb, h, civ=civ)
    _cv_castle(p, hull, g['fwd'][0] * yb, g['fwd'][1] * yb, g['fwd'][2])
    for yy in (0.3, -0.18):
        p.add(props.box(0.18, 0.14, 0.02), 'rail', T(0, yy, deck(yy) + 0.008), bevel=0.003)
        p.add(props.box(0.14, 0.1, 0.022), 'tar', T(0, yy, deck(yy) + 0.01), bevel=0.002)
    # the transom's gilt lantern, the sternpost rudder, the bowsprit
    ys_ = -yb
    p.add(props.lathe([(0.0, 0.0), (0.02, 0.035), (0.08, 0.035), (0.11, 0.0)], seg=10), 'gold',
          T(0, ys_ + 0.02, deck(ys_) + 0.3), smooth=True)
    vkit.rod(p, (0, ys_ + 0.02, deck(ys_) + 0.22), (0, ys_ + 0.02, deck(ys_) + 0.3), 0.012, 'wood_dark', seg=6)
    rud = props.slab([(0.0, -0.02), (-0.13, 0.0), (-0.15, deck(ys_) * 0.6), (-0.04, deck(ys_) * 0.9), (0.0, deck(ys_) + 0.02)], 0.026, 'X')
    p.add(rud, 'wood_dark', T(0, ys_ - 0.01, 0), bevel=0.003)
    vkit.rod(p, (0, yb * 0.9, deck(yb * 0.9) + 0.04), (0, yb + 0.38, deck(yb) + 0.16), 0.024, 'wood_dark', r1=0.016, seg=8)

    # masts and yards (tarred caps), the main top (fighting top)
    fm, mm, zm = g['fore'], g['main'], g['mizzen']
    for spec, r in ((fm, 0.026), (mm, 0.032), (zm, 0.022)):
        top = spec['top'] + (0.46 if spec is mm else 0.04)
        vkit.rod(p, (0, spec['y'], deck(spec['y']) - 0.05), (0, spec['y'], top), r, 'wood_dark', r1=r * 0.7, seg=10)
        p.add(props.lathe([(0.0, 0.024), (0.03, 0.028), (0.05, 0.0)], seg=12), 'tar', T(0, spec['y'], top), smooth=True)
        spec['_truck'] = top
    p.add(props.lathe([(0.0, 0.09), (0.05, 0.1), (0.055, 0.0)], seg=16), 'wood_dark', T(0, mm['y'], mm['top'] + 0.04), smooth=True)
    _pennant_cv(p, (0, mm['y'], mm['_truck'] + 0.01), *g['pennant'])
    if g['rigging']:
        _cv_shrouds(p, hull, fm['y'], fm['top'] - 0.02)
        _cv_shrouds(p, hull, mm['y'], mm['top'] - 0.02)
        _cv_shrouds(p, hull, zm['y'], zm['top'] - 0.2, legs=2)
        r = g['stay_r']
        vkit.rod(p, (0, fm['y'], fm['top'] - 0.02), (0, yb + 0.34, deck(yb) + 0.15), r, 'cordage', seg=6)   # forestay
        vkit.rod(p, (0, mm['y'], mm['top'] - 0.02), (0, fm['y'], deck(fm['y']) + 0.12), r, 'cordage', seg=6)   # mainstay
    p.finish()
    # the square sails (fore course, main course with the cross, the main topsail) and the lateen mizzen
    for spec, w, h, z_top, emb in ((fm, fm['w'], fm['h'], fm['top'], g['fore_device']),
                                   (mm, mm['w'], mm['h'], mm['top'], g['device']),
                                   (mm, mm['top_w'], mm['top_h'], mm['top'] + 0.42, None)):
        r = vkit.part(root, 'sail', c, SHIP_WEAR)
        _square_sail(r, w, h, z_top, g['billow'], emb, bands=1.5, brails=0, line_r=0.009, seed=int(10 * h))
        o = r.finish()
        o.location = (0, spec['y'] + 0.02, 0)
        o.rotation_euler = (0, 0, math.radians(g['brace']))   # braced: faces in the profiles
    r = vkit.part(root, 'mizzen', c, SHIP_WEAR)
    _cv_lateen(r, zm['yard'], zm['ang'], zm['drop'], g['billow'], g['device'])
    o = r.finish()
    o.location = (0, zm['y'], zm['top'] - 0.02)
    return root


def _pennant_cv(p, top, w, h, phase=0.0):
    """A civ pennant streaming aft, keeping 40 % of its height at the tip."""
    n = 10
    bm = props.bmesh.new()
    rows = []
    for j in range(n + 1):
        t = j / n
        hh = h * (1 - 0.6 * t)
        rows.append([bm.verts.new((0.06 * t * math.sin(5 * t + phase), top[1] - w * t, top[2] - hh * k)) for k in (0.0, 1.0)])
    for j in range(n):
        bm.faces.new((rows[j][0], rows[j + 1][0], rows[j + 1][1], rows[j][1]))
    p.add(bm, 'tint', smooth=True, tint=True)


# ------------------------------------------------------------ carrack (realism pass: an Iberian carrack, c. 1500)

# An Iberian carrack (nau) of c. 1500 (notes/realism/Carrack.md): ~27 x 9 m, a round, deep hull with a
# towering forecastle over the bow and a two-tier aftercastle, fore and main with courses, topsails and
# fighting tops, a lateen mizzen. L 2.7 = 27 m (1 unit = 10 m).
CARRACK = dict(
    L=2.7, B=0.9, D=0.2, draft=0.16, sheer_stern=0.46, sheer_bow=0.42,
    fore=dict(y=0.72, top=1.6, w=1.0, h=0.56, top_w=0.66, top_h=0.36),
    main=dict(y=0.02, top=2.0, w=1.35, h=0.74, top_w=0.9, top_h=0.44),
    mizzen=dict(y=-0.78, top=1.4, yard=1.24, ang=32.0, drop=0.62),
    billow=0.3, device='pattee_ck', pennant=(0.4, 0.13), bulwark_h=0.07, ports=4, rigging=True, stay_r=0.016,
    aft=((-0.98, -0.36, 0.3), (-0.98, -0.64, 0.66)), fwd=((0.5, 0.36, 0.24), (0.64, 0.3, 0.48), (0.76, 0.22, 0.72)), brace=30.0, fore_brace=35.0,
    fit_scale=1.0386, rig_scale=0.7,
)

CARRACK_COLORS = {
    'wood': '#5e4222', 'wood_alt': '#563c1e', 'wood_low': '#3c2812', 'wood_low_alt': '#342210',
    'wood_dark': '#3a2a1a', 'wale': '#5e4428', 'tar': '#2e261e', 'ash': '#b8945e', 'rail': '#3e2e1e',
    'deck': '#8a6a42', 'deck_alt': '#80623c', 'frame': '#9a7848', 'deck_line': '#7a5a34', 'wale_trim': '#9a7848', 'castle_face': '#4a3018', 'castle_band': '#7e5a30', 'glass': '#1a1610',
    'sail': '#d6c6a2', 'sail_seam': '#bcac86', 'sail_patch': '#d0c09a', 'sail_mid': '#cbbb95', 'sail_shade': '#bba982',
    'brail': '#54442f', 'gold': '#c8a048', 'iron': '#4a4a4c', 'cordage': '#2e2418',
}
for _k, _v in (('castle_face', ('#8a6436', 0.65, 0.0, 'wood')), ('ash', ('#b8945e', 0.6, 0.0, 'wood')),
               ('wood_low', ('#7e5e38', 0.62, 0.0, 'wood')), ('wood_low_alt', ('#735432', 0.62, 0.0, 'wood')),
               ('wale', ('#2a2018', 0.62, 0.0, 'wood')), ('tar', ('#2e261e', 0.7, 0.0, 'wood')),
               ('deck_alt', ('#907042', 0.7, 0.0, 'wood')), ('brail', ('#54442f', 0.85, 0.0, None)),
               ('sail_mid', ('#cbbb95', 0.9, 0.0, 'cloth')), ('sail_shade', ('#bba982', 0.9, 0.0, 'cloth'))):
    props.DEFAULTS.setdefault(_k, _v)
props.DEFAULTS.setdefault('castle_band', ('#7e5a30', 0.65, 0.0, 'wood'))
props.DEFAULTS.setdefault('frame', ('#7a5a34', 0.65, 0.0, 'wood'))
props.DEFAULTS.setdefault('deck_line', ('#7a5a34', 0.65, 0.0, 'wood'))
props.DEFAULTS.setdefault('wale_trim', ('#9a7848', 0.62, 0.0, 'wood'))
props.DEFAULTS.setdefault('glass', ('#1a1610', 0.3, 0.0, None))


EMBLEMS['pattee_ck'] = lambda x, z, h: _pattee(x, z, h * 1.1)   # one solid Order of Christ cross


def _ck_sail(p, w, h, z_top, billow, emblem=None, inset=0.85, reefs=(0.2, 0.36), nu=36, nv=24):
    """A square sail by the fleet's sail-shape rule: straight edges along the
    yard, the leeches and the foot (the belly only in the middle of the cloth:
    billow * (1 - u^2) * sin(pi v), no twist), clean canvas, the emblem in the
    flatter centre (scaled by `inset`), two faint reef bands; the yard, with
    tarred arms. Local: the mast on the z axis, the yard along x at z_top."""
    def surf(u, v):
        return (u * w / 2, 0.03 + billow * (1 - u * u) * math.sin(math.pi * v), z_top - v * h)

    bm = props.bmesh.new()
    verts = [[bm.verts.new(surf(-1 + 2 * i / nu, j / nv)) for i in range(nu + 1)] for j in range(nv + 1)]
    for j in range(nv):
        for i in range(nu):
            bm.faces.new((verts[j][i], verts[j][i + 1], verts[j + 1][i + 1], verts[j + 1][i]))
    mask = EMBLEMS.get(emblem)

    def keys(f):
        c = f.calc_center_median()
        v = (z_top - c.z) / h
        if mask and mask(c.x / inset, (c.z - (z_top - h / 2)) / inset, h):
            return ('tint', True)
        if any(abs(v - r) < 0.018 for r in reefs):
            return ('sail_seam', False)
        if abs(c.x) > w * 0.36 or v > 0.82:
            return ('sail_mid', False)   # a calm fall-off toward the leeches and the foot
        return None

    p.add(bm, 'sail', smooth=True, keys=keys)
    vkit.rod(p, (-w / 2 - 0.04, 0.03, z_top + 0.012), (w / 2 + 0.04, 0.03, z_top + 0.012), 0.016, 'wood_dark', seg=8)
    for sx in (-1, 1):
        vkit.rod(p, (sx * (w / 2 - 0.02), 0.03, z_top + 0.012), (sx * (w / 2 + 0.05), 0.03, z_top + 0.012), 0.018, 'tar', seg=8)


def _ck_lateen(p, yard, ang, drop, billow, device, seg=24):
    """A lateen mizzen in local space (the yard crossing the masthead at the
    origin, its forward end low), the sail to a clew aft and low, bellying +x,
    the civ device in its middle."""
    a = math.radians(ang)
    fwd = (0.0, math.cos(a) * yard * 0.45, -math.sin(a) * yard * 0.45)
    aft = (0.0, -math.cos(a) * yard * 0.55, math.sin(a) * yard * 0.55)
    clew = (0.0, aft[1] * 0.45, -drop)
    bm = props.bmesh.new()
    grid = []
    for j in range(seg + 1):
        v = j / seg
        row = []
        for i in range(seg - j + 1):
            u = i / seg
            w0 = 1 - u - v
            pt = [fwd[k] * w0 + aft[k] * u + clew[k] * v for k in range(3)]
            pt[0] += billow * 4 * w0 * u * (1 - v) ** 0.5 + billow * 2.0 * v * (w0 + u) * 0.5
            row.append(bm.verts.new(pt))
        grid.append(row)
    for j in range(seg):
        for i in range(seg - j):
            bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i]))
            if i < seg - j - 1:
                bm.faces.new((grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
    mask = EMBLEMS.get(device)
    cy = (fwd[1] + aft[1] + clew[1]) / 3
    cz = (fwd[2] + aft[2] + clew[2]) / 3

    def keys(f):
        c = f.calc_center_median()
        if mask and mask(-(c.y - cy), c.z - cz, drop * 0.6):
            return ('tint', True)
        if abs((c.y / 0.11) % 1.0 - 0.5) > 0.44:
            return ('sail_seam', False)
        return ('sail_mid', False) if abs(c.x) < billow * 0.5 else None

    p.add(bm, 'sail', smooth=True, keys=keys)
    vkit.rod(p, fwd, aft, 0.016, 'wood_dark', seg=8)


def _ck_castle(p, hull, y0, y1, h):
    """A raised castle between y0 and y1: solid painted sides following the hull,
    its deck on top, a dark band under a light rail cap (it reads as a tier at 1x)."""
    n = 10
    rings = []
    for i in range(n + 1):
        y = y0 + (y1 - y0) * i / n
        s = hull.s_of(y)
        b = hull.deck_half(s) * 0.98
        z0 = hull.deck_z(s) - 0.02
        rings.append([(b, y, z0), (b, y, z0 + h), (-b, y, z0 + h), (-b, y, z0)])
    p.add(props.loft(rings), 'castle_face', bevel=0.004,
          keys=lambda f: ('deck', False) if f.normal.z > 0.85 else None)
    for sx in (-1, 1):
        for dz, r, key in ((-0.06, 0.02, 'castle_band'), (-0.002, 0.009, 'deck_line'), (0.055, 0.012, 'rail')):
            path = []
            for i in range(n + 1):
                y = y0 + (y1 - y0) * i / n
                s = hull.s_of(y)
                path.append((sx * (hull.deck_half(s) * 0.98 + 0.004), y, hull.deck_z(s) - 0.02 + h + dz))
            p.add(props.tube(path, r, seg=6, scale_xy=(1.0, 0.6)), key, smooth=True)
        for i in range(int(abs(y1 - y0) / 0.07) + 1):   # balusters under the dark rail (the tier's deck line below)
            y = y0 + (y1 - y0) * i / max(1, int(abs(y1 - y0) / 0.07))
            s = hull.s_of(y)
            zb = hull.deck_z(s) - 0.02 + h
            x = sx * (hull.deck_half(s) * 0.98 + 0.002)
            vkit.rod(p, (x, y, zb), (x, y, zb + 0.055), 0.01, 'rail', seg=5)


def _ck_forecastle(p, hull, y0, y1, h):
    """A forecastle tier from y0 to y1, which may lie past the stem: over the hull
    its sides follow the deck edge, past it they close to a point (a triangular
    stack overhanging the bow), standing on the stem-head's height."""
    yb = hull.L / 2
    n = 12
    rings = []
    zb = hull.deck_z(1.0)
    for i in range(n + 1):
        y = y0 + (y1 - y0) * i / n
        if y <= yb * 0.96:
            s = hull.s_of(y)
            b = hull.deck_half(s) * 0.98
            z0 = hull.deck_z(s) - 0.02
        else:
            t = (y - yb * 0.96) / max(y1 - yb * 0.96, 1e-6)
            b = max(0.02, hull.deck_half(hull.s_of(yb * 0.96)) * 0.98 * (1 - t))
            z0 = zb - 0.02 + 0.04 * t
        rings.append([(b, y, z0), (b, y, z0 + h), (-b, y, z0 + h), (-b, y, z0)])
    p.add(props.loft(rings), 'castle_face', bevel=0.004,
          keys=lambda f: ('deck', False) if f.normal.z > 0.85 else (('tar', False) if f.normal.z < -0.6 else None))   # a dark shadow under the overhang
    for sx in (-1, 1):
        path = [(sx * (r[0][0] + 0.004), r[0][1], r[1][2] + 0.012) for r in rings]
        p.add(props.tube(path, 0.013, seg=6, scale_xy=(1.0, 0.6)), 'ash', smooth=True)
        path = [(sx * (r[0][0] + 0.004), r[0][1], r[1][2] - 0.025) for r in rings]
        p.add(props.tube(path, 0.011, seg=6, scale_xy=(1.0, 0.6)), 'wale', smooth=True)


def _ck_shrouds(p, hull, y, z_top, legs=3, r=0.016):
    """Shrouds from a mast down to deadeyes on a channel at each rail (no ratlines:
    they break up at 2x)."""
    s = hull.s_of(y)
    zr = hull.deck_z(s) + 0.05
    for sx in (-1, 1):
        xb = sx * (hull.deck_half(s) + 0.035)
        for k in range(legs):
            dy = -0.06 - 0.09 * k
            b = (xb, y + dy, zr)
            vkit.rod(p, (0, y, z_top), b, r, 'cordage', seg=6)
            p.add(props.ellipsoid(0.022, 0.022, 0.026, seg=10), 'tar', T(*b), smooth=True)
        p.add(props.box(0.05, 0.36, 0.022), 'wood_dark', T(xb - sx * 0.01, y - 0.15, zr - 0.02), bevel=0.005)


def carrack_nau(colors, **kw):
    """The realism pass's Carrack: an Iberian nau of c. 1500: a round, deep,
    sheered hull in warm wood, darker below two dark wales, a bulwark under a
    light cap, a row of lidded gunports; a towering two-tier forecastle over
    the bow and a two-tier aftercastle (light rail caps), a gilt stern lantern,
    a sternpost rudder, a bowsprit; fore and main with courses and topsails
    (civ quartered emblems on the courses) and fighting tops, a lateen mizzen;
    a civ pennant at the main truck."""
    g = dict(CARRACK, **kw)
    c = dict(SHIP_COLORS, **CARRACK_COLORS)
    c.update(colors)
    root = vkit.root('Carrack')
    root['fit_scale'] = g['fit_scale']
    p = vkit.part(root, 'hull', c, SHIP_WEAR)
    hull = Hull(L=g['L'], B=g['B'], D=g['D'], draft=g['draft'], sheer_stern=g['sheer_stern'],
                sheer_bow=g['sheer_bow'], bow_p=1.35, stern_p=2.2, transom=0.42, rake_bow=0.25, fullness=1.0,
                tumblehome=0.16)
    yb = g['L'] / 2

    def deck(y):
        return hull.deck_z(hull.s_of(y))

    ss = [-1 + 2 * i / 48 for i in range(49)]
    bm = props.loft([hull.section(s, m=32) for s in ss])

    def hkeys(f):
        cc = f.calc_center_median()
        if f.normal.z > 0.85:
            return ('deck_alt', False) if int(abs(cc.x) / 0.03) % 2 else ('deck', False)
        if cc.z < 0.012:
            return ('hull_grime', False)
        if cc.z < 0.04:
            return ('tar', False)
        zd = hull.deck_z(hull.s_of(cc.y))
        low = cc.z < zd - 0.08
        if int(math.floor(cc.z / 0.026)) % 2:
            return ('wood_low_alt' if low else 'wood_alt', False)
        return ('wood_low', False) if low else None

    p.add(bm, 'wood', smooth=True, keys=hkeys)
    for sx in (-1, 1):
        for dz, r in ((0.08, 0.02), (0.035, 0.016)):   # the main wale and a lighter one above it
            path = []
            for i in range(1, 60):
                s_ = -0.95 + 1.9 * i / 60
                z = hull.deck_z(s_) - dz
                path.append((sx * (hull.side_x(s_, z) + 0.004), hull.y(s_), z))
            p.add(props.tube(path, r, seg=6, scale_xy=(1.0, 0.45)), 'wale', smooth=True)
            p.add(props.tube([(x_ + (0.003 if x_ > 0 else -0.003), y_, z_ + r * 0.9) for x_, y_, z_ in path], 0.005, seg=5), 'wale_trim', smooth=True)   # a lighter trim edge
        # the waist bulwark (the hull's side carried up), its light cap
        bh = g['bulwark_h']
        ysb = [-yb * 0.95 + 1.9 * yb * i / 50 for i in range(51)]
        rings = []
        for y in ysb:
            s_ = hull.s_of(y)
            xo = sx * hull.deck_half(s_) * 0.995
            xi = xo - sx * 0.014
            z0 = deck(y) - 0.01
            rings.append([(xo, y, z0), (xo, y, z0 + bh), (xi, y, z0 + bh), (xi, y, z0)])
        p.add(props.loft(rings), 'wood', bevel=0.003)
        p.add(props.tube([(sx * (hull.deck_half(hull.s_of(y)) * 0.995 - sx * 0.007), y, deck(y) - 0.01 + bh + 0.006) for y in ysb],
                         0.014, seg=8), 'ash', smooth=True)
    # the castles: two tiers aft, two tiers forward; painted sides, a dark shadow band under a light rail
    for y0, y1, h in g['aft']:
        _ck_castle(p, hull, y0 * yb, y1 * yb, h)
    for y0, over, h in g['fwd']:   # the forecastle: a triangular stack overhanging the bow
        _ck_forecastle(p, hull, y0 * yb, yb + over, h)
    # the transom: gilt-framed dark windows and a stern gallery
    yt = -yb * 0.985
    zt = deck(yt) + 0.1
    w = hull.deck_half(hull.s_of(yt)) * 1.5
    for row in (0, 1):
        for i in range(3):
            x = -w / 2 + w * (i + 0.5) / 3
            p.add(props.box(w / 3 * 0.75, 0.02, 0.07), 'gold', T(x, yt - 0.012, zt + 0.12 * row), bevel=0.003)
            p.add(props.box(w / 3 * 0.55, 0.024, 0.05), 'glass', T(x, yt - 0.014, zt + 0.12 * row), bevel=0.002)
    p.add(props.box(w * 1.1, 0.07, 0.016), 'castle_face', T(0, yt - 0.04, zt - 0.06), bevel=0.004)   # the gallery's floor
    p.add(props.box(w * 1.1, 0.012, 0.05), 'ash', T(0, yt - 0.075, zt - 0.03), bevel=0.003)   # its rail
    # hatches on the waist deck (a covered deck, not an open boat)
    for yy in (0.1,):   # one hatch: a tidier waist
        p.add(props.box(0.2, 0.16, 0.02), 'rail', T(0, yy, deck(yy) + 0.008), bevel=0.003)
        p.add(props.box(0.16, 0.12, 0.022), 'tar', T(0, yy, deck(yy) + 0.01), bevel=0.002)
    # lidded gunports along the waist: dark ports, the raised lids lighter
    for k in range(g['ports']):
      for zo, off in ((0.115, 0.0), (0.2, 0.5)):
        y = -0.42 + 0.84 * (k + off) / max(1, g['ports'] - 1)
        if off and k == g['ports'] - 1:
            continue
        s_ = hull.s_of(y)
        z = hull.deck_z(s_) - zo
        for sx in (-1, 1):
            x = sx * (hull.side_x(s_, z) + 0.004)
            p.add(props.box(0.01, 0.095, 0.086), 'frame', T(x, y, z), bevel=0.003)   # a mid-tone frame
            p.add(props.box(0.016, 0.072, 0.064), 'glass', T(x, y, z), bevel=0.003)   # the dark recess
            p.add(props.box(0.008, 0.078, 0.06), 'castle_band', T(x * 1.04, y, z + 0.066) @ R('Y', sx * 40), bevel=0.002)
    # the transom's gilt lantern, the sternpost rudder, the bowsprit
    ys_ = -yb
    p.add(props.lathe([(0.0, 0.0), (0.02, 0.035), (0.08, 0.035), (0.11, 0.0)], seg=10), 'gold',
          T(0, ys_ + 0.02, deck(ys_) + 0.3), smooth=True)
    vkit.rod(p, (0, ys_ + 0.02, deck(ys_) + 0.22), (0, ys_ + 0.02, deck(ys_) + 0.3), 0.012, 'wood_dark', seg=6)
    rud = props.slab([(0.0, -0.02), (-0.09, 0.0), (-0.11, deck(ys_) * 0.6), (0.0, deck(ys_) + 0.02)], 0.02, 'X')
    p.add(rud, 'wood_dark', T(0, ys_ - 0.01, 0), bevel=0.003)
    vkit.rod(p, (0, yb * 0.9, deck(yb * 0.9) - 0.01), (0, yb + 0.6, deck(yb) + 0.08), 0.032, 'wood_dark', r1=0.02, seg=8)   # under the forecastle

    # masts and yards (tarred caps), the main top (fighting top)
    fm, mm, zm = g['fore'], g['main'], g['mizzen']
    k = g.get('rig_scale', 1.0)   # a shorter rig: masts, yards and sails scaled about each mast's foot (1.0 = as reviewed)
    if k != 1.0:
        fm, mm, zm = dict(fm), dict(mm), dict(zm)
        for spec in (fm, mm, zm):
            d0 = deck(spec['y'])
            spec['top'] = d0 + k * (spec['top'] - d0)
            for key in ('w', 'h', 'top_w', 'top_h', 'yard', 'drop'):
                if key in spec:
                    spec[key] *= k
    for spec, r in ((fm, 0.03), (mm, 0.04), (zm, 0.024)):
        top = spec['top'] + (spec['top_h'] + 0.1 if 'top_h' in spec else 0.04)
        vkit.rod(p, (0, spec['y'], deck(spec['y']) - 0.05), (0, spec['y'], top), r, 'wood_dark', r1=r * 0.7, seg=10)
        p.add(props.lathe([(0.0, 0.024), (0.03, 0.028), (0.05, 0.0)], seg=12), 'tar', T(0, spec['y'], top), smooth=True)
        spec['_truck'] = top
    for spec in (fm, mm):   # the fighting tops
        p.add(props.lathe([(0.0, 0.08), (0.06, 0.16), (0.12, 0.17), (0.125, 0.0)], seg=16), 'tar',
              T(0, spec['y'], spec['top'] + 0.02), smooth=True)
    _pennant_ck(p, (0, mm['y'], mm['_truck'] + 0.02), *g['pennant'])
    if g['rigging']:
        _ck_shrouds(p, hull, fm['y'], fm['top'] - 0.02)
        _ck_shrouds(p, hull, mm['y'], mm['top'] - 0.02)
        _ck_shrouds(p, hull, zm['y'], zm['top'] - 0.25, legs=2)
        r = g['stay_r']
        vkit.rod(p, (0, fm['y'], fm['top'] - 0.02), (0, yb + 0.56, deck(yb) + 0.08), r, 'cordage', seg=6)   # forestay
        vkit.rod(p, (0, mm['y'], mm['top'] - 0.02), (0, fm['y'], deck(fm['y']) + 0.12), r, 'cordage', seg=6)   # mainstay
    p.finish()
    # the square sails (fore course, main course with the cross, the main topsail) and the lateen mizzen
    for spec, w, h, z_top, emb in ((fm, fm['w'], fm['h'], fm['top'], g['device']),
                                   (fm, fm['top_w'], fm['top_h'], fm['top'] + fm['top_h'] + 0.06, g['device']),
                                   (mm, mm['w'], mm['h'], mm['top'], g['device']),
                                   (mm, mm['top_w'], mm['top_h'], mm['top'] + mm['top_h'] + 0.06, g['device'])):
        r = vkit.part(root, 'sail', c, SHIP_WEAR)
        _ck_sail(r, w, h, z_top, g['billow'], emb, inset=1.0)
        o = r.finish()
        o.location = (0, spec['y'] + 0.02, 0)
        o.rotation_euler = (0, 0, math.radians(g['fore_brace'] if spec is fm else g['brace']))   # braced: the faces show in the profiles
    r = vkit.part(root, 'mizzen', c, SHIP_WEAR)
    _ck_lateen(r, zm['yard'], zm['ang'], zm['drop'], g['billow'], None)
    o = r.finish()
    o.location = (0, zm['y'], zm['top'] - 0.02)
    return root


def _pennant_ck(p, top, w, h, phase=0.0):
    """A civ pennant streaming aft, keeping 40 % of its height at the tip."""
    n = 10
    bm = props.bmesh.new()
    rows = []
    for j in range(n + 1):
        t = j / n
        hh = h * (1 - 0.6 * t)
        rows.append([bm.verts.new((0.06 * t * math.sin(5 * t + phase), top[1] - w * t, top[2] - hh * k)) for k in (0.0, 1.0)])
    for j in range(n):
        bm.faces.new((rows[j][0], rows[j + 1][0], rows[j + 1][1], rows[j][1]))
    p.add(bm, 'tint', smooth=True, tint=True)


# ------------------------------------------------------------ galleon (realism pass: a Spanish galleon, c. 1588)

# A Spanish galleon of the Armada (c. 1588; notes/realism/Galleon.md): ~38 x 9.5 m (L:B ~4), a lower
# forecastle set back from the bow over a long beakhead, a tall stern with a galleried transom, two
# rows of gunports, fore and main with courses and topsails, a lateen mizzen. L 3.0 = 38 m.
GALLEON = dict(
    L=3.0, B=0.78, D=0.24, draft=0.16, sheer_stern=0.4, sheer_bow=0.26,
    fore=dict(y=0.82, top=1.52, w=1.06, h=0.6, top_w=0.76, top_h=0.42),
    main=dict(y=0.05, top=1.9, w=1.38, h=0.78, top_w=1.0, top_h=0.5),
    mizzen=dict(y=-0.78, top=1.5, yard=1.45, ang=32.0, drop=0.74),
    billow=0.26, device='stripes_wide', pennant=(0.46, 0.15), bulwark_h=0.07, ports=7, rigging=True, stay_r=0.018,
    aft=((-0.98, -0.4, 0.3), (-0.98, -0.66, 0.6)), fwd=((0.56, 0.9, 0.16),), brace=20.0, beak=0.5,
    fit_scale=1.06,
)

GALLEON_COLORS = {
    'wood': '#4a3018', 'wood_alt': '#402a14', 'wood_low': '#342210', 'wood_low_alt': '#2e1e0e',
    'wood_dark': '#3a2a1a', 'wale': '#8a6436', 'tar': '#2e261e', 'ash': '#b8945e', 'rail': '#3e2e1e',
    'deck': '#6e5434', 'deck_alt': '#644c2e', 'castle_face': '#6a4a26', 'glass': '#1a1610',
    'sail': '#e2d3ae', 'sail_seam': '#c4b48e', 'sail_patch': '#d0c09a', 'sail_mid': '#cbbb95', 'sail_shade': '#bba982',
    'brail': '#54442f', 'gold': '#c8a048', 'iron': '#4a4a4c', 'cordage': '#2e2418',
}
for _k, _v in (('castle_face', ('#8a6436', 0.65, 0.0, 'wood')), ('ash', ('#b8945e', 0.6, 0.0, 'wood')),
               ('wood_low', ('#7e5e38', 0.62, 0.0, 'wood')), ('wood_low_alt', ('#735432', 0.62, 0.0, 'wood')),
               ('wale', ('#2a2018', 0.62, 0.0, 'wood')), ('tar', ('#2e261e', 0.7, 0.0, 'wood')),
               ('deck_alt', ('#907042', 0.7, 0.0, 'wood')), ('brail', ('#54442f', 0.85, 0.0, None)),
               ('sail_mid', ('#cbbb95', 0.9, 0.0, 'cloth')), ('sail_shade', ('#bba982', 0.9, 0.0, 'cloth'))):
    props.DEFAULTS.setdefault(_k, _v)
props.DEFAULTS.setdefault('glass', ('#1a1610', 0.3, 0.0, None))


EMBLEMS['stripes_wide'] = lambda x, z, h: 0.1 * h < abs(x) < 0.44 * h and abs(z) < 0.5 * h   # two bold civ stripes


def _gn_lateen(p, yard, ang, drop, billow, device, seg=24):
    """A lateen mizzen in local space (the yard crossing the masthead at the
    origin, its forward end low), the sail to a clew aft and low, bellying +x,
    the civ device in its middle."""
    a = math.radians(ang)
    fwd = (0.0, math.cos(a) * yard * 0.45, -math.sin(a) * yard * 0.45)
    aft = (0.0, -math.cos(a) * yard * 0.55, math.sin(a) * yard * 0.55)
    clew = (0.0, aft[1] * 0.45, -drop)
    bm = props.bmesh.new()
    grid = []
    for j in range(seg + 1):
        v = j / seg
        row = []
        for i in range(seg - j + 1):
            u = i / seg
            w0 = 1 - u - v
            pt = [fwd[k] * w0 + aft[k] * u + clew[k] * v for k in range(3)]
            pt[0] += billow * 4 * w0 * u * (1 - v) ** 0.5 + billow * 2.0 * v * (w0 + u) * 0.5
            row.append(bm.verts.new(pt))
        grid.append(row)
    for j in range(seg):
        for i in range(seg - j):
            bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i]))
            if i < seg - j - 1:
                bm.faces.new((grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
    mask = EMBLEMS.get(device)
    cy = (fwd[1] + aft[1] + clew[1]) / 3
    cz = (fwd[2] + aft[2] + clew[2]) / 3

    def keys(f):
        c = f.calc_center_median()
        if mask and mask(-(c.y - cy), c.z - cz, drop * 0.6):
            return ('tint', True)
        if abs((c.y / 0.11) % 1.0 - 0.5) > 0.44:
            return ('sail_seam', False)
        return ('sail_mid', False) if abs(c.x) < billow * 0.5 else None

    p.add(bm, 'sail', smooth=True, keys=keys)
    vkit.rod(p, fwd, aft, 0.016, 'wood_dark', seg=8)


def _gn_castle(p, hull, y0, y1, h):
    """A raised castle between y0 and y1: solid painted sides following the hull,
    its deck on top, a dark band under a light rail cap (it reads as a tier at 1x)."""
    n = 10
    rings = []
    for i in range(n + 1):
        y = y0 + (y1 - y0) * i / n
        s = hull.s_of(y)
        b = hull.deck_half(s) * 0.98
        z0 = hull.deck_z(s) - 0.02
        rings.append([(b, y, z0), (b, y, z0 + h), (-b, y, z0 + h), (-b, y, z0)])
    p.add(props.loft(rings), 'castle_face', bevel=0.004,
          keys=lambda f: ('deck', False) if f.normal.z > 0.85 else None)
    for sx in (-1, 1):
        for dz, r, key in ((-0.025, 0.011, 'wale'), (0.012, 0.014, 'ash')):
            path = []
            for i in range(n + 1):
                y = y0 + (y1 - y0) * i / n
                s = hull.s_of(y)
                path.append((sx * (hull.deck_half(s) * 0.98 + 0.004), y, hull.deck_z(s) - 0.02 + h + dz))
            p.add(props.tube(path, r, seg=6, scale_xy=(1.0, 0.6)), key, smooth=True)


def _gn_forecastle(p, hull, y0, y1, h):
    """A forecastle tier from y0 to y1, which may lie past the stem: over the hull
    its sides follow the deck edge, past it they close to a point (a triangular
    stack overhanging the bow), standing on the stem-head's height."""
    yb = hull.L / 2
    n = 12
    rings = []
    zb = hull.deck_z(1.0)
    for i in range(n + 1):
        y = y0 + (y1 - y0) * i / n
        if y <= yb * 0.96:
            s = hull.s_of(y)
            b = hull.deck_half(s) * 0.98
            z0 = hull.deck_z(s) - 0.02
        else:
            t = (y - yb * 0.96) / max(y1 - yb * 0.96, 1e-6)
            b = max(0.02, hull.deck_half(hull.s_of(yb * 0.96)) * 0.98 * (1 - t))
            z0 = zb - 0.02 + 0.04 * t
        rings.append([(b, y, z0), (b, y, z0 + h), (-b, y, z0 + h), (-b, y, z0)])
    p.add(props.loft(rings), 'castle_face', bevel=0.004,
          keys=lambda f: ('deck', False) if f.normal.z > 0.85 else None)
    for sx in (-1, 1):
        path = [(sx * (r[0][0] + 0.004), r[0][1], r[1][2] + 0.012) for r in rings]
        p.add(props.tube(path, 0.013, seg=6, scale_xy=(1.0, 0.6)), 'ash', smooth=True)
        path = [(sx * (r[0][0] + 0.004), r[0][1], r[1][2] - 0.025) for r in rings]
        p.add(props.tube(path, 0.011, seg=6, scale_xy=(1.0, 0.6)), 'wale', smooth=True)


def _gn_shrouds(p, hull, y, z_top, legs=3, r=0.02):
    """Shrouds from a mast down to deadeyes on a channel at each rail (no ratlines:
    they break up at 2x)."""
    s = hull.s_of(y)
    zr = hull.deck_z(s) + 0.05
    for sx in (-1, 1):
        xb = sx * (hull.deck_half(s) + 0.035)
        for k in range(legs):
            dy = -0.06 - 0.09 * k
            b = (xb, y + dy, zr)
            vkit.rod(p, (0, y, z_top), b, r, 'cordage', seg=6)
            p.add(props.ellipsoid(0.022, 0.022, 0.026, seg=10), 'tar', T(*b), smooth=True)
        p.add(props.box(0.05, 0.36, 0.022), 'wood_dark', T(xb - sx * 0.01, y - 0.15, zr - 0.02), bevel=0.005)


def galleon_armada(colors, **kw):
    """The realism pass's Galleon: a Spanish galleon of c. 1588: a long, dark
    walnut hull with three black wales, two rows of lidded gunports, a low
    forecastle set back over a long beakhead, a tall two-tier aftercastle
    with a galleried, gilt-windowed transom, a sternpost rudder; fore and main
    with courses and topsails (bold civ stripes), braced, with dark fighting
    tops; a lateen mizzen; dark standing rigging; a civ pennant."""
    g = dict(GALLEON, **kw)
    c = dict(SHIP_COLORS, **GALLEON_COLORS)
    c.update(colors)
    root = vkit.root('Galleon')
    root['fit_scale'] = g['fit_scale']
    p = vkit.part(root, 'hull', c, SHIP_WEAR)
    hull = Hull(L=g['L'], B=g['B'], D=g['D'], draft=g['draft'], sheer_stern=g['sheer_stern'],
                sheer_bow=g['sheer_bow'], bow_p=1.35, stern_p=2.2, transom=0.42, rake_bow=0.25, fullness=1.0,
                tumblehome=0.16)
    yb = g['L'] / 2

    def deck(y):
        return hull.deck_z(hull.s_of(y))

    ss = [-1 + 2 * i / 48 for i in range(49)]
    bm = props.loft([hull.section(s, m=32) for s in ss])

    def hkeys(f):
        cc = f.calc_center_median()
        if f.normal.z > 0.85:
            return ('deck_alt', False) if int(abs(cc.x) / 0.03) % 2 else ('deck', False)
        if cc.z < 0.012:
            return ('hull_grime', False)
        if cc.z < 0.04:
            return ('tar', False)
        zd = hull.deck_z(hull.s_of(cc.y))
        low = cc.z < zd - 0.08
        if int(math.floor(cc.z / 0.026)) % 2:
            return ('wood_low_alt' if low else 'wood_alt', False)
        return ('wood_low', False) if low else None

    p.add(bm, 'wood', smooth=True, keys=hkeys)
    for sx in (-1, 1):
        for dz, r in ((0.255, 0.018), (0.155, 0.02), (0.055, 0.018)):   # raised light wales between and over the two port rows
            path = []
            for i in range(1, 60):
                s_ = -0.95 + 1.9 * i / 60
                z = hull.deck_z(s_) - dz
                path.append((sx * (hull.side_x(s_, z) + 0.004), hull.y(s_), z))
            p.add(props.tube(path, r, seg=6, scale_xy=(1.0, 0.45)), 'wale', smooth=True)
        # the waist bulwark (the hull's side carried up), its light cap
        bh = g['bulwark_h']
        ysb = [-yb * 0.95 + 1.9 * yb * i / 50 for i in range(51)]
        rings = []
        for y in ysb:
            s_ = hull.s_of(y)
            xo = sx * hull.deck_half(s_) * 0.995
            xi = xo - sx * 0.014
            z0 = deck(y) - 0.01
            rings.append([(xo, y, z0), (xo, y, z0 + bh), (xi, y, z0 + bh), (xi, y, z0)])
        p.add(props.loft(rings), 'wood', bevel=0.003)
        p.add(props.tube([(sx * (hull.deck_half(hull.s_of(y)) * 0.995 - sx * 0.007), y, deck(y) - 0.01 + bh + 0.006) for y in ysb],
                         0.014, seg=8), 'ash', smooth=True)
    # the castles: two tiers aft, two tiers forward; painted sides, a dark shadow band under a light rail
    for y0, y1, h in g['aft']:
        _gn_castle(p, hull, y0 * yb, y1 * yb, h)
    for y0, y1, h in g['fwd']:   # the forecastle: low, set back from the bow
        _gn_castle(p, hull, y0 * yb, y1 * yb, h)
    # the beakhead: a long pointed platform projecting from the stem under the bowsprit
    zbk = deck(yb) - 0.06
    beak = props.slab([(-0.0, -0.06), (g['beak'], 0.0), (0.0, 0.06)], 0.03, 'X')
    p.add(props.loft([[(0.11 * (1 - t) + 0.012, yb - 0.04 + g['beak'] * t, zbk), (-0.11 * (1 - t) - 0.012, yb - 0.04 + g['beak'] * t, zbk),
                       (-0.08 * (1 - t) - 0.01, yb - 0.04 + g['beak'] * t, zbk - 0.03 - 0.03 * (1 - t)),
                       (0.08 * (1 - t) + 0.01, yb - 0.04 + g['beak'] * t, zbk - 0.03 - 0.03 * (1 - t))] for t in (0.0, 0.5, 1.0)]),
          'castle_face', bevel=0.004)
    for sx in (-1, 1):
        p.add(props.tube([(sx * 0.085, yb - 0.04, zbk + 0.03), (sx * 0.012, yb - 0.04 + g['beak'], zbk + 0.015)], 0.01, seg=6), 'ash', smooth=True)
    # the transom: gilt-framed dark windows and a stern gallery
    yt = -yb * 0.985
    zt = deck(yt) + 0.1
    w = hull.deck_half(hull.s_of(yt)) * 1.5
    for row in (0, 1):
        for i in range(3):
            x = -w / 2 + w * (i + 0.5) / 3
            p.add(props.box(w / 3 * 0.75, 0.02, 0.07), 'gold', T(x, yt - 0.012, zt + 0.12 * row), bevel=0.003)
            p.add(props.box(w / 3 * 0.55, 0.024, 0.05), 'glass', T(x, yt - 0.014, zt + 0.12 * row), bevel=0.002)
    p.add(props.box(w * 1.1, 0.07, 0.016), 'castle_face', T(0, yt - 0.04, zt - 0.06), bevel=0.004)   # the gallery's floor
    p.add(props.box(w * 1.1, 0.012, 0.05), 'ash', T(0, yt - 0.075, zt - 0.03), bevel=0.003)   # its rail
    # hatches on the waist deck (a covered deck, not an open boat)
    for yy in (0.32, -0.12):
        p.add(props.box(0.2, 0.16, 0.02), 'rail', T(0, yy, deck(yy) + 0.008), bevel=0.003)
        p.add(props.box(0.16, 0.12, 0.022), 'tar', T(0, yy, deck(yy) + 0.01), bevel=0.002)
    # lidded gunports along the waist: dark ports, the raised lids lighter
    for k in range(g['ports']):
      for zo, off in ((0.105, 0.0), (0.205, 0.5)):
        y = -0.8 + 1.6 * (k + off) / max(1, g['ports'] - 1)
        if off and k == g['ports'] - 1:
            continue
        s_ = hull.s_of(y)
        z = hull.deck_z(s_) - zo
        for sx in (-1, 1):
            x = sx * (hull.side_x(s_, z) + 0.004)
            p.add(props.box(0.01, 0.07, 0.064), 'ash', T(x, y, z), bevel=0.003)   # a light frame
            p.add(props.box(0.014, 0.05, 0.045), 'tar', T(x, y, z), bevel=0.003)
    # the transom's gilt lantern, the sternpost rudder, the bowsprit
    ys_ = -yb
    p.add(props.lathe([(0.0, 0.0), (0.02, 0.035), (0.08, 0.035), (0.11, 0.0)], seg=10), 'gold',
          T(0, ys_ + 0.02, deck(ys_) + 0.3), smooth=True)
    vkit.rod(p, (0, ys_ + 0.02, deck(ys_) + 0.22), (0, ys_ + 0.02, deck(ys_) + 0.3), 0.012, 'wood_dark', seg=6)
    rud = props.slab([(0.0, -0.02), (-0.09, 0.0), (-0.11, deck(ys_) * 0.6), (0.0, deck(ys_) + 0.02)], 0.02, 'X')
    p.add(rud, 'wood_dark', T(0, ys_ - 0.01, 0), bevel=0.003)
    vkit.rod(p, (0, yb * 0.9, deck(yb * 0.9) + 0.04), (0, yb + 0.38, deck(yb) + 0.16), 0.024, 'wood_dark', r1=0.016, seg=8)

    # masts and yards (tarred caps), the main top (fighting top)
    fm, mm, zm = g['fore'], g['main'], g['mizzen']
    for spec, r in ((fm, 0.026), (mm, 0.032), (zm, 0.022)):
        top = spec['top'] + (spec['top_h'] + 0.1 if 'top_h' in spec else 0.04)
        vkit.rod(p, (0, spec['y'], deck(spec['y']) - 0.05), (0, spec['y'], top), r, 'wood_dark', r1=r * 0.7, seg=10)
        p.add(props.lathe([(0.0, 0.024), (0.03, 0.028), (0.05, 0.0)], seg=12), 'tar', T(0, spec['y'], top), smooth=True)
        spec['_truck'] = top
    for spec in (fm, mm):   # the fighting tops
        p.add(props.lathe([(0.0, 0.08), (0.06, 0.16), (0.12, 0.165), (0.125, 0.0)], seg=16), 'tar',
              T(0, spec['y'], spec['top'] + 0.02), smooth=True)
    _pennant_gn(p, (0, mm['y'], mm['_truck'] + 0.02), *g['pennant'])
    if g['rigging']:
        _gn_shrouds(p, hull, fm['y'], fm['top'] - 0.02)
        _gn_shrouds(p, hull, mm['y'], mm['top'] - 0.02)
        _gn_shrouds(p, hull, zm['y'], zm['top'] - 0.25, legs=2)
        r = g['stay_r']
        vkit.rod(p, (0, fm['y'], fm['top'] - 0.02), (0, yb + 0.34, deck(yb) + 0.15), r, 'cordage', seg=6)   # forestay
        vkit.rod(p, (0, mm['y'], mm['top'] - 0.02), (0, fm['y'], deck(fm['y']) + 0.12), r, 'cordage', seg=6)   # mainstay
    p.finish()
    # the square sails (fore course, main course with the cross, the main topsail) and the lateen mizzen
    for spec, w, h, z_top, emb in ((fm, fm['w'], fm['h'], fm['top'], g['device']),
                                   (fm, fm['top_w'], fm['top_h'], fm['top'] + fm['top_h'] + 0.06, None),
                                   (mm, mm['w'], mm['h'], mm['top'], g['device']),
                                   (mm, mm['top_w'], mm['top_h'], mm['top'] + mm['top_h'] + 0.06, None)):
        r = vkit.part(root, 'sail', c, SHIP_WEAR)
        _square_sail(r, w, h, z_top, g['billow'], emb, bands=1.5, brails=0, line_r=0.009, seed=int(10 * h))
        o = r.finish()
        o.location = (0, spec['y'] + 0.02, 0)
        o.rotation_euler = (0, 0, math.radians(g['brace']))   # braced: the faces show in the profiles
    r = vkit.part(root, 'mizzen', c, SHIP_WEAR)
    _gn_lateen(r, zm['yard'], zm['ang'], zm['drop'], g['billow'], None)
    o = r.finish()
    o.location = (0, zm['y'], zm['top'] - 0.02)
    return root


def _pennant_gn(p, top, w, h, phase=0.0):
    """A civ pennant streaming aft, keeping 40 % of its height at the tip."""
    n = 10
    bm = props.bmesh.new()
    rows = []
    for j in range(n + 1):
        t = j / n
        hh = h * (1 - 0.6 * t)
        rows.append([bm.verts.new((0.06 * t * math.sin(5 * t + phase), top[1] - w * t, top[2] - hh * k)) for k in (0.0, 1.0)])
    for j in range(n):
        bm.faces.new((rows[j][0], rows[j + 1][0], rows[j + 1][1], rows[j][1]))
    p.add(bm, 'tint', smooth=True, tint=True)


# ------------------------------------------------------------ frigate (realism pass: a late-18th-century frigate)

# A 36-gun frigate of c. 1780-1805 (notes/realism/Frigate.md): ~44 x 12 m (L:B ~3.7), a long, low, flush
# hull, black with an ochre band through the single row of gunports (black lids: the Nelson chequer),
# three masts square-rigged with courses, topsails and topgallants, a spanker on the mizzen, jibs.
# L 3.0 = 44 m (1 unit = 14.7 m).
FRIGATE = dict(
    L=3.0, B=0.8, D=0.26, draft=0.15, sheer_stern=0.2, sheer_bow=0.16,
    masts=(dict(y=0.82, top=1.3, sails=((1.15, 0.53), (0.94, 0.46), (0.64, 0.28)), r=0.03),
           dict(y=0.04, top=1.45, sails=((1.42, 0.6), (1.15, 0.53), (0.78, 0.32)), r=0.036),
           dict(y=-0.82, top=1.32, sails=((0.9, 0.42), (0.88, 0.42), (0.6, 0.27)), r=0.028, spanker=(0.66, 0.56))),
    billow=0.24, device='roundel_big', brace=30.0, pennant=(0.46, 0.17), bulwark_h=0.1, ports=13,
    quarterdeck=(-0.98, -0.5, 0.07), forecastle=(0.6, 0.98, 0.06), rigging=True, stay_r=0.016,
    fit_scale=0.98,
)

FRIGATE_COLORS = {
    'wood': '#4a3422', 'wood_alt': '#42301e', 'wood_low': '#36261a', 'wood_low_alt': '#302216',
    'ochre': '#b88c48', 'wood_dark': '#2a2018', 'wale': '#6e5030', 'tar': '#2a2620', 'ash': '#b8945e',
    'rail': '#2a1e14', 'deck': '#6a5030', 'deck_alt': '#60482a', 'lit': '#e0b860', 'castle_face': '#24201c', 'glass': '#1a1610',
    'sail': '#e2d3ae', 'sail_seam': '#c4b48e', 'sail_patch': '#d0c09a', 'sail_mid': '#cbbb95', 'sail_shade': '#bba982',
    'brail': '#54442f', 'gold': '#c8a048', 'cordage': '#2e2418', 'port_lid': '#141210', 'copper': '#6e7a5e',
}
for _k, _v in (('ochre', ('#b88c48', 0.6, 0.0, 'wood')), ('port_lid', ('#141210', 0.6, 0.0, 'wood')),
               ('copper', ('#6e7a5e', 0.45, 1.0, 'metal')), ('castle_face', ('#8a6436', 0.65, 0.0, 'wood')),
               ('ash', ('#b8945e', 0.6, 0.0, 'wood')), ('wood_low', ('#7e5e38', 0.62, 0.0, 'wood')),
               ('wood_low_alt', ('#735432', 0.62, 0.0, 'wood')), ('wale', ('#2a2018', 0.62, 0.0, 'wood')),
               ('tar', ('#2e261e', 0.7, 0.0, 'wood')), ('deck_alt', ('#907042', 0.7, 0.0, 'wood')),
               ('brail', ('#54442f', 0.85, 0.0, None)), ('glass', ('#1a1610', 0.3, 0.0, None)),
               ('sail_mid', ('#cbbb95', 0.9, 0.0, 'cloth')), ('lit', ('#e0b860', 0.5, 0.0, None)), ('sail_shade', ('#bba982', 0.9, 0.0, 'cloth'))):
    props.DEFAULTS.setdefault(_k, _v)


EMBLEMS['roundel_big'] = lambda x, z, h: _roundel(x, z, h * 1.12)   # a bold civ roundel


def _fg_jib(p, head, tack, clew, seg=12):
    """A triangular headsail (no stay rod: a 1 px line breaks up)."""
    bm = props.bmesh.new()
    grid = []
    for j in range(seg + 1):
        v = j / seg
        row = []
        for i in range(seg - j + 1):
            u = i / seg
            w0 = 1 - u - v
            pt = [head[k] * w0 + tack[k] * u + clew[k] * v for k in range(3)]
            pt[0] += 0.06 * 4 * w0 * u
            row.append(bm.verts.new(pt))
        grid.append(row)
    for j in range(seg):
        for i in range(seg - j):
            bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i]))
            if i < seg - j - 1:
                bm.faces.new((grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
    p.add(bm, 'sail', smooth=True, keys=lambda f: ('sail_mid', False) if abs(f.calc_center_median().x) < 0.03 else None)


def _fg_lateen(p, yard, ang, drop, billow, device, seg=24):
    """A lateen mizzen in local space (the yard crossing the masthead at the
    origin, its forward end low), the sail to a clew aft and low, bellying +x,
    the civ device in its middle."""
    a = math.radians(ang)
    fwd = (0.0, math.cos(a) * yard * 0.45, -math.sin(a) * yard * 0.45)
    aft = (0.0, -math.cos(a) * yard * 0.55, math.sin(a) * yard * 0.55)
    clew = (0.0, aft[1] * 0.45, -drop)
    bm = props.bmesh.new()
    grid = []
    for j in range(seg + 1):
        v = j / seg
        row = []
        for i in range(seg - j + 1):
            u = i / seg
            w0 = 1 - u - v
            pt = [fwd[k] * w0 + aft[k] * u + clew[k] * v for k in range(3)]
            pt[0] += billow * 4 * w0 * u * (1 - v) ** 0.5 + billow * 2.0 * v * (w0 + u) * 0.5
            row.append(bm.verts.new(pt))
        grid.append(row)
    for j in range(seg):
        for i in range(seg - j):
            bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i]))
            if i < seg - j - 1:
                bm.faces.new((grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
    mask = EMBLEMS.get(device)
    cy = (fwd[1] + aft[1] + clew[1]) / 3
    cz = (fwd[2] + aft[2] + clew[2]) / 3

    def keys(f):
        c = f.calc_center_median()
        if mask and mask(-(c.y - cy), c.z - cz, drop * 0.6):
            return ('tint', True)
        if abs((c.y / 0.11) % 1.0 - 0.5) > 0.44:
            return ('sail_seam', False)
        return ('sail_mid', False) if abs(c.x) < billow * 0.5 else None

    p.add(bm, 'sail', smooth=True, keys=keys)
    vkit.rod(p, fwd, aft, 0.016, 'wood_dark', seg=8)


def _fg_castle(p, hull, y0, y1, h):
    """A raised castle between y0 and y1: solid painted sides following the hull,
    its deck on top, a dark band under a light rail cap (it reads as a tier at 1x)."""
    n = 10
    rings = []
    for i in range(n + 1):
        y = y0 + (y1 - y0) * i / n
        s = hull.s_of(y)
        b = hull.deck_half(s) * 0.98
        z0 = hull.deck_z(s) - 0.02
        rings.append([(b, y, z0), (b, y, z0 + h), (-b, y, z0 + h), (-b, y, z0)])
    p.add(props.loft(rings), 'castle_face', bevel=0.004,
          keys=lambda f: ('deck', False) if f.normal.z > 0.85 else None)
    for sx in (-1, 1):
        for dz, r, key in ((-0.025, 0.011, 'wale'), (0.012, 0.014, 'ash')):
            path = []
            for i in range(n + 1):
                y = y0 + (y1 - y0) * i / n
                s = hull.s_of(y)
                path.append((sx * (hull.deck_half(s) * 0.98 + 0.004), y, hull.deck_z(s) - 0.02 + h + dz))
            p.add(props.tube(path, r, seg=6, scale_xy=(1.0, 0.6)), key, smooth=True)


def _fg_forecastle(p, hull, y0, y1, h):
    """A forecastle tier from y0 to y1, which may lie past the stem: over the hull
    its sides follow the deck edge, past it they close to a point (a triangular
    stack overhanging the bow), standing on the stem-head's height."""
    yb = hull.L / 2
    n = 12
    rings = []
    zb = hull.deck_z(1.0)
    for i in range(n + 1):
        y = y0 + (y1 - y0) * i / n
        if y <= yb * 0.96:
            s = hull.s_of(y)
            b = hull.deck_half(s) * 0.98
            z0 = hull.deck_z(s) - 0.02
        else:
            t = (y - yb * 0.96) / max(y1 - yb * 0.96, 1e-6)
            b = max(0.02, hull.deck_half(hull.s_of(yb * 0.96)) * 0.98 * (1 - t))
            z0 = zb - 0.02 + 0.04 * t
        rings.append([(b, y, z0), (b, y, z0 + h), (-b, y, z0 + h), (-b, y, z0)])
    p.add(props.loft(rings), 'castle_face', bevel=0.004,
          keys=lambda f: ('deck', False) if f.normal.z > 0.85 else None)
    for sx in (-1, 1):
        path = [(sx * (r[0][0] + 0.004), r[0][1], r[1][2] + 0.012) for r in rings]
        p.add(props.tube(path, 0.013, seg=6, scale_xy=(1.0, 0.6)), 'ash', smooth=True)
        path = [(sx * (r[0][0] + 0.004), r[0][1], r[1][2] - 0.025) for r in rings]
        p.add(props.tube(path, 0.011, seg=6, scale_xy=(1.0, 0.6)), 'wale', smooth=True)


def _fg_shrouds(p, hull, y, z_top, legs=3, r=0.02):
    """Shrouds from a mast down to deadeyes on a channel at each rail (no ratlines:
    they break up at 2x)."""
    s = hull.s_of(y)
    zr = hull.deck_z(s) + 0.05
    for sx in (-1, 1):
        xb = sx * (hull.deck_half(s) + 0.035)
        for k in range(legs):
            dy = -0.06 - 0.09 * k
            b = (xb, y + dy, zr)
            vkit.rod(p, (0, y, z_top), b, r, 'cordage', seg=6)
            p.add(props.ellipsoid(0.022, 0.022, 0.026, seg=10), 'tar', T(*b), smooth=True)
        p.add(props.box(0.05, 0.36, 0.022), 'wood_dark', T(xb - sx * 0.01, y - 0.15, zr - 0.02), bevel=0.005)


def frigate_36(colors, **kw):
    """The realism pass's Frigate: a 36-gun frigate of c. 1780-1805: a long,
    low, flush black hull with an ochre band through one row of gunports
    (black lids), copper below the waterline, a low quarterdeck and
    forecastle, a galleried stern with gilt windows, a figurehead under a
    long bowsprit with two jibs; three square-rigged masts (courses,
    topsails, topgallants, braced, civ roundels on the courses), a spanker,
    dark tops and standing rigging; a civ pennant."""
    g = dict(FRIGATE, **kw)
    c = dict(SHIP_COLORS, **FRIGATE_COLORS)
    c.update(colors)
    root = vkit.root('Frigate')
    root['fit_scale'] = g['fit_scale']
    p = vkit.part(root, 'hull', c, SHIP_WEAR)
    hull = Hull(L=g['L'], B=g['B'], D=g['D'], draft=g['draft'], sheer_stern=g['sheer_stern'],
                sheer_bow=g['sheer_bow'], bow_p=1.4, stern_p=2.2, transom=0.4, rake_bow=0.3, fullness=1.0,
                tumblehome=0.14)
    yb = g['L'] / 2

    def deck(y):
        return hull.deck_z(hull.s_of(y))

    ss = [-1 + 2 * i / 48 for i in range(49)]
    bm = props.loft([hull.section(s, m=32) for s in ss])
    band = (0.17, 0.08)   # the ochre band: from deck - 0.17 to deck - 0.08 (the gun deck's ports)

    def hkeys(f):
        cc = f.calc_center_median()
        if f.normal.z > 0.85:
            return ('deck_alt', False) if int(abs(cc.x) / 0.03) % 2 else ('deck', False)
        if cc.z < 0.03:
            return ('copper', False)
        zd = hull.deck_z(hull.s_of(cc.y))
        if zd - band[0] < cc.z < zd - band[1]:
            return ('ochre', False)
        if int(math.floor(cc.z / 0.024)) % 2:
            return ('wood_alt', False)
        return None

    p.add(bm, 'wood', smooth=True, keys=hkeys)
    for sx in (-1, 1):
        for dz, r in ((band[0] + 0.012, 0.016), (band[1] - 0.01, 0.014)):   # black wales edging the band
            path = []
            for i in range(1, 60):
                s_ = -0.95 + 1.9 * i / 60
                z = hull.deck_z(s_) - dz
                path.append((sx * (hull.side_x(s_, z) + 0.004), hull.y(s_), z))
            p.add(props.tube(path, r, seg=6, scale_xy=(1.0, 0.45)), 'wale', smooth=True)
        bh = g['bulwark_h']
        ysb = [-yb * 0.95 + 1.9 * yb * i / 50 for i in range(51)]
        rings = []
        for y in ysb:
            s_ = hull.s_of(y)
            xo = sx * hull.deck_half(s_) * 0.995
            xi = xo - sx * 0.014
            z0 = deck(y) - 0.01
            rings.append([(xo, y, z0), (xo, y, z0 + bh), (xi, y, z0 + bh), (xi, y, z0)])
        p.add(props.loft(rings), 'wood', bevel=0.003)
        p.add(props.tube([(sx * (hull.deck_half(hull.s_of(y)) * 0.995 - sx * 0.007), y, deck(y) - 0.01 + bh + 0.006) for y in ysb],
                         0.014, seg=8), 'rail', smooth=True)   # a dark rail
    # the gunports through the band: black squares (the lids, the chequer)
    for k in range(g['ports']):
        y = -yb * 0.72 + 1.44 * yb * k / (g['ports'] - 1)
        s_ = hull.s_of(y)
        z = hull.deck_z(s_) - (band[0] + band[1]) / 2
        for sx in (-1, 1):
            x = sx * (hull.side_x(s_, z) + 0.004)
            p.add(props.box(0.014, 0.07, 0.066), 'port_lid', T(x, y, z), bevel=0.003)
    # a low quarterdeck and forecastle, hatches, the stern gallery, the figurehead and bowsprit
    for y0, y1, h in (g['quarterdeck'], g['forecastle']):
        _fg_castle(p, hull, y0 * yb, y1 * yb, h)
    for yy in (0.3, -0.18):
        p.add(props.box(0.2, 0.16, 0.02), 'rail', T(0, yy, deck(yy) + 0.008), bevel=0.003)
        p.add(props.box(0.16, 0.12, 0.022), 'tar', T(0, yy, deck(yy) + 0.01), bevel=0.002)
    yt = -yb * 0.985
    zt = deck(yt) + 0.02
    w = hull.deck_half(hull.s_of(yt)) * 1.6
    for i in range(5):
        x = -w / 2 + w * (i + 0.5) / 5
        p.add(props.box(w / 5 * 0.8, 0.02, 0.07), 'gold', T(x, yt - 0.012, zt), bevel=0.003)
        p.add(props.box(w / 5 * 0.55, 0.024, 0.05), 'lit', T(x, yt - 0.014, zt), bevel=0.002)   # lit stern lights
    p.add(props.box(w * 1.1, 0.06, 0.016), 'wood', T(0, yt - 0.035, zt - 0.06), bevel=0.004)
    p.add(props.box(w * 1.1, 0.012, 0.04), 'ochre', T(0, yt - 0.065, zt - 0.035), bevel=0.003)
    rud = props.slab([(0.0, -0.02), (-0.1, 0.0), (-0.12, deck(-yb) * 0.6), (0.0, deck(-yb) + 0.02)], 0.024, 'X')
    p.add(rud, 'wood', T(0, -yb - 0.01, 0), bevel=0.003)
    p.add(props.ellipsoid(0.05, 0.1, 0.085, seg=14), 'gold', T(0, yb + 0.06, deck(yb) - 0.06), smooth=True)   # figurehead, gilt
    bow_end = (0, yb + 0.72, deck(yb) + 0.34)
    vkit.rod(p, (0, yb * 0.9, deck(yb * 0.9) + 0.04), bow_end, 0.034, 'wood_dark', r1=0.02, seg=8)
    for sx in (-1, 1):   # head rails sweeping from the bow to the figurehead; quarter galleries
        p.add(props.tube([(sx * hull.deck_half(hull.s_of(yb * 0.9)) * 0.9, yb * 0.9, deck(yb * 0.9) - 0.03),
                          (sx * 0.05, yb + 0.0, deck(yb) - 0.02), (0, yb + 0.06, deck(yb) - 0.05)], 0.015, seg=6), 'gold', smooth=True)
        yq = -yb * 0.9
        xq = sx * (hull.deck_half(hull.s_of(yq)) + 0.02)
        p.add(props.box(0.05, 0.16, 0.12), 'wood', T(xq, yq, deck(yq) - 0.02), bevel=0.01)
        p.add(props.box(0.054, 0.1, 0.04), 'lit', T(xq, yq, deck(yq)), bevel=0.004)
    # the waist: a ship's boat on the hatches, a capstan, carronades at the bulwarks
    p.add(props.ellipsoid(0.08, 0.26, 0.05, seg=16), 'wood_dark', T(0, 0.42, deck(0.42) + 0.05), smooth=True)
    p.add(props.ellipsoid(0.06, 0.22, 0.02, seg=16), 'deck', T(0, 0.42, deck(0.42) + 0.085), smooth=True)
    p.add(props.lathe([(0.0, 0.05), (0.04, 0.035), (0.08, 0.055), (0.09, 0.0)], seg=12), 'wood_dark', T(0, -0.45, deck(-0.45)), smooth=True)
    for k in range(4):
        yk = -0.3 + 0.24 * k
        for sx in (-1, 1):
            xk = sx * (hull.deck_half(hull.s_of(yk)) - 0.07)
            p.add(props.lathe([(0.0, 0.026), (0.1, 0.02), (0.11, 0.024)], seg=10), 'port_lid', T(xk, yk, deck(yk) + 0.05) @ R('Y', sx * 90), smooth=True)

    # masts (tarred caps, dark tops) and their rigging
    ms = [dict(m) for m in g['masts']]
    for m in ms:
        hs = sum(h for _, h in m['sails']) + 0.01 * (len(m['sails']) - 1)
        m['_head'] = m['top'] + hs - m['sails'][0][1] + 0.08
        vkit.rod(p, (0, m['y'], deck(m['y']) - 0.05), (0, m['y'], m['_head']), m['r'], 'wood_dark', r1=m['r'] * 0.6, seg=10)
        p.add(props.lathe([(0.0, 0.02), (0.025, 0.024), (0.04, 0.0)], seg=12), 'tar', T(0, m['y'], m['_head']), smooth=True)
        p.add(props.lathe([(0.0, 0.06), (0.04, 0.12), (0.08, 0.125), (0.085, 0.0)], seg=16), 'tar',
              T(0, m['y'], m['top'] + 0.02), smooth=True)
    for m in ms:   # long civ pennants at every masthead
        _pennant_fg(p, (0, m['y'], m['_head'] - 0.05), *g['pennant'])
    if g['rigging']:
        for m in ms:
            _fg_shrouds(p, hull, m['y'], m['top'] - 0.02, legs=3 if m is not ms[2] else 2)
        r = g['stay_r']
        vkit.rod(p, (0, ms[0]['y'], ms[0]['top'] - 0.02), bow_end, r, 'cordage', seg=6)   # forestay
        vkit.rod(p, (0, ms[1]['y'], ms[1]['top'] - 0.02), (0, ms[0]['y'], deck(ms[0]['y']) + 0.12), r, 'cordage', seg=6)
    p.finish()
    # the square sails, braced; the courses carry the civ roundel; the spanker and two jibs
    for k, m in enumerate(ms):
        z_top = m['top']
        for j, (w, h) in enumerate(m['sails']):
            if j:
                z_top += h + 0.01   # the sails close up
            if k == 2 and j == 0:
                continue   # the mizzen course is the spanker (a crossjack would hide it)
            r = vkit.part(root, 'sail', c, SHIP_WEAR)
            emb = g['device'] if j < 2 and k < 2 else None
            _square_sail(r, w, h, z_top, g['billow'], emb, bands=1.5, brails=0,
                         line_r=0.009, seed=10 * k + j)
            o = r.finish()
            o.location = (0, m['y'] + 0.02, 0)
            o.rotation_euler = (0, 0, math.radians(g['brace']))
    # the four-sided gaff spanker: luff on the mizzen, gaff peaked aft, the boom ending at the taffrail
    gaff, drop = ms[2]['spanker']
    zm0, ym = ms[2]['top'] + 0.05, ms[2]['y']
    tl, bl = (0, ym - 0.03, zm0), (0, ym - 0.03, zm0 - drop)
    boom_end = (0, -yb * 0.98, zm0 - drop - 0.02)
    peak = (0, ym - gaff, zm0 + gaff * 0.62)   # a raked gaff, its peak rising
    r = vkit.part(root, 'spanker', c, SHIP_WEAR)
    bm = props.bmesh.new()
    n = 12
    grid = []
    for j in range(n + 1):
        v = j / n
        row = []
        for i in range(n + 1):
            u = i / n
            pt = [(tl[k] * (1 - u) + peak[k] * u) * (1 - v) + (bl[k] * (1 - u) + boom_end[k] * u) * v for k in range(3)]
            pt[0] += 0.08 * 4 * u * (1 - u) * (0.4 + 0.6 * math.sin(math.pi * v))
            row.append(bm.verts.new(pt))
        grid.append(row)
    for j in range(n):
        for i in range(n):
            bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
    r.add(bm, 'sail_mid', smooth=True, keys=lambda f: ('sail_shade', False) if abs(f.calc_center_median().x) > 0.05 else None)   # slightly darker canvas
    vkit.rod(r, tl, peak, 0.016, 'wood_dark', seg=8)
    vkit.rod(r, bl, boom_end, 0.024, 'wood_dark', seg=8)
    r.finish()
    q = vkit.part(root, 'jibs', c, SHIP_WEAR)
    fhead = (0, ms[0]['y'], ms[0]['top'] + 0.15)
    for t, cl in ((1.0, (0.0, yb * 0.95, deck(yb) + 0.22)), (0.7, (0.0, yb * 0.85, deck(yb) + 0.18))):
        tack = (0, yb * 0.9 + (bow_end[1] - yb * 0.9) * t, deck(yb * 0.9) + 0.04 + (bow_end[2] - deck(yb * 0.9) - 0.04) * t - 0.02)
        _fg_jib(q, fhead, tack, cl)
    q.finish()
    return root


def _pennant_fg(p, top, w, h, phase=0.0):
    """A civ pennant streaming aft, keeping 40 % of its height at the tip."""
    n = 10
    bm = props.bmesh.new()
    rows = []
    for j in range(n + 1):
        t = j / n
        hh = h * (1 - 0.6 * t)
        rows.append([bm.verts.new((0.06 * t * math.sin(5 * t + phase), top[1] - w * t, top[2] - hh * k)) for k in (0.0, 1.0)])
    for j in range(n):
        bm.faces.new((rows[j][0], rows[j + 1][0], rows[j + 1][1], rows[j][1]))
    p.add(bm, 'tint', smooth=True, tint=True)


# ------------------------------------------------------------ privateer (realism pass: an armed brig, c. 1780-1812)

# A privateer brig of c. 1780-1812 (notes/realism/Privateer.md): ~27 x 8 m (L:B ~3.4), a sleek, low,
# raked dark hull with a buff band through a row of black ports, two masts square-rigged (courses,
# topsails, topgallants) and a gaff mainsail, a long bowsprit and jib-boom with jibs. L 2.6 = 27 m.
PRIVATEER = dict(
    L=2.6, B=0.66, D=0.22, draft=0.14, sheer_stern=0.16, sheer_bow=0.2,
    masts=(dict(y=0.6, top=1.12, sails=((1.08, 0.48), (0.86, 0.42), (0.58, 0.25)), r=0.03),
           dict(y=-0.2, top=1.24, sails=((1.16, 0.5), (0.94, 0.44), (0.64, 0.27)), r=0.034, spanker=(0.6, 0.5))),
    billow=0.24, device='skull_pv', brace=33.0, pennant=(0.46, 0.17), bulwark_h=0.1, ports=9,
    quarterdeck=(-0.98, -0.62, 0.05), forecastle=(0.72, 0.98, 0.04), rigging=True, stay_r=0.016,
    flag=(0.48, 0.34),
    fit_scale=0.991, rig_scale=0.82,
)

PRIVATEER_COLORS = {
    'wood': '#5e4228', 'wood_alt': '#563c24', 'wood_low': '#46301c', 'wood_low_alt': '#3e2a18',
    'ochre': '#b8a070', 'wood_dark': '#2a2018', 'wale': '#7a5a38', 'tar': '#2a2620', 'ash': '#b8945e',
    'rail': '#2a1e14', 'deck': '#6a5030', 'deck_alt': '#60482a', 'lit': '#e0b860', 'castle_face': '#24201c', 'glass': '#1a1610',
    'sail': '#e2d3ae', 'sail_seam': '#c4b48e', 'sail_patch': '#d0c09a', 'sail_mid': '#cbbb95', 'sail_shade': '#bba982',
    'brail': '#54442f', 'gold': '#c8a048', 'cordage': '#2e2418', 'port_lid': '#141210', 'copper': '#6e7a5e',
}
for _k, _v in (('ochre', ('#b88c48', 0.6, 0.0, 'wood')), ('port_lid', ('#141210', 0.6, 0.0, 'wood')),
               ('copper', ('#6e7a5e', 0.45, 1.0, 'metal')), ('castle_face', ('#8a6436', 0.65, 0.0, 'wood')),
               ('ash', ('#b8945e', 0.6, 0.0, 'wood')), ('wood_low', ('#7e5e38', 0.62, 0.0, 'wood')),
               ('wood_low_alt', ('#735432', 0.62, 0.0, 'wood')), ('wale', ('#2a2018', 0.62, 0.0, 'wood')),
               ('tar', ('#2e261e', 0.7, 0.0, 'wood')), ('deck_alt', ('#907042', 0.7, 0.0, 'wood')),
               ('brail', ('#54442f', 0.85, 0.0, None)), ('glass', ('#1a1610', 0.3, 0.0, None)),
               ('sail_mid', ('#cbbb95', 0.9, 0.0, 'cloth')), ('lit', ('#e0b860', 0.5, 0.0, None)), ('sail_shade', ('#bba982', 0.9, 0.0, 'cloth'))):
    props.DEFAULTS.setdefault(_k, _v)


EMBLEMS['skull_pv'] = lambda x, z, h: _skull(x, z, h * 1.3)   # the death's head, inside the sail


def _pv_sail(p, w, h, z_top, billow, emblem=None, inset=0.85, reefs=(0.2, 0.36), nu=36, nv=24):
    """A square sail by the fleet's sail-shape rule: straight edges along the
    yard, the leeches and the foot (the belly only in the middle of the cloth:
    billow * (1 - u^2) * sin(pi v), no twist), clean canvas, the emblem in the
    flatter centre (scaled by `inset`), two faint reef bands; the yard, with
    tarred arms. Local: the mast on the z axis, the yard along x at z_top."""
    def surf(u, v):
        return (u * w / 2, 0.03 + billow * (1 - u * u) * math.sin(math.pi * v), z_top - v * h)

    bm = props.bmesh.new()
    verts = [[bm.verts.new(surf(-1 + 2 * i / nu, j / nv)) for i in range(nu + 1)] for j in range(nv + 1)]
    for j in range(nv):
        for i in range(nu):
            bm.faces.new((verts[j][i], verts[j][i + 1], verts[j + 1][i + 1], verts[j + 1][i]))
    mask = EMBLEMS.get(emblem)

    def keys(f):
        c = f.calc_center_median()
        v = (z_top - c.z) / h
        if mask and mask(c.x / inset, (c.z - (z_top - h / 2)) / inset, h):
            return ('tint', True)
        if any(abs(v - r) < 0.018 for r in reefs):
            return ('sail_seam', False)
        if abs(c.x) > w * 0.36 or v > 0.82:
            return ('sail_mid', False)   # a calm fall-off toward the leeches and the foot
        return None

    p.add(bm, 'sail', smooth=True, keys=keys)
    vkit.rod(p, (-w / 2 - 0.04, 0.03, z_top + 0.012), (w / 2 + 0.04, 0.03, z_top + 0.012), 0.016, 'wood_dark', seg=8)
    for sx in (-1, 1):
        vkit.rod(p, (sx * (w / 2 - 0.02), 0.03, z_top + 0.012), (sx * (w / 2 + 0.05), 0.03, z_top + 0.012), 0.018, 'tar', seg=8)


def _pv_jib(p, head, tack, clew, seg=12):
    """A triangular headsail (no stay rod: a 1 px line breaks up)."""
    bm = props.bmesh.new()
    grid = []
    for j in range(seg + 1):
        v = j / seg
        row = []
        for i in range(seg - j + 1):
            u = i / seg
            w0 = 1 - u - v
            pt = [head[k] * w0 + tack[k] * u + clew[k] * v for k in range(3)]
            pt[0] += 0.06 * 4 * w0 * u
            row.append(bm.verts.new(pt))
        grid.append(row)
    for j in range(seg):
        for i in range(seg - j):
            bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i]))
            if i < seg - j - 1:
                bm.faces.new((grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
    p.add(bm, 'sail', smooth=True, keys=lambda f: ('sail_mid', False) if abs(f.calc_center_median().x) < 0.03 else None)


def _pv_lateen(p, yard, ang, drop, billow, device, seg=24):
    """A lateen mizzen in local space (the yard crossing the masthead at the
    origin, its forward end low), the sail to a clew aft and low, bellying +x,
    the civ device in its middle."""
    a = math.radians(ang)
    fwd = (0.0, math.cos(a) * yard * 0.45, -math.sin(a) * yard * 0.45)
    aft = (0.0, -math.cos(a) * yard * 0.55, math.sin(a) * yard * 0.55)
    clew = (0.0, aft[1] * 0.45, -drop)
    bm = props.bmesh.new()
    grid = []
    for j in range(seg + 1):
        v = j / seg
        row = []
        for i in range(seg - j + 1):
            u = i / seg
            w0 = 1 - u - v
            pt = [fwd[k] * w0 + aft[k] * u + clew[k] * v for k in range(3)]
            pt[0] += billow * 4 * w0 * u * (1 - v) ** 0.5 + billow * 2.0 * v * (w0 + u) * 0.5
            row.append(bm.verts.new(pt))
        grid.append(row)
    for j in range(seg):
        for i in range(seg - j):
            bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i]))
            if i < seg - j - 1:
                bm.faces.new((grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
    mask = EMBLEMS.get(device)
    cy = (fwd[1] + aft[1] + clew[1]) / 3
    cz = (fwd[2] + aft[2] + clew[2]) / 3

    def keys(f):
        c = f.calc_center_median()
        if mask and mask(-(c.y - cy), c.z - cz, drop * 0.6):
            return ('tint', True)
        if abs((c.y / 0.11) % 1.0 - 0.5) > 0.44:
            return ('sail_seam', False)
        return ('sail_mid', False) if abs(c.x) < billow * 0.5 else None

    p.add(bm, 'sail', smooth=True, keys=keys)
    vkit.rod(p, fwd, aft, 0.016, 'wood_dark', seg=8)


def _pv_castle(p, hull, y0, y1, h):
    """A raised castle between y0 and y1: solid painted sides following the hull,
    its deck on top, a dark band under a light rail cap (it reads as a tier at 1x)."""
    n = 10
    rings = []
    for i in range(n + 1):
        y = y0 + (y1 - y0) * i / n
        s = hull.s_of(y)
        b = hull.deck_half(s) * 0.98
        z0 = hull.deck_z(s) - 0.02
        rings.append([(b, y, z0), (b, y, z0 + h), (-b, y, z0 + h), (-b, y, z0)])
    p.add(props.loft(rings), 'castle_face', bevel=0.004,
          keys=lambda f: ('deck', False) if f.normal.z > 0.85 else None)
    for sx in (-1, 1):
        for dz, r, key in ((-0.025, 0.011, 'wale'), (0.012, 0.014, 'ash')):
            path = []
            for i in range(n + 1):
                y = y0 + (y1 - y0) * i / n
                s = hull.s_of(y)
                path.append((sx * (hull.deck_half(s) * 0.98 + 0.004), y, hull.deck_z(s) - 0.02 + h + dz))
            p.add(props.tube(path, r, seg=6, scale_xy=(1.0, 0.6)), key, smooth=True)


def _pv_forecastle(p, hull, y0, y1, h):
    """A forecastle tier from y0 to y1, which may lie past the stem: over the hull
    its sides follow the deck edge, past it they close to a point (a triangular
    stack overhanging the bow), standing on the stem-head's height."""
    yb = hull.L / 2
    n = 12
    rings = []
    zb = hull.deck_z(1.0)
    for i in range(n + 1):
        y = y0 + (y1 - y0) * i / n
        if y <= yb * 0.96:
            s = hull.s_of(y)
            b = hull.deck_half(s) * 0.98
            z0 = hull.deck_z(s) - 0.02
        else:
            t = (y - yb * 0.96) / max(y1 - yb * 0.96, 1e-6)
            b = max(0.02, hull.deck_half(hull.s_of(yb * 0.96)) * 0.98 * (1 - t))
            z0 = zb - 0.02 + 0.04 * t
        rings.append([(b, y, z0), (b, y, z0 + h), (-b, y, z0 + h), (-b, y, z0)])
    p.add(props.loft(rings), 'castle_face', bevel=0.004,
          keys=lambda f: ('deck', False) if f.normal.z > 0.85 else None)
    for sx in (-1, 1):
        path = [(sx * (r[0][0] + 0.004), r[0][1], r[1][2] + 0.012) for r in rings]
        p.add(props.tube(path, 0.013, seg=6, scale_xy=(1.0, 0.6)), 'ash', smooth=True)
        path = [(sx * (r[0][0] + 0.004), r[0][1], r[1][2] - 0.025) for r in rings]
        p.add(props.tube(path, 0.011, seg=6, scale_xy=(1.0, 0.6)), 'wale', smooth=True)


def _pv_shrouds(p, hull, y, z_top, legs=3, r=0.02):
    """Shrouds from a mast down to deadeyes on a channel at each rail (no ratlines:
    they break up at 2x)."""
    s = hull.s_of(y)
    zr = hull.deck_z(s) + 0.05
    for sx in (-1, 1):
        xb = sx * (hull.deck_half(s) + 0.035)
        for k in range(legs):
            dy = -0.06 - 0.09 * k
            b = (xb, y + dy, zr)
            vkit.rod(p, (0, y, z_top), b, r, 'cordage', seg=6)
            p.add(props.ellipsoid(0.022, 0.022, 0.026, seg=10), 'tar', T(*b), smooth=True)
        p.add(props.box(0.05, 0.36, 0.022), 'wood_dark', T(xb - sx * 0.01, y - 0.15, zr - 0.02), bevel=0.005)


def privateer_brig(colors, **kw):
    """The realism pass's Privateer: an armed brig of c. 1780-1812: a sleek,
    low dark hull with a buff band through one row of gunports
    (black lids), copper below the waterline, a low quarterdeck and
    forecastle, a galleried stern with gilt windows, a figurehead under a
    long bowsprit with two jibs; three square-rigged masts (courses,
    topsails, topgallants, braced, civ roundels on the courses), a spanker,
    dark tops and standing rigging; a civ pennant."""
    g = dict(PRIVATEER, **kw)
    c = dict(SHIP_COLORS, **PRIVATEER_COLORS)
    c.update(colors)
    root = vkit.root('Privateer')
    root['fit_scale'] = g['fit_scale']
    p = vkit.part(root, 'hull', c, SHIP_WEAR)
    hull = Hull(L=g['L'], B=g['B'], D=g['D'], draft=g['draft'], sheer_stern=g['sheer_stern'],
                sheer_bow=g['sheer_bow'], bow_p=1.2, stern_p=2.2, transom=0.4, rake_bow=0.45, fullness=1.0,
                tumblehome=0.14)
    yb = g['L'] / 2

    def deck(y):
        return hull.deck_z(hull.s_of(y))

    ss = [-1 + 2 * i / 48 for i in range(49)]
    bm = props.loft([hull.section(s, m=32) for s in ss])
    band = (0.17, 0.08)   # the ochre band: from deck - 0.17 to deck - 0.08 (the gun deck's ports)

    def hkeys(f):
        cc = f.calc_center_median()
        if f.normal.z > 0.85:
            return ('deck_alt', False) if int(abs(cc.x) / 0.03) % 2 else ('deck', False)
        if cc.z < 0.03:
            return ('copper', False)
        zd = hull.deck_z(hull.s_of(cc.y))
        if zd - band[0] < cc.z < zd - band[1]:
            return ('ochre', False)
        if int(math.floor(cc.z / 0.024)) % 2:
            return ('wood_alt', False)
        return None

    p.add(bm, 'wood', smooth=True, keys=hkeys)
    for sx in (-1, 1):
        for dz, r in ((band[0] + 0.012, 0.016), (band[1] - 0.01, 0.014)):   # black wales edging the band
            path = []
            for i in range(1, 60):
                s_ = -0.95 + 1.9 * i / 60
                z = hull.deck_z(s_) - dz
                path.append((sx * (hull.side_x(s_, z) + 0.004), hull.y(s_), z))
            p.add(props.tube(path, r, seg=6, scale_xy=(1.0, 0.45)), 'wale', smooth=True)
        bh = g['bulwark_h']
        ysb = [-yb * 0.95 + 1.9 * yb * i / 50 for i in range(51)]
        rings = []
        for y in ysb:
            s_ = hull.s_of(y)
            xo = sx * hull.deck_half(s_) * 0.995
            xi = xo - sx * 0.014
            z0 = deck(y) - 0.01
            rings.append([(xo, y, z0), (xo, y, z0 + bh), (xi, y, z0 + bh), (xi, y, z0)])
        p.add(props.loft(rings), 'wood', bevel=0.003)
        p.add(props.tube([(sx * (hull.deck_half(hull.s_of(y)) * 0.995 - sx * 0.007), y, deck(y) - 0.01 + bh + 0.006) for y in ysb],
                         0.014, seg=8), 'rail', smooth=True)   # a dark rail
    # the gunports through the band: black squares (the lids, the chequer)
    for k in range(g['ports']):
        y = -yb * 0.72 + 1.44 * yb * k / (g['ports'] - 1)
        s_ = hull.s_of(y)
        z = hull.deck_z(s_) - (band[0] + band[1]) / 2
        for sx in (-1, 1):
            x = sx * (hull.side_x(s_, z) + 0.004)
            p.add(props.box(0.014, 0.07, 0.066), 'port_lid', T(x, y, z), bevel=0.003)
            p.add(props.lathe([(0.0, 0.016), (0.05, 0.013), (0.06, 0.017)], seg=10), 'gunmetal', T(x, y, z) @ R('Y', sx * 90), smooth=True)   # the gun's muzzle
    # a low quarterdeck and forecastle, hatches, the stern gallery, the figurehead and bowsprit
    for y0, y1, h in (g['quarterdeck'], g['forecastle']):
        _pv_castle(p, hull, y0 * yb, y1 * yb, h)
    for yy in (0.3, -0.18):
        p.add(props.box(0.2, 0.16, 0.02), 'rail', T(0, yy, deck(yy) + 0.008), bevel=0.003)
        p.add(props.box(0.16, 0.12, 0.022), 'tar', T(0, yy, deck(yy) + 0.01), bevel=0.002)
    yt = -yb * 0.985
    zt = deck(yt) + 0.02
    w = hull.deck_half(hull.s_of(yt)) * 1.6
    for i in range(5):
        x = -w / 2 + w * (i + 0.5) / 5
        p.add(props.box(w / 5 * 0.8, 0.02, 0.07), 'gold', T(x, yt - 0.012, zt), bevel=0.003)
        p.add(props.box(w / 5 * 0.55, 0.024, 0.05), 'lit', T(x, yt - 0.014, zt), bevel=0.002)   # lit stern lights
    p.add(props.box(w * 1.1, 0.06, 0.016), 'wood', T(0, yt - 0.035, zt - 0.06), bevel=0.004)
    p.add(props.box(w * 1.1, 0.012, 0.04), 'ochre', T(0, yt - 0.065, zt - 0.035), bevel=0.003)
    rud = props.slab([(0.0, -0.02), (-0.1, 0.0), (-0.12, deck(-yb) * 0.6), (0.0, deck(-yb) + 0.02)], 0.024, 'X')
    p.add(rud, 'wood', T(0, -yb - 0.01, 0), bevel=0.003)
    p.add(props.ellipsoid(0.035, 0.07, 0.06, seg=12), 'gold', T(0, yb + 0.05, deck(yb) - 0.06), smooth=True)   # figurehead
    bow_end = (0, yb + 0.72, deck(yb) + 0.34)
    vkit.rod(p, (0, yb * 0.9, deck(yb * 0.9) + 0.04), bow_end, 0.034, 'wood_dark', r1=0.02, seg=8)
    for sx in (-1, 1):   # head rails sweeping from the bow to the figurehead; quarter galleries
        p.add(props.tube([(sx * hull.deck_half(hull.s_of(yb * 0.9)) * 0.9, yb * 0.9, deck(yb * 0.9) - 0.03),
                          (sx * 0.05, yb + 0.0, deck(yb) - 0.02), (0, yb + 0.06, deck(yb) - 0.05)], 0.011, seg=6), 'ochre', smooth=True)
        yq = -yb * 0.9
        xq = sx * (hull.deck_half(hull.s_of(yq)) + 0.02)
        p.add(props.box(0.05, 0.16, 0.12), 'wood', T(xq, yq, deck(yq) - 0.02), bevel=0.01)
        p.add(props.box(0.054, 0.1, 0.04), 'lit', T(xq, yq, deck(yq)), bevel=0.004)
    # the waist: a ship's boat on the hatches, a capstan, carronades at the bulwarks
    p.add(props.ellipsoid(0.08, 0.26, 0.05, seg=16), 'wood_dark', T(0, 0.42, deck(0.42) + 0.05), smooth=True)
    p.add(props.ellipsoid(0.06, 0.22, 0.02, seg=16), 'deck', T(0, 0.42, deck(0.42) + 0.085), smooth=True)
    p.add(props.lathe([(0.0, 0.05), (0.04, 0.035), (0.08, 0.055), (0.09, 0.0)], seg=12), 'wood_dark', T(0, -0.45, deck(-0.45)), smooth=True)
    for k in range(4):
        yk = -0.3 + 0.24 * k
        for sx in (-1, 1):
            xk = sx * (hull.deck_half(hull.s_of(yk)) - 0.07)
            p.add(props.lathe([(0.0, 0.026), (0.1, 0.02), (0.11, 0.024)], seg=10), 'port_lid', T(xk, yk, deck(yk) + 0.05) @ R('Y', sx * 90), smooth=True)

    # masts (tarred caps, dark tops) and their rigging
    ms = [dict(m) for m in g['masts']]
    k = g.get('rig_scale', 1.0)   # a shorter rig: masts, yards and sails scaled about each mast's foot (1.0 = as reviewed)
    if k != 1.0:
        for m in ms:
            d0 = deck(m['y'])
            m['top'] = d0 + k * (m['top'] - d0)
            m['sails'] = tuple((w * k, h * k) for w, h in m['sails'])
            if 'spanker' in m:
                m['spanker'] = tuple(v * k for v in m['spanker'])
    for m in ms:
        hs = sum(h for _, h in m['sails']) + 0.01 * (len(m['sails']) - 1)
        m['_head'] = m['top'] + hs - m['sails'][0][1] + 0.08
        vkit.rod(p, (0, m['y'], deck(m['y']) - 0.05), (0, m['y'], m['_head']), m['r'], 'wood_dark', r1=m['r'] * 0.6, seg=10)
        p.add(props.lathe([(0.0, 0.02), (0.025, 0.024), (0.04, 0.0)], seg=12), 'tar', T(0, m['y'], m['_head']), smooth=True)
        p.add(props.lathe([(0.0, 0.06), (0.04, 0.12), (0.08, 0.125), (0.085, 0.0)], seg=16), 'tar',
              T(0, m['y'], m['top'] + 0.02), smooth=True)
    _pennant_pv(p, (0, ms[0]['y'], ms[0]['_head'] - 0.03), *g['pennant'])
    fw, fh = g['flag']
    top = (0, ms[1]['y'], ms[1]['_head'] - 0.02)
    bm = props.bmesh.new()
    rows = []
    for jj in range(13):
        t = jj / 12
        rows.append([bm.verts.new((-0.7 * fw * t + 0.04 * math.sin(4 * t), top[1] - 0.7 * fw * t, top[2] - fh * ii / 8)) for ii in range(9)])   # streaming out at 45 deg: a face in 2, 6, 7
    for jj in range(12):
        for ii in range(8):
            bm.faces.new((rows[jj][ii], rows[jj + 1][ii], rows[jj + 1][ii + 1], rows[jj][ii + 1]))
    p.add(bm, 'tint', smooth=True, tint=True,
          keys=lambda f: ('sail', False) if _skull(fw / 2 - (top[1] - f.calc_center_median().y) / 0.7,
                                                   f.calc_center_median().z - (top[2] - fh / 2), fh * 0.9) else None)
    if g['rigging']:
        for m in ms:
            _pv_shrouds(p, hull, m['y'], m['top'] - 0.02, legs=3)
        r = g['stay_r']
        vkit.rod(p, (0, ms[0]['y'], ms[0]['top'] - 0.02), bow_end, r, 'cordage', seg=6)   # forestay
        vkit.rod(p, (0, ms[1]['y'], ms[1]['top'] - 0.02), (0, ms[0]['y'], deck(ms[0]['y']) + 0.12), r, 'cordage', seg=6)
    p.finish()
    # the square sails, braced; the courses carry the civ roundel; the spanker and two jibs
    for k, m in enumerate(ms):
        z_top = m['top']
        for j, (w, h) in enumerate(m['sails']):
            if j:
                z_top += h + 0.01   # the sails close up
            r = vkit.part(root, 'sail', c, SHIP_WEAR)
            emb = g['device'] if j == 0 else None   # one big skull on each course only
            _pv_sail(r, w, h, z_top, g['billow'], emb, inset=1.0)
            o = r.finish()
            o.location = (0, m['y'] + 0.02, 0)
            o.rotation_euler = (0, 0, math.radians(g['brace']))
    # the four-sided gaff spanker: luff on the mizzen, gaff peaked aft, the boom ending at the taffrail
    gaff, drop = ms[1]['spanker']   # the brig's gaff mainsail abaft the mainmast
    zm0, ym = ms[1]['top'] - 0.1, ms[1]['y']
    tl, bl = (0, ym - 0.03, zm0), (0, ym - 0.03, zm0 - drop)
    boom_end = (0, -yb * 0.98, zm0 - drop - 0.02)
    peak = (0, ym - gaff, zm0 + gaff * 0.45)
    r = vkit.part(root, 'spanker', c, SHIP_WEAR)
    bm = props.bmesh.new()
    n = 12
    grid = []
    for j in range(n + 1):
        v = j / n
        row = []
        for i in range(n + 1):
            u = i / n
            pt = [(tl[k] * (1 - u) + peak[k] * u) * (1 - v) + (bl[k] * (1 - u) + boom_end[k] * u) * v for k in range(3)]
            pt[0] += 0.08 * 4 * u * (1 - u) * (0.4 + 0.6 * math.sin(math.pi * v))
            row.append(bm.verts.new(pt))
        grid.append(row)
    for j in range(n):
        for i in range(n):
            bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
    r.add(bm, 'sail', smooth=True, keys=lambda f: ('sail_mid', False) if abs(f.calc_center_median().x) > 0.05 else None)
    vkit.rod(r, tl, peak, 0.016, 'wood_dark', seg=8)
    vkit.rod(r, bl, boom_end, 0.018, 'wood_dark', seg=8)
    r.finish()
    q = vkit.part(root, 'jibs', c, SHIP_WEAR)
    fhead = (0, ms[0]['y'], ms[0]['top'] + 0.15)
    for t, cl in ((1.0, (0.0, yb * 0.95, deck(yb) + 0.22)), (0.7, (0.0, yb * 0.85, deck(yb) + 0.18))):
        tack = (0, yb * 0.9 + (bow_end[1] - yb * 0.9) * t, deck(yb * 0.9) + 0.04 + (bow_end[2] - deck(yb * 0.9) - 0.04) * t - 0.02)
        _pv_jib(q, fhead, tack, cl)
    q.finish()
    return root


def _pennant_pv(p, top, w, h, phase=0.0):
    """A civ pennant streaming aft, keeping 40 % of its height at the tip."""
    n = 10
    bm = props.bmesh.new()
    rows = []
    for j in range(n + 1):
        t = j / n
        hh = h * (1 - 0.6 * t)
        rows.append([bm.verts.new((0.06 * t * math.sin(5 * t + phase), top[1] - w * t, top[2] - hh * k)) for k in (0.0, 1.0)])
    for j in range(n):
        bm.faces.new((rows[j][0], rows[j + 1][0], rows[j + 1][1], rows[j][1]))
    p.add(bm, 'tint', smooth=True, tint=True)


# ------------------------------------------------------------ man-o-war (realism pass: a 74-gun ship of the line)

# A 74-gun third-rate ship of the line of c. 1790-1815 (notes/realism/Man-O-War.md): ~53 x 14.5 m (L:B
# ~3.6), a tall two-decker, dark brown with two ochre bands through two rows of black gunports, a
# galleried, gilt stern, a gilt figurehead; three masts with courses, topsails and topgallants, a
# spanker, jibs. L 3.2 = 53 m.
MOW = dict(
    L=3.2, B=0.88, D=0.44, draft=0.17, sheer_stern=0.22, sheer_bow=0.16,
    masts=(dict(y=0.9, top=1.66, sails=((1.26, 0.6), (1.02, 0.52), (0.7, 0.32)), r=0.036, brace=35.0),
           dict(y=0.06, top=1.86, sails=((1.56, 0.68), (1.26, 0.6), (0.86, 0.37)), r=0.042, brace=25.0),
           dict(y=-0.88, top=1.62, sails=((1.0, 0.46), (0.98, 0.48), (0.66, 0.32)), r=0.032, brace=30.0, spanker=(0.74, 0.62))),
    billow=0.24, device='cross_mw', brace=30.0, pennant=(0.46, 0.17), bulwark_h=0.1, ports=14,
    quarterdeck=(-0.98, -0.45, 0.08), forecastle=(0.62, 0.98, 0.07), rigging=True, stay_r=0.018,
    fit_scale=1.0005, rig_scale=0.78,
)

MOW_COLORS = {
    'wood': '#4a3422', 'wood_alt': '#42301e', 'wood_low': '#36261a', 'wood_low_alt': '#302216',
    'ochre': '#b88c48', 'wood_dark': '#2a2018', 'wale': '#6e5030', 'tar': '#2a2620', 'ash': '#b8945e',
    'rail': '#2a1e14', 'deck': '#6a5030', 'deck_alt': '#60482a', 'lit': '#e0b860', 'castle_face': '#24201c', 'glass': '#1a1610',
    'sail': '#d6c6a2', 'sail_seam': '#bcac86', 'sail_patch': '#d0c09a', 'sail_mid': '#cbbb95', 'sail_shade': '#bba982',
    'brail': '#54442f', 'gold': '#c8a048', 'cordage': '#1e1810', 'port_lid': '#141210', 'copper': '#6e7a5e',
}
for _k, _v in (('ochre', ('#b88c48', 0.6, 0.0, 'wood')), ('port_lid', ('#141210', 0.6, 0.0, 'wood')),
               ('copper', ('#6e7a5e', 0.45, 1.0, 'metal')), ('castle_face', ('#8a6436', 0.65, 0.0, 'wood')),
               ('ash', ('#b8945e', 0.6, 0.0, 'wood')), ('wood_low', ('#7e5e38', 0.62, 0.0, 'wood')),
               ('wood_low_alt', ('#735432', 0.62, 0.0, 'wood')), ('wale', ('#2a2018', 0.62, 0.0, 'wood')),
               ('tar', ('#2e261e', 0.7, 0.0, 'wood')), ('deck_alt', ('#907042', 0.7, 0.0, 'wood')),
               ('brail', ('#54442f', 0.85, 0.0, None)), ('glass', ('#1a1610', 0.3, 0.0, None)),
               ('sail_mid', ('#cbbb95', 0.9, 0.0, 'cloth')), ('lit', ('#e0b860', 0.5, 0.0, None)), ('sail_shade', ('#bba982', 0.9, 0.0, 'cloth'))):
    props.DEFAULTS.setdefault(_k, _v)


EMBLEMS['cross_mw'] = lambda x, z, h: (abs(x) < 0.15 * h and abs(z) < 0.42 * h) or (abs(z) < 0.15 * h and abs(x) < 0.46 * h)   # one clean cross


def _mw_sail(p, w, h, z_top, billow, emblem=None, inset=0.85, reefs=(0.2, 0.36), nu=72, nv=48):
    """A square sail by the fleet's sail-shape rule: straight edges along the
    yard, the leeches and the foot (the belly only in the middle of the cloth:
    billow * (1 - u^2) * sin(pi v), no twist), clean canvas, the emblem in the
    flatter centre (scaled by `inset`), two faint reef bands; the yard, with
    tarred arms. Local: the mast on the z axis, the yard along x at z_top."""
    def surf(u, v):
        return (u * w / 2, 0.03 + billow * (1 - u * u) * math.sin(math.pi * v), z_top - v * h)   # straight edges, the belly mid-cloth

    bm = props.bmesh.new()
    verts = [[bm.verts.new(surf(-1 + 2 * i / nu, j / nv)) for i in range(nu + 1)] for j in range(nv + 1)]
    for j in range(nv):
        for i in range(nu):
            bm.faces.new((verts[j][i], verts[j][i + 1], verts[j + 1][i + 1], verts[j + 1][i]))
    mask = EMBLEMS.get(emblem)

    def keys(f):
        c = f.calc_center_median()
        v = (z_top - c.z) / h
        if mask and mask(c.x / inset, (c.z - (z_top - h / 2)) / inset, h):
            return ('tint', True)
        if any(abs(v - r) < 0.018 for r in reefs):
            return ('sail_seam', False)
        if abs(c.x) > w * 0.36 or v > 0.82:
            return ('sail_mid', False)   # a calm fall-off toward the leeches and the foot
        return None

    p.add(bm, 'sail', smooth=True, keys=keys)
    vkit.rod(p, (-w / 2 - 0.04, 0.03, z_top + 0.012), (w / 2 + 0.04, 0.03, z_top + 0.012), 0.016, 'wood_dark', seg=8)
    for sx in (-1, 1):
        vkit.rod(p, (sx * (w / 2 - 0.02), 0.03, z_top + 0.012), (sx * (w / 2 + 0.05), 0.03, z_top + 0.012), 0.018, 'tar', seg=8)


def _mw_jib(p, head, tack, clew, seg=12):
    """A triangular headsail (no stay rod: a 1 px line breaks up)."""
    bm = props.bmesh.new()
    grid = []
    for j in range(seg + 1):
        v = j / seg
        row = []
        for i in range(seg - j + 1):
            u = i / seg
            w0 = 1 - u - v
            pt = [head[k] * w0 + tack[k] * u + clew[k] * v for k in range(3)]
            pt[0] += 0.06 * 4 * w0 * u
            row.append(bm.verts.new(pt))
        grid.append(row)
    for j in range(seg):
        for i in range(seg - j):
            bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i]))
            if i < seg - j - 1:
                bm.faces.new((grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
    p.add(bm, 'sail', smooth=True, keys=lambda f: ('sail_mid', False) if abs(f.calc_center_median().x) < 0.03 else None)


def _mw_lateen(p, yard, ang, drop, billow, device, seg=24):
    """A lateen mizzen in local space (the yard crossing the masthead at the
    origin, its forward end low), the sail to a clew aft and low, bellying +x,
    the civ device in its middle."""
    a = math.radians(ang)
    fwd = (0.0, math.cos(a) * yard * 0.45, -math.sin(a) * yard * 0.45)
    aft = (0.0, -math.cos(a) * yard * 0.55, math.sin(a) * yard * 0.55)
    clew = (0.0, aft[1] * 0.45, -drop)
    bm = props.bmesh.new()
    grid = []
    for j in range(seg + 1):
        v = j / seg
        row = []
        for i in range(seg - j + 1):
            u = i / seg
            w0 = 1 - u - v
            pt = [fwd[k] * w0 + aft[k] * u + clew[k] * v for k in range(3)]
            pt[0] += billow * 4 * w0 * u * (1 - v) ** 0.5 + billow * 2.0 * v * (w0 + u) * 0.5
            row.append(bm.verts.new(pt))
        grid.append(row)
    for j in range(seg):
        for i in range(seg - j):
            bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i]))
            if i < seg - j - 1:
                bm.faces.new((grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
    mask = EMBLEMS.get(device)
    cy = (fwd[1] + aft[1] + clew[1]) / 3
    cz = (fwd[2] + aft[2] + clew[2]) / 3

    def keys(f):
        c = f.calc_center_median()
        if mask and mask(-(c.y - cy), c.z - cz, drop * 0.6):
            return ('tint', True)
        if abs((c.y / 0.11) % 1.0 - 0.5) > 0.44:
            return ('sail_seam', False)
        return ('sail_mid', False) if abs(c.x) < billow * 0.5 else None

    p.add(bm, 'sail', smooth=True, keys=keys)
    vkit.rod(p, fwd, aft, 0.016, 'wood_dark', seg=8)


def _mw_castle(p, hull, y0, y1, h):
    """A raised castle between y0 and y1: solid painted sides following the hull,
    its deck on top, a dark band under a light rail cap (it reads as a tier at 1x)."""
    n = 10
    rings = []
    for i in range(n + 1):
        y = y0 + (y1 - y0) * i / n
        s = hull.s_of(y)
        b = hull.deck_half(s) * 0.98
        z0 = hull.deck_z(s) - 0.02
        rings.append([(b, y, z0), (b, y, z0 + h), (-b, y, z0 + h), (-b, y, z0)])
    p.add(props.loft(rings), 'castle_face', bevel=0.004,
          keys=lambda f: ('deck', False) if f.normal.z > 0.85 else None)
    for sx in (-1, 1):
        for dz, r, key in ((-0.025, 0.011, 'wale'), (0.012, 0.014, 'ash')):
            path = []
            for i in range(n + 1):
                y = y0 + (y1 - y0) * i / n
                s = hull.s_of(y)
                path.append((sx * (hull.deck_half(s) * 0.98 + 0.004), y, hull.deck_z(s) - 0.02 + h + dz))
            p.add(props.tube(path, r, seg=6, scale_xy=(1.0, 0.6)), key, smooth=True)


def _mw_forecastle(p, hull, y0, y1, h):
    """A forecastle tier from y0 to y1, which may lie past the stem: over the hull
    its sides follow the deck edge, past it they close to a point (a triangular
    stack overhanging the bow), standing on the stem-head's height."""
    yb = hull.L / 2
    n = 12
    rings = []
    zb = hull.deck_z(1.0)
    for i in range(n + 1):
        y = y0 + (y1 - y0) * i / n
        if y <= yb * 0.96:
            s = hull.s_of(y)
            b = hull.deck_half(s) * 0.98
            z0 = hull.deck_z(s) - 0.02
        else:
            t = (y - yb * 0.96) / max(y1 - yb * 0.96, 1e-6)
            b = max(0.02, hull.deck_half(hull.s_of(yb * 0.96)) * 0.98 * (1 - t))
            z0 = zb - 0.02 + 0.04 * t
        rings.append([(b, y, z0), (b, y, z0 + h), (-b, y, z0 + h), (-b, y, z0)])
    p.add(props.loft(rings), 'castle_face', bevel=0.004,
          keys=lambda f: ('deck', False) if f.normal.z > 0.85 else None)
    for sx in (-1, 1):
        path = [(sx * (r[0][0] + 0.004), r[0][1], r[1][2] + 0.012) for r in rings]
        p.add(props.tube(path, 0.013, seg=6, scale_xy=(1.0, 0.6)), 'ash', smooth=True)
        path = [(sx * (r[0][0] + 0.004), r[0][1], r[1][2] - 0.025) for r in rings]
        p.add(props.tube(path, 0.011, seg=6, scale_xy=(1.0, 0.6)), 'wale', smooth=True)


def _mw_shrouds(p, hull, y, z_top, legs=3, r=0.024):
    """Shrouds from a mast down to deadeyes on a channel at each rail (no ratlines:
    they break up at 2x)."""
    s = hull.s_of(y)
    zr = hull.deck_z(s) + 0.05
    for sx in (-1, 1):
        xb = sx * (hull.deck_half(s) + 0.035)
        for k in range(legs):
            dy = -0.06 - 0.09 * k
            b = (xb, y + dy, zr)
            vkit.rod(p, (0, y, z_top), b, r, 'cordage', seg=6)
            p.add(props.ellipsoid(0.022, 0.022, 0.026, seg=10), 'tar', T(*b), smooth=True)
        p.add(props.box(0.05, 0.36, 0.022), 'wood_dark', T(xb - sx * 0.01, y - 0.15, zr - 0.02), bevel=0.005)


def man_o_war_74(colors, **kw):
    """The realism pass's Man-O-War: a 74-gun ship of the line: a tall
    dark two-decker with two ochre bands through two rows of gunports
    (black lids), copper below the waterline, a low quarterdeck and
    forecastle, a galleried stern with gilt windows, a figurehead under a
    long bowsprit with two jibs; three square-rigged masts (courses,
    topsails, topgallants, braced, civ roundels on the courses), a spanker,
    dark tops and standing rigging; a civ pennant."""
    g = dict(MOW, **kw)
    c = dict(SHIP_COLORS, **MOW_COLORS)
    c.update(colors)
    root = vkit.root('Man-O-War')
    root['fit_scale'] = g['fit_scale']
    p = vkit.part(root, 'hull', c, SHIP_WEAR)
    hull = Hull(L=g['L'], B=g['B'], D=g['D'], draft=g['draft'], sheer_stern=g['sheer_stern'],
                sheer_bow=g['sheer_bow'], bow_p=1.4, stern_p=2.2, transom=0.4, rake_bow=0.3, fullness=1.0,
                tumblehome=0.2)
    yb = g['L'] / 2

    def deck(y):
        return hull.deck_z(hull.s_of(y))

    ss = [-1 + 2 * i / 48 for i in range(49)]
    bm = props.loft([hull.section(s, m=32) for s in ss])
    bands = ((0.17, 0.08), (0.33, 0.24))   # the upper and lower gun decks' ochre bands

    def hkeys(f):
        cc = f.calc_center_median()
        if f.normal.z > 0.85:
            return ('deck_alt', False) if int(abs(cc.x) / 0.03) % 2 else ('deck', False)
        if cc.z < 0.03:
            return ('copper', False)
        zd = hull.deck_z(hull.s_of(cc.y))
        if any(zd - b0 < cc.z < zd - b1 for b0, b1 in bands):
            return ('ochre', False)
        if int(math.floor(cc.z / 0.024)) % 2:
            return ('wood_alt', False)
        return None

    p.add(bm, 'wood', smooth=True, keys=hkeys)
    for sx in (-1, 1):
        for dz, r in [(b0 + 0.012, 0.016) for b0, _ in bands] + [(bands[0][1] - 0.01, 0.014)]:   # wales edging the bands
            path = []
            for i in range(1, 60):
                s_ = -0.95 + 1.9 * i / 60
                z = hull.deck_z(s_) - dz
                path.append((sx * (hull.side_x(s_, z) + 0.004), hull.y(s_), z))
            p.add(props.tube(path, r, seg=6, scale_xy=(1.0, 0.45)), 'wale', smooth=True)
        bh = g['bulwark_h']
        ysb = [-yb * 0.95 + 1.9 * yb * i / 50 for i in range(51)]
        rings = []
        for y in ysb:
            s_ = hull.s_of(y)
            xo = sx * hull.deck_half(s_) * 0.995
            xi = xo - sx * 0.014
            z0 = deck(y) - 0.01
            rings.append([(xo, y, z0), (xo, y, z0 + bh), (xi, y, z0 + bh), (xi, y, z0)])
        p.add(props.loft(rings), 'wood', bevel=0.003)
        p.add(props.tube([(sx * (hull.deck_half(hull.s_of(y)) * 0.995 - sx * 0.007), y, deck(y) - 0.01 + bh + 0.006) for y in ysb],
                         0.014, seg=8), 'rail', smooth=True)   # a dark rail
    for sx in (-1, 1):   # a civ stripe along the upper band's top edge
        path = []
        for i in range(1, 60):
            s_ = -0.94 + 1.88 * i / 60
            z = hull.deck_z(s_) - bands[0][1] + 0.006
            path.append((sx * (hull.side_x(s_, z) + 0.006), hull.y(s_), z))
        p.add(props.tube(path, 0.02, seg=6, scale_xy=(1.0, 0.4)), 'tint', smooth=True, tint=True)
    # the gunports through the band: black squares (the lids, the chequer)
    for k, band in [(k, b) for b in bands for k in range(g['ports'])]:
        y = -yb * 0.74 + 1.48 * yb * k / (g['ports'] - 1)
        s_ = hull.s_of(y)
        z = hull.deck_z(s_) - (band[0] + band[1]) / 2
        for sx in (-1, 1):
            x = sx * (hull.side_x(s_, z) + 0.004)
            p.add(props.box(0.014, 0.07, 0.066), 'port_lid', T(x, y, z), bevel=0.003)
    # a low quarterdeck and forecastle, hatches, the stern gallery, the figurehead and bowsprit
    for y0, y1, h in (g['quarterdeck'], g['forecastle']):
        _mw_castle(p, hull, y0 * yb, y1 * yb, h)
    for yy in (0.3, -0.18):
        p.add(props.box(0.2, 0.16, 0.02), 'rail', T(0, yy, deck(yy) + 0.008), bevel=0.003)
        p.add(props.box(0.16, 0.12, 0.022), 'tar', T(0, yy, deck(yy) + 0.01), bevel=0.002)
    yt = -yb * 0.985
    zt = deck(yt) + 0.02
    w = hull.deck_half(hull.s_of(yt)) * 1.6
    for i in range(5):
        x = -w / 2 + w * (i + 0.5) / 5
        p.add(props.box(w / 5 * 0.8, 0.02, 0.07), 'gold', T(x, yt - 0.012, zt), bevel=0.003)
        p.add(props.box(w / 5 * 0.55, 0.024, 0.05), 'lit', T(x, yt - 0.014, zt), bevel=0.002)   # lit stern lights
    p.add(props.box(w * 1.1, 0.06, 0.016), 'wood', T(0, yt - 0.035, zt - 0.06), bevel=0.004)
    p.add(props.box(w * 1.1, 0.012, 0.04), 'ochre', T(0, yt - 0.065, zt - 0.035), bevel=0.003)
    rud = props.slab([(0.0, -0.02), (-0.1, 0.0), (-0.12, deck(-yb) * 0.6), (0.0, deck(-yb) + 0.02)], 0.024, 'X')
    p.add(rud, 'wood', T(0, -yb - 0.01, 0), bevel=0.003)
    p.add(props.ellipsoid(0.065, 0.12, 0.1, seg=14), 'gold', T(0, yb + 0.08, deck(yb) - 0.08), smooth=True)   # figurehead, gilt
    zbk = deck(yb) - 0.04   # the beakhead: a pointed platform under the bowsprit
    p.add(props.loft([[(0.12 * (1 - t) + 0.012, yb - 0.04 + 0.34 * t, zbk), (-0.12 * (1 - t) - 0.012, yb - 0.04 + 0.34 * t, zbk),
                       (-0.12 * (1 - t) - 0.012, yb - 0.04 + 0.34 * t, zbk - 0.04), (0.12 * (1 - t) + 0.012, yb - 0.04 + 0.34 * t, zbk - 0.04)]
                      for t in (0.0, 0.5, 1.0)]), 'wood', bevel=0.004)
    bow_end = (0, yb + 0.72, deck(yb) + 0.34)
    vkit.rod(p, (0, yb * 0.9, deck(yb * 0.9) + 0.04), bow_end, 0.034, 'wood_dark', r1=0.02, seg=8)
    for sx in (-1, 1):   # head rails sweeping from the bow to the figurehead; quarter galleries
        p.add(props.tube([(sx * hull.deck_half(hull.s_of(yb * 0.9)) * 0.9, yb * 0.9, deck(yb * 0.9) - 0.03),
                          (sx * 0.05, yb + 0.0, deck(yb) - 0.02), (0, yb + 0.06, deck(yb) - 0.05)], 0.015, seg=6), 'gold', smooth=True)
        yq = -yb * 0.9
        xq = sx * (hull.deck_half(hull.s_of(yq)) + 0.02)
        p.add(props.box(0.05, 0.16, 0.12), 'wood', T(xq, yq, deck(yq) - 0.02), bevel=0.01)
        p.add(props.box(0.054, 0.1, 0.04), 'lit', T(xq, yq, deck(yq)), bevel=0.004)
    # the waist: a ship's boat on the hatches, a capstan, carronades at the bulwarks
    p.add(props.ellipsoid(0.08, 0.26, 0.05, seg=16), 'wood_dark', T(0, 0.42, deck(0.42) + 0.05), smooth=True)
    p.add(props.ellipsoid(0.06, 0.22, 0.02, seg=16), 'deck', T(0, 0.42, deck(0.42) + 0.085), smooth=True)
    p.add(props.lathe([(0.0, 0.05), (0.04, 0.035), (0.08, 0.055), (0.09, 0.0)], seg=12), 'wood_dark', T(0, -0.45, deck(-0.45)), smooth=True)
    for k in range(4):
        yk = -0.3 + 0.24 * k
        for sx in (-1, 1):
            xk = sx * (hull.deck_half(hull.s_of(yk)) - 0.07)
            p.add(props.lathe([(0.0, 0.026), (0.1, 0.02), (0.11, 0.024)], seg=10), 'port_lid', T(xk, yk, deck(yk) + 0.05) @ R('Y', sx * 90), smooth=True)

    # masts (tarred caps, dark tops) and their rigging
    ms = [dict(m) for m in g['masts']]
    k = g.get('rig_scale', 1.0)   # a shorter rig: masts, yards and sails scaled about each mast's foot (1.0 = as reviewed)
    if k != 1.0:
        for m in ms:
            d0 = deck(m['y'])
            m['top'] = d0 + k * (m['top'] - d0)
            m['sails'] = tuple((w * k, h * k) for w, h in m['sails'])
            if 'spanker' in m:
                m['spanker'] = tuple(v * k for v in m['spanker'])
    for m in ms:
        hs = sum(h for _, h in m['sails']) + 0.01 * (len(m['sails']) - 1)
        m['_head'] = m['top'] + hs - m['sails'][0][1] + 0.08
        vkit.rod(p, (0, m['y'], deck(m['y']) - 0.05), (0, m['y'], m['_head']), m['r'], 'wood_dark', r1=m['r'] * 0.6, seg=10)
        p.add(props.lathe([(0.0, 0.02), (0.025, 0.024), (0.04, 0.0)], seg=12), 'tar', T(0, m['y'], m['_head']), smooth=True)
        p.add(props.lathe([(0.0, 0.06), (0.04, 0.12), (0.08, 0.125), (0.085, 0.0)], seg=16), 'tar',
              T(0, m['y'], m['top'] + 0.02), smooth=True)
    for m in ms:   # long civ pennants at every masthead
        _pennant_mw(p, (0, m['y'], m['_head'] - 0.05), *g['pennant'])
    if g['rigging']:
        for m in ms:
            _mw_shrouds(p, hull, m['y'], m['top'] - 0.02, legs=3 if m is not ms[2] else 2)
        r = g['stay_r']
        vkit.rod(p, (0, ms[0]['y'], ms[0]['top'] - 0.02), bow_end, r, 'cordage', seg=6)   # forestay
        vkit.rod(p, (0, ms[1]['y'], ms[1]['top'] - 0.02), (0, ms[0]['y'], deck(ms[0]['y']) + 0.12), r, 'cordage', seg=6)
    ys_e = -yb * 0.97
    ze = deck(ys_e) + 0.1
    vkit.rod(p, (0, ys_e, ze - 0.1), (0, ys_e - 0.04, ze + 0.34), 0.012, 'wood_dark', seg=6)
    bmf = props.bmesh.new()
    rows = []
    for jj in range(9):
        t = jj / 8
        rows.append([bmf.verts.new((0.03 * math.sin(4 * t), ys_e - 0.04 - 0.36 * t, ze + 0.34 - 0.24 * ii / 4)) for ii in range(5)])
    for jj in range(8):
        for ii in range(4):
            bmf.faces.new((rows[jj][ii], rows[jj + 1][ii], rows[jj + 1][ii + 1], rows[jj][ii + 1]))
    p.add(bmf, 'tint', smooth=True, tint=True)   # the ensign
    p.finish()
    # the square sails, braced; the courses carry the civ roundel; the spanker and two jibs
    for k, m in enumerate(ms):
        z_top = m['top']
        for j, (w, h) in enumerate(m['sails']):
            if j:
                z_top += h + 0.01   # the sails close up
            if k == 2 and j == 0:
                continue   # the mizzen course is the spanker (a crossjack would hide it)
            r = vkit.part(root, 'sail', c, SHIP_WEAR)
            emb = g['device'] if (k == 1 and j < 2) or (k == 0 and j == 0) else None   # the cross: main course and topsail, fore course
            _mw_sail(r, w, h, z_top, g['billow'], emb, inset=1.0)
            o = r.finish()
            o.location = (0, m['y'] + 0.02, 0)
            o.rotation_euler = (0, 0, math.radians(m.get('brace', g['brace'])))   # staggered per mast
    # the four-sided gaff spanker: luff on the mizzen, gaff peaked aft, the boom ending at the taffrail
    gaff, drop = ms[2]['spanker']
    zm0, ym = ms[2]['top'] + 0.05, ms[2]['y']
    tl, bl = (0, ym - 0.03, zm0), (0, ym - 0.03, zm0 - drop)
    boom_end = (0, -yb * 0.98, zm0 - drop - 0.02)
    peak = (0, ym - gaff, zm0 + gaff * 0.62)   # a raked gaff, its peak rising
    r = vkit.part(root, 'spanker', c, SHIP_WEAR)
    bm = props.bmesh.new()
    n = 12
    grid = []
    for j in range(n + 1):
        v = j / n
        row = []
        for i in range(n + 1):
            u = i / n
            pt = [(tl[k] * (1 - u) + peak[k] * u) * (1 - v) + (bl[k] * (1 - u) + boom_end[k] * u) * v for k in range(3)]
            pt[0] += 0.08 * 4 * u * (1 - u) * (0.4 + 0.6 * math.sin(math.pi * v))
            row.append(bm.verts.new(pt))
        grid.append(row)
    for j in range(n):
        for i in range(n):
            bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
    r.add(bm, 'sail_mid', smooth=True, keys=lambda f: ('sail_shade', False) if abs(f.calc_center_median().x) > 0.05 else None)   # slightly darker canvas
    vkit.rod(r, tl, peak, 0.016, 'wood_dark', seg=8)
    vkit.rod(r, bl, boom_end, 0.024, 'wood_dark', seg=8)
    r.finish()
    q = vkit.part(root, 'jibs', c, SHIP_WEAR)
    fhead = (0, ms[0]['y'], ms[0]['top'] + 0.15)
    for t, cl in ((1.0, (0.0, yb * 0.95, deck(yb) + 0.22)), (0.7, (0.0, yb * 0.85, deck(yb) + 0.18))):
        tack = (0, yb * 0.9 + (bow_end[1] - yb * 0.9) * t, deck(yb * 0.9) + 0.04 + (bow_end[2] - deck(yb * 0.9) - 0.04) * t - 0.02)
        _mw_jib(q, fhead, tack, cl)
    q.finish()
    return root


def _pennant_mw(p, top, w, h, phase=0.0):
    """A civ pennant streaming aft, keeping 40 % of its height at the tip."""
    n = 10
    bm = props.bmesh.new()
    rows = []
    for j in range(n + 1):
        t = j / n
        hh = h * (1 - 0.6 * t)
        rows.append([bm.verts.new((0.06 * t * math.sin(5 * t + phase), top[1] - w * t, top[2] - hh * k)) for k in (0.0, 1.0)])
    for j in range(n):
        bm.faces.new((rows[j][0], rows[j + 1][0], rows[j + 1][1], rows[j][1]))
    p.add(bm, 'tint', smooth=True, tint=True)


# ------------------------------------------------------------ the ships of sail (carrack to man-o-war)

props.DEFAULTS.setdefault('port', ('#0e0c0a', 0.9, 0.0, 'polished'))  # open gunports: flat dark, no edge wear (it lit small boxes up)
props.DEFAULTS.setdefault('gun_black', ('#1e1e20', 0.45, 1.0, 'metal'))
props.DEFAULTS.setdefault('gun_grey', ('#6e7072', 0.4, 1.0, 'metal'))   # deck guns that read at 1x
props.DEFAULTS.setdefault('glass', ('#c8b46a', 0.25, 0.3, 'metal'))   # lit stern windows


def jib(p, a, b, foot, emblem=None, stripes=False):
    """A triangular headsail between the stay points a (high) and b (low, on
    the bowsprit) and its clew `foot`; optionally striped in the civ color."""
    n = 14
    bm = props.bmesh.new()
    grid = []
    for j in range(n + 1):
        v = j / n
        row = []
        for i in range(n - j + 1):
            u = i / n
            w0 = 1 - u - v
            px = [a[k] * w0 + b[k] * u + foot[k] * v for k in range(3)]
            px[0] += 0.05 * math.sin(math.pi * u) * (1 - v)
            row.append(bm.verts.new(px))
        grid.append(row)
    for j in range(n):
        for i in range(n - j):
            bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i]))
            if i < n - j - 1:
                bm.faces.new((grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
    def keys(f):   # (always a key function: a jib added without one didn't render)
        if stripes and int(math.floor(f.calc_center_median().z / 0.07)) % 2 == 0:
            return ('tint', True)
        return ('sail_seam', False) if abs((f.calc_center_median().z / 0.05) % 1.0 - 0.5) > 0.46 else None
    p.add(bm, 'sail', smooth=True, keys=keys)
    vkit.rod(p, a, b, 0.005, 'cordage', seg=4)   # the stay it's hanked to


def _gunports(p, hull, rows, sides=(-1, 1)):
    """Rows of open gunports along the sides, each with a gun muzzle and a
    raised lid: rows = [(z, n, y0, y1)]."""
    for z, n, y0, y1 in rows:
        for i in range(n):
            y = y0 + (y1 - y0) * i / max(1, n - 1)
            s = hull.s_of(y)
            for sx in sides:
                x = sx * (hull.side_x(s, z) + 0.004)   # on the hull's surface at this height
                p.add(props.box(0.012, 0.045, 0.04), 'port', T(x, y, z), bevel=0.003)
                p.add(props.lathe([(0.0, 0.009), (0.035, 0.008)], seg=8), 'gun_black', T(x, y, z) @ R('Y', sx * 90),
                      smooth=True)
                p.add(props.box(0.006, 0.05, 0.035), 'wood_dark', T(x * 1.02, y, z + 0.035) @ R('Y', sx * 50),
                      bevel=0.002)


def _shrouds(p, hull, y, z_top, z_deck, rungs=9, spread=1.04):
    """Shroud ladders down both sides from a masthead to the channels."""
    for sx in (-1, 1):
        xb = sx * hull.deck_half(hull.s_of(y)) * spread
        legs = [((0.0, y + dy, z_top), (xb, y + dy * 1.6, z_deck)) for dy in (-0.1, -0.035, 0.035, 0.1)]
        for a, b in legs:
            vkit.rod(p, a, b, 0.005, 'cordage', seg=4)
        for k in range(1, rungs):
            t = k / rungs
            pa = Vector(legs[0][0]).lerp(Vector(legs[0][1]), t)
            pb = Vector(legs[-1][0]).lerp(Vector(legs[-1][1]), t)
            vkit.rod(p, pa, pb, 0.0035, 'cordage', seg=4)
        p.add(props.box(0.035, 0.36, 0.022), 'wood_dark', T(xb, y, z_deck - 0.02), bevel=0.005)


def _flag(p, top, w, h, emblem=None):
    """A big rectangular civ flag flying aft from a masthead staff."""
    n, m = 16, 10   # enough rows for an emblem to show on it
    bm = props.bmesh.new()
    rows = []
    for j in range(n + 1):
        t = j / n
        rows.append([bm.verts.new((0.05 * t * math.sin(4 * t), top[1] - w * t, top[2] - h * i / m)) for i in range(m + 1)])
    for j in range(n):
        for i in range(m):
            bm.faces.new((rows[j][i], rows[j + 1][i], rows[j + 1][i + 1], rows[j][i + 1]))
    mask = EMBLEMS.get(emblem)
    # the emblem's mask in civ color on white cloth, sized to the flag
    keys = (lambda f: ('tint', True) if mask(f.calc_center_median().y - (top[1] - w / 2),
                                             f.calc_center_median().z - (top[2] - h / 2), h * 1.15) else None) if mask else None
    p.add(bm, 'sail', smooth=True, keys=keys) if mask else p.add(bm, 'tint', smooth=True, tint=True)


def _wales(p, hull, stripes, n=40):
    """Continuous wales (strakes) along both sides, following the hull's surface:
    stripes = [(z0, z1, key)], each a flat band from z0 to z1."""
    for z0, z1, key in stripes:
        zc, hz = (z0 + z1) / 2, (z1 - z0) / 2
        for sx in (-1, 1):
            path = []
            for i in range(1, n):
                s = -0.97 + 1.94 * i / n
                if zc > hull.deck_z(s):
                    continue
                path.append((sx * (hull.side_x(s, zc) + 0.006), hull.y(s), zc))
            if len(path) > 2:
                p.add(props.tube(path, max(hz, 0.008), seg=6, scale_xy=(1.0, 0.3)), key, smooth=True,
                      tint=key == 'tint')   # tall, flat on the hull


def _deck_guns(p, hull, n, y0, y1, z_off=0.0):
    """Cannon on their carriages along both rails of the weather deck,
    muzzles out through the bulwark."""
    for i in range(n):
        y = y0 + (y1 - y0) * i / max(1, n - 1)
        s = hull.s_of(y)
        zd = hull.deck_z(s) + z_off
        for sx in (-1, 1):
            xr = sx * hull.deck_half(s) * 0.92
            p.add(props.box(0.05, 0.07, 0.03), 'wood_dark', T(xr - sx * 0.05, y, zd + 0.02), bevel=0.005)   # carriage
            p.add(props.lathe([(0.0, 0.032), (0.22, 0.022), (0.25, 0.028)], seg=10), 'gun_grey',
                  T(xr - sx * 0.07, y, zd + 0.045) @ R('Y', sx * 90), smooth=True)


def _rich_castle(p, hull, y0, y1, h, windows=3):
    """A tall castle (as _castle) with lit gilt-framed windows along its sides
    and a balustrade of turned posts round its top."""
    z_top = _castle(p, hull, y0, y1, h)
    for sx in (-1, 1):
        for k in range(windows):
            y = y0 + (y1 - y0) * (k + 0.5) / windows
            s = hull.s_of(y)
            x = sx * hull.deck_half(s) * 0.99
            zc = hull.deck_z(s) + h * 0.55
            p.add(props.box(0.008, 0.05, 0.05), 'port', T(x, y, zc), bevel=0.002)   # dark carved windows
            p.add(props.box(0.006, 0.065, 0.065), 'gold', T(x * 0.998, y, zc), bevel=0.002)
        n = max(4, int(abs(y1 - y0) / 0.07))
        for k in range(n + 1):
            y = y0 + (y1 - y0) * k / n
            s = hull.s_of(y)
            x = sx * hull.deck_half(s) * 0.96
            zb = hull.deck_z(s) - 0.02 + h
            vkit.rod(p, (x, y, zb), (x, y, zb + 0.06), 0.006, 'gold', seg=5)
    return z_top


def _stern_windows(p, hull, z, n=4, rows=1):
    """Gilt-framed, lit windows across the stern."""
    y = -hull.L / 2 * 0.985
    w = hull.half_beam(-0.97) * 1.6
    for r in range(rows):
        for i in range(n):
            x = -w / 2 + w * (i + 0.5) / n
            p.add(props.box(w / n * 0.62, 0.012, 0.05), 'glass', T(x, y - 0.01, z + 0.08 * r), bevel=0.003)
            p.add(props.box(w / n * 0.75, 0.008, 0.065), 'gold', T(x, y - 0.004, z + 0.08 * r), bevel=0.002)
    p.add(props.box(w * 1.05, 0.02, 0.02), 'gold', T(0, y - 0.005, z + 0.08 * rows - 0.01), bevel=0.004)


def sailing_ship(name, cfg, colors):
    """A full-rigged ship from a config: the hull (Hull's arguments, strakes),
    castles, a bowsprit with jibs, rows of gunports, stern windows, and masts
    each carrying tiers of square sails (each with an optional civ emblem),
    an optional lateen or gaff, shroud ladders, fighting tops and civ
    pennants or flags."""
    c = dict(SHIP_COLORS, **cfg.get('colors', {}))
    c.update(colors)
    root = vkit.root(name)
    p = vkit.part(root, 'hull', c, SHIP_WEAR)
    hull = Hull(**cfg['hull'])
    add_hull(p, hull, stripes=[s for s in cfg.get('stripes', []) if s[2] == 'hull_grime'])
    _wales(p, hull, [s for s in cfg.get('stripes', []) if s[2] != 'hull_grime'])
    for y0, y1, h in cfg.get('castles', []):
        _castle(p, hull, y0, y1, h)
    for y0, y1, h, nw in cfg.get('rich_castles', []):
        _rich_castle(p, hull, y0, y1, h, nw)

    def deck(y):
        return hull.deck_z(hull.s_of(y))

    _gunports(p, hull, cfg.get('ports', []))
    if cfg.get('deck_guns'):
        _deck_guns(p, hull, *cfg['deck_guns'])
    if cfg.get('stern_windows'):
        _stern_windows(p, hull, deck(-hull.L / 2 * 0.95) + cfg['stern_windows'][0], *cfg['stern_windows'][1:])
    bs = cfg.get('bowsprit')
    if bs:
        vkit.rod(p, bs[0], bs[1], 0.022, 'wood_dark', seg=8)
    fore_top = None
    for k, m in enumerate(cfg['masts']):
        y, r = m['y'], m.get('r', 0.03)
        z = deck(y) + m.get('z0', 0.25)
        tiers = m.get('tiers', [])
        z_cur = z + sum(t[0] for t in tiers) + 0.03 * len(tiers)
        top = z_cur + m.get('topmast', 0.18)
        vkit.rod(p, (0, y, deck(y) - 0.05), (0, y, top), r, 'wood_dark', seg=10)
        # the sails, from the course up: (height, width, emblem, billow)
        zt = z + sum(t[0] for t in tiers) + 0.03 * len(tiers)
        for j, (h, w, emblem, billow) in enumerate(reversed(tiers)):
            sail(p, y + 0.02, zt, w * 0.9, w, h, emblem, billow=billow, seed=10 * k + j, patches=1)
            zt -= h + 0.03
            if j == len(tiers) - 2:   # a fighting top (a drum with a rail) below the topsail
                p.add(props.lathe([(0.0, 0.06), (0.03, 0.12), (0.07, 0.125)], seg=16), 'wood_dark', T(0, y, zt - 0.03),
                      smooth=True)   # the top's dark drum
                for q in range(10):   # its rail stanchions (not k: that's the mast's index)
                    a = 2 * math.pi * q / 10
                    vkit.rod(p, (0.12 * math.cos(a), y + 0.12 * math.sin(a), zt + 0.04),
                             (0.12 * math.cos(a), y + 0.12 * math.sin(a), zt + 0.09), 0.005, 'wood_dark', seg=4)
            if m.get('yard_pennants') and j == len(tiers) - 1:   # small civ pennants at the course yard's tips
                for sx in (-1, 1):
                    pennant(p, (sx * (w / 2 + 0.03), y + 0.02, zt + h + 0.04), 0.16, 0.06, phase=sx)
        if m.get('lateen'):
            ln, ang, drop, em = m['lateen']
            lateen(p, y, deck(y) + drop + 0.25, ln, ang, drop, em)
        if m.get('shrouds', True) and tiers:
            _shrouds(p, hull, y, z + tiers[-1][0] + 0.03, deck(y) + 0.02)
        if m.get('flag'):
            _flag(p, (0, y, top + 0.02), *m['flag'])
        elif m.get('pennant', True):
            pennant(p, (0, y, top + 0.02), *m.get('pennant_size', (0.75, 0.16)), phase=k)
        if k == 0:
            fore_top = (0, y, top - 0.05)
    if bs and fore_top:
        vkit.rod(p, fore_top, bs[1], 0.006, 'cordage', seg=4)   # the forestay
        for j, (st, cl, em) in enumerate(cfg.get('jibs', [])):
            jib(p, st, bs[1] if j == 0 else Vector(bs[0]).lerp(Vector(bs[1]), 0.6), cl, stripes=em == 'stripes')
    for extra in cfg.get('extras', []):
        extra(p, hull, deck)
    p.finish()
    return root


def _carrack_extras(p, hull, deck):
    # gilt rails on the high castles and a big stern lantern
    p.add(props.lathe([(0.0, 0.03), (0.06, 0.04), (0.12, 0.0)], seg=12), 'gold', T(0, -1.32, deck(-1.3) + 0.62),
          smooth=True)
    # small civ pennants on staffs at the bowsprit's end and the stern rail, as the original
    for base, h in (((0, 1.85, 0.68), 0.22), ((0, -1.3, deck(-1.3) + 0.5), 0.26)):
        top = (base[0], base[1], base[2] + h)
        vkit.rod(p, base, top, 0.008, 'wood_dark', seg=6)
        pennant(p, top, 0.26, 0.08, phase=base[1])


SAILING = {
    'Carrack': dict(
        colors={'wood': '#5a3c22', 'wood_alt': '#4a301c', 'wood_dark': '#2a1e14', 'deck': '#7a5e3c', 'rail': '#2e2014',
                'sail': '#cfc09a', 'cordage': '#2b2118'},
        hull=dict(L=2.7, B=0.78, D=0.22, draft=0.16, sheer_stern=0.38, sheer_bow=0.34, bow_p=1.25, stern_p=1.8,
                  transom=0.3, rake_bow=0.3, fullness=1.0, tumblehome=0.25),
        stripes=[(-0.03, 0.035, 'hull_grime'), (0.06, 0.09, 'gold'), (0.16, 0.19, 'gold'), (0.29, 0.32, 'gold')],
        # the tall aftcastle (two decks, windows, balustrade) and a raised forecastle
        rich_castles=[(-1.35, -0.5, 0.26, 4), (-1.35, -0.92, 0.46, 2), (0.7, 1.22, 0.3, 3)],
        stern_windows=(0.24, 4, 2),
        bowsprit=((0, 1.2, 0.5), (0, 1.85, 0.68)),
        ports=[(0.11, 5, -0.4, 0.5)],
        masts=[dict(y=0.62, tiers=[(0.42, 0.95, 'quartered', 0.2), (0.24, 0.6, None, 0.1)], yard_pennants=True),
               dict(y=0.0, r=0.034, tiers=[(0.62, 1.35, 'quartered', 0.24), (0.34, 0.85, None, 0.1)],
                    flag=(0.4, 0.26, 'quartered'), pennant=False, yard_pennants=True),
               dict(y=-0.78, tiers=[], lateen=(1.5, 30.0, 0.72, None), pennant_size=(0.5, 0.14))],
        jibs=[],
        extras=[_carrack_extras],
    ),
    'Galleon': dict(
        colors={'wood': '#4a301c', 'wood_alt': '#3e2816', 'wood_dark': '#24180e', 'deck': '#6e5434', 'rail': '#2a1c10',
                'sail': '#dccfac', 'cordage': '#2b2118', 'gold': '#d0aa52'},
        hull=dict(L=2.9, B=0.74, D=0.22, draft=0.16, sheer_stern=0.34, sheer_bow=0.26, bow_p=1.3, stern_p=1.8,
                  transom=0.3, rake_bow=0.4, fullness=1.0, tumblehome=0.25),
        stripes=[(-0.03, 0.035, 'hull_grime'), (0.06, 0.095, 'gold'), (0.15, 0.185, 'gold'), (0.26, 0.295, 'gold')],
        rich_castles=[(-1.45, -0.7, 0.22, 4), (-1.45, -1.05, 0.38, 2), (0.85, 1.2, 0.16, 2)],
        stern_windows=(0.22, 4, 2),
        bowsprit=((0, 1.25, 0.42), (0, 1.95, 0.6)),
        ports=[(0.115, 7, -0.6, 0.7)],
        deck_guns=(4, -0.4, 0.5),
        masts=[dict(y=0.75, tiers=[(0.4, 0.95, 'stripes', 0.2), (0.3, 0.7, 'stripes', 0.12), (0.2, 0.45, None, 0.06)],
                    pennant_size=(0.75, 0.18)),
               dict(y=0.0, r=0.034, tiers=[(0.5, 1.2, 'stripes', 0.22), (0.38, 0.9, 'stripes', 0.14), (0.24, 0.55, None, 0.07)],
                    pennant_size=(0.95, 0.22)),
               dict(y=-0.75, tiers=[(0.34, 0.8, 'stripes', 0.15), (0.24, 0.55, None, 0.08)], pennant_size=(0.7, 0.16))],
        jibs=[((0, 0.77, 1.55), (0, 1.3, 0.7), None)],
    ),
    'Frigate': dict(
        colors={'wood': '#5a3e24', 'wood_alt': '#4a321c', 'wood_dark': '#241a10', 'deck': '#8a6e48', 'rail': '#2a1c10',
                'sail': '#e0d4b2', 'cordage': '#2b2118'},
        hull=dict(L=3.0, B=0.7, D=0.24, draft=0.15, sheer_stern=0.14, sheer_bow=0.12, bow_p=1.4, stern_p=1.9,
                  transom=0.35, rake_bow=0.35, fullness=1.0, tumblehome=0.15),
        stripes=[(-0.03, 0.035, 'hull_grime'), (0.09, 0.17, 'wood_dark'), (0.2, 0.225, 'gold')],
        rich_castles=[(-1.5, -0.85, 0.1, 3)],
        stern_windows=(0.12, 5, 1),
        bowsprit=((0, 1.35, 0.32), (0, 2.1, 0.55)),
        ports=[(0.13, 9, -0.95, 0.95)],
        deck_guns=(6, -0.8, 0.8),
        masts=[dict(y=0.85, tiers=[(0.38, 1.0, 'roundel', 0.2), (0.32, 0.78, None, 0.14), (0.2, 0.5, None, 0.06)],
                    pennant_size=(1.0, 0.16)),
               dict(y=0.05, r=0.036, tiers=[(0.46, 1.25, 'roundel', 0.22), (0.38, 0.95, 'roundel', 0.16),
                                            (0.24, 0.6, None, 0.07)], pennant_size=(0.9, 0.2)),
               dict(y=-0.85, tiers=[(0.34, 0.85, 'roundel', 0.15), (0.24, 0.55, None, 0.08)], pennant_size=(0.6, 0.15))],
        jibs=[((0, 0.88, 1.75), (0, 1.5, 0.72), None), ((0, 0.88, 1.45), (0, 1.25, 0.62), None)],
    ),
    'Privateer': dict(
        colors={'wood': '#2e2a26', 'wood_alt': '#262320', 'wood_dark': '#151311', 'deck': '#5e4a32', 'rail': '#1a1612',
                'sail': '#d0c2a0', 'cordage': '#221c16', 'gold': '#8a8a86'},   # a grey wale on the black hull
        hull=dict(L=2.6, B=0.6, D=0.22, draft=0.13, sheer_stern=0.16, sheer_bow=0.14, bow_p=1.35, stern_p=1.8,
                  transom=0.3, rake_bow=0.35, fullness=0.95, tumblehome=0.12),
        stripes=[(-0.03, 0.035, 'hull_grime'), (0.15, 0.18, 'gold')],
        rich_castles=[(-1.3, -0.8, 0.1, 2)],
        stern_windows=(0.14, 3, 1),
        bowsprit=((0, 1.2, 0.32), (0, 1.85, 0.52)),
        ports=[(0.11, 6, -0.75, 0.7)],
        deck_guns=(3, -0.5, 0.4),
        masts=[dict(y=0.55, tiers=[(0.42, 0.95, 'skull', 0.2), (0.3, 0.65, None, 0.1)]),
               dict(y=-0.25, r=0.034, tiers=[(0.5, 1.15, 'skull', 0.22), (0.34, 0.8, None, 0.12)],
                    pennant_size=(0.8, 0.18)),
               dict(y=-0.95, tiers=[], lateen=(1.25, 30.0, 0.62, 'skull'), pennant_size=(0.5, 0.14))],
        jibs=[((0, 0.57, 1.35), (0, 1.2, 0.62), None)],
    ),
    'Man-O-War': dict(
        colors={'wood': '#2a2622', 'wood_alt': '#24211e', 'wood_dark': '#141210', 'deck': '#7a6244', 'rail': '#1a1612',
                'sail': '#e2d8ba', 'cordage': '#221c16', 'gold': '#c8a848'},
        hull=dict(L=3.3, B=0.84, D=0.36, draft=0.17, sheer_stern=0.16, sheer_bow=0.12, bow_p=1.35, stern_p=2.0,
                  transom=0.4, rake_bow=0.3, fullness=1.0, tumblehome=0.2),
        # the Nelson chequer: buff bands along the gundecks, the ports dark in the black between them
        stripes=[(-0.03, 0.035, 'hull_grime'), (0.065, 0.105, 'gold'), (0.165, 0.205, 'gold'), (0.28, 0.3, 'gold')],
        rich_castles=[(-1.65, -0.95, 0.1, 3)],
        stern_windows=(0.12, 6, 2),
        bowsprit=((0, 1.5, 0.48), (0, 2.3, 0.72)),
        ports=[(0.13, 12, -1.2, 1.15), (0.23, 12, -1.2, 1.15)],
        deck_guns=(7, -1.0, 0.9),
        masts=[dict(y=0.95, tiers=[(0.42, 1.1, 'lion', 0.2), (0.36, 0.86, 'lion', 0.14), (0.22, 0.55, None, 0.06)],
                    pennant_size=(1.0, 0.18)),
               dict(y=0.05, r=0.04, tiers=[(0.5, 1.4, 'lion', 0.22), (0.42, 1.05, 'lion', 0.16), (0.26, 0.66, None, 0.07)],
                    pennant_size=(1.1, 0.22)),
               dict(y=-0.95, tiers=[(0.36, 0.95, 'lion', 0.15), (0.26, 0.6, None, 0.08)], pennant_size=(0.75, 0.16))],
        jibs=[((0, 0.98, 1.95), (0, 1.7, 0.85), None), ((0, 0.98, 1.6), (0, 1.42, 0.75), None)],
    ),
}


def _fleur(x, z, h):
    """The dromon's emblem: a bold fleur (a tall central petal, two curling
    side petals and a band), filling much of the sail."""
    s = 0.5 * h
    ex, ez = x / s, z / s
    centre = abs(ex) < 0.22 * (1 - max(0.0, ez - 0.1) / 0.85) and -0.2 < ez < 0.95
    sides = any(abs(math.hypot(ex - sx * 0.55, ez - 0.25) - 0.38) < 0.13 and ez > 0.05 and sx * ex > 0.15
                for sx in (-1, 1))
    band = abs(ez + 0.1) < 0.09 and abs(ex) < 0.6
    foot = abs(ex) < 0.14 and -0.7 < ez < -0.1
    return centre or sides or band or foot


def _celtic(x, z, h):
    """The curragh's emblem: a ringed Celtic cross with round terminals."""
    s = 0.44 * h
    ex, ez = x / s, z / s
    d = math.hypot(ex, ez)
    arms = (abs(ex) < 0.16 and abs(ez) < 0.95) or (abs(ez) < 0.16 and abs(ex) < 0.95)
    ring = 0.5 < d < 0.66
    knobs = any(math.hypot(ex - a, ez - b) < 0.17 for a, b in ((0.95, 0), (-0.95, 0), (0, 0.95), (0, -0.95)))
    return arms or ring or knobs


def _flames(x, z, h):
    """The dromon's emblem: wavy flame licks of different heights rising from
    the foot of the sail, each bending and flicking over at its tip, the
    central one tallest."""
    s = 0.5 * h
    ex, ez = x / s, z / s
    if abs(ex) > 0.95 or ez < -1.0:
        return False
    licks = [(-0.72, 0.85, 0.16), (-0.38, 1.25, 0.2), (0.0, 1.75, 0.26), (0.36, 1.15, 0.2), (0.7, 0.95, 0.17)]
    for cx, hgt, w in licks:
        t = (ez + 1.0) / hgt                     # 0 at the foot, 1 at this lick's tip
        if 0.0 <= t <= 1.0:
            # the lick sways sideways as it rises and its tip flicks over
            sway = 0.12 * math.sin(3.2 * t + cx * 5) + 0.18 * t ** 3 * (1 if cx >= 0 else -1)
            half = w * (1 - t) ** 0.7 * (1 + 0.25 * math.sin(7 * t + cx * 9))
            if abs(ex - cx - sway) < half:
                return True
    return ez < -0.82   # a flame bed along the foot


EMBLEMS['fleur'] = _fleur
EMBLEMS['celtic'] = _celtic
EMBLEMS['flames'] = _flames


def curragh(colors):
    """An Irish curragh: a light, deep hide boat on a wicker frame, two short
    masts each with a square sail bearing the civ cross, a few oars and a
    steering oar."""
    # a pale hide hull over the wicker frame, as the original's cream boat
    c = dict(SHIP_COLORS, **{'wood': '#d8ccb0', 'wood_alt': '#cdbf9e', 'wood_dark': '#4a3820', 'deck': '#8a7250',
                             'rail': '#3a2a18', 'sail': '#d8caa6', 'cordage': '#2b2118'})
    c.update(colors)
    root = vkit.root('Curragh')
    p = vkit.part(root, 'hull', c, SHIP_WEAR)
    hull = Hull(L=1.7, B=0.5, D=0.2, draft=0.1, sheer_stern=0.16, sheer_bow=0.2, bow_p=1.6, stern_p=1.7, fullness=0.9)
    add_hull(p, hull, stripes=[(-0.03, 0.02, 'hull_grime'), (0.15, 0.17, 'wood_dark')], plank=0.035)
    for i in range(7):   # the frame's ribs showing over the gunwale
        y = -0.6 + 1.2 * i / 6
        s = hull.s_of(y)
        for sx in (-1, 1):
            p.add(props.box(0.02, 0.025, 0.05), 'wood_dark', T(sx * hull.deck_half(s), y, hull.deck_z(s) + 0.01), bevel=0.004)
    for y, h, w, top in ((0.32, 0.36, 0.62, 0.98), (-0.28, 0.42, 0.72, 1.06)):
        vkit.rod(p, (0, y, 0.12), (0, y, top + 0.08), 0.022, 'wood_dark', seg=8)
        sail(p, y + 0.02, top, w * 0.92, w, h, 'celtic', billow=0.16, seed=int(10 * h), patches=1)
        pennant(p, (0, y, top + 0.1), 0.3, 0.1)
    # the big steering oar over the stern quarter and a long sweep out over the bow (no side oars, as the original)
    vkit.rod(p, (0.12, -0.74, 0.34), (0.24, -1.12, -0.06), 0.016, 'wood_dark', seg=8)
    p.add(props.box(0.014, 0.07, 0.18), 'wood_dark', T(0.24, -1.12, -0.08) @ R('X', 25), bevel=0.003)
    vkit.rod(p, (0.0, 0.7, 0.32), (0.0, 1.22, 0.4), 0.012, 'wood_dark', seg=8)
    vkit.rod(p, (0, 0.32, 1.02), (0, 0.88, 0.32), 0.005, 'cordage', seg=4)
    vkit.rod(p, (0, -0.28, 1.1), (0, -0.86, 0.34), 0.005, 'cordage', seg=4)
    p.finish()
    return root


# ------------------------------------------------------------ curragh (realism pass: a seagoing Irish hide boat)

# The Brendan-type ocean curragh (notes/realism/Curragh.md): ~11 m x 2.4 m, oak-tanned ox hides
# stitched over an ash frame, two masts with square sails. L 1.7 = 11 m (1 unit = 6.5 m).
HIDEBOAT = dict(
    L=1.86, B=0.32, D=0.22, draft=0.09, sheer_stern=0.46, sheer_bow=0.58, flare=0.28,
    panel_y=0.16, panel_z=0.075, hides=5, rib=0.2, ribs=(-0.52, -0.21, 0.13, 0.47),          # the hide panels' size (stitched seams between them)
    thwarts=5, oars=3,
    main=dict(y=0.06, top=1.12, w=0.98, h=0.69),
    fore=dict(y=0.56, top=0.8, w=0.58, h=0.41, rake=8.0),
    billow=0.14, fit_scale=0.88, fore_brace=13.0, main_brace=13.0,
    pennant=(0.26, 0.11),             # a civ pennant at the mainmast head (colour in the profiles)
)

HIDE_COLORS = {
    'hide': '#8a5e34', 'hide_alt': '#6a4626', 'hide_dark': '#4a301a', 'oxhide': '#76502a', 'oxhide_alt': '#5a3a1c', 'oxhide_rib': '#54381e', 'hide_low': '#3e2a16', 'hide_seam': '#5e4226', 'hide_seam_dark': '#1e140a', 'stitch': '#c8aa78',
    'ash': '#a8844e', 'oar_w': '#9a7a4e', 'wood_dark': '#3a2a1a', 'thwart': '#6e5232', 'deck': '#3e2c1a',
    'sail': '#dccca6', 'sail_seam': '#c2b28c', 'sail_patch': '#ccbc96', 'sail_mid': '#cfbf98', 'sail_shade': '#bead88',
    'brail': '#54442f', 'tar': '#2e261e', 'shelter': '#5a4028', 'oar': '#b8945e',
}
props.DEFAULTS.setdefault('hide', ('#86603a', 0.42, 0.0, 'leather'))   # oiled: a slight sheen
props.DEFAULTS.setdefault('hide_alt', ('#7e5a36', 0.6, 0.0, 'leather'))
props.DEFAULTS.setdefault('hide_seam_dark', ('#2a1c10', 0.8, 0.0, None))
props.DEFAULTS.setdefault('oar_w', ('#6e5232', 0.62, 0.0, 'wood'))
props.DEFAULTS.setdefault('oxhide', ('#6c4828', 0.34, 0.0, 'leather'))   # oiled ox hide: a faint sheen
props.DEFAULTS.setdefault('oxhide_alt', ('#5a3a1e', 0.34, 0.0, 'leather'))
props.DEFAULTS.setdefault('stitch', ('#c8aa78', 0.7, 0.0, None))
props.DEFAULTS.setdefault('oxhide_rib', ('#4e3218', 0.5, 0.0, 'leather'))
props.DEFAULTS.setdefault('hide_dark', ('#5a3c20', 0.6, 0.0, 'leather'))
props.DEFAULTS.setdefault('hide_low', ('#5e4228', 0.6, 0.0, 'leather'))
props.DEFAULTS.setdefault('hide_seam', ('#2e2216', 0.8, 0.0, None))
props.DEFAULTS.setdefault('ash', ('#b8945e', 0.6, 0.0, 'wood'))
props.DEFAULTS.setdefault('shelter', ('#5a4028', 0.7, 0.0, 'leather'))


def _pennant_solid(p, top, w, h, phase=0.0):
    """A civ pennant streaming aft that keeps ~40 % of its height at the tip
    (a pointed tip broke into a speck at 2x)."""
    n = 10
    bm = props.bmesh.new()
    rows = []
    for j in range(n + 1):
        t = j / n
        hh = h * (1 - 0.6 * t)
        rows.append([bm.verts.new((0.06 * t * math.sin(5 * t + phase), top[1] - w * t, top[2] - hh * k)) for k in (0.0, 1.0)])
    for j in range(n):
        bm.faces.new((rows[j][0], rows[j + 1][0], rows[j + 1][1], rows[j][1]))
    p.add(bm, 'tint', smooth=True, tint=True)


def curragh_hide(colors, **kw):
    """The realism pass's Curragh: a seagoing Irish hide boat (6th-10th c.):
    a deep, strongly sheered hull of tanned ox hides in stitched panels on an
    ash frame, a light ash gunwale, thwarts, a hide shelter aft, a big steering
    oar on the starboard quarter, a mainmast and a raked foremast with square
    sails bearing the civ Celtic cross."""
    g = dict(HIDEBOAT, **kw)
    c = dict(SHIP_COLORS, **HIDE_COLORS)
    c.update(colors)
    root = vkit.root('Curragh')
    root['fit_scale'] = g['fit_scale']
    p = vkit.part(root, 'hull', c, SHIP_WEAR)
    hull = Hull(L=g['L'], B=g['B'], D=g['D'], draft=g['draft'], sheer_stern=g['sheer_stern'],
                sheer_bow=g['sheer_bow'], bow_p=1.6, stern_p=1.6, fullness=1.0, tumblehome=-g['flare'])   # tapered, upswept ends
    py, pz = g['panel_y'], g['panel_z']

    def deck(y):
        return hull.deck_z(hull.s_of(y))

    ss = [-1 + 2 * i / 48 for i in range(49)]
    bm = props.loft([hull.section(s, m=32) for s in ss])

    def hkeys(f):
        cc = f.calc_center_median()
        if f.normal.z > 0.85:
            return ('deck', False)
        if cc.z < 0.012:
            return ('hull_grime', False)
        if cc.z < 0.04:
            return ('hide_low', False)   # grease- and water-darkened below
        # 5 big hides along the length, their joins wavering; a faint shadow at each rib
        col = int(math.floor((cc.y + g['L'] / 2 + 0.04 * math.sin(cc.z * 40)) / (g['L'] / g['hides'])))
        if any(abs(cc.y - ry) < 0.018 for ry in g['ribs']) and cc.z > 0.05:   # a few, irregular
            return ('oxhide_rib', False)
        return ('oxhide_alt', False) if col % 2 else None

    p.add(bm, 'oxhide', smooth=True, keys=hkeys)
    # the stitched seams: one long seam along the hull and the joins between the big hides
    for sx in (-1, 1):
        for zc in (g['D'] * 0.45,):
            path = []
            for i_ in range(1, 60):
                s_ = -0.95 + 1.9 * i_ / 60
                if zc < hull.deck_z(s_) - 0.02:
                    path.append((sx * (hull.side_x(s_, zc) + 0.003), hull.y(s_), zc))
            p.add(props.tube(path, 0.008, seg=5), 'stitch', smooth=True)   # the thread-coloured long seam
            for q in range(2, len(path) - 2, 3):   # the flax stitches across it
                p.add(props.box(0.004, 0.008, 0.02), 'stitch', T(path[q][0] + sx * 0.004, path[q][1], path[q][2]), bevel=0.001)
        for k in range(1, g['hides']):
            y = -g['L'] / 2 + g['L'] * k / g['hides']
            s_ = hull.s_of(y)
            path = [(sx * (hull.side_x(s_, z) + 0.003), y - 0.04 * math.sin(z * 40), z)
                    for z in [0.01 + (deck(y) - 0.03) * j_ / 6 for j_ in range(7)]]
            p.add(props.tube(path, 0.007, seg=5), 'stitch', smooth=True)
            for q in range(1, len(path) - 1):
                p.add(props.box(0.004, 0.02, 0.008), 'stitch', T(path[q][0] + sx * 0.004, path[q][1], path[q][2]), bevel=0.001)
        # the frame's ribs showing over the gunwale
        for k in range(int(g['L'] / g['rib']) + 1):
            y = -g['L'] / 2 + g['rib'] * (k + 0.5)
            s_ = hull.s_of(y)
            if abs(s_) < 0.85:
                p.add(props.box(0.022, 0.022, 0.03), 'wood_dark', T(sx * (hull.deck_half(s_) - 0.012), y, hull.deck_z(s_) + 0.012), bevel=0.004)
        # the ash gunwale: one continuous light cap along the sheer
        path = [(sx * (hull.deck_half(s) + 0.006), hull.y(s), hull.deck_z(s) + 0.004) for s in [-0.97 + 1.94 * i / 60 for i in range(61)]]
        p.add(props.tube(path, 0.022, seg=8), 'ash', smooth=True)
    # stem and stern posts, a little proud of the hide
    for sgn in (1, -1):
        ye = sgn * g['L'] / 2
        post = [(0, ye - sgn * 0.04, 0.0), (0, ye + sgn * 0.01, deck(ye) * 0.6), (0, ye + sgn * 0.035, deck(ye) + 0.05), (0, ye + sgn * 0.015, deck(ye) + 0.085)]
        p.add(props.tube(post, [0.026, 0.025, 0.023, 0.02], seg=10), 'ash', smooth=True)   # rounded, curling in
    # thwarts (rowing benches) across the open hull, a hide shelter over the stern
    for i in range(g['thwarts']):
        y = -0.42 + 0.84 * i / (g['thwarts'] - 1)
        s = hull.s_of(y)
        p.add(props.box(hull.deck_half(s) * 1.9, 0.04, 0.016), 'thwart', T(0, y, deck(y) + 0.008), bevel=0.003)
    arch = []
    for yy in [-0.5 - 0.22 * i / 5 for i in range(6)]:
        z0 = deck(yy) - 0.02
        hw = hull.deck_half(hull.s_of(yy)) * 0.9
        arch.append([(hw * math.cos(math.pi * k / 10), yy, z0 + 0.09 * math.sin(math.pi * k / 10)) for k in range(11)])
    p.add(props.loft(arch), 'shelter', smooth=True)
    # the big steering oar on the starboard quarter
    a = (hull.deck_half(hull.s_of(-0.62)) + 0.01, -0.62, deck(-0.62) + 0.05)
    e = (0.27, -0.9, -0.02)
    vkit.rod(p, a, e, 0.024, 'oar_w', seg=8)
    d = (Vector(e) - Vector(a)).normalized()
    m, _ = vkit.align(Vector(e) - d * 0.14, Vector(e) + d * 0.02)
    p.add(props.box(0.022, 0.13, 0.22), 'oar_w', m, bevel=0.004)   # the broad steering blade
    # rowing oars, three a side, raked aft into the water
    for i in range(g['oars']):
        y = -0.2 + 0.56 * i / max(1, g['oars'] - 1)
        s_ = hull.s_of(y)
        for sx in (-1, 1):
            a = (sx * (hull.deck_half(s_) + 0.01), y, deck(y) + 0.02)
            e = (sx * (hull.deck_half(s_) + 0.34), y - 0.14, deck(y) + 0.02 - 0.34 * math.sin(math.radians(22)))   # 22 deg down, mid-stroke: never under the keel
            vkit.rod(p, a, e, 0.018, 'oar_w', seg=6)
            d = (Vector(e) - Vector(a)).normalized()
            m, _ = vkit.align(Vector(e) - d * 0.06, Vector(e) + d * 0.02)
            p.add(props.box(0.04, 0.013, 0.07), 'oar_w', m, bevel=0.003)
    # masts
    mm, fm = g['main'], g['fore']
    vkit.rod(p, (0, mm['y'], deck(mm['y']) - 0.08), (0, mm['y'], mm['top'] + 0.04), 0.022, 'wood_dark', r1=0.017, seg=10)
    rk = math.radians(fm['rake'])
    ftop = (0, fm['y'] + math.sin(rk) * (fm['top'] - deck(fm['y'])), fm['top'] + 0.03)
    vkit.rod(p, (0, fm['y'], deck(fm['y']) - 0.08), ftop, 0.019, 'wood_dark', r1=0.015, seg=10)
    for name in ('mm', 'fm'):
        p.add(props.lathe([(0.0, 0.022), (0.025, 0.026), (0.04, 0.0)], seg=12), 'tar',
              T(*((0, mm['y'], mm['top'] + 0.03) if name == 'mm' else ftop)), smooth=True)
    if g['pennant']:
        _pennant_solid(p, (0, mm['y'], mm['top'] + 0.06), *g['pennant'])
    p.finish()
    # the sails (square, the civ Celtic cross), each its own part on its mast
    for spec, y, tilt in ((mm, mm['y'], 0.0), (fm, fm['y'], fm['rake'])):
        r = vkit.part(root, 'sail', c, SHIP_WEAR)
        _square_sail(r, spec['w'], spec['h'], spec['top'], g['billow'], 'celtic', bands=1.5, brails=0, line_r=0.009,
                     seed=int(10 * spec['h']))
        w, h, zt, bl = spec['w'], spec['h'], spec['top'], g['billow']
        def sp(u, v):
            return (u * w / 2, 0.035 + bl * (1 - u * u) * (0.3 + 0.7 * math.sin(math.pi * min(1.0, v * 1.05))), zt - v * h)
        for edge in ([sp(-1, j / 8) for j in range(9)], [sp(1, j / 8) for j in range(9)], [sp(-1 + i / 6, 1) for i in range(13)]):
            r.add(props.tube(edge, 0.008, seg=5), 'brail', smooth=True)   # the bolt rope: a reinforced edge
        o = r.finish()
        o.location = (0, y, 0)
        o.rotation_euler = (math.radians(-tilt), 0, math.radians(g['fore_brace'] if spec is fm else g['main_brace']))
    return root


def dromon(colors):
    """A Byzantine dromon: a long two-banked war galley, a fighting tower of
    civ and gilt stripes at bow and stern, a bronze ram and siphon, two
    banks of oars, a big square sail with the civ fleur and banded edges."""
    c = dict(SHIP_COLORS, **GALLEY_COLORS)
    c.update(colors)
    root = vkit.root('Dromon')
    p = vkit.part(root, 'hull', c, SHIP_WEAR)
    hull = Hull(L=2.8, B=0.56, D=0.26, draft=0.11, sheer_stern=0.24, sheer_bow=0.14, bow_p=1.5, stern_p=1.6,
                fullness=0.8)
    add_hull(p, hull, stripes=[(-0.03, 0.015, 'hull_grime'), (0.015, 0.045, 'gold'), (0.12, 0.14, 'wood_dark'),
                               (0.2, 0.235, 'gold')], plank=0.04)
    # barrel canopies (half-cylinders over hoops) at bow and stern, striped civ color and gilt, as the original
    for y0, y1 in ((-1.25, -0.8), (0.82, 1.18)):
        yc = (y0 + y1) / 2
        s = hull.s_of(yc)
        z0 = hull.deck_z(s)
        r = hull.deck_half(s) * 0.95
        arch = []
        for q in range(9):
            yy = y0 + (y1 - y0) * q / 8
            arch.append([(r * math.cos(math.pi * a / 14), yy, z0 + 0.04 + r * 1.1 * math.sin(math.pi * a / 14)) for a in range(15)])
        canopy = props.loft(arch, False, False)
        props.bmesh.ops.solidify(canopy, geom=canopy.faces[:], thickness=0.012)
        p.add(canopy, 'tint', smooth=True, tint=True,
              keys=lambda f: ('gold', False) if int((f.calc_center_median().y + 5) / 0.075) % 2 else None)
        for yy in (y0, y1):   # the hoops at the ends
            ring = [(r * 1.01 * math.cos(math.pi * a / 14), yy, z0 + 0.04 + r * 1.11 * math.sin(math.pi * a / 14)) for a in range(15)]
            p.add(props.tube(ring, 0.012, seg=6), 'wood_dark', smooth=True)
    # the serpent-headed siphon at the prow: a dark neck curling up to a gilt-eyed head
    neck = [(0, 1.18, 0.34), (0, 1.3, 0.42), (0, 1.36, 0.54), (0, 1.33, 0.64)]
    p.add(props.tube(neck, [0.045, 0.04, 0.036, 0.03], seg=10), 'wood_dark', smooth=True)
    p.add(props.ellipsoid(0.05, 0.085, 0.045, seg=14), 'wood_dark', T(0, 1.37, 0.66), smooth=True)
    p.add(props.lathe([(0.0, 0.028), (0.08, 0.018), (0.1, 0.026)], seg=10), 'gold', T(0, 1.43, 0.65) @ R('X', -95), smooth=True)
    for sx in (-1, 1):
        p.add(props.ellipsoid(0.012, 0.012, 0.012, seg=8), 'gold', T(sx * 0.04, 1.39, 0.68), smooth=True)    # the ram and the fire siphon at the bow
    p.add(props.lathe([(0.0, 0.065), (0.14, 0.055), (0.34, 0.04), (0.46, 0.006)], seg=12, scale_xy=(0.75, 1.0)), 'gold',
          T(0, 1.24, 0.04) @ R('X', -90), smooth=True)
    p.add(props.lathe([(0.0, 0.025), (0.18, 0.02), (0.22, 0.035)], seg=10), 'gold', T(0, 1.22, 0.5) @ R('X', -70),
          smooth=True)
    curl = [(0, -1.32, 0.42), (0, -1.42, 0.58), (0, -1.45, 0.74), (0, -1.38, 0.86), (0, -1.3, 0.84)]
    p.add(props.tube(curl, [0.045, 0.04, 0.035, 0.03, 0.02], seg=10), 'gold', smooth=True)
    # two banks of oars
    for bank, (zo, reach) in enumerate(((0.06, 0.48), (-0.03, 0.4))):
        for i in range(12):
            y = -0.72 + 1.5 * i / 11
            s = hull.s_of(y)
            for sx in (-1, 1):
                a = (sx * hull.half_beam(s) * 0.97, y, hull.deck_z(s) + zo - 0.08 * bank)
                e = (sx * (hull.half_beam(s) + reach), y - 0.04, -0.07)
                vkit.rod(p, a, e, 0.009, 'oar', seg=6)
                p.add(props.box(0.012, 0.04, 0.1), 'oar', T(*e) @ R('Y', -sx * 55), bevel=0.002)
    vkit.rod(p, (0, 0.0, 0.22), (0, 0.0, 1.75), 0.04, 'wood_dark', seg=10)
    p.add(props.lathe([(0.0, 0.03), (0.03, 0.07), (0.1, 0.075), (0.14, 0.045), (0.18, 0.0)], seg=16), 'gold',
          T(0, 0.0, 1.74), smooth=True)
    sail(p, 0.03, 1.66, 1.75, 1.75, 0.95, 'flames', billow=0.15, tint_borders=True)
    for z in (0.5, 0.8, 1.1):   # civ and gilt bands on the mast
        p.add(props.lathe([(0.0, 0.045), (0.05, 0.045)], seg=12), 'tint', T(0, 0.0, z), smooth=True, tint=True)
        p.add(props.lathe([(0.0, 0.046), (0.02, 0.046)], seg=12), 'gold', T(0, 0.0, z + 0.05), smooth=True)
    pennant(p, (0, 0.0, 1.9), 0.6, 0.16)
    vkit.rod(p, (0, 0.0, 1.72), (0, 1.3, 0.5), 0.006, 'cordage', seg=4)
    vkit.rod(p, (0, 0.0, 1.72), (0, -1.35, 0.8), 0.006, 'cordage', seg=4)
    p.finish()
    return root


def dromon(colors):
    """A Byzantine dromon: a long two-banked war galley, a fighting tower of
    civ and gilt stripes at bow and stern, a bronze ram and siphon, two
    banks of oars, a big square sail with the civ fleur and banded edges."""
    c = dict(SHIP_COLORS, **GALLEY_COLORS)
    c.update(colors)
    root = vkit.root('Dromon')
    p = vkit.part(root, 'hull', c, SHIP_WEAR)
    hull = Hull(L=2.8, B=0.56, D=0.26, draft=0.11, sheer_stern=0.24, sheer_bow=0.14, bow_p=1.5, stern_p=1.6,
                fullness=0.8)
    add_hull(p, hull, stripes=[(-0.03, 0.015, 'hull_grime'), (0.015, 0.045, 'gold'), (0.12, 0.14, 'wood_dark'),
                               (0.2, 0.235, 'gold')], plank=0.04)
    # barrel canopies (half-cylinders over hoops) at bow and stern, striped civ color and gilt, as the original
    for y0, y1 in ((-1.25, -0.8), (0.82, 1.18)):
        yc = (y0 + y1) / 2
        s = hull.s_of(yc)
        z0 = hull.deck_z(s)
        r = hull.deck_half(s) * 0.95
        arch = []
        for q in range(9):
            yy = y0 + (y1 - y0) * q / 8
            arch.append([(r * math.cos(math.pi * a / 14), yy, z0 + 0.04 + r * 1.1 * math.sin(math.pi * a / 14)) for a in range(15)])
        canopy = props.loft(arch, False, False)
        props.bmesh.ops.solidify(canopy, geom=canopy.faces[:], thickness=0.012)
        p.add(canopy, 'tint', smooth=True, tint=True,
              keys=lambda f: ('gold', False) if int((f.calc_center_median().y + 5) / 0.075) % 2 else None)
        for yy in (y0, y1):   # the hoops at the ends
            ring = [(r * 1.01 * math.cos(math.pi * a / 14), yy, z0 + 0.04 + r * 1.11 * math.sin(math.pi * a / 14)) for a in range(15)]
            p.add(props.tube(ring, 0.012, seg=6), 'wood_dark', smooth=True)
    # the serpent-headed siphon at the prow: a dark neck curling up to a gilt-eyed head
    neck = [(0, 1.18, 0.34), (0, 1.3, 0.42), (0, 1.36, 0.54), (0, 1.33, 0.64)]
    p.add(props.tube(neck, [0.045, 0.04, 0.036, 0.03], seg=10), 'wood_dark', smooth=True)
    p.add(props.ellipsoid(0.05, 0.085, 0.045, seg=14), 'wood_dark', T(0, 1.37, 0.66), smooth=True)
    p.add(props.lathe([(0.0, 0.028), (0.08, 0.018), (0.1, 0.026)], seg=10), 'gold', T(0, 1.43, 0.65) @ R('X', -95), smooth=True)
    for sx in (-1, 1):
        p.add(props.ellipsoid(0.012, 0.012, 0.012, seg=8), 'gold', T(sx * 0.04, 1.39, 0.68), smooth=True)    # the ram and the fire siphon at the bow
    p.add(props.lathe([(0.0, 0.065), (0.14, 0.055), (0.34, 0.04), (0.46, 0.006)], seg=12, scale_xy=(0.75, 1.0)), 'gold',
          T(0, 1.24, 0.04) @ R('X', -90), smooth=True)
    p.add(props.lathe([(0.0, 0.025), (0.18, 0.02), (0.22, 0.035)], seg=10), 'gold', T(0, 1.22, 0.5) @ R('X', -70),
          smooth=True)
    curl = [(0, -1.32, 0.42), (0, -1.42, 0.58), (0, -1.45, 0.74), (0, -1.38, 0.86), (0, -1.3, 0.84)]
    p.add(props.tube(curl, [0.045, 0.04, 0.035, 0.03, 0.02], seg=10), 'gold', smooth=True)
    # two banks of oars
    for bank, (zo, reach) in enumerate(((0.06, 0.48), (-0.03, 0.4))):
        for i in range(12):
            y = -0.72 + 1.5 * i / 11
            s = hull.s_of(y)
            for sx in (-1, 1):
                a = (sx * hull.half_beam(s) * 0.97, y, hull.deck_z(s) + zo - 0.08 * bank)
                e = (sx * (hull.half_beam(s) + reach), y - 0.04, -0.07)
                vkit.rod(p, a, e, 0.009, 'oar', seg=6)
                p.add(props.box(0.012, 0.04, 0.1), 'oar', T(*e) @ R('Y', -sx * 55), bevel=0.002)
    vkit.rod(p, (0, 0.0, 0.22), (0, 0.0, 1.75), 0.04, 'wood_dark', seg=10)
    p.add(props.lathe([(0.0, 0.03), (0.03, 0.07), (0.1, 0.075), (0.14, 0.045), (0.18, 0.0)], seg=16), 'gold',
          T(0, 0.0, 1.74), smooth=True)
    sail(p, 0.03, 1.66, 1.75, 1.75, 0.95, 'flames', billow=0.15, tint_borders=True)
    for z in (0.5, 0.8, 1.1):   # civ and gilt bands on the mast
        p.add(props.lathe([(0.0, 0.045), (0.05, 0.045)], seg=12), 'tint', T(0, 0.0, z), smooth=True, tint=True)
        p.add(props.lathe([(0.0, 0.046), (0.02, 0.046)], seg=12), 'gold', T(0, 0.0, z + 0.05), smooth=True)
    pennant(p, (0, 0.0, 1.9), 0.6, 0.16)
    vkit.rod(p, (0, 0.0, 1.72), (0, 1.3, 0.5), 0.006, 'cordage', seg=4)
    vkit.rod(p, (0, 0.0, 1.72), (0, -1.35, 0.8), 0.006, 'cordage', seg=4)
    p.finish()
    return root


def dromon(colors):
    """A Byzantine dromon: a long two-banked war galley, a fighting tower of
    civ and gilt stripes at bow and stern, a bronze ram and siphon, two
    banks of oars, a big square sail with the civ fleur and banded edges."""
    c = dict(SHIP_COLORS, **GALLEY_COLORS)
    c.update(colors)
    root = vkit.root('Dromon')
    p = vkit.part(root, 'hull', c, SHIP_WEAR)
    hull = Hull(L=2.8, B=0.56, D=0.26, draft=0.11, sheer_stern=0.24, sheer_bow=0.14, bow_p=1.5, stern_p=1.6,
                fullness=0.8)
    add_hull(p, hull, stripes=[(-0.03, 0.015, 'hull_grime'), (0.015, 0.045, 'gold'), (0.12, 0.14, 'wood_dark'),
                               (0.2, 0.235, 'gold')], plank=0.04)
    # barrel canopies (half-cylinders over hoops) at bow and stern, striped civ color and gilt, as the original
    for y0, y1 in ((-1.25, -0.8), (0.82, 1.18)):
        yc = (y0 + y1) / 2
        s = hull.s_of(yc)
        z0 = hull.deck_z(s)
        r = hull.deck_half(s) * 0.95
        arch = []
        for q in range(9):
            yy = y0 + (y1 - y0) * q / 8
            arch.append([(r * math.cos(math.pi * a / 14), yy, z0 + 0.04 + r * 1.1 * math.sin(math.pi * a / 14)) for a in range(15)])
        canopy = props.loft(arch, False, False)
        props.bmesh.ops.solidify(canopy, geom=canopy.faces[:], thickness=0.012)
        p.add(canopy, 'tint', smooth=True, tint=True,
              keys=lambda f: ('gold', False) if int((f.calc_center_median().y + 5) / 0.075) % 2 else None)
        for yy in (y0, y1):   # the hoops at the ends
            ring = [(r * 1.01 * math.cos(math.pi * a / 14), yy, z0 + 0.04 + r * 1.11 * math.sin(math.pi * a / 14)) for a in range(15)]
            p.add(props.tube(ring, 0.012, seg=6), 'wood_dark', smooth=True)
    # the serpent-headed siphon at the prow: a dark neck curling up to a gilt-eyed head
    neck = [(0, 1.18, 0.34), (0, 1.3, 0.42), (0, 1.36, 0.54), (0, 1.33, 0.64)]
    p.add(props.tube(neck, [0.045, 0.04, 0.036, 0.03], seg=10), 'wood_dark', smooth=True)
    p.add(props.ellipsoid(0.05, 0.085, 0.045, seg=14), 'wood_dark', T(0, 1.37, 0.66), smooth=True)
    p.add(props.lathe([(0.0, 0.028), (0.08, 0.018), (0.1, 0.026)], seg=10), 'gold', T(0, 1.43, 0.65) @ R('X', -95), smooth=True)
    for sx in (-1, 1):
        p.add(props.ellipsoid(0.012, 0.012, 0.012, seg=8), 'gold', T(sx * 0.04, 1.39, 0.68), smooth=True)    # the ram and the fire siphon at the bow
    p.add(props.lathe([(0.0, 0.065), (0.14, 0.055), (0.34, 0.04), (0.46, 0.006)], seg=12, scale_xy=(0.75, 1.0)), 'gold',
          T(0, 1.24, 0.04) @ R('X', -90), smooth=True)
    p.add(props.lathe([(0.0, 0.025), (0.18, 0.02), (0.22, 0.035)], seg=10), 'gold', T(0, 1.22, 0.5) @ R('X', -70),
          smooth=True)
    curl = [(0, -1.32, 0.42), (0, -1.42, 0.58), (0, -1.45, 0.74), (0, -1.38, 0.86), (0, -1.3, 0.84)]
    p.add(props.tube(curl, [0.045, 0.04, 0.035, 0.03, 0.02], seg=10), 'gold', smooth=True)
    # two banks of oars
    for bank, (zo, reach) in enumerate(((0.06, 0.48), (-0.03, 0.4))):
        for i in range(12):
            y = -0.72 + 1.5 * i / 11
            s = hull.s_of(y)
            for sx in (-1, 1):
                a = (sx * hull.half_beam(s) * 0.97, y, hull.deck_z(s) + zo - 0.08 * bank)
                e = (sx * (hull.half_beam(s) + reach), y - 0.04, -0.07)
                vkit.rod(p, a, e, 0.009, 'oar', seg=6)
                p.add(props.box(0.012, 0.04, 0.1), 'oar', T(*e) @ R('Y', -sx * 55), bevel=0.002)
    vkit.rod(p, (0, 0.0, 0.22), (0, 0.0, 1.75), 0.04, 'wood_dark', seg=10)
    p.add(props.lathe([(0.0, 0.03), (0.03, 0.07), (0.1, 0.075), (0.14, 0.045), (0.18, 0.0)], seg=16), 'gold',
          T(0, 0.0, 1.74), smooth=True)
    sail(p, 0.03, 1.66, 1.75, 1.75, 0.95, 'flames', billow=0.15, tint_borders=True)
    for z in (0.5, 0.8, 1.1):   # civ and gilt bands on the mast
        p.add(props.lathe([(0.0, 0.045), (0.05, 0.045)], seg=12), 'tint', T(0, 0.0, z), smooth=True, tint=True)
        p.add(props.lathe([(0.0, 0.046), (0.02, 0.046)], seg=12), 'gold', T(0, 0.0, z + 0.05), smooth=True)
    pennant(p, (0, 0.0, 1.9), 0.6, 0.16)
    vkit.rod(p, (0, 0.0, 1.72), (0, 1.3, 0.5), 0.006, 'cordage', seg=4)
    vkit.rod(p, (0, 0.0, 1.72), (0, -1.35, 0.8), 0.006, 'cordage', seg=4)
    p.finish()
    return root


# ------------------------------------------------------------ dromon (realism pass: a Byzantine war galley)

# A 10th-century dromon (notes/realism/Dromon.md): ~31 x 4.5 m, two banks of oars, fully decked, a
# wooden castle by the mainmast, a Greek-fire siphon in the bow over a spur, two lateen sails.
# L 2.8 = 31 m (1 unit = 11 m).
DROMON = dict(
    L=2.8, B=0.46, D=0.17, draft=0.09, sheer_stern=0.26, sheer_bow=0.16,
    bulwark_h=0.07, oars=12, oar_r=0.022, oar_r_low=0.014, reach=0.26, upper_tip_z=0.13, sweep=-0.1,
    # wing and wing: running before the wind, the two lateens set out to opposite sides
    # both yards braced to the same side, as sailing; the foresail the bigger (a dromon's arrangement)
    main=dict(y=-0.36, top=0.86, yard=1.15, ang=33.0, drop=0.53, yaw=29.0, fwd=0.3),
    fore=dict(y=0.85, top=1.02, yard=1.67, ang=33.0, drop=0.71, yaw=29.0, fwd=0.36),
    billow=0.22, sail_band=0.3, device='pattee', castle_h=0.4,
    fit_scale=1.02,
)

DROMON_COLORS = {
    'wood': '#86622f', 'wood_alt': '#805c2c', 'wood_low': '#6e4e2a', 'wood_low_alt': '#664828',
    'wood_dark': '#3a2a1a', 'wale': '#2a2018', 'tar': '#2e261e', 'post': '#6e5232', 'post_hl': '#c8a060',
    'bulwark': '#7c5a30', 'ash': '#b8945e', 'deck': '#a08050', 'deck_alt': '#8e6e42',
    'oar': '#2a1e14', 'oar_low': '#d4b888', 'gangway': '#6a4e30', 'oar_port': '#141210', 'castle': '#8a6436', 'castle_dark': '#4a3420',
    'sail': '#e2d3ae', 'sail_seam': '#c4b48e', 'sail_mid': '#cbbb95', 'sail_shade': '#bba982',
    'gold': '#c8a048', 'siphon': '#7a5c2c', 'cordage': '#54442f',
}
for _k, _v in (('bulwark', ('#7c5a30', 0.65, 0.0, 'wood')), ('castle', ('#8a6436', 0.65, 0.0, 'wood')),
               ('castle_dark', ('#4a3420', 0.7, 0.0, 'wood')), ('siphon', ('#a88240', 0.33, 1.0, 'metal')),
               ('ash', ('#b8945e', 0.6, 0.0, 'wood')), ('wood_low', ('#7e5e38', 0.62, 0.0, 'wood')),
               ('wood_low_alt', ('#735432', 0.62, 0.0, 'wood')), ('wale', ('#2a2018', 0.62, 0.0, 'wood')),
               ('tar', ('#2e261e', 0.7, 0.0, 'wood')), ('post', ('#6e5232', 0.6, 0.0, 'wood')),
               ('post_hl', ('#c8a060', 0.55, 0.0, 'wood')), ('oar_low', ('#c4a474', 0.62, 0.0, 'wood')),
               ('oar_port', ('#141210', 0.9, 0.0, None)), ('deck_alt', ('#907042', 0.7, 0.0, 'wood')),
               ('sail_mid', ('#cbbb95', 0.9, 0.0, 'cloth')), ('sail_shade', ('#bba982', 0.9, 0.0, 'cloth'))):
    props.DEFAULTS.setdefault(_k, _v)
props.DEFAULTS.setdefault('gangway', ('#6a4e30', 0.7, 0.0, 'wood'))


def _lateen_sail(p, yard, ang, drop, billow, band, device, seg=26, fwd_frac=0.42):
    """A lateen sail in local space (the mast on the z axis, the yard's crossing
    point at the origin's top): the yard runs forward-and-down at `ang` deg, 0.4
    of it forward of the mast; the sail hangs from it to a clew aft and low,
    bellying to +x. A civ band along the head (the yard) and a civ device in
    the middle; light-to-shadow by the belly."""
    a = math.radians(ang)
    fwd = (0.0, math.cos(a) * yard * fwd_frac, -math.sin(a) * yard * fwd_frac)
    aft = (0.0, -math.cos(a) * yard * (1 - fwd_frac), math.sin(a) * yard * (1 - fwd_frac))
    clew = (0.0, aft[1] * 0.55, -drop)
    bm = props.bmesh.new()
    grid = []
    for j in range(seg + 1):
        v = j / seg
        row = []
        for i in range(seg - j + 1):
            u = i / seg
            w0 = 1 - u - v
            pt = [fwd[k] * w0 + aft[k] * u + clew[k] * v for k in range(3)]
            pt[0] += billow * 4 * w0 * u * (1 - v) ** 0.5 + billow * 2.5 * v * (w0 + u) * 0.5   # the belly
            row.append(bm.verts.new(pt))
        grid.append(row)
    for j in range(seg):
        for i in range(seg - j):
            bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i]))
            if i < seg - j - 1:
                bm.faces.new((grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
    mask = EMBLEMS.get(device)
    cy = (fwd[1] + aft[1] + clew[1]) / 3
    cz = (fwd[2] + aft[2] + clew[2]) / 3
    # distance from the head (the yard line) in the sail plane
    hy, hz = aft[1] - fwd[1], aft[2] - fwd[2]
    hl = math.hypot(hy, hz)

    def keys(f):
        c = f.calc_center_median()
        dist = abs((c.y - fwd[1]) * hz - (c.z - fwd[2]) * hy) / hl
        if dist < band * drop:
            return ('tint', True)
        if mask and mask(-(c.y - cy), c.z - cz + drop * 0.1, drop * 0.72):
            return ('tint', True)
        if abs((c.y / 0.12) % 1.0 - 0.5) > 0.44:
            return ('sail_seam', False)
        k = abs(c.x) / max(abs(billow) * 1.6, 1e-6)
        return ('sail_mid', False) if k < 0.35 else None

    p.add(bm, 'sail', smooth=True, keys=keys)
    vkit.rod(p, (fwd[0], fwd[1] + math.cos(a) * 0.04, fwd[2] - math.sin(a) * 0.04),
             (aft[0], aft[1] - math.cos(a) * 0.04, aft[2] + math.sin(a) * 0.04), 0.018, 'wood_dark', seg=8)
    for end in (fwd, aft):
        p.add(props.ellipsoid(0.02, 0.02, 0.02, seg=8), 'tar', T(*end), smooth=True)


def dromon_lateen(colors, **kw):
    """The realism pass's Dromon: a Byzantine war galley (c. 900-1000): a long,
    decked, sheered hull in warm wood with a dark wale, a bulwark with the
    upper bank's ports and the lower bank's ports below the wale (dark upper
    oars, light lower), a wooden castle by the mainmast, the Greek-fire siphon
    with a gilt beast head on the forecastle over a spur, a curling sternpost,
    two steering oars and a civ stern canopy; two lateen sails with a civ band
    along the yard and a civ cross."""
    g = dict(DROMON, **kw)
    c = dict(SHIP_COLORS, **DROMON_COLORS)
    c.update(colors)
    root = vkit.root('Dromon')
    root['fit_scale'] = g['fit_scale']
    p = vkit.part(root, 'hull', c, SHIP_WEAR)
    hull = Hull(L=g['L'], B=g['B'], D=g['D'], draft=g['draft'], sheer_stern=g['sheer_stern'],
                sheer_bow=g['sheer_bow'], bow_p=1.4, stern_p=1.5, fullness=0.85)
    yb = g['L'] / 2

    def deck(y):
        return hull.deck_z(hull.s_of(y))

    ss = [-1 + 2 * i / 50 for i in range(51)]
    bm = props.loft([hull.section(s, m=32) for s in ss])

    def hkeys(f):
        cc = f.calc_center_median()
        if f.normal.z > 0.85:
            return ('deck_alt', False) if int(abs(cc.x) / 0.03) % 2 else ('deck', False)
        if cc.z < 0.012:
            return ('hull_grime', False)
        if cc.z < 0.05:
            return ('tar', False)
        zd = hull.deck_z(hull.s_of(cc.y))
        low = cc.z < zd - 0.045
        if int(math.floor(cc.z / 0.024)) % 2:
            return ('wood_low_alt' if low else 'wood_alt', False)
        return ('wood_low', False) if low else None

    p.add(bm, 'wood', smooth=True, keys=hkeys)
    bh = g['bulwark_h']
    by0, by1 = -yb * 0.78, yb * 0.72
    ys = [by0 + (by1 - by0) * i / 40 for i in range(41)]
    for sx in (-1, 1):
        # the wale (one dark band) and the bulwark with a light cap
        path = []
        for i in range(1, 60):
            s_ = -0.94 + 1.88 * i / 60
            z = hull.deck_z(s_) - 0.03
            path.append((sx * (hull.side_x(s_, z) + 0.004), hull.y(s_), z))
        p.add(props.tube(path, 0.016, seg=6, scale_xy=(1.0, 0.45)), 'wale', smooth=True)
        p.add(props.tube([(x_ + sx * 0.004, y_, z_ + 0.022) for x_, y_, z_ in path], 0.009, seg=6), 'ash', smooth=True)   # a light line between the tiers
        for zc in (0.07, 0.11):   # strake lines below
            pth = [(sx * (hull.side_x(hull.s_of(y_), zc) + 0.003), y_, zc) for _, y_, _ in path if zc < deck(y_) - 0.06]
            if len(pth) > 2:
                p.add(props.tube(pth, 0.004, seg=5), 'wale', smooth=True)
        rings = []
        for y in ys:
            s = hull.s_of(y)
            xo = sx * (hull.deck_half(s) + 0.004)
            xi = xo - sx * 0.014
            z0 = deck(y) - 0.008
            rings.append([(xo, y, z0), (xo, y, z0 + bh), (xi, y, z0 + bh), (xi, y, z0)])
        p.add(props.loft(rings), 'bulwark', bevel=0.003)
        p.add(props.tube([(sx * (hull.deck_half(hull.s_of(y)) - 0.003), y, deck(y) - 0.008 + bh + 0.006) for y in ys],
                         0.012, seg=8, scale_xy=(1.0, 1.2)), 'ash', smooth=True)
    p.add(props.loft([[(0.06, y, deck(y) + 0.006), (-0.06, y, deck(y) + 0.006), (-0.06, y, deck(y) - 0.004), (0.06, y, deck(y) - 0.004)]
                      for y in ys]), 'gangway')   # the central gangway, a darker plank run
    # two banks of oars, parallel, separated by height and value
    no = g['oars']
    for bank in (0, 1):
        for i in range(no):
            y = -0.85 + 1.6 * (i + 0.5 * bank) / (no - 0.5)
            s = hull.s_of(y)
            for sx in (-1, 1):
                if bank == 0:
                    z = deck(y) + 0.03
                    a = (sx * (hull.deck_half(s) + 0.004), y, z)
                    e = (sx * (hull.deck_half(s) + g['reach']), y + g['sweep'], g['upper_tip_z'])
                    r, key = g['oar_r'], 'oar'
                    p.add(props.box(0.01, 0.03, 0.026), 'oar_port', T(sx * (hull.deck_half(s) + 0.006), y, z), bevel=0.003)
                else:
                    z = deck(y) - 0.095
                    xs = hull.side_x(s, z)
                    a = (sx * xs, y, z)
                    e = (sx * (xs + g['reach'] * 0.62), y + g['sweep'], -0.01)
                    r, key = g['oar_r_low'], 'oar_low'
                    p.add(props.box(0.012, 0.034, 0.028), 'oar_port', T(sx * (xs + 0.002), y, z), bevel=0.003)
                vkit.rod(p, a, e, r, key, seg=6)
                d = (Vector(e) - Vector(a)).normalized()
                m, _ = vkit.align(Vector(e) - d * 0.045, Vector(e) + d * 0.02)
                p.add(props.box(0.038, r * 0.9, 0.065), key, m, bevel=0.003)
    # the forecastle platform with the siphon: a bronze tube ending in a gilt beast head; the spur below
    yf = yb * 0.8
    zf = deck(yf) + bh
    p.add(props.box(hull.deck_half(hull.s_of(yf)) * 1.8, 0.22, 0.03), 'castle', T(0, yf, zf - 0.01), bevel=0.006)
    p.add(props.box(0.16, 0.16, 0.07), 'castle_dark', T(0, yf + 0.02, zf + 0.04), bevel=0.006)   # the raised siphon platform
    zs = zf + 0.075
    neck = [(0, yf - 0.02, zs + 0.03), (0, yf + 0.1, zs + 0.05), (0, yb + 0.1, zs + 0.06)]
    p.add(props.tube(neck, [0.034, 0.032, 0.03], seg=12), 'siphon', smooth=True)   # a short dark-bronze tube
    p.add(props.ellipsoid(0.055, 0.085, 0.05, seg=16), 'siphon', T(0, yb + 0.15, zs + 0.065), smooth=True)   # the beast head
    p.add(props.lathe([(0.0, 0.028), (0.03, 0.036), (0.05, 0.05)], seg=14), 'siphon', T(0, yb + 0.21, zs + 0.062) @ R('X', -90), smooth=True)   # the flared funnel
    p.add(props.ellipsoid(0.042, 0.012, 0.042, seg=12), 'oar_port', T(0, yb + 0.262, zs + 0.062), smooth=True)   # its black mouth
    spur = [(0, yb - 0.04, 0.1), (0, yb + 0.16, 0.12), (0, yb + 0.4, 0.13)]
    p.add(props.tube(spur, [0.04, 0.032, 0.008], seg=10), 'wood_dark', smooth=True)
    p.add(props.lathe([(0.0, 0.03), (0.1, 0.02), (0.14, 0.0)], seg=10), 'siphon', T(0, yb + 0.28, 0.125) @ R('X', -90), smooth=True)
    stem = [(0, yb - 0.03, 0.02), (0, yb + 0.02, deck(yb) * 0.7), (0, yb + 0.04, deck(yb) + 0.08)]
    p.add(props.tube(stem, [0.03, 0.026, 0.02], seg=10), 'wood_dark', smooth=True)
    # the wooden castle (xylokastron) before the mainmast
    mm, fm = g['main'], g['fore']
    yc = mm['y'] + 0.3
    s = hull.s_of(yc)
    cw = hull.deck_half(s) * 1.2
    zc = deck(yc) + bh - 0.01
    ch = g['castle_h']
    p.add(props.box(cw, 0.26, ch), 'castle', T(0, yc, zc + ch / 2), bevel=0.008)
    p.add(props.box(cw + 0.03, 0.29, 0.025), 'castle_dark', T(0, yc, zc + ch), bevel=0.005)
    for kx in (-1, 0, 1):   # crenellations
        for ky in (-1, 1):
            p.add(props.box(0.065, 0.065, 0.06), 'castle', T(kx * (cw / 2 - 0.03), yc + ky * 0.115, zc + ch + 0.04), bevel=0.005)
    for ky in (-1, 0, 1):
        if ky:
            for kx in (-1, 1):
                p.add(props.box(0.065, 0.065, 0.06), 'castle', T(kx * (cw / 2 - 0.03), yc, zc + ch + 0.04), bevel=0.005)
    for sx in (-1, 1):   # civ pavises hung on its sides, a dark rim round each
        for ky in (-0.065, 0.065):
            p.add(props.box(0.012, 0.1, 0.13), 'castle_dark', T(sx * (cw / 2 + 0.004), yc + ky, zc + ch * 0.55), bevel=0.003)
            p.add(props.box(0.012, 0.084, 0.114), 'tint', T(sx * (cw / 2 + 0.008), yc + ky, zc + ch * 0.55), bevel=0.003, tint=True)
    for sy in (-1, 1):
        p.add(props.box(0.1, 0.012, 0.13), 'castle_dark', T(0, yc + sy * 0.134, zc + ch * 0.55), bevel=0.003)
        p.add(props.box(0.084, 0.012, 0.114), 'tint', T(0, yc + sy * 0.138, zc + ch * 0.55), bevel=0.003, tint=True)
    # the curling sternpost with a warm rim, two steering oars, the civ stern canopy
    ys_ = -yb
    top = deck(ys_) + 0.34
    curl = [(0, ys_ + 0.05, 0.1), (0, ys_ - 0.03, deck(ys_) - 0.02), (0, ys_ - 0.07, top - 0.12), (0, ys_ - 0.04, top - 0.02),
            (0, ys_ + 0.03, top), (0, ys_ + 0.09, top - 0.04), (0, ys_ + 0.09, top - 0.1)]
    radii = [0.05, 0.046, 0.04, 0.034, 0.028, 0.022, 0.018]
    p.add(props.tube(curl, radii, seg=12, scale_xy=(0.85, 1.3)), 'post', smooth=True)
    p.add(props.ellipsoid(0.026, 0.026, 0.026, seg=12), 'gold', T(*curl[-1]), smooth=True)
    ccy, ccz = ys_ + 0.03, top - 0.12
    hl = []
    for (x0, y0, z0), r0 in zip(curl[1:], radii[1:]):
        dy, dz = y0 - ccy, z0 - ccz
        n = math.hypot(dy, dz) or 1.0
        hl.append((0.0, y0 + dy / n * r0 * 0.55, z0 + dz / n * r0 * 0.55))
    p.add(props.tube(hl, 0.016, seg=8), 'post_hl', smooth=True)
    arch = []
    for yy in [-yb * 0.72 - 0.24 * i / 6 for i in range(7)]:
        z0 = deck(yy) + bh - 0.01
        hw = hull.deck_half(hull.s_of(yy)) * 0.9
        arch.append([(hw * math.cos(math.pi * k / 12), yy, z0 + 0.1 * math.sin(math.pi * k / 12)) for k in range(13)])
    p.add(props.loft(arch, False, False), 'tint', smooth=True, tint=True)
    for ring in (arch[0], arch[-1]):
        p.add(props.tube([(x, y, z - 0.003) for x, y, z in ring], 0.011, seg=6), 'wood_dark', smooth=True)
    for sx in (-1, 1):
        y0 = -yb * 0.72
        a = (sx * (hull.deck_half(hull.s_of(y0)) + 0.02), y0, deck(y0) + 0.05)
        e = (sx * 0.2, y0 - 0.16, 0.0)
        vkit.rod(p, a, e, 0.024, 'oar', seg=8)
        d = (Vector(e) - Vector(a)).normalized()
        m, _ = vkit.align(Vector(e) - d * 0.15, Vector(e) + d * 0.02)
        p.add(props.box(0.02, 0.08, 0.17), 'oar', m, bevel=0.004)
    # the masts (the foremast raked forward) with tarred caps
    for spec, rake in ((mm, 0.0), (fm, 10.0)):
        rk = math.radians(rake)
        z0 = deck(spec['y'])
        topp = (0, spec['y'] + math.sin(rk) * (spec['top'] - z0), spec['top'])
        vkit.rod(p, (0, spec['y'], z0), topp, 0.03 if spec is mm else 0.025, 'wood_dark', r1=0.022, seg=10)
        p.add(props.lathe([(0.0, 0.026), (0.03, 0.03), (0.05, 0.0)], seg=12), 'tar', T(*topp), smooth=True)
        spec['_top'] = topp
    p.finish()
    # the lateen sails, each its own part, the yard crossing the masthead, yawed to the wind
    for spec in (mm, fm):
        r = vkit.part(root, 'sail', c, SHIP_WEAR)
        _lateen_sail(r, spec['yard'], spec['ang'], spec['drop'], g['billow'], g['sail_band'], g['device'], fwd_frac=spec['fwd'])
        o = r.finish()
        o.location = (spec['_top'][0], spec['_top'][1], spec['_top'][2] - 0.03)
        o.rotation_euler = (0, 0, math.radians(spec['yaw']))
    return root


# ------------------------------------------------------------ modern warships: helpers (realism pass, naval-modern builder)

# The modern warships of the realism pass (notes/realism/Destroyer.md, Cruiser.md, Battleship.md, Aegis Cruiser.md):
# each built from its real ship in METRES (bow +Y, waterline z 0), scaled by k = units per metre. These helpers
# are this builder's own (prefix _nm_); no shared helper or default is changed. Painted steel: a dark graded
# hull (WWII Measure 22) or haze grey (the Aegis), a black boot topping, haze-grey houses, civ colour on the gun
# houses' roofs (the period air-recognition panels), the funnel bands and the ensign.
for _k, _v in (('nm_hull', ('#4e5357', 0.6, 0.2, 'metal')), ('nm_hull_alt', ('#45494d', 0.6, 0.2, 'metal')),
               ('nm_haze', ('#7e8286', 0.55, 0.2, 'metal')), ('nm_haze_alt', ('#727679', 0.55, 0.2, 'metal')),
               ('nm_house', ('#8e9296', 0.55, 0.2, 'metal')), ('nm_house_dk', ('#6c7074', 0.55, 0.2, 'metal')),
               ('nm_deck', ('#5a5e60', 0.8, 0.1, 'metal')), ('nm_teak', ('#6a6050', 0.75, 0.0, 'wood')),
               ('nm_barrel', ('#9a9ea0', 0.4, 0.4, 'metal')), ('nm_seam', ('#2e3134', 0.6, 0.2, 'metal')),
               ('nm_barrel_dk', ('#3a3d40', 0.4, 0.4, 'metal')),
               ('nm_spy', ('#5a5f64', 0.4, 0.3, 'metal')),
               ('nm_boot', ('#1e1e20', 0.7, 0.1, 'metal')), ('nm_edge', ('#c4c6c4', 0.5, 0.2, 'metal')),
               ('nm_gun', ('#5e6266', 0.45, 0.4, 'metal')), ('nm_gun_dk', ('#3a3d40', 0.45, 0.4, 'metal')),
               ('nm_bag', ('#d0ccbe', 0.9, 0.0, 'cloth')), ('nm_soot', ('#18181a', 0.85, 0.0, 'metal')),
               ('nm_glass', ('#16191c', 0.2, 0.0, 'polished')), ('nm_white', ('#dcdcd6', 0.6, 0.0, 'cloth')),
               ('nm_vls', ('#2e3236', 0.7, 0.2, 'metal'))):
    props.DEFAULTS.setdefault(_k, _v)
NM_WEAR = {'nm_hull': {'grime': 0.45, 'rust': 0.12}, 'nm_hull_alt': {'grime': 0.45, 'rust': 0.12},
           'nm_haze': {'grime': 0.4, 'rust': 0.1}, 'nm_haze_alt': {'grime': 0.4, 'rust': 0.1},
           'nm_house': {'grime': 0.3, 'rust': 0.05}, 'nm_house_dk': {'grime': 0.35}, 'nm_deck': {'grime': 0.5},
           'nm_teak': {'grime': 0.45}, 'nm_boot': {'grime': 0.3}, 'nm_soot': {'grime': 0.2}}


class _NM:
    """A modern warship under construction: the part, the hull, the scale k (units per metre); helpers take
    metres. deck(y) is the deck height (m) at y (m)."""

    def __init__(self, name, colors, g):
        self.g = g
        self.k = g['L_units'] / g['L']
        c = dict(SHIP_COLORS)
        c.update(g.get('colors', {}))
        c.update(colors)
        self.root = vkit.root(name)
        self.root['fit_scale'] = g['fit_scale']
        self.p = vkit.part(self.root, 'hull', c, NM_WEAR)
        k = self.k
        self.hull = Hull(L=g['L'] * k, B=g['B'] * k, D=g['D'] * k, draft=g['draft'] * k, sheer_stern=g['sheer'][0] * k,
                         sheer_bow=g['sheer'][1] * k, bow_p=g.get('bow_p', 1.4), stern_p=g.get('stern_p', 2.4),
                         transom=g.get('transom', 0.55), rake_bow=g.get('rake_bow', 0.35) * k, fullness=g.get('fullness', 0.55),
                         tumblehome=g.get('tumblehome', 0.0))

    def finish(self):
        """The tabletop exaggeration: everything above the waterline stretched up by g['zx'] (the stock ships
        stand ~2x taller than real), then the part closed."""
        zx = self.g.get('zx', 1.0)
        if zx != 1.0:
            for v in self.p.bm.verts:
                if v.co.z > 0:
                    v.co.z *= zx
        self.p.finish()
        return self.root

    def deck(self, y):
        return self.hull.deck_z(self.hull.s_of(y * self.k)) / self.k

    def half(self, y):
        return self.hull.deck_half(self.hull.s_of(y * self.k)) / self.k

    def low(self, y0, y1):
        return min(self.deck(y0 + (y1 - y0) * i / 6) for i in range(7))

    def add(self, bm, key, m=None, **kw):
        """Add a part built in metres (m: a transform in metres)."""
        k = self.k
        if m is not None:
            bmesh_ops_transform(bm, m)
        bmesh_ops_transform(bm, props.S(k))
        self.p.add(bm, key, **kw)

    def box(self, x, y, z, w, l, h, key, rot=None, bevel=0.15, **kw):
        m = T(x, y, z + h / 2)
        if rot is not None:
            m = T(x, y, z) @ rot @ T(0, 0, h / 2)
        self.add(props.box(w, l, h), key, m, bevel=bevel * self.k, **kw)

    def rod(self, a, b, r, key, seg=8, r1=None):
        k = self.k
        vkit.rod(self.p, tuple(v * k for v in a), tuple(v * k for v in b), r * k, key, seg=seg, r1=None if r1 is None else r1 * k)


def bmesh_ops_transform(bm, m):
    props.bmesh.ops.transform(bm, matrix=m, verts=bm.verts)


def _nm_hull(s, deck_key='nm_deck', dark_z=None, light='nm_haze', dark='nm_hull', course=9.0, civ_deck=(), belt=None,
             seams=()):
    """The hull: deck faces `deck_key` (civ in the civ_deck y ranges, m), a black boot topping at the waterline,
    the side `dark` below dark_z (m, the Measure 22 line) and `light` above it, in plate courses `course` m long
    (every other one a step darker). A light deck-edge line (the lit gunwale/toe-rail)."""
    hull, k, g = s.hull, s.k, s.g
    n = 60
    ss = [-1 + 2 * (i / n) for i in range(n + 1)]
    bm = props.loft([hull.section(t, m=32) for t in ss])
    boot = g.get('boot', 1.4)

    def keys(f):
        cc = f.calc_center_median()
        ym, zm = cc.y / k, cc.z / k
        if f.normal.z > 0.8:
            if any(a <= ym <= b for a, b in civ_deck):
                return ('tint', True)
            return (deck_key, False)
        if zm < boot:
            return ('nm_boot', False)
        alt = int(math.floor(ym / course)) % 2
        if dark_z is not None and zm < dark_z:
            return (dark + '_alt', False) if alt else (dark, False)
        return (light + '_alt', False) if alt else (light, False)

    s.p.add(bm, light, smooth=False, keys=keys)
    for sx in (-1, 1):
        path = [(sx * hull.deck_half(t) * 0.995, hull.y(t), hull.deck_z(t) + 0.0005) for t in [-0.985 + 1.97 * i / 80 for i in range(81)]]
        s.p.add(props.tube(path, g.get('edge_r', 0.3) * k, seg=6, scale_xy=(1.0, 0.6)), 'nm_edge', smooth=True)
        for zs in seams:   # plating strake lines along the side (dark welded seams, slightly proud)
            ts = [t for t in [-0.97 + 1.94 * i / 80 for i in range(81)] if hull.deck_z(t) > (zs + 0.3) * k]
            path = [(sx * (hull.side_x(t, zs * k) + 0.08 * k), hull.y(t), zs * k) for t in ts]
            if len(path) > 2:
                s.p.add(props.tube(path, 0.16 * k, seg=4, scale_xy=(1.0, 0.6)), 'nm_seam', smooth=True)
        if belt:   # the armour belt: a proud strip from y0 to y1 (m), z0 to z1, d m proud, its top a lit step
            y0, y1, z0, z1, d = belt
            rings = []
            for i in range(41):
                y = y0 + (y1 - y0) * i / 40
                t = hull.s_of(y * k)
                xa, xb = hull.side_x(t, z0 * k) / k, hull.side_x(t, z1 * k) / k
                rings.append([(sx * (xa - 0.2) * k, y * k, z0 * k), (sx * (xa + d) * k, y * k, z0 * k),
                              (sx * (xb + d) * k, y * k, z1 * k), (sx * (xb - 0.2) * k, y * k, z1 * k)])
            s.p.add(props.loft(rings), 'nm_hull_alt', smooth=False,
                    keys=lambda f: ('nm_edge', False) if f.normal.z > 0.6 else None)


def _nm_house(s, y0, y1, z0, h, w, key='nm_house', ports=True, front=None, taper=0.0, bevel=0.25):
    """A deckhouse from y0 to y1 (m), its floor at z0 (m above the waterline), h tall, w wide; a row of dark
    scuttles along its sides; `front` = a dark window band across its forward face (bridge). taper narrows the
    top (the sides lean in)."""
    yc, l = (y0 + y1) / 2, y1 - y0
    if taper:
        rings = []
        for yy in (y0, y1):
            rings.append([(w / 2, yy, z0), (w / 2 * (1 - taper), yy, z0 + h), (-w / 2 * (1 - taper), yy, z0 + h), (-w / 2, yy, z0)])
        s.add(props.loft(rings), key, smooth=False)
    else:
        s.box(0, yc, z0, w, l, h, key, bevel=bevel)
    if ports and h > 1.8:
        n = max(1, int(l / 3.2))
        for i in range(n):
            yy = y0 + (i + 0.5) * l / n
            for sx in (-1, 1):
                s.box(sx * (w / 2 * (1 - taper * 0.3) + 0.03), yy, z0 + h * 0.55, 0.12, 0.7, 0.55, 'nm_glass', bevel=0.0)
    if front:
        s.box(0, y1 + 0.03, z0 + h * 0.5, w * (1 - taper * 0.6) * 0.92, 0.12, h * front, 'nm_glass', bevel=0.0)


def _nm_prism(s, outline, z0, h, key, ports=None, band=None, inset=0.0, band_civ=False):
    """A deckhouse extruded from a footprint outline [(x, y) m, counter-clockwise seen from above], z0 to
    z0 + h, its walls leaning in by `inset` m at the top; ports = (y0, y1): a row of dark scuttles along both
    flat sides between them."""
    bm = props.bmesh.new()
    cy0 = sum(y for _, y in outline) / len(outline)
    top = [bm.verts.new((x - math.copysign(inset, x) if abs(x) > 1e-6 else x,
                         y - math.copysign(inset * 0.5, y - cy0), z0 + h)) for x, y in outline]
    bot = [bm.verts.new((x, y, z0)) for x, y in outline]
    bm.faces.new(top)
    bm.faces.new(list(reversed(bot)))
    for i in range(len(outline)):
        j = (i + 1) % len(outline)
        bm.faces.new((bot[i], bot[j], top[j], top[i]))
    props.bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    s.add(bm, key, bevel=0.2 * s.k)
    if band:   # a deck-edge line round the block (the 02 level's edge): a dark course, slightly proud
        zb, hb = band
        bm = props.bmesh.new()
        cx = sum(x for x, _ in outline) / len(outline)
        cy = sum(y for _, y in outline) / len(outline)
        fz = (zb + hb / 2) / h
        out = [(x - math.copysign(inset * fz - 0.25, x), y - math.copysign(inset * 0.5 * fz - 0.25, y - cy)) for x, y in outline]
        top = [bm.verts.new((x, y, z0 + zb + hb)) for x, y in out]
        bot = [bm.verts.new((x, y, z0 + zb)) for x, y in out]
        bm.faces.new(top)
        bm.faces.new(list(reversed(bot)))
        for i in range(len(out)):
            j = (i + 1) % len(out)
            bm.faces.new((bot[i], bot[j], top[j], top[i]))
        props.bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        s.add(bm, 'tint' if band_civ else 'nm_house_dk', tint=band_civ)
    if ports:
        xw = max(abs(x) for x, _ in outline)
        p0, p1 = ports
        n = max(1, int((p1 - p0) / 3.2))
        for row in (0.3, 0.72):
            for i in range(n):
                yy = p0 + (i + 0.5) * (p1 - p0) / n
                for sx in (-1, 1):
                    s.box(sx * (xw + 0.03), yy, z0 + h * row, 0.12, 0.7, 0.55, 'nm_glass', bevel=0.0)


def _nm_gunhouse(s, at, yaw, w, l, h, guns, bore, barrel_r, spacing, key='nm_haze', civ=True, elev=4.0,
                 barbette=0.0, rangefinder=0.0, bag=True, slope=0.45, round_front=False, tw=0.8, lf=0.3,
                 barrel_key='nm_barrel', drum=False):
    """A gun house (a turret or an enclosed mount): a barbette `barbette` m tall under it, the house with
    sloped sides (the roof tw of the base's width) and a sloped front face (lf of its length), or a rounded
    front in plan (round_front: the 5" enclosed mounts); a flat roof (civ colour if civ: the air-recognition
    panel), `guns` light barrels of `bore` m with blast bags and dark muzzles; optional rangefinder ears."""
    x, y, z = at
    M = T(x, y, z) @ R('Z', yaw)
    if barbette > 0:
        rb = w * (0.42 if drum else 0.44)
        s.add(props.lathe([(-0.5, rb), (barbette, rb)], seg=24), 'nm_haze_alt', M, smooth=not drum)
    zb = barbette
    lf = l * lf
    rings = [[(w / 2, -l / 2, zb), (w / 2 * tw, -l / 2, zb + h), (-w / 2 * tw, -l / 2, zb + h), (-w / 2, -l / 2, zb)]]
    if round_front:
        for i in range(7):
            a_ = (math.pi / 2) * i / 6
            yy = l / 2 - lf + lf * math.sin(a_)
            wf = max(0.3, math.cos(a_) ** 0.55)
            hh = h - (h - h * slope) * (1 - math.cos(a_))
            rings.append([(w / 2 * wf, yy, zb), (w / 2 * tw * wf, yy, zb + hh), (-w / 2 * tw * wf, yy, zb + hh), (-w / 2 * wf, yy, zb)])
    else:
        for yy, hh in ((l / 2 - lf, h), (l / 2, h * slope)):
            rings.append([(w / 2, yy, zb), (w / 2 * tw, yy, zb + hh), (-w / 2 * tw, yy, zb + hh), (-w / 2, yy, zb)])
    bm = props.loft(rings)

    def keys(f):
        if civ and f.normal.z > 0.95:
            return ('tint', True)
        return None

    s.add(bm, key, M, smooth=False, keys=keys)
    if rangefinder:
        s.add(props.tube([(-w / 2 - rangefinder, 0, 0), (w / 2 + rangefinder, 0, 0)], h * 0.2, seg=10), key,
              M @ T(0, -l / 2 + h * 0.35, zb + h * 0.7), smooth=True)
    zg = zb + h * (0.35 if round_front else slope * 0.55)
    yf = l / 2 - lf * (0.3 if round_front else 0.5)
    for i in range(guns):
        gx = (i - (guns - 1) / 2) * spacing
        gm = M @ T(gx, yf, zg) @ R('X', elev)
        if bag:
            s.add(props.lathe([(0.0, barrel_r * 1.9), (barrel_r * 2.2, barrel_r * 1.4)], seg=10), 'nm_bag', gm @ R('X', -90), smooth=True)
        s.add(props.lathe([(0.0, barrel_r * 1.3), (bore * 0.35, barrel_r), (bore * 0.9, barrel_r * 0.85)], seg=10), barrel_key,
              gm @ R('X', -90), smooth=True)
        s.add(props.lathe([(bore * 0.9, barrel_r * 0.85), (bore * 0.92, barrel_r * 1.05), (bore, barrel_r * 1.05)], seg=10), 'nm_gun_dk',
              gm @ R('X', -90), smooth=True)


def _nm_panel(s, y0, y1, wf):
    """A civ air-recognition panel painted on the deck: a rectangle wf of the deck's width, from y0 to y1 (m)."""
    z = max(s.deck(y0 + (y1 - y0) * i / 6) for i in range(7))
    w = 2 * min(s.half(y0), s.half(y1)) * wf
    s.box(0, (y0 + y1) / 2, z - 0.3, w, y1 - y0, 0.9, 'tint', bevel=0.0, tint=True)


def _nm_lattice(s, base, h, w, r, key='nm_gun_dk', radar=None):
    """A lattice mast: four stout legs tapering up, three rings of bracing, a radar on top."""
    x, y, z = base
    legs = [(sx * w / 2, sy * w / 2) for sx, sy in ((1, 1), (-1, 1), (-1, -1), (1, -1))]
    for lx, ly in legs:
        s.rod((x + lx, y + ly, z), (x + lx * 0.3, y + ly * 0.3, z + h), r, key, seg=6)
    for k_ in (0.3, 0.6, 0.9):
        f = 1 - 0.7 * k_
        pts = [(x + lx * f, y + ly * f, z + h * k_) for lx, ly in legs + legs[:1]]
        s.add(props.tube(pts, r * 0.8, seg=5), key, smooth=True)
    if radar:
        rw, rh = radar
        s.box(x, y, z + h, rw, rh * 0.4, rh, 'nm_house_dk', bevel=0.1)


def _nm_funnel(s, at, w, l, h, rake=0.12, civ=True, cap=0.8, key='nm_haze', band=(0.62, 0.84), caps=True, lip=0.22,
               fair=0.0, capflare=0.0):
    """A funnel: an oval (w x l m) h m tall, raked aft by `rake` (m per m), a sooty cap `cap` m deep and a civ
    band (band = its bottom and top as shares of h)."""
    x, y, z = at
    rings = []
    if fair or capflare:
        zs = sorted({0.0, h * 0.12, h * 0.25, h * 0.38, h * band[0], h * band[1], h - cap, h - cap * 0.5, h})
    else:   # (B2's rings: the Destroyer's and the Cruiser's finals)
        zs = [0.0, h * band[0], h * band[1], h - cap, h]

    def mult(zz):   # fair: the base flares out into the deck (faired funnels); capflare: the cap widens
        m_ = 1.0 + fair * max(0.0, 1.0 - zz / (0.4 * h)) ** 2
        if capflare and zz >= h - cap * 0.5:
            m_ += capflare
        return m_

    for zz in zs:
        m_ = mult(zz)
        rings.append([(x + w / 2 * m_ * math.cos(2 * math.pi * i / 20), y + l / 2 * m_ * math.sin(2 * math.pi * i / 20) - rake * zz,
                       z + zz) for i in range(20)])
    bm = props.loft(rings)

    def keys(f):
        zc = f.calc_center_median().z / s.k - z
        if f.normal.z > 0.9 or zc > h - cap:
            return ('nm_soot', False)
        if civ and h * band[0] < zc < h * band[1]:
            return ('tint', True)
        return None

    s.add(bm, key, smooth=False, keys=keys)
    if caps:   # the funnel cap's lip: a light rim
        rim = [(x + w / 2 * (1.04 + capflare) * math.cos(2 * math.pi * i / 20), y + l / 2 * (1.04 + capflare) * math.sin(2 * math.pi * i / 20) - rake * h, z + h - 0.1) for i in range(21)]
        s.add(props.tube(rim, lip, seg=5), 'nm_edge', smooth=True)


def _nm_pole(s, base, h, r, key='nm_gun_dk', yard=0.0, yard_z=0.75, radar=None, rake=0.0):
    """A pole mast (stout, so it holds at 1x) with a yard and an optional radar bar on top."""
    x, y, z = base
    top = (x, y - rake * h, z + h)
    s.rod(base, top, r, key, seg=8, r1=r * 0.85)
    if yard:
        zy = z + h * yard_z
        s.box(x, y - rake * h * yard_z, zy, yard, r * 1.6, r * 1.6, key, bevel=0.0)
    if radar:
        rw, rh = radar
        s.box(x, top[1], top[2], rw, rh * 0.35, rh, 'nm_house_dk', bevel=0.1)


def _nm_tripod(s, base, h, spread, r, key='nm_gun_dk', yard=0.0, radar=None):
    x, y, z = base
    top = (x, y, z + h)
    s.rod((x, y + spread, z), top, r, key, seg=8, r1=r * 0.8)
    for sx in (-1, 1):
        s.rod((x + sx * spread * 0.8, y - spread * 0.7, z), top, r * 0.8, key, seg=8, r1=r * 0.7)
    if yard:
        s.box(x, y, z + h * 0.82, yard, r * 1.6, r * 1.6, key, bevel=0.0)
    if radar:
        rw, rh = radar
        s.rod(top, (x, y, z + h + rh * 1.6), r, key, seg=6)
        s.box(x, y, z + h + rh * 1.2, rw, rh * 0.35, rh, 'nm_house_dk', bevel=0.1)


def _nm_director(s, at, w, l, h, arms=0.0, key='nm_house'):
    """A gun director: a box house on a short drum, the rangefinder's arms out to the sides."""
    x, y, z = at
    s.add(props.lathe([(0.0, w * 0.32), (h * 0.4, w * 0.32)], seg=16), key, T(x, y, z), smooth=True)
    s.box(x, y, z + h * 0.4, w, l, h * 0.6, key, bevel=0.2)
    if arms:
        s.add(props.tube([(x - w / 2 - arms, y - l * 0.2, z + h * 0.75), (x + w / 2 + arms, y - l * 0.2, z + h * 0.75)], h * 0.14, seg=8), key, smooth=True)


def _nm_tub(s, at, r, guns=2, key='nm_haze'):
    """An AA gun tub: a low ring wall, its guns' barrels raised."""
    x, y, z = at
    s.add(props.lathe([(0.0, r), (1.1, r), (1.1, r * 0.82), (0.0, r * 0.82)], seg=16), key, T(x, y, z), smooth=False)
    for i in range(guns):
        gx = x + (i - (guns - 1) / 2) * r * 0.5
        s.rod((gx, y, z + 0.9), (gx, y + r * 1.4, z + 1.9), r * 0.14, 'nm_gun_dk', seg=6)


def _nm_ensign(s, top, w, h, drop=0.0):
    """A solid civ ensign flying aft from `top` (m), w x h m, with a gentle wave."""
    x, y, z = top
    bm = props.bmesh.new()
    rows = []
    for j in range(9):
        t = j / 8
        rows.append([bm.verts.new((x + 0.12 * w * math.sin(4 * t), y - w * t, z - drop * t - h * i / 4)) for i in range(5)])
    for j in range(8):
        for i in range(4):
            bm.faces.new((rows[j][i], rows[j + 1][i], rows[j + 1][i + 1], rows[j][i + 1]))
    s.add(bm, 'tint', smooth=True, tint=True)
    # thickness: a second, offset sheet so the flag holds edge-on
    bm2 = props.bmesh.new()
    rows = []
    for j in range(9):
        t = j / 8
        rows.append([bm2.verts.new((x + 0.12 * w * math.sin(4 * t) + 0.25, y - w * t, z - drop * t - h * i / 4)) for i in range(5)])
    for j in range(8):
        for i in range(4):
            bm2.faces.new((rows[j][i], rows[j][i + 1], rows[j + 1][i + 1], rows[j + 1][i]))
    s.add(bm2, 'tint', smooth=True, tint=True)


def _nm_number(s, y, digits, size, z=None):
    """The hull number in white on both bows (seven-segment bars), size = the digit height in m."""
    if not digits:   # (the WWII ships: Measure 22 left the bow numbers small; at 1x they read as glyphs)
        return
    segs = {'0': 'abcdef', '1': 'bc', '2': 'abged', '3': 'abgcd', '4': 'fgbc', '5': 'afgcd', '6': 'afgedc', '7': 'abc',
            '8': 'abcdefg', '9': 'abcfgd'}
    hull, k = s.hull, s.k
    zz = z if z is not None else s.deck(y) * 0.62
    t = hull.s_of(y * k)
    w, hh, th = size * 0.55, size, size * 0.16
    for sx in (-1, 1):
        x = sx * (hull.side_x(t, zz * k) / k + 0.05)
        for i, d in enumerate(digits):
            yc = y - sx * (i - (len(digits) - 1) / 2) * size * 0.85
            for sg in segs[d]:
                dy, dz, ly, lz = {'a': (0, hh / 2, w, th), 'g': (0, 0, w, th), 'd': (0, -hh / 2, w, th),
                                  'f': (-w / 2, hh / 4, th, hh / 2), 'b': (w / 2, hh / 4, th, hh / 2),
                                  'e': (-w / 2, -hh / 4, th, hh / 2), 'c': (w / 2, -hh / 4, th, hh / 2)}[sg]
                s.box(x, yc - sx * dy, zz + dz - lz / 2, 0.1, ly, lz, 'nm_white', bevel=0.0)


# ------------------------------------------------------------ destroyer (realism pass: a Fletcher-class destroyer, 1943)

# USS Fletcher (DD-445) as in 1943 (notes/realism/Destroyer.md): 114.8 x 12.0 m, flush-decked with a strong
# sheer and flare forward, five single 5"/38 mounts in rounded gun houses (two forward, three aft, Nos. 2 and
# 3 superfiring), two raked funnels with caps, two quintuple torpedo tube mounts on the centreline, the bridge
# with the Mk 37 director on top, a pole foremast. Measure 22: a dark hull, haze-grey houses. Civ colour: the
# funnel bands (squadron bands) and the recognition panels fore and aft.
DESTROYER = dict(
    zx=1.7,
    L=115.0, B=15.0, D=4.8, draft=4.0, sheer=(0.6, 5.2), L_units=2.4, bow_p=1.3, stern_p=2.6, transom=0.55,
    rake_bow=9.0, tumblehome=-0.12, dark_z=3.8, seams=(2.3,),
    mounts=((40.0, 0, 0.0), (32.0, 0, 2.2), (-21.0, 180, 2.4), (-33.0, 180, 2.4), (-45.0, 180, 0.0)),
    mount=(4.4, 6.4, 3.2, 6.6, 0.36),   # w, l, h, bore, barrel r
    funnels=((8.0, 10.5), (-6.0, 9.5)), funnel=(4.2, 5.8, 0.26), band=(0.26, 0.72),
    tubes=(0.5, -14.5), mast=(16.0, 20.0), pole_r=1.0, number='', number_size=2.8,
    civ_deck=((42.0, 52.5), (-57.0, -48.6)),
    flag=(5.5, 3.6), fit_scale=0.965,
)


def destroyer_fletcher(colors, **kw):
    """The realism pass's Destroyer: a Fletcher-class destroyer of 1943 (DESTROYER)."""
    g = dict(DESTROYER, **kw)
    s = _NM('Destroyer', colors, g)
    _nm_hull(s, deck_key='nm_deck', dark_z=g['dark_z'], light='nm_haze', dark='nm_hull', civ_deck=g['civ_deck'], seams=g['seams'])
    w0 = s.half(20) * 2
    # forward deckhouse (01 level) with the superfiring No. 2 mount's platform, the bridge, the Mk 37 director
    z1 = s.low(18, 35)
    _nm_house(s, 17.0, 35.0, z1 - 0.3, 2.6, w0 * 0.72, ports=True)
    _nm_house(s, 19.0, 29.0, z1 + 2.3, 3.0, w0 * 0.62, front=0.45)        # the pilot house
    s.box(0, 27.5, z1 + 5.3, w0 * 0.92, 2.5, 0.5, 'nm_house', bevel=0.1)   # the open bridge with its wings
    _nm_house(s, 21.0, 27.0, z1 + 5.3, 2.4, w0 * 0.4, ports=False)          # the director's pedestal
    _nm_director(s, (0, 24.0, z1 + 7.7), 3.8, 4.2, 2.8, arms=1.4)
    # the after deckhouse carrying mounts 3 and 4, a 40 mm tub
    z2 = s.low(-37, -17)
    _nm_house(s, -37.0, -17.0, z2 - 0.3, 2.7, w0 * 0.6)
    _nm_tub(s, (0, -11.0, s.deck(-11) + 0.8), 2.0)
    s.box(0, -11.0, s.deck(-11) - 0.3, 3.0, 3.0, 1.1, 'nm_house', bevel=0.1)
    mw, ml, mh, bore, br = g['mount']
    for y, yaw, zr in g['mounts']:   # the 5"/38 in rounded enclosed mounts, grey roofs
        _nm_gunhouse(s, (0, y, s.deck(y) + zr), yaw, mw, ml, mh, 1, bore, br, 0.0, key='nm_house', civ=False,
                     barbette=0.4 if zr == 0 else 0.3, slope=0.55, round_front=True, tw=0.82, lf=0.42, bag=False)
    fw, fl, rake = g['funnel']
    for y, h in g['funnels']:
        zf = s.deck(y)
        s.box(0, y, zf - 0.3, fw * 1.25, fl * 1.25, 1.6, 'nm_house', bevel=0.1)   # its casing
        _nm_funnel(s, (0, y, zf + 1.0), fw, fl, h, rake=rake, cap=1.0, band=g['band'], lip=0.45)
    for y in g['tubes']:   # quintuple torpedo tubes on a turntable
        zt = s.deck(y)
        s.add(props.lathe([(0.0, 1.6), (0.6, 1.6)], seg=16), 'nm_haze_alt', T(0, y, zt), smooth=True)
        for i in range(5):
            s.rod((-1.2 + 0.6 * i, y + 3.6, zt + 1.1), (-1.2 + 0.6 * i, y - 3.4, zt + 1.1), 0.3, 'nm_gun', seg=8)
        s.box(0, y - 2.6, zt + 1.1, 3.2, 1.6, 1.4, 'nm_house_dk', bevel=0.1)
    my, mh = g['mast']
    _nm_pole(s, (0, my, s.deck(my) + 2.2), mh, g['pole_r'], yard=7.5, radar=(4.8, 2.0), rake=0.06)
    _nm_number(s, 46.0, g['number'], g['number_size'])
    # the ensign: on the stern staff
    ys = -55.5
    zs = s.deck(ys)
    s.rod((0, ys, zs), (0, ys, zs + 5.0), 0.6, 'nm_gun_dk', seg=6)
    fw2, fh2 = g['flag']
    _nm_ensign(s, (0, ys, zs + 5.0), fw2, fh2)
    return s.finish()


# ------------------------------------------------------------ cruiser (realism pass: a Baltimore-class heavy cruiser, 1944)

# USS Baltimore (CA-68) as in 1944 (notes/realism/Cruiser.md): 205 x 21.6 m, three big triple 8"/55 turrets
# (two forward, the second superfiring on a tall barbette, one aft), six twin 5"/38 mounts, two funnels set
# well apart, the boxy bridge block with the Mk 34 director and a tripod foremast, the after director on a
# pole mainmast, on the fantail two catapults with Kingfisher floatplanes and the big aircraft crane. Measure
# 22. Civ colour: the three turret roofs (air-recognition panels) and the floatplanes' wings.
CRUISER = dict(
    zx=1.7,
    L=205.0, B=26.0, D=6.4, draft=7.0, sheer=(0.8, 4.4), L_units=3.3, bow_p=1.3, stern_p=2.5, transom=0.6,
    rake_bow=10.0, tumblehome=-0.06, dark_z=5.2, seams=(3.0,),
    turrets=((64.0, 0, 0.0), (48.0, 0, 4.8), (-60.0, 180, 0.0)), turret=(13.5, 16.5, 5.4, 18.0, 0.8, 3.3),
    sec=((25.0, 0, 6.2, 0.0), (-38.0, 180, 3.0, 0.0), (10.0, 90, 3.0, 7.6), (10.0, -90, 3.0, -7.6),
         (-14.0, 90, 3.0, 7.6), (-14.0, -90, 3.0, -7.6)),
    funnels=((2.0, 16.0), (-22.0, 15.0)), funnel=(6.4, 9.0, 0.0),
    bridge=(17.0, 36.0, 6.8), fmast=(14.0, 21.0), mmast=(-30.0, 15.0), pole_r=1.1, number='', number_size=4.0,
    flag=(7.5, 5.0), fit_scale=0.96, sec_civ=True,
)


def _nm_floatplane(s, at, yaw):
    """A Kingfisher floatplane on its catapult: grey fuselage and main float, civ-coloured wings."""
    x, y, z = at
    M = T(x, y, z) @ R('Z', yaw)
    s.add(props.ellipsoid(0.8, 5.0, 0.9, seg=12), 'nm_house', M @ T(0, 0, 1.6), smooth=True)
    s.add(props.ellipsoid(0.7, 3.8, 0.55, seg=10), 'nm_house_dk', M @ T(0, 0.6, 0.4), smooth=True)   # the float
    s.add(props.box(0.3, 0.6, 1.4), 'nm_house_dk', M @ T(0, 0.6, 1.0))
    s.add(props.box(13.0, 2.2, 0.35), 'tint', M @ T(0, 0.8, 2.0), bevel=0.08 * s.k, tint=True)   # the wing
    s.add(props.box(3.6, 0.9, 0.25), 'tint', M @ T(0, -4.2, 1.9), bevel=0.05 * s.k, tint=True)  # the tailplane


def cruiser_baltimore(colors, **kw):
    """The realism pass's Cruiser: a Baltimore-class heavy cruiser of 1944 (CRUISER)."""
    g = dict(CRUISER, **kw)
    s = _NM('Cruiser', colors, g)
    _nm_hull(s, deck_key='nm_deck', dark_z=g['dark_z'], light='nm_haze', dark='nm_hull', seams=g['seams'])
    w0 = s.half(10) * 2
    # the forward superstructure: the 01 deckhouse, the boxy bridge block, the pilot house and the Mk 34 director
    z1 = s.low(-4, 40)
    _nm_house(s, -4.0, 40.0, z1 - 0.3, 3.0, w0 * 0.62)
    b0, b1, bh = g['bridge']
    zb = z1 + 2.7
    bw = w0 * 0.48
    _nm_house(s, b0, b1, zb, bh, bw, ports=False)
    for zz in (bh * 0.32, bh * 0.78):   # two rows of bridge windows (the navigating and the flag bridge)
        s.box(0, b1 + 0.04, zb + zz, bw * 0.9, 0.14, 1.0, 'nm_glass', bevel=0.0)
        for sx in (-1, 1):
            s.box(sx * (bw / 2 + 0.04), b1 - 4.0, zb + zz, 0.14, 6.0, 1.0, 'nm_glass', bevel=0.0)
    s.box(0, b1 - 2.0, zb + bh, w0 * 0.7, 3.4, 0.6, 'nm_house', bevel=0.1)     # the open bridge and its wings
    _nm_house(s, b0 + 6.0, b1 - 4.0, zb + bh, 2.8, bw * 0.6, front=0.4, ports=False)
    _nm_director(s, (0, (b0 + b1) / 2 + 1.0, zb + bh + 2.8), 5.0, 5.4, 3.8, arms=2.2)
    ztop = zb + bh + 2.8
    # the after superstructure with the after director
    z2 = s.low(-44, -26)
    _nm_house(s, -44.0, -26.0, z2 - 0.3, 3.0, w0 * 0.55)
    _nm_house(s, -36.0, -27.0, z2 + 2.7, 2.6, w0 * 0.32)
    _nm_director(s, (0, -31.0, z2 + 5.3), 3.6, 4.0, 2.8, arms=1.4)
    tw, tl, th, bore, br, sp = g['turret']
    for y, yaw, zr in g['turrets']:   # the triple turrets, all three with civ roofs
        _nm_gunhouse(s, (0, y, s.deck(y)), yaw, tw, tl, th, 3, bore, br, sp, key='nm_house', barbette=1.0 + zr,
                     rangefinder=1.3, slope=0.5, tw=0.88, lf=0.24)
    for y, yaw, zr, x in g['sec']:   # the twin 5"/38 mounts (civ roofs too)
        zz = s.deck(y) + zr
        _nm_gunhouse(s, (x, y, zz), yaw, 4.4, 6.0, 3.0, 2, 5.8, 0.3, 1.5, key='nm_house', barbette=0.3 if x else 0.4,
                     slope=0.55, civ=g['sec_civ'], round_front=True, lf=0.4, bag=False)
    fw, fl, rake = g['funnel']
    for y, h in g['funnels']:
        zf = s.deck(y)
        _nm_house(s, y - 6.0, y + 6.0, zf - 0.3, 3.0, w0 * 0.52, ports=False)   # its casing deckhouse
        _nm_funnel(s, (0, y, zf + 2.6), fw, fl, h, rake=rake, cap=1.2, civ=False, lip=0.35)
    for x, y in ((6.5, -6.0), (-6.5, -6.0), (6.5, 14.0), (-6.5, 14.0)):   # 40 mm quad tubs
        _nm_tub(s, (x, y, s.deck(y) + 2.6), 2.2, guns=2)
    fy, fh = g['fmast']
    _nm_tripod(s, (0, fy, zb), fh + ztop - zb - 4.0, 3.4, g['pole_r'], yard=11.0, radar=(5.6, 2.2))
    my, mh = g['mmast']
    _nm_pole(s, (0, my, z2 + 5.3), mh, g['pole_r'], yard=7.0, radar=(3.8, 1.6))
    # the fantail: two catapults on turntables with their floatplanes, the aircraft crane on its pedestal
    for sx in (-1, 1):
        yy = -72.0
        zc0 = s.deck(yy)
        s.add(props.lathe([(0.0, 2.2), (1.0, 2.2)], seg=16), 'nm_haze_alt', T(sx * 5.0, yy, zc0 - 0.2), smooth=True)
        s.box(sx * 5.0, yy, zc0 + 0.8, 1.8, 19.0, 1.2, 'nm_house', rot=R('Z', sx * 14), bevel=0.15)
        _nm_floatplane(s, (sx * 5.0 - sx * 1.0, yy + 2.0, zc0 + 1.6), sx * 14)
    yc = -88.0
    zc = s.deck(yc)
    s.box(0, yc, zc - 0.3, 4.4, 4.4, 2.6, 'nm_house', bevel=0.2)                   # the crane's pedestal
    s.rod((0, yc, zc + 2.0), (0, yc, zc + 10.0), 1.6, 'nm_house', seg=12)           # its post
    s.rod((0, yc, zc + 9.0), (0, yc + 14.0, zc + 15.0), 1.2, 'nm_house', seg=10, r1=0.9)   # the boom, raised forward
    s.rod((0, yc + 13.5, zc + 14.6), (0, yc + 13.5, zc + 9.0), 0.4, 'nm_gun_dk', seg=6)    # the hook's fall
    _nm_number(s, 82.0, g['number'], g['number_size'])
    ys = -100.0
    zs = s.deck(ys)
    s.rod((0, ys, zs), (0, ys, zs + 6.5), 0.7, 'nm_gun_dk', seg=6)
    fw2, fh2 = g['flag']
    _nm_ensign(s, (0, ys, zs + 6.5), fw2, fh2)
    return s.finish()


# ------------------------------------------------------------ battleship (realism pass: an Iowa-class battleship, 1944)

# USS Iowa (BB-61) as in 1944 (notes/realism/Battleship.md): 270 x 33 m, a long, sharp clipper bow with a
# strong sheer, high freeboard, the armour belt as a step along the side, three triple 16"/50 turrets (two
# forward, one aft) with rangefinder ears, the armoured conning tower in front of the tall, slim forward
# fire-control tower with its Mk 38 director, two funnels close together, ten twin 5"/38 mounts, the after
# director, a grey-brown teak deck. Measure 22. Civ colour: the recognition panels on the fore- and
# quarterdeck.
BATTLESHIP = dict(
    zx=1.7,
    L=270.0, B=40.0, D=9.0, draft=9.0, sheer=(1.0, 7.5), L_units=3.5, bow_p=1.12, stern_p=2.3, transom=0.6,
    rake_bow=22.0, tumblehome=-0.05, dark_z=7.0, seams=(), belt=(-78.0, 82.0, 1.4, 4.6, 0.8),
    colors={'nm_hull': '#43474b', 'nm_hull_alt': '#3b3f43'},
    turrets=((76.0, 0, 0.0), (52.0, 0, 7.0), (-68.0, 180, 0.0)), turret=(17.0, 21.0, 6.2, 31.0, 1.35, 4.0),
    sec=(24.0, 12.0, 0.0, -12.0, -24.0), sec_x=10.0,
    funnels=((-2.0, 18.0), (-20.0, 17.0)), funnel=(8.0, 10.0, 0.0),
    ct=(36.0, 4.8, 8.0), tower=(22.0, 31.0, 26.0), fmast=(23.0, 10.0), mmast=(-26.0, 18.0), pole_r=1.3,
    panels=((92.0, 110.0, 0.7), (-116.0, -100.0, 0.7)), number='', number_size=5.0,
    flag=(11.0, 7.0), fit_scale=1.0,
)


def battleship_iowa(colors, **kw):
    """The realism pass's Battleship: an Iowa-class battleship of 1944 (BATTLESHIP)."""
    g = dict(BATTLESHIP, **kw)
    s = _NM('Battleship', colors, g)
    _nm_hull(s, deck_key='nm_teak', dark_z=g['dark_z'], light='nm_haze', dark='nm_hull',
             belt=g['belt'], seams=g['seams'])
    for y0_, y1_, wf_ in g['panels']:   # small civ recognition panels on the fore- and quarterdeck
        _nm_panel(s, y0_, y1_, wf_)
    w0 = s.half(0) * 2
    # the 01 deckhouse, the 02 level
    z1 = s.low(-44, 38)
    _nm_house(s, -44.0, 38.0, z1 - 0.3, 3.0, w0 * 0.74)
    _nm_house(s, -36.0, 32.0, z1 + 2.7, 3.0, w0 * 0.46)
    z3 = z1 + 5.6
    # the armoured conning tower: a heavy dark cylinder with its vision slits
    cy, cr, ch = g['ct']
    s.add(props.lathe([(0.0, cr), (ch, cr), (ch + 0.5, cr * 0.9)], seg=24), 'nm_house_dk', T(0, cy - 3.0, z3), smooth=True)
    s.add(props.lathe([(0.0, cr * 1.02), (0.7, cr * 1.02)], seg=24), 'nm_glass', T(0, cy - 3.0, z3 + ch * 0.78), smooth=True)
    # the tall, slim fire-control tower: one block with two bridge levels (windows and wings) and the director
    t0, t1, th_ = g['tower']
    tw_ = w0 * 0.2
    _nm_house(s, t0, t1, z3, th_, tw_, ports=False)
    for zz, wf in ((th_ * 0.35, 0.5), (th_ * 0.68, 0.4)):
        s.box(0, t1 + 0.04, z3 + zz, tw_ * 0.9, 0.14, 1.2, 'nm_glass', bevel=0.0)
        for sx in (-1, 1):
            s.box(sx * (tw_ / 2 + 0.04), (t0 + t1) / 2, z3 + zz, 0.14, (t1 - t0) * 0.8, 1.2, 'nm_glass', bevel=0.0)
        s.box(0, t1 - 2.0, z3 + zz - 1.4, w0 * wf, 4.0, 0.7, 'nm_house', bevel=0.1)   # the bridge wings
    ztop = z3 + th_
    _nm_house(s, t0 - 1.5, t1 + 1.5, ztop, 4.6, w0 * 0.36, front=0.4, ports=False)   # the massive fire-control top
    ztop += 4.6
    _nm_director(s, (0, (t0 + t1) / 2 + 0.5, ztop), 8.0, 8.4, 5.0, arms=3.0, key='nm_house')
    tw, tl2, th, bore, br, sp = g['turret']
    for y, yaw, zr in g['turrets']:   # grey roofs (the battleship's civ colour is on its decks)
        _nm_gunhouse(s, (0, y, s.deck(y)), yaw, tw, tl2, th, 3, bore, br, sp, key='nm_house', barbette=1.2 + zr,
                     rangefinder=1.8, civ=True, slope=0.45, tw=0.9, lf=0.28, barrel_key='nm_barrel_dk', drum=True)
    for y in g['sec']:   # ten twin 5"/38 mounts on the 01 level
        for sx in (-1, 1):
            _nm_gunhouse(s, (sx * g['sec_x'], y, z1 + 2.7), sx * -90 + (0 if abs(y) < 13 else (-sx * 20 if y > 0 else sx * 20)),
                         5.0, 6.6, 3.3, 2, 8.0, 0.42, 1.7, key='nm_house', barbette=0.3, slope=0.5, civ=False,
                         lf=0.35, bag=False, barrel_key='nm_barrel_dk', drum=True)
    fw, fl, rake = g['funnel']
    for y, h in g['funnels']:
        _nm_funnel(s, (0, y, z3), fw, fl, h, rake=rake, cap=1.4, civ=True, band=(0.62, 0.8), lip=0.4, fair=0.8)
    for x, y in ((7.0, 6.0), (-7.0, 6.0), (6.0, -32.0), (-6.0, -32.0)):   # 40 mm quad tubs
        _nm_tub(s, (x, y, z3), 2.4, guns=2)
    # the after fire-control station and the mainmast
    my, mh = g['mmast']
    _nm_house(s, my - 6.0, my + 2.0, z3, 3.4, w0 * 0.26)
    _nm_director(s, (0, my - 2.0, z3 + 3.4), 4.6, 5.0, 3.4, arms=1.8)
    _nm_pole(s, (0, my + 2.5, z3 + 3.4), mh, g['pole_r'], yard=10.0, radar=(4.4, 2.0))
    fy, fh = g['fmast']
    _nm_pole(s, (0, fy, ztop + 2.0), fh, g['pole_r'], yard=12.0, radar=(6.4, 2.6))
    ys = -131.0
    zs = s.deck(ys)
    # the stern crane: a pedestal, a post and a boom
    s.box(0, -124.0, zs - 0.3, 4.0, 4.0, 2.4, 'nm_house', bevel=0.2)
    s.rod((0, -124.0, zs + 2.0), (0, -124.0, zs + 9.0), 1.4, 'nm_house', seg=10)
    s.rod((0, -124.0, zs + 8.0), (0, -111.0, zs + 12.5), 1.1, 'nm_house', seg=8, r1=0.9)
    s.rod((0, ys, zs), (0, ys, zs + 7.0), 0.9, 'nm_gun_dk', seg=6)
    fw2, fh2 = g['flag']
    _nm_ensign(s, (0, ys, zs + 7.0), fw2, fh2)
    _nm_number(s, 112.0, g['number'], g['number_size'])
    return s.finish()


# ------------------------------------------------------------ aegis cruiser (realism pass: a Ticonderoga-class cruiser, 1983)

# USS Ticonderoga (CG-47) as in the late 1980s (notes/realism/Aegis Cruiser.md): 173 x 16.8 m, the Spruance
# hull (a high flared bow, flush deck), two 5"/54 Mk 45 mounts with long barrels, two dark Mk 41 launcher
# grids fore and aft, the tall forward and after deckhouses with leaning walls, each carrying two octagonal
# SPY-1 arrays on its chamfered corners, the two stack groups at the deckhouses' facing ends, a lattice
# foremast and a pole mainmast, Phalanx domes, Harpoon canisters, a marked helicopter deck. Haze grey. Civ
# colour: the four array rims (the main region) and bands on the stacks.
AEGIS = dict(
    zx=1.7,
    L=173.0, B=20.0, D=7.0, draft=6.5, sheer=(0.3, 1.8), L_units=2.9, bow_p=1.25, stern_p=2.4, transom=0.6,
    rake_bow=6.0, tumblehome=-0.08, dark_z=None, seams=(3.2,),
    mounts=((66.0, 0), (-80.0, 180)), vls=((53.0, 9.0), (-72.5, 6.5)),
    fwd=(-4.0, 42.0), aft=(-54.0, -18.0), block_h=10.6, chamfer=8.6, inset=1.2, spy=5.3, rim=0.5, spy_civ=True,
    colors={'nm_haze': '#62666a', 'nm_haze_alt': '#5a5e62', 'nm_house': '#7e8286', 'nm_house_dk': '#55595d', 'nm_deck': '#424548'},
    stacks=(-6.5, -15.5), stack=(8.0, 6.0, 6.0),
    fmast=(14.0, 15.0), mmast=(-11.0, 15.0), pole_r=1.0, number='', number_size=4.0,
    flag=(7.0, 4.6), fit_scale=1.0,
)


def _nm_spy(s, at, yaw, r, civ, rim_r=0.6):
    """An octagonal SPY-1 array face on a deckhouse corner, facing yaw (deg), leaning back 5 deg: a grey
    octagon in a civ rim."""
    x, y, z = at
    M = T(x, y, z) @ R('Z', yaw) @ R('X', 6.5)
    ring = [(r * math.cos(math.pi / 8 + math.pi * i / 4), 0.0, r * math.sin(math.pi / 8 + math.pi * i / 4)) for i in range(8)]
    bm = props.loft([[(px, -0.6, pz) for px, _, pz in ring], [(px * 0.97, 0.9, pz * 0.97) for px, _, pz in ring]])   # proud of the wall
    s.add(bm, 'tint' if civ else 'nm_spy', M, smooth=False, tint=civ)   # the array face: a solid civ octagon
    rim = [(r * 1.03 * math.cos(math.pi / 8 + math.pi * i / 4), 0.8, r * 1.03 * math.sin(math.pi / 8 + math.pi * i / 4)) for i in range(9)]
    s.add(props.tube(rim, rim_r, seg=5), 'nm_house_dk', M, smooth=False)   # a dark frame round it


def aegis_ticonderoga(colors, **kw):
    """The realism pass's Aegis Cruiser: a Ticonderoga-class cruiser (AEGIS)."""
    g = dict(AEGIS, **kw)
    s = _NM('Aegis Cruiser', colors, g)
    _nm_hull(s, deck_key='nm_deck', dark_z=None, light='nm_haze', dark='nm_haze', seams=g['seams'])
    w0 = s.half(10) * 2
    H, c, sp, ins = g['block_h'], g['chamfer'], g['spy'], g['inset']
    lean = math.degrees(math.atan2(ins, H))
    # the forward deckhouse: one tall block, walls leaning in, forward corners chamfered with the SPY arrays;
    # the pilot house with its window band and the bridge wings on top
    y0, y1 = g['fwd']
    z1 = s.low(y0, y1)
    hw = w0 * 0.86
    _nm_prism(s, [(hw / 2, y0), (hw / 2, y1 - c), (hw / 2 - c, y1), (-hw / 2 + c, y1), (-hw / 2, y1 - c), (-hw / 2, y0)],
              z1 - 0.3, H, 'nm_house', band=(H * 0.36, 2.2), inset=ins, band_civ=True)
    zt1 = z1 - 0.3 + H
    _nm_house(s, y0 + 8.0, y1 - c - 1.0, zt1, 4.2, hw * 0.66, front=0.5, ports=False)   # the bridge level, its windows
    s.box(0, y1 - c - 3.0, zt1 + 4.2, hw * 0.94, 3.2, 0.6, 'nm_house', bevel=0.1)       # the bridge wings
    _nm_house(s, y0 + 12.0, y1 - c - 6.0, zt1 + 4.2, 2.6, hw * 0.4, ports=False)       # the level above
    zt1 += 2.4
    for sx in (-1, 1):
        xf = hw / 2 - c / 2 - ins * 0.6
        _nm_spy(s, (sx * xf, y1 - c / 2 - ins * 0.3, z1 - 0.3 + H * 0.56), sx * -45, sp, g['spy_civ'], rim_r=g['rim'])
    # the after deckhouse: its after corners chamfered with the two after arrays, the hangar door between them
    a0, a1 = g['aft']
    z2 = s.low(a0, a1)
    ha = hw * 0.92
    _nm_prism(s, [(ha / 2, a1), (-ha / 2, a1), (-ha / 2, a0 + c), (-ha / 2 + c, a0), (ha / 2 - c, a0), (ha / 2, a0 + c)],
              z2 - 0.3, H, 'nm_house', band=(H * 0.36, 2.2), inset=ins, band_civ=True)
    zt2 = z2 - 0.3 + H
    _nm_house(s, a0 + c + 4.0, a1 - 6.0, zt2, 2.6, ha * 0.46, ports=False)
    for sx in (-1, 1):
        xf = ha / 2 - c / 2 - ins * 0.6
        _nm_spy(s, (sx * xf, a0 + c / 2 + ins * 0.3, z2 - 0.3 + H * 0.6), sx * -135, sp, g['spy_civ'], rim_r=g['rim'])
    s.box(0, a0 + 0.15, z2 - 0.3, ha - 2 * c - 0.6, 0.2, H * 0.45, 'nm_house_dk', bevel=0.0)     # the hangar door
    # the helicopter deck: white landing circle, centreline and edge lines on the dark deck
    yfd = a0 - 9.5
    zd = s.deck(yfd) + 0.1
    ring = [(5.0 * math.cos(2 * math.pi * i / 32), yfd + 5.0 * math.sin(2 * math.pi * i / 32), zd) for i in range(33)]
    s.add(props.tube(ring, 0.85, seg=5, scale_xy=(1.0, 0.3)), 'nm_white', smooth=True)
    s.box(0, yfd, zd - 0.1, 0.9, 16.0, 0.2, 'nm_white', bevel=0.0)
    for sx in (-1, 1):
        s.box(sx * (s.half(yfd) - 1.2), yfd, zd - 0.1, 0.8, 15.0, 0.2, 'nm_white', bevel=0.0)
    # the 5"/54 Mk 45 mounts: angular houses with long light barrels
    for y, yaw in g['mounts']:
        _nm_gunhouse(s, (0, y, s.deck(y)), yaw, 6.0, 9.0, 4.0, 1, 13.0, 0.75, 0.0, key='nm_house', barbette=0.8, slope=0.35,
                     bag=False, civ=False, elev=10.0, tw=0.66, lf=0.4, barrel_key='nm_barrel_dk', drum=True)
    # the vertical launchers: dark patches with a light grid
    for y, l in g['vls']:
        zv = s.deck(y)
        s.box(0, y, zv - 0.4, 11.0, l * 1.15, 1.0, 'nm_vls', bevel=0.1)
        for i in range(4):
            s.box(-3.6 + 2.4 * i, y, zv + 0.6, 0.45, l * 1.1, 0.12, 'nm_edge', bevel=0.0)
        for j in range(3):
            s.box(0, y - l * 0.55 + l * 1.1 * (j + 1) / 4, zv + 0.6, 10.6, 0.45, 0.12, 'nm_edge', bevel=0.0)
    # the two stack groups at the deckhouses' facing ends: a casing with three exhausts, a civ band round its top
    sw, sl, sh = g['stack']
    zm = s.low(a1, y0)
    _nm_house(s, a1 - 1.0, y0 + 1.0, zm - 0.3, H * 0.72, hw * 0.72, ports=False, taper=0.06)   # the stack housing
    ztm = zm - 0.3 + H * 0.72
    for y, zb in ((g['stacks'][0], ztm), (g['stacks'][1], ztm)):
        _nm_house(s, y - sl / 2, y + sl / 2, zb - 0.5, sh, sw, ports=False, taper=0.08)
        s.box(0, y, zb + sh - 3.2, sw * 0.98, sl * 1.03, 2.6, 'tint', bevel=0.05, tint=True)
        for i in range(3):
            s.add(props.lathe([(0.0, 0.9), (2.0, 0.85)], seg=12), 'nm_soot', T(-2.0 + 2.0 * i, y, zb + sh - 0.5), smooth=True)
    fy, fh = g['fmast']
    _nm_lattice(s, (0, fy, zt1 + 3.0), fh, 3.4, 0.55, radar=(8.0, 1.8))
    s.box(0, fy, zt1 + 3.0 + fh * 0.62, 7.0, 0.9, 0.9, 'nm_gun_dk', bevel=0.0)   # its yard
    my, mh = g['mmast']
    _nm_lattice(s, (0, my, ztm), mh + sh, 3.0, 0.5, radar=(5.6, 2.2))   # the lattice mainmast between the stacks
    # the Phalanx CIWS domes, Harpoon canisters
    for x, y, zz in ((0.0, y1 - c - 6.0, zt1 + 3.5), (0.0, a1 - 3.0, zt2)):
        s.add(props.lathe([(0.0, 1.2), (1.3, 1.2)], seg=12), 'nm_house', T(x, y, zz), smooth=True)
        s.add(props.ellipsoid(1.1, 1.1, 1.6, seg=14), 'nm_white', T(x, y, zz + 2.4), smooth=True)
    for sx in (-1, 1):
        for i in range(2):
            s.box(sx * 5.6, -77.0 - i * 0.1, s.deck(-77) + 0.4 + i * 0.9, 1.0, 5.0, 0.8, 'nm_house_dk', rot=R('X', 12), bevel=0.1)
    _nm_number(s, 70.0, g['number'], g['number_size'])
    ys = -85.0
    zs = s.deck(ys)
    s.rod((0, ys, zs), (0, ys, zs + 5.0), 0.7, 'nm_gun_dk', seg=6)
    fw2, fh2 = g['flag']
    _nm_ensign(s, (0, ys, zs + 5.0), fw2, fh2)
    return s.finish()


# ------------------------------------------------------------ steel ships (ironclad to aegis cruiser)

props.DEFAULTS.setdefault('steel_hull', ('#6e7276', 0.55, 0.6, 'metal'))
props.DEFAULTS.setdefault('steel_dark', ('#43474b', 0.55, 0.6, 'metal'))
props.DEFAULTS.setdefault('steel_light', ('#868a8e', 0.5, 0.6, 'metal'))   # superstructure grey (not white)
props.DEFAULTS.setdefault('sub_hull', ('#3e6460', 0.5, 0.5, 'metal'))      # sea-green submarine hull
props.DEFAULTS.setdefault('radome', ('#e2e2de', 0.6, 0.0, 'cloth'))
props.DEFAULTS.setdefault('teak', ('#9a8462', 0.75, 0.0, 'wood'))
props.DEFAULTS.setdefault('boot', ('#5a2a24', 0.7, 0.2, 'metal'))      # red-brown antifouling at the waterline
props.DEFAULTS.setdefault('tarp', ('#6a8a3c', 0.9, 0.0, 'cloth'))      # green cargo tarps
props.DEFAULTS.setdefault('flight_deck', ('#3e4246', 0.85, 0.2, 'metal'))
props.DEFAULTS.setdefault('deck_line', ('#d8d6cc', 0.8, 0.0, 'cloth'))
STEEL_WEAR = {'steel_hull': {'grime': 0.4, 'rust': 0.12}, 'steel_dark': {'grime': 0.45, 'rust': 0.1},
              'steel_light': {'grime': 0.35}, 'teak': {'grime': 0.5}, 'boot': {'grime': 0.4},
              'tarp': {'grime': 0.5}, 'flight_deck': {'grime': 0.5}}


def _turret(p, at, yaw, guns=3, size=1.0, key='steel_light', barrel=0.5):
    """A gun turret: a sloped armored house on a barbette, `guns` barrels
    run out forward (along the turret's +Y after yaw)."""
    x, y, z = at
    s = size
    m = T(x, y, z) @ R('Z', yaw)
    p.add(props.lathe([(0.0, 0.11 * s), (0.04 * s, 0.11 * s)], seg=16), 'steel_dark', m, smooth=True)          # barbette
    p.add(props.box(0.2 * s, 0.24 * s, 0.08 * s), key, m @ T(0, 0, 0.07 * s), bevel=0.02 * s)                  # house
    p.add(props.box(0.18 * s, 0.06 * s, 0.05 * s), key, m @ T(0, 0.12 * s, 0.06 * s) @ R('X', -25), bevel=0.01 * s)  # sloped face
    for i in range(guns):
        gx = (i - (guns - 1) / 2) * 0.055 * s
        p.add(props.lathe([(0.0, 0.016 * s), (barrel * s, 0.011 * s), (barrel * s + 0.02, 0.013 * s)], seg=8), 'steel_dark',
              m @ T(gx, 0.12 * s, 0.075 * s) @ R('X', -88), smooth=True)


def _stack(p, at, h, r, key='steel_light', cap='steel_dark', civ=False):
    x, y, z = at
    p.add(props.lathe([(0.0, r), (h, r * 0.92)], seg=14, scale_xy=(0.8, 1.0)), key, T(x, y, z), smooth=True)
    p.add(props.lathe([(0.0, r * 0.95), (0.04, r * 0.95)], seg=14, scale_xy=(0.8, 1.0)), cap, T(x, y, z + h - 0.02), smooth=True)
    if civ:   # a civ-colored band round the funnel's top
        p.add(props.lathe([(0.0, r * 0.97), (0.05, r * 0.95)], seg=14, scale_xy=(0.8, 1.0)), 'tint', T(x, y, z + h - 0.08),
              smooth=True, tint=True)


def _mast(p, at, h, radar=True):
    x, y, z = at
    for sx in (-1, 1):   # a tripod
        vkit.rod(p, (sx * 0.06, y - 0.04, z), (0, y, z + h), 0.01, 'steel_dark', seg=6)
    vkit.rod(p, (0, y + 0.06, z), (0, y, z + h), 0.01, 'steel_dark', seg=6)
    vkit.rod(p, (-0.12, y, z + h * 0.75), (0.12, y, z + h * 0.75), 0.006, 'steel_dark', seg=4)   # yard
    if radar:
        p.add(props.box(0.16, 0.02, 0.05), 'steel_light', T(0, y, z + h + 0.03), bevel=0.005)
    vkit.rod(p, (0, y, z + h), (0, y, z + h + 0.12), 0.004, 'steel_dark', seg=4)


def steel_ship(name, cfg, colors):
    """A steel warship from a config: a sharp grey hull with a red boot
    topping, a teak or steel deck with civ-colored panels at bow and stern
    (as Civ3 marks its warships), superstructure blocks stepping up to the
    bridge, turrets, funnels, masts with radar, and extras."""
    c = dict(SHIP_COLORS, **{'wood': cfg.get('hull_color', '#6e7276'), 'wood_alt': cfg.get('hull_color', '#6e7276'),
                             'deck': cfg.get('deck', '#9a8462'), 'rail': '#43474b', 'wood_dark': '#43474b'})
    c.update(colors)
    root = vkit.root(name)
    p = vkit.part(root, 'hull', c, STEEL_WEAR)
    hull = Hull(**cfg['hull'])
    L = hull.L

    def deck(y):
        return hull.deck_z(hull.s_of(y))

    panels = cfg.get('civ_panels', [(L * 0.32, L * 0.44), (-L * 0.46, -L * 0.36)])
    add_hull(p, hull, stripes=[(-0.03, 0.03, 'boot')], plank=1.0)
    if cfg.get('hull_civ'):   # a civ band along the hull sides
        _wales(p, hull, [cfg['hull_civ']])
    # civ-colored deck panels at bow and stern
    for y0, y1 in panels:
        # a solid civ patch filling the deck's outline: adjacent strips, each as wide as the deck there. The
        # deck is one long n-gon, triangulated at render time, so its surface near the cambered ends rides
        # above the local deck height: each patch is a slab topped at the highest deck level in its range.
        z_top = max(deck(y0 + (y1 - y0) * k / 8) for k in range(9)) + 0.008
        n = 14
        for i in range(n):
            ya, yb = y0 + (y1 - y0) * i / n, y0 + (y1 - y0) * (i + 1) / n
            yc = (ya + yb) / 2
            w = hull.deck_half(hull.s_of(yc)) * 1.9
            if w > 0.02:
                dz = z_top - deck(yc) + 0.02
                p.add(props.box(w, (yb - ya) * 1.02, dz), 'tint', T(0, yc, z_top - dz / 2), tint=True)
    for (x, y, zr, sx_, sy_, sz_, key) in cfg.get('blocks', []):   # superstructure: (x, y, z above deck, size x/y/z, key)
        p.add(props.box(sx_, sy_, sz_), key, T(x, y, deck(y) + zr + sz_ / 2), bevel=min(0.012, sz_ * 0.2))
        # a row of dark windows (scuttles) along the block's sides
        if sz_ > 0.05 and cfg.get('windows', True):
            for k in range(max(1, int(sy_ / 0.05))):
                yy = y - sy_ / 2 + 0.025 + k * 0.05
                for sxw in (-1, 1):
                    p.add(props.box(0.004, 0.016, 0.012), 'port', T(x + sxw * (sx_ / 2 + 0.001), yy, deck(y) + zr + sz_ * 0.7))
    for (y, yaw, guns, size, zr) in cfg.get('turrets', []):
        _turret(p, (0, y, deck(y) + zr), yaw, guns, size, barrel=cfg.get('barrel', 0.5))
    for (y, h, r, zr) in cfg.get('stacks', []):
        _stack(p, (0, y, deck(y) + zr), h, r, civ=cfg.get('stack_civ', True))
    # civ-colored faces on the superstructure: the bridge front and side bands, as Civ3 paints its warships
    for (x, y, zr, sx_, sy_, sz_, key) in cfg.get('blocks', [])[:cfg.get('civ_blocks', 2)]:
        p.add(props.box(sx_ * 1.01, 0.008, sz_ * 0.45), 'tint', T(x, y + sy_ / 2 + 0.003, deck(y) + zr + sz_ * 0.55), tint=True)
        for sxs in (-1, 1):
            p.add(props.box(0.006, sy_ * 0.8, sz_ * 0.18), 'tint', T(x + sxs * (sx_ / 2 + 0.003), y, deck(y) + zr + sz_ * 0.8), tint=True)
    for (y, h, zr) in cfg.get('masts', []):
        _mast(p, (0, y, deck(y) + zr), h)
    # a guard rail round the deck edge, and the hull number on the bow
    for sx in (-1, 1):
        path = [(sx * hull.deck_half(s) * 0.97, hull.y(s), hull.deck_z(s) + 0.03) for s in [-0.95 + 1.9 * i / 30 for i in range(31)]]
        p.add(props.tube(path, 0.004, seg=4), 'steel_dark', smooth=True)
        if cfg.get('number', True):
            y = L * 0.36
            s = hull.s_of(y)
            for k in range(2):
                p.add(props.box(0.004, 0.035, 0.05), 'deck_line', T(sx * (hull.side_x(s, hull.deck_z(s) * 0.6) + 0.004), y + (k - 0.5) * 0.05,
                                                                    hull.deck_z(s) * 0.6), bevel=0.002)
    for extra in cfg.get('extras', []):
        extra(p, hull, deck)
    # the ensign on a staff at the stern (it floated free before)
    ys = cfg.get('ensign_y', -L * 0.46)
    zs = deck(ys)
    vkit.rod(p, (0, ys, zs), (0, ys, zs + cfg.get('ensign_h', 0.22)), 0.006, 'steel_dark', seg=6)
    pennant(p, (0, ys, zs + cfg.get('ensign_h', 0.22)), 0.2, 0.09)
    for (y, h, zr) in cfg.get('masts', [])[:1]:   # and a civ pennant at the masthead
        pennant(p, (0, y, deck(y) + zr + h + 0.12), 0.3, 0.08, phase=1.0)
    p.finish()
    return root


def _lattice_mast(p, at, h, w=0.07):
    """A lattice (truss) mast: four legs tapering up, cross bracing, a radar bar."""
    x, y, z = at
    legs = [(sx * w, y + sy * w) for sx in (-1, 1) for sy in (-1, 1)]
    for lx, ly in legs:
        vkit.rod(p, (x + lx, ly, z), (x + lx * 0.3, y + (ly - y) * 0.3, z + h), 0.006, 'steel_dark', seg=4)
    for k in range(1, 5):
        t = k / 5
        s = 1 - 0.7 * t
        zz = z + h * t
        pts = [(x + lx * s, y + (ly - y) * s, zz) for lx, ly in (legs[0], legs[1], legs[3], legs[2], legs[0])]
        p.add(props.tube(pts, 0.004, seg=4), 'steel_dark', smooth=True)
    p.add(props.box(0.2, 0.03, 0.04), 'steel_light', T(x, y, z + h + 0.02), bevel=0.005)
    vkit.rod(p, (x, y, z + h), (x, y, z + h + 0.14), 0.004, 'steel_dark', seg=4)


def _hull_number(p, hull, digits='110', size=1.0):
    """The hull number painted on both bows, digit by digit (bars as a seven-segment figure)."""
    segs = {'0': 'abcdef', '1': 'bc', '2': 'abged', '3': 'abgcd', '4': 'fgbc', '5': 'afgcd', '6': 'afgedc', '7': 'abc',
            '8': 'abcdefg', '9': 'abcfgd'}
    y0 = hull.L * 0.38
    s = hull.s_of(y0)
    z0 = hull.deck_z(s) * 0.55
    w, hh = 0.03 * size, 0.05 * size
    for sx in (-1, 1):
        x = sx * (hull.side_x(s, z0) + 0.005)
        for i, d in enumerate(digits):
            yc = y0 - sx * (i - (len(digits) - 1) / 2) * 0.045 * size
            for sg in segs[d]:
                k = 0.008 * size
                dy, dz, ly, lz = {'a': (0, hh, w, k), 'g': (0, 0, w, k), 'd': (0, -hh, w, k),
                                  'f': (-w / 2, hh / 2, k, hh), 'b': (w / 2, hh / 2, k, hh),
                                  'e': (-w / 2, -hh / 2, k, hh), 'c': (w / 2, -hh / 2, k, hh)}[sg]
                p.add(props.box(0.004, ly, lz), 'deck_line', T(x, yc - sx * dy, z0 + dz / 2))


def _ironclad_extras(p, hull, deck):
    # the casemate's sloped armor with gunports and rivet rows, a flat-topped turret on top
    for sx in (-1, 1):
        for k in range(4):
            y = -0.3 + 0.2 * k
            p.add(props.box(0.006, 0.05, 0.035), 'port', T(sx * 0.225, y, deck(y) + 0.06))
        for z in (0.03, 0.065):   # rivet rows along the hull sides
            for k in range(24):
                y = -0.85 + 1.7 * k / 23
                s = hull.s_of(y)
                if z < hull.deck_z(s):
                    p.add(props.ellipsoid(0.006, 0.006, 0.006, seg=6), 'steel_light',
                          T(sx * (hull.side_x(s, z) + 0.004), y, z), smooth=True)
    p.add(props.lathe([(0.0, 0.13), (0.1, 0.13), (0.105, 0.0)], seg=20), 'steel_dark', T(0, 0.0, deck(0) + 0.1), smooth=False)
    p.add(props.lathe([(0.0, 0.135), (0.012, 0.135)], seg=20), 'steel_light', T(0, 0.0, deck(0) + 0.19), smooth=True)
    for gx in (-0.04, 0.04):
        p.add(props.lathe([(0.0, 0.02), (0.2, 0.016)], seg=8), 'steel_dark', T(gx, 0.12, deck(0) + 0.15) @ R('X', -90),
              smooth=True)


def _transport_extras(p, hull, deck):
    # an LST: a long flat cargo deck under green tarps with civ patches, the bow ramp's raised frame
    for y in (-0.45, -0.05, 0.35):
        z0 = deck(y)
        p.add(props.box(0.5, 0.36, 0.1), 'tarp', T(0, y, z0 + 0.05), bevel=0.03)
        p.add(props.box(0.22, 0.16, 0.012), 'tint', T(0.08, y + 0.04, z0 + 0.104), bevel=0.003, tint=True)
        for dy in (-0.12, 0.12):
            p.add(props.box(0.52, 0.02, 0.105), 'steel_dark', T(0, y + dy, z0 + 0.05), bevel=0.004)   # lashing straps
    yb = hull.L / 2 * 0.86
    for sx in (-1, 1):
        vkit.rod(p, (sx * 0.22, yb, deck(yb)), (sx * 0.22, yb, deck(yb) + 0.22), 0.016, 'steel_dark', seg=6)
    vkit.rod(p, (-0.22, yb, deck(yb) + 0.22), (0.22, yb, deck(yb) + 0.22), 0.016, 'steel_dark', seg=6)
    p.add(props.box(0.46, 0.05, 0.16), 'steel_light', T(0, yb + 0.03, deck(yb) + 0.08), bevel=0.01)   # the ramp, raised


def _aegis_extras(p, hull, deck):
    # vertical launch cells fore and aft, the phased-array faces, white radomes
    for y0 in (0.55, -0.72):
        for i in range(4):
            for j in range(2):
                p.add(props.box(0.05, 0.05, 0.01), 'steel_dark', T(-0.05 + 0.1 * j, y0 + 0.055 * i, deck(y0) + 0.008), bevel=0.003)
    for sx in (-1, 1):
        for y in (0.3, -0.12):
            p.add(props.box(0.006, 0.11, 0.11), 'tint', T(sx * 0.133, y, deck(y) + 0.26) @ R('Z', sx * 0), tint=True)
    for x, y, z in ((-0.06, 0.18, 0.56), (0.06, 0.1, 0.56)):   # two prominent white radomes
        p.add(props.ellipsoid(0.07, 0.07, 0.07, seg=16), 'radome', T(x, y, deck(y) + z), smooth=True)
    p.add(props.box(0.28, 0.36, 0.2), 'tint', T(0, -0.85, deck(-0.85) + 0.1), bevel=0.015, tint=True)   # the civ hangar aft
    for sx in (-1, 1):   # civ strips along the bow
        p.add(props.box(0.006, 0.5, 0.04), 'tint', T(sx * 0.19, 0.95, deck(0.95) - 0.05), tint=True)
    p.add(props.box(0.14, 0.18, 0.06), 'steel_light', T(0, 1.0, deck(1.0) + 0.03), bevel=0.01)   # the raised gun's mount
    for sx in (-1, 1):   # canister launchers amidships
        p.add(props.box(0.05, 0.22, 0.05), 'steel_dark', T(sx * 0.12, -0.1, deck(-0.1) + 0.04) @ R('X', 15), bevel=0.006)


def _carrier_extras(p, hull, deck):
    # an angled flight deck flaring out to port, its lines, civ-colored aircraft parked along the edges, the island
    L = hull.L
    zd = deck(0) + 0.08
    # the flight deck as ONE extruded outline: the straight deck with the angled deck flaring out to port
    h2 = L * 0.49
    outline = [(0.45, h2), (0.45, -h2), (-0.37, -h2), (-0.66, -0.95), (-0.66, 0.15), (-0.37, 0.85), (-0.37, h2)]
    bm = props.bmesh.new()
    top = [bm.verts.new((x, y, zd + 0.015)) for x, y in outline]
    bot = [bm.verts.new((x, y, zd - 0.035)) for x, y in outline]
    bm.faces.new(top)
    bm.faces.new(list(reversed(bot)))
    for i in range(len(outline)):
        j = (i + 1) % len(outline)
        bm.faces.new((bot[i], bot[j], top[j], top[i]))
    p.add(bm, 'flight_deck', bevel=0.004)
    p.add(props.box(0.012, L * 0.9, 0.004), 'deck_line', T(0.0, 0.0, zd + 0.017))
    p.add(props.box(0.012, L * 0.6, 0.004), 'deck_line', T(-0.3, -0.25, zd + 0.017) @ R('Z', -9))
    for sx in (-1, 1):
        p.add(props.box(0.008, L * 0.95, 0.004), 'deck_line', T(sx * 0.38, 0.0, zd + 0.017))
    jets = [(0.29, -1.2 + 0.3 * i) for i in range(6)] + [(-0.28, 0.4 + 0.3 * i) for i in range(4)]
    for x, y in jets:   # big civ-colored jets parked along both deck edges
        k = 1.6
        p.add(props.box(0.04 * k, 0.2 * k, 0.025 * k), 'tint', T(x, y, zd + 0.035), bevel=0.008, tint=True)
        p.add(props.box(0.17 * k, 0.06 * k, 0.008 * k), 'tint', T(x, y - 0.02, zd + 0.035), bevel=0.003, tint=True)
        p.add(props.box(0.06 * k, 0.03 * k, 0.006 * k), 'tint', T(x, y - 0.15, zd + 0.04), bevel=0.002, tint=True)
    p.add(props.box(0.24, 0.3, 0.004), 'tint', T(0.25, 0.95, zd + 0.017), tint=True)   # the civ elevator patch
    for y in (-0.9, -0.3, 0.4, 1.1):   # centreline circles and the yellow dashes
        p.add(props.lathe([(0.0, 0.1), (0.004, 0.1)], seg=20), 'gold', T(0, y, zd + 0.016), smooth=True)
        p.add(props.lathe([(0.0, 0.08), (0.005, 0.08)], seg=20), 'flight_deck', T(0, y, zd + 0.017), smooth=True)
    for k in range(12):
        p.add(props.box(0.012, 0.08, 0.004), 'gold', T(-0.12, -1.4 + 0.24 * k, zd + 0.017))
    p.add(props.box(0.12, 0.42, 0.22), 'steel_light', T(0.34, -0.1, zd + 0.12), bevel=0.012)   # the island
    p.add(props.box(0.1, 0.24, 0.1), 'steel_light', T(0.34, -0.06, zd + 0.28), bevel=0.01)
    p.add(props.box(0.125, 0.008, 0.06), 'tint', T(0.34, 0.11, zd + 0.2), tint=True)
    _lattice_mast(p, (0.34, -0.05, zd + 0.33), 0.3, w=0.04)


def _sub_extras(p, hull, deck, forward=0.15, tail=False):
    # the conning tower (sail) in the civ color, with its planes, periscopes and masts; the rudder (and a cruciform tail)
    zs = deck(forward)
    p.add(props.box(0.09, 0.34, 0.2), 'tint', T(0, forward, zs + 0.1), bevel=0.035, tint=True)
    p.add(props.box(0.3, 0.06, 0.012), 'steel_dark', T(0, forward + 0.05, zs + 0.14), bevel=0.004)
    for dy, h in ((0.05, 0.16), (-0.02, 0.22), (-0.08, 0.12)):
        vkit.rod(p, (0, forward + dy, zs + 0.2), (0, forward + dy, zs + 0.2 + h), 0.007, 'steel_dark', seg=6)
    ys = -hull.L / 2 * 0.93
    p.add(props.box(0.012, 0.12, 0.16), 'steel_dark', T(0, ys, deck(0) - 0.01), bevel=0.004)
    if tail:
        p.add(props.box(0.24, 0.1, 0.012), 'steel_dark', T(0, ys, deck(0) - 0.04), bevel=0.004)


STEEL = {
    'Ironclad': dict(
        hull=dict(L=2.0, B=0.46, D=0.08, draft=0.12, sheer_stern=0.02, sheer_bow=0.04, bow_p=1.6, stern_p=1.7,
                  fullness=0.7),
        hull_color='#4a4e52', deck='#54585c',
        blocks=[(0, 0.0, 0.0, 0.46, 0.9, 0.1, 'steel_dark')],
        stacks=[(-0.35, 0.28, 0.05, 0.1)], masts=[(0.55, 0.35, 0.0)],
        civ_panels=[(0.62, 0.86), (-0.9, -0.66)], windows=False, number=False, civ_blocks=0,
        extras=[_ironclad_extras], ensign_y=-0.85,
    ),
    'Transport': dict(
        hull=dict(L=2.2, B=0.62, D=0.2, draft=0.12, sheer_stern=0.02, sheer_bow=0.05, bow_p=3.2, stern_p=3.2,
                  transom=0.8, bow_transom=0.85, fullness=0.25),   # a flat-sided, square-ended LST
        hull_color='#5e6266', deck='#6e7276',
        blocks=[(0, -0.88, 0.0, 0.52, 0.34, 0.24, 'radome'), (0, -0.9, 0.24, 0.38, 0.22, 0.12, 'radome')],
        stacks=[(-0.98, 0.16, 0.05, 0.36)], masts=[(-0.9, 0.22, 0.36)],
        civ_panels=[], number=False, civ_blocks=1,
        extras=[_transport_extras],
    ),
    'Destroyer': dict(
        hull=dict(L=2.4, B=0.34, D=0.16, draft=0.1, sheer_stern=0.02, sheer_bow=0.12, bow_p=1.4, stern_p=2.4,
                  transom=0.5, rake_bow=0.3, fullness=0.55),
        blocks=[(0, 0.32, 0.0, 0.24, 0.42, 0.18, 'steel_light'), (0, 0.36, 0.18, 0.18, 0.24, 0.12, 'steel_light'),
                (0, 0.38, 0.3, 0.12, 0.12, 0.08, 'steel_light'), (0, -0.28, 0.0, 0.22, 0.55, 0.14, 'steel_light')],
        turrets=[(0.72, 0, 1, 0.65, 0.0), (0.58, 0, 1, 0.65, 0.07), (-0.78, 180, 1, 0.65, 0.0)],
        stacks=[(0.04, 0.2, 0.055, 0.12), (-0.2, 0.18, 0.055, 0.12)],
        extras=[lambda p, h, d: (_lattice_mast(p, (0, 0.3, d(0.3) + 0.38), 0.4), _lattice_mast(p, (0, -0.12, d(-0.12) + 0.14), 0.3, w=0.05),
                                 _hull_number(p, h, '110', size=1.7))],
        hull_civ=(0.1, 0.13, 'tint'), number=False, barrel=0.35,
    ),
    'Cruiser': dict(
        hull=dict(L=3.3, B=0.44, D=0.18, draft=0.11, sheer_stern=0.02, sheer_bow=0.12, bow_p=1.4, stern_p=2.4,
                  transom=0.5, rake_bow=0.35, fullness=0.55),
        deck='#a08a64',   # the long wood deck
        blocks=[(0, 0.3, 0.0, 0.3, 0.56, 0.2, 'steel_light'), (0, 0.34, 0.2, 0.22, 0.3, 0.14, 'steel_light'),
                (0, 0.36, 0.34, 0.14, 0.14, 0.1, 'steel_light'), (0, -0.38, 0.0, 0.26, 0.6, 0.14, 'steel_light')],
        turrets=[(0.98, 0, 3, 0.85, 0.0), (0.74, 0, 3, 0.85, 0.08), (-0.98, 180, 3, 0.85, 0.0)],
        stacks=[(-0.02, 0.36, 0.1, 0.14), (-0.3, 0.34, 0.1, 0.14)],   # two big civ-banded funnels
        extras=[lambda p, h, d: (_lattice_mast(p, (0, 0.32, d(0.32) + 0.44), 0.42), _hull_number(p, h, '27', size=1.6))],
        number=False, barrel=0.42,
    ),
    'Battleship': dict(
        hull=dict(L=3.5, B=0.64, D=0.22, draft=0.14, sheer_stern=0.02, sheer_bow=0.14, bow_p=1.3, stern_p=2.3,
                  transom=0.55, rake_bow=0.4, fullness=0.65),
        civ_panels=[(1.02, 1.74), (-1.74, -1.2)],   # the big civ deck patches at both ends, as the original
        blocks=[(0, 0.15, 0.0, 0.42, 0.85, 0.2, 'steel_light'), (0, 0.22, 0.2, 0.32, 0.5, 0.18, 'steel_light'),
                (0, 0.28, 0.38, 0.22, 0.26, 0.2, 'steel_light'), (0, 0.3, 0.58, 0.16, 0.14, 0.22, 'steel_light'),
                (0, 0.3, 0.8, 0.12, 0.1, 0.1, 'steel_light'),   # the tall civ-faced tower
                (0, -0.6, 0.0, 0.36, 0.55, 0.14, 'steel_light')],
        civ_blocks=6,
        turrets=[(1.12, 0, 3, 1.3, 0.0), (0.84, 0, 3, 1.3, 0.1), (-0.95, 180, 3, 1.3, 0.1), (-1.22, 180, 3, 1.3, 0.0),
                 # secondary batteries along the superstructure's sides
                 (0.35, 90, 2, 0.55, 0.02), (0.0, 90, 2, 0.55, 0.02), (0.35, -90, 2, 0.55, 0.02), (0.0, -90, 2, 0.55, 0.02)],
        stacks=[(-0.15, 0.3, 0.09, 0.2)],
        extras=[lambda p, h, d: (_lattice_mast(p, (0, 0.3, d(0.3) + 0.9), 0.4), _lattice_mast(p, (0, -0.6, d(-0.6) + 0.14), 0.36))],
        number=False, barrel=0.6,
    ),
    'Aegis Cruiser': dict(
        hull=dict(L=2.9, B=0.44, D=0.2, draft=0.11, sheer_stern=0.02, sheer_bow=0.14, bow_p=1.4, stern_p=2.4,
                  transom=0.55, rake_bow=0.4, fullness=0.55),
        hull_color='#7a7e82', deck='#5e6266',
        blocks=[(0, 0.12, 0.0, 0.28, 0.66, 0.26, 'steel_light'), (0, 0.16, 0.26, 0.22, 0.36, 0.14, 'steel_light'),
                (0, 0.18, 0.4, 0.14, 0.16, 0.08, 'steel_light'), (0, -0.42, 0.0, 0.26, 0.36, 0.2, 'steel_light')],
        civ_blocks=3,
        turrets=[(1.0, 0, 1, 0.6, 0.0), (-1.1, 180, 1, 0.6, 0.0)],
        stacks=[(-0.2, 0.14, 0.065, 0.2)], masts=[(0.16, 0.36, 0.48)],
        extras=[_aegis_extras, lambda p, h, d: _hull_number(p, h, '52')], barrel=0.3, number=False,
    ),
    'Carrier': dict(
        hull=dict(L=3.6, B=0.62, D=0.16, draft=0.13, sheer_stern=0.0, sheer_bow=0.04, bow_p=1.6, stern_p=2.6,
                  transom=0.7, rake_bow=0.4, fullness=0.6),
        civ_panels=[], number=False, windows=False, civ_blocks=0,
        extras=[_carrier_extras], ensign_y=-1.7,
    ),
    'Submarine': dict(
        hull=dict(L=2.2, B=0.26, D=0.035, draft=0.12, sheer_stern=0.0, sheer_bow=0.0, bow_p=2.0, stern_p=1.5,
                  fullness=1.0),
        hull_color='#3e6460', deck='#4a706c', civ_panels=[], number=False, windows=False, civ_blocks=0,
        turrets=[(0.45, 0, 1, 0.45, 0.0)], extras=[_sub_extras], barrel=0.25, ensign_y=-0.15, ensign_h=0.0,
    ),
    'Nuclear Submarine': dict(
        hull=dict(L=2.8, B=0.3, D=0.04, draft=0.15, sheer_stern=0.0, sheer_bow=0.0, bow_p=2.4, stern_p=1.4,
                  fullness=1.0),
        hull_color='#34524e', deck='#3e5c58', civ_panels=[], number=False, windows=False, civ_blocks=0,
        extras=[lambda p, h, d: _sub_extras(p, h, d, forward=0.55, tail=True)], ensign_y=0.4, ensign_h=0.0,
    ),
}


# ------------------------------------------------------------ ironclad (realism pass: a casemate ironclad, 1862)

# A casemate ironclad of the 1860s (CSS Virginia's type; notes/realism/Ironclad.md): ~84 x 15 m (L:B ~5.5),
# a long hull nearly awash, a sloped iron casemate with rounded ends, gunports and a grated roof, one tall
# funnel, a pilot house, an iron ram at the waterline. L 2.2 = 84 m (1 unit = 38 m).
IRONCLAD = dict(
    L=2.2, B=0.5, D=0.032, draft=0.12, sheer_stern=0.02, sheer_bow=0.03,
    cas_y=(-0.72, 0.62), cas_h=0.25, cas_top=0.42,   # the casemate: length, height, top width as a share of the base
    ports=4, funnel=(-0.05, 0.34, 0.09), civ_patches=(),
    flag=(0.26, 0.17),
    fit_scale=1.06,
)

IRONCLAD_COLORS = {
    'iron_hull': '#34302c', 'iron_cas': '#262422', 'iron_cas_alt': '#201e1c', 'iron_dark': '#1c1a18', 'foam': '#b4bab4', 'glass': '#0a0908',
    'iron_edge': '#a49a8c', 'deck_wet': '#3a3632', 'iron_hull_alt': '#2e2a26', 'tar': '#22201e', 'port_lid': '#141210', 'grating': '#3a3632',
    'rust': '#6e4a32', 'brass': '#a8843e',
}
for _k, _v in (('iron_hull', ('#3e3a36', 0.6, 0.5, 'metal')), ('iron_cas', ('#56514b', 0.62, 0.5, 'metal')),
               ('iron_cas_alt', ('#4c4742', 0.62, 0.5, 'metal')), ('iron_dark', ('#2a2826', 0.6, 0.5, 'metal')),
               ('iron_edge', ('#8a8378', 0.5, 0.6, 'metal')), ('deck_wet', ('#4a4038', 0.7, 0.0, 'wood')),
               ('grating', ('#2a2724', 0.7, 0.3, 'metal')), ('rust', ('#6e4a32', 0.8, 0.2, 'metal')),
               ('brass', ('#a8843e', 0.4, 1.0, 'metal')), ('port_lid', ('#141210', 0.6, 0.0, 'wood')),
               ('tar', ('#2e261e', 0.7, 0.0, 'wood')), ('foam', ('#9aa09c', 0.8, 0.0, None)),
               ('iron_hull_alt', ('#2e2a26', 0.6, 0.5, 'metal')), ('glass', ('#1a1610', 0.3, 0.0, None))):
    props.DEFAULTS.setdefault(_k, _v)
IRONCLAD_WEAR = {'iron_hull': {'grime': 0.45, 'rust': 0.25}, 'iron_cas': {'grime': 0.4, 'rust': 0.2},
                 'iron_cas_alt': {'grime': 0.4, 'rust': 0.2}, 'iron_dark': {'grime': 0.4, 'rust': 0.15},
                 'deck_wet': {'grime': 0.5}}


def ironclad_casemate(colors, **kw):
    """The realism pass's Ironclad: a casemate ironclad of 1862: a long iron
    hull nearly awash with a rusty waterline, a sloped iron casemate with
    rounded ends in courses of plate (light plate edges), lidded gunports, a
    dark grated roof, a pilot house at its fore end, one tall funnel with a
    civ band, an iron ram, civ deck patches fore and aft and a civ ensign."""
    g = dict(IRONCLAD, **kw)
    c = dict(SHIP_COLORS, **IRONCLAD_COLORS)
    c.update(colors)
    root = vkit.root('Ironclad')
    root['fit_scale'] = g['fit_scale']
    p = vkit.part(root, 'hull', c, IRONCLAD_WEAR)
    hull = Hull(L=g['L'], B=g['B'], D=g['D'], draft=g['draft'], sheer_stern=g['sheer_stern'],
                sheer_bow=g['sheer_bow'], bow_p=1.2, stern_p=2.6, fullness=0.75)   # a rounded stern
    yb = g['L'] / 2

    def deck(y):
        return hull.deck_z(hull.s_of(y))

    ss = [-1 + 2 * i / 44 for i in range(45)]
    bm = props.loft([hull.section(s, m=28) for s in ss])

    def hkeys(f):
        cc = f.calc_center_median()
        if f.normal.z > 0.85:
            if any(y0 * yb < cc.y < y1 * yb for y0, y1 in g['civ_patches']):
                return ('tint', True)
            return ('deck_wet', False)
        if cc.z < 0.015:
            return ('rust', False)   # the rusty waterline
        if int(math.floor((cc.y + 2) / 0.16)) % 2:
            return ('iron_hull_alt', False)   # plate seams along the side
        return None

    p.add(bm, 'iron_hull', smooth=True, keys=hkeys)
    # the casemate: sloped sides and rounded ends, courses of plate, light plate edges
    y0, y1 = g['cas_y']
    h, tw = g['cas_h'], g['cas_top']
    rings = []
    n = 24
    for i in range(n + 1):
        t = i / n
        y = y0 + (y1 - y0) * t
        end = min(t, 1 - t) / 0.12   # the rounded ends: the section shrinks over the last 12 %
        f = min(1.0, end) ** 0.5
        b = hull.deck_half(hull.s_of(y)) * 0.92 * max(0.2, f)
        z0 = deck(y) - 0.005
        hh = h * max(0.3, f)
        rings.append([(b, y, z0), (b * tw, y, z0 + hh), (-b * tw, y, z0 + hh), (-b, y, z0)])
    cas = props.loft(rings)
    p.add(cas, 'iron_cas', smooth=False,
          keys=lambda f: ('grating', False) if f.normal.z > 0.9 else (('iron_cas_alt', False) if int(f.calc_center_median().z / 0.03) % 2 else None))
    for sx in (-1, 1):   # the plate edges: light lines along the casemate's courses and its eaves
        for frac in (0.33, 0.66, 1.0):
            path = []
            for r in rings[2:-2]:
                x0, yy, z0 = r[0]
                x1, _, z1 = r[1]
                path.append((sx * (x0 + (x1 - x0) * frac) * 1.01, yy, z0 + (z1 - z0) * frac + 0.002))
            p.add(props.tube(path, 0.0045 if frac < 1 else 0.006, seg=5), 'iron_edge', smooth=True)
        # lidded gunports on the sloped sides
        for k in range(g['ports']):
            y = y0 + (y1 - y0) * (0.2 + 0.6 * k / (g['ports'] - 1))
            b = hull.deck_half(hull.s_of(y)) * 0.92
            z = deck(y) + h * 0.45
            x = sx * b * (1 - 0.55 * (1 - tw))
            m = T(x, y, z) @ R('Y', sx * 45)
            p.add(props.box(0.02, 0.075, 0.06), 'glass', m, bevel=0.003)   # a deep black port recess
            p.add(props.box(0.01, 0.08, 0.012), 'iron_edge', m @ T(0, 0, -0.033), bevel=0.002)   # its lit sill
    p.add(props.box(0.09, (y1 - y0) * 0.75, 0.008), 'tint', T(0, (y0 + y1) / 2 - 0.02, deck(0) + h + 0.002), bevel=0.002, tint=True)   # a painted stripe along the roof
    for sx in (-1, 1):   # a dark waterline band, and a small bow wave
        path = [(sx * (hull.side_x(s_, 0.006) + 0.005), hull.y(s_), 0.006) for s_ in [-0.96 + 1.92 * i / 50 for i in range(51)]]
        p.add(props.tube(path, 0.009, seg=5, scale_xy=(1.0, 0.5)), 'iron_dark', smooth=True)
        wave = [(sx * (hull.side_x(hull.s_of(yb * 0.97), 0.004) + 0.01), yb * 0.97, 0.004), (sx * (hull.side_x(hull.s_of(yb * 0.8), 0.004) + 0.02), yb * 0.8, 0.003),
                (sx * (hull.side_x(hull.s_of(yb * 0.62), 0.004) + 0.026), yb * 0.62, 0.002)]
        p.add(props.tube(wave, [0.014, 0.012, 0.01], seg=6, scale_xy=(1.0, 0.6)), 'foam', smooth=True)
    # the pilot house at the fore end of the casemate, the funnel with a civ band, a ventilator
    yp = y1 - 0.12
    zr = deck(yp) + h
    p.add(props.lathe([(0.0, 0.06), (0.06, 0.042), (0.062, 0.0)], seg=16), 'iron_dark', T(0, yp, zr - 0.01), smooth=True)   # the pilot house: a dark truncated cone
    p.add(props.lathe([(0.0, 0.061), (0.008, 0.061)], seg=16), 'iron_edge', T(0, yp, zr - 0.01), smooth=True)   # its lit foot
    fy, fh, fr = g['funnel']
    zf = deck(fy) + h - 0.01
    p.add(props.lathe([(0.0, fr), (fh, fr * 0.95)], seg=16), 'iron_dark', T(0, fy, zf), smooth=True)
    p.add(props.lathe([(0.0, fr * 1.12), (0.03, fr * 1.12)], seg=16), 'iron_edge', T(0, fy, zf + fh - 0.03), smooth=True)
    p.add(props.lathe([(0.0, fr * 1.02), (fh * 0.3, fr * 1.02)], seg=16), 'tint', T(0, fy, zf + fh * 0.62), smooth=True, tint=True)   # the band in its top third
    p.add(props.lathe([(0.0, 0.022), (0.08, 0.022), (0.1, 0.032)], seg=10), 'iron_dark', T(0, fy - 0.22, zf), smooth=True)
    # the iron ram at the bow's waterline, a bow and a stern pivot port
    p.add(props.lathe([(0.0, 0.04), (0.12, 0.03), (0.2, 0.006)], seg=12, scale_xy=(0.7, 1.0)), 'iron_dark',
          T(0, yb - 0.06, 0.012) @ R('X', -90), smooth=True)   # the iron ram, protruding
    p.add(props.tube([(0, yb - 0.04, 0.04), (0, yb + 0.12, 0.024)], 0.006, seg=5), 'iron_edge', smooth=True)   # its lit top edge
    for yy, sy in ((y1, 1), (y0, -1)):
        p.add(props.box(0.04, 0.012, 0.035), 'port_lid', T(0, yy + sy * 0.005, deck(yy) + h * 0.4), bevel=0.003)
    # the ensign on a staff at the stern: a solid civ flag
    ys = -yb * 0.9
    zs = deck(ys)
    vkit.rod(p, (0, ys, zs), (0, ys, zs + 0.3), 0.012, 'iron_dark', seg=6)
    fw, fh2 = g['flag']
    bmf = props.bmesh.new()
    rows = []
    for jj in range(9):
        t = jj / 8
        rows.append([bmf.verts.new((0.03 * math.sin(4 * t), ys - fw * t, zs + 0.3 - fh2 * ii / 4)) for ii in range(5)])
    for jj in range(8):
        for ii in range(4):
            bmf.faces.new((rows[jj][ii], rows[jj + 1][ii], rows[jj + 1][ii + 1], rows[jj][ii + 1]))
    p.add(bmf, 'tint', smooth=True, tint=True)
    p.finish()
    return root


# ------------------------------------------------------------ transport (realism pass: an LST, 1943)

# A Landing Ship, Tank (LST-1 class, 1943; notes/realism/Transport.md): ~100 x 15 m (L:B ~6.6), a long,
# low, flat-sided hull with clamshell bow doors, the deck a vehicle park, the superstructure and
# twin stacks aft, landing craft (LCVPs) in davits along the sides. L 2.4 = 100 m (1 unit = 42 m).
LST = dict(
    L=2.0, B=0.46, D=0.22, draft=0.08, sheer_stern=0.02, sheer_bow=0.07,
    bridge=(-0.72, -0.44, 0.2), pilot=(-0.54, 0.1), cargo=((0.48, 'tank'), (0.18, 'truck'), (-0.12, 'tank'), (-0.36, 'truck')),
    boats=(-0.1, 0.18), civ_deck=False,
    fit_scale=0.95,
)

LST_COLORS = {
    'lst_hull': '#62665e', 'lst_hull_alt': '#565a52', 'lst_deck': '#46494a', 'lst_super': '#80847e', 'lst_dark': '#3a3d40',
    'boot': '#5a2a24', 'od': '#6e7044', 'od_dark': '#3a3c26', 'tarp': '#5e6040', 'lcvp': '#9ea29e', 'glass': '#0e0e10', 'port': '#141618',
    'rubber': '#1e1e1e', 'foam': '#b4bab4',
}
for _k, _v in (('lst_hull', ('#6a6e70', 0.6, 0.15, 'metal')), ('lst_hull_alt', ('#62666a', 0.6, 0.15, 'metal')),
               ('lst_deck', ('#4a4e50', 0.75, 0.1, 'metal')), ('lst_super', ('#8c9092', 0.55, 0.15, 'metal')),
               ('lst_dark', ('#3a3d40', 0.6, 0.15, 'metal')), ('od', ('#5c5e3a', 0.75, 0.0, 'cloth')),
               ('od_dark', ('#3e4028', 0.75, 0.0, 'cloth')), ('lcvp', ('#7e8284', 0.6, 0.4, 'metal')),
               ('rubber', ('#1e1e1e', 0.9, 0.0, None)), ('foam', ('#b4bab4', 0.8, 0.0, None)), ('glass', ('#1a1610', 0.3, 0.0, None))):
    props.DEFAULTS.setdefault(_k, _v)
LST_WEAR = {'lst_hull': {'grime': 0.55, 'rust': 0.16}, 'lst_hull_alt': {'grime': 0.55, 'rust': 0.16},
            'lst_deck': {'grime': 0.5}, 'lst_super': {'grime': 0.3, 'rust': 0.06}, 'boot': {'grime': 0.4}}


def _lst_vehicle(p, kind, y, z, rot=0.0):
    """A deck-load vehicle in olive drab: a truck (cab and tilted bed), a tank
    (hull, turret and gun), a jeep."""
    m = T(0, y, z) @ R('Z', rot) @ props.S(1.35)   # chunkier: they read at 1x
    if kind == 'truck':
        p.add(props.box(0.09, 0.2, 0.05), 'od', m @ T(0, -0.02, 0.035), bevel=0.008)
        p.add(props.box(0.085, 0.13, 0.04), 'od_dark', m @ T(0, -0.05, 0.075), bevel=0.012)   # the tilt
        p.add(props.box(0.08, 0.05, 0.045), 'od', m @ T(0, 0.09, 0.05), bevel=0.01)          # the cab
    elif kind == 'tank':
        p.add(props.box(0.11, 0.2, 0.04), 'od_dark', m @ T(0, 0, 0.02), bevel=0.008)
        p.add(props.box(0.1, 0.17, 0.03), 'od', m @ T(0, 0, 0.05), bevel=0.008)
        p.add(props.lathe([(0.0, 0.04), (0.03, 0.036), (0.035, 0.0)], seg=12), 'od', m @ T(0, -0.01, 0.065), smooth=True)
        vkit.rod(p, (0, y + 0.02, z + 0.08), (0, y + 0.14, z + 0.08), 0.009, 'od_dark', seg=6)
    else:
        p.add(props.box(0.06, 0.1, 0.03), 'od', m @ T(0, 0, 0.025), bevel=0.008)


def transport_lst(colors, **kw):
    """The realism pass's Transport: an LST of 1943: a long, low, flat-sided
    grey hull with a red boot topping and plate seams, clamshell bow doors,
    the weather deck loaded with olive-drab trucks, tanks and a jeep, LCVPs in
    davits along the sides, the bridge and twin stacks aft, a pole mast; the
    civ colour on the bridge band, the stacks' tops, a deck panel and the
    ensign."""
    g = dict(LST, **kw)
    c = dict(SHIP_COLORS, **LST_COLORS)
    c.update(colors)
    root = vkit.root('Transport')
    root['fit_scale'] = g['fit_scale']
    p = vkit.part(root, 'hull', c, LST_WEAR)
    hull = Hull(L=g['L'], B=g['B'], D=g['D'], draft=g['draft'], sheer_stern=g['sheer_stern'],
                sheer_bow=g['sheer_bow'], bow_p=1.8, stern_p=3.0, transom=0.8, bow_transom=0.35, fullness=0.3)   # a high, bluff, rounded bow
    yb = g['L'] / 2

    def deck(y):
        return hull.deck_z(hull.s_of(y))

    ss = [-1 + 2 * i / 44 for i in range(45)]
    bm = props.loft([hull.section(s, m=24) for s in ss])

    def hkeys(f):
        cc = f.calc_center_median()
        if f.normal.z > 0.85:
            return ('lst_deck', False)
        if cc.z < 0.03:
            return ('boot', False)
        if int(math.floor(cc.y / 0.2)) % 2 or cc.z < 0.06:
            return ('lst_hull_alt', False)   # plate courses along the side, the lower strake darker
        return None

    p.add(bm, 'lst_hull', smooth=False, keys=hkeys)
    for sx in (-1, 1):
        path = [(sx * (hull.side_x(s_, hull.deck_z(s_) - 0.015) + 0.004), hull.y(s_), hull.deck_z(s_) - 0.015) for s_ in [-0.96 + 1.92 * i / 50 for i in range(51)]]
        p.add(props.tube(path, 0.01, seg=6, scale_xy=(1.0, 0.45)), 'lst_dark', smooth=True)   # a dark sheer strake
    for sx in (-1, 1):   # a real waterline: a little foam along it
        path = [(sx * (hull.side_x(s_, 0.004) + 0.006), hull.y(s_), 0.004) for s_ in [-0.96 + 1.92 * i / 50 for i in range(51)]]
        p.add(props.tube(path, 0.008, seg=5, scale_xy=(1.0, 0.5)), 'foam', smooth=True)
    # the clamshell bow doors: two curved, shaded leaves on the rounded bow, a dark recessed centre seam,
    # hinge seams at their outer edges
    zbd = deck(yb * 0.96)
    yd = yb * 0.97
    bw = hull.deck_half(hull.s_of(yd))
    for sx in (-1, 1):
        p.add(props.ellipsoid(bw * 0.55, 0.05, (zbd - 0.02) / 2, seg=20), 'lst_super', T(sx * bw * 0.46, yd, zbd / 2 + 0.005), smooth=True)
        p.add(props.box(0.012, 0.03, zbd - 0.03), 'lst_dark', T(sx * bw * 0.98, yd - 0.01, zbd / 2 + 0.008), bevel=0.003)
    p.add(props.box(0.022, 0.06, zbd - 0.02), 'glass', T(0, yd + 0.02, zbd / 2 + 0.005), bevel=0.003)   # the dark seam recess
    p.add(props.box(hull.deck_half(hull.s_of(yb * 0.9)) * 1.9, 0.03, 0.05), 'lst_super', T(0, yb * 0.86, deck(yb * 0.86) + 0.025), bevel=0.006)
    # a low bulwark round the bow, a dark scupper line along the sides
    for sx in (-1, 1):
        path = [(sx * hull.deck_half(hull.s_of(y)) * 0.98, y, deck(y) + 0.012) for y in [yb * 0.55 + yb * 0.4 * k / 10 for k in range(11)]]
        p.add(props.tube(path, 0.012, seg=6), 'lst_hull', smooth=True)
    # the deck load
    for y, kind in g['cargo']:
        p.add(props.box(0.17, 0.3, 0.008), 'lst_dark', T(0, y, deck(y) + 0.004), bevel=0.003)   # a dark outline round each vehicle
        _lst_vehicle(p, kind, y, deck(y) + 0.008)
    if g['civ_deck']:   # an air-recognition panel on the deck (civ)
        p.add(props.box(0.24, 0.2, 0.008), 'tint', T(0, -0.27, deck(-0.27) + 0.004), bevel=0.002, tint=True)
    # LCVPs in davits along both sides: each boat tucked against the hull side, held by two dark davits
    # rooted at the deck edge (a post, an arm out over the boat, a fall down to it)
    for y in g['boats']:
        for sx in (-1, 1):
            dh = hull.deck_half(hull.s_of(y))
            x = sx * (dh + 0.044)
            z = deck(y) + 0.01
            p.add(props.box(0.082, 0.22, 0.05), 'lcvp', T(x, y, z - 0.005), bevel=0.018)          # a light grey boat hull
            p.add(props.box(0.062, 0.18, 0.02), 'lst_dark', T(x, y, z + 0.022), bevel=0.004)          # its dark well
            p.add(props.box(0.08, 0.02, 0.034), 'lcvp', T(x, y + 0.11, z + 0.012), bevel=0.006)       # its bow ramp
            for dy in (-0.07, 0.07):
                xp = sx * (dh - 0.006)
                p.add(props.box(0.022, 0.022, 0.12), 'lst_dark', T(xp, y + dy, deck(y) + 0.06), bevel=0.004)        # the davit post
                p.add(props.box(abs(x - xp) + 0.022, 0.022, 0.022), 'lst_dark', T((x + xp) / 2, y + dy, deck(y) + 0.12), bevel=0.004)   # its arm
                p.add(props.box(0.012, 0.012, deck(y) + 0.12 - (z + 0.03)), 'lst_dark', T(x, y + dy, (deck(y) + 0.12 + z + 0.03) / 2))   # the fall
    # the superstructure aft: a deckhouse, the bridge, the pilot house, twin stacks, a pole mast
    y0, y1, h = g['bridge']
    yc = (y0 + y1) / 2
    w = hull.deck_half(hull.s_of(yc)) * 1.7
    zc = deck(yc)
    p.add(props.box(w, y1 - y0, h), 'lst_super', T(0, yc, zc + h / 2), bevel=0.008)
    for k in range(5):   # scuttles
        for sx in (-1, 1):
            p.add(props.box(0.004, 0.02, 0.016), 'port', T(sx * (w / 2 + 0.001), y0 + 0.04 + k * 0.06, zc + h * 0.62))
    yp, hp = g['pilot']
    p.add(props.box(w * 0.7, 0.12, hp), 'lst_super', T(0, yp, zc + h + hp / 2), bevel=0.008)
    p.add(props.box(w * 0.72, 0.008, hp * 0.35), 'port', T(0, yp + 0.061, zc + h + hp * 0.6), bevel=0.002)   # the bridge windows
    p.add(props.box(w * 1.01, 0.008, h * 0.6), 'tint', T(0, y1 + 0.004, zc + h * 0.62), tint=True)            # a civ band on the front
    for sx in (-1, 1):
        p.add(props.box(0.006, (y1 - y0) * 0.94, h * 0.45), 'tint', T(sx * (w / 2 + 0.003), yc, zc + h * 0.82), tint=True)
        ys = y0 + 0.08
        p.add(props.lathe([(0.0, 0.056), (0.2, 0.052)], seg=14), 'lst_super', T(sx * 0.08, ys, zc + h), smooth=True)   # the stacks
        p.add(props.lathe([(0.0, 0.057), (0.08, 0.057)], seg=14), 'tint', T(sx * 0.08, ys, zc + h + 0.11), smooth=True, tint=True)   # civ stack tops
        p.add(props.lathe([(0.0, 0.058), (0.014, 0.058)], seg=14), 'lst_dark', T(sx * 0.08, ys, zc + h + 0.192), smooth=True)
    p.add(props.box(0.022, 0.022, 0.26), 'lst_dark', T(0, yp - 0.02, zc + h + hp + 0.13), bevel=0.004)   # the pole mast
    p.add(props.box(0.16, 0.02, 0.02), 'lst_dark', T(0, yp - 0.02, zc + h + hp + 0.2), bevel=0.004)      # its yard
    # the ensign at the stern
    ye = -yb * 0.96
    ze = deck(ye)
    vkit.rod(p, (0, ye, ze), (0, ye, ze + 0.2), 0.011, 'lst_dark', seg=6)
    bmf = props.bmesh.new()
    rows = []
    for jj in range(9):
        t = jj / 8
        rows.append([bmf.verts.new((0.03 * math.sin(4 * t), ye - 0.18 * t, ze + 0.2 - 0.12 * ii / 4)) for ii in range(5)])
    for jj in range(8):
        for ii in range(4):
            bmf.faces.new((rows[jj][ii], rows[jj + 1][ii], rows[jj + 1][ii + 1], rows[jj][ii + 1]))
    p.add(bmf, 'tint', smooth=True, tint=True)
    p.finish()
    return root


# ------------------------------------------------------------ carrier (realism pass: a Nimitz-class supercarrier)

# A Nimitz-class nuclear aircraft carrier (notes/realism/Carrier.md): a 333 x 77 m flight deck on a
# 317 x 41 m hull that flares out to carry it, the angled deck 9 deg to port, the island to starboard
# just aft of midships, four deck-edge elevators, the air wing parked round the edges.
# L 3.4 = 317 m (1 unit = 93 m).
CVN = dict(
    L=3.4, B=0.44, D=0.3, draft=0.12, flare=0.45, rake=0.4,
    fd=(0.3, 0.028),            # the flight deck: its underside z and thickness
    island=(0.36, -0.06),        # the island's centre (x, y)
    jet_scale=1.9, jets_civ=True, edge_civ=False, edge_stripe=0.0, sponson_civ=True, boot_civ=(0.026, 0.075), elevator_civ=True, bow_panel=(0.0, 1.27, 0.3, 0.26), stern_panel=(0.05, -1.6, 0.36, 0.14), number='68',
    fit_scale=0.99,
)

CVN_COLORS = {
    'cvn_hull': '#5a5e62', 'cvn_hull_dark': '#2e3236', 'cvn_deck': '#3c3f43', 'cvn_deck_dark': '#33363a',
    'cvn_boot': '#5a2a24', 'cvn_line': '#d8d6cc', 'cvn_yellow': '#c8a838', 'cvn_island': '#7a7e82',
    'cvn_window': '#141618', 'cvn_jet': '#74787c', 'cvn_dome': '#d6d6d0', 'cvn_canopy': '#1e2630',
    'cvn_foam': '#b8c0bc', 'cvn_slot': '#1a1c1e', 'cvn_rotodome': '#8c8e8a', 'cvn_wet': '#24282a',
}
for _k, _v in (('cvn_hull', ('#5a5e62', 0.55, 0.5, 'metal')), ('cvn_hull_dark', ('#2e3236', 0.6, 0.5, 'metal')),
               ('cvn_deck', ('#3c3f43', 0.85, 0.1, 'metal')), ('cvn_deck_dark', ('#33363a', 0.85, 0.1, 'metal')),
               ('cvn_boot', ('#5a2a24', 0.7, 0.2, 'metal')), ('cvn_line', ('#d8d6cc', 0.8, 0.0, 'cloth')),
               ('cvn_yellow', ('#c8a838', 0.8, 0.0, 'cloth')), ('cvn_island', ('#7a7e82', 0.5, 0.5, 'metal')),
               ('cvn_window', ('#141618', 0.3, 0.0, None)), ('cvn_jet', ('#74787c', 0.5, 0.4, 'metal')),
               ('cvn_dome', ('#d6d6d0', 0.6, 0.0, 'cloth')), ('cvn_canopy', ('#1e2630', 0.1, 0.0, None)),
               ('cvn_foam', ('#b8c0bc', 0.8, 0.0, None)), ('cvn_slot', ('#1a1c1e', 0.8, 0.0, None)),
               ('cvn_rotodome', ('#8c8e8a', 0.6, 0.2, 'metal')), ('cvn_wet', ('#24282a', 0.4, 0.3, 'metal'))):
    props.DEFAULTS.setdefault(_k, _v)
CVN_WEAR = {'cvn_hull': {'grime': 0.4, 'rust': 0.1}, 'cvn_hull_dark': {'grime': 0.4, 'rust': 0.08},
            'cvn_deck': {'grime': 0.55}, 'cvn_deck_dark': {'grime': 0.5}, 'cvn_boot': {'grime': 0.4},
            'cvn_island': {'grime': 0.3, 'rust': 0.05}}

# the flight deck's plan (x, y): the straight deck, the angled deck flaring out to port
CVN_DECK = [(0.14, 1.84), (0.27, 1.62), (0.33, 1.2), (0.36, 0.7), (0.37, -1.2), (0.33, -1.66), (0.22, -1.74),
            (-0.26, -1.74), (-0.36, -1.5), (-0.47, -0.62), (-0.47, -0.18), (-0.42, 0.3), (-0.3, 0.42),
            (-0.25, 0.9), (-0.2, 1.5), (-0.1, 1.84)]


def _cvn_plate(outline, z0, z1):
    """A flat plate: a closed (x, y) outline extruded from z0 to z1."""
    bm = props.bmesh.new()
    top = [bm.verts.new((x, y, z1)) for x, y in outline]
    bot = [bm.verts.new((x, y, z0)) for x, y in outline]
    bm.faces.new(top)
    bm.faces.new(list(reversed(bot)))
    n = len(outline)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((bot[i], bot[j], top[j], top[i]))
    props.bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def _cvn_edge_x(y, side):
    """The flight deck outline's x at y on the starboard (side 1) or port (-1) edge."""
    pts = sorted([(x, yy) for x, yy in CVN_DECK if (x > 0) == (side > 0)], key=lambda q: q[1])
    for (xa, ya), (xb, yb) in zip(pts, pts[1:]):
        if ya <= y <= yb:
            return xa + (xb - xa) * (y - ya) / max(yb - ya, 1e-6)
    return pts[0][0] if y < pts[0][1] else pts[-1][0]


def _cvn_line(p, a, b, w, key, z, h=0.003):
    """A painted line on the deck from a to b (x, y), w wide."""
    dx, dy = b[0] - a[0], b[1] - a[1]
    ln = math.hypot(dx, dy)
    p.add(props.box(w, ln, h), key, T((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, z + h / 2) @ R('Z', math.degrees(math.atan2(-dx, dy))))


def _cvn_jet(p, x, y, z, yaw, s, civ=True):
    """A parked strike fighter (an F/A-18 type), nose along +Y turned by yaw: the fuselage, swept wings,
    tailplanes, twin canted fins, a dark canopy. civ: the tails (fins and tailplanes) in the civ colour."""
    m = T(x, y, z) @ R('Z', yaw) @ props.S(s)
    key, t = 'cvn_jet', False
    tk, tt = ('tint', True) if civ else ('cvn_jet', False)
    p.add(props.lathe([(-0.09, 0.007), (-0.06, 0.011), (0.02, 0.012), (0.06, 0.009), (0.095, 0.0)], seg=10), key,
          m @ T(0, 0, 0.013) @ R('X', -90), smooth=True, tint=t)
    wing = [(-0.064, -0.02), (-0.012, 0.024), (0.012, 0.024), (0.064, -0.02), (0.064, -0.036), (0.012, -0.046),
            (-0.012, -0.046), (-0.064, -0.036)]
    p.add(_cvn_plate(wing, 0.009, 0.014), key, m, tint=t)
    tail = [(-0.042, -0.07), (-0.01, -0.058), (0.01, -0.058), (0.042, -0.07), (0.042, -0.086), (-0.042, -0.086)]
    p.add(_cvn_plate(tail, 0.01, 0.0145), tk, m, tint=tt)
    for sx in (-1, 1):
        p.add(props.box(0.007, 0.034, 0.036), tk, m @ T(sx * 0.014, -0.068, 0.034) @ R('Y', sx * 18), bevel=0.0015, tint=tt)
    p.add(props.ellipsoid(0.008, 0.02, 0.008, seg=10), 'cvn_canopy', m @ T(0, 0.045, 0.022), smooth=True)


def _cvn_hawkeye(p, x, y, z, yaw, s):
    """A parked E-2 Hawkeye: a grey airframe with straight wings, four fins and the white rotodome."""
    m = T(x, y, z) @ R('Z', yaw) @ props.S(s)
    p.add(props.lathe([(-0.08, 0.008), (-0.04, 0.012), (0.05, 0.013), (0.085, 0.0)], seg=10), 'cvn_jet',
          m @ T(0, 0, 0.016) @ R('X', -90), smooth=True)
    p.add(_cvn_plate([(-0.12, 0.022), (0.12, 0.022), (0.12, -0.006), (-0.12, -0.006)], 0.024, 0.029), 'cvn_jet', m)
    p.add(_cvn_plate([(-0.05, -0.06), (0.05, -0.06), (0.05, -0.08), (-0.05, -0.08)], 0.02, 0.024), 'cvn_jet', m)
    for fx in (-0.05, -0.018, 0.018, 0.05):
        p.add(props.box(0.008, 0.024, 0.03), 'cvn_jet', m @ T(fx, -0.07, 0.036), bevel=0.002)
    p.add(props.box(0.008, 0.012, 0.02), 'cvn_jet', m @ T(0, 0.0, 0.035))
    p.add(props.lathe([(0.0, 0.03), (0.005, 0.032), (0.01, 0.03)], seg=20), 'cvn_rotodome', m @ T(0, -0.005, 0.044), smooth=True)


def _cvn_digits(p, digits, x0, y0, z, h, key):
    """Seven-segment figures painted flat on the deck, read from astern (upright toward the bow)."""
    segs = {'0': 'abcdef', '1': 'bc', '2': 'abged', '3': 'abgcd', '4': 'fgbc', '5': 'afgcd', '6': 'afgedc', '7': 'abc',
            '8': 'abcdefg', '9': 'abcfgd'}
    w, k = h * 0.55, h * 0.14
    for i, d in enumerate(digits):
        xc = x0 + (i - (len(digits) - 1) / 2) * w * 1.45
        for sg in segs[d]:
            dx, dy, lx, ly = {'a': (0, h / 2, w, k), 'g': (0, 0, w, k), 'd': (0, -h / 2, w, k),
                              'f': (-w / 2, h / 4, k, h / 2), 'b': (w / 2, h / 4, k, h / 2),
                              'e': (-w / 2, -h / 4, k, h / 2), 'c': (w / 2, -h / 4, k, h / 2)}[sg]
            p.add(props.box(lx, ly, 0.003), key, T(xc + dx, y0 + dy, z + 0.0015))


def carrier_nimitz(colors, **kw):
    """The realism pass's Carrier: a Nimitz-class supercarrier: a haze-grey hull
    flaring out under the dark flight deck, the angled deck to port with its
    landing lines and tyre-dark strip, bow and waist catapults, deck-edge
    elevators, the island to starboard with its bridge windows, mast and radars,
    the air wing parked round the edges in the civ colour, a civ band round the
    island, the hull number on the bow."""
    g = dict(CVN, **kw)
    c = dict(SHIP_COLORS, **CVN_COLORS)
    c.update(colors)
    root = vkit.root('Carrier')
    root['fit_scale'] = g['fit_scale']
    p = vkit.part(root, 'hull', c, CVN_WEAR)
    hull = Hull(L=g['L'], B=g['B'], D=g['D'], draft=g['draft'], sheer_stern=0.0, sheer_bow=0.0, bow_p=1.6, stern_p=3.0,
                transom=0.85, rake_bow=g['rake'], fullness=0.6, tumblehome=-g['flare'])
    ss = [-1 + 2 * i / 44 for i in range(45)]
    bm = props.loft([hull.section(s, m=24) for s in ss])

    def hkeys(f):
        cc = f.calc_center_median()
        if cc.z < 0.025:
            return ('cvn_boot', False)
        if g['boot_civ'] and g['boot_civ'][0] <= cc.z < g['boot_civ'][1] and -1.45 < cc.y < 1.45:
            return ('tint', True)   # a civ band above the boot topping
        if cc.z > g['D'] * 0.8:
            return ('cvn_hull_dark', False)   # the shadowed gallery under the deck's overhang
        return None

    p.add(bm, 'cvn_hull', smooth=True, keys=hkeys)
    z0, t = g['fd']
    zt = z0 + t
    # the flight deck: one plate; its edge a civ stripe all round
    p.add(_cvn_plate(CVN_DECK, z0, zt), 'cvn_hull',
          keys=lambda f: ('cvn_deck', False) if f.normal.z > 0.5 else (('cvn_hull_dark', False) if f.normal.z < -0.5 else
                                                                     (('tint', True) if g['edge_civ'] else None)))
    # sponsons carrying the overhang (the angled deck to port, the gallery to starboard)
    for sx, ys in ((-1, (-1.35, -0.95, -0.55, -0.15, 0.2, 0.7, 1.2)), (1, (1.2, 0.95, -0.25, -1.0, -1.45))):
        for y in ys:
            xd = _cvn_edge_x(y, sx)
            xh = hull.deck_half(hull.s_of(y)) * 0.96
            if abs(xd) - xh > 0.02:
                p.add(_cvn_plate([(sx * xh, y + 0.05), (xd * 0.99, y + 0.05), (xd * 0.99, y - 0.05), (sx * xh, y - 0.05)],
                                 z0 - 0.05, z0 + 0.001), 'cvn_hull',
                      keys=lambda f, sx=sx: ('tint', True) if g['sponson_civ'] and f.normal.x * sx > 0.7 else None)   # a civ panel on its face
    for sx in (-1, 1):   # the hangar's openings and the gallery's dark scuttles along the sides
        for k in range(11):
            y = -1.35 + 0.27 * k
            s1 = hull.s_of(y)
            zh = g['D'] * 0.74
            p.add(props.box(0.008, 0.07, 0.022), 'cvn_window', T(sx * (hull.side_x(s1, zh) + 0.001), y, zh))
    zl = zt + 0.0005
    # the angled deck: the tyre-dark landing strip, its white edge lines, the yellow-and-white centreline, the wires
    a, b = (-0.02, -1.72), (-0.37, 0.38)
    ux, uy = b[0] - a[0], b[1] - a[1]
    ln = math.hypot(ux, uy)
    ux, uy = ux / ln, uy / ln
    nx, ny = uy, -ux   # to starboard of the centreline

    def at(tt, off):
        return (a[0] + ux * ln * tt + nx * off, a[1] + uy * ln * tt + ny * off)

    _cvn_line(p, at(0.02, 0), at(0.98, 0), 0.24, 'cvn_deck_dark', zl, h=0.002)
    for off in (-0.125, 0.125):
        _cvn_line(p, at(0.0, off), at(1.0, off), 0.014, 'cvn_line', zl)
    for k in range(14):
        _cvn_line(p, at(0.04 + k * 0.068, 0), at(0.075 + k * 0.068, 0), 0.012, 'cvn_line' if k % 2 else 'cvn_yellow', zl + 0.001)
    for k in range(4):
        _cvn_line(p, at(0.1 + 0.035 * k, -0.11), at(0.1 + 0.035 * k, 0.11), 0.006, 'cvn_line', zl)
    # catapults: two on the bow, two in the waist (dark slots with a white line beside)
    for cx in (-0.07, 0.07):
        _cvn_line(p, (cx, 1.4), (cx, 1.8), 0.009, 'cvn_slot', zl)
        _cvn_line(p, (cx + 0.02, 1.4), (cx + 0.02, 1.76), 0.005, 'cvn_line', zl)
    for off in (-0.06, 0.05):
        _cvn_line(p, at(0.58, off), at(0.96, off), 0.009, 'cvn_slot', zl)
    if g['bow_panel']:   # a painted civ panel on the bow, the hull number on it
        bx, by, bw, bl = g['bow_panel']
        p.add(props.box(bw, bl, 0.002), 'tint', T(bx, by, zl + 0.001), tint=True)
    if g['stern_panel']:   # and one on the fantail
        bx, by, bw, bl = g['stern_panel']
        p.add(props.box(bw, bl, 0.002), 'tint', T(bx, by, zl + 0.001), tint=True)
    _cvn_digits(p, g['number'], 0.0, 1.24, zl + 0.0015, 0.16, 'cvn_line')
    if g['edge_stripe']:   # a civ stripe painted round the deck's edge
        n_ = len(CVN_DECK)
        for i in range(n_):
            (xa, ya), (xb, yb) = CVN_DECK[i], CVN_DECK[(i + 1) % n_]
            dx, dy = xb - xa, yb - ya
            ll = math.hypot(dx, dy)
            # inset toward the deck's inside (the outline runs clockwise seen from above: inside is to the right)
            ix_, iy_ = dy / ll, -dx / ll
            w_ = g['edge_stripe']
            a_ = (xa + ix_ * w_ * 0.6, ya + iy_ * w_ * 0.6)
            b_ = (xb + ix_ * w_ * 0.6, yb + iy_ * w_ * 0.6)
            p.add(props.box(w_, ll + w_ * 0.3, 0.003), 'tint',
                  T((a_[0] + b_[0]) / 2, (a_[1] + b_[1]) / 2, zl + 0.0015) @ R('Z', math.degrees(math.atan2(-dx, dy))), tint=True)
    # deck-edge elevators: platforms flush with the deck, dark gaps where they meet it, a sponson and struts under them
    for ex, ey, w in ((0.42, 0.62, 0.17), (0.42, 0.3, 0.17), (0.43, -0.66, 0.16), (-0.47, -1.08, 0.16)):
        sx = 1 if ex > 0 else -1
        x_in = ex - sx * w / 2
        x_out = ex + sx * w / 2
        p.add(_cvn_plate([(ex - w / 2, ey + 0.11), (ex + w / 2, ey + 0.11), (ex + w / 2, ey - 0.11), (ex - w / 2, ey - 0.11)],
                         z0, zt + 0.0004), 'cvn_deck',
              keys=lambda f: (('tint', True) if g['elevator_civ'] else None) if f.normal.z > 0.5 else ('cvn_hull', False))   # painted civ
        _cvn_line(p, (x_in, ey - 0.11), (x_in, ey + 0.11), 0.008, 'cvn_slot', zt + 0.0004, h=0.0015)
        for yy in (ey - 0.11, ey + 0.11):
            _cvn_line(p, (x_in, yy), (x_out, yy), 0.008, 'cvn_slot', zt + 0.0004, h=0.0015)
        xh = sx * hull.deck_half(hull.s_of(ey)) * 0.96
        p.add(_cvn_plate([(xh, ey + 0.1), (x_out - sx * 0.03, ey + 0.1), (x_out - sx * 0.03, ey - 0.1), (xh, ey - 0.1)],
                         z0 - 0.045, z0 + 0.001), 'cvn_hull_dark',
              keys=lambda f, sx=sx: ('tint', True) if g['sponson_civ'] and f.normal.x * sx > 0.7 else None)   # the sponson under it
        for yy in (ey - 0.07, ey + 0.07):   # its struts down to the hull
            vkit.beam(p, (x_out - sx * 0.04, yy, z0 - 0.04), (xh, yy, z0 - 0.11), 0.016, 0.016, 'cvn_hull', bevel=0.003)
        # the hangar's open door behind it
        s = hull.s_of(ey)
        zh = g['D'] * 0.7
        p.add(props.box(0.01, 0.16, 0.05), 'cvn_window', T(sx * (hull.side_x(s, zh) + 0.001), ey, zh))
    # point-defence mounts on the deck corners' sponsons (white domes)
    for x, y in ((0.36, -1.62), (-0.3, -1.66), (0.29, 1.58)):
        p.add(props.box(0.06, 0.06, 0.03), 'cvn_hull', T(x, y, z0 - 0.012), bevel=0.006)
        p.add(props.lathe([(0.0, 0.018), (0.02, 0.018), (0.034, 0.0)], seg=12), 'cvn_dome', T(x, y, zt), smooth=True)
    # the island: four decks stepping up, the bridge's windows all round, a civ band, the hull number
    ix, iy = g['island']
    z = zt
    levels = ((0.12, 0.52, 0.115, 0.0), (0.11, 0.42, 0.095, 0.025), (0.125, 0.25, 0.07, 0.085), (0.1, 0.19, 0.045, 0.06))
    zs = []
    for (w, l, h, dy) in levels:
        p.add(props.box(w, l, h), 'cvn_island', T(ix, iy + dy, z + h / 2), bevel=0.007)
        zs.append(z)
        z += h
    p.add(props.box(0.127, 0.252, 0.022), 'cvn_window', T(ix, iy + 0.085, zs[2] + 0.042))   # the bridge's windows all round
    p.add(props.box(0.112, 0.422, 0.034), 'tint', T(ix, iy + 0.025, zs[1] + 0.05), tint=True)   # the civ band
    _cvn_digits_side(p, g['number'], ix + 0.061, iy - 0.1, zt + 0.058, 0.075)
    # the mast (a tapered pylon), its yard, the planar search radar, the big air-search array, a radome
    zm = z
    p.add(props.lathe([(0.0, 0.042), (0.26, 0.016)], seg=4), 'cvn_island', T(ix, iy + 0.05, zm) @ R('Z', 45))
    p.add(props.box(0.18, 0.024, 0.024), 'cvn_island', T(ix, iy + 0.05, zm + 0.17), bevel=0.005)
    p.add(props.box(0.08, 0.06, 0.02), 'cvn_island', T(ix, iy + 0.05, zm + 0.1), bevel=0.004)   # a radar platform
    p.add(props.box(0.1, 0.018, 0.1), 'cvn_hull_dark', T(ix, iy + 0.11, zm + 0.05) @ R('X', -12), bevel=0.005)   # SPS-48, seated on the top deck
    p.add(props.box(0.024, 0.024, 0.1), 'cvn_island', T(ix, iy - 0.02, zm + 0.05))   # the air-search array's post
    p.add(props.box(0.15, 0.018, 0.065), 'cvn_hull_dark', T(ix, iy - 0.02, zm + 0.11), bevel=0.005)
    p.add(props.ellipsoid(0.028, 0.028, 0.028, seg=12), 'cvn_dome', T(ix - 0.03, iy + 0.1, zm + 0.026), smooth=True)
    # the air wing, parked round the edges (tails over the side), one on the bow catapult, a Hawkeye
    s_ = g['jet_scale']
    za = zt
    park = ([(0.19, y, 62) for y in (1.36, 1.1, 0.84, 0.58)] + [(0.19, y, 62) for y in (-0.56, -0.82, -1.08, -1.34)]
            + [(-0.06, y, -62) for y in (1.2, 0.94)] + [(0.12, -1.56, 90)])
    for x, y, yaw in park:
        _cvn_jet(p, x, y, za, yaw, s_, civ=g['jets_civ'])
    _cvn_hawkeye(p, -0.02, 0.5, za, -70, s_)
    # the sea at the waterline: a wash along the sides and a bow wave
    for sx in (-1, 1):
        path = [(sx * (hull.side_x(s1, 0.004) + 0.006), hull.y(s1), 0.004) for s1 in [-0.96 + 1.9 * i / 50 for i in range(51)]]
        p.add(props.tube(path, 0.008, seg=5, scale_xy=(1.0, 0.5)), 'cvn_wet', smooth=True)
        p.add(props.tube(path[-12:], [0.004 + 0.006 * i / 11 for i in range(12)], seg=5, scale_xy=(1.0, 0.5)), 'cvn_foam', smooth=True)
    p.finish()
    return root


def _cvn_digits_side(p, digits, x, y0, z0, h):
    """Seven-segment figures painted on a starboard-facing wall (x), reading toward the bow."""
    segs = {'0': 'abcdef', '1': 'bc', '2': 'abged', '3': 'abgcd', '4': 'fgbc', '5': 'afgcd', '6': 'afgedc', '7': 'abc',
            '8': 'abcdefg', '9': 'abcfgd'}
    w, k = h * 0.55, h * 0.16
    for i, d in enumerate(digits):
        yc = y0 - (i - (len(digits) - 1) / 2) * w * 1.5
        for sg in segs[d]:
            dy, dz, ly, lz = {'a': (0, h / 2, w, k), 'g': (0, 0, w, k), 'd': (0, -h / 2, w, k),
                              'f': (w / 2, h / 4, k, h / 2), 'b': (-w / 2, h / 4, k, h / 2),
                              'e': (w / 2, -h / 4, k, h / 2), 'c': (-w / 2, -h / 4, k, h / 2)}[sg]
            p.add(props.box(0.003, ly, lz), 'cvn_line', T(x, yc + dy, z0 + dz))


# ------------------------------------------------------------ submarine (realism pass: a Gato-class fleet boat, 1943)

# A Gato-class fleet submarine of 1943 (notes/realism/Submarine.md): 95 x 8.3 m (L:B ~11.5), riding low:
# the saddle tanks' rounded tops at the waterline, a narrow flat deck casing with rows of limber holes,
# a raked bow, the cut-down fairwater with its bridge, periscope shears and radar mast, gun platforms
# fore and aft of it, the 5-inch deck gun forward. L 2.2 = 95 m (1 unit = 43 m).
SS = dict(
    L=2.2, B=0.22, deck=0.05, bow_rise=0.06, casing=0.08, rake=0.9, stern_drop=0.03,
    tower=(0.13, 0.031, 0.14), tower_y=0.1, gun_y=0.52, civ_stripe=False, civ_panels=((-0.58, 0.22), (0.76, 0.2)),
    tower_band=(0.36, 0.88), deck_stripe=0.0, bridge_civ=True,
    fit_scale=1.135,
)

SS_COLORS = {
    'ss_hull': '#2c3032', 'ss_deck': '#42464a', 'ss_dark': '#222527', 'ss_hole': '#101214', 'ss_foam': '#b8c0bc',
    'ss_tower': '#555a5e', 'ss_tower_dark': '#3e4246', 'ss_gun': '#5e6366', 'ss_wet': '#141618',
}
for _k, _v in (('ss_hull', ('#2c3032', 0.55, 0.3, 'metal')), ('ss_deck', ('#42464a', 0.8, 0.1, 'metal')),
               ('ss_dark', ('#222527', 0.5, 0.5, 'metal')), ('ss_hole', ('#101214', 0.8, 0.0, None)),
               ('ss_foam', ('#b8c0bc', 0.8, 0.0, None)), ('ss_tower', ('#555a5e', 0.55, 0.4, 'metal')),
               ('ss_tower_dark', ('#3e4246', 0.55, 0.4, 'metal')), ('ss_gun', ('#5e6366', 0.45, 0.6, 'metal')),
               ('ss_wet', ('#141618', 0.3, 0.3, 'metal'))):
    props.DEFAULTS.setdefault(_k, _v)
SS_WEAR = {'ss_hull': {'grime': 0.45, 'rust': 0.15}, 'ss_deck': {'grime': 0.5}, 'ss_dark': {'grime': 0.4},
           'ss_tower': {'grime': 0.4, 'rust': 0.12}, 'ss_tower_dark': {'grime': 0.4, 'rust': 0.1}}


def _ss_section(g, s):
    """(half-beam, deck height, casing half-width) at s (-1 stern .. +1 bow)."""
    p = 2.4 if s > 0 else 1.15   # a long tapering stern
    b = max(0.003, g['B'] / 2 * max(0.0, 1 - abs(s) ** p) ** 0.5)
    zd = g['deck'] + g['bow_rise'] * max(0.0, (s - 0.55) / 0.45) ** 2 - g['stern_drop'] * max(0.0, (-s - 0.5) / 0.5) ** 1.5
    dw = min(g['casing'] / 2, b * 0.8)
    if s > 0.7:   # the Gato's flared bow: the casing widens into the bow's shoulders before the knife stem
        dw = min(dw * (1 + 0.5 * (s - 0.7) / 0.3), b * 0.95)
    return b, zd, dw


def submarine_gato(colors, **kw):
    """The realism pass's Submarine: a Gato-class fleet boat of 1943: a long,
    low dark hull, the saddle tanks' tops at the waterline, a flat deck casing
    with limber holes, a raked bow, the tall slab-sided grey conning tower with
    a civ band, its bridge, periscope shears and radar mast, railed gun
    platforms fore and aft, the shielded 4-inch deck gun forward, a civ stripe
    along the casing and a civ recognition panel aft."""
    g = dict(SS, **kw)
    c = dict(SHIP_COLORS, **SS_COLORS)
    c.update(colors)
    root = vkit.root('Submarine')
    root['fit_scale'] = g['fit_scale']
    p = vkit.part(root, 'hull', c, SS_WEAR)
    L = g['L']
    rings = []
    for i in range(61):
        s = -1 + 2 * i / 60
        b, zd, dw = _ss_section(g, s)
        keel = -0.1 * (1 - 0.6 * abs(s) ** 3)
        half = [(dw, zd), (dw, zd - 0.014), (b * 0.86, zd * 0.32), (b, 0.0), (b * 0.95, keel * 0.4), (b * 0.6, keel * 0.85),
                (0.0, keel)]
        rk = g['rake'] * max(0.0, (s - 0.75) / 0.25) ** 2

        def pt(x, z):
            return (x, s * L / 2 + rk * (z + 0.1), z)
        rings.append([pt(x, z) for x, z in half] + [pt(-x, z) for x, z in reversed(half[:-1])])
    bm = props.loft(rings)
    p.add(bm, 'ss_hull', smooth=False,
          keys=lambda f: ('ss_deck', False) if f.normal.z > 0.8 and f.calc_center_median().z > 0.03 else None)
    # rows of limber holes along the casing's sides
    for i in range(52):
        y = -0.88 + 1.7 * i / 51
        s = 2 * y / L
        b, zd, dw = _ss_section(g, s)
        if dw < g['casing'] / 2 - 0.004:
            continue
        for sx in (-1, 1):
            p.add(props.box(0.007, 0.02, 0.006), 'ss_hole', T(sx * (dw + 0.002), y, zd - 0.008))
    # a civ stripe along the casing's sides, a civ recognition panel on the after deck
    if g['civ_stripe']:
        for sx in (-1, 1):
            for i in range(40):
                y0_, y1_ = -0.8 + 1.6 * i / 40, -0.8 + 1.6 * (i + 1) / 40
                b0, zd0, dw0 = _ss_section(g, 2 * (y0_ + y1_) / 2 / L)
                p.add(props.box(0.003, y1_ - y0_ + 0.001, 0.0135), 'tint', T(sx * (dw0 + 0.0015), (y0_ + y1_) / 2, zd0 - 0.0072), tint=True)
    if g['deck_stripe']:   # a narrow civ stripe down the casing's centreline (clear of the tower and the gun)
        for i in range(48):
            y0_, y1_ = -0.86 + 1.72 * i / 48, -0.86 + 1.72 * (i + 1) / 48
            yc_ = (y0_ + y1_) / 2
            if g['tower_y'] - 0.2 < yc_ < g['tower_y'] + 0.16 or abs(yc_ - g['gun_y']) < 0.035:
                continue
            zd0 = _ss_section(g, 2 * yc_ / L)[1]
            p.add(props.box(g['deck_stripe'], y1_ - y0_ + 0.001, 0.003), 'tint', T(0, yc_, zd0 + 0.0015), tint=True)
    for py, pl in g['civ_panels']:
        zpn = _ss_section(g, 2 * py / L)[1]
        p.add(props.box(g['casing'] * 0.86, pl, 0.004), 'tint', T(0, py, zpn + 0.002), tint=True)
    # the conning tower (a late-war Gato's cut-down fairwater): tall, narrow, slab-sided, rounded at the fore end,
    # stepped down aft to the after gun platform and forward to the forward one; grey, a civ band round its top
    ty = g['tower_y']
    zd = g['deck']
    tl, hw, th = g['tower']

    def stadium(hw_, y_front, y_back, n=8):
        pts = [(hw_ * math.cos(math.pi / 2 - math.pi / 2 * k / n), y_front - hw_ + hw_ * math.sin(math.pi / 2 - math.pi / 2 * k / n)) for k in range(n + 1)]
        pts += [(hw_ * 0.8, y_back), (-hw_ * 0.8, y_back)]
        pts += [(-x, y) for x, y in reversed(pts[1:n + 1])]
        return pts

    tower = stadium(hw, ty + tl / 2, ty - tl / 2)
    p.add(_ss_plate(tower, zd - 0.004, zd + th), 'ss_tower', keys=lambda f: ('ss_tower_dark', False) if f.normal.z > 0.5 else None)
    p.add(_ss_plate([(x * 1.04, ty + (y - ty) * 1.01) for x, y in tower], zd + th * g['tower_band'][0], zd + th * g['tower_band'][1]),
          'tint', tint=True)   # the civ band
    p.add(_ss_plate([(x * 1.0, y) for x, y in stadium(hw * 1.04, ty + tl / 2 + 0.006, ty + tl / 2 - 0.07)], zd + th - 0.01, zd + th + 0.026),
          'ss_tower', keys=lambda f: ('ss_tower_dark', False) if f.normal.z > 0.5 else
          (('tint', True) if g['bridge_civ'] and f.normal.z > -0.5 else None))   # the bridge's raised screen forward, its sides civ
    p.add(props.box(hw * 1.7, 0.006, 0.008), 'ss_wet', T(0, ty + tl / 2 - 0.006, zd + th + 0.02))   # its windows
    # the gun platforms fore and aft: railed tubs (an oval deck on a trunk, a low bulwark, a rail on posts above it),
    # each with a big mount: a twin 40 mm aft, a shielded 20 mm forward
    for yc, ln, hp, gun in ((ty - tl / 2 - 0.05, 0.12, 0.075, '40'), (ty + tl / 2 + 0.035, 0.085, 0.07, '20')):
        pw = hw * 1.35
        p.add(props.box(hw * 1.2, ln * 0.9, hp), 'ss_tower', T(0, yc, zd + hp / 2 - 0.004), bevel=0.004)   # the trunk
        p.add(props.lathe([(0.0, ln / 2), (0.008, ln / 2)], seg=20, scale_xy=(1.0, pw / (ln / 2))), 'ss_tower_dark', T(0, yc, zd + hp - 0.004), smooth=True)
        p.add(props.lathe([(0.0, ln / 2), (0.012, ln / 2 * 0.99)], seg=20, scale_xy=(1.0, pw / (ln / 2))), 'ss_tower', T(0, yc, zd + hp), smooth=True)   # bulwark
        ring = [(pw * 0.97 * math.sin(2 * math.pi * k / 16), yc + ln / 2 * 0.97 * math.cos(2 * math.pi * k / 16), zd + hp + 0.03) for k in range(17)]
        p.add(props.tube(ring, 0.0055, seg=5), 'ss_tower', smooth=True)
        for k in range(0, 16, 2):
            x_, y_, _ = ring[k]
            vkit.rod(p, (x_, y_, zd + hp + 0.01), (x_, y_, zd + hp + 0.031), 0.0055, 'ss_tower', seg=5)
        zg_ = zd + hp + 0.004
        if gun == '40':
            p.add(props.lathe([(0.0, 0.02), (0.014, 0.018)], seg=10), 'ss_gun', T(0, yc, zg_), smooth=True)
            p.add(props.box(0.05, 0.05, 0.03), 'ss_gun', T(0, yc, zg_ + 0.028), bevel=0.006)
            for gx in (-0.012, 0.012):
                vkit.rod(p, (gx, yc - 0.015, zg_ + 0.034), (gx, yc - 0.1, zg_ + 0.05), 0.0075, 'ss_gun', seg=6)
        else:
            p.add(props.lathe([(0.0, 0.01), (0.026, 0.008)], seg=8), 'ss_gun', T(0, yc, zg_), smooth=True)
            p.add(props.box(0.04, 0.008, 0.03), 'ss_gun', T(0, yc + 0.012, zg_ + 0.032), bevel=0.002)
            vkit.rod(p, (0, yc + 0.005, zg_ + 0.03), (0, yc + 0.07, zg_ + 0.038), 0.0075, 'ss_gun', seg=6)
    # the periscope shears (a tall frame), the two periscopes standing above it, the SJ radar mast with its dish
    zt = zd + th
    ys = ty + tl / 2 - 0.08
    p.add(props.box(0.022, 0.075, 0.15), 'ss_dark', T(0, ys, zt + 0.075), bevel=0.004)
    p.add(props.box(0.026, 0.08, 0.014), 'ss_dark', T(0, ys, zt + 0.15), bevel=0.003)
    for dy, h in ((0.022, 0.235), (-0.016, 0.21)):
        vkit.rod(p, (0, ys + dy, zt + 0.14), (0, ys + dy, zt + h), 0.0095, 'ss_dark', seg=8)
    vkit.rod(p, (0, ys - 0.065, zt), (0, ys - 0.065, zt + 0.16), 0.009, 'ss_dark', seg=8)
    p.add(props.box(0.05, 0.014, 0.026), 'ss_dark', T(0, ys - 0.065, zt + 0.165), bevel=0.003)   # the SJ radar's dish
    # the 4-inch deck gun forward: pedestal, carriage, a sloped shield, the long barrel trained ahead, the breech
    gy = g['gun_y']
    zg = _ss_section(g, 2 * gy / L)[1]
    p.add(props.lathe([(0.0, 0.022), (0.024, 0.018)], seg=12), 'ss_gun', T(0, gy, zg), smooth=True)
    p.add(props.box(0.04, 0.06, 0.026), 'ss_gun', T(0, gy - 0.005, zg + 0.034), bevel=0.006)
    p.add(props.box(0.095, 0.012, 0.068), 'ss_gun', T(0, gy + 0.03, zg + 0.058) @ R('X', -14), bevel=0.004)   # the shield
    p.add(props.box(0.024, 0.04, 0.022), 'ss_tower_dark', T(0, gy - 0.025, zg + 0.05), bevel=0.004)          # the breech
    vkit.rod(p, (0, gy + 0.02, zg + 0.055), (0, gy + 0.21, zg + 0.07), 0.016, 'ss_gun', seg=10, r1=0.0135)
    # the waterline: a dark wet band along the sides, a little foam at the bow and in the wake
    for sx in (-1, 1):
        path = []
        for i in range(51):
            s_ = -0.96 + 1.92 * i / 50
            b_, zd_, dw_ = _ss_section(g, s_)
            path.append((sx * (b_ + 0.003), s_ * L / 2, 0.003))
        p.add(props.tube(path, 0.007, seg=5, scale_xy=(1.0, 0.5)), 'ss_wet', smooth=True)
        p.add(props.tube(path[-10:], [0.003 + 0.006 * i / 9 for i in range(10)], seg=5, scale_xy=(1.0, 0.5)), 'ss_foam', smooth=True)
        p.add(props.tube(path[:6], [0.007 - 0.004 * i / 5 for i in range(6)], seg=5, scale_xy=(1.0, 0.5)), 'ss_foam', smooth=True)
    p.finish()
    return root


def _ss_plate(outline, z0, z1):
    """A flat plate: a closed (x, y) outline extruded from z0 to z1."""
    bm = props.bmesh.new()
    top = [bm.verts.new((x, y, z1)) for x, y in outline]
    bot = [bm.verts.new((x, y, z0)) for x, y in outline]
    bm.faces.new(top)
    bm.faces.new(list(reversed(bot)))
    n = len(outline)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((bot[i], bot[j], top[j], top[i]))
    props.bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


# ------------------------------------------------------------ nuclear submarine (realism pass: a Los Angeles-class SSN)

# A Los Angeles-class attack submarine of the 1980s (notes/realism/Nuclear Submarine.md): 110 x 10 m
# (L:B 11), surfaced: a long black cylinder a third out of the water with a flat walkway along its
# top, a rounded bow, the tall slim sail well forward with its fairwater planes, the stern tapering
# under so only the upper rudder fin and the planes' tips break the wash. L 2.6 = 110 m (1 unit = 42 m).
SSN = dict(
    L=2.6, R=0.12, zc=-0.056, sail_y=0.5, sail=(0.12, 0.024, 0.29), band=(0.44, 0.9), fin_y=-1.12, fin_civ=True, walk_civ=None, plane_civ=True,
    fit_scale=1.1,
)

SSN_COLORS = {
    'ssn_hull': '#1c1e20', 'ssn_deck': '#2c2f32', 'ssn_dark': '#141618', 'ssn_foam': '#b8c0bc', 'ssn_wet': '#0e1012',
    'ssn_wash': '#6c787a', 'ssn_hull_alt': '#232629',
}
for _k, _v in (('ssn_hull', ('#1c1e20', 0.36, 0.0, 'metal')), ('ssn_deck', ('#2c2f32', 0.7, 0.0, 'metal')),
               ('ssn_dark', ('#141618', 0.5, 0.3, 'metal')), ('ssn_foam', ('#b8c0bc', 0.8, 0.0, None)),
               ('ssn_wet', ('#0e1012', 0.25, 0.0, None)), ('ssn_wash', ('#6c787a', 0.8, 0.0, None)),
               ('ssn_hull_alt', ('#232629', 0.42, 0.0, 'metal'))):
    props.DEFAULTS.setdefault(_k, _v)
SSN_WEAR = {'ssn_hull': {'grime': 0.35}, 'ssn_deck': {'grime': 0.45}}


def _ssn_section(g, s):
    """(radius, centre height) at s (-1 stern .. +1 bow); the stern rides up so its top stays awash."""
    R_ = g['R']
    if s > 0.72:
        r = R_ * max(0.0, 1 - ((s - 0.72) / 0.28) ** 2) ** 0.5
    elif s < -0.2:   # the stern cone: a long visible taper
        r = R_ * max(0.02, 1 - (-s - 0.2) / 0.8) ** 0.75
    else:
        r = R_
    zc = g['zc'] + 0.05 * max(0.0, (-s - 0.2) / 0.8) ** 1.3
    zc -= 0.04 * max(0.0, (-s - 0.9) / 0.1)   # the tail cone past the planes dives under the wash
    return max(r, 0.003), zc


def _ssn_foil(hw, ln, n=12):
    """A streamlined plan (x, y) of length ln, half-width hw, blunt forward, sharp aft; front at y = +ln/2."""
    half = []
    for k in range(n + 1):
        t = (k / n) ** 1.7   # samples crowd the leading edge, so it rounds
        # a round nose (a half-ellipse over the first 18 %), then a long taper to a sharp trailing edge
        w = math.sqrt(max(0.0, 1 - (1 - t / 0.18) ** 2)) if t < 0.18 else max(0.0, 1 - (t - 0.18) / 0.82) ** 0.9
        half.append((hw * w, ln / 2 - ln * t))
    return half + [(-x, y) for x, y in reversed(half[1:-1])]


def nuclear_sub_la(colors, **kw):
    """The realism pass's Nuclear Submarine: a Los Angeles-class SSN surfaced:
    a long black anechoic cylinder with a flat walkway along its top, a rounded
    bow, the tall slim black sail forward with a civ band near its top, its
    thin fairwater planes and masts, the stern tapering under the wash with
    only the upper rudder fin showing (its top civ), a dark wet waterline."""
    g = dict(SSN, **kw)
    c = dict(SHIP_COLORS, **SSN_COLORS)
    c.update(colors)
    root = vkit.root('Nuclear Submarine')
    root['fit_scale'] = g['fit_scale']
    p = vkit.part(root, 'hull', c, SSN_WEAR)
    L = g['L']
    rings = []
    for i in range(73):
        s = -1 + 2 * i / 72
        r, zc = _ssn_section(g, s)
        y = s * L / 2
        rings.append([(r * math.sin(2 * math.pi * k / 24), y, zc + r * math.cos(2 * math.pi * k / 24)) for k in range(24)])
    bm = props.loft(rings)

    def hkeys(f):
        if f.normal.z > 0.97 and f.calc_center_median().z > 0.0:
            y0_, y1_ = g['walk_civ'] or (9, 9)
            if y0_ < f.calc_center_median().y < y1_:
                return ('tint', True)   # a civ stripe along the walkway
            return ('ssn_deck', False)   # the flat walkway along the top
        if int(math.floor((f.calc_center_median().y + 0.07) / 0.26)) % 2:
            return ('ssn_hull_alt', False)   # the hull's tile courses (panel lines between them)
        return None

    p.add(bm, 'ssn_hull', smooth=True, keys=hkeys)
    # the sail: a tall, slim streamlined fin with a rounded leading edge, black, a civ band near its top,
    # the thin fairwater planes standing out from it, two masts
    sy = g['sail_y']
    ln, hw, h = g['sail'][0] * 2, g['sail'][1], g['sail'][2]
    r, zc = _ssn_section(g, 2 * sy / L)
    zb = zc + r - 0.012
    plan = _ssn_foil(hw, ln, n=16)
    p.add(props.loft([[(x, sy + y, zb) for x, y in plan], [(x * 0.92, sy + y * 0.95, zb + h) for x, y in plan]]), 'ssn_hull')
    b0, b1 = g['band']
    p.add(props.loft([[(x * (1.0 - 0.08 * f) * 1.06, sy + y * (1.0 - 0.05 * f) * 1.02, zb + h * f) for x, y in plan]
                      for f in (b0, b1)]), 'tint', tint=True)
    zp = zb + h * 0.55
    planes = [(-0.18, sy + 0.035), (-0.016, sy + 0.07), (0.016, sy + 0.07), (0.18, sy + 0.035), (0.18, sy + 0.0), (-0.18, sy + 0.0)]
    p.add(_ssn_plate(planes, zp - 0.006, zp + 0.006), 'ssn_hull', bevel=0.002,
          keys=lambda f: ('tint', True) if g['plane_civ'] and f.normal.z > 0.9 else None)   # the planes' tops painted civ
    for dy, hh in ((0.03, 0.045), (-0.01, 0.06)):
        vkit.rod(p, (0, sy + dy, zb + h - 0.005), (0, sy + dy, zb + h + hh), 0.0095, 'ssn_dark', seg=8)
    for hy in (1.0, 0.82, 0.25, -0.05, -0.35):   # hatches along the walkway (a hatch line)
        r_, zc_ = _ssn_section(g, 2 * hy / L)
        p.add(props.box(0.03, 0.045, 0.004), 'ssn_dark', T(0, hy, zc_ + r_ + 0.0005), bevel=0.0015)
    # the stern: only the upper rudder fin stands out of the wash (its top in the civ colour)
    fy = g['fin_y']
    fin = [(fy + 0.06, -0.03), (fy - 0.05, -0.03), (fy - 0.085, 0.09), (fy - 0.04, 0.09)]   # swept back
    p.add(props.slab(fin, 0.016), 'ssn_hull', bevel=0.003)
    if g['fin_civ']:   # its tip
        p.add(props.slab([(fy - 0.0715, 0.066), (fy - 0.0255, 0.066), (fy - 0.039, 0.0905), (fy - 0.0855, 0.0905)], 0.019), 'tint', tint=True)
    # the sea: a dark wet line along the waterline, a little foam at the bow, a wash over the awash stern
    for sx in (-1, 1):
        path = []
        for i in range(61):
            s_ = -0.9 + 1.88 * i / 60
            r, zc = _ssn_section(g, s_)
            if r > abs(zc) + 0.002:
                path.append((sx * (math.sqrt(r * r - zc * zc) + 0.004), s_ * L / 2, 0.003))
        p.add(props.tube(path, 0.007, seg=5, scale_xy=(1.0, 0.5)), 'ssn_wet', smooth=True)
        p.add(props.tube(path[-6:], [0.003 + 0.004 * i / 5 for i in range(6)], seg=5, scale_xy=(1.0, 0.5)), 'ssn_wash', smooth=True)
        x0, y0, _ = path[0]
        p.add(props.tube([(x0, y0, 0.003), (sx * 0.03, fy + 0.02, 0.003), (sx * 0.05, fy - 0.09, 0.003)], [0.008, 0.01, 0.009], seg=5,
                         scale_xy=(1.0, 0.5)), 'ssn_wash', smooth=True)
    p.finish()
    return root


# ------------------------------------------------------------ numpy-naval helpers (the NumPy remakes' own, prefix _nn_)

# Helpers for the three NumPy-made ships remade in units3d (notes/realism/_numpy-naval.md): the Japanese
# Warship (an atakebune), the Pirate (a steam junk) and the Troop Ship (a WWII attack transport). Own names
# only; no shared helper or default is changed.
for _k, _v in (('nn_foam', ('#c4cac4', 0.8, 0.0, None)), ('nn_black', ('#121214', 0.6, 0.0, None)),
               ('nn_dark', ('#2a2622', 0.7, 0.0, 'wood'))):
    props.DEFAULTS.setdefault(_k, _v)


def _nn_hull(p, hull, keys, n=44, m=28, key='wood'):
    """The hull shell lofted from its sections, faces keyed by keys(face)."""
    ss = [-1 + 2 * i / n for i in range(n + 1)]
    p.add(props.loft([hull.section(s, m=m) for s in ss]), key, smooth=False, keys=keys)


def _nn_foam(p, hull, r=0.009, z=0.004, s0=-0.97, s1=0.97, bow_wave=0.0):
    """A waterline foam line along both sides (and a bow wave rising at the stem)."""
    for sx in (-1, 1):
        path = []
        for i in range(51):
            s_ = s0 + (s1 - s0) * i / 50
            zz = z + bow_wave * max(0.0, (s_ - 0.7) / 0.3) ** 2
            path.append((sx * (hull.side_x(s_, zz) + r * 0.6), hull.y(s_), zz))
        p.add(props.tube(path, r, seg=5, scale_xy=(1.0, 0.5)), 'nn_foam', smooth=True)


def _nn_line(p, hull, z_of_s, r, key, s0=-0.95, s1=0.95, out=0.003, flat=0.45, n=50, tint=False):
    """A wale or painted line along both sides at height z_of_s(s)."""
    for sx in (-1, 1):
        path = []
        for i in range(n + 1):
            s_ = s0 + (s1 - s0) * i / n
            z = z_of_s(s_)
            path.append((sx * (hull.side_x(s_, z) + out), hull.y(s_), z))
        p.add(props.tube(path, r, seg=6, scale_xy=(1.0, flat)), key, smooth=True, tint=tint)


def _nn_flag(p, top, w, h, key='tint', tint=True, keys=None, droop=0.6, wave=0.03, nu=10, nv=6):
    """A flag hanging from `top` and streaming aft (-Y), its face turned out at `droop` of 45 deg."""
    bm = props.bmesh.new()
    rows = []
    for jj in range(nu + 1):
        t = jj / nu
        rows.append([bm.verts.new((top[0] - droop * w * t * 0.7 + wave * math.sin(4 * t), top[1] - w * t * (1 - 0.3 * droop),
                                   top[2] - h * ii / nv)) for ii in range(nv + 1)])
    for jj in range(nu):
        for ii in range(nv):
            bm.faces.new((rows[jj][ii], rows[jj + 1][ii], rows[jj + 1][ii + 1], rows[jj][ii + 1]))
    p.add(bm, key, smooth=True, tint=tint, keys=keys)


# ------------------------------------------------------------ japanese warship (realism pass: a Sengoku atakebune, c. 1578)

# An atakebune (notes/realism/Japanese Warship.md): the Sengoku castle-ship (Oda Nobunaga's iron-plated
# ships at Kizugawaguchi, 1578; the Tokugawa Atake Maru): a broad wasen hull ~33 x 10 m with a raked stem
# and a square transom, the whole deck boxed in by armoured plank walls (tate-ita) pierced by two rows of
# loopholes (sama) for arquebuses and bows, a two-storey tower (yagura) of white plaster and black timber
# under grey tiled roofs with upturned eaves and gold shachi, one mast with a battened mat sail and the
# mon, a bank of sculling oars (ro) under the walls, nobori banners at the corners.
# L 2.4 = 33 m (1 unit = 14 m).
ATAKE = dict(
    L=2.4, B=0.66, D=0.12, draft=0.08, sheer_stern=0.07, sheer_bow=0.12,
    wall_h=0.2, wall_y=(-1.12, 0.98), wall_out=0.03,
    tower=(-0.3, 0.8, 0.5, 0.17), tower2=(0.52, 0.36, 0.14), tower3=(0.3, 0.22, 0.11),   # storey 1: (y, length, width, height); storey 2: (length, width, height)
    oars=13, oar_reach=0.24,
    mast_y=0.42, sail=(0.92, 0.74), mon=0.9, yard_z=1.16, brace=30.0, billow=0.06,
    nobori=((-1.0, 1), (-1.0, -1), (0.84, 1), (0.84, -1), (0.1, 1), (0.1, -1)), nobori_h=0.3,
    wall_maku=None,          # (critic 1: dark lacquer, not civ) a civ curtain along the walls' top, as fractions of the wall
    wall_z0=0.03,            # the box wall's foot: just above the waterline (critic 1: wall from the waterline up)
    slits=(0.72,),           # rows of light loophole slits (fractions of the wall's height)
    maku=False, rig_scale=1.0,
    fit_scale=1.49,   # the lead's size rule: between the stock Galleon and Carrack (not the NumPy sprite, which was Galley-sized)
)

ATAKE_COLORS = {
    'ak_wood': '#5a4128', 'ak_wood_alt': '#523a24', 'ak_iron': '#2a2420', 'ak_iron_alt': '#231e1a', 'ak_rivet': '#6a6c6e',
    'ak_pillar': '#6e5232', 'ak_plaster': '#e6e2d6', 'ak_timber': '#24201c', 'ak_roof': '#3c4046', 'ak_roof_dark': '#2a2d32', 'ak_ridge': '#d6d2c6',
    'ak_eave': '#c8c6be', 'ak_mat': '#d8c89c', 'ak_mat_b': '#bba878', 'ak_batten': '#5a4630', 'ak_deck': '#4e3e2c', 'ak_frame': '#d0b47c',
    'tar': '#2a221a', 'oar': '#3e2c1c', 'gold': '#c4a050',
}
for _k, _v in (('ak_wood', ('#5a4128', 0.62, 0.0, 'wood')), ('ak_wood_alt', ('#523a24', 0.62, 0.0, 'wood')),
               ('ak_iron', ('#2a2420', 0.6, 0.0, 'wood')), ('ak_iron_alt', ('#231e1a', 0.6, 0.0, 'wood')),
               ('ak_rivet', ('#6a6c6e', 0.5, 0.5, 'metal')), ('ak_pillar', ('#6e5232', 0.62, 0.0, 'wood')),
               ('ak_plaster', ('#e6e2d6', 0.85, 0.0, 'stone')), ('ak_timber', ('#24201c', 0.6, 0.0, 'wood')),
               ('ak_roof', ('#3c4046', 0.7, 0.0, 'stone')), ('ak_roof_dark', ('#2a2d32', 0.7, 0.0, 'stone')), ('ak_ridge', ('#d6d2c6', 0.8, 0.0, 'stone')),
               ('ak_eave', ('#c8c6be', 0.75, 0.0, 'stone')), ('ak_mat', ('#d8c89c', 0.9, 0.0, 'cloth')),
               ('ak_mat_b', ('#bba878', 0.9, 0.0, 'cloth')), ('ak_batten', ('#5a4630', 0.7, 0.0, 'wood')),
               ('ak_deck', ('#4e3e2c', 0.7, 0.0, 'wood')), ('ak_frame', ('#d0b47c', 0.62, 0.0, 'wood'))):
    props.DEFAULTS.setdefault(_k, _v)
ATAKE_WEAR = dict(SHIP_WEAR, ak_wood={'grime': 0.45}, ak_wood_alt={'grime': 0.45}, ak_iron={'grime': 0.35, 'rust': 0.12},
                  ak_iron_alt={'grime': 0.35, 'rust': 0.12}, ak_plaster={'grime': 0.3}, ak_mat={'grime': 0.4, 'stain': 0.4},
                  ak_mat_b={'grime': 0.4, 'stain': 0.4}, ak_roof={'grime': 0.3})


def _ak_roof(p, yc, xc, ln, wd, z0, h, up=0.03, eave=0.03, shachi=True):
    """A tiled hip-and-gable (irimoya) roof over a ln x wd storey: eaves overhanging by `eave`, their corners
    turned up by `up`, a dark ridge with end tiles and, if asked, gold shachi on it; a pale eave line."""
    hx, hy = wd / 2 + eave, ln / 2 + eave
    rings = []
    nz = 7
    for i in range(nz + 1):
        u = i / nz
        ax = max(hx * (1 - u), 0.006)
        ay = max(hy - hx * u * 0.9, 0.01)
        z = z0 + h * u ** 1.5
        ring = []
        for k in range(24):   # round a rectangle: 6 points a side
            side, t = divmod(k, 6)
            t = t / 6
            if side == 0:
                x, y = -ax + 2 * ax * t, -ay
            elif side == 1:
                x, y = ax, -ay + 2 * ay * t
            elif side == 2:
                x, y = ax - 2 * ax * t, ay
            else:
                x, y = -ax, ay - 2 * ay * t
            cx, cy = abs(x) / hx, abs(y) / hy
            lift = up * (cx * cy) ** 4 * max(0.0, 1 - u * 2.5)
            ring.append((xc + x, yc + y, z + lift))
        rings.append(ring)
    rings.insert(0, [(x, y, z - 0.012) for x, y, z in rings[0]])
    zr = z0 + h
    p.add(props.loft(rings), 'ak_roof', smooth=False,
          keys=lambda f: ('ak_eave', False) if f.calc_center_median().z < z0 + 0.004 else
          (('ak_roof_dark', False) if int(math.floor((f.calc_center_median().x + f.calc_center_median().y) / 0.03)) % 2 else None))
    ry = hy - hx * 0.9
    p.add(props.box(0.034, 2 * ry + 0.03, 0.026), 'ak_ridge', T(xc, yc, zr + 0.006), bevel=0.005)   # the pale ridge cap
    for s in (-1, 1):
        p.add(props.box(0.044, 0.024, 0.044), 'ak_ridge', T(xc, yc + s * (ry + 0.012), zr + 0.016), bevel=0.006)
        if shachi:   # a gold fish standing on its head at each end of the ridge
            fy = yc + s * (ry + 0.012)
            vkit.rod(p, (xc, fy, zr + 0.03), (xc, fy + s * 0.014, zr + 0.075), 0.013, 'gold', r1=0.008, seg=6)
            vkit.rod(p, (xc, fy + s * 0.014, zr + 0.075), (xc, fy - s * 0.008, zr + 0.09), 0.009, 'gold', r1=0.004, seg=5)
    # the gable boards at the ridge's ends (dark timber triangles)
    for s in (-1, 1):
        yg = yc + s * (ry + 0.004)
        p.add(props.slab([(-hx * 0.42, z0 + h * 0.45), (hx * 0.42, z0 + h * 0.45), (0.0, zr + 0.004)], 0.008, 'Y'),
              'ak_timber', T(xc, yg, 0))


def _ak_storey(p, yc, ln, wd, z0, h, windows=4):
    """A plastered storey: white walls, black corner posts and a black sill and lintel band, dark windows."""
    p.add(props.box(wd, ln, h), 'ak_plaster', T(0, yc, z0 + h / 2), bevel=0.004)
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.add(props.box(0.022, 0.022, h), 'ak_timber', T(sx * wd / 2, yc + sy * ln / 2, z0 + h / 2))
        p.add(props.box(0.012, ln + 0.01, 0.018), 'ak_timber', T(sx * wd / 2, yc, z0 + 0.009))
        p.add(props.box(0.012, ln + 0.01, 0.016), 'ak_timber', T(sx * wd / 2, yc, z0 + h - 0.008))
        for k in range(windows):   # barred windows (dark)
            y = yc - ln / 2 + ln * (k + 0.5) / windows
            p.add(props.box(0.008, ln / windows * 0.5, h * 0.32), 'ak_timber', T(sx * (wd / 2 + 0.002), y, z0 + h * 0.58))
    for sy in (-1, 1):
        p.add(props.box(wd + 0.01, 0.012, 0.018), 'ak_timber', T(0, yc + sy * ln / 2, z0 + 0.009))
        p.add(props.box(wd + 0.01, 0.012, 0.016), 'ak_timber', T(0, yc + sy * ln / 2, z0 + h - 0.008))
        p.add(props.box(wd * 0.5, 0.008, h * 0.32), 'ak_timber', T(0, yc + sy * (ln / 2 + 0.002), z0 + h * 0.58))


def _ak_mat_sail(p, w, h, z_top, billow, nu=30, nv=72, battens=9, strips=9, mon=0.8):
    """The atakebune's sail (local: the mast on z, the yard along x at z_top, belly +Y): straight edges,
    vertical cloth strips in two tones, dark battens across, a civ mon in the middle (a takeda-bishi: a
    lozenge split into four by cloth along its diagonals); the yard and boom dark."""
    def surf(u, v):
        return (u * w / 2, 0.03 + billow * (1 - u * u) * math.sin(math.pi * v), z_top - v * h)

    bm = props.bmesh.new()
    verts = [[bm.verts.new(surf(-1 + 2 * i / nu, j / nv)) for i in range(nu + 1)] for j in range(nv + 1)]
    for j in range(nv):
        for i in range(nu):
            bm.faces.new((verts[j][i], verts[j][i + 1], verts[j + 1][i + 1], verts[j + 1][i]))

    def keys(f):
        c = f.calc_center_median()
        X = c.x / (w / 2) * (w / h)
        Y = (c.z - (z_top - h / 2)) / (h / 2)
        if abs(X) * 0.95 + abs(Y) < mon:
            if abs(X * 0.95 + Y) < 0.07 or abs(X * 0.95 - Y) < 0.07:
                return ('ak_mat', False)
            return ('tint', True)
        v = (z_top - c.z) / h
        bt = v * battens
        if 0.05 < v < 0.97 and bt - math.floor(bt) < 0.14:
            return ('ak_batten', False)
        if int(v * battens * 4) % 2:   # the matting's horizontal ribs
            return ('ak_mat_b', False)
        return None

    p.add(bm, 'ak_mat', smooth=True, keys=keys)
    vkit.rod(p, (-w / 2 - 0.03, 0.03, z_top + 0.012), (w / 2 + 0.03, 0.03, z_top + 0.012), 0.017, 'ak_timber', seg=8)
    vkit.rod(p, (-w / 2 - 0.01, 0.03, z_top - h - 0.006), (w / 2 + 0.01, 0.03, z_top - h - 0.006), 0.012, 'ak_timber', seg=8)


def atakebune_sengoku(colors, **kw):
    """The realism pass's Japanese Warship: a Sengoku atakebune (see ATAKE)."""
    g = dict(ATAKE, **kw)
    c = dict(SHIP_COLORS, **ATAKE_COLORS)
    c.update(colors)
    root = vkit.root('Japanese Warship')
    root['fit_scale'] = g['fit_scale']
    p = vkit.part(root, 'hull', c, ATAKE_WEAR)
    hull = Hull(L=g['L'], B=g['B'], D=g['D'], draft=g['draft'], sheer_stern=g['sheer_stern'], sheer_bow=g['sheer_bow'],
                bow_p=2.2, stern_p=3.0, transom=0.72, rake_bow=0.2, fullness=0.55)
    yb = g['L'] / 2

    def deck(y):
        return hull.deck_z(hull.s_of(y))

    def hkeys(f):
        cc = f.calc_center_median()
        if f.normal.z > 0.85:
            return ('ak_deck', False)
        if cc.z < 0.012:
            return ('hull_grime', False)
        if cc.z < g['wall_z0'] + 0.006:
            return ('tar', False)   # the darker waterline
        if int(math.floor(cc.z / 0.026)) % 2:
            return ('ak_wood_alt', False)
        return None

    _nn_hull(p, hull, hkeys, key='ak_wood')
    _nn_foam(p, hull, bow_wave=0.02)
    _nn_line(p, hull, lambda s: hull.deck_z(s) - 0.03, 0.012, 'ak_timber', s0=-0.97, s1=0.9)   # a dark wale under the walls

    # the fortress walls (tate-ita): iron-plated, standing out over the hull sides, two rows of loopholes,
    # natural-wood pillars, a light cap; closed across the bow and the stern
    wh, wo = g['wall_h'], g['wall_out']
    wy0, wy1 = g['wall_y']
    ys = [wy0 + (wy1 - wy0) * i / 40 for i in range(41)]

    def wx(y):
        return hull.deck_half(hull.s_of(y)) + wo

    def wtop(y):
        return deck(y) - 0.03 + wh

    def wbot(y):
        return g['wall_z0'] if g['wall_z0'] is not None else deck(y) - 0.03

    def wkeys(f):
        cc = f.calc_center_median()
        if f.normal.z > 0.85:
            return ('ak_pillar', False)
        fr = (cc.z - wbot(cc.y)) / max(wtop(cc.y) - wbot(cc.y), 1e-6)
        if g['wall_maku'] and g['wall_maku'][0] < fr < g['wall_maku'][1] and abs(f.normal.x) > 0.5:
            return ('tint', True)
        if 0.34 < fr < 0.4:
            return ('ak_iron_alt', False)   # a plank seam
        return ('ak_iron_alt', False) if int(math.floor((cc.y + 3) / 0.12)) % 2 else None

    for sx in (-1, 1):
        rings = []
        for y in ys:
            xo = sx * wx(y)
            xi = xo - sx * 0.016
            zb_, zt_ = wbot(y), wtop(y)
            outer = [(xo, y, zb_ + (zt_ - zb_) * f) for f in (0.0, 0.2, 0.34, 0.4, 0.55, 0.7, 0.98, 1.0)]   # rows, so the face keys can band it
            rings.append(outer + [(xi, y, zt_), (xi, y, zb_)])
        p.add(props.loft(rings), 'ak_iron', keys=wkeys)
        p.add(props.tube([(sx * (wx(y) - 0.008), y, deck(y) - 0.03 + wh + 0.006) for y in ys], 0.012, seg=6, scale_xy=(1.0, 0.7)),
              'ak_pillar', smooth=True)   # the cap rail
        for k in range(22):   # a row of light loophole slits
            yl = wy0 + 0.08 + (wy1 - wy0 - 0.16) * k / 21
            zb_, zt_ = wbot(yl), wtop(yl)
            for fr in g['slits']:
                p.add(props.box(0.008, 0.022, 0.05), 'ak_frame', T(sx * (wx(yl) + 0.003), yl, zb_ + (zt_ - zb_) * fr))
    for yw in (wy0, wy1):   # the end walls
        xw = wx(yw)
        z0 = deck(yw) - 0.03
        zb_ = wbot(yw)
        p.add(props.box(2 * xw, 0.016, wtop(yw) - zb_), 'ak_iron', T(0, yw, (zb_ + wtop(yw)) / 2))
        p.add(props.box(2 * xw, 0.024, 0.012), 'ak_pillar', T(0, yw, z0 + wh + 0.006))
        for k in range(5):
            xl = -xw * 0.8 + xw * 1.6 * k / 4
            p.add(props.box(0.022, 0.024, 0.05), 'ak_frame', T(xl, yw, zb_ + (wtop(yw) - zb_) * g['slits'][0]))
    # the bow beyond the wall: a short foredeck to the raked stem, a dark stem post
    stem = [(0, yb - 0.02, 0.02), (0, yb + 0.05, deck(yb) * 0.7), (0, yb + 0.1, deck(yb) + 0.05)]
    p.add(props.tube(stem, [0.02, 0.018, 0.016], seg=8), 'ak_timber', smooth=True)

    # the tower (yagura): two plastered storeys under tiled irimoya roofs, a civ maku round the first
    ty, tl, tw, th = g['tower']
    zt = deck(ty) - 0.03 + wh * 0.4
    _ak_storey(p, ty, tl, tw, zt, th + wh * 0.6, windows=5)
    z1 = zt + th + wh * 0.6
    if g['maku']:   # the curtain round the storey's foot, above the walls (civ)
        for sx in (-1, 1):
            p.add(props.box(0.008, tl + 0.016, 0.08), 'tint', T(sx * (tw / 2 + 0.006), ty, deck(ty) - 0.03 + wh + 0.044), tint=True)
        for sy in (-1, 1):
            p.add(props.box(tw + 0.02, 0.008, 0.08), 'tint', T(0, ty + sy * (tl / 2 + 0.006), deck(ty) - 0.03 + wh + 0.044), tint=True)
    _ak_roof(p, ty, 0.0, tl, tw, z1, 0.09, up=0.03, eave=0.04, shachi=False)
    l2, w2, h2 = g['tower2']
    z2 = z1 + 0.05
    _ak_storey(p, ty, l2, w2, z2, h2, windows=3)
    _ak_roof(p, ty, 0.0, l2, w2, z2 + h2, 0.07, up=0.03, eave=0.035, shachi=False)
    l3, w3, h3 = g['tower3']
    z3 = z2 + h2 + 0.04
    _ak_storey(p, ty, l3, w3, z3, h3, windows=2)
    _ak_roof(p, ty, 0.0, l3, w3, z3 + h3, 0.06, up=0.025, eave=0.03, shachi=True)

    # the sculling oars (ro) under the walls, swept aft into the water
    for i in range(g['oars']):
        y = wy0 + 0.22 + (wy1 - wy0 - 0.4) * i / (g['oars'] - 1)
        s = hull.s_of(y)
        for sx in (-1, 1):
            z = wbot(y) + 0.03
            a = (sx * (wx(y) - 0.004), y, z)
            e = (sx * (wx(y) + g['oar_reach'] * 0.8), y - 0.1, 0.004)
            vkit.rod(p, a, e, 0.019, 'oar', seg=6)
            d = (Vector(e) - Vector(a)).normalized()
            m, _ = vkit.align(Vector(e) - d * 0.06, Vector(e) + d * 0.015)
            p.add(props.box(0.042, 0.016, 0.075), 'oar', m, bevel=0.003)
    # the rudder at the transom
    p.add(props.box(0.02, 0.12, 0.16), 'ak_timber', T(0, -yb - 0.04, 0.04), bevel=0.004)

    # nobori: tall civ banners on poles with a top crossbar at the walls' corners
    for (yn, sx) in g['nobori']:
        x = sx * (wx(yn) - 0.02)
        z0 = deck(yn) - 0.03 + wh
        nh = g['nobori_h']
        vkit.rod(p, (x, yn, z0 - 0.04), (x, yn, z0 + nh + 0.03), 0.011, 'ak_timber', seg=6)
        vkit.rod(p, (x, yn, z0 + nh + 0.02), (x, yn - 0.07, z0 + nh + 0.02), 0.007, 'ak_timber', seg=5)
        p.add(props.box(0.006, 0.08, nh * 0.76), 'tint', T(x, yn - 0.045, z0 + nh * 0.62), tint=True)

    # the mast
    my = g['mast_y']
    k = g['rig_scale']
    yz = deck(my) + k * (g['yard_z'] - deck(my))
    vkit.rod(p, (0, my, deck(my) - 0.02), (0, my, yz + 0.06), 0.024, 'ak_timber', r1=0.017, seg=10)
    for sx in (-1, 1):   # two shrouds to the walls
        vkit.rod(p, (0, my, yz - 0.06), (sx * (wx(my) - 0.02), my - 0.05, deck(my) - 0.03 + wh), 0.012, 'ak_timber', seg=5)
    vkit.rod(p, (0, my, yz - 0.04), (0, yb + 0.08, deck(yb) + 0.04), 0.013, 'ak_timber', seg=5)   # the forestay to the stem
    p.finish()
    r = vkit.part(root, 'rig', c, ATAKE_WEAR)
    sw, sh = g['sail']
    _ak_mat_sail(r, sw * k, sh * k, yz, g['billow'], mon=g['mon'])
    o = r.finish()
    o.location = (0, my, 0)
    o.rotation_euler = (0, 0, math.radians(g['brace']))
    return root


# ------------------------------------------------------------ pirate (realism pass: a South China Sea pirate's steam junk, c. 1850-70)

# A pirate's steam junk (notes/realism/Pirate.md): a Fujian/Guangdong junk ~30 x 8 m (L:B ~3.8): a flat
# raked transom bow with painted eyes, a high overhanging square stern with its gallery and a great rudder,
# tarred planking with ochre wales; fitted with a steam plant: paddle wheels amidships under half-round
# paddle boxes with fanned vents and a tall black funnel between them; cannon run out of a row of ports;
# three masts staggered off the centreline with fan-battened lug sails of tanned matting (straight edges),
# the black flag at the main. L 2.6 = 30 m (1 unit = 11.5 m).
SJUNK = dict(
    L=2.6, B=0.66, D=0.17, draft=0.09, sheer_stern=0.42, sheer_bow=0.12,
    stern=(-1.3, -0.7, 0.18),        # the raised poop: (y0, y1, height)
    paddle=(-0.04, 0.24, 0.22),        # paddle boxes: (y, radius, width)
    funnel=(-0.4, 0.1, 0.82),        # (y, r, height above the deck)
    house=(-0.72, -0.5, 0.09),         # the vaulted deckhouse: (y0, y1, height)
    ports=5,
    masts=(dict(y=0.86, x=0.08, h=0.98, rake=-7.0, sail=(0.62, 0.8), sheet=36.0),
           dict(y=0.4, x=0.0, h=1.42, rake=0.0, sail=(0.86, 1.2), sheet=18.0),
           dict(y=-0.98, x=-0.12, h=0.92, rake=3.0, sail=(0.54, 0.7), sheet=-32.0)),
    bulwark=(-0.62, 1.0, 0.075), guns=4,   # the gun bulwark: (y0, y1, height above the deck), guns a side
    sheet=12.0,                        # the sails sheeted this far off the centreline (a mast's own 'sheet' wins)
    battens=8, flag=(0.44, 0.12), rig_scale=1.0, civ_band=(0.05, 0.14), civ_transom=False, civ_head=False, civ_bow=False,
    civ_strake=True, civ_paddle=True,   # painted civ areas (the red trim, not these, read wrong in critic 2)
    pennant=(0.3, 0.09), transom_w=1.3, gun_r=0.045,
    fit_scale=1.3,   # the lead's size rule (critic 1): about 0.9-1.0 of the stock Privateer
)

SJUNK_COLORS = {
    'sj_tar': '#3a2c20', 'sj_tar_alt': '#33261c', 'sj_ochre': '#a8742e', 'sj_red': '#2a201a', 'sj_deck': '#7a6248',
    'sj_sail': '#b46e36', 'sj_sail_b': '#a2602e', 'sj_batten': '#2e241a', 'sj_funnel': '#1e1e20', 'sj_vent': '#a88250',
    'sj_eye': '#e2dccb', 'flag_black': '#1a1818', 'bone': '#e2dccb', 'gun': '#26282a',
}
for _k, _v in (('sj_tar', ('#3a2c20', 0.65, 0.0, 'wood')), ('sj_tar_alt', ('#33261c', 0.65, 0.0, 'wood')),
               ('sj_ochre', ('#a8742e', 0.6, 0.0, 'wood')), ('sj_red', ('#2a201a', 0.55, 0.0, 'wood')),
               ('sj_deck', ('#7a6248', 0.7, 0.0, 'wood')), ('sj_sail', ('#b46e36', 0.9, 0.0, 'cloth')),
               ('sj_sail_b', ('#a2602e', 0.9, 0.0, 'cloth')), ('sj_batten', ('#2e241a', 0.7, 0.0, 'wood')),
               ('sj_funnel', ('#1e1e20', 0.6, 0.3, 'metal')), ('sj_vent', ('#a88250', 0.6, 0.0, 'wood')),
               ('sj_eye', ('#e2dccb', 0.7, 0.0, None)), ('flag_black', ('#1a1818', 0.9, 0.0, 'cloth')),
               ('bone', ('#e2dccb', 0.8, 0.0, 'cloth')), ('gun', ('#26282a', 0.45, 0.6, 'metal'))):
    props.DEFAULTS.setdefault(_k, _v)
SJUNK_WEAR = dict(SHIP_WEAR, sj_tar={'grime': 0.45}, sj_tar_alt={'grime': 0.45}, sj_sail={'grime': 0.4, 'stain': 0.5},
                  sj_sail_b={'grime': 0.4, 'stain': 0.5}, sj_funnel={'grime': 0.5, 'soot': 0.4}, sj_deck={'grime': 0.5})


def _sj_sail(p, w, h, battens, nu=22, nv=24, civ_head=False):
    """A fan-battened junk lugsail in the local x = 0 plane (fore +Y): straight luff, leech, head and foot,
    about a quarter of it forward of the mast, the head rising aft to its peak; tanned matting in panels
    between dark battens, a little belly to +X."""
    luff_y, leech_y = 0.26 * w, -0.74 * w
    luff_h, leech_h = h * 0.55, h   # fanned: the battens spread toward the leech

    def surf(u, v):   # u 0 luff .. 1 leech, v 0 foot .. 1 head
        y = luff_y + (leech_y - luff_y) * u
        top = luff_h + (leech_h - luff_h) * u
        z = top * v
        return (0.04 * math.sin(math.pi * u) * math.sin(math.pi * min(v, 1.0)), y, z)

    bm = props.bmesh.new()
    verts = [[bm.verts.new(surf(i / nu, j / nv)) for i in range(nu + 1)] for j in range(nv + 1)]
    for j in range(nv):
        for i in range(nu):
            bm.faces.new((verts[j][i], verts[j][i + 1], verts[j + 1][i + 1], verts[j + 1][i]))

    def keys(f):
        cc = f.calc_center_median()
        u = (luff_y - cc.y) / (luff_y - leech_y)
        top = luff_h + (leech_h - luff_h) * u
        v = cc.z / max(top, 1e-6)
        if civ_head and v > 1 - 1 / battens:
            return ('tint', True)   # the head panel painted (civ)
        return ('sj_sail_b', False) if int(v * battens) % 2 else None

    p.add(bm, 'sj_sail', smooth=True, keys=keys)
    for k in range(battens + 1):   # the battens (the head is the yard, the foot the boom)
        v = k / battens
        a, b = surf(0.0, v), surf(1.0, v)
        p.add(props.tube([(a[0] + 0.006, a[1] + 0.02, a[2]), (b[0] + 0.006, b[1] - 0.02, b[2])],
                         0.015 if k in (0, battens) else 0.011, seg=6), 'sj_batten', smooth=True)
    p.add(props.tube([surf(1.0, 0.0), surf(1.0, 1.0)], 0.011, seg=6), 'sj_batten', smooth=True)   # the leech line
    p.add(props.tube([surf(0.0, 0.0), surf(0.0, 1.0)], 0.009, seg=6), 'sj_batten', smooth=True)   # the luff line
    return surf(1.0, 1.0)


def steam_junk_pirate(colors, **kw):
    """The realism pass's Pirate: a South China Sea pirate's steam junk (see SJUNK)."""
    g = dict(SJUNK, **kw)
    c = dict(SHIP_COLORS, **SJUNK_COLORS)
    c.update(colors)
    root = vkit.root('Pirate')
    root['fit_scale'] = g['fit_scale']
    p = vkit.part(root, 'hull', c, SJUNK_WEAR)
    hull = Hull(L=g['L'], B=g['B'], D=g['D'], draft=g['draft'], sheer_stern=g['sheer_stern'], sheer_bow=g['sheer_bow'],
                bow_p=2.6, stern_p=2.4, transom=0.8, bow_transom=0.5, rake_bow=0.18, fullness=0.6)
    yb = g['L'] / 2
    cz0, cz1 = g['civ_band']

    def deck(y):
        return hull.deck_z(hull.s_of(y))

    def hkeys(f):
        cc = f.calc_center_median()
        if f.normal.z > 0.85:
            return ('sj_deck', False) if int(math.floor(cc.x / 0.04)) % 2 else ('sj_deck', False)
        if cc.z < 0.02:
            return ('hull_grime', False)
        if cz0 < cc.z < cz1 and -1.05 < cc.y < 1.1:
            return ('tint', True) if g['civ_strake'] else ('sj_ochre', False)   # a painted strake along the side
        if int(math.floor(cc.z / 0.024)) % 2:
            return ('sj_tar_alt', False)
        return None

    _nn_hull(p, hull, hkeys, key='sj_tar')
    _nn_foam(p, hull, bow_wave=0.02)
    for z in (cz0 - 0.008, cz1 + 0.008):
        _nn_line(p, hull, lambda s, z=z: z, 0.011, 'sj_ochre', s0=-0.85, s1=0.86)   # ochre wales round the civ strake
    _nn_line(p, hull, lambda s: hull.deck_z(s) + 0.012, 0.014, 'sj_ochre', s0=-0.97, s1=0.97, out=-0.004, flat=1.0)   # the rail
    # the flat transom bow board, ochre-edged, and the painted eyes beside it
    zb = deck(yb)
    bw = hull.deck_half(hull.s_of(yb * 0.97))
    p.add(props.box(bw * 2.1, 0.02, zb + 0.06), 'tint' if g['civ_bow'] else 'sj_red', T(0, yb * 0.985, (zb + 0.06) / 2 + 0.01) @ R('X', -18),
          bevel=0.004, tint=g['civ_bow'])   # the painted bow board
    p.add(props.box(bw * 2.2, 0.03, 0.022), 'sj_ochre', T(0, yb * 0.985 + 0.03, zb + 0.05), bevel=0.004)
    for sx in (-1, 1):
        y = yb * 0.84
        z = deck(y) - 0.06
        x = sx * (hull.side_x(hull.s_of(y), z) + 0.004)
        p.add(props.ellipsoid(0.006, 0.05, 0.034, seg=12), 'sj_eye', T(x, y, z), smooth=True)
        p.add(props.ellipsoid(0.009, 0.024, 0.024, seg=12), 'nn_black', T(x * 1.01, y + 0.006, z), smooth=True)
    # the gun bulwark along the waist: red, ochre-capped, a row of ports with the guns run out (dark, long barrels)
    b0, b1, bh = g['bulwark']
    for sx in (-1, 1):
        rings = []
        for i in range(25):
            y = b0 + (b1 - b0) * i / 24
            xo = sx * (hull.deck_half(hull.s_of(y)) + 0.004)
            rings.append([(xo, y, deck(y) - 0.01), (xo, y, deck(y) + bh), (xo - sx * 0.018, y, deck(y) + bh), (xo - sx * 0.018, y, deck(y) - 0.01)])
        p.add(props.loft(rings), 'sj_ochre', keys=lambda f: ('sj_red', False) if f.normal.z > 0.85 else None)   # ochre, a black cap
        for k in range(g['guns']):
            y = b0 + 0.16 + (b1 - b0 - 0.4) * k / (g['guns'] - 1)
            xo = sx * (hull.deck_half(hull.s_of(y)) + 0.006)
            z = deck(y) + bh * 0.5
            gr = g['gun_r']
            p.add(props.box(0.012, 0.11, bh * 0.95), 'nn_black', T(xo, y, z))
            vkit.rod(p, (xo - sx * 0.03, y, z), (xo + sx * 0.28, y, z + 0.012), gr, 'gun', r1=gr * 0.8, seg=10)
            p.add(props.lathe([(0.0, gr * 1.1), (0.03, gr * 1.1)], seg=12), 'gun', T(xo + sx * 0.25, y, z + 0.011) @ R('Y', sx * 90), smooth=True)   # the muzzle swell
    # the high stern: a raised poop overhanging aft, its gallery and great rudder
    s0, s1, sh = g['stern']
    for sx in (-1, 1):
        rings = []
        for i in range(13):
            y = s0 + (s1 - s0) * i / 12
            xo = sx * (hull.deck_half(hull.s_of(max(y, -yb))) + 0.006)
            xi = xo - sx * 0.02
            z0 = deck(max(y, -yb)) - 0.01
            rings.append([(xo, y, z0), (xo, y, z0 + sh), (xi, y, z0 + sh), (xi, y, z0)])
        p.add(props.loft(rings), 'sj_tar', keys=lambda f: ('sj_ochre', False) if f.normal.z > 0.85 else None)
    zs = deck(-yb) - 0.01
    ws = (hull.deck_half(-1.0) + 0.006) * g['transom_w']   # the high transom, wider than the hull's end
    p.add(props.box(2 * ws + 0.02, 0.03, sh + 0.12), 'sj_red', T(0, s0 - 0.005, zs + sh / 2 - 0.04), bevel=0.006)   # the transom
    if g['civ_transom']:   # the stern's painted panel (civ)
        p.add(props.box(2 * ws * 0.78, 0.008, sh * 0.9), 'tint', T(0, s0 - 0.022, zs + sh * 0.42), bevel=0.003, tint=True)
    p.add(props.box(2 * ws * 0.3, 0.008, 0.04), 'sj_ochre', T(0, s0 - 0.028, zs + sh * 0.42), bevel=0.003)
    p.add(props.box(2 * ws + 0.04, 0.02, 0.02), 'sj_ochre', T(0, s0 - 0.01, zs + sh + 0.02), bevel=0.004)
    p.add(props.box(2 * ws * 0.9, abs(s1 - s0) * 0.95, 0.016), 'sj_deck', T(0, (s0 + s1) / 2, deck((s0 + s1) / 2) + sh), bevel=0.004)
    p.add(props.box(0.024, 0.16, 0.24), 'sj_tar', T(0, -yb - 0.05, 0.06), bevel=0.005)   # the rudder
    # paddle boxes: half-round, fanned vents, civ rims; a dark wheel below
    py_, pr, pw = g['paddle']
    for sx in (-1, 1):
        s = hull.s_of(py_)
        x = sx * (hull.side_x(s, 0.08) + pw / 2 + 0.005)   # (critic 2: wider housings give the bow-on view width)
        zc = 0.07
        nseg = 12
        for t in range(nseg):   # the housing, wedge by wedge: ochre vents and dark slots, a civ crescent at the rim
            a0, a1 = math.pi * t / nseg, math.pi * (t + 1) / nseg
            ri = pr * 0.7
            p.add(props.slab([(0.0, zc), (ri * math.cos(a0), zc + ri * math.sin(a0)), (ri * math.cos(a1), zc + ri * math.sin(a1))], pw, 'X'),
                  'sj_vent' if t % 2 else 'sj_tar', T(x, py_, 0))
            p.add(props.slab([(ri * math.cos(a0), zc + ri * math.sin(a0)), (pr * math.cos(a0), zc + pr * math.sin(a0)),
                              (pr * math.cos(a1), zc + pr * math.sin(a1)), (ri * math.cos(a1), zc + ri * math.sin(a1))], pw, 'X'),
                  'tint' if g['civ_paddle'] else 'sj_ochre', T(x, py_, 0), tint=g['civ_paddle'])
        arc = [(x + sx * (pw / 2 + 0.004), py_ + (pr + 0.004) * math.cos(math.pi * t / 16), zc + (pr + 0.004) * math.sin(math.pi * t / 16)) for t in range(17)]
        p.add(props.tube(arc, 0.016, seg=6, scale_xy=(1.0, 0.7)), 'sj_ochre', smooth=True)
        p.add(props.box(0.012, 2 * pr + 0.02, 0.02), 'sj_ochre', T(x + sx * (pw / 2 + 0.004), py_, zc + 0.008))
        vkit.ring_x(p, pr * 0.92, pr * 0.8, pw * 0.8, "sj_batten", center=(x, py_, zc), seg=24)
    # the deckhouse with a vaulted roof, and the funnel (black, two civ bands) between the paddle boxes
    h0, h1, hh = g['house']
    hw_ = hull.deck_half(hull.s_of((h0 + h1) / 2)) * 1.1
    p.add(props.box(hw_, h1 - h0, hh), 'sj_tar', T(0, (h0 + h1) / 2, deck((h0 + h1) / 2) + hh / 2), bevel=0.006)
    vault = [[(hw_ / 2 * 1.08 * math.cos(math.pi * t / 10), y, deck((h0 + h1) / 2) + hh + 0.04 * math.sin(math.pi * t / 10)) for t in range(11)]
             for y in (h0 - 0.01, h1 + 0.01)]
    p.add(props.loft(vault), 'sj_red', smooth=True)
    fy, fr, fh = g['funnel']
    zf = deck(fy)
    p.add(props.lathe([(0.0, fr), (fh, fr * 0.96)], seg=16), 'sj_funnel', T(0, fy, zf) @ R('X', -4), smooth=True)
    for zb_ in (fh * 0.58, fh * 0.8):
        p.add(props.lathe([(0.0, fr * 1.04), (0.08, fr * 1.03)], seg=16), 'tint', T(0, fy, zf) @ R('X', -4) @ T(0, 0, zb_), smooth=True, tint=True)
    p.add(props.lathe([(0.0, fr * 1.12), (0.02, fr * 1.12)], seg=16), 'sj_funnel', T(0, fy, zf) @ R('X', -4) @ T(0, 0, fh - 0.02), smooth=True)
    # a swivel gun on the foredeck
    yg = yb * 0.72
    p.add(props.lathe([(0.0, 0.02), (0.05, 0.018)], seg=8), 'sj_batten', T(0, yg, deck(yg)), smooth=True)
    vkit.rod(p, (0, yg - 0.05, deck(yg) + 0.06), (0, yg + 0.12, deck(yg) + 0.07), 0.012, 'gun', r1=0.009, seg=8)

    # the masts (staggered, raked), their sails sheeted off the centreline, the black flag at the main
    k = g['rig_scale']
    heads = []
    for m in g['masts']:
        y, x = m['y'], m['x']
        z0 = deck(y) - 0.02
        top = (x, y + math.tan(math.radians(m['rake'])) * -m['h'] * k, z0 + m['h'] * k)
        tip = (x, y + math.tan(math.radians(m['rake'])) * -(m['h'] * k + 0.05), z0 + m['h'] * k + 0.05)
        vkit.rod(p, (x, y, z0), tip, 0.024, 'sj_batten', r1=0.016, seg=8)
        heads.append(top)
    fw, fh_ = g['flag']
    _nn_flag(p, (heads[1][0], heads[1][1] - 0.004, heads[1][2] + 0.01), fw, fh_, droop=0.0, wave=0.0)   # a long civ pennant
    for t in (heads[0], heads[2]):   # civ pennants at the other mastheads
        _nn_flag(p, (t[0], t[1] - 0.004, t[2] + 0.01), g['pennant'][0], g['pennant'][1], droop=0.0, wave=0.0)
    p.finish()
    for m in g['masts']:
        r = vkit.part(root, 'sail', c, SJUNK_WEAR)
        sw, sh_ = m['sail']
        _sj_sail(r, sw * k, sh_ * k, g['battens'], civ_head=g['civ_head'])
        o = r.finish()
        z0 = deck(m['y']) + 0.06
        o.location = (m['x'] + 0.025, m['y'], z0)
        o.rotation_euler = (math.radians(-m['rake']), 0, math.radians(m.get('sheet', g['sheet'])))
    return root


# ------------------------------------------------------------ troop ship (realism pass: a WWII attack transport, 1944)

# An attack transport (APA, Haskell class, 1944; notes/realism/Troop Ship.md): a Victory-ship hull 139 x 19 m
# (L:B ~7.3) with a raked stem, a short raised forecastle and a cruiser stern; a three-tier house amidships
# with the bridge and its wings and one stout funnel; LCVPs nested in Welin davits along both sides of the
# house and stacked on the hatches; king posts with cargo booms over the hatches fore and aft; a 5-inch gun
# aft and a 3-inch forward. Measure 22: navy blue up to the main deck, haze grey above, a black boot top.
# L 3.4 = 139 m (1 unit = 41 m).
APA = dict(
    L=3.4, B=0.62, D=0.32, draft=0.09, sheer_stern=0.04, sheer_bow=0.1,
    house=(-0.56, 0.4), funnel=(-0.16, 0.13, 0.42),
    davits=(-0.46, -0.22, 0.02, 0.26), boat_out=0.075, hatches=(1.0, 0.62, -0.84, -1.16), kingposts=(0.82, 0.5, -0.68, -1.0), kingpost_h=0.52,
    civ_hatches=False, civ_stripe=(0.07, 0.09), civ_deck=True, house_civ=False, rig_scale=1.0,
    fit_scale=0.72,   # the lead's size rule: about the stock Transport's size
)

APA_COLORS = {
    'ap_navy': '#232c38', 'ap_navy_alt': '#1f2732', 'ap_haze': '#686e74', 'ap_haze_alt': '#5e646a', 'ap_deck': '#2e3238',
    'ap_dark': '#1e2126', 'ap_boat': '#6c6e42', 'ap_rim': '#c8c8b4', 'ap_well': '#3a3e40', 'ap_glass': '#121416', 'ap_gun': '#3a3e42',
}
for _k, _v in (('ap_navy', ('#232c38', 0.65, 0.0, 'metal')), ('ap_navy_alt', ('#1f2732', 0.65, 0.0, 'metal')),
               ('ap_haze', ('#686e74', 0.65, 0.0, 'metal')), ('ap_haze_alt', ('#5e646a', 0.65, 0.0, 'metal')),
               ('ap_deck', ('#2e3238', 0.8, 0.0, 'metal')), ('ap_dark', ('#2a2e34', 0.6, 0.15, 'metal')),
               ('ap_boat', ('#6c6e42', 0.7, 0.0, 'metal')), ('ap_rim', ('#c8c8b4', 0.7, 0.0, 'metal')), ('ap_well', ('#3a3e40', 0.7, 0.1, 'metal')),
               ('ap_glass', ('#121416', 0.3, 0.0, None)), ('ap_gun', ('#3a3e42', 0.45, 0.5, 'metal'))):
    props.DEFAULTS.setdefault(_k, _v)
APA_WEAR = dict(SHIP_WEAR, ap_navy={'grime': 0.5, 'rust': 0.14}, ap_navy_alt={'grime': 0.5, 'rust': 0.14},
                ap_haze={'grime': 0.35, 'rust': 0.06}, ap_haze_alt={'grime': 0.35, 'rust': 0.06}, ap_deck={'grime': 0.5})


def _ap_lcvp(p, x, y, z, yaw=0.0, s=1.0):
    """An LCVP: a light grey box hull with a dark well and its bow ramp."""
    m = T(x, y, z) @ R('Z', yaw) @ props.S(s)
    p.add(props.box(0.07, 0.19, 0.02), 'nn_black', m @ T(0, 0, -0.028), bevel=0.006)   # the dark shadow beneath
    p.add(props.box(0.075, 0.2, 0.045), 'ap_boat', m, bevel=0.014)
    p.add(props.box(0.079, 0.204, 0.008), 'ap_rim', m @ T(0, 0, 0.022), bevel=0.003)    # the light gunwale rim
    p.add(props.box(0.055, 0.15, 0.02), 'ap_well', m @ T(0, -0.01, 0.017), bevel=0.004)
    p.add(props.box(0.072, 0.018, 0.034), 'ap_boat', m @ T(0, 0.1, 0.008), bevel=0.005)


def troopship_apa(colors, **kw):
    """The realism pass's Troop Ship: a Haskell-class attack transport (see APA)."""
    g = dict(APA, **kw)
    c = dict(SHIP_COLORS, **APA_COLORS)
    c.update(colors)
    root = vkit.root('Troop Ship')
    root['fit_scale'] = g['fit_scale']
    p = vkit.part(root, 'hull', c, APA_WEAR)
    hull = Hull(L=g['L'], B=g['B'], D=g['D'], draft=g['draft'], sheer_stern=g['sheer_stern'], sheer_bow=g['sheer_bow'],
                bow_p=2.4, stern_p=2.6, transom=0.35, rake_bow=0.3, fullness=0.28)   # a bulky cargo-liner hull
    yb = g['L'] / 2

    def deck(y):
        return hull.deck_z(hull.s_of(y))

    def hkeys(f):
        cc = f.calc_center_median()
        if f.normal.z > 0.85:
            return ('ap_deck', False)
        if cc.z < 0.04:
            return ('nn_black', False)   # the boot top: a dark waterline band
        if cc.z > g['D'] - 0.004:
            return ('ap_haze', False)    # Measure 22: haze grey above the main deck's level
        if int(math.floor((cc.y + 4) / 0.24)) % 2:
            return ('ap_navy_alt', False)
        return None

    _nn_hull(p, hull, hkeys, key='ap_navy', n=56)
    _nn_foam(p, hull, r=0.01, bow_wave=0.03)
    if g['civ_stripe']:   # a painted civ stripe along the hull under the deck edge
        _nn_line(p, hull, lambda s: hull.deck_z(s) - g['civ_stripe'][0], g['civ_stripe'][1] / 2, 'tint', s0=-0.95, s1=0.88, out=0.002, flat=0.4, tint=True)
    _nn_line(p, hull, lambda s: hull.deck_z(s) - 0.004, 0.008, 'ap_haze', s0=-0.96, s1=0.96, out=0.001)   # the deck edge
    for sx in (-1, 1):   # portholes along the hull under the house
        for k in range(16):
            y = -0.7 + 1.3 * k / 15
            s = hull.s_of(y)
            z = deck(y) - 0.05
            p.add(props.box(0.006, 0.016, 0.014), 'ap_glass', T(sx * (hull.side_x(s, z) + 0.002), y, z))
    # the forecastle: a raised deck at the bow with a breakwater
    yf = 1.12
    rings = []
    for i in range(13):
        y = yf + (yb * 0.98 - yf) * i / 12
        hx = hull.deck_half(hull.s_of(y))
        z0 = deck(y) - 0.01
        rings.append([(hx, y, z0), (hx, y, z0 + 0.06), (-hx, y, z0 + 0.06), (-hx, y, z0)])
    p.add(props.loft(rings), 'ap_haze', keys=lambda f: ('ap_deck', False) if f.normal.z > 0.85 else None)
    p.add(props.box(hull.deck_half(hull.s_of(yf)) * 1.8, 0.03, 0.1), 'ap_haze', T(0, yf + 0.02, deck(yf) + 0.04), bevel=0.006)
    if g['civ_deck']:   # a civ band across the forecastle deck
        yd = yf + 0.16
        p.add(props.box(hull.deck_half(hull.s_of(yd)) * 1.9, 0.22, 0.008), 'tint', T(0, yd, deck(yd) + 0.054), tint=True)
    # the 3-inch gun on the forecastle, the 5-inch aft (shielded, dark long barrels)
    for (yg, yaw, big) in ((yb * 0.86, 0, False), (-yb * 0.88, 180, True)):
        zg = deck(yg) + (0.05 if not big else 0.0)
        s_ = 1.3 if big else 1.0
        m = T(0, yg, zg) @ R('Z', yaw)
        p.add(props.lathe([(0.0, 0.05 * s_), (0.03 * s_, 0.05 * s_)], seg=14), 'ap_haze', m, smooth=True)
        p.add(props.box(0.08 * s_, 0.09 * s_, 0.05 * s_), 'ap_haze', m @ T(0, 0, 0.05 * s_), bevel=0.012)
        vkit.rod(p, m @ Vector((0, 0.03 * s_, 0.055 * s_)), m @ Vector((0, 0.22 * s_, 0.06 * s_)), 0.011, 'ap_gun', r1=0.009, seg=8)
    # hatches (civ-painted tarps), king posts with booms; LCVPs stacked on the hatches
    for k, yh in enumerate(g['hatches']):
        w = hull.deck_half(hull.s_of(yh)) * 1.1
        p.add(props.box(w + 0.03, 0.2, 0.03), 'ap_dark', T(0, yh, deck(yh) + 0.015), bevel=0.004)
        p.add(props.box(w, 0.17, 0.012), 'tint' if g['civ_hatches'] else 'ap_haze_alt', T(0, yh, deck(yh) + 0.034), bevel=0.003,
              tint=g['civ_hatches'])
        if k in (1, 2):
            _ap_lcvp(p, 0.0, yh, deck(yh) + 0.065, s=0.95)
    for yk in g['kingposts']:
        zk = deck(yk)
        for sx in (-1, 1):
            kh = g['kingpost_h']
            vkit.rod(p, (sx * 0.12, yk, zk), (sx * 0.12, yk, zk + kh), 0.048, 'ap_haze', r1=0.04, seg=8)
            sgn = 1 if yk in (0.82, -1.0) else -1   # the outer pairs' booms reach outboard, the inner ones toward the house
            sgn = sgn if yk > 0 else -sgn
            vkit.rod(p, (sx * 0.12, yk, zk + 0.05), (sx * 0.08, yk + sgn * 0.34, zk + kh * 0.78), 0.032, 'ap_dark', seg=6)   # the booms
        p.add(props.box(0.32, 0.05, 0.05), 'ap_haze', T(0, yk, zk + g['kingpost_h'] - 0.03), bevel=0.006)
    # the house amidships: three tiers, the bridge with its wings and windows, civ band on the bridge front
    h0, h1 = g['house']
    zc = deck((h0 + h1) / 2)
    tiers = ((h0, h1, 0.24, 0.8), (h0 + 0.06, h1 - 0.02, 0.18, 0.74), (h1 - 0.26, h1 - 0.03, 0.13, 0.6),
             (h1 - 0.2, h1 - 0.04, 0.1, 0.46))   # the raised bridge (critic 2)
    z = zc
    for k, (a, b, h, wf) in enumerate(tiers):
        w = hull.deck_half(hull.s_of((a + b) / 2)) * 2 * wf
        p.add(props.box(w, b - a, h), 'ap_haze', T(0, (a + b) / 2, z + h / 2), bevel=0.006)
        for sx in (-1, 1):
            n = max(2, int((b - a) / 0.05))
            for i in range(n):
                yy = a + (b - a) * (i + 0.5) / n
                p.add(props.box(0.005, 0.016, 0.016), 'ap_glass', T(sx * (w / 2 + 0.002), yy, z + h * 0.62))
                if h > 0.15:   # a second row on the tall tiers
                    p.add(props.box(0.005, 0.016, 0.016), 'ap_glass', T(sx * (w / 2 + 0.002), yy, z + h * 0.3))
        if k == 2:
            p.add(props.box(w + 0.18, 0.06, 0.02), 'ap_haze', T(0, b - 0.03, z + h - 0.01), bevel=0.004)   # the bridge wings
            p.add(props.box(w * 0.9, 0.006, h * 0.3), 'ap_glass', T(0, b + 0.002, z + h * 0.62))           # the bridge windows
        if k == 3:   # the bridge's window band, front and sides
            p.add(props.box(w * 0.92, 0.008, h * 0.34), 'ap_glass', T(0, b + 0.004, z + h * 0.6))
            for sx in (-1, 1):
                p.add(props.box(0.008, (b - a) * 0.86, h * 0.34), 'ap_glass', T(sx * (w / 2 + 0.003), (a + b) / 2, z + h * 0.6))
        if k == 1 and g['house_civ']:
            p.add(props.box(w * 1.01, 0.008, h * 0.7), 'tint', T(0, b + 0.004, z + h * 0.5), tint=True)   # civ band on the front
            for sx in (-1, 1):
                p.add(props.box(0.006, (b - a) * 0.7, h * 0.5), 'tint', T(sx * (w / 2 + 0.003), b - (b - a) * 0.38, z + h * 0.5), tint=True)
        z += h
    zt = z
    # the funnel: stout, a civ band, a black cap
    fy, fr, fh = g['funnel']
    zf = zc + 0.24 + 0.18
    p.add(props.lathe([(0.0, fr), (fh, fr * 0.95)], seg=16, scale_xy=(0.75, 1.0)), 'ap_haze', T(0, fy, zf), smooth=True)
    p.add(props.lathe([(0.0, fr * 0.97), (0.08, fr * 0.96)], seg=16, scale_xy=(0.75, 1.0)), 'tint', T(0, fy, zf + fh - 0.11), smooth=True, tint=True)
    p.add(props.lathe([(0.0, fr * 0.97), (0.025, fr * 0.97)], seg=16, scale_xy=(0.75, 1.0)), 'nn_black', T(0, fy, zf + fh - 0.02), smooth=True)
    # the mast on the bridge: a pole with a yard and a radar bar
    ym = h1 - 0.14
    vkit.rod(p, (0, ym, zt), (0, ym, zt + 0.28 * g['rig_scale']), 0.016, 'ap_haze', r1=0.012, seg=6)
    p.add(props.box(0.16, 0.014, 0.014), 'ap_dark', T(0, ym, zt + 0.2 * g['rig_scale']))
    p.add(props.box(0.08, 0.02, 0.03), 'ap_haze', T(0, ym, zt + 0.12 * g['rig_scale']), bevel=0.004)
    # LCVPs nested in davits along both sides of the house
    for y in g['davits']:
        for sx in (-1, 1):
            dh = hull.deck_half(hull.s_of(y))
            x = sx * (dh + g['boat_out'])   # hung out over the side
            zb = deck(y) + 0.06
            _ap_lcvp(p, x, y, zb, s=1.25)
            for dy in (-0.08, 0.08):
                xp = sx * (dh - 0.02)
                p.add(props.box(0.032, 0.032, 0.24), 'ap_dark', T(xp, y + dy, deck(y) + 0.12), bevel=0.004)          # davit
                p.add(props.box(abs(x - xp) + 0.035, 0.032, 0.032), 'ap_dark', T((x + xp) / 2, y + dy, deck(y) + 0.24), bevel=0.004)
                p.add(props.box(0.012, 0.012, 0.15), 'ap_dark', T(x, y + dy, deck(y) + 0.165))   # the falls
    # 40 mm tubs at the house's corners
    for (yy, sx) in ((h1 + 0.05, 1), (h1 + 0.05, -1), (h0 - 0.05, 1), (h0 - 0.05, -1)):
        xx = sx * hull.deck_half(hull.s_of(yy)) * 0.6
        p.add(props.lathe([(0.0, 0.035), (0.035, 0.035)], seg=12), 'ap_haze', T(xx, yy, deck(yy)), smooth=True)
        vkit.rod(p, (xx, yy, deck(yy) + 0.04), (xx, yy + (0.1 if yy > 0 else -0.1), deck(yy) + 0.07), 0.006, 'ap_gun', seg=6)
    # the ensign at the stern (civ)
    ye = -yb * 0.97
    ze = deck(ye)
    vkit.rod(p, (0, ye, ze), (0, ye, ze + 0.2), 0.02, 'ap_dark', seg=6)
    _nn_flag(p, (0, ye, ze + 0.2), 0.2, 0.12, droop=0.5)
    p.finish()
    return root


def _ssn_plate(outline, z0, z1):
    """A flat plate: a closed (x, y) outline extruded from z0 to z1."""
    bm = props.bmesh.new()
    top = [bm.verts.new((x, y, z1)) for x, y in outline]
    bot = [bm.verts.new((x, y, z0)) for x, y in outline]
    bm.faces.new(top)
    bm.faces.new(list(reversed(bot)))
    n = len(outline)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((bot[i], bot[j], top[j], top[i]))
    props.bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def _steel_builder(name):
    return lambda colors, **kw: steel_ship(name, STEEL[name], colors)


def _sailing_builder(name):
    return lambda colors, **kw: sailing_ship(name, SAILING[name], colors)


BUILDERS = {'Galley': galley_bireme, 'Caravel': caravel_redonda, 'Curragh': curragh_hide, 'Dromon': dromon_lateen,
            **{n: _sailing_builder(n) for n in SAILING}, **{n: _steel_builder(n) for n in STEEL}}
BUILDERS['Carrack'] = carrack_nau   # the realism pass (its own block)
BUILDERS['Galleon'] = galleon_armada   # the realism pass (its own block)
BUILDERS['Frigate'] = frigate_36   # the realism pass (its own block)
BUILDERS['Privateer'] = privateer_brig   # the realism pass (its own block)
BUILDERS['Man-O-War'] = man_o_war_74   # the realism pass (its own block)
BUILDERS['Ironclad'] = ironclad_casemate   # the realism pass (its own block)
BUILDERS['Transport'] = transport_lst   # the realism pass (its own block)
BUILDERS['Destroyer'] = destroyer_fletcher   # the realism pass (its own block)
BUILDERS['Cruiser'] = cruiser_baltimore   # the realism pass (its own block)
BUILDERS['Battleship'] = battleship_iowa   # the realism pass (its own block)
BUILDERS['Aegis Cruiser'] = aegis_ticonderoga   # the realism pass (its own block)
BUILDERS['Carrier'] = carrier_nimitz   # the realism pass (its own block)
BUILDERS['Submarine'] = submarine_gato   # the realism pass (its own block)
BUILDERS['Nuclear Submarine'] = nuclear_sub_la   # the realism pass (its own block)
BUILDERS['Japanese Warship'] = atakebune_sengoku   # the realism pass, NumPy remake (its own block)
BUILDERS['Pirate'] = steam_junk_pirate   # the realism pass, NumPy remake (its own block)
BUILDERS['Troop Ship'] = troopship_apa   # the realism pass, NumPy remake (its own block)


def build(unit, colors=None, **params):
    return BUILDERS[unit](colors or {}, **params)
