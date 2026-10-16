# units3d: the PC session's status

The PC (Windows, RTX 5060 8 GB, Blender 4.5.5 portable at
`C:\Users\stzur\Tools\blender-4.5.5-windows-x64`, MPFB 2.0.18 with the CC0
MakeHuman assets, `CIV3_HOME` set) owns the non-foot units. Everything below is
on `units-remaster`; Civ3 originals and render passes stay local in
`ArtGen/_previews` (gitignored).

## Done

Since 2026-10-16 a review has six axes: imposing, not plastic, faithful,
readable at 1x, calm saturation, and **detail vs original** (the remake
carries every strap, trim, band, emblem, rivet and carried item the
original shows). Pass = all six >= 7; a missing original detail blocks.
The per-unit lists are in [DETAIL_AUDIT_PC.md](DETAIL_AUDIT_PC.md).

| Units | Files | Review |
|---|---|---|
| Catapult, Cannon, Galley, Caravel | siege.py, ships.py, vkit.py | 6 axes PASS (detail 7-8) |
| Horseman, Rider, Ansar Warrior | mounted.py, riders.py, remaster.py | 6 axes PASS (detail 7-8) |
| Mounted Warrior, Ancient Cavalry, Knight, Keshik, Sipahi, Conquistador, Cossack, Hussar, Cavalry | mounted.py, riders.py, remaster.py | frozen for stzur's judgment (borderline 6-7 on 5 axes); detail 2-5/10, see below |
| chariot, War Chariot, Three Man Chariot | chariots.py | frozen for stzur's judgment; detail 4-5/10, see below |
| Props (pilots + riders + footmen) | props.py, props_preview.py | pilots' props reviewed; footman props delivered, not yet seen on bodies |
| Calibration and notes for all 78 Mac units, and for the PC's units | calibration/, notes/ | numbers and words only |

The horse is Lyndon Daniels' CC0 horse (rigged by ChadM), fetched into
`_cache/` and checked by SHA-256; credited in `C7/Text/credits.txt`. A
procedural horse scored 3/10 and is kept behind `procedural=True`.

## Frozen units: the originals' details they still lack (blockers)

| Unit | Detail | Missing from the original |
|---|---|---|
| Mounted Warrior | 4 | blue hand-print war paint on the horse; blue/white beaded blanket; blue tassels on bridle and chest strap; war-bonnet trailer down the back; dun coat (it's a pinto) |
| Ancient Cavalry | 5 | big grey round shield with blue quartered emblem; blue horsehair crest; long bright blue reins; dark saddle pelt; spear with iron head and butt spike |
| Knight | 3 | steel chanfron; crinet; caparison panels with gold stripes and borders; peytral; great helm with crest; blue heater shield with white charge; mail on the legs |
| Keshik | 3 | two crossed lances behind the back; fur-brimmed spiked helmet; maroon/blue lamellar with gold edging; bronze round shield; fleece saddle; blue noseband |
| Sipahi | 4 | caparison covering the neck, chest and flanks; white dot scatter on it; gold saddle with red cloth; dark blue kalkan with boss; cloth-covered horse head with white plume |
| Conquistador | 2 | **the war dog** (black-and-tan mastiff, blue collar, all 8 views); blue rodela with gold boss; cream fringed trapper with gold breast straps; black morion and cuirass; lance tip |
| Cossack | 3 | rifle with ivory-inlaid stock; gazyri and crossed blue bandoliers on a tan cherkeska; low fur papakha with blue top; shashka; dapple-grey horse |
| Hussar | 3 | gold frogging and buttons; sheepskin shabraque with blue points; shako cords and plume; long blue reins; dun horse; sabretache and scabbard |
| Cavalry | 5 | white crossbelt; gold epaulettes; sabre in a brass scabbard; black bridle with white noseband; visible carbine; crested brass helmet with chinscale |
| chariot | 5 | gold lattice car with blue infill; scythe-bladed polearm; grey iron tires and dark spokes; a visible pole and yoke; blue head plumes and collar pads |
| War Chariot | 4 | 3-5 feather fan plumes; gold borders and yoke saddles on the caparisons; gold usekh collar and armbands; gold-rimmed dark car with a blue panel; bow case on the car side |
| Three Man Chariot | 4 | gold winged sun-disc on the side panel; gold yoke finials; one grey and one dark horse; fine silver scale trappers with blue border, neck to rump; gold spear and gold round shield; quivers at the car's front corners; grey scale and conical helmets on the crew |

These are what a fifth round would add; waiting on stzur before touching them.

## Known gaps / next steps

1. Riders: one shared seated pose for all.
2. Not started (PC's list): War Elephant, Trebuchet, Hwacha,
   Artillery, Flak, armor (6), aircraft (7), rockets (2), Cruise Missile,
   Holy Relic, the other 14 ships.
3. Nothing is composed into game sheets yet (compose.write_unit) and nothing
   is in C7/ModernArt; pushes go to units-remaster only.

## How to render

    blender -b --factory-startup --python-exit-code 1 --python ArtGen/units3d/render_vehicles.py -- <Unit> [...]
    python ArtGen/units3d/vehicles_sheet.py <Unit> [...]     # original / current / Blender, local only
    python ArtGen/units3d/size_check.py <Unit>               # on-map box vs the original
