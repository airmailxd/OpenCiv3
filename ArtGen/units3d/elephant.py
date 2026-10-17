"""The War Elephant for the Blender remaster.

    root = elephant.build('War Elephant', entry)

A sculpted Indian elephant, modelled here from primitives (no external
asset): a lofted barrel torso with the high convex back, column legs with
knees, round feet and toenails, a domed twin-lobed head, large ears, a
long trunk with ring creases, a tail with a dark tuft and long curving
tusks. The flesh parts are fused by a voxel remesh into one organic hide,
then given folds (a displacement of stretched noise, stronger on the legs
and trunk) and a wrinkled-skin shader. Civ3's War Elephant gear is built
around it:
- a civ-colored caparison with a gold key-pattern border and straps;
- a wooden plank howdah under a civ canopy with a gold key frame;
- silver round shields with gold rims, and gold-shafted spears;
- gold-capped tusks with civ bands, and a silver forehead disc in a gold ring.
Model space: facing +Y, ground at z = 0, the back about 3 m high.
"""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector

import mounted
import props
import vkit
from props import R, T

props.DEFAULTS['hide_skin'] = ('#8c7a60', 0.85, 0.0, None)   # no leather grain: it read as felt
props.DEFAULTS.setdefault('ivory', ('#e6dcc4', 0.35, 0.0, None))
props.DEFAULTS.setdefault('howdah', ('#4e3420', 0.62, 0.0, 'wood'))
props.DEFAULTS.setdefault('nail', ('#b8ae98', 0.5, 0.0, None))
props.DEFAULTS.setdefault('eye', ('#141010', 0.2, 0.0, 'polished'))
props.DEFAULTS.setdefault('tuft', ('#2a2420', 0.9, 0.0, None))
props.DEFAULTS.setdefault('silver', ('#d8dade', 0.25, 1.0, 'polished'))
props.DEFAULTS.setdefault('strap', ('#3a2618', 0.7, 0.0, 'leather'))

# the anatomy, meters (y forward)
FRONT_Y, REAR_Y = 0.85, -1.2          # leg columns
HEAD = Vector((0.0, 1.85, 2.55))      # head center
DISC = Vector((0.0, 2.5, 2.78))      # forehead disc


def _loft(stations, seg=28):
    rings = []
    for y, z, hw, hh in stations:
        ring = []
        for k in range(seg):
            a = 2 * math.pi * k / seg
            c, s = math.cos(a), math.sin(a)
            ring.append((hw * math.copysign(abs(c) ** 0.85, c), y, z + hh * math.copysign(abs(s) ** 0.9, s)))
        rings.append(ring)
    return props.loft(rings)


def _resample(path, radii, n):
    """A polyline and its radii, linearly resampled into n points."""
    pts = [Vector(p) for p in path]
    segs = [(pts[i + 1] - pts[i]).length for i in range(len(pts) - 1)]
    total = sum(segs)
    out_p, out_r = [], []
    for j in range(n):
        d = total * j / (n - 1)
        i = 0
        while i < len(segs) - 1 and d > segs[i]:
            d -= segs[i]
            i += 1
        f = d / segs[i] if segs[i] else 0
        out_p.append(pts[i].lerp(pts[i + 1], min(1.0, f)))
        out_r.append(radii[i] + (radii[i + 1] - radii[i]) * min(1.0, f))
    return out_p, out_r


