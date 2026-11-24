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
    'wraps': '#6a4a30', 'wraps_band': '#3a2618', 'boots': '#4e3624', 'sandal': '#44301e', 'shoes': '#1e1a16',
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
    if maker is G.cloth and 'cloth_rough' in entry:       # a duller, less glossy cloth
        kw = dict(kw, rough=entry['cloth_rough'])
    if maker is G.cloth and entry.get('cloth_dirt'):      # filthy cloth: 0..1
        kw = dict(kw, dirt=entry['cloth_dirt'])
    return maker(f'{part}', color, *args, tint=tint, **kw)


def _calm_skin(hexcol, sat=0.7):
    """Bare skin with its saturation scaled: the skin shader pushes saturated
    swatches orange, and dark ones maroon."""
    import colorsys
    h = hexcol.lstrip('#')
    r, g, b = (int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))
    hh, ll, ss = colorsys.rgb_to_hls(r, g, b)
    if ll < 0.35:
        # dark skin rendered maroon: pull its hue toward a neutral brown
        hh = 0.075 + (hh - 0.075) * 0.3 if hh < 0.5 else 0.075
        ss = min(ss, 0.32)
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
    if top in ('tunic', 'sleeved', 'mantle', 'vest', 'exomis'):
        bones = list(torso)
        if top in ('tunic', 'mantle', 'exomis'):
            bones += ['upperarm_l', 'upperarm_r']
        if top == 'sleeved':
            bones += ['upperarm_l', 'upperarm_r', 'lowerarm_l', 'lowerarm_r']
        keep = None
        if top in ('tunic', 'mantle'):
            # short sleeves: the upper half of the upper arm ('sleeve_x': where they end)
            keep = lambda co, sx=entry.get('sleeve_x', 0.27): abs(co.x) < sx
        elif top == 'exomis':
            # the Greek working tunic: pinned on the left shoulder, the right
            # shoulder, arm and the top of the chest bare (a line from the left
            # side of the neck down to under the right arm, front and back)
            keep = lambda co: abs(co.x) < 0.27 and co.z < 1.27 + 1.15 * (co.x + 0.15)
        # (a 'physique' body's shoulders sit higher and wider: the top rises with them)
        zmax = entry.get('top_zmax', 1.47 + 0.07 * entry.get('physique', 0.0))
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
                    B.material('mail', B.hex_color(c.get('mail', '#6a6a6e')), roughness=entry.get('mail_roughness', 0.55), metallic=entry.get('mail_metallic', 0.85),
                               nodes=G.mail_rows_nodes if entry.get('mail_rows') else G.mail_nodes),
                    zmin=1.42, zmax=1.62, offset=0.014, thickness=0.006)
    elif top == 'strap':
        # a sash over the left shoulder, across the chest
        G.shell(mesh, arm, 'Top', torso, _mat(entry, 'top', G.cloth), zmin=0.95, zmax=1.45, offset=0.009,
                thickness=0.004, keep=lambda co: abs((co.z - 1.2) - 0.9 * co.x) < 0.07)
    if bottom == 'gown':
        G.skirt(mesh, arm, 'Bottom', _mat(entry, 'bottom', G.cloth, weave=0.6), length=0.7,
                flare=entry.get('gown_flare', 1.55), folds=0.1, drop=entry.get('robe_drop', 0.5))
    if bottom in ('skirt', 'kilt'):
        G.skirt(mesh, arm, 'Bottom', _mat(entry, 'bottom', G.cloth, weave=0.6),
                length=entry.get('skirt_length', 0.55 if bottom == 'skirt' else 0.7),
                flare=entry.get('skirt_flare', 1.12 + 0.3 * entry.get('robe_drop', 0.0)),
                folds=entry.get('skirt_folds', 0.08), drop=entry.get('robe_drop', 0.0),
                troughs=entry.get('skirt_troughs'))
    elif bottom == 'loincloth':
        pass    # flaps from the belt, after posing (dress_posed)
    elif bottom in ('long', 'breeches'):
        # trousers to the ankle, or breeches to below the knee (puffed with 'puffy')
        puff = entry.get('trouser_offset', 0.022 if entry.get('puffy') else 0.008)    # hakama: ~0.05
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
        G.shell(mesh, arm, 'Veil', ['head', 'neck_01'], vmat, zmin=(topz - entry.get('veil_drop', 0.26)), zmax=(topz - entry.get('veil_top', 0.115)), offset=0.012,
                thickness=0.004, min_weight=0.2, keep=lambda co, yc=yc / G._scale: co.y - yc < 0.02)
    for g in entry.get('gear', []):
        name = g[0] if isinstance(g, (list, tuple)) else g
        if name == 'v_collar':
            # (PC2 numpy-mdj-foot) a kimono's crossed collar (eri): two bands down the front of the
            # chest from either side of the neck, meeting low at the centre in a V; 'tint' or a
            # 'color', 'width' (m), 'depth' (the V's bottom), 'spread' (its half-width at the neck)
            gp = (g[1] or {}) if isinstance(g, (list, tuple)) and len(g) > 1 else {}
            vt = gp.get('tint')
            vmat = G.cloth('v_collar', (1, 1, 1), tint=True) if vt else G.cloth('v_collar', B.hex_color(gp.get('color', c['belt'])))
            w, z0, sp = gp.get('width', 0.035) / 2, gp.get('depth', 1.2), gp.get('spread', 0.075)
            z1 = 1.56
            for sgn in (1.0, -1.0):
                inside = (lambda co, sg=sgn: co.y < -0.02 and z0 - 0.02 < co.z < z1 and
                          abs(co.x - sg * sp * (co.z - z0) / (z1 - z0)) < w)
                o = G.shell(mesh, arm, 'VCollar', torso + ['neck_01'], vmat, zmin=z0 - 0.04, zmax=z1, offset=0.012,
                            thickness=0.005, keep=lambda co, sg=sgn: co.y < -0.0 and
                            abs(co.x - sg * sp * (co.z - z0) / (z1 - z0)) < w + 0.06)
                G.trim(o, inside)
            continue
        if name == 'chest_strap':
            gp = (g[1] or {}) if isinstance(g, (list, tuple)) and len(g) > 1 else {}
            col = gp.get('color', c['belt'])
            tint = 'strap' in entry.get('tint', []) or gp.get('tint')
            half = gp.get('width', 0.07) / 2           # a sash: 'width' 0.15-0.2
            smat = G.cloth('chest_strap', (1, 1, 1), tint=True) if tint else G.leather('chest_strap', B.hex_color(col))
            off = gp.get('offset', 0.02)            # raise it over thick tops
            # cut from a wider strip of the body, then trimmed to the strap's width
            # (the body's faces are wider than a strap: it came out as flecks)
            for k, sgn in enumerate((1.0, -1.0) if gp.get('cross') else (1.0,)):
                o = G.shell(mesh, arm, 'ChestStrap' + ('2' if k else ''), torso, smat, zmin=0.95, zmax=1.55,
                            offset=off + 0.004 * k, thickness=0.006,
                            keep=lambda co, h=half, sg=sgn: abs((co.z - 1.22) + sg * 0.95 * co.x) < h + 0.06)
                G.trim(o, lambda co, h=half, sg=sgn: abs((co.z - 1.22) + sg * 0.95 * co.x) < h)
                if gp.get('edge'):
                    # edges along both sides of the sash (gold-edged)
                    ew = gp.get('edge_width', 0.035)       # (thinner ones vanish at 2x)
                    emat = _hem_mat(entry, c, gp['edge'], 'strap_edge')
                    for side in (-1, 1):
                        eo = G.shell(mesh, arm, 'StrapEdge', torso, emat, zmin=0.95, zmax=1.55,
                                     offset=off + 0.004 * k + 0.009, thickness=0.004,   # over the sash's thickness
                                     keep=lambda co, h=half, sg=sgn: abs((co.z - 1.22) + sg * 0.95 * co.x) < h + 0.06)
                        G.trim(eo, lambda co, h=half, sg=sgn, sd=side, w=ew:
                               abs((co.z - 1.22) + sg * 0.95 * co.x - sd * (h - w / 2)) < w / 2)
    # mail limbs: 'mail': ['arms', 'legs'] (ring mail sleeves to the wrist,
    # chausses from the thigh to the foot), under the other clothes
    mail = entry.get('mail_parts', [])
    if mail:
        mm = B.material('mail', B.hex_color(c.get('mail', '#6a6a6e')), roughness=entry.get('mail_roughness', 0.55), metallic=entry.get('mail_metallic', 0.85),
                        nodes=G.mail_rows_nodes if entry.get('mail_rows') else G.mail_nodes)
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
        if entry.get('belt_gear', bottom != 'gown'):
            # a pale brass buckle and a lighter leather pouch than the belt, so both read at 2x
            belt_rgb = B.hex_color(c['belt'])
            G.belt_gear(mesh, arm, B.material('buckle', B.hex_color(c.get('buckle', '#c8b27a')), roughness=0.35,
                                              metallic=1.0),
                        G.leather('pouch', B.hex_color(c['pouch']) if 'pouch' in c else
                                  tuple(min(1.0, v * 1.6 + 0.06) for v in belt_rgb)), pouch=entry.get('pouch', True))
    if entry.get('pauldrons'):
        # shoulder pieces: the top of the upper arms and shoulders
        pmat = (_mat(entry, 'cuirass', lambda n, col, tint=False: B.material(n, col, roughness=entry.get('plate_roughness', 0.35),
                                                                             metallic=entry.get('plate_metallic', 0.9),
                                                                             tint=tint, nodes=G.plate_bands(3)))
                if entry['pauldrons'] == 'metal' else _mat(entry, 'top', G.leather))
        G.shell(mesh, arm, 'Pauldrons', ['upperarm_l', 'upperarm_r', 'clavicle_l', 'clavicle_r'],
                pmat, zmin=entry.get('pauldron_zmin', 1.33), offset=entry.get('pauldron_offset', 0.012),
                thickness=0.008, min_weight=0.35, keep=lambda co: abs(co.x) > entry.get('pauldron_x', 0.13))
    if entry.get('cuirass'):
        metal = 'cuirass' in entry.get('metal', ['cuirass'])
        bands = entry.get('cuirass_bands')       # lorica segmentata: N horizontal plates
        # ('plate_metallic' / 'plate_roughness': a duller plate; a mirror one reflects the dark sky as black)
        maker = (lambda n, col, tint=False: B.material(n, col, roughness=entry.get('plate_roughness', 0.35),
                                                       metallic=entry.get('plate_metallic', 0.9), tint=tint,
                                                       nodes=G.plate_bands(bands) if bands else None)) \
            if metal else (G.cloth if 'cuirass' in entry.get('patterns', {}) else G.leather)   # a camo vest is cloth
        cu = G.shell(mesh, arm, 'Cuirass', torso + (['neck_01'] if entry.get('neck') == 'high' else []),
                _mat(entry, 'cuirass', maker), zmin=0.98,
                zmax=(1.55 if entry.get('neck') == 'high' else 1.42) + 0.06 * entry.get('physique', 0.0),
                offset=entry.get('cuirass_offset', 0.03 if entry.get('puffy') else 0.016), thickness=0.008,
                keep=lambda co, w=entry.get('cuirass_width', 0.17): abs(co.x) < w)
        if metal and cu is not None and entry.get('plate_smooth', True):
            # plate is hammered smooth: no muscles (a 'muscle cuirass' sets plate_smooth False)
            sm = cu.modifiers.new('PlateSmooth', 'SMOOTH')
            sm.factor = 1.0
            sm.iterations = entry.get('plate_smooth_iterations', 12)
            cu.modifiers.move(len(cu.modifiers) - 1, 0)
    wraps = entry.get('wraps')
    for side in 'lr':
        if wraps == 'wraps':
            wtint = 'wraps' in entry.get('tint', [])
            wmat = (G.fur('wraps_' + side, (1, 1, 1), (0.55, 0.55, 0.55), tint=True) if wtint else
                    G.fur('wraps_' + side, B.hex_color(c['wraps']), B.hex_color(c['wraps_band'])))
            if entry.get('wraps_plain'):
                # (PC2 numpy-mdj-foot) single-tone cloth gaiters (kyahan), not banded wraps; off by default
                wmat = G.cloth('wraps_' + side, (1, 1, 1) if wtint else B.hex_color(c['wraps']), tint=wtint, weave=0.5)
            G.shell(mesh, arm, 'Wrap_' + side, ['calf_' + side], wmat, zmin=0.08, zmax=entry.get('wraps_height'), offset=0.012,
                    thickness=0.012)
        if wraps == 'shoes' or entry.get('shoes'):     # 'shoes': True also goes over hose (wraps)
            # low shoes: the foot only, a little above the ankle
            G.shell(mesh, arm, 'Shoe_' + side, ['foot_' + side, 'ball_' + side, 'calf_' + side],
                    _mat(entry, 'shoes', G.leather), zmax=0.11, offset=0.007, thickness=0.006)
        if wraps == 'boots' or entry.get('boots'):
            G.shell(mesh, arm, 'Boot_' + side, ['calf_' + side, 'foot_' + side, 'ball_' + side],
                    _mat(entry, 'boots', G.leather), zmax=entry.get('boots_height', 0.36), offset=0.008, thickness=0.006)
        if wraps == 'greaves':      # over boots, when both
            G.shell(mesh, arm, 'Greave_' + side, ['calf_' + side], (lambda n, col, tint=False: B.material(
                n, col, roughness=0.4, metallic=0.85, tint=tint))('greaves', B.hex_color(c['greaves'])),
                zmin=0.1, zmax=0.48, offset=0.016, thickness=0.006)
        if entry.get('sandals') and wraps not in ('boots',):
            G.shell(mesh, arm, 'Sandal_' + side, ['foot_' + side, 'ball_' + side], _mat(entry, 'sandal', G.leather),
                    zmax=entry.get('sandal_height', 0.025), offset=0.004, thickness=0.008)
            # straps laced up the ankle
            G.shell(mesh, arm, 'SandalStrap_' + side, ['foot_' + side, 'calf_' + side], _mat(entry, 'sandal', G.leather),
                    zmin=0.04, zmax=entry.get('lace_height', 0.17), offset=0.006, thickness=0.004,
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
        # optional 4th: the band's offset off the body (m; default 0.022). (realism infantry) a small one
        # (~0.014) sits flush on a sleeve: a flat armband, not a roll
        bo = {'offset': b[3]} if not isinstance(b, str) and len(b) > 3 else {}
        bones, z = G.BANDS[where]
        tint = part in entry.get('tint', []) or part == 'tint'
        G.band(mesh, arm, 'Band_' + where, bones, (G.cloth('band_' + where, (1, 1, 1), tint=True) if tint else
               G.leather('band_' + where, B.hex_color(c.get(part, c['belt'])))), z,
               width=bw or (0.05 if where in ('waist', 'neck') else 0.045), **bo)
    if entry.get('beard'):
        style = entry.get('beard_style', 'full')       # full, mustache, goatee, chin, long
        G.beard(mesh, B.hex_color(c.get('beard', c['hair'])), length=entry.get('beard_length_short', 0.018),
                density=entry.get('beard_density', 700),
                style='full' if style == 'long' else style)
        if style == 'long':
            G.long_beard(mesh, arm, B.hex_color(c.get('beard', c['hair'])), length=entry.get('beard_length', 0.16))
    if entry.get('pelt_cape'):
        G.pelt(mesh, arm, B.hex_color(entry['pelt_cape'][0]), B.hex_color(entry['pelt_cape'][1]), length=0.03,
               kind=entry.get('pelt_kind', 'shoulders'))
    if entry.get('fur_vest'):
        G.pelt(mesh, arm, B.hex_color(entry['fur_vest'][0]), B.hex_color(entry['fur_vest'][1]), length=0.025,
               density=4000, kind='vest')
    if entry.get('fur_kilt'):
        G.pelt(mesh, arm, B.hex_color(entry['fur_kilt'][0]), B.hex_color(entry['fur_kilt'][1]), length=0.03,
               density=4000, kind='kilt', bottom=1.0 - entry.get('fur_kilt_length', 0.5))   # meters below the waist
    if entry.get('spiked_hair'):
        G.spiked_hair(mesh, arm, G.hide('spikes', B.hex_color(c['hair']), tuple(v * 0.7 for v in B.hex_color(c['hair']))),
                      length=entry.get('spike_length', 0.07))
    if entry.get('shackles'):
        G.shackles(mesh, arm, B.material('iron', B.hex_color(c.get('iron', '#3e3e40')), roughness=0.5, metallic=0.85),
                   ankles=entry['shackles'] != 'wrists', thick=entry.get('shackle_thick', 1.0))
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
                       lift=0.045 if entry.get('cuirass') else 0.03, back=bool(entry.get('back_emblem')))
        if entry.get('back_emblem') == 'both':
            G.chest_emblem(mesh, arm, emat, kind=em[0], size=em[2] if len(em) > 2 else 0.16,
                           lift=0.045 if entry.get('cuirass') else 0.03)
    # a patch on the upper arm: ('l' | 'r', 'tint' | 'flag' | hex)
    sp = entry.get('shoulder_patch')
    if sp:
        side, col = sp[0], sp[1]
        if col == 'tint':
            pm = G.cloth('patch', (1, 1, 1), tint=True)
        elif col == 'flag':
            pm = G.cloth('patch', (0.9, 0.9, 0.88), pattern=('stripes', '#b02020', 22.0))
        else:
            pm = G.cloth('patch', B.hex_color(c.get(col, col)))
        G.arm_patch(mesh, arm, pm, side=side, out_by=entry.get('patch_out', 0.045 if entry.get('puffy') else 0.02),
                    size=entry.get('patch_size', 0.06), t=entry.get('patch_t', 0.3))   # (PC) size and place along the arm
    # hem bands: [(garment, color), ...]; garment 'bottom' or 'top', color
    # 'tint', 'gold' or a hex; optional width (m) and inset from the edge
    _hems(entry, c, lambda part: part != 'cape')
    # a trim along every open edge of the cuirass/vest (front, armholes, neck, hem)
    if entry.get('cuirass_trim'):
        G.hem('Cuirass', _hem_mat(entry, c, entry['cuirass_trim'], 'cuirass'), width=entry.get('trim_width', 0.025),
              edges='all')
    if entry.get('wild_hair'):
        G.wild_hair(mesh, B.hex_color(c['hair']), length=entry['wild_hair'] if entry['wild_hair'] is not True else 0.1,
                    clump=entry.get('hair_clump', 0.55), frizz=entry.get('hair_frizz', 0.0),
                    receding=entry.get('hair_receding', False))
    # gloves to the elbow ('elbow') or the whole arm ('arm'): ('arm'|'elbow', 'tint'|hex)
    lg = entry.get('long_gloves')
    if lg:
        bones = ['lowerarm_l', 'lowerarm_r', 'hand_l', 'hand_r'] + (['upperarm_l', 'upperarm_r'] if lg[0] == 'arm' else [])
        G.shell(mesh, arm, 'Gloves', bones, _hem_mat(entry, c, lg[1], 'gloves'), offset=0.006, thickness=0.004,
                min_weight=0.4)
    if entry.get('ruff'):
        # a pleated lace ruff round the neck (Elizabethan)
        G.ruff(mesh, arm, _hem_mat(entry, c, entry['ruff'], 'ruff'), size=entry.get('ruff_size', 0.11),
               style=entry.get('ruff_style', 'flat'))
    if entry.get('pockets'):
        # two patch pockets on the jacket front: a color (a shade darker than the jacket reads)
        G.pockets(mesh, arm, _hem_mat(entry, c, entry['pockets'], 'pockets'), z_at=entry.get('pocket_z', 1.02),
                  size=entry.get('pocket_size', 0.075))
    sr = entry.get('shoulder_rolls')
    if sr:
        # puffed rolls over the shoulder seams (Renaissance doublets): 'gold'|'tint'|hex
        G.shoulder_rolls(mesh, arm, _hem_mat(entry, c, sr, 'rolls'), size=entry.get('roll_size', 0.09))
    if entry.get('trouser_flare'):
        G.flare_legs('Bottom', amount=entry['trouser_flare'])
    if entry.get('yoroi'):
        # Japanese armour (yoroi.py): do, kusazuri, sode, kote, hakama, kabuto ...
        import yoroi
        yoroi.dress(entry, mesh, arm)
    fp = entry.get('face_prop')
    if fp:
        name, params = (fp, {}) if isinstance(fp, str) else (fp[0], fp[1] if len(fp) > 1 else {})
        G.face_prop(mesh, arm, P.make(name, c, **params), kind='pipe' if 'pipe' in name else 'glasses')


