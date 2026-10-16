# Footmen batch A, round 6: fixes per unit

Axes: Imposing / Not-AI / Faithful / Readable 1x / Materials / Detail. PASS = all ≥7.
Scores are given as round 6 (round 5).
Each fix has a label: **data** means an entry change with exact keys; **feature** means new code is needed (named).
Fixes are ranked by how visible they are at 1x.

## Passes

### Bowman: 7/7/7/7/7/7 PASS (6/6/6/7/5/7)
The garment cuts now follow the physique. The red short sleeves cover the broad shoulders, the vest sits clean, and the cap knob, the quiver fletching and the loose arrow all read.
Polish: the loose arrow still angles down to the shin. Point it more forward: `hang_dir` for the arrow, or `props.arrow` length 0.4 (**data**).

### Gallic Swordsman: 7/7/8/7/7/7 PASS (7/7/7/7/7/6)
The spiked ginger hair completes his silhouette. The check trousers, the tinted forearm band and the brown shield with its silver boss all match the original.
Polish: make the spikes a little taller and more limed (pale tips) so they read at 1x (**feature** param).

## Fails

### Enslaved Worker: 6/6/7/7/6/6 (5/6/7/7/6/5)
The shackles read at the wrists and ankles, and the bow is visible.
1. **feature**: The spade blade has disappeared. Now only a pole goes into the ground; the blade sits below the ground or is edge-on. Lift the tool so the iron blade rests on the ground face-on, beside the right foot.
2. **feature**: The chain between the ankles doesn't show. Add a short sagging chain between the ankle shackles.
3. **feature**: The tunic is clean, although `grime` is 1.0. A slave's tunic needs dirt at the hem, sweat stains and a frayed hem; add `cloth_dirt` / `frayed`.
4. **data**: A sparser, more unkempt beard and hair would suit him: `beard_density` 450, and `mane` 0.15.

### Immortals: 7/6/7/7/6/7 (7/6/7/7/6/7)
The shoulders are covered now.
1. **feature**: The stripes are still not clean. On the thighs they swirl diagonally and break into streaks, because they follow the leg UVs. Project them in object space (vertical in Z), and turn off the grime and weave noise under the tinted pattern.
2. **feature**: The veil still reads as a white skull mask with eye holes. Cover only the mouth and chin, keep a separate brow cloth, and leave the eyes and nose open. This is the main not-AI blocker.

### Impi: 7/6/8/7/6/7 (7/6/8/7/6/7)
No visible change; no code touched him this round.
1. **feature**: The headdress still reads as a bundle of red sausages. Use thin tapered feathers with varied length, tilt and droop, and a rounder, fuller outline.
2. **feature**: The skin still reads maroon, even with `colors.skin` '#6a5040'. The skin shader or calm step pushes dark skin red. Check the shader on the darkest skins, and pull its hue toward neutral brown, not only its saturation.
