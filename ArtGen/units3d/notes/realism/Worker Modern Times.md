# Worker Modern Times: realism design (2026-11-23, realism/misc)

## The real-world model
A 20th-century labourer on a road or building gang: a yellow hard hat, a work shirt, tan bib overalls with
braces, a tool belt, black boots, a long-handled shovel with a D-grip. Civ3's original: bareheaded, a civ
shirt, khaki overalls, the shovel upright beside him, blade up.

## The features that must read at 1x
1. The shovel, blade up and bright, held in both hands (before: in one hand, or low across the hips where
   it turned into a stick in half the directions).
2. The yellow hard hat (the modern labourer's mark, not civ colour).
3. The civ shirt under tan bib overalls with dark braces.

## Civ colour
Stock: the shipped unit 25-35 % (mean 29), Worker 9-26 (18), Settler 13-25 (18). Regions: the shirt
(sleeves and back); the bib breaks the front.

## Stock neighbours
Worker and Settler.

## Rounds
### Build rounds 1-3 (drafts b1-b3, then a1 at HEAD on the GPU)
- b1: a yellow hard hat; the shovel upright in both hands (stand_staff): the left arm crossed the chest
  awkwardly to reach the haft.
- b2: the bib (a tan 'cuirass') over the civ shirt; a bigger shovel (scale 1.12).
- b3 = a1: one hand on the shovel, the left fist on the hip. Civ 20-26 % (mean 23.5; shipped 29); fitcheck
  pixels 1.04 (bbox 1.26: the elbow out on the hip); specks clean.

### Critic round 1 (batch a1): 73.3 / 70.75 = 72.0
The shovel was edge-on and read as a spear, and in dirs 2-3 the blade covered the face. The whole shirt in
civ colour broke the accent rule, and the sleeves ballooned (puffy). Both critics wanted overall detail.

### Build round for a2
- A new props.work_shovel: a 0.3 m dished round-point blade with treads, and a D-grip. It is planted
  blade-down by his right foot, face-on, both hands on the shaft ('ready' with two_hands, grip2 -0.32).
- A neutral grey work shirt; no puffy sleeves; upper_bulk 1.05.
- Civ colour on the bib panel, a bandana and the cuffs. Civ 8-14 % (mean 10.9), under the 15 % floor:
  the arm across the chest hides the bib.
- A dark bib edge, knee patches and bib pockets.
- a2 at 4d1e1f57: civ 8-14 % (mean 10.9); fitcheck pixels 0.92; specks clean.

### Critic round 2 (batch a2): 81.8 / 82 = 81.9
Open: the shovel was edge-on in dir 3 and behind the leg in dir 6; one hand only in some directions; the
blade was a blown-out white slab; the civ bib was one block; the bandana didn't read at 1x.

### Build round for a3
- The shovel is planted in front of him, clear of the legs, with both hands on the shaft (the right on the
  grip, the left partway down) and the torso leaning a little onto it (pose_extra at the -0.06 limit).
- The blade is turned 45 degrees (edge_dir (0.7, -0.7, 0)) so its face shows in the side views.
- Grey used steel for the blade (new DEFAULTS key shovel_steel), with a dark earthy edge (earth_edge).
- The civ is on the bib's pockets, a bigger bandana (0.12) and the cuffs; the bib itself is tan.
- Civ, a3 final: a narrow civ bib (cuirass_width 0.12) with tan pockets, plus the bandana and cuffs:
  about 9 %, below the 15 % floor because the critics asked to shrink the bib.

### Critic round 3 (batch a3): 84.5 / 82.5 = 83.5 (no critical failure)

## FINAL: round a3 (853d96b7), 83.5
Scores: a1 72.0; a2 81.9; a3 83.5. Civ 5-14 % (mean 8.8: below the 15 % floor, after the critics asked to
shrink the civ bib); fitcheck pixels 0.93; specks clean. Finals: reviews/realism/worker_modern_times/ and the
handoff review/worker-modern-times/ (shipped, final, Worker, Settler).
Later pass: stand the shaft nearly upright, the blade planted beside the front foot, both hands stacked on
the D-grip at chest height with the elbows bent; in dirs 2, 5 and 6 the blade face must show clear of the
legs. Bring the civ share back toward 15 %.
