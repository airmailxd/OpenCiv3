# Footmen round 3: fixes per unit

Axes: Imposing / Not-AI / Faithful / Readable 1x / Materials / Detail. PASS = all ≥7.
Each line has a label: **data** means an entry change with the exact keys; **feature** means new code is needed (named).
Fixes are ranked by how visible they are at 1x.

## Batch A

### Berserk: 7/6/7/7/7/6 (last round 6/6/5/6/6/5)
1. **data**: The axe head is still small at 1x. `props.axe = ('battle_axe', {'length': 0.6, 'scale': 2.0})`. **feature**: a separate `head` scale on `battle_axe`, so the head can grow without the haft.
2. **data/feature**: The bright yellow beard covers the whole face like a mask. Set `colors.hair` to '#c8a830'. **feature**: `beard_length`/`beard_density` keys, so the beard can be shorter, show the face, and stay yellow.
3. **feature**: Add `braids`, two yellow braids down the front of the fur vest.
4. **feature**: Add rivets or a nasal bar on the `conical_helmet`, for detail.

### Bowman: 6/7/7/7/7/7 (last round 6/7/7/7/7/6)
1. **data**: The loose arrow is as long as a cane and reaches the ground. Set `props.arrow = ('arrow', {'length': 0.55})`.
2. **data**: He is still slight. Set `macro.weight` 0.8 and `upper_bulk` 1.35.
3. **feature**: Show arrow fletching in the quiver mouth.

### Chasquis Scout: 6/7/8/7/7/7 (last round 6/7/7/7/6/6)
1. **data**: He is still lanky next to the muscular original. Set `macro.weight` 0.8 and `upper_bulk` 1.35.
2. **data**: Add the gold skirt hem: `'hem_bands': [('bottom', 'gold', 0.04)]`.

### Enkidu Warrior: 6/7/7/7/7/6 (last round 5/6/5/5/5/3)
1. **feature**: The kilt is still cloth; the original's kilt is fur. Add a `fur_kilt`, or let `fur_vest` take a `kind='kilt'`.
2. **feature**: Add `long_beard` (black, to mid-chest); now the beard is short stubble.
3. **feature**: Add `streamers`, tinted ribbons on the headband.
4. **data**: Bulk him up: `upper_bulk` 1.35.

### Enslaved Worker: 5/6/5/6/6/4 (last round 5/6/5/6/6/4)
1. **data**: The spade is now held out diagonally to the side. The original stands it upright at his right foot. Set `'hang_dir': (-0.05, -0.1, -1.0)`.
2. **feature**: The tinted strap is still two red flecks. See the chest-strap bug under the shared problems.
3. **feature**: Add `shackles` on both wrists and an ankle chain.
4. **data**: The worker is meant to be gaunt. Raise `age` to 0.7 and set `grime` to 1.0.

### Explorer: 7/7/7/7/7/6 (last round 7/7/7/7/7/6)
1. **feature**: The rapier at the hip is now hard to see. Lengthen the hip `sheathed_sword` and add a visible guard.
2. **feature**: Add rivets and an edge roll on the breastplate, and a buckle on the sword belt, for detail.
3. **data**: The plume reads as a red block from the front. Try `'headgear_scale': 1.1` on `morion_big`, or curve the plume back.

### Gallic Swordsman: 7/7/6/7/7/5 (unchanged; the render looks stale)
1. **check**: The entry has `patterns.bottom` check, hair '#d07a30' and shield_face '#6a4a2e'. The render shows none of them: no checks, blond hair, a grey shield. Rebuild and confirm the stills are new.
2. **feature**: Add `spiked_hair` (limed spikes standing up), unless the rebuild fixes the hair.
3. **feature**: `plain_round` calls `shield_round(tint='none')` and drops `shield_face`. Pass the color through.
4. **data**: Add the small yellow belt pouch: `colors.pouch` '#c8a030'.

### Hoplite: 7/7/8/7/6/7 (last round 7/7/7/7/7/5)
1. **feature**: The aspis face renders as woven straw or wicker. Give it a bronze face material (`face_metal: True` on `shield_round`).
2. **feature**: The crest should be taller and longer from front to back.

### Immortals: 6/7/7/7/6/6 (last round 6/6/6/7/6/5)
1. **feature**: The white headcloth still covers the shoulders like a mantle. Stop the hood shell at the neck.
2. **feature**: Add tinted vertical trouser stripes: a `patterns` kind 'stripes'.
3. **data**: Pose: `pose_extra` to bring the free hand in and add a slight forward lean, so he reads as more aggressive.

### Impi: 7/6/7/7/6/6 (last round 7/6/6/7/6/5)
1. **feature**: The calf tufts are still white/black zebra wraps. `G.fur` wraps must honor tint ('wraps' is already in `tint`).
2. **feature**: The feather headdress is a regular cylinder of spikes, like a brush. Randomize feather length and tilt.
3. **data**: The skin is still dark maroon. Set `colors.skin` to '#6e4630'.

