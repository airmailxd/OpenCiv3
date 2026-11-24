# Dromon (realism pass): design

The Dromon is a Byzantine war galley of the 10th century (the dromon of
Leo VI's Taktika): ~31 x 4.5 m (L:B ~7), fully decked, two banks of
oars (the upper rowed from the deck through ports in the bulwark, the
lower through hull ports), a wooden castle (xylokastron) by the
mainmast, the Greek-fire siphon in the bow on a forecastle over a spur
(no waterline ram by then), two lateen-rigged masts, steering oars and
a stern canopy. Neighbours (stock): Galley and Caravel.

## Baseline (as shipped)
A Galley-like hull with a big square sail (civ flames), striped civ
canopies fore and aft, a serpent siphon. Tint (tint.py, DEFAULT): 21.4 /
28.4 / 21.8 / 6.3 / 23.2 / 30.5 / 21.8 / 7.3 % (mean 20.1). Values: mean
120, p10 54, std 53, upper 149 / lower 101. Footprint 3854 px.

## Real proportions
L 2.8 = 31 m (1 unit = 11 m): beam 0.42, freeboard 0.17 to the deck plus
a 0.07 bulwark, sheer 0.26 stern / 0.16 bow; mainmast top 1.25 with a
2.0 yard, foremast top 0.95 (raked 10 deg) with a 1.4 yard (the rig ~1.3x
real for 1x, as the Galley).

## What must read at 1x (the Galley's lessons applied)
1. Warm, saturated wood with a visible sheer, a dark wale, a light
   continuous gunwale cap; the hull darker below the wale.
2. Two oar tiers by value: dark upper oars with raised blades, light lower
   oars dipping; parallel, staggered half a station.
3. Ends: the bow's gilt-headed bronze siphon on the forecastle over the
   spur; the stern's curling post with a warm rim and gilt finial, the
   civ canopy, two steering oars.
4. Two lateen sails, set wing and wing (running before the wind, out to
   opposite sides), so every direction shows sail area and the mirror
   pairs match (a fore-and-aft lateen is a sliver head-on; one yawed to
   one side shows its face in only one of each mirror pair).
No standing rigging.

## Civ colour (stock 20.1 %, mostly the sail)
A broad civ band along each sail's head (the yard) and a civ cross pattee
in the middle of each; the stern canopy.

## Rounds
New builder ships.dromon_lateen in its own block (before the steel ships);
BUILDERS 'Dromon'; the old dromon() kept, unused. No other unit changed.
- Round 1: yards yawed 20 deg: the sails a sliver head-on and stern-on
  (box 46 px wide vs the stock's 78), the ship 11 % over the stock's size.
- Round 2: yaw 48/42, fit_scale 0.9: the diagonal mirror pairs differ (a
  face in 3/7, an edge in 1/5).
- Round 3: yaw 12, belly 0.22: back to slivers head-on; a yard tip broke
  off edge-on.
- Round 4: wing and wing (main +45, fore -45; the belly to leeward):
  sail area in every direction. Tint 14.3-25.5 % (mean 20.3).
- Round 5: fit_scale 0.97 (footprint 3718 vs 3854). Sent to critic round
  1. Tint per direction (columns 1-8): 17.0 / 26.6 / 24.8 / 20.8 / 14.8 /
  22.7 / 21.4 / 18.8 % (mean 20.8). Values: mean 103, p10 43, p50 89, p90
  177, p99 204, std 52, upper 124 / lower 95. ppm 44.31. specks.py: clean.

## Critic round 1: 57.25 (A 55.5, B 59), CRITICAL (both)
The wing-and-wing rig: in 1 the fore yard and sail hang out over open
water (detached); in 2 (and 6) the crossed yards a tangle of canvas.
Sail-heavy and spindly (sails ~60 % of the silhouette); the dromon parts
vanish at 1x; the hull an open, empty trough.

- Round 6: both lateens fore-and-aft (yaw 0), ~30 % smaller (main yard
  1.45, drop 0.72, top 1.0; fore 1.0 / 0.52 / 0.8), the masts moved apart
  (main y -0.1, fore 0.95) and the main yard set 0.3 forward of its mast
  so the yards never cross and stay inside the hull's footprint; belly
  0.14; the deck lighter (#a08050); the castle twice as tall (0.34) with
  merlons and civ pavises (two a side, one fore and aft, in dark rims);
  the siphon on a raised platform, a bigger gilt beast head (0.06 x 0.09)
  with a dark muzzle; the steering oars dark. Tint fell to 11.8 %.
- Round 7: the sail's civ band 0.3 of the drop, the cross 0.8: merged
  with the band. Tint 16.1 %.
- Round 8: the cross 0.72 and lowered by 0.1 of the drop (separate from
  the band). Sent to critic round 2. Tint per direction (columns 1-8):
  16.9 / 7.6 / 17.4 / 22.6 / 21.0 / 4.8 / 17.7 / 19.8 % (mean 16.0; the
  end-on views are slivers now). Values: mean 90, p10 40, p50 77, p90
  169, p99 194, std 47, upper 102 / lower 84. ppm 47.95; footprint 3793
  (stock 3854). specks.py: clean.

## Critic round 2: 66.75 (continuity 68 with a CRITICAL, fresh 65.5): gain (+9.5)
Fore-and-aft made 2 and 6 "a stick" (civ a 1-2 px streak). Fixes (the
lead accepts mirror-pair differences): brace both yards 35-45 deg to the
same side, the foresail the bigger; the oar tiers (near-black upper, pale
lower, a 1 px light line between); the siphon ~2x, projecting past the
stem with a dark muzzle, a longer spur; the hull darker below a
contrasting wale with strake lines, the flat deck broken by a gangway.

- Round 9: both yards braced 40 deg to the same side; the foresail the
  big one (yard 1.45, drop 0.72, mast top 1.02, y 0.78), the main smaller
  (1.0 / 0.52 / 0.82, y -0.25); upper oars #2a1e14, lower #d4b888, a light
  ash line just above the dark wale, two strake lines below; the siphon
  tube r 0.05 running past the stem to a 0.085 x 0.13 gilt head with a
  dark muzzle; the spur to yb + 0.4; a darker central gangway.
- Round 10: fit_scale 1.02. Sent to critic round 3. Tint per direction
  (columns 1-8): 8.4 / 21.9 / 26.0 / 18.7 / 5.9 / 18.9 / 20.7 / 14.8 %
  (mean 16.9; 1 and 5 now see the sails edge-on). Values: mean 88, p10
  37, p50 77, p90 162, p99 195, std 47, upper 102 / lower 83. ppm 48.29;
  footprint 3825 (stock 3854). specks.py: clean.

## Critic round 3: 68.0 (continuity 72.25, fresh 63.75 with a CRITICAL): +1.25, no gain (1 of 2)
Continuity: the 40 deg bracing left 1 and 5 edge-on. Fresh: the sails
read as different rigs per view (point-down triangles under horizontal
yards in 4/6, proper lateens in 7/8). Fixes: bracing ~20-25 deg; every
yard a strong lateen slant (30-35 deg); the siphon ~35 % smaller, darker
bronze, short; the oar tiers 1-2 px further apart or thicker upper oars;
a darker waterline.

- Round 11: yards braced 22 deg, slanted 33 deg (masts lower: fore 0.92,
  main 0.78; drops 0.62 / 0.46 so the clews stay over the deck); the
  siphon a short dark-bronze tube (r 0.034, #7a5c2c) with a smaller head
  (0.055 x 0.085) in the same bronze and the black muzzle; the lower oar
  ports to deck - 0.095, the upper oars r 0.022; the tarred waterline to
  z 0.05. Sent to critic round 4. Tint per direction (columns 1-8): 11.7
  / 6.7 / 19.7 / 19.2 / 12.3 / 10.7 / 17.6 / 15.2 % (mean 14.2). Values:
  mean 84, p10 36, p50 74, p90 156, p99 190, std 45, upper 99 / lower 78.
  ppm 48.63; footprint 3846 (stock 3854). specks.py: clean.

## Critic round 4: 72.25 (continuity 76.75, fresh 67.75 with a CRITICAL): gain (+4.25)
The siphon and the oar tiers landed. The sails remain the problem: every
bracing so far left a pair of views edge-on (40 -> 1/5, 22 -> 2/6).
Fixes: camber (~12-15 % belly along the yard) and ~28-30 deg bracing,
every sail at least ~4 px of face in every view, civ in 2 and 6, sails
~15 % bigger; a wider hull, a taller castle with merlons that read at 1x;
a flared beast-head or funnel at the siphon's muzzle.

- Round 12: belly 0.14 -> 0.22, braced 29 deg; sails +15 % (fore yard
  1.67 / drop 0.71 / top 1.02 at y 0.85; main 1.15 / 0.53 / 0.86 at y
  -0.36, the yards clear of each other); B 0.46; the castle 0.4 tall and
  1.2x wide, merlons 0.065; a flared bronze funnel with a black mouth at
  the siphon's muzzle. Checked at 2x: every view shows sail faces and civ
  colour (2: 9.4 %, 6: 16.3 %). Sent to critic round 5. Tint per
  direction (columns 1-8): 15.2 / 9.4 / 23.5 / 21.8 / 12.4 / 16.3 / 21.3 /
  18.9 % (mean 17.3). Values: mean 88, p10 37, p50 77, p90 162, p99 196,
  std 47, upper 106 / lower 81. ppm 47.21; footprint 3968 (stock 3854).
  specks.py: clean.

## Critic round 5: 74.9 (continuity 77.25, fresh 72.5): gain (+2.6)
The siphon and castle landed; direction 2 the last weak frame. Fixes:
brace ~35 deg with the foresail ~20 % fuller than the main; the oar
tips wider bow-on; a civ pennant or hull band; darker plank seams, a
contrasting rubbing strake, a darker lower hull; the foresail not hidden
behind the castle in 5.

- Round 13: yards braced 35 deg; belly per sail (fore 1.2x, main 1.0x of
  0.22); oar reach 0.26 -> 0.32; a civ band along both bulwarks; three
  dark seam lines below the wale (the light ash line above it kept as the
  rubbing strake); the lower hull darker (#563c20); the castle 0.1 further
  aft. Sent to critic round 6. Tint per direction (columns 1-8): 15.6 /
  11.3 / 24.7 / 21.7 / 10.8 / 16.3 / 21.9 / 19.6 % (mean 17.7). Values:
  mean 87, p10 35, p50 75, p90 161, p99 195, std 48, upper 105 / lower 80.
  ppm 47.47; footprint 4114 (stock 3854: the longer oars). specks.py:
  clean.

## Critic round 6: 71.6 (continuity 79, fresh 64.25 with a CRITICAL: 2 at 35 deg, the yards splay, "wreckage"): no gain (1 of 2)
Lead: back to C5's rig geometry (29 deg, C5 belly and yard placement),
keep C6's hull gains (darker lower hull, seams, civ bulwark band, longer
oars); the foresail clear of the castle in 5 (forward or ~15 % bigger);
the mainsail a little more belly in 1; a bigger gilded siphon head; a gold
trim line on the sternpost and gunwale; footprint <= 5 % over.

- Round 14: yards 29 deg; the main's belly 1.15x (0.22 base), the
  foresail 15 % bigger (yard 1.9, drop 0.8, top 1.12, y 0.88); the siphon
  head gilded and bigger (0.07 x 0.1); a gilt line along the gunwale, the
  sternpost's rim highlight gold (#d4aa50). fit_scale 0.98: a 3 px speck
  in 2.
- Round 15: fit_scale 1.01. Sent to critic round 7. Tint per direction
  (columns 1-8): 17.4 / 11.3 / 27.0 / 25.5 / 14.2 / 16.7 / 23.3 / 22.1 %
  (mean 19.7). Values: mean 90, p10 36, p50 77, p90 170, p99 199, std 50,
  upper 111 / lower 83. ppm 45.92; footprint 4046 (+5 %). specks.py:
  clean.

## Critic round 7: 73.4 (continuity 78.25, fresh 68.5 with a CRITICAL: direction 2 a sliver): no gain (2 of 2) -> plateau

## FINAL: critic round 5 (own round 12, commit 8be15263)
The Dromon's block restored to 8be15263's (byte-identical; block check one
per unit). The re-render matches critic 5's sheets: final.png and
final_red.png mean abs diff 0.000002, max 1/255. Tint per direction
(columns 1-8): 15.2 / 9.4 / 23.5 / 21.8 / 12.4 / 16.3 / 21.3 / 18.9 %
(mean 17.3). ppm 47.21; footprint 3968. Finals in ArtGen/units3d/reviews/
realism/dromon/ and the handoff folder (rows: shipped, final, Galley,
Caravel).

Critic history (average; continuity / fresh):
- C1 57.25 (A 55.5 / B 59; critical: the wing-and-wing rig)
- C2 66.75 (68 / 65.5)
- C3 68.0 (72.25 / 63.75)
- C4 72.25 (76.75 / 67.75)
- C5 74.9 (77.25 / 72.5) = FINAL
- C6 71.6 (79 / 64.25)
- C7 73.4 (78.25 / 68.5); plateau

Open points: the sail faces in directions 1 and 2 (the continuity critic
suggests ~25 % belly set low at the foot rather than more bracing); the
bare yard tips in 2; the oar banks (C6's longer oars, the darker lower
hull, the seams, the civ bulwark band and the gilt trim are not in the
final).
