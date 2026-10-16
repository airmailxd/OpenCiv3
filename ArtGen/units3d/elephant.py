"""The War Elephant for the Blender remaster.

    root = elephant.build(entry)

The elephant is syncopika's low-poly elephant (CC0,
https://opengameart.org/node/92420), downloaded once into _cache/
(gitignored) and checked by SHA-256, then subdivided and smoothed into an
organic body with a procedural wrinkled hide. Civ3's War Elephant gear is
built around it: a civ-colored caparison with a gold border, a wooden
howdah with a civ-colored canopy roof, bronze shields and spears on its
sides, gold-capped tusks with civ-colored bands and a gold forehead disc.
Model space: facing +Y, ground at z = 0.
"""
import hashlib
import math
import os
import urllib.request

import bpy
from mathutils import Matrix, Vector

import mounted
import props
import vkit
from props import R, T

URL = 'https://opengameart.org/sites/default/files/elephant-lowpoly.blend'
SHA256 = 'cb916973655dcd16905681c7a7df87d80e964103a02cbe585747f816bb0fd4f1'
HEIGHT = 3.0          # ground to the top of the back, meters (an Indian war elephant)

props.DEFAULTS.setdefault('hide_skin', ('#8a7e6c', 0.75, 0.0, 'leather'))
props.DEFAULTS.setdefault('ivory', ('#e6dcc4', 0.35, 0.0, None))
props.DEFAULTS.setdefault('howdah', ('#4e3420', 0.62, 0.0, 'wood'))


def _blend():
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), '_cache', 'elephant-lowpoly.blend')
    if not os.path.exists(path):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        urllib.request.urlretrieve(URL, path + '.part')
        os.replace(path + '.part', path)
    h = hashlib.sha256(open(path, 'rb').read()).hexdigest()
    if h != SHA256:
        raise RuntimeError(f'{path}: unexpected SHA-256 {h}')
    return path


def _hide_material(color):
    """Elephant hide: the color with deep wrinkle creases (voronoi edges and
    stretched noise into the bump and darkening the creases)."""
    m = props.material('hide_skin', {'hide_skin': color}, weather={'grime': 0.5})
    p = props._principled(m)
    nt = m.node_tree
    N, L = nt.nodes, nt.links
    tc = N.new('ShaderNodeTexCoord')
    vor = N.new('ShaderNodeTexVoronoi')
    vor.feature = 'DISTANCE_TO_EDGE'
    vor.inputs['Scale'].default_value = 26.0
    L.new(tc.outputs['Object'], vor.inputs['Vector'])
    wav = N.new('ShaderNodeTexWave')
    wav.inputs['Scale'].default_value = 40.0
    wav.inputs['Distortion'].default_value = 8.0
    L.new(tc.outputs['Object'], wav.inputs['Vector'])
    mx = N.new('ShaderNodeMath')
    mx.operation = 'MINIMUM'
    L.new(vor.outputs['Distance'], mx.inputs[0])
    L.new(wav.outputs['Fac'], mx.inputs[1])
    bump = N.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 0.55
    bump.inputs['Distance'].default_value = 0.02
    L.new(mx.outputs['Value'], bump.inputs['Height'])
    L.new(bump.outputs['Normal'], p.inputs['Normal'])
    return m


def _bulk(body, width=1.55, leg=1.9):
    """The low-poly asset is narrow with stick legs: widen the body and
    thicken each leg around its own center (in the mesh's own space; legs are
    the vertices in the lower 45% of the height, grouped by quadrant)."""
    me = body.data
    zs = [v.co.z for v in me.vertices]
    z0, z1 = min(zs), max(zs)
    xs = [v.co.x for v in me.vertices]
    xc = sum(xs) / len(xs)
    ycs = [v.co.y for v in me.vertices]
    yc = sum(ycs) / len(ycs)
    cut = z0 + 0.45 * (z1 - z0)
    groups = {}
    for v in me.vertices:
        if v.co.z < cut:
            groups.setdefault((v.co.x > xc, v.co.y > yc), []).append(v)
    centers = {k: (sum(v.co.x for v in g) / len(g), sum(v.co.y for v in g) / len(g)) for k, g in groups.items()}
    for v in me.vertices:
        if v.co.z < cut:
            cx, cy = centers[(v.co.x > xc, v.co.y > yc)]
            blend = min(1.0, (cut - v.co.z) / (0.15 * (z1 - z0)))
            k = 1.0 + (leg - 1.0) * blend
            v.co.x = xc + (cx - xc) * width + (v.co.x - cx) * k
            v.co.y = cy + (v.co.y - cy) * k
        else:
            v.co.x = xc + (v.co.x - xc) * width
    me.update()


