# Marine: realism design (PC2, realism/infantry)

## The real-world model
A modern US Marine: the helmet in a woodland cover, MARPAT woodland blouse and trousers, a coyote plate carrier, knee pads, boots; the M16.

## The features that must read at 1x
1. the M16 at the hip, the muzzle forward; 2. the helmet with its band; 3. the tan plate carrier over the camo; 4. the knee pads.

## The pose
Upright, the rifle at the hip (hip_hold).

## Civ colour
Civ on a wide band round the helmet, wide armbands and the knee pads. The shipped unit is 2 % (a thin helmet band). Target: 15-30 % in every direction (foot stock 9-34 %), in 2-3 painted regions.

## Stock neighbours
Infantry and Paratrooper

## Rounds
### Build rounds 1-4
- R1: woodland MARPAT, a coyote plate carrier, civ armbands and knee pads: 8 %.
- R2: wider armbands and knee pads and a wide civ band on the helmet (`band_w` 0.06): 13-17 %.
- R3-4: the M16 broke into pieces at 1x (the prop's boxes did not scale with it, leaving gaps): new `whole` option, so the parts scale together; a thicker barrel. Clean.

### Critic batch 1: 79 / 85 = 82.0

### Round 2 (fixes)
- The M16 at the port across the chest (receiver and magazine read; the muzzle shows over the left shoulder from behind); flat civ armbands. Civ 11-16 %, mean 13 %.

### Critic batch 2: 73.5 / 83 = 78.25 (A: CRITICAL, the hands float off the M16 in dir 7)

### Round 3 (the last; fixes)
- Round 2's port arms dropped (the hands floated off the rifle in dir 7, critical): back to round 1's hold at the hip, both hands on the grip and the handguard, the muzzle angled out (gun_dir -0.45, -1, 0.14) so it shows from behind; round 2's flat armbands kept. Civ 12-15 %, mean 13 %.

### Critic batch 3: R3 averaged 78.25 (A: the support hand still floats off the handguard in dirs 0, 1 and 7)

## FINAL: R1 82.0, restored (scoped: REALISM['Marine'] only; R2 had port arms with gun_dir (0.55, -0.35, 0.75), R3 the hip hold with gun_dir (-0.45, -1, 0.14) and flat armbands at offset 0.013)

Rounds: **R1 82.0**, R2 78.25 (critical: hands off the M16 in dir 7), R3 78.25. Cap reached (3 critic rounds). Pictures: reviews/realism/marine/final(_red).png (shipped, final, two stock neighbours); the finals gate re-renders it at HEAD.

Later pass:
- fix the support-hand rig so the hand closes on the handguard
