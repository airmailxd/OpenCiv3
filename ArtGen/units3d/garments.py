"""Clothing and worn gear for the human units (Blender side).

Garments are made from the body itself, so they fit and follow any pose:
`shell()` copies the part of the body a garment covers (chosen by the rig's
bone weights and a height range on the unposed body), keeps its weights and
armature, and pushes it out and thickens it. Hanging cloth (skirts, kilts,
loincloths) starts from MakeHuman's skirt helper, a tube from waist to knee
around both legs. Beards and fur use Blender's hair particles.

Each maker takes the human (basemesh, armature) and the unit's catalog
entry's materials (`mats`, see units.py), and returns the new objects.
"""
import math

import bmesh
import bpy
from mathutils import Vector

import bscene as B


# Heights in this module (zmin, zmax, keep tests) are for a 1.75 m man;
# fit() scales them to the body at hand (set by fit_to(), from its height).
REFERENCE_HEIGHT = 1.75
_scale = 1.0


def fit_to(basemesh):
    """Scales the garment heights to `basemesh`'s height (top of the body)."""
    global _scale
    body = basemesh.vertex_groups.get('body')
    zs = [v.co.z for v in basemesh.data.vertices
          if body is None or any(g.group == body.index and g.weight > 0.5 for g in v.groups)]
    _scale = max(zs) / REFERENCE_HEIGHT
    return _scale


def z(v):
    """A height for a 1.75 m man, on the current body."""
    return None if v is None else v * _scale


# ------------------------------------------------------------ materials

def _pattern(tree, coord, base, pattern):
    """A color socket: the cloth's `base` color (linear RGBA) under a
    printed or woven pattern: ('camo', c2, c3[, c4]) blotches, ('check',
    dark[, scale]) squares, ('tartan', dark[, scale]) crossing bands."""
    n = tree.nodes
    kind = pattern[0]
    lin = [(*B.srgb_to_linear(B.hex_color(h)), 1.0) for h in pattern[1:] if isinstance(h, str)]
    nums = [v for v in pattern[1:] if isinstance(v, (int, float))]
    if kind == 'camo':
        noise = n.new('ShaderNodeTexNoise')
        noise.inputs['Scale'].default_value = nums[0] if nums else 9.0
        noise.inputs['Detail'].default_value = 1.5
        noise.inputs['Roughness'].default_value = 0.45
        tree.links.new(coord.outputs['Object'], noise.inputs['Vector'])
        ramp = n.new('ShaderNodeValToRGB')
        ramp.color_ramp.interpolation = 'CONSTANT'
        cols = [base] + lin
        els = ramp.color_ramp.elements
        while len(els) < len(cols):
            els.new(0.5)
        for i, (e, col) in enumerate(zip(els, cols)):
            e.position = 0.0 if i == 0 else 0.36 + 0.3 * (i - 1) / max(1, len(cols) - 2)
            e.color = col
        tree.links.new(noise.outputs['Fac'], ramp.inputs['Fac'])
        return ramp.outputs['Color']
    dark = lin[0] if lin else (0.02, 0.02, 0.02, 1.0)
    scale = nums[0] if nums else (28.0 if kind == 'check' else 18.0)
    if kind == 'panel':
        # a vertical panel or stripe down the front (and back) middle:
        # ('panel', color, half_width_m); a gold front stripe, an apron
        sep = n.new('ShaderNodeSeparateXYZ')
        tree.links.new(coord.outputs['Object'], sep.inputs['Vector'])
        ab = n.new('ShaderNodeMath')
        ab.operation = 'ABSOLUTE'
        tree.links.new(sep.outputs['X'], ab.inputs[0])
        lt = n.new('ShaderNodeMath')
        lt.operation = 'LESS_THAN'
        lt.inputs[1].default_value = (nums[0] if nums else 0.05) * _scale
        tree.links.new(ab.outputs['Value'], lt.inputs[0])
        mix = n.new('ShaderNodeMix')
        mix.data_type = 'RGBA'
        mix.inputs['A'].default_value = base
        mix.inputs['B'].default_value = dark
        tree.links.new(lt.outputs['Value'], mix.inputs['Factor'])
        return mix.outputs['Result']
    if kind == 'stripes':
        # vertical stripes (trouser stripes, slashing): dark bands across X
        wave = n.new('ShaderNodeTexWave')
        wave.wave_type = 'BANDS'
        wave.bands_direction = 'X'
        wave.inputs['Scale'].default_value = nums[0] if nums else 6.0
        tree.links.new(coord.outputs['Object'], wave.inputs['Vector'])
        hard = n.new('ShaderNodeMath')
        hard.operation = 'GREATER_THAN'
        hard.inputs[1].default_value = 0.8
        tree.links.new(wave.outputs['Fac'], hard.inputs[0])
        mix = n.new('ShaderNodeMix')
        mix.data_type = 'RGBA'
        mix.inputs['A'].default_value = base
        mix.inputs['B'].default_value = dark
        tree.links.new(hard.outputs['Value'], mix.inputs['Factor'])
        return mix.outputs['Result']
    if kind == 'check':
        chk = n.new('ShaderNodeTexChecker')
        chk.inputs['Scale'].default_value = scale
        chk.inputs['Color1'].default_value = base
        chk.inputs['Color2'].default_value = tuple(0.35 * b + 0.65 * d for b, d in zip(base, dark))
        tree.links.new(coord.outputs['Object'], chk.inputs['Vector'])
        return chk.outputs['Color']
    # tartan: dark bands both ways, darkest where they cross
    out = None
    for axis in ('X', 'Z'):
        wave = n.new('ShaderNodeTexWave')
        wave.wave_type = 'BANDS'
        wave.bands_direction = axis
        wave.inputs['Scale'].default_value = scale
        tree.links.new(coord.outputs['Object'], wave.inputs['Vector'])
        mix = n.new('ShaderNodeMix')
        mix.data_type = 'RGBA'
        mix.blend_type = 'MULTIPLY'
        hard = n.new('ShaderNodeMath')                 # sharp-edged stripes
        hard.operation = 'GREATER_THAN'
        hard.inputs[1].default_value = 0.62
        tree.links.new(wave.outputs['Fac'], hard.inputs[0])
        tree.links.new(hard.outputs['Value'], mix.inputs['Factor'])
        if out is None:
            mix.inputs['A'].default_value = base
        else:
            tree.links.new(out, mix.inputs['A'])
        mix.inputs['B'].default_value = tuple(0.5 + 0.5 * d for d in dark[:3]) + (1.0,)
        out = mix.outputs['Result']
    return out


def cloth(name, color, tint=False, rough=0.85, weave=0.25, pattern=None):
    """Woven cloth: a fine weave bump and a little color variation; with
    `pattern` (see _pattern), printed or woven with it."""
    def nodes(tree, bsdf):
        n = tree.nodes
        tex = n.new('ShaderNodeTexNoise')
        tex.inputs['Scale'].default_value = 220.0
        tex.inputs['Detail'].default_value = 2.0
        # creases: soft vertical ridges, wandering
        coord = n.new('ShaderNodeTexCoord')
        crease = n.new('ShaderNodeTexWave')
        crease.bands_direction = 'X'
        crease.inputs['Scale'].default_value = 14.0
        crease.inputs['Distortion'].default_value = 6.0
        crease.inputs['Detail'].default_value = 3.0
        tree.links.new(coord.outputs['Object'], crease.inputs['Vector'])
        add = n.new('ShaderNodeMath')
        add.operation = 'MULTIPLY_ADD'
        add.inputs[1].default_value = 0.15
        tree.links.new(tex.outputs['Fac'], add.inputs[0])
        tree.links.new(crease.outputs['Fac'], add.inputs[2])
        bump = n.new('ShaderNodeBump')
        bump.inputs['Strength'].default_value = weave
        tree.links.new(add.outputs['Value'], bump.inputs['Height'])
        tree.links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
        bsdf.inputs['Sheen Weight'].default_value = 0.12
        # dirt: broad, soft darker patches (no cloth stays factory-clean)
        dn = n.new('ShaderNodeTexNoise')
        dn.inputs['Scale'].default_value = 3.0
        dn.inputs['Detail'].default_value = 3.0
        tree.links.new(coord.outputs['Object'], dn.inputs['Vector'])
        dr = n.new('ShaderNodeValToRGB')
        dr.color_ramp.elements[0].position = 0.4
        dr.color_ramp.elements[0].color = (0.55, 0.5, 0.44, 1)
        dr.color_ramp.elements[1].position = 0.7
        tree.links.new(dn.outputs['Fac'], dr.inputs['Fac'])
        # darker in the folds and where the cloth meets the body
        ao = n.new('ShaderNodeAmbientOcclusion')
        ao.inputs['Distance'].default_value = 0.05
        mul = n.new('ShaderNodeMix')
        mul.data_type = 'RGBA'
        mul.blend_type = 'MULTIPLY'
        mul.inputs['Factor'].default_value = 0.75
        mul.inputs['A'].default_value = bsdf.inputs['Base Color'].default_value
        if pattern:
            tree.links.new(_pattern(tree, coord, tuple(bsdf.inputs['Base Color'].default_value), pattern),
                           mul.inputs['A'])
        tree.links.new(ao.outputs['AO'], mul.inputs['B'])
        dirt = n.new('ShaderNodeMix')
        dirt.data_type = 'RGBA'
        dirt.blend_type = 'MULTIPLY'
        dirt.inputs['Factor'].default_value = 0.8
        tree.links.new(mul.outputs['Result'], dirt.inputs['A'])
        tree.links.new(dr.outputs['Color'], dirt.inputs['B'])
        tree.links.new(dirt.outputs['Result'], bsdf.inputs['Base Color'])
    return B.material(name, color, roughness=rough, tint=tint, nodes=nodes)


