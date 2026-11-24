# Destroyer (realism pass): design

The Destroyer is a Fletcher-class destroyer of 1943 (USS Fletcher, DD-445):
114.8 x 12.0 m (L:B 9.6), flush-decked with a strong sheer forward, five
single enclosed 5"/38 mounts (Nos. 1-2 forward, 3-5 aft; 2 and 3
superfiring), two raked round funnels, two quintuple torpedo-tube mounts
on the centreline, the bridge with the boxy Mk 37 director on top, a pole
foremast with its radar. Built in metres and scaled (ships.destroyer_fletcher,
DESTROYER; beam 13 m, a touch over real, so it reads end-on). Neighbours
(stock): Cruiser and Transport.

## Baseline (as shipped)
Tint (tint.py, DEFAULT): 9.4 / 9.7 / 9.6 / 8.7 / 8.5 / 5.3 / 8.6 / 8.6 %
(mean 8.5). Values (1x, opaque non-tint): mean 106, p10 45, p50 95, p90
182, std 50, upper 128 / lower 100. Footprint 3721 px (2x, alpha>127, mean
per direction). specks.py: 2-3 pieces in 1-3 (pennant specks).

## What must read at 1x
The long, low, sharp hull with the high bow; the two funnels (the
destroyer's signature); the gun houses fore and aft; the bridge with its
director as the one tall block forward.

## Values
A dark painted hull (#4c5055, Measure 22's dark band to the deck), a
black boot topping, a mid-grey deck, light haze-grey houses and mounts
(#a2a6a8), sooty funnel caps with a light lip: dark below, light above,
as the stock (upper 128 / lower 100).

## Civ colour (stock 8.5 %)
1. The gun houses' roofs (the period air-recognition panels): five.
2. A band round each funnel's top third, under the soot.
3. The ensign on the stern staff (solid).

## Rounds
New builder ships.destroyer_fletcher (DESTROYER) in its own block; the
helpers are this builder's own (_nm_*, one block before the shared steel
ships); BUILDERS['Destroyer'] set after the dict. No shared helper or
default changed (block check: one per unit).
- Round 1: at real heights the ship was a plank at 1x (the stock stands
  ~2x taller than real): everything above the waterline now stretched by
  zx 1.7 (the tabletop exaggeration, all four ships); beam 13 -> 15 m.
- Round 2: hull darker; reads, but the bridge low and the 0.42 m mast
  broke into specks; tint 4.5 %.
- Round 3: a director pedestal raises the bridge; mast r 0.7 m, 21 m; a
  civ air-recognition panel on the forecastle; funnel bands to 0.45-0.84
  of the height; ensign 9 x 5.6 m. The bow number "445" read as a glyph at
  1x: dropped (the WWII ships carry none).
- Round 4: Measure 22: the hull dark below 3.8 m (pre-stretch), haze grey
  above (the bow flare), so the hull has a light top strake. Sent to batch
  critic round 1 (batch_navalmodern_1). Tint per direction (columns 1-8):
  7.6 / 5.7 / 7.0 / 7.5 / 7.8 / 7.5 / 7.2 / 7.5 % (mean 7.2; stock 8.5).
  Values: mean 107, p10 45, p50 104, p90 174, p99 211, std 49, upper 117
  / lower 104 (stock 106, 128 / 100). ppm 72.51; footprint 3694 px (stock
  3721, -0.7 %). specks.py: clean.

## Batch critic round 1: 67.0 (A 65.0, B 69.0), no critical failure
Shared (all four): stacked plain boxes; thin dark barrels that vanish at 1x;
flat hulls (plating, a darker waterline, a deck tone; the hull near-white
to B: a step toward haze grey, keep the light upper half); civ colour in
the same spots on every ship (roofs, funnel bands, big stern flags):
vary it per class, shrink the flags; masts drawn as hairlines. Destroyer:
funnels raked with caps; 5" houses with rounded/sloped fronts and lighter
barrel stubs; more sheer and flare.

- Round 5 (shared, own helpers only): palette a step toward haze grey
  (hull #4e5357, haze #7e8286, houses #8e9296, deck #5a5e60); light barrels
  ('nm_barrel' #9a9ea0) with dark muzzles; gun houses with sloped sides
  (roof 0.8 of the base) and a longer sloped face, or a rounded front in
  plan; the waterline band 1.4 m; a dark plating seam along the side;
  ensigns about half the size.
  Destroyer: funnels raked 0.26 m/m with heavier cap lips; the five 5"
  mounts in rounded houses, grey roofs, barrels r 0.36 m; sheer forward
  5.2 m, rake 9 m, 12 % flare; civ colour moved to the funnel bands
  (0.26-0.72 of the height, the squadron bands) and recognition panels on
  the forecastle and the fantail; mast r 1.0 m; fit_scale 0.965.
  Sent to batch critic round 2. Tint per direction (columns 1-8): 6.0 /
  4.4 / 6.0 / 6.2 / 6.7 / 8.4 / 6.8 / 6.2 % (mean 6.3; stock 8.5). Values:
  mean 107, p10 42, p50 108, p90 174, p99 216, std 50, upper 119 / lower
  105 (stock 106, 128 / 100). ppm 70.19; footprint 3705 px (stock 3721,
  -0.4 %). specks.py: clean.

## Batch critic round 2: 70.25 (A 73.0, fresh 67.5), no critical failure
Both: the light barrels vanished against the light houses (the batch-1
note was wrong): barrels darker than their mounts and longer. Shared: the
three gun ships read as scaled copies (destroyer and cruiser confused at
1x): push the silhouettes apart; one funnel per class; superstructures
still plain boxes; civ low across the batch. Destroyer: dark barrels on
sloped-front houses, funnels raked 2-3 px at 1x, shorter and capped, a
stepped bridge with a director.

- Round 6 (last): barrels dark (#3a3d40) for every ship; barbettes flat-
  shaded straight drums. Destroyer: sloped-front gun houses (face 0.38 of
  the length), civ roofs again, barrels r 0.42 m, 8 m; funnels 8.0 / 7.4 m
  tall, raked 0.45 m/m, flared caps with a heavy lip, bands 0.22-0.66; a
  bigger stepped bridge (pilot house 0.7 of the beam, wings 0.96, the
  director 4.6 x 5.0 x 3.4 m on its pedestal). Sent to batch critic round
  3. Tint per direction (columns 1-8): 6.1 / 4.7 / 6.1 / 6.1 / 6.7 / 7.6 /
  6.9 / 6.2 % (mean 6.3; stock 8.5). Values: mean 104, p10 40, p50 103,
  p90 168, p99 211, std 49, upper 116 / lower 102 (stock 106, 128 / 100).
  ppm 70.19; footprint 3714 px (stock 3721, -0.2 %). specks.py: clean.

## Batch critic round 3: 68.25 (A 73.0, fresh 63.5): below B2 (3-round cap)

## FINAL: batch critic round 2 (own round 5, commit 32d1d04e), restored
The best of the three batch rounds. The B2 block restored verbatim (scoped:
only this unit's and the Cruiser's blocks, from the B2 copy); the B3 helper
changes are options defaulting to the B2 behaviour (light 'nm_barrel',
smooth barbette drums, the plain funnel rings), switched on only in the
Battleship's and the Aegis's entries. Reproduction (finals gate, GPU, at
HEAD): see the gate line below. final.png / final_red.png (rows: shipped,
final, Cruiser, Transport) in ArtGen/units3d/reviews/realism/destroyer/
and the handoff review folder. Tint per direction (columns 1-8): 6.0 /
4.4 / 6.0 / 6.2 / 6.7 / 8.4 / 6.8 / 6.2 % (mean 6.3; stock 8.5);
footprint 3705 px (stock 3721); ppm 70.19.

Critic history (average; A / B):
- B1 67.0 (65.0 / 69.0)
- B2 70.25 (73.0 / 67.5) = FINAL
- B3 68.25 (73.0 / 63.5)

Later pass: dark barrels on the 5" houses that read at 1x (3-4 px);
rounded house fronts; raked funnels with dark caps, no white rims; a
taller stepped bridge.

Finals gate (GPU re-render at HEAD vs the B2 sheets, every flic and tint layer): mean 1.5e-08, max 2/255: PASS; specks.py clean.
