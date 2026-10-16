# PC handoff to the Mac (2026-10-16)

stzur has ended the PC session for now: "push all work and prepare
development (or have the Mac do it)". Everything is pushed to
units-remaster. The PC has NOT touched Development and has made no tags.
Please do the Development prep.

## Done on the PC (all reviewed PASS, 6 axes >= 7)

- Siege with crews (6 units) and TOW Infantry; soldiers (12); footmen batch B (11) and Impi.
- Ships (18); armor (6, armor.py); aircraft (7), ICBM, Tactical Nuke, Cruise Missile, Holy Relic (aircraft.py, siege.py, crews.py).
- Royals half 2 (22/22).
- The 7 units handed over in TODO_PC round 1: Army Ancient, Industrial, Modern; King Austria, Aztec, Incan; Impi.

## Install into C7/ModernArt (not done)

`units3d/install_pc.py` is ready:
- run with no arguments for a dry run;
- `--apply` installs with build.py --install semantics (keeps `<Unit>_32.png`);
- `--with-frozen` adds the riders and chariots;
- it never installs the War Elephant.

stzur did not explicitly approve installing the frozen riders, chariots or War Elephant. Confirm with stzur before using `--with-frozen`.

Gaps to fix before `--apply`:
1. Catapult, Cannon, Trebuchet, Hwacha, Artillery and Flak have no composed sheets. Run `python ArtGen/units3d/build_pc.py Catapult Cannon Trebuchet Hwacha Artillery Flak` first.
2. The PASSED list lacks the ships (`ships.BUILDERS` won't import outside Blender) and footmen batch B. Add them by name.

## Open tasks (TODO_PC round 2, not started)

1. **The last 4 royals** (now the PC's, or take them back): King Iroquois, Chinese, Indian, Byzantines. Blockers are in their MISSING notes and in TODO_PC.md.
2. **A full-set contact sheet** of every unit against its original, for stzur.
3. **Optional polish:**
   - King Netherlands: cape folds and a wavy or scalloped hem (it passes without them).
   - King Austria: bolder brocade (4-5 cream columns with gold dividers); gold shoe buckles.
   - King Aztec: a central red block and a narrower crest seen from the back.
   - F-117: no work open.

## Still waiting on stzur

- Approval of the look, then the install and the merge.
- The War Elephant asset (the CC-BY choice).
- Judgment on the frozen riders (9) and chariots (3).
- Animations (stills first; propose in STATUS_PC.md).

## Shared-code changes the PC made (FYI)

- `props.CAMO`: painted Perlin camo per colour key (armor, aircraft).
- `props.material`: a tinted 'polished' material gets no grime.
- Feather, device and dog materials are 'polished'.
- New props: `feather_card`, `feather_standard`, `wolfhound`; `war_standard(device=, flag_scale=)`.
- `riders.feather_headdress` styles 'fan' and 'crest'; the `wild_shock` hat.
