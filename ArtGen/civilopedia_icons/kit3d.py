"""A small kit for the symbolic icons (advances, governments, civilizations):
3D objects made of the unit renderer's primitives (ArtGen/units/mesh.py) and
of reliefs (raised 2D shapes drawn with Pillow), rendered with the unit
renderer's shading (ArtGen/units/render.py: light from the upper left, soft
shading, a thin dark contour) and fitted into the icon.

Model space: x right, y away from the camera, z up (meters, but only
proportions matter: the render fits the object to the icon).
"""
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage

import common as C
import mesh as M
import render as R

FONT = os.path.join(C.REPO, 'C7', 'Fonts', 'NotoSans-Bold.ttf')

# Standard materials: name -> (color, spec, gloss, metal)
STD = {
    'gold': ('#d9b45a', 0.55, 18, True),
    'bronze': ('#b07a45', 0.5, 16, True),
    'copper': ('#c27c55', 0.5, 16, True),
    'brass': ('#c9a85a', 0.5, 18, True),
    'steel': ('#a7adb2', 0.55, 24, True),
    'iron': ('#6d7176', 0.4, 18, True),
    'dark_metal': ('#4a4e54', 0.4, 18, True),
    'silver': ('#d0d4d8', 0.6, 26, True),
    'wood': ('#8a5f3a', 0.08, 10, False),
    'dark_wood': ('#5e3f26', 0.08, 10, False),
    'light_wood': ('#b48a5a', 0.08, 10, False),
    'stone': ('#a9a293', 0.06, 8, False),
    'dark_stone': ('#7c776c', 0.06, 8, False),
    'marble': ('#e6e1d4', 0.15, 14, False),
    'clay': ('#b8684a', 0.08, 10, False),
    'terracotta': ('#c07a4e', 0.1, 10, False),
    'parchment': ('#e8dcb8', 0.05, 8, False),
    'paper': ('#f0ece0', 0.05, 8, False),
    'ink': ('#3a2c22', 0.05, 8, False),
    'leather': ('#7a4e30', 0.12, 10, False),
    'red': ('#b8463c', 0.15, 14, False),
    'dark_red': ('#7e2b26', 0.15, 14, False),
    'blue': ('#4a6fa5', 0.15, 14, False),
    'navy': ('#2f4466', 0.15, 14, False),
    'green': ('#5b8a4a', 0.12, 12, False),
    'dark_green': ('#3d6236', 0.12, 12, False),
    'leaf': ('#6f9a48', 0.12, 12, False),
    'yellow': ('#d8b84a', 0.15, 14, False),
    'orange': ('#d0823a', 0.15, 14, False),
    'purple': ('#7a5a96', 0.15, 14, False),
    'white': ('#eeebe4', 0.12, 14, False),
    'black': ('#2c2c30', 0.25, 20, False),
    'grey': ('#8c8c88', 0.1, 12, False),
    'glass': ('#a8c8d4', 0.8, 40, False),
    'water': ('#5b8fb0', 0.6, 30, False),
    'flame': ('#f2b440', 0.0, 4, False),
    'flame_core': ('#fff0b0', 0.0, 4, False),
    'rope': ('#b49a6a', 0.05, 8, False),
    'cloth': ('#d8d0bc', 0.05, 8, False),
    'skin': ('#c8946a', 0.1, 10, False),
}