def _hem_mat(entry, c, col, part):
    """A trim material: 'tint', 'gold', 'ermine' (white with black tails) or a color."""
    if col == 'tint':
        return G.cloth(part + '_trim', (1, 1, 1), tint=True)
    if col == 'gold':
        # gold braid or thread, not plate: semi-metallic, so the plate's fake-sky
        # shading doesn't darken it on upright surfaces (it vanished on sashes)
        return B.material(part + '_gold', B.hex_color(c.get('gold', '#c8a040')), roughness=0.38, metallic=0.45)
    if col == 'ermine':
        return G.cloth(part + '_ermine', (0.95, 0.94, 0.9), pattern=('camo', '#141414', 40.0, 'cover', 0.12))
    return G.cloth(part + '_trim', B.hex_color(c.get(col, col)))


def _hems(entry, c, which):
    """The entry's hem bands on the garments `which(part)` accepts."""
    for h in entry.get('hem_bands', []):
        part, col = h[0], h[1]
        if not which(part):
            continue
        width = h[2] if len(h) > 2 else 0.05
        inset = h[3] if len(h) > 3 else 0.0
        G.hem(part.capitalize(), _hem_mat(entry, c, col, 'hem_' + part), width=width, inset=inset)


def dress_posed(entry, mesh, arm):
    """Garments placed after posing (rigid parts hung on bones), then the
    hanging cloth draped by simulation onto the posed body."""
    hanging = [o for o in bpy.data.objects if o.name in ('Bottom',)] if entry.get('bottom') in ('skirt', 'kilt') else []
    if entry.get('bottom') == 'loincloth':
        lengths = entry.get('loin_length', (0.42, 0.36))
        maker = G.leather if entry.get('bottom_material') == 'leather' else (lambda n, col, tint=False: G.cloth(n, col, tint=tint, weave=0.6))
        hanging += G.loincloth(mesh, arm, _mat(entry, 'bottom', maker), length=lengths[0], back_length=lengths[1],
                               torn=entry.get('torn', True))
    if entry.get('head_veil'):
        # a soft veil from the crown of the head: 'tint' | color, 'veil_length'
        G.head_veil(mesh, arm, _mat(entry, 'veil', G.cloth, weave=0.4) if entry['head_veil'] is True else
                    _hem_mat(entry, _colors(entry), entry['head_veil'], 'head_veil'),
                    length=entry.get('veil_length', 0.55), width=entry.get('veil_width', 0.36))
    if entry.get('cape'):
        G.cape(mesh, arm, _mat(entry, 'cape', G.cloth, weave=0.6),     # (the plain maker: patterns reach capes)
               length=entry.get('cape_length', 0.75), width=entry.get('cape_width', 0.5),
               flare=entry.get('cape_flare', 0.15), wrap=entry.get('cape_wrap', 0.0),
               train=entry.get('cape_train', 0.0), folds=entry.get('cape_folds', 6), depth=entry.get('cape_depth'),
               hem_curve=entry.get('cape_hem_curve', 0.0))
        _hems(entry, _colors(entry), lambda part: part == 'cape')
        cape = bpy.data.objects.get('Cape')
        if cape is not None and entry.get('cape_lining'):
            # the inner side in its own color (a dark lining under a red cape)
            sol = next((m for m in cape.modifiers if m.type == 'SOLIDIFY'), None) or cape.modifiers.new('Lining', 'SOLIDIFY')
            sol.thickness = sol.thickness or 0.006
            # the solidify's new shell is the cape's OUTER side: it takes the cape's
            # material (slot 1), the original faces (inner side) the lining (slot 0)
            outer = cape.data.materials[0]
            cape.data.materials[0] = _hem_mat(entry, _colors(entry), entry['cape_lining'], 'lining')
            cape.data.materials.append(outer)
            sol.material_offset = 1
        ce = entry.get('cape_emblem')
        if ce:
            G.cape_emblem(arm, _hem_mat(entry, _colors(entry), ce[1], 'cape_emblem'), kind=ce[0],
                          size=ce[2] if len(ce) > 2 else 0.22, at=ce[3] if len(ce) > 3 else 0.35)
    if entry.get('cape_drape') and bpy.data.objects.get('Cape') is not None:
        # (off by default) the cape hung by simulation onto the posed body too
        hanging.append(bpy.data.objects['Cape'])
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
    for key in ('hat', 'felt', 'fur'):           # the hats' cloth keys default to the entry's helmet color
        if key not in entry.get('colors', {}) and 'helmet' in entry.get('colors', {}):
            c[key] = entry['colors']['helmet']
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
    _helmet(entry, mesh, arm)
    co = entry.get('crown_over')
    if co:
        # a crown worn over the hood or helmet (Isabella's over her hood): wider and higher
        gold = B.material('crown', B.hex_color(_colors(entry).get('gold', '#c8a040')), roughness=0.3, metallic=1.0)
        gem = entry.get('crown_gems')
        G.crown(mesh, arm, gold, kind=co, height=entry.get('crown_height', 0.07), gems=B.hex_color(gem) if gem else None,
                lift=entry.get('crown_lift', 0.02), grow=entry.get('crown_grow', 0.03))


