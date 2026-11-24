# Samurai: realism design (2026-11-23)

## The real model

A mounted-class retainer of the late Sengoku period (c. 1570-1600) fighting on foot, in **tosei-gusoku**
("modern equipment"): the armour of the age of Nobunaga, Hideyoshi and Ieyasu, and the one most people picture.
Civ3's Samurai is a Middle Ages unit and its original wears kimono and hakama with a katana, no armour; the
overhaul keeps the original's colours (indigo-blue civ colour, dark hakama, white tabi) and its katana, and
adds the armour a samurai went to war in.

- **Dō**: an okegawa-dō, a cuirass of horizontal iron lames riveted into a barrel, black-lacquered, with a
  taller breast plate (mune-ita) at the top front and back. Lames read as rows of ridges, never one shell.
- **Kusazuri**: seven tassets hanging from the dō's lower edge to mid-thigh, five lames each, laced
  edge to edge (kebiki odoshi): from outside you see mostly the silk lacing, with the lacquered top edge of
  each lame as a thin dark line. Gaps between the tassets.
- **Sode**: two shoulder boards of six laced lames, hanging off the shoulders, square and slightly curved.
- **Kote**: cloth armoured sleeves covered in mail (kusari) with a lacquered iron forearm plate and hand plate.
- **Haidate**: the thigh apron of small lames, under the kusazuri, over the hakama.
- **Hakama**: wide, pleated, ballooning from the hip and gathered under the knee (kobakama, the field form).
- **Suneate**: iron splint shin guards (shino-suneate) from the ankle to the knee, over the hakama's gathered hem.
- **Kabuto**: a black suji-bachi (ridged bowl) with a peak, a three-lame laced shikoro (neck guard) flaring out,
  the fukigaeshi (the top lame's ends turned back beside the face), and a gilt kuwagata maedate (two flat
  horns in a V) on the brow. A dark red menpō (half mask) over the cheeks and chin.
- **Daishō** at the left hip, edge up through the obi: the katana and the shorter wakizashi.
- **Sashimono**: a civ-colour banner on a pole from a holder on the back of the dō, top bar and all.
- White tabi, straw-coloured waraji sandals.

## What must read at 1x (about 42 px tall)

1. **The kabuto's silhouette**: wide flared shikoro with the gold V of the kuwagata above the brow.
2. **The armour's mass and squareness**: sode squaring the shoulders, the kusazuri flaring over the hips,
   wide hakama legs. A broad trapezoid figure, nothing like the skinny pyjama shape before.
3. **The sashimono** standing above and behind the head (in the civ colour).
4. **The katana**: the right hand on its hilt across the body, the long scabbard slanting back from the
   left hip, out of the silhouette.

## Pose in words

