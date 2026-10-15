"""Builds a human unit in Blender from its catalog entry (Blender side).

The catalog is the old procedural remake's (ArtGen/units/catalog): the same
vocabulary of build, clothes, gear, colors and tinted parts, written down from
each Civ3 original. build_footman() turns an entry into an MPFB body dressed
with garments.py and equipped with props.py, posed in the unit's still
stance, and returns the root to render.
"""
import math

import bpy
from mathutils import Matrix, Vector

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


def _mat(entry, part, maker, *args, **kw):  # noqa: D401
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
        zmax = entry.get('top_zmax', 1.47)
        if entry.get('neck') == 'high':
            # covered to the base of the neck, as most of Civ3's soldiers and riders are
            bones = bones + ['neck_01']
            zmax = max(zmax, 1.56)
        G.shell(mesh, arm, 'Top', bones, _mat(entry, 'top', G.cloth, weave=0.6), zmin=0.9, zmax=zmax, offset=0.008,
                thickness=0.004, keep=keep)
        if entry.get('collar') == 'mail':
            # a mail collar (coif's lower part) around the neck and over the shoulders
            G.shell(mesh, arm, 'Collar', ['neck_01', 'clavicle_l', 'clavicle_r', 'spine_03'],
                    B.material('mail', B.hex_color(c.get('mail', '#6a6a6e')), roughness=0.55, metallic=0.85,
                               nodes=G.mail_nodes), zmin=1.42, zmax=1.62, offset=0.014, thickness=0.006)
    elif top == 'strap':
        # a sash over the left shoulder, across the chest
        G.shell(mesh, arm, 'Top', torso, _mat(entry, 'top', G.cloth), zmin=0.95, zmax=1.45, offset=0.009,
                thickness=0.004, keep=lambda co: abs((co.z - 1.2) - 0.9 * co.x) < 0.07)
    if bottom in ('skirt', 'kilt'):
        G.skirt(mesh, arm, 'Bottom', _mat(entry, 'bottom', G.cloth, weave=0.6), length=0.55 if bottom == 'skirt' else 0.7,
                flare=1.12, folds=0.08)
    elif bottom == 'loincloth':
        pass    # flaps from the belt, after posing (dress_posed)
    elif bottom == 'long':
        G.shell(mesh, arm, 'Bottom', ['pelvis', 'thigh_l', 'thigh_r', 'calf_l', 'calf_r'], _mat(entry, 'bottom', G.cloth),
                zmin=0.1, zmax=1.0, offset=0.008, thickness=0.004)
    # belt
    for g in entry.get('gear', []):
        name = g[0] if isinstance(g, (list, tuple)) else g
        if name == 'chest_strap':
            col = (g[1] or {}).get('color', c['belt']) if isinstance(g, (list, tuple)) and len(g) > 1 else c['belt']
            G.shell(mesh, arm, 'ChestStrap', torso, G.leather('chest_strap', B.hex_color(col)), zmin=1.0, zmax=1.5,
                    offset=0.02, thickness=0.006, keep=lambda co: abs((co.z - 1.22) + 0.95 * co.x) < 0.035)
    if bottom:
        G.shell(mesh, arm, 'Belt', ['pelvis', 'spine_01'], _mat(entry, 'belt', G.leather), zmin=0.98, zmax=1.04,
                offset=0.013, thickness=0.01)
    if entry.get('pauldrons'):
        # shoulder pieces: the top of the upper arms and shoulders
        G.shell(mesh, arm, 'Pauldrons', ['upperarm_l', 'upperarm_r', 'clavicle_l', 'clavicle_r'],
                _mat(entry, 'top', G.leather), zmin=1.33, offset=0.012, thickness=0.008, min_weight=0.35,
                keep=lambda co: abs(co.x) > 0.13)
    if entry.get('cuirass'):
        metal = 'cuirass' in entry.get('metal', ['cuirass'])
        maker = (lambda n, col, tint=False: B.material(n, col, roughness=0.35, metallic=0.9, tint=tint)) if metal else G.leather
        G.shell(mesh, arm, 'Cuirass', torso + (['neck_01'] if entry.get('neck') == 'high' else []),
                _mat(entry, 'cuirass', maker), zmin=0.98, zmax=1.55 if entry.get('neck') == 'high' else 1.42,
                offset=0.016, thickness=0.008, keep=lambda co: abs(co.x) < 0.17)
    wraps = entry.get('wraps')
    for side in 'lr':
        if wraps == 'wraps':
            G.shell(mesh, arm, 'Wrap_' + side, ['calf_' + side], G.fur('wraps_' + side, B.hex_color(c['wraps']),
                    B.hex_color(c['wraps_band'])), zmin=0.08, offset=0.012, thickness=0.012)
        if wraps == 'boots' or entry.get('boots'):
            G.shell(mesh, arm, 'Boot_' + side, ['calf_' + side, 'foot_' + side, 'ball_' + side],
                    _mat(entry, 'boots', G.leather), zmax=0.36, offset=0.008, thickness=0.006)
        elif wraps == 'greaves':
            G.shell(mesh, arm, 'Greave_' + side, ['calf_' + side], (lambda n, col, tint=False: B.material(
                n, col, roughness=0.4, metallic=0.85, tint=tint))('greaves', B.hex_color(c['greaves'])),
                zmin=0.1, zmax=0.48, offset=0.016, thickness=0.006)
        if entry.get('sandals') and wraps not in ('boots',):
            G.shell(mesh, arm, 'Sandal_' + side, ['foot_' + side, 'ball_' + side], _mat(entry, 'sandal', G.leather),
                    zmax=0.025, offset=0.004, thickness=0.008)
            # straps laced up the ankle
            G.shell(mesh, arm, 'SandalStrap_' + side, ['foot_' + side, 'calf_' + side], _mat(entry, 'sandal', G.leather),
                    zmin=0.04, zmax=0.17, offset=0.006, thickness=0.004,
                    keep=lambda co: (co.z * 60.0) % 2.0 < 0.75)
        if entry.get('bracers'):
            G.shell(mesh, arm, 'Bracer_' + side, ['lowerarm_' + side], _mat(entry, 'bracers', G.cloth),
                    offset=0.008, thickness=0.006)
    helmet(entry, mesh, arm)
    if entry.get('beard'):
        G.beard(mesh, B.hex_color(c['hair']), length=0.018, density=700)
    if entry.get('pelt_cape'):
        G.pelt(mesh, arm, B.hex_color(entry['pelt_cape'][0]), B.hex_color(entry['pelt_cape'][1]), length=0.03)
    if entry.get('mane'):
        # over a full hair mesh (no scalp shows from above): long falling
        # strands, locks forward over the shoulders, a rough strand coat
        G.falling_hair(mesh, B.hex_color(c['hair']), length=entry['mane'])
        G.front_locks(mesh, B.hex_color(c['hair']), length=entry['mane'] * 1.1)
    G.roughen_hair(mesh, B.hex_color(c['hair']), length=0.03, density=6000)


