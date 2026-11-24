# Galley (realism pass): design

The Galley is a Greek war bireme of about 550-480 BC (the ship of the
Archaic vase paintings and of the Phoenician biremes on the Nineveh
reliefs): a long, low, black-tarred hull on a bronze ram, two banks of
oars, a fighting deck with the marines' shields along its rail, a tall
inward-curling stern ornament (the aplustre), a single mast with a
square, brailed linen sail. Built as a painted 1/600-ish tabletop
miniature (ship scale), in believable wood, pitch, linen and bronze.
Neighbours for the comparison (the adjacent ancient ships): Curragh and
Dromon (stock).

## Baseline (round 0, as shipped)
- Reads as a galley by its big sail and the oar comb, but the hull is a
  pale tan tub (L:B 4.6, beam 0.48 on 2.2), the mast reaches 0.67 L and
  the sail spans 0.73 L: a toy proportion; the sail is square to the hull
  so in profile (4, 8) it is a line and the ship is a plank.
- Flat values: one tan hull tone, one cream sail; the stern canopy a
  striped civ box.
- specks.py: 2-57 pieces per direction (the 1 px oars break up head-on
  and stern-on): a critical failure as shipped.
- Tint (tint.py, DEFAULT, alpha>127): 26.8 / 39.1 / 27.1 / 3.9 / 24.3 /
  38.2 / 24.4 / 3.9 % (mean 23.5): nearly all on the sail.
- Values (1x, opaque non-tint): mean 113, p10 50, std 51, upper 135 /
  lower 99.

## Real proportions (Greek bireme / Olympias-type, scaled)
A bireme of ~30 m, beam ~3.8 m over the hull (L:B ~8; the trireme
Olympias 36.9 x 5.5 m over the outriggers), main deck ~1.4 m above the
water, the fighting-deck rail ~2.3 m, the aplustre's top ~4.5 m, the
mast ~10.5 m above the water, the yard ~9.5 m, the sail ~9 x 6 m, oars
~4.2 m (upper bank) and ~3.8 m (lower bank), the ram ~2 m long.
Model (L 2.2 = the hull without the ram, 1 unit = 13.6 m): beam 0.28,
deck 0.10, fighting-deck rail 0.17, aplustre 0.33, mast top 0.77, sail
0.66 x 0.44. The rig is drawn 1.3x real (sail ~0.86 x 0.58, mast top
~0.95), the usual tabletop exaggeration, so the sail still reads at 1x;
the hull keeps its real L:B and freeboard. The ship's length on screen is
held near the stock Galley's (fit_scale if the fit grows it).

## What must read at 1x (all 8 directions)
1. The long, low, slender black hull with a sweeping sheer, the bronze
   ram jutting forward at the waterline under a forward-curving stem, and
   the tall aplustre curling forward over the stern (bow vs stern at a
   glance, head-on and stern-on included).
2. Two banks of oars: a comb along each side, splayed out and down into
   the water, so head-on/stern-on the ship has wide "wings".
3. The square sail on a yard braced ~25 deg off square (shows area even
   in profile), with vertical brail lines and the civ device.
4. The fighting deck: a lighter natural-wood rail with a row of round
   shields (dulled bronze/hide, mid value: not specks of light).

## Values
Stock ancient ships are lighter on top than below (Galley 135 / 99,
Dromon 149 / 101, Curragh 140 / 123). Plan: the sail light linen (not
white, shaded by its billow and the brail seams), the deck and rail mid
natural wood, the hull dark pitch with a lit wale line, oars mid wood,
the ram bronze. Aim: upper - lower >= 35, p10 <= 50, no p99 above ~215.

