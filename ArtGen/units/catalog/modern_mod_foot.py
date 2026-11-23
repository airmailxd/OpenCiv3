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
    # A French line fusilier of 1806-12 (the mod's 'NapoleonicInfantry';
    # round 2 in models/line_foot.py): the 1806 shako with its brass plate,
    # chinscales, cords and pompom; the habit-veste in the civ's color with
    # white lapels, red collar, cuffs, epaulettes and turnbacks; white
    # crossbelts, the cartridge box and the briquet; the calfskin knapsack
    # with the greatcoat rolled on it; white breeches, black gaiters to the
    # knee; the Charleville musket and bayonet at order arms.
    'Militia': {
        'archetype': 'line_soldier',
        'scale': 0.84,
        'colors': {'skin': '#d0a07a', 'hair': '#2a1e14', 'trousers': '#e2ded2', 'stockings': '#e2ded2',
                   'boots': '#161412', 'strap': '#ebe8e0', 'stock': '#6a4426'},
        'tint': ['top', 'belt'],
        'hair': 'short', 'hat': 'shako1806', 'top': 'long_sleeved', 'coat_cut': 'habit',
        'straps': ['crossbelt', 'crossbelt_l'], 'legs': 'breeches', 'garters': False, 'feet': 'shoes',
        'leg_wear': 'gaiters_long', 'kit': ['calfskin', 'giberne', 'briquet'],
        'gun': 'charleville', 'hold': 'vertical', 'order_grip': 0.52,
        'poses': {'carry': {'hand_r': (0.33, 0.10, 0.90), 'w_main': (-12, 0, 0), 'elbow_r': (0.5, -0.6, -0.4)},
                  'march': {'hand_r': (0.20, 0.16, 0.96), 'w_main': (-14, -38, 0), 'support': 1.0,
                            'elbow_l': (-0.5, -0.5, -0.7)}},
        'aim': 'shoulder', 'fire_start': 'carry', 'fire_end': 'carry',
        'flash': 'musket', 'smoke': 'heavy', 'flash_size': 0.30, 'shots': {'ATTACK1': [3, 4]},
        'fortify': 'guard', 'victory': 'raise',
        'actions': dict(_FIRE),
    },
    # A US rifleman of the 1960s (Modern's Skirmisher comes with Cold War;
    # round 2 in models/line_foot.py): the M1 helmet in its leaf-pattern
    # cover with the band and a bottle tucked in it, the jungle jacket in the
    # civ's color with rolled sleeves, an olive towel round the neck, the
    # M1956 load-bearing kit (suspenders, ammunition pouches with grenades,
    # canteen, butt pack), jungle boots, and the M14 at the low ready.
    'Skirmisher': {
        'archetype': 'line_soldier',
        'colors': {'skin': '#c89c74', 'hair': '#3a2a1c', 'helmet': '#5e6a3e', 'trousers': '#55603e',
                   'boots': '#161514', 'belt': '#5e5e42', 'strap': '#5e5e42', 'stock': '#7a4e2c', 'canvas': '#6e7848'},
        'tint': ['top'],
        'desat': 0.85,
        'hair': 'short', 'hat': 'm1_cover', 'top': 'sleeved', 'jacket': 'jungle', 'rolled': True,
        'straps': ['braces'], 'legs': 'trousers', 'cargo': True, 'feet': 'boots', 'leg_wear': 'jungle_boots',
        'kit': ['m1956', 'towel'],
        'stance': {'spine': (-8, 0, 0), 'chest': (-4, 0, 0), 'head': (8, 0, 0), 'root_pos': (0, 0, -0.05),
                   'foot_l': (-0.17, 0.12, 0.08), 'foot_r': (0.19, -0.10, 0.08)},
        'gun': 'm14', 'hold': 'low', 'aim': 'shoulder',
        'poses': {'carry': {'hand_r': (0.24, 0.16, 1.02), 'w_main': (-118, 0, 30)}}, 'fire_start': 'aim', 'fire_end': 'carry',
        'flash': 'burst', 'smoke': 'light', 'shots': {'ATTACK1': [1, 2, 3]},
        'fortify': 'kneel', 'victory': 'raise',
        'actions': dict(_FIRE),
    },
    # A US machine-gun team of 1944 (the mod's 'americanHMG'; round 2 in
    # models/line_foot.py): the gunner with the water-cooled Browning
    # M1917A1 on his shoulder and a pistol on his hip, his assistant with the
    # folded tripod and an ammunition can; M1 helmets with nets, M1941 field
    # jackets in the civ's color, canvas leggings. In action the gun is on
    # its tripod, the hose to the condenser can, the assistant feeding the
    # belt from an open can.
    'Battalion': {
        'archetype': 'line_mg_team',
        'soldier': {
            'colors': {'skin': '#c89c74', 'hair': '#2a2018', 'helmet': '#5a5a46', 'trousers': '#6a5a44',
                       'boots': '#4a3020', 'belt': '#7a7458', 'strap': '#7a7458', 'leggings': '#a8987a'},
            'tint': ['top'],
            'hair': 'short', 'hat': 'm1_net', 'top': 'sleeved', 'jacket': 'm41', 'legs': 'trousers',
            'feet': 'shoes', 'leg_wear': 'leggings', 'straps': ['braces'],
        },
        'gunner': {'kit': ['holster', 'canteen_ww2']},
        'assistant': {'kit': ['cartridge_belt', 'canteen_ww2']},
        'desat': 0.85,
        'shots': {'ATTACK1': [1, 2, 5, 6, 9, 10, 13, 14]},
    },
}
