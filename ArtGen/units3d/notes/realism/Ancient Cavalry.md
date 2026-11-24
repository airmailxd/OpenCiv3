# Ancient Cavalry (realism pass)

## Design in words

**The troop type.** A Roman auxiliary cavalryman (eques alae) of the 1st-2nd
century AD: the armoured horseman of the classical world's late phase, a
step up from the Horseman's light Greek rider. He wears a mail shirt, an
iron-and-bronze cavalry helmet with cheek guards and a crest, carries a flat
oval shield and throws javelins; he sits in the four-horned saddle, still
without stirrups. Made as a tabletop miniature on a sturdy dun.

**Must read at 1x:**
1. The big oval shield on the left arm, its civ cross and rim (the largest
   civ shape), angled so its face or edge shows in every direction.
2. The javelin raised to throw: the right arm drawn back above the shoulder,
   the javelin pointing forward and up (the Horseman thrusts down; this one
   throws). A blunt-ended head that stays one piece at 2x; out of the fit.
3. The crested cavalry helmet with cheek guards (riders' attic_helm), raised
   off the eyes so the face is lit between the guards, the crest civ.
4. The grey mail shirt over a civ tunic: the rider armoured, unlike the
   Horseman's bare chiton.

**The horse.** A sturdy dun with dark points and a dark pelt over the saddle
(as the shipped unit), the family's legs; distinct from the Horseman's bay
and the Mounted Warrior's dun-and-cream by its darker pelt and the shield.

**Civ colour, three regions:** the tunic (sleeves and skirt below the mail),
the crest and the shield's cross and rim. Measured (tint.py, alpha>127):
the shipped Ancient Cavalry is 8.6 % (9.2 / 11.8 / 8.3 / 8.3 / 7.6 / 8.0 /
7.2 / 8.2); the neighbours the Horseman 16.8 % and the Mounted Warrior
26.2 %. Target 9-14 %.

**Stock neighbours:** Horseman and Mounted Warrior.

## Rounds

Render ~20 s a round; close-ups mnt_heads.py.

### Round 1
The 'ancient_cav' seat (no stirrups; the javelin drawn back above the
shoulder), the Attic cavalry helmet with cheek guards and a civ crest, a grey
mail shirt (matte) over a civ tunic, a flat oval shield (0.5 x 0.78) with a
civ cross and rim, the javelin 1.7 m with a blunt tip (out of the fit), a
trimmed mane and reins to the hand. Tint 4.4 %; the helmet low over the eyes;
the javelin across the face in the front views.
### Round 2: a civ sagum (the cape), aged bronze (#8c6a3c), a broader shield cross, lift 0.12; the hand further out: the javelin still at head height across the face from the front.
### Round 3 (critic1)
The javelin at the ready: the hand out to the right at shoulder height, the
javelin forward and up beside the head (never across it); the helmet wider
(1.1) and lifted 0.15, the cheek guards wide of the face. specks.py clean.
Tint per direction 8.2 9.1 5.6 5.5 7.4 17.7 12.8 8.4, mean 9.3 % (shipped
8.6; Horseman 16.8, Mounted Warrior 26.2).

## Critic round 1: continuity 65, fresh 69.25, average 67.1 (FAIL)
Fixes: the helmet ~1.5x a head (a gold bobblehead); the cloak a flat backpack
from behind; one bare javelin (spares, a longer one, the horned saddle,
breeches); the pale flat dun with a stiff upright neck (lower and arch it,
shade it, strap fittings).

### Round 4 (critic2)
The helmet a real head's size (0.84 x 0.76 x 0.76), darker olive bronze; the
sagum longer (0.62) and fuller (flare 0.55), falling over the rump; the javelin
2.1 m; the horse a darker, shaded dun (#9a7a4e, coat_contrast 1.25), the neck
lower and arched (-24, flex 20), a crupper and studded straps (phalerae).
Not done: the spare javelins, the horned saddle's horns, the breeches.
specks clean. Tint per direction 8.9 12.2 8.2 6.5 9.9 25.9 18.2 10.0, mean
12.5 % (shipped 8.6).

## Critic round 2: continuity 70.75, fresh 70.5, average 70.6 (+3.5)
Fixes: the cloak a flat civ slab (pin it off one shoulder); the horse's neck
llama-like, the head up; the javelin a lance (shorter), spares behind the
shield, the four saddle horns, bigger phalerae, a shaped saddle-cloth edge.

### Round 5
The sagum smaller (0.5 x 0.36) and swung onto the left shoulder (mounted.
PELISSE cape): the mail shows on the back; the neck lower and shorter, the head
flexed forward and down (raise -30, flex 34, neck 0.95); the javelin 1.86 m;
three spares behind the shield (mounted.SPARE_JAVELINS, out of the fit); the
four-horned saddle (tack saddle_horns); phalerae 0.05 on the breast strap and
crupper (stud_r, studs_rear); a rounded pelt with a bronze edge. specks: the
spares' thin heads and shafts in SE and E.
### Round 6: sturdier spares (javelin_bundle shaft_r 0.022, flat 0.8, a blunt tip): SE clean.
### Round 7 (critic3)
Thicker saddle horns. specks.py: E (dir 4) keeps one 2-px piece (brown, near
the saddle; not the horns or the spares' heads). Tint per direction 7.5 10.3
6.6 5.9 5.4 14.1 13.1 10.9, mean 9.2 % (shipped 8.6).

## Critic round 3: below C2 (needs 72.1 at C4, else plateau on C2 05bd91c6)
Fixes: the horse (a smooth topline, a longer arched neck, a larger head, the
poll near the rider's chest, darker points, muscle shading); the cloak in 2-3
deep folds with a curved hem, tapering from the clasp; a soft dark hide, no
rim; shorter legs (the foot at the girth), boots and bracae; the rider 5-10 %
larger; a scutum with a boss.

### Round 8
The neck 1.1, the head 1.12, raised -20 and flexed 30; points #1e1812,
coat_contrast 1.4; the sagum narrower at the clasp (0.26, flare 0.9) with 2.5
deep folds (cape_folds, cape_depth 0.07) and a curved hem (cape_hem_curve
0.35: garments.flap hem_curve); the pelt #2a201a with a ragged edge
(pelt_ragged 0.018), no hem line (tack hem_line False), folds 0.02; the knees
up and the foot back (ancient_cav thigh/calf); the rider 1.07; the boss 1.9x
(shield_oval boss).
### Round 9: 'pants' is not a key here: only the boots showed.
### Round 10 (critic4)
Breeches to below the knee (#5a4634, bottom 'breeches') and dark boots.
specks.py clean. Tint per direction 10.1 13.2 7.2 6.7 8.2 16.9 16.2 13.8, mean
11.5 %. Not done: mail at the cloak's inner edge, the cocked throwing arm.

## Critic round 4: continuity 75.5, fresh 70.5, average 73.0 (+2.4)
The cloak and pelt landed. Regressions: the phalerae, crupper and horned
saddle were gone (32edf6cd, the Horseman restore, had replaced this tack
entry with an older one); the dun pale and flat; the bare shin orange over the
boot. Fixes: the head lower and forward, the poll below the chest; mail that
reads as metal, a belt; a taller scutum with a bolder device; the javelin
cocked overhead; the horns visible.

### Round 11
C3's crupper and phalerae back, bigger (0.065) and brighter (tack stud_key
'phalera'); the horns 1.35x in buff leather (horn_scale, horn_key); the coat
#8a683e, mane and points #16100c, coat_contrast 1.65; the neck raised -34,
flexed 24; mail rows (#3e4046 at 16, 'rows'), the cuirass #6e7076, a dark
belt, shoes; the scutum 0.56 x 0.8 with a 0.28 cross, the rim dark (not civ),
the boss 1.6; the throwing hand (-0.32, 0.14, 0.8), the javelin (0.12, 0.85,
0.4): it crossed the face.
### Round 12: the hand overhead (-0.3, 0.2, 0.95), the javelin (0.15, 0.9, 0.3); long bracae (no bare shin). specks: a spare javelin's head over the shield in E.
### Round 13: a sturdier javelin (shaft 0.034, tip 0.016, flat 0.85): the E piece stayed.
### Round 14 (critic5)
The spares' heads blunter (SPARE_TIP 0.024). specks.py clean. Tint per
direction 5.6 8.9 6.2 5.9 6.0 14.6 15.7 10.6, mean 9.2 %.

## Critic round 5: continuity 78.25 CRITICAL (the javelin clips the helmet and face in 3-4), fresh 70.75, average 74.5
C4 stays the fallback until the clip is gone. Fixes: an overarm carry at the
shoulder, clear of the helmet by a helmet-width in every view, a thinner shaft
and a bigger bright head; the head lower and forward in 7, no white blob at the
ears, smoother coat shading; the rider ~10 % larger; the crest apart from the
cloak; the horns visible.

### Round 15
The hand above and behind the right shoulder (-0.44, 0.22, 0.78), the javelin
(0.12, 0.97, -0.18), shaft 0.024, head 1.55; the rider 1.17; the neck -38,
flexed 20; coat_contrast 1.3; the horns 1.7x; the spares 1.2 m (SPARE_LEN, off
the ears); a pale untinted crest: the whole helmet rendered cream (not
understood): reverted.
### Round 16: the civ crest back. 2x check: the shaft still passed at chin level in 3-4.
### Round 17 (critic6)
The hand wider and further back (-0.5, 0.3, 0.72), the javelin steeper
(0.25, 0.9, -0.35): at 2x clear of the helmet in all eight (in 4-5 it crosses
the chest and waist). specks.py clean. Tint per direction 7.0 10.5 7.6 6.0 6.0
17.7 19.2 13.7, mean 11.0 %. Finals gate passes. Not done: a shield wreath.

## Critic round 6: continuity 77.5 (no clip), fresh 70.75, average 74.1
## FINAL = critic 6 (ec0d8a7c, 74.1); C5 (74.5) clipped the helmet, so not eligible
Closed under the three-critic-round limit. HEAD reproduces the critic-6 sheet
(max diff 0.004); in verify_all.sh.
Open points (critic 6):
- The javelin aims down at the ground or the horse's neck, the arm stiff (a
  near T-pose): a cocked overarm throw (elbow ~90 degrees, the hand above and
  behind the ear, the shaft level, the point forward past the head); in 2 the
  head toward the viewer, in 6 the whole shaft showing.
- The horse's poll forward and down in 5; a small kinked head in 4; more coat
  value range; the phalerae flatter, wider discs.
- Short tunic sleeves with bare forearms, the civ moved into a fuller crest;
  a lighter mail ring texture with a hem.
- NB NECK_RAISE_BY_UNIT is the neck's absolute turn (the shared default -38
  raises it; less negative is lower): C6's -38 lifted the head from C5's -34.
