"""The League modes' riders (catalog/league_mounted.py): QC's Teutonic Knight
and Modern's Roughrider.

Both are the 'horse_rider' archetype (models/mounted.py) dressed by a kit of
their own, kept in this module so the shared files stay as they are. The
archetype 'league_horse_rider' takes the horse_rider's parameters plus

    'kit'   'teutonic' | 'roughrider': which outfit and tack (below)

Painted cloth. The white caparison, crinet and mantle of the Teutonic Knight
carry their crosses and hems in the mesh itself: each is a fine grid whose
quads take their material from where they lie on the cloth (paint functions),
so a cross lies on the folds of the cloth instead of floating over it.
"""
import math

import numpy as np

import mesh as M
from models import archetype
from models import humanoid as H
from models import mounted as MT
from models import props as P
from models import props_mounted as PM

mat = P.mat
T = M.translate
V3 = MT.V3


# ------------------------------------------------------------ painted grids

def grid_mesh(Pts, matfn, closed_u=False, smooth=True):
    """A surface from a (nv, nu, 3) grid of points; matfn(i, j) gives the
    material of quad (i, j) (rows i..i+1, columns j..j+1)."""
    nv, nu = Pts.shape[:2]
    cols = nu if closed_u else nu - 1
    faces, fm = [], []
    for i in range(nv - 1):
        for j in range(cols):
            a, b = i * nu + j, i * nu + (j + 1) % nu
            c, d = (i + 1) * nu + j, (i + 1) * nu + (j + 1) % nu
            faces += [(a, c, d), (a, d, b)]
            m = matfn(i, j)
            fm += [m, m]
    mesh = M.from_indexed(Pts.reshape(-1, 3), np.asarray(faces, np.int64), 0, smooth=smooth)
    mesh.M = np.asarray(fm, np.int32)
    return mesh


def in_cross(a, b, arm, w, flare=0.0, arm_b=None):
    """Whether (a, b), relative to a cross's centre, lies on it: arms of half
    length `arm` (`arm_b` along b, if given) and half width `w`, widening by
    `flare` per meter out from the centre."""
    arm_b = arm if arm_b is None else arm_b
    aa, bb = abs(a), abs(b)
    return (aa <= arm and bb <= w + flare * aa) or (bb <= arm_b and aa <= w + flare * bb)


def _arclen(Pts):
    """Cumulative arc length along each row of a (nv, nu, 3) grid."""
    seg = np.linalg.norm(np.diff(Pts, axis=1), axis=-1)
    return np.concatenate([np.zeros((Pts.shape[0], 1), np.float32), np.cumsum(seg, axis=1)], axis=1)


# ------------------------------------------------------------ the horse's cloth (body space)

def caparison(y0, y1, drop, paint, g=0.036, ny=64, na=26, nd=18, flare=0.11, hem=None, fold_amp=0.02,
              fold_len=0.21):
    """A cloth over the barrel from y0 (rump) to y1 (chest), hanging `drop`
    down the flanks (times hem(u, y), u = 0..1 along y), with soft vertical
    folds growing toward the hem. paint(y, s, to_hem) gives a quad's
    material from its place on the cloth: s the distance along the cloth from
    the spine (+ right flank, - left), to_hem the distance left to the hem."""
    ys = np.linspace(y0, y1, ny + 1)
    angs = np.linspace(0.0, math.pi, na + 1)
    rows = []
    for j, y in enumerate(ys):
        rx, rz, zc = MT.barrel_at(y)
        rx, rz = rx + g, rz + g
        u = j / ny
        dl = drop * (hem(u, y) if hem else 1.0)
        pts = []
        for k in range(nd, 0, -1):
            f = k / nd
            fold = fold_amp * f * math.sin(2 * math.pi * y / fold_len)
            pts.append((rx * (1 + flare * f) + fold, y, zc - dl * f))
        for a in angs:
            pts.append((rx * math.cos(a), y, zc + rz * math.sin(a)))
        for k in range(1, nd + 1):
            f = k / nd
            fold = fold_amp * f * math.sin(2 * math.pi * y / fold_len + 1.7)
            pts.append((-(rx * (1 + flare * f) + fold), y, zc - dl * f))
        rows.append(pts)
    Pts = np.asarray(rows, np.float32)
    cum = _arclen(Pts)
    mid = nd + na // 2
    S = cum[:, mid:mid + 1] - cum             # + toward the right flank (low indices)
    Lr, Ll = cum[:, mid], cum[:, -1] - cum[:, mid]

    def matfn(i, j):
        y = 0.5 * (ys[i] + ys[i + 1])
        s = 0.25 * (S[i, j] + S[i, j + 1] + S[i + 1, j] + S[i + 1, j + 1])
        L = 0.5 * ((Lr[i] + Lr[i + 1]) if s >= 0 else (Ll[i] + Ll[i + 1]))
        return paint(y, s, L - abs(s))
    return grid_mesh(Pts, matfn)


def neck_cloth(rings, paint, ny=22, na=28):
    """A tube of cloth round the horse's neck (neck space, along +y), from
    tube_y-style rings (y, rx, rz, zc). paint(y, a) gives a quad's material,
    a the angle round the neck (0 = right side, +90 = the crest)."""
    ry = [r[0] for r in rings]
    ys = np.linspace(ry[0], ry[-1], ny + 1)
    angs = np.linspace(-math.pi / 2, 1.5 * math.pi, na + 1)
    rows = []
    for y in ys:
        rx = float(np.interp(y, ry, [r[1] for r in rings]))
        rz = float(np.interp(y, ry, [r[2] for r in rings]))
        zc = float(np.interp(y, ry, [r[3] for r in rings]))
        rows.append([(rx * math.cos(a), y, zc + rz * math.sin(a)) for a in angs])
    Pts = np.asarray(rows, np.float32)

    def matfn(i, j):
        return paint(0.5 * (ys[i] + ys[i + 1]), 0.5 * (angs[j] + angs[j + 1]))
    return grid_mesh(Pts, matfn)


# ------------------------------------------------------------ the Teutonic Knight's gear

def topfhelm(mats, m='helmet', crest='crest', brass='brass'):
    """The great helm of the 14th century (Topfhelm): a flat-topped pot with
    eye slits either side of a brass cross on its face, breaths below on the
    right, rivets round the top plate, and the crest: two civ-colored wings
    standing on the top, splayed in a V."""
    hm = mat(mats, m, '#9a9aa0', spec=0.8, gloss=26, metal=True)
    dark = mat(mats, 'slit', '#121212', spec=0.0)
    bm = mat(mats, brass, '#b8964e', spec=0.8, gloss=22, metal=True)
    cm = mat(mats, crest, '#ffffff', tint=True, spec=0.05)
    parts = [M.loft([(-0.03, 0.128, 0.136), (0.06, 0.13, 0.142), (0.17, 0.126, 0.138), (0.25, 0.12, 0.13),
                     (0.275, 0.112, 0.12)], hm, seg=20, cap_bottom=True, cap_top=False).transformed(T(0, 0.008, 0))]
    # the top plate, a little domed, and the rivets round its edge
    parts.append(M.ellipsoid(0.112, 0.12, 0.022, hm, (0, 0.008, 0.274), seg=18))
    for i in range(10):
        a = 2 * math.pi * i / 10
        parts.append(M.sphere(0.009, bm, (0.115 * math.cos(a), 0.008 + 0.124 * math.sin(a), 0.262), seg=5))
    # the riveted band where the top plate meets the sides
    parts.append(M.loft([(0.245, 0.124, 0.134), (0.262, 0.122, 0.132)], hm, seg=20, cap_bottom=False,
                        cap_top=False).transformed(T(0, 0.008, 0)))
    # eye slits either side of the cross's upright
    for sx in (1, -1):
        parts.append(M.box(0.062, 0.03, 0.016, dark, (sx * 0.05, 0.142, 0.155)).transformed(M.rot_z(-sx * 14)))
    # the brass cross: upright down the face, the bar along the brow above the slits
    parts.append(M.box(0.022, 0.022, 0.25, bm, (0, 0.148, 0.10)))
    parts.append(M.loft([(0.175, 0.132, 0.143), (0.195, 0.132, 0.143)], bm, seg=20, cap_bottom=False,
                        cap_top=False).transformed(T(0, 0.010, 0)))
    # breaths: rows of holes on the right cheek
    for r in range(3):
        for c in range(3):
            x, z = 0.035 + 0.022 * c, 0.03 + 0.03 * r
            a = math.asin(min(0.99, x / 0.13))
            parts.append(M.box(0.008, 0.02, 0.014, dark, (x, 0.008 + 0.142 * math.cos(a), z)))
    # the wings: feathers stepped down the trailing edge, standing in the front-back plane
    # each wing: a root low on the helm, the leading edge curving up and
    # out, five primaries stepped down its trailing edge
    wing = [(0.06, 0.0), (0.11, 0.05), (0.12, 0.12), (0.09, 0.19), (0.03, 0.25), (-0.05, 0.27), (-0.07, 0.235),
            (-0.12, 0.23), (-0.13, 0.19), (-0.17, 0.17), (-0.16, 0.13), (-0.20, 0.10), (-0.17, 0.07), (-0.19, 0.04),
            (-0.09, 0.0)]
    for sx in (1, -1):
        w = M.extrude(wing, 0.024, cm).transformed(M.rot_z(90))     # outline now in the y-z plane, thin in x
        parts.append(w.transformed(T(sx * 0.016, 0.0, 0.275) @ M.rot_y(sx * 3)))
    # the wings' socket on the top plate
    parts.append(M.rounded_box(0.11, 0.07, 0.03, bm, (0, -0.01, 0.285), r=0.01, seg=6))
    return M.Mesh.concat(parts)


