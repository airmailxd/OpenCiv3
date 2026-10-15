"""Hand-held props and worn gear for the Blender unit remaster.

    obj = props.make('spear', colors, length=2.3, grip=0.95)

make(name, colors, **params) returns ONE joined mesh object, linked to the
current collection, in prop space (meters, for a 1.8 m human):

- held props (weapons, tools): the grip at the origin, the long axis along +Z
  (the business end up); a blade's or axe head's edge faces +Y;
- shields: the arm-strap center at the origin, the face toward -Y, +Z up;
- worn gear (quiver, backpack): the strap attach point at the origin; a quiver
  hangs along -Z with its arrows up, a backpack sits toward -Y (the back) with
  its shoulder straps running to +Y.

`colors` is the catalog entry's colors dict (hex strings); each part looks up
its key ('wood', 'stone', 'steel', 'bronze', 'leather', 'rope', 'string',
'fletch', 'shield_face', 'shield_rim', 'shield_emblem', 'pack', 'bedroll',
'pot') and falls back to a default. Only what the original tints is made with
tint=True (the shield emblem, the small round shield's rim and boss), since the
tint AOV is how compose.py finds the civ-color layer.

The look is remastered hard surface: real proportions, beveled edges that
catch the light, and calm procedural detail (wood grain, hammered metal,
leather and rope texture) that only varies brightness, never saturation, so
the props read at ~50 px. Pure bpy + bmesh, no add-ons, everything procedural.
"""
import math
import random

import bmesh
import bpy
from mathutils import Matrix, Vector

# ------------------------------------------------------------ materials

DEFAULTS = {
    # key: (color, roughness, metallic, detail kind)
    'wood': ('#7a5434', 0.62, 0.0, 'wood'),
    'stone': ('#8e8a86', 0.78, 0.0, 'stone'),
    'steel': ('#aeb2b8', 0.36, 1.0, 'metal'),
    'bronze': ('#b08a4a', 0.38, 1.0, 'metal'),
    'leather': ('#5c3a24', 0.58, 0.0, 'leather'),
    'leather_dark': ('#3e2616', 0.6, 0.0, 'leather'),
    'rope': ('#b89a68', 0.85, 0.0, 'rope'),
    'string': ('#ddd4bc', 0.7, 0.0, None),
    'fletch': ('#e6e0ce', 0.8, 0.0, None),
    'shield_face': ('#c8b894', 0.6, 0.0, 'wood'),
    'shield_rim': ('#5a4028', 0.58, 0.0, 'leather'),
    'shield_emblem': ('#ffffff', 0.6, 0.0, None),
    'pack': ('#76683e', 0.9, 0.0, 'cloth'),
    'bedroll': ('#a89060', 0.9, 0.0, 'cloth'),
    'pot': ('#8a5a3a', 0.42, 1.0, 'metal'),
}


def _base_material(name, color, roughness, metallic, tint):
    """bscene.material (sRGB hex color; a tinted material is rendered light
    gray and marked in the `tint` AOV)."""
    import bscene
    return bscene.material(name, color, roughness, metallic, tint=tint)


def material(key, colors, tint=False):
    """The material of a color key, created once per (key, color, tint)."""
    color, rough, metal, kind = DEFAULTS[key]
    color = colors.get(key, color)
    name = f'prop_{key}_{color.lstrip("#")}' + ('_tint' if tint else '')
    if name in bpy.data.materials:
        return bpy.data.materials[name]
    m = _base_material(name, color, rough, metal, tint)
    _detail(m, kind, rough, tint)
    return m


def _principled(m):
    for n in m.node_tree.nodes:
        if n.type == 'BSDF_PRINCIPLED':
            return n
    return None


