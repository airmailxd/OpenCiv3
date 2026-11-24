# Modern Armor (realism pass): design

The Modern Armor is an M1A1 Abrams (1985-91): the long 120 mm M256 gun
with its thermal sleeve and bore evacuator, the low flat-sided wedge
turret with sharply angled cheeks and a long bustle carrying a stowage
basket, a long low hull with full-length side skirts, seven road wheels,
the drive sprocket at the rear. Built as a painted 1/56 tabletop miniature
in the house palette: desert tan with its own camo. Neighbours for the
comparison: Tank and Mech Infantry (stock).

## Baseline (round 0, as shipped)
- Reads as a modern tank, but generic: a slab hull with a full slab skirt
  that hides the wheels, a flat turret box with a stack of crates on the
  bustle, a thin pale gun, two tall antennas.
- The antennas and the gun count in the fit: ppm 60.0 against the Tank's
  70.4 and the Panzer's 71.6, so the whole vehicle is drawn small.
- Pale desert camo in cream and brown blotches; civ "C" arcs (stock tint
  0.2-4.1 %, mean 3.3; 0.2 % from the rear: no civ colour there).

## Real proportions (measured, M1A1)
Hull 7.92 x 3.66 m (2.16 : 1), height 2.44 m at the turret roof, the
gun overhanging the nose by ~1.9 m; turret about 3.7 m wide over the
cheeks and ~2/3 of the hull long with its bustle; road wheels 0.64 m.
At the model's ~3.7 m/unit: hull 2.14 x 0.99, roof 0.66, overhang 0.51,
road wheels r 0.087.

## What must read at 1x
1. The gun: a long dark barrel (out of the fit, no shadow), a lighter
   bore-evacuator collar a third of the way out, the thermal sleeve's
   band joints, and the muzzle reference sensor as a small block on the
   tip. Mid/dark steel, no white hot spot; a light top edge.
2. The turret: a low wedge, its cheeks angled back hard from a narrow
   front, flat vertical sides, the long bustle with a dark mesh basket
   holding bags; the commander's and loader's hatches, the .50 cal, the
   CITV/sight boxes as one or two larger shapes, smoke dischargers on the
   cheeks. In a darker tone than the hull.
3. The long low hull: a shallow glacis, the side skirts ending in the
   heavier forward sections, a dark shadow line under the skirts' top
   edge and a dark skirt foot, the track and wheels showing below.
4. Running gear: seven road wheels (paired spacing as on the real one),
   the rear sprocket, the front idler, dark tracks.

