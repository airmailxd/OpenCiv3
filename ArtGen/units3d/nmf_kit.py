"""PC2's numpy-mdj-foot props (Blender side): new names only, registered with
setdefault so nothing existing is replaced; mdj_kit.py (the other PC's) is not
touched. An entry lists it in its 'kits' after 'mdj_kit'.

- 'kuwa_broad': the Japanese hira-guwa, a broad flat iron hoe blade on a
  wooden head block at the top of a long haft (the Worker's still);
- 'nmf_ija_pack': the Imperial Army's square canvas pack with the blanket
  rolled in a horseshoe over its top and sides (civ when `tint`);
- 'nmf_xbow': mdj_kit's hand crossbow made to read at 1x (thicker black prod,
  paler tiller, pale bindings), the Stone Crossbow's;
- 'nmf_arisaka': the Type 38 rifle with the Type 30 bayonet, made to read at
  1x (a full-length stock, a dark barrel, a broad bright bayonet blade with a
  dark hilt), the Japanese Rifleman's;
- the hat 'nmf_kasa' (riders.HEADGEAR): a natural sedge kasa with a civ cap
  over its crown only (the Worker's; the whole kasa in civ was 30 %);
- the hat 'nmf_zukin': a katsuo-zukin that leaves the eyes and nose open (the
  Warrior Monk's; mdj_kit's 'zukin' covers the whole face);
- 'nmf_katana': yoroi's drawn katana with a thicker back, so it never breaks
  into dashes edge-on (the Minamoto Samurai's and the Hojo General's);
- 'nmf_naginata': mdj_kit's naginata with a longer, broader, more curved blade
  and a thicker back (the Warrior Monk's);
- 'nmf_ebira': mdj_kit's ebira set out behind the right hip as a back prop
  (the Otomo no Makuta's; mdj_kit's sat inside the body);
- 'nmf_yumi': mdj_kit's yumi in its own dark lacquer with a pale grip (Makuta's);
- 'nmf_saihai': a short dark baton with a full white paper tassel (the Hojo General's);
- 'nmf_agemaki': the civ bow knot on the armour's back (the Minamoto Samurai's);
- the hat 'nmf_hachimaki': a flat band tied at the back (the Otomo
  Spearman's; mdj_kit's hachimaki reads as a rolled turban band).

Prop space as props.py: the grip at the origin, the long axis +Z with the
working end up, a blade's face toward +Y; a back prop in mil_backpack's space
(the straps' top at the origin, the load behind, -Y).
"""
import props as P
import riders
import math

from props import T, R, S, box, ellipsoid, lathe, slab, tube

P.DEFAULTS.setdefault('nmf_tiller', ('#8a6440', 0.6, 0.0, 'wood'))
P.DEFAULTS.setdefault('nmf_prod', ('#24201c', 0.45, 0.0, 'leather'))
P.DEFAULTS.setdefault('nmf_straw', ('#b49a64', 0.85, 0.0, 'rope'))
P.DEFAULTS.setdefault('nmf_straw_dk', ('#7e6a44', 0.9, 0.0, 'rope'))
P.DEFAULTS.setdefault('nmf_stock', ('#4a2c1a', 0.55, 0.0, 'wood'))
P.DEFAULTS.setdefault('nmf_furoshiki', ('#8a7048', 0.9, 0.0, 'cloth'))
P.DEFAULTS.setdefault('nmf_khaki', ('#7a7050', 0.9, 0.0, 'cloth'))
P.DEFAULTS.setdefault('nmf_case', ('#a07a4c', 0.5, 0.0, 'wood'))


def kuwa_broad(p, length=1.25, blade_w=0.24, blade_l=0.26, scale=1.0, thick=0.012):
    """A hira-guwa: an oak haft, a squared wooden head block (dai) at its top
    and a broad flat iron blade fixed to the block, hanging back down toward
    the grip at a hoe's angle; a bright worn edge on the blade's bottom."""
    s = scale
    L = length * s
    p.add(tube([(0, 0, -0.18 * s), (0, 0, L)], [0.017, 0.015], seg=10), 'wood', bevel=0.002, smooth=True)
    # the head block across the haft's top, reaching toward +Y
    p.add(box(0.05 * s, 0.16 * s, 0.05 * s), 'wood', T(0, 0.06 * s, L + 0.01 * s), bevel=0.006)
    # the blade: a broad plate from the block's front, angled back toward the grip (+Y face out)
    ang = 25.0
    plate = T(0, 0.15 * s, L - blade_l * 0.42 * s) @ R('X', ang)
    p.add(box(blade_w * s, thick * s, blade_l * s), 'iron', plate, bevel=0.003)
    # the worn edge: a bright strip along the blade's working end
    p.add(box(blade_w * s * 0.98, (thick + 0.002) * s, 0.035 * s), 'steel',
          plate @ T(0, 0, -blade_l * 0.5 * s + 0.017 * s), bevel=0.002)
    # the iron socket strap round the block
    p.add(box(0.058 * s, 0.03 * s, 0.058 * s), 'iron', T(0, 0.13 * s, L + 0.01 * s), bevel=0.004)


