"""Japanese armour and dress for the foot units (Blender side): the samurai's
tosei-gusoku, built from real parts so the MDJ units can share it.

    units.dress calls yoroi.dress(entry, mesh, arm) when the entry has 'yoroi':

    'yoroi': {
        'do': {...},          # the cuirass: lacquered horizontal lames (okegawa-do)
        'kusazuri': {...},    # the tassets: laced panels of lames from the do's hem
        'sode': {...},        # the shoulder boards: laced lames, hung on the upper arms
        'kote': {...},        # the armoured sleeves: mail on cloth, a forearm and hand plate
        'haidate': {...},     # the thigh apron: small lames over the hakama
        'hakama': {...},      # wide pleated trousers, gathered under the knee
        'suneate': {...},     # iron splint shin guards over gaiters
        'kabuto': {...},      # the helmet: ridged bowl, peak, flared shikoro, fukigaeshi, kuwagata
        'menpo': {...},       # the half mask with its throat guard
        'sashimono': {...},   # the back banner
        'wakizashi': {...},   # the short sword through the obi (the katana is units' 'hip')
    }

Every piece is optional (leave its key out) and takes its own parameters
(see each function). Colours come from the entry's 'colors': 'lacquer' (the
plates), 'lace' (the odoshi; the civ colour when 'lace' is in the entry's
'tint'), 'iron', 'gold', 'hakama', 'kote', 'menpo', 'banner' (civ when
'banner' is in 'tint'), 'mon'. Heights are for a 1.75 m man, as garments.py's.

The building block is laced_rows(obj, ...): it cuts any shell or skirt into
rows of lames (real steps in the surface, each lame's lower edge standing out
over the next) and gives each row a lacquered plate edge and its lacing.
"""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector

import bscene as B
import garments as G
import human as H
import props as P


# ------------------------------------------------------------ materials

def lacquer(name, color, tint=False, gloss=0.3):
    """Urushi lacquer: a deep colour under a glossy clear coat, with a faint
    hammered unevenness (the plates under it are hand-forged)."""
    def nodes(tree, bsdf):
        n = tree.nodes
        bsdf.inputs['Coat Weight'].default_value = gloss
        bsdf.inputs['Coat Roughness'].default_value = 0.1
        bsdf.inputs['Specular IOR Level'].default_value = 0.3
        tex = n.new('ShaderNodeTexNoise')
        tex.inputs['Scale'].default_value = 40.0
        tex.inputs['Detail'].default_value = 3.0
        bump = n.new('ShaderNodeBump')
        bump.inputs['Strength'].default_value = 0.06
        tree.links.new(tex.outputs['Fac'], bump.inputs['Height'])
        tree.links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
        tree.links.new(bump.outputs['Normal'], bsdf.inputs['Coat Normal'])
    return B.material(name, color, roughness=0.42, tint=tint, nodes=nodes)


def odoshi(name, color=(1, 1, 1), tint=True):
    """Silk lacing braid: cloth with a strong sheen and vertical cords (the
    braids run up and down through the lames)."""
    def nodes(tree, bsdf):
        n = tree.nodes
        bsdf.inputs['Sheen Weight'].default_value = 0.6
        bsdf.inputs['Sheen Roughness'].default_value = 0.35
        coord = n.new('ShaderNodeTexCoord')
        wave = n.new('ShaderNodeTexWave')
        wave.wave_type = 'BANDS'
        wave.bands_direction = 'X'
        wave.inputs['Scale'].default_value = 90.0
        wave.inputs['Distortion'].default_value = 0.5
        tree.links.new(coord.outputs['Object'], wave.inputs['Vector'])
        bump = n.new('ShaderNodeBump')
        bump.inputs['Strength'].default_value = 0.35
        tree.links.new(wave.outputs['Fac'], bump.inputs['Height'])
        tree.links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
        # darker between the braids (a cord structure that survives the 2x scale)
        mul = n.new('ShaderNodeMix')
        mul.data_type = 'RGBA'
        mul.blend_type = 'MULTIPLY'
        mul.inputs['Factor'].default_value = 0.25
        mul.inputs['A'].default_value = bsdf.inputs['Base Color'].default_value
        tree.links.new(wave.outputs['Fac'], mul.inputs['B'])
        tree.links.new(mul.outputs['Result'], bsdf.inputs['Base Color'])
    return B.material(name, color, roughness=0.55, tint=tint, nodes=nodes)


def iron(name, color):
    return B.material(name, color, roughness=0.45, metallic=0.85)


def _c(entry):
    import units
    return units._colors(entry)


def lace_mat(entry, key='lace'):
    """The entry's odoshi: civ colour when `key` is in its 'tint', else its colour."""
    if key in entry.get('tint', []):
        return odoshi('odoshi_' + key)
    return odoshi('odoshi_' + key, B.hex_color(_c(entry).get(key, '#2a3a6a')), tint=False)


def plate_mat(entry, key='lacquer'):
    return lacquer('lacquer_' + key, B.hex_color(_c(entry).get(key, '#1a1614')))


# ------------------------------------------------------------ mesh helpers

def _mesh_space(mesh, arm):
    return mesh.matrix_world.inverted() @ arm.matrix_world


def _bone_line(mesh, arm, bones):
    """The rest positions (mesh space) of the heads of `bones` and the tail of the last."""
    inv = _mesh_space(mesh, arm)
    pts = [inv @ arm.data.bones[b].head_local for b in bones]
    pts.append(inv @ arm.data.bones[bones[-1]].tail_local)
    return pts


def _closest(pts, co):
    """The closest point to `co` on the polyline `pts`, and its parameter (0..len-1)."""
    best, bt, bd = None, 0.0, 1e9
    for i, (a, b) in enumerate(zip(pts, pts[1:])):
        d = b - a
        u = max(0.0, min(1.0, (co - a).dot(d) / max(1e-9, d.length_squared)))
        p = a + d * u
        if (co - p).length < bd:
            best, bt, bd = p, i + u, (co - p).length
    return best, bt


