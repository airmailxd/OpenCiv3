# Frigate (realism pass): design

The Frigate is a 36-gun frigate of c. 1780-1805 (a Royal Navy fifth
rate): ~44 x 12 m (L:B ~3.7), a long, low, flush hull, black with an
ochre band through the single row of gunports and black port lids (the
"Nelson chequer"), copper below the waterline, a low quarterdeck and
forecastle, a galleried stern with gilt windows, a figurehead under a
long bowsprit with jibs; three masts square-rigged with courses, topsails
and topgallants, a spanker on the mizzen, dark tops and standing
rigging. Neighbours (stock): Galleon and Man-O-War.

## Baseline (as shipped)
Tint (tint.py, DEFAULT): 7.4 / 4.3 / 7.2 / 6.2 / 5.8 / 4.9 / 5.6 / 6.4 %
(mean 6.0). Values: mean 109, p10 33, std 61, upper 135 / lower 98.
Footprint 8557 px.

## Real proportions
L 3.0 = 44 m: B 0.8, D 0.26 + 0.08 bulwark, low sheer (0.14 / 0.12);
main course 1.24 x 0.52, topsail 1.0 x 0.46, topgallant 0.68 x 0.28;
fore and mizzen a step smaller; bowsprit 0.62 out.

## What must read at 1x (the fleet's lessons)
The black-and-ochre banded hull with the row of black ports (the period's
signature), closed decks with hatches; the tall three-masted square rig,
braced 20 deg with belly 0.26 (the fleet rule: no edge-on slivers); jibs
and spanker for the ends; dark tops and shrouds (r 0.016).

## Civ colour (stock 6.0 %)
A civ roundel (a light cross in it) on the courses and topsails of fore
and main; a civ pennant at the main truck.

