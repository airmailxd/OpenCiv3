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
        'colors': {'skin': '#a87a5a', 'hair': '#c8a830', 'helmet': '#5e5e62', 'belt': '#2e2016',
                   'wraps': '#6a4a30', 'wraps_band': '#241810', 'bracers': '#4e3a26', 'steel': '#6e7276', 'boots': '#3e2a1a',
                   'iron': '#3e3e40', 'wood': '#5a4028', 'leather': '#3e2a1a'},
        'helmet': None, 'headgear': 'conical_helmet',          # MISSING: conical_helmet rivets / nasal bar
        'hair': 'long', 'hair_asset': 'braid01', 'mane': 0.22, 'beard': True,   # MISSING: braids (two braids down the chest)
        'beard_length_short': 0.011, 'beard_density': 450,   # a shorter, thinner yellow beard: the face shows
        'top': 'sleeved', 'bottom': 'long', 'wraps': 'wraps', 'bracers': True,
        'pelt_cape': ('#6a4a30', '#3a2818'), 'pelt_kind': 'cloak',
        'fur_vest': ('#5e4430', '#33241a'),     # the brown fur over the torso front (only sleeves/trousers civ color)
        'gear': [],
        'weapon': 'axe',
        'props': {'axe': ('battle_axe', {'length': 1.1, 'scale': 1.15, 'head': 1.6})},   # a big head on a long haft
        # (realism pass, notes/realism/Berserk.md: the Dane axe raised in both hands, the head up
        # by his right shoulder, the haft diagonal across the front)
        # (critic round 1: both fists on the haft, the haft leaning out (not at the face in 2),
        # an iron head, shoes)
        # FINAL: round 2 restored (67). Round 3 (a 1.35 haft, a belt, the fist lower: 66.25) and the
        # extra round (the haft steep at the shoulder with 'below': 58.5) scored lower.
        'stance_name': 'ready', 'two_hands': True, 'hand_r_at': (-0.2, -0.22, -0.08), 'elbow_r': 0.4,
        'weapon_dir': (-0.55, -0.22, 0.8), 'edge_dir': (-0.7, -0.7, 0.0), 'grip2': 0.34, 'elbow_l': 0.45,
        'boots': True,
        'tint': ['top', 'bottom', 'bracers'],
        'grime': 0.85, 'upper_bulk': 1.3,
    },
    'Bowman': {
        # (realism pass, notes/realism/Bowman.md: Civ3's Babylonian bowman as a Neo-Assyrian / Neo-
        # Babylonian archer of about 650 BC: a tall pointed iron helmet with ear flaps, a long square
        # black beard, a knee-length civ tunic with short sleeves under a leather vest, a broad belt;
        # the angular composite bow in a low forward ready hold, an arrow nocked; a quiver on the back,
        # a short sword at the left hip; bare legs and laced boots)
        'physique': 0.3, 'macro': {'muscle': 0.95, 'weight': 0.8, 'height': 0.75, 'proportions': 0.65},
        'race': {'caucasian': 0.75, 'african': 0.1, 'asian': 0.15},
        'colors': {'skin': '#a87a52', 'hair': '#1e140c', 'beard': '#2e1e12', 'iron_matte': '#5e5a54',
                   'cuirass': '#6a4a2a', 'leather': '#4a3220', 'belt': '#2a1c12', 'boots': '#3a2616',
                   'wood': '#3a2616', 'string': '#d8ccaa', 'fletch': '#b8ab8a', 'bronze': '#a8823e',
                   'steel': '#b0aca4', 'bracers': '#4a3220'},
        'helmet': None, 'headgear': 'assyrian_helm', 'metal': [],
        'hair': 'short', 'beard': True, 'beard_style': 'long', 'beard_length': 0.09,
        'top': 'tunic', 'bottom': 'skirt', 'skirt_length': 0.52, 'cuirass': True,
        'hem_bands': [('bottom', '#2a1c12', 0.05)],          # the dark fringed hem
        'bands': [('waist', '#2a1c12', 0.08)],                # the broad belt
        'wraps': 'boots', 'sandals': False, 'leather_bracers': True,
        'gear': [('chest_strap', {'color': '#2e2016', 'width': 0.05})],   # the quiver's strap
        'back': 'quiver',
        'hip': ('sheathed_sword', {'length': 0.36, 'width': 0.06, 'scale': 1.2}),
        'stance_name': 'bow_ready',
        'hand_l_at': (0.26, -0.26, -0.4), 'elbow_l': 0.35,    # the bow fist low and forward, out at his left
        'arrow_dir': (0.3, -0.86, -0.4),                       # nocked, pointing at the ground ahead
        'bow_up': (0.2, 0.0, 1.0), 'bow_thick': 2.5,
        'weapon': 'arrow', 'props': {'arrow': ('arrow', {'length': 0.75})},
        'offhand': 'bow', 'offhand_mesh': ('bold_bow', {'height': 1.3, 'bend': 0.15, 'recurve': 0.12, 'draw': 0.05}),
        'tint': ['top', 'bottom'],
        'grime': 0.6, 'upper_bulk': 1.2,
    },
    'Chasquis Scout': {
        # (realism pass, notes/realism/Chasquis Scout.md: an Inca chasqui, a relay runner of the road
        # system, about 1500: the white feather fan headdress that marked him, a knee-length civ unku
        # (tunic) with a dark tocapu band at the waist, a woven chuspa bag at the hip, sandals; the
        # champi (star mace) ready in the right hand, the pututu (conch trumpet) in the left)
        'physique': 0.2, 'macro': {'muscle': 1.0, 'weight': 0.6, 'height': 0.68, 'proportions': 0.7},
        'race': {'caucasian': 0.1, 'african': 0.2, 'asian': 0.7},
        'colors': {'skin': '#b47a50', 'hair': '#120c08', 'belt': '#2a1c12', 'tocapu': '#1e1612', 'cream': '#e4dac4', 'wood': '#4e3420', 'stone': '#9a9690',
                   'steel': '#a8a49c', 'gold': '#c09a34', 'feather': '#ece6d6', 'canvas': '#8a4a2a',
                   'leather': '#4a3220', 'sandal': '#3a2818'},
        'helmet': None, 'headgear': 'fan_headdress', 'headgear_params': {'fan_tint': False, 'n': 21}, 'headgear_scale': 0.85,
        'hair': 'short', 'hair_asset': None, 'beard': False,
        'top': 'vest', 'bottom': 'skirt', 'skirt_length': 0.5,
        'bands': [('waist', 'tocapu', 0.09), ('neck', 'cream', 0.07), ('knee', 'cream', 0.04)],   # the tocapu band, a cream yoke edge and knee fringes
        'hem_bands': [('bottom', 'cream', 0.05)],
        'bracers': False, 'wraps': None, 'sandals': True,
        'gear': [('chest_strap', {'color': '#3a2416', 'width': 0.04})],
        'hip_gear': [('haversack', {'scale': 0.8}, 'r')],     # the chuspa
        'weapon': 'club', 'props': {'club': ('star_mace', {'length': 0.62, 'scale': 1.25})},
        'stance_name': 'ready', 'hand_r_at': (-0.2, -0.16, -0.16), 'elbow_r': 0.55,
        'weapon_dir': (-0.35, -0.25, 0.9), 'edge_dir': (0, -1, 0),
        'offhand': 'conch', 'offhand_mesh': ('conch', {'scale': 1.8}),
        'hand_l_at': (0.1, -0.3, -0.3), 'elbow_l': 0.5, 'offhand_dir': (0.2, -0.3, 1.0),
        'tint': ['top', 'bottom'],
        'grime': 0.6, 'upper_bulk': 1.2,
        'unit_scale': 1.07,   # (size check: the opaque area at 1x against the shipped unit's)
    },
    'Enkidu Warrior': {
        # long spear upright, butt on the ground; wild black hair and a chest-
        # length beard, a civ-color headband with streamers; light tan fur
        # over the torso and down the back to the calves, a fur kilt; civ-
        # color arm, wrist, waist and knee bands
        'macro': {'muscle': 1.0, 'weight': 0.9, 'height': 0.75, 'proportions': 0.62},
        'race': {'caucasian': 0.6, 'african': 0.2, 'asian': 0.2},
        'colors': {'skin': '#9a6e4c', 'hair': '#140e0a', 'top': '#9a8462', 'bottom': '#8a7656',
                   'wraps': '#d8ccb0', 'wraps_band': '#7a6a50', 'helmet': '#3a2a1c', 'belt': '#3a2a1c',
                   'wood': '#5a3420', 'steel': '#a8683a'},   # (round 3: a copper blade)
        'helmet': 'headband',                   # MISSING: streamers (ribbons flicking up off the headband)
        'hair': 'long', 'hair_asset': 'long01', 'mane': 0.3,
        'beard': True, 'beard_style': 'long',   # black, square to the chest (colors.hair)
        # (batch 3 round 3: a civ wool skirt showing below the fleece kilt, the civ colour's mass)
        'top': None, 'bottom': 'skirt', 'skirt_length': 0.5,
        'fur_vest': ('#b09a70', '#7a6848'),
        'fur_kilt': ('#b09a70', '#7a6848'),     # the fur kilt, waist to above the knee (no cloth skirt)
        'fur_kilt_length': 0.38,
        'pelt_cape': ('#b09a70', '#7a6848'), 'pelt_kind': 'cloak',
        'bracers': False,
        # bare shins, pale ankle wraps (colors.wraps)
        'bands': [('upperarm', 'tint', 0.1), ('wrist', 'tint', 0.08), ('waist', 'tint', 0.24), ('knee', 'tint', 0.1)],   # (batch 3 round 2: a broad civ girdle; realism pass: wider, the civ read 7-13 %; critic round 1: no pale anklets)
        'wraps': None, 'sandals': False, 'back': None,
        'gear': [],   # (a civ baldric tried in round 2: hidden under the fleece)
        'weapon': 'spear',
        'props': {'spear': ('spear', {'length': 2.7, 'grip': 1.1, 'head': 1.4, 'shaft': 0.026})},
        # (realism pass, notes/realism/Enkidu Warrior.md: the spear raised ready and tipped forward)
        'stance_name': 'ready', 'hand_r_at': (-0.13, -0.22, -0.26), 'elbow_r': 0.4,
        'weapon_dir': (-0.06, -0.3, 0.95), 'edge_dir': (-1.0, 0.0, 0.0),
        'hand_l_at': (0.2, -0.2, -0.45), 'elbow_l': 0.6,
        'tint': ['helmet', 'belt', 'bottom'],
        'grime': 0.85, 'upper_bulk': 1.35,
    },
    'Enslaved Worker': {
        # a sturdy, broad labourer at rest: long hair and beard, a sleeveless olive tunic,
        # one civ-color strap across the chest, iron shackles, a spade
        'shackles': True, 'macro': {'muscle': 0.95, 'weight': 0.65, 'height': 0.7, 'proportions': 0.72}, 'age': 0.7,
        'physique': 0.5,
        'colors': {'skin': '#a07a58', 'hair': '#3a2616', 'top': '#a8ae88', 'bottom': '#a8ae88',
                   'belt': '#2e2418', 'sandal': '#2e2016', 'wood': '#6a4a2e', 'steel': '#b4b6ba',
                   'iron': '#56565a'},
        'hair': 'long', 'hair_asset': 'long01', 'beard': True,
        'beard_density': 450, 'mane': 0.15,     # a sparser, unkempt beard and straggly hair
        'top': 'vest', 'bottom': 'skirt',       # the sleeveless knee-length tunic
        'gear': [('chest_strap', {'color': '#3a2818'})],
        'sandals': True, 'wraps': None,
        # 'shackles' puts irons on wrists and ankles; MISSING: ankle_chain (a short sagging chain between the ankles)
        'weapon': 'shovel',
        'props': {'shovel': ('shovel', {'length': 1.45})},
        'stance_name': 'hang',            # MISSING: spade_upright (blade on the ground at his right side)
        'hang_dir': (-0.1, -0.15, -1.0),         # the spade angled out, blade just above the ground by the right foot
        # (no bowed pose_extra: the original stands at rest, not slumped)
        'tint': ['strap'],
        'grime': 1.0, 'upper_bulk': 1.2,    # grime only dirties skin
        'cloth_dirt': 0.8,                  # heavy brown stains on the tunic; MISSING: frayed (a ragged hem)
        # (realism pass, notes/realism/Enslaved Worker.md: at rest with the spade upright at his
        # right side, the blade planted by his right foot, his right fist round the top of the haft at
        # chest height; the left arm hanging; civ colour on the chest strap, the belt and a headband)
        'props': {'shovel': ('spade', {'length': 1.06, 'grip': 0.04, 'blade': 1.1, 'width': 1.1})},
        # (critic round 1: the blade edge-on in 0 and 4, the civ colour too little, the irons lost
        # at 1x: the spade leans out with its face tipped up to the sky; a civ hem on the tunic and a
        # wider strap; a chain between the ankle irons)
        # (critic round 2: leaning on a cane, a black blade in 7, the irons lost at 1x: the spade upright
        # against his right leg, the elbow bent; a matte iron face; thicker irons)
        'stance_name': 'ready', 'hand_r_at': (-0.14, -0.14, -0.3), 'elbow_r': 0.45,
        'weapon_dir': (-0.07, -0.05, -1.0), 'edge_dir': (-0.6, -0.8, 0.25),
        'ankle_chain': True, 'shackle_thick': 1.7,
        # (animations: the DEFAULT flic keeps this still; the Worker's actions carry the spade)
        'still_default': True, 'anim_tool': ('spade', {'length': 1.2, 'grip': 0.18, 'blade': 1.1, 'width': 1.1, 'k': 1.15}),
        'hem_bands': [('bottom', 'tint', 0.07)],
        'hand_l_at': (0.1, -0.02, -0.56), 'elbow_l': 0.3,
        'gear': [('chest_strap', {'tint': True, 'width': 0.12})],
        'bands': [('waist', 'tint', 0.08)],
        'helmet': 'headband',
        'tint': ['strap', 'helmet'],
        'pose_extra': [('head', (0, 0.15, 1))],
    },
    'Explorer': {
        # (realism pass, notes/realism/Explorer.md: an explorer of about 1600, no armour: a tall
        # capotain with a feather (civ), a buff leather jerkin over a civ doublet's sleeves, a civ
        # sash, dark hose and tall boots; an open map in the left hand, a brass spyglass in the
        # right, a rapier at the left hip)
        'macro': {'muscle': 0.85, 'weight': 0.65, 'height': 0.76, 'proportions': 0.72}, 'age': 0.55,
        'race': {'caucasian': 0.8, 'african': 0.1, 'asian': 0.1},
        'colors': {'skin': '#c89878', 'hair': '#6a4a30', 'beard': '#6a4a30', 'hat': '#3a3028', 'cuirass': '#9a7448',
                   'bottom': '#3a3430', 'boots': '#8a6a44', 'belt': '#2a1e14', 'steel': '#a8aaae',
                   'parchment': '#d8c89c', 'brass': '#b89448'},
        'helmet': None, 'headgear': 'capotain', 'headgear_params': {'brim': 0.22, 'plume': 1.5}, 'headgear_scale': [1.0, 1.0, 1.3],
        'metal': [],
        'hair': 'short', 'beard': True, 'beard_style': 'goatee',
        'cuirass': True, 'top': 'sleeved', 'puffy': False,     # the buff jerkin over the doublet's sleeves
        'bottom': 'long', 'wraps': 'boots',
        'bands': [('waist', 'tint', 0.08)],                     # the civ sash
        'gear': [],
        'hip': ('sheathed_sword', {'rapier': True, 'length': 0.95}),
        'weapon': 'spyglass', 'props': {'spyglass': ('spyglass', {'scale': 2.0})},
        'stance_name': 'ready', 'hand_r_at': (-0.14, -0.26, -0.2), 'elbow_r': 0.5,
        'weapon_dir': (-0.35, -0.6, 0.72), 'edge_dir': (0, -1, 0),
        'offhand': 'map', 'offhand_mesh': ('map', {'w': 0.52, 'h': 0.38}),
        'hand_l_at': (0.08, -0.32, -0.22), 'elbow_l': 0.45, 'offhand_dir': (0.25, -0.4, 0.88),
        'tint': ['top', 'plume'],
        'grime': 0.5, 'upper_bulk': 1.1,
        'unit_scale': 0.96,   # (size check: the opaque area at 1x against the shipped unit's)
    },
    'Gallic Swordsman': {
        # shirtless and brawny, ginger hair spiked up, check-patterned civ-
        # color trousers, a civ-color band on the sword forearm, a long sword
        # low and a small round shield (brown face, silver boss and rim)
        'spiked_hair': True, 'macro': {'muscle': 1.0, 'weight': 0.85, 'height': 0.75, 'proportions': 0.65},
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#c8946c', 'hair': '#c87a3a', 'bottom': '#3a3a40', 'belt': '#2e2016',
                   'boots': '#3e2a1a', 'bracers': '#3a2a1c', 'steel': '#7a7e84', 'bronze': '#9a9ca2', 'gold': '#e0b448',
                   'shield_face': '#7a4a24', 'shield_rim': '#8a8c90', 'leather': '#3e2a1a',
                   'pouch': '#d8b030'},           # the small yellow belt pouch; MISSING: boot buckles/lacing
        'helmet': None,
        'hair': 'short', 'hair_asset': 'short04', 'beard': False,   # MISSING: spiked_hair (limed, straight up)
        'top': None, 'bottom': 'long', 'patterns': {'bottom': ('check', '#141414', 13.0)},   # checked trousers
        'boots': True, 'wraps': 'boots',
        'bracers': False,                       # (critic round 1: bare arms, a civ armband on the sword arm's bicep)
        'gear': [],
        'weapon': 'sword',
        'props': {'sword': ('long_sword', {'length': 1.0, 'width': 0.068, 'guard': 0.16})},
        # (realism pass, notes/realism/Gallic Swordsman.md: the long sword raised in a high guard
        # out at his right, the shield on the forearm turned to the viewer, a gold torc)
        'offhand': 'shield_round', 'offhand_mesh': ('shield_round', {'r': 0.28, 'tint': 'none', 'boss_key': 'steel', 'back': 'battens'}),
        'stance_name': 'ready', 'hand_r_at': (-0.2, -0.12, -0.04), 'elbow_r': 0.45,
        'weapon_dir': (-0.35, -0.15, 0.92), 'edge_dir': (0.0, -1.0, 0.0),
        'hand_l_at': (0.15, -0.24, -0.26), 'elbow_l': 0.2, 'shield_yaw': -15.0, 'shield_roll': 0.0, 'shield_at': 'grip', 'grip_depth': 0.05,
        'bands': [('neck', 'gold', 0.014), ('upperarm', 'tint', 0.05)],   # (round 2: a thin torc, not a yoke)
        'tint': ['bottom'],
        'grime': 0.8, 'upper_bulk': 1.3,
    },
    'Hoplite': {
        # bronze Corinthian helm with a civ-color crest, bronze cuirass with
        # civ-color trim, leather pteruges, bronze greaves; a long spear
        # upright and the big round aspis across the front, rim in civ color
        'macro': {'muscle': 1.0, 'weight': 0.85, 'height': 0.75, 'proportions': 0.7},
        'colors': {'skin': '#94704f', 'hair': '#2a1c12', 'helmet': '#9a6e34', 'crest': '#3a2a1c',
                   'cuirass': '#9a6e34', 'greaves': '#9a6e34', 'bottom': '#4a3420', 'belt': '#2e2016',
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
        'offhand': 'shield_round',             # a bronze-faced aspis
        'offhand_mesh': ('shield_round', {'r': 0.52, 'dome': 0.07, 'boss': False, 'tint': 'rim', 'emblem': 'aspis', 'face_metal': True, 'back': 'porpax'}),
        'shield_roll': 0.0,
        # (realism pass, notes/realism/Hoplite.md: the spear raised ready over the aspis, the
        # shield forward on the forearm and turned to the front-left; dull bronze, not a mirror)
        # (critic round 1: worn dark bronze with a sheen, not lemon plastic; the spear raised
        # higher over the rim and tipped forward; a bigger crest)
        'stance_name': 'ready', 'hand_r_at': (-0.16, -0.22, -0.1), 'elbow_r': 0.45,
        'weapon_dir': (-0.12, -0.45, 0.88), 'edge_dir': (-1.0, 0.0, 0.0),
        'hand_l_at': (0.12, -0.3, -0.34), 'elbow_l': 0.55, 'shield_yaw': -15.0,
        'plate_metallic': 0.85, 'plate_roughness': 0.38, 'crest_scale': 1.25,
        'tint': ['crest', 'shield_emblem', 'bracers'],
        'grime': 0.7, 'upper_bulk': 1.25,
    },
    'Immortals': {
        # Persian guard: white headcloth with a face veil, tan tunic with civ-
        # color short sleeves, a brown baldric, a civ-color waist sash, baggy
        # trousers with civ-color stripes, yellow shoes; scimitar low
        'physique': 0.85, 'macro': {'muscle': 0.95, 'weight': 0.85, 'height': 0.78, 'proportions': 0.72},
        'race': {'caucasian': 0.6, 'african': 0.1, 'asian': 0.3},
        'colors': {'skin': '#8e6a4a', 'hair': '#140e0a', 'helmet': '#cfc9b8', 'top': '#9a8058',
                   'bottom': '#5e4a34', 'belt': '#3a2a1c', 'boots': '#a08434', 'steel': '#7a7e84', 'leather': '#5a3e26',
                   'gold': '#a8842e', 'veil': '#d2c294'},   # a cream veil, not the hood's white
        'helmet': 'hood', 'hood_neck': True,   # white headcloth, stopping at the neck
        'veil': True, 'veil_drop': 0.22, 'veil_top': 0.125,   # (round 2: up over the mouth to the nose)         # a veil over the mouth and chin only; MISSING: mouth_chin_veil (veil top is fixed in code; brow cloth separate, eyes and nose open)
        'beard': False,        # (critic round 1: the veil showed as a dark beard)
        # a tan sleeved tunic; 'puffy' adds the sleeve layer over the upper
        # arms (tinted: the civ-color short sleeves) and makes the trousers baggy
        'top': 'sleeved', 'puffy': True, 'cuirass': False,
        'bands': [('wrist', 'gold'), ('waist', 'tint', 0.15)],   # (realism pass: a broad sash)   # gold bracelets, the civ-color sash
        # brown trousers (colors.bottom, untinted) with civ-color stripes
        'bottom': 'long', 'patterns': {'bottom': ('stripes', '#3a2a1c', 4.0)},   # dark stripes (the civ ones put it at 36 %); projected from the rest pose: straight
        'wraps': 'boots',
        'gear': [('chest_strap', {'color': '#3a2818'})],
        'weapon': 'sword',
        'props': {'sword': ('sabre', {'length': 0.95, 'width': 0.075, 'curve': 0.16})},
        'back': 'quiver',      # (critic round 1: the bow case and quiver they carried)   # props.scimitar has no curve param: its sabre, curved more
        # (realism pass, notes/realism/Immortals.md: upright, the sabre ready at the hip, blade
        # forward-out and up; the left fist at the belt)
        'stance_name': 'ready', 'hand_r_at': (-0.1, -0.26, -0.38), 'elbow_r': 0.3,
        'weapon_dir': (-0.35, -0.75, 0.56), 'edge_dir': (0.0, 0.0, -1.0),
        'hand_l_at': (0.14, -0.24, -0.46), 'elbow_l': 0.5,
        'tint': ['belt', 'sleeves'],
        'grime': 0.65, 'upper_bulk': 1.06,
    },
    'Impi': {
        'unit_scale': 0.975,   # (size check: the final drew 1.10x the shipped area)
        # Zulu: crouched and bow-legged, a short broad stabbing spear low, a
        # tall cowhide shield upright; civ-color feather headdress, arm and
        # wrist bands, and cow-tail tufts on the calves
        'macro': {'muscle': 1.0, 'weight': 0.7, 'height': 0.78, 'proportions': 0.7},
        'race': {'caucasian': 0.0, 'african': 1.0, 'asian': 0.0},
        'colors': {'skin': '#5e4232', 'hair': '#0e0a08',   # hue toward ~20 deg, as the original; browner, less red (the builder pulls dark skin hue toward neutral brown)
                   'helmet': '#2a1e14', 'bottom': '#7a5a3a',
                   'belt': '#2e2016', 'wraps': '#d8d0c0', 'wraps_band': '#2a2420', 'gold': '#b08a2a',
                   'hide': '#d4ccbc', 'hide_dark': '#1e1612', 'wood': '#3e2a1a', 'steel': '#7a7e84'},
        'headgear_scale': 0.78,     # (critic round 1: the headdress smaller, the face shows)
        'helmet': None, 'headgear': 'feather_headdress',
        'hair': 'short', 'hair_asset': 'afro01', 'beard': False,
        'top': None, 'bottom': 'loincloth', 'bottom_material': 'leather', 'loin_length': (0.4, 0.38), 'torn': True,
        'bracers': False,
        'bands': [('upperarm', 'tint'), ('wrist', 'tint'), ('knee', 'gold')],   # yellow bands above the tufts
        'wraps': 'wraps',                       # the cow-tail tufts, tinted
        'sandals': False,
        'gear': [('chest_strap', {'color': '#6a4a30'})],   # a lighter hide strap, so it reads
        'weapon': 'spear',
        # the iklwa: short with a broad blade
        'props': {'spear': ('spear', {'length': 1.0, 'grip': 0.32, 'head': 2.2, 'butt': False, 'flat': 0.4})},   # the iklwa: short, a broad leaf
        'offhand': 'shield_oval', 'offhand_mesh': ('cowhide_shield', {'w': 0.6, 'h': 1.32}),
        # (realism pass, notes/realism/Impi.md: the iklwa ready at the hip, blade forward and up,
        # the isihlangu forward on the forearm, upright, face to the viewer)
        'stance_name': 'ready', 'hand_r_at': (-0.1, -0.22, -0.46), 'elbow_r': 0.3,
        'weapon_dir': (-0.45, -0.85, 0.25), 'edge_dir': (0.0, 0.0, 1.0),
        'hand_l_at': (0.13, -0.28, -0.32), 'elbow_l': 0.55, 'shield_yaw': -15.0, 'shield_roll': 0.0, 'shield_at': 'grip', 'grip_depth': 0.05,
        # a wider stance: these override the stance's leg bones
        'pose_extra': [('thigh_l', (0.3, -0.1, -1)), ('thigh_r', (-0.3, -0.1, -1)),
                       ('calf_l', (0.1, 0.15, -1)), ('calf_r', (-0.1, 0.15, -1))],
        'headgear_params': {'style': 'bushy'},  # many short civ-color feathers in a round bushy crown; MISSING: tapered_feathers (thin, tapered, varied length/tilt/droop, darker civ shading inside)
        'tint': ['helmet', 'wraps'],
        'grime': 0.75, 'upper_bulk': 1.25,
    },
    'Inquisitor': {
        # solemn: a floor-length charcoal hooded robe, gold cross and sash,
        # civ-color book cover, shoulder band and sleeve cuffs; book raised
        # in the left hand, a torch low in the right
        # (realism pass, notes/realism/Inquisitor.md: a Dominican friar of the Spanish Inquisition, as
        # Berruguete paints them: the white wool habit to the floor in a few deep troughs (skirt_troughs,
        # not regular ripples), the black cappa open in front with its hood up; the torch raised beside
        # his head, the civ book at the chest; civ cuffs and a civ band at the neck)
        'physique': 0.85, 'macro': {'muscle': 0.6, 'weight': 0.55, 'height': 0.8, 'proportions': 0.75}, 'age': 0.75,
        'colors': {'skin': '#9a7656', 'top': '#aca490', 'bottom': '#aca490', 'helmet': '#1e1e22', 'cape': '#1e1e22',
                   'belt': '#2e2016', 'gold': '#9a7e2c', 'sandal': '#2a2018', 'wood': '#4a3222',
                   'book_cover': '#3a2418', 'parchment': '#d0c49e', 'cloth_wrap': '#3a2e26'},
        'helmet': 'hood', 'hair_asset': None, 'beard': False,
        'top': 'sleeved', 'neck': 'high',     # (critic round 2: no wide sleeves, so the civ cuffs show)
        'bottom': 'kilt', 'robe_drop': 0.68,          # the floor-length habit
        'skirt_folds': 0.015, 'skirt_troughs': (6, 0.15, 4),
        # (critic round 1: no civ colour from the front, a flat white habit and a flat black cappa,
        # a tiny flame: the cappa lined in the civ colour (it shows at its open front), a wider civ
        # belt, an off-white wool habit with deeper troughs, the cappa draped by simulation, a bigger
        # cross and flame)
        'cape_emblem': ('cross', 'tint', 0.4, 0.28), 'cape_drape': True,
        'chest_emblem': ('cross', 'tint', 0.2),     # (build: the Holy Office's cross on the breast too, not a civ lining)
        'cape': True, 'cape_length': 1.28, 'cape_width': 0.6, 'cape_flare': 0.45, 'cape_wrap': 0.07,
        'bands': [('neck', 'tint', 0.07), ('waist', 'tint', 0.09), ('wrist', 'tint', 0.08)],
        'hem_bands': [('sleeves', 'tint', 0.08)],     # civ cuffs on the wide sleeves
        'bracers': False, 'sandals': True, 'wraps': None, 'gear': [],
        'weapon': 'club',
        'props': {'club': ('torch', {'length': 0.55, 'flame': 1.6})},
        'offhand': 'book', 'offhand_mesh': ('book', {'tint': True, 'scale': 1.25}),   # a civ-color cover
        'offhand_dir': (0.3, -0.4, 0.85),
        'stance_name': 'upright',               # the torch upright beside the head
        # (critic round 2: the torch low at the chest in 1-3, the flame over the face: raised above the head)
        'pose_extra': [('head', (0, 0.4, 1)), ('upperarm_r', (-0.75, -0.12, -0.3)), ('lowerarm_r', (-0.3, -0.2, 1))],   # (solo: the torch out from the face)
        'tint': [],
        'grime': 0.55, 'upper_bulk': 1.05,
    },
    'Jaguar Warrior': {
        'unit_scale': 0.96,   # (size check: the spotted suit drew 1.11x the shipped area)
        # Aztec: a jaguar-skin head with a civ-color feather spray, bare chest
        # with a gold pectoral and a civ-color collar band, civ-color belt,
        # loincloth and wrist cuffs, spotted leg wraps, gold arm, knee and
        # ankle bands, a macuahuitl low and a round shield with a civ-color
        # face and a gold boss
        'macro': {'muscle': 1.0, 'weight': 0.8, 'height': 0.72, 'proportions': 0.68},
        'race': {'caucasian': 0.0, 'african': 0.2, 'asian': 0.8},
        'colors': {'skin': '#a06a40', 'hair': '#0e0a08', 'pelt': '#c08a48',   # (round 3: the cat head the suit's tawny, not plain orange)
                   'pelt_dark': '#2a1810',
                   'wraps': '#c87436', 'wraps_band': '#2a1810', 'belt': '#3a2a1c', 'bottom': '#3a2a1c',
                   'gold': '#a8842e', 'feather': '#d8cfb8', 'obsidian': '#16141a',
                   'shield_face': '#5a4030', 'wood': '#3e2a1a', 'top': '#c08a48'},
        # the jaguar's head over his own, its face over the brow, a civ-color feather spray on top
        'helmet': None, 'headgear': 'jaguar_head', 'hair_asset': None,
        'plume': True,
        # (batch 3 critic round 1: the jaguar suit over the torso and arms, a spotted pelt
        # in a lighter tawny than the skin)
        'top': 'sleeved', 'chest_emblem': ('disc', 'gold', 0.2),   # the gold pectoral
        'patterns': {'top': ('camo', '#2a1810', 'cover', 0.3, 30.0)},
        'bands': [('neck', 'tint'), ('wrist', 'tint'), ('upperarm', 'gold'), ('knee', 'gold'), ('ankle', 'gold')],
        'bottom': 'loincloth', 'loin_length': (0.36, 0.32), 'torn': False,
        'bracers': False, 'wraps': 'wraps', 'sandals': False, 'gear': [],
        'weapon': 'club',
        'props': {'club': ('macuahuitl', {'length': 0.8})},
        # a civ-color face with dark rosettes and a gold boss
        'offhand': 'shield_round', 'offhand_mesh': ('jaguar_shield', {'r': 0.3}),
        # (realism pass, notes/realism/Jaguar Warrior.md: the macuahuitl raised at the ready by
        # his right shoulder, the chimalli forward on the forearm)
        'stance_name': 'ready', 'hand_r_at': (-0.15, -0.2, -0.14), 'elbow_r': 0.3,
        'weapon_dir': (-0.3, -0.2, 0.93), 'edge_dir': (-0.7, -0.7, 0.0),
        'hand_l_at': (0.12, -0.3, -0.36), 'elbow_l': 0.55, 'shield_yaw': -15.0, 'shield_roll': 0.0, 'shield_at': 'grip', 'grip_depth': 0.05,
        'tint': ['belt', 'bottom', 'plume', 'shield_emblem'],
        'grime': 0.75, 'upper_bulk': 1.25,
    },
    'Javelin Thrower': {
        # (realism pass, notes/realism/Javelin Thrower.md: Civ3's Mayan javelin thrower as a Late
        # Classic Maya warrior: a crown of upright feathers (civ and natural) on a civ band, a
        # sleeveless jacket of quilted cotton, a civ loincloth, sandals; a javelin cocked back
        # over the right shoulder, ready to throw; a round shield with a feather fringe, its face civ)
        'macro': {'muscle': 1.0, 'weight': 0.7, 'height': 0.72, 'proportions': 0.68},
        'race': {'caucasian': 0.05, 'african': 0.2, 'asian': 0.75},
        'colors': {'skin': '#9a6440', 'hair': '#0e0a08', 'cuirass': '#ddd2b8', 'belt': '#3a2818', 'wood': '#4e3420',
                   'steel': '#2a2628', 'gold': '#c09a34', 'feather': '#3e7a52', 'shield_emblem': '#d8c8a0',
                   'shield_back': '#6a5034', 'sandal': '#3a2818'},
        'helmet': None, 'headgear': 'feather_headdress', 'veil': False,
        'hair': 'short', 'beard': False,
        'top': None, 'cuirass': True, 'metal': [], 'cuirass_offset': 0.03,   # the quilted cotton jacket
        'patterns': {'cuirass': ('check', '#c4b898', 9.0)},
        'bottom': 'loincloth', 'loin_length': (0.42, 0.36), 'torn': False,
        'bands': [('wrist', 'gold'), ('knee', '#ddd2b8', 0.04)],
        'bracers': False, 'wraps': None, 'sandals': True, 'gear': [],
        'weapon': 'spear', 'props': {'spear': ('javelin', {'length': 1.75, 'grip': 0.75})},
        'stance_name': 'ready', 'hand_r_at': (-0.24, 0.1, 0.18), 'elbow_r': 0.85,   # cocked back over the shoulder
        'weapon_dir': (0.25, -0.92, 0.3), 'edge_dir': (0, 0, 1),
        'offhand': 'shield', 'offhand_mesh': ('chimalli', {'r': 0.27, 'face_tint': True, 'emblem': True, 'rim': True}),
        'hand_l_at': (0.14, -0.32, -0.28), 'elbow_l': 0.55,
        'shield_at': 'grip', 'grip_depth': 0.04, 'shield_yaw': -15.0, 'shield_roll': 0.0,
        'tint': ['bottom', 'hat'],
        'grime': 0.6, 'upper_bulk': 1.15,
    },
    'Legionary': {
        # a steel galea with a civ-color crest, segmented plate, a civ-color
        # cape over the shoulders and back, dark red tunic skirt with a civ-
        # color hem, greaves and sandals; gladius low, a tall curved scutum
        'macro': {'muscle': 1.0, 'weight': 0.85, 'height': 0.72, 'proportions': 0.68},
        'race': {'caucasian': 0.8, 'african': 0.1, 'asian': 0.1},
        # (critic round 1: warmer steel and skin, as the stock Hoplite and Swordsman)
        'colors': {'skin': '#b27e52', 'hair': '#241810', 'helmet': '#6e685e', 'crest': '#3a2a1c',
                   'cuirass': '#7e786c', 'top': '#5e1a22', 'bottom': '#5e1a22', 'belt': '#24160e',
                   'greaves': '#4a4a4e', 'sandal': '#22160c', 'steel': '#b0aca4', 'bronze': '#a07a3a',
                   'shield_face': '#4a3e36', 'shield_emblem': '#d8d4c8', 'shield_pale': '#a8802c',
                   'leather': '#6a4a2e', 'wood': '#6a4a2e'},
        'helmet': 'galea', 'metal': ['helmet', 'cuirass'],
        'cuirass': True, 'top': 'tunic', 'cuirass_bands': 5, 'neck': 'high',   # lorica segmentata, up to the neck
        'pauldrons': 'metal',                   # shoulder plates in the cuirass color
        'pauldron_zmin': 1.4, 'pauldron_offset': 0.006, 'pauldron_x': 0.16,
        'plate_metallic': 0.6, 'plate_roughness': 0.55,      # dull iron, as the stock units' (a mirror reflected black)
        'hem_bands': [('bottom', '#24160e', 0.04)],   # smaller, closer to the arm
        # (realism pass: the civ colour on the scutum's field (a dark-gold wings-and-
        # thunderbolt device on it), the crest and the short cloak over the shoulders
        # (the original's); the tunic natural madder wool: notes/realism/Legionary.md)
        'cuirass_width': 0.26,                  # the hoops wrap the flanks to the belt
        'patterns': {'bottom': ('panel', '#24160e', 0.045)},   # the cingulum's apron of straps down the front
        'bottom': 'skirt',
        'wraps': None, 'sandals': True, 'beard': False,     # caligae, no greaves (the ranks wore none)
        'sandal_height': 0.06, 'lace_height': 0.3,           # the caligae's openwork upper and lacing up the shin
        'gear': [],
        'weapon': 'sword',
        'props': {'sword': ('sword', {'length': 0.52, 'width': 0.065, 'guard': 0.12})},
        # the original: a civ-color face with a pale (white) thunderbolt emblem
        'offhand': 'shield_oval', 'offhand_mesh': ('scutum', {'w': 0.62, 'h': 1.05, 'curve': 0.26, 'bold': 1.4, 'face_tint': True, 'rim': 0.02}),
        # braced: the fist on the grip behind the boss, the gladius ready at the hip
        'stance_name': 'shield_ready', 'shield_at': 'grip', 'shield_yaw': -12.0, 'grip_depth': 0.0, 'shield_tilt': -6.0,
        'sword_dir': (-0.72, -0.68, 0.02),      # out at his right side and forward: never across the shield
        'tint': ['crest', 'top', 'bottom'],      # the sleeve edges under the shoulder plates and the tunic's skirt
        'grime': 0.7, 'upper_bulk': 1.3,
    },
    'Libyan Mercenary': {
        # bronze Attic helmet with a crest, grey scale cuirass over a white
        # tunic with civ-color sleeves and hem, bronze greaves over leg wraps;
        # long spear upright, a big white round shield with a gold rim and a
        # civ-color sunburst
        'macro': {'muscle': 0.95, 'weight': 0.75, 'height': 0.76, 'proportions': 0.72},
        'race': {'caucasian': 0.4, 'african': 0.5, 'asian': 0.1},
        'colors': {'skin': '#8e6646', 'hair': '#140e0a', 'helmet': '#b8a068', 'crest': '#3a2a1c',
                   'cuirass': '#55565c', 'bottom': '#b8b2a2', 'belt': '#2a1c12', 'greaves': '#7e6838',
                   'wraps': '#4e3622', 'shield_face': '#b8b2a2', 'shield_rim': '#8a7444', 'bronze': '#8a6a36',
                   'wood': '#4a3622', 'steel': '#7e7a72'},
        # the open-faced galea with an untinted crest stands for the Attic
        # helmet; MISSING: attic_cheek_guards
        'helmet': None, 'headgear': 'attic_helm', 'headgear_params': {'crest_seated': True}, 'metal': [],   # bronze, crested, with cheek guards; (round 3: a linen corselet in small scales, not a plain metal slab)
        
        'beard': True,
        # the cuirass covers the tunic's body, so tinting 'top' shows on the sleeves
        'cuirass': True, 'top': 'tunic', 'neck': 'high',   # MISSING: cuirass_scale (a scale pattern on the cuirass)
        'bottom': 'skirt', 'hem_bands': [('bottom', 'tint', 0.08)],   # (realism pass: wider, so the colour shows from behind)
        'wraps': 'greaves', 'gear': [],
        'weapon': 'spear',
        'props': {'spear': ('spear', {'length': 2.5, 'grip': 1.1, 'head': 1.3, 'shaft': 0.025})},
        'offhand': 'shield_oval',               # MISSING: sunburst_emblem (a star in place of the cross bands)
        'offhand_mesh': ('shield_oval', {'w': 0.8, 'h': 0.8, 'band': 0.1, 'dome': 0.06, 'emblem': 'star', 'back': 'battens'}),   # the sunburst
        # (realism pass, notes/realism/Libyan Mercenary.md: the spear raised ready, the shield forward)
        'stance_name': 'ready', 'hand_r_at': (-0.13, -0.22, -0.26), 'elbow_r': 0.4,
        'weapon_dir': (-0.06, -0.3, 0.95), 'edge_dir': (-1.0, 0.0, 0.0),
        'hand_l_at': (0.13, -0.3, -0.36), 'elbow_l': 0.55, 'shield_yaw': -15.0, 'shield_roll': 0.0, 'shield_at': 'grip', 'grip_depth': 0.05,   # (critic round 1: +, the board in front of the knuckles)
        'tint': ['top', 'shield_emblem', 'crest'],
        'patterns': {'bottom': ('pleats', '#8a8476', 22.0), 'cuirass': ('check', '#3a3b40', 60.0)},   # (critic round 1: the linen skirt in folds, apart from the cuirass)   # (a civ crest so the colour shows from behind)
        'grime': 0.7, 'upper_bulk': 1.2,
    },
    'Longbowman': {
        # (realism pass, notes/realism/Longbowman.md: an English archer of about 1415: a kettle hat,
        # a quilted linen jack under a civ livery jacket, a short jack skirt, dark hose and ankle
        # boots, an arrow bag at the right hip and a short sword at the left; the longbow as tall as
        # he is, upright in a low forward ready hold, an arrow nocked)
        'physique': 0.3, 'macro': {'muscle': 1.0, 'weight': 0.8, 'height': 0.8, 'proportions': 0.68},
        'race': {'caucasian': 0.9, 'african': 0.05, 'asian': 0.05},
        'colors': {'skin': '#c09070', 'hair': '#6a4a2c', 'beard': '#6a4a2c', 'iron_matte': '#6a6862',
                   'top': '#cfc4a8', 'coat': '#cfc4a8', 'bottom': '#4a4038', 'boots': '#2a1e16', 'belt': '#2a1c12',
                   'leather': '#5a3e24', 'wood': '#4a3018', 'string': '#d8ccaa', 'fletch': '#d8d0bc',
                   'steel': '#a8a6a0', 'canvas': '#b8a47a'},
        'helmet': None, 'headgear': 'kettle_hat', 'headgear_params': {'brim': 0.2, 'slope': 0.03}, 'metal': [],
        'hair': 'short', 'beard': True,
        'top': 'sleeved', 'cuirass': True,                    # the jack's sleeves under the livery jacket
        'patterns': {'top': ('check', '#a89a7c', 7.0)},        # the jack's quilting
        'hem_bands': [('cuirass', '#2a1c12', 0.03), ('coat', '#2a1c12', 0.035)],   # the livery's dark bound edges
        'coat_length': 0.3, 'coat_flare': 1.15, 'bottom': 'long', 'wraps': 'boots',
        'gear': [],
        'back': None, 'hip_gear': [('arrow_bag', {'scale': 1.3}, 'r')],
        'hip': ('sheathed_sword', {'length': 0.42, 'width': 0.06, 'scale': 1.2}),
        'stance_name': 'bow_ready', 'draw_elbow': 0.3,
        'hand_l_at': (0.04, -0.4, -0.32), 'elbow_l': 0.35,    # (round 2: the fist within the right hand's reach)
        'arrow_dir': (0.5, -0.76, -0.4),
        'bow_up': (0.45, 0.0, 1.0), 'bow_thick': 2.4, 'bow_depth': 1.2,
        'weapon': 'arrow', 'props': {'arrow': ('arrow', {'length': 0.85})},
        'offhand': 'bow', 'offhand_mesh': ('bold_bow', {'height': 1.95, 'bend': 0.12, 'recurve': 0.0, 'draw': 0.05}),
        'tint': ['cuirass', 'coat'],                          # the livery jacket and its skirt
        'grime': 0.6, 'upper_bulk': 1.2,
        'unit_scale': 1.04,   # (size check: the opaque area at 1x against the shipped unit's)
    },
    'Medieval Infantry': {
        # a closed great helm, full mail, a civ-color surcoat from the
        # shoulders to the ankles with a gold hem and a brown belt, a small
        # round brown wooden shield (per the original), a flail at the knee
        'macro': {'muscle': 1.0, 'weight': 0.9, 'height': 0.78, 'proportions': 0.68},
        'colors': {'iron': '#6a6e74', 'skin': '#94704f', 'helmet': '#5e6064', 'mail': '#7a7c82', 'belt': '#3a2818',
                   'bracers': '#5a5c60', 'greaves': '#55575c', 'steel': '#8a8c90',
                   'wood': '#4a3222', 'shield_face': '#7a5434', 'shield_rim': '#3a3a3e', 'bronze': '#6e7074',
                   'boots': '#55575c'},   # steel sabatons, not white
        'helmet': 'great_helm', 'metal': ['helmet'], 'hair_asset': None, 'beard': False,
        # the surcoat: a tinted sleeveless top and a tinted kilt dropped to the ankles
        'top': 'vest', 'neck': 'high', 'collar': 'mail',   # MISSING: tabard_front_width, helm_rivets
        # the civ-color surcoat to mid-thigh with a gold hem, ring mail on the arms and legs
        'bottom': None, 'coat_length': 0.72, 'coat_flare': 1.15, 'hem_bands': [('coat', 'gold', 0.06)],
        'mail_parts': ['arms', 'legs'], 'leather_bracers': True, 'wraps': None, 'boots': True,
        'gear': [],
        'weapon': 'club',
        'props': {'club': ('flail', {'length': 0.7, 'chain': 0.38})},   # big and bright: it read faint at 1x
        # (realism pass, notes/realism/Medieval Infantry.md: the flail raised ready at the right
        # shoulder, its ball hanging clear; the round shield on the forearm with a civ cross)
        'offhand': 'shield_round', 'offhand_mesh': ('shield_round', {'r': 0.3, 'tint': 'none', 'emblem': 'cross', 'boss_key': 'steel', 'back': 'battens'}),
        # (critic round 1: the flail further out at his right, clear of the helm; the shield
        # out of the fist; dark iron mail; a belt across the surcoat)
        'stance_name': 'ready', 'hand_r_at': (-0.26, -0.14, -0.1), 'elbow_r': 0.5,
        'weapon_dir': (-0.6, 0.05, 0.8), 'edge_dir': (-0.5, -0.85, 0.0),
        'shield_at': 'grip', 'grip_depth': 0.05, 'mail_rows': True,   # the board in front of the knuckles (a positive depth moves it away from the body); mail in courses
        'bands': [('waist', 'belt', 0.05)],
        'hand_l_at': (0.12, -0.3, -0.36), 'elbow_l': 0.55, 'shield_yaw': -15.0, 'shield_roll': 0.0,
        'plate_metallic': 0.6, 'plate_roughness': 0.5,
        'tint': ['top', 'bottom', 'shield_emblem'],
        'grime': 0.7, 'upper_bulk': 1.12,
    },
    'Ninja': {
        # all in near-black olive: hood and mask, a long jacket with split
        # panels, baggy trousers, tabi; a civ-color harness of straps and
        # belt; a kama low in each hand
        # (realism pass, notes/realism/Ninja.md: a Sengoku shinobi in deep indigo-charcoal with value
        # steps (hood and mask darkest, the jacket a step lighter, the trousers between, grey leg wraps,
        # dark tabi); a bright straight ninjato held ready in the right fist, a kama in the left; the
        # empty scabbard behind; civ harness and belt)
        'macro': {'muscle': 1.0, 'weight': 0.7, 'height': 0.72, 'proportions': 0.72},
        'race': {'caucasian': 0.0, 'african': 0.0, 'asian': 1.0},
        'colors': {'skin': '#987452', 'helmet': '#101116', 'top': '#232838', 'bottom': '#1a1d27',
                   'boots': '#121316', 'belt': '#2a2a20', 'steel': '#c4c8ce', 'wood': '#5a4430', 'iron': '#3a3a3e',
                   'leather': '#2a2418', 'veil': '#1c1e24', 'wraps': '#464b58', 'wraps_band': '#23262e',
                   'shoes': '#3c404a', 'lacquer': '#141416', 'cloth_wrap': '#2a2c30', 'leather_dark': '#141210',
                   'gold': '#8a7a4a'},
        'helmet': 'hood', 'hair_asset': None, 'veil': True,   # hood and dark face mask
        'top': 'sleeved', 'neck': 'high',       # MISSING: split side panels on the jacket
        'coat_length': 0.4, 'coat_flare': 1.35,  # the jacket skirt to mid-thigh, flaring
        'bottom': 'long', 'wraps': 'shoes',     # tabi (critic round 1: not bare feet)
        'bands': [('waist', 'tint', 0.06)],     # the civ-color belt
        'harness': True,                        # two civ-color straps crossing on chest and back
        'gear': [('chest_strap', {'tint': True, 'width': 0.08, 'cross': True, 'offset': 0.04})],   # the straps in a V
        'hip_gear': [('katana_saya', {'length': 0.58, 'thick': 1.5}, 'back')],   # the empty scabbard behind
        'weapon': 'sword',
        'props': {'sword': ('katana_drawn', {'length': 0.66, 'width': 0.055, 'curve': 0.008, 'grip': 0.2}),
                  'kama': ('kama', {'length': 0.42})},
        'offhand_mesh': ('kama', {'length': 0.46, 'scale': 1.75}),   # (final = round 2; round 3's forearm kama, {'length': 0.34, 'blade': 1.6}, scored lower)
        'offhand': 'kama', 'offhand_dir': (0.55, -0.8, 0.25),
        # (critic round 1: a flat mid-grey suit, the arms splayed wide, the kama lost: an
        # indigo-black suit, the fists in close and forward, a bigger kama, slimmer shoulders)
        'stance_name': 'ready', 'hand_r_at': (-0.06, -0.4, -0.22), 'elbow_r': 0.3,
        'weapon_dir': (-0.45, -0.45, 0.77), 'edge_dir': (0.4, -0.9, 0.0),
        'hand_l_at': (0.08, -0.36, -0.36), 'elbow_l': 0.3,
        'tint': ['belt', 'harness'],
        'grime': 0.6, 'upper_bulk': 1.12,
    },
    'Pikeman': {
        # steel morion, breastplate and tassets, civ-color puffy sleeves and
        # dark civ-color breeches, cream stockings, brown shoes, light beard;
        # partisan upright, butt by the right foot
        'macro': {'muscle': 1.0, 'weight': 0.8, 'height': 0.76, 'proportions': 0.72},
        'race': {'caucasian': 0.9, 'african': 0.05, 'asian': 0.05},
        'colors': {'skin': '#94704f', 'hair': '#6a5034', 'bottom': '#2e3040', 'helmet': '#6e7074', 'cuirass': '#74767a',
                   'belt': '#2e2016', 'wraps': '#8a8c90', 'wraps_band': '#5e6064', 'sandal': '#3a2818',
                   'wood': '#4a3222', 'steel': '#8a8c90'},
        'helmet': 'morion', 'metal': ['helmet', 'cuirass'],
        'beard': True,
        'cuirass': True, 'cuirass_width': 0.22, 'top': 'sleeved', 'puffy': True, 'neck': 'high',
        'chest_emblem': ('cross', 'tint', 0.32),            # the tinted cross on the breastplate
        'coat_length': 0.36, 'coat_metal': True,             # steel tassets over the hips
        'tint_shade': {'bottom': 0.45},                     # dark civ-color breeches
        # tinted puffed breeches to below the knee, cream stocking wraps over the calves
        'bottom': 'breeches', 'wraps': 'wraps', 'sandals': True,
        'gear': [],
        'weapon': 'spear',
        'props': {'spear': ('partisan', {'length': 2.5, 'grip': 1.0, 'head': 2.2, 'shaft': 0.026})},   # about 1.3x his height
        # (realism pass, notes/realism/Pikeman.md: the partisan ported ready in both fists, tipped
        # forward, the head well above the morion; dull steel)
        # (critic round 1: one fist on the shaft, out at his right and tipped forward, the
        # broad partisan head clear; the left fist on the hip; a bigger civ cross)
        # (round 2: further out at his right, so the head clears the body in direction 2;
        # the left arm relaxed at the side; slimmer sleeves)
        'stance_name': 'ready', 'hand_r_at': (-0.28, -0.16, -0.28), 'elbow_r': 0.55,
        'weapon_dir': (-0.3, -0.04, 0.95), 'edge_dir': (0.0, -1.0, 0.0),
        'hand_l_at': (0.1, -0.06, -0.56), 'elbow_l': 0.3,
        'fortify_tool': ((-0.4, -0.9, 0.18), (1, 0, 0)),   # (FORTIFY: the lowered partisan angled out to his right and up, off the line to the camera in 1)
        'plate_metallic': 0.6, 'plate_roughness': 0.5,
        'mail_parts': ['legs'],                 # the original's grey armoured legs
        'tint': ['top'],                        # the breeches dark wool, not civ: the front read 43 %
        'grime': 0.65, 'upper_bulk': 1.12,
    },
    'Samurai': {
        # the realism overhaul (notes/realism/Samurai.md): a Sengoku samurai in
        # tosei-gusoku (yoroi.py), the katana drawn and held two-handed in chudan
        # (the critics, 2026-11-23: the weapon ready, as every stock unit; this
        # replaces the hand on the hilt of 2026-10-16), its empty scabbard at the
        # left hip. The civ colour on the odoshi lacing (sode, kusazuri, shikoro)
        # and the sashimono; the rest warm-black lacquer, a red menpo, gilt, kaki cloth.
        'macro': {'muscle': 0.9, 'weight': 0.7, 'height': 0.7, 'proportions': 0.7}, 'age': 0.5,
        'race': {'caucasian': 0.0, 'african': 0.0, 'asian': 1.0},
        'colors': {'skin': '#c8986c', 'hair': '#0e0c0c', 'top': '#2c2a30', 'belt': '#1a1824',
                   'sandal': '#a89060', 'shoes': '#c8c4ba', 'do': '#3e2820', 'lacquer': '#34211a', 'iron': '#4a4a4e',
                   'gold': '#e0b448', 'cloth_wrap': '#e2dac6', 'leather_dark': '#2a1e18', 'steel': '#dfe3e8',
                   'hakama': '#8c7a5c', 'kote': '#4a4a52',
                   'kyahan': '#3e3832', 'menpo': '#c8482c', 'rivets': '#a85a38', 'mon': '#ece8de',
                   'haidate_lace': '#3a2a20'},
        'helmet': None, 'hair': 'bald', 'hair_asset': None, 'topknot': None, 'beard': False,
        'top': 'sleeved', 'wide_sleeves': False, 'puffy': False, 'neck': 'high',
        'chest_emblem': None, 'patterns': {}, 'coat_length': None,
        'bottom': None, 'belt_gear': False,
        'sandals': True, 'wraps': 'shoes', 'gear': [],
        'yoroi': {
            'hakama': {'balloon': 0.1, 'puff_top': 0.86, 'pleats': 0.02}, 'suneate': {}, 'haidate': {}, 'kote': {},
            'do': {'sugake': False, 'rivets': 0.22, 'gloss': 0.55},
            'kusazuri': {'edge': 0.28, 'flare': 1.5, 'length': 0.55},
            'sode': {'width': 0.27, 'height': 0.3, 'out': 0.03}, 'menpo': {'glow': 0.35},
            'kabuto': {'horn_height': 0.25, 'horn_width': 1.25},
            'sashimono': {'width': 0.22, 'height': 0.6, 'pole': 1.47, 'yaw': 35.0, 'pole_r': 0.016},
            'wakizashi': {},
        },
        'weapon': 'sword', 'offhand': None,
        'props': {'sword': ('katana_drawn', {'length': 0.8, 'width': 0.042})},
        # high chudan (seigan): the blade up and out to his right, so it clears the body in every direction
        'stance_name': 'hasso', 'blade_dir': (-0.64, 0.2, 0.74), 'edge_dir': (0.3, -1, 0), 'grip2': 0.17,
        'hip': ('katana_saya', {'length': 0.7, 'thick': 1.8}),   # the empty scabbard, close down the left leg
        'hip_at': (0.19, -0.03, 0.04),
        'hip_dir': (0.12, 0.38, -0.92),
        'hand_on_hilt': False,
        'posture': False,     # its final (round 4) was posed before the posture system: keep it so (re-measured on merge)
        'tint': ['lace', 'banner'],
        'grime': 0.45, 'upper_bulk': 1.05,
    },
    'Scout': {
        # (realism pass, notes/realism/Scout.md: an ancient scout as a Greek traveller-scout of
        # about 400 BC: a wide-brimmed felt petasos (civ band), a short civ tunic, a brown wool
        # chlamys cloak on his back, laced boots; a light spear upright in the right hand, a water
        # gourd hanging from the left, a satchel at the hip)
        'macro': {'muscle': 0.9, 'weight': 0.55, 'height': 0.74, 'proportions': 0.7},
        'race': {'caucasian': 0.7, 'african': 0.15, 'asian': 0.15},
        'colors': {'skin': '#a87a52', 'hair': '#2a1c12', 'beard': '#2a1c12', 'hat': '#7a6040', 'cape': '#6a5a42',
                   'belt': '#2e2016', 'boots': '#4a3220', 'leather': '#5a3e24', 'wood': '#6a4a2e',
                   'steel': '#a09c94', 'gourd': '#a8844e', 'canvas': '#6a4a2c'},
        'helmet': None, 'headgear': 'petasos', 'hair': 'short', 'hair_asset': None, 'topknot': None, 'beard': True,
        'top': 'tunic', 'bottom': 'skirt', 'skirt_length': 0.4,
        'hem_bands': [('bottom', '#2a1c12', 0.035)],
        'cape': True, 'cape_length': 0.55, 'cape_width': 0.55, 'cape_flare': 0.2, 'cape_wrap': 0.08,
        'bands': [], 'wraps': 'boots', 'boots': False, 'leather_bracers': False,
        'gear': [('chest_strap', {'color': '#3a2818', 'width': 0.04})],
        'chest_gear': None,
        'hip_gear': [('haversack', {'scale': 0.9}, 'r')],
        'weapon': 'spear', 'props': {'spear': ('spear', {'length': 2.0, 'grip': 1.05, 'head': 1.0, 'shaft': 0.024})},
        'stance_name': 'upright', 'upright_dir': (-0.12, -0.08, 1.0),
        'offhand': 'gourd', 'offhand_mesh': ('gourd', {'scale': 1.2}), 'offhand_dir': (0.0, 0.0, 1.0),
        'back': None,
        'tint': ['top', 'bottom', 'hat'],
        'grime': 0.7, 'upper_bulk': 1.1,
        'unit_scale': 0.88,   # (size check: the opaque area at 1x against the shipped unit's)
    },
    'Swiss Mercenary': {
        # steel morion with a civ-color plume, a civ-color doublet (the
        # original shows no breastplate) with a dark strap from shoulder to
        # hip, puffy slashed pale sleeves, baggy red breeches slashed black,
        # black shoes, brown beard; a long pike upright with a civ-color shaft
        'macro': {'muscle': 1.0, 'weight': 0.85, 'height': 0.78, 'proportions': 0.7},
        'race': {'caucasian': 1.0, 'african': 0.0, 'asian': 0.0},
        'colors': {'skin': '#9a7254', 'hair': '#4a3424', 'helmet': '#7e8086', 'cuirass': '#84868c',
                   'top': '#a8a090', 'sleeves': '#bcb4a4',   # (round 3: toned down) 'bottom': '#7a2a20', 'boots': '#161210', 'belt': '#1e1612',
                   'wood': '#4a3222', 'steel': '#8a8c90'},
        'helmet': 'morion', 'plume': True, 'metal': ['helmet'], 'plume_scale': 1.6,   # a bigger plume (if the builder reads it)
        'beard': True,
        # the original's torso is a civ-color doublet (no breastplate shows), puffed pale sleeves
        'cuirass': False, 'top': 'sleeved', 'puffy': True, 'wide_sleeves': False, 'neck': 'high',   # (critic round 1: slimmer sleeves)   # white sleeves to the wrist, slashing_stripes
        'bottom': 'breeches',
        'patterns': {'bottom': ('stripes', '#141414', 8.0), 'sleeves': ('stripes', '#6e2a22', 8.0)},   # black slashes on the breeches; (batch 3 build round 3: the sleeves' slashes show a madder lining, not zebra stripes)
        'wraps': 'boots',
        'gear': [('chest_strap', {'tint': True, 'width': 0.26})],   # the bold civ-color sash over the pale doublet (wider: the shaft's colour is gone)
        'weapon': 'spear',
        # the pike: its shaft in civ color, steel head, langets and butt plain
        'props': {'spear': ('pike', {'length': 3.6, 'grip': 1.2})},   # (critic round 1: a wooden shaft, longer)
        # (realism pass, notes/realism/Swiss Mercenary.md: the pike ported ready in both fists)
        # (the Pikeman's lesson: one fist on the shaft out at his right, the pike leaning out
        # so the head clears the body; the left arm relaxed, no crossed arms)
        # (critic round 1: the arm bent, the fist closer at the chest, not held straight out)
        'stance_name': 'ready', 'hand_r_at': (-0.16, -0.24, -0.22), 'elbow_r': 0.75,   # (round 3: the fist up at the chest)
        'weapon_dir': (-0.25, -0.06, 0.97), 'edge_dir': (-1.0, 0.0, 0.0),
        'hand_l_at': (0.1, -0.06, -0.56), 'elbow_l': 0.3,
        'tint': ['plume', 'strap', 'bottom'],     # the doublet stays pale, as the original; the sash is civ; (batch 3 round 2: the slashed breeches civ, the shaft's colour gone: 6 % without)
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
