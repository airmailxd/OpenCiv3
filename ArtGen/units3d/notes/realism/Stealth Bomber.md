# Stealth Bomber (realism pass): design

A Northrop B-2A Spirit: the flying wing, leading edge swept 33 degrees,
the double-W sawtooth trailing edge, the raised centre body with the
cockpit windows, the engine humps with their intakes and the exhausts on
top. Real: span 52.4 m, length 21 m (model ~54 / 19). Dark grey.
Neighbours: the stock Tank and Helicopter.

## Baseline (as shipped)
The shape reads; the civ panels on the outer wings were cut by a slanted
line across coarse faces and came out stair-stepped. Tint 7.6-9.8 % (mean
8.7).

## What must read at 1x
1. The W planform.
2. Civ colour: one clean full-chord band across each outer wing (edges
   along the wing stations, so no stair-steps). Aim 6-9 %.
3. The centre body's humps and intakes.

## Build rounds (own, before batch critic round 1)
- R1: wing resampled (52 stations, 24 chord segments).
- R2: the civ panels as full-chord bands (s 16.5-19.6 m): edges along the
  stations, no stair-steps (a 17-19.2 band read too thin: 5.9 %).
- R3: the exhaust troughs behind the engine humps (dark, scorched).
Batch 1 sheet: tint 8.9 / 8.9 / 8.9 / 8.5 / 8.9 / 9.0 / 8.9 / 8.5 % (mean
8.8; shipped 8.7). Footprint 100.2 %. specks.py clean.

## Batch critic round 1: 74.5 (A 72.0 / B 77.0); "barely changed from the shipped"
## Batch round 2 build
- The double-W sawtooth trailing edge with edges parallel to the leading
  edges: centre point aft, inboard notch, mid point, outboard notch, then
  constant-chord outer panels (te points (0,-7.6) (5.5,-3.4) (11.5,-7.3)
  (15,-5.0) (26,-12.1)).
- The centre body blended up into a 1.7 m hump; the cockpit's four
  windows; one blended engine hump a side with a faired inlet slot under a
  lighter lip; exhaust plates and troughs.
- Lighter leading-edge panels (#5c6066), dark elevon hinge lines and ends,
  soft tone variation (a 0.12-scale soft camo layer #3c4045).
Tint 6.9 % in every direction (mean 6.9). Footprint area 94.9 %, specks.py
clean.

## Batch critic round 2: 77.4 (A 81.0 / fresh 73.75)
## Batch round 3 build (last round)
- Wing thickness 0.17 of chord at the root to 0.09 at the tip.
- Engines blended into the centre hump (one 1.7 x 4.6 x 0.95 m swell a
  side at 2.3 m), flush inlets with lighter lips; the cockpit windows now
  sit on the hump (they were under its skin) with light frames.
- Charcoal #33363a, lighter spanwise bays (#3b3e43), the leading-edge
  panels and elevon lines kept.
- Civ: a painted full-chord panel on each outer wing (s 19.6-23.4 m),
  edges on the face grid (a forward-chord variant read as tape).
Tint 7.7 / 7.9 / 7.7 / 7.7 / 7.7 / 7.9 / 7.6 / 7.7 % (mean 7.7). fitcheck:
bbox-area 1.04 (min 1.02 dir 1) ok. specks.py clean.

## Batch critic round 3: 85.4 (continuity 85.25 PASS / fresh 85.5 PASS)

## FINAL: batch round 3 (commit 4664568b), PASS
B1 74.5, B2 77.4, B3 85.4. final.png / final_red.png: shipped, final,
stock Tank, stock Helicopter. fitcheck: bbox-area 1.04 ok. On the air gate.

Later pass: charcoal rather than mid-grey; the cockpit visible in 4 and 8.
