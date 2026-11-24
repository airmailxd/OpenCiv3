# Jet Fighter (realism pass): design

A MiG-29 Fulcrum (1980s): the drooped nose and bubble canopy, big
leading-edge root extensions, widely spaced engines with the intakes under
them, twin fins canted outward on the nacelles, missiles under the wings.
Two-tone grey (hard-edged darker patches), lighter underside.
Neighbours: the stock Tank and Helicopter.

## Baseline (as shipped)
Very pale blue-grey splinter camo with civ-colour blotches mixed into the
camo (a noise pattern: PLAN.md point 3). Tint 5.1-9.8 % (mean 7.2).

## What must read at 1x
1. The twin canted fins, the swept wing and LERX, the long nose.
2. Civ colour in two places: the fins' upper halves (where the stars go)
   and the wing-tip panels. Aim 6-9 %.
3. Mid-grey values (the shipped read near-white at 1x).

## Build rounds (own, before batch critic round 1)
- R1: two-tone grey with hard patches, civ fins and wing tips, no
  blotches; the fins' faces were long (two stations) so whole fins went
  civ: fins resampled (step 0.15), wings (step 0.2).
- R2: paint darkened twice (#6c7880 / #4c5862): the shipped read near
  white at 1x. Fin band from z 2.2.
- R3: the MiG's own layout: engines wider apart (1.25), fins on the
  nacelles (1.55) canted 12 degrees, so it no longer reads as the F-15.
- R4: specks.py: 2 px fin tips in direction 2 (edge-on): fin tip 0.1 of
  chord thick. Clean.
Batch 1 sheet: tint 6.8 / 3.7 / 6.8 / 8.5 / 6.3 / 3.7 / 6.3 / 8.5 % (mean
6.3; shipped 7.2). Footprint 101.9 %.

## Batch critic round 1: 70.1 (A 72.25 / B 68.0), CRITICAL: a clone of the F-15 at 1x
## Batch round 2 build: the MiG's own shape and livery
- Livery: grey #7c868c with blue-grey #56687e patches (the more saturated
  blue-grey variant competed with a blue civ colour).
- Drooped nose, the long thick root extensions, a dorsal hump behind the
  canopy (spine body), wedge intakes under the extensions with raked
  mouths, engines at 1.35 m with a narrow centre body (the open tunnel
  between them), fins canted 18 degrees on the nacelles.
- Civ: the fins' upper halves (from z 1.9) and wing-tip panels (from
  4.6 m); panel lines (#4a5664) on the wings and spine.
Tint 8.6 / 5.5 / 8.6 / 10.2 / 7.7 / 6.1 / 7.7 / 10.2 % (mean 8.1).
Footprint area 101.5 %. specks.py clean.

## Batch critic round 2: 78.25 (A 80.25 / fresh 76.25); "still F/A-18-like"
## Batch round 3 build (last round)
- Nose drooped harder (tip 0.7 m below the canopy line); the dorsal hump
  0.75 m high from the canopy back; root extensions 5.6 m chord and 0.14
  thick from the intakes to the canopy.
- Intakes dropped 0.25 m under the extensions; engines at 1.65 m with a
  0.9 m centre body: the open tunnel shows from above and behind; fins on
  the nacelles (1.9 m) canted 28 degrees.
- Wings 0.1, tailplanes 0.08, fins 0.1-0.13 thick; a lighter panel bay
  on the wings.
Tint 7.9 / 6.3 / 7.9 / 8.4 / 6.5 / 7.1 / 7.0 / 8.4 % (mean 7.4). fitcheck:
bbox-area 0.98 (min 0.92 dir 6) ok. specks.py clean.

## Batch critic round 3: 76.6 (continuity 83.25 / fresh 70.0): below B2
The fresh critic read the B3 wing as undersized and the tunnel between the
engines as a detached-looking slit.

## FINAL: batch round 2 (78.25; commit b251b7a5), restored
Scoped restore: only jet_fighter() and its AIRCRAFT entry went back to
their b251b7a5 text; the round-3 shared options (canopy frame_r,
intake_drop, 'air_panel', radome colour) all default to the B2 behaviour
and the entry does not opt in. The HEAD render matches the B2 rows of
batch_air_2.png / _red.png exactly (mean 0, max 0; aligned by the shipped
row). fitcheck: bbox-area 0.97 (min 0.94 dir 5) ok. Tint 8.6 / 5.5 / 8.6 /
10.2 / 7.7 / 6.1 / 7.7 / 10.2 % (mean 8.1). final.png / final_red.png:
shipped, final, stock Tank, stock Helicopter. On the air gate.

Critic history (average): B1 70.1 (critical: F-15 clone), B2 78.25 = FINAL,
B3 76.6.

Later pass: a larger wing span and root chord; a shaded underside joining
the engines (no open slit); a stronger hump and nose droop (4, 8); sharp
splinter camouflage.
