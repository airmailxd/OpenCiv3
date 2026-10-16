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

# per unit: a list of per-crewman changes (index = the crew empty's number)
CREW = {
    'Catapult': [
        {'stance_name': 'guard', 'props': {'staff': ('staff', {'length': 1.4, 'grip': 0.7})}, 'weapon': 'staff'},
        {'stance_name': 'stand'},
    ],
    'Cannon': [
        # the gunner with his rammer upright, as the original
        {'weapon': 'staff', 'stance_name': 'stand_staff', 'props': {'staff': ('staff', {'length': 1.9, 'grip': 1.0})}},
    ],
    'Trebuchet': [
        {'stance_name': 'arms_folded'},
        {'stance_name': 'stand_staff', 'weapon': 'staff', 'props': {'staff': ('staff', {'length': 1.6, 'grip': 0.8})}},
    ],
    'Hwacha': [
        {'stance_name': 'stand'},
        # the fuse-lighter with his torch
        {'weapon': 'torch', 'stance_name': 'royal'},
    ],
    # WWII gun crews: the Mac's Infantry (M1 helmet, olive), read live at render time, unarmed, no pack
    'Artillery': [
        {'_base': 'Infantry', 'stance_name': 'stand'},
    ],
    'Flak': [
        {'_base': 'Infantry', 'seated': True},        # the gunner in his seat
        {'_base': 'Infantry', 'stance_name': 'stand'},  # the loader beside the mount
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
            e = dict(remaster.REMASTER[base], **BASE_OVERRIDES)
            e['archetype'] = 'footman'
        else:
            src = entry.get('crew')
            if not src:
                continue
            e = _dress(src)
        seated = extra.pop('seated', False)
        e.update(extra)
        croot = units.build_footman(e, seated=seated)
        arm = next(o for o in croot.children_recursive if o.type == 'ARMATURE')
        riders.headgear(e, arm)
        bpy.context.view_layer.update()
        # stand at the empty, facing its +Y, at CREW_SCALE in world terms
        world = e_obj.matrix_world
        loc, rot, _ = world.decompose()
        yaw = rot.to_euler().z
        croot.parent = e_obj
        croot.matrix_world = Matrix.Translation(loc) @ Matrix.Rotation(yaw, 4, 'Z') @ Matrix.Scale(CREW_SCALE, 4)
        H._update()
        made.append(croot)
    return made
