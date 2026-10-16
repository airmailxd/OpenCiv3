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
    # a tinted part renders white (the full civ color), or gray for a darker shade of it
    shade = entry.get('tint_shade', {}).get(part, 1.0)
    color = (shade, shade, shade) if tint else B.hex_color(c.get(part, '#808080'))
    pattern = entry.get('patterns', {}).get(part)
    if pattern and maker is G.cloth:
        kw = dict(kw, pattern=tuple(pattern))
    return maker(f'{part}', color, *args, tint=tint, **kw)


def _calm_skin(hexcol, sat=0.7):
    """Bare skin with its saturation scaled: the skin shader pushes saturated
    swatches orange, and dark ones maroon."""
    import colorsys
    h = hexcol.lstrip('#')
    r, g, b = (int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))
    hh, ll, ss = colorsys.rgb_to_hls(r, g, b)
    r, g, b = colorsys.hls_to_rgb(hh, ll, ss * sat)
    return '#%02x%02x%02x' % (round(r * 255), round(g * 255), round(b * 255))


def _skin_base(hexcolor, female=False):
    """The MPFB skin texture closest in lightness to the catalog's skin color."""
    r, g, b = B.hex_color(hexcolor)
    lum = 0.3 * r + 0.59 * g + 0.11 * b
    sex = 'female' if female else 'male'
    return f'middleage_african_{sex}' if lum < 0.42 else f'middleage_asian_{sex}'


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
        if top == 'sleeved' and (entry.get('puffy') or entry.get('wide_sleeves')):
            # puffed (Renaissance) or wide (kimono, robe) sleeves over the arms
            G.shell(mesh, arm, 'Sleeves', ['upperarm_l', 'upperarm_r'] + (['lowerarm_l', 'lowerarm_r'] if entry.get('wide_sleeves') else []),
                    _mat(entry, 'sleeves' if 'sleeves' in entry.get('tint', []) or 'sleeves' in c else 'top', G.cloth, weave=0.6),
                    zmin=0.85, zmax=zmax, offset=0.035 if entry.get('wide_sleeves') else 0.026, thickness=0.004)
        if entry.get('collar') == 'mail':
            # a mail collar (coif's lower part) around the neck and over the shoulders
            G.shell(mesh, arm, 'Collar', ['neck_01', 'clavicle_l', 'clavicle_r', 'spine_03'],
                    B.material('mail', B.hex_color(c.get('mail', '#6a6a6e')), roughness=0.55, metallic=0.85,
                               nodes=G.mail_nodes), zmin=1.42, zmax=1.62, offset=0.014, thickness=0.006)
    elif top == 'strap':
        # a sash over the left shoulder, across the chest
        G.shell(mesh, arm, 'Top', torso, _mat(entry, 'top', G.cloth), zmin=0.95, zmax=1.45, offset=0.009,
                thickness=0.004, keep=lambda co: abs((co.z - 1.2) - 0.9 * co.x) < 0.07)
    if bottom == 'gown':
        G.skirt(mesh, arm, 'Bottom', _mat(entry, 'bottom', G.cloth, weave=0.6), length=0.7,
                flare=entry.get('gown_flare', 1.55), folds=0.1, drop=entry.get('robe_drop', 0.5))
    if bottom in ('skirt', 'kilt'):
        G.skirt(mesh, arm, 'Bottom', _mat(entry, 'bottom', G.cloth, weave=0.6), length=0.55 if bottom == 'skirt' else 0.7,
                flare=1.12 + 0.3 * entry.get('robe_drop', 0.0), folds=0.08, drop=entry.get('robe_drop', 0.0))
    elif bottom == 'loincloth':
        pass    # flaps from the belt, after posing (dress_posed)
    elif bottom in ('long', 'breeches'):
        # trousers to the ankle, or breeches to below the knee (puffed with 'puffy')
        puff = 0.022 if entry.get('puffy') else 0.008
        G.shell(mesh, arm, 'Bottom', ['pelvis', 'thigh_l', 'thigh_r', 'calf_l', 'calf_r'],
                _mat(entry, 'bottom', G.cloth, weave=0.6), zmin=0.1 if bottom == 'long' else 0.42, zmax=1.0,
                offset=puff, thickness=0.004)
    # belt
    if entry.get('harness'):
        # two straps crossing on the chest and back
        tint = 'harness' in entry.get('tint', [])
        hmat = G.cloth('harness', (1, 1, 1), tint=True) if tint else G.leather('harness', B.hex_color(c['belt']))
        for sgn in (1, -1):
            G.shell(mesh, arm, 'Harness', torso, hmat, zmin=1.0, zmax=1.5, offset=0.02, thickness=0.006,
                    keep=lambda co, sgn=sgn: abs((co.z - 1.22) + sgn * 0.95 * co.x) < 0.03)
    if entry.get('veil'):
        # a cloth veil or mask over the lower face (below the eyes)
        _, yc, _ = G._head(mesh)
        topz = max(v.co.z for v in mesh.data.vertices) / G._scale
        tint = 'veil' in entry.get('tint', [])
        vmat = G.cloth('veil', (1, 1, 1), tint=True) if tint else G.cloth('veil', B.hex_color(c.get('veil', '#2a2622')))
        G.shell(mesh, arm, 'Veil', ['head', 'neck_01'], vmat, zmin=(topz - entry.get('veil_drop', 0.26)), zmax=(topz - 0.115), offset=0.012,
                thickness=0.004, min_weight=0.2, keep=lambda co, yc=yc / G._scale: co.y - yc < 0.02)
    for g in entry.get('gear', []):
        name = g[0] if isinstance(g, (list, tuple)) else g
        if name == 'chest_strap':
            gp = (g[1] or {}) if isinstance(g, (list, tuple)) and len(g) > 1 else {}
            col = gp.get('color', c['belt'])
            tint = 'strap' in entry.get('tint', []) or gp.get('tint')
            half = gp.get('width', 0.07) / 2           # a sash: 'width' 0.15-0.2
            G.shell(mesh, arm, 'ChestStrap', torso, G.cloth('chest_strap', (1, 1, 1), tint=True) if tint else
                    G.leather('chest_strap', B.hex_color(col)), zmin=1.0, zmax=1.5,
                    offset=0.02, thickness=0.006, keep=lambda co, h=half: abs((co.z - 1.22) + 0.95 * co.x) < h)
    # mail limbs: 'mail': ['arms', 'legs'] (ring mail sleeves to the wrist,
    # chausses from the thigh to the foot), under the other clothes
    mail = entry.get('mail_parts', [])
    if mail:
        mm = B.material('mail', B.hex_color(c.get('mail', '#6a6a6e')), roughness=0.55, metallic=0.85,
                        nodes=G.mail_nodes)
        if 'arms' in mail:
            G.shell(mesh, arm, 'MailArms', ['upperarm_l', 'upperarm_r', 'lowerarm_l', 'lowerarm_r'], mm,
                    zmin=0.82, offset=0.007, thickness=0.004, min_weight=0.3)
        if 'legs' in mail:
            G.shell(mesh, arm, 'MailLegs', ['thigh_l', 'thigh_r', 'calf_l', 'calf_r', 'foot_l', 'foot_r'], mm,
                    zmax=0.95, offset=0.006, thickness=0.004, min_weight=0.3)
    # a coat's skirts: from the waist to `coat_length` (0.62 ~ the knee), in the top's cloth
    if entry.get('coat_length'):
        if entry.get('coat_metal'):          # tassets: plate skirts in the cuirass's metal
            cmat = _mat(entry, 'cuirass', lambda n, col, tint=False: B.material(
                n, col, roughness=0.35, metallic=0.9, tint=tint, nodes=G.plate_bands(3)))
        else:
            cmat = _mat(entry, 'top', G.cloth, weave=0.6)
        G.skirt(mesh, arm, 'Coat', cmat, length=entry['coat_length'],
                flare=entry.get('coat_flare', 1.25), folds=0.08)
    if bottom:
        G.shell(mesh, arm, 'Belt', ['pelvis', 'spine_01'], _mat(entry, 'belt', G.leather), zmin=0.98, zmax=1.04,
                offset=0.013, thickness=0.01)
        if entry.get('belt_gear', True):
            G.belt_gear(mesh, arm, B.material('buckle', B.hex_color(c.get('buckle', '#8a7a52')), roughness=0.4,
                                              metallic=1.0),
                        G.leather('pouch', B.hex_color(c.get('pouch', c['belt']))), pouch=entry.get('pouch', True))
    if entry.get('pauldrons'):
        # shoulder pieces: the top of the upper arms and shoulders
        pmat = (_mat(entry, 'cuirass', lambda n, col, tint=False: B.material(n, col, roughness=0.35, metallic=0.9,
                                                                             tint=tint, nodes=G.plate_bands(3)))
                if entry['pauldrons'] == 'metal' else _mat(entry, 'top', G.leather))
        G.shell(mesh, arm, 'Pauldrons', ['upperarm_l', 'upperarm_r', 'clavicle_l', 'clavicle_r'],
                pmat, zmin=1.33, offset=0.012, thickness=0.008, min_weight=0.35,
                keep=lambda co: abs(co.x) > 0.13)
    if entry.get('cuirass'):
        metal = 'cuirass' in entry.get('metal', ['cuirass'])
        bands = entry.get('cuirass_bands')       # lorica segmentata: N horizontal plates
        maker = (lambda n, col, tint=False: B.material(n, col, roughness=0.35, metallic=0.9, tint=tint,
                                                       nodes=G.plate_bands(bands) if bands else None)) \
            if metal else G.leather
        G.shell(mesh, arm, 'Cuirass', torso + (['neck_01'] if entry.get('neck') == 'high' else []),
                _mat(entry, 'cuirass', maker), zmin=0.98, zmax=1.55 if entry.get('neck') == 'high' else 1.42,
                offset=0.016, thickness=0.008, keep=lambda co, w=entry.get('cuirass_width', 0.17): abs(co.x) < w)
    wraps = entry.get('wraps')
    for side in 'lr':
        if wraps == 'wraps':
            G.shell(mesh, arm, 'Wrap_' + side, ['calf_' + side], G.fur('wraps_' + side, B.hex_color(c['wraps']),
                    B.hex_color(c['wraps_band'])), zmin=0.08, zmax=entry.get('wraps_height'), offset=0.012,
                    thickness=0.012)
        if wraps == 'boots' or entry.get('boots'):
            G.shell(mesh, arm, 'Boot_' + side, ['calf_' + side, 'foot_' + side, 'ball_' + side],
                    _mat(entry, 'boots', G.leather), zmax=0.36, offset=0.008, thickness=0.006)
        if wraps == 'greaves':      # over boots, when both
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
        if entry.get('bracers') or entry.get('leather_bracers'):
            G.shell(mesh, arm, 'Bracer_' + side, ['lowerarm_' + side], _mat(entry, 'bracers', G.cloth),
                    offset=0.008, thickness=0.006)
    helmet(entry, mesh, arm)
    gear_hat = entry.get('headgear') if isinstance(entry.get('headgear'), str) else None
    if gear_hat:
        headgear(entry, gear_hat, mesh, arm)
    for b in entry.get('bands', []):
        where, part = (b, b) if isinstance(b, str) else (b[0], b[1])
        bw = b[2] if not isinstance(b, str) and len(b) > 2 else None     # optional width (m)
        bones, z = G.BANDS[where]
        tint = part in entry.get('tint', []) or part == 'tint'
        G.band(mesh, arm, 'Band_' + where, bones, (G.cloth('band_' + where, (1, 1, 1), tint=True) if tint else
               G.leather('band_' + where, B.hex_color(c.get(part, c['belt'])))), z,
               width=bw or (0.05 if where in ('waist', 'neck') else 0.045))
    if entry.get('beard'):
        G.beard(mesh, B.hex_color(c['hair']), length=0.018, density=700)
    if entry.get('pelt_cape'):
        G.pelt(mesh, arm, B.hex_color(entry['pelt_cape'][0]), B.hex_color(entry['pelt_cape'][1]), length=0.03,
               kind=entry.get('pelt_kind', 'shoulders'))
    if entry.get('fur_vest'):
        G.pelt(mesh, arm, B.hex_color(entry['fur_vest'][0]), B.hex_color(entry['fur_vest'][1]), length=0.025,
               density=4000, kind='vest')
    if entry.get('topknot'):
        # 'topknot' or 'ponytail' on a shaven head (set 'hair_asset': None)
        G.topknot(mesh, arm, G.hide('knot', B.hex_color(c['hair']), tuple(v * 0.6 for v in B.hex_color(c['hair']))),
                  kind=entry['topknot'], length=entry.get('tail_length', 0.3))
    if entry.get('crown_hair'):
        G.crown_hair(mesh, B.hex_color(c['hair']), length=0.06, density=7000)
    if entry.get('mane'):
        # over a full hair mesh (no scalp shows from above): long falling
        # strands, locks forward over the shoulders, a rough strand coat
        G.falling_hair(mesh, B.hex_color(c['hair']), length=entry['mane'])
        G.front_locks(mesh, B.hex_color(c['hair']), length=entry['mane'] * 1.1)
    if entry.get('roughen_hair', True):
        G.roughen_hair(mesh, B.hex_color(c['hair']), length=0.012, density=2500)
    # a chest emblem: ('cross' | 'disc', 'tint' | 'gold' | hex[, size])
    em = entry.get('chest_emblem')
    if em:
        col = em[1]
        if col == 'tint':
            emat = G.cloth('emblem', (1, 1, 1), tint=True)
        elif col == 'gold':
            emat = B.material('emblem_gold', B.hex_color(c.get('gold', '#c8a040')), roughness=0.3, metallic=1.0)
        else:
            emat = G.cloth('emblem', B.hex_color(c.get(col, col)))
        G.chest_emblem(mesh, arm, emat, kind=em[0], size=em[2] if len(em) > 2 else 0.16,
                       lift=0.045 if entry.get('cuirass') else 0.03)
    # hem bands: [(garment, color), ...]; garment 'bottom' or 'top', color
    # 'tint', 'gold' or a hex; optional width (m) and inset from the edge
    for h in entry.get('hem_bands', []):
        part, col = h[0], h[1]
        width = h[2] if len(h) > 2 else 0.05
        inset = h[3] if len(h) > 3 else 0.0
        if col == 'tint':
            hm = G.cloth('hem_' + part, (1, 1, 1), tint=True)
        elif col == 'gold':
            hm = B.material('hem_gold', B.hex_color(c.get('gold', '#c8a040')), roughness=0.35, metallic=0.9)
        else:
            hm = G.cloth('hem_' + part, B.hex_color(c.get(col, col)))
        G.hem(part.capitalize(), hm, width=width, inset=inset)


