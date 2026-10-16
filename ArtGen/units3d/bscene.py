"""The Blender side of the unit renders: the scene every unit is rendered in.

Run inside Blender (blender -b --factory-startup --python-exit-code 1
--python <script>), never in the ArtGen venv. It matches the camera and
light of the earlier software renderer (ArtGen/units/render.py), so a remade
unit stands where the old one stood:

* Orthographic camera looking north, tilted down 30 degrees (Civ3's 2:1
  diamond). World x = east, y = north, z = up, meters; a unit model faces +y,
  and turning it by facing_deg(row) faces it along flic row `row` (SW, S, SE,
  E, NE, N, NW, W).
* A world point (x, y, z) lands on sheet pixel
  (ax + x * ppm, ay - (y sin30 + z cos30) * ppm), where (ax, ay) is the
  anchor and ppm the pixels per meter, both at the sheet's resolution (2x
  the flic's).
* Key light from the northwest, high; soft fill from the front right and a
  rim light from behind; a soft sky. The ground is a shadow catcher: only
  the unit's shadow on it is rendered.

Each render writes, per direction: the unit (RGBA, straight alpha), the tint
mask (the AOV `tint`: 1 where a material takes the civ color) and the shadow
(the shadow catcher pass), all at the canvas size. compose.py turns them
into the game's base and tint layers.
"""
import math
import os

import bpy
from mathutils import Matrix, Vector

ELEVATION = 30.0
SIN_E, COS_E = math.sin(math.radians(ELEVATION)), math.cos(math.radians(ELEVATION))

# The same light directions as ArtGen/units/render.py (unit vectors toward the light).
_KEY_EL = math.radians(66)
KEY = Vector((-math.cos(_KEY_EL) * 0.7071, math.cos(_KEY_EL) * 0.7071, math.sin(_KEY_EL)))
FILL = Vector((0.55, -0.65, 0.35)).normalized()
RIM = Vector((0.35, 0.85, 0.25)).normalized()
FACE = Vector((0.0, -1.0, 0.12)).normalized()      # low, from the camera's side: faces under brims

DIR_VECTORS = [(-1, -1), (0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0)]


def facing_deg(row):
    dx, dy = DIR_VECTORS[row]
    return math.degrees(math.atan2(dy, dx)) - 90.0


class Look:
    """The lighting shared by every unit (one look for the whole set)."""
    key = 5.6           # sun strengths (W/m^2): a strong NW key and deep
    fill = 0.30         # shading on the southeast side, like Civ3's units
    rim = 0.7
    face = 0.75         # a weak, shadowless front fill (faces read as dark blobs under helmets)
    sky = (0.60, 0.64, 0.70)
    sky_strength = 0.32
    samples = 96
    exposure = 0.0


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = Look.samples
    scene.cycles.use_denoising = True
    scene.render.film_transparent = True
    scene.render.filter_size = 1.2
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'None'
    scene.view_settings.exposure = Look.exposure
    scene.display_settings.display_device = 'sRGB'
    return scene


def use_gpu(scene):
    """Render on the GPU when there is one (OptiX, CUDA, Metal), else the CPU."""
    prefs = bpy.context.preferences.addons['cycles'].preferences
    for kind in ('OPTIX', 'CUDA', 'METAL', 'HIP', 'ONEAPI'):
        try:
            prefs.compute_device_type = kind
        except TypeError:
            continue
        prefs.get_devices()
        gpus = [d for d in prefs.devices if d.type == kind]
        if gpus:
            for d in prefs.devices:
                d.use = d.type == kind
            scene.cycles.device = 'GPU'
            return kind
    scene.cycles.device = 'CPU'
    return 'CPU'


def _sun(name, direction, strength, shadow=True, angle_deg=4.0):
    data = bpy.data.lights.new(name, 'SUN')
    data.energy = strength
    data.angle = math.radians(angle_deg)
    data.use_shadow = shadow
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    # a sun shines along its local -Z: point -Z away from `direction`
    obj.rotation_euler = direction.to_track_quat('Z', 'Y').to_euler()
    return obj


def lights():
    """Key, fill and rim suns and the sky. The key is a little steeper
    than the old renderer's (62 degrees, between its key and shadow lights),
    so the ground shadow stays compact on the map."""
    key = _sun('Key', KEY, Look.key, angle_deg=2.5)   # crisp shadow edges
    _sun('Fill', FILL, Look.fill, shadow=False, angle_deg=20.0)
    _sun('Rim', RIM, Look.rim, shadow=False, angle_deg=10.0)
    if Look.face:
        _sun('Face', FACE, Look.face, shadow=False, angle_deg=30.0)
    world = bpy.data.worlds.new('Sky')
    world.use_nodes = True
    bg = world.node_tree.nodes['Background']
    bg.inputs['Color'].default_value = (*Look.sky, 1.0)
    bg.inputs['Strength'].default_value = Look.sky_strength
    bpy.context.scene.world = world
    return key


def ground():
    """A large shadow catcher plane at z = 0."""
    bpy.ops.mesh.primitive_plane_add(size=40.0, location=(0, 0, 0))
    plane = bpy.context.active_object
    plane.name = 'Ground'
    plane.is_shadow_catcher = True
    return plane


