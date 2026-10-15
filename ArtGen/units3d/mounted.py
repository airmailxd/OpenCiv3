"""Mounted units for the Blender remaster: the horse (and later its rider seat).

    root = mounted.build('Horseman', colors)

The horse is procedural: a skeleton of joints (spine, neck, head, four legs,
tail root) skinned with Blender's Skin modifier into one organic body, then
subdivided and smoothed, with muscle masses (chest, shoulders, haunches,
jowl) added as extra skeleton branches. Model space: meters, facing +Y,
ground z = 0, about 1.5 m at the withers. A 'rider' empty sits at the saddle
(the Mac session seats a human there). Coat colors come from the catalog's
'horse' entry: coat, mane, points (legs), muzzle; the saddle cloth and the
bridle are civ-colored as in Civ3.
"""
import math

import bmesh
import bpy
from mathutils import Vector

import props
import vkit
from props import T

props.DEFAULTS.setdefault('coat', ('#7a5032', 0.55, 0.0, 'leather'))
props.DEFAULTS.setdefault('points', ('#2e2218', 0.6, 0.0, 'leather'))
props.DEFAULTS.setdefault('mane', ('#1e1610', 0.75, 0.0, 'rope'))
props.DEFAULTS.setdefault('muzzle', ('#3a2a20', 0.5, 0.0, 'leather'))
props.DEFAULTS.setdefault('hoof', ('#24201c', 0.5, 0.0, 'stone'))

# Skeleton: name -> (position, radius across, radius up). Positions in meters.
JOINTS = {
    # spine, tail to head: a deep barrel and a thick crested neck (a war horse, not a racer)
    'tail': ((0, -0.98, 1.36), 0.08, 0.08),
    'croup': ((0, -0.8, 1.44), 0.33, 0.3),
    'hip': ((0, -0.56, 1.4), 0.37, 0.38),
    'loin': ((0, -0.3, 1.33), 0.36, 0.42),
    'back': ((0, -0.02, 1.3), 0.38, 0.45),
    'girth': ((0, 0.24, 1.3), 0.38, 0.47),
    'withers': ((0, 0.42, 1.4), 0.32, 0.42),
    'neck0': ((0, 0.6, 1.5), 0.27, 0.36),
    'neck1': ((0, 0.76, 1.68), 0.19, 0.27),
    'poll': ((0, 0.88, 1.84), 0.13, 0.15),
    'brow': ((0, 1.0, 1.73), 0.14, 0.15),
    'nose': ((0, 1.15, 1.52), 0.1, 0.11),
    'muzzle': ((0, 1.21, 1.42), 0.095, 0.1),
    # muscle masses (branches off the spine)
    'chest': ((0, 0.52, 1.15), 0.28, 0.24),
    'jowl': ((0, 0.9, 1.7), 0.12, 0.13),
}
LEGS = {
    # (y, z, r_across, r_up) from the body down to the hoof
    'fore': [(0.42, 1.12, 0.15, 0.19), (0.4, 0.92, 0.1, 0.12), (0.42, 0.55, 0.065, 0.07), (0.43, 0.36, 0.05, 0.055),
             (0.44, 0.16, 0.055, 0.06), (0.47, 0.05, 0.066, 0.055)],
    'hind': [(-0.7, 1.16, 0.21, 0.24), (-0.58, 0.9, 0.14, 0.15), (-0.82, 0.56, 0.068, 0.075), (-0.79, 0.36, 0.05, 0.055),
             (-0.77, 0.16, 0.055, 0.06), (-0.74, 0.05, 0.066, 0.055)],
}
SPINE = ['tail', 'croup', 'hip', 'loin', 'back', 'girth', 'withers', 'neck0', 'neck1', 'poll', 'brow', 'nose', 'muzzle']


META_K = 0.575    # an isolated metaball ellipsoid's half-extent is META_K * radius * size
META_SHRINK = 0.8  # overlapping elements blend and swell, so each is drawn a bit smaller


