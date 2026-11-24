"""Crews for the siege engines: a human at each of the model's crew empties.

    crews.populate(root, 'Cannon', catalog['Cannon'])

The catalog entry's 'crew' (the old remake's dress) is translated into the
foot units' vocabulary (riders.translate), then CREW below adds what each
crewman carries and how he stands, per unit and crew index. Each body is
built with units.build_footman (seated for a crewman whose CREW entry says
so) and parented to its empty, standing at its point and facing its +Y,
at CREW_SCALE. Needs MPFB (see README.md).
"""
import math

import bpy
from mathutils import Matrix, Vector

import riders

CREW_SCALE = 1.0
# a unit's crew hat pitch (degrees forward, riders.headgear front_fix); default 'level' (the crown straight up)
HAT_PITCH_BY_UNIT = {}
# the catalog's hat kinds -> the shared headgear (riders.HEADGEAR) or a footman helmet
HAT_KINDS = {'steel': ('_headgear', 'modern_helmet'), 'shako': ('_headgear', 'shako'), 'gat': ('_headgear', 'gat'),
             'cap': ('helmet', 'cap')}

# a crewman at work: a handspike (lever) held low across the body in both hands
WORK = {'weapon': 'staff', 'stance_name': 'low_across', 'two_hands': True, 'tool_dir': (-0.7, 0.35, 0.6),
        'props': {'staff': ('staff', {'length': 1.5, 'grip': 0.5})}}
# a WWII gun crew: the Infantry's body and M1, in olive drab (not its civ field shirt), civ accents only
OLIVE = {'_base': 'Infantry', 'tint': ['belt'], '_colors': {'top': '#5a6050', 'bottom': '#4e5444', 'belt': '#2a2a24'},
         'wraps': 'boots', 'gear': [], 'patterns': {}, 'bands': [('upperarm', 'tint', 0.05)]}
# kneeling on the right knee, the left foot planted forward (build_footman plants the lowest point)
KNEEL = {'pose_extra': [('thigh_l', (0.15, -0.95, -0.25)), ('calf_l', (0.08, 0.05, -1)), ('foot_l', (0.3, -1, -0.2)),
                        ('thigh_r', (-0.1, 0.3, -1)), ('calf_r', (-0.04, 1.0, -0.3)), ('foot_r', (0.0, 0.5, -1))]}   # shin on the ground
# the Hwacha crew's layered robes (Civ3: blue-grey robes over trousers, a white sash)
ROBE = {'coat_skirt': 'robe', 'coat_length': 0.85, 'sash': 'belt', 'hem_bands': [('robe', '#5a6070', 0.04)],
        '_headgear': 'straw_hat', 'hair_asset': None, 'headgear_lift': 0.03}   # a shallow conical straw hat (reads tan from every side; no hair through its crown)

# a bearer of the Ark (Civ3: blue tunics, bare heads, boots): walking, arms out and down to the poles at his sides
BEARER = {'_base': 'Settler', 'helmet': None, '_headgear': None, 'hair': 'short', 'beard': True,
          'top': 'sleeved', 'neck': 'high', 'bottom': 'kilt', 'robe_drop': 0.08, 'wraps': 'boots', 'cape': False,
          'cuirass': False, 'gear': [], 'bands': [('waist', 'belt', 0.05)], 'tint': ['top', 'bottom', 'sleeves'],
          'stance_name': 'stand',
          'pose_extra': [('upperarm_l', (0.52, 0.05, -1)), ('lowerarm_l', (0.32, 0.15, -1)),
                         ('upperarm_r', (-0.52, 0.05, -1)), ('lowerarm_r', (-0.32, 0.15, -1)),
                         ('thigh_l', (0.0, 0.3, -1)), ('thigh_r', (0.0, -0.2, -1))]}   # mid-stride