def _flesh(p):
    """The fused body parts (before the remesh)."""
    # the barrel: rump to the neck, the back highest over the shoulders and hips (Asian), a deep belly
    G, H = 1.22, 1.08   # girth: broad and massive, wider than the old asset
    p.add(_loft([(y, z, hw * G, hh * H) for y, z, hw, hh in
                 [(-1.85, 2.2, 0.2, 0.3), (-1.72, 2.25, 0.55, 0.62), (-1.35, 2.32, 0.74, 0.76), (-0.8, 2.26, 0.8, 0.82),
                  (-0.1, 2.22, 0.82, 0.86), (0.55, 2.3, 0.78, 0.8), (1.05, 2.42, 0.7, 0.72), (1.35, 2.5, 0.6, 0.66),
                  (1.6, 2.55, 0.5, 0.58)]]), 'hide_skin', smooth=True)
    # the head: a broad skull with the twin domes, the cheeks, the trunk root
    p.add(props.ellipsoid(0.66, 0.66, 0.76, seg=28), 'hide_skin', T(*HEAD), smooth=True)   # a huge head
    for sx in (-1, 1):
        p.add(props.ellipsoid(0.32, 0.36, 0.3, seg=16), 'hide_skin', T(sx * 0.2, 1.85, 3.18), smooth=True)
        p.add(props.ellipsoid(0.3, 0.3, 0.36, seg=16), 'hide_skin', T(sx * 0.36, 2.15, 2.3), smooth=True)
    # the trunk: long and hanging to the ground, flaring at the root, ring creases (radius modulated)
    path, radii = _resample([(0, 2.35, 2.45), (0, 2.55, 2.0), (0, 2.55, 1.3), (0, 2.48, 0.65), (0, 2.57, 0.2), (0, 2.72, 0.08)],
                            [0.36, 0.27, 0.19, 0.14, 0.1, 0.09], 90)
    radii = [r * (1.0 - 0.07 * (math.sin(i * 1.9) ** 2)) for i, r in enumerate(radii)]
    p.add(props.tube([tuple(v) for v in path], radii, seg=16), 'hide_skin', smooth=True)
    # the legs: columns with a knee and a flared round foot
    for y, front in ((FRONT_Y, True), (REAR_Y, False)):
        for sx in (-1, 1):
            x = sx * 0.58
            if front:
                pts, rr = [(x, y, 2.0), (x, y + 0.03, 1.2), (x, y, 0.55), (x, y + 0.02, 0.12)], [0.46, 0.35, 0.32, 0.37]
            else:   # the hind leg's knee a little forward
                pts, rr = [(x, y, 2.05), (x, y + 0.08, 1.25), (x, y - 0.03, 0.55), (x, y, 0.12)], [0.5, 0.36, 0.32, 0.37]
            path, radii = _resample(pts, rr, 40)
            p.add(props.tube([tuple(v) for v in path], radii, seg=18), 'hide_skin', smooth=True)
            p.add(props.lathe([(0.0, 0.39), (0.12, 0.38), (0.17, 0.31)], seg=20), 'hide_skin', T(x, y + 0.02, 0.0), smooth=True)
    # the ears: big rounded flaps lying back along the neck (thick enough to survive the remesh)
    outline = [(0.0, 0.25), (-0.25, 0.32), (-0.55, 0.22), (-0.72, -0.05), (-0.68, -0.38), (-0.5, -0.62),
               (-0.25, -0.78), (-0.08, -0.62), (0.0, -0.3)]
    for sx in (-1, 1):
        ear = props.slab([(u * 1.45, v * 1.45) for u, v in outline], 0.1, 'X')
        for v in ear.verts:   # cup the ear: its rim curls outward
            r = (v.co.y ** 2 + v.co.z ** 2) ** 0.5
            v.co.x += 0.14 * r * r
        if sx < 0:
            for v in ear.verts:
                v.co.x = -v.co.x
            bmesh.ops.reverse_faces(ear, faces=ear.faces)
        p.add(ear, 'hide_skin', T(sx * 0.62, 1.7, 2.8) @ R('Z', sx * 22), smooth=True)
    # the tail
    p.add(props.tube([(0, -1.82, 2.45), (0, -1.95, 2.0), (0, -1.94, 1.35)], [0.07, 0.045, 0.03], seg=10), 'hide_skin', smooth=True)


