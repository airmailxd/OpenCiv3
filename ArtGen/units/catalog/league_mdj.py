"""MDJ's (Medieval Japan) units that standalone mode drew with a stock
unit's look, under art names of their own: the Warship as an atakebune
(models/league_sea.py), calibrated against the stock Galley it replaces,
and the Hojo General as a Sengoku commander of the Hojo (the 'hojo_general'
archetype, models/league_hojo.py), calibrated against the
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
    # A Sengoku commander of the Later Hojo of Odawara, on foot: black
    # lacquered tosei-gusoku laced in red (ribbed kabuto with tall gold
    # kuwagata and a gold maedate bearing the clan's mitsu-uroko, a red half
    # mask with a grey moustache, okegawa-do, sode, kusazuri, kote, suneate),
    # over it the civ-coloured jinbaori with the mitsu-uroko large on its
    # back, a pale sash, the daisho at the hip; the gunbai war fan in his
    # right hand, the katana drawn to fight.
    'Hojo General': {
        'archetype': 'hojo_general',
        'scale': 0.76,
        'colors': {'skin': '#d0a078', 'hair': '#141214', 'top': '#3e3028', 'trousers': '#6a5846', 'bottom': '#6a5846',
                   'belt': '#2a241e', 'sandal': '#3a2e24', 'helmet': '#6a4e3c', 'gold': '#c4a050',
                   'lacing': '#c4503e', 'shikoro': '#6a4e3c', 'kote': '#3e3028', 'kote_plate': '#6a4e3c', 'wood': '#3a2a1c'},
        'tint': ['jinbaori'],
        'metal': [],
        'desat': 0.85, 'keep_saturation': ['gold', 'menpo', 'sun'],
        'build': {'bulk': 0.92},
        'hair': 'none', 'top': 'long_sleeved', 'bottom': 'skirt', 'sandals': True,
        'gear': ['hojo_kabuto', 'hojo_menpo', 'hojo_nodowa',
                 ('do', {'tint': False, 'color': '#6a4e3c', 'lacing': 'do_rivets', 'lacing_color': '#9a8048', 'finish': 'gloss'}),
                 ('sode', {'tint': False, 'color': '#6a4e3c', 'length': 0.22, 'lacing_color': '#c4503e', 'lames': 4}),
                 ('kusazuri', {'tint': False, 'color': '#6a4e3c', 'length': 0.20, 'n': 7, 'flare': 0.04, 'z0': 0.06,
                               'lacing_color': '#c4503e', 'lames': 3}),
                 'hojo_do_mon', 'hojo_kote',
                 ('suneate', {'tint': False, 'color': '#6a4e3c', 'ties_color': '#c4503e'}),
                 ('trousers', {'baggy': 1.1}),
                 ('hojo_jinbaori', {'open_front': 30.0, 'length': 0.40, 'gap': 0.055}), 'hojo_obi', 'hojo_daisho'],
        'weapon': 'sword', 'props': {'sword': ('katana', {'size': 1.1}), 'gunbai': ('hojo_gunbai', {})},
        'fan_actions': ['DEFAULT', 'RUN', 'ATTACK1', 'DEATH'],
        # the fan held out to the right at chest height, the left hand at the sword
        'stance': {'foot_r': (0.22, 0.05, 0.08), 'foot_l': (-0.22, -0.05, 0.08),
                   'hand_l': (-0.24, 0.16, 0.98), 'elbow_l': (-0.8, -0.4, -0.3),
                   'hand_r': (0.48, 0.18, 1.18), 'w_main': (22, 16, 0), 'elbow_r': (0.9, -0.3, -0.3)},
                'actions': {'ATTACK1': 'attack_slash'},
    },
}
