"""Effects for vehicles, siege engines and aircraft: explosions, fire, smoke,
muzzle flashes, rocket plumes, debris.

The renderer has no emissive materials, so glowing parts are drawn with
materials whose names start with 'fx_' and whose normals FxMesh turns toward
the key light after the model is turned to its direction: they come out
evenly bright (a stylized glow) while keeping a little faceting. Smoke is
ordinary shaded geometry (soft gray puffs). There is no partial alpha for
geometry, so puffs fade by shrinking.

FxMesh is what the archetypes return from frame(): the model (in model space,
turned by the framework) plus optional parts already in world space (placed
on screen, e.g. where the original's explosion is) and per-direction parts.
"""
import math

import numpy as np

import mesh as M
import render as R
from models import vparts as VP

FX_COLORS = {
    'fx_white': '#fffaf0', 'fx_yellow': '#ffe8a8', 'fx_orange': '#f6c27a', 'fx_red': '#d08a58',
    'fx_flash': '#fff2b8', 'fx_plume': '#fff0c8',
    'smoke_dark': '#4a4846', 'smoke': '#76746f', 'smoke_light': '#a8a6a0', 'smoke_white': '#d8d6d0',
    'dust': '#b4a07a', 'char': '#2a2624', 'ember': '#5a2a18',
}

# How strongly each glow material ignores its own shading (1 = flat lit).
GLOW = {'fx_white': 0.8, 'fx_yellow': 0.6, 'fx_orange': 0.5, 'fx_red': 0.4, 'fx_flash': 0.9, 'fx_plume': 0.85}


def add_fx_mats(mats):
    for name, col in FX_COLORS.items():
        mats.add(name, col, spec=0.0 if name.startswith('fx_') else 0.03, gloss=6)


def _glow_normals(N, k):
    key = R.KEY[None, None, :]
    view = R.VIEW[None, None, :]
    tgt = key * 0.75 + view * 0.45
    tgt = tgt / np.linalg.norm(tgt, axis=-1, keepdims=True)
    out = N * (1 - k)[:, None, None] + tgt * k[:, None, None]
    return out / (np.linalg.norm(out, axis=-1, keepdims=True) + 1e-9)


class FxMesh(M.Mesh):
    """A model-space mesh plus world-space extras; `transformed(facing)`
    returns the turned model, the world parts, the parts for that
    direction (per_dir(d) -> Mesh, in world space) and fixes glow normals."""

    def __init__(self, model, mats, world=None, per_dir=None, tilt=None):
        model = model if model is not None else M.Mesh()
        super().__init__(model.V, model.N, model.M)
        self.mats = mats
        self.world = world
        self.per_dir = per_dir
        self.tilt = tilt        # (degrees, pivot): tip the turned model toward the camera

    def transformed(self, T):
        base = M.Mesh.transformed(self, T)
        if self.tilt is not None and len(base):
            deg, piv = self.tilt
            piv = np.asarray(piv, np.float32)
            base = base.transformed(M.translate(*piv) @ M.rot_x(deg) @ M.translate(*(-piv)))
        out = [base]
        if self.world is not None and len(self.world):
            out.append(self.world)
        if self.per_dir is not None:
            d = facing_row(T)
            if d is not None:
                extra = self.per_dir(d)
                if extra is not None and len(extra):
                    out.append(extra)
        m = M.Mesh.concat(out)
        return apply_glow(m, self.mats)


def apply_glow(m, mats):
    if not len(m):
        return m
    k = np.zeros(len(mats.names), np.float32)
    for name, g in GLOW.items():
        if name in mats.names:
            k[mats[name]] = g
    kt = k[m.M]
    if not kt.any():
        return m
    N = m.N.copy()
    sel = kt > 0
    N[sel] = _glow_normals(N[sel], kt[sel])
    return M.Mesh(m.V, N, m.M)


_FACINGS = [R.facing_matrix(d)[:3, :3] for d in range(8)]


def facing_row(T):
    R3 = np.asarray(T, np.float32)[:3, :3]
    for d, F in enumerate(_FACINGS):
        if np.abs(R3 - F).max() < 1e-4:
            return d
    return None


# ------------------------------------------------------------ building blocks

def _mid(mats, name):
    return mats[name]


def puff(mats, center, r, name, seed=0, sub=2, jitter=0.10, squash=(1, 1, 1)):
    """A soft round cloud ball (smooth shaded, slightly lumpy)."""
    if r <= 0.004:
        return None
    return VP.icosphere(r, mats[name], center, sub=sub, jitter=jitter, seed=seed, squash=squash, smooth=True)


