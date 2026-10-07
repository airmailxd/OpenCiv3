"""Hard-surface modeling helpers for vehicles, siege engines and aircraft.

Shared by models/vehicles.py, models/aircraft.py and models/missiles.py (no
archetypes here). Coordinates as everywhere: x = right, y = forward, z = up,
meters. Hard parts are flat shaded (crisp low-poly facets); round parts
(wheels, barrels, fuselages) are smooth.
"""
import math

import numpy as np

import mesh as M


# ------------------------------------------------------------ materials

def hexrgb(c):
    if isinstance(c, str):
        c = c.lstrip('#')
        return tuple(int(c[i:i + 2], 16) for i in (0, 2, 4))
    return tuple(c)


def shade(c, k):
    """A color multiplied by k (k > 1 lightens toward white)."""
    r = np.array(hexrgb(c), np.float32)
    if k >= 1:
        r = r + (255 - r) * (k - 1)
    else:
        r = r * k
    return '#%02x%02x%02x' % tuple(int(np.clip(v, 0, 255)) for v in r)


def add_mats(mats, table, tint=(), metal=()):
    """Adds {name: color} (or {name: (color, spec, gloss)}) to a Materials
    table; tinted names render white into the civ-color layer."""
    for name, v in table.items():
        spec, gloss = 0.10, 14.0
        if isinstance(v, tuple) and len(v) == 3 and isinstance(v[0], str):
            v, spec, gloss = v
        is_t = name in tint
        mats.add(name, '#ffffff' if is_t else v, tint=is_t, spec=spec, gloss=gloss,
                 metal=(name in metal) and not is_t)


# ------------------------------------------------------------ geometry

def sweep(sections, mat, smooth=False, caps=True):
    """A solid from cross-sections along y: [(y, [(x, z), ...]), ...], each
    section a polygon with the same number of points (counter-clockwise seen
    from +y, i.e. from the front), front to back or back to front. Caps are
    fanned from the section centroid (convex sections work best). Hulls,
    turrets, fuselages."""
    secs = [(float(y), np.asarray(p, np.float32)) for y, p in sections]
    n = len(secs[0][1])
    V = []
    for y, p in secs:
        V.append(np.stack([p[:, 0], np.full(n, y, np.float32), p[:, 1]], -1))
    V = np.concatenate(V).astype(np.float32)
    faces = []
    for i in range(len(secs) - 1):
        for j in range(n):
            a = i * n + j
            b = i * n + (j + 1) % n
            c = (i + 1) * n + j
            d = (i + 1) * n + (j + 1) % n
            faces += [(a, b, d), (a, d, c)]
    if caps:
        for k, (y, p) in ((0, secs[0]), (len(secs) - 1, secs[-1])):
            c = len(V)
            ctr = p.mean(0)
            V = np.vstack([V, [[ctr[0], y, ctr[1]]]]).astype(np.float32)
            for j in range(n):
                faces.append((c, k * n + (j + 1) % n, k * n + j))
    m = M.from_indexed(V, np.array(faces), mat, smooth=smooth)
    return fix_winding(m)


def fix_winding(m, center=None):
    """Orients every triangle's normal (for flat parts) outward from the
    mesh's center, so hand-built solids shade consistently. The renderer is
    double-sided, so this only matters for normals."""
    if not len(m):
        return m
    c = m.V.reshape(-1, 3).mean(0) if center is None else np.asarray(center, np.float32)
    fc = m.V.mean(1)
    fn = np.cross(m.V[:, 1] - m.V[:, 0], m.V[:, 2] - m.V[:, 0])
    out = ((fc - c) * fn).sum(-1) < 0
    m.V[out] = m.V[out][:, ::-1]
    m.N[out] = -m.N[out][:, ::-1]
    return m


def prism(poly, h, mat, z0=0.0, smooth=False):
    """A 2D polygon in the x-y plane (seen from above) extruded from z0 to
    z0 + h. Wings, tail planes, plates seen from above."""
    p = np.asarray(poly, np.float32)
    n = len(p)
    c = p.mean(0)
    tris, norms = [], []
    z1 = z0 + h
    for i in range(n):
        a, b = p[i], p[(i + 1) % n]
        tris.append([[c[0], c[1], z1], [a[0], a[1], z1], [b[0], b[1], z1]]); norms.append([[0, 0, 1]] * 3)
        tris.append([[c[0], c[1], z0], [b[0], b[1], z0], [a[0], a[1], z0]]); norms.append([[0, 0, -1]] * 3)
        e = b - a
        sn = np.array([e[1], -e[0], 0], np.float32)
        sn /= np.linalg.norm(sn) + 1e-9
        if ((a + b) / 2 - c) @ sn[:2] < 0:
            sn = -sn
        tris.append([[a[0], a[1], z0], [b[0], b[1], z0], [b[0], b[1], z1]])
        tris.append([[a[0], a[1], z0], [b[0], b[1], z1], [a[0], a[1], z1]])
        norms += [[sn] * 3, [sn] * 3]
    return M.Mesh(np.array(tris, np.float32), np.array(norms, np.float32), np.full(len(tris), mat, np.int32))


