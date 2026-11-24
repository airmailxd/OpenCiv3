# Archer: realism design

## What archer this is

The Civ3 original (notes/anim/Archer.md) is a bare-armed archer in a civ-coloured tunic and skirt, a dark
cap, a quiver on his back and a recurve bow. The shipped remake made him a bare-chested man in a long
loincloth with a topknot, a strap over the chest: no real archer of the period. A tunic, a dark cap and a
recurve bow on a bare-armed man is exactly a **Cretan archer** of the 4th to 2nd centuries BC, the most
famous archers of the Greek world, hired by Greek cities, by Alexander and the Successors, and by Rome:

- the **exomis**: the working tunic of wool, pinned on the left shoulder, leaving the right shoulder and arm
  bare (the draw arm), belted at the waist, to mid-thigh;
- a **pilos**-like felt cap (dark, close);
- a **composite recurve bow**, short, with the tips curling forward; a leather **quiver** on the back, arrows
  fletched in pale feather;
- bare legs, sandals or laced boots (krepides); a short beard.

## The features that must read at 1x

1. The **bow held up and out** from the body, an arrow nocked: the recurve's outline in front of him.
2. The **one-shouldered tunic**: one bare shoulder and arm, the rest of the torso covered.
3. The **quiver** on the back, fletching up over the right shoulder.
4. The **dark cap** on the head.

## The pose in words

The archer at the ready: weight on the right (rear) leg, the left foot forward, the knee soft; the pelvis
rolled over the weight leg and turned a little to the left, the shoulders counter-rolled. The torso upright.
The left (bow) arm out forward and down, the bow upright in the fist, its back toward the target; an arrow
nocked, the right fist at the nock on the string at the waist, the arrow pointing forward and down at the
ground ahead (the archer's "ready" before raising and drawing). The head level, looking where the arrow
points.

## Civ colour

**The exomis (tunic and its skirt) and the cap's band**, nothing else: the tunic is the original's civ-coloured
garment and a single large deliberate region; the bare right shoulder, arms and legs and the leather quiver
break it. The shipped loincloth and chest strap are gone. Target, measured on the stock foot neighbours' DEFAULT sheets (tint pixels / unit pixels per direction,
2026-11-23): Hoplite 7-18 % (mean 11), Swordsman 7-10 (9), Spearman 11-24 (16), Pikeman 29-37 (34),
Longbowman 5-13 (9), Bowman 21-32 (26). So about 10-20 %, and roughly even across the eight directions.

## Rounds

### Round 1

- The bow now reads at 1x in every direction: upright out in front at the chest, an arrow nocked, the right
  fist on the string. The exomis's bare right shoulder reads from the front; the cap and quiver read.
- Civ colour 29 % mean (20-36): high against Spearman 16 / Bowman 26. The skirt hangs to the knee; a real
  exomis is shorter. Next: `skirt_length` 0.42 (mid-thigh), which also trims the colour.

### Round 2

- The exomis now ends at mid-thigh, belted; civ colour 23 % mean (15-29), between Spearman and Bowman.
- From the sides and behind the bow and nocked arrow read clearly. From the front (directions 0-1) the bow is
  near edge-on and the arrow points at the viewer: a thin line. Next: aim the arrow across to the left
  (`arrow_dir` x 0.4), so the bow turns its face to the front.

### Round 3

- Turned across to the left, the bow shows its curve in the front views too. The figure reads as an archer
  at the ready from all eight directions at 1x.
- Thin on detail at the belt. Next: a short sword in its scabbard at the left hip (Cretan archers carried
  one), a second era read and a break in the tunic.

### Round 4

- The short sword shows at the left hip as a dark diagonal in the side views; small at 1x, but true to the
  type and it breaks the tunic's outline. Civ colour 22 % (15-29). Holding the still here; the animations next.

### Round 5 (the animations)

- ATTACK1: the bow raised level, the nocked arrow drawn to the cheek, loosed (hidden at f5), back to the
  ready. RUN: bow and nocked arrow carried forward, the stride clean. DEATH: struck, falls back, bow in hand
  (the template's fall, as the shipped unit).
- FORTIFY was wrong: the template's arms pulled the right hand off the string, the arrow left hanging from
  the fist below the bow. Now a bow at the ready keeps the stance's arms (the arrow stays nocked) and the
  body crouches. Civ colour 22 % (15-29), within the stock neighbours' range.

### Critic round 1 fixes (69)

- The bow arm out to his left, the arrow aimed out and forward: the bow clears the body in S and SW.
  Limbs thicker (`bow_thick` 2.3) and lighter (horn-brown).
- Short brown beard, lighter skin and felt cap; built like the stock Bowman (physique 0.3, bulk 1.28,
  proportions 0.62; physique 0.8 / bulk 1.38 made him a hulk with a small head); a dark hem band.
- Civ colour by direction: 15 26 22 15 23 28 22 16, mean 21 %.

### Critic round 2 fixes (67.75)

- The bow half drawn (a D, the string inside the limbs), recurve tips, darker limbs, held upright out at his
  left; dark brown felt cap; a bigger sword at the hip. Civ colour 23 26 22 12 22 26 21 15, mean 21 %.
- The DEFAULT cell is now 78x84 with offset (86, 61), against the shipped 78x71 at (85, 73): the bow's top
  rises above the head.

### Critic round 3 fixes (72)

- No physique target, bulk 1.15 (the deltoid ball), the quiver strap over the right shoulder, leather
  bracers, a darker heavier hem; the bow arm out to his left at shoulder height, a shorter bow (1.15), the
  drawing hand at the chest. Civ colour 26 27 23 13 20 24 20 15, mean 21 %. Cell 69x80.

### Critic round 4 fixes (72.5)

- A drawn pose: bow forward-left at chest height, canted (`bow_up` x 0.4), drawn to the jaw (`draw` 0.55,
  `bow_depth` 1.25 so the draw stays within reach), the draw elbow out (`draw_elbow`). Directions 1-3 and
  5-7 read as an archer drawing; direction 0 still hides the bow behind the body (the shared pose turned).
- Tried dark fold stripes and pleats on the tunic: they read as a barcode and untinted it; dropped. Fletching
  toned down, a felt-brown cap with a brim, darker hair. Civ colour 16 24 21 15 20 26 21 17, mean 20 %.

## Final (critic round 5)

Scores (picture-only critics, two per round): R1 70/68 = 69; R2 73/62.5 = 67.75; R3 75.5/68.5 = 72;
R4 76.5/68.5 = 72.5; **R5 77/70.5 = 73.75, final** (plateau, no critical failure). Animations re-rendered
from the round-5 still (RUN, ATTACK1, DEATH, FORTIFY): the arrow stays nocked through FORTIFY; ATTACK1's
f5-7 is the release follow-through, the draw hand flung up and back. Pictures: reviews/realism/archer/.

Open points:
- direction 1: the bow is lost against the torso; bow, arrow and scabbard merge into one diagonal;
- direction 6: the bow is edge-on and reads as a rod;
- the scabbard is too long for a short sword;
- the tunic is flat: it needs shading under the belt and at the armpit (not fold lines, which read as a barcode);
- he is a little slight beside the stock Bowman;
- the brief's "at the ready" against the full draw: a later pass could try a low forward ready hold (as the
  stock Bowman's), which shows in every direction.