def cut(obj, inside, levels=0):
    """Keeps only the faces of `obj` whose centre passes inside(centre, normal)
    (rest positions scaled to a 1.75 m man), after `levels` subdivisions."""
    if obj is None or not obj.data.polygons:
        return obj
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    for _ in range(levels):
        bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=1, use_grid_fill=True)
    bm.normal_update()
    kill = [f for f in bm.faces if not inside(f.calc_center_median() / G._scale, f.normal)]
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(obj.data)
    bm.free()
    return obj


def _mats(obj, *mats):
    """Material slot indices of `mats` on `obj` (appended if missing)."""
    out = []
    for m in mats:
        names = [s.name for s in obj.data.materials]
        if m.name not in names:
            obj.data.materials.append(m)
            names.append(m.name)
        out.append(names.index(m.name))
    return out


def laced_rows(obj, z0, z1, rows, plate, lace=None, step=0.012, edge=0.3, top_lace=0.0, levels=1, axis=None,
               centre=None):
    """Turns `obj` (a shell or skirt, in rest positions) into `rows` lames
    between heights z0 (bottom) and z1 (top) (1.75 m man): the surface is cut
    at each row's border and split there, each lame's lower edge pushed out
    by `step` over the top of the next (the overlapping lames of Japanese
    armour, real steps that catch the light). Each row is `plate` with, when
    `lace` is given, the lower 1 - `edge` of it laced (kebiki odoshi: the
    lacing covers the lame, its lacquered top edge shows as a dark line).
    `top_lace`: that much of the top (m) is lacing only (the suspension
    cords between a do and its tassets). `axis`: measure the rows along this
    direction instead of up (a sode on a raised arm); `centre`: push out from
    this vertical axis (x, y) instead of along the normals (a skirt)."""
    if obj is None or not obj.data.polygons:
        return obj
    s = G._scale
    up = Vector(axis or (0, 0, 1)).normalized()
    lo, hi = z0 * s, z1 * s
    tl = top_lace * s
    h = (hi - tl - lo) / rows
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    for _ in range(levels):
        bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=1, use_grid_fill=True)
    borders = [lo + h * k for k in range(1, rows)] + ([hi - tl] if tl > 0 else [])
    edges_lines = [lo + h * k + h * (1 - edge) for k in range(rows)] if lace is not None else []
    split_edges = []
    for zc in borders + edges_lines:
        res = bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=up * zc, plane_no=up,
                                     dist=1e-5)
        if zc in borders:
            split_edges += [e for e in res['geom_cut'] if isinstance(e, bmesh.types.BMEdge)]
    live = set(bm.edges)
    split_edges = [e for e in split_edges if e in live and e.is_valid]
    if split_edges:
        bmesh.ops.split_edges(bm, edges=split_edges)
    bm.normal_update()

    def row_of(zc):
        if zc > hi - tl:
            return -1, 0.0                      # above the lames: the suspension lacing (or plate)
        if zc < lo:
            return 0, 0.0                       # below the lowest lame's foot: plate
        k = min(rows - 1, max(0, int((zc - lo) / h)))
        return k, (lo + h * (k + 1) - zc) / h   # 0 at the row's top, 1 at its bottom
    pi, li = _mats(obj, plate, lace) if lace is not None else (_mats(obj, plate)[0], None)
    face_row = {}
    for f in bm.faces:
        zc = f.calc_center_median().dot(up)
        k, t = row_of(zc)
        face_row[f] = k
        if k < 0:
            f.material_index = li if (li is not None and tl > 0) else pi
        elif li is not None and t > edge:
            f.material_index = li
        else:
            f.material_index = pi
    moves = {}
    for v in bm.verts:
        ks = [face_row[f] for f in v.link_faces]
        if not ks:
            continue
        k = max(set(ks), key=ks.count)
        if k < 0:
            continue
        t = max(0.0, min(1.0, (lo + h * (k + 1) - v.co.dot(up)) / h))
        if centre is not None:
            r = Vector((v.co.x - centre[0], v.co.y - centre[1], 0.0))
            d = r.normalized() if r.length > 1e-6 else v.normal
        else:
            d = v.normal
        moves[v] = d * step * t * s
    for v, d in moves.items():
        v.co += d
    bm.to_mesh(obj.data)
    bm.free()
    return obj


def clear_of(obj, others, gap=0.012, smooth=8):
    """Pushes each vertex of `obj` out along its normal until it lies `gap` (1.75 m man)
    outside the outermost surface of the objects named in `others` (rest positions):
    a garment laid over another that would poke through it."""
    from mathutils.bvhtree import BVHTree
    trees = []
    for name in others:
        o = bpy.data.objects.get(name)
        if o is not None and o.data.polygons:
            bm = bmesh.new()
            bm.from_mesh(o.data)
            trees.append(BVHTree.FromBMesh(bm))
            bm.free()
    if not trees or obj is None:
        return obj
    g = gap * G._scale
    me = obj.data
    need = [0.0] * len(me.vertices)
    for v in me.vertices:
        n = v.normal.normalized()
        if n.length < 0.5:
            continue
        for t in trees:
            hit = t.ray_cast(v.co + n * 0.4 * G._scale, -n, 0.8 * G._scale)
            if hit[0] is not None:
                need[v.index] = max(need[v.index], (hit[0] - v.co).dot(n) + g)
    # a smooth push (a stiff plate rides over a bulge whole, it doesn't dent round it)
    nb = [[] for _ in me.vertices]
    for e in me.edges:
        a, b = e.vertices
        nb[a].append(b)
        nb[b].append(a)
    for _ in range(smooth):
        need = [max(need[i], sum(need[j] for j in nb[i]) / len(nb[i])) if nb[i] else need[i] for i in range(len(need))]
    for v in me.vertices:
        if need[v.index] > 0:
            v.co += v.normal.normalized() * need[v.index]
    me.update()
    return obj


