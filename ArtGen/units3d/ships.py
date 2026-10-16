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
                 rake_bow=0.0, fullness=0.55, tumblehome=0.0):
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


def add_hull(p, hull, stripes=(), n=36, plank=0.045):
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
                p.add(props.tube(path, max(hz, 0.008), seg=6, scale_xy=(1.0, 0.3)), key, smooth=True)   # tall, flat on the hull


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

    add_hull(p, hull, stripes=[(-0.03, 0.03, 'boot')], plank=1.0)
    # civ-colored deck panels at bow and stern
    for y0, y1 in cfg.get('civ_panels', [(L * 0.32, L * 0.44), (-L * 0.46, -L * 0.36)]):
        yc = (y0 + y1) / 2
        w = hull.deck_half(hull.s_of(yc)) * 1.6
        p.add(props.box(w, y1 - y0, 0.006), 'tint', T(0, yc, deck(yc) + 0.004), bevel=0.002, tint=True)
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


def _hull_number(p, hull, digits='110'):
    """The hull number painted on both bows, digit by digit (bars as a seven-segment figure)."""
    segs = {'0': 'abcdef', '1': 'bc', '2': 'abged', '3': 'abgcd', '4': 'fgbc', '5': 'afgcd', '6': 'afgedc', '7': 'abc',
            '8': 'abcdefg', '9': 'abcfgd'}
    y0 = hull.L * 0.38
    s = hull.s_of(y0)
    z0 = hull.deck_z(s) * 0.55
    w, hh = 0.03, 0.05
    for sx in (-1, 1):
        x = sx * (hull.side_x(s, z0) + 0.005)
        for i, d in enumerate(digits):
            yc = y0 - sx * (i - (len(digits) - 1) / 2) * 0.045
            for sg in segs[d]:
                dy, dz, ly, lz = {'a': (0, hh, w, 0.006), 'g': (0, 0, w, 0.006), 'd': (0, -hh, w, 0.006),
                                  'f': (-w / 2, hh / 2, 0.006, hh), 'b': (w / 2, hh / 2, 0.006, hh),
                                  'e': (-w / 2, -hh / 2, 0.006, hh), 'c': (w / 2, -hh / 2, 0.006, hh)}[sg]
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
    for y, z in ((0.16, 0.5), (-0.3, 0.36)):
        p.add(props.ellipsoid(0.045, 0.045, 0.045, seg=14), 'radome', T(0, y, deck(y) + z), smooth=True)


