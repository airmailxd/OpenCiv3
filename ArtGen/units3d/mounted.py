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
from mathutils import Matrix, Vector

import props
import vkit
from props import R, T

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
HORSE_HEIGHT = 2.08      # ground to ear tips, meters (a big war horse, about 16.3 hands at the withers)


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


def _lin(hexcol):
    import bscene
    return (*bscene.srgb_to_linear(bscene.hex_color(hexcol)), 1.0)


# The default specular reflects the sky on up-facing surfaces and greys dark colors;
# bscene.material uses this level on dielectrics, and so do the horse's own materials.
SPECULAR = 0.18
LEGGY = 1.07   # the asset is a stocky draft horse: a little taller and slimmer reads as a warhorse
COAT_LUM = 0.42   # the asset texture's typical linear luminance (its median is ~0.45)


def _coat_material(h):
    """The horse's own painted texture (AO, normal map, and its brightness as
    detail) recolored to the catalog's coat: coat color x (texture luminance /
    COAT_LUM), so the painted muscle shading, hair direction and wear stay
    while the color is the unit's. Legs below the knee and hock go to the
    'points' color, the muzzle darker, and road dirt darkens toward the ground."""
    m = bpy.data.materials.new('horse_coat')
    m.use_nodes = True
    nt = m.node_tree
    N, L = nt.nodes, nt.links
    p = N['Principled BSDF']
    p.inputs['Roughness'].default_value = 0.42   # a short, glossy coat
    p.inputs['Specular IOR Level'].default_value = SPECULAR   # as bscene.material's dielectrics
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
    nm.inputs['Strength'].default_value = 0.7
    L.new(nrm.outputs['Color'], nm.inputs['Color'])
    L.new(nm.outputs['Normal'], p.inputs['Normal'])

    def mix(a, b, fac, blend='MIX'):
        n = N.new('ShaderNodeMix')
        n.data_type = 'RGBA'
        n.blend_type = blend
        for sock, v in (('A', a), ('B', b)):
            if isinstance(v, tuple):
                n.inputs[sock].default_value = v
            else:
                L.new(v, n.inputs[sock])
        if isinstance(fac, (int, float)):
            n.inputs['Factor'].default_value = fac
        else:
            L.new(fac, n.inputs['Factor'])
        return n.outputs['Result']

    def maprange(src_, f0, f1, t0, t1):
        r = N.new('ShaderNodeMapRange')
        r.inputs['From Min'].default_value = f0
        r.inputs['From Max'].default_value = f1
        r.inputs['To Min'].default_value = t0
        r.inputs['To Max'].default_value = t1
        L.new(src_, r.inputs['Value'])
        return r.outputs['Result']

    # detail: the texture's luminance relative to its typical value, as a gray
    bw = N.new('ShaderNodeRGBToBW')
    L.new(tex.outputs['Color'], bw.inputs['Color'])
    # (a little extra contrast in the painted muscle shading: from 0.6x..1.4x the typical luminance to 0.4..1.6)
    detail = maprange(bw.outputs['Val'], 0.4 * COAT_LUM, 1.6 * COAT_LUM, 0.7, 1.3)
    clamp = N.new('ShaderNodeClamp')
    clamp.inputs['Min'].default_value = 0.2
    clamp.inputs['Max'].default_value = 1.8
    L.new(detail, clamp.inputs['Value'])
    gray = N.new('ShaderNodeCombineColor')
    for ch in ('Red', 'Green', 'Blue'):
        L.new(clamp.outputs['Result'], gray.inputs[ch])
    coat = mix(_lin(h.get('coat', '#7a4526')), gray.outputs['Color'], 1.0, 'MULTIPLY')
    tc = N.new('ShaderNodeTexCoord')
    if h.get('dapple'):
        # dapples: soft round spots of the lighter/darker dapple color over the body
        vor = N.new('ShaderNodeTexVoronoi')
        vor.inputs['Scale'].default_value = 9.0
        L.new(tc.outputs['Object'], vor.inputs['Vector'])
        dcol = mix(_lin(h['dapple']), gray.outputs['Color'], 1.0, 'MULTIPLY')
        coat = mix(coat, dcol, maprange(vor.outputs['Distance'], 0.18, 0.32, 0.0, 0.85))
    if h.get('coat2'):
        # pinto patches: large irregular areas of the second coat color
        nz = N.new('ShaderNodeTexNoise')
        nz.inputs['Scale'].default_value = 0.9   # a few large pinto patches
        nz.inputs['Detail'].default_value = 3.0
        L.new(tc.outputs['Object'], nz.inputs['Vector'])
        c2 = mix(_lin(h['coat2']), gray.outputs['Color'], 1.0, 'MULTIPLY')
        coat = mix(coat, c2, maprange(nz.outputs['Fac'], 0.5, 0.54, 0.0, 1.0))
    coat = mix(coat, ao.outputs['Color'], 0.4, 'MULTIPLY')
    # points: legs darken toward the hooves (world height; the horse stands on z = 0)
    geo = N.new('ShaderNodeNewGeometry')
    sep = N.new('ShaderNodeSeparateXYZ')
    L.new(geo.outputs['Position'], sep.inputs['Vector'])
    pts_col = mix(_lin(h.get('points', '#1e1610')), gray.outputs['Color'], 0.35, 'MULTIPLY')
    coat = mix(coat, pts_col, maprange(sep.outputs['Z'], 0.5, 0.58, 1.0, 0.0))   # a hard boundary at the knee/hock
    # road dirt: darker toward the ground, on the legs and the belly
    dark = N.new('ShaderNodeHueSaturation')
    L.new(coat, dark.inputs['Color'])
    L.new(maprange(sep.outputs['Z'], 0.2, 1.15, 0.8, 1.05), dark.inputs['Value'])
    aov = N.new('ShaderNodeOutputAOV')
    aov.aov_name = 'tint'
    aov.inputs['Value'].default_value = 0.0
    cs = h.get('coat_spots')
    if cs:
        # civ-colored spots painted on the coat (Civ3's Rider's specks, the Mounted Warrior's war paint):
        # a Voronoi of round spots, kept to boxes of the body (y0, y1, z0, z1), written to the tint AOV
        def math_(op, a, b):
            n = N.new('ShaderNodeMath')
            n.operation = op
            for i, v in enumerate((a, b)):
                if isinstance(v, (int, float)):
                    n.inputs[i].default_value = v
                else:
                    L.new(v, n.inputs[i])
            return n.outputs[0]
        stc = N.new('ShaderNodeTexCoord')
        stc.name = 'spot_tc'   # horse_asset points it at the unit's root: metres, turning with the unit
        sepo = N.new('ShaderNodeSeparateXYZ')
        L.new(stc.outputs['Object'], sepo.inputs['Vector'])
        vor = N.new('ShaderNodeTexVoronoi')
        vor.inputs['Scale'].default_value = cs.get('scale', 14.0)
        vor.inputs['Randomness'].default_value = 0.8
        L.new(stc.outputs['Object'], vor.inputs['Vector'])
        size = cs.get('size', 0.2)
        spot = maprange(vor.outputs['Distance'], size - 0.03, size, 1.0, 0.0)
        region = None
        for y0, y1, z0, z1 in cs.get('boxes', []):
            box = math_('MULTIPLY', maprange(sepo.outputs['Y'], y0, y0 + 0.04, 0.0, 1.0),
                        maprange(sepo.outputs['Y'], y1 - 0.04, y1, 1.0, 0.0))
            box = math_('MULTIPLY', box, maprange(sepo.outputs['Z'], z0, z0 + 0.03, 0.0, 1.0))
            box = math_('MULTIPLY', box, maprange(sepo.outputs['Z'], z1 - 0.03, z1, 1.0, 0.0))
            region = box if region is None else math_('MAXIMUM', region, box)
        mask = math_('MULTIPLY', spot, region) if region is not None else None
        for by, bz, br in cs.get('blobs', []):
            # a bold round mark on both flanks: distance from (by, bz) in the side plane
            dy = math_('SUBTRACT', sepo.outputs['Y'], by)
            dz = math_('SUBTRACT', sepo.outputs['Z'], bz)
            d = math_('SQRT', math_('ADD', math_('MULTIPLY', dy, dy), math_('MULTIPLY', dz, dz)), 0.0)
            blob = maprange(d, br - 0.015, br, 1.0, 0.0)
            mask = blob if mask is None else math_('MAXIMUM', mask, blob)
        L.new(mask, aov.inputs['Value'])
        light = (0.82 ** 2.2,) * 3 + (1.0,)   # tinted parts render light gray, as bscene.material's
        under = dark.outputs['Color']
        for py, pz, pr, pcol in cs.get('pale', []):   # a pale patch of coat under the paint (the Mounted Warrior's haunch)
            dy = math_('SUBTRACT', sepo.outputs['Y'], py)
            dz = math_('SUBTRACT', sepo.outputs['Z'], pz)
            d = math_('SQRT', math_('ADD', math_('MULTIPLY', dy, dy), math_('MULTIPLY', dz, dz)), 0.0)
            under = mix(under, mix(_lin(pcol), gray.outputs['Color'], 1.0, 'MULTIPLY'), maprange(d, pr - 0.04, pr, 1.0, 0.0))
        L.new(mix(under, light, mask), p.inputs['Base Color'])
    else:
        L.new(dark.outputs['Color'], p.inputs['Base Color'])
    return m


