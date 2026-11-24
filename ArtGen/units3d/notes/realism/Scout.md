# Scout: realism design (2026-11-23)

## The real-world model

Civ3's Scout is an ancient pathfinder with no armour. Here he is a Greek traveller-scout of about 400 BC (the
hemerodromoi and the light men sent ahead of an army):

- a wide-brimmed felt **petasos**, the traveller's hat, on chin cords;
- a short **tunic** (the civ colour) and a brown wool **chlamys** (short cloak) on his back;
- laced boots (endromides);
- a light **spear** as a walking staff, a **water gourd**, a leather satchel at the hip on a strap.

The Civ3 original: bald with a ponytail, bare chest with a strap and small pack, civ armbands and loincloth, baggy
tan leggings, boots, a gourd in hand. The gourd and satchel stay; the hat, cloak and spear make him a traveller by
shape.

## The features that must read at 1x

1. The **petasos**'s wide brim.
2. The **spear** upright in his right hand, longer than he is.
3. The **cloak** on his back (from behind and the sides).
4. The **gourd** hanging from his left fist.

## The pose

Standing at ease, weight on one leg; torso upright; the spear planted at his right, the gourd at his left.

## Civ colour

The tunic and its skirt, the hat's band. Stock: the shipped Scout 7-16 % (mean 12), Explorer 19-23. Aim 12-22 %.

## Stock neighbours

Archer and Spearman (shipped).

## Shared-code options this unit uses (off by default)

- `riders.HEADGEAR['petasos']` (new).

## Rounds

### Build rounds 1-2
- The petasos and the spear make the traveller; the gourd reads. The cloak hangs as a board. Civ 6-25 % (mean 15).

### Critic round 1 (batch 1): 71 / 68 = 69.5
Both: the cloak a flat slab with pale edge streaks; no satchel seen; the face lost under the brim. Fixes: the cloak
flared and wrapped, a dark lining, an undyed tunic border, the satchel larger at the left hip, lighter skin.

### Critic round 2 (batch 1): 72.5 / 66 = 69.25 (fresh: critical, the whole tunic civ, 35-40 % from the front)
The cloak still a flat board with pale edges. Fixes: the cloak off; the tunic undyed wool with a civ hem border,
civ sleeve edges and a civ sash; the hat band civ.

### Critic round 3 (batch 1): 72 / 64 = 68
Without the cloak and with an undyed tunic he read as a modern safari scout (khaki shirt, shorts).

## Final (critic round 1)
Scores: **R1 71 / 68 = 69.5, final**; R2 72.5 / 66 = 69.25; R3 72 / 64 = 68 (a plateau). Restored to round 1:
the civ tunic, the cloak. Civ 6-25 % (mean 15). One fresh critic (round 2) called the whole civ tunic too much.

Open points:
- the cloak: a flat board with pale edges; it needs a draped chlamys pinned at one shoulder (a cloth-sim or a new
  pinned-cloak garment);
- the civ tunic over 30 % from the front; an undyed tunic with a border read as safari khaki: a mid-tone undyed wool
  with a narrow civ border, or a civ cloak once a draped one exists;
- the petasos reads as a bush hat: lower and flatter;
- the face under the brim.

Size check (2026-11-24, opaque area at 1x against the shipped DEFAULT, alpha>127 plus tint): see the table below; fitted by `unit_scale` (re-render only), animations re-rendered.
| Unit | before | unit_scale | after |
|---|---|---|---|
| Longbowman | 0.91 (0.85-0.95) | 1.04 | 0.98 (0.91-1.03) |
| Scout | 1.36 (1.26-1.54) | 0.88 | 1.06 (0.96-1.19, the spear) |
| Explorer | 1.13 (1.02-1.19) | 0.96 | 1.05 (0.95-1.11) |
| Chasquis Scout | 0.86 (0.77-0.98) | 1.07 | 0.98 (0.88-1.11) |
