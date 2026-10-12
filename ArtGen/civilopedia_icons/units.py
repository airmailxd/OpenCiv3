"""Unit icons: the unit's own remade 3D model (ArtGen/units), rendered up
close in a three-quarter view on a quiet backdrop (land, sea or sky).

The model is the same one the map sprites are rendered from (catalog entry,
archetype, standing pose), so each icon matches its unit on the map; it is
only drawn bigger, at the icon's resolution, with the civ-colored parts in a
civ color.
"""
import math

import numpy as np

import common as C
import render as R
import mesh as M
import catalog
import models

# A unit's civ-colored parts, unless it belongs to one civilization.
DEFAULT_TINT = C.mute(C.hex_rgb('4363D8'), 0.8, 0.95)
YAW = 148.0   # facing a little left of the camera (south-southwest)


class _Ctx:
    """Stands in for framework.ActionContext: no original flic to measure."""

    def __init__(self, unit, action, n):
        self.unit, self.action, self.n, self.speed, self.m, self.flic = unit, action, n, 60, None, None

    def event(self, name, default=None):
        return default


class IconStyle(R.Style):
    ss = 4


_catalog = None


def unit_entry(folder):
    global _catalog
    if _catalog is None:
        _catalog = catalog.load()
        models.load_all()
    for k, v in _catalog.items():
        if k.lower() == folder.lower():
            return k, v
    raise KeyError(folder)


def _yaw(deg):
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    T = np.eye(4, dtype=np.float32)
    T[0:2, 0:2] = [[c, -s], [s, c]]
    return T


def model_mesh(folder, action='DEFAULT'):
    name, entry = unit_entry(folder)
    model = models.ARCHETYPES[entry['archetype']](name, entry)
    n = 15
    try:
        local = model.frame(action, n - 1, n, _Ctx(name, action, n))
    except Exception:
        local = model.frame(action, 0, n, None)
    return model, local


def tinted(base, tint, color):
    b = base.astype(np.float32) / 255
    t = tint.astype(np.float32) / 255
    tc = t[..., :3] * np.asarray(color, np.float32)
    a = t[..., 3:4]
    rgb = b[..., :3] * b[..., 3:4] * (1 - a) + tc * a
    al = b[..., 3:4] * (1 - a) + a
    return np.concatenate([rgb / np.maximum(al, 1e-6), al], -1)


def render_unit(folder, size, color, kind='land', yaw=YAW, fill=0.80, action='DEFAULT'):
    """The unit (RGBA float, transparent background) fitted in a size x size
    square."""
    model, local = model_mesh(folder, action)
    mesh = local.transformed(_yaw(yaw))
    mats = model.mats.arrays()
    cam1 = R.Camera(1.0, (0.0, 0.0))
    S = cam1.project(mesh.V).reshape(-1, 3)
    x0, x1 = S[:, 0].min(), S[:, 0].max()
    y0, y1 = S[:, 1].min(), S[:, 1].max()
    w, h = x1 - x0, y1 - y0
    ppm = min(size * fill / max(w, 1e-3), size * fill / max(h, 1e-3))
    if kind == 'land':
        # feet near the bottom, centered horizontally
        foot = 0.90 if h * ppm > size * 0.6 else 0.84
        anchor = (size / 2 - (x0 + x1) / 2 * ppm, size * foot - y1 * ppm)
        # keep the head inside
        top = anchor[1] + y0 * ppm
        if top < size * 0.06:
            anchor = (anchor[0], anchor[1] + size * 0.06 - top)
    else:
        anchor = (size / 2 - (x0 + x1) / 2 * ppm, size * (0.60 if kind == 'sea' else 0.5) - (y0 + y1) / 2 * ppm)
    cam = R.Camera(ppm, anchor)
    rc = getattr(model, 'render_cell', None)
    if rc is not None:
        b, t = rc(mesh, mats, cam, size, size)
    else:
        b, t = R.render_cell(mesh, mats, cam, size, size, style=IconStyle, shadow=(kind == 'land'))
    return tinted(b, t, color)


