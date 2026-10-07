"""Fits a city's buildings to the footprint of the original sprite.

The original cell's alpha (upscaled 2x) is the guide: a building is placed only
where its projected silhouette (the box it fits in) lies inside the original's
silhouette, and the placement greedily covers as much of that silhouette as it
can, so the remake has the same outline, density and height profile."""
import math
import numpy as np


def screen_of(x, y, z, cx, cy):
    return cx + x - y, cy + (x + y) / 2 - z


def ground_of(sx, sy, cx, cy):
    """World ground point under a screen point (z = 0)."""
    a, b = sx - cx, sy - cy
    return b + a / 2, b - a / 2


def box_silhouette(x0, y0, x1, y1, h, cx, cy, W, H):
    """Mask of the screen pixels covered by the box [x0,x1]x[y0,y1]x[0,h]:
    returns (ys slice, xs slice, bool mask)."""
    sxa = cx + x0 - y1; sxb = cx + x1 - y0
    sya = cy + (x0 + y0) / 2 - h; syb = cy + (x1 + y1) / 2
    i0, i1 = max(0, int(math.floor(sxa))), min(W, int(math.ceil(sxb)))
    j0, j1 = max(0, int(math.floor(sya))), min(H, int(math.ceil(syb)))
    if i1 <= i0 or j1 <= j0:
        return None
    sx = np.arange(i0, i1) + 0.5 - cx
    sy = (np.arange(j0, j1) + 0.5)[:, None] - cy
    lo = np.maximum(x0 - sx / 2, y0 + sx / 2)
    hi = np.minimum(x1 - sx / 2, y1 + sx / 2)
    m = (lo <= hi) & (lo <= sy + h) & (hi >= sy)
    return slice(j0, j1), slice(i0, i1), m


def dilate(m, r):
    out = m.copy()
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            if dx * dx + dy * dy <= r * r:
                out |= np.roll(np.roll(m, dy, 0), dx, 1)
    return out


class Fitter:
    def __init__(self, mask, cx, cy, rng, inside=0.9, gap=2.0):
        self.mask = mask
        self.md = dilate(mask, 3)
        self.H, self.W = mask.shape
        self.cx, self.cy = cx, cy
        self.cover = np.zeros_like(mask)
        self.rects = []          # ground footprints, (x0,y0,x1,y1)
        self.rng = rng
        self.inside = inside
        self.gap = gap

    def ground_ok(self, x0, y0, x1, y1, gap=None):
        g = self.gap if gap is None else gap
        for (a0, b0, a1, b1) in self.rects:
            if x0 < a1 + g and x1 > a0 - g and y0 < b1 + g and y1 > b0 - g:
                return False
        return True

    def score(self, x0, y0, x1, y1, h):
        s = box_silhouette(x0, y0, x1, y1, h, self.cx, self.cy, self.W, self.H)
        if s is None:
            return 0, 0, None
        ys, xs, m = s
        n = m.sum()
        if n == 0:
            return 0, 0, None
        full = (x1 - x0) * (y1 - y0) + (x1 - x0 + y1 - y0) * h  # unclipped area (approx.)
        inside = (m & self.md[ys, xs]).sum() / max(n, full * 0.98)
        gain = (m & self.mask[ys, xs] & ~self.cover[ys, xs]).sum() / n
        return inside, gain, s

    def accept(self, x0, y0, x1, y1, h, s):
        ys, xs, m = s
        self.cover[ys, xs] |= m
        self.rects.append((x0, y0, x1, y1))

    def try_place(self, x, y, w, d, h, min_h=None, min_gain=0.25, gap=None, shrink=True, inside=None):
        """Tries a w x d footprint centered at (x, y), lowering the height (down to
        min_h) until it fits. Returns the (x0, y0, x1, y1, h) placed or None."""
        inside_req = self.inside if inside is None else inside
        min_h = h * 0.5 if min_h is None else min_h
        x0, y0, x1, y1 = x - w / 2, y - d / 2, x + w / 2, y + d / 2
        if not self.ground_ok(x0, y0, x1, y1, gap):
            return None
        hh = h
        while hh >= min_h - 1e-6:
            ins, gain, s = self.score(x0, y0, x1, y1, hh)
            if ins >= inside_req and gain >= min_gain:
                self.accept(x0, y0, x1, y1, hh, s)
                return (x0, y0, x1, y1, hh)
            if not shrink or ins < 0.5:
                break
            hh *= 0.85
        return None

    def ground_points(self, step=3.0):
        """Ground points whose projection is inside the silhouette, shuffled."""
        pts = []
        for j in range(0, self.H, 2):
            for i in range(0, self.W, 2):
                if self.mask[j, i]:
                    pts.append(ground_of(i + 0.5, j + 0.5, self.cx, self.cy))
        pts = np.array(pts)
        pts = np.round(pts / step) * step
        pts = np.unique(pts, axis=0)
        self.rng.shuffle(pts)
        return pts

    def coverage(self):
        return (self.cover & self.mask).sum() / max(1, self.mask.sum())
