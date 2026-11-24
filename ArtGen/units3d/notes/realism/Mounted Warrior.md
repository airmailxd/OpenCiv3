# Mounted Warrior (realism pass)

## Design in words

**The troop type.** Civ3's Mounted Warrior (the Iroquois unique unit, drawn
in the popular image of the Plains horse warrior): a North American
horseman of about 1750-1870 who rode with a pad or bareback, a rope war
bridle looped on the jaw, and fought with the bow from the saddle. Made as a
tabletop miniature, with respect: a warrior on a painted pony, the bow
drawn.

**Must read at 1x:**
1. The bow drawn: the left arm straight out to the front-left holding the
   bow upright, the right hand drawn back to the cheek. A strong action pose
   no stock neighbour has (the Horseman's spear, the Rider's sword); the bow
   is a clear curved line beside the horse's head in every view.
2. The war bonnet: the halo of feathers swept back with civ-coloured tips
   and a trailer down the back, raised so the face is lit.
3. The paint horse: a dun-and-cream pinto with civ handprints on the
   shoulder and haunch (the shipped unit's war paint), a hogged forelock, no
   saddle: a small civ pad and a rope bridle.
4. Buckskin: fringed leggings and a breechcloth, bare chest and arms, a
   quiver on the back.

**The horse.** A small, quick pony (no larger than the Horseman's), a dun
pinto with dark points; the family's legs.

**Civ colour, three regions:** the bonnet's feather tips, the handprints and
the pad. Measured (tint.py, alpha>127): the shipped Mounted Warrior is
26.2 % (28.8 / 18.8 / 29.1 / 31.1 / 26.5 / 20.1 / 25.0 / 30.5); the
neighbours the Horseman 16.8 % and the Rider 35.2 %. Target 18-26 %.

**Stock neighbours:** Horseman and Rider.

## Rounds

Render ~23 s a round; close-ups mnt_heads.py.

### Round 1
The 'bow_draw' seat (no saddle; the left arm out with the bow, the right
hand drawn back to the cheek, the torso and head turned toward the target);
the bow re-held upright in the left hand (mounted.BOW_DRAW), drawn (0.3); the
bonnet 1.15 (from 1.6) and lifted; a small civ pad (no saddle), a rope bridle,
reins to the hand. Tint 15.1 %. specks.py: 5-16 pieces a direction: the
bonnet's trailer feathers (they swung off the body with the turned head) and
the halo's tips.
### Round 2: no trailer; 23 wider feathers. 3-6 pieces: the tips.
### Round 3: a civ thong through the feathers near the tips (feather_bonnet tip_cord). 2-3 pieces: the bow.
### Round 4: heavier bow limbs (props.bow thick 1.6), 27 feathers 0.075 wide: one direction left (E).
### Round 5-7 (critic1)
Bow thick 2.1 and a string that stays one line (0.0055 x thick; a thinner one
broke into dots). Tint per direction 20.0 20.3 19.0 17.0 16.2 12.4 16.1 19.2,
mean 17.5 % (shipped 26.2; Horseman 16.8, Rider 35.2).
specks.py: E (dir 4) has 2 small pieces (4 px, 2 px) where the bow's lower
limb passes behind the fist and the mane (an occlusion split, not a detached
part); the rest clean.

## Critic round 1: critic A 62 (CRITICAL), critic B 66.75, average 64.4
Critical: the bonnet a solid tilted disc / sombrero. Fixes: separate eagle
feathers, white with dark tips, ~1.2x head height, civ on the browband only;
the rider ~10 % bigger with more mass, skin and buckskin separate; the bow and
the draw (longer, thicker, side-on, the hand at the cheek, an arrow nocked);
a small pad saddle, no gold trim.

### Round 8
The bonnet 0.85 with 24 feathers (0.066 wide, gaps), dark tips (feather_bonnet
dark_tips), no tip thong; the rider 1.1x (RIDER_SCALE_BY_UNIT), more muscle;
tan leggings (#c8a878) apart from the skin; the bow 1.5 m, thick 2.4, turned
more side-on, the draw hand at the cheek and a dark arrow nocked (BOW_DRAW
arrow); a small pad with a dark edge and a leather saddle. Tint 8.6 %.
### Round 9: beaded civ strips down the leggings (side_stripe), a bigger pad. Tint 11.6 %.
### Round 10: civ fluffs at the feather tips: they read as a solid rim at 1x again; dropped.
### Round 11 (critic2)
Tint per direction 14.2 10.3 14.3 13.0 12.1 10.4 11.6 13.8, mean 12.5 %
(shipped 26.2): low, with the civ now on the browband, the pad, the leggings'
strips, the handprints, the breast strap and tassels.
specks.py: E (dir 4) still 2 small pieces (3 px, 1 px) at the bow.

## Critic round 2: A 76.0, B 69.0, average 72.5 (+8.1; the critical gone)
Fixes: the dark tips the heaviest mass (a crest); the bow longbow-length
(~1 m, off the face in S); a tapered quiver, darker legging seams, a fringed pad.

### Round 12
The dark only on the feathers' outer part, the fan spread 1.15; the bow
1.05 m with more recurve; the bow hand further out. specks: 6 directions.
### Round 13-17: the specks hunted: an overlapping dark tip card, then the dark tip on the same card as the white (props.feather_card tip_key, tip_frac: one piece), no spread, wider feathers (0.075), a thicker arrow, no string: the pieces were the bow's limbs (the shorter, more recurved bow and the new hand position).
### Round 18-19: heavier limbs (thick 3.2), the bow 1.1-1.25 m: still pieces.
### Round 20 (critic3)
Round 8's bow hand back, the bow 1.1 m, thick 3.2, the default recurve; the
feathers white with the outer ~38 % dark on one card. Tint per direction 14.2
10.4 14.0 12.7 11.9 10.4 10.7 13.7, mean 12.3 %.
specks.py: 1-4 px pieces in SW, E, NE: the bow limbs' edges (dir 1: a dark
1-px point at the arrow head). Not done: the hide quiver, the legging seams,
the fringed pad.

## Critic round 3: fixes asked (C4)
The bonnet a flat halo from the side: a shallow cone swept back and up, a
short one-piece trailer; the bowstring thin and sinew-brown (keep the dark
arrow), the upper limb clear of the bonnet in NE; a fringed, folded pad with a
dark outline; legging seams; chest shading; a quiver with fletching.

### Round 21
feather_bonnet cone (the side feathers swept back and up, out of the halo's
plane) and trailer 'short' (five overlapping pairs to the shoulder blades on a
civ band); bow string_r / string_key (a 'sinew' string); the bow's 'up'
tilted out and forward (-0.06, 0.22, 1.0); the pad's outline dark (#3a2a1e)
with a buckskin fringe (tack 'fringe') and deeper folds (0.04); a bigger
quiver (9 arrows, 1.15); muscle 1.0. The cone 0.4 narrower: a crest, not a
bonnet. The 0.007 string broke into dots.
### Round 22: the cone wider (x 0.8, back 0.22); the string 0.011.
### Round 23 (critic4)
The string 0.014 (one line at 2x, ~1 px at 1x), the feathers 0.085 wide.
Tint per direction 12.8 10.1 12.1 11.3 10.6 9.6 9.6 12.2, mean 11.0 %.
specks.py: 1-4 px pieces in S, E, NE, N, W: dark feather tips (S, W) and the
bow limb and arrow head edges (E, NE, N), as at C3.

## Critic round 4: continuity 79.5, fresh 69.75, average 74.6 (+2.0)
The bonnet shape, string and blanket landed. Fixes: the feathers near-white
and clean (they rendered a mottled grey), crisp dark tips; a pale arrow shaft;
paint patches on the shoulder, neck and chest, less coat noise; a darker
legging; the bow ~10 % shorter.

### Round 24 (critic5)
feather_bonnet clean (a plain near-white 'feather_white' and a plain dark tip,
no cloth mottle); the arrow #d2bc8c; three more pale coat patches (shoulder,
neck, chest; coat_spots pale), coat_contrast 0.8; the leggings #8a6440, boots
#5a4028; the bow 1.0 m. Tint per direction 12.8 10.5 12.2 11.3 10.7 9.4 9.8
12.0, mean 11.1 %. specks.py: 1-4 px pieces in S, E, NE, N, W (feather tips,
bow limb edges), as at C4.

## Critic round 5: continuity 83.0, fresh 67.0, average 75.0 (+0.4, no gain: 1 of 2)
The bonnet the blocker: 24 thin feathers a white cloud front-on, flat in 3-5.
Fixes: 12-14 broad feathers with gaps, standing up in 3-5; thinner round
limbs with a clear recurve, a fletched arrow; the bridle with contrast.

### Round 25
The bonnet 13 feathers (width 0.085), cone 0.45 (standing up); the bow thick
2.5, recurve 0.11, round limbs (props.bow round_); the arrow fletched
(BOW_DRAW fletch); the bridle #1e1610. specks: the E/NE/N pieces were the
reins rising to the bow hand (dark now).
### Round 26 (critic6)
The reins left on the neck (no 'hand', as the Keshik). specks.py: three 1-px
pieces (S, E, W: feather tips). Tint per direction 13.2 11.2 12.7 11.6 11.0
9.7 10.1 12.3, mean 11.5 %. Finals gate passes.

## Critic round 6: continuity 85.25, fresh 74.0, average 79.6 (+4.6)
## FINAL = critic 6 (9646d95b, 79.6)
Closed under the three-critic-round limit. HEAD reproduces the critic-6 sheet
(max diff 0.012); in verify_all.sh. specks: three 1-px feather tips (S, E, W).
Open points (critic 6):
- The draw aims steeply skyward in 4-6 (the bow near vertical above the head
  in 5): a level draw, forward and to the side; in 1 the bow across the
  horse's neck, clear of the face.
- The bow a flat brown wedge: slimmer, tapered to the tips, a pale belly edge.
- The bonnet a white puff at 1x in 2 and 5: darker quill lines, small civ or
  red ribbons at the base; the crown ~10 degrees higher in 4.
- Paw prints on the flank: handprints or hail dots instead; the muzzle and
  eye in 8 sharper.