def _detail(m, kind, rough, tint):
    """Calm procedural surface detail: brightness variation (value only, so
    never more saturated than the catalog color), roughness variation and a
    small bump. Tinted parts only get the bump."""
    if kind is None or not m.use_nodes:
        return
    p = _principled(m)
    if p is None:
        return
    nt = m.node_tree
    N, L = nt.nodes, nt.links
    tc = N.new('ShaderNodeTexCoord')
    mp = N.new('ShaderNodeMapping')
    L.new(tc.outputs['Object'], mp.inputs['Vector'])

    def noise(scale, detail=4.0, rough_=0.55):
        t = N.new('ShaderNodeTexNoise')
        t.inputs['Scale'].default_value = scale
        t.inputs['Detail'].default_value = detail
        t.inputs['Roughness'].default_value = rough_
        L.new(mp.outputs['Vector'], t.inputs['Vector'])
        return t.outputs['Fac']

    def wave(scale, direction='X', distortion=4.0, profile='SIN'):
        t = N.new('ShaderNodeTexWave')
        t.wave_type = 'BANDS'
        t.bands_direction = direction
        t.wave_profile = profile
        t.inputs['Scale'].default_value = scale
        t.inputs['Distortion'].default_value = distortion
        t.inputs['Detail'].default_value = 3.0
        L.new(mp.outputs['Vector'], t.inputs['Vector'])
        return t.outputs['Fac']

    def voronoi(scale):
        t = N.new('ShaderNodeTexVoronoi')
        t.inputs['Scale'].default_value = scale
        L.new(mp.outputs['Vector'], t.inputs['Vector'])
        return t.outputs['Distance']

    def map_range(src, lo, hi):
        r = N.new('ShaderNodeMapRange')
        r.inputs['To Min'].default_value = lo
        r.inputs['To Max'].default_value = hi
        L.new(src, r.inputs['Value'])
        return r.outputs['Result']

    def value_mod(src, lo, hi):
        """Multiply the base color's value (HSV V) by a factor in [lo, hi]."""
        bc = p.inputs['Base Color']
        if bc.is_linked:
            return
        hsv = N.new('ShaderNodeHueSaturation')
        hsv.inputs['Hue'].default_value = 0.5
        hsv.inputs['Saturation'].default_value = 1.0
        hsv.inputs['Color'].default_value = bc.default_value
        L.new(map_range(src, lo, hi), hsv.inputs['Value'])
        L.new(hsv.outputs['Color'], bc)

    def rough_mod(src, spread):
        ri = p.inputs['Roughness']
        if not ri.is_linked:
            L.new(map_range(src, max(0.0, rough - spread), min(1.0, rough + spread)), ri)

    def bump(src, strength, distance):
        b = N.new('ShaderNodeBump')
        b.inputs['Strength'].default_value = strength
        b.inputs['Distance'].default_value = distance
        L.new(src, b.inputs['Height'])
        if not p.inputs['Normal'].is_linked:
            L.new(b.outputs['Normal'], p.inputs['Normal'])

    if kind == 'wood':
        # grain runs along the prop's long axis (+Z): stretch the coordinates in Z
        mp.inputs['Scale'].default_value = (1.0, 1.0, 0.12)
        g = wave(6.0, 'X', distortion=7.0)
        if not tint:
            value_mod(g, 0.9, 1.04)
            rough_mod(noise(30.0), 0.06)
        bump(g, 0.18, 0.002)
    elif kind == 'stone':
        n = noise(22.0, 6.0, 0.6)
        if not tint:
            value_mod(n, 0.82, 1.08)
        bump(voronoi(16.0), 0.35, 0.004)
    elif kind == 'metal':
        n = noise(36.0, 5.0, 0.5)
        if not tint:
            value_mod(n, 0.9, 1.04)
            rough_mod(noise(70.0), 0.1)
        bump(voronoi(80.0), 0.07, 0.001)
    elif kind == 'leather':
        n = noise(70.0, 6.0, 0.6)
        if not tint:
            value_mod(noise(14.0), 0.86, 1.04)
        bump(n, 0.12, 0.001)
    elif kind == 'rope':
        g = wave(140.0, 'DIAGONAL', distortion=1.0)
        if not tint:
            value_mod(noise(40.0), 0.86, 1.04)
        bump(g, 0.3, 0.0015)
    elif kind == 'cloth':
        if not tint:
            value_mod(noise(16.0, 4.0, 0.5), 0.86, 1.05)
        bump(wave(260.0, 'X', distortion=0.5), 0.06, 0.001)


# ------------------------------------------------------------ geometry

# Minimum radius of long shafts (spear, staff, shovel): about 1 px wide at 1x.
SHAFT_R = 0.022

def _frames(path):
    """Parallel-transport frames (tangent, normal, binormal) along a polyline."""
    pts = [Vector(p) for p in path]
    tans = []
    for i in range(len(pts)):
        a = pts[max(i - 1, 0)]
        b = pts[min(i + 1, len(pts) - 1)]
        tans.append((b - a).normalized())
    t0 = tans[0]
    ref = Vector((1, 0, 0)) if abs(t0.x) < 0.9 else Vector((0, 1, 0))
    n = t0.cross(ref).normalized()
    out = []
    for i, t in enumerate(tans):
        if i:
            n = (n - t * n.dot(t)).normalized()
        out.append((t, n, t.cross(n).normalized()))
    return pts, out


