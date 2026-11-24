# Tank (realism pass): design

The Tank is an M4 Sherman (mid-war, 75 mm, VVSS suspension) built as a
painted 1/56 tabletop miniature: one believable vehicle, olive drab, worn
by a campaign, with the civ colour on two deliberate regions.

## Baseline (round 0, as shipped)
- Reads as a Sherman: rounded turret, sloped glacis, six road wheels.
- Tint: the turret band and five stars, 7-9 % of the unit's pixels at 2x
  (the stock Panzer 1-3 %, the stock Modern Armor 3-4 %). Below the plan's
  10-25 % target; the band is the right idea, the glacis stars are small
  and read as specks at 1x.
- Desert tan with a faint blotch camo: not a Sherman colour; it also puts
  the Tank in the same tan family as the Panzer (cream) and Modern Armor
  (desert), so the three read alike at 1x.
- Turret: a squashed ellipsoid, roof too domed (a mushroom), no bustle
  shape, the mantlet a small box.
- Running gear: the bogies are plain boxes, the return rollers float at the
  top evenly spaced (not on the bogies), wheels are plain discs, the track
  is a uniform ribbed belt; no end connectors, so links don't read.
- Stowage: two crates and two jerrycans on the engine deck; nothing on the
  turret or hull sides that says "crew lives here".
- Weathering: the shared grime (AO + blotches) everywhere at one strength;
  no dust gradient, no chipping logic.

## Design
Colour and paint
- US olive drab, lifted a little so it reads at 1x: base ~#6e6a46, no camo
  (US Shermans were plain OD). Separates it from the cream Panzer and the
  tan Abrams at game size.
- Civ colour (2 regions, target ~11-15 % of pixels):
  1. the turret band (wider, sitting on the turret's widest part, where it
     reads from every direction);
  2. the national stars: one big star per hull side, one on the glacis
     centre (replacing the two small glacis-corner stars), one on the
     engine deck (an air-recognition star, period-correct and visible from
     the high camera). All solid, no rings.

Construction (Sherman anatomy)
- Turret: cast, flatter roof, near-vertical sides, a rounded bustle drawn
  back; the wide M34A1 mantlet with its cheek lips; the 75 mm barrel a
  touch thicker; the loader's oval hatch and the commander's split hatch
  (one leaf open, a silhouette cue), the .50 cal on its pintle at the
  rear of the roof, a pistol port, periscope guards.
- Hull: the three-piece bolted transmission housing at the nose (a rounded
  half-drum with flange ribs), the glacis with the driver/co-driver hatches
  (rims, hinges, periscopes), the bow MG ball, headlights with guards,
  lifting rings, the sponson-overhang of the upper hull, a rear engine deck
  with grille doors and an exhaust deflector.
- Running gear: three VVSS bogies per side, each a bracket with two dished
  spoked wheels, the two volute-spring housings (vertical drums) and the
  return roller on top of the bracket (trailing, as on the later M4s);
  the high drive sprocket at the front with its teeth and hub; the idler
  at the rear; the track a run of steel links with a raised chevron cleat
  and the end connectors at both edges (the bright bumps that make the
  links read from the side).

Stowage (believable, not clutter)
- On the engine deck: a rolled canvas tarp strapped across the front of the
  deck behind the turret, a wooden ration crate, two jerrycans in a rack,
  a pair of duffel bags.
- On the hull sides: the pioneer tools (already) and a spare road wheel.
- Spare track links on the glacis (bolted bar), as crews did for armour.
- On the turret: a bedroll strapped on the bustle.

Weathering (one logic, not uniform grime)
- Dust low: a pale dust colour rising from the ground, strongest on the
  tracks, wheels, lower hull and fender lips, fading out by the sponson
  top; patchy (noise-masked), so the bottom edge isn't a flat gradient.
- Chips: dark steel chips only on convex edges (pointiness), masked by a
  fine noise so they're broken, never a continuous outline.
- Grime in crevices (the shared AO term) kept, the blotchy grime lowered on
  the paint so the upper surfaces stay clean OD.
