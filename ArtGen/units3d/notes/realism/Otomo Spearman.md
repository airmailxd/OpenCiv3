# Otomo Spearman: realism design (2026-11-23)

MDJ unit (the Otomo of Bungo, Kyushu), remade in units3d from the NumPy sprite's identity.

## The real model
A lightly armoured ji-samurai spearman: a pale kosode and wide hakama, a black okegawa-do over them (no tassets),
the sleeves tied back with a **tasuki** cord crossed over chest and back, a **hachimaki** round the brow, an obi;
leg wraps and waraji; a topknot. A yari of 2.6 m and a katana at the hip.

## What must read at 1x
1. The pale robe and hakama under a black do: a light figure with a dark chest;
2. the civ tasuki X on chest and back and the civ hachimaki;
3. the yari held ready, tipped forward, its bright blade clear of the head.

## Pose
'ready', as the stock Spearman (81, the melee family's best): the right fist forward at chest height, the shaft
tipped forward, the left hand forward low on the haft's line.

## Civ colour
Stock Spearman 21% (17-26); shipped 8% (5-10). Target 15-25%: the tasuki (front and back), the hachimaki, the obi.

## Neighbours and size
Critic sheet: shipped, new, Samurai, Spearman. Size counterpart: the Spearman.

## Rounds

### Build round 1 (48 samples; C:/Dev/_handoff/realism/review/mdj/b1_oto.png and b1_oto_red.png)
Seen: civ 4.6%. Reads: the pale robe and hakama, the civ hachimaki, the yari. Wrong: the tasuki (harness) is hidden under the do, so there's almost no civ; the do reads grey; area 445 vs Spearman 595 (0.75).
Change (round 2, in the entry, not yet rendered): the tasuki as a crossed tinted chest_strap over the do (offset 0.06), do gloss 0.3, unit_scale 1.1.
Round 2 never rendered on this PC (memory over the gate all night); the unit moved to the second PC. No critic round has run. The sizes above are from the 48-sample render: re-measure at 128 against the stock class before the first critic round.

### b1 (PC2, 128 samples, GPU; batch_numpymdjfoot_b1a)
Re-measured at 128: area 0.94 of the Spearman (the 0.75 at 48 samples was wrong); unit_scale 1.1 kept.
Changes: civ was 4.2% at 128: the tasuki widened to a painted band (width 0.1), the obi 0.12, and civ kyahan
(wraps_plain, tint 'wraps') as the third accent with the hachimaki.
Numbers: civ 12.7% (10.1-15.7), area 0.94 Spearman / 1.32 NumPy; specks: 1-2 px spear-tip pixels in dirs 1 and 8.

b1 scores: 85.75 / 65.3 = 75.5 (B read the costume as Ottoman / Central Asian).

### b2 (batch_numpymdjfoot_b2a)
The rolled band replaced by nmf_hachimaki (nmf_kit: a flat band tight above the brow, a flat knot and two tails at
the nape; r 0.104, brow_drop 0.095); narrow hakama (balloon 0.03, hem 0.45) tied into civ kyahan to just below the
knee (wraps_height 0.47); the tasuki X kept (width 0.085), obi 0.1; the yari 3.0 m and the right fist further out
(-0.3, -0.2, -0.16). Not closed: in dir 7 the gripping hand is still behind the body.
Numbers: civ 14.4% (10.9-17.0), area 0.92 Spearman / 1.29 NumPy; specks: 1 px tip pixels, dirs 1 and 4.

b2 scores: 86.5 / 71.8 = 79.15 (A passes).

### b3 (batch_numpymdjfoot_b3a, the last round)
Both hands on the shaft (two_hands, grip2 -0.35: the left fist below the right), so a hand shows on it in dir 7;
a small civ sashimono (0.18 x 0.42 on a 1.22 m pole, yaw 70) as the Japanese identifier, the hachimaki kept.
Numbers: civ 17.0% (14.5-19.8), area 0.98 Spearman / 1.38 NumPy; specks: 1 px, dirs 1 and 4.

## FINAL: b3, A 85.0 / B 73 = 79.0 (rounds: b1 75.5, b2 79.15, b3 79.0); no critical open; b2 (79.15) is within the 1.5 noise band, so b3 stays final with no restore
Final sheet: reviews/realism/otomo_spearman/final(_red).png (shipped NumPy, final, Spearman, Samurai); also D:\Dev\_handoff\realism\review\otomo-spearman\.
Later pass: the second arm is stiff in dirs 1-2; civ sits in four places: make the kyahan natural.