def _hair_material(h):
    """Mane and tail cards: the catalog's mane color carrying the asset hair
    texture's strand detail (its brightness) and its alpha."""
    m = bpy.data.materials.new('horse_hair')
    m.use_nodes = True
    nt = m.node_tree
    N, L = nt.nodes, nt.links
    p = N['Principled BSDF']
    p.inputs['Roughness'].default_value = 0.7
    p.inputs['Specular IOR Level'].default_value = SPECULAR
    tex = N.new('ShaderNodeTexImage')
    tex.image = _image('Hair12Main2k.png')
    tex.image.colorspace_settings.name = 'sRGB'
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
    tone.inputs['A'].default_value = _lin(h.get('mane', '#140e0a'))
    comb = N.new('ShaderNodeCombineColor')
    for ch in ('Red', 'Green', 'Blue'):
        L.new(k.outputs['Result'], comb.inputs[ch])
    L.new(comb.outputs['Color'], tone.inputs['B'])
    L.new(tex.outputs['Alpha'], p.inputs['Alpha'])
    aov = N.new('ShaderNodeOutputAOV')
    aov.aov_name = 'tint'
    aov.inputs['Value'].default_value = 0.0
    tip = h.get('tail_tip')
    if tip:
        # the tail's lower part bound in a civ wrap (Civ3's Hussar): below z = tip, behind the rump
        stc = N.new('ShaderNodeTexCoord')
        stc.name = 'spot_tc'   # horse_asset points it at the unit's root
        sep = N.new('ShaderNodeSeparateXYZ')
        L.new(stc.outputs['Object'], sep.inputs['Vector'])
        lo = N.new('ShaderNodeMath')
        lo.operation = 'LESS_THAN'
        lo.inputs[1].default_value = tip
        L.new(sep.outputs['Z'], lo.inputs[0])
        back = N.new('ShaderNodeMath')
        back.operation = 'LESS_THAN'
        back.inputs[1].default_value = -0.8
        L.new(sep.outputs['Y'], back.inputs[0])
        both = N.new('ShaderNodeMath')
        both.operation = 'MULTIPLY'
        L.new(lo.outputs['Value'], both.inputs[0])
        L.new(back.outputs['Value'], both.inputs[1])
        mx = N.new('ShaderNodeMix')
        mx.data_type = 'RGBA'
        L.new(both.outputs['Value'], mx.inputs['Factor'])
        L.new(tone.outputs['Result'], mx.inputs['A'])
        mx.inputs['B'].default_value = (0.82 ** 2.2,) * 3 + (1.0,)
        L.new(mx.outputs['Result'], p.inputs['Base Color'])
        L.new(both.outputs['Value'], aov.inputs['Value'])
    else:
        L.new(tone.outputs['Result'], p.inputs['Base Color'])
    return m


def _raise_head(objs, degrees, by_name=None):
    """Carry the head higher, like Civ3's horse (the asset rests grazing-low):
    turn the neck bone up. The mane and eyes aren't bound to the rig, so they
    are parented to the neck and head bones first and move with them."""
    arm = next(o for o in objs if o.type == 'ARMATURE')
    bpy.context.view_layer.update()
    names = by_name or {o.name: o for o in objs}
    for src_name, o in names.items():
        bone = 'Bone.001' if src_name == 'BezierCurve' else 'Bone.002' if src_name.startswith('Sphere') else None
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


def horse_asset(root, colors, neck_raise=-38.0):
    """Append the CC0 horse under `root`, facing +Y, standing on z = 0,
    HORSE_HEIGHT tall, with rebuilt materials. Returns the body object."""
    path = _horse_blend()
    with bpy.data.libraries.load(path, link=False) as (src, dst):
        wanted = [n for n in HORSE_OBJECTS if n in src.objects]
        dst.objects = list(wanted)
        # the old Blender Internal materials no longer reference the packed
        # textures, so the images are appended by name
        dst.images = [n for n in src.images if n.startswith(('HorseMain', 'Hair12'))]
    objs = [o for o in dst.objects if o is not None]
    # several horses in one scene get renamed copies (Plane.001, ...): keep the source names
    by_name = {n: o for n, o in zip(wanted, dst.objects) if o is not None}
    holder = bpy.data.objects.new('horse_asset', None)
    bpy.context.collection.objects.link(holder)
    holder.parent = root
    for o in objs:
        bpy.context.collection.objects.link(o)
        if o.parent is None:
            o.parent = holder
    body = by_name['Plane']
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
    holder.scale = (s * 0.95, s * 0.95, s * LEGGY)
    holder.location = (xc * s * 0.95, yc * s * 0.95, -zmin * s * LEGGY)
    _raise_head(objs, neck_raise, by_name)
    coat = _coat_material(colors)
    if 'spot_tc' in coat.node_tree.nodes:
        coat.node_tree.nodes['spot_tc'].object = root
    hair = _hair_material(colors)
    if 'spot_tc' in hair.node_tree.nodes:
        hair.node_tree.nodes['spot_tc'].object = root
    eye = bpy.data.materials.new('horse_eye')
    eye.use_nodes = True
    eye.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.01, 0.008, 0.006, 1)
    eye.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = 0.15
    for n, o in by_name.items():
        if o.type != 'MESH':
            continue
        o.data.materials.clear()
        o.data.materials.append(coat if n == 'Plane' else hair if n.startswith('Bezier') else eye)
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


