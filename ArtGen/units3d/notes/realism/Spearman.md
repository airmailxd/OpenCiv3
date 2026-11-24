# Spearman: realism design (2026-11-23)

## The real-world model

A spearman of the Late Bronze Age Aegean and Levant, about 1250-1150 BC (the warriors of the Warrior Vase
from Mycenae, and the Sea Peoples on the Medinet Habu reliefs): the light infantryman before the hoplite.

- a **bronze helmet with a crest** (the Warrior Vase's horned and crested helmets; here a crest of dyed
  horsehair on a ridge);
- **bare torso** with a leather baldric strap, a short **kilt** belted at the waist, sandals or bare feet;
- a **round shield** of hide on a wicker or wooden frame, about 80 cm, with a bronze boss and a bound rim,
  held by a central grip and a strap;
- a **spear** of ash, about 2.2-2.5 m, with a leaf-shaped bronze head.

Civ3's original: a crested helmet with a civ crest, a bare torso with a civ sash, a short civ kilt, a large
round dark shield on the left arm, a spear of about 2.5 m in the right hand.

## The features that must read at 1x

1. The **spear held ready**: upright but tipped forward, the head well above the helmet, the right fist at
   chest height, the shaft clear of the body's centre line and of the shield.
2. The **round shield** on the left arm, upright and turned toward the viewer, its civ cross and bronze
   boss reading; a structured back (battens, a grip) when seen from behind.
3. The **crested helmet** (civ crest).
4. The bare torso and the short kilt.

## The pose

Weight on the right leg, the left foot a half step forward behind the shield; the torso upright, the
shoulders counter-rolled. The shield arm forward and across a little, the board covering the left half of
the body. The right fist forward at chest height on the shaft, the spear raised and tipped forward about 15
degrees, butt behind and below the right hip: ready to lower and thrust.

## Civ colour

Stock: the shipped Spearman 17-26 % (mean 21), Hoplite 19-29 (23), Warrior 12-17 (14); aim 15-30 % in every
direction. Regions: the **shield's cross**, the **crest**, and the **kilt** (the original's top/sash goes:
a sash over a bare chest is a strip, kept as dark leather).

## Stock neighbours

Hoplite and Warrior.

## Rounds

### Build rounds 1-2
- Bare torso, civ kilt, the spear raised and tipped forward, the round shield on the forearm with a battened back.
  Civ 12-21 % after the baldric was made civ (the original's sash).

### Critic round 1 (batch 1): 77 / 73.5 = 75.25
Both: the helmet is dark olive (reads as cloth); the spear trails low in directions 0-1; the shield small in 7.
Fixes: a bronze helmet, the fist higher and the spear tipped further forward, the shield higher.

### Critic round 2 (batch 1): 75 / 85 = 80
The fresh critic passed it (85). The continuity critic found the shield forearm coming out through the board's
lower face in 0-1 once the shield was raised (critical). Fix, shared: round shields held by the central grip
('shield_at' 'grip', the fist behind the board, 'grip_depth' -0.05) instead of on the forearm.

### Critic round 3 (batch 1): 78 / 84 = 81
The shield clipping is gone. Still: the inside of the shield bare planks; the shield face flat cream.

### Solo round (round 3's state, unchanged): 78 / 80 = 79

## Final (round 3)
Scores: R1 77 / 73.5 = 75.25; R2 75 / 85 = 80; **R3 78 / 84 = 81, final**; solo 78 / 80 = 79. No critical failure;
not 85. Civ 12-22 % (mean 17). Open points: a hide texture, a stitched rim and wear on the shield face, a grip, hide
rim and lacing inside it; muscle shading on the skin (the fresh critic read it as noisy); kilt folds; an Aegean
helmet shape (boar's-tusk bands) so it stops echoing the Hoplite; the spear low in direction 1.
