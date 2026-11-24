# Cruiser (realism pass): design

The Cruiser is a Baltimore-class heavy cruiser of 1944 (USS Baltimore,
CA-68): 205 x 21.6 m (L:B 9.5), three triple 8"/55 turrets (two forward,
the second superfiring; one aft), six twin 5"/38 mounts (one on the
centreline fore and aft, two a side), two funnels set well apart, the
forward tower bridge with the Mk 34 director, a tripod foremast, a pole
mainmast with the after director, and on the fantail two catapults and
the big aircraft crane. Built in metres (ships.cruiser_baltimore, CRUISER).
Neighbours (stock): Battleship and Destroyer.

## Baseline (as shipped)
Tint (tint.py, DEFAULT): 5.4 / 10.9 / 5.5 / 3.8 / 4.2 / 9.2 / 4.2 / 3.8 %
(mean 5.9). Values: mean 107, p10 51, p50 102, p90 179, std 46, upper
120 / lower 105. Footprint 5073 px. specks.py: up to 6 pieces (pennants).

## What must read at 1x
Between the Destroyer and the Battleship: triple turrets (not single
mounts), two funnels well apart (the battleship's are close together), a
modest tower bridge, the stern crane (only the cruiser has it).

## Civ colour (stock 5.9 %)
The three turret roofs and the six 5" mount roofs (air-recognition
panels), a band round each funnel, the ensign.

## Rounds
New builder ships.cruiser_baltimore (CRUISER), own block, the _nm_*
helpers; BUILDERS['Cruiser'] after the dict. No shared code changed.
- Round 1: flat and small at 1x (zx 1.7 added for all), the tripod's
  radar a floating speck (r 0.35 m stalk).
- Round 2: the tower bridge as five levels narrowing up (to ~1/3 of the
  ship's height, as the stock), turrets 12 x 14.5 x 4.2 m with 0.65 m
  barrels, funnels 16/15 m tall; masts r 0.85 m, radar stalks as thick as
  the mast; the crane post 1.4 m, its jib 1.0 m; the hook's fall dropped.
- Round 3: scuttles only on the tower's bottom level (the ports striped
  it); hull a step lighter, houses a step darker (#969a9e); no bow number.
- Round 4: Measure 22 (dark below 5 m, haze above), funnel bands 0.5-0.84.
  Sent to batch critic round 1. Tint per direction (columns 1-8): 6.2 /
  4.4 / 6.1 / 6.0 / 6.2 / 5.6 / 5.8 / 5.8 % (mean 5.8; stock 5.9). Values:
  mean 104, p10 45, p50 103, p90 170, p99 200, std 48, upper 116 / lower
  101 (stock 107, 120 / 105). ppm 67.50; footprint 5181 px (stock 5073,
  +2.1 %). specks.py: clean.

## Batch critic round 1: 64.5 (A 67.5, B 61.5), no critical failure
Shared points as in Destroyer.md. Cruiser: must not read as a long
destroyer: a boxy Baltimore bridge block with a director and a tripod;
bigger, taller triple turrets with a superfiring step forward, all three
roofs alike; a thicker crane (pedestal, boom) and the catapult.

- Rounds 5-7: the shared changes (Destroyer.md); the tower replaced by
  one boxy bridge block (0.48 of the beam, 6.8 m) with two rows of bridge
  windows, the open bridge and its wings, the pilot house and the Mk 34
  director on top, a tripod (r 1.1 m) behind it; turrets 13.5 x 16.5 x
  5.4 m, sloped faces and sides, light barrels r 0.8 m, the superfiring
  No. 2 on a 5.8 m barbette; civ colour on the three turret roofs alike
  (flat roof 0.88 of the base), the six 5" mount roofs and the wings of
  two Kingfisher floatplanes on the catapults; the funnels plain grey;
  the crane a pedestal, a 1.6 m post and a 1.2 m boom; catapults 1.8 m
  wide on turntables; freeboard 6.4 m, 6 % flare; fit_scale 0.96. Sent to
  batch critic round 2. Tint per direction (columns 1-8): 4.0 / 5.9 / 3.9
  / 3.3 / 3.9 / 5.2 / 3.8 / 3.3 % (mean 4.2; stock 5.9). Values: mean
  104, p10 44, p50 105, p90 162, p99 194, std 45, upper 111 / lower 102
  (stock 107, 120 / 105). ppm 64.49; footprint 5072 px (stock 5073).
  specks.py: clean.

## Batch critic round 2: 68.75 (A 74.5, fresh 63.0), no critical failure
Shared points as in Destroyer.md. Cruiser: bigger triple turrets with long
dark 8" barrels, B clearly superfiring over A; fat capped funnels; a
massive tower bridge; the pale ball under turret 2 to a straight barbette.

- Round 8 (last): turrets 15 x 18 x 6 m, dark barrels r 0.95 m, 22 m; A at
  y 67, B at 47 on a 7.2 m flat-shaded barbette; funnels 8.6 x 11.5 m,
  14 / 13 m, flared caps, civ bands 0.55-0.78 (the cruiser's funnels are
  its own: fat and capped); the bridge block 0.6 of the beam, 9 m, with a
  pilot-house level and a director platform stepping up to a 6.0 x 6.4 x
  4.4 m director; the tripod behind it. Sent to batch critic round 3. Tint
  per direction (columns 1-8): 5.7 / 6.2 / 5.7 / 5.3 / 5.6 / 5.9 / 5.4 /
  5.2 % (mean 5.6; stock 5.9). Values: mean 100, p10 41, p50 98, p90 164,
  p99 195, std 47, upper 109 / lower 98 (stock 107, 120 / 105). ppm 63.03;
  footprint 5109 px (stock 5073, +0.7 %). specks.py: clean.

## Batch critic round 3: 68.5 (A 78.0, fresh 59.0, CRITICAL from the fresh critic: identity, "a longer Destroyer")

## FINAL: batch critic round 2 (own round 7, commit 32d1d04e), restored
B2 is the higher average and has no critical failure. Restored as the
Destroyer (scoped, B2 block verbatim; the B3 helper options default to B2).
final.png / final_red.png (rows: shipped, final, Battleship, Destroyer) in
ArtGen/units3d/reviews/realism/cruiser/ and the handoff review folder.
Tint per direction (columns 1-8): 4.0 / 5.9 / 3.9 / 3.3 / 3.9 / 5.2 / 3.8
/ 3.3 % (mean 4.2; stock 5.9); footprint 5072 px (stock 5073); ppm 64.49.

Critic history (average; A / B):
- B1 64.5 (67.5 / 61.5)
- B2 68.75 (74.5 / 63.0) = FINAL
- B3 68.5 (78.0 / 59.0, critical: identity)

Later pass: a bridge tower taller and broader than the Destroyer's; wider
triple turrets; floatplane and catapult as a solid mass amidships.

Finals gate (GPU re-render at HEAD vs the B2 sheets, every flic and tint layer): mean 1.7e-08, max 1/255: PASS; specks.py clean.