def leather(name, color, tint=False):
    def nodes(tree, bsdf):
        n = tree.nodes
        tex = n.new('ShaderNodeTexNoise')
        tex.inputs['Scale'].default_value = 60.0
        tex.inputs['Detail'].default_value = 6.0
        ramp = n.new('ShaderNodeValToRGB')
        ramp.color_ramp.elements[0].position = 0.35
        ramp.color_ramp.elements[1].position = 0.75
        mix = n.new('ShaderNodeMix')
        mix.data_type = 'RGBA'
        mix.blend_type = 'MULTIPLY'
        mix.inputs['Factor'].default_value = 0.25
        mix.inputs['A'].default_value = bsdf.inputs['Base Color'].default_value
        tree.links.new(tex.outputs['Fac'], ramp.inputs['Fac'])
        tree.links.new(ramp.outputs['Color'], mix.inputs['B'])
        tree.links.new(mix.outputs['Result'], bsdf.inputs['Base Color'])
        bump = n.new('ShaderNodeBump')
        bump.inputs['Strength'].default_value = 0.15
        tree.links.new(tex.outputs['Fac'], bump.inputs['Height'])
        tree.links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
    return B.material(name, color, roughness=0.55, tint=tint, nodes=nodes)


def fur(name, color, dark=None, tint=False):
    """Fur wrappings: banded light and dark (the original's striped wraps)."""
    dark = dark or tuple(c * 0.55 for c in color)

    def nodes(tree, bsdf):
        n = tree.nodes
        coord = n.new('ShaderNodeTexCoord')
        sep = n.new('ShaderNodeSeparateXYZ')
        tree.links.new(coord.outputs['Object'], sep.inputs['Vector'])
        wave = n.new('ShaderNodeTexWave')
        wave.wave_type = 'BANDS'
        wave.bands_direction = 'Z'
        wave.inputs['Scale'].default_value = 5.0
        wave.inputs['Distortion'].default_value = 9.0
        wave.inputs['Detail'].default_value = 8.0
        wave.inputs['Detail Roughness'].default_value = 0.8
        tree.links.new(coord.outputs['Object'], wave.inputs['Vector'])
        ramp = n.new('ShaderNodeValToRGB')
        ramp.color_ramp.elements[0].color = (*B.srgb_to_linear(dark), 1)
        ramp.color_ramp.elements[1].color = (*B.srgb_to_linear(color), 1)
        ramp.color_ramp.elements[0].position = 0.35
        ramp.color_ramp.elements[1].position = 0.65
        tree.links.new(wave.outputs['Fac'], ramp.inputs['Fac'])
        # darker in the creases, like the cloth (else the same swatch read much paler as fur)
        ao = n.new('ShaderNodeAmbientOcclusion')
        ao.inputs['Distance'].default_value = 0.05
        mul = n.new('ShaderNodeMix')
        mul.data_type = 'RGBA'
        mul.blend_type = 'MULTIPLY'
        mul.inputs['Factor'].default_value = 0.85
        tree.links.new(ramp.outputs['Color'], mul.inputs['A'])
        tree.links.new(ao.outputs['AO'], mul.inputs['B'])
        dim = n.new('ShaderNodeMix')
        dim.data_type = 'RGBA'
        dim.blend_type = 'MULTIPLY'
        dim.inputs['Factor'].default_value = 1.0
        dim.inputs['B'].default_value = (0.72, 0.72, 0.72, 1.0)
        tree.links.new(mul.outputs['Result'], dim.inputs['A'])
        tree.links.new(dim.outputs['Result'], bsdf.inputs['Base Color'])
        noise = n.new('ShaderNodeTexNoise')
        noise.inputs['Scale'].default_value = 300.0
        bump = n.new('ShaderNodeBump')
        bump.inputs['Strength'].default_value = 0.6
        tree.links.new(noise.outputs['Fac'], bump.inputs['Height'])
        tree.links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
    return B.material(name, color, roughness=0.95, tint=tint, nodes=nodes)


def hair_material(name, color):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    tree = mat.node_tree
    tree.nodes.clear()
    out = tree.nodes.new('ShaderNodeOutputMaterial')
    hair = tree.nodes.new('ShaderNodeBsdfHairPrincipled')
    # the swatch's color directly (the melanin model, from the swatch's
    # lightness, turned mid gingers and browns into pale blonds)
    hair.parametrization = 'COLOR'
    # (the strands scatter light and read much paler than the swatch: darken)
    hair.inputs['Color'].default_value = tuple(0.45 * c * c ** 0.3 for c in B.srgb_to_linear(color)) + (1.0,)
    hair.inputs['Roughness'].default_value = 0.5
    hair.inputs['Radial Roughness'].default_value = 0.35
    tree.links.new(hair.outputs[0], out.inputs['Surface'])
    aov = tree.nodes.new('ShaderNodeOutputAOV')
    aov.aov_name = 'tint'
    aov.inputs['Value'].default_value = 0.0
    return mat


# ------------------------------------------------------------ shells

def _bone_weights(mesh_obj):
    """Per vertex: {bone name: weight} for the deform groups."""
    names = {g.index: g.name for g in mesh_obj.vertex_groups}
    return [{names[g.group]: g.weight for g in v.groups} for v in mesh_obj.data.vertices]


