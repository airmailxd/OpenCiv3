# Musketeer: realism design (PC2, realism/infantry)

## The real-world model
A cavalier-era musketeer, about 1630: a broad felt hat with an ostrich plume, long hair, moustache and goatee; a buff coat with skirts, a white falling collar; a wide baldric and a sash; a rapier at the hip; dark breeches, tall bucket boots; the musket.

## The features that must read at 1x
1. the broad hat and big plume; 2. the musket held at the port across the chest; 3. the baldric and sash; 4. the long buff coat over bucket boots.

## The pose
Upright, the musket diagonal at the port (muzzle up at the left shoulder), the rapier hanging at the left hip.

## Civ colour
Civ on the plume, the wide baldric, the sash and the cuffs. The shipped unit is 16 %. Target: 15-30 % in every direction (foot stock 9-34 %), in 2-3 painted regions.

## Stock neighbours
Musketman and Rifleman

## Rounds
### Build rounds 1-3
- R1: a buff coat with skirts, a white falling collar, a wide civ baldric and sash, a rapier at the hip, the musket at the port (it was held upright before, its bright bell reading as a mace): civ 12 %.
- R2: a wider sash and baldric and civ cuffs: 15 %.
- R3: the plume's frond ends thicker (`tip` 0.014: thin ends broke into specks); the hat higher (`brow_drop` 0.065), a lighter skin and a moustache only, because the face under the brim read as a black blob. Clean; civ 13-18 %.

### Critic batch 1: 72.5 / 82 = 77.25 (A: face critical)

### Round 2 (fixes)
- Face lit under the brim (`face_fill` 16, the hat higher, a lighter skin); a thicker musket (`stock_w` 1.5, `barrel_w` 2.0) angled off the body; a shorter cup-hilt rapier (0.78 m); cropped hair and a broader white collar to break the hat and hair from behind; flat civ cuffs. Civ 13-24 %, mean 17.5 %.

### Critic batch 2: 80 / 86 = 83.0

### Round 3 (the last; fixes)
- The plume cut about 25 % (1.0) with thicker frond ends; the face fill down to 9 W and a fuller dark moustache. Civ 11-19 %, mean 14 %.

### Critic batch 3: 83 / 86 = 84.5

## FINAL: R3 84.5

Rounds: R1 77.25, R2 83.0, **R3 84.5**. Cap reached (3 critic rounds). Pictures: reviews/realism/musketeer/final(_red).png (shipped, final, two stock neighbours); the finals gate re-renders it at HEAD.

Later pass:
- a pale patch on the face in dirs 6-7
