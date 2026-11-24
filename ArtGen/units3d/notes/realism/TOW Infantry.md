# TOW Infantry: realism design (PC2, realism/infantry)

## The real-world model
A TOW anti-tank team (US, 1980s): the launch tube on its traversing unit with the sight and night sight, on a low tripod; a missile crate; the gunner seated behind it at the sight, the assistant gunner kneeling at his left, spotting. Woodland BDUs, PASGT helmets in camo covers, olive flak vests, LBE.

## The features that must read at 1x
1. the long launch tube on its tripod; 2. two men at it (one seated at the sight, one kneeling); 3. the PASGT helmets.

## The pose
The gunner leaning in to the sight, both hands on the grips; the assistant kneeling, a hand shading his eyes.

## Civ colour
Civ on wide armbands and knee pads on both men. The shipped unit (a lone standing spotter with no launcher) is 26 % (a civ vest). Target: 15-30 % in every direction (foot stock 9-34 %), in 2-3 painted regions.

## Stock neighbours
Infantry and Artillery

## Rounds
### Build rounds 1-5
- R0: the siege builder's team (`siege.tow_team`): the spotter stood 1.4 m away and the team was spread out, so render_vehicles' box fit drew the men small (ppm 38 against the foot units' 46).
- R1-3: one crate instead of two, the spotter close in; he would not kneel (the KNEEL legs did not take), so in R4 he sits on the missile crate at the gunner's left, a hand shading his eyes. ppm 47, near the foot units' scale. Olive flak vests, civ armbands and knee pads (the shipped one has a civ vest).
- R5: a civ band on each helmet, so the colour reads from above and from behind.
- Size: the shipped TOW Infantry is a lone standing spotter with no launcher, so the team is 2.5x its opaque area. The render is fitted to the Civ3 original's box (a team at a launcher, calibration/TOW Infantry.json). This is for the lead to decide; see the hand-back.

### Critic batch 1: 68 / 86 = 77.0

### Round 2 (fixes)
- Size, the lead's ruling: compared with the stock crewed weapons. Before: 1.40x the Artillery, 1.50x the Cannon. Now 1.07-1.10x the Artillery and about 1.15x the Cannon, via new `render_vehicles.PPM_SCALE` 0.87 (the box fit, then drawn smaller); the men are at about 0.87 of foot scale (ppm about 40 against the Infantry's 46). The launcher: a shorter (1.5 m), darker OD tube with black end caps, clamp bands and a muzzle ring, raised 7 degrees; a big boxy day sight/tracker with a lens hood and eyepiece; a night sight. The team turned 22.5 degrees and the assistant gunner's crate moved out to his left, so the two men are separate in every view. With 8 views 45 degrees apart, 22.5 degrees is the most any straight tube can be kept off end-on in all of them; dirs 1 and 5 see it at 22.5 degrees, raised.

### Critic batch 2: 71.5 / 84 = 77.75 (B: the tube a stub)

### Round 3 (the last; fixes)
- A longer (1.9 m), thicker tube in a lighter OD than the uniforms; the team turned 90 degrees, so dirs 0, 1, 2, 4, 5 and 6 see the tube 45-90 degrees off the camera axis and only the side views 3 and 7 see it end-on (the rig turns the whole model; this yaw gives the largest smallest angle for dirs 0-2 and 6); lighter woodland on the men. Size: PPM_SCALE 0.9.

### Critic batch 3: 78 / 82 = 80.0 (B: CRITICAL, wrong direction: the 90-degree team turn aimed the launcher
sideways to the unit's facing in every view)

### Round 4 (targeted, on the critical only; batch_infantry_c4)
- The 90-degree turn undone: the muzzle follows the facing again. The longer, lighter tube kept. The tube traversed
  22 degrees within the facing and raised 12 degrees (the sights ride with it; the gunner and his crate moved behind
  the traversed sight), so the S and N views, where a tube along the facing is end-on, still see its length.
- Size: 1.10x the stock Artillery (per direction 0.99 1.39 1.17 1.00 0.90 1.19 1.12 1.07), 1.18x the Cannon; the
  men at about 0.86 of foot scale (ppm 39.6 against the Infantry's 46.3), PPM_SCALE 0.89. Civ 5-12 %, mean 9 %.
  Specks clean.
- Best so far: R3 80.0 (with the critical). After R4's score it closes on its best round, or STUCK.

### Critic R4 (targeted): 79.5 / 86 = 82.75; the wrong-direction critical closed (both critics)

## FINAL: R4 82.75

Rounds: R1 77.0, R2 77.75, R3 80.0 (critical: wrong direction), **R4 82.75**. Pictures: reviews/realism/tow_infantry/final(_red).png
(shipped, final, stock Artillery, stock Cannon); the finals gate re-renders it at HEAD (build_pc.py).

Later pass:
- dir 1 (S): the tube still foreshortens to a block between the gunner's knees; traverse it about 30-35 degrees in that view;
- dirs 4-5: the raised tube stands near-vertical and reads as a mortar; lower its elevation there;
- the rig turns only the whole team, so both need a per-direction option (a launcher yaw/elevation per view).