def elephant_asset(root, colors):
    """Append the CC0 elephant under `root`: facing +Y, standing on z = 0,
    HEIGHT tall at the back, subdivided and smoothed; returns the body."""
    with bpy.data.libraries.load(_blend(), link=False) as (src, dst):
        dst.objects = [n for n in ('Plane', 'Armature') if n in src.objects]
    objs = [o for o in dst.objects if o is not None]
    holder = bpy.data.objects.new('elephant_asset', None)
    bpy.context.collection.objects.link(holder)
    holder.parent = root
    for o in objs:
        bpy.context.collection.objects.link(o)
        if o.parent is None:
            o.parent = holder
    body = next(o for o in objs if o.type == 'MESH')
    arm = next(o for o in objs if o.type == 'ARMATURE')
    arm.data.pose_position = 'REST'
    if arm.animation_data:
        arm.animation_data.action = None
    # which material slots are the tusks: the lightest diffuse color
    lum = [sum(m.diffuse_color[:3]) if m else 0 for m in body.data.materials]
    tusk_slot = lum.index(max(lum)) if lum else -1
    bpy.context.view_layer.update()
    vs = [body.matrix_world @ v.co for v in body.data.vertices]
    tusk_vs = [body.matrix_world @ body.data.vertices[i].co
               for poly in body.data.polygons if poly.material_index == tusk_slot for i in poly.vertices]
    zmin, zmax = min(v.z for v in vs), max(v.z for v in vs)
    yc = sum(v.y for v in vs) / len(vs)
    head_sign = 1.0 if tusk_vs and sum(v.y for v in tusk_vs) / len(tusk_vs) > yc else -1.0
    s = HEIGHT / (zmax - zmin)
    xc = sum(v.x for v in vs) / len(vs)
    holder.matrix_parent_inverse.identity()
    holder.rotation_euler = (0, 0, 0.0 if head_sign > 0 else math.pi)
    holder.scale = (s, s, s)
    holder.location = (-xc * s * head_sign, -yc * s * head_sign, -zmin * s)
    _bulk(body)
    sub = body.modifiers.new('subsurf', 'SUBSURF')
    sub.levels = sub.render_levels = 2
    for poly in body.data.polygons:
        poly.use_smooth = True
    hide = _hide_material(colors.get('skin', '#8a7e6c'))
    ivory = props.material('ivory', colors)
    body.data.materials.clear()
    body.data.materials.append(hide)
    body.data.materials.append(ivory)
    for poly in body.data.polygons:
        poly.material_index = 1 if poly.material_index == tusk_slot else 0
    bpy.context.view_layer.update()
    return body


def _eval_verts(obj):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(dg)
    return [ev.matrix_world @ v.co for v in ev.data.vertices]