def _smooth(obj, iterations=8, factor=0.5, keep_boundary=True):
    """Relaxes a shell's surface (plate is hammered smooth: no muscles)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    verts = [v for v in bm.verts if not (keep_boundary and v.is_boundary)]
    for _ in range(iterations):
        bmesh.ops.smooth_vert(bm, verts=verts, factor=factor, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    bm.to_mesh(obj.data)
    bm.free()


def _rigid(obj, arm, bone):
    """Parents a rigid part to `bone`, keeping its world placement."""
    world = obj.matrix_world.copy()
    obj.parent = arm
    obj.parent_type = 'BONE'
    obj.parent_bone = bone
    obj.matrix_world = world
    H._update()
    return obj


# ------------------------------------------------------------ the do (cuirass)

def do(entry, mesh, arm, rows=5, z0=0.97, z1=1.46, mune=0.14, offset=0.034, barrel=0.035, step=0.014,
       sugake=True, rivets=0.12, gloss=0.3):
    """The okegawa-do: a barrel of black-lacquered horizontal lames round the
    torso (`rows` lames from z0 up, then the taller breast and back plate,
    `mune` m high, up to z1), smoothed into plate and let out at the waist
    (`barrel`: the hem stands this far further out than the body) so it
    hangs straight from the chest; watagami straps over the shoulders and,
    with `sugake`, pairs of lacing cords down the lames (lace colour)."""
    pm = lacquer('lacquer_do', B.hex_color(_c(entry).get('do', _c(entry).get('lacquer', '#1a1614'))), gloss=gloss)
    torso = ['spine_01', 'spine_02', 'spine_03', 'clavicle_l', 'clavicle_r', 'pelvis']
    o = G.shell(mesh, arm, 'Do', torso, pm, zmin=z0 - 0.02, zmax=z1, offset=offset, thickness=0.01)
    _smooth(o, iterations=14)
    # a barrel: below the chest, push the shell out from the torso's axis
    s = G._scale
    zc = (z1 - mune) * s
    yc = sum(w.co.y for w in o.data.vertices) / len(o.data.vertices)
    for v in o.data.vertices:
        if v.co.z < zc:
            k = (zc - v.co.z) / max(1e-6, zc - z0 * s)
            r = Vector((v.co.x, v.co.y - yc, 0.0))
            if r.length > 1e-6:
                v.co += r.normalized() * barrel * s * min(1.0, k) ** 0.8
    # each lame's foot: a line of bronze rivet heads (`rivets`: that share of the lame; 0: none)
    rm = (lacquer('do_edge', B.hex_color(_c(entry).get('rivets', '#4a3a26')), gloss=0.6) if rivets else None)
    laced_rows(o, z0, z1 - mune, rows, pm, rm, step=step, edge=1.0 - rivets if rivets else 0.3)
    o['yoroi'] = 'do'
    # the watagami: straps over the shoulders joining back to breast plate
    for sx in (1, -1):
        w = G.shell(mesh, arm, 'Watagami', ['clavicle_l', 'clavicle_r', 'spine_03', 'neck_01'], pm,
                    zmin=z1 - 0.06, zmax=1.62, offset=offset * 0.7, thickness=0.01, min_weight=0.2,
                    keep=lambda co, sx=sx: 0.06 < sx * co.x < 0.13)
        _smooth(w, iterations=4)
    if sugake:
        lm = lace_mat(entry)
        made = []
        for x in (-0.11, -0.085, 0.085, 0.11):
            for side in (-1, 1):            # front and back
                st = G.shell(mesh, arm, 'Sugake', torso, lm, zmin=z0, zmax=z1 - mune, offset=offset + 0.004,
                             thickness=0.004, keep=lambda co, x=x, sd=side: abs(co.x - x) < 0.03 and co.y * sd > -0.02)
                G.trim(st, lambda co, x=x: abs(co.x - x) < 0.007)
                made.append(st)
        # pushed out with the do's barrel: follow it from the do's own surface
        for st in made:
            _snap_out(st, o, 0.004)
    return o


def _snap_out(obj, onto, gap):
    """Moves each vertex of `obj` onto the outside of `onto` (closest point plus `gap` along its normal)."""
    from mathutils.bvhtree import BVHTree
    bm = bmesh.new()
    bm.from_mesh(onto.data)
    tree = BVHTree.FromBMesh(bm)
    for v in obj.data.vertices:
        hit = tree.find_nearest(v.co)
        if hit[0] is not None:
            v.co = hit[0] + hit[1] * (0.01 + gap) * G._scale
    bm.free()


# ------------------------------------------------------------ kusazuri (tassets)

def kusazuri(entry, mesh, arm, panels=7, rows=5, length=0.42, flare=1.42, top=0.985, gap=0.07, step=0.012,
             hang=0.04, edge=0.4, over=('Hakama', 'Haidate_l', 'Haidate_r')):
    """The tassets: a skirt from MakeHuman's skirt helper from `top` (under
    the do's hem) down `length` of the helper, flared, cut into `panels`
    hanging plates (`gap` radians between them; one centred in front) of
    `rows` laced lames, the top `hang` m the suspension lacing."""
    lm, pm = lace_mat(entry), plate_mat(entry)
    o = G.skirt(mesh, arm, 'Kusazuri', lm, length=length, flare=flare, folds=0.0)
    s = G._scale
    me = mesh.data
    grp = mesh.vertex_groups.get('helper-skirt')
    idx = [v for v in me.vertices if any(g.group == grp.index and g.weight > 0.5 for g in v.groups)]
    cx = sum(v.co.x for v in idx) / len(idx)
    cy = sum(v.co.y for v in idx) / len(idx)
    width = 2 * math.pi / panels

    def inside(co, nrm):
        if co.z * s > top * s:
            return False
        a = math.atan2(co.x * s - cx, -(co.y * s - cy))     # 0 in front
        u = (a / width + 0.5) % 1.0                          # 0..1 across a panel
        return gap / width / 2 < u < 1 - gap / width / 2
    cut(o, inside, levels=2)
    zs = [v.co.z for v in o.data.vertices]
    clear_of(o, over, 0.014)
    zs = [v.co.z for v in o.data.vertices]
    laced_rows(o, min(zs) / s, max(zs) / s, rows, pm, lm, step=step, edge=edge, top_lace=hang, levels=0,
               centre=(cx, cy))
    o['yoroi'] = 'kusazuri'
    return o


# ------------------------------------------------------------ sode (shoulder boards)

def sode_mesh(p, width=0.24, height=0.27, rows=6, radius=0.11, edge=0.28, step=0.012, lace_key='lace',
              lace_tint=True, plate_key='lacquer', arc_seg=10):
    """One sode as a prop: `rows` lames curved round the arm (an arc of
    `radius` about +Z, facing +X), each a lacquered top strip over its lacing,
    each lame's foot standing `step` out over the next, under a solid
    lacquered top plate (kanmuri-ita). Origin on the arm's axis at the top,
    hanging down -Z."""
    half = width / 2 / radius            # half the arc's angle
    h = height / (rows + 0.6)

    def strip(z_top, z_bot, r_top, r_bot, th=0.007):
        rings = []
        for z, r in ((z_top, r_top), (z_bot, r_bot)):
            rings.append([(r * math.cos(a), r * math.sin(a), z)
                          for a in [(-half + 2 * half * i / arc_seg) for i in range(arc_seg + 1)]])
        bm = P.loft(rings, False, False)
        bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=th)
        return bm
    # the kanmuri-ita: the top plate, bent out at its upper edge
    p.add(strip(0.0, -0.6 * h, radius - 0.01, radius), plate_key, smooth=True, bevel=0.002)
    p.add(strip(0.012, 0.0, radius - 0.03, radius - 0.01), plate_key, smooth=True)
    for k in range(rows):
        zt = -0.6 * h - k * h
        zb = zt - h
        r0, r1 = radius + 0.001, radius + step
        zm = zt - edge * h
        rm = r0 + (r1 - r0) * edge
        p.add(strip(zt, zm, r0, rm), plate_key, smooth=True)
        p.add(strip(zm, zb, rm, r1), lace_key if not lace_tint else 'tint', smooth=True, tint=lace_tint)


def sode(entry, mesh, arm, width=0.24, height=0.27, rows=6, out=0.02, drop=0.03):
    """Both sode, hung on the upper arms (they follow them), `out` off the arm
    and their top `drop` below the shoulder joint."""
    c = dict(_c(entry))
    tinted = 'lace' in entry.get('tint', [])
    made = []
    inv = arm.matrix_world
    s = G._scale
    for sd, sx in (('l', 1.0), ('r', -1.0)):
        p = P.Prop('Sode_' + sd, c)
        _register_keys(c)
        sode_mesh(p, width=width, height=height, rows=rows, radius=0.09 + out, lace_tint=tinted)
        obj = p.finish()
        b = arm.data.bones['upperarm_' + sd]
        head, tail = inv @ b.head_local, inv @ b.tail_local
        d = (tail - head).normalized()
        o = Vector((sx, 0, 0))
        o = (o - d * o.dot(d)).normalized()
        zax = -d
        xax = o
        yax = zax.cross(xax).normalized()
        m = Matrix((xax, yax, zax)).transposed().to_4x4() @ Matrix.Diagonal((s, s, s, 1.0))
        m.translation = head + d * drop * s
        obj.matrix_world = m
        made.append(_rigid(obj, arm, 'upperarm_' + sd))
    return made


def _register_keys(c):
    """The prop material keys the kit uses (lacquer, lace, iron, gold, mon, banner ...)."""
    for key, val in (('lacquer', ('#1a1614', 0.3, 0.0, 'leather')), ('lace', ('#2a3a6a', 0.6, 0.0, 'cloth')),
                     ('mon', ('#ece8de', 0.8, 0.0, 'cloth')), ('menpo', ('#5e1c16', 0.3, 0.0, 'leather')),
                     ('tint', ('#ffffff', 0.6, 0.0, 'cloth')), ('straw', ('#a89060', 0.85, 0.0, 'rope')),
                     ('bamboo', ('#8a7240', 0.5, 0.0, 'wood'))):
        P.DEFAULTS.setdefault(key, val)


# ------------------------------------------------------------ kote, hakama, haidate, suneate

def kote(entry, mesh, arm, plates=True):
    """The armoured sleeves: cloth sewn over with mail (kusari) from the
    shoulder to the wrist, a lacquered splint plate down the back of each
    forearm and a plate on the back of the hand."""
    c = _c(entry)
    mm = B.material('kote_mail', B.hex_color(c.get('kote', '#34343a')), roughness=0.55, metallic=0.6,
                    nodes=G.mail_nodes)
    o = G.shell(mesh, arm, 'Kote', ['upperarm_l', 'upperarm_r', 'lowerarm_l', 'lowerarm_r'], mm, zmin=0.8,
                offset=0.016, thickness=0.004, min_weight=0.3)
    made = [o]
    if plates:
        pm = plate_mat(entry)
        for sd, sx in (('l', 1.0), ('r', -1.0)):
            pts = _bone_line(mesh, arm, ['lowerarm_' + sd])
            fa = G.shell(mesh, arm, 'KotePlate_' + sd, ['lowerarm_' + sd], pm, offset=0.024, thickness=0.006,
                         min_weight=0.6)

            def outer(co, nrm, sx=sx, pts=pts):
                p, t = _closest(pts, co * G._scale)
                r = co * G._scale - p
                return 0.12 < t < 0.92 and r.normalized().dot(Vector((sx * 0.55, 0.45, 0.7)).normalized()) > 0.0
            cut(fa, outer, levels=1)
            made.append(fa)
            hp = G.shell(mesh, arm, 'Tekko_' + sd, ['hand_' + sd], pm, offset=0.012, thickness=0.004, min_weight=0.7)
            hpts = _bone_line(mesh, arm, ['hand_' + sd])
            cut(hp, lambda co, nrm, sx=sx, h=hpts: _closest(h, co * G._scale)[1] < 0.55 and nrm.dot(
                Vector((sx * 0.3, 0.5, 0.8)).normalized()) > 0.2)
            made.append(hp)
    return made


def hakama(entry, mesh, arm, hem=0.4, knee=0.52, balloon=0.075, gather=0.03, pleats=0.012, n_pleats=7,
           puff_top=None):
    """Wide pleated trousers (kobakama): from the waist to `hem` (above the
    ankle), ballooning out from each leg by `balloon` m at the knee and
    gathered to `gather` at the tied hem, with pleats round the leg."""
    c = _c(entry)
    mat = G.cloth('hakama', B.hex_color(c.get('hakama', '#34303a')), weave=0.6)
    o = G.shell(mesh, arm, 'Hakama', ['pelvis', 'thigh_l', 'thigh_r', 'calf_l', 'calf_r', 'spine_01'], mat,
                zmin=hem - 0.03, zmax=1.02, offset=0.012, thickness=0.004)
    s = G._scale
    legs = {sd: _bone_line(mesh, arm, ['thigh_' + sd, 'calf_' + sd]) for sd in 'lr'}
    hip_z = legs['l'][0].z
    knee_z = legs['l'][1].z
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=1, use_grid_fill=True)
    for v in bm.verts:
        sd = 'l' if v.co.x > 0 else 'r'
        p, t = _closest(legs[sd], v.co)
        r = v.co - p
        r.z = 0.0
        if r.length < 1e-6:
            continue
        rn = r.normalized()
        z = v.co.z
        # how far out: nothing at the waist, the most at the knee, gathered at the hem
        top_z = puff_top * s if puff_top else hip_z + 0.06 * s     # where the balloon starts
        if z > knee_z:
            k = (top_z - z) / max(1e-6, top_z - knee_z)
            k = max(0.0, min(1.0, k)) ** 0.8
            amt = balloon * k
        else:
            k = (knee_z - z) / max(1e-6, knee_z - hem * s)
            k = max(0.0, min(1.0, k))
            amt = balloon + (gather - balloon) * k ** 1.6
        # pleats: ridges round the leg, deeper toward the knee
        a = math.atan2(rn.x, -rn.y)
        amt += pleats * (0.5 + 0.5 * math.cos(a * n_pleats)) * min(1.0, amt / max(1e-6, balloon) * 1.5)
        inward = rn.x * (1 if sd == 'l' else -1) < -0.2      # the inner side of the leg: less (the legs don't merge)
        if inward:
            amt *= 0.45
        v.co += rn * amt * s
    bm.to_mesh(o.data)
    bm.free()
    o['yoroi'] = 'hakama'
    return o


def haidate(entry, mesh, arm, z0=0.6, z1=0.9, rows=4, offset=0.03, step=0.008, over=('Hakama',)):
    """The thigh apron: two panels of small lacquered lames laced in rows over
    the fronts of the thighs (out over the hakama by `offset`)."""
    pm, lm = plate_mat(entry), lace_mat(entry, 'haidate_lace' if 'haidate_lace' in _c(entry) else 'lace')
    made = []
    for sd in 'lr':
        pts = _bone_line(mesh, arm, ['thigh_' + sd])
        o = G.shell(mesh, arm, 'Haidate_' + sd, ['thigh_' + sd], pm, zmin=z0, zmax=z1, offset=offset,
                    thickness=0.006, min_weight=0.5)
        cut(o, lambda co, nrm, pts=pts: (co * G._scale - _closest(pts, co * G._scale)[0]).normalized().y < -0.1)
        _smooth(o, iterations=4)
        clear_of(o, over, 0.01)
        _smooth(o, iterations=2)
        laced_rows(o, z0, z1, rows, pm, lm, step=step, edge=0.55)
        made.append(o)
    return made


def suneate(entry, mesh, arm, z0=0.08, z1=0.44, splints=5):
    """Shin guards: a dark cloth gaiter (kyahan) round the calf from the ankle
    to under the knee, and over its front `splints` iron splints (shino-suneate)."""
    c = _c(entry)
    made = []
    gm = G.cloth('kyahan', B.hex_color(c.get('kyahan', '#2a2a30')), weave=0.5)
    im = iron('suneate', B.hex_color(c.get('iron', '#3e3e42')))
    for sd in 'lr':
        pts = _bone_line(mesh, arm, ['calf_' + sd])
        g = G.shell(mesh, arm, 'Kyahan_' + sd, ['calf_' + sd], gm, zmin=z0, zmax=z1 + 0.02, offset=0.016,
                    thickness=0.004, min_weight=0.5)
        made.append(g)
        o = G.shell(mesh, arm, 'Suneate_' + sd, ['calf_' + sd], im, zmin=z0 + 0.02, zmax=z1, offset=0.026,
                    thickness=0.006, min_weight=0.5)

        def front(co, nrm, pts=pts):
            p, _ = _closest(pts, co * G._scale)
            r = co * G._scale - p
            r.z = 0.0
            if r.length < 1e-6:
                return False
            a = math.atan2(r.x, -r.y)                    # 0 in front
            if abs(a) > 1.2:
                return False
            u = (a + 1.2) / 2.4 * splints
            return 0.12 < u % 1.0 < 0.88
        cut(o, front, levels=2)
        made.append(o)
    return made


# ------------------------------------------------------------ kabuto and menpo

def kabuto_mesh(p, ridges=16, bowl=(0.122, 0.135, 0.13), lames=3, kuwagata=True, lace_tint=True,
                fukigaeshi=True, horn_height=0.2, horn_width=1.0):
    """The helmet as a prop in headgear space (origin at the brow, +Y front,
    +Z up; units.headgear's convention): a ridged suji-bachi bowl with the
    gilt tehen ring at its crown, a peak (mabizashi) over the brow, the
    shikoro of `lames` laced lames flaring out round the sides and back, the
    top lame's front ends turned back (fukigaeshi), and the gilt kuwagata
    rising in a V from a plate on the brow."""
    rx, ry, rz = bowl
    rings = []
    nseg = ridges * 4
    for j in range(10):
        el = math.radians(-6 + 90 * j / 9)          # elevation, from just under the brow to the top
        ring = []
        for i in range(nseg):
            a = 2 * math.pi * i / nseg
            rib = 1.0 + 0.022 * max(0.0, math.cos(a * ridges)) ** 6 * min(1.0, j / 2) * (1 - j / 10)
            ring.append((rx * math.cos(el) * math.sin(a) * rib, ry * math.cos(el) * math.cos(a) * rib,
                         -0.012 + rz * math.sin(el)))
        rings.append(ring)
    bm = P.loft(rings, False, True)
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.006)
    p.add(bm, 'lacquer', smooth=True)
    p.add(P.lathe([(0.0, 0.03), (0.012, 0.022), (0.016, 0.0)], seg=16), 'gold', T(0, 0, rz - 0.024), smooth=True)   # tehen
    # the peak: a band over the brow, sloping forward and down
    pk = []
    for i in range(17):
        a = math.radians(-70 + 140 * i / 16)
        pk.append([(rx * 1.01 * math.sin(a), ry * 1.01 * math.cos(a), -0.005),
                   ((rx + 0.03) * math.sin(a), (ry + 0.055) * math.cos(a), -0.035)])
    bm = P.loft([[r[0] for r in pk], [r[1] for r in pk]], False, False)
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.006)
    p.add(bm, 'lacquer', smooth=True)
    p.add(P.tube([r[1] for r in pk], 0.005, seg=6), 'gold', smooth=True)       # the peak's gilt rim (fukurin)
    # the shikoro: lames round the sides and back, each lower one wider (a flared umbrella)
    a0, a1 = math.radians(62), math.radians(298)
    z = -0.008
    for k in range(lames):
        h = 0.042
        r_top = 1.04 + 0.16 * k
        r_bot = 1.04 + 0.16 * (k + 1)
        zt, zb = z, z - h

        def ring(r, zz):
            return [(rx * r * math.sin(a0 + (a1 - a0) * i / 24), ry * r * math.cos(a0 + (a1 - a0) * i / 24), zz)
                    for i in range(25)]
        zm = zt - 0.3 * h
        rm = r_top + (r_bot - r_top) * 0.3
        for (za, ra, zb_, rb_), key in (((zt, r_top, zm, rm), 'lacquer'), ((zm, rm, zb, r_bot), 'lace')):
            bm = P.loft([ring(ra, za), ring(rb_, zb_)], False, False)
            bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.006)
            tint = key == 'lace' and lace_tint
            p.add(bm, 'tint' if tint else key, smooth=True, tint=tint)
        z = zb + 0.008
    if fukigaeshi:
        # the top lame's ends, turned back outward beside the face
        for sx in (-1, 1):
            a = a0 if sx > 0 else a1
            x0, y0 = rx * 1.06 * math.sin(a), ry * 1.06 * math.cos(a)
            out = Vector((math.sin(a), math.cos(a), 0)).normalized()
            fwd = Vector((0, 1, 0))
            # a flap: from the shikoro's edge forward and out, bent back
            pts = [Vector((x0, y0, -0.006)), Vector((x0, y0, -0.05)),
                   Vector((x0, y0, -0.05)) + out * 0.055 - fwd * 0.012, Vector((x0, y0, -0.006)) + out * 0.06 - fwd * 0.01]
            bm = bmesh.new()
            vs = [bm.verts.new(v) for v in pts]
            bm.faces.new(vs)
            bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=2, use_grid_fill=True)
            bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.007)
            p.add(bm, 'lacquer', smooth=False, bevel=0.002)
            # a gilt rim (fukurin) round the turned-back flap's outer edge
            p.add(P.tube([tuple(pts[3]), tuple(pts[2])], 0.004, seg=6), 'gold', smooth=True)
    if kuwagata:
        # the haraidate (socket plate) on the brow, the two blades in a V
        k = horn_width
        p.add(P.box(0.06 * k, 0.012, 0.045 * k), 'gold', T(0, ry + 0.012, 0.03) @ R('X', -12), bevel=0.003)
        p.add(P.ellipsoid(0.02 * k, 0.01, 0.02 * k, seg=12), 'gold', T(0, ry + 0.022, 0.035), smooth=True)  # the mon boss
        hz = horn_height / 0.2
        for sx in (-1, 1):
            path = [(sx * 0.014, ry + 0.02, 0.04), (sx * 0.05 * k, ry + 0.035, 0.1 * hz),
                    (sx * 0.09 * k, ry + 0.035, 0.16 * hz), (sx * 0.115 * k, ry + 0.025, horn_height)]
            p.add(P.tube(path, [0.016 * k, 0.024 * k, 0.02 * k, 0.006], seg=6, scale_xy=(1.0, 0.25)), 'gold',
                  smooth=True)


def T(x=0.0, y=0.0, z=0.0):
    return Matrix.Translation((x, y, z))


def R(axis, deg):
    return Matrix.Rotation(math.radians(deg), 4, axis)


def on_head(obj, mesh, arm, brow_drop=0.085, scale=1.12):
    """Places a headgear prop (origin at the brow, +Y front) on the unposed
    head and fixes it to the head bone (as units.headgear)."""
    top, yc, _ = G._head(mesh)
    s = scale * G._scale
    brow = top - brow_drop * G._scale
    obj.matrix_world = (Matrix.Translation((0, yc + 0.004 * G._scale, brow)) @ Matrix.Rotation(math.pi, 4, 'Z')
                        @ Matrix.Diagonal((s, s, s, 1.0)))
    return _rigid(obj, arm, 'head')


def kabuto(entry, mesh, arm, **params):
    c = dict(_c(entry))
    _register_keys(c)
    p = P.Prop('Kabuto', c, {'lacquer': {'grime': 0.25}, 'gold': {'grime': 0.15}})
    # 'lace_tint' (off by default: the shikoro follows the entry's 'lace' tint): False keeps the
    # shikoro's lacing in the 'lace' colour, off the eye line, while the sode and kusazuri stay civ
    lace_tint = params.pop('lace_tint', None)
    kabuto_mesh(p, lace_tint=('lace' in entry.get('tint', [])) if lace_tint is None else lace_tint, **params)
    return on_head(p.finish(), mesh, arm, brow_drop=entry.get('brow_drop', 0.075))


def menpo(entry, mesh, arm, throat_rows=2, top_d=0.15, moustache=True, glow=0.0):
    """The half mask over the cheeks and chin (below the nose), in red-brown
    lacquer, and its throat guard (yodare-kake) of laced lames under the chin."""
    c = _c(entry)
    mm = lacquer('menpo', B.hex_color(c.get('menpo', '#5e1c16')))
    if glow:
        # lit from within a little: the mask sits in the brim's shadow and must still read as a red face
        bs = next(n for n in mm.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
        bs.inputs['Emission Color'].default_value = bs.inputs['Base Color'].default_value
        bs.inputs['Emission Strength'].default_value = glow
    top, yc, _ = G._head(mesh)
    s = G._scale

    def face(co, top=top / s, yc=yc / s):
        d = top - co.z
        return top_d < d < 0.27 and co.y - yc < 0.045
    o = G.shell(mesh, arm, 'Menpo', ['head', 'neck_01'], mm, offset=0.012, thickness=0.006, min_weight=0.3,
                keep=face)
    # leave the mouth open: a slit under the nose
    cut(o, lambda co, nrm, top=top / s, yc=yc / s: not (0.17 < top - co.z < 0.19 and abs(co.x) < 0.022
                                                          and co.y - yc < -0.05), levels=1)
    _smooth(o, iterations=3)
    made = [o]
    if moustache:
        # a pale horsehair moustache on the mask, under the nose (where the mask stands furthest forward)
        zs = top - 0.165 * s
        band = [v.co for v in o.data.vertices if abs(v.co.z - zs) < 0.012 * s and abs(v.co.x) < 0.05 * s]
        if band:
            fy = min(c.y for c in band) - 0.008 * s
            pts = [(-0.045, 0.022, -0.02), (-0.025, 0.006, 0.0), (0.0, 0.0, 0.004), (0.025, 0.006, 0.0),
                   (0.045, 0.022, -0.02)]
            pts = [(x * s, fy + y * s, zs + z * s) for x, y, z in pts]
            bm = P.tube(pts, [0.004 * s, 0.009 * s, 0.008 * s, 0.009 * s, 0.004 * s], seg=8)
            me = bpy.data.meshes.new('Moustache')
            bm.to_mesh(me)
            bm.free()
            mo = bpy.data.objects.new('Moustache', me)
            bpy.context.scene.collection.objects.link(mo)
            me.materials.append(G.hide('moustache', B.hex_color(c.get('moustache', '#e6e0d2')),
                                       tuple(0.75 * v for v in B.hex_color(c.get('moustache', '#e6e0d2')))))
            for poly in me.polygons:
                poly.use_smooth = True
            made.append(_rigid(mo, arm, 'head'))
    if throat_rows:
        lm, pm = lace_mat(entry), plate_mat(entry)
        th = G.shell(mesh, arm, 'Yodarekake', ['neck_01', 'head', 'spine_03', 'clavicle_l', 'clavicle_r'], lm,
                     offset=0.03, thickness=0.006, min_weight=0.2,
                     keep=lambda co, top=top / s, yc=yc / s: 0.25 < top - co.z < 0.36 and co.y - yc < 0.0)
        zs = [v.co.z for v in th.data.vertices] or [0.0]
        laced_rows(th, min(zs) / s, max(zs) / s, throat_rows, pm, lm, step=0.008, edge=0.3)
        made.append(th)
    return made


# ------------------------------------------------------------ sashimono, wakizashi

def sashimono_mesh(p, height=0.78, width=0.3, pole=1.15, tint=True, mon=True, side=1.0, pole_r=0.011):
    """The back banner as a prop: origin at the holder's foot (the small of the
    back), +Z up, the banner facing +-Y. A lacquered holder tube, the bamboo
    pole, a top bar, and the banner along the pole and under the bar,
    stretched (sashimono are stiffened, they don't flap), with a pale mon."""
    p.add(P.tube([(0, 0, 0.0), (0, 0, 0.3)], 0.016, seg=10), 'lacquer', smooth=True)
    p.add(P.tube([(0, 0, 0.02), (0, 0, pole)], pole_r, seg=8), 'bamboo', smooth=True)
    p.add(P.tube([(0, 0, pole - 0.01), (side * width, 0, pole - 0.01)], 0.008, seg=8), 'bamboo', smooth=True)
    z1, z0 = pole - 0.02, pole - 0.02 - height
    bm = bmesh.new()
    bm.faces.new([bm.verts.new(v) for v in ((side * 0.012, 0, z1), (side * width, 0, z1), (side * width, 0, z0),
                                            (side * 0.012, 0, z0))])
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=6, use_grid_fill=True)
    for v in bm.verts:   # a slight belly in the cloth
        u = (v.co.x * side - 0.012) / (width - 0.012)
        w = (z1 - v.co.z) / height
        v.co.y += 0.018 * math.sin(math.pi * u) * math.sin(math.pi * w)
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.004)
    p.add(bm, 'tint' if tint else 'banner', smooth=True, tint=tint)
    if mon:
        # a mon on both faces: a pale ring with a disc
        cx, cz = side * (0.012 + width) / 2, z1 - height * 0.3
        r = width * 0.3
        for y in (-0.006, 0.006):
            ring = [(cx + r * math.cos(2 * math.pi * k / 24), y, cz + r * math.sin(2 * math.pi * k / 24)) for k in range(25)]
            p.add(P.tube(ring, 0.011, seg=6), 'mon', smooth=True)
            p.add(P.lathe([(0.0, r * 0.45), (0.002, r * 0.45)], seg=20), 'mon',
                  T(cx, y, cz) @ R('X', 90), smooth=True)


