# Pirate (realism pass, NumPy remake): design

The Modern mod's Pirate is a South China Sea pirate's steam junk (the mod's
picture, "SteamTiger", is a paddle junk; the NumPy entry marks it modern). Real
object: a Fujian / Guangdong sea-going junk of the mid-19th century, ~25-35 m, a
flat raked transom bow with painted eyes, a high overhanging square stern with a
painted transom and a great rudder, tarred planking with ochre wales; three masts
staggered off the centreline (the fore raked forward) with fan-battened lug sails
of tanned matting; here fitted out as a steamer, as Chinese and pirate owners did
from the 1850s-60s (paddle wheels amidships under half-round paddle boxes, a tall
black funnel), cannon run out of a row of ports, the black flag.

NumPy source (identity only): art-base ArtGen/units/catalog/modern_mod_sea_air.py,
design 'steam_junk' in models/modern_mod_sea_air.py. Installed sheets SteamTiger*
(140 x 140). Stock neighbours for its own finals: Man-O-War and Privateer.

## Baseline (shipped NumPy sprite)
Tint (tint.py, DEFAULT): 6.2 / 12.4 / 8.1 / 6.6 / 6.2 / 14.6 / 8.4 / 6.8 % (mean 8.7).
Side-view bbox at 1x 69 x 32 (a low ship).

## What must read at 1x
A junk, not a European ship: the flat bow and the high square stern, the battened
lug sails (straight edges, dark battens) near the centreline; a steamer: the tall
black funnel and the paddle boxes; a pirate: the black flag with the skull, the
guns run out.

## Civ colour (shipped 8.7 %)
Painted: a strake along the side between ochre wales, the paddle boxes' fanned
vents, the stern transom panel, the sails' head panels, the funnel bands; pennants.

## Build
ships.steam_junk_pirate in its own block (SJUNK dict; keys sj_*). L 2.6 = 30 m.
Sails sheeted 12 deg off the centreline (fore-and-aft sails: near the centreline
they show their faces in the six oblique and side views; 30-38 deg put them
edge-on in dirs 1 and 5).
- Draft 1: sails rust-brown read as wooden boards; the funnel short; tint 1.7 %.
- Draft 2: canvas-tan sails, bigger paddle boxes with civ vents, tint 3.1 %.
- Draft 3: sheet 12 deg, a civ transom panel.
- Draft 4: the funnel and deckhouse aft of the main sail (the sail hid them in the
  profiles), civ head panels on the sails.
