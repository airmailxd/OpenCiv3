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
  'upright_dir'); other left-hand items stay MISSING;
- women are 'gender': 0.0 with a 'gown' bottom ('gown_flare', 'robe_drop');
- royal poses are the stances 'fists_on_hips', 'hands_clasped' (also for
  hands together at the chest), 'hands_behind', 'royal' (a scepter held
  up in the right hand), 'stand' (arms relaxed, an item hanging from the
  right hand), 'stand_staff' and 'fist_hip_l' / 'fist_hip_r' (one fist on
  the hip; with 'fist_hip_l' the right hand holds an item as 'stand' does,
  aimed by 'hang_dir'; with 'fist_hip_r' the item goes in the left hand,
  aimed by 'tool_dir');
- beards are 'beard': True with 'beard_style' (full, mustache, goatee, chin,
  long) and an optional colors['beard']; low shoes are 'wraps': 'shoes', or
  'shoes': True over hose ('wraps': 'wraps');
- a narrow front strip (a waistcoat, a tie, a V) is a non-metal 'cuirass'
  with 'cuirass_width' (half-width, m; it also shows down the back);
- cloth patterns are 'patterns': {part: ...}: ('panel', hex, half_width) for
  a front stripe or apron (front and back), ('stripes', hex, scale), 'check',
  'tartan'; a tinted part keeps the civ color as its base, and the pattern
  color can't be 'tint' (so tinted stripes on a plain robe stay MISSING);
