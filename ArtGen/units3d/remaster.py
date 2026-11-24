"""The remaster's creative changes to the old catalog entries (shared by the
Blender side and the venv side; plain data).

The catalog says what each Civ3 original shows; these entries push a unit
toward the owner's direction ("less AI generated, more badass ... use the
original Civ3 models as a baseline, but be creative"): heavier builds, a
pelt, a different weapon size, a stance. Keys are merged over the catalog
entry (dicts merged one level deep).
"""

REMASTER = {
    'Warrior': {
        'macro': {'muscle': 1.0, 'weight': 0.9, 'height': 0.7, 'proportions': 0.6},
        'colors': {'skin': '#a07450', 'hair': '#4a3020', 'wraps': '#4a3020', 'wraps_band': '#22160c',
                   'wood': '#6a4a2e', 'stone': '#3e443a', 'leather': '#4a3020', 'belt': '#3a2618'},
        'upper_bulk': 1.2,
        'hair_asset': 'long01',
        'mane': 0.26,
        'pelt_cape': ('#8a7050', '#5a4430'),
        'grime': 0.8,
        # (realism pass, notes/realism/Warrior.md: a Copper Age warrior with a polished stone
        # battle-axe raised at the ready; civ colour on the loincloth, wrist wraps and a headband)
        # (critic round 1: the axe out clear of the head in the front views; a darker, smaller
        # polished-stone head; civ bands at the wrists, not gauntlets)
        # (round 2: higher, the head above the head line in every direction; a bigger head)
        'props': {'stone_axe': ('stone_axe', {'length': 0.62, 'scale': 1.3})},
        'stance_name': 'ready', 'hand_r_at': (-0.22, -0.12, 0.04), 'elbow_r': 0.5,
        'weapon_dir': (-0.42, 0.1, 0.9), 'edge_dir': (-0.6, -0.8, 0.0),
        'bracers': False, 'bands': [('wrist', 'tint', 0.07)],
        'hand_l_at': (0.2, -0.28, -0.4), 'elbow_l': 0.6,
        'helmet': 'headband', 'tint': ['bottom', 'helmet'],
    },
    'Spearman': {
        'macro': {'muscle': 1.0, 'weight': 0.8, 'height': 0.75, 'proportions': 0.7},
        'colors': {'skin': '#94704f', 'helmet': '#9a7a40', 'crest': '#3a2a1c', 'bottom': '#4e3a26',
                   'shield_face': '#b09060', 'shield_rim': '#3a3634', 'belt': '#3a2818'},
        'props': {'spear': ('spear', {'grip': 1.2, 'length': 2.6, 'head': 1.35, 'shaft': 0.026}),
                  'shield_oval': ('shield_oval', {'band': 0.13, 'w': 0.8, 'h': 0.8})},
        'offhand_mesh': ('shield_oval', {'band': 0.13, 'w': 0.8, 'h': 0.8, 'back': 'battens'}),
        'gear': [('chest_strap', {'tint': True, 'width': 0.06})],   # the original's civ sash, as a baldric
        'grime': 0.7,
        'beard': True,
        # (realism pass, notes/realism/Spearman.md: a Late Bronze Age spearman, bare torso and a
        # civ kilt, the spear raised ready and tipped forward, the round shield on the forearm)
        'top': None, 'bottom': 'skirt', 'skirt_length': 0.42,
        'hem_bands': [('bottom', '#2a1c12', 0.035)],
        # (critic round 1: a bronze helmet; the spear's fist higher, tipped further forward;
        # the shield higher on the forearm)
        'stance_name': 'ready', 'hand_r_at': (-0.15, -0.24, -0.16), 'elbow_r': 0.4,
        'weapon_dir': (-0.1, -0.4, 0.91), 'edge_dir': (-1.0, 0.0, 0.0),
        'hand_l_at': (0.13, -0.3, -0.3), 'elbow_l': 0.55, 'shield_yaw': -15.0,
        'shield_at': 'grip', 'grip_depth': -0.05,    # (round 2: the fist on the central grip, behind the board)
        'plate_metallic': 0.75, 'plate_roughness': 0.4,
        'tint': ['bottom', 'shield_emblem', 'crest'],
        'metal': ['helmet'],
        'upper_bulk': 1.25,
    },
    'Archer': {
        # a Cretan archer (notes/realism/Archer.md): the exomis, a wool tunic pinned
        # on the left shoulder, the right shoulder and arm bare, belted, to mid-thigh
        # (civ color); a dark felt cap; a short composite recurve bow at the ready,
        # an arrow nocked; a leather quiver on the back; laced boots
        # (critic round 1: built like the stock Bowman, a short brown beard, the
        # face lit under the cap, the bow out to the side and thicker)
        'physique': 0.0, 'macro': {'muscle': 0.9, 'weight': 0.8, 'height': 0.75, 'proportions': 0.62},
        'race': {'caucasian': 0.7, 'african': 0.15, 'asian': 0.15},
        'colors': {'skin': '#a87a52', 'hair': '#1e140c', 'beard': '#3a2616', 'helmet': '#5a4028', 'fletch': '#a89a7a', 'boots': '#3a2616',
                   'leather': '#4a3220', 'wood': '#5a3e24', 'string': '#d8ccaa', 'belt': '#2e2016',
                   'bronze': '#c8a058', 'steel': '#c8c4bc', 'bracers': '#4a3220'},
        'top': 'exomis', 'bottom': 'skirt', 'skirt_length': 0.42,   # to mid-thigh
        'hem_bands': [('bottom', '#24160e', 0.04)],               # a dark woven border at the hem
        'tint': ['top', 'bottom'],
        'hair': 'short', 'helmet': 'cap', 'metal': [], 'cap_brim': True,
        'beard': True, 'beard_length_short': 0.01, 'beard_density': 450,
        'wraps': 'boots', 'upper_bulk': 1.15, 'leather_bracers': True,
        'gear': [('chest_strap', {'color': '#3a2616', 'width': 0.05})],   # the quiver's strap over the right shoulder
        'back': 'quiver',
        'hip': ('sheathed_sword', {'length': 0.5, 'width': 0.06, 'scale': 1.35}),   # a short sword at the left hip, as Cretan archers carried
        'stance_name': 'bow_ready',
        'arrow_dir': (0.3, -0.95, 0.02),        # drawn to the jaw, aimed forward and to his left
        'bow_up': (0.4, 0.0, 1.0), 'bow_thick': 2.1, 'bow_depth': 1.25, 'draw_elbow': 0.55,   # the upper limb canted out; the draw elbow out and up
        'weapon': 'arrow', 'props': {'arrow': ('arrow', {'length': 0.8})},   # nocked on the string
        'offhand_mesh': ('bold_bow', {'height': 1.15, 'bend': 0.18, 'recurve': 0.16, 'draw': 0.55}),   # half drawn: a D
        'grime': 0.5,
    },
    'Swordsman': {
        # the original: an iron helmet with a brow ridge (reads horned), brown
        # leather armour over a skirt of leather strips, grey greaves; civ
        # color on the upper arms and the small round shield's rim and boss
        'macro': {'muscle': 1.0, 'weight': 0.8, 'height': 0.75, 'proportions': 0.7},
        'colors': {'skin': '#8a684a', 'helmet': '#5c5c60', 'cuirass': '#5e4028', 'bottom': '#4e3622', 'top': '#6a2a22',
                   'steel': '#8a8c90', 'bracers': '#4e3622',
                   'greaves': '#6a6a6e', 'horn': '#b8a888', 'belt': '#2e2016', 'gold': '#6a6c70', 'shield_face': '#5e4430', 'steel': '#6e7074'},
        # (critic round 1: a Montefortino bowl with its knob and a civ plume, not horns; no
        # shoulder balls, the civ in short tunic sleeves; the shield further out of the fist)
        'helmet': 'cap', 'cap_knob': True, 'plume': True, 'plume_scale': 1.3, 'metal': ['helmet'], 'cuirass': True,
        'pauldrons': False, 'shield_at': 'grip', 'grip_depth': 0.05,   # (the board in front of the knuckles: a positive depth moves it away from the body)
        'bands': [('waist', 'tint', 0.1)],      # (round 2: a civ sash; the sleeves madder wool)
        'neck': 'high', 'boots': True,
        # (realism pass, notes/realism/Swordsman.md: an Iberian/Italic swordsman of c. 300 BC,
        # the sword ready at the hip, the caetra on the forearm; civ sleeves, shield device and rim)
        'stance_name': 'shield_ready', 'sword_dir': (-0.42, -0.88, 0.18), 'edge_dir': (0, 0, 1),
        'shield_yaw': -15.0, 'shield_roll': 0.0, 'top': 'tunic',
        'offhand_mesh': ('shield_round', {'r': 0.3, 'tint': 'none', 'face_tint': True, 'boss_key': 'steel', 'back': 'battens'}),
        'plate_metallic': 0.6, 'plate_roughness': 0.55,
        'hem_bands': [('cuirass', 'tint', 0.045), ('bottom', 'tint', 0.07)],   # civ cuirass edge and skirt hem: colour from behind      # a civ edge binding on the cuirass (round 3)
        'props': {'sword': ('sword', {'guard': 0.18, 'length': 0.62, 'width': 0.08, 'hilt': 'iron'})},
        'bottom': 'loincloth', 'bottom_material': 'leather', 'loin_length': (0.5, 0.46), 'leather_bracers': True,
        'back': None, 'wraps': 'greaves', 'beard': True, 'colors_extra': None,
        'tint': ['top', 'shield_emblem', 'plume'],   # (round 3: civ short sleeves again, slim now the pauldrons are gone)
        'grime': 0.7, 'upper_bulk': 1.15,
    },
    # ---- riders (the PC session's units: mounted.py seats them on their horses)
    # chariot crews (chariots.py): keys '<unit>:crew<i>'
    # (the realism pass) a thrusting spear, not the crescent glaive (it broke into specks); the left fist forward on the reins
    'chariot:crew0': {'weapon': 'spear', 'props': {'spear': ('spear', {'length': 2.6, 'grip': 1.0, 'shaft': 0.04, 'head': 1.7})}, 'tint': ['top', 'bottom'],
                      'upright_dir': (-0.08, -0.62, 0.78),   # (batch 3) the spear angled forward, ready to thrust
                      # (batch 2) a bronze crested helmet and a leather corselet over the civ tunic
                      'helmet': None, '_headgear': 'ch_crested', 'hair_asset': None, 'headgear_lift': 0.03,
                      'cuirass': True, 'metal': [], 'cuirass_bands': 5, 'colors': {'cuirass': '#7a5634'},
                      'pose_extra': [('upperarm_l', (0.15, -0.6, -0.78)), ('lowerarm_l', (-0.05, -1.0, 0.05))]},
    # the Egyptian archer: a gold usekh and armbands; his bow case rides on the car
    'War Chariot:crew0': {'bands': [('neck', 'gold', 0.08), ('upperarm', 'gold', 0.04), ('wrist', 'gold', 0.04)], 'back': None,
                          # (the realism pass) the composite bow held up in the raised left fist, its arc reading at 1x
                          'offhand_dir': (0.05, -0.1, 1.0), 'offhand_mesh': ('bow', {'composite': True, 'thick': 2.0, 'height': 1.3, 'draw': 0.35}), 'upper_bulk': 1.3, '_headgear': 'ch_khepresh',
                          'pose_extra': [('upperarm_l', (0.55, -0.8, 0.05)), ('lowerarm_l', (0.45, -0.88, 0.15)),
                                         ('upperarm_r', (-0.35, -0.55, -0.1)), ('lowerarm_r', (0.75, -0.2, 0.25))]},   # (batch 3) the bow raised forward, half drawn to the chest
    # the Assyrian crew: grey scale shirts and conical helmets; a gold spear and a gold round shield
    'Three Man Chariot:crew0': {'tint': ['top', 'bottom'], 'pose_extra': [('upperarm_l', (0.15, -0.6, -0.78)), ('lowerarm_l', (-0.1, -1.0, 0.05)), ('upperarm_r', (-0.15, -0.6, -0.78)), ('lowerarm_r', (0.1, -1.0, 0.05))],   # (the realism pass) the driver's fists forward on the reins
                                'cuirass': True, 'metal': ['cuirass'], 'cuirass_bands': 7, 'colors': {'cuirass': '#8a6a40', 'helmet': '#9a9ca2'},
                                'helmet': None, '_headgear': 'ch_hittite', 'hair_asset': None, 'headgear_lift': 0.03},
    'Three Man Chariot:crew1': {'tint': ['top', 'bottom'], 'props': {'spear': ('spear', {'length': 2.4, 'shaft': 0.04, 'head': 1.7})}, 'upright_dir': (-0.05, -0.55, 0.83), 'cuirass': True, 'metal': ['cuirass'], 'cuirass_bands': 7, 'colors': {'cuirass': '#8a6a40', 'helmet': '#9a9ca2', 'wood': '#6e4e30'},
                                'helmet': None, '_headgear': 'ch_hittite', 'hair_asset': None, 'headgear_lift': 0.03},
    'Three Man Chariot:crew2': {'tint': ['top', 'bottom'], 'weapon': None, 'cuirass': True, 'metal': ['cuirass'], 'cuirass_bands': 7,
                                'colors': {'cuirass': '#8a6a40', 'helmet': '#9a9ca2', 'shield_face': '#c8a048', 'shield_rim': '#d8b458'},
                                'helmet': None, '_headgear': 'ch_hittite', 'hair_asset': None, 'headgear_lift': 0.03,
                                'offhand_mesh': ('ch_eight', {})},
    # Civ3's Rider: the civ-colored coat with gold belt fittings and gold hems
    # (the realism pass) a Ming-era cavalryman: the conical fur-brimmed hat with a red tassel, lifted off the eyes; a
    # long civ coat (its skirts: mounted.COAT_SKIRT) with gold hems and a gold belt; the dao raised up and out
    'Rider': {'colors': {'belt': '#2a1e14', 'pants': '#6a4a30', 'bottom': '#6a4a30', 'boots': '#2a1e16', 'skin': '#d8ac8a',
                         'steel': '#dcdfe4', 'hat': '#2a2220', 'feather_red': '#8a1a1a'},   # (C4) a brighter blade
              'tint': ['top'],   # (C2) the hat's crown dark, not civ; the civ coat broken by a dark belt, gold collar and cuffs
              'hem_bands': [('top', 'gold', 0.06)], 'bands': [('waist', 'belt', 0.1), ('wrist', 'belt', 0.06), ('neck', 'belt', 0.06)],
              'gear': [('chest_strap', {'color': '#2a1e14', 'width': 0.06})],   # (C5) the coat's dark crossover lapel   # (C4) a broad dark belt, dark collar and cuffs
              'headgear_scale': [0.95, 0.95, 0.9], 'hair_asset': None, 'headgear_lift': 0.11,
              'headgear_params': {'tassel': 1.0, 'height': 1.45, 'brim': '#d2c4a2', 'brim_wide': True},   # (C4) a wide pale fur brim, a big tassel   # (C3) a taller cone, a slimmer dark fur brim
              'macro': {'weight': 0.9, 'muscle': 1.0},   # (C5) a heavier torso and arms
              'props': {'sabre': ('sabre', {'length': 0.8, 'width': 0.095, 'curve': 0.14, 'guard': 'brass', 'spine': 0.03,
                                            'tip': 0.022, 'guard_r': 0.016})}},
    # Civ3's Ansar: a gilded lance, a big round shield with a gold rim and boss, a sword at the hip
    'Ansar Warrior': {'props': {'cav_spear': ('cav_spear', {'length': 2.5, 'grip': 0.8, 'shaft': 0.026, 'head': 1.5, 'flat': 0.45, 'tip': 0.02})},   # (batch 2) a slimmer dark shaft, a shorter leaf, the butt short
                      'colors': {'wood': '#8a7048', 'shield_rim': '#d0a840', 'bronze': '#d0a840', 'shield_face': '#b89448',
                                 'boots': '#3a2a1e', 'pants': '#e0dcd4', 'bottom': '#e0dcd4', 'skin': '#b88a66', 'wood': '#4a3220', 'steel': '#9ea2aa'},
                      'headgear_params': {'wrapped': True, 'white': True}, 'face_glow': 0.35,
                      'tint': ['hat', 'belt'], 'bands': [('waist', 'tint', 0.21)],   # (round 4) a broad civ sash, front and back   # a broad civ sash; (the realism pass) white trousers, dark boots
                      'headgear_scale': 0.95, 'headgear_lift': 0.17, 'hair_asset': None,   # (the realism pass) the turban off the eyes, the bearded face lit
                      'macro': {'muscle': 0.9, 'weight': 0.8},
                      'offhand_mesh': ('shield_round', {'r': 0.32, 'tint': 'none'}),
                      'hip': ('sheathed_sword', {'length': 0.78})},
    # Civ3's Ancient Cavalry: a big grey round shield quartered by a civ cross, a tall civ crest, a heavier javelin
    # (the realism pass) a Roman auxiliary horseman: a crested cavalry helmet with cheek guards, a mail shirt over a
    # civ tunic, a flat oval shield with a civ cross and rim, the javelin drawn back to throw (out of the fit)
    'Ancient Cavalry': {'top': 'sleeved', 'tint': ['top', 'crest', 'cape'], 'helmet': None, '_headgear': 'attic_helm',
                        'headgear_lift': 0.1, 'hair_asset': None, 'headgear_scale': [0.84, 0.76, 0.76],   # (critic2) a real head's size, the cheek guards wide of the face
                        'cuirass': True, 'metal': [],
                        'cape': True, 'cape_length': 0.52, 'cape_width': 0.26, 'cape_flare': 0.9, 'cape_folds': 2.5, 'cape_depth': 0.07, 'cape_hem_curve': 0.35,   # the sagum (civ), pinned off one shoulder; (C4) tapering from the clasp, deep folds, a curved hem
                        'colors': {'shield_face': '#c8b48c', 'shield_rim': '#5a4628', 'cuirass': '#6e7076', 'belt': '#2e2016', 'shoes': '#2e2016', 'bronze': '#6e5a34',   # a darker olive bronze
                                   'skin': '#c49a72', 'sandal': '#3a2818', 'bottom': '#5a4634', 'boots': '#2e2016'},
                        'bottom': 'long', 'wraps': 'boots', 'shoes': True, 'sandals': False,   # (C5) long bracae: no bare shin over the boot
                        'patterns': {'cuirass': ('rows', '#3e4046', 16.0, 0.55)}, 'bands': [('waist', 'belt', 0.06)],   # (C5) mail rows, a belt   # (C4) bracae to below the knee and boots, not bare legs
                        'offhand': 'shield_oval',
                        'offhand_mesh': ('shield_oval', {'w': 0.56, 'h': 0.8, 'rim_tint': False, 'emblem': 'cross', 'band': 0.28, 'boss': 1.6}),   # (C5) the civ device bolder than a dark rim
                        'props': {'javelin': ('javelin', {'scale': 1.2, 'length': 1.55, 'shaft': 0.024, 'head': 1.55, 'tip': 0.016,
                                                          'flat': 0.85})}},   # (C5) a sturdier head: one piece edge-on in E
    # (pass 5) a civ surcoat over mail with a belt and a sword at the hip (no plate cuirass: it read as a silver
    # cylinder); the skirts: mounted.COAT_SKIRT; a smaller true heater; the lance upright at rest, longer (out of the fit).
    # Pass 3: 'top': 'sleeved', 'cuirass': True, metal cuirass, heater 0.56 x 0.86, lance 2.3 x 1.4 couched.
    'Knight': {'top': 'tunic', 'cuirass': False, 'metal': ['helmet'],
               'colors': {'cuirass': '#9a9ca2', 'mail': '#7a7c80', 'shield_pale': '#f2f0ea', 'shield_rim': '#c4a462', 'belt': '#4a3220'},
               'bands': [('waist', 'belt', 0.05)], 'hip': ('sheathed_sword', {'length': 0.9, 'width': 0.07}),
               'offhand': 'shield_heater', 'offhand_mesh': ('shield_heater', {'w': 0.46, 'h': 0.66}),
               'weapon': 'lance', 'props': {'lance': ('lance', {'length': 3.2, 'grip': 1.2, 'girth': 1.3})},
               'headgear_params': {'top': 0.15, 'bottom': -0.21}, 'mail_parts': ['arms'],   # no horns: a flat-topped helm
               'hair_asset': None},   # no hair through the helm's top
    # Civ3's Cossack: a tan coat; only the papakha and belt are civ-colored
    # Civ3's Cossack: a low tan papakha with a civ top, gazyri across the chest, crossed civ straps, a shashka
    'Cossack': {'headgear_scale': [0.8, 0.8, 0.62], 'headgear_params': {'crown': True}, 'headgear_lift': 0.12,
                # (the realism pass) a dark cherkeska (the long skirt: mounted.COAT_SKIRT) with bright gazyri rows,
                # a tall black papakha with a civ crown, the pika in hand, the rifle on the back, a shashka at the hip
                'colors': {'fur': '#b4946a', 'skin': '#e2b494', 'top': '#6e5c4a', 'coat': '#6e5c4a', 'boots': '#1a1614', 'wood': '#7a5434'},
                'patterns': {'top': ('plastron', 'tint')}, 'hair': 'bald',   # a civ beshmet in the coat's opening (the gazyri: mounted.GAZYRI)
                'gear': [('chest_strap', {'tint': True, 'width': 0.09, 'cross': True, 'offset': 0.03})],
                'hip': ('sheathed_sword', {'length': 1.0, 'width': 0.08, 'key': 'shashka', 'shashka': True}),
                'weapon': 'cav_spear', 'props': {'cav_spear': ('cav_spear', {'length': 2.1, 'grip': 0.8, 'shaft': 0.028, 'head': 1.6, 'flat': 0.9, 'spike': True, 'tip': 0.012})},   # a pika: a slim spike, a blunt last tip
                # the bashlyk: riders.bashlyk (mounted.BASHLYK), the hood with its tails; civ cuffs
                'tint': ['hat', 'belt'], 'bands': [('wrist', 'tint', 0.07)],
                'beard': False},   # (the stubble read as a dark mask on the riders)
    # Civ3's Hussar: gold frogging and buttons, a corded shako with a tall plume, a sabre scabbard
    'Hussar': {'headgear_params': {'cords': 'high', 'peak': 'hussar_peak', 'peak_tilt': -8.0, 'peak_w': 0.06, 'straight': True, 'plume_k': 2.2, 'band': 'gold', 'cord_down': False, 'swag_z': 0.165},
               'patterns': {'top': ('frogging', '#f0c040', 5.0, 0.19, 0.42)},   # thicker cords, carried round the sides
               # (the realism pass) a tall black felt shako, straight-sided, lifted off the eyes, gold cords across its
               # upper front and a tall civ plume (critic1: the civ shako and the high civ collar masked the face)
               'headgear_scale': [0.95, 0.95, 0.97], 'headgear_lift': 0.115, 'headgear_tilt': -7.0, 'neck': 'low', 'hair': 'short',   # dark short hair at the back of the head (ponytail01's side locks darkened the face)
               'colors': {'plume': '#d8b048', 'skin': '#ecc4a2', 'hair': '#1a120c', 'pants': '#c8bea4', 'bottom': '#c8bea4', 'steel': '#f6f8fa',
                          'hussar_peak': '#3a2a1c', 'hat': '#1c1a1c'},
               'weapon': 'sabre', 'props': {'sabre': ('sabre', {'length': 0.85, 'width': 0.085, 'curve': 0.09, 'guard': 'brass', 'spine': 0.014, 'guard_r': 0.016})},
               'hip': ('sheathed_sword', {'length': 0.95, 'width': 0.08, 'empty': True, 'key': 'steel'}),
               'bands': [('wrist', 'gold', 0.04), ('waist', 'gold', 0.07)],   # gold cuffs, the barrel sash
               # the pelisse: a short civ cape with a pale fur edge, swung onto the left shoulder (mounted.PELISSE)
               'cape': True, 'cape_length': 0.42, 'cape_width': 0.44, 'cape_flare': 0.3, 'cape_wrap': 0.05,
               'tint': ['top', 'cape', 'plume'], 'tint_shade': {'cape': 0.62},   # the pelisse a darker shade of the civ colour
               'hem_bands': [('cape', '#3a2c22', 0.13)]},   # a dark brown fur edge (pale read as a belt)
    # Civ3's Keshik: a spiked bronze helm with a fur brim, maroon lamellar with civ rows and gold edges,
    # a bronze round shield on the back (the bow stays in hand)
    # Civ3's Cavalry: a white crossbelt, gold epaulettes, a sabre in a brass scabbard at the hip, a brass helmet
    # (pass 8: pass 6's head, sabre and face on the family proportions, plus the re-check: no pale plastron (it read as a
    # grey beard), the coat front civ with two thin white crossbelts. Pass 4's entry: chest_strap width 0.055, the buff
    # plastron, sabre 0.9 x 0.065, steel #dfe3e8, headgear_lift 0.025, no peak='brass', skin from the catalog: commit 43c8df6e)
    'Cavalry': {'gear': [('chest_strap', {'color': '#f2efe8', 'width': 0.045, 'cross': True})], 'pauldrons': 'metal',   # the carbine and sabre belts crossing
                'weapon': 'sabre', 'props': {'sabre': ('sabre', {'length': 0.76, 'width': 0.065, 'curve': 0.06, 'guard': 'brass', 'spine': 0.024})},   # (critic9: a real sabre's length; a thicker back: unbroken edge-on)
                'headgear_params': {'crest': 1.5, 'mane': True, 'peak': 'brass'},
                'colors': {'cuirass': '#c8a038', 'gold': '#c8a038', 'helmet': '#b08a40', 'brass': '#c8a038', 'steel': '#b8bcc2',   # steel-grey, not white (critic9)
                           'skin': '#e0b08c',   # a warm, light face under the peak
                           'leather_dark': '#0c0a08'},
                'metal': ['helmet'], 'hip': ('sheathed_sword', {'length': 1.05, 'width': 0.085, 'empty': True, 'key': 'steel'}),
                'pauldron_zmin': 1.4, 'pauldron_x': 0.17, 'pauldron_offset': 0.006,   # flat boards, not balls
                'headgear_scale': [0.9, 0.95, 0.95], 'hair_asset': None, 'headgear_lift': 0.075,   # smaller, off the eyes
                'bands': [('wrist', 'gold', 0.05), ('neck', 'gold', 0.035)]},   # gold cuffs and collar
    # (the realism pass) a keshig horse archer: the backward shot (mounted.BOW_DRAW), the spiked fur-brimmed helmet
    # lifted off the eyes, a civ lamellar coat in broad rows with dark lacing (garments 'rows'), a pale robe
    'Keshik': {'tint': ['top'], 'headgear_params': {'spike': True}, 'headgear_lift': 0.13, 'hair_asset': None,   # a civ deel under dark lamellar
               'cuirass': True, 'metal': [], 'cuirass_trim': 'gold',
               'colors': {'cuirass': '#7a7c80', 'bronze': '#8a6a40', 'skin': '#d2a47e', 'wood': '#5a3a20', 'fletch': '#d6ccb4'},
               'quiver_at': (-0.22, 0.06, -0.32), 'quiver_up': (-0.15, 0.45, 1),   # (C3) the quiver at the right hip, pale fletching
               'patterns': {'cuirass': ('rows', '#4a2218', 7.0, 0.9)},   # (C2) iron lamellar, dark red leather lacing rows; muted fletching
               'bands': [('waist', 'tint', 0.05)],   # a thin civ sash
               'offhand': 'bow', 'offhand_mesh': ('bow', {'height': 1.02, 'draw': 0.3, 'thick': 2.3, 'recurve': 0.12, 'string_r': 0.012, 'string_key': 'sinew', 'composite': True}), 'weapon': None, 'back_gear': None},
    # Civ3's Sipahi: a dark kalkan with a gold boss, a gilded helmet band, a mail aventail (spiked_helm has one)
    # (the realism pass) a timariot sipahi: a chichak with a peak, a nasal and an aventail open at the face, a civ
    # plume; a krug (mail and plates) over a kaftan whose civ skirts show below (mounted.COAT_SKIRT); the kalkan
    # on the left arm, the kilij raised
    'Sipahi': {'offhand': 'shield_round', 'offhand_mesh': ('shield_round', {'r': 0.3, 'tint': 'none', 'boss_key': 'gold'}),
               'tint': ['plume'], 'hair_asset': None, 'headgear_lift': 0.14, 'headgear_scale': 0.95, 'metal': [],
               'headgear_params': {'open_face': True, 'nasal': True, 'peak': True, 'plume_tip': 0.024, 'spike_tip': 0.01, 'plume_r': 1.8},
               'colors': {'shield_rim': '#c4a462', 'bronze': '#c4a462', 'shield_face': '#3a3c42', 'helmet': '#b8bcc2', 'mail': '#8a8c90',
                          'top': '#6a2a1c', 'cuirass': '#b4b8c0', 'belt': '#2a1e14', 'boots': '#5a2a1e', 'pants': '#3a3430',
                          'skin': '#c89a78', 'steel': '#c4c8d0', 'leather': '#2a1e14'},   # (round 4) a lighter blade   # a dark steel kalkan; (batch 2) a shaded blade, a dark hilt
               'patterns': {'cuirass': ('rows', '#3a3c42', 9.0, 0.75)}, 'bands': [('waist', 'belt', 0.08)], 'macro': {'weight': 1.0, 'muscle': 1.0},
               'face_glow': 0.25,
               'props': {'sabre': ('sabre', {'length': 0.8, 'width': 0.085, 'curve': 0.2, 'guard': 'brass', 'spine': 0.042, 'edge': True,
                                             'tip': 0.03, 'guard_r': 0.016})}},
    # Civ3's Conquistador: a black morion and breastplate, a civ rodela with a gold boss and rim
    # (the realism pass) the morion smaller and lifted (the bearded face lit under the brim), the civ doublet sleeves
    # and rodela the two civ regions, a dark breastplate
    'Conquistador': {'tint': ['top'], 'metal': ['cuirass', 'helmet'], 'headgear_scale': 1.0, 'headgear_lift': 0.16, 'face_glow': 0.35,
                     'colors': {'cuirass': '#a8acb4', 'helmet': '#8a8e96', 'shield_rim': '#c4a462', 'bronze': '#c4a462',   # (batch 3) polished steel
                                'skin': '#d8ac8a', 'boots': '#3a2a1e', 'pants': '#5a4636'},   # a polished steel morion
                     'offhand': 'shield_round', 'offhand_mesh': ('shield_round', {'r': 0.3, 'face_tint': True, 'boss_key': 'gold'}),
                     'props': {'lance': ('lance', {'length': 2.6, 'head': 1.5, 'pennon': True})}},
    # (the realism pass) Civ3's Mounted Warrior as a Plains horse warrior: the bonnet smaller and lifted (the face
    # lit), bare chest, fringed buckskin leggings, a quiver on the back; the bow drawn (mounted.BOW_DRAW)
    'Mounted Warrior': {'headgear_scale': 0.85, 'headgear_lift': 0.05, 'headgear_params': {'trailer': 'short', 'n': 13, 'tip_cord': False, 'dark_tips': True, 'spread': 1.0, 'width': 0.085, 'cone': 0.45, 'clean': True},   # (C6) 13 broad feathers with gaps, standing up
                        'offhand_mesh': ('bow', {'height': 1.0, 'draw': 0.28, 'thick': 2.5, 'recurve': 0.11, 'round_': True, 'string_r': 0.014, 'string_key': 'sinew'}),   # (C5) 10 % shorter; (C6) thinner round limbs, a clear recurve
                        'back_params': {'arrows': 9, 'scale': 1.15},
                        'colors': {'skin': '#a87a5a', 'pants': '#8a6440', 'bottom': '#8a6440', 'boots': '#5a4028', 'wood': '#5a3a20'},   # (C5) darker buckskin: the leg apart from the dun barrel
                        'macro': {'muscle': 1.0, 'weight': 0.75},
                        'patterns': {'bottom': ('side_stripe', 'tint', 0.55)}},   # beaded civ strips down the leggings
    # (the realism pass) a classical Greek horseman: a bronze Boeotian helmet, a civ chiton and a civ chlamys
    # flying from the shoulders, bare arms and legs, short boots; the xyston raised overhand (out of the fit)
    'Horseman': {
        'top': 'tunic', 'bottom': None, 'wraps': 'boots', 'helmet': None, '_headgear': 'boeotian', 'headgear_lift': 0.2, 'headgear_params': {'brim': 0.62, 'key': 'olive_bronze', 'rim': 'gold', 'droop': 0.02},
        'mail_parts': [], 'sleeves': None, 'collar': None, 'neck': 'low',
        'props': {'cav_spear': ('cav_spear', {'length': 2.8, 'grip': 1.1, 'head': 1.5, 'shaft': 0.034, 'flat': 0.7, 'tip': 0.012})},
        'colors': {'skin': '#b48c70', 'boots': '#5a3c24', 'belt': '#3a2818', 'steel': '#7a7c80', 'wood': '#8a6a44',
                   'bronze': '#8c6a3c', 'cape': '#5e4432'},   # aged bronze, weathered ash, a natural wool chlamys
        'tint': ['top'], 'cape': True, 'cape_length': 0.3, 'cape_width': 0.38, 'cape_flare': 0.45,
        'hem_bands': [('cape', 'tint', 0.12), ('top', '#3a2818', 0.03)],   # a civ border on the chlamys, a dark tunic hem
        'headgear_scale': [0.9, 0.9, 0.9],
        'bands': [('waist', 'belt', 0.04)],
        'macro': {'muscle': 0.75, 'weight': 0.6, 'proportions': 0.75},
        'grime': 0.5,
    },
    'Settler': {
        # the original: a traveller with a big bundle and a pot on his back, a
        # tall staff, civ color on the tunic and the cap, a cream kilt
        'macro': {'muscle': 0.85, 'weight': 0.65, 'height': 0.72, 'proportions': 0.7}, 'age': 0.6,
        'colors': {'skin': '#94704f', 'bottom': '#cfc6b0', 'hair': '#3a2a1c', 'pack': '#6a4e30', 'sandal': '#3a2818',
                   'pot': '#8a5a34', 'bedroll': '#9a8a66'},
        'tint': ['top', 'helmet'], 'beard': True, 'helmet': 'hood', 'pack_scale': 1.6, 'top': 'vest',
        'pose_extra': [('spine_01', (0, -0.12, 1)), ('spine_02', (0, -0.3, 1)), ('head', (0, 0.25, 1)),
                       ('upperarm_l', (0.16, -0.12, -1)), ('lowerarm_l', (0.08, -0.4, -1))],
        'props': {'staff': ('staff', {'length': 2.35, 'grip': 1.25})},
        'grime': 0.7, 'upper_bulk': 1.1,
        # (realism pass, notes/realism/Settler.md: the load big enough to show beside and over the
        # shoulders from the front too; civ tunic and hood)
        # (critic round 1: the sack a plain blob: smaller, bound with rope bands, a sewn patch; a
        # dark edge on the hood)
        # (critic round 2: the sack still a blob, the hood a blue lump: vertical rope lashings and a
        # patch on the sack; a cream rim on the hood)
        'pack_scale': 1.6, 'pack_up': 0.14,   # (round 3: the lashings read as a shield's rims: off)
        'hem_bands': [('helmet', '#cfc6b0', 0.04)],
    },
    'Worker': {
        # the original: slim and wiry, a straw headband, civ color on a sash
        # over one shoulder and an apron, a shovel held diagonally, blade down
        'macro': {'muscle': 0.85, 'weight': 0.5, 'height': 0.7, 'proportions': 0.7},
        'colors': {'skin': '#8e6a48', 'helmet': '#c8b688', 'hair': '#241a10', 'wraps': '#3e2a1c',
                   'wraps_band': '#22160c', 'wood': '#7a5a3a', 'steel': '#b4b6ba', 'iron': '#5e5e60'},
        'helmet': 'cap', 'cap_brim': True, 'two_hands': True, 'tool_dir': (-0.4, -0.62, 0.68), 'tool_face': (0.75, -0.65, 0.0),
        'pose_extra': [('spine_02', (0, -0.14, 1)), ('head', (0, 0.2, 1))],
        'top': 'strap', 'bottom': 'loincloth', 'loin_length': (0.45, 0.4), 'torn': False,
        'tint': ['top', 'bottom'], 'wraps': 'wraps', 'sandals': False,
        'grime': 0.85, 'upper_bulk': 1.1,
        # (realism pass, notes/realism/Worker.md: an ancient labourer with an iron-shod spade carried
        # ready in both fists, the right low on the haft's end, the left up it, the blade up and out past
        # his right shoulder so it reads in every direction; civ sash, apron and hat band)
        # (critic round 1: it read as a glaive brandished overhead: now carried on the right shoulder,
        # the blade up and back behind it with its face to the sky, so the iron shows from above in
        # every direction; the left hand free at the side)
        'props': {'shovel': ('spade', {'length': 1.25, 'grip': 0.32, 'blade': 1.15, 'width': 1.15})},
        # (critic round 2: the blade edge-on or behind the hat in 3-7: the haft out over the shoulder's
        # point so the blade clears the head, its face turned up and out; the fist in closer)
        'stance_name': 'ready', 'hand_r_at': (0.02, -0.2, -0.3), 'elbow_r': 0.25,
        'weapon_dir': (-0.3, 0.62, 0.72), 'edge_dir': (-0.6, -0.2, 0.8),
        'hand_l_at': (0.08, -0.04, -0.58), 'elbow_l': 0.35,
        # (animations: the DEFAULT flic keeps this still; the Worker's actions carry the spade)
        'still_default': True, 'anim_tool': ('spade', {'length': 1.2, 'grip': 0.18, 'blade': 1.1, 'width': 1.1, 'k': 1.15}),
        'pose_extra': [('head', (0, 0.2, 1))],
        'hem_bands': [('helmet', 'tint', 0.03)],
    },
}

