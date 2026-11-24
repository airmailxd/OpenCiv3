"""The MDJ (Medieval Japan) siege engines, remade in units3d from the NumPy
generator's units (ArtGen/units/models/mdj_engines.py, mdj_siege.py):

  * Medieval Japanese Catapult: a traction trebuchet (the Chinese pao, the
    Japanese hassekisha) on a low four-wheeled cart: a trestle of two raking
    A-frames under a wide lacquered lintel with upswept ends, a long composite
    arm bound with rope, its sling end down at the rear with the stone in the
    pouch, its short end raised at the front with the hauling ropes hanging.
  * Fire Cannon: a breech-loading bronze furanki (the kunikuzushi, the
    "country-breaker"): a long gun with an open breech bay holding a
    removable iron powder chamber, wedged, a tiller behind; on a low box
    carriage of stacked beams on four solid trucks.
  * Siege Crossbow: a double-bow bed crossbow (chuangnu, Japanese oyumi):
    two wide composite bows on a heavy stock, the strings drawn back to the
    trigger nut, a spear-sized bolt in the groove, a windlass at the rear,
    on a low four-wheeled cart.

Same conventions as siege.py: meters, the engine faces +Y, ground z = 0, the
crew stands at the 'crew_<n>' empties (crews.CREW_R dresses them). The
builders register in siege.BUILDERS and their chosen values in siege.REALISM.
"""
import math

from mathutils import Matrix, Vector

import props
import siege
import vkit
from props import R, T

props.DEFAULTS.setdefault('lacquer_red', ('#5a1e14', 0.4, 0.0, 'leather'))   # (unused default; civ parts take 'beam')
props.DEFAULTS.setdefault('bow_core', ('#2a1c14', 0.42, 0.0, 'leather'))     # a black-lacquered composite bow
props.DEFAULTS.setdefault('rattan', ('#b89058', 0.75, 0.0, 'rope'))          # rattan bindings
props.DEFAULTS.setdefault('gun_bronze', ('#6e5434', 0.48, 1.0, 'metal'))    # the furanki's cast bronze
props.DEFAULTS.setdefault('match', ('#e8a040', 0.6, 0.0, 'flame'))           # the slow match's glow

MDJ_COLORS = dict(siege.OAK_LIGHT, rope='#a08a60', iron='#3e3c3a', stone='#a49c90', string='#e6dcc0')
MDJ_WEAR = dict(siege.CATAPULT_WEAR, gun_bronze={'grime': 0.6, 'soot': 0.45, 'edges': 0.4},
                bow_core={'grime': 0.3}, rattan={'grime': 0.4})


def _cart(p, L, W, zr, wheel_r, sill=0.16, planks=True, tint_ends=False, sill_key='side_rail'):
    """A low four-wheeled cart frame: two heavy sills, cross ties, a planked
    bed between them, iron corner plates; the wheels are drawn by _wheels."""
    for sx in (-1, 1):
        x = sx * W / 2
        vkit.beam(p, (x, -L / 2, zr), (x, L / 2, zr), sill * siege.TH, (sill + 0.03) * siege.TH, sill_key, bevel=0.02,
                  tint=sill_key == 'beam')
        for y in (-L / 2 + 0.08, L / 2 - 0.08):
            p.add(props.box(0.25, 0.1, 0.27), 'iron', T(x, y, zr), bevel=0.01)
    for y in (-L / 2 + 0.12, 0.0, L / 2 - 0.12):
        vkit.beam(p, (-W / 2, y, zr), (W / 2, y, zr), 0.15 * siege.TH, 0.15 * siege.TH,
                  'beam' if tint_ends and abs(y) > 0.1 else 'wood', tint=tint_ends and abs(y) > 0.1)
    if planks:
        n = int(L / 0.24)
        for i in range(n):
            y = -L / 2 + 0.2 + (L - 0.4) * i / max(1, n - 1)
            p.add(props.box(W - 0.12, 0.2, 0.04), 'wood', T(0, y, zr + 0.1) @ R('Z', (i % 3 - 1) * 0.8), bevel=0.006)
    for sx in (-1, 1):   # axletrees
        pass
    for y in (-L / 2 + 0.45, L / 2 - 0.45):
        vkit.rod(p, (-W / 2 - 0.25, y, wheel_r), (W / 2 + 0.25, y, wheel_r), 0.05, 'iron', seg=10)
        for sx in (-1, 1):   # the axle blocks under the sills
            p.add(props.box(0.16, 0.22, max(0.05, zr - wheel_r + 0.1)), 'wood_dark',
                  T(sx * W / 2, y, (zr + wheel_r) / 2 - 0.02), bevel=0.01)


def _crew(root, crew):
    """The crew empties: (pos, yaw[, reach]) each; reach ({'l'|'r': (x, y, z)} in the root's space) is stored on
    the empty for crews.populate, over crews.CREW_R's."""
    import json
    for i, c in enumerate(crew):
        e = vkit.crew(root, i, tuple(c[0]), yaw=c[1])
        if len(c) > 2 and c[2]:
            e['reach'] = json.dumps({k: list(v) for k, v in c[2].items()})