def dress_posed(entry, mesh, arm):
    """Garments placed after posing (rigid parts hung on bones), then the
    hanging cloth draped by simulation onto the posed body."""
    hanging = [o for o in bpy.data.objects if o.name in ('Bottom',)] if entry.get('bottom') in ('skirt', 'kilt') else []
    if entry.get('bottom') == 'loincloth':
        lengths = entry.get('loin_length', (0.42, 0.36))
        maker = G.leather if entry.get('bottom_material') == 'leather' else (lambda n, col, tint=False: G.cloth(n, col, tint=tint, weave=0.6))
        hanging += G.loincloth(mesh, arm, _mat(entry, 'bottom', maker), length=lengths[0], back_length=lengths[1],
                               torn=entry.get('torn', True))
    if entry.get('cape'):
        G.cape(mesh, arm, _mat(entry, 'cape', lambda n, col, tint=False: G.cloth(n, col, tint=tint, weave=0.6)),
               length=entry.get('cape_length', 0.75))
    hanging = [o for o in hanging if not o.get('no_drape')]
    if hanging and entry.get('drape', True):
        G.drape(hanging, mesh)


def headgear(entry, kind, mesh, arm):
    """One of the riders' headgear kinds (riders.HEADGEAR: hats, shakos,
    modern helmets, headdresses ...) on a foot unit: placed on the unposed
    head from the mesh (its brow line below the top of the head) and fixed to
    the head bone, so it follows the pose and fits any build."""
    import riders
    from mathutils import Matrix
    if kind not in riders.HEADGEAR:
        print('unknown headgear', kind)
        return None
    c = dict(_colors(entry))
    tl = entry.get('tint', [])
    p = P.Prop(f'headgear_{kind}', c, {'hat': {'grime': 0.5}, 'helmet': {'grime': 0.4, 'rust': 0.15},
                                      'tint': {'grime': 0.45}})
    riders.HEADGEAR[kind](p, c, 'hat' in tl or 'helmet' in tl, 'crest' in tl or 'plume' in tl,
                          **entry.get('headgear_params', {}))
    obj = p.finish()
    top, yc, _ = G._head(mesh)
    k = entry.get('headgear_scale', 1.0)
    sx, sy, sz = (k, k, k) if isinstance(k, (int, float)) else k
    s = 1.12 * G._scale             # a little larger than life, so it keeps its silhouette at 1x
    brow = top - entry.get('brow_drop', 0.085) * G._scale
    # the hat's +Y is its front; the unposed body faces -y
    obj.matrix_world = (Matrix.Translation((0, yc + 0.004 * G._scale, brow)) @ Matrix.Rotation(math.pi, 4, 'Z')
                        @ Matrix.Diagonal((sx * s, sy * s, sz * s, 1.0)))
    world = obj.matrix_world.copy()
    obj.parent = arm
    obj.parent_type = 'BONE'
    obj.parent_bone = 'head'
    obj.matrix_world = world
    H._update()
    return obj


