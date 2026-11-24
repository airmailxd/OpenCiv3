# Minamoto Samurai: realism design (2026-11-23)

MDJ unit, remade in units3d. The Minamoto clan of the Genpei War (1180-1185).

## The real model
A mounted-class bushi of the late Heian in **o-yoroi**: the boxy armour of laced scales (rendered with yoroi.py's
lames), the **big flat sode** (larger than the Sengoku samurai's), **four broad kusazuri** (o-yoroi hung four
boxy tassets), a hoshi-bachi **kabuto** with tall gilt kuwagata and the wide fukigaeshi, no menpo (the half mask
came later). Pale hakama (the Minamoto's white). A tachi drawn and held high in hasso.
It must not read as a second Samurai: bigger square sode, fewer and broader tassets, no banner, no red mask,
light hakama, taller horns.

## What must read at 1x
1. The kabuto's tall gilt V over the flared shikoro;
2. the square, big civ-laced sode and the four-panel kusazuri: a broad, boxy figure;
3. the bright blade up and out over the right shoulder;
4. pale hakama under dark armour.

## Pose
The Samurai's hasso (the critics found it reads in 6 of 8 directions), so his animations are the Samurai's
('x_Samurai...' flics; anim.ACTIONS['Minamoto Samurai']).

## Civ colour
The odoshi lacing (sode, kusazuri, shikoro). Shipped 37% (the whole armour civ); stock 11-35%. Target 20-30%.

## Neighbours and size
Critic sheet: shipped, new, Samurai, Medieval Infantry. Size counterpart: the Samurai.

## Rounds

### Build round 1 (48 samples; C:/Dev/_handoff/realism/review/mdj/b1_min.png and b1_min_red.png)
Seen: civ 37% (33-42). Reads well: big laced sode, pale hakama, gilt V, the blade in hasso; distinct from the Samurai. Wrong: civ too high; area 506 vs the Samurai's 602 (0.84).
Change (round 2, in the entry, not yet rendered): kusazuri edge 0.45 (more lacquer showing), unit_scale 1.05. Next if still high: sode lacing partly lacquer.
Round 2 never rendered on this PC (memory over the gate all night); the unit moved to the second PC. No critic round has run. The sizes above are from the 48-sample render: re-measure at 128 against the stock class before the first critic round.

### b1 (PC2, 128 samples, GPU; batch_numpymdjfoot_b1b)
Re-measured at 128: area 1.17 of the Samurai at unit_scale 1.05 (48 samples had said 0.84): unit_scale 0.97.
Changes: the kabuto lifted off the face (brow_drop 0.055); the tachi as nmf_katana (nmf_kit: yoroi's drawn katana
with a thicker back and a blunter kissaki), width 0.085, so the blade reads at 1x and doesn't break into dashes.
Numbers: civ 13.9% (11.5-16.1), area 1.03 Samurai / 1.07 NumPy; specks: the blade's tip (12 px) parts from the
blade by one pixel in dir 2; 1 px tip pixels in dirs 3, 5, 6.

b1 scores: 78.75 / 76.5 = 77.6 (A: the face a black blob in dirs 1-3; the tachi hidden in dir 4).

### b2 (batch_numpymdjfoot_b2b)
The shikoro's lacing out of the civ colour (new kabuto param lace_tint False, yoroi.py, off by default) in a pale
tan ('lace' #b89a6a) so the eye line isn't a civ band and the face shows against it; the kabuto lifted
(brow_drop 0.03), the skin lighter; the tachi curved (nmf_katana curve 0.075, width 0.09, spine 0.024: the 12 px tip
is gone); hasso's blade_dir (-0.62, -0.05, 0.78) so in dir 4 it rises clear above the helmet.
Numbers: civ 11.8% (9.0-14.3), area 1.04 Samurai / 1.08 NumPy; specks: 1 px, dirs 2 and 3.

b2 scores: 81.75 / 79 = 80.4 (A: the dir-4 critical open, the tachi a stub at the cheek).

### b3 (batch_numpymdjfoot_b3b, the last round)
The tachi longer (0.92) and more upright out of hasso (blade_dir (-0.42, -0.08, 0.9)) so it rises above the
kuwagata in dir 4; a deeper curve (0.1); a civ agemaki bow on the back (nmf_agemaki).
Numbers: civ 12.9% (10.7-15.0), area 1.05 Samurai / 1.10 NumPy; specks: 1-2 px, dirs 1-3.

## FINAL: b3, A 85.25 / B 79.8 = 82.5 (rounds: b1 77.6, b2 80.4, b3 82.5); no critical open; the dir-4 critical closed at b3
Final sheet: reviews/realism/minamoto_samurai/final(_red).png (shipped NumPy, final, Samurai, Medieval Infantry); also D:\Dev\_handoff\realism\review\minamoto-samurai\.
Later pass: raise the dir-4 tip clear above the kuwagata; the agemaki barely shows (bigger, or higher on the back).
