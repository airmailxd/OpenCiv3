# RPG Trooper: realism design (numpy-foot, PC2)

## What it is

An anti-tank gunner of the 1990s-2000s with the RPG-7 (the NumPy 'RPG Trooper', the mod's 'RPG'). The NumPy sprite and its catalog entry were read for identity only; the build is the units3d realism look.
Entry: `npf_units.ENTRIES['RPG Trooper']`.

## The features that must read at 1x

1. the RPG-7 with its bulbous PG-7 warhead, carried loaded on the right shoulder, muzzle up and forward
2. the carrier of three spare rounds on his back, warheads up
3. a covered helmet with goggles
4. a civ-coloured shirt with rolled sleeves under a khaki chest rig

## Pose

Upright torso (no hunch), the weight on the right leg (human.posture's defaults for the stance), the weapon held
where it reads, off the body's centre line.

## Civ colour

the shirt (sleeves and the shoulders round the rig). Target 10-20 % by direction, near the shipped NumPy share and within the stock foot range (9-34 %).

## Rounds

### Build rounds (own looking, before critic batch a1)

- r1: the RPG crossed the chest, muzzle up; a thin tube; 1.47x the shipped pixels. Next: on the shoulder, a thicker tube, fit 0.84.
- r2: still held before the chest (the 'ready' hand placement close to the shoulder came out at mid-chest).
- vA/vB (variants): vA tube up over the shoulder, vB aimed level from the shoulder, yawed out. vB reads as an RPG
  gunner in every direction (the warhead's bulb ahead of him, the venturi behind); kept.
- r3: vB, fit 1.07 pixels, tint 11.6 % (9.6-14.5; shipped 14.3), clean specks. Then the skin lifted (faces read dark
  under helmets) and the helmet raised (brow_drop 0.075).

### Critic round a1: 55.5 / 70.3 = 62.9 (critical, both: identity: the launcher held like a spear at chest height; dir 2 folds into the chest)

Fixes for a2: on the right shoulder, level, the grip before the shoulder, turned out 20 degrees (dirs 2 and 6); a thicker tube, a smaller warhead, a dark grip and optic block; civ colour as the chest rig's crossed straps and a helmet band (the shirt is olive now); the shared lessons below.

Shared lessons of a1 (both critics, every unit of the group): the stock foot build on every figure (_stock_build: muscle 1.0, weight 0.92, proportions 0.64, upper_bulk 1.32, baggier trousers); weapons thick with an identifying part, readable in dirs 2 and 6; faces lifted, brims off the eyes; civ colour in 2-3 accents. Size now measured against the stock counterpart as well (lead's rule).

### Critic round a2: 75.5 / 74.0 = 74.75 (A: identity closed; B: dir 2 still a knot on the chest)

Fixes for a3: turned out 27 degrees and held level; a khaki shirt; civ colour on a flat sleeve band and a chest patch only.

### Round a3 (the last): out 27 degrees, level; khaki shirt; civ on the short sleeves and a chest patch. Tint 3.0 % (1.5-4.5); 1.17 vs NumPy, 0.92 vs Marine; clean.

Civ colour raised before the critics saw the round (coordinator: at least ~10 % per direction, accents larger but flat): final tint 8.6 % (min 7.7).

### Critic round a3: 78 / 75.3 = 76.65 (the dir-2 critical closed)

**FINAL = A3.** reviews/realism/rpg_trooper/final(_red).png (shipped NumPy, final, stock Marine and Infantry); the entry is frozen: later options for other units default to this look (scratch/numpyfoot/verify/verify_npf.sh re-renders it).

Later pass (the critics' open points):
- the helmet band off the eye line (onto the crown)
- the launcher level on the shoulder in dirs 2-3 and 6-7
