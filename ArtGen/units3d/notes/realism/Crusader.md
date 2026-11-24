# Crusader: realism design (2026-11-23, realism/misc)

## The real-world model
A knight of the Third Crusade on foot, about 1190-1250 (the Templar and Hospitaller effigies, the
Maciejowski Bible): a flat-topped great helm with eye slits and a cross-shaped brace; a mail hauberk with
mail sleeves and chausses; a long linen surcoat over the mail, belted, marked with the crusader's cross front
and back; a heater shield painted with the cross; an arming sword. Civ3's original is on foot too (not a
horse_rider): great helm, sword low in the right hand, a heater shield with a white cross, a civ surcoat.

## The features that must read at 1x
1. The great helm (a flat-topped steel barrel with a dark slit and a gold cross brace).
2. The heater shield on the left arm, upright and turned to the viewer: civ face, pale cross.
3. The pale surcoat over dark mail, with the civ cross on chest and back.
4. The sword held ready at the hip, point forward and out.

## The pose
The melee family's shield_ready: weight on the right leg, the shield by its grip on the left, the sword ready
at the right hip, the torso upright.

## Civ colour
Stock: the shipped Crusader 15-58 % (mean 35: a whole civ surcoat), Medieval Infantry 13-42 (23), Knight
13-39 (31), Swordsman 7-11 (9). Aim 15-30 %. Regions: the shield's face, the cross on the surcoat front and
back, the shoulders and the surcoat's hem. The surcoat itself undyed linen (the realism rule: never a whole
coat in civ colour).

## Stock neighbours
Medieval Infantry and Knight.

## Rounds
### Build rounds 1-3 (drafts b1-b3, then a1 at HEAD on the GPU)
- b1: the surcoat undyed with the civ cross front and back; shield_ready with the heater by its grip. 11 %:
  too little civ; the fist showed on the shield face in 0-1; mirror-bright helm; stains on the linen.
- b2: grip_depth -0.05 (the fist still on the face), darker steel and mail, civ shoulders, a bigger cross: 14 %.
- b3: grip_depth +0.12 (the fist gone behind the board in every direction), civ hem 0.08, grime 0.2: 16.5 %.
- a1: boots (the feet were bare skin), grime 0.1, a 0.09 blade (0.075 left a 1 px tip speck in dir 6).
  Civ 7-23 % (mean 16.4; shipped 35); fitcheck pixels 1.02 (bbox 1.10, the sword out in front); specks clean.

### Critic round 1 (batch a1): 78.3 / 70.5 = 74.4
Both critics said the plate read as plate, not Third Crusade mail, and the great helm was top-heavy. Also:
grey blotches on the linen; no civ from behind (dirs 5-7); in dir 7 the sword lay over the shield's blank
back.

### Build round for a2
- Matte mail: new options mail_metallic 0.35 and mail_roughness 0.8, darker mail, no plate greaves,
  upper_bulk 1.1.
- headgear_scale 0.8; proportions 0.74 for a longer torso.
- The surcoat cut as a coat (coat_length 0.62) instead of 'skirt'. The blotches were the mail chausses
  showing through the skirt, not grime. Grime 0.
- The civ cross enlarged to 0.55, front and back; the civ hem moved to the coat.
- sword_dir (-0.55, -0.8, 0.1) and shield_yaw -25, so the sword is off the shield in dir 7.
- Speck chase at a2: at upper_bulk 1.1, the back cross's bar (0.55 down to 0.38) and the shoulder bands
  stood off the body's side in dir 4. Final: upper_bulk 1.2, the cross at 0.42, no shoulder bands,
  shield_yaw -20, unit_scale 1.03.
- a2 at 23526328: civ 7-23 % (mean 16.1); fitcheck pixels 0.99 (bbox 1.12); specks clean.

### Critic round 2 (batch a2): 85.3 / 82.5 = 83.9
No critical failures. Open: the mail read as a flat dark-grey sweatsuit; there were orange skin dots at the
shoulders in dirs 1-3.

### Build round for a3 (on the merged realism/all code)
- Lighter steel mail (#7e8086), metallic 0.5 and roughness 0.62, so the ring courses catch light while
  staying matte.
- A pale coif edge band at the neck.
- Mail mufflers: the skin is set to mail grey (the face is closed under the helm), so no skin shows at the
  shoulder seams.

### Critic round 3 (batch a3): 86.3 / 85.75 = 86.0, PASS

## FINAL: round a3 (853d96b7), 86.0, PASS
Scores: a1 74.4; a2 83.9; a3 86.0. Civ 7-23 % (mean 16.1); fitcheck pixels 0.99; specks clean.
Finals: reviews/realism/crusader/final(_red).png and the handoff review/crusader/ (shipped, final, Medieval
Infantry, Knight).
