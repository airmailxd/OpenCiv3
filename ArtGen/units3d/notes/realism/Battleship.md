# Battleship (realism pass): design

The Battleship is an Iowa-class battleship of 1944 (USS Iowa, BB-61): 270
x 33 m (L:B 8.2), a long clipper bow with a strong sheer, three triple
16"/50 turrets (two forward, one aft) with rangefinder hoods at their
rear corners, the armoured conning tower under the tall stepped
fire-control tower and its Mk 38 director, two funnels close together,
ten twin 5"/38 mounts along the superstructure, the after director and
pole mainmast, a teak deck. Built in metres (ships.battleship_iowa,
BATTLESHIP). Neighbours (stock): Cruiser and Carrier.

## Baseline (as shipped)
Tint (tint.py, DEFAULT): 7.2 / 13.7 / 7.4 / 4.8 / 4.9 / 7.9 / 4.9 / 4.8 %
(mean 6.9). Values: mean 108, p10 53, p50 102, p90 179, std 46, upper
127 / lower 105. Footprint 6740 px. specks.py: 10-12 pieces in 1/3/5/7
(railing and pennant specks).

## What must read at 1x
The biggest gun ship: the three huge turrets with long barrels, the tall
tower forward, the broad hull with the teak deck; heavier and beamier
than the Cruiser.

## Civ colour (stock 6.9 %)
The three turret roofs (air-recognition panels), a band round each
funnel, the ensign. (The 5" mounts stay grey: their roofs would scatter
the civ colour.)

