# Battalion: realism design (numpy-foot, PC2)

## What it is

A US machine-gun team of 1944 in action (the NumPy 'Battalion', archetype line_mg_team; the mod's 'americanHMG'). The NumPy sprite and its catalog entry were read for identity only; the build is the units3d realism look.
Entry: `npf_units.ENTRIES['Battalion']`.

## The features that must read at 1x

1. the water-cooled Browning M1917A1 on its low tripod
2. two men kneeling at it: the gunner on the spade grips, the assistant at the ammunition box
3. M1 helmets with nets
4. M1941 field jackets and canvas leggings

## Pose

Upright torso (no hunch); the weapon held where it reads, off the body's centre line.

## Civ colour

the crossed suspenders, the belts and bands on the upper arms (the shipped team carried 6.4 %). Target 10-20 % by direction, near the shipped NumPy share and within the stock foot range (9-34 %).

## Rounds

### Build rounds and batch c1

- r1: the two men lunged and overlapped; tint 3.4 %.
- r2-r3: a true kneel (the right thigh down, the shin along the ground), the assistant further out, civ armbands and wider suspenders (6.9 %), a thicker tripod and hose (specks), no holster.
- c1 (with a1's shared lessons): fit 1.0, 1.26 against the NumPy sprite; tint 6.9 % (shipped 6.4).

### Critic round c1: 48.5 / 48.3 = 48.4 (both: clipping between the men, the crew apart from the gun, no water jacket)

Fixes for c2: both men kneel upright on both knees (no lunge), the gunner's hands placed on the spade grips, the loader's at the ammunition box, the men 0.9 m apart; the gun 45 % bigger with a fat corrugated water jacket; civ helmet bands added.

### Round c2: both men kneel upright (no lunge), the gunner's hands on the grips, the loader at the ammunition box, 0.9 m apart; the gun 45 % bigger with a fat corrugated jacket; civ helmet bands, suspenders, belts and armbands. Tint 7.8 %; 1.27 vs NumPy (2.26 vs the single-figure TOW Infantry); clean.

Civ colour raised before the critics saw the round (coordinator: at least ~10 % per direction, accents larger but flat): final tint 8.8 % (min 7.0).

### Critic round c3: 71.5 / 68 = 69.75 (A: merged figures in dirs 3 and 7)

### Round c4 (targeted): the loader 67.5 degrees off the gunner (halfway between two camera axes) at (1.29, 0.08); faces lifted (brow_drop 0.055, lighter skin); civ suspenders 0.14 wide. Tint 9.7 % (7.6-10.7); 1.37 vs NumPy; specks: the two men and the gun are separate pieces in dirs 1, 2 and 6 by design, nothing small. Some overlap remains in dirs 3-4 and 7-8.

### Critic round c4: 71.5 / 69.8 = 70.65

**FINAL = C4, **STUCK** (the men merge in dirs 4, 7, 8; the gun hidden in dir 6).** reviews/realism/battalion/final(_red).png (shipped NumPy, final, stock Marine and Infantry). The entry is frozen; scratch/numpyfoot/verify/verify_npf.sh re-renders it.

Later pass:
- separate the loader per direction, or accept a three-quarter group pose
