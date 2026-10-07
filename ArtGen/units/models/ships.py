"""Ships: a rigid-vessel archetype ('ship') for every naval unit.

A ship is built once from a design (models/ship_designs.py) by a ShipBuilder:
a lofted hull (Hull), static parts (decks, castles, superstructures, masts,
rigging), and animated parts: sails (rebuilt per frame with their billow),
flags (waving), oars (sweeping), turrets (yaw, elevation, recoil) and weapon
emitters. Each frame poses the ship (bob, pitch, roll, or the death's list
and sinking), clips it at the waterline (z = 0, nothing under water is drawn),
and adds the effects in water space: a soft wake and foam at the waterline,
muzzle flashes, smoke, fire, projectiles.

Model space: the bow points +y, starboard is +x, the waterline is z = 0, in
'display meters' (a galley is ~2.3 m long) so the shared render style
(contours, AO) behaves like it does for the foot units.

Ships draw their cells themselves (`Ship.render_cell`, picked up by the
framework): a subtle, soft sun shadow on the water instead of a ground
shadow, the wake and foam composited under the hull without contours, then
the ship.

Calibration: the framework fits a unit's scale to the original's standing
height, which suits figures but not ships (their height depends on the
masts). A ship instead fits its length: the original DEFAULT flic's
east/west views give the width (bow to stern, bowsprit and all) and the
waterline; `Ship.__init__` measures the model the same way and sets the
anchor, and the model's calibration frame (ctx None, only used by the
framework's height fit) is a post of exactly the height that makes the fit
produce that scale. The catalog `scale` still multiplies it.
"""
import math

import numpy as np

import mesh as M
import render as R
import rig
from models import archetype

TAU = 2 * math.pi


def _clamp(x, a=0.0, b=1.0):
    return max(a, min(b, x))


def _smooth(x):
    x = _clamp(x)
    return x * x * (3 - 2 * x)


def _hex(c):
    c = c.lstrip('#')
    return tuple(int(c[i:i + 2], 16) for i in (0, 2, 4))


# ------------------------------------------------------------ materials

SHIP_COLORS = {
    'wood': '#7a603c', 'wood_dark': '#45341f', 'wood_light': '#a08860', 'deck': '#a89064',
    'rail': '#5a4428', 'sail': '#e6d8ac', 'rope': '#3a2e20', 'gold': '#c8963c',
    'iron': '#5c6064', 'steel': '#80868c', 'steel_dark': '#4c5258', 'steel_light': '#a4aab0',
    'deck_steel': '#8e8a80', 'deck_wood': '#a08a64', 'dark': '#2a2e32', 'glass': '#2c3a48',
    'bottom': '#7a3a2c', 'black': '#26282a', 'white': '#e8e8e4', 'bronze': '#b07a38',
    'foam': '#f4f8fa', 'wake': '#e2f0f4',
    'smoke': '#c4c0b8', 'smoke_dark': '#4c4844', 'smoke_mid': '#8a8580',
    'flash': '#fff4c0', 'fire': '#ff8a2a', 'fire_core': '#ffd878',
    'tint': '#f0f0f0', 'sail_tint': '#ffffff',
}
# effect materials: no ground shadow, and foam/wake drawn in their own passes
FX_MATS = {'smoke', 'smoke_dark', 'smoke_mid', 'flash', 'fire', 'fire_core', 'arrow', 'missile_trail'}
SMOKE_MATS = {'smoke', 'smoke_dark', 'smoke_mid'}   # drawn translucent over the ship
SMOKE_ALPHA = 0.82
SOFT_MATS = {'wake'}
FOAM_MATS = {'foam'}
METALS = {'gold', 'iron', 'steel', 'steel_dark', 'steel_light', 'bronze', 'dark'}
GLOW = {'flash': (1.0, 1.5), 'fire': (0.9, 2.0), 'fire_core': (1.0, 1.5)}


def make_mats(colors, tint):
    mats = M.Materials()
    cols = dict(SHIP_COLORS)
    cols.update(colors or {})
    if 'plank' not in cols:
        w = np.array(_hex(cols['wood']) if isinstance(cols['wood'], str) else cols['wood'], np.float32)
        cols['plank'] = tuple(int(v) for v in np.clip(w * 0.84, 0, 255))
    if 'plate' not in cols:
        w = np.array(_hex(cols['steel']) if isinstance(cols['steel'], str) else cols['steel'], np.float32)
        cols['plate'] = tuple(int(v) for v in np.clip(w * 0.88, 0, 255))
    for name, c in cols.items():
        spec, gloss, metal = 0.06, 12.0, False
        if name in METALS:
            spec, gloss, metal = 0.35, 24.0, True
        if name in GLOW:
            spec, gloss = GLOW[name]
        if name in ('steel', 'steel_dark', 'steel_light', 'deck_steel'):
            spec, gloss, metal = 0.18, 18.0, False
        mats.add(name, c, tint=(name in tint or name in ('tint', 'sail_tint')), spec=spec, gloss=gloss, metal=metal)
    return mats


def mat(mats, name, default='#808080', tint=False):
    if name not in mats:
        mats.add(name, default, tint=tint)
    return mats[name]


# ------------------------------------------------------------ geometry helpers

def clip_below(mesh, z0=0.0):
    """The part of a mesh above the plane z = z0 (triangles cut exactly)."""
    if not len(mesh):
        return mesh
    V, N, Mt = mesh.V, mesh.N, mesh.M
    below = V[..., 2] < z0
    cnt = below.sum(1)
    out_V, out_N, out_M = [V[cnt == 0]], [N[cnt == 0]], [Mt[cnt == 0]]
    for c in (1, 2):
        sel = np.nonzero(cnt == c)[0]
        if not len(sel):
            continue
        b = below[sel]
        # the odd vertex: the one below (c == 1) or the one above (c == 2)
        odd = np.argmax(b if c == 1 else ~b, axis=1)
        idx = (odd[:, None] + np.arange(3)[None]) % 3
        v = np.take_along_axis(V[sel], idx[..., None], 1)
        n = np.take_along_axis(N[sel], idx[..., None], 1)
        A, B, C = v[:, 0], v[:, 1], v[:, 2]
        nA, nB, nC = n[:, 0], n[:, 1], n[:, 2]
        tb = ((z0 - A[:, 2]) / (B[:, 2] - A[:, 2] + 1e-12))[:, None]
        tc = ((z0 - A[:, 2]) / (C[:, 2] - A[:, 2] + 1e-12))[:, None]
        AB, AC = A + (B - A) * tb, A + (C - A) * tc
        nAB, nAC = nA + (nB - nA) * tb, nA + (nC - nA) * tc
        m = Mt[sel]
        if c == 1:   # A below: quad AB B C AC
            out_V += [np.stack([AB, B, C], 1), np.stack([AB, C, AC], 1)]
            out_N += [np.stack([nAB, nB, nC], 1), np.stack([nAB, nC, nAC], 1)]
            out_M += [m, m]
        else:        # A above
            out_V.append(np.stack([A, AB, AC], 1))
            out_N.append(np.stack([nA, nAB, nAC], 1))
            out_M.append(m)
    return M.Mesh(np.concatenate(out_V), np.concatenate(out_N), np.concatenate(out_M))


def split_long(mesh, cam, max_px=8.0, rounds=8):
    """Splits triangles whose longest screen edge is over max_px (sheet
    pixels) at that edge's midpoint. The renderer bins triangles by their
    screen bounding box and scans a square tile per triangle, so long thin
    triangles (masts, rigging, long decks) are very costly; short pieces are
    cheap and look the same."""
    V, N, Mt = mesh.V, mesh.N, mesh.M
    for _ in range(rounds):
        S = cam.project(V)[..., :2]
        e = np.stack([np.linalg.norm(S[:, (k + 1) % 3] - S[:, k], axis=-1) for k in range(3)], 1)
        big = e.max(1) > max_px
        if not big.any():
            break
        keepV, keepN, keepM = V[~big], N[~big], Mt[~big]
        v, n, m = V[big], N[big], Mt[big]
        k = np.argmax(e[big], 1)
        idx = (k[:, None] + np.arange(3)[None]) % 3      # longest edge is (0, 1)
        v = np.take_along_axis(v, idx[..., None], 1)
        n = np.take_along_axis(n, idx[..., None], 1)
        mid = (v[:, 0] + v[:, 1]) / 2
        nm = n[:, 0] + n[:, 1]
        nm /= np.linalg.norm(nm, axis=-1, keepdims=True) + 1e-12
        A = np.stack([v[:, 0], mid, v[:, 2]], 1)
        B = np.stack([mid, v[:, 1], v[:, 2]], 1)
        nA = np.stack([n[:, 0], nm, n[:, 2]], 1)
        nB = np.stack([nm, n[:, 1], n[:, 2]], 1)
        V = np.concatenate([keepV, A, B])
        N = np.concatenate([keepN, nA, nB])
        Mt = np.concatenate([keepM, m, m])
    return M.Mesh(V, N, Mt)


def subset(mesh, keep):
    return M.Mesh(mesh.V[keep], mesh.N[keep], mesh.M[keep])