def shell(basemesh, arm, name, bones, mat, *, zmin=None, zmax=None, offset=0.006, thickness=0.004,
          min_weight=0.35, keep=None):
    """A garment from the body's surface: the faces whose vertices mostly
    belong to `bones` (and lie between zmin and zmax on the unposed body,
    heights for a 1.75 m man; `keep` also sees positions scaled to one),
    pushed out by `offset` and given `thickness`. `keep(co)`, if given, is a
    further test on each vertex's rest position."""
    weights = _bone_weights(basemesh)
    body_group = basemesh.vertex_groups.get('body')
    me = basemesh.data
    chosen = set()
    for v, w in zip(me.vertices, weights):
        if body_group is not None and w.get('body', 0) < 0.5:
            continue
        if sum(w.get(b, 0.0) for b in bones) < min_weight:
            continue
        h = v.co.z / _scale
        if zmin is not None and h < zmin or zmax is not None and h > zmax:
            continue
        if keep and not keep(v.co / _scale):
            continue
        chosen.add(v.index)
    new = basemesh.copy()
    new.data = basemesh.data.copy()
    new.name = name
    bpy.context.scene.collection.objects.link(new)
    bm = bmesh.new()
    bm.from_mesh(new.data)
    bm.verts.ensure_lookup_table()
    drop = [f for f in bm.faces if not all(v.index in chosen for v in f.verts)]
    bmesh.ops.delete(bm, geom=drop, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    # a clean hem: relax the cut edge (the faces' stair-step outline) along the surface
    edge = [v for v in bm.verts if v.is_boundary]
    for _ in range(6):
        bmesh.ops.smooth_vert(bm, verts=edge, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    bm.normal_update()
    for v in bm.verts:
        v.co += v.normal * offset
    bm.to_mesh(new.data)
    bm.free()
    for m in list(new.modifiers):
        if m.type not in ('ARMATURE',):
            new.modifiers.remove(m)
    new.data.materials.clear()
    new.data.materials.append(mat)
    for key in (new.data.shape_keys.key_blocks if new.data.shape_keys else []):
        pass
    sol = new.modifiers.new('Solidify', 'SOLIDIFY')
    sol.thickness = thickness
    sol.offset = 1.0
    sol.use_rim = False          # an open edge: the rim caught the light as a pale piping
    sub = new.modifiers.new('Subdiv', 'SUBSURF')
    sub.levels = sub.render_levels = 1
    return new


def skirt(basemesh, arm, name, mat, *, length=0.5, flare=1.08, front_only=False, panels=None, folds=0.06, drop=0.0):
    """Hanging cloth from MakeHuman's skirt helper (waist to knee): cut to
    `length` of the helper's height, widened by `flare` toward the hem.
    `panels` = (front, back) half-widths (radians) keeps only flaps in front
    and behind: a loincloth."""
    me = basemesh.data
    grp = basemesh.vertex_groups.get('helper-skirt')
    idx = {v.index for v in me.vertices if any(g.group == grp.index and g.weight > 0.5 for g in v.groups)}
    zs = [me.vertices[i].co.z for i in idx]
    ztop, zbot = max(zs), min(zs)
    zcut = ztop - (ztop - zbot) * length
    cx = sum(me.vertices[i].co.x for i in idx) / len(idx)
    cy = sum(me.vertices[i].co.y for i in idx) / len(idx)
    new = basemesh.copy()
    new.data = basemesh.data.copy()
    new.name = name
    bpy.context.scene.collection.objects.link(new)
    bm = bmesh.new()
    bm.from_mesh(new.data)
    bm.verts.ensure_lookup_table()

    def keep(v):
        if v.index not in idx or v.co.z < zcut - 1e-4:
            return False
        if panels:
            a = math.atan2(v.co.x - cx, -(v.co.y - cy))   # 0 = front (-y on the unposed MakeHuman)
            front, back = panels
            return abs(a) <= front or abs(abs(a) - math.pi) <= back
        return True
    cut = [f for f in bm.faces if not all(keep(v) for v in f.verts)]
    bmesh.ops.delete(bm, geom=cut, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    for v in bm.verts:
        t = (ztop - v.co.z) / max(1e-6, ztop - zbot)
        k = 1.0 + (flare - 1.0) * t
        # folds: radial ripples around the legs, deepening toward the hem
        a = math.atan2(v.co.x - cx, v.co.y - cy)
        k += folds * t * (0.6 * math.sin(a * 9.0) + 0.4 * math.sin(a * 23.0 + 1.3))
        v.co.x = cx + (v.co.x - cx) * k
        v.co.y = cy + (v.co.y - cy) * k
        # a robe or surcoat: stretched down toward the ankles
        v.co.z -= drop * t
    bm.to_mesh(new.data)
    bm.free()
    for m in list(new.modifiers):
        if m.type != 'ARMATURE':
            new.modifiers.remove(m)
    new.data.materials.clear()
    new.data.materials.append(mat)
    sol = new.modifiers.new('Solidify', 'SOLIDIFY')
    sol.thickness = 0.005
    sub = new.modifiers.new('Subdiv', 'SUBSURF')
    sub.levels = sub.render_levels = 1
    return new


def beard(basemesh, color, length=0.018, density=900, style='full'):
    """A beard of hair particles on the unposed body (MakeHuman's head: top at
    head_z, chin about 0.22 below): 'full' (jaw, chin and upper lip),
    'mustache' (the upper lip only, drooping a little wider), 'goatee' (lip
    and the middle of the chin), 'chin' (along the jaw, no mustache)."""
    me = basemesh.data
    head_z, yc, on_body = _head(basemesh)
    grp = basemesh.vertex_groups.new(name='beard')
    for v in me.vertices:
        if not on_body(v):
            continue
        c = v.co
        d = head_z - c.z
        y = c.y - yc
        jaw = 0.185 < d < 0.235 and y < -0.02      # below the cheekbones: the face stays open
        lip = 0.148 < d < 0.162 and y < -0.07 and abs(c.x) < 0.03
        if style == 'mustache':
            keep = 0.146 < d < 0.168 and y < -0.065 and abs(c.x) < 0.045
        elif style == 'goatee':
            keep = lip or (jaw and abs(c.x) < 0.03)
        elif style == 'chin':
            keep = jaw
        else:
            keep = jaw or lip
        if keep:
            grp.add([v.index], 1.0, 'REPLACE')
    return _hair_system(basemesh, 'Beard', 'beard', color, length, density)


def long_beard(basemesh, arm, color, length=0.16, width=0.11, square=True):
    """A long beard falling from the chin down the chest: a tapered (or, with
    `square`, square-cut) mass of hair under a strand coat; on the head bone."""
    import human as H
    head_z, yc, _ = _head(basemesh)
    s = _scale
    top = head_z - 0.19 * s
    bm = bmesh.new()
    rows = 6
    for i in range(rows + 1):
        t = i / rows
        w = width * s * (1.0 - (0.15 if square else 0.6) * t) / 2
        dpt = 0.05 * s * (1.0 - 0.4 * t)
        z = top - length * s * t
        yfront = yc - 0.085 * s - 0.02 * s * t
        for x, y in ((-w, yfront), (w, yfront), (w, yfront + dpt), (-w, yfront + dpt)):
            bm.verts.new((x, y, z))
    bm.verts.ensure_lookup_table()
    for i in range(rows):
        for k in range(4):
            a, b = i * 4 + k, i * 4 + (k + 1) % 4
            bm.faces.new((bm.verts[a], bm.verts[b], bm.verts[b + 4], bm.verts[a + 4]))
    bm.faces.new([bm.verts[rows * 4 + k] for k in range(4)])
    me = bpy.data.meshes.new('LongBeard')
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new('LongBeard', me)
    bpy.context.scene.collection.objects.link(obj)
    sub = obj.modifiers.new('Subdiv', 'SUBSURF')
    sub.levels = sub.render_levels = 2
    obj.data.materials.append(hide('beard', color, tuple(v * 0.55 for v in color)))
    grp = obj.vertex_groups.new(name='strands')
    grp.add([v.index for v in obj.data.vertices], 1.0, 'REPLACE')
    _hair_system(obj, 'BeardStrands', 'strands', color, 0.02, 1500)
    world = obj.matrix_world.copy()
    obj.parent = arm
    obj.parent_type = 'BONE'
    obj.parent_bone = 'head'
    obj.matrix_world = world
    H._update()
    return obj


def _hair_system(obj, name, group, color, length, density):
    mod = obj.modifiers.new(name, 'PARTICLE_SYSTEM')
    ps = mod.particle_system
    st = ps.settings
    st.type = 'HAIR'
    st.count = density
    st.hair_length = length
    st.use_advanced_hair = True
    st.child_type = 'INTERPOLATED'
    st.rendered_child_count = 6
    st.child_length = 0.9
    st.roughness_2 = 0.04
    st.clump_factor = 0.3
    st.root_radius = 0.0025
    st.tip_radius = 0.0008
    st.radius_scale = 1.0
    ps.vertex_group_density = group
    mat = hair_material(name + 'Mat', color)
    obj.data.materials.append(mat)
    st.material = len(obj.data.materials)
    return mod


def hide(name, color, dark, tint=False):
    """Shaggy animal hide: blotchy light and dark fur with a deep, coarse bump."""
    def nodes(tree, bsdf):
        n = tree.nodes
        coord = n.new('ShaderNodeTexCoord')
        noise = n.new('ShaderNodeTexNoise')
        noise.inputs['Scale'].default_value = 14.0
        noise.inputs['Detail'].default_value = 10.0
        noise.inputs['Roughness'].default_value = 0.75
        tree.links.new(coord.outputs['Object'], noise.inputs['Vector'])
        ramp = n.new('ShaderNodeValToRGB')
        ramp.color_ramp.elements[0].color = (*B.srgb_to_linear(dark), 1)
        ramp.color_ramp.elements[1].color = (*B.srgb_to_linear(color), 1)
        ramp.color_ramp.elements[0].position = 0.35
        ramp.color_ramp.elements[1].position = 0.7
        tree.links.new(noise.outputs['Fac'], ramp.inputs['Fac'])
        tree.links.new(ramp.outputs['Color'], bsdf.inputs['Base Color'])
        strands = n.new('ShaderNodeTexWave')
        strands.wave_type = 'BANDS'
        strands.bands_direction = 'Z'
        strands.inputs['Scale'].default_value = 90.0
        strands.inputs['Distortion'].default_value = 25.0
        strands.inputs['Detail'].default_value = 6.0
        tree.links.new(coord.outputs['Object'], strands.inputs['Vector'])
        bump = n.new('ShaderNodeBump')
        bump.inputs['Strength'].default_value = 0.9
        tree.links.new(strands.outputs['Fac'], bump.inputs['Height'])
        tree.links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
    return B.material(name, color, roughness=0.95, tint=tint, nodes=nodes)


def pelt(basemesh, arm, color, dark, length=0.045, density=3000, kind='shoulders'):
    """A heavy animal pelt: a thick hide shell with fur particles, so the
    figure reads broader and rougher. `kind`: 'shoulders' (over the
    shoulders and down the back, not the chest), 'cloak' (from the shoulders
    down the whole back to the thighs) or 'vest' (round the torso, front and
    back, to the hips)."""
    if kind == 'cloak':
        def keep(co):
            return co.z > 1.4 or co.y > 0.02          # the unposed MakeHuman faces -y
        bones = ['clavicle_l', 'clavicle_r', 'spine_01', 'spine_02', 'spine_03', 'pelvis',
                 'upperarm_l', 'upperarm_r', 'thigh_l', 'thigh_r']
        zmin, offset = 0.62, 0.045
    elif kind == 'kilt':
        # a fur kilt from the waist to the knee, round the hips and thighs
        keep = None
        bones = ['pelvis', 'thigh_l', 'thigh_r']
        zmin, offset = 0.5, 0.045
    elif kind == 'vest':
        keep = None
        bones = ['clavicle_l', 'clavicle_r', 'spine_01', 'spine_02', 'spine_03', 'pelvis']
        zmin, offset = 0.9, 0.04
    else:
        def keep(co):
            return (co.z > 1.4) or (co.y > 0.0 and co.z > 1.12)
        bones = ['clavicle_l', 'clavicle_r', 'spine_02', 'spine_03', 'upperarm_l', 'upperarm_r']
        zmin, offset = 1.12, 0.035
    obj = shell(basemesh, arm, 'Pelt_' + kind, bones, hide('pelt', color, dark), zmin=zmin,
                zmax=1.0 if kind == 'kilt' else 1.47,
                offset=offset, thickness=0.025, min_weight=0.3, keep=keep)
    grp = obj.vertex_groups.new(name='furzone')
    grp.add([v.index for v in obj.data.vertices], 1.0, 'REPLACE')
    _hair_system(obj, 'PeltFur', 'furzone', dark, length, density)
    return obj


def _head(basemesh):
    """(top z, center y) of the unposed head, and the body test."""
    me = basemesh.data
    body = basemesh.vertex_groups.get('body')

    def on_body(v):
        return body is None or any(g.group == body.index and g.weight > 0.5 for g in v.groups)
    top = max(v.co.z for v in me.vertices if on_body(v))
    head = [v.co.y for v in me.vertices if on_body(v) and v.co.z > top - 0.12]
    return top, (min(head) + max(head)) / 2.0, on_body


def wild_hair(basemesh, color, length=0.1, density=5000, clump=0.55, rough=0.05):
    """Long, unkempt hair from the scalp: hair particles on the crown, the
    back of the head and the sides above the ears (not the face), falling to
    the shoulders, clumped and rough."""
    top, yc, on_body = _head(basemesh)
    grp = basemesh.vertex_groups.new(name='scalp')
    for v in basemesh.data.vertices:
        if not on_body(v):
            continue
        d = top - v.co.z
        y = v.co.y - yc                          # < 0: toward the face
        crown = d < 0.06 and y > -0.07
        back = d < 0.19 and y > -0.005
        sides = d < 0.115 and abs(v.co.x) > 0.055 and y > -0.05
        if crown or back or sides:
            grp.add([v.index], 1.0, 'REPLACE')
    mod = _hair_system(basemesh, 'Hair', 'scalp', color, length, density)
    st = mod.particle_system.settings
    st.clump_factor = clump
    st.clump_shape = -0.2
    st.roughness_1 = rough
    st.roughness_endpoint = 0.04
    st.rendered_child_count = 12
    st.child_radius = 0.02
    st.use_hair_bspline = True
    # out from the scalp: a wild, shaggy mane (hair can't fall without a
    # simulation, and an unkempt mane suits these units)
    st.roughness_2 = 0.08
    st.kink = 'WAVE'
    st.kink_amplitude = 0.012
    st.kink_frequency = 3.0
    st.hair_length = length      # last: setting velocities rescales the length
    return mod


def roughen_hair(basemesh, color, length=0.022, density=4000):
    """Breaks up a hair mesh's smooth surface (MPFB's hair assets) with a coat
    of short, rough strands, so it reads as unkempt hair, not a shell."""
    import human as H
    hair = [c for c in H.assets(basemesh) if c.type == 'MESH' and (any(
        (s.material and 'hair' in s.material.name.lower()) for s in c.material_slots) or any(
        k in c.name.lower() for k in ('long', 'short', 'ponytail', 'braid', 'bob', 'afro')))]
    for obj in hair:
        grp = obj.vertex_groups.new(name='fuzz')
        grp.add([v.index for v in obj.data.vertices], 1.0, 'REPLACE')
        mod = _hair_system(obj, 'Fuzz', 'fuzz', color, length, density)
        st = mod.particle_system.settings
        st.roughness_1 = 0.012
        st.roughness_2 = 0.03
        st.clump_factor = 0.4
        st.hair_length = length
    return hair


def flap(name, mat, width=0.26, length=0.34, folds=5, depth=0.04, ragged=0.05, curve=0.12, seed=1, torn=True,
         flare=0.15, wrap=0.0, train=0.0):
    """A hanging cloth or hide flap (a loincloth panel, a tabard): a grid
    hanging down from its top edge at the origin, in the X-Z plane, facing
    -Y; with vertical folds deepening toward the hem, a ragged, uneven hem,
    and curved around the body by `curve` (meters of bow at the edges).
    Place it with human.attach on the pelvis."""
    import random
    rnd = random.Random(seed)
    nx, nz = 18, 14
    bm = bmesh.new()
    rows = []
    # torn into strips of uneven width (2-4 columns) and length, each cut at
    # its own angle with frayed tips
    edges, i = [0], 0
    while i < nx:
        i = min(nx, i + (rnd.choice((2, 3, 3, 4)) if torn else nx))
        edges.append(i)
    slits = set(edges[1:-1])
    hem = [0.0] * (nx + 1)
    for a, b in zip(edges, edges[1:]):
        base = length * (1.0 - (rnd.uniform(0.0, 0.35) if torn else 0.0))
        tilt = rnd.uniform(-0.08, 0.08) * length if torn else 0.0
        for c in range(a, b + 1):
            u = (c - a) / max(1, b - a)
            hem[c] = max(hem[c], base + tilt * (u - 0.5) * 2 + rnd.uniform(-0.012, 0.012))
    for j in range(nz + 1):
        row = []
        for i in range(nx + 1):
            u = i / nx - 0.5
            t = j / nz
            zz = -t * hem[i]
            ph = (u + 0.5) * math.pi * 2 * folds
            fold = depth * (0.25 + 0.75 * t) * (0.7 * math.sin(ph) + 0.3 * math.sin(ph * 2.3 + 1.1))
            yy = -curve * (1 - (2 * u) ** 2) * 0.5 + fold + 0.02 * t   # bowed around the body, swinging out
            # `wrap`: the sides come forward round the body toward the hem; `train`: the hem trails back
            yy += wrap * (2 * u) ** 2 * (0.3 + 0.7 * t) - train * t ** 3
            row.append(bm.verts.new((u * width * (1 + flare * t), yy - curve * 0.5, zz)))
        rows.append(row)
    for j in range(nz):
        for i in range(nx):
            # slits between the strips, from 45% down
            if j / nz > 0.45 and (i + 1) in slits:
                continue
            bm.faces.new((rows[j][i], rows[j][i + 1], rows[j + 1][i + 1], rows[j + 1][i]))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.use_smooth = True
    sol = obj.modifiers.new('Solidify', 'SOLIDIFY')
    sol.thickness = 0.006
    sol.use_rim = False
    sub = obj.modifiers.new('Subdiv', 'SUBSURF')
    sub.levels = sub.render_levels = 1
    return obj


def loincloth(basemesh, arm, mat, back_mat=None, width=0.34, length=0.42, back_length=0.36, seed=3, torn=True):
    """Front and back flaps hanging from the belt, parented to the pelvis."""
    import human as H
    from mathutils import Matrix
    H._update()
    pb = arm.pose.bones['pelvis']
    zb = (arm.matrix_world @ pb.head).z + 0.05 * _scale
    dg = bpy.context.evaluated_depsgraph_get()
    ev = basemesh.evaluated_get(dg)
    me = ev.to_mesh()
    mw = ev.matrix_world
    ph = arm.matrix_world @ pb.head
    # the hips at belt height (not the hands, which hang at the same height)
    ring = [p for p in (mw @ v.co for v in me.vertices)
            if abs(p.z - zb) < 0.025 * _scale and math.hypot(p.x - ph.x, p.y - ph.y) < 0.2 * _scale]
    ev.to_mesh_clear()
    cx = sum(p.x for p in ring) / len(ring)
    front = min(p.y for p in ring)
    back = max(p.y for p in ring)
    made = []
    for side, y, rot, ln, m in (('F', front - 0.012, 0.0, length, mat), ('B', back + 0.012, math.pi, back_length, back_mat or mat)):
        f = flap('Loin' + side, m, width=width * _scale, length=ln * _scale, curve=0.06 * _scale, seed=seed + (side == 'B'),
                 torn=torn, ragged=0.05 if torn else 0.015)
        f['no_drape'] = True
        f.matrix_world = Matrix.Translation((cx, y, zb)) @ Matrix.Rotation(rot, 4, 'Z')
        world = f.matrix_world.copy()
        f.parent = arm
        f.parent_type = 'BONE'
        f.parent_bone = 'pelvis'
        f.matrix_world = world
        made.append(f)
    H._update()
    return made


def falling_hair(basemesh, color, length=0.24, density=6000):
    """Long, shaggy hair falling from the scalp past the shoulders, in
    separate clumps (hair particles grown down; set the length last, as
    setting the growth velocities rescales it)."""
    mod = wild_hair(basemesh, color, length=length, density=density, clump=0.8, rough=0.03)
    st = mod.particle_system.settings
    # growth velocities are in meters: a little out from the scalp, then
    # `length` down and a little back (+y is behind the unposed body)
    # (measured: a strand grows about 4x its velocity's length)
    st.normal_factor = 0.012
    st.object_align_factor[0] = 0.0
    st.object_align_factor[1] = 0.16 * length
    st.object_align_factor[2] = -0.22 * length
    st.kink = 'NO'
    st.clump_factor = 0.85
    st.clump_shape = 0.3
    st.child_radius = 0.035
    return mod


def drape(objs, body, frames=60, pin_height=0.03, stiffness=2.0):
    """Cloth simulation: hangs each garment in `objs` under gravity onto the
    posed `body` (a collider), pinned along its top edge (the vertices within
    `pin_height` of its highest point, in its own space), then bakes the
    result into the mesh. Call after posing; the pose must not change after."""
    import human as H
    scene = bpy.context.scene
    col = body.modifiers.new('Collision', 'COLLISION')
    body.collision.thickness_outer = 0.006
    body.collision.cloth_friction = 8.0
    for obj in objs:
        zs = [v.co.z for v in obj.data.vertices]
        top = max(zs)
        pin = obj.vertex_groups.new(name='pin')
        topv = [v for v in obj.data.vertices if v.co.z > top - pin_height]
        if obj.get('tie_points'):
            # tied at a few points along the belt: the cloth between sags into swags
            xs = sorted(v.co.x for v in topv)
            lo, hi = xs[0], xs[-1]
            n = obj['tie_points']
            ties = [lo + (hi - lo) * k / (n - 1) for k in range(n)]
            tol = (hi - lo) / (n * 4.0)
            topv = [v for v in topv if any(abs(v.co.x - t) < tol for t in ties)]
        pin.add([v.index for v in topv], 1.0, 'REPLACE')
        cloth = obj.modifiers.new('Cloth', 'CLOTH')
        # right after the armature (cloth acts on the posed shape), before solidify/subdiv
        idx = next((i for i, m in enumerate(obj.modifiers) if m.type == 'ARMATURE'), -1) + 1
        with bpy.context.temp_override(object=obj, active_object=obj):
            bpy.ops.object.modifier_move_to_index(modifier='Cloth', index=idx)
        st = cloth.settings
        st.vertex_group_mass = 'pin'
        st.quality = 8
        st.mass = 0.25
        st.tension_stiffness = st.compression_stiffness = stiffness
        st.bending_stiffness = 0.05
        st.air_damping = 2.0
        cloth.collision_settings.use_self_collision = False
        cloth.collision_settings.distance_min = 0.004
        cloth.point_cache.frame_start = 1
        cloth.point_cache.frame_end = frames
    scene.frame_start, scene.frame_end = 1, frames
    for f in range(1, frames + 1):
        scene.frame_set(f)
    # bake: convert each garment to a plain mesh at the last frame
    for obj in objs:
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        parent, bone, ptype = obj.parent, obj.parent_bone, obj.parent_type
        world = obj.matrix_world.copy()
        bpy.ops.object.convert(target='MESH')
        new = bpy.context.active_object
        # the armature is applied: the baked mesh is in posed space
        new.parent = parent
        new.parent_type = ptype
        new.parent_bone = bone
        new.matrix_world = world
    body.modifiers.remove(col)
    scene.frame_set(0)
    H._update()


def crown_hair(basemesh, color, length=0.13, density=5000):
    """Hair lying back over the crown (parted, flat), not a fuzzy dome: strands
    from the top of the head brushed back and slightly down."""
    top, yc, on_body = _head(basemesh)
    grp = basemesh.vertex_groups.new(name='crown')
    for v in basemesh.data.vertices:
        if on_body(v) and top - v.co.z < 0.07 and v.co.y - yc > -0.075:
            grp.add([v.index], 1.0, 'REPLACE')
    mod = _hair_system(basemesh, 'Crown', 'crown', color, length, density)
    st = mod.particle_system.settings
    st.normal_factor = 0.004
    st.object_align_factor[1] = 0.22 * length     # back
    st.object_align_factor[2] = -0.06 * length    # a little down
    st.clump_factor = 0.7
    st.roughness_1 = 0.01
    st.child_radius = 0.02
    return mod


def crest(basemesh, arm, mat, height=0.15, length=0.32, width=0.06, lift=0.035):
    """A horsehair crest along the top of a helmet, front to back, arched and
    fuller at the back; parented to the head bone (call before posing)."""
    import human as H
    top, yc, _ = _head(basemesh)
    bm = bmesh.new()
    n = 16
    rows = []
    for i in range(n + 1):
        t = i / n
        y = yc - 0.11 + t * length           # front (-y) to back
        h = height * math.sin(math.pi * (0.15 + 0.8 * t)) * (0.7 + 0.5 * t)
        z0 = top + lift - 0.035 * (2 * t - 1) ** 2
        w = width * (0.6 + 0.6 * t)
        rows.append([bm.verts.new((sx * w, y + 0.02 * t * (h / height) * (1 if k else 0), z0 + (h if k else 0.0)))
                     for k in (0, 1) for sx in (-1, 1)])
    for a, b in zip(rows, rows[1:]):
        # each row: bottom-left, bottom-right, top-left, top-right
        for (i, j, k, l) in ((0, 2, 2, 0), (1, 3, 3, 1)):
            pass
        bm.faces.new((a[0], b[0], b[2], a[2]))     # left side
        bm.faces.new((a[3], b[3], b[1], a[1]))     # right side
        bm.faces.new((a[2], b[2], b[3], a[3]))     # top
    me = bpy.data.meshes.new('Crest')
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new('Crest', me)
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.use_smooth = True
    sub = obj.modifiers.new('Subdiv', 'SUBSURF')
    sub.levels = sub.render_levels = 2
    world = obj.matrix_world.copy()
    obj.parent = arm
    obj.parent_type = 'BONE'
    obj.parent_bone = 'head'
    obj.matrix_world = world
    H._update()
    return obj


def front_locks(basemesh, color, length=0.3, density=1800):
    """Thick locks from the temples and sides falling forward over the
    shoulders, framing the face (with falling_hair behind)."""
    top, yc, on_body = _head(basemesh)
    grp = basemesh.vertex_groups.new(name='locks')
    for v in basemesh.data.vertices:
        if not on_body(v):
            continue
        d = top - v.co.z
        y = v.co.y - yc
        if 0.03 < d < 0.12 and abs(v.co.x) > 0.05 and -0.06 < y < 0.03:
            grp.add([v.index], 1.0, 'REPLACE')
    mod = _hair_system(basemesh, 'Locks', 'locks', color, length, density)
    st = mod.particle_system.settings
    st.normal_factor = 0.02
    st.object_align_factor[0] = 0.0
    st.object_align_factor[1] = -0.06 * length    # forward
    st.object_align_factor[2] = -0.22 * length
    st.kink = 'NO'
    st.clump_factor = 0.9
    st.clump_shape = 0.4
    st.child_radius = 0.03
    st.roughness_1 = 0.02
    return mod


def horns(basemesh, arm, mat, length=0.11, radius=0.018):
    """Two short curved horns on the sides of a helmet, parented to the head."""
    import human as H
    top, yc, _ = _head(basemesh)
    made = []
    for sx in (-1, 1):
        bm = bmesh.new()
        n, seg = 10, 8
        prev = None
        for i in range(n + 1):
            t = i / n
            # out from the temple, curving up and forward
            c = Vector((sx * (0.075 + 0.6 * length * t), yc + 0.01 - 0.25 * length * t * t, top - 0.07 + 0.9 * length * t * t))
            r = radius * (1 - 0.92 * t)
            ring = [bm.verts.new(c + Vector((0, math.cos(2 * math.pi * k / seg) * r, math.sin(2 * math.pi * k / seg) * r)))
                    for k in range(seg)]
            if prev:
                for k in range(seg):
                    bm.faces.new((prev[k], prev[(k + 1) % seg], ring[(k + 1) % seg], ring[k]))
            prev = ring
        me = bpy.data.meshes.new('Horn')
        bm.to_mesh(me)
        bm.free()
        obj = bpy.data.objects.new('Horn', me)
        bpy.context.scene.collection.objects.link(obj)
        obj.data.materials.append(mat)
        for p in obj.data.polygons:
            p.use_smooth = True
        world = obj.matrix_world.copy()
        obj.parent = arm
        obj.parent_type = 'BONE'
        obj.parent_bone = 'head'
        obj.matrix_world = world
        made.append(obj)
    H._update()
    return made


def mail_nodes(tree, bsdf):
    """Ring mail: a fine voronoi bump (rings) for a metal material."""
    n = tree.nodes
    coord = n.new('ShaderNodeTexCoord')
    vor = n.new('ShaderNodeTexVoronoi')
    vor.inputs['Scale'].default_value = 260.0
    vor.feature = 'DISTANCE_TO_EDGE'
    tree.links.new(coord.outputs['Object'], vor.inputs['Vector'])
    bump = n.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 0.6
    tree.links.new(vor.outputs['Distance'], bump.inputs['Height'])
    tree.links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])


def band(basemesh, arm, name, bones, mat, z, width=0.06, offset=0.022):
    """A band around a limb or the body: at height `z` (for a 1.75 m man), or
    with `z` = (bone, t), at fraction t along both sides' `bone`_l/_r in the
    rest pose (the arms don't hang in it). Cut from a wider shell and
    subdivided, so it is a whole ring of the exact width (the body's faces
    are larger than a band)."""
    along = isinstance(z, (tuple, list))
    obj = shell(basemesh, arm, name, bones, mat, zmin=None if along else z - width / 2 - 0.06,
                zmax=None if along else z + width / 2 + 0.06, offset=offset, thickness=0.006, min_weight=0.3)
    if obj is None or not obj.data.polygons:
        return obj
    if along:
        inv = basemesh.matrix_world.inverted() @ arm.matrix_world
        axes = {}
        for sd in ('l', 'r'):
            b = arm.data.bones[z[0] + '_' + sd]
            h, t = inv @ b.head_local, inv @ b.tail_local
            axes[sd] = (h, t - h)

        def inside(co):
            h, d = axes['l' if co.x > 0 else 'r']
            u = (co - h).dot(d) / d.length_squared
            half = width * _scale / 2 / d.length
            return z[1] - half <= u <= z[1] + half
    else:
        lo, hi = (z - width / 2) * _scale, (z + width / 2) * _scale

        def inside(co):
            return lo <= co.z <= hi
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    for _ in range(3):
        bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=1, use_grid_fill=True)
    kill = [f for f in bm.faces if not all(inside(v.co) for v in f.verts)]
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(obj.data)
    bm.free()
    return obj


