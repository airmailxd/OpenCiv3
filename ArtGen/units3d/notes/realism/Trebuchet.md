# Trebuchet (realism pass, siege batch)

**Model.** A wheeled counterweight trebuchet, cocked: a long base on four spoked wheels, two braced A-frame towers
carrying the axle, the arm with its plank counterweight box raised over the front and its long end pulled down to
the rear with the sling and stone, a rear winch, round shot piled by the wheels; two men-at-arms as crew.

**Must read at 1x:** the tall towers and the boxed counterweight; the long diagonal arm; the wheeled base.

**Civ colour:** brackets at the towers' cross ties, the upper side rails, the arm bands, the box bands, the cap
beam, the crew's surcoats. Stock 16.3 % (19.5/10.3/15.3/19.6/15.9/12.2/15.8/22.2).
Stock specks: dirs 4/5 two pieces (a shot pile floating in front of the frame).

## Build rounds
- b1: warm wood; civ brackets at every joint (as shipped) vs plain iron joints (11.7 %): the all-civ joints read as
  a blue checker (a noise pattern), all-iron too little colour.
- b2: 'mixed': civ brackets on the cross-tie joints only (14.8 %).
- b3: dark oak vs warmer oak (barely differs after weathering): kept OAK, matching the Catapult; shot piles moved
  in (still detached); thicker box corner posts (a brass cap had floated as a 4 px speck in dir 4).
- b4: shot piles against the wheels: specks clean in every direction.
Chosen (siege.REALISM['Trebuchet']): brackets='mixed', piles against the wheels, post=0.12, OAK + darker box planks.

## Batch 1: 66.5 / 64.0 = 65.25, CRITICAL (both): unclear identity
## Batch 2 build rounds
- c1: at rest (arm -48): the long arm (3.4 m, pale wood, two wide civ bands) raised high to the rear, the box
  (0.85x) hanging below the pivot between the towers, the sling (0.03 m ropes) and pouch with shot hanging from
  the tip; wheels 0.58 m; a man hauling on the winch's hand wheel, one steadying the box.
- c2: arm -55 / -60: -60 keeps the arm long in the head-on views (2, 6). Kept -60.
Not done: "~20% smaller" — the footprint is held to the stock box; the taller rest pose shrinks it on screen instead.

## Batch 2: 70 / 61 = 65.5, fresh CRITICAL: identity and height (the raised arm made a 1.5-2x giant)
## Batch 3 build rounds
- d1: cocked again; arm 2.4 m tapered in the dark rail timber with iron bands and one wide civ band; big dark box
  hoisted at the short end; iron joints. Read as a crate on a cart (the short arm hid in the frame); ppm 44.
- e1: arm 2.9 m at 30 deg (its long end out past the rear frame in 4/8, the sling to the shot in the trough),
  box 1.0x with wide civ bands (0.24), k 1.0 (ppm 36). Tint 11.4 % vs stock 16.3: the civ in few solid masses,
  as asked (rails, cap, box bands, one arm band, surcoats).

## Batch 3: 64.0 / 79.0 = 71.5 (best so far); continuity critic reopened the identity critical
## Round 4 (targeted, identity only)
- r4a: the short end 1.25 m cocked at 45 deg (the box above the towers), arm yawed 12 deg, base 0.8x in plan
  (frame scaled in x/y, wheels moved in), a pale shot and sling. Box above the towers in every view.
- r4b/r4c: short end 1.4, yaw 20; r4c also the arm 3.3 m: its end projects at the rear in 3-5 and the beam
  shows in 2 and 6. Kept r4c. Fitcheck 1.01 (min 0.73 dir 2, the narrow head-on view).
Round 4 values (the starting point for a later pass; a near-miss: 4-8 read, only the arm in 1-2 hidden):
'arm': 45.0, 'arm_long': 3.3, 'arm_iron': (0.55, -1.6, -2.8), 'short': 1.4, 'arm_yaw': 20.0, 'base': 0.8,
crew (((-1.2, -1.08, 0.0), -90.0), ((0.65, -1.0, 0.0), 90.0)), CREW_R reach winch l (-0.74, -0.98, 1.18)
r (-0.74, -1.25, 1.0), loader l (0.14, -0.9, 0.95) r (0.14, -1.12, 0.95); colors + stone '#b8b0a2',
rope '#d0b890'. The builder options (short, base, arm_yaw) stay in siege.py, defaulting to the B3 geometry.

## Round 4: 67.5 / 74.5 = 71.0 (identity still open in 1-2 for both critics)
## FINAL = batch 3 (71.5; f16ebbf7, restored by a scoped restore of its REALISM and CREW_R entries).
## STUCK: identity at 1x in dirs 1-2 (reads as a crate on a cart/ladder; the arm is hidden behind the box).
Critic history: B1 65.25, B2 65.5, B3 71.5, R4 71.0.
Finals: reviews/realism/trebuchet/final.png, final_red.png.
Later pass: in 1 and 2, swing the long end so a diagonal beam crosses in front of the box; lift the box and hanger
clear of the frame top with a visible pivot gap; lengthen the sling so the rope shows in 1-3.