def grid_mesh(P, mat_ids, smooth=True):
    """A Mesh from a (rows, cols, 3) grid of points; mat_ids: an int or a
    (rows-1, cols-1) array of per-quad materials."""
    R_, C_ = P.shape[:2]
    faces = M.grid_faces(R_ - 1, C_, closed_u=False)
    m = M.from_indexed(P.reshape(-1, 3), faces, 0, smooth=smooth)
    if np.ndim(mat_ids) == 0:
        m.M[:] = int(mat_ids)
    else:
        q = np.asarray(mat_ids, np.int32).reshape(-1)
        m.M[:] = np.repeat(q, 2)
    return m


def rod(a, b, r, mat_id, seg=6, r1=None):
    """A cylinder from a to b."""
    a, b = np.asarray(a, np.float32), np.asarray(b, np.float32)
    L = float(np.linalg.norm(b - a))
    if L < 1e-6:
        return M.Mesh()
    c = M.cylinder(r, r if r1 is None else r1, 1.0, mat_id, seg=seg)
    return c.transformed(M.look_along(a, a + (b - a) / L) @ M.scale(1, 1, L))


def box(c, s, mat_id, bevel=0.0):
    if bevel > 0:
        return M.rounded_box(s[0], s[1], s[2], mat_id, center=c, r=bevel, seg=12)
    return M.box(s[0], s[1], s[2], mat_id, center=c)


def lathe_at(profile, mat_id, pos, seg=12):
    return M.lathe(profile, mat_id, seg).transformed(M.translate(*pos))


def patch(corners, nu, nv, mat_fn, offset_fn=None):
    """A bilinear patch over 4 corners (top-left, top-right, bottom-right,
    bottom-left), (nu x nv) quads; mat_fn(u, v) -> material id per quad
    (u across, v down, at the quad's center); offset_fn(u, v) -> (.., 3)
    displacement (billow, wave)."""
    A, B, C, D = [np.asarray(c, np.float32) for c in corners]
    u = np.linspace(0, 1, nu + 1, dtype=np.float32)
    v = np.linspace(0, 1, nv + 1, dtype=np.float32)
    U, Vv = np.meshgrid(u, v)
    P = ((1 - Vv)[..., None] * ((1 - U)[..., None] * A + U[..., None] * B)
         + Vv[..., None] * ((1 - U)[..., None] * D + U[..., None] * C))
    if offset_fn is not None:
        P = P + offset_fn(U, Vv)
    uc = (np.arange(nu) + 0.5) / nu
    vc = (np.arange(nv) + 0.5) / nv
    UC, VC = np.meshgrid(uc, vc)
    mids = np.vectorize(mat_fn)(UC, VC) if mat_fn is not None else 0
    return grid_mesh(P, mids)


def lit_cloth(m, eps=0.003, up=0.62):
    """Canvas lit like Civ3's sails: the renderer turns shading normals to the
    camera, so a camera-facing sail is lit only by the fill and ambient (the
    key light comes from behind). Sails become two layers, eps apart, whose
    normals lean up and out to their own side, so whichever side faces the
    camera catches the sky and the key light (like translucent canvas)."""
    if not len(m):
        return m
    g = np.cross(m.V[:, 1] - m.V[:, 0], m.V[:, 2] - m.V[:, 0])
    g /= np.linalg.norm(g, axis=-1, keepdims=True) + 1e-12
    n = m.N * np.sign((m.N * g[:, None]).sum(-1, keepdims=True) + 1e-9)
    U = np.array([0, 0, 1], np.float32)
    a = math.sqrt(max(1e-6, 1 - up * up))
    out = []
    for sgn in (1, -1):
        N = sgn * n * a + U * up
        N /= np.linalg.norm(N, axis=-1, keepdims=True) + 1e-12
        out.append(M.Mesh(m.V + sgn * n * eps, N, m.M.copy()))
    return M.Mesh.concat(out)


def disc_flat(cx, cy, z, r, mat_id, seg=7, rot=0.0, sx=1.0, sy=1.0):
    """A flat horizontal polygon (foam flecks, wake)."""
    a = np.linspace(0, TAU, seg, endpoint=False) + rot
    pts = np.stack([cx + r * sx * np.cos(a), cy + r * sy * np.sin(a), np.full(seg, z)], -1)
    c = np.array([cx, cy, z], np.float32)
    V = np.stack([np.repeat(c[None], seg, 0), pts, np.roll(pts, -1, 0)], 1).astype(np.float32)
    N = np.zeros_like(V)
    N[..., 2] = 1
    return M.Mesh(V, N, np.full(seg, mat_id, np.int32))


def blob(center, r, mat_id, seg=10, squash=1.0):
    return M.ellipsoid(r, r, r * squash, mat_id, center=center, seg=seg)


# ------------------------------------------------------------ hull

