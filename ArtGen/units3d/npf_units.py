"""The League "modern mod" foot units that the NumPy generator made (ArtGen/units: catalog/modern_mod_foot.py and
league_foot.py), remade as units3d units for the realism overhaul (PC2, branch realism/numpy-foot).

Everything here is new and self-contained, so the shared foot code (human.py, units.py, props.py, garments.py:
owned by PC1's melee builder) is not touched:

- the new props and hats register themselves in props.BUILDERS and riders.HEADGEAR under 'npf_' names when this
  module is imported on the Blender side;
- ENTRIES are full units3d entries (the builder's own vocabulary: 'top', 'bottom', 'cuirass', 'headgear',
  'weapon', 'stance_name' ...), not merged over the old catalog (these units have no catalog entry on this branch);
- build(entry) builds a lone soldier with units.build_footman, or a team ('team': a list of entries, each placed at
  its own spot) for the Battalion's machine-gun crew.

npf_render.py renders a unit's still from here; npf_build.py renders and composes it (as build.py does).
Design notes per unit: notes/realism/<Unit>.md; group notes: notes/realism/_numpy_foot.md.
"""
import math

try:
    import bpy  # noqa: F401
    from mathutils import Matrix, Vector
    import props as P
    from props import T, R, S, box, tube, lathe, slab, ellipsoid
    import riders
    _BLENDER = True
except ImportError:          # the venv side (npf_build.py) only reads ENTRIES
    _BLENDER = False


# ------------------------------------------------------------ materials (new keys only)

NEW_DEFAULTS = {
    'npf_tube': ('#2c2e2a', 0.5, 0.6, 'metal'),        # an RPG's parkerized tube
    'npf_warhead': ('#4e5236', 0.55, 0.2, 'metal'),     # the PG-7 warhead's olive paint
    'npf_wood': ('#6e3e22', 0.55, 0.0, 'wood'),         # a heat shield / stock in dark red-brown wood
    'npf_brass': ('#b08e48', 0.4, 1.0, 'metal'),        # cartridge brass on a belt
    'npf_glass': ('#1c2226', 0.08, 0.0, None),          # optics, goggles
    'npf_rag': ('#d8ccb0', 0.9, 0.0, 'cloth'),          # a molotov's rag
    'npf_bottle': ('#4e7a3a', 0.15, 0.0, None),          # a green glass bottle
    'npf_ranger': ('#4e5242', 0.85, 0.0, 'cloth'),      # ranger-green nylon kit
    'npf_coyote': ('#8a7652', 0.85, 0.0, 'cloth'),      # coyote-brown nylon
    'npf_shemagh': ('#d8d0bc', 0.9, 0.0, 'cloth'),      # the shemagh's off-white
    'npf_shemagh_red': ('#8e2a24', 0.9, 0.0, 'cloth'),  # its red check
    'npf_tank': ('#5a5e4c', 0.5, 0.5, 'metal'),         # flamethrower tanks
    'npf_fire': ('#ff7a20', 0.5, 0.0, 'flame'),          # a burning rag (self-lit, orange)
    'npf_fire_core': ('#ffd84a', 0.5, 0.0, 'flame'),     # a flame's yellow heart
    'npf_light_wood': ('#9a6a3c', 0.55, 0.0, 'wood'),    # a lighter birch stock
    'npf_flame_matte': ('#ff5a0a', 0.6, 0.0, None),       # a saturated orange plume (not self-lit: no wash-out)
    'npf_flame_tip': ('#ffc828', 0.6, 0.0, None),
    'npf_hose': ('#8e8c7c', 0.7, 0.0, None),            # a light rubberised hose
    'npf_glow': ('#7ae8e0', 0.3, 0.0, 'flame'),         # an emitter's glow (self-lit)
    'npf_armor': ('#5a5650', 0.55, 0.3, 'metal'),       # power-armour plate, matte gunmetal
    'npf_helmet_poly': ('#2a2b2c', 0.6, 0.0, None),   # a polymer ballistic helmet
    'npf_supp': ('#6e706c', 0.5, 0.4, 'metal'),        # a suppressor's lighter grey
    'npf_suit': ('#2a2a28', 0.8, 0.0, 'cloth'),         # the suit under the armour
}


def _register_defaults():
    for k, v in NEW_DEFAULTS.items():
        P.DEFAULTS.setdefault(k, v)


# ------------------------------------------------------------ props (grip at the origin, muzzle +Z, top +Y)

def _sb(s, sx, sy, sz):
    """A box scaled with its prop (props.py's boxes keep their size when `scale` moves them apart)."""
    return box(sx * s, sy * s, sz * s)


def npf_rpg7(p, scale=1.0):
    """An RPG-7 with a PG-7 round loaded, held at its pistol grip (origin): the tube along +Z (the muzzle and
    warhead ahead, the flared venturi behind), the wooden heat shields over the middle, a forward grip, the
    PGO-7 optic on the left. The tube's axis runs 0.075 above the grip (+Y)."""
    s = scale
    y = 0.075 * s
    p.add(tube([(0, y, -0.55 * s), (0, y, 0.38 * s)], 0.04 * s, seg=14), 'npf_tube', smooth=True)
    p.add(lathe([(-0.72 * s, 0.062 * s), (-0.66 * s, 0.05 * s), (-0.55 * s, 0.03 * s)], seg=16), 'npf_tube',
          T(0, y, 0), smooth=True)                                                         # the venturi bell
    p.add(lathe([(-0.12 * s, 0.036 * s), (0.2 * s, 0.036 * s)], seg=14), 'npf_wood', T(0, y, 0), smooth=True)
    # the round: the tail with its fins in the muzzle, the bulbous warhead and its nose cone
    p.add(lathe([(0.36 * s, 0.032 * s), (0.44 * s, 0.032 * s), (0.5 * s, 0.045 * s), (0.56 * s, 0.062 * s),
                 (0.66 * s, 0.064 * s), (0.72 * s, 0.05 * s), (0.8 * s, 0.026 * s), (0.86 * s, 0.01 * s),
                 (0.88 * s, 0.0)], seg=18), 'npf_warhead', T(0, y, 0), smooth=True)
    p.add(_sb(s, 0.04, 0.06, 0.12), 'gunmetal', T(0, 0.0, 0.0) @ R('X', -12), bevel=0.006)          # pistol grip
    p.add(_sb(s, 0.03, 0.05, 0.09), 'npf_tube', T(0, 0.0, 0.25 * s) @ R('X', -12), bevel=0.006)    # fore grip
    p.add(_sb(s, 0.045, 0.06, 0.16), 'gunmetal', T(0.055 * s, y + 0.035 * s, 0.1 * s), bevel=0.006)   # the optic block
    p.add(tube([(0.05 * s, y + 0.03 * s, -0.02 * s), (0.05 * s, y + 0.03 * s, -0.06 * s)], 0.016 * s, seg=10),
          'npf_glass', smooth=True)


def npf_rocket_carrier(p, scale=1.0):
    """A back carrier of three spare PG-7 rounds, warheads up, angled (straps at the origin, the load behind -Y)."""
    s = scale
    p.add(box(0.3 * s, 0.07 * s, 0.34 * s), 'npf_coyote', T(0, -0.1 * s, -0.12 * s), bevel=0.02)
    for i, x in enumerate((-0.09, 0.0, 0.09)):
        base = Vector((x * s, -0.17 * s, -0.25 * s))
        m = T(*base) @ R('Y', 12 - 12 * i)
        p.add(tube([(0, 0, 0), (0, 0, 0.3 * s)], 0.02 * s, seg=10), 'npf_tube', m, smooth=True)
        p.add(lathe([(0.28 * s, 0.03 * s), (0.34 * s, 0.065 * s), (0.42 * s, 0.065 * s), (0.5 * s, 0.03 * s),
                     (0.55 * s, 0.0)], seg=14), 'npf_warhead', m, smooth=True)
    for x in (-0.1 * s, 0.1 * s):
        path = [(x, -0.06 * s, 0.08 * s), (x, 0.02 * s, 0.13 * s), (x, 0.12 * s, 0.06 * s), (x * 1.1, 0.13 * s, -0.25 * s)]
        p.add(tube(path, 0.02 * s, seg=6, scale_xy=(1.0, 0.3)), 'npf_coyote', smooth=True)


def npf_pkm(p, scale=1.0):
    """A PKM general-purpose machine gun at the pistol grip (origin): a long thick receiver with its feed cover, the
    fluted barrel with a slotted flash hider and the bipod folded under it, a gas tube, the skeleton stock (an
    open frame), the 100-round box under the receiver with a short brass belt hanging from the feed."""
    s = scale
    p.add(_sb(s, 0.06, 0.1, 0.4), 'gunmetal', T(0, 0.015, 0.14 * s), bevel=0.006)                 # receiver
    p.add(_sb(s, 0.055, 0.04, 0.16), 'gunmetal', T(0, 0.075, 0.18 * s), bevel=0.006)              # feed cover
    p.add(tube([(0, 0.025, 0.3 * s), (0, 0.025, 0.86 * s)], 0.018 * s, seg=10), 'gunmetal', smooth=True)
    p.add(tube([(0, 0.025, 0.86 * s), (0, 0.025, 0.95 * s)], 0.026 * s, seg=10), 'gunmetal', smooth=True)
    p.add(tube([(0, 0.06, 0.3 * s), (0, 0.06, 0.52 * s)], 0.014 * s, seg=8), 'gunmetal', smooth=True)  # gas tube
    for x in (-0.014, 0.014):                                                                       # folded bipod
        p.add(tube([(x * s, 0.01, 0.8 * s), (x * s, 0.0, 0.5 * s)], 0.009 * s, seg=6), 'gunmetal', smooth=True)
    p.add(_sb(s, 0.04, 0.06, 0.11), 'npf_wood', T(0, -0.05, 0.0) @ R('X', -15), bevel=0.008)       # grip
    for path in (((0, 0.03, -0.04), (0, 0.04, -0.4)), ((0, -0.04, -0.06), (0, -0.07, -0.4)),
                 ((0, 0.04, -0.4), (0, -0.08, -0.4))):                                             # skeleton stock
        p.add(tube([tuple(v * s for v in q) for q in path], 0.016 * s, seg=8), 'npf_wood', smooth=True)
    p.add(_sb(s, 0.08, 0.13, 0.15), 'olive', T(0, -0.1 * s, 0.16 * s), bevel=0.012)              # the ammo box
    for i in range(7):                                                                             # the belt
        p.add(_sb(s, 0.024, 0.03, 0.02), 'npf_brass', T(0.035 * s, (-0.03 - 0.03 * i) * s, (0.24 + 0.012 * i) * s),
              bevel=0.002)


