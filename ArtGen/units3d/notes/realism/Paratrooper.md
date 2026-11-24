# Paratrooper: realism design (PC2, realism/infantry)

## The real-world model
A modern paratrooper: the helmet, arid camouflage, the main parachute pack on the back, the reserve on the belly, the harness, knee pads, boots; a carbine.

## The features that must read at 1x
1. the big parachute pack on the back; 2. the reserve on the belly (civ); 3. the helmet; 4. the carbine across the waist.

## The pose
Upright and alert, the carbine at the hip.

## Civ colour
Civ on the reserve chute's cover, the armbands and the knee pads. The shipped unit is 15 % (a civ vest). Target: 15-30 % in every direction (foot stock 9-34 %), in 2-3 painted regions.

## Stock neighbours
WWII Paratrooper and Marine

## Rounds
### Build rounds 1-4
- R1: arid camouflage, the main pack on the back, a new civ reserve chute on the belly (props.reserve_chute), civ knee pads: 5 % (the reserve hidden behind the arms).
- R2: a bigger reserve (1.5) and civ armbands: 7-17 %, mean 13 %.
- R3-4: the carbine whole (`whole`), a thicker barrel: clean.

### Critic batch 1: 66 / 79 = 72.5

### Round 2 (fixes)
- Tan arid camouflage with an olive helmet cover (away from the Marine's woodland); a bigger, higher squared main container (1.35) with harness straps crossing the chest; the reserve olive with one civ band (new reserve_chute `trim`); tall brown jump boots (new `boots_height` 0.44); flat civ armbands; the carbine angled out past the right hip; `unit_scale` 0.965 (the bigger chute put it at 1.14 of the shipped area). Civ 7-13 %, mean 9 %.

### Critic batch 2: 80 / 84 = 82.0

### Round 3 (the last; fixes)
- The container OD with webbing straps over it and raised side flaps (new chute_pack `straps`); the harness's quick-release buckle on the chest (new reserve_chute `buckle`) and leg straps high on the thighs (new band location 'thigh'); dark webbing; the carbine out at 45 degrees; a wider civ helmet band. Civ 8-13 %, mean 11 %.

### Critic batch 3: 78 / 86 = 82.0

## FINAL: R3 82.0 (ties R2; R3 has the better airborne read)

Rounds: R1 72.5, R2 82.0, **R3 82.0**. Cap reached (3 critic rounds). Pictures: reviews/realism/paratrooper/final(_red).png (shipped, final, two stock neighbours); the finals gate re-renders it at HEAD.

Later pass:
- the support hand off the handguard in dirs 0-1
- the buckle and leg straps do not read at 1x
- fix the support-hand rig so the hand closes on the handguard