# realism-misc: a Levite bearer of the Ark (Numbers 4, 1 Chronicles 15): a civ tunic under a white linen
# ephod, linen breeches, a belt; mid-stride under a pole that rests on one shoulder
# (siege.RELIC_POLE_Z), both fists closed on it: the near fist in front of that shoulder, elbow bent and
# down, the other hand across the chest. LEVITE_R carries on his right shoulder, LEVITE_L on his left.
LEVITE = dict(BEARER, stance_name='ready', pose_extra=[], tint=['top'], cuirass=True, metal=[],
              # (a3) a civ tunic with its skirt to the knee all round, under a white linen ephod (vest), over
              # linen breeches (Exodus 28:42); the kilt was a front flap over bare legs in some directions
              bottom='long', coat_length=0.58, coat_flare=1.2,
              hem_bands=[('coat', '#e0d8c4', 0.05)], bands=[('waist', 'belt', 0.06)],
              _colors={'cuirass': '#e0d8c4', 'bottom': '#cfc8b6', 'belt': '#4a3220', 'boots': '#4a3424'})
LEVITE_R = dict(LEVITE, hand_r_at=(0.07, -0.2, 0.07), elbow_r=0.75, hand_l_at=(-0.32, -0.36, 0.07), elbow_l=0.3)
LEVITE_L = dict(LEVITE, hand_l_at=(-0.07, -0.2, 0.07), elbow_l=0.75, hand_r_at=(0.32, -0.36, 0.07), elbow_r=0.3)

# per unit: a list of per-crewman changes (index = the crew empty's number)
CREW = {
    # ancient and medieval crews are the Mac's reviewed soldiers of the era, disarmed and set to work
    'Catapult': [
        # working the winch with a handspike held low across the body
        # (the catalog's blue tunic and steel cap, as the original, with a belt strap and hem band)
        # hauling: bent forward at the waist over the handspike, head up
        dict(WORK, chest_strap='offset', hem_bands=[('top', '#3a2818', 0.03)], _skin='#a07a5c',
             pose_extra=[('spine_01', (0, -0.9, 1)), ('spine_02', (0, -1.1, 1)), ('neck_01', (0, -0.2, 1)),
                         ('head', (0, 0.5, 1)),   # bent near 50 degrees at the hips, head up
                         ('thigh_l', (0.2, -0.45, -1)), ('calf_l', (0.15, 0.35, -1)),   # knees flexed, braced
                         ('thigh_r', (-0.2, -0.2, -1)), ('calf_r', (-0.15, 0.45, -1))]),
        {'stance_name': 'fist_hip_l', 'chest_strap': 'offset', 'hem_bands': [('top', '#3a2818', 0.03)],
         '_skin': '#a07a5c'},
    ],
    'Cannon': [
        # the gunner, a musketman in his plumed morion, with the rammer upright
        {'_base': 'Musketman', 'weapon': 'staff', 'stance_name': 'stand_staff',
         'props': {'staff': ('staff', {'length': 1.9, 'grip': 1.0})},
         'tint': ['top', 'bottom'], '_colors': {'plume': '#ece8dc'}},   # the original's white plume
    ],
    'Trebuchet': [
        {'_base': 'Medieval Infantry', 'stance_name': 'arms_folded'},
        dict(WORK, _base='Medieval Infantry'),
    ],
    'Hwacha': [
        dict(ROBE, stance_name='stand'),
        # the fuse-lighter with his torch
        dict(ROBE, weapon='torch', stance_name='royal'),
    ],
    # WWII gun crews: the Mac's Infantry (M1 helmet, olive), read live at render time, unarmed, no pack
    'Artillery': [
        dict(OLIVE, stance_name='stand'),
    ],
    # TOW Infantry: both crewmen are the soldiers' TOW Infantry entry (remaster_soldiers.py)
    'TOW Infantry': [
        # the gunner seated on a crate at the sight, leaning in, both hands on the grips
        {'_base': 'TOW Infantry', 'seated': True,
         'pose_extra': [('spine_02', (0, -0.3, 1)), ('head', (0, 0.3, 1)),
                        ('upperarm_l', (0.15, -0.8, -0.45)), ('lowerarm_l', (-0.2, -1, 0.1)),
                        ('upperarm_r', (-0.15, -0.8, -0.45)), ('lowerarm_r', (0.2, -1, 0.1))]},
        # the assistant gunner seated on the missile crate beside him, spotting: a hand shading his eyes
        # (realism infantry)
        {'_base': 'TOW Infantry', 'seated': True,
         'pose_extra': [('spine_02', (0, -0.1, 1)), ('head', (0, 0.3, 1)),
                        ('upperarm_l', (0.25, -0.3, -1)), ('lowerarm_l', (-0.1, -1, -0.2)),
                        ('upperarm_r', (-0.3, -0.8, -0.05)), ('lowerarm_r', (0.6, 0.3, 0.6))]},
    ],
    # the Ark's bearers: knee-length civ tunics, bareheaded, brown boots; the poles at their spread hands
    'Holy Relic': [
        # crew 0-1 on the ark's left (front, rear), 2-3 on its right: each pole on the inner shoulder
        dict(LEVITE_R, _colors=dict(LEVITE['_colors'])),
        dict(LEVITE_R, beard=False, _colors=dict(LEVITE['_colors'], skin='#8a6648')),
        dict(LEVITE_L, beard=False, _colors=dict(LEVITE['_colors'], skin='#9a7656')),
        dict(LEVITE_L, _colors=dict(LEVITE['_colors'], skin='#7e5c40')),
    ],
    'Flak': [
        dict(OLIVE, seated=True),          # the gunner in his seat
        dict(OLIVE, stance_name='stand'),  # the loader beside the mount
    ],
}
# The realism pass (notes/realism/<Unit>.md): crews at work, hands on the engine ('_reach', in the engine
# root's space, matching the crew positions siege.REALISM gives the unit), with more uniform detail. Used in
# place of CREW[unit] when the unit's root carries 'crew_realism' (set by siege.build from siege.REALISM).
WEBBING = [('chest_strap', {'color': '#4e4c3a', 'width': 0.06, 'cross': True, 'offset': 0.03})]
# batch 3: the stock Infantry's build (thicker limbs and torso) for every realism crewman
BULK = {'macro': {'muscle': 1.0, 'weight': 0.9, 'height': 0.76, 'proportions': 0.72}, 'upper_bulk': 1.3}
OLIVE_R = dict(OLIVE, **BULK, gear=WEBBING, headgear_scale=0.85, hip_gear=[('canteen', {}, 'r')],
               headgear_params={'band': True},   # a civ band round the M1
               bands=[('upperarm', 'tint', 0.09), ('waist', 'belt', 0.05)],
               _colors={'top': '#5a6050', 'bottom': '#30342a', 'belt': '#2a2a24'})   # trousers darker than the tunic