def helmet(entry, mesh, arm):
    """Headgear fitted to the head: a shell of the scalp above the brow."""
    kind = entry.get('helmet')
    if not kind:
        return
    me = mesh.data
    body = mesh.vertex_groups.get('body')
    top = max(v.co.z for v in me.vertices if any(g.group == body.index and g.weight > 0.5 for g in v.groups))
    metal = kind in entry.get('metal', ['helmet']) or 'helmet' in entry.get('metal', ['helmet'])
    if kind == 'crown':
        gold = B.material('crown', B.hex_color(_colors(entry).get('gold', '#c8a040')), roughness=0.3, metallic=1.0)
        gem = entry.get('crown_gems')
        G.crown(mesh, arm, gold, kind=entry.get('crown', 'spiked'), height=entry.get('crown_height', 0.07),
                gems=B.hex_color(gem) if gem else None)
        return
    if kind == 'great_helm':
        # a closed flat-topped helm down to the neck, with an eye slit
        tint = 'helmet' in entry.get('tint', [])
        gm = B.material('helmet', (1, 1, 1) if tint else B.hex_color(_colors(entry)['helmet']),
                        roughness=0.5, metallic=0 if tint else 0.85, tint=tint)
        _, yc, _ = G._head(mesh)

        def keep(co, top=top / G._scale, yc=yc / G._scale):
            d = top - co.z
            return not (co.y - yc < -0.05 and 0.085 < d < 0.105 and abs(co.x) < 0.065)   # the eye slit
        G.shell(mesh, arm, 'Helmet', ['head', 'neck_01'], gm, zmin=(top - 0.27) / G._scale, offset=0.035,
                thickness=0.01, min_weight=0.15, keep=keep)
    if kind in ('galea', 'morion'):
        metalmat = B.material('helmet', B.hex_color(_colors(entry)['helmet']), roughness=0.5, metallic=0.85)
        G.shell(mesh, arm, 'Helmet', ['head', 'neck_01'], metalmat, zmin=(top - 0.09) / G._scale, offset=0.018,
                thickness=0.008, min_weight=0.2)
        if kind == 'galea':
            tint = 'crest' in entry.get('tint', [])
            c2 = _colors(entry)
            G.crest(mesh, arm, G.hide('crest', (1, 1, 1) if tint else B.hex_color(c2['crest']),
                                      (0.8, 0.8, 0.8) if tint else tuple(v * 0.6 for v in B.hex_color(c2['crest'])),
                                      tint=tint), height=0.07, length=0.2, width=0.035)
        else:
            G.brim(mesh, arm, metalmat)
    if kind == 'cap' and entry.get('cap_brim'):
        tint = 'helmet' in entry.get('tint', [])
        bm_ = G.cloth('brim', (1, 1, 1), tint=True) if tint else G.leather('brim', B.hex_color(_colors(entry)['helmet']))
        G.brim(mesh, arm, bm_, radius=0.15, lift=0.06, curl=0.0, comb=0)
    if entry.get('cap_knob'):
        # a gold knob on the crown of the cap
        G.knob(mesh, arm, B.material('knob', B.hex_color(_colors(entry).get('gold', '#c8a040')), roughness=0.3,
                                     metallic=1.0))
    if entry.get('plume'):
        tint = 'plume' in entry.get('tint', [])
        col = (1, 1, 1) if tint else B.hex_color(_colors(entry).get('plume', '#e8e0d0'))
        G.plume(mesh, arm, G.hide('plume', col, tuple(v * 0.75 for v in col), tint=tint))
    if kind in ('crested', 'horned', 'cap', 'wolf', 'hood'):
        depth = {'cap': 0.07, 'crested': 0.082, 'horned': 0.09, 'wolf': 0.12, 'hood': 0.19}[kind]
        if kind == 'wolf':
            mat = G.fur('helmet', B.hex_color(_colors(entry)['pelt']), B.hex_color(_colors(entry)['pelt_dark']))
        elif kind == 'hood' or not metal:
            mat = _mat(entry, 'helmet', G.cloth if kind == 'hood' else G.leather)
        else:
            tint = 'helmet' in entry.get('tint', [])
            mat = B.material('helmet', (1, 1, 1) if tint else B.hex_color(_colors(entry)['helmet']),
                             roughness=0.62, metallic=0.75, tint=tint)
        keep = None
        if kind == 'hood':
            _, yc, _ = G._head(mesh)

            def keep(co, top=top / G._scale, yc=yc / G._scale):
                d = top - co.z
                return not (co.y - yc < -0.03 and 0.045 < d < 0.24 and abs(co.x) < 0.075)
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
        G.shell(mesh, arm, 'Helmet', ['head', 'neck_01'] + (['clavicle_l', 'clavicle_r', 'spine_03'] if kind == 'hood' else []),
                mat, zmin=(top - depth) / G._scale if kind != 'hood' else 1.36,
                offset={'hood': 0.03, 'cap': 0.01}.get(kind, 0.02),
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
        G.shell(mesh, arm, 'Helmet', ['head'], _mat(entry, 'helmet', G.leather), zmin=(top - 0.09) / G._scale, zmax=(top - 0.055) / G._scale,
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
             ('upperarm_l', (0.3, -0.06, -1)), ('lowerarm_l', (0.18, -0.25, -1)),
             ('upperarm_r', (-0.42, -0.12, -1)), ('lowerarm_r', (-0.25, -1.0, -0.4))],
    'upright': [('spine_02', (0, -0.02, 1)), ('neck_01', (0, 0.22, 1)), ('head', (0, 0.5, 1)),
                ('clavicle_l', (1, -0.25, 0.05)), ('clavicle_r', (-1, -0.25, 0.05)),
                ('thigh_l', (0.14, -0.08, -1)), ('calf_l', (0.1, 0.02, -1)), ('foot_l', (0.35, -1, -0.3)),
                ('thigh_r', (-0.14, 0.04, -1)), ('calf_r', (-0.1, 0.06, -1)), ('foot_r', (-0.35, -1, -0.3)),
                ('upperarm_l', (0.2, -0.45, -1)), ('lowerarm_l', (-0.55, -1.0, 0.25)),
                ('upperarm_r', (-0.62, -0.1, -1)), ('lowerarm_r', (-0.45, -1.0, 0.5))],
    'low_across': [('spine_02', (0, -0.08, 1)), ('neck_01', (0, 0.05, 1)), ('head', (0, 0.3, 1)),
                   ('thigh_l', (0.18, -0.1, -1)), ('calf_l', (0.12, 0.04, -1)), ('foot_l', (0.4, -1, -0.3)),
                   ('thigh_r', (-0.18, 0.04, -1)), ('calf_r', (-0.12, 0.06, -1)), ('foot_r', (-0.4, -1, -0.3)),
                   ('upperarm_l', (0.25, -0.2, -1)), ('lowerarm_l', (0.2, -0.85, -0.6)),
                   ('upperarm_r', (-0.22, -0.1, -1)), ('lowerarm_r', (0.15, -0.6, -0.8))],
}

