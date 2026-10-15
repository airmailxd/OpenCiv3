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


# ------------------------------------------------------------ materials

def cloth(name, color, tint=False, rough=0.85, weave=0.25):
    """Woven cloth: a fine weave bump and a little color variation."""
    def nodes(tree, bsdf):
        n = tree.nodes
        tex = n.new('ShaderNodeTexNoise')
        tex.inputs['Scale'].default_value = 220.0
        tex.inputs['Detail'].default_value = 2.0
        bump = n.new('ShaderNodeBump')
        bump.inputs['Strength'].default_value = weave
        tree.links.new(tex.outputs['Fac'], bump.inputs['Height'])
        tree.links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
        bsdf.inputs['Sheen Weight'].default_value = 0.4
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
        wave.inputs['Scale'].default_value = 9.0
        wave.inputs['Distortion'].default_value = 3.0
        wave.inputs['Detail'].default_value = 4.0
        tree.links.new(coord.outputs['Object'], wave.inputs['Vector'])
        ramp = n.new('ShaderNodeValToRGB')
        ramp.color_ramp.elements[0].color = (*B.srgb_to_linear(dark), 1)
        ramp.color_ramp.elements[1].color = (*B.srgb_to_linear(color), 1)
        ramp.color_ramp.elements[0].position = 0.35
        ramp.color_ramp.elements[1].position = 0.65
        tree.links.new(wave.outputs['Fac'], ramp.inputs['Fac'])
        tree.links.new(ramp.outputs['Color'], bsdf.inputs['Base Color'])
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
    hair.parametrization = 'COLOR'
    hair.inputs['Color'].default_value = (*B.srgb_to_linear(color), 1)
    hair.inputs['Roughness'].default_value = 0.45
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
    belong to `bones` (and lie between zmin and zmax on the unposed body),
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
        z = v.co.z
        if zmin is not None and z < zmin or zmax is not None and z > zmax:
            continue
        if keep and not keep(v.co):
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
    sub = new.modifiers.new('Subdiv', 'SUBSURF')
    sub.levels = sub.render_levels = 1
    return new


def skirt(basemesh, arm, name, mat, *, length=0.5, flare=1.08, front_only=False, panels=None):
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
    drop = [f for f in bm.faces if not all(keep(v) for v in f.verts)]
    bmesh.ops.delete(bm, geom=drop, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    for v in bm.verts:
        t = (ztop - v.co.z) / max(1e-6, ztop - zbot)
        k = 1.0 + (flare - 1.0) * t
        v.co.x = cx + (v.co.x - cx) * k
        v.co.y = cy + (v.co.y - cy) * k
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


def beard(basemesh, color, length=0.018, density=900):
    """A short full beard: hair particles on the jaw, chin and upper lip of
    the unposed body (MakeHuman's head: top at head_z, chin about 0.22 below)."""
    me = basemesh.data
    body = basemesh.vertex_groups.get('body')

    def on_body(v):
        return body is None or any(g.group == body.index and g.weight > 0.5 for g in v.groups)
    head_z = max(v.co.z for v in me.vertices if on_body(v))
    grp = basemesh.vertex_groups.new(name='beard')
    for v in me.vertices:
        if not on_body(v):
            continue
        c = v.co
        d = head_z - c.z
        # chin and jaw (front 2/3 of the head, below the mouth) and the upper lip
        jaw = 0.165 < d < 0.235 and c.y < 0.01
        lip = 0.148 < d < 0.162 and c.y < -0.07 and abs(c.x) < 0.03
        if jaw or lip:
            grp.add([v.index], 1.0, 'REPLACE')
    return _hair_system(basemesh, 'Beard', 'beard', color, length, density)


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