def dress_posed(entry, mesh, arm):
    """Garments placed after posing (rigid parts hung on bones), then the
    hanging cloth draped by simulation onto the posed body."""
    hanging = [o for o in bpy.data.objects if o.name in ('Bottom',)] if entry.get('bottom') in ('skirt', 'kilt') else []
    if entry.get('bottom') == 'loincloth':
        lengths = entry.get('loin_length', (0.42, 0.36))
        maker = G.leather if entry.get('bottom_material') == 'leather' else (lambda n, col, tint=False: G.cloth(n, col, tint=tint, weave=0.6))
        hanging += G.loincloth(mesh, arm, _mat(entry, 'bottom', maker), length=lengths[0], back_length=lengths[1],
                               torn=entry.get('torn', True))
    hanging = [o for o in hanging if not o.get('no_drape')]
    if hanging and entry.get('drape', True):
        G.drape(hanging, mesh)


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
        depth = {'cap': 0.085, 'crested': 0.082, 'horned': 0.09, 'wolf': 0.12, 'hood': 0.19}[kind]
        if kind == 'wolf':
            mat = G.fur('helmet', B.hex_color(_colors(entry)['pelt']), B.hex_color(_colors(entry)['pelt_dark']))
        elif kind == 'hood' or not metal:
            mat = _mat(entry, 'helmet', G.cloth if kind == 'hood' else G.leather)
        else:
            tint = 'helmet' in entry.get('tint', [])
            mat = B.material('helmet', (1, 1, 1) if tint else B.hex_color(_colors(entry)['helmet']),
                             roughness=0.62, metallic=0.75, tint=tint)
        keep = None
        if kind == 'crested':
            # a Corinthian war helm: down to the jaw, with cheek guards and a
            # T-shaped opening for the eyes and mouth
            depth = 0.2
            _, yc, _ = G._head(mesh)

            def keep(co, top=top / G._scale, yc=yc / G._scale):
                d = top - co.z
                face = co.y - yc < -0.045
                eyes = 0.075 < d < 0.11 and abs(co.x) < 0.06
                mouth = d >= 0.11 and abs(co.x) < 0.028
                return not (face and (eyes or mouth))
        G.shell(mesh, arm, 'Helmet', ['head', 'neck_01'], mat, zmin=(top - depth) / G._scale, offset=0.02 if kind != 'hood' else 0.012,
                thickness=0.008, min_weight=0.2, keep=keep)
        if kind == 'horned':
            G.horns(mesh, arm, B.material('horn', B.hex_color(_colors(entry).get('horn', '#c8b898')), roughness=0.5),
                    length=0.17, radius=0.026)
        if kind == 'crested':
            c = _colors(entry)
            tint = 'crest' in entry.get('tint', [])
            G.crest(mesh, arm, G.hide('crest', (1, 1, 1) if tint else B.hex_color(c['crest']),
                                      (0.8, 0.8, 0.8) if tint else tuple(v * 0.6 for v in B.hex_color(c['crest'])), tint=tint))
    elif kind == 'headband':
        G.shell(mesh, arm, 'Helmet', ['head'], _mat(entry, 'helmet', G.cloth), zmin=(top - 0.085) / G._scale, zmax=(top - 0.05) / G._scale,
                offset=0.014, thickness=0.01, min_weight=0.2)