def _remesh(obj, voxel=0.028):
    """Fuse the parts into one hide: a voxel remesh, smoothing, then folds."""
    rm = obj.modifiers.new('remesh', 'REMESH')
    rm.mode = 'VOXEL'
    rm.voxel_size = voxel
    rm.adaptivity = 0.0
    sm = obj.modifiers.new('smooth', 'SMOOTH')
    sm.factor, sm.iterations = 0.6, 6
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(obj.evaluated_get(dg))
    obj.modifiers.clear()
    old = obj.data
    obj.data = me
    bpy.data.meshes.remove(old)
    # folds: stretched noise displacement, horizontal creases strongest on the legs and trunk
    import random
    from mathutils import noise
    for v in me.vertices:
        co = v.co
        d = noise.noise(Vector((co.x * 0.9, co.y * 0.9, co.z * 0.9)) + Vector((3.1, 7.2, 1.3))) * 0.05   # big folds
        n = noise.noise(Vector((co.x * 2.0, co.y * 2.0, co.z * 2.0)))
        if co.z < 1.75 and co.y < 2.2:   # the legs: horizontal ring creases
            d += 0.016 * math.sin(co.z * (17.0 + 6.0 * n) + n * 6.0) * (0.6 + 0.4 * n)   # irregular creases
        if co.y > 2.25 and co.z < 2.1:   # the trunk
            d += 0.012 * math.sin(co.z * 30.0 + n * 3.0)
        if 1.15 < co.y < 1.75 and co.z > 1.8:   # the neck rolls
            d += 0.03 * math.sin(co.y * 16.0 + n)
        v.co = co + v.normal * d
    me.update()
    for poly in me.polygons:
        poly.use_smooth = True
    return obj


def _hide_material(color):
    """Elephant hide: deep wrinkle creases (voronoi edges and stretched waves
    into the bump, darkening the creases) over a warm tan-grey."""
    m = props.material('hide_skin', {'hide_skin': color}, weather={'grime': 0.4})
    p = props._principled(m)
    nt = m.node_tree
    N, L = nt.nodes, nt.links
    tc = N.new('ShaderNodeTexCoord')
    mp = N.new('ShaderNodeMapping')
    mp.inputs['Scale'].default_value = (1.0, 1.0, 2.2)   # creases mostly horizontal
    L.new(tc.outputs['Object'], mp.inputs['Vector'])
    vor = N.new('ShaderNodeTexVoronoi')
    vor.feature = 'DISTANCE_TO_EDGE'
    vor.inputs['Scale'].default_value = 9.0
    L.new(mp.outputs['Vector'], vor.inputs['Vector'])
    wav = N.new('ShaderNodeTexWave')
    wav.bands_direction = 'Z'
    wav.inputs['Scale'].default_value = 14.0
    wav.inputs['Distortion'].default_value = 2.5
    L.new(mp.outputs['Vector'], wav.inputs['Vector'])
    mx = N.new('ShaderNodeMath')
    mx.operation = 'MINIMUM'
    L.new(wav.outputs['Fac'], mx.inputs[0])   # broad wave creases only (voronoi cells read as sponge)
    L.new(wav.outputs['Fac'], mx.inputs[1])
    bump = N.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 0.6
    bump.inputs['Distance'].default_value = 0.025
    L.new(mx.outputs['Value'], bump.inputs['Height'])
    L.new(bump.outputs['Normal'], p.inputs['Normal'])
    return m


