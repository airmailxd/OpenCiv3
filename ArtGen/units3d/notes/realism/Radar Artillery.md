# Radar Artillery (realism pass): design

The Radar Artillery is an M270 MLRS (1983-): the M993 carrier (a Bradley-
derived chassis: six road wheels, front sprocket, return rollers), the
armoured three-man cab forward with its sloped glazed front and louvred
armour shutters, and the launcher-loader module behind it: a big box
holding two pods of six rockets each, the pods split by a centre seam,
ribbed framing on the sides, the twelve tube ends in two 3 x 2 grids on
its front face, the loader booms on its top. Built as a painted 1/56
tabletop miniature in the house sand with its own camo. Neighbours for
the comparison: Mobile SAM and Mech Infantry (the new finals; the
Bradley is its chassis' parent).

## Baseline (round 0, as shipped)
- A tan box on tracks with a big flat-topped box behind a short cab; a
  large civ triangle on each side and the top (2.4-3.6 %, mean 3.1);
  reads as "a truck with a crate". Values (values.py): mean 128, p10 36,
  std 59, upper 160 / lower 100.

## Real proportions (M270)
6.85 x 2.97 m, 2.6 m high (launcher stowed), the cab ~1.7 m long, the
launcher box ~4.2 m long and ~0.9 m tall on the deck. At ~3.6 m/unit:
L 1.9, W 1.0 (as the roster), deck ~0.33, box top ~0.72, cab roof ~0.66.

## What must read at 1x (with the four vehicles' lessons from the start)
1. The launcher box as the dominant mass, in its own (darker) value,
   with the dark centre seam, the side ribs, and the twelve tube mouths
   (dark bores with light rims) on its front face: the weapon reads head-
   on; the box raised a few degrees off the deck so it reads as a
   launcher, not a crate.
2. The cab forward: lower than the box, a sloped front with a dark
   windscreen band, a darker roof with a hatch, headlights: the front
   identity reads at 1x.
3. Running gear: six road wheels with dark hubs, front sprocket, rear
   idler, return rollers, dark track; less pale dust.
4. Values: the light-top / dark-bottom split (aim upper - lower >= 30),
   camo on every big plane including the roofs, a lit edge on the box's
   top rim, a shadow gap under the box.

## Civ colour
Stock Radar Artillery 2.4-3.6 %; Mobile SAM final 2.3-3.9; Mech Infantry
final 2.2-3.8. Aim 2.5-4 %: an inverted-V chevron on each box side and
on its rear, on each cab side, one on the cab front.

## Rounds
Tint per direction (compare columns 1-8); values from values.py.

- Round 1 (14 s): the new model (a rewritten _mlrs): the cab forward with
  a dark windscreen band, a darker roof and a dark hatch; the launcher box
  in the darker paint raised 5 deg on its cradle (a dark gap under it),
  the centre seam, side ribs, a lit top rim, the twelve tube mouths
  (light rims, dark bores) on its front, the loader booms; fender lips
  over a dark shadow, dark hubs, little dust. Reads as an MLRS at 2x and
  1x. Civ small: 0.4-1.8 %. Values: mean 106, p10 31, std 55, upper 127
  / lower 86 (the light-top/dark-bottom split, from the start).
- Round 2 (14 s, waited 45 s): bigger chevrons (box sides 0.24, cab 0.15,
  rear 0.2, front 0.13) and one on the box's roof (the stock's top mark).
  Tint 1.9-4.8 %.
- Round 3 (14 s): seams with a light bevel on the hull sides, a box behind
  the cab on each side, front chevron 0.16. Sent to critic round 1
  (neighbours: Mobile SAM final and Mech Infantry final). Tint: 3.7 /
  2.2 / 3.6 / 4.3 / 4.8 / 4.3 / 4.8 / 4.3 % (mean 4.0). Values: mean 105,
  p10 30, std 55, upper 127 / lower 85. ppm 57.30 (the box is in the fit,
  as the original's box is the unit's silhouette).
No shared code changed.

