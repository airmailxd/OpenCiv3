# Stealth Fighter (realism pass): design

A Lockheed F-117A Nighthawk: the faceted arrowhead (a diamond-section
fuselage, every side a flat facet, thin wings swept 67.5 degrees), the
faceted canopy, gridded intakes on the shoulders, the platypus slot
exhaust and two big V tails canted outward, no tailplanes. Real: length
20.1 m, span 13.2 m (model ~19.6 / 13.2). Flat dark grey.
Neighbours: the stock Tank and Helicopter.

## Baseline (as shipped)
A stubby kite/diamond in plan (not the F-117's arrowhead), a civ chevron
drawn as a thin outline loop on the back (reads as a frame). Tint 5.3-7.0 %
(mean 6.0).

## What must read at 1x
1. The arrowhead planform and the V tails.
2. Facets catching the light differently (flat shading).
3. Civ colour in two places: the V tails' upper halves and the wing-tip
   panels. Aim 6-8 %.

## Build rounds (own, before batch critic round 1)
- R1: rebuilt: the arrowhead (diamond fuselage, flat shading, thin wings
  on the 67.5 degree line), faceted canopy with frames, gridded intakes,
  the platypus slot, V tails canted 40 degrees.
- R2: the V tails resampled (step 0.15; one face per tail made them all
  civ), paint #2c2e32.
- R3: civ trimmed: tails above z 2.3, tip panels from 5.4 m out.
Batch 1 sheet: tint 5.9 / 5.1 / 5.9 / 6.9 / 6.6 / 5.6 / 6.6 / 6.9 % (mean
6.2; shipped 6.0). Footprint: bbox w 97 %, h 101 %, opaque area 93.5 %
(the slim arrowhead has less area than the shipped kite in the same box).
specks.py clean.

## Batch critic round 1: 69.25 (A 68.5 / B 70.0), CRITICAL (A): a flat paper dart, out of house style
## Batch round 2 build
- facet_hull(): a hexagonal faceted hull (chines, two ridge lines with a
  narrow flat top, a flat belly), lofted without resampling so each facet
  is one flat plane; pyramid height x1.3; grey edge highlights (tubes in
  #74787e) along the chines and ridges.
- Faceted wings: wing(seg=3, smooth=False), a diamond section with a
  spanwise ridge (thickness 0.08 of chord; tip chord at least 1.0 m: the
  thinner tips broke into 1 px specks).
- The notched W trailing edge (root -8.4, notch at 2.8 m / -5.6, tip
  -7.3); the stepped faceted canopy with frames; the slot exhaust under a
  broad lip.
- Near-black #202226 (a #35383d variant showed the facets more but lost
  the F-117's black); civ on the whole V-tail panels.
Tint 12.5 / 8.7 / 12.5 / 18.1 / 13.3 / 9.9 / 13.4 / 18.1 % (mean 13.3;
stock 6.0: whole tail panels, as asked). Footprint: box w 96.5 %, h 103 %,
area 92.3 %. specks.py clean.

## Batch critic round 2: 71.75 (A 76.25 / fresh 67.25); "a thin spine on a flat plate"
## Batch round 3 build (last round)
- The pyramid widened x1.6 (chines to 3.2 m: it reaches the wing roots)
  and heightened x1.5, narrow ridges: lit and dark facet planes separate;
  paint lifted to #33363b so they read.
- V tails canted 45 degrees, rooted in the hull (they floated 0.7 m above
  it once the hull grew: a 326 px detached piece), tips 0.13 of chord
  thick (edge-on specks in 1 and 3).
- Notch deeper (3.0 m / -4.9), the slot exhaust 3 m wide under a lighter
  lip.
Tint 11.6 / 9.5 / 11.7 / 16.0 / 11.8 / 10.6 / 11.8 / 16.1 % (mean 12.4).
fitcheck: bbox-area 0.99 (min 0.87 dir 2) ok. specks.py clean.

## Batch critic round 3: 77.75 (continuity 83.5 / fresh 72.0)

## FINAL: batch round 3 (commit 4664568b)
Best of B1 69.25 (critical: paper dart), B2 71.75, B3 77.75. final.png /
final_red.png: shipped, final, stock Tank, stock Helicopter. fitcheck:
bbox-area 0.99 ok. On the air gate.

Later pass: the body back to near-black with grey only on the facet edges
and per-facet highlights; a clear pyramid hump in the side views; the
white slot exhaust toned down to dark metal.