# ------------------------------------------------------------ stance and gear

def _rot(x=0, y=0, z=0):
    return (Matrix.Rotation(math.radians(z), 4, 'Z') @ Matrix.Rotation(math.radians(y), 4, 'Y')
            @ Matrix.Rotation(math.radians(x), 4, 'X'))


# Still stances by the weapon held (armature space: the human faces -y, its
# right side is -x). Each: bone aims, then the right/left hand prop offsets
# (in the hand bone's space: +y along the fingers).
STANCES = {
    'hang': [('spine_01', (0, -0.08, 1)), ('spine_02', (0, -0.22, 1)), ('spine_03', (0, -0.15, 1)),
             ('clavicle_l', (1, -0.45, 0.05)), ('clavicle_r', (-1, -0.45, 0.05)),
             ('neck_01', (0, -0.12, 1)), ('head', (0, 0.1, 1)),
             ('thigh_l', (0.3, -0.05, -1)), ('calf_l', (0.24, 0.02, -1)), ('foot_l', (0.5, -1, -0.3)),
             ('thigh_r', (-0.3, -0.02, -1)), ('calf_r', (-0.24, 0.04, -1)), ('foot_r', (-0.5, -1, -0.3)),
             ('upperarm_l', (0.42, -0.08, -1)), ('lowerarm_l', (0.3, -0.25, -1)),
             ('upperarm_r', (-0.55, -0.15, -1)), ('lowerarm_r', (-0.35, -1.0, -0.35))],
    'upright': [('spine_02', (0, -0.02, 1)), ('neck_01', (0, 0.22, 1)), ('head', (0, 0.5, 1)),
                ('clavicle_l', (1, -0.25, 0.05)), ('clavicle_r', (-1, -0.25, 0.05)),
                ('thigh_l', (0.14, -0.08, -1)), ('calf_l', (0.1, 0.02, -1)), ('foot_l', (0.35, -1, -0.3)),
                ('thigh_r', (-0.14, 0.04, -1)), ('calf_r', (-0.1, 0.06, -1)), ('foot_r', (-0.35, -1, -0.3)),
                ('upperarm_l', (0.2, -0.45, -1)), ('lowerarm_l', (-0.55, -1.0, 0.25)),
                ('upperarm_r', (-0.62, -0.1, -1)), ('lowerarm_r', (-0.45, -1.0, 0.5))],
    'low_across': [('spine_02', (0, -0.1, 1)), ('neck_01', (0, 0.0, 1)), ('head', (0, 0.25, 1)),
                   ('thigh_l', (0.18, -0.1, -1)), ('calf_l', (0.12, 0.04, -1)), ('foot_l', (0.4, -1, -0.3)),
                   ('thigh_r', (-0.18, 0.04, -1)), ('calf_r', (-0.12, 0.06, -1)), ('foot_r', (-0.4, -1, -0.3)),
                   ('upperarm_l', (0.15, -0.45, -1)), ('lowerarm_l', (-0.45, -1.0, 0.15)),
                   ('upperarm_r', (-0.32, -0.15, -1)), ('lowerarm_r', (-0.2, -0.8, -0.6))],
}

