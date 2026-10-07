"""Stylized tree sprites (broadleaf, pine, jungle palm and bush, reed tufts)
drawn at render size (SS px per original px), and placing them in a footprint."""
import numpy as np
import scipy.ndimage as ndi

from render import LIGHT, SS, lerp, saturate, smoothstep, stamp, shadow_stamp


class Sprite:
    def __init__(self, rgba, ax, ay, shadow=None, sax=0, say=0, halo=0.0):
        if halo:
            rgba, ax, ay = _halo(rgba, ax, ay, halo)
        self.rgba, self.ax, self.ay = rgba, ax, ay  # anchor = trunk base
        self.shadow, self.sax, self.say = shadow, sax, say


def _halo(rgba, ax, ay, strength, pad=5):
    """Adds a soft dark occlusion halo around (mostly below-right of) a sprite, which
    darkens whatever is behind it and separates overlapping crowns."""
    h, w = rgba.shape[:2]
    out = np.zeros((h + 2 * pad, w + 2 * pad, 4))
    out[pad:pad + h, pad:pad + w] = rgba
    a = out[..., 3]
    halo = ndi.gaussian_filter(np.roll(np.roll(a, 2, 0), 1, 1), 2.2)
    halo = np.clip(halo * 1.3, 0, 1) * strength
    under = np.zeros_like(out)
    under[..., 3] = halo
    under = under * (1 - a[..., None]) + out
    return under, ax + pad, ay + pad


def _grid(h, w):
    y, x = np.mgrid[0:h, 0:w].astype(float)
    return y + 0.5, x + 0.5


def _shade(n, albedo, amb=0.42, diff=0.75, rim=0.0, rim_color=None):
    lam = np.clip(n @ LIGHT, 0, 1)
    col = albedo * (amb + diff * lam)[..., None]
    if rim:
        # A rim of light on the edges that face the light.
        lxy = np.clip(n[..., 0] * LIGHT[0] + n[..., 1] * LIGHT[1], 0, 1) / np.hypot(LIGHT[0], LIGHT[1])
        r = (1 - np.clip(n[..., 2], 0, 1)) ** 2 * lxy * rim
        col = col + (rim_color if rim_color is not None else albedo * 1.6) * r[..., None]
    return np.clip(col, 0, 1)