HORSE_COLORS = {'coat': '#8a5430', 'mane': '#140e0a', 'points': '#1e1610', 'muzzle': '#30221a', 'leather': '#3e2a1a'}


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


def _sections(vs, ys, zmin=0.85, window=0.1):
    """Body cross-sections at each y: (half-width, top z, belly z, center z) of
    the horse's barrel (vertices above zmin, so the legs don't count)."""
    out = []
    for y in ys:
        # the widest the barrel gets near this station, so the cloth clears it between stations
        sl = [v for v in vs if abs(v.y - y) < window and v.z > zmin]
        if not sl:
            out.append(None)
            continue
        w = max(abs(v.x) for v in sl)
        top = max(v.z for v in sl)
        mid = [v for v in sl if abs(v.x) < 0.12]
        belly = min(v.z for v in mid) if mid else zmin
        out.append((w, top, belly, 0.5 * (top + belly)))
    return out


def drape(vs, y0, y1, hem, n=24, k=24, gap=0.035, flare=0.06, folds=0.018, zmin=0.85):
    """A cloth hanging over the horse's back from y0 to y1: each section an
    arc over the body (its measured half-width and top, `gap` off it) down to
    the widest point, then falling straight to the hem height (a number or a
    function of y), flaring out a little. Returns a bmesh."""
    ys = [y0 + (y1 - y0) * j / n for j in range(n + 1)]
    secs = _sections(vs, ys, zmin)
    rings = []
    for y, sec in zip(ys, secs):
        if sec is None:
            continue
        w, top, belly, zc = sec
        zh = hem(y) if callable(hem) else hem
        hh = top - zc
        ring = [(-(w + gap + flare), y, zh)]
        for i in range(k + 1):
            a = math.pi * (1 - i / k)          # from the left side over the top to the right
            ring.append(((w + gap) * math.cos(a), y, zc + (hh + gap) * math.sin(a)))
        ring.append(((w + gap + flare), y, zh))
        # folds: the hanging cloth ripples in and out along its length (more toward the hem)
        fold = folds * math.sin(2 * math.pi * (y - y0) / 0.16)
        ring = [(x + math.copysign(fold * max(0.0, (zc - z) / max(zc - zh, 1e-3)), x), yy, z) for x, yy, z in ring]
        rings.append(ring)
    return props.loft(rings, False, False), rings


def _dots(rx, period=0.11, r=0.028, scatter=False):
    """A face-key function for a spotted caparison (Sipahi): a staggered grid of
    round dots, or (scatter) an irregular scatter of white spots."""
    from mathutils import noise as _n

    def keys(f):
        c = f.calc_center_median()
        if scatter:
            per, rr = 0.11, 0.03   # round spots on a jittered staggered grid
            u, v = (c.y + c.x * 0.6) / per, c.z / per
            iu, iv = math.floor(u), math.floor(v)
            ju = _n.noise(Vector((iu * 1.7, iv * 2.3, 0.5))) * 0.35
            jv = _n.noise(Vector((iu * 2.9, iv * 1.1, 1.5))) * 0.35
            du, dv = (u - iu - 0.5 - ju) * per, (v - iv - 0.5 - jv) * per
            return ('blanket2', False) if du * du + dv * dv < rr * rr else None
        u, v = c.y / period, c.z / period
        if int(math.floor(v)) % 2:
            u += 0.5
        du, dv = (u - math.floor(u) - 0.5) * period, (v - math.floor(v) - 0.5) * period
        return ('blanket2', False) if du * du + dv * dv < r * r else None
    return keys


def _patchwork(period=0.13):
    """A face-key function for a patchwork blanket: alternate squares in white
    ('blanket2') over the civ cloth (Civ3's Mounted Warrior)."""
    def keys(f):
        c = f.calc_center_median()
        i, j = math.floor((c.y + 3.0) / period), math.floor(c.z / period)
        return ('blanket2', False) if (i + j) % 2 else None
    return keys


def _spot_discs(p, bm, per=0.12, r=0.03, outward=False):
    """Small round white discs (key 'blanket2') laid on a cloth bmesh, one per
    cell of a jittered grid over the whole cloth: Civ3's spotted caparison."""
    from mathutils import noise as _n
    bm.normal_update()
    best = {}
    for f in bm.faces:
        c = f.calc_center_median()
        nrm = f.normal.copy()
        if nrm.dot(c - Vector((0, c.y, 1.2))) < 0:   # always out from the body's long axis
            nrm = -nrm
        u, v = (c.y + c.x * 0.7) / per, c.z / per
        cell = (math.floor(u), math.floor(v), c.x > 0)
        ju = 0.5 + _n.noise(Vector((cell[0] * 1.7, cell[1] * 2.3, 0.5))) * 0.4
        jv = 0.5 + _n.noise(Vector((cell[0] * 2.9, cell[1] * 1.1, 1.5))) * 0.4
        d = (u - cell[0] - ju) ** 2 + (v - cell[1] - jv) ** 2
        if cell not in best or d < best[cell][0]:
            best[cell] = (d, c, nrm)
    for d, c, nrm in best.values():
        m = Matrix.Translation(c + nrm * 0.014) @ nrm.to_track_quat('Z', 'Y').to_matrix().to_4x4()
        p.add(props.ellipsoid(r, r, 0.004, seg=10), 'spot_white', m, smooth=True)


def _scales(period=0.09):
    """A face-key function giving mail/lamellar barding rows of lighter and darker plates."""
    def keys(f):
        c = f.calc_center_median()
        return ('barding_dark', False) if int(math.floor(c.z / period + 0.5 * (int(c.y / period) % 2))) % 2 else None
    return keys


props.DEFAULTS.setdefault('tassel', ('#1a1614', 0.9, 0.0, 'rope'))
props.DEFAULTS.setdefault('spot_white', ('#f2f0ea', 0.8, 0.0, 'polished'))


def _girth_arc(vs, z, front, sec, ys):
    """Points round the horse's chest (front) or rump (not front) at height z,
    just off the body, from one side of the saddle to the other."""
    w = sec[0]
    band = [v for v in vs if abs(v.z - z) < 0.05 and ((v.y > ys + 0.1) if front else (v.y < ys - 0.2))]
    pts = []
    for i in range(13):
        x = -w + 2 * w * i / 12
        col = [v.y for v in band if abs(v.x - x) < 0.05]
        if col:
            pts.append(Vector((x * 1.06, (max(col) + 0.025) if front else (min(col) - 0.025), z)))
    if len(pts) < 3:
        return []
    y_side = ys + 0.18 if front else ys - 0.26
    zs = sec[1] - 0.12   # up to the saddle's skirt
    return [Vector((-(w + 0.04), y_side, zs))] + pts + [Vector((w + 0.04, y_side, zs))]


