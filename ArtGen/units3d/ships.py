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
        if tint_borders and abs(u) > 0.88:
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
        rows.append([bm.verts.new((0.02 * math.sin(3 * t + phase), top[1] - w * t, top[2] - hh * k))
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
        if abs(d - 1.15) < 0.16 and abs(ex) < 0.95:
            return True
    return abs(ez) < 0.1 and abs(ex) < 0.75


def _pattee(x, z, h):
    """A cross pattee: four arms flaring outward."""
    s = 0.42 * h
    ex, ez = abs(x / s), abs(z / s)
    return (ex <= 1.0 and ez <= 0.16 + 0.42 * ex) or (ez <= 1.0 and ex <= 0.16 + 0.42 * ez)


def _disc_cross(x, z, h):
    """The caravel's emblem as in Civ3: a round disc with a cross on it (the
    cross left in sailcloth, the disc in the civ color)."""
    s = 0.4 * h
    ex, ez = x / s, z / s
    if math.hypot(ex, ez) > 1.0:
        return False
    return not (abs(ex) < 0.16 or abs(ez) < 0.16)


EMBLEMS = {'pisces': _pisces, 'pattee': _pattee, 'disc_cross': _disc_cross}


# ------------------------------------------------------------ galley

GALLEY_COLORS = {'wood': '#7c5530', 'wood_alt': '#664424', 'wood_dark': '#2a1e14', 'wood_light': '#9c7a3c',
                 'deck': '#74583a', 'rail': '#3a2816', 'sail': '#cdbf9e', 'gold': '#8e6c32', 'oar': '#4a3420'}


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
    add_hull(p, hull, stripes=[(-0.03, 0.015, 'hull_grime'), (0.015, 0.04, 'wood_light'),
                               (0.1, 0.125, 'wood_dark'), (0.19, 0.21, 'wood_light')], plank=0.04)
    # a curled stern post and a short stem
    curl = [(0, -1.06, 0.36), (0, -1.17, 0.5), (0, -1.22, 0.66), (0, -1.19, 0.8), (0, -1.1, 0.87), (0, -1.02, 0.84),
            (0, -1.02, 0.76)]
    p.add(props.tube(curl, [0.045 - 0.004 * i for i in range(len(curl))], seg=10), 'wood', smooth=True)
    p.add(props.tube([(0, 1.06, 0.28), (0, 1.14, 0.38), (0, 1.12, 0.47)], [0.035, 0.026, 0.018], seg=10), 'wood',
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
    p.add(canopy, 'tint', smooth=True, tint=True)
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
    # the mast with a gilded finial, the square sail with the emblem and banded edges
    vkit.rod(p, (0, 0.05, 0.2), (0, 0.05, 1.48), 0.036, 'wood_dark', seg=10)
    p.add(props.ellipsoid(0.045, 0.045, 0.055, seg=12), 'gold', T(0, 0.05, 1.51), smooth=True)
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


CARAVEL_COLORS = {'wood': '#74502e', 'wood_alt': '#5e4024', 'wood_dark': '#2a1e14', 'wood_light': '#8e7050',
                  'deck': '#7a5e3c', 'rail': '#2e2014', 'sail': '#d8ccae', 'cordage': '#2b2118'}


def caravel(colors, rig_h=1.45, rig_w=1.75):
    """A caravel: a rich dark hull with curve, sheer and tumblehome, grime at
    the waterline, a sterncastle and bowsprit; square fore and main sails
    with the civ cross, a topsail and a lateen mizzen, weathered and patched;
    civ pennants."""
    c = dict(SHIP_COLORS, **CARAVEL_COLORS)
    c.update(colors)
    root = vkit.root('Caravel')
    p = vkit.part(root, 'hull', c, SHIP_WEAR)
    hull = Hull(L=2.5, B=0.66, D=0.2, draft=0.14, sheer_stern=0.3, sheer_bow=0.3, bow_p=1.3, stern_p=2.0,
                transom=0.4, rake_bow=0.3, fullness=1.0, tumblehome=0.2)
    add_hull(p, hull, stripes=[(-0.03, 0.03, 'hull_grime'), (0.03, 0.05, 'wood_light'), (0.12, 0.145, 'wood_light'),
                               (0.22, 0.25, 'wood_dark')])
    _castle(p, hull, -1.25, -0.68, 0.2)
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
    sail(p, 0.54, z + h, w * 0.92, w, h, 'disc_cross', billow=0.14, seed=4)
    # mainmast: a course with the cross and a topsail, a pennant at the top
    z = deck(0.0) + (0.48 - deck(0.0)) * rig_h
    h1, w1 = 0.6 * rig_h, 1.15 * rig_w
    h2, w2 = 0.3 * rig_h, 0.66 * rig_w
    top = z + h1 + 0.025 * rig_h + h2 + 0.16
    mast(0.0, top, 0.032)
    sail(p, 0.02, z + h1, w1 * 0.92, w1, h1, 'disc_cross', billow=0.15, seed=2)
    sail(p, 0.02, z + h1 + 0.025 * rig_h + h2, w2 * 0.92, w2, h2, None, billow=0.09, seed=3, patches=2)
    pennant(p, (0, 0.0, top), 0.42, 0.1)
    # mizzen: a lateen sail with the cross
    mast(-0.7, deck(-0.7) + 1.1, 0.022)
    lateen(p, -0.7, deck(-0.7) + 1.0, 1.5, 30.0, 0.72, 'disc_cross')
    pennant(p, (0, -0.7, deck(-0.7) + 1.1), 0.14, 0.08, phase=1.0)
    # stays and shrouds
    vkit.rod(p, (0, 0.0, top - 0.1), (0, 1.6, 0.52), 0.006, 'cordage', seg=4)
    for sx in (-1, 1):
        for dy in (-0.08, 0.08):
            vkit.rod(p, (0, dy, top - 0.25), (sx * 0.33, dy - 0.05, deck(0.0) + 0.02), 0.005, 'cordage', seg=4)
    p.finish()
    return root


BUILDERS = {'Galley': galley, 'Caravel': caravel}


def build(unit, colors=None, **params):
    return BUILDERS[unit](colors or {}, **params)