def _meta_body(name, step=0.06):
    """The horse body as metaballs: overlapping ellipsoids sampled along the
    spine, neck and head and down each leg (sizes from JOINTS and LEGS), plus
    the muscle masses, blended into one organic surface and converted to a
    mesh. Smoother and more muscular than a skin modifier's box sections."""
    mb = bpy.data.metaballs.new(name + '_meta')
    mb.resolution = mb.render_resolution = 0.022
    mb.threshold = 0.6

    def blob(pos, a, b, c):
        """An ellipsoid with half-extents a (x), b (y), c (z)."""
        m = max(a, b, c)
        e = mb.elements.new(type='ELLIPSOID')
        e.co = pos
        e.radius = m * META_SHRINK / META_K
        e.size_x, e.size_y, e.size_z = a / m, b / m, c / m

    def chain(pts):
        """pts: [(pos, rx, rz, r_along)], sampled every `step` along the polyline."""
        for (p0, rx0, rz0, ra0), (p1, rx1, rz1, ra1) in zip(pts[:-1], pts[1:]):
            p0, p1 = Vector(p0), Vector(p1)
            n = max(1, int((p1 - p0).length / step))
            for i in range(n):
                t = i / n
                p = p0.lerp(p1, t)
                d = (p1 - p0).normalized()
                rx, rz, ra = rx0 + (rx1 - rx0) * t, rz0 + (rz1 - rz0) * t, ra0 + (ra1 - ra0) * t
                # along a mostly vertical segment the 'up' radius becomes the along one
                if abs(d.z) > 0.7:
                    blob(p, rx, rz, ra)
                else:
                    blob(p, rx, ra, rz)
        p, rx, rz, ra = pts[-1]
        blob(Vector(p), rx, ra, rz)

    spine = [(JOINTS[k][0], JOINTS[k][1], JOINTS[k][2], max(step * 1.4, 0.6 * min(JOINTS[k][1], JOINTS[k][2])))
             for k in SPINE]
    chain(spine)
    for k in ('chest', 'jowl'):
        pos, rx, rz = JOINTS[k]
        blob(Vector(pos), rx, rz * 0.9, rz)
    for leg, links in LEGS.items():
        for sx in (-1, 1):
            pts = []
            for i, (y, z, rx, rz) in enumerate(links):
                x = sx * (0.17 if leg == 'fore' else 0.19) * (1.0 if i else 0.85)
                # leg sections: rx across, rz front-to-back, along-the-leg about a step
                pts.append(((x, y, z), rx, rz, max(step * 1.2, rx)))
            chain(pts)
    for sx in (-1, 1):   # ears
        blob(Vector((sx * 0.08, 0.86, 1.99)), 0.03, 0.025, 0.07)
    obj = bpy.data.objects.new(name + '_meta', mb)
    bpy.context.collection.objects.link(obj)
    dg = bpy.context.evaluated_depsgraph_get()
    body = bpy.data.meshes.new_from_object(obj.evaluated_get(dg))
    bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.metaballs.remove(mb)
    return body


def _skin_body(name):
    """The horse body: a skeleton mesh with a Skin and a Subdivision modifier,
    evaluated into a plain mesh object."""
    bm = bmesh.new()
    radii = []
    verts = {}

    def vert(key, pos, rx, rz):
        v = bm.verts.new(pos)
        verts[key] = v
        radii.append((rx, rz))
        return v

    for k in SPINE + ['chest', 'jowl']:
        (pos, rx, rz) = JOINTS[k]
        vert(k, pos, rx, rz)
    for a, b in zip(SPINE[:-1], SPINE[1:]):
        bm.edges.new((verts[a], verts[b]))
    bm.edges.new((verts['girth'], verts['chest']))
    bm.edges.new((verts['poll'], verts['jowl']))
    for leg, chain in LEGS.items():
        attach = verts['withers' if leg == 'fore' else 'hip']
        for sx in (-1, 1):
            prev = attach
            for i, (y, z, rx, rz) in enumerate(chain):
                x = sx * (0.17 if leg == 'fore' else 0.19) * (1.0 if i else 0.85)
                v = vert(f'{leg}{sx}{i}', (x, y, z), rx, rz)
                bm.edges.new((prev, v))
                prev = v
    # ears
    for sx in (-1, 1):
        v = vert(f'ear{sx}', (sx * 0.07, 0.83, 1.98), 0.025, 0.02)
        bm.edges.new((verts['poll'], v))
    me = bpy.data.meshes.new(name + '_skel')
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name + '_skel', me)
    bpy.context.collection.objects.link(obj)
    skin = obj.modifiers.new('skin', 'SKIN')
    skin.use_smooth_shade = True
    for i, (rx, rz) in enumerate(radii):
        me.skin_vertices[0].data[i].radius = (rx, rz)
    me.skin_vertices[0].data[0].use_root = True
    sub = obj.modifiers.new('subsurf', 'SUBSURF')
    sub.levels = sub.render_levels = 3
    # the skin's box-like sections: relax them into round, muscular forms
    sm = obj.modifiers.new('smooth', 'SMOOTH')
    sm.factor = 0.9
    sm.iterations = 12
    dg = bpy.context.evaluated_depsgraph_get()
    body = bpy.data.meshes.new_from_object(obj.evaluated_get(dg))
    bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.meshes.remove(me)
    return body