def _helmet(entry, mesh, arm):
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
                gems=B.hex_color(gem) if gem else None, lift=entry.get('crown_lift', 0.0))
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
        G.plume(mesh, arm, G.hide('plume', col, tuple(v * 0.75 for v in col), tint=tint),
                height=0.22 * entry.get('plume_scale', 1.0))
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
        if kind == 'cap' and entry.get('cap_hairline'):
            # short receding hair: the cap in the hair color, cut back from a high forehead (King Chinese)
            _, yc, _ = G._head(mesh)
            depth = 0.12

            def keep(co, top=top / G._scale, yc=yc / G._scale, front=entry['cap_hairline']):
                return co.y - yc > -front
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
        shoulders = kind == 'hood' and not entry.get('hood_neck')     # 'hood_neck': stops at the neck
        G.shell(mesh, arm, 'Helmet', ['head', 'neck_01'] + (['clavicle_l', 'clavicle_r', 'spine_03'] if shoulders else []),
                mat, zmin=(top - depth) / G._scale if kind != 'hood' else (1.36 if shoulders else 1.47),
                offset={'hood': 0.03, 'cap': 0.01}.get(kind, 0.02),
                thickness=0.008, min_weight=0.2, keep=keep)
        if kind == 'horned':
            G.horns(mesh, arm, B.material('horn', B.hex_color(_colors(entry).get('horn', '#c8b898')), roughness=0.5),
                    length=0.17, radius=0.026)
        if kind == 'crested':
            c = _colors(entry)
            tint = 'crest' in entry.get('tint', [])
            k = entry.get('crest_scale', 1.0)
            G.crest(mesh, arm, G.hide('crest', (1, 1, 1) if tint else B.hex_color(c['crest']),
                                      (0.8, 0.8, 0.8) if tint else tuple(v * 0.6 for v in B.hex_color(c['crest'])), tint=tint),
                    height=0.15 * k, width=0.06 * k)
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

