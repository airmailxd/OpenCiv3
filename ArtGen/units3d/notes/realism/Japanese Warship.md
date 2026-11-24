# Japanese Warship (realism pass, NumPy remake): design

MDJ's Warship is an atakebune, the Sengoku castle-ship (Oda Nobunaga's iron-plated
atakebune at the second battle of Kizugawaguchi, 1578; the Tokugawa Atake Maru,
1630s). Real object: a broad wasen hull ~30-50 m long, its whole deck boxed in by a
plank fortress (tate-ita) with loopholes (sama) for arquebuses and bows, Oda's
faced with iron; a two- or three-storey tower (yagura) of white plaster and black
timber under tiled irimoya roofs; one mast with a big mat sail; driven in battle by
50-160 sculling oars (ro) worked from under the walls; nobori banners and maku
curtains with the clan's mon.

NumPy source (identity only): art-base ArtGen/units/catalog/league_mdj.py, design
'atakebune' in models/league_sea.py (walled war junk, tower, mat sail with a mon,
civ curtain and nobori, oars). Installed sheets: Galley* (66 x 53), calibrated
against the stock Galley. Stock neighbours for the unit's own finals: Galley and
Dromon. Critic sheet batch_numpynaval_1 (Galleon and Transport finals at the bottom).

## Baseline (shipped NumPy sprite)
Tint (tint.py, DEFAULT): 15.3 / 15.7 / 12.3 / 9.3 / 13.2 / 13.9 / 10.9 / 12.9 % (mean 13.0).
Side-view bbox at 1x 59 x 42.

## What must read at 1x
The boxed-in fortress silhouette (dark walls standing over the hull, rows of lit
loopholes); the stepped white tower with dark roofs and gold shachi; the comb of
oars; the square mat sail with the civ mon; nobori at the corners.

## Civ colour (shipped 13 %)
Painted areas, not lines: the mon on the sail (a takeda-bishi lozenge), the maku
curtain along the walls' top and round the tower's foot, six nobori.

## Build
ships.atakebune_sengoku in its own block (ATAKE dict; keys ak_*). L 2.4 = 33 m.
- Draft 1: walls too low (the open deck read as a tan boat), the tower small; the
  oar blades broke into 1-px specks in the profiles.
- Draft 2: walls 0.2 high, a three-storey tower, thicker oars; the walls rendered
  pale (a single tall face per segment took the rivet key: the wall loft needed rows).
- Draft 3: black lacquered walls, wall maku, six nobori, a bigger mon.
- Draft 4-5: deeper wall maku (0.7-0.98 of the wall), bigger nobori and tower maku;
  the shrouds and forestay thick enough to stay joined at 1x (specks clean).
- Size (the lead's rule for the NumPy remakes, 2026-11-24): sized against the stock
  Galleon and Carrack, not the Galley-sized NumPy sprite: fit_scale 0.98 -> 1.49.

## Round 1 (sent to critic batch_numpynaval_1)
Tint per direction (columns 1-8): 14.6 / 11.0 / 10.2 / 13.3 / 12.9 / 8.4 / 9.9 / 13.9 %
(mean 11.8; shipped 13.0). ppm 74.64. specks.py: clean.
Size: vs the stock Galleon bbox-area 1.05, pixels 1.12, side-view hull length 1.00
(97 / 97 px at 1x); vs the stock Carrack 0.81 / 0.86 / 0.87 (97 / 112) -> inside the
Galleon-Carrack range. vs the NumPy sprite 2.27 / 2.41 (that sprite was Galley-sized).

## Critic round 1 (batch_numpynaval_1): 75.0 (A 66.5, B 83.5), no critical
A: an open rowing boat with a pagoda on top. Fixes: a full-length black-lacquered
box wall from the waterline to the tower base with a row of light loophole slits,
replacing the open gunwale and the bare tan wood; 2-3 clearly stepped storeys with
dark tiled roofs and pale ridge caps, taller than the walls; a ribbed mat sail with
horizontal battens; civ ~18 % (B): the castle-base and wall bands to dark lacquer,
keep the mon and banners; plank seams and a darker waterline on the remaining wood.

- Round 2: the wall's foot at z 0.03 (wall_z0), the wall from there to the old top,
  closed at both ends and longer (y -1.12 .. 0.98), with a seam band, panels and one
  row of 22 light slits a side (slits); the oars out of the wall's face; no civ maku
  (wall_maku None, maku False); storeys 0.17 / 0.14 / 0.11 high; roofs #3c4046 /
  #2a2d32, pale eaves, pale ridge caps and end tiles (ak_ridge); the sail with 9
  heavier battens and fine horizontal ribs; a darker deck and waterline band.
  Sent to critic batch 2. Tint per direction (columns 1-8): 9.9 / 10.5 / 4.3 / 6.7 /
  7.4 / 6.3 / 3.7 / 7.3 % (mean 7.0; shipped 13.0). ppm 74.52. specks.py: clean.
  Size: vs stock Galleon bbox-area 1.05, pixels 1.15, hull length 1.00; vs stock
  Carrack 0.81 / 0.88 / 0.87. vs the NumPy sprite 2.26 / 2.47.

## Critic round 2 (batch_numpynaval_2): 85.1 (A 83.5, B 86.75), PASS

## FINAL: critic round 2 (own round 2, commit e2aca4dc)
The R2 code, unchanged. Finals in ArtGen/units3d/reviews/realism/japanese-warship/
and the handoff review/japanese-warship/ (rows: shipped NumPy sprite, final, stock
Galleon, stock Carrack; scratch/numpynaval/finals_nn.sh). Added to the finals gate
(scratch/numpynaval/gate.sh; sheet refs in scratch/numpynaval/sheets_ref).

Critic history (average; A / B):
- C1 75.0 (66.5 / 83.5)
- C2 85.1 (83.5 / 86.75) = FINAL, PASS

Later-pass list (the critics):
- Raise the black wall to deck height or roof it, so no tan deck shows.
- Wrap the bow and stern overhangs.
- Tone the white tower walls down slightly.
