"""Remaster entries for 22 more Civ3 foot units (plain data, same format as
remaster.py's REMASTER: each entry is merged over the unit's catalog entry,
dicts one level deep).

The notes (notes/<Unit>.md) are the truth where they differ from the catalog;
each 'tint' list is the note's "Tinted (civ color)" line, as far as the
supported part keys can say it. Direction: Civ3 remastered, not AI-looking --
battle-hardened, weathered tan-brown skin, grime, dark realistic materials,
heavier builds for warriors and lean wiry ones for scouts and workers.

Every weapon key maps to a builder that exists in props.py (or clubs.war_club),
and every offhand is a props.py builder or None: the catalog's own prop names
(battle_axe, star_mace, scimitar, scutum, kama, torch, book, gourd, ...) are not
built yet, so the entries override them. What is approximated is marked
"MISSING:" and listed under MISSING FEATURES at the bottom.
"""

REMASTER = {
    'Berserk': {
        # brawny, wide crouch, long axe two-handed low across the hips; yellow
        # braids and beard under a conical iron cap; a shaggy fur cloak down
        # the whole back; civ-color tunic, sleeves, wristbands and trousers
        'macro': {'muscle': 1.0, 'weight': 0.95, 'height': 0.78, 'proportions': 0.6},
        'race': {'caucasian': 0.9, 'african': 0.0, 'asian': 0.1},
        'colors': {'skin': '#a87a5a', 'hair': '#b89a3a', 'helmet': '#5e5e62', 'belt': '#2e2016',
                   'wraps': '#4e3622', 'wraps_band': '#241810', 'bracers': '#4e3a26', 'steel': '#a8aaae',
                   'wood': '#5a4028', 'leather': '#3e2a1a'},
        'helmet': 'cap', 'metal': ['helmet'],   # MISSING: conical_helmet (a pointed nasal cap)
        'hair': 'long', 'hair_asset': 'braid01', 'mane': 0.22, 'beard': True,   # MISSING: braids (two braids down the chest)
        'top': 'sleeved', 'bottom': 'long', 'wraps': 'wraps', 'bracers': True,
        'pelt_cape': ('#6a4a30', '#3a2818'),    # MISSING: long_pelt_cloak (to the thighs, not the shoulders)
        'gear': [],
        'weapon': 'axe',
        # MISSING: battle_axe -- a stone axe stretched long stands in
        'props': {'axe': ('stone_axe', {'length': 0.95, 'scale': 1.45})},
        'stance_name': 'low_across',            # MISSING: two_hand_low (haft level across the hips, head out right)
        'tint': ['top', 'bottom', 'bracers'],
        'grime': 0.85, 'upper_bulk': 1.3,
    },
    'Bowman': {
        # bow low in the left hand, a loose arrow in the right; a civ-color
        # cap with a gold knob, leather vest over a civ-color knee tunic,
        # quiver on the back, leg wraps and sandals
        'macro': {'muscle': 0.95, 'weight': 0.7, 'height': 0.75, 'proportions': 0.72},
        'colors': {'skin': '#8e6a4a', 'hair': '#1a120c', 'cuirass': '#4a3420', 'leather': '#3e2a1a',
                   'belt': '#2a1c12', 'wraps': '#7a6448', 'wraps_band': '#3a2a1a', 'sandal': '#2e2016',
                   'wood': '#5a3e24', 'string': '#8a7a5a'},
        'helmet': 'cap', 'metal': [],           # MISSING: cap_knob (gold knob on the cap's crown)
        'hair': 'short', 'beard': True,
        'cuirass': True, 'top': 'tunic', 'bottom': 'skirt',   # the cuirass leaves the tunic's sleeves showing
        'wraps': 'wraps', 'sandals': True,
        'gear': [('chest_strap', {'color': '#2e2016'})],
        'weapon': None,                         # MISSING: loose_arrow (in the right hand, point down-forward)
        'offhand': 'bow', 'offhand_mesh': ('bold_bow', {'height': 1.35, 'bend': 0.18, 'recurve': 0.1}),
        'back': 'quiver', 'stance_name': 'bow_low',
        'tint': ['helmet', 'top', 'bottom'],
        'grime': 0.75, 'upper_bulk': 1.15,
    },
    'Chasquis Scout': {
        # an Inca runner: a broad civ-color fan headdress framing the face,
        # long black hair, bare chest, civ-color wrap skirt with a gold belt,
        # civ-color bands at wrist, knee and ankle; a star mace held low
        'macro': {'muscle': 0.9, 'weight': 0.5, 'height': 0.68, 'proportions': 0.72},
        'race': {'caucasian': 0.1, 'african': 0.2, 'asian': 0.7},
        'colors': {'skin': '#8a5a3a', 'hair': '#120c08', 'belt': '#b08a2a', 'bottom': '#5a4030',
                   'wood': '#4e3420', 'stone': '#4a4846', 'bracers': '#5a4030', 'helmet': '#5a4030'},
        'helmet': 'headband',                   # MISSING: fan_headdress (a broad rounded fan round the head)
        'hair': 'long', 'hair_asset': 'long01', 'beard': False,
        'top': None, 'bottom': 'loincloth', 'loin_length': (0.38, 0.34), 'torn': False,
        'bracers': True,                        # MISSING: leg_bands (civ-color bands below the knee and at the ankle)
        'wraps': None, 'sandals': False, 'gear': [],
        'weapon': 'club',
        # MISSING: star_mace -- a small studded club stands in
        'props': {'club': ('war_club', {'length': 0.5, 'knob': 0.06, 'studs': 8, 'scale': 1.1})},
        'stance_name': 'hang',
        'tint': ['helmet', 'bottom', 'bracers'],
        'grime': 0.7, 'upper_bulk': 1.1,
    },
    'Enkidu Warrior': {
        # long spear upright, butt on the ground; wild black hair and a chest-
        # length beard, a civ-color headband with streamers; light tan fur
        # over the torso and down the back to the calves, a fur kilt; civ-
        # color arm, wrist, waist and knee bands
        'macro': {'muscle': 1.0, 'weight': 0.9, 'height': 0.75, 'proportions': 0.62},
        'race': {'caucasian': 0.6, 'african': 0.2, 'asian': 0.2},
        'colors': {'skin': '#9a6e4c', 'hair': '#140e0a', 'top': '#9a8462', 'bottom': '#8a7656',
                   'wraps': '#b8a888', 'wraps_band': '#7a6a50', 'helmet': '#3a2a1c', 'bracers': '#3a2a1c',
                   'belt': '#3a2a1c', 'wood': '#5a3420', 'steel': '#7e7a72'},
        'helmet': 'headband',                   # MISSING: streamers (ribbons flicking up off the headband)
        'hair': 'long', 'hair_asset': 'long01', 'mane': 0.3,
        'beard': True,                          # MISSING: long_beard (black, to mid-chest)
        'top': 'vest', 'bottom': 'skirt',       # MISSING: fur_garment (a vest and kilt of shaggy fur, not cloth)
        'pelt_cape': ('#b09a70', '#7a6848'),    # MISSING: long_pelt_cloak (down to the calves)
        'bracers': True,                        # MISSING: arm_bands (upper arm) and leg_bands (knee)
        'wraps': 'wraps', 'sandals': False, 'back': None, 'gear': [],
        'weapon': 'spear',
        'props': {'spear': ('spear', {'length': 2.5, 'grip': 1.1, 'head': 1.2})},
        'stance_name': 'upright',
        'tint': ['helmet', 'bracers', 'belt'],
        'grime': 0.85, 'upper_bulk': 1.25,
    },
    'Enslaved Worker': {
        # a gaunt labourer: long hair and beard, a sleeveless olive tunic,
        # one civ-color strap across the chest, iron shackles, a spade
        'macro': {'muscle': 0.8, 'weight': 0.4, 'height': 0.7, 'proportions': 0.72}, 'age': 0.6,
        'colors': {'skin': '#8e6a4a', 'hair': '#3a2616', 'bottom': '#7e8264', 'belt': '#2e2418',
                   'sandal': '#2e2016', 'wood': '#4e3622', 'steel': '#5a5a5a'},
        'hair': 'long', 'hair_asset': 'long01', 'beard': True,
        # the strap is the only civ color, and gear straps can't be tinted, so
        # the tinted sash (top 'strap') carries it over a bare chest; the
        # olive kilt keeps the tunic's knee-length hem
        'top': 'strap', 'bottom': 'skirt',      # MISSING: tintable_strap over a sleeveless tunic
        'sandals': True, 'wraps': None, 'gear': [],   # MISSING: shackles (wrists and ankles, chain between the ankles)
        'weapon': 'shovel',
        'props': {'shovel': ('shovel', {'length': 1.45})},
        'stance_name': 'low_across',            # MISSING: spade_upright (blade on the ground at his right side)
        'tint': ['top'],
        'grime': 0.9, 'upper_bulk': 1.05,
    },
    'Explorer': {
        # a conquistador-era explorer: steel comb morion with a civ-color
        # plume, steel breastplate over puffy civ-color sleeves, baggy slate
        # breeches, pale high boots; a map in hand, rapier sheathed at the hip
        'macro': {'muscle': 0.85, 'weight': 0.6, 'height': 0.75, 'proportions': 0.75}, 'age': 0.6,
        'race': {'caucasian': 0.8, 'african': 0.1, 'asian': 0.1},
        'colors': {'skin': '#9a7254', 'hair': '#2e2016', 'helmet': '#8e9096', 'cuirass': '#8a8c92',
                   'bottom': '#3e3c4e', 'boots': '#8a8680', 'belt': '#2a1e14', 'steel': '#a8aaae'},
        'helmet': 'cap', 'metal': ['helmet', 'cuirass'],   # MISSING: morion (comb and brim) and plume (tinted)
        'hair': 'short', 'beard': True,
        'cuirass': True, 'top': 'sleeved',      # MISSING: puffy_sleeves
        'bottom': 'long', 'wraps': 'boots',     # MISSING: baggy_breeches (to the knee) over stockings
        'gear': [],
        'weapon': None,                         # MISSING: sheathed_sword (rapier at the left hip) and map_in_hand
        'stance_name': 'upright',
        'tint': ['top'],
        'grime': 0.6, 'upper_bulk': 1.1,
    },
    'Gallic Swordsman': {
        # shirtless and brawny, ginger hair spiked up, check-patterned civ-
        # color trousers, a civ-color band on the sword forearm, a long sword
        # low and a small round shield (brown face, silver boss and rim)
        'macro': {'muscle': 1.0, 'weight': 0.85, 'height': 0.75, 'proportions': 0.65},
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#b08462', 'hair': '#a85a24', 'bottom': '#3a3a40', 'belt': '#2e2016',
                   'boots': '#3e2a1a', 'bracers': '#3a2a1c', 'steel': '#a8aaae', 'bronze': '#9a9ca2',
                   'shield_face': '#5e4430', 'shield_rim': '#8a8c90', 'leather': '#3e2a1a'},
        'helmet': None,
        'hair': 'short', 'hair_asset': 'short04', 'beard': False,   # MISSING: spiked_hair (limed, straight up)
        'top': None, 'bottom': 'long',          # MISSING: check_pattern on the trousers
        'boots': True, 'wraps': 'boots',
        'bracers': True,                        # MISSING: one_bracer (the sword arm only)
        'gear': [],
        'weapon': 'sword',
        'props': {'sword': ('long_sword', {'length': 0.85, 'width': 0.055, 'guard': 0.16})},
        # the original's shield has no civ color: shield_round tints its rim and
        # boss, so a small shield_oval with a hairline emblem stands in
        'offhand': 'shield_oval',               # MISSING: plain_shield (no tinted parts)
        'offhand_mesh': ('shield_oval', {'w': 0.52, 'h': 0.52, 'band': 0.012, 'dome': 0.05}),
        'stance_name': 'guard',
        'tint': ['bottom', 'bracers'],
        'grime': 0.8, 'upper_bulk': 1.3,
    },
    'Hoplite': {
        # bronze Corinthian helm with a civ-color crest, bronze cuirass with
        # civ-color trim, leather pteruges, bronze greaves; a long spear
        # upright and the big round aspis across the front, rim in civ color
        'macro': {'muscle': 1.0, 'weight': 0.85, 'height': 0.75, 'proportions': 0.7},
        'colors': {'skin': '#94704f', 'hair': '#2a1c12', 'helmet': '#7a5e30', 'crest': '#3a2a1c',
                   'cuirass': '#7e6234', 'greaves': '#7a5e30', 'bottom': '#4a3420', 'belt': '#2e2016',
                   'bracers': '#3a2a1c', 'shield_face': '#8a7448', 'bronze': '#8a6a36', 'wood': '#3e2a1a'},
        'helmet': 'crested', 'metal': ['helmet', 'cuirass'],
        'beard': True,
        'cuirass': True, 'top': None, 'neck': 'high',   # MISSING: cuirass_trim (tinted shoulder, armhole and waist edges)
        'bottom': 'loincloth', 'bottom_material': 'leather', 'loin_length': (0.46, 0.42), 'torn': False,
        'wraps': 'greaves', 'bracers': True,
        'gear': [],
        'weapon': 'spear',
        'props': {'spear': ('spear', {'length': 2.55, 'grip': 1.15, 'head': 1.1})},
        # the aspis: a big round board; shield_round's rim is the tinted part
        'offhand': 'shield_round',              # MISSING: shield_emblem_paint (a dark painted device on the face)
        'offhand_mesh': ('shield_round', {'r': 0.44, 'dome': 0.07, 'boss': False}),
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
                   'bottom': '#5e4a34', 'belt': '#3a2a1c', 'boots': '#a08434', 'steel': '#a8aaae'},
        'helmet': 'hood',                       # MISSING: face_veil (cloth over the chin and mouth)
        'beard': True,
        'top': 'tunic', 'cuirass': False,       # MISSING: tinted_sleeves (only the short sleeves in civ color)
        'bottom': 'long',                       # MISSING: trouser_stripes (tinted vertical stripes)
        'wraps': 'boots',
        'gear': [('chest_strap', {'color': '#3a2818'})],
        'weapon': 'sword',
        # MISSING: scimitar -- the curved sabre stands in
        'props': {'sword': ('sabre', {'length': 0.78, 'width': 0.045, 'curve': 0.1})},
        'stance_name': 'hang',
        'tint': ['belt'],
        'grime': 0.65, 'upper_bulk': 1.2,
    },
    'Impi': {
        # Zulu: crouched and bow-legged, a short broad stabbing spear low, a
        # tall cowhide shield upright; civ-color feather headdress, arm and
        # wrist bands, and cow-tail tufts on the calves
        'macro': {'muscle': 1.0, 'weight': 0.7, 'height': 0.78, 'proportions': 0.7},
        'race': {'caucasian': 0.0, 'african': 1.0, 'asian': 0.0},
        'colors': {'skin': '#4e3220', 'hair': '#0e0a08', 'helmet': '#2a1e14', 'bottom': '#4a3424',
                   'belt': '#2e2016', 'bracers': '#2a1e14', 'wraps': '#d8d0c0', 'wraps_band': '#2a2420',
                   'shield_face': '#d8d0c0', 'shield_rim': '#2a2018', 'wood': '#3e2a1a', 'steel': '#a8aaae'},
        'helmet': 'headband',                   # MISSING: feather_headdress (a bushy crown of feathers)
        'hair': 'short', 'hair_asset': 'afro01', 'beard': False,
        'top': None, 'bottom': 'loincloth', 'bottom_material': 'leather', 'loin_length': (0.4, 0.38), 'torn': True,
        'bracers': True,
        'wraps': 'wraps',                       # MISSING: tinted_fur_tufts (cow-tail tufts, civ color; fur wraps don't tint)
        'sandals': False,
        'gear': [('chest_strap', {'color': '#3a2818'})],
        'weapon': 'spear',
        # the iklwa: short with a broad blade
        'props': {'spear': ('spear', {'length': 1.25, 'grip': 0.45, 'head': 1.7, 'butt': False})},
        'offhand': 'shield_oval',               # MISSING: cowhide_shield (black-and-white hide, a stick behind, no civ color)
        'offhand_mesh': ('shield_oval', {'w': 0.56, 'h': 1.15, 'band': 0.012, 'dome': 0.04}),
        'stance_name': 'hang',
        'tint': ['helmet', 'bracers', 'wraps'],
        'grime': 0.75, 'upper_bulk': 1.25,
    },
    'Inquisitor': {
        # solemn: a floor-length charcoal hooded robe, gold cross and sash,
        # civ-color book cover, shoulder band and sleeve cuffs; book raised
        # in the left hand, a torch low in the right
        'macro': {'muscle': 0.6, 'weight': 0.55, 'height': 0.72, 'proportions': 0.75}, 'age': 0.75,
        'colors': {'skin': '#9a7656', 'top': '#2e2e32', 'bottom': '#2e2e32', 'helmet': '#2a2a2e',
                   'belt': '#9a7e2c', 'sandal': '#2a2018', 'bracers': '#2e2e32', 'wood': '#4a3222'},
        'helmet': 'hood', 'hair_asset': None, 'beard': False,
        'top': 'sleeved', 'neck': 'high',       # MISSING: wide_sleeves; shoulder_band (tinted); chest_cross (gold)
        'bottom': 'kilt',                       # MISSING: long_robe (to the floor)
        'bracers': True,                        # the tinted cuffs
        'sandals': True, 'wraps': None, 'gear': [],
        'weapon': 'club',
        # MISSING: torch -- a short staff stands in
        'props': {'club': ('staff', {'length': 0.6, 'grip': 0.15})},
        'offhand': None,                        # MISSING: book_raised (open book at chest-to-head height, tinted cover)
        'stance_name': 'guard',
        'tint': ['bracers'],
        'grime': 0.55, 'upper_bulk': 1.05,
    },
    'Jaguar Warrior': {
        # Aztec: a jaguar-skin head with a civ-color feather spray, bare chest
        # with a gold pectoral and a civ-color collar band, civ-color belt,
        # loincloth and wrist cuffs, spotted leg wraps, a macuahuitl low and a
        # round shield with a civ-color face and a gold boss
        'macro': {'muscle': 1.0, 'weight': 0.8, 'height': 0.72, 'proportions': 0.68},
        'race': {'caucasian': 0.0, 'african': 0.2, 'asian': 0.8},
        'colors': {'skin': '#7a4a2e', 'hair': '#0e0a08', 'pelt': '#a8622e', 'pelt_dark': '#2a1810',
                   'wraps': '#a8622e', 'wraps_band': '#2a1810', 'belt': '#3a2a1c', 'bottom': '#3a2a1c',
                   'bracers': '#3a2a1c', 'bronze': '#a8842e', 'shield_face': '#5a4030', 'shield_rim': '#2a2018',
                   'wood': '#3e2a1a', 'stone': '#2a2a2e'},
        'helmet': 'wolf',                       # MISSING: jaguar_head (spotted hide, the cat's face over the brow) and feather_plume (tinted)
        'top': None,                            # MISSING: pectoral (gold) and collar_band (tinted)
        'bottom': 'loincloth', 'loin_length': (0.36, 0.32), 'torn': False,
        'bracers': True, 'wraps': 'wraps', 'sandals': False, 'gear': [],
        'weapon': 'club',
        # MISSING: macuahuitl (a flat wooden club edged with obsidian blades)
        'props': {'club': ('war_club', {'length': 0.6, 'knob': 0.055, 'studs': 10, 'scale': 1.2})},
        # the face in civ color: a round shield_oval whose emblem bands are so
        # wide they cover most of it, with a gold boss
        'offhand': 'shield_oval',               # MISSING: tinted_shield_face
        'offhand_mesh': ('shield_oval', {'w': 0.52, 'h': 0.52, 'band': 0.28, 'dome': 0.05}),
        'stance_name': 'guard',
        'tint': ['belt', 'bottom', 'bracers', 'shield_emblem'],
        'grime': 0.75, 'upper_bulk': 1.25,
    },
    'Javelin Thrower': {
        # a tinted close hood with yellow feather tufts and a dark red mask, a
        # tinted sleeveless top and loincloth, tinted arm, knee and ankle
        # bands; a tall bundle of javelins planted at his left, one javelin
        # low in the right hand
        'macro': {'muscle': 0.95, 'weight': 0.6, 'height': 0.74, 'proportions': 0.72},
        'race': {'caucasian': 0.1, 'african': 0.3, 'asian': 0.6},
        'colors': {'skin': '#7e5236', 'hair': '#0e0a08', 'belt': '#3a2a1c', 'wood': '#4e3420',
                   'steel': '#8a8a88'},
        'helmet': 'hood',                       # MISSING: feather_tufts (yellow) and face_mask (dark red)
        'top': 'vest', 'bottom': 'loincloth', 'loin_length': (0.36, 0.32), 'torn': False,
        'bracers': False,                       # MISSING: arm_bands and leg_bands (tinted upper arm, knee, ankle)
        'wraps': None, 'sandals': False, 'gear': [],
        'weapon': 'spear',
        'props': {'spear': ('javelin', {'length': 1.6, 'grip': 0.7})},
        'offhand': None,                        # MISSING: javelin_bundle (3-4 planted upright, taller than him, tinted shaft wraps)
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
                   'shield_face': '#5e5e62', 'shield_rim': '#3a3a3e'},
        'helmet': 'crested', 'metal': ['helmet', 'cuirass'],   # MISSING: galea (open face, cheek guards, short crest)
        'cuirass': True, 'top': 'tunic', 'pauldrons': True,     # MISSING: segmentata (banded plate) and cape (tinted, shoulders to waist)
        'bottom': 'skirt',                      # MISSING: hem_band (tinted band at the skirt's hem)
        'wraps': 'greaves', 'sandals': True, 'beard': False,
        'gear': [],
        'weapon': 'sword',
        'props': {'sword': ('sword', {'length': 0.52, 'width': 0.065, 'guard': 0.12})},
        # MISSING: scutum -- a tall oval shield_oval stands in; its tinted
        # cross bands stand for the thunderbolt emblem on a grey field
        'offhand': 'shield_oval',
        'offhand_mesh': ('shield_oval', {'w': 0.62, 'h': 1.05, 'band': 0.08, 'dome': 0.08}),
        'stance_name': 'guard',
        'tint': ['crest', 'shield_emblem'],
        'grime': 0.7, 'upper_bulk': 1.25,
    },
    'Libyan Mercenary': {
        # bronze Attic helmet with a crest, grey scale cuirass over a white
        # tunic with civ-color sleeves and hem, bronze greaves over leg wraps;
        # long spear upright, a big white round shield with a gold rim and a
        # civ-color sunburst
        'macro': {'muscle': 0.95, 'weight': 0.75, 'height': 0.76, 'proportions': 0.72},
        'race': {'caucasian': 0.4, 'african': 0.5, 'asian': 0.1},
        'colors': {'skin': '#7e5a3e', 'hair': '#140e0a', 'helmet': '#7e6838', 'crest': '#3a2a1c',
                   'cuirass': '#6e6e72', 'bottom': '#b8b2a2', 'belt': '#2a1c12', 'greaves': '#7e6838',
                   'wraps': '#4e3622', 'shield_face': '#b8b2a2', 'shield_rim': '#8a7444', 'bronze': '#8a6a36',
                   'wood': '#4a3622', 'steel': '#7e7a72'},
        'helmet': 'crested', 'metal': ['helmet', 'cuirass'],   # MISSING: attic_helmet (open face)
        'beard': True,
        # the cuirass covers the tunic's body, so tinting 'top' shows on the sleeves
        'cuirass': True, 'top': 'tunic',
        'bottom': 'skirt',                      # MISSING: hem_band (tinted)
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
        'colors': {'skin': '#94704f', 'hair': '#5a4028', 'helmet': '#8a8470', 'mail': '#5e5c58',
                   'belt': '#1e1a16', 'boots': '#1e1a18', 'leather': '#6a5030', 'wood': '#4e3420',
                   'string': '#bab09a'},
        'helmet': 'cap', 'metal': ['helmet'],   # MISSING: plume (curling, tinted, at the back of the cap)
        'hair': 'short', 'beard': True,
        'top': 'tunic', 'neck': 'high', 'collar': 'mail',   # MISSING: mail_sleeves (full arm)
        'bottom': 'skirt',                      # the tunic's skirt; MISSING: trousers under a skirt (grey)
        'wraps': 'boots',
        'gear': [('chest_strap', {'color': '#4a3420'})],
        'weapon': None,
        'offhand': 'bow', 'offhand_mesh': ('bold_bow', {'height': 1.85, 'bend': 0.1, 'recurve': 0.0}),
        'back': 'quiver',                       # MISSING: hip_arrow_bag (large tan bag low at the right hip)
        'stance_name': 'bow_low',               # MISSING: bow_upright_nocked (longbow vertical, arrow nocked)
        'tint': ['top', 'bottom'],
        'grime': 0.75, 'upper_bulk': 1.25,
    },
    'Medieval Infantry': {
        # a closed great helm, full mail, a civ-color surcoat from the
        # shoulders to the ankles with a gold hem and a brown belt, a tall
        # tan oval shield, a flail hanging at the knee
        'macro': {'muscle': 1.0, 'weight': 0.9, 'height': 0.78, 'proportions': 0.68},
        'colors': {'skin': '#94704f', 'helmet': '#6a6c70', 'mail': '#5a5a5e', 'belt': '#3a2818',
                   'bracers': '#5a5a5e', 'greaves': '#6e7074', 'steel': '#8a8c90', 'wood': '#4a3222',
                   'shield_face': '#8a7048', 'shield_rim': '#3a3a3e', 'bronze': '#6e7074'},
        'helmet': 'cap', 'metal': ['helmet'], 'hair_asset': None, 'beard': False,   # MISSING: great_helm (closed, eye slit)
        'top': 'vest', 'neck': 'high', 'collar': 'mail',   # MISSING: mail_sleeves and mail_chausses (arms and legs)
        'bottom': 'kilt',                       # MISSING: surcoat (one tinted garment, shoulders to ankles, gold hem)
        'leather_bracers': True, 'wraps': 'greaves', 'gear': [],
        'weapon': 'club',
        # MISSING: flail (a haft, a chain and a spiked ball dangling at the knee)
        'props': {'club': ('war_club', {'length': 0.55, 'knob': 0.07, 'studs': 10, 'scale': 1.1})},
        'offhand': 'shield_oval',
        'offhand_mesh': ('shield_oval', {'w': 0.55, 'h': 0.95, 'band': 0.012, 'dome': 0.05}),
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
        'colors': {'skin': '#987452', 'helmet': '#1c2016', 'top': '#20261a', 'bottom': '#1c2016',
                   'boots': '#121410', 'belt': '#2a2a20', 'steel': '#9a9ca0', 'wood': '#6a5034',
                   'leather': '#2a2418'},
        'helmet': 'hood', 'hair_asset': None,   # MISSING: face_mask (dark cloth over the nose and mouth)
        'top': 'sleeved', 'neck': 'high',       # MISSING: long_jacket (to mid-thigh, split panels)
        'bottom': 'long', 'wraps': 'boots',
        'gear': [],                             # MISSING: tintable_harness (straps over both shoulders, V on chest and back)
        'weapon': 'sword',
        # MISSING: kama (a sickle) -- a short, strongly curved sabre stands in
        'props': {'sword': ('sabre', {'length': 0.32, 'width': 0.05, 'curve': 0.12})},
        'offhand': None,                        # MISSING: second kama in the left hand
        'stance_name': 'hang',
        'tint': ['belt'],
        'grime': 0.6, 'upper_bulk': 1.1,
    },
    'Pikeman': {
        # steel morion, breastplate and tassets, civ-color puffy sleeves and
        # dark civ-color breeches, cream stockings, brown shoes, light beard;
        # pike upright, butt by the right foot
        'macro': {'muscle': 0.95, 'weight': 0.8, 'height': 0.76, 'proportions': 0.72},
        'race': {'caucasian': 0.9, 'african': 0.05, 'asian': 0.05},
        'colors': {'skin': '#94704f', 'hair': '#6a5034', 'helmet': '#6e7074', 'cuirass': '#74767a',
                   'belt': '#2e2016', 'wraps': '#a89c80', 'wraps_band': '#7a7058', 'sandal': '#3a2818',
                   'wood': '#4a3222', 'steel': '#8a8c90'},
        'helmet': 'cap', 'metal': ['helmet', 'cuirass'],   # MISSING: morion (brim and comb)
        'beard': True,
        'cuirass': True, 'top': 'sleeved',      # MISSING: puffy_sleeves; breastplate_cross (tinted emblem); tassets
        # tinted breeches with cream stocking wraps over the calves
        'bottom': 'long', 'wraps': 'wraps', 'sandals': True,   # MISSING: knee_breeches (stop at the knee)
        'gear': [],
        'weapon': 'spear',
        # about 1.3x his height; MISSING: partisan_head (side lugs)
        'props': {'spear': ('spear', {'length': 2.4, 'grip': 1.0, 'head': 1.3})},
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
        'colors': {'skin': '#a07c58', 'hair': '#0e0c0c', 'bottom': '#2e2c40', 'belt': '#1a1824',
                   'sandal': '#2a2018'},
        'helmet': None, 'hair': 'topknot', 'hair_asset': 'ponytail01', 'beard': False,
        'top': 'sleeved',                       # MISSING: long_coat (to the knee), wide_sleeves (untinted grey-lavender), white under-robe
        'bottom': 'long',                       # MISSING: hakama (wide pleated trousers)
        'sandals': True, 'wraps': None, 'gear': [],
        'weapon': None,                         # MISSING: sheathed_sword (katana at the left hip, curving back past the knee)
        'stance_name': 'upright',               # MISSING: arms_folded stance
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
                   'wraps': '#8a7656', 'wraps_band': '#5a4a36', 'leather': '#4a3220'},
        'helmet': None, 'hair': 'topknot', 'hair_asset': 'ponytail01', 'beard': True,
        'top': None, 'bottom': 'loincloth', 'loin_length': (0.34, 0.3), 'torn': True,
        'wraps': 'wraps', 'boots': True,        # MISSING: baggy_leggings (tan, under the loincloth)
        'gear': [('chest_strap', {'color': '#4a3220'})],   # MISSING: small_chest_pack
        'weapon': None,
        'offhand': None,                        # MISSING: gourd (a small water gourd in the right hand)
        'back': None,
        'stance_name': 'hang',
        'tint': ['bottom'],                     # MISSING: arm_bands (wide, tinted, on the upper arms)
        'grime': 0.85, 'upper_bulk': 1.1,
    },
    'Swiss Mercenary': {
        # steel morion with a civ-color plume, polished breastplate, a civ-
        # color sash from shoulder to hip, puffy slashed pale sleeves, baggy
        # red breeches slashed black, black shoes, brown beard; a long pike
        # upright with a civ-color shaft
        'macro': {'muscle': 1.0, 'weight': 0.85, 'height': 0.78, 'proportions': 0.7},
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#9a7254', 'hair': '#4a3424', 'helmet': '#7e8086', 'cuirass': '#84868c',
                   'top': '#b8b2a4', 'bottom': '#6e2a20', 'boots': '#161210', 'belt': '#1e1612',
                   'wood': '#4a3222', 'steel': '#8a8c90'},
        'helmet': 'cap', 'metal': ['helmet', 'cuirass'],   # MISSING: morion and plume (tinted)
        'beard': True,
        'cuirass': True, 'top': 'sleeved',      # MISSING: puffy_sleeves (slashed)
        'bottom': 'long',                       # MISSING: slashed_breeches (black stripes)
        'wraps': 'boots',
        # the sash should be civ color; gear straps can't be tinted, and a
        # 'strap' top would hide under the breastplate and drop the sleeves
        'gear': [('chest_strap', {'color': '#3a2a20'})],   # MISSING: tintable_strap (the diagonal sash)
        'weapon': 'spear',
        # MISSING: tinted_shaft (the pike's wood in civ color, steel head and butt plain)
        'props': {'spear': ('spear', {'length': 3.0, 'grip': 1.1, 'head': 1.2})},
        'stance_name': 'upright',
        'tint': [],                             # every tinted part of the original is a missing feature
        'grime': 0.65, 'upper_bulk': 1.25,
    },
}