class Kit:
    """Materials and parts of one icon."""

    def __init__(self):
        self.mats = M.Materials()
        self.parts = []

    def m(self, name, color=None, spec=None, gloss=None, metal=None):
        """A material id: a standard one by name, or a custom color."""
        if color is None and name in self.mats:
            return self.mats[name]
        c, s, g, mt = STD.get(name, ('#999999', 0.1, 12, False))
        return self.mats.add(name, color or c, spec=s if spec is None else spec,
                             gloss=g if gloss is None else gloss, metal=mt if metal is None else metal)

    def add(self, mesh, T=None):
        if T is not None:
            mesh = mesh.transformed(T)
        self.parts.append(mesh)
        return mesh

    def mesh(self):
        return M.Mesh.concat(self.parts)

    # ---- shapes ----
    def cyl(self, a, b, r0, mat, r1=None, seg=20, caps=True):
        r1 = r0 if r1 is None else r1
        m = M.cylinder(r0, r1, 1.0, self._mat(mat), seg=seg, caps=caps)
        return self.add(m, M.look_along(a, b))

    def sph(self, c, r, mat, seg=20, scale=(1, 1, 1)):
        return self.add(M.ellipsoid(r * scale[0], r * scale[1], r * scale[2], self._mat(mat), center=c, seg=seg))

    def box(self, size, mat, center=(0, 0, 0), T=None, bevel=0.0):
        m = M.box(*size, self._mat(mat), bevel=bevel)
        T2 = M.translate(*center)
        if T is not None:
            T2 = T2 @ T
        return self.add(m, T2)

    def lathe(self, profile, mat, T=None, seg=28, smooth=True):
        """profile [(z, r), ...] about the z axis."""
        return self.add(M.lathe(profile, self._mat(mat), seg=seg, smooth=smooth), T)

    def torus(self, R0, r, mat, T=None, seg=32, tube=12):
        prof = [(r * math.sin(2 * math.pi * k / tube), R0 + r * math.cos(2 * math.pi * k / tube)) for k in range(tube + 1)]
        return self.lathe(prof, mat, T, seg=seg)

    def extrude(self, poly, depth, mat, T=None):
        """A 2D polygon [(x, z)] (star-shaped around its centroid) extruded
        along y."""
        return self.add(M.extrude(poly, depth, self._mat(mat)), T)

    def relief(self, layers, width, T=None, base=None):
        """A raised shape standing in the x-z plane, facing the camera (-y):
        `layers` is a list of (mask HxW bool, material, height, bevel px),
        each raised on top of the ones before it; `width` is the size of the
        mask's width in model units."""
        return self.add(relief_mesh(layers, width, self), T)

    def _mat(self, mat):
        return self.m(mat) if isinstance(mat, str) else mat


# ---------------------------------------------------------------- reliefs

def relief_mesh(layers, width, kit):
    H, W = layers[0][0].shape
    px = width / W
    height = np.zeros((H, W), np.float32)
    matmap = np.full((H, W), -1, np.int32)
    for mask, mat, h, bevel in layers:
        mask = np.asarray(mask, bool)
        if not mask.any():
            continue
        d = ndimage.distance_transform_edt(np.pad(mask, 1))[1:-1, 1:-1]
        prof = np.clip(d / max(bevel, 1e-3), 0, 1)
        prof = np.sin(prof * math.pi / 2) ** 0.8
        height = height + mask * prof * h
        matmap[mask] = kit._mat(mat)
    inside = matmap >= 0
    # vertices at pixel centers, plus a ring of zero height around the shape
    ys, xs = np.mgrid[0:H, 0:W]
    X = (xs - W / 2 + 0.5) * px
    Z = (H / 2 - ys - 0.5) * px
    Y = -height
    verts = np.stack([X, Y, Z], -1).reshape(-1, 3)
    idx = np.arange(H * W).reshape(H, W)
    a, b, c, d = idx[:-1, :-1], idx[:-1, 1:], idx[1:, :-1], idx[1:, 1:]
    ins = inside.reshape(-1)
    faces, fm = [], []
    mm = matmap.reshape(-1)
    for tri in ((a, c, b), (b, c, d)):
        t = np.stack([x.reshape(-1) for x in tri], 1)
        any_in = ins[t].any(1)
        t = t[any_in]
        # the material of the triangle: of its inside corners, the topmost layer's
        m3 = mm[t]
        mat = np.where(m3 >= 0, m3, -1).max(1)
        faces.append(t)
        fm.append(mat)
    faces = np.concatenate(faces)
    fm = np.concatenate(fm)
    mesh = M.from_indexed(verts, faces, 0, smooth=True)
    mesh.M = fm.astype(np.int32)
    # a flat back so the object has a little thickness from the side
    return mesh


def canvas(n=256):
    """A blank mask canvas and its drawer (mode 'L'; draw with fill=255)."""
    im = Image.new('L', (n, n), 0)
    return im, ImageDraw.Draw(im)


def mask_of(im):
    return np.asarray(im, np.uint8) > 127


def text_mask(text, n=256, size=0.7, font=FONT, dy=0.0):
    im, d = canvas(n)
    f = ImageFont.truetype(font, int(n * size))
    l, t, r, b = d.textbbox((0, 0), text, font=f)
    d.text(((n - (r - l)) / 2 - l, (n - (b - t)) / 2 - t + dy * n), text, font=f, fill=255)
    return mask_of(im)


