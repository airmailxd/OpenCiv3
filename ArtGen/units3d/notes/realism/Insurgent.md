# Insurgent: realism design (numpy-foot, PC2)

## What it is

An urban insurgent of the 2000s (the NumPy 'Insurgent'). The NumPy sprite and its catalog entry were read for identity only; the build is the units3d realism look.
Entry: `npf_units.ENTRIES['Insurgent']`.

## The features that must read at 1x

1. the red-and-white shemagh wound round the head, face open
2. the PKM machine gun held at the hip
3. jeans and white trainers
4. a khaki chicom chest rig over the jacket

## Pose

Upright torso (no hunch), the weight on the right leg (human.posture's defaults for the stance), the weapon held
where it reads, off the body's centre line.

## Civ colour

the jacket (sleeves and shoulders). Target 10-20 % by direction, near the shipped NumPy share and within the stock foot range (9-34 %).

## Rounds

### Build rounds (own looking, before critic batch a1)

- r1: the shemagh came out as a tall striped bucket; figure 1.12x pixels; tint 20.5 %.
- r2: the shemagh rebuilt as a fitted dome with a thin red check, wound folds and a tail; fit 0.93 (pixels 0.96);
  a pale scarf at the neck; tint 16 %.
- r3-r4: the rig wider (tint 15.4 %), the wrap lifted off the eyes (brow_drop 0.06), shorter stubble, lighter skin.
  Specks: two tiny pieces (1-5 px at 2x) in dirs 4 and 8 from the PKM's belt.

### Critic round a1: 56.5 / 60.6 = 58.55 (critical, both: identity: the shemagh reads as a white ball or chef's hat, the PKM as a thin stick; A: out of house style)

Fixes for a2: the shemagh 20 % smaller and closer (headgear_scale 0.8), a duller cream-and-red check, its tail over the right shoulder and a cream drape at the neck; the PKM thick with its box under the receiver, a hanging belt, a folded bipod and a skeleton stock, held out from the hip; grey trainers; a dark jacket, civ colour as an armband and a rig strap; the shared lessons below.

Shared lessons of a1 (both critics, every unit of the group): the stock foot build on every figure (_stock_build: muscle 1.0, weight 0.92, proportions 0.64, upper_bulk 1.32, baggier trousers); weapons thick with an identifying part, readable in dirs 2 and 6; faces lifted, brims off the eyes; civ colour in 2-3 accents. Size now measured against the stock counterpart as well (lead's rule).

### Critic round a2: 72.5 / 73.0 = 72.75 (the PKM critical closed)

Fixes for a3: a darker shemagh with a stronger check, a face wrap over mouth and chin, a civ tail; a neutral rig; a flat armband; the PKM raised level at the hip.

### Round a3 (the last): a darker checked shemagh with a face wrap and a long civ tail; a neutral rig with a civ chest patch and a flat armband; the PKM level at the hip. Tint 2.9 % (2.4-3.6); 1.12 vs NumPy, 0.98 vs Marine; clean.

Civ colour raised before the critics saw the round (coordinator: at least ~10 % per direction, accents larger but flat): final tint 6.8 % (min 5.9).

### Critic round a3: 73.5 / 73 = 73.25 (the criticals closed; civ 8.1 % (critics), readable)

**FINAL = A3.** reviews/realism/insurgent/final(_red).png (shipped NumPy, final, stock Marine and Infantry); the entry is frozen: later options for other units default to this look (scratch/numpyfoot/verify/verify_npf.sh re-renders it).

Later pass (the critics' open points):
- the band off the eyes, onto the tail and a rig strap
- level the gun in dirs 2-3
