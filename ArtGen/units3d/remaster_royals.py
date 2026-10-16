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
  eagle_standard, staffs and canes held in 'stand_staff' (the butt toward
  the ground; the armies' standards with 'two_hands': True), scepter, crook,
  swagger_stick and microphone held up in the 'royal' stance (tilted with
  'upright_dir'); left-hand items stay MISSING;
- women are 'gender': 0.0 with a 'gown' bottom ('gown_flare', 'robe_drop');
- royal poses are the stances 'fists_on_hips', 'hands_clasped' (also for
  hands together at the chest), 'hands_behind', 'royal' (a scepter held
  up in the right hand), 'stand' (arms relaxed, an item hanging from the
  right hand) and 'stand_staff'; one fist on the hip still falls back to 'stand'.
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
        'colors': {'skin': '#b4845e', 'hair': '#8a7a66', 'top': '#9a8262', 'bottom': '#9a8262',
                   'belt': '#4a3424', 'sandal': '#4a3020', 'wood': '#5e4228', 'leather': '#3e2a1a'},
        'hair': 'short', 'hair_asset': 'short02', 'beard': True, 'helmet': None,
        'top': 'sleeved', 'wide_sleeves': True, 'neck': 'high',
        'bottom': 'kilt', 'robe_drop': 0.45,   # MISSING: undergown (cream gown showing down the open front)
        'bands': [('wrist', 'tint', 0.06)],     # the civ-color sleeve cuffs; MISSING: sleeve_cuff (band at the wide sleeve's end)
        'hem_bands': [('bottom', 'tint', 0.04, 0.05), ('bottom', 'tint', 0.04, 0.15)],
        # MISSING: lapel_trim (tinted robe front edges)
        'cape': False, 'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False, 'gear': [],
        # the crossbar-hung vexillum, tinted field; MISSING: flag_device (three vertical bars, red/green/cyan)
        'weapon': 'standard', 'props': {'standard': ('war_standard', {'era': 'ancient', 'length': 2.6, 'grip': 1.2})},
        'stance_name': 'stand_staff', 'two_hands': True,
        'tint': ['banner'],
        'grime': 0.55, 'upper_bulk': 1.1,
    },
    'Army Industrial Ages': {
        # an officer in a black bicorne and a civ-color frock coat with gold
        # epaulettes, grey trousers, tall black boots, holding the flagpole
        'macro': {'muscle': 0.8, 'weight': 0.7, 'height': 0.78, 'proportions': 0.72}, 'age': 0.55,
        'race': {'caucasian': 0.9, 'african': 0.05, 'asian': 0.05},
        'colors': {'skin': '#d4a888', 'hair': '#3a2a1c', 'helmet': '#1a1a1e', 'bottom': '#8a8c92',
                   'boots': '#141416', 'belt': '#1a1a1e', 'gold': '#c8a038', 'wood': '#4a3222',
                   'cuirass': '#e8e4dc'},
        'helmet': None, 'headgear': 'bicorne', 'brow_drop': 0.06,
        'hair': 'short', 'hair_asset': 'short02', 'beard': False,
        'top': 'sleeved', 'neck': 'high',       # MISSING: epaulettes (gold; pauldrons would take the tinted top)
        'coat_length': 0.35, 'coat_flare': 1.1,   # the frock coat's skirts
        'cuirass': True, 'metal': [],           # the white waistcoat front (untinted)
        'bottom': 'long', 'wraps': 'boots',
        'bands': [('wrist', 'gold')],           # gold cuffs
        'cape': False, 'sandals': False, 'bracers': False, 'gear': [],
        # the flag flying from the pole top, tinted field; MISSING: flag_device (blob emblems)
        'weapon': 'standard', 'props': {'standard': ('war_standard', {'era': 'industrial', 'length': 2.6, 'grip': 1.2})},
        'stance_name': 'stand_staff', 'two_hands': True,
        'tint': ['top', 'banner'],
        'grime': 0.45, 'upper_bulk': 1.15,
    },
    'Army Middle Ages': {
        # an armored king: gold crown over a dark coif, full plate, a dark
        # tabard skirt, a floor-length civ-color cape with a white fur hem and
        # a gold lion; both hands on the swallow-tailed banner's pole
        'macro': {'muscle': 0.9, 'weight': 0.8, 'height': 0.8, 'proportions': 0.7}, 'age': 0.6,
        'race': {'caucasian': 0.9, 'african': 0.05, 'asian': 0.05},
        'colors': {'skin': '#c49a7e', 'hair': '#4a3a2c', 'helmet': '#3a3836', 'top': '#868b94',
                   'cuirass': '#8e939c', 'greaves': '#868b94', 'bottom': '#4a4238', 'belt': '#2e2418',
                   'boots': '#8e939c', 'mail': '#6a6c70', 'wood': '#4a3222', 'gold': '#c8a038'},
        'helmet': 'crown', 'crown': 'spiked', 'crown_height': 0.06,   # MISSING: coif (dark, under the crown)
        'hair': 'short', 'hair_asset': 'short02', 'beard': True,
        'cuirass': True, 'metal': ['cuirass'], 'top': 'sleeved', 'pauldrons': True, 'collar': 'mail',
        'bottom': 'kilt', 'robe_drop': 0.0, 'wraps': 'greaves', 'boots': True,   # MISSING: plate_legs (cuisses over the thighs)
        'cape': True, 'cape_length': 1.35,      # MISSING: ermine_trim (white fur hem), cape_emblem (gold lion on a gold field)
        # the white fur hem (stand-in) and the two gold bands
        'hem_bands': [('cape', '#ece8e0', 0.08), ('cape', 'gold', 0.03, 0.14), ('cape', 'gold', 0.03, 0.22)],
        'sandals': False, 'bracers': False, 'gear': [],
        # the swallow-tailed, crossbar-hung banner, tinted field; MISSING: flag_device (green/teal quarters)
        'weapon': 'standard', 'props': {'standard': ('war_standard', {'era': 'medieval', 'length': 2.6, 'grip': 1.2})},
        'stance_name': 'stand_staff', 'two_hands': True,
        'tint': ['cape', 'banner'],
        'grime': 0.5, 'upper_bulk': 1.25,
    },
    'Army Modern Times': {
        # a soldier in an olive peaked cap, olive tunic, khaki
        # trousers, brown boots and a broad gold-edged civ-color sash
        'macro': {'muscle': 0.85, 'weight': 0.7, 'height': 0.78, 'proportions': 0.72}, 'age': 0.45,
        'colors': {'skin': '#c89a74', 'hair': '#2a1e14', 'helmet': '#3e4430', 'top': '#545a3c',
                   'bottom': '#9a8c6c', 'boots': '#4e3424', 'belt': '#3a2a1c', 'wood': '#4a3222'},
        # the original wears a peaked cap with a visor (the note's round helmet contradicts it)
        'helmet': None, 'headgear': 'kepi', 'metal': [],   # MISSING: peaked_cap (the kepi stands in)
        'hair': 'short', 'hair_asset': 'short02', 'beard': False,
        'top': 'sleeved', 'neck': 'high', 'bottom': 'long', 'wraps': 'boots',
        'gear': [('chest_strap', {'width': 0.17})],   # the broad tinted sash; MISSING: sash_edges (gold)
        'cape': False, 'cuirass': False, 'sandals': False, 'bracers': False,
        # the big flag, tinted field; MISSING: flag_device (diagonal cross)
        'weapon': 'standard', 'props': {'standard': ('war_standard', {'era': 'modern', 'length': 2.6, 'grip': 1.2})},
        'stance_name': 'stand_staff', 'two_hands': True,
        'tint': ['strap', 'banner'],
        'grime': 0.55, 'upper_bulk': 1.15,
    },

    # ------------------------------------------------------------ kings
    'King American': {
        # Lincoln: tall and gaunt-dignified, a stovepipe hat with a civ-color
        # band, chin beard, black frock coat over a civ-color waistcoat,
        # white collar; hands clasped behind the back
        'macro': {'muscle': 0.7, 'weight': 0.5, 'height': 0.85, 'proportions': 0.78}, 'age': 0.7,
        'race': {'caucasian': 0.95, 'african': 0.05, 'asian': 0.0},
        'colors': {'skin': '#c89070', 'hair': '#241c16', 'helmet': '#141416', 'top': '#1e1c1c',
                   'cuirass': '#2a2826', 'bottom': '#1a1818', 'boots': '#3a2418', 'belt': '#1a1818',
                   'shirt': '#e8e4dc'},
        'helmet': None, 'headgear': 'top_hat',
        'hair': 'short', 'hair_asset': None,
        'beard': True,                          # MISSING: chin_beard (no mustache)
        'top': 'sleeved', 'neck': 'high',
        'coat_length': 0.55, 'coat_flare': 1.05,   # the frock coat's skirts
        # the tinted waistcoat, a narrow strip between the black coat fronts
        'cuirass': True, 'metal': [], 'cuirass_width': 0.07,
        'bands': [('neck', 'shirt', 0.03)],     # the white shirt collar
        'bottom': 'long', 'wraps': 'boots',     # MISSING: low_shoes
        'cape': False, 'sandals': False, 'bracers': False, 'gear': [],
        'weapon': None, 'offhand': None,
        # MISSING: hand_to_chest (the original's right hand is raised to the lapel; the note says hands behind)
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
        'helmet': None, 'headgear': 'turban', 'brow_drop': 0.06, 'headgear_scale': 0.9,
        'hair': 'short', 'hair_asset': None, 'beard': True,
        'top': 'sleeved', 'wide_sleeves': True, 'neck': 'high',
        'bottom': 'kilt', 'robe_drop': 0.48,    # MISSING: front_trim (tinted)
        'hem_bands': [('bottom', 'tint', 0.05)],
        'gear': [('chest_strap', {'width': 0.12})],   # the diagonal baldric; MISSING: strap_stripe (gold stripe along it)
        'bands': [('wrist', 'tint', 0.06)],     # the tinted sleeve cuffs; MISSING: sleeve_cuff (band at the wide sleeve's end)
        'cape': False, 'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False,
        'weapon': None, 'offhand': None,
        'stance_name': 'arms_folded',
        'tint': ['helmet', 'strap'],
        'grime': 0.4, 'upper_bulk': 1.15,
    },
    'King Austria': {
        # Maximilian-era prince: a dark low hat with a white feather, beard,
        # puffed civ-color sleeves, a quilted brown vest, a dark brown cape to
        # the knees (as the original; the note's short tan cape contradicts
        # it), brown puffed breeches, white hose; a wolfhound at his side,
        # his hand near its back
        'macro': {'muscle': 0.8, 'weight': 0.78, 'height': 0.78, 'proportions': 0.7}, 'age': 0.6,
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#d0a07a', 'hair': '#3a2616', 'helmet': '#1c1a1a', 'plume': '#ece8e0',
                   'top': '#5a3e28', 'bottom': '#5a3e28', 'cape': '#4e3626', 'belt': '#2a2018',
                   'wraps': '#e4e0d4', 'wraps_band': '#c8c2b4', 'sandal': '#2a2018', 'gold': '#c8a038'},
        'helmet': None, 'headgear': 'beret',             # MISSING: feather laid sideways on the beret
        'hair': 'short', 'hair_asset': None, 'beard': True,
        # the civ-color puffed sleeves (check they cover the shoulder tops); MISSING: quilting, gold dots
        'top': 'sleeved', 'puffy': True,
        'bottom': 'breeches', 'wraps': 'wraps', 'sandals': True,   # white hose; MISSING: low_shoes (gold buckles)
        'cape': True, 'cape_length': 1.0,
        'cuirass': False, 'bracers': False, 'gear': [],
        'weapon': None, 'offhand': None,        # MISSING: companion_dog (grey wolfhound, tinted collar)
        'stance_name': 'stand',
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
        # MISSING: dense feather fan (gold/tint/red/teal cycle; feather_headdress has 11 sparse feathers)
        'helmet': None, 'headgear': 'feather_headdress',
        'hair': 'long', 'hair_asset': 'bob01', 'beard': False,
        'top': None,
        'bottom': 'loincloth', 'loin_length': (0.5, 0.42), 'torn': False,
        # a wide gold collar; MISSING: broad_collar (gold pectoral)
        'bands': [('neck', 'gold', 0.08), ('upperarm', 'gold'), ('wrist', 'gold')],
        'cape': True, 'cape_length': 1.1,       # MISSING: cape_panels (gold top, fringed teal/tinted panels, red block)
        'hem_bands': [('cape', 'gold', 0.04)],  # the gold-edged cloak
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
        'colors': {'skin': '#a87e5c', 'hair': '#140e0a', 'helmet': '#c8b070', 'top': '#c4ae72',
                   'bottom': '#c4ae72', 'belt': '#a8862e', 'sandal': '#4a3622', 'gold': '#c8a038'},
        'helmet': 'crown', 'crown': 'tall', 'crown_height': 0.06,   # MISSING: crown_band (tinted)
        'hair': 'long', 'hair_asset': 'short02',
        'beard': True, 'beard_style': 'long',  # the long square beard to the chest
        'top': 'sleeved', 'neck': 'high', 'bottom': 'kilt', 'robe_drop': 0.48,
        'hem_bands': [('bottom', 'gold', 0.035), ('bottom', 'tint', 0.03, 0.05), ('cape', 'gold', 0.035)],
        'cape': True, 'cape_length': 1.38,
        'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False, 'gear': [],
        'weapon': None, 'offhand': None,
        'stance_name': 'fists_on_hips',
        'tint': ['cape'],
        'grime': 0.45, 'upper_bulk': 1.2,
    },
    'King Byzantines': {
        # Theodora: a tall jeweled crown, long dark hair, a floor-length
        # white gown with white sleeves (as the original; the note tints
        # them) and a gold front stripe, a broad gold jeweled collar, a
        # full-length civ-color cape with a broad gold hem; hands together at
        # the chest holding a small white object
        'macro': {'muscle': 0.5, 'weight': 0.5, 'height': 0.72, 'proportions': 0.78}, 'age': 0.5,
        'race': {'caucasian': 0.8, 'african': 0.1, 'asian': 0.1},
        'colors': {'skin': '#d0aa8c', 'hair': '#2a1a10', 'helmet': '#c8a038', 'top': '#e8e2cc',
                   'bottom': '#e8e2cc', 'belt': '#c8a038', 'sandal': '#c8a040', 'gold': '#c8a038'},
        # MISSING: crown_panels (the original's tall crown is red/tinted with gold bands)
        'gender': 0.0, 'helmet': 'crown', 'crown': 'tall', 'crown_height': 0.08, 'crown_gems': '#3a6ac8',
        'hair': 'long', 'hair_asset': 'long01', 'beard': False,
        'top': 'sleeved', 'wide_sleeves': True, 'neck': 'high',
        'bottom': 'gown', 'robe_drop': 0.55,    # MISSING: gown_front_stripe (gold, full length)
        'hem_bands': [('bottom', 'gold', 0.07), ('cape', 'gold', 0.08)],
        'bands': [('neck', 'gold')],            # MISSING: broad_collar (gold jeweled)
        'cape': True, 'cape_length': 1.38,
        'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False, 'gear': [],
        'weapon': None, 'offhand': None,        # MISSING: small_object (white, held at the chest)
        'stance_name': 'hands_clasped',
        'tint': ['cape'],
        'grime': 0.3, 'upper_bulk': 1.0,
    },
    'King Carthaginian': {
        # Hannibal: bald, a grey scale cuirass with a scaled back cape, steel
        # shoulder pieces (as the original; the note says gold), a dark brown
        # tunic below the chest piece, brown baggy trousers into boots, wide
        # civ-color bracers and trouser stripes, a short sword sheathed at the hip
        'macro': {'muscle': 0.95, 'weight': 0.8, 'height': 0.78, 'proportions': 0.68}, 'age': 0.55,
        'race': {'caucasian': 0.6, 'african': 0.3, 'asian': 0.1},
        'colors': {'skin': '#a07c5e', 'hair': '#241a12', 'cuirass': '#646a72', 'top': '#4a3a2c',
                   'bottom': '#5a4a3a', 'boots': '#2e241c', 'belt': '#3a2a1c', 'steel': '#a8aaae',
                   'leather': '#3a2818'},
        'helmet': None, 'hair': 'bald', 'hair_asset': None, 'beard': False,
        # plate bands stand in for the scales; MISSING: scale_armor (scales, scaled back cape)
        'cuirass': True, 'metal': ['cuirass'], 'neck': 'high', 'cuirass_bands': 5,
        'top': 'tunic', 'coat_length': 0.25,    # the dark brown tunic below the chest piece
        'pauldrons': 'metal',                   # steel pauldrons (the cuirass's metal)
        'bottom': 'long', 'puffy': True, 'wraps': 'boots',   # MISSING: trouser_stripes (tinted, down each side)
        'bands': [('wrist', 'tint', 0.09)],     # the wide tinted bracers
        'cape': False, 'sandals': False, 'bracers': False, 'gear': [],
        'weapon': None, 'offhand': None,
        'hip': ('sheathed_sword', {'length': 0.6}),
        'stance_name': 'stand',
        'tint': [],
        'grime': 0.55, 'upper_bulk': 1.3,
    },
    'King Celtic': {
        # Boudica's people: long dark hair, a big mustache, a hip-length
        # civ-color tartan tunic over a brown skirt (as the original; the note
        # has a knee-length tartan tunic), dark green trousers, brown bracers,
        # a brown baldric, a long sword sheathed on the back (gold hilt over
        # the shoulder), boots; arms hanging
        'macro': {'muscle': 0.95, 'weight': 0.85, 'height': 0.8, 'proportions': 0.66}, 'age': 0.55,
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#c09a78', 'hair': '#3a2414', 'belt': '#4a3020', 'boots': '#3a2a1c',
                   'leather': '#5a3a22', 'gold': '#c8a038', 'bottom': '#5a4630', 'bracers': '#5a3a22'},
        'helmet': None, 'hair': 'long', 'hair_asset': 'long01', 'mane': 0.18,
        'beard': True, 'beard_style': 'mustache',
        'top': 'tunic', 'neck': 'high', 'coat_length': 0.2,   # the tunic to the hip
        'patterns': {'top': ('tartan', '#1e3a24')},
        'bottom': 'kilt', 'robe_drop': 0.0,     # the brown skirt; MISSING: trousers under a skirt (dark green)
        'gear': [('chest_strap', {'color': '#5a3a22'})],
        'wraps': 'boots', 'cape': False, 'cuirass': False, 'sandals': False, 'bracers': True,
        'weapon': None, 'offhand': None,        # MISSING: shoulder_clasp
        # MISSING: back_angle (the scabbard runs diagonally, gold hilt over the shoulder; check its angle)
        'back': 'sheathed_sword', 'back_params': {'length': 0.9},
        'stance_name': 'stand',
        'tint': ['top'],
        'grime': 0.6, 'upper_bulk': 1.3,
    },
    'King Chinese': {
        # Mao: thinning dark hair, a grey-blue high-collared suit, darker
        # trousers, brown shoes, civ-color armbands; hands clasped low in front
        'macro': {'muscle': 0.65, 'weight': 0.95, 'height': 0.76, 'proportions': 0.7}, 'age': 0.75,
        'race': {'caucasian': 0.0, 'african': 0.0, 'asian': 1.0},
        'colors': {'skin': '#c89c76', 'hair': '#141414', 'top': '#66788a', 'bottom': '#505a68',
                   'boots': '#3a2418', 'belt': '#4e5664'},
        'helmet': None, 'hair': 'short', 'hair_asset': 'short02', 'beard': False,   # MISSING: balding_hair
        'top': 'sleeved', 'neck': 'high', 'bottom': 'long', 'wraps': 'boots',   # MISSING: low_shoes, jacket pockets
        'coat_length': 0.15,                    # the hip-length jacket
        'bands': [('upperarm', 'tint', 0.06)],
        'cape': False, 'cuirass': False, 'sandals': False, 'bracers': False, 'gear': [],
        'weapon': None, 'offhand': None,
        'stance_name': 'hands_clasped',
        'tint': [],
        'grime': 0.3, 'upper_bulk': 1.05,
    },
    'King Egyptian': {
        # Ramesses: a striped civ-color nemes with a gold uraeus, a gold broad
        # collar and arm bands, a white garment over the torso (as the
        # original; the note implies a bare chest), a white pleated ankle kilt
        # with tinted bands and a tinted apron; a long gold crook held low and
        # out; the other hand on the hip
        'macro': {'muscle': 0.9, 'weight': 0.65, 'height': 0.78, 'proportions': 0.72}, 'age': 0.5,
        'race': {'caucasian': 0.4, 'african': 0.5, 'asian': 0.1},
        'colors': {'skin': '#9a6a44', 'hair': '#141010', 'helmet': '#e4dcc4', 'top': '#f0ece0',
                   'bottom': '#f0ece0', 'belt': '#c8a038', 'sandal': '#c8a040', 'gold': '#c8a038',
                   'wood': '#b8902e', 'leather': '#b8902e'},
        'helmet': 'hood',                       # MISSING: nemes (striped headcloth, lappets, gold uraeus)
        'hair': 'bald', 'hair_asset': None, 'beard': False,
        'top': 'vest',
        'bottom': 'kilt', 'robe_drop': 0.42,    # MISSING: pleats, front_apron (tinted hanging panel)
        'hem_bands': [('bottom', 'tint', 0.04, 0.12), ('bottom', 'tint', 0.04, 0.32)],
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
                   'gold': '#c8a038', 'ruff': '#f0eee8', 'wood': '#b8902e', 'leather': '#b8902e',
                   'boots': '#3a2418'},
        'gender': 0.0, 'helmet': 'crown', 'crown': 'spiked', 'crown_height': 0.08,
        'hair': 'short', 'hair_asset': 'bob01', 'beard': False,   # MISSING: updo
        'top': 'sleeved', 'puffy': True, 'neck': 'high',
        'bottom': 'gown', 'gown_flare': 1.9, 'robe_drop': 0.55,   # MISSING: farthingale stripes (gold) and jewel dots
        'hem_bands': [('bottom', 'gold', 0.05)],
        'bands': [('neck', 'ruff', 0.06)],      # MISSING: ruff (tall lace fan behind the head)
        'cape': False, 'cuirass': False, 'wraps': 'boots', 'sandals': False, 'bracers': False, 'gear': [],
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
        'colors': {'skin': '#e0b494', 'hair': '#8a3a1c', 'cuirass': '#9a9fa8', 'top': '#90959e',
                   'bottom': '#8a8f98', 'greaves': '#90959e', 'belt': '#3a2a1c', 'boots': '#9a9fa8',
                   'steel': '#b8bcc2', 'leather': '#4a3020', 'mail': '#7a7c80'},
        'helmet': None, 'hair': 'short', 'hair_asset': 'short02', 'beard': False,
        'cuirass': True, 'metal': ['cuirass'], 'neck': 'high', 'top': 'sleeved', 'pauldrons': True,
        'bottom': 'long', 'wraps': 'greaves', 'boots': True,   # MISSING: plate_legs, plate_arms (metal, not cloth), sabatons
        'cape': True, 'cape_length': 1.38,      # MISSING: shoulder_mantle (tinted), cape_emblem (gold fleur-de-lis)
        'hem_bands': [('cape', 'gold', 0.03)],  # the gold edge trim
        'sandals': False, 'bracers': False, 'gear': [],
        # held low by 'stand'; MISSING: held_low_forward
        'weapon': 'sword', 'props': {'sword': ('long_sword', {'length': 0.9, 'guard': 0.2})},
        'stance_name': 'stand',
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
        'top': 'sleeved', 'neck': 'high',       # MISSING: epaulettes (gold)
        'coat_length': 0.55, 'coat_flare': 1.05,   # the frock coat's skirts
        'bottom': 'long', 'wraps': 'boots',
        'gear': [('chest_strap', {'width': 0.16})],   # the tinted diagonal sash
        'bands': [('wrist', 'gold')],
        'cape': False, 'cuirass': False, 'sandals': False, 'bracers': False,
        'weapon': None, 'offhand': None,
        'stance_name': 'stand',                 # MISSING: fist_on_hip (left)
        'tint': ['strap'],
        'grime': 0.35, 'upper_bulk': 1.2,
    },
    'King Greek': {
        # Alexander: short curly hair with a gold headband (as the original;
        # the note tints it), a sleeveless white chiton, a big civ-color
        # himation over the left shoulder and round the hips to the ankles
        # (white down one side), a gold neck collar, gold sandals; hands
        # together at the waist
        'macro': {'muscle': 0.9, 'weight': 0.7, 'height': 0.78, 'proportions': 0.72}, 'age': 0.45,
        'race': {'caucasian': 0.9, 'african': 0.05, 'asian': 0.05},
        'colors': {'skin': '#c4a07e', 'hair': '#3a2616', 'top': '#e4e0d6', 'bottom': '#e4e0d6',
                   'belt': '#c8a038', 'sandal': '#c8a038', 'gold': '#c8a038', 'helmet': '#c8a038'},
        'helmet': 'headband', 'hair': 'short', 'hair_asset': 'short04', 'beard': False,   # MISSING: curly_hair
        'top': 'vest', 'bottom': 'kilt', 'robe_drop': 0.45,
        # MISSING: himation (draped over the left shoulder, wrapping the hips to below the knee)
        'gear': [('chest_strap', {'width': 0.10})], 'cape': False,
        'bands': [('neck', 'gold', 0.025)],
        'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False,
        'weapon': None, 'offhand': None,
        'stance_name': 'hands_clasped',
        'tint': ['strap', 'bottom'],
        'grime': 0.35, 'upper_bulk': 1.2,
    },
    'King Hatti': {
        # a gold circlet on dark hair, full dark beard, a long-sleeved cream
        # ankle robe with tinted cuffs and a tinted waist band, a maroon front
        # apron; a small gold axe raised in the right hand (as the original;
        # the note's knobbed scepter held low and its tinted shawl contradict it)
        'macro': {'muscle': 0.85, 'weight': 0.85, 'height': 0.76, 'proportions': 0.68}, 'age': 0.6,
        'race': {'caucasian': 0.8, 'african': 0.1, 'asian': 0.1},
        'colors': {'skin': '#a88262', 'hair': '#241a12', 'helmet': '#c8a038', 'top': '#d8ccaa',
                   'bottom': '#d8ccaa', 'belt': '#c8a038', 'sandal': '#4a3622', 'gold': '#c8a038',
                   'wood': '#b8902e', 'steel': '#c8a038', 'iron': '#c8a038'},
        'helmet': 'crown', 'crown': 'circlet', 'crown_height': 0.05,
        'hair': 'short', 'hair_asset': 'short02', 'beard': True,
        'top': 'sleeved', 'neck': 'high',
        'bottom': 'kilt', 'robe_drop': 0.55,    # MISSING: front_apron (maroon), seam line
        'hem_bands': [('bottom', 'tint', 0.04)],
        'bands': [('waist', 'tint'), ('wrist', 'tint')],
        'cape': False, 'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False, 'gear': [],
        # the small gold axe, raised
        'weapon': 'axe', 'props': {'axe': ('battle_axe', {'length': 0.55})},
        'stance_name': 'royal',
        'tint': [],
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
                   'bottom': '#b0904a', 'belt': '#c8a038', 'sandal': '#b89a50', 'gold': '#c8a038',
                   'wood': '#3e2a1a'},
        # MISSING: crown_disc (gold, at the front), crown_top (the original's red/tinted top; the note says plain gold)
        'helmet': 'crown', 'crown': 'circlet', 'crown_height': 0.06,
        'hair': 'short', 'hair_asset': None, 'beard': False,
        'top': 'tunic', 'bottom': 'kilt', 'robe_drop': 0.0,
        'patterns': {'top': ('check', '#6a4e2a', 20.0)},   # the gold and brown checkerboard tunic
        'bands': [('knee', 'gold'), ('neck', 'gold')],
        'cape': True, 'cape_length': 1.05,      # MISSING: zigzag_border (white, stepped)
        # the gold band, and a white band standing in for the zig-zag
        'hem_bands': [('cape', 'gold', 0.05, 0.12), ('cape', '#ece8e0', 0.03, 0.06)],
        'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False, 'gear': [],
        # MISSING: feather_standard (staff topped with a fan of tinted/red/teal feathers), left_hand_hold
        'weapon': 'staff', 'props': {'staff': ('staff', {'length': 2.1, 'grip': 1.2})},
        'stance_name': 'stand_staff',
        'tint': ['cape'],
        'grime': 0.45, 'upper_bulk': 1.15,
    },
    'King Indian': {
        # Gandhi: bald, glasses, a short-sleeved white wrap (the original has
        # no long shawl down the back, though the note asks for one), a white
        # knee dhoti, a civ-color sash, sandals, a tall thin walking stick;
        # slightly stooped, walking
        'macro': {'muscle': 0.55, 'weight': 0.3, 'height': 0.72, 'proportions': 0.75}, 'age': 0.85,
        'race': {'caucasian': 0.4, 'african': 0.3, 'asian': 0.3},
        'colors': {'skin': '#8a6448', 'hair': '#c8c4bc', 'top': '#e4e2dc', 'bottom': '#e4e2dc',
                   'cape': '#e4e2dc', 'belt': '#c8c2b4', 'sandal': '#5a3a22', 'wood': '#5a3a22'},
        'helmet': None, 'hair': 'bald', 'hair_asset': None, 'beard': False,   # MISSING: face_prop (props.glasses 'round')
        'top': 'mantle', 'bottom': 'kilt', 'robe_drop': 0.0,   # MISSING: dhoti (wrapped between the legs)
        'cape': False,
        'gear': [('chest_strap', {'width': 0.12})],   # the sash, the original's main civ mark
        'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False,
        # MISSING: tinted_tip (the stick's top)
        'weapon': 'staff', 'props': {'staff': ('staff', {'length': 1.55, 'grip': 0.95})},
        'stance_name': 'stand_staff',           # MISSING: walking_stoop
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
        'helmet': None, 'headgear': 'feather_bonnet',      # MISSING: war_bonnet trailer down the back
        'hair': 'long', 'hair_asset': 'long01', 'beard': False,
        'top': None, 'bottom': 'loincloth', 'loin_length': (0.5, 0.45), 'torn': False,
        'wraps': 'wraps', 'sandals': False,     # MISSING: leggings (brown cloth), moccasins
        'gear': [('chest_strap', {'width': 0.10})],
        'bands': [('upperarm', 'tint'), ('knee', 'tint')],
        'cape': False, 'cuirass': False, 'bracers': False,
        # MISSING: feathered_spear (fur/feather wrap below the head)
        'weapon': 'spear', 'props': {'spear': ('spear', {'length': 2.3, 'grip': 1.15})},
        'offhand': 'shield_round', 'offhand_mesh': ('shield_round', {'r': 0.25, 'boss': False, 'tint': 'none'}),
        'shield_at': 'left_hip',                # MISSING: shield_at_hip (it floats in front of the groin)
        'stance_name': 'stand_staff',           # MISSING: fist_on_hip (left)
        'tint': ['helmet', 'bottom', 'strap'],
        'grime': 0.5, 'upper_bulk': 1.25,
    },
    'King Japanese': {
        # a black topknot, a civ-color jacket, a full-width grey hakama with a
        # gold vertical stripe and gold hem, a white V collar, white tabi (as
        # the original; the note's tinted cap and robe contradict it); both
        # fists on hips
        'macro': {'muscle': 0.8, 'weight': 0.8, 'height': 0.74, 'proportions': 0.68}, 'age': 0.6,
        'race': {'caucasian': 0.0, 'african': 0.0, 'asian': 1.0},
        'colors': {'skin': '#d0a882', 'hair': '#141010', 'cuirass': '#3a3a40', 'belt': '#ece8e0',
                   'boots': '#e8e4dc', 'collar_w': '#ece8e0', 'gold': '#c8a038', 'bottom': '#6a6a72'},
        # the black topknot (check it reads on top of the head)
        'helmet': None, 'hair': 'topknot', 'hair_asset': 'ponytail01', 'beard': False,
        'top': 'sleeved', 'wide_sleeves': False, 'neck': 'high',
        'bottom': 'kilt', 'robe_drop': 0.48,    # MISSING: hakama_stripe (gold, down the front)
        'hem_bands': [('bottom', 'gold', 0.04)],
        'cuirass': False,
        'bands': [('neck', 'collar_w'), ('wrist', 'gold')],
        'cape': False, 'wraps': 'boots', 'sandals': False, 'bracers': False, 'gear': [],   # MISSING: low_shoes
        'weapon': None, 'offhand': None,
        'stance_name': 'fists_on_hips',
        'tint': ['top', 'sleeves'],
        'grime': 0.3, 'upper_bulk': 1.15,
    },
    'King Korean': {
        # Wang Kon: shaved head, a golden-yellow ankle robe with wide sleeves,
        # civ-color collar, front stripes and sleeve edges; both hands
        # together at the chest holding a small dark tablet
        'macro': {'muscle': 0.75, 'weight': 0.75, 'height': 0.74, 'proportions': 0.7}, 'age': 0.6,
        'race': {'caucasian': 0.0, 'african': 0.0, 'asian': 1.0},
        'colors': {'skin': '#c8a68a', 'hair': '#141010', 'top': '#d4bc88', 'bottom': '#d4bc88',
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
        'colors': {'skin': '#8e6a4e', 'hair': '#141010', 'helmet': '#c8a038', 'bottom': '#c8a040',
                   'belt': '#c8a038', 'sandal': '#5a3a22', 'gold': '#c8a038', 'plume': '#2e7a4a'},
        'helmet': None, 'headgear': 'feather_headdress',   # MISSING: white feather cascade to the waist
        'hair': 'short', 'hair_asset': 'bob01', 'beard': False,   # MISSING: face_ornaments (gold)
        'top': None, 'bottom': 'kilt', 'robe_drop': 0.0,   # MISSING: jaguar_spots
        'bands': [('neck', 'gold'), ('upperarm', 'gold'), ('wrist', 'gold')],   # MISSING: broad_collar
        'cape': True, 'cape_length': 1.3,       # MISSING: cape_flare (a narrow rigid slab now)
        'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False, 'gear': [],
        'weapon': None, 'offhand': None,        # MISSING: small_object (gold, in the right hand)
        'stance_name': 'arms_folded',
        'tint': ['cape', 'plume'],
        'grime': 0.45, 'upper_bulk': 1.2,
    },
    'King Mongol': {
        # Genghis: a grey fur-brimmed hat with a tinted pointed crown, a
        # mustache and goatee, a long ochre deel to mid-shin with tinted cuffs
        # and hem band (as the original; the note's tinted vest and full beard
        # contradict it), brown boots; a scimitar low beside the thigh
        'macro': {'muscle': 0.95, 'weight': 0.9, 'height': 0.76, 'proportions': 0.66}, 'age': 0.6,
        'race': {'caucasian': 0.05, 'african': 0.0, 'asian': 0.95},
        'colors': {'skin': '#b88a60', 'hair': '#241a12', 'top': '#b88838', 'bottom': '#b88838',
                   'belt': '#3a2a1c', 'boots': '#3a2a1e', 'steel': '#b0b4b8', 'gold': '#c8a038',
                   'leather': '#3a2a1c'},
        'helmet': None, 'headgear': 'fur_hat',
        'hair': 'short', 'hair_asset': None,
        'beard': False,                         # MISSING: goatee (and long mustache); a full beard reads as a dark mask
        'top': 'sleeved', 'neck': 'high', 'bottom': 'kilt', 'robe_drop': 0.22,
        'hem_bands': [('bottom', 'tint', 0.06)],
        'bands': [('wrist', 'tint')],
        'cuirass': False,
        'wraps': 'boots',
        'cape': False, 'sandals': False, 'bracers': False, 'gear': [],
        'weapon': 'sword', 'props': {'sword': ('scimitar', {'length': 0.85})},
        'stance_name': 'stand',
        'tint': ['helmet'],
        'grime': 0.55, 'upper_bulk': 1.3,
    },
    'King Netherlands': {
        # William of Orange: a long curly dark periwig, a white doublet with
        # red slashes, white breeches and hose, black shoes (as the original;
        # the note's tinted breeches and tan shoes contradict it), an enormous
        # civ-color robe with a gold edge and hem, flaring wide onto the
        # ground; left hand on a planted cane, right fist on hip
        'macro': {'muscle': 0.75, 'weight': 0.75, 'height': 0.78, 'proportions': 0.72}, 'age': 0.55,
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#e0b89a', 'hair': '#5a3a20', 'top': '#e4e0d8', 'bottom': '#e4e0d8', 'belt': '#c8a038',
                   'wraps': '#ece8e0', 'wraps_band': '#d0ccc4', 'sandal': '#1e1a18', 'boots': '#1e1a18',
                   'wood': '#2a1e16', 'lacquer': '#2a1e16', 'gold': '#c8a038'},
        'helmet': None, 'hair': 'long', 'hair_asset': 'long01',   # MISSING: periwig (curly, to the shoulders)
        'beard': False,
        'top': 'sleeved', 'neck': 'high',       # MISSING: doublet_slashes (red)
        'bottom': 'breeches', 'puffy': True, 'wraps': 'boots', 'sandals': False,   # MISSING: low_shoes
        # MISSING: cape_train (wider, spread on the ground), ermine_trim (dots on the cape), cape_lining (dark purple)
        'cape': True, 'cape_length': 1.45,
        'hem_bands': [('cape', 'gold', 0.06)],
        'cuirass': False, 'bracers': False, 'gear': [],
        # the gold-topped cane planted from the fist; MISSING: left_hand_hold (the note has it in the left)
        'weapon': 'cane', 'props': {'cane': ('cane', {'length': 0.95})},
        'stance_name': 'stand_staff',           # MISSING: fist_on_hip (right)
        'tint': ['cape'],
        'grime': 0.35, 'upper_bulk': 1.1,
    },
    'King Ottoman': {
        # Suleiman: a large round white turban with a tinted aigrette and
        # jewel, dark beard, a white ankle kaftan with tinted panels low on
        # the skirt, a tinted waist-length vest with gold edges, a gold belt,
        # gold-yellow shoes (as the original; the note says brown); hands
        # together at the waist
        'macro': {'muscle': 0.8, 'weight': 0.85, 'height': 0.78, 'proportions': 0.7}, 'age': 0.6,
        'race': {'caucasian': 0.8, 'african': 0.1, 'asian': 0.1},
        'colors': {'skin': '#c89a74', 'hair': '#241a12', 'helmet': '#f0ece2', 'top': '#ece8de',
                   'bottom': '#ece8de', 'belt': '#c8a038', 'boots': '#c8a038', 'gold': '#c8a038'},
        # the white turban (hats take the helmet color: verify it renders white); MISSING: aigrette (jewel)
        'helmet': None, 'headgear': 'turban',
        'hair': 'short', 'hair_asset': None, 'beard': True,
        'top': 'sleeved', 'neck': 'high',
        # MISSING: side slits, skirt_panels (tinted triangles at the front, a large arc at the back)
        'bottom': 'kilt', 'robe_drop': 0.5,
        'hem_bands': [('bottom', 'tint', 0.12)],
        'cuirass': True,    # the tinted waist-length vest; MISSING: vest (sleeveless over-garment, gold edges)
        'cape': False, 'wraps': 'boots', 'sandals': False, 'bracers': False, 'gear': [],   # MISSING: low_shoes
        'weapon': None, 'offhand': None,
        'stance_name': 'hands_clasped',
        'tint': ['cuirass', 'plume'],
        'grime': 0.3, 'upper_bulk': 1.15,
    },
    'King Persian': {
        # Xerxes: a spiked gold crown, black hair and beard, a short-sleeved
        # white tunic to mid-thigh with a brown leather diagonal sash, a red
        # chest patch and a tinted hem band, narrow gold wristbands, tinted
        # knee bands, brown boots (as the original; the note says brown hair
        # and a tinted chest band); arms hanging
        'macro': {'muscle': 0.9, 'weight': 0.8, 'height': 0.8, 'proportions': 0.7}, 'age': 0.5,
        'race': {'caucasian': 0.8, 'african': 0.1, 'asian': 0.1},
        'colors': {'skin': '#b08664', 'hair': '#141010', 'helmet': '#c8a038', 'top': '#e8e4da',
                   'bottom': '#e8e4da', 'belt': '#c8a038', 'boots': '#5a3a22', 'bracers': '#c8a038',
                   'gold': '#c8a038'},
        'helmet': 'crown', 'crown': 'spiked',
        'hair': 'long', 'hair_asset': None, 'mane': 0, 'beard': True,   # MISSING: long_beard (wavy, square-cut)
        'top': 'tunic', 'bottom': 'skirt', 'robe_drop': 0.0,
        'hem_bands': [('bottom', 'tint', 0.06)],
        'gear': [('chest_strap', {'width': 0.1, 'color': '#5a3e24'})],   # the brown leather sash
        'chest_emblem': ('disc', 'tint', 0.12),   # MISSING: chest_patch (square)
        'bracers': False,
        'bands': [('wrist', 'gold'), ('knee', 'tint'), ('ankle', 'tint')],
        'wraps': 'boots',
        'cape': False, 'cuirass': False, 'sandals': False,
        'weapon': None, 'offhand': None,
        'stance_name': 'stand',
        'tint': [],
        'grime': 0.45, 'upper_bulk': 1.25,
    },
    'King Portugal': {
        # Henry the Navigator: a black round hat with a gold brim edge, long
        # dark hair and grey beard, a dark-brown doublet with gold puffed
        # shoulders and gold cuffs, civ-color trunk hose with dark slashes and
        # a gold hem, grey hose with gold garters, black shoes (as the
        # original; the note tints the whole doublet); a short gold-hilted
        # sword low and forward, left forearm to the chest
        'macro': {'muscle': 0.8, 'weight': 0.75, 'height': 0.76, 'proportions': 0.72}, 'age': 0.65,
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#d0aa8e', 'hair': '#3a2a1c', 'helmet': '#1e1a18', 'top': '#3a2a1e',
                   'cuirass': '#c8a038', 'wraps': '#9a9aa2', 'wraps_band': '#7a7a82',
                   'boots': '#18161a', 'belt': '#c8a038', 'gold': '#c8a038', 'steel': '#b0b4b8',
                   'leather': '#2a1e16'},
        'helmet': None, 'headgear': 'cavalier',   # MISSING: wide_brim_hat (round brim, gold edge; cavalier stands in)
        'hair': 'long', 'hair_asset': 'long01', 'beard': True,   # MISSING: grey beard with dark hair (one hair color)
        'top': 'sleeved', 'puffy': True, 'neck': 'high',   # MISSING: doublet_trim (gold buttons and edges)
        'pauldrons': 'metal',                   # the gold puffed shoulders (in the cuirass's gold)
        'bottom': 'breeches',                   # the tinted trunk hose; MISSING: hose_slashes (dark, vertical)
        'hem_bands': [('bottom', 'gold', 0.03)],
        'wraps': 'wraps',                       # the grey hose; MISSING: low_shoes (black)
        'bands': [('wrist', 'gold'), ('knee', 'gold')],   # gold cuffs and garters
        'cape': False, 'cuirass': False, 'sandals': False, 'bracers': False, 'gear': [],
        'weapon': 'sword', 'props': {'sword': ('sword', {'length': 0.62, 'guard': 0.16})},
        'stance_name': 'stand',                 # MISSING: hand_to_chest (left forearm raised)
        'tint': ['bottom'],
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
        'cuirass': True, 'metal': ['cuirass'], 'neck': 'high',   # MISSING: cuirass_panels (tinted at chest and shoulders)
        'top': 'tunic',
        # MISSING: pteruges (strip skirt; it renders on top of the cape in the back views)
        'bottom': 'loincloth', 'loin_length': (0.42, 0.4), 'torn': False,
        'wraps': 'boots',
        'cape': True, 'cape_length': 1.05,      # MISSING: cape_drape (over the left shoulder, wrapping the arm)
        'sandals': False, 'bracers': False, 'gear': [],
        # the gold eagle-topped staff, the butt at the ground (check the eagle stays in frame)
        'weapon': 'standard', 'props': {'standard': ('eagle_standard', {'length': 2.0, 'grip': 1.0})},
        'stance_name': 'stand_staff',
        'tint': ['cape'],
        'grime': 0.45, 'upper_bulk': 1.25,
    },
    'King Russian': {
        # Catherine: a red-and-gold crown, hair in a bun, a silver-grey bodice
        # with a red diagonal sash, puffy grey-white sleeves, a huge bell skirt
        # in alternating vertical stripes of civ color and silver (as the
        # original; the note's blue gown under silver brocade contradicts it);
        # a short gold scepter raised by the shoulder, an orb at the lap
        'macro': {'muscle': 0.45, 'weight': 0.7, 'height': 0.72, 'proportions': 0.75}, 'age': 0.65,
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#e4c8b4', 'hair': '#6a4a2c', 'helmet': '#b8bcc4', 'top': '#b8bcc4', 'sleeves': '#c8ccd4',
                   'belt': '#c8a038', 'gold': '#c8a038', 'wood': '#b8902e', 'leather': '#b8902e'},
        'gender': 0.0, 'helmet': 'crown', 'crown': 'arched', 'crown_gems': '#a8242a',   # MISSING: crown_silver (silver-and-gold)
        'hair': 'short', 'hair_asset': None, 'beard': False,   # MISSING: updo (bun)
        'top': 'sleeved', 'puffy': True, 'neck': 'high',
        # MISSING: skirt_stripes (vertical, civ color and silver), lace_hem
        'bottom': 'gown', 'gown_flare': 3.0, 'robe_drop': 0.62,
        'cape': False, 'cuirass': False, 'wraps': None, 'sandals': False, 'bracers': False,
        'gear': [('chest_strap', {'width': 0.08})],   # the red diagonal sash
        # the short gold scepter raised by the shoulder; MISSING: left_hand_hold (props.orb at the lap)
        'weapon': 'scepter', 'props': {'scepter': ('scepter', {'length': 0.6, 'head': 'fleur'})},
        'stance_name': 'royal',
        'tint': ['bottom', 'strap'],
        'grime': 0.3, 'upper_bulk': 1.0,
    },
    'King Spanish': {
        # Isabella: a tall gold open crown on bare light-brown hair, a white
        # veil from the head down the back to the hips (no hood, as the
        # original; the note's tinted wimple contradicts it), a civ-color
        # bodice and sleeves, a gown with a white front panel, civ-color
        # sides and a gold hem; left hand on hip, a gold scepter low and
        # forward in the right
        'macro': {'muscle': 0.4, 'weight': 0.45, 'height': 0.78, 'proportions': 0.8}, 'age': 0.55,
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#e4c8b4', 'hair': '#8a5a2a', 'cape': '#e8e8e8', 'belt': '#c8a038',
                   'gold': '#c8a038', 'wood': '#b8902e', 'leather': '#b8902e'},
        'gender': 0.0, 'helmet': 'crown', 'crown': 'tall',
        'hair': 'short', 'hair_asset': 'bob01', 'beard': False,
        'top': 'sleeved', 'neck': 'high',
        # MISSING: gown_front_panel (white), fleur_pattern (gold)
        'bottom': 'gown', 'gown_flare': 2.2, 'robe_drop': 0.62,
        'hem_bands': [('bottom', 'gold', 0.04)],
        # the white veil to the hips; MISSING: head_veil (from the head over the crown, not the shoulders), cape_flare
        'cape': True, 'cape_length': 0.9,
        'cuirass': False, 'wraps': None, 'sandals': False, 'bracers': False, 'gear': [],
        # the gold fleur-headed scepter, tipped forward
        'weapon': 'scepter', 'props': {'scepter': ('scepter', {'length': 0.7, 'head': 'fleur'})},
        'stance_name': 'royal', 'upright_dir': (0.0, -0.7, 0.7),   # MISSING: held_low_forward, fist_on_hip (left)
        'tint': ['top', 'bottom'],
        'grime': 0.3, 'upper_bulk': 1.0,
    },
    'King Sumeria': {
        # Gilgamesh: a tall white flat-topped crown, long black hair and
        # square beard, a brown one-shoulder wrap and a brown skirt with two
        # civ-color bands, a vertical red front stripe and white stripes at
        # the hem, a red waist band, white wristbands; a white ball-topped
        # staff held upright (as the original; the note's cream shawl, tinted
        # skirt and level rod contradict it)
        'macro': {'muscle': 0.95, 'weight': 0.85, 'height': 0.78, 'proportions': 0.66}, 'age': 0.55,
        'race': {'caucasian': 0.7, 'african': 0.2, 'asian': 0.1},
        'colors': {'skin': '#9a7456', 'hair': '#141010', 'helmet': '#ece6da', 'plume': '#ece6da',
                   'top': '#6a4430', 'bottom': '#6a4430', 'belt': '#8a3226', 'redband': '#8a3226',
                   'cuff': '#ece8e0', 'sandal': '#4a3622', 'wood': '#ece8e0'},
        'helmet': None, 'headgear': 'hat_cone',   # MISSING: tall_crown (a white flat-topped cylinder; hat_cone stands in)
        'hair': 'long', 'hair_asset': 'long01', 'mane': 0.15,
        'beard': True,                          # MISSING: long_beard (square, black)
        'top': 'mantle', 'neck': 'high',        # the brown wrap
        'bottom': 'kilt', 'robe_drop': 0.46,    # MISSING: front_stripe (red, vertical)
        'hem_bands': [('bottom', 'tint', 0.05, 0.10), ('bottom', 'tint', 0.05, 0.25), ('bottom', '#ece8e0', 0.03, 0.0)],
        'bands': [('waist', 'redband'), ('wrist', 'cuff')],
        'cape': False, 'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False, 'gear': [],
        # MISSING: staff_ball (a ball head on the staff)
        'weapon': 'staff', 'props': {'staff': ('staff', {'length': 1.9})},
        'stance_name': 'stand_staff',
        'tint': [],
        'grime': 0.5, 'upper_bulk': 1.3,
    },
    'King Viking': {
        # Ragnar: a steel conical helmet with gold rim and nasal, long
        # reddish-blond hair and beard, a grey mail tunic with gold belt and
        # collar, brown baggy trousers, wrapped boots, an ankle-length civ-
        # color cape; a long sword point-down behind the leg
        'macro': {'muscle': 1.0, 'weight': 0.9, 'height': 0.82, 'proportions': 0.65}, 'age': 0.55,
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#d0a888', 'hair': '#b07840', 'top': '#6e7278', 'mail': '#6e7278',
                   'bottom': '#5a4630', 'belt': '#4a3020', 'boots': '#4a3a2c', 'gold': '#c8a038',
                   'steel': '#a8acb0', 'leather': '#3a2818', 'helmet': '#8a8e94', 'bronze': '#c8a038'},
        'helmet': None, 'headgear': 'conical_helmet',   # the bronze rim and nasal guard
        'hair': 'long', 'hair_asset': 'long01', 'mane': 0, 'beard': True,
        'top': 'sleeved', 'neck': 'high', 'collar': 'mail',   # MISSING: mail_shirt (rings on the whole tunic)
        'bottom': 'long', 'puffy': True, 'wraps': 'boots',
        'bands': [('neck', 'gold')],
        'cape': True, 'cape_length': 1.3,       # MISSING: cape_closed_back (it splits open down the back)
        'cuirass': False, 'sandals': False, 'bracers': False, 'gear': [],
        'weapon': 'sword', 'props': {'sword': ('long_sword', {'length': 0.95})},
        'stance_name': 'stand',                 # MISSING: sword_point_down_back
        'tint': ['cape'],
        'grime': 0.55, 'upper_bulk': 1.35,
    },
    'King Zulu': {
        # Shaka: a cream feather crown round the head (untinted), bare dark
        # torso with a cream fur collar, a tan apron-kilt, tinted armlets and
        # below-knee bands; a bladed spear in the right hand and a tall red
        # (tinted) shield with white bars (as the original; the note's
        # knobkerrie and tinted headdress contradict it)
        'macro': {'muscle': 1.0, 'weight': 0.8, 'height': 0.82, 'proportions': 0.66}, 'age': 0.45,
        'race': {'caucasian': 0.0, 'african': 1.0, 'asian': 0.0},
        'colors': {'skin': '#5a3e2a', 'hair': '#141010', 'helmet': '#c4ac78', 'plume': '#ece4cc',
                   'feather': '#ece4cc', 'bottom': '#c4ac78', 'cape': '#b89c68', 'belt': '#6a5032',
                   'wood': '#b89a6a', 'stone': '#b89a6a', 'leather': '#6a5032', 'hide': '#ece8e0',
                   'hide_dark': '#2a1e16'},
        # MISSING: untinted feather crown (feather_headdress always tints half its feathers; 'bushy' tints all)
        'helmet': None, 'headgear': 'feather_headdress',
        'hair': 'short', 'hair_asset': 'afro01', 'beard': False,
        'top': None,
        'bottom': 'kilt', 'robe_drop': 0.0,     # MISSING: apron_kilt
        'cape': False, 'fur_vest': ('#c4ac78', '#6a5032'),   # the fur collar
        'bands': [('upperarm', 'tint'), ('knee', 'tint')],
        'cuirass': False, 'wraps': None, 'sandals': False, 'bracers': False, 'gear': [],
        # the short stabbing spear (as on the Impi)
        'weapon': 'spear', 'props': {'spear': ('spear', {'length': 1.3, 'grip': 0.45, 'head': 1.7, 'butt': False})},
        # MISSING: tinted_shield_stripes (a red shield with white bars; cowhide_shield has no tint)
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
        'bottom': 'kilt', 'robe_drop': 0.46,    # MISSING: undergown (white), lapel_trim (tinted)
        'hem_bands': [('bottom', 'tint', 0.05, 0.02), ('bottom', 'tint', 0.04, 0.10)],
        'bands': [('wrist', 'tint'), ('neck', 'tint')],
        'cape': False, 'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False, 'gear': [],
        # the shepherd's crook, wooden (gold/lapis recolored); check the hook is in frame and the butt reaches the
        # ground; MISSING: crook extras (a grip: the rod reaches only 0.2 below the fist)
        'weapon': 'crook', 'props': {'crook': ('crook', {'length': 2.0})},
        'stance_name': 'stand_staff',
        'tint': [],
        'grime': 0.5, 'upper_bulk': 1.05,
    },
    'Leader Industrial Ages': {
        # Washington/Frederick: a white powdered wig with a queue, a civ-
        # color knee-length frock coat with gold epaulettes and collar, grey
        # waistcoat and breeches, gold cuffs, black knee boots; one hand on
        # the hip, the other holding a small dark object at the waist
        'macro': {'muscle': 0.8, 'weight': 0.8, 'height': 0.82, 'proportions': 0.72}, 'age': 0.65,
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#e0b89a', 'hair': '#f4f2ee', 'bottom': '#a0a2a8', 'boots': '#141416',
                   'belt': '#a0a2a8', 'gold': '#c8a038', 'cuff': '#ece8e0', 'cuirass': '#c8a038'},
        'helmet': None, 'hair': 'topknot', 'hair_asset': 'ponytail01', 'beard': False,   # MISSING: powdered_wig (side rolls)
        'top': 'sleeved', 'neck': 'high',       # MISSING: waistcoat_front (white)
        'coat_length': 0.55,                    # the frock coat's skirts
        'pauldrons': 'metal',                   # the gold epaulettes (in the cuirass's gold)
        'bottom': 'breeches', 'wraps': 'boots',
        'bands': [('neck', 'gold'), ('wrist', 'gold')],
        'cape': False, 'cuirass': False, 'sandals': False, 'bracers': False, 'gear': [],
        'weapon': None, 'offhand': None,        # MISSING: tricorne_in_hand (small dark object at the waist)
        'stance_name': 'stand',                 # MISSING: fist_on_hip
        'tint': ['top'],
        'grime': 0.35, 'upper_bulk': 1.15,
    },
    'Leader Middle Ages': {
        # an armored king: gold crown, dark beard, full plate with a silver
        # plate chest (as the original; the note's tinted surcoat contradicts
        # it), a tan cloth skirt, a full-width civ-color cape whose back is a
        # large gold field inside a civ-color border, with a white fur hem
        # and ermine collar; a longsword point down at his side
        'macro': {'muscle': 0.95, 'weight': 0.85, 'height': 0.82, 'proportions': 0.68}, 'age': 0.6,
        'race': {'caucasian': 0.9, 'african': 0.05, 'asian': 0.05},
        'colors': {'skin': '#d4aa8a', 'hair': '#4a2e18', 'helmet': '#c8a038', 'top': '#a8acb4',
                   'cuirass': '#b8bcc4', 'bottom': '#b09a6a', 'greaves': '#a8acb4', 'boots': '#a8acb4',
                   'belt': '#3a2a1c', 'steel': '#b8bcc2', 'mail': '#7a7c80', 'leather': '#3a2a1c',
                   'gold': '#c8a038'},
        'helmet': 'crown', 'crown': 'spiked', 'brow_drop': 0.06,
        'hair': 'short', 'hair_asset': None, 'beard': True,
        'cuirass': True, 'metal': ['cuirass'], 'neck': 'high',   # the silver plate chest
        'top': 'sleeved', 'pauldrons': True, 'collar': 'mail',   # MISSING: plate_arms, plate_legs
        'bottom': 'kilt', 'robe_drop': 0.0, 'wraps': 'greaves', 'boots': True,
        # MISSING: cape_emblem (the gold field and lion on the back), ermine_trim (collar)
        'cape': True, 'cape_length': 1.38,
        'hem_bands': [('cape', '#f0ece4', 0.08)],   # the white fur hem
        'sandals': False, 'bracers': False, 'gear': [],
        'weapon': 'sword', 'props': {'sword': ('long_sword', {'length': 0.95})},
        'stance_name': 'stand',
        'tint': ['cape'],
        'grime': 0.45, 'upper_bulk': 1.3,
    },
    'Leader Modern Times': {
        # a general: green peaked cap, goatee, olive tunic, a broad gold-edged
        # civ-color sash shoulder to hip, tinted cuffs and shoulder patch,
        # khaki breeches, tall brown boots; a swagger stick level at the chest
        'macro': {'muscle': 0.8, 'weight': 0.75, 'height': 0.78, 'proportions': 0.72}, 'age': 0.6,
        'colors': {'skin': '#c8a48a', 'hair': '#2a1e14', 'helmet': '#4a5038', 'top': '#545a40',
                   'bottom': '#9a907a', 'boots': '#4e3828', 'belt': '#3a2a1c'},
        # the kepi (hats take the helmet color: verify it renders green); MISSING: peaked_cap (kepi stands in)
        'helmet': None, 'headgear': 'kepi',
        'hair': 'short', 'hair_asset': None,
        'beard': False,                         # MISSING: goatee (a full beard reads as a dark mask)
        'top': 'sleeved', 'neck': 'high', 'bottom': 'breeches', 'wraps': 'boots',
        'gear': [('chest_strap', {'width': 0.16})],   # the broad sash; MISSING: sash_edges (gold), shoulder_patch (tinted)
        'bands': [('wrist', 'tint')],
        'cape': False, 'cuirass': False, 'sandals': False, 'bracers': False,
        'weapon': 'stick', 'props': {'stick': ('swagger_stick', {'length': 0.6})}, 'offhand': None,   # level, forward
        'stance_name': 'royal', 'upright_dir': (0.0, -1.0, 0.1),
        'tint': ['strap'],
        'grime': 0.4, 'upper_bulk': 1.15,
    },
    'Princess': {
        # slender, brown hair in an updo, a gold tiara, a tinted choker, bare
        # shoulders, full-length tinted arms/gloves, a white bodice with a
        # small red-and-gold chest emblem (as the original; the note tints
        # the bodice), a floor-length white gown with a gold front stripe and
        # a tinted hem band; a short gold scepter raised upright to the shoulder
        'macro': {'muscle': 0.35, 'weight': 0.35, 'height': 0.72, 'proportions': 0.82}, 'age': 0.4,
        'race': {'caucasian': 0.8, 'african': 0.1, 'asian': 0.1},
        'colors': {'skin': '#e8c4ac', 'hair': '#6a4224', 'top': '#f2f0ea', 'bottom': '#f2f0ea', 'belt': '#c8a038',
                   'gold': '#c8a038', 'wood': '#b8902e', 'leather': '#b8902e'},
        'gender': 0.0, 'helmet': 'crown', 'crown': 'circlet',   # the gold tiara
        'hair': 'short', 'hair_asset': 'bob01', 'beard': False,   # MISSING: updo
        'top': 'vest', 'top_zmax': 1.4,         # the white bodice, shoulders bare
        'chest_emblem': ('disc', 'tint', 0.08),
        'bottom': 'gown', 'robe_drop': 0.62,    # MISSING: gown_front_stripe (gold)
        'hem_bands': [('bottom', 'tint', 0.05)],
        'bracers': True,                        # the long tinted gloves; MISSING: long_gloves (full arm, over the hands)
        'bands': [('neck', 'tint'), ('upperarm', 'tint')],   # the choker and the gloves' upper ends
        'cape': False, 'cuirass': False, 'wraps': None, 'sandals': False, 'gear': [],
        # the short gold scepter raised; MISSING: left_hand_hold
        'weapon': 'scepter', 'props': {'scepter': ('scepter', {'length': 0.5, 'head': 'orb'})},
        'stance_name': 'royal',
        'tint': ['bracers'],
        'grime': 0.3, 'upper_bulk': 0.9,
    },
    'SciLeader': {
        # Einstein: wild white hair and a big white mustache, a rumpled dark
        # grey suit, a tinted tie, a thin black cane planted in the right
        # hand, a brown doctor's bag (tinted straps) low in the left, a pipe;
        # slightly stooped
        'macro': {'muscle': 0.55, 'weight': 0.6, 'height': 0.72, 'proportions': 0.72}, 'age': 0.85,
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#d8ae8e', 'hair': '#f2f2f2', 'top': '#5a5a5e', 'bottom': '#3a3a3e',
                   'boots': '#2a2420', 'belt': '#2a2a2e', 'wood': '#18181a', 'leather': '#18181a',
                   'lacquer': '#18181a', 'gold': '#3a3a3e'},
        'helmet': None, 'hair': 'long', 'hair_asset': 'short04', 'mane': 0,   # MISSING: wild_hair
        'beard': False,                         # MISSING: mustache (big, white)
        'top': 'sleeved', 'neck': 'high', 'bottom': 'long', 'wraps': 'boots',   # MISSING: suit_jacket (open, lapels), tie (tinted), low_shoes
        'bands': [('neck', 'tint')],            # a tinted collar stands in for the tie
        'cape': False, 'cuirass': False, 'sandals': False, 'bracers': False, 'gear': [],
        # the thin black cane planted from the fist; MISSING: left_hand_hold (props.briefcase, brown,
        # tinted straps), mouth_prop (props.pipe)
        'weapon': 'cane', 'props': {'cane': ('cane', {'length': 0.9})},
        'stance_name': 'stand_staff',           # MISSING: walking_stoop
        'tint': [],
        'grime': 0.35, 'upper_bulk': 0.95,
    },
    'Shogun': {
        # a black kabuto with a gold kuwagata (as the original; the note tints
        # the bowl), a dark-brown kataginu vest, a civ-color ankle robe with
        # wide sleeves and dark and gold vertical stripes, white V collar,
        # gold cuffs and hem, white tabi; a katana raised two-handed over the
        # right shoulder
        'macro': {'muscle': 0.95, 'weight': 0.85, 'height': 0.76, 'proportions': 0.68}, 'age': 0.55,
        'race': {'caucasian': 0.0, 'african': 0.0, 'asian': 1.0},
        'colors': {'skin': '#c4a07e', 'hair': '#141010', 'helmet': '#1a1614', 'cuirass': '#2e221a',
                   'belt': '#2a2830', 'boots': '#e8e4dc', 'sandal': '#d8d4cc', 'collar_w': '#e0dcd4',
                   'gold': '#c8a038', 'lacquer': '#1a1412', 'iron': '#3a3a3c', 'cloth_wrap': '#2a2630',
                   'steel': '#b8bcc2'},
        'helmet': 'cap', 'metal': ['helmet'],   # MISSING: kabuto (flared neck guard, gold kuwagata crest)
        'hair': 'short', 'hair_asset': None, 'beard': False,
        'top': 'sleeved', 'wide_sleeves': True, 'neck': 'high',
        'cuirass': True,    # the dark kataginu panel; MISSING: kataginu (winged shoulders)
        'bottom': 'kilt', 'robe_drop': 0.5,     # MISSING: robe_stripes (dark and gold, vertical)
        'hem_bands': [('bottom', 'gold', 0.05)],
        'bands': [('neck', 'collar_w'), ('wrist', 'gold')],
        'cape': False, 'wraps': 'boots', 'sandals': False, 'bracers': False, 'gear': [],   # the white tabi
        'weapon': 'sword', 'props': {'sword': ('katana', {'length': 0.75})},
        'stance_name': 'guard',                 # MISSING: two_hand_raised (katana over the right shoulder)
        'tint': ['top', 'bottom', 'sleeves'],
        'grime': 0.45, 'upper_bulk': 1.3,
    },
    'The King': {
        # Elvis: a black pompadour, sunglasses, a white high-collared
        # jumpsuit with a deep tinted V, gold belt, tinted cuffs, bell-bottoms
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
        'bands': [('wrist', 'tint')],           # the tinted cuffs (as the original; the note says gold)
        'cape': True, 'cape_length': 0.75,      # MISSING: cape_flare (a stiff board now)
        'sandals': False, 'bracers': False, 'gear': [],
        'weapon': 'microphone', 'props': {'microphone': ('microphone', {})}, 'offhand': None,
        'stance_name': 'royal',                 # MISSING: rock_stance (wide, knees bent, arms out holding the cape)
        'tint': ['cuirass', 'cape'],
        'grime': 0.3, 'upper_bulk': 1.1,
    },
}

