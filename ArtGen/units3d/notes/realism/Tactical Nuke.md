# Tactical Nuke (realism pass): design

A Pershing-style theatre nuclear missile standing on a small pad: two
stages in the house sand with olive-drab camo (US Army missiles were
olive; the house sand base keeps it in the roster and apart from the
white ICBM), four big cropped-delta tail fins, the long dark conical
re-entry vehicle with small vanes, dark interstage and raceway. ONE
direction (row 0). Neighbours: Mobile SAM, Jet Fighter (stock).

## Baseline (as shipped)
A smaller copy of the ICBM with a civ skirt and civ noise patches. Tint
29.2 %.

## What must read at 1x
1. Shorter and darker than the ICBM, with a dark pointed warhead.
2. Four tail fins spreading at the base (the identity against the ICBM).
3. Standing on a pad.

## Civ colour (aim 18-24 %): the four tail fins and a band under the
re-entry vehicle.

## Rounds
- r1: pad 1.9 r, fins span 0.62. Tint 15.1 %, footprint 1.25, clean.
- r2: pad 1.45 r, fins 0.7 x 1.55, RV band 0.7. Tint 17.9 %, footprint 1.21.
- r3: camo threshold 0.5 (it was too sparse), fit_scale 0.92. Tint 15.3 %, footprint 1.07.
- r4 = batch 1 (GPU): RV band 0.9 m, fit_scale 0.90. Tint 18.1 %, footprint 0.98, clean. ppm 8.45.
Sent to batch critic round 1: review\_batch\batch_airmissiles_1.png / _red.png (rows: shipped, new per unit; Jet Fighter and Mobile SAM stock).

## Batch critic round 1: 63.75 (A 66.0 / B 61.5), no critical failure
Unit: a 4-5 px needle, the odd one out: scale up and thicken keeping the footprint near stock; plain olive drab, no camo; bigger civ fins and nose band; a visible erector/launcher wider than the fins.
- s1: r 0.62 (was 0.5), plain olive drab #5c6042, fins 1.9 x 0.95 span, nose band 1.2 m, a steel launch platform (wider than the fins) with a blast ring, an olive erector strongback beside it with two clamp arms and rings. Tint 17.6 %, footprint 2.99 (fit_scale 1.08): as big as the ICBM.
- s2: platform 2.9 m, erector closer, fit_scale 0.8. Tint 18.7 %, footprint 1.43, clean.
- s3: fit_scale 0.76 (footprint ~1.3: the compromise between the 1x read and the shipped needle's area).
- batch 2 (GPU): tint 18.2 %, footprint 1.27, clean. ppm 7.31.
Sent to batch critic round 2: review\_batch\batch_airmissiles_2.png / _red.png (neighbours: Bomber, Mobile SAM).
Lead (batch 2): the 1.27x footprint is accepted for this round; the critics are told it is drawn larger than the shipped needle.

## Batch critic round 2: 68.25 (A 70.75 / fresh 65.75), no critical failure; both would pick it out at 1x
Unit: raise its height ~25 %, at least the shipped unit's, accept the area; lighter olive with light stage rims and fin-root highlights; replace the tower with a wheeled erector-launcher trailer raising the missile at an angle (the Pershing silhouette).
- t1: rebuilt as a Pershing on an M790-style trailer: flat bed with lit edges, gooseneck (civ plates on its front and sides), landing legs, rear stabiliser jacks, two axles of dual wheels with fenders; the erector boom hinged at the rear, a hinge block, two clamp rings, a hydraulic ram from the bed. The missile is lighter olive (#7c8058), with light rims at the stage joints and light strips along the fin roots, civ fins at 45 deg, civ band 1.2 m. Raised at 52 deg; everything in the fit; fit_scale 1.3. 1x height 36 (shipped 46), tint 11.4 %, footprint 2.26.
- t2: raised to 62 deg, fit_scale 1.7: 1x height 50, width 43, footprint 3.44, tint 11.7 %, clean.
- t3: fit_scale 1.6 (1x height ~47, just over the shipped 46 and under the ICBM's 56).
- batch 3 (GPU): tint 11.5 %, footprint 3.05 (accepted larger area), clean, ppm 9.15. Sent to batch critic round 3.

## Batch critic round 3: 74.5 (continuity 82.5 / fresh 66.5), no critical failure (so no extra round)

## FINAL: batch round 3 (code 6ba10a93), the best of three, cap reached
Critic history (average; continuity / fresh): batch 1 63.75 (66.0 / 61.5); batch 2 68.25 (70.75 / 65.75); batch 3 74.5 (82.5 / 66.5) = FINAL.
final.png / final_red.png: rows shipped, final, Bomber, Mobile SAM (stock); one direction. Tint 11.5 %. Footprint 3.05 of the shipped needle (the lead accepted a larger area; 1x height 46 px = the shipped, width 40). Specks clean. ppm 9.15.
Later pass: deck plating or stowage on the trailer, and a darker tone on it; a darker olive missile than the trailer; a dual axle.
Finals gate (scratch/airmissiles/verify/verify_all.sh, GPU re-render at HEAD 6ba10a93 against final.png / final_red.png): PASS.