def fire_color(heat):
    """Glow material for a heat 0..1 (1 = white hot)."""
    if heat > 0.85:
        return 'fx_white'
    if heat > 0.6:
        return 'fx_yellow'
    if heat > 0.35:
        return 'fx_orange'
    if heat > 0.18:
        return 'fx_red'
    return None


def smoke_color(dark):
    if dark > 0.66:
        return 'smoke_dark'
    if dark > 0.33:
        return 'smoke'
    return 'smoke_light'


def explosion(mats, center, size, age, seed=0, n=9, smoke_dark=0.6, rise=0.6, spread=1.0, flat=1.0, smoke=True):
    """A fireball growing from a white flash to orange fire and then dark
    smoke that rises and shrinks away. age 0..1 over its life (beyond 1:
    gone). size ~ the fireball radius at its biggest (meters)."""
    if age < 0 or age > 1.0:
        return None
    rng = np.random.default_rng(seed)
    c = np.asarray(center, np.float32)
    parts = []
    for i in range(n):
        d = rng.normal(size=3)
        d[2] = abs(d[2]) * 0.8 + 0.2
        d /= np.linalg.norm(d)
        d[:2] *= spread
        d[2] *= flat
        start = rng.random() * 0.12           # puffs bloom one after another
        life = 0.75 + rng.random() * 0.25
        a = (age - start) / life
        if a < 0 or a > 1:
            continue
        grow = 1 - (1 - min(a / 0.25, 1)) ** 2
        r0 = size * (0.38 + 0.3 * rng.random())
        dist = size * (0.35 + 0.45 * rng.random()) * (0.4 + 0.6 * grow)
        pos = c + d * dist + np.array([0, 0, rise * size * a * a * 1.6], np.float32)
        heat = 1.0 - a * 1.6 - 0.25 * (i % 3) / 2
        if a > 0.55:
            r = r0 * (1.15 - (a - 0.55) / 0.45 * 1.15)   # smoke shrinks away
        else:
            r = r0 * (0.35 + 0.8 * grow)
        name = fire_color(heat)
        if name is None:
            name = smoke_color(smoke_dark + 0.3 * (a - 0.5)) if smoke else 'fx_red'
        parts.append(puff(mats, pos, r, name, seed=seed * 31 + i))
        # a bright core inside the early fireball
        if a < 0.35:
            parts.append(puff(mats, pos * 0.6 + c * 0.4, r * 0.7, 'fx_white' if a < 0.15 else 'fx_yellow',
                              seed=seed * 31 + i + 7))
    return M.Mesh.concat(parts)


def smoke_column(mats, base, size, age, seed=0, n=6, color=0.75, drift=(0.15, 0.1), height=2.0):
    """Puffs rising from `base` (a burning wreck): each puff loops through
    its own life, so the column keeps going; `age` is the action time."""
    rng = np.random.default_rng(seed)
    b = np.asarray(base, np.float32)
    parts = []
    for i in range(n):
        ph = (age * 1.0 + i / n + rng.random() * 0.1) % 1.0
        z = ph * height * size
        pos = b + np.array([drift[0] * ph * size * 3 + (rng.random() - 0.5) * size * 0.4,
                            drift[1] * ph * size * 3, z], np.float32)
        r = size * (0.25 + 0.55 * math.sin(math.pi * min(ph * 1.2, 1.0)))
        parts.append(puff(mats, pos, r, smoke_color(color - 0.3 * ph), seed=seed * 17 + i))
    return M.Mesh.concat(parts)


def flames(mats, base, size, t, seed=0, n=5):
    """Licking flames at a point (burning wreck): small glowing tongues."""
    rng = np.random.default_rng(seed)
    b = np.asarray(base, np.float32)
    parts = []
    for i in range(n):
        ph = (t * 2 + i / n) % 1.0
        off = np.array([(rng.random() - 0.5) * size, (rng.random() - 0.5) * size, ph * size * 0.8], np.float32)
        r = size * 0.28 * (1 - ph) + 0.01
        parts.append(puff(mats, b + off, r, 'fx_yellow' if ph < 0.3 else ('fx_orange' if ph < 0.7 else 'fx_red'),
                          seed=seed + i, squash=(0.8, 0.8, 1.4)))
    return M.Mesh.concat(parts)