# MISSING FEATURES
# What the notes and the originals need that the supported keys can't
# express. Each entry above approximates it and marks the spot with
# "MISSING:". Ranked by how many units need it (the count in brackets).
#
# headgear kinds     [12] peaked_cap (Army Modern, Leader Modern; the kepi stands in), pickelhaube (German),
#                    kabuto (Shogun: neck guard, gold kuwagata), nemes (Egyptian: striped, lappets, uraeus),
#                    tall_crown (Sumeria: white flat-topped cylinder; hat_cone stands in), wide_brim_hat (Portugal:
#                    gold edge; cavalier stands in), an untinted feather crown (Zulu: feather_headdress always
#                    tints), a dense feather fan with cycling colors (Aztec), a white feather cascade to the waist
#                    (Mayan), a war-bonnet trailer (Iroquois), a feather laid on the beret (Austria), an aigrette
#                    (Ottoman).
# beard shapes       [10] long_beard (Babylonian, Persian, Sumeria, Leader Ancient), goatee (Mongol, Leader
#                    Modern), mustache only (Celtic, German, SciLeader), chin_beard (American). The full beard
#                    renders as a dark mask over the lower face, so Mongol and Leader Modern go beardless for now.
# vertical stripes / front panels  [9] gold or tinted stripes and panels down a robe or gown: Byzantines (gold
#                    front stripe), Princess (gold front stripe), Spanish (white front panel), English (farthingale
#                    gold stripes, jewel dots), Russian (civ color/silver skirt stripes), Japanese (gold hakama
#                    stripe), Korean (tinted robe stripes), Shogun (dark and gold), Sumeria (red front stripe).
# hair styles        [9] updo (English, Russian, Princess), periwig (Netherlands), powdered_wig (Leader
#                    Industrial), pompadour (The King), wild_hair (SciLeader), curly_hair (Greek), balding (Chinese).
# low_shoes          [8] American, Austria, Chinese, Japanese, Netherlands, Ottoman, Portugal, SciLeader: shoes to
#                    the ankle (boots reach mid-shin; sandals are strapped).
# cape shape         [7] cape_flare / cape_train (Netherlands, Spanish, The King, Mayan: narrow rigid slabs now),
#                    cape_closed_back (Viking: splits open down the back), cape_drape (Roman: over the left
#                    shoulder), head_veil (Spanish: from the head, over the crown).
# fist_on_hip        [6] Egyptian, German, Iroquois, Netherlands, Spanish, Leader Industrial: ONE hand on the hip,
#                    the other free or holding ('fists_on_hips' does both). Today they fall back to 'stand'.
# left_hand_hold     [5] a left-hand item slot ('offhand' is shields and bows only): Incan (feather standard),
#                    Netherlands (the cane), Russian (props.orb at the lap), Princess (the scepter), SciLeader
#                    (props.briefcase). Today the item goes in the right hand or is left out.
# crown extras       [5] Army Middle Ages (dark coif under it), Incan (gold front disc, red top), Russian
#                    (silver-and-gold), Byzantines (red/tinted panels), Babylonian (tinted band).
# broad_collar       [4] Aztec, Byzantines, Egyptian, Mayan: a gold pectoral collar (a wide neck band stands in).
# flag_device        [4] all four Armies: device paint on the war_standard's tinted field (bars, quarters, blobs,
#                    a diagonal cross).
# cape_emblem / ermine_trim  [4] Army Middle Ages, Leader Middle Ages (the gold field and lion on the cape's back,
#                    ermine collar), French (gold fleur-de-lis, tinted shoulder mantle), Netherlands (ermine dots,
#                    dark lining). Hem bands stand in for the fur hems.
# plate_arms / plate_legs  [3] French, Army Middle Ages, Leader Middle Ages: metal (not cloth) arm and leg plates.
# held_low_forward   [3] Egyptian (crook), French (sword), Spanish (scepter): an item held low and out, head up.
# small_object       [3] Byzantines (white), Korean (dark tablet), Mayan (gold): a small item held at the chest.
# sash_edges         [3] Army Modern, Leader Modern (gold edges on the broad sash), Arab (gold stripe on the baldric).
# undergown / lapel_trim  [2] Army Ancient, Leader Ancient: an open robe front showing a white gown, tinted lapels.
# epaulettes         [2] Army Industrial, German: gold epaulettes over a tinted or colored coat ('pauldrons'
#                    takes the top's or the cuirass's color; Leader Industrial uses 'metal' pauldrons in gold).
# front_apron        [2] Egyptian (tinted), Hatti (maroon): a hanging panel down the front of the robe.
# sleeve_cuff        [2] Army Ancient, Arab: a band at the end of a wide sleeve (a wrist band renders black inside it).
# hand_to_chest      [2] American (the original's hand at the lapel), Portugal: a forearm raised to the chest.
# walking_stoop      [2] Indian, SciLeader: a slight forward stoop with a stride, a cane planted ahead.
# face_prop          [2] Indian (props.glasses 'round'), The King (props.sunglasses).
# crook extras       [2] Leader Ancient (a grip param: the rod reaches only 0.2 below the fist), Egyptian (tinted bands).
# vest / panel_edges [2] Ottoman (gold-edged vest, tinted skirt panels, side slits), Shogun (kataginu with winged
#                    shoulders).
# one each           scale_armor and trouser_stripes (Carthaginian), mail_shirt and sword_point_down_back (Viking),
#                    himation (Greek), dhoti (Indian), pteruges and cuirass_panels (Roman), long_gloves (Princess),
#                    suit_jacket and tie (SciLeader), deep_v, bell_bottoms and rock_stance (The King),
#                    two_hand_raised (Shogun), ruff (English), cape_panels (Aztec), zigzag_border (Incan),
#                    feather_standard (Incan), jaguar_spots and face_ornaments (Mayan), pleats (Egyptian),
#                    fleur_pattern (Spanish), lace_hem (Russian), doublet_slashes (Netherlands), hose_slashes and
#                    doublet_trim (Portugal), quilting and companion_dog (Austria), trousers under a skirt and
#                    shoulder_clasp (Celtic), a back scabbard angle (Celtic), chest_patch (Persian), apron_kilt and
#                    tinted_shield_stripes (Zulu), staff_ball (Sumeria), feathered_spear, leggings and the shield
#                    at the hip (Iroquois), tinted_tip (Indian), shoulder_patch (Leader Modern), tricorne_in_hand
#                    and waistcoat_front (Leader Industrial), front_trim (Arab), jacket pockets (Chinese), a grey
#                    beard with dark hair (Portugal).
#
# Render bugs the reviewers saw (code, not data): the head bowed with hair over the face (Celtic, Hatti); bob01
# covering the face (English); gown_flare 1.9 rendering a straight column (English); notches in the cape hem
# (Babylonian); the cape standing off the back like a board in directions 3 and 7 (Byzantines); the Iroquois
# shield ('left_hip') floating in front of the groin.
