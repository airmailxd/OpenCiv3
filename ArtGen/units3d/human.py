"""Human figures for the unit renders (Blender side): a realistic body from
MPFB (MakeHuman for Blender; its CC0 system assets) with the game_engine rig,
skin, eyes, brows and hair, posed by aiming bones.

Set up once per machine: install the MPFB extension and unpack MakeHuman's
CC0 system asset pack into MPFB's user data folder (see README.md).
"""
import math

import bpy
from mathutils import Matrix, Vector

from bl_ext.user_default.mpfb.services.humanservice import HumanService
from bl_ext.user_default.mpfb.services.locationservice import LocationService
from bl_ext.user_default.mpfb.services.targetservice import TargetService

import os


def _asset(kind, name, ext):
    """The path of an MPFB library asset, e.g. ('skins', 'old_african_male', 'mhmat')."""
    path = os.path.join(LocationService.get_user_data(kind), name, f'{name}.{ext}')
    if not os.path.isfile(path):
        raise FileNotFoundError(path)
    return path


def body(gender=1.0, age=0.5, muscle=0.75, weight=0.55, height=0.55, proportions=0.6,
         race=None, skin='middleage_caucasian_male', hair='short02', eyebrows='eyebrow001',
         eyes='brown', subdiv=1, skin_tone=None, hair_tone=None):
    """A human (basemesh, armature). Macro values are MakeHuman's 0..1 sliders.
    `skin_tone` (an sRGB color) multiplies the skin texture: the catalog's
    'skin' color, so a unit keeps its original's skin color."""
    macro = TargetService.get_default_macro_info_dict()
    macro.update(gender=gender, age=age, muscle=muscle, weight=weight, height=height, proportions=proportions)
    if race:
        macro['race'] = dict(race)
    basemesh = HumanService.create_human(scale=0.1, macro_detail_dict=macro, feet_on_ground=True)
    # the build is shape keys on a neutral mesh: bake it, so garments made
    # from the body's surface fit the final body
    TargetService.bake_targets(basemesh)
    arm = HumanService.add_builtin_rig(basemesh, 'game_engine', import_weights=True)
    HumanService.set_character_skin(_asset('skins', skin, 'mhmat'), basemesh, skin_type='ENHANCED_SSS')
    import garments
    garments.fit_to(basemesh)
    if skin_tone:
        tone_skin(basemesh, skin_tone)
    if hair_tone and hair:
        hair_color(basemesh, hair_tone)
    for kind, name in (('eyebrows', eyebrows), ('eyelashes', 'eyelashes01'), ('eyes', 'high-poly'), ('hair', hair)):
        if name:
            try:
                HumanService.add_mhclo_asset(_asset(kind, name, 'mhclo'), basemesh, asset_type=kind.capitalize().rstrip('s'),
                                             subdiv_levels=subdiv, material_type='MAKESKIN')
            except FileNotFoundError as e:
                print('missing asset', e)
    if subdiv:
        mod = basemesh.modifiers.new('Subdiv', 'SUBSURF')
        mod.levels = mod.render_levels = subdiv
    return basemesh, arm


