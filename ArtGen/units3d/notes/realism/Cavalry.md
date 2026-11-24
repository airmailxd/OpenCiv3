# Cavalry (realism prototype)

## Design in words

A line dragoon / heavy cavalryman of about 1870-1900, the Civ3 Cavalry's role
(the gunpowder-era horse soldier who fights mounted with sabre and carbine),
built as a tabletop miniature: a bay troop horse, a trooper sitting tall.

**Horse.** A troop horse, not a draft horse: about 15.3-16 hands, a deep but
not barrel-wide body, a clean neck set high, a refined head. The anatomy fix
the art lead asked for: slender cannon bones (the shin between knee/hock and
fetlock is the thinnest part of the leg, about a third of the forearm's
width), forearm and gaskin muscled, the head about 0.6 of the neck's length.
Coat: a dark bay (red-brown body, black mane, tail and lower legs), the
commonest troop-horse colour and distinct from the Hussar's dun and the
Cossack's dapple grey beside it.

**Tack.** A military saddle (a leather seat with a raised pommel and cantle),
a shabraque (saddle cloth) in the civ colour with a pale edge, cut short (it
ends above the elbow line, as a regulation shabraque does), a black bridle
with a pale noseband and a double rein, a breast strap, a rolled cloak behind
the cantle, a carbine bucket. The reins run from the bit to the rider's left
hand, which holds them just above the pommel.

**Rider.** An upright cavalry seat: the torso vertical over the seat bones,
the shoulders back; a long stirrup with the thigh sloping about 45 degrees
down and forward around the barrel, the lower leg hanging straight under the
knee, the heel pushed down (toe up) in the stirrup. The left (bridle) hand
low and forward over the pommel, holding the reins; the right hand holds the
carbine "advanced": the butt resting on the right thigh, the muzzle up and a
little forward, so the gun reads as a clean vertical line at 1x, unlike the
Hussar's raised sabre and the Cossack's rifle low across the saddle. The
right shoulder sits back and the left forward (the shoulders counter-turned
against the hips), the head turned a little to the front-left. The sabre in a
steel scabbard hangs at the left hip.

Uniform: a civ-coloured tunic, a white crossbelt, gold shoulder boards, grey
riding breeches with black boots, a brass dragoon helmet with a civ-coloured
horsehair crest.

**Civ colour, three deliberate regions:** the tunic, the helmet crest and the
shabraque. Everything else in material colours (brass, steel, leather, bay
coat). Target 10-25 % of the unit's pixels at 1x.

**Readability at 1x:** the silhouette must say "upright horseman with a gun
pointing up": tall rider (the rider was small at RIDER_SCALE 1.26 x 0.91; it
goes up to about the Hussar's), the vertical carbine, the crest.

## Rounds

Render times are run_locked's "ran" (lock wait 0 s unless noted); close-ups are
an extra ~15-20 s debug render of 2-4 directions at about 4x the sheet scale.

### Round 1 (37 s)
Shared horse fix in: legs slimmed below the knee/hock (LEG_PROFILE), head
x0.84 about the poll. Rider scale 1.26 (rider_scale 1.0 over the catalog's
0.91), the 'cavalry' ride pose, a dark bay coat.
- Cannons now slender; the fetlocks and hooves still clubby. Head reads smaller.
- The shabraque hangs as a long flat curtain.
- Close-up: the left (rein) arm reaches across, the carbine hand far forward
  over the withers, not at the thigh.
Next: smaller hooves, hands closer, shorter cloth.

### Round 2 (render ~40 s)
- The torso still pitched forward: my spine aims were more forward than the
  shared RIDER_DEFAULTS'. The helmet's neck guard shows in front views.
Next: sit taller (spine back), neck up.

### Round 3 (render ~40 s, close-up 21 s)
- Found it: riders.headgear puts every rider's hat on back to front (the hat's
  +Y maps to the head bone's -Z; the face is +Z). The crest falls over the
  face, the peak is behind. Pre-existing, in all riders/crews/chariots.
- Found: the rider root's scale lifts the pelvis (k - 1) x its height above
  the seat; at k = 1.26 the rider floats ~0.27 m above the seat point.
Next: front_fix for the hat (per unit), seat the pelvis at the seat.

### Round 4 (render ~40 s, close-up 18 s)
- Face, peak and crest now right. The rider sits down in the saddle. The
  carbine is a clean vertical line at 1x in every view; the rider is the
  Hussar's size. Civ colour 16.8 % (tunic, crest, shabraque).
