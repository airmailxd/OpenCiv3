# Curragh (realism pass): design

The Curragh is a seagoing Irish hide boat of the 6th-10th centuries (the
boat of the Navigatio Sancti Brendani, rebuilt by Tim Severin as the
Brendan in 1976): oak-tanned ox hides stitched in panels over an ash
frame, a deep, strongly sheered hull, an ash gunwale, thwarts, a small
hide shelter aft, a big steering oar on the starboard quarter, a
mainmast and a raked foremast with square linen sails. Neighbours (stock):
Galley and Caravel.

## Baseline (as shipped)
A pale cream hide hull with two cross sails and civ pennants. Tint
(tint.py, DEFAULT): 5.6 / 7.7 / 5.3 / 1.8 / 5.0 / 6.1 / 5.2 / 1.8 %
(mean 4.8). Values: mean 129, p10 60, std 50, upper 140 / lower 123.
Footprint 1756 px (2x, alpha>127).

## Real proportions (the Brendan)
~11 m x 2.4 m (L:B ~4.5), ~1 m of freeboard amidships rising to the
ends, mainmast ~7 m, foremast ~5 m raked forward. Model: L 1.7 (1 unit
= 6.5 m), B 0.38, D 0.15, sheer 0.2 bow / 0.12 stern; mainmast top 0.98,
sail 0.74 x 0.52; foremast top 0.74 raked 8 deg, sail 0.5 x 0.36 (the
rig ~1.3x real for 1x, as the Galley).

## What must read at 1x
1. The deep, sheered hide hull: warm tanned-leather panels with dark
   stitched seams, a light continuous ash gunwale (the Galley's lessons:
   warm saturated material, a clear sheer, one continuous light cap).
