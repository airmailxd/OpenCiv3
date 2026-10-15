"""Brutal hand weapons for the remaster (Blender side), beside props.py:
`war_club`, a gnarled haft flaring into a heavy knob studded with stones.
Same convention as props: grip at the origin, long axis +Z."""
import math
import random

import bmesh
import bpy
from mathutils import Vector, noise

import bscene as B


def _wood(name, color):
    def nodes(tree, bsdf):
        n = tree.nodes
        coord = n.new('ShaderNodeTexCoord')
        wave = n.new('ShaderNodeTexWave')
        wave.bands_direction = 'Z'
        wave.inputs['Scale'].default_value = 6.0
        wave.inputs['Distortion'].default_value = 8.0
        tree.links.new(coord.outputs['Object'], wave.inputs['Vector'])
        bump = n.new('ShaderNodeBump')
        bump.inputs['Strength'].default_value = 0.5
        tree.links.new(wave.outputs['Fac'], bump.inputs['Height'])
        tree.links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
    return B.material(name, color, roughness=0.8, nodes=nodes)


def war_club(colors, length=0.62, knob=0.075, studs=6, seed=7, scale=1.0):
    """A gnarled wooden club: a thin grip flaring into a heavy, lumpy knob,
    with knapped stone studs driven into the head and a rawhide grip."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    rings, seg = 24, 10
    prev = None
    first = None
    for i in range(rings + 1):
        t = i / rings
        z = -0.08 + t * length
        # grip radius, flaring into the head over the last third
        r = 0.017 + 0.01 * t + knob * max(0.0, (t - 0.62) / 0.38) ** 1.6
        if t > 0.96:
            r *= 0.75
        ring = []
        for k in range(seg):
            a = 2 * math.pi * k / seg
            p = Vector((math.cos(a) * r, math.sin(a) * r, z))
            # gnarls: noise displacement, larger on the head
            d = noise.noise(p * 18 + Vector((seed, 0, 0))) * (0.004 + 0.02 * t ** 3)
            p.x += math.cos(a) * d
            p.y += math.sin(a) * d
            p.x += 0.01 * math.sin(t * 5.0)        # a crooked haft
            ring.append(bm.verts.new(p))
        if prev:
            for k in range(seg):
                bm.faces.new((prev[k], prev[(k + 1) % seg], ring[(k + 1) % seg], ring[k]))
        else:
            first = ring
        prev = ring
    bm.faces.new(list(reversed(first)))
    bm.faces.new(prev)
    me = bpy.data.meshes.new('war_club')
    bm.to_mesh(me)
    bm.free()
    club = bpy.data.objects.new('war_club', me)
    bpy.context.scene.collection.objects.link(club)
    club.data.materials.append(_wood('club_wood', colors.get('wood', '#5a3e28')))
    for p in club.data.polygons:
        p.use_smooth = True
    sub = club.modifiers.new('Subdiv', 'SUBSURF')
    sub.levels = sub.render_levels = 2
    # stone studs in the knob
    stone = B.material('club_stone', colors.get('stone', '#9a8a74'), roughness=0.85)
    parts = [club]
    for k in range(studs):
        a = 2 * math.pi * k / studs + rnd.random() * 0.4
        zz = -0.08 + length * (0.8 + 0.12 * rnd.random())
        r = knob * 0.9
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=0.018 + 0.008 * rnd.random(),
                                              location=(math.cos(a) * r, math.sin(a) * r, zz))
        st = bpy.context.active_object
        st.scale = (1.0, 0.6, 1.6)
        st.rotation_euler = (rnd.random(), rnd.random(), a)
        st.data.materials.append(stone)
        parts.append(st)
    # rawhide grip wrap
    leather = B.material('club_grip', colors.get('leather', '#4a3020'), roughness=0.7)
    bpy.ops.mesh.primitive_cylinder_add(radius=0.021, depth=0.14, location=(0, 0, -0.01))
    grip = bpy.context.active_object
    grip.data.materials.append(leather)
    parts.append(grip)
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = club
    bpy.ops.object.convert(target='MESH')
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.scale = (scale, scale, scale)
    bpy.ops.object.transform_apply(scale=True)
    return obj
