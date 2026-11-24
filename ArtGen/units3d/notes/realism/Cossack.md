# Cossack (realism pass)

## Design in words

**The troop type.** A Cossack of the Napoleonic and later 19th-century wars
(about 1812-1880): the irregular light horseman of the steppe, a scout and
raider who fought with the pika (the Cossack lance), the shashka and a
musket or rifle. Civ3's Cossack wears the Caucasian kit that became the
popular image: a tan cherkeska with gazyri (cartridge tubes) across the
chest, a low fur papakha with a cloth top, crossed straps. Made as a tabletop
miniature: a Cossack on a compact Don horse.

**Must read at 1x:**
1. The pika: a long, dark lance held at the ready, angled forward out of the
   right hand, its point well clear of the head. It's the Cossack's weapon and
   a shape no stock neighbour has (the Hussar's raised sabre, the Cavalry's
   carbine). The rifle goes slung across the back, the shashka at the hip.
2. The papakha: dark fur with a civ cloth top crossed by gold braid, the face
   lit under it (never a dark mask or a beard over the cheek).
3. The long cherkeska: its skirts over the thighs, gazyri rows on the chest,
   a civ belt.
4. The Cossack seat: short stirrups, knees up, sitting a little forward
   over the withers.

**The horse.** A Don horse: compact and hardy, a deep barrel and chest, short
strong legs (heavier cannons than the shared slimmed legs), a long natural
mane (not trimmed: the steppe horse), a dapple grey as the shipped unit (it
separates from the Hussar's dun and the Cavalry's buckskin), its dapples soft,
not dirt. Ears smaller and the head a little lower, so they don't stand
up beside the rider's head from behind (the roll-out regression).

