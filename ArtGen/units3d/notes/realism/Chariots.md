# Chariots (realism pass): chariot, War Chariot, Three Man Chariot

Catalog names: `chariot` (lower case, Civ3's generic Chariot), `War Chariot` (Egypt), `Three Man Chariot`
(the Hittites). Code: chariots.py (car, team, reins, crew placement); crew dress in remaster.py
(`<unit>:crew<i>`). Batched critic rounds (PROCESS.md), 3-round cap.

## Design in words

**chariot.** A Late Bronze Age two-horse light chariot of about 1500-1200 BC (Mycenaean or Canaanite): a light
car with a low, open-backed breastwork of painted leather over a wooden frame, two spoked wheels, a pole
to a yoke on the horses' withers. One warrior stands in it with a thrusting spear and the reins in his
left fist. Must read at 1x: the two small horses, the big spoked wheels, the low painted car, the
upright spear. Civ colour: the car's outer leather panels, the tunic, the yoke pads and head plumes.

**War Chariot (Egypt).** The New Kingdom chariot of about 1450-1200 BC: very light, six-spoked wheels with
the axle at the very back of the car (the Egyptian signature), the car sides sloping down to an open
back, a bow case on the side. The archer wears the blue khepresh war crown (civ), a white kilt and a gold
broad collar, bare-chested, with the composite bow, and drives with the reins tied round his waist (as
the pharaoh's battle reliefs show). The horses wear tall plumes and fitted neck cloths.
Must read at 1x: the axle at the back, six spokes, the crowned archer, the plumed team.

**Three Man Chariot (Hittite).** The heavy Hittite chariot at Kadesh (1274 BC): a bigger car with the
axle at its centre to carry three men: a driver, a spearman and a shield-bearer, in scale shirts and
conical helmets, the team in scale trappers. Must read at 1x: three heads in the car, the bigger
car and eight-spoked wheels, the armoured team, the winged-sun emblem.

**Stock neighbours.** Horseman and Ancient Cavalry (the ancient mounted line); the War Elephant for size.

**Measured (tint.py, alpha>127, installed sheets).** chariot 24.2 % (18.1 / 20.9 / 17.4 / 17.0 / 25.5 /
50.0 / 26.4 / 18.5), War Chariot 33.3 % (27.0 / 21.9 / 25.7 / 27.3 / 36.9 / 62.1 / 37.6 / 27.8), Three Man
Chariot 34.1 % (26.6 / 26.8 / 26.1 / 23.8 / 39.5 / 67.7 / 38.0 / 24.7). The neighbours: Horseman 16.8,
Ancient Cavalry 8.6, Cavalry 19.0, Knight 30.8. The spike in direction 6 (from behind) is the inside of the
civ breastwork. Target: about the shipped amount in the side views with the rear spike gone (each 18-28 %,
even across directions).

**Footprint at 1x (foot.py: mean bbox over the 8 directions, alpha>127).** chariot 74.2 h x 50.9 w,
1592 px; War Chariot 68.1 x 57.0, 1922 px; Three Man Chariot 79.1 x 66.2, 2751 px; Horseman 56.4 x 39.1,
744 px; Ancient Cavalry 59.4 x 36.0, 953 px; War Elephant 70.4 x 55.0, 2371 px. The fit (render_vehicles)
matches each unit's box to the Civ3 original's, so the footprint stays near the stock by construction;
long poles are out of the fit (no_fit).

## Rounds
Render ~2-3 min per unit; drafts on the CPU lane, the sheet's renders on the GPU.

### Build rounds 1-5 (before critic batch 1)
Shared (chariots.py): the breastwork rebuilt: a dark wooden frame (CAR_FRAME) with the civ colour on an
outer leather skin only, inset from gold rails and posts, the sides sloping down to an OPEN back (props.loft
closes its rings, so the old car had a boarded, civ-coloured back: the 50-68 % spike from behind); lower
walls (WALL_K 1.55, the Three Man Chariot 0.85 of that); the one-horse shafts no longer drawn beside every
pole team (a for-else bug); the reins (REINS_R 0.02, 0.012 broke into specks) from each bit to the driver's
left fist, or round the Egyptian archer's waist; smaller, shorter-legged chariot ponies (HORSE_K 0.97,
leggy 1.0, neck 1.05); thicker quiver arrows. The diagonal lattice and mid rail dropped (toy box, specks).
- chariot: a 2.6 m thrusting spear (the crescent glaive broke into specks), kept in the fit (FIT_POLES: out
  of it the unit grew 28 % past the stock's footprint); the left fist forward on the reins; a civ tunic and
  kilt; sides 0.72 at the back.
- War Chariot: the axle at the back of the car (CAR_FWD 0.42); a composite bow held up in the raised left
  fist; the reins round the waist; a civ horse cloth and fan plumes (the fitted neck sleeves read as blue
  buckets); a painted civ bow case; a sleek light bay team (the dun read shaggy); sides 0.7 at the back.
- Three Man Chariot: the driver's fists forward on the reins; civ skirts on the three crew; sides 0.8.

Batch 1 sheet (GPU): D:\Dev\_handoff\realism\review\_batch\batch_chariots_1.png and _red.png.
| unit | tint per direction (mean) | stock | footprint 1x (h x w, area) | stock |
|---|---|---|---|---|
| chariot | 20.4 26.3 18.8 12.1 12.6 19.0 13.2 13.4 (17.0) | 24.2 | 72.2 x 52.0, 1609 | 74.2 x 50.9, 1592 |
| War Chariot | 17.9 17.0 18.1 15.4 13.9 13.7 12.8 15.0 (15.5) | 33.3 | 65.4 x 59.2, 1788 | 68.1 x 57.0, 1922 |
| Three Man Chariot | 21.8 29.3 23.6 14.3 15.6 19.1 15.9 14.0 (19.2) | 34.1 | 83.9 x 69.1, 2880 | 79.1 x 66.2, 2751 |
specks.py: chariot and War Chariot clean; Three Man Chariot dir 3 one 1-px piece (a spiked helmet's tip).

## Critic batch 1 (A / B, avg): chariot 73.5 / 72.5 = 73.0; War Chariot 75.0 / 74.5 = 74.75; Three Man Chariot 72.0 / 67.0 = 69.5
B: likely critical on the Three Man Chariot (1-3: the crew merge into one silver lump, bodies and shields
through each other). Civ colour fine (both). Shared: chrome metal; flat painted box panels; thin or missing
yoke and harness (the pairs blur); the first two teams the same brown with the same fans; thin weapons in 5-7.

### Batch 2 build rounds 1-4
Shared: matte painted-miniature metals: the car's own keys (ch_gilt, ch_sun, ch_bronze) instead of the shared
glossy 'trim'/'sun_gold', and _matte(): every metal material on the unit (trappers, crew armour, blades)
copied per unit at roughness >= 0.58, metallic <= 0.55. Panels: two civ-painted leather fields split by a
natural-leather band with bronze rosettes. A bowed yoke on the yoke saddles at the withers' height, the pole
rising to it; breast straps and girths (HARNESS); the pair wider apart (TEAM_X 0.5); lower, flexed necks
(NECK_RAISE -24, HEAD_FLEX 14) so the crew shows over them in 1-3; smaller ears (the tips were specks);
per-horse coats with more texture (coat_contrast 1.3); the reins start inside the head. Side kit per unit
and side (SIDE_KIT). New crew headgear registered from chariots.py into riders.HEADGEAR under new names
(riders.py untouched): ch_khepresh, ch_hittite, ch_crested.
- chariot: dark wooden six-spoked wheels with rawhide tyres; a bronze crested helmet and a leather corselet;
  a civ-painted hide shield hung on the left, a quiver on the right; a bay and a dark bay, no plumes;
  a thicker spear shaft (0.032).
- War Chariot: the tall khepresh flaring back, gold studs and the uraeus; the bow held out to the left side
  (thick 2.0); an ochre leather bow case angled on the right, a quiver on the left; a light bay and a flaxen
  chestnut.
- Three Man Chariot: the crew spaced (CREW_POS: driver front right, shield-bearer front left, spearman
  behind), the shield-bearer's sword dropped; Hittite bronze cone helmets with cheek guards and a long dark
  tail; the axle forward under the car's middle (CAR_FWD -0.12); matte bronze scale trappers with bigger rows
  (scale_period 0.075); a thicker spear.

Batch 2 sheet (GPU): D:\Dev\_handoff\realism\review\_batch\batch_chariots_2.png and _red.png.
| unit | tint per direction (mean) | footprint 1x (h x w, area) |
|---|---|---|
| chariot | 13.9 16.1 11.7 7.8 7.6 9.7 9.4 9.8 (10.8) | 70.9 x 53.1, 1589 |
| War Chariot | 16.5 19.1 16.2 13.1 10.8 9.8 11.0 13.4 (13.7) | 67.1 x 60.1, 1809 |
| Three Man Chariot | 20.7 28.2 20.4 14.0 13.6 20.9 15.6 15.2 (18.6) | 84.6 x 69.0, 2956 |
specks.py: Three Man Chariot clean; chariot and War Chariot dir 8 one 1-px piece each: an anti-aliased
ear-edge pixel at alpha 128 among 100-120 neighbours (threshold noise, not a floating part).

## Critic batch 2 (A / fresh, avg): chariot 81.5 / 77.0 = 79.25; War Chariot 79.5 / 68.5 = 74.0; Three Man Chariot 77.25 / 67.75 = 72.5
No critical failures. Shared: too much gilt; thin weapons with tiny heads; stiff upright crews; empty car
floors from behind; identical fans, dark horses merging. War Chariot: the khepresh overshot (a dunce cap).
Three Man Chariot: spiky "organ pipe" trappers, gold face masks, a car too big for three, one big blue side.

### Batch 3 build rounds 1-3 (the last round under the cap)
Shared: no gilt wheels (CAR_GOLD empty: matte wood, rawhide tyres on all three); rails light painted wood
(ch_border), posts and emblems a dull bronze (ch_gilt/ch_sun darker, rougher); tighter cars (DEPTH 0.78 /
0.78 / 0.95 over CAR_K; the Three Man Chariot 0.86 as wide); spears with bigger heads (1.7) and 0.04 shafts.
- chariot: the spear angled forward ready to thrust (upright_dir); a brighter bronze helmet with a tall
  horsehair crest seated on the dome (an arc on a stilt floated off it in 4 and 8); the hung shield a hide
  face with a civ ring, a rawhide rim and bronze boss; a bigger light-leather quiver tilted out; a bay and a
  chestnut.
- War Chariot: the khepresh ~35 % lower, bulbous and swept back; the bow raised forward and half drawn to
  the chest (draw 0.35), heavier arms (upper_bulk 1.3); a wider bow case tilted out; the archer 0.06 m
  forward in the car.
- Three Man Chariot: a bronze-brown trapper over the body with a civ border and a clean hem (a saddle cloth
  at cloth_frac 1.0: the scale barding's drape let the coat poke through its top edge, the spikes and organ
  pipes); no steel neck lames, face plates or plumes; bronze scale shirts; Hittite figure-of-eight hide
  shield (props.BUILDERS 'ch_eight', registered from chariots.py); a wooden spear angled forward; the side
  split into bays by natural-leather strips (STRIPS); the crew closer in the smaller car.

Batch 3 sheet (GPU): D:\Dev\_handoff\realism\review\_batch\batch_chariots_3.png and _red.png.
| unit | tint per direction (mean) | footprint 1x (h x w, area) |
|---|---|---|
| chariot | 13.1 16.3 13.3 7.1 6.8 9.9 7.2 7.3 (10.1) | 69.0 x 54.8, 1710 |
| War Chariot | 16.9 21.0 16.5 12.4 10.3 8.7 10.3 12.7 (13.6) | 66.1 x 59.9, 1860 |
| Three Man Chariot | 17.9 21.1 19.4 11.2 13.1 19.4 14.9 13.2 (16.2) | 83.5 x 69.5, 2956 |
specks.py: only 1-px anti-aliased edge pixels (chariot dirs 4 and 8, War Chariot 8, Three Man Chariot 4 and 8).

## Critic batch 3 (continuity / fresh, avg): War Chariot 86.0 / 85.0 = 85.5 PASS; chariot 86.25 / 80.0 = 83.1; Three Man Chariot 82.0 / 80.0 = 81.0
No critical failures. All three close on batch 3, their best round (3-round cap).

## FINAL
| unit | B1 | B2 | B3 | final |
|---|---|---|---|---|
| chariot | 73.5 / 72.5 = 73.0 | 81.5 / 77.0 = 79.25 | 86.25 / 80.0 = 83.1 | B3 (6487ce6d) |
| War Chariot | 75.0 / 74.5 = 74.75 | 79.5 / 68.5 = 74.0 | 86.0 / 85.0 = 85.5 PASS | B3 (6487ce6d) |
| Three Man Chariot | 72.0 / 67.0 = 69.5 | 77.25 / 67.75 = 72.5 | 82.0 / 80.0 = 81.0 | B3 (6487ce6d) |

Final sheets (shipped, final, Horseman, Ancient Cavalry): ArtGen/units3d/reviews/realism/<chariot | war-chariot |
three-man-chariot>/final.png and final_red.png (copies in D:\Dev\_handoff\realism\review\<unit>\).

fitcheck.py (1x bbox area and opaque pixels, final / shipped):
- chariot: bbox-area 1.02 (min 0.90 dir 1), pixels 1.07, ok
- War Chariot: bbox-area 1.01 (min 0.87 dir 6), pixels 0.97, ok
- Three Man Chariot: bbox-area 1.12 (min 1.04 dir 1), pixels 1.07, ok

Tint (tint.py mean): chariot 10.1 %, War Chariot 13.6 %, Three Man Chariot 16.2 % (shipped 24.2 / 33.3 / 34.1,
mostly the old boarded civ back; within the Horseman 16.8 - Ancient Cavalry 8.6 range, kept by the lead).
specks.py: only 1-px anti-aliased edge pixels.

### Later-pass lists (the critics' last open points)
- chariot: a bright bronze dome with a narrower ridge crest, warmer brown or smaller (the crest reads as a
  black blob or hat at 1x in 1, 3, 4, 8); the warrior's torso ~15 % thicker; edging on the car side in E and W.
- War Chariot: softer elbow and wrist joints (1-3); the bow drawn in 2 as well; the near horse's pale mane
  flares in NE; the horses a touch longer in the leg; plumes different from the chariot's.
- Three Man Chariot: split the single blue car side with a leather band or a quiver (1-3); stagger the crew so
  the three helmets show separately (1, 4, 8, N/NE/NW); the spear ~20 % higher; the shield turned outward so it
  reads in E, W, NE. Its trapper is a civ-bordered bronze-brown cloth: a scale coat with visible rows needs the
  shared barding drape to clear the coat (mounted.drape's gap), a mounted-owner change.

Finals gate (D:\Dev\_handoff\realism\scratch\chariots\gate_all.sh, verify_ch.sh): the 9 mounted finals and the 3 chariots re-rendered at HEAD all reproduce their final.png (mean <= 1.8e-7, max 0.008, no pixel off by 0.1).