def loft(rings, cap_start=True, cap_end=True):
    """A bmesh skinned through rings of equal size (lists of points). A ring
    whose points all coincide closes to a point."""
    bm = bmesh.new()
    vrings = []
    for ring in rings:
        if max((Vector(p) - Vector(ring[0])).length for p in ring) < 1e-7:
            vrings.append([bm.verts.new(ring[0])])
        else:
            vrings.append([bm.verts.new(p) for p in ring])
    for a, b in zip(vrings[:-1], vrings[1:]):
        if len(a) == 1 and len(b) == 1:
            continue
        k = max(len(a), len(b))
        for i in range(k):
            j = (i + 1) % k
            quad = [a[i % len(a)], a[j % len(a)], b[j % len(b)], b[i % len(b)]]
            uniq = []
            for v in quad:
                if v not in uniq:
                    uniq.append(v)
            if len(uniq) >= 3:
                bm.faces.new(uniq)
    if cap_start and len(vrings[0]) > 2:
        bm.faces.new(list(reversed(vrings[0])))
    if cap_end and len(vrings[-1]) > 2:
        bm.faces.new(vrings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def tube(path, radii, seg=10, scale_xy=(1.0, 1.0), caps=True):
    """A tube along a polyline; radii is one radius or one per point.
    scale_xy flattens the cross-section (normal, binormal)."""
    pts, fr = _frames(path)
    if not isinstance(radii, (list, tuple)):
        radii = [radii] * len(pts)
    rings = []
    for p, (t, n, b), r in zip(pts, fr, radii):
        ring = []
        for k in range(seg):
            a = 2 * math.pi * k / seg
            ring.append(p + n * (r * scale_xy[0] * math.cos(a)) + b * (r * scale_xy[1] * math.sin(a)))
        rings.append(ring)
    return loft(rings, caps, caps)


def lathe(profile, seg=12, scale_xy=(1.0, 1.0)):
    """Revolve (z, r) pairs around Z."""
    return tube([(0, 0, z) for z, _ in profile], [r for _, r in profile], seg, scale_xy)


def box(sx, sy, sz):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=(sx, sy, sz), verts=bm.verts)
    return bm


def ellipsoid(rx, ry, rz, seg=16):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=max(6, seg // 2), radius=1.0)
    bmesh.ops.scale(bm, vec=(rx, ry, rz), verts=bm.verts)
    return bm


def slab(outline, thickness, axis='X'):
    """Extrude a closed 2D outline. axis='X': outline in (y, z), thickness
    along X centered on 0; axis='Y': outline in (x, z), thickness along Y."""
    if axis == 'X':
        front = [(-thickness / 2, u, v) for u, v in outline]
        back = [(thickness / 2, u, v) for u, v in outline]
    else:
        front = [(u, -thickness / 2, v) for u, v in outline]
        back = [(u, thickness / 2, v) for u, v in outline]
    return loft([front, back])


def domed_disc(rx, rz, thick, dome, rings=7, seg=40):
    """A shield board: the back (+Y side) flat at y = 0, the front bulging to
    -Y by `thick` at the edge plus `dome` at the center. Elliptical, rx by rz."""
    bm = bmesh.new()

    def front_y(s):
        return -thick - dome * (1 - s * s)

    cf = bm.verts.new((0, front_y(0), 0))
    cb = bm.verts.new((0, 0, 0))
    fr, br = [], []
    for i in range(1, rings + 1):
        s = i / rings
        fr.append([bm.verts.new((rx * s * math.cos(2 * math.pi * k / seg), front_y(s),
                                 rz * s * math.sin(2 * math.pi * k / seg))) for k in range(seg)])
        br.append([bm.verts.new((rx * s * math.cos(2 * math.pi * k / seg), 0.0,
                                 rz * s * math.sin(2 * math.pi * k / seg))) for k in range(seg)])
    for c, rs in ((cf, fr), (cb, br)):
        for k in range(seg):
            bm.faces.new((c, rs[0][k], rs[0][(k + 1) % seg]))
        for a, b in zip(rs[:-1], rs[1:]):
            for k in range(seg):
                j = (k + 1) % seg
                bm.faces.new((a[k], a[j], b[j], b[k]))
    for k in range(seg):
        j = (k + 1) % seg
        bm.faces.new((fr[-1][k], fr[-1][j], br[-1][j], br[-1][k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def jitter(bm, amount, seed, cuts=0):
    """Knapped / hand-made irregularity: optional subdivision, then a
    deterministic random offset of every vertex."""
    if cuts:
        bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=cuts, use_grid_fill=True)
    rnd = random.Random(seed)
    for v in bm.verts:
        v.co += Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1))) * amount
    return bm


