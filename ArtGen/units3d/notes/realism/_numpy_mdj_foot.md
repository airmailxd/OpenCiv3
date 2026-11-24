# numpy-mdj-foot: five MDJ foot units remade in units3d (PC2)

Branch `realism/numpy-mdj-foot` (from realism-pc1/foot-mdj 16f5eabf). Units: Japanese Rifleman, Oda Arquebusier,
Stone Crossbow, Medieval Japanese Settler, Medieval Japanese Worker. The NumPy sprites and catalog in art-base were
read for identity only.

## Ownership and shared code

`mdj_kit.py` and the shared foot code (`human.py`, `units.py`, `props.py`, `garments.py`, `yoroi.py`) belong to the
other PC's MDJ builder and are untouched here. This branch changes only:

| File | What |
|------|------|
| `remaster_mdj.py` | the five batch-2 entries above (they are this group's own; no other entry touched) |
| `nmf_kit.py` (new) | props `kuwa_broad` (a broad hira-guwa hoe, the Worker's; named apart from the other PC's coming `kuwa`), `nmf_ija_pack` (the IJA pack with the civ blanket roll in a horseshoe, the Rifleman's), `nmf_xbow` (the hand crossbow made to read at 1x, the Stone Crossbow's), `nmf_arisaka` (Type 38 with a broad Type 30 bayonet, the Rifleman's); the hat `nmf_kasa` (sedge kasa with a civ crown, the Worker's); colour keys `nmf_tiller`, `nmf_prod`, `nmf_straw` (setdefault) |

Entry keys used that already exist in shared code (no new ones): `unit_scale` (render_unit.py), `brow_drop`
(units.headgear), `two_hands` / `grip2` ('ready').

The props register with `setdefault` (an existing name is never replaced). An entry loads them with
`'kits': ['mdj_kit', 'nmf_kit']`.

New shared options (a2, in `units.py`, both off by default, so no existing unit changes; the gate re-run after):
- entry key `wraps_plain` (default False): `wraps='wraps'` drawn as single-tone cloth gaiters (kyahan), civ when
  'wraps' is tinted, instead of the banded fur wraps;
- gear item `('v_collar', {color | tint, width, depth, spread})`: a kimono's crossed collar, two bands down the
  front of the chest from either side of the neck, meeting in a V (default: not drawn unless listed).

nmf_kit.py (a2): props `nmf_matchlock`, `nmf_furoshiki`, `nmf_pouch`, `nmf_canteen`, `nmf_bolt_quiver`; the hat
`nmf_type90`; colour keys `nmf_straw_dk`, `nmf_stock`, `nmf_furoshiki`, `nmf_khaki`; `nmf_kasa` gained `weave`,
`kuwa_broad` `thick`, `nmf_ija_pack` `roll` (all this group's own).

## Finals gate

`D:\Dev\_handoff\realism\scratch\numpymdjfoot\verify\verify_all.sh`: re-renders Legionary, Archer, Swordsman,
Medieval Infantry and Samurai at HEAD in this worktree and diffs each to its `reviews/realism/<unit>/final(_red).png`
with each sheet's own row labels: `shipped` / `new` for the four, `shipped` / `final` for the Samurai. The
Samurai's 78 px (max 0.757) seen on PC2 before was the label ("new" vs "final"), not cloth noise: with `final` it
reproduces (max 0.008, 0 px). Pass: max < ~0.03.

- Baseline at 16f5eabf: 5/5 (max 0.024 Archer, 0.008 others).
- At a5e4520e (after nmf_kit.py and the five entries): 5/5 (Archer max 0.024; Legionary, Swordsman 0.008; Medieval
  Infantry, Samurai 0.004; 0 px over 0.1). The later commits touch only nmf_kit.py props the gate units don't load.

- At 4285a771 (after the two units.py options): 5/5 (Archer max 0.024; Legionary, Swordsman 0.008; Medieval
  Infantry, Samurai 0.004; 0 px over 0.1).

## Size

The lead's rule: sized against the stock class in D:\Dev\C3\C7\ModernArt\Art\Units, opaque area 0.90-1.10:
Japanese Rifleman vs Rifleman, Oda Arquebusier vs Musketman, Stone Crossbow vs Archer (the ranged foot counterpart),
Settler vs Settler, Worker vs Worker. Each entry's `unit_scale` (1.08-1.18) brings it into the band.
Tools: `scratch\numpymdjfoot\tools\fitstock.py` (stock) and `fitnumpy.py` (the NumPy sprite); `check.sh` runs both,
tint.py and specks.py for all five.

## Batches

- `batch_numpymdjfoot_a1` (128 samples, GPU, HEAD 250b3c62): the five; Legionary (final) and Archer (final) at the
  bottom; `_red`, `_tight`, `_tight_red` beside it.

## a1 numbers

| Unit | Tint new / NumPy | Area vs stock | Area vs NumPy | Specks |
|---|---|---|---|---|
| Japanese Rifleman | 14.2 / 11.4 % | 0.99 Rifleman (0.81 Musketman) | 1.23 | clean |
| Oda Arquebusier | 17.1 / 35.3 % | 0.93 Musketman | 1.39 | clean |
| Stone Crossbow | 11.7 / 36.6 % | 1.03 Archer (0.88 Musketman) | 1.11 | clean |
| Medieval Japanese Settler | 12.0 / 18.1 % | 0.98 Settler | 1.42 | clean |
| Medieval Japanese Worker | 12.7 / 23.5 % | 0.96 Worker | 1.00 | clean |

## a1 critic scores (A / B = average)

Japanese Rifleman 85.3 / 70.5 = 77.9; Stone Crossbow 85.0 / 66 = 75.5; Worker 72 / 56.5 = 64.25 (critical: the blade
through the brim); Settler 70.8 / 56.5 = 63.65 (critical: the floating staff); Oda Arquebusier 70.5 / 56.5 = 63.5
(critical: the gun read as a staff).

## a2 numbers (`batch_numpymdjfoot_a2`, 128 samples, GPU, HEAD 4285a771)

| Unit | Tint new / NumPy | Area vs stock | Area vs NumPy | Specks |
|---|---|---|---|---|
| Japanese Rifleman | 10.7 / 11.4 % | 1.03 Rifleman | 1.27 | clean |
| Oda Arquebusier | 11.4 / 35.3 % | 1.06 Musketman | 1.58 | clean |
| Stone Crossbow | 14.1 / 36.6 % | 0.97 Musketman (1.14 Archer: the jingasa and bolt case) | 1.24 | clean |
| Medieval Japanese Settler | 11.0 / 18.1 % | 0.93 Settler | 1.34 | clean |
| Medieval Japanese Worker | 11.7 / 23.5 % | 1.04 Worker | 1.08 | clean |

## a2 critic scores (A / B = average)

Oda Arquebusier 84.75 / 70.5 = 77.6 (gun critical closed); Stone Crossbow 83.3 / 71.3 = 77.3 (A: new critical, the
crossbow lost in dir 6); Settler 83.5 / 68 = 75.75 (staff critical closed); Japanese Rifleman 77.5 / 71.5 = 74.5
(regressed; A: critical, the rifle end-on in dir 1); Worker 78.3 / 67 = 72.65 (clip critical closed; A: new critical,
a different grip per direction).

## a3 numbers (`batch_numpymdjfoot_a3`, the last round; 128 samples, GPU, HEAD c5fc8be4)

No shared code changed after 4285a771 (only nmf_kit.py and the five entries), so the gate there (5/5) stands.

| Unit | Tint new / NumPy | Area vs stock | Area vs NumPy | Specks |
|---|---|---|---|---|
| Japanese Rifleman | 8.9 / 11.4 % | 1.03 Rifleman | 1.28 | clean |
| Oda Arquebusier | 10.8 / 35.3 % | 1.07 Musketman | 1.60 | clean |
| Stone Crossbow | 13.4 / 36.6 % | 1.03 Archer (0.89 Musketman) | 1.13 | clean |
| Medieval Japanese Settler | 10.2 / 18.1 % | 1.00 Settler | 1.44 | clean |
| Medieval Japanese Worker | 9.0 / 23.5 % | 1.01 Worker | 1.05 | clean |

## CLOSED (a3 final for all five; no critical open)

| Unit | Final | A / B = average | Rounds a1 / a2 / a3 |
|---|---|---|---|
| Oda Arquebusier | a3 | 86.5 / 75.5 = 81.0 | 63.5 / 77.6 / 81.0 |
| Japanese Rifleman | a3 | 85.8 / 75 = 80.4 | 77.9 / 74.5 / 80.4 |
| Medieval Japanese Worker | a3 | 83.3 / 70.3 = 76.8 | 64.25 / 72.65 / 76.8 |
| Medieval Japanese Settler | a3 | 83.5 / 69.3 = 76.4 | 63.65 / 75.75 / 76.4 |
| Stone Crossbow | a3 | 82.5 / 66.3 = 74.4 | 75.5 / 77.3 / 74.4 |

The Stone Crossbow's a1 (75.5) is 1.1 over a3, under the 1.5 gain threshold, and a3 closes a2's dir-6 critical:
final a3, no restore. The later-pass lists are in each unit's notes (FINAL section).

Finals: reviews/realism/<unit>/final(_red).png and D:\Dev\_handoff\realism\review\<unit>\ (shipped NumPy, final, two
stock neighbours; row label `final`). Nothing installed into C7/ModernArt.

Final gate (scratch\numpymdjfoot\verify): `verify_all.sh` (Legionary, Archer, Swordsman, Medieval Infantry, Samurai)
5/5 and `verify_nmf_all.sh` (the five finals re-rendered at HEAD, diffed to their final sheets) 5/5: max 0.024
(Archer), all others 0.008 or less, 0 px over 0.1.

# Batch 1 (PC2, branch realism/numpy-mdj-foot-b1)

Ashigaru Spearman, Otomo Spearman, Minamoto Samurai, Warrior Monk, Otomo no Makuta, Hojo General: the other PC's
build round 1 (48 samples, no critic) with its round-2 entries merged from relay realism-pc1/foot-mdj 091061cc
(merge 6a540b5e). Scripts: `D:\Dev\_handoff\realism\scratch\numpymdjfoot_b1\` (rend.sh, check.sh, batch.sh and
verify\ pointed at this worktree).

## Shared code (b1)

Nothing in mdj_kit.py, yoroi.py, units.py or the other shared foot code changed. New in `nmf_kit.py` only (new
names, no existing default touched):
- hat `nmf_zukin` (brow, mouth): a katsuo-zukin that leaves the eyes and nose open (the Warrior Monk's; mdj_kit's
  `zukin` covers the whole face);
- prop `nmf_katana` (length, width, curve, grip, spine): yoroi's `katana_drawn` with a thicker back and a blunter
  tip, so a blade seen edge-on stays a line (the Minamoto Samurai's and the Hojo General's).
- (b2) prop `nmf_naginata` (the Monk's longer, broader, curved blade), prop `nmf_ebira` (mdj_kit's ebira set out
  behind the right hip as a back prop; Makuta's), hat `nmf_hachimaki` (a flat band tied at the back; Otomo's).
- (b3) props `nmf_yumi` (Makuta's dark yumi with a pale grip), `nmf_saihai` (Hojo's baton with a full paper
  tassel), `nmf_agemaki` (Minamoto's civ bow knot on the back).
- (b2) SHARED, yoroi.py `kabuto`: param `lace_tint` (default None = the old behaviour, the shikoro follows the
  entry's 'lace' tint); False keeps the shikoro in the 'lace' colour while sode and kusazuri stay civ (Minamoto).
Entry keys used that already exist: unit_scale, brow_drop, wraps_plain, kits.

## Size (128 samples; 48 samples had overstated batch 1 too)

| Unit | unit_scale | Area vs stock | vs NumPy |
|---|---|---|---|
| Ashigaru Spearman | 0.95 | 0.93 Spearman (1.13 Pikeman) | 0.86 |
| Otomo Spearman | 1.1 | 0.94 Spearman (1.15 Pikeman) | 1.32 |
| Minamoto Samurai | 0.97 | 1.03 Samurai | 1.07 |
| Warrior Monk | 0.95 | 0.99 Pikeman (0.80 Swordsman) | 1.02 |
| Otomo no Makuta | 1.0 | 1.09 Samurai (0.88 Swordsman) | 0.99 |
| Hojo General | 0.9 | 1.05 Swordsman (1.29 Samurai, banner in) | 1.29 |

## Gate (b1, after nmf_zukin and nmf_katana)

verify_all.sh (foot finals) 5/5: Archer max 0.024; Legionary, Samurai, Swordsman 0.008; Medieval Infantry 0.004;
0 px over 0.1. verify_nmf_all.sh (the batch-2 finals) 5/5: max 0.004-0.008, 0 px over 0.1.

## b1 critic sheets

`review\_batch\batch_numpymdjfoot_b1a` (Ashigaru, Otomo Spearman, Warrior Monk) and `_b1b` (Minamoto, Makuta,
Hojo), each with `_red`, `_tight`, `_tight_red`; stock Samurai and Pikeman at the bottom (two sheets: at most 5 units
per sheet).

## b1 scores (A / B = average)

Ashigaru 84.25 / 74.5 = 79.4; Minamoto 78.75 / 76.5 = 77.6; Hojo 84.0 / 67.8 = 75.9; Otomo Spearman 85.75 / 65.3 =
75.5; Makuta 78.0 / 71.0 = 74.5; Monk 77.5 / 69.3 = 73.4.

## b2

Sheets `review\_batch\batch_numpymdjfoot_b2a` (Ashigaru, Otomo Spearman, Monk) and `_b2b` (Minamoto, Makuta, Hojo),
with `_red`, `_tight`, `_tight_red`. Changes per unit in each unit's notes.

Gate at b2 (after yoroi's lace_tint): verify_all 5/5 (Archer max 0.024; Legionary, Swordsman 0.008; Samurai,
Medieval Infantry 0.004; 0 px over 0.1); verify_nmf_all 5/5 (max 0.004-0.008, 0 px over 0.1).

## b2 scores (A / B = average)

Ashigaru 85.25 / 75.5 = 80.4; Minamoto 81.75 / 79 = 80.4 (dir-4 critical open); Monk 83.5 / 75.5 = 79.5; Otomo
Spearman 86.5 / 71.8 = 79.15; Makuta 83.25 / 74 = 78.6; Hojo 81.5 / 72.5 = 77.0.

## b3 (the last round)

Sheets `review\_batch\batch_numpymdjfoot_b3a` / `_b3b` (+ `_red`, `_tight`, `_tight_red`). Only nmf_kit.py (new names)
and the six entries changed: verify_nmf_all (the batch-2 finals, which load nmf_kit) re-run.
Gate at b3: verify_nmf_all 5/5 (max 0.004-0.008, 0 px over 0.1). No shared code changed after b2's 10/10.

## Batch 1 CLOSED (b3 final for all six; no critical open)

| Unit | Final | A / B = average | Rounds b1 / b2 / b3 |
|---|---|---|---|
| Minamoto Samurai | b3 | 85.25 / 79.8 = 82.5 | 77.6 / 80.4 / 82.5 |
| Hojo General | b3 | 84.5 / 77.3 = 80.9 | 75.9 / 77.0 / 80.9 |
| Ashigaru Spearman | b3 | 85.25 / 76 = 80.6 | 79.4 / 80.4 / 80.6 |
| Otomo no Makuta | b3 | 83.25 / 77 = 80.1 | 74.5 / 78.6 / 80.1 |
| Warrior Monk | b3 | 84.5 / 75.5 = 80.0 | 73.4 / 79.5 / 80.0 |
| Otomo Spearman | b3 | 85.0 / 73 = 79.0 | 75.5 / 79.15 / 79.0 |

The Otomo Spearman's b2 (79.15) is within the 1.5 noise band of b3: b3 final, no restore. The Minamoto's dir-4
critical closed at b3. The later-pass lists are in each unit's notes (FINAL section). Group, from critic B: the batch
plateaued; the next pass wants stronger material contrast between lacquer, cloth and metal.

Finals: reviews/realism/<unit>/final(_red).png and D:\Dev\_handoff\realism\review\<unit>\ (shipped NumPy, final,
two stock neighbours; row label `final`). Nothing installed into C7/ModernArt.
The nmf gate (`scratch\numpymdjfoot_b1\verify\verify_nmf_all.sh`) now covers all eleven MDJ finals (batch 1 and 2);
final_layout.sh has the six's neighbours.

Close gate at HEAD: verify_all (foot finals) 5/5 (Archer max 0.024; Legionary, Swordsman 0.008; Samurai, Medieval
Infantry 0.004); verify_nmf_all (all eleven MDJ finals) 11/11 (max 0.004-0.008); 0 px over 0.1 anywhere.