def _tusks(p, colors):
    """The tusks, toenails, eyes and tail tuft (not fused): returns the tusk axes."""
    axes = []
    for sx in (-1, 1):
        path, radii = _resample([(sx * 0.27, 2.25, 2.15), (sx * 0.32, 2.62, 1.75), (sx * 0.35, 2.9, 1.36), (sx * 0.36, 3.02, 1.06)],
                                [0.1, 0.085, 0.06, 0.02], 24)
        p.add(props.tube([tuple(v) for v in path], radii, seg=14), 'ivory', smooth=True)
        axes.append(path)
        p.add(props.ellipsoid(0.04, 0.03, 0.03, seg=10), 'eye', T(sx * 0.52, 2.22, 2.72), smooth=True)
    for y in (FRONT_Y, REAR_Y):
        for sx in (-1, 1):
            for k in range(4 if y == FRONT_Y else 3):
                a = math.radians(-50 + 100 * k / (3 if y == FRONT_Y else 2))
                p.add(props.ellipsoid(0.07, 0.05, 0.06, seg=10), 'nail',
                      T(sx * 0.58 + 0.38 * math.sin(a), y + 0.02 + 0.38 * math.cos(a), 0.06), smooth=True)
    p.add(props.ellipsoid(0.06, 0.06, 0.16, seg=10), 'tuft', T(0, -1.94, 1.25), smooth=True)
    return axes


def _meander_unit(p, place, s):
    """One Greek-key hook (a square spiral) in gold strips: place(u, v) maps
    the unit's local (along, up) meters onto the surface; s is the step."""
    pts = [(0, 0), (0, 3), (3, 3), (3, 1), (1.5, 1), (1.5, 2)]
    q = [place(u * s, v * s) for u, v in pts]
    p.add(props.tube([tuple(v) for v in q], s * 0.28, seg=5), 'gold', smooth=True)


