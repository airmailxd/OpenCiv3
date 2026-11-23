"""The League modes' foot units (catalog/league_foot.py): Modern's
Partisans, Special Forces and the mod's Japanese rifleman (its Infantry).

Archetype 'league_soldier': models/modern_mod_foot.py's 'mod_soldier' plus

    hat     'beret' (worn over the right ear, a small badge on the left) |
            'flat_cap' (a cloth cap with a short peak) |
            'ija' (a round steel helmet in a cloth cover, a small star on the
            front and a havelock of flaps hanging over the neck: material
            'havelock')
            as well as mod_soldier's
    armband material name of a band round the left upper arm (None: none)
    scarf   material name of a scarf round the neck, its knot in front
    gun     also a GUNS name of this module: 'ppsh' (a drum-fed submachine
            gun), 'arisaka' (a long bolt rifle with a long bayonet),
            'm4_sd' (a short carbine with a sight and a long suppressor)

Gear follows models/props.py: grip at the origin, long axis +z, sights -y.
"""
import mesh as M
from models import archetype
from models import humanoid as H
from models import props_foot_modern as G
from models import modern_mod_foot as MF
from models import modern_mod_foot_gear as MG
from models.props import mat, steel

GUNS = {
    'ppsh': {'muzzle': 0.50, 'support': (0.06, 0.22), 'butt': -0.34},
    'arisaka': {'muzzle': 1.04, 'support': (0.02, 0.36), 'butt': -0.36},
    'm4_sd': {'muzzle': 0.64, 'support': (0.03, 0.24), 'butt': -0.26},
}


# ------------------------------------------------------------ guns

def ppsh(mats, muzzle=0.50):
    """A wooden-stocked submachine gun: a perforated barrel jacket and the
    round drum magazine under it."""
    s = G.stock(mats)
    g = G.gunmetal(mats)
    parts = [G._stock_wood(mats, -0.34, 0.16, depth=0.08, wrist=0.03, m=s)]
    parts.append(M.box(0.04, 0.05, 0.20, g, (0, -0.01, 0.10)))                       # receiver
    parts.append(M.cylinder(0.026, 0.026, 0.26, g, seg=10, z0=0.20))                # jacket
    for z in (0.25, 0.32, 0.39):
        parts.append(M.cylinder(0.029, 0.029, 0.02, mat(mats, 'gun_dark', '#1e1e20'), seg=10, z0=z))
    parts.append(M.cylinder(0.011, 0.011, muzzle - 0.44, g, seg=8, z0=0.44))
    # the drum: a flat cylinder under the receiver, its face to the side
    parts.append(M.cylinder(0.075, 0.075, 0.05, g, seg=16, z0=-0.025).transformed(
        M.translate(0, 0.10, 0.12) @ M.rot_y(90)))
    return M.Mesh.concat(parts)


def arisaka(mats, muzzle=1.04):
    """A long bolt-action rifle, its wooden handguard nearly to the muzzle,
    and a long blade bayonet."""
    s = G.stock(mats)
    st = steel(mats)
    g = G.gunmetal(mats)
    parts = [G._stock_wood(mats, -0.36, 0.86, depth=0.075, m=s)]
    parts.append(G._barrel(mats, 0.0, muzzle, 0.013, g))
    for z in (0.46, 0.84):
        parts.append(M.cylinder(0.024, 0.024, 0.02, g, seg=8, z0=z).transformed(M.translate(0, 0.006, 0)))
    parts.append(M.capsule((0.02, -0.02, 0.02), (0.07, 0.0, 0.0), 0.009, 0.014, g, seg=5, rings=1))   # bolt
    parts.append(MG._bayonet(mats, muzzle, 0.40, 0.026))
    parts.append(M.capsule((0, 0.05, -0.30), (0, 0.06, 0.70), 0.007, 0.007, mat(mats, 'strap', '#4a3a2a'), seg=4, rings=1))
    return M.Mesh.concat(parts)


def m4_sd(mats, muzzle=0.64):
    """A short black carbine: collapsible stock, a box sight on top, a
    vertical grip and a long fat suppressor."""
    p = mat(mats, 'polymer', '#24252a', spec=0.25, gloss=16)
    g = G.gunmetal(mats)
    parts = [M.box(0.032, 0.07, 0.16, p, (0, 0.01, -0.18))]                         # stock
    parts.append(M.box(0.045, 0.07, 0.34, g, (0, 0.0, 0.08)))                       # receiver, rail
    parts.append(M.box(0.03, 0.10, 0.04, g, (0, 0.07, 0.07)).transformed(M.rot_x(8)))    # magazine
    parts.append(M.box(0.03, 0.07, 0.035, p, (0, 0.05, -0.01)).transformed(M.rot_x(-15)))  # grip
    parts.append(M.box(0.026, 0.06, 0.03, p, (0, 0.05, 0.20)))                      # front grip
    parts.append(M.rounded_box(0.04, 0.05, 0.08, p, (0, -0.06, 0.06), r=0.012, seg=6, p=4))   # sight
    parts.append(M.cylinder(0.026, 0.026, muzzle - 0.25, p, seg=10, z0=0.25))       # suppressor
    return M.Mesh.concat(parts)


