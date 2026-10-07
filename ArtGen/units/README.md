# Units: remade unit animations

Civ3's units were pre-rendered 3D models. The remake renders new, stylized
low-poly 3D models with a small numpy software renderer, in the same
isometric view, into sheets the game reads in place of each `.flc` (see the
contract in `ArtGen/README.md`).

```
python ArtGen/units/build.py                  # every catalogued unit (all cores)
python ArtGen/units/build.py Warrior Archer   # just these
python ArtGen/units/build.py --list           # what is / isn't catalogued
python ArtGen/units/preview.py Warrior                       # original | remake, every flic
python ArtGen/units/preview.py Warrior ATTACK1 --dirs=1,3 --frames=0,5,9 --scale=2
python ArtGen/units/preview.py --showcase Warrior Spearman   # default frame, 4 directions
python ArtGen/units/inspect_original.py Warrior 1            # every frame of every original flic, row 1 (S)
python ArtGen/units/sample_colors.py Warrior                 # the original's dominant colors
```

Previews go to `ArtGen/_previews/units/`, per-flic build stats to
`ArtGen/_previews/units/build_stats.json`. Uncatalogued units are skipped, so
the game keeps their original art.

## How it works

| File | What it does |
|------|--------------|
| `flic.py` | Reads Civ3 `.flc` files (port of `ConvertCiv3Media/ReadFlic.cs`) and unit `.INI`s. Splits a frame like the game: index <16 or even <64 = civ-color tint, 240–254 = black shadow (alpha (255-i)·16), 255 = transparent. |
| `mesh.py` | Triangle-soup `Mesh` (positions, per-corner normals, material ids), `Materials` (color, `tint` flag, specular, metal), transforms (`translate`, `rot_x/y/z`, `euler`) and primitives: `box`, `rounded_box`, `ellipsoid`/`sphere`, `cylinder`, `cone`, `capsule` (tapered limb between two points), `loft` (stacked elliptical/superellipse rings: torsos, skirts, helmets, hulls), `lathe`, `extrude` (2D outline → slab: blades, axe heads), `disc` (domed shield), `blade`, `plate`. |
| `render.py` | Camera + rasterizer + shading. Orthographic, looking north, tilted down 30° (Civ3's 2:1 diamond). World: x east, y north, z up, meters; a model faces +y and `facing_matrix(row)` turns it to flic row `row` (SW, S, SE, E, NE, N, NW, W). Fully vectorized rasterizer (triangles binned by size, fragments resolved by sort) at 3× supersampling, deferred shading: NW key light with wrap, sky ambient, front-right fill, rim light, Blinn specular (metals tinted), screen-space AO + ground AO, inner lines at depth breaks, outer dark contour, soft blurred ground shadow cast down-right (faded near the cell border). Tint materials render as gray shading into the tint layer; base alpha is compensated so base + tint composite correctly in the game. Only the sprite's bounding box is rendered. All look constants are in `render.Style`. |
| `rig.py` | `Skeleton` (bone tree), `Keys` (Catmull-Rom keyframes over normalized time, looping or not), `frame_times(n, loop)`. |
| `framework.py` | `UnitJob`: builds a unit's model from its catalog entry, calibrates it against the original (ground anchor = median foot point of the DEFAULT flic in 240×240 unit space; scale fitted so the standing height matches), renders each flic's directions × frames into 2× cells (`render_flic`) and writes `<flic>.png` / `<flic>_tint.png` (`save_sheets`, with a layout check). `ActionContext.event()` measures the original's motion (`'reach_max'`: frame of furthest extent, e.g. a strike; `'down'`: a death's fall; `'low'`, `'high'`) so animations can be retimed to match. |
| `models/` | Archetypes (`@archetype('name')` classes), loaded automatically. `humanoid.py`: foot units; `props.py`: weapons, tools, shields and worn gear shared by all archetypes. |
| `catalog/` | Which unit folders are remade and with what parameters. Every module's `UNITS` dict is merged; a unit may only appear once. `ancient.py` holds the first batch. |
| `build.py` | Parallel driver (one task per flic, `multiprocessing` over all cores). |

