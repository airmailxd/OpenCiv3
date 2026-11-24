# Privateer (realism pass): design

The Privateer is an armed privateer brig of c. 1780-1812 (the American
and French commerce raiders): ~27 x 8 m (L:B ~3.4), a sleek, low black
hull with a narrow grey band through one row of gunports (black lids), a
low quarterdeck, a galleried stern; two masts square-rigged with courses,
topsails and topgallants and a gaff mainsail abaft the main, a long
bowsprit with jibs; standing rigging; the death's head in the civ colour
on the courses and topsails. Neighbours (stock): Frigate and Galleon.

## Baseline (as shipped)
Tint (tint.py, DEFAULT): 12.4 / 7.1 / 11.6 / 9.4 / 9.5 / 5.5 / 9.6 / 9.8 %
(mean 9.3). Values: mean 95, p10 29, std 58, upper 123 / lower 80.
Footprint 6624 px.

## What must read at 1x (the fleet's lessons, bracing rule v3)
A two-masted brig, not a three-masted frigate: the black hull with the
pale band and black ports; courses the biggest sails, braced 30 deg with
deep belly (0.3) so 4 and 8 show the faces and the skulls; the gaff
mainsail kept small; dark tops and shrouds.

## Rounds
New builder ships.privateer_brig, generated from the Frigate's block
(nav_gen script: own names), in its own block after it;
BUILDERS['Privateer'] set after the dict. No other unit changed (block
count checked: one per unit).
- Round 1: the brig at 20 deg bracing: the gaff mainsail and the jibs
  dominated the profile; the skull small (tint 3.5 %).
- Round 2: bracing rule v3 (30 deg, belly 0.3); skull 1.08x on courses
  and topsails; gaff mainsail smaller (1.0 x 0.6); the band greyer. The
  pennant's tip a speck in 2. (A splice-script slip duplicated the block;
  fixed, block count 1.)
- Round 3: the pennant set under the truck. Sent to critic round 1. Tint
  per direction (columns 1-8): 13.4 / 13.4 / 6.9 / 4.5 / 8.6 / 7.8 / 2.9 /
  8.5 % (mean 8.2). Values: mean 102, p10 30, p50 101, p90 176, p99 232,
  std 57, upper 132 / lower 86. ppm 53.07; footprint 7161 (stock 6624).
  specks.py: clean.

## Critic round 1: 58.25 (A 55 with a CRITICAL: house style, a pale barge deck, no ports at 1x, a sparse rig; B 61.5)
Shares the Frigate C1's hull problems: reuse the Frigate C2 hull fixes.
Fixes: a dark deck with bulwarks, guns and clutter; one clear row of black
lidded ports on a buff stripe; a sharper, longer bow with a cutwater;
stern lights with a quarter rail; masts ~20-25 % taller, wider yards; a
long bowsprit and jib-boom to two clear jibs; a larger death's-head flag at
the main masthead (civ colour in 3 and 7).

- Round 4: re-derived from the Frigate's C2 block (nav_gen_privateer2:
  dark brown hull, darker deck with a boat, capstan and carronades, a
  0.1 bulwark with a dark rail, lit stern lights and quarter galleries,
  head rails, the thicker bowsprit to yb + 0.72, jibs), then made a brig
  again: masts ~20 % taller (course heads 1.3 / 1.44, main course 1.2 x
  0.56), the gaff mainsail four-sided abaft the main; nine ports a side
  in a buff band; a sharper bow (bow fullness 1.2, rake 0.45); a civ
  death's-head flag (0.36 x 0.26, the skull in pale cloth) at the main
  masthead, the pennant on the fore. Brace 30, belly 0.3. Sent to critic
  round 2. Tint per direction (columns 1-8): 14.2 / 14.2 / 6.9 / 5.1 /
  9.1 / 8.9 / 2.8 / 9.0 % (mean 8.8). Values: mean 104, p10 30, p50 107,
  p90 182, p99 227, std 60, upper 137 / lower 84. ppm 48.84; footprint
  7412 (stock 6624). specks.py: clean.