# where bands go: bones and height (1.75 m man)
BANDS = {
    'upperarm': (['upperarm_l', 'upperarm_r'], ('upperarm', 0.45)),
    'wrist': (['lowerarm_l', 'lowerarm_r', 'hand_l', 'hand_r'], ('lowerarm', 0.88)),
    'knee': (['calf_l', 'calf_r', 'thigh_l', 'thigh_r'], 0.5),
    'ankle': (['calf_l', 'calf_r', 'foot_l', 'foot_r'], 0.11),
    'waist': (['pelvis', 'spine_01'], 1.0),
    'neck': (['neck_01', 'spine_03', 'clavicle_l', 'clavicle_r'], 1.5),
}


def cape(basemesh, arm, mat, length=0.75, width=0.5, flare=0.15, wrap=0.0, train=0.0, folds=6):
    """A cape hanging from the shoulders down the back: a flap pinned along
    the shoulder line, behind the body, falling to `length`."""
    import human as H
    from mathutils import Matrix
    H._update()
    nk = arm.matrix_world @ arm.pose.bones['neck_01'].head
    f = flap('Cape', mat, width=width * _scale, length=length * _scale, curve=0.14 * _scale, folds=folds, depth=0.03,
             torn=False, ragged=0.01, flare=flare, wrap=wrap * _scale, train=train * _scale)
    f.matrix_world = Matrix.Translation((nk.x, nk.y + 0.1 * _scale, nk.z - 0.02 * _scale)) @ Matrix.Rotation(math.pi, 4, 'Z')
    world = f.matrix_world.copy()
    f.parent = arm
    f.parent_type = 'BONE'
    f.parent_bone = 'spine_03'
    f.matrix_world = world
    H._update()
    return f