## Rounds
New builder ships.battleship_iowa (BATTLESHIP), own block, the _nm_*
helpers; BUILDERS['Battleship'] after the dict. No shared code changed.
- Round 1: a long low plank at 1x, the superstructure a strip; the teak
  pale. zx 1.7 for all; beam 34 -> 40 m; teak darker (#7a6446).
- Round 2: reads as a warship; the tower low beside the stock's tall
  pyramid; turrets small; the masts (0.6 m) specked.
- Round 3: the forward tower as six levels narrowing up with the Mk 38
  director on top (~1/3 of the length tall, as the stock), turrets 16 x
  20 x 5.2 m with 1.05 m barrels 26 m long, funnels 18/17 m, masts and the
  stern crane 1.0 m, teak #6e5a40. The hull read pale on its long flat
  side.
- Round 4: Measure 22 with a darker hull (#3a3e42) below 6.2 m and haze
  above; funnel bands 0.45-0.84; ensign 18 x 11 m. Sent to batch critic
  round 1. Tint per direction (columns 1-8): 7.1 / 4.0 / 6.5 / 6.9 / 6.9 /
  4.6 / 6.5 / 6.8 % (mean 6.2; stock 6.9). Values: mean 108, p10 45, p50
  115, p90 173, p99 211, std 49, upper 119 / lower 106 (stock 108, 127 /
  105). ppm 73.25; footprint 6705 px (stock 6740, -0.5 %). specks.py:
  clean.

## Batch critic round 1: 71.9 (A 72.5, B 71.25), no critical failure
Shared points as in Destroyer.md. Battleship: an armoured conning tower
and a tall, slim, heavy director tower instead of the stepped cone; a
longer, sharper clipper bow, more freeboard and bulk, an armour-belt step;
the teak toward grey-brown.

- Round 5: the shared changes (Destroyer.md); the tower one slim block
  (0.2 of the beam, 26 m) with two bridge levels (window bands, wings) and
  the Mk 38 director on top, the conning tower a heavy dark cylinder
  (r 4.8 m) with a vision-slit band in front of it; bow rake 22 m, bow_p
  1.12 (finer), freeboard 9 m, sheer forward 7.5 m, 5 % flare; the armour
  belt a proud strip (0.8 m) from y -78 to 82 m with a lit top step; teak
  #6a6050; civ colour moved off the turret roofs to recognition panels on
  the forecastle and quarterdeck; barrels r 1.2 m, light; ensign 11 x 7 m;
  fit_scale 0.955. Sent to batch critic round 2. Tint per direction
  (columns 1-8): 5.1 / 8.0 / 5.0 / 5.1 / 5.5 / 8.1 / 5.2 / 5.1 % (mean
  5.9; stock 6.9). Values: mean 111, p10 49, p50 117, p90 172, p99 212,
  std 47, upper 122 / lower 108 (stock 108, 127 / 105). ppm 69.61;
  footprint 6591 px (stock 6740, -2.2 %). specks.py: clean.

## Batch critic round 2: 70.5 (A 77.0, fresh 64.0), no critical failure (below C1's 71.9)
Shared points as in Destroyer.md. Battleship: separate the turrets, B
clearly above A, sloped faces; a massive fire-control top, faired
funnels, rows of 5" mounts; smaller deck rectangles (they read as decals),
civ elsewhere (turret tops) to keep the amount.

- Round 6 (last): turrets 17 x 21 x 6.2 m at y 76 / 52 / -68, B on an
  8.2 m barbette, dark barrels r 1.35 m, 31 m; civ roofs (0.9 of the base)
  on all three; a fire-control top block (0.36 of the beam, 4.6 m) under
  an 8 x 8.4 x 5 m director; faired funnels (the base flaring into the
  deck) with narrow civ bands; the 5" twins 5 x 6.6 x 3.3 m with dark
  barrels; the deck panels smaller (0.7 of the deck's width, 18 / 16 m
  long, boxes on the teak instead of the whole deck width). Sent to batch
  critic round 3. Tint per direction (columns 1-8): 5.1 / 5.1 / 5.0 / 5.1
  / 5.8 / 5.8 / 5.5 / 5.0 % (mean 5.3; stock 6.9). Values: mean 104, p10
  47, p50 104, p90 163, p99 198, std 45, upper 118 / lower 101 (stock 108,
  127 / 105). ppm 68.41; footprint 6695 px (stock 6740, -0.7 %).
  specks.py: clean.

## Batch critic round 3: 75.25 (A 80.5, fresh 70.0), no critical failure: gain (+3.35 on B1's 71.9)

## FINAL: batch critic round 3 (own round 6, commit f71a9c87)
The best round and the current code (its gun houses pass barrel_key=
'nm_barrel_dk' and drum=True: the B3 behaviour as options in the helpers,
default off for the Destroyer and Cruiser). final.png / final_red.png
(rows: shipped, final, Cruiser, Carrier) in
ArtGen/units3d/reviews/realism/battleship/ and the handoff review folder.
Tint per direction (columns 1-8): 5.1 / 5.1 / 5.0 / 5.1 / 5.8 / 5.8 / 5.5
/ 5.0 % (mean 5.3; stock 6.9); footprint 6695 px (stock 6740); ppm 68.41.

Critic history (average; A / B):
- B1 71.9 (72.5 / 71.25)
- B2 70.5 (77.0 / 64.0)
- B3 75.25 (80.5 / 70.0) = FINAL

Later pass: separate dark-barrelled 5" twin mounts; a stronger belt
line; faired, capped funnels without white rims; sloped turret faces.

Finals gate (GPU re-render at HEAD vs the B3 sheets, every flic and tint layer): mean 6.2e-09, max 1/255: PASS; specks.py clean.

Fit fix (draw size only; geometry, materials and civ colour unchanged): fit_scale 0.955 -> 1.0 in BATTLESHIP.
fitcheck bbox-area ratio 0.89 -> 0.97 (dirs 2/6 width 25 -> 19 -> 20: the Iowa's beam is narrower bow-on than
the stock's); side view (dirs 4/8) 1x width shipped 130, before 127, after 133. specks.py clean. Finals
re-rendered at HEAD into sheets_fit (ppm 71.64), the gate's reference now; naval-modern finals gate (4 ships)
PASS: Battleship vs sheets_fit mean 4.8e-09, max 1/255.