- remaster.py gives every royal unit_scale 0.85 (0.95 for women), cape_flare
  0.6, cape_wrap 0.1, cape_width 0.56, belt_gear False and upper_bulk <= 1.1;
  the entries override these where needed.
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
        'colors': {'skin': '#c08c64', 'hair': '#9a8e7c', 'top': '#9a8262', 'bottom': '#9a8262',
                   'belt': '#4a3424', 'sandal': '#4a3020', 'wood': '#5e4228', 'leather': '#3e2a1a'},
        'hair': 'short', 'hair_asset': 'short02', 'beard': True, 'helmet': None,
        'top': 'sleeved', 'wide_sleeves': True, 'neck': 'high',
        'bottom': 'kilt', 'robe_drop': 0.45,   # MISSING: undergown (cream gown showing down the open front)
        # stand-in for the undergown and its tinted lapels: a cream front-only panel edged in the civ color
        'patterns': {'top': [('panel', 'tint', 0.085, 'front'), ('panel', '#dcd8cc', 0.065, 'front')],
                     'bottom': [('panel', 'tint', 0.085, 'front'), ('panel', '#dcd8cc', 0.065, 'front')]},
        'bands': [('wrist', 'tint', 0.06)],     # the civ-color sleeve cuffs; MISSING: sleeve_cuff (band at the wide sleeve's end)
        'hem_bands': [('bottom', 'tint', 0.06, 0.05), ('bottom', 'tint', 0.06, 0.17)],   # wide enough to read at 1x
        # MISSING: lapel_trim (tinted robe front edges; the panel edges stand in)
        'cape': False, 'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False, 'gear': [],
        # the crossbar-hung vexillum, tinted field; MISSING: flag_device (three vertical bars, red/green/cyan)
        'weapon': 'standard', 'props': {'standard': ('war_standard', {'era': 'ancient', 'length': 2.30, 'grip': 1.2})},   # lower again: the flag top touched the cell top (dirs 6, 7), then 1 px in dir 6
        'stance_name': 'stand_staff', 'two_hands': True,
        'tint': ['banner'],
        'grime': 0.55, 'upper_bulk': 1.1,
        'cloth_rough': 0.95, 'cloth_dirt': 0.2,   # the coarse robe read flat
    },
    'Army Industrial Ages': {
        # an officer in a black bicorne and a civ-color frock coat with gold
        # epaulettes, grey trousers, tall black boots, holding the flagpole
        'macro': {'muscle': 0.8, 'weight': 0.7, 'height': 0.78, 'proportions': 0.72}, 'age': 0.55,
        'race': {'caucasian': 0.9, 'african': 0.05, 'asian': 0.05},
        'colors': {'skin': '#d4a888', 'hair': '#3a2a1c', 'helmet': '#1a1a1e', 'bottom': '#8a8c92',
                   'boots': '#141416', 'belt': '#1a1a1e', 'gold': '#b08a2e', 'wood': '#4a3222',
                   'cuirass': '#b08a2e'},       # (gold: the epaulettes)
        'helmet': None, 'headgear': 'bicorne', 'brow_drop': 0.06,
        'hair': 'short', 'hair_asset': 'short02', 'beard': False,
        'top': 'sleeved', 'neck': 'high', 'pauldrons': 'metal',   # gold epaulettes
        'coat_length': 0.5, 'coat_flare': 1.1,   # the frock coat's skirts, to the knee
        'cuirass': False, 'patterns': {'top': ('panel', '#e8e4dc', 0.06, 'front')},   # the white waistcoat front
        'bottom': 'long', 'wraps': 'boots',
        'bands': [('wrist', 'gold')],           # gold cuffs
        'cape': False, 'sandals': False, 'bracers': False, 'gear': [],
        # the flag flying from the pole top, tinted field; MISSING: flag_device (blob emblems)
        'weapon': 'standard', 'props': {'standard': ('war_standard', {'era': 'industrial', 'length': 2.4, 'grip': 1.2})},   # 0.05 lower again: it touched the cell top in dir 6
        'stance_name': 'stand_staff', 'two_hands': True,
        'tint': ['top', 'banner'],
        'grime': 0.45, 'upper_bulk': 1.15,
        'cloth_rough': 0.95,                    # the coat cloth read flat
    },
    'Army Middle Ages': {
        # an armored king: gold crown over a dark coif, full plate, a dark
        # tabard skirt, a floor-length civ-color cape with a white fur hem and
        # a gold lion; both hands on the swallow-tailed banner's pole
        'macro': {'muscle': 0.9, 'weight': 0.8, 'height': 0.8, 'proportions': 0.7}, 'age': 0.6,
        'race': {'caucasian': 0.9, 'african': 0.05, 'asian': 0.05},
        'colors': {'skin': '#c49a7e', 'hair': '#6a5640', 'helmet': '#3a3836', 'top': '#6a6e76',
                   'cuirass': '#6a6e76', 'greaves': '#666a72', 'bottom': '#4a4238', 'belt': '#2e2418',
                   'boots': '#6e727a', 'mail': '#5e626a', 'wood': '#4a3222', 'gold': '#b08a2e',
                   'beard': '#8a7258'},   # the goatee lighter: the face was muddy
        'helmet': 'crown', 'crown': 'spiked', 'crown_height': 0.07,   # MISSING: coif (dark, under the crown)
        'hair': 'short', 'hair_asset': 'short02', 'beard': True, 'beard_style': 'goatee',   # a short beard: the face reads
        'cuirass': True, 'metal': ['cuirass'], 'top': 'sleeved', 'pauldrons': True, 'collar': 'mail',
        'bottom': 'kilt', 'robe_drop': 0.0, 'wraps': 'greaves', 'boots': True,   # MISSING: plate_legs (cuisses over the thighs)
        'cape': True, 'cape_length': 1.3,       # off the floor, so the back hem isn't lost at the ground
        # the gold lion on the cape's back (an outline shape; the gold disc field it replaces read as a patch)
        'cape_emblem': ('lion', 'gold', 0.5),
        # the ermine hem (white with black tails), wider so it reads round the back, and the two gold bands above it
        'hem_bands': [('cape', 'ermine', 0.12), ('cape', 'gold', 0.03, 0.17), ('cape', 'gold', 0.03, 0.25)],
        'sandals': False, 'bracers': False, 'gear': [],
        # the swallow-tailed, crossbar-hung banner, tinted field; MISSING: flag_device (swallow-tail, red/green quarters; the PC is making it)
        'weapon': 'standard', 'props': {'standard': ('war_standard', {'era': 'medieval', 'length': 2.5, 'grip': 1.2})},
        'stance_name': 'stand_staff', 'two_hands': True,
        'tint': ['cape', 'banner'],
        'grime': 0.5, 'upper_bulk': 1.25,
    },
    'Army Modern Times': {
        # a soldier in an olive peaked cap, olive tunic, khaki
        # trousers, brown boots and a broad gold-edged civ-color sash
        'macro': {'muscle': 0.85, 'weight': 0.7, 'height': 0.78, 'proportions': 0.72}, 'age': 0.45,
        'colors': {'skin': '#c89a74', 'hair': '#2a1e14', 'helmet': '#3e4430', 'top': '#545a3c',
                   'bottom': '#9a8c6c', 'boots': '#4e3424', 'belt': '#3a2a1c', 'wood': '#4a3222',
                   'gold': '#b08a2e'},
        # the original wears an olive peaked cap with a black visor (the note's round helmet contradicts it);
        # the cap's crown takes the helmet color, its band the civ color
        'helmet': None, 'headgear': 'peaked_cap', 'metal': [],
        'hair': 'short', 'hair_asset': 'short02', 'beard': False,
        'top': 'sleeved', 'neck': 'high', 'bottom': 'long', 'wraps': 'boots',
        'gear': [('chest_strap', {'width': 0.2, 'edge': 'gold', 'edge_width': 0.035})],   # the broad tinted sash, gold-edged
        'cape': False, 'cuirass': False, 'sandals': False, 'bracers': False,
        # the big flag, tinted field; MISSING: flag_device (diagonal cross; the PC is making it)
        'weapon': 'standard', 'props': {'standard': ('war_standard', {'era': 'modern', 'length': 2.43, 'grip': 1.2})},   # lower: the flag top touched the cell top (dirs 6, 7)
        'stance_name': 'stand_staff', 'two_hands': True,
        'tint': ['strap', 'banner'],
        'grime': 0.55, 'upper_bulk': 1.15,
        'cloth_rough': 0.95, 'cloth_dirt': 0.15,   # the tunic cloth read flat
    },

    # ------------------------------------------------------------ kings
    'King American': {
        # Lincoln: tall and gaunt-dignified, a stovepipe hat with a civ-color
        # band, chin beard, black frock coat over a civ-color waistcoat,
        # white collar; hands clasped behind the back
        'macro': {'muscle': 0.7, 'weight': 0.5, 'height': 0.85, 'proportions': 0.78}, 'age': 0.7,
        'race': {'caucasian': 0.95, 'african': 0.05, 'asian': 0.0},
        'colors': {'skin': '#d8a888', 'hair': '#241c16', 'helmet': '#141416', 'top': '#1e1c1c',
                   'cuirass': '#2a2826', 'bottom': '#2a2622', 'shoes': '#3a2418', 'belt': '#1a1818',
                   'shirt': '#e8e4dc', 'beard': '#3e3228'},   # trousers a little lighter than the coat; the beard lighter than the hair
        'helmet': None, 'headgear': 'top_hat', 'brow_drop': 0.06,   # the hat lifted off the eyes; its band is always tinted (MISSING: hat_band_width, it's thin; the review wants it 0.05 wide)
        'hair': 'short', 'hair_asset': None,
        'beard': True, 'beard_style': 'chin',   # the chin beard, no mustache
        'top': 'sleeved', 'neck': 'high',
        'coat_length': 0.55, 'coat_flare': 1.15,   # the frock coat's skirts, heavier
        # the tinted waistcoat between the black coat fronts (0.07 read as a necktie)
        'cuirass': True, 'metal': [], 'cuirass_width': 0.14,
        'bands': [('neck', 'shirt', 0.03)],     # the white shirt collar
        'bottom': 'long', 'wraps': 'shoes',     # the brown shoes
        'cape': False, 'sandals': False, 'bracers': False, 'gear': [],
        'weapon': None, 'offhand': None,
        # MISSING: hand_to_chest (the original's right hand is raised to the lapel; the note says hands behind)
        'stance_name': 'hands_behind',
        'tint': ['cuirass'],
        'grime': 0.35, 'upper_bulk': 1.0,
        'cloth_rough': 0.95,                    # the frock cloth read flat
    },
    'King Arab': {
        # a big round civ-color turban, full black beard, a long loose cream
        # robe with wide sleeves, a civ-color baldric shoulder to hip; arms
        # folded at the waist
        'macro': {'muscle': 0.8, 'weight': 0.8, 'height': 0.78, 'proportions': 0.7}, 'age': 0.6,
        'race': {'caucasian': 0.7, 'african': 0.2, 'asian': 0.1},
        'colors': {'skin': '#b88a66', 'hair': '#1a1410', 'top': '#ddd4c0', 'bottom': '#ddd4c0',
                   'belt': '#8a6a3a', 'sandal': '#4a3622', 'helmet': '#ddd4c0', 'beard': '#3a2e24'},
        'cloth_rough': 0.95,                    # the cream robe read plastic
        # MISSING: aigrette (the tinted 'turban' has none; 'turban_aigrette' is the Ottoman's white one)
        'helmet': None, 'headgear': 'turban', 'brow_drop': 0.06, 'headgear_scale': 1.0,
        'hair': 'short', 'hair_asset': None, 'beard': True,
        'top': 'sleeved', 'wide_sleeves': True, 'neck': 'high',
        'bottom': 'kilt', 'robe_drop': 0.48,
        # a narrow tinted stripe down the robe front stands in for the front trim; MISSING: front_trim (along the edges)
        'patterns': {'top': ('panel', 'tint', 0.025, 'front'), 'bottom': ('panel', 'tint', 0.025, 'front')},
        'hem_bands': [('bottom', 'tint', 0.05)],
        'gear': [('chest_strap', {'width': 0.12, 'edge': 'gold', 'edge_width': 0.035})],   # the diagonal baldric, its gold stripe along the edges
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
        'colors': {'skin': '#d0a07a', 'hair': '#3a2616', 'beard': '#5a4230', 'helmet': '#1c1a1a', 'plume': '#ece8e0',
                   'top': '#5a3e28', 'bottom': '#5a3e28', 'cape': '#4e3626', 'belt': '#2a2018',
                   'wraps': '#e4e0d4', 'wraps_band': '#c8c2b4', 'sandal': '#2a2018', 'gold': '#b08a2e'},
        'helmet': None, 'headgear': 'beret_feather', 'brow_drop': 0.07,     # the dark beret (helmet color), the white plume laid along it
        'hair': 'short', 'hair_asset': None, 'beard': True,
        # the civ-color puffed sleeves (check they cover the shoulder tops); MISSING: quilting, gold dots
        'top': 'sleeved', 'puffy': True,
        'bottom': 'breeches', 'wraps': 'wraps', 'shoes': True, 'sandals': False,   # white hose, low shoes; MISSING: gold buckles
        'cape': True, 'cape_length': 1.0,       # dark brown as the original; MISSING: brocade
        'hem_bands': [('cape', 'tint', 0.07)],  # the tinted cape edge (wider: 0.05 didn't read)
        'cuirass': False, 'bracers': False, 'gear': [],
        # MISSING: companion_dog (grey wolfhound, tinted collar; the PC is making the prop). Once it exists, place it
        # with 'ground_props': [(name, params, (x, y), yaw)] at his side (x is his left, +y forward)
        'weapon': None, 'offhand': None,
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
        'colors': {'skin': '#b88c66', 'hair': '#140e0a', 'helmet': '#b08a2e', 'bottom': '#3e7a6a',
                   'belt': '#b08a2e', 'sandal': '#4a3020', 'gold': '#b08a2e', 'cape': '#9a3a2a'},
        # MISSING: dense feather fan (~17 feathers, gold/tint/red/teal cycle; the PC is making it)
        'helmet': None, 'headgear': 'feather_headdress', 'brow_drop': 0.03,   # the band higher above the brow (0.085 is the default; lifted another 0.02): the face was dark under it
        'hair': 'long', 'hair_asset': 'bob01', 'beard': False,
        'top': None,
        'bottom': 'loincloth', 'loin_length': (0.5, 0.42), 'torn': False,
        # a wide gold collar; MISSING: broad_collar (gold pectoral)
        'bands': [('neck', 'gold', 0.08), ('upperarm', 'gold'), ('wrist', 'gold')],
        'cape': True, 'cape_length': 1.1,       # MISSING: cape_panels (gold top, fringed teal/tinted panels, red block)
        'hem_bands': [('cape', 'gold', 0.05)],  # the gold-edged cloak
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
        'cloth_rough': 0.95,                    # the gold-cream robe read plastic
        'colors': {'skin': '#a87e5c', 'hair': '#140e0a', 'beard': '#3c3026', 'helmet': '#c8b070', 'top': '#c8b27a',
                   'bottom': '#c8b27a', 'belt': '#a8862e', 'sandal': '#4a3622', 'gold': '#b08a2e'},
        'helmet': 'crown', 'crown': 'tall', 'crown_height': 0.06,   # MISSING: crown_band (tinted)
        'hair': 'long', 'hair_asset': 'short02',
        'beard': True, 'beard_style': 'long',  # the long square beard to the chest
        # robe_drop 0.48 -> 0.4: the robe's hem poked through the cape's back as a pale torn patch (bottom right)
        'top': 'sleeved', 'neck': 'high', 'bottom': 'kilt', 'robe_drop': 0.4,
        'hem_bands': [('bottom', 'gold', 0.035), ('bottom', 'tint', 0.03, 0.05), ('cape', 'gold', 0.035)],
        'cape': True, 'cape_length': 1.38,
        'cape_lining': 'tint',                  # the inner side tinted too
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
        'colors': {'skin': '#d0aa8c', 'hair': '#2a1a10', 'helmet': '#b08a2e', 'top': '#e8e2cc',
                   'bottom': '#e8e2cc', 'belt': '#b08a2e', 'sandal': '#b08a2e', 'gold': '#b08a2e'},
        # MISSING: crown_panels (the original's tall crown is red/tinted with gold bands)
        'gender': 0.0, 'helmet': 'crown', 'crown': 'tall', 'crown_height': 0.08, 'crown_gems': '#3a6ac8',
        'hair': 'long', 'hair_asset': 'long01', 'beard': False,
        'top': 'sleeved', 'wide_sleeves': True, 'neck': 'high',
        'bottom': 'gown', 'robe_drop': 0.55,
        'patterns': {'bottom': ('panel', '#b08a2e', 0.04)},   # the gold front stripe, full length (the cape covers the back)
        'hem_bands': [('bottom', 'gold', 0.07), ('cape', 'gold', 0.08)],
        'bands': [('neck', 'gold', 0.06)],      # a wider gold collar; MISSING: broad_collar (gold jeweled)
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
                   'bottom': '#5a4a3a', 'boots': '#2e241c', 'belt': '#3a2a1c', 'steel': '#6e727a',
                   'leather': '#3a2818'},
        'helmet': None, 'hair': 'bald', 'hair_asset': None, 'beard': False,
        # plate bands stand in for the scales; MISSING: scale_armor (scales, scaled back cape)
        'cuirass': True, 'metal': ['cuirass'], 'neck': 'high', 'cuirass_bands': 5,
        'top': 'tunic', 'coat_length': 0.25,    # the dark brown tunic below the chest piece
        'pauldrons': 'metal',                   # steel pauldrons (the cuirass's metal)
        'bottom': 'long', 'puffy': True, 'wraps': 'boots',
        'patterns': {'bottom': ('side_stripe', 'tint', 0.88)},   # one tinted stripe down each outer seam ('stripes' repeated round the leg: pyjamas)
        'bands': [('wrist', 'tint', 0.12)],     # the wide tinted bracers, wrist to mid-forearm
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
        'colors': {'skin': '#c09a78', 'hair': '#7a5a3a', 'beard': '#8a6a48', 'belt': '#4a3020', 'boots': '#3a2a1c',
                   'leather': '#5a3a22', 'gold': '#b08a2e', 'bottom': '#5a4630', 'bracers': '#5a3a22'},
        'helmet': None, 'hair': 'long', 'hair_asset': 'long01', 'mane': 0.06,   # shorter: clear of the cheeks
        'beard': True, 'beard_style': 'mustache', 'beard_length_short': 0.01, 'beard_density': 450,   # lighter, smaller, thinner: not a blot
        'top': 'tunic', 'neck': 'high', 'coat_length': 0.2,   # the tunic to the hip
        # a bolder, larger tartan (it didn't read at 1x), on the tinted tunic and kilt
        'patterns': {'top': ('tartan', '#1e3a24', 7.0), 'bottom': ('tartan', '#1e3a24', 7.0)},   # bigger checks
        'bottom': 'kilt', 'robe_drop': 0.0,     # the tinted kilt; MISSING: trousers under a skirt (dark green)
        'gear': [('chest_strap', {'color': '#5a3a22'})],
        'wraps': 'boots', 'cape': False, 'cuirass': False, 'sandals': False, 'bracers': True,
        'weapon': None, 'offhand': None,        # MISSING: shoulder_clasp
        # MISSING: back_angle (no key: the scabbard hangs vertically; the review wants it tilted so the hilt rises over the right shoulder)
        'back_tilt': 35.0, 'back': 'sheathed_sword', 'back_params': {'length': 0.9},
        'stance_name': 'stand',
        'tint': ['top', 'bottom'],
        'grime': 0.6, 'upper_bulk': 1.3,
    },
    'King Chinese': {
        # Mao: thinning dark hair, a grey-blue high-collared suit, darker
        # trousers, brown shoes, civ-color armbands; hands clasped low in front
        'macro': {'muscle': 0.65, 'weight': 0.95, 'height': 0.76, 'proportions': 0.7}, 'age': 0.75,
        'race': {'caucasian': 0.0, 'african': 0.0, 'asian': 1.0},
        'colors': {'skin': '#c89c76', 'hair': '#141414', 'top': '#66788a', 'bottom': '#505a68',
                   'shoes': '#3a2418', 'belt': '#4e5664'},
        'helmet': None, 'hair': 'short', 'hair_asset': 'short02', 'beard': False,   # MISSING: balding_hair
        'top': 'sleeved', 'neck': 'high', 'bottom': 'long', 'wraps': 'shoes',   # MISSING: jacket pockets
        'coat_length': 0.28,                    # the jacket below the hip (0.2 still read waist-length)
        'bands': [('upperarm', 'tint', 0.06)],
        'cape': False, 'cuirass': False, 'sandals': False, 'bracers': False, 'gear': [],
        'weapon': None, 'offhand': None,
        'stance_name': 'hands_clasped',
        'tint': [],
        'grime': 0.3, 'upper_bulk': 1.05,
        'cloth_rough': 0.95,                    # the suit cloth read flat
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
                   'bottom': '#f0ece0', 'belt': '#b08a2e', 'sandal': '#b08a2e', 'gold': '#b08a2e',
                   'wood': '#a8842a', 'leather': '#a8842a'},
        'helmet': None, 'headgear': 'nemes',    # gold and civ-color stripes, lappets, the gold uraeus
        'hair': 'bald', 'hair_asset': None, 'beard': False,
        'top': 'vest', 'cloth_rough': 0.95,     # the white linen read plastic
        'bottom': 'kilt', 'robe_drop': 0.42,    # MISSING: front_apron (a hanging panel; the front panel stands in)
        # the linen pleats round the kilt and the tinted apron down the front
        'patterns': {'bottom': [('pleats', '#d6d0c0', 40.0), ('panel', 'tint', 0.07, 'front')]},
        'hem_bands': [('bottom', 'tint', 0.05, 0.12), ('bottom', 'tint', 0.05, 0.32)],   # check: were dashes (patterns now follow the rest pose)
        'bands': [('neck', 'gold'), ('upperarm', 'gold'), ('wrist', 'gold')],   # MISSING: broad_collar
        'cape': False, 'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False, 'gear': [],
        # the gold crook; MISSING: crook extras (tinted bands; they're lapis)
        'weapon': 'crook', 'props': {'crook': ('crook', {'length': 1.1})},
        # the crook low in the right fist, rising up and out; check the hook clears the hat
        'stance_name': 'fist_hip_l', 'hang_dir': (-0.6, -0.3, 0.75),   # MISSING: walking stance
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
        'colors': {'skin': '#e4c8b4', 'hair': '#9a4a26', 'helmet': '#b08a2e', 'belt': '#b08a2e',
                   'gold': '#b08a2e', 'wood': '#a8842a', 'leather': '#a8842a',
                   'boots': '#3a2418', 'bottom': '#b89a4a'},
        'gender': 0.0, 'helmet': 'crown', 'crown': 'spiked', 'crown_height': 0.1,
        # short hair on the crown, off the face (bob01 covered it); MISSING: updo
        'hair': 'short', 'hair_asset': None, 'crown_hair': True, 'beard': False,
        'top': 'sleeved', 'puffy': True, 'neck': 'high',
        'bottom': 'gown', 'gown_flare': 3.0, 'robe_drop': 0.55,   # the bell farthingale; MISSING: jewel dots, gold front panel
        # the farthingale: gold-tan with civ-color stripes, as the original
        'patterns': {'bottom': ('pleats', 'tint', 16.0)},   # stripes running round the skirt ('stripes' made rings)
        'hem_bands': [('bottom', 'gold', 0.05)],
        'ruff': '#f2efe6', 'ruff_size': 0.12, 'ruff_style': 'fan',   # the tall lace fan standing behind the head (about the head's height)
        'cape': False, 'cuirass': False, 'wraps': 'boots', 'sandals': False, 'bracers': False, 'gear': [],
        # the gold scepter (plain knob head), tilted across the chest
        'weapon': 'scepter', 'props': {'scepter': ('scepter', {'length': 0.7, 'head': 'fleur'})},
        'stance_name': 'royal', 'upright_dir': (0.5, -0.15, 1.0),
        'tint': ['top', 'sleeves'],
        'grime': 0.3, 'upper_bulk': 1.0,
    },
    'King French': {
        # Joan / a French king in full silver plate, bareheaded, auburn hair;
        # a floor-length civ-color cape and shoulder mantle with a gold
        # fleur-de-lis; a brown-hilted sword low and forward
        'macro': {'muscle': 0.9, 'weight': 0.75, 'height': 0.8, 'proportions': 0.72}, 'age': 0.5,
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#e0b494', 'hair': '#8a3a1c', 'cuirass': '#9aa0aa',   # brighter: the armor read as a grey mass at 1x
                   'top': '#6a6e76',
                   'bottom': '#666a72', 'greaves': '#6a6e76', 'belt': '#3a2a1c', 'boots': '#6e727a',
                   'steel': '#7a7e86', 'leather': '#4a3020', 'mail': '#5e626a'},
        'helmet': None, 'hair': 'short', 'hair_asset': 'short02', 'beard': False,
        'cuirass': True, 'metal': ['cuirass'], 'neck': 'high', 'top': 'sleeved', 'pauldrons': True,
        'bottom': 'long', 'wraps': 'greaves', 'boots': True,   # MISSING: plate_legs, plate_arms (metal, not cloth), sabatons
        'cape': True, 'cape_length': 1.38,      # MISSING: shoulder_mantle (tinted)
        'cape_emblem': ('fleur', 'gold', 0.5),  # the gold fleur-de-lis (outline shape)
        'hem_bands': [('cape', 'gold', 0.03)],  # the gold edge trim
        'sandals': False, 'bracers': False, 'gear': [],
        # held low by 'stand', hanging down and a little forward; MISSING: held_low_forward (point up)
        'weapon': 'sword', 'props': {'sword': ('long_sword', {'length': 0.9, 'guard': 0.2})},
        'stance_name': 'stand', 'hang_dir': (-0.3, -0.45, -0.85),
        # head and chest lifted (-y is forward): the shoulders and head hunched under the cape
        'pose_extra': [('head', (0, 0.4, 1)), ('spine_03', (0, 0.08, 1))],
        'tint': ['cape'],
        'grime': 0.45, 'upper_bulk': 1.25,
    },
    'King German': {
        # Bismarck: a silver pickelhaube, a big mustache, a pale blue-grey
        # frock coat, gold epaulettes and cuffs, a civ-color sash shoulder to
        # hip, a white belt, black trousers and boots; left fist on hip
        'macro': {'muscle': 0.85, 'weight': 0.9, 'height': 0.82, 'proportions': 0.7}, 'age': 0.75,
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'cloth_rough': 0.95,                    # the coat cloth read flat and plastic
        'colors': {'skin': '#d8ac8c', 'hair': '#d8d0c0', 'helmet': '#6e727a', 'top': '#8a9cb0',
                   'bottom': '#18181a', 'boots': '#121214', 'belt': '#e8e4dc', 'gold': '#b08a2e',
                   'cuirass': '#b08a2e'},   # the epaulettes' gold (the cuirass is otherwise unused)
        'helmet': None, 'headgear': 'pickelhaube', 'brow_drop': 0.06, 'metal': [],   # off the eyes; silver (the helmet color), gold spike and plate
        'hair': 'short', 'hair_asset': None,
        'beard': True, 'beard_style': 'mustache', 'beard_length_short': 0.026, 'beard_density': 900,   # the big walrus mustache
        'top': 'sleeved', 'neck': 'high',
        'pauldrons': 'metal',                   # the gold epaulettes (the cuirass color, plate bands)
        'coat_length': 0.55, 'coat_flare': 1.05,   # the frock coat's skirts
        'bottom': 'long', 'wraps': 'boots',
        'gear': [('chest_strap', {'width': 0.16})],   # the tinted diagonal sash
        'bands': [('wrist', 'gold')],
        'cape': False, 'cuirass': False, 'sandals': False, 'bracers': False,
        'weapon': None, 'offhand': None,
        'stance_name': 'fist_hip_l',            # left fist on the hip, the right arm hanging
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
                   'belt': '#b08a2e', 'sandal': '#b08a2e', 'gold': '#b08a2e', 'helmet': '#b08a2e'},
        'helmet': 'headband', 'hair': 'short', 'hair_asset': 'short04', 'beard': False,
        'wild_hair': 0.04,                      # short tousled curls over the cap; MISSING: curly_hair (true curls)
        'top': 'vest', 'bottom': 'kilt', 'robe_drop': 0.45,
        # MISSING: himation (draped over the left shoulder, wrapping the hips to below the knee)
        'gear': [('chest_strap', {'color': '#e8e4da', 'width': 0.3, 'offset': 0.03})], 'cape': False,
        # no gold neck band: even at 0.012 it read as a shoulder yoke
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
        'cloth_rough': 0.95,                    # the cream robe read plastic
        'colors': {'skin': '#a88262', 'hair': '#3e2c20', 'beard': '#5e4a38', 'helmet': '#b08a2e', 'top': '#d8ccaa',
                   'bottom': '#d8ccaa', 'belt': '#b08a2e', 'sandal': '#4a3622', 'gold': '#b08a2e',
                   'wood': '#a8842a', 'steel': '#b08a2e', 'iron': '#b08a2e'},
        'helmet': 'crown', 'crown_lift': 0.03, 'crown': 'circlet', 'crown_height': 0.07,   # taller: at 0.05 the circlet was lost in the hair
        # the long beard lighter than the hair and kept below the cheeks, so it doesn't merge with the face
        'hair': 'short', 'hair_asset': 'short02', 'beard': True, 'beard_style': 'long',
        'beard_length_short': 0.012, 'beard_density': 500,
        'top': 'sleeved', 'neck': 'high',
        'cuirass': True, 'metal': [],           # the tinted shoulder shawl (the chest read cream)
        'bottom': 'kilt', 'robe_drop': 0.55,    # MISSING: seam line
        # the maroon front apron as a panel (it also runs down the back: MISSING front_only)
        'patterns': {'bottom': ('panel', '#6a1e22', 0.08)},
        'hem_bands': [('bottom', 'tint', 0.04)],
        'bands': [('waist', 'tint'), ('wrist', 'tint')],
        'cape': False, 'wraps': None, 'sandals': True, 'bracers': False, 'gear': [],
        # the small gold axe, raised
        'weapon': 'axe', 'props': {'axe': ('battle_axe', {'length': 0.55})},
        'stance_name': 'royal',
        'tint': ['cuirass'],
        'grime': 0.45, 'upper_bulk': 1.2,
    },
    'King Incan': {
        # Pachacuti: a gold crown with a front disc, a knee-length gold and
        # brown checkerboard tunic, a heavy civ-color cape to the knee with a
        # gold band and white zig-zag, gold leg bands; a tall feather-topped
        # staff planted at his left
        'macro': {'muscle': 0.85, 'weight': 0.8, 'height': 0.74, 'proportions': 0.68}, 'age': 0.6,
        'race': {'caucasian': 0.0, 'african': 0.15, 'asian': 0.85},
        'colors': {'skin': '#8a6448', 'hair': '#141010', 'helmet': '#b08a2e', 'top': '#b0904a',
                   'bottom': '#b0904a', 'belt': '#b08a2e', 'sandal': '#b89a50', 'gold': '#b08a2e',
                   'wood': '#3e2a1a'},
        # MISSING: crown_disc (gold, at the front), crown_top (the original's red/tinted top; the note says plain gold)
        'helmet': 'crown', 'crown': 'tall', 'crown_height': 0.07,   # a gold cylinder (the circlet read thin)
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
                   'cape': '#e4e2dc', 'belt': '#c8c2b4', 'sandal': '#5a3a22', 'wood': '#5a3a22',
                   'lens': '#7a5a42', 'gold': '#1e1a18'},   # clear-ish skin-toned lenses (white read as glowing eyes), dark wire rims
        'helmet': None, 'hair': 'bald', 'hair_asset': None, 'beard': False,
        'face_prop': ('glasses', {'kind': 'round'}),   # the round glasses
        'top': 'mantle', 'bottom': 'kilt', 'robe_drop': 0.0,   # MISSING: dhoti (wrapped between the legs)
        'cape': False,
        'gear': [('chest_strap', {'width': 0.12})],   # the sash, the original's main civ mark
        'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False,
        # MISSING: tinted_tip (the stick's top; no key on 'staff')
        'weapon': 'staff', 'props': {'staff': ('staff', {'length': 1.55, 'grip': 0.95})},
        'pose_extra': [('spine_02', (0, -0.18, 1)), ('neck_01', (0, -0.12, 1)), ('head', (0, 0.25, 1))],   # the walking stoop (-y forward)
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
                   'steel': '#8a8c90', 'shield_face': '#d8ccb0', 'shield_rim': '#8a6a48', 'feather': '#e8e4da'},
        'helmet': None, 'headgear': 'war_bonnet', 'headgear_scale': 0.62,   # white feathers, tinted tips and headband, the trailer (smaller again: 2-5 px clip at the cell top, dirs 5-7)
        'hair': 'long', 'hair_asset': 'long01', 'beard': False,
        'top': None, 'bottom': 'loincloth', 'loin_length': (0.5, 0.45), 'torn': False,
        'wraps': 'wraps', 'sandals': False,     # MISSING: leggings (brown cloth), moccasins
        'gear': [('chest_strap', {'width': 0.10})],
        'bands': [('upperarm', 'tint'), ('knee', 'tint')],
        'cape': False, 'cuirass': False, 'bracers': False,
        # MISSING: feathered_spear (fur/feather wrap below the head)
        # upright from the low right fist; check the butt meets the ground
        'weapon': 'spear', 'props': {'spear': ('spear', {'length': 2.0, 'grip': 0.8})}, 'hang_dir': (-0.05, 0.0, 1.0),
        'offhand': 'shield_round', 'offhand_mesh': ('shield_round', {'r': 0.25, 'boss': False, 'tint': 'none'}),
        'shield_at': 'back',                # BUG: it sits on the front of the thigh, not at the hip side
        'stance_name': 'fist_hip_l',            # left fist on the hip
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
                   'shoes': '#e8e4dc', 'collar_w': '#ece8e0', 'gold': '#c8a038'},
        # the black topknot rising straight up on short crown hair (ponytail01 fell back)
        'helmet': None, 'headgear': 'eboshi', 'hair': 'short', 'hair_asset': None,   # the tall civ court cap
        'beard': False,
        'top': 'sleeved', 'wide_sleeves': False, 'neck': 'high',
        'bottom': 'kilt', 'robe_drop': 0.48,
        'patterns': {'bottom': [('panel', '#c8a038', 0.135, 'front'), ('panel', '#2a2a30', 0.075, 'front')]},   # the dark front panel, wide gold edges
        'hem_bands': [('bottom', 'gold', 0.04)],
        'cuirass': False,
        'bands': [('neck', 'collar_w', 0.08), ('upperarm', 'gold', 0.06), ('wrist', 'gold', 0.1), ('waist', 'belt', 0.06)],   # the collar, gold shoulder bands and cuffs, the white belt
        'chest_emblem': ('v', '#ece8e0', 0.32),   # the cream V collar
        'cape': False, 'wraps': 'shoes', 'sandals': False, 'bracers': False, 'gear': [],   # the white tabi
        'weapon': None, 'offhand': None,
        'stance_name': 'fists_on_hips',
        'tint': ['top', 'sleeves', 'bottom', 'hat'],   # the robe civ to the ankles (as the original), and the cap
        'grime': 0.3, 'upper_bulk': 1.15,
    },
    'King Korean': {
        # Wang Kon: shaved head, a golden-yellow ankle robe with wide sleeves,
        # civ-color collar, front stripes and sleeve edges; both hands
        # together at the chest holding a small dark tablet
        'macro': {'muscle': 0.75, 'weight': 0.75, 'height': 0.74, 'proportions': 0.7}, 'age': 0.6,
        'race': {'caucasian': 0.0, 'african': 0.0, 'asian': 1.0},
        'colors': {'skin': '#c8a68a', 'hair': '#141010', 'top': '#d8b850', 'bottom': '#d8b850',   # the golden-yellow robe
                   'belt': '#8a6a2a', 'boots': '#3a2a1e', 'book_cover': '#2a1c14'},
        'helmet': None, 'hair': 'bald', 'hair_asset': None, 'beard': False,
        'top': 'sleeved', 'wide_sleeves': True, 'neck': 'high',
        'bottom': 'kilt', 'robe_drop': 0.48,
        # the civ robe stripes: down the front, the sides and the back; the sleeve edges
        'patterns': {'top': [('stripes', 'tint', 3.2), ('panel', 'tint', 0.08, 'front')],
                     'bottom': [('stripes', 'tint', 3.2), ('panel', 'tint', 0.08, 'front')]},
        'hem_bands': [('sleeves', 'tint', 0.08)],
        'bands': [('neck', 'tint'), ('wrist', 'tint')],   # collar and sleeve edges (check the cuffs show past the clasped hands)
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
                   'belt': '#c8a038', 'sandal': '#5a3a22', 'gold': '#c8a038', 'plume': '#2e7a4a',
                   'feather': '#ece8e0'},
        # the tinted crest and the long white feather cascade down the back
        # MISSING: a third feather color (feather_headdress ignores 'plume': the fan is tint/white, not green/red/white)
        'helmet': None, 'headgear': 'feather_headdress', 'headgear_params': {'cascade': 'back', 'front': True, 'crest': 'tint'},   # a solid civ crest   # red and green front feathers
        'hair': 'short', 'hair_asset': 'bob01', 'beard': False,   # MISSING: face_ornaments (gold)
        'top': None, 'bottom': 'kilt', 'robe_drop': 0.0,
        'patterns': {'bottom': ('stripes', '#3a2a18', 9.0)},   # the kilt's gold and dark jaguar stripes
        'bands': [('neck', 'gold'), ('upperarm', 'gold'), ('wrist', 'gold')],
        'chest_emblem': ('disc', 'gold', 0.3),   # the broad gold pectoral
        'cape': True, 'cape_length': 1.3,
        'hem_bands': [('cape', 'gold', 0.1)],   # the gold hem on the cape
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
                   'leather': '#3a2a1c', 'fur': '#8a8a88'},   # the grey fur brim
        'helmet': None, 'headgear': 'fur_hat',
        'hair': 'short', 'hair_asset': None,
        'beard': True, 'beard_style': 'goatee',   # MISSING: long_mustache (drooping past the chin)
        'top': 'sleeved', 'neck': 'high', 'bottom': 'kilt', 'robe_drop': 0.22,
        'hem_bands': [('bottom', 'tint', 0.06)],
        'bands': [('wrist', 'tint')],
        'cuirass': True, 'metal': [], 'cuirass_trim': '#e0b840', 'trim_width': 0.045,   # the civ chest vest, gold edged
        'wraps': 'boots',
        'cape': False, 'sandals': False, 'bracers': False, 'gear': [],
        'weapon': 'sword', 'props': {'sword': ('scimitar', {'length': 0.85})},
        'stance_name': 'stand',
        'tint': ['helmet', 'cuirass'],
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
                   'shoes': '#1e1a18', 'wraps': '#ece8e0', 'wraps_band': '#ece8e0',
                   'wood': '#2a1e16', 'lacquer': '#2a1e16', 'gold': '#c8a038'},
        'helmet': None, 'hair': 'long', 'hair_asset': 'long01',   # MISSING: periwig (curly, to the shoulders)
        'beard': False,
        'top': 'sleeved', 'neck': 'high',
        'patterns': {'top': ('stripes', '#a8242a', 10.0)},   # the doublet's red slashes (stripes stand in for dashes)
        # white breeches and hose in one: full-length puffy legs, black low shoes
        'bottom': 'breeches', 'puffy': True, 'trouser_offset': 0.04, 'wraps': 'wraps', 'shoes': True, 'sandals': False,   # short puffed civ breeches over white hose
        # the enormous robe: a wide bell flaring onto the ground, the gold hem pooling behind
        'cape': True, 'cape_length': 1.5, 'cape_flare': 1.9, 'cape_train': 0.9, 'cape_width': 0.7,   # a wide bell with a train
        'hem_bands': [('cape', 'gold', 0.14), ('cape', 'ermine', 0.12, 0.14)],   # the gold hem, ermine above it
        'cape_lining': '#3a2040',   # the dark purple lining
        'cuirass': False, 'bracers': False, 'gear': [],
        # the gold-topped cane in the free left fist ('fist_hip_r' puts the item there), the rod down to the
        # ground ('tool_dir' is the way it runs from the fist); check the tip meets the ground
        'weapon': 'cane', 'props': {'cane': ('cane', {'length': 1.0})},
        'stance_name': 'stand_staff',           # the cane planted at his side (no hand raised to the head)
        'tint': ['cape', 'bottom'],   # the civ breeches, as the original
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
                   'bottom': '#ece8de', 'belt': '#c8a038', 'shoes': '#5a3a22', 'gold': '#c8a038',   # brown shoes
                   'turban_white': '#f0ece2', 'jewel': '#a8242a'},
        # the white turban, a gold-set jewel and a plume ('plume' in tint: civ-colored)
        'helmet': None, 'headgear': 'turban_aigrette', 'headgear_scale': 0.9,   # rounder
        'hair': 'short', 'hair_asset': None, 'beard': True,
        'top': 'sleeved', 'neck': 'high',
        # MISSING: side slits, skirt_panels (tinted triangles at the front, a large arc at the back)
        'bottom': 'kilt', 'robe_drop': 0.5,
        'hem_bands': [('bottom', 'tint', 0.12)],
        'bands': [('waist', 'gold', 0.07)],   # the gold belt
        'cuirass': True, 'cuirass_trim': '#e0b840', 'trim_width': 0.05,   # the tinted waist-length vest, gold edges
        'cape': False, 'wraps': 'shoes', 'sandals': False, 'bracers': False, 'gear': [],   # the gold shoes
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
        'colors': {'skin': '#b08664', 'hair': '#3a2616', 'helmet': '#c8a038', 'top': '#e8e4da',
                   'bottom': '#e8e4da', 'belt': '#c8a038', 'boots': '#5a3a22', 'bracers': '#c8a038',
                   'gold': '#c8a038'},
        'helmet': 'crown', 'crown': 'spiked', 'crown_height': 0.1,   # the big crown ('headgear_scale' is for headgear)
        'hair': 'long', 'hair_asset': 'long01', 'mane': 0.1,   # wavy brown hair
        'beard': True, 'beard_style': 'long',   # the square-cut beard to the chest; MISSING: wavy beard
        'top': 'tunic', 'neck': 'high', 'bottom': 'skirt', 'robe_drop': 0.0,   # short white sleeves, not off-shoulder
        'hem_bands': [('bottom', 'tint', 0.08)],   # check it renders on the 'skirt'
        'gear': [('chest_strap', {'width': 0.17, 'tint': True})],   # the broad civ diagonal band
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
        'colors': {'skin': '#d0aa8e', 'hair': '#3a2a1c', 'helmet': '#1e1a18', 'hat': '#1e1a18', 'top': '#3a2a1e', 'bottom': '#8a8a92', 'plume': '#1e1a18',   # grey hose
                   'cuirass': '#c8a038', 'wraps': '#9a9aa2', 'wraps_band': '#9a9aa2', 'beard': '#9a968e',
                   'boots': '#18161a', 'belt': '#c8a038', 'gold': '#c8a038', 'steel': '#b0b4b8',
                   'leather': '#2a1e16'},
        # the plume dyed the hat's black (the original has none); MISSING: wide_brim_hat (round, flat, even brim,
        # gold edge; cavalier stands in)
        'helmet': None, 'headgear': 'round_hat',   # flat, even brim with a gold edge, no plume
        'hair': 'long', 'hair_asset': 'long01', 'beard': True,   # the grey beard under dark hair
        'top': 'sleeved', 'puffy': True, 'neck': 'high',   # MISSING: doublet_trim (gold buttons and edges)
        'pauldrons': False, 'shoulder_rolls': 'gold', 'roll_size': 0.1,   # the big gold puffed shoulders
        'bottom': 'breeches',                   # the tinted trunk hose; MISSING: hose_slashes (dark, vertical)
        'hem_bands': [('bottom', 'gold', 0.03)],
        'patterns': {'top': [('panel', '#c8a038', 0.12, 'front'), ('panel', '#ece8e0', 0.07, 'front')]},   # the white shirt front, gold edged (gold slashes greyed the civ)   # the white shirt front, gold edged
        'wraps': 'wraps', 'shoes': True,        # the plain grey hose (band color = hose color: no stripes), black shoes
        'bands': [('wrist', 'gold', 0.06), ('knee', 'gold', 0.06)],   # gold cuffs and garters
        'cape': False, 'cuirass': False, 'sandals': False, 'bracers': False, 'gear': [],
        'weapon': 'sword', 'props': {'sword': ('sword', {'length': 0.62, 'guard': 0.16})},
        'stance_name': 'stand',                 # MISSING: hand_to_chest (left forearm raised)
        'tint': ['top'],   # the civ doublet (dark trunk hose), as the original
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
        'chest_emblem': ('disc', 'tint', 0.22),   # the civ panel on the cuirass
        'cuirass': True, 'metal': ['cuirass'], 'neck': 'high',   # MISSING: cuirass_panels (tinted at chest and shoulders)
        'top': 'tunic',
        # MISSING: pteruges (strip skirt; it renders on top of the cape in the back views)
        'bottom': 'loincloth', 'loin_length': (0.42, 0.4), 'torn': False,
        'wraps': 'boots',
        'cape': True, 'cape_length': 1.05,      # MISSING: cape_drape (over the left shoulder, wrapping the arm)
        'sandals': False, 'bracers': False, 'gear': [],
        # the gold eagle-topped staff, the butt at the ground (check the eagle stays in frame)
        'weapon': 'standard', 'props': {'standard': ('eagle_standard', {'length': 1.85, 'grip': 1.0})},
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
        'colors': {'skin': '#e4c8b4', 'hair': '#6a4a2c', 'helmet': '#b8bcc4', 'top': '#b8bcc4', 'sleeves': '#c8ccd4', 'bottom': '#c0c4cc',
                   'belt': '#c8a038', 'gold': '#c8a038', 'wood': '#b8902e', 'leather': '#b8902e'},
        'gender': 0.0, 'helmet': 'crown', 'crown': 'arched', 'crown_gems': '#a8242a', 'crown_height': 0.16,   # MISSING: crown_silver
        'hair': 'short', 'hair_asset': None, 'beard': False,   # MISSING: updo (bun)
        'top': 'sleeved', 'puffy': True, 'neck': 'high',
        # the vast bell skirt in vertical stripes of civ color and silver; MISSING: lace_hem
        'bottom': 'gown', 'gown_flare': 4.0, 'robe_drop': 0.62,
        'patterns': {'bottom': ('camo', 'tint', 7.0, 'cover', 0.65), 'top': ('panel', '#c8a038', 0.1, 'front')},   # silver brocade stripes on the civ bell, a gold bodice front
        'hem_bands': [('bottom', '#f2efe8', 0.07)],   # the white lace frill
        'cape': False, 'cuirass': False, 'wraps': None, 'sandals': False, 'bracers': False,
        'gear': [],
        # the short gold scepter raised by the shoulder, the orb in the raised left hand
        'weapon': 'scepter', 'props': {'scepter': ('scepter', {'length': 0.6, 'head': 'fleur'})}, 'offhand': 'orb', 'offhand_dir': (0, 0, 1),
        'pose_extra': [('lowerarm_l', (0.1, -1.0, 0.35))],
        'stance_name': 'royal',
        'tint': ['top'],   # the civ bodice; the gown is silver under civ blotches (a hex camo on a civ part got tinted dark)
        'grime': 0.12, 'upper_bulk': 1.0,
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
        'gender': 0.0, 'helmet': 'crown', 'crown': 'spiked', 'crown_gems': '#a8242a', 'crown_height': 0.15,   # the tall open crown with points
        'hair': 'short', 'hair_asset': 'bob01', 'beard': False,
        'top': 'sleeved', 'neck': 'high',
        # the white front panel between civ-color sides; MISSING: fleur_pattern (gold)
        'bottom': 'gown', 'gown_flare': 2.4, 'robe_drop': 0.62,
        'patterns': {'bottom': ('panel', '#ece8e4', 0.13)},
        'hem_bands': [('bottom', '#f2efe8', 0.08, 0.0), ('bottom', 'gold', 0.06, 0.08)],   # gold and white lace at the hem
        # the white veil to the hips, narrow and close to the back; MISSING: head_veil (from the head, over the crown)
        'cape': True, 'cape_length': 0.9, 'cape_width': 0.4, 'cape_flare': 0.25, 'cape_wrap': 0.45,   # close to the back
        'cuirass': False, 'wraps': None, 'sandals': False, 'bracers': False, 'gear': [],
        # the gold fleur-headed scepter low in the right fist, head up and forward
        'weapon': 'scepter', 'props': {'scepter': ('scepter', {'length': 0.7, 'head': 'fleur'})},
        'stance_name': 'fist_hip_l', 'hang_dir': (-0.25, -0.65, 0.7),   # left fist on the hip
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
                   'top': '#e4dcc8', 'bottom': '#6a4430',   # the cream shawl over the chest 'belt': '#8a3226', 'redband': '#8a3226',
                   'cuff': '#ece8e0', 'sandal': '#4a3622', 'wood': '#6a4a2a',   # a wooden rod
                   'turban_white': '#ece6da', 'gold': '#c8a038'},
        # the tall white crown on a gold band (it tapers to a gold knob; the original's is flat-topped)
        'helmet': None, 'headgear': 'sumer_crown',
        'hair': 'long', 'hair_asset': 'long01', 'mane': 0.15,
        'beard': True, 'beard_style': 'long',   # the long square black beard
        'top': 'mantle', 'neck': 'high',        # the brown wrap
        'bottom': 'kilt', 'robe_drop': 0.46,
        'patterns': {'bottom': [('panel', '#c8a038', 0.1, 'front'), ('panel', '#1a1612', 0.035, 'front')],   # the gold and black front panel
                     'top': ('stripes', '#8a3226', 5.0)},   # the shawl's red pattern, bolder
        'hem_bands': [('bottom', '#8a3226', 0.06, 0.0), ('bottom', '#ece8e0', 0.04, 0.07), ('bottom', '#8a3226', 0.05, 0.16)],   # red and white hem borders
        'bands': [('waist', 'redband', 0.07), ('wrist', 'cuff'), ('upperarm', 'tint', 0.08)],   # the red waist band, civ cap sleeves
        'cape': False, 'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False, 'gear': [],
        # MISSING: staff_ball (a ball head on the staff)
        'weapon': 'staff', 'props': {'staff': ('staff', {'length': 1.3})},
        'stance_name': 'low_across', 'two_hands': True, 'tool_dir': (-0.95, -0.25, 0.15),   # the rod level across the chest
        'tint': ['bottom'],   # the long skirt in civ color, front and back
        'grime': 0.5, 'upper_bulk': 1.3,
    },
    'King Viking': {
        # Ragnar: a steel conical helmet with gold rim and nasal, long
        # reddish-blond hair and beard, a grey mail tunic with gold belt and
        # collar, brown baggy trousers, wrapped boots, an ankle-length civ-
        # color cape; a long sword point-down behind the leg
        'macro': {'muscle': 1.0, 'weight': 0.9, 'height': 0.82, 'proportions': 0.65}, 'age': 0.55,
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#d0a888', 'hair': '#c8803a', 'beard': '#c8803a', 'top': '#6e7278', 'mail': '#6e7278',
                   'bottom': '#5a4630', 'belt': '#4a3020', 'boots': '#4a3a2c', 'gold': '#c8a038',
                   'steel': '#a8acb0', 'leather': '#3a2818', 'helmet': '#8a8e94', 'bronze': '#c8a038'},
        'helmet': None, 'headgear': 'viking_bowl',   # a round civ bowl, gold rim, crest strip and nasal
        'hair': 'long', 'hair_asset': 'long01', 'mane': 0.2, 'beard': True, 'beard_style': 'long',   # the long hair shows behind the helmet
        'top': 'sleeved', 'neck': 'high', 'collar': 'mail',   # MISSING: mail_shirt (rings on the whole tunic)
        'bottom': 'long', 'puffy': True, 'wraps': 'boots',
        'bands': [('neck', 'gold', 0.08), ('waist', 'gold', 0.07), ('upperarm', 'gold', 0.05)],   # gold collar, belt and arm bands
        'chest_strap': 'cross', 'gear': [('chest_strap', {'color': '#c8a038', 'width': 0.05, 'cross': True})],   # the gold chest bands
        'cape': True, 'cape_length': 1.3,       # MISSING: cape_closed_back (it splits open down the back)
        'cuirass': False, 'sandals': False, 'bracers': False,
        'weapon': 'sword', 'props': {'sword': ('long_sword', {'length': 0.95})},
        'stance_name': 'stand',                 # MISSING: sword_point_down_back
        'tint': ['cape', 'helmet'],   # the civ helmet bowl, as the original
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
        'colors': {'skin': '#5a3e2a', 'hair': '#141010', 'helmet': '#c4ac78', 'plume': '#c4ac78',
                   'feather': '#c4ac78',   # the compact tan crown
                   'bottom': '#c4ac78', 'cape': '#b89c68', 'belt': '#6a5032',   # the tan fur kilt
                   'wood': '#b89a6a', 'stone': '#b89a6a', 'leather': '#6a5032', 'hide': '#ece8e0',
                   'hide_dark': '#2a1e16'},
        # the dense untinted crown of cream feathers on a fur band (fur takes the helmet color)
        'helmet': None, 'headgear': 'feather_headdress', 'headgear_params': {'style': 'crown'}, 'headgear_scale': 0.7,
        'hair': 'short', 'hair_asset': 'afro01', 'beard': False,
        'top': None,
        'bottom': 'kilt', 'robe_drop': 0.0,     # MISSING: apron_kilt
        'hem_bands': [('bottom', 'tint', 0.05)],   # the civ kilt bands
        'cape': False, 'fur_vest': ('#c4ac78', '#6a5032'),   # the fur collar
        'bands': [('upperarm', 'tint', 0.07), ('knee', 'tint', 0.07)],   # the civ armlets and below-knee bands
        'cuirass': False, 'wraps': None, 'sandals': False, 'bracers': False, 'gear': [],
        # the short stabbing spear (as on the Impi)
        'weapon': 'club', 'props': {'club': ('knobkerrie', {'length': 0.75})},   # the knobkerrie, angled forward and down
        # MISSING: tinted_shield_stripes (a red shield with white bars; cowhide_shield has no tint)
        'offhand': 'shield_cowhide', 'offhand_mesh': ('cowhide_shield', {'w': 0.5, 'h': 1.1, 'bars': True, 'tuft': True}),   # civ bars, a white tuft
        'stance_name': 'guard', 'hang_dir': (-0.2, -0.6, -0.7),
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
        'helmet': None, 'hair': 'bald', 'hair_asset': None,
        'beard': True, 'beard_style': 'long',   # the white beard to the chest
        'top': 'sleeved', 'wide_sleeves': True, 'neck': 'high',
        'bottom': 'kilt', 'robe_drop': 0.46,
        # the white gown down the open front, its edges in civ color
        'patterns': {'top': [('panel', 'tint', 0.155, 'front'), ('panel', '#ece8e0', 0.11, 'front')],
                     'bottom': [('panel', 'tint', 0.155, 'front'), ('panel', '#ece8e0', 0.11, 'front')]},   # the white gown, civ edges
        'hem_bands': [('bottom', 'tint', 0.07, 0.02), ('bottom', 'tint', 0.06, 0.12)],
        'bands': [('wrist', 'tint'), ('neck', 'tint')],
        'hem_bands': [('bottom', 'tint', 0.07, 0.02), ('bottom', 'tint', 0.06, 0.12), ('sleeves', 'tint', 0.08)],   # the cuffs too
        'cape': False, 'cuirass': False, 'wraps': None, 'sandals': True, 'bracers': False, 'gear': [],
        # the shepherd's crook, wooden (gold/lapis recolored); check the hook is in frame and the butt reaches the
        # ground; MISSING: crook extras (a grip: the rod reaches only 0.2 below the fist)
        'weapon': 'crook', 'props': {'crook': ('crook', {'length': 1.6})},   # shorter: the hook stays in frame
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
        'colors': {'skin': '#e0b89a', 'hair': '#ffffff', 'bottom': '#a0a2a8', 'boots': '#141416',
                   'belt': '#a0a2a8', 'gold': '#c8a038', 'cuff': '#ece8e0', 'cuirass': '#c8a038'},
        'helmet': None, 'hair': 'topknot', 'hair_asset': 'ponytail01', 'beard': False,   # MISSING: powdered_wig (side rolls)
        # MISSING: waistcoat_front (white; a 'cuirass' strip would also recolor the gold 'metal' pauldrons, and
        # a top 'panel' would run down the back and the coat skirts)
        'top': 'sleeved', 'neck': 'high',
        'coat_length': 0.55,                    # the frock coat's skirts
        'pauldrons': 'metal',                   # the gold epaulettes (in the cuirass's gold)
        'bottom': 'breeches', 'wraps': 'boots',
        'bands': [('neck', 'gold'), ('wrist', 'cuff', 0.07)],   # white cuffs
        'patterns': {'top': ('panel', '#ece8e0', 0.11, 'front')},   # the white waistcoat front
        'cape': False, 'cuirass': False, 'sandals': False, 'bracers': False, 'gear': [],
        'weapon': None, 'offhand': None,        # MISSING: tricorne_in_hand (small dark object at the waist)
        'stance_name': 'fist_hip_l',            # left fist on the hip, the right hand free
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
        'cuirass': True, 'metal': [], 'neck': 'high',   # the civ surcoat over the chest, as the original
        'top': 'sleeved', 'pauldrons': True, 'collar': 'mail',   # MISSING: plate_arms, plate_legs
        'bottom': 'kilt', 'robe_drop': 0.0, 'wraps': 'greaves', 'boots': True,
        # MISSING: the lion on the gold field, ermine_trim (collar)
        'cape': True, 'cape_length': 1.38, 'cape_flare': 0.9,   # a bell
        'cape_emblem': ('lion', 'gold', 0.5),   # the gold rampant lion on the back
        'hem_bands': [('cape', '#f0ece4', 0.15), ('cape', '#b09a6a', 0.07, 0.3), ('cape', '#b09a6a', 0.07, 0.55)],   # fur hem, tan bands
        'sandals': False, 'bracers': False, 'gear': [],
        'weapon': 'sword', 'props': {'sword': ('long_sword', {'length': 0.95})},
        'stance_name': 'stand',
        'tint': ['cape', 'cuirass'],
        'grime': 0.45, 'upper_bulk': 1.3,
    },
    'Leader Modern Times': {
        # a general: green peaked cap, goatee, olive tunic, a broad gold-edged
        # civ-color sash shoulder to hip, tinted cuffs and shoulder patch,
        # khaki breeches, tall brown boots; a swagger stick level at the chest
        'macro': {'muscle': 0.8, 'weight': 0.75, 'height': 0.78, 'proportions': 0.72}, 'age': 0.6,
        'colors': {'skin': '#c8a48a', 'hair': '#2a1e14', 'helmet': '#4a5038', 'top': '#545a40',
                   'bottom': '#9a907a', 'boots': '#4e3828', 'belt': '#3a2a1c', 'gold': '#c8a038'},
        # the green peaked cap (its crown takes the helmet color), civ band, black peak, gold badge
        'helmet': None, 'headgear': 'peaked_cap',
        'hair': 'short', 'hair_asset': None,
        'beard': True, 'beard_style': 'goatee',
        'top': 'sleeved', 'neck': 'high', 'bottom': 'breeches', 'wraps': 'boots',
        'gear': [('chest_strap', {'width': 0.16, 'edge': 'gold'})],   # the broad civ sash, gold edged
        'shoulder_patch': ('l', 'tint'), 'patch_size': 0.1, 'patch_out': 0.04,
        'bands': [('wrist', 'tint')],
        'cape': False, 'cuirass': False, 'sandals': False, 'bracers': False,
        'weapon': 'stick', 'props': {'stick': ('swagger_stick', {'length': 0.6})}, 'offhand': None,   # level, forward
        'stance_name': 'port_arms', 'gun_dir': (0.95, -0.15, 0.1), 'fore_end': 0.28,   # the stick level across the chest
        'pose_extra': [('upperarm_r', (-0.3, -0.25, -1)), ('lowerarm_r', (0.3, -1.0, 0.35)),
                       ('upperarm_l', (0.3, -0.25, -1)), ('lowerarm_l', (-0.3, -1.0, 0.35))],   # the hands up at the chest
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
        'chest_emblem': None,
        'bands': [('neck', 'tint'), ('upperarm', 'tint'), ('waist', 'gold', 0.05)],   # choker, glove tops, the gold waistband
        'bottom': 'gown', 'robe_drop': 0.6, 'gown_flare': 1.8,   # a fuller column
        # the gold front stripe (it also runs down the back, which the original hasn't: MISSING front_only)
        'patterns': {'bottom': [('camo', 'tint', 8.0, 'cover', 0.14), ('panel', '#c8a038', 0.035, 'front')]},   # the civ spots, the gold stripe   # the gold front stripe
        'hem_bands': [('bottom', 'tint', 0.07), ('bottom', 'tint', 0.05, 0.12)],   # the civ hem and a band of civ above it
        'bracers': False, 'long_gloves': ('arm', 'tint'),   # the long tinted gloves, over the hands
        
        'cape': False, 'cuirass': False, 'wraps': None, 'sandals': False, 'gear': [],
        # the short gold scepter raised; MISSING: left_hand_hold
        'weapon': 'scepter', 'props': {'scepter': ('scepter', {'length': 0.5, 'head': 'orb', 'shaft': 'tint'})},
        'stance_name': 'royal',
        'tint': ['top'],   # the civ bodice, as the original
        'grime': 0.3, 'upper_bulk': 0.9,
    },
    'SciLeader': {
        # Einstein: wild white hair and a big white mustache, a rumpled dark
        # grey suit, a tinted tie, a thin black cane planted in the right
        # hand, a brown doctor's bag (tinted straps) low in the left, a pipe;
        # slightly stooped
        'macro': {'muscle': 0.55, 'weight': 0.6, 'height': 0.72, 'proportions': 0.72}, 'age': 0.85,
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#d8ae8e', 'hair': '#ecebe6', 'beard': '#f4f2ee', 'top': '#5a5a5e', 'bottom': '#3a3a3e',
                   'shoes': '#2a2420', 'belt': '#2a2a2e', 'wood': '#18181a', 'leather': '#18181a',
                   'lacquer': '#18181a', 'gold': '#3a3a3e'},
        'helmet': None, 'hair': 'short', 'hair_asset': 'afro01', 'mane': 0.0,   # the big white shock (a mesh asset; 'mane' and wild_hair are particles: a ball over the face)
        'beard': True, 'beard_style': 'mustache',   # the big white mustache
        'top': 'sleeved', 'neck': 'high', 'bottom': 'long', 'wraps': 'shoes',   # MISSING: suit_jacket (open, lapels)
        # the tinted tie: a narrow non-metal 'cuirass' strip (it also runs down the back: MISSING tie, front only)
        'cuirass': False,
        'patterns': {'top': ('panel', 'tint', 0.035, 'front')},   # the civ tie, front only
        'chest_emblem': ('v', '#a8302a', 0.14),   # the red at the throat (a neck band hid under the collar)
        'cape': False, 'sandals': False, 'bracers': False, 'gear': [],
        # the thin black cane planted from the fist; MISSING: left_hand_hold (props.briefcase, brown,
        # tinted straps)
        'face_prop': 'pipe',
        'weapon': 'cane', 'props': {'cane': ('cane', {'length': 0.9})},
        'offhand': 'briefcase', 'offhand_mesh': ('briefcase', {'straps': 'tint'}), 'offhand_dir': (0, 0, 1),   # the doctor's bag, civ straps
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
        'colors': {'skin': '#c4a07e', 'hair': '#141010', 'cuirass': '#2e221a',
                   'belt': '#2a2830', 'boots': '#e8e4dc', 'sandal': '#d8d4cc', 'collar_w': '#e0dcd4',
                   'gold': '#c8a038', 'lacquer': '#1a1412', 'iron': '#3a3a3c', 'cloth_wrap': '#2a2630',
                   'steel': '#b8bcc2'},
        # the kabuto: a black bowl (as the original; the note tints it), lacquer neck-guard lames, gold kuwagata
        'helmet': None, 'headgear': 'kabuto', 'metal': [],
        'hair': 'short', 'hair_asset': None, 'beard': False,
        'top': 'sleeved', 'wide_sleeves': True, 'neck': 'high',
        'cuirass': True,    # the brown-black kataginu panel (leather: 'metal' is [] above); MISSING: kataginu (winged shoulders)
        # MISSING: robe_stripes (dark and gold, vertical, front only: the original's back is plain)
        'bottom': 'kilt', 'robe_drop': 0.5,
        'hem_bands': [('bottom', 'gold', 0.07), ('sleeves', 'gold', 0.06)],
        'patterns': {'bottom': [('panel', '#c8a038', 0.09, 'front'), ('panel', '#2a2218', 0.06, 'front')]},   # the dark front panel, gold edged
        'chest_emblem': ('v', '#e0dcd4', 0.24),   # the white V collar
        'bands': [('neck', 'collar_w'), ('wrist', 'gold')],
        'cape': False, 'wraps': 'boots', 'sandals': False, 'bracers': False, 'gear': [],   # the white tabi
        'weapon': 'sword', 'props': {'sword': ('katana', {'length': 0.95})},
        'stance_name': 'shoulder_arms',   # the hilt at the right shoulder, the blade up and back
        'tint': ['top', 'bottom', 'sleeves', 'helmet'],   # the civ kabuto bowl, as the original
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
        'helmet': None, 'hair': 'short', 'hair_asset': 'short04', 'beard': False,   # MISSING: pompadour
        'face_prop': 'sunglasses',
        'top': 'sleeved', 'neck': 'high',
        'cuirass': True, 'metal': [], 'cuirass_width': 0.14,   # the wide civ V; MISSING: deep_v
        'bottom': 'long', 'wraps': 'boots', 'trouser_flare': 0.8,   # bell-bottoms
        'patterns': {'bottom': ('stripes', 'tint', 3.0)},   # the tinted flare inserts
        'bands': [('wrist', 'tint', 0.07), ('waist', 'gold', 0.08)],   # civ cuffs, the wide gold belt
        'cape': True, 'cape_length': 0.75, 'cape_flare': 0.9,   # flaring wide
        'sandals': False, 'bracers': False, 'gear': [],
        'weapon': 'microphone', 'props': {'microphone': ('microphone', {})}, 'offhand': None,
        'stance_name': 'royal',                 # MISSING: rock_stance
        'pose_extra': [('thigh_l', (0.38, -0.2, -1)), ('calf_l', (0.25, 0.2, -1)), ('thigh_r', (-0.38, -0.2, -1)), ('calf_r', (-0.25, 0.2, -1))],   # wide-legged, knees bent
        'tint': ['cuirass', 'cape'],
        'grime': 0.3, 'upper_bulk': 1.1,
    },
}

