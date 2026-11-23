"""Modern's foot units (C7/Lua/modern, the Civ Players League's Modern 2.4):
art of their own under their own art names (archetypes in
models/modern_mod_foot.py), each calibrated against the picture the mod
ships for it (civ3art.MOD_UNIT_ORIGINALS: the Super Trooper against the
mod's Samurai folder, and so on). `shots` are the frames where the mod's
picture fires.
"""

_FIRE = {'ATTACK1': 'fire'}

UNITS = {
    # Near-future power armour (models/regulars_foot.py): warm gunmetal
    # plates over a dark suit, the chest, back and upper-arm plates in the
    # civ's color, broad pauldrons of stacked lames, big knee and shin
    # guards, a closed helmet with a visor plate, cheek pieces and seams, a
    # power pack, a heavy pulse rifle with coils and an emitter held low
    # and out to the right (the mod's 'XAirborne').
    'Super Trooper': {
        'archetype': 'regular_soldier',
        'build': {'build': 'muscular', 'bulk': 1.10},
        'scale': 0.79,
        'gun_thick': 1.9,
        'colors': {'skin': '#c8a07a', 'top': '#2e2d29', 'trousers': '#2e2d29', 'boots': '#26251f',
                   'belt': '#26251f', 'strap': '#26251f'},
        'tint': ['plate'],
        'hair': 'none', 'hat': 'exo', 'armor': 'exo', 'top': 'long_sleeved', 'legs': 'trousers', 'feet': 'boots',
        'gun': 'pulse_rifle', 'hold': 'low', 'aim': 'hip', 'fire_start': 'aim', 'fire_end': 'aim', 'fire_blend': 3,
        'poses': {'carry': {'hand_r': (0.30, 0.18, 1.02), 'w_main': (-108, 0, -40), 'support': 1.0,
                            'elbow_r': (0.9, -0.4, -0.3), 'elbow_l': (-0.4, -0.3, -0.9)},
                  'aim': {'hand_r': (0.26, 0.26, 1.04), 'w_main': (-94, 0, -20)}},
        'flash': 'laser', 'beam_len': 1.5,
        'shots': {'ATTACK1': [3, 4, 7, 8, 9, 10, 14, 15, 16, 17]},
        'fortify': 'kneel', 'victory': 'raise',
        'actions': dict(_FIRE),
    },
    # An assault engineer (models/regulars_foot.py): the flamethrower's
    # tanks on a back frame with a shovel and a pick-mattock strapped on,
    # a respirator under goggles, a flak vest with pouches, a demolition
    # satchel at the hip, wire cutters, the flame gun at the hip and a jet
    # of fire (the mod's 'americanFT').
    'Combat Engineer': {
        'archetype': 'regular_soldier',
        'colors': {'skin': '#c89c74', 'hair': '#2a2018', 'helmet': '#8a8466', 'vest': '#6e7458',
                   'trousers': '#8e8868', 'boots': '#4a3826', 'belt': '#4a4836', 'strap': '#4a4836',
                   'tank': '#7a7e68', 'tank_dark': '#55584a', 'pouch': '#7e7a5c'},
        'camo': {'vest': ['#58603e', '#8e8e6c', '#4a4a36'], 'trousers': ['#74704e', '#a69e7c']},
        'tint': ['top', 'pack_band'],
        'desat': 0.85,
        'hair': 'short', 'hat': 'm1', 'goggles': True, 'respirator': True, 'top': 'long_sleeved',
        'vest': 'flak', 'legs': 'trousers', 'cargo': True, 'feet': 'boots',
        'kit': ['tank_pack', 'flak_pouches', 'satchel', 'etool', 'kneepads', 'gloves', 'belt_kit'],
        'gun': 'flame_gun', 'hold': 'hip', 'aim': 'hip', 'fire_start': 'aim', 'fire_end': 'aim', 'fire_blend': 3,
        'flash': 'flame', 'flame_len': 1.6,
        'shots': {'ATTACK1': list(range(3, 13))},
        'fortify': 'aim', 'victory': 'raise',
        'actions': dict(_FIRE),
    },
    # An anti-tank gunner with an RPG-7 on his right shoulder, both hands
    # on its grips, three spare rounds in a carrier on his back, angled
    # back below the helmet; a covered PASGT helmet with goggles, a chest
    # rig of booster pouches and belt kit over a civ-colored shirt with
    # rolled sleeves, camouflaged trousers (models/regulars_foot.py);
    # fires kneeling (the mod's 'RPG').
    'RPG Trooper': {
        'archetype': 'regular_soldier',
        'colors': {'skin': '#c4946c', 'hair': '#2a2018', 'helmet': '#6e6a4a', 'helmet_band': '#4e4c34',
                   'trousers': '#6a6448', 'boots': '#2a2018', 'belt': '#3a3426', 'strap': '#4a4430'},
        'camo': {'trousers': ['#4a4630', '#8a805c', '#5a4a32']},
        'tint': ['top'],
        'desat': 0.85,
        'hair': 'short', 'hat': 'covered', 'goggles': True, 'top': 'sleeved',
        'legs': 'trousers', 'cargo': True, 'feet': 'boots', 'kit': ['rpg_rig', 'rocket_carrier', 'belt_kit'],
        'gun': 'rpg7', 'hold': 'shoulder', 'aim': 'shoulder', 'kneel_fire': True, 'fire_start': 'carry', 'fire_end': 'aim',
        'poses': {'carry': {'hand_r': (0.20, 0.16, 1.42), 'w_main': (-52, 0, -16), 'support': 1.0,
                            'elbow_r': (0.8, -0.5, -0.4), 'elbow_l': (-0.6, -0.6, -0.5)},
                  'aim': {'hand_r': (0.16, 0.16, 1.80), 'w_main': (-90, 0, 0), 'head': (-4, 0, -6)}},
        'flash': 'rocket', 'impact': 3.4, 'flight': 4,
        'shots': {'ATTACK1': [5]},
        'fortify': 'aim', 'victory': 'fist',
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
