# Caravel (realism pass): design

The Caravel is a Portuguese caravela redonda of the 1490s (the Nina as
re-rigged for Columbus's first voyage): ~21 x 6.4 m (L:B ~3.3), a short,
beamy, strongly sheered carvel hull with tumblehome, a high two-step
aftercastle over a transom with a sternpost rudder, a low forecastle, a
short bowsprit; square fore- and mainsails (a main topsail, a fighting top)
and a lateen mizzen; the sails with the cross pattee of the Order of
Christ. Neighbours (stock): Galleon and Dromon.

## Baseline (as shipped)
Tint (tint.py, DEFAULT): 12.1 / 13.8 / 12.0 / 8.2 / 9.6 / 12.5 / 9.3 /
7.8 % (mean 10.7). Values: mean 108, p10 38, std 51, upper 125 / lower
96. Footprint 5608 px.

## Real proportions
L 2.4 = 21 m (1 unit = 8.75 m): B 0.72, D 0.2 plus a 0.07 waist bulwark,
sheer 0.34 stern / 0.28 bow; main course 1.12 x 0.7 (top at 1.62), fore
0.8 x 0.5, main topsail 0.6 x 0.32, lateen mizzen yard 1.3 (the rig ~1.1x
real).

## What must read at 1x (the Galley's lessons)
1. Warm, saturated wood, darker below the wales; two dark wales; a light
   cap on the bulwark and the castle rails; a visible sheer.
2. Ends: the high stepped aftercastle with the gilt stern lantern and
   the rudder (stern), the bowsprit over a low forecastle (bow).
3. The rig: two square sails across (with the topsail), the lateen mizzen
   fore-and-aft (sail area in the profiles).
No standing rigging.

## Civ colour (stock 10.7 %)
The cross pattee on the fore and main courses and on the mizzen; a civ
pennant at the main truck.

## Rounds
New builder ships.caravel_redonda in its own block (after the old
caravel()); BUILDERS 'Caravel'. No other unit changed.
- Round 1: the caravela redonda. ppm 50.46, footprint 5382 (stock 5608).
  The hull read pale: the wide deck (#957444) seen from above dominated,
  the side a thin band; the wales faint.
- Round 2: hull wood darker, wales thicker: little change (the deck was
  the pale mass; checked with a test colour).
- Round 3: a 0.07 waist bulwark with a light cap (the side carried up,
  less deck seen), the deck darker (#7e603a). Sent to critic round 1.
  Tint per direction (columns 1-8): 9.7 / 18.2 / 9.8 / 4.0 / 9.9 / 10.7 /
  9.9 / 4.1 % (mean 9.5). Values: mean 104, p10 40, p50 99, p90 170, p99
  213, std 52, upper 130 / lower 91. ppm 50.46; footprint 5462. specks.py:
  clean.

## Critic round 1: 54.0 (A 51, B 57), CRITICAL (both)
"Out of house style: a bare open rowboat with no rigging next to the
stock ships"; weak identity in 4/8 (no visible stepped aftercastle; the
stock Caravel is a boxy stepped hull with visible standing rigging, so
rigging is on-roster here).

- Round 4: the Carrack's castle and shroud helpers copied in (own names):
  a stepped aftercastle (0.2 / 0.4 over the deck, painted sides, a dark
  band under a light rail on each tier; the upper tier's band in the civ
  colour) over the flat transom, a raised forecastle (0.13); sheer 0.42 /
  0.32; three wales; a bigger sternpost rudder; hatches on a lighter
  covered deck; the mizzen moved forward (y -0.6, yard 1.12) so the
  lateen stays over the stern; shrouds with deadeyes and channels (three
  a side fore and main, two on the mizzen), forestay and mainstay, r
  0.016, dark #2e2418; belly 0.2. Footprint 6135 (+9 %), tint 8.0 %.
- Round 5: fit_scale 0.97; the cross bigger ('pattee_big', 1.25 x).
  Tint 12.5 %.
- Rounds 6-8: the cross 1.15 x; the pennant shorter and deeper, set just
  under the truck (its top corner was a 2 px speck in 3). Sent to critic
  round 2. Tint per direction (columns 1-8): 11.6 / 23.2 / 11.6 / 4.4 /
  9.2 / 10.9 / 9.2 / 4.6 % (mean 10.6). Values: mean 92, p10 35, p50 77,
  p90 163, p99 210, std 51, upper 120 / lower 79 (stock 108, 125 / 96).
  ppm 48.67; footprint 5487 (stock 5608). specks.py: clean.

## Critic round 2: 74.6 (continuity 79.5, fresh 69.75): gain (+20.6)
No criticals left. Fixes: brace fore and main ~30-35 deg (the fleet
lesson), sails ~15 % bigger, the civ colour more even (the foresail's
cross thinner for 2, 4/7/8 up); darker, thicker wales (dark-light-dark at
1x), walnut wood; the upper aftercastle tier 30-40 % taller, the rudder
visible; sail belly.

- Round 9: courses 0.92 x 0.58 and 1.29 x 0.8, topsail 0.69 x 0.36,
  braced 32 deg; belly 0.24; the foresail's cross the plain pattee (the
  main keeps the 1.15x one); wood #4e3418 / #442c14 (below the wales
  #36240f), wales #161210 at r 0.022 / 0.028 / 0.022; the upper aft tier
  0.4 -> 0.54. The main topsail sat above its mast's top.
- Round 10: the mainmast to the topsail's head. Sent to critic round 3.
  Tint per direction (columns 1-8): 15.7 / 23.3 / 6.0 / 8.0 / 10.8 / 12.7
  / 4.0 / 10.5 % (mean 11.4; 3 and 7 see the braced sails edge-on).
  Values: mean 95, p10 30, p50 88, p90 168, p99 224, std 55, upper 128 /
  lower 79. ppm 47.8; footprint 5618 (stock 5608). specks.py: clean.

## Critic round 3: 82.6 (continuity 84, fresh 81.25): gain (+8.0)
Fixes: the pale upper planking and castle sides toward walnut (light rail
caps only), plank lines, a balustrade and a darker band on the
aftercastle; the fleet bracing (~18-22 deg with belly) so 3 and 7 aren't
slivers, ~8 % civ in 3/7; the mizzen a triangular lateen on a long,
steep yard in 8/5 (not a square sail with a cross); less deck clutter.

- Round 11: castle sides #4e3418 with a painted band (#7e5a30; the upper
  aft tier's band in the civ colour), a light deck line, a dark rail on a
  balustrade; deck #86683e; brace 20, belly 0.28; a civ stripe along the
  waist bulwark; the mizzen lateen plain (no cross) on a longer, steeper
  yard (1.36, 42 deg, mast top 1.3). A 3 px speck in 3 (a thin edge near
  the main topsail's yard-arm).
- Rounds 12-13: the pennant lowered under the truck (no change); brace
  22 deg (the speck gone). Sent to critic round 4. Tint per direction
  (columns 1-8): 14.8 / 23.4 / 8.4 / 5.2 / 10.0 / 13.0 / 5.0 / 7.1 % (mean
  10.9). Values: mean 94, p10 31, p50 78, p90 171, p99 229, std 56, upper
  129 / lower 76. ppm 47.0; footprint 5622 (stock 5608). specks.py: clean.

## Critic round 4: 79.75 (continuity 86.5 PASS, fresh 73): no gain (1 of 2)
Fresh: the 22 deg bellied sails "twisted, crumpled, folded paper", near
edge-on in 3/4/7/8. Lead: back to C3's bracing and sail shape (32 deg,
C3 belly), keep the C4 hull and castle work; the aftercastle a little
higher, a stronger sweep at both ends, the rudder visible in 4/8;
lighter wales and rails with plank lines in 1/5/7; in 3 the main yard
opened a few degrees.

- Round 14: brace 32, belly 0.24 (C3); the main yard 28 deg; sheer 0.48
  stern / 0.38 bow; the upper aft tier 0.62; the rudder bigger (0.19 aft,
  0.03 thick); wales mid-light (#7a5a34), plank alternation stronger. A
  2 px speck in 3.
- Round 15: main 29 deg: the speck stays (the pennant's tip, edge-on).
- Round 16: the pennant shorter (0.24 x 0.15). Sent to critic round 5.
  Tint per direction (columns 1-8): 15.4 / 22.7 / 6.1 / 7.1 / 9.9 / 12.6
  / 3.1 / 8.1 % (mean 10.6). Values: mean 94, p10 36, p50 80, p90 168, p99
  220, std 52, upper 125 / lower 78. ppm 47.09; footprint 5718 (stock
  5608). specks.py: clean.

## Critic round 5: 79.25 (continuity 86.5 PASS, fresh 72): no gain (2 of 2) -> plateau

## FINAL: critic round 3 (own round 10, commit 82d96458)
The best average (82.6). The Caravel's block restored to 82d96458's
(byte-identical; no other unit touched, block check one per unit). The
re-render matches critic 3's comparison sheets: final.png and final_red.png
both mean abs diff 0.000002, max 2/255, no pixel over 2/255. Tint per
direction (columns 1-8): 15.7 / 23.3 / 6.0 / 8.0 / 10.8 / 12.7 / 4.0 /
10.5 % (mean 11.4). ppm 47.8. final.png / final_red.png = critic 3's (rows:
shipped, final, Galleon, Dromon), in ArtGen/units3d/reviews/realism/caravel/
and the handoff folder. (The C4/C5 code is kept in the scratchpad.)

Critic history (average; continuity / fresh):
- C1 54.0 (A 51 / B 57; critical: out of house style, a bare open boat)
- C2 74.6 (79.5 / 69.75)
- C3 82.6 (84 / 81.25) = FINAL
- C4 79.75 (86.5 / 73)
- C5 79.25 (86.5 / 72); plateau

Open points: the main course's cross not whole in direction 3 (the
braced sail foreshortened); the sails have no seams or bolt-ropes (C4/C5
tried belly and bracing, which the fresh critics read as crumpled paper);
the stern detail is thin (the C4/C5 balustraded castles, walnut sides, the
civ waist stripe and the bigger rudder are not in the final).
