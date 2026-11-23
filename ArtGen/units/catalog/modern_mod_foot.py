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
    # wound over the head and face in the civ's color, a dark jacket with
    # the sleeves rolled, a chicom chest rig, a PKM machine gun at the hip
    # with a spare belt over the shoulder, trainers; fires long bursts from
    # the hip.
    'Insurgent': {
        'archetype': 'irregular',
        'colors': {'skin': '#b88e6c', 'top': '#5c5642', 'trousers': '#3e4048', 'boots': '#2c2e36', 'belt': '#26221e',
                   'strap': '#26221e', 'seam': '#2a2820', 'kneepad': '#2e3038', 'rig': '#9a8e64', 'rig_dark': '#6a6048',
                   'sole': '#e2e0da', 'stripe': '#cfcdc6', 'hat_stripe': '#ece8de'},
        'camo': {'top': ['#4e4836', '#6a624a'], 'trousers': ['#4a4c56', '#2e3036']},
        'camo_density': 1.2, 'camo_size': 0.04,
        'tint': ['hat'],
        'hair': 'none', 'hat': 'shemagh', 'top': 'sleeved', 'rolled': True,
        'jacket': {'collar': True, 'zip': True, 'hem': True},
        'rig': 'chicom', 'mg_belt': True, 'legs': 'trousers', 'cargo': True, 'knees': True, 'feet': 'shoes',
        'sneakers': True,
        'gun': 'pkm', 'hold': 'hip', 'aim': 'hip', 'fire_start': 'aim', 'fire_end': 'aim',
        'flash': 'burst', 'smoke': 'light', 'flash_size': 0.30,
        'shots': {'ATTACK1': [1, 2, 3, 5, 6, 7, 9, 10, 11], 'ATTACK2': [2, 3, 4, 8, 9, 10], 'ATTACK3': [1, 3, 5, 7]},
        'fortify': 'kneel', 'victory': 'fire_air',
        'actions': {'ATTACK1': 'fire', 'ATTACK2': 'fire', 'ATTACK3': 'fire'},
    },
    # A townsman in an uprising (models/irregulars.py): grey-bearded, an old
    # army helmet, a shirt in the civ's color under a dark waistcoat, a lit
    # Molotov cocktail in his hand and a bag of bottles at his hip; he
    # throws it overarm and it bursts into flame where it lands.
    'Rebel': {
        'archetype': 'irregular',
        'colors': {'skin': '#c89a74', 'hair': '#a8a49c', 'helmet': '#5e6252', 'trousers': '#62584a', 'bag': '#8e7e58',
                   'bag_dark': '#6e6244', 'bag_strap': '#5a4e36',
                   'boots': '#2e241c', 'belt': '#2e2218', 'vest': '#5a4a3a', 'waistcoat_edge': '#3a2e22',
                   'chinstrap': '#3a3428'},
        'camo': {'trousers': ['#6e6252', '#524638']}, 'camo_density': 1.2, 'camo_size': 0.04,
        'tint': ['top'],
        'hair': 'short', 'beard': True, 'beard_style': 'trimmed', 'hat': 'm1', 'chinstraps': True,
        'top': 'sleeved', 'rolled': True, 'shirt': {'collar': True, 'buttons': 4, 'pocket': True},
        'waistcoat': 'vest', 'legs': 'trousers', 'turnups': True, 'knee_patch': True, 'feet': 'shoes', 'bag': 'bottles',
        'gun': 'molotov', 'hold': 'port',
        'poses': {'carry': {'hand_r': (0.32, 0.14, 1.40), 'w_main': (-6, -8, 0), 'support': 0.0,
                            'elbow_r': (0.7, -0.6, -0.4), 'hand_l': (-0.36, 0.02, 0.86)}},
        'fortify': 'kneel', 'victory': 'fist',
        'actions': {'ATTACK1': 'fire', 'ATTACK2': 'fire'},
    },
    # An Afghan mujahid of the 1980s (models/irregulars.py): a pakol, a full
    # beard, a knee-length kameez over shalwar, a patu shawl in the civ's
    # color over the shoulders, a chicom chest rig, sandals, and a
    # Lee-Enfield sloped over the right shoulder; fires from the shoulder.
    'Militant': {
        'archetype': 'irregular',
        'colors': {'skin': '#b88a64', 'hair': '#1a1410', 'top': '#aaa084', 'hem': '#8e8468', 'trousers': '#8c8878',
                   'cuff': '#726e60', 'boots': '#b88a64', 'sandal': '#4a3220', 'belt': '#3a2a1a',
                   'strap': '#3a2a1a', 'rig': '#6e6448', 'rig_dark': '#504836', 'hat': '#6e5e4a', 'hat_seam': '#54473a',
                   'patu_border': '#3a3026'},
        'camo': {'top': ['#9a9076', '#b6ac90']}, 'camo_density': 1.0, 'camo_size': 0.04,
        'tonal': {'kameez': ['#9a9076', '#b4aa8e', '#8e8468'], 'shalwar': ['#7e7a6a', '#9a9684']},
        'tint': ['patu'],
        'hair': 'short', 'beard': True, 'beard_style': 'full', 'hat': 'pakol', 'top': 'long_sleeved',
        'kameez': 0.50, 'shalwar': True, 'patu': 'patu', 'rig': 'chicom', 'feet': 'shoes', 'sandals': True,
        'gun': 'enfield', 'hold': 'shoulder', 'aim': 'shoulder', 'fire_start': 'carry', 'fire_end': 'carry',
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
