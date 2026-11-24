# Special Forces: realism design (numpy-foot, PC2)

## What it is

A present-day special operator (the League 'Special Forces'). The NumPy sprite and its catalog entry were read for identity only; the build is the units3d realism look.
Entry: `npf_units.ENTRIES['Special Forces']`.

## The features that must read at 1x

1. the black high-cut helmet with the night-vision goggles flipped up and the headset
2. a bulky ranger-green plate carrier
3. a suppressed carbine with optics at the low ready, off the centre line
4. a beard, knee pads, an assault pack

## Pose

Upright torso (no hunch), the weight on the right leg (human.posture's defaults for the stance), the weapon held
where it reads, off the body's centre line.

## Civ colour

the sleeves (as the stock Marine). Target 10-20 % by direction, near the shipped NumPy share and within the stock foot range (9-34 %).

## Rounds

### Build rounds (own looking, before critic batch a1)

- r1: the carbine hung low, pointing at the ground, parts read as separate pieces; the civ sleeves 26 %; 1.48x pixels.
- r2: fit 0.85 (pixels 1.01); still low and limp. long_gloves sit under the sleeve shell (no effect): removed.
- r3: aimed from the shoulder, muzzle a little down (the unit fires from the shoulder): reads in every direction;
  wrist bands as gloves and cuffs; tint 15.5 % (10.5-20.9; shipped 14.1).

### Critic round a1: 73.5 / 75.5 = 74.5 (no critical)

Fixes for a2: lighter face and beard; the NVG mount down at the brow; the carbine short and thick, a fat light suppressor, raised to chest height; a plate rig with three magazine pouches, a radio and the cummerbund's edges; civ colour on the upper sleeves and helmet patches only, camo forearms; the shared lessons below.

Shared lessons of a1 (both critics, every unit of the group): the stock foot build on every figure (_stock_build: muscle 1.0, weight 0.92, proportions 0.64, upper_bulk 1.32, baggier trousers); weapons thick with an identifying part, readable in dirs 2 and 6; faces lifted, brims off the eyes; civ colour in 2-3 accents. Size now measured against the stock counterpart as well (lead's rule).

### Critic round a2: 69.5 / 78.3 = 73.9 (no gain)

Fixes for a3: sleeve-width arms (bulk 1.12), the civ blocks gone (a flat 5 cm sleeve band, helmet patches, a rig patch); a lighter face and beard; the carbine at the shoulder.

### Round a3 (the last): sleeve-width arms, a civ helmet cover, a flat sleeve band and a rig patch; the carbine at the shoulder; a lighter face. Tint 8.8 % (7.7-10.2); 1.11 vs NumPy, 0.99 vs Marine; clean.

Civ colour raised before the critics saw the round (coordinator: at least ~10 % per direction, accents larger but flat): final tint 9.0 % (min 7.8).

### Critic round a3: 75.5 / 81 = 78.25 (no critical)

**FINAL = A3.** reviews/realism/special_forces/final(_red).png (shipped NumPy, final, stock Marine and Infantry); the entry is frozen: later options for other units default to this look (scratch/numpyfoot/verify/verify_npf.sh re-renders it).

Later pass (the critics' open points):
- a camo helmet cover with a civ band instead of the all-civ bowl
- lift the face and beard in dirs 1-3
- shoulder the carbine in dirs 1-2
- pouches on the back of the carrier