def nmf_ija_pack(p, size=1.0, tint=False, roll=0.045, flap=False, tin=1.0):
    """The IJA infantry pack: a squared canvas pack, the blanket rolled round its
    top and down both sides in a horseshoe (the civ colour with `tint`), the
    mess tin strapped on the back, the shoulder straps."""
    s = size
    p.add(box(0.3 * s, 0.13 * s, 0.32 * s), 'olive_dark', T(0, -0.13 * s, -0.1 * s), bevel=0.03)
    key = 'tint' if tint else 'olive'
    r = roll * s
    shoe = [(-0.19 * s, -0.13 * s, -0.24 * s), (-0.19 * s, -0.13 * s, 0.02 * s), (-0.15 * s, -0.13 * s, 0.1 * s),
            (0.0, -0.13 * s, 0.12 * s), (0.15 * s, -0.13 * s, 0.1 * s), (0.19 * s, -0.13 * s, 0.02 * s),
            (0.19 * s, -0.13 * s, -0.24 * s)]
    p.add(tube(shoe, r, seg=12), key, smooth=True, tint=tint)
    for z in (-0.1 * s, 0.04 * s):        # the binding straps over the roll
        p.add(box(0.44 * s, 0.17 * s, 0.018 * s), 'leather_dark', T(0, -0.13 * s, z), bevel=0.004)
    # the mess tin (`tin` scales it), a darker oval on the pack's back
    p.add(box(0.13 * s * tin, 0.05 * s * tin, 0.16 * s * tin), 'olive_dark' if tin > 1.0 else 'olive',
          T(0, -0.2 * s - 0.025 * s * tin, -0.12 * s), bevel=0.02)
    if flap:
        # the pack's top flap in the civ colour (with `tint`), down over its back
        p.add(box(0.31 * s, 0.135 * s, 0.1 * s), 'tint' if tint else 'olive', T(0, -0.135 * s, 0.035 * s), bevel=0.02,
              tint=tint)
    for x in (-0.09 * s, 0.09 * s):
        path = [(x, -0.06 * s, 0.1 * s), (x, 0.0, 0.14 * s), (x, 0.1 * s, 0.1 * s), (x * 1.05, 0.12 * s, -0.1 * s)]
        p.add(tube(path, 0.015 * s, seg=6, scale_xy=(1.0, 0.35)), 'leather_dark', smooth=True)


def nmf_xbow(p, length=0.8, span=0.9, scale=1.0, bolt=True):
    """mdj_kit's hand crossbow made to read at 1x: the same layout (held at the
    tiller's wrist, the bow end +Z, the groove +Y), a paler and deeper tiller, a
    thick black-lacquered prod with pale cord bindings at its centre and tips,
    a pale string to the bronze nut, a bolt with a bright head."""
    s = scale
    L = length * s
    tiller = slab([(-0.04, -0.24), (0.03, -0.24), (0.028, 0.0), (0.024, L), (-0.024, L), (-0.028, 0.08), (-0.06, -0.08)],
                  0.055, 'X')
    p.add(tiller, 'nmf_tiller', bevel=0.005)
    zb = L - 0.05
    pts = [(span / 2 * u, 0.0, zb - 0.1 * u * u) for u in [i / 8 - 1 for i in range(17)]]
    radii = [0.016 + 0.016 * (1 - abs(i / 8 - 1)) ** 0.7 for i in range(17)]
    p.add(tube(pts, radii, seg=8, scale_xy=(1.0, 1.4)), 'nmf_prod', smooth=True)
    p.add(box(0.08, 0.06, 0.07), 'cord', T(0, 0.0, zb), bevel=0.008)
    for q in (pts[1], pts[-2]):
        p.add(tube([(q[0] - 0.02, 0, q[2]), (q[0] + 0.02, 0, q[2])], 0.022, seg=8), 'cord', smooth=True)
    nut = (0.0, 0.035, 0.07)
    p.add(tube([pts[0], nut, pts[-1]], 0.0025, seg=5), 'string', smooth=True)
    p.add(box(0.035, 0.03, 0.035), 'bronze', T(0, 0.032, 0.07), bevel=0.004)
    p.add(tube([(0, -0.02, 0.07), (0, -0.08, 0.0)], 0.008, seg=6), 'bronze', smooth=True)
    if bolt:
        p.add(tube([(0, 0.038, 0.08), (0, 0.038, L - 0.01)], 0.009, seg=6), 'wood', smooth=True)
        p.add(lathe([(0.0, 0.02), (0.05, 0.008)], seg=6), 'steel', T(0, 0.038, L - 0.01), bevel=0.0005)