- Pennants and the black flag set so they stay joined to their mastheads in every
  view (_nn_flag now offsets by the mast's x); luff and leech lines on the sails.
- Size (the lead's rule): against the stock Ironclad: fit_scale 1.0 -> 0.93.

## Round 1 (sent to critic batch_numpynaval_1)
Tint per direction (columns 1-8): 5.2 / 2.8 / 10.0 / 11.6 / 9.5 / 3.8 / 8.2 / 8.1 %
(mean 7.4; shipped 8.7; the bow and stern views low). ppm 43.02. specks.py: clean.
Size: vs the stock Ironclad bbox-area 0.98, pixels 1.04 (side width 61 vs 75 px, height
30 vs 24); vs the stock Privateer 0.45 / 0.49. vs the NumPy sprite 0.91 / 0.82.

## Critic round 1 (batch_numpynaval_1): 59.75 (A 54, B 65.5); identity at 1x unclear (A critical, B borderline)
Both: far too small, a smudge at 1x. Fixes (lead): size against the stock Privateer
(~0.90-1.00 of its pixels); tall fanned lug sails in ochre / red-brown with strong
battens; solid paddle housings, a bigger funnel, muzzles in ports along a bulwark, a
high junk stern; no skull-and-crossbones (Western): a civ pennant.

- Round 2: fit_scale 0.93 -> 1.3 (against the Privateer); sails red-brown (#b46e36 /
  #a2602e), taller (main 0.86 x 1.2 on a 1.42 mast), fanned (luff 0.55 of the
  leech), 8 battens 0.011 (edges 0.015); the masts staggered (fore x +0.08, mizzen
  -0.12) and each sheeted its own way (fore 24, main 12, mizzen -16 deg) so the
  junk shape holds bow-on; the paddle housings solid, built wedge by wedge (ochre
  vents and dark slots, a civ crescent round the rim); the funnel r 0.1 x 0.82; a
  red, ochre-capped gun bulwark along the waist with six ports a side and long dark
  guns with muzzle swells (the old hull ports gone); stern sheer 0.42 and a poop
  0.18 high; a long civ pennant at the main instead of the skull flag.
  Sent to critic batch 2. Tint per direction (columns 1-8): 7.3 / 7.3 / 9.2 / 10.3 /
  10.9 / 11.9 / 10.4 / 9.5 % (mean 9.6; shipped 8.7). ppm 56.30. specks.py: clean.
  Size: vs stock Privateer bbox-area 0.90, pixels 0.96 (side 80 vs 95 px, height 47
  vs 43); vs stock Ironclad 1.95 / 2.04. vs the NumPy sprite 1.81 / 1.61.

## Critic round 2 (batch_numpynaval_2): 80.6 (A 80, B 81.25), identity critical closed
Fixes: guns that read at 1x (3-4 a side, ~2x thicker and longer, dark against the
bulwark); the bow-on view (N/S) a thin pole: splay the sails, wider paddle housings,
a high transom wider than the bow; the red trim read as civ on the red sheet: black
lacquer or ochre, civ only on the pennants and the funnel bands.

- Round 3 (the last): four guns a side, r 0.045 (was 0.024), run out 0.28 with
  muzzle swells, in black ports in an ochre bulwark with a black cap; sails splayed
  (fore 36, main 18, mizzen -32 deg); paddle housings 0.22 wide (was 0.14), ochre
  rims instead of civ; the transom 1.3x the hull's end; the red trim (deckhouse
  roof, transom, bulwark cap) black lacquer (#2a201a); civ only on the pennants
  (main 0.44 x 0.12, others 0.3 x 0.09) and two funnel bands 0.08 deep (civ_strake,
  civ_paddle, civ_head, civ_bow, civ_transom off; the strake ochre).
  Sent to critic batch 3. Tint per direction (columns 1-8): 3.3 / 0.8 / 2.4 / 4.0 /
  3.7 / 1.7 / 3.4 / 3.8 % (mean 2.9; shipped 8.7: low by the lead's instruction).
  ppm 55.33. specks.py: clean. Size: vs stock Privateer bbox-area 0.93, pixels 0.98
  (side 78 vs 95 px, height 46 vs 43). vs the NumPy sprite 1.90 / 1.66.
- Round 3, civ fix (the lead, before the critics): civ_strake and civ_paddle back on
  (painted areas; only the red trim had to go). Tint per direction (columns 1-8):
  7.9 / 6.3 / 6.7 / 8.9 / 9.6 / 9.4 / 9.0 / 8.5 % (mean 8.3; shipped 8.7).
  specks.py: clean. This is the round-3 sheet the critics see.

## Critic round 3 (batch_numpynaval_3): 85.4 (A 84.5, B 86.25), PASS

## FINAL: critic round 3 (own round 3 with the civ fix, commit 1a24d44e)
Finals in ArtGen/units3d/reviews/realism/pirate/ and the handoff review/pirate/
(rows: shipped NumPy sprite, final, stock Privateer, stock Galleon;
scratch/numpynaval/finals_nn.sh). In the finals gate (scratch/numpynaval/gate.sh).

Critic history (average; A / B):
- C1 59.75 (54 / 65.5; identity at 1x unclear)
- C2 80.6 (80 / 81.25; identity critical closed)
- C3 85.4 (84.5 / 86.25) = FINAL, PASS

Later-pass list (optional):
- Gun muzzles ~3-4 px proud of the hull at 1x in the side views, with a light rim
  or a pale port surround.
- The S view of the splayed sails is a little busy.
