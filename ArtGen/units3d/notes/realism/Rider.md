# Rider (realism pass)

## Design in words

**The troop type.** Civ3's Rider is the Chinese unique horseman. Made here
as a Ming-era Chinese cavalryman (about 1400-1600): a horse soldier in a
long padded coat with gold-edged hems and a conical, fur-brimmed hat, fighting
with the dao (a single-edged curved sabre) from a saddle with stirrups, on a
pale dapple horse with a civ saddle cloth, tasselled breast strap and a red
crest plume. Made as a tabletop miniature.

**Must read at 1x:**
1. The conical hat with its pale fur brim and a red tassel on top: the
   Rider's mark since the shipped unit, raised so the face reads lit.
2. The long civ coat: its skirts falling over the thighs (riders.coat_skirt in
   the civ colour), gold-edged, belted in gold.
3. The dao raised up and out (the Cavalry's critic-approved carry), a
   steel-grey blade that stays one line edge-on (thick back), blunt tip.
4. The pale dapple horse with the civ saddle cloth, a white emblem, gold
   tassels and a red plume: the brightest horse of the medieval riders.

**Civ colour, three regions:** the coat (body and skirts), the saddle cloth
and the hat's crown. Measured (tint.py, alpha>127): the shipped Rider is
35.2 % (36.8 / 17.9 / 37.7 / 43.0 / 38.5 / 34.5 / 35.3 / 37.9); the
neighbours the Keshik 6.4 % and the Knight 30.8 %. Target 25-33 %.

**Stock neighbours:** Keshik and Knight.

## Rounds

Render ~23 s a round; close-ups mnt_heads.py.

### Round 1
The 'rider' seat (the Cavalry's, stirrups), the dao raised up and out
(0.42, 0.22, 0.88; turned 45 degrees; a thick back), the hat with a red
tassel (riders.hat_cone tassel, height 0.85), civ coat skirts with a gold hem
(coat_skirt tint), gold belt and cuffs, reins to the hand, a trimmed mane.
Tint 39.0 %; the brim over the eyes; the horse's red plume in front of the
face in S.
### Round 2: the hat lifted 0.11 (the face lit), no horse plume, the saddle cloth shorter behind (0.55). Tint 29.7 %.
### Round 3: no civ specks on the haunch (a noise pattern). specks: 1-px pieces at the blade's tip (SW, NE).
### Round 4 (critic1)
A blunter, thicker blade tip (sabre tip 0.022, spine 0.03): specks.py clean.
Tint per direction 30.2 26.8 30.9 29.4 29.0 29.7 27.7 29.1, mean 29.1 %
(shipped 35.2; Keshik 6.4, Knight 30.8).

## Critic round 1: A 65.25, B 68.0, average 66.6
"One flat civ blob from collar to boot", the cloth "a sticker". Fixes: break
the coat (dark belt, collar, cuffs, hem trim, split flaring skirts, dark boots,
stirrups; ~22-25 %); a wider, curved dao clear of the horse's head; a smaller
gold-edged motif instead of the white diamond and crescent, a dark saddle; a
bigger crimson tassel, the hat's crown not civ; more torso.

### Round 5 (critic2)
A dark leather belt (0.07), gold collar and cuffs, the skirts shorter (0.42)
with gold edges up their front and back (coat_skirt edges) and deeper folds;
dark boots; the dao wider (0.095) and more curved (0.14), a bigger brass guard,
angled further out (0.6, 0.2, 0.8); no white emblems, a gold-bordered cloth
(cloth_border 0.05), a dark leather saddle; the hat's crown dark (#2a2220), a
bigger crimson tassel (hat_cone tassel 1.5); more weight and muscle.
specks.py clean. Tint per direction 25.7 21.5 27.2 27.2 26.2 24.0 24.1 25.4,
mean 25.2 %.

## Critic round 2: fixes asked
A dark non-civ saddle cloth with civ only on its border, a dark saddle with
raised front and back, the grey horse showing under the thigh; longer darker
boots with iron stirrups; a wide dark hem band on the skirts; the rider 5-10 %
larger; a taller pointed hat with a slimmer darker fur brim and the crimson
tassel from the tip; the dao angled up and out in 3-4.

### Round 6 (critic3)
The saddle cloth dark (#2a2220, blanket_tint False) with a civ border
(border_tint), half its old size (cloth_frac 0.5) so the horse shows under the
thigh; a dark hem band on the skirts (trim #2a1e14, 0.065); the rider 1.07;
the hat taller (height 1.25) with a slimmer dark fur brim (#5a4a3a, hat_cone
brim) and the tassel 1.2 from the tip; the dao further up and out
(0.55, 0.12, 0.95). specks.py clean. Tint per direction 23.2 22.3 22.5 20.8
21.7 30.2 21.8 22.0, mean 23.1 %.

## Critic round 3: continuity 76.75, fresh 67.75, average 72.25 (+3.85)
Fixes: the hat a dark cone on a wide pale fur ring, a bigger tassel with a dark
edge; an even felt cloth (not a blotchy fur) with a gold inner line, the civ
border and bells kept; a lighter saddle; the gold 'stirrups' (the bow case)
dark; the dao clear of the horse's head in 3-5, a brighter edge; a contrasting
belt, collar and cuffs; the skirt's split visible.

### Round 7
hat_cone brim_wide (a wide thick roll, #d2c4a2) and a dark edge under the
tassel (1.7); the cloth #2e2418 with less grime (tack blanket_wear) and a gold
line inside the border (tack inner_line); the saddle #7a5434; the bow case
dark leather (tack bow_case as a key); the steel #dcdfe4; a dark belt 0.1,
dark collar and cuffs; the skirts split wider (a0 55); the dao (0.45, -0.3,
0.95): it crossed the face side-on.
### Round 8 (critic4)
The cloth without the cloth mottle (tack blanket_even: a plain material), the
tassel 1.35, the dao (0.65, -0.08, 0.9). specks.py clean. Tint per direction
20.4 18.3 20.6 19.2 20.6 28.1 20.0 19.4, mean 20.8 %.

## Critic round 4: continuity 82.5, fresh 69.0, average 75.75 (+3.5)
The hat, cloth and dao fixes landed. Fixes: the dao high and back in 3-5
(clear of the head and ears); the coat broken (a crossover lapel, a hem
trim, stronger skirt folds); the torso and arms heavier; the crown a clearer
cone; a cleaner dapple grey with dark points and mane.

### Round 9 (critic5)
The 'rider' pose's sword hand high and back (-0.4, 0.1, 0.86), elbow 0.8, the
dao (0.5, -0.35, 0.9); a dark crossover lapel (gear chest_strap #2a1e14, 0.06),
the coat's gold hem 0.06, skirt folds 0.09; the rider 1.11, weight 0.9,
muscle 1.0; the hat taller (1.45), the tassel 1.0; the horse's mane #8a8a88,
points #4a4a48, coat_contrast 0.7. specks.py clean. Tint per direction 18.8
18.6 22.3 21.9 22.0 30.5 20.3 18.3, mean 21.6 %. Finals gate passes.

## Critic round 5: continuity 84.5, fresh 78.0, average 81.25 (+5.5)
## FINAL = critic 5 (90fcbb4d, 81.25)
Closed under the three-critic-round limit (the lead, 2026-11-23). HEAD
reproduces the critic-5 sheet (max diff 0.008); in verify_all.sh.
Open points (critic 5):
- The dao in 4 hides behind the hat; in 5 it lies among the horse's ears (a
  per-direction carry, or one that clears 4 and 5).
- The brown felt goes near-black at 1x in 4, 7, 8: one value step lighter, a
  wider civ border, so cloth, saddle and boot read as three shapes.
- Beside the larger rider the grey reads spindly next to the Knight and Keshik
  mounts: the horse ~5 % bigger, the dark points kept.