def nmf_arisaka(p, length=1.28, bayonet=0.4, scale=1.0):
    """An Arisaka Type 38 held at the wrist of the stock (origin), muzzle +Z,
    sights +Y: the stock and handguard of warm wood nearly to the muzzle, a dark
    barrel, the bolt with its round knob on the right, two dark bands; the Type
    30 bayonet: a dark hilt and hooked crossguard at the muzzle and a broad,
    single-edged bright blade."""
    s = scale
    L = (length - 0.36) * s              # the muzzle above the wrist
    stock = slab([(-0.055, -0.36), (0.035, -0.36), (0.04, -0.12), (0.022, 0.0), (0.024, L - 0.1), (-0.014, L - 0.1),
                  (-0.018, 0.02), (-0.045, -0.1)], 0.05, 'X')
    p.add(stock, 'wood', S(s), bevel=0.006)
    p.add(tube([(0, 0.026, 0.0), (0, 0.026, L)], 0.013 * s, seg=10), 'iron', smooth=True)
    p.add(box(0.034, 0.034, 0.12), 'iron', T(0, 0.02, 0.05 * s), bevel=0.004)          # the receiver
    p.add(tube([(-0.018, 0.03, 0.06 * s), (-0.06, 0.02, 0.05 * s)], 0.006, seg=6), 'iron', smooth=True)   # the bolt
    p.add(ellipsoid(0.014, 0.014, 0.014, seg=8), 'iron', T(-0.062, 0.02, 0.05 * s), smooth=True)
    for z in (0.32, L / s - 0.14):
        p.add(lathe([(0.0, 0.03), (0.02, 0.03)], seg=10), 'iron', T(0, 0.008, z * s), smooth=True)
    if bayonet:
        b = bayonet * s
        p.add(box(0.026, 0.05, 0.1 * s), 'leather_dark', T(0, -0.004, L - 0.05 * s), bevel=0.004)   # the hilt
        p.add(box(0.016, 0.09, 0.016), 'iron', T(0, 0.0, L + 0.004), bevel=0.003)                  # the guard
        blade = slab([(-0.026, 0.0), (0.016, 0.0), (0.012, b * 0.85), (-0.004, b), (-0.026, b * 0.78)], 0.026, 'X')
        p.add(blade, 'steel', T(0, 0.026, L + 0.012), bevel=0.0015)


@riders.hat('nmf_kasa')
def nmf_kasa(p, c, tinted, _, brim=0.26, height=0.11, crown=0.45, weave=False):
    """A farmer's sugegasa in natural sedge with the civ colour (when tinted) as a
    cloth cap over its crown out to `crown` of the brim, a dark knot at the apex,
    the woven rings, a pale chin cord."""
    key, t = ('tint', True) if tinted else ('cloth_wrap', False)

    def z_at(r):
        # the cone's height at radius r (the lathe's profile, piecewise)
        prof = [(brim, 0.0), (brim * 0.96, 0.012), (brim * 0.5, 0.03 + height * 0.5), (0.0, 0.03 + height)]
        for (r0, z0), (r1, z1) in zip(prof, prof[1:]):
            if r1 <= r <= r0:
                return z0 + (z1 - z0) * (r0 - r) / (r0 - r1)
        return 0.03 + height
    p.add(lathe([(0.0, brim), (0.012, brim * 0.96), (0.03 + height * 0.5, brim * 0.5), (0.03 + height, 0.0)],
                seg=36), 'nmf_straw', smooth=True)
    if weave:
        # the sedge's weave: close rings of a darker straw and radial ribs from the apex to the rim
        for k in range(1, 10):
            rr = brim * (1 - 0.1 * k)
            p.add(lathe([(z_at(rr) - 0.0025, rr + 0.003), (z_at(rr) + 0.0025, rr + 0.003)], seg=36), 'nmf_straw_dk',
                  smooth=True)
        for k in range(16):
            a = 2 * math.pi * k / 16
            path = [(r * math.cos(a), r * math.sin(a), z_at(r) + 0.003) for r in (brim * 0.98, brim * 0.5, brim * 0.05)]
            p.add(tube(path, 0.0035, seg=4), 'nmf_straw_dk', smooth=True)
    else:
        for k in range(1, 4):   # the woven rings
            rr = brim * (1 - 0.2 * k)
            p.add(lathe([(z_at(rr) - 0.002, rr + 0.003), (z_at(rr) + 0.002, rr + 0.003)], seg=36), 'cord', smooth=True)
    rc = brim * crown       # the civ cap over the crown, a little proud of the sedge
    prof = ([(z_at(rc) + 0.004, rc + 0.004)] + [(z_at(r) + 0.006, r + 0.002) for r in (rc * 0.66, rc * 0.33)] +
            [(0.03 + height + 0.006, 0.0)])
    p.add(lathe(prof, seg=36), key, smooth=True, tint=t)
    p.add(lathe([(z_at(rc) + 0.002, rc + 0.007), (z_at(rc) + 0.008, rc + 0.007)], seg=36), 'lacquer', smooth=True)
    p.add(lathe([(0.03 + height * 0.9, brim * 0.06), (0.03 + height + 0.014, 0.0)], seg=12), 'lacquer', smooth=True)
    p.add(lathe([(0.0, 0.11), (0.05, 0.11)], seg=24), 'cloth_wrap', smooth=True)
    for sx in (-1, 1):
        p.add(tube([(sx * 0.1, 0.0, 0.01), (sx * 0.08, 0.02, -0.1), (0.0, 0.05, -0.18)], 0.005, seg=4), 'cord',
              smooth=True)


