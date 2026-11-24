"""Modern's (C7/Lua/modern) own ships and plane, each under an art name of
its own (C7Engine/GameSetup.Modern.cs), calibrated against the mod's
pictures for the stock folder it drew the unit with (civ3art.MOD_UNIT_ORIGINALS:
the Pirate's Man-O-War, the Troop Ship's Galleon, the Fighter/Bomber's F-15).
Models in models/modern_mod_sea_air.py.
"""

UNITS = {
    # A scouting raider: grey steam hull, junk sails in civ-colored panels.
    'Pirate': {
        'archetype': 'ship', 'design': 'steam_junk', 'modern': True, 'billow_k': 1.0,
        'colors': {'steel': '#6c7076', 'steel_dark': '#43474e', 'steel_light': '#a4a8ae', 'deck_wood': '#6e5a44',
                   'sail': '#e0d8c4', 'wood_dark': '#4a3a2a'},
    },
    # A steam troop transport: grey hull, rust deck, civ-colored hatches.
    'Troop Ship': {
        'archetype': 'ship', 'design': 'troopship', 'modern': True,
        'colors': {'steel': '#6e7276', 'steel_dark': '#44484c', 'steel_light': '#a8acb0', 'deck': '#8a5e40',
                   'bottom': '#7a3a2c'},
    },
    # A twin-boom prop fighter-bomber (a P-38) in olive drab.
    'Fighter Bomber': {'archetype': 'aircraft', 'model': 'p38', 'tint': ['band'], 'weapon': 'guns'},
}
