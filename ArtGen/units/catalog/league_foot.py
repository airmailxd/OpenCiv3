"""The League modes' foot units that standalone mode drew with a stock
unit's look, each under an art name of its own (models/league_foot.py).
Each is calibrated against the original named in civ3art.MOD_UNIT_ORIGINALS:
the Partisans against the stock Rifleman (a lone rifleman; Civ3 has no
partisan, and the TOW team Modern borrowed is two men and a tripod), the
Special Forces against the Guerilla they replace, and Modern's Japanese
rifleman against the mod's own Infantry folder. `shots` are frames where
the original fires.
"""

UNITS = {
    # A Soviet partisan of 1942-44 (models/irregulars.py): an ushanka with
    # the ear flaps tied up, its cloth crown and a ribbon across it in the
    # civ's color, a scarf, armband and the straps of his sack and bag in the
    # civ's color too, a quilted telogreika with breast pockets, tall boots,
    # grenades, and a PPSh-41 with its drum at the hip; fires from the hip.
    'Partisans': {
        'archetype': 'irregular',
        'death_low': 0.14,
        'colors': {'skin': '#c89c78', 'hair': '#2a2018', 'top': '#7a7a72', 'coat': '#7a7a72', 'trousers': '#46443e',
                   'boots': '#24201c', 'belt': '#2e2218', 'strap': '#3a2c20', 'hat': '#6a5642', 'fur_dark': '#4a3c2e',
                   'button': '#141210', 'seam': '#4a4a44', 'stock': '#b07a42', 'quilt_seam': '#4e4e46', 'sack': '#8a7e60'},
        'camo': {'top': ['#6a6a62', '#8a8a80'], 'trousers': ['#3a3832', '#54524a']}, 'camo_density': 1.2,
        'camo_size': 0.04,
        'tint': ['ribbon', 'scarf', 'armband', 'sack_dark', 'bag_strap', 'sack', 'bag'],
        'hair': 'short', 'hat': 'ushanka', 'ribbon': 'ribbon', 'scarf': 'scarf', 'armband2': 'armband', 'armbands_both': True,
        'top': 'long_sleeved', 'coat': 0.20, 'pockets': True,
        'quilted': True, 'jacket': {'buttons': 5}, 'belt_over': True, 'bag': 'bread',
        'grenade': True, 'f1': True, 'sack': True,
        'legs': 'trousers', 'feet': 'tall_boots', 'boot_folds': True,
        'gun': 'ppsh41', 'hold': 'hip', 'aim': 'hip', 'fire_start': 'carry', 'fire_end': 'carry',
        'poses': {'carry': {'hand_r': (0.27, 0.20, 0.92), 'w_main': (-100, 0, 4)}},
        'run_hold': {'hand_r': (0.20, 0.16, 1.00), 'w_main': (-14, -38, 0), 'support': 1.0, 'elbow_l': (-0.5, -0.5, -0.7)},
        'flash': 'burst', 'smoke': 'light', 'flash_size': 0.24, 'shots': {'ATTACK1': [5, 6, 7, 8, 9]},
        'fortify': 'kneel', 'victory': 'fire_air',
        'actions': {'ATTACK1': 'fire'},
    },
    # A special forces operator: a civ-colored beret, tan
    # camouflage under a dark plate carrier, a suppressed carbine at the
    # low ready; fires from the shoulder.
    'Special Forces': {
        'archetype': 'league_soldier',
        'colors': {'skin': '#c09474', 'hair': '#2a2018', 'top': '#8a7c5c', 'trousers': '#8a7c5c',
                   'vest': '#4a4636', 'boots': '#5a4a36', 'belt': '#3a362c', 'strap': '#3a362c', 'pouch': '#4a4636'},
        'camo': {'top': ['#6e6448', '#a8976e', '#5a5a3e'], 'trousers': ['#6e6448', '#a8976e', '#5a5a3e']},
        'tint': ['hat'],
        'desat': 0.9,
        'hair': 'short', 'hat': 'beret', 'top': 'long_sleeved', 'vest': 'flak', 'straps': ['webbing'],
        'legs': 'trousers', 'cargo': True, 'feet': 'boots',
        'gun': 'm4_sd', 'hold': 'low', 'aim': 'shoulder', 'fire_start': 'aim', 'fire_end': 'aim',
        'flash': 'burst', 'smoke': None, 'flash_size': 0.12,
        'shots': {'ATTACK1': [0, 2, 7, 9, 12]},
        'fortify': 'kneel', 'victory': 'raise',
        'actions': {'ATTACK1': 'fire', 'ATTACK2': 'lunge'},
    },
    # A Japanese rifleman of the 1940s (the mod's 'japaneseRM'): a round
    # helmet in a civ-colored cover with a neck flap, a khaki tunic,
    # breeches and puttees, a field pack and a long rifle with a long
    # bayonet; attacks with the bayonet.
    'Japanese Rifleman': {
        'archetype': 'league_soldier',
        'build': 'lean',
        'colors': {'skin': '#d0a47a', 'hair': '#1a1410', 'top': '#8a7c54', 'trousers': '#8a7c54',
                   'stockings': '#7a7050', 'boots': '#4a3626', 'belt': '#4a3a26', 'strap': '#5a4a30',
                   'pack': '#6e6446', 'pouch': '#5a4a30', 'havelock': '#8a7c54'},
        'tint': ['helmet'],
        'desat': 0.85,
        'hair': 'short', 'hat': 'ija', 'top': 'long_sleeved', 'straps': ['webbing'], 'pack': 'field',
        'legs': 'breeches', 'feet': 'shoes',
        'gun': 'arisaka', 'hold': 'port', 'aim': 'shoulder', 'fire_start': 'aim', 'fire_end': 'carry',
        'flash': 'burst', 'smoke': 'light',
        'fortify': 'guard', 'victory': 'raise',
        'actions': {'ATTACK1': 'lunge'},
    },
}
