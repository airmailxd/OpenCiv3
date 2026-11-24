"""The League riders (the realism pass on the NumPy units): the Teutonic
Knight and the Roughrider. Their old catalog (ArtGen/units/catalog/
league_mounted.py) isn't on this branch, so their horse_rider entries live
here (read for identity from realism-pc1/art-base; mounted.build uses ENTRIES
when the catalog has no entry). New parts only:

- hat 'slouch_hat' (riders.HEADGEAR): the drab felt campaign hat of the 1st
  US Volunteer Cavalry, a creased crown, a broad soft brim, a dark band.
"""
import math

import props
import riders
from props import T

props.DEFAULTS.setdefault('cap_cross', ('#161412', 0.85, 0.0, 'cloth'))   # the Order's black cross on the trapper
props.DEFAULTS.setdefault('slouch_felt', ('#8a7656', 0.95, 0.0, 'cloth'))
props.DEFAULTS.setdefault('slouch_band', ('#2a2218', 0.8, 0.0, 'cloth'))

SKIN = '#c49a72'

ENTRIES = {
    # a knight-brother of the Teutonic Order, 13th century: the white mantle and surcoat with the black cross, a
    # great helm, mail, a heater shield, a lance with a pennon; a barded destrier in a white trapper with black
    # crosses (the civ colour on the shield's field, the trapper's border band and the pennon)
    'Teutonic Knight': {
        'archetype': 'horse_rider', 'rider_scale': 0.99, 'attack': 'thrust', 'victory_rear': False,
        'horse': {'barding_steel': '#9a9ea6', 'blanket': 'barding', 'caparison_over': True, 'chanfron': True, 'cloth_drop': 0.4,
                  'coat': '#4a4440', 'mane': '#1e1612', 'neck_cover': 'steel', 'over_drop': 0.38, 'over_gap': 0.06,
                  'over_span': [-0.62, 0.36], 'points': '#34302e', 'saddle': '#4a3424', 'trim': '#2a2622'},
        'rider': {'bottom': 'none', 'hair': 'none', 'headgear': 'great_helm', 'metal': ['helmet', 'boots'],
                  'offhand': 'shield_oval', 'pants': 'pants', 'prop_scale': 1.25, 'sleeves': 'mail', 'top': 'tunic',
                  'weapon': 'lance', 'wraps': 'boots', 'tint': ['shield_emblem'],
                  'colors': {'belt': '#3a2a1c', 'boots': '#8a8c92', 'helmet': '#9a9ca2', 'mail': '#7a7c80', 'pants': '#7a7c80',
                             'shield_face': '#ece8e0', 'shield_rim': '#2a2622', 'skin': SKIN, 'top': '#ece8e0'}},
    },
    # a trooper of the 1st US Volunteer Cavalry, Cuba 1898: a drab slouch hat, the dark blue flannel shirt (the civ
    # colour), a pale neckerchief, the Mills cartridge belt, brown canvas trousers and leather leggings, the Krag
    # carbine held up from the thigh; a bay in the black McClellan saddle on the folded blue army blanket with its
    # orange stripe (civ, an orange edge), a blanket roll, reins to the hand
    'Roughrider': {
        'archetype': 'horse_rider', 'rider_scale': 1.06, 'attack': 'shoot_gun', 'victory_rear': False,
        'horse': {'blanket': 'saddle_cloth', 'cloth_drop': 0.32, 'coat': '#6e4a30', 'mane': '#1e1612', 'points': '#2a1e16'},
        'rider': {'bottom': 'none', 'hair': 'short', 'headgear': 'slouch_hat', 'offhand': None, 'pants': 'pants',
                  'prop_scale': 1.25, 'tint': ['top'], 'top': 'long_sleeved', 'weapon': 'rifle', 'wraps': 'boots',
                  'colors': {'skin': SKIN, 'hair': '#4a3424', 'top': '#ffffff', 'hat': '#8a7656', 'belt': '#b4a47c',
                             'boots': '#5a3a22', 'pants': '#7e6a4c'}},
    },
}


@riders.hat('slouch_hat')
def slouch_hat(p, c, tinted, _, brim=0.2, crown=0.12, droop=0.02, pinch=0.04):   # (R2) a narrower brim, a pinched front
    """The 1898 campaign hat: a tall soft felt crown with a fore-and-aft
    crease and a pinched front, a dark band, a broad brim drooping a little
    at the front and back (drab felt; the civ colour never)."""
    rows = []
    for j in range(9):
        t = j / 8
        z = crown * t
        r = 0.122 * (1 - 0.18 * t ** 2)
        ring = []
        for k in range(24):
            a = 2 * math.pi * k / 24
            x, y = r * math.sin(a), r * math.cos(a)
            zz = z - (0.035 * t ** 2 * math.cos(a) ** 2 * (abs(math.sin(a)) < 0.4))   # the crease along the top
            if y > 0:   # the pinched front
                x *= 1 - pinch / 0.12 * t * (y / r)
            ring.append((x, y, zz))
        rows.append(ring)
    p.add(props.loft(rows, False, True), 'slouch_felt', smooth=True)
    p.add(props.lathe([(-0.005, 0.124), (0.028, 0.121)], seg=28), 'slouch_band', smooth=True)
    br = []
    for rr in (0.118, 0.17, brim):
        ring = []
        for k in range(32):
            a = 2 * math.pi * k / 32
            d = droop * ((rr - 0.118) / (brim - 0.118)) ** 2 * abs(math.cos(a))   # down at the front and back
            ring.append((rr * math.sin(a), rr * math.cos(a), -0.003 - d))
        br.append(ring)
    bm = props.loft(br, False, False)
    props.bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.008)
    p.add(bm, 'slouch_felt', smooth=True)


# (Roughrider R2) the cavalry seat, the carbine resting forward off the right thigh (not upright: it read as a pole)
riders.RIDE_POSES.setdefault('carbine_thigh', dict(riders.RIDE_POSES['cavalry'], hand_r=(-0.3, -0.18, 0.26), elbow_r=0.5))   # (R3) the hand at the hip
