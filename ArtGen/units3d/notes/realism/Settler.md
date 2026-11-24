# Settler: realism design (2026-11-23)

## The real-world model

An emigrant of the ancient world setting out to found a colony (the Greek apoikia; the Phoenician and Roman
colonists): a traveller on foot with all he owns on his back.

- a **big bundle**: a stuffed sack with a bedroll strapped across its top and a copper pot lashed to it, on
  shoulder straps;
- a **tall walking staff** of rough wood;
- a short wool **tunic** and a **hood** (the cucullus) against the weather; a cream kilt; sandals; a beard.

Civ3's original: a traveller with a big bundle and a pot on his back, a tall staff, civ colour on the tunic
and the hood, a cream kilt.

## The features that must read at 1x

1. The **load**: a bundle big enough to show over and beside the shoulders in every direction (not only from
   behind), the pot and bedroll on it: the identity read.
2. The **tall staff**, taller than he is, upright and clear of the body.
3. The **hood** and the civ tunic.

## The pose

Walking-ready at rest: the staff planted at his right, the right fist high on it; the left hand on the
pack's strap at the chest. Torso upright under the load (no stoop), the weight on one leg.

## Civ colour

Stock: the shipped Settler 18-29 % (mean 22), Worker 14-29 (23), Scout 14-29 (22). Aim 15-28 %. Regions: the
**tunic**, the **hood**.

## Stock neighbours

Worker and Scout.

## Rounds

### Build rounds 1-2
- R1: the pack bigger (pack_scale 1.85) and higher, so it shows beside the shoulders from the front. Civ 13-26 %
  (mean 19). R2: looked, no change.

### Critic round 1 (batch civil 1): 78 / 80.5 = 79.25
Both: the sack a smooth brown blob; the hood merges with the tunic. Fixes: the sack smaller (1.55) and bound with
rope bands and a patch (props.backpack 'cords', new, off by default); a dark edge on the hood.

### Critic round 2 (batch civil 1): 78 / 78 = 78
Landed: a smaller sack (modest). Not landed: the hood's dark edge. Still: the sack a plain blob in 3-6. (The fresh
critic also called the civ share high, about 30 % by eye; it measures 16-28 %, mean 21, as the shipped.) Fixes:
vertical rope lashings and a patch on the sack (the cords option now lashes vertically: the round-1 rings read
as a shield's rims); a cream rim on the hood.

### Critic round 3 (batch civil 1): 79 / 81 = 80
The vertical rope lashings read as a shield's rims again and were dropped before the round. Landed: the hood rim
from the front. Still: the sack plain from behind (a cloth bundle with a knot, seams, a hide grain); the hood a
blue dome from behind (a point down the back); flat tunic folds.

### Solo round (round 3's state, unchanged): 79 / 65 = 72
Both: the sack a plain oval that reads as a shield or boulder from behind; no shoulder straps visible; the hood a
blue dome from behind. The fresh critic: a static pose, the pot and bedroll over the hood in direction 1, the
kilt too bright, a cleaner, more plastic look than the stock.

## Final (round 3)
Scores: R1 78 / 80.5 = 79.25; R2 78 / 78 = 78; **R3 79 / 81 = 80, final**; solo 79 / 65 = 72. No critical failure;
not 85; at the cap. Civ 16-28 % (mean 21). Open points: the sack as a cloth or hide bundle knotted at the top with
ears, seams and patches (rope bands, tried twice, read as a shield's rims); shoulder straps visible from the front;
a hood point down the back; a walking pose; the pot and bedroll off-centre in direction 1; a dirtier kilt.

## Animations
The templates (run, death) from the final still; checked at 2x. The staff falls with him.
