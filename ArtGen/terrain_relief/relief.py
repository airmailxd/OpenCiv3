"""Hills, mountains and volcanoes (and their forested / jungle / snowy versions).

Each cell is redrawn from a heightfield built on the original's footprint:
the distance from the footprint's edge gives the overall mass (so ranges keep
their silhouettes and connect to their neighbors like the originals), noise
adds ridges and gullies, and the surface is lit from the northwest. The
original's colors only say which material goes where (grass, rock, snow, ash)
and what color it is.
"""
import numpy as np
import scipy.ndimage as ndi

import civ3art
import trees as T
import vegetation as V
from render import (SS, LIGHT, srgb, saturate, lerp, smoothstep, upsample, fbm, value_noise,
                    premul, over, downsample, coverage, normals, cavity, shift)


def blur_masked(rgb, mask, sigma):
    """Blurs colors inside a mask without bleeding in the transparent outside."""
    m = mask.astype(float)
    num = ndi.gaussian_filter(rgb * m[..., None], (sigma, sigma, 0))
    den = ndi.gaussian_filter(m, sigma)[..., None]
    return num / np.maximum(den, 1e-4)


def materials(cell):
    """Soft material weights (orig size): rock, snow, ash, from the original's colors."""
    a = cell[..., 3] > 0
    rgb = blur_masked(cell[..., :3] / 255.0, a, 1.1)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mx, mn = rgb.max(-1), rgb.min(-1)
    sat = (mx - mn) / np.maximum(mx, 1e-3)
    lum = rgb @ np.array([0.3, 0.59, 0.11])
    rock = smoothstep(0.02, 0.16, (r - g) / np.maximum(r, 1e-3))
    snow = smoothstep(0.45, 0.62, lum) * smoothstep(0.42, 0.18, sat)
    ash = smoothstep(0.3, 0.12, sat) * (1 - snow)
    return rock * a, snow * a, ash * a


def tone(c, chroma, lift=0.0):
    """Scales a color's chroma around its own luminance (chroma < 1 greys it), and
    lifts its brightness by `lift`."""
    c = np.asarray(c, float)
    lum = c @ np.array([0.3, 0.59, 0.11])
    return np.clip(lum + (c - lum) * chroma + lift, 0, 1)


def palette(cell, rock, snow, ash, hill=False):
    """The original's median color per material, greyed: never more saturated than
    the original, and a little calmer."""
    a = cell[..., 3] > 0
    def med(m, default):
        sel = cell[..., :3][a & m]
        if len(sel) <= 30:
            return srgb(default)
        return srgb(np.median(sel, 0))
    grass = med((rock < 0.3) & (snow < 0.3) & (ash < 0.3), (140, 140, 50))
    grass = tone(grass * np.array([0.94, 1.0, 1.0]), 0.78) * 0.93
    rk = np.clip(tone(med(rock > 0.6, (150, 105, 50)), 0.6) * np.array([1.26, 1.24, 1.13]), 0, 1)
    return grass, rk


def soft_alpha(m, cov, top_crisp=True, fade=8.0):
    """Antialiased footprint whose ground-contact edges (bottom and sides) fade softly
    while the skyline (top edge) stays crisp."""
    inside = m > 0.5
    dist = ndi.distance_transform_edt(inside)
    gy, gx = np.gradient(ndi.gaussian_filter(m, 3))
    g = np.maximum(np.hypot(gx, gy), 1e-5)
    top = smoothstep(0.1, 0.6, gy / g)  # edge where the shape lies below: a skyline
    top = ndi.gaussian_filter(top * inside + 0.0, 4)
    fade_a = smoothstep(0, fade, dist)
    return cov * lerp(fade_a * 0.85 + 0.15, 1.0, np.clip(top * 1.6, 0, 1))


class Kind:
    HILL, MOUNTAIN, SNOW, VOLCANO = 'hill', 'mountain', 'snow', 'volcano'


