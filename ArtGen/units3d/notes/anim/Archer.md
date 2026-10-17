# Archer: motion reference from the Civ3 originals

**Source:** `Art/Units/Archer/Archer.INI` and its flics (Civ3 Complete). Written from contact sheets of every frame in direction 3, where the archer faces screen-right. "Forward" is the way he faces, "back" behind him.

## Timing

- All flics have 8 directions. `[Speed] Normal=225`; every `[Timing]` value is 0.5.
- The game plays Death once; the rest loop. The Archer has only one attack flic.

| Action | Flic | Frames | ms/frame | Size (offset) |
|---|---|---|---|---|
| DEFAULT | ArcherDefault.flc | 15 | 125 | 46x50 (96,81) |
| ATTACK1 | ArcherAttackA.flc | 15 | 83 | 104x64 (68,62) |
| DEATH | ArcherDeath.flc | 15 | 83 | 92x72 (74,70) |
| RUN | ArcherRun.flc | **12** | 66 | 46x48 (97,79) |
| FORTIFY | ArcherFortify.flc | 10 | 83 | 60x57 (90,69) |

Run has 12 frames, not 10.

## The figure

A bare-armed archer with a civ-colored tunic and skirt, a dark cap, a quiver on the back, and a recurve bow in the left hand.

## ATTACK1 (AttackA, 15 f, 83 ms): loose, then draw and nock the next arrow

The flic starts at full draw: the shot is at the start.

- **f0-1:** **full draw**: feet apart, side-on, the bow arm straight forward at shoulder height, the right hand at the cheek, the arrow level.
- **f2:** **the release**: the arrow flies forward out of the frame (it leaves the flic's box at f2); the right hand snaps back behind the head, the bow arm still extended.
- **f3-4:** follow-through: the right arm stays flung back, the bow held forward.
- **f5-6:** the right hand reaches back over the shoulder to the quiver; the bow drops a little.
- **f7-8:** he draws an arrow from the quiver and brings it down and forward past the hip.
- **f9-10:** he nocks the arrow, the bow coming level.
- **f11-13:** drawing: the string pulled back toward the cheek.
- **f14:** full draw again (f14 = f0).
- **One shot per cycle, at f2** (the release). No travel.

## DEATH (15 f, 83 ms, once): struck, crumples forward-left and falls on his face

- **f0:** drawn bow, the stance of AttackA.
- **f1-2:** hit: the right arm flies out back, the body twisting, the bow swinging up.
- **f3-4:** staggering backward, the bow flying out of his hand forward and up.
- **f5-6:** dropping to his knees, the torso bending forward, one arm reaching down.
- **f7:** falling forward onto his front.
- **f8:** **impact**: face-down.
- **f9-14:** lying still face-down, stretched out, head toward the back (he twisted as he fell), one leg bent; the bow lies on the ground well ahead of him, about 1 m forward.
- **Off the anchor:** the body ends about 0.8 m behind the anchor, the bow ahead.

## RUN (12 f, 66 ms, loop): a running stride, bow carried low

- **The carry:** the bow in the left hand, held low and forward, angled across the body, the top limb forward. The right arm pumps.
- **Stance:** torso leaning about 15° forward.
- **Stride:** two steps per cycle; f3 and f9 the widest, f0, f5-6 and f11 passing.
- **Anchor:** runs in place.

## FORTIFY (10 f, 83 ms): from standing to a held full draw

- **f0:** standing side-on, the bow held low and forward with an arrow nocked.
- **f1-2:** raising the bow to shoulder height and turning toward the viewer.
- **f3-5:** drawing the string back to the cheek.
- **f6-9:** **holding at full draw**, the arrow level and pointing forward, feet apart. **f9 is the held fortify pose.**

## Notes for keyframing

- **Strike frame:** AttackA f2, the release. The arrow leaves at f2; Civ3 draws no arrow in flight beyond the flic.
- **Death:** falls forward onto the face, impact at f8; the bow ends forward.
- **Fortify still:** holding at full draw.