class Prop:
    """Collects parts (each its own bmesh, material, bevel and shading) into
    one mesh object."""

    def __init__(self, name, colors):
        self.name = name
        self.colors = colors
        self.bm = bmesh.new()
        self.mats = []

    def mat(self, key, tint=False):
        return material(key, self.colors, tint)

    def add(self, part, key, matrix=None, bevel=0.0, smooth=False, tint=False, angle=35.0):
        m = self.mat(key, tint)
        if m not in self.mats:
            self.mats.append(m)
        idx = self.mats.index(m)
        if bevel > 0:
            edges = [e for e in part.edges
                     if len(e.link_faces) == 2 and math.degrees(e.calc_face_angle(0.0)) > angle]
            if edges:
                bmesh.ops.bevel(part, geom=edges, offset=bevel, offset_type='OFFSET', segments=2,
                                profile=0.5, affect='EDGES', clamp_overlap=True)
        if matrix is not None:
            bmesh.ops.transform(part, matrix=matrix, verts=part.verts)
        for f in part.faces:
            f.material_index = idx
            f.smooth = smooth
        tmp = bpy.data.meshes.new('_prop_part')
        part.to_mesh(tmp)
        part.free()
        self.bm.from_mesh(tmp)
        bpy.data.meshes.remove(tmp)

    def finish(self):
        me = bpy.data.meshes.new(self.name)
        self.bm.normal_update()
        self.bm.to_mesh(me)
        self.bm.free()
        for m in self.mats:
            me.materials.append(m)
        obj = bpy.data.objects.new(self.name, me)
        bpy.context.collection.objects.link(obj)
        return obj


def T(x=0.0, y=0.0, z=0.0):
    return Matrix.Translation((x, y, z))


def R(axis, deg):
    return Matrix.Rotation(math.radians(deg), 4, axis)


def S(x, y=None, z=None):
    y = x if y is None else y
    z = x if z is None else z
    return Matrix.Diagonal((x, y, z, 1.0))


def _wrap(p, z0, z1, r, key, turns=None, seg=8):
    """A rawhide / leather binding: a tight helix around the Z axis."""
    turns = turns or max(2, int((z1 - z0) / 0.012))
    n = turns * 10
    path = [(r * math.cos(2 * math.pi * i / 10), r * math.sin(2 * math.pi * i / 10), z0 + (z1 - z0) * i / n)
            for i in range(n + 1)]
    p.add(tube(path, 0.0045, seg=5), key, smooth=True)


# ------------------------------------------------------------ weapons

def stone_axe(p, length=0.52, scale=1.0):
    """A short club-axe: a slightly bent wooden haft with a knapped stone head
    lashed with rawhide near the top, the edge facing +Y."""
    L = length * scale
    path = [(0, 0.004 * math.sin(math.pi * t), -0.07 + (L + 0.09) * t) for t in (0, 0.25, 0.5, 0.75, 1.0)]
    p.add(tube(path, [0.019, 0.019, 0.02, 0.022, 0.021], seg=10), 'wood', bevel=0.003, smooth=True)
    # the head: a knapped wedge of stone, thick at the butt, tapering to a
    # slightly flared cutting edge
    outline = [(-0.04, -0.028), (0.02, -0.036), (0.1, -0.042), (0.15, -0.05), (0.158, -0.01),
               (0.158, 0.02), (0.15, 0.052), (0.1, 0.045), (0.02, 0.038), (-0.04, 0.03)]
    head = slab(outline, 0.055, 'X')
    for v in head.verts:  # taper the thickness toward the edge (+Y)
        v.co.x *= max(0.12, 1.0 - 0.85 * max(0.0, v.co.y + 0.04) / 0.198)
    jitter(head, 0.0035, seed=7, cuts=1)
    p.add(head, 'stone', T(0, 0, L - 0.07) @ S(1.25 * scale), bevel=0.004)
    # lashing: crossed rawhide straps over the head and wraps on the haft
    _wrap(p, L - 0.14, L - 0.1, 0.024, 'rope')
    _wrap(p, L - 0.03, L - 0.005, 0.024, 'rope')
    for sgn in (-1, 1):
        strap = [(sgn * 0.03, -0.03, L - 0.12), (sgn * 0.032, 0.0, L - 0.07), (sgn * 0.03, 0.03, L - 0.02)]
        p.add(tube(strap, 0.006, seg=6), 'rope', smooth=True)


