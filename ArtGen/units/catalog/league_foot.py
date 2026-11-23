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
    # A resistance fighter: a flat cap, a long dark civilian coat over a
    # sweater, a civ-colored armband and scarf, a bandolier, a drum-fed
    # submachine gun held across the body.
    'Partisans': {
        'archetype': 'league_soldier',
        'colors': {'skin': '#c89c78', 'hair': '#2a2018', 'hat': '#3e3a36', 'top': '#5e564a', 'coat': '#4a443c',
                   'trousers': '#4a4640', 'boots': '#26201c', 'belt': '#3a2c20', 'strap': '#4a3a28'},
        'tint': ['armband', 'scarf'],
        'build': 'lean',
        'desat': 0.85,
        'hair': 'short', 'hat': 'flat_cap', 'top': 'long_sleeved', 'coat': 0.34,
        'straps': ['bandolier'], 'armband': 'armband', 'scarf': 'scarf',
        'legs': 'trousers', 'feet': 'tall_boots',
        'gun': 'ppsh', 'hold': 'across', 'aim': 'hip', 'fire_start': 'carry', 'fire_end': 'carry',
        'flash': 'burst', 'smoke': 'light', 'flash_size': 0.22, 'shots': {'ATTACK1': [6, 7, 8, 9]},
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
    # A Japanese rifleman of 1941-44 (the mod's 'japaneseRM'; round 2 in
    # models/line_foot.py): the Type 98 field cap in the civ's color with its
    # star and four neck flaps, the khaki Type 98 tunic (stand-and-fall
    # collar with red tabs, four pockets), breeches and wound puttees, the
    # leather belt with two cartridge boxes in front and one behind, the
    # bayonet scabbard, canteen and bread bag on crossed straps, the
    # knapsack with the blanket rolled round it (civ color), and the Type 38
    # rifle with its long Type 30 bayonet held forward; attacks with the
    # bayonet.
    'Japanese Rifleman': {
        'archetype': 'line_soldier',
        'build': {'bulk': 0.96},
        'colors': {'skin': '#d0a47a', 'hair': '#1a1410', 'top': '#8a7c50', 'trousers': '#8a7c50',
                   'stockings': '#7a6e4a', 'boots': '#4a3424', 'belt': '#4a3424', 'strap': '#5a4a30',
                   'pack': '#7a6e4a', 'stock': '#7a4e2c', 'hatband': '#6a603e', 'puttees': '#66603e'},
        'tint': ['hat', 'havelock', 'blanket'],
        'desat': 0.85,
        'hair': 'short', 'hat': 'senboshi', 'top': 'long_sleeved', 'jacket': 'type98',
        'legs': 'breeches', 'garters': False, 'feet': 'shoes', 'leg_wear': 'puttees',
        'kit': ['ija_belt', 'ija_pack', 'ija_canteen'],
        'gun': 'type38', 'hold': 'port',
        'poses': {'carry': {'hand_r': (0.26, 0.12, 0.96), 'w_main': (-44, 0, 14), 'support': 1.0,
                            'elbow_l': (-0.5, -0.4, -0.8)}},
        'aim': 'shoulder', 'fire_start': 'aim', 'fire_end': 'carry',
        'flash': 'burst', 'smoke': 'light',
        'fortify': 'guard', 'victory': 'raise',
        'actions': {'ATTACK1': 'lunge'},
    },
}
