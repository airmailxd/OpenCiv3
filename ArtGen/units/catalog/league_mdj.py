"""MDJ's (Medieval Japan) units that standalone mode drew with a stock
unit's look, under art names of their own: the Warship as an atakebune
(models/league_sea.py), calibrated against the stock Galley it replaces,
and the Hojo General as an armored samurai commander (the 'footman'
archetype with models/mdj_foot_gear.py's armor), calibrated against the
stock Army Ancient Times (civ3art.MOD_UNIT_ORIGINALS).
"""
from catalog.ships import WOOD_ANCIENT, _c

UNITS = {
    # A walled war junk: a dark armored house, a tower with tiled roofs, a
    # mat sail, a civ-colored curtain round the walls and a stern banner.
    'Japanese Warship': {
        'archetype': 'ship', 'brace': 14, 'billow_k': 1.2, 'design': 'atakebune', 'fire_t': 0.1,
        'colors': _c(WOOD_ANCIENT, wood='#7a5e40', wood_dark='#3a2c20', wood_light='#a88c64', deck='#8a7454',
                     sail='#d8c8a0', roof='#4a4a4c', gold='#c4a45e'),
    },
    # A samurai general: black lacquered armor laced in red, a kabuto with
    # tall gold horns, a big civ-colored sashimono on his back, a katana.
    'Hojo General': {
        'archetype': 'footman',
        'colors': {'skin': '#d0a078', 'hair': '#141214', 'top': '#5a2a24', 'trousers': '#3a2e26', 'bottom': '#3a2e26',
                   'belt': '#2a241e', 'obi': '#dcd8cc', 'sandal': '#3a2e24', 'helmet': '#2e2c2a', 'gold': '#c4a050',
                   'lacing': '#8a2e24', 'shikoro': '#2a2826', 'wood': '#3a2a1c'},
        'tint': ['sashimono'],
        'metal': [],
        'desat': 0.8, 'keep_saturation': ['gold'],
        'build': {'build': 'muscular', 'bulk': 1.0},
        'hair': 'none', 'top': 'long_sleeved', 'bottom': 'skirt', 'sandals': True,
        'gear': [('kabuto', {'horns': 0.46, 'spread': 0.20}),
                 ('do', {'tint': False, 'color': '#2a2826', 'lacing_color': '#8a2e24', 'belt': 'obi'}),
                 ('sode', {'tint': False, 'color': '#2a2826', 'length': 0.24, 'lacing_color': '#8a2e24'}),
                 ('kusazuri', {'tint': False, 'color': '#2a2826', 'length': 0.26, 'flare': 0.04, 'z0': 0.06,
                               'lacing_color': '#8a2e24'}),
                 ('suneate', {'tint': False, 'color': '#2a2826'}),
                 ('trousers', {'baggy': 1.1}),
                 ('sashimono', {'pole': 1.55, 'height': 0.80, 'width': 0.42})],
        'weapon': 'sword', 'props': {'sword': ('katana', {'size': 1.1})},
        'twohand': 0.17, 'twohand_actions': ['ATTACK1'],
        'actions': {'ATTACK1': 'attack_slash'},
    },
}
