# Keshik (realism pass)

## Design in words

**The troop type.** A keshig, the Mongol imperial guard of the 13th century:
a heavy horse archer in lamellar armour, a spiked helmet with a fur brim and
leather neck flap, riding a small, sturdy steppe horse with short stirrups,
shooting the composite bow at the gallop, including the famous backward
("Parthian") shot. Made as a tabletop miniature.

**Must read at 1x:**
1. The backward shot: the torso turned in the saddle toward the left rear,
   the bow arm out behind, the bow upright and drawn, a dark arrow nocked: a
   pose no neighbour has (the Mounted Warrior draws forward).
2. The spiked helmet with its fur brim, raised so the face reads lit.
3. The lamellar coat: a few broad civ rows (not a pinstripe), a dark lacing
   band between them, worn over a pale robe.
4. A stocky steppe pony, short-legged, a bay with a dark mane, a pale felt
   saddle cloth; no twin lances on the back (clutter at 1x).

**Civ colour, two regions:** the lamellar rows and the bow case/quiver
trim. Measured (tint.py, alpha>127): the shipped Keshik is 6.4 %
(7.4 / 9.9 / 7.1 / 6.4 / 5.1 / 3.9 / 4.8 / 6.3); the neighbours the Rider
35.2 % and the Horseman 16.8 %. Target 8-14 %.

**Stock neighbours:** Rider and Horseman.

## Rounds

Render ~22 s a round; close-ups mnt_heads.py.

### Round 1
The 'bow_back' seat (the torso turned to the left rear, the bow arm out
behind, the draw hand at the cheek), the bow re-held upright and drawn toward
the left rear with a dark arrow (mounted.BOW_DRAW), short stirrups; no back
lances; a pale felt; a stocky steppe pony (size 0.94, wide 1.06); a civ
lamellar in horizontal rows (new garments pattern 'rows'). Tint 1.1 %: the
lacing rows covered the civ.
### Round 2: thinner lacing rows: 2.9 %.
### Round 3: dark lacquered lamellar with bronze lacing over a civ deel: 2.1 % (only the sleeves showed).
### Round 4: the deel's civ skirts over the thighs (coat_skirt tint): 11.1 %. (A comment had swallowed the helmet lift.)
### Round 5 (critic1)
The helmet lifted 0.13 (the face lit), no hair. Tint per direction 9.1 8.1
8.8 11.1 12.6 17.3 11.3 11.4, mean 11.2 % (shipped 6.4; Rider 35.2,
Horseman 16.8). specks.py: E (dir 4) 8 small pieces and NE one: the bow, its
string and the arrow seen edge-on beside the turned torso.

## Critic round 1: A 64.5, B 72.0, average 68.25
Fixes: the backward shot (the bow at shoulder height canted ~20 degrees, a
level arrow back over the rump, the torso turned to the rear, the bow ~15 %
smaller); the 8 E specks; a steppe pony (shorter, heavier legs, a heavier
head, a shaggy mane), short stirrups; lighter iron lamellar with contrasting
lacing, the civ deel visible in front, a sash; a trimmed saddle cloth; less
white at the wrists.

### Round 6
bow_back: twist 14/26/32, head 38, the bow hand (0.42, 0.5, 0.48) and the
draw hand (0.04, 0.12, 0.5) at shoulder height, the knees higher; the bow
canted (BOW_DRAW up (-0.36, -0.1, 1)), 1.02 m; the pony leggy 0.9, wide 1.12,
legs 0.7, head 1.12; the asset's mane back; iron lamellar #7a7c80 with dark red
lacing (#4a2218), a thin civ sash, muted fletching (#8a7a62); the deel's
skirts longer and further round the front (0.52, a0 20); the felt with a dark
edge and folds. specks: SE/E dark pieces.
### Round 7: a trimmed, fuller mane (tack mane_shag 0.6) and a sinew string (0.013): the specks stayed: they were the reins rising to the raised bow hand.
### Round 8 (critic2)
The reins left on the neck (no 'hand'). specks.py clean. Tint per direction
11.9 9.1 10.6 12.3 13.8 15.6 14.8 15.4, mean 12.9 %. Not done: a bow case.

## Critic round 2: continuity 75.25, fresh 69.0, average 72.1 (+3.9)
The grey lamellar and the backward shot landed. Fixes: a composite bow (thin
limbs, a light horn belly, dark ears, its arc visible in S), a pale fletched
arrow; the pony's cannons shorter, the neck lower and thicker, the knees
higher, muscle shading; a draped fringed cloth, more deel over the thigh, a
quiver at the right hip.

### Round 9
props.bow composite (a dark lacquered back, a pale horn belly, dark ears),
thick 2.3, recurve 0.12; BOW_DRAW forward (-0.82, -0.57) and a pale arrow
(#c8b48a) with two crossed fletching vanes (BOW_DRAW fletch); the pony's
lower legs squeezed (HORSE_SHAPE shins 0.85: _refine_horse shin), coat_contrast
1.25; the knees higher; a draped pale felt (saddle_cloth, tubes folds 0.05) with
a dark trim and a fringe; the deel's skirts longer and further forward (0.56,
a0 10); the quiver at the right hip (remaster quiver_at / quiver_up, units.py),
pale fletching. The neck raised -18: long and level.
### Round 10: the neck -10: still long.
### Round 11 (critic3)
The neck -4 and shorter (neck 0.92): the head lower. specks.py clean. Tint per
direction 14.8 14.4 14.3 16.1 18.8 22.0 21.0 18.6, mean 17.5 %. The finals gate
(verify_all.sh) passes: the five finals unchanged.

## Critic round 3: continuity 78.5, fresh 67.25, average 72.9 (+0.8, no gain)
## FINAL = critic 3 (4195087b, 72.9)
Closed under the three-critic-round limit (C4 started, discarded uncommitted).
HEAD reproduces the critic-3 sheet (max diff 0.008); in verify_all.sh.
Open points (critic 3):
- The civ deel a flat slab to the knee (~25-30 %), the pale felt hidden, the
  fringe reading as black slits: a split coat skirt a third smaller with a
  contrasting hem, a pale felt with a civ border, ~18-20 % civ.
- The pony grazing-low, the head merging with the chest in 2: the poll at
  withers height. NB NECK_RAISE_BY_UNIT is absolute (default -38 raises the
  head; C3's -4 lowered it a long way); -34 looked right in the discarded C4.
- The bow hidden behind the head in 3 and 7 and the torso in 8 (cant it out);
  laced plate rows with lighter edges and a waist break; the hip quiver darker
  (birch bark) and readable at 1x.