def war_gear(root, body, tusk_axes, colors):
    """Civ3's gear around the sculpted elephant."""
    c = dict(colors)
    p = vkit.part(root, 'gear', c, {'tint': {'grime': 0.35}, 'howdah': {'grime': 0.55, 'soot': 0.2}})
    vs = [body.matrix_world @ v.co for v in body.data.vertices]
    body_vs = [v for v in vs if v.z > 1.4 and -1.6 < v.y < 1.2]
    top = max(v.z for v in body_vs)
    ymid = -0.15
    # the caparison over the back and down the flanks; gold border with a key pattern of studs
    cap, rings = mounted.drape(vs, ymid - 0.95, ymid + 0.85, 1.4, gap=0.05, folds=0.03, zmin=1.5, flare=0.08)
    p.add(cap, 'tint', smooth=True, tint=True)
    for edge in ([r[0] for r in rings], [r[-1] for r in rings], rings[0], rings[-1]):
        p.add(props.tube(edge, 0.06, seg=6, scale_xy=(1.0, 0.4)), 'gold', smooth=True)
    for edge in ([r[3] for r in rings], [r[-4] for r in rings]):   # the inner gold line of the border
        p.add(props.tube([tuple(Vector(v) + Vector((math.copysign(0.012, v[0]), 0, 0))) for v in edge], 0.025, seg=5, scale_xy=(1.0, 0.4)), 'gold', smooth=True)
    for side in (0, -1):   # the key pattern: gold tabs in a step rhythm a little inside the hem
        hem = [r[side] for r in rings]
        for j in range(0, len(hem) - 1):
            a, b = Vector(hem[j]), Vector(hem[j + 1])
            m = a.lerp(b, 0.5) + Vector((0, 0, 0.13))
            out = Vector((m.x, 0, 0)).normalized() * 0.012
            p.add(props.box(0.012, (b - a).length * 0.42, 0.1 if j % 2 else 0.05), 'gold', T(*(m + out)))
    # the Greek-key meander on each flank, in a band above the hem, projected onto the cloth
    cap_vs = [Vector(v) for r in rings for v in r]
    for sx in (-1, 1):
        side = [v for v in cap_vs if v.x * sx > 0.3]
        if not side:
            continue
        zb = min(v.z for v in side) + 0.2
        def on_cloth(y, z, sx=sx, side=side):
            near = min(side, key=lambda v: (v.y - y) ** 2 + (v.z - z) ** 2)
            return Vector((near.x + sx * 0.02, y, z))
        for y0 in [ymid - 0.85 + 0.16 * i for i in range(11)]:
            _meander_unit(p, lambda u, v, y0=y0, zb=zb, on=on_cloth: on(y0 + u, zb + v), 0.035)
        for z in (zb - 0.03, zb + 0.15):
            pts = [on_cloth(ymid - 0.88 + 0.06 * i, z) for i in range(30)]
            p.add(props.tube([tuple(q) for q in pts], 0.014, seg=5), 'gold', smooth=True)
    # girth and breast straps
    for y in (ymid - 0.2, ymid + 0.95):
        ring = [(math.sin(a) * 0.84, y, 2.22 + math.cos(a) * 0.86) for a in [math.pi * (i / 16.0 - 0.5) * 2 for i in range(17)]]
        p.add(props.tube(ring, 0.04, seg=6, scale_xy=(1.0, 0.35), caps=False), 'strap', smooth=True)
    # the howdah: a plank tower with corner posts, the civ canopy with a gold key frame
    hz, hy = top + 0.06, ymid - 0.05
    p.add(props.box(1.05, 1.2, 0.12), 'howdah', T(0, hy, hz + 0.06), bevel=0.02)
    for sx in (-1, 1):
        for k in range(3):
            p.add(props.box(0.07, 1.18, 0.11), 'howdah', T(sx * 0.5, hy, hz + 0.2 + 0.12 * k), bevel=0.012)
    for sy in (-1, 1):
        for k in range(3):
            p.add(props.box(1.05, 0.07, 0.11), 'howdah', T(0, hy + sy * 0.57, hz + 0.2 + 0.12 * k), bevel=0.012)
    for sx in (-1, 1):
        for sy in (-1, 1):
            vkit.rod(p, (sx * 0.5, hy + sy * 0.57, hz + 0.1), (sx * 0.5, hy + sy * 0.57, hz + 0.86), 0.04, 'howdah')
    top_rail = hz + 0.5
    for sx in (-1, 1):   # a railing of turned balusters along the sides, a rail on top
        p.add(props.box(0.06, 1.18, 0.05), 'howdah', T(sx * 0.5, hy, top_rail + 0.2), bevel=0.01)
        for k in range(7):
            vkit.rod(p, (sx * 0.5, hy - 0.5 + k / 6.0, top_rail), (sx * 0.5, hy - 0.5 + k / 6.0, top_rail + 0.2), 0.018, 'howdah', seg=6)
    for sy in (-1, 1):
        p.add(props.box(1.05, 0.06, 0.05), 'howdah', T(0, hy + sy * 0.57, top_rail + 0.2), bevel=0.01)
        for k in range(6):
            vkit.rod(p, (-0.45 + 0.9 * k / 5.0, hy + sy * 0.57, top_rail), (-0.45 + 0.9 * k / 5.0, hy + sy * 0.57, top_rail + 0.2), 0.018, 'howdah', seg=6)
    for sx in (-1, 1):   # gold finials on the corner posts
        for sy in (-1, 1):
            p.add(props.ellipsoid(0.05, 0.05, 0.06, seg=10), 'gold', T(sx * 0.5, hy + sy * 0.57, hz + 0.78), smooth=True)
    rz = hz + 0.9   # the canopy, low over the howdah
    for sx in (-1, 1):   # a gold fringe of tassels hanging from the canopy's edges
        for k in range(12):
            y = hy - 0.72 + 1.44 * k / 11.0
            p.add(props.lathe([(0.0, 0.025), (0.09, 0.012)], seg=6), 'gold', T(sx * 0.71, y, rz - 0.15), smooth=True)
    for sy in (-1, 1):
        for k in range(11):
            x = -0.65 + 1.3 * k / 10.0
            p.add(props.lathe([(0.0, 0.025), (0.09, 0.012)], seg=6), 'gold', T(x, hy + sy * 0.78, rz - 0.15), smooth=True)
    p.add(props.box(1.4, 1.55, 0.1), 'tint', T(0, hy, rz), bevel=0.03, tint=True)
    p.add(props.box(1.44, 1.59, 0.045), 'gold', T(0, hy, rz - 0.07), bevel=0.01)
    for sx in (-1, 1):   # the gold frame and meander on the roof
        p.add(props.box(0.04, 1.35, 0.02), 'gold', T(sx * 0.6, hy, rz + 0.06))
    for sy in (-1, 1):
        p.add(props.box(1.24, 0.04, 0.02), 'gold', T(0, hy + sy * 0.67, rz + 0.06))
    for sy in (-1, 1):
        for y0 in [hy - 0.55 + 0.16 * i for i in range(8)]:
            _meander_unit(p, lambda u, v, y0=y0, sy=sy: Vector((sy * (0.5 - v), y0 + u, rz + 0.06)), 0.035)
    # silver shields with gold rims, and gold-shafted spears forward
    for sx in (-1, 1):
        sh = props.lathe([(0.0, 0.44), (0.04, 0.42), (0.1, 0.28), (0.13, 0.12), (0.2, 0.0)], seg=32)   # a big domed shield, its boss
        p.add(sh, 'silver', T(sx * 0.6, hy, hz + 0.45) @ R('Y', sx * 90), smooth=True)
        p.add(props.lathe([(0.0, 0.47), (0.05, 0.46)], seg=32), 'gold', T(sx * 0.59, hy, hz + 0.45) @ R('Y', sx * 90), smooth=True)
        for k, dz in enumerate((0.55, 0.8)):
            a = (sx * 0.45, hy - 0.5, hz + dz)
            e = (sx * (0.75 + 0.15 * k), hy + 1.55, hz + dz + 0.35 - 0.2 * k)
            vkit.rod(p, a, e, 0.03, 'gold', seg=8)
            p.add(props.lathe([(0.0, 0.05), (0.24, 0.0)], seg=4), 'steel',
                  Matrix.Translation(e) @ (Vector(e) - Vector(a)).normalized().to_track_quat('Z', 'Y').to_matrix().to_4x4())
    # the forehead disc: silver in a gold ring
    m = T(*DISC) @ R('X', -72)
    p.add(props.lathe([(0.0, 0.2), (0.03, 0.19), (0.05, 0.0)], seg=28), 'gold', m, smooth=True)
    p.add(props.lathe([(0.04, 0.14), (0.07, 0.05), (0.08, 0.0)], seg=28), 'silver', m, smooth=True)
    # gold caps (long, over the lower half) and civ bands on the tusks
    for path in tusk_axes:
        n = len(path)
        cap = [tuple(path[i]) for i in range(n // 2, n)]
        rr = [0.085 * (1 - (i - n // 2) / (n - n // 2)) + 0.012 for i in range(n // 2, n)]
        p.add(props.tube(cap, rr, seg=14), 'gold', smooth=True)
        b = path[n // 5]
        d = (path[n // 5 + 1] - b).normalized()
        p.add(props.lathe([(0.0, 0.11), (0.1, 0.1)], seg=14), 'tint',
              Matrix.Translation(b) @ d.to_track_quat('Z', 'Y').to_matrix().to_4x4(), smooth=True, tint=True)
    p.finish()


ELEPHANT_COLORS = {'skin': '#8c7a60', 'gold': '#c8a458', 'wood': '#5a3e28'}


def build(unit, entry):
    e = dict(entry.get('elephant', {}))
    c = dict(ELEPHANT_COLORS)
    for k in ('gold', 'wood'):   # (not the catalog's grey skin: the original reads warm tan)
        if e.get(k):
            c[k] = e[k]
    root = vkit.root(unit)
    fp = vkit.part(root, 'hide', c)
    _flesh(fp)
    body = fp.finish()
    _remesh(body)
    body.data.materials.clear()
    body.data.materials.append(_hide_material(c.get('skin', '#9a8a72')))
    tp = vkit.part(root, 'ivory', c)
    axes = _tusks(tp, c)
    tp.finish()
    war_gear(root, body, axes, c)
    return root


UNITS = ['War Elephant']