# Seated on a horse: thighs forward and apart around the barrel, shins
# down, hands forward at the reins (the weapon hand set by `equip`).
STANCES['ride'] = [('spine_02', (0, -0.05, 1)), ('neck_01', (0, -0.04, 1)), ('head', (0, 0.06, 1)),
                   ('thigh_l', (0.42, -0.85, -0.35)), ('calf_l', (0.22, 0.12, -1)), ('foot_l', (0.25, -1, -0.2)),
                   ('thigh_r', (-0.42, -0.85, -0.35)), ('calf_r', (-0.22, 0.12, -1)), ('foot_r', (-0.25, -1, -0.2)),
                   ('upperarm_l', (0.25, -0.5, -1)), ('lowerarm_l', (0.05, -1.0, -0.1)),
                   ('upperarm_r', (-0.3, -0.35, -1)), ('lowerarm_r', (-0.15, -1.0, 0.35))]

STANCES['bow_low'] = [('spine_02', (0, 0.0, 1)), ('neck_01', (0, 0.12, 1)), ('head', (0, 0.4, 1)),
                      ('clavicle_l', (1, -0.15, 0.1)), ('clavicle_r', (-1, -0.15, 0.1)),
                      ('thigh_l', (0.2, -0.1, -1)), ('calf_l', (0.12, 0.04, -1)), ('foot_l', (0.4, -1, -0.3)),
                      ('thigh_r', (-0.16, 0.06, -1)), ('calf_r', (-0.1, 0.06, -1)), ('foot_r', (-0.4, -1, -0.3)),
                      ('upperarm_l', (0.32, -0.15, -1)), ('lowerarm_l', (0.25, -1.0, -0.45)),
                      ('upperarm_r', (-0.32, -0.15, -1)), ('lowerarm_r', (-0.05, -1.0, -0.4))]

STANCES['guard'] = [('spine_02', (0, -0.04, 1)), ('neck_01', (0, 0.1, 1)), ('head', (0, 0.35, 1)),
                    ('clavicle_l', (1, -0.2, 0.05)), ('clavicle_r', (-1, -0.2, 0.05)),
                    ('thigh_l', (0.24, -0.12, -1)), ('calf_l', (0.16, 0.04, -1)), ('foot_l', (0.45, -1, -0.3)),
                    ('thigh_r', (-0.24, 0.06, -1)), ('calf_r', (-0.16, 0.06, -1)), ('foot_r', (-0.45, -1, -0.3)),
                    ('upperarm_l', (0.38, -0.35, -1)), ('lowerarm_l', (0.05, -1.0, 0.1)),
                    ('upperarm_r', (-0.35, -0.15, -1)), ('lowerarm_r', (-0.3, -0.9, -0.45))]

WEAPON_STANCE = {
    'stone_axe': 'hang', 'club': 'hang', 'sword': 'hang', 'axe': 'hang',
    'spear': 'upright', 'staff': 'upright', 'pike': 'upright',
    'shovel': 'low_across', 'bow': 'bow_low',
}


