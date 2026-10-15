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
    """Tones the hair asset (a child of the body) toward `color` (sRGB)."""
    for child in basemesh.children:
        if 'hair' in child.name.lower() or any('hair' in (s.material.name.lower() if s.material else '') for s in child.material_slots):
            tone_skin(child, color, texture='')


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