def grime(basemesh, amount=0.35, paint=None):
    """Weathers the skin: dirt in blotches (a noise darkening the diffuse
    color), a sweaty sheen, and optional war paint, dark bands across the
    face and chest (`paint` = sRGB color)."""
    import bscene as B
    done = set()

    def visit(tree):
        if tree is None or tree.name in done:
            return
        done.add(tree.name)
        for node in list(tree.nodes):
            if node.type == 'GROUP':
                visit(node.node_tree)
        bsdf = next((n for n in tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
        if bsdf is None or not bsdf.inputs['Base Color'].is_linked:
            return
        src = bsdf.inputs['Base Color'].links[0].from_socket
        n = tree.nodes
        coord = n.new('ShaderNodeTexCoord')
        noise = n.new('ShaderNodeTexNoise')
        noise.inputs['Scale'].default_value = 7.0
        noise.inputs['Detail'].default_value = 8.0
        noise.inputs['Roughness'].default_value = 0.7
        tree.links.new(coord.outputs['Object'], noise.inputs['Vector'])
        ramp = n.new('ShaderNodeValToRGB')
        ramp.color_ramp.elements[0].position = 0.4
        ramp.color_ramp.elements[0].color = (0.2, 0.15, 0.1, 1)
        ramp.color_ramp.elements[1].position = 0.72
        ramp.color_ramp.elements[1].color = (1, 1, 1, 1)
        tree.links.new(noise.outputs['Fac'], ramp.inputs['Fac'])
        dirt = n.new('ShaderNodeMix')
        dirt.data_type = 'RGBA'
        dirt.blend_type = 'MULTIPLY'
        dirt.inputs['Factor'].default_value = amount
        tree.links.new(src, dirt.inputs['A'])
        tree.links.new(ramp.outputs['Color'], dirt.inputs['B'])
        out = dirt.outputs['Result']
        # darker creases (armpits, under the pecs, abs, knees): ambient occlusion
        ao = n.new('ShaderNodeAmbientOcclusion')
        ao.inputs['Distance'].default_value = 0.06
        crease = n.new('ShaderNodeMix')
        crease.data_type = 'RGBA'
        crease.blend_type = 'MULTIPLY'
        crease.inputs['Factor'].default_value = 1.0
        aoramp = n.new('ShaderNodeValToRGB')
        aoramp.color_ramp.elements[0].color = (0.35, 0.24, 0.17, 1)
        aoramp.color_ramp.elements[1].position = 0.85
        tree.links.new(ao.outputs['AO'], aoramp.inputs['Fac'])
        tree.links.new(out, crease.inputs['A'])
        tree.links.new(aoramp.outputs['Color'], crease.inputs['B'])
        out = crease.outputs['Result']
        if paint:
            wave = n.new('ShaderNodeTexWave')
            wave.wave_type = 'BANDS'
            wave.bands_direction = 'Z'
            wave.inputs['Scale'].default_value = 3.2
            wave.inputs['Distortion'].default_value = 2.0
            tree.links.new(coord.outputs['Object'], wave.inputs['Vector'])
            band = n.new('ShaderNodeValToRGB')
            band.color_ramp.elements[0].position = 0.88
            band.color_ramp.elements[0].color = (0, 0, 0, 1)
            band.color_ramp.elements[1].position = 0.93
            band.color_ramp.elements[1].color = (1, 1, 1, 1)
            tree.links.new(wave.outputs['Fac'], band.inputs['Fac'])
            pm = n.new('ShaderNodeMix')
            pm.data_type = 'RGBA'
            pm.inputs['B'].default_value = (*B.srgb_to_linear(paint), 1)
            tree.links.new(band.outputs['Color'], pm.inputs['Factor'])
            tree.links.new(out, pm.inputs['A'])
            out = pm.outputs['Result']
        tree.links.new(out, bsdf.inputs['Base Color'])
        # a sweaty sheen
        if not bsdf.inputs['Roughness'].is_linked:
            bsdf.inputs['Roughness'].default_value = 0.6
        # weathered skin: little specular, no plastic sheen on the shoulders
        bsdf.inputs['Specular IOR Level'].default_value = 0.05
        bsdf.inputs['Coat Weight'].default_value = 0.0

    for slot in basemesh.material_slots:
        if slot.material and slot.material.use_nodes:
            visit(slot.material.node_tree)
            # MPFB's skin group takes its roughness and coat from group inputs
            for node in slot.material.node_tree.nodes:
                if node.type == 'GROUP':
                    for name, value in (('Roughness', 0.66), ('Clearcoat', 0.0), ('Clearcoat Roughness', 0.8)):
                        if name in node.inputs:
                            node.inputs[name].default_value = value


def tone_skin(basemesh, color, strength=1.0, texture='diffuse'):
    """Multiplies the skin's diffuse texture (in MPFB's skin node group, which
    every body material shares) by `color` (sRGB), normalized so the
    texture's average lands on `color`."""
    import bscene as B
    lin = B.srgb_to_linear(color)
    done = set()

    def visit(tree):
        if tree is None or tree.name in done:
            return
        done.add(tree.name)
        for node in list(tree.nodes):
            if node.type == 'GROUP':
                visit(node.node_tree)
            if node.type != 'TEX_IMAGE' or not node.image or texture not in node.image.name.lower():
                continue
            links = [l for l in tree.links if l.from_node == node and l.from_socket.name == 'Color']
            mul = tree.nodes.new('ShaderNodeMix')
            mul.data_type = 'RGBA'
            mul.blend_type = 'MULTIPLY'
            mul.inputs['Factor'].default_value = strength
            # the skin textures average about 0.45 linear: scale so they land on `color`
            mul.inputs['B'].default_value = tuple(min(4.0, c / 0.45) for c in lin) + (1.0,)
            tree.links.new(node.outputs['Color'], mul.inputs['A'])
            for l in links:
                tree.links.new(mul.outputs['Result'], l.to_socket)

    for slot in basemesh.material_slots:
        if slot.material and slot.material.use_nodes:
            visit(slot.material.node_tree)


def hair_color(basemesh, color):
    """Tones the hair asset (a child of the body) toward `color` (sRGB) and
    makes it matte (matted hair, not a shiny shell)."""
    for child in basemesh.children:
        if 'hair' in child.name.lower() or any('hair' in (s.material.name.lower() if s.material else '') for s in child.material_slots):
            tone_skin(child, color, texture='')
            matte(child)


def matte(obj, roughness=0.85, specular=0.08):
    """Kills the sheen of every Principled BSDF in obj's materials (and groups)."""
    done = set()

    def visit(tree):
        if tree is None or tree.name in done:
            return
        done.add(tree.name)
        for node in tree.nodes:
            if node.type == 'GROUP':
                visit(node.node_tree)
            if node.type == 'BSDF_PRINCIPLED':
                if not node.inputs['Roughness'].is_linked:
                    node.inputs['Roughness'].default_value = roughness
                node.inputs['Specular IOR Level'].default_value = specular
                node.inputs['Coat Weight'].default_value = 0.0
                node.inputs['Sheen Weight'].default_value = 0.2
    for slot in obj.material_slots:
        if slot.material and slot.material.use_nodes:
            visit(slot.material.node_tree)


# ------------------------------------------------------------ posing

def _update():
    bpy.context.view_layer.update()


def aim(arm, bone, direction, roll_up=None):
    """Turns pose bone `bone` so it points along `direction` (armature space),
    keeping its head where it is."""
    pb = arm.pose.bones[bone]
    d = Vector(direction).normalized()
    m = pb.matrix.copy()
    y = (m.to_3x3() @ Vector((0, 1, 0))).normalized()
    rot = y.rotation_difference(d).to_matrix().to_4x4()
    head = m.translation.copy()
    new = rot @ Matrix.Translation(-head) @ m
    new.translation = head
    pb.matrix = new
    _update()


def pose(arm, aims):
    """Aims bones in order (parents first): a list of (bone, direction)."""
    for bone, direction in aims:
        aim(arm, bone, direction)


def bone_matrix(arm, bone):
    """The world matrix of a pose bone (its head; +Y along the bone)."""
    return arm.matrix_world @ arm.pose.bones[bone].matrix


def attach(obj, arm, bone, offset=Matrix.Identity(4)):
    """Parents `obj` to `bone` so it follows the pose; `offset` places it in
    the bone's space (at the bone's tail = 0, 0 for hand bones' grip)."""
    obj.parent = arm
    obj.parent_type = 'BONE'
    obj.parent_bone = bone
    pb = arm.pose.bones[bone]
    # parented to a bone, the child sits at the bone's tail
    obj.matrix_parent_inverse = Matrix.Identity(4)
    obj.matrix_basis = Matrix.Translation((0, -pb.length, 0)) @ offset
    _update()


def plant(root, basemesh):
    """Moves the root so the posed body's lowest point (its feet) is on the ground."""
    _update()
    dg = bpy.context.evaluated_depsgraph_get()
    ev = basemesh.evaluated_get(dg)
    me = ev.to_mesh()
    mw = ev.matrix_world
    zmin = min((mw @ v.co).z for v in me.vertices)
    ev.to_mesh_clear()
    root.location.z -= zmin
    _update()


def rooted(arm):
    """An empty that holds the human, turned so it faces +y like every unit
    model (MakeHuman's humans face -y); render_directions turns the empty."""
    root = bpy.data.objects.new('Root', None)
    bpy.context.scene.collection.objects.link(root)
    arm.parent = root
    arm.rotation_euler.z = math.pi
    _update()
    return root


def everything(basemesh, arm):
    """Every object of the human (mesh, rig, assets)."""
    return [arm, basemesh] + [c for c in basemesh.children] + [c for c in arm.children if c is not basemesh]


def fists(arm, curl=75, thumb=30, sides='lr'):
    """Clenches the hands: every finger curled toward the palm."""
    for side in sides:
        for f in ('index', 'middle', 'ring', 'pinky'):
            for k in (1, 2, 3):
                pb = arm.pose.bones.get(f'{f}_0{k}_{side}')
                if pb:
                    pb.rotation_mode = 'XYZ'
                    pb.rotation_euler.x = math.radians(curl)
        for k in (1, 2):
            pb = arm.pose.bones.get(f'thumb_0{k}_{side}')
            if pb:
                pb.rotation_mode = 'XYZ'
                pb.rotation_euler.x = math.radians(thumb)
    _update()


def grip_point(arm, bone='hand_r', along=0.55):
    """A point inside the closed hand: `along` of the way from the wrist (the
    hand bone's head) to the knuckles (its tail), in world space."""
    _update()
    pb = arm.pose.bones[bone]
    head = arm.matrix_world @ pb.head
    tail = arm.matrix_world @ pb.tail
    return head.lerp(tail, along)


def hold(obj, arm, bone, at, up, forward=(0, -1, 0)):
    """Places a prop (long axis +Z, front +Y) in world space with its origin
    at `at`, its +Z along `up` and its +Y as close to `forward` as possible,
    then parents it to `bone` keeping that placement (it follows the bone)."""
    from mathutils import Vector
    z = Vector(up).normalized()
    f = Vector(forward)
    x = f.cross(z)
    if x.length < 1e-6:
        x = Vector((1, 0, 0))
    x.normalize()
    y = z.cross(x).normalized()
    m = Matrix((x, y, z)).transposed().to_4x4()
    m.translation = Vector(at)
    obj.matrix_world = m
    _update()
    world = obj.matrix_world.copy()
    obj.parent = arm
    obj.parent_type = 'BONE'
    obj.parent_bone = bone
    obj.matrix_world = world
    _update()


def bulk(arm, k=1.18):
    """Thickens the upper body: the upper arms, forearms and the chest and
    back (lats, traps) scaled across their bones, lengths unchanged; each
    hand grows a little with its arm (Civ3's big hands)."""
    across = {'upperarm_l': k, 'upperarm_r': k, 'lowerarm_l': k * 0.95, 'lowerarm_r': k * 0.95,
              'spine_03': 1 + (k - 1) * 0.7, 'clavicle_l': 1 + (k - 1) * 0.5, 'clavicle_r': 1 + (k - 1) * 0.5}
    for name, f in across.items():
        pb = arm.pose.bones.get(name)
        if pb is None:
            continue
        pb.scale = (f, 1.0, f)
    # forearms and hands inherit the upper arms' scale (Civ3's figures have
    # big hands); counter part of it on the forearms
    for side in 'lr':
        pb = arm.pose.bones.get('lowerarm_' + side)
        if pb:
            pb.scale = (k * 0.95 / k, 1.0, k * 0.95 / k)
    _update()