# Seated on a horse: thighs forward and apart around the barrel, shins
# down, hands forward at the reins (the weapon hand set by `equip`).
STANCES['ride'] = [('spine_02', (0, -0.05, 1)), ('neck_01', (0, -0.04, 1)), ('head', (0, 0.06, 1)),
                   ('thigh_l', (0.42, -0.85, -0.35)), ('calf_l', (0.22, 0.12, -1)), ('foot_l', (0.25, -1, -0.2)),
                   ('thigh_r', (-0.42, -0.85, -0.35)), ('calf_r', (-0.22, 0.12, -1)), ('foot_r', (-0.25, -1, -0.2)),
                   ('upperarm_l', (0.25, -0.5, -1)), ('lowerarm_l', (0.05, -1.0, -0.1)),
                   ('upperarm_r', (-0.3, -0.35, -1)), ('lowerarm_r', (-0.15, -1.0, 0.35))]

STANCES['bow_low'] = [('spine_01', (0, 0.06, 1)), ('spine_02', (0, 0.08, 1)), ('neck_01', (0, 0.15, 1)), ('head', (0, 0.45, 1)),
                      ('clavicle_l', (1, 0.05, 0.12)), ('clavicle_r', (-1, 0.05, 0.12)),
                      ('thigh_l', (0.3, -0.18, -1)), ('calf_l', (0.2, 0.05, -1)), ('foot_l', (0.5, -1, -0.3)),
                      ('thigh_r', (-0.22, 0.12, -1)), ('calf_r', (-0.12, 0.05, -1)), ('foot_r', (-0.5, -1, -0.3)),
                      ('upperarm_l', (0.2, -0.12, -1)), ('lowerarm_l', (0.18, -1.0, -0.5)),
                      ('upperarm_r', (-0.2, -0.04, -1)), ('lowerarm_r', (-0.12, -0.45, -1))]

