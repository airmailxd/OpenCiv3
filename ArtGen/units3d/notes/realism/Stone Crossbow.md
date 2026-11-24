# Stone Crossbow: realism design (2026-11-23)

MDJ unit, remade in units3d: a footman with a hand crossbow.

## The real model
The ritsuryo armies of early Japan (7th-9th c.) kept crossbows (do-kyu) in their arsenals, on the Chinese model.
A conscript in a pale kosode tied back with a civ **tasuki** cord, a civ cap (a soft eboshi), brown hakama bound
from the knee with dark leg wraps, waraji; the **crossbow**: a dark tiller, a laminated prod bound to its head, the
string drawn to a bronze nut, a bronze trigger, a bolt in the groove. Held ready across the hips.

## What must read at 1x
1. The crossbow's T: the prod across the tiller, held forward across the hips;
2. a pale figure with a civ cap and the civ tasuki X;
3. the hakama bound tight below the knee.

## Pose
'hip_hold': the right fist at the tiller's wrist by the right hip, the left hand under the fore-end, the bow end
forward and to his left. Animations: the gun template (aimed at the shoulder and loosed), without a flash or
smoke ('muzzle': False).

## Civ colour
The cap and the tasuki. Shipped 11%; stock Longbowman 11%, Musketman 33%. Target 12-22%.

## Neighbours and size
Critic sheet: shipped, new, Samurai, Longbowman. Size counterpart: the Musketman.

## Build rounds (PC2, numpy-mdj-foot, 2026-11-24)
- The harness was hidden under the kosode (civ 5 %): a crossed civ tasuki (chest_strap, cross, offset 0.04), a civ obi
  and a civ collar band; the cap stays civ.
- `nmf_xbow` (nmf_kit.py): mdj_kit's hand crossbow made to read at 1x: a thick black prod with pale bindings, a paler
  tiller, a thin string (thicker strings broke into speck lines), a blunter bolt head.
- unit_scale 1.09 (sized against the Archer, the ranged foot counterpart; 0.88 of the Musketman).
- a1 numbers: tint 11.7 % (NumPy 36.6); opaque area 1.03 of the stock Archer (0.88 of the Musketman), 1.11 of the
  NumPy sprite; specks clean.

## Critic round a1: A 85.0 / B 66 = 75.5 (B: reads as a European peasant)
a2 fixes: a jingasa (civ mon) instead of the cap; the kosode's crossed collar (gear 'v_collar', dark); single-tone
civ kyahan (wraps_plain) over the hakama, waraji; a lacquered bolt case at the right hip (`nmf_bolt_quiver`); the
prod shortened to 0.75; civ: tasuki (0.1), obi (0.1), kyahan, mon.

## Critic round a2: A 83.3 / B 71.3 = 77.3 (A: new critical, the crossbow lost in dir 6)
a3 fixes (the last round): the crossbow raised across the chest in both hands ('ready', weapon_dir 0.55, -0.55,
0.63, span 0.85), the prod's limbs out past the shoulders in every direction; a civ hachimaki above the brow
(brow_drop 0.05) instead of the jingasa (a twin of the Arquebusier's at 1x); the kyahan dark, tied at the knee, the
civ moved to a wider obi (0.12); a narrower hakama (balloon 0.035); the bolt case longer (0.45) in light wood.

## FINAL: a3, A 82.5 / B 66.3 = 74.4 (rounds: a1 75.5, a2 77.3, a3 74.4); no critical open
a3 closes a2's dir-6 critical; a1 is 1.1 higher, under the 1.5 gain threshold, so the final is a3 (no restore).
Final sheet: reviews/realism/stone_crossbow/final(_red).png (shipped NumPy, final, Archer, Musketman).
Later pass: in dir 1 the crossbow is edge-on and reads as a club: yaw it ~30 degrees; the hachimaki reads as a
beret: a thin band with tails, or a jingasa of another shape than the Arquebusier's.
