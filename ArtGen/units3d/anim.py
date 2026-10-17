"""Animated actions for the foot units (Blender side): keyframed poses,
interpolated per frame, with the tools in the hands.

    root, ctx = anim.setup(entry)           # the built human, no weapon of its own
    anim.apply(ctx, ACTIONS['Worker']['workerMine'], frame)

An action is a dict:
- 'frames': how many frames to render (the flic may have more; the game
  spreads the columns over its frames), 'loop': whether it cycles;
- 'tool': (prop, params) in the right fist, or None;
- 'left': (prop, params) in the left fist (a basket), or None;
- 'ground': [(prop, params, (x, y, z), (rx, ry, rz) degrees)] standing in the
  unit's own space (+y forward), for the whole action (a stake);
- 'spray': {frame: (x, y, z)} a spray of seed in the air at those frames;
- 'drop_at': (x, y, yaw) where the tool lies once dropped (unit space);
- 'keys': [(frame, pose), ...].

A pose holds bone aims in armature space (as units.STANCES: -y is forward,
+x the figure's left, +z up) laid over BASE, and these extras:
- 'tool': (up, face): the tool's long axis (toward its working end) and the
  way its edge, point or face looks, armature space;
- 'slide': the right fist this far up the haft from the tool's grip;
- 'grip2': the left fist on the haft this far from the right one, toward
  the butt (negative: toward the head); 'reach_l': the left fist at a point
  in unit space instead (on the stake);
- 'tilt': degrees the whole body leans back about the feet (a fall);
- 'drop': True from this key on the tool lies on the ground, False picks it up.

Between keys the aims are interpolated with an ease in and out; a looping
action eases from its last key back to its first.
"""
import math

import bpy
from mathutils import Matrix, Vector

import human as H
import props as P
import tools  # noqa: F401  (registers the work tools)
import units

ORDER = ['spine_01', 'spine_02', 'spine_03', 'clavicle_l', 'clavicle_r', 'neck_01', 'head',
         'upperarm_l', 'lowerarm_l', 'upperarm_r', 'lowerarm_r',
         'thigh_l', 'calf_l', 'foot_l', 'thigh_r', 'calf_r', 'foot_r']

BASE = dict(units.UPRIGHT)
BASE.update({
    'thigh_l': (0.2, -0.06, -1), 'calf_l': (0.13, 0.04, -1), 'foot_l': (0.45, -1, -0.3),
    'thigh_r': (-0.2, 0.04, -1), 'calf_r': (-0.13, 0.05, -1), 'foot_r': (-0.45, -1, -0.3),
    'upperarm_l': (0.18, -0.05, -1), 'lowerarm_l': (0.1, -0.2, -1),
    'upperarm_r': (-0.18, -0.05, -1), 'lowerarm_r': (-0.1, -0.2, -1),
})


def _tip(deg):
    """The armature-space direction of a bone tipped `deg` forward from straight up."""
    a = math.radians(deg)
    return (0.0, -math.sin(a), math.cos(a))


def bend(k, head=0.3):
    """Bent forward over the work by k (1 is 60 degrees at the chest; the aims
    are absolute, so each bone is set to its share), the head coming back up
    by `head` so the face reads; negative arches back."""
    a = 60.0 * k
    return {'spine_01': _tip(0.65 * a), 'spine_02': _tip(a), 'spine_03': _tip(a), 'neck_01': _tip(0.8 * a),
            'head': _tip(0.6 * a - math.degrees(math.atan(head)))}


WIDE = {'thigh_l': (0.26, -0.45, -1), 'calf_l': (0.15, 0.3, -1), 'thigh_r': (-0.26, -0.3, -1), 'calf_r': (-0.15, 0.35, -1)}
SQUAT = {'thigh_l': (0.35, -0.95, -0.25), 'calf_l': (0.12, 0.45, -1), 'foot_l': (0.4, -1, -0.3),
         'thigh_r': (-0.35, -0.9, -0.3), 'calf_r': (-0.12, 0.5, -1), 'foot_r': (-0.4, -1, -0.3)}
HALF_KNEEL = {'thigh_l': (0.2, -0.95, -0.3), 'calf_l': (0.1, 0.1, -1), 'foot_l': (0.3, -1, -0.2),
              'thigh_r': (-0.15, -0.2, -1), 'calf_r': (-0.05, 1.0, -0.25), 'foot_r': (0.0, 0.5, -1)}