def npf_carbine(p, scale=1.0, suppressor=True):
    """A present-day special-operations carbine (M4/HK416 pattern) at the pistol grip (origin): a short, thick
    receiver and rail, a fat suppressor in a lighter grey, a red-dot optic with a magnifier, a weapon light, the
    magazine, a vertical grip, the collapsible stock."""
    s = scale
    p.add(_sb(s, 0.06, 0.095, 0.26), 'gunmetal', T(0, 0.015, 0.08 * s), bevel=0.008)              # receiver
    p.add(_sb(s, 0.065, 0.08, 0.22), 'polymer', T(0, 0.018, 0.32 * s), bevel=0.008)               # rail
    if suppressor:
        p.add(tube([(0, 0.02, 0.43 * s), (0, 0.02, 0.62 * s)], 0.032 * s, seg=14), 'npf_supp', smooth=True)
    p.add(_sb(s, 0.04, 0.05, 0.08), 'polymer', T(0, 0.09, 0.07 * s), bevel=0.008)                 # red dot
    p.add(tube([(0, 0.095, -0.03 * s), (0, 0.095, 0.02 * s)], 0.02 * s, seg=10), 'polymer', smooth=True)
    p.add(tube([(0.045, 0.0, 0.3 * s), (0.045, 0.0, 0.4 * s)], 0.017 * s, seg=10), 'polymer', smooth=True)
    mag = tube([(0, -0.03, 0.09 * s), (0, -0.13, 0.11 * s), (0, -0.2, 0.14 * s)], 0.026 * s, seg=4, scale_xy=(0.5, 1.3))
    p.add(mag, 'polymer', bevel=0.003)
    p.add(_sb(s, 0.04, 0.06, 0.11), 'polymer', T(0, -0.045, 0.0) @ R('X', -15), bevel=0.006)
    p.add(_sb(s, 0.045, 0.07, 0.08), 'polymer', T(0, -0.04, 0.3 * s), bevel=0.006)               # vertical grip
    p.add(tube([(0, 0.0, -0.06 * s), (0, 0.0, -0.2 * s)], 0.018 * s, seg=8), 'gunmetal', smooth=True)
    p.add(slab([(-0.07, -0.3), (0.04, -0.3), (0.035, -0.17), (-0.045, -0.17)], 0.05, 'X'), 'polymer', S(s),
          bevel=0.006)


def npf_ak(p, scale=1.0):
    """An AKM, unmistakable at 1x: props.ak built at its size (stamped receiver, red-brown wood, gas tube), a big
    dark curved magazine over its own, the slanted muzzle brake and the tall front sight, then scaled whole."""
    P.ak(p, scale=1.0)
    mag = [(0, -0.03, 0.2), (0, -0.11, 0.225), (0, -0.18, 0.28), (0, -0.23, 0.35)]
    p.add(tube(mag, 0.048, seg=4, scale_xy=(0.6, 1.35)), 'gunmetal', bevel=0.003)        # the big curved magazine
    p.add(tube([(0, 0.045, 0.27), (0, 0.045, 0.47)], 0.016, seg=8), 'gunmetal', smooth=True)        # the gas tube
    p.add(_sb(1.0, 0.026, 0.07, 0.05), 'gunmetal', T(0, 0.05, 0.6) @ R('X', 20), bevel=0.003)      # front sight
    p.add(tube([(0, 0.01, 0.62), (0, 0.016, 0.67)], 0.013, seg=8), 'gunmetal', smooth=True)        # muzzle brake
    import bmesh
    bmesh.ops.scale(p.bm, vec=(scale, scale, scale), verts=p.bm.verts)


def npf_patch(p, w=0.13, h=0.1, scale=1.0):
    """A civ-coloured cloth patch worn on the chest (chest gear: origin mid-chest, +Y out), stitched on a dark border."""
    s = scale
    p.add(box(w * s + 0.016, 0.012, h * s + 0.016), 'polymer', bevel=0.003)
    p.add(box(w * s, 0.016, h * s), 'tint', T(0, 0.004, 0), bevel=0.003, tint=True)


def npf_plastron(p, w=0.2, h=0.26, scale=1.0):
    """The white lapels of a habit-veste buttoned across the chest (chest gear: origin mid-chest, +Y out), with a
    row of brass buttons each side."""
    s = scale
    p.add(box(w * s, 0.012, h * s), 'canvas', T(0, 0, -0.04 * s), bevel=0.004)
    for x in (-0.06, 0.06):
        for k in range(4):
            p.add(ellipsoid(0.01, 0.006, 0.01, seg=8), 'brass', T(x * s, 0.008, (0.06 - 0.06 * k) * s), smooth=True)


def npf_limb_plate(p, length=0.4, radius=0.08, kind='plate', scale=1.0):
    """An armour piece for a limb, centred on the origin along Z: 'plate' a ridged cylinder of plates (thigh,
    shin, forearm), 'knee' a domed cop, 'gauntlet' a boxy armoured hand."""
    s = scale
    if kind == 'knee':
        p.add(ellipsoid(radius * s, radius * s, radius * 0.9 * s, seg=16), 'npf_armor', smooth=True)
        return
    if kind == 'gauntlet':
        p.add(box(radius * 1.5 * s, radius * 1.1 * s, length * s), 'npf_armor', bevel=0.012)
        p.add(box(radius * 1.6 * s, radius * 1.2 * s, length * 0.3 * s), 'gunmetal', T(0, 0, length * 0.35 * s), bevel=0.008)
        return
    L = length * s
    p.add(tube([(0, 0, -L / 2), (0, 0, L / 2)], [radius * s * 0.95, radius * s], seg=16), 'npf_armor', smooth=True)
    for k in (-0.3, 0.05, 0.38):                                                                      # plate ridges
        p.add(tube([(0, 0, k * L), (0, 0, k * L + 0.02 * s)], radius * s * 1.06, seg=16), 'gunmetal', smooth=True)


def npf_plate_rig(p, scale=1.0, patch=False):
    """The front of a plate carrier, worn on the chest (origin mid-chest, +Y out): three rifle-magazine pouches
    across the front flap, a radio pouch high on the left with its antenna stub, the cummerbund's edges."""
    s = scale
    p.add(_sb(s, 0.3, 0.035, 0.2), 'npf_ranger', T(0, 0.0, -0.08 * s), bevel=0.012)               # front flap
    for x in (-0.085, 0.0, 0.085):
        p.add(_sb(s, 0.075, 0.05, 0.11), 'npf_ranger', T(x * s, 0.035 * s, -0.11 * s), bevel=0.01)
        p.add(_sb(s, 0.075, 0.055, 0.025), 'npf_coyote', T(x * s, 0.036 * s, -0.05 * s), bevel=0.006)
    p.add(_sb(s, 0.07, 0.06, 0.12), 'polymer', T(0.13 * s, 0.03 * s, 0.07 * s), bevel=0.01)        # radio
    p.add(tube([(0.15 * s, 0.03 * s, 0.13 * s), (0.16 * s, 0.03 * s, 0.24 * s)], 0.008 * s, seg=6), 'polymer',
          smooth=True)
    for x in (-1, 1):
        p.add(_sb(s, 0.04, 0.12, 0.12), 'npf_ranger', T(x * 0.17 * s, -0.05 * s, -0.1 * s), bevel=0.01)
    if patch:                                                                                       # a civ patch
        p.add(_sb(s, 0.12, 0.02, 0.08), 'tint', T(-0.06 * s, 0.045 * s, 0.02 * s), bevel=0.004, tint=True)


def npf_ak_bayonet(p, scale=1.0):
    """The Kalashnikov (props.ak, built at its own size) with its knife bayonet fixed under the muzzle, the whole
    gun then scaled (props.ak's parts drift apart when built with a scale)."""
    P.ak(p, scale=1.0)
    p.add(box(0.018, 0.026, 0.06), 'gunmetal', T(0, -0.005, 0.62), bevel=0.003)
    blade = slab([(-0.014, 0.0), (0.014, 0.0), (0.012, 0.15), (0.0, 0.19), (-0.014, 0.15)], 0.014, 'X')
    p.add(blade, 'steel', T(0, -0.005, 0.64) @ S(1.1), bevel=0.002)
    import bmesh
    bmesh.ops.scale(p.bm, vec=(scale, scale, scale), verts=p.bm.verts)


def npf_shotgun(p, scale=1.0):
    """An old side-by-side double-barrelled shotgun at the wrist of the stock (origin): two blued barrels side by
    side, the action, the forend and a straight-grip stock in walnut."""
    s = scale
    for x in (-0.014, 0.014):
        p.add(tube([(x * s, 0.02, 0.05 * s), (x * s, 0.02, 0.74 * s)], 0.022 * s, seg=10), 'gunmetal', smooth=True)
    p.add(_sb(s, 0.012, 0.03, 0.66), 'gunmetal', T(0, 0.016, 0.4 * s))           # the rib between the barrels
    p.add(_sb(s, 0.044, 0.05, 0.11), 'gunmetal', T(0, 0.004, 0.06 * s), bevel=0.006)
    p.add(_sb(s, 0.06, 0.045, 0.26), 'npf_wood', T(0, -0.012, 0.26 * s), bevel=0.01)            # the fore-end
    p.add(slab([(-0.07, -0.38), (0.04, -0.38), (0.035, -0.04), (-0.02, 0.02), (-0.035, -0.1)], 0.055, 'X'),
          'npf_wood', S(s), bevel=0.01)


