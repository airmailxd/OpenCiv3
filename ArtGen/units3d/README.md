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

## Posture (human.posture)

Every standing foot unit is posed through `human.posture(arm, aims, settings, scale)` (called by
`units.equip`): the stance's bone aims laid over a weight shift (contrapposto), with the torso kept upright
(the owner, 2026-10-16: no hunch, no forward lean; `units.UPRIGHT` still sets the spine).

1. the pelvis rolls `hip` degrees so the weight leg's hip rises, and turns `hip_turn` degrees;
2. the torso takes its aims; the shoulder line counter-rolls `shoulder` degrees (the weight side down) and
   the chest turns to `chest_turn` degrees from the front, both about the chest;
3. the neck and head take their aims again (level, over the hips) and the head turns to `head_turn`;
4. the arms and legs take their aims (so a weapon's carry, set by the arms, is unchanged);
5. both ankles come to one height (`human.leg_to`, a two-bone solve): the weight foot in under the body by
   `weight_in`, its leg near straight; the free foot out by `free_out` and forward by `free_fwd`, its knee
   bending forward and out to reach the ground; `free_lift` raises the free heel.

The settings: `human.POSTURE` (defaults, the weight on the right leg), laid under `units.STANCE_POSTURE`
(per stance), laid under the entry's `'posture'` dict. `'posture': False` (or `'weight': None`) poses the
aims alone, as before. Riding (`ride`), digging (`low_across`) and `stoop` entries get none.
The tools under it: `human.turn(arm, bone, axis, degrees)` turns a bone about any armature-space axis
through its head (`aim` only points bones, never rolls them), `human.twist` about its own length, and
`human.leg_to(arm, side, target, knee)` places an ankle.

The animations (anim.py) start from the built pose, so the weight shift carries into their first and last
frames; their aims (absolute) take the limbs and spine from there.

Stances with the weapon carried where it reads (units.STANCES): `shield_ready` (a short sword ready at the
hip, point forward and out, `sword_dir`; the shield held by its grip behind the boss with `shield_at`
'grip', `shield_yaw`, `shield_tilt`, `grip_depth`) and `bow_ready` (the bow upright and out in the left
fist, `bow_up`; an arrow nocked on the string, `arrow_dir`, the right fist at the nock).
The animation templates (anim.py) know both: `shield_ready` animates as 'melee', `bow_ready` as 'bow', and
their FORTIFY keeps the stance's arms (a braced shield, a nocked arrow) while the body crouches.

## Japanese armour (yoroi.py)

Real parts for the samurai and the MDJ Japanese units. An entry with `'yoroi': {piece: params, ...}` gets each
named piece from `units.dress` (pieces left out are not made; `{}` takes the defaults). Heights are for a
1.75 m man, as in garments.py. Colours come from the entry's `colors`; the civ colour goes where the entry's
`tint` lists `'lace'` (all odoshi lacing) and `'banner'` (the sashimono).

| Piece | Function | What | Main parameters |
|------|------|------|------|
| `hakama` | `hakama(entry, mesh, arm, ...)` | wide pleated trousers, ballooning to the knee, gathered at the hem | `hem`, `balloon`, `gather`, `pleats`, `n_pleats` |
| `suneate` | `suneate(...)` | cloth gaiters with iron splints down the shins | `z0`, `z1`, `splints` |
| `haidate` | `haidate(...)` | thigh apron of laced lames over the hakama | `z0`, `z1`, `rows`, `offset` |
| `kote` | `kote(...)` | mail-covered sleeves, a forearm plate and a hand plate | `plates` |
| `do` | `do(...)` | okegawa-do: lacquered horizontal lames in a barrel, breastplate, shoulder straps, sugake cords | `rows`, `z0`, `z1`, `mune`, `barrel`, `sugake` |
| `kusazuri` | `kusazuri(...)` | tassets: panels of laced lames from the do's hem | `panels`, `rows`, `length`, `flare`, `gap` |
| `sode` | `sode(...)` | shoulder boards of laced lames, hung on the upper arms | `width`, `height`, `rows`, `out`, `drop` |
| `menpo` | `menpo(...)` | half mask over cheeks and chin, laced throat guard | `throat_rows` |
| `kabuto` | `kabuto(...)` | ridged bowl, peak, flared shikoro, fukigaeshi, gilt kuwagata | `ridges`, `bowl`, `lames`, `kuwagata`, `fukigaeshi`, `horn_height` |
| `sashimono` | `sashimono(...)` | back banner on a pole and top bar, with a pale mon | `height`, `width`, `pole`, `side` |
| `wakizashi` | `wakizashi(...)` | short sword through the obi | `length`, `at`, `direction` |

Colour keys: `lacquer` (plates), `lace` (odoshi when not civ), `iron`, `gold`, `hakama`, `kote`, `kyahan`, `menpo`,
`mon`, `banner` (when not civ).

Building blocks, for new pieces:

- `laced_rows(obj, z0, z1, rows, plate, lace=None, step, edge, top_lace, axis, centre)`: cuts any shell or skirt
  into rows of lames with real steps (each lame's foot stands out over the next) and gives each its
  lacquered top edge and its lacing (kebiki odoshi); `lace=None` is plain lacquered plate (an okegawa-do).
- `cut(obj, inside(centre, normal), levels)`: keeps the faces passing a test (panels, splints, half shells).
- `lacquer(name, color)`, `odoshi(name, color, tint)`, `iron(name, color)`: the materials.
- `kabuto_mesh(p, ...)`, `sode_mesh(p, ...)`, `sashimono_mesh(p, ...)`, `saya(p, ...)`: the rigid parts as
  `props.Prop` builders (`'katana_saya'`, the empty scabbard, is registered in `props.BUILDERS`);
  `on_head(obj, mesh, arm)` fits headgear-space props on the head.

Related entry keys in units.py: `hip_at` (where the hip sword's scabbard mouth sits) and, in anim.py,
`hip_drawn` (the empty scabbard shown while an action's pose says `drawn`). `anim.ACTIONS['Samurai']` draws
and cuts (ATTACK1), draws into chudan (FORTIFY), runs with the hand on the hilt and falls; `OWN_BASE` units
start their actions from their own still stance.