def relief_cell(base_cell, kind, seed, color_cell=None):
    """Draws a bare hill / mountain / volcano. Returns premultiplied render-size RGBA,
    the heightfield and the distance from the edge (for placing trees)."""
    color_cell = base_cell if color_cell is None else color_cell
    a = base_cell[..., 3] > 0
    h, w = a.shape[0] * SS, a.shape[1] * SS
    m = ndi.gaussian_filter(upsample(a), SS * 0.55)
    cov = coverage(m)
    inside = m > 0.5
    D = ndi.distance_transform_edt(inside) / SS          # distance from the edge, in original px
    # The front of the relief faces the viewer, so only the skyline and the flanks
    # turn the surface away: the mass is built from everything below the outline.
    below = np.maximum.accumulate(inside, axis=0)
    rock, snow, ash = materials(color_cell)
    rock4 = ndi.gaussian_filter(upsample(rock), SS * 0.6)
    snow4 = ndi.gaussian_filter(upsample(snow), SS * 0.6)
    ash4 = ndi.gaussian_filter(upsample(ash), SS * 0.6)
    grass_c, rock_c = palette(color_cell, rock, snow, ash, hill=(kind == Kind.HILL))
    hill = kind == Kind.HILL

    # Shape guide: the original's broad light and dark puts the folds, spurs and
    # sub-peaks roughly where the original has them.
    lum = blur_masked(base_cell[..., :3] / 255.0, a, 1.6 if hill else 3.0) @ np.array([0.3, 0.59, 0.11])
    lum = ndi.gaussian_filter(upsample(lum), SS * 0.8)
    sel = lum[inside]
    guide = np.where(inside, (lum - sel.mean()) / (sel.std() + 1e-3), 0) if sel.size else np.zeros((h, w))
    belowf = below.astype(float)
    dome = (ndi.gaussian_filter(belowf, 2.5 * SS) * 3.0 + ndi.gaussian_filter(belowf, 6 * SS) * 6.0
            + ndi.gaussian_filter(belowf, 12 * SS) * 6.0)
    edge_in = smoothstep(0, 3, D)
    if hill:
        # Gentle rises whose folds and creases follow the original's (the guide).
        mounds = hill_mounds(inside)
        Hh = (dome * 0.55 + mounds * 0.7 + guide * 1.6
              + (fbm((h, w), 40, seed, 2) - 0.5) * 1.0)
        strength = 1.7
        rock4 = snow4 = ash4 = np.zeros((h, w))
        streak = np.full((h, w), 0.5)
    else:
        rockiness = ndi.gaussian_filter(np.clip(rock4 + snow4 + ash4, 0, 1), 2 * SS)
        peaks = peak_field(inside, seed, volcano=(kind == Kind.VOLCANO), rockiness=rockiness)
        # Rock texture: crisp vertical striations (ribs and chutes running down the
        # faces), stronger on rock, faint on the grassy foot.
        streak = fbm((h, w), 13, seed + 2, 2, aniso=(3.2, 1.0), ridged=True, gain=0.45)
        broken = fbm((h, w), 20, seed + 3, 2, aniso=(1.6, 1.0))
        Hh = (smooth_max(dome * 1.2 + guide * 1.0, peaks, 1.0) + guide * 0.5
              + (streak - 0.5) * (0.1 + 2.2 * rockiness) * edge_in
              + (broken - 0.5) * (0.3 + 0.3 * rockiness))
        strength = 1.35
    crater, cgeo = np.zeros((h, w)), None
    if kind == Kind.VOLCANO:
        crater, rimband, cgeo = _crater(ash4, h, w)
        Hh = Hh - crater * 9.0 + rimband * 2.0

    n = normals(ndi.gaussian_filter(Hh, 0.7), strength * SS)
    n0 = n
    # The map is seen from above at an angle, so flat parts face up as well as at the viewer.
    n = n + np.array([0.0, -0.32, 0.0])
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    lam = np.clip(n @ LIGHT, 0, 1)
    cav = cavity(Hh, 6)
    ao = np.clip(1.0 - np.clip(cav, 0, None) * 0.25, 0.55, 1.0) * (1.0 if hill else 0.85 + 0.15 * smoothstep(0, 3.5, D))
    hn = (Hh - Hh[inside].mean()) / (Hh[inside].std() + 1e-3) if inside.any() else Hh * 0

    # Albedo: grass with soft low-contrast patches; rock with faint strata; snow; ash.
    gnz = fbm((h, w), 18, seed + 4, 3)
    gcol = grass_c * (0.93 + 0.12 * gnz + 0.06 * (fbm((h, w), 5, seed + 9, 2) - 0.5))[..., None]
    earth = tone(lerp(grass_c, srgb((140, 120, 80)), 0.55), 0.8)
    gcol = lerp(gcol, earth, smoothstep(0.64, 0.8, fbm((h, w), 20, seed + 5, 3)) * (0.15 if hill else 0.3))
    y, x = np.mgrid[0:h, 0:w].astype(float)
    warp = fbm((h, w), 24, seed + 6, 2)
    strata = 0.5 + 0.5 * np.sin((y + warp * 40 - x * 0.18) * 2 * np.pi / 15.0)
    rn = fbm((h, w), 12, seed + 7, 3)
    rcol = rock_c * (0.86 + 0.2 * rn + 0.05 * strata)[..., None]
    scol_dark, scol_lit = srgb((160, 164, 172)), srgb((248, 248, 246))
    acol = srgb((100, 95, 90)) * (0.85 + 0.25 * rn)[..., None]

    # Material blend: rock where the original has rock (more on steep faces, less in
    # hollows where grass collects), with a ragged edge.
    edge_n = (fbm((h, w), 8, seed + 8, 3) - 0.5) * 0.45
    steep = 1 - n0[..., 2]
    conv = -cavity(Hh, 2.5 * SS)
    conv = conv / (np.abs(conv[inside]).std() + 1e-3) if inside.any() else conv
    wr = smoothstep(0.42, 0.6, rock4 * 0.95 + edge_n + hn * 0.08 + (steep - 0.3) * 0.4 + conv * 0.05)
    snowbase = ndi.gaussian_filter(snow4, 2.5 * SS)
    # Snow follows the ridges down the faces and leaves the gullies' rock bare.
    ws = smoothstep(0.42, 0.52, snowbase * 1.1 + edge_n * 0.4 + conv * 0.1 + (hn - 0.5) * 0.1
                    + (streak - 0.5) * 0.5 - np.clip(steep - 0.55, 0, 1) * 0.5
                    + 0.2)
    ws = ws * (snowbase > 0.04) * (kind == Kind.SNOW)
    wa = np.maximum(smoothstep(0.18, 0.42, ash4 + edge_n * 0.6), smoothstep(0.0, 0.3, crater))
    alb = lerp(gcol, rcol, wr)
    alb = lerp(alb, acol, wa)

    amb, diffuse = 0.46, 0.7
    shade = (amb + diffuse * lam) * ao
    # Keep the original's brightness: the grass's median shading is about 1.
    ground = inside & (wr < 0.3) & (wa < 0.3)
    if ground.sum() > 100:
        shade = shade / max(np.median(shade[ground]), 0.5) ** 0.6
    col = alb * shade[..., None]
    if ws.any():
        # Snow: lit faces bright, shadowed faces blue-grey, so ridges and faces still read.
        sl = np.clip((lam - 0.2) / 0.85, 0, 1)
        snow_col = lerp(scol_dark, scol_lit, sl[..., None]) * (0.85 + 0.15 * ao[..., None])
        col = lerp(col, snow_col, ws[..., None])
    if cgeo is not None:
        # The crater: a dark hollow, its far wall in shadow and its near wall catching
        # some light, inside a lit rim (the original has no glow).
        e, ex, ey = cgeo
        t = np.clip(0.45 - ex * 0.35 + ey * 0.5, 0, 1) * smoothstep(1.0, 0.3, e)
        hollow = srgb((100, 95, 90)) * (0.3 + 0.45 * t)[..., None]
        col = lerp(col, hollow, smoothstep(1.02, 0.72, e)[..., None])
        ring = smoothstep(0.84, 0.98, e) * smoothstep(1.2, 1.0, e)
        col = col + (ring * 0.14 * np.clip(0.6 - ex * 0.6 - ey * 0.4, 0, 1))[..., None]
    col = np.clip(col, 0, 1)

    alpha = soft_alpha(m, cov, fade=12.0 if hill else 8.0)
    canvas = np.zeros((h, w, 4))
    if not hill:
        # A soft shadow cast down-right on the ground (only beyond the footprint's
        # down-right side, so no dark halo on the lit side).
        sh = ndi.gaussian_filter(shift(cov, 4, 8), 5)
        sh = sh * (1 - ndi.gaussian_filter(cov, 3)) * smoothstep(0.0, 0.3, sh - ndi.gaussian_filter(shift(cov, -2, -4), 5))
        over(canvas, premul(np.zeros((h, w, 3)), np.clip(sh, 0, 1) * 0.3))
    over(canvas, premul(col, alpha))
    return canvas, Hh, D