## Civ colour (stock: Galley 23.5 % mean, 3.9 in profile; Dromon 20.1 %;
Curragh 4.8 %)
Three deliberate regions:
1. The sail: two broad vertical bands at its edges and a bold device in
   the middle (the Civ3 galley's "double crescent" kept as a solid shape).
2. The bow cheeks: the painted panel round the eye on both sides of the
   bow (the period "painted cheeks"), with the eye in pale and dark on it:
   reads head-on and in profile.
3. The stern awning over the helmsman (plain civ cloth on two hoops).
Aim: 15-25 % on the sail-facing directions, 6-10 % in profile (higher than
the stock's 3.9 there: the bow and awning carry it).

## Rounds
Render ~12 s each (Blender + compose, through run_locked). Tint is
tint.py's share per direction (columns 1-8); values from a 1x
opaque-non-tint luminance measure (upper / lower = the halves of each
direction's box). The new builder is ships.galley_bireme (BUILDERS
'Galley'); the old galley() is kept, unused. No shared code changed: new
colour keys only (setdefault, unused by any other unit).

- Round 1: the bireme at real proportions, rig 1.3x. ppm 55.8: the fit
  grew the hull ~20 % past the stock's length on screen. The fighting deck
  one big pale plane (flat); shields invisible (metal rendered black);
  the 1 px stays and braces broke into 4-36 specks per direction.
  Tint 20.2 / 20.0 / 9.2 / 9.6 / 19.9 / 19.8 / 8.0 / 8.5 %.
- Round 2: fit_scale 0.82, stays r 0.015 (braces dropped), deck darker,
  shields painted (dielectric) and raised, aplustre thicker. specks
  clean. At 1x the ship read clearly smaller than the stock Galley and
  Dromon (the sail half their height); values too dark (upper 99 / lower
  79, mean 87).
- Round 3: rig 1.45x real (sail 1.0 x 0.66 = 0.45 L, mast top 1.13), the
  sail lighter linen. Still small at 1x with fit_scale 0.82.
- Round 4: fit_scale 0.92 (ppm 49.3: the hull ~10 % longer than the
  stock's on screen, the whole ship about the stock's size at 1x), the
  pitch and the gallery a step lighter. The deck still a flat tan plane.
- Round 5: the fighting deck opened as on early Greek warships: a central
  gangway and a side gangway inside each gallery, dark rowing wells
  between them crossed by 22 thwarts; 15 shields a side (r 0.045); the
  ram bigger and raised to show above the water.
- Round 6: the shields' faces turned up-and-out (62 deg) so they catch
  the light: a row of lit bronze discs along the rail. Sent to critic
  round 1. Tint per direction (columns 1-8): 21.5 / 23.2 / 9.2 / 9.0 /
  19.9 / 22.6 / 6.8 / 8.6 % (mean 15.1). Values: mean 92, p10 38, p50 80,
  p90 167, p99 201, std 48, upper 115 / lower 78 (stock Galley 113, p10
  50, std 51, 135 / 99). ppm 48.98. specks.py: clean.

## Critic round 1: 67.75 (continuity 67, fresh 68.5), FAIL, no critical failure
Both, point for point: (1) the stays made a thick dark tent/crane
triangle over the ship; (2) the ram a tiny dark point, only one oar bank
read, the shield row a line of pale beads; (3) the ship smaller and
duller than the shipped Galley and the Dromon, the sail small and low (a
sliver in 3/4/7/8), the hull a flat dark band.

- Round 7: rig to the shipped scale (sail 1.25 x 0.8, yard 1.27, mast top
  1.33, belly 0.15, braced 30 deg); stays r 0.011 in a lighter cordage to
  the stem and aplustre tops; ram 0.3 long, r 0.056, bright bronze
  (#c09a52, metal, roughness 0.33), the eye bigger; shields off, the
  gallery wall's lower half a dark oar-port strake; lower bank thinner
  (r 0.012), steeper, with blades on both banks; wale lighter and wider,
  planking contrast stronger. The 1.1 px stays broke into 7-24 specks per
  direction.
- Round 8: stays dropped (stays=False); fit_scale 1.0 (ppm 48.97). A
  2 px speck at the awning's edge in direction 3.
- Round 9: dark hoops along the awning's ends (specks clean); the lower
  bank darker ('oar_low' #5e4830) and splayed forward, so the two banks
  read as two fans.
- Round 10: sail linen a step darker (#d8c8a2, no near-white). Sent to
  critic round 2. Tint per direction (columns 1-8): 30.5 / 30.1 / 10.9 /
  17.0 / 30.8 / 31.5 / 10.1 / 15.2 % (mean 22.0; stock 23.5). Values:
  mean 97, p10 39, p50 83, p90 181, p99 207, std 53, upper 133 / lower 78
  (stock 113, 135 / 99). Footprint at 2x (alpha>127, mean px per
  direction): 2990 (shipped 2872); boxes 95x79 / 57x93 / 95x91 / 125x80
  (shipped 86x88 / 76x92 / 85x88 / 117x86). ppm 48.97. specks.py: clean.

## Critic round 2: 72.1 (continuity 74.75, fresh 69.5), no critical failure
Landed: stays gone, the ram, the footprint, the sail. Both: the near-
black hull is the odd one out against the roster's tan hulls (the fresh
critic: a borderline house-style problem) and its light planks read as
painted stripes; the second oar bank still one even comb at 1x; civ
25-32 % on the sail-facing views (want ~18-20); the fresh critic: the
30 deg brace makes the mirror pairs differ and skews the sail in 4/8;
the continuity critic: too much bare mast over the yard.

- Round 11: hull in mid-brown wood (#94744a / #8a6a42 planks), tar kept
  only as a band at the waterline (tar_z 0.035) and one continuous dark
  wale tube; two thin plank seams below it and a light gunwale cap over
  it; sail bands only (no device), brace 14 deg, belly 0.2, yard 1.24 with
  the mast top at 1.27; upper oars reach 0.28, the lower 0.15 and lower
  (deck - 0.062, below the wale, so the two port rows have planking
  between them). Tint fell to 6.5-12.4 % (thin bands); in profile the
  sail was a sliver again.
- Round 12: broad edge bands (|u| > 0.58), brace 20 deg (a compromise:
  the critic-1 sliver vs the critic-2 skew). Tint 10.2-22.1 %.
- Round 13: the upper bank at mid-stroke, its blades out of the water
  (tips at z 0.075), the lower bank dipping: two tiers in profile; upper
  reach 0.25 (a tighter spread head-on and stern-on). Sent to critic
  round 3. Tint per direction (columns 1-8): 20.5 / 22.3 / 13.5 / 10.8 /
  20.0 / 21.1 / 12.6 / 11.2 % (mean 16.5). Values: mean 106, p10 42, p50
  94, p90 185, p99 215, std 54, upper 140 / lower 83 (stock 113, 135 /
  99). ppm 49.24. specks.py: clean.

## Critic round 3: 71.0 (continuity 76.25, fresh 65.75): no gain (1 of 2)
Landed: the sail, the lower yard, the civ colour; the hull only partly.
Both: the oar tiers still one comb at 1x (the darker lower bank lost in
the hull's shadow); the eye and ram too small; the hull wants structure
(warmer, more mass and depth; value contradicted, kept), and one
continuous gunwale cap instead of the dashed cream/black rail. Fresh: the
stern-on view a forked insect blob (the aplustre's fan).

- Round 14: lower oars LIGHT (#b0905e) and reaching further (0.62 of the
  upper's reach), the upper tier's blades up at z 0.1: two rows of tips;
  ram 0.38 long, r 0.07, brighter (#d2aa5e), fins wider; the eye bigger
  (0.072 x 0.042 white in a dark outline, pupil r 0.03) just behind the
  ram; hull beam 0.30 -> 0.34, wood a touch warmer (#9c7a48 / #906e40);
  the gallery one tone (the dark strake gone) under one continuous light
  gunwale cap (r 0.012); the aplustre one solid curl (r 0.055 tapering,
  the side fan removed). Sent to critic round 4. Tint per direction
  (columns 1-8): 19.9 / 23.0 / 11.6 / 10.0 / 19.6 / 22.6 / 10.9 / 10.3 %
  (mean 16.0). Values: mean 112, p10 47, p50 108, p90 185, p99 214, std
  51, upper 142 / lower 93 (stock 113, 135 / 99). ppm 48.29; footprint
  2931 px (shipped 2872). specks.py: clean.

## Critic round 4: 76.75 (continuity 82, fresh 71.5): gain (+4.65), new best
Landed: the warm hull and continuous gunwale, the eye, the ram, the
sternpost curl; the oar tiers only at 2x. Both: the tiers still merge at
1x; the civ colour in 4/8 only a thin sail-edge strip; the sail plainer
than the roster's emblazoned sails; the deck interior black and pale
"racing stripes". Contradicted (kept at 20 deg): the brace.

- Round 15: upper bank swept forward (+0.1), lower aft (-0.12), shorter
  blades (0.065) so the two rows of tips keep a gap; a civ trident device
  in the sail's middle (bands thinned to |u| > 0.7); a civ band along the
  gallery; the wells mid-brown (#5a4630) with lighter thwarts (#866848);
  billow 0.24; the aplustre to 0.53; the steering oars thicker (r 0.022,
  bigger blades) in the light oar wood. The band was inside the gallery
  wall (the tube's flattening axis was vertical).
- Rounds 16-18: the band set outside the wall and flattened against it
  (r 0.026, 0.25): a clear civ stripe along both sides in the profiles.
  Sent to critic round 5. Tint per direction (columns 1-8): 20.9 / 22.7 /
  14.8 / 12.7 / 18.2 / 19.4 / 11.5 / 13.6 % (mean 16.7). Values: mean
  114, p10 49, p50 111, p90 183, p99 209, std 50, upper 142 / lower 95
  (stock 113, 135 / 99). ppm 48.17; footprint 3016 px (shipped 2872).
  specks.py: clean.

## Critic round 5: 74.25 (continuity 82.5, fresh 66): no gain (1 of 2)
Landed: the trident, the belly, the sternpost, the steering oars. Both:
the opposite sweeps crossed the tiers in an X in 4/8 (impossible), still
one comb at 1x. By majority (fresh critics of rounds 2, 3, 5): the braced
yard read as a lateen rig and the mirror pairs as two rigs: square it.
Continuity: the gangway a black stripe with pale dashes; civ ~22-25 %:
halve the sail's outer bands.

- Round 19: both tiers swept the same way (-0.06: parallel), separated
  by height (upper tips at z 0.1, lower in the water, staggered half a
  station); yard square (brace 0); bands |u| > 0.85; the black hatches
  dropped; the ram 0.43 long, r 0.08, brighter (#dcb466).
- Round 20: bands |u| > 0.78.
- Round 21: the rowing wells floored 0.026 under the gangways (shallow,
  mid-brown #6a5436) with the thwarts (#8a6c48) on it: a built deck with
  bench rows, no black slots. Sent to critic round 6. Tint per direction
  (columns 1-8): 13.3 / 15.5 / 13.3 / 9.9 / 10.5 / 9.9 / 10.5 / 9.9 %
  (mean 11.6; the square sail is foreshortened in the diagonals, and the
  bands halved). Values: mean 121, p10 54, p50 119, p90 190, p99 211, std
  51, upper 142 / lower 101 (stock 113, 135 / 99). ppm 46.79; footprint
  2927 px (shipped 2872). specks.py: clean.

## Critic round 6: 78.25 (continuity 85.5 PASS, fresh 71): gain (+1.5), new best
Every critic-5 fix landed for the continuity critic (the square yard "an
improvement", the mirror pairs match). Both: the two banks still one comb
at 1x in 4/8. Fresh: the stern view (6) near an identity fail (a white
square over a spiky splay of oars); the hull a thin pale sliver with a
flat deck; the sail +15-20 %. Continuity: a dark centre line on the deck.

- Round 22: oar reach 0.21 and both tiers swept further aft (-0.1): a
  tighter double row end-on; upper blade tips up to z 0.13, the lower
  shafts a step darker (#9a7a4e), bigger lower ports; the hull below the
  wale darker ('wood_low' #7e5e38 / #735432), beam 0.34 -> 0.37; the sail
  +10 % (1.375 x 0.88, yard 1.33, mast top 1.36); the trident 12 %
  bigger; the sternpost taller (0.62) and darker ('post' #6e5232); the
  rowing wells shallower (0.016).
- Round 23: the sternpost's section widened athwartships (0.85 x 1.6) so
  it shows as a broad dark post end-on. Sent to critic round 7. Tint per
  direction (columns 1-8): 14.2 / 16.6 / 14.4 / 10.0 / 10.6 / 10.4 /
  10.6 / 10.1 % (mean 12.1). Values: mean 123, p10 53, p50 120, p90 191,
  p99 212, std 52, upper 144 / lower 100 (stock 113, 135 / 99). ppm
  45.65; footprint 3020 px (shipped 2872). specks.py: clean.

## Critic round 7: 76.1 (continuity 86.5 PASS, fresh 65.75): no gain (1 of 2)
Continuity praised the dark lower strakes, the beam, the bigger sail and
trident, the soft deck line (kept). Merged fixes: the upper hull and deck
a pale bleached beige (upper half 144 vs the stock's 135): warm and
darken toward the Dromon's gold-brown, softer plank checker, more
freeboard; the two tiers at 1x (lighter lower blades, a dark gap); sail
billow shading and darker brails; a warm highlight on the sternpost's
outer edge; steeper steering oars with blades.

- Round 24: upper wood #8e6a3a / #8a6638 (softer checker), gallery
  #866238, deck #957444 / #8a6a3e; freeboard D 0.125 -> 0.145 (beam
  kept); lower oars #a8875a (the wale and the gallery's foot between the
  rows of ports read as the dark gap); the sail in three tones by the
  belly (lit middle, 'sail_mid' #cbbb95, 'sail_shade' #bba982 toward the
  edges and the foot), brails #54442f; a light tube along the sternpost
  curl's outer edge; steering oars steep and close (to +-0.17, -1.04,
  -0.05) with broad blades. Sent to critic round 8. Tint per direction
  (columns 1-8): 15.8 / 19.1 / 15.9 / 10.6 / 12.4 / 12.3 / 12.4 / 10.7 %
  (mean 13.7). Values: mean 115, p10 53, p50 116, p90 172, p99 199, std
  45, upper 131 / lower 97 (stock 113, 135 / 99). ppm 46.32; footprint
  3072 px (shipped 2872). specks.py: clean.

## Critic round 8: 80.0 (continuity 87.5 PASS, fresh 72.5): gain (+1.75), new best
Kept: the gold-brown hull, the freeboard, the sternpost highlight, the
sail gradient. Merged fixes: the upper bank read as a pale railing (dark
shafts wanted), the lower blades a step lighter; a visible sheer and
more tone along the hull, a shaped stern end in 6; the ram a long gold
spear tip (shorter, blunter, darker bronze); the steering oars shorter
below the stern; the sail's brightest part a little lighter.

- Round 25: upper oars dark (#5a4230) against the pale gallery, lower
  oars #b8986a (stagger kept); sheer 0.14/0.07 -> 0.22/0.14 (stern/bow);
  plank-butt tone patches ('wood_b' #94703c) along the upper strakes; the
  stern's last strakes in the shaded 'wood_low'; ram 0.28 long, r 0.09,
  a flat blunt head with the three fins, darker bronze #a88240; steering
  oars end at (+-0.16, -0.99, 0.0); sail #e2d3ae. Sent to critic round 9.
  Tint per direction (columns 1-8): 16.4 / 18.3 / 16.5 / 10.9 / 12.6 /
  10.4 / 12.6 / 11.0 % (mean 13.6). Values: mean 112, p10 49, p50 112,
  p90 172, p99 199, std 47, upper 131 / lower 91 (stock 113, 135 / 99).
  ppm 47.59; footprint 3264 px (shipped 2872). specks.py: clean.

## Critic round 9: 80.75 (continuity 89.5 PASS, fresh 72): +0.75, no gain (1 of 2)
Three fresh critics in a row: "washed out / bleached" beside the shipped
Galley and the Dromon. The lead: the cause is mostly the sail (the stock
ships carry bold blue panels; ours a pale sheet with a thin trident, tint
13.6 % vs the stock's 23.5). Fixes: bolder sail civ colour (18-22 %), the
upper hull ~10-15 % darker and warmer, an eye readable at 1x, a lit top
on the ram, a crisper masthead and darker yard tips.

- Round 26: bands |u| > 0.64, trident at 0.7 h: tint 24.9 %, the trident
  cropped by the sail's edges and merged with the bands into blocks.
  Wood #82602e / #7e5c2c, 'wood_b' #8a6430, gallery #7c5a30; the eye a
  dark almond (0.062 x 0.04) in a light ring (0.092 x 0.058); a gold ridge
  along the ram's top; a tarred masthead cap; tarred yard-arm ends.
- Round 27: the trident at the sail's full height (0.5 h) with bold
  strokes (0.14), bands 0.72. The heads a little blocky.
- Round 28: strokes 0.12, the outer tines out to +-0.56, narrower barbed
  heads: a clear trident head-on. Sent to critic round 10. Tint per
  direction (columns 1-8): 24.3 / 32.4 / 24.3 / 13.0 / 21.4 / 23.5 / 21.5
  / 12.9 % (mean 21.6; stock 23.5). Values: mean 106, p10 48, p50 102,
  p90 168, p99 197, std 46, upper 125 / lower 90 (stock 113, 135 / 99).
  ppm 47.49; footprint 3149 px (C9 3264, shipped 2872). specks.py: clean.

## Critic round 10: 83.4 (continuity 89.5 PASS, fresh 77.25): gain (+2.6), new best
The bold sail worked. Merged fixes: the sternpost a stubby log in the
diagonals (taller, thinner, a gilt cap); the end-on views a muddy wedge
with a noisy deck; fore- and backstays as on the shipped Galley and the
Dromon (solid at 1x); the outer sail bands ~1 px narrower (19-20 %);
the two oar rows a step more contrast.

- Round 29: sternpost to 0.72, thinner (r 0.05 -> 0.018, section 0.85 x
  1.3) with a gilt finial and the rim highlight; deck checker quieter
  (deck_alt #907042, thwarts #7c603c); the stem's lower part painted in
  the civ colour (shows head-on above the ram); upper oars #4a3626,
  lower #c4a474; stays r 0.016 (one solid line at 2x) from the masthead
  to the stem and to the sternpost's top; bands |u| > 0.76. The
  centreline forestay covered the trident's shaft head-on.
- Round 30: two forestays from the masthead to the bow's quarters (a V
  head-on, the trident clear). (Rounds 31-32: the bands are quantized by
  the sail mesh's 40 columns: 0.72 = C10's width, 20.4 %; 0.73-0.76 one
  column narrower, 17.6 %.) Kept at 0.75, narrower as asked.
- Round 32 = round 30 (bands 0.75). Sent to critic round 11. Tint per
  direction (columns 1-8): 19.9 / 26.4 / 20.0 / 10.1 / 17.1 / 20.1 / 17.1
  / 10.1 % (mean 17.6). Values: mean 105, p10 46, p50 102, p90 166, p99
  196, std 45, upper 125 / lower 87 (stock 113, 135 / 99). ppm 47.67;
  footprint 3356 px (the stays' pixels; the box as C10's). specks.py:
  clean.

## Critic round 11: 81.75 (continuity 89 PASS, fresh 74.5): no gain (1 of 2)
Both count the stays against it (busier, off-roster; booms or poles in
4/8); both like the new sternpost. Fixes: the stays off (C10's rig),
keep the C11 sternpost, deck, painted stem and oar contrast; a clearly
brighter rim on the sternpost; the eye, the wale and the ram's cap a
little bigger/darker.

- Round 33: stays=False; the sternpost's rim highlight #c8a060 ('post_hl'),
  r 0.016; the eye 0.11 x 0.07 (light ring) with a 0.074 x 0.048 dark
  eye; the wale r 0.016 in #2a2018; the ram's gold ridge r 0.018. Bands
  0.75 (without the stays the tint is back in range). Sent to critic
  round 12. Tint per direction (columns 1-8): 21.7 / 28.8 / 21.7 / 11.1 /
  18.7 / 20.6 / 18.6 / 11.0 % (mean 19.0). Values: mean 107, p10 46, p50
  105, p90 169, p99 197, std 47, upper 129 / lower 89 (stock 113, 135 /
  99). ppm 47.67; footprint 3220 px. specks.py: clean.

## Critic round 12: 84.25 (continuity 89.5 PASS, fresh 79): +0.85, no gain (2 of 2) -> plateau

## FINAL: critic round 12 (own round 33, commit 475383f6)
The highest average and the current code. Reproduction check (after the
Curragh's commit 7e7169e2): the re-render matches critic 12's sheets
(every flic: mean abs diff <= 0.00001, max 2/255; tint layers identical).
final.png / final_red.png = critic 12's (rows: shipped, final, Curragh,
Dromon), in ArtGen/units3d/reviews/realism/galley/ and the handoff folder.
Tint per direction (columns 1-8): 21.7 / 28.8 / 21.7 / 11.1 / 18.7 /
20.6 / 18.6 / 11.0 % (mean 19.0). ppm 47.67.

Critic history (average; continuity / fresh):
- C1 67.75 (67 / 68.5)
- C2 72.1 (74.75 / 69.5)
- C3 71.0 (76.25 / 65.75)
- C4 76.75 (82 / 71.5)
- C5 74.25 (82.5 / 66)
- C6 78.25 (85.5 / 71)
- C7 76.1 (86.5 / 65.75)
- C8 80.0 (87.5 / 72.5)
- C9 80.75 (89.5 / 72)
- C10 83.4 (89.5 / 77.25)
- C11 81.75 (89 / 74.5)
- C12 84.25 (89.5 / 79) = FINAL, plateau

Open points (the fresh critics'): the two oar banks still mostly one comb
at 1x; the eye and wale small at 1x; standing rigging split the critics
(C11's stays cost points: none in the final).

Lessons for the fleet: bold civ colour on the sail near the stock amount;
warm, saturated wood; a visible sheer; ends that read at 1x (ram,
sternpost with a rim highlight); oar tiers by value contrast; standing
rigging minimal, only where the stock ship clearly shows it; thin lines
need r >= ~0.015 at ppm ~47 to stay one piece.
