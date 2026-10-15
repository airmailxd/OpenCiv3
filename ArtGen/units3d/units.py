"""Builds a human unit in Blender from its catalog entry (Blender side).

The catalog is the old procedural remake's (ArtGen/units/catalog): the same
vocabulary of build, clothes, gear, colors and tinted parts, written down from
each Civ3 original. build_footman() turns an entry into an MPFB body dressed
with garments.py and equipped with props.py, posed in the unit's still
stance, and returns the root to render.
"""
import math

import bpy
from mathutils import Matrix

import bscene as B
import garments as G
import human as H
import props as P

# MakeHuman macro sliders per catalog build (Civ3's figures are lean and
# long-limbed with small heads: high proportions, moderate weight).
BUILDS = {
    'lean': dict(muscle=0.62, weight=0.32),
    'normal': dict(muscle=0.55, weight=0.42),
    'muscular': dict(muscle=0.78, weight=0.42),
    'brawny': dict(muscle=0.9, weight=0.5),
}

HAIR = {'short': 'short02', 'long': 'long01', 'topknot': 'ponytail01', 'ponytail': 'ponytail01',
        'bald': None, None: None}

DEFAULT_COLORS = {
    'skin': '#c09470', 'hair': '#3a2a1c', 'top': '#8a7a5a', 'bottom': '#6a5038', 'belt': '#44301e',
    'wraps': '#6a4a30', 'wraps_band': '#3a2618', 'boots': '#4e3624', 'sandal': '#44301e',
    'helmet': '#8e8a80', 'crest': '#4a3626', 'cuirass': '#a08452', 'pelt': '#8a8682', 'pelt_dark': '#4a4642',
    'greaves': '#8a8a8e', 'bracers': '#6a4a30', 'leather': '#5e4430',
}


def _colors(entry):
    c = dict(DEFAULT_COLORS)
    c.update(entry.get('colors', {}))
    return c


def _mat(entry, part, maker, *args, **kw):
    """A garment material for catalog part `part`: tinted if the catalog says so."""
    c = _colors(entry)
    tint = part in entry.get('tint', [])
    color = (1, 1, 1) if tint else B.hex_color(c.get(part, '#808080'))
    return maker(f'{part}', color, *args, tint=tint, **kw)


def _skin_base(hexcolor):
    """The MPFB skin texture closest in lightness to the catalog's skin color."""
    r, g, b = B.hex_color(hexcolor)
    lum = 0.3 * r + 0.59 * g + 0.11 * b
    return 'middleage_african_male' if lum < 0.42 else 'middleage_asian_male'


# ------------------------------------------------------------ clothes