def plume(basemesh, arm, mat, height=0.22, lean=0.05):
    """A tall feather plume standing up from the top of a helmet (a crest that
    rises rather than runs front to back)."""
    import human as H
    top, yc, _ = _head(basemesh)
    bm = bmesh.new()
    n, seg = 12, 8
    prev = None
    for i in range(n + 1):
        t = i / n
        c = Vector((0.0, yc + 0.02 + lean * t * t, top + 0.04 + height * t))
        r = 0.035 * math.sin(math.pi * min(1.0, 0.15 + t)) + 0.006
        ring = [bm.verts.new(c + Vector((math.cos(2 * math.pi * k / seg) * r, math.sin(2 * math.pi * k / seg) * r * 0.6, 0)))
                for k in range(seg)]
        if prev:
            for k in range(seg):
                bm.faces.new((prev[k], prev[(k + 1) % seg], ring[(k + 1) % seg], ring[k]))
        prev = ring
    me = bpy.data.meshes.new('Plume')
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new('Plume', me)
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.use_smooth = True
    world = obj.matrix_world.copy()
    obj.parent = arm
    obj.parent_type = 'BONE'
    obj.parent_bone = 'head'
    obj.matrix_world = world
    H._update()
    return obj


def brim(basemesh, arm, mat, radius=0.17, lift=0.075, curl=0.03, comb=0.05):
    """A morion's brim and comb: a curled-up brim around the helmet at the
    brow line and a raised comb front to back. Parented to the head."""
    import human as H
    top, yc, _ = _head(basemesh)
    made = []
    bm = bmesh.new()
    k = 40
    inner, outer = [], []
    for i in range(k):
        a = 2 * math.pi * i / k
        # boat-shaped: longer front to back, tips curling up at front and back
        rx, ry = radius * 0.85, radius * 1.15
        up = curl * (math.cos(a) ** 2)
        inner.append(bm.verts.new((math.sin(a) * 0.09, yc + math.cos(a) * 0.11, top - lift)))
        outer.append(bm.verts.new((math.sin(a) * rx, yc + math.cos(a) * ry, top - lift + up)))
    for i in range(k):
        bm.faces.new((inner[i], inner[(i + 1) % k], outer[(i + 1) % k], outer[i]))
    me = bpy.data.meshes.new('Brim')
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new('Brim', me)
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.append(mat)
    sol = obj.modifiers.new('Solidify', 'SOLIDIFY')
    sol.thickness = 0.005
    made.append(obj)
    if comb:
        c = crest(basemesh, arm, mat, height=comb, length=0.24, width=0.008, lift=0.0)
        made.append(c)
    for o in made[:1]:
        world = o.matrix_world.copy()
        o.parent = arm
        o.parent_type = 'BONE'
        o.parent_bone = 'head'
        o.matrix_world = world
    H._update()
    return made


