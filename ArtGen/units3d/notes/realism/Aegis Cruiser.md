# Aegis Cruiser (realism pass): design

The Aegis Cruiser is a Ticonderoga-class guided-missile cruiser (USS
Ticonderoga, CG-47, late 1980s): 173 x 16.8 m (L:B 10.3), the Spruance
hull (a high flared bow, flush deck), two 5"/54 Mk 45 mounts, Mk 41
vertical launchers fore and aft, the tall forward and after deckhouses
each carrying two octagonal SPY-1 arrays on their corners, two squat
paired uptakes, a tripod foremast and a pole mainmast, Phalanx domes,
Harpoon canisters, a hangar and helicopter deck aft. Haze grey all over.
Built in metres (ships.aegis_ticonderoga, AEGIS). Neighbours (stock):
Destroyer and Battleship.

## Baseline (as shipped)
Tint (tint.py, DEFAULT): 19.4 / 19.8 / 19.2 / 15.0 / 17.7 / 23.2 / 17.5 /
15.1 % (mean 18.3: big civ blocks on the hangar). Values: mean 111, p10
49, p50 110, p90 177, std 48, upper 128 / lower 105. Footprint 5319 px.
specks.py: 2-4 pieces in 1/2/3/5/7 (1 px specks).

## What must read at 1x
The tall, slab-sided deckhouses with the octagonal array faces (the Aegis
signature), few guns (two small mounts), the launcher grids, the
helicopter deck aft: a modern ship, not a gun ship.

## Civ colour (stock 18.3 %)
1. The four SPY-1 array faces (big, deliberate, one per corner).
2. A band round each uptake's top.
3. The ensign. (Option: a civ panel on the helicopter deck, if the
   amount runs short.)

