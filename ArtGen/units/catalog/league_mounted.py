"""The League modes' riders that standalone mode drew with a stock unit's
look, under art names of their own (models/league_mounted.py), calibrated
against the stock unit each replaces (civ3art.MOD_UNIT_ORIGINALS): Modern's
Roughrider against the Cossack, QC's Teutonic Knight against the Knight.
"""

SKIN = '#c49a72'

UNITS = {
    # A volunteer cavalryman of 1898: a wide-brimmed slouch hat with a
    # civ-colored band, a civ-colored kerchief, a dark blue shirt, khaki
    # trousers and brown leggings, a carbine held up from the thigh, on a
    # chestnut with a plain dark saddle cloth.
    'Roughrider': {
        'archetype': 'league_horse_rider',
        'rider_scale': 1.04,
        'horse': {'coat': '#8a5a36', 'mane': '#3a2416', 'points': '#4a2e1c', 'muzzle': '#4a3426',
                  'blanket': 'saddle_cloth', 'cloth_drop': 0.22, 'cloth_span': (-0.32, 0.14),
                  'blanket_color': '#3a3c44', 'blanket_tint': False, 'saddle': '#5a3a22',
                  'bridle': '#3a2a1c', 'bridle_tint': False},
        'neckerchief': 'neckerchief',
        'rider': {
            'prop_scale': 1.25,
            'colors': {'skin': SKIN, 'hair': '#3a2a1c', 'top': '#3e4658', 'hat': '#8a7454', 'hatband': '#ffffff',
                       'belt': '#6a5232', 'boots': '#6a4a2c', 'pants': '#9a8a64'},
            'tint': ['hatband', 'neckerchief'],
            'hair': 'short', 'headgear': 'slouch_hat', 'headgear_scale': 1.05, 'top': 'long_sleeved',
            'bottom': 'none', 'pants': 'pants', 'wraps': 'boots', 'sash': 'belt', 'weapon': 'rifle', 'offhand': None,
            # the carbine's butt on the right thigh, the muzzle up
            'stance': {'hand_r': (0.26, 0.24, 1.00), 'w_main': (-4, 0, 0)},
        },
        'attack': 'shoot_gun',
        'victory_rear': True,
    },
    # A knight of the Teutonic Order: a white mantle with the black cross,
    # a great helm with a civ-colored fan crest, a heater shield with the
    # cross, a lance with a civ-colored pennant; the horse in a white
    # caparison with black crosses and a civ-colored hem.
    'Teutonic Knight': {
        'archetype': 'league_horse_rider',
        'rider_scale': 0.99,
        # the raised lance makes the standing height: scale up to the Knight's horse
        'scale': 1.22,
        'horse': {'coat': '#3e3632', 'mane': '#1e1612', 'points': '#34302e', 'blanket': 'caparison',
                  'blanket_color': '#e2dfd6', 'blanket_tint': False, 'cloth_drop': 0.60,
                  'trim': '#ffffff', 'trim_tint': True, 'chanfron': True, 'barding_steel': '#9a9ea6',
                  'saddle': '#4a3424', 'bridle': '#2a2420', 'bridle_tint': False},
        'cross': '#1c1c1e',
        'flank_cross': '#1c1c1e',
        'pennant': True,
        'rider': {
            'prop_scale': 1.2,
            'colors': {'skin': SKIN, 'helmet': '#9a9ca2', 'top': '#e2dfd6', 'belt': '#4a3424', 'boots': '#8a8c92',
                       'pants': '#7a7c80', 'mail': '#7a7c80', 'crest': '#ffffff', 'shield_face': '#e2dfd6'},
            'tint': ['crest'],
            'metal': ['helmet', 'boots'],
            'hair': 'none', 'headgear': 'crested_helm', 'top': 'tunic', 'bottom': 'none', 'pants': 'pants',
            'sleeves': 'mail', 'wraps': 'boots', 'weapon': 'lance', 'offhand': 'shield_kite',
        },
        'attack': 'thrust',
        'victory_rear': False,
    },
}
