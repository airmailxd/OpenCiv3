# Jaguar Warrior: realism design (2026-11-23)

## The real-world model

An Aztec jaguar knight (ocelotl), one of the two elite military orders of Tenochtitlan, about 1450-1521 (the
Codex Mendoza, the Florentine Codex, the Tlaloc and eagle-house sculptures):

- the **jaguar suit**: a jaguar skin over a quilted cotton armour (ichcahuipilli), the cat's head worn as a
  helmet with the man's face in its jaws; a feather crest;
- the **macuahuitl**: a flat wooden club of about 80 cm edged with obsidian blades on both sides;
- the **chimalli**: a round shield of wicker or wood with feathers and a painted device;
- gold and jade ornaments, a loincloth (maxtlatl), leg wraps.

Civ3's original: a jaguar-skin hood with a civ feather spray, a bare chest with a gold pectoral and a civ collar,
a civ belt and loincloth, civ wrist cuffs, spotted leg wraps, gold bands, a macuahuitl low and out, a round
shield with a civ face and a gold boss.

## The features that must read at 1x

1. The **jaguar head helmet** with its civ feather spray.
2. The **macuahuitl raised at the ready** by his right shoulder, its dark obsidian edges against a pale wood.
3. The **round civ shield** with dark spots and a gold boss, on the forearm, turned to the viewer.
4. Gold pectoral and bands over a bare chest; spotted leg wraps.

## The pose

Weight on the right leg, the left foot forward behind the shield; the club's head up by the right shoulder; the
torso upright.

## Civ colour

Stock: the shipped Jaguar Warrior 19-36 % (mean 26). Aim 15-30 % in every direction. Regions: the **feather
spray**, the **shield's face**, the **loincloth and belt**, the collar and cuffs.

## Stock neighbours

Warrior and Swordsman.

## Rounds

### Build rounds 1-2
- The macuahuitl raised by the right shoulder; the chimalli on the forearm, then held by its grip. Civ 22-35 %.

### Critic round 1 (batch 3, review/melee/batch_melee_3_r1.png): 66.5 / 61.5 = 64
Both: no jaguar suit (the torso and arms bare orange skin, spots only on the head and leg wraps); the cat head reads
as a mask on top; saturated orange. One: the club edge-on in 7, the shield edge-on in 8. Fixes: a spotted tawny suit
over the torso and arms ('top': 'sleeved' with a camo-fleck pattern).

### Critic round 2 (batch 3, batch_melee_3_r2.png): 73.5 / 70 = 71.75
Landed: the spotted suit reads at 1x; unit_scale 0.96 (the suit drew 1.11x the shipped area). Both: the cat head plain
orange beside the spotted suit, a pumpkin mask. One: the club edge-on in 7; the shield edge-on in 4 and 8. Fix: the
head's pelt the suit's tawny.

### Critic round 3 (batch 3, batch_melee_3_r3.png): 78 / 74.5 = 76.25
Landed: the cat head in the suit's spotted tawny, reading as a cat's head with ears and muzzle (continuity critic);
the fresh critic still reads a teddy-bear face from the front.

## Final: round 3, 76.25 (the cap)
Civ 21-35 % (mean 26); area x1.01 of the shipped (unit_scale 0.96). Open points: the macuahuitl edge-on in 7 (tilt it
so the obsidian shows); the shield edge-on in 4 and 8; a longer muzzle and open jaws framing the face; the rosette
suit down the thighs and shins (the wraps read as tiger bands).

Animations re-rendered from the final still (build.py --anim, 2026-11-23) and checked at 2x (directions 1, 3, 6): ATTACK1, FORTIFY, RUN and DEATH play cleanly; in direction 1 the thrust or cut points at the camera, so it reads foreshortened.
