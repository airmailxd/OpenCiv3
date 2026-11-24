# Partisans: realism design (numpy-foot, PC2)

## What it is

A WWII resistance fighter (the League 'Partisans'). The NumPy sprite and its catalog entry were read for identity only; the build is the units3d realism look.
Entry: `npf_units.ENTRIES['Partisans']`.

## The features that must read at 1x

1. the PPSh with its drum held across the body
2. the long dark civilian coat to the knee and the flat cap
3. the civ armbands and scarf
4. a leather bandolier and tall boots

## Pose

Upright torso (no hunch), the weight on the right leg (human.posture's defaults for the stance), the weapon held
where it reads, off the body's centre line.

## Civ colour

armbands on both upper arms and the scarf (the shipped 2.5 % was too low to read). Target 10-20 % by direction, near the shipped NumPy share and within the stock foot range (9-34 %).

## Rounds

### Build rounds (own looking, before critic batch b1)

- r1: reads as a partisan in a long coat and flat cap; tint 5.6 % (shipped 2.5; stock foot 9+); pixels 0.85; the
  PPSh read as a wooden plank, its drum not shown.
- r2-r3: fit 0.95 (pixels 1.00); armbands and scarf wider (tint 9.0 %, 7.6-10.1); a dark hem on the coat;
  clean-shaven (the stubble read as a dark mask); a bigger drum.
- r4: the drum still hidden: at port arms the gun's underside faces the body. r5: carried across the hips
  ('hip_hold'), so the drum hangs under the gun where it shows.

### Batch b1 (with a1's shared lessons)

The stock build; fit 0.88 (1.00 against the stock Rifleman, 1.03 the NumPy sprite); tint 13.5 % (11.8-14.8; armbands and scarf); the PPSh's drum hanging under the gun.

### Critic round b1: 64 / 69.8 = 66.9 (A: civ reads as shoulder pads)

Fixes for b2: one flat 5 cm armband and the scarf (bands laid flush, FLAT_BAND); a lighter bandolier, a brown belt, a bread bag and a pouch, coat pockets; a bigger drum, a perforated jacket and a lighter stock on the PPSh; a lighter face.

### Round b2: a flat armband and the scarf, a lighter bandolier, a brown belt, bread bag, pouch and pockets; the PPSh with a lighter stock, perforated jacket and a 30 % bigger drum. Tint 8.9 %; 1.00 vs NumPy, 0.97 vs Rifleman; clean.

Civ colour raised before the critics saw the round (coordinator: at least ~10 % per direction, accents larger but flat): final tint 10.3 % (min 8.7).

### Critic round b3: 67.5 / 74.5 = 71.0

**FINAL = B3.** reviews/realism/partisans/final(_red).png (shipped NumPy, final, stock Rifleman and Infantry). Tint 13.6 % (10.7-16.8); 0.95 against the stock Rifleman. The entry is frozen.

Later pass:
- the PPSh still reads as a pan on a stick in dirs 1, 3, 4, 8: turn it side-on so the stock, receiver, jacket and drum read in a line
- civ colour on a cap band only, not the whole crown
