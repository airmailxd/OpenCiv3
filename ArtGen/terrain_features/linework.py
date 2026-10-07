"""Tools for the linear features (rivers, roads, railroads): tracing the paths of
an original sprite cell as smooth center lines, and drawing smooth strokes along
center lines at high resolution.

Tracing: the original cell's opaque pixels form a thick band. From a hub inside
the band, a shortest-path tree (Dijkstra, with a cost that keeps paths on the
band's middle) reaches every connection point the cell has. That tree, split at
its branch points into segments and smoothed, is the cell's set of center lines.
The ends at connection points are straightened along the direction the path
continues into the neighboring tile, so neighbors join seamlessly.
"""
import os
import sys

import numpy as np
from scipy import ndimage
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import dijkstra

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'common'))
import civ3art  # noqa: E402


# ---------------------------------------------------------------- tracing

def _grid_graph(mask, cost):
    """Sparse 8-connected graph over the mask's pixels; returns (graph, index image, coords)."""
    h, w = mask.shape
    idx = -np.ones((h, w), np.int64)
    ys, xs = np.nonzero(mask)
    idx[ys, xs] = np.arange(len(ys))
    rows, cols, vals = [], [], []
    for dy, dx in ((0, 1), (1, 0), (1, 1), (1, -1)):
        # Source pixel (y, x), neighbor (y+dy, x+dx).
        y0, y1 = 0, h - dy
        x0, x1 = max(0, -dx), w - max(0, dx)
        src = mask[y0:y1, x0:x1] & mask[y0 + dy:y1 + dy, x0 + dx:x1 + dx]
        sy, sx = np.nonzero(src)
        sy, sx = sy + y0, sx + x0
        ty, tx = sy + dy, sx + dx
        step = np.hypot(dy, dx)
        wgt = step * 0.5 * (cost[sy, sx] + cost[ty, tx])
        rows.append(idx[sy, sx]); cols.append(idx[ty, tx]); vals.append(wgt)
    r = np.concatenate(rows); c = np.concatenate(cols); v = np.concatenate(vals)
    n = len(ys)
    g = coo_matrix((np.concatenate([v, v]), (np.concatenate([r, c]), np.concatenate([c, r]))), shape=(n, n)).tocsr()
    return g, idx, np.stack([ys, xs], 1)


def _nearest_in_mask(mask, p):
    ys, xs = np.nonzero(mask)
    d = (ys - p[1]) ** 2 + (xs - p[0]) ** 2
    i = int(np.argmin(d))
    return int(ys[i]), int(xs[i]), float(np.sqrt(d[i]))


def trace_cell(alpha, points, hub_hint=None, close=1, center_weight=2.0, single_far=True, hub_at=None):
    """Traces the center lines of one cell.

    alpha: HxW bool mask of the original cell (1x).
    points: list of (x, y) connection points (1x cell coordinates) the paths must reach.
    Returns a list of polylines (each an Nx2 float array of (x, y)), ordered from
    the hub outward, and the hub point.
    """
    mask = alpha.copy()
    if close:
        mask = ndimage.binary_closing(np.pad(mask, 2), iterations=close)[2:-2, 2:-2] | mask
    # Keep the components that matter (drop specks).
    lab, n = ndimage.label(mask, structure=np.ones((3, 3)))
    if n > 1:
        sizes = ndimage.sum(mask, lab, range(1, n + 1))
        keep = np.zeros(n + 1, bool)
        keep[1:] = sizes >= 6
        mask = keep[lab]
    dt = ndimage.distance_transform_edt(mask)
    cost = 1.0 / (0.35 + dt) ** center_weight
    g, idx, coords = _grid_graph(mask, cost)

    ends = []
    for p in points:
        y, x, _ = _nearest_in_mask(mask, p)
        ends.append(idx[y, x])
    ends = list(dict.fromkeys(ends))
    dist = dijkstra(g, indices=ends)  # len(ends) x N
    reach = np.isfinite(dist).all(0)
    if hub_at is not None:
        hy, hx, _ = _nearest_in_mask(mask, hub_at)
        hub = int(idx[hy, hx])
    elif len(ends) == 1:
        # One connection: run to the far end of the band (a river source, a road's end).
        d = np.where(np.isfinite(dist[0]), dist[0], -1)
        if hub_hint is not None and not single_far:
            hy, hx, _ = _nearest_in_mask(mask, hub_hint)
            hub = idx[hy, hx]
        else:
            # Farthest along the band, but by straight-line reach (avoid curling back).
            far = np.hypot(coords[:, 0] - coords[ends[0], 0], coords[:, 1] - coords[ends[0], 1])
            score = np.where(d >= 0, far + 0.02 * d / max(d.max(), 1e-6), -1)
            hub = int(np.argmax(score))
    else:
        tot = np.where(reach, dist.sum(0), np.inf)
        hub = int(np.argmin(tot))
    _, pred = dijkstra(g, indices=hub, return_predecessors=True)

    # The tree: parent links from each end back to the hub.
    paths = []
    for e in ends:
        if e == hub:
            continue
        path = [e]
        while path[-1] != hub and pred[path[-1]] >= 0:
            path.append(int(pred[path[-1]]))
        if path[-1] != hub:
            continue
        paths.append(path[::-1])  # hub -> end
    # Split at branch points: count how many paths use each node.
    use = {}
    for p in paths:
        for v in p:
            use[v] = use.get(v, 0) + 1
    segs = []
    seen = set()
    for p in paths:
        start = 0
        for i in range(1, len(p)):
            if i == len(p) - 1 or use[p[i]] != use[p[i + 1]]:
                key = (p[start], p[i])
                if key not in seen and i > start:
                    seen.add(key)
                    segs.append(p[start:i + 1])
                start = i
    lines = [coords[s][:, ::-1].astype(float) for s in segs]
    return lines, coords[hub][::-1].astype(float)


