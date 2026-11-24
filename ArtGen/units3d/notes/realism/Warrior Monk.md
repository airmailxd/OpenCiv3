# Warrior Monk: realism design (2026-11-23)

MDJ unit, remade in units3d: a sohei of the great temples (Enryaku-ji on Mount Hiei, Kofuku-ji), 11th-16th c.

## The real model
- The **katsuo-zukin**: a white cloth wrapped over the head and the lower face (mdj_kit 'zukin');
- a dark robe (koromo) with wide sleeves over wide hakama, white kyahan leg wraps, waraji;
- the monk's **kesa** worn as a broad band over the left shoulder, and an obi;
- the **naginata**: a long curved blade on a pole, the sohei's weapon.

## What must read at 1x
1. The white head wrap (a pale dome on a dark figure);
2. the naginata's long bright curved blade high and out at his right;
3. the wide dark sleeves and hakama: a robed silhouette, not a soldier's;
4. the civ kesa band across the chest.

## Pose
'ready': the naginata upright in the right fist out at his side, blade up and edge forward; the left hand at
his side.

## Civ colour
The kesa band (chest and back) and the obi. Shipped 10% (robes civ); stock 11-35%. Target 12-20%.

## Neighbours and size
Critic sheet: shipped, new, Samurai, Pikeman. Size counterpart: the Pikeman.

## Rounds

### Build round 1 (48 samples; C:/Dev/_handoff/realism/review/mdj/b1_monk.png and b1_monk_red.png)
Seen: civ 6.7%. Reads: the white head wrap, the naginata, dark robes. Wrong: the zukin reads as a white box or bucket (stacked ellipsoids and fold rings); the civ kesa too thin.
Change (round 2, in the entry, not yet rendered): zukin rebuilt as a rounder hood with a close lower wrap (mdj_kit), kesa width 0.2 and offset 0.04, obi 0.1, unit_scale 0.95 (area 514 vs Pikeman 485).
Round 2 never rendered on this PC (memory over the gate all night); the unit moved to the second PC. No critic round has run. The sizes above are from the 48-sample render: re-measure at 128 against the stock class before the first critic round.

### b1 (PC2, 128 samples, GPU; batch_numpymdjfoot_b1a)
Re-measured at 128: area 0.99 of the Pikeman (0.80 of the Swordsman); unit_scale 0.95 kept.
Changes: mdj_kit's rounder zukin covered the whole face (a white ball from the front, the hanging ends like a mask
from behind): a new hat nmf_zukin (nmf_kit) leaves the eyes and nose open between the hood and the mouth wrap, one
broad end behind; the kesa 0.26 wide, the obi 0.16; the naginata blade wider (0.075) so it reads at 1x.
Numbers: civ 9.8% (3.7-15.0; the kesa is on the left shoulder only), area 0.99 Pikeman / 1.02 NumPy;
specks: one 1 px blade-tip pixel in dir 1.

b1 scores: 77.5 / 69.3 = 73.4 (A: identity, the naginata read as a yari in dirs 1-4).

### b2 (batch_numpymdjfoot_b2a)
The naginata as nmf_naginata (nmf_kit): 2.5 m (taller than him), a 0.78 m blade 0.095 wide with a strong sori
(curve 0.16) and a thick back, the flat turned to the side (edge_dir (-1, -0.3, 0)) so the curve shows in dirs 1-4;
unit_scale 1.05 (stock height); a white under-robe collar (v_collar) and lighter grey hakama against the dark robe.
Numbers: civ 9.7% (3.9-14.6), area 1.00 Swordsman / 1.23 Samurai and Pikeman (the naginata in) / 1.27 NumPy;
specks: clean.

b2 scores: 83.5 / 75.5 = 79.5.

### b3 (batch_numpymdjfoot_b3a, the last round)
The naginata held out to his right about 30 degrees (weapon_dir (-0.5, -0.15, 0.85), the fist at x -0.3) so the
blade clears the hood and body in dirs 3-4; the kesa wider (0.34) with pale edges (chest_strap 'edge', kesa_edge
#e4dac0), a lighter stole over the dark robe.
Numbers: civ 9.3% (3.2-14.3), area 1.00 Swordsman / 1.27 NumPy; specks: clean.

## FINAL: b3, A 84.5 / B 75.5 = 80.0 (rounds: b1 73.4, b2 79.5, b3 80.0); no critical open
Final sheet: reviews/realism/warrior_monk/final(_red).png (shipped NumPy, final, Pikeman, Samurai); also D:\Dev\_handoff\realism\review\warrior-monk\.
Later pass: in dirs 3-4 the haft tilted back toward vertical so the blade rises above the hood; fold shading on the robe.
