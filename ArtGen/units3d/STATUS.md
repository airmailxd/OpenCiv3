# Unit remaster: status (2026-10-15, evening)

Branch `units-remaster` on airmailxd/OpenCiv3 (Mac and the Windows PC both push here; nothing on
Development, nothing tagged). Stills only (one pose, 8 directions; every action shows it).

## Review state (independent reviewer agents, PASS = every axis >= 7)
Owner's rule (2026-10-15): **equal or more detail than the originals** -- an upgrade, not a
simplification. Reviewers score a 6th axis, detail vs original (every strap, trim, band, emblem,
pattern and item of the original present, plus believable extra detail); MISSING features that the
original shows are blockers, not polish. Earlier PASSes need a re-check on this axis.

| Unit | State | Open notes |
|------|-------|-----------|
| Warrior, Spearman, Archer, Swordsman, Settler, Worker | **all PASS** on all 6 axes (detail included) | polish only |
| Footmen batch A (11, Mac) | 10/11 PASS | Impi: waits on the PC's tapered feather cards |
| Footmen batch B (11, PC) | **all PASS** | |
| 12 soldiers (PC) | **all PASS** | |
| Siege crews, ships, armor, aircraft (PC) | PC reports all PASS | see STATUS_PC.md |
| Royals half 1 (22, Mac) | 8/22 PASS (round 8) | 6 wait on PC props (flag devices, wolfhound, feather standard, Aztec fan) |
| Royals half 2 (22, PC) | 16/22 PASS (PC's last report) | |

Split since 2026-10-15 evening: the owner noted the PC is faster, so the PC also owns the soldiers and
footmen batch B (entries, renders, review loop). The Mac owns the builder code (units.py, garments.py,
human.py, compose.py, bscene.py), footmen batch A and the royals. Shared file remaster_footmen.py:
each side edits only its own units' blocks.

PC (Windows, "Civ3 Question Taker"): Catapult, Cannon, Galley, Caravel PASS; 12 horse riders and the
3 chariots rolled out and frozen for the owner's judgment (Horseman, Rider, Ansar PASS). Delivered
firearms, royal props and 12 hat kinds (riders.HEADGEAR). War Elephant WIP is not good: no realistic
CC0 elephant exists; **owner decision needed: keep the old remake's elephant, or a CC-BY model with
credit.** Next for the PC: Trebuchet, Hwacha, Artillery, Flak, then ships, armor, aircraft.

## New on the Mac today
- compose.py: no white piping round civ-color parts (partly tinted pixels take their untinted
  neighbors' color). Every sheet must be recomposed/rebuilt to get it.
- Foot units take `'headgear': '<kind>'` (any riders.HEADGEAR kind, incl. modern_helmet).
- `'gender': 0.0` female bodies; `'bottom': 'gown'`; `'helmet': 'crown'` (spiked/arched/tall/circlet,
  crown_gems); stances fists_on_hips, hands_clasped, hands_behind, royal (item raised in the right
  hand), order_arms (butt by the foot), port_arms (diagonal, left hand at 'fore_end').
- Pelt kinds: `'pelt_kind': 'cloak'` (to the thighs), `'fur_vest': (color, dark)`.
- Wider bands over the clothes; hip sword hangs down; 'upright' poles vertical ('upright_dir').

## Today's features (entry keys)
headgear (any riders.HEADGEAR kind, placed from the head mesh), hem_bands, mail_parts, coat_length /
coat_flare / coat_metal, chest_emblem, patterns (camo / check / tartan), fur_vest, pelt_kind 'cloak',
chest_strap width/tint, cap_knob, tint_shade, cuirass_bands, back: any prop, hip_hold, order_arms,
port_arms, royal, fists_on_hips, hands_clasped, hands_behind, gender, gown, crown, veil_drop, hang_dir,
upright_dir, shield_out, brow_drop. Global: AO cavity + unsharp mask, harder key, belt buckle and pouch.

## Next steps (Mac)
1. Queue running (scratchpad queue2.sh): Swordsman/Settler/Worker, batch A again, the 12 soldiers.
   Review each; fix; repeat.
2. Royals: first renders and review; then left-hand items, face props (glasses), fist_on_hip (one hand).
3. Features most asked for: camo pattern, hip hold, back slot for any prop, boots height, hem band,
   long coat, Berserk/Enkidu fur (use fur_vest / pelt cloak), Inquisitor civ color.
4. When the owner approves the look: `build.py --install` into C7/ModernArt, thumbnails, PR/merge.

## Lessons
- Hair color never applied before 2026-10-15 evening (hair_color ran before the hair asset was
  added; the strand coat's melanin model paled every color). Fixed: toned after the assets,
  normalized by the texture's mean color; strands colored directly, darkened.
- Bands and hems are cut from subdivided copies: the body's faces are larger than a band.
- Edit code with asserted replacements: unchecked string replaces silently failed and wasted rounds.
- MPFB parents assets (hair, brows, eyes) to the rig, not the body (human.assets).
- Props are placed in world terms (human.hold, center=True for shields; keeps the prop's scale).
  The bodies are heroic (taller than 1.75 m): long props need ~15% more length to reach the shoulder.
- Low specular on dielectrics, or dark colors go grey under the sky.
- Blender crashes now and then with cloth sim: build.py retries.