def _blob_layer(h, w, prims, noise_amp, rng, cols):
    """Union of shaded domes. prims: rows of (cx, cy, r, z0, color index).
    Returns rgb, coverage, height."""
    y, x = _grid(h, w)
    best_z = np.full((h, w), -1e9)
    best_i = np.full((h, w), -1)
    sd_min = np.full((h, w), 1e9)
    for i, (cx, cy, r, z0, ci) in enumerate(prims):
        d2 = (x - cx) ** 2 + ((y - cy) * 1.0) ** 2
        sd = np.sqrt(d2) - r
        z = z0 + np.sqrt(np.clip(r * r - d2, 0, None))
        take = (sd < 0.5) & (z > best_z)
        best_z = np.where(take, z, best_z)
        best_i = np.where(take, i, best_i)
        sd_min = np.minimum(sd_min, sd)
    cov = np.clip(0.5 - sd_min, 0, 1)
    zz = np.where(best_i >= 0, best_z, 0)
    # Leafy bumps.
    bump = (ndi.gaussian_filter(rng.random((h, w)), 1.3) - 0.5) * noise_amp * 3.0
    zz = zz + bump * (best_i >= 0)
    gy, gx = np.gradient(ndi.gaussian_filter(zz, 0.6))
    n = np.stack([-gx, -gy, np.ones_like(zz) * 0.9], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    ci = np.array([p[4] for p in prims])[np.clip(best_i, 0, None)]
    albedo = np.asarray(cols)[ci]
    return n, albedo, cov, zz, best_i


def broadleaf(rng, R, leaf, dark, light, trunk, kind='broadleaf'):
    """A round broadleaf crown of a few leafy clumps on a short trunk. R = crown radius (render px)."""
    w = int(R * 2.8) + 4
    h = int(R * 3.2) + 4
    cx, base = w / 2, h - 2
    cy = base - R * 1.55
    prims = []
    # trunk (color 1), drawn as a stack of small circles
    tr = max(R * 0.12, 1.3)
    for k in range(8):
        prims.append((cx + rng.normal(0, 0.25), base - tr - k * (R * 0.95 / 8), tr * (1.1 - k * 0.03), -50, 1))
    nclump = rng.integers(5, 8)
    for k in range(nclump):
        a = rng.uniform(0, 2 * np.pi)
        rr = R * rng.uniform(0.25, 0.5)
        r = R * rng.uniform(0.42, 0.58)
        prims.append((cx + np.cos(a) * rr, cy + np.sin(a) * rr * 0.85, r, np.sin(a) * R * 0.3, 0))
    prims.append((cx - R * 0.08, cy - R * 0.2, R * 0.55, R * 0.25, 0))
    n, alb, cov, zz, idx = _blob_layer(h, w, prims, R * 0.35, rng, [leaf, trunk])
    y, x = _grid(h, w)
    # Blend in the normal of the crown as a whole, so each tree reads as one round mass.
    gx, gy = (x - cx) / (R * 1.05), (y - cy) / (R * 1.05)
    gz = np.sqrt(np.clip(1 - gx * gx - gy * gy, 0.05, 1))
    ng = np.stack([gx, gy, gz], -1)
    crown = (idx != 1)[..., None]
    n = np.where(crown, 0.55 * n + 0.6 * ng, n)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    # Darker underside of the crown (ambient occlusion).
    t = np.clip((y - (cy - R)) / (2 * R), 0, 1)
    ao = 1.0 - 0.45 * smoothstep(0.4, 1.0, t)
    rgb = _shade(n, alb, amb=0.26, diff=1.05, rim=0.75, rim_color=light)
    rgb = lerp(rgb, dark, (1 - ao) * (idx != 1))
    rgb = np.where(idx[..., None] == 1, rgb * (0.7 + 0.5 * (x < cx))[..., None], rgb)
    a = cov
    spr = np.concatenate([rgb * a[..., None], a[..., None]], -1)
    sh = _shadow_ellipse(R * 1.15, R * 0.5)
    return Sprite(spr, int(round(cx)), int(round(base)), sh, sh.shape[1] // 2 - int(R * 0.55), sh.shape[0] // 2 - int(R * 0.1), halo=0.6)


def pine(rng, W, Hc, leaf, dark, light, trunk):
    """A tiered conifer. W = half width at the bottom, Hc = height of the crown (render px)."""
    w = int(W * 2.6) + 4
    th = Hc * 0.14
    h = int(Hc + th) + 4
    cx, base = w / 2 + rng.normal(0, 0.3), h - 2
    y, x = _grid(h, w)
    t = (base - th - y) / Hc                       # 0 at the bottom of the crown, 1 at the tip
    tc = np.clip(t, 0, 1)
    tiers = rng.integers(4, 6)
    f = np.mod(tc * tiers + rng.uniform(0, 0.3), 1.0)  # 0 at the bottom of each tier
    prof = (1 - tc) ** 0.95 * (0.62 + 0.38 * (1 - f) ** 1.5)
    r = W * np.clip(prof, 0, None)
    dx = x - cx
    sd = np.where((t > 0) & (t < 1), np.abs(dx) - r, 1e3)
    sd = np.minimum(sd, np.where(t >= 1, np.hypot(dx, (y - (base - th - Hc)) * 2), 1e3))
    cov = np.clip(0.5 - sd, 0, 1) * (t > -0.02)
    u = np.clip(dx / np.maximum(r, 0.5), -1, 1)
    ny = -0.35 - 0.45 * (1 - f)                  # tier tops face up, undersides face out
    n = np.stack([u * 0.95, ny * np.ones_like(u), np.sqrt(np.clip(1 - u * u, 0.05, 1))], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    alb = np.broadcast_to(leaf, (h, w, 3)).copy()
    rgb = _shade(n, alb, amb=0.38, diff=0.9, rim=0.6, rim_color=light)
    under = smoothstep(0.25, 0.0, f) * 0.35 + 0.18 * (1 - t)  # shade under each tier
    rgb = lerp(rgb, dark, np.clip(under, 0, 0.6))
    # trunk
    tw = max(W * 0.16, 1.2)
    tsd = np.where((y > base - th - 2) & (y < base), np.abs(dx) - tw, 1e3)
    tcov = np.clip(0.5 - tsd, 0, 1) * (1 - cov)
    tcol = trunk * (0.75 + 0.35 * (dx < 0))[..., None]
    rgb = rgb * cov[..., None] + tcol * tcov[..., None]
    a = np.clip(cov + tcov, 0, 1)
    rgb = np.where(a[..., None] > 0, rgb / np.maximum(a[..., None], 1e-6), 0)
    spr = np.concatenate([rgb * a[..., None], a[..., None]], -1)
    sh = _shadow_ellipse(W * 1.25, W * 0.55)
    return Sprite(spr, int(round(cx)), int(round(base)), sh, sh.shape[1] // 2 - int(W * 0.7), sh.shape[0] // 2 - int(W * 0.12), halo=0.45)


def palm(rng, R, leaf, dark, light, trunk):
    """A jungle palm: a thin leaning trunk with a star of drooping fronds."""
    tall = R * rng.uniform(1.5, 2.1)
    w = int(R * 3.2) + 6
    h = int(tall + R * 1.6) + 6
    base_x, base = w / 2, h - 2
    lean = rng.normal(0, 0.18) * R
    top = (base_x + lean, base - tall)
    prims = []
    tr = max(R * 0.12, 1.2)
    for k in range(10):
        s = k / 9
        prims.append((base_x + lean * s * s, base - tr - s * (tall - tr), tr * (1.15 - 0.3 * s), -40, 1))
    nf = rng.integers(6, 9)
    a0 = rng.uniform(0, 2 * np.pi)
    for i in range(nf):
        a = a0 + i * 2 * np.pi / nf + rng.normal(0, 0.2)
        L = R * rng.uniform(1.0, 1.35)
        dxu, dyu = np.cos(a), np.sin(a) * 0.55
        steps = 9
        for k in range(steps):
            s = (k + 1) / steps
            px = top[0] + dxu * L * s
            py = top[1] + dyu * L * s - R * 0.45 * s + R * 0.75 * s * s   # arch up then droop
            rad = R * 0.2 * (1 - s * 0.7)
            z = 6 - 6 * s + (2 if dyu > 0 else 0)
            prims.append((px, py, rad, z, 0))
    prims.append((top[0], top[1], R * 0.22, 8, 0))
    n, alb, cov, zz, idx = _blob_layer(h, w, prims, R * 0.15, rng, [leaf, trunk])
    rgb = _shade(n, alb, amb=0.42, diff=0.8, rim=0.6, rim_color=light)
    # Fronds darken toward their tips' undersides.
    y, x = _grid(h, w)
    dd = np.hypot(x - top[0], (y - top[1]) * 1.4) / (R * 1.3)
    rgb = lerp(rgb, dark, np.clip(dd - 0.4, 0, 0.5) * (idx != 1))
    a = cov
    spr = np.concatenate([rgb * a[..., None], a[..., None]], -1)
    sh = _shadow_ellipse(R * 1.1, R * 0.45)
    return Sprite(spr, int(round(base_x)), int(round(base)), sh, sh.shape[1] // 2 - int(R * 0.7), sh.shape[0] // 2 - int(R * 0.1), halo=0.35)


def tuft(rng, size, col, dark, light, blades=None, spread=0.45, lean=0.0):
    """A tuft of grass or reed blades. size = blade length (render px)."""
    nb = blades or rng.integers(4, 8)
    w = int(size * 1.6) + 6
    h = int(size) + 4
    bx, by = w / 2, h - 1.5
    y, x = _grid(h, w)
    cov = np.zeros((h, w))
    shade = np.zeros((h, w))
    order = np.argsort(rng.random(nb))
    for j, k in enumerate(order):
        ang = (k / max(nb - 1, 1) - 0.5) * 2 * spread + rng.normal(0, 0.12) + lean
        L = size * rng.uniform(0.6, 1.0)
        tx, ty = bx + np.sin(ang) * L + rng.normal(0, 0.5), by - np.cos(ang) * L
        x0 = bx + rng.normal(0, size * 0.08)
        # distance to segment
        vx, vy = tx - x0, ty - by
        ll = vx * vx + vy * vy
        s = np.clip(((x - x0) * vx + (y - by) * vy) / ll, 0, 1)
        d = np.hypot(x - (x0 + s * vx), y - (by + s * vy))
        wdt = 0.9 + (1 - s) * size * 0.05
        c = np.clip(wdt - d + 0.5, 0, 1)
        lit = 0.55 + 0.45 * (np.sin(ang) < 0.05) + 0.25 * s
        newc = c * (1 - cov) if False else c
        shade = np.where(c > 0, shade * (1 - newc) + lit * newc, shade)
        cov = 1 - (1 - cov) * (1 - c)
    rgb = lerp(np.broadcast_to(dark, (h, w, 3)), np.broadcast_to(col, (h, w, 3)), np.clip(shade, 0, 1))
    rgb = lerp(rgb, light, np.clip(shade - 1.0, 0, 1))
    spr = np.concatenate([rgb * cov[..., None], cov[..., None]], -1)
    return Sprite(spr, int(round(bx)), int(round(by)))


def _shadow_ellipse(rx, ry, soft=0.35):
    w, h = int(rx * 2 * 1.4) + 4, int(ry * 2 * 1.6) + 4
    y, x = _grid(h, w)
    d = np.hypot((x - w / 2) / rx, (y - h / 2) / ry)
    return smoothstep(1.0 + soft, 1.0 - soft, d)


class Library:
    """Pre-drawn variants of a sprite, chosen by a seeded generator."""
    def __init__(self, make, n, seed):
        rng = np.random.default_rng(seed)
        self.items = [make(np.random.default_rng(rng.integers(1 << 30))) for _ in range(n)]

    def pick(self, rng):
        return self.items[rng.integers(len(self.items))]


def draw_sprite(canvas, spr, x, y, tint=None):
    rgba = spr.rgba
    if tint is not None:
        rgba = rgba.copy()
        rgba[..., :3] *= tint
    stamp(canvas, rgba, int(round(x)) - spr.ax, int(round(y)) - spr.ay)


def draw_shadow(shadow, spr, x, y, strength=1.0):
    if spr.shadow is None:
        return
    shadow_stamp(shadow, spr.shadow * strength, int(round(x)) - spr.sax, int(round(y)) - spr.say)


def poisson_points(rng, accept, h, w, spacing_x, spacing_y, jitter=0.45):
    """Jittered-grid points (x, y) where accept(x, y) is true, staggered rows."""
    pts = []
    ny, nx = int(h / spacing_y) + 2, int(w / spacing_x) + 2
    for j in range(ny):
        off = (j % 2) * 0.5 * spacing_x
        for i in range(nx):
            x = i * spacing_x + off + rng.uniform(-jitter, jitter) * spacing_x
            y = j * spacing_y + rng.uniform(-jitter, jitter) * spacing_y
            if 0 <= x < w and 0 <= y < h and accept(x, y):
                pts.append((x, y))
    return pts