- Tracks: dark steel with bare-metal wear on the cleats and connectors.
Shared code: new opt-in weather keys in props._weather ('dust', 'chips'),
zero by default, so every other unit's materials are unchanged. New Tank
options in armor.ARMOR['Tank'] (track style, VVSS bogies, wheel style,
stowage list); other vehicles keep their defaults.

## Rounds
Render times are Blender + compose inside run_locked (plus any wait for
the lock). Tint is the share of the unit's pixels in civ colour, DEFAULT,
directions 0/1/3/4/5/6/7.

- Round 0 (baseline, 22 s): see above. Tint 7-9 %.
- Round 1 (14 s, waited 65 s): olive drab, VVSS bogies, chevron links with
  end connectors, reshaped turret, nose housing, stowage, dust/chips.
  Wrong: the turret now a straight drum (T-34-like), dark chips read as
  big blotches on the smooth turret, the band thin (tint 3-4 %), the side
  stars overhanging the 0.24 m hull side (floating), OD too green and
  plastic. Next: taper the turret sides, smaller and rarer chips, band
  wider, stars sized to the plates, a faded browner OD.
- Round 2 (14 s): turret reads as the Sherman's again. Side view: daylight
  under the hull between the bogies (the belly too high, the ground shows
  through). Tint 5-7.5 %: still under target. Next: lower belly; a third
  civ region, a civ air-recognition panel tied over the engine deck (the
  1944 practice), and move the glacis star off the gun line (from the front
  the barrel cut it in two).
- Round 3 (14 s): the panel is a strong, deliberate region (tint 5-12 %),
  but a flat painted rectangle; tarp and duffels near-white. Measured value:
  new 90 mean luminance vs shipped 101, stock Panzer 136, Modern Armor 151,
  Artillery 75: in family. Next: cloth panel with folds and tied corners,
  darker canvas, band up to 0.2.
- Round 4 (14 s): band 0.2 swallowed the turret (a blue dome, no band;
  tint 10-18 %) and the OD drifted to khaki. Next: band back to three face
  rows, a greener OD.
- Round 5 (14 s, red civ): reads as an OD Sherman; side view clean (bogies,
  return rollers, sprocket, idler, link connectors, tools, tarp, panel).
  Dust barely visible on the lower hull. Next: dust higher up the sponson,
  exhaust soot on the rear plate.
- Round 6 (17 s): soot visible only on close inspection; dust falls off
  too fast (quadratic). Next: linear dust falloff.
- Round 7 (17 s): dust now on the nose housing, wheels and sponson foot.
  The open hatch leaf is edge-on from most directions. Next: open it past
  vertical so its face catches the light; dust a little stronger.
- Round 8 (17 s): fine at 2x; but at 1x the new tank sat smaller than the
  shipped one: the 0.4 m antenna counted in the fit's bounding box and
  shrank the whole model (ppm 65.3 vs 66.9). Next: drop the antenna.
- Round 9 (17 s): ppm 66.98 (shipped 66.85), the hull back to the shipped
  size at 1x. Tint 6.6-12 % (front views lowest: only the band and the
  glacis star face that way). Panzer re-rendered with the shared changes:
  pixel-identical to the shipped Panzer (mean abs diff 0.0001, max 4/255).

## Still weak (for the critic)
- Tint from the front quarter is ~6.5 %, under the 10 % floor; the hull's
  front has no room for more civ colour without noise.
- Chips are too fine to register at 1x (by design, but then they add
  little); the dust gradient is subtle at game size.
- The open hatch leaf and the hatch rims are 4-6 px at 2x: they read as
  "detail", not as hatches.
- OD on grass: the darkest vehicle beside the stock's pale camo; the
  outline and the civ regions carry it, but it is the one most likely to
  be picked out as "different" (on purpose: authentic Sherman colour).

## Critic round 1: FAIL 78/100 (readability 8, proportions 8, materials 7, authenticity 8, posing 8, consistency 7)
Civ colour at ~9 % judged enough; fixes asked, most important first
(directions 1-8 = the compare sheet's columns = render rows 0-7):

1. Value +15-20 % and tonal variation: OD lifted to #838650 (mean
   luminance 90 -> 106, +18 %; hue 0.16, sat 0.26: still olive). New
   opt-in weather keys: 'fade' (soft +-20 % value patches ~0.3 m across)
   and 'streaks' (dust runs down the steep faces only: hull sides,
   glacis, turret walls; flat decks stay clean).
