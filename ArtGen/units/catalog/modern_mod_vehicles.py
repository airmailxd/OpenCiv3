"""The land vehicles of the Civ Players League's Modern 2.4 (C7/Lua/modern),
under art names of their own (models/modern_mod_vehicles.py). Their
originals are the mod's pictures (civ3art.MOD_UNIT_ORIGINALS): the Laser
Tank's under Modern Armor, the Bradley's under Tank, the Convoy's (a Humvee)
under Panzer and the Iron Dome's (a Rapier launcher) under Cannon.
"""

UNITS = {
    'Laser Tank': {
        'archetype': 'mod_armor', 'model': 'laser_tank',
        'tint': ['decal', 'beam'],
    },
    'Bradley': {
        'archetype': 'mod_armor', 'model': 'light_ifv',
        'tint': ['decal'],
    },
    'Convoy': {
        'archetype': 'mod_armor', 'model': 'humvee', 'turret_off': False,
        'tint': ['decal', 'helmet'],
    },
    'Iron Dome': {
        'archetype': 'mod_armor', 'model': 'battery',
        'tint': ['decal'],
    },
}
