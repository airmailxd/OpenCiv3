# Footmen batch A, round 7: fixes per unit

Axes: Imposing / Not-AI / Faithful / Readable 1x / Materials / Detail. PASS = all ≥7.
Scores are given as round 7 (round 6).
Each fix has a label: **data** means an entry change with exact keys; **feature** means new code is needed (named).

## Passes

### Immortals: 7/7/7/7/7/7 PASS (7/6/7/7/6/7)
The cream mouth-and-chin veil leaves the eyes and nose open, and the skull-mask look is gone. The stripes are now vertical and read as brown trousers with civ-color stripes at 1x.
Polish (**feature**): at 2x the stripes still carry red flecks along their edges. Turn off the grime and weave noise under the tinted pattern for fully clean bands.

## Fails

### Enslaved Worker: 6/6/6/7/7/7 (6/6/7/7/6/6)
The dirty cloth, the fuller hair and the shackles raised materials and detail to 7. The blade is visible again.
1. **data (regression)**: The spade is now held out on a long diagonal and trails across the ground in every direction, like round 3. The original stands it upright beside the right foot. Set `'hang_dir': (-0.1, -0.15, -1.0)` and `props.shovel` length to 1.15, so that it is vertical and the blade rests on the ground at the foot instead of sinking below it.
2. **feature**: If the blade still sinks with a vertical `hang_dir`, add a ground clamp for hung tools: slide the grip up the haft until the working end sits at z ≥ 0.
3. **feature**: Add a short chain between the ankle shackles.

### Impi: 7/6/8/7/6/7 (7/6/8/7/6/7)
There is no visible difference from round 6 (the render is fresh).
1. **feature**: The skin is still maroon-red at 2x and 1x, so the hue pull toward neutral brown is too weak to see. Pull harder on dark skins (target about hue 25-30°, saturation ≤ 0.35), and check it reaches the base mesh material, not only the swatch.
2. **feature**: The headdress still reads as a bundle of red sausages. This is the main not-AI blocker. Use tapered, flattened feather cards with varied length, tilt and droop, a rounder, fuller outline, and darker civ shading at the roots.
