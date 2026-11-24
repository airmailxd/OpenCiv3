# Musketman: realism design (PC2, realism/infantry)

## The real-world model
A matchlock musketeer of the 1630s-40s (Thirty Years War, English Civil War): a steel morion, a buff-leather coat over a russet doublet, puffed sleeves, breeches, grey stockings and low boots; the 'twelve apostles' (powder charges) on a bandolier; a heavy matchlock and its forked rest.

## The features that must read at 1x
1. the long matchlock sloped on the right shoulder; 2. the forked rest planted upright in the left hand; 3. the comb morion with its plume; 4. the pale buff coat and the white apostles across it.

## The pose
Upright, the weight on the right leg (the default posture), the musket on the right shoulder (shoulder_arms), the rest in the left fist.

## Civ colour
Civ on the puffed sleeves, the plume and a waist sash (the field sign of the 1640s). The shipped unit is 52 % civ (the whole suit). Target: 15-30 % in every direction (foot stock 9-34 %), in 2-3 painted regions.

## Stock neighbours
Pikeman and Musketeer

## Rounds
### Build rounds 1-5 (my own looking, before the critics)
- R1: a buff coat (the cuirass as leather, its edges bound) over a russet doublet; civ puffed sleeves and breeches: civ 37 %, too much.
- R2: the breeches dark, a civ sash at the waist: 13 % (the sash hides under the coat's skirt).
- R3-4: the puffed sleeves left slivers past the coat's armholes (specks); plain civ doublet sleeves instead: 17-27 % by direction, mean 23 %. Darker, thicker barrel (`barrel_w` 1.6).
- R5-6: the last specks were the forked rest's thin iron horns beside the left shoulder: new `musket_rest` options
  `fork_w` 2.2 and `shaft_w` 1.4, so the rest reads at 1x. Clean; civ 16-26 %, mean 22 %.

### Critic batch 1: 69 / 86 = 77.5 (A: face critical)

### Round 2 (fixes)
- Face lit under the morion (new `face_fill` 35 W, the morion higher, moustache only, a lighter skin); the rest longer (1.62 m) with a wide Y (`fork_spread` 1.8); civ moved off the whole sleeves to the plume, flat cuffs, a sash over the coat and a field-sign sash across it. Civ 7-17 %, mean 12 %.

### Critic batch 2: 75 / 82.5 = 78.75

### Round 3 (the last; fixes)
- The buff coat darker and desaturated (#7a6446) so it no longer reads as bare skin, its edges bound dark, pocket flaps, a dark leather collar; the face fill down to 12 W (35 made a white mask) with a normal skin; wider cuffs (0.1) and sash (0.16). Civ 7-18 %, mean 12 %.

### Critic batch 3: 80 / 85 = 82.5

## FINAL: R3 82.5

Rounds: R1 77.5, R2 78.75, **R3 82.5**. Cap reached (3 critic rounds). Pictures: reviews/realism/musketman/final(_red).png (shipped, final, two stock neighbours); the finals gate re-renders it at HEAD.

Later pass:
- a dark band across the eyes in dirs 0-2
- one muddy brown overall
