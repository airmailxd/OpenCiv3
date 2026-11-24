# Laser Tank (League modern mod; realism pass, numpy-vehicles): design

Identity (NumPy model `laser_tank`): an original design, a low wedge-hulled
tank on one long track a side behind bolted skirts, a low turret, and above
it the beam projector that makes its silhouette: a long raised emitter
housing finned along its top and flanks, capacitor drums at its root, a
ringed lens head. Shipped tint 3.0 %.

## Grounded reading
Kept plausible: a modern MBT chassis (7 road wheels, rear sprocket, full
skirts, long glacis) and the emitter as hardware, not sci-fi: an armoured
beam-director housing on a pylon, cooling fins, capacitor drums, cable
bundles into the pylon, a ringed barrel and a big dark optical lens (like
the beam directors on today's vehicle-mounted laser demonstrators, scaled
up). No glow, no beam in the still.

## Build
`armor_mod._laser` on `armor.chassis` (cfg in armor_mod.UNITS_CFG): hull kit
(glacis plates, the driver's hatch, sensor heads, lights, long grille deck),
6 bolted skirt panels a side with dirt, a low faceted turret in the darker
paint with a bustle and smoke banks, and the emitter on its own part.

## What must read at 1x
1. The long finned emitter housing over the low turret, projecting forward
   past the hull's glacis, with the dark lens and civ collar.
2. The low wedge hull on skirted tracks (a tank, not an APC).

## Civ colour (shipped 3.0 %; aim 3-5 %)
The lens collar, a band on each turret side, a band across two forward
skirt panels each side.

## Rounds
- d1: emitter short and squat over the turret; antenna specks; tint 1.6 %.
- d2: emitter 1.0 long, set forward (projects past the turret front),
  antennas off, bigger civ bands; tint 2.2 %.
- d3: lens 0.08 with a deeper collar, fewer fins. Tint 2.4 %.
- Critic round 1: tint 2.4 / 1.2 / 2.4 / 3.1 / 2.6 / 1.8 / 2.6 / 3.1 % (mean
  2.4; shipped 3.0); fitcheck bbox 0.99, pixels 0.98; specks clean.

## Critic round 1: 70.5 (A 71 / B 70); A: CRITICAL identity (radiator box / rocket pod)
Round 2 rebuild:
1. Emitter: no pylon; a long thick ringed housing out of a mantlet in the
   turret front, past the hull front like a tank gun (its own part, out of
   the fit), ending in a flared aperture with the civ collar, a gunmetal rim
   and a big dark lens.
2. Turret: massive (0.98 x 0.9 x 0.2) on a wide dark turret ring; the fins
   moved to a heat exchanger on the roof's rear; capacitor drums on the bustle.
3. Hull: L 2.2 -> 1.88 (-15 %), 6 road wheels, H 0.08 -> 0.12, a steeper
   glacis (front 0.5, nose 0.2), the skirts lower (0.55) in the darker paint.
4. Civ: side panels 0.6 x 0.6 of the turret, panels on both front cheeks, a
   panel on the bustle's back, the lens collar, one skirt band a side.
fit_scale 0.93. Critic round 2 (batch_numpyvehicles_2): tint 3.7 / 1.2 / 3.7 /
4.5 / 5.1 / 4.3 / 5.0 / 4.5 % (mean 4.0); specks clean; fitcheck vs the
NumPy sprite: pixels 0.98, bbox 1.04; vs stock: 1.02 of the Tank, 1.10 of the
Modern Armor.

## Critic round 2: 83.5 (A 83 / B 84), identity critical CLOSED
Round 3 (the last): emitter radius 0.062 -> 0.093, the collars only as a
heat-sink jacket over its rear half (0.16-0.42), lens 0.11 -> 0.14 with the
dark glass at 0.9 of it; depressed 6 degrees; the emitter casts no shadow
(its cut shadow read as a stick on the ground); turret 10 % lower (0.18);
the side civ panels cut to plates 0.5 x 0.36 of the side, skirt bands on two
panels a side. Critic round 3 (batch_numpyvehicles_3): tint 2.3 / 1.1 / 2.3 /
2.9 / 3.5 / 3.5 / 3.5 / 2.9 % (mean 2.8); specks clean; vs NumPy pixels 0.99;
vs stock 1.03 of the Tank, 1.12 of the Modern Armor.

## Critic round 3: 86.0 (A 86 / B 86), PASS. FINAL = B3 (1f1a990e)
Rounds: B1 70.5 (identity critical), B2 83.5 (closed), B3 86.0.
Finals: reviews/realism/laser-tank/final.png (+_red): shipped, final, Modern
Armor, Mech Infantry. Size vs stock: 1.03 of the Tank, 1.12 of the Modern Armor.
Later pass: the emitter's base ribs still read a little coil-like in D1, D2.
