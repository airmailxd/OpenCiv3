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
    # A commando in dark urban camouflage, a balaclava, a chest rig in the
    # civ's color and a submachine gun; fires kneeling (the mod's 'FCommando').
    'Insurgent': {
        'archetype': 'mod_soldier',
        'colors': {'skin': '#b88e6c', 'top': '#4a4b4c', 'trousers': '#4a4b4c', 'boots': '#1e1e20',
                   'belt': '#1e1e20', 'strap': '#1e1e20', 'hat': '#222326'},
        'camo': {'top': ['#2a2b2c', '#76766e', '#5a5048'], 'trousers': ['#2a2b2c', '#76766e', '#5a5048']},
        'tint': ['vest'],
        'hair': 'none', 'hat': 'balaclava', 'top': 'long_sleeved', 'vest': 'flak', 'legs': 'trousers', 'cargo': True,
        'feet': 'boots', 'straps': ['webbing'],
        'gun': 'smg', 'hold': 'low', 'aim': 'hip', 'kneel_fire': True, 'fire_start': 'carry', 'fire_end': 'carry',
        'flash': 'burst', 'smoke': None, 'flash_size': 0.2, 'shots': {'ATTACK1': [5, 7, 9]},
        'fortify': 'kneel', 'victory': 'fire_air',
        'actions': dict(_FIRE),
    },
    # An irregular: a grey-bearded man in a white T-shirt, trousers in the
    # civ's color, an old steel helmet and a stout stick (the mod's 'OldTimer').
    'Rebel': {
        'archetype': 'mod_soldier',
        'build': 'lean',
        'colors': {'skin': '#c89a74', 'hair': '#b4b0a8', 'top': '#e6e4de', 'helmet': '#6a6c64',
                   'boots': '#3a3430', 'belt': '#3a2c20'},
        'tint': ['trousers'],
        'hair': 'short', 'beard': True, 'hat': 'm1', 'top': 'sleeved', 'legs': 'trousers', 'feet': 'shoes',
        'weapon': 'stick', 'stoop': 6,
        'actions': {'ATTACK1': 'attack_chop', 'ATTACK2': 'attack_chop'},
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
