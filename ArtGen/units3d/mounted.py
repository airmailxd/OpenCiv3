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
    cc = h.get('coat_contrast', 1.0)   # a unit's stronger muscle shading (the realism pass)
    detail = maprange(bw.outputs['Val'], 0.4 * COAT_LUM, 1.6 * COAT_LUM, 1.0 - 0.3 * cc, 1.0 + 0.3 * cc)
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
            under = mix(under, mix(_lin(pcol), gray.outputs['Color'], 1.0, 'MULTIPLY'), maprange(d, pr - 0.12, pr, 1.0, 0.0))
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


# Species-correct proportions over the asset's draft-horse build (the realism pass, for every horse):
# the cannon bones (knee/hock to fetlock) the leg's slenderest part, about 0.6 of the asset's;
# the forearm and gaskin a little leaner; the head smaller, about 0.6 of the neck's length.
LEG_PROFILE = [   # (t from the knee/hock (0) to the ground (1), horizontal scale of the leg's section)
    # (the family pass, approved by the art lead: the cannons at 0.72 and the forearms toward 0.9; at 0.58 the
    # horses read as ponies with thin legs beside the stock mounts. Before: (-0.45, 1.0), (-0.2, 0.9), (0.0, 0.8),
    # (0.1, 0.66), (0.22, 0.58), (0.6, 0.58), (0.74, 0.64), (0.84, 0.68), (0.92, 0.76), (1.0, 0.84))
    (-0.45, 1.0), (-0.2, 0.95), (0.0, 0.9), (0.1, 0.8), (0.22, 0.72), (0.6, 0.72), (0.74, 0.76),
    (0.84, 0.8), (0.92, 0.85), (1.0, 0.9)]
HEAD_SCALE = 0.84
NECK_STRETCH = 1.1   # the asset's draft-horse neck is short: 10 % longer along its bone


def _profile(t):
    pts = LEG_PROFILE
    if t <= pts[0][0]:
        return pts[0][1]
    for (t0, k0), (t1, k1) in zip(pts[:-1], pts[1:]):
        if t <= t1:
            return k0 + (k1 - k0) * (t - t0) / (t1 - t0)
    return pts[-1][1]


def _refine_horse(body, arm, ear_scale=1.0, neck=None, head=None, leg_k=1.0, shin=1.0):
    """Slim the asset's legs (the rest mesh, before the rig deforms it): each
    leg vertex's horizontal offset from its leg's centre at that height is
    scaled by LEG_PROFILE, weighted by how much the leg's bones own it; and
    shrink the head about the poll by HEAD_SCALE (a pose scale on the head
    bone: the eyes, parented to it, follow) and lengthen the neck by
    NECK_STRETCH."""
    legs = {  # leg -> (its bones, the knee/hock bone whose head is t = 0)
        'fl': (('Bone_L', 'Bone_L.001', 'Bone_L.002'), 'Bone_L.002'),
        'fr': (('Bone_R', 'Bone_R.001', 'Bone_R.002'), 'Bone_R.002'),
        'hl': (('Bone_L.003', 'Bone_L.004', 'Bone_L.005'), 'Bone_L.005'),
        'hr': (('Bone_R.003', 'Bone_R.004', 'Bone_R.005'), 'Bone_R.005')}
    to_arm = arm.matrix_world.inverted() @ body.matrix_world
    from_arm = to_arm.inverted()
    gi = {g.name: g.index for g in body.vertex_groups}
    me = body.data
    co = [to_arm @ v.co for v in me.vertices]
    ground = min(c.z for c in co)
    owner = {}
    for v in me.vertices:
        ws = {g.group: g.weight for g in v.groups}
        best, bw = None, 0.0
        for leg, (bones, _) in legs.items():
            w = sum(ws.get(gi[b], 0.0) for b in bones if b in gi)
            if w > bw:
                best, bw = leg, w
        if best and bw > 0.05:
            owner[v.index] = (best, min(1.0, bw))
    for leg, (bones, knee_bone) in legs.items():
        kz = arm.data.bones[knee_bone].head_local.z
        idx = [i for i, (l_, _) in owner.items() if l_ == leg]
        if not idx:
            continue
        # the leg's centre line: the mean of its vertices in height bands
        band = 0.04
        bands = {}
        for i in idx:
            bands.setdefault(int(co[i].z / band), []).append(co[i])
        ctr = {k: (sum(p.x for p in ps) / len(ps), sum(p.y for p in ps) / len(ps)) for k, ps in bands.items()}
        for i in idx:
            c = co[i]
            t = (kz - c.z) / max(kz - ground, 1e-6)
            k = 1.0 + (_profile(t) - 1.0) * owner[i][1] * leg_k
            cx, cy = ctr[int(c.z / band)]
            me.vertices[i].co = from_arm @ Vector((cx + (c.x - cx) * k, cy + (c.y - cy) * k, c.z))
    if shin != 1.0:   # (the realism pass) shorter cannons and forearms: below the knees and hocks squeezed toward the
        # ground, everything above lowered to meet them (blended by how much a leg owns the vertex)
        kzs = {leg: arm.data.bones[kb].head_local.z for leg, (_, kb) in legs.items()}
        km = sum(kzs.values()) / len(kzs)
        for v in me.vertices:
            c = to_arm @ v.co
            o = owner.get(v.index)
            kz = kzs[o[0]] * o[1] + km * (1 - o[1]) if o else km
            z = ground + (c.z - ground) * shin if c.z < kz else c.z - (1 - shin) * (kz - ground)
            v.co = from_arm @ Vector((c.x, c.y, z))
    me.update()
    # the neck longer, the head and ears keeping their own (uniform) scale
    for b in ('Bone.002', 'Bone.001_L', 'Bone.001_R'):
        arm.data.bones[b].inherit_scale = 'NONE'
    arm.pose.bones['Bone.001'].scale = (1.0, neck or NECK_STRETCH, 1.0)
    pb = arm.pose.bones['Bone.002']
    hs = head or HEAD_SCALE
    pb.scale = (hs, hs, hs)
    for b in ('Bone.001_L', 'Bone.001_R'):
        arm.pose.bones[b].scale = (ear_scale,) * 3
    bpy.context.view_layer.update()


def horse_asset(root, colors, neck_raise=-38.0, head_flex=0.0, ear_scale=1.0, shape=None):
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
    shape = shape or {}
    s *= shape.get('size', 1.0)   # a unit's bigger horse (the Knight's destrier); the fit rescales the whole unit
    wide, leggy = shape.get('wide', 1.0), shape.get('leggy', LEGGY)   # a unit's broader, shorter-legged horse
    holder.scale = (s * 0.95 * wide, s * 0.95, s * leggy)
    holder.location = (xc * s * 0.95 * wide, yc * s * 0.95, -zmin * s * leggy)
    _raise_head(objs, neck_raise, by_name)
    _refine_horse(body, next(o for o in objs if o.type == 'ARMATURE'), ear_scale, shape.get('neck'), shape.get('head'),
                  shape.get('legs', 1.0), shape.get('shins', 1.0))
    if head_flex:   # the face brought toward the vertical at the poll (an arched, collected neck)
        hb = next(o for o in objs if o.type == 'ARMATURE').pose.bones['Bone.002']
        hb.rotation_mode = 'XYZ'
        hb.rotation_euler = (math.radians(head_flex), 0.0, 0.0)
        bpy.context.view_layer.update()
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


def _tube_folds(y0, y1, seed):
    """g(y) in -1..1 along a hanging cloth: irregular tube folds (ridges +1,
    valleys -1), 0.12-0.24 m apart, of uneven depth, as cloth hanging from a
    straight edge falls; seeded so each side of the horse differs."""
    import random
    rnd = random.Random(seed)
    ridges = []
    y = y0 + rnd.uniform(0.03, 0.1)
    while y < y1:
        ridges.append((y, rnd.uniform(0.05, 0.09), rnd.uniform(0.55, 1.0)))
        y += rnd.uniform(0.12, 0.24)

    def g(yy):
        v = 0.0
        for c, w, a in ridges:
            d = (yy - c) / w
            if abs(d) < 1.0:
                v = max(v, a * math.cos(0.5 * math.pi * d) ** 2)
        return 2.0 * v - 1.0
    return g


def drape(vs, y0, y1, hem, n=24, k=24, gap=0.035, flare=0.06, folds=0.018, zmin=0.85, style=None, seed=0, drop_n=1):
    """A cloth hanging over the horse's back from y0 to y1: each section an
    arc over the body (its measured half-width and top, `gap` off it) down to
    the widest point, then falling straight to the hem height (a number or a
    function of y), flaring out a little. Returns a bmesh.

    style='tubes' (the realism pass): instead of the even sine ripple, the
    cloth lies on the body near the top and falls into irregular tube folds
    (_tube_folds, different on each side) that deepen toward the hem, `folds`
    their depth there; the hem dips a little under each ridge. drop_n > 1
    splits the straight fall to the hem into that many faces."""
    ys = [y0 + (y1 - y0) * j / n for j in range(n + 1)]
    secs = _sections(vs, ys, zmin)
    rings = []
    gl, gr = (_tube_folds(y0, y1, seed), _tube_folds(y0, y1, seed + 101)) if style == 'tubes' else (None, None)
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
        if drop_n > 1:   # the straight fall from the widest point to the hem in drop_n faces (for painted bands)
            lo, hi = Vector(ring[0]), Vector(ring[1])
            ring[1:1] = [tuple(lo.lerp(hi, j / drop_n)) for j in range(1, drop_n)]
            lo, hi = Vector(ring[-1]), Vector(ring[-2])
            ring[-1:-1] = [tuple(lo.lerp(hi, j / drop_n)) for j in range(drop_n - 1, 0, -1)]
        if style == 'tubes':
            out = []
            for x, yy, z in ring:
                dep = min(1.0, max(0.0, (zc + 0.25 * hh - z) / max(zc + 0.25 * hh - zh, 1e-3))) ** 1.6
                f = (gl if x < 0 else gr)(y)
                out.append((x + math.copysign(folds * f * dep, x), yy, z - 0.012 * max(0.0, f) * (dep >= 0.999)))
            rings.append(out)
            continue
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


