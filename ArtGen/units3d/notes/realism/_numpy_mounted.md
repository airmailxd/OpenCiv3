# NumPy mounted group (branch realism/numpy-mounted)

The NumPy generator's mounted units remade in units3d, on the mounted
family's code (branched from realism/mounted at 3ca8d271).

Batch 1: Shoni no Kagesuke, Nakamaro Horseman (Medieval Japan).
Batch 2 (queued): Teutonic Knight, Roughrider (League; identity from
D:\Dev\C3-wt\art-base).

## Calibration without Civ3
PC2 has no Civ3 install, so a NumPy unit's calibration/<Unit>.json is
measured from its installed sheet: DEFAULT frame 0, the alpha>127 bbox and
foot point per direction in 1x unit space, the flic headers from its
metadata (D:\Dev\_handoff\realism\scratch\numpymounted\cal_from_sheet.py).
render_vehicles.fit then sizes the model to those boxes as for the stock
units; FIT_SCALE trims it to the shipped opaque area (fitcheck.py).

## New shared code (every default keeps the old behaviour)
- New module `mdj_riders.py`: hat 'o_kabuto'; o_yoroi() (sode, kusazuri);
  abumi(); kura(); props 'yumi' and 'ebira'; RIDE_POSES 'yabusame', 'tachi'.
  Imported only for mounted.MDJ_RIDERS units (and by asset_tack for 'kura').
- mounted.build: archetype 'mdj_horse_rider' mounts like 'horse_rider'.
- mounted.seat_rider: ABUMI / O_YOROI tables (empty for every other unit);
  BOW_DRAW entry key 'prop' (default 'bow'); OFFHAND_NO_FIT set (empty by
  default): a unit's bow left out of the fit.
- mounted.asset_tack: tack option 'kura' (a colour key): the kura's arches
  instead of the pommel and cantle (off by default).
- mounted._girth_arc: option `reach` and tack option 'strap_reach'
  ((front, back) m): the breast strap and crupper look no farther than this
  from the saddle (a low head or the tail at strap height was taken for the
  chest or rump: the strap ran out to the nose). Off by default.
- mounted.HORSE_RIDERS gains the MDJ units; new entries in RIDE_POSE_BY_UNIT,
  WEAPON_UP_BY_UNIT, WEAPON_FWD_BY_UNIT, NECK_RAISE_BY_UNIT,
  HEAD_FLEX_BY_UNIT, HORSE_SHAPE_BY_UNIT, HORSE_COAT_OVERRIDES,
  TACK_OVERRIDES; render_vehicles.FIT_SCALE entries; remaster.REMASTER
  entries.

## Gate
scratch/numpymounted/verify/gate_all.sh: the 9 closed mounted finals
re-rendered at HEAD on this worktree and diffed to review/<unit>/final.png.
Baseline at 3ca8d271: all 9 reproduce (max 0.012).
At 6690f12b (batch 1): all 9 reproduce (max 0.012, Mounted Warrior, as at
the baseline). Log: scratch/numpymounted/verify/gate_*.log.

## Batch 1 critic sheet
review/_batch/batch_numpymounted_1.png and _red.png (GPU at 6690f12b): per
unit shipped (NumPy) and new; the stock Knight and the closed Keshik final.

## Round 2 of batch 1 and batch 2 (0d9e48fe)
New shared options (defaults unchanged):
- mounted._charge_decals `glow` (default 0.35) and tack option
  'charge_glow': dark charges without the white glow.
- mdj_riders: tack 'kura_rim' (the kura's rim key, default gold);
  o_yoroi 'lace_frac' (off by default; tried and dropped for Nakamaro);
  prop 'tachi'; pose 'yumi_ready'.
- New module league_riders.py: ENTRIES (the League entries, absent from this
  branch's catalog) and hat 'slouch_hat'; mounted.build reads ENTRIES for
  mounted.LEAGUE_RIDERS when the catalog has no entry.
- Per-unit entries for Teutonic Knight and Roughrider in the mounted tables,
  FIT_SCALE and REMASTER.
Size (the lead's rule, 2026-11-24): each remake's opaque area against its
stock counterpart (scratch/numpymounted/fitcheck_vs.py, stockfit.sh):
Shoni vs Keshik 1.00, Nakamaro vs Knight 0.99, Teutonic vs Knight 1.00,
Roughrider vs Cavalry 0.99.
Gate at 0d9e48fe: all 9 reproduce (max 0.008).
Sheets: review/_batch/batch_numpymounted_1r2 and batch_numpymounted_2 (+ _red).

## Batch 3 (1ed7a4c7 + the lance fix)
New shared options (defaults unchanged): props.lance 'bold'; mdj_riders tachi
'bright'; league_riders pose 'carbine_thigh'. Sheet:
review/_batch/batch_numpymounted_3 (+ _red), all four units, the stock Knight
and the closed Keshik final.

## Finals (batch 3 scores)
Shoni no Kagesuke R3 86.4 PASS, Teutonic Knight R2 86.0 PASS, Nakamaro
Horseman R3 81.4 (best, cap): finals in review/<unit>/final.png (+ _red;
shipped, final, Knight, Keshik) and ArtGen/units3d/reviews/realism/<unit>/
(scratch/numpymounted/finals.sh). The gate (verify/gate_all.sh) now also
re-renders these three (verify/verify_np.sh: the shipped dir per unit, art-base
for the Teutonic Knight). Roughrider round 3 (the last): solo sheet
review/roughrider/solo3/final.png (+ _red; Knight, Cavalry).
Roughrider R3 82.9: FINAL; in the gate. The group is closed (4 units).
Mounted Warrior gate re-run alone at b87134a1: max 0.004 (the 0.094 was
noise from 12 concurrent GPU renders).
