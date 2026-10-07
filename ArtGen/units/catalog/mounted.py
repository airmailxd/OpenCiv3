"""Mounted units: horsemen, chariots and war elephants (models/mounted.py)."""
import numpy as np


def _anchor(unit):
    """The original's ground anchor for a four-legged unit: the hooves are
    spread along the body, so the anchor is taken where the unit stands
    sideways (rows E and W: the lowest point is under the middle of the
    body) and centered on the legs as seen from the front and back (rows S
    and N)."""
    import flic as F
    fl = F.load_unit_flic(unit, F.unit_flics(unit)['DEFAULT'])
    ys, xs = [], []
    for d in range(fl.n_anims):
        idx = fl.frames[d, 0]
        m = ~F.shadow_mask(idx) & (idx != 255)
        yy, xx = np.nonzero(m)
        if len(yy) == 0:
            continue
        by = yy.max()
        if d in (3, 7):
            ys.append(by + fl.offset_top)
        if d in (1, 5):
            top = yy.min()
            sel = yy >= by - (by - top) * 0.22
            xs.append((xx[sel].min() + xx[sel].max()) / 2 + fl.offset_left)
    return float(np.mean(xs)), float(np.mean(ys)) - 2.0


# Colors are sampled from the originals (sample_colors.py) and kept a little
# greyer: natural horse coats, leather, steel and cloth. The civ color (the
# 'tint' lists) only goes where the original has it, on about as much area.
SKIN = '#c49a72'
GOLD = '#c4a462'        # brass/gold trim, muted like the originals'

