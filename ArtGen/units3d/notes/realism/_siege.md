# Siege family (realism pass, PC2, branch realism/siege)

Units (units3d, siege.py + crews.py): Catapult, Trebuchet, Hwacha, Cannon, Artillery, Flak.
Not here: Radar Artillery (vehicles family); Medieval Japanese Catapult, Fire Cannon, Siege Crossbow
(NumPy MDJ units, remade in units3d after the families).

Critic rounds are batched (PROCESS.md): review\_batch\batch_siege_<n>.png, one shipped + one new row per
unit, Knight and Infantry as the stock rows at the bottom.

## Tooling (all defaults unchanged)
- render_vehicles.py: SIEGE_PARAMS (JSON builder kwargs) and RV_TAG (variant output folder); unset = as before.
- build_pc.py: --tag=<t> (render/compose into <Unit><t>/ and sheets<t>/), --no-catalog (skip catalog_json,
  for parallel runs). No install for a tagged variant.
- siege.py: new keyword options, each defaulting to the old geometry; the chosen values are set per unit
  in the builder's defaults (siege-only code, no other family calls these builders).

## Stock civ colour (tint.py, alpha>127, installed DEFAULT)
Catapult 24.1, Trebuchet 16.3, Hwacha 13.6, Cannon 26.5, Artillery 7.3, Flak 5.8 (Radar Artillery 3.1).
Stock specks: Trebuchet dirs 4/5 two pieces, Hwacha dirs 5/6/7 (arrow tips); the rest clean.

## Batch 1 (own build rounds b1-b4, GPU re-render g1)
Shared fix once: siege.OAK, a dark oiled oak for the wooden engines (Catapult, Trebuchet; the Hwacha warmer timber),
replacing the pale pine that read as a toy; per-unit choices in siege.REALISM, applied by siege.build().
Sheet: review\_batch\batch_siege_1.png / _red.png. Footprint (alpha>127 area, bbox) within 2 % of the stock for all
six (the fit is to the calibration box). Specks clean in every direction for all six (the stock Trebuchet and
Hwacha were not).
Shared-default changes: none (build_pc/render_vehicles options default off; _pneumatic_wheel disc default 'rim_steel').

## Batch 1 critics (A / B, avg)
Catapult 72.5/73.5 = 73.0; Trebuchet 66.5/64.0 = 65.25 (CRITICAL both: identity); Hwacha 69.5/70.5 = 70.0;
Cannon 68.0/69.5 = 68.75; Artillery 67.5/65.0 = 66.25 (CRITICAL: civ unreadable in 1-2); Flak 65.0/73.0 = 69.0
(CRITICAL A: crew ~1.4x the stock Infantry). Shared: near-copies of the shipped models recoloured; idle crews;
crew scale; head-on views lose the identifying part; flat plates; muted palettes; uneven civ colour.

## Batch 2 (rework)
- Crews at work (crews.CREW_R, used when siege.REALISM sets crew_realism; crews.CREW untouched): hands brought
  to the engine with the two-bone reach ('_reach' in the engine root's space), webbing, canteen, armbands and
  smaller helmets on the WWII crews (OLIVE_R), belts and hems on the tunics, a dark sash and trim on the robes.
- Crew scale: the crews stand at a fixed world size and the fit matches the stock box, so the engine's own scale
  sets the crew's size on screen. Target ppm ~34 at 2x (a 1.8 m man ~ the stock Infantry's 97 px). Batch 1: Flak 47.4
  (1.4x), Trebuchet 40.8, Cannon 27.3, Artillery 29.7. Batch 2: Flak k 1.35, Cannon k 0.85 + a shorter barrel,
  Artillery k 0.9, the Trebuchet taller (its arm up): all within ~34 +- 3.
- Palette: OAK a little warmer and more saturated.

## Batch 2 critics (continuity / fresh = avg; batch 1)
Catapult 68.5/81 = 74.75 (73.0); Trebuchet 70/61 = 65.5 (65.25; fresh: CRITICAL identity + height);
Hwacha 75/82.5 = 78.75 (70.0); Cannon 70/81.5 = 75.75 (68.75); Artillery 69.5/70.5 = 70.0 (66.25; fresh: civ in
1-3 still unreadable); Flak 73/71.5 = 72.25 (69.0; fresh: crew spindly).
Shared: crews thin, low-contrast, idle.

## Batch 3 (the last critic round)
- Crews (CREW_R): the stock Infantry's build for all (macro weight 0.9, upper_bulk 1.3), trousers darker than the
  tunic, every man working the machine; civ band on the M1 (headgear_params band); a civ sash on the robes.
Batch 3 fitcheck (1x bbox-area vs shipped, mean): Catapult 1.06, Trebuchet 1.01 (min 0.81 dir 2), Hwacha 0.98,
Cannon 1.08, Artillery 1.04, Flak 1.04: all ok (>= 0.90).

## Family close (siege)
Catapult B3 79.0, Hwacha B3 81.75, Cannon B3 78.75, Artillery B3 72.75, Flak R4 77.25, Trebuchet B3 71.5 STUCK
(identity in 1-2). All six in verify_siege.sh's CLOSED list.