**Horse/rider scale (the family's recurring "rider too big on a pony"):**
measured in Blender, installed-era code (3265fe07) against today's: the
horse's trunk is unchanged (rump width 0.741 m, depth 0.878, back at 1.806),
the barrel at the elbows 9 % narrower (0.80 -> 0.73, the leaner forearms of
LEG_PROFILE), the cannons slimmed to 0.58. The stock Cossack's rider is scale
1.336 (catalog 1.06 x 1.26), sitting 0.22 m high then. The Cossack keeps the
catalog rider scale (the stock ratio), on a horse with heavier cannons and a
broader barrel (HORSE_SHAPE wide 1.08, legs 0.72).

**Civ colour, three regions:** the papakha's top, the belt and crossed
straps, a small saddle cloth. Measured (tint.py, alpha>127): shipped
Cossack 15.5 % (14.7 / 8.6 / 15.6 / 19.2 / 18.1 / 13.4 / 17.3 / 17.4);
the neighbours the Hussar 30.9 and the Cavalry 19.0. Target 12-19 %.

**Stock neighbours:** Hussar and Cavalry (C7\ModernArt\Art\Units).

## Rounds

Render ~26 s per round (run_locked "ran"); close-ups mnt_heads.py (~26 s).

### Round 1
The pika (cav_spear 3.0 m, dark shaft) at the ready, forward and up
(WEAPON_UP (0.18, 0.62, 1)); the rifle slung on the back (BACK_CARBINE); the
'cossack' seat (short stirrups, a little forward); stirrups, girth, reins to
the hand; a small cut civ saddle cloth; horse wide 1.08, leggy 1.0, legs 0.72,
neck -24, ears 0.8; no stubble beard (it read as a mask), lighter skin.
- The low papakha covered the face (the head bowed); the horse's ears beside
  the rider's head from behind; tint 9.0 % (the cloth smaller than shipped).

### Round 2: head up (0.36), papakha lifted 0.05, ears 0.6, neck -30, cloth deeper (0.8). Tint 10.7 %. Face still hidden.
### Round 3: the papakha narrower and taller (0.86 x 0.86 x 1.25), lift 0.09: eyes, nose and mouth lit. Cloth 1.0. Tint 11.4 %.
### Round 4: ears 0.35, neck -36; a civ hem band on the cherkeska (no effect: the seated rider has no skirt; dropped).
### Round 5 (critic1)
A civ breast strap (it shows in the front views, where the cloth hides
behind the chest). Tint per direction 13.7 6.4 14.4 14.6 12.7 7.9 13.4 15.5,
mean 12.3 % (shipped 15.5; Hussar 30.9, Cavalry 19.0).

## Critic round 1: continuity 64, fresh 62.5, average 63.25 (FAIL; identity borderline)
"A generic lancer/uhlan on a grey horse": 1 the papakha a small blue
pillbox; 2 the cherkeska short, flat khaki: dark, long, skirts over the
flanks, bright gazyri; 3 the rifle and shashka vanish. Praised: the lance,
the horse's anatomy. (The family pass changed the shared horse legs since:
LEG_PROFILE cannons 0.72; the Cossack's own legs/wide dropped.)

### Round 6
A tall black lambskin papakha with a small civ crown patch crossed in gold
(riders.papakha crown=True); a dark charcoal cherkeska (#2e2c2c) with bright
gazyri (#d8d2c4); a seated coat skirt (new: riders.coat_skirt, mounted.
COAT_SKIRT); a shashka in a lighter scabbard with silver mounts, guardless
(props.sheathed_sword shashka=True, key 'shashka'), hung diagonally
(SCABBARD_UP); the rifle longer and lighter (1.4 m, x1.3, #8a5a32). Tint 6.4 %:
the skirt covered the civ saddle cloth; the papakha's big tufts read as bubbles
and covered the eyes.
### Round 7: small low curls on the papakha, narrower (0.86) and lifted 0.11: the face lit. A civ bashlyk (hood) on the back (the cape). Tint 8.4 %.
### Round 8: the skirt out past the saddle cloth (0.53): a dark blanket over the whole saddle. Tint 5.6 %.
### Round 9: the skirt as side panels (55-150 degrees), shorter (0.68): still blanket-like.
### Round 10 (critic2)
A broad civ hem band on the skirt (it reads at 1x as a coloured edge along the
flank), a bigger bashlyk (0.42 x 0.44). Tint per direction 7.5 7.3 8.6 7.9
10.5 14.6 12.7 7.6, mean 9.6 % (shipped 15.5): low, the saddle cloth now mostly
under the skirt.
Still weak: the skirt reads as a dark saddle blanket rather than a coat; the
rider is a dark figure at 1x (the critics asked for the dark coat).

## Critic round 2: continuity 69, fresh 66, average 67.5 (+4.25)
The pika and the tall dark papakha read. Fixes: 1 the bashlyk a flat
hard-edged rectangle; 2 the gazyri don't read, the dark coat and skirt merge
with the saddle and mane into a black blob: charcoal-brown, a civ beshmet or
facings, civ toward 15-18 %; 3 the papakha a smooth cylinder (busby): shorter,
wider, an irregular tufted outline, lighter curls. The shashka's curve; no
rider scale-up (the family ratio).

### Round 11
Gazyri as geometry (new riders.gazyri, mounted.GAZYRI): two angled rows of
ivory tubes with brass caps; a civ beshmet in the coat's opening (the
'plastron' pattern in the civ colour); the coat and skirt charcoal-brown
(#4a3e34); the papakha 0.95 x 0.95 x 0.85 with 26 two-tone curls (some
#5e5046); the bashlyk smaller with more flare (folds); the shashka 1.0 m.
- The curls read as bubbles again; the gazyri sat at the collar. Tint 8.2 %.
### Round 12: smaller curls (0.017-0.027), the papakha 0.9 x 0.9 x 0.9 lifted 0.12 (face lit); the gazyri lower; the skirt's front edge back (a0 80) so the civ saddle cloth shows in front; the bashlyk 0.38 x 0.42. Tint 11.1 %.
### Round 13 (critic3)
The gazyri forward of the chest (depth 0.19, r 0.022): they show at the
opening's edges, partly behind the arms (the lance and rein hands cross the
chest). Tint per direction 11.0 9.8 12.3 9.2 10.2 13.6 12.1 9.0, mean 10.9 %.
Still weak: tint below the 15 % target; the gazyri half hidden by the arms;
the lighter curls read as dots at 2x.

## Critic round 3: continuity 72.5, fresh 66, average 69.25 (+1.75)
Fixes: 1 the back's civ block a rectangle: a real bashlyk (pointed hood, two
tails over the shoulders) or drop it; the rifle a full diagonal; 2 the gazyri
hidden by the hands: outside the beshmet, the hands off the chest; 3 the
papakha a straight cylinder at 1x: tufts breaking the top edge, the crown a
little wider. Also: a step lighter (coat, horse), a material break, civ toward
15 % on the near side (lining, cuffs).

### Round 14
A bashlyk (new riders.bashlyk, mounted.BASHLYK): a pointed civ hood on the
upper back with folds and a ragged point, its two tails over the shoulders
down the chest; the cape dropped. Gazyri outside the beshmet (x 0.115-0.15),
the lance and rein hands off the chest. The papakha's crown flaring (0.138
over a 0.12 band) with tufts on its top edge. The coat #5a4a3c, the horse a
step lighter (#5e5e62, dapples #94949a), black boots, civ cuffs. Tint 8.4 %.
### Round 15 (critic4)
Bigger gazyri caps (r 0.03, further forward), a bigger bashlyk (0.15 x 0.36,
tails 0.36), smaller top tufts, the coat skirt shorter (0.55) so the civ
saddle cloth shows below it. Tint per direction 11.7 9.0 11.9 10.3 9.4 8.6
8.6 9.9, mean 9.9 %.
Still weak: tint under 15 %; the papakha's tufts read as curls/bubbles at 2x.

## Critic round 4: continuity 82, fresh 65.5, average 73.75 (+4.5)
"It now reads as a Cossack" (continuity). Fixes: the pika out of the fit (it
shrank the unit 26 %: 44.5 -> 60 px at 1x, the shipped Cossack ~57); the skirt a
shapeless sack (a defined hem, a split, folds); the papakha 20 % shorter; the
gazyri contrast; the shashka, rifle and pika; the lance arm.

### Round 16
The pika out of the fit (mounted.POLE_WEAPONS); the skirt with civ edges up
its front and back (the split) as well as the hem, deeper folds; the papakha
0.72 tall; brighter gazyri caps (#f6f0e2) on dark pocket bands; a pale bone
shashka hilt, the scabbard hung more level; the rifle a dark stock and a pale
barrel; a thin pika shaft with a long head; the lance hand out to the right,
the elbow bent. Tint 14.5 %. Specks: the thin shaft broke in two directions.
### Round 17: the shaft 0.042. One 1-px speck left (dir 1).
### Round 18 (critic5)
A fuller pika head (flat 0.4). Tint per direction 15.4 13.4 16.9 15.0 13.8
12.0 12.7 14.7, mean 14.2 % (shipped 15.5). specks.py: dir 1 has one 1-px
piece: the pika's point at the cell's top-left corner (the tip's last pixel
falls under alpha 127), not a floating part.

## Critic round 5: continuity 83.75, fresh 72.75, average 78.25 (+4.5)
The skirt, papakha and gazyri landed; the unit at the Hussar's size. Fixes:
the lance head a broad partisan (shrink 40-50 %, a slim spike); the lance a
column through the horse's head in S and towering in N: tilt out, shorten
20-25 %; the rifle a sliver; the coat's side and rear flat.

### Round 19
A pika spike (props.spear spike=True: a slim tapered head, no leaf; tip: a
blunt last point), head 1.6; the shaft 2.4 m (from 3.0), tilted out
(WEAPON_UP (0.42, 0.68, 1)); the rifle 1.6x with a dark stock and a pale
barrel; deeper skirt folds (0.06). specks.py: the thin spike broke into a
dotted line (dir 1) and a 4-px piece (dir 8), at the lance's point.
### Round 20 (critic6)
The spike fuller (radii 0.018-0.028, flat 0.9, blunt tip 0.012): specks.py
clean (1 piece per direction). Tint per direction 15.6 14.2 17.1 14.3 13.2
12.8 12.8 14.9, mean 14.4 %.

## Critic round 6: continuity 85.5 (PASS), fresh 73, average 79.25 (+1.0: one round without a gain)
Fixes: the pale rifle barrel behind the shoulder read as a second blade; the
lance shaft thick, flat brown, long; the coat and papakha a dark lump.

### Round 21 (critic7)
The rifle a gunmetal barrel (#34363a) with a mid-brown stock showing, hung
lower (riders.sling_carbine drop 0.14); the pika 2.1 m (from 2.4) with a
thinner (0.028), lighter shaft (#a07a4a), the steel butt and socket; the coat
and skirt a step lighter (#6e5c4a). specks.py clean. Tint per direction 16.0
14.3 17.4 14.7 13.5 14.0 13.9 15.2, mean 14.9 %.

## Critic round 7: continuity 86.25 (PASS), fresh 76.5, average 81.4 (+2.1)
Fixes: the lance through the papakha in N/NW and hidden in E; the papakha
oversized; the gazyri specks at 1x.

### Round 22 (critic8)
The lance hand further out (-0.58) and the lance tilted forward (0.45, 0.85,
1): it clears the head in every direction; a mid-brown shaft (#7a5434); the
papakha 20 % smaller (0.8 x 0.8 x 0.62); bigger gazyri caps (r 0.036). specks
clean. Tint per direction 15.8 14.4 16.7 15.3 13.0 13.7 13.6 15.2, mean 14.7 %.

## Critic round 8: continuity 84.5, fresh 74.25, average 79.4 (below C7's 81.4)
The regression: in SE the lance a stub across the lap (its forward lean
pointed it at the camera).

### Round 23: C7's lance angle back with C8's hand: still short in SE (the head over the horse's head).
### Round 24 (critic9)
The lance steeper (0.45, 0.5, 1.25): its head stands above the rider in SE,
beside the papakha in N/NW (the hand out at -0.58 from C8). Everything else
as C8 (the papakha frozen). specks clean. Tint per direction 15.8 14.1 16.9
14.7 12.9 13.8 13.7 15.3, mean 14.6 %.

## Result: plateau, FINAL = critic 9 (6e0bd2ed)
Critic averages (continuity / fresh): C1 63.25 (64 / 62.5); C2 67.5 (69 / 66);
C3 69.25 (72.5 / 66); C4 73.75 (82 / 65.5); C5 78.25 (83.75 / 72.75);
C6 79.25 (85.5 / 73); C7 81.4 (86.25 / 76.5); C8 79.4 (84.5 / 74.25);
C9 81.375 (86.25 / 76.5) = FINAL (a tie with C7 at 0.025 below, and it holds the
SE lance fix). The committed code re-renders the critic9 sheet (mean abs diff
1e-6, no pixel over 2 levels).
Tint per direction 15.8 14.1 16.9 14.7 12.9 13.8 13.7 15.3, mean 14.6 %
(shipped 15.5). final.png / final_red.png: shipped, final, Hussar, Cavalry.
Open points (from the critics): the coat skirt vs the saddle cloth's fringe in
E/NE/W by value; the horse's head a black blob in E/NE; the papakha still
large to the fresh eye; the belt and the shashka hilt's contrast.
