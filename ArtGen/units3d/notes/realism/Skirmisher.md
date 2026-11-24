# Skirmisher: realism design (numpy-foot, PC2)

## What it is

A US rifleman of the 1960s (the NumPy 'Skirmisher'). The NumPy sprite and its catalog entry were read for identity only; the build is the units3d realism look.
Entry: `npf_units.ENTRIES['Skirmisher']`.

## The features that must read at 1x

1. the M1 helmet in its leaf-pattern cover with its band
2. the M14 at the low ready
3. the civ jungle jacket with rolled sleeves
4. M1956 suspenders, canteen, butt pack, towel at the neck

## Pose

Upright torso (no hunch), the weight on the right leg (human.posture's defaults for the stance), the weapon held
where it reads, off the body's centre line.

## Civ colour

the jungle jacket. Target 10-20 % by direction, near the shipped NumPy share and within the stock foot range (9-34 %).

## Rounds

### Build rounds (own looking, before critic batch b1)

- r1: the M14 hung limp at the low ready; tint 20.7 %; pixels 0.90.
- r2: the M14 at port arms across the chest (reads in all directions); fit 0.93 (pixels 1.02); tint 19.4 %
  (16.1-23.9; shipped 14.3); a wider towel. Then lighter skin and the helmet lifted (faces read dark).

### Batch b1 (with a1's shared lessons)

The stock build; an OG jacket, civ colour on the helmet band, the towel, the bandolier straps and the rolled cuffs (tint 15.9 %, 13.8-19.5); fit 0.98 (1.01 against the stock Infantry).

### Critic round b1: 64.5 / 66.5 = 65.5 (both: the rifle unreadable in dir 1; A: civ invalid)

Fixes for b2: the M14 lower and more across the chest; civ colour on the helmet band and a flat armband only (the yoke olive again); OG sleeves to the forearm, slimmer arms.

### Round b2: the M14 diagonal across the chest; civ on a wide helmet band, the towel and a flat armband; full OG sleeves, slimmer arms. Tint 9.4 %; 1.24 vs NumPy, 0.99 vs Infantry; clean.

Civ colour raised before the critics saw the round (coordinator: at least ~10 % per direction, accents larger but flat): final tint 9.4 % (min 8.2).

### Critic round b3: 69 / 71.8 = 70.4 (the dir-1 critical still open)

### Round 4 (targeted, the rifle only): 71 / 70.5 = 70.75

Port arms with the muzzle at the left shoulder and the butt at the right hip, the rifle's side to the front. Tint 10.0 %
(8.4-11.8); 0.99 against the stock Infantry; clean.

**FINAL = R4.** reviews/realism/skirmisher/final(_red).png (the solo4 sheet: shipped NumPy, final, stock Infantry and
Rifleman). **STUCK: rifle unreadable at 1x in dirs 1, 4, 5 and 8** (both critics: in dirs 1 and 8 the muzzle points at
the camera from the hip, a 6 px stock stub; in dirs 4-5 near vertical). The entry is frozen.

Later pass:
- rotate the rifle about 60 degrees in dirs 1 and 8 so it runs from the left hip to the right shoulder across the chest;
- tilt it 35-40 degrees off vertical in dirs 4-5;
- one fixed diagonal goes edge-on in some views: a per-direction carry, or the rifle slung across the back, may be needed.
