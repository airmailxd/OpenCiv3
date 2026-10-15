import sys, os, math, json, bpy
from mathutils import Matrix, Vector
sys.path.insert(0, os.environ['U3D'])
import bscene as B
scene = B.reset_scene()
import addon_utils; addon_utils.enable('bl_ext.user_default.mpfb', default_set=True)
import human as H, garments as G, props as P
print('device', B.use_gpu(scene)); scene.cycles.samples = int(os.environ.get('SAMPLES', 96))
B.lights(); B.ground(); B.tint_aov()
col = B.hex_color
mesh, arm = H.body(gender=1.0, age=0.5, muscle=1.0, weight=0.72, height=0.8, proportions=0.85,
                   race={'caucasian': 0.4, 'african': 0.3, 'asian': 0.3}, skin='middleage_asian_male', hair='long01',
                   skin_tone=col('#b07a4c'), hair_tone=col('#5a3e26'))
H.grime(mesh, amount=0.45)
loin = G.cloth('loin', (1, 1, 1), tint=True, weave=0.6)
G.shell(mesh, arm, 'Belt', ['pelvis', 'spine_01'], G.leather('belt', col('#3a2618')), zmin=0.98, zmax=1.05, offset=0.014, thickness=0.012)
for side in 'lr':
    G.shell(mesh, arm, 'Bracer_' + side, ['lowerarm_' + side], G.cloth('bracer_' + side, (1, 1, 1), tint=True, weave=0.6), offset=0.009, thickness=0.008)
    G.shell(mesh, arm, 'Wrap_' + side, ['calf_' + side], G.fur('wrap_' + side, col('#5a3e28'), col('#2a1a10')), zmin=0.06, offset=0.014, thickness=0.014)
G.pelt(mesh, arm, col('#7a6448'), col('#4a3a28'), length=0.03)
G.beard(mesh, col('#4a3220'), length=0.018, density=700)
G.roughen_hair(mesh, col('#5a3e26'))
# a predatory crouch: hunched, head forward, knees bent wide, arms flexed out, club ready
# planted wide stance, feet turned out, shoulders driven forward, elbows bent, fists ready
H.pose(arm, [('spine_01', (0, -0.08, 1)), ('spine_02', (0, -0.22, 1)), ('spine_03', (0, -0.15, 1)),
             ('clavicle_l', (1, -0.45, 0.05)), ('clavicle_r', (-1, -0.45, 0.05)),
             ('neck_01', (0, -0.12, 1)), ('head', (0, 0.1, 1)),
             ('thigh_l', (0.3, -0.05, -1)), ('calf_l', (0.24, 0.02, -1)), ('foot_l', (0.5, -1, -0.3)),
             ('thigh_r', (-0.3, -0.02, -1)), ('calf_r', (-0.24, 0.04, -1)), ('foot_r', (-0.5, -1, -0.3)),
             ('upperarm_l', (0.55, -0.25, -1)), ('lowerarm_l', (0.2, -1.0, -0.15)),
             ('upperarm_r', (-0.55, -0.15, -1)), ('lowerarm_r', (-0.35, -1.0, -0.35))])
# clenched fists: curl every finger toward the palm
for side in 'lr':
    for f in ('index', 'middle', 'ring', 'pinky'):
        for k in (1, 2, 3):
            pb = arm.pose.bones.get(f'{f}_0{k}_{side}')
            if pb: pb.rotation_mode = 'XYZ'; pb.rotation_euler.x = math.radians(75)
    for k in (1, 2):
        pb = arm.pose.bones.get(f'thumb_0{k}_{side}')
        if pb: pb.rotation_mode = 'XYZ'; pb.rotation_euler.x = math.radians(30)
bpy.context.view_layer.update()
G.loincloth(mesh, arm, loin)
club = P.make('stone_axe', {'wood': '#5a3e28', 'stone': '#9a8a74', 'leather': '#4a3020'}, length=0.42, scale=1.1)
H.attach(club, arm, 'hand_r', Matrix.Translation((0.0, 0.05, 0.0)) @ Matrix.Rotation(math.radians(-90), 4, 'X') @ Matrix.Rotation(math.radians(-25), 4, 'Z'))
root = H.rooted(arm)
H.plant(root, mesh)
cal = json.load(open(os.path.join(os.environ['U3D'], 'calibration', 'Warrior.json')))
z = float(os.environ.get('ZOOM', 0))
if z: B.camera(480, 480, cal['fit']['ppm_2x'] * z, (240, float(os.environ.get('AY', 445))))
else: B.camera(480, 480, cal['fit']['ppm_2x'], tuple(c * 2 for c in cal['anchor']))
B.outputs(os.environ['OUT']); B.render_directions(root, rows=[int(r) for r in os.environ.get('ROWS', '0,1,2,3,4,5,6,7').split(',')])
