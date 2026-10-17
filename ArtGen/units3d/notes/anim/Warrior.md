# Warrior: motion reference from the Civ3 originals

**Source:** `Art/Units/Warrior/Warrior.INI` and its flics (Civ3 Complete). Written from contact sheets of every frame in direction 3, where the warrior faces screen-right. "Forward" is the way he faces, "back" behind him.

## Timing

- All flics have 8 directions. `[Speed] Normal=225`; every `[Timing]` value is 0.5.
- The game plays Death once; the rest loop. ATTACK1 is the only attack the game plays; AttackB is noted for completeness.

| Action | Flic | Frames | ms/frame | Size (offset) |
|---|---|---|---|---|
| DEFAULT | WarriorDefault.flc | 15 | 125 | 51x49 (93,82) |
| ATTACK1 | WarriorAttackA.flc | 15 | 83 | 91x71 (73,71) |
| ATTACK2 | WarriorAttackB.flc | 15 | 83 | 94x70 (71,72) |
| DEATH | WarriorDeath.flc | 15 | 83 | 168x90 (36,73) |
| RUN | WarriorRun.flc | 10 | 66 | 51x52 (92,79) |
| FORTIFY | WarriorFortify.flc | 10 | 66 | 47x49 (95,81) |

Death is by far the widest flic: he travels about a body length back as he falls.

## The figure

A bare-chested warrior with long hair, a civ-colored loincloth and wrist wraps, striped leg wraps, and a stone-headed axe on a long haft (about 1.2 m), held in the right hand.

## ATTACK1 (AttackA, 15 f, 83 ms): wind-up and a lunging forward thrust

- **f0:** ready stance, knees bent, the axe held across the chest, head forward.
- **f1:** he turns slightly back; the axe comes up past the right shoulder.
- **f2-4:** the wind-up. The axe is raised high over and behind the head, and the front knee lifts high (f3-4 the highest knee, almost a hop).
- **f5:** coiled: the knee drops, the axe still high behind the head.
- **f6:** the step forward begins, the torso twisting.
- **f7:** **the strike**: a deep lunge, the front knee bent and the rear leg straight back; the axe is driven forward at shoulder height, the haft level and fully extended in front.
- **f8-10:** held extended in the lunge (the axe stays level and forward).
- **f11-12:** recovering: the feet come together, the axe pulled back toward the body, still pointing forward.
- **f13-14:** back to ready, the axe at the chest (f14 = f0).
- **One strike per cycle, at f7.** No travel: he lunges in place and returns.

## ATTACK2 (AttackB, 15 f, 83 ms): overhead chop into the ground

- **f0-1:** ready, the axe at the chest.
- **f2:** the axe swings up over the head.
- **f3-6:** the wind-up: the axe goes far back behind the head, head down behind him, his torso arching back (f5-6 the furthest).
- **f7:** the axe passes overhead.
- **f8:** the chop: he drops into a lunge, the axe sweeping down and forward.
- **f9:** **contact**: deep crouch, the axe head at the ground about 0.7 m in front.
- **f10-11:** held low, the axe on the ground in front.
- **f12-13:** rising, the axe lifted back to the chest.
- **f14:** ready.

## DEATH (15 f, 83 ms, once): knocked backward onto his back

- **f0:** ready stance, the axe at the chest.
- **f1-2:** hit: he recoils, the torso bending back, one knee coming up.
- **f3:** airborne, falling backward, the legs flung up.
- **f4:** he lands sitting, the legs still raised.
- **f5-6:** rolling onto his back; one arm flies up and the axe swings back over his head.
- **f7:** **impact**: flat on his back.
- **f8-14:** lying still on his back: head toward the back (away from where he faced), feet forward, one arm raised by the head. The axe lies on the ground beyond his head, about 0.5 m behind it.
- **Off the anchor:** he ends about 1.2 m behind where he stood.

## RUN (10 f, 66 ms, loop): a leaning trot, axe carried low

- **The carry:** the axe is held in the right hand low in front, the head forward at hip height, the haft angled back across the body. It barely moves.
- **Stance:** torso leaning about 15° forward, knees bent.
- **Stride:** two steps per cycle.
  - f0: crouched, the feet passing under the hips.
  - f1-3: one leg drives back, the other reaches forward; f2-3 the widest.
  - f4-5: passing.
  - f6-8: the other leg leads; f7 the widest.
  - f9: passing, back to f0.
- **Anchor:** runs in place.

## FORTIFY (10 f, 66 ms): from carry to a held guard

- **f0:** standing, the axe held forward at the hip, head forward.
- **f1-3:** the axe lowers and turns across the body, the haft level in both hands at the waist.
- **f4-5:** he straightens and brings the axe up.
- **f6-9:** the guard: the axe held upright in front of the chest, haft vertical, head by the shoulder, knees slightly bent. **f9 is the held fortify pose** (the still the game shows).

## Notes for keyframing

- **Strike frames:** AttackA f7 (the thrust is fully extended at f7); AttackB f9 (the ground contact).
- **Death:** falls backward, impact at f7; the axe lands behind the head.
- **Fortify:** the last frame is the still, so the guard pose matters most.
