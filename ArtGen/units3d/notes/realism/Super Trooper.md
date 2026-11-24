# Super Trooper: realism design (numpy-foot, PC2)

## What it is

Near-future power armour (the NumPy 'Super Trooper', the mod's 'XAirborne'). The NumPy sprite and its catalog entry were read for identity only; the build is the units3d realism look.
Entry: `npf_units.ENTRIES['Super Trooper']`.

## The features that must read at 1x

1. the closed helmet with its dark visor
2. the broad plated shoulders and armoured shins
3. the heavy pulse rifle with a glowing emitter, at the hip
4. the power pack on the back with glowing cells

## Pose

Upright torso (no hunch); the weapon held where it reads, off the body's centre line.

## Civ colour

the chest and back plates (the pauldrons stay the suit's grey). Target 10-20 % by direction, near the shipped NumPy share and within the stock foot range (9-34 %).

## Rounds

### Build rounds and batch c1

- r1: civ plates and pauldrons at 27 %; the helmet a shiny dome.
- r2: a chest disc emblem hidden under the plate (2.8 %).
- r3: civ chest and back plates only, grey plated shoulders, a matte helmet without the crest bar: 13.4 %.
- c1 (with a1's shared lessons): 13.1 % (6.6-17.3); 0.95 against the stock Marine.

### Critic round c1: 63.5 / 69 = 66.25 (reads as a wetsuit with pauldrons)

Fixes for c2: plated thighs and knees, gauntlets over the forearms and hands; a bigger power pack; a boxy rifle with a glowing power cell.

### Round c2: grey plated thighs, knees and gauntlets; a bigger power pack; a boxy rifle with a glowing cell; civ chest and back plates only. Tint 7.3 %; 1.15 vs NumPy, 1.05 vs Marine; clean.

Civ colour raised before the critics saw the round (coordinator: at least ~10 % per direction, accents larger but flat): final tint 10.1 % (min 3.1, dir 4).

### Critic round c3: 65.5 / 70.3 = 67.9 (A: out of house style; the leg plates were hidden under a bulked trouser shell, the hands bare)

### Round c4 (targeted): limb armour as props on the posed bones (npf_armour: plated thighs and shins, knee cops, forearm plates, gauntlets), no trouser_offset. Tint 10.2 % (3.8-14.6); 1.12 vs NumPy, 1.02 vs Marine; clean.

### Critic round c4: 65.5 / 72.8 = 69.15

**FINAL = C4, **STUCK** (A: out of the house style: slim legs; hands flesh-coloured in dirs 2, 3, 5).** reviews/realism/super_trooper/final(_red).png (shipped NumPy, final, stock Marine and Infantry). The entry is frozen; scratch/numpyfoot/verify/verify_npf.sh re-renders it.

Later pass:
- leg armour with real bulk, matching the torso
- gauntlets in grey metal (they render as flesh: check the material assignment of npf_limb_plate's gauntlet on the hand bones)