def nmf_matchlock(p, length=1.35, scale=1.0, match=True):
    """A tanegashima built to read as a gun at 1x, held at the wrist (origin),
    muzzle +Z, sights +Y: a wide dark stock with a deep down-curved butt, a thick
    octagonal dark-iron barrel to the muzzle with a swelled muzzle, the brass lock
    plate, serpentine and pan cover at the hand, brass pins, and the slow match:
    a pale cord coiled round the wrist and trailing from the serpentine."""
    s = scale
    L = length * s
    stock = slab([(-0.1, -0.32), (-0.02, -0.34), (0.026, -0.12), (0.026, 0.0), (0.022, L - 0.16), (-0.02, L - 0.16),
                  (-0.026, 0.06), (-0.05, -0.04), (-0.1, -0.2)], 0.06, 'X')
    p.add(stock, 'nmf_stock', bevel=0.007)
    p.add(tube([(0, 0.04, -0.05), (0, 0.04, L)], [0.021 * s, 0.017 * s], seg=8), 'iron', bevel=0.001)
    p.add(tube([(0, 0.058, 0.02), (0, 0.055, L - 0.02)], 0.006 * s, seg=6), 'steel', smooth=True)   # the polished flat
    p.add(lathe([(0.0, 0.023 * s), (0.04, 0.023 * s)], seg=8), 'iron', T(0, 0.04, L - 0.04))
    p.add(box(0.008, 0.016, 0.026), 'iron', T(0, 0.062, L - 0.08))
    # the lock at the hand, right side (-X): plate, serpentine, pan and its cover
    p.add(box(0.008, 0.045, 0.15), 'bronze', T(-0.032, 0.01, 0.0), bevel=0.002)
    p.add(tube([(-0.038, 0.0, -0.05), (-0.044, 0.04, 0.0), (-0.044, 0.05, 0.035)], 0.008, seg=6), 'bronze', smooth=True)
    p.add(box(0.024, 0.03, 0.04), 'bronze', T(-0.036, 0.04, 0.055), bevel=0.004)
    p.add(box(0.014, 0.03, 0.05), 'bronze', T(0, -0.035, -0.04), bevel=0.004)          # the trigger guard plate
    for z in (0.3, 0.62, L - 0.38):
        p.add(tube([(-0.034, 0.012, z), (0.034, 0.012, z)], 0.006, seg=6), 'bronze', smooth=True)
    if match:
        ring = [(0.05 * math.cos(2 * math.pi * k / 12), 0.05 * math.sin(2 * math.pi * k / 12), 0.14 + 0.007 * k)
                for k in range(25)]
        p.add(tube(ring, 0.007, seg=5), 'cord', smooth=True)
        p.add(tube([(-0.044, 0.05, 0.035), (-0.06, 0.02, 0.09), (-0.05, 0.0, 0.14)], 0.006, seg=5), 'cord', smooth=True)


def nmf_furoshiki(p, scale=1.0, mat=True, tint_ties=False, soft=False):
    """A peasant's load: a square cloth parcel (furoshiki) on the back (mil_backpack's
    space: the straps' top at the origin, the load behind, -Y), a deep, squared bundle
    with its two top corners knotted into ears, the cloth's long ends taken over both
    shoulders and knotted on the chest (+Y; the civ colour with `tint_ties`); a rolled
    straw mat across its top, below the head."""
    s = scale
    if soft:
        # soft knotted cloth: a full, squarish bundle with soft corners, gathered to a knot on top
        p.add(box(0.44 * s, 0.32 * s, 0.38 * s), 'nmf_furoshiki', T(0, -0.21 * s, -0.17 * s), bevel=0.11 * s)
        p.add(ellipsoid(0.07 * s, 0.05 * s, 0.05 * s, seg=12), 'nmf_furoshiki', T(0, -0.2 * s, 0.03 * s), smooth=True)
        for sx in (-1, 1):
            p.add(ellipsoid(0.05 * s, 0.02 * s, 0.035 * s, seg=10), 'nmf_furoshiki',
                  T(sx * 0.06 * s, -0.2 * s, 0.07 * s) @ R('Y', sx * 35), smooth=True)
    else:
        p.add(box(0.38 * s, 0.28 * s, 0.36 * s), 'nmf_furoshiki', T(0, -0.2 * s, -0.21 * s), bevel=0.07 * s)
    # the cloth's folds: darker creases gathered up to the knot
    for x in (() if soft else (-0.1, 0.0, 0.1)):
        p.add(tube([(x * 1.5 * s, -0.33 * s, -0.36 * s), (x * s, -0.35 * s, -0.2 * s), (x * 0.4 * s, -0.3 * s, -0.02 * s)],
                   0.007 * s, seg=5), 'nmf_straw_dk', smooth=True)
    for sx in (-1, 1):
        # the knotted top corners (ears)
        p.add(ellipsoid(0.045 * s, 0.035 * s, 0.04 * s, seg=10), 'nmf_furoshiki', T(sx * 0.16 * s, -0.14 * s, 0.0),
              smooth=True)
    tk = 'tint' if tint_ties else 'nmf_furoshiki'
    for sx in (-1, 1):
        # the long ends over the shoulders to the chest knot (+Y in front)
        path = [(sx * 0.16 * s, -0.12 * s, 0.0), (sx * 0.14 * s, -0.02 * s, 0.05 * s), (sx * 0.12 * s, 0.07 * s, 0.04 * s),
                (sx * 0.1 * s, 0.13 * s, -0.04 * s), (sx * 0.03 * s, 0.16 * s, -0.16 * s)]
        p.add(tube(path, 0.024 * s, seg=6, scale_xy=(1.0, 0.45)), tk, smooth=True, tint=tint_ties)
    p.add(ellipsoid(0.042 * s, 0.032 * s, 0.038 * s, seg=10), tk, T(0, 0.165 * s, -0.17 * s), smooth=True, tint=tint_ties)
    for sx in (-1, 1):                           # the knot's two tails
        p.add(box(0.028 * s, 0.012 * s, 0.08 * s), tk,
              T(sx * 0.022 * s, 0.17 * s, -0.23 * s) @ R('Y', sx * 15), bevel=0.003, tint=tint_ties)
    if mat:
        p.add(tube([(-0.26 * s, -0.2 * s, -0.01 * s), (0.26 * s, -0.2 * s, -0.01 * s)], 0.045 * s, seg=14), 'nmf_straw',
              smooth=True)
        for x in (-0.2, 0.2):
            p.add(lathe([(-0.006, 0.047 * s), (0.006, 0.047 * s)], seg=14), 'cord',
                  T(x * s, -0.2 * s, -0.01 * s) @ R('Y', 90), smooth=True)


