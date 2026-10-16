"""Remaster entries for 22 more Civ3 foot units (plain data, same format as
remaster.py's REMASTER: each entry is merged over the unit's catalog entry,
dicts one level deep).

The notes (notes/<Unit>.md) are the truth where they differ from the catalog;
each 'tint' list (plus any ('where', 'tint') band) is the note's "Tinted (civ
color)" line, as far as the supported keys can say it. Direction: Civ3
remastered, not AI-looking -- battle-hardened, weathered tan-brown skin, grime,
dark realistic materials, heavier builds for warriors and lean wiry ones for
scouts and workers.

Every weapon key maps to a builder in props.py (or clubs.war_club); every
offhand is a props.py builder. Shields go through offhand 'shield_round' /
'shield_oval' (equip places anything whose offhand name starts with 'shield'
on the forearm) with the real prop in 'offhand_mesh'. What is still
approximated is marked "MISSING:" and listed under MISSING FEATURES at the
bottom.
"""

REMASTER = {
    'Berserk': {
        # brawny, wide crouch, long axe two-handed low across the hips; yellow
        # braids and beard under a conical iron cap; a shaggy fur cloak down
        # the whole back; civ-color tunic, sleeves, wristbands and trousers
        'macro': {'muscle': 1.0, 'weight': 0.95, 'height': 0.78, 'proportions': 0.6},
        'race': {'caucasian': 0.9, 'african': 0.0, 'asian': 0.1},
        'colors': {'skin': '#a87a5a', 'hair': '#d8c020', 'helmet': '#5e5e62', 'belt': '#2e2016',
                   'wraps': '#6a4a30', 'wraps_band': '#241810', 'bracers': '#4e3a26', 'steel': '#a8aaae',
                   'iron': '#3e3e40', 'wood': '#5a4028', 'leather': '#3e2a1a'},
        'helmet': None, 'headgear': 'conical_helmet',
        'hair': 'long', 'hair_asset': 'braid01', 'mane': 0.22, 'beard': True,   # MISSING: braids (two braids down the chest)
        'top': 'sleeved', 'bottom': 'long', 'wraps': 'wraps', 'bracers': True,
        'pelt_cape': ('#6a4a30', '#3a2818'), 'pelt_kind': 'cloak',
        'fur_vest': ('#5e4430', '#33241a'),     # the brown fur over the torso front (only sleeves/trousers civ color)
        'gear': [],
        'weapon': 'axe',
        'props': {'axe': ('battle_axe', {'length': 0.75, 'scale': 1.6})},
        'stance_name': 'low_across',            # MISSING: two_hand_low (haft level across the hips, head out right)
        'two_hands': True, 'tool_dir': (-0.8, -0.2, 0.25),
        'tint': ['top', 'bottom', 'bracers'],
        'grime': 0.85, 'upper_bulk': 1.3,
    },
    'Bowman': {
        # bow low in the left hand, a loose arrow in the right; a civ-color
        # cap with a gold knob, leather vest over a civ-color knee tunic,
        # quiver on the back, leg wraps and sandals
        'macro': {'muscle': 1.0, 'weight': 0.7, 'height': 0.75, 'proportions': 0.72},
        'colors': {'skin': '#8e6a4a', 'hair': '#1a120c', 'cuirass': '#6a4a28', 'leather': '#3e2a1a',
                   'belt': '#2a1c12', 'wraps': '#7a6448', 'wraps_band': '#3a2a1a', 'sandal': '#2e2016',
                   'wood': '#5a3e24', 'string': '#8a7a5a'},
        'helmet': 'cap', 'metal': [], 'cap_knob': True,
        'hair': 'short', 'beard': True,
        'cuirass': True, 'top': 'tunic', 'bottom': 'skirt',   # the cuirass leaves the tunic's sleeves showing
        'wraps': 'wraps', 'sandals': True,
        'gear': [('chest_strap', {'color': '#2e2016'})],
        'weapon': 'arrow', 'props': {'arrow': ('arrow', {'length': 0.85})},   # a loose arrow in the right hand
        'offhand': 'bow', 'offhand_mesh': ('bold_bow', {'height': 1.35, 'bend': 0.18, 'recurve': 0.1}),
        'back': 'quiver', 'stance_name': 'bow_low',
        'tint': ['helmet', 'top', 'bottom'],
        'grime': 0.75, 'upper_bulk': 1.25,
    },
    'Chasquis Scout': {
        # an Inca runner: a broad civ-color fan headdress framing the face,
        # long black hair, bare chest, civ-color wrap skirt with a gold belt,
        # civ-color bands at wrist, knee and ankle; a star mace held low
        'macro': {'muscle': 1.0, 'weight': 0.65, 'height': 0.68, 'proportions': 0.72},
        'race': {'caucasian': 0.1, 'african': 0.2, 'asian': 0.7},
        'colors': {'skin': '#a86c44', 'hair': '#120c08', 'belt': '#b08a2a', 'bottom': '#5a4030',
                   'wood': '#4e3420', 'stone': '#3e3c3a', 'helmet': '#5a4030'},
        'helmet': None, 'headgear': 'fan_headdress',
        'hair': 'long', 'hair_asset': 'long01', 'beard': False,
        'top': None, 'bottom': 'loincloth', 'loin_length': (0.38, 0.34), 'torn': False,
        'bracers': False,
        'bands': [('wrist', 'tint'), ('knee', 'tint'), ('ankle', 'tint')],
        'wraps': None, 'sandals': False, 'gear': [],
        'weapon': 'club',
        'props': {'club': ('star_mace', {'length': 0.55})},
        'stance_name': 'hang',
        'tint': ['helmet', 'bottom'],
        'grime': 0.7, 'upper_bulk': 1.25,
    },
    'Enkidu Warrior': {
        # long spear upright, butt on the ground; wild black hair and a chest-
        # length beard, a civ-color headband with streamers; light tan fur
        # over the torso and down the back to the calves, a fur kilt; civ-
        # color arm, wrist, waist and knee bands
        'macro': {'muscle': 1.0, 'weight': 0.9, 'height': 0.75, 'proportions': 0.62},
        'race': {'caucasian': 0.6, 'african': 0.2, 'asian': 0.2},
        'colors': {'skin': '#9a6e4c', 'hair': '#140e0a', 'top': '#9a8462', 'bottom': '#8a7656',
                   'wraps': '#b8a888', 'wraps_band': '#7a6a50', 'helmet': '#3a2a1c', 'belt': '#3a2a1c',
                   'wood': '#5a3420', 'steel': '#7e7a72'},
        'helmet': 'headband',                   # MISSING: streamers (ribbons flicking up off the headband)
        'hair': 'long', 'hair_asset': 'long01', 'mane': 0.3,
        'beard': True,                          # MISSING: long_beard (black, to mid-chest)
        'top': None, 'bottom': 'skirt',         # MISSING: fur kilt (the skirt is cloth under the vest)
        'fur_vest': ('#b09a70', '#7a6848'),
        'pelt_cape': ('#b09a70', '#7a6848'), 'pelt_kind': 'cloak',
        'bracers': False,
        'bands': [('upperarm', 'tint'), ('wrist', 'tint'), ('waist', 'tint'), ('knee', 'tint')],
        'wraps': 'wraps', 'sandals': False, 'back': None, 'gear': [],
        'weapon': 'spear',
        'props': {'spear': ('spear', {'length': 2.7, 'grip': 1.1, 'head': 1.2})},
        'stance_name': 'upright',
        'tint': ['helmet', 'belt'],
        'grime': 0.85, 'upper_bulk': 1.25,
    },
    'Enslaved Worker': {
        # a gaunt labourer: long hair and beard, a sleeveless olive tunic,
        # one civ-color strap across the chest, iron shackles, a spade
        'macro': {'muscle': 0.8, 'weight': 0.4, 'height': 0.7, 'proportions': 0.72}, 'age': 0.6,
        'colors': {'skin': '#a07a58', 'hair': '#3a2616', 'top': '#a8ae88', 'bottom': '#a8ae88',
                   'belt': '#2e2418', 'sandal': '#2e2016', 'wood': '#4e3622', 'steel': '#5a5a5a',
                   'iron': '#2e2e30'},
        'hair': 'long', 'hair_asset': 'long01', 'beard': True,
        'top': 'vest', 'bottom': 'skirt',       # the sleeveless knee-length tunic
        'gear': [('chest_strap', {'color': '#3a2818'})],
        'sandals': True, 'wraps': None,
        # MISSING: shackles placement -- props.shackles exists, but equip has
        # no slot that puts it on both wrists (and the ankle chain)
        'weapon': 'shovel',
        'props': {'shovel': ('shovel', {'length': 1.45})},
        'stance_name': 'hang',            # MISSING: spade_upright (blade on the ground at his right side)
        'hang_dir': (-0.55, -0.3, -0.78),          # the spade upright at his side (one hand)
        'tint': ['strap'],
        'grime': 0.9, 'upper_bulk': 1.05,
    },
    'Explorer': {
        # a conquistador-era explorer: steel comb morion with a civ-color
        # plume, steel breastplate over puffy civ-color sleeves, baggy slate
        # breeches, pale high boots; a map in hand, rapier sheathed at the hip
        'macro': {'muscle': 0.85, 'weight': 0.6, 'height': 0.75, 'proportions': 0.75}, 'age': 0.6,
        'race': {'caucasian': 0.8, 'african': 0.1, 'asian': 0.1},
        'colors': {'skin': '#9a7254', 'hair': '#4a3624', 'helmet': '#8e9096', 'cuirass': '#8a8c92',
                   'bottom': '#3e3c4e', 'boots': '#8a8680', 'belt': '#2a1e14', 'steel': '#a8aaae',
                   'parchment': '#cfc4a2'},
        'helmet': None, 'headgear': 'morion_big', 'metal': ['cuirass'],
        'hair': 'short', 'beard': True,
        'cuirass': True, 'top': 'sleeved', 'puffy': True,   # puffed civ-color sleeves, baggy breeches
        'bottom': 'breeches', 'wraps': 'boots',  # pale high boots below the breeches
        'gear': [],
        'hip': ('sheathed_sword', {'rapier': True}),   # the rapier sheathed at the left hip
        'weapon': None,
        'offhand': 'map', 'offhand_mesh': ('map', {'w': 0.34, 'h': 0.26}),
        'stance_name': 'upright',
        'tint': ['top', 'plume'],
        'grime': 0.6, 'upper_bulk': 1.1,
    },
    'Gallic Swordsman': {
        # shirtless and brawny, ginger hair spiked up, check-patterned civ-
        # color trousers, a civ-color band on the sword forearm, a long sword
        # low and a small round shield (brown face, silver boss and rim)
        'macro': {'muscle': 1.0, 'weight': 0.85, 'height': 0.75, 'proportions': 0.65},
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#c8946c', 'hair': '#d07a30', 'bottom': '#3a3a40', 'belt': '#2e2016',
                   'boots': '#3e2a1a', 'bracers': '#3a2a1c', 'steel': '#a8aaae', 'bronze': '#9a9ca2',
                   'shield_face': '#6a4a2e', 'shield_rim': '#8a8c90', 'leather': '#3e2a1a'},
        'helmet': None,
        'hair': 'short', 'hair_asset': 'short04', 'beard': False,   # MISSING: spiked_hair (limed, straight up)
        'top': None, 'bottom': 'long', 'patterns': {'bottom': ('check', '#141414', 34.0)},   # checked trousers
        'boots': True, 'wraps': 'boots',
        'bracers': True,                        # MISSING: one_bracer (the sword arm only)
        'gear': [],
        'weapon': 'sword',
        'props': {'sword': ('long_sword', {'length': 0.85, 'width': 0.055, 'guard': 0.16})},
        'offhand': 'shield_round', 'offhand_mesh': ('plain_round', {'r': 0.27}),
        'stance_name': 'guard',
        'tint': ['bottom', 'bracers'],
        'grime': 0.8, 'upper_bulk': 1.3,
    },
    'Hoplite': {
        # bronze Corinthian helm with a civ-color crest, bronze cuirass with
        # civ-color trim, leather pteruges, bronze greaves; a long spear
        # upright and the big round aspis across the front, rim in civ color
        'macro': {'muscle': 1.0, 'weight': 0.85, 'height': 0.75, 'proportions': 0.7},
        'colors': {'skin': '#94704f', 'hair': '#2a1c12', 'helmet': '#b88a3e', 'crest': '#3a2a1c',
                   'cuirass': '#b88a3e', 'greaves': '#b88a3e', 'bottom': '#4a3420', 'belt': '#2e2016',
                   'bracers': '#3a2a1c', 'shield_face': '#9a7a3a', 'bronze': '#8a6a36', 'wood': '#3e2a1a'},
        'helmet': 'crested', 'metal': ['helmet', 'cuirass'],
        'beard': True,
        'cuirass': True, 'top': None, 'neck': 'high',
        # the cuirass trim: tinted bands at the neckline and the waist edge
        'bands': [('neck', 'tint'), ('waist', 'tint')],   # MISSING: armhole_trim (tinted edges round the armholes)
        'bottom': 'loincloth', 'bottom_material': 'leather', 'loin_length': (0.46, 0.42), 'torn': False,
        'wraps': 'greaves', 'bracers': True,
        'gear': [],
        'weapon': 'spear',
        'props': {'spear': ('spear', {'length': 2.55, 'grip': 1.15, 'head': 1.1})},
        # the aspis: a big round bronze board, only its rim in civ color, a
        # black painted device with a red accent on the face; upright (no
        # 45-degree roll), so the device's chevron points up
        'offhand': 'shield_round',
        'offhand_mesh': ('shield_round', {'r': 0.52, 'dome': 0.07, 'boss': False, 'tint': 'rim', 'emblem': 'aspis'}),
        'shield_roll': 0.0,
        'stance_name': 'upright',
        'tint': ['crest', 'shield_emblem', 'bracers'],
        'grime': 0.7, 'upper_bulk': 1.25,
    },
    'Immortals': {
        # Persian guard: white headcloth with a face veil, tan tunic with civ-
        # color short sleeves, a brown baldric, a civ-color waist sash, baggy
        # trousers with civ-color stripes, yellow shoes; scimitar low
        'macro': {'muscle': 0.95, 'weight': 0.75, 'height': 0.78, 'proportions': 0.72},
        'race': {'caucasian': 0.6, 'african': 0.1, 'asian': 0.3},
        'colors': {'skin': '#8e6a4a', 'hair': '#140e0a', 'helmet': '#cfc9b8', 'top': '#9a8058',
                   'bottom': '#5e4a34', 'belt': '#3a2a1c', 'boots': '#a08434', 'steel': '#a8aaae',
                   'gold': '#a8842e', 'veil': '#cfc9b8'},
        'helmet': 'hood', 'veil': True, 'veil_drop': 0.215,         # white headcloth and a veil over the chin and mouth
        'beard': True,
        # a tan sleeved tunic; 'puffy' adds the sleeve layer over the upper
        # arms (tinted: the civ-color short sleeves) and makes the trousers baggy
        'top': 'sleeved', 'puffy': True, 'cuirass': False,
        'bands': [('wrist', 'gold'), ('waist', 'tint')],   # gold bracelets, the civ-color sash
        'bottom': 'long',                       # MISSING: trouser_stripes (tinted vertical stripes)
        'wraps': 'boots',
        'gear': [('chest_strap', {'color': '#3a2818'})],
        'weapon': 'sword',
        'props': {'sword': ('sabre', {'length': 0.95, 'width': 0.05, 'curve': 0.16})},   # props.scimitar has no curve param: its sabre, curved more
        'stance_name': 'hang',
        'tint': ['belt', 'sleeves'],
        'grime': 0.65, 'upper_bulk': 1.2,
    },
    'Impi': {
        # Zulu: crouched and bow-legged, a short broad stabbing spear low, a
        # tall cowhide shield upright; civ-color feather headdress, arm and
        # wrist bands, and cow-tail tufts on the calves
        'macro': {'muscle': 1.0, 'weight': 0.7, 'height': 0.78, 'proportions': 0.7},
        'race': {'caucasian': 0.0, 'african': 1.0, 'asian': 0.0},
        'colors': {'skin': '#5e3c26', 'hair': '#0e0a08', 'helmet': '#2a1e14', 'bottom': '#4a3424',
                   'belt': '#2e2016', 'wraps': '#d8d0c0', 'wraps_band': '#2a2420', 'gold': '#b08a2a',
                   'hide': '#d4ccbc', 'hide_dark': '#1e1612', 'wood': '#3e2a1a', 'steel': '#a8aaae'},
        'helmet': None, 'headgear': 'feather_headdress',
        'hair': 'short', 'hair_asset': 'afro01', 'beard': False,
        'top': None, 'bottom': 'loincloth', 'bottom_material': 'leather', 'loin_length': (0.4, 0.38), 'torn': True,
        'bracers': False,
        'bands': [('upperarm', 'tint'), ('wrist', 'tint'), ('knee', 'gold')],   # yellow bands above the tufts
        # the cow-tail tufts; 'wraps' is listed as tinted per the note, but
        # G.fur wraps don't take the tint yet
        'wraps': 'wraps',                       # MISSING: tinted_fur_tufts
        'sandals': False,
        'gear': [('chest_strap', {'color': '#3a2818'})],
        'weapon': 'spear',
        # the iklwa: short with a broad blade
        'props': {'spear': ('spear', {'length': 1.25, 'grip': 0.45, 'head': 1.7, 'butt': False})},
        'offhand': 'shield_oval', 'offhand_mesh': ('cowhide_shield', {'w': 0.55, 'h': 1.15}),
        'shield_at': 'left_hip',
        'stance_name': 'hang',
        # a wider stance: these override the stance's leg bones
        'pose_extra': [('thigh_l', (0.3, -0.1, -1)), ('thigh_r', (-0.3, -0.1, -1)),
                       ('calf_l', (0.1, 0.15, -1)), ('calf_r', (-0.1, 0.15, -1))],
        'headgear_params': {'style': 'bushy'},  # many short civ-color feathers in a round bushy crown
        'tint': ['helmet', 'wraps'],
        'grime': 0.75, 'upper_bulk': 1.25,
    },
    'Inquisitor': {
        # solemn: a floor-length charcoal hooded robe, gold cross and sash,
        # civ-color book cover, shoulder band and sleeve cuffs; book raised
        # in the left hand, a torch low in the right
        'macro': {'muscle': 0.6, 'weight': 0.55, 'height': 0.72, 'proportions': 0.75}, 'age': 0.75,
        'colors': {'skin': '#9a7656', 'top': '#2e2e32', 'bottom': '#2e2e32', 'helmet': '#2a2a2e',
                   'belt': '#9a7e2c', 'gold': '#9a7e2c', 'sandal': '#2a2018', 'wood': '#4a3222',
                   'book_cover': '#3a2418', 'parchment': '#d0c49e', 'cloth_wrap': '#3a2e26'},
        'helmet': 'hood', 'hair_asset': None, 'beard': False,
        'top': 'sleeved', 'wide_sleeves': True, 'neck': 'high',
        'chest_emblem': ('cross', 'gold', 0.2), 'hem_bands': [('sleeves', 'tint', 0.06)],   # gold cross, civ cuffs
        'bottom': 'kilt', 'robe_drop': 0.4,          # the floor-length robe
        'bands': [('neck', 'tint'), ('wrist', 'tint'), ('waist', 'gold')],   # shoulder band, cuffs, gold sash
        'bracers': False, 'sandals': True, 'wraps': None, 'gear': [],
        'weapon': 'club',
        'props': {'club': ('torch', {'length': 0.5})},
        # MISSING: book_raised (held at chest-to-head height; offhand sits low
        # at the hips) and tinted_book_cover (props.book's cover doesn't tint)
        'offhand': 'book',
        'stance_name': 'upright',               # the torch upright beside the head
        'tint': [],
        'grime': 0.55, 'upper_bulk': 1.05,
    },
    'Jaguar Warrior': {
        # Aztec: a jaguar-skin head with a civ-color feather spray, bare chest
        # with a gold pectoral and a civ-color collar band, civ-color belt,
        # loincloth and wrist cuffs, spotted leg wraps, gold arm, knee and
        # ankle bands, a macuahuitl low and a round shield with a civ-color
        # face and a gold boss
        'macro': {'muscle': 1.0, 'weight': 0.8, 'height': 0.72, 'proportions': 0.68},
        'race': {'caucasian': 0.0, 'african': 0.2, 'asian': 0.8},
        'colors': {'skin': '#a06a40', 'hair': '#0e0a08', 'pelt': '#a8622e', 'pelt_dark': '#2a1810',
                   'wraps': '#a8622e', 'wraps_band': '#2a1810', 'belt': '#3a2a1c', 'bottom': '#3a2a1c',
                   'gold': '#a8842e', 'feather': '#d8cfb8', 'obsidian': '#16141a',
                   'shield_face': '#5a4030', 'wood': '#3e2a1a'},
        'helmet': 'wolf',                       # MISSING: jaguar_head (spotted hide, the cat's face over the brow)
        'headgear': 'feather_headdress', 'headgear_scale': 0.7,   # the feather spray over the wolf helmet
        'plume': True,
        'top': None, 'chest_emblem': ('disc', 'gold', 0.2),   # the gold pectoral
        'bands': [('neck', 'tint'), ('wrist', 'tint'), ('upperarm', 'gold'), ('knee', 'gold'), ('ankle', 'gold')],
        'bottom': 'loincloth', 'loin_length': (0.36, 0.32), 'torn': False,
        'bracers': False, 'wraps': 'wraps', 'sandals': False, 'gear': [],
        'weapon': 'club',
        'props': {'club': ('macuahuitl', {'length': 0.8})},
        # a civ-color face with dark rosettes and a gold boss
        'offhand': 'shield_round', 'offhand_mesh': ('jaguar_shield', {'r': 0.3}),
        'stance_name': 'guard',
        'tint': ['belt', 'bottom', 'plume', 'shield_emblem'],
        'grime': 0.75, 'upper_bulk': 1.25,
    },
    'Javelin Thrower': {
        # a tinted close hood with yellow feather tufts and a dark red mask, a
        # tinted sleeveless top and loincloth, tinted arm, knee and ankle
        # bands; a tall bundle of javelins planted at his left, one javelin
        # low in the right hand
        'macro': {'muscle': 0.95, 'weight': 0.6, 'height': 0.74, 'proportions': 0.72},
        'race': {'caucasian': 0.1, 'african': 0.3, 'asian': 0.6},
        'colors': {'skin': '#9a6440', 'hair': '#0e0a08', 'belt': '#c09a34', 'wood': '#4e3420',
                   'steel': '#8a8a88', 'gold': '#c09a34', 'veil': '#7a2a20', 'feather': '#d0a830'},
        # the original's crown of upright red/yellow feathers over the hood
        'helmet': 'hood', 'headgear': 'feather_headdress',   # MISSING: feather_tufts (small, on the hood)
        'veil': True,                           # the dark red face mask
        'top': 'vest', 'bottom': 'loincloth', 'loin_length': (0.36, 0.32), 'torn': False,
        'bands': [('upperarm', 'tint'), ('knee', 'tint'), ('ankle', 'tint'), ('wrist', 'gold')],
        'bracers': False, 'wraps': None, 'sandals': False, 'gear': [],
        'weapon': 'spear',
        'props': {'spear': ('javelin', {'length': 1.6, 'grip': 0.7})},
        # civ-color shafts, a yellow tassel under each head; MISSING:
        # planted_bundle -- the offhand is held low and level across the hips,
        # not planted upright
        'offhand': 'javelin_bundle',
        'offhand_mesh': ('javelin_bundle', {'n': 4, 'length': 1.95, 'shaft_tint': True, 'tassels': True}),
        'stance_name': 'guard',                 # MISSING: javelin_level (held low and horizontal, point forward)
        'tint': ['helmet', 'top', 'bottom'],
        'grime': 0.7, 'upper_bulk': 1.15,
    },
    'Legionary': {
        # a steel galea with a civ-color crest, segmented plate, a civ-color
        # cape over the shoulders and back, dark red tunic skirt with a civ-
        # color hem, greaves and sandals; gladius low, a tall curved scutum
        'macro': {'muscle': 1.0, 'weight': 0.85, 'height': 0.72, 'proportions': 0.68},
        'race': {'caucasian': 0.8, 'african': 0.1, 'asian': 0.1},
        'colors': {'skin': '#9a7050', 'hair': '#241810', 'helmet': '#6a6c70', 'crest': '#3a2a1c',
                   'cuirass': '#6e7074', 'top': '#5a201c', 'bottom': '#5a201c', 'belt': '#2a1c12',
                   'greaves': '#4a4a4e', 'sandal': '#2e2016', 'steel': '#a8aaae', 'bronze': '#8a6a36',
                   'shield_face': '#5e5e62', 'shield_emblem': '#d8d4c8'},
        'helmet': 'galea', 'metal': ['helmet', 'cuirass'],
        'cuirass': True, 'top': 'tunic', 'cuirass_bands': 5,   # lorica segmentata
        'pauldrons': 'metal',                   # shoulder plates in the cuirass color
        'cape': True, 'cape_length': 0.55,      # shoulders to the waist
        'bottom': 'skirt', 'hem_bands': [('bottom', 'tint', 0.05)],
        'wraps': 'greaves', 'sandals': True, 'beard': False,
        'gear': [],
        'weapon': 'sword',
        'props': {'sword': ('sword', {'length': 0.52, 'width': 0.065, 'guard': 0.12})},
        # the original: a civ-color face with a pale (white) thunderbolt emblem
        'offhand': 'shield_oval', 'offhand_mesh': ('scutum', {'w': 0.62, 'h': 1.05, 'curve': 0.16, 'face_tint': True}),
        'shield_at': 'left_hip',
        'stance_name': 'guard',
        'tint': ['crest', 'cape'],
        'grime': 0.7, 'upper_bulk': 1.25,
    },
    'Libyan Mercenary': {
        # bronze Attic helmet with a crest, grey scale cuirass over a white
        # tunic with civ-color sleeves and hem, bronze greaves over leg wraps;
        # long spear upright, a big white round shield with a gold rim and a
        # civ-color sunburst
        'macro': {'muscle': 0.95, 'weight': 0.75, 'height': 0.76, 'proportions': 0.72},
        'race': {'caucasian': 0.4, 'african': 0.5, 'asian': 0.1},
        'colors': {'skin': '#8e6646', 'hair': '#140e0a', 'helmet': '#b8a068', 'crest': '#3a2a1c',
                   'cuirass': '#6e6e72', 'bottom': '#b8b2a2', 'belt': '#2a1c12', 'greaves': '#7e6838',
                   'wraps': '#4e3622', 'shield_face': '#b8b2a2', 'shield_rim': '#8a7444', 'bronze': '#8a6a36',
                   'wood': '#4a3622', 'steel': '#7e7a72'},
        # the open-faced galea with an untinted crest stands for the Attic
        # helmet; MISSING: attic_cheek_guards
        'helmet': 'galea', 'metal': ['helmet', 'cuirass'],
        'beard': True,
        # the cuirass covers the tunic's body, so tinting 'top' shows on the sleeves
        'cuirass': True, 'top': 'tunic',          # MISSING: cuirass_scale (a scale pattern on the cuirass)
        'bottom': 'skirt', 'hem_bands': [('bottom', 'tint', 0.04)],
        'wraps': 'greaves', 'gear': [],
        'weapon': 'spear',
        'props': {'spear': ('spear', {'length': 2.5, 'grip': 1.1})},
        'offhand': 'shield_oval',               # MISSING: sunburst_emblem (a star in place of the cross bands)
        'offhand_mesh': ('shield_oval', {'w': 0.8, 'h': 0.8, 'band': 0.1, 'dome': 0.06}),
        'stance_name': 'upright',
        'tint': ['top', 'shield_emblem'],
        'grime': 0.7, 'upper_bulk': 1.2,
    },
    'Longbowman': {
        # a pale metal cap with a curling civ-color plume, a civ-color
        # sleeveless tunic to mid-thigh over mail sleeves, grey trousers,
        # black boots, a big tan arrow bag at the right hip; the longbow
        # upright in front with an arrow nocked
        'macro': {'muscle': 1.0, 'weight': 0.75, 'height': 0.8, 'proportions': 0.72},
        'race': {'caucasian': 0.9, 'african': 0.05, 'asian': 0.05},
        'colors': {'skin': '#94704f', 'hair': '#5a4028', 'helmet': '#d0c8a8',
                   'bottom': '#8e887c', 'sleeves': '#8e887c', 'mail': '#9a9488',
                   'belt': '#1e1a16', 'boots': '#1e1a18', 'leather': '#6a5030', 'wood': '#4e3420',
                   'string': '#bab09a'},
        'helmet': 'cap', 'metal': ['helmet'], 'plume': True,   # MISSING: cap_size / plume_curl (curling at the back, not upright)
        'hair': 'short', 'beard': True,
        # the tinted body over ring mail on the arms and legs
        'top': 'sleeved', 'neck': 'high', 'collar': 'mail', 'mail_parts': ['arms', 'legs'],
        'bottom': 'skirt',                      # grey, untinted; MISSING: skirt_over_trousers (tinted tunic skirt over grey)
        'wraps': 'boots',
        'gear': [('chest_strap', {'color': '#4a3420'})],
        'weapon': None,
        'offhand': 'bow', 'offhand_mesh': ('bold_bow', {'height': 1.85, 'bend': 0.1, 'recurve': 0.0}),
        'back': 'quiver',                       # MISSING: arrow_bag (large tan bag low at the right hip)
        'stance_name': 'bow_low',               # MISSING: bow_upright_nocked (longbow vertical, arrow nocked)
        'tint': ['top', 'plume'],
        'grime': 0.75, 'upper_bulk': 1.25,
    },
    'Medieval Infantry': {
        # a closed great helm, full mail, a civ-color surcoat from the
        # shoulders to the ankles with a gold hem and a brown belt, a small
        # round brown wooden shield (per the original), a flail at the knee
        'macro': {'muscle': 1.0, 'weight': 0.9, 'height': 0.78, 'proportions': 0.68},
        'colors': {'skin': '#94704f', 'helmet': '#6a6c70', 'mail': '#5a5a5e', 'belt': '#3a2818',
                   'bracers': '#5a5a5e', 'greaves': '#6e7074', 'steel': '#8a8c90', 'iron': '#4a4a4e',
                   'wood': '#4a3222', 'shield_face': '#7a5434', 'shield_rim': '#3a3a3e', 'bronze': '#6e7074',
                   'boots': '#b4b4bc'},
        'helmet': 'great_helm', 'metal': ['helmet'], 'hair_asset': None, 'beard': False,
        # the surcoat: a tinted sleeveless top and a tinted kilt dropped to the ankles
        'top': 'vest', 'neck': 'high', 'collar': 'mail',   # MISSING: tabard_front_width, helm_rivets
        # the civ-color surcoat to mid-thigh with a gold hem, ring mail on the arms and legs
        'bottom': None, 'coat_length': 0.42, 'coat_flare': 1.15, 'hem_bands': [('coat', 'gold', 0.06)],
        'mail_parts': ['arms', 'legs'], 'leather_bracers': True, 'wraps': None, 'boots': True,
        'gear': [],
        'weapon': 'club',
        'props': {'club': ('flail', {'length': 0.55, 'chain': 0.32})},
        # the original: a small round brown wooden shield
        'offhand': 'shield_round', 'offhand_mesh': ('plain_round', {'r': 0.25}),
        'shield_at': 'left_hip',
        'stance_name': 'guard',
        'tint': ['top', 'bottom'],
        'grime': 0.7, 'upper_bulk': 1.3,
    },
    'Ninja': {
        # all in near-black olive: hood and mask, a long jacket with split
        # panels, baggy trousers, tabi; a civ-color harness of straps and
        # belt; a kama low in each hand
        'macro': {'muscle': 0.9, 'weight': 0.45, 'height': 0.7, 'proportions': 0.75},
        'race': {'caucasian': 0.0, 'african': 0.0, 'asian': 1.0},
        'colors': {'skin': '#987452', 'helmet': '#1c2016', 'top': '#343c28', 'bottom': '#262c1e',
                   'boots': '#121410', 'belt': '#2a2a20', 'steel': '#9a9ca0', 'wood': '#6a5034',
                   'leather': '#2a2418', 'veil': '#1a1e14', 'wraps': '#1c2016', 'wraps_band': '#343c28'},
        'helmet': 'hood', 'hair_asset': None, 'veil': True,   # hood and dark face mask
        'top': 'sleeved', 'neck': 'high',       # MISSING: split side panels on the jacket
        'coat_length': 0.4, 'coat_flare': 1.2,  # the jacket to mid-thigh
        'bottom': 'long', 'wraps': 'wraps',     # dark leg wraps
        'bands': [('waist', 'tint', 0.06)],     # the civ-color belt
        'harness': True,                        # two civ-color straps crossing on chest and back
        'gear': [('chest_strap', {'tint': True, 'width': 0.06})],
        'weapon': 'sword',
        'props': {'sword': ('kama', {'length': 0.42})},
        'offhand': 'kama',                      # held low across the hips, not out at the side
        'stance_name': 'hang',
        'tint': ['belt', 'harness'],
        'grime': 0.6, 'upper_bulk': 1.1,
    },
    'Pikeman': {
        # steel morion, breastplate and tassets, civ-color puffy sleeves and
        # dark civ-color breeches, cream stockings, brown shoes, light beard;
        # partisan upright, butt by the right foot
        'macro': {'muscle': 0.95, 'weight': 0.8, 'height': 0.76, 'proportions': 0.72},
        'race': {'caucasian': 0.9, 'african': 0.05, 'asian': 0.05},
        'colors': {'skin': '#94704f', 'hair': '#6a5034', 'helmet': '#6e7074', 'cuirass': '#74767a',
                   'belt': '#2e2016', 'wraps': '#a89c80', 'wraps_band': '#7a7058', 'sandal': '#3a2818',
                   'wood': '#4a3222', 'steel': '#8a8c90'},
        'helmet': 'morion', 'metal': ['helmet', 'cuirass'],
        'beard': True,
        'cuirass': True, 'cuirass_width': 0.22, 'top': 'sleeved', 'puffy': True,
        'chest_emblem': ('cross', 'tint', 0.22),            # the tinted cross on the breastplate
        'coat_length': 0.3, 'coat_metal': True,             # steel tassets over the hips
        'tint_shade': {'bottom': 0.45},                     # dark civ-color breeches
        # tinted puffed breeches to below the knee, cream stocking wraps over the calves
        'bottom': 'breeches', 'wraps': 'wraps', 'sandals': True,
        'gear': [],
        'weapon': 'spear',
        'props': {'spear': ('partisan', {'length': 2.4, 'grip': 1.0})},   # about 1.3x his height
        'stance_name': 'upright',
        'tint': ['top', 'bottom'],
        'grime': 0.65, 'upper_bulk': 1.2,
    },
    'Samurai': {
        # planted wide, arms folded; shaven pate and a black topknot; a knee-
        # length civ-color coat (darker pattern), very wide grey-lavender
        # sleeves, a white under-robe at the chest, wide dark hakama, white
        # tabi and sandals; katana sheathed at the left hip
        'macro': {'muscle': 0.95, 'weight': 0.75, 'height': 0.72, 'proportions': 0.7}, 'age': 0.55,
        'race': {'caucasian': 0.0, 'african': 0.0, 'asian': 1.0},
        'colors': {'skin': '#c8986c', 'hair': '#0e0c0c', 'bottom': '#2e2c40', 'sleeves': '#3e3c54', 'belt': '#1a1824',
                   'sandal': '#2a2018', 'lacquer': '#1a1412', 'iron': '#3a3a3c', 'cloth_wrap': '#2a2630',
                   'wraps': '#e8e4da', 'wraps_band': '#e8e4da'},
        'helmet': None, 'hair': 'topknot', 'hair_asset': None, 'topknot': 'topknot', 'beard': False,
        # the civ-color coat with wide untinted grey-lavender sleeves over it
        'top': 'sleeved', 'wide_sleeves': True,   # MISSING: white under-robe; topknot/bald
        'coat_length': 0.6, 'coat_flare': 1.3,  # the knee-length coat, a darker spot pattern on the civ color
        'patterns': {'top': ('camo', '#7a7a7a', 16.0)},
        'bottom': 'long', 'puffy': True,        # MISSING: hakama (puffed trousers stand in for the pleats)
        'sandals': True, 'wraps': 'wraps', 'wraps_height': 0.16, 'gear': [],   # ankle-high white tabi
        # arms folded on the chest, hands empty, the katana sheathed at the left hip
        'weapon': None, 'offhand': None,
        'hip': ('katana_sheathed', {'length': 0.72}),
        'stance_name': 'arms_folded',
        'tint': ['top'],
        'grime': 0.55, 'upper_bulk': 1.2,
    },
    'Scout': {
        # lean and wiry, slightly hunched mid-stride; bald with a long dark
        # ponytail and beard, bare chest with a brown strap and a small pack,
        # civ-color upper-arm bands and loincloth, baggy tan leggings, boots,
        # a gourd in the right hand
        'macro': {'muscle': 0.9, 'weight': 0.45, 'height': 0.7, 'proportions': 0.75},
        'colors': {'skin': '#8e6646', 'hair': '#241810', 'belt': '#3a2818', 'boots': '#3e2a1a',
                   'wraps': '#a8906a', 'wraps_band': '#a8906a', 'leather': '#4a3220', 'gourd': '#9a7a48'},
        'helmet': None, 'hair': 'topknot', 'hair_asset': None, 'topknot': 'ponytail', 'beard': True,   # MISSING: topknot/bald
        'top': None, 'bottom': 'loincloth', 'loin_length': (0.34, 0.3), 'torn': True,
        'bands': [('upperarm', 'tint')],
        'wraps': 'wraps', 'boots': True,        # MISSING: baggy_leggings (tan, under the loincloth)
        'gear': [('chest_strap', {'color': '#4a3220', 'width': 0.05})],   # MISSING: chest_pack
        # the gourd hangs from the right fist ('upright' keeps its +Z, the
        # cord's top, up, so it hangs below the hand)
        'weapon': 'gourd',
        'props': {'gourd': ('gourd', {})},
        'offhand': None, 'back': None,
        'stance_name': 'upright',               # MISSING: walking_hunch (relaxed, slightly hunched mid-stride)
        'tint': ['bottom', 'strap'],
        'grime': 0.85, 'upper_bulk': 1.1,
    },
    'Swiss Mercenary': {
        # steel morion with a civ-color plume, a civ-color doublet (the
        # original shows no breastplate) with a dark strap from shoulder to
        # hip, puffy slashed pale sleeves, baggy red breeches slashed black,
        # black shoes, brown beard; a long pike upright with a civ-color shaft
        'macro': {'muscle': 1.0, 'weight': 0.85, 'height': 0.78, 'proportions': 0.7},
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#9a7254', 'hair': '#4a3424', 'helmet': '#7e8086', 'cuirass': '#84868c',
                   'top': '#b8b2a4', 'sleeves': '#d8d4cc', 'bottom': '#7a2a20', 'boots': '#161210', 'belt': '#1e1612',
                   'wood': '#4a3222', 'steel': '#8a8c90'},
        'helmet': 'morion', 'plume': True, 'metal': ['helmet'],
        'beard': True,
        # the original's torso is a civ-color doublet (no breastplate shows), puffed pale sleeves
        'cuirass': False, 'top': 'sleeved', 'puffy': True, 'wide_sleeves': True,   # white sleeves to the wrist, slashing_stripes
        'bottom': 'breeches',                   # MISSING: slashing_stripes (black on the breeches)
        'wraps': 'boots',
        'gear': [('chest_strap', {'tint': True, 'width': 0.15})],   # the civ-color diagonal sash over the doublet
        'weapon': 'spear',
        # the pike: its shaft in civ color, steel head, langets and butt plain
        'props': {'spear': ('pike', {'length': 3.0, 'grip': 1.1, 'shaft_tint': True})},
        'stance_name': 'upright',
        'tint': ['plume', 'top'],
        'grime': 0.65, 'upper_bulk': 1.25,
    },
}