_ROYAL_BASE = [('spine_02', (0, 0.06, 1)), ('neck_01', (0, 0.14, 1)), ('head', (0, 0.36, 1)),
               ('clavicle_l', (1, 0.05, 0.08)), ('clavicle_r', (-1, 0.05, 0.08)),
               ('thigh_l', (0.16, -0.06, -1)), ('calf_l', (0.1, 0.04, -1)), ('foot_l', (0.45, -1, -0.3)),
               ('thigh_r', (-0.16, 0.02, -1)), ('calf_r', (-0.1, 0.04, -1)), ('foot_r', (-0.45, -1, -0.3))]
STANCES['fists_on_hips'] = _ROYAL_BASE + [('upperarm_l', (1.0, 0.15, -0.8)), ('lowerarm_l', (-0.66, 0.1, -0.75)),
                                          ('upperarm_r', (-1.0, 0.15, -0.8)), ('lowerarm_r', (0.66, 0.1, -0.75))]
STANCES['hands_clasped'] = _ROYAL_BASE + [('upperarm_l', (0.18, -0.25, -1)), ('lowerarm_l', (-0.55, -0.75, -0.35)),
                                          ('upperarm_r', (-0.18, -0.25, -1)), ('lowerarm_r', (0.55, -0.75, -0.35))]
STANCES['hands_behind'] = _ROYAL_BASE + [('upperarm_l', (0.2, 0.35, -1)), ('lowerarm_l', (-0.5, 0.7, -0.4)),
                                         ('upperarm_r', (-0.2, 0.35, -1)), ('lowerarm_r', (0.5, 0.7, -0.4))]
# standing tall, feet close, the free arm relaxed at the side: 'stand' with
# the right hand low (an item hanging from it), 'stand_staff' with a staff,
# cane or standard upright in the right fist
STANCES['stand'] = _ROYAL_BASE + [('upperarm_l', (0.16, 0.0, -1)), ('lowerarm_l', (0.08, -0.12, -1)),
                                  ('upperarm_r', (-0.18, -0.05, -1)), ('lowerarm_r', (-0.1, -0.35, -1))]
STANCES['stand_staff'] = _ROYAL_BASE + [('upperarm_l', (0.16, 0.0, -1)), ('lowerarm_l', (0.08, -0.12, -1)),
                                        ('upperarm_r', (-0.3, -0.12, -1)), ('lowerarm_r', (-0.12, -0.9, -0.55))]
# one fist on the hip, the other arm free: left fist on hip (right hand free
# for a cane or sword hanging), and the mirror
STANCES['fist_hip_l'] = _ROYAL_BASE + [('upperarm_l', (1.0, 0.15, -0.8)), ('lowerarm_l', (-0.66, 0.1, -0.75)),
                                       ('upperarm_r', (-0.18, -0.05, -1)), ('lowerarm_r', (-0.1, -0.35, -1))]
