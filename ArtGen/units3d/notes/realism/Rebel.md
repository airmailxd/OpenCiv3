# Rebel: realism design (numpy-foot, PC2)

## What it is

A townsman of the 1956 Budapest uprising (the NumPy 'Rebel'). The NumPy sprite and its catalog entry were read for identity only; the build is the units3d realism look.
Entry: `npf_units.ENTRIES['Rebel']`.

## The features that must read at 1x

1. a lit Molotov cocktail in his left hand
2. an old double-barrelled shotgun low in his right
3. a flat cap, a dark jacket, the tricolour patch
4. a bag of bottles at the hip

## Pose

Upright torso (no hunch), the weight on the right leg (human.posture's defaults for the stance), the weapon held
where it reads, off the body's centre line.

## Civ colour

the waistcoat and the neckerchief. Target 10-20 % by direction, near the shipped NumPy share and within the stock foot range (9-34 %).

## Rounds

### Build rounds (own looking, before critic batch b1)

- r1: the identity reads (flat cap, civ waistcoat, shotgun low, a lit bottle in the left hand); pixels 0.78.
- r2: fit 1.0 (pixels 1.00); the shotgun read as a long stick, the flame as a pale bulb.
- r3: a shorter shotgun (scale 1.0), a bigger orange flame, a brighter green bottle, lighter skin, a wider
  neckerchief; tint 18.3 % (9.2-29.3; shipped 12.8).

### Batch b1 (with a1's shared lessons)

The stock build; the waistcoat dark, civ colour on the neckerchief and armbands (tint 11.6 %, 9.4-12.5); 1.01 against the stock Rifleman.

### Critic round b1: 62.5 / 70.3 = 66.4 (A: civ reads as shoulder pads)

Fixes for b2: the civ colour on a flat armband patch and the neckerchief; the shotgun thicker with a wooden fore-end, carried diagonally across the body; the jacket darker than the trousers, a belt; the Molotov's flame orange with a yellow heart, a bigger rag.

### Round b2: the civ neckerchief and an armband patch; a thicker shotgun with a wooden fore-end across the body; a dark jacket and belt; the Molotov's orange flame with a yellow heart. Tint 4.4 %; 1.24 vs NumPy, 1.01 vs Rifleman; clean.

Civ colour raised before the critics saw the round (coordinator: at least ~10 % per direction, accents larger but flat): final tint 7.9 % (min 7.0).

### Critic round b3: 67.5 / 72 = 69.75 (below B2's 70.65)

**FINAL = B2** (scoped restore of its own entry to the b2 commit d0d74a3a: the flat cap's brow band as band_at='brow', the B2 Molotov; verified against the b2 sheet). reviews/realism/rebel/final(_red).png. The b3 values, kept for a later pass: the neckerchief 0.07, one flat armband patch (patch_out 0.016) and no upper-arm bands, the cap's crown in civ, the shotgun side-on (weapon_dir (0.75, -0.3, 0.6), edge_dir (-0.6, 0, 0.8)), the Molotov's glowing petrol and bigger flame.

Later pass:
- the shotgun is missing in dir 1 and vertical in dir 5
- a smaller Molotov flame, saturated orange with a yellow tip
- civ colour on a cap band only
- some texture on the jacket