# (numpy-mounted) the Medieval Japan riders (mounted.MDJ_RIDERS, mdj_riders.py): o-yoroi over a hitatare, the dou in civ
# lacing rows with a pale stencilled-leather front (tsurubashiri), an o-kabuto lifted off the eyes
_MDJ_RIDER = {'_headgear': 'o_kabuto', 'hair_asset': None, 'headgear_lift': 0.08, 'headgear_scale': 1.0,
              'race': {'caucasian': 0.0, 'african': 0.0, 'asian': 1.0}, 'macro': {'muscle': 0.9, 'weight': 0.85},
              'tint': ['cuirass'], 'metal': [], 'top': 'sleeved', 'cuirass': True, 'bottom': 'long', 'wraps': 'boots',
              'patterns': {'cuirass': [('rows', '#1a1612', 9.0, 0.86), ('panel', '#b49868', 0.07, 'front')]},
              'bands': [('waist', 'belt', 0.05)], 'face_glow': 0.3, 'belt_gear': False}
REMASTER['Shoni no Kagesuke'] = dict(_MDJ_RIDER,
    patterns={'cuirass': [('rows', '#1e1a16', 9.0, 0.55), ('panel', '#b49868', 0.07, 'front')]},   # (R3) dark plates between the lacing
    headgear_params={'maedate': True}, face_glow=0.6, headgear_lift=0.15,   # (round 2) a lit face under the peak
    colors={'cuirass': '#1e1a16', 'top': '#7a5a30', 'bottom': '#d8d0bc', 'boots': '#3a2a1e', 'skin': '#c8a07a', 'belt': '#2a2622',
            'string': '#e8e0c8', 'fletch': '#e8e2d2', 'wood': '#6a4a2a'},
    offhand='bow', offhand_mesh=('yumi', {'height': 1.95, 'thick': 1.6}), weapon=None, back='ebira', back_params={}, pack_up=0.0)
