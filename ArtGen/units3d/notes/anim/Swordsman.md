# Swordsman: motion reference from the Civ3 originals

**Source:** `Art/Units/Swordsman/Swordsman.INI` and its flics (Civ3 Complete). Written from contact sheets of every frame in direction 3. Here the swordsman stands three-quarter on, facing screen-right and toward the viewer; he strikes to screen-right. "Forward" is the way he faces, "back" behind him.

## Timing

- All flics have 8 directions. `[Speed] Normal=225`; every `[Timing]` value is 0.5.
- The game plays Death once; the rest loop. ATTACK1 is the only attack the game plays.

| Action | Flic | Frames | ms/frame | Size (offset) |
|---|---|---|---|---|
| DEFAULT | SwordsmanDefault.flc | 15 | 125 | 48x52 (95,78) |
| ATTACK1 | SwordsmanAttackA.flc | 15 | 66 | 92x76 (73,67) |
| ATTACK2 | SwordsmanAttackB.flc | 15 | 66 | 126x78 (55,72) |
| DEATH | SwordsmanDeath.flc | 15 | 83 | 137x92 (52,63) |
| RUN | SwordsmanRun.flc | 10 | 66 | 64x70 (85,63) |
| FORTIFY | SwordsmanFortify.flc | 10 | 66 | 75x59 (80,76) |

## The figure

A swordsman in a grey wolf-pelt or animal-head helmet, a civ-colored tunic over a leather kilt, leg wraps, a small round shield or buckler on the left arm, and a straight sword (about 0.8 m) in the right hand.

## ATTACK1 (AttackA, 15 f, 66 ms): a backswing, a forward cut, then twirls back to guard

- **f0:** the guard: a wide crouch, the sword held low and forward, the shield arm forward.
- **f1:** the sword swings back level at shoulder height behind him.
- **f2-3:** the backswing continues up and over: the sword raised high behind the head.
- **f4:** the body turns away (seen from the back), the sword coming over the top.
- **f5:** **the cut**: a wide lunge to the right, the sword swung forward and down to full reach at waist height, the blade level.
- **f6-9:** recovering: back into the crouch, the sword brought in close to the body (f8-9 held up at the chest).
- **f10-11:** a small flourish: the sword raised upright in front, then angled forward.
- **f12-13:** the sword lowered across the body to the left hip.
- **f14:** back to the guard, the sword low (f14 = f0).
- **One strike per cycle, at f5.** No travel.

## ATTACK2 (AttackB, 15 f, 66 ms): a lunging thrust

- **f0-1:** the guard, the sword low at the left; the sword brought across the body.
- **f2-3:** the sword drawn back level across the chest, the point forward.
- **f4:** stepping into a lunge, the blade level.
- **f5-9:** **the thrust**: a deep lunge, the sword thrust straight forward at shoulder height, fully extended (strike f6; held to f9).
- **f10-11:** withdrawing, the sword pulled back level.
- **f12-13:** the sword lowered across the body.
- **f14:** the guard.

## DEATH (15 f, 83 ms, once): struck, sword flung up, falls back onto his back

- **f0-3:** hit: he staggers in the guard, the sword dropping, then swinging out.
- **f4:** he rears back, the sword arm flung out behind.
- **f5:** spinning back, the sword held up and back.
- **f6:** falling backward, the sword raised high, the knees buckling.
- **f7-9:** collapsing backward onto the ground, the sword arm raised straight up; bits of debris scatter around him.
- **f10:** **impact**: on his back, the sword arm still raised.
- **f11-14:** lying still on his back: head toward the back, feet forward, the sword arm stretched back along the ground beyond his head with the blade flat. Small debris lies scattered around him.
- **Off the anchor:** he ends about 1 m behind where he stood.

## RUN (10 f, 66 ms, loop): a charging run, sword forward

- **The carry:** the sword in the right hand, forward and low in f0-3 (the point forward at hip height) and raised to an upward angle in f4-9 (the point up and forward at shoulder height). The shield is held forward at the chest.
- **Stance:** torso leaning about 15° forward.
- **Stride:** f2 and f7 the widest, f0, f4-5 and f9 passing.
- **Anchor:** runs in place.

## FORTIFY (10 f, 66 ms): from side-on to a front-facing guard

- **f0-2:** side-on and upright, the sword held level forward at waist height, the shield forward.
- **f3-4:** turning toward the viewer, the sword drawn back across the body.
- **f5:** facing front, the sword at the left hip.
- **f6-9:** the guard, facing the viewer: a wide stance, knees bent, the sword held low at his right side with the point down, the shield arm bent at the left. **f9 is the held fortify pose.**

## Notes for keyframing

- **Strike frames:** AttackA f5 (the cut); AttackB f6 (the thrust).
- **Death:** falls backward, impact at f10; the sword ends flat beyond the head. Optional: a few small debris bits.
- **Fortify still:** front-facing guard, sword low at the side.