# MISSING FEATURES
# What the notes and the originals need that the supported keys can't
# express. Each entry above approximates it and marks the spot with
# "MISSING:". Ranked by how many units need it (the count in brackets).
# (Now keys, so no longer here: cape_flare / cape_train / cape_width, 'panel'
# and 'stripes' patterns, shoulder_patch, 'shoes' over hose, cuirass_width,
# the level royal head, the hanging sword in 'stand', belt_gear off.)
#
# pattern extras     [10] 'tint' as a pattern color (tinted stripes or panels on a plain base): Korean (robe
#                    stripes), Carthaginian (trouser stripes), Egyptian (front apron), Ottoman (skirt panels),
#                    English (the tan farthingale's tinted stripes; swapped to gold on tint now). A front-only
#                    'panel' (it runs down the back too): Princess (gold stripe), Hatti (maroon apron), SciLeader
#                    (tie), Leader Industrial (white waistcoat), Shogun (dark and gold front stripes).
# hair styles        [9] updo (English, Russian, Princess), periwig (Netherlands), powdered_wig (Leader
#                    Industrial), pompadour (The King), wild_hair (SciLeader), curly_hair (Greek), balding (Chinese).
# cape_emblem / ermine_trim / cape patterns  [5] Army Middle Ages, Leader Middle Ages (the gold field and lion
#                    on the cape's back, ermine collar; 'patterns' don't reach the cape), French (gold fleur-de-lis,
#                    tinted shoulder mantle), Netherlands (ermine dots, dark lining), Austria (tinted edge,
#                    brocade). Hem bands stand in for the fur hems.
# crown extras       [5] Army Middle Ages (dark coif under it), Incan (gold front disc, red top), Russian
#                    (silver-and-gold), Byzantines (red/tinted panels), Babylonian (tinted band).
# left_hand_hold     [4] a left-hand item slot ('offhand' is shields and bows only): Incan (feather standard),
#                    Russian (props.orb at the lap), Princess (the scepter), SciLeader (props.briefcase). Today the
#                    item goes in the right hand or is left out. (Netherlands' cane goes in the left hand through
#                    'fist_hip_r'.)
# broad_collar       [4] Aztec, Byzantines, Egyptian, Mayan: a gold pectoral collar (a wide neck band stands in).
# flag_device        [4] all four Armies: device paint on the war_standard's tinted field (bars, quarters, blobs,
#                    a diagonal cross).
# headgear           [3] wide_brim_hat (Portugal: round, flat, gold edge; cavalier with a black plume stands in), a
#                    dense feather fan with cycling colors (Aztec: 11 sparse feathers), a third feather color
#                    (Mayan: feather_headdress ignores 'plume', so the fan is tint/white, not green/red/white).
# epaulettes         [3] Army Industrial, German: gold epaulettes over a tinted or colored coat ('pauldrons'
#                    takes the top's or the cuirass's color); Portugal: bigger puffed gold shoulder rolls.
# cape shape         [3] cape_closed_back (Viking: splits open down the back), cape_drape (Roman: over the left
#                    shoulder), head_veil (Spanish: from the head, over the crown).
# plate_arms / plate_legs  [3] French, Army Middle Ages, Leader Middle Ages: metal (not cloth) arm and leg plates.
# small_object       [3] Byzantines (white), Korean (dark tablet), Mayan (gold): a small item held at the chest.
# sash_edges         [3] Army Modern, Leader Modern (gold edges on the broad sash), Arab (gold stripe on the baldric).
# undergown / lapel_trim  [2] Army Ancient, Leader Ancient: an open robe front showing a white gown, tinted lapels.
# sleeve_cuff        [2] Army Ancient, Arab: a band at the end of a wide sleeve (a wrist band renders black inside it).
# hand_to_chest      [2] American (the original's hand at the lapel), Portugal: a forearm raised to the chest.
# walking_stoop      [2] Indian, SciLeader: a slight forward stoop with a stride, a cane planted ahead.
# face_prop          [2] Indian (props.glasses 'round'), The King (props.sunglasses).
# crook extras       [2] Leader Ancient (a grip param: the rod reaches only 0.2 below the fist), Egyptian (tinted bands).
# vest / panel_edges [2] Ottoman (gold-edged vest, side slits), Shogun (kataginu with winged shoulders).
# one each           scale_armor (Carthaginian), mail_shirt and sword_point_down_back (Viking), himation
#                    (Greek), dhoti (Indian), pteruges and cuirass_panels (Roman), long_gloves (Princess),
#                    suit_jacket (SciLeader), deep_v, bell_bottoms and rock_stance (The King), two_hand_raised
#                    (Shogun), ruff (English), cape_panels (Aztec), zigzag_border (Incan), feather_standard (Incan),
#                    face_ornaments (Mayan), pleats (Egyptian), fleur_pattern (Spanish), lace_hem (Russian),
#                    doublet_slashes as dashes (Netherlands; stripes stand in), hose_slashes and doublet_trim
#                    (Portugal), quilting and companion_dog (Austria), trousers under a skirt and shoulder_clasp
#                    (Celtic), a back scabbard angle (Celtic: the hilt over the shoulder), chest_patch (Persian),
#                    apron_kilt and tinted_shield_stripes (Zulu), staff_ball (Sumeria), feathered_spear, leggings
#                    and shield_at hip side (Iroquois), tinted_tip (Indian), tricorne_in_hand (Leader Industrial),
#                    front_trim (Arab), jacket pockets (Chinese), a wavy beard (Persian), a long drooping mustache
#                    with the goatee (Mongol), held_low_forward (French: point up).
#
# Render bugs the reviewers saw (code, not data): the tartan 'patterns' not showing on Celtic's 'tunic' top;
# Incan's cape hem_bands not visible on the back; Egyptian's kilt hem bands rendering as broken dashes;
# Persian's tinted hem band not showing on the 'skirt'; notches in the cape hem looking torn (Babylonian);
# gown_flare 1.9 rendering a straight column (English); the Iroquois shield ('left_hip') sitting in front of
# the thigh; Greek's neck band reading as a yoke over the shoulders; tall items touching the frame top
# (Roman's eagle, Leader Ancient's crook and Sumeria's crown; shortened as data, and the renders sit 8-10 px
# (2x) lower than the originals).