### Inquisitor: 6/7/7/7/7/6 (last round 6/7/6/6/7/4)
1. **feature**: The face is fully lit and exposed; the original's hood shadows it. A deeper hood or face shadow.
2. **feature**: Add `book_raised` (chest height) and a tinted book cover (`book(tint=True)`).
3. **data**: Widen the cuffs and shoulder band: `hem_bands` `('sleeves', 'tint', 0.09)` and `bands` `('neck', 'tint', 0.08)`.

## Batch B

### Javelin Thrower: 7/7/6/7/7/6 (last round 7/6/6/7/6/5)
1. **feature**: `planted_bundle`. The bundle is still carried flat across the hips and should stand upright at his left, taller than him. This is the biggest gap in faithfulness.
2. **data**: The dark red mask doesn't read. Set `veil_drop` to 0.24 and `colors.veil` to '#5a1a14'.

### Legionary: 8/7/8/7/7/7 PASS (last round 7/7/5/6/6/4)
1. **feature (polish)**: The scutum face has a glittery high-frequency grain at 2x. Lower the noise scale or contrast. The emblem in the middle reads as a smudge; make it bolder.

### Libyan Mercenary: 7/7/6/7/7/6 (last round 7/7/6/7/7/5)
1. **feature**: The sunburst emblem. The shield still shows a + cross; add an emblem kind 'star' on `shield_oval`.
2. **feature**: Scale texture on the cuirass, for detail.
3. **feature**: Cheek guards on the helmet (Attic).

### Longbowman: 7/7/6/7/7/6 (last round 7/7/6/7/6/5)
1. **data (regression)**: The arms are civ red now. The 'sleeved' tinted top covers the mail sleeves; the original has grey sleeves and a sleeveless red tunic. Set `'top': 'vest'` and drop `colors.sleeves`.
2. **feature**: `bow_upright_nocked`.
3. **feature**: A bigger pale rounded cap and a curling plume (`plume_curl`).
4. **feature**: The big tan hip arrow bag, as an `arrow_bag` prop through `hip_hold`/`back`.

### Medieval Infantry: 7/7/8/7/7/7 PASS (last round 7/7/7/7/7/6)
1. **data (polish)**: The sabatons read as white plastic. Set `colors.boots` to '#8a8c90'.

### Ninja: 5/6/6/6/6/5 (last round 5/6/6/5/5/4)
1. **feature**: The harness straps are still invisible; only the belt reads. See the chest-strap bug under the shared problems. He also needs a second strap so the straps form a V.
2. **data**: He is still a thin silhouette. Set `macro.muscle` 1.0, `weight` 0.6, `upper_bulk` 1.25, and `coat_flare` 1.35 so the jacket skirt shows.
3. **feature**: A sheathed short blade on the back (through `back`, any prop), as extra detail.

### Pikeman: 6/7/7/7/6/6 (last round 6/7/5/7/6/4)
1. **feature**: The breastplate is still a narrow bib, and the puffy sleeves run over the chest. Add `cuirass_width` (about 0.22).
2. **data**: The tassets are a small white flap. Set `coat_length` to 0.36, and `coat_metal` True (check it is set).
3. **data**: He is not imposing enough. Set `macro.muscle` 1.0 and `upper_bulk` 1.3.

### Samurai: 6/6/6/6/6/6 (last round 5/6/5/6/5/4)
1. **feature**: The legs are still skinny tights; add wide `hakama`.
2. **data**: The coat pattern is a fine glitter noise. Use a larger scale: `'patterns': {'top': ('camo', '#5a5a5a', 6.0)}`.
3. **feature**: Add the white under-robe V at the chest.
4. **feature**: The wide sleeves look like foam pads. Give them hanging, squared sleeve folds.

### Scout: 6/7/7/7/7/6 (last round 6/7/7/7/7/6)
1. **feature**: The leggings render cream, although `colors.wraps` is '#a8906a'. The `G.fur` wraps seem to pale the color.
2. **feature**: The tinted strap is invisible (the chest-strap bug); also add the small chest pack.
3. **data**: Set `hang_dir`/`pose_extra` for a slight forward hunch: `('spine_02', (0, 0.15, 1))`.

### Swiss Mercenary: 7/7/7/7/7/6 (last round 7/7/6/7/6/5)
1. **feature**: The sash is a thin dark line, although `width` is 0.15 and it is tinted. See the chest-strap bug under the shared problems.
2. **feature**: Black slashes on the breeches and sleeves: a `patterns` kind 'slashes' or 'stripes'.
3. **data**: Make the plume larger (`headgear_scale`, or a plume size key if one exists).

### Jaguar Warrior: 7/7/7/7/6/7 (last round 6/6/5/6/5/5)
1. **feature**: The `wolf` helmet still renders as a striped tan ball. Add `jaguar_head` with the cat's face over the brow.
2. **feature**: The feather spray is white/red. It should be the civ color; add a `headgear_tint` key that tints the headgear without tinting the pelt helmet.
3. **data**: Lighten the leg wraps so the spots read: `colors.wraps` '#c87436'.