def npf_molotov(p, scale=1.0, lit=True):
    """A Molotov cocktail held by the neck (origin): a green bottle hanging below the fist, a rag stuffed in its
    neck sticking up out of the fist, lit."""
    s = scale
    p.add(lathe([(0.02, 0.012), (-0.04, 0.012), (-0.07, 0.035), (-0.1, 0.038), (-0.25, 0.038), (-0.26, 0.0)],
                seg=14), 'npf_bottle', S(s), smooth=True)
    p.add(tube([(0, 0, -0.02 * s), (0.015, 0.008, 0.05 * s), (0.0, 0.0, 0.09 * s)], [0.02 * s, 0.024 * s, 0.014 * s], seg=7),
          'npf_rag', smooth=True)
    if lit:
        p.add(ellipsoid(0.04, 0.04, 0.085, seg=12), 'npf_fire', T(0, 0, 0.14 * s) @ S(s), smooth=True)
    p.add(ellipsoid(0.026, 0.026, 0.05, seg=10), 'npf_fire_core', T(0, 0, 0.12 * s) @ S(s), smooth=True)   # the yellow heart


def npf_ppsh(p, scale=1.0):
    """A PPSh-41 at the wrist of the stock (origin): the perforated barrel jacket with its slanted compensator,
    the 71-round drum under the receiver, the wooden stock."""
    s = scale
    p.add(_sb(s, 0.046, 0.06, 0.2), 'gunmetal', T(0, 0.012, 0.08 * s), bevel=0.006)
    p.add(tube([(0, 0.02, 0.18 * s), (0, 0.02, 0.42 * s)], 0.026 * s, seg=12), 'npf_supp', smooth=True)
    for k in range(5):                                                                              # perforations
        z = (0.21 + 0.045 * k) * s
        p.add(tube([(0, 0.02, z), (0, 0.02, z + 0.014 * s)], 0.028 * s, seg=12), 'gunmetal', smooth=True)
    p.add(_sb(s, 0.04, 0.05, 0.04), 'gunmetal', T(0, 0.024, 0.43 * s) @ R('X', 20), bevel=0.004)
    drum = ellipsoid(0.07, 0.09, 0.09, seg=24)            # the 71-round drum, thick, under the receiver
    p.add(drum, 'npf_tube', T(0, -0.11 * s, 0.17 * s) @ S(s), smooth=True)
    p.add(slab([(-0.065, -0.36), (0.035, -0.36), (0.03, -0.04), (0.02, 0.17), (-0.01, 0.17), (-0.025, -0.02),
                (-0.04, -0.12)], 0.05, 'X'), 'npf_light_wood', S(s), bevel=0.008)


def npf_flame_gun(p, scale=1.0):
    """A flamethrower's gun (M2 pattern) at the rear grip (origin): a long wand with the fuel valve, a fore
    grip, the igniter cylinder at the nozzle, the hose dropping from the rear."""
    s = scale
    p.add(tube([(0, 0.03, -0.12 * s), (0, 0.03, 0.62 * s)], 0.03 * s, seg=10), 'gunmetal', smooth=True)
    p.add(tube([(0, 0.03, 0.5 * s), (0, 0.03, 0.66 * s)], 0.036 * s, seg=12), 'gunmetal', smooth=True)  # igniter
    p.add(ellipsoid(0.055, 0.055, 0.08, seg=12), 'npf_flame_matte', T(0, 0.03, 0.74 * s) @ S(s), smooth=True)   # an orange plume
    p.add(ellipsoid(0.03, 0.03, 0.035, seg=10), 'npf_flame_tip', T(0, 0.03, 0.8 * s) @ S(s), smooth=True)        # a short yellow core
    p.add(_sb(s, 0.034, 0.05, 0.1), 'npf_wood', T(0, -0.02, 0.0) @ R('X', -15), bevel=0.008)
    p.add(_sb(s, 0.034, 0.05, 0.09), 'npf_wood', T(0, -0.02, 0.32 * s) @ R('X', -15), bevel=0.008)
    p.add(_sb(s, 0.05, 0.07, 0.08), 'npf_tank', T(0, 0.03, 0.1 * s), bevel=0.008)                    # valve body
    p.add(tube([(0, 0.03, -0.12 * s), (0, -0.12 * s, -0.2 * s), (0, -0.3 * s, -0.36 * s), (0, -0.34 * s, -0.56 * s),
                (0, -0.22 * s, -0.74 * s)], 0.05 * s, seg=10), 'npf_hose', smooth=True)   # the fuel hose, hanging below the arm and back to the tanks


def npf_flame_tanks(p, scale=1.0, bands=False):
    """A flamethrower's back frame: two tall fuel tanks and the small pressure tank between them, on a pack
    frame with straps (straps' top at the origin, the load behind -Y)."""
    s = scale
    for x in (-0.085, 0.085):
        p.add(lathe([(-0.27, 0.0), (-0.26, 0.06), (0.14, 0.06), (0.16, 0.04), (0.17, 0.0)], seg=18), 'npf_tank',
              T(x * s, -0.13 * s, -0.12 * s) @ S(s), smooth=True)
        if bands:                                                                                   # civ tank bands
            p.add(lathe([(-0.08, 0.062), (0.0, 0.062)], seg=18), 'tint', T(x * s, -0.13 * s, -0.12 * s) @ S(s),
                  smooth=True, tint=True)
    p.add(ellipsoid(0.04, 0.04, 0.04, seg=12), 'npf_tank', T(0, -0.21 * s, -0.04 * s) @ S(s), smooth=True)
    p.add(box(0.3 * s, 0.03 * s, 0.05 * s), 'webbing', T(0, -0.07 * s, -0.3 * s), bevel=0.008)
    p.add(box(0.3 * s, 0.03 * s, 0.05 * s), 'webbing', T(0, -0.07 * s, 0.02 * s), bevel=0.008)
    for x in (-0.1 * s, 0.1 * s):
        path = [(x, -0.06 * s, 0.08 * s), (x, 0.02 * s, 0.13 * s), (x, 0.12 * s, 0.06 * s), (x * 1.1, 0.13 * s, -0.25 * s)]
        p.add(tube(path, 0.02 * s, seg=6, scale_xy=(1.0, 0.3)), 'webbing', smooth=True)


def npf_power_pack(p, scale=1.0, stripe=False):
    """A power-armour back pack: an armoured box with vents and two glowing cells (straps at the origin)."""
    s = scale
    p.add(box(0.38 * s, 0.2 * s, 0.46 * s), 'npf_armor', T(0, -0.15 * s, -0.1 * s), bevel=0.03)
    if stripe:                                                                                      # a civ panel
        p.add(box(0.3 * s, 0.02 * s, 0.14 * s), 'tint', T(0, -0.255 * s, 0.04 * s), bevel=0.005, tint=True)
    for x in (-0.09, 0.09):
        p.add(tube([(x * s, -0.2 * s, -0.25 * s), (x * s, -0.2 * s, 0.05 * s)], 0.03 * s, seg=12), 'gunmetal', smooth=True)
        p.add(lathe([(0.0, 0.032 * s), (0.03 * s, 0.032 * s)], seg=12), 'npf_glow', T(x * s, -0.2 * s, -0.05 * s), smooth=True)


def npf_pulse_rifle(p, scale=1.0):
    """A heavy near-future pulse rifle at the grip (origin): a slab-sided armoured body, a thick barrel shroud
    with coil rings, a glowing emitter at the muzzle, a top optic."""
    s = scale
    p.add(_sb(s, 0.1, 0.14, 0.5), 'npf_armor', T(0, 0.02, 0.18 * s), bevel=0.014)               # the boxy receiver
    p.add(tube([(0, 0.03, 0.42 * s), (0, 0.03, 0.74 * s)], 0.04 * s, seg=14), 'gunmetal', smooth=True)   # (thicker: a 1 px edge speck)
    for z in (0.48, 0.55, 0.62):
        p.add(lathe([(0.0, 0.04 * s), (0.025 * s, 0.04 * s)], seg=14), 'npf_armor', T(0, 0.03, z * s), smooth=True)
    p.add(lathe([(-0.01 * s, 0.036 * s), (0.05 * s, 0.034 * s)], seg=14), 'npf_glow', T(0, 0.03, 0.74 * s), smooth=True)
    p.add(_sb(s, 0.05, 0.07, 0.14), 'gunmetal', T(0, 0.09, 0.12 * s), bevel=0.008)   # the optic, seated on the body
    p.add(_sb(s, 0.04, 0.05, 0.11), 'gunmetal', T(0, -0.05, 0.0) @ R('X', -15), bevel=0.008)
    p.add(_sb(s, 0.08, 0.13, 0.14), 'gunmetal', T(0, -0.08, 0.24 * s), bevel=0.01)                   # power cell
    p.add(_sb(s, 0.082, 0.02, 0.1), 'npf_glow', T(0, -0.15 * s, 0.24 * s))                          # its glowing face
    p.add(slab([(-0.07, -0.26), (0.05, -0.26), (0.04, -0.06), (-0.03, -0.06)], 0.05, 'X'), 'npf_armor', S(s), bevel=0.01)


