# Footmen batch A, round 5: fixes per unit

Axes: Imposing / Not-AI / Faithful / Readable 1x / Materials / Detail. PASS = all ≥7.
Scores are given as round 5 (round 4).
Each fix has a label: **data** means an entry change with exact keys; **feature** means new code is needed (named).
Fixes are ranked by how visible they are at 1x. Berserk and Explorer passed in round 4 and were not re-rendered.

## Passes

### Chasquis Scout: 7/7/8/7/7/7 PASS (6/7/8/7/7/7)
`physique` gave him real breadth.
Polish: the gold skirt hem still barely reads. Use `'hem_bands': [('bottom', 'gold', 0.06)]` (**data**).

### Enkidu Warrior: 7/7/7/7/7/7 PASS (6/6/6/7/7/7)
The shorter fur kilt, bare shins, pale ankle wraps and red knee bands now match the original.
Polish: the headband `streamers` (**feature**).

### Hoplite: 7/7/8/7/7/7 PASS (7/7/8/7/6/7)
The polished bronze aspis face fixed the materials score.
Polish: a taller crest that is deeper from front to back (**feature**).

### Inquisitor: 7/7/7/7/7/7 PASS (6/7/7/7/7/7)
The broader, upright build reads as solemn and imposing.
Polish: the face is still fully lit, while the original's hood shadows it; add a face shadow inside the hood. Also add `book_raised` (**feature**, both).

## Fails

### Bowman: 6/6/6/7/5/7 (6/7/7/7/7/7). Regression
1. **feature (bug)**: The clothes don't fit the `physique` body. The shoulders are now bare skin, with red tunic and sleeve patches poking through, and the leather vest is torn at the shoulders. You can see it in every direction. The garment shells seem to be built on, or offset from, the mesh before the physique change, or the shell offset is too small for the wider shoulders. Build the shells after the physique targets are applied, or raise the shell `offset` over the shoulders.
2. **data**: Until that is fixed, lower the physique strength on this unit (shoulder-width target at about 0.5), so the short sleeves cover the shoulders again.
3. **data**: The loose arrow still reaches the ankle. Set `props.arrow` length to 0.4.

### Enslaved Worker: 5/6/7/7/6/5 (5/6/7/7/6/5)
1. **check**: The bow from the `pose_extra` doesn't show; he still stands straight. Check that the corrected (-y forward) spine and neck values are applied, for example `('spine_02', (0, -0.2, 1))` and `('neck_01', (0, -0.3, 1))`.
2. **feature**: `shackles` on both wrists, and the ankle chain. They are the unit's identity and still missing.
3. **feature**: The spade blade at the ground is edge-on, a thin T. Turn it so the blade face shows.
4. **data**: `grime` 1.0 is still not visible; the tunic is clean. Add a dirt gradient at the hem, or a `cloth_dirt` key (**feature**).

### Gallic Swordsman: 7/7/7/7/7/6 (7/7/7/7/7/6)
The ginger hair and the brown shield are fixed.
1. **feature**: `spiked_hair`, limed spikes standing straight up. It is the unit's signature silhouette in the original, and the only thing keeping detail at 6.
2. **data**: Make the yellow pouch more visible: `colors.pouch` '#d8b030'. Add buckles or lacing on the boots (**feature**).

### Immortals: 7/6/7/7/6/7 (6/6/7/7/7/7)
`physique` brought imposing up to 7.
1. **feature**: The veil still reads as a white skull mask with dark eye holes. Lower it to cover only the mouth and chin; keep the brow cloth separate and leave the eyes and nose open.
2. **data/feature**: The trouser stripes read as speckled red marbling, not clean vertical stripes. Use a lower-frequency pattern: `('stripes', 'tint', 4.0)`. Add a 'stripes' mode with hard edges and no noise (**feature**).
3. **check**: The shoulders show skin between the sleeves and the neckline. This is the same garment-fit problem as Bowman.

### Impi: 7/6/8/7/6/7 (7/6/8/7/6/7)
No visible change this round.
1. **feature**: The headdress still reads as a bundle of red sausages or fingers. Use thin tapered feathers with varied length, tilt and droop; a fuller, rounder outline; and some darker civ shading inside.
2. **data**: The skin still reads maroon. Set `colors.skin` to '#6e4a34' and check it took effect.
3. **data**: Lighten the hide straps so they read on the dark skin: `chest_strap` color '#6a4a30'.
