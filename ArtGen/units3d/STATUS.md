# Unit remaster: status (2026-10-15)

Branch `units-remaster` on airmailxd/OpenCiv3 (Mac and the Windows PC both push here; nothing on
Development, nothing tagged). Stills only (one pose, 8 directions; every action shows it).

## Review state (independent reviewer agents, PASS = every axis >= 7)
| Unit | State | Open notes |
|------|-------|-----------|
| Warrior | PASS (7/7/7/8/7) | hair since fixed (assets under the rig); re-check once |
| Spearman | PASS (7/7/7/8/7) | dark blotch at the back waist; contact AO under the shield |
| Archer | 6/7/7/7/6 | hair now dark (fixed after the last review); grip through the left fist; apron pale edge |
| Swordsman | 6/6/7/6/7 | shield now 'left_hip' (untested in a full build); cuirass trim/straps; balloon pauldrons |
| Settler | beats old; 6/5/7/7/5 | pack proportions (bedroll shorter, sack lumpy, straps); lean under the load; cloth grime |
| Worker | 5/5/7/6/6 | pose has no intent (try two hands / planted shovel); straw cap texture; blade value |
| 22 footmen | entries drafted (remaster_footmen.py), not rendered | see its `# MISSING FEATURES` list |
| soldier / royal / Crusader / TOW | calibration + notes ready (from the PC) | no entries yet |

PC (Windows, "Civ3 Question Taker"): Catapult, Cannon, Galley, Caravel PASS; all 12 horse riders rolled
out (Horseman, Rider, Ansar PASS; others 6-7, waiting for the owner's judgment); next: chariots, War
Elephant, then ships, armor, aircraft. Its files: props.py, vkit.py, siege.py, ships.py, mounted.py,
riders.py, render_vehicles.py.

## Next steps (Mac)
1. Rebuild the six pilots (`python build.py Warrior Spearman Archer Swordsman Settler Worker`; the last
   rebuild was interrupted at shutdown) and re-review; fix the open notes above.
2. First renders of the 22 footmen; review in batches; implement the top MISSING features
   (hip slot for sheathed swords, folded arms, great helm, veils/masks, harness, long coat).
3. Entries for soldiers (firearms props needed from the PC), Crusader, TOW, then the 44 royals
   (notes say most catalog gestures are wrong: arms low / fists on hips / hands clasped).
4. When the owner approves the look: `build.py --install` into C7/ModernArt, thumbnails, PR/merge.

## Lessons
- Edit code with asserted replacements: unchecked string replaces silently failed and wasted rounds.
- MPFB parents assets (hair, brows, eyes) to the rig, not the body (human.assets).
- Props are placed in world terms (human.hold, center=True for shields; keeps the prop's scale).
- Low specular on dielectrics, or dark colors go grey under the sky.
- Blender crashes now and then with cloth sim: build.py retries.