def npf_m1917(p, scale=1.0):
    """A Browning M1917A1 water-cooled machine gun on its low tripod (the tripod's head at the origin, on the
    ground plane's level minus its height; the barrel along +Y): the round water jacket, the receiver with the
    spade grips, the condenser hose to a can, the ammunition box on the left with a belt."""
    s = scale
    h = 0.42 * s
    # tripod: two front legs splayed forward, one rear trail
    for a in (35, -35):
        d = Vector((math.sin(math.radians(a)), math.cos(math.radians(a)), 0)) * 0.55 * s
        p.add(tube([(0, 0, h), (d.x, d.y, 0.0)], 0.022 * s, seg=6), 'gunmetal', smooth=True)
    p.add(tube([(0, 0, h), (0, -0.7 * s, 0.0)], 0.024 * s, seg=6), 'gunmetal', smooth=True)
    p.add(box(0.08 * s, 0.12 * s, 0.06 * s), 'gunmetal', T(0, 0, h), bevel=0.01)
    gz = h + 0.09 * s
    p.add(box(0.1 * s, 0.32 * s, 0.13 * s), 'gunmetal', T(0, -0.06 * s, gz), bevel=0.01)          # receiver
    p.add(tube([(0, 0.1 * s, gz), (0, 0.62 * s, gz)], 0.07 * s, seg=18), 'npf_tank', smooth=True)  # the fat water jacket
    for k in range(6):                                                                              # its corrugations
        y = (0.15 + 0.08 * k) * s
        p.add(tube([(0, y, gz), (0, y + 0.02 * s, gz)], 0.074 * s, seg=18), 'gunmetal', smooth=True)
    p.add(tube([(0, 0.62 * s, gz), (0, 0.72 * s, gz)], 0.016 * s, seg=8), 'gunmetal', smooth=True)
    for x in (-0.04, 0.04):                                                                         # spade grips
        p.add(tube([(x * s, -0.22 * s, gz + 0.02 * s), (x * s, -0.26 * s, gz - 0.06 * s)], 0.012 * s, seg=6),
              'npf_wood', smooth=True)
    p.add(box(0.1 * s, 0.2 * s, 0.12 * s), 'olive', T(0.13 * s, 0.0, h - 0.06 * s), bevel=0.008)  # ammo box
    for i in range(5):
        p.add(box(0.012 * s, 0.03 * s, 0.012 * s), 'npf_brass', T((0.09 - 0.018 * i) * s, 0.0, h + (0.02 + 0.015 * i) * s))
    p.add(tube([(0, 0.55 * s, gz - 0.05 * s), (-0.1 * s, 0.5 * s, 0.1 * s), (-0.25 * s, 0.35 * s, 0.05 * s)],
               0.02 * s, seg=6), 'polymer', smooth=True)                                            # condenser hose
    p.add(box(0.12 * s, 0.12 * s, 0.18 * s), 'olive', T(-0.28 * s, 0.32 * s, 0.09 * s), bevel=0.01)


def npf_charleville(p, length=1.52, scale=1.0):
    """The Charleville 1777 musket (props.musket) with its socket bayonet fixed: a triangular blade, thick
    enough to stay joined to the muzzle at 1x (the shared spike bayonet broke off as a speck)."""
    P.musket(p, length=length, scale=scale)
    z = length * scale - 0.38                     # the muzzle (props.musket's barrel ends at L - 0.38)
    p.add(lathe([(0.0, 0.016), (0.05, 0.015)], seg=10), 'steel', T(0, 0.01, z - 0.02), smooth=True)   # the socket
    # a round-sectioned spike that keeps its width from every side (a flat blade broke up edge-on)
    p.add(lathe([(0.0, 0.016), (0.24, 0.014)], seg=8), 'steel', T(0, 0.03, z + 0.02), smooth=True)
    p.add(ellipsoid(0.014, 0.014, 0.03, seg=8), 'steel', T(0, 0.03, z + 0.26), smooth=True)   # a rounded point


def npf_m14(p, scale=1.0):
    """An M14 at the wrist of the stock (origin): the walnut stock to the front band, the rear sight, the
    flash suppressor; the magazine under the receiver."""
    s = scale
    p.add(slab([(-0.055, -0.37), (0.035, -0.37), (0.035, -0.1), (0.02, 0.0), (0.018, 0.5), (-0.016, 0.5),
                (-0.02, 0.06), (-0.04, -0.08)], 0.044, 'X'), 'npf_wood', S(s), bevel=0.006)
    p.add(tube([(0, 0.022, 0.0), (0, 0.022, 0.78 * s)], 0.011 * s, seg=10), 'gunmetal', smooth=True)
    p.add(tube([(0, 0.022, 0.78 * s), (0, 0.022, 0.84 * s)], 0.015 * s, seg=10), 'gunmetal', smooth=True)
    p.add(_sb(s, 0.034, 0.05, 0.22), 'gunmetal', T(0, 0.02, 0.08 * s), bevel=0.005)
    p.add(_sb(s, 0.028, 0.09, 0.06), 'gunmetal', T(0, -0.05, 0.12 * s), bevel=0.004)
    p.add(_sb(s, 0.012, 0.03, 0.03), 'gunmetal', T(0, 0.05, 0.0), bevel=0.003)


def npf_satchel(p, scale=1.0, flap=False):
    """A demolition satchel at the hip (attach point at the origin, hanging along -Z); flap=True: its flap in the
    civ's colour."""
    s = scale
    p.add(box(0.08 * s, 0.22 * s, 0.17 * s), 'canvas', T(0, 0, -0.1 * s), bevel=0.015)
    p.add(box(0.09 * s, 0.225 * s, 0.12 * s), 'tint' if flap else 'webbing', T(0, 0, -0.06 * s), bevel=0.01, tint=flap)


def npf_bottle_bag(p, scale=1.0):
    """A canvas shoulder bag with spare bottles' necks showing (attach point at the origin)."""
    s = scale
    p.add(box(0.08 * s, 0.24 * s, 0.18 * s), 'canvas', T(0, 0, -0.11 * s), bevel=0.02)
    for y in (-0.06, 0.0, 0.06):
        p.add(tube([(0, y * s, -0.04 * s), (0, y * s, 0.03 * s)], 0.014 * s, seg=8), 'npf_bottle', smooth=True)


PROPS = {f.__name__: f for f in (npf_limb_plate, npf_plastron, npf_patch, npf_ak, npf_plate_rig, npf_rpg7, npf_rocket_carrier, npf_pkm, npf_carbine, npf_ak_bayonet, npf_shotgun,
                                  npf_molotov, npf_ppsh, npf_flame_gun, npf_flame_tanks, npf_power_pack,
                                  npf_pulse_rifle, npf_m1917, npf_charleville, npf_m14, npf_satchel, npf_bottle_bag)}


# ------------------------------------------------------------ hats (origin at the brow, +Y forward, Z up)

def hat_helmet_goggles(p, c, tinted, _, camo=True, band=False, band_up=False):
    """A covered PASGT-style helmet with goggles strapped round it above the brim; band: a civ-coloured band."""
    P.modern_helmet(p, camo=camo)
    if band and band_up:                                                                            # a civ crown band
        p.add(lathe([(0.075, 0.12), (0.125, 0.082)], seg=24, scale_xy=(1.04, 1.04)), 'tint', smooth=True, tint=True)
    elif band:                                                                                      # a broad civ band
        p.add(lathe([(-0.005, 0.146), (0.085, 0.13)], seg=24), 'tint', smooth=True, tint=True)
    p.add(lathe([(0.03, 0.142), (0.05, 0.142)], seg=24), 'polymer', smooth=True)                  # strap
    for x in (-0.045, 0.045):
        p.add(ellipsoid(0.04, 0.02, 0.026, seg=12), 'npf_glass', T(x, 0.135, 0.045), smooth=True)


def hat_fast(p, c, tinted, _, patch=False):
    """A high-cut ballistic helmet (FAST pattern): a dome cut high over the ears, side rails, the night-vision
    goggles flipped up on the front mount, ear-cup headset."""
    dome = lathe([(-0.005, 0.13), (0.06, 0.124), (0.12, 0.088), (0.16, 0.0)], seg=24)
    p.add(dome, 'tint' if tinted else 'npf_helmet_poly', S(1.0), smooth=True, tint=tinted)   # a civ helmet cover
    for x in (-1, 1):
        p.add(box(0.012, 0.09, 0.03), 'polymer', T(x * 0.128, 0.0, 0.04), bevel=0.004)             # rails
        p.add(ellipsoid(0.03, 0.045, 0.05, seg=12), 'polymer', T(x * 0.13, -0.01, -0.03), smooth=True)  # headset
    p.add(box(0.06, 0.035, 0.035), 'polymer', T(0, 0.135, 0.035), bevel=0.006)                      # mount, at the brow
    for x in (-0.028, 0.028):                                                                       # NVG up
        p.add(tube([(x, 0.155, 0.03), (x, 0.15, 0.085)], 0.019, seg=10), 'polymer', smooth=True)
        p.add(lathe([(0.0, 0.017), (0.006, 0.017)], seg=10), 'npf_glass', T(x, 0.15, 0.09), smooth=True)
    if patch:                                                                                       # civ patches
        for x in (-1, 1):
            p.add(box(0.012, 0.07, 0.05), 'tint', T(x * 0.13, -0.04, 0.075), bevel=0.004, tint=True)
    p.add(box(0.04, 0.03, 0.05), 'polymer', T(0, -0.13, 0.04), bevel=0.006)                        # battery


def _check(cell_a=22, cell_z=0.028, line=0.2):
    """A face-key function: a red check of thin lines on the shemagh's off-white."""
    def keys(f):
        c = f.calc_center_median()
        a = (math.atan2(c.y, c.x) / (2 * math.pi) * cell_a) % 1.0
        z = (c.z / cell_z) % 1.0
        return ('npf_shemagh_red', False) if (a < line or z < line) else None
    return keys


def hat_shemagh(p, c, tinted, _, tail_tint=False, band_tint=False):
    """A shemagh wound round the head, the face open: a close cloth dome over the skull in off-white with a thin
    red check, two wound folds round the brow and across the crown, the loose end hanging at the back."""
    prof = [(-0.035, 0.124), (0.0, 0.13), (0.045, 0.128), (0.085, 0.108), (0.115, 0.07), (0.13, 0.03), (0.134, 0.0)]
    fine = []
    for (z0, r0), (z1, r1) in zip(prof[:-1], prof[1:]):
        fine += [(z0 + (z1 - z0) * k / 4, r0 + (r1 - r0) * k / 4) for k in range(4)]
    dome = lathe(fine + [prof[-1]], seg=88)
    p.add(dome, 'npf_shemagh', smooth=True, keys=_check(line=0.3))
    p.add(lathe([(-0.015, 0.137), (0.045, 0.139)], seg=28), 'tint' if band_tint else 'npf_shemagh', R('X', -8),
          smooth=True, tint=band_tint)                                                              # the brow fold
    p.add(lathe([(0.0, 0.134), (0.012, 0.136)], seg=28), 'npf_shemagh_red', T(0, 0, 0.033) @ R('X', -8), smooth=True)
    p.add(lathe([(-0.015, 0.125), (0.02, 0.127)], seg=28), 'npf_shemagh', T(0, 0, 0.055) @ R('X', 28) @ R('Y', 10),
          smooth=True, keys=_check())
    tail = slab([(-0.07, 0.0), (0.07, 0.0), (0.07, -0.34), (-0.04, -0.36)], 0.022, 'Y')       # over the right shoulder
    p.add(tail, 'tint' if tail_tint else 'npf_shemagh', T(-0.07, -0.1, -0.02) @ R('Z', -50) @ R('X', 30), bevel=0.004,
          tint=tail_tint, keys=None if tail_tint else _check(cell_a=40, line=0.3))


