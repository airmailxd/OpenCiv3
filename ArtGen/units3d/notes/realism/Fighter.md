# Fighter (realism pass): design

A Supermarine Spitfire Mk IX (1943): the elliptical low wing with its 6
degree dihedral, the slim fuselage with the long Merlin nose and six
exhaust stubs a side, the framed canopy and its rear fairing, symmetrical
radiators under the wings, two 20 mm cannons a wing, the four-blade Rotol
prop. Real: span 11.2 m, length 9.5 m (model 11.9 / 9.0). Built as a
painted 1/48 tabletop miniature in the RAF day-fighter scheme: ocean grey
with big hard-edged dark-green disruptive patches, medium sea grey below.
Neighbours on the sheets: the stock Tank and the stock Helicopter.

## Baseline (as shipped)
Fine-grained grey/green noise camo (reads as a digital pattern, not RAF
disruptive camo), blobby civ ovals on the wings and fuselage plus civ
blotches in the camo (a noise pattern: PLAN.md point 3). Tint 16.8-24.7 %
(mean 19.2).

## What must read at 1x
1. The elliptical wing in plan, the long nose and the prop.
2. Civ colour in three deliberate places: white-ringed civ roundels on the
   upper wings, the fuselage band (where the sky band goes) with small
   side roundels, and the spinner. No camo blotches in civ colour.
3. The big disruptive camo patches (object-space noise at scale 0.32,
   hard edges), the paler underside.

## Civ colour
Stock air class (installed sheets, tint.py): Fighter 19.2, Bomber 11.5,
Jet Fighter 7.2, F-15 5.0, Stealth Fighter 6.0, Stealth Bomber 8.7. Aim
here 9-12 % (less than the shipped, deliberate regions only).

## Footprint
render_vehicles.fit scales the model to the original's per-direction
boxes (calibration/Fighter.json), so the footprint follows the stock box.

## Build rounds (own, before batch critic round 1)
- R1: the RAF scheme (big disruptive patches), white-ringed civ roundels,
  civ fuselage band and spinner, no civ blotches; yellow leading-edge
  strips came out as yellow specks at 1x: dropped.
- R2: wing faces resampled (step 0.12, 24 chord segments) so the roundels
  come out round, not stair-stepped; ring 0.2.
- R3: darker ocean grey (#62666a) and dark green (#3c4632), roundel r 0.62
  (the 0.7 ones dominated); blades 0.18 r wide.
- R4: specks.py found 2 px pieces in 5 and 7: the cannons floated 0.17 m
  above the wing (z was not on the chord line); now on the chord line,
  r 0.065. specks.py clean.
Batch 1 sheet: tint 8.5 / 7.7 / 8.5 / 9.8 / 8.1 / 6.5 / 8.1 / 9.8 % (mean
8.4; shipped 19.2). Footprint (opaque area vs shipped) 99.3 %.

## Batch critic round 1: 81.1 (A 84.25 / B 78.0), no critical failure
80+ = one solo round. Fixes asked: sharper ocean grey / dark green contrast
against the grass, panel and aileron lines; enough mass in the Merlin nose,
exhausts and radiators to read in 4 and 8.

## Solo round 1 (review/fighter/solo1)
- Ocean grey #666a6e (lighter), dark green #323c2a (darker); a light-line
  variant (#9a9ea0) read as white decals, so the lines are dark (#2e322e):
  aileron and flap hinge lines, aileron ends, the spar line, four fuselage
  frames (face keys on the fine wing faces; panel-line helper `lines()`).
- Nose deeper (half-height 0.6-0.66 m), six bigger exhaust stubs on a sooty
  manifold, deeper radiators (0.42 x 1.3 x 0.34 m) with dark mouths, the
  carburettor intake under the nose.
Tint 8.5 / 7.6 / 8.4 / 9.6 / 8.0 / 6.4 / 8.1 / 9.7 % (mean 8.3). Footprint
100.5 % (area), specks.py clean.

## Solo round 1 critics: 85.9 (A 86.75 PASS / fresh 85.0 PASS), no critical failure

## FINAL: solo round 1 (commit b251b7a5)
The first air final. reviews/realism/fighter/final.png and final_red.png
are the solo-round sheets (rows: shipped, final, stock Tank, stock
Helicopter). Reproduced at HEAD with verify.sh: mean 0, max 0 (blue and
red). It is on the air gate (scratch/air/verify/verify_all.sh).

Critic history (average; A / B):
- Batch 1: 81.1 (84.25 / 78.0)
- Solo 1: 85.9 (86.75 / 85.0) = FINAL, PASS

Optional points for a later pass: chunkier exhaust stubs; darker panel
lines; the wing and fuselage camo patches do not line up (the camo is
object-space noise, so the patches cross the wing root at random).
fitcheck (lead's rule): bbox-area 0.98 (min 0.97 dir 3) ok. Re-verified
after the round-3 shared changes: mean 0, max 0.
Air gate at the air family's close (all six finals at HEAD): Fighter mean
0 max 0; Bomber, Jet Fighter, F-15, Stealth Fighter, Stealth Bomber mean
<= 1.4e-8, max <= 0.008, no pixel over 0.1 (blue and red).