# ------------------------------------------------------------ the CC0 horse asset

# The procedural horse above didn't reach a convincing anatomy (an independent
# review scored it 3/10: a smooth tube on dowel legs). This uses a sculpted,
# textured horse instead: Lyndon Daniels' horse from his "Realtime Ranchers"
# pack, rigged by ChadM, CC0 (https://opengameart.org/content/rigged-horse,
# https://opengameart.org/node/10771). It is downloaded once into _cache/
# (gitignored) and checked against its SHA-256.
HORSE_URL = 'https://opengameart.org/sites/default/files/riggedHorse.blend'
HORSE_SHA256 = '9cca670b93a74d50e89263e50d55ab035a6c46aa7d2b21e354bdac6987037f4a'
HORSE_OBJECTS = ['Plane', 'Armature', 'BezierCurve', 'BezierCurve.005', 'Sphere', 'Sphere.002']
HORSE_HEIGHT = 2.05      # ground to ear tips, meters (a heavy war horse, about 16.2 hands at the withers)


def _horse_blend():
    import hashlib
    import os
    import urllib.request
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), '_cache', 'riggedHorse.blend')
    if not os.path.exists(path):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        urllib.request.urlretrieve(HORSE_URL, path + '.part')
        os.replace(path + '.part', path)
    h = hashlib.sha256(open(path, 'rb').read()).hexdigest()
    if h != HORSE_SHA256:
        raise RuntimeError(f'{path}: unexpected SHA-256 {h}')
    return path


def _image(name):
    for im in bpy.data.images:
        if im.name.startswith(name):
            return im
    return None


