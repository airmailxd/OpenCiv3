# Spearman: motion reference from the Civ3 originals

**Source:** `Art/Units/Spearman/Spearman.INI` and its flics (Civ3 Complete). Written from contact sheets of every frame in direction 3, where the spearman faces screen-right. "Forward" is the way he faces, "back" behind him.

## Timing

- All flics have 8 directions. `[Speed] Normal=225`; every `[Timing]` value is 0.5.
- The game plays Death once; the rest loop. ATTACK1 is the only attack the game plays.

| Action | Flic | Frames | ms/frame | Size (offset) |
|---|---|---|---|---|
| DEFAULT | SpearmanDefault.flc | 15 | 125 | 41x52 (96,75) |
| ATTACK1 | SpearmanAttackA.flc | 15 | 66 | 129x78 (54,72) |
| ATTACK2 | SpearmanAttackB.flc | 15 | 66 | 119x80 (58,68) |
| DEATH | SpearmanDeath.flc | 15 | 66 | 153x97 (43,62) |
| RUN | SpearmanRun.flc | 10 | 66 | 53x68 (90,62) |
| FORTIFY | SpearmanFortify.flc | 10 | 66 | 62x70 (86,62) |

The attacks are wide because the long spear is held level.

## The figure

A bronze-age spearman: a crested helmet with a civ-colored crest, a bare torso with a civ sash, a short civ kilt, a large round dark shield on the left arm, and a long spear (about 2.5 m) in the right hand.

## ATTACK1 (AttackA, 15 f, 66 ms): a two-step lunging thrust

- **f0:** the guard: crouched, the shield forward on the left arm, the spear held level at hip height, head forward, butt trailing back.
- **f1-3:** he draws the spear back: the right hand pulls the shaft back, the shield raised and turned to the front.
- **f4:** the lunge begins: he steps forward, the spear starting forward.
- **f5-6:** the thrust drives forward at waist height, the front knee bending deeply.
- **f7-9:** **full extension**: deep lunge, the rear leg straight; the spear thrust forward its full reach, level at waist height, the shield tucked in. The point is furthest forward at f8.
- **f10-11:** withdrawing: the spear pulled back, the feet coming together.
- **f12-14:** back to the guard (f14 = f0).
- **One thrust per cycle; strike at f8.** No travel.

## ATTACK2 (AttackB, 15 f, 66 ms): an overhand thrust

- **f0-1:** the guard, as AttackA f0.
- **f2:** the spear comes up; the right hand rises to shoulder height.
- **f3:** the spear is drawn back at shoulder height, overhand, the point forward.
- **f4:** he steps into the thrust, the shield forward.
- **f5-9:** **the thrust**: lunging, the spear driven forward at chest height, fully extended at f6-8 (strike f7).
- **f10-11:** withdrawing.
- **f12-14:** back to the guard.

## DEATH (15 f, 66 ms, once): spun and thrown backward, shield over him

- **f0:** the guard.
- **f1-2:** hit: he doubles over, the spear dropping across the body.
- **f3:** spun upright, the shield raised and flung back.
- **f4-5:** falling backward, one leg kicking up, the spear knocked out of his hands (it flies back over his shoulder).
- **f6:** he lands sitting, falling back.
- **f7-8:** rolling onto his back; the shield lands on edge behind him; the spear falls flat beside him.
- **f9:** **impact**: on his back.
- **f10-14:** lying still on his back, head toward the back, feet forward, one knee bent; the shield lies tilted against his head and shoulders; the spear lies flat on the ground beside him, parallel to the body.
- **Off the anchor:** he ends about 1.3 m behind where he stood.

## RUN (10 f, 66 ms, loop): a crouched charge behind the shield

- **The carry:** the shield carried forward on the left arm, covering the chest. The spear changes hold mid-cycle: in f0-4 it is held low, point forward and down; in f5-9 it is carried upright beside the body, point up. (The cycle reads as two strides with a carry change; a constant upright carry is fine.)
- **Stance:** a deep crouch, torso leaning about 25° forward.
- **Stride:** f2 and f7 the widest, f0, f4-5 and f9 passing.
- **Anchor:** runs in place.

## FORTIFY (10 f, 66 ms): from standing to the guard

- **f0-3:** standing upright, the shield on the left arm, the spear upright in the right hand (butt on the ground), turning slightly toward the front.
- **f4-5:** he lowers the spear forward and down, stepping into a crouch.
- **f6-9:** the guard: crouched, the spear held level at hip height pointing forward, the shield held in front. **f9 is the held fortify pose.**

## Notes for keyframing

- **Strike frames:** AttackA f8; AttackB f7.
- **Death:** falls backward, impact at f9; the shield ends over the head and shoulders, the spear flat beside the body.
- **Fortify still:** the level-spear guard behind the shield.
