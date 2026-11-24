# Militant: realism design (numpy-foot, PC2)

## What it is

A fighter in a headwrap with an AK and bayonet (the NumPy 'Militant', the mod's 'Mujahidin'). The NumPy sprite and its catalog entry were read for identity only; the build is the units3d realism look.
Entry: `npf_units.ENTRIES['Militant']`.

## The features that must read at 1x

1. the AK with its bayonet fixed, upright in the right fist
2. the civ-coloured headwrap and a full beard
3. a long khaki shirt over camouflage trousers
4. a crossbelt

## Pose

Upright torso (no hunch), the weight on the right leg (human.posture's defaults for the stance), the weapon held
where it reads, off the body's centre line.

## Civ colour

the headwrap and a scarf band at the neck. Target 10-20 % by direction, near the shipped NumPy share and within the stock foot range (9-34 %).

## Rounds

### Build rounds (own looking, before critic batch a1)

- r1: the stock head_wrap read as a white spike over blue rings; the AK held out at arm's length, upright, the arm
  raised; tint 8 %.
- r2: own headwrap (npf_headwrap, the civ colour, wound folds); the AK upright in front of the right shoulder in both
  hands, bayonet up; fit 0.92 (pixels 0.97); tint 15.8 % (12.8-20.4; shipped 10.9).
- r3: the face read dark: lighter skin, a goatee instead of the full beard, the wrap lifted (brow_drop 0.07).

### Critic round a1: 68.5 / 71.0 = 69.75 (critical, A: identity: reads as a bolt-action with a long bayonet)

Fixes for a2: an unmistakable AK (npf_ak: a big dark curved magazine, the slanted brake, the tall front sight, no bayonet) at port arms, 35-40 degrees across the chest; a neutral khaki headwrap, civ colour on the scarf and a sash; a lighter face; the shared lessons below.

Shared lessons of a1 (both critics, every unit of the group): the stock foot build on every figure (_stock_build: muscle 1.0, weight 0.92, proportions 0.64, upper_bulk 1.32, baggier trousers); weapons thick with an identifying part, readable in dirs 2 and 6; faces lifted, brims off the eyes; civ colour in 2-3 accents. Size now measured against the stock counterpart as well (lead's rule).

### Critic round a2: 67.5 / 69.5 = 68.5 (A: the AK critical not closed)

Fixes for a3: a bigger dark-metal curved magazine, the AK smaller (scale 1.3) and further out in front of the chest; a loose cloth headwrap with fat folds and a long tail.

### Round a3 (the last): a loose headwrap with fat folds and a long tail; a bigger dark curved AK magazine, the AK further out across the chest; civ scarf and sash. Tint 8.6 % (5.1-14.1); 0.85 vs NumPy, 0.97 vs Marine; clean.

Civ colour raised before the critics saw the round (coordinator: at least ~10 % per direction, accents larger but flat): final tint 12.4 % (min 8.9).

### Round 4 (targeted, the AK critical only): 71 / 73.5 = 72.25

The AK bigger (scale 1.45) with a larger dark curved magazine, a gas tube and a tall front sight, carried in one
diagonal across the chest with its side to the front ('ready' stance); the wide civ neck band (read as a veil) now a
low 6 cm scarf; the headwrap's civ fold moved up. Tint 8.0 % (5.8-11.4); 1.03 against the stock Marine; clean.

**FINAL = R4** (above A1's 69.75). reviews/realism/militant/final(_red).png (the solo4 sheet: shipped NumPy, final,
stock Marine and Infantry). **STUCK: AK identity at 1x in dirs 1, 4, 5 and 6** (the critics split after the targeted
round: A open, B closed; lead's rule). The entry is frozen.

Later pass:
- carry the AK at about 45 degrees with the magazine side to the viewer in dirs 1, 4, 5 and 6, as in dirs 2 and 3;
- loosen the headwrap into an uneven wrap with folds and a hanging tail (the dome-and-band reads as a pith helmet).
