# numpy-foot: the League "modern mod" foot units remade in units3d (PC2)

Branch `realism/numpy-foot` (from realism-pc1/foot 9208ec25). Eleven units the NumPy generator made
(art-base `ArtGen/units/catalog/modern_mod_foot.py`, `league_foot.py`): Battalion (line_mg_team), Combat
Engineer, Insurgent, Militant, Militia, Partisans, RPG Trooper, Rebel, Skirmisher, Special Forces, Super Trooper.
The NumPy entries were read for identity only (what the unit is, its kit, its civ placement), not for style.

## Shared code: nothing changed

The shared foot code (`human.py`, `units.py` stances, `props.py`, `garments.py`, owned by PC1's melee builder)
is untouched. Everything for this group is in new files:

| File | What |
|------|------|
| `npf_units.py` | the units' full entries (ENTRIES, the builder's own vocabulary), the new props and hats, the team builder |
| `npf_render.py` | renders one unit's still from ENTRIES (as render_unit.py; these units have no old-catalog entry here) |
| `npf_build.py` | renders and composes (as build.py), `--tag=` for variants; nothing is installed |
| `calibration/<Unit>.json` | anchors, flic headers and ppm_2x from the installed NumPy sheets in art-base (numbers only) |

New names, all prefixed `npf_` so they cannot clash with the realism/infantry branch:

- props registered into `props.BUILDERS` at import (setdefault: an existing name is never replaced):
  `npf_rpg7`, `npf_rocket_carrier`, `npf_pkm`, `npf_carbine`, `npf_ak_bayonet`, `npf_shotgun`, `npf_molotov`,
  `npf_ppsh`, `npf_ak`, `npf_plate_rig`, `npf_flame_gun`, `npf_flame_tanks`, `npf_power_pack`, `npf_pulse_rifle`, `npf_m1917`,
  `npf_charleville`, `npf_m14`, `npf_satchel`, `npf_bottle_bag`;
- hats registered into `riders.HEADGEAR`: `npf_helmet_goggles`, `npf_fast`, `npf_shemagh`, `npf_flat_cap`,
  `npf_shako1806`, `npf_m1_leaf`, `npf_exo`, `npf_headwrap`;
- colour keys added to `props.DEFAULTS` (setdefault): `npf_tube`, `npf_warhead`, `npf_wood`, `npf_brass`,
  `npf_glass`, `npf_rag`, `npf_bottle`, `npf_ranger`, `npf_coyote`, `npf_shemagh`, `npf_shemagh_red`, `npf_tank`,
  `npf_glow`, `npf_fire`, `npf_armor`, `npf_suit`, `npf_supp`, `npf_helmet_poly`;
- entry keys read only by npf_render.py: `fit_scale` (multiplies the calibration's ppm_2x; default 1.0) and
  `team` / `ground_props` at the top level (npf_units.build; a lone soldier when absent).

No new options were added to shared code, so no default changed.

## Finals gate

`D:\Dev\_handoff\realism\scratch\numpyfoot\verify\verify_all.sh`: re-renders Legionary, Archer,
Swordsman and Medieval Infantry at HEAD and diffs each to its `reviews/realism/<unit>/final(_red).png`.
Baseline at 9208ec25 (before any work here): Legionary, Archer, Swordsman, Medieval Infantry reproduce (max
0.024, 0 px over 0.1); the Samurai differs by 78 px already at the branch start (its cloth). The lead's rule
(2026-11-24): the Samurai's difference is cross-machine cloth-sim noise (PC1 reproduces it within 2/255); this gate
leaves it out. Re-run at 9f0849c9: the four reproduce (max 0.024, 0 px over 0.1). No shared file has changed since.

## Batches

- `batch_numpyfoot_a1`: RPG Trooper, Special Forces, Insurgent, Militant
- `batch_numpyfoot_b1`: Partisans, Rebel, Skirmisher, Militia
- `batch_numpyfoot_c1`: Combat Engineer, Super Trooper, Battalion

Stock rows at the bottom of each sheet: the closed foot finals Legionary and Archer (rendered at HEAD).

## Civ colour target

Shipped NumPy sprites (tint.py, mean %): Battalion 6.4, Combat Engineer 10.8, Insurgent 13.2, Militant 10.9,
Militia 30.2, Partisans 2.5, RPG Trooper 14.3, Rebel 12.8, Skirmisher 14.3, Special Forces 14.1, Super Trooper
13.0. Stock foot 9-34 %. Aim 10-20 %, even across directions (Partisans brought up from 2.5).

## Size

The lead's rule (2026-11-24): sized against the stock counterpart (D:\Dev\C3\C7\ModernArt\Art\Units), opaque
area 0.90-1.10: Marine for RPG Trooper, Special Forces, Insurgent, Militant, Combat Engineer, Super Trooper; Rifleman
for Partisans, Rebel, Militia; Infantry for the Skirmisher. The Battalion is a two-man team and the installed TOW
Infantry is now a single figure: each of its men is drawn at about 92 % of the stock Infantry's scale.
Tool: scratch\numpyfoot\tools\fitstock.py <unit_dir> <StockUnit>.

## Critic rounds

- a1 (RPG Trooper 62.9, Special Forces 74.5, Insurgent 58.55, Militant 69.75). Shared lessons applied to all eleven
  (`_stock_build` in npf_units.py: the stock foot build, upper_bulk 1.32, baggier trousers; thick weapons with an
  identifying part; faces lifted; civ colour in 2-3 accents).
- a2 (RPG 74.75, SF 73.9, Insurgent 72.75, Militant 68.5); c1 (Combat Engineer 66.75, Super Trooper 66.25,
  Battalion 48.4); b1 (Partisans 66.9, Rebel 66.4, Skirmisher 65.5, Militia 76.25). Rule for the branch: no civ
  upper-arm blocks; armbands flat and 4-6 cm (npf_units.build lays every band flush, FLAT_BAND 0.012, by wrapping
  garments.band while an npf unit is built; garments.py is untouched); upper_bulk 1.24.

## Closed (all eleven final)

| Unit | Final | Score | Note |
|---|---|---|---|
| RPG Trooper | A3 | 76.65 | |
| Special Forces | A3 | 78.25 | |
| Insurgent | A3 | 73.25 | |
| Militant | R4 | 72.25 | STUCK: AK identity in dirs 1, 4, 5, 6 |
| Partisans | B3 | 71.0 | |
| Rebel | B2 | 70.65 | scoped restore |
| Militia | B2 | 80.75 | scoped restore |
| Skirmisher | R4 | 70.75 | STUCK: rifle in dirs 1, 4, 5, 8 |
| Combat Engineer | C4 | 74.9 | |
| Super Trooper | C4 | 69.15 | STUCK: house style (legs, hands) |
| Battalion | C4 | 70.65 | STUCK: the men merge in dirs 4, 7, 8 |

Finals: reviews/realism/<unit>/final(_red).png and D:\Dev\_handoffealismeview\<unit>\. The npf finals gate,
scratch
umpyfooterifyerify_npf_all.sh, re-renders all eleven with npf_build and diffs them to their finals.
Entry keys added in this branch, read only by npf_units / npf_render: fit_scale, team, ground_props, bulk_final,
npf_armour; hat options band_up (helmet with goggles), band_at (flat cap), band_tint/tail_tint (shemagh), loose/band
(headwrap), patch (FAST), stripe (exo helmet, power pack), flap (satchel), bands (flame tanks), patch (plate rig).
