"""The land vehicles of the Civ Players League's Modern 2.4 (C7/Lua/modern),
under art names of their own (models/modern_mod_vehicles.py). Their
originals are the mod's pictures (civ3art.MOD_UNIT_ORIGINALS): the Laser
Tank's under Modern Armor, the Bradley's under Tank, the Convoy's (a Humvee)
under Panzer and the Iron Dome's (a Rapier launcher) under Cannon.
"""

UNITS = {
    'Laser Tank': {
        'archetype': 'mod_armor', 'model': 'laser_tank', 'size': 1.017,
        'tint': ['decal', 'beam'],
    },
    'Bradley': {
        'archetype': 'mod_armor', 'model': 'light_ifv', 'scale': 1.08, 'anchor': (117.0, 121.3),
        'tint': ['decal', 'tow'],
    },
    'Convoy': {
        'archetype': 'mod_armor', 'model': 'humvee', 'size': 1.1, 'turret_off': False,
        'tint': ['decal', 'helmet'],
    },
    'Iron Dome': {
        'archetype': 'mod_armor', 'model': 'battery', 'size': 1.42,
        'tint': ['decal'],
    },
}
