# Medieval Japanese Settler: realism design (2026-11-23)

MDJ unit, remade in units3d.

## The real model
A peasant moving to new land: a topknot, a pale kosode tucked up to the knee, a civ obi and civ kyahan leg wraps,
waraji; a big furoshiki bundle on his back with a rolled mat and a pot, a tall staff.

## What must read at 1x
1. The big bundle over the shoulders;
2. the tall staff beside him;
3. a pale figure with a civ sash and civ lower legs.

## Pose
'upright' with the staff (the stock Settler's).

## Civ colour
The obi and the leg wraps. Shipped 8%; stock Settler 12% (8-15). Target 10-18%.

## Neighbours and size
Critic sheet: shipped, new, Samurai, Settler. Size counterpart: the Settler.

## Build rounds (PC2, numpy-mdj-foot, 2026-11-24)
- The obi widened to 0.12 and a civ hachimaki added, raised above the brow (brow_drop 0.055), for civ 12 %.
- unit_scale 1.1 (sized against the stock Settler).
- a1 numbers: tint 12.0 % (NumPy 18.1); opaque area 0.98 of the stock Settler, 1.42 of the NumPy sprite; specks clean.

## Critic round a1: A 70.8 / B 56.5 = 63.65 (critical, A: the staff floated in dir 4)
a2 fixes: posed 'ready' with the staff through a closed right fist and the left arm down at the side in every
direction; the load is `nmf_furoshiki` (a deep squared cloth parcel in persimmon brown, knotted ears, a straw mat
across its top below the head, its long ends over both shoulders knotted on the chest in civ); a kosode in a
natural hemp tone with its crossed collar (v_collar), the skirt in the same cloth; single-tone civ kyahan
(wraps_plain); the hachimaki dropped (it read as a target from above); unit_scale 1.15.

## Critic round a2: A 83.5 / B 68 = 75.75 (staff critical closed)
a3 fixes (the last round): the bundle soft knotted cloth in indigo (nmf_furoshiki soft: a squarish bundle with
soft corners, the knot on top, no mat), raised to the shoulder blades (pack_up 0.16) and showing beside the
shoulders in dirs 1-3; a small woven sugegasa over the topknot (it read as a mohawk); the staff 1.95 m.

## FINAL: a3, A 83.5 / B 69.3 = 76.4 (rounds: a1 63.65, a2 75.75, a3 76.4); no critical open
Final sheet: reviews/realism/medieval_japanese_settler/final(_red).png (shipped NumPy, final, Settler, Worker).
Later pass: the bundle reads as a rigid crate: soft, sagging cloth with knot ears, a lighter or warmer tone (the
indigo blends with a blue civ), showing in dirs 1-2.