### The humanoid archetype (`models/humanoid.py`)

A chunky, Civ3-proportioned body (big hands, feet and head) on a 17-bone
skeleton. Clothing and gear are separate meshes on the bones, chosen by
catalog parameters (`hair`, `beard`, `helmet`: crested/horned/hood/cap/headband/wolf,
`top`: bare/tunic/sleeved/strap/mantle/vest, `cuirass`, `bottom`:
loincloth/skirt/kilt/long, `bracers`, `wraps`: wraps/boots/greaves, `sandals`,
`weapon`, `offhand`, `back`: quiver/backpack/pelt, `pack_strap`). `colors` overrides any material
color (`skin`, `hair`, `top`, `bottom`, `belt`, `wraps`, `helmet`, `crest`,
`cuirass`, `pelt`, `wood`, `steel`, `stone`, `leather`, `shield_face`, ...); `tint`
lists the materials drawn in the civ color (give them light colors: they
render white-shaded).

**Body build (`'build'`).** Civ3's foot soldiers are brawny; use a build to
match. A name from `humanoid.BUILDS` — `'normal'` (the default; no change),
`'lean'` (muscle 0.5: workers, settlers, scholars), `'muscular'` (muscle 1.0:
most soldiers), `'brawny'` (muscle 1.3: barbarians, berserkers) — or a dict
`{'build': 'muscular', 'height': 1.05, 'bulk': 1.1, 'muscle': 1.2}` (any key
optional; `bulk` scales torso/limb thickness, `height` the skeleton).
`muscle` > 0 broadens the chest and shoulders (V taper) and adds shaped
volumes: pecs and traps (on bare chests), deltoids, biceps, triceps, forearms,
quads and calves. Muscles under clothing take the garment's material (sleeves
and leg wraps bulge instead of being pierced). Entries without `build` render
exactly as before.

Animations are functions of normalized time returning pose channels in model
space: `hand_r`/`hand_l` (grip points; arms reach them by two-bone IK with
`elbow_*` pole hints), `foot_r`/`foot_l` (ankle points; legs by IK, knees
forward), `toe_*`, `fyaw_*`, `root_pos`/`root_rot` (pelvis offset/rotation),
`spine`/`chest`/`neck`/`head` (pitch, roll, yaw degrees; + pitch leans back,
+ yaw turns left), `w_main`/`w_off` (orientation of the right/left hand
prop, whose long axis is its +z), `draw` (bow string), `pack` (settler's pack
set down). Actions that throw the body around (death) pose limbs directly
(FK channels `arm_*`, `forearm_*`, `thigh_*`, `shin_*`; + pitch swings a
hanging limb forward). The animation library (names used in a catalog `actions` map):

`idle`, `run`, `attack_chop`, `attack_lunge`, `attack_slash`, `attack_thrust`,
`attack_shoot`, `death`, `fortify`, `fidget`, `victory`, `build`, `captured`,
`work_dig` (road), `work_pick` (mine), `work_hoe` (irrigate), `work_hammer`
(fortress, kneeling), `work_machete` (jungle), `work_chop` (forest),
`work_sow` (plant). The default INI-action → animation map is
`humanoid.DEFAULT_ACTIONS`; labor actions swap the held tool (`WORK_TOOLS`).
Every flic keeps the original's frame count; loops (idle, run, labor) never
repeat their first frame, one-shots run start to end, and strikes and falls
are retimed to the frame where the original strikes or falls.

## Adding a unit

1. Look at the original: `inspect_original.py <Unit> 1` (all frames, facing
   S) and `sample_colors.py <Unit>` (its colors). Note which INI actions it
   has (`flic.unit_flics('<Unit>')`).