STANCES['fist_hip_r'] = _ROYAL_BASE + [('upperarm_r', (-1.0, 0.15, -0.8)), ('lowerarm_r', (0.66, 0.1, -0.75)),
                                       ('upperarm_l', (0.16, 0.0, -1)), ('lowerarm_l', (0.08, -0.12, -1))]
# a sword held forward in both hands, the fists before the navel (chudan-no-kamae)
STANCES['chudan'] = _ROYAL_BASE + [('upperarm_l', (0.12, -0.45, -1)), ('lowerarm_l', (-0.45, -1.0, 0.1)),
                                   ('upperarm_r', (-0.2, -0.45, -1)), ('lowerarm_r', (0.3, -1.0, 0.15))]
# hasso: the fists before the right shoulder, the blade up and out over it
STANCES['hasso'] = _ROYAL_BASE + [('upperarm_l', (0.1, -0.6, -0.8)), ('lowerarm_l', (-0.6, -0.6, 0.5)),
                                  ('upperarm_r', (-0.45, -0.35, -0.82)), ('lowerarm_r', (0.3, -0.55, 0.78))]
# high chudan (seigan): the fists at the chest, the blade up and out
STANCES['seigan'] = _ROYAL_BASE + [('upperarm_l', (0.15, -0.65, -0.75)), ('lowerarm_l', (-0.35, -0.75, 0.55)),
                                   ('upperarm_r', (-0.25, -0.65, -0.75)), ('lowerarm_r', (0.15, -0.75, 0.6))]
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
                       ('upperarm_r', (-0.22, 0.45, -1)), ('lowerarm_r', (0.08, -0.8, -0.5))]
# a musket on the right shoulder: the right fist at the shoulder's front
# holding the butt, the barrel sloping back over the shoulder
STANCES['shoulder_arms'] = [('spine_02', (0, -0.02, 1)), ('neck_01', (0, 0.1, 1)), ('head', (0, 0.3, 1)),
                            ('clavicle_l', (1, 0.05, 0.08)), ('clavicle_r', (-1, 0.05, 0.08)),
                            ('thigh_l', (0.14, -0.04, -1)), ('calf_l', (0.08, 0.03, -1)), ('foot_l', (0.4, -1, -0.3)),
                            ('thigh_r', (-0.14, 0.0, -1)), ('calf_r', (-0.08, 0.03, -1)), ('foot_r', (-0.4, -1, -0.3)),
                            ('upperarm_l', (0.18, -0.02, -1)), ('lowerarm_l', (0.1, -0.15, -1)),
                            ('upperarm_r', (-0.22, -0.35, -1)), ('lowerarm_r', (0.05, -0.45, 1.0))]
# a short sword ready at the hip behind a braced shield (the Legionary): the
# right elbow bent at the side, the fist in front of the right hip, the blade
# forward and out (`sword_dir`); the left forearm level and forward, its fist on
# the shield's grip behind the boss (`shield_at` 'grip')
STANCES['shield_ready'] = [('spine_02', (0, 0.0, 1)), ('neck_01', (0, 0.1, 1)), ('head', (0, 0.32, 1)),
                           ('thigh_l', (0.16, -0.16, -1)), ('calf_l', (0.1, 0.02, -1)), ('foot_l', (0.4, -1, -0.3)),
                           ('thigh_r', (-0.2, 0.1, -1)), ('calf_r', (-0.12, 0.06, -1)), ('foot_r', (-0.55, -1, -0.3)),
                           ('upperarm_l', (0.16, -0.12, -1)), ('lowerarm_l', (-0.55, -0.8, -0.12)),
                           ('upperarm_r', (-0.35, 0.35, -1)), ('lowerarm_r', (-0.32, -0.92, 0.08))]
# a bow at the ready (the Archer): the bow arm out forward and down, the bow
# upright in the fist, an arrow nocked and the string hand at its nock, the
# arrow pointing forward and down at the ground ahead
STANCES['bow_ready'] = [('spine_02', (0, 0.0, 1)), ('neck_01', (0, 0.1, 1)), ('head', (0, 0.32, 1)),
                        ('thigh_l', (0.18, -0.14, -1)), ('calf_l', (0.12, 0.02, -1)), ('foot_l', (0.5, -1, -0.3)),
                        ('thigh_r', (-0.2, 0.1, -1)), ('calf_r', (-0.12, 0.06, -1)), ('foot_r', (-0.6, -1, -0.3)),
                        ('upperarm_l', (0.45, -0.85, -0.08)), ('lowerarm_l', (0.4, -0.9, 0.04)),
                        ('upperarm_r', (-0.2, 0.1, -1)), ('lowerarm_r', (0.2, -1.0, 0.1))]
# a weapon held ready (the melee family's realism pass): the legs a half step apart, the
# hands placed by the entry: `hand_r_at` / `hand_l_at` (metres from that shoulder, for a
# 1.75 m man; the figure faces -y, its right is -x) reached with `elbow_r` / `elbow_l`
# (0 down, 1 out); the weapon through the right fist along `weapon_dir`, its front (+Y:
# an axe's edge, a blade's flat) toward `edge_dir`; `two_hands`: the left fist on the
# haft `grip2` metres below the right
STANCES['ready'] = [('spine_02', (0, 0.0, 1)), ('neck_01', (0, 0.1, 1)), ('head', (0, 0.32, 1)),
                    ('thigh_l', (0.16, -0.14, -1)), ('calf_l', (0.1, 0.02, -1)), ('foot_l', (0.4, -1, -0.3)),
                    ('thigh_r', (-0.2, 0.08, -1)), ('calf_r', (-0.12, 0.06, -1)), ('foot_r', (-0.55, -1, -0.3)),
                    ('upperarm_l', (0.25, -0.2, -1)), ('lowerarm_l', (-0.3, -0.9, -0.3)),
                    ('upperarm_r', (-0.35, -0.1, -1)), ('lowerarm_r', (-0.2, -0.9, 0.3))]
GUNS = ('musket', 'rifle', 'rifle_bolt', 'assault_rifle', 'smg')

# The torso of every standing stance (the owner, 2026-10-16: the units were
# hunched too far forward): the spine upright with the chest a little up, the
# shoulders back, the neck straight and the head level over the hips. The
# stances' own torso aims are replaced; the arms keep theirs (armature space),
# so weapons stay where they were. Riding and digging bend for real and keep
# their torsos, as does an entry with 'stoop' (Gandhi's walk).
UPRIGHT = [('spine_01', (0, 0.0, 1)), ('spine_02', (0, 0.03, 1)), ('spine_03', (0, 0.06, 1)),
           ('clavicle_l', (1, 0.1, 0.07)), ('clavicle_r', (-1, 0.1, 0.07)),
           ('neck_01', (0, 0.08, 1)), ('head', (0, 0.3, 1))]
LEANING = ('ride', 'low_across')
TORSO = {b for b, _ in UPRIGHT}
MAX_LEAN = -0.06      # the most forward (-y) an entry's pose_extra may tip a torso bone


def stance_aims(entry, stance):
    """The bone aims of `stance` with the entry's pose_extra, the torso upright
    (see UPRIGHT)."""
    extra = [tuple(a) for a in entry.get('pose_extra', [])]
    if stance in LEANING or entry.get('stoop'):
        return STANCES[stance] + extra
    extra = [(b, (d[0], max(d[1], MAX_LEAN), d[2])) if b in TORSO else (b, d) for b, d in extra]
    return UPRIGHT + [a for a in STANCES[stance] if a[0] not in TORSO] + extra

