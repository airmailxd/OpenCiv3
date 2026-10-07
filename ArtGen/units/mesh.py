"""Triangle-soup meshes and modeling primitives for the unit renderer.

A Mesh is a triangle soup: V (T,3,3) corner positions, N (T,3,3) corner
normals and M (T,) material indexes into a Materials table. Smooth parts get
per-corner normals from the analytic surface; hard parts get face normals.

Coordinates (model space): x = the unit's right, y = forward, z = up, meters.
A standing human is ~1.8 m tall with feet at z = 0.
"""
import math

import numpy as np


# ---------------------------------------------------------------- materials

class Materials:
    """A table of named materials. `color` is sRGB 0-255 (or a hex string);
    `tint` materials are civ-colored: they render (shaded in gray) into the
    _tint sheet. `spec` is the specular strength, `gloss` its exponent."""

    def __init__(self):
        self.names, self.color, self.tint, self.spec, self.gloss, self.metal = [], [], [], [], [], []

    def add(self, name, color, tint=False, spec=0.08, gloss=12.0, metal=False):
        if isinstance(color, str):
            c = color.lstrip('#')
            color = tuple(int(c[i:i + 2], 16) for i in (0, 2, 4))
        if name in self.names:
            i = self.names.index(name)
            self.color[i], self.tint[i], self.spec[i], self.gloss[i], self.metal[i] = color, tint, spec, gloss, metal
            return i
        self.names.append(name)
        self.color.append(tuple(color))
        self.tint.append(bool(tint))
        self.spec.append(spec)
        self.gloss.append(gloss)
        self.metal.append(metal)
        return len(self.names) - 1

    def __getitem__(self, name):
        return self.names.index(name)

    def __contains__(self, name):
        return name in self.names

    def arrays(self):
        col = np.array(self.color, np.float32) / 255.0
        lin = col ** 2.2
        return (lin, np.array(self.tint, bool), np.array(self.spec, np.float32),
                np.array(self.gloss, np.float32), np.array(self.metal, bool))


# ---------------------------------------------------------------- mesh

class Mesh:
    def __init__(self, V=None, N=None, M=None):
        self.V = np.zeros((0, 3, 3), np.float32) if V is None else np.asarray(V, np.float32)
        self.N = np.zeros((0, 3, 3), np.float32) if N is None else np.asarray(N, np.float32)
        self.M = np.zeros((0,), np.int32) if M is None else np.asarray(M, np.int32)

    def __len__(self):
        return len(self.V)

    def copy(self):
        return Mesh(self.V.copy(), self.N.copy(), self.M.copy())

    def transformed(self, T):
        """A copy transformed by a 4x4 matrix (normals by its rotation part)."""
        T = np.asarray(T, np.float32)
        R, t = T[:3, :3], T[:3, 3]
        V = self.V @ R.T + t
        # Normal matrix (handles non-uniform scale).
        Ni = np.linalg.inv(R).T
        N = self.N @ Ni.T
        N /= np.linalg.norm(N, axis=-1, keepdims=True) + 1e-12
        if np.linalg.det(R) < 0:  # mirrored: keep the winding consistent
            V, N = V[:, ::-1], N[:, ::-1]
        return Mesh(V, N, self.M.copy())

    def with_material(self, m):
        return Mesh(self.V, self.N, np.full(len(self.V), m, np.int32))

    @staticmethod
    def concat(meshes):
        meshes = [m for m in meshes if m is not None and len(m)]
        if not meshes:
            return Mesh()
        return Mesh(np.concatenate([m.V for m in meshes]), np.concatenate([m.N for m in meshes]),
                    np.concatenate([m.M for m in meshes]))

    def __add__(self, other):
        return Mesh.concat([self, other])