def spear(p, length=2.3, grip=0.95, butt=True, scale=1.0):
    """A long ash spear held `grip` m from its butt: a leaf-shaped iron head on
    a socket, a leather grip binding and an optional butt spike."""
    z0, z1 = -grip, length - grip
    # the shaft is kept at least SHAFT_R thick so it stays ~1 px wide at 1x
    p.add(tube([(0, 0, z0 + 0.06), (0, 0, z1 - 0.2)], [SHAFT_R + 0.002, SHAFT_R], seg=10), 'wood', bevel=0.002,
          smooth=True)
    # leaf head: a lathe flattened across Y with a raised midrib (diamond section)
    prof = [(0.0, 0.022), (0.06, 0.026), (0.12, 0.048), (0.2, 0.043), (0.27, 0.02), (0.33, 0.0)]
    head = lathe(prof, seg=4, scale_xy=(1.0, 0.3))
    p.add(head, 'steel', T(0, 0, z1 - 0.33) @ R('Z', 90), bevel=0.0015)
    # socket with a collar ring
    p.add(lathe([(0.0, 0.025), (0.1, 0.023), (0.11, 0.022)], seg=12), 'steel', T(0, 0, z1 - 0.33), smooth=True)
    p.add(lathe([(0.0, 0.027), (0.012, 0.027)], seg=12), 'steel', T(0, 0, z1 - 0.34), bevel=0.002, smooth=True)
    # leather binding where the hand holds it
    p.add(tube([(0, 0, -0.1), (0, 0, 0.12)], SHAFT_R + 0.0025, seg=12), 'leather', smooth=True)
    _wrap(p, -0.1, 0.12, SHAFT_R + 0.0028, 'leather_dark', turns=14)
    if butt:
        p.add(lathe([(0.0, 0.0005), (0.07, 0.022), (0.1, 0.024)], seg=10), 'steel', T(0, 0, z0 - 0.04),
              smooth=True)


def sword(p, length=0.82, width=0.085, guard=0.2, blade='steel', scale=1.0):
    """A straight short sword: a waisted, long-pointed blade along +Z (flat to
    X, edges toward +-Y) with a central ridge, a bar guard, a wrapped leather
    grip and a round pommel."""
    L, w = length * scale, width * scale
    rings = []
    n = 14
    for i in range(n + 1):
        t = i / n
        z = 0.075 + L * t
        if t < 0.72:  # gently waisted, then the long point
            hw = w / 2 * (1.0 - 0.12 * math.sin(math.pi * t / 0.72))
        else:
            hw = w / 2 * (1.0 - (t - 0.72) / 0.28) ** 0.85
        ridge = 0.0065 * scale * (1.0 - 0.6 * t)
        rings.append([(0.0, -hw, z), (ridge, 0.0, z), (0.0, hw, z), (-ridge, 0.0, z)])
    p.add(loft(rings), blade, bevel=0.0008, angle=50.0)
    # guard, grip, pommel
    p.add(box(0.04, guard * scale, 0.028), 'bronze', T(0, 0, 0.06), bevel=0.006)
    p.add(tube([(0, 0, -0.075), (0, 0, 0.045)], [0.017, 0.019], seg=12), 'leather', smooth=True)
    _wrap(p, -0.07, 0.04, 0.0192, 'leather_dark', turns=9)
    p.add(ellipsoid(0.03, 0.022, 0.026), 'bronze', T(0, 0, -0.1), bevel=0.0, smooth=True)


def _bow_points(height, draw, bend, recurve, n=17):
    pts = []
    for i in range(n):
        u = -1 + 2 * i / (n - 1)
        z = u * height / 2 * (1 - 0.05 * draw)
        y = -(bend + 0.08 * draw) * u * u
        if recurve and abs(u) > 0.8:  # tips curl forward again
            y += recurve * ((abs(u) - 0.8) / 0.2) ** 2
        pts.append((0.0, y, z))
    return pts


def bow(p, height=1.45, draw=0.0, bend=0.12, recurve=0.07, string=True, scale=1.0):
    """A recurve bow held at its grip: limbs up and down along Z bending back
    toward the archer (-Y), the tips curling forward; leather grip; string."""
    h = height * scale
    pts = _bow_points(h, draw, bend, recurve)
    radii = [0.006 + 0.012 * (1 - abs(-1 + 2 * i / (len(pts) - 1))) ** 0.7 for i in range(len(pts))]
    p.add(tube(pts, radii, seg=8, scale_xy=(1.0, 0.75)), 'wood', smooth=True)
    p.add(tube([(0, -0.002, -0.07), (0, -0.002, 0.07)], 0.02, seg=12), 'leather', smooth=True)
    if string:
        top, bot = pts[-1], pts[0]
        mid = (0.0, top[1] - 0.04 - 0.55 * draw, 0.0)
        p.add(tube([bot, mid, top], 0.0028, seg=5), 'string', smooth=True)