# The posture (human.posture: the weight on one leg, the pelvis rolled over it,
# the shoulders counter-rolled, the chest and head turned a little) of each
# standing stance; an entry's 'posture' dict is laid over it ('posture': False:
# none, the aims alone). Riding, digging and stooping keep their own bodies.
STANCE_POSTURE = {
    'shield_ready': {'hip': 6.0, 'shoulder': 4.0, 'hip_turn': -6.0, 'chest_turn': 4.0, 'head_turn': 10.0,
                     'free_fwd': 0.04},
    'bow_ready': {'hip': 6.0, 'shoulder': 5.0, 'hip_turn': -8.0, 'chest_turn': -4.0, 'head_turn': 6.0},
    'ready': {'hip': 6.0, 'shoulder': 4.0, 'hip_turn': -6.0, 'chest_turn': 4.0, 'head_turn': 8.0, 'free_fwd': 0.04},
    # feet close and a level bearing (rulers, standards): a lighter weight shift
    'stand': {'hip': 3.5, 'shoulder': 2.5, 'chest_turn': 4.0, 'head_turn': 5.0},
    'royal': {'hip': 3.5, 'shoulder': 2.5, 'chest_turn': 4.0, 'head_turn': 5.0},
}


def posture_of(entry, stance):
    """The human.posture settings for `entry` in `stance` (False: none)."""
    if stance in LEANING or entry.get('stoop') or entry.get('posture') is False:
        return False
    settings = dict(STANCE_POSTURE.get(stance, {}))
    settings.update(entry.get('posture') or {})
    return settings


WEAPON_STANCE = {
    'stone_axe': 'hang', 'club': 'hang', 'sword': 'hang', 'axe': 'hang',
    'spear': 'upright', 'staff': 'upright', 'pike': 'upright',
    'shovel': 'low_across', 'bow': 'bow_low',
}


def stance_of(entry):
    """The stance units.equip poses the entry in."""
    weapon, offhand = entry.get('weapon'), entry.get('offhand')
    return entry.get('stance_name') or WEAPON_STANCE.get(weapon or ('bow' if offhand == 'bow' else None), 'hang')