TUNIC_R = dict(BULK, chest_strap='offset', hem_bands=[('top', '#3a2818', 0.04)], _skin='#a07a5c',
               bands=[('waist', 'belt', 0.06)], weapon=None, props={}, _colors={'bottom': '#3a2818'})
ROBE_R = dict(ROBE, **BULK, hem_bands=[('robe', '#262c3a', 0.09)], bands=[('waist', 'belt', 0.07)],
              gear=[('chest_strap', {'width': 0.08, 'tint': True})])   # a civ sash over the robe, a dark belt and hem
MED_R = dict(BULK, _base='Medieval Infantry', stance_name='stand', bands=[('waist', 'belt', 0.06)])
CREW_R = {
    'Catapult': [
        # the winchman at the left crank, both hands on its grip, leaning into it
        dict(TUNIC_R, stance_name='stand', _reach={'l': (-1.1, -1.25, 1.1), 'r': (-1.1, -1.36, 1.08)},
             pose_extra=[('spine_01', (0, -0.25, 1)), ('spine_02', (0, -0.3, 1)), ('head', (0, 0.3, 1)),
                         ('thigh_l', (0.05, -0.3, -1)), ('thigh_r', (-0.05, 0.25, -1))]),
        # the loader hauling the raised arm down with both hands, against the frame
        dict(TUNIC_R, stance_name='stand', _reach={'l': (0.14, -1.05, 1.45), 'r': (0.14, -0.82, 1.17)},
             pose_extra=[('spine_02', (0, -0.15, 1)), ('head', (0, 0.25, 1))]),
    ],
    'Trebuchet': [
        # at the winch's hand wheel, hauling on its rim
        dict(MED_R, _reach={'l': (-0.93, -1.2, 1.18), 'r': (-0.93, -1.55, 1.0)},
             pose_extra=[('spine_01', (0, -0.3, 1)), ('spine_02', (0, -0.3, 1)), ('head', (0, 0.35, 1))]),
        # loading the sling: bent over the trough, both hands on the shot
        dict(MED_R, _reach={'l': (0.16, -1.12, 0.95), 'r': (0.16, -1.38, 0.95)},
             pose_extra=[('spine_01', (0, -0.6, 1)), ('spine_02', (0, -0.6, 1)), ('head', (0, 0.5, 1)),
                         ('thigh_l', (0.1, -0.3, -1)), ('thigh_r', (-0.1, -0.3, -1))]),
    ],
    'Hwacha': [
        # on the handles, steadying the cart
        dict(ROBE_R, stance_name='stand', _reach={'l': (0.42, -1.82, 0.85), 'r': (-0.42, -1.82, 0.85)},
             pose_extra=[('spine_01', (0, -0.15, 1)), ('head', (0, 0.15, 1))]),
        # the fuse-lighter, a big lit torch held out to the fuses at the rack's back
        dict(ROBE_R, weapon='torch', props={'torch': ('torch', {'length': 0.6, 'scale': 1.5})}, stance_name='royal',
             _reach={'r': (0.5, -0.85, 1.15), 'l': (0.45, -0.45, 1.0)}),
    ],
    'Cannon': [
        # the gunner with a heavy rammer held across, toward the muzzle, the left hand on the wheel
        {'_base': 'Musketman', **BULK, 'weapon': 'staff', 'stance_name': 'port_arms',
         'props': {'staff': ('staff', {'length': 1.4, 'grip': 0.75, 'scale': 1.4})},
         'tint': ['top', 'bottom'], '_colors': {'plume': '#ece8dc'}, 'headgear_scale': 0.85,
         '_reach': {'l': (0.7, -0.5, 1.25)}},
    ],
    'Artillery': [
        # the gunner laying the gun: bent over the breech, one hand on it, one on the elevating handwheel
        dict(OLIVE_R, stance_name='stand', _reach={'r': (-0.15, -0.7, 0.8), 'l': (-0.3, -0.35, 0.8)},
             pose_extra=[('spine_01', (0, -0.35, 1)), ('spine_02', (0, -0.35, 1)), ('head', (0, 0.4, 1))]),
    ],
    'Flak': [
        # the gunner in his seat, both hands on the grips under the sight
        dict(OLIVE_R, seated=True, _reach={'l': (0.16, -0.3, 1.1), 'r': (-0.16, -0.3, 1.1)}),
        # the loader at the guns with a fresh magazine
        dict(OLIVE_R, stance_name='stand', _reach={'l': (0.38, 0.15, 1.35), 'r': (0.38, -0.1, 1.3)}),
    ],
}
# the MDJ engines' ashigaru (numpy_siege.py): a civ-colour coat over dark trousers, leg wraps and sandals, a
# black jingasa; the stock Infantry's build, as every realism crewman
ASHI_R = dict(BULK, race={'caucasian': 0.0, 'african': 0.0, 'asian': 1.0}, hair='short', helmet=None, _headgear='jingasa',
              headgear_lift=0.03, gear=[], tint=['top'], hem_bands=[('top', '#2a2018', 0.05)],
              bands=[('waist', 'belt', 0.07)], wraps='wraps', sandals=True, weapon=None, props={}, stance_name='stand',
              _skin='#b88a62', _colors={'bottom': '#3a3430', 'belt': '#2a2018', 'wraps': '#d8d0c0', 'wraps_band': '#2a2018',
                                        'sandal': '#6a5438'})