def side_prism(poly, w, mat, x0=None):
    """A 2D polygon in the y-z plane (seen from the side, [(y, z), ...])
    extruded across x over a width w centered on x0 (default 0)."""
    p = [(z, -y) for y, z in poly]   # (local x', y') with x' -> z, y' -> -y
    m = prism(p, w, mat, z0=-w / 2)
    # local (x', y', z') = (z, -y, x) -> world
    T = np.array([[0, 0, 1, 0], [0, -1, 0, 0], [1, 0, 0, 0], [0, 0, 0, 1]], np.float32)
    m = m.transformed(T)
    if x0:
        m = m.transformed(M.translate(x0, 0, 0))
    return m


def bar(a, b, w, h, mat, up=(0, 0, 1)):
    """A rectangular beam from a to b, w wide and h tall (flat shaded)."""
    a, b = np.asarray(a, np.float32), np.asarray(b, np.float32)
    d = b - a
    L = float(np.linalg.norm(d))
    z = d / (L + 1e-9)
    upv = np.asarray(up, np.float32)
    if abs(float(z @ upv)) > 0.95:
        upv = np.array([0, 1, 0], np.float32) if abs(z[1]) < 0.95 else np.array([1, 0, 0], np.float32)
    x = np.cross(upv, z); x /= np.linalg.norm(x)
    y = np.cross(z, x)
    T = np.eye(4, dtype=np.float32)
    T[:3, 0], T[:3, 1], T[:3, 2], T[:3, 3] = x, y, z, a
    return M.box(w, h, L, mat, center=(0, 0, L / 2)).transformed(T)


def tube(a, b, r0, r1, mat, seg=10, caps=True):
    """A (tapered) cylinder from a to b."""
    a, b = np.asarray(a, np.float32), np.asarray(b, np.float32)
    L = float(np.linalg.norm(b - a))
    m = M.cylinder(r0, r1, 1.0, mat, seg=seg, caps=caps)
    return m.transformed(M.look_along(a, b) @ M.scale(1, 1, 1)) if L > 0 else m


def axle_x(r, w, mat, seg=14, center=(0, 0, 0)):
    """A cylinder along x (wheels, axles, rollers) centered at `center`."""
    m = M.cylinder(r, r, w, mat, seg=seg, z0=-w / 2)
    return m.transformed(M.translate(*center) @ M.rot_y(90))


def ring_x(r_out, r_in, w, mat, seg=18, center=(0, 0, 0)):
    """A ring (tire, wheel rim) around the x axis."""
    prof = [(-w / 2, r_in), (-w / 2, r_out), (w / 2, r_out), (w / 2, r_in), (-w / 2, r_in)]
    m = M.lathe(prof, mat, seg, smooth=False)
    return m.transformed(M.translate(*center) @ M.rot_y(90))


def spoked_wheel(r, w, mats, rim, spoke, hub, n_spokes=10, angle=0.0, center=(0, 0, 0), tire=None):
    """A cart/gun wheel around the x axis, turned by `angle` degrees (so its
    spokes show the rotation)."""
    parts = [ring_x(r, r * 0.84, w, rim, seg=22)]
    if tire is not None:
        parts.append(ring_x(r * 1.04, r * 0.97, w * 1.05, tire, seg=22))
    parts.append(axle_x(r * 0.2, w * 1.5, hub, seg=10))
    for i in range(n_spokes):
        a = math.radians(angle + i * 360.0 / n_spokes)
        tip = (0, math.cos(a) * r * 0.86, math.sin(a) * r * 0.86)
        parts.append(bar((0, 0, 0), tip, w * 0.45, w * 0.45, spoke, up=(1, 0, 0)))
    return M.Mesh.concat(parts).transformed(M.translate(*center))


def disc_wheel(r, w, rim, hub, angle=0.0, center=(0, 0, 0), bolts=5, tire=None):
    """A solid road/car wheel around the x axis with a hub and bolt marks
    (which show it turning)."""
    parts = []
    if tire is not None:
        parts.append(axle_x(r, w, tire, seg=18))
        parts.append(axle_x(r * 0.62, w * 1.06, rim, seg=14))
    else:
        parts.append(axle_x(r, w, rim, seg=18))
    parts.append(axle_x(r * 0.28, w * 1.25, hub, seg=10))
    for i in range(bolts):
        a = math.radians(angle + i * 360.0 / bolts)
        c = (0, math.cos(a) * r * 0.45, math.sin(a) * r * 0.45)
        parts.append(M.box(w * 1.12, r * 0.13, r * 0.13, hub, center=c))
    return M.Mesh.concat(parts).transformed(M.translate(*center))


