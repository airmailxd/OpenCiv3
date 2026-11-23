"""Modern's (C7/Lua/modern) own ships and plane, each under an art name of
its own (C7Engine/GameSetup.Modern.cs), calibrated against the mod's
pictures for the stock folder it drew the unit with (civ3art.MOD_UNIT_ORIGINALS:
the Pirate's Man-O-War, the Troop Ship's Galleon, the Fighter/Bomber's F-15).
Models in models/modern_mod_sea_air.py.
"""

UNITS = {
    # A pirate's steam junk (the mod's picture): a grey iron hull with paddle
    # boxes and a casemate, fan-battened junk sails in civ-colored panels.
    'Pirate': {
        'archetype': 'ship', 'design': 'steam_junk', 'modern': True, 'billow_k': 1.0,
        'colors': {'steel': '#6c7076', 'steel_dark': '#43474e', 'steel_light': '#a4a8ae', 'deck_wood': '#6e5a44',
                   'sail': '#e0d8c4', 'wood_dark': '#4a3a2a'},
    },
    # A WWII attack transport in Measure 22 (navy blue hull, haze grey
    # above): landing craft in davits, king posts, civ-colored hatches.
    'Troop Ship': {
        'archetype': 'ship', 'design': 'troopship', 'modern': True,
        'colors': {'steel': '#44526a', 'steel_dark': '#3a404a', 'steel_light': '#a8aeb4', 'deck_steel': '#4e5256',
                   'house': '#a4aab0', 'house_dark': '#868c94', 'bottom': '#7a3a2c'},
    },
    # A P-38 Lightning fighter-bomber in olive drab over neutral grey.
    'Fighter Bomber': {'archetype': 'aircraft', 'model': 'p38', 'tint': ['band'], 'weapon': 'guns'},
}