def hat_headwrap(p, c, tinted, _, loose=False, band=False):
    """A plain cloth headwrap wound round the head (the civ's colour when tinted): a close dome, three wound
    folds stepping up the brow, the loose end tucked at the back."""
    key, t = ('tint', True) if tinted else ('hat', False)
    p.add(lathe([(-0.03, 0.126), (0.03, 0.13), (0.08, 0.112), (0.115, 0.075), (0.135, 0.03), (0.14, 0.0)], seg=28),
          key, smooth=True, tint=t)
    for i, (z, r, tilt) in enumerate(((-0.02, 0.138, -6), (0.02, 0.136, 4), (0.055, 0.126, 14))):
        p.add(lathe([(0.0, r), (0.028, r - 0.004)], seg=28), key, T(0, 0, z) @ R('X', tilt), smooth=True, tint=t)
    p.add(slab([(-0.045, 0.0), (0.045, 0.0), (0.035, -0.11), (-0.03, -0.12)], 0.018, 'Y'), key,
          T(0.02, -0.128, 0.0) @ R('X', 14), bevel=0.004, tint=t)
    if loose:
        # loose cloth: two fat wound folds standing proud of the dome, and a long loose end over the left shoulder
        for i, (z, r, tilt) in enumerate(((0.0, 0.152, -10), (0.045, 0.142, 16))):
            bk, bt = ('tint', True) if (band and i == 1) else (key, t)                          # band: the upper fold in civ
            p.add(tube([(r * math.cos(a), r * math.sin(a), 0.0) for a in [2 * math.pi * k / 24 for k in range(25)]],
                       0.026 if bt else 0.022, seg=8), bk, T(0, 0, z) @ R('X', tilt), smooth=True, tint=bt)
        p.add(slab([(-0.05, 0.0), (0.05, 0.0), (0.045, -0.3), (-0.02, -0.32)], 0.02, 'Y'), key,
              T(0.08, -0.1, -0.02) @ R('Z', 45) @ R('X', 28), bevel=0.005, tint=t)


def hat_flat_cap(p, c, tinted, _, band=False, band_at='crown'):
    """A cloth flat cap: a soft flat crown pulled forward over a short stiff peak."""
    key, t = ('tint', True) if tinted else ('hat', False)
    crown = ellipsoid(0.135, 0.15, 0.05, seg=20)
    up = band and band_at == 'crown'
    brow = band and band_at == 'brow'
    p.add(crown, 'tint' if up else key, T(0, 0.02, 0.07), smooth=True, tint=up or t)               # (crown: civ)
    p.add(lathe([(-0.005, 0.122), (0.06, 0.128)], seg=24), 'tint' if brow else key, smooth=True, tint=brow or t)   # (brow: civ)
    visor = slab([(-0.09, 0.0), (0.09, 0.0), (0.07, 0.06), (-0.07, 0.06)], 0.01, 'X')
    p.add(visor, key, T(0, 0.12, 0.01) @ R('Z', 90) @ R('X', -12), bevel=0.003, tint=t)


def hat_shako1806(p, c, tinted, plume_tinted):
    """The French 1806 infantry shako: a tall black felt cylinder flaring a little to the top, a leather
    top and peak, the brass lozenge plate, chinscales, white cords and a pompom."""
    p.add(lathe([(0.0, 0.122), (0.28, 0.13)], seg=24), 'hat', smooth=True)                         # tall, black
    p.add(lathe([(0.28, 0.13), (0.29, 0.13), (0.292, 0.0)], seg=24), 'leather_dark', smooth=True)    # the flat top
    p.add(lathe([(0.255, 0.131), (0.275, 0.132)], seg=24), 'brass', smooth=True)                    # the top band
    p.add(lathe([(-0.002, 0.122), (0.025, 0.124)], seg=24), 'leather_dark', smooth=True)
    visor = slab([(-0.1, 0.0), (0.1, 0.0), (0.08, 0.07), (-0.08, 0.07)], 0.008, 'X')
    p.add(visor, 'leather_dark', T(0, 0.12, 0.0) @ R('Z', 90) @ R('X', -15), bevel=0.003)
    p.add(slab([(0.0, 0.06), (0.055, 0.13), (0.0, 0.2), (-0.055, 0.13)], 0.008, 'Y'), 'brass', T(0, 0.13, 0.0))
    for x in (-1, 1):
        p.add(tube([(x * 0.12, 0.02, 0.03), (x * 0.1, 0.05, -0.08)], 0.012, seg=6), 'brass', smooth=True)
    p.add(tube([(-0.12, 0.08, 0.2), (0.0, 0.14, 0.1), (0.12, 0.08, 0.2)], 0.007, seg=6), 'canvas', smooth=True)
    key, t = ('tint', True) if plume_tinted else ('plume', False)
    p.add(ellipsoid(0.045, 0.04, 0.055, seg=12), key, T(0, 0.09, 0.33), smooth=True, tint=t)


def hat_m1_leaf(p, c, tinted, _, band=False):
    """The M1 helmet in its leaf-pattern cover with the elastic band (band=True: in the civ's colour), a bottle of
    insect repellent tucked in it."""
    P.modern_helmet(p, camo=True)
    if band:                                                                                        # a civ crown band
        p.add(lathe([(0.07, 0.125), (0.12, 0.09)], seg=24, scale_xy=(1.04, 1.04)), 'tint', smooth=True, tint=True)
    p.add(lathe([(0.01, 0.142), (0.04, 0.136)], seg=24), 'polymer', smooth=True)                    # the elastic band
    p.add(box(0.025, 0.02, 0.07), 'canvas', T(0.09, 0.1, 0.06) @ R('Z', 40), bevel=0.004)


def hat_exo(p, c, tinted, _, stripe=False):
    """A closed power-armour helmet: a rounded shell, a dark visor plate across the face, cheek plates and
    a crest seam."""
    dome = lathe([(-0.13, 0.12), (-0.04, 0.14), (0.04, 0.142), (0.11, 0.11), (0.17, 0.0)], seg=24)
    p.add(dome, 'npf_armor', S(1.0), smooth=True)
    p.add(slab([(-0.09, -0.06), (0.09, -0.06), (0.07, 0.02), (-0.07, 0.02)], 0.02, 'Y'), 'npf_glass',
          T(0, 0.138, 0.0) @ R('X', -8), bevel=0.005)
    for x in (-1, 1):
        p.add(box(0.03, 0.09, 0.1), 'gunmetal', T(x * 0.12, 0.04, -0.07), bevel=0.01)
    if stripe:                                                                                      # a civ crown stripe
        p.add(lathe([(0.02, 0.143), (0.08, 0.124)], seg=24), 'tint', smooth=True, tint=True)


HATS = {'npf_helmet_goggles': hat_helmet_goggles, 'npf_headwrap': hat_headwrap, 'npf_fast': hat_fast, 'npf_shemagh': hat_shemagh,
        'npf_flat_cap': hat_flat_cap, 'npf_shako1806': hat_shako1806, 'npf_m1_leaf': hat_m1_leaf, 'npf_exo': hat_exo}


def register():
    if not _BLENDER:
        return
    _register_defaults()
    for k, f in PROPS.items():
        P.BUILDERS.setdefault(k, f)
    for k, f in HATS.items():
        riders.HEADGEAR.setdefault(k, f)


# ------------------------------------------------------------ the units

# a modern soldier's build (as the stock Marine's remaster)
_BUILD = {'macro': {'muscle': 1.0, 'weight': 0.78, 'height': 0.77, 'proportions': 0.72}}
# the stock woodland camo (Marine)
_WOODLAND = ('camo', '#4a5a3c', '#55352d', '#909369')

# kneeling on the right knee, the left foot planted forward (as crews.KNEEL); no weight shift (it would stand him up)
# kneeling upright on both knees (thighs straight down, shins along the ground): compact, no lunge
_KNEEL2 = [('thigh_l', (0.1, 0.04, -1)), ('calf_l', (0.04, 1.0, -0.18)), ('foot_l', (0.0, 0.35, -1)),
           ('thigh_r', (-0.1, 0.04, -1)), ('calf_r', (-0.04, 1.0, -0.18)), ('foot_r', (0.0, 0.35, -1))]
_KNEEL = [('thigh_l', (0.15, -0.95, -0.25)), ('calf_l', (0.08, 0.05, -1)), ('foot_l', (0.3, -1, -0.2)),
          ('thigh_r', (-0.08, 0.02, -1)), ('calf_r', (-0.04, 1.0, -0.3)), ('foot_r', (0.0, 0.5, -1))]
# a US soldier of 1944 for the Battalion's team: the M1 with its net, the M1941 field jacket, canvas leggings,
# the civ colour on the crossed suspenders and the belt
_TEAM = dict(_BUILD, **{
    'colors': {'skin': '#cca682', 'hair': '#2a2018', 'olive': '#56563f', 'top': '#6e6a4e', 'bottom': '#4e4234',
               'belt': '#ffffff', 'boots': '#4a3020', 'leggings': '#857a56', 'olive_dark': '#5e5a44'},
    'headgear': 'modern_helmet', 'headgear_params': {'net': True}, 'brow_drop': 0.055, 'helmet': None,   # (no band: it sat on the eye line) 'hair': 'short', 'beard': False,
    'top': 'sleeved', 'neck': 'high', 'bottom': 'long', 'boots': True, 'wraps': 'boots', 'cloth_rough': 0.95,
    'gear': [('chest_strap', {'tint': True, 'width': 0.14, 'cross': True, 'offset': 0.03})],
    'bands': [('ankle', 'leggings', 0.16), ('upperarm', 'tint', 0.1)],
    'stance_name': 'ready', 'posture': False, 'elbow_r': 0.3, 'elbow_l': 0.3, 'pose_extra': _KNEEL2,
    'tint': ['belt'],
    'grime': 0.8, 'upper_bulk': 1.2,
})