def resample(line, step=0.5):
    d = np.r_[0, np.cumsum(np.hypot(*np.diff(line, axis=0).T))]
    if d[-1] < 1e-6:
        return line[:1].copy()
    n = max(2, int(np.ceil(d[-1] / step)) + 1)
    t = np.linspace(0, d[-1], n)
    return np.stack([np.interp(t, d, line[:, 0]), np.interp(t, d, line[:, 1])], 1)


def smooth(line, sigma=2.0, fix_ends=True, step=0.5):
    """Gaussian smoothing along arc length (sigma in pixels), ends kept in place."""
    line = resample(line, step)
    if len(line) < 3:
        return line
    s = sigma / step
    pad = int(3 * s) + 1
    # Reflect about the ends (odd extension) so the ends stay put and keep their direction.
    a, b = line[0], line[-1]
    head = 2 * a - line[1:pad + 1][::-1]
    tail = 2 * b - line[-pad - 1:-1][::-1]
    ext = np.concatenate([head, line, tail])
    out = np.stack([ndimage.gaussian_filter1d(ext[:, i], s, mode='nearest') for i in range(2)], 1)[len(head):len(head) + len(line)]
    if fix_ends:
        out[0], out[-1] = a, b
    return out


def straighten_end(line, point, direction, length=8.0):
    """Bends the end of `line` (the end nearest `point`) so it arrives at `point`
    exactly, heading along `direction` (unit vector pointing out of the cell)."""
    line = resample(line, 0.5)
    flip = np.hypot(*(line[0] - point)) < np.hypot(*(line[-1] - point))
    if flip:
        line = line[::-1]
    d = np.asarray(direction, float); d /= np.hypot(*d)
    p = np.asarray(point, float)
    # Arc length measured back from the end.
    seg = np.hypot(*np.diff(line, axis=0).T)
    s = np.r_[np.cumsum(seg[::-1])[::-1], 0]
    length = max(1e-3, min(length, 0.75 * s[0]))  # never move the other end
    w = np.clip(1 - s / length, 0, 1)
    w = w * w * (3 - 2 * w)
    target = p - d[None] * s[:, None]
    shift = p - line[-1]
    line = line + shift[None] * np.clip(1 - s / max(1e-3, min(3 * length, s[0])), 0, 1)[:, None]  # move the end onto the point
    line = line * (1 - w[:, None]) + target * w[:, None]
    if flip:
        line = line[::-1]
    return line


# ---------------------------------------------------------------- drawing

def to_ground(x, y):
    """Screen (iso 2:1) to ground coordinates: equal lengths on the ground are equal
    in (u, v), so widths and spacings measured here look right in perspective."""
    return 0.5 * x + y, -0.5 * x + y