2. If an existing archetype fits (any foot unit: `humanoid`), add an entry to a
   catalog module for its group, e.g. `catalog/medieval.py`:
   ```python
   UNITS = {
       'Pikeman': {
           'archetype': 'humanoid',
           'colors': {'skin': '#c8905c', 'helmet': '#9a9aa0', 'bottom': '#6a4a2e'},
           'tint': ['top'],
           'helmet': 'crested', 'top': 'sleeved', 'cuirass': True, 'bottom': 'skirt', 'wraps': 'boots',
           'weapon': 'spear', 'offhand': None,
           'actions': {'ATTACK1': 'attack_thrust'},
           # optional: 'stance': {...pose channels...}, 'build': {'bulk': 1.1}, 'scale': 1.0, 'anchor': (x, y)
       },
   }
   ```
   Create a new module rather than editing someone else's (no conflicts);
   unit names must be unique across modules (checked at load).
3. New gear: add a function to `models/props.py` (grip at the origin, long
   axis +z, materials via `mat(mats, name, default_color)` so catalogs can
   recolor them) and handle it in `humanoid.stance()` if it needs its own
   rest pose. New helmets/clothes: `humanoid.Body`.
4. A new kind of unit (horses, elephants, chariots, ships, aircraft,
   vehicles): add `models/<category>.py` with a class decorated
   `@archetype('<name>')`, taking `(unit, params)`, with a `mats`
   (`mesh.Materials`) attribute and `frame(action, f, n, ctx) -> Mesh` (model
   space: facing +y, ground at z = 0, meters, ~1.8 m per human). Use
   `rig.Keys`/`frame_times` for timing and `ctx.event(...)` to match the
   original's beats. Rigid vehicles are simplest: build the meshes once and
   move/rotate parts per frame (recoil, turret yaw, bob, sinking). Riders can
   reuse `humanoid.Body` parts and `Humanoid.pose_mesh`.
5. `python preview.py <Unit> --scale=2` and compare with the original
   (silhouette, colors, weapon, pose, timing, nothing cut off at the cell
   edges); `python build.py <Unit>`; check in the game with the devshot tool
   (`ArtGen/README.md`). The build prints a warning when a flic's sprite
   touches its cell border (it'd be cut off): shorten the motion or set the
   catalog `scale`.

Orientation rules of thumb: a model faces +y with its right hand at +x; `facing_deg` turns it.
Keep motion readable from the camera: strikes and labor go to the side
(not straight at the camera), and weapons stay outside the silhouette.

## Stats (first batch, 5 processes)

| Unit | Flics | Frames | CPU time | Sheets on disk |
|------|-------|--------|----------|----------------|
| Warrior | 8 | 880 | 138 s | 5.7 MB |
| Spearman | 8 | 880 | 142 s | 5.7 MB |
| Archer | 7 | 776 | 113 s | 4.8 MB |
| Swordsman | 8 | 880 | 160 s | 6.3 MB |
| Settler | 6 | 640 | 118 s | 4.1 MB |
| Worker | 11 | 1000 | 144 s | 5.7 MB |

About 0.13–0.18 s per frame per process with 5 processes (0.35–0.5 s when
20 processes share the machine); the six units build in ~2.5 minutes wall
with 5 processes, ~32 MB.
At this rate all ~150 Civ3 units would be roughly 0.8 GB of sheets and
~50 minutes. The sheets are 2× the flic frames (e.g. WarriorDeath:
5040×1440 px).

## Known weak points

* Bodies are rigid parts (no skinning): joints are covered by spheres, and
  skirts don't follow the thighs on big strides.
* Faces are minimal (brows, eyes, nose); fine at map scale, plain up close.
* Settler BUILD keeps the staff in hand instead of planting it.
* Animations are hand-keyed per weapon style and retimed to the original's
  strike/fall frame, not traced pose by pose.
