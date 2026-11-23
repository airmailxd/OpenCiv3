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
        'scale': 0.75,
        'gun_thick': 1.9,
        'colors': {'skin': '#c8a07a', 'top': '#2e2d29', 'trousers': '#2e2d29', 'boots': '#26251f',
                   'belt': '#26251f', 'strap': '#26251f'},
        'tint': ['plate'],
        'hair': 'none', 'hat': 'exo', 'armor': 'exo', 'top': 'long_sleeved', 'legs': 'trousers', 'feet': 'boots',
        'gun': 'pulse_rifle', 'hold': 'low', 'aim': 'shoulder', 'fire_start': 'aim', 'fire_end': 'aim', 'fire_blend': 3,
        'poses': {'carry': {'hand_r': (0.24, 0.30, 1.04), 'w_main': (-96, 0, -12), 'support': 1.0,
                            'elbow_r': (0.9, -0.4, -0.3), 'elbow_l': (-0.8, 0.1, -0.5)},
                  'aim': {'elbow_l': (-0.8, 0.2, -0.6)}},
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
        'gun': 'pkm', 'death_gun': 'pkm_stub', 'hold': 'hip', 'aim': 'hip', 'fire_start': 'aim', 'fire_end': 'aim',
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
    # A French line fusilier of 1806-12 (the mod's 'NapoleonicInfantry';
    # round 2 in models/line_foot.py): the 1806 shako with its brass plate,
    # chinscales, cords and pompom; the habit-veste in the civ's color with
    # white lapels, red collar, cuffs, epaulettes and turnbacks; white
    # crossbelts, the cartridge box and the briquet; the calfskin knapsack
    # with the greatcoat rolled on it; white breeches, black gaiters to the
    # knee; the Charleville musket and bayonet at order arms.
    'Militia': {
        'archetype': 'line_soldier',
        'scale': 0.87,
        'colors': {'skin': '#d0a07a', 'hair': '#2a1e14', 'trousers': '#d2ccbc', 'stockings': '#d2ccbc',
                   'boots': '#161412', 'strap': '#e4e0d6', 'stock': '#6a4426', 'lapels': '#e6e2d8',
                   'waistcoat': '#ebe7dd', 'facings': '#a02c26', 'turnbacks': '#a02c26', 'epaulette': '#a02c26'},
        'tint': ['top', 'belt', 'epaulette'],
        'fringe': False,
        'desat': 0.7,
        'hair': 'short', 'hat': 'shako1806', 'top': 'long_sleeved', 'coat_cut': 'habit',
        'straps': ['crossbelt', 'crossbelt_l'], 'legs': 'breeches', 'garters': False, 'feet': 'shoes',
        'leg_wear': 'gaiters_long', 'kit': ['calfskin', 'giberne', 'briquet'],
        'gun': 'charleville', 'hold': 'shoulder',
        'poses': {'carry': {'hand_r': (0.13, 0.20, 1.10), 'w_main': (30, 24, 0)},
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
        'colors': {'skin': '#c89c74', 'hair': '#3a2a1c', 'helmet': '#6e7a4c', 'trousers': '#6a7450',
                   'boots': '#161514', 'belt': '#5e5e42', 'strap': '#5e5e42', 'stock': '#9a6a3c', 'canvas': '#6e7848',
                   'og107': '#6e7650'},
        'tint': ['top', 'band', 'bandolier'],
        'desat': 0.85,
        'build': {'bulk': 0.95},
        'hair': 'short', 'hat': 'm1_cover', 'top': 'sleeved', 'jacket': 'jungle', 'rolled': True,
        'sleeve_mat': 'og107',
        'straps': ['braces'], 'legs': 'trousers', 'cargo': True, 'feet': 'boots', 'leg_wear': 'jungle_boots',
        'kit': ['m1956', 'towel', 'bandoliers', 'etool', 'combat_pack'],
        'scale': 0.95,
        'stance': {'spine': (-8, 0, 0), 'chest': (-4, 0, 0), 'head': (16, 0, 0), 'root_pos': (0, 0, -0.05),
                   'foot_l': (-0.17, 0.12, 0.08), 'foot_r': (0.19, -0.10, 0.08)},
        'gun': 'm14', 'hold': 'low', 'aim': 'shoulder',
        'poses': {'carry': {'hand_r': (0.22, 0.18, 0.94), 'w_main': (-48, -52, 0), 'support': 1.0,
                            'elbow_l': (-0.5, -0.5, -0.7)}}, 'fire_start': 'carry', 'fire_end': 'carry',
        'flash': 'burst', 'smoke': 'light', 'shots': {'ATTACK1': [3, 4, 5]}, 'death_late_hands': True, 'death_rest': 'chest',
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
        'scale': 0.72,
        'soldier': {
            'colors': {'skin': '#c89c74', 'hair': '#2a2018', 'helmet': '#5a5a46', 'trousers': '#4e4234',
                       'top': '#6e6a4e',
                       'boots': '#4a3020', 'belt': '#7a7458', 'strap': '#7a7458', 'leggings': '#857a56',
                       'pack': '#7a7058'},
            'tint': ['strap', 'belt'],
            'hair': 'short', 'hat': 'm1_net', 'top': 'long_sleeved', 'jacket': 'm41', 'legs': 'trousers',
            'feet': 'shoes', 'leg_wear': 'leggings', 'straps': ['braces'],
        },
        'gunner': {'kit': ['holster', 'canteen_ww2', 'cartridge_belt'], 'build': {'bulk': 1.06}},
        'assistant': {'kit': ['cartridge_belt', 'canteen_ww2'], 'pack': 'field', 'hat': 'm1',
                      'build': {'bulk': 0.93, 'height': 0.97}},
        'desat': 0.85,
        'shots': {'ATTACK1': [1, 2, 5, 6, 9, 10, 13, 14]},
    },
}