class StrokeField:
    """For each pixel of a canvas region near a center line: the distance to the line
    and the signed offset across it (both measured on the ground, so strokes are
    flattened like the iso map), and the arc length along it (ground units)."""

    def __init__(self, shape, line, scale, margin=16, iso=True):
        h, w = shape
        pts = resample(np.asarray(line, float) * scale, 0.35)
        if iso:
            gu, gv = to_ground(pts[:, 0], pts[:, 1])
        else:
            gu, gv = pts[:, 0], pts[:, 1]
        g = np.stack([gu, gv], 1)
        seg = np.hypot(*np.diff(g, axis=0).T)
        s = np.r_[0, np.cumsum(seg)]
        tan = np.gradient(g, axis=0)
        tan /= np.maximum(np.hypot(tan[:, 0], tan[:, 1]), 1e-9)[:, None]
        x0 = int(max(0, np.floor(pts[:, 0].min()) - margin)); x1 = int(min(w, np.ceil(pts[:, 0].max()) + margin + 1))
        y0 = int(max(0, np.floor(pts[:, 1].min()) - margin)); y1 = int(min(h, np.ceil(pts[:, 1].max()) + margin + 1))
        self.box = (y0, y1, x0, x1)
        bh, bw = y1 - y0, x1 - x0
        px = np.clip(np.floor(pts[:, 0] - x0).astype(int), 0, bw - 1)
        py = np.clip(np.floor(pts[:, 1] - y0).astype(int), 0, bh - 1)
        seed = np.ones((bh, bw), bool)
        seed[py, px] = False
        sid = -np.ones((bh, bw), np.int64)
        sid[py, px] = np.arange(len(pts))
        # Nearest center line pixel, measured on the ground (rows count double).
        _, (iy, ix) = ndimage.distance_transform_edt(seed, sampling=(2.0, 1.0) if iso else None, return_indices=True)
        k = sid[iy, ix]
        yy, xx = np.mgrid[y0:y1, x0:x1].astype(float) + 0.5
        if iso:
            uu, vv = to_ground(xx, yy)
        else:
            uu, vv = xx, yy
        # Refine: nearest of the points k-6..k+6 (sub-pixel accuracy along the line).
        best = None
        for o in range(-6, 7):
            kk = np.clip(k + o, 0, len(pts) - 1)
            d2 = (uu - g[kk, 0]) ** 2 + (vv - g[kk, 1]) ** 2
            if best is None:
                best, bk = d2, kk
            else:
                m = d2 < best
                best = np.where(m, d2, best); bk = np.where(m, kk, bk)
        k = bk
        du, dv = uu - g[k, 0], vv - g[k, 1]
        tu, tv = tan[k, 0], tan[k, 1]
        along = du * tu + dv * tv
        self.perp = -du * tv + dv * tu
        end = ((k == 0) & (along < 0)) | ((k == len(pts) - 1) & (along > 0))
        self.dist = np.where(end, np.hypot(du, dv), np.abs(self.perp))
        self.end_cap = end
        self.s = s[k] + along
        self.past = np.where(end, np.abs(along), 0.0)  # how far beyond an end
        self.t = s[k] / max(s[-1], 1e-9)  # 0..1 along the line
        self.tu, self.tv = tu, tv
        self.length = s[-1]
        self.k = k

    def place(self, canvas_shape, values, fill=0.0):
        y0, y1, x0, x1 = self.box
        out = np.full(canvas_shape, fill, float)
        out[y0:y1, x0:x1] = values
        return out


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def downsample(img, f):
    """Box-filters an HxWxC (or HxW) float image by an integer factor."""
    h, w = img.shape[:2]
    return img.reshape(h // f, f, w // f, f, *img.shape[2:]).mean(axis=(1, 3))


def over(dst, src):
    """Alpha-composites premultiplied src (HxWx4 float, rgb premultiplied) over dst (same)."""
    a = src[..., 3:4]
    return src + dst * (1 - a)


def premul(rgb, alpha):
    rgb = np.broadcast_to(np.asarray(rgb, float), alpha.shape + (3,))
    return np.concatenate([rgb * alpha[..., None], alpha[..., None]], -1)


def to_rgba8(pm):
    a = pm[..., 3]
    rgb = np.where(a[..., None] > 1e-6, pm[..., :3] / np.maximum(a[..., None], 1e-6), 0)
    out = np.concatenate([np.clip(rgb, 0, 1), np.clip(a, 0, 1)[..., None]], -1)
    return (out * 255 + 0.5).astype(np.uint8)


def value_noise(shape, scale, seed, octaves=3):
    """Smooth multi-octave value noise in [0, 1], tileable-free (just smooth)."""
    rng = np.random.RandomState(seed)
    h, w = shape
    out = np.zeros(shape)
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        cs = max(1.0, scale / (2 ** o))
        gh, gw = int(h / cs) + 3, int(w / cs) + 3
        g = rng.rand(gh, gw)
        yy = np.arange(h) / cs
        xx = np.arange(w) / cs
        out += amp * ndimage.map_coordinates(g, np.meshgrid(yy, xx, indexing='ij'), order=3, mode='nearest')
        tot += amp
        amp *= 0.5
    out /= tot
    return np.clip((out - out.mean()) / (out.std() * 4 + 1e-9) + 0.5, 0, 1)


def diamond_mask(w, h, ss=1):
    """Anti-aliased iso diamond of a w x h cell (at the cell's resolution)."""
    yy, xx = np.mgrid[0:h, 0:w].astype(float) + 0.5
    d = np.abs(xx - w / 2) / (w / 2) + np.abs(yy - h / 2) / (h / 2)
    # Distance in pixels to the edge, approximately.
    px = (1 - d) * (w * h / 2) / np.hypot(w / 2, h / 2)
    return np.clip(px + 0.5, 0, 1)


# Connection points of a road/railroad cell (128x64 at 1x) by flag bit, and the
# direction the path continues into the neighbor there.
ROAD_POINTS = [((96, 16), (2, -1)), ((128, 32), (1, 0)), ((96, 48), (2, 1)), ((64, 64), (0, 1)),
               ((32, 48), (-2, 1)), ((0, 32), (-1, 0)), ((32, 16), (-2, -1)), ((64, 0), (0, -1))]


def traced_lines(cell_alpha, conns, sigma=2.0, straight=8.0, close=1, center_weight=2.0):
    """The smoothed center lines of an original cell (1x continuous coordinates),
    with the ends at the connection points `conns` [(point, direction)] made exact.
    Ends on the cell's border run on past it, so the neighbor's piece takes over
    without a gap."""
    h, w = cell_alpha.shape
    pts = [p for p, _ in conns]
    snap = [(min(max(p[0], 0), w - 1), min(max(p[1], 0), h - 1)) for p in pts]
    lines, hub = trace_cell(cell_alpha, snap, close=close, center_weight=center_weight)
    out = []
    for L in lines:
        L = L + 0.5
        L = smooth(L, sigma)
        for p, d in conns:
            p = np.asarray(p, float)
            for end in (0, -1):
                if np.hypot(*(L[end] - p)) < 6:
                    L = straighten_end(L, p, d, straight)
                    if p[0] in (0, w) or p[1] in (0, h):
                        dd = np.asarray(d, float) / np.hypot(*d)
                        ext = p + dd * 8
                        L = np.vstack([L, ext]) if np.hypot(*(L[-1] - p)) < 1e-3 else np.vstack([ext, L])
        out.append(L)
    return out, hub + 0.5


# ---------------------------------------------------------------- skeleton tracing

def thin(mask):
    """Zhang-Suen thinning of a bool mask to 1-pixel-wide 8-connected lines."""
    img = np.pad(mask.astype(np.uint8), 1)
    while True:
        changed = False
        for step in (0, 1):
            p = img
            P2 = p[:-2, 1:-1]; P3 = p[:-2, 2:]; P4 = p[1:-1, 2:]; P5 = p[2:, 2:]
            P6 = p[2:, 1:-1]; P7 = p[2:, :-2]; P8 = p[1:-1, :-2]; P9 = p[:-2, :-2]
            nb = [P2, P3, P4, P5, P6, P7, P8, P9]
            B = sum(n.astype(int) for n in nb)
            seq = nb + [P2]
            A = sum(((seq[i] == 0) & (seq[i + 1] == 1)).astype(int) for i in range(8))
            if step == 0:
                c = (P2 * P4 * P6 == 0) & (P4 * P6 * P8 == 0)
            else:
                c = (P2 * P4 * P8 == 0) & (P2 * P6 * P8 == 0)
            rem = (p[1:-1, 1:-1] == 1) & (B >= 2) & (B <= 6) & (A == 1) & c
            if rem.any():
                img[1:-1, 1:-1][rem] = 0
                changed = True
        if not changed:
            break
    return img[1:-1, 1:-1].astype(bool)


_N8 = [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)]


