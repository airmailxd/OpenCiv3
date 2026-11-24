# Legionary: realism design

## The real-world model

An Imperial Roman legionary of about AD 100 (Trajan's Dacian wars; Trajan's Column, the Adamklissi
metopes and the finds at Corbridge and Newstead):

- **lorica segmentata** (Corbridge/Newstead pattern): iron girth hoops over the torso, shoulder guards of
  curved plates; under it a red-brown wool tunic to the knee;
- **Imperial Gallic helmet** (galea), iron, with a deep neck guard and cheek pieces; a crest fitted for
  battle and parade;
- **scutum**: the curved rectangular shield, about 1.05 x 0.65 m, plywood faced with leather, painted in the
  unit's colour with wings and thunderbolts, a bronze edge binding and a bronze or iron boss over a
  **horizontal grip** held in the fist (no arm strap);
- **gladius** (Pompeii type): a short straight blade with a long point, bone grip and round pommel, used to
  thrust from behind the shield;
- **focale**: a wool scarf at the neck against the armour's chafing; caligae (hobnailed sandal boots).

## The features that must read at 1x

1. The **tall curved scutum held braced** in front of the left half of the body: the biggest shape, and the
   unit's signature.
2. The **gladius held ready at the hip**, point forward and out past the shield's edge, not hanging limp at
   the thigh.
3. The **segmented grey armour** with its shoulder plates (a pale banded torso).
4. The **crested galea** (a civ-coloured crest on a grey helmet).

## The pose in words

Weight on the right (rear) leg, the left foot a half step forward behind the shield, its knee soft. The
pelvis rolls over the weight leg (the right hip up) and turns a little toward the shield; the shoulders roll
the other way (the right shoulder down) and the chest turns back toward the front; the head is level, over
the hips, turned a little toward the enemy. The torso is upright: no hunch, no forward lean. The left forearm
is level and forward, the fist closed on the shield's grip behind the boss, so the shield stands upright,
braced, its rim from the shoulder to the shin. The right elbow is bent at the side, the fist in front of the
right hip, the gladius held in it point forward, out and a little up: ready to thrust past the shield's edge.

## Civ colour

**Final (round 6): the crest, the tunic (skirt and sleeves) and a bold civ device on a dark scutum**, as the
Civ3 original; it measures 9-24 % by direction, mean 18 %. The first decision, kept below for the record,
left the rear directions at 2-3 % once the board's back was bare.

First decision: **the shield's painted field, the crest, and the focale**.

- The scutum's field was painted per legion, so it is the truest place for a "unit colour"; the device
  (wings and thunderbolts) stays pale. It is also the largest region and reads at 1x from the front and
  the sides.
- The crest is the second read, from every direction, including behind the shield.
- The focale is a small band at the neck, so the colour also shows from behind.
- The shipped cape is dropped: it carried the civ colour over the whole back (the shipped unit is 28 % civ
  colour on average, 40 % in some directions, against 9-16 % for its stock neighbours), and legionaries
  fought without the cloak. The tunic stays a natural madder red, not the civ colour.

Target, measured on the stock foot neighbours' DEFAULT sheets (tint pixels / unit pixels per direction,
2026-11-23): Hoplite 7-18 % (mean 11), Swordsman 7-10 (9), Spearman 11-24 (16), Pikeman 29-37 (34),
Longbowman 5-13 (9), Bowman 21-32 (26). So about 10-20 %, and roughly even across the eight directions.

## Rounds

### Round 1 (debug renders, then the first full sheet)

- A debug render showed the scutum's centre at chest height (the forearm level from a hanging upper arm puts
  the fist at 1.15 m): lowered the grip (upper arm a little forward, forearm sloping down) and flattened the
  gladius (it pointed up past the head from behind).
- Sheet: the shield now braced and upright, the gladius forward and out of the fist (it reads at 1x in five
  directions of eight, against the thigh before). The weight shift shows in the hips and the free knee.
- Wrong: the shield is held out at arm's length, off to the left; the torso shows beside it. Next: the
  forearm across toward the centre, the elbow in, so the board covers the left half and the middle.
- Wrong: the focale (0.06) reads as a blue cowl over the shoulder plates. Next: 0.022.
- Civ colour 21 % mean (10-34 by direction); the stock foot neighbours run 9-34 (Hoplite 11, Spearman 16).

### Round 2

- The board now covers the left half and the middle from the front; the device reads; the focale is a thin
  line of colour at the neck. The gladius reads forward and out.
- Wrong: from the sides (directions 3, 7) the shield still stands a forearm's length ahead of the body.
  Next: elbow in at the side, forearm more across the belly than forward.
- Wrong: the board's back is painted like its face, so from behind the civ colour fills the shield (dir 5).
  A scutum's back is bare leather-faced wood. Next: the back faces unpainted, the focale a little wider
  (0.03) so the colour still shows from behind.
- Civ colour 20 % mean (9-38).

### Round 3

- Closer: the board stands just ahead of the belly from the sides now, the elbow in.
- Wrong: from behind (dir 5) the board still shows its painted face. Cause: the solidified back shell kept the
  front's normals, so the "back" test never matched. Next: normals updated before the test.
- Civ colour 21 % mean (10-40); the front views stay high (the board fills them), which is the design.

### Round 4

- Same numbers to the pixel: the back was still painted. The solidify's new shell has the same winding as
  the face, so no normal test separates them. Next: the back told by its depth behind the board's curve.

### Round 5