def dress(entry, mesh, arm):
    c = _colors(entry)
    top, bottom = entry.get('top'), entry.get('bottom')
    torso = ['spine_01', 'spine_02', 'spine_03', 'clavicle_l', 'clavicle_r', 'pelvis']
    if top in ('tunic', 'sleeved', 'mantle', 'vest'):
        bones = list(torso)
        if top in ('tunic', 'mantle'):
            bones += ['upperarm_l', 'upperarm_r']
        if top == 'sleeved':
            bones += ['upperarm_l', 'upperarm_r', 'lowerarm_l', 'lowerarm_r']
        keep = None
        if top in ('tunic', 'mantle'):
            # short sleeves: the upper half of the upper arm
            keep = lambda co: abs(co.x) < 0.27
        G.shell(mesh, arm, 'Top', bones, _mat(entry, 'top', G.cloth), zmin=0.9, zmax=1.47, offset=0.008,
                thickness=0.004, keep=keep)
    elif top == 'strap':
        # a sash over the left shoulder, across the chest
        G.shell(mesh, arm, 'Top', torso, _mat(entry, 'top', G.cloth), zmin=0.95, zmax=1.45, offset=0.009,
                thickness=0.004, keep=lambda co: abs((co.z - 1.2) - 0.9 * co.x) < 0.07)
    if bottom in ('skirt', 'kilt'):
        G.skirt(mesh, arm, 'Bottom', _mat(entry, 'bottom', G.cloth), length=0.55 if bottom == 'skirt' else 0.7,
                flare=1.1)
    elif bottom == 'loincloth':
        G.skirt(mesh, arm, 'Bottom', _mat(entry, 'bottom', G.cloth), length=0.42, flare=1.12, panels=(0.75, 0.75))
    elif bottom == 'long':
        G.shell(mesh, arm, 'Bottom', ['pelvis', 'thigh_l', 'thigh_r', 'calf_l', 'calf_r'], _mat(entry, 'bottom', G.cloth),
                zmin=0.1, zmax=1.0, offset=0.008, thickness=0.004)
    # belt
    if bottom:
        G.shell(mesh, arm, 'Belt', ['pelvis', 'spine_01'], _mat(entry, 'belt', G.leather), zmin=0.98, zmax=1.04,
                offset=0.013, thickness=0.01)
    if entry.get('cuirass'):
        metal = 'cuirass' in entry.get('metal', ['cuirass'])
        maker = (lambda n, col, tint=False: B.material(n, col, roughness=0.35, metallic=0.9, tint=tint)) if metal else G.leather
        G.shell(mesh, arm, 'Cuirass', torso, _mat(entry, 'cuirass', maker), zmin=0.98, zmax=1.42, offset=0.016,
                thickness=0.008, keep=lambda co: abs(co.x) < 0.17)
    wraps = entry.get('wraps')
    for side in 'lr':
        if wraps == 'wraps':
            G.shell(mesh, arm, 'Wrap_' + side, ['calf_' + side], G.fur('wraps_' + side, B.hex_color(c['wraps']),
                    B.hex_color(c['wraps_band'])), zmin=0.08, offset=0.012, thickness=0.012)
        elif wraps == 'boots':
            G.shell(mesh, arm, 'Boot_' + side, ['calf_' + side, 'foot_' + side, 'ball_' + side],
                    _mat(entry, 'boots', G.leather), zmax=0.36, offset=0.008, thickness=0.006)
        elif wraps == 'greaves':
            G.shell(mesh, arm, 'Greave_' + side, ['calf_' + side], (lambda n, col, tint=False: B.material(
                n, col, roughness=0.4, metallic=0.85, tint=tint))('greaves', B.hex_color(c['greaves'])),
                zmin=0.1, zmax=0.48, offset=0.012, thickness=0.006, keep=lambda co: co.y < 0.02)
        if entry.get('sandals') and wraps not in ('boots',):
            G.shell(mesh, arm, 'Sandal_' + side, ['foot_' + side, 'ball_' + side], _mat(entry, 'sandal', G.leather),
                    zmax=0.025, offset=0.004, thickness=0.008)
        if entry.get('bracers'):
            G.shell(mesh, arm, 'Bracer_' + side, ['lowerarm_' + side], _mat(entry, 'bracers', G.cloth),
                    offset=0.008, thickness=0.006)
    helmet(entry, mesh, arm)
    if entry.get('beard'):
        G.beard(mesh, B.hex_color(c['hair']))


def helmet(entry, mesh, arm):
    """Headgear fitted to the head: a shell of the scalp above the brow."""
    kind = entry.get('helmet')
    if not kind:
        return
    me = mesh.data
    body = mesh.vertex_groups.get('body')
    top = max(v.co.z for v in me.vertices if any(g.group == body.index and g.weight > 0.5 for g in v.groups))
    metal = kind in entry.get('metal', ['helmet']) or 'helmet' in entry.get('metal', ['helmet'])
    if kind in ('crested', 'horned', 'cap', 'wolf', 'hood'):
        depth = {'cap': 0.085, 'crested': 0.10, 'horned': 0.10, 'wolf': 0.12, 'hood': 0.19}[kind]
        if kind == 'wolf':
            mat = G.fur('helmet', B.hex_color(_colors(entry)['pelt']), B.hex_color(_colors(entry)['pelt_dark']))
        elif kind == 'hood' or not metal:
            mat = _mat(entry, 'helmet', G.cloth if kind == 'hood' else G.leather)
        else:
            tint = 'helmet' in entry.get('tint', [])
            mat = B.material('helmet', (1, 1, 1) if tint else B.hex_color(_colors(entry)['helmet']),
                             roughness=0.35, metallic=0.9, tint=tint)
        G.shell(mesh, arm, 'Helmet', ['head', 'neck_01'], mat, zmin=top - depth, offset=0.02 if kind != 'hood' else 0.012,
                thickness=0.008, min_weight=0.2)
    elif kind == 'headband':
        G.shell(mesh, arm, 'Helmet', ['head'], _mat(entry, 'helmet', G.cloth), zmin=top - 0.085, zmax=top - 0.05,
                offset=0.014, thickness=0.01, min_weight=0.2)


# ------------------------------------------------------------ stance and gear

def _rot(x=0, y=0, z=0):
    return (Matrix.Rotation(math.radians(z), 4, 'Z') @ Matrix.Rotation(math.radians(y), 4, 'Y')
            @ Matrix.Rotation(math.radians(x), 4, 'X'))


