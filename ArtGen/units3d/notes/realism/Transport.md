# Transport (realism pass): design

The Transport is a Landing Ship, Tank (LST-1 class, 1943): ~100 x 15 m
(L:B ~6.6), a long, low, flat-sided grey hull with a red boot topping and
clamshell bow doors, the weather deck loaded with olive-drab trucks,
tanks and a jeep, LCVPs in davits along the sides, the bridge and twin
stacks aft, a pole mast. Neighbours (stock): Ironclad and Destroyer.

## Baseline (as shipped)
Tint (tint.py, DEFAULT): 7.3 / 10.1 / 7.4 / 5.5 / 5.5 / 3.4 / 5.3 / 5.4 %
(mean 6.2). Values: mean 119, p10 51, std 58, upper 143 / lower 114.
Footprint 2742 px.

## What must read at 1x
The long low LST silhouette with the blunt bow doors (bow) and the
deckhouse with twin stacks (stern); the olive vehicle load (what it
carries); the boats in davits; plate courses and a dark sheer strake so
the grey hull isn't one flat plane. No thin rails or rods.

## Civ colour (stock 6.2 %)
A band on the bridge front and sides, the stack tops, an air-recognition
panel on the deck, the ensign.

## Rounds
New builder ships.transport_lst in its own block (before _steel_builder,
placed by the generic nav_splice_steel script); BUILDERS['Transport']
set after the dict. No other unit changed (block check: one per unit).
- Round 1: the LST. The LCVPs hung clear of the hull (separate pieces in
  six directions); footprint 2414; tint 2.7 %.
- Round 2: the boats snug to the side on longer davits (one piece);
  fit_scale 1.1 (footprint 2930); the paint lighter. The superstructure
  read near-white, the hull one flat light band.