def skeleton_lines(skel):
    """Splits a 1-pixel skeleton into polylines between ends and junctions.
    Adjacent junction pixels form one junction, at their centroid.
    Returns a list of Nx2 (x, y) float arrays."""
    pts = set(zip(*np.nonzero(skel)))

    def nbrs(p):
        return [(p[0] + dy, p[1] + dx) for dy, dx in _N8 if (p[0] + dy, p[1] + dx) in pts]

    deg = {p: len(nbrs(p)) for p in pts}
    junc = {p for p in pts if deg[p] >= 3}
    # Cluster junction pixels.
    cluster = {}
    cid = 0
    for p in junc:
        if p in cluster:
            continue
        stack = [p]; cluster[p] = cid
        while stack:
            q = stack.pop()
            for r in nbrs(q):
                if r in junc and r not in cluster:
                    cluster[r] = cid; stack.append(r)
        cid += 1
    centers = {}
    for p, c in cluster.items():
        centers.setdefault(c, []).append(p)
    centers = {c: (np.mean([q[1] for q in v]), np.mean([q[0] for q in v])) for c, v in centers.items()}
    ends = {p for p in pts if deg[p] <= 1}
    visited = set()
    lines = []

    def walk(start_xy, prev_set, cur):
        path = [start_xy]
        prev = None
        while True:
            visited.add(cur)
            path.append((cur[1], cur[0]))
            if cur in junc:
                path[-1] = centers[cluster[cur]]
                return path, ('j', cluster[cur])
            if cur in ends and len(path) > 1:
                return path, ('e', cur)
            nxt = [r for r in nbrs(cur) if r != prev and r not in prev_set and (r not in visited or r in junc)]
            prev_set = ()
            if not nxt:
                return path, ('e', cur)
            nxt.sort(key=lambda r: (r not in junc, abs(r[0] - cur[0]) + abs(r[1] - cur[1])))
            prev, cur = cur, nxt[0]

    # From each junction cluster, along every exit.
    for c in centers:
        members = [p for p, k in cluster.items() if k == c]
        mset = set(members)
        for m in members:
            for r in nbrs(m):
                if r in mset or r in visited:
                    continue
                if r in junc:
                    continue
                path, _ = walk(centers[c], mset, r)
                lines.append(np.array(path, float))
    # From free ends not yet visited (isolated lines).
    for e in ends:
        if e in visited:
            continue
        nb = nbrs(e)
        visited.add(e)
        if not nb:
            continue
        path, _ = walk((e[1], e[0]), {e}, nb[0])
        lines.append(np.array(path, float))
    # Closed loops without junctions.
    rest = pts - visited - junc
    while rest:
        start = rest.pop()
        path = [(start[1], start[0])]; cur = start
        while True:
            nxt = [r for r in nbrs(cur) if r in rest]
            if not nxt:
                break
            cur = nxt[0]; rest.discard(cur); path.append((cur[1], cur[0]))
        path.append(path[0])
        if len(path) > 4:
            lines.append(np.array(path, float))
    # Junction-to-junction lines are found from both ends: keep one.
    uniq, seen = [], set()
    for L in lines:
        key = (tuple(np.round(L[0], 2)), tuple(np.round(L[-1], 2)), len(L))
        rkey = (key[1], key[0], key[2])
        if key in seen or rkey in seen:
            continue
        seen.add(key); uniq.append(L)
    return uniq


