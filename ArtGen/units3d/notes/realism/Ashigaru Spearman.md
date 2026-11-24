# Ashigaru Spearman: realism design (2026-11-23)

MDJ unit, remade in units3d from the NumPy sprite (its identity, not its style). Style reference: the Samurai final.

## The real model
A yari ashigaru of a Sengoku daimyo's spear block (c. 1560-1600): a foot soldier in munitions armour.
- **Okegawa-do** of black-lacquered horizontal lames (yoroi.do), no sugake cords; a short skirt of
  **kusazuri**, 6 panels of 3 laced lames (cheap armour: fewer, shorter tassets than a samurai's).
- **Jingasa**: the wide shallow cone of lacquered iron (mdj_kit 'jingasa'), the lord's mon on its front.
- A dark kosode, kobakama tucked to the knee, gaiters (suneate), waraji.
- **Sashimono** on the back: the unit's banner.
- **Yari**: a long spear (3.3 m; real nagae-yari ran 4.5-6 m, shortened so it stays in the frame), a straight
  narrow blade with a gilt habaki, the shaft dark lacquer, an iron butt. A katana at the left hip.

## What must read at 1x
1. The jingasa's wide flat cone (nothing else in the roster has it);
2. the long yari tipped forward, its bright blade above the head;
3. the sashimono over the shoulders;
4. the dark do over a laced kusazuri skirt.

## Pose
'ready': planted, a half step, upright with contrapposto; the right fist on the shaft out at his side, the yari
tipped forward and out (off the banner pole's line); the left hand relaxed at the side.

## Civ colour (tint.py: tint px / unit px, DEFAULT, by direction)
Stock: Spearman 21% (17-26), Pikeman 32% (27-38); the shipped sprite 22% (18-25). Target 15-30%.
Regions: the kusazuri's odoshi lacing, the sashimono, the jingasa's mon.

## Neighbours and size
Critic sheet: shipped, new, Samurai (final), Pikeman. Size counterpart: the Pikeman (0.90-1.10).

## Rounds

### Build round 1 (48 samples; C:/Dev/_handoff/realism/review/mdj/b1_ash.png and b1_ash_red.png)
Seen: civ 14.7% (12-17). Reads: the jingasa cone, the long yari tipped forward, the banner. Wrong: the figure a dark grey mass (kosode and jingasa near black), the kusazuri lacing barely shows; area 596 vs Pikeman 485 (too big).
Change (round 2, in the entry, not yet rendered): kosode #5a4a3a, kusazuri edge 0.3 and length 0.38, unit_scale 0.95. Next: a lighter iron rim on the jingasa, a bigger mon.
Round 2 never rendered on this PC (memory over the gate all night); the unit moved to the second PC. No critic round has run. The sizes above are from the 48-sample render: re-measure at 128 against the stock class before the first critic round.

### b1 (PC2, 128 samples, GPU; batch_numpymdjfoot_b1a)
Re-measured at 128: area 0.95 of the Spearman (1.15 of the Pikeman: the yari and the sashimono); unit_scale 0.95 kept.
Changes: the yari carried as the Otomo Spearman's (right fist forward at chest height, tipped forward, weapon_dir
(-0.12, -0.42, 0.9)) so its full length shows in every direction (dir 3 hid it behind the body); the jingasa
lifted off the face (brow_drop 0.055, height 0.15) with a lighter iron rim; the do a warmer brown (#3a2e26) with
lighter edges so the figure isn't one dark mass; the kusazuri longer with more lacing (edge 0.2).
Numbers: civ 9.9% (4.7-13.3; the sashimono edge-on in dirs 3 and 7), area 0.93 Spearman / 1.13 Pikeman / 0.86 NumPy;
specks: one 1 px spear-tip pixel in dir 4.

b1 scores: 84.25 / 74.5 = 79.4 (no critical).

### b2 (batch_numpymdjfoot_b2a)
The yari tipped further forward (weapon_dir (-0.12, -0.62, 0.78), about 40 degrees off vertical) so it parts from the
sashimono pole in dirs 3/7; the sashimono turned to yaw 65 so its face shows in dirs 3/7; the jingasa lifted
(brow_drop 0.04); the do and lacquer warm brown as the Arquebusier's final (#5a4434 / #4a3a2e); a gourd at the right
hip. Numbers: civ 9.6% (6.8-12.0), area 0.94 Spearman / 1.14 Pikeman / 0.86 NumPy; specks: 1 px tip pixels, dirs 1, 5, 8.

b2 scores: 85.25 / 75.5 = 80.4 (A passes).

### b3 (batch_numpymdjfoot_b3a, the last round)
The sashimono turned further (yaw 80) so its face shows in dirs 3/7; the gourd bigger (scale 1.5) and pale (#dcc088).
Numbers: civ 9.3% (5.2-11.1), area 0.94 Spearman / 0.85 NumPy; specks: 1 px tips, dirs 1, 5, 8.

## FINAL: b3, A 85.25 / B 76 = 80.6 (rounds: b1 79.4, b2 80.4, b3 80.6); no critical open
Final sheet: reviews/realism/ashigaru_spearman/final(_red).png (shipped NumPy, final, Pikeman, Samurai); also D:\Dev\_handoff\realism\review\ashigaru-spearman\.
Later pass: the banner is edge-on in dirs 2/6; a larger gourd.
