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
along 'gun_dir' with the left hand reaching to 'fore_end' (used both for the
diagonal port and, with a level gun_dir, for hip holds); 'order_arms' stands
a long gun beside the right foot; 'upright' (with 'upright_dir') holds a short
one muzzle-up in the fist. Hats go through 'headgear' (riders.HEADGEAR, 'helmet' left
unset) where a kind exists; the old 'helmet' path is kept where it can tint a
plume. Vests (flak, chest rig, jerkin, bib) are a leather 'cuirass' with
'metal' not listing it. What is still approximated is marked "MISSING:" and
listed under MISSING FEATURES at the bottom.
"""

REMASTER = {
    'Musketman': {
        # upright, legs slightly apart; a matchlock on the right shoulder
        # angled up and forward, a tall forked rest planted in the left hand;
        # steel morion with a civ-color plume, dark leather jerkin with a
        # bandolier, civ-color puffed sleeves and breeches, grey stockings
        'macro': {'muscle': 0.95, 'weight': 0.75, 'height': 0.76, 'proportions': 0.72},
        'race': {'caucasian': 0.9, 'african': 0.05, 'asian': 0.05},
        'colors': {'skin': '#9a7254', 'hair': '#3a2a1c', 'helmet': '#8a8c92', 'cuirass': '#4e463e',
                   'belt': '#2a2220', 'wraps': '#8a857c', 'wraps_band': '#5e5a54', 'sandal': '#1e1a16',
                   'wood': '#5e3e24', 'steel': '#7e8086', 'bronze': '#8a6a36'},
        # the old helmet path: the only morion that takes a tinted plume
        # (riders' 'morion' headgear has a better comb and brim but no plume)
        'helmet': 'morion', 'plume': True, 'metal': ['helmet'],
        'hair': 'short', 'beard': True,
        'cuirass': True, 'top': 'sleeved', 'puffy': True,   # the jerkin over civ-color puffed sleeves
        'bottom': 'breeches',
        'wraps': 'wraps', 'sandals': True,      # grey stockings, dark shoes; MISSING: shoes (sandals stand in)
        'gear': [('chest_strap', {'color': '#2a2220'})],   # MISSING: bandolier_bottles (the apostles on the strap); hip_pouch (tan)
        'weapon': 'musket',
        'props': {'musket': ('musket', {'length': 1.78})},
        'offhand': None,                        # MISSING: musket_rest (a tall forked rest planted in the left hand)
        'stance_name': 'order_arms',            # MISSING: shoulder_arms (musket on the right shoulder, angled up-forward)
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
        'colors': {'skin': '#9a7052', 'hair': '#2a1e16', 'helmet': '#4a2a2b', 'top': '#6e3e3a',
                   'bottom': '#262220', 'belt': '#221e1a', 'boots': '#141110', 'wood': '#5a3a24',
                   'steel': '#a8a6a2'},
        'helmet': 'cap', 'cap_brim': True, 'plume': True, 'metal': [],   # MISSING: hat cavalier (wide brim pinned up one side)
        'hair': 'long', 'hair_asset': 'long01', 'beard': True,
        'top': 'sleeved', 'neck': 'high',       # MISSING: long_coat (knee-length skirts over the breeches)
        'bottom': 'breeches', 'wraps': 'boots', # MISSING: tall_boots (to the knee, cuffed)
        'gear': [('chest_strap', {'color': '#3a2a20'})],   # the baldric, over the left shoulder
        'bands': [('waist', 'tint'), ('knee', 'tint')],    # MISSING: hem_band (the coat's hem, not the waist)
        'weapon': 'musket',
        'props': {'musket': ('musket', {'length': 1.72})},   # MISSING: flared_muzzle (bell-mouthed, pale steel)
        'offhand': None,
        'stance_name': 'order_arms',            # MISSING: present_arms (vertical in front of the chest, both hands on it)
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
        'headgear': 'kepi',
        'hair': 'short', 'beard': False,
        'top': 'sleeved', 'neck': 'high',       # MISSING: long_coat (the frock's skirts to mid-thigh)
        'bottom': 'long', 'wraps': 'boots',     # MISSING: shoes (low boots stand in)
        'harness': True,                        # the brown crossbelts; MISSING: cartridge_box (on the belt behind)
        'gear': [],                             # MISSING: haversack (a tan bag at the left hip; 'hip' has no bag prop)
        'weapon': 'rifle',
        'props': {'rifle': ('rifle_bolt', {'length': 1.25, 'bayonet': True})},
        # right hand on the wrist at the right hip, left on the fore-end,
        # muzzle up at the left shoulder
        'stance_name': 'port_arms', 'gun_dir': (0.6, -0.3, 0.75), 'fore_end': 0.45,
        'tint': ['top', 'hat'],
        'grime': 0.7, 'upper_bulk': 1.15,
    },
    'Infantry': {
        # square stance, legs apart; a Garand with a thin bayonet level across
        # the waist, muzzle forward; a steel-grey M1 helmet, a civ-color field
        # shirt, webbing, a big blocky civ-color pack high on the back,
        # mottled tan trousers, off-white gaiters, brown boots
        'macro': {'muscle': 1.0, 'weight': 0.75, 'height': 0.76, 'proportions': 0.72},
        'colors': {'skin': '#9a7254', 'hair': '#3a2a1c', 'olive': '#62625c', 'bottom': '#7e6e52',
                   'belt': '#8a7656', 'boots': '#4a3020', 'wraps': '#bcb09a', 'wraps_band': '#8e8470',
                   'pack': '#6a6450', 'bedroll': '#5e5844', 'pot': '#4a4a40', 'wood': '#6a4628',
                   'steel': '#5a5c5e'},
        'headgear': 'modern_helmet', 'helmet': None,   # the M1 (its paint is the 'olive' color)
        'hair': 'short', 'beard': False,
        'top': 'sleeved', 'neck': 'high',
        'bottom': 'long',                       # MISSING: camo_pattern (mottled brown/tan trousers)
        'boots': True, 'wraps': 'wraps',        # MISSING: gaiters (canvas, off-white; G.fur wraps stand in)
        'harness': True,                        # the webbing
        'gear': [],
        # MISSING: mil_backpack on 'back' (equip only takes 'quiver'/'backpack');
        # MISSING: tinted_pack (the original's pack is solid civ color)
        'back': 'backpack', 'pack_scale': 1.15,
        'weapon': 'rifle',
        'props': {'rifle': ('rifle_bolt', {'length': 1.1, 'bayonet': True})},
        'stance_name': 'hip_hold',
        # level, muzzle forward (a touch down, which keeps the sights up): right
        # hand on the wrist, left on the fore-end; MISSING: hip_hold (a stance
        # with the hands low at the right hip; port_arms' arms stand in)
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
                   'cuirass': '#3e4638', 'bottom': '#4a5240', 'belt': '#26261f', 'boots': '#121212'},
        'headgear': 'modern_helmet', 'helmet': None,   # MISSING: helmet_band (tinted) and camo_pattern on the cover
        'hair': 'short', 'beard': False,
        'top': 'sleeved', 'neck': 'high',
        'cuirass': True, 'metal': [],           # the flak vest (leather stands in for the camo cloth)
        'bottom': 'long',                       # MISSING: camo_pattern; cargo_pockets
        'boots': True, 'wraps': 'boots',
        'gear': [],
        # MISSING: hip_gear placement -- 'hip' now hangs its prop +Z down (a
        # scabbard), which would stand props.canteen on its head; the small pale
        # canteen on the back belt is left out
        'weapon': 'rifle',
        'props': {'rifle': ('assault_rifle', {})},
        'stance_name': 'hip_hold',
        # level, muzzle forward; MISSING: hip_hold
        'tint': ['top'],
        'grime': 0.75, 'upper_bulk': 1.3,
    },
    'Paratrooper': {
        # upright and alert; a compact black carbine across the waist in both
        # hands, muzzle to the front-left; a white helmet, light grey-white
        # mottled camo, a civ-color chest vest and gloves, white knee pads, a
        # big grey parachute pack, black boots
        'macro': {'muscle': 1.0, 'weight': 0.7, 'height': 0.76, 'proportions': 0.74},
        'colors': {'skin': '#9a7456', 'hair': '#2a2018', 'olive': '#cfcfd2', 'top': '#a8a6aa',
                   'bottom': '#a8a6aa', 'belt': '#36363a', 'boots': '#1c1c1e', 'kneepad': '#d8d6d2',
                   'pack': '#6e6e74', 'bedroll': '#5e5e64', 'pot': '#4a4a50'},
        'headgear': 'modern_helmet', 'helmet': None,
        'hair': 'short', 'beard': False,
        'top': 'sleeved', 'neck': 'high',       # MISSING: camo_pattern (white/grey/charcoal mottle)
        'cuirass': True, 'metal': [],           # the civ-color chest vest
        'bottom': 'long',
        'boots': True, 'wraps': 'boots',
        'bands': [('knee', 'kneepad'), ('wrist', 'tint')],   # MISSING: gloves (tinted hands; wrist bands stand in)
        'gear': [],
        'back': 'backpack', 'pack_scale': 1.2,  # MISSING: chute_pack (a big grey parachute pack, not the settler's sack)
        'weapon': 'rifle',
        'props': {'rifle': ('assault_rifle', {'scale': 0.85})},
        'stance_name': 'hip_hold',   # across the waist, muzzle front-left
        # MISSING: hip_hold
        'tint': ['cuirass'],
        'grime': 0.6, 'upper_bulk': 1.25,
    },
    'WWII Paratrooper': {
        # upright, feet apart; a Thompson low across the hip, muzzle forward;
        # a netted M1, an olive-khaki camo jump smock with a civ-color chest
        # front, a civ-color belt and knee bands, harness, a huge camo chute
        # bundle on the back, brown jump boots
        'macro': {'muscle': 1.0, 'weight': 0.75, 'height': 0.76, 'proportions': 0.72},
        'colors': {'skin': '#9a7254', 'hair': '#2a2018', 'olive': '#4e4d3c', 'top': '#6e6c52',
                   'bottom': '#6e6c52', 'belt': '#34322a', 'boots': '#5a3a22', 'pack': '#4e4d3c',
                   'bedroll': '#5e5c44', 'pot': '#3e3e32', 'wood': '#6e5030'},
        'headgear': 'modern_helmet', 'helmet': None,   # MISSING: helmet_net
        'hair': 'short', 'beard': False,
        'top': 'sleeved', 'neck': 'high',       # MISSING: camo_pattern; flag_patch (on the shoulder)
        'cuirass': True, 'metal': [],           # the civ-color chest front
        'bottom': 'long',
        'boots': True, 'wraps': 'boots',
        'harness': True,
        'bands': [('knee', 'tint')],
        'gear': [],
        'back': 'backpack', 'pack_scale': 1.3,  # MISSING: chute_pack (a huge camo-wrapped bundle)
        'weapon': 'smg',
        'props': {'smg': ('smg', {})},
        'stance_name': 'hip_hold',
        # level, muzzle forward; MISSING: hip_hold
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
        'colors': {'skin': '#8e6646', 'hair': '#1a140e', 'top': '#46601a', 'bottom': '#3e5418',
                   'belt': '#2a2014', 'boots': '#2e2216', 'pack': '#5a4430', 'bedroll': '#4a3a28',
                   'pot': '#3e3428', 'wood': '#5e3e16'},
        'helmet': 'headband',                   # the tied bandana; MISSING: bandana_knot (tails at the back)
        'hair': 'short', 'beard': True,
        'top': 'tunic',                         # short sleeves; MISSING: camo_pattern (woodland)
        'bottom': 'long', 'puffy': True,        # baggy trousers ('puffy' only fattens the breeches/sleeves)
        'boots': True, 'wraps': 'boots',
        'harness': True,                        # the crossed bandoliers; MISSING: bandolier_rounds (cartridge loops)
        'gear': [],
        'back': 'backpack', 'pack_scale': 0.65, # MISSING: mil_backpack on 'back'; tinted_pack_straps
        'weapon': 'rifle',
        'props': {'rifle': ('assault_rifle', {})},   # MISSING: ak (wooden furniture, curved mag)
        'stance_name': 'upright', 'upright_dir': (-0.05, 0.05, 1.0),   # muzzle up in the fist; MISSING: rifle_shoulder (vertical against the right shoulder, arm loose left)
        'tint': ['helmet', 'harness'],
        'grime': 0.9, 'upper_bulk': 1.15,
    },
    'Settler Modern Times': {
        # walking; a tall staff planted at his side, the other arm loose; a
        # civ-color boonie hat, a white shirt under a civ-color vest, khaki
        # trousers, dark high boots, a huge brown pack with a bedroll on top
        'macro': {'muscle': 0.9, 'weight': 0.6, 'height': 0.74, 'proportions': 0.74},
        'colors': {'skin': '#9a7254', 'hair': '#413025', 'top': '#cfc8b8', 'bottom': '#958a70',
                   'belt': '#3a2a1e', 'boots': '#221a14', 'pack': '#6a4a2e', 'bedroll': '#4a3a2a',
                   'pot': '#7a5a3a', 'wood': '#4e3420', 'leather': '#3a2a1c'},
        'helmet': None, 'headgear': 'boonie', 'metal': [],
        'hair': 'short', 'beard': True,
        'top': 'sleeved',
        'cuirass': True,                        # the civ-color vest ('metal' leaves it out: leather)
        'bottom': 'long', 'wraps': 'boots',     # MISSING: tall_boots (knee-high)
        'gear': [],
        'back': 'backpack', 'pack_scale': 1.35,
        'weapon': 'staff',
        'props': {'staff': ('staff', {'length': 1.9})},
        'stance_name': 'upright',               # MISSING: walking (mid-stride)
        'tint': ['helmet', 'cuirass'],
        'grime': 0.65, 'upper_bulk': 1.15,
    },
    'Worker Modern Times': {
        # a slight forward lean, legs apart; a shovel low in both hands, blade
        # forward and down; bareheaded, short brown hair; a civ-color shirt
        # with rolled sleeves, light tan bib overalls with Y braces, a dark
        # tool belt with pouches, black boots
        'macro': {'muscle': 0.95, 'weight': 0.6, 'height': 0.74, 'proportions': 0.74},
        'colors': {'skin': '#9a7052', 'hair': '#5a3a22', 'cuirass': '#a08c66', 'bottom': '#a08c66',
                   'belt': '#2e241a', 'boots': '#1e1814', 'wood': '#9a7a52', 'steel': '#7e8084'},
        'helmet': None, 'hair': 'short', 'beard': False,
        'top': 'sleeved',                       # MISSING: rolled_sleeves (forearms bare)
        'cuirass': True, 'metal': [],           # the bib; MISSING: bib_overalls (cloth bib joined to the trousers)
        'bottom': 'long',
        'harness': True,                        # the braces (crossed, not Y); MISSING: tool_belt (pouches at the hip)
        'boots': True, 'wraps': 'boots',
        'gear': [],
        'weapon': 'shovel',
        'props': {'shovel': ('shovel', {'length': 1.25, 'd_grip': True})},
        'stance_name': 'low_across', 'two_hands': True,   # MISSING: dig_lean (leaning in, blade forward and down)
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
        'top': 'vest', 'neck': 'high', 'collar': 'mail',   # MISSING: mail_sleeves / mail_chausses
        'bottom': 'skirt',                      # the surcoat's skirt to the knee
        # MISSING: surcoat_cross (civ-color quarters, white cross); hem_band (tinted)
        'bands': [('neck', 'tint'), ('upperarm', 'tint'), ('ankle', 'gold')],   # civ-color shoulders, gold spurs
        'wraps': 'greaves', 'leather_bracers': False,
        'gear': [],
        'weapon': 'sword',
        'props': {'sword': ('sword', {'length': 0.85, 'width': 0.06, 'guard': 0.2})},
        # MISSING: heater_shield (a kite/heater board) and tinted_shield_face
        # (the face civ color with a white cross: the oval's tinted cross on a
        # white face is the inverse)
        'offhand': 'shield_oval',
        'offhand_mesh': ('shield_oval', {'w': 0.55, 'h': 0.8, 'band': 0.1, 'dome': 0.04}),
        'stance_name': 'guard',
        'tint': ['shield_emblem'],
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
        'headgear': 'modern_helmet', 'helmet': None,   # MISSING: camo_pattern on the cover
        'hair': 'short', 'beard': False,
        'top': 'sleeved', 'neck': 'high',       # MISSING: camo_pattern; flag_patch
        'bottom': 'long',
        'boots': True, 'wraps': 'boots',
        'harness': True,                        # MISSING: y_harness (crossed straps stand in)
        'bands': [('upperarm', 'tint'), ('knee', 'tint')],   # MISSING: tinted_shoulders (the whole shoulder cap)
        'gear': [],
        # MISSING: ground_props (props.tow_launcher on its tripod and two missile
        # crates set on the ground beside him; equip only puts props in hands)
        # MISSING: team (a second, kneeling soldier at the launcher's sight)
        'weapon': None, 'offhand': None,
        'stance_name': 'guard',                 # MISSING: spotting (a hand raised to the brow)
        'tint': ['harness', 'belt'],
        'grime': 0.8, 'upper_bulk': 1.25,
    },
}

# MISSING FEATURES
# What the notes still need that the supported keys can't express, ranked by
# how many of these 12 units need it. Each entry above approximates it and
# marks the spot with "MISSING:".
#
# camo_pattern       6  Infantry, Marine, Paratrooper, WWII Paratrooper, Guerilla, TOW Infantry (+ Marine/TOW helmet
#                       covers): a 2-3 color mottled/woodland pattern on a cloth part (catalog 'camo' has the colors).
# hip_hold           4  Infantry, Marine, WWII Paratrooper, Paratrooper: a gun stance with both hands low at the right
#                       hip, muzzle forward (port_arms with a level gun_dir stands in; its hands sit at the chest).
# mil_backpack/chute_pack on 'back'  4  Infantry, Guerilla (mil_backpack), Paratrooper, WWII Paratrooper (a parachute
#                       pack): equip's 'back' accepts only 'quiver'/'backpack', so the settler's sack with its pot
#                       stands in; let 'back' take any prop (mil_backpack(size) exists).
# tall_boots / shoes 4  Musketeer, Settler Modern Times (knee-high boots), Musketman, Rifleman (low shoes): boots'
#                       zmax as a param.
# hats (riders.HEADGEAR)  3  hat kepi (Rifleman; shako stands in), hat cavalier (Musketeer; plumed wide brim
#                       pinned up), hat boonie (Settler Modern Times, tinted).
# hem_band           2  Musketeer (tinted coat hem), Crusader (tinted surcoat hem).
# long_coat          2  Musketeer (knee-length coat), Rifleman (frock skirts to mid-thigh).
# flag_patch         2  WWII Paratrooper, TOW Infantry: a small flag on the upper sleeve.
# modern_helmet extras 2  helmet_band (Marine, tinted) and helmet_net (WWII Paratrooper); the modern_helmet hat
#                       ignores 'tinted' too.
# plume on headgear  1  Musketman: riders' morion has no plume, so the old 'helmet': 'morion' path (worse comb and
#                       brim) is used to keep the tinted plume.
# tinted_pack        1  Infantry: the back pack in civ color (backpack(tint=True)); Guerilla wants tinted pack straps.
# gloves             1  Paratrooper: tinted hands (wrist bands stand in).
# shoulder_arms      1  Musketman: musket on the right shoulder, angled up-forward (order_arms stands in).
# musket_rest        1  Musketman: a tall forked rest planted upright in the left hand.
# present_arms       1  Musketeer: the musket vertical in front, both hands on it.
# flared_muzzle      1  Musketeer: a bell-mouthed muzzle (musket param).
# rifle_shoulder     1  Guerilla: rifle vertical against the right shoulder ('upright' holds it low in the fist;
#                       order_arms would put its pistol grip at the foot).
# haversack / cartridge_box / hip_pouch / bandolier_bottles  2  Rifleman, Musketman: small bags and boxes on belts.
# tool_belt          1  Worker Modern Times: a belt with pouches at the hip.
# hip_gear placement 1  Marine: 'hip' hangs a prop like a scabbard (+Z down); a canteen or holster (hanging along
#                       -Z) needs its own placement, e.g. a 'belt_gear' list (canteen at the back belt).
# cargo_pockets      1  Marine.
# gaiters            1  Infantry: off-white canvas gaiters (G.fur wraps stand in).
# ak                 1  Guerilla: an AK (wooden furniture, curved magazine); assault_rifle stands in.
# bandolier_rounds   1  Guerilla: cartridge loops on the crossed straps.
# bandana_knot       1  Guerilla: the knot and tails at the back of the headband.
# rolled_sleeves     1  Worker Modern Times: sleeves to the elbow, forearms bare.
# bib_overalls       1  Worker Modern Times: a cloth bib joined to the trousers, Y braces behind.
# dig_lean           1  Worker Modern Times: leaning in, blade forward and down.
# walking            1  Settler Modern Times: mid-stride.
# mail_sleeves / mail_chausses  1  Crusader (already listed for the footmen).
# surcoat_cross      1  Crusader: civ-color quarters with a white cross on the surcoat.
# heater_shield / tinted_shield_face  1  Crusader: a heater board, face civ color with a white cross.
# y_harness / tinted_shoulders  1  TOW Infantry: a Y harness; a whole tinted shoulder cap, not a band.
# ground_props       1  TOW Infantry: tow_launcher on its tripod and missile crates on the ground (no crate prop).
# team               1  TOW Infantry: a second, kneeling figure at the sight.
# spotting           1  TOW Infantry: a hand raised to the brow.
