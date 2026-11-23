# Japanese Warship, round 2

## What it is
An **atakebune**, the Sengoku period's floating fortress (Oda Nobunaga's at Kizugawaguchi, 1578; the
Tokugawa *Atake Maru*): a broad wasen hull with a raked stem and a square transom, its deck boxed in by
a plank fortress (the *tate-ita* walls) pierced by two rows of small square ports for arquebuses and bows,
a two-storey castle tower (*yagura*) of white plaster and black timber under tiled hip-and-gable
(*irimoya*) roofs, one mast with a big matting sail, sculling oars (*ro*) out under the walls, and tall
*nobori* banners on the walls. It fights with arquebus volleys and arrows from the ports.

## What must read at 1x
1. The boxy walled hull, longer and lower than a galley, with a dotted row of dark ports.
2. The castle tower: white walls, dark curved roofs stacked twice, upturned eaves: Japanese at a glance.
3. The big sail, not a card: billowed, horizontal battens, vertical cloth strips, a civ-coloured *mon*
   (a ring round three scales) on it, sheets running down to the deck.
4. Civ-coloured *nobori* banners standing along the walls.

## Stock neighbours
- **Galley** (calibration source): the big emblem sail, the many oars, light wood, a curled post.
  Takes: the sail's size and emblem treatment, the oar rows, light hull planking.
- **Dromon**: civ-coloured castles with gold trim, mast rings. Takes: civ colour on built structure
  (here the banners and the tower's curtain), gold accents (the ridge finials).

## Parts (Galley ~25 parts, Dromon ~40; target 70+)
Hull (planked, two wales, dark bottom band), transom with a dark border, raked stem post with a
bow board; the fortress: 4 walls painted in vertical planks with corner posts and 7 pillars a side, a top
beam, a roofed walk; 2 rows of ports a side (about 30); deck rail; the tower: lower storey (white plaster,
black posts and a black band of lattice windows), irimoya roof with gables, ridge with onigawara ends,
upper storey, its roof, 2 gold shachi on the ridge; the curtain (*maku*) round the tower's base in civ
colour with white mon dots; mast with tabernacle and a crane block at the head; the sail with
battens/strips/mon and a yard; 2 sheets, a halyard, forestay; 6 nobori on poles; a fukinagashi streamer
at the stern; rudder; anchor at the bow; 2 x 10 sculling oars; an arquebus volley (flashes and smoke at the
ports) and arrows.

## Rounds
(see below)
- **r0 (round 1 as rejected):** a dark box, a stack of grey slabs for a tower, a plain cream card for a sail.
- **r1:** new hull and fortress (planked walls, pillars, two rows of ports), irimoya roofs, battened sail with
  the mon, nobori, sheets. Wrong: a black cap hid the whole upper deck; the tower read grey (too much black
  timber, too little plaster); the mon a thin ring with dots; battens too faint.
- **r2:** the cap is a rim; more plaster on the tower; bigger scales, thicker battens, more strip contrast;
  lighter walls. Wrong: the roofs read as stacked grey boxes (horizontal courses), the tower small; the
  fore deck a flat brown field.
- **r3:** roofs tiled in ribs running down the slope, lighter eave tile-ends, stronger corner upturn; three
  storeys (castle silhouette); deck planks, two hatches, the windlass, water casks. Reads as a castle ship
  now. Wrong: the sail's head touched the cell's top in NE/N views; next to the Galley the ship is far
  darker and carries less civ colour (the Galley's emblem is big and bold).
- **r4:** mast and sail lowered; the mon a filled civ disc with the scales and a ring left in the cloth;
  pillars a dark brown instead of near-black. Holds at 1x: walled box, castle, emblem sail, banners.
- **r5 (with Galley, Dromon):** still colder and darker than the stock ancient ships. Warmer, lighter woods
  (walls, hull, deck) close to the Galley's tan, pillars brown; the sail widened toward the Galley's.
  Now sits in the family.
- **r6 (animations):** attack: an arquebus volley runs along the starboard ports (flash and white smoke at
  each), then arrows; run: the sculling oars sweep, wake; death: lists and sinks, sail slackening.

## After the first review (failed)
- **r7:** mon changed to takeda-bishi (a civ lozenge split in four along its diagonals), not an "A" in a
  ring; sail edge and battens thinned and lightened to the stock weight; tower roofs lightened with a pale
  tile-end edge on every tier; plank stripes halved and quieter, pillars brown, one row of ports.
- **r7/r8 death:** a new death in a subclass of the ship archetype (`ship_r2`, models/league_sea.py): the
  ship heels until the rig lies toward the water and goes under together; white water rings wherever the
  hull, tower or mast crosses the waterline. The stock Galley leaves its sail standing alone above the sea.
- **r8-r15 (measured with the reviewer's meas.py, fixed background):** civ blue 6.2% -> 11.3% (Galley
  15.6, Dromon 10.7); non-civ brightness 109 -> 142 (stocks 150-155): lighter walls, hull planks, deck,
  roofs, plaster and sail. Brightness stays about 5% under the stocks; most of the difference is the
  broad hull's larger shadow on the water.

## After the second review (passed; one nit)
- Death: the shared `ship_r2` death now rolls the ship onto her side by the middle of the sinking and
  then takes everything down at one rate, so the banners, sail and tower go under with the hull.
