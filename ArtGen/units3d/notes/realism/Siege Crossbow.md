# Siege Crossbow (realism pass, NumPy siege group)

**Model.** A double-bow bed crossbow (the Chinese chuangnu; Japan's oyumi): a heavy grooved stock on two
trestles on a low four-wheeled cart, two wide composite bows lashed across it (the front one lacquered in the
civ colour, the second in natural rattan-bound horn), all four string ends drawn back to the bronze trigger nut,
a spear-sized bolt with an iron leaf head and wooden vanes in the groove, a rope windlass at the rear with
crossed capstan bars. Two crew in black jingasa and civ coats: the windlass man on a capstan bar, the aimer at
the trigger.

**Must read at 1x:** the wide front bow with the white strings drawn back in a V; the bolt; the low cart.

**Civ colour:** the front bow, the crew's coats. Shipped 16.1 % (15.4/17.0/15.3/14.3/17.6/18.4/17.8/12.9).

## Own build rounds
- a1: thin black bows and a zigzag second string: unreadable; strings speckled.
- a2: thicker bows, both civ, straight strings to the nut: reads; tint 26.2 (over).
- a3: rear bow black, hats black: tint 15.1, ppm 32.0, pixels 1.01.
- a5: the aimer moved back to the trigger (he hid the bows in 3), rear bow in tan rattan (the black one
  vanished), bolt 1.6x: ppm 31.5, pixels 0.99, tint 15.9, specks clean.

## Batch 1 (sheet review\_batch\batch_numpysiege_1.png, GPU at HEAD bc911571)
Builder's numbers: tint 15.9 vs shipped 16.1; fitcheck pixels 0.99, bbox-area 1.01; specks clean; crew ppm 31.5.
Critics: 71 / 76.75 = 73.9. Fixes asked: darker, thicker hemp strings (white flickered as X/stars in 2, 3, 6);
bows dark with civ only on tips or bindings, tint made up elsewhere; crew spread to the rear and a side (they
stacked in front of the bed in 5-7); more mass and parts on bed and cart.

## Round 2 build (b1-b3)
Size rule (lead): between the stock Catapult and the stock Cannon (Cannon = 0.54x the Catapult's area).
- b1: strings #8a7452 r 0.03; both bows dark (front lacquer, rear rattan) with civ grip bindings and civ tip caps;
  civ side boards on the cart; stock 0.3 x 0.2; iron cheek plates at the bow seats; a rack of three spare bolts
  on the right side; the windlass man outside the left capstan, the aimer behind the stock's end: tint 13.9.
- b2: civ hats on both: 19.5. b3: the aimer's hat only: 16.7 (shipped 16.1).
  0.79 of the stock Catapult, 1.48 of the stock Cannon (inside their range), 0.98 of the NumPy sprite;
  ppm 30.9; specks clean.

## Batch 2: 79.5 / 83.75 = 81.6 (best so far). Batch 1: 73.9.
Fixes asked: the bows merged with the bed at 1x (1, 3, 4, 8): lighter edge or thicker and longer; crew separated
(1, 5, 6), the front man behind the capstan in 5.
## Round 3 build (c3)
Bows thicker (r 0.13 / 0.11) and longer (spans 3.0 / 2.5), both in dark lacquer (the tan rattan rear bow merged
with the bed); the windlass man behind the left capstan (-0.6, -1.85) on its bar's lower end, the aimer behind the
stock's right side (0.5, -1.9). Tint 15.0 (shipped 16.1); 0.75 of the stock Catapult, 1.42 of the stock Cannon
(in range), 0.94 of the NumPy sprite; ppm 29.5; specks clean.

## Batch 3: 81 / 85 = 83.0 (best).
## FINAL = round 3 (code 8b911c0a). Critic history: B1 73.9, B2 81.6, B3 83.0; no critical.
Finals: reviews/realism/siege-crossbow/final.png, final_red.png (shipped, final, stock Catapult and Cannon finals).
Size: 0.75 of the stock Catapult, 1.42 of the stock Cannon (in their range), 0.94 of the NumPy sprite.
Later pass: the aimer's hat is still civ: make it dark like the other two units; both crew in front of the bed in
dir 6; the bows weak in dir 4; the crew stack in 4 and 8.