def equip(entry, arm):
    c = _colors(entry)
    weapon = entry.get('weapon')
    offhand = entry.get('offhand')
    props = entry.get('props', {})
    stance = entry.get('stance_name') or WEAPON_STANCE.get(weapon or ('bow' if offhand == 'bow' else None), 'hang')
    H.posture(arm, stance_aims(entry, stance), posture_of(entry, stance), scale=G._scale)
    H.fists(arm, sides='lr')
    bulk = entry.get('upper_bulk')
    if bulk:
        H.bulk(arm, bulk)
    if stance == 'ready':
        # the hands where the entry puts them, from each shoulder
        for side in ('r', 'l'):
            off = entry.get(f'hand_{side}_at')
            if off:
                sh = arm.matrix_world @ arm.pose.bones['upperarm_' + side].head
                H.reach(arm, side, sh + Vector(off) * G._scale, elbow_out=entry.get(f'elbow_{side}', 0.6))
        H.fists(arm, sides='lr')
    if stance == 'bow_ready' and entry.get('hand_l_at'):
        # (ranged family, off by default) the bow fist placed from the left shoulder, as 'ready'
        # places its hands: a low forward ready hold that shows the bow from every side
        sh = arm.matrix_world @ arm.pose.bones['upperarm_l'].head
        H.reach(arm, 'l', sh + Vector(entry['hand_l_at']) * G._scale, elbow_out=entry.get('elbow_l', 0.3))
        H.fists(arm, sides='l')
    made = []
    nocked = None          # the arrow of 'bow_ready', nocked once the bow is in the left hand
    if weapon:
        name, params = props.get(weapon, (weapon, {}))
        params = dict(params)
        if 'weapon_scale' in entry and isinstance(entry['weapon_scale'], (int, float)):
            params.setdefault('scale', entry['weapon_scale'] * 0.87)
        if name == 'war_club':
            import clubs
            obj = clubs.war_club(c, **params)
        else:
            if name not in P.BUILDERS:
                import yoroi    # noqa: F401  (registers katana_drawn, katana_saya)
            obj = P.make(name, c, **params)
        at = H.grip_point(arm, 'hand_r')
        if stance == 'order_arms':
            # standing upright through the fist, the butt by the right foot
            foot = arm.matrix_world @ arm.pose.bones['foot_r'].head
            # the gun's origin is the wrist of the stock, `butt` above its butt
            butt = entry.get('butt', 0.38) * params.get('scale', 1.0)
            base = Vector((at.x - 0.02, at.y, foot.z - 0.07 * G._scale + butt))
            H.hold(obj, arm, 'hand_r', base, up=tuple((at - base).normalized()), forward=(0, -1, 0))
        elif stance == 'shoulder_arms':
            # the barrel back over the right shoulder, sights up
            d = Vector(entry.get('gun_dir', (-0.12, 0.55, 0.83))).normalized()
            H.hold(obj, arm, 'hand_r', at, up=tuple(d), forward=(0, 0, 1))
        elif stance == 'hip_hold':
            # level at the hip, muzzle forward and a little across
            d = Vector(entry.get('gun_dir', (0.12, -1.0, 0.16))).normalized()
            H.hold(obj, arm, 'hand_r', at, up=tuple(d), forward=(0, 0, 1))
            H.reach(arm, 'l', at + d * entry.get('fore_end', 0.24) * G._scale, elbow_out=0.75)
            H.fists(arm, sides='l')
        elif stance == 'port_arms':
            # diagonal across the chest, muzzle up at the left shoulder, sights out
            d = Vector(entry.get('gun_dir', (0.6, -0.3, 0.75))).normalized()
            H.hold(obj, arm, 'hand_r', at, up=tuple(d), forward=(0, -1, 0.2))
            H.reach(arm, 'l', at + d * entry.get('fore_end', 0.42) * G._scale + Vector((0, -0.03, 0)))
            H.fists(arm, sides='l')
        elif stance == 'shield_ready':
            # ready at the hip: the point forward and out, a little up; the edges up
            # and down (a thrusting grip), so the blade's flat shows from the side
            d = Vector(entry.get('sword_dir', (-0.62, -0.75, -0.12))).normalized()
            H.hold(obj, arm, 'hand_r', at, up=tuple(d), forward=tuple(entry.get('edge_dir', (0, 0, 1))))
        elif stance == 'bow_ready':
            nocked = obj
        elif stance == 'ready':
            d = Vector(entry.get('weapon_dir', (-0.25, 0.1, 0.96))).normalized()
            H.hold(obj, arm, 'hand_r', at, up=tuple(d), forward=tuple(entry.get('edge_dir', (0, -1, 0))))
            if entry.get('two_hands'):
                H.reach(arm, 'l', at - d * entry.get('grip2', 0.3) * G._scale, elbow_out=entry.get('elbow_l', 0.5))
                H.fists(arm, sides='l')
        elif stance == 'bow_low':
            # a loose arrow in the right hand, point down and forward
            H.hold(obj, arm, 'hand_r', at, up=tuple(entry.get('hang_dir', (-0.15, -0.55, -0.8))))
        elif stance in ('hang', 'guard', 'stand', 'fist_hip_l'):
            # hanging from the fist, down and out to the side, head low
            hd = (-0.45, -0.25, -0.86) if stance in ('stand', 'fist_hip_l') else (-0.7, -0.45, -0.5)
            H.hold(obj, arm, 'hand_r', at, up=tuple(entry.get('hang_dir', hd)))
            # never into the ground: a long tool hanging from the fist slides up
            # through it until its lowest point clears the ground (a spade's blade)
            H._update()
            foot = arm.matrix_world @ arm.pose.bones['foot_r'].head
            ground = foot.z - 0.075 * G._scale
            low = min((obj.matrix_world @ Vector(cn)).z for cn in obj.bound_box)
            if low < ground:
                obj.matrix_world.translation.z += ground - low + 0.01
                H._update()
        elif stance == 'upright' and name == 'staff':
            # a walking staff, upright beside the body
            H.hold(obj, arm, 'hand_r', at, up=(-0.04, 0.0, 1.0))
        elif stance in ('upright', 'stand_staff'):
            # upright through the fist, butt on the ground by the right foot
            up = Vector(entry.get('upright_dir', (-0.05, 0.0, 1.0))).normalized()
            H.hold(obj, arm, 'hand_r', at, up=tuple(up))
            if stance == 'stand_staff' and entry.get('two_hands'):
                # the left hand higher up the pole (a standard held in both hands)
                H.reach(arm, 'l', at + up * 0.28 * G._scale + Vector((0.02, 0, 0)), elbow_out=0.5)
                H.fists(arm, sides='l')
        elif stance in ('chudan', 'seigan', 'hasso'):
            # a two-handed sword held forward (chudan-no-kamae): the right fist at the
            # guard, the left below it at the pommel, the blade up and forward
            d = Vector(entry.get('blade_dir', (0.0, -0.72, 0.7))).normalized()
            H.hold(obj, arm, 'hand_r', at, up=tuple(d), forward=tuple(entry.get('edge_dir', (0, 0.3, -1))))
            H.reach(arm, 'l', at - d * entry.get('grip2', 0.17) * G._scale, elbow_out=0.45)
            H.fists(arm, sides='l')
        elif stance == 'royal':
            # a scepter, cane or standard upright in the raised right fist
            H.hold(obj, arm, 'hand_r', at, up=tuple(entry.get('upright_dir', (0.0, -0.15, 1.0))))
        else:
            # a tool held diagonally across the body, working end down at the
            # lower right of the picture (the figure's left), the left hand low
            # on the haft and the right hand further up it
            low = H.grip_point(arm, 'hand_l')
            up_dir = Vector(entry.get('tool_dir', (-0.7, -0.25, 0.65))).normalized()    # toward the haft's top end
            # 'tool_face': where the tool's flat side (its +Y) faces, so a blade shows broad
            # with two hands, the tool slides up through the left fist (low on
            # the haft) so its top grip sits by the right hand
            shift = entry.get('grip_shift', 0.42) * G._scale if entry.get('two_hands') else 0.0
            H.hold(obj, arm, 'hand_l', low + up_dir * shift, up=tuple(-up_dir),
                   forward=tuple(entry.get('tool_face', (0, -1, 0))))
            if entry.get('two_hands'):
                # the right hand further up the haft: the forearm reaches for it
                # the right hand near the top grip, up the haft from the left fist
                target = low + up_dir * (shift - entry.get('second_grip', 0.06) * G._scale)
                H.reach(arm, 'r', target, elbow_out=0.6)
                H.fists(arm, sides='r')
        made.append(obj)
        wt = entry.get('weapon_tip')
        if wt:
            # a colored band at the top end of the held pole (a tinted stick tip)
            zmax = max(cn[2] for cn in obj.bound_box)
            bpy.ops.mesh.primitive_cylinder_add(radius=0.026, depth=0.07, vertices=12, location=(0, 0, 0))
            tip = bpy.context.active_object
            tip.data.materials.append(_hem_mat(entry, c, wt, 'tip'))
            tip.parent = obj
            tip.matrix_parent_inverse.identity()
            tip.location = (0.0, 0.0, zmax - 0.05)
            sc = obj.matrix_world.to_scale()
            tip.scale = (1.0 / sc[0], 1.0 / sc[1], 1.0 / sc[2]) if min(sc) > 0 else (1, 1, 1)
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
            if entry.get('shield_at') == 'back':
                # slung on the back, face out, below the shoulder blades
                sp3 = arm.matrix_world @ arm.pose.bones['spine_03'].head
                at = sp3 + Vector((0.0, 0.16 * G._scale, -0.08 * G._scale))
            elif entry.get('shield_at') == 'left_hip':
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
            if entry.get('shield_at') == 'back':
                # on the back: facing out behind (+y), held by the chest
                H.hold(obj, arm, 'spine_03', at, up=(sr, 0.0, cr), forward=(0, 1, 0), center=True)
            elif entry.get('shield_at') == 'grip':
                # held by the grip behind the boss (a scutum): the left fist closed on
                # the grip bar (`grip_depth` behind the prop's origin), the board
                # upright in front of it, tipped back by `shield_tilt` degrees
                back = Vector((sy, cy, 0.0))
                tilt = math.radians(entry.get('shield_tilt', 0.0))
                up = Vector((sr * cy, -sr * sy, cr)) * math.cos(tilt) + back * math.sin(tilt)
                at = H.grip_point(arm, 'hand_l', along=0.5) - back * entry.get('grip_depth', 0.04)
                H.hold(obj, arm, 'hand_l', at, up=tuple(up), forward=tuple(back))
            else:
                H.hold(obj, arm, 'lowerarm_l', at, up=(sr * cy, -sr * sy, cr), forward=(sy, cy, 0.0), center=True)
        else:
            at = H.grip_point(arm, 'hand_l')
            if offhand == 'bow':
                # thicker limbs, so the bow reads at 1x
                k = entry.get('bow_thick', 1.8)
                obj.scale = (k, entry.get('bow_depth', k), 1.0)   # (depth: the bend and the draw scale with it)
                bpy.context.view_layer.update()
                obj.select_set(True)
                bpy.context.view_layer.objects.active = obj
                bpy.ops.object.transform_apply(scale=True)
            at = H.grip_point(arm, 'hand_l', along=0.35)
            if offhand == 'bow' and stance == 'bow_ready':
                # upright in the fist, the back of the bow toward where the arrow points
                aim_dir = Vector(entry.get('arrow_dir', (0.12, -1.0, -0.5))).normalized()
                up = Vector(entry.get('bow_up', (0.2, -0.1, 1.0)))
                up = (up - aim_dir * up.dot(aim_dir)).normalized()
                H.hold(obj, arm, 'hand_l', at, up=tuple(up), forward=tuple(aim_dir))
                if nocked is not None:
                    _nock(entry, arm, obj, nocked, up, aim_dir)
            else:
                # held low and level across the front of the hips, pointing forward-left,
                # its grip deep in the fist
                H.hold(obj, arm, 'hand_l', at, up=tuple(entry.get('offhand_dir', (1.0, -0.35, 0.05))))
        made.append(obj)
    if entry.get('ankle_chain'):
        # (off by default; the Enslaved Worker) a short sagging chain between the ankles' irons
        H._update()
        la = arm.matrix_world @ arm.pose.bones['calf_l'].tail
        ra = arm.matrix_world @ arm.pose.bones['calf_r'].tail
        mid = (la + ra) / 2 + Vector((0.0, 0.0, 0.05 * G._scale))
        span = (la - ra).length
        obj = P.make('shackles', c, span=span, **(entry['ankle_chain'] if isinstance(entry['ankle_chain'], dict) else {}))
        xdir = (la - ra).normalized()
        H.hold(obj, arm, 'calf_l', mid, up=(0, 0, 1), forward=tuple(Vector((0, 0, 1)).cross(xdir)))
        made.append(obj)
    back = entry.get('back')
    if back and back not in ('quiver', 'backpack', 'pelt') and back in P.BUILDERS:
        # any other prop worn on the back (mil_backpack, a parachute pack ...),
        # in the backpack's convention: straps' top at the origin, the load behind (-Y)
        obj = P.make(back, c, **entry.get('back_params', {}))
        sp = arm.matrix_world @ arm.pose.bones['spine_03'].head
        if 'sheath' in back or 'scabbard' in back:
            # a sword slung on the back: the mouth (origin) behind the right
            # shoulder, the hilt up over it, the blade (+Z) down across the back
            at = sp + Vector((-0.1 * G._scale, 0.12 * G._scale, 0.12 * G._scale))
            t = math.radians(entry.get('back_tilt', 20.0))     # degrees off vertical, hilt over the right shoulder
            H.hold(obj, arm, 'spine_03', at, up=(math.sin(t), 0.05, -math.cos(t)), forward=(0, 1, 0))
        else:
            at = sp + Vector((0.0, 0.04 * G._scale, entry.get('pack_up', 0.1) * G._scale))
            H.hold(obj, arm, 'spine_03', at, up=(0, 0.05, 1), forward=(0, -1, 0))
        made.append(obj)
    elif back in ('quiver', 'backpack'):
        obj = P.make(back, c, **entry.get('back_params', {}))
        # on the upper back (+y behind the unposed body), strap point at the
        # shoulder blades; a quiver slung diagonally, arrows up over the right shoulder
        sp = arm.matrix_world @ arm.pose.bones['spine_03'].head
        if back == 'quiver':
            qa = entry.get('quiver_at')   # (the realism pass) a unit's own place for it (a hip quiver), offset from spine_03
            at = sp + (Vector(qa) * G._scale if qa else Vector((-0.06, 0.17 * G._scale, 0.3 * G._scale)))
            obj.scale = (1.3, 1.3, 1.3)
            bpy.context.view_layer.update()
            H.hold(obj, arm, 'spine_03', at, up=tuple(entry.get('quiver_up', (-0.4, 0.1, 1))), forward=(0, -1, 0))
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
        hx, hy, hz = entry.get('hip_at', (0.17, -0.04, 0.05))     # the scabbard's mouth from the pelvis
        at = pv + Vector((hx * G._scale, hy * G._scale, hz * G._scale))
        if entry.get('hand_on_hilt'):
            # ready to draw: the sword sloped lower (the hilt forward and up at
            # the belt), the right hand across the body on the grip, the left
            # on the scabbard by its mouth, thumb at the guard
            H.hold(obj, arm, 'pelvis', at, up=tuple(entry.get('hip_dir', (0.18, 0.55, -0.82))), forward=(1, 0, 0))
            s = params.get('scale', 1.0)
            grip = obj.matrix_world @ Vector((0.0, 0.0, -0.12 * s))
            mouth = obj.matrix_world @ Vector((0.0, 0.0, 0.11 * s))
            H.reach(arm, 'r', grip, elbow_out=0.7)
            H.reach(arm, 'l', mouth, elbow_out=0.8)
            H.fists(arm, sides='lr')
        else:
            H.hold(obj, arm, 'pelvis', at, up=tuple(entry.get('hip_dir', (0.15, 0.45, -0.88))), forward=(1, 0, 0))
        made.append(obj)
    return made


