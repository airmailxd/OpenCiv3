# units3d: the PC session's status

The PC (Windows, RTX 5060 8 GB, Blender 4.5.5 portable at
`C:\Users\stzur\Tools\blender-4.5.5-windows-x64`, MPFB 2.0.18 with the CC0
MakeHuman assets, `CIV3_HOME` set) owns the non-foot units. Everything below is
on `units-remaster`; Civ3 originals and render passes stay local in
`ArtGen/_previews` (gitignored).

## Done

| Units | Files | Review (independent subagent, 5 axes, pass = all >= 7) |
|---|---|---|
| Catapult, Cannon, Galley, Caravel | siege.py, ships.py, vkit.py | all PASS 7/10 |
| Horseman (frozen at the middle rider scale) | mounted.py, riders.py | 7/7/8/7/7 PASS |
| Rider, Ansar Warrior | mounted.py, riders.py | PASS (7-8 on every axis) |
| Mounted Warrior, Ancient Cavalry, Knight, Keshik, Sipahi, Conquistador, Cossack, Hussar, Cavalry | mounted.py, riders.py, remaster.py | 6-7 borderline after 3 rounds; notes had become contradictory taste calls, stopped for stzur's judgment |
| Props (pilots + riders + footmen) | props.py, props_preview.py | pilots' props reviewed; footman props delivered, not yet seen on bodies |
| Calibration and notes for all 78 Mac units, and for the PC's units | calibration/, notes/ | numbers and words only |

The horse is Lyndon Daniels' CC0 horse (rigged by ChadM), fetched into
`_cache/` and checked by SHA-256; credited in `C7/Text/credits.txt`. A
procedural horse scored 3/10 and is kept behind `procedural=True`.

## Known gaps / next steps

1. Riders, remaining: Knight caparison has no heraldic white band; Mounted
   Warrior's bonnet reads as spikes and lacks the blue hand-print paint;
   Conquistador's dog escort not modeled; one shared seated pose for all.
2. Not started (PC's list): chariots (3), War Elephant, Trebuchet, Hwacha,
   Artillery, Flak, armor (6), aircraft (7), rockets (2), Cruise Missile,
   Holy Relic, the other 14 ships.
3. Nothing is composed into game sheets yet (compose.write_unit) and nothing
   is in C7/ModernArt; pushes go to units-remaster only.

## How to render

    blender -b --factory-startup --python-exit-code 1 --python ArtGen/units3d/render_vehicles.py -- <Unit> [...]
    python ArtGen/units3d/vehicles_sheet.py <Unit> [...]     # original / current / Blender, local only
    python ArtGen/units3d/size_check.py <Unit>               # on-map box vs the original
