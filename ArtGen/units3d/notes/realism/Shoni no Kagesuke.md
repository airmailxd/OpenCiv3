# Shoni no Kagesuke (realism pass: a NumPy unit remade in units3d)

## Design in words

**Who.** Shoni Kagesuke, the Kamakura shogunate's commander in Kyushu at the
first Mongol invasion (1274), remembered for shooting the Mongol general
Liu Fuheng from the saddle. The unit is a mounted samurai archer of the late
Heian to Kamakura period, made as a tabletop miniature.

**Must read at 1x:**
1. The yumi: the tall asymmetric bamboo longbow, held a third of the way up
   so its upper limb towers above the kabuto, drawn in the yabusame shot to
   the left (umate) with the right hand back past the ear. No neighbour has
   this silhouette (the Keshik's and Mounted Warrior's bows are short).
2. The o-yoroi: big square laced sode on the shoulders, the laced kusazuri
   over the thighs and saddle, the dou in laced rows with a pale stencilled
   leather front (tsurubashiri); the lacing (odoshi) is the civ colour.
3. The o-kabuto: a dark iron hoshi-kabuto with a bell-shaped laced shikoro,
   fukigaeshi turnbacks and a small gold maedate.
4. A small, stocky golden-buckskin native horse with a shaggy dark mane, a
   lacquered kura with tall arches, slipper abumi, and the civ munagai and
   shirigai cords with big tassels; an ebira at the right hip, its barred
   eagle-feather fletching up behind the shoulder.

**Identity from the shipped NumPy sprite** (ArtGen/units catalog
`mdj_mounted.py`, archetype `mdj_horse_rider`): dark lacquered armour with
civ sode, pale hakama, a kabuto with a small gold crest, the long yumi held
upright on the left, the ebira; a palomino (golden, dark mane) with a civ
saddle cloth and civ cords with tassels.

**Civ colour, deliberate regions:** the odoshi lacing (sode, kusazuri, the
dou's rows, the shikoro) and the horse's cords and tassels. Measured on the
installed sheet (tint.py, alpha>127): shipped 14.6 %
(14.3 / 12.4 / 14.2 / 15.7 / 15.6 / 12.3 / 16.0 / 15.9).

**Calibration:** PC2 has no Civ3 install, so calibration/Shoni no
Kagesuke.json is measured from the installed NumPy sheet (DEFAULT frame 0,
alpha>127 bbox per direction; flic headers from its metadata):
scratch/numpymounted/cal_from_sheet.py.

**Stock neighbours:** Keshik (the closed units3d final) and Knight.

## Rounds
Drafts: scratch/numpymounted/d<n>.png (compare) and c<n>_* (close-ups).

### Round 1
First build: o_kabuto (maedate), o_yoroi sode and kusazuri, the dou in civ
rows with a pale tsurubashiri panel, yumi 2.2 m drawn in 'yabusame', the
ebira, kura, abumi, civ cords. The yumi in the fit shrank the unit (ppm 30.9,
pixels 0.85); the shikoro flared like a hat brim and hid the face; the breast
strap and crupper arcs ran out to the nose and past the tail (the low head
and tail at strap height); the yumi's rattan bands read as a striped pole.
### Round 2
A bell-shaped shikoro (flare 0.45); smaller kura arches with thin gilt rims;
tack 'strap_reach' (0.85, 1.0); darker, thinner rattan bindings; the yumi out
of the fit (OFFHAND_NO_FIT): pixels 1.36, so FIT_SCALE 0.86.
### Round 3
No belt pouches (they stood as black plates at the belly); the yumi 1.95;
the yabusame pose re-set from the critic-approved bow_draw (twist 10/16, the
bow hand out to the left front, the draw hand at the cheek); longer kusazuri
(5 lames x 0.085). pixels 1.01; tint 8.7 %.
### Round 4-5
Bigger sode (0.28 x 6 lames), kusazuri lames 0.095, fewer dark rows on the
dou (threshold 0.86), a civ aori with a gold edge (frac 0.9, drop 0.42).
### Batch 1 sheet (6690f12b)
Tint per direction 10.5 / 8.9 / 9.8 / 11.4 / 12.1 / 15.6 / 15.3 / 12.6, mean
12.0 % (shipped 14.6). fitcheck bbox-area 1.14, pixels 1.02 (the yumi above
the shipped box). specks.py clean.

## Batch 1 critic round: A 75, B 85, average 80.0, CRITICAL (A)
In SW the bow and arrow crossed the head and chest; faces a dark blob under
the kabuto; in N/NE the ebira and drawing arm merged.
### Round 2 build (batch_numpymounted_1r2)
A new ready hold ('yumi_ready'): the yumi upright at arm's length out to the
left (hand_l (0.62, -0.16, 0.5)), the right hand on the string beside it, the
arrow nocked toward the left front (BOW_DRAW up (-0.12, 0.05, 1), forward
(-0.95, 0.3, 0)); face_glow 0.6, the kabuto lifted 0.15; a black ebira.
The lead's size rule: sized to the stock Keshik (FIT_SCALE 0.97).

## Round 2 critics: A 81, B 85.5, average 83.25 (bow critical closed by both)
### Round 3 build (batch_numpymounted_3, 1ed7a4c7)
The yumi further out to the side (hand_l (0.74, -0.06, 0.56)), thicker (1.6),
pale rattan bands (#a88a5c; #d2bc90 read as a striped pole), a dark grip
wrap (yumi_grip); Shoni's own dou rows 0.55 (dark plates between the
lacing). lace_frac on the lames was not used: at 1x it read all dark
(Nakamaro's round 2).

## Round 3 critics: A 85.75, B 87, average 86.4, PASS
## FINAL = round 3 (1ed7a4c7; HEAD 44a0c5c7 reproduces it, in the gate)
review/shoni-no-kagesuke/final.png (+ _red): shipped, final, Knight, Keshik;
copies in ArtGen/units3d/reviews/realism/shoni-no-kagesuke/.
Later pass: a little more dark lacquer between the lacing on the back.