def crown(basemesh, arm, mat, kind='spiked', height=0.07, gems=None):
    """A crown on the head (parented to the head bone): a band around the
    head at the brow, plus points ('spiked'), arches meeting over the top
    ('arched', a closed royal crown), or a tall cylinder ('tall', the
    Mesopotamian kind). 'circlet' is the band alone."""
    import human as H
    top, yc, _ = _head(basemesh)
    zb = top - 0.07
    rx, ry = 0.088, 0.105
    made = []
    bm = bmesh.new()
    k = 36
    lo, hi = [], []
    tall = height * 2.2 if kind == 'tall' else 0.035
    for i in range(k):
        a = 2 * math.pi * i / k
        x, y = math.sin(a) * rx, yc + math.cos(a) * ry
        lo.append(bm.verts.new((x, y, zb)))
        h = tall
        if kind == 'spiked':
            h += height * max(0.0, math.cos(a * 8)) ** 3
        hi.append(bm.verts.new((x * (1.04 if kind == 'tall' else 1.0), (y - yc) * (1.04 if kind == 'tall' else 1.0) + yc, zb + h)))
    for i in range(k):
        bm.faces.new((lo[i], lo[(i + 1) % k], hi[(i + 1) % k], hi[i]))
    if kind == 'tall':
        cap = bm.verts.new((0, yc, zb + tall))
        for i in range(k):
            bm.faces.new((hi[i], hi[(i + 1) % k], cap))
    if kind == 'arched':
        # two arches crossing over the top, and a small orb on the cross point
        for ax in (0, 1):
            prev = None
            for j in range(17):
                t = math.pi * j / 16
                if ax == 0:
                    p = (math.cos(t) * rx, yc, zb + 0.035 + math.sin(t) * 0.085)
                else:
                    p = (0.0, yc + math.cos(t) * ry, zb + 0.035 + math.sin(t) * 0.085)
                v1 = bm.verts.new((p[0] - (0.006 if ax else 0), p[1] - (0.006 if not ax else 0), p[2]))
                v2 = bm.verts.new((p[0] + (0.006 if ax else 0), p[1] + (0.006 if not ax else 0), p[2]))
                if prev:
                    bm.faces.new((prev[0], prev[1], v2, v1))
                prev = (v1, v2)
    me = bpy.data.meshes.new('Crown')
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new('Crown', me)
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.append(mat)
    sol = obj.modifiers.new('Solidify', 'SOLIDIFY')
    sol.thickness = 0.006
    made.append(obj)
    if kind == 'arched':
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.016, location=(0, yc, zb + 0.13))
        orb = bpy.context.active_object
        orb.data.materials.append(mat)
        made.append(orb)
    if gems:
        gm = B.material('gem', gems, roughness=0.15)
        for i in range(6):
            a = 2 * math.pi * i / 6
            bpy.ops.mesh.primitive_ico_sphere_add(radius=0.008, subdivisions=1,
                                                  location=(math.sin(a) * rx * 1.04, yc + math.cos(a) * ry * 1.04, zb + 0.017))
            g = bpy.context.active_object
            g.data.materials.append(gm)
            made.append(g)
    for o in made:
        world = o.matrix_world.copy()
        o.parent = arm
        o.parent_type = 'BONE'
        o.parent_bone = 'head'
        o.matrix_world = world
    H._update()
    return made