def star_points(cx, cy, r_out, r_in, n=5, rot=-90):
    pts = []
    for k in range(2 * n):
        r = r_out if k % 2 == 0 else r_in
        a = math.radians(rot + 180 * k / n)
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def gear_poly(n, r_out, r_in, tooth=0.5, cx=0.0, cz=0.0, rot=0.0, steps=6):
    """A gear outline [(x, z)] with n teeth."""
    pts = []
    for k in range(n):
        a0 = 2 * math.pi * k / n + rot
        w = 2 * math.pi / n
        for (f, r) in ((0.0, r_in), (0.5 - tooth / 2 - 0.08, r_in), (0.5 - tooth / 2, r_out), (0.5 + tooth / 2, r_out),
                       (0.5 + tooth / 2 + 0.08, r_in)):
            a = a0 + f * w
            pts.append((cx + r * math.cos(a), cz + r * math.sin(a)))
    return pts


# ---------------------------------------------------------------- rendering

class IconStyle(R.Style):
    ss = 4
    contour_alpha = 0.55


def _fit(mesh, size, fill, center=(0.5, 0.5)):
    S = R.Camera(1.0, (0.0, 0.0)).project(mesh.V).reshape(-1, 3)
    x0, x1, y0, y1 = S[:, 0].min(), S[:, 0].max(), S[:, 1].min(), S[:, 1].max()
    ppm = min(size * fill / max(x1 - x0, 1e-6), size * fill / max(y1 - y0, 1e-6))
    anchor = (size * center[0] - (x0 + x1) / 2 * ppm, size * center[1] - (y0 + y1) / 2 * ppm)
    return R.Camera(ppm, anchor)


def render(kit, size, yaw=-20.0, pitch=0.0, fill=0.8, ground=False, center=(0.5, 0.5)):
    """The kit's object (RGBA float, transparent) fitted into size x size.
    yaw turns it about z (+ = counter-clockwise from above), pitch tips its
    top toward (+) or away from the camera."""
    mesh = kit.mesh().transformed(M.rot_z(yaw) @ M.rot_x(-pitch))
    if ground:
        mesh = mesh.transformed(M.translate(0, 0, -float(mesh.V[..., 2].min())))
    cam = _fit(mesh, size, fill, center)
    b, t = R.render_cell(mesh, kit.mats.arrays(), cam, size, size, style=IconStyle, shadow=ground)
    out = b.astype(np.float32) / 255
    if t[..., 3].any():
        out = C.over(t.astype(np.float32) / 255, out)
    return out


def drop_shadow(obj, dx, dy, r, alpha=0.35):
    a = C.blur(obj[..., 3], r)
    a = np.roll(np.roll(a, int(dy), 0), int(dx), 1)
    sh = np.zeros_like(obj)
    sh[..., 3] = a * alpha
    return sh


def radial_backdrop(n, inner, outer, cy=0.42):
    d = C.radial(n, n * 0.45, n * cy, n * 0.75)
    t = np.clip(d, 0, 1)[..., None] ** 1.2
    rgb = C.mix(C.hex_rgb(inner), C.hex_rgb(outer), t)
    return np.concatenate([rgb, np.ones((n, n, 1), np.float32)], -1)


def compose(kit, inner, outer, yaw=-20.0, pitch=0.0, fill=0.8, ground=False, small_fill=None, center=(0.5, 0.5)):
    """(large, small) icons: the object over a radial backdrop with a soft
    shadow."""
    out = []
    for n, f in ((C.LARGE, fill), (C.SMALL * 2, small_fill or min(0.94, fill * 1.12))):
        bg = radial_backdrop(n, inner, outer)
        obj = render(kit, n, yaw, pitch, f, ground, center)
        k = n / C.LARGE
        img = C.over(obj, C.over(drop_shadow(obj, 4 * k, 5 * k, 3 * k, 0.0 if ground else 0.32), bg))
        img[..., :3] = C.frame_edges(img[..., :3])
        out.append(img)
    large, small2 = out
    small = C.sharpen(C.downsample(small2, 2), 0.35, 0.5)
    return large, small
