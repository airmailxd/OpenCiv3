"""Remaster entries for the 44 Civ3 "royal" units: the kings, the leaders and
the armies (plain data, same format as remaster.py's REMASTER: each entry is
merged over the unit's catalog entry, dicts one level deep).

The notes (notes/<Unit>.md) are the truth where they differ from the catalog,
above all on the pose (most catalog gestures are wrong: most kings stand with
arms low, fists on hips or hands clasped), the held items and the "Tinted
(civ color)" parts. Direction: Civ3 remastered, not AI-looking -- imposing,
regal, a powerful presence; heavier, dignified builds aged per the note;
deep, real fabric colors (velvet, brocade, fur, gold); cleaner than the
soldiers (grime 0.3-0.6) but still weathered.

The catalog's royal vocabulary (headgear, held_r, gesture, robe lists, a
cape dict, top 'long_sleeved' ...) is not read by units.build_footman, so
every entry sets the keys the builder does read explicitly: top, bottom,
helmet, hair_asset, beard, cape, cuirass, wraps, sandals, bracers, gear,
weapon/offhand, stance_name, tint. Ankle-length robes are a 'kilt' with
'robe_drop' (meters dropped toward the ankles); capes are 'cape': True with
'cape_length' (meters from the shoulder line; ~1.35 reaches the floor).

Conventions where no key exists yet:
- a tinted waistcoat, vest, chest panel or surcoat is a non-metal 'cuirass'
  (the front-and-back torso panel) with 'cuirass' in tint;
- crowns are 'helmet': 'crown' ('crown': spiked/arched/tall/circlet, in
  colors['gold']); a turban or soft hat is a cloth/leather 'cap';
- held regalia are the real props in the right hand: war_standard /
  eagle_standard held 'upright' (the butt toward the ground), scepter, crook,
  swagger_stick and microphone held up in the 'royal' stance (tilted with
  'upright_dir'), a cane held 'upright' (its rod runs down from the knob in
  the fist to the ground); left-hand items stay MISSING;
- women are 'gender': 0.0 with a 'gown' bottom ('gown_flare', 'robe_drop');
- royal poses are the stances 'fists_on_hips', 'hands_clasped' (also for
  hands together at the chest), 'hands_behind' and 'royal' (a scepter held
  up in the right hand); one fist on the hip still falls back to 'hang'.
Each gap is marked "MISSING: <feature>" and listed under MISSING FEATURES at
the bottom.
"""

