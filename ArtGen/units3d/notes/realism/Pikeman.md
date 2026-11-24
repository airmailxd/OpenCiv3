# Pikeman: realism design (2026-11-23)

## The real-world model

A pikeman of the late 16th century (the Spanish tercios, the Dutch and English pike of the 1580s-1600s):

- a **morion** of steel (the crested comb morion or the cabasset), brim turned up front and back;
- a **corselet**: a steel breastplate and backplate with **tassets** over the thighs, a gorget at the neck;
- a doublet with **puffed, slashed sleeves**, **breeches** to the knee, hose, shoes;
- the **pike** of 4.5-5.5 m in the field; Civ3 draws a shorter staff weapon with a broad head and side lugs
  (a partisan), about 1.3 times his height, which reads in the frame.

Civ3's original: a steel morion, breastplate and tassets, civ puffy sleeves, a civ cross on the
breastplate, dark civ breeches, cream stockings, brown shoes, a light beard, the partisan upright at his
right with the butt by his foot, the left arm relaxed.

## The features that must read at 1x

1. The **staff weapon held ready**: upright and a little forward in the right fist at chest height, the
   broad head with its lugs well above the morion, the shaft clear of the body.
2. The **steel morion's** brim and comb.
3. The **bright steel breastplate and tassets** with the civ cross.
4. The **civ puffed sleeves** and dark civ breeches over pale stockings.

## The pose

Weight on the right leg, the free left foot a little forward, knees soft; the torso upright; the right fist
at chest height on the shaft, the left fist above it (both hands on the pole, "ported" and ready to lower);
the head level.

## Civ colour

Stock: the shipped Pikeman 27-38 % (mean 32), Medieval Infantry 9-12 (11), Musketman (about 38). Aim 15-30
% in every direction. Regions: the **sleeves**, the **breeches** (darkened), and the **breastplate's cross**.

## Stock neighbours

Medieval Infantry and Musketman.

## Rounds

### Build rounds 1-2
- The partisan ported in both fists; the breeches made dark wool, not civ (the front read 43 %): 11-27 %.

### Critic round 1 (batch 1): 57 / 57.5 = 57.25
Both: reads as a spearman (a narrow head); the arms cross the body awkwardly; the weapon hides in direction 2
(critical, one critic); the civ cross invisible. Fixes: one fist on the shaft out at his right, tipped forward,
a broad partisan head (head 2.2, the lugs scaled), the left fist on the hip, a bigger cross.

### Critic round 2 (batch 1): 59 / 58 = 58.5
Landed: the partisan head, the civ cross, no crossed arms. Still: in direction 2 the head hides behind the body
(critical); balloon sleeves; the hand on the hip odd. Fixes: the fist and the shaft further out at his right, the
left arm relaxed, upper bulk 1.12.

### Critic round 3 (batch 1): 69 / 73 = 71
Landed: the partisan's head clears the body in direction 2, the relaxed arm. Still: smooth tube sleeves; the
partisan trails low and idle in 0-1; the stockings read as banded steel.

## Final (round 3)
Scores: R1 57 / 57.5 = 57.25; R2 59 / 58 = 58.5; **R3 69 / 73 = 71, final**. No critical failure. Civ 12-22 %
(mean 16). Open points: puffed and slashed sleeves with a pale lining; the stockings pale and plain (they come from
'mail_parts' legs); the partisan held at the port in both hands, longer.

### FORTIFY (after the final)
The lowered partisan pointed straight at the camera in direction 1 and vanished. The entry's new fortify_tool (anim.template_actions, off by default) angles it out to his right and a little up: the head reads in every direction at the hold.