STANCES['guard'] = [('spine_02', (0, -0.04, 1)), ('neck_01', (0, 0.1, 1)), ('head', (0, 0.35, 1)),
                    ('clavicle_l', (1, -0.2, 0.05)), ('clavicle_r', (-1, -0.2, 0.05)),
                    ('thigh_l', (0.24, -0.12, -1)), ('calf_l', (0.16, 0.04, -1)), ('foot_l', (0.45, -1, -0.3)),
                    ('thigh_r', (-0.24, 0.06, -1)), ('calf_r', (-0.16, 0.06, -1)), ('foot_r', (-0.45, -1, -0.3)),
                    ('upperarm_l', (0.3, -0.2, -1)), ('lowerarm_l', (-0.65, -0.75, 0.1)),
                    ('upperarm_r', (-0.35, -0.15, -1)), ('lowerarm_r', (-0.3, -0.9, -0.45))]

STANCES['arms_folded'] = [('spine_02', (0, -0.02, 1)), ('neck_01', (0, 0.1, 1)), ('head', (0, 0.35, 1)),
                          ('clavicle_l', (1, -0.15, 0.05)), ('clavicle_r', (-1, -0.15, 0.05)),
                          ('thigh_l', (0.2, -0.06, -1)), ('calf_l', (0.14, 0.04, -1)), ('foot_l', (0.45, -1, -0.3)),
                          ('thigh_r', (-0.2, 0.0, -1)), ('calf_r', (-0.14, 0.04, -1)), ('foot_r', (-0.45, -1, -0.3)),
                          ('upperarm_l', (0.2, -0.5, -1)), ('lowerarm_l', (-1.0, -0.25, 0.25)),
                          ('upperarm_r', (-0.2, -0.55, -1)), ('lowerarm_r', (1.0, -0.35, 0.3))]