def nmf_pouch(p, scale=1.0, key='leather_dark'):
    """A leather cartridge pouch on the belt (hip_gear: origin at the belt, +Z down):
    a squared box with a flap."""
    s = scale
    p.add(box(0.11 * s, 0.06 * s, 0.085 * s), key, T(0, 0, 0.045 * s), bevel=0.008)
    p.add(box(0.115 * s, 0.065 * s, 0.03 * s), key, T(0, 0.0, 0.012 * s), bevel=0.006)


def nmf_canteen(p, scale=1.0):
    """The IJA's canteen in its strap harness at the hip (hip_gear: origin at the
    belt, +Z down): a flat oval khaki-covered bottle, dark straps, a dark cap."""
    s = scale
    p.add(ellipsoid(0.055 * s, 0.035 * s, 0.08 * s, seg=14), 'nmf_khaki', T(0, 0, 0.1 * s), smooth=True)
    for z in (0.07, 0.13):
        p.add(lathe([(-0.005, 0.058 * s), (0.005, 0.058 * s)], seg=14, scale_xy=(1.0, 0.66)), 'leather_dark',
              T(0, 0, z * s), smooth=True)
    p.add(lathe([(0.0, 0.012), (0.03, 0.012)], seg=8), 'leather_dark', T(0, 0, 0.0), smooth=True)


def nmf_bolt_quiver(p, bolts=6, scale=1.0, length=0.32, key='lacquer'):
    """A crossbowman's bolt case at the hip (hip_gear: origin at the belt, +Z down):
    a squared lacquered box open at the top, the bolts' shafts and fletching
    standing out of its mouth (-Z, up)."""
    s = scale
    p.add(box(0.1 * s, 0.07 * s, length * s), key, T(0, 0, (0.02 + length / 2) * s), bevel=0.008)
    p.add(box(0.105 * s, 0.075 * s, 0.025 * s), 'cord', T(0, 0, 0.06 * s), bevel=0.004)
    for i in range(bolts):
        x = (-0.035 + 0.07 * i / max(1, bolts - 1)) * s
        y = (0.012 if i % 2 else -0.012) * s
        p.add(tube([(x, y, 0.05 * s), (x * 1.1, y, -0.07 * s)], 0.006, seg=4), 'wood', smooth=True)
        p.add(box(0.004, 0.026, 0.05), 'fletch', T(x * 1.1, y, -0.05 * s), bevel=0.0)


def nmf_katana(p, length=0.8, width=0.06, curve=0.03, scale=1.0, grip=0.27, spine=0.012):
    """yoroi's drawn katana with a thicker back (`spine`, half the blade's
    thickness at the habaki; yoroi's is 0.0045): seen edge-on at 1x the blade
    stays a line instead of breaking into 1 px dashes (Minamoto, Hojo)."""
    L = length * scale
    n = 16
    rings = []
    for i in range(n + 1):
        t = i / n
        z = 0.075 + L * t
        bow = -curve * scale * t ** 1.6
        hw = width / 2 * scale * (1.0 - 0.2 * t if t < 0.86 else (1 - t) / 0.14 * 0.6 + 0.23)   # a blunt kissaki
        back = spine * scale * (1 - 0.4 * t)
        rings.append([(0.0, bow - hw, z), (back, bow + hw * 0.3, z), (0.0, bow + hw, z), (-back, bow + hw * 0.3, z)])
    p.add(P.loft(rings), 'steel', bevel=0.0006, angle=50.0)
    p.add(box(0.026, width * 1.05, 0.03), 'gold', T(0, 0, 0.075), bevel=0.002)                       # habaki
    p.add(lathe([(0.0, 0.042), (0.008, 0.042)], seg=20), 'iron', T(0, 0, 0.056), bevel=0.002)       # tsuba
    p.add(tube([(0, 0, -grip), (0, 0, 0.056)], [0.016, 0.017], seg=10, scale_xy=(0.8, 1.0)), 'cloth_wrap',
          smooth=True)
    P._wrap(p, -grip + 0.01, 0.05, 0.0172, 'leather_dark', turns=11)
    p.add(ellipsoid(0.017, 0.015, 0.013, seg=12), 'iron', T(0, 0, -grip - 0.006), smooth=True)      # kashira