PM.HEADGEAR.setdefault('topfhelm', topfhelm)


def heater_shield(mats, w=0.50, h=0.64, face='shield_face', cross='shield_cross', curve=0.07):
    """A heater shield facing -y, centred at the origin, curved round the
    arm: a flat top, the sides sweeping to a point; the black cross painted
    from edge to edge, a steel rim, the guige strap over the top."""
    fm = mat(mats, face, '#e6e2d8', spec=0.05)
    xm = mat(mats, cross, '#1c1c1e', spec=0.05)
    rm = mat(mats, 'shield_rim', '#6a6a70', spec=0.6, gloss=22, metal=True)
    lm = mat(mats, 'guige', '#3a2a1c', spec=0.05)
    top, bot = h * 0.42, -h * 0.58

    def half_w(z):
        u = (top - z) / (top - bot)
        return w / 2 * max(0.0, math.cos(min(1.0, u) * math.pi / 2)) ** 0.75 if u > 0.25 else w / 2

    nz, nx = 20, 16
    rows = []
    for i in range(nz + 1):
        z = top - (top - bot) * i / nz
        hw = max(half_w(z), 0.004)
        rows.append([(x, -curve * (1 - (x / (w / 2)) ** 2) - 0.012, z) for x in np.linspace(-hw, hw, nx + 1)])
    Pts = np.asarray(rows, np.float32)
    zc = 0.08

    def matfn(i, j):
        z = 0.5 * (Pts[i, j, 2] + Pts[i + 1, j + 1, 2])
        x = 0.5 * (Pts[i, j, 0] + Pts[i + 1, j + 1, 0])
        return xm if in_cross(x, z - zc, 1.0, 0.045) else fm
    parts = [grid_mesh(Pts, matfn)]
    back = Pts.copy()
    back[..., 1] += 0.02
    parts.append(grid_mesh(back[:, ::-1], lambda i, j: rm))
    # the rim: capsules round the edge
    edge = [tuple(Pts[i, 0]) for i in range(nz + 1)][::-1] + [tuple(Pts[0, j]) for j in range(nx + 1)] + \
           [tuple(Pts[i, -1]) for i in range(nz + 1)]
    for a, b in zip(edge[:-1], edge[1:]):
        if np.linalg.norm(np.subtract(a, b)) > 1e-4:
            parts.append(M.capsule(a, b, 0.012, 0.012, rm, seg=5, rings=1))
    # the guige over the top edge
    parts.append(M.capsule((-0.16, 0.0, top), (0.0, 0.03, top + 0.16), 0.014, 0.014, lm, seg=5, rings=1))
    parts.append(M.capsule((0.16, 0.0, top), (0.0, 0.03, top + 0.16), 0.014, 0.014, lm, seg=5, rings=1))
    return M.Mesh.concat(parts)


def order_lance(mats, length=2.3, grip=0.75):
    """The knight's lance: a long ash shaft swelling at the grip, a leather
    grip, a steel vamplate, a leaf head, and a swallow-tailed civ-colored
    pennon hanging from below the head."""
    wm = mat(mats, 'wood', '#7a5a3a', spec=0.08)
    s = mat(mats, 'steel', '#b8bcc2', spec=0.9, gloss=30, metal=True)
    lm = mat(mats, 'grip', '#3a2a1c', spec=0.05)
    pm = mat(mats, 'pennant', '#ffffff', tint=True, spec=0.04)
    z0, z1 = -grip, length - grip
    parts = [M.lathe([(z0, 0.0005), (z0, 0.022), (-0.25, 0.028), (0.10, 0.03), (0.35, 0.024), (z1 - 0.24, 0.016),
                      (z1 - 0.2, 0.0005)], wm, seg=8)]
    parts.append(M.cylinder(0.031, 0.031, 0.22, lm, seg=8, z0=-0.14))
    parts.append(M.lathe([(0.10, 0.03), (0.13, 0.09), (0.22, 0.10), (0.25, 0.032)], s, seg=12))
    head = M.lathe([(0.0, 0.017), (0.05, 0.02), (0.10, 0.034), (0.20, 0.026), (0.30, 0.0005)], s, seg=8)
    parts.append(head.transformed(T(0, 0, z1 - 0.26) @ M.scale(1.0, 0.45, 1.0)))
    # the pennon: two tails, flying back from the shaft (in the y-z plane, toward -y)
    pen = [(0.0, 0.0), (0.0, 0.24), (-0.44, 0.22), (-0.26, 0.12), (-0.46, 0.02)]
    pnn = M.extrude(pen, 0.01, pm).transformed(T(0, 0, z1 - 0.56) @ M.rot_z(90))
    parts.append(pnn)
    parts.append(pnn.transformed(M.rot_z(60)))       # the tails twist: it reads from every side
    return M.Mesh.concat(parts)


def scabbard_sword(mats):
    """A long sword in its scabbard, hanging from the belt (local: the
    scabbard's mouth at the origin, the blade down -z): a dark leather
    scabbard with a steel chape and locket, the hilt above (cross guard,
    leather grip, wheel pommel)."""
    lm = mat(mats, 'scabbard', '#2a2420', spec=0.1)
    s = mat(mats, 'steel', '#b8bcc2', spec=0.9, gloss=30, metal=True)
    gm = mat(mats, 'grip', '#3a2a1c', spec=0.05)
    parts = [M.rounded_box(0.034, 0.06, 0.82, lm, (0, 0, -0.41), r=0.012, seg=6)]
    parts.append(M.rounded_box(0.04, 0.066, 0.06, s, (0, 0, -0.80), r=0.012, seg=6))
    parts.append(M.rounded_box(0.04, 0.066, 0.05, s, (0, 0, -0.03), r=0.012, seg=6))
    parts.append(M.rounded_box(0.03, 0.20, 0.025, s, (0, 0, 0.02), r=0.01, seg=6))
    parts.append(M.cylinder(0.018, 0.016, 0.13, gm, seg=8, z0=0.03))
    parts.append(M.cylinder(0.035, 0.035, 0.022, s, seg=10, z0=0.165).transformed(
        T(0, 0, 0.176) @ M.rot_y(90) @ T(0, 0, -0.176)))
    return M.Mesh.concat(parts)


# ------------------------------------------------------------ the Roughrider's gear

