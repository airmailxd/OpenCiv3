"""The League modes' riders that standalone mode drew with a stock unit's
look, under art names of their own (models/league_mounted.py), calibrated
against the stock unit each replaces (civ3art.MOD_UNIT_ORIGINALS): Modern's
Roughrider against the Cossack, QC's Teutonic Knight against the Knight.
"""

SKIN = '#c49a72'

UNITS = {
    # A trooper of the 1st US Volunteer Cavalry, Cuba 1898: the drab slouch
    # hat, the dark blue flannel shirt in the civ colour (placket, pockets,
    # collar), a polka-dot neckerchief, braces, the Mills cartridge belt and
    # a holstered revolver, gauntlets, brown canvas trousers and laced
    # leggings, the Krag carbine held up from the thigh; a bay in the
    # McClellan saddle on a folded civ-coloured blanket with its orange
    # stripe, the blanket roll over the pommel, saddlebags, a canteen, a
    # picket rope, the empty carbine boot and hooded stirrups.
    'Roughrider': {
        'archetype': 'league_horse_rider',
        'kit': 'roughrider',
        'rider_scale': 1.0,
        'horse': {'coat': '#6e4a30', 'mane': '#1e1612', 'points': '#2a1e16', 'muzzle': '#3a2a20', 'hoof': '#2a2420',
                  'blanket': None, 'saddle_visible': False, 'girth': False, 'breastplate': True,
                  'saddle': '#2a2220', 'bridle': '#2a2018', 'bridle_tint': False, 'tack': '#2a2018'},
        'rider': {
            'prop_scale': 1.15,
            'colors': {'skin': SKIN, 'hair': '#4a3424', 'top': '#ffffff', 'hat': '#9a8462', 'hatband': '#2a2218',
                       'belt': '#b4a47c', 'boots': '#5a3a22', 'pants': '#7e6a4c'},
            'tint': ['top'],
            'hair': 'short', 'headgear': 'campaign_hat', 'headgear_scale': 1.0,
            'top': 'long_sleeved', 'sleeves': 'top', 'bottom': 'none', 'pants': 'pants', 'wraps': 'boots', 'weapon': 'rifle',
            'offhand': None,
            # the carbine's butt on the right thigh, the muzzle up
            'stance': {'hand_r': (0.30, 0.24, 1.00), 'w_main': (-16, 18, 0)},
        },
        'attack': 'shoot_gun',
        'victory_rear': True,
    },
    # A knight-brother of the Teutonic Order (13th-14th c.): the Order's
    # white mantle with the black cross on the left shoulder over a white
    # surcoat with crosses front and back, mail with steel knee cops, a
    # Topfhelm with a brass cross on its face and civ-colored wings on top, a
    # white heater shield with the black cross, a lance with a civ-colored
    # pennon; the destrier in a full white caparison and crinet with black
    # crosses and a broad civ hem, a steel chanfron, a high war saddle.
    'Teutonic Knight': {
        'archetype': 'league_horse_rider',
        'kit': 'teutonic',
        'rider_scale': 0.99,
        'scale': 1.22,
        'horse': {'coat': '#3e3632', 'mane': '#1e1612', 'points': '#34302e', 'mane_h': 0.02,
                  'blanket': None, 'saddle_visible': False, 'girth': False, 'breastplate': False,
                  'chanfron': True, 'barding_steel': '#9a9ea6',
                  'saddle': '#4a3424', 'bridle': '#ffffff', 'bridle_tint': True, 'tack': '#3a2a1c'},
        'rider': {
            'prop_scale': 1.15,
            'colors': {'skin': SKIN, 'helmet': '#9a9ca2', 'top': '#e4e1d8', 'belt': '#3a2a1c', 'boots': '#7a7c80',
                       'pants': '#7a7c80', 'mail': '#7a7c80'},
            'metal': ['helmet'],
            'tint': ['crest'],
            'hair': 'none', 'headgear': 'topfhelm', 'headgear_scale': 1.05, 'top': 'tunic', 'bottom': 'none',
            'pants': 'pants', 'sleeves': 'mail', 'wraps': 'boots', 'coat_skirt': 'top',
            'weapon': 'lance', 'offhand': 'shield_oval',
        },
        'attack': 'thrust',
        'victory_rear': False,
    },
}