def arrow(p, length=0.75, scale=1.0):
    """A loose arrow along +Z, the nock at the origin: shaft, iron bodkin,
    three fletchings."""
    L = length * scale
    p.add(tube([(0, 0, 0.0), (0, 0, L - 0.05)], 0.0055, seg=6), 'wood', smooth=True)
    p.add(lathe([(0.0, 0.007), (0.025, 0.012), (0.06, 0.0)], seg=4), 'steel', T(0, 0, L - 0.06), bevel=0.0005)
    for a in (0, 120, 240):
        fl = slab([(0.0, 0.02), (0.022, 0.035), (0.024, 0.13), (0.0, 0.12)], 0.002, 'X')
        p.add(fl, 'fletch', R('Z', a) @ R('Z', 90))


# ------------------------------------------------------------ shields

def _strap_back(p, half=0.09, y=0.04):
    """The arm strap and hand grip behind a shield (toward +Y), centered at
    the origin."""
    p.add(tube([(-half, y * 0.2, 0.0), (-half * 0.5, y, 0.0), (half * 0.5, y, 0.0), (half, y * 0.2, 0.0)],
               0.009, seg=6, scale_xy=(0.5, 1.6)), 'leather', smooth=True)


def _emblem_band(rx, rz, thick, dome, half_len, half_w, n=16):
    """A strip along X lying just on a domed shield front, slightly raised."""
    def fy(x, z):
        s2 = min(1.0, (x / rx) ** 2 + (z / rz) ** 2)
        return -thick - dome * (1 - s2)

    rings = []
    for i in range(n + 1):
        x = -half_len + 2 * half_len * i / n
        top = [(x, fy(x, half_w) - 0.0025, half_w), (x, fy(x, -half_w) - 0.0025, -half_w)]
        bot = [(x, fy(x, -half_w) + 0.001, -half_w), (x, fy(x, half_w) + 0.001, half_w)]
        rings.append(top + bot)
    return loft(rings)


def shield_oval(p, w=0.66, h=0.66, dome=0.06, emblem_angle=45.0, band=0.1, scale=1.0):
    """The Spearman's big wooden shield (round by default, oval if w != h):
    a tan board face, a leather-bound rim, a quartered cross emblem in the civ
    color (tint) and a small bronze boss; arm strap at the origin."""
    rx, rz = w / 2 * scale, h / 2 * scale
    thick, gap = 0.022, -0.05  # board thickness; the board sits in front of the forearm
    face = domed_disc(rx, rz, thick, dome, rings=8, seg=48)
    p.add(face, 'shield_face', T(0, gap, 0), smooth=True)
    # rim: a rawhide-bound ring around the edge
    k = 64
    ring = [(rx * math.cos(2 * math.pi * i / k), gap - thick * 0.6, rz * math.sin(2 * math.pi * i / k))
            for i in range(k + 1)]
    p.add(tube(ring, 0.014, seg=8, scale_xy=(1.0, 1.3)), 'shield_rim', smooth=True)
    # emblem: two crossing bands following the dome (the original's quartered cross)
    for ang in (emblem_angle, -emblem_angle):
        a = math.radians(ang)
        half_len = 0.93 * rx * rz / math.hypot(rz * math.cos(a), rx * math.sin(a))
        b = _emblem_band(rx, rz, thick, dome, half_len, band / 2 * scale)
        p.add(b, 'shield_emblem', T(0, gap, 0) @ R('Y', ang), smooth=True, tint=True)
    p.add(ellipsoid(0.055, 0.03, 0.055), 'bronze', T(0, gap - thick - dome - 0.008, 0), smooth=True)
    _strap_back(p)