class Hull:
    """A lofted hull: stations from the stern (s = 0) to the bow (s = 1).

    L length, B beam, D freeboard (deck height above the waterline amidships),
    draft (keel depth), hb bulwark height above the deck, bt bulwark
    thickness, sheer_bow/sheer_stern (deck rise at the ends), bow_p/stern_p
    (fullness: larger = fuller), transom (stern width as a fraction of the
    beam; 0 = pointed), rake_bow/rake_stern (overhang of the stem/stern at the
    rail, m), n (section squareness: 2 round, 4 boxy), flare (rail wider than
    the waterline), stripes [(d0, d1, material)] by depth below the rail,
    boot (material below the waterline)."""

    def __init__(self, mats, L, B, D, draft=0.12, hb=0.03, bt=0.015, sheer_bow=0.0, sheer_stern=0.0,
                 bow_p=1.8, stern_p=1.8, bow_e=0.65, stern_e=0.65, transom=0.0, rake_bow=0.0, rake_stern=0.0,
                 n=2.4, flare=0.0, mid=0.0, camber=0.01, stations=34, side='wood', deck='deck', rail='rail',
                 inner=None, stripes=(), boot='bottom', y0=0.0, transom_mat=None, bulwark=True, bands=(), planks=None):
        self.mats = mats
        self.L, self.B, self.D, self.draft = L, B, D, draft
        self.hb, self.bt = hb, bt
        self.sheer_bow, self.sheer_stern = sheer_bow, sheer_stern
        self.bow_p, self.stern_p, self.bow_e, self.stern_e = bow_p, stern_p, bow_e, stern_e
        self.transom, self.rake_bow, self.rake_stern = transom, rake_bow, rake_stern
        self.n, self.flare, self.mid, self.camber = n, flare, mid, camber
        self.stations = stations
        self.side, self.deck, self.rail = side, deck, rail
        self.inner = inner or rail
        self.stripes = list(stripes)
        self.boot = boot
        self.y0 = y0
        self.transom_mat = transom_mat or side
        self.bulwark = bulwark
        self.bands = list(bands)   # [(z0, z1, material)] at absolute heights
        # > 0: alternate 'plank' bands of this height (m); wooden hulls by default
        self.planks = (0.026 if side == 'wood' else 0.0) if planks is None else planks

    # profile functions of s in [0, 1]
    def _u(self, s):
        return 2 * s - 1

    def half_beam(self, s):
        u = self._u(s)
        a = max(0.0, (abs(u) - self.mid) / (1 - self.mid)) if self.mid < 1 else 0.0
        if u >= 0:
            f = (1 - a ** self.bow_p) ** self.bow_e if a < 1 else 0.0
        else:
            f = (1 - a ** self.stern_p) ** self.stern_e if a < 1 else 0.0
            f = self.transom + (1 - self.transom) * f
        return self.B / 2 * f

    def deck_z(self, s):
        u = self._u(s)
        return self.D + self.sheer_bow * max(u, 0) ** 2 + self.sheer_stern * max(-u, 0) ** 2

    def rail_z(self, s):
        return self.deck_z(s) + self.hb

    def keel_z(self, s):
        u = abs(self._u(s))
        return -self.draft * (1 - u ** 4)

    def y_of(self, s):
        return self.y0 - self.L / 2 + s * self.L

    def s_of(self, y):
        return _clamp((y - self.y0 + self.L / 2) / self.L)

    def _rake(self, s, z):
        u = self._u(s)
        zr = max(self.rail_z(s), 1e-3)
        h = z / zr
        dy = 0.0
        if u > 0:
            dy += self.rake_bow * u ** 6 * h
        else:
            dy -= self.rake_stern * u ** 6 * h
        return dy

    def section(self, s, k=18):
        """Outer section points (x >= 0 side) from the rail down to the keel."""
        b = self.half_beam(s)
        zr, zk = self.rail_z(s), self.keel_z(s)
        th = np.linspace(0, math.pi / 2, k)
        e = 2.0 / self.n
        c, si = np.cos(th) ** e, np.sin(th) ** e
        z = zr - (zr - zk) * si
        # flare: the waterline is narrower than the rail
        w = b * (1 - self.flare * np.clip((zr - z) / max(zr, 1e-3), 0, 1))
        x = w * c
        return x, z

    def side_x(self, y, z):
        """x of the outer (starboard) surface at (y, z)."""
        s = self.s_of(y)
        x, zz = self.section(s, 40)
        return float(np.interp(z, zz[::-1], x[::-1]))

    def waterline(self, n=48):
        """Closed waterline outline [(x, y), ...] (starboard stern->bow, port bow->stern)."""
        ss = 0.5 - 0.5 * np.cos(np.linspace(0, math.pi, n))
        pts = []
        for s in ss:
            y = self.y_of(s)
            x, z = self.section(s, 40)
            xw = float(np.interp(0.0, z[::-1], x[::-1])) if z.min() < 0 < z.max() else 0.0
            pts.append((xw, y + self._rake(s, 0.0)))
        star = np.array(pts, np.float32)
        port = star[::-1].copy()
        port[:, 0] *= -1
        return np.concatenate([star, port[1:-1]])

    def build(self):
        mats = self.mats
        ns = self.stations
        ss = 0.5 - 0.5 * np.cos(np.linspace(0, math.pi, ns))
        K = 18
        outer = np.zeros((ns, 2 * K - 1, 3), np.float32)
        rail = np.zeros((ns, 4, 3), np.float32)
        inner_s = np.zeros((ns, 2, 3), np.float32)
        inner_p = np.zeros((ns, 2, 3), np.float32)
        deck = np.zeros((ns, 7, 3), np.float32)
        for i, s in enumerate(ss):
            y = self.y_of(s)
            x, z = self.section(s, K)
            xs = np.concatenate([-x[:-1], x[::-1]])  # port rail -> keel -> starboard rail
            zs = np.concatenate([z[:-1], z[::-1]])
            for j in range(2 * K - 1):
                outer[i, j] = (xs[j], y + self._rake(s, zs[j]), zs[j])
            b = x[0]
            bi = max(b - self.bt, 0.0)
            zr, zd = self.rail_z(s), self.deck_z(s)
            ry = y + self._rake(s, zr)
            dy = y + self._rake(s, zd)
            rail[i] = [(-b, ry, zr), (-bi, ry, zr), (bi, ry, zr), (b, ry, zr)]
            inner_s[i] = [(bi, ry, zr), (bi, dy, zd)]
            inner_p[i] = [(-bi, ry, zr), (-bi, dy, zd)]
            xd = np.linspace(-bi, bi, 7)
            for j, xx in enumerate(xd):
                cz = self.camber * (1 - (xx / max(bi, 1e-4)) ** 2) if bi > 1e-4 else 0
                deck[i, j] = (xx, dy, zd + cz)
        # outer materials: boot below the waterline, stripes by depth below the rail
        side = mats[self.side]
        q = np.full((ns - 1, 2 * K - 2), side, np.int32)
        cz = (outer[:-1, :-1, 2] + outer[1:, 1:, 2] + outer[:-1, 1:, 2] + outer[1:, :-1, 2]) / 4
        zr = np.array([self.rail_z(s) for s in ss], np.float32)
        zrq = (zr[:-1] + zr[1:])[:, None] / 2
        d = zrq - cz
        pm = 'plank' if self.side == 'wood' else 'plate'
        if self.planks > 0 and pm in mats:
            q[(np.floor(d / self.planks) % 2 == 1) & (cz > 0) & (q == side)] = mats[pm]
        for d0, d1, mname in self.stripes:
            q[(d >= d0) & (d < d1)] = mats[mname]
        for z0, z1, mname in self.bands:
            q[(cz >= z0) & (cz < z1)] = mats[mname]
        if self.boot:
            q[cz < -0.004] = mats[self.boot]
        parts = [grid_mesh(outer, q)]
        if self.bulwark and self.hb > 0:
            # the rail caps: two strips along the sides (not across the deck)
            parts.append(grid_mesh(np.ascontiguousarray(rail[:, :2]), mats[self.rail], smooth=False))
            parts.append(grid_mesh(np.ascontiguousarray(rail[:, 2:]), mats[self.rail], smooth=False))
            parts.append(grid_mesh(inner_s, mats[self.inner]))
            parts.append(grid_mesh(inner_p, mats[self.inner]))
        parts.append(grid_mesh(deck, mats[self.deck]))
        if self.transom > 0:
            # close the stern: a fan over the stern section
            o = outer[0]
            c = o.mean(0)
            V = np.stack([np.repeat(c[None], len(o) - 1, 0), o[:-1], o[1:]], 1)
            # top edge closes the deck
            V = np.concatenate([V, [[c, o[-1], o[0]]]])
            fn = np.cross(V[:, 1] - V[:, 0], V[:, 2] - V[:, 0])
            fn /= np.linalg.norm(fn, axis=-1, keepdims=True) + 1e-9
            parts.append(M.Mesh(V, np.repeat(fn[:, None], 3, 1), np.full(len(V), mats[self.transom_mat], np.int32)))
        return M.Mesh.concat(parts)


# ------------------------------------------------------------ sail emblems

def emblem(kind, aspect=1.0):
    """A function (x, y) -> bool for the civ-colored parts of a sail; x, y in
    [-1, 1] across and down the sail (y down). `aspect` is the sail's
    width / height: shapes are drawn in half-height units (X) so they keep
    their proportions on wide sails; edge bands use x."""
    def f(x, y):
        X, Y = x * aspect, y
        r = math.hypot(X, Y)
        if kind == 'bands':
            return abs(x) > 0.8
        if kind == 'pisces':   # the galley: two crescents and a bar, edge bands
            if abs(x) > 0.87:
                return True
            for sgn in (-1, 1):
                cx = sgn * 0.78
                rr = math.hypot(X - cx, Y * 0.95)
                if abs(rr - 0.6) < 0.17 and sgn * (X - cx) < 0.2:
                    return True
            return abs(Y) < 0.16 and abs(X) < 0.6
        if kind == 'cross':
            return (abs(X) < 0.14 or abs(Y) < 0.14) and r < 0.66
        if kind == 'curragh':   # a ringed cross
            return ((abs(X) < 0.13 or abs(Y) < 0.13) and r < 0.66) or abs(r - 0.42) < 0.09
        if kind == 'pattee':   # a cross pattee
            ax, ay = abs(X), abs(Y)
            return ((ax < 0.08 + 0.45 * ay) and ay < 0.58) or ((ay < 0.08 + 0.45 * ax) and ax < 0.58)
        if kind == 'flame':    # the dromon
            if abs(X) > 0.66:
                return False
            top = -0.7 + 0.45 * abs(math.sin(X * 5.0)) + 0.45 * abs(X)
            return top < Y < 0.7 - 0.25 * abs(X)
        if kind == 'cross_square':  # the carrack: a square with a white cross inside
            m = max(abs(X), abs(Y))
            return m < 0.58 and not (abs(X) < 0.08 or abs(Y) < 0.08)
        if kind == 'stripes':
            return int((x + 1) / 2 * 5) % 2 == 1
        if kind == 'quatrefoil':
            for cx, cy in ((0.34, 0), (-0.34, 0), (0, 0.34), (0, -0.34)):
                if math.hypot(X - cx, Y - cy) < 0.31:
                    return r > 0.12
            return False
        if kind == 'skull':
            sk = math.hypot(X, (Y + 0.2) * 1.05) < 0.36 or (abs(X) < 0.2 and -0.05 < Y < 0.24)
            eyes = any(math.hypot(X - ex, Y + 0.22) < 0.1 for ex in (-0.15, 0.15))
            bones = 0.2 < Y < 0.75 and (abs(X - (Y - 0.47) * 1.7) < 0.11 or abs(X + (Y - 0.47) * 1.7) < 0.11)
            return (sk and not eyes) or bones
        if kind == 'crest':    # a heraldic shield blot (man-o-war)
            shield = abs(X) < 0.5 and -0.55 < Y < 0.25 + 0.4 * (1 - abs(X) / 0.5)
            hole = math.hypot(X, Y + 0.05) < 0.13
            return shield and not hole
        if kind == 'band_bottom':
            return y > 0.55
        if kind == 'all':
            return True
        return False
    return f


def sail_mats(mats, kind, aspect=1.0, base='sail', tint='sail_tint'):
    f = emblem(kind, aspect)
    b, t = mats[base], mats[tint]
    return lambda u, v: t if f(2 * u - 1, 2 * v - 1) else b


# ------------------------------------------------------------ builder

class Turret:
    def __init__(self, pos, mesh_base, mesh_gun, pivot, rest=0.0, aim=0.0, muzzles=(), delay=0.0,
                 recoil=0.05, flash=1.0, elev=6.0):
        self.pos = np.asarray(pos, np.float32)
        self.base, self.gun = mesh_base, mesh_gun
        self.pivot = np.asarray(pivot, np.float32)   # gun trunnion in turret space
        self.rest, self.aim = rest, aim
        self.muzzles = [np.asarray(m, np.float32) for m in muzzles]  # in gun space (barrels along +y)
        self.delay, self.recoil, self.flash, self.elev = delay, recoil, flash, elev
        self.fixed = False

    def pose(self, yaw, elev=0.0, rec=0.0):
        T = M.translate(*self.pos) @ M.rot_z(yaw)
        G = T @ M.translate(*self.pivot) @ M.rot_x(elev) @ M.translate(0, -rec, 0)
        return M.Mesh.concat([self.base.transformed(T), self.gun.transformed(G)]), G