def _extra_tack(p, h, vs, arm, a, b, ys, top, sec):
    """The originals' smaller tack, each from the horse entry `h`:
    'breast_strap' / 'crupper' (the strap's color key) round the chest / rump,
    'tassels' (a key) hanging from them; 'nose_wrap' a civ-colored band over
    the muzzle; 'throat_tassels' (a key) under the jaw; 'bedroll' behind the
    cantle; 'bow_case' a gilded bow case at the left of the saddle;
    'cloth_emblem' (a key) a roundel on each flank of the saddle cloth."""
    tint = lambda key: key == 'tint'   # noqa: E731
    for opt, front, z in (('breast_strap', True, sec[3] + 0.06), ('crupper', False, sec[1] - 0.1)):
        key = h.get(opt)
        if not key:
            continue
        arc = _girth_arc(vs, z, front, sec, ys)
        if not arc:
            continue
        sag = h.get('strap_sag', 0.0)   # a strap hanging in a loose loop, lowest at the middle
        if sag:
            n = len(arc) - 1
            arc = [q + Vector((0, 0.3 * sag * math.sin(math.pi * i / n) if front else 0, -sag * math.sin(math.pi * i / n)))
                   for i, q in enumerate(arc)]
        p.add(props.tube(arc, h.get('strap_w', 0.026), seg=6, scale_xy=(1.0, 0.45)), key, smooth=True, tint=tint(key))
        tk = h.get('tassels')
        if tk:
            for q in arc[2:-2:2]:
                p.add(props.tube([q + Vector((0, 0.01 if front else -0.01, -0.01)), q + Vector((0, 0.03 if front else -0.03, -0.2))],
                                 [0.07, 0.025], seg=8), h.get('tassel_key', tk), smooth=True, tint=tint(h.get('tassel_key', tk)))
                p.add(props.ellipsoid(0.03, 0.03, 0.03, seg=8), 'trim', T(*q), smooth=True)   # a brass stud
    if h.get('nose_wrap'):
        rings = [r for r in (_head_ring(vs, a, b, t, grow=0.02) for t in (0.48, 0.56, 0.64, 0.72, 0.8)) if r]
        if len(rings) >= 2:
            p.add(props.loft(rings, False, False), 'tint', smooth=True, tint=True)
    tk = h.get('throat_tassels')
    if tk:
        ring = _head_ring(vs, a, b, 0.62, grow=0.01)
        if ring:
            low = min(ring, key=lambda v: v.z)
            for dx in (-0.05, 0.0, 0.05):
                q = low + Vector((dx, 0.02, 0))
                p.add(props.tube([q, q + Vector((0, 0.02, -0.2)), q + Vector((0, 0.03, -0.42))], [0.05, 0.07, 0.03],
                                 seg=6), tk, smooth=True, tint=tint(tk))
    if h.get('bedroll'):
        p.add(props.tube([(-0.26, ys - 0.34, top + 0.05), (0.26, ys - 0.34, top + 0.05)], 0.075, seg=12), 'bedroll',
              smooth=True)
        for x in (-0.14, 0.14):
            p.add(props.lathe([(0.0, 0.082), (0.03, 0.082)], seg=12), 'leather_dark',
                  T(x, ys - 0.34, top + 0.05) @ R('Y', 90), smooth=True)
    if h.get('bow_case'):
        w = sec[0]
        m = T(-(w + 0.09), ys - 0.12, top - 0.22) @ R('X', -35)
        p.add(props.ellipsoid(0.035, 0.13, 0.3, seg=12), 'trim', m, smooth=True)          # the gilded case
        p.add(props.tube([(0, 0.02, 0.2), (0, 0.06, 0.42)], 0.012, seg=6), 'wood', m, smooth=True)   # the bow's tip
        p.add(props.tube([(0, -0.1, 0.1), (0, -0.12, 0.3)], [0.05, 0.05], seg=8), 'leather', m, smooth=True)  # quiver
    ek = h.get('cloth_emblem')
    if ek:
        for y in h.get('emblem_ys', [0.0]):
            s = _sections(vs, [ys + y])[0] or sec
            for sx in (-1, 1):
                _roundel(p, ek, T(sx * (s[0] + 0.075), ys + y, s[3] - 0.08) @ R('Y', sx * 90), tint)


def _roundel(p, ek, m, tint):
    """A bold solid white drop/triangle emblem (Civ3's Rider), facing the matrix's +Z."""
    tri = props.slab([(0.0, 0.3), (0.22, -0.15), (0.0, -0.26), (-0.22, -0.15)], 0.02, 'Y')   # big: about 40% of the panel
    p.add(tri, ek, m @ R('X', 90), tint=tint(ek), bevel=0.006)


