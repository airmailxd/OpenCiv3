# Footmen batch A, round 4: fixes per unit

Axes: Imposing / Not-AI / Faithful / Readable 1x / Materials / Detail. PASS = all ≥7.
Scores are given as round 4 (round 3).
Each fix has a label: **data** means an entry change with the exact keys; **feature** means new code is needed (named).
Fixes are ranked by how visible they are at 1x.

## Passes

### Berserk: 7/7/8/7/7/7 PASS (7/6/7/7/7/6)
The big axe head, the two-handed grip, the fur vest, the riveted conical helm and the shorter beard all read now.
Polish:
1. **feature**: `braids`, two yellow braids down the front of the vest. They are in the original.
2. **data**: The beard still bands across the face at 1x. Set `beard_density` to 450.

### Explorer: 7/7/7/7/7/7 PASS (7/7/7/7/7/6)
The rapier guard at the hip adds the missing detail.
Polish:
1. **data**: The face is a dark mask. Set `'beard': False`, or `colors.hair` to '#5a4430'.
2. **feature**: The plume reads as a red blob from the front. Thin it and curve it back.

## Fails

### Bowman: 6/7/7/7/7/7 (6/7/7/7/7/7)
1. **feature**: Raising `weight` to 0.8 and `upper_bulk` to 1.35 made no visible change. The torso is still narrow. Add a `shoulder_width` key, or make `upper_bulk` actually widen the chest and shoulders.
2. **data**: Use a prouder stance: `'pose_extra': [('spine_02', (0, -0.1, 1)), ('thigh_l', (0.22, -0.05, -1)), ('thigh_r', (-0.22, 0, -1))]`, which puts the chest up and the feet wider.
3. **data**: The loose arrow still runs from the hand down to the ankle. Shorten it to `props.arrow` length 0.45, angled forward.

### Chasquis Scout: 6/7/8/7/7/7 (6/7/8/7/7/7)
1. **feature**: Same as Bowman: `weight` 0.8 and `upper_bulk` 1.35 changed nothing visible. He is still lanky. Use `shoulder_width` or a working `upper_bulk`.
2. **data**: The arms hang slack. Use a wider, planted stance with `pose_extra` on the thighs: (±0.25, -0.05, -1).
3. **data**: The gold skirt hem doesn't read. Use `'hem_bands': [('bottom', 'gold', 0.05)]`.

### Enkidu Warrior: 6/6/6/7/7/7 (6/7/7/7/7/6)
1. **data**: He reads as a fur onesie: the fur kilt runs into the fur calf wraps, so he is fur from neck to ankle. The original has bare shins and pale ankle wraps. Set `'wraps': None` and add `('ankle', 'wraps')` to `bands` with `colors.wraps` '#d8ccb0'.
2. **data**: The waist and knee bands are buried in the fur. Add a width: `('waist', 'tint', 0.07)` and `('knee', 'tint', 0.06)`.
3. **feature**: The fur kilt needs a hem edge or a length key (`fur_kilt_length`), so it ends above the knee as in the original.
4. **feature**: `streamers`, tinted ribbons on the headband.

### Enslaved Worker: 5/6/7/7/6/5 (5/6/5/6/6/4)
The planted spade and the readable strap fixed the faithfulness.
1. **feature**: Add `shackles` on the wrists and the ankle chain. They are the unit's identity and still missing.
2. **feature**: The spade blade at the ground is edge-on, a thin T. Turn it so the blade face shows.
3. **data**: The tunic is too clean for a slave. Set `grime` to 1.0 (and check that `age` 0.7 applied).
4. **data**: Imposing is not the point for this unit, but give it a bowed, weary posture: `pose_extra` `('spine_02', (0, 0.2, 1))`, `('neck_01', (0, 0.3, 1))`.

### Gallic Swordsman: 7/7/7/7/7/6 (7/7/6/7/7/5)
The check pattern now renders.
1. **feature**: The hair still renders dull blond, although `colors.hair` is '#d07a30'. The `short04` asset is still toned pale, so check the hair-color fix on this asset. He also needs `spiked_hair`; it is his signature.
2. **data**: The shield reads grey-brown. Set `colors.shield_face` to '#7a4a24' (warmer).
3. **data**: Add the yellow belt pouch: `colors.pouch` '#c8a030'. Check that the pouch is enabled.

### Hoplite: 7/7/8/7/6/7 (7/7/8/7/6/7)
1. **feature**: `face_metal: True` is set, but the aspis still renders as matte woven tan. Check whether the 'bronze' material is reaching the face. The face uses `keys=_round_device(...)`; check that the device layer doesn't override the base material.
2. **feature**: Make the crest taller and deeper from front to back.

### Immortals: 6/6/7/7/7/7 (6/7/7/7/6/6)
The hood stopping at the neck and the stripes are both progress.
1. **feature**: The stripes are inverted. The trousers now read as civ red with brown stripes; the original has brown trousers with civ-color stripes. The `stripes` pattern needs a tinted stripe color on an untinted base (for example `('stripes', 'tint', 6.0)` with the base `colors.bottom`).
2. **feature**: The veil reads as a white skull mask with eye holes. Lower it to the mouth and chin so the eyes and nose show, as in the original.
3. **data**: Use a more aggressive stance: `pose_extra` `('spine_02', (0, 0.1, 1))` for a forward lean, and `macro.weight` 0.85.

### Impi: 7/6/8/7/6/7 (7/6/7/7/6/6)
The tinted calf tufts fixed the faithfulness.
1. **feature**: The headdress reads as a bundle of red sausages or fingers. Give the feathers thin tapered tips, varied length and tilt, and some droop.
2. **data**: The skin still reads maroon. Set `colors.skin` to '#6e4a34' (less red).
3. **feature**: The hide straps and ornaments on the chest are barely visible. Lighten `chest_strap` to '#6a4a30' (data), or add a hide ornament (feature).

### Inquisitor: 6/7/7/7/7/7 (6/7/7/7/7/6)
The civ-color book and the cuffs read now.
1. **feature**: The face is still fully lit. The original's hood keeps the face in shadow; make the hood deeper or add a face shadow. The weak front fill makes this worse.
2. **data**: He slumps. Set `macro.height` to 0.8 and use `pose_extra` `('spine_02', (0, -0.1, 1))`, `('head', (0, 0.6, 1))` for a solemn, upright posture.
3. **feature**: `book_raised`: hold the book at chest-to-head height, as in the original.