def campaign_hat(mats, m='hat', band='hatband'):
    """The 1898 drab slouch hat: a tall soft crown with a crease down its
    middle and a pinch either side at the front, a wide brim dipping a
    little at the front and back, a dark band and its bow."""
    hm = mat(mats, m, '#9a8462', spec=0.04)
    bm = mat(mats, band, '#2a2218', spec=0.04)
    dk = mat(mats, 'hat_crease', '#6e5c42', spec=0.03)
    angs = np.linspace(0, 2 * math.pi, 24, endpoint=False)

    def crown(z, r, pinch):
        # pinched toward the front (+y) on both sides
        return [(r * math.cos(a) * (1 - pinch * max(0.0, math.sin(a)) ** 3 * abs(math.cos(a)) * 2.2),
                 r * 1.08 * math.sin(a) - 0.006, z) for a in angs]
    rows = [crown(0.11, 0.132, 0.0), crown(0.19, 0.126, 0.10), crown(0.25, 0.113, 0.22), crown(0.28, 0.088, 0.28),
            crown(0.285, 0.03, 0.0)]
    Pc = np.asarray(rows, np.float32)
    # the crease: a fore-and-aft valley pressed down the middle of the crown
    Pc[2:, :, 2] -= 0.035 * np.exp(-(Pc[2:, :, 0] / 0.05) ** 2) * np.array([0.5, 1.0, 1.0], np.float32)[:, None]
    parts = [grid_mesh(Pc, lambda i, j: hm, closed_u=True)]
    # band and bow
    parts.append(M.cylinder(0.135, 0.135, 0.032, bm, seg=20, z0=0.11).transformed(M.scale(1, 1.08, 1)))
    parts.append(M.ellipsoid(0.012, 0.03, 0.02, bm, (0.135, 0.0, 0.127), seg=6))
    # the brim: a soft medium brim, the left side pinned up against the crown
    rows = []
    for rr, k in ((0.13, 0.0), (0.185, 0.5), (0.235, 1.0)):
        rows.append([(rr * math.cos(a), rr * 1.06 * math.sin(a) - 0.006,
                      0.112 + k * (0.012 * math.cos(a) ** 2 - 0.02 * math.sin(a) ** 2
                                   + 0.14 * max(0.0, -math.cos(a)) ** 4)) for a in angs])
    Pb = np.asarray(rows, np.float32)
    # the pinned side folds in toward the crown
    left = Pb[..., 0] < 0
    Pb[..., 0] = np.where(left, Pb[..., 0] * (1 - 0.32 * np.clip((Pb[..., 2] - 0.112) / 0.14, 0, 1)), Pb[..., 0])
    parts.append(grid_mesh(Pb, lambda i, j: hm, closed_u=True))
    Pb2 = Pb.copy()
    Pb2[..., 2] -= 0.014
    parts.append(grid_mesh(Pb2[::-1], lambda i, j: hm, closed_u=True))
    # a brass pin holding the brim up
    parts.append(M.sphere(0.014, mat(mats, 'hat_pin', '#c0a058', spec=0.8, gloss=22, metal=True),
                          (-0.17, 0.0, 0.20), seg=6))
    # worn pushed back on the head
    return M.Mesh.concat(parts).transformed(T(0, 0, 0.11) @ M.rot_x(9) @ T(0, 0, -0.11))


PM.HEADGEAR.setdefault('campaign_hat', campaign_hat)


def krag_carbine(mats, length=1.16, grip=0.30, k=1.35):
    """The M1896 Krag-Jorgensen carbine, held at the wrist of the stock: the
    butt below the grip, the receiver with the Krag's box magazine bulging on
    its right, the bolt handle, the half-length fore-end with its band, the
    barrel, the saddle ring on the left."""
    w = mat(mats, 'stock', '#6a4426', spec=0.1)
    s = mat(mats, 'gunmetal', '#3e4044', spec=0.7, gloss=26, metal=True)
    parts = []
    # butt (wide, with the steel butt plate) and wrist
    parts.append(M.rounded_box(0.045, 0.12, 0.30, w, (0, 0.035, -0.19), r=0.018, seg=8, p=3))
    parts.append(M.box(0.047, 0.125, 0.012, s, (0, 0.035, -0.345)))
    parts.append(M.rounded_box(0.038, 0.05, 0.10, w, (0, 0.005, -0.02), r=0.014, seg=8, p=3))
    # the receiver and the magazine box on its right side, the bolt handle
    parts.append(M.rounded_box(0.042, 0.05, 0.16, s, (0, -0.005, 0.10), r=0.01, seg=6))
    parts.append(M.rounded_box(0.035, 0.06, 0.10, s, (0.035, 0.005, 0.09), r=0.012, seg=6))
    parts.append(M.capsule((0.0, -0.02, 0.16), (0.06, -0.03, 0.15), 0.01, 0.014, s, seg=5, rings=1))
    parts.append(M.capsule((0.0, 0.03, 0.02), (0.0, 0.055, 0.06), 0.008, 0.008, s, seg=4, rings=1))   # trigger guard
    # fore-end and its band, the barrel beyond
    fe = length - grip - 0.36
    parts.append(M.rounded_box(0.04, 0.046, fe, w, (0, 0.0, 0.18 + fe / 2), r=0.014, seg=8, p=3))
    parts.append(M.cylinder(0.026, 0.026, 0.025, s, seg=8, z0=0.18 + fe - 0.04))
    parts.append(M.cylinder(0.013, 0.011, length - grip - 0.16, s, seg=8, z0=0.16).transformed(T(0, -0.012, 0)))
    parts.append(M.box(0.01, 0.012, 0.025, s, (0, -0.024, length - grip - 0.02)))     # front sight
    # the saddle ring on the left of the wrist
    parts.append(M.lathe([(0.0, 0.03), (0.008, 0.034), (0.016, 0.03)], s, seg=10).transformed(
        T(-0.03, 0.0, 0.02) @ M.rot_y(90)))
    return M.Mesh.concat(parts).transformed(M.scale(k, k, 1.0))


# ------------------------------------------------------------ the archetype