_ROYAL_BASE = [('spine_02', (0, 0.04, 1)), ('neck_01', (0, 0.06, 1)), ('head', (0, 0.16, 1)),
               ('clavicle_l', (1, 0.05, 0.08)), ('clavicle_r', (-1, 0.05, 0.08)),
               ('thigh_l', (0.16, -0.06, -1)), ('calf_l', (0.1, 0.04, -1)), ('foot_l', (0.45, -1, -0.3)),
               ('thigh_r', (-0.16, 0.02, -1)), ('calf_r', (-0.1, 0.04, -1)), ('foot_r', (-0.45, -1, -0.3))]
STANCES['fists_on_hips'] = _ROYAL_BASE + [('upperarm_l', (1.0, 0.15, -0.8)), ('lowerarm_l', (-0.66, 0.1, -0.75)),
                                          ('upperarm_r', (-1.0, 0.15, -0.8)), ('lowerarm_r', (0.66, 0.1, -0.75))]
STANCES['hands_clasped'] = _ROYAL_BASE + [('upperarm_l', (0.18, -0.25, -1)), ('lowerarm_l', (-0.55, -0.75, -0.35)),
                                          ('upperarm_r', (-0.18, -0.25, -1)), ('lowerarm_r', (0.55, -0.75, -0.35))]
STANCES['hands_behind'] = _ROYAL_BASE + [('upperarm_l', (0.2, 0.35, -1)), ('lowerarm_l', (-0.5, 0.7, -0.4)),
                                         ('upperarm_r', (-0.2, 0.35, -1)), ('lowerarm_r', (0.5, 0.7, -0.4))]
STANCES['royal'] = _ROYAL_BASE + [('upperarm_l', (0.25, -0.1, -1)), ('lowerarm_l', (0.15, -0.6, -0.9)),
                                  ('upperarm_r', (-0.3, -0.2, -1)), ('lowerarm_r', (-0.1, -1.0, 0.45))]

# long guns: at the order (butt on the ground by the right foot, the right
# hand round the barrel) and at the port (diagonal across the chest, the
# right hand on the wrist of the stock, the left on the fore-end)
STANCES['order_arms'] = [('spine_02', (0, -0.02, 1)), ('neck_01', (0, 0.1, 1)), ('head', (0, 0.3, 1)),
                         ('clavicle_l', (1, 0.05, 0.08)), ('clavicle_r', (-1, 0.05, 0.08)),
                         ('thigh_l', (0.14, -0.04, -1)), ('calf_l', (0.08, 0.03, -1)), ('foot_l', (0.4, -1, -0.3)),
                         ('thigh_r', (-0.14, 0.0, -1)), ('calf_r', (-0.08, 0.03, -1)), ('foot_r', (-0.4, -1, -0.3)),
                         ('upperarm_l', (0.2, -0.05, -1)), ('lowerarm_l', (0.1, -0.15, -1)),
                         ('upperarm_r', (-0.32, -0.08, -1)), ('lowerarm_r', (-0.12, -0.45, -1))]
STANCES['port_arms'] = [('spine_02', (0, -0.05, 1)), ('neck_01', (0, 0.08, 1)), ('head', (0, 0.3, 1)),
                        ('clavicle_l', (1, 0.0, 0.08)), ('clavicle_r', (-1, 0.0, 0.08)),
                        ('thigh_l', (0.2, -0.1, -1)), ('calf_l', (0.12, 0.05, -1)), ('foot_l', (0.5, -1, -0.3)),
                        ('thigh_r', (-0.2, 0.06, -1)), ('calf_r', (-0.12, 0.05, -1)), ('foot_r', (-0.5, -1, -0.3)),
                        ('upperarm_l', (0.2, -0.3, -1)), ('lowerarm_l', (-0.3, -1.0, 0.3)),
                        ('upperarm_r', (-0.3, -0.12, -1)), ('lowerarm_r', (0.25, -0.9, -0.35))]
# a rifle at the hip, muzzle forward: upright and alert, the right hand on
# the grip at the right hip, the left forearm under the fore-end
STANCES['hip_hold'] = [('spine_02', (0, 0.0, 1)), ('neck_01', (0, 0.1, 1)), ('head', (0, 0.32, 1)),
                       ('clavicle_l', (1, 0.0, 0.08)), ('clavicle_r', (-1, 0.0, 0.08)),
                       ('thigh_l', (0.2, -0.12, -1)), ('calf_l', (0.12, 0.04, -1)), ('foot_l', (0.5, -1, -0.3)),
                       ('thigh_r', (-0.2, 0.08, -1)), ('calf_r', (-0.12, 0.05, -1)), ('foot_r', (-0.5, -1, -0.3)),
                       ('upperarm_l', (0.25, -0.2, -1)), ('lowerarm_l', (-0.25, -1.0, -0.15)),
                       ('upperarm_r', (-0.3, 0.15, -1)), ('lowerarm_r', (0.0, -1.0, -0.25))]
