# Worker: motion reference from the Civ3 originals

**Source:** `Art/Units/Worker/Worker.INI` and its flics (Civ3 Complete). These notes were written from contact sheets of every frame in direction 3, a side view with the worker facing screen-right. In these notes, "forward" means the way he faces and "back" means behind him.

## Timing

**Common facts:**
- All flics have 8 directions.
- The INI gives `[Speed] Normal=225`, and every `[Timing]` value is 0.5.
- `STOP_AT_LAST_FRAME` is empty, so the INI marks no flic as play-once. The game decides that: Death and Captured play once, the rest loop.
- The frame time in the table is the flic's own speed.

| Action | Flic | Frames | ms/frame | Loop | Tool |
|---|---|---|---|---|---|
| DEFAULT | WorkerDefault.flc | 15 | 125 | yes | shovel (spade) |
| RUN | workerRun.flc | 10 | 66 | yes | shovel |
| PLANT | WorkerPlant.flc | 10 | 100 | yes | seed basket and a handful of seed |
| IRRIGATE | workerIrrigate.flc | 10 | 100 | yes | long-handled hoe |
| MINE | workerMine.flc | 10 | 100 | yes | pickaxe |
| ROAD | workerRoad.flc | 10 | 100 | yes | mattock or grub hoe (short head on a long haft) |
| FOREST | workerForest.flc | 10 | 83 | yes | felling axe |
| JUNGLE | workerJungle.flc | 10 | 83 | yes | machete |
| FORTRESS | workerFortress.flc | 10 | 83 | yes | mallet, and a stake held upright |
| DEATH | workerDeath.flc | 15 | 83 | once | shovel (dropped) |
| CAPTURE | workerCaptured.flc | 15 | 83 | once | shovel (dropped, then picked up) |

**Flic sizes and offsets** in the 240x240 unit space, as (width x height, offset):
- Default 50x51 (93,80), Run 48x48 (96,77)
- Death 80x60 (80,81)
- Road 64x54 (88,78), Mine 60x66 (90,65), Irrigate 74x57 (83,76), Fortress 40x48 (100,80)
- Captured 52x54 (94,78), Jungle 50x53 (95,72), Forest 46x54 (97,71), Plant 67x50 (88,80)

Death is the widest because he lies along the ground. Mine is the tallest because the pick rises overhead.

## The figure (all flics)

A bare-chested worker in a straw hat, with a short civ-colored kilt and sandals. He stands with his knees a little bent and his torso slightly forward.

**Correction for our build:** keep the upright torso (UPRIGHT) in the standing poses. The working poses below bend at the hips, so that bend is intended.

## DEFAULT (15 f, 125 ms, loop): idle with the shovel

- **f0-14:** he stands still, feet a little apart.
- **The shovel:** held upright in front of the right shoulder. The right hand grips it at chest height, and the blade points up beside the head (blade high, handle down).
- **Motion:** an almost static, very slight breathing sway. No frame differs by more than about 1 px, so a static pose or a slow ±2° sway is fine.

## RUN (10 f, 66 ms, loop): a crouched trot

- **The tool:** the shovel is held upright in both hands in front of the chest, blade above the head. It stays in that position through the whole cycle.
- **Stance:** a low crouch, the torso leaning about 15° forward, knees deeply bent.
- **Stride:** two steps per cycle, one per leg. The cycle repeats once within the 10 frames.
  - f0: feet together under the hips, the lowest crouch.
  - f1-3: a long stride, one leg reaching far forward and the other trailing far back. f2-3 are the widest.
  - f4-5: passing, feet together.
  - f6-8: the other leg leads. f7 is the widest.
  - f9: passing, then back to f0.
- **Anchor:** he runs in place.

## PLANT (10 f, 100 ms, loop): sowing seed

- **Stance:** standing upright, feet apart.
- **The basket:** a small seed basket held at the waist in front in the left hand. The right hand dips into it at f0-3.
- **f4:** the right arm swings forward and down, the hand at about knee height in front, and seed sprays out.
- **f5-6:** the seed falls to the ground about 0.5-0.8 m in front, as small dark particles that hit the ground at f6.
- **f7-9:** the right hand returns to the basket.
- **One throw per cycle.** He doesn't bend, apart from a slight lean at f4-5.

## IRRIGATE (10 f, 100 ms, loop): hoeing a ditch

- **The tool:** a long-handled hoe (a haft of about 1.6 m with a small blade), held in both hands. The rear hand is at the hip and the front hand halfway up the haft.
- **Stance:** a wide stance, knees bent, the torso bent forward about 35°. He stays bent throughout.
- **Keys:**
  - f0-1: the haft is level and pushed far forward, the blade about 1.1 m in front at waist height.
  - f2: the blade drops.
  - f3-4: contact, the blade biting into the ground about 1.0 m in front.
  - f5-6: drag back, the blade raised a little.
  - f7: second contact.
  - f8-9: push forward again.
- **Two chops per cycle**, at f3 and f7. The body bobs slightly with each chop.

## MINE (10 f, 100 ms, loop): the pickaxe

- **The tool:** a pickaxe in both hands, at the end of the haft.
- **f0:** standing, the pick held across the chest, head forward.
- **f1-4:** the wind-up. The pick rises over the head and back, and the torso arches back slightly.
  - f2-4: the pick is horizontal above and behind the head, its head behind him.
  - f4: the top of the swing.