def smooth_max(a, b, k):
    return np.logaddexp(a / k, b / k) * k


def find_peaks(inside):
    """Summits: local highs of the footprint's skyline (render px coordinates)."""
    h, w = inside.shape
    cols = np.where(inside.any(0))[0]
    if len(cols) == 0:
        return []
    top = np.full(w, float(h))
    top[cols] = np.argmax(inside[:, cols], 0)
    bottom = np.zeros(w)
    bottom[cols] = h - 1 - np.argmax(inside[::-1, cols], 0)
    ts = ndi.gaussian_filter1d(np.where(np.isin(np.arange(w), cols), top, h), 1.5 * SS, mode='nearest')
    peaks = []
    win, far = 6 * SS, 12 * SS
    for x in cols:
        lo, hi = max(x - win, 0), min(x + win + 1, w)
        if ts[x] > ts[lo:hi].min() + 1e-6:
            continue
        lo2, hi2 = max(x - far, 0), min(x + far + 1, w)
        prom = min(ts[lo2:x + 1].max(), ts[x:hi2].max()) - ts[x]
        if prom >= 2.5 * SS or ts[x] == ts[cols].min():
            if not peaks or x - peaks[-1][0] > win:
                peaks.append((x, top[x], bottom[x], prom))
    return peaks