# MISSING FEATURES
# What the notes still need that the supported keys can't express. Each entry
# above approximates it and marks the spot with "MISSING:".
#
# -- civ-color (tint) gaps
# short_sleeves      Libyan Mercenary, Bowman (and Immortals, done with 'puffy' + 'sleeves' tint): a tintable
#                    short-sleeve layer on a 'tunic' top, without puffing the bottom (they rely on the cuirass
#                    hiding the tunic body today).
# one_bracer         Gallic Swordsman: a bracer (or band) on the sword (right) forearm only, e.g. ('wrist_r', part).
# armhole_trim       Hoplite: tinted edging round the cuirass's armholes (neck and waist are done with bands).
# tinted_fur_tufts   Impi: 'wraps' fur (G.fur) never tints; the cow-tail tufts must. Make wraps honor tint.
# trouser_stripes    Immortals: tinted vertical stripes on the trousers.
# tinted_book_cover  Inquisitor: props.book's cover with tint=True (a book(tint=True) param).
#
# -- garments
# long_sleeves       Libyan Mercenary, Bowman: a tunic's sleeves to mid-bicep (tintable, without 'puffy').
# under_robe / split_panels  Samurai: a white under-robe V at the chest; Ninja: split side panels on the
#                    mid-thigh jacket (coat_length stands in).
# puffy_sleeves_to_wrist  Swiss Mercenary: puffed sleeves down the whole arm ('puffy' puffs the upper arm only).
# slashing_stripes   Swiss Mercenary: slashed sleeves and black-striped slashes on the breeches.
# skirt_over_trousers  Longbowman: a mid-thigh tunic skirt over grey trousers (two bottom layers).
# hakama             Samurai: wide pleated trousers (puffed 'long' trousers stand in).
# baggy_leggings     Scout (tan, under a loincloth), Berserk (baggy trousers).
# tabard_front_width Medieval Infantry: the surcoat's front panel narrower than the body, mail showing at the sides.
# fur_garment        Enkidu Warrior: vest and kilt made of shaggy fur (G.fur), not cloth.
# long_pelt_cloak    Berserk (to the thighs), Enkidu Warrior (to the calves): pelt_cape with a length.
# cuirass_scale      Libyan Mercenary: a scale pattern on the cuirass (plate_bands has only horizontal bands).
#
# -- headgear and faces
# cap_size / plume_curl  Longbowman: a bigger cap with the plume curling at the back (the 'plume' stands upright).
# helm_rivets        Medieval Infantry: rivet rows on the great helm.
# attic_cheek_guards Libyan Mercenary: hinged cheek guards on the Attic helmet (the galea stands in).
# jaguar_head        Jaguar Warrior: a spotted jaguar-hide hood with the cat's face over the brow (the wolf helmet
#                    stands in, a scaled feather_headdress for the spray).
# feather_tufts      Javelin Thrower: small yellow feather tufts on the hood (feather_headdress stands in).
# topknot / bald     Samurai (shaven pate, black topknot), Scout (bald with a long dark ponytail): a bald scalp
#                    with a topknot/ponytail (hair_asset ponytail01 covers the whole scalp).
# streamers          Enkidu Warrior: tinted ribbons flicking up off the headband.
# braids / long_beard  Berserk (two yellow braids down the chest), Enkidu Warrior (black beard to mid-chest).
# spiked_hair        Gallic Swordsman: limed ginger hair spiked straight up.
#
# -- props and where they go
# shackles placement Enslaved Worker: props.shackles on both wrists (and a chain between the ankles).
# book_raised        Inquisitor: the book held open at chest-to-head height (offhand sits low at the hips).
# planted_bundle     Javelin Thrower: the bundle planted upright at his left, gripped high (offhand is level).
# sunburst_emblem    Libyan Mercenary: a star emblem (tinted) in place of the cross bands.
# arrow_bag          Longbowman: a large tan arrow bag low at the right hip (not a back quiver).
# chest_pack         Scout: a small pack on the chest strap.
# iklwa              Impi: a short spear with a very broad, long blade (a spear with head=1.7 stands in).
#
# -- stances
# two_hand_low       Berserk: haft level across the hips in both hands, the head out to the right at knee height.
# spade_upright      Enslaved Worker: a spade upright at the right side, blade on the ground (hang stands in).
# javelin_level      Javelin Thrower: a javelin held low and horizontal, point forward.
# bow_upright_nocked Longbowman: the longbow vertical in front, an arrow nocked, the right hand at the string.
# walking_hunch      Scout: relaxed, slightly hunched mid-stride with an item hanging from the fist ('hang'
#                    turns a hanging item upside down; 'upright' stands in).
# dual_low           Ninja: both hands low at the sides, blades out (the offhand kama sits across the hips).
