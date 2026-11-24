# Sipahi (realism pass)

## Design in words

An Ottoman timariot sipahi, 16th century: a light horseman in a mail-and-plate
krug shirt over a kaftan, a pointed chichak helmet with a small peak, a nasal
and a mail aventail at the sides and back only (the face open and lit), a
round kalkan shield of dark steel with a gold boss on the left arm, a curved
kilij raised in the right hand. Short Turkish stirrups, the knees up.

The horse: a fine dark bay Turkish horse with a steel chanfron, not a fully
barded one. Civ3's spotted civ caparison is the unit's identity, so the civ
stays on the horse but as a deliberate cut: a large civ saddle cloth (caprak)
from the withers over the croup, a broad gold border and a few large pale
crescent-like discs (no scatter of white dots: a noise pattern), a gold
breast strap with tassels. No cloth sleeve over the neck (it hid the horse),
no plume on the horse's head (it crossed the rider's face in S).

Civ in two or three deliberate regions: the saddle cloth body, the kaftan's
skirts below the armour, a plume on the helmet. Aim ~20-30 % (shipped 50 %).

Shipped: a blue box of a horse (full caparison, white dots, neck sleeve), the
rider all grey with the face behind a full mail curtain.

Contracts as for the family (sheet layout, flics, 2x cells, anchor/ppm, tint
alignment). New looks only behind Sipahi entries; shared code defaults keep
the closed finals (verify_all.sh).

## Rounds (own)

Render ~23 s a round; close-ups mnt_heads.py.

### Round 1
riders.spiked_helm open_face (the aventail round the sides and back only),
nasal, peak; a new 'sipahi' seat (the Rider's raised sword arm, a kalkan on the
forearm), the kilij up and back; the kaftan's civ skirts (COAT_SKIRT); a civ
caprak with a gold border and two pale discs a side (tack cloth_discs), a
steel chanfron, no neck sleeve, no horse plume. The helmet still hid the face.
### Round 2: the helmet 0.95 and lifted 0.14: the face lit; crimson kaftan sleeves, a darker krug.
### Round 3: the krug's plate rows ('rows', metal off), a darker chanfron.
### Rounds 4-7: specks: the reins off the hand; a blunt spike (spike_tip), a thicker plume (plume_r, plume_tip); a wider kilij (0.1, spine 0.042); no stirrups (a speck at the left stirrup). specks.py clean.
Tint per direction 12.1 6.5 15.5 19.5 18.8 15.5 18.5 18.7, mean 15.6 % (shipped 52 %).
### Round 8 (batch 1): the caprak larger (cloth_frac 1.1, back 0.72): tint 15.7 6.7 19.3 23.9 22.7 18.0 22.4 23.1, mean 19.0 % (stock 51.5); footprint 0.88x the stock's area (1.02x height). specks.py clean.

## Batch 1: A 72.5, B 71.25, average 71.9
Fixes: a curved, shaded kilij with a dark hilt (not a white stick); the white
chanfron slab gone or darkened; an embroidered, tasselled, draped caprak.
### Round 9 (batch 2)
The kilij curve 0.2, steel #a4a8b0, a dark grip; no chanfron, studs on the
breast strap; the caprak draped (tubes, folds 0.06) with a gold fringe and an
inner gold line; face_glow 0.25. Tint 19.0 % (stock 51.5); footprint 0.91x the
stock's area. specks.py clean.

## Batch 2: continuity 76.5, fresh 68.0, average 72.25 (no gain on 71.9)
At 1x a small bare horse beside the barded stock. Fixes: a full draped cloth
(~30 % civ), a bulkier rider with bright plates, a darker, curved blade, the
blade off the helmet in 4.
### Round 10-11 (batch 3)
The caprak croup to shoulders (cloth_back 0.95, a new tack cloth_front 0.6,
frac 1.25), rounder corners (0.26), deeper folds (0.09); bright krug plates
(#b4b8c0), weight 1.0; the kilij #8a8e96, turned to show its curve, out off
the helmet (0.68, -0.3, 0.8); FIT_SCALE 1.08 (the pixels were 0.83x stock).
Tint 32.8 % (stock 51.5); fitcheck bbox-area 1.16, pixels 0.97. specks clean.

## Batch 3: continuity 79.0, fresh 77.0, average 78.0 (+5.75); CRITICAL in 4 (the kilij straight up in front of the face, merging into the helmet)
### Round 4 (solo4: review\sipahi\solo4), aimed only at the critical
The 'sipahi' seat's sword hand out to the right and forward (-0.6, -0.05, 0.8);
the kilij up, out and forward (0.45, 0.4, 0.9): in 4 it stands beside the
helmet over the horse's neck, in 3 beside it; a lighter, narrower blade
(#c4c8d0, 0.085) with a bright polished edge strip (props.sabre edge, off by
default). Tried: (0.62, 0.05, 0.85) hid it in 3; (0.4, 0, 1) crossed the face
in 4. Tint 31.5 %; fitcheck bbox-area 1.18, pixels 0.93; specks clean.

## Round 4: continuity 82.5, fresh 82.0, average 82.25 (+4.25); the blade critical closed
## FINAL = round 4 (ba03be5f, 82.25)
HEAD reproduces the solo-4 sheet; in verify_all.sh. fitcheck bbox-area 1.18,
pixels 0.93; tint 31.5 % (stock 51.5).
Later pass:
- the kilij's curve in profile in 4 and 5;
- more blade length in 2 and 3 (it reads as a dagger);
- a bent elbow and a kaftan sleeve on the sword arm (1, 2, 6);
- shoulders and upper arms ~15 % wider.
