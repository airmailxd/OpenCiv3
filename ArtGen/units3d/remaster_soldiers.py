"""Remaster entries for 12 Civ3 gunpowder-to-modern foot soldiers (and the
Crusader), plain data in the same format as remaster.py's REMASTER: each entry
is merged over the unit's catalog entry, dicts one level deep.

The notes (notes/<Unit>.md) are the truth where they differ from the catalog;
each 'tint' list (plus any ('where', 'tint') band) is the note's "Tinted (civ
color)" line, as far as the supported keys can say it. Direction: Civ3
remastered, not AI-looking -- imposing, hard-bitten soldiers with the
originals' silhouettes, realistic issue colors (olive, khaki, steel, worn
leather), real grime.

These units' catalog entries are the old remake's soldier vocabulary ('hat',
'gun', 'hold', 'legs', 'vest', 'top': 'long_sleeved' ...), which the builder
doesn't read, so every entry sets the builder's own keys in full ('top',
'bottom', 'cuirass', 'helmet'/'headgear', 'weapon', 'offhand', 'stance_name')
and clears what would clash (a catalog 'offhand': 'musket_rest' has no prop).

Guns are held props (props.py: grip/wrist of the stock at the origin, muzzle
+Z): 'weapon' names a key of 'props'. 'port_arms' holds one in the right fist
along 'gun_dir' with the left hand reaching to 'fore_end' (the diagonal port,
or near-vertical for present arms); 'hip_hold' holds it level at the right
hip; 'order_arms' stands a long gun beside the right foot; 'upright' (with
'upright_dir') holds a short one muzzle-up in the fist. Hats go through
'headgear' (riders.HEADGEAR, 'helmet' set to None). Vests (flak, chest rig,
jerkin) are a leather 'cuirass' with 'metal' not listing it; camouflage and
other prints are 'patterns' on the cloth parts. A free arm in the upright and
guard stances is lowered with 'pose_extra' (the stances raise it forward).
What is still approximated is marked "MISSING:" and listed under MISSING
FEATURES at the bottom.
"""

# the free left arm hanging at the side (upright/guard raise it forward)
ARM_DOWN_L = [('upperarm_l', (0.18, 0, -1)), ('lowerarm_l', (0.1, -0.1, -1))]

