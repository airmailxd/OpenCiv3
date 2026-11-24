# Group notes: realism/misc (PC2; Crusader, Holy Relic, Settler Modern Times, Worker Modern Times)

Branch realism/misc from realism-pc1/foot 9208ec25. The Crusader is on foot (remaster_soldiers.py, the foot
builder), not a horse_rider; the Settler and Worker are foot entries in remaster_soldiers.py; the Holy Relic is
siege.holy_relic with its bearers in crews.CREW['Holy Relic'].

## New shared entries and options (every default keeps the old behaviour)
- props.py: new prop `settler_pack(p, size=1.0)`, registered with `BUILDERS['settler_pack'] = settler_pack`
  in a realism/misc block at the end of the file.
- siege.py: `holy_relic(..., pole_z=None)` and the module constant `RELIC_POLE_Z` (None = the old z0 + 0.3;
  set to 0.9 here). Only the Relic reads it.
- crews.py: a new `LEVITE` dict after BEARER; CREW['Holy Relic'] (the Relic's own entry) now uses it.
  BEARER itself is unchanged.
- remaster_soldiers.py: only the Crusader, Settler Modern Times and Worker Modern Times entries changed.

## Finals gate
scratch/misc/verify/verify_all.sh (Legionary, Archer, Swordsman, Medieval Infantry; Samurai left out).
Baseline at 9208ec25: 4/4 (max 0.024). After the props.py commit e5a7eb4d: 4/4 (max 0.024).

## New shared options in batch a2 (defaults keep the old behaviour)
- units.py: entry options mail_metallic (0.85) and mail_roughness (0.55) for the mail material, in its two places.
- riders.py: a new hat, slouch_hat, in a realism/misc block at the end.
- props.py: new props suitcase and work_shovel, a new DEFAULTS key alu; settler_pack (this branch's own) has an aluminium frame and a hip belt.
- siege.py: holy_relic_carried, _cherub, RELIC_CARRIED, RELIC_POLE_Z, RELIC_BEARER_X/Y in a block before BUILDERS; holy_relic gains a two-line dispatch at its top (the pole_z option from a1 is removed again).
- crews.py: LEVITE_R and LEVITE_L; CREW['Holy Relic'] has four bearers.

## Batches
- a1: review/_batch/batch_misc_a1(_red, _tight, _tight_red).png, the shipped and new rows per unit, with the
  Legionary and Archer finals at the bottom.
- a2: review/_batch/batch_misc_a2(_red, _tight, _tight_red).png, same layout. Gate after the units.py
  commit 4d1e1f57: 4/4 (max 0.024).

## Merge with realism/all (567be8b8): a77327ef
- --no-ff merge. One conflict: riders.py end of file (misc's slouch_hat block and the ranged family's hats
  both appended); kept both. dupes.py and defaults.py: no new collisions or conflicting defaults.
- Merged gate (scratch/all/sgate.py, sprite level, GPU, detached worktree realism-misc-g1 at a77327ef):
  Legionary, Archer, Swordsman, Medieval Infantry, Samurai, Settler, Worker, Knight, Trebuchet: 9/9 PASS
  blue and red (worst 6/255, the Archer). scratch/misc/verify/gate_merge_a77327ef.txt.
- Effects on misc units: Crusader, Settler and Worker Modern Times render as at a2. The Holy Relic's
  Levites build on the redesigned Settler base (LEVITE overrides its hood, vest and pose) and, as crews,
  now default to posture False.

## New shared entries in batch a3 (defaults keep the old behaviour)
- props.py: new prop holdall; new DEFAULTS keys shovel_steel, earth_edge, holdall (work_shovel is this
  branch's own).
- crews.py: LEVITE changed (the Relic's own crew).
- siege.py: _cherub's wing path (the Relic's own).

## Batches
- a3: review/_batch/batch_misc_a3(_red, _tight, _tight_red).png, the last round.

## Closed (a3, 853d96b7)
| Unit | a1 | a2 | a3 | Final |
|---|---|---|---|---|
| Holy Relic | 74.75 | 84.75 | 86.25 | a3, PASS |
| Crusader | 74.4 | 83.9 | 86.0 | a3, PASS |
| Settler Modern Times | 76.5 | 81.5 | 85.25 | a3, PASS |
| Worker Modern Times | 72.0 | 81.9 | 83.5 | a3 (no critical) |
The four finals are in the finals gate (scratch/misc/verify/merged.list).