def _wheels(p, W, L, wheel_r, solid=False, n_spokes=6, width=0.13):
    for sx in (-1, 1):
        for y in (-L / 2 + 0.45, L / 2 - 0.45):
            c = (sx * (W / 2 + 0.17), y, wheel_r)
            if solid:   # a solid truck of two planks, an iron tire and a hub
                p.add(props.lathe([(-width / 2, wheel_r * 0.96), (width / 2, wheel_r * 0.96)], seg=24), 'wheel',
                      T(*c) @ R('Y', 90), smooth=True)
                for f in (-1, 1):
                    p.add(props.ellipsoid(wheel_r * 0.95, wheel_r * 0.95, 0.01, seg=24), 'wheel',
                          T(c[0] + f * width / 2, c[1], c[2]) @ R('Y', 90), smooth=True)
                vkit.ring_x(p, wheel_r, wheel_r * 0.93, width * 1.08, 'iron', c, seg=28)
                p.add(props.box(0.012, 0.02, wheel_r * 1.8), 'wood_dark', T(c[0] + sx * width / 2, c[1], c[2]), bevel=0.003)
                p.add(props.lathe([(0.0, 0.08), (0.06, 0.06), (0.09, 0.0)], seg=14), 'iron',
                      T(c[0] + sx * width / 2, c[1], c[2]) @ R('Y', sx * 90), smooth=True)
            else:
                vkit.spoked_wheel(p, wheel_r, width, 'felloe', 'wheel', 'iron', n_spokes=n_spokes, center=c,
                                  tire_key='iron', spoke_scale=1.3)


def _lashing(p, at, r, n=4, step=0.035, axis='X', key='rope', tint=False):
    """Rope turns bound round a member (rings along `axis`)."""
    x, y, z = at
    rot = {'X': R('Y', 90), 'Y': R('X', 90), 'Z': Matrix.Identity(4)}[axis]
    for j in range(n):
        o = (j - (n - 1) / 2) * step
        d = {'X': (o, 0, 0), 'Y': (0, o, 0), 'Z': (0, 0, o)}[axis]
        p.add(props.lathe([(-0.014, r), (0.014, r)], seg=14), key, T(x + d[0], y + d[1], z + d[2]) @ rot, smooth=True,
              tint=tint)


# ------------------------------------------------------------ Medieval Japanese Catapult (a traction trebuchet)