def merge_short_and_prune(lines, conn_pts, min_spur=5.0):
    """Drops short dangling spurs (ends that are no connection point)."""
    def length(L):
        return np.hypot(*np.diff(L, axis=0).T).sum() if len(L) > 1 else 0

    def near_conn(p):
        return any(np.hypot(p[0] - c[0], p[1] - c[1]) < 4 for c in conn_pts)

    for _ in range(3):
        ends = {}
        for L in lines:
            for e in (tuple(L[0]), tuple(L[-1])):
                ends[e] = ends.get(e, 0) + 1
        keep = []
        for L in lines:
            dangling = [e for e in (tuple(L[0]), tuple(L[-1])) if ends[e] == 1 and not near_conn(e)]
            if dangling and length(L) < min_spur:
                continue
            keep.append(L)
        if len(keep) == len(lines):
            break
        lines = keep
    return lines


def join_through(lines):
    """Joins lines meeting at a point where exactly two lines meet (degree-2 nodes
    left over after pruning), so they are smoothed as one."""
    lines = [L for L in lines]
    changed = True
    while changed:
        changed = False
        ends = {}
        for i, L in enumerate(lines):
            for j, e in enumerate((tuple(L[0]), tuple(L[-1]))):
                ends.setdefault(e, []).append((i, j))
        for e, lst in ends.items():
            if len(lst) == 2 and lst[0][0] != lst[1][0]:
                (i, a), (k, b) = lst
                A = lines[i] if a == 1 else lines[i][::-1]
                B = lines[k] if b == 0 else lines[k][::-1]
                lines[i] = np.vstack([A, B[1:]])
                del lines[k]
                changed = True
                break
    return lines


