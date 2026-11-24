# Mech Infantry (realism pass): design

The Mech Infantry is an M2A2 Bradley (ODS, 1991): a tall, slab-sided
aluminium hull with bolted spaced-armour plates on its sides, a steep
upper glacis over a sloped lower nose with the trim vane, a two-man
turret with the 25 mm Bushmaster and the twin TOW launcher box folded on
its left side, six road wheels with the drive sprocket in front, armoured
side skirts, and the big rear ramp. Built as a painted 1/56 tabletop
miniature in the house sand with its own camo. Neighbours for the
comparison: Modern Armor (the new final) and Mobile SAM (stock).

## Baseline (round 0, as shipped)
- Reads as a generic APC box: a flat dark-grey turret, a tan TOW box, a
  plain slab hull with a trim vane, side skirts hiding the wheels, an
  antenna pair; big blue rectangles (tint 4.4-5.7 %, mean 5.0).
- Camo in strong cream, brown and grey blotches.

## Real proportions (M2A2)
Length 6.55 m, width 3.6 m over the armour (1.82 : 1), hull roof ~2.0 m,
turret roof ~2.6 m (antennas excluded), road wheels ~0.6 m. The turret
sits a little right of the centre line, behind the engine (front right)
and the driver (front left); the troop compartment and the ramp behind.

## What must read at 1x
1. The tall box hull with a steep glacis and the trim vane, the bolted
   side armour as rows of panels with dark bolt lines, the ramp.
2. The turret: small and angular on the tall hull, the 25 mm gun (dark,
   long, a thin barrel with a flash suppressor), the TOW box on its left
   (a distinct lighter block with two dark tube ends).
3. Running gear: six road wheels below the skirts, front sprocket, rear
   idler, dark track.
4. Value: tops lit, sides clearly darker, the lower hull and running gear
   darker still (see below).

## Values (measured, DEFAULT at 1x, opaque non-tint pixels)
values.py: stock vehicles have a wide spread (std 55-69, p10 26-59) and
their upper half is much lighter than their lower half (Panzer 164/109,
SAM 135/72, Tank 129/84). Our finals are flatter: Tank 137/99, Panzer
113/96, Modern Armor 138/105 (std 51-63). Aim here: std >= 60, p10 <= 35,
upper minus lower >= 40.

## Paint and civ colour
- House sand base, its own camo: hard-edged dark brown and olive-grey
  patches (a NATO-style disruptive pattern over sand), a little stronger
  than the Abrams' soft one; the turret the same paint.
- Civ (stock Mech Infantry 4.4-5.7 %, Modern Armor final 2.4-3.4 %):
  inverted-V chevrons on both side armour runs, the ramp and the turret
  sides; a small one on the glacis. Aim 3-5 %.

## Rounds
Tint per direction (compare columns 1-8); values from values.py.

- Round 1 (15 s): the new model (a rewritten _bradley). Reads as a
  Bradley at 2x: slab hull, bolted side armour, skirts, small turret with
  the 25 mm and the TOW box, ramp. Chevrons small: tint 0.5-1.3 %.
  Values: mean 124, p10 43, std 56, upper 135 / lower 115.
- Round 2 (15 s): bigger chevrons; the skirts in a dark dirt tone with a
  dirtier lower 60 %; the side armour in the second paint at 0.74.
  Tint 0.9-2.4 %. Values: std 60, upper 139 / lower 115.
- Round 3 (15 s): side armour paint 0.66, a second glacis chevron and one
  on the TOW box's outer face, side chevron 0.2. Tint 1.6-2.8 %. On a
  closeup the side armour still looked pale: the paint's 'dust' (1.0 up
  to 0.5 m) covered the whole hull side in pale dust (the same setting
  the Modern Armor and the Panzer carry: a likely cause of the critics'
  "pale" note).
- Round 4 (15 s): dust 0.45 up to 0.28 only. On the 2x sheet the side
  armour reads taupe and the skirts brown under the lit deck. Sent to
  critic round 1 (neighbours: the Modern Armor final and the stock
  Mobile SAM). Tint: 2.4 / 1.6 / 2.4 / 2.7 / 2.8 / 1.9 / 2.5 / 2.5 %
  (mean 2.4). Values: mean 125, p10 42, p50 120, p90 206, std 60, upper
  139 / lower 113 (stock Tank 129/84, Panzer 164/109, SAM 135/72; stock
  Mech Infantry 133/143). ppm 68.39 (shipped: see fit.json history).