CROUCH_RUN = {'thigh_l': (0.15, -0.35, -1), 'calf_l': (0.1, 0.35, -1), 'thigh_r': (-0.15, -0.35, -1), 'calf_r': (-0.1, 0.35, -1)}


def K(*parts, **extra):
    pose = {}
    for p in parts:
        pose.update(p)
    pose.update(extra)
    return pose


def R_(upper, lower):
    return {'upperarm_r': upper, 'lowerarm_r': lower}


def L_(upper, lower):
    return {'upperarm_l': upper, 'lowerarm_l': lower}


# ------------------------------------------------------------ the Worker
# from notes/anim/Worker.md (the Civ3 originals, frame by frame)

SHOVEL = ('shovel', {'length': 1.15, 'k': 1.3})
# the idle carry: upright in front of the right shoulder, blade up beside the head
CARRY = K(R_((-0.15, -0.3, -1), (0.0, -1.0, 0.55)), tool=((-0.05, -0.05, 1), (0, -1, 0)), slide=0.5)


def _two_hand_swing(tool, grip2, keys, slide=0.0):
    """A two-handed tool: the left fist `grip2` toward the butt from the right, the right fist
    `slide` up the haft from the grip."""
    return {'frames': 10, 'loop': True, 'tool': tool,
            'keys': [(f, K(p, grip2=grip2, **({'slide': slide} if slide else {}))) for f, p in keys]}