def mdj_catapult(colors, k=0.85, arm=28.0, arm_long=2.7, arm_short=1.2, za=2.2, lintel=2.3, sweep=0.22,
                 ropes=5, crew=None, lintel_tint=False, wrap_tint=False, spar=1.5, rope_r=0.026,
                 haul_hands=None, sling=0.78, L=2.2, planks=False,
                 legs=(-0.95, 1.0), th=0.9, fit_scale=1.12, reach_out=0.0, lintel_style='straight', sill_tint=True,
                 haul=None):
    """Round 2 (critics: the curved civ lintel read as a bow): lintel_style 'straight', a heavy plain cap beam;
    haul: [(rope index, hand point)], the ropes in the haulers' hands; sill_tint: the cart's sills (cheeks) civ."""
    if haul is None and haul_hands:
        haul = [(ropes - 2, haul_hands[0]), (ropes - 1, haul_hands[1])]
    if haul is None and not crew:
        haul = MDJ_CATAPULT_HAUL
    """A traction trebuchet on a cart: a trestle of two raking A-frames, a wide
    lacquered lintel with upswept ends (the shipped unit's bow-like crossbar),
    the arm of bundled spars bound with rope, cocked: the sling end down to
    the rear with the stone in its pouch, the short end up at the front with
    its hauling ropes hanging to the haulers' hands and the ground."""
    c = dict(MDJ_COLORS, **colors)
    root = vkit.root('Medieval Japanese Catapult')
    W, zr, wr = 1.05, 0.48, 0.34
    f = vkit.part(root, 'frame', c, MDJ_WEAR)
    _cart(f, L, W, zr, wr, planks=planks, sill=0.16 * th, sill_key='beam' if sill_tint else 'side_rail')
    if not planks:   # an open frame: two loose boards to stand on
        for y in (-0.55, 0.45):
            f.add(props.box(W - 0.1, 0.24, 0.04), 'wood', T(0, y, zr + 0.11), bevel=0.006)
    yp = 0.15   # the pivot's y
    for sx in (-1, 1):
        x = sx * (W / 2 - 0.02)
        # the A-frame: two raking legs from the sill to the pivot block, a tie between them
        for y0 in legs:
            vkit.beam(f, (x, y0, zr + 0.08), (x * 0.92, yp, za - 0.08), 0.13 * siege.TH * th, 0.13 * siege.TH * th, 'wood',
                      bevel=0.015)
            f.add(props.box(0.2, 0.2, 0.12), 'iron', T(x, y0, zr + 0.14), bevel=0.01)
        zt = zr + 0.55
        t = (zt - zr) / (za - zr)
        vkit.beam(f, (x * (1 - 0.08 * t), legs[0] + (yp - legs[0]) * t, zt), (x * (1 - 0.08 * t), legs[1] + (yp - legs[1]) * t, zt),
                  0.1 * siege.TH * th, 0.1 * siege.TH * th, 'wood')
        f.add(props.box(0.24, 0.3, 0.26), 'wood_dark', T(x * 0.92, yp, za - 0.06), bevel=0.02)   # the bearing block
    # the lintel: a wide beam over the trestle, its ends swept up (a kasagi), lacquered in the civ colour;
    # a straight tie (nuki) under it through both frames
    if lintel_style == 'straight':   # a heavy, straight, plain cap beam over the bearing blocks, iron-strapped
        vkit.beam(f, (-W / 2 - 0.2, yp, za + 0.16), (W / 2 + 0.2, yp, za + 0.16), 0.26, 0.24, 'wood_dark', bevel=0.025)
        for sx in (-1, 1):
            f.add(props.box(0.08, 0.3, 0.3), 'iron', T(sx * (W / 2 - 0.02), yp, za + 0.16), bevel=0.01)
        vkit.beam(f, (-W / 2 - 0.1, yp, za - 0.4), (W / 2 + 0.1, yp, za - 0.4), 0.16, 0.18, 'wood')
    else:
        n = 13
        pts = []
        for i in range(n):
            u = -1 + 2 * i / (n - 1)
            pts.append((u * lintel / 2, yp, za + 0.16 + sweep * abs(u) ** 2.4))
        f.add(props.tube(pts, [th * 0.11 if abs(-1 + 2 * i / (n - 1)) < 0.85 else th * 0.085 for i in range(n)], seg=8,
                         scale_xy=(1.0, 1.25)), 'beam' if lintel_tint else 'wood_dark', smooth=True, tint=lintel_tint)
        vkit.beam(f, (-W / 2 - 0.22, yp, za - 0.32), (W / 2 + 0.22, yp, za - 0.32), 0.12, 0.14,
                  'beam' if lintel_tint else 'wood', tint=lintel_tint)
        for sx in (-1, 1):   # posts from the tie up to the lintel, a dark cap on each lintel end
            vkit.beam(f, (sx * 0.2, yp, za - 0.32), (sx * 0.2, yp, za + 0.1), 0.08, 0.08, 'wood_dark')
            _lashing(f, (sx * (W / 2 - 0.05), yp, za - 0.32), 0.1, n=3, axis='X')
    # the pivot axle across the bearing blocks
    vkit.rod(f, (-W / 2 - 0.05, yp, za - 0.02), (W / 2 + 0.05, yp, za - 0.02), 0.05, 'iron', seg=10)
    # spare stones in a basket on the bed
    for i, (dx, dy) in enumerate(((0.18, -0.55), (-0.12, -0.5))):
        st = props.ellipsoid(0.12, 0.11, 0.1, seg=8)
        props.jitter(st, 0.025, seed=20 + i)
        f.add(st, 'stone', T(dx, dy, zr + 0.2), bevel=0.006)
    f.finish()

    w = vkit.part(root, 'wheels', c, MDJ_WEAR)
    _wheels(w, W, L, wr, n_spokes=6, width=0.13 * th)
    w.finish()

    # the arm: three spars bundled, bound with rope bands (civ-dyed wraps at the pivot and the short end)
    a = vkit.part(root, 'arm', c, MDJ_WEAR)
    for dx, dz, r0, r1 in ((0, 0, 0.1, 0.055), (0.07, -0.05, 0.07, 0.04), (-0.07, -0.05, 0.07, 0.04)):
        vkit.rod(a, (dx * spar, arm_short, dz * spar), (dx * 0.5 * spar, -arm_long, dz * 0.5 * spar), r0 * spar, 'side_rail',
                 seg=10, r1=r1 * spar)
    for y in [v for v in (0.75, -0.45, -0.95, -1.45, -1.95) if -arm_long < v < arm_short]:
        r = (0.13 - 0.03 * max(0.0, -y) / arm_long) * spar
        _lashing(a, (0, y, -0.02), r, n=3, step=0.03, axis='Y')
    if wrap_tint:   # broad civ-dyed cloth wraps: over the pivot and at the short end where the ropes hang
        for y0, y1 in ((-0.38, 0.3), (arm_short - 0.42, arm_short - 0.02)):
            a.add(props.lathe([(0.0, 0.15 * spar), (y1 - y0, 0.15 * spar)], seg=14), 'beam', T(0, y0, -0.02) @ R('X', -90), smooth=True,
                  tint=True)
    # the hauling ropes from a crossbar at the short end
    vkit.rod(a, (-0.32, arm_short - 0.12, 0.0), (0.32, arm_short - 0.12, 0.0), 0.035, 'wood_dark', seg=8)
    ao = a.finish()
    ao.matrix_parent_inverse = Matrix.Identity(4)
    ao.location = (0, yp, za)
    ao.rotation_euler = (math.radians(arm), 0, 0)   # the long end down to the rear
    import bpy
    bpy.context.view_layer.update()
    to_root = root.matrix_world.inverted() @ ao.matrix_world
    sa = math.radians(arm)
    tip = to_root @ Vector((0, -arm_long, 0))
    bar = [to_root @ Vector((x, arm_short - 0.12, 0)) for x in
           [(-0.3 + 0.6 * i / max(1, ropes - 1)) for i in range(ropes)]]

    r = vkit.part(root, 'ropes', c, MDJ_WEAR)
    # the hauling ropes: each from the bar down to the ground in front, the hauler's two in his hands
    held = dict((i, tuple(h)) for i, h in (haul or []))
    for i, b in enumerate(bar):
        if i in held:   # taut into the hauler's hands, the tail trailing behind him to the ground
            mid = held[i]
            r.add(props.tube([tuple(b), mid], rope_r, seg=6), 'rope', smooth=True)
            r.add(props.tube([mid, (mid[0], mid[1] + 0.22, mid[2] * 0.5), (mid[0], mid[1] + 0.55, 0.03)], rope_r * 0.9,
                             seg=6), 'rope', smooth=True)
        elif reach_out:   # laid out forward to the ground, ending at a wooden pull-bar for the haulers
            gy = b.y + reach_out
            r.add(props.tube([tuple(b), (b.x * 1.05, b.y + 0.3 * reach_out, b.z * 0.45), (b.x * 1.2, gy, 0.05)], rope_r,
                             seg=6), 'rope', smooth=True)
        else:   # hanging down, tied off to the cart's front beam until the haulers take them
            fy = L / 2 - 0.12
            r.add(props.tube([tuple(b), (b.x * 0.9, (b.y + fy) / 2 + 0.04, (b.z + zr) / 2), (b.x * 0.8, fy, zr + 0.12)],
                             rope_r, seg=6), 'rope', smooth=True)
            _lashing(r, (b.x * 0.8, fy, zr), 0.12, n=2, step=0.04, axis='X')
    if not held and reach_out:
        y0 = bar[0].y + reach_out
        vkit.rod(r, (bar[0].x * 1.2 - 0.15, y0, 0.05), (bar[-1].x * 1.2 + 0.15, y0, 0.05), 0.035, 'wood_dark', seg=8)
    # the sling: two cords from the arm's tip to the pouch, the stone in it (the loader holds the pouch)
    pouch = (tip.x, tip.y - 0.18, tip.z - sling)
    for sx in (-1, 1):
        r.add(props.tube([tuple(tip), (pouch[0] + sx * 0.12, pouch[1], pouch[2] + 0.08)], rope_r, seg=6), 'rope', smooth=True)
    r.add(props.ellipsoid(0.2, 0.17, 0.09, seg=12), 'leather', T(*pouch), smooth=True)
    st = props.ellipsoid(0.17, 0.16, 0.15, seg=10)
    props.jitter(st, 0.02, seed=5)
    r.add(st, 'stone', T(pouch[0], pouch[1], pouch[2] + 0.1), bevel=0.006)
    r.finish()

    root.scale = (k, k, k)
    root['fit_scale'] = fit_scale   # render_vehicles.fit: the sparse shipped outline, filled by a denser engine
    _crew(root, crew or MDJ_CATAPULT_CREW)
    root['mdj_tip'] = list(tip)
    root['mdj_pouch'] = list(pouch)
    return root