# ------------------------------------------------------------ the body

class LeagueBody(MF.ModBody):
    def parts(self):
        out = super().parts()
        p = self.full
        lb = H.LIMB * H.build_params(p).get('bulk', 1.0)
        if p.get('armband'):
            bm = self.mm(p['armband'], '#ffffff')
            out.append(('arm_l', M.cylinder(0.078 * lb + 0.012, 0.074 * lb + 0.012, 0.075, bm, seg=12, z0=-0.13), None))
        if p.get('scarf'):
            sm = self.mm(p['scarf'], '#ffffff')
            out.append(('chest', M.loft([(0.26, 0.15, 0.115), (0.30, 0.125, 0.10), (0.34, 0.09, 0.085)], sm, seg=16,
                                        cap_bottom=False, cap_top=False), None))
            out.append(('chest', M.ellipsoid(0.05, 0.035, 0.04, sm, (0.02, 0.10, 0.27), seg=8), None))
            out.append(('chest', M.rounded_box(0.05, 0.02, 0.14, sm, (0.04, 0.125, 0.19), r=0.008, seg=6, p=4)
                        .transformed(M.rot_y(-10)), None))
        return out

    def _hat(self, add0):
        def add(mesh, tag='hat'):
            add0('head', mesh.transformed(M.scale(H.HEAD)), tag)
        hat = self.full.get('hat')
        if hat == 'beret':
            hm = self.mm('hat', '#ffffff')
            # a flat disc pulled down over the right side of the head
            add(M.ellipsoid(0.135, 0.132, 0.045, hm, (0.025, -0.01, 0.205), seg=18).transformed(
                M.translate(0.025, 0, 0.205) @ M.rot_y(16) @ M.translate(-0.025, 0, -0.205)))
            add(M.cylinder(0.122, 0.124, 0.035, self.mm('beret_band', '#1e1c1a'), seg=18, z0=0.135).transformed(
                M.scale(1, 1.08, 1)))
            add(M.extrude([(-0.025, 0.0), (0.025, 0.0), (0.02, 0.04), (0.0, 0.05), (-0.02, 0.04)], 0.012,
                          self.mm('badge', '#c8b070', spec=0.8, gloss=28, metal=True))
                .transformed(M.translate(-0.06, 0.12, 0.17) @ M.rot_x(90)))
        elif hat == 'flat_cap':
            hm = self.mm('hat', '#3a3632')
            add(M.ellipsoid(0.135, 0.15, 0.065, hm, (0, 0.012, 0.19), seg=18))
            add(M.cylinder(0.12, 0.122, 0.05, hm, seg=18, z0=0.13).transformed(M.scale(1, 1.06, 1)))
            add(M.ellipsoid(0.11, 0.07, 0.012, hm, (0, 0.13, 0.135), seg=12).transformed(
                M.translate(0, 0.13, 0.135) @ M.rot_x(12) @ M.translate(0, -0.13, -0.135)))
        elif hat == 'ija':
            hm = self.mm('helmet', '#6a6448', spec=0.12, gloss=12)
            add(H._dome(0.128, 0.142, 0.15, hm, (0, -0.004, 0.12), -0.025))
            add(M.loft([(0.09, 0.142, 0.156), (0.103, 0.132, 0.146)], hm, seg=18, cap_bottom=True, cap_top=False))
            add(M.extrude([(0.0, 0.03), (0.009, 0.009), (0.03, 0.009), (0.013, -0.004), (0.019, -0.026), (0.0, -0.012),
                           (-0.019, -0.026), (-0.013, -0.004), (-0.03, 0.009), (-0.009, 0.009)], 0.012,
                          self.mm('badge', '#c8a850', spec=0.7, gloss=24, metal=True))
                .transformed(M.translate(0, 0.142, 0.19) @ M.rot_x(90)))
            fm = self.mm('havelock', '#8a7e58')
            for k, (x, ry) in enumerate(((-0.10, 55), (-0.035, 10), (0.035, -10), (0.10, -55))):
                add(M.rounded_box(0.085, 0.012, 0.15, fm, (0, 0, -0.07), r=0.005, seg=4, p=4).transformed(
                    M.translate(x * 0.9, -0.12 + abs(x) * 0.25, 0.10) @ M.rot_z(ry) @ M.rot_x(-14)), 'hat')
        else:
            super()._hat(add0)


@archetype('league_soldier')
class LeagueSoldier(MF.ModSoldier):
    def __init__(self, unit, params):
        super().__init__(unit, params)
        bp = dict(params)
        bp['weapon'] = params.get('weapon', params.get('gun'))
        bp['offhand'] = params.get('offhand')
        self.body = LeagueBody(bp)
        self.mats = self.body.mats
        self.parts = self.body.parts()
        self._props = {}
        if self.gun in GUNS:
            self.gspec = GUNS[self.gun]

    def prop(self, name, **kw):
        fn = globals().get(name) if name in GUNS else None
        if fn is None:
            return super().prop(name, **kw)
        key = (name, tuple(sorted(kw.items())))
        if key not in self._props:
            k = float(self.p.get('gun_thick', 1.6))
            self._props[key] = fn(self.mats, **kw).transformed(M.scale(k, k, 1.0))
        return self._props[key]