WORKER = {
    'WorkerDefault': {'frames': 1, 'loop': True, 'tool': SHOVEL, 'keys': [(0, CARRY)]},

    # a crouched trot in place, two steps a cycle, the shovel upright in both hands at the chest
    'workerRun': {
        'frames': 10, 'loop': True, 'tool': SHOVEL,
        'keys': [(f, K(bend(0.25), CROUCH_RUN, legs, R_((-0.2, -0.55, -0.8), (0.1, -1.0, 0.45)),
                       tool=((0.0, -0.1, 1), (0, -1, 0)), slide=0.45, grip2=0.32))
                 for f, legs in [
                     (0, {}),
                     (2, {'thigh_r': (-0.12, -0.95, -0.6), 'calf_r': (-0.08, -0.25, -1), 'foot_r': (-0.3, -1, 0.0),
                          'thigh_l': (0.12, 0.45, -1), 'calf_l': (0.08, 0.85, -0.7), 'foot_l': (0.3, -0.2, -1)}),
                     (4, {'thigh_r': (-0.12, -0.3, -1), 'thigh_l': (0.12, -0.25, -1), 'calf_l': (0.08, 0.6, -1)}),
                     (7, {'thigh_l': (0.12, -0.95, -0.6), 'calf_l': (0.08, -0.25, -1), 'foot_l': (0.3, -1, 0.0),
                          'thigh_r': (-0.12, 0.45, -1), 'calf_r': (-0.08, 0.85, -0.7), 'foot_r': (-0.3, -0.2, -1)}),
                     (9, {'thigh_l': (0.12, -0.3, -1), 'thigh_r': (-0.12, -0.25, -1), 'calf_r': (-0.08, 0.6, -1)}),
                 ]]},

    # sowing: the basket at the waist in the left hand, the right hand dips, then throws
    'WorkerPlant': {
        'frames': 10, 'loop': True, 'tool': None, 'left': ('seed_basket', {'k': 1.35}),
        'spray': {4: (0.05, 0.5, 0.55), 5: (0.05, 0.65, 0.3), 6: (0.05, 0.75, 0.04)},
        'keys': [
            (0, K(L_((0.15, -0.2, -1), (-0.45, -0.75, -0.3)), R_((-0.05, -0.4, -0.95), (0.6, -0.75, 0.1)))),
            (2, K(L_((0.15, -0.2, -1), (-0.45, -0.75, -0.3)), R_((-0.05, -0.4, -0.95), (0.65, -0.7, -0.1)))),
            (4, K(bend(0.15), L_((0.15, -0.2, -1), (-0.45, -0.75, -0.3)), R_((-0.15, -0.85, -0.5), (-0.05, -0.7, -0.7)))),
            (6, K(bend(0.1), L_((0.15, -0.2, -1), (-0.45, -0.75, -0.3)), R_((-0.2, -0.6, -0.8), (0.1, -0.9, -0.2)))),
            (9, K(L_((0.15, -0.2, -1), (-0.45, -0.75, -0.3)), R_((-0.05, -0.4, -0.95), (0.6, -0.75, 0.1)))),
        ]},

    # hoeing a ditch: bent 35 degrees throughout, pushed forward level, two chops (f3, f7)
    'workerIrrigate': _two_hand_swing(('hoe', {'length': 1.5, 'k': 1.15}), 0.45, slide=0.6, keys=[
        (0, K(WIDE, bend(0.55), R_((-0.15, -0.95, -0.2), (0.05, -0.95, 0.1)), tool=((0, -1, 0.05), (0, 0, -1)))),
        (2, K(WIDE, bend(0.6), R_((-0.15, -0.9, -0.35), (0.05, -0.9, -0.2)), tool=((0, -0.9, -0.35), (0, 0.3, -1)))),
        (3, K(WIDE, bend(0.65), R_((-0.15, -0.85, -0.45), (0.05, -0.85, -0.35)), tool=((0, -0.85, -0.5), (0, 0.4, -0.9)))),
        (5, K(WIDE, bend(0.55), R_((-0.15, -0.6, -0.75), (0.05, -0.8, -0.35)), tool=((0, -0.9, -0.3), (0, 0.2, -1)))),
        (7, K(WIDE, bend(0.65), R_((-0.15, -0.85, -0.45), (0.05, -0.85, -0.35)), tool=((0, -0.85, -0.5), (0, 0.4, -0.9)))),
        (9, K(WIDE, bend(0.55), R_((-0.15, -0.95, -0.2), (0.05, -0.95, 0.1)), tool=((0, -1, 0.05), (0, 0, -1)))),
    ]),

    # the pickaxe: across the chest, up over the head and back (top f4), down past the face (f5),
    # the strike bent 60 degrees (f6-7), levered up (f8), upright (f9)
    'workerMine': _two_hand_swing(('pickaxe', {'k': 1.35}), 0.12, [
        (0, K(R_((-0.25, -0.6, -0.75), (0.55, -0.75, 0.35)), tool=((0.35, -0.75, 0.55), (0, 0.3, 1)))),
        (2, K(bend(-0.05), R_((-0.25, -0.35, 0.9), (0.0, 0.2, 1.0)), tool=((0, 0.45, 0.9), (0, -0.4, 1)))),
        (4, K(bend(-0.2, head=0.45), R_((-0.2, 0.15, 1), (0.0, 0.75, 0.65)), tool=((0, 1, 0.05), (0, 0, 1)))),
        (5, K(WIDE, bend(0.3), R_((-0.15, -0.65, 0.65), (0.0, -0.25, 1.0)), tool=((0, -0.15, 1), (0, -1, 0)))),
        (6, K(WIDE, bend(0.95), R_((-0.12, -0.85, -0.45), (0.0, -0.8, -0.6)), tool=((0, -0.4, -0.92), (0, 0.9, -0.4)))),
        (7, K(WIDE, bend(0.95), R_((-0.12, -0.85, -0.45), (0.0, -0.8, -0.6)), tool=((0, -0.4, -0.92), (0, 0.9, -0.4)))),
        (8, K(WIDE, bend(0.6), R_((-0.15, -0.75, -0.5), (0.0, -0.75, -0.2)), tool=((0, -0.75, -0.6), (0, 0.6, -0.8)))),
        (9, K(bend(0.1), R_((-0.2, -0.55, -0.8), (0.1, -0.95, 0.4)), tool=((0, -0.15, 1), (0, -1, 0)))),
    ]),

    # grubbing the roadbed: scraping back toward the feet (f0-4), lifted to vertical (f6-7),
    # swung down (f8), at knee height (f9), contact into f0
    'workerRoad': _two_hand_swing(('mattock', {'k': 1.25}), 0.4, slide=0.35, keys=[
        (0, K(WIDE, bend(0.75), R_((-0.15, -0.85, -0.5), (0.05, -0.8, -0.55)), tool=((0, -0.7, -0.7), (0, 0.7, -0.7)))),
        (4, K(WIDE, bend(0.8), R_((-0.15, -0.6, -0.8), (0.05, -0.55, -0.85)), tool=((0, -0.45, -0.9), (0, 0.9, -0.45)))),
        (5, K(WIDE, bend(0.5), R_((-0.2, -0.6, -0.5), (0.05, -0.9, 0.3)), tool=((0, -0.5, 0.85), (0, -0.85, -0.5)))),
        (7, K(WIDE, bend(0.15), R_((-0.2, -0.45, 0.4), (0.0, -0.3, 1.0)), tool=((0, 0.1, 1), (0, -1, 0.1)))),
        (8, K(WIDE, bend(0.55), R_((-0.15, -0.8, -0.2), (0.05, -0.9, 0.0)), tool=((0, -0.9, 0.3), (0, -0.3, -0.9)))),
        (9, K(WIDE, bend(0.7), R_((-0.15, -0.85, -0.45), (0.05, -0.85, -0.4)), tool=((0, -0.8, -0.55), (0, 0.55, -0.8)))),
    ]),

    # felling: the axe low at the right side (f0), over the shoulder (f1), behind the head (f3-4),
    # vertical in front (f5), the chop bent 50 degrees at shin height (f6-7), up (f8), vertical (f9)
    'workerForest': _two_hand_swing(('felling_axe', {'k': 1.4}), 0.14, [
        (0, K(R_((-0.25, -0.2, -1), (-0.1, -0.5, -0.9)), tool=((-0.2, -0.4, -0.9), (0, -1, 0.3)))),
        (1, K(R_((-0.5, -0.2, 0.4), (0.3, -0.3, 1)), tool=((-0.3, 0.5, 0.8), (0, -0.6, 0.6)))),
        (3, K(bend(-0.15, head=0.45), R_((-0.2, 0.15, 1), (0.0, 0.75, 0.65)), tool=((0, 1, 0.05), (0, 0, 1)))),
        (4, K(bend(-0.15, head=0.45), R_((-0.2, 0.15, 1), (0.0, 0.75, 0.65)), tool=((0, 1, 0.05), (0, 0, 1)))),
        (5, K(WIDE, bend(0.3), R_((-0.15, -0.65, 0.65), (0.0, -0.25, 1.0)), tool=((0, -0.15, 1), (0, -1, 0)))),
        (6, K(WIDE, bend(0.8), R_((-0.12, -0.9, -0.4), (0.0, -0.85, -0.5)), tool=((0, -0.6, -0.8), (0, 0.8, -0.6)))),
        (7, K(WIDE, bend(0.8), R_((-0.12, -0.9, -0.4), (0.0, -0.85, -0.5)), tool=((0, -0.6, -0.8), (0, 0.8, -0.6)))),
        (8, K(WIDE, bend(0.45), R_((-0.15, -0.75, -0.3), (0.0, -0.8, 0.1)), tool=((0, -0.6, 0.5), (0, -0.6, -0.6)))),
        (9, K(bend(0.1), R_((-0.2, -0.55, -0.8), (0.1, -0.95, 0.4)), tool=((0, -0.15, 1), (0, -1, 0)))),
    ]),

    # the machete, one-handed in a slight crouch: two slashes a cycle, low in front (f3, f8),
    # overhead between them (f1, f6)
    'workerJungle': {
        'frames': 10, 'loop': True, 'tool': ('machete', {'k': 1.4}),
        'keys': [(f, K(CROUCH_RUN, bend(0.25), L_((0.28, -0.1, -1), (0.05, -0.65, -0.6)), arm,
                       tool=tool)) for f, arm, tool in [
            (0, R_((-0.2, -0.75, 0.4), (0.0, -0.4, 1.0)), ((0.0, -0.2, 1), (0, -1, 0))),
            (1, R_((-0.3, -0.1, 1), (0.0, 0.6, 0.8)), ((0, 0.9, 0.3), (0, 0.2, 1))),
            (2, R_((-0.25, -0.75, 0.5), (0.05, -0.6, 0.8)), ((0, -0.6, 0.6), (0, -0.6, -0.6))),
            (3, R_((-0.1, -0.85, -0.5), (0.45, -0.85, -0.25)), ((0.75, -0.6, -0.1), (0.4, 0.3, -0.8))),
            (4, R_((-0.45, -0.45, 0.6), (-0.1, -0.2, 1)), ((-0.4, 0.2, 0.9), (0, -1, 0.2))),
            (6, R_((-0.3, -0.1, 1), (0.0, 0.6, 0.8)), ((0, 0.9, 0.3), (0, 0.2, 1))),
            (7, R_((-0.25, -0.75, 0.5), (0.05, -0.6, 0.8)), ((0, -0.6, 0.6), (0, -0.6, -0.6))),
            (8, R_((-0.1, -0.85, -0.55), (0.5, -0.8, -0.35)), ((0.8, -0.5, -0.25), (0.4, 0.3, -0.8))),
            (9, R_((-0.2, -0.8, 0.0), (0.0, -0.7, 0.7)), ((0.0, -0.4, 0.9), (0, -1, -0.3))),
        ]]},

    # driving a stake in a deep squat: the left hand steadies it, the mallet up (f2) and down on it (f5)
    'workerFortress': {
        'frames': 10, 'loop': True, 'tool': ('mallet', {'length': 0.34, 'k': 1.35}),
        'ground': [('stake', {'length': 0.95, 'k': 1.3}, (0.05, 0.98, -0.1), (0, 0, 0))],
        'keys': [(f, K(SQUAT, bend(b), arm, reach_l=(0.05, 0.92, 0.85), tool=tool)) for f, b, arm, tool in [
            (0, 0.45, R_((-0.2, -0.75, 0.35), (0.1, -0.75, 0.55)), ((0.1, -0.55, -0.85), (0, 0.6, -0.8))),
            (2, 0.2, R_((-0.3, -0.2, 1), (0.0, 0.6, 0.8)), ((0, 0.8, 0.5), (0, -0.4, 1))),
            (4, 0.35, R_((-0.25, -0.7, 0.6), (0.05, -0.2, 1)), ((0, -0.3, 0.95), (0, -1, 0.2))),
            (5, 0.5, R_((-0.2, -0.75, 0.3), (0.1, -0.75, 0.5)), ((0.1, -0.55, -0.85), (0, 0.6, -0.8))),
            (9, 0.45, R_((-0.2, -0.75, 0.35), (0.1, -0.75, 0.55)), ((0.1, -0.55, -0.85), (0, 0.6, -0.8))),
        ]]},

    # struck: recoils hunched (f1-3), sits back (f4), topples (f5-6) as the shovel flies forward,
    # back on the ground (f7) with an arm up (f7-8), lying still (f9-14)
    'workerDeath': {
        'frames': 15, 'loop': False, 'tool': SHOVEL, 'drop_at': (0.1, 1.0, 20),
        'keys': [
            (0, CARRY),
            (2, K(bend(0.45, head=0.1), HALF_KNEEL, L_((0.25, -0.7, -0.6), (-0.4, -0.8, 0.3)),
                  R_((-0.25, -0.7, -0.6), (0.4, -0.8, 0.3)), tool=((-0.05, -0.3, 1), (0, -1, 0)), slide=0.5)),
            (4, K(SQUAT, bend(0.1, head=0.3), L_((0.4, -0.6, -0.4), (0.3, -0.9, 0.2)),
                  R_((-0.4, -0.6, -0.4), (-0.3, -0.9, 0.2)), tilt=30, drop=True)),
            (6, K(SQUAT, bend(-0.1, head=0.4), L_((0.5, -0.5, 0.4), (0.4, -0.6, 0.6)),
                  R_((-0.5, -0.5, 0.4), (-0.4, -0.6, 0.6)), tilt=65)),
            (7, K(bend(0.0, head=0.2), L_((0.6, -0.3, 0.7), (0.3, -0.2, 1.0)), R_((-0.8, 0.0, -0.4), (-0.9, 0.1, -0.2)),
                  thigh_l=(0.2, -0.45, -1), calf_l=(0.12, 0.35, -1), thigh_r=(-0.2, -0.4, -1), calf_r=(-0.12, 0.4, -1),
                  tilt=88)),
            (8, K(bend(0.0, head=0.2), L_((0.5, -0.6, 0.6), (0.3, -0.6, 0.7)), R_((-0.4, 0.0, -1), (-0.3, 0.1, -1)),
                  thigh_l=(0.2, -0.4, -1), calf_l=(0.12, 0.3, -1), thigh_r=(-0.2, -0.35, -1), calf_r=(-0.12, 0.35, -1),
                  tilt=90)),
            (10, K(head=(0, 0.15, 1), upperarm_l=(0.3, 0.0, -1), lowerarm_l=(0.2, -0.1, -1),
                   upperarm_r=(-0.3, 0.0, -1), lowerarm_r=(-0.2, -0.1, -1),
                   thigh_l=(0.22, -0.3, -1), calf_l=(0.14, 0.2, -1), thigh_r=(-0.22, -0.25, -1), calf_r=(-0.14, 0.25, -1),
                   tilt=90)),
        ]},

    # surrender: the shovel thrown down in front (f2), cowering half-kneeling with both hands
    # raised (f3-8, lowest f5-7), up again reaching for it (f9-10), the idle carry back (f11-14)
    'workerCaptured': {
        'frames': 15, 'loop': False, 'tool': SHOVEL, 'drop_at': (0.05, 0.6, 80),
        'keys': [
            (0, CARRY),
            (1, CARRY),
            (2, K(bend(0.3), R_((-0.2, -0.8, -0.5), (0.0, -0.9, -0.4)), tool=((0, -0.9, -0.3), (0, 0, 1)), drop=True)),
            (4, K(HALF_KNEEL, bend(0.6, head=0.0), L_((0.2, -0.7, -0.5), (-0.1, -0.5, 0.85)),
                  R_((-0.2, -0.7, -0.5), (0.1, -0.5, 0.85)))),
            (6, K(HALF_KNEEL, bend(0.75, head=-0.1), L_((0.2, -0.75, -0.55), (-0.1, -0.45, 0.9)),
                  R_((-0.2, -0.75, -0.55), (0.1, -0.45, 0.9)))),
            (8, K(HALF_KNEEL, bend(0.6, head=0.0), L_((0.2, -0.7, -0.5), (-0.1, -0.5, 0.85)),
                  R_((-0.2, -0.7, -0.5), (0.1, -0.5, 0.85)))),
            (10, K(SQUAT, bend(0.8), R_((-0.15, -0.6, -0.8), (0.0, -0.5, -0.9)))),
            (11, K(bend(0.4), R_((-0.15, -0.5, -0.85), (0.0, -0.8, -0.2)), tool=((0, -0.45, 0.9), (0, -1, 0)), drop=False)),
            (13, CARRY),
            (14, CARRY),
        ]},
}