# round 2: two haulers in front, facing the engine, leaning back on the pull ropes (two ropes each; the middle one
# hangs slack); the stone lies in the sling's pouch on the ground behind (no man on the arm)
# round 3: 30 % wider apart (the frame shows between them in 2), staggered half a body (they merged in 4/8)
_HA = ((-0.91, 1.78, 1.38), (-0.71, 1.78, 1.38))
_HB = ((0.71, 2.1, 1.38), (0.91, 2.1, 1.38))
MDJ_CATAPULT_HAUL = ((0, _HA[0]), (1, _HA[1]), (3, _HB[0]), (4, _HB[1]))
MDJ_CATAPULT_CREW = (((-0.81, 2.08, 0.0), 180.0, {'l': _HA[0], 'r': _HA[1]}),
                     ((0.81, 2.4, 0.0), 180.0, {'l': _HB[0], 'r': _HB[1]}))


# ------------------------------------------------------------ Fire Cannon (a breech-loading furanki)

def fire_cannon(colors, k=0.95, elev=3.0, barrel=2.6, cheek_tint=True, crew=None, solid=True, tiller=True, gauge=1.45,
                linstock=False, wr=0.36, swell=1.35, chamber=1.2, fit_scale=0.83):
    """Round 2: a shorter gun (2.25 m) with a heavier muzzle swell; the breech bay the right way up (the
    barrel's local +Y is down in the world), so the chamber with its handle and the wedge stand proud on top;
    bigger trucks; a second crewman (the rammer), so the socketed linstock is off by default."""
    """A bronze breech-loader (furanki / kunikuzushi) on a low box carriage:
    the long barrel with a muzzle swell and raised rings, its rear an open
    bay holding the iron powder chamber (a mug with a handle), wedged in;
    the carriage's two cheeks of stacked beams stepped down to the rear, the
    middle beam lacquered in the civ colour, on four solid trucks."""
    c = dict(MDJ_COLORS, **colors)
    root = vkit.root('Fire Cannon')
    f = vkit.part(root, 'carriage', c, MDJ_WEAR)
    W = 0.66
    steps = ((0.95, -0.95, 0.42), (0.85, -0.55, 0.6), (0.75, -0.1, 0.78))   # (front y, back y, z) per stacked beam
    for sx in (-1, 1):
        x = sx * W / 2
        for i, (y1, y0, z) in enumerate(steps):
            civ = cheek_tint and i == 1
            vkit.beam(f, (x, y0, z), (x, y1, z), 0.16, 0.19, 'beam' if civ else ('side_rail' if i != 1 else 'wood'),
                      bevel=0.02, tint=civ)
        for y in (0.6, -0.3):   # iron bolts down through the stack
            vkit.rod(f, (x, y, 0.3), (x, y, 0.69 if y < 0 else 0.9), 0.022, 'iron', seg=6)
            f.add(props.ellipsoid(0.035, 0.035, 0.02, seg=8), 'iron', T(x, y, 0.69 + 0.1 if y < 0 else 0.9), smooth=True)
        f.add(props.box(0.2, 0.5, 0.2), 'iron', T(x, 0.55, 0.88), bevel=0.012)    # capsquare over the trunnion
    for y in (0.85, -0.85):   # transoms
        vkit.beam(f, (-W / 2, y, 0.42), (W / 2, y, 0.42), 0.14, 0.18, 'wood_dark')
    f.add(props.box(W - 0.1, 1.7, 0.05), 'wood', T(0, 0.0, 0.34), bevel=0.008)   # the bed
    f.add(props.box(0.3, 0.42, 0.16), 'wood_dark', T(0, -0.35, 0.62) @ R('X', 8), bevel=0.012)   # the quoin
    for y in (0.6, -0.6):   # axletrees
        vkit.beam(f, (-W / 2 - 0.12, y, wr), (W / 2 + 0.12, y, wr), 0.14, 0.14, 'wood_dark')
    # gear on the carriage: a rammer along the cheek; the linstock standing in its socket at the rear corner, its
    # slow match smouldering at the top
    vkit.rod(f, (W / 2 + 0.02, -0.8, 0.5), (W / 2 + 0.02, 0.9, 0.5), 0.02, 'wood_dark', seg=6)
    if linstock:
        lx, ly = -W / 2 - 0.02, -0.92
        f.add(props.box(0.08, 0.08, 0.14), 'iron', T(lx, ly, 0.5), bevel=0.01)
        vkit.rod(f, (lx, ly, 0.42), (lx - 0.05, ly - 0.08, 1.45), 0.022, 'wood_dark', seg=8)
        f.add(props.tube([(lx - 0.05, ly - 0.08, 1.45), (lx - 0.12, ly - 0.08, 1.5), (lx - 0.14, ly - 0.02, 1.42)], 0.02, seg=6),
              'rope', smooth=True)
        f.add(props.ellipsoid(0.03, 0.03, 0.03, seg=8), 'match', T(lx - 0.14, ly - 0.02, 1.41), smooth=True)
    f.finish()

    w = vkit.part(root, 'wheels', c, MDJ_WEAR)
    for sx in (-1, 1):
        for y in (0.6, -0.6):
            cc = (sx * (W / 2 + 0.17), y, wr)
            if solid:
                w.add(props.lathe([(-0.07, wr * 0.96), (0.07, wr * 0.96)], seg=24), 'wheel', T(*cc) @ R('Y', 90), smooth=True)
                for s2 in (-1, 1):
                    w.add(props.ellipsoid(wr * 0.95, wr * 0.95, 0.012, seg=24), 'wheel',
                          T(cc[0] + s2 * 0.07, cc[1], cc[2]) @ R('Y', 90), smooth=True)
                vkit.ring_x(w, wr, wr * 0.92, 0.15, 'iron', cc, seg=28)
                w.add(props.lathe([(0.0, 0.08), (0.05, 0.065), (0.08, 0.0)], seg=14), 'iron',
                      T(cc[0] + sx * 0.07, cc[1], cc[2]) @ R('Y', sx * 90), smooth=True)
            else:
                vkit.spoked_wheel(w, wr, 0.12, 'felloe', 'wheel', 'iron', n_spokes=6, center=cc, tire_key='iron')
    w.finish()

    # the gun along +Z of its own part, then laid along +Y
    b = vkit.part(root, 'barrel', c, MDJ_WEAR)
    bk = barrel / 2.5
    z0 = -0.55          # the bay's front (the chase starts here)
    prof = [(0.0, 0.15), (0.1, 0.15), (0.12, 0.135), (1.2, 0.115), (1.7, 0.1), (1.8, 0.12 * swell), (1.9, 0.12 * swell),
            (1.92, 0.105 * swell), (1.95, 0.05)]
    b.add(props.lathe([(z * bk, r_ * gauge) for z, r_ in prof], seg=24), 'gun_bronze', T(0, 0, z0), smooth=True)
    for zz, rr in ((0.03, 0.17), (0.55, 0.14), (1.05, 0.13), (1.6, 0.115)):   # raised rings
        b.add(props.lathe([(0.0, rr * gauge), (0.06, rr * gauge)], seg=24), 'gun_bronze', T(0, 0, z0 + zz * bk), smooth=True)
    # the breech bay: an open trough (two side walls and a floor) behind the chase, the chamber sitting in it
    bay = 0.62
    U = -1.0   # the world's up in the barrel's own space (laid along +Y by R('X', -90 + elev): local +Y points down)
    cr = 0.1 * gauge * chamber
    for sx in (-1, 1):   # low side walls, so the chamber stands above them
        b.add(props.box(0.05, 0.16 * gauge, bay), 'gun_bronze', T(sx * (cr + 0.04), -U * 0.04, z0 - bay / 2), bevel=0.01)
    b.add(props.box(2 * cr + 0.12, 0.06, bay), 'gun_bronze', T(0, -U * (cr * 0.75), z0 - bay / 2), bevel=0.01)   # floor
    b.add(props.box(2 * cr + 0.14, 0.26 * gauge, 0.08), 'gun_bronze', T(0, -U * 0.02, z0 - bay - 0.02), bevel=0.012)   # back plate
    # the chamber: an iron mug, its mouth to the chase, a big loop handle standing up on top
    b.add(props.lathe([(0.0, cr * 0.85), (0.04, cr), (0.36, cr), (0.4, cr * 0.9), (0.44, cr * 0.6)], seg=18), 'iron',
          T(0, U * 0.02, z0 - 0.52), smooth=True)
    for z in (z0 - 0.48, z0 - 0.16):   # its reinforcing hoops
        b.add(props.lathe([(0.0, cr + 0.012), (0.03, cr + 0.012)], seg=18), 'iron', T(0, U * 0.02, z), smooth=True)
    hz = cr + 0.02
    b.add(props.tube([(0, U * hz, z0 - 0.42), (0, U * (hz + 0.13), z0 - 0.37), (0, U * (hz + 0.13), z0 - 0.21),
                      (0, U * hz, z0 - 0.16)], 0.024, seg=8), 'iron', smooth=True)
    # the wedge driven down behind the chamber, standing proud of the bay
    b.add(props.box(2 * cr + 0.16, 0.2, 0.08), 'wood_dark', T(0, U * (cr * 0.55), z0 - bay + 0.05), bevel=0.012)
    if tiller:   # the tiller bar out of the back plate
        vkit.rod(b, (0, 0.0, z0 - bay - 0.05), (0, U * 0.03, z0 - bay - 0.35), 0.035, 'iron', seg=8)
        b.add(props.ellipsoid(0.05, 0.05, 0.065, seg=10), 'iron', T(0, U * 0.03, z0 - bay - 0.38), smooth=True)
    vkit.rod(b, (-0.25, 0, 0.0), (0.25, 0, 0.0), 0.05, 'iron', seg=10)   # trunnions
    bo = b.finish()
    bo.matrix_parent_inverse = Matrix.Identity(4)
    bo.location = (0, 0.55, 0.98)
    bo.rotation_euler = (math.radians(-90 + elev), 0, 0)

    root.scale = (k, k, k)
    root['fit_scale'] = fit_scale
    _crew(root, crew or FIRE_CANNON_CREW)
    return root


