# Musketman: motion reference from the Civ3 originals

**Source:** `Art/Units/Musketman/Musketman.INI` and its flics (Civ3 Complete). Written from contact sheets of every frame in direction 3, where the musketman faces screen-right. "Forward" is the way he faces, "back" behind him.

## Timing

- All flics have 8 directions. `[Speed] Normal=225`; every `[Timing]` value is 0.5.
- The game plays Death once; the rest loop. The Musketman has only one attack flic.

| Action | Flic | Frames | ms/frame | Size (offset) |
|---|---|---|---|---|
| DEFAULT | MusketmanDefault.flc | 15 | 125 | 31x56 (105,71) |
| ATTACK1 | MusketmanAttackA.flc | 15 | 83 | **183x117** (29,32) |
| DEATH | MusketmanDeath.flc | 15 | **66** | 99x76 (71,68) |
| RUN | MusketmanRun.flc | 10 | 66 | 42x58 (99,73) |
| FORTIFY | MusketmanFortify.flc | 10 | 66 | 77x66 (82,70) |

The attack flic is huge because the muzzle flash and smoke cloud drift well out in front.

## The figure

A 16th/17th-century musketeer: a steel morion with a civ plume, a civ-colored doublet with a grey bandolier and belts, civ breeches, grey stockings, a long matchlock musket, and a forked musket rest (about 1.2 m) planted in front.

## ATTACK1 (AttackA, 15 f, 83 ms): fire from the rest

He is already aimed when the flic starts; there is no reload in it.

- **f0-4:** aiming: a wide braced stance, the front knee bent; the musket laid in the fork of the rest, level at shoulder height, the butt at the right shoulder, the rest planted about 0.6 m in front. Almost still.
- **f5:** **the shot**: a bright muzzle flash at the muzzle with a puff of smoke; a slight recoil, the shoulders rocking back.
- **f6-7:** the flash fading into a growing smoke puff just in front of the muzzle.
- **f8-14:** the smoke cloud drifting forward, about 1 m in front of the muzzle and slightly lower, thinning out; he holds the aim. The smoke is still there at f14.
- **One shot per cycle, at f5.** No travel.

## DEATH (15 f, 66 ms, once): drops the musket and rest, slumps to sitting

- **f0:** aiming on the rest.
- **f1-2:** hit: he straightens and rocks back; the musket tips up off the rest and the rest starts to fall forward.
- **f3-4:** buckling: one knee comes up, he sinks back; the musket swings across in front of him.
- **f5:** sitting down hard, the legs forward.
- **f6-7:** sliding back onto one elbow; the musket and rest fall flat on the ground in front of him (f7 the musket hits the ground).
- **f8-14:** half-lying, propped on one elbow, legs stretched forward, head drooping back; the rest and musket lie crossed on the ground in front of his feet.
- **The fall:** a backward slump to sitting and reclining, not flat on the back. **Off the anchor:** he ends about 0.6 m back; the weapons end about 0.7 m forward.

## RUN (10 f, 66 ms, loop): a running stride, musket at the port

- **The carry:** the musket held diagonally across the body at the port, the muzzle up at the left shoulder, the butt low at the right hip; the rest is not shown in the run.
- **Stance:** torso leaning about 10° forward.
- **Stride:** f2-3 and f7-8 the widest, f0, f4-5 and f9 passing.
- **Anchor:** runs in place.

## FORTIFY (10 f, 66 ms): from carry to aiming on the rest

- **f0-2:** standing, the musket on the right shoulder (muzzle back over the shoulder), the rest upright in the left hand, planted.
- **f3-4:** the musket swings down off the shoulder to upright in front of him.
- **f5-6:** lowering the musket forward, swinging it into the fork of the rest.
- **f7-9:** aiming: the musket level in the fork, the braced stance of AttackA f0. **f9 is the held fortify pose.**

## Notes for keyframing

- **Strike frame:** AttackA f5, a flash plus smoke. The smoke persists to f14, so render it as an effect layer that drifts forward and thins; the size of the flic comes from the smoke.
- **The rest:** a separate prop, planted in front during the attack and fortify, dropped in the death; not carried in the run.
- **Death:** slumps back to sitting and leaning on an elbow; weapons end forward.
- **Fortify still:** aiming on the rest.