ACTIONS = {'Worker': WORKER}


# ------------------------------------------------------------ playback

def _ease(t):
    return t * t * (3 - 2 * t)


def _merged(pose):
    full = dict(BASE)
    full.update(pose)
    return full


def _mix(a, b, t):
    return Vector(a).normalized().lerp(Vector(b).normalized(), t).normalized()


def pose_at(action, frame):
    """The interpolated pose of `action` at `frame`: bone aims as Vectors, and the extras."""
    keys = [(f, _merged(p)) for f, p in action['keys']]
    n = action['frames']
    if action.get('loop') and len(keys) > 1:
        keys = keys + [(keys[0][0] + n, keys[0][1])]
    a, b, t = keys[-1][1], keys[-1][1], 0.0
    if frame <= keys[0][0]:
        a = b = keys[0][1]
    else:
        for (f0, p0), (f1, p1) in zip(keys, keys[1:]):
            if f0 <= frame <= f1:
                a, b, t = p0, p1, _ease((frame - f0) / (f1 - f0))
                break
    pose = {bone: _mix(a[bone], b[bone], t) for bone in ORDER}
    ta, tb = a.get('tool', b.get('tool')), b.get('tool', a.get('tool'))
    if ta is not None:
        pose['tool'] = tuple(_mix(x, y, t) for x, y in zip(ta, tb))
    pose['slide'] = a.get('slide', 0.0) * (1 - t) + b.get('slide', 0.0) * t
    pose['grip2'] = a.get('grip2') if t < 0.5 else b.get('grip2')
    ra, rb = a.get('reach_l'), b.get('reach_l')
    pose['reach_l'] = Vector(ra).lerp(Vector(rb), t) if ra and rb else (ra or rb)
    pose['tilt'] = a.get('tilt', 0.0) * (1 - t) + b.get('tilt', 0.0) * t
    pose['drop'] = False
    for f, p in action['keys']:      # the last key at or before the frame that says
        if f <= frame and 'drop' in p:
            pose['drop'] = p['drop']
    return pose