# the gunner at the breech with the linstock, the loader beside the carriage with a spare chamber
# the gunner at the breech: the left hand on the tiller, the right on the powder chamber's handle, seating it
# round 2: the gunner beside the breech (not behind it), facing the gun: the left hand on the chamber's handle,
# the right on the back plate; the rammer on the other side, his rammer upright
# round 3: both at the breech's flanks and rear (nobody between the camera and the barrel)
FIRE_CANNON_CREW = (((-0.68, -0.72, 0.0), -60.0, {'l': (-0.04, -0.3, 1.22), 'r': (-0.1, -0.64, 1.05)}),
                    ((0.7, -0.85, 0.0), 75.0))


# ------------------------------------------------------------ Siege Crossbow (a double-bow bed crossbow)

def _bow(p, y, span, bend, z, r, key, tint=False, sign=1.0, grip=0.0, grip_key='beam', grip_tint=True):
    """A wide composite bow across X at (y, z): its limbs curving toward
    y + sign * bend at the tips; returns the tips."""
    n = 15
    pts = []
    for i in range(n):
        u = -1 + 2 * i / (n - 1)
        pts.append((u * span / 2, y + sign * bend * u * u - sign * 0.04 * u ** 4, z))
    rad = [r * (1.0 - 0.45 * abs(-1 + 2 * i / (n - 1))) for i in range(n)]
    p.add(props.tube(pts, rad, seg=8, scale_xy=(1.0, 1.3)), key, smooth=True, tint=tint)
    if grip:   # bound at the middle (rattan or civ wraps)
        for u in (-0.18, 0.0, 0.18) if grip > 0.3 else (0.0,):
            p.add(props.lathe([(-grip / 6, r * 1.2), (grip / 6, r * 1.2)], seg=12), grip_key,
                  T(u * span / 2, y, z) @ R('Y', 90), smooth=True, tint=grip_tint)
    return pts[0], pts[-1]


