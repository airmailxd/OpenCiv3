"""Shared helpers for the non-human unit models (siege.py, ships.py).

A model is a root empty holding mesh parts (each a props.Prop joined into one
object) and crew empties. Model space: meters, the unit faces +Y, +X is its
right (starboard), z = 0 the ground (a ship's waterline).

    root = vkit.root('Catapult')
    p = vkit.part(root, 'frame', colors)      # a props.Prop to add geometry to
    vkit.beam(p, (x0, y0, z0), (x1, y1, z1), 0.14, 0.14, 'wood')
    p.finish_into(root)                        # (part() wires this up)
    vkit.crew(root, 0, (0.5, -1.6, 0.0), yaw=0)
"""
import math

import bpy
from mathutils import Matrix, Vector

import props

# Material keys the vehicle models use, on top of props.DEFAULTS:
# key: (color, roughness, metallic, detail kind). Catalog colors override.
props.DEFAULTS.update({
    'wood_dark': ('#5e4628', 0.65, 0.0, 'wood'),
    'wood_light': ('#c8aa74', 0.6, 0.0, 'wood'),
    'side_rail': ('#8e7250', 0.62, 0.0, 'wood'),
    'beam': ('#e8e8e8', 0.6, 0.0, 'wood'),
    'iron': ('#5e5e5c', 0.5, 1.0, 'metal'),
    'carriage': ('#3a4048', 0.55, 0.0, 'wood'),
    'carriage_dark': ('#262a30', 0.6, 0.0, 'wood'),
    'wheel': ('#e8e8e8', 0.6, 0.0, 'wood'),
    'felloe': ('#4e3c2e', 0.62, 0.0, 'wood'),
    'hub': ('#2a2a2c', 0.45, 1.0, 'metal'),
    'deck': ('#c0a470', 0.7, 0.0, 'wood'),
    'rail': ('#5c4628', 0.62, 0.0, 'wood'),
    'sail': ('#f2e2b6', 0.9, 0.0, 'cloth'),
    'gold': ('#b08a40', 0.38, 1.0, 'metal'),
    'tint': ('#ffffff', 0.7, 0.0, 'cloth'),
    'cordage': ('#6e5a40', 0.85, 0.0, None),
})


def root(name):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = 'PLAIN_AXES'
    bpy.context.collection.objects.link(e)
    return e


class Part(props.Prop):
    """A props.Prop whose finished object is parented to the model root."""

    def __init__(self, root_obj, name, colors):
        super().__init__(f'{root_obj.name}_{name}', colors)
        self.root = root_obj

    def finish(self):
        obj = super().finish()
        obj.parent = self.root
        return obj


def part(root_obj, name, colors):
    return Part(root_obj, name, colors)


def crew(root_obj, n, pos, yaw=0.0):
    """An empty 'crew_<n>' where a crewman stands (feet at pos, facing +Y
    turned by yaw degrees); the Mac session places a human body there."""
    e = bpy.data.objects.new(f'crew_{n}', None)
    e.empty_display_type = 'SINGLE_ARROW'
    bpy.context.collection.objects.link(e)
    e.parent = root_obj
    e.location = pos
    e.rotation_euler = (0.0, 0.0, math.radians(yaw))
    return e


def align(a, b):
    """The matrix putting a part built along +Z from 0 to |b - a| between a and b."""
    a, b = Vector(a), Vector(b)
    d = b - a
    q = d.normalized().to_track_quat('Z', 'Y')
    return Matrix.Translation(a) @ q.to_matrix().to_4x4(), d.length


def beam(p, a, b, w, h, key, bevel=0.012, tint=False, smooth=False):
    """A squared timber from a to b, w wide (across) and h deep."""
    m, length = align(a, b)
    bx = props.box(w, h, length)
    p.add(bx, key, m @ props.T(0, 0, length / 2), bevel=min(bevel, w * 0.25, h * 0.25), tint=tint, smooth=smooth)


def rod(p, a, b, r, key, seg=10, r1=None, tint=False, bevel=0.0):
    """A round rod (pole, axle, line) from a to b."""
    m, length = align(a, b)
    t = props.tube([(0, 0, 0), (0, 0, length)], [r, r if r1 is None else r1], seg=seg)
    p.add(t, key, m, smooth=True, tint=tint, bevel=bevel)


def ring_x(p, r_out, r_in, width, key, center=(0, 0, 0), seg=32, tint=False, bevel=0.006):
    """A wheel rim / tire: an annulus around the X axis."""
    rings = []
    for x in (-width / 2, width / 2):
        rings.append([(x, r_out * math.cos(2 * math.pi * k / seg), r_out * math.sin(2 * math.pi * k / seg))
                      for k in range(seg)])
    outer = props.loft(rings, False, False)
    inner_rings = [[(x, r_in * math.cos(2 * math.pi * k / seg), r_in * math.sin(2 * math.pi * k / seg))
                    for k in range(seg)] for x in (width / 2, -width / 2)]
    inner = props.loft(inner_rings, False, False)
    side = []
    for x, sgn in ((-width / 2, -1), (width / 2, 1)):
        side.append(props.loft([
            [(x, r_in * math.cos(2 * math.pi * k / seg), r_in * math.sin(2 * math.pi * k / seg)) for k in range(seg)],
            [(x, r_out * math.cos(2 * math.pi * k / seg), r_out * math.sin(2 * math.pi * k / seg)) for k in range(seg)],
        ], False, False))
    M = props.T(*center)
    for bm in [outer, inner] + side:
        p.add(bm, key, M, smooth=True, tint=tint)


def spoked_wheel(p, r, width, rim_key, spoke_key, hub_key, n_spokes=8, center=(0, 0, 0), tire_key=None,
                 tint_spokes=False, tint_rim=False):
    """A cart / gun wheel around the X axis: felloe rim, optional iron tire,
    spokes and a hub."""
    cx, cy, cz = center
    ring_x(p, r * 0.97, r * 0.84, width, rim_key, center, tint=tint_rim)
    if tire_key:
        ring_x(p, r, r * 0.955, width * 1.06, tire_key, center)
    rod(p, (cx - width * 0.9, cy, cz), (cx + width * 0.9, cy, cz), r * 0.17, hub_key, seg=12)
    for i in range(n_spokes):
        a = 2 * math.pi * i / n_spokes
        tip = (cx, cy + math.cos(a) * r * 0.86, cz + math.sin(a) * r * 0.86)
        beam(p, (cx, cy + math.cos(a) * r * 0.12, cz + math.sin(a) * r * 0.12), tip, width * 0.42, width * 0.32,
             spoke_key, bevel=0.004, tint=tint_spokes)
