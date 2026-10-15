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
mesh, arm = H.body(gender=1.0, age=0.5, muscle=1.0, weight=0.62, height=0.8, proportions=0.9,
                   race={'caucasian': 0.4, 'african': 0.3, 'asian': 0.3}, skin='middleage_asian_male', hair='long01',
                   skin_tone=col('#b07a4c'), hair_tone=col('#2e2016'))
H.grime(mesh, amount=0.45)
loin = G.cloth('loin', (1, 1, 1), tint=True, weave=0.6)
G.skirt(mesh, arm, 'Loincloth', loin, length=0.5, flare=1.15, panels=(0.8, 0.8))
G.shell(mesh, arm, 'Belt', ['pelvis', 'spine_01'], G.leather('belt', col('#3a2618')), zmin=0.98, zmax=1.05, offset=0.014, thickness=0.012)
for side in 'lr':
    G.shell(mesh, arm, 'Bracer_' + side, ['lowerarm_' + side], G.cloth('bracer_' + side, (1, 1, 1), tint=True, weave=0.6), offset=0.009, thickness=0.008)
    G.shell(mesh, arm, 'Wrap_' + side, ['calf_' + side], G.fur('wrap_' + side, col('#5a3e28'), col('#2a1a10')), zmin=0.06, offset=0.014, thickness=0.014)
G.pelt(mesh, arm, col('#5a4a3a'), col('#2e241a'))
G.beard(mesh, col('#241810'), length=0.016, density=700)
G.roughen_hair(mesh, col('#241810'))
# a predatory crouch: hunched, head forward, knees bent wide, arms flexed out, club ready
H.pose(arm, [('spine_01', (0, -0.12, 1)), ('spine_02', (0, -0.32, 1)), ('spine_03', (0, -0.22, 1)),
             ('neck_01', (0, -0.12, 1)), ('head', (0, 0.08, 1)),
             ('thigh_l', (0.32, -0.3, -1)), ('calf_l', (0.12, 0.22, -1)), ('foot_l', (0.15, -1, -0.35)),
             ('thigh_r', (-0.3, 0.12, -1)), ('calf_r', (-0.1, 0.32, -1)), ('foot_r', (-0.15, -1, -0.35)),
             ('upperarm_l', (0.62, -0.12, -1)), ('lowerarm_l', (0.25, -0.75, -0.35)),
             ('upperarm_r', (-0.6, -0.2, -1)), ('lowerarm_r', (-0.3, -0.85, -0.45))])
club = P.make('stone_axe', {'wood': '#4a3424', 'stone': '#6e6a66', 'leather': '#3a2618'}, length=0.62, scale=1.35)
H.attach(club, arm, 'hand_r', Matrix.Translation((0.0, 0.05, 0.0)) @ Matrix.Rotation(math.radians(-90), 4, 'X') @ Matrix.Rotation(math.radians(-25), 4, 'Z'))
root = H.rooted(arm)
H.plant(root, mesh)
cal = json.load(open(os.path.join(os.environ['U3D'], 'calibration', 'Warrior.json')))
z = float(os.environ.get('ZOOM', 0))
if z: B.camera(480, 480, cal['fit']['ppm_2x'] * z, (240, float(os.environ.get('AY', 445))))
else: B.camera(480, 480, cal['fit']['ppm_2x'], tuple(c * 2 for c in cal['anchor']))
B.outputs(os.environ['OUT']); B.render_directions(root, rows=[int(r) for r in os.environ.get('ROWS', '0,1,2,3,4,5,6,7').split(',')])