No shared code changed (the Bradley's builder and spec only).

## Critic round 1: 62.5 (continuity 61, fresh 64), CRITICAL: floating plates
Both: thin plates rising above the deck at the front corners (a black
triangle in the side views, fins head-on, slivers past the roof from the
rear). Cause: the side-armour panels and their dark backing were boxes
of full hull height running forward past the glacis' top edge, so they
stood above the sloped glacis. Fixes:
1. The side armour and its backing are now solid extruded outlines whose
   top follows the hull's line (the deck, then down the glacis); the lit
   top edge and the bolt row run along the same line. Checked at 2x in
   every direction: nothing above the roof line.
2. Turret: a low wedge (sides sloped 0.3, the front face 0.45, rear 0.15,
   0.15 tall), a wider gun mantlet in the darker paint, flat square
   hatches in dark outlines (no round lids), the commander's ring of
   vision blocks.
3. TOW box: in the darker paint, no lit top, the two tube mouths with
   mid-tone rims round dark bores.
4. Hull taller toward the real M2A2: H 0.27 -> 0.36 (deck ~0.68 units,
   ~2.4 m at 3.52 m/unit; real ~2.57 m to the roof), the raked glacis and
   the sloped nose kept. Deck: stowage on the rear corners. Side chevron
   0.24.

- Round 5 (15 s, waited 10 s): all in. Sent to critic round 2. Tint per
  direction (columns 1-8): 2.8 / 1.5 / 2.8 / 3.3 / 3.1 / 1.7 / 3.0 / 3.2 %
  (mean 2.7). Values: mean 117, p10 44, std 54, upper 128 / lower 108.
  ppm 68.88. No shared code changed.

## Critic round 2: 74.9 (continuity 80, fresh 69.75), no critical failure
1. Turret ~22 % bigger (0.76 x 0.76 x 0.18) in the darker second paint,
   a wider shadow ring at its base (1.1), a wider mantlet.
2. TOW box bigger (0.16 x 0.5 x 0.16), in the dark deck tone, raised so
   its top shows over the turret in profile, light tube-mouth rims.
3. Sides: a horizontal seam and raised lit vertical edges on every
   armour panel, a sloped upper plate from the deck's edge out to the
   armour (lit) with a dark lip under it, harder camo edges (0.01), the
   deck paint lighter (#cab688) and the skirts darker (#5c4e3a).
   Gun 0.62 -> 0.74; the ramp with a dark frame and a hinge rod.

- Round 6 (15 s): all in; the darker turret pulled the upper half down
  (values: upper 112 / lower 106).
- Round 7 (15 s): deck lighter, skirts darker, turret paint 0.7. Sent to
  critic round 3. Tint per direction (columns 1-8): 2.8 / 1.6 / 2.7 /
  3.2 / 2.9 / 1.7 / 2.8 / 3.1 % (mean 2.6). Values: mean 112, p10 44,
  std 53, upper 116 / lower 108 (the turret, now dark by request, sits in
  the upper half, so this split will not reach the stock's). ppm 67.85.
  No shared code changed.

## Critic round 3: 83.1 (continuity 86.75 PASS, fresh 79.5)
1. 25 mm: barrel r 0.022 -> 0.03 (shroud 1.9x, suppressor 1.5x, its
   face and the top edge in the mid gun tone), length 0.76.
2. Turret offset checked: it was 0.08 right of the centre line (0.28 m at
   3.52 m/unit; the real Bradley's turret sits ~0.3-0.4 m right), too
   little to read at 1x; now 0.11 (0.39 m).
3. Civ: the small glacis chevrons (specks at 1x) replaced by one bigger
   one (0.16) left of the driver's hatch; turret-side chevrons 0.1 ->
   0.15, now set on the sloped wall itself (before they stood a little
   off it: the old 0.9 factor assumed a 0.2 side slope); TOW-box chevron
   0.1.
4. The turret's lit roof edges thinner and in the softer 'armor_edge';
   the TOW tube rims bigger (r 0.05).

- Round 8 (15 s): all in. Sent to critic round 4. Tint per direction
  (columns 1-8): 3.0 / 2.3 / 2.8 / 3.8 / 3.5 / 2.2 / 3.0 / 3.1 % (mean
  2.9). Values: mean 111, p10 43, std 53, upper 115 / lower 107. ppm
  67.85. No shared code changed.

## Critic round 4: 86.5 (continuity 88.5 PASS, fresh 84.5) = PASS

## FINAL: critic round 4 (own round 8, commit acfa3aab)
The unit passes (average 86.5, no critical failure). The final is the
current code; final.png / final_red.png are critic 4's (rows: shipped,
final, Modern Armor final, Mobile SAM stock).

Critic history (average; continuity / fresh):
- C1 62.5 (61 / 64; critical: card plates above the deck)
- C2 74.9 (80 / 69.75)
- C3 83.1 (86.75 / 79.5)
- C4 86.5 (88.5 / 84.5) = FINAL, PASS

Optional polish for a later pass (not needed): the 25 mm barrel reads a
little long in 4/7/8 (shorten it 25-30 % and thin it); more contrast in
the roof camo in 5/6; deeper TOW tube openings; the front chevron
centred; a deck detail between the turret and the rear hatch.

## Correctness fix after the final: detached antenna tips (specks.py)
specks.py (run on all finals after it was introduced) found 1-3 px pieces
in directions 3 and 5: the tips of the two antennas, whose 4.5 mm rods
broke up at 2x. The antennas' rods are now 9 mm (a new optional radius in
armor.antenna, default unchanged; the Bradley's spec sets antenna_r).
specks.py: clean. Against the recorded final (DEFAULT, 2x): 1277 px
changed by more than 2/255 (488 by more than 24), all along the two
antenna lines: per direction 103-203 px, e.g. dir 1 x 109-156 y 10-49,
dir 4 x 38-53 y 24-68; the tint layer 1 px. No other change; it stays
the final. final.png / final_red.png and the branch copies re-made from
the fixed render. Tint per direction (columns 1-8): 3.0 / 2.2 / 2.8 / 3.8 /
3.5 / 2.2 / 3.0 / 3.1 % (mean 2.9).
