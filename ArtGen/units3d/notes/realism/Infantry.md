# Infantry: realism design (PC2, realism/infantry)

## The real-world model
A WWII rifleman (US, 1943-44): the M1 helmet, the olive drab wool shirt and trousers, canvas leggings, brown boots; the khaki cartridge belt and suspenders; the pack high on the back; the M1 Garand with its bayonet.

## The features that must read at 1x
1. the M1 helmet's round dome; 2. the rifle and bayonet level at the hip, pointing forward; 3. the pack on the back (olive); 4. the pale leggings.

## The pose
Square and alert, the rifle at the hip (hip_hold), the muzzle forward.

## Civ colour
Civ on the helmet (painted) and wide armbands; the pack olive (round 3). The shipped unit is 52 % (the shirt and pack). Target: 15-30 % in every direction (foot stock 9-34 %), in 2-3 painted regions.

## Stock neighbours
Marine and WWII Paratrooper

## Rounds
### Build rounds 1-5
- R1: olive drab wool, khaki webbing, canvas leggings; civ only on the pack: 0-27 % by direction (none from the front).
- R2: the M1 painted civ (new `modern_helmet` option `paint`): 10-47 %, the pack too strong from behind.
- R3: the pack in olive, civ armbands instead: 13-18 %, even. A wider bayonet with a blunt point (specks at its tip).
- R6-7: the specks were the bayonet's needle point: the widened blade now runs near parallel to a blunt point, round in section. Clean.

### Critic batch 1: 69.5 / 82 = 75.75

### Round 2 (fixes)
- Darker, warmer olive drab wool, matte (`cloth_rough` 0.95); dark webbing (belt, crossed suspenders) and a canteen on the back of the belt; the M1 in olive steel with a wide civ band (`band_w` 0.07) instead of a civ-painted shell; flat civ armbands (0.18); a thicker Garand; the muzzle angled out past the right hip, so it shows from behind. Civ 6-12 %, mean 9 %.

### Critic batch 2: 79 / 85 = 82.0

### Round 3 (the last; fixes)
- Khaki webbing, belt, canteen cover and a pouch against the OD wool; the Garand out at 45 degrees, so the front view sees its length (the hip hold kept rather than a new stance). Civ 6-12 %, mean 9 %.

### Critic batch 3: 84 / 86 = 85.0 PASS

## FINAL: R3 85.0 PASS

Rounds: R1 75.75, R2 82.0, **R3 85.0 (PASS)**. Cap reached (3 critic rounds). Pictures: reviews/realism/infantry/final(_red).png (shipped, final, two stock neighbours); the finals gate re-renders it at HEAD.

Later pass:
- the face dark under the helmet brim in dirs 1-2
- heavy pale puttees
