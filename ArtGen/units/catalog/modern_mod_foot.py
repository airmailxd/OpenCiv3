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
    # An urban insurgent of the 2000s (models/irregulars.py): a shemagh
    # wound over the head, the face open, a jacket with the sleeves rolled
    # over jeans, a chicom chest rig in the civ's color, a PKM machine gun at
    # the hip, trainers; fires long bursts from the hip.
    'Insurgent': {
        'archetype': 'irregular',
        'colors': {'skin': '#b88e6c', 'top': '#5c6844', 'trousers': '#5a6270', 'boots': '#3a3c44', 'belt': '#2e2820',
                   'strap': '#2e2820', 'seam': '#3e4630', 'seam_light': '#8c9098', 'kneepad': '#464a52',
                   'rig_dark': '#5a5444', 'sole': '#e2e0da', 'stripe': '#cfcdc6', 'hat': '#c8bca0',
                   'hat_stripe': '#7a3028', 'toggle': '#2a2018'},
        'camo': {'top': ['#4e5a38', '#6c7852'], 'trousers': ['#4e5664', '#6a7280']},
        'camo_density': 1.0, 'camo_size': 0.04,
        'tint': ['rig', 'rig_dark'],
        'hair': 'none', 'hat': 'shemagh', 'top': 'sleeved', 'rolled': True,
        'jacket': {'collar': True, 'zip': True}, 'seams': True, 'jeans': True, 'buckle': True,
        'rig': 'chicom', 'legs': 'trousers', 'knees': True, 'feet': 'shoes', 'sneakers': True,
        'gun': 'pkm', 'hold': 'hip', 'aim': 'hip', 'fire_start': 'aim', 'fire_end': 'aim',
        'run_hold': {'hand_r': (0.20, 0.16, 1.00), 'w_main': (-14, -38, 0), 'support': 1.0, 'elbow_l': (-0.5, -0.5, -0.7)},
        'flash': 'burst', 'smoke': 'light', 'flash_size': 0.30,
        'shots': {'ATTACK1': [1, 2, 3, 5, 6, 7, 9, 10, 11], 'ATTACK2': [2, 3, 4, 8, 9, 10], 'ATTACK3': [1, 3, 5, 7]},
        'fortify': 'kneel', 'victory': 'fire_air',
        'actions': {'ATTACK1': 'fire', 'ATTACK2': 'fire', 'ATTACK3': 'fire'},
    },
    # A townsman in an uprising (models/irregulars.py): grey-bearded, a flat
    # cap, a collarless shirt under a waistcoat, an armband and neckerchief in
    # the civ's color, a bandolier of shells, an old double-barrelled gun on
    # his shoulder and a lit Molotov cocktail low in his left hand, a bag of
    # bottles at his hip; he throws the bottle overarm and it bursts into
    # flame where it lands.
    'Rebel': {
        'archetype': 'irregular',
        'colors': {'skin': '#c89a74', 'hair': '#a8a49c', 'hat': '#6a645a', 'top': '#c8c0aa', 'trousers': '#6a5e4e',
                   'boots': '#2e241c', 'belt': '#2e2218', 'vest': '#5a4a3a', 'waistcoat_edge': '#3a2e22',
                   'bag': '#8e7e58', 'bag_dark': '#6e6244', 'bag_strap': '#5a4e36', 'strap': '#4a3624',
                   'bottles': '#a83a2a', 'patch': '#857358'},
        'camo': {'trousers': ['#776a58', '#5a4e40']}, 'camo_density': 1.2, 'camo_size': 0.04,
        'tint': ['armband', 'kerchief'],
        'hair': 'short', 'beard': False, 'beard_style': 'trimmed', 'hat': 'flat_cap',
        'top': 'sleeved', 'rolled': True, 'shirt': {'buttons': 3, 'pocket': True},
        'armband2': 'armband', 'kerchief': 'kerchief', 'shell_belt': True,
        'waistcoat': 'vest', 'legs': 'trousers', 'turnups': True, 'knee_patch': True, 'feet': 'shoes', 'bag': 'bottles',
        'gun': 'shotgun', 'offhand': 'molotov', 'hold': 'shoulder',
        'poses': {'carry': {'hand_r': (0.14, 0.20, 1.12), 'w_main': (40, 20, 0), 'support': 0.0,
                            'elbow_r': (0.6, -0.6, -0.5), 'hand_l': (-0.36, 0.14, 0.92), 'w_off': (0, 8, 0),
                            'elbow_l': (-0.6, -0.8, -0.3)}},
        'run_hold': {'hand_l': (-0.34, 0.16, 0.98)},
        'fortify': 'guard', 'victory': 'fist',
        'actions': {'ATTACK1': 'fire', 'ATTACK2': 'fire'},
    },
    # An Afghan mujahid of the 1980s (models/irregulars.py): a pakol, a
    # beard, a kameez over shalwar, a sash in the civ's color, a wool patu
    # over the shoulders, a chicom chest rig, sandals, and a Lee-Enfield held
    # at port arms; fires from the shoulder.
    'Militant': {
        'archetype': 'irregular',
        'scale': 0.95,
        'colors': {'skin': '#b88a64', 'hair': '#2e2218', 'top': '#aaa084', 'hem': '#8e8468', 'trousers': '#8c8878',
                   'cuff': '#726e60', 'boots': '#b88a64', 'sandal': '#4a3220', 'belt': '#3a2a1a',
                   'strap': '#3a2a1a', 'rig': '#6e6448', 'rig_dark': '#504836', 'hat': '#6e5e4a', 'hat_seam': '#54473a',
                   'patu': '#6a5e4c', 'patu_border': '#3a3026'},
        'tint': ['sash'],
        'hair': 'short', 'beard': False, 'beard_style': 'full', 'hat': 'pakol', 'top': 'long_sleeved',
        'kameez': 0.40, 'kameez_folds': True, 'sash': 'sash', 'shalwar': True, 'patu': 'patu', 'rig': 'chicom',
        'feet': 'shoes', 'sandals': True,
        'gun': 'enfield', 'hold': 'port', 'aim': 'shoulder', 'fire_start': 'carry', 'fire_end': 'carry',
        'flash': 'burst', 'smoke': 'light', 'flash_size': 0.26, 'shots': {'ATTACK1': [6, 12]},
        'fortify': 'guard', 'victory': 'raise',
        'actions': {'ATTACK1': 'fire'},
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
