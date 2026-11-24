# NumPy siege group (realism pass, PC2, branch realism/numpy-siege)

Units (units3d, numpy_siege.py + crews.py ASHI_R): Medieval Japanese Catapult, Fire Cannon, Siege Crossbow
(the MDJ scenario's engines, remade from the NumPy generator's ArtGen/units models: identity from
catalog/mdj_mounted.py, mdj_ranged.py and models/mdj_engines.py, mdj_siege.py; not their style).
Not here: Stone Crossbow (a footman, foot units' PC).

Critic rounds are batched: review\_batch\batch_numpysiege_<n>.png / _red.png, one shipped (NumPy) row and one
new row per unit, the closed units3d Catapult and Cannon finals as the stock rows at the bottom.

## Calibration
No Civ3 original: calibration/<Unit>.json is written from the INSTALLED NumPy sheets
(scratch/numpysiege/calib_numpy.py: DEFAULT frame 0 bbox per direction, alpha>127 on base+tint, the flics'
headers from the unit's metadata). render_vehicles.fit matches the box as for the stock units.

## Tooling (all defaults unchanged; siege gate re-run after each shared commit)
- numpy_siege.py (new): the three builders; registered in siege.BUILDERS / siege.REALISM by an import at the end
  of siege.py (the six closed builders untouched).
- render_vehicles.fit: an opt-in root['fit_scale'] (absent = 1.0, as before).
- crews.populate: an opt-in per-empty 'reach' (JSON, set by numpy_siege._crew) over CREW_R's _reach.
- crews.CREW_R: new entries for the three units (ASHI_R: the stock Infantry's build, an ashigaru's civ coat,
  dark trousers, white leg wraps, sandals, a jingasa).
- riders.HEADGEAR['jingasa'] (new hat): a wide shallow black-lacquered cone (civ when the entry tints 'hat').

## Size: crew ppm vs the opaque-area band
The NumPy MDJ Catapult is drawn with thin members and a lot of air; at the crew's ~34 ppm, two crewmen alone
are ~0.4 of its opaque area (measured with NO_CREW). So the MDJ Catapult keeps one crewman (as the shipped
unit), thin timbers (th 0.6) and fit_scale 0.92 (ppm ~32). The Fire Cannon has one gunner (as shipped), the
Siege Crossbow two (its sparse bows leave room).

## Shipped (NumPy) civ colour (tint.py)
Medieval Japanese Catapult 22.2, Fire Cannon 19.4, Siege Crossbow 16.1.
Shipped specks: MDJ Catapult dir 8 (2 pieces), Fire Cannon dirs 4/8, Siege Crossbow dirs 2/4/8 (thin frame).

## Own build rounds (a1-a5) before batch 1
See each unit's notes.

## Batch 1
Sheet: review\_batch\batch_numpysiege_1.png / _red.png (rows: shipped, new per unit; the closed units3d
Catapult and Cannon finals, rendered by the gate at HEAD). Finals gate (scratch/numpysiege/verify/verify_siege.sh,
this worktree) at bc911571: all 6 closed finals PASS (max diff 0.008 Trebuchet, 0.004 others, 0 px > 0.1).
Critic scores: (pending)
Batch 1 critics: MDJ Catapult 62.5/64.5 = 63.5 (near-critical identity), Fire Cannon 72.5/79.5 = 76.0,
Siege Crossbow 71/76.75 = 73.9.

## Size rule (lead, 2026-11-24; NUMPY_BRIEF.md)
Size against the class's stock units, not the NumPy sprite: MDJ Catapult between the stock Catapult and Trebuchet,
Fire Cannon 0.90-1.10 of the stock Cannon, Siege Crossbow between the stock Catapult and Cannon.
scratch/numpysiege/fitstock.py <new> <stock dirs...> (fitcheck against D:\Dev\C3 stock); sg.sh prints it.
Stock spans (opaque px, vs stock Catapult): Catapult 1.00, Trebuchet 1.93, Cannon 0.54.

## New shared options (round 2; defaults unchanged)
- numpy_siege._cart: sill_key (default 'side_rail').
- (round 1) render_vehicles root['fit_scale']; crews.populate per-empty 'reach'; riders.HEADGEAR['jingasa'];
  crews.CREW_R / ASHI_R entries for the three units.

## Batch 2
Sheet: review\_batchatch_numpysiege_2.png / _red.png.
Finals gate at 37e3864a: all 6 closed siege finals PASS (max diff 0.004, 0 px > 0.1).
Batch 2 critics: MDJ Catapult 78.5/83.5 = 81.0 (identity critical closed); Siege Crossbow 79.5/83.75 = 81.6;
Fire Cannon 67/70.25 = 68.6 (B: new critical, identity). Fire Cannon size allowance (lead): up to ~1.30 of the
stock Cannon.

## Batch 3 (the last round)
Sheet: review\_batch\batch_numpysiege_3.png / _red.png (code 8b911c0a). No new shared options in round 3.
Finals gate at 8b911c0a: all 6 closed siege finals PASS (max diff 0.008 Trebuchet, 0.004 others, 0 px > 0.1).
Batch 3 critics: MDJ Catapult 84.3/86.25 = 85.3 PASS; Siege Crossbow 81/85 = 83.0; Fire Cannon 81/83.75 = 82.4
(identity critical closed).

## Group close
All three FINAL on round 3 (their best), no critical: MDJ Catapult 85.3 PASS, Siege Crossbow 83.0, Fire Cannon 82.4.
Finals in reviews/realism/<unit>/ and D:\Dev\_handoff\realism\review\<unit>\ (shipped, final, stock Catapult and
Cannon units3d finals). Gate: scratch/numpysiege/verify/verify_siege.sh now holds all 9 siege finals (the six
siege units plus these three). Later-pass lists in each unit's notes.
Final gate at 62816e02 (9 units): all PASS; siege six max diff 0.008 (Trebuchet) / 0.004, the three NumPy finals 0.000 (finals use the gate's 'new' row label).
