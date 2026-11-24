# NumPy remake, group numpy-air (PC2): Fighter Bomber, Wildcat

Branch realism/numpy-air (from realism/air e3e71585). The two League
aircraft whose installed art was made by the old NumPy generator are
rebuilt in units3d (aircraft.py), to the closed air family's realism look
(the Fighter's notes: real paint, white-ringed civ roundels, dark panel
lines, deliberate civ regions only).

Identity from the art-base reference (realism-pc1/art-base f17bb128):
- Fighter Bomber: catalog modern_mod_sea_air.py, model p38 ("A P-38
  Lightning fighter-bomber in olive drab over neutral grey"), tint 'band'.
- Wildcat: catalog league_air.py, model wildcat ("A stubby blue-grey
  carrier fighter: a big round cowling with a civ-coloured ring,
  civ-coloured roundels and a band round the tail").

## Calibration
League units have no Civ3 FLC, so calibration/calibrate_sheet.py (new)
writes calibration/<Unit>.json from the installed NumPy sheet in
calibrate.py's format: per-direction opaque boxes of DEFAULT frame 0
(alpha > 127, which leaves out the sheet's alpha-102 shadow, as the FLC
shadow indices are left out), the median foot point as the anchor, and the
flic headers from the metadata (the flic names stay the sheet's own:
x_P38*, x_Wildcat*). render_vehicles.fit then fits the model to those boxes
exactly as for the stock air units.

## Shared code (all new entries; no existing default changed)
- aircraft.py: new builders p38(), wildcat(), gp_bomb(); new AIRCRAFT
  entries 'Fighter Bomber' and 'Wildcat'; new material defaults
  'bomb_olive' and 'turbo' (props.DEFAULTS.setdefault: only added when
  absent).
- New AIR_OPTS knobs read only by the new builders: boom_x, dihedral,
  band_y, band_w, fin_tint, hub, pylon_x, bomb_l, bomb_r, bombs, alt,
  cowl_key, cowl_band, side_y. Keys the old units already read (paint,
  line, camo_hex, camo_scale, roundel_*, side_r, lines, wing_seg, gun_r,
  blade_w, spinner_key) keep their old defaults for the old units.
- Round 2, more new knobs for the new builders: nose_dark, nacelle_band,
  top_stripe, belly, belly_z, frames, frame_r, spine, scoop_key, turbo_r,
  pitch, blade_t, fin_top, fin_edge, bomb_z, bomb_y, under, panel,
  fit_scale, side_z, more_lines, te_paint, cowl_r, lip_r (cowl_band is
  gone).
- Round 2, new options on shared helpers (old defaults keep the old
  geometry exactly): prop(pitch=18.0, thick=0.05, tip_in=0.0) (the tip's
  thickness stays 0.84 x thick = 0.042); canopy(spine=False); an AIRCRAFT
  entry's optional 'fit_scale' sets root['fit_scale'] (only when present).
- Round 3: canopy(frame_key='glass_frame') (new option, old default);
  gp_bomb(band=False); new material defaults 'bomb_band' and
  'frame_light'; new knobs boom_tone, rudder_band, rudder_z, bomb_band,
  belly_drop, belly_y, frame_key. Changed defaults of the new builders'
  own knobs only: nacelle_band and top_stripe now False, fin_tint None,
  scoop_key 'air_panel', the P-38's panel colour #3c3f27, bomb_olive
  #2a2c20 satin.
- calibration/calibrate_sheet.py (new script) and calibration/Fighter
  Bomber.json, Wildcat.json (new).

## Finals gate
scratch/numpyair/verify/verify_all.sh (a copy of the air gate pointed at
this worktree): the six closed air finals re-rendered at HEAD and diffed
to review/<unit>/final.png.
Gate after b17eaf08 (the first shared-code commit): all six closed finals
reproduce, blue and red: Fighter and Jet Fighter mean 0 max 0; Bomber,
F-15, Stealth Fighter, Stealth Bomber mean <= 2.2e-8, max <= 0.008, no
pixel over 0.1.

## Rounds
- Batch 1: review/_batch/batch_numpyair_1.png and _red.png (GPU, HEAD
  b17eaf08).
- Batch 1 critics: Fighter Bomber 76.9 (77 / 76.75), Wildcat 76.1
  (73 / 79.25), no critical failure.
- Batch 2: review/_batch/batch_numpyair_2.png and _red.png (GPU, HEAD
  007ad3d7). Gate after 007ad3d7: all six closed finals reproduce, blue
  and red: mean <= 1.7e-8, max <= 0.008, no pixel over 0.1 (Jet Fighter
  mean 0 max 0).
- Batch 2 critics: Fighter Bomber 82.0 (81 / 83), Wildcat 82.75
  (83 / 82.5), no critical failure.
- Batch 3 (the last under the cap): review/_batch/batch_numpyair_3.png and
  _red.png (GPU, HEAD dbfdf9cc). Size measured against the stock Fighter
  and Bomber (scratch/numpyair/fitref.py, a fitcheck against any reference
  dir). Gate after 8fa1f126 and again after dbfdf9cc: all six closed
  finals reproduce, blue and red: mean <= 2.8e-8, max <= 0.008, no pixel
  over 0.1.
- Batch 3 critics: Fighter Bomber 85.4 (85.5 / 85.25) PASS, Wildcat 86.1
  (86.0 / 86.25) PASS. GROUP CLOSED; both finals = batch 3.

## Closed
Finals in reviews/realism/fighter-bomber/ and wildcat/ (final.png,
final_red.png: shipped NumPy, final, stock Fighter, stock Bomber). The
gate (scratch/numpyair/verify/verify_all.sh) now holds 8 units: the six
closed air finals plus Fighter Bomber and Wildcat (their shipped row is
the art-base NumPy sprite).
