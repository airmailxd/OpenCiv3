# Footmen batch A, round 8: fixes per unit

Axes: Imposing / Not-AI / Faithful / Readable 1x / Materials / Detail. PASS = all ≥7.
Scores are given as round 8 (round 7).
Each fix has a label: **data** means an entry change with exact keys; **feature** means new code is needed (named).

### Enslaved Worker: 6/7/7/7/7/7 (6/6/6/7/7/7). Fails on imposing only
The spade now stands upright beside the foot with the blade face-on at the ground, in all 8 directions. That fixes faithfulness. The twill and dirt make the tunic read as worn rags, and the shackles, strap and long hair all read.
1. **data**: The original is a sturdy, broad labourer standing at rest. The render is gaunt and bowed; the bow was my own round-5 suggestion, and the original does not have it. Remove the bowed `pose_extra` (spine_02/spine_03/neck_01 -0.3) and give him the original's build: `macro.muscle` 0.95, `weight` 0.65, `upper_bulk` 1.2, and `physique` at about 0.5. Keep `cloth_dirt`, the shackles and the grime; they carry the "enslaved" story without the slump.
2. **feature (optional)**: Add a short chain between the ankle shackles.

### Impi: 7/6/8/7/7/7 (7/6/8/7/6/7). Fails on not-AI only
Skin, measured as the mean of the skin pixels in the bottom row of the 2x image:
| Render | RGB | Hue | Saturation |
|---|---|---|---|
| Round 7 | (62, 40, 34) | 12° | 0.29 |
| Round 8 | (62, 42, 37) | 13° | 0.26 |
| Original (top row) | (65, 50, 42) | 21° | 0.21 |

The desaturation helped, and materials goes to 7. The skin is still about 8° redder than the original.
1. **feature**: The headdress still reads as a bundle of red sausages or fingers. Every feather is a round, blunt, equal-thickness tube. This is the last blocker. Use flat, tapered feather cards (or a fluffy strand/fur coat on the crown, like `G.fur` with long strands), with varied length and droop and a soft, rounded outer silhouette. Shade darker toward the roots.
2. **data/feature (polish)**: Nudge the skin hue toward 20°. Either set `colors.skin` to '#5e4232', or have the dark-skin hue pull target about 20-22° instead of its current result.