class ShipBuilder:
    """Collects a design's parts. Designs call these helpers."""

    def __init__(self, mats, params):
        self.mats = mats
        self.p = params
        self.static = []
        self.sails = []      # dicts: corners, nu, nv, mat_fn, billow dir, amount
        self.flags = []      # (pos, w, h, phase)
        self.oars = None
        self.turrets = []
        self.guns = []       # (muzzle position, direction, delay, size)
        self.hull = None
        self.weapon = None   # dict(kind=..., ...)
        self.smoke_stacks = []  # funnel tops for idle smoke
        self.spinners = []   # (mesh, pos, axis z, rpm-ish) radars
        self.extra_wake = 1.0

    def m(self, name, default='#808080', tint=False):
        return mat(self.mats, name, default, tint)

    def add(self, mesh):
        self.static.append(mesh)
        return mesh

    def set_hull(self, hull):
        self.hull = hull
        self.add(hull.build())
        return hull

    # rigging -------------------------------------------------------
    def mast(self, y, z0, h, r=0.022, mat_name='wood_dark', x=0.0, rake=0.0, top=None):
        h *= self.p.get('rig_h', 1.0)
        a = (x, y, z0)
        b = (x, y - rake * h, z0 + h)
        self.add(rod(a, b, r, self.m(mat_name), seg=8, r1=r * 0.7))
        if top == 'finial':
            self.add(M.sphere(r * 1.9, self.m('gold'), center=b, seg=10))
        elif top == 'crow':
            self.add(M.cylinder(r * 3.2, r * 3.6, r * 3.0, self.m('wood'), seg=10, z0=0).transformed(
                M.translate(b[0], b[1], b[2] - h * 0.18)))
        return np.array(b, np.float32)

    def yard(self, y, z, w, r=0.012, mat_name='wood_dark', x=0.0, tilt=0.0):
        a = (x - w / 2, y, z - tilt)
        b = (x + w / 2, y, z + tilt)
        self.add(rod(a, b, r, self.m(mat_name), seg=6, r1=r))

    def line(self, a, b, r=0.005, mat_name='rope'):
        self.add(rod(a, b, r, self.m(mat_name), seg=4))

    def square_sail(self, y, z_top, w_top, w_bot, h, emblem_kind='none', billow=0.12, x=0.0, nu=14, nv=12,
                    yard=True, base='sail', tint='sail_tint', foot_fwd=0.03, brace=None):
        # Braced yards (catalog 'brace', degrees): the sail turns about the
        # mast so it still shows its face in the side views (E/W), like the
        # originals, instead of going edge-on.
        br = math.radians(self.p.get('brace', 0.0) if brace is None else brace)
        ca, sa = math.cos(br), math.sin(br)

        def rot(px, pz):
            dx = px - x
            return (x + dx * ca, y + dx * sa, pz)
        if yard:
            a, e = rot(x - w_top * 0.54, z_top + 0.01), rot(x + w_top * 0.54, z_top + 0.01)
            self.add(rod(a, e, 0.012, self.m('wood_dark'), seg=6, r1=0.012))
        corners = [rot(x - w_top / 2, z_top), rot(x + w_top / 2, z_top),
                   rot(x + w_bot / 2, z_top - h), rot(x - w_bot / 2, z_top - h)]
        self.sails.append(dict(kind='square', corners=corners, nu=nu, nv=nv,
                               mat_fn=sail_mats(self.mats, emblem_kind, w_top / max(h, 1e-3), base, tint),
                               dir=np.array([-sa, ca, 0], np.float32), amount=billow * self.p.get('billow_k', 1.0), h=h, w=w_top, foot=foot_fwd))

    def fore_aft_sail(self, tack, head, clew, peak=None, emblem_kind='none', billow=0.06, nu=10, nv=10,
                      base='sail', tint='sail_tint', side=1.0):
        """A triangular (or 4-sided with peak) fore-and-aft sail: lateen, jib, spanker.
        Corners: head (top front), peak (top back), clew (bottom back), tack (bottom front)."""
        peak = head if peak is None else peak
        corners = [head, peak, clew, tack]
        self.sails.append(dict(kind='fa', corners=corners, nu=nu, nv=nv,
                               mat_fn=sail_mats(self.mats, emblem_kind, 1.0, base, tint),
                               dir=np.array([side, 0, 0], np.float32), amount=billow))

    def flag(self, pos, w=0.14, h=0.09, phase=0.0, mat_name='tint', pole=True, droop=0.0):
        pos = np.asarray(pos, np.float32)
        if pole:
            self.add(rod(pos - (0, 0, h * 1.1), pos + (0, 0, 0.01), 0.006, self.m('wood_dark'), seg=4))
        self.flags.append((pos, w, h, phase, self.mats[mat_name], droop))

    def set_oars(self, n, y0, y1, length, z=None, r=0.011, mat_name='wood_light', spread=1.0, out=0.45,
                 dip=58.0):
        self.oars = dict(n=n, y0=y0, y1=y1, L=length, z=z, r=r, mat=self.m(mat_name), out=out, dip=dip)

    def turret(self, pos, kind='twin', size=0.1, barrel=0.18, rest=0.0, aim=0.0, delay=0.0, n=2,
               mat_name='steel', gun_mat='steel_dark', h=None, recoil=None, elev=6.0, flash=1.0, shape='round'):
        mt = self.m(mat_name)
        mg = self.m(gun_mat)
        h = h if h is not None else size * 0.45
        if shape == 'round':
            base = M.lathe([(0, size * 0.55), (h * 0.85, size * 0.52), (h, size * 0.38), (h + 0.001, 0.0)], mt, seg=14)
            base = base + M.rounded_box(size * 1.05, size * 0.9, h * 0.9, mt, center=(0, size * 0.1, h * 0.45),
                                        r=h * 0.25, seg=12)
        elif shape == 'drum':   # monitor turret
            base = M.cylinder(size * 0.5, size * 0.5, h, mt, seg=16) + \
                M.cylinder(size * 0.5, 0.0005, 0.001, mt, seg=16, z0=h)
        else:                   # 'box' gun house
            base = M.rounded_box(size * 0.9, size * 1.0, h, mt, center=(0, 0, h / 2), r=h * 0.2, seg=12)
        gun = []
        muz = []
        piv = (0, size * 0.25 if shape != 'drum' else size * 0.45, h * 0.55)
        offs = np.linspace(-1, 1, n) * size * 0.2 if n > 1 else [0.0]
        for ox in offs:
            gun.append(rod((ox, 0, 0), (ox, barrel, 0), size * 0.07, mg, seg=6, r1=size * 0.055))
            muz.append((ox, barrel + 0.01, 0))
        t = Turret(pos, base, M.Mesh.concat(gun), piv, rest, aim, muz, delay,
                   recoil if recoil is not None else barrel * 0.22, flash, elev)
        self.turrets.append(t)
        return t

    def gun(self, pos, direction, delay=0.0, size=1.0):
        d = np.asarray(direction, np.float32)
        self.guns.append((np.asarray(pos, np.float32), d / np.linalg.norm(d), delay, size))

    def spinner(self, mesh, pos, speed=1.0):
        self.spinners.append((mesh, np.asarray(pos, np.float32), speed))


# ------------------------------------------------------------ render style

class ShipStyle(R.Style):
    ground_ao = 0.12
    saturation = 1.0        # no boost: ships stay as muted as the originals


class FxStyle(R.Style):
    contour_alpha = 0.0
    ssao = 0.0
    ground_ao = 0.0
    inner_line = 0.0
    rim = 0.15


class SmokeStyle(FxStyle):
    ambient = 0.45
    key = 0.7
    rim = 0.1


SHADOW_ALPHA = 0.17
SHADOW_BLUR = 2.4
WAKE_ALPHA = 0.42
FOAM_ALPHA = 0.88


def _over(top, bot):
    """Straight-alpha 'over' of float RGBA arrays."""
    ta, ba = top[..., 3:4], bot[..., 3:4]
    a = ta + ba * (1 - ta)
    c = (top[..., :3] * ta + bot[..., :3] * ba * (1 - ta)) / np.maximum(a, 1e-6)
    return np.concatenate([c, a], -1)


def _f(a):
    return a.astype(np.float32) / 255.0


def soft_shadow(mesh, cam, W, H, ss=2):
    """A soft sun shadow of a mesh on the water (alpha HxW)."""
    out = np.zeros((H, W), np.float32)
    if not len(mesh):
        return out
    Vs = R._shadow_verts(mesh.V)
    P = cam.project(Vs).reshape(-1, 3)
    m = 8
    x0 = int(max(0, math.floor(P[:, 0].min()) - m)); x1 = int(min(W, math.ceil(P[:, 0].max()) + m))
    y0 = int(max(0, math.floor(P[:, 1].min()) - m)); y1 = int(min(H, math.ceil(P[:, 1].max()) + m))
    if x1 <= x0 or y1 <= y0:
        return out
    sub = R.Camera(cam.ppm * ss, ((cam.anchor[0] - x0) * ss, (cam.anchor[1] - y0) * ss))
    cov = R.coverage(sub.project(Vs), (x1 - x0) * ss, (y1 - y0) * ss).astype(np.float32)
    sh = R._downsample(cov, ss)
    sh = np.clip(R._blur(sh, SHADOW_BLUR), 0, 1) * SHADOW_ALPHA
    yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float32)
    d = np.minimum(np.minimum(xx + 0.5, W - xx - 0.5), np.minimum(yy + 0.5, H - yy - 0.5))
    sh *= np.clip(d / 6.0, 0, 1) ** 1.5
    out[y0:y1, x0:x1] = sh
    return out


