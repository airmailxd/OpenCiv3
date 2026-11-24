# Nakamaro Horseman (realism pass: a NumPy unit remade in units3d)

## Design in words

**Who.** The Medieval Japan scenario's mounted samurai swordsman ("Horse
Samurai" flics): a late-Heian to Kamakura bushi in o-yoroi with a tachi, made
as a tabletop miniature.

**Must read at 1x:**
1. The tall gold kuwagata rising in a V from a dark iron o-kabuto with a
   bell-shaped laced shikoro and fukigaeshi.
2. The o-yoroi: big laced sode, laced kusazuri, the dou in laced rows with a
   pale tsurubashiri; the odoshi lacing is the civ colour.
3. The tachi held out low to the right, its point forward (the shipped
   sprite's carry), the reins in the left hand.
4. A small, stocky dark-bay native horse with a lacquered kura and red
   aori, slipper abumi, and civ munagai and shirigai cords with tassels.

**Identity from the shipped NumPy sprite** (`mdj_mounted.py`): lacquered
armour laced and plated in the civ colour (sode and kusazuri), the tall gold
kuwagata, a sword held out low to the right; a dark bay with civ cords and
tassels and a red saddle cloth (and a gold chanfron, left out: horse masks
are later than the Kamakura period).

**Civ colour, deliberate regions:** the odoshi lacing (sode, kusazuri, the
dou's rows, the shikoro) and the horse's cords and tassels. Shipped 19.4 %
(18.5 / 17.5 / 16.2 / 18.7 / 21.0 / 23.0 / 20.2 / 20.4).

**Calibration:** from the installed NumPy sheet (no Civ3 on PC2), as the
Shoni no Kagesuke's.

**Stock neighbours:** Keshik (the closed units3d final) and Knight.

## Rounds
Drafts: scratch/numpymounted/d<n>.png (compare) and c<n>_* (close-ups).

### Round 1
First build as the Shoni's armour with a tall gold kuwagata, the katana held
out low to the right ('tachi' pose), a red aori. The thin blade broke into
specks in 4 directions; pixels 1.21; the strap arcs ran to the nose.
### Round 2-3
The tachi as a sabre prop (0.92 x 0.06, spine 0.024, a round steel guard);
strap_reach; FIT_SCALE 0.91: pixels 1.00. Tint 9.1 %.
### Round 4-6
Bigger sode, longer kusazuri, fewer dark rows; a civ aori with a broad red
border (the sprite's civ cloth over red); a deep civ hitatare under the
armour (tint_shade 0.6: the sprite's civ arms): tint 18.2 %. The sabre spine
0.036; the specks left were the kuwagata's tips: blunter, thicker blades.
### Batch 1 sheet (6690f12b)
Tint per direction 21.2 / 21.8 / 16.9 / 14.6 / 14.7 / 18.3 / 17.1 / 18.4,
mean 17.9 % (shipped 19.4). fitcheck bbox-area 0.93, pixels 1.02. specks.py
clean.

## Batch 1 critic round: A 64, B 65.5, average 64.75, CRITICAL (both): civ
The red-bordered civ aori read as a second civ colour; civ ~25-30 %, the
armour almost all civ; the sword a long white rod, across the neck in SE.
### Round 2 build (batch_numpymounted_1r2)
No civ robe or aori: a matte dark leather aori (low grime), the kura's rims
lacquer (kura_rim); the dou dark lacquer with civ lacing rows (rows
'#1e1a16' 0.75); civ only on the lames' lacing and the cords and tassels.
A real tachi prop (mdj_riders 'tachi': 0.66 m, koshi-zori curve, matte steel
with a dark edge line, a round iron tsuba, a long dark grip), held in a
high guard up and out to the right (WEAPON_UP (0.45, 0.35, 0.85), the hand
out at (-0.52, -0.08, 0.5)); face_glow 0.55, the kabuto lifted 0.12. The
lead's size rule: sized to the stock Knight (FIT_SCALE 1.02). Tint 10.4 %
(lace_frac options tried at 0.5-0.9 read dark at 1x: 4-8 %).

## Round 2 critics: A 72.5, B 81.5, average 77.0 (civ and clip criticals closed)
### Round 3 build (batch_numpymounted_3, 1ed7a4c7)
The tachi lit polished steel (option 'bright'), 0.73 m, width 0.075, back
0.032, held wider (WEAPON_UP (0.62, 0.4, 0.68)); a chestnut horse
(#a0602e) so the dark rider reads against it.

## Round 3 critics: A 80.25, B 82.5, average 81.4 (no critical)
## FINAL = round 3 (best round; the 3-round cap), 1ed7a4c7, in the gate
review/nakamaro-horseman/final.png (+ _red): shipped, final, Knight, Keshik.
Later pass:
- SE: the tachi hides behind the torso: swing it out to the right as in SW.
- S/SW: a lighter breastplate panel.
- N/NE/NW: dark lacquer between the lacing rows on the back.
