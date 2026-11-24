# Combat Engineer: realism design (numpy-foot, PC2)

## What it is

An assault engineer with a flamethrower (the NumPy 'Combat Engineer', the mod's 'americanFT'). The NumPy sprite and its catalog entry were read for identity only; the build is the units3d realism look.
Entry: `npf_units.ENTRIES['Combat Engineer']`.

## The features that must read at 1x

1. the flamethrower's fuel tanks on a back frame
2. the flame gun at the hip, its pilot flame lit at the nozzle
3. an M1 helmet with goggles, a flak vest
4. a demolition satchel at the hip

## Pose

Upright torso (no hunch); the weapon held where it reads, off the body's centre line.

## Civ colour

the field shirt (sleeves and shoulders round the vest). Target 10-20 % by direction, near the shipped NumPy share and within the stock foot range (9-34 %).

## Rounds

### Build rounds and batch c1

- r1: the tanks read small, the flame gun as a tube; tint 17 % on the sleeves.
- r2: bigger tanks (scale 1.5), a darker thicker gun with a lit pilot flame; fit 0.95.
- c1 (with a1's shared lessons): the stock build; civ colour on the flak vest and the helmet band (tint 13.3 %, 6.0-21.7); 1.08 against the stock Marine.

### Critic round c1: 61 / 72.5 = 66.75 (A: identity, civ on the whole vest)

Fixes for c2: a thick fuel hose from the gun toward the tanks, a thicker barrel and a flame plume, the gun out to the side; a neutral vest, civ colour on the satchel's flap and the helmet band; the satchel bigger.

### Round c2: the fuel hose, a thicker flame gun with a flame plume, out to the side; a neutral vest with a civ chest patch, a civ helmet band and a flat brassard; a bigger satchel with a civ flap. Tint 3.6 %; 1.14 vs NumPy, 1.09 vs Marine; clean.

Civ colour raised before the critics saw the round (coordinator: at least ~10 % per direction, accents larger but flat): final tint 7.7 % (min 3.1, dir 3).

### Critic round c3: 71 / 73.5 = 72.25 (A: flame identity open: the self-lit plume washed out pale)

### Round c4 (targeted): a matte saturated orange plume, hand length, with a short yellow core; a light thick hose hanging below the arm back to the tanks. Tint 7.8 % (2.9-13.3); 1.13 vs NumPy, 1.08 vs Marine; clean.

### Critic round c4: 75 / 74.8 = 74.9

**FINAL = C4; the identity critical closed.** reviews/realism/combat_engineer/final(_red).png (shipped NumPy, final, stock Marine and Infantry). The entry is frozen; scratch/numpyfoot/verify/verify_npf.sh re-renders it.

Later pass:
- run the hose up to the gun's rear grip in black rubber (it reads as a white floating curl)
- the flame about 2x longer
