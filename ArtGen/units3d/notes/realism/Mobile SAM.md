# Mobile SAM (realism pass): design

The Mobile SAM is an M48 Chaparral (1970s-90s): the M730 tracked carrier
(an M548 derivative: a low hull with the crew cab forward) carrying the
M54 launch station at the rear: a turntable, the gunner's enclosed cab in
the middle and two arms with two MIM-72 missiles each (four in all), the
missiles long and thin with canards and tail fins. Built as a painted
1/56 tabletop miniature in the house sand with its own camo. Neighbours
for the comparison: Mech Infantry (the new final) and Radar Artillery
(stock: the other missile carrier).

## Baseline (round 0, as shipped)
- An olive-drab M113-type box with a big launcher of four white missiles
  with red noses and civ fins; strong at 1x by its missiles, but olive is
  out of the house palette (the Tank's critical failure), the white
  missiles are the brightest thing on the sprite, and the hull is a
  generic box.
- Tint 3.2-6.4 % (mean 5.6), mostly on the missile fins and side panels.
- Values (values.py, 1x): mean 96, p10 27, std 62, upper 135 / lower 72
  (the missiles light, the olive hull dark).

## Real proportions (M48A1/M730)
6.06 x 2.69 m, 2.68 m to the missiles' top; the cab ~1.4 m long at the
front, the hull deck low (~1.3 m), the launch station on the rear deck,
missiles 2.9 m long, 0.13 m across. The model at ~3.5 m/unit: L 1.74, W
0.9 (widened as the roster's vehicles are), missiles 0.82 long.

## What must read at 1x
1. Four long missiles on two arms either side of the gunner's cab, raised
   ~15 deg, light grey (not white), dark seeker noses, fins at both ends.
2. The low carrier: a cab forward with its windscreen, a flat cargo deck
   behind it with the launch station's turntable.
3. Running gear: five road wheels, front sprocket, rear idler, dark track;
   the lower hull darker than the lit deck and cab roof.

## Paint, values and civ colour
- House sand with its own camo: hard-edged dark olive (#5e5e44) and field
  drab (#7a6a4e) bands. Values aim: the stock's split (upper clearly
  lighter: the missiles and the cab roof; the lower hull and running gear
  dark), std >= 55.
- Civ (stock Mobile SAM 3.2-6.4 %, Mech Infantry final 2.2-3.8 %): inverted
  -V chevrons on the cab sides and the launch station's cab sides, one on
  the rear; a civ band on each missile's body (small, decal-like). Aim
  3-5 %.

## Rounds
Tint per direction (compare columns 1-8); values from values.py.

- Round 1 (14 s, waited 165 s): the new model (a rewritten _sam). Reads as
  a Chaparral at 2x: the cab forward, the turntable, the gunner's cab and
  four light-grey missiles with dark noses. Cab tall and bus-like; civ
  marks small: tint 0.0-1.2 % (none head-on). Values: mean 112, p10 31,
  std 67, upper 136 / lower 92 (the split the stock has).
- Round 2 (14 s): cab lower (0.26 -> 0.2), bigger chevrons on the cab and
  the gunner's cab, wider civ bands on the missiles, a chevron on the cab
  front. Tint 0.6-2.3 %.
- Round 3 (14 s): two chevrons on the cab front, missile bands 0.1. Sent
  to critic round 1 (neighbours: Mech Infantry final, Radar Artillery
  stock). Tint: 3.0 / 2.3 / 3.0 / 2.6 / 2.5 / 1.7 / 2.5 / 2.6 % (mean
  2.5). Values: mean 113, p10 31, p50 101, p90 216, std 67, upper 135 /
  lower 94. ppm 64.69.
No shared code changed (the SAM's builder and spec only).

## Critic round 1: 62.25 (continuity 59, fresh 65.5), FAIL (near "slop")
Both: the cab a featureless camo crate, the hull one flat slab, the
launch station a plain cube with the missiles merging into a bundle.
Fixes (the builder rewritten):
1. Cab built into the hull's front: lower (0.18), a sloped front (0.4)
   with a framed two-pane windscreen and a centre pillar, two dark side
   windows a side, a door seam, a lit roof lip with a hatch ring, mirrors
   on arms, headlights in guards on the nose.
2. Hull: the engine grille and an exhaust stack behind the cab, a lit
   fender lip with a dark shadow under it the full length, the vertical
   hull sides in the darker second paint, stowage bins on the rear deck,
   a framed rear door, tow hooks front and rear, dark (gunmetal) hubs.
3. Launch station: a dark recess with a lit bearing ring, a pedestal,
   the turret block with a dark-glazed gunner's canopy on top, dark
   trunnions out to two well-separated arms, longer slimmer missiles
   (0.95 x r 0.034) on rail shoes, fins with dark tips, raised 25 deg.
   Rear: two chevrons.

- Round 4 (14 s): all in. Tint 1.3-2.6 %.
- Round 5 (14 s): bigger chevrons (cab 0.17, station 0.14, rear 0.11),
  missile bands 0.13. Sent to critic round 2. Tint per direction
  (columns 1-8): 2.7 / 1.3 / 2.7 / 2.6 / 3.5 / 2.7 / 3.5 / 2.6 % (mean
  2.7). Values: mean 110, p10 30, std 67, upper 132 / lower 91. ppm
  65.79. No shared code changed.

## Critic round 2: 68.1 (continuity 66.25, fresh 70), no critical failure
1. Cab ~40 % lower (0.18 -> 0.11), its front sloped further (0.5), the
   hatch on the roof.
2. Missiles thicker (r 0.034 -> 0.04) and lighter (#d4d0c2, still short
   of white), the pairs further apart (+-0.09) and the arms further out
   (0.19); fins and noses scale with the radius.
3. Pose: the missiles sat centred on the trunnions, so half their length
   trailed behind the station and from several views they read as
   pointing back. Now pushed 0.22 forward along their rails and raised to
   35 deg: the engage pose, forward over the cab.
   Running gear: less pale dust on the wheels and tyres (dark hubs show);
   the pedestal with a lit band at the top and a dark one at its foot
   (turned metal); front chevrons 0.13.

- Round 6 (15 s): all in. Sent to critic round 3. Tint per direction
  (columns 1-8): 3.4 / 2.3 / 3.4 / 3.3 / 3.8 / 2.9 / 3.9 / 3.4 % (mean
  3.3). Values: mean 112, p10 29, std 68, upper 135 / lower 99. ppm 61.79
  (the forward missiles widen the fit's box). No shared code changed.

## Critic round 3: 78.4 (continuity 79.25, fresh 77.5), no critical failure
CHECK (the launcher's bearing across facings): the model is one rigid
object turned for each direction and both arms share one pivot rotation
(parallel, no toe-in); what read as "rearward" in 4/8 and a V in 5/7 is
foreshortening of the long missiles, not a change of pose.
1. Head-on: the arms further out (0.24) and higher (42 deg): all four
   missiles clear the cab and the drum from the front and the rear.
2. Hull: vertical panel seams on the hull sides, a lit deck edge, three
   boxes along each fender, two framed hatches on the rear deck;
   elevation rams from the turret block to the arms.
3. Cab: its roof in the darker paint, a dark lip round it (it was a lit
   frame: a "placard"), one dark window strip along each side.

- Round 7 (15 s): 1-2 and the cab roof/strip. Tint 2.2-4.3 %.
- Round 8 (15 s): the cab's lip dark. Sent to critic round 4. Tint per
  direction (columns 1-8): 3.3 / 2.2 / 3.3 / 3.5 / 4.2 / 3.3 / 4.3 / 3.5 %
  (mean 3.5). Values: mean 106, p10 30, std 62, upper 125 / lower 95.
  ppm 60.34. No shared code changed.

## Critic round 4: 77.6 (continuity 81.75, fresh 73.5): no gain
(The fresh critic's "critical" mirrored launcher was checked on the sheet
by the lead and in the code: consistent; the real issue is that the front
doesn't read.)
1. Front identity: the cab roof a darker mid brown ('skirt_dirt'), its
   hatch dark, one dark windscreen band across the sloped front; the
   rear keeps the grille, the exhaust and the framed door.
2. Size: the arms, rams and missiles on their own part, out of the fit
   (as the guns): ppm 60.34 -> 66.23 (Mech Infantry 67.85).
3. End-on: longer dark noses (0.13), the arms toed out 5 deg so each pair
   splits; raised to 46 deg and moved back along the rails (fwd 0.15) so
   they clear the cab in 4/5.
   Side seams: each a dark line with a light bevel beside it.

- Round 9 (15 s): all in. 4 against 8 side by side: the cab end (dark
  brown roof, dark windscreen strip, chevron) and the launcher end read
  apart. Sent to critic round 5. Tint per direction (columns 1-8): 3.5 /
  2.7 / 3.4 / 3.4 / 4.4 / 3.5 / 4.4 / 3.4 % (mean 3.6). Values: mean 110,
  p10 30, std 66, upper 131 / lower 98. No shared code changed.

## Critic round 5: 78.0 (continuity 82.25, fresh 73.75) -> plateau

## FINAL: critic round 3 (own round 6, commit dcfe28a6)
Two critic rounds (4, 5) without beating round 3 by 1.5: plateau.
armor.py is restored to dcfe28a6 (the later code was the SAM's only);
the re-render matches critic 3's sheets (mean abs diff 0.000003, max
2/255). final.png / final_red.png are critic 3's (rows: shipped, final,
Mech Infantry final, Radar Artillery stock). Tint per direction (columns
1-8): 3.4 / 2.3 / 3.4 / 3.3 / 3.8 / 2.9 / 3.9 / 3.4 % (mean 3.3). ppm
61.79.

Critic history (average; continuity / fresh):
- C1 62.25 (59 / 65.5)
- C2 68.1 (66.25 / 70)
- C3 78.4 (79.25 / 77.5) = FINAL
- C4 77.6 (81.75 / 73.5)
- C5 78.0 (82.25 / 73.75); plateau

The alternative: critic 5 (commit 556e53c7), 0.4 lower, fixes the small
draw size (the launcher out of the fit: ppm 66.2 against 61.8, matching
the Mech Infantry) and makes the front readable (a darker cab roof, a
dark windscreen band, toed-out arms that split head-on, hull-side detail).
The art lead may prefer it for roster consistency.

Open points for a later pass: the cab as a distinct M548-style cab with
camo on its roof (not a pale panel); the end-on missiles in direction 2;
the camo contrast; the empty mid-deck; a plain launch-station drum.

## FINAL changed by the art lead: critic round 5 (own round 9, commit 556e53c7)
The lead's decision: the final is critic 5 (78.0), not the score-best
critic 3 (78.4): 0.4 is within the critics' noise, and the correct draw
size (the launcher out of the fit: ppm 66.23 against 61.79, matching the
Mech Infantry) and the readable front matter more for the roster. The
SAM's builder and spec are restored from 556e53c7 into the current
armor.py (the Radar Artillery's code untouched); the re-render matches
critic 5's sheets (mean abs diff 0.000002, max 1/255). final.png /
final_red.png are now critic 5's. Tint per direction (columns 1-8): 3.5 /
2.7 / 3.4 / 3.4 / 4.4 / 3.5 / 4.4 / 3.4 % (mean 3.6).

## Correctness fix after the final: detached mirrors (specks.py)
specks.py found a 17-21 px piece in directions 1/3/4/8: each was a cab
mirror, its 6 mm arm too thin to render, so the mirror floated. Removing
the mirrors changed the fit (they count in its box: ppm 66.23 -> 66.90,
the whole sheet rescaled), so the arms are thickened instead (16 mm,
'mirror_r'): the mirror now hangs on a visible arm and the fit is
unchanged (ppm 66.23). specks.py: clean. Against the recorded final
(DEFAULT, 2x): 925 px changed by more than 2/255 (165 by more than 24),
all at the two mirrors and their faint ground shadows (per direction
89-154 px; the bounding boxes span both mirrors of a direction, e.g. dir 8
x 36-53 y 38-107); the tint layer 1-32 px (bounces near the cab). No
other change; it stays the final. final.png / final_red.png and the
branch copies re-made. Tint per direction (columns 1-8): 3.4 / 2.7 / 3.4 /
3.4 / 4.4 / 3.5 / 4.4 / 3.4 % (mean 3.6).