# Still stances by the weapon held (armature space: the human faces -y, its
# right side is -x). Each: bone aims, then the right/left hand prop offsets
# (in the hand bone's space: +y along the fingers).
STANCES = {
    'hang': [('spine_02', (0, -0.12, 1)), ('neck_01', (0, -0.05, 1)),
             ('thigh_l', (0.17, 0.02, -1)), ('calf_l', (0.1, 0.06, -1)),
             ('thigh_r', (-0.17, -0.04, -1)), ('calf_r', (-0.1, 0.04, -1)),
             ('upperarm_l', (0.32, 0.02, -1)), ('lowerarm_l', (0.3, -0.3, -1)),
             ('upperarm_r', (-0.34, -0.04, -1)), ('lowerarm_r', (-0.42, -0.25, -1))],
    'upright': [('neck_01', (0, -0.02, 1)),
                ('thigh_l', (0.1, 0.0, -1)), ('calf_l', (0.06, 0.04, -1)),
                ('thigh_r', (-0.1, 0.0, -1)), ('calf_r', (-0.06, 0.04, -1)),
                ('upperarm_l', (0.3, -0.35, -1)), ('lowerarm_l', (0.15, -1.0, 0.1)),
                ('upperarm_r', (-0.32, -0.15, -1)), ('lowerarm_r', (-0.05, -1.0, 0.9))],
    'low_across': [('neck_01', (0, -0.03, 1)),
                   ('thigh_l', (0.12, 0.0, -1)), ('calf_l', (0.07, 0.04, -1)),
                   ('thigh_r', (-0.12, 0.0, -1)), ('calf_r', (-0.07, 0.04, -1)),
                   ('upperarm_l', (0.25, -0.3, -1)), ('lowerarm_l', (-0.4, -0.9, -0.2)),
                   ('upperarm_r', (-0.25, -0.25, -1)), ('lowerarm_r', (0.3, -0.9, -0.3))],
}

WEAPON_STANCE = {
    'stone_axe': 'hang', 'club': 'hang', 'sword': 'hang', 'axe': 'hang',
    'spear': 'upright', 'staff': 'upright', 'pike': 'upright',
    'shovel': 'low_across', 'bow': 'low_across',
}


def equip(entry, arm):
    c = _colors(entry)
    weapon = entry.get('weapon')
    offhand = entry.get('offhand')
    props = entry.get('props', {})
    stance = WEAPON_STANCE.get(weapon or ('bow' if offhand == 'bow' else None), 'hang')
    H.pose(arm, STANCES[stance])
    made = []
    if weapon:
        name, params = props.get(weapon, (weapon, {}))
        params = dict(params)
        if 'weapon_scale' in entry and isinstance(entry['weapon_scale'], (int, float)):
            params.setdefault('scale', entry['weapon_scale'] * 0.87)
        obj = P.make(name, c, **params)
        if stance == 'hang':
            off = Matrix.Translation((0.0, 0.05, 0.0)) @ _rot(x=-90, y=20)
        elif stance == 'upright':
            off = Matrix.Translation((0.0, 0.05, 0.0)) @ _rot(x=180)
        else:
            off = Matrix.Translation((0.0, 0.05, 0.0)) @ _rot(x=-90, z=0)
        H.attach(obj, arm, 'hand_r', off)
        made.append(obj)
    if offhand:
        name, params = (entry['offhand_mesh'] if entry.get('offhand_mesh') else (offhand, {}))
        obj = P.make(name, c, **params)
        if offhand.startswith('shield'):
            off = Matrix.Translation((0.06, -0.08, 0.0)) @ _rot(z=90, x=90)
        else:
            off = Matrix.Translation((0.0, 0.05, 0.0)) @ _rot(y=90, x=-90)
        H.attach(obj, arm, 'lowerarm_l' if offhand.startswith('shield') else 'hand_l', off)
        made.append(obj)
    back = entry.get('back')
    if back in ('quiver', 'backpack'):
        obj = P.make(back, c)
        H.attach(obj, arm, 'spine_03', Matrix.Translation((0.0, -0.12, 0.13)) @ _rot(x=-90, z=180))
        made.append(obj)
    elif back == 'pelt':
        pass  # TODO: a pelt over the shoulders
    return made


def build_footman(entry):
    """The posed, dressed and equipped human of a catalog entry; returns the root."""
    c = _colors(entry)
    build = entry.get('build', 'normal')
    if isinstance(build, dict):
        macro = dict(BUILDS.get(build.get('build', 'normal'), BUILDS['normal']))
        macro['muscle'] = min(1.0, macro['muscle'] * build.get('muscle', 1.0) ** 0.3)
        macro['weight'] = min(1.0, macro['weight'] * build.get('bulk', 1.0))
    else:
        macro = dict(BUILDS.get(build, BUILDS['normal']))
    hair = HAIR.get(entry.get('hair'), 'short02')
    mesh, arm = H.body(gender=1.0, age=0.45, height=0.6, proportions=0.75, **macro,
                       skin=_skin_base(c['skin']), hair=hair if entry.get('helmet') not in ('hood', 'wolf', 'crested', 'horned', 'cap') or entry.get('hair') == 'long' else None,
                       skin_tone=B.hex_color(c['skin']), hair_tone=B.hex_color(c['hair']))
    dress(entry, mesh, arm)
    equip(entry, arm)
    return H.rooted(arm)