ENTRIES = {
    'RPG Trooper': dict(_BUILD, **{
        # an anti-tank gunner of the 1990s-2000s: the RPG-7 loaded, carried on the right shoulder, muzzle up and
        # forward, both hands on its grips; three spare rounds in a carrier on his back; a covered helmet with
        # goggles; a khaki chest rig over a civ-coloured shirt with rolled sleeves; camouflage trousers
        'colors': {'skin': '#ae8864', 'hair': '#2a2018', 'olive': '#6a6648', 'top': '#8a7e5c', 'cuirass': '#6a6448',
                   'bottom': '#6a6448', 'belt': '#3a3426', 'boots': '#2a2018'},
        'headgear': 'npf_helmet_goggles', 'headgear_params': {'band': True}, 'brow_drop': 0.065, 'helmet': None, 'hair': 'short', 'beard': False,
        'top': 'tunic', 'sleeve_x': 0.36, 'neck': 'high',
        'cuirass': True, 'metal': [], 'cuirass_width': 0.15,
        'bottom': 'long', 'patterns': {'bottom': ('camo', '#4a4630', '#8a805c', '#5a4a32')},
        'boots': True, 'wraps': 'boots', 'cloth_rough': 0.95,
        'bands': [('upperarm', 'tint', 0.1), ('neck', 'tint', 0.1)],   # civ: the short sleeves, a neckerchief                 # civ: the short sleeves' cuffs and a chest patch
        'chest_gear': ('npf_patch', {'w': 0.2, 'h': 0.15}), 'chest_out': 0.22,   # and a chest patch
        'back': 'npf_rocket_carrier', 'back_params': {'scale': 1.15},
        'weapon': 'rpg', 'props': {'rpg': ('npf_rpg7', {'scale': 1.25})},
        # on the right shoulder, level, the grip before the shoulder, the cheek to the sight; turned out 20 degrees
        # so it stands out of the silhouette head-on and from behind
        'stance_name': 'ready', 'hand_r_at': (0.0, -0.2, -0.08), 'elbow_r': 0.35,
        'weapon_dir': (-0.5, -1.0, -0.04),   # out 27 degrees (dir 2), level from behind 'edge_dir': (0.0, 0.0, 1.0),
        'two_hands': True, 'grip2': -0.3, 'elbow_l': 0.4,
        'tint': [], 'fit_scale': 0.89,
        'grime': 0.75, 'upper_bulk': 1.25,
    }),
    'Special Forces': dict(_BUILD, **{
        # a present-day special operator: a black high-cut helmet with the NVGs flipped up and a headset; a
        # beard; a ranger-green plate carrier with magazines over civ-coloured sleeves (as the stock Marine);
        # camouflage trousers with knee pads; an assault pack; a suppressed carbine with optics at the low ready
        'colors': {'skin': '#d0aa86', 'hair': '#4a3624', 'beard': '#6e543c', 'top': '#5e6250', 'helmet': '#2a2b2c', 'cuirass': '#4e5242',
                   'bottom': '#6a6c5a', 'belt': '#34342e', 'boots': '#3a3630', 'kneepad': '#2e2e2a', 'gloves': '#3a3c32',
                   'olive_dark': '#4e5242', 'olive': '#5a5e4a'},
        'headgear': 'npf_fast', 'headgear_params': {'patch': False}, 'brow_drop': 0.065, 'helmet': None, 'hair': 'short', 'beard': True, 'beard_length_short': 0.014,
        'top': 'sleeved', 'neck': 'high',
        'cuirass': True, 'metal': [], 'cuirass_width': 0.19, 'cuirass_offset': 0.03,
        'bottom': 'long', 'patterns': {'bottom': ('camo', '#4e5242', '#82866c', '#40403a'), 'top': ('camo', '#4e5242', '#82866c', '#40403a')},
        'boots': True, 'wraps': 'boots', 'cloth_rough': 0.95,
        'bands': [('knee', 'kneepad', 0.09), ('wrist', 'gloves', 0.2), ('upperarm', 'tint', 0.06), ('neck', 'tint', 0.07)],   # civ: a sleeve band, the helmet's patches, a rig patch
        'back': 'mil_backpack', 'back_params': {'size': 0.85},
        'hip_gear': [('pistol_holster', {'scale': 1.2}, 'r')],
        'chest_gear': ('npf_plate_rig', {'scale': 1.15, 'patch': True}), 'chest_out': 0.19,
        'weapon': 'carbine', 'props': {'carbine': ('npf_carbine', {'scale': 1.25})},
        'stance_name': 'ready', 'hand_r_at': (0.04, -0.2, -0.05), 'elbow_r': 0.55,   # at the shoulder
        'weapon_dir': (0.25, -1.0, 0.0), 'edge_dir': (0.0, 0.2, 1.0),
        'two_hands': True, 'grip2': -0.3, 'elbow_l': 0.5,
        'tint': ['helmet'], 'fit_scale': 0.9,   # civ: the helmet cover, a sleeve band, a rig patch
        'grime': 0.6, 'upper_bulk': 1.2, 'bulk_final': 1.12,   # sleeve-width arms (a1: the blocks read as pads)
    }),
    'Insurgent': dict(_BUILD, **{
        # an urban insurgent of the 2000s: a red-and-white shemagh wound round the head, the face open; a
        # civ-coloured jacket under a khaki chicom chest rig; jeans; trainers; a PKM at the hip
        'macro': {'muscle': 0.9, 'weight': 0.6, 'height': 0.75, 'proportions': 0.74},
        'race': {'caucasian': 0.5, 'african': 0.2, 'asian': 0.3},
        'colors': {'skin': '#c09a74', 'hair': '#1a120c', 'top': '#3a3e40', 'cuirass': '#7e7454', 'scarf': '#b4a484', 'veil': '#b4a484',
                   'bottom': '#3e4a5e', 'belt': '#2a241c', 'boots': '#8a8680', 'shoes': '#8a8680',
                   'npf_shemagh': '#b4a484', 'npf_shemagh_red': '#8a2a22'},
        'headgear': 'npf_shemagh', 'headgear_params': {'tail_tint': True, 'band_tint': True}, 'headgear_scale': 0.8,
        'veil': True, 'veil_drop': 0.22,   # the shemagh's face wrap over the mouth and chin
        'brow_drop': 0.05, 'helmet': None, 'hair': 'short', 'beard': True, 'beard_length_short': 0.006,
        'top': 'sleeved', 'neck': 'high',
        'cuirass': True, 'metal': [], 'cuirass_width': 0.19,
        'bottom': 'long', 'wraps': 'shoes', 'sandals': False, 'cloth_rough': 0.95,
        'weapon': 'pkm', 'props': {'pkm': ('npf_pkm', {'scale': 1.4})},
        'stance_name': 'hip_hold', 'gun_dir': (-0.3, -1.0, 0.25), 'fore_end': 0.4,   # out from the hip, clear of the body
        'bands': [('neck', 'scarf', 0.08), ('upperarm', 'tint', 0.1), ('waist', 'tint', 0.11)],   # + a civ sash (the patch hides behind the arms)
        'chest_gear': ('npf_patch', {'w': 0.24, 'h': 0.16}), 'chest_out': 0.3,   # (at 0.24 it sat inside the rig)   # the shemagh's drape; civ: an armband and the shemagh's tail
        'tint': [], 'fit_scale': 0.93,
        'grime': 0.7, 'upper_bulk': 1.15,
    }),
    'Militant': dict(_BUILD, **{
        # a fighter in a civ-coloured headwrap, a beard, a long khaki shirt over camouflage trousers, a
        # crossbelt, an AK with its bayonet fixed, held upright in the right fist
        'macro': {'muscle': 0.9, 'weight': 0.58, 'height': 0.76, 'proportions': 0.74},
        'race': {'caucasian': 0.55, 'african': 0.15, 'asian': 0.3},
        'colors': {'skin': '#c4a07c', 'hair': '#1e1610', 'beard': '#5a4430', 'hat': '#8a7e66', 'top': '#a29470', 'bottom': '#56603a', 'belt': '#3a2a1a',
                   'boots': '#3a2a1a'},
        'headgear': 'npf_headwrap', 'headgear_params': {'loose': True, 'band': True}, 'brow_drop': 0.07, 'helmet': None, 'hair': 'short', 'beard': True, 'beard_style': 'goatee', 'beard_length_short': 0.012,
        'top': 'sleeved', 'neck': 'high', 'coat_length': 0.3, 'coat_flare': 1.1,
        'bottom': 'long', 'patterns': {'bottom': ('camo', '#78843e', '#2e3a1a', '#6a5a30')},
        'boots': True, 'wraps': 'boots', 'cloth_rough': 0.95,
        'gear': [('chest_strap', {'color': '#4a3624', 'width': 0.07})],
        'bands': [('neck', 'tint', 0.06), ('waist', 'tint', 0.16)],   # civ: a scarf low at the neck (a wide one read as a face veil), a sash
        'weapon': 'ak', 'props': {'ak': ('npf_ak', {'scale': 1.45})},
        # the same diagonal across the chest in every direction, the gun's side (not its edge) to the front
        'stance_name': 'ready', 'hand_r_at': (0.06, -0.2, -0.44), 'elbow_r': 0.5,
        'weapon_dir': (0.78, -0.3, 0.55), 'edge_dir': (-0.55, 0.0, 0.83), 'two_hands': True, 'grip2': -0.3, 'elbow_l': 0.5,
        'tint': [], 'fit_scale': 0.8,
        'grime': 0.75, 'upper_bulk': 1.15,
    }),
    'Partisans': dict(_BUILD, **{
        'fit_scale': 0.88,
        # a resistance fighter of 1941-44: a dark flat cap, a long dark civilian coat to the knee, a civ-coloured
        # armband and scarf, a leather bandolier, tall boots, a PPSh with its drum held across the body
        'macro': {'muscle': 0.85, 'weight': 0.5, 'height': 0.76, 'proportions': 0.74},
        'race': {'caucasian': 0.9, 'african': 0.0, 'asian': 0.1},
        'colors': {'skin': '#cca682', 'canvas': '#a08a60', 'hair': '#2a2018', 'hat': '#36322e', 'top': '#6a6556', 'bottom': '#4a463e',
                   'belt': '#6a4626', 'boots': '#221c18', 'pouch': '#7a5230', 'npf_wood': '#9a6a3c'},
        'headgear': 'npf_flat_cap', 'headgear_params': {'band': True}, 'helmet': None, 'hair': 'short', 'beard': False,
        'top': 'sleeved', 'neck': 'high', 'coat_length': 0.6, 'coat_flare': 1.15,
        'bottom': 'long', 'boots': True, 'wraps': 'boots', 'cloth_rough': 0.95,
        'gear': [('chest_strap', {'color': '#7a5a3a', 'width': 0.07, 'offset': 0.03})],   # a lighter leather bandolier
        'bands': [('upperarm', 'tint', 0.06), ('neck', 'tint', 0.06), ('waist', 'belt', 0.06)],   # civ: a flat armband, a low scarf, the cap's crown; a brown belt
        'hip_gear': [('haversack', {}, 'l'), ('cartridge_box', {'scale': 1.2}, 'r')],   # the bread bag and a pouch
        'pockets': '#3a3530', 'pocket_z': 1.0,
        'hem_bands': [('coat', '#4a463c', 0.05)],
        'patterns': {'top': ('camo', '#5e5a4c', '#76705e')},   # quilted, mottled field-grey wool
        'weapon': 'smg', 'props': {'smg': ('npf_ppsh', {'scale': 1.3})},
        'stance_name': 'hip_hold', 'gun_dir': (0.3, -1.0, 0.15), 'fore_end': 0.32,   # across the hips, the drum hanging under
        'tint': [],
        'grime': 0.8, 'upper_bulk': 1.1,
    }),
    'Rebel': dict(_BUILD, **{
        'fit_scale': 1.0,
        # a townsman in the 1956 Budapest uprising: a flat cap, a civ-coloured neckerchief and waistcoat under an
        # open dark jacket, the tricolour on his arm, worn trousers and shoes; an old double-barrelled shotgun in
        # his right hand and a lit Molotov cocktail in his left; a bag of bottles at his hip
        'macro': {'muscle': 0.85, 'weight': 0.55, 'height': 0.75, 'proportions': 0.74}, 'age': 0.45,
        'race': {'caucasian': 0.95, 'african': 0.0, 'asian': 0.05},
        'colors': {'skin': '#c09a76', 'cuirass': '#3a3430', 'hair': '#5a5650', 'hat': '#3e3a34', 'top': '#2e2824', 'bottom': '#6e6454',
                   'belt': '#2e2218', 'shoes': '#2a201a', 'boots': '#2a201a', 'canvas': '#8e7e58'},
        'headgear': 'npf_flat_cap', 'headgear_params': {'band': True, 'band_at': 'brow'},   # (final B2) 'helmet': None, 'hair': 'short', 'beard': False,
        'top': 'sleeved', 'neck': 'high',
        'cuirass': True, 'metal': [], 'cuirass_width': 0.16,
        'bottom': 'long', 'wraps': 'shoes', 'sandals': False, 'cloth_rough': 0.95,
        'bands': [('neck', 'tint', 0.15), ('upperarm', 'tint', 0.1), ('waist', 'belt', 0.05)],   # a big neckerchief, a broad flat armband   # civ: the neckerchief and the tricolour armband (patch)
        'shoulder_patch': ('l', 'tint'), 'patch_size': 0.14,
        'hip_gear': [('npf_bottle_bag', {'scale': 1.2}, 'l')],
        'weapon': 'shotgun', 'props': {'shotgun': ('npf_shotgun', {'scale': 1.15})},
        'stance_name': 'ready', 'hand_r_at': (0.04, -0.24, -0.42), 'elbow_r': 0.5,   # the shotgun diagonal across the body
        'weapon_dir': (0.55, -0.45, 0.7), 'edge_dir': (0.0, -1.0, 0.0),
        'hand_l_at': (0.08, -0.32, -0.3), 'elbow_l': 0.55,
        'offhand': 'npf_molotov', 'offhand_mesh': ('npf_molotov', {'scale': 1.5}), 'offhand_dir': (0.1, -0.1, 1.0),
        'tint': [],
        'grime': 0.8, 'upper_bulk': 1.1,
    }),
    'Skirmisher': dict(_BUILD, **{
        'fit_scale': 1.03,
        # a US rifleman of the 1960s: the M1 helmet in its leaf-pattern cover with its band; the jungle jacket in
        # the civ's colour, sleeves rolled; an olive towel at the neck; the M1956 load-bearing suspenders with
        # pouches, canteen and butt pack; jungle boots; the M14 at the low ready
        'colors': {'skin': '#c09a76', 'top': '#5e6a46', 'hair': '#3a2a1c', 'olive': '#6e7a4c', 'bottom': '#5e684a', 'belt': '#5e5e42',
                   'boots': '#161514', 'olive_dark': '#5e6448', 'towel': '#6e7650'},
        'headgear': 'npf_m1_leaf', 'headgear_params': {'band': True}, 'brow_drop': 0.065, 'helmet': None, 'hair': 'short', 'beard': False,
        'top': 'sleeved', 'neck': 'high',   # OG sleeves rolled to mid-forearm
        'bottom': 'long', 'boots': True, 'wraps': 'boots', 'cloth_rough': 0.95,
        'gear': [('chest_strap', {'color': '#5e5e42', 'width': 0.06, 'cross': True, 'offset': 0.03})],   # the M1956 suspenders
        'bands': [('neck', 'tint', 0.1), ('upperarm', 'tint', 0.06), ('wrist', 'top', 0.1)],   # civ: the towel as a scarf   # civ: the helmet band and a flat armband; the rolled cuffs
        'hip_gear': [('canteen', {'scale': 1.3}, 'r'), ('cartridge_box', {'scale': 1.2}, 'l'),
                     ('cartridge_box', {'scale': 1.5}, 'back')],
        'weapon': 'm14', 'props': {'m14': ('npf_m14', {'scale': 1.3})},
        # port arms: the same diagonal across the chest in every direction, the rifle's side to the front
        'stance_name': 'ready', 'hand_r_at': (0.08, -0.16, -0.45), 'elbow_r': 0.5,   # R4: muzzle at the left shoulder, butt at the right hip
        'weapon_dir': (0.7, -0.12, 0.7), 'edge_dir': (-0.55, 0.0, 0.83), 'two_hands': True, 'grip2': -0.34, 'elbow_l': 0.5,
        'tint': [],
        'grime': 0.8, 'upper_bulk': 1.2, 'bulk_final': 1.15,
    }),
    'Militia': dict(_BUILD, **{
        'fit_scale': 0.84,
        # a French line fusilier of 1806-12: the 1806 shako with its brass plate and pompom; the habit-veste in the
        # civ's colour with red collar and epaulettes; white crossbelts; white breeches, black gaiters; the
        # calfskin knapsack; the Charleville musket with its bayonet on the shoulder
        'macro': {'muscle': 0.9, 'weight': 0.6, 'height': 0.74, 'proportions': 0.72},
        'race': {'caucasian': 0.95, 'african': 0.0, 'asian': 0.05},
        'colors': {'skin': '#c49c78', 'top': '#4a5468', 'hair': '#2a1e14', 'hat': '#121212', 'bottom': '#d2ccbc', 'belt': '#e4e0d6',
                   'boots': '#161412', 'facings': '#a02c26', 'olive_dark': '#8a6a46', 'olive': '#7a7068',
                   'plume': '#a02c26', 'wood': '#6a4426', 'steel': '#a8aaae', 'canvas': '#e4e0d6'},
        'headgear': 'npf_shako1806', 'brow_drop': 0.065, 'helmet': None, 'hair': 'short', 'beard': False,
        'top': 'sleeved', 'neck': 'high', 'coat_length': 0.5, 'coat_flare': 1.1,   # the tails behind
        'bottom': 'breeches', 'boots': True, 'wraps': 'boots', 'cloth_rough': 0.9,
        'gear': [('chest_strap', {'color': '#e4e0d6', 'width': 0.07, 'cross': True, 'offset': 0.03})],
        'bands': [('neck', 'tint', 0.08), ('wrist', 'tint', 0.08)],   # civ: collar and cuffs
        'hem_bands': [('coat', 'tint', 0.1)],                    # and the turnbacks
        'back': 'mil_backpack', 'back_params': {'size': 1.0},
        'weapon': 'musket', 'props': {'musket': ('npf_charleville', {'length': 1.3, 'scale': 1.15})},
        'stance_name': 'shoulder_arms', 'gun_dir': (-0.08, 0.3, 0.95),   # near upright on the shoulder
        'tint': ['plume'],
        'grime': 0.6, 'upper_bulk': 1.15,
    }),
    'Combat Engineer': dict(_BUILD, **{
        'fit_scale': 0.93,
        # an assault engineer with a flamethrower (the mod's 'americanFT'): the fuel tanks on a back frame, an M1
        # helmet with goggles, a flak vest, a civ-coloured field shirt, a demolition satchel at the hip, the
        # flame gun held at the hip in both hands, its igniter forward
        'colors': {'skin': '#c09a76', 'top': '#6a6a50', 'hair': '#2a2018', 'olive': '#7a7658', 'cuirass': '#6e7458', 'bottom': '#8a8466',
                   'belt': '#4a4836', 'boots': '#4a3826', 'canvas': '#7e7a5c', 'webbing': '#4a4836'},
        'headgear': 'npf_helmet_goggles', 'brow_drop': 0.065, 'headgear_params': {'camo': False, 'band': True, 'band_up': True}, 'helmet': None,
        'hair': 'short', 'beard': False,
        'top': 'sleeved', 'neck': 'high',
        'cuirass': True, 'metal': [], 'cuirass_width': 0.19, 'cuirass_offset': 0.03,
        'bottom': 'long', 'patterns': {'bottom': ('camo', '#74704e', '#a69e7c', '#5e5a40')},
        'boots': True, 'wraps': 'boots', 'cloth_rough': 0.95,
        'bands': [('knee', 'belt', 0.08), ('waist', 'tint', 0.09)],   # civ: a sash at the belt (reads from behind)
        'gear': [('chest_strap', {'tint': True, 'width': 0.07, 'offset': 0.045})],   # and a civ rig strap (dir 3)
        'shoulder_patch': ('r', 'tint'), 'patch_size': 0.12, 'patch_out': 0.016,   # a flat civ patch on the right sleeve (dirs 3-4)
        'chest_gear': ('npf_patch', {'w': 0.32, 'h': 0.09}), 'chest_out': 0.3,   # and a broad civ stripe across the vest
        'back': 'npf_flame_tanks', 'back_params': {'scale': 1.5, 'bands': True},
        'hip_gear': [('npf_satchel', {'scale': 2.2, 'flap': True}, 'l')],
        'weapon': 'flame', 'props': {'flame': ('npf_flame_gun', {'scale': 1.4})},
        'stance_name': 'hip_hold', 'gun_dir': (-0.35, -1.0, 0.05), 'fore_end': 0.42,   # out to the side
        'tint': [],   # civ: the satchel's flap and the helmet band
        'grime': 0.8, 'upper_bulk': 1.2,
    }),
    'Super Trooper': dict(_BUILD, **{
        'fit_scale': 0.86,
        # near-future power armour: matte gunmetal plates over a dark suit, the chest and back plates in the civ's
        # colour, broad plated pauldrons, armoured shins, a closed helmet with a dark visor, a power pack on the
        # back, a heavy pulse rifle with a glowing emitter held at the hip
        'macro': {'muscle': 1.0, 'weight': 0.9, 'height': 0.85, 'proportions': 0.7},
        'race': {'caucasian': 0.4, 'african': 0.3, 'asian': 0.3},
        'colors': {'skin': '#ae8864', 'hair': '#2a2018', 'top': '#4a4844', 'bottom': '#2e2d29', 'cuirass': '#5a5650',
                   'belt': '#26251f', 'boots': '#26251f', 'greaves': '#5a5650', 'gloves': '#2a2a28'},
        'headgear': 'npf_exo', 'headgear_params': {'stripe': True}, 'helmet': None, 'hair': None, 'hair_asset': None, 'beard': False,
        'top': 'sleeved', 'neck': 'high',
        'cuirass': True, 'cuirass_bands': 3, 'plate_metallic': 0.55, 'plate_roughness': 0.45,
        'cuirass_offset': 0.04, 'cuirass_width': 0.2,
        'pauldrons': True, 'pauldron_offset': 0.03, 'pauldron_x': 0.12,   # plated in the suit's grey, not the civ colour
        'bottom': 'long', 'boots': True, 'wraps': 'greaves', 'cloth_rough': 0.9,
        # (c4) real armour on the limbs: plated thighs, knee cops, shin plates, forearm plates and gauntlets
        'npf_armour': [('thigh_l', 'plate', {'length': 0.36, 'radius': 0.105}), ('thigh_r', 'plate', {'length': 0.36, 'radius': 0.105}),
                       ('calf_l', 'plate', {'length': 0.34, 'radius': 0.085}), ('calf_r', 'plate', {'length': 0.34, 'radius': 0.085}),
                       ('calf_l', 'knee', {'radius': 0.075, 'at': 0.02}), ('calf_r', 'knee', {'radius': 0.075, 'at': 0.02}),
                       ('lowerarm_l', 'plate', {'length': 0.22, 'radius': 0.07}), ('lowerarm_r', 'plate', {'length': 0.22, 'radius': 0.07}),
                       ('hand_l', 'gauntlet', {'length': 0.12, 'radius': 0.06, 'at': 0.45}), ('hand_r', 'gauntlet', {'length': 0.12, 'radius': 0.06, 'at': 0.45})],
        'long_gloves': ('elbow', 'greaves'),   # grey gauntlets over the hands
        'shoulder_patch': ('r', 'tint'), 'patch_size': 0.13, 'patch_out': 0.035, 'patch_t': 0.25,   # a civ shoulder plate (dir 4)
        'bands': [('knee', 'greaves', 0.3), ('wrist', 'greaves', 0.3), ('upperarm', 'greaves', 0.1), ('waist', 'tint', 0.1), ('ankle', 'tint', 0.07)],   # a civ belt plate and boot bands   # plated thighs, gauntlets (grey: 'cuirass' bands took the civ tint)
        'back': 'npf_power_pack', 'back_params': {'scale': 1.4, 'stripe': True},
        'weapon': 'pulse', 'props': {'pulse': ('npf_pulse_rifle', {'scale': 1.35})},
        'stance_name': 'hip_hold', 'gun_dir': (0.2, -1.0, 0.08), 'fore_end': 0.3,
        'tint': ['cuirass'],
        'grime': 0.4, 'upper_bulk': 1.35,
    }),
    'Battalion': {
        # a US machine-gun team of 1944 in action (the mod's 'americanHMG'): the water-cooled Browning M1917A1 on
        # its low tripod, the gunner kneeling behind it, both hands on the spade grips, the loader kneeling at its
        # left by the ammunition box, feeding the belt; M1 helmets with nets, M1941 field jackets, canvas
        # leggings; the civ colour on their crossed suspenders, belts, armbands and helmet bands
        'fit_scale': 1.0,
        'team': [
            dict(_TEAM, at=(0.0, -0.46), yaw=0.0, hand_r_at=(0.06, -0.38, -0.36), hand_l_at=(-0.06, -0.38, -0.36)),
            dict(_TEAM, at=(1.29, 0.08), yaw=100.0, upper_bulk=1.15,   # (c4) 67.5 degrees off the gunner, halfway between camera axes: no view lines the men up
                 colors=dict(_TEAM['colors'], skin='#c8a27e'),
                 hand_r_at=(0.06, -0.34, -0.5), hand_l_at=(-0.06, -0.34, -0.5),
                 hip_gear=[('canteen', {'scale': 1.3}, 'back')]),
        ],
        'ground_props': [('npf_m1917', {'scale': 1.45}, (0.0, 0.3), 0.0)],
        'colors': {'olive': '#5a5c44', 'npf_tank': '#4e5040'},
    },
}




