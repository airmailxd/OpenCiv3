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
                   'wood': '#6a4a2e', 'stone': '#b8a888', 'leather': '#4a3020', 'belt': '#3a2618'},
        'upper_bulk': 1.2,
        'hair_asset': 'long01',
        'mane': 0.26,
        'pelt_cape': ('#8a7050', '#5a4430'),
        'grime': 0.8,
        'props': {'stone_axe': ('war_club', {'length': 0.62, 'knob': 0.075, 'scale': 1.35})},
    },
    'Spearman': {
        'macro': {'muscle': 1.0, 'weight': 0.8, 'height': 0.75, 'proportions': 0.7},
        'colors': {'skin': '#94704f', 'helmet': '#5a4c2c', 'crest': '#3a2a1c', 'bottom': '#4e3a26',
                   'shield_face': '#b09060', 'shield_rim': '#3a3634', 'belt': '#3a2818'},
        'props': {'spear': ('spear', {'grip': 1.2, 'length': 2.6}),
                  'shield_oval': ('shield_oval', {'band': 0.13, 'w': 0.8, 'h': 0.8})},
        'offhand_mesh': ('shield_oval', {'band': 0.13, 'w': 0.8, 'h': 0.8}),
        'gear': [('chest_strap', {'color': '#2a1c12', 'width': 0.05})], 'shield_at': 'left_hip',
        'grime': 0.7,
        'beard': True,
        'tint': ['top', 'shield_emblem', 'crest'],
        'metal': ['helmet'],
        'upper_bulk': 1.25,
    },
    'Archer': {
        # the original: lean and bare-chested, a sash over one shoulder and a
        # long apron (both civ color), the bow held low across the hips
        'macro': {'muscle': 0.9, 'weight': 0.6, 'height': 0.75, 'proportions': 0.75},
        'colors': {'skin': '#8a684a', 'hair': '#140e0a', 'helmet': '#7a5434', 'boots': '#3a2616',
                   'leather': '#4e3826', 'wood': '#3e2a1a', 'string': '#8a7a5a'},
        'top': 'strap', 'bottom': 'loincloth', 'loin_length': (0.6, 0.42), 'torn': False,
        'tint': ['top', 'bottom'],
        'hair': 'topknot', 'hair_asset': 'ponytail01', 'helmet': 'headband',
        'wraps': 'boots', 'beard': False, 'upper_bulk': 1.25, 'gear': [],
        'stance_name': 'bow_low',
        'weapon': 'arrow', 'props': {'arrow': ('arrow', {'length': 0.85})},   # an arrow ready in the right hand
        'offhand_mesh': ('bold_bow', {'height': 1.45, 'bend': 0.2, 'recurve': 0.12}),
        'grime': 0.6,
    },
    'Swordsman': {
        # the original: an iron helmet with a brow ridge (reads horned), brown
        # leather armour over a skirt of leather strips, grey greaves; civ
        # color on the upper arms and the small round shield's rim and boss
        'macro': {'muscle': 1.0, 'weight': 0.8, 'height': 0.75, 'proportions': 0.7},
        'colors': {'skin': '#8a684a', 'helmet': '#5c5c60', 'cuirass': '#5e4028', 'bottom': '#4e3622',
                   'steel': '#8a8c90', 'bracers': '#4e3622',
                   'greaves': '#6a6a6e', 'horn': '#b8a888', 'belt': '#2e2016', 'shield_face': '#5e4430', 'steel': '#6e7074'},
        'helmet': 'horned', 'metal': ['helmet'], 'cuirass': True, 'top': None, 'pauldrons': True,
        'neck': 'high', 'boots': True, 'stance_name': 'guard', 'shield_at': 'left_hip',
        'shield_yaw': -48.0, 'shield_drop': 0.38,
        'hem_bands': [('cuirass', '#2a1c12', 0.03)],      # a dark edge binding on the cuirass 'offhand_mesh': ('shield_round', {'tint': 'both'}),
        'props': {'sword': ('sword', {'guard': 0.18, 'length': 0.62, 'width': 0.09})},
        'bottom': 'loincloth', 'bottom_material': 'leather', 'loin_length': (0.5, 0.46), 'leather_bracers': True,
        'back': None, 'wraps': 'greaves', 'beard': True, 'colors_extra': None,
        'tint': ['top', 'shield_emblem'],
        'grime': 0.7, 'upper_bulk': 1.25,
    },
    # ---- riders (the PC session's units: mounted.py seats them on their horses)
    # chariot crews (chariots.py): keys '<unit>:crew<i>'
    'chariot:crew0': {'weapon': 'spear', 'props': {'spear': ('spear', {'length': 2.8, 'grip': 1.0})}},
    # Civ3's Rider: the civ-colored coat with gold belt fittings and gold hems
    'Rider': {'colors': {'belt': '#c4a462'}, 'hem_bands': [('top', 'gold', 0.03)]},
    # Civ3's Ansar: a gilded lance, a big round shield with a gold rim and boss, a sword at the hip
    'Ansar Warrior': {'props': {'cav_spear': ('cav_spear', {'length': 2.3})},
                      'colors': {'wood': '#8a7048', 'shield_rim': '#d0a840', 'bronze': '#d0a840', 'shield_face': '#b89448'},
                      'tint': ['hat', 'belt'],
                      'offhand_mesh': ('shield_round', {'r': 0.32, 'tint': 'none'}),
                      'hip': ('sheathed_sword', {'length': 0.78})},
    'Ancient Cavalry': {'top': 'sleeved', 'tint': ['top', 'crest', 'shield_face', 'shield_emblem']},
    'Knight': {'top': 'sleeved', 'cuirass': True, 'metal': ['helmet', 'cuirass'],
               'colors': {'cuirass': '#9a9ca2', 'mail': '#7a7c80', 'shield_pale': '#f2f0ea', 'shield_rim': '#c4a462'},
               # the original: a civ heater shield with a white charge, a horned great helm, mail sleeves
               'offhand': 'shield_heater', 'offhand_mesh': ('shield_heater', {'w': 0.56, 'h': 0.86}),
               'weapon': 'lance', 'props': {'lance': ('lance', {'length': 2.1})},   # couched forward, charging (the original); short so the unit keeps its size
               'headgear_params': {'crest': 'horns'}, 'mail_parts': ['arms']},
    # Civ3's Cossack: a tan coat; only the papakha and belt are civ-colored
    'Cossack': {'tint': ['hat', 'belt'], 'headgear_scale': [0.95, 0.95, 0.9]},
    'Keshik': {'tint': ['top', 'hat']},
    # Civ3's Conquistador: a black morion and breastplate, a civ rodela with a gold boss and rim
    'Conquistador': {'tint': ['top'], 'metal': ['cuirass', 'helmet'],
                     'colors': {'cuirass': '#4a4c52', 'helmet': '#8a8e96', 'shield_rim': '#c4a462', 'bronze': '#c4a462'},   # a polished steel morion
                     'offhand': 'shield_round', 'offhand_mesh': ('shield_round', {'r': 0.3, 'face_tint': True, 'boss_key': 'gold'}),
                     'props': {'lance': ('lance', {'length': 2.6})}},
    'Horseman': {
        # Civ3's Horseman: civ-colored tunic, covered arms and legs, boots, an iron cap
        'top': 'tunic',      # short sleeves, so the grey mail sleeves show (as the original)
        'bottom': 'long',
        'wraps': 'boots',
        # as the original: a long slim pale shaft and a dark leaf head that stays broad from any side
        'props': {'cav_spear': ('cav_spear', {'length': 2.3, 'grip': 0.9, 'head': 2.0, 'shaft': 0.03, 'flat': 0.6})},
        'neck': 'high', 'collar': 'mail',   # covered to the neck, a ring-mail collar as Civ3's grey shoulders
        # grey mail down the legs and a pale ash spear shaft, as the original
        'mail_parts': ['arms', 'legs'],
        'colors': {'skin': '#8e6448', 'bottom': '#4e4a44', 'boots': '#3a2a1c', 'belt': '#3a2818', 'helmet': '#8a8c90',
                   'steel': '#5c5e62', 'mail': '#8a8c90', 'wood': '#c4a46a'},
        # sitting tall in the saddle, chest out (the riding stance leans a little forward)
        'pose_extra': [('spine_01', (0, 0.12, 1)), ('spine_02', (0, 0.16, 1)), ('spine_03', (0, 0.14, 1)),
                       ('neck_01', (0, 0.04, 1)), ('head', (0, -0.04, 1))],
        'upper_bulk': 1.35,
        'macro': {'muscle': 1.0, 'weight': 0.85, 'proportions': 0.7},
        'grime': 0.6,
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
    },
    'Worker': {
        # the original: slim and wiry, a straw headband, civ color on a sash
        # over one shoulder and an apron, a shovel held diagonally, blade down
        'macro': {'muscle': 0.85, 'weight': 0.5, 'height': 0.7, 'proportions': 0.7},
        'colors': {'skin': '#8e6a48', 'helmet': '#c8b688', 'hair': '#241a10', 'wraps': '#3e2a1c',
                   'wraps_band': '#22160c', 'wood': '#6a4a2e', 'steel': '#7a7872'},
        'helmet': 'cap', 'cap_brim': True, 'two_hands': True, 'tool_dir': (-0.4, -0.62, 0.68), 'tool_face': (0.75, -0.65, 0.0),
        'pose_extra': [('spine_02', (0, -0.14, 1)), ('head', (0, 0.2, 1))],
        'top': 'strap', 'bottom': 'loincloth', 'loin_length': (0.45, 0.4), 'torn': False,
        'tint': ['top', 'bottom'], 'wraps': 'wraps', 'sandals': False,
        'grime': 0.85, 'upper_bulk': 1.1,
    },
}


# Entries for further units live in their own modules, merged here.
for _module in ('remaster_footmen', 'remaster_royals', 'remaster_soldiers'):
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