def nmf_naginata(p, length=2.5, grip=1.1, blade=0.78, width=0.095, curve=0.16, scale=1.0, thick=0.012):
    """mdj_kit's naginata made to read at 1x: a longer, broader blade with a
    strong sori (its curve shows whenever the flat faces the camera) and a
    thicker back, so edge-on it stays a line; the same shaft, tsuba, habaki,
    dogane and ishizuki."""
    from mdj_kit import _blade
    L = length * scale
    z0, z1 = -grip, L - grip
    zb = z1 - blade
    p.add(tube([(0, 0, z0 + 0.05), (0, 0, zb)], [P.SHAFT_R + 0.005, P.SHAFT_R + 0.003], seg=10, scale_xy=(0.85, 1.0)),
          'wood', smooth=True)
    _blade(p, zb + 0.03, blade, width, curve=curve, thick=thick)
    p.add(box(0.03, width * 0.95, 0.04), 'gold', T(0, 0.004, zb + 0.02), bevel=0.002)                     # habaki
    p.add(lathe([(0.0, 0.05), (0.01, 0.05)], seg=18), 'iron', T(0, 0, zb - 0.006), bevel=0.002)          # tsuba
    for z in (zb - 0.06, zb - 0.16):                                                                       # dogane
        p.add(lathe([(0.0, P.SHAFT_R + 0.007), (0.025, P.SHAFT_R + 0.007)], seg=10), 'iron', T(0, 0, z), smooth=True)
    p.add(lathe([(0.0, P.SHAFT_R + 0.005), (0.1, P.SHAFT_R + 0.005), (0.12, 0.004)], seg=10), 'iron',
          T(0, 0, z0 + 0.12) @ R('X', 180), smooth=True)


def nmf_ebira(p, arrows=10, scale=1.0, back=0.17, side=0.1, drop=0.1, lean=-14.0, top=0.3):
    """mdj_kit's ebira worn as a back prop (units' generic 'back' places a
    prop's origin at the spine, where mdj_kit's sank into the body): the same
    open box quiver moved `back` m behind and `side` m to the right, `drop` m
    down, leaned out over the right shoulder by `lean` degrees, so its frame
    shows at the right hip and the arrows' fan above the shoulder."""
    s = scale
    M = T(side, -back, -drop) @ R('Y', lean)
    p.add(box(0.13 * s, 0.07 * s, 0.1 * s), 'lacquer', M @ T(0, 0, -0.42 * s), bevel=0.006)
    for sx in (-1, 1):
        p.add(box(0.012 * s, 0.012 * s, 0.42 * s), 'lacquer', M @ T(sx * 0.055 * s, 0.0, -0.21 * s), bevel=0.002)
    p.add(box(0.13 * s, 0.014 * s, 0.03 * s), 'lacquer', M @ T(0, 0.0, -0.02 * s), bevel=0.003)
    for k in range(arrows):
        x = (-0.05 + 0.1 * k / max(1, arrows - 1)) * s
        a = (k - (arrows - 1) / 2) * 2.5
        p.add(tube([(x, 0.0, -0.4 * s), (x, 0.0, top * s)], 0.007, seg=5), 'bamboo', M @ R('Y', a), smooth=True)
        fl = slab([(0.0, 0.0), (0.026, 0.03), (0.028, 0.14), (0.0, 0.13)], 0.003, 'X')
        p.add(fl, 'fletch', M @ R('Y', a) @ T(x, 0.0, (top - 0.16) * s) @ R('Z', 90))


P.DEFAULTS.setdefault('nmf_bow', ('#221a16', 0.35, 0.0, 'leather'))
P.DEFAULTS.setdefault('nmf_grip', ('#e0d4b4', 0.8, 0.0, 'rope'))


