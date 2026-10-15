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
        'colors': {'skin': '#a07450', 'hair': '#3a2616', 'wraps': '#4a3020', 'wraps_band': '#22160c',
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
                   'shield_face': '#8a7250', 'shield_rim': '#3a3634', 'belt': '#3a2818'},
        'props': {'spear': ('spear', {'grip': 1.2, 'length': 2.6}),
                  'shield_oval': ('shield_oval', {'band': 0.13, 'w': 0.82, 'h': 0.82})},
        'offhand_mesh': ('shield_oval', {'band': 0.13, 'w': 0.82, 'h': 0.82}),
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
        'colors': {'skin': '#94704f', 'hair': '#1e1610', 'helmet': '#2a2018', 'boots': '#3a2616',
                   'leather': '#4e3826', 'wood': '#3e2a1a'},
        'top': 'strap', 'bottom': 'loincloth', 'loin_length': (0.6, 0.42), 'torn': False,
        'tint': ['top', 'bottom'],
        'hair': 'topknot', 'hair_asset': 'ponytail01', 'helmet': 'headband',
        'wraps': 'boots', 'beard': False,
        'offhand_mesh': ('bold_bow', {'height': 1.45, 'bend': 0.2, 'recurve': 0.12}),
        'grime': 0.7, 'upper_bulk': 1.12,
    },
    'Swordsman': {
        # the original: an iron helmet with a brow ridge (reads horned), brown
        # leather armour over a skirt of leather strips, grey greaves; civ
        # color on the upper arms and the small round shield's rim and boss
        'macro': {'muscle': 1.0, 'weight': 0.8, 'height': 0.75, 'proportions': 0.7},
        'colors': {'skin': '#94704f', 'helmet': '#5c5c60', 'cuirass': '#5e4028', 'bottom': '#4e3622',
                   'greaves': '#6a6a6e', 'horn': '#b8a888', 'belt': '#2e2016', 'shield_face': '#5e4430'},
        'helmet': 'horned', 'metal': ['helmet'], 'cuirass': True, 'top': 'tunic',
        'props': {'sword': ('sword', {'guard': 0.18, 'length': 0.62, 'width': 0.09})},
        'bottom': 'loincloth', 'bottom_material': 'leather', 'loin_length': (0.36, 0.34),
        'back': None, 'wraps': 'greaves', 'beard': True,
        'tint': ['top', 'shield_emblem'],
        'grime': 0.7, 'upper_bulk': 1.25,
    },
    # ---- riders (the PC session's units: mounted.py seats them on their horses)
    'Horseman': {
        # Civ3's Horseman: civ-colored tunic, covered arms and legs, boots, an iron cap
        'top': 'sleeved',
        'bottom': 'long',
        'wraps': 'boots',
        'neck': 'high', 'collar': 'mail',   # covered to the neck, a ring-mail collar as Civ3's grey shoulders
        'colors': {'skin': '#8e6448', 'bottom': '#4e4a44', 'boots': '#3a2a1c', 'belt': '#3a2818', 'helmet': '#8a8c90',
                   'steel': '#c8ccd0', 'mail': '#6e7074', 'wood': '#4a3020'},
        # sitting tall in the saddle, chest out (the riding stance leans a little forward)
        'pose_extra': [('spine_01', (0, 0.12, 1)), ('spine_02', (0, 0.16, 1)), ('spine_03', (0, 0.14, 1)),
                       ('neck_01', (0, 0.04, 1)), ('head', (0, -0.04, 1))],
        'upper_bulk': 1.35,
        'macro': {'muscle': 1.0, 'weight': 0.85, 'proportions': 0.7},
        'grime': 0.6,
    },
}


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