def equip(entry, arm):
    c = _colors(entry)
    weapon = entry.get('weapon')
    offhand = entry.get('offhand')
    props = entry.get('props', {})
    stance = entry.get('stance_name') or WEAPON_STANCE.get(weapon or ('bow' if offhand == 'bow' else None), 'hang')
    H.pose(arm, STANCES[stance] + [tuple(a) for a in entry.get('pose_extra', [])])
    H.fists(arm, sides='lr')
    bulk = entry.get('upper_bulk')
    if bulk:
        H.bulk(arm, bulk)
    made = []
    if weapon:
        name, params = props.get(weapon, (weapon, {}))
        params = dict(params)
        if 'weapon_scale' in entry and isinstance(entry['weapon_scale'], (int, float)):
            params.setdefault('scale', entry['weapon_scale'] * 0.87)
        if name == 'war_club':
            import clubs
            obj = clubs.war_club(c, **params)
        else:
            obj = P.make(name, c, **params)
        at = H.grip_point(arm, 'hand_r')
        if stance in ('hang', 'guard'):
            # hanging from the fist, down and out to the side, head low
            H.hold(obj, arm, 'hand_r', at, up=(-0.7, -0.45, -0.5))
        elif stance == 'upright':
            # upright through the fist, butt near the ground
            H.hold(obj, arm, 'hand_r', at, up=(-0.22, 0.04, 1.0))
        else:
            # a tool held diagonally across the body, working end down at the right
            H.hold(obj, arm, 'hand_r', at, up=(-0.55, -0.25, -0.8))
        made.append(obj)
    if offhand:
        name, params = (entry['offhand_mesh'] if entry.get('offhand_mesh') else (offhand, {}))
        obj = P.make(name, c, **params)
        if offhand.startswith('shield'):
            # on the left forearm, held forward and upright, face to the front
            # strapped on the left forearm, which crosses the body: the shield
            # covers the torso from chest to hip, facing the front
            pb = arm.pose.bones['lowerarm_l']
            mid = (arm.matrix_world @ pb.head).lerp(arm.matrix_world @ pb.tail, 0.5)
            chest = arm.matrix_world @ arm.pose.bones['spine_02'].head
            shoulder = arm.matrix_world @ arm.pose.bones['clavicle_l'].head
            at = Vector((0.5 * (mid.x + chest.x) + 0.04, min(mid.y, chest.y) - 0.07, shoulder.z - 0.22 * G._scale))
            # turned 45 degrees in its plane: the X of its emblem bands reads as a + cross
            # tilted back a little, so its face turns up toward the camera
            H.hold(obj, arm, 'lowerarm_l', at, up=(0.7, 0.12, 0.7), forward=(0, 1, 0))
        else:
            at = H.grip_point(arm, 'hand_l')
            if offhand == 'bow':
                # thicker limbs, so the bow reads at 1x
                obj.scale = (1.8, 1.8, 1.0)
                bpy.context.view_layer.update()
                obj.select_set(True)
                bpy.context.view_layer.objects.active = obj
                bpy.ops.object.transform_apply(scale=True)
            # held low and level across the front of the hips, pointing forward-left
            H.hold(obj, arm, 'hand_l', at, up=(1.0, -0.35, 0.05))
        made.append(obj)
    back = entry.get('back')
    if back in ('quiver', 'backpack'):
        obj = P.make(back, c)
        # on the upper back (+y behind the unposed body), strap point at the
        # shoulder blades; a quiver slung diagonally, arrows up over the right shoulder
        sp = arm.matrix_world @ arm.pose.bones['spine_03'].head
        if back == 'quiver':
            at = sp + Vector((-0.06, 0.17 * G._scale, 0.3 * G._scale))
            obj.scale = (1.3, 1.3, 1.3)
            bpy.context.view_layer.update()
            H.hold(obj, arm, 'spine_03', at, up=(-0.4, 0.1, 1), forward=(0, -1, 0))
        else:
            at = sp + Vector((0.0, 0.14 * G._scale, 0.05 * G._scale))
            H.hold(obj, arm, 'spine_03', at, up=(0, 0.05, 1), forward=(0, -1, 0))
        made.append(obj)
    elif back == 'pelt':
        pass  # TODO: a pelt over the shoulders
    return made


def build_footman(entry, seated=False):
    """The posed, dressed and equipped human of a catalog entry; returns the
    root. `seated`: in the riding pose, not planted on the ground; the root's
    origin is then at the pelvis (put it on the saddle's 'rider' empty)."""
    if seated:
        entry = dict(entry, stance_name='ride')
    c = _colors(entry)
    build = entry.get('build', 'normal')
    if isinstance(build, dict):
        macro = dict(BUILDS.get(build.get('build', 'normal'), BUILDS['normal']))
        macro['muscle'] = min(1.0, macro['muscle'] * build.get('muscle', 1.0) ** 0.3)
        macro['weight'] = min(1.0, macro['weight'] * build.get('bulk', 1.0))
    else:
        macro = dict(BUILDS.get(build, BUILDS['normal']))
    macro.update(entry.get('macro', {}))
    macro.setdefault('height', 0.65)
    macro.setdefault('proportions', 0.8)
    helmeted = entry.get('helmet') in ('hood', 'wolf', 'crested', 'horned', 'cap')
    hair = entry.get('hair_asset', HAIR.get(entry.get('hair'), 'short02'))
    if helmeted and entry.get('hair') != 'long':
        hair = None
    mesh, arm = H.body(gender=1.0, age=entry.get('age', 0.5), **macro,
                       race=entry.get('race', {'caucasian': 0.4, 'african': 0.3, 'asian': 0.3}),
                       skin=_skin_base(c['skin']), hair=hair,
                       skin_tone=B.hex_color(c['skin']), hair_tone=B.hex_color(c['hair']))
    H.grime(mesh, amount=entry.get('grime', 0.4))
    dress(entry, mesh, arm)
    equip(entry, arm)
    dress_posed(entry, mesh, arm)
    root = H.rooted(arm)
    if seated:
        # the pelvis on the root's origin
        H._update()
        pelvis = arm.matrix_world @ arm.pose.bones['pelvis'].head
        root.location -= pelvis
        H._update()
    else:
        H.plant(root, mesh)
    return root