# Critic round a1 (both critics, every unit): the figures read slimmer than the stock Legionary and Archer. Every
# soldier takes the stock foot build (Legionary: muscle 1.0, weight 0.85, proportions 0.68; heavier still, as modern
# kit is bulky), thick upper arms and baggier trousers.
_STOCK_BUILD = {'muscle': 1.0, 'weight': 0.92, 'height': 0.72, 'proportions': 0.64}


def _stock_build(e):
    if e.get('team'):
        e['team'] = [_stock_build(dict(m)) for m in e['team']]
        return e
    macro = dict(_STOCK_BUILD)
    if e.get('macro', {}).get('height', 0) > 0.8:        # the power armour keeps its height
        macro.update(height=e['macro']['height'], weight=0.95)
    e['macro'] = macro
    e['upper_bulk'] = e.get('bulk_final') or max(e.get('upper_bulk', 1.0), 1.24)   # (b1: 1.32 read as Popeye arms)
    if e.get('bottom') == 'long':
        e.setdefault('trouser_offset', 0.02)
    return e


ENTRIES = {k: _stock_build(dict(v)) for k, v in ENTRIES.items()}


def entry(unit):
    """The unit's full entry (a copy)."""
    import copy
    return copy.deepcopy(ENTRIES[unit])


def build(e):
    """The posed, dressed and equipped unit of entry `e`: a lone soldier, or a team ('team': [entries], each with
    'at': (x, y) metres in the unit's frame and 'yaw' degrees) under one root. Bands (armbands, scarves, sashes)
    are laid flush on the cloth (FLAT_BAND), not standing off it as garments.band's default does."""
    import garments as G
    orig = G.band

    def flat(*a, **k):
        k.setdefault('offset', FLAT_BAND)
        return orig(*a, **k)
    G.band = flat
    try:
        return _build(e)
    finally:
        G.band = orig


