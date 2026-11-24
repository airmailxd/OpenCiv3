# Militia: realism design (numpy-foot, PC2)

## What it is

A French line fusilier of 1806-12 (the NumPy 'Militia', the mod's 'NapoleonicInfantry'). The NumPy sprite and its catalog entry were read for identity only; the build is the units3d realism look.
Entry: `npf_units.ENTRIES['Militia']`.

## The features that must read at 1x

1. the tall 1806 shako with its brass plate and red pompom
2. the musket with its bayonet on the shoulder
3. the white crossbelts on the civ habit-veste
4. white breeches, black gaiters, the knapsack

## Pose

Upright torso (no hunch), the weight on the right leg (human.posture's defaults for the stance), the weapon held
where it reads, off the body's centre line.

## Civ colour

the habit-veste (coat and its skirts), broken by the white crossbelts and red facings. Target 10-20 % by direction, near the shipped NumPy share and within the stock foot range (9-34 %).

## Rounds

### Build rounds (own looking, before critic batch b1)

- r1: reads as a Napoleonic fusilier: shako with brass plate and red pompom, musket and bayonet on the shoulder,
  white crossbelts on the civ habit, white breeches, black gaiters, knapsack. Tint 27.3 % (shipped 30.2);
  pixels 1.11. Next: fit 0.84, lighter skin under the shako's peak.

### Batch b1 (with a1's shared lessons)

The stock build; the habit a dark blue-grey, civ colour on the collar, epaulettes, cuffs, turnbacks and pompom (tint 15.1 %, 13.6-17.9); 1.02 against the stock Rifleman; the musket thicker, the bayonet a round spike with a rounded point (thin blades split into specks).

### Critic round b1: 75.5 / 77 = 76.25 (no critical)

Fixes for b2: the shako 40 % taller, black, flat-topped, a bigger brass plate; the habit lighter blue-grey with longer tails; civ on collar and cuffs; the musket near upright on the shoulder.

### Round b2: a tall black flat-topped shako with a big brass plate and pompom; a lighter blue-grey habit with longer tails; civ collar, cuffs and turnbacks; the musket near upright at the shoulder. Tint 8.8 %; 1.15 vs NumPy, 1.00 vs Rifleman; clean.

Civ colour raised before the critics saw the round (coordinator: at least ~10 % per direction, accents larger but flat): final tint 9.8 % (min 8.2).

### Critic round b3: 81 / 80 = 80.5 (B: a new critical: the fixed Napoleonic-blue coat reads as a blue player's unit on the red sheet)

**FINAL = B2** (80.75, no critical; scoped restore of its own entry to the b2 commit d0d74a3a and the B2 shako top; verified against the b2 sheet). reviews/realism/militia/final(_red).png. The b3 values, kept for a later pass: the coat '#24346e', the white plastron (npf_plastron), coat_length 0.62, turnbacks 0.14, a black shako top, brow_drop 0.055, skin '#d0a884'.

Later pass:
- the coat colour: either the coat itself in the civ colour (as the stock main garment), with fixed white lapels, belts and breeches, or a neutral grey-brown coat with civ lapels and cuffs, so a red civ reads red
- visible tails in dirs 5-7