def muzzle_flash(mats, origin, direction, size, seed=0, prongs=5):
    """A star-shaped flash at a muzzle, pointing along `direction`."""
    o = np.asarray(origin, np.float32)
    d = np.asarray(direction, np.float32)
    d = d / (np.linalg.norm(d) + 1e-9)
    rng = np.random.default_rng(seed)
    parts = [puff(mats, o + d * size * 0.35, size * 0.42, 'fx_white', seed=seed, sub=1, jitter=0.1)]
    parts.append(puff(mats, o + d * size * 0.7, size * 0.36, 'fx_yellow', seed=seed + 1, sub=1, jitter=0.2))
    up = np.array([0, 0, 1], np.float32) if abs(d[2]) < 0.9 else np.array([1, 0, 0], np.float32)
    u = np.cross(d, up); u /= np.linalg.norm(u)
    v = np.cross(d, u)
    for i in range(prongs):
        a = 2 * math.pi * i / prongs + rng.random() * 0.5
        side = (u * math.cos(a) + v * math.sin(a))
        tip = o + d * size * (0.9 + 0.5 * rng.random()) + side * size * 0.55
        parts.append(VP.tube(o + d * size * 0.2, tip, size * 0.16, 0.004, mats['fx_orange' if i % 2 else 'fx_yellow'], seg=5))
    parts.append(VP.tube(o, o + d * size * 1.8, size * 0.2, 0.004, mats['fx_yellow'], seg=6))
    return M.Mesh.concat(parts)


def smoke_puffs(mats, origin, direction, size, age, seed=0, n=4, color=0.2):
    """Gun smoke blown out of a muzzle along `direction`, drifting and
    shrinking (age 0..1)."""
    if age < 0 or age > 1:
        return None
    o = np.asarray(origin, np.float32)
    d = np.asarray(direction, np.float32)
    d = d / (np.linalg.norm(d) + 1e-9)
    rng = np.random.default_rng(seed)
    parts = []
    for i in range(n):
        k = (i + 1) / n
        pos = o + d * size * (0.5 + 1.6 * k) * (0.5 + age) + np.array([0, 0, size * age * 0.35], np.float32) \
            + rng.normal(size=3).astype(np.float32) * size * 0.15
        r = size * (0.3 + 0.35 * k) * (0.6 + 0.8 * age) * max(0.0, 1 - age ** 1.5)
        parts.append(puff(mats, pos, r, smoke_color(color + 0.25 * age), seed=seed + i))
    return M.Mesh.concat(parts)


def plume(mats, nozzle, direction, length, radius, t=0.0, seed=0, smoke=True, n_smoke=5):
    """A rocket exhaust: a glowing cone out of the nozzle and puffs of smoke
    trailing behind (pointing along `direction`, away from the rocket)."""
    o = np.asarray(nozzle, np.float32)
    d = np.asarray(direction, np.float32)
    d = d / (np.linalg.norm(d) + 1e-9)
    parts = [VP.tube(o, o + d * length * 0.45, radius, radius * 0.3, mats['fx_white'], seg=8),
             VP.tube(o + d * length * 0.1, o + d * length, radius * 1.15, 0.004, mats['fx_yellow'], seg=8)]
    if smoke:
        rng = np.random.default_rng(seed)
        for i in range(n_smoke):
            k = (i + (t * 3) % 1.0) / n_smoke
            pos = o + d * length * (1 + 2.2 * k) + rng.normal(size=3).astype(np.float32) * radius * 0.6
            parts.append(puff(mats, pos, radius * (1.2 + 2.0 * k), 'smoke_white' if k < 0.6 else 'smoke_light',
                              seed=seed + i))
    return M.Mesh.concat(parts)


def debris(mats, center, size, age, seed=0, n=8, names=('char', 'smoke_dark'), gravity=1.0, speed=1.0):
    """Chunks thrown out of an explosion along ballistic arcs, landing on
    the ground (z = 0)."""
    rng = np.random.default_rng(seed)
    c = np.asarray(center, np.float32)
    parts = []
    for i in range(n):
        d = rng.normal(size=3)
        d[2] = abs(d[2]) + 0.6
        d /= np.linalg.norm(d)
        v = d * size * (2.5 + 2 * rng.random()) * speed
        t = age * 0.8
        p = c + v * t + np.array([0, 0, -9.0 * gravity * size * t * t], np.float32)
        p[2] = max(p[2], 0.02)
        s = size * (0.08 + 0.1 * rng.random())
        rot = M.euler(age * 500 * rng.random(), age * 400 * rng.random(), rng.random() * 360)
        parts.append(M.box(s, s * 0.7, s * 0.5, mats[names[i % len(names)]]).transformed(M.translate(*p) @ rot))
    return M.Mesh.concat(parts)


def ground_scorch(mats, center, r, name='char', seed=0):
    """A flat dark scorch patch on the ground."""
    rng = np.random.default_rng(seed)
    pts = []
    for i in range(10):
        a = 2 * math.pi * i / 10
        rr = r * (0.75 + 0.35 * rng.random())
        pts.append((center[0] + rr * math.cos(a), center[1] + rr * math.sin(a)))
    return VP.prism(pts, 0.01, mats[name], z0=0.0)
