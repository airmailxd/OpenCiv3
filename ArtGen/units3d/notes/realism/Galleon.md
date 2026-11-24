# Galleon (realism pass): design

The Galleon is a Spanish galleon of the Armada (c. 1588, the San Martin's
type): ~38 x 9.5 m (L:B ~4), longer and lower than the carrack, a low
forecastle set back from the bow over a long beakhead, a tall two-tier
aftercastle with a galleried, gilt-windowed transom, two rows of lidded
gunports, fore and main with courses and topsails and fighting tops, a
lateen mizzen, standing rigging. Neighbours (stock): Carrack and Frigate.

## Baseline (as shipped)
Tint (tint.py, DEFAULT): 9.4 / 8.1 / 9.0 / 6.5 / 7.5 / 7.2 / 7.3 / 6.5 %
(mean 7.7). Values: mean 98, p10 34, std 58, upper 125 / lower 86.
Footprint 8526 px.

## Real proportions
L 3.0 = 38 m: B 0.78, D 0.24 plus a 0.07 bulwark, sheer 0.4 stern / 0.26
bow; aftercastle 0.24 / 0.48; forecastle 0.16 (set back); beakhead 0.36
long; main course 1.32 x 0.72, topsail 0.8 x 0.38; fore 1.0 x 0.56 and
0.6 x 0.32; lateen mizzen yard 1.3.

## What must read at 1x (the fleet's lessons; the Carrack's critic 1)
The dark walnut hull with three black wales and two rows of ports; the
high stern with the gilt transom windows and gallery; the long beakhead
under the bowsprit (galleon, not carrack); square yards braced 32 deg so
the profiles show sail faces; dark fighting tops; dark standing rigging.

## Civ colour (stock 7.7 %)
Two bold civ stripes on each course; a civ pennant at the main truck.

## Rounds
New builder ships.galleon_armada in its own block, derived from the
Carrack's (own names); BUILDERS['Galleon'] set after the dict. No other
unit changed.
- Round 1: the galleon. Tint 5.9 % (the stock stripes, 0.14-0.4 h).
- Round 2: wider stripes ('stripes_wide', 0.1-0.44 h). Sent to critic
  round 1. Tint per direction (columns 1-8): 12.2 / 15.1 / 4.0 / 4.9 /
  9.3 / 10.0 / 2.5 / 6.9 % (mean 8.1). Values: mean 91, p10 31, p50 77,
  p90 168, p99 219, std 54, upper 123 / lower 73. ppm 52.85; footprint
  8494 (stock 8526). specks.py: clean.

## Critic round 1: 57.0 (A 56.5, B 57.5), CRITICAL (identity, both)
A small caravel or lesser carrack, a step down from the Carrack, a squat
rig, the masts bare in 3/4/7. Fleet rule update: brace ~18-22 deg with
real belly (32 deg left 3 and 7 edge-on). Fixes: ~10 % bigger
(footprint ~9400), masts ~25 % taller with bigger topsails and belly,
heavier dark tops and shrouds; the aftercastle ~2x the waist's freeboard,
a beakhead that reads at 1x; two clear rows of lighter-framed ports
between raised lighter wales; civ ~10 % in 1/2/5/6.

