# Medieval Infantry: realism design (2026-11-23)

## The real-world model

A man-at-arms on foot of about 1250-1300 (the Maciejowski Bible, the effigies of the period, Falkirk and
Courtrai):

- a **great helm** (a flat-topped or sugarloaf barrel helm of riveted plates, eye slits and breaths);
- a **hauberk of mail** to the knee with mail sleeves and mittens, **mail chausses** on the legs;
- a **surcoat**: a long sleeveless linen coat over the mail, split front and back for walking, belted, in
  the lord's or the man's colours;
- a **shield**: a wooden heater or a round target, painted; a **flail** or a mace or a falchion.

Civ3's original: a closed great helm, full mail, a civ surcoat from the shoulders down with a gold hem and a
brown belt, a small round brown wooden shield, a flail with its chain and spiked ball at the knee.

## The features that must read at 1x

1. The **flail held ready**: the haft up in the right fist at the shoulder, the chain and spiked head
   hanging from its top, out at his right side, clear of the body (the shipped one hung at the knee).
2. The **great helm**: a dark-slitted steel cylinder for a head.
3. The **civ surcoat** over grey mail, with a gold hem and a belt.
4. The **shield** on the left arm, upright, turned to the viewer.

## The pose

Weight on the right leg, the left foot forward; the shield forward on the left forearm, its face to the
front-left; the right fist at shoulder height in front of the shoulder, the flail's haft up and out, the head
hanging from it; the torso upright.

## Civ colour

Stock: the shipped Medieval Infantry 9-12 % (mean 11), Pikeman 27-38 (32), Swordsman 7-15 (11). Aim 15-30 %
in every direction. Regions: the **surcoat** (front and back, broken by the belt and the gold hem), and a
civ **device on the shield** (a cross); the mail, helm and wood stay in their materials.

## Stock neighbours

Pikeman and Swordsman.

## Rounds

### Build rounds 1-2
- The flail raised ready at the shoulder; the surcoat lengthened (0.72) so the colour shows from behind: 13-22 %.

### Critic round 1 (batch 1): 62 / 54 = 58
Both: the shield hand pokes through the shield in 0-1 (critical); smooth bright grey mail reads as a robot; the
flail lies on the helm in 2; the surcoat a flat civ field (the critics read 35-40 %, measured 16 %). Fixes: the
shield further from the fist, dark iron mail and sabatons, the flail further out, a belt across the surcoat.

### Critic round 2 (batch 1): 64 / 65 = 64.5
Landed: the flail out at his side, the belt. Still: the shield hand in front of the board in 0-1 (critical); smooth
mail. Fixes: the shield by its grip; mail in visible courses (new shared option 'mail_rows': a ring-row value
pattern, garments.mail_rows_nodes).

### Critic round 3 (batch 1): 67 / 61 = 64
Critical: the glove on the shield face in 0-1 (one critic), 'out of the house style' (the other: rubber mail, a
skull helm). Extra round: lighter steel mail with stronger ring courses, slimmer arms (1.12); the board moved the
wrong way (see the Swordsman): solo 69 / 63 = 66. The grip fixed afterwards (grip_depth +0.05): the hand is behind
the board in every direction at 3x (checked by me, not re-scored). The shield's civ cross shows now (the hand had
hidden it from the tint mask before).

## Final (the extra round with the grip fix)
Scores: R1 62 / 54 = 58; R2 64 / 65 = 64.5; R3 67 / 61 = 64; solo 69 / 63 = 66. **Final: the solo round's model with
the grip fixed** (recorded as stuck below 85). Civ 16-27 % (mean 21). Open points: mail rings that read at 2x
(mail_rows is still too fine); a flat-topped great helm with a sight slit and breaths; the flail's head hanging
from its chain in 4-5 (the prop's chain is rigid); a heraldic charge or a split skirt to break the surcoat (the
critics read it at a third of the figure).

### Check of the grip fix (continuity critic, solo sheet): 71
The hand is gone from the shield face in all eight directions; no critical failure left. Open: ring courses that
read at 2x; a heraldic charge to break the surcoat.

### Fresh critic on the grip-fixed model (solo, review/melee/medieval_infantry_solo.png): 56.5
Borderline critical (one critic): the flail's head stands stiff beside the haft in 5-7, reading as a mace or stick;
the domed helm and smooth grey limbs lose the 13th-century identity at 1x. Open: a hanging chain (the prop's chain is
rigid), a flat-topped great helm with an eye slit and breaths, mail rings that read, a split and folded surcoat.

## Final score
**(71 + 56.5) / 2 = 63.75** (continuity critic's grip check and a fresh critic, both on the grip-fixed model).
Reported as stuck: the flail's rigid chain and the helm want new props (a hanging chain, a flat-topped great helm).
