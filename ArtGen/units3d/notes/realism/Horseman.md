# Horseman (realism pass)

## Design in words

**The troop type.** A classical Greek horseman of about 400-300 BC, the
hippeus of Xenophon's day: an ancient light-to-medium cavalryman who rode
without stirrups on a cloth over the horse's back and fought with the spear
(the xyston). Civ3's Horseman is the first mounted unit, so it should read as
the simple, early rider of the roster. Made as a tabletop miniature: a Greek
rider on a small, sturdy bay.

**Must read at 1x:**
1. The Boeotian helmet: a bronze cavalry helmet with a broad, flaring
   folded brim (Xenophon's recommended cavalry helmet), a shape no stock
   neighbour has (the Ancient Cavalry's crested helmet, the Mounted Warrior's
   feather bonnet). It's raised off the eyes, so the face reads as lit skin.
2. The spear raised overhand, ready to thrust down: the hand above the
   shoulder, the long shaft forward and a little down, with a blunt-ended
   leaf head that stays one piece at 2x. The spear is out of the fit
   (mounted.POLE_WEAPONS), so the horse and rider are sized like the stock
   riders (the shipped Horseman is shrunk by its spear to ~42 px).
3. The civ chlamys: a short cloak pinned at the shoulder, flying back
   (garments.cape), and a civ chiton: the two civ shapes on the rider.
4. No stirrups (the period): the legs hanging long, bare, in short boots,
   the seat on a civ saddle cloth with a pale fringe border.

**The horse.** A small, sturdy ancient horse, a bay with black points,
a trimmed (hogged) mane as on Greek reliefs and a blaze, the family's legs.
The bay separates it from the Ancient Cavalry's dun and the Mounted
Warrior's dun-and-cream.

**Pose.** Sitting upright with the legs long (no stirrups), the left hand at
the reins, the right arm raised with the spear overhand.

**Civ colour, three regions:** the chiton, the chlamys and the saddle cloth.
Measured (tint.py, alpha>127): the shipped Horseman is 16.8 %
(18.3 / 13.3 / 17.9 / 15.8 / 17.2 / 17.4 / 18.4 / 16.3); the neighbours the
Ancient Cavalry 8.6 % and the Mounted Warrior 26.2 %. Target 14-20 %.

**Stock neighbours:** Ancient Cavalry and Mounted Warrior.

## Rounds

Render ~22 s a round; close-ups mnt_heads.py.

### Round 1
A Boeotian helmet (new riders hat 'boeotian': a bronze dome, a broad brim
flaring down in soft pleats, narrower over the face, a ridge, a rolled rim);
the 'horseman' seat (no stirrups, the legs long, the spear overhand); a civ
chiton and civ chlamys (the cape), bare arms and legs, short boots, no mail;
the xyston 2.8 m with a blunt-tipped head (tip 0.012), out of the fit; a cut
civ saddle cloth with a pale fringe border, a hogged mane, a blaze, reins to
the hand. Tint 20.4 %.
- The brim covered the eyes; the spear's shaft passed in front of the face.
### Round 2: the spear hand out to the right and lower, the spear angled out; lift 0.1, the brim 0.8 wide.
### Round 3: lift 0.14 (tried the pole's shadow off: it wasn't the speck; reverted).
### Round 4 (critic1)
The brim narrower over the face, lift 0.17: eyes, nose and mouth lit under
it; the spear further out (0.4, 1.0, -0.36). Tint per direction 19.7 13.8
20.7 21.8 20.8 23.6 22.3 21.4, mean 20.5 % (shipped 16.8; Ancient Cavalry
8.6, Mounted Warrior 26.2).
specks.py: dir 8 (W) shows a 21-px piece: the reins' knot over the civ saddle
cloth. The base layer is transparent where the civ colour goes (the tint layer
carries it), so a dark strap over a civ region counts apart; with the base and
tint layers together every direction is one piece.
Height: with the spear out of the fit the horse and rider are stock-sized
(the Horseman measured 53 px at 1x in W with the family code, the shipped one
~42 px under its spear; the Cavalry 56, the Cossack 60).

## Critic round 1: continuity 64, fresh 64.5, average 64.25 (FAIL)
Near-clipping: the overhand spear flat at brow height across the face (NE, N,
W) and pointing down in S, SE. Fixes: one grip at shoulder height or below,
the tip forward along the horse; a darker, thinner shaft with a leaf blade; a
neutral wool chlamys pinned at one shoulder (civ only on its border), a belted
tunic; less orange skin; aged, duller bronze; civ off the bridle; a neutral
saddle cloth with a civ border.

### Round 5
The spear underarm at the hip, forward along the horse (0.18, 1.0, 0.1), a
weathered ash shaft (#8a6a44, 0.034), a leaf head; the chlamys natural wool
(#8a7c66) with a civ border, swung to one shoulder (mounted.PELISSE cape); a
dark tunic hem; skin #b48c70, slimmer arms; the helmet 0.9; no civ nose wrap,
a neutral felt saddle cloth (#6e6050) with a civ border (new tack option
border_tint). Tint 7.7 %: too little.
### Round 6 (critic2)
Wider civ borders (the saddle cloth 0.11, the chlamys 0.12); the helmet in a
duller aged bronze (riders.boeotian key='aged_bronze'). specks.py clean.
Tint per direction 12.4 10.9 13.0 12.5 12.6 11.6 9.1 10.3, mean 11.5 %
(shipped 16.8): below the critics' 17-20, with the civ now only on the tunic
and the two borders.

## Critic round 2: continuity 67.75, fresh 68.25, average 68.0 (+3.75)
Fixes: the helmet a flat khaki sun hat with a black band over the eyes; the
chlamys a crate on the back merging with the saddle cloth; the spear upright
in NE and point-down in S; the saddle cloth too deep.

### Round 7 (critic3)
The helmet a darker olive bronze that keeps its highlight (olive_bronze), a
brass rim (boeotian rim='gold'), the brim narrower (0.62) and turned further
down (droop), lifted 0.2: the face out of the brim's shadow. The chlamys short
(0.3), dark warm wool (#5e4432) with its civ hem, swung diagonally from the
shoulder (roll 18). The spear out to the right as well as forward (0.5, 1,
0.08): a diagonal in every view, never upright or point-down across the body.
The saddle cloth to the girth line (cloth_frac 0.42). specks clean. Tint per
direction 12.3 10.5 13.3 12.3 12.6 12.2 11.1 10.9, mean 11.9 %.

## Critic round 3: continuity 72.25, fresh 72.5, average 72.4 (+4.4)
Fixes: the rider small and hunched on a big long-legged horse; the brim's
shadow as sunglasses, the spear across the face in W, steep in NW; the saddle
cloth a flat grey box (warm fleece, folds, phalerae); the chlamys' civ hem.

### Round 8
A smaller ancient horse (HORSE_SHAPE size 0.92, leggy 1.0) under the stock-scale
rider; the torso straighter, the legs longer down the flank; more chest and arm
mass; the helmet lifted 0.24, the brim 0.58; the spear lower (hand 0.28, level);
a warm pelt with folds, studded straps (phalerae), the chlamys' civ hem 0.16.
Tint 17.8 %: the pelt's border read as a civ box round the legs.
### Round 9-10: a narrower border; back to a warm felt (#5a4030) with tube folds: still a boxy frame.
### Round 11 (critic4)
The cloth smaller (cloth_frac 0.3, back 0.32), a narrow civ border (0.035).
specks clean. Tint per direction 12.4 18.7 14.3 10.4 13.6 18.0 13.1 9.3,
mean 13.7 %. Still weak: the small cloth reads as an upright frame round the
thigh in the close-ups.

## Critic round 4: continuity 68.25 (CRITICAL: the saddle cloth a hollow civ frame), fresh 66.5, average 67.4 (no gain)
Measured at 1x (mean bounding box over the 8 directions, with shadow and
weapon): C4 60.8 high x 53.6 wide; the stock Horseman 56.4 x 39.1, the stock
Ancient Cavalry 59.4 x 36.0, the stock Cavalry 61.2 x 41.6. The height is
within the stock riders'; the width was the 2.8 m spear.

### Round 12
A tan felt (#c8a46c) that separates from the bay, no civ border; the lower
legs shorter (the shins back, the toes down and forward); the spear 2.4 m;
the helmet lifted 0.28 with a narrower brim (0.5), lighter skin (#d2a684).
Tint 5.9 %: the civ only on the tunic and the chlamys' hem.
### Round 13 (critic5)
A sleeved civ tunic and a broad civ hem on the chlamys (0.28). specks clean.
Tint per direction 10.7 12.9 12.6 7.4 8.0 11.8 6.4 5.0, mean 9.3 % (below the
stock 16.8).

## Result: plateau, FINAL = critic 3 (5ce9365b)
Critic averages (continuity / fresh): C1 64.25 (64 / 64.5); C2 68.0 (67.75 /
68.25); C3 72.4 (72.25 / 72.5) = FINAL; C4 67.4 (68.25 critical / 66.5); C5
69.6 (70.25 / 69.0). C3's Horseman settings restored at HEAD (the tack, the
spear carry, the seat, the remaster entry; no HORSE_SHAPE): the re-render's
comparison sheet matches critic3/final.png (mean abs diff 4e-7, one pixel
differs, none by more than 2 levels). specks clean. Tint per direction 12.3
10.5 13.3 12.3 12.6 12.2 11.1 10.9, mean 11.9 %.
Open points: the brim's shadow over the eyes in the front views; the saddle
cloth reading as a box (and, as a civ frame, the C4 critical); the scale: the
rider small on a big horse to the critics (C4's smaller horse 0.92 scored lower
overall); the spear's angle per direction; civ colour below the stock 16.8.

## Fit fix (2026-11-23, re-render only)
The spear out of the fit made the unit draw 1.57x the stock's bbox area and
1.56x its opaque pixels (fitcheck). render_vehicles.FIT_SCALE 'Horseman' 0.82:
bbox-area 1.06, pixels 1.06; 1x heights 46-74 -> 47-58 (the stock's include its
spear). No geometry, material or civ change. final.png rebuilt; the gate's
reference with it.
