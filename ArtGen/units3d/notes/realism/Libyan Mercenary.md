# Libyan Mercenary: realism design (2026-11-23)

## The real-world model

A Libyan spearman in Carthage's armies of the Punic Wars, about 260-202 BC (Polybius on the Libyans at Cannae
re-armed with Roman gear; the Libyphoenician levies):

- a **bronze helmet** of Hellenistic type (an Attic or Montefortino-like bowl with cheek pieces), a crest;
- a **linen or scale cuirass** over a white tunic;
- a **round shield** (the Hellenistic aspis or a large flat round), a long **spear**;
- greaves over leg wraps.

Civ3's original: a bronze Attic helmet with a crest, a grey cuirass over a white tunic with civ sleeves and a
civ hem, bronze greaves over leg wraps, the long spear upright, a big white round shield with a gold rim and a
civ sunburst.

## The features that must read at 1x

1. The **big white round shield with the civ sunburst**, forward on the forearm, turned to the viewer.
2. The **spear held ready**, tipped forward.
3. The **bronze crested helmet**.
4. The **grey cuirass** over the white tunic, civ sleeves and hem.

## The pose

As the Hoplite's: weight on the right leg, the shield forward on the left forearm, the spear raised in the
right fist at chest height and tipped forward; the torso upright.

## Civ colour

Stock: the shipped Libyan Mercenary 10-22 % (mean 15): low from behind. Aim 15-30 % in every direction.
Regions: the **sunburst**, the **sleeves**, the **tunic's hem**.

## Stock neighbours

Hoplite and Spearman.

## Rounds

### Build rounds 1-3
- The spear raised ready, the sunburst shield forward, then by its grip (its oval board as the Spearman's); a wider
  civ hem (0.08), then a civ crest, so the colour shows from behind (9-12 % before).

### Critic round 1 (batch 3): 79.5 / 72.5 = 76
Critical (one critic): the shield hand's knuckles through the sunburst in 1-2 (grip_depth was -0.05, the sign error
the Swordsman found). Both: the cuirass a featureless grey slab merging with the tunic at 1x. Fixes: grip_depth
+0.05, a darker cuirass, the skirt in pleats.

### Critic round 2 (batch 3): 81.5 / 78 = 79.75
Landed: the pleated skirt, the shield in front of the knuckles. Both: the cuirass still a featureless slab from
behind. One: the shield's back in 6-7 bare; the shield edge-on in 4, behind him in 5. Fix: a linen corselet in small
scales (the cuirass cloth with a fine check pattern, not metal).

### Critic round 3 (batch 3): 82 / 80.5 = 81.25; solo round: 82 / 70 = 76
Over 80, so a solo round (libyan_solo_r4.png): the fresh critic found a critical failure, the crest floating on its
stilt with sky under it in 3, 4 and 8. Fixed in riders.attic_helm (the crest seated on the crown; only the Libyan
uses it). That critic confirmed it gone (70, unchanged on score; the crest now low, more skullcap than horsehair).

## Final: round 3's model with the crest seated, 81.25 (best round)
Civ 11-20 % (mean 15). Open points: the corselet's construction from behind (shoulder flaps, a rim, pteruges; the
fine check pattern does not read); the shield's inner face (a rim, wood or leather); the spear hand crossing the
shield in 4 and 8; the spear nearer upright.

Animations re-rendered from the final still (build.py --anim, 2026-11-23) and checked at 2x (directions 1, 3, 6): ATTACK1, FORTIFY, RUN and DEATH play cleanly; in direction 1 the thrust or cut points at the camera, so it reads foreshortened.