- Round 3: paint darker (hull #70746a, super #8a8e88), a dark sheer
  strake; the deck panel 0.24 x 0.2, the bridge band deeper, the stack
  tops civ (tint 4.5 %).
- Round 4 (CPU draft): the greys less metallic (they mirrored the sky).
- Round 5: plate courses stronger, the lower strake darker.
  Sent to critic round 1. Tint per direction (columns 1-8): 4.7 / 6.2 /
  4.3 / 5.0 / 4.6 / 2.2 / 4.1 / 5.0 % (mean 4.5). Values: mean 91, p10 42,
  p50 92, p90 133, p99 188, std 37, upper 98 / lower 90 (stock 119, 143 /
  114). ppm 64.58; footprint 2926 (stock 2742, +7 %). specks.py: clean.

## Critic round 1 (batch naval 1): 59.9 (A 63.25, fresh 56.5)
Too long and thin for an LST ("a thin pale strip", the odd one out):
shorter, more beam, footprint near the stock's, plating shading, a darker
waterline band, a darker grey; the clamshell doors legible with a split
seam; the stacks readable; the LCVPs in dark outlines; chunkier vehicles;
the loose blue deck square's colour onto the bridge band and stack tops.

- Round 6 (CPU draft): L 2.4 -> 2.0, B 0.46, D 0.14; greys darker (hull
  #62665e, deck #46494a, super #80847e); two lighter clamshell door
  panels split by a dark seam; stacks r 0.046 x 0.16 with civ tops and dark
  caps; LCVPs with dark hull outlines and gunwales; the vehicles 1.35x
  (two tanks, two trucks); the deck panel gone, the bridge band deeper.
  Footprint +25 %.
- Rounds 7-8 (GPU): fit_scale 0.9 overshot (2333), 0.98. Sent to the
  batch round (batch_naval_2; the unit's own sheet review/transport/batch2).
  Tint per direction (columns 1-8): 4.5 / 4.9 / 4.1 / 3.8 / 3.5 / 1.9 /
  3.1 / 3.8 % (mean 3.7). Values: mean 82, p10 39, p50 85, p90 118, p99
  162, std 31, upper 87 / lower 81 (stock 119, 143 / 114). ppm 62.64;
  footprint 2743 (stock 2742). specks.py: clean.

## Critic round 2 (batch naval 2): 63.0 (A 68, fresh 58): gain (+3.1)
It reads as a tray or barge (a flat slab with a sunken deck). Fixes: a
tall, flat-sided LST hull; a high, bluff, rounded bow with curved door
leaves and a hinge seam; weathered, darker sides with plate seams, a real
waterline with foam; the vehicles spaced, outlined, lighter tops; bigger
landing craft hanging over the side; a taller bridge, larger stacks; civ
colour up (3.7 % vs 6.2 %).

- Round 9: D 0.14 -> 0.22, the bow rounder (bow_p 2.2, bow_transom 0.55)
  with hinge seams at the leaves' outer edges; hull wear up; a foam line
  at the waterline; four vehicles spaced on dark outline pads, the olive
  a step lighter (#6e7044); LCVPs 0.085 x 0.22 hanging 0.05 out in the
  davits, dark outlines and gunwales; bridge 0.2 tall, pilot house 0.1;
  stacks r 0.056 x 0.2 with deeper civ tops. Footprint 2952 (+8 %).
- Round 10: fit_scale 0.95. Sent to critic round 3 (the last;
  review/transport/critic3). Tint per direction (columns 1-8): 5.9 / 6.7 /
  5.5 / 4.1 / 4.3 / 2.5 / 3.8 / 4.0 % (mean 4.6). Values: mean 86, p10 49,
  p50 88, p90 122, p99 154, std 28, upper 89 / lower 85 (stock 119, 143 /
  114). ppm 57.55; footprint 2774 (stock 2742). specks.py: clean.

## Critic round 3: 64.5 (A 71.5, fresh 57.5 with a CRITICAL)
The landing craft read as floating (no davit arms; in 4/8/6 beyond the
hull with nothing holding them; the continuity critic saw it in 1/3), and
the identity unclear (a blunt box bow, no visible doors). By the rule, one
more round aimed only at the critical.

- Round 11 (C4, targeted): each LCVP tucked against the hull side and held
  by two dark davits rooted at the deck edge (a post, an arm out over the
  boat, a fall down to it); the boats a lighter grey with a dark well; the
  bow rounder in plan (bow_p 1.8, bow_transom 0.35) with two curved,
  shaded clamshell leaves, a dark recessed centre seam and hinge seams.
  The lathe boat hulls rendered as pale balls.
- Round 12: the boats rounded boxes (0.082 x 0.22 x 0.05) at deck level.
  Sent to critic round 4 (review/transport/critic4). Tint per direction
  (columns 1-8): 6.1 / 6.9 / 5.8 / 4.3 / 4.5 / 2.6 / 3.9 / 4.1 % (mean
  4.8). Values: mean 87, upper 90 / lower 87. ppm 58.47; footprint 2769
  (stock 2742). specks.py: clean.

## Critic round 4 (targeted): 68.0 (continuity 73.25: "landing craft now attached, no critical"; fresh 62.75: floating cleared, identity at 1x still unclear)

## FINAL: critic round 4 (own round 12, commit 2ca31c0c); STUCK on identity at 1x (one critic)
The current code; the reproduction is checked in the finals gate (all
twelve naval finals). Finals in ArtGen/units3d/reviews/realism/transport/
and the handoff folder (rows: shipped, final, Ironclad, Destroyer).

Critic history (average; A-continuity / fresh):
- C1 59.9 (63.25 / 56.5)
- C2 63.0 (68 / 58)
- C3 64.5 (71.5 / 57.5; critical: floating landing craft, unclear identity)
- C4 68.0 (73.25 / 62.75; targeted round, floating cleared) = FINAL

Stuck: the fresh critic still can't identify it at 1x. Open points: the
bow-door seam and leaves too faint at 1x; the davits reading as spikes;
the tanks a tan smear; the hull low and flat grey; no foam (the waterline
foam line doesn't read).