## Critic round 2: 69.5 (continuity 72, fresh 67): gain (+11.25)
Fixes: skulls off the topsails and angled sails (one big skull per
course, the death's-head flag larger and turned to show its face in 2, 6,
7, a long pennant); the rig ~1.5x the hull's length (masts ~15 % lower);
the hull lifted to the stock's warm mid-brown with a lighter wale; gun
muzzles along the port strake; bracing 30-35 deg. Fleet sail rule:
straight edges, the belly mid-cloth, the emblem in the flat centre.

- Round 5: own sail helper _pv_sail (the rule; the shared _square_sail
  untouched); masts ~14 % lower (course heads 1.12 / 1.24, sails ~0.9x);
  the tiers closed up; brace 33, belly 0.24; skulls on the courses only;
  the flag 0.48 x 0.34 streaming out at 45 deg; the pennant 0.46 long on
  the fore; hull #5e4228 / #563c24, wale #7a5a38; a gun muzzle in each
  port. Tint 4.6 %.
- Round 6: the skull 1.3x, no inset. Footprint 7596 (+15 %).
- Round 7: fit_scale 0.94. Sent to critic round 3. Tint per direction
  (columns 1-8): 9.9 / 8.8 / 4.0 / 5.3 / 7.2 / 8.1 / 2.4 / 7.0 % (mean
  6.6). Values: mean 110, p10 35, p50 106, p90 190, p99 244, std 61, upper
  145 / lower 87. ppm 48.82; footprint 6732 (stock 6624, +2 %). specks.py:
  clean.

## Critic round 3: 74.5 (continuity 77.5, fresh 71.5): gain (+5.0)
The hull tone landed. The C4 round was cancelled by the user's
3-critic-round cap.

## FINAL: critic round 3 (own round 7, commit c7847391)
The best average and the committed code (a started C4 draft was put back
to c7847391's block, byte-identical). The re-render matches critic 3's
sheets: final.png and final_red.png mean abs diff 0.000001, max 2/255.
Tint per direction (columns 1-8): 9.9 / 8.8 / 4.0 / 5.3 / 7.2 / 8.1 / 2.4 /
7.0 % (mean 6.6). ppm 48.82; footprint 6732 (stock 6624). Finals in
ArtGen/units3d/reviews/realism/privateer/ and the handoff folder (rows:
shipped, final, Frigate, Galleon).

Critic history (average; A-continuity / B-fresh):
- C1 58.25 (55 / 61.5; critical: house style)
- C2 69.5 (72 / 67)
- C3 74.5 (77.5 / 71.5) = FINAL; capped

Open points (C3's critics): the course skulls read as letter-like blobs cut
by the topsails and yards (identity better carried by the masthead flag
alone, angled to show a face in 2, 3 and 7, plus a thin civ wale stripe);
the hull short and tall (lengthen ~15 %, lower freeboard, masts raked aft:
a long, low raider); the sail tiers stack without a gap in 1/2/5; the
emblems stair-stepped on the angled sails.

Fit fix (draw size only; geometry, materials and civ colour unchanged): fit_scale 0.94 -> 1.05 in PRIVATEER.
The tall brig rig in the fit box had shrunk the hull. fitcheck bbox-area ratio 0.89 -> 1.12 (the taller sail
plan now draws above the stock's); side view (dirs 4/8) 1x width shipped 95, before 84, after 94; hull alone
(no bowsprit) about 81 / 73 / 84 px. specks.py clean. Finals re-rendered at HEAD (ppm 54.53); realism/naval
finals gate (11 ships) PASS.
Fit fix, step 2 (proportion toward stock; re-render only, no critic round): rig_scale 0.82 (new option in this
unit's builder, default 1.0 = unchanged): both masts, their yards and sails and the spanker scaled about each
mast's foot, so the sail plan keeps its shape. The pennant and flag keep their size at the mastheads. fit_scale
1.05 -> 0.991 holds the hull's ppm at 54.53 (the shorter rig raises the fitted ppm), so the hull length is unchanged:
about 84 px at 1x (shipped 81). Side-view height at 1x: shipped 43, step 1 57/55, now 49/47. bbox-area ratio
1.12 -> 0.96. specks.py clean; realism/naval finals gate (11 ships) PASS.
