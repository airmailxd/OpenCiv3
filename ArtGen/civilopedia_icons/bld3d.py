"""Building icons are drawn with the city renderer (ArtGen/cities/render3d.py:
the same isometric projection, light, shadows, ambient occlusion and
procedural surfaces as the city art), at a zoom so a building fills its icon.

render3d.render draws one world unit per output pixel; render_zoom below is
the same pipeline with a zoom factor (world units are still the city art's,
so windows, roof tiles and bricks keep their proportions to the building).
"""
import math

import numpy as np

import common as C
import render3d as R3
from render3d import (PLAIN, STUCCO, WINDOWS, ROOF_TILE, THATCH, SLATE, GLASS, BRICK, METAL, DOME, STONE,  # noqa: F401
                      WATER, FOLIAGE, WOOD, GOLD, RUBBLE, PAVING, rasterize, texture, _blur)
import geom as G  # noqa: F401


def render_zoom(scene, W, H, cx, cy, zoom, ss=3, shadow_strength=0.42, contour=0.45, ground_z=0.0):
    """render3d.render with a zoom (output pixels per world unit). (cx, cy)
    is the output pixel of the world origin; the shadows fall on the plane
    z = ground_z. Returns float RGBA (straight)."""
    V, N, Cc, Mm, B, FN = scene.arrays()
    SW, SH = W * ss, H * ss
    k = zoom * ss
    P = np.stack([cx * ss + (V[..., 0] - V[..., 1]) * k, cy * ss + ((V[..., 0] + V[..., 1]) / 2 - V[..., 2]) * k], -1)
    Dep = V.sum(-1)
    zb, ib, nb = rasterize(P, Dep, SW, SH, N)
    cover = ib >= 0

    px = ((np.arange(SW) + 0.5) / ss - cx) / zoom
    py = ((np.arange(SH) + 0.5)[:, None] / ss - cy) / zoom
    sxw = np.broadcast_to(px[None, :], (SH, SW))
    syw = np.broadcast_to(py, (SH, SW))
    s = np.where(cover, (syw + zb) / 1.5, 2 * (syw + ground_z))
    wz = np.where(cover, zb - s, ground_z)
    wx = (s + sxw) / 2
    wy = (s - sxw) / 2
    nrm = np.where(cover[..., None], nb, np.array([0, 0, 1.0]))
    nrm = nrm / np.maximum(np.linalg.norm(nrm, axis=-1, keepdims=True), 1e-9)

    L = R3.LIGHT
    up = np.array([0, 0, 1.0])
    lu = np.cross(up, L); lu /= np.linalg.norm(lu)
    lv = np.cross(L, lu)
    res = 3.0
    lx = V @ lu; ly = V @ lv; ld = V @ L
    ox, oy = lx.min() - 8, ly.min() - 8
    SMW = int((lx.max() - ox + 8) * res) + 1
    SMH = int((ly.max() - oy + 8) * res) + 1
    smz, _, _ = rasterize(np.stack([(lx - ox) * res, (ly - oy) * res], -1), ld, SMW, SMH)
    P3 = np.stack([wx, wy, wz], -1)
    qx = ((P3 @ lu) - ox) * res
    qy = ((P3 @ lv) - oy) * res
    qd = P3 @ L
    lit = np.zeros((SH, SW))
    offs = [(0, 0), (1.5, 0), (-1.5, 0), (0, 1.5), (0, -1.5), (1.1, 1.1), (-1.1, 1.1), (1.1, -1.1), (-1.1, -1.1)]
    for ddx, ddy in offs:
        ix = np.clip((qx + ddx).astype(int), 0, SMW - 1)
        iy = np.clip((qy + ddy).astype(int), 0, SMH - 1)
        inb = ((qx + ddx) >= 0) & ((qx + ddx) < SMW) & ((qy + ddy) >= 0) & ((qy + ddy) < SMH)
        occ = inb & (smz[iy, ix] > qd + 0.9)
        lit += ~occ
    lit /= len(offs)

    gx0 = min(V[..., 0].min(), wx.min()) - 20
    gy0 = min(V[..., 1].min(), wy.min()) - 20
    GW = int(max(V[..., 0].max(), wx.max()) - gx0 + 20) + 1
    GH = int(max(V[..., 1].max(), wy.max()) - gy0 + 20) + 1
    hz, _, _ = rasterize(np.stack([V[..., 0] - gx0, V[..., 1] - gy0], -1), V[..., 2], GW, GH)
    hz = np.where(np.isfinite(hz), np.maximum(hz, ground_z), ground_z)
    ao = np.ones((SH, SW))
    gi = np.clip((wx - gx0).astype(int), 0, GW - 1)
    gj = np.clip((wy - gy0).astype(int), 0, GH - 1)
    for r, wgt in ((3, 0.5), (8, 0.35), (16, 0.2)):
        hb = _blur(hz, r)
        rise = np.clip((hb[gj, gi] - wz) / (2.2 * r), 0, 1)
        ao -= wgt * rise
    ao = np.clip(ao, 0.25, 1)
    ao *= np.where(cover, 1 - 0.28 * np.exp(-np.clip(wz, 0, None) / 5.0) * (1 - np.abs(nrm[..., 2])), 1)

    albedo = np.where(cover[..., None], Cc[np.maximum(ib, 0)], 0.0)
    mat = np.where(cover, Mm[np.maximum(ib, 0)], -1)
    bid = np.where(cover, B[np.maximum(ib, 0)], -1)
    spec = np.zeros((SH, SW)); gloss = np.full((SH, SW), 16.0)
    albedo, spec, gloss, emis = texture(scene, albedo, mat, bid, wx, wy, wz, nrm, ss * zoom, spec, gloss)

    ndl = np.clip(nrm @ L, 0, 1)
    sky = 0.55 + 0.45 * nrm[..., 2]
    amb = (0.40 * sky + 0.20)[..., None] * np.array([0.95, 0.98, 1.05]) * ao[..., None]
    sun = (0.80 * ndl * lit)[..., None] * np.array([1.05, 1.0, 0.92])
    hvec = L + R3.VIEW; hvec /= np.linalg.norm(hvec)
    sp = (spec * np.clip(nrm @ hvec, 0, 1) ** gloss * lit)[..., None]
    rim = (0.10 * np.clip(1 - (nrm @ R3.VIEW), 0, 1) ** 2 * np.clip(nrm @ L, 0, 1))[..., None]
    rgb = albedo * (amb + sun + rim) + sp + emis[..., None] * albedo
    lum = rgb @ np.array([0.3, 0.55, 0.15])
    rgb = lum[..., None] + (rgb - lum[..., None]) * 1.08

    edge = np.zeros((SH, SW))
    rr = max(1, int(round(ss * 1.0)))
    with np.errstate(invalid='ignore'):
        for dx, dy in [(rr, 0), (-rr, 0), (0, rr), (0, -rr)]:
            zs = np.roll(np.roll(zb, dy, 0), dx, 1)
            bs = np.roll(np.roll(bid, dy, 0), dx, 1)
            cs = np.roll(np.roll(cover, dy, 0), dx, 1)
            jump = cover & cs & (np.nan_to_num(zs - zb, nan=0, posinf=0, neginf=0) > 6)
            diffb = cover & cs & (bs != bid) & (zs > zb)
            edge = np.maximum(edge, np.where(jump, 1.0, 0) + np.where(diffb, 0.6, 0))
            sil = cover & ~cs
            edge = np.maximum(edge, sil * 1.0)
    edge = np.clip(edge, 0, 1)
    rgb = rgb * (1 - contour * edge[..., None] * 0.9)

    gshade = np.clip((1 - lit) * shadow_strength + (1 - ao) * 0.55, 0, 0.75)
    out_rgb = np.where(cover[..., None], rgb, 0.0)
    out_a = np.where(cover, 1.0, gshade)
    pm = out_rgb * out_a[..., None]
    pm = pm.reshape(H, ss, W, ss, 3).mean((1, 3))
    a = out_a.reshape(H, ss, W, ss).mean((1, 3))
    rgb = np.where(a[..., None] > 1e-6, pm / np.maximum(a, 1e-6)[..., None], 0)
    rgb = np.clip(rgb, 0, 1) ** (1 / 1.0)
    return np.concatenate([rgb, a[..., None]], -1).astype(np.float32)