GUNS = ('musket', 'rifle', 'rifle_bolt', 'assault_rifle', 'smg')

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
        if stance == 'order_arms':
            # standing upright through the fist, the butt by the right foot
            foot = arm.matrix_world @ arm.pose.bones['foot_r'].head
            # the gun's origin is the wrist of the stock, `butt` above its butt
            butt = entry.get('butt', 0.38) * params.get('scale', 1.0)
            base = Vector((at.x - 0.02, at.y, foot.z - 0.07 * G._scale + butt))
            H.hold(obj, arm, 'hand_r', base, up=tuple((at - base).normalized()), forward=(0, -1, 0))
        elif stance == 'hip_hold':
            # level at the hip, muzzle forward and a little across
            d = Vector(entry.get('gun_dir', (0.15, -1.0, 0.08))).normalized()
            H.hold(obj, arm, 'hand_r', at, up=tuple(d), forward=(0, 0, 1))
            H.reach(arm, 'l', at + d * entry.get('fore_end', 0.3) * G._scale, elbow_out=0.6)
            H.fists(arm, sides='l')
        elif stance == 'port_arms':
            # diagonal across the chest, muzzle up at the left shoulder, sights out
            d = Vector(entry.get('gun_dir', (0.6, -0.3, 0.75))).normalized()
            H.hold(obj, arm, 'hand_r', at, up=tuple(d), forward=(0, -1, 0.2))
            H.reach(arm, 'l', at + d * entry.get('fore_end', 0.42) * G._scale + Vector((0, -0.03, 0)))
            H.fists(arm, sides='l')
        elif stance == 'bow_low':
            # a loose arrow in the right hand, point down and forward
            H.hold(obj, arm, 'hand_r', at, up=tuple(entry.get('hang_dir', (-0.15, -0.55, -0.8))))
        elif stance in ('hang', 'guard'):
            # hanging from the fist, down and out to the side, head low
            H.hold(obj, arm, 'hand_r', at, up=tuple(entry.get('hang_dir', (-0.7, -0.45, -0.5))))
        elif stance == 'upright' and name == 'staff':
            # a walking staff, upright beside the body
            H.hold(obj, arm, 'hand_r', at, up=(-0.04, 0.0, 1.0))
        elif stance == 'upright':
            # upright through the fist, butt on the ground by the right foot
            H.hold(obj, arm, 'hand_r', at, up=tuple(entry.get('upright_dir', (-0.05, 0.0, 1.0))))
        elif stance == 'royal':
            # a scepter, cane or standard upright in the raised right fist
            H.hold(obj, arm, 'hand_r', at, up=tuple(entry.get('upright_dir', (0.0, -0.15, 1.0))))
        else:
            # a tool held diagonally across the body, working end down at the
            # lower right of the picture (the figure's left), the left hand low
            # on the haft and the right hand further up it
            low = H.grip_point(arm, 'hand_l')
            up_dir = Vector(entry.get('tool_dir', (-0.7, -0.25, 0.65))).normalized()    # toward the haft's top end
            H.hold(obj, arm, 'hand_l', low, up=tuple(-up_dir))
            if entry.get('two_hands'):
                # the right hand further up the haft: the forearm reaches for it
                target = low + up_dir * 0.4 * G._scale
                eb = arm.matrix_world @ arm.pose.bones['lowerarm_r'].head
                H.aim(arm, 'lowerarm_r', arm.matrix_world.inverted().to_3x3() @ (target - eb))
                H.fists(arm, sides='r')
        made.append(obj)
    if offhand:
        name, params = (entry['offhand_mesh'] if entry.get('offhand_mesh') else (offhand, {}))
        obj = P.make(name, c, **params)
        if offhand.startswith('shield'):
            # strapped on the left forearm, which crosses the body: the board's
            # center just in front of the forearm (so the arm is behind it, in
            # the straps), upright, facing the front, turned 45 degrees in its
            # plane so the X of its emblem bands reads as a + cross. Placed by
            # its geometry's center (the prop's origin is the strap).
            pb = arm.pose.bones['lowerarm_l']
            fa = (arm.matrix_world @ pb.head).lerp(arm.matrix_world @ pb.tail, 0.6)
            at = fa + Vector((0.0, -entry.get('shield_out', 0.09) * G._scale, 0.03 * G._scale))
            if entry.get('shield_at') == 'left_hip':
                # a big shield low at the left hip, close to the body: from
                # behind it then stays beside the body, below the shoulders
                chest = arm.matrix_world @ arm.pose.bones['spine_02'].head
                shoulder = arm.matrix_world @ arm.pose.bones['clavicle_l'].head
                at = Vector((0.5 * (fa.x + chest.x) + 0.08, min(chest.y - 0.2 * G._scale, fa.y - 0.07 * G._scale),
                             shoulder.z - entry.get('shield_drop', 0.4) * G._scale))
            yaw = entry.get('shield_yaw', 0.0)       # turned toward the front-left, so it's never edge-on
            # both the board's facing and its in-plane 45 degree axis turn about world Z
            sy, cy = math.sin(math.radians(yaw)), math.cos(math.radians(yaw))
            # round shields turn 45 degrees in their plane (the emblem's X reads
            # as a +); tall ones (scutum, oval, heater) stand upright
            roll = math.radians(entry.get('shield_roll', 45.0 if any(k in name for k in ('round', 'aspis', 'buckler', 'oval'))
                                          else 0.0))
            sr, cr = math.sin(roll), math.cos(roll)
            H.hold(obj, arm, 'lowerarm_l', at, up=(sr * cy, -sr * sy, cr), forward=(sy, cy, 0.0), center=True)
        else:
            at = H.grip_point(arm, 'hand_l')
            if offhand == 'bow':
                # thicker limbs, so the bow reads at 1x
                obj.scale = (1.8, 1.8, 1.0)
                bpy.context.view_layer.update()
                obj.select_set(True)
                bpy.context.view_layer.objects.active = obj
                bpy.ops.object.transform_apply(scale=True)
            # held low and level across the front of the hips, pointing forward-left,
            # its grip deep in the fist
            at = H.grip_point(arm, 'hand_l', along=0.35)
            H.hold(obj, arm, 'hand_l', at, up=tuple(entry.get('offhand_dir', (1.0, -0.35, 0.05))))
        made.append(obj)
    back = entry.get('back')
    if back and back not in ('quiver', 'backpack', 'pelt') and back in P.BUILDERS:
        # any other prop worn on the back (mil_backpack, a parachute pack ...),
        # in the backpack's convention: straps' top at the origin, the load behind (-Y)
        obj = P.make(back, c, **entry.get('back_params', {}))
        sp = arm.matrix_world @ arm.pose.bones['spine_03'].head
        at = sp + Vector((0.0, 0.04 * G._scale, entry.get('pack_up', 0.1) * G._scale))
        H.hold(obj, arm, 'spine_03', at, up=(0, 0.05, 1), forward=(0, -1, 0))
        made.append(obj)
    elif back in ('quiver', 'backpack'):
        obj = P.make(back, c, **entry.get('back_params', {}))
        # on the upper back (+y behind the unposed body), strap point at the
        # shoulder blades; a quiver slung diagonally, arrows up over the right shoulder
        sp = arm.matrix_world @ arm.pose.bones['spine_03'].head
        if back == 'quiver':
            at = sp + Vector((-0.06, 0.17 * G._scale, 0.3 * G._scale))
            obj.scale = (1.3, 1.3, 1.3)
            bpy.context.view_layer.update()
            H.hold(obj, arm, 'spine_03', at, up=(-0.4, 0.1, 1), forward=(0, -1, 0))
        else:
            k = entry.get('pack_scale', 1.0)
            if k != 1.0:
                obj.scale = (k, k, k)
                bpy.context.view_layer.update()
            # a big load high on the back: the prop's origin is its straps'
            # top at the shoulders, the load behind it (its -Y, turned to +y)
            at = sp + Vector((0.0, 0.04 * G._scale, entry.get('pack_up', 0.1) * G._scale))
            H.hold(obj, arm, 'spine_03', at, up=(0, 0.05, 1), forward=(0, -1, 0))
        made.append(obj)
    elif back == 'pelt':
        pass  # TODO: a pelt over the shoulders
    # more gear at the belt: [(prop, params, side), ...]; side 'r' (right
    # hip), 'back' or 'l'; hung like the scabbard (+Z down and back)
    for item in entry.get('hip_gear', []):
        name, params = item[0], (item[1] if len(item) > 1 else {})
        side = item[2] if len(item) > 2 else 'r'
        obj = P.make(name, c, **params)
        pv = arm.matrix_world @ arm.pose.bones['pelvis'].head
        x = {'r': -0.17, 'l': 0.17, 'back': 0.06}[side] * G._scale
        y = (0.12 if side == 'back' else 0.02) * G._scale
        at = pv + Vector((x, y, 0.05 * G._scale))
        H.hold(obj, arm, 'pelvis', at, up=(0.0, 0.25, -0.97), forward=(-1 if side == 'r' else 1, 0, 0) if side != 'back'
               else (0, 1, 0))
        made.append(obj)
    # gear worn on the chest (a bandolier of flasks ...): origin mid-chest, +Y out
    chest = entry.get('chest_gear')
    if chest:
        name, params = chest[0], (chest[1] if len(chest) > 1 else {})
        obj = P.make(name, c, **params)
        sp = arm.matrix_world @ arm.pose.bones['spine_03'].head
        at = sp + Vector((0.0, -entry.get('chest_out', 0.17) * G._scale, -0.02 * G._scale))
        H.hold(obj, arm, 'spine_03', at, up=(0, 0, 1), forward=(0, -1, 0))
        made.append(obj)
    hip = entry.get('hip')
    if hip:
        # a sheathed sword at the left hip: the scabbard's mouth at the belt,
        # hanging back and down at an angle
        name, params = hip
        obj = P.make(name, c, **params)
        pv = arm.matrix_world @ arm.pose.bones['pelvis'].head
        at = pv + Vector((0.17 * G._scale, -0.04 * G._scale, 0.05 * G._scale))
        H.hold(obj, arm, 'pelvis', at, up=(0.15, 0.45, -0.88), forward=(1, 0, 0))
        made.append(obj)
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
    # 'headgear' names a riders.HEADGEAR kind (the old catalog's own headgear lists are not read)
    gear_hat = entry.get('headgear') if isinstance(entry.get('headgear'), str) else None
    helmeted = entry.get('helmet') in ('hood', 'wolf', 'crested', 'horned', 'cap') or gear_hat
    hair = entry.get('hair_asset', HAIR.get(entry.get('hair'), 'short02'))
    if helmeted and entry.get('hair') != 'long':
        hair = None
    gender = entry.get('gender', 1.0)
    mesh, arm = H.body(gender=gender, age=entry.get('age', 0.5), **macro,
                       race=entry.get('race', {'caucasian': 0.4, 'african': 0.3, 'asian': 0.3}),
                       skin=_skin_base(c['skin'], female=gender < 0.5), hair=hair,
                       skin_tone=B.hex_color(_calm_skin(c['skin'])), hair_tone=B.hex_color(c['hair']))
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