def belt_gear(basemesh, arm, metal, leather, pouch=True):
    """Detail on the belt: a metal buckle at the front and (pouch) a small
    leather pouch hanging at the right hip; on the pelvis bone."""
    import human as H
    me = basemesh.data
    body = basemesh.vertex_groups.get('body')
    z0, z1 = z(0.98), z(1.04)
    # the waist only: in the rest pose the hands hang at this height too
    ring = [v.co for v in me.vertices if z0 <= v.co.z <= z1 and abs(v.co.x) < 0.2 * _scale and
            (body is None or any(g.group == body.index and g.weight > 0.5 for g in v.groups))]
    if not ring:
        return []
    zc = (z0 + z1) / 2
    front = min((co for co in ring if abs(co.x) < 0.03), key=lambda co: co.y, default=None)
    made = []
    if front is not None:
        # a square frame with a tongue, standing proud of the belt
        bpy.ops.mesh.primitive_cube_add(size=1, location=(0, front.y - 0.026, zc))
        b = bpy.context.active_object
        b.scale = (0.05 * _scale, 0.01, 0.05 * _scale)
        bpy.ops.object.transform_apply(scale=True)
        sol = b.modifiers.new('Bevel', 'BEVEL')
        sol.width = 0.003
        b.data.materials.append(metal)
        made.append(b)
        bpy.ops.mesh.primitive_cube_add(size=1, location=(0, front.y - 0.031, zc))
        t = bpy.context.active_object
        t.scale = (0.03 * _scale, 0.004, 0.03 * _scale)
        bpy.ops.object.transform_apply(scale=True)
        t.data.materials.append(leather)
        made.append(t)
    if pouch:
        side = min(ring, key=lambda co: co.x)                  # the right side is -x
        bpy.ops.mesh.primitive_cube_add(size=1, location=(side.x - 0.02, side.y + 0.02, zc - 0.06 * _scale))
        pc = bpy.context.active_object
        pc.scale = (0.055, 0.14 * _scale, 0.15 * _scale)
        bpy.ops.object.transform_apply(scale=True)
        sub = pc.modifiers.new('Subdiv', 'SUBSURF')
        sub.levels = 2
        sub.render_levels = 2
        pc.data.materials.append(leather)
        made.append(pc)
        # the flap: a darker slab over the top half
        bpy.ops.mesh.primitive_cube_add(size=1, location=(side.x - 0.043, side.y + 0.02, zc - 0.035 * _scale))
        fl = bpy.context.active_object
        fl.scale = (0.008, 0.105 * _scale, 0.06 * _scale)
        bpy.ops.object.transform_apply(scale=True)
        bv = fl.modifiers.new('Bevel', 'BEVEL')
        bv.width = 0.004
        fl.data.materials.append(leather)
        made.append(fl)
    for o in made:
        world = o.matrix_world.copy()
        o.parent = arm
        o.parent_type = 'BONE'
        o.parent_bone = 'pelvis'
        o.matrix_world = world
    H._update()
    return made


def hem(name, mat, width=0.05, lift=0.004, inset=0.0):
    """A band along the bottom edge of garment `name` (a tinted or gold hem
    stripe): a copy of the garment trimmed to the faces within `width`
    (measured along the surface) of its lower open edge, or between `inset`
    and inset + width, lifted off the cloth by `lift`. Keeps the garment's
    rigging, so it follows the pose."""
    import heapq
    src = bpy.data.objects.get(name)
    if src is None or src.type != 'MESH':
        return None
    obj = src.copy()
    obj.data = src.data.copy()
    obj.name = 'Hem_' + name
    for col in src.users_collection:
        col.objects.link(obj)
    for m in list(obj.modifiers):
        if m.type in ('CLOTH', 'COLLISION'):
            obj.modifiers.remove(m)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    # a coarse garment (its rows further apart than the band) is subdivided
    # first (linearly: same surface), so the band can be cut from whole faces
    for _ in range(4):
        mean = sum(e.calc_length() for e in bm.edges) / max(1, len(bm.edges))
        if mean < width * _scale / 3:
            break
        bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=1, use_grid_fill=True)
    bm.verts.ensure_lookup_table()
    zs = [v.co.z for v in bm.verts]
    mid = (min(zs) + max(zs)) / 2
    # the lower open edge: boundary vertices below the garment's middle; a
    # closed (solidified) garment has none: then the lowest vertices of each
    # sector round the garment's axis (hems drop and fold unevenly)
    start = [v for v in bm.verts if v.is_boundary and v.co.z < mid]
    if not start:
        cx = sum(v.co.x for v in bm.verts) / len(bm.verts)
        cy = sum(v.co.y for v in bm.verts) / len(bm.verts)
        sector = {}
        for v in bm.verts:
            k = int((math.atan2(v.co.y - cy, v.co.x - cx) + math.pi) / (2 * math.pi) * 36) % 36
            sector.setdefault(k, []).append(v)
        for vs in sector.values():
            low = min(v.co.z for v in vs)
            start += [v for v in vs if v.co.z < low + 0.015]
    dist = {v.index: 0.0 for v in start}
    heap = [(0.0, v.index) for v in start]
    w0, w1 = inset * _scale, (inset + width) * _scale
    while heap:
        d, i = heapq.heappop(heap)
        if d > dist.get(i, 1e9) or d > w1:
            continue
        v = bm.verts[i]
        for e in v.link_edges:
            o = e.other_vert(v)
            nd = d + e.calc_length()
            if nd < dist.get(o.index, 1e9):
                dist[o.index] = nd
                heapq.heappush(heap, (nd, o.index))
    kill = [f for f in bm.faces if not all(w0 - 1e-6 <= dist.get(v.index, 1e9) <= w1 + 1e-6 for v in f.verts)]
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.normal_update()
    for v in bm.verts:
        v.co += v.normal * lift
    bm.to_mesh(obj.data)
    bm.free()
    if not obj.data.polygons:
        bpy.data.objects.remove(obj)
        return None
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    return obj


