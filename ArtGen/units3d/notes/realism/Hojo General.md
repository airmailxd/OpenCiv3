# Hojo General: realism design (2026-11-23)

MDJ unit (standalone mode drew it with a stock look under the art name 'ArmyAncient*'), remade in units3d.
It has no catalog entry on this branch: remaster_mdj says 'units3d_only' and catalog_json.py gives it an
empty footman to stand on.

## The real model
A Sengoku daimyo of the Hojo of Odawara in his best tosei-gusoku: the Samurai's armour laced in **red** (a
lord's colour, not the civ colour), a kabuto with very tall gilt kuwagata and a red menpo, dark red-brown
hakama; the **big civ sashimono** (the general's banner); his katana drawn and raised forward in his right
hand, signalling the advance; the left fist on his hip; the scabbard empty at his hip.

## What must read at 1x
1. The very tall gilt kuwagata and the big banner: the tallest figure among the Japanese units;
2. the raised bright blade pointing forward, one-handed (a commander's gesture, not a fighter's guard);
3. red-laced dark armour: a dark red figure, unlike the blue-laced Samurai.

## Pose
'ready' with the katana raised forward and up; contrapposto; left fist on the hip.

## Civ colour
The sashimono only (shipped 2%). Target 12-20% with a larger banner.

## Neighbours and size
Critic sheet: shipped, new, Samurai, Medieval Infantry. Size counterpart: the Samurai (a commander a touch larger).

## Rounds

### Build round 1 (48 samples; C:/Dev/_handoff/realism/review/mdj/b1_hojo.png and b1_hojo_red.png)
Seen: civ 6.9% (3-10). Reads well: red-laced armour, tall kuwagata, the raised blade, the banner. Wrong: civ too low (the banner alone).
Change (round 2, in the entry, not yet rendered): sashimono 0.34 x 0.8 m on a 1.66 m pole. Size: area 696 vs Samurai 602 (fine for a commander).
Round 2 never rendered on this PC (memory over the gate all night); the unit moved to the second PC. No critic round has run. The sizes above are from the 48-sample render: re-measure at 128 against the stock class before the first critic round.

### b1 (PC2, 128 samples, GPU; batch_numpymdjfoot_b1b)
Re-measured at 128 with the bigger banner: area 1.55 of the Samurai at scale 1.0: unit_scale 0.9 (1.29 Samurai,
1.05 Swordsman; the banner is about a fifth of it, so the body sits near the Samurai's size).
Changes: the katana as nmf_katana, width 0.085 (it broke into dashes in dir 8); the banner pole thicker (0.022) and
the kuwagata thicker and a little shorter (0.31, width 1.6) so neither breaks up.
Numbers: civ 11.2% (0.6-18.0: the banner is the only civ and is edge-on in dirs 3 and 7), area 1.29 Samurai /
1.05 Swordsman / 1.29 NumPy; specks: a 5 px blade-tip piece in dir 7, 1 px elsewhere.

b1 scores: 84.0 / 67.8 = 75.9 (both: the civ unreadable in dirs 3/7, the banner the only accent).

### b2 (batch_numpymdjfoot_b2b)
The lacing in the civ colour on the brown lacquer (tint lace + banner; the red menpo kept), so sode, kusazuri and
shikoro carry civ from every side; the kuwagata and maedate plate bigger (horn_width 1.9); the katana raised above
the shoulder (hand (-0.2, -0.3, 0.3), weapon_dir (-0.4, -0.45, 0.8)) as nmf_katana width 0.09 / spine 0.024; a
saihai raised in the left fist; the banner back to 0.28 x 0.66 (civ was 25%).
Numbers: civ 21.6% (14.9-26.5), area 1.01 Swordsman / 1.25 Samurai (banner in) / 1.25 NumPy; specks: 1 px, dir 3.

b2 scores: 81.5 / 72.5 = 77.0 (both: he twinned the Minamoto).

### b3 (batch_numpymdjfoot_b3b, the last round)
The red lacing back on the do (red lame edges, rivets 0.45 in #a0281c) and the shikoro (kabuto lace_tint False,
lace #9a2a20); civ on the sode, kusazuri and sashimono only; the saihai as nmf_saihai (a short dark baton with a
full white paper tassel); the katana raised higher (hand (-0.2, -0.24, 0.45), weapon_dir (-0.3, -0.35, 0.89)).
Numbers: civ 19.7% (13.1-25.7), area 1.02 Swordsman / 1.26 Samurai (banner in) / 1.26 NumPy; specks: clean.

## FINAL: b3, A 84.5 / B 77.3 = 80.9 (rounds: b1 75.9, b2 77.0, b3 80.9); no critical open
Final sheet: reviews/realism/hojo_general/final(_red).png (shipped NumPy, final, Samurai, Medieval Infantry); also D:\Dev\_handoff\realism\review\hojo-general\.
Later pass: the red lacing wider, over the whole do; in dir 3 the katana angled away from the banner pole; a jinbaori (the commander's surcoat).
