# Medieval Japanese Catapult (realism pass, NumPy siege group)

**Model.** A traction trebuchet (the Chinese pao, Japanese hassekisha; the engine the Mongol invasions and the
later continental manuals show) on a low four-wheeled cart: a trestle of two raking A-frames, a wide lacquered
lintel with upswept ends (a kasagi; the shipped unit's very wide, bow-like crossbar) over a straight tie, a long
arm of three spars bound with rope, cocked: the long end down to the rear with the sling and its stone held by
the loader, the short end raised at the front with the hauling ropes hanging, tied off to the cart's front beam.
One crewman (as the shipped unit): an ashigaru in a civ coat, dark trousers, white leg wraps and a civ jingasa.

**Must read at 1x:** the tall trestle with the wide upswept lintel; the long diagonal arm down to the loader;
the stone in the sling; the hat.

**Civ colour:** the lintel and its tie (lacquered), broad cloth wraps on the arm (pivot, short end), the
crewman's coat and jingasa. Shipped 22.2 % (22.2/21.3/22.6/16.3/24.9/34.4/25.6/10.4).

**Neighbours (batch):** the closed units3d Catapult and Cannon finals.

## Own build rounds
- a1: arm 14 deg, loader and hauler at the ends: ppm 24.7 (crews small), 26 specks (thin ropes).
- a2: shorter arm, 5 ropes r 0.024: specks clean, ppm 27.4, pixels 1.44.
- a3: civ jingasa (the shipped hat), compact cart: ppm 30.6 but pixels 1.63.
- v/c/d/e: two crewmen at ~34 ppm are ~0.4 of the shipped opaque area (NO_CREW render): the band can't be met
  with two; one crewman (the loader, as shipped), thin timbers (th 0.6), open cart, root fit_scale 0.92.
  Ropes laid forward to a pull-bar read as a rake/ladder: now hanging, tied off to the front beam.
- f1: a longer arm (2.7 m, 28 deg) on a taller trestle (2.2 m): a sparser, taller outline like the shipped one:
  ppm 31.1, pixels 1.09, bbox 0.90, tint 18.8. a6: 2 ropes (3 read as a cage head-on).
- a7: wider civ wraps on the arm: tint 20.4 (shipped 22.2).

## Batch 1 (sheet review\_batch\batch_numpysiege_1.png, GPU at HEAD bc911571)
Builder's numbers: tint 20.4 vs shipped 22.2; fitcheck pixels 1.09, bbox-area 0.90 (min 0.64 dir 2: narrower
and taller head-on than the shipped wide crossbar view); specks clean; crew ppm 31.1 (fit_scale 0.92).
Critics: 62.5 / 64.5 = 63.5 (borderline/near critical: identity, reads as a bow or ballista; spindly next to
the stock Catapult). Fixes asked: a straight plain axle beam (the curved lintel read as a bow in 2/6); a traction
crew of two hauling 4-6 ropes, nobody on the arm; frame and arm ~1.5x heavier; civ on cheeks or coats.

## Round 2 build (b1-b2)
Size rule changed (lead): size against the stock class: between the stock Catapult and the stock Trebuchet
(stock Trebuchet = 1.93x the Catapult's opaque area). No more fit-to-the-sparse-NumPy compromise.
- b1: lintel_style 'straight' (a plain dark cap beam, iron straps, a plain tie under it), th 0.9 and spar 1.5,
  civ sills (the cart's cheeks), five pull ropes from the short end, two haulers in front (two ropes each, the
  middle one slack), the stone in the sling's pouch on the ground behind: 0.92 of the stock Catapult.
- b2: fit_scale 1.12, the haulers spread to x +-0.62: 1.09 of the stock Catapult (0.57 of the Trebuchet),
  1.73 of the NumPy sprite; ppm 34.6; tint 21.0 (shipped 22.2); specks clean.

## Batch 2: 78.5 / 83.5 = 81.0 (best so far; identity critical closed). Batch 1: 63.5.
Fixes asked: dark lacquer hats (civ discs merged in 2/6); the arm's sleeve plain wood; haulers ~30 % wider (2),
staggered half a body (4/8).
## Round 3 build (c3)
Hats dark (tint ['top'] only), wrap_tint off, haulers at x +-0.81 (were 0.62), the right one 0.32 m further out
(y 2.4). Tint 12.5 (shipped 22.2; civ now coats and sills only, as asked); 1.03 of the stock Catapult
(range 1.0-1.93), 1.64 of the NumPy sprite; ppm 33.2; specks clean.

## Batch 3: 84.3 / 86.25 = 85.3 PASS (best).
## FINAL = round 3 (code 8b911c0a). Critic history: B1 63.5, B2 81.0, B3 85.3 PASS; no critical.
Finals: reviews/realism/medieval-japanese-catapult/final.png, final_red.png (shipped, final, stock Catapult and
Cannon units3d finals). Size: 1.03 of the stock Catapult (1.64 of the NumPy sprite).
Later pass: the haulers overlap partly in dir 8; the frame a little light next to the stock Catapult; tint 12.5 %
against the stock Catapult's 24 %.