- Round 3: course heads at 1.52 (fore) and 1.9 (main), courses 1.06 x 0.6
  and 1.38 x 0.78, topsails 0.76 x 0.42 and 1.0 x 0.5; mizzen top 1.5,
  yard 1.45; brace 20, belly 0.26; tops r 0.165, shrouds and stays r
  0.018-0.02; aftercastle 0.3 / 0.6; beakhead 0.5 long and wider; seven
  ports a side in two staggered rows from y -0.8 to 0.8, each dark port in
  a light frame, three raised light wales (#8a6436) between and over
  them. fit_scale 1.12: footprint 10575.
- Round 4: fit_scale 1.06, stripes back to 0.1-0.44 h. Sent to critic
  round 2. Tint per direction (columns 1-8): 10.5 / 12.6 / 7.1 / 3.9 / 7.8
  / 11.3 / 4.4 / 5.4 % (mean 7.9). Values: mean 98, p10 36, p50 89, p90
  171, p99 228, std 54, upper 130 / lower 80. ppm 51.53; footprint 9368.
  specks.py: clean.

## Critic round 2: 67.6 (continuity 66.75 with a CRITICAL: the side-view rig reads as bare masts; fresh 68.5): gain (+10.6)
The gun deck landed. Bracing rule v3 (30 deg, deep belly, courses the
biggest, mizzen lateens smaller). Fixes: the main course the biggest
shape in 4/8; the mizzen lateen ~60 % with its yard inside the stern;
shrouds and stays between the masts; the hull ~15 % longer (it was ~85 %
of the Carrack's length at 1x) with the masts raised to match; the
beakhead clearly below the bowsprit in 8; one civ band per course, none on
the topsails (~10 %).

- Round 5: L 3.0 -> 3.45, B 0.82, sheer 0.44 / 0.28; fore at y 0.95 (head
  1.66, course 1.16 x 0.66), main head 2.06 (course 1.52 x 0.86); mizzen
  yard 0.9, drop 0.46, top 1.3, at y -0.92; brace 30, belly 0.3; one civ
  band (|x| < 0.17 h) per course; the beakhead lower (deck - 0.11) and
  longer (0.58); a mizzen stay. ppm fell to 44 (footprint 7926), tint
  4.1 %, a pennant speck in 2.
- Round 6: fit_scale 1.1, band 0.26 h.
- Round 7: the pennant shorter and under the truck. Sent to critic round
  3. Tint per direction (columns 1-8): 10.1 / 10.3 / 4.1 / 4.8 / 5.1 / 7.0
  / 2.2 / 5.8 % (mean 6.2). Values: mean 97, p10 37, p50 88, p90 167, p99
  228, std 52, upper 129 / lower 79. ppm 48.94; footprint 9526.
  specks.py: clean.

## Critic round 3: 61.9 (continuity 71.25, fresh 52.5): no gain (1 of 2)
Both: the hull reads light ochre/tan at 1x, the rig thin next to the
Carrack and Frigate. Fixes: a dark brown to near-black hull below the
wales, mid-tone wales, two rows of dark ports; a dark stern gallery band
with lit windows and a stepped quarter rail; topsails ~25 % bigger, a
spritsail, fighting tops that read, the fore course braced a little more
so the courses don't stack in 3/7.

- Round 8: wood #3a2614 / #32200f, below the wales #22160a; wales and
  port frames mid-tone (#6e5030); a dark band behind the transom windows,
  the windows lit (#e0b860); a stepped quarter rail; topsails 1.0 x 0.52
  and 1.28 x 0.62; a spritsail under the bowsprit; tops r 0.2; fore yard
  36 deg, main 30.
- Round 9: castle sides darker (#46301a). Sent to critic round 4. Tint
  per direction (columns 1-8): 8.3 / 7.8 / 3.6 / 4.8 / 4.5 / 6.4 / 1.6 /
  5.6 % (mean 5.3). Values: mean 98, p10 34, p50 86, p90 171, p99 228,
  std 55, upper 132 / lower 78 (stock 98, 125 / 86: the lower half, the
  hull, now darker than the stock's). ppm 47.45; footprint 9611.
  specks.py: clean.

## Critic round 4: 65.75 (continuity 78, fresh 53.5 with a CRITICAL: house style, lighter and smaller than the Carrack and Frigate): no gain (2 of 2) -> plateau

## FINAL: critic round 2 (own round 4, commit e805885d), a low final (67.6), flagged to the lead
The Galleon's block restored to e805885d's (byte-identical; block check one
per unit). The re-render matches critic 2's sheets: final.png and
final_red.png mean abs diff 0.000001, max 1/255. Tint per direction
(columns 1-8): 10.5 / 12.6 / 7.1 / 3.9 / 7.8 / 11.3 / 4.4 / 5.4 % (mean
7.9). ppm 51.53; footprint 9368. Finals in ArtGen/units3d/reviews/realism/
galleon/ and the handoff folder (rows: shipped, final, Carrack, Frigate).

Critic history (average; continuity / fresh):
- C1 57.0 (A 56.5 / B 57.5; critical: identity, a small caravel)
- C2 67.6 (66.75 / 68.5) = FINAL
- C3 61.9 (71.25 / 52.5)
- C4 65.75 (78 / 53.5; critical: house style); plateau

Later-pass list (the lead's own look at C4 and the critics):
- The hull still reads mid-brown at 2x and 1x, not near-black (C4's
  darker woods were not enough under this light).
- The ship is visibly shorter than the stock Carrack at 1x (the C3 hull
  +15 % is not in the final).
- The two port rows read weakly at 1x.
- (C3/C4 tried: rule-v3 bracing, a small mizzen, the lit stern gallery,
  a spritsail, bigger topsails; the code is in the scratchpad.)
