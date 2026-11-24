# Conquistador (realism pass)

## Design in words

A Spanish conquistador of the 1520s: a polished steel morion with a comb and
upswept brim, lifted and a little smaller so the bearded face reads lit under
it; a dark blackened breastplate over a civ doublet (the civ sleeves); a civ
rodela (round shield) with a gold rim and boss on the left arm; a long lance
held upright in the right hand (out of the fit, as all poles). A dapple-grey
horse under a cream trapper, now draped (tube folds) with a gold border and
the gold breast strap, crupper and tassels; reins to the hand; iron stirrups.
The war dog (a black-and-tan mastiff with a civ collar) trots beside, as in
Civ3: a separate figure on purpose (specks.py counts it as a second piece in S
and N).

Civ in two deliberate regions: the doublet sleeves and the rodela (plus the
dog's collar). Shipped: the morion's brim hid the face; a flat box of a
trapper.

## Rounds (own)
### Round 1: the morion 1.0 and lifted 0.1; a draped trapper (tubes, folds 0.04) with a gold border; reins to the hand; iron stirrups. Tint 9.6 %.
### Round 2-3: the morion lifted 0.16 (the beard and mouth lit; the eyes under the brim).
Batch 1: tint per direction 11.4 15.1 7.9 4.3 6.2 14.1 12.3 5.7, mean 9.6 % (stock
9.4); footprint 1.31x the stock's area (1.14x height): the lance out of the fit.
specks.py: S and N two pieces (the war dog, a separate figure as in the stock).

## Batch 1: A 66.5, B 74.75, average 70.6, CRITICAL
~15-20 % larger than the stock and the neighbours; in 8 the dog passes
through the horse. Also: faces dark under the morion; the dog's legs; a
near-copy of stock.
### Round 4-5 (batch 2)
render_vehicles.FIT_SCALE (a unit's own factor on the fitted ppm; default
1.0): 0.9 for the Conquistador. GENERAL RULE for units with a pole out of the
fit (mounted.mark_poles): the rest is fitted to the stock's box, which held the
pole, so the unit comes out big; measure with scratch/footprint.py against the
stock and set FIT_SCALE (0.87 gave 0.91x area, 0.9 gives 0.99x area, 0.98x
height; the stock is ~45 px wide, 67-79 tall at 1x). The dog out at (1.15,
0.75) and truer (props.war_dog real: jointed legs, paws with toes, a keel of a
chest; DOG_BY_UNIT); a fringed, deeper-folded trapper; face_glow 0.35
(human.skin_glow: a faint emission in the skin tone; off by default).
Tint 9.1 % (stock 9.4). specks.py: the dog a separate figure in S, NE, N.

## Batch 2: continuity 71.5, fresh 77.0, average 74.25 (+3.65): both criticals closed
Fixes: a polished breastplate; real folds and a breaking hem; a bigger lance
head and a civ pennon; the dog clear of the forelegs in 8, hocks and paws.
### Round 6 (batch 3)
The breastplate #a8acb4 (metal); the trapper rounder (0.24), folds 0.1, the
hem lower (frac 1.0); props.lance head 1.5 and a civ swallow-tail pennon
(options, off by default); the dog 1.7x, at (1.2, 0.25). Tint 9.8 % (stock
9.4); fitcheck bbox-area 1.08, pixels 1.04. specks: the dog apart in S and N.

## Batch 3: continuity 77.5, fresh 83.0, average 80.25 (+6.0), no critical
## FINAL = batch 3 (3ca8d271, 80.25)
HEAD reproduces the batch-3 sheet (diff 0); in verify_all.sh. fitcheck
bbox-area 1.08, pixels 1.04; tint 9.8 % (stock 9.4).
Later pass:
- the dog fully clear of the horse in 7 and 8;
- lighter boots or a breastcollar in dir 2;
- a slightly larger pennon (more civ in 4 and 8);
- a less panel-like trapper.