def sashimono(entry, mesh, arm, height=0.78, width=0.3, pole=1.15, side=1.0, lift=0.0, yaw=0.0, pole_r=0.011):
    c = dict(_c(entry))
    _register_keys(c)
    p = P.Prop('Sashimono', c)
    sashimono_mesh(p, height=height, width=width, pole=pole, tint='banner' in entry.get('tint', []), side=side,
                   pole_r=pole_r)
    obj = p.finish()
    s = G._scale
    inv = _mesh_space(mesh, arm)
    sp = inv @ arm.data.bones['spine_01'].head_local
    # behind the do: the holder's foot at the small of the back, standing up (+y is behind the unposed body)
    back = max(v.co.y for v in mesh.data.vertices if abs(v.co.x) < 0.05 * s and abs(v.co.z - sp.z) < 0.08 * s)
    obj.matrix_world = Matrix.Translation((0, back + 0.075 * s, sp.z + lift * s)) @ \
        Matrix.Rotation(math.pi + math.radians(yaw), 4, 'Z') @ Matrix.Diagonal((s, s, s, 1.0))
    obj['fall_hide'] = True     # anim.apply hides it once he lies on the ground (the pole would go into it)
    return _rigid(obj, arm, 'spine_03')


def saya(p, length=0.72, scale=1.0, thick=1.0):
    """An empty katana scabbard (its sword drawn), in props.katana_sheathed's
    space: the mouth at +0.07 Z, the scabbard along +Z, its koiguchi band and
    kojiri cap in iron."""
    L = length * scale
    k = thick
    sc = [(0.0, -0.004 * (i / 8) ** 2 * 10, 0.07 + L * i / 8) for i in range(9)]
    p.add(P.tube(sc, [(0.02 - 0.004 * i / 8) * k for i in range(9)], seg=10, scale_xy=(0.6, 1.0)), 'lacquer', smooth=True)
    tip = sc[-1]
    p.add(P.lathe([(0.0, 0.017 * k), (0.035, 0.017 * k), (0.045, 0.004)], seg=10, scale_xy=(0.6, 1.0)),
          'iron', T(tip[0], tip[1], tip[2] - 0.03), smooth=True)
    p.add(P.tube([(0, 0, 0.07), (0, 0, 0.1)], 0.021 * k, seg=10, scale_xy=(0.6, 1.0)), 'iron', smooth=True)


