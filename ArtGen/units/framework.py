"""Renders a catalogued unit's flics into the remade sheets.

For each flic the INI lists, every direction (row) and frame (column) is
rendered by asking the unit's archetype for the model at that frame, turning
it to the row's direction and drawing it with render.render_cell into a cell
2x the flic's frame size, with the unit's ground anchor where the original's
feet are.

Calibration (per unit): the original DEFAULT flic gives the ground anchor
(the point between the feet, in the game's 240x240 unit space) and the
standing height; the remake's scale is fitted so its standing height matches
(catalog 'scale' multiplies it, 'anchor' overrides it).
"""
import json
import os
import time

import numpy as np
from PIL import Image

import flic as F  # (first: puts ArtGen/common on the path)
import civ3art
import mesh as M
import render as R
import models

SCALE = civ3art.SCALE
# Pixels per meter at 1x for a standard figure; calibration adjusts per unit.
BASE_PPM = 26.0


class ActionContext:
    """What an archetype knows about the original flic it is remaking."""

    def __init__(self, unit, action, fl, metrics):
        self.unit = unit
        self.action = action
        self.flic = fl
        self.n = fl.n_frames
        self.speed = fl.speed
        self.m = metrics       # dict of per-frame profiles (see flic_metrics)

    def event(self, name, default=None):
        """Normalized time (0..1) of an event in the original's motion:
        'reach_max' (the frame where the unit extends furthest from its
        anchor, e.g. an attack's strike), 'low' (lowest height, e.g. a crouch or
        the body on the ground), 'down' (the first frame below 55% of the
        starting height: a death's fall), 'high' (tallest frame)."""
        m = self.m
        if m is None or self.n < 2:
            return default
        h, r = np.asarray(m['height']), np.asarray(m['reach'])
        if name == 'reach_max':
            f = int(np.argmax(r))
        elif name == 'low':
            f = int(np.argmin(h))
        elif name == 'high':
            f = int(np.argmax(h))
        elif name == 'down':
            idx = np.nonzero(h < 0.55 * h[0])[0]
            if len(idx) == 0:
                return default
            f = int(idx[0])
        else:
            return default
        return f / (self.n - 1)


def flic_metrics(fl, anchor):
    """Per-frame height and reach of the original (averaged over the
    directions, in 1x pixels): height = foot line minus the top, reach = the
    farthest opaque pixel horizontally from the anchor."""
    hs, rs = [], []
    for f in range(fl.n_frames):
        hh, rr = [], []
        for d in range(fl.n_anims):
            idx = fl.frames[d, f]
            m = ~F.shadow_mask(idx) & (idx != 255)
            ys, xs = np.nonzero(m)
            if len(ys) == 0:
                continue
            hh.append(anchor[1] - (ys.min() + fl.offset_top))
            rr.append(np.abs(xs + fl.offset_left - anchor[0]).max())
        hs.append(float(np.mean(hh)) if hh else 0.0)
        rs.append(float(np.mean(rr)) if rr else 0.0)
    return {'height': hs, 'reach': rs}


def original_stance(unit):
    """(anchor x, anchor y, standing height) of the original's DEFAULT flic in
    the 240x240 unit space at 1x, averaged over the directions."""
    name = F.unit_flics(unit)['DEFAULT']
    fl = F.load_unit_flic(unit, name)
    ax, ay, hh = [], [], []
    for d in range(fl.n_anims):
        idx = fl.frames[d, 0]
        m = ~F.shadow_mask(idx) & (idx != 255)
        ys, xs = np.nonzero(m)
        if len(ys) == 0:
            continue
        by = ys.max()
        ax.append(xs[ys >= by - 3].mean() + fl.offset_left)
        ay.append(by + fl.offset_top)
        hh.append(by - ys.min() + 1)
    return float(np.median(ax)), float(np.median(ay)) - 1.5, float(np.median(hh))


def model_extent(model, ppm):
    """Standing height in 1x pixels of the model's DEFAULT frame 0 (median
    over the directions), and its lowest point offset."""
    hs = []
    for d in range(8):
        mesh = model.frame('DEFAULT', 0, 15, None).transformed(R.facing_matrix(d))
        cam = R.Camera(ppm, (0, 0))
        S = cam.project(mesh.V)
        hs.append((S[..., 1].max() - S[..., 1].min()) / SCALE)
    return float(np.median(hs))