def _coat_material(colors):
    """The horse's own painted texture (color, AO, normal map), turned into a
    bay: the coat's value and warmth from the catalog, legs below the knee and
    hock darkened to black points, muzzle darker, plus the shared weathering."""
    m = bpy.data.materials.new('horse_coat')
    m.use_nodes = True
    nt = m.node_tree
    N, L = nt.nodes, nt.links
    p = N['Principled BSDF']
    p.inputs['Roughness'].default_value = 0.55
    tex = N.new('ShaderNodeTexImage')
    tex.image = _image('HorseMain4k00.png')
    tex.image.colorspace_settings.name = 'sRGB'   # the file marks its color maps Non-Color, which washes them out
    ao = N.new('ShaderNodeTexImage')
    ao.image = _image('HorseMain4k00AO00')
    ao.image.colorspace_settings.name = 'Non-Color'
    nrm = N.new('ShaderNodeTexImage')
    nrm.image = _image('HorseMain4k00Norm00')
    nrm.image.colorspace_settings.name = 'Non-Color'
    nm = N.new('ShaderNodeNormalMap')
    nm.inputs['Strength'].default_value = 0.8
    L.new(nrm.outputs['Color'], nm.inputs['Color'])
    L.new(nm.outputs['Normal'], p.inputs['Normal'])
    # chestnut -> bay: a little darker and less red
    hsv = N.new('ShaderNodeHueSaturation')
    hsv.inputs['Hue'].default_value = 0.505        # a touch warmer
    hsv.inputs['Saturation'].default_value = 1.25   # keeps reading as a bay when small
    hsv.inputs['Value'].default_value = 0.95
    L.new(tex.outputs['Color'], hsv.inputs['Color'])
    mul = N.new('ShaderNodeMix')
    mul.data_type = 'RGBA'
    mul.blend_type = 'MULTIPLY'
    mul.inputs['Factor'].default_value = 0.8
    L.new(hsv.outputs['Color'], mul.inputs['A'])
    L.new(ao.outputs['Color'], mul.inputs['B'])
    # black points: legs darken toward the hooves (object-space height, set by horse_asset)
    geo = N.new('ShaderNodeNewGeometry')
    sep = N.new('ShaderNodeSeparateXYZ')
    L.new(geo.outputs['Position'], sep.inputs['Vector'])
    ramp = N.new('ShaderNodeMapRange')
    ramp.inputs['From Min'].default_value = 0.42
    ramp.inputs['From Max'].default_value = 0.62
    ramp.inputs['To Min'].default_value = 1.0
    ramp.inputs['To Max'].default_value = 0.0
    L.new(sep.outputs['Z'], ramp.inputs['Value'])
    pts = N.new('ShaderNodeMix')
    pts.data_type = 'RGBA'
    pts.blend_type = 'MIX'
    pts.inputs['B'].default_value = (0.03, 0.022, 0.016, 1.0)
    L.new(ramp.outputs['Result'], pts.inputs['Factor'])
    L.new(mul.outputs['Result'], pts.inputs['A'])
    L.new(pts.outputs['Result'], p.inputs['Base Color'])
    aov = N.new('ShaderNodeOutputAOV')
    aov.aov_name = 'tint'
    aov.inputs['Value'].default_value = 0.0
    return m


def _hair_material():
    """Mane and tail cards: the asset's hair texture with its alpha, darkened to a bay's black mane."""
    m = bpy.data.materials.new('horse_hair')
    m.use_nodes = True
    nt = m.node_tree
    N, L = nt.nodes, nt.links
    p = N['Principled BSDF']
    p.inputs['Roughness'].default_value = 0.7
    tex = N.new('ShaderNodeTexImage')
    tex.image = _image('Hair12Main2k.png')
    tex.image.colorspace_settings.name = 'sRGB'
    # a bay's black mane and tail: a near-black color carrying only the
    # texture's strand detail (its brightness) and its alpha
    bw = N.new('ShaderNodeRGBToBW')
    L.new(tex.outputs['Color'], bw.inputs['Color'])
    k = N.new('ShaderNodeMapRange')
    k.inputs['To Min'].default_value = 0.5
    k.inputs['To Max'].default_value = 1.3
    L.new(bw.outputs['Val'], k.inputs['Value'])
    tone = N.new('ShaderNodeMix')
    tone.data_type = 'RGBA'
    tone.blend_type = 'MULTIPLY'
    tone.inputs['Factor'].default_value = 1.0
    tone.inputs['A'].default_value = (0.022, 0.015, 0.01, 1.0)
    comb = N.new('ShaderNodeCombineColor')
    for ch in ('Red', 'Green', 'Blue'):
        L.new(k.outputs['Result'], comb.inputs[ch])
    L.new(comb.outputs['Color'], tone.inputs['B'])
    L.new(tone.outputs['Result'], p.inputs['Base Color'])
    L.new(tex.outputs['Alpha'], p.inputs['Alpha'])
    aov = N.new('ShaderNodeOutputAOV')
    aov.aov_name = 'tint'
    aov.inputs['Value'].default_value = 0.0
    return m


