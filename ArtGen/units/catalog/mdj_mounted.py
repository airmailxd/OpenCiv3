"""Medieval Japan (the MDJ ruleset): its mounted samurai, its catapult and
its fire cannon. The riders are 'mdj_horse_rider' (models/mdj_mounted_parts.py,
on top of models/mounted.py), the engines 'mdj_catapult' and 'fire_cannon'
(models/mdj_engines.py, on top of models/siege.py). Their originals are in
the Sengoku conquest's and PTW's Medieval Japan folders."""
from catalog.mounted import _anchor

SKIN = '#c8a07a'
LACQUER = '#34322c'      # black-brown lacquered armor plates
GOLD = '#c4a45e'

UNITS = {
    # A mounted samurai archer on a palomino: dark lacquered armor with
    # civ-colored sleeves, shoulder plates and skirt, pale hakama, a kabuto
    # with a small gold crest, the long yumi and a quiver; civ-colored
    # saddle cloth, bridle and cords with tassels on the horse.
    'Shoni no Kagesuke': {
        'archetype': 'mdj_horse_rider',
        'rider_scale': 1.0,
        'horse': {'coat': '#b4863c', 'mane': '#1e1c1a', 'points': '#6a4c26', 'hoof': '#2a2420', 'muzzle': '#5a4632',
                  'blanket': 'saddle_cloth', 'cloth_span': (-0.36, 0.20), 'cloth_drop': 0.30, 'trim': GOLD,
                  'saddle': '#3a2a20', 'tack': '#3a2a20', 'reins': 'bridle', 'breastplate': False},
        'sode': 'sode', 'kusazuri': 'kusazuri', 'cords': 'cords',
        'yumi': {'height': 1.95, 'grip': 0.36},
        'rider': {
            'prop_scale': 1.15,
            'colors': {'skin': SKIN, 'hair': '#141210', 'helmet': LACQUER, 'top': '#3a3832', 'sode': '#ffffff',
                       'cuirass': LACQUER, 'kusazuri': '#e2dac8', 'belt': '#2a2622', 'boots': '#2a2420',
                       'pants': '#dcd4c0', 'string': '#e8e0c8'},
            'tint': ['sode'],
            'metal': [],
            'build': 'muscular', 'hair': 'none', 'headgear': 'kabuto_maedate', 'headgear_scale': 1.12,
            'top': 'long_sleeved', 'cuirass': True, 'bottom': 'none', 'pants': 'pants', 'wraps': 'boots',
            'back_gear': 'ebira', 'weapon': None, 'offhand': 'bow',
        },
        'attack': 'shoot_bow',
    },
    # A mounted samurai swordsman on a dark bay with a gold chanfron:
    # lacquered armor laced and plated in the civ color, a kabuto with the
    # tall gold kuwagata, a katana; civ-colored cords with tassels round the
    # horse's chest and hindquarters, a red saddle cloth.
    'Nakamaro Horseman': {
        'archetype': 'mdj_horse_rider',
        'rider_scale': 1.0,
        'horse': {'coat': '#76603e', 'mane': '#16120e', 'points': '#2e2216', 'hoof': '#221c16', 'muzzle': '#3a2a1c',
                  'blanket': 'saddle_cloth', 'cloth_span': (-0.34, 0.16), 'cloth_drop': 0.18, 'blanket_color': '#7a2e24',
                  'blanket_tint': False, 'saddle': '#2a2018', 'tack': '#2a2018', 'bridle': '#2a2018', 'bridle_tint': False,
                  'chanfron': True, 'barding_steel': GOLD, 'breastplate': False},
        'sode': 'sode', 'kusazuri': 'kusazuri', 'cords': 'cords',
        'rider': {
            'prop_scale': 1.15,
            'colors': {'skin': SKIN, 'hair': '#141210', 'helmet': '#4a4a48', 'top': '#2e2c2a', 'sode': '#ffffff',
                       'cuirass': LACQUER, 'kusazuri': '#ffffff', 'belt': '#2a2622', 'boots': '#2a2420',
                       'pants': '#3a3834'},
            'tint': ['sode', 'kusazuri'],
            'metal': ['helmet'],
            'build': 'muscular', 'hair': 'none', 'headgear': 'kabuto_kuwagata', 'headgear_scale': 1.12,
            'top': 'long_sleeved', 'cuirass': True, 'bottom': 'none', 'pants': 'pants', 'wraps': 'boots',
            'weapon': 'katana', 'offhand': None,
            # the katana held out low to the right, its point forward and down, as in the original
            'stance': {'hand_r': (0.36, 0.26, 1.00), 'w_main': (-35, 118, 0), 'elbow_r': (0.8, -0.6, -0.3)},
        },
        'attack': 'slash',
        'victory_rear': False,
    },
    # A torsion catapult on a low planked chassis with a very wide, bow-like
    # crossbar, lashed and strapped in the civ color; its crewman in a
    # civ-colored coat and jingasa loads a stone into the cup.
    'Medieval Japanese Catapult': {
        'archetype': 'mdj_catapult', 'fire': 0.22,
        'tint': ['band'],
        'crew': {'colors': {'skin': '#b88a62', 'hair': '#1a1614', 'top': '#ffffff', 'bottom': '#ffffff', 'belt': '#e4dccc',
                            'pants': '#2e2c2a', 'boots': '#3a2e24', 'hat': '#ffffff', 'sandal': '#d8d0c0'},
                 'tint': ['top', 'bottom', 'hat'], 'hair': 'short', 'top': 'sleeved', 'bottom': 'skirt', 'pants': 'pants',
                 'sandals': True,
                 'jingasa': True, 'metal': []},
    },
    # A long iron gun on a low four-wheeled box carriage with civ-colored
    # straps, its gunner in a civ-colored coat and a black jingasa.
    'Fire Cannon': {
        'archetype': 'fire_cannon',
        'tint': ['strap'],
        'crew': {'colors': {'skin': '#c8a07a', 'hair': '#1a1614', 'top': '#ffffff', 'bottom': '#ffffff', 'belt': '#e8e2d4',
                            'pants': '#8a7a5a', 'boots': '#4a3424', 'hat': '#26241f', 'wood': '#6a4a2e'},
                 'tint': ['top', 'bottom'], 'hair': 'short', 'top': 'sleeved', 'bottom': 'skirt', 'pants': 'pants',
                 'wraps': 'boots',
                 'jingasa': True, 'metal': []},
    },
}

for _u, _e in UNITS.items():
    if 'anchor' not in _e and _e['archetype'] == 'mdj_horse_rider':
        try:
            _e['anchor'] = _anchor(_u)
        except Exception:   # no Civ3 install: the framework's default
            pass