CREW_R.update({
    'Medieval Japanese Catapult': [
        # round 2: two haulers in front of the engine, hands on the pull ropes (numpy_siege's reach), leaning back
        dict(ASHI_R, _reach={'l': (-0.47, 1.62, 1.42), 'r': (-0.27, 1.62, 1.42)},
             pose_extra=[('spine_01', (0, 0.15, 1)), ('head', (0, 0.1, 1)),
                         ('thigh_l', (0.05, 0.3, -1)), ('thigh_r', (-0.05, -0.25, -1))]),
        dict(ASHI_R, _reach={'l': (0.27, 1.62, 1.42), 'r': (0.47, 1.62, 1.42)},
             pose_extra=[('spine_01', (0, 0.15, 1)), ('head', (0, 0.1, 1)),
                         ('thigh_l', (0.05, 0.3, -1)), ('thigh_r', (-0.05, -0.25, -1))]),
    ],
    'Fire Cannon': [
        # the gunner beside the breech: hands on the powder chamber and the back plate (numpy_siege's reach)
        dict(ASHI_R, _reach={'l': (-0.04, -0.3, 1.22), 'r': (-0.1, -0.64, 1.05)},
             pose_extra=[('spine_01', (0, -0.2, 1)), ('head', (0, 0.2, 1))]),
        # round 2: the rammer on the other side, his rammer upright
        dict(ASHI_R, weapon='staff', stance_name='stand_staff',
             props={'staff': ('staff', {'length': 1.9, 'grip': 1.0})}),
    ],
    'Siege Crossbow': [
        # the windlass man hauling a capstan bar round
        dict(ASHI_R, _reach={'l': (-0.45, -1.45, 0.88), 'r': (-0.62, -1.38, 0.95)},
             pose_extra=[('spine_01', (0, -0.3, 1)), ('spine_02', (0, -0.2, 1)), ('head', (0, 0.3, 1))]),
        # the aimer beside the stock, both hands on it, laying the bolt
        dict(ASHI_R, tint=['top', 'hat'], _reach={'l': (0.15, -0.35, 1.22), 'r': (0.15, -0.75, 1.22)},
             pose_extra=[('spine_01', (0, -0.3, 1)), ('head', (0, 0.3, 1))]),
    ],
})
# (realism/all) The crews' '_base' units as they were when the siege finals were rendered. The PC1 foot branch
# redesigned the Medieval Infantry in place (remaster_footmen.REMASTER), which changed the Trebuchet's crew; a
# base listed here is read from this copy instead of the live remaster.REMASTER entry. Likewise realism/infantry
# redesigned the Musketman (the Cannon's gunner) and the Infantry (the Artillery and Flak crews) through its REALISM
# overlay: those bases are read from remaster_soldiers.LEGACY, the entries as they were before it (LEGACY_SOLDIERS).
# The TOW Infantry team is itself an infantry final and keeps its live entry.
LEGACY_BASES = {
    'Medieval Infantry': {
        # a closed great helm, full mail, a civ-color surcoat from the
        # shoulders to the ankles with a gold hem and a brown belt, a small
        # round brown wooden shield (per the original), a flail at the knee
        'macro': {'muscle': 1.0, 'weight': 0.9, 'height': 0.78, 'proportions': 0.68},
        'colors': {'iron': '#7e828a', 'skin': '#94704f', 'helmet': '#6a6c70', 'mail': '#5a5a5e', 'belt': '#3a2818',
                   'bracers': '#5a5a5e', 'greaves': '#6e7074', 'steel': '#8a8c90',
                   'wood': '#4a3222', 'shield_face': '#7a5434', 'shield_rim': '#3a3a3e', 'bronze': '#6e7074',
                   'boots': '#8a8c90'},   # steel sabatons, not white
        'helmet': 'great_helm', 'metal': ['helmet'], 'hair_asset': None, 'beard': False,
        # the surcoat: a tinted sleeveless top and a tinted kilt dropped to the ankles
        'top': 'vest', 'neck': 'high', 'collar': 'mail',   # MISSING: tabard_front_width, helm_rivets
        # the civ-color surcoat to mid-thigh with a gold hem, ring mail on the arms and legs
        'bottom': None, 'coat_length': 0.42, 'coat_flare': 1.15, 'hem_bands': [('coat', 'gold', 0.06)],
        'mail_parts': ['arms', 'legs'], 'leather_bracers': True, 'wraps': None, 'boots': True,
        'gear': [],
        'weapon': 'club',
        'props': {'club': ('flail', {'length': 0.7, 'chain': 0.38})},   # big and bright: it read faint at 1x
        'hang_dir': (0.55, 0.35, -0.75),   # held out from the body, the head swinging clear
        # the original: a small round brown wooden shield
        'offhand': 'shield_round', 'offhand_mesh': ('plain_round', {'r': 0.25}),
        'shield_at': 'left_hip',
        'stance_name': 'guard',
        'tint': ['top', 'bottom'],
        'grime': 0.7, 'upper_bulk': 1.3,
    },
    # (realism/all, pinned at 034109a0) the Settler as the Holy Relic's final was rendered: the Ark's bearers
    # (BEARER, LEVITE) build from it, so a later change to the Settler can't move the Relic
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
        'pack_up': 0.14,   # (round 3: the lashings read as a shield's rims: off)
        'hem_bands': [('helmet', '#cfc6b0', 0.04)],
    },
}
LEGACY_SOLDIERS = ('Musketman', 'Infantry')
BASE_OVERRIDES = {'weapon': None, 'back': None, 'offhand': None, 'offhand_mesh': None, 'hip': None,
                  'hip_gear': None, 'chest_gear': None}