def shield_round(p, r=0.24, dome=0.045, boss=True, scale=1.0):
    """The Swordsman's small round shield: a dark plank board, a rim and boss
    in the civ color (tint, as in the original); arm strap at the origin."""
    rr = r * scale
    thick, gap = 0.02, -0.045
    p.add(domed_disc(rr, rr, thick, dome, rings=6, seg=40), 'shield_face', T(0, gap, 0), smooth=True)
    k = 56
    ring = [(rr * math.cos(2 * math.pi * i / k), gap - thick * 0.6, rr * math.sin(2 * math.pi * i / k))
            for i in range(k + 1)]
    p.add(tube(ring, 0.016, seg=8, scale_xy=(1.0, 1.3)), 'shield_emblem', smooth=True, tint=True)
    if boss:
        prof = [(0.0, 0.075), (0.012, 0.072), (0.02, 0.05), (0.045, 0.035), (0.06, 0.0)]
        b = lathe(prof, seg=24)
        p.add(b, 'shield_emblem', T(0, gap - thick - dome + 0.01, 0) @ R('X', 90), smooth=True, tint=True)
    _strap_back(p, half=0.08)


# ------------------------------------------------------------ tools

def staff(p, length=1.95, grip=1.05, scale=1.0):
    """A tall walking staff, slightly crooked, with a couple of knots and a
    worn top, taller than its bearer; held `grip` m from the foot."""
    L = length * scale
    rnd = random.Random(11)
    n = 9
    path, radii = [], []
    for i in range(n + 1):
        t = i / n
        path.append((0.012 * math.sin(2.2 * t + 0.4) + rnd.uniform(-0.004, 0.004),
                     0.01 * math.sin(3.1 * t) + rnd.uniform(-0.004, 0.004), -grip + L * t))
        radii.append(SHAFT_R + 0.003 * (1 - t) + (0.004 if i in (3, 6) else 0.0))
    p.add(tube(path, radii, seg=10), 'wood', bevel=0.002, smooth=True)
    p.add(ellipsoid(0.027, 0.027, 0.033), 'wood', T(*path[-1]), smooth=True)
    p.add(tube([(0, 0, -0.08), (0, 0, 0.1)], SHAFT_R + 0.0035, seg=12), 'leather', smooth=True)


def shovel(p, length=1.15, grip=0.0, d_grip=False, scale=1.0):
    """A shovel: a wooden haft along +Z from the grip, an iron spade at the far
    end (+Z), its face toward +Y, with a socket and rivets; optional D-grip."""
    L = length * scale
    p.add(tube([(0, 0, -0.18), (0, 0, L - 0.28)], [SHAFT_R + 0.001, SHAFT_R], seg=10), 'wood', bevel=0.002,
          smooth=True)
    outline = [(-0.11, 0.0), (0.11, 0.0), (0.12, 0.17), (0.07, 0.26), (0.0, 0.29), (-0.07, 0.26), (-0.12, 0.17)]
    blade = slab(outline, 0.006, 'Y')
    for v in blade.verts:  # a slight dish across the blade
        v.co.y += 0.025 * (v.co.x / 0.12) ** 2
    p.add(blade, 'steel', T(0, 0, L - 0.28), bevel=0.0015)
    p.add(lathe([(0.0, SHAFT_R + 0.006), (0.1, SHAFT_R + 0.002)], seg=12), 'steel', T(0, 0, L - 0.36), smooth=True)
    for z in (L - 0.33, L - 0.3):
        p.add(ellipsoid(0.005, 0.006, 0.005, seg=8), 'steel', T(0, SHAFT_R + 0.004, z), smooth=True)
    if d_grip:  # a D-shaped loop below the haft end
        dg = [(0.065 * math.sin(math.pi * t), 0.0, -0.18 - 0.12 * math.sin(math.pi * t / 2))
              for t in [i / 12 for i in range(13)]]
        p.add(tube(dg, 0.014, seg=8), 'wood', smooth=True)


# ------------------------------------------------------------ worn gear

def quiver(p, arrows=7, scale=1.0):
    """A leather quiver hanging along -Z from its strap ring at the origin,
    with arrows sticking out of the top."""
    s = scale
    q = lathe([(z * s - 0.47 * s, r * s) for z, r in [(0.0, 0.0), (0.02, 0.058), (0.45, 0.066), (0.5, 0.068)]],
              seg=14, scale_xy=(1.0, 0.75))
    p.add(q, 'leather', smooth=True)
    p.add(lathe([(0.0, 0.071 * s), (0.035 * s, 0.071 * s)], seg=14, scale_xy=(1.0, 0.75)), 'leather_dark',
          T(0, 0, 0.0), bevel=0.003, smooth=True)
    p.add(lathe([(0.0, 0.07 * s), (0.03 * s, 0.07 * s)], seg=14, scale_xy=(1.0, 0.75)), 'leather_dark',
          T(0, 0, -0.38 * s), bevel=0.003, smooth=True)
    p.add(tube([(0.0, 0.05, 0.0), (0.0, 0.09, 0.01), (0.0, 0.05, 0.03)], 0.006, seg=6), 'leather_dark',
          smooth=True)
    rnd = random.Random(3)
    for i in range(arrows):
        a = 2 * math.pi * i / arrows
        x, y = 0.035 * s * math.cos(a), 0.024 * s * math.sin(a)
        tilt = R('X', rnd.uniform(-5, 5)) @ R('Y', rnd.uniform(-5, 5))
        p.add(tube([(0, 0, -0.1), (0, 0, 0.1)], 0.0055, seg=5), 'wood', T(x, y, 0.0) @ tilt, smooth=True)
        for f in (0, 120, 240):
            fl = slab([(0.0, 0.0), (0.02, 0.012), (0.022, 0.1), (0.0, 0.09)], 0.002, 'X')
            p.add(fl, 'fletch', T(x, y, 0.0) @ tilt @ R('Z', f + rnd.uniform(-20, 20)))