def _carrier_extras(p, hull, deck):
    # an angled flight deck flaring out to port, its lines, civ-colored aircraft parked along the edges, the island
    L = hull.L
    zd = deck(0) + 0.08
    p.add(props.box(0.82, L * 0.98, 0.03), 'flight_deck', T(0.04, 0.0, zd), bevel=0.01)
    p.add(props.box(0.38, L * 0.55, 0.03), 'flight_deck', T(-0.42, -0.25, zd) @ R('Z', -9), bevel=0.01)   # the angled deck
    p.add(props.box(0.012, L * 0.9, 0.004), 'deck_line', T(0.0, 0.0, zd + 0.017))
    p.add(props.box(0.012, L * 0.6, 0.004), 'deck_line', T(-0.3, -0.25, zd + 0.017) @ R('Z', -9))
    for sx in (-1, 1):
        p.add(props.box(0.008, L * 0.95, 0.004), 'deck_line', T(sx * 0.38, 0.0, zd + 0.017))
    for i in range(7):   # jets parked along the starboard edge and the bow, in the civ color
        x, y = (0.3, -0.9 + 0.26 * i) if i < 5 else (-0.15 + 0.2 * (i - 5), L * 0.4)
        p.add(props.box(0.04, 0.2, 0.025), 'tint', T(x, y, zd + 0.03), bevel=0.008, tint=True)
        p.add(props.box(0.17, 0.06, 0.008), 'tint', T(x, y - 0.02, zd + 0.03), bevel=0.003, tint=True)
        p.add(props.box(0.06, 0.03, 0.006), 'tint', T(x, y - 0.1, zd + 0.035), bevel=0.002, tint=True)
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
                  transom=0.8, fullness=0.35),
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
        extras=[lambda p, h, d: (_lattice_mast(p, (0, 0.3, d(0.3) + 0.38), 0.4), _hull_number(p, h, '110'))],
        number=False, barrel=0.35,
    ),
    'Cruiser': dict(
        hull=dict(L=2.9, B=0.42, D=0.18, draft=0.11, sheer_stern=0.02, sheer_bow=0.12, bow_p=1.4, stern_p=2.4,
                  transom=0.5, rake_bow=0.35, fullness=0.55),
        blocks=[(0, 0.3, 0.0, 0.3, 0.56, 0.2, 'steel_light'), (0, 0.34, 0.2, 0.22, 0.3, 0.14, 'steel_light'),
                (0, 0.36, 0.34, 0.14, 0.14, 0.1, 'steel_light'), (0, -0.38, 0.0, 0.26, 0.6, 0.14, 'steel_light')],
        turrets=[(0.98, 0, 3, 0.85, 0.0), (0.74, 0, 3, 0.85, 0.08), (-0.98, 180, 3, 0.85, 0.0)],
        stacks=[(0.0, 0.24, 0.07, 0.14), (-0.24, 0.22, 0.07, 0.14)],
        extras=[lambda p, h, d: (_lattice_mast(p, (0, 0.32, d(0.32) + 0.44), 0.42), _hull_number(p, h, '27'))],
        number=False, barrel=0.42,
    ),
    'Battleship': dict(
        hull=dict(L=3.5, B=0.64, D=0.22, draft=0.14, sheer_stern=0.02, sheer_bow=0.14, bow_p=1.3, stern_p=2.3,
                  transom=0.55, rake_bow=0.4, fullness=0.65),
        civ_panels=[(1.25, 1.55), (-1.6, -1.3)],
        blocks=[(0, 0.15, 0.0, 0.42, 0.85, 0.2, 'steel_light'), (0, 0.22, 0.2, 0.32, 0.5, 0.18, 'steel_light'),
                (0, 0.28, 0.38, 0.22, 0.26, 0.16, 'steel_light'), (0, 0.3, 0.54, 0.14, 0.12, 0.12, 'steel_light'),
                (0, -0.6, 0.0, 0.36, 0.55, 0.14, 'steel_light')],
        civ_blocks=4,
        turrets=[(1.12, 0, 3, 1.3, 0.0), (0.84, 0, 3, 1.3, 0.1), (-0.95, 180, 3, 1.3, 0.1), (-1.22, 180, 3, 1.3, 0.0),
                 # secondary batteries along the superstructure's sides
                 (0.35, 90, 2, 0.55, 0.02), (0.0, 90, 2, 0.55, 0.02), (0.35, -90, 2, 0.55, 0.02), (0.0, -90, 2, 0.55, 0.02)],
        stacks=[(-0.15, 0.3, 0.09, 0.2)],
        extras=[lambda p, h, d: (_lattice_mast(p, (0, 0.3, d(0.3) + 0.66), 0.4), _lattice_mast(p, (0, -0.6, d(-0.6) + 0.14), 0.36))],
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


def _steel_builder(name):
    return lambda colors, **kw: steel_ship(name, STEEL[name], colors)


def _sailing_builder(name):
    return lambda colors, **kw: sailing_ship(name, SAILING[name], colors)


BUILDERS = {'Galley': galley, 'Caravel': caravel, 'Curragh': curragh, 'Dromon': dromon,
            **{n: _sailing_builder(n) for n in SAILING}, **{n: _steel_builder(n) for n in STEEL}}


def build(unit, colors=None, **params):
    return BUILDERS[unit](colors or {}, **params)
