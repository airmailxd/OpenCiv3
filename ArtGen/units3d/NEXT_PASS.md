# Unit remaster: notes, concerns and questions for the next pass

This is written at the release that first ships the remastered units (October 2026). STATUS.md has the per-group state. STATUS_PC.md has the Windows PC's side.

## What shipped in v0.8.0 (October 16, 2026)

**Only the Mac's 28 units are installed in the game**:
- the 6 pilots;
- footmen batch A except Impi;
- 12 royals: Egyptian, German, Army Middle, Arab, Babylonian, Carthaginian, English, French, Greek, Hatti, American and Celtic.

All the other units were remastered and passed review on the PC, but they are **not installed yet**. That covers the soldiers, footmen batch B and Impi, the other royals, the siege units with crews, ships, armor, aircraft, rockets and the Relic.

The PC session only installs what stzur approves, directly in that session: its riders, chariots and the War Elephant were frozen for stzur's judgment. **To ship them, tell the PC session to run `ArtGen/units3d/install_pc.py --apply`** (add `--with-frozen` for the riders and chariots), commit, then release again. It first has to re-run build_pc for the 6 siege units, whose sheets weren't composed, and add the ships and footmen batch B to its list.

## State when the Mac shut down (October 16, 2026, mid-morning)

- **v0.8.1 is not released yet.** The Mac's local re-render of the PC's units was stopped: it would take hours.
- The PC session ("Civ3 Question Taker") was asked to:
  1. pull units-remaster at 669d4ff3 or later;
  2. run `install_pc.py --apply` WITHOUT `--with-frozen` (the list now has ships and footmen batch B);
  3. commit the C7/ModernArt/Art/Units changes and push them to units-remaster.
- **Next session (either machine), once that push is in:**
  1. Fast-forward Development to units-remaster.
  2. Run the unit art tests and `dotnet format --verify-no-changes`.
  3. Write the "Say what's new in version 0.8.1" entry (site/index.html, site/whats-new.html). It covers the remaining remastered units: soldiers, footmen batch B, Impi, 28 royals, siege with crews, ships, armor, aircraft and the Relic.
  4. Tag v0.8.1 and push.
- Still out until stzur approves: the riders, chariots and War Elephant. The last 4 royals are out too (Iroquois, Chinese, Indian, Byzantines).

## The work itself

- Blender-rendered stills for all of the standalone set's human, vehicle and ship units: one pose in 8 directions, shown for every action, using one-column sheets read by UnitArtMetadata.SheetColumn.
- Two machines built them: this Mac and the Windows PC.
- Each unit passed an independent reviewer on 6 axes: imposing, not AI-looking, faithful to the original, readable at 1x, materials, and detail vs the original (the owner's rule: equal or more detail than the original). PASS means every axis at 7 or higher.
- **Exceptions at release time:**
  - The War Elephant keeps the old remake's art. No realistic CC0 model was found.
  - The PC's STATUS_PC.md lists any royals still short of a pass at release. At the Mac's last count those were Iroquois, Chinese, Indian and Byzantines. They ship in their current, near-passing state.

## Concerns

1. **Stills only.** Attack, death, fortify and run all show the same pose. The units read well standing, but combat has no motion. This is the biggest gap against the originals.
2. **The passes come from AI reviewers.** No human has judged the full set yet. Before the next release, look at the whole set side by side (the PC was asked to build a contact sheet), above all the scale and the tint placement.
3. **Code changed after some units passed.** Several global changes landed partway through:
   - cloth twill, sheen and mottle;
   - the dark-skin desaturation;
   - the metal form shading;
   - the hair-color fix;
   - the white tint-edge fix.

   Some units passed before them and weren't re-reviewed. The shipped sheets were also built at different code versions, on two machines. **Recommendation:** re-render the entire set once with the final code on one machine, re-review it in a single sweep, then install.
4. **Scale.** Foot units are deliberately about 10–20% taller than the originals, for an imposing look. Royals use 0.85. Check that this reads well on the map next to cities and terrain.
5. **The thumbnails (<Unit>_32.png) and the Civilopedia icons are still the old art.** They no longer match the new sprites.
6. **Approximations a careful player may notice:**
   - Iroquois: its shield is slung on the back; the original holds it at the hip.
   - Greek: a cloth sash stands in for a draped himation.
   - Byzantines: the crown panels and broad collar are missing.
   - Impi: the skin hue stays a little redder than the original. Traced as far as MakeHuman's skin node group, not fixed.
   - SciLeader (Einstein): uses a scaled hair asset. Particle hair read as a white ball.
7. **The notes sometimes contradicted the original sprites.** Where they disagreed, we followed the original image. A few units changed noticeably because of this: Army Modern's cap, Austria's cape, and several royal tint layouts.
8. **The pipeline is fragile in two places.** Blender crashes now and then (build.py retries), and particle hair is hard to control. See the "Lessons" in STATUS.md and the memory notes.

## Questions for the owner (stzur)

1. **Animations:** go ahead, and for which actions first? Attack and death matter most.
2. **Thumbnails and Civilopedia icons:** regenerate them from the new stills?
3. **War Elephant:** keep the old one, use the CC0 low-poly with Civ3's gear, or a CC-BY model with credit?
4. **The two suggested waivers:** Gandhi isn't "imposing", by design, and the Chinese leader's Mao suit is plain, by design. Do you accept judging them against the original instead?
5. **Scale:** keep the heroic +10–20% for foot units, or bring them closer to the originals?
6. **Riders and chariots:** these were frozen for your judgment and now ship as they were. Are they OK?
7. **Leader heads:** do you want the per-era leader heads that were deferred earlier?

## Suggested next pass

1. Run one consistent full re-render and re-review (concern 3).
2. Do the human review on the contact sheet, then fix what you flag.
3. Regenerate the thumbnails and Civilopedia icons.
4. Animations: attack and death first, then fortify and run.
5. Close the remaining royals and the approximations listed above.