UNITS = {
    # The ancient spearman on a bay: round helmet, mail, civ-colored tunic,
    # a spear held upright, a small saddle cloth.
    'Horseman': {
        'archetype': 'horse_rider',
        'rider_scale': 0.91,
        'horse': {'coat': '#6a5446', 'mane': '#241c16', 'points': '#30261e', 'muzzle': '#3a2e26',
                  'blanket': 'saddle_cloth', 'cloth_span': (-0.30, 0.18), 'cloth_drop': 0.24},
        'rider': {
            'prop_scale': 1.25,
            'colors': {'skin': SKIN, 'hair': '#3a2a1c', 'helmet': '#9a9ca2', 'top': '#ffffff', 'belt': '#4a3424',
                       'boots': '#4a3a2c', 'pants': '#5e5a54', 'mail': '#7a7c80'},
            'tint': ['top'],
            'build': 'muscular', 'hair': 'short', 'helmet': 'cap', 'top': 'tunic', 'bottom': 'none', 'wraps': 'boots', 'pants': 'pants',
            'sleeves': 'mail', 'weapon': 'cav_spear', 'offhand': None,
        },
        'attack': 'thrust',
    },
    # A bare-chested plains rider on a painted pony: the tall feather bonnet,
    # a bow, civ-colored hand prints on the horse.
    'Mounted Warrior': {
        'archetype': 'horse_rider',
        'rider_scale': 0.93,
        'horse': {'coat': '#9a8266', 'mane': '#4a3c2e', 'points': '#d4ccbc', 'hoof': '#8a7a66', 'muzzle': '#6a5a4c',
                  'blanket': None, 'saddle_visible': False, 'girth': False, 'breastplate': False, 'reins': 'bridle',
                  'patches': [(-0.92, -0.25, -40, 75)], 'coat2': '#d0cabe',
                  'marks': [(-0.62, -0.42, 10, 45), (0.30, 0.45, -20, 15)]},
        'rider': {
            'prop_scale': 1.25,
            'colors': {'skin': '#b48464', 'hair': '#1e1612', 'top': '#ffffff', 'bottom': '#ffffff', 'belt': '#5a3e2a',
                       'pants': '#8a6a4a', 'leather': '#6a4e36', 'boots': '#7a5e42', 'feathers': '#eeeae0'},
            'tint': ['top', 'bracers'],
            'build': 'brawny', 'hair': 'long', 'top': 'strap', 'bottom': 'none', 'pants': 'pants', 'bracers': True, 'wraps': 'boots',
            'headgear': 'feather_bonnet', 'headgear_scale': 1.6, 'back': 'quiver', 'weapon': None, 'offhand': 'bow',
        },
        'attack': 'shoot_bow',
    },
    # A Greek/Roman horseman: crested helmet, civ-colored mantle, a big round
    # civ-colored shield and a javelin, on a dun horse.
    'Ancient Cavalry': {
        'archetype': 'horse_rider',
        'rider_scale': 0.93,
        'horse': {'coat': '#7a6c5a', 'mane': '#2a2420', 'points': '#3a332c', 'blanket': 'saddle_cloth', 'cloth_drop': 0.22,
                  'blanket_color': '#7e6650', 'blanket_tint': False, 'reins': 'bridle'},
        'rider': {
            'prop_scale': 1.25,
            'colors': {'skin': SKIN, 'hair': '#3a2a1c', 'helmet': '#a89668', 'crest': '#ffffff', 'top': '#ffffff',
                       'cuirass': '#a0a0a0', 'belt': '#4a3424', 'sandal': '#4a3424', 'shield_face': '#ffffff',
                       'shield_rim': '#a89668'},
            'tint': ['top', 'crest', 'shield_face'],
            'build': 'muscular', 'hair': 'short', 'helmet': 'crested', 'top': 'mantle', 'cuirass': True, 'bottom': 'none', 'sandals': True,
            'weapon': 'javelin', 'offhand': 'shield_round',
        },
        'attack': 'javelin',
        'attack_rear': True,
        'victory_rear': False,
    },
    # The Chinese rider: a white horse under a civ-colored saddle cloth, a
    # conical hat, a sabre.
    'Rider': {
        'archetype': 'horse_rider',
        'rider_scale': 0.92,
        'horse': {'coat': '#d0d0cc', 'mane': '#9a9a98', 'points': '#a8a8a4', 'hoof': '#4a4644', 'muzzle': '#6a6664',
                  'dapple': '#a8a8a6', 'blanket': 'saddle_cloth', 'cloth_drop': 0.26, 'trim': GOLD,
                  'tack': '#8a6a3a'},
        'rider': {
            'prop_scale': 1.25,
            'colors': {'skin': '#d0a888', 'hair': '#1e1612', 'top': '#ffffff', 'hat': '#ffffff', 'belt': GOLD,
                       'boots': '#2a2420', 'pants': '#3a3a40'},
            'tint': ['top', 'hat'],
            'hair': 'short', 'top': 'long_sleeved', 'bottom': 'none', 'pants': 'pants', 'wraps': 'boots',
            'headgear': 'hat_cone', 'headgear_scale': 1.15, 'weapon': 'sabre', 'offhand': None,
        },
        'attack': 'slash',
        'victory_rear': False,
    },
    # The armored knight: steel barding over the whole horse with civ-colored
    # skirts (gold hems) over it, a great helm, a surcoat and a shield.
    'Knight': {
        'archetype': 'horse_rider',
        'rider_scale': 0.99,
        'horse': {'coat': '#3e3632', 'mane': '#1e1612', 'points': '#34302e', 'blanket': 'barding', 'barding_steel': '#9a9ea6',
                  'cloth_drop': 0.40, 'caparison_over': True, 'over_span': (-0.62, 0.36), 'over_drop': 0.38, 'over_gap': 0.06,
                  'trim': GOLD, 'chanfron': True, 'neck_cover': 'steel', 'saddle': '#4a3424'},
        'rider': {
            'prop_scale': 1.25,
            'colors': {'skin': SKIN, 'helmet': '#9a9ca2', 'top': '#ffffff', 'belt': '#5a4028', 'boots': '#8a8c92',
                       'pants': '#7a7c80', 'mail': '#7a7c80', 'shield_face': '#dcd8ce', 'shield_emblem': '#ffffff',
                       'shield_rim': GOLD},
            'tint': ['top', 'shield_emblem'],
            'metal': ['helmet', 'boots'],
            'hair': 'none', 'headgear': 'great_helm', 'top': 'tunic', 'bottom': 'none', 'pants': 'pants', 'sleeves': 'mail',
            'wraps': 'boots', 'weapon': 'long_sword', 'offhand': 'shield_oval',
        },
        'attack': 'slash',
        'victory_rear': False,
    },
    # The Mongol horse archer on a chestnut: pointed fur-brimmed hat, a tan
    # coat over a civ-colored tunic, a bow and spears on the back.
    'Keshik': {
        'archetype': 'horse_rider',
        'rider_scale': 0.97,
        'horse': {'coat': '#604a3e', 'mane': '#1a1410', 'points': '#2a1e16', 'blanket': 'saddle_cloth', 'cloth_drop': 0.25,
                  'blanket_color': '#bcae8e', 'blanket_tint': False, 'bridle': '#4a3424', 'bridle_tint': False},
        'rider': {
            'prop_scale': 1.25,
            'colors': {'skin': '#c09a74', 'hair': '#1e1612', 'top': '#ffffff', 'hat': '#a8906c', 'fur': '#5a4430',
                       'coat': '#bcae8e', 'cuirass': '#6a4e3a', 'belt': '#6a3a2a', 'boots': '#3a2a1e', 'pants': '#a8987a', 'leather': '#5a3e2a'},
            'tint': ['top'],
            'hair': 'short', 'top': 'long_sleeved', 'cuirass': True, 'bottom': 'none', 'pants': 'pants', 'wraps': 'boots', 'sleeves': 'coat',
            'coat_skirt': 'coat', 'headgear': 'fur_hat', 'headgear_scale': 1.15, 'back': 'quiver', 'back_gear': 'spears',
            'weapon': None, 'offhand': 'bow',
        },
        'attack': 'shoot_bow',
    },
    # The Ottoman sipahi: a civ-colored caparison strewn with white dots, a
    # steel chanfron, a spiked helmet with a white plume, a sabre and shield.
    'Sipahi': {
        'archetype': 'horse_rider',
        'rider_scale': 0.97,
        'horse': {'coat': '#4e4640', 'mane': '#e0dcd4', 'points': '#463a30', 'blanket': 'dots', 'cloth_drop': 0.30,
                  'blanket2': '#eeece6', 'neck_cover': 'steel', 'chanfron': True, 'barding_steel': '#c8c8c4',
                  'plume': True, 'plume_tint': False, 'plume_color': '#eeece6', 'trim': GOLD},
        'rider': {
            'prop_scale': 1.25,
            'colors': {'skin': SKIN, 'hair': '#1e1612', 'helmet': '#a8aab0', 'top': '#7c7e84', 'cuirass': '#a0a2a8',
                       'belt': '#5a4434', 'boots': '#3a2e26', 'pants': '#6a6460', 'shield_face': '#ffffff',
                       'plume': '#eeece6'},
            'tint': ['shield_face'],
            'hair': 'none', 'headgear': 'spiked_helm', 'headgear_scale': 1.15, 'top': 'long_sleeved', 'cuirass': True,
            'bottom': 'none', 'pants': 'pants', 'wraps': 'boots', 'weapon': 'sabre', 'offhand': 'shield_round',
        },
        'attack': 'slash',
    },
    # The conquistador on a dapple gray, his war dog beside him: morion,
    # breastplate, civ-colored sleeves, a lance and a round shield.
    'Conquistador': {
        'archetype': 'horse_rider',
        'rider_scale': 1.03,
        'horse': {'coat': '#7c7c80', 'mane': '#2a2826', 'points': '#3a3836', 'dapple': '#505054', 'dapple_n': 90,
                  'blanket': 'saddle_cloth', 'cloth_drop': 0.26, 'blanket_color': '#b49c74', 'blanket_tint': False,
                  'trim': '#8a6a40', 'saddle': '#6a4a2e', 'bridle': '#4a3424', 'bridle_tint': False},
        'rider': {
            'prop_scale': 1.0,
            'colors': {'skin': '#d0a488', 'hair': '#2a1e14', 'helmet': '#a0a2a8', 'cuirass': '#7a7c82', 'top': '#ffffff',
                       'belt': '#5a3e2a', 'boots': '#5a4030', 'pants': '#6a5444', 'shield_face': '#8a8c90',
                       'shield_rim': GOLD},
            'tint': ['top'],
            'hair': 'short', 'beard': True, 'headgear': 'morion', 'headgear_scale': 1.2, 'top': 'sleeved', 'cuirass': True,
            'bottom': 'none', 'pants': 'pants', 'wraps': 'boots', 'weapon': 'lance', 'offhand': 'shield_round',
            'stance': {'hand_r': (0.30, 0.26, 1.02), 'w_main': (36, 0, 0)},
        },
        'attack': 'thrust',
        'victory_rear': False,
        'escort': 'dog',
        'dog': {'coat': '#9a7450', 'saddle': '#2a2420', 'place': (0.95, 0.40, 0.0)},
    },
    # The Cossack on a dark gray: tall papakha with a civ-colored top, a long
    # tan coat with a civ-colored sash, a beard, a rifle.
    'Cossack': {
        'archetype': 'horse_rider',
        'rider_scale': 1.06,
        'horse': {'coat': '#4c4c50', 'mane': '#1a1a1c', 'points': '#2a2a2c', 'blanket': 'saddle_cloth', 'cloth_drop': 0.32},
        'rider': {
            'prop_scale': 1.25,
            'colors': {'skin': SKIN, 'hair': '#2a1e14', 'top': '#b4a080', 'coat': '#b4a080', 'hat': '#ffffff',
                       'fur': '#3a2e26', 'belt': '#ffffff', 'boots': '#2a2420', 'pants': '#3a3844'},
            'tint': ['hat', 'belt'],
            'hair': 'short', 'beard': True, 'headgear': 'papakha', 'headgear_scale': (1.0, 1.0, 1.3), 'top': 'long_sleeved',
            'bottom': 'none', 'pants': 'pants', 'wraps': 'boots', 'coat_skirt': 'coat', 'sash': 'belt', 'weapon': 'rifle', 'offhand': None,
            'stance': {'hand_r': (0.22, 0.30, 1.04), 'w_main': (-62, 0, 58), 'hand_l': (-0.16, 0.42, 1.06)},
        },
        'attack': 'shoot_gun',
        'victory_rear': False,
    },
    # The hussar: a very tall civ-colored shako with a plume, a civ-colored
    # dolman, dark breeches and civ-colored reins, a sabre, on a dark bay.
    'Hussar': {
        'archetype': 'horse_rider',
        'rider_scale': 0.93,
        'horse': {'coat': '#56483c', 'mane': '#1e1a16', 'points': '#2a2420', 'blanket': 'pelt', 'blanket_tint': False,
                  'blanket_color': '#3a2e26', 'cloth_drop': 0.30, 'reins': 'bridle'},
        'rider': {
            'prop_scale': 1.25,
            'colors': {'skin': SKIN, 'hair': '#2a1e14', 'top': '#ffffff', 'hat': '#ffffff', 'belt': GOLD,
                       'boots': '#1e1a18', 'pants': '#3a3632', 'plume': '#e8e4da'},
            'tint': ['top', 'hat'],
            'hair': 'short', 'headgear': 'shako', 'headgear_scale': (1.0, 1.0, 1.05), 'top': 'long_sleeved', 'bottom': 'none',
            'pants': 'pants', 'wraps': 'boots', 'sash': 'belt', 'weapon': 'sabre', 'offhand': None,
        },
        'attack': 'slash',
    },
    # 19th-century cavalry: a brass helmet with a civ-colored crest, a
    # civ-colored coat, gray breeches and black boots, a carbine, on a buckskin.
    'Cavalry': {
        'archetype': 'horse_rider',
        'rider_scale': 0.91,
        'horse': {'coat': '#9c805c', 'mane': '#3a2e22', 'points': '#4a3a2a', 'blanket': 'saddle_cloth', 'cloth_drop': 0.20,
                  'cloth_span': (-0.30, 0.14), 'blanket_color': '#3a3a42', 'blanket_tint': False, 'trim': '#d8d4ca'},
        'rider': {
            'prop_scale': 1.25,
            'colors': {'skin': SKIN, 'hair': '#2a1e14', 'top': '#ffffff', 'helmet': '#8a7448', 'crest': '#ffffff',
                       'belt': '#e0dcd2', 'boots': '#1e1a18', 'pants': '#6a6a6e'},
            'tint': ['top', 'crest'],
            'hair': 'short', 'headgear': 'dragoon_helm', 'headgear_scale': (1.1, 1.15, 1.35), 'top': 'long_sleeved', 'bottom': 'none', 'pants': 'pants',
            'wraps': 'boots', 'sash': 'belt', 'weapon': 'rifle', 'offhand': None,
            # the carbine held across the saddle, as in the original
            'stance': {'hand_r': (0.22, 0.30, 1.04), 'w_main': (-62, 0, 58), 'hand_l': (-0.16, 0.42, 1.06)},
        },
        'attack': 'shoot_gun',
        'victory_rear': False,
    },
    # The Arab ansar on a black horse: white robes, a civ-colored turban and
    # reins, a round gold-rimmed shield, a spear.
    'Ansar Warrior': {
        'archetype': 'horse_rider',
        'rider_scale': 0.97,
        'horse': {'coat': '#2e2e32', 'mane': '#121214', 'points': '#1c1c1e', 'muzzle': '#1a1a1c', 'blanket': 'saddle_cloth',
                  'cloth_drop': 0.24, 'blanket_color': '#d8d2c4', 'blanket_tint': False, 'reins': 'bridle', 'tack': '#3a2a20'},
        'rider': {
            'prop_scale': 1.25,
            'colors': {'skin': '#a87e5c', 'hair': '#1e1612', 'top': '#e8e4dc', 'robe': '#e8e4dc', 'hat': '#ffffff',
                       'belt': '#ffffff', 'boots': '#4a3a2c', 'pants': '#e0dcd4', 'shield_face': '#ffffff', 'shield_rim': GOLD},
            'tint': ['hat', 'belt', 'shield_face'],
            'build': 'muscular', 'hair': 'short', 'beard': True, 'headgear': 'head_wrap', 'headgear_scale': 1.15, 'top': 'long_sleeved',
            'bottom': 'none', 'pants': 'pants', 'wraps': 'boots', 'coat_skirt': 'robe', 'sash': 'belt',
            'weapon': 'cav_spear', 'offhand': 'shield_round',
        },
        'attack': 'thrust',
        'victory_rear': False,
    },
    # ---------------------------------------------------------------- chariots
    # Egyptian: two horses in cream blankets with civ-colored neck cloths and
    # feather fans, a civ-colored car with gold rails, an archer in a khepresh.
    'War Chariot': {
        'archetype': 'chariot',
        'horses': 2,
        'horse': {'coat': '#8a7a64', 'mane': '#3a2e22', 'points': '#5a4c3e', 'blanket': 'saddle_cloth', 'cloth_span': (-0.30, 0.40),
                  'cloth_drop': 0.20, 'blanket_color': '#d0c4a4', 'blanket_tint': False, 'trim': GOLD, 'saddle_visible': False,
                  'girth': False, 'neck_cover': 'cloth', 'plume': 'fan', 'tack': GOLD, 'bridle': GOLD, 'bridle_tint': False},
        'car': {'kind': 'light', 'trim': GOLD, 'spokes': 6, 'wall_h': 0.28, 'wheel_r': 0.52},
        'axle_y': -1.02, 'crew_scale': 0.74,
        'crew': [{
            'pos': (0.0, -0.08),
            'colors': {'skin': '#a87a54', 'hair': '#1e1612', 'hat': '#ffffff', 'bottom': '#ece8dc', 'belt': GOLD,
                       'sandal': '#6a4e34', 'bracers': GOLD},
            'tint': ['hat'],
            'build': 'muscular', 'hair': 'short', 'headgear': 'khepresh', 'top': 'bare', 'bottom': 'kilt', 'sandals': True, 'bracers': True,
            'weapon': None, 'offhand': 'bow', 'back': 'quiver',
            'actions': {'ATTACK1': 'attack_shoot', 'RUN': 'idle'},
        }],
    },
    # Bronze-age chariot: one bay horse, a wooden car with gold rails, a
    # glaive-armed charioteer in a civ-colored tunic and cap.
    'chariot': {
        'archetype': 'chariot',
        'horses': 1,
        'horse': {'coat': '#6e5442', 'mane': '#241c16', 'points': '#3a2c20', 'blanket': 'saddle_cloth', 'cloth_span': (0.26, 0.44),
                  'cloth_drop': 0.08, 'trim': GOLD, 'saddle_visible': False, 'girth': False},
        'car': {'kind': 'light', 'body': '#8a6a48', 'body_tint': False, 'trim': GOLD, 'rim': '#8a8a86', 'spoke': '#8a8a86',
                'spokes': 8, 'wall_h': 0.50},
        'axle_y': -0.98, 'crew_scale': 0.74,
        'crew': [{
            'pos': (0.0, -0.08),
            'colors': {'skin': SKIN, 'hair': '#2a1e14', 'helmet': '#a89668', 'top': '#ffffff', 'bottom': '#e4dccc',
                       'belt': GOLD, 'sandal': '#5a3e28', 'boots': '#5a3e28'},
            'tint': ['top'],
            'metal': ['helmet'],
            'build': 'muscular', 'hair': 'short', 'beard': True, 'helmet': 'cap', 'top': 'sleeved', 'bottom': 'skirt', 'wraps': 'boots',
            'weapon': 'glaive', 'offhand': None, 'driver': False,
            'stance': {'hand_r': (0.34, 0.14, 0.98), 'w_main': (-4, 0, 0), 'elbow_r': (0.6, -0.8, -0.3),
                       'hand_l': (-0.16, 0.42, 1.02)},
            'actions': {'ATTACK1': 'attack_thrust', 'RUN': 'idle'},
        }],
    },
    'Three Man Chariot': {
        'archetype': 'chariot',
        'horses': 2,
        'horse': {'coat': '#b4b4b0', 'mane': '#5a5a58', 'points': '#a8a8a4', 'blanket': 'barding', 'barding_steel': '#a0a4aa',
                  'cloth_drop': 0.42, 'neck_cover': 'steel', 'plume': True, 'trim': '#ffffff', 'trim_tint': True, 'saddle_visible': False},
        'car': {'kind': 'heavy', 'wood': '#7a5638', 'trim': GOLD, 'rim': '#7a5638', 'spoke': '#9a7650', 'rim_metal': False,
                'spokes': 8, 'track': 0.70, 'wall_h': 0.62},
        'axle_y': -1.26, 'crew_scale': 0.74,
        'crew': [
            {'pos': (0.0, 0.14), 'driver': True,
             'colors': {'skin': SKIN, 'hair': '#2a1e14', 'helmet': '#ffffff', 'top': '#ffffff', 'bottom': '#d4ccbc',
                        'belt': '#5a3e28', 'sandal': '#5a3e28'},
             'tint': ['top', 'helmet'],
             'hair': 'short', 'helmet': 'crested', 'top': 'sleeved', 'bottom': 'skirt', 'sandals': True,
             'weapon': None, 'offhand': None,
             'stance': {'hand_r': (0.14, 0.40, 1.08), 'hand_l': (-0.14, 0.40, 1.08), 'elbow_r': (0.6, -0.8, -0.3),
                        'elbow_l': (-0.6, -0.8, -0.3)},
             'actions': {'ATTACK1': 'idle', 'RUN': 'idle', 'VICTORY': 'idle', 'FIDGET': 'idle'}},
            {'pos': (0.30, -0.18),
             'colors': {'skin': SKIN, 'hair': '#2a1e14', 'helmet': '#9a9ca2', 'top': '#ffffff', 'bottom': '#d4ccbc',
                        'belt': '#5a3e28', 'sandal': '#5a3e28'},
             'tint': ['top'],
             'hair': 'short', 'beard': True, 'helmet': 'cap', 'top': 'sleeved', 'bottom': 'skirt', 'sandals': True,
             'weapon': 'spear', 'offhand': None,
             'actions': {'ATTACK1': 'attack_thrust', 'RUN': 'idle'}},
            {'pos': (-0.30, -0.18),
             'colors': {'skin': SKIN, 'hair': '#2a1e14', 'helmet': '#9a9ca2', 'top': '#ffffff', 'bottom': '#d4ccbc',
                        'belt': '#5a3e28', 'sandal': '#5a3e28', 'shield_face': '#bca478'},
             'tint': ['top', 'shield_emblem'],
             'hair': 'short', 'helmet': 'cap', 'top': 'sleeved', 'bottom': 'skirt', 'sandals': True,
             'weapon': 'sword', 'offhand': 'shield_round',
             'actions': {'ATTACK1': 'attack_slash', 'RUN': 'idle'}},
        ],
    },
    # ---------------------------------------------------------------- the Crusader (on foot in Civ3's art)
    # A white surcoat with a big civ-colored cross over mail, a kite shield
    # with the cross, a sword.
    'Crusader': {
        'archetype': 'foot_knight',
        'colors': {'skin': SKIN, 'hair': '#3a2a1c', 'helmet': '#9a9ca2', 'top': '#e4e0d6', 'bottom': '#e4e0d6',
                   'belt': '#5a3e28', 'boots': '#8a8c92', 'mail': '#8a8c92', 'shield_face': '#e8e4da',
                   'shield_emblem': '#ffffff', 'shield_rim': '#8a6e48'},
        'tint': ['shield_emblem', 'cross'],
        'metal': ['helmet', 'boots'],
        'hair': 'short', 'helmet': 'cap', 'top': 'tunic', 'bottom': 'skirt', 'wraps': 'greaves', 'sleeves': 'mail',
        'cross': True, 'cross_scale': 1.3, 'pants': 'mail', 'weapon': 'sword', 'offhand': 'shield_oval',
        'actions': {'ATTACK1': 'attack_slash', 'FORTRESS': 'work_hammer'},
    },
    # ---------------------------------------------------------------- the war elephant
    # A gray-brown elephant under a civ-colored blanket with a wide gold hem,
    # a wooden howdah with a civ-colored canopy, shields and spears.
    'War Elephant': {
        'archetype': 'elephant',
        'elephant': {'skin': '#7a6e5c', 'skin_dark': '#564c3e', 'wood': '#5a3e28', 'gold': GOLD,
                     'cloth_span': (-0.54, 0.42), 'cloth_drop': 0.24, 'hem': 2, 'canopy_h': 0.13, 'canopy_w': 0.86, 'canopy_band': True},
    },
}

for _u, _e in UNITS.items():
    if 'anchor' not in _e and _e['archetype'] != 'foot_knight':
        try:
            _e['anchor'] = _anchor(_u)
        except Exception:   # no Civ3 install: the framework's default
            pass