def render_unit_at(folder, action, t, ppm, anchor, W, H, color=None, yaw=YAW):
    """The unit at time t (0..1) of an action, drawn with a camera of `ppm`
    pixels per meter with its feet at `anchor`, into a W x H RGBA (float)."""
    name, entry = unit_entry(folder)
    model = models.ARCHETYPES[entry['archetype']](name, entry)
    n = 30
    f = min(n - 1, int(round(t * (n - 1))))
    local = model.frame(action, f, n, _Ctx(name, action, n))
    mesh = local.transformed(_yaw(yaw))
    b, tn = R.render_cell(mesh, model.mats.arrays(), R.Camera(ppm, anchor), W, H, style=IconStyle, shadow=True)
    return tinted(b, tn, DEFAULT_TINT if color is None else color)


# ---------------------------------------------------------------- backdrops

def backdrop(kind, n, seed=0):
    if kind == 'sea':
        sky = C.vgradient(n, C.hex_rgb('b9c8d0'), C.hex_rgb('dfe2da'))
        water_top, water_bot = C.hex_rgb('6f8f9c'), C.hex_rgb('3f6575')
        hz = int(n * 0.40)
        yy = np.arange(n, dtype=np.float32)[:, None]
        t = np.clip((yy - hz) / (n - hz), 0, 1)[..., None]
        water = C.mix(water_top, water_bot, t ** 0.8)
        nz = C.noise(n, n / 6, seed + 3, 3)[..., None]
        water = water * (0.94 + 0.10 * nz)
        rgb = np.where((yy >= hz)[..., None], np.broadcast_to(water, (n, n, 3)), sky)
        # a soft horizon
        band = np.exp(-((yy - hz) / (n * 0.012)) ** 2)[..., None]
        rgb = rgb * (1 - 0.25 * band) + C.hex_rgb('e8ebe4') * 0.25 * band
    elif kind == 'air':
        rgb = C.vgradient(n, C.hex_rgb('8fa9bd'), C.hex_rgb('dde3e2'), 0.9)
        cl = C.noise(n, n / 3, seed + 11, 4)
        cl = np.clip((cl - 0.52) * 3.2, 0, 1)[..., None]
        rgb = rgb * (1 - 0.55 * cl) + C.hex_rgb('f2f2ee') * 0.55 * cl
    else:
        sky = C.vgradient(n, C.hex_rgb('c4cfd2'), C.hex_rgb('e6e2d4'))
        hz = int(n * 0.58)
        yy = np.arange(n, dtype=np.float32)[:, None]
        t = np.clip((yy - hz) / (n - hz), 0, 1)[..., None]
        ground = C.mix(C.hex_rgb('b3ad8a'), C.hex_rgb('8f8a63'), t)
        nz = C.noise(n, n / 5, seed + 5, 3)[..., None]
        ground = ground * (0.95 + 0.08 * nz)
        rgb = np.where((yy >= hz)[..., None], np.broadcast_to(ground, (n, n, 3)), sky)
        band = np.exp(-((yy - hz) / (n * 0.02)) ** 2)[..., None]
        rgb = rgb * (1 - 0.3 * band) + C.hex_rgb('d9d6c3') * 0.3 * band
    rgb = C.vignette(rgb, 0.16)
    return np.concatenate([rgb, np.ones((n, n, 1), np.float32)], -1)


def unit_kind(proto):
    cats = proto.get('categories') or ['Land']
    if 'Sea' in cats:
        return 'sea'
    if 'Air' in cats:
        return 'air'
    name = proto['civilopediaEntry']
    if name in ('PRTO_Cruise_Missile', 'PRTO_ICBM', 'PRTO_NUKE'):
        return 'air'
    return 'land'


def make(proto, color=None):
    """(large, small) icons of a ruleset unit prototype."""
    folder = proto['art']['mainArt']['defaultName']
    kind = unit_kind(proto)
    color = DEFAULT_TINT if color is None else color
    seed = sum(map(ord, proto['civilopediaEntry']))
    out = []
    for n, fill in ((C.LARGE, 0.80), (C.SMALL * 2, 0.92)):
        bg = backdrop(kind, n, seed)
        u = render_unit(folder, n, color, kind, fill=fill)
        img = C.over(u, bg)
        img[..., :3] = C.frame_edges(img[..., :3])
        out.append(img)
    large, small2 = out
    small = C.sharpen(C.downsample(small2, 2), 0.35, 0.5)
    return large, small
