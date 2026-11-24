# Otomo no Makuta: realism design (2026-11-23)

MDJ unit, remade in units3d: a samurai archer (yumi-tori); the bow was the samurai's first weapon.

## The real model
Dark lacquered armour (do, kusazuri, sode, kote, suneate) laced in the civ colour, a kabuto with a small gilt
maedate, pale hakama; the **yumi**, the tall asymmetric bow (2 m, the grip a third of the way up), black lacquer
bound with pale rattan; an arrow nocked; an **ebira** (open box quiver) of arrows on his back; a katana at the hip.

## What must read at 1x
1. The yumi's tall dark arc, taller than he is, rising high above the head;
2. the armour's square sode and laced kusazuri (civ);
3. the arrows' fan over the shoulder.

## Pose
'bow_ready': the bow upright and forward in the left fist, an arrow nocked and pointing forward-down, the right
fist at the nock (a low ready hold like the stock Bowman's; never full draw).

## Civ colour
The odoshi lacing (sode, kusazuri, shikoro). Shipped 9%; stock bowmen 11-15%. Target 15-25%.

## Neighbours and size
Critic sheet: shipped, new, Samurai, Longbowman. Size counterpart: the Samurai.

## Rounds

### Build round 1 (48 samples; C:/Dev/_handoff/realism/review/mdj/b1_mak.png and b1_mak_red.png)
Seen: civ 31.7% (28-37). Reads well: the tall yumi in every direction, the laced armour, the arrows. Wrong: civ a little high; area 492 vs Samurai 602 (0.82).
Change (round 2, in the entry, not yet rendered): kusazuri edge 0.38, unit_scale 1.05.
Round 2 never rendered on this PC (memory over the gate all night); the unit moved to the second PC. No critic round has run. The sizes above are from the 48-sample render: re-measure at 128 against the stock class before the first critic round.

### b1 (PC2, 128 samples, GPU; batch_numpymdjfoot_b1b)
Re-measured at 128: area 1.18 of the Samurai at unit_scale 1.05: unit_scale 1.0.
Changes: the yumi was end-on in dir 1 (bow_up (0.25, 0, 1) leaned it at the camera): bow_up (-0.15, 0, 1), now
tall in all 8; the kabuto lifted (brow_drop 0.04); the kuwagata a little thicker (horn_width 1.1).
Numbers: civ 12.2% (10.5-13.5), area 1.09 Samurai / 0.88 Swordsman / 0.99 NumPy; specks: 1-3 px arrow-tip and
string-end pixels in most directions.

b1 scores: 78.0 / 71.0 = 74.5 (no critical).

### b2 (batch_numpymdjfoot_b2b)
The yumi 2.3 m (about 1.3x his height), the grip at 0.8 m (below centre), brown lacquer with pale rattan; the
arrow aimed out to his left (arrow_dir (0.55, -0.8, -0.25)) so the bow turns its curve to dirs 1-2; the armour
a lighter brown lacquer (do #7a5a3a, lacquer #5e4230), so it is not the black-laced Minamoto; the kabuto lifted
(brow_drop 0.03), the skin lighter; mdj_kit's ebira sat inside the body: nmf_ebira (nmf_kit) sets it out behind
the right hip with the arrows' fan above the back, seen in the back views.
Numbers: civ 10.7% (7.6-12.6), area 1.14 Samurai / 0.92 Swordsman / 1.04 NumPy; specks: 1-2 px tip pixels.

b2 scores: 83.25 / 74 = 78.6.

### b3 (batch_numpymdjfoot_b3b, the last round)
The yumi as nmf_yumi (its own dark lacquer, a pale grip binding), turned about 35 degrees further (arrow_dir
(0.8, -0.55, -0.25), bow_up (0.15, 0, 1)) so its curve and string face dirs 1-2; the ebira's arrows taller (top 0.72)
so the fletchings show above the right shoulder from the front; the kabuto lifted again (brow_drop 0.02).
Numbers: civ 10.2% (6.7-12.1), area 0.96 Swordsman / 1.19 Samurai / 1.08 NumPy; specks: 1-4 px bits at the bow
tips and fletchings in dirs 2, 3, 5, 6, 8.

## FINAL: b3, A 83.25 / B 77 = 80.1 (rounds: b1 74.5, b2 78.6, b3 80.1); no critical open
Final sheet: reviews/realism/otomo_no_makuta/final(_red).png (shipped NumPy, final, Samurai, Longbowman); also D:\Dev\_handoff\realism\review\otomo-no-makuta\.
Later pass: in dir 1 turn the yumi about 40 degrees so its curve shows to the left, away from the ebira; the ebira's back reads as matting.