## Critic round 1: 62.4 (continuity 59.5, fresh 65.25), CRITICAL: unclear identity
Both: the tube grid only shows in 1-3; from the side and the rear the
launcher is a smooth pale-rimmed box ("a crate on tracks").
Orientation check: on a travelling M270 the launcher-loader module sits
with the pods' firing ends toward the REAR of the vehicle (the rear view
shows the two 2 x 3 grids of tube covers; the cab end of the module is
the pods' closed back). Source: my knowledge of M270 reference photos
and the stowed configuration; not re-verified on line in this session.
Fixes:
1. The twelve tube mouths moved to the box's rear face, a dark gap
   between the pods there; the cab-end face braced (a dark frame and a
   cross per pod); the rear chevron moved to the hull's rear plate.
2. The box on a mount: a dark turntable, two cradle rails and the pivot
   bar under it; each side split into two pods (a dark vertical gap, a
   dark horizontal frame) over the ribs.
3. The pale rim gone: a dark band round the box's foot, the lid in the
   lighter hull paint over the darker sides (shading, not an outline).
   Cab: louvres over the windscreen, grab rails, mirrors; a louvred nose
   grille. Civ smaller (box sides 0.2, top 0.17, rear 0.12, cab 0.14,
   front 0.14).

- Round 4 (14 s): 1-3 without the lid: the box all dark, values flat
  (upper 111 / lower 79, std 48).
- Round 5 (14 s, waited 40 s): the lid in the hull paint. Sent to critic
  round 2. Tint per direction (columns 1-8): 2.7 / 1.5 / 2.5 / 3.1 / 2.8 /
  1.9 / 2.8 / 3.1 % (mean 2.5). Values: mean 108, p10 31, std 61, upper
  138 / lower 80 (stock 160 / 100). ppm 56.70. No shared code changed.

## Critic round 2: 71.1 (continuity 66.25, fresh 76)
1. Floating specks: the mirror arms (6 mm rods) and their small mirrors
   at the cab corners didn't resolve at 1x: removed. The loader booms on
   the lid (read as rails) removed.
2. Mount: the box raised 0.13 on a dark mount block with a lit turntable
   ring, tilted 6 deg: a dark band under it in every direction and a
   shadow gap under its front.
3. Two pods: two lids with a dark gap between them, two dark straps across
   each and hoist points at the corners; the side seam 50 mm; the cab
   hatch with a lit rim and five vision blocks round it.
   Rear: the tube faces dark ('deck_dark') behind dark-rimmed mouths.

- Round 6 (14 s): all in. Sent to critic round 3. Tint per direction
  (columns 1-8): 2.6 / 1.5 / 2.5 / 2.9 / 2.7 / 1.7 / 2.7 / 2.9 % (mean
  2.4). Values: mean 106, p10 29, std 63, upper 137 / lower 77. ppm
  56.22. No shared code changed.

## Critic round 3: 72.6 (continuity 74.25, fresh 71.0), no critical failure
1. Launcher cues from the front: the loader boom with its hoist housing
   along the box's top front edge (dark, a mid-tone top line), a lifting-
   arm rail and a cable run along each side, the tube ends standing 50 mm
   proud of the rear face (they show at the corners in 1/3/5/7).
2. Cab taller (0.26 -> 0.34): three framed panes (a light frame) under
   the louvres on the angled front, the side doors outlined.
3. Box sides shaded: a darker lower third, a lit top line, the pod seams
   and frame kept.
   Mount (the critics contradict: kept, given structure): the mount block
   in the hull's second paint with a lit top edge and a dark foot (no
   void), the box lowered (lift 0.13 -> 0.1).

- Round 7 (14 s): all in. specks.py: clean (1 piece per direction). Sent
  to critic round 4. Tint per direction (columns 1-8): 2.3 / 1.3 / 2.3 /
  2.6 / 2.5 / 1.8 / 2.6 / 2.6 % (mean 2.3). Values: mean 107, p10 30, std
  63, upper 134 / lower 82. ppm 56.31. No shared code changed.

