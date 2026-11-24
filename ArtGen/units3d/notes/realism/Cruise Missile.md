# Cruise Missile (realism pass): design

A BGM-109 Tomahawk in flight (1983-), as shipped. Real: 6.25 m, 0.52 m
across, wings 2.67 m. Built a touch fatter (0.6 m) so the body reads at
1x. Light warm grey body (never paper white), dark radome, dark wings and
tail fins thick enough not to vanish edge-on, panel seams, the belly air
scoop, a dark exhaust. Neighbours: Jet Fighter (stock) and Mobile SAM
(stock, the other missiles in the roster).

## Baseline (as shipped)
White with blue bands and grey blocks; a thin bright stick at 1x, fins
0.03 m (paper-thin). Tint 16.2 / 14.0 / 16.2 / 17.1 / 15.0 / 13.6 / 15.1 /
17.7 % (mean 15.6).

## What must read at 1x
1. A long cylinder with a dark nose: a missile, not a plane.
2. Small straight wings at mid-body, a cross of tail fins.
3. The civ bands: two wide bands, fore and aft of the wings.

## Civ colour (aim 10-13 %): the two bands only.

## Rounds
Tint per direction (columns 1-8); footprint against the shipped sheet.
- r1: body r 0.3, light grey, dark radome, seams, 0.07 m X-fins, thick wings. Tint 14.7 %, footprint 1.10, clean.
- r2: r 0.28, narrower bands. Tint 11.6 %, footprint 1.07, clean.
- r4: body toned down (#bab8ae: it was the brightest value on the row). Same numbers.
- r5 = batch 1 (GPU): fit_scale 0.98. Tint 11.5 / 10.0 / 11.5 / 12.1 / 11.3 / 10.0 / 11.3 / 12.1 % (mean 11.2). Clean. Footprint 1.01-1.06 (mean 1.03). ppm 14.32.
Sent to batch critic round 1: review\_batch\batch_airmissiles_1.png / _red.png (rows: shipped, new per unit; Jet Fighter and Mobile SAM stock).

## Batch critic round 1: 70.9 (A 72.75 / B 69.0), no critical failure
Shared: plain tube, too few parts; barely differs from shipped. Unit: heading ambiguous (both ends dark): light rounded radome, exhaust nozzle, dark tail the only dark end; longer, lighter wings for 4/8; belly intake, seams, tonal variation.
- s1: light rounded radome (#d8d6ce) with a dark joint ring; body in three tones (forward #c2c0b6, mid #b4b2a8, aft #a4a298); dark nozzle with a black mouth; wings 1.75 m and light; belly scoop with a dark mouth; bands 0.9 + 0.7 m. Tint 13.8 %, footprint 1.04, clean.
- s2: wings toned to #b8b6ac. Same numbers.
- batch 2 (GPU) = the last draft's code; same numbers. Sent to batch critic round 2: review\_batch\batch_airmissiles_2.png / _red.png (neighbours: Bomber, Mobile SAM).

## Batch critic round 2: 79.5 (A 78.25 / fresh 80.75), no critical failure
Shared: large single-tone surfaces collapse at 1x. Unit: grey down or shorten the bright wings (diagonals read as a small aircraft); grey underside or a darker band wings-to-tail; darker belly intake (4/8); faint heat tint at the nozzle.
- t1: wings 1.45 m and grey (#a2a098); grey underside (#8a8880 on n.z < -0.45); aft section #8e8c84; the intake a dark scoop with a black mouth; a heat-tinted nozzle lip (am_heat). Tint 14.3 %, footprint 1.04, clean.
- batch 3 (GPU): same numbers.
Sent to batch critic round 3: review\_batch\batch_airmissiles_3.png / _red.png (neighbours: Bomber, Mobile SAM).

## Batch critic round 3: 82.9 (continuity 84.25 / fresh 81.5), no critical failure

## FINAL: batch round 3 (code 6ba10a93), the best of three, cap reached
Critic history (average; continuity / fresh): batch 1 70.9 (72.75 / 69.0); batch 2 79.5 (78.25 / 80.75); batch 3 82.9 (84.25 / 81.5) = FINAL.
final.png / final_red.png: rows shipped, final, Bomber, Mobile SAM (stock). Tint 14.2 / 12.8 / 14.2 / 16.0 / 14.2 / 12.6 / 14.4 / 16.0 % (mean 14.3). Footprint 1.04. Specks clean. ppm 14.11.
Later pass: a larger, darker belly intake; grey on the lower half of the forward body; lighter tail fins at 1x.
Finals gate (scratch/airmissiles/verify/verify_all.sh, GPU re-render at HEAD 6ba10a93 against final.png / final_red.png): PASS.
