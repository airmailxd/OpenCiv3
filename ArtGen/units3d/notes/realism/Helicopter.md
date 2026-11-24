# Helicopter (realism pass): design

A CH-47D Chinook (1980s-2000s), the shipped unit's model, kept: tandem
rotors are the one shape that reads "helicopter" at 1x. Real: fuselage
15.9 m, rotors 18.3 m across, the aft pylon ~1.3 m taller than the front.
Built as a painted miniature in the house sand with its own camo (olive
and brown blotches), a darker olive-brown belly. Neighbours: Jet Fighter
(stock, same era, air) and Mobile SAM (stock, house-palette vehicle).

## Baseline (as shipped)
Pale grey/white with a saturated civ spine stripe: the brightest, least
house-like unit in the air row; the rotor blades are thin dark sticks.
Tint 10.6 / 15.1 / 13.2 / 12.0 / 13.2 / 10.0 / 9.9 / 9.1 % (mean 11.6).
Stock air neighbours: Bomber 11.5, Jet Fighter 7.2, F-15 5.0, Fighter 19.2.

## What must read at 1x
1. Two big three-blade rotors, broad dark blades with yellow tips, the
   aft one clearly higher on the tall pylon.
2. The long boxy fuselage: dark glazed nose and chin windows, round
   windows, sponsons along the lower sides, gear.
3. The two engine pods on the aft pylon's flanks (dark intakes/exhausts).

## Civ colour (aim 7-10 %, even across directions)
Three deliberate regions: the aft pylon's flanks, a band round the
fuselage behind the cockpit (not the belly), the rotor hub caps.

## Rounds
Tint per direction (columns 1-8); footprint = DEFAULT alpha>127 pixels against the shipped sheet.
- r1: house sand + olive/brown camo, dark belly, blades 0.12 m with yellow tips, civ on the aft pylon's flanks (hidden by the engines). Tint 3.3 %, footprint 1.06, a 16 px speck in 6.
- r2: blades 8.2 m (true 9.1: the fit gives the body more of the box), tips dropped (they read as dots), civ on the whole aft pylon top + flanks, wider band. Tint 5.4 %, footprint 1.16; specks in 1/3/6/7: the ramp-hinge box hung below the belly (6), thin drooping blades broke up near edge-on (7).
- r3: hinge box removed, blades 0.2 m thick, civ on the front pylon hump, fit_scale 0.94. Clean, footprint 1.05, tint 5.9 % (2.9 astern).
- r4 = batch 1 (GPU): band 1.3 m, the aft pylon's rear face civ. Tint 8.8 / 4.0 / 7.4 / 8.0 / 7.0 / 6.3 / 7.4 / 7.8 % (mean 7.1). Specks clean. Footprint 1.04-1.06 (mean 1.05). ppm 6.63.
Sent to batch critic round 1: review\_batch\batch_airmissiles_1.png / _red.png (rows: shipped, new per unit; Jet Fighter and Mobile SAM stock).

## Batch critic round 1: 76.9 (A 81.75 / B 72.0), no critical failure
Shared: desert tan drifts from the stock's grey/woodland and blurs at 1x; civ on tiny parts. Unit: darker olive/woodland, higher-contrast blotches, window and ramp lines; fuel pods, glazing, ramp hinge to read in 4/8; civ panels on both pylons' sides.
- s1: olive base #62664a, big blotches (noise 0.3/0.34: dark green, brown), belly #44473a; light framed glazing (am_glass); fuller fuel pods with dark caps and a seam; windows in light rims; ramp hinge rod, side edges and plate; civ panels proud on both pylons' flanks, bigger hub caps, pylon tops civ. Tint 6.7 % (2/6: 4.2/4.4), footprint 1.07, clean. Dark against the grass.
- s2: base lifted to #6c7050, the spine tunnel civ (seen from every side). Tint 10.3 / 6.8 / 12.3 / 12.7 / 11.3 / 4.4 / 7.4 / 10.4 (mean 9.4), footprint 1.07, clean.
- batch 2 (GPU) = the last draft's code; same numbers. Sent to batch critic round 2: review\_batch\batch_airmissiles_2.png / _red.png (neighbours: Bomber, Mobile SAM).

## Batch critic round 2: 81.4 (A 86.0 PASS / fresh 76.75), no critical failure -> solo round 3
Shared: large single-tone surfaces collapse at 1x; dark olive low contrast on grass. Unit: raise the aft pylon (rear hub clearly above the front, 4/8); lighten belly, window-band edges and the fuselage's top edge, bigger glazing (sides go to a dark slab in 1/3/5/7/4/8).
- t1: aft pylon taller (top ~4.2 m, hub 3.6 -> 4.5), its civ panels raised; light shoulder (am_edge #a0a282 on faces n.z 0.3-0.85) and a lighter window band (am_wband #8a8c6c); belly #6a6c58; glazing from y 5.9, bigger chin windows. Tint 9.5 %, footprint 1.10, clean.
- solo 1 (GPU): fit_scale 0.92. Tint 10.7 / 7.0 / 12.5 / 13.1 / 10.5 / 3.6 / 7.0 / 10.9 % (mean 9.4), footprint 1.06, clean. ppm 6.49. Sheets: review\helicopter\solo1\final.png / final_red.png (shipped, new, Bomber, Mobile SAM).
Sent to the solo critic round (round 3).

## Round 3 (solo 1): 84.0 (continuity 87.5 / fresh 80.5), no critical failure

## FINAL: solo round 1 (round 3; code 6ba10a93), the best of three, cap reached
Critic history (average; continuity / fresh): batch 1 76.9 (81.75 / 72.0); batch 2 81.4 (86.0 PASS / 76.75); solo 1 84.0 (87.5 / 80.5) = FINAL.
final.png / final_red.png (reviews/realism/helicopter, and review/helicopter for the gate): rows shipped, final, Bomber, Mobile SAM (stock). Tint per direction (columns 1-8): 10.7 / 7.0 / 12.5 / 13.1 / 10.5 / 3.6 / 7.0 / 10.9 % (mean 9.4). Footprint 1.06 of the shipped. Specks clean. ppm 6.49.
Later pass: darker olive/brown camouflage on the sides; thinner blades (they were thickened 0.12 -> 0.2 m against specks: thin them only with a specks check).
Finals gate (scratch/airmissiles/verify/verify_all.sh, GPU re-render at HEAD 6ba10a93 against final.png / final_red.png): PASS.
