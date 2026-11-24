# Hussar (realism pass)

## Design in words

**The troop type.** A Napoleonic light-cavalry hussar of about 1805-1815: the
fast horseman of the gunpowder era who scouts, raids and charges with the
sabre. The Civ3 unit wears the classic kit: a braided dolman, the fur-trimmed
pelisse slung over the left shoulder, a tall corded shako with a plume, tight
breeches and hessian boots, a curved light-cavalry sabre. Made as a tabletop
miniature: a hussar on a light, quick troop horse.

**Must read at 1x (3-4 features):**
1. The sabre held high: the right arm up and the curved blade standing above
   the shako, a near-vertical bright line, so it is the same shape in all 8
   directions (no cleaver in one view and a needle in another), about 2 px
   wide with a bright edge. It also sets it apart from the Cavalry's sabre
   held forward and out and the Cossack's rifle low across the saddle.
2. The pelisse: the fur-trimmed jacket slung from the left shoulder, hanging
   down the back and left side with its empty sleeve, a dark fur edge round
   it. It's the hussar's signature and a big shape (the dolman's frogging is
   too fine to read at 1x on its own).
3. The shako: tall but not a stovepipe, a short peak that doesn't shade the
   face (the face reads as lit skin under it), cords swagged across its upper
   front and down to the chest (since the roll-out they hung across the eyes
   like a gold visor), a gold plume on top.
4. The shabraque: the hussar's long pointed rear corner, a broad gold lace
   edge, a dark sheepskin over the seat.