def icosphere(r, mat, center=(0, 0, 0), sub=1, jitter=0.0, seed=0, smooth=False, squash=(1, 1, 1)):
    """A low-poly sphere (faceted), optionally lumpy (jitter = fraction of r)."""
    t = (1 + 5 ** 0.5) / 2
    v = [(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
         (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)]
    f = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6),
         (7, 1, 8), (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10),
         (8, 6, 7), (9, 8, 1)]
    V = np.array(v, np.float64)
    V /= np.linalg.norm(V, axis=1, keepdims=True)
    F = np.array(f)
    for _ in range(sub):
        edges = {}
        newf = []
        Vl = list(V)
        def mid(a, b):
            k = (min(a, b), max(a, b))
            if k not in edges:
                p = (Vl[a] + Vl[b]) / 2
                Vl.append(p / np.linalg.norm(p))
                edges[k] = len(Vl) - 1
            return edges[k]
        for a, b, c in F:
            ab, bc, ca = mid(a, b), mid(b, c), mid(c, a)
            newf += [(a, ab, ca), (b, bc, ab), (c, ca, bc), (ab, bc, ca)]
        V = np.array(Vl)
        F = np.array(newf)
    if jitter > 0:
        rng = np.random.default_rng(seed)
        V = V * (1 + jitter * (rng.random((len(V), 1)) * 2 - 1))
    V = V * r * np.asarray(squash) + np.asarray(center)
    return M.from_indexed(V.astype(np.float32), F, mat, smooth=smooth)


# ------------------------------------------------------------ subdivision and camouflage

def subdivide(m, max_edge):
    """Splits triangles (at their longest edge's midpoint) until no edge is
    longer than max_edge, so flat panels can carry painted patterns."""
    V, N, Mi = m.V.copy(), m.N.copy(), m.M.copy()
    for _ in range(12):
        e = np.stack([np.linalg.norm(V[:, 1] - V[:, 0], axis=-1), np.linalg.norm(V[:, 2] - V[:, 1], axis=-1),
                      np.linalg.norm(V[:, 0] - V[:, 2], axis=-1)], -1)
        k = e.argmax(-1)
        big = e.max(-1) > max_edge
        if not big.any():
            break
        idx = np.nonzero(big)[0]
        out_V, out_N, out_M = [V[~big]], [N[~big]], [Mi[~big]]
        kk = k[idx]
        # rotate corners so the longest edge is 0-1
        order = np.stack([kk, (kk + 1) % 3, (kk + 2) % 3], -1)
        Vb = np.take_along_axis(V[idx], order[..., None], 1)
        Nb = np.take_along_axis(N[idx], order[..., None], 1)
        mv = (Vb[:, 0] + Vb[:, 1]) / 2
        mn = Nb[:, 0] + Nb[:, 1]
        mn /= np.linalg.norm(mn, axis=-1, keepdims=True) + 1e-9
        t1 = np.stack([Vb[:, 0], mv, Vb[:, 2]], 1)
        t2 = np.stack([mv, Vb[:, 1], Vb[:, 2]], 1)
        n1 = np.stack([Nb[:, 0], mn, Nb[:, 2]], 1)
        n2 = np.stack([mn, Nb[:, 1], Nb[:, 2]], 1)
        out_V += [t1, t2]; out_N += [n1, n2]; out_M += [Mi[idx], Mi[idx]]
        V, N, Mi = np.concatenate(out_V), np.concatenate(out_N), np.concatenate(out_M)
    return M.Mesh(V, N, Mi)


def noise3(P, seed=0, scale=1.0, octaves=3):
    """Smooth pseudo-noise (sum of random plane waves) of points P (...,3),
    roughly in [-1, 1]."""
    rng = np.random.default_rng(seed)
    out = np.zeros(P.shape[:-1], np.float32)
    amp, tot = 1.0, 0.0
    f = scale
    for _ in range(octaves):
        for _ in range(4):
            d = rng.normal(size=3)
            d /= np.linalg.norm(d)
            out += amp * np.sin((P @ d) * f * 2 * math.pi + rng.random() * 6.283)
            tot += amp
        amp *= 0.5
        f *= 2.1
    return out / (tot ** 0.5 * 1.2)


def camo(m, base_mat, pattern, max_edge=0.12, seed=1, scale=0.9):
    """Paints the base_mat triangles of a mesh in a blotchy camouflage:
    pattern = [(threshold, material id), ...] in ascending threshold; a
    triangle whose noise value exceeds a threshold takes that material."""
    sel = m.M == base_mat
    if not sel.any():
        return m
    part = subdivide(M.Mesh(m.V[sel], m.N[sel], m.M[sel]), max_edge)
    rest = M.Mesh(m.V[~sel], m.N[~sel], m.M[~sel])
    c = part.V.mean(1)
    v = noise3(c, seed, scale)
    mm = part.M.copy()
    for thr, mat in pattern:
        mm[v > thr] = mat
    part.M = mm
    return M.Mesh.concat([rest, part])


def recolor(m, mapping):
    """A copy with materials remapped {from id: to id}."""
    out = m.copy()
    for a, b in mapping.items():
        out.M[m.M == a] = b
    return out


def xf(m, *Ts):
    return m.transformed(M.compose(*Ts))


def mirror_x(m):
    return m.transformed(M.scale(-1, 1, 1))
