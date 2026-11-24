# Nuclear Submarine (realism pass): design

The Nuclear Submarine is a Los Angeles-class attack submarine (1976 on):
110 x 10 m (L:B 11), surfaced: a long black cylinder about a third out of
the water with a flat walkway along its top, a rounded bow, the tall slim
sail well forward with its fairwater planes and masts, the stern
tapering under the wash so only the upper rudder fin and the stern
planes' tips show. Neighbours (stock): Submarine and Destroyer.

## Baseline (as shipped)
Tint: 9.1 / 6.4 / 9.6 / 10.0 / 9.5 / 6.3 / 9.9 / 10.0 % (mean 8.8).
Values: mean 82, p10 38, p50 81, p90 123, std 31, upper 82 / lower 82.
Footprint 2076 px. specks.py: clean.

## What must read at 1x
The rounded whale-back hull (lit top, dark flanks) as the one long shape,
the tall sail forward with its planes, the rudder fin aft; no gun, no
deck casing (that is the WWII Submarine's).

## Civ colour (stock 8.8 %)
The sail's sides (its cap dark), as the stock.

## Rounds
New builder ships.nuclear_sub_la in its own block; BUILDERS['Nuclear
Submarine'] set after the dict.
- Round 1: a lofted cylinder (R 0.112) with the stern riding up to stay
  awash, a foil-plan sail, planes, rudder fin, a waterline wash. Hull
  read pale grey (values mean 100), sail small (tint 5.5 %), footprint
  1606.
- Round 2: hull #26292c, R 0.12, more out of the water, sail 0.26 x 0.06
  x 0.18, fit_scale 1.1. Values 83, tint 7.7 %, footprint 2017; 1-2 px
  specks at the stern planes' tips.
- Round 3: planes thicker and shorter, a wash spreading aft past the fin,
  sail 0.27 x 0.068 x 0.195. Tint 8.8 %; specks at the wash's thin ends.
- Round 4: the wash ends blunt; specks now the tail cone past the planes.
- Round 5 (GPU, batch 1): the tail cone dives under the wash past
  s = -0.9. Tint per direction: 9.1 / 3.6 / 9.1 / 12.8 / 9.4 / 3.4 / 9.2 /
  13.3 % (mean 8.7; stock 8.8). Values: mean 84, p10 40, p50 88, p90 119,
  std 30, upper 73 / lower 84 (stock 82, 82 / 82). ppm 87.40; footprint
  2117 (stock 2076, +2.0 %). specks.py: clean.

## Batch critic round 1: 60.25 (A 59.0, B 61.5), no critical failure
The civ sail read as a blob with a "window" and dark collar; flat
shading; the planes lost at 1x; a pale halo along the hull bottom; the
cruciform stern wrong for the read; near-invisible in 2/6. Asked: a sail
~1.5x taller and narrower with a rounded leading edge, thin planes that
stand out, black with a civ band near the top; a black anechoic hull
with a soft sheen; only the rudder fin aft.

- Batch 2 build rounds (b2a-b2d): the sail 0.24 long x 0.048 wide x 0.29
  tall, black (#1c1e20, roughness 0.36, a soft sheen), a civ band from
  56 to 88 % of its height, no dark cap; fairwater planes 0.26 span,
  9 mm thick; the stern planes gone, the rudder fin's upper half civ;
  a civ stripe along the walkway from the sail's aft end to the bow;
  the waterline a dark wet line, foam at the bow, a grey wash aft.
  Sent to batch 2 (GPU, b2e: the walkway stripe from y 0.2): tint 11.3 /
  7.7 / 10.6 / 10.2 / 10.9 / 4.2 / 10.4 / 10.3 % (mean 9.5; stock 8.8).
  Values: mean 64, p10 32, p50 58, p90 102, std 30 (stock 82: black as
  asked). Footprint 2100 (stock 2076, +1.2 %). specks.py: clean.

## Batch critic round 2: 69.9 (A 77.25, fresh 62.5), no critical failure
The walkway stripe reads as a racing stripe; asked: a rounder sail
leading edge; planes with span enough for 1/3/5/7; a swept black rudder
with civ only on its tip; hull panel lines and a hatch line; no white
bow crescent; a visible stern taper; civ in 2/6 more than a dot.

- Batch 3 build rounds (b3a, b3c): the sail's plan a half-ellipse nose
  over the first 18 % then a long taper; planes 0.36 span, 12 mm thick,
  their tops painted civ (the bar that shows in 2/6); the sail band 44-90
  %; the rudder swept back, civ only on its top 0.025; the walkway stripe
  gone; tile courses (#232629 / #1c1e20 every 0.26) and five hatches
  along the walkway; the bow foam a short grey wash; the stern cone from
  s -0.2 (exponent 0.75).
  Sent to batch 3 (GPU, b3c): tint 7.4 / 4.6 / 7.7 / 8.3 / 6.8 / 3.8 / 7.2
  / 8.7 % (mean 6.8; stock 8.8). Values: mean 66, p10 33, p50 63, p90
  100, std 28 (stock 82; black as asked). Footprint 2092 (stock 2076,
  +0.8 %). specks.py: clean.

## Batch critic round 3: 72.75 (continuity 83.0, fresh 62.5), no critical failure

## FINAL = batch round 3 (b3c, code at 20470727), 72.75 (3-round cap)
Rounds: batch 1 60.25, batch 2 69.9, batch 3 72.75 (the best). Final
sheets: reviews/realism/nuclear-submarine/final.png and final_red.png
(shipped, final, Submarine and Destroyer). Tint 6.8 % (stock 8.8),
footprint 2092 (stock 2076).

Later pass (the critics' last open points):
- keep the sail hull-black, civ only on a top band plus the planes;
- keep the planes' edges and undersides black (no "blue drip" in 4/8);
- round the sail's leading edge and top;
- thicken the hull ~1.4x;
- a larger upper rudder fin.

Finals gate (GPU re-render at HEAD, scratch/naval-carriers/gate.sh): PASS; diff against final.png mean 0, max 0 (red the same); specks.py: clean.