def siege_crossbow(colors, k=0.9, span=(3.0, 2.5), crew=None, bow_tint=False, wraps=True, panel_tint=True, bolt=1.6,
                   bow_r=(0.13, 0.11), string_r=0.03, zs=1.15, rear_bow='bow_core', string='#8a7452', tips=True,
                   rack=True, stock_w=0.3, fit_scale=1.0):
    """Round 2: hemp-tan strings, thicker (white ones flickered at 1x); dark-lacquered bows with civ bindings
    and tips (bow_tint True: the whole front bow civ, as round 1); civ side boards; a heavier stock, iron fittings
    at the bow seats, a rack of spare bolts on the cart's side."""
    """A double-bow bed crossbow on a cart: a heavy stock on two trestles, two
    wide black-lacquered composite bows (the front one bent forward, the
    second backward), their strings drawn back to the trigger nut, a great
    bolt with an iron leaf head and wooden vanes in the groove, a windlass
    with capstan bars at the rear; civ-lacquered side boards on the cart."""
    c = dict(MDJ_COLORS, **dict({'string': string} if string else {}, **colors))
    root = vkit.root('Siege Crossbow')
    L, W, zr, wr = 2.4, 1.0, 0.45, 0.32
    f = vkit.part(root, 'frame', c, MDJ_WEAR)
    _cart(f, L, W, zr, wr)
    if panel_tint:   # lacquered side boards along the sills (the shipped unit's civ panels)
        for sx in (-1, 1):
            f.add(props.box(0.04, L - 0.5, 0.2), 'beam', T(sx * (W / 2 + 0.11), 0.0, zr + 0.02), bevel=0.008, tint=True)
    # two trestles carrying the stock
    for y in (0.75, -0.55):
        for sx in (-1, 1):
            vkit.beam(f, (sx * 0.42, y, zr + 0.08), (sx * 0.08, y, zs - 0.1), 0.1, 0.12, 'wood', bevel=0.012)
        vkit.beam(f, (-0.3, y, zr + 0.35), (0.3, y, zr + 0.35), 0.08, 0.08, 'wood_dark')
    # the stock: a heavy beam with a groove (two rails), iron bands
    vkit.beam(f, (0, -1.25, zs - 0.08), (0, 1.35, zs - 0.08), stock_w, 0.2, 'side_rail', bevel=0.02)
    for sx in (-1, 1):
        vkit.beam(f, (sx * 0.08, -1.2, zs + 0.01), (sx * 0.08, 1.35, zs + 0.01), 0.06, 0.05, 'wood_dark', bevel=0.008)
    for y in (1.25, 0.95, 0.45, -0.2, -0.9):
        f.add(props.box(0.27, 0.05, 0.2), 'iron', T(0, y, zs - 0.08), bevel=0.008)
    # the bow seats: blocks across the stock where each bow is lashed
    yb1, yb2 = 1.05, 0.5
    for y in (yb1, yb2):
        f.add(props.box(0.4, 0.2, 0.2), 'wood_dark', T(0, y, zs - 0.02), bevel=0.015)
        if rack:   # iron cheek plates bolted over each seat
            for sx in (-1, 1):
                f.add(props.box(0.03, 0.26, 0.24), 'iron', T(sx * 0.215, y, zs - 0.02), bevel=0.006)
    if rack:   # a rack of three spare bolts on brackets along the cart's right side
        for y in (-0.7, 0.6):
            vkit.beam(f, (W / 2 + 0.05, y, zr + 0.05), (W / 2 + 0.12, y, zr + 0.38), 0.05, 0.05, 'wood_dark')
        for j in range(3):
            zz = zr + 0.2 + 0.08 * j
            f.add(props.tube([(W / 2 + 0.14, -1.0, zz), (W / 2 + 0.14, 0.95, zz)], 0.03, seg=6), 'wood', smooth=True)
            f.add(props.lathe([(0.0, 0.045), (0.12, 0.0)], seg=4), 'iron', T(W / 2 + 0.14, 0.95, zz) @ R('X', -90), smooth=False)
    # the trigger nut (bronze) and the windlass at the rear
    yn = -0.75
    f.add(props.box(0.16, 0.14, 0.12), 'brass', T(0, yn, zs + 0.06), bevel=0.012)
    yw, zw = -1.05, zs - 0.12
    vkit.rod(f, (-0.5, yw, zw), (0.5, yw, zw), 0.1, 'wood_dark', seg=12)
    for layer, rr in enumerate((0.115, 0.135)):
        coil = [(-0.25 + 0.5 * i / 70, yw + rr * math.cos(i * 0.62 + layer), zw + rr * math.sin(i * 0.62 + layer))
                for i in range(71)]
        f.add(props.tube(coil, 0.018, seg=5), 'rope', smooth=True)
    for sx in (-1, 1):
        f.add(props.box(0.08, 0.2, 0.42), 'wood', T(sx * 0.36, yw, zw - 0.18), bevel=0.01)   # cheeks
        hub = (sx * 0.55, yw, zw)
        f.add(props.lathe([(-0.06, 0.12), (0.06, 0.12)], seg=12), 'wood_dark', T(*hub) @ R('Y', 90), smooth=True)
        for a in (20, 110):   # capstan bars through the hub, a cross of handspikes
            ra = math.radians(a)
            d = (0, 0.42 * math.cos(ra), 0.42 * math.sin(ra))
            vkit.rod(f, (hub[0], hub[1] - d[1], hub[2] - d[2]), (hub[0], hub[1] + d[1], hub[2] + d[2]), 0.025, 'wood', seg=8)
    vkit.rod(f, (0, yw, zw + 0.1), (0, yn - 0.05, zs + 0.06), 0.014, 'rope', seg=5)   # the windlass rope to the nut
    f.finish()

    w = vkit.part(root, 'wheels', c, MDJ_WEAR)
    _wheels(w, W, L, wr, n_spokes=6)
    w.finish()

    bw = vkit.part(root, 'bows', c, MDJ_WEAR)
    s1, s2 = span
    # the bows lacquered (in the civ colour, as Japanese bows were lacquered red or black), bound in dark rattan
    t1 = _bow(bw, yb1, s1, 0.24, zs + 0.0, bow_r[0], 'beam' if bow_tint else 'bow_core', tint=bow_tint, sign=1.0,
              grip=0.36 if wraps else 0.0, grip_key='bow_core' if bow_tint else 'beam', grip_tint=not bow_tint)
    if tips and not bow_tint:   # civ-lacquered caps on the front bow's tips
        for tp in t1:
            bw.add(props.ellipsoid(0.08, 0.12, 0.08, seg=10), 'beam', T(*tp), smooth=True, tint=True)
    t2 = _bow(bw, yb2, s2, 0.2, zs + 0.0, bow_r[1], 'beam' if bow_tint == 'both' else rear_bow, tint=bow_tint == 'both', sign=1.0,
              grip=0.3 if wraps else 0.0, grip_key='bow_core', grip_tint=False)
    for y in (yb1, yb2):   # rope lashing the bows to their seats
        _lashing(bw, (0, y, zs + 0.0), 0.11, n=4, step=0.04, axis='X')
    # the strings: drawn from each bow's tips back to the nut
    nut = (0, yn + 0.02, zs + 0.1)
    for tip in (t1[0], t1[1], t2[0], t2[1]):
        bw.add(props.tube([tip, nut], string_r, seg=6), 'string', smooth=True)
    # the bolt: a thick shaft from the nut out past the front, an iron leaf head, three wooden vanes
    yb0, yb1_ = yn + 0.05, 1.55 + 0.35 * (bolt - 1)
    zb = zs + 0.12
    bw.add(props.tube([(0, yb0, zb), (0, yb1_, zb)], 0.035 * bolt, seg=8), 'wood', smooth=True)
    head = props.lathe([(0.0, 0.0), (0.04, 0.07 * bolt), (0.22 * bolt, 0.05 * bolt), (0.34 * bolt, 0.0)], seg=4,
                       scale_xy=(1.0, 0.45))
    bw.add(head, 'iron', T(0, yb1_ - 0.02, zb) @ R('X', -90) @ R('Z', 90), smooth=False)
    for a in (90, 210, 330):   # vanes
        ra = math.radians(a)
        bw.add(props.box(0.012, 0.3, 0.07), 'wood_dark',
               T(0.06 * math.cos(ra), yb0 + 0.22, zb + 0.06 * math.sin(ra)) @ R('Y', -a + 90), bevel=0.003)
    bw.finish()

    root.scale = (k, k, k)
    root['fit_scale'] = fit_scale
    _crew(root, crew or SIEGE_CROSSBOW_CREW)
    return root


