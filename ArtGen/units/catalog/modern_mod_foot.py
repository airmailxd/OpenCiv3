"""Modern's foot units (C7/Lua/modern, the Civ Players League's Modern 2.4):
art of their own under their own art names (archetypes in
models/modern_mod_foot.py), each calibrated against the picture the mod
ships for it (civ3art.MOD_UNIT_ORIGINALS: the Super Trooper against the
mod's Samurai folder, and so on). `shots` are the frames where the mod's
picture fires.
"""

_FIRE = {'ATTACK1': 'fire'}

UNITS = {
    # Power armour: a big armoured trooper, a laser rifle at the hip (the
    # mod's 'XAirborne', blue armour over a dark suit).
    'Super Trooper': {
        'archetype': 'mod_soldier',
        'build': {'build': 'muscular', 'bulk': 1.12, 'height': 1.05},
        'colors': {'skin': '#c8a07a', 'top': '#2c2e33', 'trousers': '#2c2e33', 'boots': '#26272b',
                   'belt': '#26272b', 'strap': '#26272b'},
        'tint': ['plate', 'helmet'],
        'tint_shade': '#d0d0d0',
        'hat': 'power', 'armor': 'power', 'top': 'long_sleeved', 'legs': 'trousers', 'feet': 'boots',
        'gun': 'laser_rifle', 'hold': 'low', 'aim': 'hip', 'fire_start': 'aim', 'fire_end': 'aim',
        'flash': 'laser', 'beam_len': 1.5,
        'shots': {'ATTACK1': [1, 2, 3, 4, 7, 8, 9, 10, 14, 15, 16, 17]},
        'fortify': 'aim', 'victory': 'raise',
        'actions': dict(_FIRE),
    },
    # A flamethrower: fuel tanks on the back, the flame gun at the hip, a
    # jet of fire (the mod's 'americanFT').
    'Combat Engineer': {
        'archetype': 'mod_soldier',
        'build': 'muscular',
        'colors': {'skin': '#c89c74', 'hair': '#2a2018', 'helmet': '#59604a', 'vest': '#606650', 'trousers': '#6e6a50',
                   'boots': '#3a2c1e', 'belt': '#3a3a2e', 'strap': '#3a3a2e'},
        'tint': ['top'],
        'desat': 0.85,
        'hair': 'short', 'hat': 'pasgt', 'top': 'long_sleeved', 'vest': 'flak', 'legs': 'trousers', 'cargo': True,
        'feet': 'boots', 'back_gear': 'tanks',
        'gun': 'flamethrower', 'hold': 'hip', 'aim': 'hip', 'fire_start': 'aim', 'fire_end': 'aim',
        'flash': 'flame', 'flame_len': 1.6,
        'shots': {'ATTACK1': list(range(1, 13))},
        'fortify': 'aim', 'victory': 'raise',
        'actions': dict(_FIRE),
    },
    # An RPG on the shoulder, two spare rounds on the back, brown kit (the
    # mod's 'RPG'); fires kneeling.
    'RPG Trooper': {
        'archetype': 'mod_soldier',
        'colors': {'skin': '#c4946c', 'hair': '#2a2018', 'helmet': '#6a5a40', 'top': '#6e5a40', 'trousers': '#5e5038',
                   'boots': '#2e2418', 'belt': '#2e2a22', 'strap': '#2e2a22', 'hat': '#6a5a40'},
        'camo': {'trousers': ['#4a3e2a', '#7e6e4e']},
        'tint': ['vest'],
        'desat': 0.9,
        'hair': 'short', 'hat': 'boonie', 'top': 'long_sleeved', 'vest': 'jerkin', 'vest_skirt': 0.0,
        'legs': 'trousers', 'cargo': True, 'feet': 'boots', 'back_gear': 'rockets',
        'gun': 'rpg', 'hold': 'shoulder', 'aim': 'shoulder', 'kneel_fire': True, 'fire_start': 'carry', 'fire_end': 'aim',
        'poses': {'carry': {'hand_r': (0.16, 0.20, 1.10), 'w_main': (40, 18, 0)}},
        'flash': 'rocket', 'impact': 3.4, 'flight': 4,
        'shots': {'ATTACK1': [3]},
        'fortify': 'kneel', 'victory': 'fist',
        'actions': dict(_FIRE),
    },
    # An urban insurgent of the 2000s (models/irregulars.py): a red-and-white
    # shemagh wound over the head, the face open, a dark jacket with the
    # sleeves rolled over jeans, a chicom chest rig and armband in the civ's
    # color, a PKM machine gun at the hip, trainers; fires long bursts from
    # the hip.
    'Insurgent': {
        'archetype': 'irregular',
        'colors': {'skin': '#a87e5c', 'vest': '#46502e', 'rig': '#7e7454', 'rig_dark': '#5a5240', 'trousers': '#424a58', 'boots': '#2e3038', 'belt': '#2a241c',
                   'strap': '#2a241c', 'seam': '#2e3422', 'seam_light': '#6e7480', 'kneepad': '#363a42',
                   'sole': '#d8d6d0', 'stripe': '#c4c2bc', 'hat': '#3a3634', 'hat_stripe': '#b8b0a0',
                   'toggle': '#2a2018', 'eyes': '#1a120c'},
        'camo': {'vest': ['#3a4424', '#56603a'], 'trousers': ['#384050', '#525a68']},
        'camo_density': 1.0, 'camo_size': 0.04,
        'tint': ['top'],
        'hair': 'none', 'hat': 'shemagh', 'top': 'long_sleeved', 'elbow_cuff': False, 'vest': 'jerkin', 'vest_skirt': 0.02,
        'jacket': {'zip': True}, 'seams': True, 'jeans': True, 'buckle': True,
        'rig': 'chicom', 'legs': 'trousers', 'knees': True, 'feet': 'shoes', 'sneakers': True,
        'gun': 'pkm', 'hold': 'hip', 'aim': 'hip', 'fire_start': 'aim', 'fire_end': 'aim',
        'run_hold': {'hand_r': (0.20, 0.16, 1.00), 'w_main': (-14, -38, 0), 'support': 1.0, 'elbow_l': (-0.5, -0.5, -0.7)},
        'flash': 'burst', 'smoke': 'light', 'flash_size': 0.30,
        'shots': {'ATTACK1': [1, 2, 3, 5, 6, 7, 9, 10, 11], 'ATTACK2': [2, 3, 4, 8, 9, 10], 'ATTACK3': [1, 3, 5, 7]},
        'fortify': 'kneel', 'victory': 'fire_air',
        'actions': {'ATTACK1': 'fire', 'ATTACK2': 'fire', 'ATTACK3': 'fire'},
    },
    # A townsman in an uprising, Budapest 1956 (models/irregulars.py): a flat
    # cap and neckerchief in the civ's color, a dark jacket open over his
    # shirt, the tricolour with its hole cut out on his arm, a belt of
    # shells, an old double-barrelled gun in his right hand and a lit Molotov
    # cocktail low in his left, a bag of bottles at his hip; he throws the
    # bottle overarm and it bursts into flame where it lands.
    'Rebel': {
        'archetype': 'irregular',
        'colors': {'skin': '#c89a74', 'hair': '#8a8680', 'hat': '#3e3a34', 'top': '#5a4c3e', 'trousers': '#6e6454', 'shirtfront': '#d4cebc',
                   'boots': '#2a201a', 'belt': '#2e2218', 'vest': '#4a3e32', 'waistcoat_edge': '#2e241c',
                   'bag': '#8e7e58', 'bag_dark': '#6e6244', 'bag_strap': '#5a4e36', 'strap': '#4a3624',
                   'bottles': '#a83a2a', 'patch': '#6e604c', 'stock': '#9a6838'},
        'camo': {'top': ['#665848', '#4a3e32'], 'trousers': ['#7a7060', '#5a5044']}, 'camo_density': 1.2,
        'camo_size': 0.04,
        'tint': ['kerchief', 'vest'],
        'hair': 'short', 'beard': False, 'hat': 'cloth_cap',
        'top': 'long_sleeved', 'shirt_front': True, 'tricolour': ['#b8302a', '#e8e4dc', '#3a7a3a'],
        'kerchief': 'kerchief', 'shell_belt': True, 'pockets': True,
        'waistcoat': 'vest', 'legs': 'trousers', 'turnups': True, 'knee_patch': True, 'feet': 'shoes', 'bag': 'bottles',
        'gun': 'shotgun', 'offhand': 'molotov', 'hold': 'low',
        'poses': {'carry': {'hand_r': (0.30, 0.12, 0.98), 'w_main': (-120, 0, -6), 'support': 0.0,
                            'hand_l': (-0.36, 0.14, 0.92), 'w_off': (0, 8, 0), 'elbow_l': (-0.6, -0.8, -0.3)},
                  'guard': {'support': 0.0, 'hand_l': (-0.36, 0.20, 1.00)}},
        'run_hold': {'hand_l': (-0.34, 0.16, 0.98)},
        'fortify': 'guard', 'victory': 'fist',
        'actions': {'ATTACK1': 'fire', 'ATTACK2': 'fire'},
    },
    # A fighter in a civ-colored headwrap, a khaki shirt and camouflage
    # trousers, an AK with a bayonet held upright (the mod's 'Mujahidin').
    'Militant': {
        'archetype': 'mod_soldier',
        'colors': {'skin': '#b88a64', 'hair': '#1e1610', 'top': '#a29470', 'coat': '#a29470', 'trousers': '#56603a',
                   'boots': '#3a2a1a', 'belt': '#3a2a1a', 'strap': '#3a2a1a'},
        'camo': {'trousers': ['#78843e', '#2e3a1a', '#6a5a30']},
        'tint': ['hat'],
        'hair': 'short', 'beard': True, 'hat': 'turban', 'top': 'long_sleeved', 'coat': 0.18,
        'legs': 'trousers', 'feet': 'boots', 'straps': ['crossbelt'],
        'gun': 'ak_bayonet', 'hold': 'vertical', 'aim': 'hip', 'fire_start': 'aim', 'fire_end': 'carry',
        'flash': 'burst', 'smoke': 'light', 'flash_size': 0.24, 'shots': {'ATTACK1': [0, 2, 7, 9, 11, 13]},
        'fortify': 'guard', 'victory': 'fire_air',
        'actions': dict(_FIRE),
    },
    # Napoleonic line infantry: a shako, a coat in the civ's color, white
    # crossbelts and trousers, a musket with its bayonet fixed (the mod's
    # 'NapoleonicInfantry').
    'Militia': {
        'archetype': 'mod_soldier',
        'colors': {'skin': '#d0a07a', 'hair': '#3a2a1c', 'trousers': '#e2ded4', 'boots': '#1c1a18',
                   'belt': '#ebe8e0', 'strap': '#ebe8e0', 'gaiters': '#1e1c1a', 'vest': '#e8e4dc'},
        'tint': ['top', 'coat'],
        'hair': 'short', 'hat': 'shako', 'top': 'long_sleeved', 'coat': 0.30, 'straps': ['crossbelt', 'crossbelt_l'],
        'legs': 'trousers', 'feet': 'shoes', 'gaiters': True,
        'gun': 'musket_bayonet', 'hold': 'vertical', 'aim': 'shoulder', 'fire_start': 'carry', 'fire_end': 'carry',
        'poses': {'carry': {'hand_r': (0.30, 0.10, 0.90), 'w_main': (-4, 4, 0)}},
        'flash': 'musket', 'smoke': 'heavy', 'flash_size': 0.30, 'shots': {'ATTACK1': [3, 4]},
        'fortify': 'guard', 'victory': 'hat',
        'actions': dict(_FIRE),
    },
    # A light WWII rifleman: a netted helmet, a short-sleeved shirt in the
    # civ's color, webbing and an M1 carbine at port arms (the mod's
    # 'americanRM').
    'Skirmisher': {
        'archetype': 'mod_soldier',
        'colors': {'skin': '#c89c74', 'hair': '#3a2a1c', 'helmet': '#5e6148', 'helmet_net': '#8a8a62',
                   'trousers': '#6a6a4c', 'boots': '#4a3420', 'belt': '#7a6a4a', 'strap': '#7a6a4a',
                   'pouch': '#6a6248'},
        'tint': ['top'],
        'desat': 0.85,
        'hair': 'short', 'hat': 'm1', 'net': True, 'top': 'sleeved', 'straps': ['webbing'],
        'legs': 'trousers', 'feet': 'boots',
        'gun': 'm1_carbine', 'hold': 'port', 'aim': 'shoulder', 'fire_start': 'aim', 'fire_end': 'carry',
        'flash': 'burst', 'smoke': 'light', 'shots': {'ATTACK1': [1, 2, 3]},
        'fortify': 'kneel', 'victory': 'raise',
        'actions': dict(_FIRE),
    },
    # Two men with a machine gun: the gunner carries it on his shoulder, the
    # bearer two ammunition cans; in action it's on its tripod (the mod's
    # 'americanHMG').
    'Battalion': {
        'archetype': 'hmg_team',
        'soldier': {
            'colors': {'skin': '#c89c74', 'hair': '#2a2018', 'helmet': '#54583e', 'vest': '#5c6046',
                       'trousers': '#6a6448', 'boots': '#3a2a1a', 'belt': '#3a3a2c', 'strap': '#3a3a2c'},
            'camo': {'trousers': ['#4a4a32', '#86805e']},
            'tint': ['top'],
            'hair': 'short', 'hat': 'm1', 'top': 'long_sleeved', 'vest': 'flak', 'legs': 'trousers', 'cargo': True,
            'feet': 'boots',
        },
        'desat': 0.85,
        'shots': {'ATTACK1': [1, 2, 5, 6, 9, 10, 13, 14]},
    },
}