def skeleton_traced_lines(cell_alpha, conns, sigma=2.0, straight=8.0, close=1, min_spur=5.0):
    """Like traced_lines, but keeps the original's full network (loops, parallel
    curves) by thinning its mask instead of building a tree."""
    h, w = cell_alpha.shape
    mask = cell_alpha.copy()
    if close:
        mask = ndimage.binary_closing(np.pad(mask, 3), iterations=close)[3:-3, 3:-3] | mask
    lab, n = ndimage.label(mask, structure=np.ones((3, 3)))
    if n > 1:
        sizes = ndimage.sum(mask, lab, range(1, n + 1))
        keep = np.zeros(n + 1, bool); keep[1:] = sizes >= max(8, 0.2 * sizes.max())
        mask = keep[lab]
    skel = thin(mask)
    pts = [p for p, _ in conns]
    lines = skeleton_lines(skel)
    lines = merge_short_and_prune(lines, [(min(p[0], w - 1), min(p[1], h - 1)) for p in pts], min_spur)
    lines = join_through(lines)
    lines = [L + 0.5 for L in lines if len(L) >= 2]
    # Which line end serves each connection point: the nearest dangling end.
    deg = {}
    for L in lines:
        for e in (tuple(L[0]), tuple(L[-1])):
            deg[e] = deg.get(e, 0) + 1
    assign = []  # (line index, end, point, direction, distance)
    taken = set()
    for p, d in conns:
        pp = np.asarray(p, float)
        best = None
        for i, L in enumerate(lines):
            for end in (0, -1):
                e = tuple(L[end])
                if deg.get(e, 2) != 1 or (i, end) in taken:
                    continue
                dist = np.hypot(*(L[end] - pp))
                if dist < 16 and (best is None or dist < best[0]):
                    best = (dist, i, end)
        if best is not None:
            taken.add((best[1], best[2]))
            assign.append((best[1], best[2], pp, d, best[0]))
        else:
            # No free end nearby: branch off the nearest point of the network.
            allp = np.concatenate(lines) if lines else np.zeros((0, 2))
            if len(allp):
                q = allp[np.argmin(np.hypot(*(allp - pp).T))]
                lines.append(np.array([q, (q + pp) / 2, pp]))
                assign.append((len(lines) - 1, -1, pp, d, 0.0))
    smoothed = [smooth(L, sigma) for L in lines]
    out = []
    for i, L in enumerate(smoothed):
        for (j, end, pp, d, dist) in assign:
            if j != i:
                continue
            if end == 0:
                L = L[::-1]
            L = straighten_end(L, pp, d, max(straight, 2.0 * dist))
            if pp[0] in (0, w) or pp[1] in (0, h):
                dd = np.asarray(d, float) / np.hypot(*d)
                L = np.vstack([L, pp + dd * 8])
            if end == 0:
                L = L[::-1]
        out.append(L)
    return out
