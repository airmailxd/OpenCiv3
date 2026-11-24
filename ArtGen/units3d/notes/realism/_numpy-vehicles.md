# numpy-vehicles: the League's modern-mod vehicles remade in units3d (group notes)

Branch realism/numpy-vehicles (from realism/armour f5519213), worktree
D:\Dev\C3-wt\realism-numpy-vehicles. Units: Bradley, Laser Tank, Iron Dome,
Convoy (catalog: art-base ArtGen\units\catalog\modern_mod_vehicles.py,
models ArtGen\units\models\modern_mod_vehicles.py). Identity only was read
from the NumPy models; the style is the armour family's.

## Code
- `armor_mod.py` (new): the four units' configs and builders. The Bradley is
  `armor._bradley` (the Mech Infantry's M2) with the A3 kit on top; the Laser
  Tank uses `armor.chassis` with its own builder; the Convoy and the Iron
  Dome are built in metres through a scaled Part (`Metric`, K = 1.86/6.55
  units per metre) with their own tyres.
- `calibration/<Unit>.json`: from the installed NumPy sheets in art-base
  (scratch/numpyvehicles/calib_numpy.py), numbers only.

## New shared options (all default to the old behaviour)
- `armor.tracked`: `cfg['chassis_fn']` replaces `armor.chassis` (a wheeled
  unit's own running gear); unset = the tracks, as before.
- `render_vehicles.py`: dispatches `armor_mod.UNITS` to `armor_mod.build`
  (new units only); `RV_TAG` env names a variant's output folder (unset = as
  before).
- `build_pc.py`: `--tag=<suffix>` (variant folders, never installed) and
  `--no-catalog` (skip catalog_json.py); both absent = as before.
- `bscene.use_gpu`: `REALISM_DEVICE=CPU` (with `REALISM_CPU_THREADS`) forces
  the CPU for run_locked's --cpu-ok lane; unset = as before.
  (These three are the same opt-ins the siege/mounted branches carry.)

## Finals gate
scratch/numpyvehicles/verify/verify_all.sh: re-renders the six closed armour
finals at HEAD and diffs them with review/<unit>/final.png.

## Log
- 8041131b: the four units; gate 6/6 reproduce exactly (max diff 0.008).
- Critic round 1 sheet: review/_batch/batch_numpyvehicles_1.png (+_red),
  rows shipped/new per unit, then the Modern Armor and Mech Infantry finals.
- Batch 1 scores: Bradley 85.0 PASS, Convoy 85.0 PASS (finals B1, in the
  gate since 7ca6b8f1); Laser Tank 70.5, Iron Dome 70.0.
- Size rule (lead): against the stock class, not the NumPy sprite
  (scratch/numpyvehicles/sizecmp.py). Stock armour opaque area vs the stock
  Modern Armor: Radar Artillery 0.85, Mobile SAM 0.97, Mech Infantry 1.06,
  Tank 1.08, Panzer 1.30.
- Round 2 (Laser Tank, Iron Dome): batch_numpyvehicles_2; gate 8/8 (six
  armour finals + Bradley + Convoy) reproduce.
- GROUP CLOSED. Finals: Bradley 85.0 PASS (B1), Convoy 85.0 PASS (B1, fit fix
  0.89), Laser Tank 86.0 PASS (B3), Iron Dome 83.5 (B3). The gate covers 10
  units: the six armour finals and these four.