# MISSING FEATURES
# What the notes need that the supported keys can't express. Each entry above
# approximates it and marks the spot with "MISSING:".
#
# -- civ-color (tint) gaps: the most urgent, since the tint is how a unit reads as its civ
# tintable_strap     Enslaved Worker, Swiss Mercenary, Ninja (harness): gear ('chest_strap', {'tint': True})
#                    honored -- the diagonal shoulder-to-hip strap made with tint=True (and a 'harness' variant:
#                    two straps over the shoulders forming a V/Y on chest and back, joined to the belt).
# tinted_sleeves     Immortals (and cleanly for Libyan Mercenary, Pikeman, Explorer, Bowman): a 'sleeves' part
#                    separate from 'top' so sleeves can be tinted while the tunic body keeps its own color.
# arm_bands / leg_bands  Scout, Enkidu Warrior, Javelin Thrower, Chasquis Scout, Jaguar Warrior, Impi: thin
#                    tintable bands at the upper arm, below the knee and at the ankle ('bands': ['arm','knee','ankle']).
# one_bracer         Gallic Swordsman: bracer on the sword (right) forearm only.
# hem_band           Legionary, Libyan Mercenary, Medieval Infantry (gold): a narrow band at a skirt/tunic hem,
#                    with its own color or tint.
# cuirass_trim       Hoplite: tinted edging on the cuirass at the shoulders, armholes and waist.
# collar_band        Jaguar Warrior, Inquisitor (shoulder_band): a tinted band round the upper chest/shoulders.
# plume              Explorer, Longbowman, Swiss Mercenary, Jaguar Warrior (feather_plume): a tintable feather
#                    plume on the helmet (upright spray for the Swiss/Explorer, a curl at the back for the
#                    Longbowman, a tall spray from the crown for the Jaguar).
# tinted_shaft       Swiss Mercenary: a spear/pike param that makes the wooden shaft tint=True.
# tinted_shield_face Jaguar Warrior: shield_round with a tinted face and a plain gold boss and rim
#                    (today shield_round tints only rim and boss; the entry fakes it with very wide emblem bands).
# plain_shield       Gallic Swordsman, Impi: a shield with no tinted parts (shield_oval param emblem=False).
# tinted_fur_tufts   Impi: 'wraps' fur (G.fur) never tints; the cow-tail tufts must. Make wraps honor tint.
# trouser_stripes    Immortals: tinted vertical stripes on the trousers.
# breastplate_cross  Pikeman: a tinted cross emblem on the breastplate.
#
# -- garments
# long_robe          Inquisitor: a floor-length robe with wide sleeves (bottom 'robe' to the ankles, flared).
# surcoat            Medieval Infantry: one tinted garment from shoulders to ankles over mail, belted, gold hem.
# long_coat          Samurai: a knee-length coat with a skirt (tinted, darker pattern), a white under-robe V
#                    at the chest; Ninja: a mid-thigh jacket with split side panels.
# wide_sleeves       Samurai (untinted grey-lavender, very wide), Inquisitor: sleeves flaring at the wrist.
# puffy_sleeves      Pikeman, Swiss Mercenary (slashed, pale), Explorer: full, puffed sleeves.
# mail_sleeves / mail_chausses  Longbowman, Medieval Infantry: mail on the full arms and legs (top/bottom
#                    in the 'mail' material), not just the collar.
# knee_breeches      Pikeman, Explorer, Swiss Mercenary (slashed, black stripes): baggy breeches ending at the
#                    knee over stockings ('long' bottom with zmin at the knee + a baggy factor).
# skirt_over_trousers  Longbowman: a mid-thigh tunic skirt over grey trousers (two bottom layers).
# hakama             Samurai: wide pleated trousers.
# baggy_leggings     Scout (tan, under a loincloth), Berserk (baggy trousers).
# check_pattern      Gallic Swordsman: a black check over the tinted trousers.
# fur_garment        Enkidu Warrior: vest and kilt made of shaggy fur (G.fur), not cloth.
# long_pelt_cloak    Berserk (to the thighs), Enkidu Warrior (to the calves): pelt_cape with a length.
# cape               Legionary: a tinted cloth cape over both shoulders and the back down to the waist.
# segmentata         Legionary: banded segmented plate in place of a smooth cuirass, pale shoulder plates.
# tassets            Pikeman: steel plates over the hips below the breastplate.
#
# -- headgear and faces
# galea / attic_helmet  Legionary, Libyan Mercenary: an open-faced crested helmet with cheek guards
#                    ('crested' is a closed Corinthian).
# great_helm         Medieval Infantry: a closed flat-topped steel helm with an eye slit.
# morion             Explorer, Pikeman, Swiss Mercenary: a steel helmet with a comb and an upturned brim.
# conical_helmet     Berserk: a pointed iron cap with a nasal.
# cap_knob           Bowman: a gold knob on the crown of the cap.
# jaguar_head        Jaguar Warrior: a spotted jaguar-hide hood with the cat's face over the brow.
# fan_headdress      Chasquis Scout: a broad rounded fan framing the whole head (tinted, white/gold rim).
# feather_headdress  Impi (bushy crown, tinted), Javelin Thrower (small yellow tufts on the hood).
# streamers          Enkidu Warrior: tinted ribbons flicking up off the headband.
# face_veil / face_mask  Immortals (white cloth over chin and mouth), Ninja (dark mask), Javelin Thrower
#                    (dark red face paint/mask).
# braids / long_beard  Berserk (two yellow braids down the chest), Enkidu Warrior (black beard to mid-chest).
# spiked_hair        Gallic Swordsman: limed ginger hair spiked straight up.
#
# -- held and worn props
# battle_axe         Berserk: a long-hafted axe with a big pale steel head (stone_axe stands in).
# star_mace          Chasquis Scout: a short handle with a dark star-shaped stone head.
# macuahuitl         Jaguar Warrior: a flat wooden club edged with obsidian blades.
# flail              Medieval Infantry: a haft, a chain and a spiked ball dangling at the knee.
# scimitar           Immortals: a broad curved blade (sabre stands in).
# kama               Ninja: a sickle in EACH hand (needs a second weapon in the left hand).
# torch              Inquisitor: a short torch with a small orange flame.
# book_raised        Inquisitor: an open book with a tinted cover, held at chest-to-head height in the left hand.
# map_in_hand        Explorer: a white unrolled map/scroll at waist height.
# gourd              Scout: a small brown gourd/pouch held low in the right hand.
# loose_arrow        Bowman: an arrow in the right hand, point down-forward (bow_low puts any weapon in the left hand).
# javelin_bundle     Javelin Thrower: 3-4 javelins planted upright at his left, taller than him, tinted shaft
#                    wraps and yellow tassels.
# scutum             Legionary: a tall curved rectangular shield with a tinted thunderbolt emblem on grey.
# cowhide_shield     Impi: a tall oval black-and-white hide shield with a stick behind it.
# sunburst_emblem    Libyan Mercenary: a star emblem (tinted) in place of the cross bands; Hoplite: a dark
#                    painted device (shield_emblem_paint).
# sheathed_sword     Explorer (rapier, left hip), Samurai (katana, left hip, curving back past the knee).
# shackles           Enslaved Worker: iron cuffs at wrists and ankles with a chain between the ankles.
# partisan_head      Pikeman: a pike head with small side lugs.
# hip_arrow_bag      Longbowman: a large tan arrow bag low at the right hip (not a back quiver).
# small_chest_pack   Scout: a small pack on the chest strap.
# pectoral           Jaguar Warrior: a gold collar/pectoral.
#
# -- stances
# two_hand_low       Berserk: haft level across the hips in both hands, the head out to the right at knee height.
# arms_folded        Samurai: arms crossed on the chest, no weapon in hand.
# spade_upright      Enslaved Worker: a spade upright at the right side, blade on the ground (low_across stands in).
# javelin_level      Javelin Thrower: a javelin held low and horizontal, point forward.
# bow_upright_nocked Longbowman: the longbow vertical in front, an arrow nocked, the right hand at the string.