def _dress(entry):
    """The catalog crew entry in the foot units' vocabulary."""
    e = riders.translate(dict(entry, archetype='footman'))
    hat = e.pop('hat', None)
    if hat:
        kind = hat[0] if isinstance(hat, (list, tuple)) else hat
        slot, name = HAT_KINDS.get(kind, ('_headgear', kind))
        e[slot] = name
        c = e['colors']
        if len(hat) > 1:
            c['hat'] = c['helmet'] = hat[1]
        if len(hat) > 2:
            c['plume'] = hat[2]
    return e


def populate(root, unit, entry, catalog=None):
    """Put the unit's crew on its crew empties; returns the crew roots."""
    import addon_utils
    addon_utils.enable('bl_ext.user_default.mpfb', default_set=True)
    import units
    import human as H
    empties = sorted((o for o in root.children_recursive if o.type == 'EMPTY' and o.name.startswith('crew_')),
                     key=lambda o: o.name)
    made = []
    for e_obj in empties:
        i = int(e_obj.name.split('_')[1].split('.')[0])
        table = CREW_R if root.get('crew_realism') else CREW
        extra = dict(table[unit][i]) if i < len(table.get(unit, [])) else {}
        base = extra.pop('_base', None)
        if base:
            # a Mac foot unit's current look (remaster.REMASTER), disarmed
            import remaster
            src = LEGACY_BASES.get(base)   # (realism/all) a frozen base where listed
            if src is None and base in LEGACY_SOLDIERS:
                import remaster_soldiers
                src = remaster_soldiers.LEGACY[base]
            if src is None:
                src = remaster.REMASTER[base]
            e = {k: v for k, v in src.items() if k not in BASE_OVERRIDES}   # disarmed
            e['archetype'] = 'footman'
        else:
            src = entry.get('crew')
            if not src:
                continue
            e = _dress(src)
        seated = extra.pop('seated', False)
        # '_reach': {'l'|'r': (x, y, z) in the engine root's space}: the hands brought to the engine's handles
        # after the crewman is placed (riders' two-bone reach); absent = posed as his stance leaves him
        reach = extra.pop('_reach', None)
        if e_obj.get('reach'):   # the builder's own hand points for this crewman (numpy_siege.py), over the table's
            import json
            reach = json.loads(e_obj['reach'])
        reach_elbow = extra.pop('_reach_elbow', 0.35)
        more_colors = extra.pop('_colors', {})
        skin = extra.pop('_skin', None)
        e.update(extra)
        e['colors'] = dict(e.get('colors', {}), **more_colors, **({'skin': skin} if skin else {}))
        if e.get('coat_skirt') and 'robe' not in e.get('colors', {}):   # a robe in the coat's color
            e['colors'] = dict(e.get('colors', {}), robe=e.get('colors', {}).get('top', '#9aa2b0'))
        # (realism/all) no weight shift: the foot branch's posture system (units.STANCE_POSTURE) postdates the siege
        # crews' finals, which were posed without it; an entry may still ask for one with its own 'posture'
        e.setdefault('posture', False)
        croot = units.build_footman(e, seated=seated)
        arm = next(o for o in croot.children_recursive if o.type == 'ARMATURE')
        riders.headgear(e, arm, front_fix=HAT_PITCH_BY_UNIT.get(unit, 'level'))   # facing forward
        bpy.context.view_layer.update()
        # stand at the empty, facing its +Y, at CREW_SCALE in world terms
        world = e_obj.matrix_world
        loc, rot, _ = world.decompose()
        yaw = rot.to_euler().z
        # keep the root's own offset from build_footman (feet planted; pelvis at the origin when seated)
        own = croot.matrix_world.copy()
        croot.parent = e_obj
        croot.matrix_world = Matrix.Translation(loc) @ Matrix.Rotation(yaw, 4, 'Z') @ Matrix.Scale(CREW_SCALE, 4) @ own
        H._update()
        if reach:
            for side, at in sorted(reach.items()):
                H.reach(arm, side, root.matrix_world @ Vector(at), elbow_out=reach_elbow)
            H.fists(arm, sides=''.join(sorted(reach)))
            H._update()
        made.append(croot)
    return made