def asset_tack(root, body, colors, h=None):
    """Civ3's tack on the asset horse, from the catalog's 'horse' entry `h`:
    the blanket ('saddle_cloth' civ-colored or in 'blanket_color'; 'pelt';
    'barding': mail to the knees under a civ caparison; 'dots': a long civ
    caparison with light dots; None: bareback), a leather saddle with pommel
    and cantle, a bridle (civ-colored unless 'bridle_tint' is False) and
    reins, and optionally a steel chanfron, a crinet over the neck and a
    plume. Returns the seat point for the rider empty."""
    h = h or {}
    wear = {'leather': {'grime': 0.6, 'soot': 0.2}, 'tint': {'grime': 0.6, 'soot': 0.3},
            'blanket': {'grime': 0.55, 'soot': 0.2}, 'pelt': {'grime': 0.5}, 'barding_mail': {'grime': 0.5, 'rust': 0.2}}
    c = dict(colors)
    for k_src, k_dst in (('blanket_color', 'blanket'), ('blanket2', 'blanket2'), ('barding_steel', 'barding_mail'),
                         ('trim', 'trim'), ('saddle', 'leather'), ('bridle', 'bridle'), ('tack', 'leather'),
                         ('plume_color', 'plume'), ('pelt_color', 'pelt')):
        if h.get(k_src):
            c[k_dst] = h[k_src]
    for key, (col, rough, metal, kind) in {'blanket': ('#7e6650', 0.85, 0.0, 'cloth'), 'blanket2': ('#eeece6', 0.85, 0.0, 'cloth'),
                                           'barding_mail': ('#9a9ea6', 0.45, 1.0, 'metal'), 'trim': ('#2e2014', 0.6, 0.0, 'leather'),
                                           'bridle': ('#4a3424', 0.6, 0.0, 'leather'), 'plume': ('#eeece6', 0.9, 0.0, 'cloth'),
                                           'pelt': ('#3a2e26', 0.95, 0.0, 'rope'),
                                           'barding_dark': ('#3a3c40', 0.5, 1.0, 'metal')}.items():
        props.DEFAULTS.setdefault(key, (col, rough, metal, kind))
    p = vkit.part(root, 'tack', c, wear)
    vs = _world_verts(body)
    feet = [v for v in vs if v.z < 0.15]
    yf = max(v.y for v in feet)                  # the front hooves
    yh = min(v.y for v in feet)                  # the hind hooves
    ys = yh + 0.6 * (yf - yh)                    # the saddle sits just behind the withers
    top = max(v.z for v in vs if abs(v.y - ys) < 0.08 and abs(v.x) < 0.1)
    sec = _sections(vs, [ys])[0]
    blanket = h.get('blanket', 'saddle_cloth')
    tinted = h.get('blanket_tint', True)
    trim = 'trim' if h.get('trim') else 'leather_dark'

    def hem_line(rings, key):
        wide = key == 'tint'   # a civ-colored trim reads as a band, not a thread
        for edge in ([r[0] for r in rings], [r[-1] for r in rings], rings[0], rings[-1]):
            p.add(props.tube(edge, 0.035 if wide else 0.014, seg=6, scale_xy=(1.0, 0.4) if wide else (1.0, 1.0)), key,
                  smooth=True, tint=wide)

    if blanket in ('saddle_cloth', 'pelt'):
        # deeper for a larger catalog cloth_drop (the old default was 0.24)
        frac = max(0.3, min(1.15, 0.75 * h.get('cloth_drop', 0.24) / 0.24))
        hem = sec[3] - frac * (sec[3] - sec[2])
        # 'cloth_back' reaches it back over the rump (Civ3's Rider)
        cloth, rings = drape(vs, ys - h.get('cloth_back', 0.3), ys + 0.26, hem, folds=0.006 if blanket == 'pelt' else 0.018)
        key = 'pelt' if blanket == 'pelt' else ('tint' if tinted else 'blanket')
        p.add(cloth, key, smooth=True, tint=key == 'tint', keys=_patchwork() if h.get('cloth_keys') == 'patchwork' else None)
        hem_line(rings, trim)
    elif blanket in ('barding', 'dots'):
        # a long caparison to the knees over the whole body; for barding, mail beneath
        knee = 0.62
        if blanket == 'barding':
            mail_hem = (sec[2] - 0.02) if h.get('full_caparison') else knee - 0.06   # under a caparison: plate to the belly, legs bare
            mail, mrings = drape(vs, yh + 0.05 * (yf - yh), yf - 0.12 * (yf - yh), mail_hem, gap=0.02, folds=0.005)
            p.add(mail, 'barding_mail', smooth=True, keys=None if h.get('full_caparison') else _scales())
            if h.get('full_caparison'):   # plate barding: a few riveted lame lines, not a checker
                for zl in (sec[2] + 0.15, sec[2] + 0.35):
                    for sx in (-1, 1):
                        pts = [q for q in (Vector((sx * (s_[0] + 0.03), y_, zl)) for y_, s_ in
                               ((yy, _sections(vs, [yy])[0]) for yy in [ys - 0.3 + 0.04 * i for i in range(11)]) if s_)]
                        if len(pts) > 2:
                            p.add(props.tube(pts, 0.012, seg=5), 'barding_dark', smooth=True)
            if not h.get('caparison_over'):
                hem_line(mrings, 'tint' if h.get('trim_tint') else ('trim' if h.get('full_caparison') else trim))
                rings = None
            span = h.get('over_span', [-0.62, 0.36])
            y0, y1 = ys + span[0] * (yf - yh) * 0.6, ys + span[1] * (yf - yh) * 0.6
            if h.get('caparison_over'):
                cap, rings = drape(vs, y0, y1, knee + h.get('over_gap', 0.06) + 0.08, gap=0.05, folds=0.02)
                p.add(cap, 'tint', smooth=True, tint=True)
        else:
            y0, y1 = yh - h.get('dots_back', -0.05) * (yf - yh), yf - h.get('dots_front', 0.15) * (yf - yh)
            cap, rings = drape(vs, y0, y1, h.get('dots_hem', knee), gap=0.04, folds=0.018)
            if h.get('dot_scatter'):   # round white discs laid on the cloth (face-keyed spots streaked)
                _spot_discs(p, cap)
                p.add(cap, 'tint', smooth=True, tint=True)
            else:
                p.add(cap, 'tint', smooth=True, tint=True, keys=_dots(0.0))
        if rings:
            hem_line(rings, trim)
    if h.get('full_caparison'):
        knee = sec[2] + 0.06   # just below the belly line: the long legs show beneath
        y0, y1 = yh - 0.28, yf + 0.22   # from over the tail round the chest
        per = h.get('stripe_period', 0.62)

        def cap_keys(f, hem=knee):
            c_ = f.calc_center_median()
            if c_.z < hem + 0.15 and c_.z > hem + 0.1:           # the gold band above the hem
                return ('trim', False)
            if abs(c_.x) > 0.2 and ((c_.y - y0) % per) < 0.035:   # the vertical gold stripes on the flanks, few and thin
                return ('trim', False)
            return None
        wavy = lambda y: knee + 0.06 * math.sin((y - y0) * 11.0)   # a scalloped, dagged hem
        # two panels, rump and chest, the silver barding showing between them across the mid-flank
        for a_, b_ in ((y0, ys - 0.32), (ys + 0.12, y1)):
            cap, crings = drape(vs, a_, b_, wavy, gap=0.05, folds=0.03, flare=0.03, zmin=0.7)
            p.add(cap, 'tint', smooth=True, tint=True, keys=cap_keys)
            hem_line(crings, 'trim')
    if h.get('peytral'):
        # a steel breastplate showing above the chest cloth, riveted
        arc = _girth_arc(vs, sec[3] + 0.18, True, sec, ys)
        if arc:
            p.add(props.tube(arc[1:-1], 0.075, seg=8, scale_xy=(1.0, 0.35)), 'barding_mail', smooth=True)
            for q in arc[1:-1]:
                p.add(props.ellipsoid(0.016, 0.016, 0.016, seg=6), 'trim', T(*(q + Vector((0, 0.03, 0.04)))), smooth=True)
    if blanket is not None and h.get('saddle_visible', True):
        pad = _shrink_patch('saddle', body, [-0.24 + 0.48 * i / 12 for i in range(13)],
                            [ys - 0.22 + 0.42 * j / 10 for j in range(11)], top, 0.05 if blanket not in ('barding', 'dots') else 0.09)
        p.add(pad, 'leather', smooth=True)
        p.add(props.ellipsoid(0.13, 0.05, 0.09, seg=12), 'leather', T(0, ys + 0.2, top + 0.11), smooth=True)   # pommel
        p.add(props.ellipsoid(0.16, 0.05, 0.08, seg=12), 'leather', T(0, ys - 0.2, top + 0.1), smooth=True)    # cantle
    # bridle from the head bone
    arm = next(o for o in root.children_recursive if o.type == 'ARMATURE')
    hb = arm.pose.bones['Bone.002']
    a, b = arm.matrix_world @ hb.head, arm.matrix_world @ hb.tail
    btint = h.get('bridle_tint', True) and not h.get('bridle')
    for t in (0.12, 0.66):   # crownpiece behind the ears, noseband above the nostrils
        ring = _head_ring(vs, a, b, t, grow=0.004)
        if ring:
            p.add(props.tube(ring, 0.034 if h.get('reins_bold') else 0.02, seg=8), 'tint' if btint else 'bridle',
                  smooth=True, tint=btint)
    if h.get('chanfron'):
        # a steel face plate down the front of the head
        mid = a.lerp(b, 0.45)
        axis = (b - a).normalized()
        front = axis.cross(Vector((1, 0, 0))).normalized()
        if front.z < 0:
            front = -front
        rot = axis.to_track_quat('Z', 'Y').to_matrix().to_4x4()
        ck = 'tint' if h.get('chanfron') == 'tint' else 'barding_mail'   # a civ-colored cloth face piece
        if h.get('chanfron') == 'steel':
            m = Matrix.Translation(mid + front * 0.11) @ rot
            p.add(props.ellipsoid(0.11, 0.045, 0.34, seg=18), ck, m, smooth=True)                      # the face plate
            p.add(props.ellipsoid(0.02, 0.03, 0.3, seg=8), 'barding_dark', m @ T(0, 0.04, 0.02), smooth=True)   # the central ridge
            p.add(props.lathe([(0.0, 0.03), (0.12, 0.0)], seg=8), 'trim', m @ T(0, 0.06, 0.12) @ R('X', -70), smooth=True)  # the spike
            for sx in (-1, 1):   # ear guards and a row of rivets along the edges
                p.add(props.lathe([(0.0, 0.045), (0.1, 0.02)], seg=10), ck, m @ T(sx * 0.07, -0.02, 0.3) @ R('Y', sx * 15), smooth=True)
                for k in range(6):
                    p.add(props.ellipsoid(0.012, 0.012, 0.012, seg=6), 'trim', m @ T(sx * 0.09, 0.03, -0.25 + 0.1 * k), smooth=True)
        else:
            plate = props.ellipsoid(0.075, 0.03, 0.26, seg=14)
            p.add(plate, ck, Matrix.Translation(mid + front * 0.1) @ rot, smooth=True, tint=ck == 'tint')
    if h.get('neck_cover_chest'):
        front, frings = drape(vs, ys + 0.15, yf + 0.25, 0.75, gap=0.05, folds=0.012, zmin=0.6)
        _spot_discs(p, front)
        p.add(front, 'tint', smooth=True, tint=True)
        hem_line(frings, trim)
    if h.get('neck_cover') == 'cloth':
        # a civ-colored cloth over the chest and the base of the neck (Egyptian chariot horses)
        front, frings = drape(vs, ys + 0.22, yf + 0.12, 0.95, gap=0.03, folds=0.012)
        p.add(front, 'tint', smooth=True, tint=True)
        hem_line(frings, trim)
    elif h.get('neck_cover'):
        # a crinet: a sleeve over the neck, fitted to its measured cross-sections
        nb = arm.pose.bones['Bone.001']
        n0, n1 = arm.matrix_world @ nb.head, arm.matrix_world @ nb.tail
        grow_ = 0.045 if h.get('neck_cover') == 'cloth_sleeve' else 0.02
        rings = [r for r in (_head_ring(vs, n0, n1, t, grow=grow_) for t in (0.0, 0.12, 0.3, 0.48, 0.66, 0.84, 0.97)) if r]
        if h.get('neck_cover') == 'lames':
            # overlapping steel plates over the top of the neck, a dark seam and rivets on each
            for i, t_ in enumerate([0.08 + 0.085 * k for k in range(10)]):
                r = _head_ring(vs, n0, n1, t_, grow=0.03 + 0.004 * (i % 2))
                if not r:
                    continue
                top_arc = [q for q in r if q.z > sum(v.z for v in r) / len(r)]
                if len(top_arc) < 3:
                    continue
                top_arc.sort(key=lambda q: q.x)
                p.add(props.tube(top_arc, 0.05, seg=6, scale_xy=(0.35, 1.0)), 'barding_mail', smooth=True)
                p.add(props.tube(top_arc, 0.012, seg=5), 'barding_dark', T(0, 0, -0.03), smooth=True)
                hi = max(top_arc, key=lambda q: q.z)
                p.add(props.ellipsoid(0.014, 0.014, 0.014, seg=6), 'trim', T(*(hi + Vector((0, 0, 0.035)))), smooth=True)
            rings = []
        if len(rings) >= 2:
            sleeve = props.loft(rings, False, False)
            if h.get('neck_cover') == 'cloth_sleeve':
                props.bmesh.ops.subdivide_edges(sleeve, edges=sleeve.edges[:], cuts=4, use_grid_fill=True)
                _spot_discs(p, sleeve, outward=True)
            cloth_cover = h.get('neck_cover') in ('cloth', 'cloth_sleeve')
            p.add(sleeve, 'tint' if cloth_cover else 'barding_mail', smooth=True, tint=cloth_cover,
                  keys=None if cloth_cover else _scales())
    if h.get('plume'):
        ctr = a.lerp(b, 0.08) + Vector((0, 0, 0.12))
        big = 2.2 if h.get('plume') == 'fan' else 1.3   # a tall fan of plumes (Egyptian chariot horses)
        for dx in ((-0.05, -0.025, 0.0, 0.025, 0.05) if big > 1 else (-0.025, 0.0, 0.025)):
            p.add(props.tube([ctr + Vector((dx, 0, 0)), ctr + Vector((dx * 1.5, -0.04, 0.16 * big)),
                              ctr + Vector((dx * 2.5, -0.1, 0.24 * big))], [0.045, 0.05, 0.02], seg=8), 'tint' if h.get('plume_tint', True) else 'plume',
                  smooth=True, tint=h.get('plume_tint', True))
    nose = _head_ring(vs, a, b, 0.66, grow=0.004)
    if nose:
        bold = h.get('reins_bold')   # Civ3 draws some reins as thick civ-colored loops
        for side in (6, 18):   # reins from the bit rings at the noseband's sides to the pommel
            q = nose[side]
            p.add(props.tube([q, q.lerp(Vector((0, ys + 0.25, top + 0.15)), 0.5) + Vector((0, 0, -h.get('rein_drop', 0.14) if bold else -0.08)),
                              Vector((0.06 if side == 6 else -0.06, ys + 0.25, top + 0.12))], 0.024 if bold else 0.014,
                             seg=6), 'tint' if bold else 'leather', smooth=True, tint=bool(bold))
    _extra_tack(p, h, vs, arm, a, b, ys, top, sec)
    if h.get('rump_spots'):
        rnd_ = __import__('random').Random(7)
        back_edge = ys - h.get('cloth_back', 0.3) - 0.03 if blanket in ('saddle_cloth', 'pelt') else 9.0
        hem_z = (hem - 0.05) if blanket in ('saddle_cloth', 'pelt') else 9.0
        # the outer haunch the cloth leaves bare, above the stifle (the posed hind leg moves away below it)
        hind = [v for v in vs if v.y < ys - 0.3 and 0.8 < v.z < 1.2 and abs(v.x) > 0.16
                and (v.y < back_edge or v.z < hem_z)]
        picked = []
        for q in rnd_.sample(hind, len(hind)):   # spread out: no two spots closer than 0.11
            if all((q - o).length > 0.08 for o in picked):
                picked.append(q)
            if len(picked) >= 40:
                break
        for q in picked:
            nrm = Vector((q.x, 0, (q.z - 1.1) * 0.3)).normalized()
            p.add(props.ellipsoid(0.045, 0.045, 0.006, seg=10), 'tint',
                  Matrix.Translation(q + nrm * 0.012) @ nrm.to_track_quat('Z', 'Y').to_matrix().to_4x4(), smooth=True, tint=True)
    if h.get('back_lances'):
        # two lances crossed behind the rider's back, their butts at the cantle (Civ3's Keshik)
        for sx in (-1, 1):
            a_ = Vector((-sx * 0.05, ys - 0.25, top + 0.15))
            b_ = Vector((sx * 0.45, ys - 0.5, top + 1.45))   # a tight V behind the head
            vkit.rod(p, a_, b_, 0.022, 'wood', seg=8)
            d_ = (b_ - a_).normalized()
            p.add(props.lathe([(0.0, 0.08), (0.12, 0.075), (0.4, 0.0)], seg=6), 'gunmetal',
                  Matrix.Translation(b_) @ d_.to_track_quat('Z', 'Y').to_matrix().to_4x4(), smooth=True)
            p.add(props.tube([b_ - d_ * 0.02, b_ - d_ * 0.02 + Vector((0, -0.06, -0.22))], [0.06, 0.02], seg=8), 'tint',
                  smooth=True, tint=True)   # a civ horsehair tassel under the head
    obj = p.finish()
    for m in obj.data.materials:   # painted spots stay white in the shade (as Civ3's)
        if m and m.name.startswith('prop_spot_white'):
            pr = props._principled(m)
            if pr:
                pr.inputs['Emission Color'].default_value = (0.9, 0.9, 0.88, 1.0)
                pr.inputs['Emission Strength'].default_value = 0.55
    return (0.0, ys, top + (0.03 if blanket not in ('barding', 'dots') else 0.07))   # sitting down into the saddle


