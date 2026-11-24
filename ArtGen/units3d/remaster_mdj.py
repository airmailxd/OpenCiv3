"""The MDJ (Medieval Japan) foot units remade in units3d (the realism overhaul,
notes/realism/<Unit>.md): plain data merged over the old catalog entries, as
remaster.py's REMASTER. The NumPy sprites they replace gave each unit's
identity; the style is the Samurai's final (yoroi.py's armour, mdj_kit.py's
props and hats). Units with no catalog entry here say 'units3d_only'.

Every entry clears the old catalog's keys the Blender side would misread
('gear', 'stance', 'top' / 'bottom' names) and says its own.
"""

_ASIAN = {'caucasian': 0.0, 'african': 0.0, 'asian': 1.0}
_BASE = {
    'kits': ['mdj_kit'], 'race': _ASIAN, 'age': 0.45,
    'macro': {'muscle': 0.9, 'weight': 0.7, 'height': 0.7, 'proportions': 0.7},
    'helmet': None, 'hair': 'bald', 'hair_asset': None, 'topknot': None, 'beard': False,
    'gear': [], 'stance': None, 'patterns': {}, 'chest_emblem': None, 'coat_length': None,
    'wide_sleeves': False, 'puffy': False, 'belt_gear': False, 'offhand': None, 'back': None,
    'grime': 0.45, 'upper_bulk': 1.05, 'desat': 0.85,
}


def _e(**kw):
    out = dict(_BASE)
    out.update(kw)
    return out