def _nock(entry, arm, bow, arrow, up, aim_dir):
    """Nocks `arrow` (nock at its origin, along +Z) on `bow`'s string: the
    shaft resting on the bow hand, pointing along `aim_dir`; the right fist
    brought to the nock, holding it."""
    H._update()
    # the string at the grip's height: the bow's vertices furthest behind its grip (-Y)
    near = [v.co for v in bow.data.vertices if abs(v.co.z) < 0.06]
    brace = -min(c.y for c in near)
    grip = bow.matrix_world.translation.copy()
    side = up.cross(aim_dir).normalized()          # across the bow, toward the archer's left
    rest = grip + up * entry.get('arrow_rest', 0.04) + side * 0.012
    nock = rest - aim_dir * brace
    # the right fist on the nock (twice: reaching moves the wrist, the fist is past it)
    for _ in range(3):
        g = H.grip_point(arm, 'hand_r')
        wrist = arm.matrix_world @ arm.pose.bones['hand_r'].head
        H.reach(arm, 'r', nock - (g - wrist), elbow_out=entry.get('draw_elbow', 0.85))
    H.fists(arm, sides='r')
    H.hold(arrow, arm, 'hand_r', nock, up=tuple(aim_dir), forward=tuple(up))


def build_footman(entry, seated=False):
    """The posed, dressed and equipped human of a catalog entry; returns the
    root. `seated`: in the riding pose, not planted on the ground; the root's
    origin is then at the pelvis (put it on the saddle's 'rider' empty)."""
    if seated:
        entry = dict(entry, stance_name='ride')
    for kit in entry.get('kits', []):
        # modules of props and hats the entry uses (e.g. 'mdj_kit'), registered on import
        __import__(kit)
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
                       skin_tone=B.hex_color(_calm_skin(c['skin'])), hair_tone=B.hex_color(c['hair']),
                       # 'physique' 0..1: broad shoulders, a V torso, big chest and shoulder muscles
                       physique=entry.get('physique', 0.0))
    H.grime(mesh, amount=entry.get('grime', 0.4))
    if entry.get('face_glow'):   # (the realism pass) the face lit under a brim or turban
        H.skin_glow(mesh, B.hex_color(c['skin']), entry['face_glow'])
    hs = entry.get('hair_scale')
    if hs and hair:
        # a bigger head of hair (Einstein's mop): the hair asset scaled about the head's centre
        top, yc, _ = G._head(mesh)
        from mathutils import Vector
        centre = Vector((0.0, yc, top - 0.1 * G._scale))
        for o in H.assets(mesh):
            if o.name.lower().endswith(hair.lower()):
                for v in o.data.vertices:
                    v.co = centre + (v.co - centre) * hs
    dress(entry, mesh, arm)
    G.store_rest()                 # the garments' rest positions, for patterns
    equip(entry, arm)
    dress_posed(entry, mesh, arm)
    G.store_rest()                 # garments made after posing (capes, flaps): as they are
    ff = entry.get('face_fill')
    if ff:
        # (realism infantry) a soft point light before the face, on the head bone: lifts a face shaded by a
        # brim (a morion, a broad hat) so it never renders as a black blob. Watts; invisible to the camera.
        from mathutils import Vector
        H._update()
        hb = arm.pose.bones['head']
        head = arm.matrix_world @ hb.head
        ld = bpy.data.lights.new('FaceFill', 'POINT')
        ld.energy = ff
        ld.shadow_soft_size = 0.2
        ld.use_shadow = False
        lo = bpy.data.objects.new('FaceFill', ld)
        bpy.context.collection.objects.link(lo)
        lo.visible_camera = False
        lo.location = head + Vector((0.0, -entry.get('face_fill_at', 0.4) * G._scale, 0.08 * G._scale))
        H._update()
        world = lo.matrix_world.copy()
        lo.parent = arm
        lo.parent_type = 'BONE'
        lo.parent_bone = 'head'
        lo.matrix_world = world
    root = H.rooted(arm)
    if seated:
        # the pelvis on the root's origin
        H._update()
        pelvis = arm.matrix_world @ arm.pose.bones['pelvis'].head
        root.location -= pelvis
        H._update()
    else:
        H.plant(root, mesh)
        # props standing on the ground by the unit (a hound, a crate, a launcher):
        # [(prop, params, (x, y) meters in the unit's frame, yaw degrees)]; the
        # unit faces +y, x is its left; they turn with the unit
        for gp in entry.get('ground_props', []):
            name, params = gp[0], (gp[1] if len(gp) > 1 else {})
            x, y = gp[2] if len(gp) > 2 else (0.5, 0.0)
            yaw = gp[3] if len(gp) > 3 else 0.0
            obj = P.make(name, c, **params)
            obj.parent = root
            obj.location = (x * G._scale, y * G._scale, 0.0)
            obj.rotation_euler = (0.0, 0.0, math.radians(yaw))
        H._update()
    return root