## Critic round 4: 79.5 (continuity 78.75, fresh 80.25), no critical failure
1. Box sides: the pod seam wider (70 mm), a lit edge under the frame
   rail, two hoist cylinders a side down at the cradle, the lower third
   darker (#86704e).
2. Cab front: two louvres (not three), light mullions and side frames
   over them (three panes read separately), a dark gap behind the cab's
   top against the launcher's front face; the hatch ring and vision
   blocks kept.
3. The box's front face: a dark armoured panel per pod with four bolts
   and a latch (the X braces gone); the rear: a light frame round each
   pod's tube face, the bores 30 mm deep.
   The box's tilt 6 -> 2.5 deg (it read as nose-up in 4 against 8);
   head-on and rear chevrons a little bigger (0.17 / 0.16).

- Round 8 (14 s): all in. specks.py: clean. Sent to critic round 5. Tint
  per direction (columns 1-8): 2.6 / 1.9 / 2.5 / 2.7 / 2.8 / 2.2 / 2.8 /
  2.7 % (mean 2.5). Values: mean 109, p10 31, std 63, upper 137 / lower
  83. ppm 56.66. No shared code changed.

## Critic round 5: 80.4 (continuity 80.5, fresh 80.25): +0.9, no gain
1. Cab armoured: 0.34 -> 0.3 tall, the panes shorter (0.75), their frames
   in the hull's second paint (no near-white), a shutter over each pane's
   top third with a dark slat, no louvres; no mirrors.
2. Flat surfaces: the hull side band above the fender darker in its lower
   half with a lit deck edge; bigger support rollers (r 0.038); the lids'
   raised side rims.
3. The launcher from 1-4 and 8: the pod-end frames standing 40 mm proud
   (they show from the side), a vertical hoist boom at each rear corner,
   tie-down cleats along the box's foot, a pivot trunnion at its rear foot.
   Running gear: the front sprocket raised (z 0.19, r 0.085, 12 teeth).
   Civ: the cab-side chevrons dropped; one bold chevron per box side
   (0.24), one on top, the head-on and rear marks kept.

- Round 9 (15 s): all in, but the panes sat behind their frame (only a
  faint outline showed): pane_set back to 0.
- Round 10 (15 s): specks.py clean. Sent to critic round 6. Tint per
  direction (columns 1-8): 2.6 / 1.8 / 2.5 / 2.6 / 2.7 / 2.2 / 2.7 / 2.6 %
  (mean 2.5). Values: mean 109, p10 30, std 61, upper 134 / lower 85.
  ppm 56.54. No shared code changed.

## Critic round 6: 77.6 (continuity 82.0, fresh 73.25) -> plateau

## FINAL: critic round 5 (own round 8, commit 39e8e739)
Two critic rounds (5, 6) without beating the best by 1.5: plateau. The
Radar Artillery's builder and spec are restored from 39e8e739 (the other
units' code untouched); the re-render matches critic 5's sheets exactly
(mean abs diff 0, max 0). final.png / final_red.png are critic 5's (rows:
shipped, final, Mobile SAM final, Mech Infantry final). Tint per
direction (columns 1-8): 2.6 / 1.9 / 2.5 / 2.7 / 2.8 / 2.2 / 2.8 / 2.7 %
(mean 2.5). ppm 56.66.

Critic history (average; continuity / fresh):
- C1 62.4 (59.5 / 65.25; critical: unclear identity)
- C2 71.1 (66.25 / 76)
- C3 72.6 (74.25 / 71.0)
- C4 79.5 (78.75 / 80.25)
- C5 80.4 (80.5 / 80.25) = FINAL
- C6 77.6 (82.0 / 73.25); plateau

Open points for a later pass:
- From 1-4 and 8 it still reads partly as a cargo box: the launcher cues
  there are subtle.
- The cab: C6's armoured look (short deep-set panes in hull-coloured
  frames under slatted shutters, commit 6ce13bfd) worked and is worth
  carrying over, but without C6's near-white pod-end frames.
- The sprocket and the top rollers don't show at 1x.
- The cab's camo is too busy at 1x.

## The vehicles family: reproduction check (after the final restore)
All six vehicle finals re-rendered in one run with the current armor.py
and compared, DEFAULT sheet and tint layer, with each unit's recorded
final (mean abs diff / max, of 255): Tank 0.000003 / 1, Panzer 0.000017
/ 2, Modern Armor 0 / 0, Mech Infantry 0.000015 / 2, Mobile SAM 0.000004
/ 1, Radar Artillery 0.000004 / 1; tint layers identical. Nothing drifts.
specks.py on the finals: Tank, Panzer, Modern Armor and Radar Artillery
clean; Mech Infantry 2-3 px pieces in directions 3 and 5, Mobile SAM a
17-21 px piece in directions 1/3/4/8 (both passed or were finalised
before the checker existed; see the report). Family contact sheet:
D:\Dev\_handoff\realism\review\_family\vehicles.png (+ _red), shipped
above final, roster order.