def war_gear(root, body, colors):
    """Civ3's gear: caparison, howdah with canopy, shields and spears, tusk caps, forehead disc."""
    c = dict(colors)
    p = vkit.part(root, 'gear', c, {'tint': {'grime': 0.45, 'soot': 0.15}, 'howdah': {'grime': 0.6, 'soot': 0.3}})
    vs = _eval_verts(body)
    ys = [v.y for v in vs]
    yb, yf = min(ys), max(ys)
    body_vs = [v for v in vs if v.z > 1.2]
    top = max(v.z for v in body_vs)
    ymid = sum(v.y for v in body_vs) / len(body_vs)
    # the caparison: over the back, down the flanks to below the belly line
    cap, rings = mounted.drape(vs, ymid - 0.9, ymid + 0.75, 1.15, gap=0.05, folds=0.03, zmin=1.3, flare=0.08)
    p.add(cap, 'tint', smooth=True, tint=True)
    for edge in ([r[0] for r in rings], [r[-1] for r in rings], rings[0], rings[-1]):
        p.add(props.tube(edge, 0.05, seg=6, scale_xy=(1.0, 0.4)), 'gold', smooth=True)
    # the howdah: a wooden box tower on the back with a civ-colored canopy roof
    hz = top + 0.05
    hy = ymid - 0.05
    p.add(props.box(1.0, 1.15, 0.12), 'howdah', T(0, hy, hz + 0.06), bevel=0.02)
    for sx in (-1, 1):
        p.add(props.box(0.08, 1.15, 0.65), 'howdah', T(sx * 0.48, hy, hz + 0.42), bevel=0.015)
    for sy in (-1, 1):
        p.add(props.box(1.0, 0.08, 0.65), 'howdah', T(0, hy + sy * 0.55, hz + 0.42), bevel=0.015)
    for sx in (-1, 1):
        for sy in (-1, 1):
            vkit.rod(p, (sx * 0.46, hy + sy * 0.53, hz + 0.7), (sx * 0.46, hy + sy * 0.53, hz + 1.25), 0.03, 'howdah')
    roof = props.box(1.25, 1.35, 0.1)
    p.add(roof, 'tint', T(0, hy, hz + 1.3), bevel=0.03, tint=True)
    p.add(props.box(1.3, 1.4, 0.04), 'gold', T(0, hy, hz + 1.24), bevel=0.01)
    # bronze shields and spears on the howdah's sides
    for sx in (-1, 1):
        sh = props.lathe([(0.0, 0.22), (0.03, 0.2), (0.06, 0.1), (0.075, 0.0)], seg=24)
        p.add(sh, 'bronze', T(sx * 0.55, hy, hz + 0.45) @ R('Y', sx * 90), smooth=True)
        for k, dz in enumerate((0.35, 0.55, 0.75)):
            a = (sx * 0.4, hy - 0.7 + 0.1 * k, hz + dz)
            e = (sx * 0.75, hy + 0.9 + 0.1 * k, hz + dz + 0.25)
            vkit.rod(p, a, e, 0.016, 'wood', seg=6)
            p.add(props.lathe([(0.0, 0.02), (0.12, 0.0)], seg=4), 'steel',
                  Matrix.Translation(e) @ (Vector(e) - Vector(a)).normalized().to_track_quat('Z', 'Y').to_matrix().to_4x4())
    # gold forehead disc
    head_vs = [v for v in vs if v.y > yf - 1.0 and v.z > 1.5]
    if head_vs:
        hf = max(head_vs, key=lambda v: v.y)
        disc = props.lathe([(0.0, 0.16), (0.03, 0.15), (0.05, 0.06), (0.06, 0.0)], seg=24)
        p.add(disc, 'gold', T(0, hf.y + 0.02, hf.z - 0.25) @ R('X', -70), smooth=True)
    # gold caps and civ bands on the tusks: the tusk tips are the ivory vertices furthest forward
    ev = body.evaluated_get(bpy.context.evaluated_depsgraph_get())
    tusk = [ev.matrix_world @ ev.data.vertices[i].co for poly in ev.data.polygons if poly.material_index == 1
            for i in poly.vertices]
    for sx in (-1, 1):
        side = [v for v in tusk if v.x * sx > 0]
        if not side:
            continue
        tip = max(side, key=lambda v: v.y)
        base = min(side, key=lambda v: v.y)
        d = (tip - base).normalized()
        rot = d.to_track_quat('Z', 'Y').to_matrix().to_4x4()
        p.add(props.lathe([(0.0, 0.07), (0.25, 0.05), (0.33, 0.0)], seg=12), 'gold',
              Matrix.Translation(tip - d * 0.3) @ rot, smooth=True)
        mid = base.lerp(tip, 0.55)
        p.add(props.lathe([(0.0, 0.085), (0.08, 0.08)], seg=12), 'tint', Matrix.Translation(mid) @ rot, smooth=True,
              tint=True)
    p.finish()


ELEPHANT_COLORS = {'skin': '#8e8270', 'gold': '#c4a462', 'wood': '#5a3e28'}


def build(unit, entry):
    e = dict(entry.get('elephant', {}))
    c = dict(ELEPHANT_COLORS)
    for k in ('skin', 'gold', 'wood'):
        if e.get(k):
            c[k] = e[k]
    root = vkit.root(unit)
    body = elephant_asset(root, c)
    war_gear(root, body, c)
    return root


UNITS = ['War Elephant']