2. Two masts, the fore one raked, two square sails with the civ Celtic
   cross (bold, as the Galley's sail colour).
3. The big steering oar on the starboard quarter (the stern's mark), the
   dark hide shelter aft.
No standing rigging (thin lines read as booms or break into specks).

## Civ colour (stock ~4.8 %)
The Celtic crosses on both sails, and one solid civ pennant at the
mainmast head (colour in the profiles; a pointed tip broke into a speck).

## Rounds
New builder ships.curragh_hide (BUILDERS 'Curragh'); the old curragh()
kept, unused; its own block and keys (the Galley's code untouched).
- Round 1: the hide boat. ppm 50.8: a little bigger than the stock
  (footprint 2044 px); profile tint 0.4 % (no civ there); the hide a
  dark brown (lower 103 vs the stock's 123).
- Round 2: fit_scale 0.92 (footprint 1761), hide lighter (#9a7246 /
  #906a40), a civ pennant. Its pointed tip a 1 px speck in 2 and 4.
- Round 3: a solid pennant (40 % of its height at the tip). Sent to
  critic round 1. Tint per direction (columns 1-8): 7.4 / 13.5 / 7.0 /
  1.8 / 7.8 / 6.9 / 7.3 / 1.9 % (mean 6.7). Values: mean 115, p10 53, p50
  118, p90 168, p99 202, std 43, upper 128 / lower 106. ppm 46.76.
  specks.py: clean.

## Critic round 1: 60.25 (A 56.5, B 64), no critical failure
Identity borderline in 4/8 ("a flat canoe or punt with two sticks").
Both: ~40-50 % deeper with a strong upswept sheer and a high, bluff,
rounded stem; the hide bands read as wood planking (darker oiled mottled
hide in a patchwork with stitched seams, a thick light ash gunwale, the
hull clearly darker than the sails); rowing kit (3-4 oars a side, thwarts
visible inside, a longer, heavier steering oar).

- Round 4: L 1.62, B 0.4, D 0.15 -> 0.22, sheer 0.32 bow / 0.24 stern,
  bluff ends (bow_p 2.3, stern_p 2.2); hide #74502c / #684626 / #5a3c20 in
  a three-tone patchwork, rows of panels with their vertical seams offset
  half a panel, stitching lighter; ash gunwale r 0.022 (#c8a46a); a dark
  open interior with the thwarts on it; three oars a side raked aft into
  the water (r 0.018, blades); steering oar r 0.027, longer, a bigger
  blade. The seams read as brickwork.
- Round 5: seams toned down (#8c6c44) and jittered (the hides not cut to
  a grid). Sent to critic round 2. Tint per direction (columns 1-8): 6.6 /
  10.5 / 6.0 / 1.4 / 6.0 / 5.7 / 5.6 / 1.3 % (mean 5.4). Values: mean
  109, p10 51, p50 106, p90 171, p99 204, std 47, upper 126 / lower 101.
  ppm 43.18; footprint 1742 px (stock 1756). specks.py: clean.

## Critic round 2: 71.75 (continuity 75.75, fresh 67.75): gain (+11.5)
The depth and sheer landed. Fixes: the oars the brightest thing,
splayed like insect legs (thinner, darker, 2 a side, shorter, the
steering oar a broad separate blade); the hide still one even brown at 1x
(more panel contrast, dark long seams; the pale framing outside toned
down, the gunwale a little darker); bow and stern higher, a taller stem;
sails ~15 % bigger with a reinforced edge.

- Round 6: sheer 0.4 bow / 0.3 stern, stem and stern posts to deck +
  0.09; sails 0.85 x 0.6 and 0.58 x 0.41 with a dark bolt rope round the
  sides and foot; hide tones #7e5630 / #664426 / #4e341c, the long
  (horizontal) seams dark #2a1c10, the short ones near the hide's tone;
  gunwale #a8844e; two oars a side (r 0.013, #6e5232, 0.22 outboard,
  raked aft); the steering oar shorter with a broad blade (0.13 x 0.22).
  A thin oar's blade broke off end-on in 6.
- Round 7: oars r 0.015; thwarts darker (#6e5232: they read as pale
  stanchions); hide #8a5e34 / #6a4626 / #4a301a. Sent to critic round 3.
  Tint per direction (columns 1-8): 7.2 / 13.2 / 6.6 / 1.3 / 6.3 / 8.0 /
  5.8 / 1.3 % (mean 6.2). Values: mean 99, p10 42, p50 89, p90 163, p99
  196, std 47, upper 122 / lower 84. ppm 43.34; footprint 1892 (stock
  1756). specks.py: clean.

## Critic round 3: 68.6 (continuity 83.25, fresh 54): no gain (1 of 2)
Both: the hide reads as brickwork / plank grain. Fixes: 4-5 big
irregular hides with a few long seams, smoother and darker with a sheen,
a faint rib shadow; oars never hanging under the keel ("stilts": raked
out and aft 30-45 deg); the hull ~15 % longer and a little narrower (the
footprint back near the stock's); the foresail showing area in 4/8.

- Round 8: L 1.62 -> 1.86, B 0.38; the hide in its own keys ('oxhide'
  #6c4828 / #5a3a1e, roughness 0.42: props.py's own 'hide' key overrode
  the old one): 5 big hides along the length (wavering joins), one long
  dark seam along the side and the dark joins, faint rib shadows; oars
  0.32 outboard and 0.2 aft (~33 deg down); the foresail braced 25 deg;
  belly 0.14. fit_scale 0.84 overshot (footprint 1553).
- Round 9: fit_scale 0.895 (footprint 1754, stock 1756).
- Round 10: rib shadows softer and wider apart (0.2; #5e3e20) so they
  don't read as staves. Sent to critic round 4. Tint per direction
  (columns 1-8): 7.8 / 12.0 / 6.2 / 2.0 / 7.1 / 7.0 / 5.8 / 2.2 % (mean
  6.3). Values: mean 100, p10 45, p50 92, p90 162, p99 197, std 44, upper
  124 / lower 86. ppm 40.19. specks.py: clean.

## Critic round 4: 77.0 (continuity 85.75 PASS, fresh 68.25): gain (+5.25)
The hide reads as oiled skin (continuity); the fresh critic still sees a
dory (flat vertical sides, level gunwales, upright ends).

- Round 11: the sides flare out over a round bottom (Hull tumblehome
  -0.28, fullness 1.0; beam 0.32 to keep the footprint); sheer 0.5 bow /
  0.38 stern; tall, rounded, upswept stem and stern posts (to deck +
  0.15); the hide warmer tan-ochre (#8e6436 / #7c5630, roughness 0.34);
  the long seam darker and thicker with light flax stitches across it,
  stitched cross-seams at the joins; rib heads over the gunwale; both
  yards braced 15 deg.
- Round 12: braces 22 deg (profile tint ~3 %). Sent to critic round 5.
  Tint per direction (columns 1-8): 8.2 / 11.1 / 3.2 / 3.0 / 6.5 / 7.9 /
  3.0 / 3.3 % (mean 5.8). Values: mean 104, p10 51, p50 101, p90 160, p99
  197, std 42, upper 124 / lower 91. ppm 39.93; footprint 1773 (stock
  1756). specks.py: clean.

## Critic round 5: 70.6 (continuity 85.75 PASS, fresh 55.5 with a CRITICAL): no gain (1 of 2)
Fresh: the oars like stilts (pointing steeply down past the keel) in
1/3/4/5/7/8; "a birch-bark canoe". (The oars were unchanged since C4:
0.32 out, 0.2 aft, ending at the waterline: ~33 deg down, which reads
steep from the high camera.) Fixes: oars 20-25 deg below horizontal,
shorter, nothing under the keel; the hide ~10 % darker (back toward C4),
fewer, irregular rib marks; the post heads rounded and shorter; brace
~12-15 deg with belly, the mainsail ~15 % bigger. Fleet rule: no sail
edge-on.

- Round 13: oars 22 deg below horizontal at mid-stroke (0.3 out, 0.14
  aft, the blades clear of the water: nothing below the keel in any
  view); four irregular rib shadows (y -0.52, -0.21, 0.13, 0.47); posts
  rounded and curling in, to deck + 0.085; braces 13 deg; mainsail 0.98 x
  0.69 (head 1.12).
- Round 14: hide #6e4a26 / #604022 (it read pale with the sheen),
  fit_scale 0.88; a thin oar broke off end-on in 6.
- Round 15: oars r 0.018. Sent to critic round 6. Tint per direction
  (columns 1-8): 8.7 / 12.8 / 5.8 / 1.8 / 7.8 / 8.9 / 5.2 / 3.0 % (mean
  6.8). Values: mean 104, p10 49, p50 98, p90 164, p99 201, std 44, upper
  128 / lower 89. ppm 38.83; footprint 1755 (stock 1756). specks.py:
  clean.

## Critic round 6: 78.9 (continuity 88 PASS, fresh 69.75): gain (+1.9)
Fresh: blunt squared ends read as a punt. Fixes: ends swept up into
tapered, upturned, rounded points (keep the curl); tonal variation
between hides, light thread-coloured seams that hold at 1x; lighter oar
shafts, one more oar a side; bow and oars beside the sail in 2/6; size
at the stock's.

- Round 16: sheer 0.58 bow / 0.46 stern, the ends tapered (bow_p and
  stern_p 2.3 / 2.2 -> 1.6); hides #76502a / #5a3a1c; the long seams and
  the joins in the light thread colour (#c8aa78, r 0.008 / 0.007); three
  oars a side (#9a7a4e, from y -0.2 to 0.36, 0.34 outboard). Sent to
  critic round 7. Tint per direction (columns 1-8): 8.5 / 12.2 / 5.4 /
  1.6 / 7.6 / 9.2 / 5.3 / 2.8 % (mean 6.6). Values: mean 108, p10 56, p50
  105, p90 163, p99 196, std 42, upper 128 / lower 97. ppm 38.69;
  footprint 1791 (stock 1756). specks.py: clean.

## Critic round 7: 80.5 (continuity 87.25 PASS, fresh 73.75): gain (+1.6)
The seams and oars landed (continuity); the tapered points read as a
birch-bark canoe (fresh). Fixes: back to round 6's fuller, bluffer ends
with curled heads, keeping C7's sheer; brace ~25 deg; the hide a step
lighter toward tan-ochre with the seams and 2-3 joints darker and wider;
the oars even, shallow, raked back along the hull, ~10 % darker; the
steering oar readable.

- Round 17: ends bluff again (bow_p 2.3, stern_p 2.2) with the C7 sheer
  (0.58 / 0.46); braces 25 deg; hide #82582e / #6a4826; the long seam
  and the joins dark (#1e140a) at r 0.01; oars #8a6c44, raked back 0.24
  (0.3 out); the steering oar r 0.03. Footprint 1852.
- Round 18: fit_scale 0.86. Sent to critic round 8. Tint per direction
  (columns 1-8): 9.9 / 11.4 / 3.4 / 3.8 / 7.8 / 9.1 / 3.1 / 4.4 % (mean
  6.6; the profiles up from 1.6-2.8 to 3.8-4.4). Values: mean 106, p10
  49, p50 104, p90 162, p99 194, std 43, upper 127 / lower 92. ppm 38.34;
  footprint 1769 (stock 1756). specks.py: clean.

## Critic round 8: 76.9 (continuity 87.25 PASS, fresh 66.5): no gain (1 of 2)
Lead: C9 from C7's code (6b723866) plus only: C6-style fuller shoulders
below the curl with C7's sheer; ~18 deg bracing; the near-side oars raked
out at the common angle with the blades at the waterline (posts in 1 and
5); in 1 the bow sail lifted and joined to its mast. C7's hide and thread
seams kept.

- Round 19: the block restored to 6b723866's, then: bow_p 2.0 / stern_p
  1.95 (between C7's taper and C6's bluff); braces 18 deg; oars 0.42 out,
  0.16 aft, the blades at z 0.03 beside the hull; the foresail's head
  0.88 and its mast's rake 4 deg. Sent to critic round 9. Tint per
  direction (columns 1-8): 9.1 / 11.0 / 4.9 / 2.8 / 8.0 / 8.7 / 4.9 / 3.5 %
  (mean 6.6). Values: mean 108, p10 57, p50 105, p90 163, p99 197, std 41,
  upper 128 / lower 96. ppm 38.22; footprint 1824 (+4 %). specks.py:
  clean.

## Critic round 9: 79.4 (continuity 87.25 PASS, fresh 71.5: the oars "stilts" again in 1/4/5/7/8): no gain (2 of 2) -> plateau

## FINAL: critic round 7 (own round 16, commit 6b723866)
The Curragh's block restored to 6b723866's (byte-identical; block check one
per unit). The re-render matches critic 7's sheets exactly (final.png and
final_red.png: mean abs diff 0, max 0). Tint per direction (columns 1-8):
8.5 / 12.2 / 5.4 / 1.6 / 7.6 / 9.2 / 5.3 / 2.8 % (mean 6.6). ppm 38.69;
footprint 1791 (stock 1756). Finals in ArtGen/units3d/reviews/realism/
curragh/ and the handoff folder (rows: shipped, final, Galley, Caravel).

Critic history (average; continuity / fresh):
- C1 60.25 (A 56.5 / B 64)
- C2 71.75 (75.75 / 67.75)
- C3 68.6 (83.25 / 54)
- C4 77.0 (85.75 / 68.25)
- C5 70.6 (85.75 / 55.5; critical: oars like stilts)
- C6 78.9 (88 / 69.75)
- C7 80.5 (87.25 / 73.75) = FINAL
- C8 76.9 (87.25 / 66.5)
- C9 79.4 (87.25 / 71.5); plateau

Open points: the oars' rake as read from the high camera (every angle
tried, 22-33 deg down, read as posts or stilts to some fresh critic);
the ends (round 6's curl preferred over C7's taper); the sails in 3 and 7
(narrow at C7's 13 deg bracing).

Fit check (lead's side-view criterion): no fit fix. The 1x side width is 49 -> 38 because the stock sprite's
oars/spars stick out past its hull; the hull alone is about 37 px in both (2x: 74 shipped, 76 final), within 5 %.
