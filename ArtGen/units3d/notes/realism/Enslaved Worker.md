# Enslaved Worker: realism design (2026-11-23)

## The real-world model

A labourer in bondage of the ancient world (the gang slaves of Roman mines and estates, the ergastulum's
chained workers): broader and older than the free Worker, in a coarse sleeveless tunic gone grey-green with
wear, with **iron shackles** on the wrists and ankles.

- a **spade** of the period, iron-shod, resting with its blade on the ground at his right foot;
- a coarse, stained wool **tunic** to above the knee, a rope belt;
- **iron shackles** at both wrists and both ankles;
- long, unkempt hair and beard; worn sandals.

Civ3's original: a sturdy labourer at rest, long hair and beard, a sleeveless olive tunic, one civ strap
across the chest, a spade.

## The features that must read at 1x

1. The **spade**, its blade clear of the legs with a bright edge (upright beside him, the right hand on the
   haft at chest height): it must read in all 8 directions.
2. The **iron shackles** at wrists and ankles, dark and thick enough to read at 2x.
3. The pale **tunic** against the skin and dark hair: the Worker's opposite in value (dressed, not bare).

## The pose

At rest, the spade upright at his right side, the blade planted by his right foot, the right fist round the
haft at chest height; the left arm hanging, shackled. Upright torso, the head a little down (tired, not
slumped: no hunch).

## Civ colour

Stock: the shipped Enslaved Worker 2-5 % (mean 3): far below the foot range (9-34 %), so it does not tell
blue from red at 1x. Aim about 8-15 %: the **strap** across the chest (as the original) and a **cloth band**
(the tunic's belt, or a rag round the head), so it reads from behind as well.

## Stock neighbours

Worker and Settler.

## Rounds

### Build rounds 1-2
- R1: the spade upright at his right, blade planted by the foot (the 'ready' stance, the haft down through the fist);
  civ strap, belt and headband: 6-11 % (the shipped 2-5).
- R2: props.spade, a brighter iron.

### Critic round 1 (batch civil 1): 71 / 68 = 69.5
The fresh critic: civ colour too weak in 3 and 6 (critical). Both: the blade edge-on in 0 and 4 (a walking stick) or a
dark lump (a mace); the irons lost at 1x. Fixes: a civ hem on the tunic, a wider strap and belt; the spade leaning
out with its face tipped up; a chain between the ankle irons (units.equip 'ankle_chain', new, off by default).

### Critic round 2 (batch civil 1): 75 / 67 = 71
Landed: civ colour (10-17 %, readable from behind), the blade face in 1-5, the ankle chain at 2x. Still: the spade
out on a straight arm (a cane, a broom), the stand-out of the batch at 1x; the blade black in 7 (the mirror iron
caught the sky); the irons lost at 1x. Fixes: the spade upright against the leg, the elbow bent; the blade's face in
a matte forged iron (props 'forged_iron', new); thicker irons (garments.shackles 'thick', units 'shackle_thick').

### Critic round 3 (batch civil 1): 81 / 71 = 76
Landed: the spade against the leg (1-3, 5-7), the matte iron, the irons and chain. Still: the arm out in 0 and 4;
thin, long forearms; the hair a black cape from behind (the fresh critic, who also read the civ colour at about
5 %; it measures 11-17 %, mean 14).

## Final (round 3)
Scores: R1 71 / 68 = 69.5; R2 75 / 67 = 71; **R3 81 / 71 = 76, final**. Cap of three batch rounds; no critical
failure (the round-1 civ-colour failure fixed: 11-17 %, mean 14). Open points: the spade arm out in directions 0
and 4; thin, long forearms; the long hair a black cape from behind (shorter, in locks, so the strap shows).

## Animations
Drawn with the Worker's actions (WORKER_LIKE): 'still_default' keeps the planted-spade still for DEFAULT, and
'anim_tool' gives the actions the spade. Checked at 2x: run, mine (Build), road (Shovel), death, capture.