**The horse.** A light-cavalry horse, about 15 hands: lighter than a dragoon's
but not a greyhound beside the stock horses: a deep chest and barrel, a
collected neck carriage (not thrown up), a trimmed mane with shading, a
readable head from the front (a blaze). Coat: a chestnut (red-gold, mane and
tail the same colour, a shade darker) with a blaze. That separates it from
its neighbours (the Cavalry's buckskin dun, the Cossack's dapple grey, the
Horseman's bay) and from the shipped Hussar's dun, and it doesn't sink into
the shade or merge with the gold braid.

**Pose.** A charging hussar: seated upright and a little forward over the
pommel, the left hand low at the reins, the right arm raised high with the
sabre up ("sabre haut"), the head to the front, the shoulders counter-turned.

**Civ colour, three regions:** the dolman and pelisse (the jacket), the shako
and the shabraque. The braid, fur, sheepskin, breeches and boots are material
colours. Measured (tint.py, alpha>127): the shipped Hussar is 30.9 %
(29.4 / 17.6 / 29.4 / 35.1 / 35.7 / 32.5 / 34.3 / 33.1); the neighbours are
the Cossack at 15.5 and the shipped Cavalry at 19.0. Target: 24-31 %, even
across directions.

**Stock neighbours:** Cossack and Cavalry (C7\ModernArt\Art\Units).

**The roll-out regression:** the shako cords hang across the eyes (front_fix
put them in front): they go to the shako's upper front and down to the chest.

## Rounds

Render times are run_locked's "ran" (~25 s; lock waits noted); close-ups are
mnt_heads.py (head, seat and full-unit tiles, ~24 s). Rounds 1-2 in the review
folder predate this pass; this pass starts at round 3.

### Round 3 (25 s + 20 s wait, close-up 24 s)
New: RIDE_POSES['hussar'] (upright, the sword arm raised high), the sabre
standing up above the shako (WEAPON_UP (0.08, 0.2, 1)), the shako with
cords='high' (swags across its upper front, raquettes at the sides, a cord
behind the ear), a smaller lacquered peak, shako z-scale 1.4 (was 1.7), no
hair under it; a chestnut with a blaze, a trimmed mane, reins to the hand;
the shabraque cut with a pointed tail, a gold lace border, a black
sheepskin; buff breeches, an empty steel scabbard, stirrups, rider scale 1.0;
a built pelisse panel (riders.pelisse). Tint 25.9 %.
- The sabre reads in all 8 views but is a needle in S and a broad blade in SE.
- The shako's band and peak still covered the eyes.
- The pelisse stood off the back like a board, with a fur "headphone" collar.

### Round 4 (25 s + 35 s wait)
Shako lifted 0.07 and the peak nearly flat: nose, mouth and chin lit under it.
Sabre 0.085 wide with a thicker spine (props.sabre spine 0.014), turned 45
degrees (WEAPON_FWD_BY_UNIT), so its flat shows in six directions.
The pelisse panel hugging the back: still a board.

### Round 5 (25 s)
The pelisse slung diagonally from the left shoulder: still a stiff box.

### Round 6 (25 s)
The pelisse remade as the entry's cape (garments.cape: real folds) with a
dark fur hem band, swung round the neck onto the left shoulder
(riders.sling_cape, yaw -30, roll 14). It reads as cloth off the left
shoulder at 1x. Tint 30.4 %. The red civ shabraque sat close to the chestnut.

### Round 7 (25 s)
A golden chestnut (#b47a40), the cape shorter (0.42): too close to the stock
Cavalry's buckskin dun.

### Round 8 (25 s, close-up 24 s): critic1
A mid chestnut #aa6834 (mane #6e3a1a); the cape's raised corner lowered (yaw
-26, roll 6); the horse's ears 0.8 and the neck lower (-18), since they showed
beside the rider's head from behind.
Tint per direction 29.4 22.4 28.3 31.0 30.2 30.9 30.0 30.8, mean 29.1 %
(shipped 30.9; Cossack 15.5, Cavalry 19.0).
The Cavalry and Knight re-render the same, apart from a few pixels (6-9 px, at
most 2 levels): a repeat render of the same code differs by as much (GPU noise).

## Critic round 1: continuity 74, fresh 66.5, average 70.25 (FAIL, critical)
Critical (both): civ colour "covers the face": a blue block with a gold band
in S/SW/SE, the back of the head one civ slab with the shako in N/NE/NW. Also:
the headgear reads as a gold-domed crested helmet, not a shako; the horse a
little pony-like and flat; the pelisse's fur doesn't read; the vertical sabre
stiff and taller than the roster.

Diagnosis (the composed tint layer, enlarged): the tint mask doesn't reach the
face; no bleed. The civ shako's body sat at the brow with the gold cords at its
lower edge, the high civ collar (RIDER_DEFAULTS neck 'high') came up under the
chin, and the visible skin was 2-3 px between them with the dark peak across
it: at 1x, one civ block with a gold band. From behind, the civ shako, the high
civ collar and the civ pelisse joined into one slab.

### Round 9 (25 s)
Shako black felt (#1c1a1c), not tinted; straight sides (shako straight=True),
0.95 x 0.95 x 1.6; civ plume; collar low ('neck': 'low', no gold neck band);
the pelisse's fur hem pale (#d4cec2, 0.09). Horse 10 % bigger against the
rider (size 1.1), neck 1.2, legs 1.05, coat shading x1.6 (coat_contrast).
Sabre angled ~25 degrees forward (WEAPON_UP (0.08, 0.5, 1)), the hand a little
forward. Tint 23.8 %. The plume was a small tuft; the brim still at the eyes.

### Round 10 (25 s)
A tall plume (plume_k 2.2), lift 0.09. Tint 24.3 %.
- At 1x the black shako and the shaded face merged into one dark head.

### Round 11 (25 s)
A gold band round the shako's foot (band='gold'), lift 0.13, height 1.5,
lighter skin. The whole face showed, but the shako floated over a bald scalp.

### Round 12 (25 s): critic2
Lift 0.1 and the short hair back (it fills under the shako; no poke-through),
the gold band stands proud of the felt. Close-up: eyes, nose and mouth lit
under the gold band; from behind, hair and nape under the shako, the white
collar line, no civ above the shoulders.
Tint per direction 24.0 20.7 25.1 26.0 24.6 23.9 24.0 26.3, mean 24.3 %.
Cavalry and Knight re-rendered: Knight identical; Cavalry 6 px at most 2 levels
(render noise, as before).

## Critic round 2: continuity 79.5, fresh 75.5, average 77.5 (FAIL, no critical)
The critical failure gone. Agreed fixes: 1 the black shako down to the nose
(a dark block at 1x): raise it, cut its height ~15 %, tip it back, lighten the
face; 2 the coat a flat orange with a same-colour mane and dirty mottling:
bay points, clean shading, thicker cannons; 3 the sabre still straight up in
N/NW/W: forward and down 30-40 degrees. Contradiction (kept, restructured):
the pelisse's pale fur (reads / reads as a waist belt): hang it further off
the left shoulder. Also: shabraque folds, the pointed corner at 1x.

### Round 13 (25 s)
Shako 0.95 x 0.95 x 1.28 (from 1.5), lift 0.115, tipped back 7 degrees
(new entry key headgear_tilt), skin #ecc4a2. A bay: coat #a2602e, black mane,
tail and points; coat_contrast 1.15 (1.6 read as dirt); cannons heavier (legs
0.65). Sabre up (0.1, 0.78, 1.0), about 38 degrees forward. Pelisse swung
further onto the left shoulder (yaw -42, roll 10), its fur hem narrower
(0.06). Shabraque folds 0.04, the pointed corner 0.6. Tint 24.8 %.
- The capped gold top band still made a gold disc on top of the shako.

### Round 14 (25 s, close-up 23 s): critic3
The top band an open gold ring (the crown stays black felt). Close-up: the
whole face lit under the gold foot band; a black shako with gold rings and
cords, a tall civ plume.
Tint per direction (tint.py): Hussar: 23.9 / 20.9 / 24.8 / 26.5 / 24.5 / 25.1 / 25.1 / 27.5  mean 24.8%

## Critic round 3: continuity 82, fresh 74.75, average 78.4 (+0.9: one round without a gain)
Fixes: 1 the sabre still the tallest thing in N/NW (its forward lean points
away from the camera); 2 the head still a dark block with a gold bar at 1x,
the side cord across the eyes in SE; 3 the back one civ slab, the pelisse not
a separate fur-edged jacket; 4 the shabraque a printed sheet, a stiff gold box
under the tail. Contradicted (kept): the horse (landed / wants more mass).

### Round 14b (25 s): a no-op (a script assertion stopped the edits; the render was round 14's).

### Round 15 (25 s, close-up 25 s): critic4
Sabre forward AND out to the right (up (0.5, 0.75, 0.75)), the hand lower
(0.82): in N it now leans out to the side, no higher than the plume. Shako
height 1.12 (from 1.28), the cord down behind the ear dropped (cord_down
False). The pelisse a darker shade of the civ colour (tint_shade 0.62) with a
dark brown fur edge, shifted out onto the left shoulder; a gold barrel sash at
the waist breaks the back. The shabraque with irregular tube folds
(cloth_style 'tubes', depth 0.07), a narrower lace border (0.035), shorter at
the back (0.48). The barrel a little broader (wide 1.1).
Tint per direction 22.3 18.1 22.5 24.3 22.5 24.6 23.7 25.7, mean 23.0 %.

## Critic round 4: continuity 82.5, fresh 82.5, average 82.5 (+4.1)
Fixes: 1 the sabre still a vertical spike in NW, a short hairline pointing at
the viewer in SW/S; 2 the cords and peak across the upper face in SW/SE, the
shako ~15 % shorter; 3 the back of the head a skin patch (reads as a face
turned back): dark hair; 4 the shabraque still flat sheets: deeper folds.
Pelisse (contradicted): keep, thicker fur, folds, an empty sleeve. Frogging
(optional): thicker, round the sides.

### Round 16 (25 s)
Shako 0.97 tall (from 1.12), the cord swags higher (swag_z 0.165), a smaller
peak; dark hair #1a120c (tried the ponytail01 queue: its side locks framed the
face in black, dropped in round 17); frogging thicker and wider (garments
frogging rows/half-width/duty: 5, 0.19, 0.42); the pelisse's fur hem 0.13 and
an empty sleeve with a fur cuff (riders.sling_cape sleeve); shabraque folds
0.12; a brighter blade (#f6f8fa) and a bigger brass guard. Sabre (0.72, 0.55,
0.5): still a vertical spike in NW (a blade pointing away from the camera
rises on screen: its screen height is 0.5 x its level reach + 0.87 x its rise).

### Round 17 (25 s): sabre (0.72, 0.62, 0.25), short dark hair: NW still a spike.

### Round 18 (25 s, close-up 25 s): critic5
The sabre levelled at the charge, the arm reaching forward at chest height
(hand (-0.38, -0.34, 0.68), blade (0.6, 0.78, 0.1)): its full length shows in
SW, S, SE, E, W against the grass; in NW (pointing away from the camera) it is
now a short stub below the plume tip instead of a spike.
Tint per direction 21.8 18.4 20.8 22.8 22.6 25.3 23.8 25.7, mean 22.6 %.

## Critic round 5: continuity 86 (PASS), fresh 80, average 83 (+0.5: second round without a gain of 1.5)
Fixes: 1 the NW sabre still near-vertical behind the shako (out 15-40 degrees),
the SE blade a white smear on the chest; 2 the horse lean beside the Cavalry's
(barrel, chest, cannons; shoulder and quarter highlights); 3 the rear
shabraque a flat sheet with a square gold frame under the rump.
Face (contradicted): keep, lighten a step.

### Round 19 (25 s)
Sabre further out (0.85, 0.5, 0.14), the hand further out to the right
(-0.46) and the elbow out: NW now a diagonal clear of the head and plume, SE
beside the body. Horse wide 1.18, leggy 1.0, cannons heavier (legs 0.55),
coat_contrast 1.3. Skin #f2cdb0. Shabraque tail point 0.75, back 0.4:
the tail hung below the belly as a stepped gold box. Tint 20.6 %.

### Round 20 (25 s + 15 s wait, close-up 25 s): critic6
Shabraque: point 0.3, rounder corners (0.2), deeper sides (cloth_frac 0.7),
back 0.45: no box from behind at 1x, a few gold steps at 2x.
Tint per direction 22.9 19.0 21.4 22.5 21.5 23.6 22.7 25.6, mean 22.4 %.

## Critic round 6: continuity 86 (PASS), fresh 72, average 79
The heavier horse, the rear shabraque and the lighter face landed; the NW and
SE sabre fixes didn't; fresh: the rider too big and high on a narrow horse,
the sword arm hidden behind the horse's head in N, a white blob on the chest in SE.

## Result: plateau, final = critic 5 (e75bde6b)
Critic averages (continuity / fresh):
C1 70.25 (74 / 66.5; critical: civ colour over the face); C2 77.5 (79.5 / 75.5);
C3 78.4 (82 / 74.75); C4 82.5 (82.5 / 82.5); C5 83 (86 / 80) = FINAL;
C6 79 (86 / 72). Two rounds in a row without beating the best by 1.5: plateau.
Round 18's values (critic 5) are restored (pass 6's tried values noted beside
HORSE_SHAPE_BY_UNIT); the re-render's comparison sheet matches critic5/final.png
exactly (mean abs diff 0.0). The Cavalry and Knight re-render within render
noise (1-2 px, at most 2 levels).
Tint per direction 21.8 18.4 20.8 22.8 22.6 25.3 23.8 25.7, mean 22.6 %
(shipped 30.9). final.png / final_red.png: shipped, final, Cossack, Cavalry.

Open points from the last critics:
- The NW sabre: a near-vertical spike behind the shako (a blade pointing away
  from the camera rises on screen): lay it forward along the neck line as in W.
- The SE sabre: a white smear on the chest: swing it out to the right so the
  steel shows against the grass.
- The horse against the rider (recurring in the family): the rider reads big
  and high on a narrow, pony-like horse beside the stock mounts.
- The vandyke tail from behind (round 19's longer point hung as a gold box).

## Family pass re-check: critic 7 = FINAL
The family pass (heavier legs LEG_PROFILE 0.72, the catalog rider scale) on
critic 5's values: continuity 87.5 (PASS; "one family with the Cavalry and
Cossack"), fresh 76, average 81.75: 1.25 below critic 5's 83, within 1.5, so
the new version is the final (the art lead's rule: consistent proportions).
Tint per direction 21.2 18.5 20.2 22.6 21.9 23.9 23.2 24.8, mean 22.0 %.
final.png / final_red.png are critic 7's (shipped, final, Cossack, Cavalry).