class Context:
    def __init__(self, root, arm, mesh, colors):
        self.root, self.arm, self.mesh, self.colors = root, arm, mesh, colors
        self.made = {}


def setup(entry):
    """Builds the entry's human with no weapon of its own; the actions bring their tools."""
    entry = dict(entry, weapon=None, offhand=None, props={})
    root = units.build_footman(entry)
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    mesh = next(o for o in bpy.data.objects if o.type == 'MESH' and o.parent == arm
                and any(m.type == 'ARMATURE' for m in o.modifiers) and len(o.data.vertices) > 10000)
    return root, Context(root, arm, mesh, units._colors(entry))


def _prop(ctx, slot, spec):
    """The prop for `slot`, made once, shown, unparented and back at its own
    scale (a bone parent's scale would otherwise build up frame by frame)."""
    key = (slot, spec[0], repr(sorted(spec[1].items())))
    if key not in ctx.made:
        params = dict(spec[1])
        k = params.pop('k', 1.0)          # the whole prop this much bigger (Civ3's tools read big)
        obj = P.make(spec[0], ctx.colors, **params)
        if k != 1.0:
            obj.data.transform(Matrix.Diagonal((k, k, k, 1.0)))
        ctx.made[key] = obj
    obj = ctx.made[key]
    obj.hide_render = False
    obj.parent = None
    obj.matrix_world = Matrix.Identity(4)
    return obj


