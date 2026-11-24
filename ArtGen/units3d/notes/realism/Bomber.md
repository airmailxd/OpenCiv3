# Bomber (realism pass): design

A Boeing B-17G Flying Fortress (1944): the long fuselage with the glazed
nose and the chin turret, cockpit glazing, top, ball and tail guns, waist
windows; four radials in their nacelles with three-blade Hamilton props
and turbochargers; the huge fin with its dorsal fillet. Real: span 31.6 m,
length 22.7 m (model 33.6 / 23.8). Olive drab over neutral grey with a soft
sun fade (no camo), black de-icer boots along the wings' leading edges,
white code letters. Neighbours: the stock Tank and Helicopter.

## Baseline (as shipped)
Two-tone olive/tan noise mottling (reads as camo the B-17 never wore), the
whole tailplanes in civ colour, small blocky civ squares on the wings, civ
cheek stripes. Tint 7.8-15.3 % (mean 11.5).

## What must read at 1x
1. Four engines with their props, the long straight wing, the big fin.
2. Civ colour in three places: white-ringed civ roundels on the upper
   wings (where the star goes), the fin's upper band (a bomb group's tail
   colour) and the tailplane tips. Aim 9-12 %.
3. The glazed nose, the dark de-icer boots, olive drab over grey.

## Build rounds (own, before batch critic round 1)
- R1: olive drab with a soft fade instead of the olive/tan mottling;
  white-ringed civ roundels, the fin band, tailplane tips.
- R2: wing, tailplane and fin faces resampled (round roundels, clean band
  edges); civ trimmed to 10 % (tailplane tips from 3.0 m out, roundel r 1.1).
- R3: black de-icer boots on the wings' leading edges. BUG found by
  specks.py (the props were 90 px detached pieces): the four nacelles were
  lofted at x = 0, inside the fuselage, so only the cowling rings and
  props hung in front of the bare wing. body() takes an x now (default 0:
  unchanged for every other caller); the nacelles sit on the wing. Blades
  0.26 r wide (they broke up at 2x). specks.py clean.
Batch 1 sheet: tint 11.3 / 9.7 / 11.1 / 9.4 / 11.7 / 10.7 / 11.6 / 9.4 %
(mean 10.6; shipped 11.5). Footprint 104.2 % (the nacelles add area).

## Batch critic round 1: 75.25 (A 77.0 / B 73.5)
Fixes asked: fin and tailplanes olive drab with a civ band only; the
dorsal fillet, chin turret, cheek guns, ball turret; cowled radials ahead
of the leading edge, a broader tapered root, panel lines.

## Batch round 2 build
- Civ: white-ringed roundels (r 1.3, ring 0.34) and the fin's upper band
  (from z 3.6); the tailplanes olive drab.
- A separate dorsal fillet body running from the wing root up into the
  fin; chin turret r 0.5, ball turret r 0.72 with its sighting window,
  cheek-gun blisters with guns, thicker guns (r 0.045).
- Wing root broadened (LE 5.2, chord 7.2 m) and tapered to 2.1 m; deep
  cowlings 1.5 m ahead of the leading edge with a dark cylinder ring.
- Panel lines (#46492e): wing ribs, the aileron hinge, fuselage frames.
Tint 5.5 / 3.2 / 5.5 / 6.8 / 6.0 / 3.9 / 6.0 / 6.9 % (mean 5.5; the band
is edge-on from above in 2 and 6). Footprint: box 100 / 100 %, area 113 %
(the broader wing and the nacelles). specks.py clean.

## Batch critic round 2: 82.1 (A 85.25 PASS / B 79.0) -> solo round (review/bomber/solo1)
## Solo round 1 build
- Neutral grey (#86888a) up the fuselage's lower sides (faces facing
  down past -0.1), the turrets' housings in it.
- Chin turret r 0.75, dark, moved forward under the bombardier's glazing
  (y 11.1) on a grey mount so it shows in 2 and 6; ball turret r 0.82
  with its sighting window; top turret r 0.62 with a dark frame ring.
- Tonal panels: the wings' leading-edge bays (between the ribs) and
  alternate fuselage bays in a lighter olive (#63663f, key 'air_panel').
- Exhaust staining ('air_soot' #2c2a26) on the nacelles' rear flanks and
  bellies and a streak along their tops.
- Wing faces finer (step 0.15, 36 chord segments): the roundels' edges no
  longer rag in 4 and 8.
Tint 5.6 / 3.3 / 5.6 / 6.9 / 6.2 / 4.3 / 6.2 / 7.0 % (mean 5.6). Footprint:
box 100.8 / 100.6 %, area 114 %. fitcheck: bbox-area 1.01 (min 1.00 dir 6)
ok. specks.py clean.

## Solo round 1 critics: 85.1 (continuity 86.25 PASS / fresh 84.0)

## FINAL: solo round 1 (commit 4664568b), PASS
reviews/realism/bomber/final.png and final_red.png are the solo-round
sheets (rows: shipped, final, stock Tank, stock Helicopter); the render at
HEAD matches them exactly (verify.sh: mean 0, max 0). On the air gate.

Critic history (average; continuity / fresh):
- Batch 1: 75.25 (77.0 / 73.5)
- Batch 2: 82.1 (85.25 / 79.0)
- Solo 1: 85.1 (86.25 / 84.0) = FINAL, PASS

Later pass: shrink or darken the white fuselage code letters (4, 8); more
panel lines and darker control surfaces; a US star-and-bar instead of the
RAF-like round roundels.
