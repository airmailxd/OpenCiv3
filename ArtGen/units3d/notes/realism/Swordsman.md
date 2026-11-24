# Swordsman: realism design (2026-11-23)

## The real-world model

An Iron Age swordsman of the Mediterranean, about 400-200 BC: a Celtiberian or Italic (Samnite) infantryman
of the kind Rome met and copied, with the straight cut-and-thrust sword that became the gladius hispaniensis.

- an **iron helmet** (Montefortino type: a bowl with a knob and a short neck guard), cheek pieces;
- a **leather or linen cuirass** with shoulder pieces, over a tunic; a skirt of leather strips (pteruges);
- a **straight double-edged sword** of about 65 cm, with a bar guard and a round pommel;
- a **round shield** (the caetra of the Iberians, about 55-60 cm), its face painted, an iron boss and rim;
- greaves on the shins, boots or sandals.

Civ3's original: an iron helmet with a brow ridge (reads horned), brown leather armour over a skirt of leather
strips, grey greaves, civ colour on the upper arms and on the small round shield's rim and boss; a straight
sword of about 0.8 m.

## The features that must read at 1x

1. The **sword held ready**: the right fist at the hip, the blade forward and up, out at his right side; a
   bright edge, a dark hilt, at least 2 px wide.
2. The **round shield** on the left arm, upright and turned to the viewer, its civ rim and boss reading.
3. The **iron helmet** with its crest knob and brow (the original's horned read).
4. The **brown leather cuirass with civ sleeves** and the strip skirt, grey greaves.

## The pose

Weight on the right leg, the left foot a half step forward; the shield forward on the left forearm, its
face to the front-left; the right elbow bent at the side, the fist at the hip, the blade forward-out and a
little up; the torso upright, shoulders counter-rolled, the head level.

## Civ colour

Stock: the shipped Swordsman 7-15 % (mean 11), Legionary 22-34 (28), Hoplite 19-29 (23), Medieval Infantry
9-12 (11). Aim 15-30 % in every direction. Regions: the **tunic's sleeves** under the shoulder pieces, the
**shield's face device and rim**, and the **helmet's crest** (a short horsehair plume on the knob, as the
Montefortino carried one).

## Stock neighbours

Legionary and Medieval Infantry.

## Rounds

### Build rounds 1-2
- The Legionary's shield_ready stance with a round shield on the forearm, its cross upright, a battened back.
  Found: the round shield's civ cross (face keys) never reached the tint mask; it is now raised tinted bands.

### Critic round 1 (batch 1): 58 / 61 = 59.5
Both: the horned helmet reads Viking; the blue shoulder balls; the shield hand pokes through the boss in 0-1
(critical, one critic); the blade thrust sideways. Fixes: a Montefortino bowl with a knob and a civ plume, no
pauldrons (civ short sleeves), the shield further from the fist, the blade more forward.

### Critic round 2 (batch 1): 66 / 71 = 68.5
Landed: the Montefortino helmet, the blade forward. Still: the fist on the shield face in 1; the blue shoulders read
as balls; one brown mass at 1x (shield, cuirass, skirt); the blade like a horn behind the helmet in 6. Fixes: the
shield by its grip; the civ moved to a painted shield face, the plume and a sash, the sleeves madder wool; arms
slimmer; the blade lower and more forward.

### Critic round 3 (batch 1): 65 / 65 = 65
Landed: slim sleeves, sash, plume, blade. Critical: a skin patch at the boss in 0-1 (the fist), and the civ colour
3-7 % from behind. Extra round (aimed at both): civ on full tunic sleeves, the cuirass's edge binding and the skirt's
hem (now 11-20 %); the board moved... the wrong way: solo 66 / 65 = 65.5 with the fist bigger. Cause: 'grip_depth'
is subtracted along the board's back axis, so a negative value pulls the board toward the body. Fixed afterwards
(grip_depth +0.05): the fist is behind the board in every direction at 3x (checked by me, not re-scored).

## Final (the extra round with the grip fix)
Scores: R1 58 / 61 = 59.5; R2 66 / 71 = 68.5 (best score, but with the fist on the shield, critical); R3 65 / 65 =
65; solo 66 / 65 = 65.5. **Final: the solo round's model with the grip fixed** (critical failures fixed, not
re-scored by critics; recorded as stuck below 85). Civ 11-20 % (mean 15). Open points: the shoulder caps read as
blobs, they want elbow sleeves; seams and a lighter edge on the leather cuirass; dull the white greaves to iron;
in direction 6 the blade tip reads as a horn beside the helmet.

### Check of the grip fix (continuity critic, solo sheet): 71
The hand is gone from the shield face in all eight directions; no critical failure left. Not re-scored by a fresh
critic. Open: elbow sleeves, binding and seams on the cuirass.

### Fresh critic on the grip-fixed model (solo, review/melee/swordsman_solo.png): 65.5
No critical failure. Open: a flat cuirass (no edging, shoulder straps, panels); the strip skirt reads as a fringe; the
civ loincloth reads as "blue briefs" (wants a mid-thigh tunic hem); the helmet small, wanting the knob and cheek
guards at 1x; the rear views 5-7 almost all brown (the shield's plain back).

## Final score
**(71 + 65.5) / 2 = 68.25** (continuity critic's grip check and a fresh critic, both on the grip-fixed model).
