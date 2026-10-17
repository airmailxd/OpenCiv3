# Legionary: motion reference from the Civ3 originals

**Source:** `Art/Units/Legionary/Legionary.INI` and its flics (Civ3 Complete). Written from contact sheets of every frame in direction 3. Here the legionary stands three-quarter on, facing screen-right and toward the viewer; he strikes to screen-right. "Forward" is the way he faces, "back" behind him.

## Timing

- All flics have 8 directions. `[Speed] Normal=225`; every `[Timing]` value is 0.5.
- The game plays Death once; the rest loop. ATTACK1 is the only attack the game plays.
- Note the INI names the second attack `LegionaryAttack2.flc`, not AttackB.

| Action | Flic | Frames | ms/frame | Size (offset) |
|---|---|---|---|---|
| DEFAULT | LegionaryDefault.flc | 15 | 125 | 40x51 (98,77) |
| ATTACK1 | LegionaryAttackA.flc | 15 | 83 | 69x64 (85,72) |
| ATTACK2 | LegionaryAttack2.flc | 15 | 83 | 65x66 (85,67) |
| DEATH | LegionaryDeath.flc | 15 | 83 | 129x104 (55,50) |
| RUN | LegionaryRun.flc | 10 | 66 | 40x54 (98,75) |
| FORTIFY | LegionaryFortify.flc | 10 | 66 | 48x55 (93,74) |

The attacks are compact: the gladius is short and he stays behind the shield.

## The figure

A Roman legionary: a helmet with a civ-colored crest, segmented grey armor over a civ tunic, a civ-colored cloak at the shoulders, a tall dark scutum (painted with a civ device in the death flic) on the left arm, and a short gladius in the right hand.

## ATTACK1 (AttackA, 15 f, 83 ms): an overhead backswing into a low thrust

- **f0:** standing upright, facing the viewer, the shield at the left, the sword at the right side.
- **f1-4:** raising the sword up and back over the right shoulder, high behind the head (f2-3 the highest).
- **f5:** turning side-on, stepping forward, the shield coming across the front.
- **f6:** stepping into a lunge behind the shield, the sword coming forward low.
- **f7-9:** **the thrust**: the gladius driven straight forward under the shield's edge at waist height, the front knee bent (strike f8, the longest reach).
- **f10-11:** withdrawing behind the shield, the sword pulled back low.
- **f12-13:** turning back toward the viewer, standing up.
- **f14:** standing (f14 = f0).
- **One strike per cycle, at f8.** No travel.

## ATTACK2 (Attack2, 15 f, 83 ms): a thrust, then an overhand cut

- **f0-2:** standing; the sword raised point-up before the chest.
- **f3:** a step forward behind the shield.
- **f4:** **a short thrust**: the sword driven forward at waist height (the first strike).
- **f5-8:** the sword sweeps up and back over the right shoulder, the body turning (f7-8 high).
- **f9:** **an overhand cut** coming down over the shoulder (the second strike).
- **f10-12:** the sword swinging down and back to low at the right side.
- **f13-14:** standing.

## DEATH (15 f, 83 ms, once): spins, drops the shield and falls on his back

- **f0:** standing behind the shield.
- **f1-2:** hit: he doubles forward, the sword thrown up and back over him.
- **f3:** reeling, turned away, the shield swinging round.
- **f4:** falling backward, the shield flung up showing its civ device, the sword flying away up and back.
- **f5-6:** dropping backward onto the ground, the shield landing at his feet.
- **f7:** **impact**: flat on his back.
- **f8-14:** lying still on his back: head toward the back, arms flung out wide, feet forward. The shield lies flat beyond his feet (forward) showing its device; the sword lands point-down in the ground well behind his head (it falls from high up at f8-11).
- **Off the anchor:** he ends about 1 m behind where he stood; the sword ends about 1.5 m behind.

## RUN (10 f, 66 ms, loop): a jog behind the shield

- **The carry:** the shield held forward on the left arm covering the body; the gladius in the right hand, point up, at chest height.
- **Stance:** torso leaning slightly forward, knees bent.
- **Stride:** f2 and f7 the widest, f0, f4-5 and f9 passing.
- **Anchor:** runs in place.

## FORTIFY (10 f, 66 ms): from side-on to a front-facing stand

- **f0-2:** side-on, the shield forward, the sword held low and forward.
- **f3-4:** the sword brought up across the body, the body turning.
- **f5-6:** turning to face the viewer, the sword lowered to the right side.
- **f7-9:** standing facing the viewer, feet apart, the shield at the left side, the sword low at the right. **f9 is the held fortify pose.**

## Notes for keyframing

- **Strike frames:** AttackA f8; Attack2 f4 (a thrust) and f9 (a cut).
- **Death:** falls backward, impact at f7. The shield ends forward of his feet; the sword ends upright in the ground behind his head.
- **Fortify still:** standing front-on behind the shield.
