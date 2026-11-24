# Man-O-War (realism pass): design

The Man-O-War is a 74-gun third-rate ship of the line of c. 1790-1815:
~53 x 14.5 m (L:B ~3.6), a tall two-decker, dark warm brown with two
ochre bands through two rows of black gunports, a galleried, lit stern
with quarter galleries, a gilt figurehead and head rails, three masts with
courses, topsails and topgallants, a four-sided spanker, jibs, dark tops
and standing rigging, long civ pennants at the mastheads, the civ lion
rampant on the courses and topsails. Neighbours (stock): Frigate and
Ironclad.

## Baseline (as shipped)
Tint (tint.py, DEFAULT): 13.1 / 11.8 / 13.3 / 8.0 / 9.4 / 8.7 / 8.8 / 7.1 %
(mean 10.0). Values: mean 107, p10 31, std 63, upper 135 / lower 95.
Footprint 10428 px.

## What must read at 1x (the Frigate's and Galleon's lessons)
The tall dark hull with two chequer bands (a two-decker, not the
frigate's one); one smooth belly on every sail, no gaps between tiers,
30 deg bracing; the fleet sail-shape rule (straight edges along the yard,
leeches and foot; the belly only mid-cloth; the emblem in the flat
centre); long civ pennants; the gilt figurehead and the lit stern gallery.

## Rounds
New builder ships.man_o_war_74 in its own block (after the Privateer's),
derived once from the Frigate's C3 code (nav_gen_mow) and edited by itself
since; its own sail helper _mw_sail (the shared _square_sail untouched);
BUILDERS['Man-O-War'] set after the dict. Block check: one per unit.
- Round 1: the two-decker (L 3.2, B 0.88, D 0.36, bands at deck - 0.17 to
  - 0.08 and - 0.33 to - 0.24, 14 ports a row). Tint 2.8 % (the lion
  small); the sails' heads and feet curled.
- Round 2: _mw_sail (straight edges, belly 0.24 mid-cloth, reef bands);
  the lion 0.78 h on courses and topsails. Footprint 12038 (+15 %).
- Round 3: the lion 0.95 h, fit_scale 0.93. Sent to critic round 1. Tint
  per direction (columns 1-8): 10.9 / 11.9 / 6.3 / 5.4 / 6.3 / 3.7 / 2.8 /
  7.5 % (mean 6.8). Values: mean 109, p10 36, p50 109, p90 182, p99 240,
  std 59, upper 144 / lower 86. ppm 47.78; footprint 10339 (stock
  10428). specks.py: clean.

## Critic round 1: 67.4 (A 67, B 67.75), no critical failure
Both: it reads as "a Frigate with more sails". Fleet findings: staggered
bracing per mast (fore ~35, main ~25, mizzen ~30), emblems clean (not
stair-stepped), the belly well inside the edges. Fixes: the hull ~1.3x
the Frigate's height with more tumblehome, masts ~10-15 % taller (the
footprint within ~5-8 %); one clean device (a cross or band) on the main
course and topsail; darker, thicker shrouds and stays; the twisted mizzen
topsail in 8; a beakhead and gilt figurehead in 1/2/4, quarter galleries.

- Round 4: D 0.36 -> 0.44, tumblehome 0.2; course heads 1.66 / 1.86 /
  1.62 (~12 % taller); braces fore 35, main 25, mizzen 30; the sail mesh
  twice as fine (72 x 48: cleaner emblem edges), a belly shape squared to
  keep it off the edges; a civ cross on the main course and topsail;
  shrouds r 0.024 in a darker rope (#1e1810); a beakhead platform and a
  bigger gilt figurehead. Tint 1.9 % (the cross thin).
- Round 5: a bolder cross; a civ stripe along the upper gun band; a civ
  ensign at the stern. Tint 3.4 %.
- Round 6: the cross on the fore course too, the stripe wider. The
  squared belly made bright domes in 2.
- Round 7: the belly back to (1 - u^2) sin(pi v) (straight edges, no
  domes). Sent to critic round 2. Tint per direction (columns 1-8): 7.5 /
  4.6 / 5.5 / 5.8 / 6.1 / 3.6 / 4.1 / 7.0 % (mean 5.5; stock 10.0). Values:
  mean 106, p10 32, p50 105, p90 179, p99 232, std 58, upper 140 / lower
  80. ppm 45.4; footprint 10127 (stock 10428). specks.py: clean.

## Critic round 2: 71.5 (continuity 70.75, fresh 72.25): gain (+4.1)
Both: the hull now strong, the ship above the Frigate. Fixes: the square
sails curl into twisted scrolls in 3/4/7/8 (find the cause; each sail one
flat-edged, bellied face square to its yard, braced ~25-30); bigger
yards, courses and topsails (canvas to match or beat the Frigate's),
topgallants and jibs that read; one complete cross on the main topsail
face; a visible staff for the stern flag; a brighter, bigger figurehead
and a light rail across the beakhead.

The cause of the "scrolls": no twist in the code (each sail is one mesh
rotated about its own mast by one angle); the belly (0.24 of the yard's
length, out of a flat head and foot) seen obliquely from the high camera
draws the leeches as curled edges. Halving the belly flattens it.

- Round 8: belly 0.24 -> 0.12; courses 1.42 x 0.62 and 1.74 x 0.7, topsails
  1.18 x 0.56 and 1.44 x 0.64, topgallants 0.84 / 1.0 wide, the mizzen
  topsail 1.1; braces 30 / 26 / 28 (fore, main, mizzen: close, no
  twist); the cross on the main topsail only; the ensign staff r 0.02
  standing above the flag; the figurehead 0.08 x 0.15 in a brighter gold
  (#dcb456), a light rail across the beakhead. Sent to critic round 3
  (the last). Tint per direction (columns 1-8): 4.3 / 1.7 / 3.9 / 4.6 /
  4.0 / 3.4 / 3.9 / 4.7 % (mean 3.8; stock 10.0: the device now on one
  sail). Values: mean 111, p10 33, p50 116, p90 179, p99 207, std 57,
  upper 143 / lower 85. ppm 44.23; footprint 10146 (stock 10428).
  specks.py: clean.

## Critic round 3 (batch naval 1): 70.1 (A 72.75, fresh 67.5): below C2, cap reached

## FINAL: critic round 2 (own round 7, commit 65d10a56)
The Man-O-War's block restored to 65d10a56's (byte-identical; block check
one per unit). The re-render matches critic 2's sheets: final.png mean
abs diff 0.000003, max 3/255 (one pixel over 2/255); final_red.png max
2/255. Tint per direction (columns 1-8): 7.5 / 4.6 / 5.5 / 5.8 / 6.1 / 3.6
/ 4.1 / 7.0 % (mean 5.5). ppm 45.4; footprint 10127 (stock 10428). Finals
in ArtGen/units3d/reviews/realism/man-o-war/ and the handoff folder (rows:
shipped, final, Frigate, Ironclad). Added to the finals gate.

Critic history (average; A / fresh):
- C1 67.4 (67 / 67.75)
- C2 71.5 (70.75 / 72.25) = FINAL
- C3 70.1 (72.75 / 67.5); cap

Open points: the sails' belly/billow (C3's halved belly is in the
scratchpad) and the cross breaking across stacked sails in 3/7; the ship
should clearly outsize the Frigate; the stern and figurehead reading at 1x;
civ colour under the stock's (5.5 vs 10.0 %).

Fit fix (draw size only; geometry, materials and civ colour unchanged): fit_scale 0.93 -> 1.09 in MOW.
The three tall masts in the fit box had shrunk the hull. fitcheck bbox-area ratio 0.88 -> 1.20 (the taller
sail plan now draws above the stock's); side view (dirs 4/8) 1x width shipped 112, before 97, after 114; hull
alone (no bowsprit) about 101 / 83 / 98 px. specks.py clean. Finals re-rendered at HEAD (ppm 53.21);
realism/naval finals gate (11 ships) PASS.
Fit fix, step 2 (proportion toward stock; re-render only, no critic round): rig_scale 0.78 (new option in this
unit's builder, default 1.0 = unchanged): the three masts, their yards and sails and the spanker scaled about each
mast's foot, so the sail plan keeps its shape. The pennants keep their size at the mastheads. fit_scale 1.09 ->
1.0005 holds the hull's ppm at 53.21, so the hull length is unchanged: about 98 px at 1x (shipped 101). Side-view
height at 1x: shipped 56, step 1 77/76, now 63/63. bbox-area ratio 1.20 -> 0.99. specks.py clean; realism/naval
finals gate (11 ships) PASS.
