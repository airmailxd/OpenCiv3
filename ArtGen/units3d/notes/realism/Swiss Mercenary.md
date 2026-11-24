# Swiss Mercenary: realism design (2026-11-23)

## The real-world model

A Swiss pikeman (a Reisläufer) of about 1500-1525 (Novara, Marignano, Bicocca; Urs Graf's and Niklaus Manuel's
drawings):

- the **pike** of about 5 m (shortened in Civ's frame), a small head with long langets;
- a **morion** or kettle hat with feathers;
- a **breastplate** (often), a doublet with **slashed and puffed sleeves**, **slashed hose** or breeches;
- a sword or a Swiss dagger at the belt.

Civ3's original: a steel morion with a civ plume, a broad civ sash from shoulder to hip, puffy slashed pale
sleeves, baggy red breeches slashed black, black shoes, a brown beard, the long pike upright with a civ shaft.

## The features that must read at 1x

1. The **pike ported in both hands**, ready to lower, the head far above the morion.
2. The **civ plume on the steel morion**.
3. The **broad civ sash** across a pale puffed doublet.
4. The **red-and-black slashed breeches**.

## The pose

As the Pikeman's: weight on the right leg; both fists on the shaft before the chest, the right below the left;
the pike upright and tipped forward a little; the torso upright.

## Civ colour

Stock: the shipped Swiss Mercenary 14-20 % (mean 16). Aim 15-30 % in every direction. Regions: the **plume**,
the **sash**, the **pike shaft**.

## Stock neighbours

Pikeman and Musketman.

## Rounds

### Build rounds 1-2
- The pike first ported in both fists: the arms crossed the body (the Pikeman's round-1 failure); now one fist out at
  his right with the pike leaning out, the left arm relaxed (the Pikeman's round-3 fix).

### Critic round 1 (batch 3): 60.5 / 57.5 = 59
Both: the pike short (reads as a spear) with a whole civ shaft; the arm held straight out like a T-pose; zebra
sleeves (the build round's madder slashes still read as stripes); taller than the stock Pikeman (the shipped Swiss is
as tall: the size check gives area x1.01). Fixes: a wooden shaft, 3.6 m; the fist in at the chest with the elbow bent;
slimmer sleeves (upper arm only); a wider sash.

### Critic round 2 (batch 3): 70.5 / 64 = 67.25
Landed: the wooden pike reads as a pike; slimmer sleeves; civ 21-29 % on the sash and breeches. Both: the arm still
at full stretch down to the pike in 1, 2 and 8; big and bright beside the stock Pikeman. Fixes: the fist up at the
chest (hand_r_at z -0.22, elbow 0.75); the sleeves and doublet toned down. Not scaled down: the area is already
x0.965 of the shipped Swiss.

### Critic round 3 (batch 3): 71.5 / 63 = 67.25
Landed: the sleeves toned to cream. Partly: the elbow bends in 1-2 but the fist sits at the hip; 8 still at arm's
length. Both: too big beside the stock Pikeman (the fresh critic calls it critical, out of house style).

## Final: round 3, 67.25 (ties round 2; kept for the continuity critic's higher score)
Civ 20-29 % (mean 25). Size: area x0.96 of the shipped Swiss, which is itself the size the critics object to;
scaling 15-20 % down would break the size check (0.90-1.10 of the shipped), so it is reported, not changed: the
lead's call. Open points: the fist at the chest with the pike grounded by the foot (1, 2, 8), a two-handed port;
a less pinstriped slash on the breeches.

Animations re-rendered from the final still (build.py --anim, 2026-11-23) and checked at 2x (directions 1, 3, 6): ATTACK1, FORTIFY, RUN and DEATH play cleanly; in direction 1 the thrust or cut points at the camera, so it reads foreshortened.