def from_indexed(verts, faces, mat, normals=None, smooth=True):
    """A Mesh from an indexed mesh. With smooth=True and no normals, vertex
    normals are the area-weighted average of the faces around each vertex."""
    verts = np.asarray(verts, np.float32)
    faces = np.asarray(faces, np.int64)
    V = verts[faces]
    fn = np.cross(V[:, 1] - V[:, 0], V[:, 2] - V[:, 0])
    if smooth:
        if normals is None:
            vn = np.zeros_like(verts)
            for k in range(3):
                np.add.at(vn, faces[:, k], fn)
            normals = vn
        normals = np.asarray(normals, np.float32)
        normals = normals / (np.linalg.norm(normals, axis=-1, keepdims=True) + 1e-12)
        N = normals[faces]
    else:
        n = fn / (np.linalg.norm(fn, axis=-1, keepdims=True) + 1e-12)
        N = np.repeat(n[:, None], 3, 1)
    return Mesh(V, N, np.full(len(faces), mat, np.int32))


# ---------------------------------------------------------------- transforms

def translate(x, y=0.0, z=0.0):
    if np.ndim(x):
        x, y, z = x
    T = np.eye(4, dtype=np.float32)
    T[:3, 3] = (x, y, z)
    return T


def scale(sx, sy=None, sz=None):
    sy = sx if sy is None else sy
    sz = sx if sz is None else sz
    return np.diag([sx, sy, sz, 1]).astype(np.float32)


def rot_x(deg):
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    T = np.eye(4, dtype=np.float32)
    T[1:3, 1:3] = [[c, -s], [s, c]]
    return T


def rot_y(deg):
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    T = np.eye(4, dtype=np.float32)
    T[0, 0], T[0, 2], T[2, 0], T[2, 2] = c, s, -s, c
    return T


def rot_z(deg):
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    T = np.eye(4, dtype=np.float32)
    T[0:2, 0:2] = [[c, -s], [s, c]]
    return T


def euler(pitch=0.0, roll=0.0, yaw=0.0):
    """Rotation of a joint: yaw (about z, + turns left) of pitch (about x, +
    swings a hanging limb forward) of roll (about y, + swings a hanging limb to
    the unit's left)."""
    return rot_z(yaw) @ rot_x(pitch) @ rot_y(roll)


def compose(*Ts):
    out = np.eye(4, dtype=np.float32)
    for T in Ts:
        out = out @ T
    return out


def look_along(a, b):
    """A transform taking the +z axis segment [0, 1] onto the segment a->b
    (scaled by its length along z only)."""
    a, b = np.asarray(a, np.float32), np.asarray(b, np.float32)
    d = b - a
    L = float(np.linalg.norm(d))
    z = d / (L + 1e-12)
    up = np.array([0, 1, 0], np.float32) if abs(z[1]) < 0.9 else np.array([1, 0, 0], np.float32)
    x = np.cross(up, z)
    x /= np.linalg.norm(x)
    y = np.cross(z, x)
    T = np.eye(4, dtype=np.float32)
    T[:3, 0], T[:3, 1], T[:3, 2], T[:3, 3] = x, y, z * L, a
    return T


# ---------------------------------------------------------------- primitives

def box(sx, sy, sz, mat, center=(0, 0, 0), bevel=0.0):
    """An axis-aligned box of size sx, sy, sz (flat shaded); `bevel` > 0 cuts
    its edges for a softer, more modern look."""
    if bevel > 0:
        return rounded_box(sx, sy, sz, mat, center, bevel)
    hx, hy, hz = sx / 2, sy / 2, sz / 2
    v = np.array([[x, y, z] for x in (-hx, hx) for y in (-hy, hy) for z in (-hz, hz)], np.float32) + center
    f = [(0, 1, 3), (0, 3, 2), (4, 6, 7), (4, 7, 5), (0, 4, 5), (0, 5, 1),
         (2, 3, 7), (2, 7, 6), (0, 2, 6), (0, 6, 4), (1, 5, 7), (1, 7, 3)]
    return from_indexed(v, f, mat, smooth=False)


def superellipse(a, b, p=6.0):
    """Radius function of a superellipse section (|x/a|^p + |y/b|^p = 1) for
    loft(): returns (rx, ry) callables. p=2 is an ellipse, large p a
    rounded rectangle."""
    def r(ang):
        c, s = np.abs(np.cos(ang)), np.abs(np.sin(ang))
        return (c ** p / a ** p + s ** p / b ** p + 1e-12) ** (-1.0 / p)
    return r, r


