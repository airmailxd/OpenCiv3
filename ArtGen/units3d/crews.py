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
from mathutils import Matrix

import riders

CREW_SCALE = 1.0
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
        '_headgear': 'straw_hat'}   # a shallow conical straw hat (reads tan from every side)

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
        {'_base': 'TOW Infantry'},   # the spotter, as the entry poses him (hand raised to his face)
    ],
    'Flak': [
        dict(OLIVE, seated=True),          # the gunner in his seat
        dict(OLIVE, stance_name='stand'),  # the loader beside the mount
    ],
}
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
        extra = dict(CREW[unit][i]) if i < len(CREW.get(unit, [])) else {}
        base = extra.pop('_base', None)
        if base:
            # a Mac foot unit's current look (remaster.REMASTER), disarmed
            import remaster
            e = {k: v for k, v in remaster.REMASTER[base].items() if k not in BASE_OVERRIDES}   # disarmed
            e['archetype'] = 'footman'
        else:
            src = entry.get('crew')
            if not src:
                continue
            e = _dress(src)
        seated = extra.pop('seated', False)
        more_colors = extra.pop('_colors', {})
        skin = extra.pop('_skin', None)
        e.update(extra)
        e['colors'] = dict(e.get('colors', {}), **more_colors, **({'skin': skin} if skin else {}))
        if e.get('coat_skirt') and 'robe' not in e.get('colors', {}):   # a robe in the coat's color
            e['colors'] = dict(e.get('colors', {}), robe=e.get('colors', {}).get('top', '#9aa2b0'))
        croot = units.build_footman(e, seated=seated)
        arm = next(o for o in croot.children_recursive if o.type == 'ARMATURE')
        riders.headgear(e, arm)
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
        made.append(croot)
    return made