## Rounds
New builder ships.frigate_36 in its own block (after the Galleon's);
BUILDERS['Frigate'] set after the dict. No other unit changed.
- Round 1: the frigate. Tint 2.1 % (the stock roundel small on these
  sails).
- Round 2: the roundel 1.45x on courses and topsails: clipped by the sail
  edges, tint 8.4 %. (A splice-script slip duplicated the block; fixed,
  only the new block in the diff.)
- Round 3: the roundel 1.12x. Sent to critic round 1. Tint per direction
  (columns 1-8): 7.5 / 8.5 / 5.0 / 3.0 / 5.6 / 5.8 / 4.1 / 4.2 % (mean
  5.5). Values: mean 105, p10 30, p50 111, p90 173, p99 224, std 56,
  upper 129 / lower 91. ppm 53.07; footprint 8822 (stock 8557).
  specks.py: clean.

## Critic round 1: 63.5 (A 63.5, B 63.5), no critical failure
The spanker borderline (a free triangle on a boom past the stern).
Fixes: the mizzen ~80 % of the main with a topsail and topgallant, a
four-sided gaff spanker whose boom ends at the taffrail; some sheer,
higher bulwarks with a dark rail, a darker deck with gratings, a boat, a
capstan and guns; lit stern windows and quarter galleries, head rails and
the figurehead; the hull toward the stock brown; ~13 larger ports;
bracing rule v3, the rig ~15-20 % taller, fuller sails, a thicker
bowsprit and jib-boom.

- Round 4: sheer 0.2 / 0.16; the rig ~15 % taller (course heads 1.3 /
  1.45 / 1.18; main course 1.42 x 0.6), the mizzen with topsail and
  topgallant (its course is the spanker); a four-sided gaff spanker (gaff
  0.62 peaked aft, the boom to the taffrail); brace 30, belly 0.3; hull
  dark brown #3a2a1c with the ochre band; bulwark 0.1 with a dark rail;
  deck #6a5030 with a boat, a capstan and four carronades a side; 13
  ports a side (0.07 x 0.066); lit stern lights (#e0b860) and quarter
  galleries; head rails; bowsprit r 0.034 to yb + 0.72. Sent to critic
  round 2. Tint per direction (columns 1-8): 8.3 / 8.9 / 4.3 / 4.7 / 5.8
  / 4.2 / 2.6 / 5.4 % (mean 5.5). Values: mean 102, p10 30, p50 104, p90
  177, p99 230, std 58, upper 134 / lower 82. ppm 50.04; footprint 9358
  (stock 8557). specks.py: clean.

## Critic round 2: 77.5 (continuity 82.25, fresh 72.75): gain (+14.0)
The gun band, dark hull and sail mass landed. Fixes: the sails read as
separate curled, twisted paper panels with gaps (a smooth belly, closed
gaps, consistent 30 deg); the mizzen truly ~80 % of the main, the spanker
with a raked gaff, a visible boom, darker canvas; the hull a step lighter
(dark warm brown) with lighter wales and rails; a bigger gilt figurehead
and head rails; long civ masthead pennants; footprint toward ~5 % over.

- Round 5: one belly (0.24) on every square sail; the sails close up
  (0.01 between tiers instead of 0.05); the mizzen's course head 1.32 with
  a bigger topsail and topgallant; the spanker's gaff peak at 0.62 of its
  length, the boom r 0.024, the canvas a shade darker; hull #4a3422 /
  #42301e (below #36261a), wales mid-tone (#6e5030); figurehead 0.05 x
  0.1 gilt, head rails gilt r 0.015; a civ pennant at every masthead;
  fit_scale 0.98. The long pennants specked in 2.
- Round 6: pennants 0.46 x 0.17, set under the trucks. Sent to critic
  round 3. Tint per direction (columns 1-8): 9.5 / 9.0 / 5.5 / 6.5 / 6.7 /
  3.3 / 4.0 / 6.9 % (mean 6.4). Values: mean 103, p10 34, p50 106, p90
  173, p99 233, std 56, upper 134 / lower 83. ppm 49.11; footprint 9192
  (+7 %). specks.py: clean.
(No reef bands: the square sails are drawn by the Galley's shared
_square_sail, which must stay as it is.)

## Critic round 3: 80.5 (continuity 84, fresh 77): gain (+3.0)
Fleet sail-shape rule (straight edges along the yard, leeches and foot,
the belly only mid-cloth, the emblem in the flat centre, clean canvas,
one brace angle). Fixes: three clean tiers per mast with faint seam or
reef lines; the spanker with a clearly steeper gaff, a trapezoid, a step
darker; a longer, thicker bowsprit and jib-boom with bigger jibs, a bright
gilt figurehead and cutwater; the hull wood a little lighter.

- Round 7: the Frigate's own sail helper _fg_sail (the rule, two reef
  bands; the shared _square_sail untouched); the spanker's peak at 0.9 of
  the gaff, the canvas 'sail_shade'; bowsprit r 0.042 to yb + 0.86 (the
  jibs follow it); a bigger bright-gilt figurehead and a gilt cutwater
  line; hull #56402a / #4e3a26. Sails p99 245.
- Round 8: sailcloth a step darker (#d6c6a2). Sent to critic round 4.
  Tint per direction (columns 1-8): 9.2 / 9.7 / 5.8 / 6.4 / 6.8 / 3.3 /
  4.2 / 6.9 % (mean 6.5). Values: mean 106, p10 35, p50 106, p90 179, p99
  236, std 58, upper 139 / lower 84. ppm 48.14; footprint 9018 (stock
  8557, +5 %). specks.py: clean.

## Critic round 4: 77.5 (continuity 84, fresh 71): no gain (1 of 2)
The C5 round was cancelled by the user's 3-critic-round cap.

## FINAL: critic round 3 (own round 6, commit 36b98625)
The Frigate's block restored to 36b98625's (byte-identical; block check one
per unit). The re-render matches critic 3's sheets: final.png and
final_red.png mean abs diff 0.000003, max 3/255 (one pixel over 2/255).
Tint per direction (columns 1-8): 9.5 / 9.0 / 5.5 / 6.5 / 6.7 / 3.3 / 4.0 /
6.9 % (mean 6.4). ppm 49.11; footprint 9192 (stock 8557, +7 %). Finals in
ArtGen/units3d/reviews/realism/frigate/ and the handoff folder (rows:
shipped, final, Galleon, Man-O-War).

Critic history (average; A-continuity / B-fresh):
- C1 63.5 (63.5 / 63.5)
- C2 77.5 (82.25 / 72.75)
- C3 80.5 (84 / 77) = FINAL
- C4 77.5 (84 / 71); capped

Open points (C4's critics): the topsail and topgallant leeches S-curl in
3/4/7/8 (the belly too near the edges); the courses hard to see in 4/8
(the lower masts look bare); the emblems stair-stepped on angled sails;
staggered bracing per mast (fore ~35, main ~25, mizzen ~30) instead of one
angle; a gilt figurehead block of 3x3 px at 1x in 2; the spanker a step
darker with seam lines. (C4's flat-edged sail helper and gilt bow are in
the scratchpad, not in the final.)