def _raise_head(objs, degrees):
    """Carry the head higher, like Civ3's horse (the asset rests grazing-low):
    turn the neck bone up. The mane and eyes aren't bound to the rig, so they
    are parented to the neck and head bones first and move with them."""
    arm = next(o for o in objs if o.type == 'ARMATURE')
    bpy.context.view_layer.update()
    for o in objs:
        bone = 'Bone.001' if o.name.startswith('BezierCurve') and o.name != 'BezierCurve.005' else \
            'Bone.002' if o.name.startswith('Sphere') else None
        if bone is None:
            continue
        mw = o.matrix_world.copy()
        o.parent = arm
        o.parent_type = 'BONE'
        o.parent_bone = bone
        bpy.context.view_layer.update()
        o.matrix_world = mw
    pb = arm.pose.bones['Bone.001']
    pb.rotation_mode = 'XYZ'
    pb.rotation_euler = (math.radians(degrees), 0.0, 0.0)
    bpy.context.view_layer.update()


def horse_asset(root, colors, neck_raise=-28.0):
    """Append the CC0 horse under `root`, facing +Y, standing on z = 0,
    HORSE_HEIGHT tall, with rebuilt materials. Returns the body object."""
    path = _horse_blend()
    with bpy.data.libraries.load(path, link=False) as (src, dst):
        dst.objects = [n for n in HORSE_OBJECTS if n in src.objects]
        # the old Blender Internal materials no longer reference the packed
        # textures, so the images are appended by name
        dst.images = [n for n in src.images if n.startswith(('HorseMain', 'Hair12'))]
    objs = [o for o in dst.objects if o is not None]
    holder = bpy.data.objects.new('horse_asset', None)
    bpy.context.collection.objects.link(holder)
    holder.parent = root
    for o in objs:
        bpy.context.collection.objects.link(o)
        if o.parent is None:
            o.parent = holder
    body = bpy.data.objects['Plane']
    bpy.context.view_layer.update()
    vs = [body.matrix_world @ v.co for v in body.data.vertices]
    zmin = min(v.z for v in vs)
    zmax = max(v.z for v in vs)
    s = HORSE_HEIGHT / (zmax - zmin)
    feet = [v for v in vs if v.z < zmin + 0.6]
    yc = (min(v.y for v in feet) + max(v.y for v in feet)) / 2
    xc = sum(v.x for v in feet) / len(feet)
    # head toward +Y: turn 180 degrees about Z, scale to size, feet centered on the origin
    holder.matrix_parent_inverse.identity()
    holder.rotation_euler = (0, 0, math.pi)
    holder.scale = (s, s, s)
    holder.location = (xc * s, yc * s, -zmin * s)
    _raise_head(objs, neck_raise)
    coat = _coat_material(colors)
    hair = _hair_material()
    eye = bpy.data.materials.new('horse_eye')
    eye.use_nodes = True
    eye.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.01, 0.008, 0.006, 1)
    eye.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = 0.15
    for o in objs:
        if o.type != 'MESH':
            continue
        o.data.materials.clear()
        o.data.materials.append(coat if o.name == 'Plane' else hair if o.name.startswith('Bezier') else eye)
        for poly in o.data.polygons:
            poly.use_smooth = True
    return body


def horse(root, colors):
    """The horse's body as a vkit part: coat with dark points (legs below the
    knee), a darker muzzle, mane and tail, hooves."""
    p = vkit.part(root, 'horse', colors, {'coat': {'grime': 0.35}, 'points': {'grime': 0.5}})
    me = _meta_body('horse')
    bm = bmesh.new()
    bm.from_mesh(me)
    bpy.data.meshes.remove(me)

    def keys(f):
        c = f.calc_center_median()
        if c.z < 0.09:
            return ('hoof', False)
        if c.z < 0.5 + 0.0 * c.y:
            return ('points', False)
        if c.y > 1.05:
            return ('muzzle', False)
        return None

    p.add(bm, 'coat', smooth=True, keys=keys)
    # mane: a ridge of strands along the crest of the neck
    for i in range(14):
        t = i / 13
        a = Vector(JOINTS['withers'][0]).lerp(Vector(JOINTS['poll'][0]), t) + Vector((0, -0.03, 0.34 - 0.2 * t))
        b = a + Vector((0.09 * (1 if i % 2 else -1), -0.08, -0.22))
        p.add(props.tube([a, a.lerp(b, 0.5) + Vector((0, 0, 0.03)), b], [0.045, 0.032, 0.008], seg=5), 'mane',
              smooth=True)
    # tail: a few heavy strands falling from the tail root
    for i in range(6):
        sx = (i - 2.5) * 0.012
        path = [(sx, -1.02, 1.36), (sx * 1.5, -1.12, 1.2), (sx * 2, -1.16, 0.9), (sx * 2.5 + 0.01 * i, -1.12, 0.62)]
        p.add(props.tube(path, [0.05, 0.045, 0.035, 0.01], seg=6), 'mane', smooth=True)
    return p.finish()