def fit_render(scene, size, fill=0.84, ss=3, center=(0.5, 0.52), ground_z=0.0):
    """Renders the scene fitted into size x size."""
    V = scene.arrays()[0]
    sx = V[..., 0] - V[..., 1]
    sy = (V[..., 0] + V[..., 1]) / 2 - V[..., 2]
    x0, x1, y0, y1 = sx.min(), sx.max(), sy.min(), sy.max()
    zoom = min(size * fill / (x1 - x0), size * fill / (y1 - y0))
    cx = size * center[0] - (x0 + x1) / 2 * zoom
    cy = size * center[1] - (y0 + y1) / 2 * zoom
    return render_zoom(scene, size, size, cx, cy, zoom, ss, ground_z=ground_z)


def lin(h):
    """A color for the scene (the renderer takes sRGB-ish 0..1 colors)."""
    return tuple(float(v) for v in C.hex_rgb(h))


# ---------------------------------------------------------------- ground

GRASS = lin('8a9a58')
EARTH = lin('86714f')
PAVE = lin('b8b2a2')
WATERC = lin('5f8ea8')


def plinth(sc, x0, y0, x1, y1, h=4.0, top=GRASS, side=EARTH, mat=PLAIN):
    """The block of ground a building stands on (its top at z = 0)."""
    b = sc.new_building()
    G.box(sc, x0, y0, x1, y1, -h, 0, side, RUBBLE, b, top=top, topmat=mat)
    return b


def water_plinth(sc, x0, y0, x1, y1, h=4.0):
    b = sc.new_building()
    G.box(sc, x0, y0, x1, y1, -h, -0.6, lin('4a6e80'), PLAIN, b, top=WATERC, topmat=WATER)
    return b
