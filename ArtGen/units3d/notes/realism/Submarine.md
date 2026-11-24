# Submarine (realism pass): design

The Submarine is a Gato-class fleet boat of 1943: 95 x 8.3 m (L:B ~11.5),
riding low on the surface: the saddle tanks' rounded tops at the
waterline, a narrow flat deck casing with rows of limber holes, a raked
bow, the cut-down fairwater with its bridge, periscope shears and SJ radar
mast, gun platforms fore and aft (40 mm, 20 mm), the 5-inch deck gun
forward. Neighbours (stock): Destroyer and Transport.

## Baseline (as shipped)
Tint: 13.7 / 9.9 / 13.9 / 16.3 / 14.2 / 9.7 / 14.2 / 16.2 % (mean 13.5).
Values: mean 76, p10 33, p50 76, p90 116, std 33, upper 75 / lower 77.
Footprint 2344 px. specks.py: clean.

## What must read at 1x
The long low dark hull with the flat casing (a lighter deck line), the
fairwater as the one tall shape with the periscope shears above it, the
deck gun forward; the wash at the waterline. Distinct from the Nuclear
Submarine by the flat casing, the stepped tower and the gun.

## Civ colour (stock 13.5 %)
The fairwater (its shell and the raised bridge), as the stock.

## Rounds
New builder ships.submarine_gato in its own block; BUILDERS['Submarine']
set after the dict.
- Round 1: hull lofted from the casing, saddle tanks and keel; limber
  holes; fairwater, gun platforms, shears, the 5-inch gun. Pale (values
  mean 99) and the tower small: tint 3.5 %.
- Round 2: hull #2c3032, B 0.22, deck 0.05, a bigger fairwater with the
  gun platforms' bulwarks in civ too (one region), fit_scale 1.12. Tint
  7.9 %, footprint 2344, values 82.
- Round 3: fairwater 0.29 x 0.1 x 0.11 tall. Tint 10.6 %.
- Round 4 (GPU, batch 1): fairwater 0.33 long. Tint per direction: 12.7 /
  6.3 / 12.9 / 14.8 / 12.6 / 6.0 / 12.6 / 14.8 % (mean 11.6; stock 13.5,
  the end-on views 6 vs 10). Values: mean 81, p10 39, p50 83, p90 115,
  std 30, upper 78 / lower 81 (stock 76, 75 / 77). ppm 97.11; footprint
  2345 (stock 2344). specks.py: clean.

## Batch critic round 1: 60.25 (A 63.0, B 57.5), no critical failure
The civ fairwater and gun tubs read as a blue blob; flat one-value
shading; the deck gun lost at 1x (1/4/5/8); a pale halo along the hull
bottom; near-invisible in 2/6. Asked: a tall, narrow, slab-sided,
stepped Gato conning tower in grey with open railed gun platforms,
taller shears, civ only as a band; a barrelled 4"/5" gun with a shield.

- Batch 2 build rounds (b2a-b2d): the tower a slab-sided stadium plan
  (0.13 long, 0.062 wide, 0.14 tall) in grey #555a5e, a raised bridge
  screen with windows, a civ band round its upper half; gun platforms
  fore and aft on narrow trunks with open railings (posts and a top
  rail), a twin 40 mm aft and a shielded 20 mm forward; shears 0.11 tall
  with periscopes to 0.175 and the SJ radar; the 4-inch gun with a
  sloped shield, breech and a 0.18 barrel in a lighter gun grey; civ
  spread as markings: the casing's sides (a stripe), a 0.03 stripe down
  the casing's centreline, recognition panels fore and aft; the
  waterline a dark wet line, foam only at bow and stern; fit_scale 1.1.
  Sent to batch 2 (GPU, b2d): tint 9.3 / 7.0 / 9.3 / 8.8 / 9.3 / 7.2 /
  9.3 / 8.8 % (mean 8.6; stock 13.5: the civ now only as markings, below
  the stock's poured fairwater). Values: mean 70, p10 37, p50 69, p90
  105, std 25, upper 77 / lower 68 (stock 76). Footprint 2360 (stock
  2344, +0.7 %). specks.py: clean.

## Batch critic round 2: 71.4 (A 76.25, fresh 66.5), no critical failure
Civ stripes read as racing stripes; asked: the casing stripes cut back to
the recognition panels, civ on the bridge sides; railed tubs with a
visible mount; a thicker barrel and shield; a raked, flared Gato bow and
a tapered stern (heading in 4/8); taller, darker shears.

- Batch 3 build rounds (b3a, b3d): casing stripes gone; recognition
  panels 0.22 / 0.2 long fore and aft; the bridge screen taller with civ
  sides; gun platforms as oval tubs (deck, low bulwark, a rail on posts)
  with big mounts; the 4-inch barrel r 0.016 and a 0.095 x 0.068 shield;
  bow rise 0.06, rake 0.9, the casing flaring into the bow's shoulders;
  the stern a long taper (p 1.15) with the deck dropping 0.03; the shears
  0.15 tall in the dark grey, periscopes to 0.235; fit_scale 1.135.
  Sent to batch 3 (GPU, b3d): tint 6.3 / 6.0 / 6.4 / 6.2 / 5.9 / 3.8 / 5.9
  / 6.2 % (mean 5.8; stock 13.5: civ now only the tower band, bridge
  sides and the two recognition panels, as the critics asked). Values:
  mean 70, p10 32, p50 68, p90 109, std 29, upper 74 / lower 68 (stock
  76). Footprint 2338 (stock 2344, -0.3 %). specks.py: clean.

## Batch critic round 3: 76.25 (continuity 83.5, fresh 69.0), no critical failure

## FINAL = batch round 3 (b3d, code at 20470727), 76.25 (3-round cap)
Rounds: batch 1 60.25, batch 2 71.4, batch 3 76.25 (the best). Final
sheets: reviews/realism/submarine/final.png and final_red.png (shipped,
final, Destroyer and Transport). Tint 5.8 % (stock 13.5), footprint
2338 (stock 2344).

Later pass (the critics' last open points):
- the hull and freeboard ~1.5x thicker (the fresh critic reads it as 20:1);
- a larger deck gun with a visible barrel;
- a solid civ mass over the fairwater's face and top, toward ~10 %;
- simplify the gun tubs into clear railed rings.

Finals gate (GPU re-render at HEAD, scratch/naval-carriers/gate.sh): PASS; diff against final.png mean 6.1e-09, max 0.004 (red the same); specks.py: clean.