class UnitJob:
    """Everything needed to render one unit."""

    def __init__(self, unit, entry):
        self.unit = unit
        self.entry = entry
        models.load_all()
        cls = models.get(entry['archetype'])
        self.model = cls(unit, entry)
        ax, ay, oh = original_stance(unit)
        if 'anchor' in entry:
            ax, ay = entry['anchor']
        self.anchor = (ax, ay)
        ppm = BASE_PPM * SCALE
        mh = model_extent(self.model, ppm)
        # The original's height includes its 1px pixel edge; ours its soft contour.
        fit = (oh - 0.5) / max(mh, 1e-3)
        self.ppm = ppm * fit * entry.get('scale', 1.0)
        self.orig_height = oh

    def flics(self):
        return F.unit_flics(self.unit)

    def render_flic(self, action, name, rows=None, cols=None):
        """The (base, tint) sheets of one flic as uint8 RGBA arrays, plus the
        Flic. rows/cols restrict the rendering (for previews)."""
        fl = F.load_unit_flic(self.unit, name)
        W, H = fl.width * SCALE, fl.height * SCALE
        metrics = flic_metrics(fl, self.anchor)
        ctx = ActionContext(self.unit, action, fl, metrics)
        rows = range(fl.n_anims) if rows is None else rows
        cols = range(fl.n_frames) if cols is None else cols
        base = np.zeros((fl.n_anims * H, fl.n_frames * W, 4), np.uint8)
        tint = np.zeros_like(base)
        anchor = ((self.anchor[0] - fl.offset_left) * SCALE, (self.anchor[1] - fl.offset_top) * SCALE)
        cam = R.Camera(self.ppm, anchor)
        cache = {}
        for f in cols:
            local = self.model.frame(action, f, fl.n_frames, ctx)
            mats = self.model.mats.arrays()  # props may add materials lazily
            for d in rows:
                if d >= 8:
                    continue
                mesh = local.transformed(R.facing_matrix(d))
                # An archetype may draw its cells itself (e.g. ships: wake, subtle shadow).
                b, t = getattr(self.model, 'render_cell', R.render_cell)(mesh, mats, cam, W, H)
                base[d * H:(d + 1) * H, f * W:(f + 1) * W] = b
                tint[d * H:(d + 1) * H, f * W:(f + 1) * W] = t
        return base, tint, fl


def sheet_path(unit, name, suffix='', animations=False):
    """Where a flic's sheet goes: C7/ModernArt (committed) for the core art,
    or with animations=True C7/ModernArtAnimations (not committed)."""
    rel = F.flic_rel(unit, name)
    path = os.path.splitext(civ3art.modern_path(rel))[0] + suffix + '.png'
    if animations:
        path = os.path.join(civ3art.MODERN_ART_ANIMATIONS, os.path.relpath(path, civ3art.MODERN_ART))
    return path


def _save_pair(base, tint, p, pt):
    os.makedirs(os.path.dirname(p), exist_ok=True)
    Image.fromarray(np.ascontiguousarray(base), 'RGBA').save(p, compress_level=9)
    if tint[..., 3].any():
        Image.fromarray(np.ascontiguousarray(tint), 'RGBA').save(pt, compress_level=9)
    elif os.path.exists(pt):
        os.remove(pt)
    return [p] + ([pt] if os.path.exists(pt) else [])


def save_sheets(unit, name, fl, base, tint):
    """Writes <flic>.png and <flic>_tint.png (the tint sheet only if the unit
    has civ-colored pixels), checking the sheet layout against the flic.

    By flic.flic_kind: a worker's job loop goes whole to C7/ModernArt; a
    standing/fortified flic goes whole to C7/ModernArtAnimations plus, as a
    one-column "still" of its last frame (what the game shows with animations
    off), to C7/ModernArt; any other animation only to ModernArtAnimations."""
    exp = (fl.n_anims * fl.height * SCALE, fl.n_frames * fl.width * SCALE)
    assert base.shape[:2] == exp, f'{unit}/{name}: sheet {base.shape[:2]} != {exp}'
    kind = F.flic_kind(unit, name)
    full_core = kind == 'loop'
    paths = _save_pair(base, tint, sheet_path(unit, name, animations=not full_core),
                       sheet_path(unit, name, '_tint', animations=not full_core))
    if kind == 'still':
        W = fl.width * SCALE
        paths += _save_pair(base[:, -W:], tint[:, -W:], sheet_path(unit, name), sheet_path(unit, name, '_tint'))
    return paths


def save_still(unit, name, fl, base, tint):
    """Writes just the committed still of a standing/fortified flic: the last
    frame's column of sheets rendered with only that frame (render_flic with
    cols=[last])."""
    W = fl.width * SCALE
    return _save_pair(base[:, -W:], tint[:, -W:], sheet_path(unit, name), sheet_path(unit, name, '_tint'))


def clipped_fraction(base, tint, fl):
    """How much of the sprite touches the cell borders (a sign the remake
    reaches outside the original's frame and gets cut off)."""
    H, W = fl.height * SCALE, fl.width * SCALE
    a = np.maximum(base[..., 3], tint[..., 3]).astype(np.float32) / 255
    edge = np.zeros_like(a, bool)
    for r in range(fl.n_anims):
        for c in range(fl.n_frames):
            y0, x0 = r * H, c * W
            edge[y0, x0:x0 + W] = edge[y0 + H - 1, x0:x0 + W] = True
            edge[y0:y0 + H, x0] = edge[y0:y0 + H, x0 + W - 1] = True
    return float((a[edge] > 0.3).sum()) / max(1.0, float((a > 0.3).sum())) * 100
