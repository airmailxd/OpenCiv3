# Ironclad (realism pass): design

The Ironclad is a casemate ironclad of 1862 (CSS Virginia's type): ~84 x
15 m (L:B ~5.5), a long iron hull nearly awash with a rusty waterline, a
sloped iron casemate with rounded ends in courses of plate, lidded
gunports, a dark grated roof, a pilot house at its fore end, one tall
funnel, an iron ram at the waterline. Neighbours (stock): Man-O-War and
Transport.

## Baseline (as shipped)
Tint (tint.py, DEFAULT): 11.4 / 12.5 / 11.4 / 9.5 / 10.6 / 12.7 / 10.4 /
9.5 % (mean 11.0). Values: mean 84, p10 35, std 37, upper 106 / lower 75.
Footprint 3037 px.

## What must read at 1x (the fleet's lessons, steel ships)
The low dark hull with the casemate as the one big shape (lit sloped
sides with light plate edges, a dark roof), the tall funnel; parts thick
enough to hold (no rails or thin rods); civ colour near the stock's.

## Civ colour (stock 11.0 %)
Deck patches fore and aft (as the stock), a band round the funnel, a
solid civ ensign at the stern.

## Rounds
New builder ships.ironclad_casemate in its own block (before
_steel_builder); BUILDERS['Ironclad'] set after the dict. No other unit
changed (block check: one per unit).
- Round 1: the casemate ironclad. Footprint 2267 (small), the casemate low
  and flat, everything one grey (std 29); tint 13.0 %.
- Round 2: casemate 0.21 tall, funnel 0.5 x r 0.06, the civ patches
  shorter, hull and deck darker, fit_scale 1.12 (footprint 2999).
- Round 3: casemate plating lighter (#746c62 / #665f56), plate edges
  lighter, roof grating a step lighter. Sent to critic round 1. Tint per
  direction (columns 1-8): 10.0 / 8.7 / 9.3 / 9.1 / 10.2 / 7.9 / 9.5 /
  9.1 % (mean 9.2). Values: mean 92, p10 44, p50 92, p90 138, p99 172, std
  37, upper 87 / lower 94 (stock 84, 106 / 75). ppm 74.52; footprint 2999
  (stock 3037). specks.py: clean.

## Critic round 1: 63.5 (A 61.5, B 65.5), no critical failure
2 and 6 close on identity ("a grey lozenge with a chimney"). Both: the
casemate read as pale concrete or a houseboat cabin (the lightest thing on
the ship): sooty near-black iron, a step darker than the deck, plate
courses, deep black gunports with lit sills; the deck nearly awash with a
waterline wash; a protruding iron ram with a highlight, a sharper prow; a
shorter, ~1.5x wider funnel with the civ band in its top third; a clear
pilot house; civ placements believable (a casemate-top stripe, the
funnel band, the ensign; not the deck-end triangles).

- Round 4: casemate #2e2c2a / #262422 (the deck #4e463c now lighter than
  it); D 0.05 -> 0.032; a light wash line at the waterline; the bow
  sharper (bow_p 1.2); the ram 0.2 long, raised to show, with a lit top
  edge; ports 0.07 x 0.055 black with lit sills below; the funnel 0.34
  tall, r 0.09, the civ band in its top third; the pilot house a
  truncated cone in the light edge iron; the deck patches gone, a civ
  stripe along the casemate roof. Tint 7.2 %.
- Round 5: the roof stripe 0.09 wide, the ensign 0.26 x 0.17. Sent to
  critic round 2. Tint per direction (columns 1-8): 12.2 / 8.4 / 11.5 /
  12.1 / 12.6 / 9.0 / 11.7 / 12.0 % (mean 11.2). Values: mean 79, p10 46,
  p50 71, p90 115, p99 187, std 32, upper 85 / lower 78 (stock 84, 106 /
  75). ppm 73.83; footprint 2882 (stock 3037). specks.py: clean.

## Critic round 2 (batch naval 1): 64.6 (A 69.25, fresh 60): +1.1, no gain
Shared steel notes (both critics): flat, evenly lit steel; hulls not
sitting in the water; civ as decal patches; parts too small at 1x; thin pale
slivers next to the chunkier stock. Ironclad: a pale plank or needle (a
darker wet-iron deck, a dark waterline band, a small bow wave, more beam,
a rounded stern, plate seams); the pilot house and the forward knob bright
white balls (dark iron, a clear truncated cone); the casemate taller and
steeper with deeper black port recesses; the ram visible in 4/8.

- Round 6 (CPU draft): B 0.42 -> 0.5, a rounded stern (stern_p 2.6),
  plate seams along the side (alternating plates every 0.16), the deck
  dark wet iron (#3a3632), the casemate #262422 / #201e1c, 0.25 tall with
  steeper sides (top 0.42 of the base); deep black port recesses; a dark
  waterline band and a foam bow wave; the pilot house and the ventilator
  dark iron (the pilot house's foot a thin lit ring). The wave's thin tips
  specked in 2; footprint +12 %.
- Round 7 (GPU): the wave thicker and closer, fit_scale 1.06. Sent to the
  batch round (batch_naval_2; the unit's own sheet review/ironclad/batch2).
  Tint per direction (columns 1-8): 10.6 / 6.6 / 10.1 / 10.3 / 10.9 / 7.4
  / 10.2 / 10.1 % (mean 9.5). Values: mean 70, p10 37, p50 64, p90 113,
  p99 162, std 31, upper 78 / lower 67 (stock 84, 106 / 75). ppm 69.25;
  footprint 3056 (stock 3037). specks.py: clean.

## Critic round 3 (batch naval 2): 70.9 (A 75.75, fresh 66): the best, cap reached

## FINAL: critic round 3 (own round 7, commit ceac0c0c)
The current code. The re-render matches the batch-2 sheet (the unit's
review/ironclad/batch2 sheets: mean abs diff 0.0, max 1/255). Tint per
direction (columns 1-8): 10.6 / 6.6 / 10.1 / 10.3 / 10.9 / 7.4 / 10.2 /
10.1 % (mean 9.5). ppm 69.25; footprint 3056 (stock 3037). Finals in
ArtGen/units3d/reviews/realism/ironclad/ and the handoff folder (rows:
shipped, final, Man-O-War, Transport). Added to the finals gate.

Critic history (average; A / fresh):
- C1 63.5 (61.5 / 65.5)
- C2 64.6 (69.25 / 60)
- C3 70.9 (75.75 / 66) = FINAL; cap

Open points: the pale plate rails (the plate-edge lines on the casemate)
read as railings; the gunports small; the waterline band thin; a
symmetric cigar hull with no distinct stern or rudder.