- Still: the shabraque is a flat rectangle curtain with square corners and a
  thread-thin edge; the coat texture is blotchy close up.
Next: shape the shabraque (rounded rear corner, a broad pale lace border).

### Round 5 (~40 s, close-up 10 s)
Shabraque cut: rounded rear/front hem corners (cloth_round 0.14), a broad
pale lace border painted round its edges (cloth_border), a breast strap.
- Reads as a regulation shabraque, the border a clean pale outline at 1x
  (checked in red too).

### Round 6 (~40 s, close-up 10 s)
Shared: the horse's neck 10 % longer (NECK_STRETCH), head and ears unscaled
by it. Head to neck now about 0.6.
- The reins still end at the pommel, not in the rider's hand.

### Round 7 (~40 s, close-up 14 s)
reins_to_hand: the reins run from the bit rings to the left fist.
- The pale curve beside the left thigh in close-ups is the shabraque's front
  lace edge passing under the thigh, which is correct.

### Rule change (coordinator): civ colour to match the stock of the class
Measured as tint alpha>0 / base alpha>0 per direction on the DEFAULT sheet:
Hussar 23.9, Cossack 11.8, Horseman 10.2, shipped Cavalry 12.6 (mean %).
Round 7 measured 8.0: too little.

### Round 8 (~40 s): shabraque deeper (cloth_frac 0.42): 9.1 %. Still low.

### Round 9 (~40 s, close-up 10 s): final
cloth_frac 0.8, cloth_back 0.5, border 0.03: per direction
12.7 7.4 12.7 12.6 12.4 11.0 13.2 12.6 (mean 11.8 %), in the Cossack and
shipped-Cavalry range. Three regions: tunic, crest, shabraque.

Still weak: the bay coat's asset texture is blotchy close up; the front view
(S) has the least civ colour (7.4 %) as the horse's chest hides the cloth;
the shoulder boards are gold lumps; the sabre's scabbard tip hangs low by the
hind leg in the W/NW views.

## Second pass: outside critic FAIL 71 (posing 6, readability 7)
Fixes asked: 1 a passive rider, 2 the sabre invisible, 3 the neck near
vertical in NE/NW (reads as rearing), 4 the shabraque too long with the
brightest edge, 5 the face a dark smudge, the front flat blue.

### Round 10 (27 s, close-up 18 s)
Sabre drawn and raised forward (remaster weapon 'sabre', 0.95 m), the right
arm extended; the scabbard stays at the hip. Neck lowered and arched for
this unit (NECK_RAISE_BY_UNIT -27, HEAD_FLEX_BY_UNIT 14). Shabraque cloth_frac
0.8 -> 0.6 (25 % shorter), lace edge darkened to #7c7464. Helmet pitch 4
instead of 14 (HAT_PITCH_BY_UNIT), so the peak no longer shades the face; a
buff plastron (garments pattern 'plastron') down the chest.
- Close-up: chin lit, plastron reads. At 1x the sabre is lost against the
  mane in E. Civ 9.3 %.

### Round 11 (27 s, close-up 10 s)
Sabre higher and more upright, cloth_frac 0.64.
- The sabre reads in 7 of 8 views; the S view is short. Civ 9.4 %.

### Round 12 (27 s, close-up 11 s)
A fuller civ crest (dragoon_helm crest=1.5), the sabre angled out to the
right: it now clears the body in S.

### Round 13 (27 s): final (blue and red)
Sword hand a little higher. Civ colour per direction
10.1 6.6 10.0 9.7 9.8 9.7 10.7 10.4 (mean 9.6 %; Horseman 10.2, Cossack 11.8,
shipped Cavalry 12.6): at the low end of the class, held down by the
shorter shabraque the critic asked for.
Still weak: the SE view, where the sabre sits in front of the torso; the S
view's low civ colour; the Hussar also raises a sabre (the two separate by
the bay horse, the brass helmet and the shabraque).

## Third pass: critics 76.75 (FAIL, no critical)
Fixes asked: 1 the carbine gone, 2 the front: helmet and head one gold blob,
dark face, the sword arm across the face in SE; 3 the NW neck high; 4 the
saddle cloth a flat rectangle, the sabre a 1 px hairline.