# the windlass man at the rear on the capstan bars, the aimer beside the stock with the trigger mallet
# round 2: spread out (they stacked in front of the bed in 5-7): the windlass man outside the left capstan, facing
# it, hauling a bar round; the aimer behind the stock's end, sighting along it
# round 3: the windlass man behind the left capstan, hauling its bar's lower end; the aimer behind the stock's
# right side, a metre apart (they merged in 1, 5, 6)
SIEGE_CROSSBOW_CREW = (((-0.6, -1.85, 0.0), 0.0, {'l': (-0.5, -1.45, 0.9), 'r': (-0.64, -1.42, 0.95)}),
                       ((0.5, -1.9, 0.0), 10.0, {'l': (0.14, -1.27, 1.12), 'r': (0.04, -1.3, 1.1)}))
_SIEGE_CROSSBOW_CREW_R1 = (((-0.35, -1.75, 0.0), 0.0),
                       ((0.95, -0.95, 0.0), 90.0, {'l': (0.15, -0.78, 1.22), 'r': (0.15, -1.02, 1.2)}))


BUILDERS = {'Medieval Japanese Catapult': mdj_catapult, 'Fire Cannon': fire_cannon, 'Siege Crossbow': siege_crossbow}
REALISM = {
    'Medieval Japanese Catapult': {'crew_realism': True},
    'Fire Cannon': {'crew_realism': True},
    'Siege Crossbow': {'crew_realism': True},
}
siege.BUILDERS.update(BUILDERS)
for _u, _v in REALISM.items():
    siege.REALISM.setdefault(_u, _v)
