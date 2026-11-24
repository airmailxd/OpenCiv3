# numpy-naval (realism pass): group notes

The three NumPy-made ships (ArtGen/units, the 'ship_r2' archetype) remade in units3d, on
realism/numpy-naval (from realism/naval 366fdc1f, which holds the 11 closed naval finals):

- Japanese Warship (MDJ, catalog/league_mdj.py, design 'atakebune'): a Sengoku atakebune.
- Pirate (Modern mod, catalog/modern_mod_sea_air.py, design 'steam_junk', modern): a pirate's steam junk.
- Troop Ship (Modern mod, same file, design 'troopship', modern): a WWII attack transport (APA).

The NumPy sources were read for identity only (art-base f17bb128, read-only).

## Calibration
calibration/<Unit>.json made from the INSTALLED NumPy sheets in art-base (C7/ModernArt/Art/Units/<Unit>):
the DEFAULT frame 0 bbox per direction (alpha > 127, base + tint) and the flics' headers, by
scratch/numpynaval/calib_numpy.py (the numpy-siege builder's script, its source folder pointed at art-base).
The units keep their sheet names and sizes: Galley* (66 x 53), SteamTiger* (140 x 140), Cargo* (120 x 69).
fitcheck runs against art-base (a copy of fitcheck.py with SHIP pointed there).

## Size rule (the lead, 2026-11-24)
Size against the stock units of the class, not the NumPy sprite: the Japanese Warship
between the stock Galleon and Carrack (and the sailing ships' hull-length rule), the Pirate
0.90-1.10 of the stock Ironclad or Privateer, the Troop Ship 0.90-1.10 of the stock Transport.
scratch/numpynaval/fitstock.py <new> <stock dirs...> reports bbox-area, pixels and the side-view
width against each stock counterpart; the hand-back reports both ratios.

## Shared code
None changed. ships.py gets four new bounded blocks before `_steel_builder` (spliced by
scratch/numpynaval/splice.py), each with its own names:
- `numpy-naval helpers` (prefix _nn_: hull loft, waterline foam, side lines, a streaming flag; three new
  material keys nn_foam, nn_black, nn_dark via DEFAULTS.setdefault);
- `japanese warship (realism pass ...)`: ATAKE, atakebune_sengoku (keys ak_*);
- `pirate (realism pass ...)`: SJUNK, steam_junk_pirate (keys sj_*, flag_black, bone, gun);
- `troop ship (realism pass ...)`: APA, troopship_apa (keys ap_*);
and three registrations after the Transport's line: BUILDERS['Japanese Warship'|'Pirate'|'Troop Ship'].
New shared options: none (rig_scale and fit_scale are each builder's own dict entries; defaults 1.0).
Block check (scratch/nav_blocks.py): 14 realism blocks, one per unit, no duplicate registrations or defs.

## Gate
scratch/numpynaval/gate.sh: the realism/naval finals gate (11 ships) pointed at this worktree.

## Group closed (all PASS)
- Japanese Warship: FINAL C2 85.1 (e2aca4dc code).
- Pirate: FINAL C3 85.4 (1a24d44e).
- Troop Ship: FINAL C3 85.25 (1a24d44e).
Finals via scratch/numpynaval/finals_nn.sh (shipped NumPy sprite from art-base, final,
two stock neighbours). The finals gate (scratch/numpynaval/gate.sh) covers 14 units:
the 11 realism/naval finals and these three (their sheet refs in
scratch/numpynaval/sheets_ref).