### Round 14 (28 s, close-up 14 s; a first try died at import, 1 s: a name clash)
Carbine slung across the back (riders.sling_carbine, mounted.BACK_CARBINE):
dark stock and barrel, from the right hip to the left shoulder. Helmet a
little smaller, a black horsehair mane down the back (dragoon_helm mane=True),
pitch -2. Neck NECK_RAISE -17, HEAD_FLEX 22 (poll about chest height).
Shabraque with a pointed tail (cloth_point), a black sheepskin over the seat,
a big blue-grey rolled cloak (bedroll_color, bedroll_r), an empty steel
scabbard (sheathed_sword empty=True, key='steel'), a broader, brighter blade
(width 0.065, steel #dfe3e8).
- The MPFB hair now poked through the smaller helmet.

### Round 15 (26 s, close-up 10 s + 20 s wait)
No hair under the helmet; helmet scale 1.0/1.05/1.05; sword hand further out.
- Face lit, mane reads; the blade still runs past the face in SE.

### Round 16 (26 s, close-up 10 s)
The sabre angled far out to the right.
- Clears the face, but lost in E and SE at 1x (the hand too low).

### Round 17 (26 s + 15 s wait, close-up 6 s): final (blue and red)
Hand back up, blade between the two. Reads in 7 of 8 views (not SE, where it
is behind the head); the carbine reads as a dark diagonal in N, NE, NW.
Civ colour per direction 10.7 7.7 10.6 10.3 10.3 9.7 11.1 10.8 (mean 10.1 %).

## Fourth pass: critics 77.6 (FAIL, no critical)
Fixes asked: 1 the head and helmet one gold blob, 2 the torso a flat civ
block, 3 the tail, sheepskin, cloak and scabbard invisible at 1x (add a
breast strap, stirrup, girth), 4 the sabre straight up and long in W/NW.

### Round 18 (30 s, close-up 11 s)
Helmet smaller (0.9/0.95/0.95) and raised off the eyes (headgear_lift
0.025), a black peak (leather_dark #0c0a08), a longer, fuller black
horsehair tail. White crossbelts crossing (chest_strap cross), gold collar
(neck band) and cuffs, smaller shoulder boards. Pointed tail 0.36 with a
darker, wider lace band, a pale grey-white cloak roll, a white breast strap,
the girth (tack 'girth'), stirrups and leathers (riders.stirrups). The
scabbard re-hung as a diagonal (SCABBARD_UP). Sabre 0.9 m with a brass guard
(sabre guard kwarg), angled forward.
- Face lit, belts and collar break up the blue. The scabbard stuck out
  sideways in the front view.

### Round 19 (30 s, close-up 11 s)
Scabbard turned in along the flank. Civ 10.5 %.

### Round 20 (30 s, close-up 7 s)
Checked the kit at 2x: the cloak roll, breast strap, girth and stirrups read;
the sabre is angled forward in W/NW.

### Round 21 (30 s + 5 s wait, close-up 11 s): final (blue and red)
A longer, wider steel scabbard run further down and back.
Civ colour per direction 11.4 7.7 11.1 11.0 10.6 9.2 11.3 11.8 (mean 10.5 %).
Still weak: the scabbard still mostly hides behind the left thigh; the
pointed tail is a small step at 1x; the back carbine reads as a vertical
stick in the E view.

## Critic averages so far
Pass 1 71; pass 2 76.75; pass 3 77.6; pass 4 81.75 (continuity 82 / fresh ~80).

## Fifth pass
Fixes asked: 1 the white breast strap the loudest thing (make it dark
leather with brass), mute the cloak roll; 2 the face dark brown in
S/SW/SE; 3 the sabre a hairline (2 px, bright edge, brass guard, ~15 %
shorter raised), a thicker carbine stock; 4 the horse lean and leggy beside
the stock horses (thicker barrel and chest, shorter cannons, heavier
quarters, a lighter bay).

### Round 22 (30 s, close-up 11 s)
Breast strap dark leather with brass studs (tack 'strap_studs'), cloak roll
a muted grey (#7e7c76). Skin warmer and lighter (#e0b08c), the helmet
raised further (lift 0.04). Sabre 0.78 m (from 0.9), blade 0.1 wide (about
2 px at 1x), steel #f4f6f8, a thicker brass guard. Carbine stock thicker.
Horse for this unit: HORSE_SHAPE_BY_UNIT 'wide' 1.1 (barrel, chest and
quarters broader), 'leggy' 1.0 (not 1.07: shorter legs), 'legs' 0.72
(heavier cannons); coat a lighter bay #83502c.
- At 1x the sabre reads bright in every view; the horse matches the stock
  horses' mass; the face shows lit skin in S. The carbine read as a post.

### Round 23 (30 s): final (blue and red)
Helmet pitched back a little more (-6), the carbine 1.0 m at 1.4x.
Civ colour per direction 11.4 8.4 10.9 11.0 10.6 9.6 11.4 11.9 (mean
10.6 %), in the stock band (Cossack 11.8, shipped Cavalry 12.6, Horseman
10.2); the cloth kept at its size, not pushed to 20 %.

## Pass 5 critics: continuity 81, fresh 79, average 80 (best 81.75 at pass 4:
one round without improvement). Tint now by tint.py (alpha > 127): pass 5
19.9 % against Horseman 16.8, Cossack 15.5, Hussar 30.9, shipped Cavalry 19.0.

## Sixth pass
Fixes asked: 1 the face a beard/mask in the front views; 2 the sabre
inconsistent (cleaver, stub, lost in the mane, needle); 3 the head thrown up
on a short neck in the side views, the dark bay and black mane one blob in
front; 4 the rider a little small.

### Round 24 (30 s, close-ups 11 s + 11 s)
High-zoom face close-up: the "mask" was the leather peak, a flat black slab
sitting skewed across the eyes, and the flat brass neck guard showing as a
plate beside the cheek. Now a curved brass peak and a rounded brass neck
guard (dragoon_helm peak='brass'); the horsehair tail narrower, down the
back only. Sabre slim (0.06 wide, 0.85 m, slight curve, brass guard), raised
up and out (0.42, 0.22, 0.88) so a line of steel clears the silhouette in
every view. Horse neck lower and longer (raise -10, flex 26, neck 1.22), a
smooth trimmed mane roll replacing the asset's spiky hair cards (tack
'mane_trim'), a pale blaze down the face (tack 'blaze'), a mid
chestnut-bay coat #a0643a. Rider scale 1.06.
- The face was still under the brim (only mouth and chin). Tint 19.8 %.

### Round 25 (30 s + 10 s wait, close-up 11 s): critic6 final (blue and red)
Helmet raised further (headgear_lift 0.075): nose, mouth and chin lit
under a clean brass peak.
Tint (tint.py) per direction 21.7 16.4 20.7 21.3 19.1 15.9 19.7 23.3,
mean 19.8 %.

## Result: plateau, final = pass 4 (48d4856f)
Critic averages (continuity / fresh):
P1 71; P2 76.75; P3 77.6; P4 81.75 (82 / ~80) = FINAL; P5 80 (81 / 79);
P6 80 (85 / 75). Two critic rounds in a row without beating the best by 1.5:
plateau. The final is pass 4. Its per-unit values are restored (the pass 5-6
code paths stay in, off for the Cavalry: HORSE_SHAPE 'wide'/'leggy',
strap_studs, mane_trim, blaze, dragoon_helm peak='brass', sabre guard_r); a
re-render matches pass 4's DEFAULT sheet exactly (mean abs diff 0.0; the
Knight still matches pass 3 exactly). final.png / final_red.png (shipped,
final, Hussar, Cossack) are pass 4's.
Tint (tint.py) per direction 21.3 14.1 20.2 21.5 19.8 14.5 19.4 22.7, mean 19.2 %.