def _girth_arc(vs, z, front, sec, ys, reach=None):
    """Points round the horse's chest (front) or rump (not front) at height z,
    just off the body, from one side of the saddle to the other. reach: (the
    numpy-mounted pass) the farthest the band looks from the saddle (m), so a
    low head or the tail at that height isn't taken for the chest or rump."""
    w = sec[0]
    band = [v for v in vs if abs(v.z - z) < 0.05 and ((v.y > ys + 0.1) if front else (v.y < ys - 0.2))]
    if reach is not None:
        band = [v for v in band if abs(v.y - ys) < reach]
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
        arc = _girth_arc(vs, z, front, sec, ys, **({'reach': h['strap_reach'][0 if front else 1]} if h.get('strap_reach') else {}))
        if not arc:
            continue
        sag = h.get('strap_sag', 0.0)   # a strap hanging in a loose loop, lowest at the middle
        if sag:
            n = len(arc) - 1
            arc = [q + Vector((0, 0.3 * sag * math.sin(math.pi * i / n) if front else 0, -sag * math.sin(math.pi * i / n)))
                   for i, q in enumerate(arc)]
        p.add(props.tube(arc, h.get('strap_w', 0.026), seg=6, scale_xy=(1.0, 0.45)), key, smooth=True, tint=tint(key))
        if h.get('strap_studs') and (front or h.get('studs_rear')):   # brass fittings along the breast strap (and crupper)
            props.DEFAULTS.setdefault('brass', ('#a88a4a', 0.4, 1.0, 'metal'))
            sr = h.get('stud_r', 0.024)   # (the realism pass) bigger discs: phalerae that catch a highlight at 1x
            sk = h.get('stud_key', 'brass')   # (the realism pass) a brighter bronze for phalerae
            props.DEFAULTS.setdefault('phalera', ('#d6a852', 0.3, 1.0, 'polished'))
            for q in arc[1:-1:2] if sr < 0.03 else arc[2:-2:3]:
                p.add(props.ellipsoid(sr, sr, sr * 0.6, seg=10) if 'stud_r' in h else props.ellipsoid(0.024, 0.024, 0.024, seg=8), sk,
                      T(*(q + Vector((0, 0.012 if front else -0.012, 0)))), smooth=True)   # (the default studs as before ee6e2d5a)
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
        br = h.get('bedroll_r', 0.075)
        p.add(props.tube([(-0.26, ys - 0.34, top + 0.05), (0.26, ys - 0.34, top + 0.05)], br, seg=12), 'bedroll',
              smooth=True)
        for x in (-0.14, 0.14):
            p.add(props.lathe([(0.0, br + 0.007), (0.03, br + 0.007)], seg=12), 'leather_dark',
                  T(x, ys - 0.34, top + 0.05) @ R('Y', 90), smooth=True)
    if h.get('bow_case'):
        w = sec[0]
        m = T(-(w + 0.09), ys - 0.12, top - 0.22) @ R('X', -35)
        p.add(props.ellipsoid(0.035, 0.13, 0.3, seg=12), h['bow_case'] if isinstance(h['bow_case'], str) else 'trim', m, smooth=True)   # the gilded case (or a key: leather)
        p.add(props.tube([(0, 0.02, 0.2), (0, 0.06, 0.42)], 0.012, seg=6), 'wood', m, smooth=True)   # the bow's tip
        p.add(props.tube([(0, -0.1, 0.1), (0, -0.12, 0.3)], [0.05, 0.05], seg=8), 'leather', m, smooth=True)  # quiver
    for dy_, dz_ in h.get('cloth_discs', []):   # (the realism pass) a few pale discs on the cloth's flanks (Civ3's Sipahi spots)
        s_ = _sections(vs, [ys + dy_])[0] or sec
        for sx in (-1, 1):
            p.add(props.ellipsoid(0.11, 0.11, 0.014, seg=20), 'blanket2',
                  T(sx * (s_[0] + 0.07), ys + dy_, s_[3] + dz_) @ R('Y', sx * 90), smooth=True)
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


def _charge_decals(root, cloth, colors, key, panels, z_band, k, half_w, glow=0.35):
    """The arms' crosses as crisp decals: for each panel and flank, a finely
    gridded flat cross shrink-wrapped onto the simulated cloth, a few mm off
    it (painting them on the cloth's own faces smeared them)."""
    props.DEFAULTS.setdefault(key, ('#eeece6', 0.85, 0.0, 'cloth'))
    dp = vkit.part(root, 'charges', colors)

    def rect(sx, y0_, y1_, z0_, z1_, n=10):
        bm = bmesh.new()
        grid = [[bm.verts.new((sx * (half_w + 0.3), y0_ + (y1_ - y0_) * i / n, z0_ + (z1_ - z0_) * j / n))
                 for j in range(n + 1)] for i in range(n + 1)]
        for i in range(n):
            for j in range(n):
                bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
        return bm
    for a_, b_ in panels:
        yc, zc = 0.5 * (a_ + b_), z_band + 0.2 * k
        for sx in (-1, 1):
            dp.add(rect(sx, yc - 0.045 * k, yc + 0.045 * k, zc - 0.17 * k, zc + 0.2 * k), key, tint=key == 'tint')
            dp.add(rect(sx, yc - 0.15 * k, yc + 0.15 * k, zc + 0.015 * k, zc + 0.105 * k), key, tint=key == 'tint')
    obj = dp.finish()
    sw = obj.modifiers.new('wrap', 'SHRINKWRAP')
    sw.target = cloth
    sw.wrap_method = 'PROJECT'   # straight in across the flank, so the cross keeps its shape over the folds
    sw.use_project_x = True
    sw.use_negative_direction = True
    sw.use_positive_direction = True
    sw.wrap_mode = 'OUTSIDE_SURFACE'
    sw.offset = 0.007
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(obj.evaluated_get(dg))
    obj.modifiers.clear()
    obj.data = me
    bm = bmesh.new()   # drop the pieces whose rays missed the cloth (they stay out where they were made)
    bm.from_mesh(me)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if abs(v.co.x) > half_w + 0.22], context='VERTS')
    bm.to_mesh(me)
    bm.free()
    for m in obj.data.materials:   # the charges stay white in the shade, as painted spots do
        pr = props._principled(m) if m else None
        if pr:
            pr.inputs['Emission Color'].default_value = (0.9, 0.9, 0.88, 1.0)
            pr.inputs['Emission Strength'].default_value = glow   # glow: (numpy-mounted) 0 for dark charges (the Order's black cross)
    return obj