- **f5:** the down-swing, with a bright glint on the pick point. The haft passes vertical in front of the face.
- **f6-7:** the strike. The torso is bent about 60° forward, knees bent, and the pick head is at the ground about 0.5 m in front. f6 is the contact frame.
- **f8:** levering it up.
- **f9:** straightening, the pick upright in front, then back to f0.
- **One strike per cycle**, at f6.

## ROAD (10 f, 100 ms, loop): grubbing and levelling

- **The tool:** a mattock or grub hoe on a long haft, in both hands.
- **f0-4:** bent forward about 45°, scraping. The head is on the ground about 0.6-0.8 m in front, dragged back towards the feet. f4 is the furthest forward reach, with the haft lowest.
- **f5:** the lift starts, the haft diagonal.
- **f6-7:** the haft is near vertical in front, the head raised above the hat. Dirt particles flick up off the blade.
- **f8:** the swing down. The dirt flies in an arc in front, and the torso bends again.
- **f9:** the head is at about knee height in front, just before contact, then back to f0.
- **Contact:** at f9 into f0. One raise-and-chop per cycle, plus the scrape.

## FOREST (10 f, 83 ms, loop): felling with an axe

- **The tool:** a felling axe in both hands.
- **f0:** standing upright, the axe hanging low at the right side, head down.
- **f1:** the axe raised over the right shoulder.
- **f2-4:** the wind-up. The axe is behind the head, horizontal, its head well behind. f3-4 are the top.
- **f5:** the swing. The axe passes vertical in front.
- **f6-7:** the strike. Bent about 50° forward, knees bent, the axe head low in front at shin or trunk height, about 0.6 m ahead. f6 is contact.
- **f8:** pulling back up.
- **f9:** upright, the axe vertical in front, then back to f0.
- **One chop per cycle.**

## JUNGLE (10 f, 83 ms, loop): clearing with a machete

- **The tool:** a machete in the right hand only. The left hand is free at the side or chest.
- **Stance:** a slight crouch, the torso about 15° forward.
- **Keys:**
  - f0: the machete raised in front, near vertical.
  - f1: high, over and behind the head.
  - f2: swinging down and forward.
  - f3: slash one, the blade low and horizontal in front at knee height, about 0.5 m ahead.
  - f4: the back-swing up diagonally.
  - f5-6: overhead again (f6 is the highest, the blade glinting above the hat).
  - f7: the down-swing.
  - f8: slash two, low in front at shin height.
  - f9: recovery, the blade rising.
- **Two slashes per cycle**, at f3 and f8.

## FORTRESS (10 f, 83 ms, loop): driving a stake

- **Stance:** crouched low (deep squat, knees fully bent), the torso bent forward.
- **The tools:** the left hand steadies an upright wooden stake or post planted in front of him, about 0.3 m ahead, reaching up to his shoulder. The right hand swings a short mallet or hammer.
- **f0:** the mallet head resting on top of the stake.
- **f1-4:** the mallet raised up and back over the head. Its silver head is above the hat at f1-3, the highest at f2.
- **f5:** the down-blow, contact on the stake top.
- **f6-9:** a follow-through, the mallet resting on the stake and the body still. Then it repeats.
- **One blow per cycle**, at f5.

## DEATH (15 f, 83 ms, once): falling backwards

- **f0:** the default stance with the shovel.
- **f1-3:** hit. He recoils, knees buckle, hunching, and his arms come in.
- **f4:** falling back and down, sitting, his weight going backward.
- **f5-6:** toppling onto his back. The shovel flies out of his hands forward.
- **f7:** his back hits the ground (the impact frame). One arm flies up.
- **f8:** the arm is still raised, then drops.
- **f9-14:** lying still on his back.
  - His head lies behind (back from the anchor), his feet toward the front, his legs slightly bent and his arms at his sides.
  - The shovel lies on the ground beside and beyond his feet, about 0.8-1.2 m ahead, the blade flat.
- **Off the anchor:** the body ends about one body-length long along the ground, from about 0.6 m behind the anchor to the feet just ahead of it. The tool ends forward.

## CAPTURE (15 f, 83 ms, once): surrender, then picked up as captured

- **f0-1:** standing, as in Default, with the shovel.
- **f2:** he throws or drops the shovel down in front. It lands flat on the ground about 0.6 m ahead and stays there through f2-9.
- **f3-8:** he cowers. He drops into a deep crouch or half-kneel, the torso bent far forward, head down and both hands raised in front: submission, not bound. f5-7 are the lowest.
- **f9-10:** he rises and reaches down to the shovel.
- **f11-14:** he stands again with the shovel back in the Default carry. f14 matches Default f0, so the captured worker returns to idle.
- **Anchor:** no travel. Only the tool goes out in front.

## Notes for keyframing

- **Strike frames, for any impact sound or dust:**
  - Mine: f6.
  - Forest: f6.
  - Road: f9 into f0.
  - Irrigate: f3 and f7.
  - Jungle: f3 and f8.
  - Fortress: f5.
  - Plant: seed release at f4, ground at f6.
- **Tool swaps per flic:** shovel for Default, Run, Death and Capture; seed basket for Plant; hoe for Irrigate; pickaxe for Mine; mattock for Road; axe for Forest; machete for Jungle; mallet plus a stake prop for Fortress.
- **Uncertain from the 1x pixels:**
  - which hand leads on the two-handed tools (the right hand reads nearer the head in most flics);
  - the Road tool's exact head (mattock or hoe).
  Check other directions if it matters.