REMASTER['Nakamaro Horseman'] = dict(_MDJ_RIDER,
    headgear_params={'kuwagata': 1.0}, face_glow=0.55, headgear_lift=0.12,   # (round 2) a lit face
    tint=['cuirass'], patterns={'cuirass': [('rows', '#1e1a16', 9.0, 0.75), ('panel', '#b49868', 0.07, 'front')]},   # (round 2) a dark dou, civ lacing rows
    colors={'cuirass': '#1e1a16', 'top': '#2e2c2a', 'bottom': '#3a3834', 'boots': '#2a2420', 'skin': '#c8a07a', 'belt': '#2a2622',
            'steel': '#c8ccd2', 'iron': '#3a3a3c', 'cloth_wrap': '#2a2630', 'leather_dark': '#1a1614'},
    weapon='tachi', props={'tachi': ('tachi', {'length': 0.73, 'width': 0.075, 'back': 0.032, 'bright': True})}, offhand=None)   # (round 2) a real tachi, a third shorter

# (numpy-mounted, batch 2) the League riders (entries: league_riders.ENTRIES)
REMASTER['Teutonic Knight'] = dict(REMASTER['Knight'],
    tint=['shield_emblem'], chest_emblem=('cross', '#161412', 0.24), back_emblem='both', cape_emblem=('cross', '#161412', 0.34, 0.28),   # (R2) the Order's cross on the mantle   # the surcoat's black cross, front and back
    cape=True, cape_length=0.5, cape_width=0.4, cape_flare=0.45,   # the Order's white mantle
    colors=dict(REMASTER['Knight']['colors'], top='#ece8e0', cape='#ece8e0', shield_pale='#161412', shield_rim='#2a2622'),
    props={'lance': ('lance', {'length': 3.2, 'grip': 1.2, 'girth': 1.3, 'pennon': True, 'head': 1.4, 'bold': True})})   # a civ pennon
