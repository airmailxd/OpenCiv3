# Troop Ship (realism pass, NumPy remake): design

The Modern mod's Troop Ship is a WWII attack transport (APA). Real object: a
Haskell-class APA (1944-45, on the VC2-S-AP5 Victory hull): 139 x 19 m (L:B ~7.3),
a raked stem, a short raised forecastle, a cruiser stern; a tall three-tier house
amidships with rows of portholes, the bridge and its wings, a pole mast and one
stout funnel; LCVPs nested in Welin davits along both sides of the house and
stacked on the hatches; king posts with cargo booms over the hatches fore and aft;
a 5-inch/38 aft, a 3-inch forward, 40 mm tubs. Measure 22: navy blue from the boot
top to the main deck's level, haze grey above.

NumPy source (identity only): art-base ArtGen/units/catalog/modern_mod_sea_air.py,
design 'troopship' in models/modern_mod_sea_air.py. Installed sheets Cargo*
(120 x 69). Stock neighbours for its own finals: Transport and Destroyer. It must
not read as the closed Transport final (an LST): a fine sharp bow, a tall central
house and a funnel, boats in davits, no vehicle park.

## Baseline (shipped NumPy sprite)
Tint (tint.py, DEFAULT): 5.5 / 3.9 / 5.5 / 5.2 / 5.4 / 5.8 / 5.5 / 5.1 % (mean 5.2).
Side-view bbox at 1x 97 x 24 (long and low).

## Civ colour (shipped 5.2 %)
Painted: the hatch covers (four), the band on the house front and sides, the
funnel band; the ensign.

## Build
ships.troopship_apa in its own block (APA dict; keys ap_*). L 3.4 = 139 m.
- Draft 1: greys mirrored the sky (metallic 0.15), near-white; the king posts and
  booms broke into specks in the profiles; a low freeboard.
- Draft 2: matte paint, darker navy and haze; D 0.25; four davit stations with
  bigger boats; thicker king posts (specks clean).
- Draft 3: a taller house (0.11 / 0.09 / 0.07), a deeper funnel band.
- Draft 4: darker navy, a deeper civ band on the house.
- Draft 5: a longer house (-0.56 .. 0.4), a bigger ensign on a thicker staff.
- Size (the lead's rule): against the stock Transport: fit_scale 1.0 -> 0.82.

## Round 1 (sent to critic batch_numpynaval_1)
Tint per direction (columns 1-8): 4.9 / 6.7 / 4.1 / 3.9 / 4.0 / 4.4 / 3.3 / 3.9 %
(mean 4.4; shipped 5.2). ppm 43.82. specks.py: clean.
Size: vs the stock Transport bbox-area 1.11, pixels 0.97 (a longer, slimmer ship: side
width 81 vs 57 px at 1x, height 18 vs 24). vs the NumPy sprite 0.64 / 0.62.

## Critic round 1 (batch_numpynaval_1): 72.6 (A 68.5, B 76.75), no critical
Both: it reads as a slim destroyer. Fixes: a wider, higher, bulky cargo-liner hull;
the midships house ~2x taller and boxier, a funnel with real height; king posts
with tall booms fore and aft of the bridge; haze-grey landing craft with visible
davit arms; civ moved to a hull stripe, a deck band and the funnel band; a dark
waterline band and a darker deck. Size within ~0.90-1.10 of the stock Transport.

- Round 2: B 0.48 -> 0.62, D 0.25 -> 0.32, fuller (fullness 0.28, bow_p 2.4); house
  tiers 0.24 / 0.18 / 0.13 high and narrower (0.8 / 0.74 / 0.6 of the beam) so the
  boats show alongside, a second window row; funnel r 0.13 x 0.42; four king posts
  (y 0.82, 0.5, -0.68, -1.0) 0.52 high with long dark booms; LCVPs haze grey
  (#90969c) in heavier dark davits; civ: a painted hull stripe (civ_stripe), a band
  across the forecastle deck (civ_deck), the funnel band, the ensign (hatches and
  house bands no longer civ); boot top to z 0.04; deck #2e3238; fit_scale 0.74.
  Sent to critic batch 2. Tint per direction (columns 1-8): 5.4 / 3.1 / 5.0 / 6.5 /
  5.4 / 2.5 / 5.0 / 6.4 % (mean 4.9; shipped 5.2). ppm 37.84. specks.py: clean.
  Size: vs stock Transport bbox-area 1.04, pixels 1.03 (side 71 vs 57 px, height 23
  vs 24). vs the NumPy sprite 0.60 / 0.66.

## Critic round 2 (batch_numpynaval_2): 83.0 (A 82, B 84), no critical
Fixes: the landing craft (the APA's tell) lost grey-on-grey: 3-4 a side hung out on
dark davit arms, olive or a warmer grey with a light rim and a dark shadow beneath;
king posts and booms ~1 px thicker at 1x; a raised bridge tier with dark windows
ahead of the funnel.

- Round 3 (the last): four LCVPs a side (y -0.46, -0.22, 0.02, 0.26) hung 0.075
  out over the side, olive drab (#6c6e42) with a light gunwale rim and a dark
  shadow block beneath, in heavier dark davits (0.032) with falls; king posts r
  0.048, booms r 0.032, crosstrees 0.05; a fourth, raised bridge tier (0.1 high)
  with a dark window band on its front and sides; fit_scale 0.72.
  Sent to critic batch 3. Tint per direction (columns 1-8): 4.1 / 2.5 / 3.6 / 4.6 /
  3.8 / 1.9 / 3.3 / 4.6 % (mean 3.5; shipped 5.2). ppm 36.31. specks.py: clean.
  Size: vs stock Transport bbox-area 0.99, pixels 0.99 (side 68 vs 57 px, height 22
  vs 24). vs the NumPy sprite 0.57 / 0.64.
- Round 3, civ fix (the lead, before the critics): the civ hull stripe wider (0.09
  high, centred 0.07 under the deck edge) and longer (s -0.95 .. 0.88, so it reaches
  the stern), the forecastle deck band 0.22 deep (was 0.16). Tint per direction
  (columns 1-8): 6.4 / 3.9 / 5.9 / 8.2 / 6.4 / 3.5 / 5.9 / 8.2 % (mean 6.0; shipped
  5.2). specks.py: clean. This is the round-3 sheet the critics see.

## Critic round 3 (batch_numpynaval_3): 85.25 (A 84.0, B 86.5), PASS

## FINAL: critic round 3 (own round 3 with the civ fix, commit 1a24d44e)
Finals in ArtGen/units3d/reviews/realism/troop-ship/ and the handoff
review/troop-ship/ (rows: shipped NumPy sprite, final, stock Transport, stock
Galleon; scratch/numpynaval/finals_nn.sh). In the finals gate.

Critic history (average; A / B):
- C1 72.6 (68.5 / 76.75)
- C2 83.0 (82 / 84)
- C3 85.25 (84.0 / 86.5) = FINAL, PASS

Later-pass list (optional):
- The landing craft as 3-4 separate mid-olive hulls a side with gaps between them
  and visible davit arms (critic A still reads them as one cream strip).