def camera(width, height, ppm, anchor):
    """The orthographic camera putting world point (0, 0, 0) on pixel `anchor`
    of a width x height image at `ppm` pixels per meter."""
    scene = bpy.context.scene
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.resolution_percentage = 100
    scene.render.pixel_aspect_x = scene.render.pixel_aspect_y = 1.0
    data = bpy.data.cameras.new('Cam')
    data.type = 'ORTHO'
    data.ortho_scale = max(width, height) / ppm
    data.sensor_fit = 'AUTO'
    data.clip_start = 0.01
    data.clip_end = 200.0
    cam = bpy.data.objects.new('Cam', data)
    scene.collection.objects.link(cam)
    cam.rotation_euler = (math.radians(90.0 - ELEVATION), 0.0, 0.0)
    right = Vector((1.0, 0.0, 0.0))
    up = Vector((0.0, SIN_E, COS_E))
    back = Vector((0.0, -COS_E, SIN_E))
    # the world point at the image center, in screen units (meters)
    xc = (width / 2.0 - anchor[0]) / ppm
    uc = (anchor[1] - height / 2.0) / ppm
    cam.location = right * xc + up * uc + back * 50.0
    scene.camera = cam
    return cam


def tint_aov():
    """The view layer's `tint` AOV (a value: 1 where a material takes the civ color)."""
    layer = bpy.context.view_layer
    if 'tint' not in [a.name for a in layer.aovs]:
        aov = layer.aovs.add()
        aov.name = 'tint'
        aov.type = 'VALUE'
    layer.cycles.use_pass_shadow_catcher = True
    # ambient occlusion over a short distance: the crevices between parts
    # (compose.py darkens them, for the original sprites' local contrast)
    layer.use_pass_ambient_occlusion = True
    if bpy.context.scene.world:
        bpy.context.scene.world.light_settings.distance = 0.12


def outputs(folder):
    """Compositor nodes writing the unit, the tint mask and the shadow as PNGs
    into `folder` (file names from the current frame number = direction row)."""
    scene = bpy.context.scene
    scene.use_nodes = True
    tree = scene.node_tree
    tree.nodes.clear()
    rl = tree.nodes.new('CompositorNodeRLayers')
    comp = tree.nodes.new('CompositorNodeComposite')
    tree.links.new(rl.outputs['Image'], comp.inputs['Image'])
    out = tree.nodes.new('CompositorNodeOutputFile')
    out.base_path = folder
    out.format.file_format = 'PNG'
    out.format.color_depth = '16'
    out.format.color_mode = 'RGBA'
    out.file_slots.clear()
    out.file_slots.new('unit_')
    out.file_slots.new('tint_')
    out.file_slots.new('shadow_')
    out.file_slots.new('ao_')
    for slot in ('tint_', 'shadow_', 'ao_'):
        fs = out.file_slots[slot]
        fs.use_node_format = False
        fs.format.file_format = 'PNG'
        fs.format.color_depth = '16'
        fs.format.color_mode = 'BW'
    tree.links.new(rl.outputs['Image'], out.inputs['unit_'])
    tree.links.new(rl.outputs['tint'], out.inputs['tint_'])
    tree.links.new(rl.outputs['Shadow Catcher'], out.inputs['shadow_'])
    if 'AO' in rl.outputs:
        tree.links.new(rl.outputs['AO'], out.inputs['ao_'])
    # PNG output from the compositor is display-referred: keep the masks linear
    scene.view_settings.view_transform = 'Standard'


def render_directions(root, rows=range(8)):
    """Renders the unit (everything parented to `root`) facing each row's
    direction; the outputs are named <pass>_<row:04d>.png."""
    scene = bpy.context.scene
    base = root.rotation_euler.z
    for row in rows:
        root.rotation_euler.z = base + math.radians(facing_deg(row))
        scene.frame_current = row
        scene.frame_set(row)
        bpy.ops.render.render(write_still=False)
    root.rotation_euler.z = base


def material(name, color, roughness=0.6, metallic=0.0, tint=False, nodes=None):
    """A principled material; `color` is sRGB, as a '#rrggbb' string or a
    tuple of 0..1 values. `tint` marks it for the civ color (it's then
    rendered light gray, like the tinted parts of Civ3's units). `nodes`, if
    given, is called with (tree, bsdf) to add texture."""
    if isinstance(color, str):
        color = hex_color(color)
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    tree = mat.node_tree
    bsdf = tree.nodes['Principled BSDF']
    if tint:
        # a gray `color` darkens the civ color's shade (white: the full civ color)
        g = color[0] if (len(set(round(v, 4) for v in color[:3])) == 1) else 1.0
        color = (0.82 * g,) * 3
    bsdf.inputs['Base Color'].default_value = (*srgb_to_linear(color), 1.0)
    bsdf.inputs['Roughness'].default_value = roughness
    bsdf.inputs['Metallic'].default_value = metallic
    if metallic < 0.5:
        # dielectrics: little specular, or the sky's reflection (the camera
        # looks down on many up-facing surfaces) lifts dark colors to grey
        bsdf.inputs['Specular IOR Level'].default_value = 0.18
    if nodes:
        nodes(tree, bsdf)
    aov = tree.nodes.new('ShaderNodeOutputAOV')
    aov.aov_name = 'tint'
    aov.inputs['Value'].default_value = 1.0 if tint else 0.0
    if 'tint_mask' in tree.nodes:
        # a pattern whose stripes/panel take the civ color: the mask drives the AOV
        tree.links.new(tree.nodes['tint_mask'].outputs[0], aov.inputs['Value'])
    return mat


def srgb_to_linear(c):
    def one(v):
        return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4
    return tuple(one(v) for v in c)


def hex_color(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))