def hill_mounds(inside):
    """Rounded rises under each high point of a hill's skyline."""
    h, w = inside.shape
    y, x = np.mgrid[0:h, 0:w].astype(float)
    field = np.zeros((h, w))
    for (px, ptop, pbot, prom) in find_peaks(inside):
        ay = ptop + 3 * SS
        tall = max((pbot - ay) / SS, 4)
        dx, dy = (x - px) / SS, (y - ay) / SS
        dist2 = (dx / (tall * 1.4)) ** 2 + (dy / (tall * np.where(dy > 0, 1.6, 0.6))) ** 2
        field = smooth_max(field, tall * 1.1 * np.exp(-dist2 * 1.5), 1.2)
    return field


def peak_field(inside, seed, volcano=False, rockiness=None):
    """Rocky cones under each summit, with ridges and spurs radiating from the top."""
    h, w = inside.shape
    y, x = np.mgrid[0:h, 0:w].astype(float)
    rng = np.random.default_rng(seed + 77)
    warp = (fbm((h, w), 40, seed + 11, 2, gain=0.3) - 0.5) * 2.0
    field = np.full((h, w), -50.0)
    # Spurs are sharpest on the rock and soften on the grassy foot.
    ramp = 1.0 if rockiness is None else 0.45 + 0.55 * np.clip(rockiness * 1.5, 0, 1)
    for (px, ptop, pbot, prom) in find_peaks(inside):
        ay = ptop + 1.5 * SS
        tall = (pbot - ay) / SS
        A = tall * 1.15
        dx, dy = (x - px) / SS, (y - ay) / SS
        dist = np.sqrt(dx * dx + (dy * np.where(dy > 0, 0.62, 1.6)) ** 2)
        theta = np.arctan2(dx, np.maximum(dy, 0) + 0.5)
        nr = rng.integers(6, 9)
        ph = rng.uniform(0, 2 * np.pi)
        rid = 1 - np.abs(np.sin(theta * nr / 2 * 1.6 + ph + warp))
        cone = A - 1.55 * dist
        if volcano:
            cone = np.minimum(cone, A - 1.55 * 2.5)
        cone = cone + (rid - 0.5) * np.clip(dist / 1.2, 0, 1) * (1.1 + 0.3 * dist) * np.clip(1.3 - dist / (tall * 1.4 + 1), 0, 1) * 1.7 * ramp
        field = smooth_max(field, cone, 0.8)
    return field


def _crater(ash4, h, w):
    """An elliptical crater in the upper part of the ash cone: depth 0..1, a raised rim
    band, and the ellipse coordinates (radius, x, y) for coloring it."""
    rows = np.where(ash4.max(1) > 0.5)[0]
    if len(rows) == 0:
        return np.zeros((h, w)), np.zeros((h, w)), None
    top = rows[0]
    band = ash4[top:top + 14 * SS] > 0.5
    cols = np.where(band.any(0))[0]
    cx = (cols[0] + cols[-1]) / 2
    width = cols[-1] - cols[0]
    rx = width * 0.36
    ry = rx * 0.48
    cy = top + ry + 2.2 * SS
    y, x = np.mgrid[0:h, 0:w].astype(float)
    e = np.hypot((x - cx) / rx, (y - cy) / ry)
    depth = smoothstep(1.0, 0.25, e)
    rim = smoothstep(1.35, 1.0, e) * smoothstep(0.8, 1.0, e)
    return depth, rim, (e, (x - cx) / rx, (y - cy) / ry)


