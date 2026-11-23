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
                   'trousers': '#46493e', 'vest': '#3e4234', 'carrier': '#4a4e3c', 'carrier_dark': '#34372a',
                   'boots': '#2a2824', 'belt': '#262622', 'strap': '#262622', 'kneepad': '#222220'},
        'camo': {'trousers': ['#33362c', '#5c5f4c', '#2a2a26'], 'vest': ['#33362c', '#565a46']},
        'tint': ['top'],
        'desat': 0.9,
        'hair': 'short', 'beard': True, 'hat': 'fast', 'top': 'long_sleeved', 'vest': 'flak',
        'legs': 'trousers', 'cargo': True, 'feet': 'boots',
        'kit': ['plate_carrier', 'assault_pack', 'kneepads', 'gloves', 'holster', 'flag'],
        'poses': {'carry': {'hand_r': (0.26, 0.26, 1.10), 'w_main': (-100, 0, 34), 'support': 1.0,
                            'elbow_r': (0.9, -0.3, -0.4), 'elbow_l': (-0.5, -0.3, -0.8)}},
        'gun': 'sf_carbine', 'hold': 'across', 'aim': 'shoulder', 'fire_start': 'aim', 'fire_end': 'aim',
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
