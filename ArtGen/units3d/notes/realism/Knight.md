# Knight (realism prototype)

## Design in words

A late 13th-century knight (about 1270-1300), the period of the great helm,
the heater shield and the cloth caparison over the destrier, made as a
tabletop miniature. The installed Knight is already the strongest mounted
unit; this pass polishes it rather than redesigning it.

**Horse.** A destrier: a big, short-coupled, well-muscled horse, but still a
riding horse: slender cannon bones and a head about 0.6 of the neck's length
(the shared horse fix). A pale grey under the caparison.

**Barding.** A civ-coloured cloth caparison in two panels (chest and rump),
split at the saddle, hanging to just below the belly with gold bands and
borders. The folds are real cloth folds: the cloth hangs nearly flat over the
back and shoulder where it lies on the horse, and falls into irregular tube
folds that grow toward the hem, so the hem line undulates with the folds
(not a regular sine ripple along the length). A steel chanfron with a
central ridge and a spike, a crinet of overlapping steel lames over the neck,
a peytral at the chest.

**Rider.** A great helm (flat-topped, eye slits and a gold cross), mail
under a surcoat in the civ colour, plate on the arms and legs following the
limbs (couters and poleyns at the joints, greaves), a heater shield on the
left arm with a white charge on the civ field, a lance couched under the
right arm: level, the point forward over the horse's head. The seat: the
"long" medieval seat, the legs nearly straight and braced forward in the
stirrups, the torso upright to slightly back against the lance's shock.

**Civ colour:** the surcoat, the caparison panels and the shield field (three
regions); gold trim and white charge are material colours.

**Readability at 1x:** the lance as a long diagonal/level line ahead of the
horse, the triangle of the shield, the boxy helm, the coloured caparison.

## Rounds

Render times are run_locked's "ran" for the full build (~40 s each; lock waits
noted); close-ups are an extra 9-17 s debug render of 2-4 directions.

### Round 1 (~40 s): the shared fixes only
Slimmer legs, smaller head, the helm now facing forward (riders.headgear
front_fix): the gold cross and eye slits are at the front at last.
- The helm's top stops below the crown: the rider's hair shows through the
  top between the horns. Riders get MPFB hair even under a closed helm.
- The rider sits as on a chair (the shared 'ride' stance), knees high.
- The caparison: an even sine ripple and a scalloped (sine) hem.
- The lance is a thin line at 1x. Civ colour 26.8 %.

### Round 2 (~40 s, close-up 16 s)
Helm closed over the crown (great_helm top/bottom kwargs, only for the
Knight), no hair, the knight's long seat (RIDE_POSES['knight']), the lance
2.3 m, 1.4x girth, couched across the neck to the left; caparison tube folds
(drape style='tubes') with a straight hem.
- The helm leans back about 15 degrees: the head bone leans back when the
  face is level, and the hat follows the bone.
- The shield lies flat like a tray: it followed the forearm to the reins.

### Round 3 (~40 s, close-up 16 s)
HAT_PITCH 14 degrees with front_fix: the helm is upright.
- Shield still a tray. Civ 28.6 %: the caparison panels are big.

### Round 4 (~40 s, close-up 16 s)
riders.place_shield re-hangs the heater upright on the left forearm, face to
the front-left: the white cross reads in the front views.
- Civ 28 %; the panels are plain blue boxes at 1x.

### Round 5 (~40 s, close-up 9 s)
A broad gold border band above the hem and the white cross of the arms on
each panel's flanks (cap_band, cap_charge).
- The crosses come out broken (an inverted T): the cloth's straight fall to
  the hem is a single face, so painted keys can't draw on it. Shield too high.

### Round 6 (~40 s + 20 s lock wait, close-up 9 s)
drape drop_n: the fall in 10 faces. The crosses and border read at 1x; the
shield sits lower. Civ colour 24.9 % (surcoat, caparison, shield field).

### Round 7 (~40 s, close-up 9 s)
Re-render with the shared neck change. Civ 24.6 % by my old measure.
- The rider's legs are inside the chest panel (hidden; a clipping a critic
  may call missing legs).