REMASTER = {
    # An ashigaru yari spearman of the late Sengoku (c. 1575): a black okegawa-do and short
    # laced kusazuri over a dark kosode, kobakama and gaiters, a lacquered iron jingasa with
    # the civ mon, the civ sashimono on his back, a long yari carried ready, tipped forward.
    'Ashigaru Spearman': _e(
        unit_scale=0.95,
        colors={'skin': '#c49a70', 'hair': '#141010', 'top': '#5a4a3a', 'belt': '#2a2420', 'sandal': '#a89060',
                'shoes': '#c8c4ba', 'do': '#5a4434', 'lacquer': '#4a3a2e', 'iron': '#7a7a80', 'gold': '#c8a048',
                'hakama': '#6e604a', 'kyahan': '#3e3832', 'rivets': '#8a6a48', 'mon': '#ece8de',
                'wood': '#3a2a22', 'steel': '#d8dce2', 'cord': '#d8d0bc', 'cloth_wrap': '#2a2420',
                'gourd': '#dcc088'},
        top='sleeved', neck='high', bottom=None, sandals=True, wraps='shoes',
        headgear='jingasa', headgear_params={'brim': 0.24, 'height': 0.15}, brow_drop=0.04,
        yoroi={'hakama': {'balloon': 0.05, 'puff_top': 0.86, 'pleats': 0.015}, 'suneate': {},
               'do': {'sugake': False, 'rivets': 0.2, 'gloss': 0.45},
               'kusazuri': {'panels': 6, 'rows': 3, 'length': 0.4, 'flare': 1.45, 'edge': 0.2},
               'sashimono': {'width': 0.22, 'height': 0.55, 'pole': 1.4, 'yaw': 80.0, 'pole_r': 0.014}},
        hip_gear=[('gourd', {'scale': 1.5}, 'r')],
        weapon='spear', props={'spear': ('yari', {'length': 3.3, 'grip': 1.15, 'head': 0.38, 'width': 0.055})},
        stance_name='ready', hand_r_at=(-0.15, -0.24, -0.16), elbow_r=0.4,
        weapon_dir=(-0.12, -0.62, 0.78), edge_dir=(-1.0, 0.0, 0.0),
        hand_l_at=(0.1, -0.08, -0.55), elbow_l=0.3,
        hip=('katana_sheathed', {'length': 0.62, 'thick': 1.6}), hip_at=(0.18, -0.04, 0.05),
        tint=['lace', 'banner', 'hat'],
    ),
    # A ji-samurai spearman of the Otomo of Bungo (Kyushu): a pale kosode and hakama, a
    # black do over them, the sleeves tied back with a civ tasuki crossed on chest and
    # back, a civ hachimaki and obi; a shorter yari held ready, tipped forward.
    'Otomo Spearman': _e(
        unit_scale=1.1,
        colors={'skin': '#c89a6c', 'hair': '#141010', 'top': '#b8a074', 'belt': '#2a2420', 'sandal': '#a89060',
                'shoes': '#c8c4ba', 'do': '#2a2220', 'lacquer': '#2a2220', 'iron': '#4a4a4e', 'gold': '#c8a048',
                'hakama': '#9a8458', 'kyahan': '#4a4238', 'rivets': '#8a5a38', 'wood': '#5a3a24',
                'steel': '#d8dce2', 'cord': '#d8d0bc', 'wraps': '#4a4238', 'wraps_band': '#2e2a24'},
        top='sleeved', neck='high', bottom=None, sandals=True, wraps='wraps',
        hair='short', topknot='topknot', headgear='nmf_hachimaki', headgear_params={'r': 0.104, 'z': 0.0}, brow_drop=0.095,
        kits=['mdj_kit', 'nmf_kit'],
        wraps_height=0.47, wraps_plain=True,
        yoroi={'hakama': {'balloon': 0.03, 'puff_top': 0.86, 'pleats': 0.015, 'hem': 0.45, 'gather': 0.015},
               'do': {'sugake': False, 'rivets': 0.18, 'gloss': 0.3, 'rows': 4},
               'sashimono': {'width': 0.18, 'height': 0.42, 'pole': 1.22, 'yaw': 70.0, 'pole_r': 0.014}},
        gear=[('chest_strap', {'tint': True, 'width': 0.085, 'cross': True, 'offset': 0.06})], bands=[('waist', 'tint', 0.1)],
        weapon='spear', props={'spear': ('yari', {'length': 3.0, 'grip': 1.1, 'head': 0.36, 'width': 0.055})},
        stance_name='ready', hand_r_at=(-0.3, -0.2, -0.16), elbow_r=0.5,
        weapon_dir=(-0.12, -0.42, 0.9), edge_dir=(-1.0, 0.0, 0.0),
        two_hands=True, grip2=-0.35, elbow_l=0.5,
        hip=('katana_sheathed', {'length': 0.62, 'thick': 1.6}), hip_at=(0.18, -0.04, 0.05),
        tint=['hat', 'strap', 'banner', 'wraps'],
    ),
    # A Minamoto warrior of the Genpei War (1180s) in o-yoroi: the boxy laced armour of the
    # mounted archer-knight, the big flat sode and four broad kusazuri laced in the civ
    # colour, a hoshi-bachi kabuto with tall gilt kuwagata and wide fukigaeshi, no mask,
    # pale hakama (the Minamoto white); the tachi drawn and held high in hasso.
    'Minamoto Samurai': _e(
        unit_scale=0.97, brow_drop=0.03,
        macro={'muscle': 0.95, 'weight': 0.75, 'height': 0.72, 'proportions': 0.7},
        colors={'skin': '#d8a878', 'hair': '#0e0c0c', 'top': '#2c2a30', 'belt': '#1a1824', 'sandal': '#a89060',
                'shoes': '#d8d4ca', 'do': '#2a201c', 'lace': '#b89a6a', 'lacquer': '#2a1e1a', 'iron': '#4a4a4e', 'gold': '#e0b448',
                'cloth_wrap': '#e2dac6', 'leather_dark': '#2a1e18', 'steel': '#dfe3e8', 'hakama': '#cfc6ae',
                'kote': '#4a4a52', 'kyahan': '#3e3832', 'rivets': '#a85a38', 'mon': '#ece8de',
                'haidate_lace': '#3a2a20'},
        top='sleeved', neck='high', bottom=None, sandals=True, wraps='shoes',
        yoroi={'hakama': {'balloon': 0.1, 'puff_top': 0.86, 'pleats': 0.02}, 'suneate': {}, 'kote': {},
               'do': {'sugake': False, 'rivets': 0.22, 'gloss': 0.5},
               'kusazuri': {'panels': 4, 'gap': 0.1, 'edge': 0.45, 'flare': 1.45, 'length': 0.5},
               'sode': {'width': 0.32, 'height': 0.34, 'out': 0.04, 'rows': 7},
               'kabuto': {'horn_height': 0.3, 'horn_width': 1.35, 'lace_tint': False}},
        kits=['mdj_kit', 'nmf_kit'],
        weapon='sword', props={'sword': ('nmf_katana', {'length': 0.92, 'width': 0.09, 'spine': 0.024, 'curve': 0.1})},
        back='nmf_agemaki', back_params={'scale': 1.2}, pack_up=0.0,
        stance_name='hasso', blade_dir=(-0.42, -0.08, 0.9), edge_dir=(0.3, -1, 0), grip2=0.17,
        hip=('katana_saya', {'length': 0.72, 'thick': 1.8}), hip_at=(0.19, -0.03, 0.04),
        hip_dir=(0.12, 0.38, -0.92), hand_on_hilt=False, posture=False,
        tint=['lace'], upper_bulk=1.08,
    ),
    # A sohei (warrior monk) of Mount Hiei: the white katsuo-zukin wrapped over the head and
    # lower face, a dark robe with wide sleeves and hakama, white gaiters, the monk's kesa
    # over the left shoulder and an obi in the civ colour, waraji; the naginata held ready,
    # the curved blade high and out.
    'Warrior Monk': _e(
        unit_scale=1.05,
        colors={'skin': '#c08e64', 'hair': '#141010', 'top': '#3a3634', 'belt': '#2a2420', 'sandal': '#a89060',
                'hakama': '#5e5650', 'kyahan': '#d8d4ca', 'wraps': '#d8d4ca', 'wraps_band': '#a8a49a',
                'wood': '#3a2620', 'steel': '#dfe3e8', 'iron': '#3e3e42', 'gold': '#c8a048', 'cord': '#d8d0bc',
                'zukin': '#e6e2d8', 'lacquer': '#2a1e1a', 'kesa_edge': '#e4dac0'},
        top='sleeved', wide_sleeves=True, neck='high', bottom=None, sandals=True, wraps='wraps',
        headgear='nmf_zukin', kits=['mdj_kit', 'nmf_kit'],
        yoroi={'hakama': {'balloon': 0.11, 'puff_top': 0.9, 'pleats': 0.02}},
        gear=[('v_collar', {'color': '#e2ded4', 'width': 0.05}),
              ('chest_strap', {'tint': True, 'width': 0.34, 'offset': 0.04, 'edge': 'kesa_edge', 'edge_width': 0.035})],
        bands=[('waist', 'tint', 0.16)],
        weapon='spear', props={'spear': ('nmf_naginata', {'length': 2.5, 'grip': 1.1, 'blade': 0.78, 'width': 0.095,
                                                         'curve': 0.16})},
        stance_name='ready', hand_r_at=(-0.3, -0.18, -0.26), elbow_r=0.55,
        weapon_dir=(-0.5, -0.15, 0.85), edge_dir=(-1.0, -0.3, 0.0),
        hand_l_at=(0.1, -0.06, -0.56), elbow_l=0.3,
        tint=['strap'], upper_bulk=1.1,
    ),
    # Otomo no Makuta, a samurai archer: dark lacquered armour laced in the civ colour
    # (sode, kusazuri, shikoro), a kabuto with a gilt maedate, pale hakama; the tall
    # asymmetric yumi at the ready with an arrow nocked, an ebira of arrows at his back.
    'Otomo no Makuta': _e(
        unit_scale=1.0, brow_drop=0.02,
        colors={'skin': '#dcb084', 'hair': '#141010', 'top': '#3a3634', 'belt': '#2a2420', 'sandal': '#a89060',
                'shoes': '#c8c4ba', 'do': '#7a5a3a', 'lacquer': '#5e4230', 'iron': '#4a4a4e', 'gold': '#d8b048',
                'hakama': '#b8a888', 'kote': '#4a4a52', 'kyahan': '#3e3832', 'rivets': '#8a6a48',
                'wood': '#5a3e24', 'string': '#e0dccc', 'fletch': '#e6e0ce', 'leather': '#4a3220',
                'haidate_lace': '#3a2a20', 'bamboo': '#a08a50', 'rattan': '#d0c090'},
        top='sleeved', neck='high', bottom=None, sandals=True, wraps='shoes',
        yoroi={'hakama': {'balloon': 0.1, 'puff_top': 0.86, 'pleats': 0.02}, 'suneate': {}, 'kote': {},
               'do': {'sugake': False, 'rivets': 0.2, 'gloss': 0.45},
               'kusazuri': {'panels': 5, 'edge': 0.38, 'flare': 1.45, 'length': 0.48},
               'sode': {'width': 0.25, 'height': 0.28, 'out': 0.03},
               'kabuto': {'horn_height': 0.16, 'horn_width': 1.1}},
        stance_name='bow_ready', offhand='bow',
        offhand_mesh=('nmf_yumi', {'height': 2.3, 'below': 0.8, 'bend': 0.12}),
        arrow_dir=(0.8, -0.55, -0.25), bow_up=(0.15, 0.0, 1.0), bow_thick=1.3, bow_depth=1.0, draw_elbow=0.6,
        weapon='arrow', props={'arrow': ('arrow', {'length': 0.85})},
        back='nmf_ebira', back_params={'arrows': 10, 'scale': 1.2, 'back': 0.13, 'side': 0.1, 'drop': 0.05, 'lean': 0.0, 'top': 0.72}, pack_up=0.0, kits=['mdj_kit', 'nmf_kit'],
        hip=('katana_sheathed', {'length': 0.62, 'thick': 1.6}), hip_at=(0.18, -0.04, 0.05),
        tint=['lace'],
    ),
    # Hojo General: a Sengoku daimyo of the Hojo in full tosei-gusoku laced in red, a
    # kabuto with tall gilt kuwagata and a red menpo, the big civ sashimono with the Hojo's
    # three scales; his katana drawn and raised forward in the right hand, signalling the
    # advance, the left fist on his hip.
    'Hojo General': _e(
        units3d_only=True, unit_scale=0.9,
        macro={'muscle': 0.95, 'weight': 0.8, 'height': 0.72, 'proportions': 0.7}, age=0.6,
        colors={'skin': '#c8986c', 'hair': '#0e0c0c', 'top': '#2c2a30', 'belt': '#1a1824', 'sandal': '#a89060',
                'shoes': '#c8c4ba', 'do': '#3e2820', 'lacquer': '#34211a', 'iron': '#4a4a4e', 'gold': '#e0b448',
                'cloth_wrap': '#e2dac6', 'leather_dark': '#2a1e18', 'steel': '#dfe3e8', 'hakama': '#5a3a2e',
                'kote': '#4a4a52', 'kyahan': '#3e3832', 'menpo': '#c8482c', 'rivets': '#a0281c', 'mon': '#ece8de',
                'haidate_lace': '#3a2a20', 'paper': '#f4f2ea', 'lace': '#9a2a20'},
        top='sleeved', neck='high', bottom=None, sandals=True, wraps='shoes',
        yoroi={'hakama': {'balloon': 0.1, 'puff_top': 0.86, 'pleats': 0.02}, 'suneate': {}, 'haidate': {}, 'kote': {},
               'do': {'sugake': False, 'rivets': 0.45, 'gloss': 0.55},
               'kusazuri': {'edge': 0.28, 'flare': 1.5, 'length': 0.55},
               'sode': {'width': 0.28, 'height': 0.3, 'out': 0.03}, 'menpo': {'glow': 0.35},
               'kabuto': {'horn_height': 0.31, 'horn_width': 1.9, 'lace_tint': False},
               'sashimono': {'width': 0.28, 'height': 0.66, 'pole': 1.6, 'yaw': 35.0, 'pole_r': 0.022},
               'wakizashi': {}},
        kits=['mdj_kit', 'nmf_kit'],
        weapon='sword', props={'sword': ('nmf_katana', {'length': 0.8, 'width': 0.09, 'spine': 0.024})},
        stance_name='ready', hand_r_at=(-0.2, -0.24, 0.45), elbow_r=0.5,
        weapon_dir=(-0.3, -0.35, 0.89), edge_dir=(0.0, -0.3, 1.0),
        hand_l_at=(0.14, -0.16, -0.2), elbow_l=0.6,
        offhand='saihai', offhand_mesh=('nmf_saihai', {}), offhand_dir=(0.25, -0.25, 0.93),
        hip=('katana_saya', {'length': 0.7, 'thick': 1.8}), hip_at=(0.19, -0.03, 0.04),
        hip_dir=(0.12, 0.38, -0.92),
        tint=['lace', 'banner'], upper_bulk=1.1,
    ),

    # ---------------------------------------------------------------- batch 2

    # A rifleman of the Imperial Japanese Army (1937-45): the Type 90 helmet under a civ
    # cover, a khaki tunic, breeches and puttees, webbing and a field pack (civ), the long
    # Arisaka with its long bayonet carried at the port.
    'Japanese Rifleman': _e(
        units3d_only=True, kits=['mdj_kit', 'nmf_kit'],
        macro={'muscle': 0.9, 'weight': 0.65, 'height': 0.7, 'proportions': 0.7},
        colors={'skin': '#c49a70', 'hair': '#141010', 'top': '#857a52', 'bottom': '#857a52', 'belt': '#4a3a26',
                'boots': '#4a3626', 'wraps': '#7a7050', 'wraps_band': '#5e5640', 'olive_dark': '#6e6446',
                'olive': '#7a7050', 'wood': '#7a4a28', 'steel': '#d0d4da', 'helmet': '#6a6448',
                'leather_dark': '#3a2a1c', 'gold': '#c8a048', 'cord': '#5e5640'},
        hair='short', headgear='nmf_type90', headgear_params={'band': 0.03, 'band_z': 0.05, 'net': False},
        top='sleeved', neck='high', bottom='breeches', trouser_offset=0.02, wraps='wraps', boots=True,
        sandals=False,
        gear=[('chest_strap', {'color': '#5a4a30', 'width': 0.06, 'cross': True, 'offset': 0.03})],
        bands=[('waist', 'belt', 0.06), ('neck', 'tint', 0.05)],
        back='nmf_ija_pack', back_params={'size': 0.95, 'tint': True, 'roll': 0.062, 'flap': True, 'tin': 1.35},
        hip_gear=[('nmf_pouch', {}, 'r'), ('nmf_canteen', {}, 'l'), ('nmf_pouch', {}, 'back')],
        weapon='rifle', props={'rifle': ('nmf_arisaka', {'length': 1.28, 'bayonet': 0.4})},
        stance_name='ready', hand_r_at=(-0.05, -0.26, -0.36), elbow_r=0.35,
        weapon_dir=(0.42, -0.32, 0.85), edge_dir=(0.0, 0.5, 1.0), two_hands=True, grip2=-0.3, elbow_l=0.45,
        tint=[], upper_bulk=1.1, grime=0.75, unit_scale=1.15,
    ),
    # A teppo ashigaru of Oda Nobunaga's gunners (Nagashino, 1575): a tall pointed jingasa
    # with the civ mon, a black okegawa-do and kusazuri laced in grey over a kosode in the
    # civ colour (its sleeves), dark hakama and gaiters; the tanegashima carried upright
    # in the right fist, muzzle up, the slow match coiled on it; a powder flask at the belt.
    'Oda Arquebusier': _e(
        colors={'skin': '#c89a70', 'hair': '#141010', 'top': '#3a3230', 'belt': '#2a2420', 'sandal': '#a89060',
                'shoes': '#8a8478', 'do': '#5a4434', 'lacquer': '#4a3a2e', 'iron': '#3e3e42', 'gold': '#c8a048',
                'hakama': '#3e3a36', 'kyahan': '#3a3632', 'rivets': '#8a6a48', 'mon': '#ece8de',
                'lace': '#8a8a8c', 'wood': '#8a5e34', 'bronze': '#b08a40', 'match': '#6a5a44', 'cord': '#d8d0bc',
                'cloth_wrap': '#2a2420'},
        top='sleeved', neck='high', bottom=None, sandals=True, wraps='shoes',
        headgear='jingasa', headgear_params={'brim': 0.22, 'height': 0.2}, brow_drop=0.06,
        yoroi={'hakama': {'balloon': 0.05, 'puff_top': 0.86, 'pleats': 0.015}, 'suneate': {},
               'do': {'sugake': False, 'rivets': 0.2, 'gloss': 0.45},
               'kusazuri': {'panels': 6, 'rows': 3, 'length': 0.36, 'flare': 1.45, 'edge': 0.35},
               'sashimono': {'width': 0.27, 'height': 0.62, 'pole': 1.4, 'yaw': 35.0, 'pole_r': 0.014}},
        kits=['mdj_kit', 'nmf_kit'],
        weapon='musket', props={'musket': ('nmf_matchlock', {'length': 1.35})},
        stance_name='ready', hand_r_at=(-0.05, -0.26, -0.36), elbow_r=0.35,
        weapon_dir=(0.42, -0.32, 0.85), edge_dir=(0.0, 0.5, 1.0), two_hands=True, grip2=-0.32, elbow_l=0.45,
        hip=('katana_sheathed', {'length': 0.6, 'thick': 1.6}), hip_at=(0.18, -0.04, 0.05),
        tint=['hat', 'lace', 'banner'], unit_scale=1.18,
    ),
    # A crossbowman of the early Japanese state (the ritsuryo armies, 7th-9th c., kept the
    # crossbow, do-kyu, in their arsenals): a civ eboshi cap, a pale kosode tied back with a
    # civ tasuki, leather shoulder pads, brown hakama bound with dark leg wraps; a hand
    # crossbow held ready across the hips, a bolt in the groove.
    'Stone Crossbow': _e(
        macro={'muscle': 1.0, 'weight': 0.75, 'height': 0.72, 'proportions': 0.7},
        colors={'skin': '#b88c62', 'hair': '#141010', 'top': '#cfc6ae', 'belt': '#2a2420', 'sandal': '#a89060',
                'hakama': '#6e5440', 'wraps': '#4a4238', 'wraps_band': '#4a4238', 'wood': '#6e5238',
                'nmf_case': '#a07a4c',
                'prod': '#3a2a1e', 'string': '#e0dccc', 'bronze': '#a8803c', 'steel': '#c8ccd2',
                'cord': '#c8b488', 'cuirass': '#a8844c', 'leather_dark': '#3a2a1c'},
        hair='short', topknot='topknot', headgear='hachimaki', brow_drop=0.05,
        top='sleeved', neck='high', bottom=None, sandals=True, wraps='wraps', wraps_height=0.42, wraps_plain=True,
        yoroi={'hakama': {'balloon': 0.035, 'puff_top': 0.86, 'pleats': 0.015}},
        gear=[('v_collar', {'color': '#5a4632', 'width': 0.04}),
              ('chest_strap', {'tint': True, 'cross': True, 'offset': 0.04, 'width': 0.1})],
        kits=['mdj_kit', 'nmf_kit'], bands=[('waist', 'tint', 0.12), ('knee', 'wraps', 0.04)],
        hip_gear=[('nmf_bolt_quiver', {'length': 0.45, 'key': 'nmf_case'}, 'r')],
        weapon='musket', props={'musket': ('nmf_xbow', {'length': 0.8, 'span': 0.85})}, muzzle=False,
        stance_name='ready', hand_r_at=(-0.02, -0.25, -0.45), elbow_r=0.4,
        weapon_dir=(0.55, -0.55, 0.63), edge_dir=(0.0, -1.0, 0.0), two_hands=True, grip2=-0.3, elbow_l=0.5,
        tint=['hat'], upper_bulk=1.15, unit_scale=1.09,
    ),
    # The daimyo in court dress: the tall lacquered eboshi (civ crown), a dark
    # wide-sleeved kariginu with the sleeves in the civ colour, a pale under-robe at the
    # collar, wide hakama, white tabi; the gilt-rimmed gunbai raised in his right hand.
    'Medieval Japanese Leader': _e(
        macro={'muscle': 0.8, 'weight': 0.8, 'height': 0.74, 'proportions': 0.68}, age=0.6,
        colors={'skin': '#d0a882', 'hair': '#141010', 'top': '#2a2626', 'belt': '#ece8e0', 'shoes': '#e8e4dc',
                'hakama': '#5e4428', 'lacquer': '#1e1614', 'gold': '#d0a848', 'collar_w': '#ece8e0'},
        hair='short', headgear='eboshi',
        top='sleeved', wide_sleeves=True, neck='high', bottom=None, sandals=False, wraps='shoes',
        yoroi={'hakama': {'balloon': 0.12, 'puff_top': 0.95, 'pleats': 0.025, 'hem': 0.06}},
        bands=[('neck', 'collar_w', 0.06), ('waist', 'belt', 0.06)],
        weapon='scepter', props={'scepter': ('gunbai', {'length': 0.46, 'r': 0.15})},
        stance_name='royal', upright_dir=(-0.1, -0.35, 1.0),
        tint=['hat', 'sleeves'], upper_bulk=1.1, grime=0.3,
    ),
    # A peasant family's man moving to new land: a topknot tied with a civ cord, a pale
    # kosode tucked up, a civ obi and civ kyahan leg wraps, waraji; a big furoshiki bundle
    # on his back, a tall staff.
    'Medieval Japanese Settler': _e(
        macro={'muscle': 0.85, 'weight': 0.65, 'height': 0.72, 'proportions': 0.7}, age=0.55,
        colors={'skin': '#c89c70', 'hair': '#141010', 'top': '#a89a7c', 'bottom': '#a89a7c', 'belt': '#2a2420',
                'sandal': '#a89060', 'pack': '#5a4a36', 'bedroll': '#8a8270', 'pot': '#6a4a30', 'wood': '#6a4a30',
                'nmf_furoshiki': '#3e4456', 'nmf_straw': '#9a8456',
                'wraps_band': '#808080'},
        hair='short', topknot='topknot', kits=['mdj_kit', 'nmf_kit'],
        headgear='nmf_kasa', headgear_params={'brim': 0.2, 'height': 0.1, 'crown': 0.18, 'weave': True},
        top='sleeved', neck='high', bottom='skirt', skirt_length=0.45, sandals=True, wraps='wraps',
        wraps_height=0.4, wraps_plain=True, bands=[('waist', 'tint', 0.12)],
        gear=[('v_collar', {'color': '#6a5a44', 'width': 0.055})],
        back='nmf_furoshiki', back_params={'scale': 1.3, 'tint_ties': True, 'soft': True, 'mat': False},
        pack_up=0.16,
        weapon='staff', props={'staff': ('staff', {'length': 1.95, 'grip': 1.1})},
        stance_name='ready', hand_r_at=(-0.16, -0.12, -0.4), elbow_r=0.4,
        weapon_dir=(-0.04, 0.0, 1.0), edge_dir=(0.0, -1.0, 0.0),
        hand_l_at=(0.07, 0.02, -0.58), elbow_l=0.25,
        tint=['wraps'], grime=0.6, unit_scale=1.15,
    ),
    # A peasant at work: the wide sugegasa in the civ colour, a short tan jacket with civ
    # armbands, dark breeches to the knee, bare shins and waraji; the kuwa (hoe) carried on
    # the right shoulder, its blade up and back.
    'Medieval Japanese Worker': _e(
        # (realism/all, staging) the DEFAULT flic keeps this still, as its final; the Worker's actions still animate
        still_default=True,
        macro={'muscle': 0.85, 'weight': 0.5, 'height': 0.7, 'proportions': 0.7},
        colors={'skin': '#b4845b', 'hair': '#141010', 'top': '#b8a07a', 'bottom': '#6e6250', 'belt': '#dcd6c8',
                'sandal': '#a89060', 'wood': '#7a5a3a', 'steel': '#c4c6ca', 'iron': '#3a3a3e', 'cord': '#d8d0bc',
                'cloth_wrap': '#2a2420', 'lacquer': '#2a2420'},
        hair='short', headgear='nmf_kasa',
        headgear_params={'brim': 0.27, 'height': 0.15, 'crown': 0.4, 'weave': True},
        top='tunic', sleeve_x=0.3, neck='high', bottom='long', sandals=True, wraps=None,
        kits=['mdj_kit', 'nmf_kit'], bands=[('waist', 'tint', 0.13), ('knee', 'tint', 0.045)],
        weapon='shovel', props={'shovel': ('kuwa_broad', {'length': 0.95, 'blade_w': 0.3, 'blade_l': 0.3, 'thick': 0.02})},
        stance_name='ready', hand_r_at=(-0.04, -0.13, -0.12), elbow_r=0.25,
        weapon_dir=(-0.38, 0.92, 0.12), edge_dir=(-0.5, 0.0, -0.85),
        hand_l_at=(0.08, -0.04, -0.58), elbow_l=0.35,
        tint=['hat'], grime=0.8, unit_scale=1.08,
    ),
}
