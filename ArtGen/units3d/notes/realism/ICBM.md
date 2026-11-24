# ICBM (realism pass): design

A three-stage silo ICBM (Minuteman III / Peacekeeper) standing on a low
concrete launch pad with four steel hold-down posts (the pad is outside the
fit, so the missile keeps the shipped size). Off-white stages, dark
interstage rings and panel seams, the narrower third stage, the bright
re-entry shroud with a dark tip, a dark raceway and hatch on the camera's
side. DEFAULT has ONE direction (row 0): only that view is composed
(compose declared_rows, see LEDGER). Neighbours: Mobile SAM, Jet Fighter
(stock).

## Baseline (as shipped)
A grey missile with civ noise patches all over (the "pyjamas" pattern
PLAN.md rules out), no ground contact beyond the bells. Tint 27.6 %.

## What must read at 1x
1. A tall white missile with a bright pointed nose: unmistakably an ICBM.
2. Stage structure: two dark rings, the step to the narrow third stage.
3. Standing on a pad, not floating.

## Civ colour (aim 15-22 %: below the shipped 27.6, still bold on a
needle 8-11 px wide): three bands, the aft skirt, the top of stage 1, the
shroud base.

## Rounds
- r1: pad r 1.55 x body r, light concrete; five seams. Tint 13.6 %, footprint 1.14, clean. Read as stacked cans; the pad as a white plinth.
- r2: pad 1.3 r, three seams, stage-1 band 1.4 m. Tint 16.0 %, footprint 1.11.
- r3: dark concrete, stages toned to #c6c4ba (the nose stays the lightest), fit_scale 0.96. Tint 15.6 %, footprint 1.02, clean.
- batch 1 (GPU, = r3 code): tint 15.6 %, footprint 1.02, clean. ppm 7.94.
Sent to batch critic round 1: review\_batch\batch_airmissiles_1.png / _red.png (rows: shipped, new per unit; Jet Fighter and Mobile SAM stock).

## Batch critic round 1: 68.5 (A 70.5 / B 66.5), no critical failure
Shared: plain tube; pad doesn't read at 1x; civ on thin bands. Unit: real pad wider than the missile (launch ring, flame deflector, umbilical mast/arm, hold-down clamps); stage-skirt steps with taper, a tone per stage, nozzle skirts, raceway conduit, a distinct re-entry nose.
- s1: pad 2.5 r with a dark flame trench, steel launch ring on four legs, four clamps to the skirt, an umbilical mast (light, dark bracing bands, a head) with two arms; stages stepping in and tapering, s1 #b4b2a8 / s2 #c4c2b8, four nozzles below the skirt, round conduit per stage, shroud with a dark base ring and tip; civ aft skirt, a band at the top of stage 1, the whole third stage. Tint 11.8 %, footprint 1.31 (fit_scale 0.9), clean.
- s2: fit_scale 0.84, stage-1 band 1.65 m. Tint 12.2 %, footprint 1.17: the stand is deliberately wider than the shipped bare missile.
- batch 2 (GPU) = the last draft's code; same numbers. Sent to batch critic round 2: review\_batch\batch_airmissiles_2.png / _red.png (neighbours: Bomber, Mobile SAM).

## Batch critic round 2: 78.9 (A 79.75 / fresh 78.0), no critical failure
Shared: stacked-block launch structures merge with the missile. Unit: a distinct grey per stage (one clearly darker), a darker interstage under the top civ band, interstage rings with depth; the mast an open lattice truss darker than the missile, two visible swing arms, a broader footing.
- t1: an open lattice tower (corner posts, a frame and a diagonal per bay, members 0.24 m, dark #3c3e40) on a concrete footing, a head platform; two swing arms (a lit top chord, a dark lower chord, a strut); interstages with lit lips (depth), the one under the civ stage 0.55 m. Tint 9.1 %, footprint 1.47.
- t2: stage 1 #8c8a84 (clearly darker), fit_scale 0.8. Tint 9.4 %, footprint 1.34 (the tower's area), clean; 1x height 56 px (shipped 60), width 18.
- batch 3 (GPU) = t2: tint 9.4 %, footprint 1.34, clean, ppm 6.66. Sent to batch critic round 3.

## Batch critic round 3: 82.1 (continuity 85.75 / fresh 78.5), no critical failure

## FINAL: batch round 3 (code 6ba10a93), the best of three, cap reached
Critic history (average; continuity / fresh): batch 1 68.5 (70.5 / 66.5); batch 2 78.9 (79.75 / 78.0); batch 3 82.1 (85.75 / 78.5) = FINAL.
final.png / final_red.png: rows shipped, final, Bomber, Mobile SAM (stock); one direction. Tint 9.4 %. Footprint 1.34 (the stand and tower; 1x height 56 px, shipped 60). Specks clean. ppm 6.66.
Later pass: faint grey on the upper stage; open up the lattice.
Finals gate (scratch/airmissiles/verify/verify_all.sh, GPU re-render at HEAD 6ba10a93 against final.png / final_red.png): PASS.