def seat_rider(root, unit, entry):
    """Seat the unit's rider (the catalog entry's 'rider', with remaster.py's
    changes) on the 'rider' empty: units.build_footman(seated=True) puts the
    pelvis on its root's origin, so the root is parented to the empty, scaled
    by the entry's rider_scale. Needs MPFB (see README.md)."""
    import addon_utils
    addon_utils.enable('bl_ext.user_default.mpfb', default_set=True)
    import remaster
    import units
    import human as H
    seat = next(o for o in root.children_recursive if o.name.startswith('rider'))
    import riders
    base = dict(riders.translate(entry['rider']), archetype='footman')
    for k, v in RIDER_DEFAULTS.items():   # every rider sits tall, covered to the neck (as the Horseman)
        if k == 'neck' and base.get('top') in (None, 'bare', 'strap'):
            continue
        base.setdefault(k, v)
    r_entry = remaster.apply(unit, base)
    rroot = units.build_footman(r_entry, seated=True)
    rroot.parent = seat
    rroot.matrix_parent_inverse.identity()
    k = entry.get('rider_scale', 1.0) * RIDER_SCALE
    rroot.scale = (k, k, k)
    bpy.context.view_layer.update()
    # aim the weapon as Civ3's riders carry it (the riding stance holds it low across)
    arm = next(o for o in rroot.children_recursive if o.type == 'ARMATURE')
    riders.headgear(r_entry, arm)
    weapon = r_entry.get('weapon')
    up = WEAPON_UP_BY_UNIT.get(unit) or RIDER_WEAPON_UP.get(weapon)   # a unit's own carry overrides the weapon's
    if up:
        obj = next((o for o in bpy.data.objects if o.name.split('.')[0] == weapon and o.parent is not None), None)
        if obj is not None:
            H.hold(obj, arm, 'hand_r', H.grip_point(arm, 'hand_r'), up=up)
            bpy.context.view_layer.update()
    return rroot