# ------------------------------------------------------------ the archetype

LOOPS = {'DEFAULT', 'RUN', 'FIDGET', 'FORTIFY', 'WALK'}


@archetype('ship')
class Ship:
    def __init__(self, unit, params):
        self.unit = unit
        self.p = params
        from models import ship_designs
        tint = set(params.get('tint', []))
        self.mats = make_mats(params.get('colors'), tint)
        for name in ('arrow',):
            mat(self.mats, name, '#e8dcb4')
        mat(self.mats, 'missile_trail', '#e8e4dc')
        b = ShipBuilder(self.mats, params)
        ship_designs.DESIGNS[params['design']](b, params)
        self.b = b
        self.static = M.Mesh.concat(b.static)
        self.S = M.scale(params.get('beam_k', 1.0), 1.0, params.get('height_k', 1.0))
        self.waterline = b.hull.waterline() if b.hull else None
        if self.waterline is not None:
            self.waterline = self.waterline * np.array([params.get('beam_k', 1.0), 1.0], np.float32)
        self.length = float(np.ptp(self.static.V[..., 1])) if len(self.static) else 1.0
        self.fx_ids = {self.mats[n] for n in FX_MATS if n in self.mats}
        self.soft_ids = {self.mats[n] for n in SOFT_MATS if n in self.mats}
        self.foam_ids = {self.mats[n] for n in FOAM_MATS if n in self.mats}
        self.smoke_ids = {self.mats[n] for n in SMOKE_MATS if n in self.mats}
        self._calib = None
        self._calibrate(params)
        # effects scale with the ship's size (and its fitted scale, so they read the same on screen)
        self.fx_k = (self.length / 3.0) ** 0.8 * (25.0 / max(self.target_ppm, 1.0)) ** 0.3

    # ---------------------------------------------------- calibration
    def _calibrate(self, params):
        """Fit the model's east/west silhouette to the original's (width and
        waterline), see the module docstring."""
        import flic as F
        name = F.unit_flics(self.unit)['DEFAULT']
        fl = F.load_unit_flic(self.unit, name)
        rest = clip_below(self.ship_mesh('DEFAULT', 0.0, rest=True).transformed(self.S))
        ps, axs, ays = [], [], []
        for d in (3, 7):
            idx = fl.frames[d, 0]
            mk = ~F.shadow_mask(idx) & (idx != 255)
            ys, xs = np.nonzero(mk)
            if not len(xs):
                continue
            ow = xs.max() - xs.min() + 1
            ocx = (xs.max() + xs.min() + 1) / 2 + fl.offset_left
            oby = ys.max() + fl.offset_top
            S = R.Camera(1.0, (0, 0)).project(rest.transformed(R.facing_matrix(d)).V)
            mw = S[..., 0].max() - S[..., 0].min()
            p = (ow - 1.0) / mw
            ps.append(p)
            axs.append(ocx - (S[..., 0].max() + S[..., 0].min()) / 2 * p)
            ays.append(oby + 0.5 - S[..., 1].max() * p)
        p = float(np.mean(ps))
        self.target_ppm = p     # 1x pixels per model meter
        if 'anchor' not in params:
            params['anchor'] = (float(np.mean(axs)), float(np.mean(ays)))
        self.anchor = params['anchor']

    def _calib_mesh(self):
        if self._calib is None:
            import framework
            oh = framework.original_stance(self.unit)[2]
            h = (oh - 0.5) / (R.COS_E * self.target_ppm)
            self._calib = M.box(0.002, 0.002, h, 0, center=(0, 0, h / 2))
        return self._calib

    def loops(self, action):
        return action in LOOPS

    # ---------------------------------------------------- ship parts
    def ship_mesh(self, action, t, rest=False, billow=1.0, wind=0.0, oar=None, turret_pose=None, flag_k=1.0,
                  spin=0.0):
        """The ship in its own frame (not posed on the water)."""
        b = self.b
        parts = [self.static]
        for sd in b.sails:
            parts.append(self._sail(sd, billow, wind, t))
        for (pos, w, h, ph, mid, droop) in b.flags:
            parts.append(self._flag(pos, w, h, ph, mid, t, flag_k, droop))
        if b.oars:
            parts.append(self._oars(oar if oar is not None else (0.0, 0.0)))
        tp = turret_pose or {}
        self._gun_frames = []
        for i, tr in enumerate(b.turrets):
            yaw, el, rec = tp.get(i, (tr.rest, 0.0, 0.0))
            m, G = tr.pose(yaw, el, rec)
            parts.append(m)
            self._gun_frames.append(G)
        for mesh, pos, speed in b.spinners:
            parts.append(mesh.transformed(M.translate(*pos) @ M.rot_z(spin * 360 * speed)))
        return M.Mesh.concat(parts)

    def _sail(self, sd, billow, wind, t):
        amt = sd['amount'] * billow
        d = sd['dir']
        if sd['kind'] == 'square':
            foot = sd.get('foot', 0.03) * billow

            def off(U, V):
                bell = (4 * U * (1 - U)) ** 0.7
                prof = 0.35 + 0.65 * np.sin(np.pi * np.clip(V * 0.9 + 0.05, 0, 1))
                k = amt * bell * prof + foot * V ** 1.5 + wind * 0.02 * np.sin(TAU * (U * 0.7 + t)) * V
                return k[..., None] * d
        else:
            def off(U, V):
                bell = np.sin(np.pi * np.clip(U, 0, 1)) * np.sin(np.pi * np.clip(V * 0.9 + 0.1, 0, 1))
                k = amt * bell + wind * 0.01 * np.sin(TAU * (V + t)) * U
                return k[..., None] * d
        return lit_cloth(patch(sd['corners'], sd['nu'], sd['nv'], sd['mat_fn'], off))

    def _flag(self, pos, w, h, phase, mid, t, k, droop):
        def off(U, V):
            a = (0.012 + 0.02 * k) * U * np.sin(TAU * (1.3 * U - t * (1 + k)) + phase)
            z = -droop * U ** 2
            return np.stack([a, np.zeros_like(a), z * np.ones_like(a)], -1)
        x, y, z = pos
        corners = [(x, y, z), (x, y - w, z), (x, y - w, z - h), (x, y, z - h)]
        return lit_cloth(patch(corners, 6, 2, lambda u, v: mid, off), 0.002)

    def _oars(self, ph):
        """ph = (sweep angle deg, lift 0..1)."""
        o = self.b.oars
        hull = self.b.hull
        sweep, lift = ph
        out = []
        self._oar_tips = []
        ys = np.linspace(o['y0'], o['y1'], o['n'])
        for side in (-1, 1):
            for y in ys:
                s = hull.s_of(y)
                z = o['z'] if o['z'] is not None else hull.rail_z(s) - 0.02
                xb = hull.half_beam(s) + 0.005
                a = np.array([side * xb, y, z], np.float32)
                dip = math.radians(o['dip'] - 14 * lift)
                sw = math.radians(sweep)
                dirv = np.array([side * math.cos(dip) * math.cos(sw), math.sin(sw) * math.cos(dip), -math.sin(dip)])
                dirv /= np.linalg.norm(dirv)
                # pivot: the loom inside and the blade out in the water
                inb = a - dirv * o['L'] * 0.12
                tip = a + dirv * o['L']
                self._oar_tips.append((tip, lift))
                out.append(rod(inb, tip, o['r'], o['mat'], seg=5))
                bl = tip - dirv * o['L'] * 0.18
                out.append(rod(bl, tip, o['r'] * 2.2, o['mat'], seg=5, r1=o['r'] * 2.0))
        return M.Mesh.concat(out)

    # ---------------------------------------------------- motion
    def frame(self, action, f, n, ctx):
        if ctx is None:
            return self._calib_mesh()
        loop = self.loops(action)
        t = rig.frame_times(n, loop)[f]
        act = self.p.get('actions', {}).get(action, action)
        if act in ('DEFAULT', 'WALK', 'STOP_AT_LAST_FRAME', 'DEFEND', 'DEAD', 'VICTORY'):
            return self._idle(t, n, run=False, fidget=False)
        if act == 'RUN':
            return self._idle(t, n, run=True, fidget=False)
        if act in ('FIDGET', 'FORTIFY', 'FORTIFYHOLD'):
            return self._idle(t, n, run=False, fidget=True)
        if act.startswith('ATTACK'):
            return self._attack(t, n, ctx)
        if act == 'DEATH':
            return self._death(t, n, ctx)
        return self._idle(t, n, run=False, fidget=False)

    def _pose_T(self, z=0.0, pitch=0.0, roll=0.0):
        # the design's proportions (beam_k, height_k) are applied before the pose
        return M.translate(0, 0, z) @ M.rot_x(pitch) @ M.rot_y(roll) @ self.S

    def _bob(self, t, run):
        k = self.p.get('bob', 1.0) * (1.6 if run else 1.0)
        L = self.length
        z = 0.012 * k * math.sin(TAU * t) * min(1.0, L / 2.0)
        pitch = 1.1 * k * math.sin(TAU * t + 1.2) * (2.0 / max(L, 1.0)) ** 0.5
        roll = 1.6 * k * math.sin(TAU * t + 0.4) * (2.0 / max(L, 1.0)) ** 0.5
        if run:
            pitch -= 0.6
        return z, pitch, roll

    def _idle(self, t, n, run, fidget):
        z, pitch, roll = self._bob(t, run)
        billow = 1.0 + (0.08 * math.sin(TAU * t) if not run else 0.15)
        b = self.b
        oar = None
        if b.oars:
            if run:
                ang = TAU * t * self.p.get('oar_cycles', 2)
                oar = (28 * math.sin(ang), 0.5 + 0.5 * math.cos(ang))
            else:
                oar = (4 * math.sin(TAU * t), 0.15)
        tp = {}
        if fidget and b.turrets:
            for i, tr in enumerate(b.turrets):
                sw = 25 * math.sin(TAU * t + i * 0.7) * _smooth(min(1, 4 * min(t, 1 - t) + 0.0))
                tp[i] = (tr.rest + sw, 4 * math.sin(TAU * t + i), 0.0)
        ship = self.ship_mesh('DEFAULT', t, billow=billow, wind=1.0 if fidget else 0.4, oar=oar, turret_pose=tp,
                              flag_k=1.8 if fidget or run else 1.0, spin=t)
        T = self._pose_T(z, pitch, roll)
        ship = clip_below(ship.transformed(T))
        fx = [self._wake(t, run, z), self._stack_smoke(t, T)]
        if b.oars and run:
            # splashes where the blades bite
            mf = self.mats['foam']
            for k, (tip, lift) in enumerate(self._oar_tips):
                if lift < 0.55:
                    p = (T @ np.array([*tip, 1.0], np.float32))[:3]
                    r = 0.03 * (1 - lift / 0.55)
                    fx.append(disc_flat(p[0], p[1], 0.004, r, mf, seg=6, rot=k))
        return M.Mesh.concat([ship] + fx)

    # ---------------------------------------------------- effects
    def _wake(self, t, run, dz=0.0, scale=1.0, k_foam=1.0, band=1.0):
        """Soft wake around the waterline, foam at the bow (and the stern when
        running); deterministic per frame, flecks drift aft over the loop."""
        wl = self.waterline
        if wl is None:
            return M.Mesh()
        mw = self.mats['wake']
        mf = self.mats['foam']
        L = self.length
        rng = np.random.default_rng(7)
        out = []
        # soft band hugging the hull
        c = wl.mean(0)
        P = wl - c
        nrm = np.roll(P, -1, 0) - np.roll(P, 1, 0)
        nrm = np.stack([nrm[:, 1], -nrm[:, 0]], -1)
        nrm /= np.linalg.norm(nrm, axis=-1, keepdims=True) + 1e-9
        # orient outward
        if (nrm * P).sum() < 0:
            nrm = -nrm
        yb = (wl[:, 1] - wl[:, 1].min()) / max(np.ptp(wl[:, 1]), 1e-6)   # 0 stern .. 1 bow
        w = (0.035 + 0.03 * yb ** 3 + (0.05 * (1 - yb) ** 4 if run else 0.0)) * scale * self.b.extra_wake
        w = w * (1 + 0.25 * np.sin(TAU * (yb * 3 - t * 2))) * band
        inner = wl + nrm * 0.004
        outer = wl + nrm * (0.004 + w[:, None] * (1.6 if run else 1.0))
        n = len(wl) if band > 0.05 else 0
        V = []
        for i in range(n):
            j = (i + 1) % n
            a, b_, c2, d = inner[i], inner[j], outer[j], outer[i]
            V.append([(*a, 0.002), (*b_, 0.002), (*c2, 0.002)])
            V.append([(*a, 0.002), (*c2, 0.002), (*d, 0.002)])
        if V:
            V = np.array(V, np.float32)
            N = np.zeros_like(V)
            N[..., 2] = 1
            out.append(M.Mesh(V, N, np.full(len(V), mw, np.int32)))
        # foam flecks: at the bow, along the sides, and trailing (running)
        nf = int((26 if run else 12) * k_foam * max(1.0, L / 2.0))
        ymin, ymax = wl[:, 1].min(), wl[:, 1].max()
        for i in range(nf):
            ph = rng.random()
            age = (t * (2 if run else 1) + ph) % 1.0
            side = 1 if i % 2 else -1
            # start near the bow, drift aft along the side and outwards
            if run:
                y = ymax - age * (ymax - ymin) * 1.15
            else:
                y = ymax - (0.15 + 0.5 * rng.random()) * (ymax - ymin) * (0.4 + 0.6 * age)
            yy = np.clip(y, ymin, ymax)
            k = np.argmin(np.abs(wl[: len(wl) // 2 + 1, 1] - yy))
            xw = abs(wl[k, 0]) if y > ymin else 0.0
            x = side * (xw + 0.01 + age * (0.07 if run else 0.025) + rng.random() * 0.02)
            r = (0.012 + 0.018 * rng.random()) * (1 - age) ** 0.7 * scale
            if r < 0.004:
                continue
            out.append(disc_flat(x, y, 0.004, r, mf, seg=6, rot=rng.random() * 3, sx=1.0, sy=1.6))
        # bow wave crest
        bow = wl[np.argmax(wl[:, 1])]
        if run:
            for side in (-1, 1):
                for j in range(5):
                    a = j / 4
                    y = bow[1] - a * L * 0.25
                    x = side * (0.02 + a * 0.12 + abs(np.interp(y, wl[: len(wl) // 2 + 1, 1], wl[: len(wl) // 2 + 1, 0])))
                    out.append(disc_flat(x, y, 0.004, 0.02 * (1 - a * 0.5) * scale, mf, seg=7, sx=1, sy=2.0,
                                         rot=0.3 * side))
        else:
            out.append(disc_flat(bow[0], bow[1] + 0.01, 0.004, 0.022 * scale, mf, seg=8, sx=1.4, sy=0.8))
        return M.Mesh.concat(out)

    def _stack_smoke(self, t, T):
        out = []
        ms = self.mats['smoke_mid']
        for i, top in enumerate(self.b.smoke_stacks):
            p = (T @ np.array([*top, 1.0], np.float32))[:3]
            for j in range(4):
                a = (t + j / 4) % 1.0
                c = p + np.array([0.0, -0.10 * a - 0.02, 0.02 + 0.12 * a], np.float32)
                r = 0.018 + 0.03 * a
                r *= 1 - _smooth((a - 0.75) / 0.25)
                if r > 0.004:
                    out.append(blob(c, r, ms, seg=8))
        return M.Mesh.concat(out)

    def _puff(self, center, t0, t, size, mat_name='smoke', drift=(0, 0, 0.12), life=0.45, n=4, seed=0):
        """An expanding then fading smoke puff (cluster of spheres)."""
        a = (t - t0) / life
        if a < 0 or a > 1:
            return M.Mesh()
        rng = np.random.default_rng(seed)
        out = []
        mid = self.mats[mat_name]
        grow = 1 - (1 - min(1.0, a * 2.5)) ** 2
        fade = 1 - _smooth((a - 0.55) / 0.45)
        for i in range(n):
            o = rng.normal(0, 0.55, 3) * size
            o[2] = abs(o[2]) * 0.4
            c = np.asarray(center, np.float32) + o * (0.6 + grow) + np.asarray(drift, np.float32) * a
            r = size * (0.45 + 0.35 * rng.random()) * (0.4 + 0.8 * grow) * fade
            if r > 0.004:
                out.append(blob(c, r, mid, seg=9, squash=0.75))
        return M.Mesh.concat(out)

    def _flash(self, pos, d, t0, t, size=0.08, life=0.08):
        a = (t - t0) / life
        if a < 0 or a > 1:
            return M.Mesh()
        k = math.sin(math.pi * min(1.0, a * 1.3 + 0.2))
        d = np.asarray(d, np.float32)
        d = d / (np.linalg.norm(d) + 1e-9)
        pos = np.asarray(pos, np.float32)
        out = [blob(pos + d * size * 0.6 * k, size * 0.55 * k, self.mats['fire_core'], seg=10)]
        L = size * 2.2 * k
        c = pos + d * L * 0.6
        e = M.ellipsoid(size * 0.35 * k, L * 0.6, size * 0.35 * k, self.mats['flash'], seg=10)
        yaw = math.degrees(math.atan2(-d[0], d[1]))
        pitch = math.degrees(math.asin(np.clip(d[2], -1, 1)))
        out.append(e.transformed(M.translate(*c) @ M.rot_z(yaw) @ M.rot_x(pitch)))
        out.append(blob(pos + d * size * 0.2, size * 0.75 * k, self.mats['fire'], seg=10, squash=0.8))
        return M.Mesh.concat(out)

    def _splash(self, x, y, t0, t, size=0.05, life=0.3, seed=0):
        a = (t - t0) / life
        if a < 0 or a > 1:
            return M.Mesh()
        mf = self.mats['foam']
        rng = np.random.default_rng(seed)
        out = []
        h = size * 2.2 * math.sin(math.pi * a)
        for i in range(6):
            ang = rng.random() * TAU
            rr = size * (0.3 + 0.5 * a)
            c = (x + math.cos(ang) * rr * 0.6, y + math.sin(ang) * rr * 0.6, h * (0.4 + 0.6 * rng.random()))
            r = size * 0.32 * (1 - a * 0.7)
            out.append(blob(c, r, mf, seg=7))
        out.append(disc_flat(x, y, 0.003, size * (0.6 + a), self.mats['wake'], seg=10))
        return M.Mesh.concat(out)

    # ---------------------------------------------------- attack
    def _attack(self, t, n, ctx):
        b = self.b
        wpn = b.weapon or {'kind': 'none'}
        kind = wpn['kind']
        tf = self.p.get('fire_t', wpn.get('t', 0.2))
        z, pitch, roll = self._bob(t * 0.5, False)
        fx = []
        tp = {}
        # turrets: swing to their aim, fire (staggered), recoil, swing back
        for i, tr in enumerate(b.turrets):
            t_aim = _smooth(t / max(tf, 1e-3)) if t < tf else 1 - _smooth((t - 0.7) / 0.3)
            # Civ3's ships fire to starboard: turrets train to the right (fore
            # mounts a little forward of the beam, aft mounts a little aft)
            if self.p.get('fire_dir', 'starboard') == 'starboard' and not tr.fixed:
                tgt = -90 + (18 if tr.pos[1] > 0 else -18)
                aim = tr.rest + ((tgt - tr.rest + 180) % 360 - 180)
            else:
                aim = tr.aim
            yaw = tr.rest + (aim - tr.rest) * t_aim
            ft = tf + tr.delay
            rec = 0.0
            if t >= ft:
                a = (t - ft) / 0.12
                rec = tr.recoil * (math.exp(-a * 1.5) * min(1.0, a * 6)) if a < 3 else 0.0
            tp[i] = (yaw, tr.elev * t_aim, rec)
        # recoil of the whole ship (heel away from a broadside / big guns)
        kick = 0.0
        if kind in ('cannons', 'turrets', 'deckgun') and b.weapon.get('kick', True):
            a = (t - tf) / 0.25
            if a > 0:
                kick = math.sin(min(a, 1) * math.pi) * math.exp(-a) * self.p.get('kick', 2.5)
        oar = None
        if b.oars:
            ang = TAU * t * 2
            oar = (14 * math.sin(ang), 0.5 + 0.5 * math.cos(ang))
        ship = self.ship_mesh('DEFAULT', t, billow=1.05, wind=0.4, oar=oar, turret_pose=tp, flag_k=1.3, spin=t)
        T = self._pose_T(z, pitch - kick * 0.4, roll + kick)
        ship = clip_below(ship.transformed(T))
        fx.append(self._wake(t, False, z))
        fx.append(self._stack_smoke(t, T))
        # muzzles of the turrets: one flash and smoke cloud per turret
        k = self.fx_k
        for i, tr in enumerate(b.turrets):
            if i >= len(self._gun_frames) or not tr.muzzles:
                continue
            G = T @ self._gun_frames[i]
            ft = tf + tr.delay
            dvec = (G[:3, :3] @ np.array([0, 1, 0], np.float32))
            dvec /= np.linalg.norm(dvec) + 1e-9
            mz = np.mean(tr.muzzles, 0)
            p = (G @ np.array([*mz, 1.0], np.float32))[:3]
            sz = 0.13 * tr.flash * k * (1 + 0.25 * (len(tr.muzzles) - 1))
            fx.append(self._flash(p, dvec, ft, t, size=sz, life=0.12))
            fx.append(self._puff(p + dvec * sz * 1.6, ft + 0.05, t, sz * 1.3, 'smoke_mid',
                                 drift=(dvec[0] * sz * 2.5, dvec[1] * sz * 2.5, sz * 0.6), life=0.6, n=7,
                                 seed=i * 7))
        # broadside guns: flash, then white powder smoke along the side
        for i, (pos, d, delay, size) in enumerate(b.guns):
            if d[0] < 0 and self.p.get('fire_dir', 'starboard') == 'starboard':
                continue
            p = (T @ np.array([*pos, 1.0], np.float32))[:3]
            dv = T[:3, :3] @ d
            dv /= np.linalg.norm(dv) + 1e-9
            ft = tf + delay
            sz = 0.07 * size * k
            fx.append(self._flash(p, dv, ft, t, size=sz, life=0.09))
            fx.append(self._puff(p + dv * sz * 1.8, ft + 0.02, t, sz * 2.2, 'smoke',
                                 drift=(dv[0] * sz * 3, dv[1] * sz * 3 - 0.05, sz * 0.6), life=0.6, n=6, seed=31 + i))
        fx.append(self._weapon_fx(kind, wpn, t, tf, T))
        for ex in wpn.get('extra', []):
            fx.append(self._weapon_fx(ex['kind'], ex, t, tf + ex.get('dt', 0.0), T))
        return M.Mesh.concat([ship] + fx)

    def _weapon_fx(self, kind, w, t, tf, T):
        out = []
        if kind == 'arrows':
            ma = self.mats['arrow']
            src = np.asarray(w.get('src', (0, 0, 0.25)), np.float32)
            d = np.asarray(w.get('dir', (-0.5, 1.0, 0.0)), np.float32)
            d /= np.linalg.norm(d)
            rng = np.random.default_rng(3)
            reach = w.get('reach', 1.6)
            for vol in range(w.get('volleys', 2)):
                t0 = tf + vol * w.get('gap', 0.3)
                for k in range(w.get('n', 5)):
                    a = (t - t0 - k * 0.02) / w.get('flight', 0.35)
                    if a < 0 or a > 1:
                        continue
                    o = np.array([rng.normal(0, 0.12), rng.normal(0, 0.25), 0], np.float32)
                    p0 = (T @ np.array([*(src + o), 1.0], np.float32))[:3]
                    dd = d + np.array([rng.normal(0, 0.08), 0, 0], np.float32)
                    pos = p0 + dd * reach * a + np.array([0, 0, 0.35 * reach * a * (1 - a)], np.float32)
                    vel = dd * reach + np.array([0, 0, 0.35 * reach * (1 - 2 * a)], np.float32)
                    vel /= np.linalg.norm(vel)
                    out.append(rod(pos - vel * 0.09, pos + vel * 0.08, 0.009, ma, seg=4))
                    out.append(rod(pos + vel * 0.07, pos + vel * 0.11, 0.013, self.mats['iron'], seg=4, r1=0.002))
        elif kind == 'fire':   # greek fire: jets from the siphons, then fireballs and smoke
            src = (T @ np.array([*w['src'], 1.0], np.float32))[:3]
            reach = w.get('reach', 1.2)
            dur = w.get('dur', 0.3)
            k = self.fx_k
            for jn, dd in enumerate(w.get('dirs', [(0, 1, -0.05)])):
                d = np.asarray(dd, np.float32)
                d = T[:3, :3] @ (d / np.linalg.norm(d))
                d /= np.linalg.norm(d) + 1e-9
                t0 = tf + jn * 0.03
                a = (t - t0) / dur
                if 0 <= a <= 1.0:
                    ext = min(1.0, a * 2.2) * (1 - 0.6 * _smooth((a - 0.7) / 0.3))
                    for j in range(16):
                        q = j / 15
                        if q > ext:
                            break
                        p = src + d * reach * q + np.array([0, 0, -0.1 * reach * q * q], np.float32)
                        r = (0.04 + 0.16 * q) * k
                        out.append(blob(p, r, self.mats['fire_core' if q < 0.4 else 'fire'], seg=9))
                # the fireball where the jet ends, turning into dark smoke
                p = src + d * reach * 1.05 + np.array([0, 0, -src[2] + 0.12 * k], np.float32)
                aa = (t - t0 - dur * 0.45) / 0.4
                if 0 <= aa <= 1:
                    r = 0.42 * k * (0.5 + aa) * (1 - _smooth((aa - 0.6) / 0.4)) + 0.001
                    for q, (ox, oy, oz) in enumerate(((0, 0, 0), (0.5, 0.2, 0.3), (-0.4, -0.3, 0.25))):
                        out.append(blob(p + np.array([ox, oy, oz], np.float32) * r + (0, 0, 0.1 * aa), r,
                                        self.mats['fire'], seg=10))
                    out.append(blob(p + (0, 0, 0.1 * aa + r * 0.2), r * 0.55, self.mats['fire_core'], seg=8))
                out.append(self._puff(p + (0, 0, 0.15 * k), t0 + dur * 0.45 + 0.3, t, 0.45 * k, 'smoke_dark',
                                      drift=(0, 0, 0.2 * k), life=0.35, n=5, seed=50 + jn))
        elif kind == 'torpedo':
            src = np.asarray(w['src'], np.float32)
            reach = w.get('reach', 2.0)
            a = (t - tf) / w.get('dur', 0.6)
            mf = self.mats['foam']
            if 0 <= a <= 1:
                y = src[1] + reach * a
                pos = np.array([src[0], y, 0.0], np.float32)
                out.append(rod(pos + (0, -0.06, 0.003), pos + (0, 0.06, 0.003), 0.012, self.mats['dark'], seg=6))
                for j in range(10):
                    q = j / 9
                    yy = y - q * min(reach * a, 0.9)
                    if yy < src[1]:
                        break
                    out.append(disc_flat(src[0] + 0.01 * math.sin(j * 2.1), yy, 0.004, 0.012 + 0.012 * q, mf, seg=6))
            out.append(self._splash(src[0], src[1], tf - 0.02, t, size=0.05, life=0.25, seed=5))
        elif kind == 'missile':
            src = (T @ np.array([*w['src'], 1.0], np.float32))[:3]
            a = (t - tf) / w.get('dur', 0.45)
            if 0 <= a <= 1:
                d = np.array([0, 0.45 * a, 1.0 - 0.5 * a], np.float32)
                p = src + np.array([0, 0.9 * a * a, 1.1 * a], np.float32)
                d /= np.linalg.norm(d)
                out.append(rod(p - d * 0.05, p + d * 0.05, 0.012, self.mats['white'], seg=6))
                out.append(blob(p - d * 0.07, 0.022, self.mats['fire_core'], seg=8))
                for j in range(8):
                    q = j / 7 * a
                    pp = src + np.array([0, 0.9 * q * q, 1.1 * q], np.float32)
                    out.append(blob(pp, 0.02 + 0.03 * (a - q), self.mats['smoke'], seg=7))
            out.append(self._flash(src, (0, 0, 1), tf, t, size=0.07, life=0.08))
            out.append(self._puff(src, tf, t, 0.08, 'smoke', drift=(0, -0.1, 0.06), life=0.6, n=5, seed=9))
        elif kind == 'jet':     # carrier: a jet runs along the deck and lifts off
            from models import ship_designs
            a = (t - tf) / w.get('dur', 0.5)
            if 0 <= a <= 1:
                y0, y1 = w['y0'], w['y1']
                y = y0 + (y1 - y0) * a ** 1.8
                z = w['z'] + max(0.0, a - 0.7) * 0.6
                jet = ship_designs.jet(self.mats, w.get('size', 0.16) * 1.3)
                jt = T @ M.translate(w.get('x', 0.0), y, z) @ M.rot_x(-12 * max(0.0, a - 0.65) / 0.35)
                out.append(jet.transformed(jt))
                p = (jt @ np.array([0, -w.get('size', 0.16) * 0.55, 0.01, 1.0], np.float32))[:3]
                out.append(blob(p, 0.04 + 0.015 * math.sin(a * 40), self.mats['fire_core'], seg=8))
                out.append(blob(p - (0, 0.03, 0), 0.03, self.mats['flash'], seg=8))
            out.append(self._puff((T @ np.array([w.get('x', 0.0), w['y0'], w['z'], 1.0], np.float32))[:3],
                                  tf + 0.05, t, 0.05, 'smoke', drift=(0, -0.1, 0.04), life=0.5, n=4, seed=17))
        return M.Mesh.concat(out)

    # ---------------------------------------------------- death
    def _death(self, t, n, ctx):
        b = self.b
        modern = self.p.get('modern', False)
        td = ctx.event('down', None) if ctx is not None else None
        end = self.p.get('sink_end', 0.92)
        if td is not None:
            end = float(np.clip(td / 0.55, 0.6, 0.95))
        start = self.p.get('sink_start', 0.12 if not modern else 0.25)
        u = _clamp((t - start) / max(end - start, 1e-3))
        side = self.p.get('death_side', -1.0)
        roll = side * self.p.get('death_roll', 45.0) * _smooth(u * 1.3)
        pitch = -self.p.get('death_pitch', 14.0) * _smooth(u)
        H = float(self.static.V[..., 2].max()) * self.p.get('height_k', 1.0) + 0.1
        hh = ((self.b.hull.D + self.b.hull.hb) * self.p.get('height_k', 1.0) + 0.08) if self.b.hull else 0.3
        zs = -hh * _smooth(u / 0.6) - (H - hh) * 1.1 * max(0.0, (u - 0.6) / 0.4) ** 1.3
        z0, p0, r0 = self._bob(t * 0.5, False)
        ship = self.ship_mesh('DEFAULT', t, billow=1.0 - 0.6 * u, wind=0.5, flag_k=1.0, spin=0.0,
                              oar=(10 * math.sin(TAU * t), 0.6) if b.oars else None)
        brk = self.p.get('death_break', 0.0)
        if brk:
            # the hull snaps amidships: both ends go down
            a = brk * _smooth(u * 1.6)
            cy = ship.V[..., 1].mean(1)
            fr = cy > 0
            ship = M.Mesh.concat([subset(ship, fr).transformed(M.translate(0, 0, 0.12 * a / brk) @ M.rot_x(-a)),
                                  subset(ship, ~fr).transformed(M.translate(0, 0, 0.12 * a / brk) @ M.rot_x(a * 0.6))])
            roll *= 0.3
        T = self._pose_T(z0 * (1 - u) + zs, p0 + pitch, r0 + roll)
        ship = clip_below(ship.transformed(T))
        fx = [self._wake(t, False, scale=1.0 + 0.4 * u, k_foam=1.0 + 2.5 * math.sin(math.pi * min(u, 1)),
                         band=max(0.0, 1 - u / 0.4))]
        # sinking foam: splashes along the waterline cut
        L = self.length
        for j in range(4):
            fx.append(self._splash(((-1) ** j) * 0.1, (j / 3 - 0.5) * L * 0.6, start + 0.15 * j + 0.1, t,
                                   size=(0.07 + 0.02 * L) * self.fx_k, life=0.4, seed=60 + j))
        if modern:
            # explosions and smoke
            for j, (fx_, fy, ft) in enumerate(self.p.get('explosions', [(0.0, 0.15, 0.04), (0.05, -0.25, 0.16),
                                                                        (-0.04, 0.35, 0.3)])):
                p = (T @ np.array([fx_, fy * L, self.b.hull.D + 0.05, 1.0], np.float32))[:3]
                p[2] = max(p[2], 0.05)
                a = (t - ft) / 0.3
                k = self.fx_k
                if 0 <= a <= 1:
                    r = 0.2 * k * (0.4 + a) * (1 - _smooth((a - 0.55) / 0.45)) + 0.001
                    for q, (ox, oy) in enumerate(((0, 0), (0.6, 0.3), (-0.5, 0.4), (0.2, -0.6))):
                        c = p + np.array([ox * r * 0.7, oy * r * 0.7, 0.1 * a + r * 0.3 * (q > 0)], np.float32)
                        fx.append(blob(c, r * (1 - 0.2 * (q > 0)), self.mats['fire'], seg=10))
                    fx.append(blob(p + (0, 0, 0.1 * a + r * 0.4), r * 0.65, self.mats['fire_core'], seg=8))
                fx.append(self._puff(p + (0, 0, 0.12 * k), ft + 0.15, t, 0.17 * k, 'smoke_dark',
                                     drift=(0.08, -0.12, 0.3 * k), life=0.6, n=6, seed=80 + j))
        return M.Mesh.concat([ship] + fx)

    # ---------------------------------------------------- rendering
    def render_cell(self, mesh, mats, cam, W, H):
        mesh = split_long(mesh, cam)
        Mt = mesh.M
        soft = np.isin(Mt, list(self.soft_ids))
        foam = np.isin(Mt, list(self.foam_ids))
        fx = np.isin(Mt, list(self.fx_ids))
        smoke = np.isin(Mt, list(self.smoke_ids))
        main = ~soft & ~foam & ~smoke
        base, tint = R.render_cell(subset(mesh, main), mats, cam, W, H, style=ShipStyle, shadow=False)
        sh = soft_shadow(subset(mesh, main & ~fx), cam, W, H)
        under = np.zeros((H, W, 4), np.float32)
        under[..., 3] = sh
        if soft.any():
            wb, _ = R.render_cell(subset(mesh, soft), mats, cam, W, H, style=FxStyle, shadow=False)
            w = _f(wb)
            w[..., 3] = np.clip(R._blur(w[..., 3], 0.8), 0, 1) * WAKE_ALPHA
            under = _over(w, under)
        if foam.any():
            fb, _ = R.render_cell(subset(mesh, foam), mats, cam, W, H, style=FxStyle, shadow=False)
            fo = _f(fb)
            fo[..., 3] *= FOAM_ALPHA
            under = _over(fo, under)
        out = _over(_f(base), under)
        if smoke.any():
            sb, _ = R.render_cell(subset(mesh, smoke), mats, cam, W, H, style=SmokeStyle, shadow=False)
            sm = _f(sb)
            sm[..., 3] = np.clip(R._blur(sm[..., 3], 1.0), 0, 1) * SMOKE_ALPHA
            out = _over(sm, out)
            # smoke hides the civ-colored parts behind it too
            tf = _f(tint)
            tf[..., 3] *= 1 - sm[..., 3]
            tint = R._u8(tf)
        return R._u8(out), tint