### Round 8 (~40 s, close-up 13 s): final
Thighs wider and the shins out (the 'knight' pose), the chest panel starts
further forward (cap_front 0.2): the legs show between the panels.
Civ colour (coordinator's measure, tint alpha>0 / base alpha>0):
22.5 14.6 18.9 22.0 23.9 22.0 27.1 25.2, mean 22.0 %, against Crusader 24.6
and the shipped Knight 24.4. Three regions: surcoat, caparison, shield field.

Still weak: the horns on the great helm (a German crest, but they may read
as "Viking"); the crosses on the caparison have stepped edges at 2x; the
rider's plate legs are mostly hidden; the S view has the least colour.

## Second pass: outside critic FAIL 61.5, CRITICAL "Viking horns"
Fixes asked: 1 no horns from any view, 2 the caparison as real draped
cloth, 3 a darker horse, 4 the lance couched up and forward so it shows
from front and back, 5 the leg in the stirrup down the flank, the rider higher.

### Round 9 (27 s + 15 s lock wait, close-up 20 s)
No helm crest; the chanfron's ear cups off (chanfron_ears False). The
caparison simulated (mounted._sim_cloth, tack option cap_sim): two panels
cut fuller toward the hem (cap_fullness), pinned along the spine, dropped
onto the horse with Blender cloth (50 frames, no crash). Dark bay coat.
Lance up = (-0.1, 1, 0.45). SEAT_PELVIS_BY_UNIT 0.15, legs down the flank.
- No horns; the cloth drapes over the rump; but the hem was ragged (a
  stepped hem jitter) and crumpled; the horse's own ears stood up beside the
  helm from behind. Civ 29.6 %.

### Round 10 (27 s, close-up 16 s)
Smooth uneven hem, stiffer bending (broad folds), less friction, ears x0.5
(EAR_SCALE_BY_UNIT), panels a little apart (cap_rear 0.22, cap_front 0.14).
- Clean draped folds. The gold band broke where the hem rose. Civ 27.5 %.

### Round 11 (27 s)
The band follows each panel's hem; a broader band. Civ 26.2 %.
- At 1x: the dark horse separates the rider; the lance reads in every view.

### Round 12 (27 s): final (blue and red)
Larger crosses (cap_charge_size 1.35). Civ colour per direction
26.1 20.4 23.8 25.5 25.5 27.1 27.9 28.3 (mean 25.6 %; Crusader 24.6).
The DEFAULT cell grew by a pixel or two (compose grows cells to hold the
raised lance, as it did for the shipped sheets).
Still weak: the crosses' edges are stepped at 2x; the surcoat and plate on
the rider are small at 1x; the chest panel's hem sits a little stiffly.

## Third pass: critics 72 (FAIL, no critical)
Fixes asked: 1 the rider too big for the horse (a pony), 2 stiff panels and
smeared crosses, 3 the head hidden in S, a black block from behind, the
lance a stub in S, 4 tint to ~24 %.

### Round 13 (31 s, close-up 21 s)
A bigger destrier for this unit (HORSE_SHAPE_BY_UNIT: size 1.14, neck 1.2,
head 0.9, legs 0.75 of the slimming); reins to the hand; no peytral (the
white "fur" band); the shield further out to the left; ears 0.75; the lance
angled out to the side. Caparison finer and fuller (fullness 0.38).
- Horse proportion right, head clears the chest. The cloth shredded into
  strips (too much cloth for the sim), crosses still smeared. Civ 28.1 %.

### Round 14 (31 s, close-up 21 s)
Crosses as decals: a gridded flat cross shrink-wrapped onto the simulated
cloth (mounted._charge_decals, cap_decal); sim quality 12, a coarser mesh.
- Decals partly scattered (nearest-point wrap on folds). Civ 28.5 %.

### Round 15 (32 s + 15 s wait, close-up 21 s)
Decals projected along x; hem higher (cap_hem); stiffer cloth.
- Rays that missed left cross bars sticking out like fins. Boards again.

### Round 16 (33 s, close-up 23 s)
Unprojected decal pieces dropped; cloth kept 0.07 off the body.
- Crosses crisp; but the hem ends flared out as fins, civ 28.6 %.

### Round 17 (32 s, close-up 21 s)
Back to the cloth of round 11 (fullness 0.15, bending 2, 0.045 off); the
mail barding without flare under the cloth (it poked through as strips).
- Clean hem waves, crisp crosses, plate lames between. Civ 25.5 %.

### Round 18 (32 s + 10 s wait): final (blue and red)
Hem a little higher (cap_hem 0.27): more of the destrier's legs.
Civ colour per direction 24.9 21.4 22.6 23.6 24.7 25.2 25.8 24.5 (mean 24.1 %).
Still weak: the panels fold only gently (deeper folds tore or finned in the
sim); the front panel's rear edge is busy beside the rider's leg.

## Fourth pass: critics 72.5 (FAIL, no critical)
Fixes asked: 1 one long trapper (no chrome flanks), 2 the leg a chrome
slab, 3 the head lost in the front view, 4 one lance hold, thicker, clear of
the head, 5 a bigger horse still, 6 civ colour to the cloth and ~24 %.

### Round 19 (23 s + 15 s wait, close-up 13 s)
One long trapper from the croup over the chest, toward the hocks: a
sculpted static drape with deep tube folds (cap_one; the sim tore on longer
cloth); the plate barding under it pulled up out of sight. Mail chausses
with matte poleyns and greaves (riders.leg_harness, 'plate' steel),
stirrups. Neck lower and arched (-16, flex 22). The lance raised at the
rest (one hold in every view, clear of the head), girth 1.8. Destrier
size 1.24. The shield's back faced in leather (shield_heater back_key).
- A big box curtain hiding the rider's legs; the chest front bare; the
  lance a club. Civ 30.4 %.

### Round 20 (23 s, close-up 13 s)
Thighs and shins out over the cloth, the trapper trimmed at the tail and
hem higher, lance girth 1.45, crosses larger. Legs, poleyns, greaves and
stirrups show. Civ 24.4 %; the chest still bare.

### Round 21 (23 s + 5 s wait, close-up 9 s)
A breast cloth projected onto the chest, closing the trapper. Its top edge
came out ragged at the neck. Civ 28.1 %.

### Round 22 (23 s + 10 s wait, close-up 9 s)
Breast cloth lower and narrower. Civ 27.1 %.

### Round 23 (23 s + 10 s wait, close-up 6 s): final (blue and red)
A broad gold border (cap_band 0.02-0.26) and bigger crosses (2.0).
Civ colour per direction 27.6 24.3 25.1 24.1 20.1 17.8 24.9 27.0 (mean 23.9 %).
Still weak: the trapper reads boxy (vertical ends); the breast cloth's top
edge is uneven close up; the helm and shield's mirror steel stay as they were.

## Result: plateau, final = pass 3 (733ca88e)
Critic averages (continuity / fresh): pass 1 61.5; pass 2 72; pass 3 72.5;
pass 4 71.5 (74 / 69). Plateau: two passes without beating the best by 1.5.
The final is pass 3. Its per-unit values are restored (pass 4's code paths
stay in, off for the Knight: cap_one, LEG_HARNESS, STIRRUPS, back_key); a
re-render matches pass 3's DEFAULT sheet exactly (mean abs diff 0.0), and
final.png / final_red.png are pass 3's.
Civ colour per direction 24.9 21.4 22.6 23.6 24.7 25.2 25.8 24.5 (mean 24.1 %).

Open points for a later pass:
- A properly draped trapper: pass 3's covers only the hindquarters and leaves
  chrome flank panels; pass 4's long trapper was a stiff box with a big gold
  fringe.
- Fewer, smaller crosses; a thin trim.
- A clearer lance.
- The rider's leg (pass 4's mail chausses + plate harness fixed it:
  riders.leg_harness).
