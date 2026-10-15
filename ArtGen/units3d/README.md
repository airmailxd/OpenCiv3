# units3d: the remastered unit stills (Blender)

The second unit art pass: each unit is a realistic 3D model rendered in
Blender (Cycles) in the game's isometric view and light, one still pose in
the 8 directions, which the game shows for every action (a sheet of one
column; see UnitArtMetadata.SheetColumn). Direction from the owner: "Civ3
remastered", not AI-looking; imposing; the Civ3 original as the baseline,
but creative; every unit still readable at 1x.

## Setup (once per machine)

1. Blender 4.5 LTS (GPU: OptiX/CUDA/Metal is picked automatically).
2. MPFB (MakeHuman for Blender) as an extension, from its source:
   download https://github.com/makehumancommunity/mpfb2/archive/refs/tags/v2.0.18.zip,
   zip its `src/mpfb` folder and install it:
   `blender -b --factory-startup --command extension install-file -r user_default -e mpfb-ext.zip`.
3. MakeHuman's CC0 system assets: download
   https://files.makehumancommunity.org/asset_packs/makehuman_system_assets/makehuman_system_assets_cc0.zip
   and unzip it into MPFB's user data folder (print it with
   `from bl_ext.user_default.mpfb.services.locationservice import LocationService; LocationService.get_user_data()`;
   on macOS `~/Library/Application Support/Blender/4.5/extensions/.user/user_default/mpfb/data`).
4. The ArtGen venv (pillow, numpy, scipy) and `python ArtGen/units3d/catalog_json.py`.

## Rendering a unit

    blender -b --factory-startup --python-exit-code 1 --python ArtGen/units3d/render_unit.py -- Warrior <out>
    python -c "import compose, json; compose.write_unit('Warrior', '<out>', json.load(open('calibration/Warrior.json')), '<sheets>')"

## Files

| File | Side | What |
|------|------|------|
| bscene.py | Blender | scene, camera (matches ArtGen/units/render.py), lights, shadow catcher, tint AOV, passes |
| human.py | Blender | MPFB bodies, skin tone, grime, posing (aim), holding props, bulk, fists |
| garments.py | Blender | clothes from the body's surface, skirts, loincloth flaps, pelts, hair, beards, crests, drape |
| units.py | Blender | a foot unit from its catalog entry (dress, equip, stance); `seated=True` for riders |
| remaster.py | both | creative changes over the old catalog, per unit |
| clubs.py, props.py | Blender | weapons, tools, shields, back gear |
| vkit.py, siege.py, ships.py, mounted.py | Blender | vehicles, ships, horses (PC) |
| render_unit.py | Blender | renders one foot unit's still |
| compose.py | venv | passes -> base/tint layers, one-column sheets, metadata |
| calibration/ | data | the originals' anchors, scale and flic headers (numbers only) |