## Rounds
New builder ships.aegis_ticonderoga (AEGIS), own block, the _nm_*
helpers (plus _nm_prism, _nm_spy); BUILDERS['Aegis Cruiser'] after the
dict. No shared code changed.
- Round 1: (old heights) a low pale ship.
- Round 2: zx 1.7; pale (mean 137 vs the stock's 111); the arrays lost
  inside the houses (4 % tint); the mainmast floated between the houses.
- Round 3: arrays 5.6 m, darker greys, mainmast on the after house; the
  arrays still sank into the houses' corners (only rims showed).
- Round 4: the two deckhouses rebuilt as tall slab-sided blocks with
  chamfered corners (forward corners fore, after corners aft), the arrays
  flat on the chamfers: clear octagons in every view. The bow number
  "47" read as a glyph: dropped.
- Round 5: arrays 4.8 m on 7.8 m chamfers, blocks 10.6 m; a civ landing
  area on the flight deck with a white ring; civ bands on the uptakes 2 m
  deep. The blocks read as pale boxes.
- Round 6: a dark deck-edge course round each block at mid-height (the 02
  level), houses #8a8e92 over a #54585c hull, ensign 12 x 7.5 m,
  fit_scale 0.94. Sent to batch critic round 1. Tint per direction
  (columns 1-8): 6.1 / 9.5 / 5.8 / 6.6 / 6.7 / 12.3 / 6.1 / 6.3 % (mean
  7.4; stock 18.3, almost all of it the stock's flat civ blocks: kept
  lower on purpose, the four arrays the deliberate region). Values: mean
  109, p10 54, p50 103, p90 165, p99 208, std 43, upper 125 / lower 99
  (stock 111, 128 / 105). Footprint 5221 px (stock 5319, -1.8 %).
  specks.py: clean.

## Batch critic round 1: 56.0 (A 56.0, B 56.0), CRITICAL (both): identity
The porthole-dotted white deckhouses read as a ferry or the stock
Transport. Fixes: no portholes; leaning deckhouse walls; the SPY-1 faces
grey octagons with a civ rim only (the main civ region); the two stack
groups between the deckhouses; a lattice and a pole mast; 5" mounts fore
and aft with barrels; dark launcher grids fore and aft; a marked
helicopter deck; it must read as a modern warship in 1, 2, 3, 4 and 8.

- Rounds 7-9: the shared changes (Destroyer.md); the deckhouses without
  scuttles, walls leaning in 1.2 m over their 10.6 m, the mid-height deck
  edge kept; the arrays grey (#5a5f64) octagons in a civ rim (r 1.1 m);
  the stack groups at the deckhouses' facing ends (a leaning casing, a
  civ band round its top, three black exhausts); a lattice foremast
  (legs r 0.55 m, three bracing rings, the radar) and a pole mainmast
  (r 1.0 m); Mk 45 mounts 4.8 x 7.4 m, angular, with light barrels
  (r 0.56 m, 10 m); the launchers dark patches with a light grid; the
  helicopter deck dark with a white circle, centre line and edge lines
  (the civ deck panel removed); the greys a step darker (hull #62666a,
  houses #7e8286); 8 % flare. Sent to batch critic round 2. Tint per
  direction (columns 1-8): 3.9 / 7.3 / 4.0 / 3.4 / 3.9 / 6.5 / 3.6 / 3.2 %
  (mean 4.5; stock 18.3: the rims are the deliberate region now). Values:
  mean 102, p10 50, p50 104, p90 149, p99 189, std 39, upper 114 / lower
  97 (stock 111, 128 / 105). ppm 71.10; footprint 5296 px (stock 5319,
  -0.4 %). specks.py: clean.

## Batch critic round 2: 57.25 (A 62.0, fresh 52.5); identity cleared, new CRITICAL (fresh): civ unreadable
A: it reads as a modern warship now. Fresh: the civ colour is thin dark
rings only, barely legible at 1x, nearly lost in 2 and 6. Fixes: solid civ
octagons (toward ~15 %), blue vs red legible in 2 and 6 (no "goggles");
fill the empty middle with the stack housings and a lattice mast; the
Mk 45s, launcher grids and deck marks big enough for 1x; a long,
near-level forecastle; a taller, stepped forward deckhouse with windows.

- Rounds 10-12 (last): the four array faces solid civ octagons (r 5.3 m
  on 8.6 m chamfers) standing 0.9 m proud of the leaning walls in a dark
  frame; a civ stripe (2.2 m) round both deckhouses below the arrays; a
  stack housing (0.72 of the block height) filling the middle, carrying
  both stack groups (civ bands 2.6 m) and a lattice mainmast between them;
  the bridge level 4.2 m with a window band, the wings, a level above;
  Mk 45s 6 x 9 x 4 m with dark barrels r 0.75 m, 13 m; launcher patches
  11 m wide with a light grid; the deck circle and lines ~2x heavier;
  sheer forward 5.0 -> 1.8 m, rake 6 m (a near-level forecastle). Sent to
  batch critic round 3. Tint per direction (columns 1-8): 7.3 / 8.4 / 7.5
  / 7.7 / 7.3 / 7.1 / 6.4 / 7.1 % (mean 7.3, even across directions;
  short of ~15: the arrays can't grow past the chamfers). Values: mean 105,
  p10 48, p50 105, p90 159, p99 195, std 43, upper 115 / lower 99 (stock
  111, 128 / 105). ppm 70.36; footprint 5210 px (stock 5319, -2.0 %).
  specks.py: clean.

## Batch critic round 3: 64.5 (A 69.0, fresh 60.0), no critical failure: the civ critical CLOSED

## FINAL: batch critic round 3 (own round 12, commit f71a9c87)
The best round and the current code (its Mk 45s pass barrel_key=
'nm_barrel_dk' and drum=True). final.png / final_red.png (rows: shipped,
final, Destroyer, Battleship) in ArtGen/units3d/reviews/realism/aegis-
cruiser/ and the handoff review folder. Tint per direction (columns 1-8):
7.3 / 8.4 / 7.5 / 7.7 / 7.3 / 7.1 / 6.4 / 7.1 % (mean 7.3; stock 18.3);
footprint 5210 px (stock 5319); ppm 70.36.

Critic history (average; A / B):
- B1 56.0 (56.0 / 56.0, critical: identity, "a ferry")
- B2 57.25 (62.0 / 52.5, critical (fresh): civ unreadable)
- B3 64.5 (69.0 / 60.0) = FINAL, both criticals closed

Later pass: the forward deckhouse raised above the aft one, with a bridge
window band; squat boxy stacks between the deckhouses; dark, larger VLS
grids; the 5" mounts' domes.

Finals gate (GPU re-render at HEAD vs the B3 sheets, every flic and tint layer): mean 1.3e-08, max 2/255: PASS; specks.py clean.

Fit fix (draw size only; geometry, materials and civ colour unchanged): fit_scale 0.94 -> 1.0 in AEGIS.
fitcheck bbox-area ratio 0.87 -> 0.99 (about 7 % short on every axis before); side view (dirs 4/8) 1x width
shipped 114/115, before 108, after 115. specks.py clean. Finals re-rendered at HEAD into sheets_fit (ppm
74.86), the gate's reference now; naval-modern finals gate (4 ships) PASS: Aegis vs sheets_fit mean 1.2e-08,
max 1/255.
