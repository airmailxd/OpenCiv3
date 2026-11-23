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
    # A present-day special operator (models/regulars_foot.py): a black
    # high-cut helmet with the night-vision goggles flipped up on their
    # mount and a headset, a beard; ranger-green kit over a dark combat
    # uniform, civ-colored sleeves like the stock Marine's; a plate
    # carrier with magazines, a radio whose antenna rises behind the head,
    # an assault pack, knee pads, a drop-leg holster, gloves; a suppressed
    # carbine with an optic held out from the body; fires from the shoulder.
    'Special Forces': {
        'archetype': 'regular_soldier',
        'scale': 0.93,
        'colors': {'skin': '#c09474', 'hair': '#2e2218', 'helmet': '#2a2b2c', 'helmet_rail': '#1a1a1a',
                   'trousers': '#686c5c', 'vest': '#5e644e', 'carrier': '#6e7258', 'carrier_dark': '#52563f',
                   'boots': '#3a3630', 'belt': '#34342e', 'strap': '#34342e', 'kneepad': '#2e2e2a'},
        'camo': {'trousers': ['#4e5242', '#82866c', '#40403a'], 'vest': ['#4e5242', '#7a7e64']},
        'tint': ['top'],
        'desat': 0.9,
        'hair': 'short', 'beard': True, 'hat': 'fast', 'top': 'long_sleeved', 'vest': 'flak',
        'legs': 'trousers', 'cargo': True, 'feet': 'boots',
        'kit': ['plate_carrier', 'assault_pack', 'kneepads', 'gloves', 'holster', 'flag'],
        'poses': {'carry': {'hand_r': (0.24, 0.40, 1.08), 'w_main': (-98, 0, 28), 'support': 1.0,
                            'elbow_r': (0.9, -0.3, -0.4), 'elbow_l': (-0.9, 0.1, -0.5)},
                  'aim': {'elbow_l': (-0.8, 0.2, -0.6)}},
        'gun': 'sf_carbine', 'hold': 'across', 'aim': 'shoulder', 'fire_start': 'aim', 'fire_end': 'aim',
        'flash': 'burst', 'smoke': None, 'flash_size': 0.12, 'fire_blend': 3,
        'shots': {'ATTACK1': [3, 5, 7, 9, 12]},
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
        'build': {'bulk': 0.9},
        'stance': {'head': (10, 0, 0)},
        'death_late_hands': True, 'death_head0': 6.0, 'thrust_level': True,
        'colors': {'skin': '#d0a47a', 'hair': '#1a1410', 'top': '#7e7c4c', 'trousers': '#b4a67e', 'pouch': '#7a5430',
                   'stockings': '#7a6e4a', 'hat': '#8e8858', 'havelock': '#8e8858', 'boots': '#4a3424', 'belt': '#4a3424', 'strap': '#5a4a30',
                   'pack': '#7a6e4a', 'stock': '#7a4e2c', 'puttees': '#4e4a34'},
        'tint': ['hatband', 'blanket'],
        'desat': 0.85,
        'hair': 'none', 'hat': 'senboshi', 'top': 'long_sleeved', 'jacket': 'type98',
        'legs': 'breeches', 'garters': False, 'feet': 'shoes', 'leg_wear': 'puttees',
        'kit': ['ija_belt', 'ija_pack', 'ija_canteen'],
        'gun': 'type38', 'hold': 'port',
        'poses': {'carry': {'hand_r': (0.22, 0.18, 0.98), 'w_main': (-20, -36, 0)}},
        'aim': 'shoulder', 'fire_start': 'aim', 'fire_end': 'carry',
        'flash': 'burst', 'smoke': 'light',
        'fortify': 'guard', 'victory': 'raise',
        'actions': {'ATTACK1': 'lunge'},
    },
}