## Paint
- Base desert tan ~#c4ae80 (the house sand), the turret ~0.8 of it.
- Its own camo: large soft patches of a cooler grey-brown (~#9a8a68) and
  a few pale sand panels (~#d6c8a2), lower frequency than the Panzer's,
  at the stock's value contrast.
- Gun and running gear dark; the skirt foot and the hull underside dark.
- Weathering: the Tank's opt-in keys (dust low, streaks, fade, chips,
  exhaust soot at the rear grille).

## Civ colour
Stock class (tint.py, DEFAULT): Modern Armor 0.2-4.1 % (mean 3.3), Tank
6.8-9.1 % (7.9), Mech Infantry 4.4-5.7 % (5.0), Panzer final 2.5-3.3 %.
Aim 2.5-5 %, even across directions, small decal-like marks:
1. The inverted-V recognition chevron (the 1991 marking) on both skirts,
   both turret sides and the bustle rack's rear.
2. A small chevron (or bumper-code plate) on the glacis/front of each
   skirt, so the head-on view has colour.

## Construction notes
- Antennas out of the fit (no_fit), or dropped; the barrel no_fit.
- The skirt: full length, a heavier front section, a dark foot, the
  shadow line under its top edge; the road wheels' lower halves show.
- Any new shared code behind opt-in keys; the Tank and Panzer re-rendered
  and compared after it.

## Rounds
- Round 1 (15 s, waited 50 s): the new model (a rewritten _abrams): wedge
  turret, bustle basket, gun with sleeve joints, bore evacuator and MRS,
  full skirts with heavy forward sections, dark foot and shadow line;
  antennas and gun out of the fit: ppm 60.0 -> 65.7. Reads as an M1 at
  2x. The civ chevrons small and thin (tint 0.1-0.9 %); the turret
  barely darker than the deck; the camo faint.
- Round 2 (15 s): bigger chevrons, a chevron on the bustle basket's rear
  bag, lower turret (0.18), narrower front (0.55), camo a step stronger.
  Tint 0.7-1.9 %.
- Round 3 (15 s): a bold inverted-V shape ('vee', new, unused by other
  units), turret paint 0.72. Measured on a closeup: turret roof L ~165
  against the lit deck ~235: still too close. Tint 0.8-2.0 %.
- Round 4 (15 s): vees thicker and bigger, turret paint 0.66: the turret
  now a clearly darker mass at 1x. Sent to critic round 1. Tint per
  direction (columns 1-8): 2.9 / 2.2 / 2.9 / 2.9 / 2.8 / 1.3 / 2.8 /
  2.8 % (mean 2.6). ppm 66.06 (shipped 60.04).

Shared changes: none to behaviour; armor.py gains a 'vee' marking shape
and a 'brake_steel' colour key (both unused by the other vehicles).
  Tank and Panzer re-rendered: identical to their finals (mean abs diff
  0.000007 / 0.00002, max 1 / 2 of 255).

## Critic round 1: 66.25 (continuity 67, fresh 65.5), FAIL, no critical failure
Both: the hull and skirt a tall boxcar slab with the wheels a faint row;
the turret a rounded lump in a different (greyer) paint; muddy camo; a
notch at the bow; the model a little big. Fixes:
1. Running gear lower: sprocket and idler down (z 0.17), so the deck
   drops (z_f 0.35 -> 0.29); the skirt bottom at 0.85 of the wheel radius
   (was 0.5): the skirt ~38 % shallower, the wheels' lower halves and dark
   gaps show. The skirt as five separate panels with dark gaps and a lit
   bottom lip each, the heavy forward pair thicker and proud.
2. Turret: one paint with the hull (no turret_tone), lit edges round the
   roof's facets, the basket raised (rack_drop 0.5) with the dark gap
   under it.
3. Paint: the camo very soft and close to the base (grey-brown #aa9672,
   pale #d2c49e, low frequency, wide soft edges).
   Bow: the lower hull's nose nearly vertical (0.06) and a lit edge along
   the glacis foot: one sloped plate. Rear: the basket's chevron bigger.
4. Size: a new opt-in fit_scale (armor spec -> root['fit_scale'] ->
   render_vehicles.fit multiplies its ppm; default 1.0): 0.955 here, ppm
   66.6 -> 63.6 (shipped 60.0, Tank 70.4): bigger than the Sherman, as
   the real one is, without standing out.

- Round 5 (16 s, waited 45 s): all but the fit scale (its patch missed the
  CRLF file). ppm 66.58. Tint 1.4-3.1 %.
- Round 6 (15 s, waited 30 s): fit_scale 0.955, ppm 63.58. Sent to critic
  round 2. Tint per direction (columns 1-8): 3.0 / 2.2 / 3.0 / 3.0 / 2.8 /
  1.5 / 2.8 / 3.0 % (mean 2.6).
  Tank and Panzer re-rendered after the fit_scale change: identical to
  their finals (mean abs diff 0.000004 / 0.000005, max 1 / 2 of 255).

## Critic round 2: 72.25 (continuity 72.5, fresh 72), no critical failure
1. Hull lower and longer: the track top fixed at 0.23 (sprocket and idler
   z 0.15), road wheels r 0.08, the upper hull 0.07 (was 0.1): deck 0.45
   -> 0.33 (-27 %), turret 0.2 tall, so deck : roof = 0.33 : 0.54 = 0.61
   (real M1A1 ~1.5 : 2.44 = 0.61). L 2.14 -> 2.22.
2. Flat surfaces: wider panel gaps (22 mm) over a dark backing (each seam
   a dark line), each panel's lower third in a dirtier tan
   ('skirt_dirt' #a08a64), the driver's hatch bigger with a dark rim.
3. Turret vs hull: the turret paint 1.07 of the hull's (its roof lighter
   than the deck), a dark shadow band round its base on the deck, the
   roof's edges in the light 'muzzle_light'.
   Rear: two more chevrons on the basket's side bags (three in a row).
4. Size: measured on the DEFAULT sheet, side view (direction 3): the new
   Tank 133 x 79 px (2x, with its gun); round 7 at fit_scale 0.955 (ppm
   63.06) 177 x 64, hull 140 px against the Tank's 114 (+23 %). fit_scale
   0.84: ppm 55.47, side bbox 156 x 56, hull 2.22 x 55.47 = 123 px against
   114 (+8 %). (Shipped: ppm 60.0, hull 126 px.)

- Round 7 (15 s): all of 1-3, fit_scale 0.955. Tint 2.1-3.4 %.
- Round 8 (15 s, waited 35 s): fit_scale 0.84. Sent to critic round 3.
  Tint per direction (columns 1-8): 2.9 / 1.9 / 2.9 / 2.8 / 3.2 / 2.2 /
  3.2 / 2.8 % (mean 2.7). ppm 55.47.
  Shared: one new colour key ('skirt_dirt'), unused by the other vehicles.

## Critic round 3: 78.4 (continuity 80.75, fresh 76), no critical failure
1. Too pale and smooth: base ~10 % darker (#b09c72; soft camo #968460 /
   #c2b48e, a little denser), the turret 0.98 of the hull (its roof no
   longer blows out), more grime (0.5) and fade (0.3) on the paint; the
   loader's hatch rim dark.
2. Turret front: a dark line under each cheek's top edge and down its
   front corner (cheek_lines), the lit roof edges kept.
3. Running gear: a lit hub on every road wheel (a new opt-in 'hub_key'
   in chassis/wheel, default the old gunmetal); the skirt run shortened
   at both ends (skirt_y -0.96 .. 0.98) so the sprocket and idler stand
   clear; a dark band low on the nose (bow_dark).
   Rear: the basket chevrons a little bigger (0.17 middle, 0.13 sides).

- Round 9 (15 s): all in. Sent to critic round 4. Tint per direction
  (columns 1-8): 3.1 / 2.1 / 3.1 / 2.8 / 3.9 / 3.0 / 3.9 / 2.8 % (mean
  3.1). ppm 56.52 (the shorter skirts shrank the fit's box): hull 2.22 x
  56.52 = 125 px against the Tank's 114 (+10 %).
  Tank and Panzer re-rendered after the hub_key change: identical to their
  finals (mean abs diff 0.000008 / 0.000004, max 1/255).

## Critic round 4: 75.75 (continuity 85.25 PASS, fresh 66.25): no gain
1. Turret, measured (3.57 m/unit): height above the deck 0.212 (0.76 m;
   real ~0.9 m: not too tall), its ring 0.12 behind mid-hull (0.43 m) ->
   0.04 (0.14 m; the real ring sits about mid-hull); side taper 0.06 ->
   0.12 and the front faces sloped 0.14 -> 0.22, the cheeks narrower at
   the front (plan_front 0.55 -> 0.6): a sharper, lower-looking wedge; the
   roof's lit edges and cheek lines follow the new taper.
2. Surfaces: panel-line shadows across the deck, a line down each glacis
   side, a tow cable along each deck edge, dark front fender lips; the
   barrel darker (#24231c).
3. Running gear: the hubs a step lighter ('muzzle_light'), the skirt's
   bottom lip a mid tone (was lit) and its lower third darker (#8c7858):
   a darker band from the skirt foot down.
4. Basket: an open rail frame (posts along the rear and sides), deeper
   (0.26), three smaller packs spaced apart, the three chevrons spaced
   with them.

- Round 10 (15 s): all in. Sent to critic round 5. Tint per direction
  (columns 1-8): 3.2 / 2.4 / 3.3 / 3.0 / 3.4 / 2.9 / 3.3 / 3.0 % (mean
  3.1). ppm 56.74. No shared code changed.

## Critic round 5: 78.75 (continuity 87 PASS, fresh 70.5) -> plateau

## FINAL: critic round 5 (own round 10, commit f6320565)
Two critic rounds (4, 5) without beating the best by 1.5: plateau. Round
5 is itself the best-scoring round, so the final is the current code;
nothing restored. final.png / final_red.png are critic 5's (rows:
shipped, final, Tank, Mech Infantry). Tint per direction (columns 1-8):
3.2 / 2.4 / 3.3 / 3.0 / 3.4 / 2.9 / 3.3 / 3.0 % (mean 3.1). ppm 56.74
(fit_scale 0.84), hull ~126 px at 2x against the Tank's 114.

Critic history (average; continuity / fresh):
- C1 66.25 (67 / 65.5)
- C2 72.25 (72.5 / 72)
- C3 78.4 (80.75 / 76)
- C4 75.75 (85.25 / 66.25)
- C5 78.75 (87 / 70.5) = FINAL; plateau

Open points for a later pass:
- The fresh critics' recurring note: pale, flat, low contrast next to the
  stock Tank and Mech Infantry; the side faces want to be darker than the
  tops, more tonal variation in the paint, more dust toward the tracks.
- The basket reads as a slightly tall block at the rear in 4/8: lower it
  about a pixel.
- The turret's length and position measure close to the real M1A1, yet a
  fresh critic still read it as long and set back.
