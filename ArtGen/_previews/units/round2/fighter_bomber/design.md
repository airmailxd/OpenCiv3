# Fighter Bomber, round 2

## What it is
The mod's picture (its F-15 folder) is a P-38. The model is the **Lockheed P-38J Lightning** as a
fighter-bomber (1944): a central gondola with the guns in its nose and a bubble canopy, two booms each with an
Allison engine, chin intakes, a turbo-supercharger fairing on top and radiator scoops on its flanks, oval fins
above and below the booms joined by the tailplane, a 1,000 lb bomb under each wing root. Olive drab over
neutral grey.

## What must read at 1x
1. Twin booms and the tailplane closing them: the P-38's frame silhouette.
2. The central gondola with its canopy between the booms.
3. Civ colour on the wing tips (as the mod has), the boom bands and the insignia.

## Stock neighbours
- **Fighter**: a WWII single-engine fighter, roundels, a 3-blade prop, camouflage. Takes: prop treatment,
  scale of markings.
- **Bomber** (B-17): olive drab, roundels, tail band. Takes: the palette and the civ tail markings.

## Parts (Fighter ~14, Bomber ~25; target 50+)
Gondola (2 panel lines), 5 nose guns, canopy with 3 hoops and 2 bars, windscreen; 2 wings with 4 panel
lines, flap and aileron lines, civ tips with a white line, 2 wing insignia; 2 booms (5 panel lines each),
2 spinners, 4 chin intakes with dark mouths, 2 turbo fairings with their wheels, 4 radiator scoops with dark
mouths, 2 civ boom bands, 2 boom insignia; 2 pylons, 2 bombs (body, nose, tail cone, fins, fuze band);
2 fins with rudder lines, the tailplane with its elevator line; 2 three-bladed props.

## Rounds
- **r0 (round 1 as rejected):** camouflage blotches (noise on a P-38, which never wore them), plain booms,
  no canopy frame, no intakes or turbos.
- **r1:** rebuilt as above. Reads as a P-38 at 1x. Wrong: less civ colour than the mod's picture and its
  neighbours.
- **r2:** civ wing tips with a white line inboard, the insignia moved in.
- **r3 (with Fighter, Bomber, close-up):** palette sits with the Bomber's olive drab; turbos, canopy frame,
  boom bands and tips all read. Wrong: panel lines faint on the broad wing.
- **r4:** darker panel lines. Holds with the stocks.
- **r5 (animations):** attack: tracers from the nose guns; death: breaks up, falls, fireball; run: bob and
  bank with both props turning. The Wildcat and P-38 cannot be confused at 1x (one engine and blue, twin
  booms and olive).

## After the first review (failed)
- **Framing:** catalog size 0.82 and drop 5 (1x px; `aircraft_r2` lowers the fitted anchor): the default
  frame now keeps 10-22 px (2x) clear of the cell top in every direction (it touched in S). The look
  sheets now show whole cells, not crops.
- **Bombs:** larger, pale (light olive grey with a yellow band) and hung forward so their noses show ahead
  of the wing; they read in the S/N views and in the shadow.
- **Death:** `aircraft_r2` (models/league_air.py): the pieces keep their civ colour, tumble down over
  ~6 frames trailing fire, reach the ground and shrink away under the smoke over 3 more, instead of
  vanishing in one frame (the stock Fighter does that).
- **Measured:** civ blue 6.3% (Fighter 6.3, Bomber 3.1) after narrowing the civ tips and dropping the boom
  insignia; brightness 96 (95-104).

## After the second review (model passed; animations failed)
- Death: the pieces fall only to about half the altitude before shrinking away, and drift less, so
  the whole death stays inside the cell (closest 26 px from the bottom, 2x).
- Attack: `aircraft_r2` overrides the gun attack with shorter tracers (0.75 x the plane's length).
- Bombs: dark teardrops (1,000 lb, yellow nose band, box fins) on longer inboard pylons, hung forward so
  their noses show ahead of the wing.