- The horse's head in the front view.
- A horse that reads as a destrier.

## Family pass re-check (critic 5): 68.5. The art lead: no pass-3 sheets (the
current code can't reproduce them); the final is the best round on the current code.

## Pass 5 (critic 6): the structure the critics keep naming
Poles out of the fit (mounted.POLE_WEAPONS / mark_poles; render_vehicles skips
'no_fit' meshes): the lance shrank the Knight 5 %.
### Round 24
One long trapper to near the fetlocks (cap_one, cap_hem -0.6, deep folds),
a cloth neck sleeve, a civ surcoat over mail (no plate cuirass: the silver
cylinder) with a belt and a sword at the hip, civ surcoat skirts (coat_skirt
tint), a smaller heater (0.46 x 0.66), the lance upright at rest (3.2 m,
'knight_rest' seat, WEAPON_UP (0.06, 0.12, 1)), a lighter bay (#6a4630).
- Tint 75.6 %: everything civ. Dropped the neck sleeve (no neck cover: the
  plate crinet was 15th century) and the surcoat skirts.
### Round 25: hem -0.32. Tint 51.5 % (a civ trapper over the whole horse).
### Round 26: a pale trapper (cap_field #e6e0d2) with a civ border band and civ crosses: 6.6 %, and the crosses don't land (decals above the cloth).
### Round 27: paly civ and pale panels (cap_paly 0.42): a candy stripe (noise). 24.9 %.
### Round 28 (critic6)
Broad paly panels (1.2 m): two or three civ and pale panels a side, heraldic,
readable at 1x. Tint per direction 36.9 20.8 39.2 41.2 38.7 32.5 42.2 40.5,
mean 36.5 % (stock Knight 24.4): high.
Still weak: the trapper's ends are vertical (boxy); the crosses on the trapper
don't land; the tint above the stock's.

## Critic 6: continuity 74.75, fresh 63, average 68.9 (critical: the chest)
The horse's coat broke through the white breast cloth as a sawtooth; the
shield vanished in 4-7; the trapper a rigid box; tint 36.5; the crosses
missing. Upright lance near the Conquistador's: slope it 45 degrees.

### Round 29 (critic7 fixes)
Lance sloped forward 45 degrees (0.06, 0.8, 0.8), the point ahead of the head;
the shield out to the side (face (-0.97, 0.25), out 0.24) so its edge shows
from behind; the civ share of each paly period 0.36 (tint 24.8 %); the
trapper's hem flared (0.13) and 0.12 higher (more lower leg); one white cross
a side on the civ rump panel (the decal's z offset had put them above the
cloth). The breast cloth still holed (brown patches): offset 0.11 didn't help.
### Round 30: missed rays filled from their neighbours: no holes, but a flat stepped board.
### Round 31 (critic7)
No breast cloth (breast_cloth False): the trapper ends at the chest, the
horse's breast shows clean. Tint per direction 22.8 16.0 22.1 28.1 27.0 28.0
30.4 27.5, mean 25.2 % (stock 24.4). specks.py: 1-px pieces in dirs 5, 7, 8
(the lance's point).

## Result: FINAL = critic 7 (9a849eea), on the later-pass list
Critic averages (continuity / fresh) on the current code: C5 68.5 (pass 3's
values on the family proportions); C6 68.9 (74.75 / 63; critical: the chest);
C7 77.9 (80.25 / 75.5) = FINAL ("a charging lancer, not the Conquistador").
(Earlier code: P1 61.5, P2 72, P3 72.5, P4 71.5; the art lead: those sheets
can't be reproduced by the current code.)
Tint per direction 22.8 16.0 22.1 28.1 27.0 28.0 30.4 27.5, mean 25.2 %.
final.png / final_red.png: shipped, final, Crusader, Conquistador.
Open points for the later pass: the lance a stub head-on in S; the bare
breast and neck since the breast cloth went (a peytral, harness straps with
fittings, muscle shading); the pale trapper panels plain and evenly lit (fold
shadows, a darker lining at the flared hem, a narrow civ border); the hem
low, the legs stubs (raise it to mid-cannon: knees and hocks); the head weak in
S, SE, NE; the shield gone in E to NW; crosses on the pale flank panels too;
1-px specks at the lance point (dirs 5, 7, 8; see the Cavalry's notes on tips).

Specks (the coordinator): the 1-px pieces at the lance point are an edge-on weapon tip, known; not a detached part.

## Fit fix (2026-11-23, re-render only)
The lance out of the fit: 1.37x the stock's bbox area, 1.24x its opaque pixels.
render_vehicles.FIT_SCALE 'Knight' 0.92: bbox-area 1.16 (the lance), pixels
1.05; 1x heights 53-62 -> 51-79 (the lance stands higher than the stock's). No
geometry, material or civ change. specks.py: one 1-px piece in E (the lance
point, known). final.png rebuilt; the gate's reference with it.