def _unit_point(root, p):
    return root.matrix_world @ Vector(p)


def apply(ctx, action, frame):
    """Poses the human for `frame` of `action`, with its tools and props."""
    arm, root = ctx.arm, ctx.root
    for o in ctx.made.values():
        o.hide_render = True
    pose = pose_at(action, frame)
    arm.rotation_euler.x = 0.0
    H.pose(arm, [(b, tuple(pose[b])) for b in ORDER])
    H.fists(arm, sides='lr')
    H.plant(root, ctx.mesh)
    to_world = arm.matrix_world.to_3x3()
    tool = _prop(ctx, 'r', action['tool']) if action.get('tool') else None
    if tool is not None and not pose['drop'] and 'tool' in pose:
        up, face = (to_world @ v for v in pose['tool'])
        up.normalize()
        at = H.grip_point(arm, 'hand_r')
        H.hold(tool, arm, 'hand_r', at - up * pose['slide'], up=tuple(up), forward=tuple(face))
        if pose.get('grip2') is not None:
            H.reach(arm, 'l', at - up * pose['grip2'], elbow_out=0.6)
            H.fists(arm, sides='l')
    if pose.get('reach_l') is not None:
        H.reach(arm, 'l', _unit_point(root, pose['reach_l']), elbow_out=0.5)
        H.fists(arm, sides='l')
    if action.get('left'):
        basket = _prop(ctx, 'l', action['left'])
        H.hold(basket, arm, 'hand_l', H.grip_point(arm, 'hand_l'), up=(0, 0, 1), forward=tuple(to_world @ Vector((0, -1, 0))))
    # things on the ground stand on the real ground: planting the feet moves the root down in a squat
    ground = -root.location.z
    for i, (name, params, at, rot) in enumerate(action.get('ground', [])):
        obj = _prop(ctx, f'g{i}', (name, params))
        obj.parent = root
        obj.location = (at[0], at[1], at[2] + ground)
        obj.rotation_euler = tuple(math.radians(r) for r in rot)
    if frame in action.get('spray', {}):
        spray = _prop(ctx, 's', ('seed_spray', {}))
        spray.parent = root
        x, y, z = action['spray'][frame]
        spray.location = (x, y, z + ground)
    # a fall: the body tips back about the feet (backward is +y in the armature's own space)
    if pose['tilt']:
        arm.rotation_euler.x = -math.radians(pose['tilt'])
        H.plant(root, ctx.mesh)
    if tool is not None and pose['drop']:
        tool.parent = root
        x, y, yaw = action.get('drop_at', (0.3, 0.6, 0))
        tool.location = (x, y, 0.03 - root.location.z)
        tool.rotation_euler = (math.radians(-90), 0.0, math.radians(yaw))
    H._update()
    return pose
