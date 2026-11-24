# Gunpowder and modern infantry: family notes (PC2, branch realism/infantry)

Units: Musketman, Musketeer, Rifleman, Infantry, Marine, Paratrooper, WWII Paratrooper, Guerilla, TOW Infantry.
Built from relay/realism-pc1/foot 9208ec25. Per-unit notes: notes/realism/<Unit>.md.

## Where the entries live

The eight foot units' entries are in remaster_soldiers.py. The realism pass does not edit the old entries:
`REALISM[unit]` (at the bottom of the file) is laid over each one (keys replaced), and `LEGACY` keeps a
deep copy of the entries as they were before it.

TOW Infantry is a team: `siege.tow_team` (the launcher, its tripod, a crate) with two crewmen from
`crews.CREW['TOW Infantry']`, both built from the `'TOW Infantry'` entry. It renders through build_pc.py
(render_vehicles.py fits the model to the Civ3 original's box, calibration/TOW Infantry.json).

## Merge hazard for the lead (siege crews)

crews.py builds some siege crewmen from a foot unit's live entry (`'_base'`): the Cannon's gunner is
Musketman, the Artillery and Flak crews are Infantry, and the TOW team is TOW Infantry. Once this branch merges,
the Cannon, Artillery and Flak finals will render with the new Musketman and Infantry kit (the Infantry's
civ-painted helmet, for example). To keep those finals unchanged, point those crews at
`remaster_soldiers.LEGACY[...]` instead of `remaster.REMASTER[...]`. That is a one-line change in
crews.populate, but crews.py is the siege branch's file, so I have not made it. The Trebuchet's crew
(`'_base'` Medieval Infantry) already carries PC1's melee change.

Edits on this branch to PC2 siege files, all inside the TOW team, which no closed final uses:
- siege.tow_team: one missile crate instead of two, set at the gunner's left; the second crewman (crew_1) sits
  on it (hips at 0.4 m) instead of standing 1.4 m away.
- (round 2) siege.tow_team: a shorter, darker launch tube with end caps raised 7 degrees, a bigger sight/tracker, the crate and assistant moved out to (-0.95, -0.32), the team turned 22.5 degrees.
- crews.CREW['TOW Infantry'][1]: the assistant gunner seated, spotting (a hand shading his eyes). A kneeling pose
  (`KNEEL`) did not take on a standing build, so he sits.

## New shared options (all off by default; the old behaviour is unchanged)

| File | Option | Default | What |
|------|--------|---------|------|
| props.py | `reserve_chute(p, scale=1.0, tint=False, handle=True)` | new builder `'reserve_chute'` | a reserve parachute on the belly (chest_gear): a squared pack, civ panel with `tint` |
| props.py | `_bayonet(..., w=1.0)` | 1.0 | widens the blade; when w != 1 the blade also runs to a blunt, round point (seg 6), so its tip doesn't break into specks |
| props.py | `rifle(..., barrel_w=1.0)` | 1.0 | barrel radius factor |
| props.py | `rifle_bolt(..., barrel_w=1.0, bayonet_w=1.0)` | 1.0 | passed to rifle() and _bayonet() |
| props.py | `musket(..., barrel_w=1.0, bayonet_w=1.0)` | 1.0 | barrel radius factor, bayonet width |
| props.py | `assault_rifle(..., barrel_w=1.0, whole=False)` | 1.0 / False | barrel radius; `whole`: the boxes (receiver, handguard, grip, sight) scale with `scale` too. Without it a scaled gun breaks into pieces at 1x (positions scale, sizes don't) |
| props.py | `ak(..., barrel_w=1.0, whole=False)` | 1.0 / False | as assault_rifle |
| props.py | `smg(..., whole=False)` | False | as assault_rifle |
| props.py | `modern_helmet(..., paint=False, band_w=0.035)` | False / 0.035 (the old band) | `paint`: the shell in the civ colour; `band_w`: the civ band's height |
| props.py | `para_pack(..., gap=0.17)` | 0.17 (as before) | how far behind the straps the bundle sits; the static-line bag and line move with it |
| props.py | `musket_rest(..., fork_w=1.0, shaft_w=1.0)` | 1.0 | thicker fork horns and pole (the thin horns were specks at 1x) |
| riders.py | `_modern_helmet(..., **more)` | (passes nothing) | passes `paint` and `band_w` from `headgear_params` to props.modern_helmet |
| riders.py | `cavalier(..., tip=0.005)` | 0.005 (as before) | the plume fronds' end radius (thin ends broke into specks) |

| props.py | `rifle(..., stock_w=1.0)`, `rifle_bolt(..., stock_w=1.0)`, `musket(..., stock_w=1.0)` | 1.0 | a thicker stock (and on the rifle a lock plate that stands out of it) |
| props.py | `musket_rest(..., fork_spread=1.0)` | 1.0 | a wider, taller Y |
| props.py | `reserve_chute(..., trim=False)` | False | an olive pack with one civ band round its middle |
| riders.py | `kepi(..., band=False, slope=8.0, peak=1.0, tall=1.0)` | the old kepi | `band` True: a civ band on a 'hat' crown; 'dark': a civ crown (if tinted) over a 'felt' band; slope, peak size, crown height |
| units.py | `bands` entries' optional 4th element: offset | 0.022 (as before) | a flat armband flush on the sleeve (0.013), not a roll |
| units.py | `boots_height` | 0.36 | the boots' top (jump boots 0.42-0.44) |
| units.py | `face_fill` (W), `face_fill_at` (m, 0.4) | off | a soft point light before the face, on the head bone, invisible to the camera: a face under a brim never renders as a black blob |
| render_vehicles.py | `PPM_SCALE` table | 1.0 for every unit not listed | the ppm after the box fit times this; only 'TOW Infantry': 0.89 (final) |

| props.py | `chute_pack(..., straps=False)` | False | webbing straps over the container and raised side flaps |
| props.py | `reserve_chute(..., buckle=False)` | False | the harness's quick-release box and straps on the chest above the reserve |
| garments.py | `BANDS['thigh']` | (a new location) | a band high on the thigh: a parachute harness's leg straps |

No change to human.py or units.py's stances; garments.py only gains the 'thigh' band location; no existing default changed.

## Finals gate

scratch/infantry/verify/verify_all.sh (D:/Dev/_handoff/realism): re-renders Legionary, Archer, Samurai, Swordsman,
Medieval Infantry and the infantry finals (Musketman, Musketeer, Rifleman, Infantry, Marine, Paratrooper, WWII
Paratrooper, Guerilla, TOW Infantry) at HEAD and diffs each new row against
reviews/realism/<unit>/final(_red).png.
(The Samurai's shipped row differs from its final sheet because the installed Samurai art changed after that
sheet was made. Its new row is identical, so the gate judges the new row.)

## Finals (batch 3, 2026-11-24)

| Unit | Final | Score |
|---|---|---|
| Infantry | R3 | 85.0 PASS |
| Musketeer | R3 | 84.5 |
| Musketman | R3 | 82.5 |
| Guerilla | R3 | 82.5 |
| Paratrooper | R3 | 82.0 |
| Marine | R1 (restored) | 82.0 |
| Rifleman | R2 (restored) | 80.0 |
| WWII Paratrooper | R3 | 80.0 |
| TOW Infantry | R4 (targeted on the wrong-direction critical) | 82.75 |

Restores are scoped to each unit's own REALISM entry, and each reproduces its round's sheet row (Rifleman vs
batch_infantry_a2, Marine vs batch_infantry_a1: mean under 1e-7, max 2/255; scratch/infantry/rowfind.py).
Later pass, for all: fix the support-hand rig (Marine and both Paratroopers) so the hand closes on the handguard.