def tack(root, colors):
    """Saddle cloth and bridle (civ color, as in Civ3) and a leather saddle;
    returns the saddle seat point for the rider empty."""
    p = vkit.part(root, 'tack', colors)
    # saddle cloth draped over the back
    rings = []
    for i in range(9):
        y = -0.3 + 0.48 * i / 8
        ring = []
        for k in range(17):   # over the back and down the flanks
            a = -0.15 + (math.pi + 0.3) * k / 16
            ring.append((0.4 * math.cos(a), y, 1.3 + 0.47 * math.sin(a)))
        rings.append(ring)
    p.add(props.loft(rings, False, False), 'tint', smooth=True, tint=True)
    seat = (0.0, -0.05, 1.84)
    p.add(props.ellipsoid(0.2, 0.28, 0.07, seg=16), 'leather', T(0, -0.05, 1.79), smooth=True)
    p.add(props.ellipsoid(0.13, 0.05, 0.09, seg=12), 'leather', T(0, 0.18, 1.84), smooth=True)   # pommel
    # bridle: headstall and reins
    for sx in (-1, 1):
        p.add(props.tube([(sx * 0.11, 0.9, 1.86), (sx * 0.12, 1.04, 1.66), (sx * 0.1, 1.18, 1.46)], 0.014, seg=6),
              'tint', smooth=True, tint=True)
        p.add(props.tube([(sx * 0.1, 1.18, 1.46), (sx * 0.16, 0.7, 1.6), (sx * 0.12, 0.2, 1.8)], 0.009, seg=5),
              'leather', smooth=True)
    p.add(props.tube([(-0.12, 1.1, 1.55), (0.0, 1.14, 1.6), (0.12, 1.1, 1.55)], 0.014, seg=6), 'tint',
          smooth=True, tint=True)
    p.finish()
    return seat


HORSE_COLORS = {'coat': '#6a3e1e', 'mane': '#1a120c', 'points': '#221810', 'muzzle': '#30221a', 'leather': '#3e2a1a'}


def horseman(colors, procedural=False, with_tack=True):
    c = dict(HORSE_COLORS, **colors)
    root = vkit.root('Horseman')
    if procedural:
        horse(root, c)
        seat = tack(root, c)
    else:
        body = horse_asset(root, c)
        seat = asset_tack(root, body, c) if with_tack else (0.0, 0.0, 1.85)
    e = vkit.crew(root, 0, seat, yaw=0.0)
    e.name = 'rider'
    return root


def _world_verts(obj):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(dg)
    return [ev.matrix_world @ v.co for v in ev.data.vertices]


def _shrink_patch(name, body, xs, ys, z_top, offset, keyfn=None):
    """A grid laid over the horse (x across, y along, starting above z_top)
    and shrink-wrapped onto the posed body surface `offset` above it; returns
    a bmesh in world space."""
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    rows = []
    for y in ys:
        rows.append([bm.verts.new((x, y, z_top + 0.4 - 0.9 * x * x)) for x in xs])
    for a, b in zip(rows[:-1], rows[1:]):
        for i in range(len(xs) - 1):
            bm.faces.new((a[i], a[i + 1], b[i + 1], b[i]))
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(obj)
    sw = obj.modifiers.new('wrap', 'SHRINKWRAP')
    sw.target = body
    sw.wrap_method = 'NEAREST_SURFACEPOINT'
    sw.offset = offset
    dg = bpy.context.evaluated_depsgraph_get()
    out = bmesh.new()
    out.from_mesh(bpy.data.meshes.new_from_object(obj.evaluated_get(dg)))
    bpy.data.objects.remove(obj, do_unlink=True)
    return out