def nmf_yumi(p, height=2.3, below=0.8, bend=0.12, string=True, scale=1.0):
    """mdj_kit's yumi in its own dark lacquer ('nmf_bow', apart from the
    armour's 'lacquer') with a pale grip binding ('nmf_grip') and pale rattan
    bands, so the bow reads dark against a light-armoured archer."""
    h = height * scale
    lo, hi = -below * scale, h - below * scale
    n = 24
    pts = []
    for i in range(n + 1):
        t = i / n
        z = lo + (hi - lo) * t
        u = (z - lo) / (hi - lo) * 2 - 1
        y = -bend * (z / (h / 2)) ** 2
        tip = max(0.0, abs(u) - 0.85) / 0.15
        y += 0.06 * tip * tip
        pts.append((0.0, y, z))
    radii = [0.009 + 0.009 * (1 - abs(-1 + 2 * i / n)) ** 0.6 for i in range(n + 1)]
    p.add(tube(pts, radii, seg=8, scale_xy=(1.0, 0.8)), 'nmf_bow', smooth=True)
    for i in range(2, n - 1, 3):
        x, y, z = pts[i]
        p.add(lathe([(0.0, radii[i] + 0.002), (0.012, radii[i] + 0.002)], seg=8), 'rattan', T(x, y, z - 0.006),
              smooth=True)
    gy = pts[n // 3][1]
    p.add(tube([(0, gy, -0.1), (0, gy, 0.08)], 0.022, seg=10), 'nmf_grip', smooth=True)
    if string:
        p.add(tube([pts[0], (0.0, min(pts[0][1], pts[-1][1]) - 0.02, 0.0), pts[-1]], 0.003, seg=5), 'string',
              smooth=True)


def nmf_saihai(p, length=0.36, strips=22, strip_l=0.3, scale=1.0):
    """A saihai made unmistakable at 1x: a short dark lacquered baton with a
    gilt cap and a big full tassel of white paper strips springing from its
    head and falling in a mop round it (never a straight blade)."""
    s = scale
    L = length * s
    p.add(tube([(0, 0, -0.08 * s), (0, 0, L)], 0.014, seg=8), 'lacquer', smooth=True)
    p.add(ellipsoid(0.024, 0.024, 0.026, seg=10), 'gold', T(0, 0, L), smooth=True)
    for k in range(strips):
        a = 2 * math.pi * k / strips + (0.3 if k % 2 else 0.0)
        out = 0.03 + 0.02 * (k % 3)
        lean = 28 + 10 * (k % 2)
        p.add(box(0.03, 0.004, strip_l * s), 'paper',
              T(0, 0, L + 0.01 * s) @ R('Z', math.degrees(a)) @ T(0, out, 0) @ R('X', 180 - lean) @
              T(0, 0, strip_l * s / 2), bevel=0.0008)


def nmf_agemaki(p, scale=1.0, back=0.17, drop=0.3, tint=True):
    """An agemaki: the big ornamental bow knot of cord tied on the back of
    the armour (two loops spread sideways, two tails hanging), in the civ
    colour with `tint`; set `back` m behind the spine and `drop` m down, as a
    back prop."""
    s = scale
    key, t = ('tint', True) if tint else ('cord', False)
    c0 = (0.0, -back * s, -drop * s)
    p.add(ellipsoid(0.035 * s, 0.025 * s, 0.03 * s, seg=10), key, T(*c0), smooth=True, tint=t)
    for sx in (-1, 1):
        loop = [(c0[0] + sx * 0.02 * s, c0[1], c0[2] + 0.01 * s), (c0[0] + sx * 0.08 * s, c0[1] - 0.01 * s, c0[2] + 0.07 * s),
                (c0[0] + sx * 0.14 * s, c0[1] - 0.01 * s, c0[2] + 0.02 * s), (c0[0] + sx * 0.09 * s, c0[1], c0[2] - 0.03 * s),
                (c0[0] + sx * 0.02 * s, c0[1], c0[2] - 0.005 * s)]
        p.add(tube(loop, 0.014 * s, seg=6), key, smooth=True, tint=t)
        tail = [(c0[0] + sx * 0.015 * s, c0[1], c0[2] - 0.02 * s), (c0[0] + sx * 0.05 * s, c0[1] + 0.005 * s, c0[2] - 0.14 * s),
                (c0[0] + sx * 0.06 * s, c0[1] + 0.01 * s, c0[2] - 0.24 * s)]
        p.add(tube(tail, 0.012 * s, seg=6), key, smooth=True, tint=t)


@riders.hat('nmf_hachimaki')
def nmf_hachimaki(p, c, tinted, _, width=0.032, z=0.012, r=0.121):
    """A flat hachimaki: a thin cloth band tight round the head above the brow
    (the civ colour when tinted), a small flat knot at the back and two short
    tails hanging down the nape (not mdj_kit's rolled band)."""
    key, t = ('tint', True) if tinted else ('cloth_wrap', False)
    p.add(lathe([(z, r), (z + width, r - 0.004)], seg=32), key, T(0, -0.006, 0), smooth=True, tint=t)
    p.add(lathe([(z - 0.002, r + 0.0025), (z + width + 0.002, r - 0.0015)], seg=32), key, T(0, -0.006, 0), smooth=True,
          tint=t)
    p.add(box(0.045, 0.014, 0.03), key, T(0, -r - 0.003, z + width * 0.5), bevel=0.004, tint=t)       # the knot
    for sx in (-1, 1):
        p.add(box(0.028, 0.006, 0.1), key, T(sx * 0.016, -r - 0.007, z - 0.04) @ R('Y', sx * 12), bevel=0.002,
              tint=t)


@riders.hat('nmf_zukin')
def nmf_zukin(p, c, tinted, _, brow=0.0, mouth=-0.072):
    """The sohei's katsuo-zukin made to show the face at 1x: a round white hood
    over the crown down to the brow (`brow`, a rolled fold at its edge), the
    cloth down the sides and back of the head, and a close wrap over the mouth
    and chin from `mouth` down; the eyes and nose left open between them (the
    skin shows as a band), one broad end hanging behind."""
    key = 'zukin'
    P.DEFAULTS.setdefault('zukin', ('#e6e2d8', 0.85, 0.0, 'cloth'))
    p.add(lathe([(brow - 0.012, 0.128), (brow + 0.03, 0.126), (brow + 0.08, 0.107), (brow + 0.12, 0.062),
                 (brow + 0.14, 0.0)], seg=32), key, T(0, -0.008, 0), smooth=True)
    p.add(lathe([(brow - 0.016, 0.132), (brow + 0.004, 0.133)], seg=32), key, T(0, -0.006, 0), smooth=True)
    # down the sides and the back of the head, behind the cheekbones
    p.add(ellipsoid(0.124, 0.108, 0.118, seg=24), key, T(0, -0.045, -0.055), smooth=True)
    # over the mouth and chin, close round the jaw
    rz = 0.05
    p.add(ellipsoid(0.1, 0.098, rz, seg=24), key, T(0, 0.028, mouth - rz), smooth=True)
    p.add(ellipsoid(0.112, 0.115, 0.05, seg=24), key, T(0, -0.02, mouth - 0.06), smooth=True)   # round the neck
    p.add(box(0.09, 0.014, 0.17), key, T(0, -0.13, -0.15) @ R('X', -12), bevel=0.005)        # the end behind


@riders.hat('nmf_type90')
def nmf_type90(p, c, tinted, _, star=True, band=0.035, net=True, band_z=0.012):
    """The Type 90 helmet under its khaki cloth cover: a high rounded dome, the
    short flared rim, a cord net over the dome, the cover's band low on the dome
    (the civ colour when tinted, above the brow), the gilt star, the chin strap."""
    t = tinted
    p.add(lathe([(-0.025, 0.128), (0.0, 0.124), (0.06, 0.118), (0.12, 0.085), (0.155, 0.03), (0.162, 0.0)], seg=32),
          'nmf_khaki', smooth=True)
    p.add(lathe([(-0.03, 0.15), (-0.022, 0.13)], seg=32), 'nmf_khaki', smooth=True)
    z0 = band_z
    dome = [(-0.025, 0.128), (0.0, 0.124), (0.06, 0.118), (0.12, 0.085), (0.155, 0.03), (0.162, 0.0)]

    def r_at(z):
        for (za, ra), (zb, rb) in zip(dome, dome[1:]):
            if za <= z <= zb:
                return ra + (rb - ra) * (z - za) / (zb - za)
        return dome[0][1]
    p.add(lathe([(z, r_at(z) + 0.0035) for z in (z0, z0 + band * 0.5, z0 + band)], seg=32), 'tint' if t else 'leather_dark',
          smooth=True, tint=t)
    if net:
        for k in range(8):
            a = 2 * math.pi * k / 8 + math.pi / 8
            arc = []
            for i in range(7):
                e = i * math.pi / 2 / 6
                z = z0 + band + 0.002 + (0.16 - z0 - band) * math.sin(e)
                r = 0.127 * math.cos(e) + 0.003
                arc.append((r * math.cos(a), r * math.sin(a), z))
            p.add(tube(arc, 0.0035, seg=4), 'nmf_straw_dk', smooth=True)
    if star:
        p.add(lathe([(0.0, 0.018), (0.004, 0.018)], seg=5), 'gold', T(0, 0.128, 0.075) @ R('X', -80), smooth=False)
    for sx in (-1, 1):
        p.add(tube([(sx * 0.11, 0.0, -0.02), (sx * 0.08, 0.03, -0.12), (0.0, 0.06, -0.17)], 0.005, seg=4),
              'leather_dark', smooth=True)


for _name, _fn in (('nmf_matchlock', nmf_matchlock), ('nmf_furoshiki', nmf_furoshiki), ('nmf_pouch', nmf_pouch),
                   ('nmf_canteen', nmf_canteen), ('nmf_bolt_quiver', nmf_bolt_quiver)):
    P.BUILDERS.setdefault(_name, _fn)


for _name, _fn in (('nmf_katana', nmf_katana), ('nmf_naginata', nmf_naginata), ('nmf_ebira', nmf_ebira),
                   ('nmf_yumi', nmf_yumi), ('nmf_saihai', nmf_saihai), ('nmf_agemaki', nmf_agemaki), ('kuwa_broad', kuwa_broad), ('nmf_ija_pack', nmf_ija_pack), ('nmf_xbow', nmf_xbow), ('nmf_arisaka', nmf_arisaka)):
    P.BUILDERS.setdefault(_name, _fn)