- Unchanged again. A prop test (the scutum built alone, its faces' materials listed) found the cause: the
  board's faces had no normals when it was solidified, so the "board" was two coincident shells with no
  thickness, the painted front fighting the back. Fixed: normals before the solidify; the board is now
  2 cm thick, its back bare dark leather. (Other shields' builders may share this; not touched.)
- Regression check of the default posture on Spearman and Hoplite (review/posture/regress_*.png): the same
  figures, the hips shifted over the right leg, no broken holds or floating feet.

### Round 6 (with the animations)

- With the board's back bare at last, the civ colour fell to 2-3 % in the four rear directions (31-40 % in
  front): far from even. The coordinator's correction: match the stock foot neighbours (measured above:
  about 10-20 %, even across directions). The Civ3 original's own scheme does that: **crest, tunic and a
  civ device on a dark shield** (notes/anim/Legionary.md: "a civ-colored crest ... over a civ tunic ... a
  tall dark scutum painted with a civ device"). So the decision changes: the tunic (its skirt and the
  sleeves under the shoulder plates) in civ colour, the crest, and the wings-and-thunderbolt device in civ
  colour, drawn bolder (`bold` 1.6), on a dark leather-brown board; the focale dropped. The device is the
  legion's badge, the tunic the colour that shows from every side.
- Animations (templates): ATTACK1 the overhead backswing into a level thrust past the shield's edge, good.
  DEATH flings the shield up showing its face (as the original's f4), then he lies on his back, shield
  beside him. RUN carries the shield and the gladius forward, feet planted on each step. FORTIFY tipped the
  shield over like a tray (the template brings the forearm forward and up): now a braced shield keeps the
  stance's arm and only the body crouches.

### Critic round 1 fixes (63.5)

- Scutum: civ-coloured field again, one bold yellow-bronze wings-and-thunderbolt device (a 40x32 face grid,
  not 12x16, so its outline is clean), a 2 cm bronze rim, curve 0.26; back bare leather.
- The sword hand back at the hip, the gladius forward and out past the shield's edge, near level.
- Smaller shoulder plates, warmer steel and skin, caligae without greaves; tunic and crest in civ colour.
- Civ colour by direction: 38 38 33 18 18 26 23 31, mean 28 %.

### Critic round 2 fixes (67)

- Tunic natural madder wool; civ colour on the shield field, crest, a short cloak (the original's) and a thin
  hem trim: 27 32 19 7 16 22 16 16 by direction, mean 19 %.
- New scutum: the board snug ahead of the grip (it floated 21 cm ahead), a grip bar on posts and a wooden
  boss backing inside, a rim all round, a wings-and-thunderbolts device round the boss in dark gold.
- Lighter plate, caligae with lacing to mid-shin. The gladius points out at his right side; in the two
  side views (3, 7) the blade still crosses the shield's lower edge or top in projection.

### Critic round 3 fixes (73.75)

- A short sagum (0.3 m, a dark lining), so the back hoops show below it; the sleeve edges in civ colour under
  the shoulder plates; arms thicker (bulk 1.42); a deeper cool madder tunic with a dark apron panel at the
  belt; the shield's inside bare planking. Civ colour 26 33 19 7 14 19 14 14, mean 18 %. Cell 88x76.

### Critic round 4 fixes (70)

- No cloak: the lorica's back hoops are the read from behind; civ colour on the shield field, crest, sleeve
  edges and the tunic's skirt: 30 31 23 14 14 19 18 26, mean 22 %. The hoops wrap the flanks
  (`cuirass_width` 0.26); arms back to bulk 1.3; the shield's top tipped 6° toward the viewer.

### Critic round 5 (78.6) fixes

- The black stripe at the chest and neck was the plate itself: metallic 0.9 at roughness 0.35 mirrored the
  dark sky. Now dull iron (`plate_metallic` 0.6, `plate_roughness` 0.55) and a darker base; the face a
  little lighter; the sword elbow bent ~30° with the fist at the hip; shoulder plates ~15 % smaller; a dark
  hem band. Civ colour 32 34 26 15 15 20 19 27, mean 23 %.

### Critic round 6 (78.5) fixes

- Shoulder plates back to round 5's size; the tunic sleeve cut shorter (`sleeve_x` 0.215); arms 1.33;
  the sword arm lower and bent, the blade forward at the hip; a dark hilt and pommel (sword `hilt`); the
  shield's back dark leather with a bronze boss bulge, a wooden grip bar and the bronze rim; lighter skin and
  plate. Civ colour 32 34 26 15 15 20 19 26, mean 23 %. Still: in direction 7 the blade shows above the
  board, in direction 2 it points down by the knee, and a red sleeve still shows below the plates in the back
  views.

## Final (round 6)

Scores (picture-only critics, two per round): R1 65/62 = 63.5; R2 73/61 = 67; R3 75/72.5 = 73.75;
R4 76/64 = 70; R5 80/77.25 = 78.6; **R6 81/76 = 78.5, final**; R7 82/71.5 = 76.75 (a fresh critic read the
edge-on shield with the blade across it in directions 2 and 7 as an archer drawing a bow). Rounds 6 and 7
did not beat 78.6 + 1.5: plateau. Round 6's entry and stance are restored; round 7's code stays, off by
default (sword `hilt`, `sleeve_x`, scutum `inside='leather'`: the dark leather back, boss bulge and grip).
Animations re-rendered from it. Pictures: reviews/realism/legionary/.

Open points:
- W / direction 7: the sword shows above the shield, and the hands meet at the board;
- the shoulders read as sleeves at round-6 size; round 7's banded caps (with `sleeve_x`) were better;
- the face is dark under the helmet in S/SE/SW: clean-shave him and lift the cheek guards;
- the shield's back is a plain plank (round 7's `inside='leather'` helped);
- the front-view civ colour is near 34 %;
- the shield is tilted edge-on in directions 2 and 7.