2. Hull vs tracks: a lighter worn lip ('armor_edge', #a8aa80) along both
   sponsons' foot, plus the lighter hull over the dark running gear.
3. Gun: r 0.042 -> 0.052 (about +1 px at 1x), a muzzle collar and a dark
   bore disc; the mantlet in the darker OD (mantlet_key='armor_camo2').
4. Cut the ration crate, the duffel bags and the deck tools (orange specks
   at 1x). Kept: the tarp roll behind the turret, the turret bedroll, the
   jerrycan rack on the rear plate.
5. Turret band on the sides and rear only (band_front=0.45: faces within
   ~63 deg of straight ahead stay paint), so no full ring.
6. The barrel is its own object with visible_shadow off (gun_no_shadow):
   no long shadow streak on the ground.
7. Scale: the fit (render_vehicles.fit) scales the model so its projected
   box matches the Civ3 original's box. The Tank's long barrel was in that
   box, so the hull shrank (ppm 66.98, against the Panzer's 70.22 with its
   short gun). The barrel is now left out of the fit (gun_no_fit sets the
   object's 'no_fit' property; screen_points skips it): ppm 69.43. Hull in
   2x px: Tank 112 x 72, Panzer 118 x 74 (real M4 5.84 m vs Pz IV 5.92 m),
   Modern Armor 126 long (ppm 60.04, a 2.1 m model). DEFAULT cell 84x70
   (Panzer 85x69, Modern Armor 105x81, shipped Tank 95x75: the shipped
   cell had grown round the gun's long shadow). The fit procedure and
   the calibration files are unchanged; only what the box counts.

- Round 10 (18 s): all seven fixes in. Hull reads light olive, separated
  from the tracks; gun thick with a muzzle; no shadow streak; band C-shaped
  from the sides. Tonal variation still faint. Tint 3.2-12 % (front 3.2 %:
  the band's front is gone). Next: fade 0.2, streaks 0.5.
- Round 11 (18 s, red): streaks visible on the hull side and glacis; the
  colour drifting toward sage. Next: a touch more saturation.
- Round 12 (18 s, waited 10 s): #838650. At 1x beside the Panzer: same
  size class, hull separated from the running gear in every direction,
  the gun reads with a muzzle end, front and rear included.
  Panzer re-rendered again: identical to shipped (mean abs diff 0.0001,
  max 4/255). Modern Armor rendered once for its ppm (not installed).

## Still weak
- Front-view tint is now ~3 % (band front removed by request 5); the
  glacis star and the band's flanks carry it.
- Chips stay below 1x resolution; the hatches are small at 1x.

## Critic round 2: FAIL 73/100, CRITICAL: out of the house style (the last Tank round)
Every stock vehicle is light sand with a broken camo; the flat olive was
the odd one out and close to the green ground. PLAN.md point 8 (house
palette) and the new point 3 (civ colour 3-6 %, even, measured) apply.

1. Paint: house sand base #bea87a with the Panzer's mechanism (props.CAMO
   via the spec's camo list) in a scheme of its own: olive-drab blotches
   (#7e7c4e, freq 6.5) and earth patches (#6e5c40, freq 9), the US 1944
   disruptive OD-and-earth look over sand. Fade and streaks kept.
2. Civ colour: the rear-deck air panel removed; the band slimmed to two
   face rows and kept as a C (band_front 0.7: the front ~90 deg is paint);
   small stars (sides 0.11, glacis 0.145).
3. Gun: r 0.056, and a light top edge ('armor_edge' strip along the
   barrel's top) so it reads foreshortened.
4. Turret roof (simple_roof): one clear light hatch ring and the
   commander's MG; the loader's hatch, roof periscopes, lifting rings,
   split hatch and turret bedroll gone. The single stowage shape is the
   tarp roll behind the turret.
5. Hull (hull_edges): a worn light edge along both deck side edges, the
   sponson foot lip kept, darker weld seams (two vertical, one horizontal
   per side). Rear (rear_plain): the jerrycan rack gone; two door seams, a
   horizontal seam and two tow hooks on a plain plate.

- Round 13 (18 s, waited 10 s): in house style at a glance; camo scale and
  contrast close to the Panzer's. Tint 1.1-2.7 %: the one-row band read as
  a pin line. Next: two rows, bigger stars.
- Round 14 (18 s, waited 35 s): tint 2.0-5.4 %, front views low (2-3 %).
  Rear plate now blank. Next: open the band's C a little toward the front,
  a slightly bigger glacis star; seams and tow hooks on the rear.
- Round 15 (18 s, waited 15 s, red): final. Tint per direction (columns
  1-8): 4.1 / 3.8 / 3.7 / 5.2 / 4.9 / 5.4 / 4.7 / 5.0 % (mean 4.6).
  ppm 69.43, DEFAULT cell 84x70. Panzer re-rendered: identical to shipped
  (mean abs diff 0.0001, max 4/255).

## Still weak
- The camo is the Panzer's noise mechanism with other colours; at 1x the
  two read as siblings, which is the point, but the Tank's blotches are
  a little larger and browner than any stock scheme.
- The hatch ring and the MG are the only roof detail; chips stay sub-pixel.

## Critic round 3: 77/100, no critical failure (the user asked for rounds to 85)
1. Turret band: the solid ring at the turret's base was still the first
   thing seen at 1x. Now a thin stripe on the lowest face row, on the rear
   arc only (band_front 0.37 keeps the front ~136 deg paint, band_clear 22
   keeps the star zones clear), plus a civ star on each turret side
   (turret_star 0.11, set a little into the curved wall) and a star on the
   rear plate (rear_star 0.14); hull side stars 0.115, glacis star 0.17.
   Colour now sits as hull and turret decals, like the stock's.
   A dashed stripe (round 18, band_dash) read as a dotted line: dropped.
2. Gun: the barrel and mantlet in their own dark olive ('gun_olive'
   #56553a, gun_key, mantlet_key), the muzzle collar in gunmetal
   (muzzle_key), the light top edge kept, thickness kept (r 0.056).
   gun() takes a key (default the hull paint, as before).
3. Hull top and rear: a pair of big louvred engine doors in dark frames
   (deck_grilles); the drivers' hatches larger with light rims
   (hull_hatches 0.085); light edges where the glacis and the rear plate
   meet the deck; a third, darker camo tone in large patches
   (armor_camo3 #4c4432, freq 3.6).

- Round 16 (18 s): all three in, band dropped. Tint 0.2-1.9 %: the stars
  alone are too small, the rear view almost bare.
- Round 17 (18 s, waited 35 s): bigger turret stars and a rear star.
  Tint 1.0-2.3 %.
- Round 18 (18 s, waited 10 s): a dashed stripe at the turret base: tint
  1.7-2.3 % and even, but the dashes read as a dotted line at 1x.
- Round 19 (18 s, waited 20 s): final. A solid thin rear-arc stripe, the
  stars a little bigger. At 1x it sits with the Panzer: sand camo, small
  civ decals, a dark gun, dark deck grilles.
  Tint per direction (compare columns 1-8): 2.6 / 2.5 / 1.9 / 2.9 / 3.0 /
  3.6 / 3.0 / 2.9 % (mean 2.8; Panzer 2.2, Modern Armor 3.2).
  ppm 69.43, DEFAULT cell 84x70. Panzer re-rendered: identical to shipped
  (mean abs diff 0.0001, max 4/255).

## Still weak
- Tint mean 2.8 %, a little under the 3-6 % aim (front three-quarter
  1.9 %); more would need bigger markings than the plates hold.
- The turret-side stars sit on a curved wall; their points stand off it
  by about a pixel at 2x.

## Critic round 4: 77.5 (continuity 79, fresh 76), no critical failure
1. Gun not dark. Cause, measured: the barrel material did reach the mesh
   (side-view body pixels were olive, not sand) but (a) 'gun_olive' was
   mid olive #56553a with metallic 0.35 and roughness 0.55 (metallic < 0.5
   skips bscene's _metal_form, but the sheen and the default 'edges'
   lightening on the convex barrel lifted it), and (b) the gun_highlight
   strip was r 0.42 of the barrel, in the sand 'armor_edge', offset so it
   covered ~40 % of the barrel seen from above: in N the barrel read as a
   white pipe. Compose desat (0.9) isn't the cause.
   Fix: gun_olive #2e2d24, matte (metallic 0, roughness 0.8, no edge
   lightening); the highlight r 0.15 in its own mid tone 'gun_edge'
   #767256, on the very top; a dark ring at the barrel's root
   ('shade_dark'); the muzzle collar in shade_dark; the mantlet in
   gun_olive; the bore disc as before.
   Measured in the composed DEFAULT sheet (2x):
   - E side view (render row 3), barrel body: round 19 rgb 88/87/65
     (L 86) -> round 22 rgb 50/50/44 (L 50); the hull's darker camo
     there is L 77 (15th percentile), mid tone L 139.
   - N view (row 5, pointing up-screen), barrel incl. its lit top:
     round 19 rgb 161/161/139 (L 159, max 255) -> round 22 rgb 91/89/79
     (L 89, max 146).
2. Grilles: the frame was 'gunmetal' (metal 1.0 -> near black). Now a
   matte dark hull tone ('deck_dark' #5a5240), five slats in armor_shade,
   a lit rim ('armor_edge' plate under each); a tarp-wrapped bundle
   (deck_bundle) strapped over the left door; the tarp roll stays.
3. Separation: the sponson foot lip now dark ('shade_dark', lip_key) and a
   little taller; a dark shade ring where the turret meets the roof
   (turret_shade). The light deck-edge highlights stay.
4. Stars: the turret-side stars dropped (the NE view showed three); the
   glacis, hull-side and rear stars stay, one per face. To keep the tint,
   the turret stripe now runs the sides and rear (band_front 0.2, no
   clear zones), still a C open at the front.
5. Camo: olive blotches toward tan-brown and lower contrast (#968a62),
   earth #86724f, the third tone #5c503c in large patches.

- Round 20 (18 s): all five in. Barrel body L 60 (side), N view L 118 with
  a bright strip (max 248). Tint 1.5-3.0 %.
- Round 21 (18 s, red): highlight in its own mid tone; stripe along the
  sides. Tint 1.9-3.3 %. N view barrel L 103, max 170.
- Round 22 (18 s): darker gun_olive. Final. Tint per direction (columns
  1-8): 2.6 / 2.4 / 1.9 / 2.8 / 3.2 / 3.3 / 3.1 / 2.8 % (mean 2.8).
  ppm 69.43. Panzer re-rendered: identical to shipped (mean abs diff
  0.0001, max 4/255).

## Critic round 5: continuity PASS 85, fresh 76 (average 80.5)
1. Gun: barrel 10 % shorter (gun_len 0.666, the short 75 mm M3), the
   muzzle a slim ring (muzzle_slim) instead of the knob; dark tone and
   root thickness kept; still out of the fit.
2. Turret: lower (turret_rz 0.26) and flatter on top (roof_flat 0.45);
   the bustle drawn further back (1.45) with its underside lifted
   (bustle_lift 0.12) so it overhangs the deck behind the ring; a crisp
   dark mantlet block (mantlet_block); two clean hatches with light rims
   and dark lids (loader_hatch 0.075, hatch_key='armor_camo3'); the thin
   band broken under the bustle (band_rear 158: only the side arcs).
3. Hull sides: a dark shadow line under the side overhang above the
   track's top run (shadow_line); a shovel and a pick strapped on each
   side ahead of the star, dark handles, a light blade (side_tools); on
   the rear plate the two air-cleaner drums with dark caps (air_cleaners).
4. Front: a dark joint line and a lit flange where the glacis meets the
   cast nose (glacis_joint); the drivers' hatches with dark lids and light
   rims; the bow MG ball 1.3x in the gun's dark olive.
   Side star 0.12, rear star 0.15 to hold the tint.

- Round 23 (18 s, waited 20 s): all four in. The lower turret shrank the
  model's box, so the fit's ppm rose 69.43 -> 70.35 (the Panzer: 70.22).
  Tint 1.9-2.6 %.
- Round 24 (18 s, red): final. Band arcs a little longer, stars a little
  bigger. Tint per direction (columns 1-8): 2.7 / 2.4 / 2.0 / 2.4 / 2.6 /
  2.7 / 2.6 / 2.5 % (mean 2.5). DEFAULT cell 81x69 (Panzer 85x69).
  Panzer re-rendered: identical to shipped (mean abs diff 0.0001, max 4/255).

## Critic round 6: 82 (continuity 80, fresh 84)
1. Hatches: the pale rims read as a pair of eyes. Rims now a mid camo
   tone (hatch_rim='armor_camo2'), lids in the third tone; the commander's
   hatch is a small vision cupola (a raised drum with six dark slits and
   its lid on top: cupola), the loader's flat and sunk a little.
2. Camo: base lifted toward the Panzer's cream (#c4b68c), blotches lower
   in contrast (#a2946c, #958260, third tone #7e6e52), lower frequency
   (5.0 / 6.5 / 3.0) and fewer (thresholds up), with soft edges: a new
   optional 5th element per props.CAMO layer (the edge width, default the
   old 0.015) set from the spec's camo_soft (0.045). The deck-edge
   highlight toned down (armor_edge #d4c8a2).
3. Barrel: the blob was the smooth-shaded end cap of gun()'s lathe plus
   the bore disc standing 8-12 mm proud of the muzzle. Now (gun_tube) an
   open tube with a flat-shaded muzzle face, a recessed dark bore and a
   slim ring in the light 'gun_edge' tone, which also marks the muzzle in
   the foreshortened front views; length as round 6 (0.666).
4. Front: the glacis sloped further back (upper front 0.5 -> 0.56,
   front_low 0.04: ~57 deg from vertical), the turret moved back 0.02
   (turret_y -0.04) and the drivers' hatches wider apart (x 0.27, r 0.075)
   so they clear it; the deck tarp roll dropped (the bustle now overhangs
   where it sat) and the grilles centred between the bustle and the rear
   edge. ppm 70.42 (Panzer 70.22), DEFAULT cell 80x69 (Panzer 85x69).
5. The hull star is already at the side plate's limit (0.12 on a 0.24 m
   plate); left as is.

- Round 25 (18 s, waited 40 s): all in; camo too faint (plain sand).
- Round 26 (18 s, waited 25 s, red): final, a little more camo contrast.
  Tint per direction (columns 1-8): 2.6 / 2.3 / 2.0 / 2.6 / 2.7 / 2.8 /
  2.8 / 2.6 % (mean 2.6). Panzer re-rendered: identical to shipped
  (mean abs diff 0.0001, max 4/255).

## Critic round 7: 83.75 (continuity 87 PASS, fresh 80.5)
1. Running gear: the three VVSS bogies as groups. Wheel pairs closer
   (pair_gap 0.003) and the bogies further apart (centres -0.5 / -0.01 /
   0.48), so there is a clear gap between groups; brackets, wheel arms,
   roller posts and volute drums 1.3x chunkier in the dark 'shade_dark'
   (bogie_key, spring_key, bogie_chunk); the return rollers bigger
   (roll_r 0.042) on top of the brackets. vvss_bogie() takes the keys and
   the scale (defaults as before).
2. Gun toward the viewer: the muzzle ring in the light 'armor_edge' tone
   and 1.2x the barrel (muzzle_r), so a light circle round the dark bore
   stands clear of the dark mantlet in the front views.
3. Turret structure, height unchanged: a cast lip at the turret ring
   (proud of the wall, in the paint and camo) and a dark casting seam
   round the walls above the civ stripe (turret_cast).
   The optional thicker stripe is not done: the stripe is one row of the
   turret's faces; a second row doubles it.

- Round 27 (17 s): bogies read as three groups at 1x; the light muzzle
  ring shows in the front views. The seam sat inside the wall (invisible).
- Round 28 (17 s, red): seam darker, still buried.
- Round 29 (17 s, red): seam set 8 mm out; a thin dark line on the walls.
  Final. Tint per direction (columns 1-8): 2.6 / 2.3 / 1.9 / 2.6 / 2.6 /
  2.8 / 2.8 / 2.6 % (mean 2.6). ppm 70.42 (Panzer 70.22), DEFAULT cell
  80x69. Panzer re-rendered: identical to shipped (mean abs diff 0.0001,
  max 4/255).

## Critic round 8: 84.25 (continuity 82.5, fresh 86 PASS)
1. Turret top: the turret in its own paint ('armor_turret', turret_key)
   at 0.86 of the hull's tone, its camo scaled the same, so the dome is
   a darker mid tone than the hull and its shadow side goes darker; a
   near-black shadow ring under the cast lip; the cupola a darker raised
   drum ('armor_camo3', cupola_key) with two clear vision blocks (light
   glass in dark frames, vision_blocks); a dark pistol port proud of the
   left wall and a lifting eye on each side (side_fittings).
2. Gun head-on: a light catch along the mantlet block's top and side
   edges (mantlet_catch); the muzzle ring 1.35x and brighter
   ('muzzle_light' #e8dfc0) round a wider dark bore (bore_r 0.68).
3. Bogies at 1x: bogies further apart (-0.52 / -0.01 / 0.49; the idler
   moved back to -0.77), the lower hull's sides behind the wheels in the
   dark shadow tone (dark_belly_sides) so the gaps read dark, the brackets
   back in the light hull paint against the dark wheels and springs, the
   return rollers on top. Checked on a 1x crop: three groups.
4. Glacis star: centred (x 0) and set low on the glacis (t 0.64), just
   under the muzzle in the head-on view; the cast nose a little smaller
   (nose_r 0.095) so it doesn't hide the lower glacis; the spare links
   moved left (links_x -0.3).
5. Stripe doubled (two face rows) measured 3.5-4.4 %: over the 3 % cap, so
   back to one row.

- Round 30 (18 s): all in, stripe doubled: tint 0.8-4.4 %; the centred
  star sat right behind the muzzle head-on (0.8 %).
- Round 31 (18 s, red): one-row stripe; star at t 0.8 hidden by the nose.
- Round 32 (18 s): star at t 0.64, nose smaller. Final. Tint per
  direction (columns 1-8): 1.9 / 0.9 / 2.1 / 2.4 / 2.8 / 2.6 / 2.7 /
  2.3 % (mean 2.2; front view lowest, the star partly behind the
  muzzle). ppm 70.16 (Panzer 70.22), DEFAULT cell 80x69. Panzer
  re-rendered: identical to shipped (mean abs diff 0.0001, max 4/255).

## FINAL: round 8 (commit 2953a6a6), round 9 reverted
The Tank plateaued; round 8's parameters are the final. Round 9's code
(56772b57) is reverted in armor.py (no other file used it); the re-render
matches round 8's sheet (mean abs diff 0.0000014 over the comparison
sheet, max 1/255). final.png / final_red.png are round 8's.

Critic history (average; continuity / fresh where two):
- R1 78
- R2 73 (critical: out of house style)
- R3 77
- R4 77.5 (79 / 76)
- R5 80.5 (85 / 76)
- R6 82 (80 / 84)
- R7 83.75 (87 / 80.5)
- R8 84.25 (82.5 / 86) = FINAL
- R9 81.25, reverted

Open points from the last critics (for a later pass):
- The turret top is a smooth pale dome at 1x: needs a darker shadow side,
  a shadow ring under the cast lip, a darker cupola.
- The gun head-on reads as a dark square: needs a light catch on the
  mantlet's edges and a bigger muzzle ring.
- The bogies read as an even row at 1x.
- The glacis star sits lopsided in view 2.
- The turret stripe is thin in E/W.
Round 9 (56772b57, notes above) tried these; the strict critic said its
turret darkening, cupola and bogie gaps landed, so they're worth reusing
in a later pass (without its centred glacis star, which dropped the
head-on tint to 0.9 %).
