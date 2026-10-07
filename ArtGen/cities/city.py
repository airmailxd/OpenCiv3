"""Composes a city scene for one cell of a city sprite sheet and renders it."""
import math
import numpy as np
from render3d import Scene, render, fbm, _blur
from layout import Fitter, ground_of
import buildings as bd

CELL_W, CELL_H = 332, 190   # a city cell at 2x (the game crops 166x95)
CX, CY = 166.0, 95.0        # the tile center inside the cell


def orig_to_world(ox, oy):
    """A ground point given by its position in the original (1x) cell."""
    return ground_of(ox * 2, oy * 2, CX, CY)


def _sample(rng, rngspec):
    a, b = rngspec
    return rng.uniform(a, b)


class Kind:
    """A building kind for the fill: its draw function and size ranges."""
    def __init__(self, fn, weight, w, d, h, min_h=None, square=False, **kw):
        self.fn, self.weight, self.w, self.d, self.h = fn, weight, w, d, h
        self.min_h = min_h
        self.square = square
        self.kw = kw


def compose(style, size, mask, seed, pre=None, bounds=None):
    rng = np.random.default_rng(seed)
    P = style['palette']
    fit = Fitter(mask, CX, CY, rng, inside=style.get('inside', 0.9), gap=style.get('gap', 2.0))
    sc = Scene()
    placed = []
    if pre is not None:
        pre(sc, fit, P, rng)

    def draw(fn, r, h, kw):
        x0, y0, x1, y1, hh = r
        fn(sc, (x0, y0, x1, y1), hh, P, np.random.default_rng(rng.integers(1 << 30)), **kw)
        placed.append((x0, y0, x1, y1, hh))

    # Plaza ornaments first (they keep their ground free).
    for orn in style.get('ornaments', {}).get(size, []):
        kind, ox, oy, rad = orn
        x, y = orig_to_world(ox, oy)
        if fit.ground_ok(x - rad, y - rad, x + rad, y + rad, gap=1):
            pr = rad + style.get('plaza_pad', 2)
            fit.rects.append((x - pr, y - pr, x + pr, y + pr))
            if kind == 'fountain':
                bd.fountain(sc, x, y, rad, P)
            elif kind == 'statue':
                bd.statue(sc, x, y, rad * 2.5, P)
            elif kind == 'obelisk':
                bd.obelisk(sc, x, y, rad * 5, P)
            elif kind == 'tree' or kind == 'palm':
                bd.tree(sc, x, y, rad * 2.2, P, rng, palm=(kind == 'palm'))

    # Landmarks near their places in the original.
    for lm in sorted(style['landmarks'].get(size, []), key=lambda l: l[1] * l[2]):
        fn, w, d, h, (ox, oy) = lm[:5]
        kw = lm[5] if len(lm) > 5 else {}
        px, py = orig_to_world(ox, oy)
        best = None
        for scale in (1.0, 0.9, 0.8, 0.7):
            cands = []
            for dx in np.arange(-24, 25, 3):
                for dy in np.arange(-24, 25, 3):
                    cands.append((dx * dx + dy * dy, px + dx, py + dy))
            cands.sort()
            for _, x, y in cands:
                if bounds is not None and not (bounds[0] + w * scale / 2 <= x <= bounds[2] - w * scale / 2
                                               and bounds[1] + d * scale / 2 <= y <= bounds[3] - d * scale / 2):
                    continue
                r = fit.try_place(x, y, w * scale, d * scale, h, min_h=h * 0.75, min_gain=0.05, inside=0.85)
                if r:
                    best = r
                    break
            if best:
                break
        if best:
            draw(fn, best, best[4], kw)

    # Fill with ordinary buildings.
    kinds = style['fill']
    weights = np.array([k.weight for k in kinds], float)
    weights /= weights.sum()
    for pas, (min_gain, szs) in enumerate(((0.45, (1.0, 0.85)), (0.3, (1.0, 0.85, 0.7)), (0.2, (0.8, 0.7, 0.6)))):
        pts = fit.ground_points(step=3.0)
        if bounds is not None:
            bx0, by0, bx1, by1 = bounds
            pts = pts[(pts[:, 0] > bx0) & (pts[:, 0] < bx1) & (pts[:, 1] > by0) & (pts[:, 1] < by1)]
        for (x, y) in pts:
            k = kinds[rng.choice(len(kinds), p=weights)]
            fs = style.get('scale', 1.2)
            w0 = _sample(rng, k.w) * fs
            d0 = w0 if k.square else _sample(rng, k.d) * fs
            h = _sample(rng, k.h) * style.get('hscale', 1.0)
            mh = k.min_h if k.min_h is not None else h * 0.6
            for sz in szs:
                r = fit.try_place(x, y, w0 * sz, d0 * sz, h, min_h=mh, min_gain=min_gain)
                if r:
                    draw(k.fn, r, r[4], k.kw)
                    break
    return sc, fit, placed, P


def make_ground(fit, P, rng_seed=0, strength=0.8):
    """The city's ground: packed earth / paving around the buildings."""
    if not fit.rects or P.get('plaza') is None:
        return None
    rs = np.array(fit.rects)
    gx0, gy0 = rs[:, 0].min() - 30, rs[:, 1].min() - 30
    gx1, gy1 = rs[:, 2].max() + 30, rs[:, 3].max() + 30
    GW, GH = int(gx1 - gx0) + 1, int(gy1 - gy0) + 1
    occ = np.zeros((GH, GW))
    for (x0, y0, x1, y1) in fit.rects:
        occ[max(0, int(y0 - gy0)):int(y1 - gy0) + 1, max(0, int(x0 - gx0)):int(x1 - gx0) + 1] = 1
    soft = _blur(occ, 5)
    yy, xx = np.mgrid[0:GH, 0:GW]
    nz = fbm((xx + gx0) * 0.08, (yy + gy0) * 0.08, 77 + rng_seed, 3)
    a = np.clip((soft - 0.16 + 0.22 * (nz - 0.5)) / 0.14, 0, 1) * strength
    col = np.array(P['plaza'])

    def ground(wx, wy, lit, ao):
        i = np.clip((wx - gx0).astype(int), 0, GW - 1)
        j = np.clip((wy - gy0).astype(int), 0, GH - 1)
        inb = (wx >= gx0) & (wx < gx1) & (wy >= gy0) & (wy < gy1)
        al = np.where(inb, a[j, i], 0.0)
        t = fbm(wx * 0.35, wy * 0.35, 91, 2)
        rgb = col * (0.88 + 0.24 * t)[..., None]
        return rgb, al
    return ground


def render_cell(style, size, mask, seed, ss=4, pre=None, bounds=None):
    sc, fit, placed, P = compose(style, size, mask, seed, pre, bounds)
    ground = make_ground(fit, P, seed, style.get('plaza_alpha', 0.8))
    img = render(sc, CELL_W, CELL_H, CX, CY, ss=ss, ground=ground)
    return img, fit