def veg_cell(base_cell, veg_cell_, kind, veg, seed, libs):
    """A hill / mountain / volcano with forest or jungle on it."""
    canvas, Hh, D = relief_cell(base_cell, kind, seed)
    h, w = Hh.shape
    va = veg_cell_[..., 3] > 0
    ba = base_cell[..., 3] > 0
    diff = np.abs(veg_cell_[..., :3].astype(int) - base_cell[..., :3].astype(int)).sum(-1) > 40
    treem = va & (~ba | diff)
    treem = ndi.binary_opening(treem, iterations=1)
    dens = ndi.gaussian_filter(upsample(ndi.gaussian_filter(treem.astype(float), 1.3)), SS * 0.5)
    pm = V.pine_mask(veg_cell_) & treem
    pfrac = ndi.gaussian_filter(upsample(ndi.gaussian_filter(pm.astype(float), 2.0)), SS)
    tfrac = ndi.gaussian_filter(upsample(ndi.gaussian_filter(treem.astype(float), 2.0)), SS)
    pratio = pfrac / np.maximum(tfrac, 1e-3)
    jn = value_noise((h, w), 14, seed + 5)
    rng = np.random.default_rng(seed)

    def kind_at(x, y):
        xi, yi = int(np.clip(x, 0, w - 1)), int(np.clip(y - 16, 0, h - 1))
        if veg == 'jungle':
            return 'palm' if jn[yi, xi] > 0.5 else 'bush'
        return 'pine' if pratio[yi, xi] > 0.3 else 'broad'

    shadow = np.zeros((h, w))
    tr = np.zeros((h, w, 4))
    V.plant(tr, shadow, dens, kind_at, libs, rng, scale=0.82, thresholds=(0.4, 0.35, 0.2), spacing=0.95)
    over(canvas, premul(np.zeros((h, w, 3)), ndi.gaussian_filter(shadow, 1.5) * 0.4 * (canvas[..., 3] > 0.5)))
    over(canvas, tr)
    return canvas


def relief_sheet(rel, kind, cw, ch, seed, base_rel=None, veg=None):
    orig = civ3art.load_original(rel)
    base = civ3art.load_original(base_rel) if base_rel else orig
    H, W = orig.shape[:2]
    out = np.zeros((H * 2, W * 2, 4), np.uint8)
    libs = None
    if veg:
        a = orig[..., 3] > 0
        diff = np.abs(orig[..., :3].astype(int) - base[..., :3].astype(int)).sum(-1) > 40
        treem = a & (~(base[..., 3] > 0) | diff)
        pm = V.pine_mask(orig) & treem
        tc = V.civ3art_median_all(orig, treem & ~pm, (100, 120, 30))
        pc = V.civ3art_median_all(orig, pm, (60, 85, 60))
        if veg == 'jungle':
            libs = V.make_libraries(seed, jungle=tc * np.array([1.0, 1.08, 1.0]) + np.array([0.02, 0.04, 0.0]), scale=0.82)
        else:
            libs = V.make_libraries(seed, broad=tc * np.array([0.72, 0.86, 0.85]),
                                    pine=np.clip(pc * np.array([0.95, 1.0, 0.82]), 0, 1), scale=0.82)
    for row in range(H // ch):
        for col in range(W // cw):
            y0, x0 = row * ch, col * cw
            bc = base[y0:y0 + ch, x0:x0 + cw]
            if not bc[..., 3].any():
                continue
            s = seed * 100 + row * 4 + col
            if veg:
                canvas = veg_cell(bc, orig[y0:y0 + ch, x0:x0 + cw], kind, veg, s, libs)
            else:
                canvas, _, _ = relief_cell(bc, kind, s, color_cell=orig[y0:y0 + ch, x0:x0 + cw])
            out[y0 * 2:(y0 + ch) * 2, x0 * 2:(x0 + cw) * 2] = downsample(canvas)
    return orig, out