# The old catalog's rider_scale was tuned for the old, smaller horse.
RIDER_SCALE = 1.26
# How each rider weapon is carried (the weapon's +Z in the rider's space: +y forward, +z up).
RIDER_WEAPON_UP = {
    'cav_spear': (0.0, 0.62, 1.0),   # couched forward and up at a diagonal, as Civ3's Horseman
    'javelin': (0.0, 0.5, 1.0),      # raised, ready to throw
    'lance': (0.0, 1.0, 0.18),       # couched level and forward
    'sabre': (0.15, 0.35, 1.0),      # drawn and held up
    'long_sword': (0.15, 0.3, 1.0),
    'rifle': (0.0, 0.25, 1.0),       # held upright at the hip, as Civ3's Cossack and Cavalry
}
WEAPON_UP_BY_UNIT = {'Conquistador': (0.05, 0.12, 1.0),   # the lance held upright (the original)
                     'Cavalry': (-1.0, 0.25, 0.08),       # the carbine level across the saddle
                     'Cossack': (-1.0, 0.1, -0.3)}   # low across the saddle, the muzzle down
RIDER_DEFAULTS = {
    'neck': 'high',
    'pose_extra': [('spine_01', (0, 0.12, 1)), ('spine_02', (0, 0.16, 1)), ('spine_03', (0, 0.14, 1)),
                   ('neck_01', (0, 0.04, 1)), ('head', (0, -0.04, 1))],
    'grime': 0.55,
}


HORSE_RIDERS = ['Horseman', 'Mounted Warrior', 'Ancient Cavalry', 'Rider', 'Knight', 'Keshik', 'Sipahi',
                'Conquistador', 'Cossack', 'Hussar', 'Cavalry', 'Ansar Warrior']


def mount(unit, h):
    """A horse_rider unit's mount from its catalog 'horse' entry `h`."""
    c = dict(HORSE_COLORS)
    for k in ('coat', 'mane', 'points', 'muzzle'):
        if h.get(k):
            c[k] = h[k]
    over = HORSE_COAT_OVERRIDES.get(unit, {})
    c.update({k: v for k, v in over.items() if k in c})
    h = dict(h, **{k: v for k, v in over.items() if k in ('dapple', 'coat2', 'blanket_tint', 'coat_spots', 'tail_tip')})
    h.update(TACK_OVERRIDES.get(unit, {}))
    root = vkit.root(unit)
    body = horse_asset(root, dict(h, **c))
    seat = asset_tack(root, body, c, h)
    e = vkit.crew(root, 0, seat, yaw=0.0)
    e.name = 'rider'
    return root


