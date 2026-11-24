# Fire Cannon (realism pass, NumPy siege group)

**Model.** A breech-loading bronze furanki of the kind Otomo Sorin took from the Portuguese (the kunikuzushi,
"country-breaker"): a long gun with raised rings and a muzzle swell, its rear an open breech bay holding the
removable iron powder chamber (a mug with a handle), wedged, a short iron tiller behind; on a low box carriage
(the shipped unit's: two cheeks of three stacked beams stepped down to the rear) on four solid trucks; a linstock
in its socket at the rear corner. One gunner (as shipped): an ashigaru in a civ coat and civ jingasa, the left
hand on the tiller, the right on the chamber, seating it.

**Must read at 1x:** the long gun past the carriage; the low box carriage on small solid wheels; the gunner
at the breech.

**Civ colour:** the middle beam of each cheek (lacquered), the gunner's coat and hat.
Shipped 19.4 % (17.3/28.6/16.8/11.0/17.8/35.8/17.3/10.9).

## Own build rounds
- a1: two crew (gunner with a torch as linstock, loader): pixels 1.24, bbox 1.22 head-on.
- a2: one gunner, barrel 2.9 m, darker bronze: pixels 1.04, tint 14.5.
- a3: civ jingasa: tint 21.1.
- a4: gauge 1.25 (the gun read thin), the breech bay scaled with it, grimier bronze.
- a5: the torch read as a spear along the barrel: dropped; a linstock stands in a socket on the carriage; the
  gunner seats the chamber (hands on tiller and chamber handle). ppm 37.3, pixels 1.09, tint 19.5.
- a6: engine k 1.1 (crew ppm toward 34).
- a7: k back to 1.0 (k 1.1 pushed pixels to 1.12).

## Batch 1 (sheet review\_batch\batch_numpysiege_1.png, GPU at HEAD bc911571)
Builder's numbers: tint 19.5 vs shipped 19.4; fitcheck pixels 1.09, bbox-area 1.05; specks clean; crew ppm 37.3.
Critics: 72.5 / 79.5 = 76.0. Fixes asked: a second crewman (ramrod or linstock); the chamber a mug with a handle
and the wedge, standing clear (it was built upside down: the barrel's local +Y is down, so the handle hung under
the gun); barrel 20-25 % shorter with a heavier swell; the gunner beside the breech in 6; bigger trucks.

## Round 2 build (b1, k1-k4, b3)
Size rule (lead): 0.90-1.10 of the stock Cannon's opaque area. The NumPy sprite itself is 1.35x the stock Cannon.
- b1: barrel 2.25 m, swell 1.35, the bay and chamber flipped the right way up (low walls, a hooped mug with a
  big loop handle on top, the wedge proud behind it), trucks r 0.36, the gunner beside the breech, a rammer:
  1.96x the stock Cannon.
- k1-k4: engine k and fit_scale for two crew in a Cannon-sized outline: k 0.78 / fit 0.72 gives 1.07.
- b3: k 0.8, fit 0.72, gauge 1.45 (the gun read thin at the smaller scale), the rammer by the muzzle, both hats
  black (as shipped): 1.04 of the stock Cannon (0.77 of the NumPy sprite), ppm 31.0, tint 24.0 (shipped 19.4,
  stock Cannon 26.5). Open: the two crew dominate a small gun at this size.

## Batch 2: 67 / 70.25 = 68.6 (B: new critical, unclear identity: two men beside a small cart). Batch 1: 76.0 (best).
Fixes asked: gun and carriage ~1.5x, barrel between R1 and R2, the largest shape; crew at the breech's flanks and
rear, nobody between camera and barrel. Size allowance: up to ~1.30 of the stock Cannon.
## Round 3 build (r3a/b, o1/o2, p1/p2, g3)
- r3a/b: k 1.2, barrel 2.6 m, crew at (-0.68, -0.72) and (0.7, -0.85): the gun reads; 1.44-1.69 of the stock Cannon.
- o1/o2: one crewman (the gunner): still 1.52-1.68 (the 1.5x gun is the mass), tint 11: kept two.
- p1/p2, g3: k 0.95 (gun and carriage ~1.2x round 2; the barrel 2.5 m in the world, longer than a man),
  fit_scale 0.83: 1.28 of the stock Cannon, 0.94 of the NumPy sprite; ppm 31.6; tint 19.9 (shipped 19.4);
  specks clean.

## Batch 3: 81 / 83.75 = 82.4 (best; identity critical closed).
## FINAL = round 3 (code 8b911c0a). Critic history: B1 76.0, B2 68.6 (critical), B3 82.4.
Finals: reviews/realism/fire-cannon/final.png, final_red.png (shipped, final, stock Catapult and Cannon finals).
Size: 1.28 of the stock Cannon (the lead's 1.30 allowance), 0.94 of the NumPy sprite.
Later pass: in dirs 1, 3, 4 and 8 the two crew stack into one tall lump behind the breech: move the rammer forward
to the muzzle side; the gun ~1.1x larger if the size allowance permits, so it clearly outweighs the crew.