def backpack(p, size=1.0):
    """The Settler's bundle: a stuffed cloth sack hanging behind (-Y) with a
    bedroll strapped across the top, a copper pot lashed to the side and
    shoulder straps running forward (+Y) from the attach point at the origin."""
    s = size
    sack = ellipsoid(0.17 * s, 0.12 * s, 0.24 * s, seg=20)
    for v in sack.verts:  # flatter against the back, fuller at the bottom
        if v.co.y > 0:
            v.co.y *= 0.55
        if v.co.z < 0:
            v.co.x *= 1.08
            v.co.y *= 1.06
    jitter(sack, 0.006 * s, seed=5)
    p.add(sack, 'pack', T(0, -0.15 * s, -0.12 * s), smooth=True)
    # tied neck of the sack
    p.add(lathe([(0.0, 0.05 * s), (0.04 * s, 0.035 * s), (0.07 * s, 0.05 * s)], seg=12), 'pack',
          T(0, -0.16 * s, 0.1 * s), smooth=True)
    p.add(lathe([(0.0, 0.04 * s), (0.015 * s, 0.04 * s)], seg=12), 'rope', T(0, -0.16 * s, 0.135 * s), smooth=True)
    # bedroll across the top, with two straps
    p.add(tube([(-0.2 * s, 0, 0), (0.2 * s, 0, 0)], 0.065 * s, seg=16), 'bedroll',
          T(0, -0.14 * s, 0.17 * s), bevel=0.01, smooth=True)
    for x in (-0.12 * s, 0.12 * s):
        p.add(lathe([(0.0, 0.069 * s), (0.02 * s, 0.069 * s)], seg=16), 'leather',
              T(x, -0.14 * s, 0.17 * s) @ R('Y', 90), smooth=True)
    # copper pot lashed to the side
    pot = lathe([(0.0, 0.0005), (0.005, 0.05 * s), (0.06 * s, 0.075 * s), (0.11 * s, 0.065 * s),
                 (0.12 * s, 0.07 * s), (0.125 * s, 0.07 * s)], seg=18)
    p.add(pot, 'pot', T(0.17 * s, -0.2 * s, -0.08 * s) @ R('Y', -20), smooth=True)
    p.add(lathe([(0.0, 0.074 * s), (0.015 * s, 0.074 * s)], seg=18), 'rope',
          T(0.17 * s, -0.2 * s, -0.08 * s) @ R('Y', -20) @ T(0, 0, 0.05 * s), smooth=True)
    # shoulder straps: from the top of the pack over the shoulders and down the chest
    for x in (-0.1 * s, 0.1 * s):
        path = [(x, -0.08 * s, 0.08 * s), (x, 0.0, 0.12 * s), (x, 0.1 * s, 0.1 * s), (x * 1.05, 0.13 * s, -0.08 * s),
                (x * 1.1, 0.1 * s, -0.25 * s)]
        p.add(tube(path, 0.016 * s, seg=6, scale_xy=(1.0, 0.35)), 'leather', smooth=True)


# ------------------------------------------------------------ registry

BUILDERS = {
    'stone_axe': stone_axe,
    'spear': spear,
    'sword': sword,
    'bow': bow,
    'bold_bow': bow,
    'arrow': arrow,
    'quiver': quiver,
    'staff': staff,
    'shovel': shovel,
    'shield_round': shield_round,
    'shield_oval': shield_oval,
    'backpack': backpack,
}


def make(name, mats_colors=None, **params):
    """Build prop `name` with the catalog colors and params; returns one
    joined mesh object (see the module docstring for its space)."""
    p = Prop(name, mats_colors or {})
    BUILDERS[name](p, **params)
    return p.finish()
