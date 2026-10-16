# Footmen batch A, round 9

Axes: Imposing / Not-AI / Faithful / Readable 1x / Materials / Detail. PASS = all ≥7.
Scores are given as round 9 (round 8).

### Enslaved Worker: 7/7/7/7/7/7 PASS (6/7/7/7/7/7)
With the bowed pose gone and the sturdier build, he now stands broad at rest, as in the original. The upright spade, shackles, strap, long hair and dirty twill tunic all hold.
Polish (**data**): the head still tips down slightly. Use `pose_extra` `('head', (0, 0.4, 1))` for a level gaze, like the original.

### Impi: 7/6/8/7/7/7 (7/6/8/7/7/7). Fails on not-AI only; waiting on the PC's feather cards
Skin, measured as the mean of the skin pixels in the bottom row of the 2x image:
| Render | RGB | Hue | Saturation |
|---|---|---|---|
| Round 8 | (62, 42, 37) | 13° | 0.26 |
| Round 9 | (60, 41, 35) | 13° | 0.26 |
| Original | (65, 50, 42) | 21° | 0.21 |

1. **feature**: The headdress is unchanged and still reads as blunt red tubes ("sausages"). The tapered feather cards should clear this.
2. **feature (check)**: `colors.skin` '#5e4232' (hue 22°) made no measurable change; the result is still hue 13°. Something after the swatch is controlling the final hue: the calm step, the dark-skin texture, or the red SSS. Trace it, so that `colors.skin` actually sets the rendered hue. This is polish; it does not block the pass.