def chest_emblem(basemesh, arm, mat, kind='cross', size=0.16, z_at=1.3, lift=0.03, back=False):
    """A flat emblem on the chest front (a 'cross', or a 'disc'), standing
    `lift` off the skin so it sits on the garments; on the chest bone."""
    import human as H
    me = basemesh.data
    body = basemesh.vertex_groups.get('body')
    zc = z_at * _scale
    pts = [v.co for v in me.vertices if abs(v.co.z - zc) < 0.03 and abs(v.co.x) < 0.04 and
           (body is None or any(g.group == body.index and g.weight > 0.5 for g in v.groups))]
    if not pts:
        return []
    # the unposed body faces -y; `back` puts it on the back
    y = (max(co.y for co in pts) + lift) if back else (min(co.y for co in pts) - lift)
    s = size * _scale
    made = []
    if kind == 'v':
        # a V at the throat (an under-robe's crossed collar)
        for sx in (-1, 1):
            bpy.ops.mesh.primitive_cube_add(size=1, location=(sx * 0.2 * s, y, zc + 0.25 * s),
                                            rotation=(0, sx * 0.5, 0))
            o = bpy.context.active_object
            o.scale = (0.14 * s, 0.008, 0.6 * s)
            made.append(o)
    elif kind == 'disc':
        bpy.ops.mesh.primitive_cylinder_add(radius=s / 2, depth=0.008, location=(0, y, zc),
                                            rotation=(math.pi / 2, 0, 0), vertices=24)
        made.append(bpy.context.active_object)
    else:
        # (width, height, raise) of the upright and the cross-bar, in emblem sizes
        for w, h, dz in ((0.2, 1.0, 0.0), (0.66, 0.2, 0.18)):
            bpy.ops.mesh.primitive_cube_add(size=1, location=(0, y, zc + dz * s))
            o = bpy.context.active_object
            o.scale = (w * s, 0.008, h * s)
            made.append(o)
    for o in made:
        bpy.ops.object.select_all(action='DESELECT')
        o.select_set(True)
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.transform_apply(scale=True)
        bv = o.modifiers.new('Bevel', 'BEVEL')
        bv.width = 0.002
        o.data.materials.append(mat)
        world = o.matrix_world.copy()
        o.parent = arm
        o.parent_type = 'BONE'
        o.parent_bone = 'spine_03'
        o.matrix_world = world
    H._update()
    return made


def knob(basemesh, arm, mat, radius=0.022):
    """A knob on top of the head (a cap's finial); on the head bone."""
    import human as H
    top, yc, _ = _head(basemesh)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=radius * _scale, location=(0, yc, top + 0.012 * _scale))
    o = bpy.context.active_object
    o.data.materials.append(mat)
    bpy.ops.object.shade_smooth()
    world = o.matrix_world.copy()
    o.parent = arm
    o.parent_type = 'BONE'
    o.parent_bone = 'head'
    o.matrix_world = world
    H._update()
    return o


def plate_bands(n, gap=0.18):
    """Material nodes for overlapping metal plates: `n` bands per ~0.4 m of
    height with dark seams between them (lorica segmentata, tassets)."""
    def nodes(tree, bsdf):
        t = tree.nodes
        coord = t.new('ShaderNodeTexCoord')
        wave = t.new('ShaderNodeTexWave')
        wave.wave_type = 'BANDS'
        wave.bands_direction = 'Z'
        wave.wave_profile = 'SAW'
        wave.inputs['Scale'].default_value = n * 0.9
        tree.links.new(coord.outputs['Object'], wave.inputs['Vector'])
        seam = t.new('ShaderNodeMath')
        seam.operation = 'LESS_THAN'
        seam.inputs[1].default_value = gap
        tree.links.new(wave.outputs['Fac'], seam.inputs[0])
        mix = t.new('ShaderNodeMix')
        mix.data_type = 'RGBA'
        mix.blend_type = 'MULTIPLY'
        mix.inputs['A'].default_value = bsdf.inputs['Base Color'].default_value
        mix.inputs['B'].default_value = (0.15, 0.15, 0.15, 1.0)
        tree.links.new(seam.outputs['Value'], mix.inputs['Factor'])
        tree.links.new(mix.outputs['Result'], bsdf.inputs['Base Color'])
        bump = t.new('ShaderNodeBump')
        bump.inputs['Strength'].default_value = 0.5
        tree.links.new(wave.outputs['Fac'], bump.inputs['Height'])
        tree.links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
    return nodes


def topknot(basemesh, arm, mat, kind='topknot', length=0.3):
    """Hair on a shaven head: a 'topknot' (a bound knot on the crown, folded
    forward) or a 'ponytail' (a long tail from the back of the crown); on
    the head bone."""
    import human as H
    from mathutils import Vector
    top, yc, _ = _head(basemesh)
    s = _scale
    made = []
    if kind == 'topknot':
        bpy.ops.mesh.primitive_cylinder_add(radius=0.016 * s, depth=0.05 * s, vertices=12,
                                            location=(0, yc + 0.02 * s, top + 0.012 * s), rotation=(0.9, 0, 0))
        made.append(bpy.context.active_object)
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.022 * s, location=(0, yc - 0.01 * s, top + 0.03 * s))
        made.append(bpy.context.active_object)
    else:
        # a tapering tail: from the back of the crown down the back (+y behind)
        pts = [Vector((0, yc + 0.07 * s, top - 0.03 * s)), Vector((0, yc + 0.12 * s, top - 0.1 * s)),
               Vector((0, yc + 0.13 * s, top - 0.1 * s - length * 0.5 * s)),
               Vector((0, yc + 0.12 * s, top - 0.1 * s - length * s))]
        curve = bpy.data.curves.new('Ponytail', 'CURVE')
        curve.dimensions = '3D'
        sp = curve.splines.new('BEZIER')
        sp.bezier_points.add(len(pts) - 1)
        for bp, p in zip(sp.bezier_points, pts):
            bp.co = p
            bp.handle_left_type = bp.handle_right_type = 'AUTO'
        for i, bp in enumerate(sp.bezier_points):
            bp.radius = 1.0 - 0.6 * i / (len(pts) - 1)
        curve.bevel_depth = 0.022 * s
        curve.bevel_resolution = 3
        obj = bpy.data.objects.new('Ponytail', curve)
        bpy.context.scene.collection.objects.link(obj)
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.convert(target='MESH')
        obj = bpy.context.active_object
        made.append(obj)
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.026 * s, location=tuple(pts[0]))
        made.append(bpy.context.active_object)
    for o in made:
        o.data.materials.clear()
        o.data.materials.append(mat)
        world = o.matrix_world.copy()
        o.parent = arm
        o.parent_type = 'BONE'
        o.parent_bone = 'head'
        o.matrix_world = world
    H._update()
    return made


def trim(obj, inside, levels=3):
    """Subdivides `obj`'s mesh `levels` times and keeps only the faces whose
    vertices all pass `inside(co)` (rest coordinates, scaled to a 1.75 m
    man): narrow strips cut cleanly from a coarse garment."""
    if obj is None or not obj.data.polygons:
        return obj
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    for _ in range(levels):
        bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=1, use_grid_fill=True)
    kill = [f for f in bm.faces if not all(inside(v.co / _scale) for v in f.verts)]
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(obj.data)
    bm.free()
    return obj


def arm_patch(basemesh, arm, mat, side='l', t=0.3, size=0.06, out_by=0.035):
    """A small patch (a flag or unit patch) on the outside of the upper arm,
    `t` along the bone from the shoulder; on the upper arm bone."""
    import human as H
    from mathutils import Vector
    inv = basemesh.matrix_world.inverted() @ arm.matrix_world
    b = arm.data.bones['upperarm_' + side]
    h, tl = inv @ b.head_local, inv @ b.tail_local
    p = h.lerp(tl, t)
    out = 1.0 if side == 'l' else -1.0
    grp = basemesh.vertex_groups.get('upperarm_' + side)
    pts = [v.co for v in basemesh.data.vertices
           if grp is not None and any(g.group == grp.index and g.weight > 0.5 for g in v.groups)
           and (v.co - p).length < 0.07 * _scale]
    if not pts:
        return None
    o_pt = max(pts, key=lambda co: co.x * out)
    loc = o_pt + Vector((out * out_by * _scale, 0, 0))          # over sleeves (puffy ones ~0.026 out)
    bpy.ops.mesh.primitive_cube_add(size=1, location=tuple(loc))
    o = bpy.context.active_object
    o.scale = (0.006, size * _scale, size * 0.75 * _scale)
    bpy.ops.object.transform_apply(scale=True)
    o.data.materials.append(mat)
    world = o.matrix_world.copy()
    o.parent = arm
    o.parent_type = 'BONE'
    o.parent_bone = 'upperarm_' + side
    o.matrix_world = world
    H._update()
    return o