P.BUILDERS.setdefault('katana_saya', saya)


def katana_drawn(p, length=0.72, width=0.04, curve=0.03, scale=1.0, grip=0.27):
    """A drawn katana in the props convention (the grip at the origin, the blade up
    +Z, the edge toward +Y): a bright, gently curved blade (wider than life, so it reads
    at 1x like the stock swords) with its ridge line, a gilt habaki, a round iron
    tsuba, and a long two-hand grip in a pale wrap crossed by dark binding."""
    L = length * scale
    n = 16
    rings = []
    for i in range(n + 1):
        t = i / n
        z = 0.075 + L * t
        bow = -curve * scale * t ** 1.6
        hw = width / 2 * scale * (1.0 - 0.2 * t if t < 0.86 else (1 - t) / 0.14 * 0.83)
        back = 0.0045 * scale * (1 - 0.5 * t)
        rings.append([(0.0, bow - hw, z), (back, bow + hw * 0.5, z), (0.0, bow + hw, z), (-back, bow + hw * 0.5, z)])
    p.add(P.loft(rings), 'steel', bevel=0.0006, angle=50.0)
    p.add(P.box(0.014, width * 1.05, 0.03), 'gold', T(0, 0, 0.075), bevel=0.002)                      # habaki
    p.add(P.lathe([(0.0, 0.042), (0.008, 0.042)], seg=20), 'iron', T(0, 0, 0.056), bevel=0.002)     # tsuba
    p.add(P.tube([(0, 0, -grip), (0, 0, 0.056)], [0.016, 0.017], seg=10, scale_xy=(0.8, 1.0)), 'cloth_wrap',
          smooth=True)
    P._wrap(p, -grip + 0.01, 0.05, 0.0172, 'leather_dark', turns=11)
    p.add(P.ellipsoid(0.017, 0.015, 0.013, seg=12), 'iron', T(0, 0, -grip - 0.006), smooth=True)   # kashira