REMASTER = {
    # ------------------------------------------------------------ armies
    'Army Ancient Times': {
        # a bearded elder with both hands on a tall banner pole; a coarse tan
        # robe open over a cream undergown, civ color on the lapels, cuffs
        # and two hem bands, and on the flag field
        'macro': {'muscle': 0.75, 'weight': 0.7, 'height': 0.75, 'proportions': 0.7}, 'age': 0.75,
        'colors': {'skin': '#b4845e', 'hair': '#6a5a4a', 'top': '#9a8262', 'bottom': '#9a8262',
                   'belt': '#4a3424', 'sandal': '#4a3020', 'wood': '#5e4228', 'leather': '#3e2a1a'},
        'hair': 'short', 'hair_asset': 'short02', 'beard': True, 'helmet': None,
        'top': 'sleeved', 'wide_sleeves': True, 'neck': 'high',
        'bottom': 'kilt', 'robe_drop': 0.45,   # MISSING: undergown (cream gown showing down the open front)
        'bands': [('wrist', 'tint')],           # the civ-color sleeve cuffs
        # MISSING: lapel_trim (tinted robe front edges), hem_band x2 (tinted, near the hem)
        'cape': False, 'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False, 'gear': [],
        # the crossbar-hung vexillum, tinted field; MISSING: two_hand_pole stance
        'weapon': 'standard', 'props': {'standard': ('war_standard', {'era': 'ancient', 'length': 2.6, 'grip': 1.2})},
        'stance_name': 'upright',
        'tint': ['banner'],
        'grime': 0.55, 'upper_bulk': 1.1,
    },
    'Army Industrial Ages': {
        # an officer in a black bicorne and a civ-color frock coat with gold
        # epaulettes, grey trousers, tall black boots, holding the flagpole
        'macro': {'muscle': 0.8, 'weight': 0.7, 'height': 0.78, 'proportions': 0.72}, 'age': 0.55,
        'race': {'caucasian': 0.9, 'african': 0.05, 'asian': 0.05},
        'colors': {'skin': '#d4a888', 'hair': '#3a2a1c', 'helmet': '#1a1a1e', 'bottom': '#8a8c92',
                   'boots': '#141416', 'belt': '#1a1a1e', 'gold': '#c8a038', 'wood': '#4a3222'},
        'helmet': 'cap', 'metal': [],           # MISSING: bicorne
        'hair': 'short', 'hair_asset': 'short02', 'beard': False,
        'top': 'sleeved', 'neck': 'high',       # MISSING: frock_coat (knee-length skirts), epaulettes (gold), white waistcoat front
        'bottom': 'long', 'wraps': 'boots',
        'bands': [('wrist', 'gold')],           # gold cuffs
        'cape': False, 'cuirass': False, 'sandals': False, 'bracers': False, 'gear': [],
        # the flag flying from the pole top, tinted field; MISSING: two_hand_pole stance
        'weapon': 'standard', 'props': {'standard': ('war_standard', {'era': 'industrial', 'length': 2.6, 'grip': 1.2})},
        'stance_name': 'upright',
        'tint': ['top', 'banner'],
        'grime': 0.45, 'upper_bulk': 1.15,
    },
    'Army Middle Ages': {
        # an armored king: gold crown over a dark coif, full plate, a dark
        # tabard skirt, a floor-length civ-color cape with a white fur hem and
        # a gold lion; both hands on the swallow-tailed banner's pole
        'macro': {'muscle': 0.9, 'weight': 0.8, 'height': 0.8, 'proportions': 0.7}, 'age': 0.6,
        'race': {'caucasian': 0.9, 'african': 0.05, 'asian': 0.05},
        'colors': {'skin': '#c49a7e', 'hair': '#3a2a1c', 'helmet': '#3a3836', 'top': '#9a9ea6',
                   'cuirass': '#a8acb4', 'greaves': '#9a9ea6', 'bottom': '#4a4238', 'belt': '#2e2418',
                   'mail': '#6a6c70', 'wood': '#4a3222', 'gold': '#c8a038'},
        'helmet': 'crown', 'crown': 'spiked',   # MISSING: coif (dark, under the crown)
        'hair': 'short', 'hair_asset': 'short02', 'beard': True,
        'cuirass': True, 'metal': ['cuirass'], 'top': 'sleeved', 'pauldrons': True, 'collar': 'mail',
        'bottom': 'kilt', 'robe_drop': 0.0, 'wraps': 'greaves',   # MISSING: plate_legs (cuisses over the thighs)
        'cape': True, 'cape_length': 1.35,      # MISSING: ermine_trim (white fur hem), cape_emblem (gold lion), cape gold bands
        'sandals': False, 'bracers': False, 'gear': [],
        # the swallow-tailed, crossbar-hung banner, tinted field; MISSING: two_hand_pole stance
        'weapon': 'standard', 'props': {'standard': ('war_standard', {'era': 'medieval', 'length': 2.6, 'grip': 1.2})},
        'stance_name': 'upright',
        'tint': ['cape', 'banner'],
        'grime': 0.5, 'upper_bulk': 1.25,
    },
    'Army Modern Times': {
        # a soldier in a round olive steel helmet, olive tunic, khaki
        # trousers, brown boots and a broad gold-edged civ-color sash
        'macro': {'muscle': 0.85, 'weight': 0.7, 'height': 0.78, 'proportions': 0.72}, 'age': 0.45,
        'colors': {'skin': '#c89a74', 'hair': '#2a1e14', 'helmet': '#3e4430', 'top': '#545a3c',
                   'bottom': '#9a8c6c', 'boots': '#4e3424', 'belt': '#3a2a1c', 'wood': '#4a3222'},
        'helmet': 'cap', 'metal': ['helmet'],   # MISSING: brodie_helmet (round brim)
        'hair': 'short', 'hair_asset': 'short02', 'beard': False,
        'top': 'sleeved', 'neck': 'high', 'bottom': 'long', 'wraps': 'boots',
        'gear': [('chest_strap', {})],          # the tinted sash; MISSING: broad_sash (wide, gold-edged)
        'cape': False, 'cuirass': False, 'sandals': False, 'bracers': False,
        # the big flag, tinted field; MISSING: flag_device (diagonal cross), two_hand_pole stance
        'weapon': 'standard', 'props': {'standard': ('war_standard', {'era': 'modern', 'length': 2.6, 'grip': 1.2})},
        'stance_name': 'upright',
        'tint': ['strap', 'banner'],
        'grime': 0.55, 'upper_bulk': 1.15,
    },

    # ------------------------------------------------------------ kings
    'King American': {
        # Lincoln: tall and gaunt-dignified, a stovepipe hat with a civ-color
        # band, chin beard, black frock coat over a civ-color waistcoat,
        # white collar; hands clasped behind the back
        'macro': {'muscle': 0.7, 'weight': 0.5, 'height': 0.92, 'proportions': 0.78}, 'age': 0.7,
        'race': {'caucasian': 0.95, 'african': 0.05, 'asian': 0.0},
        'colors': {'skin': '#c89070', 'hair': '#241c16', 'helmet': '#141416', 'top': '#1e1c1c',
                   'cuirass': '#2a2826', 'bottom': '#1a1818', 'boots': '#3a2418', 'belt': '#1a1818',
                   'shirt': '#e8e4dc'},
        'helmet': 'cap', 'metal': [],           # MISSING: top_hat (tall stovepipe, tinted band)
        'hair': 'short', 'hair_asset': None,
        'beard': True,                          # MISSING: chin_beard (no mustache)
        'top': 'sleeved', 'neck': 'high',       # MISSING: frock_coat (knee-length)
        'cuirass': True,    # the waistcoat front panel (tinted)
        'bands': [('neck', 'shirt')],           # the white shirt collar
        'bottom': 'long', 'wraps': 'boots',     # MISSING: low_shoes
        'cape': False, 'sandals': False, 'bracers': False, 'gear': [],
        'weapon': None, 'offhand': None,
        'stance_name': 'hands_behind',
        'tint': ['cuirass'],
        'grime': 0.35, 'upper_bulk': 1.0,
    },
    'King Arab': {
        # a big round civ-color turban, full black beard, a long loose cream
        # robe with wide sleeves, a civ-color baldric shoulder to hip; arms
        # folded at the waist
        'macro': {'muscle': 0.8, 'weight': 0.8, 'height': 0.78, 'proportions': 0.7}, 'age': 0.6,
        'race': {'caucasian': 0.7, 'african': 0.2, 'asian': 0.1},
        'colors': {'skin': '#a87e5c', 'hair': '#1a1410', 'top': '#ddd4c0', 'bottom': '#ddd4c0',
                   'belt': '#8a6a3a', 'sandal': '#4a3622', 'helmet': '#ddd4c0'},
        'helmet': 'cap', 'metal': [],           # MISSING: turban (large round wound cloth)
        'hair': 'short', 'hair_asset': None, 'beard': True,
        'top': 'sleeved', 'wide_sleeves': True, 'neck': 'high',
        'bottom': 'kilt', 'robe_drop': 0.48,    # MISSING: hem_band, front_trim (tinted)
        'gear': [('chest_strap', {})],          # the diagonal baldric; MISSING: strap_stripe (gold stripe along it)
        'bands': [('wrist', 'tint')],           # the tinted sleeve cuffs
        'cape': False, 'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False,
        'weapon': None, 'offhand': None,
        'stance_name': 'arms_folded',
        'tint': ['helmet', 'strap'],
        'grime': 0.4, 'upper_bulk': 1.15,
    },
    'King Austria': {
        # Maximilian-era prince: a dark low hat with a white feather, beard,
        # puffed civ-color sleeves, a quilted brown vest, a short tan brocade
        # cape with civ-color edging, brown puffed breeches, white hose; a
        # wolfhound at his side, his hand near its back
        'macro': {'muscle': 0.8, 'weight': 0.78, 'height': 0.78, 'proportions': 0.7}, 'age': 0.6,
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#d0a07a', 'hair': '#3a2616', 'helmet': '#1c1a1a', 'plume': '#ece8e0',
                   'top': '#5a3e28', 'bottom': '#5a3e28', 'cape': '#c0a06a', 'belt': '#2a2018',
                   'wraps': '#e4e0d4', 'wraps_band': '#c8c2b4', 'sandal': '#2a2018', 'gold': '#c8a038'},
        'helmet': 'cap', 'metal': [], 'plume': True,   # MISSING: beret (low, wide), feather laid sideways
        'hair': 'short', 'hair_asset': None, 'beard': True,
        'top': 'sleeved', 'puffy': True,        # the civ-color puffed sleeves; MISSING: quilting, gold dots
        'bottom': 'breeches', 'wraps': 'wraps', 'sandals': True,   # white hose; MISSING: low_shoes (gold buckles)
        'cape': True, 'cape_length': 0.5,       # MISSING: brocade (pattern), cape_edge_tint
        'cuirass': False, 'bracers': False, 'gear': [],
        'weapon': None, 'offhand': None,        # MISSING: companion_dog (grey wolfhound, tinted collar)
        'stance_name': 'hang',
        'tint': ['sleeves'],
        'grime': 0.4, 'upper_bulk': 1.15,
    },
    'King Aztec': {
        # Montezuma: a tall gold headdress with an upright fan of gold, civ
        # color and red feathers; bare chest, gold collar; a long civ-color
        # cloak edged in gold; a teal loincloth; arms crossed
        'macro': {'muscle': 0.9, 'weight': 0.75, 'height': 0.76, 'proportions': 0.7}, 'age': 0.55,
        'race': {'caucasian': 0.05, 'african': 0.15, 'asian': 0.8},
        'colors': {'skin': '#946c4c', 'hair': '#140e0a', 'helmet': '#c8a038', 'bottom': '#3e7a6a',
                   'belt': '#c8a038', 'sandal': '#4a3020', 'gold': '#c8a038', 'cape': '#9a3a2a'},
        'helmet': 'headband', 'plume': True,    # MISSING: feather_headdress (tall upright fan, tinted and red feathers)
        'hair': 'long', 'hair_asset': 'bob01', 'beard': False,
        'top': None,
        'bottom': 'loincloth', 'loin_length': (0.5, 0.42), 'torn': False,
        'bands': [('neck', 'gold'), ('upperarm', 'gold'), ('wrist', 'gold')],   # MISSING: broad_collar (gold pectoral)
        'cape': True, 'cape_length': 1.1,       # MISSING: cape_panels (gold top, fringed teal/tinted panels, red block)
        'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False, 'gear': [],
        'weapon': None, 'offhand': None,
        'stance_name': 'arms_folded',
        'tint': ['cape', 'plume'],
        'grime': 0.45, 'upper_bulk': 1.25,
    },
    'King Babylonian': {
        # Hammurabi: a tall flat-topped gold-cream cylinder cap with a civ
        # band, a long square black beard, a pale gold-cream ankle robe, a
        # floor-length civ-color cape with gold edges; fists on hips
        'macro': {'muscle': 0.85, 'weight': 0.85, 'height': 0.78, 'proportions': 0.68}, 'age': 0.65,
        'race': {'caucasian': 0.7, 'african': 0.2, 'asian': 0.1},
        'colors': {'skin': '#a87e5c', 'hair': '#140e0a', 'helmet': '#c8b070', 'top': '#d4c48c',
                   'bottom': '#d4c48c', 'belt': '#a8862e', 'sandal': '#4a3622', 'gold': '#c8a038'},
        'helmet': 'crown', 'crown': 'tall', 'crown_height': 0.07,
        'hair': 'long', 'hair_asset': 'bob01',
        'beard': True,                          # MISSING: long_beard (square, to the chest)
        'top': 'sleeved', 'neck': 'high', 'bottom': 'kilt', 'robe_drop': 0.48,
        'hem_bands': [('bottom', 'gold', 0.035), ('bottom', 'tint', 0.03, 0.05)],
        'cape': True, 'cape_length': 1.38,      # MISSING: cape_edge (gold)
        'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False, 'gear': [],
        'weapon': None, 'offhand': None,
        'stance_name': 'fists_on_hips',
        'tint': ['cape'],
        'grime': 0.45, 'upper_bulk': 1.2,
    },
    'King Byzantines': {
        # Theodora: a domed jeweled gold crown, long dark hair, a floor-
        # length white gown with tinted sleeves and front panel, a broad gold
        # jeweled collar, a full-length civ-color cape; hands together at the
        # chest holding a small white object
        'macro': {'muscle': 0.5, 'weight': 0.5, 'height': 0.72, 'proportions': 0.78}, 'age': 0.5,
        'race': {'caucasian': 0.8, 'african': 0.1, 'asian': 0.1},
        'colors': {'skin': '#d0aa8c', 'hair': '#2a1a10', 'helmet': '#c8a038', 'top': '#e8e2cc',
                   'bottom': '#e8e2cc', 'belt': '#c8a038', 'sandal': '#c8a040', 'gold': '#c8a038'},
        'gender': 0.0, 'helmet': 'crown', 'crown': 'arched', 'crown_gems': '#3a6ac8',
        'hair': 'long', 'hair_asset': 'long01', 'beard': False,
        'top': 'sleeved', 'wide_sleeves': True, 'neck': 'high',
        'bottom': 'gown', 'robe_drop': 0.55,
        'bands': [('neck', 'gold')],            # MISSING: broad_collar (gold jeweled)
        'cape': True, 'cape_length': 1.38,      # MISSING: cape_hem_band (broad gold)
        'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False, 'gear': [],
        'weapon': None, 'offhand': None,        # MISSING: small_object (white, held at the chest)
        'stance_name': 'hands_clasped',
        'tint': ['cape', 'sleeves'],
        'grime': 0.3, 'upper_bulk': 1.0,
    },
    'King Carthaginian': {
        # Hannibal: bald, a grey scale cuirass with a scaled back cape, gold
        # shoulder pieces, brown baggy trousers into boots, civ-color
        # wristbands and trouser stripes, a short sword sheathed at the hip
        'macro': {'muscle': 0.95, 'weight': 0.8, 'height': 0.78, 'proportions': 0.68}, 'age': 0.55,
        'race': {'caucasian': 0.6, 'african': 0.3, 'asian': 0.1},
        'colors': {'skin': '#a07c5e', 'hair': '#241a12', 'cuirass': '#727880', 'top': '#b08a34',
                   'bottom': '#5a4a3a', 'boots': '#2e241c', 'belt': '#3a2a1c', 'steel': '#a8aaae',
                   'leather': '#3a2818'},
        'helmet': None, 'hair': 'bald', 'hair_asset': None, 'beard': False,
        'cuirass': True, 'metal': ['cuirass'], 'neck': 'high',   # MISSING: scale_armor (scales, scaled back cape)
        'top': None, 'pauldrons': True,         # gold pauldrons (they take the 'top' color)
        'bottom': 'long', 'puffy': True, 'wraps': 'boots',   # MISSING: trouser_stripes (tinted, down each side)
        'bands': [('wrist', 'tint')],
        'cape': False, 'sandals': False, 'bracers': False, 'gear': [],
        'weapon': None, 'offhand': None,
        'hip': ('sheathed_sword', {'length': 0.6}),
        'stance_name': 'hang',
        'tint': [],
        'grime': 0.55, 'upper_bulk': 1.3,
    },
    'King Celtic': {
        # Boudica's people: long dark hair, a big mustache, a knee-length
        # civ-color tartan tunic, a brown baldric, a long sword sheathed on
        # the back (gold hilt over the shoulder), boots; arms hanging
        'macro': {'muscle': 0.95, 'weight': 0.85, 'height': 0.8, 'proportions': 0.66}, 'age': 0.55,
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#c09a78', 'hair': '#3a2414', 'belt': '#4a3020', 'boots': '#3a2a1c',
                   'leather': '#5a3a22', 'gold': '#c8a038'},
        'helmet': None, 'hair': 'long', 'hair_asset': 'long01', 'mane': 0.18,
        'beard': True,                          # MISSING: mustache (big, drooping, no beard)
        'top': 'tunic', 'bottom': 'kilt', 'robe_drop': 0.0,   # MISSING: tartan (dark green check on the civ color)
        'gear': [('chest_strap', {'color': '#5a3a22'})],
        'wraps': 'boots', 'cape': False, 'cuirass': False, 'sandals': False, 'bracers': False,
        'weapon': None, 'offhand': None,        # MISSING: back_scabbard (long sword on the back, gold hilt), shoulder_clasp
        'stance_name': 'hang',
        'tint': ['top', 'bottom'],
        'grime': 0.6, 'upper_bulk': 1.3,
    },
    'King Chinese': {
        # Mao: thinning dark hair, a grey-blue high-collared suit, darker
        # trousers, brown shoes, civ-color armbands; hands clasped low in front
        'macro': {'muscle': 0.65, 'weight': 0.85, 'height': 0.76, 'proportions': 0.7}, 'age': 0.75,
        'race': {'caucasian': 0.0, 'african': 0.0, 'asian': 1.0},
        'colors': {'skin': '#c89c76', 'hair': '#141414', 'top': '#66707c', 'bottom': '#4e5664',
                   'boots': '#3a2418', 'belt': '#4e5664'},
        'helmet': None, 'hair': 'short', 'hair_asset': 'short02', 'beard': False,   # MISSING: balding_hair
        'top': 'sleeved', 'neck': 'high', 'bottom': 'long', 'wraps': 'boots',   # MISSING: low_shoes
        'bands': [('upperarm', 'tint')],
        'cape': False, 'cuirass': False, 'sandals': False, 'bracers': False, 'gear': [],
        'weapon': None, 'offhand': None,
        'stance_name': 'hands_clasped',
        'tint': [],
        'grime': 0.3, 'upper_bulk': 1.05,
    },
    'King Egyptian': {
        # Ramesses: a striped civ-color nemes with a gold uraeus, a gold broad
        # collar and arm bands, a white pleated ankle kilt with a tinted
        # apron; a long gold crook held low and out; the other hand on the hip
        'macro': {'muscle': 0.9, 'weight': 0.65, 'height': 0.78, 'proportions': 0.72}, 'age': 0.5,
        'race': {'caucasian': 0.4, 'african': 0.5, 'asian': 0.1},
        'colors': {'skin': '#8a5a34', 'hair': '#141010', 'helmet': '#e4dcc4', 'bottom': '#f0ece0',
                   'belt': '#c8a038', 'sandal': '#c8a040', 'gold': '#c8a038', 'wood': '#b8902e',
                   'leather': '#b8902e'},
        'helmet': 'hood',                       # MISSING: nemes (striped headcloth, lappets, gold uraeus)
        'hair': 'bald', 'hair_asset': None, 'beard': False,
        'top': None,
        'bottom': 'kilt', 'robe_drop': 0.42,    # MISSING: pleats, front_apron (tinted hanging panel)
        'bands': [('neck', 'gold'), ('upperarm', 'gold'), ('wrist', 'gold')],   # MISSING: broad_collar
        'cape': False, 'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False, 'gear': [],
        # the gold crook, held up; MISSING: crook extras (tinted bands; they're lapis)
        'weapon': 'crook', 'props': {'crook': ('crook', {'length': 1.1})},
        'stance_name': 'royal',                 # MISSING: held_low_forward, fist_on_hip (left), walking stance
        'tint': ['helmet'],
        'grime': 0.4, 'upper_bulk': 1.2,
    },
    'King English': {
        # Elizabeth I: red hair in an updo with a small crown, a tall lace
        # ruff fanning up behind the head, a civ-color bodice, puffed sleeves
        # and bell farthingale with gold stripes; a gold scepter across the
        # chest, the other hand low at the waist
        'macro': {'muscle': 0.45, 'weight': 0.5, 'height': 0.72, 'proportions': 0.78}, 'age': 0.6,
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#e4c8b4', 'hair': '#9a4a26', 'helmet': '#c8a038', 'belt': '#c8a038',
                   'gold': '#c8a038', 'ruff': '#f0eee8', 'wood': '#b8902e', 'leather': '#b8902e'},
        'gender': 0.0, 'helmet': 'crown', 'crown': 'spiked', 'crown_height': 0.05,
        'hair': 'short', 'hair_asset': 'bob01', 'beard': False,   # MISSING: updo
        'top': 'sleeved', 'puffy': True, 'neck': 'high',
        'bottom': 'gown', 'gown_flare': 1.9, 'robe_drop': 0.55,   # MISSING: farthingale stripes (gold) and jewel dots
        'bands': [('neck', 'ruff')],            # MISSING: ruff (tall lace fan behind the head)
        'cape': False, 'cuirass': False, 'wraps': None, 'sandals': False, 'bracers': False, 'gear': [],
        # the gold scepter (plain knob head), tilted across the chest
        'weapon': 'scepter', 'props': {'scepter': ('scepter', {'length': 0.7, 'head': 'fleur'})},
        'stance_name': 'royal', 'upright_dir': (0.5, -0.15, 1.0),
        'tint': ['top', 'bottom', 'sleeves'],
        'grime': 0.3, 'upper_bulk': 1.0,
    },
    'King French': {
        # Joan / a French king in full silver plate, bareheaded, auburn hair;
        # a floor-length civ-color cape and shoulder mantle with a gold
        # fleur-de-lis; a brown-hilted sword low and forward
        'macro': {'muscle': 0.9, 'weight': 0.75, 'height': 0.8, 'proportions': 0.72}, 'age': 0.5,
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#e0b494', 'hair': '#8a3a1c', 'cuirass': '#b4b8c0', 'top': '#a4a8b0',
                   'bottom': '#9ea2aa', 'greaves': '#a4a8b0', 'belt': '#3a2a1c', 'boots': '#8a8e96',
                   'steel': '#b8bcc2', 'leather': '#4a3020', 'mail': '#7a7c80'},
        'helmet': None, 'hair': 'short', 'hair_asset': 'short02', 'beard': False,
        'cuirass': True, 'metal': ['cuirass'], 'neck': 'high', 'top': 'sleeved', 'pauldrons': True,
        'bottom': 'long', 'wraps': 'greaves',   # MISSING: plate_legs, plate_arms (metal, not cloth), sabatons
        'cape': True, 'cape_length': 1.38,      # MISSING: shoulder_mantle (tinted), cape_emblem (gold fleur-de-lis)
        'sandals': False, 'bracers': False, 'gear': [],
        'weapon': 'sword', 'props': {'sword': ('long_sword', {'length': 0.9, 'guard': 0.2})},
        'stance_name': 'hang',
        'tint': ['cape'],
        'grime': 0.45, 'upper_bulk': 1.25,
    },
    'King German': {
        # Bismarck: a silver pickelhaube, a big mustache, a pale blue-grey
        # frock coat, gold epaulettes and cuffs, a civ-color sash shoulder to
        # hip, a white belt, black trousers and boots; left fist on hip
        'macro': {'muscle': 0.85, 'weight': 0.9, 'height': 0.82, 'proportions': 0.7}, 'age': 0.75,
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#d8ac8c', 'hair': '#c8c0b0', 'helmet': '#a8acb4', 'top': '#8a9cb0',
                   'bottom': '#18181a', 'boots': '#121214', 'belt': '#e8e4dc', 'gold': '#c8a038'},
        'helmet': 'cap', 'metal': ['helmet'],   # MISSING: pickelhaube (tall spike)
        'hair': 'short', 'hair_asset': None,
        'beard': True,                          # MISSING: mustache (big walrus, no beard)
        'top': 'sleeved', 'neck': 'high',       # MISSING: frock_coat (knee-length), epaulettes (gold)
        'bottom': 'long', 'wraps': 'boots',
        'gear': [('chest_strap', {})],          # the tinted diagonal sash
        'bands': [('wrist', 'gold')],
        'cape': False, 'cuirass': False, 'sandals': False, 'bracers': False,
        'weapon': None, 'offhand': None,
        'stance_name': 'hang',                  # MISSING: fist_on_hip (left)
        'tint': ['strap'],
        'grime': 0.35, 'upper_bulk': 1.2,
    },
    'King Greek': {
        # Alexander: short curly hair with a civ-color headband, a sleeveless
        # white chiton to the ankles, a big civ-color himation over the left
        # shoulder and round the hips, a gold neck collar, gold sandals;
        # hands together at the waist
        'macro': {'muscle': 0.9, 'weight': 0.7, 'height': 0.78, 'proportions': 0.72}, 'age': 0.45,
        'race': {'caucasian': 0.9, 'african': 0.05, 'asian': 0.05},
        'colors': {'skin': '#c4a07e', 'hair': '#3a2616', 'top': '#e4e0d6', 'bottom': '#e4e0d6',
                   'belt': '#c8a038', 'sandal': '#c8a038', 'gold': '#c8a038'},
        'helmet': 'headband', 'hair': 'short', 'hair_asset': 'short04', 'beard': False,   # MISSING: curly_hair
        'top': 'vest', 'bottom': 'kilt', 'robe_drop': 0.45,
        # MISSING: himation (draped over the left shoulder, wrapping the hips to below the knee)
        'gear': [('chest_strap', {})], 'cape': True, 'cape_length': 0.95,
        'bands': [('neck', 'gold')],
        'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False,
        'weapon': None, 'offhand': None,
        'stance_name': 'hands_clasped',
        'tint': ['helmet', 'strap', 'cape'],
        'grime': 0.35, 'upper_bulk': 1.2,
    },
    'King Hatti': {
        # a gold circlet on dark hair, full dark beard, a cream ankle robe, a
        # civ-color shawl over shoulders and chest, a maroon front apron, a
        # tinted waist band, a short gold knobbed scepter held low and forward
        'macro': {'muscle': 0.85, 'weight': 0.85, 'height': 0.76, 'proportions': 0.68}, 'age': 0.6,
        'race': {'caucasian': 0.8, 'african': 0.1, 'asian': 0.1},
        'colors': {'skin': '#a88262', 'hair': '#241a12', 'helmet': '#c8a038', 'top': '#d8ccaa',
                   'bottom': '#d8ccaa', 'belt': '#c8a038', 'sandal': '#4a3622', 'gold': '#c8a038',
                   'wood': '#b8902e'},
        'helmet': 'crown', 'crown': 'circlet', 'hair': 'short', 'hair_asset': 'short02', 'beard': True,
        'top': 'mantle', 'neck': 'high',        # the tinted shoulder mantle (over cream sleeves: MISSING: shoulder_mantle layer)
        'bottom': 'kilt', 'robe_drop': 0.48,    # MISSING: hem_band (tinted), front_apron (maroon), seam line
        'bands': [('waist', 'tint')],
        'cape': False, 'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False, 'gear': [],
        # the short gold knobbed scepter, tipped forward toward level
        'weapon': 'scepter', 'props': {'scepter': ('scepter', {'length': 0.5, 'head': 'fleur'})},
        'stance_name': 'royal', 'upright_dir': (0.0, -1.0, 0.35),
        'tint': ['top'],
        'grime': 0.45, 'upper_bulk': 1.2,
    },
    'King Incan': {
        # Pachacuti: a gold crown with a front disc, a knee-length gold and
        # brown checkerboard tunic, a heavy civ-color cape to the knee with a
        # gold band and white zig-zag, gold leg bands; a tall feather-topped
        # staff planted at his left
        'macro': {'muscle': 0.85, 'weight': 0.8, 'height': 0.74, 'proportions': 0.68}, 'age': 0.6,
        'race': {'caucasian': 0.0, 'african': 0.15, 'asian': 0.85},
        'colors': {'skin': '#8a6448', 'hair': '#141010', 'helmet': '#c8a038', 'top': '#b0904a',
                   'bottom': '#6a4e2a', 'belt': '#c8a038', 'sandal': '#b89a50', 'gold': '#c8a038',
                   'wood': '#3e2a1a'},
        'helmet': 'crown', 'crown': 'circlet',  # MISSING: crown_disc (gold, at the front)
        'hair': 'short', 'hair_asset': None, 'beard': False,
        'top': 'tunic', 'bottom': 'kilt', 'robe_drop': 0.0,   # MISSING: checkerboard (gold/brown tunic)
        'bands': [('knee', 'gold'), ('neck', 'gold')],
        'cape': True, 'cape_length': 1.05,      # MISSING: zigzag_border (white stepped, gold band on the cape)
        'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False, 'gear': [],
        # MISSING: feather_standard (staff topped with a fan of tinted/red/teal feathers), left_hand_hold
        'weapon': 'staff', 'props': {'staff': ('staff', {'length': 2.1, 'grip': 1.2})},
        'stance_name': 'upright',
        'tint': ['cape'],
        'grime': 0.45, 'upper_bulk': 1.15,
    },
    'King Indian': {
        # Gandhi: bald, glasses, a white shawl over the shoulders hanging long
        # down the back, a white knee dhoti, a narrow civ-color sash, sandals,
        # a tall thin walking stick; slightly stooped, walking
        'macro': {'muscle': 0.55, 'weight': 0.3, 'height': 0.72, 'proportions': 0.75}, 'age': 0.85,
        'race': {'caucasian': 0.4, 'african': 0.3, 'asian': 0.3},
        'colors': {'skin': '#8a6448', 'hair': '#c8c4bc', 'top': '#e4e2dc', 'bottom': '#e4e2dc',
                   'cape': '#e4e2dc', 'belt': '#c8c2b4', 'sandal': '#5a3a22', 'wood': '#5a3a22'},
        'helmet': None, 'hair': 'bald', 'hair_asset': None, 'beard': False,   # MISSING: face_prop (props.glasses 'round')
        'top': 'mantle', 'bottom': 'kilt', 'robe_drop': 0.0,   # MISSING: dhoti (wrapped between the legs)
        'cape': True, 'cape_length': 1.1,       # the long shawl down the back
        'gear': [('chest_strap', {})],
        'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False,
        # MISSING: tinted_tip (the stick's top)
        'weapon': 'staff', 'props': {'staff': ('staff', {'length': 1.55, 'grip': 0.95})},
        'stance_name': 'upright',               # MISSING: walking_stoop
        'tint': ['strap'],
        'grime': 0.4, 'upper_bulk': 0.95,
    },
    'King Iroquois': {
        # Hiawatha: a big feathered war bonnet with tinted tips, long dark
        # hair, bare chest with a tinted shoulder strap, tinted breechcloth,
        # brown leggings, arm and leg bands; a tall spear planted at his
        # right, left fist on hip, a round hide shield at the hip
        'macro': {'muscle': 0.95, 'weight': 0.75, 'height': 0.8, 'proportions': 0.68}, 'age': 0.5,
        'race': {'caucasian': 0.1, 'african': 0.1, 'asian': 0.8},
        'colors': {'skin': '#9a7458', 'hair': '#141010', 'plume': '#e8e4da', 'wraps': '#5e4632',
                   'wraps_band': '#3e2e20', 'belt': '#5a3a22', 'sandal': '#5a3a22', 'wood': '#4a3222',
                   'steel': '#8a8c90', 'shield_face': '#d8ccb0', 'shield_rim': '#8a6a48'},
        'helmet': 'headband', 'plume': True,    # MISSING: war_bonnet (feather fan, tinted tips, trailer down the back)
        'hair': 'long', 'hair_asset': 'long01', 'beard': False,
        'top': None, 'bottom': 'loincloth', 'loin_length': (0.5, 0.45), 'torn': False,
        'wraps': 'wraps', 'sandals': False,     # MISSING: leggings (brown cloth), moccasins
        'gear': [('chest_strap', {})],
        'bands': [('upperarm', 'tint'), ('knee', 'tint')],
        'cape': False, 'cuirass': False, 'bracers': False,
        # MISSING: feathered_spear (fur/feather wrap below the head)
        'weapon': 'spear', 'props': {'spear': ('spear', {'length': 2.3, 'grip': 1.15})},
        'offhand': 'shield_round', 'offhand_mesh': ('shield_round', {'r': 0.25, 'boss': False, 'tint': 'none'}),
        'shield_at': 'left_hip',
        'stance_name': 'upright',               # MISSING: fist_on_hip (left)
        'tint': ['helmet', 'bottom', 'strap'],
        'grime': 0.5, 'upper_bulk': 1.25,
    },
    'King Japanese': {
        # a tall civ-color court cap, a wide-shouldered civ-color ankle robe
        # with broad sleeves, a dark front panel with gold edges, a cream V
        # collar, a white belt, gold cuffs and hem; both fists on hips
        'macro': {'muscle': 0.8, 'weight': 0.8, 'height': 0.74, 'proportions': 0.68}, 'age': 0.6,
        'race': {'caucasian': 0.0, 'african': 0.0, 'asian': 1.0},
        'colors': {'skin': '#d0a882', 'hair': '#141010', 'cuirass': '#3a3a40', 'belt': '#ece8e0',
                   'boots': '#e8e4dc', 'collar_w': '#ece8e0', 'gold': '#c8a038'},
        'helmet': 'cap', 'metal': [],           # MISSING: eboshi (tall court cap with upright tail)
        'hair': 'short', 'hair_asset': None, 'beard': False,
        'top': 'sleeved', 'wide_sleeves': True, 'neck': 'high',
        'bottom': 'kilt', 'robe_drop': 0.48,    # MISSING: hem_band (gold)
        'cuirass': True,    # the dark front panel; MISSING: panel_edges (gold)
        'bands': [('neck', 'collar_w'), ('wrist', 'gold')],
        'cape': False, 'wraps': 'boots', 'sandals': False, 'bracers': False, 'gear': [],   # MISSING: low_shoes
        'weapon': None, 'offhand': None,
        'stance_name': 'fists_on_hips',
        'tint': ['helmet', 'top', 'bottom', 'sleeves'],
        'grime': 0.3, 'upper_bulk': 1.15,
    },
    'King Korean': {
        # Wang Kon: shaved head, a golden-yellow ankle robe with wide sleeves,
        # civ-color collar, front stripes and sleeve edges; both hands
        # together at the chest holding a small dark tablet
        'macro': {'muscle': 0.75, 'weight': 0.75, 'height': 0.74, 'proportions': 0.7}, 'age': 0.6,
        'race': {'caucasian': 0.0, 'african': 0.0, 'asian': 1.0},
        'colors': {'skin': '#c8a68a', 'hair': '#141010', 'top': '#d0b050', 'bottom': '#d0b050',
                   'belt': '#8a6a2a', 'boots': '#3a2a1e', 'book_cover': '#2a1c14'},
        'helmet': None, 'hair': 'bald', 'hair_asset': None, 'beard': False,
        'top': 'sleeved', 'wide_sleeves': True, 'neck': 'high',
        'bottom': 'kilt', 'robe_drop': 0.48,    # MISSING: robe_stripes (tinted, down the front edges and sides)
        'bands': [('neck', 'tint'), ('wrist', 'tint')],   # collar and sleeve edges
        'cape': False, 'cuirass': False, 'wraps': 'boots', 'sandals': False, 'bracers': False, 'gear': [],
        'weapon': None, 'offhand': None,        # MISSING: tablet (held in both hands at the chest)
        'stance_name': 'hands_clasped',
        'tint': [],
        'grime': 0.3, 'upper_bulk': 1.05,
    },
    'King Mayan': {
        # Pacal: a tall headdress with a tinted crest and a long cascade of
        # white feathers down the back, gold collar and arm bands, a spotted
        # jaguar-skin kilt, a civ-color cape to the ankles; arms folded, the
        # right hand holding a small gold object
        'macro': {'muscle': 0.85, 'weight': 0.7, 'height': 0.74, 'proportions': 0.7}, 'age': 0.5,
        'race': {'caucasian': 0.0, 'african': 0.15, 'asian': 0.85},
        'colors': {'skin': '#8e6a4e', 'hair': '#141010', 'helmet': '#c8a038', 'bottom': '#b88a48',
                   'belt': '#c8a038', 'sandal': '#5a3a22', 'gold': '#c8a038'},
        'helmet': 'headband', 'plume': True,    # MISSING: feather_headdress (tinted crest, white feather cascade to the waist)
        'hair': 'short', 'hair_asset': 'bob01', 'beard': False,   # MISSING: face_ornaments (gold)
        'top': None, 'bottom': 'kilt', 'robe_drop': 0.0,   # MISSING: jaguar_spots
        'bands': [('neck', 'gold'), ('upperarm', 'gold'), ('wrist', 'gold')],   # MISSING: broad_collar
        'cape': True, 'cape_length': 1.3,
        'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False, 'gear': [],
        'weapon': None, 'offhand': None,        # MISSING: small_object (gold, in the right hand)
        'stance_name': 'arms_folded',
        'tint': ['cape', 'plume'],
        'grime': 0.45, 'upper_bulk': 1.2,
    },
    'King Mongol': {
        # Genghis: a grey fur-brimmed hat with a tinted pointed crown, dark
        # beard, a long ochre deel to mid-shin, a tinted chest panel with gold
        # edges, brown boots; a scimitar low beside the thigh
        'macro': {'muscle': 0.95, 'weight': 0.9, 'height': 0.76, 'proportions': 0.66}, 'age': 0.6,
        'race': {'caucasian': 0.05, 'african': 0.0, 'asian': 0.95},
        'colors': {'skin': '#b88a60', 'hair': '#241a12', 'top': '#b88838', 'bottom': '#b88838',
                   'belt': '#3a2a1c', 'boots': '#3a2a1e', 'steel': '#b0b4b8', 'gold': '#c8a038',
                   'leather': '#3a2a1c'},
        'helmet': 'cap', 'metal': [],           # MISSING: fur_hat (grey fur brim, pointed tinted crown)
        'hair': 'short', 'hair_asset': None,
        'beard': True,                          # MISSING: goatee (and long mustache)
        'top': 'sleeved', 'neck': 'high', 'bottom': 'kilt', 'robe_drop': 0.22,
        'cuirass': True,    # the tinted chest panel; MISSING: panel_edges (gold)
        'wraps': 'boots',
        'cape': False, 'sandals': False, 'bracers': False, 'gear': [],
        'weapon': 'sword', 'props': {'sword': ('scimitar', {'length': 0.85})},
        'stance_name': 'hang',
        'tint': ['helmet', 'cuirass'],
        'grime': 0.55, 'upper_bulk': 1.3,
    },
    'King Netherlands': {
        # William of Orange: a long curly dark periwig, white-silver doublet,
        # tinted short puffed breeches, white hose, an enormous civ-color
        # ermine-dotted robe with a train on the ground and a dark lining;
        # left hand on a planted cane, right fist on hip
        'macro': {'muscle': 0.75, 'weight': 0.75, 'height': 0.78, 'proportions': 0.72}, 'age': 0.55,
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#e0b89a', 'hair': '#5a3a20', 'top': '#e4e0d8', 'belt': '#c8a038',
                   'wraps': '#ece8e0', 'wraps_band': '#d0ccc4', 'sandal': '#1e1a18', 'wood': '#2a1e16',
                   'lacquer': '#2a1e16', 'gold': '#c8a038'},
        'helmet': None, 'hair': 'long', 'hair_asset': 'long01',   # MISSING: periwig (curly, to the shoulders)
        'beard': False,
        'top': 'sleeved', 'neck': 'high',
        'bottom': 'breeches', 'puffy': True, 'wraps': 'wraps', 'sandals': True,   # MISSING: low_shoes
        # MISSING: ermine_trim (dots on the cape), cape_train (spread on the ground), cape_lining (dark purple)
        'cape': True, 'cape_length': 1.45,
        'cuirass': False, 'bracers': False, 'gear': [],
        # the gold-topped cane planted from the fist; MISSING: left_hand_hold (the note has it in the left)
        'weapon': 'cane', 'props': {'cane': ('cane', {'length': 0.95})},
        'stance_name': 'upright',               # MISSING: fist_on_hip (right)
        'tint': ['cape', 'bottom'],
        'grime': 0.35, 'upper_bulk': 1.1,
    },
    'King Ottoman': {
        # Suleiman: a large round white turban with a tinted aigrette and
        # jewel, dark beard, a white ankle kaftan, a tinted waist-length vest
        # with gold edges, a gold belt, brown shoes; hands together at the waist
        'macro': {'muscle': 0.8, 'weight': 0.85, 'height': 0.78, 'proportions': 0.7}, 'age': 0.6,
        'race': {'caucasian': 0.8, 'african': 0.1, 'asian': 0.1},
        'colors': {'skin': '#c89a74', 'hair': '#241a12', 'helmet': '#f0ece2', 'top': '#ece8de',
                   'bottom': '#ece8de', 'belt': '#c8a038', 'boots': '#6a4a2a', 'gold': '#c8a038'},
        'helmet': 'cap', 'metal': [], 'plume': True,   # MISSING: turban (large, round), aigrette (jewel)
        'hair': 'short', 'hair_asset': None, 'beard': True,
        'top': 'sleeved', 'neck': 'high',
        'bottom': 'kilt', 'robe_drop': 0.5,     # MISSING: hem_band (tinted), side slits
        'cuirass': True,    # the tinted waist-length vest; MISSING: vest (sleeveless over-garment, gold edges)
        'cape': False, 'wraps': 'boots', 'sandals': False, 'bracers': False, 'gear': [],   # MISSING: low_shoes
        'weapon': None, 'offhand': None,
        'stance_name': 'hands_clasped',
        'tint': ['cuirass', 'plume'],
        'grime': 0.3, 'upper_bulk': 1.15,
    },
    'King Persian': {
        # Xerxes: a spiked gold crown, long wavy brown hair and beard, a short-
        # sleeved white tunic to mid-thigh with a tinted chest band and hem,
        # gold bracers, tinted leg bands, brown boots; arms hanging
        'macro': {'muscle': 0.9, 'weight': 0.8, 'height': 0.8, 'proportions': 0.7}, 'age': 0.5,
        'race': {'caucasian': 0.8, 'african': 0.1, 'asian': 0.1},
        'colors': {'skin': '#b08664', 'hair': '#4a3424', 'helmet': '#c8a038', 'top': '#e8e4da',
                   'bottom': '#e8e4da', 'belt': '#c8a038', 'boots': '#5a3a22', 'bracers': '#c8a038',
                   'gold': '#c8a038'},
        'helmet': 'crown', 'crown': 'spiked',
        'hair': 'long', 'hair_asset': 'long01', 'beard': True,   # MISSING: long_beard (wavy, square-cut)
        'top': 'tunic', 'bottom': 'skirt', 'robe_drop': 0.0,   # MISSING: hem_band (tinted pattern)
        'gear': [('chest_strap', {})],          # the tinted diagonal chest band; MISSING: pattern
        'bracers': True,
        'bands': [('knee', 'tint'), ('ankle', 'tint')],   # MISSING: leg_wraps (tinted bands round the shins)
        'wraps': 'boots',
        'cape': False, 'cuirass': False, 'sandals': False,
        'weapon': None, 'offhand': None,
        'stance_name': 'hang',
        'tint': ['strap'],
        'grime': 0.45, 'upper_bulk': 1.25,
    },
    'King Portugal': {
        # Henry the Navigator: a wide round-brimmed dark hat with a gold edge,
        # long dark hair and grey beard, a civ-color doublet with puffed
        # shoulders and gold trim, grey hose with gold garters, black shoes;
        # a short gold-hilted sword low and forward, left forearm to the chest
        'macro': {'muscle': 0.8, 'weight': 0.75, 'height': 0.76, 'proportions': 0.72}, 'age': 0.65,
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#d0aa8e', 'hair': '#3a2a1c', 'helmet': '#4a3020', 'bottom': '#9a9aa2',
                   'boots': '#18161a', 'belt': '#c8a038', 'gold': '#c8a038', 'steel': '#b0b4b8',
                   'leather': '#2a1e16'},
        'helmet': 'cap', 'metal': [],           # MISSING: wide_brim_hat (round brim, gold edge)
        'hair': 'long', 'hair_asset': 'long01', 'beard': True,   # MISSING: grey beard with dark hair (one hair color)
        'top': 'sleeved', 'puffy': True, 'neck': 'high',   # MISSING: doublet_trim (gold buttons and edges)
        'bottom': 'long', 'wraps': 'boots',     # MISSING: hose, low_shoes
        'bands': [('wrist', 'gold'), ('knee', 'gold')],   # gold cuffs and garters
        'cape': False, 'cuirass': False, 'sandals': False, 'bracers': False, 'gear': [],
        'weapon': 'sword', 'props': {'sword': ('sword', {'length': 0.62, 'guard': 0.16})},
        'stance_name': 'hang',                  # MISSING: hand_to_chest (left forearm raised)
        'tint': ['top', 'sleeves'],
        'grime': 0.4, 'upper_bulk': 1.15,
    },
    'King Roman': {
        # Caesar: bareheaded, short brown hair, a gold muscle cuirass with
        # tinted panels, a short white tunic and pale strip skirt, laced
        # brown boots; a civ-color cape over the left shoulder to the calves;
        # a tall gold eagle-topped staff upright at his right
        'macro': {'muscle': 0.9, 'weight': 0.75, 'height': 0.78, 'proportions': 0.72}, 'age': 0.6,
        'race': {'caucasian': 0.9, 'african': 0.05, 'asian': 0.05},
        'colors': {'skin': '#c8987a', 'hair': '#3a2a1c', 'cuirass': '#c8a040', 'top': '#ece8e0',
                   'bottom': '#d8d6d0', 'belt': '#6a4a2a', 'boots': '#6a4a2a', 'wood': '#b8902e',
                   'leather': '#b8902e'},
        'helmet': None, 'hair': 'short', 'hair_asset': 'short02', 'beard': False,
        'cuirass': True, 'metal': ['cuirass'], 'neck': None,   # MISSING: cuirass_panels (tinted at chest and shoulders)
        'top': 'tunic',
        'bottom': 'loincloth', 'loin_length': (0.42, 0.4), 'torn': False,   # MISSING: pteruges (strip skirt)
        'wraps': 'boots',
        'cape': True, 'cape_length': 1.05,      # MISSING: cape_drape (over the left shoulder, wrapping the arm)
        'sandals': False, 'bracers': False, 'gear': [],
        # the gold eagle-topped staff
        'weapon': 'standard', 'props': {'standard': ('eagle_standard', {'length': 2.2, 'grip': 1.1})},
        'stance_name': 'upright',
        'tint': ['cape'],
        'grime': 0.45, 'upper_bulk': 1.25,
    },
    'King Russian': {
        # Catherine: a small silver-and-gold crown on brown hair in a bun, a
        # civ-color bodice with a gold front panel and lace, puffy grey-white
        # sleeves, a vast civ-color hoop skirt under silver brocade; a short
        # gold scepter raised by the shoulder, an orb at the lap
        'macro': {'muscle': 0.45, 'weight': 0.7, 'height': 0.72, 'proportions': 0.75}, 'age': 0.65,
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#e4c8b4', 'hair': '#6a4a2c', 'helmet': '#b8bcc4', 'sleeves': '#c8ccd4',
                   'belt': '#c8a038', 'gold': '#c8a038', 'wood': '#b8902e', 'leather': '#b8902e'},
        'gender': 0.0, 'helmet': 'crown', 'crown': 'arched',   # MISSING: crown_silver (silver-and-gold)
        'hair': 'short', 'hair_asset': 'bob01', 'beard': False,   # MISSING: updo (bun)
        'top': 'sleeved', 'puffy': True, 'neck': 'high',
        'bottom': 'gown', 'gown_flare': 2.0, 'robe_drop': 0.58,   # MISSING: brocade (silver), lace_hem
        'cape': False, 'cuirass': False, 'wraps': None, 'sandals': False, 'bracers': False, 'gear': [],
        # the short gold scepter raised by the shoulder; MISSING: left_hand_hold (props.orb at the lap)
        'weapon': 'scepter', 'props': {'scepter': ('scepter', {'length': 0.6, 'head': 'fleur'})},
        'stance_name': 'royal',
        'tint': ['top', 'bottom'],
        'grime': 0.3, 'upper_bulk': 1.0,
    },
    'King Spanish': {
        # Isabella: a tall gold open crown over a civ-color hood/wimple, a
        # long white veil-cape to the floor, a fitted civ-color gown with gold
        # fleurs spilling on the ground; left hand on hip, a gold scepter low
        # and forward in the right
        'macro': {'muscle': 0.4, 'weight': 0.45, 'height': 0.78, 'proportions': 0.8}, 'age': 0.55,
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#e4c8b4', 'hair': '#8a5a2a', 'cape': '#e8e8e8', 'belt': '#c8a038',
                   'gold': '#c8a038', 'wood': '#b8902e', 'leather': '#b8902e'},
        'gender': 0.0, 'helmet': 'hood',        # the tinted wimple; MISSING: crown (tall gold, open) over it
        'hair': 'short', 'hair_asset': None, 'beard': False,
        'top': 'sleeved', 'neck': 'high',
        'bottom': 'gown', 'robe_drop': 0.6,     # MISSING: gown_hem (floor-spilling, tinted edge), fleur_pattern (gold)
        'cape': True, 'cape_length': 1.42,      # the white veil-cape, open at the front
        'cuirass': False, 'wraps': None, 'sandals': False, 'bracers': False, 'gear': [],
        # the gold fleur-headed scepter, tipped forward
        'weapon': 'scepter', 'props': {'scepter': ('scepter', {'length': 0.7, 'head': 'fleur'})},
        'stance_name': 'royal', 'upright_dir': (0.0, -0.7, 0.7),   # MISSING: held_low_forward, fist_on_hip (left)
        'tint': ['helmet', 'top', 'bottom'],
        'grime': 0.3, 'upper_bulk': 1.0,
    },
    'King Sumeria': {
        # Gilgamesh: a tall white conical crown with a gold band, long black
        # hair and square beard, a cream shawl over chest and one shoulder
        # with tinted cap sleeves and a red waist band, a long civ-color skirt
        # with gold-black hem stripes; a long rod held across the body
        'macro': {'muscle': 0.95, 'weight': 0.85, 'height': 0.78, 'proportions': 0.66}, 'age': 0.55,
        'race': {'caucasian': 0.7, 'african': 0.2, 'asian': 0.1},
        'colors': {'skin': '#9a7456', 'hair': '#141010', 'helmet': '#e4dccc', 'top': '#e4dccc',
                   'belt': '#8a3226', 'redband': '#8a3226', 'sandal': '#4a3622', 'wood': '#5a3e24'},
        'helmet': 'cap', 'metal': [],           # MISSING: tall_crown (white cone, gold band and top)
        'hair': 'long', 'hair_asset': 'long01', 'mane': 0.15,
        'beard': True,                          # MISSING: long_beard (square, black)
        'top': 'mantle', 'neck': 'high',        # the cream shawl; MISSING: shawl_pattern (red/brown), tinted cap sleeves
        'bottom': 'kilt', 'robe_drop': 0.46,    # MISSING: hem_band (gold-black stripes, red border)
        'bands': [('waist', 'redband')],
        'cape': False, 'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False, 'gear': [],
        'weapon': 'staff', 'props': {'staff': ('staff', {'length': 1.6, 'grip': 0.6})},
        'stance_name': 'low_across',            # MISSING: rod_level (held horizontally across the chest, tip out)
        'tint': ['bottom'],
        'grime': 0.5, 'upper_bulk': 1.3,
    },
    'King Viking': {
        # Ragnar: a civ-color round helmet with gold rim and nasal, long
        # reddish-blond hair and beard, a grey mail tunic with gold belt and
        # collar, brown baggy trousers, wrapped boots, an ankle-length civ-
        # color cape; a long sword point-down behind the leg
        'macro': {'muscle': 1.0, 'weight': 0.9, 'height': 0.82, 'proportions': 0.65}, 'age': 0.55,
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#d0a888', 'hair': '#b07840', 'top': '#6e7278', 'mail': '#6e7278',
                   'bottom': '#5a4630', 'belt': '#c8a038', 'boots': '#4a3a2c', 'gold': '#c8a038',
                   'steel': '#a8acb0', 'leather': '#3a2818'},
        'helmet': 'cap', 'metal': ['helmet'],   # MISSING: helmet_trim (gold rim and nasal strip)
        'hair': 'long', 'hair_asset': 'long01', 'mane': 0.2, 'beard': True,
        'top': 'sleeved', 'neck': 'high', 'collar': 'mail',   # MISSING: mail_shirt (rings on the whole tunic)
        'bottom': 'long', 'puffy': True, 'wraps': 'boots',
        'bands': [('neck', 'gold')],
        'cape': True, 'cape_length': 1.3,
        'cuirass': False, 'sandals': False, 'bracers': False, 'gear': [],
        'weapon': 'sword', 'props': {'sword': ('long_sword', {'length': 0.95})},
        'stance_name': 'hang',                  # MISSING: sword_point_down_back
        'tint': ['helmet', 'cape'],
        'grime': 0.55, 'upper_bulk': 1.35,
    },
    'King Zulu': {
        # Shaka: a tan feather/fur headdress fanned round the head, bare dark
        # torso with a fur shoulder cape (tinted edge), a tan fur kilt with
        # tinted bands, tinted armlets and below-knee bands; a knobkerrie
        # forward-down and a tall cowhide shield with tinted stripes
        'macro': {'muscle': 1.0, 'weight': 0.8, 'height': 0.82, 'proportions': 0.66}, 'age': 0.45,
        'race': {'caucasian': 0.0, 'african': 1.0, 'asian': 0.0},
        'colors': {'skin': '#5a3e2a', 'hair': '#141010', 'helmet': '#c4ac78', 'plume': '#ece4cc',
                   'bottom': '#c4ac78', 'cape': '#b89c68', 'belt': '#6a5032', 'wood': '#b89a6a',
                   'stone': '#b89a6a', 'leather': '#6a5032', 'hide': '#ece8e0', 'hide_dark': '#2a1e16'},
        'helmet': 'headband', 'plume': True,    # MISSING: feather_headdress (fanned round the head)
        'hair': 'short', 'hair_asset': 'afro01', 'beard': False,
        'top': None,
        'bottom': 'kilt', 'robe_drop': 0.0,     # MISSING: fur_garment (kilt and cape of fur, tinted bands)
        'cape': True, 'cape_length': 0.35,      # the shoulder cape; MISSING: fur_garment, cape_edge_tint
        'bands': [('upperarm', 'tint'), ('knee', 'tint')],
        'cuirass': False, 'wraps': None, 'sandals': False, 'bracers': False, 'gear': [],
        # MISSING: knobkerrie (plain wooden knob club; the war club stands in)
        'weapon': 'club', 'props': {'club': ('war_club', {'length': 0.6, 'knob': 0.06, 'studs': 0})},
        # MISSING: tinted_shield_stripes (cowhide_shield has no tint), feather tuft on the stick
        'offhand': 'shield_cowhide', 'offhand_mesh': ('cowhide_shield', {'w': 0.5, 'h': 1.1}),
        'stance_name': 'guard',
        'tint': [],
        'grime': 0.55, 'upper_bulk': 1.35,
    },

    # ------------------------------------------------------------ leaders
    'Leader Ancient Times': {
        # a bald white-bearded patriarch, an open tan robe with wide sleeves
        # over a white gown, a brown sash belt, sandals; a tall wooden
        # shepherd's crook planted at his right
        'macro': {'muscle': 0.7, 'weight': 0.7, 'height': 0.76, 'proportions': 0.7}, 'age': 0.88,
        'colors': {'skin': '#b88a64', 'hair': '#e8e4dc', 'top': '#9a8262', 'bottom': '#9a8262',
                   'belt': '#6a4a2e', 'sandal': '#4a3020', 'wood': '#7a5a3a', 'leather': '#5a3e24',
                   'gold': '#7a5a3a', 'lapis': '#7a5a3a'},
        'helmet': None, 'hair': 'bald', 'hair_asset': None, 'beard': True,   # MISSING: long_beard (white, to the chest)
        'top': 'sleeved', 'wide_sleeves': True, 'neck': 'high',
        'bottom': 'kilt', 'robe_drop': 0.46,    # MISSING: undergown (white), lapel_trim (tinted), hem_band x2 (tinted)
        'bands': [('wrist', 'tint')],
        'cape': False, 'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False, 'gear': [],
        # the shepherd's crook, wooden (gold/lapis recolored); MISSING: crook extras (a grip: the rod reaches only
        # 0.2 below the fist, so it can't be planted on the ground)
        'weapon': 'crook', 'props': {'crook': ('crook', {'length': 1.4})},
        'stance_name': 'upright',
        'tint': [],
        'grime': 0.5, 'upper_bulk': 1.05,
    },
    'Leader Industrial Ages': {
        # Washington/Frederick: a white powdered wig with a queue, a civ-
        # color knee-length frock coat with gold epaulettes and collar, grey
        # waistcoat and breeches, white cuffs, black knee boots; one hand on
        # the hip, the other holding a small dark object at the waist
        'macro': {'muscle': 0.8, 'weight': 0.8, 'height': 0.82, 'proportions': 0.72}, 'age': 0.65,
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#e0b89a', 'hair': '#e8e4dc', 'bottom': '#a0a2a8', 'boots': '#141416',
                   'belt': '#a0a2a8', 'gold': '#c8a038', 'cuff': '#ece8e0'},
        'helmet': None, 'hair': 'topknot', 'hair_asset': 'ponytail01', 'beard': False,   # MISSING: powdered_wig (side rolls)
        'top': 'sleeved', 'neck': 'high',       # MISSING: frock_coat, epaulettes (gold), waistcoat (grey)
        'bottom': 'breeches', 'wraps': 'boots',
        'bands': [('neck', 'gold'), ('wrist', 'cuff')],
        'cape': False, 'cuirass': False, 'sandals': False, 'bracers': False, 'gear': [],
        'weapon': None, 'offhand': None,        # MISSING: tricorne_in_hand (small dark object at the waist)
        'stance_name': 'hang',                  # MISSING: fist_on_hip
        'tint': ['top'],
        'grime': 0.35, 'upper_bulk': 1.15,
    },
    'Leader Middle Ages': {
        # an armored king: gold crown, dark beard, full plate, a civ-color
        # surcoat on the chest, a tan cloth skirt, a full-width civ-color cape
        # with ermine collar and hem, tan bands and a gold lion; a longsword
        # point down at his side
        'macro': {'muscle': 0.95, 'weight': 0.85, 'height': 0.82, 'proportions': 0.68}, 'age': 0.6,
        'race': {'caucasian': 0.9, 'african': 0.05, 'asian': 0.05},
        'colors': {'skin': '#d4aa8a', 'hair': '#4a2e18', 'helmet': '#c8a038', 'top': '#a8acb4',
                   'bottom': '#b09a6a', 'greaves': '#a8acb4', 'belt': '#3a2a1c', 'steel': '#b8bcc2',
                   'mail': '#7a7c80', 'leather': '#3a2a1c', 'gold': '#c8a038'},
        'helmet': 'crown', 'crown': 'spiked',
        'hair': 'short', 'hair_asset': 'short02', 'beard': True,
        'cuirass': True, 'metal': [], 'neck': 'high',   # the tinted surcoat over the plate
        'top': 'sleeved', 'pauldrons': True, 'collar': 'mail',   # MISSING: plate_arms, plate_legs
        'bottom': 'kilt', 'robe_drop': 0.0, 'wraps': 'greaves',
        'cape': True, 'cape_length': 1.38,      # MISSING: ermine_trim (collar and hem), cape_emblem (gold lion), cape bands (tan)
        'sandals': False, 'bracers': False, 'gear': [],
        'weapon': 'sword', 'props': {'sword': ('long_sword', {'length': 0.95})},
        'stance_name': 'hang',
        'tint': ['cuirass', 'cape'],
        'grime': 0.45, 'upper_bulk': 1.3,
    },
    'Leader Modern Times': {
        # a general: green peaked cap, goatee, olive tunic, a broad gold-edged
        # civ-color sash shoulder to hip, tinted cuffs and shoulder patch,
        # khaki breeches, tall brown boots; a swagger stick level at the chest
        'macro': {'muscle': 0.8, 'weight': 0.75, 'height': 0.78, 'proportions': 0.72}, 'age': 0.6,
        'colors': {'skin': '#c8a48a', 'hair': '#2a1e14', 'helmet': '#4a5038', 'top': '#545a40',
                   'bottom': '#9a907a', 'boots': '#4e3828', 'belt': '#3a2a1c'},
        'helmet': 'cap', 'metal': [],           # MISSING: peaked_cap (visor)
        'hair': 'short', 'hair_asset': None,
        'beard': True,                          # MISSING: goatee
        'top': 'sleeved', 'neck': 'high', 'bottom': 'breeches', 'wraps': 'boots',
        'gear': [('chest_strap', {})],          # MISSING: broad_sash (gold-edged), shoulder_patch (tinted)
        'bands': [('wrist', 'tint')],
        'cape': False, 'cuirass': False, 'sandals': False, 'bracers': False,
        'weapon': 'stick', 'props': {'stick': ('swagger_stick', {'length': 0.6})}, 'offhand': None,   # level, forward
        'stance_name': 'royal', 'upright_dir': (0.0, -1.0, 0.1),
        'tint': ['strap'],
        'grime': 0.4, 'upper_bulk': 1.15,
    },
    'Princess': {
        # slender, brown hair in an updo, a tinted choker, bare shoulders,
        # long tinted gloves, a tinted bodice, a floor-length white gown with
        # a gold V waist and gold stripes; a short tinted scepter raised
        # upright to the shoulder
        'macro': {'muscle': 0.35, 'weight': 0.35, 'height': 0.72, 'proportions': 0.82}, 'age': 0.4,
        'race': {'caucasian': 0.8, 'african': 0.1, 'asian': 0.1},
        'colors': {'skin': '#e8c4ac', 'hair': '#6a4224', 'bottom': '#f2f0ea', 'belt': '#c8a038',
                   'gold': '#c8a038', 'wood': '#b8902e', 'leather': '#b8902e'},
        'gender': 0.0, 'helmet': None, 'hair': 'short', 'hair_asset': 'bob01', 'beard': False,   # MISSING: updo
        'top': 'vest', 'top_zmax': 1.4,         # the tinted bodice, shoulders bare
        'bottom': 'gown', 'robe_drop': 0.56,    # MISSING: gown_pattern (gold stripes, tinted hem band and spots)
        'bracers': True,                        # the long tinted gloves; MISSING: long_gloves (to the elbow, over the hands)
        'bands': [('neck', 'tint')],            # the choker
        'cape': False, 'cuirass': False, 'wraps': None, 'sandals': False, 'gear': [],
        # the short scepter raised; MISSING: scepter_tint (props.scepter is gold only), left_hand_hold
        'weapon': 'scepter', 'props': {'scepter': ('scepter', {'length': 0.5, 'head': 'orb'})},
        'stance_name': 'royal',
        'tint': ['top', 'bracers'],
        'grime': 0.3, 'upper_bulk': 0.9,
    },
    'SciLeader': {
        # Einstein: wild white hair and a big white mustache, a rumpled dark
        # grey suit, a tinted tie, a thin black cane planted in the right
        # hand, a brown doctor's bag (tinted straps) low in the left, a pipe;
        # slightly stooped
        'macro': {'muscle': 0.55, 'weight': 0.6, 'height': 0.72, 'proportions': 0.72}, 'age': 0.85,
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#d8ae8e', 'hair': '#e4e4e4', 'top': '#5a5a5e', 'bottom': '#3a3a3e',
                   'boots': '#2a2420', 'belt': '#2a2a2e', 'wood': '#18181a', 'leather': '#18181a',
                   'lacquer': '#18181a', 'gold': '#3a3a3e'},
        'helmet': None, 'hair': 'long', 'hair_asset': 'short04', 'mane': 0.08,   # MISSING: wild_hair
        'beard': False,                         # MISSING: mustache (big, white)
        'top': 'sleeved', 'neck': 'high', 'bottom': 'long', 'wraps': 'boots',   # MISSING: suit_jacket (open, lapels), tie (tinted), low_shoes
        'bands': [('neck', 'tint')],            # a tinted collar stands in for the tie
        'cape': False, 'cuirass': False, 'sandals': False, 'bracers': False, 'gear': [],
        # the thin black cane planted from the fist; MISSING: left_hand_hold (props.briefcase, brown,
        # tinted straps), mouth_prop (props.pipe)
        'weapon': 'cane', 'props': {'cane': ('cane', {'length': 0.9})},
        'stance_name': 'upright',               # MISSING: walking_stoop
        'tint': [],
        'grime': 0.35, 'upper_bulk': 0.95,
    },
    'Shogun': {
        # a civ-color kabuto with a gold crest, a dark grey kataginu vest, a
        # civ-color ankle robe with wide sleeves, a dark front panel, white V
        # collar, gold cuffs and hem, white tabi; a katana raised two-handed
        # over the right shoulder
        'macro': {'muscle': 0.95, 'weight': 0.85, 'height': 0.76, 'proportions': 0.68}, 'age': 0.55,
        'race': {'caucasian': 0.0, 'african': 0.0, 'asian': 1.0},
        'colors': {'skin': '#c4a07e', 'hair': '#141010', 'cuirass': '#3e3c40', 'belt': '#2a2830',
                   'sandal': '#d8d4cc', 'collar_w': '#e0dcd4', 'gold': '#c8a038', 'lacquer': '#1a1412',
                   'iron': '#3a3a3c', 'cloth_wrap': '#2a2630', 'steel': '#b8bcc2'},
        'helmet': 'cap', 'metal': ['helmet'],   # MISSING: kabuto (flared neck guard, gold kuwagata crest)
        'hair': 'short', 'hair_asset': None, 'beard': False,
        'top': 'sleeved', 'wide_sleeves': True, 'neck': 'high',
        'cuirass': True,    # the dark kataginu panel; MISSING: kataginu (winged shoulders)
        'bottom': 'kilt', 'robe_drop': 0.5,     # MISSING: hem_band (gold)
        'bands': [('neck', 'collar_w'), ('wrist', 'gold')],
        'cape': False, 'wraps': None, 'sandals': True, 'bracers': False, 'gear': [],
        'weapon': 'sword', 'props': {'sword': ('katana', {'length': 0.75})},
        'stance_name': 'guard',                 # MISSING: two_hand_raised (katana over the right shoulder)
        'tint': ['helmet', 'top', 'bottom', 'sleeves'],
        'grime': 0.45, 'upper_bulk': 1.3,
    },
    'The King': {
        # Elvis: a black pompadour, sunglasses, a white high-collared
        # jumpsuit with a deep tinted V, gold belt and cuffs, bell-bottoms
        # with tinted inserts, a civ-color cape to the knees; wide-legged,
        # knees bent, arms spread holding the cape open, a microphone
        'macro': {'muscle': 0.75, 'weight': 0.7, 'height': 0.78, 'proportions': 0.74}, 'age': 0.45,
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#e0b89a', 'hair': '#141418', 'top': '#f2f0ec', 'bottom': '#f2f0ec',
                   'belt': '#c8a038', 'boots': '#e8e4dc', 'gold': '#c8a038'},
        'helmet': None, 'hair': 'short', 'hair_asset': 'short04', 'beard': False,   # MISSING: pompadour, face_prop (props.sunglasses)
        'top': 'sleeved', 'neck': 'high',
        'cuirass': True,    # the tinted chest V; MISSING: deep_v (tinted V panels, not a block)
        'bottom': 'long', 'wraps': 'boots',     # MISSING: bell_bottoms (tinted flare inserts)
        'bands': [('wrist', 'gold')],
        'cape': True, 'cape_length': 0.85,
        'sandals': False, 'bracers': False, 'gear': [],
        'weapon': 'microphone', 'props': {'microphone': ('microphone', {})}, 'offhand': None,
        'stance_name': 'royal',                 # MISSING: rock_stance (wide, knees bent, arms out holding the cape)
        'tint': ['cuirass', 'cape'],
        'grime': 0.3, 'upper_bulk': 1.1,
    },
}

