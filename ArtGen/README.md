# ArtGen: the remade ("modern") map art

These scripts draw the art for the **Graphics Overhaul** setting (Main Menu →
Settings → Graphics). The art is drawn with code (procedural textures,
lighting, and a small software 3D renderer for units and cities) and written
to `C7/ModernArt/`, laid out like the game's media folders. With the setting
on, the game uses a remade file wherever one exists and falls back to the
original Civ3 art everywhere else. See `C7/Textures/ModernGraphics.cs`.

Build everything: `python ArtGen/build_all.py`. Each area can also be built on
its own: `python ArtGen/<area>/build.py`.

## The contract with the game

* **Same file, same layout, twice the pixels.** The remake of
  `Art/Terrain/xtgc.pcx` is `C7/ModernArt/Art/Terrain/xtgc.png`, exactly
  2× the original's width and height. Every sprite sits in its original cell,
  scaled by 2, because the game crops sprite sheets with the original's
  coordinates (×2). Use `civ3art.save_modern(rel, rgba)`, which checks the size.
* **Same footprint.** A sprite is drawn where the original's was, so it must
  occupy the same area: tiles fill the same diamond, mountains rise from the
  same base, a city covers the same ground. Use the original's alpha as the
  reference footprint (upscaled ×2) and stay close to it.
* **Transparency is real alpha** (the game reads PNG alpha directly). Shadows
  are black with partial alpha.
* **Base terrain tiles have hard edges.** A base tile's alpha must be exactly
  the original's diamond mask upscaled ×2 (nearest neighbor), so tiles still
  join without seams. Everything drawn on top of tiles (hills, forests, rivers,
  roads, cities, units) gets smooth anti-aliased edges instead.
* **Unit animations** (`.flc`) are remade as two sheets per flic:
  `<flic name>.png` and `<flic name>_tint.png`, in the flic's folder. A row per
  direction, in the flic's order (SW, S, SE, E, NE, N, NW, W), and a column per
  frame, with exactly the flic's number of directions and frames. Each cell is
  2× the flic's frame size, and the unit stands at the same spot in the frame
  as in the original. The tint sheet holds the civ-colored parts (clothing,
  flags, banners), shaded in grayscale/white, which the game multiplies by the
  civ color; those pixels are transparent in the base sheet.
* **Animations are optional.** The game is played with animations off, so the
  committed art is what shows then: units standing or fortified (stored as a
  one-column "still" of the flic's last frame) and workers at work (whole
  loops). `python ArtGen/units/build.py` renders just those; `--animations`
  also renders attacks, deaths and the like into `C7/ModernArtAnimations/`,
  which isn't committed and is used by the game when present.
* **Don't remake** `Territory.pcx` (borders are recolored by exact color),
  `FogOfWar.pcx`, or any interface art.
* **Deterministic.** Fixed random seeds, so a rebuild gives the same art.
* Only numpy and Pillow are guaranteed (`pip install scipy` is fine if needed).

## The look

**KISS: keep it simple.** Modern, but still unmistakably Civ3 (2005): a calm,
clean, readable isometric map that's easy to look at for hours. A careful
repaint of the originals at a higher resolution, not a different game. It must
always be obvious what's going on: what each tile is, what each unit is.

* **Respect the originals.** Each remake keeps what its original shows and
  where: the same subject, silhouette, composition, and color identity.
  Players should recognize every tile and unit at a glance.
* **Muted, natural color. Never more saturated than the original.** Sample
  each original's colors (median per region) and use them as the ceiling for
  saturation; when in doubt, go a little greyer, not brighter. No neon
  turquoise water, no bright lime grass. Gentle contrast. The map should feel
  restful.
* **Simple and legible.** Terrain types must stay instantly distinguishable at
  1x zoom. No busy micro-detail, no strong repeating patterns or waves, no
  photorealism. Large areas should be calm, with soft, low-contrast variation.
* **Clean surfaces.** No palette dithering or pixel speckle. Smooth gradients,
  soft low-contrast procedural texture.
* **Light from the upper left (northwest), high in the sky.** Lit faces face
  up-left; shadows fall down-right, soft, about 35-45% black. Every set uses
  the same light.
* **Soft ambient occlusion** where things meet the ground, so sprites sit on
  the map instead of floating.
* **Sprites (units, cities, resources, improvements):** soft shading, a thin
  darker contour so they read against any terrain.
* **Units must be unmistakable.** Each unit's defining features (weapon,
  headgear, shield, armor, mount, hull) are clear at 1x, and units look
  different from each other. The civ color is an accent, used only on the
  parts the original colors (about as much area as the original's tinted
  pixels, never more): most of a unit is its own natural colors.

## Checking your work

* `civ3art.save_preview(name, original_2x, remake)` writes side-by-side images
  to `ArtGen/_previews/` (not committed).
* In the game: from `C7/`, run
  `C:/Dev/Godot_v4.4-stable_mono_win64/Godot_v4.4-stable_mono_win64_console.exe --path . res://_devshot/devshot.tscn -- "--save=<SAV>" --out=<dir> --name=<prefix> --zoom=1,2 [--tile=x,y] [--modern=both|0|1]`.
  It loads a save, takes screenshots of the map with the original and the
  modern art at each zoom, and quits (it needs about 20–40 seconds). A good save:
  `D:/Steam/steamapps/common/Sid Meier's Civilization III Complete/Conquests/Saves/Auto/Conquests Autosave 280 AD.SAV`.
  The C# is already built; don't edit or rebuild the C# project.