def rounded_box(sx, sy, sz, mat, center=(0, 0, 0), r=0.02, seg=16, p=6.0):
    """A box with rounded edges and corners (smooth shaded)."""
    r = min(r, sx / 2.5, sy / 2.5, sz / 2.5)
    rings = []
    for k in range(4):
        t = k / 3 * (math.pi / 2)
        inset = r * (1 - math.sin(t))
        z = -sz / 2 + r * (1 - math.cos(t))
        rings.append((z, inset))
    full = rings + [(-zz, ins) for zz, ins in reversed(rings)]
    lofted = []
    for z, ins in full:
        fx, fy = superellipse(max(sx / 2 - ins, 1e-4), max(sy / 2 - ins, 1e-4), p)
        lofted.append((z, fx, fy))
    m = loft(lofted, mat, seg)
    return m.transformed(translate(*center))


def sphere_grid(nv, nu):
    th = np.linspace(0, math.pi, nv + 1)
    ph = np.linspace(0, 2 * math.pi, nu, endpoint=False)
    T, P = np.meshgrid(th, ph, indexing='ij')
    return np.stack([np.sin(T) * np.cos(P), np.sin(T) * np.sin(P), np.cos(T)], -1).astype(np.float32)


def grid_faces(nv, nu, closed_u=True):
    f = []
    cols = nu if closed_u else nu - 1
    for i in range(nv):
        for j in range(cols):
            a = i * nu + j
            b = i * nu + (j + 1) % nu
            c = (i + 1) * nu + j
            d = (i + 1) * nu + (j + 1) % nu
            f.append((a, c, d))
            f.append((a, d, b))
    return np.array(f, np.int64)


