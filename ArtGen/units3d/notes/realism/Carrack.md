# Carrack (realism pass): design

The Carrack is an Iberian nau of c. 1500 (Vasco da Gama's Sao Gabriel,
the Santa Maria's type): ~27 x 9 m (L:B ~3), a round, deep hull with a
strong sheer, a two-tier forecastle over the bow and a two-tier
aftercastle over a transom, lidded gunports in the waist, a bowsprit;
fore and main with courses, topsails and round fighting tops, a lateen
mizzen; shrouds to deadeyes on channels, a forestay and a mainstay.
Neighbours (stock): Caravel and Galleon.

## Baseline (as shipped)
Tint (tint.py, DEFAULT): 8.4 / 8.0 / 8.2 / 4.5 / 6.3 / 9.7 / 6.1 / 4.6 %
(mean 7.0). Values: mean 99, p10 39, std 52, upper 124 / lower 85.
Footprint 11056 px.

## Real proportions
L 2.7 = 27 m (1 unit = 10 m): B 0.9, D 0.24 plus a 0.07 bulwark, sheer
0.36 stern / 0.32 bow; castles aft 0.22 + 0.46, forward 0.2 + 0.4 over
the deck; main course 1.3 x 0.72, topsail 0.78 x 0.38; fore 0.95 x 0.55
and 0.55 x 0.3; lateen mizzen yard 1.35.

## What must read at 1x (the fleet's lessons; the Caravel's critic 1)
1. The towering stepped castles (painted sides, a dark band under a
   light rail on each tier) over a warm wooden hull with two dark wales,
   a covered deck with hatches, lidded gunports.
2. The rig: courses and topsails with the fighting tops, the lateen
   mizzen; the standing rigging (the stock carrack shows it): three
   shrouds a side on fore and main, two on the mizzen, deadeyes on
   channels, forestay and mainstay, r 0.016 and dark (#2e2418).
3. Ends: the forecastle over the bowsprit; the transom with the gilt
   lantern and the rudder.

## Civ colour (stock 7.0 %)
The quartered civ emblem (a light cross) on the fore and main courses; a
civ pennant at the main truck.

## Rounds
New builder ships.carrack_nau in its own block (BUILDERS['Carrack'] set
after the dict; the old SAILING entry kept). No other unit changed.
- Round 1: hull, castles (from the shared _castle), rig without
  shrouds: it read as an open boat with low castles (the Caravel's
  critic-1 failure).
- Round 2: own castles (painted solid sides, a dark band and a light rail
  cap per tier), hatches on a lighter covered deck, shrouds with deadeyes
  and channels, forestay and mainstay, belly 0.2.
- Round 3: taller castles (aft 0.22 / 0.46, fore 0.2 / 0.4). Sent to
  critic round 1. Tint per direction (columns 1-8): 7.9 / 12.5 / 8.1 /
  1.9 / 5.0 / 10.1 / 4.9 / 1.8 % (mean 6.5). Values: mean 99, p10 37, p50
  89, p90 169, p99 217, std 53, upper 126 / lower 85. ppm 62.57; footprint
  11630 (stock 11056). specks.py: clean.

## Critic round 1: 56.0 (A 57.5, B 54.5), CRITICAL (both)
Identity in 4/8 (a caravel or cog: low box castles, the square sails
edge-on slivers) and house style (a pale flat pine-tan hull against the
dark weathered roster). Fleet lesson: brace the square yards ~30-35 deg
so the profiles show sail faces.

- Round 4: sheer 0.46 / 0.42 (a deep crescent); the aftercastle 0.26 /
  0.56 with a transom of gilt-framed dark windows (two rows) and a stern
  gallery; the forecastle a two-tier triangular stack overhanging the bow
  (to yb + 0.2 and + 0.14; light rail, dark band); hull #523818 /
  #463014 (below the wales #3c2812), three black wales, two rows of
  lidded ports (staggered), the waist deck dark (#6e5434); fore and main
  sails braced 32 deg; the fighting tops dark discs (r 0.145). Sent to
  critic round 2. Tint per direction (columns 1-8): 8.8 / 10.6 / 3.3 /
  3.9 / 6.9 / 7.2 / 2.1 / 5.1 % (mean 6.0). Values: mean 89, p10 32, p50
  77, p90 164, p99 216, std 52, upper 120 / lower 72 (stock 99, 124 /
  85). ppm 62.32; footprint 11856 (stock 11056). specks.py: clean.

## Critic round 2: 61.4 (continuity 67.25, fresh 55.5): gain (+5.4)
The crescent sheer and the side-view sail faces landed. Fixes: the rig
stubby on the big hull (main ~25-30 % taller, wider courses and
topsails, a bigger lateen, a longer, thicker bowsprit); the castles flat
pale-tan walls (stacked decks: a deck line, a dark rail with balusters, a
darker painted band); the lower hull a flat dark slab (less freeboard);
bigger port lids with light edges; the footprint back near 11056. Fleet
rule: brace ~18-22 deg with belly.

- Round 5: D 0.24 -> 0.2; fore course 1.1 x 0.62 (head 1.6), topsail 0.72
  x 0.4; main 1.5 x 0.82 (head 2.0), topsail 0.98 x 0.48; mizzen yard
  1.6, drop 0.8; brace 20, belly 0.26; bowsprit to yb + 0.6, r 0.032;
  castle sides dark walnut (#4a3018) with a painted band (#7e5a30), a
  light deck line, a dark rail on balusters; ports in light frames with
  lids. Sent to critic round 3. Tint per direction (columns 1-8): 8.1 /
  9.9 / 5.4 / 3.5 / 6.3 / 9.4 / 3.7 / 4.2 % (mean 6.3). Values: mean 96,
  p10 33, p50 81, p90 172, p99 227, std 57, upper 134 / lower 74. ppm
  54.82; footprint 11040 (stock 11056). specks.py: clean.

## Critic round 3: 73.0 (continuity 77.75, fresh 68.25): gain (+11.6)
All three critic-2 fixes landed. Bracing rule v3 (30 deg, deep belly,
courses the biggest, mizzen lateens ~20 % smaller). Fixes: a third
forecastle tier towering over the waist, a higher aftercastle; larger
lidded ports with dark recesses, contrasting wales, channels with
deadeyes; a thicker mainmast and top (6 at 1x).

- Round 6: brace 30, belly 0.3; mizzen yard 1.6 -> 1.24, drop 0.62, top
  1.4; forecastle tiers 0.2 / 0.42 / 0.62 (each overhanging the stem a
  little less); aftercastle 0.3 / 0.66; wales light (#8a6436) on the dark
  hull; ports 0.095 x 0.086 light frames round dark recesses with lids;
  masts r 0.03 / 0.04 / 0.024, tops r 0.17. Sent to critic round 4. Tint
  per direction (columns 1-8): 8.4 / 10.0 / 4.2 / 4.5 / 6.7 / 7.7 / 2.7 /
  5.4 % (mean 6.2). Values: mean 95, p10 36, p50 79, p90 168, p99 233,
  std 54, upper 129 / lower 75. ppm 55.37; footprint 11162 (stock 11056).
  specks.py: clean.

## Critic round 4: 81.0 (continuity 81.25, fresh 80.75): gain (+8.0)
Fixes: hull banding (light wales read as busy bright stripes for one
critic, the hull a dark mass for the other: two muted mid-tone wales,
darker rails, the planking a step lighter, the dark port recesses
carrying the side); the forecastle visibly triangular past the stem; the
courses ~10 % smaller or the castle tops lighter so the castles show in
1/2/6; one solid civ cross (Order of Christ) instead of the quartered
checker on courses and topsails; the fore yard ~35 deg.

- Round 7: wood #5e4222 / #563c1e; two wales (#5e4428); port frames and
  the castle deck lines mid-tone (#7a5a34); deck and castle tops lighter
  (#8a6a42); forecastle overhangs 0.3 / 0.24 / 0.16 past the stem;
  courses 1.0 x 0.56 and 1.35 x 0.74, topsails 0.66 x 0.36 and 0.9 x 0.44;
  a solid cross pattee (1.1x) on courses and topsails; fore yard 35 deg,
  main 30. Sent to critic round 5. Tint per direction (columns 1-8): 11.5
  / 14.1 / 4.9 / 4.8 / 7.1 / 9.7 / 2.2 / 7.0 % (mean 7.7). Values: mean
  89, p10 36, p50 70, p90 162, p99 217, std 51, upper 120 / lower 73. ppm
  55.79; footprint 10865 (stock 11056). specks.py: clean.

## Critic round 5: 80.0 (continuity 83.5, fresh 76.5): no gain (1 of 2)
The cross pattee and the calm hull sides landed (continuity). Fixes: a
tall multi-level triangular forecastle past the stem, at least the
aftercastle's height, a dark shadow under it, the bowsprit below; the
fleet sail rule with four clean flared arms on the crosses; a lighter
trim edge on the wales, framed port lids that read at 1x; a tidier waist.

- Round 8: the Carrack's own sail helper _ck_sail (the rule; the shared
  _square_sail untouched), the cross at full size in the flat centre;
  forecastle tiers 0.24 / 0.48 / 0.72 overhanging 0.36 / 0.3 / 0.22, its
  underside dark (tar); the bowsprit lowered under it (and the forestay's
  foot); a light trim line (#9a7848) along each wale, port frames the same
  tone; one hatch. Sails p99 239.
- Round 9: sailcloth a step darker (#d6c6a2). Sent to critic round 6.
  Tint per direction (columns 1-8): 12.2 / 15.8 / 6.1 / 4.9 / 7.0 / 9.6 /
  3.1 / 7.9 % (mean 8.3). Values: mean 91, p10 36, p50 69, p90 171, p99
  229, std 54, upper 126 / lower 73. ppm 55.8; footprint 10995 (stock
  11056). specks.py: clean.

## Critic round 6: 81.4 (continuity 83.5, fresh 79.25): +0.4, no gain (2 of 2) -> plateau

## FINAL: critic round 6 (own round 9, commit b73b78fb)
The highest average and the current code (no restore). The re-render
matches critic 6's sheets: final.png and final_red.png mean abs diff
0.000003, max 2/255. Tint per direction (columns 1-8): 12.2 / 15.8 / 6.1 /
4.9 / 7.0 / 9.6 / 3.1 / 7.9 % (mean 8.3). ppm 55.8; footprint 10995.
Finals in ArtGen/units3d/reviews/realism/carrack/ and the handoff folder
(rows: shipped, final, Caravel, Galleon).

Critic history (average; continuity / fresh):
- C1 56.0 (A 57.5 / B 54.5; critical: identity and house style)
- C2 61.4 (67.25 / 55.5)
- C3 73.0 (77.75 / 68.25)
- C4 81.0 (81.25 / 80.75)
- C5 80.0 (83.5 / 76.5)
- C6 81.4 (83.5 / 79.25) = FINAL; plateau

Open points: the emblem edges on the angled sails ("torn, projected": the
face mask is per face, stair-stepped); the fore-rig faces in 4 and 8; the
stacked sails in 2.

Fit fix (draw size only; geometry, materials and civ colour unchanged): fit_scale 1.0 -> 1.17 in CARRACK.
The tall rig and castles in the fit box had shrunk the hull. fitcheck bbox-area ratio 0.94 -> 1.29 (the tall
sail plan and castles now draw above the stock's); side view (dirs 4/8) 1x width shipped 112, before 95, after
112; hull alone (no bowsprit) about 101 / 89 / 101 px. 1.15 and 1.16 left the main-masthead pennant joined to
the mast only by alpha-127 pixels in dir 2 (specks.py: a 25 px piece); 1.17 (and 1.14) is clean. Finals
re-rendered at HEAD; realism/naval finals gate (11 ships) PASS.
Fit fix, step 2 (proportion toward stock; re-render only, no critic round): rig_scale 0.7 (new option in this
unit's builder, default 1.0 = unchanged): fore and main masts with their courses and topsails, and the lateen
mizzen's mast, yard and sail, scaled about each mast's foot, so the sail plan keeps its shape. The pennant keeps
its size at the main truck. fit_scale 1.17 -> 1.0386 holds the hull's ppm at 65.28, so the hull length is
unchanged: about 101 px at 1x (shipped 101). Side-view height at 1x: shipped 57, step 1 85/84, now 65/64.
bbox-area ratio 1.29 -> 0.96. specks.py clean (the masthead pennant included). Look note: the lowered lateen
mizzen now sits mostly behind the tall sterncastle in the side views. Scaling about 0.3 above the deck (rig 0.65)
was tried and did not show it better. realism/naval finals gate (11 ships) PASS.

Later pass (lead, 2026-11-24): leave as is for now. The lowered lateen mizzen is hidden behind the sterncastle,
and the castles dominate the side views. To fix: give the mizzen its own factor (~0.85), or lower the sterncastle.