P.BUILDERS.setdefault('katana_drawn', katana_drawn)


def wakizashi(entry, mesh, arm, length=0.5, thick=1.7, at=(0.1, -0.13, 0.02), direction=(0.35, 0.85, -0.4)):
    """The short sword through the obi at the left front of the belly, the hilt
    forward, the scabbard sloping back (katana_sheathed's prop space)."""
    c = dict(_c(entry))
    obj = P.make('katana_sheathed', c, length=length, thick=thick)
    obj.name = 'Wakizashi'
    s = G._scale
    inv = _mesh_space(mesh, arm)
    pv = inv @ arm.data.bones['pelvis'].head_local
    pos = pv + Vector(at) * s
    H.hold(obj, arm, 'pelvis', pos, up=tuple(Vector(direction).normalized()), forward=(1, 0, 0))
    return obj


# ------------------------------------------------------------ the whole

PIECES = ('hakama', 'suneate', 'haidate', 'kote', 'do', 'kusazuri', 'sode', 'menpo', 'kabuto', 'sashimono',
          'wakizashi')


def dress(entry, mesh, arm):
    """Every piece the entry's 'yoroi' names, in the order they are worn."""
    spec = entry.get('yoroi') or {}
    made = {}
    for name in PIECES:
        if name in spec and spec[name] is not False:
            params = spec[name] if isinstance(spec[name], dict) else {}
            made[name] = globals()[name](entry, mesh, arm, **params)
    return made
