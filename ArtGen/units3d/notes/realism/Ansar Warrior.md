# Ansar Warrior (realism pass)

## Design in words

An early Arab (Rashidun) horseman: a white robe and trousers, a civ turban
wound over a spiked cap, lifted off the eyes so the bearded face reads lit; a
broad civ sash; a long cavalry spear with a gilded head held at the ready; a
round shield with a gold rim and boss; a sword at the hip; dark leather boots.
A black Arab horse, its muscles shaded so the shape reads on grass, with
Civ3's bold civ reins and breast strap, civ crupper and tassels.

Civ in deliberate regions: the turban, the sash, the horse's bold reins and
strapping. Civ3's civ trousers and boots dropped (a civ field from the waist
to the feet).

## Rounds (own)
### Round 1: the turban 1.0 and lifted 0.08; white trousers, dark boots (no civ below the sash); the black coat shaded (1.35). Tint 5.3 %: low.
### Round 2: a civ saddle cloth with a gold edge: 11.5 %.
### Round 3 (batch 1): the turban 0.95 and lifted 0.17; a blunt spear point (tip 0.02: the W speck). specks.py clean.
Tint per direction 12.4 5.0 10.6 14.2 10.5 7.8 12.9 15.4, mean 11.1 % (stock
11.9); footprint 1.08x the stock's area (1.06x height).

## Batch 1: A 68.5, B 70.75, average 69.6
The spear butt into the saddle cloth in 4-5 (borderline clip). Fixes: a wound
turban with a tail, no spike; robe folds and a sash; a slimmer, darker spear
with a shorter leaf; the face lit; drape.
### Round 4-5 (batch 2)
head_wrap wrapped (five crossing wraps, a cloth crown, a tail end; no plume);
the robe's skirts in folds (COAT_SKIRT, via a new 'ansar' seat: the
Horseman's underarm spear, the shield out on the forearm); the spear 2.1 m,
shaft 0.026 in dark wood (#4a3220), a shorter leaf (1.5), the butt short (grip
0.8); a draped, gold-fringed civ saddle cloth; face_glow 0.35. Tint 8.9 %
(stock 11.9); footprint 1.15x the stock's area (1.09x height). specks.py clean.

## Batch 2: continuity 73.5, fresh 67.0, average 70.25 (no gain on 69.6); no spear clip
The turban a civ beanie. Fixes: a white wound turban (civ on one band and the
tail only), the civ on the tack and cloth, robe fold shading, the cloth under
the thigh draped, the spear longer and clear of the body in 1-3.
### Round 6-7 (batch 3)
head_wrap white (option); the robe folds 0.14 with a darker hem; the saddle
cloth rounder (0.22), folds 0.09, larger (frac 1.1); the spear 2.5 m, carried
forward, up and out (0.3, 0.75, 0.55); FIT_SCALE 0.95 (the pixels were
1.16x stock). Tint 7.5 % (stock 11.9: the turban's civ moved off); fitcheck
bbox-area 1.31 (the spear), pixels 1.06. specks clean.

## Batch 3: continuity 71.0, fresh 76.0, average 73.5 (+3.25); CRITICAL: the civ unreadable at 1x in 2 and 6 (2.7 %, 1.7 %)
### Round 4 (solo4: review\ansar-warrior\solo4), aimed only at the critical
A broad civ sash (0.21), a broader civ breast strap and crupper (0.085), the
civ saddle cloth further back behind the rider (cloth_back 0.5); the white
turban kept. Tint per direction 12.1 7.6 14.5 16.4 14.6 7.2 13.6 15.4, mean
12.7 % (stock 11.9): 2 and 6 at 7.6 and 7.2 %. fitcheck bbox-area 1.31 (the
spear), pixels 1.07; specks clean.

## Round 4: continuity 72.0, fresh 75.0, average 73.5 (= B3, which had the open civ critical); the civ critical closed
## FINAL = round 4 (ba03be5f, 73.5)
HEAD reproduces the solo-4 sheet; in verify_all.sh. fitcheck bbox-area 1.31
(the spear), pixels 1.07; tint 12.7 % (stock 11.9).
Later pass:
- the turban from 3-4 offset wraps with a lower, flatter, uneven crown (it
  reads as a pith helmet in 1-3), the tail lower and behind the head, clear
  of the face in 6;
- a belted fold line, an open neckline and shaded folds on the robe's torso
  (1, 2, 3, 6, 8).