The alternative: pass 6 (10589213), sheets in review/cavalry/critic6/ (and
reviews/realism/cavalry/critic6/ in the branch). Its continuity critic passed
it at 85 (face, sabre clearing the silhouette, lighter horse, bigger rider
all landed); the fresh critic gave 75. The art lead may prefer it.

Open points from the last critics:
- The horse reads small and short-barrelled beside an oversized rider (fresh:
  +10-15 % horse against the rider).
- The S-facing head a blob: forehead, ears, muzzle, a blaze.
- The shabraque a flat slab in E/W: lace, a pointed corner, a valise with depth.
- The sabre a hairline in S, and inconsistent angles across directions.
- The carbine a vertical stick in E/NE.
- The breastplate (plastron) a pale patch under the chin in S/SE.
- Flat horse modelling: muscle shading, dark points.

## Family pass re-check (critic 7): continuity 81, fresh 76.75, average 78.9
(pass 4's values on the family proportions: 2.85 below pass 4's 81.75). The
proportions read better; pass 4's face-as-beard and sabre problems back.

## Pass 8 (the art lead: pass 6's fixes on the family proportions)
Pass 6's head, sabre, horse and kit (10589213) on the family LEG_PROFILE and
the catalog rider scale: the brass peak with the helmet raised (lift 0.075,
pitch -6), the narrow horsehair tail behind; the slim sabre (0.85 x 0.06,
curve 0.06, brass guard, steel #f4f6f8) raised up and out (0.42, 0.22,
0.88), the sword hand (-0.44, -0.12, 0.62); the lower arched neck (-10,
flex 26), the broader barrel (wide 1.1, leggy 1.0, neck 1.22; the legs from
the family profile), the trimmed mane and blaze, a mid chestnut-bay (#a0643a)
with dark points; the thick carbine stock; skin #e0b08c. Plus the re-check:
no pale plastron (it read as a grey beard): the civ coat front with two thin
white crossbelts (0.035); the breast strap buff leather (#a8875a) with brass
studs; a muted cloak roll.
### Round 26 (28 s): as above. Tint 20.9 %. From behind the horse's ears stood beside the crest.
### Round 27 (28 s): critic8
Ears 0.7. Tint per direction 23.7 21.8 24.0 21.6 19.5 14.8 19.7 22.5, mean
21.0 % (pass 4 19.2; stock Cavalry 19.0). Pass 4's values are in the comments
beside each changed setting (and commit 43c8df6e).

## Critic 8: continuity 85 (PASS), fresh 72.25, average 78.6. The art lead:
no pass-4 sheets (the current code can't reproduce them); critic 8 is the base.

### Round 28 (critic9 fixes)
The neck lower and less steep (raise -22, flex 18, neck 1.12) so the head sits
ahead of the chest in the profiles; a finer head (0.9); the sabre a real length
(0.76) and steel-grey (#b8bcc2); smoother coat shading (coat_contrast 0.7);
the shabraque with tube folds (0.05) and a buff lace border (#a8987a); the
crossbelts 0.045. Tint 21.5 %. specks.py: the slimmer grey blade broke into a
dotted line edge-on (S, N).
### Round 29-30: a thicker blade back; the blade turned 45 degrees (WEAPON_FWD): still specks edge-on in SW/NE.
### Round 31 (critic9)
The blade back 0.024 (it reads as one line edge-on). Tint per direction 23.8
21.7 23.9 22.9 19.7 15.2 19.8 23.7, mean 21.3 %. specks.py: one 1-px piece
in dir 5 (the blade's tip), the rest clean.

## Result: FINAL = critic 9 (2486316e)
Critic averages (continuity / fresh): P1 71; P2 76.75; P3 77.6; P4 81.75;
P5 80; P6 80 (85 / 75) — all on the earlier code. On the family proportions
(LEG_PROFILE 0.72, the catalog rider scale): C7 78.9 (81 / 76.75, pass 4's
values); C8 78.6 (85 / 72.25, pass 6's fixes); C9 81.4 (87.75 / 75) = FINAL by
the art lead's rule (pass 4's sheets can't be reproduced by the current code;
80.4 or more on the current code is final).
Tint per direction 23.8 21.7 23.9 22.9 19.7 15.2 19.8 23.7, mean 21.3 %.
final.png / final_red.png: shipped, final, Hussar, Cossack.
Open points for a later pass: the horse's head in the front view a small brown
knot (a blaze or nose highlight); the sabre shortest-looking in E; the 1-px
sabre tip in NE (specks.py); the shabraque's rear corners in N may read as a
stiff board past the hindquarters (check it doesn't clip); a squarer, livelier
stance (one foreleg lifted); civ colour at the top of the range (~30 % by the
critics' eye; 21.3 measured).

## Tip specks (after the final)
specks.py on the final: one 1-px piece in NE (dir 5). Located (mnt_which.py:
the canvas pixel's nearest vertices): the sabre blade's last 5 cm (local z
0.78-0.83), its tip seen nearly edge-on. Re-rendered at the final's own fit
(render_vehicles KEEP_FIT=1, so nothing else moves): with a blunter tip (props.sabre
tip 0.01 / 0.02) the changed pixels stay at the blade (343-476 of 252,320,
>2 levels), but the 1-px piece stays (and a second appears in SW at 0.02), so
the final is kept as it was (the tip option stays in the code, off).

Specks (the coordinator): the 1-px pieces are an edge-on weapon tip, known; not a detached part.