REMASTER = {
    'Musketman': {
        # upright, legs slightly apart; a matchlock on the right shoulder
        # angled up and forward, a tall forked rest planted in the left hand;
        # steel morion with a civ-color plume, dark leather jerkin with a
        # bandolier, civ-color puffed sleeves and breeches, grey stockings
        'macro': {'muscle': 0.95, 'weight': 0.75, 'height': 0.76, 'proportions': 0.72},
        'race': {'caucasian': 0.9, 'african': 0.05, 'asian': 0.05},
        'colors': {'skin': '#9a7254', 'hair': '#3a2a1c', 'helmet': '#8a8c92', 'cuirass': '#4e463e',
                   'belt': '#2a2220', 'wraps': '#8a857c', 'wraps_band': '#5e5a54', 'boots': '#1e1a16',
                   'wood': '#3a2618', 'steel': '#7e8086', 'bronze': '#8a6a36'},   # dark wood: the rest reads as the original's black pole
        'headgear': 'morion_big', 'helmet': None, 'metal': [],   # high comb, swept brim, big tinted plume
        'hair': 'short', 'beard': True,
        'cuirass': True, 'top': 'sleeved', 'puffy': True,   # the jerkin over civ-color puffed sleeves
        'bottom': 'breeches',
        'wraps': 'wraps', 'sandals': False, 'boots': True,  # grey stockings, dark low boots
        'chest_gear': ('bandolier_bottles', {'scale': 1.4, 'bottle': 'apostle'}),   # the white apostles 'hip_gear': [('haversack', {}, 'back')],   # the apostles, a tan pouch
        'gear': [('chest_strap', {'color': '#2a2220', 'width': 0.08})],
        'weapon': 'musket',
        'props': {'musket': ('musket', {'length': 1.78})},
        'offhand': 'musket_rest', 'offhand_mesh': ('musket_rest', {'length': 1.45, 'grip': 0.8}), 'offhand_dir': (0.05, -0.05, 1.0),
        'stance_name': 'shoulder_arms',         # the matchlock on the right shoulder, sloping back
        'trouser_offset': 0.045,                # puffed breeches
        # MISSING: plume_size (PC: morion_big's plume half as big and standing up); the
        # reference shows it white-grey, not tinted: the owner's call, so 'plume' stays tinted
        'tint': ['top', 'bottom', 'plume'],
        'grime': 0.7, 'upper_bulk': 1.2,
    },
    'Musketeer': {
        # upright; a bell-mouthed musket held vertical in front in both hands;
        # a wide cavalier hat with a big civ-color plume, long hair, a dusty
        # red knee-length coat, a civ-color baldric, a civ-color band at the
        # waist and at the knees, dark breeches and tall black boots
        'macro': {'muscle': 0.95, 'weight': 0.75, 'height': 0.78, 'proportions': 0.72},
        'race': {'caucasian': 0.9, 'african': 0.05, 'asian': 0.05},
        'colors': {'skin': '#9a7052', 'hair': '#2a1e16', 'helmet': '#4a2a2b', 'top': '#7d4742',
                   'bottom': '#262220', 'belt': '#221e1a', 'boots': '#141110', 'wood': '#5a3a24',
                   'steel': '#a8a6a2', 'felt': '#4a2a2b'},
        # wide brim pinned up one side, the plume tinted ('plume' in tint);
        'headgear': 'cavalier', 'helmet': None, 'headgear_params': {'plume': 1.35}, 'headgear_scale': 0.82,   # the hat no wider than the shoulders
        'hair': 'long', 'hair_asset': 'long01', 'beard': True,
        'top': 'sleeved', 'neck': 'high',
        'coat_length': 0.62, 'coat_flare': 1.1,  # the knee-length coat
        'hem_bands': [('coat', 'tint', 0.06)],
        'bottom': 'long', 'wraps': 'boots',     # dark trousers into the boots: no gap at the knee
        'gear': [('chest_strap', {'tint': True, 'width': 0.12})],   # the civ-color baldric, over the left shoulder
        'bands': [('waist', 'tint', 0.2), ('knee', 'tint')],   # the wide civ-color sash round the middle
        'weapon': 'musket',
        'props': {'musket': ('musket', {'length': 1.72, 'flared': True})},   # the bright steel bell at the muzzle
        'offhand': None,
        # present arms: near-vertical in front of the chest, both hands on it
        'stance_name': 'port_arms', 'gun_dir': (0.0, -0.12, 1.0), 'fore_end': 0.3,   # upright before the chest
        'tint': ['plume', 'strap'],
        'grime': 0.6, 'upper_bulk': 1.2,
    },
    'Rifleman': {
        # upright; a long rifle with a long bright bayonet at port arms
        # (butt at the right hip, muzzle toward the left shoulder); a civ-
        # color kepi and frock jacket, brown crossbelts and waist belt with a
        # cartridge box, a tan haversack at the left hip, grey-blue trousers
        'macro': {'muscle': 0.9, 'weight': 0.6, 'height': 0.76, 'proportions': 0.74},
        'colors': {'skin': '#9a7254', 'hair': '#2a1e16', 'bottom': '#5e6c7a', 'belt': '#2a1e14',
                   'sandal': '#141210', 'boots': '#141210', 'wood': '#7a5030', 'steel': '#b8bcc2'},
        'headgear': 'kepi', 'helmet': None,
        'hair': 'short', 'beard': False,
        'top': 'sleeved', 'neck': 'high',
        'coat_length': 0.35,                    # the frock's skirts to mid-thigh
        'bottom': 'long', 'wraps': 'boots',
        'gear': [('chest_strap', {'color': '#4a3624', 'width': 0.11, 'cross': True, 'offset': 0.03})],   # the brown crossbelts
        'hip_gear': [('haversack', {}, 'l'), ('cartridge_box', {}, 'r')],
        'weapon': 'rifle',
        'props': {'rifle': ('rifle_bolt', {'length': 1.65, 'bayonet': True})},   # the long rifle and bayonet
        # right hand on the wrist at the right hip, left on the fore-end,
        # muzzle up at the left shoulder
        'stance_name': 'port_arms', 'gun_dir': (0.45, -0.12, 0.88), 'fore_end': 0.25,
        'pose_extra': [('head', (0, 0.25, 1))],
        'tint': ['top', 'hat'],
        'grime': 0.7, 'upper_bulk': 1.25,
    },
    'Infantry': {
        # square stance, legs apart; a Garand with a thin bayonet level across
        # the waist, muzzle forward; a steel-grey M1 helmet, a civ-color field
        # shirt, webbing, a big blocky civ-color pack high on the back,
        # mottled tan trousers, off-white gaiters, brown boots
        'macro': {'muscle': 1.0, 'weight': 0.75, 'height': 0.76, 'proportions': 0.72},
        'colors': {'skin': '#9a7254', 'hair': '#3a2a1c', 'olive': '#62625c', 'olive_dark': '#5e5a48',
                   'bottom': '#8a7a5e', 'belt': '#8a7656', 'boots': '#4a3020', 'wraps': '#bcb09a',
                   'wraps_band': '#8e8470', 'wood': '#7a4a28', 'steel': '#5a5c5e'},
        'headgear': 'modern_helmet', 'helmet': None,   # the M1 (its paint is the 'olive' color)
        'hair': 'short', 'beard': False,
        'top': 'sleeved', 'neck': 'high',
        'bottom': 'long',
        'patterns': {'bottom': ('camo', '#a4967c', '#6a5a42')},   # mottled brown/tan
        'boots': True, 'wraps': 'wraps',        # the off-white gaiters (G.fur wraps)
        'gear': [('chest_strap', {'color': '#b8a47c', 'width': 0.08, 'cross': True, 'offset': 0.03})],   # light tan webbing
        # the big blocky pack (mil_backpack's bag is 'olive_dark', its roll 'olive');
        'back': 'mil_backpack', 'back_params': {'size': 1.4, 'tint': True},   # solid civ color, as the original 'pack_up': 0.18,
        'weapon': 'rifle',
        'props': {'rifle': ('rifle_bolt', {'length': 1.2, 'bayonet': True})},
        'stance_name': 'hip_hold',              # butt at the right hip, elbows bent (the stance's defaults)
        'tint': ['top'],
        'grime': 0.8, 'upper_bulk': 1.25,
    },
    'Marine': {
        # upright, feet apart; an M16 low at the right hip, level and forward,
        # the left hand on the handguard; a PASGT helmet in woodland camo with
        # a civ-color band, a camo flak vest over civ-color sleeves, camo
        # cargo trousers, black boots, a small pale canteen on the belt
        'macro': {'muscle': 1.0, 'weight': 0.8, 'height': 0.78, 'proportions': 0.72},
        'colors': {'skin': '#9a7456', 'hair': '#2a2018', 'olive': '#454c3c', 'olive_dark': '#c8c4b4',
                   'cuirass': '#4a4c36', 'bottom': '#353e35', 'belt': '#26261f', 'boots': '#121212'},
        # the band takes the civ color by itself (props.modern_helmet); a woodland cover
        'headgear': 'modern_helmet', 'helmet': None, 'headgear_params': {'band': True, 'camo': True},
        'hair': 'short', 'beard': False,
        'top': 'sleeved', 'neck': 'high',
        'cuirass': True, 'metal': [],           # the flak vest, in woodland camo (a patterned vest becomes cloth)
        'cloth_rough': 0.95, 'tint_shade': {'top': 0.72},   # matte cloth; the civ sleeves a shade darker, less glare
        'bottom': 'long',
        # woodland: on the trousers, and as a darker mottle on the civ-color sleeves
        # base dark green; mid green and brown dominate, khaki in small spots
        'patterns': {'cuirass': ('camo', '#4a5a3c', '#55352d', '#909369'),
                     'bottom': ('camo', '#4a5a3c', '#55352d', '#909369'),
                     'top': ('camo', '#8a8a8a', '#5a5a5a')},
        'boots': True, 'wraps': 'boots',
        'gear': [],
        'hip_gear': [('canteen', {'scale': 1.3}, 'back')],   # the small pale canteen ('olive_dark') on the back of the belt
        'weapon': 'rifle',
        'props': {'rifle': ('assault_rifle', {'scale': 1.35})},
        'stance_name': 'hip_hold',              # butt at the right hip, muzzle forward
        'tint': ['top'],
        'grime': 0.75, 'upper_bulk': 1.3,
    },
    'Paratrooper': {
        # upright and alert; a compact black carbine across the waist in both
        # hands, muzzle to the front-left; a white helmet, light grey-white
        # mottled camo, a civ-color chest vest and gloves, white knee pads, a
        # big grey parachute pack, black boots
        'macro': {'muscle': 1.0, 'weight': 0.7, 'height': 0.76, 'proportions': 0.74},
        'colors': {'skin': '#9a7456', 'hair': '#2a2018', 'olive': '#cfcfd2', 'olive_dark': '#6e6e74',
                   'top': '#d4d2d2', 'bottom': '#d4d2d2', 'belt': '#36363a', 'boots': '#1c1c1e',
                   'kneepad': '#faf8f2'},
        'headgear': 'modern_helmet', 'helmet': None,
        'hair': 'short', 'beard': False,
        'top': 'sleeved', 'neck': 'high',
        'cuirass': True, 'metal': [],           # the civ-color chest vest
        'bottom': 'long',
        'patterns': {'top': ('camo', '#9a979c', '#5a5a62', '#3e3e46'),      # urban white/grey/charcoal
                     'bottom': ('camo', '#9a979c', '#5a5a62', '#3e3e46')},
        'boots': True, 'wraps': 'boots',
        'bands': [('knee', 'kneepad', 0.1), ('wrist', 'tint')],   # MISSING: gloves (tinted hands; wrist bands stand in)
        'gear': [],
        'back': 'chute_pack',                   # the big grey parachute pack
        'weapon': 'rifle',
        'props': {'rifle': ('assault_rifle', {'scale': 1.25})},
        'stance_name': 'hip_hold',              # butt at the right hip, muzzle front-left
        'tint': ['cuirass'],
        'grime': 0.6, 'upper_bulk': 1.25,
    },
    'WWII Paratrooper': {
        # upright, feet apart; a Thompson low across the hip, muzzle forward;
        # a netted M1, an olive-khaki camo jump smock with a civ-color chest
        # front, a civ-color belt and knee bands, harness, a huge camo chute
        # bundle on the back, brown jump boots
        'macro': {'muscle': 1.0, 'weight': 0.75, 'height': 0.76, 'proportions': 0.72},
        'colors': {'skin': '#9a7254', 'hair': '#2a2018', 'olive': '#4e4d3c', 'top': '#7a7450',
                   'bottom': '#7a7450', 'belt': '#34322a', 'boots': '#5a3a22', 'pack': '#4e4d3c',
                   'bedroll': '#5e5c44', 'pot': '#3e3e32', 'wood': '#6e5030',
                   'chute_tan': '#8a8462', 'olive_dark': '#5e3e22', 'flag': '#a8322c'},   # dark olive chute; the smg's stock is 'olive_dark': brown wood
        'headgear': 'modern_helmet', 'helmet': None, 'headgear_params': {'net': True},   # the netted M1
        'hair': 'short', 'beard': False,
        'top': 'sleeved', 'neck': 'high',
        'cuirass': True, 'metal': [],           # the civ-color chest front
        'bottom': 'long',
        'patterns': {'top': ('camo', '#9e9562', '#49432f', '#2e2a20'),     # olive-khaki smock
                     'bottom': ('camo', '#9e9562', '#49432f', '#2e2a20')},
        'boots': True, 'wraps': 'boots',
        'harness': True,
        'bands': [('knee', 'tint', 0.08)],
        'shoulder_patch': ('l', 'flag'),        # the flag on the left shoulder
        'gear': [],
        'back': 'para_pack', 'back_params': {'size': 1.45, 'bag': 'chute_tan', 'camo': True},   # the huge camo chute bundle
        'weapon': 'smg',
        'props': {'smg': ('smg', {'scale': 1.4})},
        'stance_name': 'hip_hold',              # butt at the right hip, muzzle forward
        'tint': ['cuirass', 'belt'],
        'grime': 0.8, 'upper_bulk': 1.25,
    },
    'Guerilla': {
        # a relaxed stand; an AK upright at the right shoulder in the right
        # hand, the left arm loose; a civ-color bandana, a short-sleeved camo
        # shirt with bare forearms, civ-color bandoliers crossed on the chest,
        # baggy camo trousers, dark boots, a small brown pack
        'macro': {'muscle': 0.95, 'weight': 0.55, 'height': 0.74, 'proportions': 0.74},
        'race': {'caucasian': 0.3, 'african': 0.35, 'asian': 0.35},
        'colors': {'skin': '#8e6646', 'hair': '#1a140e', 'top': '#7a9a32', 'bottom': '#76922e',
                   'belt': '#2a2014', 'boots': '#2e2216', 'olive_dark': '#5a4430', 'olive': '#4a3a28',
                   'wood': '#5e3e16'},
        'helmet': 'headband',                   # the tied bandana; MISSING: headband_height (it sits low, over the eyes)
        'hair': 'short', 'beard': True,
        'top': 'tunic',                         # short sleeves, forearms bare
        'bottom': 'long', 'puffy': True,        # MISSING: loose_trousers (puffy still reads skin-tight)
        # lighter woodland; MISSING: cloth_roughness (the dark camo reads wet)
        # busy woodland over a lime base: dark green, brown and near-black blotches
        'patterns': {'bottom': ('camo', '#25340a', '#5a4a24', '#141a08'),
                     'top': ('camo', '#25340a', '#5a4a24', '#141a08')},
        'boots': True, 'wraps': 'boots',
        # the crossed civ-color bandoliers, over the tunic; MISSING: cartridge loops on them
        'gear': [('chest_strap', {'tint': True, 'width': 0.07, 'cross': True, 'offset': 0.035})],
        'back': 'mil_backpack', 'back_params': {'size': 0.7},   # the small brown pack
        'weapon': 'rifle',
        'props': {'rifle': ('ak', {'scale': 1.25})},
        # muzzle up in the fist; MISSING: rifle_shoulder (vertical against the right shoulder)
        'stance_name': 'upright', 'upright_dir': (-0.05, 0.05, 1.0),
        'pose_extra': ARM_DOWN_L,
        'tint': ['helmet'],
        'grime': 0.9, 'upper_bulk': 1.15,
    },
    'Settler Modern Times': {
        # walking; a tall staff planted at his side, the other arm loose; a
        # civ-color boonie hat, a white shirt under a civ-color vest, khaki
        # trousers, dark high boots, a huge brown pack with a bedroll on top
        'macro': {'muscle': 0.9, 'weight': 0.6, 'height': 0.74, 'proportions': 0.74},
        'colors': {'skin': '#9a7254', 'hair': '#413025', 'top': '#cfc8b8', 'bottom': '#a19379',
                   'belt': '#3a2a1e', 'boots': '#221a14', 'olive_dark': '#6a4a2e', 'olive': '#a08e64',   # a pale bedroll roll on the pack
                   'wood': '#4e3420', 'leather': '#3a2a1c'},
        # the hat is tinted per the note, but the reference row shows tan: the
        # owner's call. MISSING: boonie_level (the pinned brim tilts it); tall_boots
        'helmet': None, 'headgear': 'boonie', 'metal': [],
        'hair': 'short', 'beard': True,
        'top': 'sleeved',
        'cuirass': True,                        # the civ-color vest ('metal' leaves it out: leather)
        'bottom': 'long', 'wraps': 'boots',
        # the pack's shoulder straps, dark and wide over the vest
        'gear': [('chest_strap', {'color': '#241a12', 'width': 0.08, 'cross': True, 'offset': 0.035})],
        # the tall frame pack with its bedroll on top
        'back': 'mil_backpack', 'back_params': {'size': 1.6},
        'weapon': 'staff',
        'props': {'staff': ('staff', {'length': 1.9})},
        # the staff is held straight up (equip's staff branch ignores
        # 'upright_dir'), so the right upper arm comes in to bring it close
        'stance_name': 'upright',
        'pose_extra': ARM_DOWN_L + [('upperarm_r', (-0.15, -0.05, -1))],
        'tint': ['helmet', 'cuirass'],
        'grime': 0.65, 'upper_bulk': 1.15,
    },
    'Worker Modern Times': {
        # a slight forward lean, legs apart; a shovel low in both hands, blade
        # forward and down; bareheaded, short brown hair; a civ-color shirt
        # with rolled sleeves, light tan bib overalls with Y braces, a dark
        # tool belt with pouches, black boots
        'macro': {'muscle': 0.95, 'weight': 0.6, 'height': 0.74, 'proportions': 0.74},
        'colors': {'skin': '#9a7052', 'hair': '#5a3a22', 'bottom': '#a8946c',
                   'belt': '#2e241a', 'boots': '#1e1814', 'wood': '#9a7a52', 'steel': '#7e8084'},
        'helmet': None, 'hair': 'short', 'beard': False,
        'top': 'sleeved',                       # long civ-color sleeves (rolled: MISSING rolled_sleeves)
        'cuirass': False,
        'bottom': 'long', 'puffy': True,        # the overalls sit over the shirt (no red at the crotch)
        'boots': True, 'wraps': 'boots',
        # the braces, dark and wide; MISSING: y_braces (straight braces; these cross), tool_belt
        'gear': [('chest_strap', {'color': '#241c14', 'width': 0.08, 'cross': True, 'offset': 0.03})],
        'hip_gear': [('cartridge_box', {'scale': 1.1}, 'r'), ('cartridge_box', {'scale': 0.9}, 'l')],   # tool-belt pouches
        'weapon': 'shovel',
        'props': {'shovel': ('shovel', {'length': 1.25, 'd_grip': True})},
        # level across the waist in both hands: the blade (the prop's +Z, along
        # -tool_dir) at the figure's left, the right hand on the haft toward the D-grip
        'stance_name': 'low_across', 'two_hands': True, 'tool_dir': (-0.95, 0.0, 0.2),
        'tint': ['top'],
        'grime': 0.85, 'upper_bulk': 1.2,
    },
    'Crusader': {
        # upright; a sword low in the right hand, blade down-forward; a heater
        # shield on the left arm; a closed flat-topped great helm, full mail,
        # an off-white knee-length surcoat quartered civ color with a white
        # cross, civ-color shoulders and hem band; gold at the spurs
        'macro': {'muscle': 1.0, 'weight': 0.9, 'height': 0.78, 'proportions': 0.68},
        'colors': {'skin': '#94704f', 'top': '#d6d2c8', 'bottom': '#d6d2c8', 'helmet': '#7e8086',
                   'mail': '#5e5e62', 'belt': '#4a3220', 'greaves': '#6e7074', 'gold': '#a8842e',
                   'steel': '#a8aaae', 'shield_face': '#d6d2c8', 'shield_rim': '#6e5434', 'bronze': '#8a6a36'},
        'headgear': 'great_helm', 'helmet': None, 'metal': [], 'hair_asset': None, 'beard': False,
        'top': 'vest', 'neck': 'high', 'collar': 'mail',
        'mail_parts': ['arms', 'legs'],         # mail sleeves and chausses
        'bottom': 'skirt',                      # the surcoat's skirt to the knee
        'chest_emblem': ('cross', '#ece8dc', 0.32), 'back_emblem': 'both',   # a white cross front and back
        'hem_bands': [('bottom', 'tint', 0.05)],
        'bands': [('neck', 'tint'), ('upperarm', 'tint'), ('ankle', 'gold')],   # civ-color shoulders, gold spurs
        'wraps': 'greaves', 'leather_bracers': False,
        'gear': [],
        'weapon': 'sword',
        'props': {'sword': ('sword', {'length': 0.85, 'width': 0.06, 'guard': 0.2})},
        'hang_dir': (-0.3, -0.25, -0.92),       # blade low, down and a little forward
        # the left forearm stays across the body, in the shield's straps
        # the original's blue heater with a white cross
        'offhand': 'shield_heater', 'offhand_mesh': ('shield_heater', {'w': 0.66, 'h': 0.9}),
        'shield_out': 0.2,
        'stance_name': 'guard',
        'tint': ['shield_emblem', 'top', 'bottom'],   # the surcoat in the civ color
        'grime': 0.65, 'upper_bulk': 1.3,
    },
    'TOW Infantry': {
        # a two-man team round a TOW launcher on a low tripod: one kneels at
        # the sight, the other stands beside it, a hand raised to his face,
        # spotting. Both: PASGT helmets in camo covers, woodland camo, a civ-
        # color Y harness, shoulders, belt and bands below the knee, brown
        # boots; two olive missile crates on the ground. Built as the
        # standing spotter.
        'macro': {'muscle': 1.0, 'weight': 0.75, 'height': 0.76, 'proportions': 0.72},
        'colors': {'skin': '#9a7254', 'hair': '#2a2018', 'olive': '#4e5240', 'top': '#3e4232',
                   'bottom': '#3e4232', 'belt': '#2e2e26', 'boots': '#4a3420'},
        'headgear': 'modern_helmet', 'helmet': None, 'headgear_params': {'camo': True},   # camo covers
        'hair': 'short', 'beard': False,
        'top': 'sleeved', 'neck': 'high',
        'bottom': 'long',
        'patterns': {'top': ('camo', '#2f3326', '#523d25', '#7a7a5a'),     # woodland: dark green and brown, khaki spots
                     'bottom': ('camo', '#2f3326', '#523d25', '#7a7a5a')},
        'boots': True, 'wraps': 'boots',
        # the civ-color harness, crossed over the shirt; MISSING: y_harness
        'gear': [('chest_strap', {'tint': True, 'width': 0.1, 'cross': True, 'offset': 0.035})],
        'bands': [('upperarm', 'tint', 0.07), ('knee', 'tint', 0.08)],   # the civ-color shoulders and bands below the knee
        'shoulder_patch': ('l', 'flag'), 'patch_out': 0.05, 'patch_size': 0.1, 'patch_t': 0.55,   # a big flag patch below the civ band
        # MISSING: ground_props (props.tow_launcher on its tripod and two missile
        # crates set on the ground beside him; equip only puts props in hands)
        # MISSING: team (a second, kneeling soldier at the launcher's sight)
        'weapon': None, 'offhand': None,
        # spotting: the left arm down, the right upper arm forward (not above
        # the helmet), the forearm back so the hand shades the brow
        'stance_name': 'guard',
        'pose_extra': ARM_DOWN_L + [('upperarm_r', (-0.3, -0.8, -0.05)), ('lowerarm_r', (0.6, 0.3, 0.6))],
        'tint': ['belt'],
        'grime': 0.8, 'upper_bulk': 1.25,
    },
}

