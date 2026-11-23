"""Modern's (C7/Lua/modern) own ships and plane, each under an art name of
its own (C7Engine/GameSetup.Modern.cs), calibrated against the mod's
pictures for the stock folder it drew the unit with (civ3art.MOD_UNIT_ORIGINALS:
the Pirate's Man-O-War, the Troop Ship's Galleon, the Fighter/Bomber's F-15).
Models in models/modern_mod_sea_air.py.
"""

UNITS = {
    # A pirate's steam junk (the mod's picture): a tarred junk hull with
    # gunports and paddle boxes, plain junk sails, the black flag.
    'Pirate': {
        'archetype': 'ship_r2', 'design': 'steam_junk', 'modern': True, 'billow_k': 1.0, 'scale': 0.79, 'rig_sink': 0.9, 'plume': 1.0,
        'colors': {'deck_wood': '#8a7254', 'sail': '#d8cbab', 'wood_dark': '#3e3022', 'wood': '#5a4634',
                   'rail': '#9a6a34'},
    },
    # A WWII attack transport in Measure 22 (navy blue hull, haze grey
    # above): landing craft in davits, king posts, civ-colored hatches.
    'Troop Ship': {
        'archetype': 'ship_r2', 'design': 'troopship', 'modern': True,
        'colors': {'steel': '#6a727a', 'steel_dark': '#3c4048', 'steel_light': '#b4b8bc', 'deck_steel': '#5a5e62',
                   'house': '#a4aab0', 'house_dark': '#868c94', 'bottom': '#7a3a2c'},
    },
    # A P-38 Lightning fighter-bomber in olive drab over neutral grey.
    'Fighter Bomber': {'archetype': 'aircraft_r2', 'model': 'p38', 'size': 0.82, 'drop': 5, 'tint': ['band'], 'weapon': 'guns'},
}
