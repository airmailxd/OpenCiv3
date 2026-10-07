"""Ancient-era foot units and civilians (the framework's first batch).

They use the 'footman' archetype (models/foot_ancient.py: the humanoid plus
gear, prop overrides, pose overrides and the muted color pass: `desat`,
`tint_shade`). The civ color goes only where the original has it.
"""

# A fortified archer takes aim: the bow raised, the string drawn, an arrow nocked.
BOW_AIM = {'hand_l': (-0.06, 0.68, 1.36), 'w_off': (-4, 6, 0), 'chest': (0, 0, -50), 'spine': (0, 0, -22),
           'head': (0, 0, 55), 'foot_l': (-0.15, 0.24, 0.08), 'foot_r': (0.16, -0.16, 0.08), 'fyaw_r': -40.0,
           'elbow_l': (-1.0, -0.2, -0.5), 'hand_r': (0.12, 0.08, 1.46), 'draw': 1.0, 'elbow_r': (1.0, -0.4, 0.3)}

UNITS = {
    'Warrior': {
        'archetype': 'footman',
        'colors': {'skin': '#c08a5a', 'hair': '#3e2a1c', 'bottom': '#ffffff', 'belt': '#4e3424',
                   'wraps': '#6a4a30', 'wraps_band': '#3a2618', 'stone': '#8a8784', 'wood': '#5e4430'},
        'tint': ['bottom', 'bracers'],
        'desat': 0.78,
        'hair': 'long', 'beard': True, 'top': 'bare', 'bottom': 'loincloth',
        'bracers': True, 'wraps': 'wraps',
        # a big stone club-axe: the warrior's whole identity
        'weapon': 'stone_axe', 'weapon_scale': 1.15,
        'build': {'build': 'brawny', 'bulk': 1.05},
        'actions': {'ATTACK1': 'attack_lunge', 'ATTACK2': 'attack_chop'},
    },
    'Spearman': {
        'archetype': 'footman',
        'colors': {'skin': '#c08c62', 'hair': '#3a2616', 'bottom': '#5e4630', 'belt': '#44301e',
                   'helmet': '#8e7650', 'crest': '#4a3626', 'shield_face': '#c8b894', 'shield_rim': '#5a4028', 'sandal': '#44301e'},
        'tint': ['top', 'shield_emblem'],
        'metal': ['helmet'],
        'desat': 0.85,
        'hair': 'short', 'helmet': 'crested', 'top': 'mantle', 'bottom': 'skirt', 'sandals': True,
        'gear': [('chest_strap', {'color': '#44301e'})],
        # a spear just over his head, an oval shield
        'weapon': 'spear', 'offhand': 'shield_oval',
        'props': {'spear': ('spear', {'length': 2.1, 'grip': 0.95})},
        'build': 'muscular',
        'actions': {'ATTACK1': 'attack_thrust', 'ATTACK2': 'attack_thrust'},
    },
    'Archer': {
        'archetype': 'footman',
        'colors': {'skin': '#c49c70', 'hair': '#33302c', 'bottom': '#5e4630',
                   'belt': '#3e2c1e', 'boots': '#4e3c2c', 'leather': '#5e4430', 'wood': '#6a4a2a'},
        'tint': ['top'],
        'metal': [],
        'desat': 0.85,
        # long dark hair and a full beard (no helmet); a leather skirt and a
        # chest strap break up the civ-colored tunic
        'hair': 'long', 'beard': True, 'helmet': None, 'top': 'tunic', 'bottom': 'skirt', 'wraps': 'boots',
        'gear': [('chest_strap', {'color': '#3e2c1e'})],
        'weapon': None, 'offhand': 'bow', 'back': 'quiver',
        'offhand_mesh': ('bold_bow', {'height': 1.45}),
        'build': 'muscular',
        # the bow held across the front, an arrow ready; fortified: drawn and aimed
        'stance': {'hand_l': (-0.18, 0.34, 1.02), 'w_off': (-62, 0, -60), 'elbow_l': (-0.6, -0.8, -0.2)},
        'overrides': {'FORTIFY': BOW_AIM},
        'actions': {'ATTACK1': 'attack_shoot'},
    },
    'Swordsman': {
        'archetype': 'footman',
        'colors': {'skin': '#c89a72', 'hair': '#3a2a1c', 'pelt': '#8a8682', 'pelt_dark': '#4a4642', 'bottom': '#5e4430',
                   'belt': '#3a281c', 'boots': '#4e3624', 'cuirass': '#a08452', 'shield_face': '#6e4e32'},
        'tint': ['top'],
        'desat': 0.85,
        # a wolf pelt: the wolf's head worn over his head, its hide down his back
        'hair': 'short', 'helmet': 'wolf', 'back': 'pelt', 'top': 'sleeved', 'cuirass': True, 'bottom': 'skirt',
        'wraps': 'greaves',
        'build': 'muscular',
        # a broad sword held out low, a round shield
        'weapon': 'sword', 'offhand': 'shield_round',
        'props': {'sword': ('sword', {'length': 0.82, 'width': 0.085, 'guard': 0.2})},
        'stance': {'hand_r': (0.42, 0.18, 0.88), 'w_main': (-128, 0, -38)},
        'actions': {'ATTACK1': 'attack_chop', 'ATTACK2': 'attack_slash'},
    },
    'Settler': {
        'archetype': 'footman',
        'colors': {'skin': '#c09470', 'hair': '#3a2a1c', 'bottom': '#d6d0c0', 'belt': '#4e3826',
                   'sandal': '#44321e', 'pack': '#76683e', 'helmet': '#a89060'},
        'tint': ['top'],
        'metal': [],
        'desat': 0.9,
        'hair': 'short', 'helmet': 'cap', 'top': 'tunic', 'bottom': 'kilt', 'sandals': True,
        'weapon': 'staff', 'back': 'backpack', 'pack_strap': True,
        'build': 'lean',
    },
    'Worker': {
        'archetype': 'footman',
        'colors': {'skin': '#a87c56', 'hair': '#2a1e14', 'helmet': '#cdbb8e', 'belt': '#44301e',
                   'sandal': '#44301e', 'bottom': '#6a5038'},
        'tint': ['top'],
        'metal': [],
        'desat': 0.85,
        'hair': 'short', 'helmet': 'headband', 'top': 'strap', 'bottom': 'skirt', 'sandals': True,
        'weapon': 'shovel',
        # chunkier tools (pick, hoe, axe, ...) so the job reads at map scale
        'tool_scale': (1.4, 1.4, 1.1), 'weapon_scale': (1.3, 1.3, 1.0),
        'build': 'lean',
        'actions': {'CAPTURE': 'work_dig'},
        # a little hunched, the shovel held across the hips, blade to the left
        'stance': {'hand_r': (0.26, 0.20, 0.86), 'hand_l': (-0.22, 0.24, 0.88), 'w_main': (-92, 0, 88),
                   'root_pos': (0, 0, -0.05), 'spine': (-8, 0, 0), 'head': (8, 0, 0),
                   'elbow_l': (-0.6, -0.8, -0.2)},
    },
}
