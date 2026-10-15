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

def cloth(name, color, tint=False, rough=0.85, weave=0.25):
    """Woven cloth: a fine weave bump and a little color variation."""
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
        bsdf.inputs['Sheen Weight'].default_value = 0.4
        # darker in the folds and where the cloth meets the body
        ao = n.new('ShaderNodeAmbientOcclusion')
        ao.inputs['Distance'].default_value = 0.05
        mul = n.new('ShaderNodeMix')
        mul.data_type = 'RGBA'
        mul.blend_type = 'MULTIPLY'
        mul.inputs['Factor'].default_value = 0.75
        mul.inputs['A'].default_value = bsdf.inputs['Base Color'].default_value
        tree.links.new(ao.outputs['AO'], mul.inputs['B'])
        tree.links.new(mul.outputs['Result'], bsdf.inputs['Base Color'])
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
    # pigment, like real hair: dark swatches are mostly eumelanin, reddish
    # ones get some pheomelanin
    r, g, b = color
    lum = 0.3 * r + 0.59 * g + 0.11 * b
    hair.parametrization = 'MELANIN'
    hair.inputs['Melanin'].default_value = min(1.0, max(0.0, 1.0 - lum * 1.6))
    hair.inputs['Melanin Redness'].default_value = min(1.0, max(0.0, (r - b) * 2.5))
    hair.inputs['Roughness'].default_value = 0.55
    hair.inputs['Radial Roughness'].default_value = 0.7
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
    sub = new.modifiers.new('Subdiv', 'SUBSURF')
    sub.levels = sub.render_levels = 1
    return new


def skirt(basemesh, arm, name, mat, *, length=0.5, flare=1.08, front_only=False, panels=None, folds=0.06):
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
        # folds: radial ripples around the legs, deepening toward the hem
        a = math.atan2(v.co.x - cx, v.co.y - cy)
        k += folds * t * (0.6 * math.sin(a * 9.0) + 0.4 * math.sin(a * 23.0 + 1.3))
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
    head_z, yc, on_body = _head(basemesh)
    grp = basemesh.vertex_groups.new(name='beard')
    for v in me.vertices:
        if not on_body(v):
            continue
        c = v.co
        d = head_z - c.z
        y = c.y - yc
        # chin and jaw (the face half of the head, below the mouth) and the upper lip
        jaw = 0.165 < d < 0.235 and y < -0.01
        lip = 0.148 < d < 0.162 and y < -0.07 and abs(c.x) < 0.03
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


def pelt(basemesh, arm, color, dark, length=0.045, density=3000):
    """A heavy animal pelt over the shoulders and down the back (not the
    chest): a thick hide shell with fur particles, so the figure reads
    broader and rougher."""
    def keep(co):
        back = co.y > 0.0                       # the unposed MakeHuman faces -y
        return (co.z > 1.4) or (back and co.z > 1.12)
    obj = shell(basemesh, arm, 'Pelt', ['clavicle_l', 'clavicle_r', 'spine_02', 'spine_03', 'upperarm_l', 'upperarm_r'],
                hide('pelt', color, dark), zmin=1.12, zmax=1.47, offset=0.035, thickness=0.025, min_weight=0.3, keep=keep)
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
    hair = [c for c in basemesh.children if c.type == 'MESH' and any(
        (s.material and 'hair' in s.material.name.lower()) for s in c.material_slots) or 'long' in c.name.lower()
        or 'short' in c.name.lower() or 'ponytail' in c.name.lower()]
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


def flap(name, mat, width=0.26, length=0.34, folds=5, depth=0.04, ragged=0.05, curve=0.12, seed=1):
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
        i = min(nx, i + rnd.choice((2, 3, 3, 4)))
        edges.append(i)
    slits = set(edges[1:-1])
    hem = [0.0] * (nx + 1)
    for a, b in zip(edges, edges[1:]):
        base = length * (1.0 - rnd.uniform(0.0, 0.35))
        tilt = rnd.uniform(-0.08, 0.08) * length
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
            row.append(bm.verts.new((u * width * (1 + 0.15 * t), yy - curve * 0.5, zz)))
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
    sub = obj.modifiers.new('Subdiv', 'SUBSURF')
    sub.levels = sub.render_levels = 1
    return obj


def loincloth(basemesh, arm, mat, back_mat=None, width=0.34, length=0.42, back_length=0.36, seed=3):
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
        f = flap('Loin' + side, m, width=width * _scale, length=ln * _scale, curve=0.06 * _scale, seed=seed + (side == 'B'))
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


def crest(basemesh, arm, mat, height=0.12, length=0.3, width=0.045, lift=0.03):
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