REMASTER['Roughrider'] = {'hair_asset': 'short02', 'headgear_lift': 0.06, 'headgear_scale': 1.0, 'face_glow': 0.3, 'beard': False,
    'macro': {'muscle': 0.9, 'weight': 0.75},
    'colors': {'skin': '#c8a07a', 'hair': '#4a3424', 'belt': '#b4a47c', 'boots': '#4a301c', 'pants': '#7e6a4c', 'bottom': '#7e6a4c',
               'kerchief': '#b0322a', 'wood': '#6a4428', 'steel': '#585c64'},   # (R3) a lighter gunmetal: the barrel reads at 1x
    'bands': [('waist', 'belt', 0.09), ('neck', 'kerchief', 0.12)],   # (R2) a big red neckerchief   # the Mills cartridge belt, the neckerchief
    'weapon': 'rifle', 'props': {'rifle': ('rifle', {'length': 1.1, 'scale': 1.15})}}   # (R2) heavier: a stock, receiver and dark barrel at 1x   # the Krag carbine


# Entries for further units live in their own modules, merged here.
for _module in ('remaster_footmen', 'remaster_royals', 'remaster_soldiers', 'remaster_mdj'):
    try:
        _more = __import__(_module).REMASTER
    except ImportError:
        _more = {}
    for _k, _v in _more.items():
        if _module == 'remaster_royals':
            # royals rendered ~25-30% taller than the originals (crowns and
            # standards ran out of the frame): a shade smaller by default
            # (women a little larger: they read weak at 0.85); full, wrapping
            # capes; no belt pouch on robes
            _v = dict({'unit_scale': 0.95 if _v.get('gender', 1.0) < 0.5 else 0.85,
                       'upper_bulk': min(_v.get('upper_bulk', 1.0), 1.1),
                       'cape_flare': 0.6, 'cape_wrap': 0.1, 'cape_width': 0.56, 'belt_gear': False},
                      **{k: v for k, v in _v.items() if k != 'upper_bulk'})
        REMASTER.setdefault(_k, _v)


def apply(name, entry):
    out = dict(entry)
    for k, v in REMASTER.get(name, {}).items():
        if isinstance(v, dict) and isinstance(out.get(k), dict):
            merged = dict(out[k])
            merged.update(v)
            out[k] = merged
        else:
            out[k] = v
    return out
