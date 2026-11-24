# Wildcat (NumPy remake, realism pass): design

A Grumman F4F-4 Wildcat (1942). Real: span 11.58 m, length 8.76 m. The
features that must read at 1x: the stubby barrel fuselage behind the big
round cowling of the twin-row Wasp, the mid-set wing with its squared
tips, the tall rounded fin. Then the framed canopy over the faired turtle
deck, the wheels in their wells in the fuselage flanks, the four .50s in
each leading edge, the three-blade Curtiss prop. Built as a 1/48
miniature in the 1942 US Navy scheme: blue-grey over light grey.

Identity from the NumPy unit (art-base, model wildcat): "a stubby
blue-grey carrier fighter: a big round cowling with a civ-coloured ring,
civ-coloured roundels and a band round the tail"; installed tint 2.3-4.3 %
(mean 3.2). Its style is not copied.

It must not read as the Fighter (a Spitfire): blue-grey not RAF camo, a
barrel body and a big blunt cowl instead of a long pointed nose, square
wingtips instead of the ellipse.

Neighbours on the critic sheet: the closed Fighter and Bomber finals.

## Civ colour
White-ringed civ roundels on the upper wings and the fuselage sides, the
cowling's front band. Aim 5-8 %.

## Build rounds (own)
- R1: first build. Fit 1.05 bbox-area, pixels 0.96, specks clean, tint
  4.7 %. Too pale (read light grey, not blue-grey), the body slim, the
  cowl's civ ring only on its front face (invisible from above), the side
  roundel stair-stepped.
- R2: darker blue-grey (#4c5a69 / camo #586676), a wider barrel body, the
  civ band round the cowling's front 0.42 m (reads from above), finer
  fuselage faces (step 0.12) and a bigger side roundel (0.42). Tint 6.8 %,
  fit 1.05 / pixels 0.96, specks clean.
- R3 (GPU at b17eaf08, unchanged from R2): tint 6.6 / 4.8 / 6.3 / 7.7 /
  7.7 / 6.3 / 7.6 / 7.7 % (mean 6.8; shipped 3.2), fitcheck bbox-area 1.05
  (min 0.91 dir 2), pixels 0.96, specks clean.

## Batch critic round 1 (review/_batch/batch_numpyair_1.png, _red.png)
Rows: shipped NumPy, new, for both units; the closed Fighter and Bomber
finals at the bottom.

## Batch critic round 1: 76.1 (A 73 / B 79.25), no critical failure
Asked: the silhouette (A: barely told from the stock Fighter at 1x) with a
15-20 % fatter cowl and the barrel width kept back to the cockpit; the real
two-tone (dark blue-grey over light grey), panel lines, canopy framing, the
dark cowl-flap band, the belly rise ahead of the wing; the bright jagged
line on the trailing edges and the lower fuselage removed; civ colour on
the cowl lip only, the dark engine face behind it.

## Build round 2 (batch 2)
- Cowl r 0.86 (was 0.72), the fuselage 0.76-0.8 m half-width back to the
  cockpit, a deeper belly ahead of the wing (centre line down 0.1).
- Paint #43505f over #a2a8ac; the light grey below a straight demarcation
  (z < -0.18) on the fuselage instead of the faceted normal test (that was
  the jagged white line); the trailing-edge bands stay top colour (the
  grey underside showed as a bright edge line); spar and panel-joint
  lines on the wings; darker lines (#262e36).
- Canopy: 5 hoops and a spine frame (r 0.035). The dark cowl-flap band at
  the cowl's back. The civ colour only on a lip ring (a torus, r 0.085);
  behind it the dark engine face, the cylinder ring and the gearbox.
- Props: blades 0.3 r wide, pitch 45, 0.12 m thick, tips inside the blade.
- The side roundel lower and smaller (z 0.1, r 0.38): its ring no longer
  wraps over the top as a white X.
- GPU at 007ad3d7: tint 4.6 / 5.0 / 4.6 / 5.6 / 5.0 / 4.8 / 5.0 / 5.5 %
  (mean 5.0; shipped 3.2), fitcheck bbox-area 1.05 (min 0.93 dir 2),
  pixels 1.00, specks clean. Sheet review/_batch/batch_numpyair_2.png.
  Open (own look): in the side views the light-grey belly behind the wing
  shows as a thin straight light band (now the intended demarcation, no
  longer jagged).

## Batch critic round 2: 82.75 (A 83 / B 82.5), no critical failure
Asked: darker upper surfaces (about 20 %) toward Navy blue-grey; a dark
glazing band with a bright frame line in dirs 4 and 8; the light sliver at
the wing-belly join near the tail (SW/NE).

## Build round 3 (batch 3, the last under the cap)
- Upper paint #36404c (20 % darker), camo #434e5b.
- Canopy a little bigger (0.38 x 0.88 x 0.42, z 0.7), 4 hoops and a spine
  in a bright frame colour ('frame_light' #a8acae, r 0.04) over the dark
  glass.
- The belly demarcation drops 0.3 m behind the wing (y < -0.6), so the
  narrowing rear belly stays top colour: no sliver.
- Size (the lead's new rule): fit_scale 1.13 so it matches the stock
  Fighter.
- GPU at dbfdf9cc (batch_numpyair_3): tint 4.9 / 5.2 / 4.7 / 5.7 / 5.1 /
  4.6 / 5.0 / 5.7 % (mean 5.1; shipped 3.2); specks clean. Size: against
  the stock Fighter pixels 0.99, bbox-area 0.92; against the NumPy sprite
  pixels 1.27, bbox-area 1.35.

## Batch critic round 3: 86.1 (A 86.0 / B 86.25), PASS, no critical failure

## FINAL: batch round 3 (art at dbfdf9cc)
reviews/realism/wildcat/final.png and final_red.png (also review/wildcat/
on the handoff share): rows shipped (the NumPy sprite), final, stock
Fighter, stock Bomber. On the numpy-air gate (8 units).

Critic history (average; A / B):
- Batch 1: 76.1 (73 / 79.25)
- Batch 2: 82.75 (83 / 82.5)
- Batch 3: 86.1 (86.0 / 86.25) = FINAL, PASS

Optional points for a later pass: dull the canopy frame to a light grey
(it reads zebra-striped at 2x); remove the faint light edge at the
wing-tail join (SW/NE).