def _head_ring(verts, a, b, t, grow=0.012):
    """A loop around the head: the vertices in a thin slice across the head
    bone (from a to b, at fraction t), ordered by angle, pushed out a little."""
    a, b = Vector(a), Vector(b)
    axis = (b - a).normalized()
    c = a.lerp(b, t)
    sl = [v for v in verts if abs((v - c).dot(axis)) < 0.02 and (v - c).length < 0.35]
    if len(sl) < 8:
        return None
    ctr = sum(sl, Vector()) / len(sl)
    u = axis.cross(Vector((0, 0, 1))).normalized()
    w = axis.cross(u).normalized()
    pts = []
    for k in range(24):
        ang = 2 * math.pi * k / 24
        d = u * math.cos(ang) + w * math.sin(ang)
        r = max(((v - ctr) - axis * (v - ctr).dot(axis)).dot(d) for v in sl)
        pts.append(ctr + d * (r + grow))
    return pts + [pts[0]]


def asset_tack(root, body, colors):
    """Civ3's tack on the asset horse: a civ-colored saddle cloth draped over
    the back (shrink-wrapped to it), a leather saddle with pommel and cantle,
    a civ-colored bridle (noseband and crownpiece) and reins. Returns the seat
    point for the rider empty."""
    p = vkit.part(root, 'tack', colors, {'leather': {'grime': 0.5}})
    vs = _world_verts(body)
    feet = [v for v in vs if v.z < 0.15]
    yf = max(v.y for v in feet)                  # the front hooves
    yh = min(v.y for v in feet)                  # the hind hooves
    ys = yh + 0.6 * (yf - yh)                    # the saddle sits just behind the withers
    top = max(v.z for v in vs if abs(v.y - ys) < 0.08 and abs(v.x) < 0.1)
    xs = [-0.78 + 1.56 * i / 26 for i in range(27)]   # well down the flanks, as Civ3's cloth hangs
    cloth = _shrink_patch('cloth', body, xs, [ys - 0.4 + 0.74 * j / 14 for j in range(15)], top, 0.012)
    p.add(cloth, 'tint', smooth=True, tint=True)
    pad = _shrink_patch('saddle', body, [-0.24 + 0.48 * i / 12 for i in range(13)],
                        [ys - 0.22 + 0.42 * j / 10 for j in range(11)], top, 0.05)
    p.add(pad, 'leather', smooth=True)
    p.add(props.ellipsoid(0.13, 0.05, 0.09, seg=12), 'leather', T(0, ys + 0.2, top + 0.11), smooth=True)   # pommel
    p.add(props.ellipsoid(0.16, 0.05, 0.08, seg=12), 'leather', T(0, ys - 0.2, top + 0.1), smooth=True)    # cantle
    # bridle from the head bone
    arm = next(o for o in root.children_recursive if o.type == 'ARMATURE')
    hb = arm.pose.bones['Bone.002']
    a, b = arm.matrix_world @ hb.head, arm.matrix_world @ hb.tail
    for t in (0.12, 0.72):   # crownpiece behind the ears, noseband above the nostrils
        ring = _head_ring(vs, a, b, t)
        if ring:
            p.add(props.tube(ring, 0.024, seg=8), 'tint', smooth=True, tint=True)   # ~1-2 px at 1x
    nose = _head_ring(vs, a, b, 0.72)
    if nose:
        for side in (6, 18):   # reins from the bit rings at the noseband's sides to the pommel
            q = nose[side]
            p.add(props.tube([q, q.lerp(Vector((0, ys + 0.25, top + 0.15)), 0.5) + Vector((0, 0, -0.08)),
                              Vector((0.06 if side == 6 else -0.06, ys + 0.25, top + 0.12))], 0.014, seg=6),
                  'leather', smooth=True)
    p.finish()
    return (0.0, ys, top + 0.08)


BUILDERS = {'Horseman': horseman}


def build(unit, colors=None, **params):
    return BUILDERS[unit](colors or {}, **params)