# MISSING FEATURES
# What the notes need that the supported keys can't express. Each entry above
# approximates it and marks the spot with "MISSING:". Ranked roughly by how
# many units need it.
#
# -- body
# female hair        English, Russian, Princess: female hair assets (updo/bun).
#
# -- stances and gestures
# fist_on_hip        German, Egyptian, Iroquois, Spanish, Netherlands, Leader Industrial: ONE hand on the hip,
#                    the other free or holding ('fists_on_hips' does both). Today they fall back to 'hang'.
# hands_at_chest     Byzantines, Korean: hands together at the sternum, not the waist ('hands_clasped' stands in).
# two_hand_pole      all four Armies: both fists on the war_standard's pole in front of the chest, one above the
#                    other ('upright' holds it in the right fist only).
# left_hand_hold     a left-hand held-item slot ('offhand' is shields and bows only): Russian (props.orb at the
#                    lap), SciLeader (props.briefcase, low), Netherlands (the cane), Princess (the scepter, raised
#                    to the shoulder), Incan (the feather standard); a pharaoh's royal_flail beside the crook.
#                    Today the item goes in the right hand or is left out.
# walking_stoop      Indian, SciLeader: slight forward stoop with a stride, a cane planted ahead.
# held_low_forward   Spanish (scepter), Egyptian (crook): an item held low and out with its head up; 'royal'
#                    raises the forearm, 'hang' points the head down.
# rod_level          Sumeria: a long rod held horizontally across the chest, tip out to the side.
# hand_to_chest      Portugal: the left forearm raised to the chest.
# sword_point_down_back  Viking: a long sword point-down, angled back behind the leg.
# two_hand_raised    Shogun: a katana in both hands, raised over the right shoulder ('guard' stands in).
# rock_stance        The King: wide legs, bent knees, both arms out at waist height holding the cape open.
# (pointing          no unit needs it: every catalog 'point' gesture is wrong per the notes.)
#
# -- regalia and headgear
# crown extras       Spanish (tall open crown OVER the tinted wimple: a crown on top of a hood), Army Middle Ages
#                    (the dark coif under the crown), Incan (gold front disc), Russian (silver-and-gold),
#                    Byzantines (tinted panels), Babylonian (tinted band).
# tall_crown         Sumeria (white cone, gold band): a cone profile and a non-gold color ('tall' is a gold
#                    flat-topped cylinder).
# feather_headdress  Aztec (tall upright fan, tinted/gold/red), Mayan (tinted crest + white cascade to the waist),
#                    Iroquois (war bonnet: fan with tinted tips and a trailer), Zulu (fanned round the head),
#                    Incan feather_standard: a fan/ring of N feathers with cycling colors and a tint list.
# turban             Arab (large round, tinted), Ottoman (large round white, jewel and tinted aigrette).
# soft hats          top_hat (American, tinted band), bicorne (Army Industrial), beret (Austria, feather laid
#                    sideways), wide_brim_hat (Portugal, gold edge), peaked_cap (Leader Modern), eboshi
#                    (Japanese, tinted), fur_hat (Mongol, grey fur brim, tinted pointed crown).
# military helmets   pickelhaube (German, spike), brodie_helmet (Army Modern), kabuto (Shogun: neck guard, gold
#                    kuwagata), helmet_trim (Viking: gold rim and nasal).
# nemes              Egyptian: a striped tinted headcloth with lappets and a gold uraeus (the 'hood' stands in).
# scepter_tint       Princess: props.scepter is gold only (the note has a tinted scepter).
# crook extras       Leader Ancient: a grip param so a tall crook can be planted (the rod stops 0.2 below the
#                    fist); Egyptian: tinted bands (they're 'lapis').
# flag_device        Army Modern (diagonal cross), any army: device paint (bars, quarters) on the war_standard's
#                    tinted field.
#
# -- garments and fabrics
# ermine_trim        Army Middle Ages, Leader Middle Ages (white fur collar and hem), Netherlands (ermine dots
#                    over the tinted cape): a white fur band with black tails on a cape edge/collar.
# cape_emblem        Army Middle Ages, Leader Middle Ages (gold lion), French (gold fleur-de-lis).
# cape_edge / lining / train / drape  Babylonian (gold edge), Austria (tinted edge), Zulu (tinted edge),
#                    Netherlands (dark lining, train on the ground), Roman (over the left shoulder, wrapping the
#                    arm), French (tinted shoulder mantle), Aztec (gold top, fringed panels, red block), Incan
#                    (white zig-zag border, gold band).
# gown extras        Spanish (a hem that pools on the ground, tinted edge), English (farthingale gold stripes and
#                    jewel dots), Princess (gold stripes, tinted hem band), a front panel part.
# hem_band           Army Ancient, Arab, Babylonian, Hatti, Japanese, Ottoman, Persian, Sumeria, Shogun, Leader
#                    Ancient: a band (tinted or gold) at the robe/skirt hem, following the hem.
# frock_coat         Army Industrial, American, German, Leader Industrial: a coat with knee-length skirts over
#                    trousers (two bottom layers), with epaulettes (gold) on the shoulders.
# vest / panel_edges Ottoman, Mongol, Japanese, Shogun (kataginu with winged shoulders), Leader Industrial: a
#                    sleeveless over-garment with gold edging (a 'cuirass' stands in, without edges).
# undergown / lapel_trim  Army Ancient, Leader Ancient: an open robe front showing a white gown, tinted lapels.
# low_shoes          American, Austria, Chinese, Japanese, Netherlands, Ottoman, Portugal, SciLeader: shoes to
#                    the ankle (boots reach mid-shin; sandals are strapped).
# fabric materials   velvet, brocade (Austria, Russian), tartan (Celtic), checkerboard (Incan), jaguar_spots
#                    (Mayan), fleur_pattern (Spanish), pleats (Egyptian): a pattern/sheen param on a garment part.
# fur_garment        Zulu (kilt and shoulder cape of fur, tinted bands).
# plate_arms / plate_legs  French, Army Middle, Leader Middle: metal (not cloth) arm and leg plates.
# scale_armor / mail_shirt  Carthaginian (scales, scaled back cape), Viking (mail over the whole tunic).
# trouser_stripes    Carthaginian (tinted, down each side); bell_bottoms (The King, tinted inserts).
# broad_collar       Aztec, Byzantines, Egyptian, Mayan: a gold pectoral collar (a neck band stands in).
# ruff               English: a tall white lace fan behind the head.
# shoulder_mantle    Hatti: a tinted shawl over cream sleeves (the 'mantle' top tints the whole torso).
# himation / dhoti / pteruges / long_gloves / tie  Greek, Indian, Roman, Princess, SciLeader.
# broad_sash         Army Modern, Leader Modern: a wide gold-edged sash (chest_strap is narrow, no edges).
#
# -- faces and hair
# long_beard / chin_beard / goatee / mustache  Babylonian, Sumeria, Persian, Leader Ancient (long); American
#                    (chin); Mongol, Leader Modern (goatee); Celtic, German, SciLeader (mustache only).
# updo / periwig / powdered_wig / pompadour / wild_hair / curly_hair  English, Russian, Princess; Netherlands;
#                    Leader Industrial; The King; SciLeader; Greek.
# face_prop          Indian (props.glasses 'round'), The King (props.sunglasses): a slot placing a prop on the
#                    face at the bridge of the nose.
# mouth_prop         SciLeader: props.pipe in the mouth.
#
# -- props and companions
# small_object       Byzantines (white), Mayan (gold), Korean (dark tablet): a small item held at the chest.
# back_scabbard      Celtic: a long sword sheathed on the back, gold hilt over the shoulder.
# briefcase_tint     SciLeader: tinted straps on props.briefcase (and it needs left_hand_hold).
# tinted_shield_stripes  Zulu: cowhide_shield with tinted horizontal stripes and a feather tuft on the stick.
# feathered_spear    Iroquois: fur/feather wrap below the spear head.
# knobkerrie         Zulu: a plain wooden knob club (war_club with studs=0 stands in).
# companion_dog      Austria: a grey wolfhound at his side, tinted collar.
# tricorne_in_hand   Leader Industrial: a small dark hat/pistol held at the waist.