# The Horseman's coat as reviewed (a warm bay); the other catalog coats are used as they are.
HORSE_COAT_OVERRIDES = {
    # coats read from the Civ3 originals (the old catalog's were tuned for the old renderer)
    'Horseman': {'coat': '#8a5430', 'mane': '#140e0a', 'points': '#1e1610'},
    # a dun (not a pinto), with civ war paint on the shoulder and the hindquarter
    'Mounted Warrior': {'coat': '#b08a58', 'coat2': None, 'mane': '#2e241a', 'points': '#5a4632',
                        # (the asset horse stands ~1.9 m at the croup: the hip is near z 1.5)
                        'coat_spots': {'pale': [(-0.72, 1.5, 0.45, '#e2d8c2')],   # the cream croup and haunch
                                       'blobs': [(-0.72, 1.5, 0.18), (-0.5, 1.25, 0.07), (-0.92, 1.2, 0.07),
                                                 (0.65, 1.6, 0.15), (0.75, 1.3, 0.07)]}},
    'Ancient Cavalry': {'coat': '#a88a5e', 'mane': '#2a2018', 'points': '#2e2418'},
    'Rider': {'coat': '#c8c8c4', 'dapple': '#9a9a98', 'mane': '#e0ded8', 'points': '#8a8a88',
              'coat_spots': {'scale': 6.0, 'size': 0.34, 'boxes': [(-1.15, -0.2, 0.55, 1.35)]}},   # civ specks on the haunch
    'Knight': {'coat': '#c4c6ca', 'mane': '#3a3836', 'points': '#8a8c90'},   # pale: the silver barding reads   # the lower legs grey as mail
    'Keshik': {'coat': '#7a4826', 'mane': '#1a120c', 'points': '#20160e'},
    'Sipahi': {'coat': '#6a4428', 'mane': '#2a1c12', 'points': '#2a1c12'},
    'Conquistador': {'coat': '#a0a0a2', 'dapple': '#5c5c60', 'mane': '#2a2826', 'points': '#3a3836', 'blanket_tint': False},
    'Cossack': {'coat': '#4a4a4e', 'dapple': '#7a7a80', 'mane': '#141416', 'points': '#1a1a1c'},   # a dapple grey
    'Hussar': {'coat': '#a8865a', 'mane': '#1e1610', 'points': '#2a2018', 'tail_tip': 0.75},   # civ-bound tail tip   # a dun with dark points, as the original
    'Cavalry': {'coat': '#b8843c', 'mane': '#2e241a', 'points': '#3a2c1e', 'blanket_tint': True},
    'Ansar Warrior': {'coat': '#2e2e32', 'mane': '#121214', 'points': '#1c1c1e'},
}

# The originals' tack the catalog doesn't describe (see DETAIL_AUDIT_PC.md)
TACK_OVERRIDES = {
    # Civ3's Cavalry: a dark bridle and reins with a pale noseband, a rolled cloak behind the cantle
    'Cavalry': {'bridle': '#1e1a18', 'bridle_tint': False, 'bedroll': True},
    # Civ3's Keshik: twin lances behind the back, a pale sheepskin saddle, a civ noseband
    'Keshik': {'back_lances': True, 'blanket': 'pelt', 'nose_wrap': True, 'pelt_color': '#ddd2b8'},
    # Civ3's Sipahi: a civ caparison scattered with white spots over the neck to the ears, chest and flanks nearly to
    # the hocks, a civ cloth over the head with a white plume, a gold saddle on a red cloth, tassels
    'Sipahi': {'neck_cover': 'cloth_sleeve', 'dot_scatter': True, 'dots_front': -0.05, 'dots_hem': 0.58, 'dots_back': 0.4, 'chanfron': 'tint',
               'saddle': '#c4a462', 'breast_strap': 'trim', 'tassels': 'trim', 'neck_cover_chest': True},
    # Civ3's Conquistador: a padded cream trapper with a gold fringe, gold leather breast strap and crupper
    'Conquistador': {'blanket_color': '#d8c8a0', 'trim': '#c4a462', 'breast_strap': 'trim', 'crupper': 'trim', 'tassels': 'trim',
                     'cloth_back': 0.6, 'cloth_drop': 0.34},
    # Civ3's Knight: fully barded, a civ caparison banded in gold from chest to rump, steel crinet, chanfron and peytral
    'Knight': {'full_caparison': True, 'neck_cover': 'lames', 'chanfron': 'steel', 'peytral': True, 'caparison_over': False,
               'blanket': 'barding', 'barding_steel': '#c8ccd2'},
    'Horseman': {'nose_wrap': True, 'breast_strap': 'leather_dark'},
    # a civ-and-white patchwork blanket, civ tassels at the bridle and breast strap
    'Mounted Warrior': {'blanket': 'saddle_cloth', 'blanket_tint': True, 'trim': '#c8a878', 'cloth_drop': 0.24,
                        'cloth_back': 0.5, 'breast_strap': 'tint', 'tassels': 'tint', 'throat_tassels': 'tint'},
    # long bold civ reins, a dark pelt saddle pad
    'Ancient Cavalry': {'reins_bold': True, 'rein_drop': 0.3, 'blanket': 'pelt', 'pelt_color': '#3a2e26', 'cloth_drop': 0.26,
                        'breast_strap': 'leather_dark'},
    'Cossack': {'bedroll': True, 'breast_strap': 'leather_dark'},   # the burka rolled behind the saddle
    # a civ shabraque with a gold edge, bold civ reins
    'Hussar': {'blanket': 'saddle_cloth', 'blanket_tint': True, 'cloth_drop': 0.3, 'cloth_back': 0.45, 'trim': '#c4a462',
               'reins_bold': True, 'rein_drop': 0.28, 'breast_strap': 'leather_dark'},
    'Rider': {'breast_strap': 'trim', 'crupper': 'trim', 'tassels': 'trim', 'plume': True, 'plume_tint': False,
              'bedroll': True, 'bow_case': True, 'cloth_emblem': 'blanket2', 'emblem_ys': [0.0, -0.5],
              'cloth_back': 0.85, 'cloth_drop': 0.36, 'chanfron': 'tint', 'reins_bold': True, 'emblem_ys': [-0.35],
              'saddle': '#c4a462', 'cloth_drop': 0.26},
    # the original's heavy blue reins and breast straps hang in big loops with tassels
    'Ansar Warrior': {'throat_tassels': 'tassel', 'reins_bold': True, 'rein_drop': 0.38, 'breast_strap': 'tint', 'strap_w': 0.05,
                      'crupper': 'tint', 'strap_sag': 0.22, 'tassels': 'tint', 'tassel_key': 'tassel'},
}

BUILDERS = {'Horseman': horseman}


def build(unit, colors=None, entry=None, rider=False, **params):
    """The unit's mount (and, with rider=True and its catalog entry, the
    seated rider)."""
    if entry and entry.get('archetype') == 'horse_rider':
        root = mount(unit, entry.get('horse', {}))
    else:
        root = BUILDERS[unit](colors or {}, **params)
    if rider and entry and entry.get('rider'):
        seat_rider(root, unit, entry)
    if entry and entry.get('escort') == 'dog':
        # the war dog trotting beside the horse (Civ3's Conquistador): a black-and-tan mastiff, civ collar
        d = entry.get('dog', {})
        dog = props.make('war_dog', {}, scale=d.get('scale', 1.6))   # a black-and-tan mastiff, civ collar
        dog.parent = root
        x, y, z = d.get('place', [0.95, 0.4, 0.0])
        dog.location = (x, y, z)
    return root
