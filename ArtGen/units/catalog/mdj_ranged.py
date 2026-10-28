"""MDJ (Medieval Japan): its ranged foot units and its settler and worker,
whose originals are in the Sengoku conquest / PTW "Medieval Japan" folders.

Gear, props and the 'mdj_footman' (crossbow) and 'ashigaru_gunner'
archetypes are in models/mdj_ranged_gear.py; the Siege Crossbow's engine
in models/mdj_siege.py. The civ color goes only where the original has it.
"""
from catalog.ancient import BOW_AIM

UNITS = {
    'Medieval Japanese Settler': {
        'archetype': 'footman',
        'colors': {'skin': '#d4ae80', 'hair': '#1c1814', 'top': '#cdc8ba', 'bottom': '#c4bdac', 'belt': '#ffffff',
                   'wraps_band': '#3a3028', 'sandal': '#4a3622', 'wood': '#5a3a22', 'pack': '#4a3a26',
                   'bedroll': '#8a8270', 'collar': '#e8e4da'},
        'tint': ['belt', 'wraps', 'hair_tie', 'armband'],
        'metal': [],
        'desat': 0.85,
        # a topknot tied with a civ-colored cloth, a pale kimono with civ cuffs,
        # sash and leg wraps, a big bundle on the back and a tall staff
        'hair': 'none', 'top': 'long_sleeved', 'bottom': 'skirt', 'wraps': 'wraps', 'sandals': True,
        'gear': ['topknot', 'hair_tie', 'collar', ('bands', {'where': ('wrist',)})],
        'weapon': 'staff', 'props': {'staff': ('staff', {'length': 2.0, 'grip': 1.0})},
        'back': 'backpack', 'pack_strap': True,
        'build': 'lean',
    },
    'Oda Arquebusier': {
        'archetype': 'ashigaru_gunner',
        'colors': {'skin': '#d0a07a', 'hair': '#1c1814', 'belt': '#e6e0d2', 'boots': '#d8ceb4', 'hat': '#4a4a4c', 'stock': '#94703a', 'brass': '#a88a40',
                   'do': '#2c2c2e', 'lacing': '#8a8a8c', 'suneate': '#3a3a3c', 'strap': '#2a2420'},
        'tint': ['top', 'trousers'],
        'desat': 0.8,
        # an ashigaru gunner: a pointed iron jingasa, a black lacquered cuirass
        # and tassets over a civ-colored kimono and trousers, black shin guards,
        # a katana at the hip; the long tanegashima held muzzle down at his side
        'hair': 'short', 'top': 'long_sleeved', 'legs': 'trousers', 'feet': 'shoes',
        'gear': [('jingasa', {'brim': 0.21, 'height': 0.23}), ('do', {'lacing_color': '#8a8a8c', 'lacing_tint': False}),
                 ('kusazuri', {'lacing_color': '#8a8a8c', 'lacing_tint': False, 'n': 8, 'length': 0.2, 'spread': 1.05}),
                 ('obi', {'color': '#e6e0d2', 'z': 0.10, 'h': 0.05}), 'suneate', 'katana_hip'],
        'gun': 'tanegashima', 'hold': 'low', 'aim': 'shoulder',
        'poses': {'carry': {'hand_r': (0.30, 0.10, 0.92), 'w_main': (159, 0, 164), 'support': 0.0,
                            'hand_l': (-0.36, 0.06, 0.86)}},
        'fire_start': 'aim', 'fire_end': 'aim', 'flash': 'musket', 'smoke': 'heavy', 'flash_size': 0.40,
        'shots': {'ATTACK1': [5, 6]},
        'fortify': 'aim', 'victory': 'fist',
        'actions': {'ATTACK1': 'fire'},
    },
    'Siege Crossbow': {
        'archetype': 'siege_crossbow', 'fire': 0.08,
        'tint': ['panel', 'band'],
        'crew': {'colors': {'skin': '#c8966a', 'hair': '#1c1814', 'top': '#ffffff', 'bottom': '#ffffff',
                            'belt': '#e8e2d4', 'sandal': '#5a3e26', 'wraps': '#4a3624', 'wraps_band': '#2a1e14',
                            'trousers': '#5a4030'},
                 'tint': ['top', 'bottom', 'cap'], 'metal': [], 'desat': 0.85,
                 'hair': 'short', 'top': 'sleeved', 'bottom': 'skirt', 'wraps': 'wraps', 'sandals': True,
                 'gear': [('soft_cap', {'height': 0.07, 'lean': 0.03}), ('trousers', {'baggy': 1.1, 'color': '#5a4030',
                                                                                    'knee': False})]},
    },
    'Stone Crossbow': {
        'archetype': 'mdj_footman',
        'colors': {'skin': '#b48a64', 'hair': '#1c1814', 'top': '#ffffff', 'bottom': '#ffffff', 'belt': '#ece6d6',
                   'trousers': '#7a6450', 'wraps': '#1e1a18', 'wraps_band': '#d8d2c4', 'sandal': '#86521c',
                   'pad': '#c0a050', 'strap': '#4a3422', 'pouch': '#6a4a2a', 'stock': '#6e5238', 'prod': '#4a3a2c',
                   'band': '#2a2420', 'steel': '#8e8e92'},
        'tint': ['top', 'bottom', 'cap'],
        'metal': [],
        'desat': 0.8, 'keep_saturation': ['pad'],
        # a peaked civ-colored cap, a sleeveless civ tunic over bare brawny arms,
        # ochre shoulder pads, brown trousers bound with black leg wraps; a
        # heavy crossbow held across the hips, a bolt bag at the hip
        'hair': 'short', 'top': 'vest', 'bottom': 'skirt', 'wraps': 'wraps', 'sandals': True,
        'gear': [('soft_cap', {'height': 0.09, 'lean': 0.04}),
                 ('pauldrons', {'mat': 'pad', 'color': '#c0a050', 'finish': 'matte', 'size': 1.2}),
                 ('trousers', {'baggy': 1.15, 'color': '#7a6450', 'knee': False}), ('chest_strap', {'color': '#4a3422'}),
                 'tool_pouch'],
        'weapon': 'xbow', 'crossbow': 'xbow', 'props': {'xbow': ('crossbow', {'thick': 2.0, 'span': 0.9, 'length': 0.9})},
        'twohand': -0.30, 'twohand_actions': ['DEFAULT', 'RUN', 'ATTACK1', 'FORTIFY', 'FIDGET'],
        'build': 'muscular',
        'stance': {'hand_r': (0.26, 0.22, 0.92), 'w_main': (-92, 0, 62), 'elbow_r': (0.8, -0.6, -0.2)},
        'events': {'ATTACK1': {'release': 0.62}},
        'actions': {'ATTACK1': 'attack_crossbow', 'FORTIFY': 'fortify_kneel'},
    },
    'Otomo no Makuta': {
        'archetype': 'footman',
        'colors': {'skin': '#d8b488', 'hair': '#141214', 'top': '#b4a284', 'bottom': '#2e2e30', 'belt': '#ffffff',
                   'trousers': '#bca888', 'boots': '#2a2220', 'kabuto': '#505356', 'do': '#36393c',
                   'yumi': '#b08a50', 'string': '#e0dccc', 'ebira': '#3a2c20'},
        'tint': ['lacing', 'crest', 'bow_tip', 'belt'],
        'metal': [],
        'desat': 0.8,
        # a samurai archer in dark lacquered o-yoroi laced in the civ color: a
        # horned kabuto, big square shoulder guards, pale hakama bound at the
        # shins; the tall asymmetric yumi, a box quiver of arrows on the back
        'hair': 'none', 'top': 'long_sleeved', 'bottom': 'skirt', 'wraps': 'boots',
        'gear': ['kabuto', 'do', ('kusazuri', {'n': 9, 'width': 0.16, 'spread': 1.12, 'splay': 16}), ('sode', {'size': 0.78}),
                 ('trousers', {'baggy': 1.25, 'color': '#bca888'}), 'ebira'],
        'weapon': None, 'offhand': 'bow',
        'offhand_mesh': ('yumi', {}),
        'build': 'muscular',
        # the bow held slanting across the front; fortified: drawn and aimed
        'stance': {'hand_l': (-0.24, 0.30, 0.98), 'w_off': (112, 0, 106), 'elbow_l': (-0.6, -0.8, -0.2),
                   'hand_r': (0.32, 0.16, 0.92)},
        'overrides': {'FORTIFY': BOW_AIM},
        'actions': {'ATTACK1': 'attack_shoot'},
    },
    'Medieval Japanese Worker': {
        'archetype': 'footman',
        'colors': {'skin': '#b4845b', 'hair': '#1c1814', 'top': '#c4ae86', 'bottom': '#1e1a18', 'shorts': '#1e1a18',
                   'belt': '#dcd6c8', 'sandal': '#5a3e26', 'wood': '#6a4a30', 'steel': '#8e8e92'},
        'tint': ['hat', 'armband'],
        'metal': [],
        'desat': 0.85,
        # a wide civ-colored hat, a short-sleeved tan jacket with civ cuffs, black shorts
        'hair': 'short', 'top': 'sleeved', 'bottom': 'skirt', 'sandals': True,
        'gear': [('kasa', {'brim': 0.27, 'height': 0.10}), ('bands', {'where': ('arm',), 'r': 0.082}),
                 ('trousers', {'mat': 'shorts', 'color': '#1e1a18', 'knee': False, 'baggy': 1.08})],
        'weapon': 'shovel',
        'tool_scale': (1.4, 1.4, 1.1), 'weapon_scale': (1.3, 1.3, 1.0),
        'build': 'lean',
        'actions': {'CAPTURE': 'work_dig'},
        # a little hunched, the shovel held across the hips, blade to the left
        'stance': {'hand_r': (0.26, 0.20, 0.86), 'hand_l': (-0.22, 0.24, 0.88), 'w_main': (-92, 0, 88),
                   'root_pos': (0, 0, -0.05), 'spine': (-8, 0, 0), 'head': (8, 0, 0),
                   'elbow_l': (-0.6, -0.8, -0.2)},
    },
}