FLAT_BAND = 0.016     # metres off the skin: just over the sleeve shell (0.008 + 0.004 thick), so an armband is flush


def _armour(e, root):
    """The entry's limb armour ('npf_armour': [(bone, kind, params)]): each piece centred on its posed bone (a
    fraction 'at' along it), along the bone, parented to it."""
    import human as H
    arm = next(o for o in root.children_recursive if o.type == 'ARMATURE')
    H._update()
    for bone, kind, params in e.get('npf_armour', []):
        params = dict(params)
        t = params.pop('at', 0.5)
        pb = arm.pose.bones[bone]
        h, tl = arm.matrix_world @ pb.head, arm.matrix_world @ pb.tail
        obj = P.make('npf_limb_plate', e.get('colors', {}), kind=kind, **params)
        H.hold(obj, arm, bone, h.lerp(tl, t), up=tuple((tl - h).normalized()), forward=(0, 0, 1), center=True)
    H._update()


def _build(e):
    import units
    if not e.get('team'):
        root = units.build_footman(e)
        if e.get('npf_armour'):
            _armour(e, root)
        return root
    import human as H
    root = bpy.data.objects.new('TeamRoot', None)
    bpy.context.scene.collection.objects.link(root)
    for i, m in enumerate(e['team']):
        me = dict(m)
        at, yaw = me.pop('at', (0.0, 0.0)), me.pop('yaw', 0.0)
        r = units.build_footman(me)
        r.parent = root
        r.location = (at[0] + r.location.x, at[1] + r.location.y, r.location.z)
        r.rotation_euler.z += math.radians(yaw)
    for gp in e.get('ground_props', []):
        name, params = gp[0], (gp[1] if len(gp) > 1 else {})
        x, y = gp[2] if len(gp) > 2 else (0.0, 0.0)
        obj = P.make(name, e.get('colors', {}), **params)
        obj.parent = root
        obj.location = (x, y, 0.0)
        obj.rotation_euler = (0.0, 0.0, math.radians(gp[3] if len(gp) > 3 else 0.0))
    H._update()
    return root
