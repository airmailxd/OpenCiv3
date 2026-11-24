# Explorer: realism design (2026-11-23)

## The real-world model

Civ3's Explorer is a Renaissance pathfinder. The brief: no armour. Here he is an explorer of about 1600 (Raleigh's
and Champlain's men, the Dutch and English voyages):

- a tall felt **capotain** with a band and buckle and a feather (the civ colour);
- a buff leather **jerkin**, sleeveless, over a doublet with long sleeves (the civ colour); a civ sash;
- dark hose and tall leather boots;
- an open **map** in his left hand, a brass **spyglass** (from 1608) in his right; a rapier at the left hip.

The Civ3 original: a steel morion with a civ plume, a steel breastplate over civ puffed sleeves, slate breeches,
pale boots, a map in hand, a rapier. The morion and breastplate go (no armour); the capotain, jerkin and spyglass
say "explorer", not "soldier".

## The features that must read at 1x

1. The tall **hat** with its brim.
2. The pale **map** held open in front of him.
3. The **spyglass** raised in his right hand (brass, bright).
4. The civ **sleeves** against the buff jerkin; the tall boots.

## The pose

Weight on one leg, torso upright, head turned a little toward the map; the map at chest height in the left hand,
the spyglass raised in the right, forward and out.

## Civ colour

The doublet's sleeves (and the torso under the jerkin's opening), the sash, the hat's feather. Stock: the shipped
Explorer 19-23 % (mean 21). Aim 15-25 %.

## Stock neighbours

Archer and Spearman (shipped).

## Shared-code options this unit uses (off by default)

- `riders.HEADGEAR['capotain']` (new), `props.spyglass` (new).

## Rounds

### Build rounds 1-2
- The capotain and the civ sleeves under the buff jerkin read; the map and spyglass are small. Civ 13-19 % (mean 17).

### Critic round 1 (batch 1): 58.5 / 61 = 59.75
Both: identity weak at 1x (a gentleman with a stick and a card); the hat reads as a derby or top hat; the face dark.
Fixes: a map half again as large, the spyglass thicker (scale 2), the hat's crown taller and its brim wider, a
bigger feather, tan boots, lighter skin and beard.

### Critic round 2 (batch 1): 64.5 / 68.5 = 66.5
Landed: the capotain, the bigger map and feather. Still: the spyglass a cane; the tan boots read as bare shins; the
face dark. Fixes: the spyglass shorter and raised toward the eye, dark boots against grey hose, lighter face.

### Critic round 3 (batch 1): 67 / 60 = 63.5

## Final (critic round 2)
Scores: R1 58.5 / 61 = 59.75; **R2 64.5 / 68.5 = 66.5, final**; R3 67 / 60 = 63.5. Restored to round 2. Civ 12-19 %
(mean 16). The brief's "no armour" made the morion and breastplate go; the identity now rests on the hat, the map
and the spyglass, which are small at 1x.

Open points:
- the spyglass reads as a cane or baton: raised to the eye, segmented, a wide eyepiece;
- the map in one hand reads as a card: open in both hands, tilted to the camera (needs a two-handed map hold);
- the hat still reads as a 19th-century top hat to one critic: taper the crown;
- the rapier hangs far out to the ground: closer to the leg, a swept hilt;
- tall boots (round 3's dark boots read better than the tan ones);
- the face dark under the brim.

Size check (2026-11-24, opaque area at 1x against the shipped DEFAULT, alpha>127 plus tint): see the table below; fitted by `unit_scale` (re-render only), animations re-rendered.
| Unit | before | unit_scale | after |
|---|---|---|---|
| Longbowman | 0.91 (0.85-0.95) | 1.04 | 0.98 (0.91-1.03) |
| Scout | 1.36 (1.26-1.54) | 0.88 | 1.06 (0.96-1.19, the spear) |
| Explorer | 1.13 (1.02-1.19) | 0.96 | 1.05 (0.95-1.11) |
| Chasquis Scout | 0.86 (0.77-0.98) | 1.07 | 0.98 (0.88-1.11) |
