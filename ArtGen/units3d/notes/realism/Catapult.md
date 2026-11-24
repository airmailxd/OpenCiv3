# Catapult (realism pass, siege batch)

**Model.** A wheeled torsion mangonel (Hellenistic/Roman onager family, as Civ3 draws it): two heavy side rails on
four spoked wheels, an A-frame with a padded crossbar, a twisted-rope torsion skein, the throwing arm raised with
its iron cup and boulder, a rope-wound winch drum at the rear; two crewmen (winchman with a handspike, loader).

**Must read at 1x:** the raised arm with the dark boulder in its cup; the A-frame and crossbar; the four wheels
under a heavy timber chassis; the two-man crew.

**Civ colour:** the crossbar, the upper side rails, the front bar, the crew's tunics (the cup lining).
Stock 24.1 % (25.5/22.3/26.4/23.1/23.8/25.9/25.0/20.6).

**Neighbours (batch):** Knight, Infantry stock rows; per-unit looks against Trebuchet and Legionary.

## Build rounds (own looks, before batch critic 1)
- b1: warmer wood (#9a6a3a) with and without the flat civ side boards: change too subtle (grime/soot dominate).
- b2: dark oiled oak (siege.OAK); side boards off; upper rails as wood (18.6 %) vs civ (24.0 %): kept civ rails
  (stock parity; they read as deliberate painted beams on the dark timber).
- b3: arm 62 vs 50: kept 62 (the cup and boulder silhouette).
- b4: the pale steel cup rim read as a white boulder at 1x: rim in dark iron, stone #5e574e.
Chosen (siege.REALISM['Catapult']): panels=False, cup_rim='iron', colors=OAK.

## Batch 1: 72.5 / 73.5 = 73.0
Fixes asked: a smaller boulder in a visible spoon cup, the arm cocked back, clear of the crewman's helmet in 4/5;
the winch crank, ratchet and rope in 4/8 with a man on it.
## Batch 2 build rounds
- c1: arm cocked back (30 deg) and shorter (cup 1.7 m from the pivot), the boulder 0.75x down in the cup; the
  winchman on the left crank (both hands on the grip, leaning in), the loader's hands on the boulder.
- c2: the cup still beside the loader's head in 4: arm 24 (cup at 1.43 m, below head height, the arm resting on
  the winch drum), the loader further out. c3: the loader closer (he stood apart in 6, a second piece).

## Batch 2: 68.5 / 81 = 74.75
## Batch 3 build rounds
- d1: arm 54 (arm and cup above the top beam), cup 1.3x, stone 0.85x and pale (#a49c90), timber a shade lighter
  (OAK_LIGHT); loader hauling the arm down against the frame. The cup's bottom showed as a dark disc.
- d2: cup_level: the cup opens straight up at this arm angle; the boulder now shows in it.
- d3: the loader forward along the arm (his head was under the cup).

## Batch 3: 74.5 / 83.5 = 79.0 (best)
## FINAL = batch 3 (f16ebbf7). Critic history: B1 73.0, B2 74.75, B3 79.0.
Finals: reviews/realism/catapult/final.png, final_red.png (shipped, final, Knight, Infantry).
Later pass: the cup back to ~1.1x and the boulder ~25 % smaller (reads as a wok/mushroom in 2, 4, 5, 6); show
the arm spar in 4 and 8; bring the right-hand crewman in, in 2.