Planted, feet a little apart, upright (the owner's rule: never hunched), ready to draw: the right hand across
the body on the katana's hilt at the left hip, the left hand on the scabbard by its mouth, thumb at the guard
(the owner's decision of 2026-10-16: a hand on the hilt, no drawn sword, no folded arms). Head level.

## Civ colour (2-3 regions, no noise)

Measured on the installed DEFAULT sheets (tint pixels / unit pixels, alpha > 127, mean and range over the 8
directions): Legionary 28% (13-40), Pikeman 34% (29-37), Swordsman 9% (7-10), Ninja 5% (2-8), Musketman 38%
(34-42), Spearman 17% (11-24), Hoplite 11% (7-18), Medieval Infantry 19% (12-31); MDJ: Ashigaru Spearman 47%,
Minamoto Samurai 45%, Oda Arquebusier 28%, Otomo Spearman 15%, Warrior Monk 30%, Shoni no Kagesuke 13%, Otomo
no Makuta 6%; the shipped Samurai 26% (21-34). Foot units run from 5% to 47%, most between 10% and 35%.
Target: about 15-25%, even across the directions (the banner shows most from front and back, the sode
from the sides, so the two balance).


1. The **odoshi lacing** of the sode and the kusazuri (and the shikoro): the lames' lacing in the civ colour,
   broken by the lacquered lame edges into horizontal rows.
2. The **sashimono** banner.
3. (small) The cords: the dō's sugake lace pairs. Everything else in its material: black lacquer, dark iron,
   undyed dark cloth, white tabi, gilt horns.

## Parts list

dō (mune-ita + 5 nakagawa lames, front and back; barrel-shaped), watagami shoulder straps, 7 kusazuri × 5 lames,
2 sode × 6 lames with a top kanmuri-ita, 2 kote (mail cloth + forearm plate + hand plate), haidate (4 rows),
hakama (wide, pleated, gathered), 2 suneate (5 splints each), 2 tabi + waraji, obi, kabuto (bowl with 16 ridges,
tehen ring, peak, 3-lame shikoro, 2 fukigaeshi, kuwagata on a haraidate), menpō, katana (scabbard, tsuba, wrapped
grip, kashira), wakizashi, sashimono (holder, pole, top bar, banner with a mon), shaven pate and topknot (under the helm).

## Reusable kit (ArtGen/units3d/yoroi.py)

Entry key `'yoroi': {...}` (units.dress calls yoroi.dress). Each piece is its own function so the 12 MDJ units
can pick: `do`, `kusazuri`, `sode`, `kote`, `haidate`, `hakama`, `suneate`, `kabuto`, `menpo`, `sashimono`,
`wakizashi`; and the helpers `laced_rows` (any shell or skirt into lames), `lacquer`, `odoshi`. See README.

## Rounds

### Round 1 (2 directions, 16 samples, raw passes)
Seen: it reads as a samurai: the kabuto's gold V, the sode, the laced kusazuri rows, the banner. Wrong: civ colour
~47% of the figure (banner far too big: 0.3 x 0.78 m; shikoro, kusazuri and sode lacing all civ; the dō's
sugake cords read as blue blobs on the chest); the black lacquer reads mid grey (the clear coat mirrors the sky);
hakama and suneate read as dark tights from the front.
Change: banner 0.22 x 0.5 m, pole 1.05; sugake off; kusazuri lames show more lacquer edge (40%); lacquer coat
0.55 -> 0.3, roughness 0.42.

### Round 2 (full sheets, compare.py beside shipped, Legionary, Pikeman)
Seen: at 1x it reads by shape: banner over the head, gold kuwagata, the scabbard slanting out behind the left hip,
white tabi; side views show the sode as squared blue blocks. Wrong: civ only 12.7% (9-16%), the lacing reads dim
(40% lacquer edge, steps in shadow); the whole figure is near black (do, kote, hakama, gaiters) — darker and
flatter than the stock units beside it, a dark blob at 1x; the legs still narrow; the armour mass narrow for a
trapezoid read.
Change: hakama a kaki brown (#6b5c48) and ballooned 0.1; mail sleeves lighter; gaiters warmer; kusazuri lacing
72% of each lame and flare 1.55; sode 0.27 x 0.30, out 0.03.

### Round 3
Seen: the kaki hakama gives the legs a wide, light, trapezoid read, and the figure no longer a black blob. Wrong:
the kusazuri and haidate are mostly buried inside the ballooned hakama (only short blue lines at the hips), so
the civ colour stays at 12.8% and the tassets don't read as a skirt of plates.
Change: yoroi.clear_of(): the kusazuri and the haidate pushed out along their normals over the hakama (and the
kusazuri over the haidate) before their lames are cut.

### Round 4 (civ 19.5%, 15.5-23.3% by direction)
Seen (sheets and a 3x close-up): civ share now in range and even; the kusazuri read as a laced skirt; sode rows,
shikoro rows, menpo and the gilt kuwagata read in the close-up. Wrong: clear_of's per-vertex push tore the
kusazuri hem into ragged blue patches, and the haidate's civ lacing pokes through as blotches (noise at 1x);
kusazuri too short (hem ~0.82 m).
Change: clear_of pushes a smoothed field (stiff plates ride over the bulge whole); the hakama stays slim under
the tassets and balloons from 0.86 m down (as a real hakama does under kusazuri); kusazuri length 0.55 (to
mid-thigh); haidate laced in dark brown cords, not civ.

### Round 5 (civ 19.9%, 16.3-22.7%)
Seen: the kusazuri hang clean as a laced skirt to mid-thigh, the hakama balloons below them to the gaiters, the
sode and shikoro read as laced rows; no more blotches. Wrong: the do's front is a large flat dark-grey area (the
lames' steps too shallow to read): a "flat area" tell.
Change: do lames stepped 0.014, and a bronze rivet line along each lame's foot (16% of the lame).

### Round 6
Seen: the do came out bronze over most of its front: laced_rows gave the region above the top lame (the
mune-ita) and below the lowest lame's foot the second material when there is no suspension lacing (a bug).
Change: those regions are plate; rivets a darker, duller bronze, 12% of each lame.

### Round 7 (civ 19.9%, 16.2-22.6%)
Seen: the do is black lacquer with thin bronze rivet lines at each lame's foot, the mune-ita plain lacquer; at
1x the figure reads by shape (banner, gold V, squared sode, flared kusazuri, ballooned hakama, slanting
scabbard, white tabi) and holds its own beside the Legionary and Pikeman. Left: a few dark specks in the front
kusazuri's lacing (the layer beneath showing through at the panel gaps); the face is mostly menpo and shadow.
Next: the animations.

### Animations (anim.ACTIONS['Samurai'], from the still stance)
ATTACK1: still -> the draw (blade out level across the front) -> jodan overhead -> a lunging downward cut ->
follow-through -> chudan; reads in every direction, the blade a bright line. FORTIFY: draws into chudan and holds
it (the blade forward at throat height, a crouch). RUN: a trot with the hand kept on the hilt, the banner riding
on the back. DEATH: recoil, knees go, falls on his back. The scabbard at the hip is shown empty while the blade
is out ('hip_drawn').
Open: on the ground in DEATH the banner pole goes into the ground (it is fixed to the spine); the figure stands a
little smaller than the Pikeman (the calibration's height, as shipped); a few dark specks in the front kusazuri
lacing at 2x.

### Critic round 1 (70 and 65.5) -> round r2
Asked: the dark mass at 1x in the front views; the weapon ready (overrides the hand on the hilt); the banner
edge-on in the side views. Changed: lacquer a warm brown-black (#34211a) with a sharper clear coat; kuwagata 25%
taller and wider, brighter gilt, a gilt boss on the maedate; menpo red lacquer (#9a3020); hakama lighter
(#8c7a5c) with deeper pleats; the katana drawn (yoroi.katana_drawn: a bright 4.2 cm blade, gilt habaki, pale
grip wrap) and held two-handed in chudan (units 'chudan' stance), its empty scabbard at the hip; the banner yawed
35 degrees. Actions: ATTACK1 cuts from chudan and returns to it, FORTIFY settles into hasso, RUN keeps the guard
(not re-rendered this round). Civ 18.9%.

### Critic round 2 (78 and 75.25) -> r3
Changed: the katana in a high chudan (units 'seigan' stance), the blade mostly upright and leaning out to his
right (blade_dir -0.5, -0.12, 0.86): any strong horizontal lean points at the camera in some direction (tried
forward-out and 45-degree out first); each do lame's foot a glossy red-brown lacquer band (22%); a gilt rim on
the helmet's peak; the menpo larger, brighter red, with a pale moustache; the sashimono raised on its pole
(1.45 m, 0.18 x 0.42 m) to clear the kabuto; the empty saya shorter and close down the left leg.
Seen at 1x: the blade shows in 6 of 8 directions; in direction 2 it is short beside the banner pole, in 6 nearly
parallel to it. Civ 19.2%.

### Critic round 3 (79 and 72) -> r4
Changed: a hasso guard (units 'hasso' stance), the fists before the right shoulder and the blade 40 degrees off
vertical, out to his right and a little back (away from the banner pole); the do's own lacquer lighter and
glossier (#3e2820, coat 0.55) with brighter red-brown lame edges; the menpo a little self-lit (0.35) so it reads
under the brim; the banner longer and wider (0.22 x 0.6 m) on a thicker pole, its foot just above the kabuto;
the tabi light grey.
Seen at 1x: blade and pole are two distinct lines in directions 0, 1, 4, 5, 6, 7; in 2 the blade crosses the
body diagonally, in 3 it is a short stub beside the helmet. Civ 21.2%.

## Final (round 4)

Critic scores (two picture-only critics, PROCESS.md rubric): R1 70 / 65.5 = 67.75; R2 78 / 75.25 = 76.6;
R3 79 / 72 = 75.5; R4 81 / 71.5 = 76.25 (final: plateaued; within 0.35 of R2, the continuity critic scores it
higher, and it keeps the banner and guard fixes). No critical failure in any round.

Animations re-rendered from the round-4 still (hasso): ATTACK1 cuts from hasso and returns; FORTIFY settles into
a crouched hasso; RUN keeps the guard; DEATH falls on his back, the sashimono hidden once he is past 50 degrees
(it is fixed to the spine and would go into the ground; 'fall_hide').

Open for a later pass:
- the blade is a 1-px hairline at 1x; it should be about 2 px of bright steel, with a dark tsuba and hilt;
- the blade runs beside or parallel to the banner pole in directions 7-8 and is hidden by the body in 4;
- direction 1's chest and arms are still a dark mass: lighter kote and lame edges as in direction 2;
- the do's back is flat: it needs a sheen;
- the critics read the civ colour at 30-40%; measured it is 21% (15.6-24.0% by direction).
Animation check (2x, directions 1, 3, 6): all four play cleanly; in FORTIFY's crouched hasso the blade is hidden
by the body in direction 3. Final pictures: reviews/realism/samurai/final.png and final_red.png.
