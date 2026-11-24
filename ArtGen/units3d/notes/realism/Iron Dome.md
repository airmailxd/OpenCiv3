# Iron Dome (League modern mod; realism pass, numpy-vehicles): design

Identity (NumPy model `battery`): the Iron Dome battery's launcher: a box of
twenty Tamir interceptor cells raised steeply. The NumPy model put it on a
6x6 truck; the brief asks for the real thing: the launcher trailers.
Shipped tint 1.3 %.

## Real object
Iron Dome (Rafael, 2011-): a battery is a radar (EL/M-2084), a control
unit and three or four launchers. Each launcher is a towed twin-axle
trailer, unhitched and levelled on outrigger jacks when deployed, its box
of 20 Tamir cells (4 x 5) hinged at one end and raised steeply on two
rams; IDF sinai grey. Two launchers stand in this unit (the battery).

## Build
`armor_mod._iron_dome`: two `_launcher_trailer`s staggered side by side,
built in metres: twin axles with off-road tyres, frame rails, bed,
mudguards, the A-frame drawbar with its eye and parking leg, an electronics
cabinet at the front, four outrigger arms to jacks on pads; the box hinged
at the rear (62 degrees), ribs, an access door, 20 pale cell covers in a
dark frame on its top face, two rams. fit_scale 1.1.

## What must read at 1x
1. The two raised boxes with the grid of pale cell covers.
2. Trailers on jacks (wheels, drawbars, outriggers): towed, not trucks.

## Civ colour (shipped 1.3 %; aim 3-5 %)
A panel on each side of each box.

## Rounds
- d1: pale, small (pixels 0.88), trailers overlapping in dir 0.
- d2: darker sinai grey, fit_scale 1.1, trailers staggered the other way.
- d3: the cabinet in the box paint (it read as a white fridge).
- Critic round 1: + civ bands on the box backs and cabinet fronts. Tint
  3.1 / 1.7 / 2.7 / 3.8 / 2.6 / 0.0 / 1.9 / 3.5 % (mean 2.4; shipped 1.3);
  fitcheck bbox 1.32 (min 1.00), pixels 1.09; specks clean.
- Box hinged at the rear, leaning back, cells up and aft (as the shipped).

## Critic round 1: 70.0 (A 72 / B 68); both: CRITICAL civ unreadable from the rear
Round 2 (lead: ONE launcher): a single heavy trailer, no truck. Bed at
1.45 m on deep side beams (open over the wheels), frame rails 0.3 x 0.45,
tyres r 0.56, drawbar tubes r 0.13 with a big eye, outrigger arms 0.32
square, jack housings, legs r 0.12, pads r 0.38, rams r 0.16/0.09; the box
3.4 x 1.95 x 1.6. Cell face: 20 dark cell mouths with grey caps (cell_cap
#8a877e) in the dark frame. Civ in every direction: a frame round the cell
face, panels on the box's sides, top and belly, a band across the back, the
cabinet front, a sleeve on the drawbar, a band across the bed's tail.
Critic round 2: tint 5.1 / 4.0 / 5.0 / 4.9 / 6.1 / 7.1 / 6.1 / 4.9 % (mean
5.4, min 4.0); specks clean; fitcheck vs the NumPy sprite: pixels 0.96, bbox
1.09; vs stock: 0.90 of the Mobile SAM, 1.02 of the Radar Artillery (stock
range: Radar Artillery 0.85 to Mobile SAM 0.97 of the Modern Armor; the
launcher is 0.87).

## Critic round 2: 81.0 (A 85 / B 77), rear civ critical CLOSED
Round 3 (the last): the box in a darker paint (turret_tone 0.9 -> 0.74) with
a second, darker camo layer over the whole unit; heavier drawbar (r 0.17),
side beams 0.5 -> 0.72 deep, legs r 0.17 with outer jack tubes; ribs across
the box's broad faces and its hinge end, a junction box and cable runs on its
belly; the civ outline round the cell face 0.07 -> 0.045; fit_scale 1.05.
Critic round 3: tint 4.1 / 3.1 / 4.0 / 3.8 / 5.4 / 5.5 / 5.4 / 3.9 % (mean
4.4); specks clean; vs NumPy pixels 1.10; vs stock 1.03 of the Mobile SAM,
1.16 of the Radar Artillery (about 1.0 of the Modern Armor).

## Critic round 3: 83.5 (A 87 / B 80), no critical. FINAL = B3 (1f1a990e), the best round
Rounds: B1 70.0 (rear civ critical), B2 81.0 (closed), B3 83.5.
Finals: reviews/realism/iron-dome/final.png (+_red): shipped, final, Modern
Armor, Mech Infantry. Size vs stock: 1.03 of the Mobile SAM, 1.16 of the
Radar Artillery (about 1.0 of the Modern Armor).
Later pass: tone the box and the trailer bed to the stock desert tan (the bed
reads near-white in E, W and the diagonals); fill the open frame under the
box with an equipment block or generator so the E/W silhouette is solid.