def ellipsoid(rx, ry, rz, mat, center=(0, 0, 0), seg=12, smooth=True):
    nv = max(4, seg // 2 + 1)
    g = sphere_grid(nv, seg)
    v = g * np.array([rx, ry, rz], np.float32)
    n = g / np.array([rx, ry, rz], np.float32)
    faces = grid_faces(nv, seg)
    # drop degenerate triangles at the poles
    vv = v.reshape(-1, 3)[faces]
    area = np.linalg.norm(np.cross(vv[:, 1] - vv[:, 0], vv[:, 2] - vv[:, 0]), axis=-1)
    faces = faces[area > 1e-10]
    return from_indexed(v.reshape(-1, 3) + np.asarray(center, np.float32), faces, mat,
                        normals=n.reshape(-1, 3) if smooth else None, smooth=smooth)


def sphere(r, mat, center=(0, 0, 0), seg=12):
    return ellipsoid(r, r, r, mat, center, seg)


def loft(rings, mat, seg=12, cap_bottom=True, cap_top=True, smooth=True, phase=0.0):
    """A smooth body from horizontal elliptical rings, each (z, rx, ry[, dx, dy]),
    bottom to top. Superb for torsos, skirts, helmets, heads, bottles, tents.
    rx/ry may be callables of the angle for non-elliptical sections."""
    ang = np.linspace(0, 2 * math.pi, seg, endpoint=False) + phase
    pts = []
    for r in rings:
        z, rx, ry = r[0], r[1], r[2]
        dx, dy = (r[3], r[4]) if len(r) > 4 else (0.0, 0.0)
        RX = rx(ang) if callable(rx) else rx
        RY = ry(ang) if callable(ry) else ry
        pts.append(np.stack([dx + RX * np.cos(ang), dy + RY * np.sin(ang), np.full(seg, z)], -1))
    v = np.concatenate(pts).astype(np.float32)
    nr = len(rings)
    faces = list(grid_faces(nr - 1, seg))
    if cap_bottom:
        c = len(v)
        v = np.vstack([v, [[rings[0][3] if len(rings[0]) > 4 else 0, rings[0][4] if len(rings[0]) > 4 else 0, rings[0][0]]]])
        faces += [(c, (j + 1) % seg, j) for j in range(seg)]
    if cap_top:
        c = len(v)
        last = rings[-1]
        v = np.vstack([v, [[last[3] if len(last) > 4 else 0, last[4] if len(last) > 4 else 0, last[0]]]])
        base = (nr - 1) * seg
        faces += [(c, base + j, base + (j + 1) % seg) for j in range(seg)]
    return from_indexed(v, np.array(faces), mat, smooth=smooth)


def cylinder(r0, r1, h, mat, seg=10, z0=0.0, caps=True, smooth=True):
    """A (tapered) cylinder along +z from z0 to z0 + h."""
    m = loft([(z0, r0, r0), (z0 + h, r1, r1)], mat, seg, caps, caps, smooth=False)
    if smooth:
        # side normals: radial with the taper slope
        ang = np.arctan2(m.V[..., 1], m.V[..., 0])
        slope = (r0 - r1) / max(h, 1e-6)
        n = np.stack([np.cos(ang), np.sin(ang), np.full_like(ang, slope)], -1)
        n /= np.linalg.norm(n, axis=-1, keepdims=True)
        side = np.abs(m.N[:, 0, 2]) < 0.99
        m.N[side] = n[side]
    return m


def cone(r, h, mat, seg=10, z0=0.0):
    return cylinder(r, 0.0001, h, mat, seg, z0)


def capsule(a, b, r0, r1, mat, seg=10, rings=3):
    """A tapered capsule (limb) between points a and b with radii r0 at a and
    r1 at b: smooth, with rounded ends."""
    a, b = np.asarray(a, np.float32), np.asarray(b, np.float32)
    L = float(np.linalg.norm(b - a))
    prof = []
    for i in range(rings, 0, -1):  # bottom hemisphere
        t = (i / rings) * (math.pi / 2)
        prof.append((-r0 * math.sin(t), r0 * math.cos(t)))
    prof.append((0.0, r0))
    prof.append((L, r1))
    for i in range(1, rings + 1):
        t = (i / rings) * (math.pi / 2)
        prof.append((L + r1 * math.sin(t), r1 * math.cos(t)))
    m = lathe(prof, mat, seg)
    T = look_along(a, a + (b - a) / (L + 1e-9))  # unit length along z
    return m.transformed(T)


def lathe(profile, mat, seg=12, smooth=True):
    """A surface of revolution about z from a profile [(z, r), ...] bottom to
    top. Ends with r = 0 close the shape."""
    ang = np.linspace(0, 2 * math.pi, seg, endpoint=False)
    zs = np.array([p[0] for p in profile], np.float32)
    rs = np.array([max(p[1], 1e-5) for p in profile], np.float32)
    v = np.stack([rs[:, None] * np.cos(ang)[None], rs[:, None] * np.sin(ang)[None],
                  np.repeat(zs[:, None], seg, 1)], -1).reshape(-1, 3)
    faces = grid_faces(len(profile) - 1, seg)
    # analytic normals from the profile slope
    dz = np.gradient(zs)
    dr = np.gradient(rs)
    nz = -dr
    nr = dz
    ln = np.sqrt(nz ** 2 + nr ** 2) + 1e-9
    nz, nr = nz / ln, nr / ln
    n = np.stack([nr[:, None] * np.cos(ang)[None], nr[:, None] * np.sin(ang)[None],
                  np.repeat(nz[:, None], seg, 1)], -1).reshape(-1, 3)
    vv = v[faces]
    area = np.linalg.norm(np.cross(vv[:, 1] - vv[:, 0], vv[:, 2] - vv[:, 0]), axis=-1)
    faces = faces[area > 1e-12]
    return from_indexed(v, faces, mat, normals=n if smooth else None, smooth=smooth)


def extrude(poly, depth, mat, bevel=0.0, smooth_sides=False):
    """A flat shape: a 2D polygon [(x, z), ...] (counter-clockwise, in the
    x-z plane, like a blade or an axe head seen from the side) extruded
    `depth` along y, centered on y = 0. Convex or star-shaped around its
    centroid works best (the caps are fanned from the centroid)."""
    p = np.asarray(poly, np.float32)
    n = len(p)
    c = p.mean(0)
    hy = depth / 2
    tris = []
    norms = []

    def P(xz, y):
        return np.array([xz[0], y, xz[1]], np.float32)

    for i in range(n):
        a, b = p[i], p[(i + 1) % n]
        # caps (front: -y, back: +y)
        tris.append([P(c, -hy), P(b, -hy), P(a, -hy)])
        norms.append([[0, -1, 0]] * 3)
        tris.append([P(c, hy), P(a, hy), P(b, hy)])
        norms.append([[0, 1, 0]] * 3)
        # side
        e = b - a
        sn = np.array([e[1], 0, -e[0]], np.float32)
        sn /= np.linalg.norm(sn) + 1e-9
        tris.append([P(a, -hy), P(b, -hy), P(b, hy)])
        tris.append([P(a, -hy), P(b, hy), P(a, hy)])
        norms += [[sn] * 3, [sn] * 3]
    V = np.array(tris, np.float32)
    N = np.array(norms, np.float32)
    if bevel > 0:
        # soften: tilt cap normals outwards near the rim
        pass
    return Mesh(V, N, np.full(len(V), mat, np.int32))


def blade(length, width, thick, mat, tip=0.25, z0=0.0):
    """A double-edged blade along +z: a flattened diamond cross-section, smooth
    across the faces so it catches the light like steel."""
    w = width / 2
    t = thick / 2
    L = length
    # cross-section ring (x: width, y: thickness) at several heights
    def ring(z, s):
        return [(-w * s, 0, z), (0, -t * s, z), (w * s, 0, z), (0, t * s, z)]
    rings = [ring(z0, 1.0), ring(z0 + L * (1 - tip), 1.0)]
    v = np.array(rings[0] + rings[1] + [(0, 0, z0 + L)], np.float32)
    f = []
    for j in range(4):
        a, b = j, (j + 1) % 4
        f += [(a, b, 4 + b), (a, 4 + b, 4 + a), (4 + a, 4 + b, 8)]
    f += [(0, 2, 1), (0, 3, 2)]
    return from_indexed(v, f, mat, smooth=False)


def plate(points, mat, thick=0.01, smooth=False):
    """A thin flat plate from a convex polygon of 3D points (fan)."""
    p = np.asarray(points, np.float32)
    c = p.mean(0)
    n = np.cross(p[1] - p[0], p[2] - p[0])
    n /= np.linalg.norm(n) + 1e-9
    V, N = [], []
    for i in range(len(p)):
        a, b = p[i], p[(i + 1) % len(p)]
        V.append([c, a, b]); N.append([n] * 3)
        V.append([c - n * thick, b - n * thick, a - n * thick]); N.append([-n] * 3)
    return Mesh(np.array(V, np.float32), np.array(N, np.float32), np.full(len(V), mat, np.int32))


def disc(rx, rz, depth, mat, seg=20, dome=0.0):
    """A round shield-like disc in the x-z plane, facing -y (its front), with
    an optional dome bulging towards -y."""
    ang = np.linspace(0, 2 * math.pi, seg, endpoint=False)
    rings = []
    k = 4
    for i in range(k + 1):
        s = 1 - i / k
        y = -dome * (1 - s * s) - depth / 2
        rings.append((s, y))
    verts = []
    for s, y in rings:
        for a in ang:
            verts.append((rx * s * math.cos(a), y, rz * s * math.sin(a)))
    # back face ring
    back0 = len(verts)
    for a in ang:
        verts.append((rx * math.cos(a), depth / 2, rz * math.sin(a)))
    center_back = len(verts)
    verts.append((0, depth / 2, 0))
    v = np.array(verts, np.float32)
    faces = []
    for i in range(k):
        for j in range(seg):
            a = i * seg + j
            b = i * seg + (j + 1) % seg
            c = (i + 1) * seg + j
            d = (i + 1) * seg + (j + 1) % seg
            faces += [(a, b, d), (a, d, c)]
    for j in range(seg):  # rim
        a, b = j, (j + 1) % seg
        faces += [(back0 + a, back0 + b, b), (back0 + a, b, a)]
        faces.append((center_back, back0 + b, back0 + a))
    m = from_indexed(v, np.array(faces), mat, smooth=True)
    return m
