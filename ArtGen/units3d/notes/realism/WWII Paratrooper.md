# WWII Paratrooper: realism design (PC2, realism/infantry)

## The real-world model
A WWII airborne trooper (US, 1944): the netted M1 helmet, the plain khaki M42 jump suit, the main chute on the back and the reserve on the belly, jump boots; the Thompson.

## The features that must read at 1x
1. the netted helmet; 2. the bulky chute on the back and the reserve in front; 3. the Thompson low across the hip; 4. the khaki jump suit and brown boots.

## The pose
Upright, the Thompson at the hip, the muzzle forward.

## Civ colour
Civ on the reserve chute's cover and an armband. The shipped unit is 13 % (the chest front and the belt). Target: 15-30 % in every direction (foot stock 9-34 %), in 2-3 painted regions.

## Stock neighbours
Paratrooper and Infantry

## Rounds
### Build rounds 1-5
- R1: the plain khaki M42 jump suit, a netted M1, a civ reserve on the belly and a civ armband: 5 %.
- R2-3: a bigger reserve, wider armbands and civ knee bands: 6-16 %, mean 11 %.
- R4: the Thompson whole (`whole`).
- R5: the chute bundle sat 0.25 m off the back and read as a separate shield from the side (a second piece in dirs 4 and 8): new `para_pack` option `gap`, the bundle snug on the back.
- R6: the static line moved in with the bundle (it was left floating behind it). Clean; civ 7-16 %, mean 11 %.

### Critic batch 1: 63.5 / 79 = 71.25 (B: borderline identity)

### Round 2 (fixes)
- The camo shell replaced by a squared T-5 pack in plain OD canvas (chute_pack 1.15); the reserve olive with one civ band; a bigger Thompson (1.65, whole) angled out past the hip; jump boots; flat civ armbands. Civ 7-12 %, mean 9 %.

### Critic batch 2: 75 / 84 = 79.5

### Round 3 (the last; fixes)
- A darker tan M42 (#7a7050) with chest pocket flaps and bellows cargo pouches at the hips (plain, no camo, as the M42 was); the harness and the reserve's buckle on the chest; darker OD straps on the pack; a civ band on the netted helmet. Civ 8-12 %, mean 9 %.

### Critic batch 3: 75 / 85 = 80.0

## FINAL: R3 80.0

Rounds: R1 71.25, R2 79.5, **R3 80.0**. Cap reached (3 critic rounds). Pictures: reviews/realism/wwii_paratrooper/final(_red).png (shipped, final, two stock neighbours); the finals gate re-renders it at HEAD.

Later pass:
- the pockets do not read
- the support hand in dir 0
- fix the support-hand rig so the hand closes on the handguard