@archetype('league_horse_rider')
class LeagueHorseRider(MT.HorseRider):
    def __init__(self, unit, params):
        super().__init__(unit, params)
        self.kit = params.get('kit')
        self.base['cap.slump'] = np.float32(0)
        if self.kit == 'teutonic':
            self._teutonic()
        elif self.kit == 'roughrider':
            self._roughrider()

    # -- animations of our own
    def anim_attack(self, style, ctx):
        if not (style == 'thrust' and self.kit == 'teutonic'):
            return super().anim_attack(style, ctx)
        B = self.base
        st = MT._ev(ctx, 'reach_max', 0.5, 0.3, 0.8)
        # the lance comes down from the rest to the couch under the arm as
        # the horse gathers, then horse and rider drive forward together
        wind = {'hand_r': V3(0.30, -0.16, 1.22), 'w_main': V3(-66, 0, -6), 'chest': V3(8, 0, 14), 'spine': V3(4, 0, 6),
                'elbow_r': V3(0.9, -0.3, 0.1), 'h.pos': V3(0, -0.12, 0), 'h.rot': V3(5, 0, 0), 'h.neck': V3(10, 0, 0),
                'h.head': V3(-6, 0, 0), 'h.hl': V3(14, 0, 0, 0, 0), 'h.hr': V3(14, 0, 0, 0, 0)}
        hit = {'hand_r': V3(0.20, 0.34, 1.14), 'w_main': V3(-82, 0, 24), 'chest': V3(-20, 0, -8), 'spine': V3(-14, 0, -4),
               'root_pos': V3(0, 0.10, -0.02), 'head': V3(16, 0, 0), 'elbow_r': V3(0.8, -0.6, -0.2),
               'h.pos': V3(0, 0.20, 0.03), 'h.rot': V3(-5, 0, 0), 'h.neck': V3(-16, 0, 0), 'h.head': V3(12, 0, 0),
               'h.fl': V3(44, -70, -24, 0, 0), 'h.fr': V3(16, -14, 0, 0, 0), 'h.hl': V3(-24, 4, 0, 0, 0),
               'h.hr': V3(-30, 12, 0, 0, 0), 'h.tail': V3(-20, 0, 0)}
        chans = set(wind) | set(hit)
        k = [(0.0, {}), (0.30, wind), (0.50, hit), (0.62, hit),
             (0.84, {'hand_r': B['hand_r'], 'w_main': B['w_main'] + V3(-12, 0, 0), 'h.pos': V3(0, 0.12, 0),
                     'chest': V3(-4, 0, 0)}),
             (1.0, {c: B[c] for c in chans})]
        anim = MT._k(dict(B, draw=np.float32(0)), k)
        return MT.Act(lambda t: anim(MT.warp(t, [(0.5, st)])))

    def anim_death(self, ctx):
        """The horse rears, twists and falls on its side; the rider is
        thrown clear behind it, turning in the air, and lands on his back
        well away from the horse, one knee up, the arms loose at his sides
        (the stock riders end spread-eagled on the horse)."""
        B = dict(self.base)
        down = MT._ev(ctx, 'down', 0.5, 0.3, 0.8)
        fk_seat = {'thigh_r': V3(78, -22, 0), 'shin_r': V3(-82, 0, 0), 'thigh_l': V3(78, 22, 0), 'shin_l': V3(-82, 0, 0),
                   'arm_r': V3(30, -20, 0), 'forearm_r': V3(50, 0, 0), 'arm_l': V3(30, 20, 0), 'forearm_l': V3(50, 0, 0)}
        B.update(fk_seat)
        B['h.pivot'] = V3(-0.25, -0.6, 0)
        rear = dict(MT.REAR_LEGS, **{'h.rot': V3(22, 0, 0), 'h.neck': V3(12, 0, -6), 'h.head': V3(-15, 0, 0),
                                     'h.tail': V3(20, 0, 0), 'h.ground': 1.0})
        lie = {'h.rot': V3(0, -90, -55), 'h.pos': V3(0.55, -0.85, 0.0), 'h.fl': V3(35, -35, -8, 0, 0),
               'h.fr': V3(22, -45, -8, 0, 0), 'h.hl': V3(8, 18, 8, 0, 0), 'h.hr': V3(-14, 22, 12, 0, 0),
               'h.neck': V3(-26, 0, 16), 'h.head': V3(14, 0, 0), 'h.tail': V3(-20, 0, 0), 'cap.slump': 1.0}
        rest_body = {'r.pos': V3(-1.0, 1.55, 0.14), 'r.rot': V3(90, 0, -18),
                     'arm_r': V3(12, -22, 0), 'forearm_r': V3(30, 0, 0), 'arm_l': V3(20, 26, 0), 'forearm_l': V3(55, 0, 0),
                     'thigh_r': V3(8, -10, 0), 'shin_r': V3(-6, 0, 0), 'thigh_l': V3(52, 12, 0), 'shin_l': V3(-96, 0, 0),
                     'chest': V3(-4, 0, 0), 'head': V3(-12, 0, 18), 'spine': V3(0, 0, 0)}
        k = [
            (0.0, {}),
            (0.22, dict(rear, **{'r.free': 0.0, 'chest': V3(14, 0, 10), 'head': V3(24, 0, 0),
                                 'arm_r': V3(70, -40, 0), 'arm_l': V3(60, 40, 0), 'forearm_r': V3(40, 0, 0)})),
            (0.36, dict(rear, **{'h.rot': V3(20, -25, 10), 'r.free': 0.75, 'r.pos': V3(-0.55, -0.20, 2.10),
                                 'r.rot': V3(50, -10, 14), 'arm_r': V3(110, -40, 0), 'arm_l': V3(100, 40, 0),
                                 'thigh_r': V3(40, -20, 0), 'shin_r': V3(-50, 0, 0), 'thigh_l': V3(30, 20, 0),
                                 'shin_l': V3(-40, 0, 0), 'cap.slump': 0.2})),
            (0.50, {'h.rot': V3(8, -50, -20), 'h.pos': V3(0.3, -0.4, 0.0), 'h.ground': 0.0, 'r.free': 1.0,
                    'r.pos': V3(-0.85, 0.85, 1.25), 'r.rot': V3(76, -6, -10), 'arm_r': V3(80, -50, 0),
                    'arm_l': V3(70, 50, 0), 'thigh_r': V3(30, -14, 0), 'shin_r': V3(-30, 0, 0),
                    'thigh_l': V3(40, 14, 0), 'shin_l': V3(-60, 0, 0), 'cap.slump': 0.5}),
            (0.64, dict(lie, **{'h.rot': V3(4, -80, -48), 'h.pos': V3(0.5, -0.75, 0.0), 'h.ground': 0.0, 'r.free': 1.0,
                                'r.pos': V3(-0.98, 1.45, 0.30), 'r.rot': V3(92, -4, -16), 'arm_r': V3(30, -40, 0),
                                'forearm_r': V3(20, 0, 0), 'arm_l': V3(40, 40, 0), 'forearm_l': V3(30, 0, 0),
                                'thigh_r': V3(20, -12, 0), 'shin_r': V3(-20, 0, 0), 'thigh_l': V3(60, 14, 0),
                                'shin_l': V3(-80, 0, 0), 'cap.slump': 0.85})),
            (0.80, dict(lie, **rest_body)),
            (1.0, dict(lie, **rest_body)),
        ]
        anim = MT._k(B, k)
        # where the weapon lands: clear of horse and rider (the knight's long
        # lance further out, lying fore-and-aft)
        land, land_yaw = (V3(-1.95, 0.25, 0.04), 0) if self.kit == 'teutonic' else (V3(-1.3, -0.4, 0.04), 20)
        wk = MT._k({c: B[c] for c in ('w.drop', 'w.pos', 'w.rot')},
                   [(0.0, {}), (0.30, {'w.drop': 0.0, 'w.pos': V3(0.3, -0.2, 2.3), 'w.rot': V3(-30, 0, 0)}),
                    (0.31, {'w.drop': 1.0, 'w.pos': V3(0.28, -0.3, 2.3), 'w.rot': V3(-20, 0, 5)}),
                    (0.46, {'w.pos': V3(-0.6, -0.4, 1.4), 'w.rot': V3(40, 0, 30)}),
                    (0.60, {'w.pos': land, 'w.rot': V3(88, 0, land_yaw)}),
                    (1.0, {'w.pos': land, 'w.rot': V3(88, 0, land_yaw)})])

        def fn(t):
            u = MT.warp(t, [(0.64, down)])
            q = anim(u)
            q.update(wk(u))
            q['w.drop'] = 1.0 if u > 0.305 else 0.0
            return q
        return MT.Act(fn, legs='fk', arms='fk')

    def pose_mesh(self, pose, act, action):
        s = float(pose.get('cap.slump', 0.0))
        if self.kit != 'teutonic' or s < 0.05:
            return super().pose_mesh(pose, act, action)
        # the cloth slumps as the horse goes down: a shorter fall, no flare,
        # the folds bunching
        q = round(s * 6) / 6
        if q not in self._cap_cache:
            a = dict(self._cap_args)
            a['drop'] *= 1 - 0.55 * q
            a['flare'] *= 1 - q
            a['fold_amp'] *= 1 + 1.5 * q
            a['fold_len'] = 0.21 * (1 - 0.35 * q)
            self._cap_cache[q] = caparison(**a)
        keep = self.horse.back_parts[0]
        self.horse.back_parts[0] = self._cap_cache[q]
        try:
            return super().pose_mesh(pose, act, action)
        finally:
            self.horse.back_parts[0] = keep

    # -- helpers
    def _hm(self, name, color, **kw):
        """A rider material."""
        return mat(self.rider.h.mats, name, color, **kw)

    def _m(self, name, color, **kw):
        """A horse material."""
        return mat(self.mats, name, color, **kw)

    def _rider_point(self, p):
        """Body-space point of a rider-space point at rest (the rider sits on
        the seat: rider_transform without the horse's motion)."""
        p = np.asarray(p, np.float32)
        return MT.SEAT + self.rs * (p - np.array([0, 0, H.PELVIS_H - MT.PELVIS_SEAT], np.float32))

    def _chest_rings(self):
        rp = self.p.get('rider', {})
        tw = H.TORSO_W * H.build_params(rp).get('bulk', 1.0)
        rings = [(0.0, 0.165, 0.105, 0, 0.0), (0.10, 0.19, 0.12, 0, 0.012), (0.19, 0.205, 0.122, 0, 0.008),
                 (0.25, 0.185, 0.108, 0, -0.005), (0.30, 0.10, 0.07, 0, -0.01), (0.33, 0.05, 0.05, 0, -0.01)]
        return [(z, rx * tw, ry * (1 + (tw - 1) * 0.6), dx, dy) for z, rx, ry, dx, dy in rings], tw

    def _override_props(self, table):
        orig = self.rider.h.prop
        hm = self.rider.h.mats
        cache = {}
        ps = self.p.get('rider', {}).get('prop_scale', 1.0)

        def prop(name, **kw):
            if name in table:
                key = (name, tuple(sorted(kw.items())))
                if key not in cache:
                    fn, fkw = table[name]
                    cache[key] = fn(hm, **dict(fkw, **kw)).transformed(M.scale(ps))
                return cache[key]
            return orig(name, **kw)
        self.rider.h.prop = prop

    # -- the Teutonic Knight
    def _teutonic(self):
        p = self.p
        hp = p.get('horse', {})
        white = self._m('cloth', hp.get('cloth', '#c8c4ba'), spec=0.05)
        black = self._m('cloth_cross', '#1e1e20', spec=0.05)
        hem = self._m('cloth_hem', '#ffffff', tint=True, spec=0.05)
        edge = self._m('cloth_edge', '#1e1e20', spec=0.05)

        # The caparison, cut to the horse: crosses on each flank over the
        # shoulder and the quarter and one over the croup, a civ hem; slit
        # high over the forelegs and hind legs so the legs read, shorter at
        # the chest and the rump where it wraps round. In the death it
        # slumps (see pose_mesh).
        crosses = [(-0.50, 0.50), (0.27, 0.47)]

        def paint(y, s, to_hem):
            if to_hem < 0.15:
                return hem
            for yc, sc in crosses:
                if in_cross(y - yc, abs(s) - sc, 0.13, 0.032, flare=0.12):
                    return black
            if in_cross(y + 0.70, s, 0.12, 0.03, flare=0.12):
                return black
            return white

        def hem_len(u, y):
            k = 1.0
            for yc, w in ((0.47, 0.09), (-0.60, 0.10)):
                d = abs(y - yc) / w
                if d < 1.0:
                    k -= 0.42 * (1 - d * d)
            return k * (0.78 + 0.22 * math.sin(math.pi * u))
        self._cap_args = dict(y0=-0.96, y1=0.78, drop=0.46, paint=paint, hem=hem_len, g=0.022, flare=0.05,
                              fold_amp=0.018, nd=14)
        self._cap_cache = {}
        self.horse.back_parts.insert(0, caparison(**self._cap_args))
        self.base['cap.slump'] = np.float32(0)

        # The crinet in the civ colour over the neck (so the neck reads
        # against the white body), a black cross on each side, a white band
        # at its foot.
        rings = [(-0.17, 0.18, 0.325, -0.02), (0.0, 0.178, 0.295, 0.0), (0.22, 0.148, 0.235, 0.01),
                 (0.45, 0.123, 0.18, 0.02), (0.60, 0.112, 0.152, 0.02)]

        def npaint(y, a):
            if y < -0.10:
                return white
            for side in (0.0, math.pi):
                d = (a - side + math.pi) % (2 * math.pi) - math.pi
                if in_cross(y - 0.24, d * 0.17, 0.10, 0.026, flare=0.1):
                    return white
            return hem
        self.horse.neck_parts.append(neck_cloth(rings, npaint))

        # the war saddle: a high cantle wrapping round the rider's hips and a
        # tall pommel, both standing out of the cloth
        sm = self.horse.m_saddle
        rx, rz, zc = MT.barrel_at(-0.05)
        top = zc + rz + 0.05
        self.horse.back_parts.append(MT.drape(-0.30, 0.18, lambda j, i, e: sm, g=0.07, drop=0.12, arc=0.8, ny=5, na=8,
                                              flare=0.0))
        cant = [(top + 0.02, 0.20, 0.12, 0, -0.17), (top + 0.16, 0.21, 0.13, 0, -0.19), (top + 0.24, 0.20, 0.12, 0, -0.21)]
        self.horse.back_parts.append(H._shell(cant, sm, math.radians(200), math.radians(340), seg=12))
        self.horse.back_parts.append(H._shell([(r[0], r[1] + 0.012, r[2] + 0.012, r[3], r[4]) for r in cant], sm,
                                              math.radians(200), math.radians(340), seg=12))
        pom = [(top, 0.15, 0.06, 0, 0.17), (top + 0.13, 0.11, 0.05, 0, 0.20), (top + 0.18, 0.07, 0.04, 0, 0.21)]
        self.horse.back_parts.append(H._shell(pom, sm, math.radians(20), math.radians(160), seg=10))
        brass = self._m('saddle_brass', '#b8964e', spec=0.8, gloss=22, metal=True)
        self.horse.back_parts.append(M.capsule((-0.19, -0.20, top + 0.24), (0.19, -0.20, top + 0.24), 0.012, 0.012,
                                               brass, seg=6, rings=1))
        # stirrup leathers and iron stirrups under the rider's feet
        self._stirrups(self._m('stirrup', '#8a8c92', spec=0.8, gloss=24, metal=True), self.horse.m_tack)

        # the chanfron's spike
        st = self._m('barding_steel', hp.get('barding_steel', '#9a9ea6'), spec=0.85, gloss=28, metal=True)
        self.horse.head_parts.append(M.cone(0.022, 0.09, st, seg=6).transformed(T(0, 0.16, 0.12) @ M.rot_x(-30)))

        # -- the rider
        r = self.rider.h
        parts = r.parts
        cross_r = self._hm('cross', '#1e1e20', spec=0.05)
        mail_row = self._hm('mail_row', '#55575c', spec=0.3, gloss=12)
        steel = self._hm('plate', '#a8acb4', spec=0.85, gloss=28, metal=True)
        chest, tw = self._chest_rings()
        lb = H.LIMB * H.build_params(p.get('rider', {})).get('bulk', 1.0)

        # crosses on the surcoat, front and back, following the chest
        def ring_at(z, gap):
            zs = [c[0] for c in chest]
            return (z, float(np.interp(z, zs, [c[1] for c in chest])) + gap,
                    float(np.interp(z, zs, [c[2] for c in chest])) + gap)
        for c in (math.pi / 2, 1.5 * math.pi):
            up = [ring_at(z, 0.022) for z in np.linspace(0.0, 0.26, 6)]
            parts.append(('chest', H._shell(up, cross_r, c - 0.16, c + 0.16, seg=3), None))
            bar = [ring_at(z, 0.023) for z in (0.14, 0.205)]
            parts.append(('chest', H._shell(bar, cross_r, c - 0.62, c + 0.62, seg=8), None))

        # the white mantle: from a cord at the neck over the shoulders and
        # down the back, spreading over the cantle; a cross on the left
        # shoulder
        tk0 = [0.0, 0.10, 0.28, 0.55, 0.80, 1.0]
        tk = list(np.linspace(0.0, 1.0, 19))     # rows fine enough to carry the shoulder cross
        zk = list(np.interp(tk, tk0, [0.335, 0.30, 0.19, 0.02, -0.15, -0.30]))
        rxk = list(np.interp(tk, tk0, [0.12, 0.26, 0.295, 0.30, 0.305, 0.30]))
        ryk = list(np.interp(tk, tk0, [0.095, 0.16, 0.185, 0.19, 0.195, 0.20]))
        dyk = list(np.interp(tk, tk0, [-0.01, -0.02, -0.04, -0.07, -0.10, -0.13]))
        a0, a1 = math.radians(132), math.radians(408)
        angs = np.linspace(a0, a1, 73)
        back = 1.5 * math.pi
        rows = []
        for t, zt, rxt, ryt, dyt in zip(tk, zk, rxk, ryk, dyk):
            row = []
            for a in angs:
                d = min(abs(a - back) / (back - a0), 1.0)
                f = 1.0 - 0.38 * d ** 1.6           # the hem rises toward the front edges
                z = zk[0] - (zk[0] - zt) * f
                pl = 0.030 * math.sin(11 * (a - back)) * t ** 0.6   # pleats from the shoulders, deeper below
                row.append(((rxt + pl) * math.cos(a), dyt + (ryt + pl) * math.sin(a), z))
            rows.append(row)
        Pm = np.asarray(rows, np.float32)
        mdy = dyk
        mz = zk
        mant = self._hm('mantle', '#ccc8be', spec=0.04)

        def mpaint(i, j):
            a = 0.5 * (angs[j] + angs[j + 1])
            z = 0.5 * (Pm[i, j, 2] + Pm[i + 1, j + 1, 2])
            if in_cross((a - math.radians(238)) * 0.24, z - 0.20, 0.075, 0.022):
                return cross_r
            return mant
        parts.append(('chest', grid_mesh(Pm, mpaint), None))
        Pi = Pm.copy()
        dy = np.asarray(mdy, np.float32)[:, None]
        Pi[..., 0] *= 0.97
        Pi[..., 1] = (Pi[..., 1] - dy) * 0.97 + dy
        lining = self._hm('mantle_lining', '#ffffff', tint=True, spec=0.04)
        parts.append(('chest', grid_mesh(Pi[:, ::-1], lambda i, j: lining), None))
        # the front edges turned back, showing the lining
        for jj in (0, -1):
            parts.append(('chest', M.Mesh.concat([M.capsule(tuple(Pm[k, jj]), tuple(Pm[k + 1, jj]), 0.018, 0.018, lining,
                                                            seg=5, rings=1) for k in range(len(Pm) - 1)]), None))
        cord = self._hm('cord', '#2a2420')
        parts.append(('chest', M.capsule((-0.11, 0.08, 0.31), (0.11, 0.08, 0.31), 0.014, 0.014, cord, seg=6, rings=1),
                      None))
        parts.append(('chest', M.sphere(0.022, steel, (0.0, 0.10, 0.30), seg=8), None))

        # mail over the shoulder and elbow joints (no skin between the pieces)
        mail0 = r.mats['mail'] if 'mail' in r.mats else self._hm('mail', '#7a7c80')
        for side in 'rl':
            parts.append((f'arm_{side}', M.sphere(0.083 * lb, mail0, (0, 0, -0.01), seg=10), None))
            parts.append((f'forearm_{side}', M.sphere(0.066 * lb, mail0, (0, 0, 0.0), seg=10), None))
            parts.append((f'arm_{side}', M.capsule((0, 0, 0), (0, 0, -H.UPPER_ARM), 0.08 * lb, 0.068 * lb, mail0, seg=10,
                                                   rings=2), None))
        # mail coif showing under the helm
        mail = r.mats['mail'] if 'mail' in r.mats else self._hm('mail', '#7a7c80')
        parts.append(('neck', M.loft([(-0.02, 0.115, 0.105), (0.06, 0.098, 0.09), (0.11, 0.085, 0.08)], mail, seg=14,
                                     cap_bottom=False, cap_top=False), None))
        # rows of mail on the sleeves and legs
        for side in 'rl':
            for z in np.arange(-0.04, -H.UPPER_ARM, -0.055):
                rr = 0.078 * lb * (1 + 0.15 * z)
                parts.append((f'arm_{side}', M.cylinder(rr, rr, 0.012, mail_row, seg=10, z0=float(z)), None))
            for z in np.arange(-0.03, -H.FOREARM + 0.04, -0.05):
                rr = 0.063 * lb * (1 + 0.2 * z)
                parts.append((f'forearm_{side}', M.cylinder(rr, rr, 0.012, mail_row, seg=10, z0=float(z)), None))
            for z in np.arange(-0.10, -H.THIGH, -0.06):
                rr = (0.10 + (0.075 - 0.10) * (-z / H.THIGH)) * lb + 0.004
                parts.append((f'thigh_{side}', M.cylinder(rr, rr, 0.012, mail_row, seg=10, z0=float(z)), None))
            # knee cop and its wing
            # the poleyn: a cup over the front of the knee, a lame above and below, a fan-shaped wing outside
            sxs = 1 if side == 'r' else -1
            r0 = 0.072 * lb
            parts.append((f'shin_{side}', H._shell([(-0.06, r0 * 0.95, r0 * 0.95), (0.0, r0 * 1.08, r0 * 1.12),
                                                    (0.06, r0 * 0.95, r0 * 0.95)], steel, math.radians(10),
                                                   math.radians(170), seg=10), None))
            for z in (0.075, -0.075):
                parts.append((f'shin_{side}', H._shell([(z - 0.012, r0 * 0.97, r0 * 0.97), (z + 0.012, r0 * 0.97, r0 * 0.97)],
                                                       steel, math.radians(20), math.radians(160), seg=8), None))
            parts.append((f'shin_{side}', M.extrude([(0.0, -0.05), (0.045, -0.03), (0.055, 0.0), (0.045, 0.03),
                                                     (0.0, 0.05)], 0.008, steel).transformed(
                T(sxs * r0 * 0.95, 0.02, 0.0) @ M.rot_z(90 if sxs > 0 else -90) @ M.scale(1, 1, 1))
                          .transformed(M.rot_z(0)), None))
            # mail mitten over the hand
            parts.append((f'hand_{side}', M.ellipsoid(0.05 * H.HAND, 0.058 * H.HAND, 0.066 * H.HAND, mail,
                                                     (0, 0.008, -0.055), seg=9), None))
            # prick spur at the heel
            parts.append((f'foot_{side}', M.capsule((0, -0.06, -0.02), (0, -0.14, -0.035), 0.01, 0.006, steel,
                                                    seg=5, rings=1), None))
            parts.append((f'foot_{side}', M.cylinder(0.07, 0.07, 0.012, self._hm('spur_strap', '#2a2420'), seg=10,
                                                     z0=-0.03).transformed(M.scale(0.75, 1, 1)), None))
        # the sword at the left hip, lying back along the horse's flank
        parts.append(('pelvis', scabbard_sword(r.mats).transformed(
            T(-0.27, 0.02, 0.02) @ M.rot_y(-14) @ M.rot_x(-62)), None))
        # the sword belt, slung low
        parts.append(('pelvis', M.loft([(0.00, 0.20 * tw, 0.155 * tw), (0.035, 0.20 * tw, 0.155 * tw)],
                                       self._hm('sword_belt', '#2a2420'), seg=18, cap_bottom=False, cap_top=False), None))
        self._override_props({'lance': (order_lance, {}), 'shield_oval': (heater_shield, {})})

    def _stirrups(self, iron, leather):
        B = self.base
        for side, sx in (('r', 1), ('l', -1)):
            foot = self._rider_point(B[f'foot_{side}'])
            tread = foot + np.array([0.0, 0.04, -0.075], np.float32)
            hang = np.array([sx * 0.25, -0.05, 1.50], np.float32)
            self.horse.back_parts.append(M.capsule(hang, tread + np.array([0, 0, 0.16], np.float32), 0.016, 0.016,
                                                   leather, seg=5, rings=1))
            # the iron: a rounded triangle under the ball of the foot
            pts = [(-0.06, -0.012), (0.06, -0.012), (0.045, 0.09), (0.0, 0.16), (-0.045, 0.09)]
            pts = [tread + np.array([x, 0.0, z], np.float32) for x, z in pts]
            for a, b in zip(pts, pts[1:] + pts[:1]):
                self.horse.back_parts.append(M.capsule(a, b, 0.012, 0.012, iron, seg=5, rings=1))
            self.horse.back_parts.append(M.box(0.12, 0.06, 0.012, iron, tuple(tread + np.array([0, 0, -0.012]))))

    # -- the Roughrider
    def _roughrider(self):
        p = self.p
        hp = p.get('horse', {})
        hb = self.horse
        tack = hb.m_tack
        sm = hb.m_saddle
        brass = self._m('tack_brass', '#c0a058', spec=0.8, gloss=22, metal=True)
        canvas = self._m('canvas', hp.get('roll', '#9a9278'), spec=0.04)
        strap = self._m('strap', '#3a2a1c', spec=0.1)

        # the folded saddle blanket: the civ colour with an orange stripe near its edge
        blanket = self._m('blanket', '#ffffff', tint=True, spec=0.04)
        stripe = self._m('blanket_stripe', '#c8843a', spec=0.04)

        def bpaint(y, s, to_hem):
            edge_y = min(y - (-0.40), 0.20 - y)
            if 0.03 < to_hem < 0.065 or 0.03 < edge_y < 0.065:
                return stripe
            return blanket
        hb.back_parts.insert(0, caparison(-0.40, 0.20, 0.26, bpaint, g=0.022, ny=24, na=18, nd=8, flare=0.04,
                                          fold_amp=0.0))
        # the McClellan saddle: the seat with its open slot, a round pommel
        # and a high round cantle, the side bars, the cinch and its ring
        rx, rz, zc = MT.barrel_at(-0.05)
        top = zc + rz + 0.045
        hb.back_parts.append(MT.drape(-0.30, 0.16, lambda j, i, e: sm, g=0.05, drop=0.08, arc=0.75, ny=6, na=10,
                                      flare=0.0))
        hb.back_parts.append(M.box(0.035, 0.30, 0.012, self._m('slot', '#141210'), (0, -0.07, top + 0.012)))
        hb.back_parts.append(M.ellipsoid(0.085, 0.07, 0.075, sm, (0, 0.16, top + 0.06), seg=10))
        hb.back_parts.append(M.sphere(0.03, brass, (0, 0.19, top + 0.12), seg=8))
        cant = [(top + 0.0, 0.17, 0.08, 0, -0.27), (top + 0.08, 0.17, 0.08, 0, -0.29), (top + 0.13, 0.15, 0.07, 0, -0.30)]
        hb.back_parts.append(H._shell(cant, sm, math.radians(195), math.radians(345), seg=10))
        hb.back_parts.append(H._shell([(r[0], r[1] + 0.012, r[2] + 0.012, r[3], r[4]) for r in cant], sm,
                                      math.radians(195), math.radians(345), seg=10))
        for sx in (1, -1):
            hb.back_parts.append(M.rounded_box(0.03, 0.62, 0.10, sm, (sx * (rx + 0.06), -0.06, zc + rz * 0.55),
                                               r=0.015, seg=6).transformed(M.rot_y(0)))
        cx, cz_, cc = MT.barrel_at(0.10)
        hb.back_parts.append(MT.tube_y([(0.07, cx + 0.03, cz_ + 0.03, cc), (0.13, cx + 0.03, cz_ + 0.03, cc)], strap,
                                       seg=18, cap0=False, cap1=False))
        for sx in (1, -1):
            hb.back_parts.append(M.lathe([(0.0, 0.035), (0.012, 0.042), (0.024, 0.035)], brass, seg=10).transformed(
                T(sx * (cx + 0.05), 0.10, cc + cz_ * 0.35) @ M.rot_y(90)))
        # the blanket roll strapped across behind the cantle, over the bags,
        # its ends showing the rolled layers, three straps round it
        hb.back_parts.append(M.capsule((-0.36, -0.44, 1.62), (0.36, -0.44, 1.62), 0.075, 0.075, canvas, seg=12, rings=2))
        for sx in (1, -1):
            hb.back_parts.append(M.disc(0.05, 0.05, 0.004, self._m('roll_end', '#6e6852', spec=0.03), seg=12)
                                 .transformed(T(sx * 0.43, -0.44, 1.62) @ M.rot_z(90)))
        for x in (-0.22, 0.0, 0.22):
            hb.back_parts.append(M.cylinder(0.08, 0.08, 0.025, strap, seg=12, z0=-0.0125).transformed(
                T(x, -0.44, 1.62) @ M.rot_y(90)))
        # saddlebags behind the cantle: a pouch on each side, its flap and two buckled straps
        for sx in (1, -1):
            bx = sx * 0.27
            hb.back_parts.append(M.rounded_box(0.11, 0.24, 0.26, sm, (bx, -0.44, 1.36), r=0.04, seg=8))
            hb.back_parts.append(M.rounded_box(0.125, 0.25, 0.12, self._m('flap', '#2a221c', spec=0.12),
                                               (bx * 1.02, -0.44, 1.45), r=0.03, seg=8))
            for yy in (-0.51, -0.37):
                hb.back_parts.append(M.box(0.13, 0.025, 0.16, strap, (bx * 1.03, yy, 1.36)))
                hb.back_parts.append(M.box(0.135, 0.035, 0.025, brass, (bx * 1.04, yy, 1.30)))
        hb.back_parts.append(M.rounded_box(0.50, 0.08, 0.05, sm, (0, -0.44, 1.52), r=0.02, seg=6))
        # the canteen on the near side of the pommel: a round flask in its
        # wool cover, a cap, a strap to the saddle
        wool = self._m('wool', '#7e7462', spec=0.03)
        hb.back_parts.append(M.cylinder(0.13, 0.13, 0.07, wool, seg=16, z0=-0.035).transformed(
            T(-0.36, 0.10, 1.25) @ M.rot_y(90)))
        hb.back_parts.append(M.cylinder(0.022, 0.022, 0.05, self._m('cap', '#3a3a3c', spec=0.5, metal=True), seg=8,
                                        z0=0).transformed(T(-0.36, 0.10, 1.38)))
        hb.back_parts.append(M.capsule((-0.36, 0.10, 1.40), (-0.22, 0.14, 1.58), 0.012, 0.012, strap, seg=5, rings=1))
        # the coiled picket rope on the off side of the pommel
        rope = self._m('rope', '#b49c6c', spec=0.03)
        for k in range(3):
            hb.back_parts.append(M.lathe([(0.0, 0.10 + 0.012 * k), (0.012, 0.105 + 0.012 * k), (0.024, 0.10 + 0.012 * k)],
                                         rope, seg=14).transformed(T(0.35 + 0.012 * k, 0.16, 1.34) @ M.rot_y(90)))
        # hooded stirrups (wooden, a leather hood over the toe) on their leathers
        B = self.base
        wood = self._m('stirrup_wood', '#6a4a2e', spec=0.06)
        for side, sx in (('r', 1), ('l', -1)):
            foot = self._rider_point(B[f'foot_{side}'])
            tread = foot + np.array([0.0, 0.05, -0.08], np.float32)
            hb.back_parts.append(M.capsule(np.array([sx * 0.25, -0.04, 1.50], np.float32),
                                           tread + np.array([0, -0.02, 0.18], np.float32), 0.017, 0.017, strap,
                                           seg=5, rings=1))
            hb.back_parts.append(M.rounded_box(0.13, 0.09, 0.03, wood, tuple(tread), r=0.01, seg=6))
            for xx in (-0.06, 0.06):
                hb.back_parts.append(M.capsule(tread + np.array([xx, 0, 0]), tread + np.array([xx * 0.6, -0.02, 0.17]),
                                               0.013, 0.013, wood, seg=5, rings=1))
            # the hood: a leather cup closed in front of the toe, open behind
            hood = self._m('hood', '#4a3020', spec=0.1)
            c = tread + np.array([0, 0.04, 0.0], np.float32)
            hb.back_parts.append(H._shell([(0.0, 0.075, 0.10, 0, 0.0), (0.10, 0.07, 0.09, 0, -0.01),
                                           (0.15, 0.05, 0.06, 0, -0.03)], hood, math.radians(-10), math.radians(190),
                                          seg=10).transformed(T(*c)))
        # the breast filling out where the neck meets the chest (no step there)
        hb.body_parts.append(M.ellipsoid(0.16, 0.15, 0.24, hb.m_coat, (0, 0.58, 1.33), seg=14))
        # the US bit's brass bosses and a white blaze down the face
        for sx in (1, -1):
            hb.head_parts.append(M.lathe([(0.0, 0.03), (0.01, 0.03), (0.016, 0.0005)], brass, seg=10).transformed(
                T(sx * 0.072, 0.38, -0.07) @ M.rot_y(sx * 90)))
        hb.head_parts.append(M.ellipsoid(0.03, 0.22, 0.02, self._m('blaze', '#e8e2d6', spec=0.08), (0, 0.24, 0.07),
                                         seg=10).transformed(M.rot_x(-4)))

        # -- the rider
        r = self.rider.h
        parts = r.parts
        chest, tw = self._chest_rings()
        lb = H.LIMB * H.build_params(p.get('rider', {})).get('bulk', 1.0)
        shirt = r.mats['top']
        button = self._hm('button', '#e0dccf', spec=0.2)
        khaki = self._hm('webbing', '#cdbd8e', spec=0.04)
        cart = self._hm('cartridge', '#c8a050', spec=0.8, gloss=22, metal=True)
        leather = self._hm('holster', '#5a3a22', spec=0.12)
        glove = self._hm('glove', '#8a6440', spec=0.08)
        legging = self._hm('legging', '#d2c69e', spec=0.04)
        lace = self._hm('lace', '#4a3a28', spec=0.04)
        steel = self._hm('spur', '#9a9ca2', spec=0.8, gloss=24, metal=True)

        def ring_at(z, gap):
            zs = [c[0] for c in chest]
            return (z, float(np.interp(z, zs, [c[1] for c in chest])) + gap,
                    float(np.interp(z, zs, [c[2] for c in chest])) + gap)
        # the shirt's placket and its buttons, the two pockets with flaps, the collar
        front = math.pi / 2
        parts.append(('chest', H._shell([ring_at(z, 0.018) for z in (0.12, 0.30)], shirt, front - 0.10, front + 0.10,
                                        seg=2), None))
        for z in (0.15, 0.20, 0.25):
            rz_ = ring_at(z, 0.022)
            parts.append(('chest', M.sphere(0.012, button, (0, rz_[2], z), seg=5), None))
        for sx in (1, -1):
            a = front - sx * 0.55
            parts.append(('chest', H._shell([ring_at(z, 0.022) for z in (0.12, 0.20)], shirt, a - 0.26, a + 0.26, seg=4),
                          None))
            parts.append(('chest', H._shell([ring_at(z, 0.028) for z in (0.185, 0.215)], shirt, a - 0.28, a + 0.28, seg=4),
                          None))
            rz_ = ring_at(0.19, 0.03)
            parts.append(('chest', M.sphere(0.01, button, (rz_[1] * math.cos(a), rz_[2] * math.sin(a), 0.19), seg=5),
                          None))
        parts.append(('chest', M.loft([(0.29, 0.13, 0.10, 0, -0.005), (0.33, 0.115, 0.095, 0, 0.0)], shirt, seg=16,
                                      cap_bottom=False, cap_top=False), None))
        # the neckerchief: knotted at the throat, its point down the chest, white dots
        kc = self._hm('neckerchief', '#9a2e22', spec=0.04)
        dot = self._hm('dot', '#e8e4da', spec=0.04)
        parts.append(('chest', M.loft([(0.27, 0.125, 0.10, 0, 0.0), (0.31, 0.11, 0.09, 0, 0.0), (0.335, 0.085, 0.075)], kc,
                                      seg=16, cap_bottom=False, cap_top=False), None))
        ry_k = ring_at(0.24, 0.03)[2]
        parts.append(('chest', M.extrude([(-0.11, 0.0), (0.11, 0.0), (0.0, -0.20)], 0.02, kc).transformed(
            T(0, ry_k + 0.005, 0.31) @ M.rot_x(-14)), None))
        parts.append(('chest', M.sphere(0.036, kc, (0, ry_k + 0.014, 0.30), seg=8), None))
        for dx, dz in ((-0.03, 0.27), (0.03, 0.26), (0.0, 0.22), (-0.035, 0.30), (0.04, 0.295)):
            parts.append(('chest', M.sphere(0.009, dot, (dx, ry_k + 0.02, dz), seg=4), None))
        # braces from the belt over the shoulders, crossed at the back
        for sx in (1, -1):
            pts = [(sx * 0.11, ring_at(0.0, 0.02)[2] * 0.95, 0.0), (sx * 0.12, ring_at(0.12, 0.022)[2], 0.12),
                   (sx * 0.13, ring_at(0.22, 0.022)[2] * 0.95, 0.24), (sx * 0.15, 0.02, 0.33),
                   (-sx * 0.03, -ring_at(0.22, 0.022)[2] * 0.95, 0.20), (-sx * 0.09, -ring_at(0.05, 0.02)[2], 0.0)]
            for a, b in zip(pts[:-1], pts[1:]):
                parts.append(('chest', M.capsule(a, b, 0.013, 0.013, self._hm('braces', '#4a3a2a', spec=0.06), seg=5,
                                                 rings=1), None))
        # the Mills cartridge belt: woven webbing, a row of cartridge tips all round, a brass buckle
        bz0, bz1 = 0.02, 0.14
        parts.append(('pelvis', M.loft([(bz0, 0.196 * tw, 0.150 * tw), (bz1, 0.19 * tw, 0.145 * tw)], khaki, seg=20,
                                       cap_bottom=False, cap_top=False), None))
        for k in range(26):
            a = 2 * math.pi * k / 26
            if abs(math.sin(a) - 1) < 0.08:
                continue
            x, y = 0.205 * tw * math.cos(a), 0.158 * tw * math.sin(a)
            parts.append(('pelvis', M.cylinder(0.014, 0.014, 0.06, cart, seg=5, z0=bz1 - 0.025).transformed(T(x * 1.02, y * 1.02, 0)),
                          None))
        parts.append(('pelvis', M.box(0.07, 0.02, 0.06, cart, (0, 0.152 * tw + 0.02, (bz0 + bz1) / 2)), None))
        # the haversack: a canvas strap from the right shoulder across the chest to a bag on the left hip
        hv = self._hm('haversack', '#b8aa82', spec=0.04)
        pts = [(0.17, 0.0, 0.33), (0.10, ring_at(0.25, 0.03)[2], 0.25), (-0.02, ring_at(0.15, 0.03)[2], 0.13),
               (-0.14, ring_at(0.03, 0.03)[2] * 0.9, 0.0)]
        for a, b in zip(pts[:-1], pts[1:]):
            parts.append(('chest', M.capsule(a, b, 0.022, 0.022, hv, seg=6, rings=1), None))
        for a, b in ((pts[0], (0.02, -ring_at(0.22, 0.03)[2], 0.22)), ((0.02, -ring_at(0.22, 0.03)[2], 0.22),
                                                                       (-0.15, -ring_at(0.03, 0.03)[2] * 0.9, 0.0))):
            parts.append(('chest', M.capsule(a, b, 0.022, 0.022, hv, seg=6, rings=1), None))
        parts.append(('pelvis', M.rounded_box(0.07, 0.22, 0.20, hv, (-0.25 * tw * 0.8 - 0.05, -0.03, -0.10), r=0.03,
                                              seg=6), None))
        parts.append(('pelvis', M.rounded_box(0.075, 0.22, 0.08, self._hm('haversack_flap', '#9c8e68', spec=0.04),
                                              (-0.25 * tw * 0.8 - 0.055, -0.03, -0.03), r=0.02, seg=6), None))
        # the holster on the right hip, the revolver's butt showing
        parts.append(('pelvis', M.rounded_box(0.06, 0.12, 0.24, leather, (0.25 * tw * 0.8 + 0.04, -0.02, -0.06),
                                              r=0.025, seg=6).transformed(M.rot_x(-12)), None))
        parts.append(('pelvis', M.rounded_box(0.035, 0.05, 0.10, self._hm('revolver', '#2a2624', spec=0.3),
                                              (0.25 * tw * 0.8 + 0.04, 0.03, 0.09), r=0.012, seg=6)
                      .transformed(M.rot_x(-30)), None))
        # gauntlets: brown gloves with flared cuffs
        for side in 'rl':
            parts.append((f'hand_{side}', M.ellipsoid(0.046 * H.HAND, 0.056 * H.HAND, 0.064 * H.HAND, glove,
                                                     (0, 0.008, -0.055), seg=9), None))
            parts.append((f'forearm_{side}', M.loft([(-H.FOREARM + 0.0, 0.05 * lb, 0.05 * lb),
                                                     (-H.FOREARM + 0.10, 0.068 * lb, 0.068 * lb)], glove, seg=10,
                                                    cap_bottom=False, cap_top=False), None))
            # canvas leggings laced down the outside, over brown shoes; spurs
            sx = 1 if side == 'r' else -1
            parts.append((f'shin_{side}', M.loft([(-H.SHIN + 0.02, 0.066 * lb, 0.068 * lb), (-0.20, 0.075 * lb, 0.078 * lb),
                                                  (-0.06, 0.08 * lb, 0.082 * lb)], legging, seg=12, cap_bottom=False,
                                                 cap_top=True), None))
            parts.append((f'shin_{side}', M.loft([(-0.10, 0.083 * lb, 0.085 * lb), (-0.04, 0.088 * lb, 0.09 * lb)],
                                                 legging, seg=12, cap_bottom=False, cap_top=False), None))
            parts.append((f'shin_{side}', M.box(0.014, 0.035, 0.30, lace, (sx * 0.078 * lb, 0.0, -0.21)), None))
            for z in np.linspace(-0.33, -0.09, 5):
                parts.append((f'shin_{side}', M.sphere(0.008, cart, (sx * 0.082 * lb, 0.012, float(z)), seg=4), None))
            parts.append((f'foot_{side}', M.capsule((0, -0.06, -0.02), (0, -0.13, -0.03), 0.01, 0.006, steel, seg=5,
                                                    rings=1), None))
            parts.append((f'foot_{side}', M.ellipsoid(0.03, 0.01, 0.03, steel, (0, -0.135, -0.03), seg=6), None))
        # a moustache
        parts.append(('head', M.ellipsoid(0.05, 0.02, 0.014, r.mats['hair'], (0, 0.118, 0.072), seg=8)
                      .transformed(M.scale(H.HEAD)), None))
        self._override_props({'rifle': (krag_carbine, {})})
