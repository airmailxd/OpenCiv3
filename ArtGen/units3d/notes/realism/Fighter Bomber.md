# Fighter Bomber (NumPy remake, realism pass): design

A Lockheed P-38J Lightning carrying bombs. Real: span 15.85 m, length
11.53 m, booms 4.9 m apart. The features that must read at 1x: the twin
booms with the central gondola between them, the twin oval fins joined by
the tailplane, the two counter-rotating three-blade props. Then the bubble
canopy and the nose gun cluster, the chin radiators ('beard') under the
spinners, the coolant scoops on the boom flanks, the turbo-superchargers
on top of the booms, two 1,000 lb GP bombs on the centre-section pylons.
Built as a 1/48 miniature in USAAF olive drab over neutral grey (the stock
Bomber's paint, so the two read as one air force).

Identity from the NumPy unit (art-base, model p38): an olive-drab P-38 with
civ dots on the wings and civ spinners; installed tint 5.0-5.2 % (mean
5.1). Its style is not copied.

Neighbours on the critic sheet: the closed Fighter and Bomber finals.

## Civ colour
Three deliberate places: white-ringed civ roundels on the outer upper
wings, a painted band round each boom behind the radiators, the fins'
tops; plus the spinners. Air class (installed, tint.py): Fighter final
8.4, Bomber 11.5 shipped; NumPy shipped 5.1. Aim 5-8 %.

## Build rounds (own)
- R1: first build. Fit 1.02 bbox-area, pixels 1.00, specks clean, tint
  3.5 %. The boom bands sat on the thin tail end and read as dots; the
  bombs are hidden under the wing from above.
- R2: band moved forward to the thicker boom (still a dot at 1x), civ fin
  tops (read well in every direction), bigger spinners (hub 0.3). Tint
  6.1 %, fit 1.01 / pixels 1.00.
- R3: the boom band off (option band_w, default 0): roundels, fin tops,
  spinners.
  GPU at b17eaf08: tint 5.8 / 3.6 / 5.8 / 6.4 / 5.1 / 2.9 / 5.3 / 6.3 %
  (mean 5.1; shipped 5.1), fitcheck bbox-area 1.01 (min 0.94 dir 6),
  pixels 1.00, specks clean.

## Batch critic round 1 (review/_batch/batch_numpyair_1.png, _red.png)
Rows: shipped NumPy, new, for both units; the closed Fighter and Bomber
finals at the bottom.

## Batch critic round 1: 76.9 (A 77 / B 76.75), no critical failure
Asked: visible bombs; darker paint (the olive read as grey-khaki mush, the
booms dissolved into the grass), a lighter top edge, dark nacelle bands,
radiator scoops, turbos and canopy framing; the dark nose-gun cluster
(dirs 2, 6); thicker props (lost in 4, 8); taller oval fins with a thin
dark edge (the civ caps read as blobs in E/W); bigger, toward fitcheck
1.08-1.10.

## Build round 2 (batch 2)
- Bombs 2.0 x 0.3 m, near-black olive, hung low (z -1.0) and forward
  (the noses ahead of the leading edge) on dark pylons: dark lumps at 1x.
- Paint #484b2e over neutral grey #9a9c9c; under-grey below a straight
  demarcation on the booms and gondola; a lighter olive top stripe
  (#666a42) on the booms and gondola; sooty nacelle bands behind the
  props; dark radiator scoops; bigger turbo bumps (r 0.3); the canopy with
  4 hoops and a spine frame (r 0.035); the gondola's nose dark, the guns
  r 0.07; darker panel lines (#2c2e1e).
- Props: blades 0.3 r wide, pitch 45, 0.12 m thick, the yellow tips pulled
  inside the blade (a 1 px tip speck).
- Fins: oval, 2.35 m tall, a dark edge tube; civ caps above 1.55 m.
- fit_scale 1.03 (shared option on the AIRCRAFT entry).
- GPU at 007ad3d7: tint 5.2 / 3.7 / 5.3 / 6.2 / 5.1 / 2.7 / 5.3 / 6.2 %
  (mean 5.0; shipped 5.1), fitcheck bbox-area 1.08 (min 1.00 dir 6),
  pixels 1.09, specks clean. Sheet review/_batch/batch_numpyair_2.png.

## Batch critic round 2: 82.0 (A 81 / B 83), no critical failure
Asked: bombs still not picked out at 1x (A: none in the diagonals); the
fins' civ caps still read as blobs in E/W; the boom bands and scoops broke
each boom into dashes in the diagonals; size (B wants 10-12 % over the
stock Fighter; left at 1.09 under the lead's 1.10 cap, noted for the lead).

## Build round 3 (batch 3, the last under the cap)
- Found: round 2's edit had put the round-1 boom band (band_w, meant off)
  inside the belly block, so the civ boom bands were on in batch 2 (the
  "dashes"). Back under band_w (default 0).
- Booms: one calm darker olive (#3c3f27, 'air_panel') over each boom,
  the chin intakes and radiator scoops in the same tone; the nacelle soot
  bands and the light top stripe off (nacelle_band, top_stripe default
  False).
- Fins: olive; civ only on a thin band down each rudder's trailing part
  (rudder_band y < -7.62, z > 0.35); fin_tint (the caps) off.
- Bigger wing roundels (r 0.74, ring 0.22).
- Bombs 2.8 m x 0.31, near-black satin olive (#2a2c20, rough 0.25: a light
  sheen edge) with a yellow nose band, hung lower (z -1.3) and further
  forward (the noses 1.85 m ahead of the leading edge), mid-way between the
  gondola and the booms (x 1.25), clear of the prop discs.
- Size (the lead's new rule: against the stock class, not the NumPy
  sprite): fit_scale 1.094, blades 0.34 r / 0.14 m (a 1 px blade speck
  at the bigger size).
- GPU at dbfdf9cc (batch_numpyair_3): tint 4.1 / 4.8 / 4.2 / 4.5 / 4.3 /
  4.2 / 4.3 / 4.6 % (mean 4.4; shipped 5.1); specks clean. Size: against
  the stock Fighter pixels 1.15, bbox-area 1.10; against the stock Bomber
  pixels 0.50 (under it); against the NumPy sprite pixels 1.24, bbox-area
  1.23.

## Batch critic round 3: 85.4 (A 85.5 / B 85.25), PASS, no critical failure

## FINAL: batch round 3 (art at dbfdf9cc)
reviews/realism/fighter-bomber/final.png and final_red.png (also
review/fighter-bomber/ on the handoff share): rows shipped (the NumPy
sprite), final, stock Fighter, stock Bomber. On the numpy-air gate
(scratch/numpyair/verify/verify_all.sh, 8 units).

Critic history (average; A / B):
- Batch 1: 76.9 (77 / 76.75)
- Batch 2: 82.0 (81 / 83)
- Batch 3: 85.4 (85.5 / 85.25) = FINAL, PASS

Optional points for a later pass: bombs that read at 1x in the diagonals;
a wider head-on span in dirs 2 and 6, so it clearly exceeds the stock
Fighter's there.
