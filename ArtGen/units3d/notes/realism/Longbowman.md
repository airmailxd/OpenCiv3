# Longbowman: realism design (2026-11-23)

## The real-world model

An English archer of the Hundred Years' War, about 1415 (Agincourt):

- a **kettle hat** (chapel de fer): a rounded iron skull, a wide sloping brim;
- a quilted linen **jack** (padded coat) with long sleeves and a short skirt; over it a **livery jacket** in his
  lord's colour (the civ colour);
- dark wool hose, ankle boots;
- the **longbow** of yew, as tall as he is, about 1.8-1.9 m; arrows in a canvas arrow bag at the belt; a short sword
  (or a falchion) at the left hip.

The Civ3 original: a pale rounded cap with a civ plume, a civ sleeveless tunic over mail sleeves, grey trousers,
boots, a big arrow bag at the right hip, the longbow upright in front. The cap becomes the kettle hat; the mail
sleeves become the jack (archers rarely wore mail).

## The features that must read at 1x

1. The **longbow**, taller than the man, upright at his left front in a low ready hold, an arrow nocked and pointed
   at the ground: a long dark arc from above his hat to near the ground, in every direction.
2. The **kettle hat**'s brim.
3. The pale **jack** sleeves and skirt against the civ livery jacket.
4. The **arrow bag** at the right hip.

## The pose

Weight on the right leg, the left foot forward; torso upright. The bow fist low and forward at his left (belt
height), the bow upright; the right fist at the nock behind the bow, the arrow pointing down ahead.

## Civ colour

The livery jacket over the torso. Stock: the shipped Longbowman 5-13 % (mean 9), Bowman 21-32. Aim 10-20 %.

## Stock neighbours

Archer and Spearman (shipped).

## Shared-code options this unit uses (off by default)

- `bow_ready` with `hand_l_at` (as the Bowman); `riders.HEADGEAR['kettle_hat']` (new).

## Rounds

### Build rounds 1-2
- The kettle hat, pale jack and civ livery read; the arrow bag shows. Civ 5-14 % (mean 11) once the jack's skirt
  took the livery colour. The bow merges with the body in 0-1.

### Critic round 1 (batch 1): 66.5 / 74 = 70.25
Both: the right forearm raised across the face (the draw elbow's pole); the bow short in 0-2; the jack and livery
flat. Fixes: `draw_elbow` 0.3, the bow fist higher and out, the bow canted, the jack quilted (a fine check), dark
bound edges on the livery.

### Critic round 2 (batch 1): 71 / 71 = 71
Landed: the arm off the face (the nock now in reach, `bow_depth` 1.2). Still: the bow short (head to knee) and
lost in 0 and 4; the quilting faint; the livery a sleeveless bib. Fixes: the bow 2.2 m, darker quilting, the livery
wider over the shoulders, a smaller brim, lighter skin.

### Critic round 3 (batch 1): 73.5 / 68 = 70.75

## Final (critic round 2)
Scores: R1 66.5 / 74 = 70.25; **R2 71 / 71 = 71, final**; R3 73.5 / 68 = 70.75 (a plateau). No critical failure.
Restored to round 2 (bow 1.95 m, the fist in front of the left hip, `bow_depth` 1.2). Civ 5-14 % (mean 11).

Open points:
- the bow short and lost against the leg in 0 and 7 (2.2 m in round 3 helped 2-6 only);
- the jack's quilting: a check reads as faint lumps or camouflage, it needs vertical channels;
- the livery a sleeveless bib: a real jacket with short sleeves and skirts;
- the face in the kettle hat's shadow.

Size check (2026-11-24, opaque area at 1x against the shipped DEFAULT, alpha>127 plus tint): see the table below; fitted by `unit_scale` (re-render only), animations re-rendered.
| Unit | before | unit_scale | after |
|---|---|---|---|
| Longbowman | 0.91 (0.85-0.95) | 1.04 | 0.98 (0.91-1.03) |
| Scout | 1.36 (1.26-1.54) | 0.88 | 1.06 (0.96-1.19, the spear) |
| Explorer | 1.13 (1.02-1.19) | 0.96 | 1.05 (0.95-1.11) |
| Chasquis Scout | 0.86 (0.77-0.98) | 1.07 | 0.98 (0.88-1.11) |