def _sim_cloth(obj, collider, pin, frames=50, stiffness=8.0, thickness=0.04, bending=2.0):
    """Cloth-simulate `obj` (a plain mesh) under gravity onto `collider`,
    the vertices where pin(world point) is true held fixed, then bake the
    last frame into its mesh (garments.drape does this for worn cloth; this
    one pins by position and keeps the cloth `thickness` off the collider, so
    it lies over the mail barding). Blender's cloth sim has crashed now and
    then (build.py): rerun the build if Blender dies here."""
    scene = bpy.context.scene
    col = collider.modifiers.new('Collision', 'COLLISION')
    collider.collision.thickness_outer = thickness
    collider.collision.cloth_friction = 3.0
    mw = obj.matrix_world
    g = obj.vertex_groups.new(name='pin')
    g.add([v.index for v in obj.data.vertices if pin(mw @ v.co)], 1.0, 'REPLACE')
    cl = obj.modifiers.new('Cloth', 'CLOTH')
    st = cl.settings
    st.vertex_group_mass = 'pin'
    st.quality = 12
    st.mass = 0.5                    # a heavy woollen caparison
    st.tension_stiffness = st.compression_stiffness = stiffness
    st.bending_stiffness = bending   # broad folds, not crumples
    st.air_damping = 1.5
    cl.collision_settings.use_self_collision = False
    cl.collision_settings.distance_min = 0.01
    cl.point_cache.frame_start, cl.point_cache.frame_end = 1, frames
    scene.frame_start, scene.frame_end = 1, frames
    for f in range(1, frames + 1):
        scene.frame_set(f)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(obj.evaluated_get(dg))
    obj.modifiers.clear()
    obj.data = me
    collider.modifiers.remove(col)
    scene.frame_set(0)
    bpy.context.view_layer.update()


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
    if h.get('blanket_wear') is not None:   # (the realism pass) an even felt: less grime and soot
        wear['blanket'] = h['blanket_wear']
    c = dict(colors)
    for k_src, k_dst in (('blanket_color', 'blanket'), ('blanket2', 'blanket2'), ('barding_steel', 'barding_mail'),
                         ('trim', 'trim'), ('saddle', 'leather'), ('bridle', 'bridle'), ('tack', 'leather'),
                         ('plume_color', 'plume'), ('pelt_color', 'pelt'), ('barding_dark', 'barding_dark'),
                         ('bedroll_color', 'bedroll'), ('sheepskin', 'fleece')):
        if h.get(k_src):
            c[k_dst] = h[k_src]
    for key, (col, rough, metal, kind) in {'blanket': ('#7e6650', 0.85, 0.0, 'cloth'), 'blanket2': ('#eeece6', 0.85, 0.0, 'cloth'),
                                           'barding_mail': ('#9a9ea6', 0.45, 1.0, 'metal'), 'trim': ('#2e2014', 0.6, 0.0, 'leather'),
                                           'bridle': ('#4a3424', 0.6, 0.0, 'leather'), 'plume': ('#eeece6', 0.9, 0.0, 'cloth'),
                                           'pelt': ('#3a2e26', 0.95, 0.0, 'rope'), 'buff_leather': ('#a8875a', 0.6, 0.0, 'leather'),
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
    trim = 'tint' if h.get('border_tint') else ('trim' if h.get('trim') else 'leather_dark')

    def hem_line(rings, key):
        wide = key == 'tint'   # a civ-colored trim reads as a band, not a thread
        for edge in ([r[0] for r in rings], [r[-1] for r in rings], rings[0], rings[-1]):
            p.add(props.tube(edge, 0.035 if wide else 0.014, seg=6, scale_xy=(1.0, 0.4) if wide else (1.0, 1.0)), key,
                  smooth=True, tint=wide)

    if blanket in ('saddle_cloth', 'pelt'):
        # deeper for a larger catalog cloth_drop (the old default was 0.24)
        frac = h.get('cloth_frac') or max(0.3, min(1.15, 0.75 * h.get('cloth_drop', 0.24) / 0.24))
        hem = sec[3] - frac * (sec[3] - sec[2])
        # 'cloth_back' reaches it back over the rump (Civ3's Rider)
        y0c, y1c = ys - h.get('cloth_back', 0.3), ys + h.get('cloth_front', 0.26)   # cloth_front: (the realism pass) up to the shoulders
        keys = _patchwork() if h.get('cloth_keys') == 'patchwork' else None
        rr, bw = h.get('cloth_round', 0.0), h.get('cloth_border', 0.0)
        if rr or bw:
            # a cut shabraque (the realism pass): the hem's corners rounded with radius 'cloth_round', and a
            # broad lace border 'cloth_border' wide in the trim color, painted round the cloth's edges
            pt = h.get('cloth_point', 0.0)   # the rear corner cut to a pointed tail hanging pt lower

            def hem_fn(y, hem=hem):
                if pt:
                    d = min(rr, y1c - y)
                    tail = pt * max(0.0, 1.0 - (y - y0c) / 0.3) ** 1.5
                    return hem - tail + (rr - math.sqrt(max(0.0, rr * rr - (rr - d) ** 2)) if rr else 0.0)
                d = min(rr, y - y0c, y1c - y)
                return hem + (rr - math.sqrt(max(0.0, rr * rr - (rr - d) ** 2)) if rr else 0.0)
            cloth, rings = drape(vs, y0c, y1c, hem_fn, n=40, k=36, folds=0.006 if blanket == 'pelt' else h.get('cloth_folds', 0.018),
                                 drop_n=6, **({'style': h['cloth_style']} if h.get('cloth_style') else {}))
            if bw:
                def keys(f, bw=bw):
                    c_ = f.calc_center_median()
                    bk = ('tint', True) if h.get('border_tint') else ('trim', False)   # border_tint: a civ border on a neutral cloth
                    return bk if (c_.z < hem_fn(c_.y) + bw or c_.y < y0c + bw or c_.y > y1c - bw) else None
        else:
            rg = h.get('pelt_ragged', 0.0)   # (the realism pass) a hide's soft irregular edge
            hem_r = (lambda y, hem=hem: hem + rg * (math.sin(y * 23.0) + 0.6 * math.sin(y * 51.0 + 1.0))) if rg else hem
            cloth, rings = drape(vs, y0c, y1c, hem_r, folds=h.get('cloth_folds', 0.006 if blanket == 'pelt' else 0.018))
        key = 'pelt' if blanket == 'pelt' else ('tint' if tinted else 'blanket')
        if key == 'blanket' and h.get('blanket_even'):   # (the realism pass) an even felt: no cloth mottle or stains
            props.DEFAULTS.setdefault('felt_even', (c.get('blanket', '#7e6650'), 0.9, 0.0, None))
            c['felt_even'] = c.get('blanket', '#7e6650')
            key = 'felt_even'
        p.add(cloth, key, smooth=True, tint=key == 'tint', keys=keys)
        if h.get('hem_line', True):
            hem_line(rings, trim)
        il = h.get('inner_line')
        if il and (rr or bw):   # (the realism pass) one thin gold line inside the border, along both sides
            for side, top_i in ((0, 6), (-1, -7)):
                pts = []
                for r_ in rings[3:-3]:
                    lo, hi = Vector(r_[side]), Vector(r_[top_i])
                    t_ = min(0.9, (bw + 0.035) / max(1e-3, hi.z - lo.z))
                    q = lo.lerp(hi, t_)
                    pts.append(q + Vector((0.012 if q.x > 0 else -0.012, 0, 0)))
                if len(pts) > 2:
                    p.add(props.tube(pts, 0.01, seg=6), il, smooth=True)
        if h.get('fringe'):   # (the realism pass) a leather fringe hanging from the hem on both sides
            props.DEFAULTS.setdefault('fringe', (h['fringe'], 0.8, 0.0, 'leather'))
            for r_ in rings[1:-1:2]:
                for e_ in (r_[0], r_[-1]):
                    e_ = Vector(e_)
                    p.add(props.tube([e_ + Vector((0, 0, 0.01)), e_ + Vector((0.01 if e_.x > 0 else -0.01, 0, -h.get('fringe_len', 0.07)))],
                                     0.007, seg=4), 'fringe', smooth=True)
    elif blanket in ('barding', 'dots'):
        # a long caparison to the knees over the whole body; for barding, mail beneath
        knee = 0.62
        if blanket == 'barding':
            mail_hem = (sec[2] - 0.02) if h.get('full_caparison') else knee - 0.06   # under a caparison: plate to the belly, legs bare
            b0, b1 = yh + 0.05 * (yf - yh), yf - 0.12 * (yf - yh)
            if h.get('barding_curve'):   # the hem rises toward the chest and the rump, so the trapper follows the body
                lo, mid, half = h['barding_curve'], (b0 + b1) / 2, (b1 - b0) / 2
                mail_hem = lambda y, lo=lo, mid=mid, half=half: lo + 0.45 * ((y - mid) / half) ** 2
            if h.get('cap_one'):
                mail_hem = sec[2] + 0.1   # hidden under the long trapper
            mail, mrings = drape(vs, b0, b1, mail_hem, gap=0.02, folds=0.012 if h.get('barding_curve') else 0.005,
                                 flare=0.0 if h.get('cap_sim') else 0.02 if h.get('barding_curve') else 0.06)   # (under simulated cloth: no flare to poke through)
            p.add(mail, 'barding_mail', smooth=True, keys=None if h.get('full_caparison') else _scales(h.get('scale_period', 0.09)))
            if h.get('full_caparison') and not h.get('cap_one'):   # plate barding: a few riveted lame lines, not a checker
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
        knee = sec[2] + h.get('cap_hem', 0.06)   # just below the belly line: the long legs show beneath
        y0, y1 = yh - 0.28, yf + 0.22   # from over the tail round the chest
        per = h.get('stripe_period', 0.62)

        band = h.get('cap_band', (0.1, 0.15))
        charge = h.get('cap_charge')   # the realism pass: the rider's arms (a pale cross) on each panel's flanks
        panels = ((y0, ys - h.get('cap_rear', 0.32)), (ys + h.get('cap_front', 0.12), y1))   # cap_front: where the chest panel starts

        def cap_keys(f, hem=knee, c_=None):
            c_ = c_ if c_ is not None else f.calc_center_median()
            if hem + band[0] < c_.z < hem + band[1]:            # the gold band above the hem (a civ band on a pale field)
                return ('tint', True) if h.get('cap_field') else ('trim', False)
            if charge and abs(c_.x) > 0.2 and not h.get('cap_decal'):
                for a_, b_ in panels:
                    k_ = h.get('cap_charge_size', 1.0)
                    yc, zc = 0.5 * (a_ + b_), hem + band[1] + 0.2 * k_
                    dy, dz = abs(c_.y - yc), c_.z - zc
                    if (dy < 0.045 * k_ and -0.17 * k_ < dz < 0.2 * k_) or (abs(dz - 0.06 * k_) < 0.045 * k_ and dy < 0.15 * k_):
                        return (charge, False)
                return None
            pal = h.get('cap_paly')   # (Knight pass 5) paly: broad vertical civ and pale panels (on a pale cap_field)
            if pal and ((c_.y - y0) % pal) < pal * h.get('cap_paly_civ', 0.5):   # the civ share of each period
                return ('tint', True)
            if abs(c_.x) > 0.2 and ((c_.y - y0) % per) < 0.035:   # the vertical gold stripes on the flanks, few and thin
                return ('trim', False)
            return None
        wavy = lambda y: knee + 0.06 * math.sin((y - y0) * 11.0)   # a scalloped, dagged hem
        tubes = h.get('cap_folds') == 'tubes'   # the realism pass: a straight hem, real tube folds
        # two panels, rump and chest, the silver barding showing between them across the mid-flank
        if h.get('cap_one'):
            # the realism pass, 4th round: one long trapper over chest, flanks and croup toward the hocks, a
            # sculpted static drape (tube folds deepening to an uneven hem; longer cloth tore in the sim)
            ya = y0 + h.get('cap_back_trim', 0.0)
            cap, crings = drape(vs, ya, y1, knee, n=110, k=34, gap=0.05, folds=h.get('cap_fold_depth', 0.07),
                                flare=h.get('cap_flare', 0.07), zmin=0.7, style='tubes', seed=11, drop_n=14)
            if h.get('cap_field'):   # (Knight pass 5) a pale field: the civ colour only on the border band and the charges
                props.DEFAULTS.setdefault('cap_field', (h['cap_field'], 0.85, 0.0, 'cloth'))
                c['cap_field'] = h['cap_field']
            fk = 'cap_field' if h.get('cap_field') else 'tint'
            p.add(cap, fk, smooth=True, tint=fk == 'tint', keys=cap_keys)
            hem_line(crings, 'trim')
            if h.get('breast_cloth', True):   # (Knight critic7: off; the projected breast cloth showed holes or a flat board)
                # the breast cloth: the trapper closed across the chest, from the base of the neck to the hem,
                # a grid projected back onto the horse's breast a little off it
                fw = crings[-1]
                xs_ = [q[0] for q in fw]
                w_ = max(abs(min(xs_)), max(xs_))
                ztop = max(q[2] for q in fw) - 0.3   # below the neck, where every ray finds the breast
                w_ *= 0.88
                bm_ = bmesh.new()
                nx, nz = 16, 18
                grid = [[bm_.verts.new((-w_ + 2 * w_ * i / nx, y1 + 0.8, knee + (ztop - knee) * j / nz)) for j in range(nz + 1)]
                        for i in range(nx + 1)]
                for i in range(nx):
                    for j in range(nz):
                        bm_.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
                me_ = bpy.data.meshes.new('breast')
                bm_.to_mesh(me_)
                bm_.free()
                bo = bpy.data.objects.new('breast', me_)
                bpy.context.collection.objects.link(bo)
                sw_ = bo.modifiers.new('wrap', 'SHRINKWRAP')
                sw_.target = body
                sw_.wrap_method = 'PROJECT'
                sw_.use_project_y = True
                sw_.use_negative_direction = True
                sw_.use_positive_direction = False
                sw_.offset = h.get('breast_offset', 0.06)   # (Knight critic7: 0.11, the horse's coat broke through at 0.06)
                dg_ = bpy.context.evaluated_depsgraph_get()
                bmb = bmesh.new()
                bmb.from_mesh(bpy.data.meshes.new_from_object(bo.evaluated_get(dg_)))
                bpy.data.objects.remove(bo, do_unlink=True)
                miss = [v for v in bmb.verts if v.co.y > y1 + 0.5]   # rays that missed
                if h.get('breast_fill') and len(miss) < len(bmb.verts):
                    # (Knight critic7) no holes: a missed vertex takes its nearest hit neighbour's depth (the coat showed
                    # through the holes as brown sawtooth patches)
                    hit = [v for v in bmb.verts if v.co.y <= y1 + 0.5]
                    for v in miss:
                        n_ = min(hit, key=lambda q: (q.co.x - v.co.x) ** 2 + (q.co.z - v.co.z) ** 2)
                        v.co.y = n_.co.y
                else:
                    bmesh.ops.delete(bmb, geom=miss, context='VERTS')
                p.add(bmb, fk, smooth=True, tint=fk == 'tint', keys=lambda f: cap_keys(f))
            if charge and h.get('cap_decal'):
                p_obj = None
                cp = vkit.part(root, 'caparison_ref', c)
                cp.add(drape(vs, ya, y1, knee, n=110, k=34, gap=0.05, folds=h.get('cap_fold_depth', 0.07), flare=0.07,
                             zmin=0.7, style='tubes', seed=11, drop_n=14)[0], 'tint')
                ref = cp.finish()
                pan = ((y0 + 0.1, ys - 0.25),) if h.get('cap_charges') == 'rear' else ((y0 + 0.1, ys - 0.25), (ys + 0.3, y1 - 0.1))
                _charge_decals(root, ref, c, charge, pan, knee + band[1],
                               h.get('cap_charge_size', 1.0), sec[0], glow=h.get('charge_glow', 0.35))   # cap_charges 'rear': one cross a side, on the rump
                bpy.data.objects.remove(ref, do_unlink=True)
        elif h.get('cap_sim'):
            # the realism pass: real draped cloth, simulated: each panel cut fuller toward the hem (so it has
            # cloth to fold), pinned along the spine and dropped onto the horse; painted band and charges
            cp = vkit.part(root, 'caparison', c, wear)
            rnd_ = __import__('random').Random(5)
            for i_, (a_, b_) in enumerate(panels):
                hem_j = [knee + rnd_.uniform(-0.05, 0.05) for _ in range(9)]
                def hem_f(y, a_=a_, b_=b_, hj=hem_j):   # an uneven hem, smoothly varying along the panel
                    u = min(7.999, max(0.0, 8 * (y - a_) / (b_ - a_)))
                    i = int(u)
                    return hj[i] + (hj[i + 1] - hj[i]) * (u - i)
                cap, crings = drape(vs, a_, b_, hem_f, n=50, k=30, gap=0.07, folds=0.0, flare=0.1, zmin=0.7, drop_n=12)
                yc, full = 0.5 * (a_ + b_), h.get('cap_fullness', 0.15)
                stretch = lambda z: 1.0 + full * max(0.0, min(1.0, (sec[1] - z) / max(sec[1] - knee, 1e-3))) ** 1.5
                for v in cap.verts:   # fullness: the lower cloth longer along the body, the hem wider than the top
                    v.co.y = yc + (v.co.y - yc) * stretch(v.co.z)

                def sim_keys(f, hf=hem_f, yc=yc, stretch=stretch):   # the band follows the uneven hem
                    c_ = f.calc_center_median()
                    u = Vector((c_.x, yc + (c_.y - yc) / stretch(c_.z), c_.z))   # as cut: the charges unstretched
                    return cap_keys(f, hf(u.y), u)
                cp.add(cap, 'tint', smooth=True, tint=True, keys=sim_keys)
            cobj = cp.finish()
            _sim_cloth(cobj, body, lambda q: abs(q.x) < 0.12, bending=h.get('cap_bending', 2.0),
                       thickness=0.045)   # clear of the mail barding
            if charge and h.get('cap_decal'):
                _charge_decals(root, cobj, c, charge, panels, knee + band[1], h.get('cap_charge_size', 1.0), sec[0],
                               glow=h.get('charge_glow', 0.35))
        for i_, (a_, b_) in enumerate(() if h.get('cap_sim') or h.get('cap_one') else panels):
            if tubes:
                cap, crings = drape(vs, a_, b_, knee, n=56, k=32, gap=0.05, folds=h.get('cap_fold_depth', 0.05), flare=0.05,
                                    zmin=0.7, style='tubes', seed=7 + 13 * i_, drop_n=10)
            else:
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
        if h.get('kura'):   # (numpy-mounted) a Japanese saddle: tall lacquered arches, no pommel or cantle
            import mdj_riders
            mdj_riders.kura(p, ys, top, h)
        else:
            p.add(props.ellipsoid(0.13, 0.05, 0.09, seg=12), 'leather', T(0, ys + 0.2, top + 0.11), smooth=True)   # pommel
        if h.get('saddle_horns'):   # (the realism pass) the Roman four-horned saddle: a horn at each corner of the seat
            for sx in (-1, 1):
                for sy, lean in ((0.2, 0.25), (-0.2, -0.2)):
                    b_ = Vector((sx * 0.12, ys + sy, top + 0.06))
                    hs = h.get('horn_scale', 1.0)   # (the realism pass) bigger, lighter horns that read at 1x
                    p.add(props.tube([b_, b_ + Vector((sx * 0.05 * hs, lean * 0.3 * hs, 0.17 * hs))], [0.045 * hs, 0.032 * hs], seg=8),
                          h.get('horn_key', 'leather'), smooth=True)
        if not h.get('kura'):
            p.add(props.ellipsoid(0.16, 0.05, 0.08, seg=12), 'leather', T(0, ys - 0.2, top + 0.1), smooth=True)    # cantle
        if h.get('sheepskin'):   # a fleece over the seat, its edges showing round the rider's thighs
            props.DEFAULTS.setdefault('fleece', ('#e4dcc8', 0.95, 0.0, 'rope'))
            fl = _shrink_patch('fleece', body, [-0.3 + 0.6 * i / 14 for i in range(15)],
                               [ys - 0.3 + 0.56 * j / 12 for j in range(13)], top, 0.085)
            p.add(fl, 'fleece', smooth=True)
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
                if h.get('chanfron_ears', True):
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
        front, frings = drape(vs, ys + 0.22, yf + 0.12, h.get('neck_cloth_hem', 0.95), gap=0.03, folds=0.012)
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
            if h.get('neck_cover') == 'cloth_sleeve' and h.get('sleeve_spots', True):
                props.bmesh.ops.subdivide_edges(sleeve, edges=sleeve.edges[:], cuts=4, use_grid_fill=True)
                _spot_discs(p, sleeve, outward=True)
            if h.get('sleeve_trim'):   # gold edges at both ends of the sleeve (Egyptian chariot horses)
                for rg in (rings[0], rings[-1]):
                    p.add(props.tube(rg + rg[:1], 0.022, seg=6), 'trim', smooth=True)
            cloth_cover = h.get('neck_cover') in ('cloth', 'cloth_sleeve')
            p.add(sleeve, 'tint' if cloth_cover else 'barding_mail', smooth=True, tint=cloth_cover,
                  keys=None if cloth_cover else _scales(h.get('scale_period', 0.09)))
    if h.get('plume'):
        ctr = a.lerp(b, 0.08) + Vector((0, 0, 0.12))
        big = 2.2 if h.get('plume') == 'fan' else 1.3   # a tall fan of plumes (Egyptian chariot horses)
        for dx in ((-0.09, -0.045, 0.0, 0.045, 0.09) if big > 1 else (-0.025, 0.0, 0.025)):   # a fan of separate feathers
            p.add(props.tube([ctr + Vector((dx, 0, 0)), ctr + Vector((dx * 1.5, -0.04, 0.16 * big)),
                              ctr + Vector((dx * 2.5, -0.1, 0.24 * big))], [0.045, 0.05, 0.02], seg=8), 'tint' if h.get('plume_tint', True) else 'plume',
                  smooth=True, tint=h.get('plume_tint', True))
    nose = _head_ring(vs, a, b, 0.66, grow=0.004)
    if nose and h.get('reins') != 'hand':   # ('hand': drawn to the rider's fist by reins_to_hand)
        bold = h.get('reins_bold')   # Civ3 draws some reins as thick civ-colored loops
        for side in (6, 18):   # reins from the bit rings at the noseband's sides to the pommel
            q = nose[side]
            p.add(props.tube([q, q.lerp(Vector((0, ys + 0.25, top + 0.15)), 0.5) + Vector((0, 0, -h.get('rein_drop', 0.14) if bold else -0.08)),
                              Vector((0.06 if side == 6 else -0.06, ys + 0.25, top + 0.12))], 0.024 if bold else 0.014,
                             seg=6), 'tint' if bold else 'leather', smooth=True, tint=bool(bold))
    _extra_tack(p, h, vs, arm, a, b, ys, top, sec)
    if h.get('mane_trim'):   # a short trimmed mane: the asset's spiky hair cards off, a smooth roll along the crest
        for o in list(root.children_recursive):
            if o.name == 'BezierCurve':
                bpy.data.objects.remove(o, do_unlink=True)
        nb = arm.pose.bones['Bone.001']
        n0, n1 = arm.matrix_world @ nb.head, arm.matrix_world @ nb.tail
        crest = []
        for t_ in [0.04 + 0.09 * i for i in range(11)]:
            r = _head_ring(vs, n0, n1, t_, grow=0.0)
            if r:
                crest.append(max(r, key=lambda q: q.z))
        if len(crest) > 2:
            props.DEFAULTS.setdefault('mane', ('#1e1610', 0.75, 0.0, 'rope'))
            mw_ = h.get('mane_shag', 0.0)   # (the realism pass) a shaggy mane: a fuller roll falling to one side, in tufts
            if mw_:
                crest = [q + Vector((0.025 * mw_ * (1 if i % 2 else 0.6), 0.0, -0.02 * mw_)) for i, q in enumerate(crest)]
            p.add(props.tube(crest, [r_ * (1 + mw_) for r_ in [0.05] + [0.045] * (len(crest) - 2) + [0.03]], seg=10,
                             scale_xy=(0.55 + 0.25 * mw_, 1.0 + 0.4 * mw_)), 'mane', smooth=True)
    if h.get('blaze'):   # a pale blaze down the front of the face
        props.DEFAULTS.setdefault('blaze', (h['blaze'], 0.6, 0.0, 'leather'))
        c.setdefault('blaze', h['blaze'])
        tops = []
        for t_ in (0.2, 0.32, 0.44, 0.56, 0.68, 0.8):
            r = _head_ring(vs, a, b, t_, grow=0.006)
            if r:
                tops.append(max(r, key=lambda q: q.z))
        if len(tops) > 2:
            p.add(props.tube(tops, [0.03, 0.035, 0.03, 0.028, 0.03, 0.026][:len(tops)], seg=8, scale_xy=(1.0, 0.25)), 'blaze',
                  smooth=True)
    if h.get('girth'):   # the girth under the belly, from the saddle cloth's hem round to the other side
        gy = ys + 0.08
        g = _sections(vs, [gy])[0] or sec
        w_, top_, bel_, zc_ = g
        arc = [Vector(((w_ + 0.02) * math.cos(a), gy, zc_ + (zc_ - bel_ + 0.02) * math.sin(a)))
               for a in [math.radians(-15 - 150 * i / 12) for i in range(13)]]
        p.add(props.tube(arc, 0.04, seg=6, scale_xy=(0.35, 1.0)), 'leather_dark', smooth=True)
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


def reins_to_hand(root, rider_arm, color):
    """The reins from the bit rings (the noseband's sides) to the posed
    rider's left fist, sagging a little (the realism pass; the tack's own
    reins end at the pommel, so a unit using this sets 'reins': 'hand')."""
    harm = next(o for o in root.children_recursive if o.type == 'ARMATURE' and 'Bone.002' in o.pose.bones)
    body = next(o for o in root.children_recursive if o.type == 'MESH'
                and any(m.type == 'ARMATURE' and m.object == harm for m in o.modifiers))
    hb = harm.pose.bones['Bone.002']
    nose = _head_ring(_world_verts(body), harm.matrix_world @ hb.head, harm.matrix_world @ hb.tail, 0.66, grow=0.004)
    if not nose:
        return
    pb = rider_arm.pose.bones['hand_l']
    fist = (rider_arm.matrix_world @ pb.head).lerp(rider_arm.matrix_world @ pb.tail, 0.8)
    p = vkit.part(root, 'reins', {'reins': color}, {'reins': {'grime': 0.4}})
    props.DEFAULTS.setdefault('reins', ('#1e1a18', 0.6, 0.0, 'leather'))
    for side in (6, 18):
        q = nose[side]
        mid = q.lerp(fist, 0.5) + Vector((0.0, 0.0, -0.07))
        p.add(props.tube([q, mid, fist], 0.013, seg=6), 'reins', smooth=True)
    p.finish()


def seat_rider(root, unit, entry):
    """Seat the unit's rider (the catalog entry's 'rider', with remaster.py's
    changes) on the 'rider' empty: units.build_footman(seated=True) puts the
    pelvis on its root's origin, so the root is parented to the empty, scaled
    by the entry's rider_scale. Needs MPFB (see README.md)."""
    import addon_utils
    addon_utils.enable('bl_ext.user_default.mpfb', default_set=True)
    if unit in MDJ_RIDERS:   # (numpy-mounted) the o_kabuto hat, the yumi and ebira props, the MDJ poses
        import mdj_riders  # noqa: F401
    if unit in LEAGUE_RIDERS:   # (numpy-mounted) the slouch hat
        import league_riders  # noqa: F401
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
    k = RIDER_SCALE_BY_UNIT.get(unit, entry.get('rider_scale', 1.0)) * RIDER_SCALE
    rroot.scale = (k, k, k)
    bpy.context.view_layer.update()
    # aim the weapon as Civ3's riders carry it (the riding stance holds it low across)
    arm = next(o for o in rroot.children_recursive if o.type == 'ARMATURE')
    # sit down into the saddle: the scaled root lifts the pelvis (k - 1) x its height off the seat
    # (the riders floated 0.15-0.35 m high); put the hip joints SEAT_PELVIS above the seat, for every rider
    pel = arm.matrix_world @ arm.pose.bones['pelvis'].head
    want = seat.matrix_world.translation + Vector((0.0, 0.0, SEAT_PELVIS_BY_UNIT.get(unit, SEAT_PELVIS) * k))
    rroot.location += seat.matrix_world.to_3x3().inverted() @ (want - pel)
    bpy.context.view_layer.update()
    if unit in RIDE_POSE_BY_UNIT:   # the unit's own riding seat (riders.RIDE_POSES), over the shared 'ride' stance
        riders.ride_pose(arm, RIDE_POSE_BY_UNIT[unit])
        if unit in BACK_CARBINE:   # a carbine slung diagonally across the back (it shows from behind)
            riders.sling_carbine(arm, BACK_CARBINE[unit])
        if unit in PELISSE:   # a hussar's fur-trimmed pelisse slung over the left shoulder
            pe = PELISSE[unit]
            if pe.get('cape'):   # the entry's cape (remaster: cape, hem_bands) swung onto the left shoulder
                riders.sling_cape(arm, pe.get('yaw', -30.0), pe.get('roll', 14.0), pe.get('shift', (0.0, 0.0, 0.0)),
                                  sleeve=dict(pe['sleeve'], k=k) if pe.get('sleeve') else None)
            else:
                riders.pelisse(arm, k, pe)
        if unit in BASHLYK:   # a Cossack's hood on the back, its tails over the shoulders (riders.bashlyk)
            riders.bashlyk(arm, k, BASHLYK[unit])
        if unit in GAZYRI:   # a cherkeska's cartridge rows on the chest (riders.gazyri)
            riders.gazyri(arm, k, GAZYRI[unit])
        if unit in COAT_SKIRT:   # a long coat's skirt over the flanks (riders.coat_skirt)
            riders.coat_skirt(arm, k, COAT_SKIRT[unit])
        if unit in LEG_HARNESS:
            riders.leg_harness(arm, k)
        if unit in STIRRUPS:
            riders.stirrups(root, arm, seat.matrix_world.translation, k)
        if unit in ABUMI:   # (numpy-mounted) Japanese slipper stirrups
            import mdj_riders
            mdj_riders.abumi(root, arm, seat.matrix_world.translation, k, ABUMI[unit])
        if unit in O_YOROI:   # (numpy-mounted) the o-yoroi's sode and kusazuri
            import mdj_riders
            mdj_riders.o_yoroi(arm, k, O_YOROI[unit])
        if unit in SCABBARD_UP:   # the empty scabbard re-hung as a diagonal down and back along the flank
            sc = next((o for o in bpy.data.objects if o.name.split('.')[0] == 'sheathed_sword' and o.parent is not None), None)
            if sc is not None:
                H.hold(sc, arm, 'pelvis', sc.matrix_world.translation.copy(), up=SCABBARD_UP[unit], forward=(-1.0, 0.0, 0.0))
        if unit in SPARE_JAVELINS:   # spares held upright behind the shield, on the left forearm
            sp_ = props.make('javelin_bundle', {}, n=SPARE_JAVELINS[unit], length=SPARE_LEN.get(unit, 1.5), shaft_r=0.022, flat=0.8, tip=SPARE_TIP.get(unit, 0.012))
            pb_ = arm.pose.bones['lowerarm_l']
            mid_ = (arm.matrix_world @ pb_.head).lerp(arm.matrix_world @ pb_.tail, 0.6)
            H.hold(sp_, arm, 'lowerarm_l', mid_ + Vector((-0.08 * k, -0.05 * k, 0.0)), up=(-0.15, 0.35, 1.0), forward=(-1.0, 0.0, 0.0))
            mark_poles(unit, 'javelin_bundle')
        if unit in BOW_DRAW:   # the bow upright in the extended left hand, its belly toward the target
            bd = BOW_DRAW[unit]
            bw = next((o for o in bpy.data.objects if o.name.split('.')[0] == bd.get('prop', 'bow') and o.parent is not None), None)   # 'prop': (numpy-mounted) the yumi
            if bw is not None:
                if unit in OFFHAND_NO_FIT:   # (numpy-mounted) a tall bow left out of the fit (FIT_SCALE sizes the rest)
                    bw['no_fit'] = True
                gp = H.grip_point(arm, 'hand_l', along=0.35)
                H.hold(bw, arm, 'hand_l', gp, up=bd['up'], forward=bd['forward'])
                if bd.get('arrow'):   # a dark arrow nocked from the drawing hand past the bow's grip
                    hp = H.grip_point(arm, 'hand_r')
                    d_ = (gp - hp).normalized()
                    ap = vkit.part(root, 'arrow', {'arrow': bd['arrow']})
                    props.DEFAULTS.setdefault('arrow', (bd['arrow'], 0.6, 0.0, 'leather'))
                    ap.add(props.tube([hp - d_ * 0.03, gp + d_ * 0.2], 0.02 * k, seg=6), 'arrow', smooth=True)   # (thick enough to stay one line at 2x)
                    ap.add(props.lathe([(0.0, 0.022 * k), (0.07 * k, 0.0)], seg=6), 'steel',
                           Matrix.Translation(gp + d_ * 0.2) @ d_.to_track_quat('Z', 'Y').to_matrix().to_4x4(), smooth=True)
                    if bd.get('fletch'):   # (the realism pass) pale fletching at the nock: two crossed vanes
                        props.DEFAULTS.setdefault('fletch', ('#e6e0ce', 0.8, 0.0, None))
                        ap.add(props.tube([hp, hp + d_ * 0.13 * k], [0.012 * k, 0.03 * k], seg=4, scale_xy=(1.0, 0.2)), 'fletch', smooth=True)
                        ap.add(props.tube([hp, hp + d_ * 0.13 * k], [0.012 * k, 0.03 * k], seg=4, scale_xy=(0.2, 1.0)), 'fletch', smooth=True)
                    ao = ap.finish()
                    w_ = ao.matrix_world.copy()
                    ao.parent = arm
                    ao.parent_type = 'BONE'
                    ao.parent_bone = 'hand_r'
                    ao.matrix_world = w_
        if TACK_OVERRIDES.get(unit, {}).get('reins') == 'hand':
            reins_to_hand(root, arm, TACK_OVERRIDES[unit].get('bridle', '#1e1a18'))
        om = r_entry.get('offhand_mesh') or (r_entry.get('offhand'), {})
        if om[0] and om[0].startswith('shield') and r_entry.get('shield_at') != 'back':
            sh = next((o for o in bpy.data.objects if o.name.split('.')[0] == om[0] and o.parent is not None), None)
            riders.place_shield(arm, sh, RIDE_POSE_BY_UNIT[unit])
    riders.headgear(r_entry, arm, front_fix=HAT_PITCH_BY_UNIT.get(unit, 'level') if unit not in HEADGEAR_BACK_TO_FRONT else False)
    weapon = r_entry.get('weapon')
    up = WEAPON_UP_BY_UNIT.get(unit) or RIDER_WEAPON_UP.get(weapon)   # a unit's own carry overrides the weapon's
    if up:
        obj = next((o for o in bpy.data.objects if o.name.split('.')[0] == weapon and o.parent is not None), None)
        if obj is not None:
            H.hold(obj, arm, 'hand_r', H.grip_point(arm, 'hand_r'), up=up, **({'forward': WEAPON_FWD_BY_UNIT[unit]}
                                                                          if unit in WEAPON_FWD_BY_UNIT else {}))
            bpy.context.view_layer.update()
    mark_poles(unit, weapon)
    return rroot


def mark_poles(unit, weapon):
    """Leave a long pole weapon (POLE_WEAPONS, or the unit's WEAPON_NO_FIT) out of render_vehicles' fit
    ('no_fit'), so the horse and rider are sized like the stock's (the art lead, 2026-11-23: a pika shrank the
    Cossack 26 %). Sabres and carbines stay in."""
    if weapon in POLE_WEAPONS or unit in WEAPON_NO_FIT:
        for o in bpy.data.objects:
            if o.name.split('.')[0] == weapon and o.parent is not None:
                o['no_fit'] = True


# The old catalog's rider_scale was tuned for the old, smaller horse.
RIDER_SCALE = 1.26
# A unit's rider scale over its catalog rider_scale (the realism pass: the Cavalry's rider read small).
# (the family pass: the Cavalry's and Hussar's 1.0 dropped back to the catalog, the stock ratio; the Knight's
# rider grown with its destrier (HORSE_SHAPE size 1.14): 0.99 x 1.14, the stock Knight's rider-to-horse ratio)
RIDER_SCALE_BY_UNIT = {'Knight': 1.129, 'Mounted Warrior': 1.1, 'Rider': 1.11, 'Ancient Cavalry': 1.17}   # (Mounted Warrior C2: the rider read small on the pony)
# Every rider wears its headgear facing forward (riders.headgear's front_fix, at HAT_PITCH_BY_UNIT's pitch
# or riders.HAT_PITCH); a unit listed here keeps the old back-to-front placement (none since the roll-out).
HEADGEAR_BACK_TO_FRONT = set()
# Units seated in their own riders.RIDE_POSES pose; the others keep units.STANCES['ride'].
# Every rider's pelvis (the hip joints) sits SEAT_PELVIS (the rider's meters, before
# rider scale) above the saddle's seat point.
SEAT_PELVIS = 0.1
SEAT_PELVIS_BY_UNIT = {'Knight': 0.15}   # the knight sits up in a high war saddle
HAT_PITCH_BY_UNIT = {'Cavalry': -6.0, 'Knight': True}   # (Cavalry pass 8: pass 6's -6; pass 4 was -2)   # True: riders.HAT_PITCH; the others 'level'
BACK_CARBINE = {'Cossack': {'wood': '#6a4426', 'steel': '#34363a', 'length': 1.45, 'scale': 1.6, 'drop': 0.14},   # (critic7: a gunmetal barrel, the stock showing, lower)   # stock and muzzle past the shoulders
                'Cavalry': {'wood': '#4a3020', 'steel': '#2a2c30', 'length': 1.0, 'scale': 1.4}}   # (pass 8 = pass 6's; pass 4: '#3a2618', 1.15, 1.2)
PELISSE = {'Ancient Cavalry': {'cape': True, 'yaw': -32.0, 'roll': 14.0},   # (C3) the sagum pinned off one shoulder
           'Horseman': {'cape': True, 'yaw': -30.0, 'roll': 18.0},   # the chlamys pinned at one shoulder
           'Hussar': {'cape': True, 'yaw': -42.0, 'roll': 10.0, 'shift': (-0.03, 0.0, 0.0),
                      'sleeve': {'shade': 0.62, 'fur': '#3a2c22'}}}   # riders.sling_cape / riders.pelisse (the realism pass)
GAZYRI = {'Cossack': {'n': 6, 'below': 0.13, 'drop': 0.17, 'x0': 0.115, 'x1': 0.155, 'r': 0.036, 'depth': 0.2,
                       'ivory': '#f6f0e2', 'pocket': True}}   # outside the beshmet
BASHLYK = {'Cossack': {'width': 0.15, 'length': 0.36, 'tail': 0.36}}   # riders.bashlyk (the realism pass)   # riders.gazyri (the realism pass)
COAT_SKIRT = {'Ansar Warrior': {'color': '#e8e4dc', 'length': 0.5, 'out': 0.5, 'a0': 30, 'a1': 165, 'fold': 0.14, 'trim': '#9a9488', 'trim_r': 0.035},   # (batch 2) the robe's skirts in folds
              'Sipahi': {'tint': True, 'length': 0.45, 'out': 0.5, 'a0': 35, 'a1': 165, 'fold': 0.07, 'trim': '#c4a462', 'trim_r': 0.04, 'edges': True},   # the kaftan's skirts
              'Keshik': {'tint': True, 'length': 0.56, 'out': 0.55, 'a0': 10, 'a1': 165, 'fold': 0.04, 'trim': '#3a2a1e', 'trim_r': 0.03},   # the deel's skirts
              'Rider': {'tint': True, 'length': 0.42, 'out': 0.48, 'a0': 55, 'a1': 165, 'fold': 0.09, 'trim': '#2a1e14', 'trim_r': 0.065, 'edges': True},   # the long coat's skirts
              'Cossack': {'color': '#6e5c4a', 'length': 0.55, 'out': 0.5, 'a0': 80, 'a1': 150, 'fold': 0.06, 'trim': 'tint', 'trim_r': 0.08, 'edges': True}}   # riders.coat_skirt (the realism pass)
# Bows held upright in the drawing hand (the unit facing +y, his left -x): 'up' (the bow's limbs), 'forward' (its back,
# toward the target).
BOW_DRAW = {'Keshik': {'up': (-0.36, -0.1, 1.0), 'forward': (-0.82, -0.57, 0.0), 'arrow': '#c8b48a', 'fletch': True},   # (C3) turned to show its arc in S; a pale fletched arrow   # the backward shot; (C2) canted 20 degrees
            'Mounted Warrior': {'up': (-0.06, 0.22, 1.0), 'forward': (-0.8, 0.6, 0.0), 'arrow': '#d2bc8c', 'fletch': True}}   # (C5) a pale cane shaft   # (C2) more side-on, an arrow nocked; (C4) the upper limb out and forward, clear of the bonnet
SPARE_JAVELINS = {'Ancient Cavalry': 3}   # (C3) a bundle behind the shield
SPARE_TIP = {'Ancient Cavalry': 0.024}
SPARE_LEN = {'Ancient Cavalry': 1.2}   # (C6) shorter: the heads stay behind the shield, off the horse's ears   # (C5) blunter heads: one piece edge-on in E
LEG_HARNESS = set()                       # riders.leg_harness: poleyns and greaves over mail (tried on the Knight, pass 4)
STIRRUPS = {'Cavalry', 'Hussar', 'Cossack', 'Rider', 'Keshik', 'Conquistador'}          # riders.stirrups: irons and leathers to the posed feet
SCABBARD_UP = {'Cavalry': (0.1, -0.55, -0.83), 'Cossack': (0.3, -0.75, -0.55)}   # the scabbard's run (world, the unit facing +y)     # less forward pitch: the helmet's peak shaded the face
# A unit's own head carriage (the shared horse carries its neck raised 38 degrees, head_flex 0).
NECK_RAISE_BY_UNIT = {'Keshik': -4.0, 'Ancient Cavalry': -38.0, 'Cavalry': -22.0, 'Knight': -30.0, 'Hussar': -18.0, 'Cossack': -36.0}
HEAD_FLEX_BY_UNIT = {'Ancient Cavalry': 20.0, 'Cavalry': 18.0, 'Knight': 14.0, 'Hussar': 22.0, 'Cossack': 18.0}
# A unit's horse build: 'size' (overall), 'neck' (stretch, over NECK_STRETCH), 'head' (over HEAD_SCALE),
# 'legs' (how much of LEG_PROFILE's slimming applies: < 1 heavier legs).
HORSE_SHAPE_BY_UNIT = {'Knight': {'size': 1.14, 'neck': 1.2, 'head': 0.9, 'legs': 0.75},
                       'Hussar': {'size': 1.1, 'wide': 1.1, 'leggy': 1.05, 'legs': 0.65, 'neck': 1.2},   # a light troop horse, not a greyhound
                       'Cossack': {'leggy': 1.0},
                       'Keshik': {'size': 0.94, 'leggy': 0.9, 'wide': 1.12, 'legs': 0.7, 'head': 1.12, 'shins': 0.85, 'neck': 0.92},   # (C3) shorter cannons, a shorter neck   # a stocky steppe pony; (C2) shorter, heavier legs, a heavier head
                       'Ancient Cavalry': {'neck': 1.1, 'head': 1.12},   # (C4) a longer arched neck, a larger head;   # (C3) a shorter, thicker-looking neck   # (C4) a smaller ancient horse, shorter legs: the rider (stock scale) reads right
                       # (pass 8: pass 6's broader troop horse on the family legs; pass 4 had none)
                       'Cavalry': {'wide': 1.1, 'leggy': 1.0, 'neck': 1.12, 'head': 0.9}}   # (critic9: neck 1.12 from 1.22, a finer head 0.9)   # a compact Don horse: short legs (the family LEG_PROFILE covers the cannons)
                       # (Hussar pass 6, 0e792887, tried wide 1.18, leggy 1.0, legs 0.55, coat_contrast 1.3: it scored lower)
                       # a stockier troop horse beside the stock Hussar's and Cossack's: broader barrel and
                       # chest and quarters ('wide', across), shorter legs ('leggy' 1.0 not 1.07), heavier cannons
# (pass 5-6 tried the Cavalry at {'wide': 1.1, 'leggy': 1.0, 'legs': 0.72, 'neck': 1.22}: 10589213)
EAR_SCALE_BY_UNIT = {'Knight': 0.75, 'Hussar': 0.8, 'Cossack': 0.35, 'Cavalry': 0.7}   # (Hussar: from behind they showed beside the rider's head)   # small ears under the chanfron: from behind they stood up beside the helm like horns
RIDE_POSE_BY_UNIT = {'Ansar Warrior': 'ansar', 'Sipahi': 'sipahi', 'Keshik': 'bow_back', 'Rider': 'rider', 'Mounted Warrior': 'bow_draw', 'Horseman': 'horseman', 'Ancient Cavalry': 'ancient_cav', 'Cavalry': 'cavalry', 'Knight': 'knight_rest', 'Hussar': 'hussar', 'Cossack': 'cossack'}
# How each rider weapon is carried (the weapon's +Z in the rider's space: +y forward, +z up).
RIDER_WEAPON_UP = {
    'cav_spear': (0.0, 0.62, 1.0),   # couched forward and up at a diagonal, as Civ3's Horseman
    'javelin': (0.0, 0.5, 1.0),      # raised, ready to throw
    'lance': (0.0, 1.0, 0.18),       # couched level and forward
    'sabre': (0.15, 0.35, 1.0),      # drawn and held up
    'long_sword': (0.15, 0.3, 1.0),
    'rifle': (0.0, 0.25, 1.0),       # held upright at the hip, as Civ3's Cossack and Cavalry
}
WEAPON_UP_BY_UNIT = {'Ansar Warrior': (0.3, 0.75, 0.55),   # (batch 3) forward, up and out, clear of the body
                     'Conquistador': (0.05, 0.12, 1.0),   # the lance held upright (the original)
                     'Horseman': (0.5, 1.0, 0.08),
                     'Sipahi': (0.45, 0.4, 0.9),   # (round 4) out to the right and forward, clear of the helmet in 4   # (the realism pass) the kilij up and back; (batch 3) out, off the helmet
                     'Rider': (0.5, -0.35, 0.9),   # (C5) up and back over the raised arm, clear of the head and ears   # (C4: (0.65, -0.08, 0.9))
                     # (Rider C3: (0.55, 0.12, 0.95))   # (the realism pass) the dao raised up and out, clear of the horse's head (C2)   # (critic2) underarm at the hip, forward along the horse, never across the head
                     'Ancient Cavalry': (0.25, 0.9, -0.35),   # (C6) overarm at the shoulder, forward and a little down   # (C5: (0.15, 0.9, 0.3)) cocked overhead to throw, forward   # (C4: (0.3, 0.7, 0.75))
   # (the realism pass) the javelin at the ready, forward and up, out to the right        # (the realism pass) overhand, forward and a little down
                     'Knight': (0.06, 0.8, 0.8),   # (critic7) sloped forward 45 degrees, the point ahead of the head;          # (pass 5) upright at rest, one hold in every view (pass 3-4: (-0.32, 1.0, 0.45), raised diagonal)
                     'Cavalry': (0.42, 0.22, 0.88),      # up and out (pass 8 = pass 6; pass 4: (0.5, 0.68, 0.55), forward and out)         # the sabre raised forward, the charge (it reads at 1x from every side)
                     # (the realism pass) the pika at the ready, forward and up, a little out to the right
                     # (before: the rifle low across the saddle, (-1.0, 0.1, -0.3))
                     'Cossack': (0.45, 0.5, 1.25),   # (C9: steeper, so its head clears the rider in SE; C8's (0.45, 0.85, 1) was a stub there)   # (critic6: tilted out, clear of the horse's head in S)
                     # forward and out to the right, nearly level (a blade pointing away from the camera reads as
                     # rising: the flatter it is, the lower its tip in every view)
                     'Hussar': (0.6, 0.78, 0.1)}
# Units whose held weapon is left out of the fit (render_vehicles.screen_points: 'no_fit'), so a long lance doesn't
# shrink the horse and rider against the stock (the realism pass).
# Pole weapons are left out of the fit by default (mark_poles): measured, the pika shrank the Cossack 26 %, the
# lance the Knight 5 %; the Hussar's and Cavalry's sabres only 2 % (they stay in).
POLE_WEAPONS = {'lance', 'cav_spear', 'spear', 'pike', 'javelin', 'javelin_bundle', 'glaive', 'halberd', 'standard', 'banner',
                'staff_banner'}
WEAPON_NO_FIT = set()   # a unit's other weapon left out of the fit
# A unit's weapon roll: the prop's +Y (a blade's edge) toward this (world, the unit facing +y); default (0, -1, 0).
# The Hussar's raised sabre turned 45 degrees: its flat shows in six directions, edge-on only in two.
WEAPON_FWD_BY_UNIT = {'Sipahi': (0.3, -0.95, 0.0), 'Rider': (0.7, -0.7, 0.0), 'Hussar': (0.7, -0.7, 0.0), 'Cavalry': (0.7, -0.7, 0.0)}   # (Cavalry critic9: edge-on it broke into specks in S and N)
RIDER_DEFAULTS = {
    'neck': 'high',
    'pose_extra': [('spine_01', (0, 0.12, 1)), ('spine_02', (0, 0.16, 1)), ('spine_03', (0, 0.14, 1)),
                   ('neck_01', (0, 0.04, 1)), ('head', (0, -0.04, 1))],
    'grime': 0.55,
}


DOG_BY_UNIT = {'Conquistador': {'real': True, 'place': (1.2, 0.25, 0.0), 'scale': 1.7}}   # (batch 1: in W it passed through the horse)
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
    h = dict(h, **{k: v for k, v in over.items() if k in ('dapple', 'coat2', 'blanket_tint', 'coat_spots', 'tail_tip',
                                                          'coat_contrast')})
    h.update(TACK_OVERRIDES.get(unit, {}))
    root = vkit.root(unit)
    body = horse_asset(root, dict(h, **c), neck_raise=NECK_RAISE_BY_UNIT.get(unit, -38.0),
                       head_flex=HEAD_FLEX_BY_UNIT.get(unit, 0.0), ear_scale=EAR_SCALE_BY_UNIT.get(unit, 1.0),
                       shape=HORSE_SHAPE_BY_UNIT.get(unit))
    seat = asset_tack(root, body, c, h)
    e = vkit.crew(root, 0, seat, yaw=0.0)
    e.name = 'rider'
    return root


# The Horseman's coat as reviewed (a warm bay); the other catalog coats are used as they are.
HORSE_COAT_OVERRIDES = {
    # coats read from the Civ3 originals (the old catalog's were tuned for the old renderer)
    'Horseman': {'coat': '#8a5430', 'mane': '#140e0a', 'points': '#1e1610'},   # a bay
    # a dun (not a pinto), with civ war paint on the shoulder and the hindquarter
    'Mounted Warrior': {'coat': '#b08a58', 'coat2': None, 'mane': '#2e241a', 'points': '#5a4632',
                        # (the asset horse stands ~1.9 m at the croup: the hip is near z 1.5)
                        'coat_contrast': 0.8,   # (C5) less coat noise
                        'coat_spots': {'pale': [(-0.72, 1.5, 0.45, '#e2d8c2'),   # the cream croup and haunch
                                                (0.42, 1.22, 0.28, '#e2d8c2'), (0.78, 1.62, 0.22, '#e2d8c2'), (0.78, 1.1, 0.22, '#e2d8c2')],   # (C5) paint patches on the shoulder, neck and chest
                                       # civ handprints (a palm and five fingers) on the haunch and the shoulder
                                       'blobs': [(-0.72, 1.4, 0.102), (-0.789, 1.512, 0.038), (-0.845, 1.544, 0.038), (-0.766, 1.559, 0.038), (-0.805, 1.631, 0.038), (-0.72, 1.571, 0.038), (-0.72, 1.652, 0.038), (-0.677, 1.561, 0.038), (-0.641, 1.634, 0.038), (-0.658, 1.521, 0.038), (-0.607, 1.561, 0.038), (0.65, 1.52, 0.085), (0.593, 1.613, 0.032), (0.546, 1.64, 0.032), (0.611, 1.653, 0.032), (0.58, 1.712, 0.032), (0.65, 1.663, 0.032), (0.65, 1.73, 0.032), (0.686, 1.654, 0.032), (0.716, 1.715, 0.032), (0.702, 1.621, 0.032), (0.745, 1.654, 0.032), (-0.5, 1.2, 0.06), (-0.95, 1.15, 0.06), (0.8, 1.3, 0.06)]}},
    'Ancient Cavalry': {'coat': '#8a683e', 'mane': '#16100c', 'points': '#16100c', 'coat_contrast': 1.3},   # (C6) smoother shading;   # (C5) a mid dun, black points, stronger shading;   # (C4) darker points, more muscle shading;   # a darker dun, shaded (critic2)
    'Rider': {'coat': '#c8c8c4', 'dapple': '#9a9a98', 'mane': '#8a8a88', 'points': '#4a4a48', 'coat_contrast': 0.7},   # (C5) a cleaner dapple grey, dark points, a darker mane   # (the realism pass: no civ specks on the haunch: noise)
    'Knight': {'coat': '#6a4630', 'mane': '#151110', 'points': '#1a1512'},   # (pass 5: a lighter bay, the head reads; was #4e3424)   # a dark bay destrier: the plate separates   # pale: the silver barding reads   # the lower legs grey as mail
    'Keshik': {'coat': '#7a4826', 'mane': '#1a120c', 'points': '#20160e', 'coat_contrast': 1.25},   # (C3) more muscle shading
    'Sipahi': {'coat': '#6a4428', 'mane': '#1a120c', 'points': '#1a120c', 'coat_contrast': 1.2},
    'Conquistador': {'coat': '#a0a0a2', 'dapple': '#5c5c60', 'mane': '#2a2826', 'points': '#3a3836', 'blanket_tint': False},
    'Cossack': {'coat': '#5e5e62', 'dapple': '#94949a', 'mane': '#141416', 'points': '#1a1a1c'},   # a dapple grey (a step lighter, the realism pass; was #4a4a4e / #7a7a80)
    # a chestnut (the realism pass; it was a dun with a civ-bound tail tip): red-gold, mane and tail a shade darker
    # (critic2) a bay: black mane, tail and lower legs; the coat's shading as the others (1.6 read as dirt)
    'Hussar': {'coat': '#a2602e', 'mane': '#16100c', 'points': '#1c140e', 'coat_contrast': 1.15},
    'Cavalry': {'coat': '#a0643a', 'mane': '#1e1610', 'points': '#18130f', 'blanket_tint': True, 'coat_contrast': 0.7},   # (critic9: smoother shading)   # a mid chestnut-bay (pass 8; pass 4: a dark bay #6e3f22, mane #141010)
    'Ansar Warrior': {'coat': '#34343a', 'mane': '#121214', 'points': '#1c1c1e', 'coat_contrast': 1.35},   # (the realism pass) the black Arab's muscles read
}

# The originals' tack the catalog doesn't describe (see DETAIL_AUDIT_PC.md)
TACK_OVERRIDES = {
    # Civ3's Cavalry: a dark bridle and reins with a pale noseband, a rolled cloak behind the cantle
    'Cavalry': {'bridle': '#1e1a18', 'bridle_tint': False, 'bedroll': True, 'reins': 'hand', 'cloth_frac': 0.6, 'cloth_back': 0.55, 'trim': '#a8987a', 'cloth_point': 0.36,
                'sheepskin': '#2a2420', 'bedroll_color': '#7e7c76', 'bedroll_r': 0.1, 'girth': True, 'strap_studs': True,
                'mane_trim': True, 'blaze': '#e6dfd2',   # (pass 8 = pass 6's kit, a buff breast strap; pass 4: bedroll #d4d2ca, breast strap 'blanket2')
                'cloth_round': 0.16, 'cloth_border': 0.05, 'cloth_folds': 0.05, 'cloth_style': 'tubes', 'breast_strap': 'buff_leather'},   # (critic9: drape)
    # Civ3's Keshik: twin lances behind the back, a pale sheepskin saddle, a civ noseband
    # (the realism pass) no back lances (clutter at 1x), a pale felt cloth, reins to the hand, a trimmed mane
    'Keshik': {'back_lances': False, 'blanket': 'saddle_cloth', 'blanket_tint': False, 'blanket_color': '#d2c4a4', 'nose_wrap': False, 'cloth_round': 0.12,
               'cloth_frac': 0.6, 'bridle': '#2a1e16', 'bridle_tint': False, 'mane_trim': True, 'mane_shag': 0.6, 'breast_strap': 'leather_dark',
               'cloth_border': 0.04, 'trim': '#3a2a1e', 'cloth_folds': 0.05, 'cloth_style': 'tubes', 'fringe': '#8a6a48'},   # (C3) a draped felt, a dark trim, a fringe   # (C2) the shaggy mane back, a dark edge, folds
    # Civ3's Sipahi: a civ caparison scattered with white spots over the neck to the ears, chest and flanks nearly to
    # the hocks, a civ cloth over the head with a white plume, a gold saddle on a red cloth, tassels
    # (the realism pass) a civ caprak with a broad gold border and two pale discs a side, a steel chanfron, no neck
    # sleeve, no plume; reins to the hand
    'Sipahi': {'blanket': 'saddle_cloth', 'blanket_tint': True, 'neck_cover': None, 'plume': False, 'chanfron': None, 'strap_studs': True,
               'cloth_frac': 1.25, 'cloth_back': 0.95, 'cloth_front': 0.6, 'cloth_round': 0.26, 'cloth_border': 0.07, 'trim': '#c4a462', 'cloth_folds': 0.09,   # (batch 3) a full draped cloth, croup to shoulders
               'cloth_style': 'tubes', 'fringe': '#c4a462', 'inner_line': 'gold',   # (batch 2) drape, a gold fringe and an inner line; a leather bridle, no chanfron
               'cloth_discs': [(-0.5, -0.02), (-0.18, -0.1)], 'saddle': '#7a2a1e', 'breast_strap': 'trim', 'tassels': 'trim',
               'bridle': '#2a1e16', 'bridle_tint': False, 'mane_trim': True, 'reins_bold': True},
    # Civ3's Conquistador: a padded cream trapper with a gold fringe, gold leather breast strap and crupper
    'Conquistador': {'blanket_color': '#d8c8a0', 'trim': '#c4a462', 'breast_strap': 'trim', 'crupper': 'trim', 'tassels': 'trim',
                     'cloth_back': 0.6, 'cloth_drop': 0.34,
                     'cloth_round': 0.24, 'cloth_border': 0.05, 'cloth_folds': 0.1, 'cloth_style': 'tubes', 'fringe': '#c4a462', 'cloth_frac': 1.0,   # (batch 3) deeper folds, a lower hem   # (the realism pass) a draped, gold-edged, fringed trapper
                     'reins': 'hand', 'bridle': '#2a1e16', 'mane_trim': True},
    # Civ3's Knight: fully barded, a civ caparison banded in gold from chest to rump, steel crinet, chanfron and peytral
    'Knight': {'full_caparison': True, 'neck_cover': 'lames', 'chanfron': 'steel', 'peytral': True, 'caparison_over': False,
               'blanket': 'barding', 'barding_steel': '#c8ccd2', 'cap_folds': 'tubes',
               'cap_band': (0.03, 0.17), 'cap_charge': 'blanket2', 'cap_charge_size': 1.45, 'cap_front': 0.2, 'cap_sim': True,
               'cap_rear': 0.28, 'chanfron_ears': False, 'cap_fullness': 0.15, 'cap_bending': 2.0, 'cap_hem': 0.27, 'reins': 'hand', 'cap_decal': True,
               'peytral': False,
               # (pass 5) one long trapper to near the fetlocks, a sculpted drape with deep folds, a thin gold border,
               # one small cross a side (on the rump panel); no neck cover (the plate crinet is 15th century). Pass 3: cap_sim
               # panels with two crosses a side, the steel crinet ('lames'), cap_hem 0.27, band (0.03, 0.17), size 1.45.
               'cap_sim': False, 'cap_one': True, 'cap_hem': -0.2, 'cap_fold_depth': 0.1, 'cap_band': (0.02, 0.1),
               'cap_charge_size': 1.1, 'cap_charges': 'rear', 'neck_cover': None,
               'cap_field': '#e6e0d2', 'cap_charge': 'blanket2', 'cap_paly': 1.2, 'cap_paly_civ': 0.36, 'cap_flare': 0.13,
               'breast_cloth': False},   # (critic7: a white cross on the civ rump panel, less civ, a flared hem, the breast cloth clear)   # paly civ and pale panels   # a pale trapper, civ border and crosses (the civ colour near the stock's 24 %)
    # (the realism pass) a cut civ saddle cloth with a pale fringe, no saddle tree; a hogged mane and a blaze; reins to the hand
    # (critic2) a neutral felt cloth with a civ border, no civ on the bridle
    'Horseman': {'nose_wrap': False, 'blanket_tint': False, 'blanket_color': '#6e6050', 'border_tint': True,
                 'breast_strap': 'leather_dark', 'cloth_round': 0.14, 'cloth_border': 0.11, 'trim': '#d8cfb8',
                 'cloth_frac': 0.42, 'cloth_back': 0.4, 'mane_trim': True, 'blaze': '#e6dccb', 'reins': 'hand', 'bridle': '#2a1e16',
                 'bridle_tint': False},
    # a civ-and-white patchwork blanket, civ tassels at the bridle and breast strap
    # (the realism pass) a small civ pad, no saddle, a rope war bridle; the civ tassels and breast strap kept small
    'Mounted Warrior': {'blanket': 'saddle_cloth', 'blanket_tint': True, 'trim': '#3a2a1e', 'fringe': '#c8a878', 'cloth_folds': 0.04, 'cloth_drop': 0.24,   # (C4) a dark outline, a buckskin fringe, folds
                        'cloth_frac': 0.42, 'cloth_back': 0.25, 'cloth_round': 0.14, 'cloth_border': 0.03, 'saddle_visible': False,
                        'breast_strap': 'tint', 'tassels': 'tint', 'throat_tassels': 'tint', 'bridle': '#1e1610',
                        'bridle_tint': False},
    # long bold civ reins, a dark pelt saddle pad
    # (the realism pass: a trimmed mane and reins to the hand)
    'Ancient Cavalry': {'mane_trim': True, 'reins': 'hand', 'bridle': '#2a1e16', 'bridle_tint': False, 'rein_drop': 0.3, 'blanket': 'pelt', 'pelt_color': '#2a201a', 'pelt_ragged': 0.018, 'hem_line': False, 'cloth_folds': 0.02, 'cloth_drop': 0.15,   # (C4) a soft dark hide, no rim
                        'crupper': 'leather_dark', 'strap_studs': True, 'stud_r': 0.065, 'studs_rear': True, 'stud_key': 'phalera',   # (C5) C3's phalerae back (lost at 32edf6cd), bigger, brighter
                        'saddle_horns': True, 'horn_scale': 1.7, 'horn_key': 'buff_leather',
                        'breast_strap': 'leather_dark'},
    # the burka rolled behind the saddle; (the realism pass) a small civ saddle cloth, cut and edged, reins to the hand
    'Cossack': {'bedroll': True, 'breast_strap': 'tint', 'strap_w': 0.045, 'cloth_frac': 1.0, 'cloth_round': 0.12, 'cloth_border': 0.03,
                'trim': '#3a2a1e', 'cloth_back': 0.36, 'reins': 'hand', 'bridle': '#1e1a18', 'bridle_tint': False, 'girth': True},
    # a civ shabraque with a gold edge, bold civ reins
    # (the realism pass) the hussar's shabraque with its long pointed rear corner and a broad gold lace border,
    # a black sheepskin over the seat, reins to the hand, a trimmed mane and a blaze
    'Hussar': {'blanket': 'saddle_cloth', 'blanket_tint': True, 'cloth_drop': 0.3, 'trim': '#c8a448',
               'cloth_frac': 0.62, 'cloth_round': 0.12, 'cloth_border': 0.035, 'cloth_back': 0.48, 'cloth_point': 0.6, 'cloth_folds': 0.12, 'cloth_style': 'tubes', 'sheepskin': '#24201c',
               'reins': 'hand', 'bridle': '#1e1a18', 'bridle_tint': False, 'breast_strap': 'leather_dark', 'girth': True,
               'mane_trim': True, 'blaze': '#e6dccb'},
    'Rider': {'reins': 'hand', 'bridle': '#3a2a1e', 'bridle_tint': False, 'mane_trim': True, 'plume_color': '#a8302a',
              'breast_strap': 'trim', 'crupper': 'trim', 'tassels': 'trim', 'plume': False, 'plume_tint': False,   # (the plume hid the face in S)
              'bedroll': True, 'bow_case': 'leather_dark', 'cloth_emblem': None, 'emblem_ys': [0.0, -0.5],   # (C2: no white diamond and crescent)
              'cloth_round': 0.14, 'cloth_border': 0.05,   # a gold border (the catalog's trim)
              'cloth_back': 0.85, 'cloth_drop': 0.36, 'chanfron': 'tint', 'reins_bold': True, 'emblem_ys': [-0.35],
              'saddle': '#7a5434', 'cloth_drop': 0.26, 'cloth_back': 0.55,   # (C2) a dark leather saddle, a gold-edged cloth; (C4) a lighter leather
              'blanket_tint': False, 'blanket_color': '#2e2418', 'border_tint': True, 'cloth_frac': 0.5,   # (C3) a dark cloth, civ only on its border
              'blanket_wear': {'grime': 0.15, 'soot': 0.0}, 'blanket_even': True, 'inner_line': 'gold'},   # (C4) an even deep-brown felt, a gold line inside the border; a dark bow case
   # (C3) a dark cloth, civ only on its border   # (the realism pass: shorter at the back: less civ)
    # the original's heavy blue reins and breast straps hang in big loops with tassels
    'Ansar Warrior': {'blanket_tint': True, 'cloth_round': 0.22, 'cloth_border': 0.04, 'trim': '#d0a840', 'cloth_folds': 0.09, 'cloth_frac': 1.1, 'cloth_back': 0.5,   # (round 4) the civ cloth shows behind the rider
                      'cloth_style': 'tubes', 'fringe': '#d0a840',   # (batch 2) drape and a gold fringe   # (the realism pass) a civ saddle cloth, a gold edge
                      'throat_tassels': 'tassel', 'reins_bold': True, 'rein_drop': 0.38, 'breast_strap': 'tint', 'strap_w': 0.085,   # (round 4) a broader civ breast strap and crupper
                      'crupper': 'tint', 'strap_sag': 0.22, 'tassels': 'tint', 'tassel_key': 'tassel'},
}

BUILDERS = {'Horseman': horseman}

# ---- (numpy-mounted) the Medieval Japan riders, remade from the NumPy units (mdj_riders.py): late-Heian to Kamakura
# mounted samurai in o-yoroi (laced sode and kusazuri, the dou's laced rows; civ lacing), an o-kabuto, on small
# stocky native horses with a lacquered kura, slipper abumi and civ munagai and shirigai cords with tassels.
MDJ_RIDERS = {'Shoni no Kagesuke', 'Nakamaro Horseman'}
HORSE_RIDERS = HORSE_RIDERS + sorted(MDJ_RIDERS)
# ---- (numpy-mounted, batch 2) the League riders (league_riders.py holds their entries and the slouch hat)
LEAGUE_RIDERS = {'Teutonic Knight', 'Roughrider'}
HORSE_RIDERS = HORSE_RIDERS + sorted(LEAGUE_RIDERS)
RIDE_POSE_BY_UNIT.update({'Teutonic Knight': 'knight_rest', 'Roughrider': 'carbine_thigh'})
WEAPON_UP_BY_UNIT.update({'Roughrider': (0.12, 1.0, 0.1)})   # (R3) one carry: muzzle forward, nearly level across the pommel, stock at the hip   # (R2: (0.3, 1.0, 0.42) rose over the head from behind)
WEAPON_UP_BY_UNIT.update({'Teutonic Knight': (0.5, 0.5, 0.75)})   # (R2) the lance angled out to the side: its length and pennon read
RIDER_SCALE_BY_UNIT.update({'Teutonic Knight': 1.129})
SEAT_PELVIS_BY_UNIT.update({'Teutonic Knight': 0.15})
HAT_PITCH_BY_UNIT.update({'Teutonic Knight': True})
NECK_RAISE_BY_UNIT.update({'Teutonic Knight': -30.0, 'Roughrider': -26.0})
HEAD_FLEX_BY_UNIT.update({'Teutonic Knight': 14.0, 'Roughrider': 16.0})
EAR_SCALE_BY_UNIT.update({'Teutonic Knight': 0.75})
HORSE_SHAPE_BY_UNIT.update({'Teutonic Knight': {'size': 1.14, 'neck': 1.2, 'head': 0.9, 'legs': 0.75},
                            'Roughrider': {'wide': 1.05, 'leggy': 1.02, 'neck': 1.1, 'head': 0.95}})   # a western cow pony
STIRRUPS.update({'Roughrider'})
HORSE_COAT_OVERRIDES.update({'Teutonic Knight': {'coat': '#4a4440', 'mane': '#151110', 'points': '#1a1512'},
                             'Roughrider': {'coat': '#7a4a2a', 'mane': '#16100c', 'points': '#1c140e', 'coat_contrast': 1.15}})
ABUMI = {'Shoni no Kagesuke': {}, 'Nakamaro Horseman': {}}            # mdj_riders.abumi's options
O_YOROI = {'Shoni no Kagesuke': {'swing_l': 55.0},                   # mdj_riders.o_yoroi's options (the bow arm's sode swung back)
           'Nakamaro Horseman': {'swing_r': 35.0}}   # (round 2) full civ lacing on the lames (dark edges), the dou dark with civ lacing rows
OFFHAND_NO_FIT = {'Shoni no Kagesuke'}                             # a unit's bow left out of the fit
BOW_DRAW['Shoni no Kagesuke'] = {'prop': 'yumi', 'up': (-0.12, 0.05, 1.0), 'forward': (-0.95, 0.3, 0.0), 'arrow': '#c8b48a', 'fletch': True}   # (round 2) toward the left front
RIDE_POSE_BY_UNIT.update({'Shoni no Kagesuke': 'yumi_ready', 'Nakamaro Horseman': 'tachi'})   # (round 2: was 'yabusame')
WEAPON_UP_BY_UNIT.update({'Nakamaro Horseman': (0.62, 0.4, 0.68)})   # (round 2) a high guard, up and out to the right, clear of the neck   # the tachi out low to the right, point forward (the sprite's)
WEAPON_FWD_BY_UNIT.update({'Nakamaro Horseman': (0.7, -0.7, 0.0)})
NECK_RAISE_BY_UNIT.update({'Shoni no Kagesuke': -30.0, 'Nakamaro Horseman': -30.0})
HEAD_FLEX_BY_UNIT.update({'Shoni no Kagesuke': 12.0, 'Nakamaro Horseman': 12.0})
HORSE_SHAPE_BY_UNIT.update({   # a small native horse: short legs, a deep barrel, a heavy head, a shaggy mane
    'Shoni no Kagesuke': {'size': 0.9, 'leggy': 0.9, 'wide': 1.12, 'legs': 0.7, 'head': 1.12, 'shins': 0.85, 'neck': 0.95},
    'Nakamaro Horseman': {'size': 0.9, 'leggy': 0.9, 'wide': 1.12, 'legs': 0.7, 'head': 1.12, 'shins': 0.85, 'neck': 0.95}})
HORSE_COAT_OVERRIDES.update({
    'Shoni no Kagesuke': {'coat': '#b4843a', 'mane': '#1e1a16', 'points': '#3a2a1a', 'coat_contrast': 1.15},   # a golden buckskin (the sprite's)
    'Nakamaro Horseman': {'coat': '#a0602e', 'mane': '#1c120c', 'points': '#2a1a10', 'coat_contrast': 1.15}})   # (R3) a chestnut: the dark rider reads against it
_MDJ_TACK = {'kura': 'yoroi_lacquer', 'blanket': 'saddle_cloth', 'blanket_tint': False, 'chanfron': None, 'nose_wrap': False,
             'cloth_frac': 0.5, 'cloth_back': 0.32, 'cloth_round': 0.16, 'cloth_border': 0.035, 'trim': '#c4a462',
             'cloth_style': 'tubes', 'cloth_folds': 0.04, 'bridle': '#241a14', 'bridle_tint': False, 'mane_shag': 0.6,
             'breast_strap': 'tint', 'crupper': 'tint', 'tassels': 'tint', 'strap_w': 0.04, 'strap_sag': 0.1, 'strap_reach': (0.85, 1.0)}
TACK_OVERRIDES.update({
    # the Knight's trapper re-dressed: a white field, the Order's black crosses (decals, no glow), a civ border band
    'Teutonic Knight': dict(TACK_OVERRIDES['Knight'], cap_paly=None, cap_charge='cap_cross', cap_charges='both', charge_glow=0.0,
                            cap_field='#ece8e0', trim='#2a2622', cap_band=(0.02, 0.28), cap_charge_size=1.3, cap_decal=False, breast_cloth=True, breast_offset=0.11),   # (R2) a wider civ border; the trapper closed over the chest   # crosses painted on the cloth's faces
    # the McClellan on the folded army blanket: civ with an orange edge stripe; a blanket roll; reins to the hand
    'Roughrider': {'blanket': 'saddle_cloth', 'blanket_tint': False, 'blanket_color': '#5a6470', 'border_tint': True,   # (R2) army blue-grey, a thin civ edge
                   'trim': '#c8742a', 'cloth_border': 0.03, 'cloth_frac': 0.55,
                   'cloth_back': 0.36, 'cloth_round': 0.06, 'cloth_drop': 0.3, 'saddle': '#1e1a18', 'bedroll': True,
                   'bedroll_color': '#6e6a5a', 'reins': 'hand', 'bridle': '#1e1a18', 'bridle_tint': False, 'girth': True,
                   'breast_strap': 'leather_dark', 'mane_trim': True, 'nose_wrap': False}})
TACK_OVERRIDES.update({
    'Shoni no Kagesuke': dict(_MDJ_TACK, blanket_tint=True, cloth_frac=0.9, cloth_drop=0.42, cloth_back=0.45),                         # a civ aori (the sprite's civ cloth), gold edged; the reins knotted on the neck
    'Nakamaro Horseman': dict(_MDJ_TACK, blanket_tint=False, blanket_color='#2e2219', trim='#1e1a16', cloth_border=0.04, cloth_frac=0.9,
                              cloth_drop=0.42, cloth_back=0.45, reins='hand', blanket_wear={'grime': 0.1, 'soot': 0.0},
                              kura_rim='yoroi_lacquer')})   # (round 2) a matte dark leather aori, no gilt: the civ only on the cords


def build(unit, colors=None, entry=None, rider=False, **params):
    """The unit's mount (and, with rider=True and its catalog entry, the
    seated rider)."""
    if entry is None and unit in LEAGUE_RIDERS:   # (numpy-mounted) the League riders' entries (not in this catalog)
        import league_riders
        entry = league_riders.ENTRIES[unit]
    if entry and entry.get('archetype') in ('horse_rider', 'mdj_horse_rider'):   # (numpy-mounted) the MDJ riders too
        root = mount(unit, entry.get('horse', {}))
    else:
        root = BUILDERS[unit](colors or {}, **params)
    if rider and entry and entry.get('rider'):
        seat_rider(root, unit, entry)
    if entry and entry.get('escort') == 'dog':
        # the war dog trotting beside the horse (Civ3's Conquistador): a black-and-tan mastiff, civ collar
        d = dict(entry.get('dog', {}), **DOG_BY_UNIT.get(unit, {}))   # (the realism pass) a unit's own dog
        dog = props.make('war_dog', {}, scale=d.get('scale', 1.6), **({'real': True} if d.get('real') else {}))   # a black-and-tan mastiff, civ collar
        dog.parent = root
        x, y, z = d.get('place', [0.95, 0.4, 0.0])
        dog.location = (x, y, z)
    return root
