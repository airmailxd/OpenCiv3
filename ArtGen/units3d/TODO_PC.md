# TODO for the PC session (from the Mac, 2026-10-16, round 3)

stzur looked at the units in game (showcase saves) and sent three notes:

> Elephant needs to be more detailed, and probably fully reworked.
> Samurai has no sword.
> In general, the units are hunched too far forward.

stzur also answered two questions:
- **War Elephant: use a realistic CC-BY model, credited.** This settles the open asset question.
- **Samurai: a hand on the sheathed hilt**, ready to draw. It does not get a drawn sword or folded arms.

stzur wants this done as fast as possible, split between both machines. Pull `units-remaster` first.

## 1. War Elephant: a full rework (yours)

- Replace syncopika's low-poly elephant in elephant.py with a **realistic, detailed CC-BY elephant**: wrinkled hide, real ears, trunk and feet, and Indian/Asian proportions if you can find them. Sketchfab's downloadable CC-BY models are a good place to look.
- Fetch it into `_cache/` and check its SHA-256, as you did for the horse.
- Put the author, the model's name, its URL and the licence in `C7/Text/credits.txt`, next to the horse's line.
- Keep or improve every piece of Civ3's gear: the civ-colored caparison with a gold border, the howdah with its civ canopy, the bronze shields and spears, the gold-capped tusks with civ bands and the forehead disc. Add the mahout if the original has one. Write `notes/War Elephant.md` from the original first, since it's the only unit without notes.
- Detail must be **equal to or more than the original** (the 6-axis review, detail included). The current one would fail Detail and Not-plastic.
- Install it once it passes, since stzur asked for it directly. Ask stzur only if the model's licence is anything other than plain CC-BY.

## 2. Posture: everyone stands upright (builder change, now on the branch)

`units.py` now has `UPRIGHT`, used by `stance_aims()`:
- Every standing stance gets an upright torso: spine vertical, chest a little up, shoulders back, head level over the hips. The stances' arm aims are untouched, so weapons stay where they were.
- `pose_extra` torso leans are capped at -0.06 forward.
- Two kinds of pose are left alone: the stances in `LEANING` (`ride`, `low_across`) and entries with `'stoop': True` (only King Indian has it).
- Measured before the change, the head sat 7-17 cm ahead of the hips, and the shoulders rolled forward 12-24 degrees on the 'hang' units.

**Re-render every foot unit you own with it**: the 12 soldiers, footmen batch B (except the Samurai), Impi, royals half 2, the Leaders and Armies, and the standing siege crews. Also check the riders and chariot crews for a forward hunch, since `ride` is left alone. stzur's note is about units "in general", so straighten the riders' backs too if they hunch.

Add a 7th check to the reviewer brief: **posture**, meaning upright and not hunched forward. A hunch is a blocker.

## 3. Samurai: taken by the Mac

The Mac changed the Samurai block in remaster_footmen.py and the katana in props.py:
- `'hand_on_hilt': True`: the right hand on the grip, the left on the scabbard;
- `katana_sheathed(thick=1.5)` with a pommel cap and tip cap;
- a red-lacquered scabbard and an ivory grip, so it reads at 1x.

Please don't edit that block. The Mac renders, reviews and installs it.

## 4. Still open from round 2

- The last 4 royals (Iroquois, Chinese, Indian, Byzantines) and the full-set contact sheet. Do these after 1 and 2.

## Who does what now

| Mac | PC |
|---|---|
| Posture code (done), Samurai | War Elephant rework |
| Re-render its foot units (batch A footmen, royals half 1, pilots, Warrior-Worker), review, install | Re-render its foot units and crews with the posture fix, review, install |

Report in STATUS_PC.md and push. The Mac pulls often.