# MISSING FEATURES
# What the notes still need that the supported keys can't express. Each entry
# above approximates it and marks the spot with "MISSING:". (PC) items need
# the prop or builder changed on the PC with Blender, not data.
#
# plume_size (PC)    2  Musketman: morion_big's plume about half as big and standing up (it hangs down the
#                       back and clips the frame); its tint is the owner's call (reference white-grey).
# para_pack_shape (PC) 1  WWII Paratrooper: a huge bulky camo bundle (renders as a thin plate; now dark olive).
# shoulder_arms      1  Musketman: musket on the right shoulder, angled up-forward (order_arms stands in).
# vest_camo          1  Marine: camo on the cuirass ('patterns' only reach cloth; the vest is leather).
# gloves             1  Paratrooper: tinted hands (wrist bands stand in).
# rifle_shoulder     1  Guerilla: rifle vertical against the right shoulder ('upright' holds it out in the fist).
# headband_height    1  Guerilla: the headband up on the forehead (its height is fixed in units.helmet).
# loose_trousers     1  Guerilla: baggy trousers ('puffy' still reads skin-tight).
# cloth_roughness    1  Guerilla: a matte camo (G.cloth's roughness isn't an entry key).
# cartridge loops    1  Guerilla: loops on the crossed bandoliers (plain tinted straps now).
# boonie_level       1  Settler Modern Times: a level boonie (the pinned brim tilts it); the hat's tint
#                       (tan in the reference, tinted in the note) is the owner's call.
# tall_boots         1  Settler Modern Times: knee-high boots.
# y_braces           1  Worker Modern Times: straight Y braces (crossed chest straps stand in).
# tool_belt          1  Worker Modern Times: a belt with pouches at the hip (the default belt pouch stands in).
# back_emblem        1  Crusader: chest_emblem on the back of the surcoat as well.
# y_harness          1  TOW Infantry: a tinted Y harness (crossed tinted chest straps stand in).
# ground_props       1  TOW Infantry: tow_launcher on its tripod and missile crates on the ground (no crate prop).
# team               1  TOW Infantry: a second, kneeling figure at the sight.
