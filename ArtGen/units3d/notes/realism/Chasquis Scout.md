# Chasquis Scout: realism design (2026-11-23)

## The real-world model

Civ3's Chasquis Scout is the Inca unique unit: the chasqui, a relay runner of the Inca road system, about 1500.
The chroniclers (Cieza de León, Guaman Poma) describe:

- a **headdress of white feathers** that marked him from afar;
- a knee-length **unku** (tunic), with a band of tocapu (woven squares) at the waist;
- a **pututu**, the conch trumpet he blew to warn the next post, a **chuspa** (woven bag) at the hip, a quipu;
- for defence a **champi**, the star-headed mace, and a sling;
- sandals (usuta), cotton fringes below the knee.

The Civ3 original: a broad civ fan headdress, long black hair, bare chest, a civ wrap skirt with a gold belt, civ
bands at wrist, knee and ankle, a star mace low. The fan stays as his silhouette, in white feathers; the civ colour
moves to the unku.

## The features that must read at 1x

1. The **fan headdress** behind the head (white, a gold band).
2. The **star mace** raised at the ready in his right hand.
3. The civ **unku** with its dark waist band.
4. The pale **conch** in his left hand.

## The pose

Weight on the right leg, the left foot forward; torso upright. The mace at the ready (fist at the hip, head up and
out), the conch held at the waist.

## Civ colour

The unku (tunic and skirt). Stock: the shipped Chasquis Scout 12-24 % (mean 20). Aim 15-25 %.

## Stock neighbours

Archer and Spearman (shipped).

## Shared-code options this unit uses (off by default)

- `riders.fan_headdress(fan_tint=False, rim_key=...)`, `props.conch` (new).

## Rounds

### Build rounds 1-2
- The white fan, the civ unku and the raised star mace read. The fan's spokes read as a wire dish: more feathers.
  Civ 11-24 % (mean 18).

### Critic round 1 (batch 1): 68 / 61 = 64.5
Both: the fan a flat disc (a plate or halo), the unku a plain sack ending mid-thigh, the conch a white blob. Fixes:
the fan smaller, the unku to the knee with a cream hem and a cream neck edge (the dark tocapu waist band now shows:
band colours must be colour keys), the conch larger.

### Critic round 2 (batch 1): 68 / 71.5 = 69.75
Landed: the hems, waist band, conch. Still: the unku a plain civ sack; both arms raised behind the fan read as a
surrender in 5. Fixes: the unku's body checked (civ and cream tocapu squares), the mace lowered to a ready carry.

### Critic round 3 (batch 1): 68 / 59 = 63.5
The checked unku did not render (a pattern on a civ-tinted shell does not show); the lowered mace still read raised
in 5-7.

## Final (critic round 2)
Scores: R1 68 / 61 = 64.5; **R2 68 / 71.5 = 69.75, final**; R3 68 / 59 = 63.5. Restored to round 2. Civ 11-26 %
(mean 20).

Open points:
- the unku one flat civ field: a band of large tocapu squares across the chest (needs civ panels on untinted cloth:
  the pattern with 'tint' as its colour on an undyed unku);
- the fan reads as a disc, plate or visor: separate splayed feathers with depth;
- the conch reads as paper: a spiral shell with a pink lip;
- the mace raised behind the fan in 5-7 (reads as a surrender).

Size check (2026-11-24, opaque area at 1x against the shipped DEFAULT, alpha>127 plus tint): see the table below; fitted by `unit_scale` (re-render only), animations re-rendered.
| Unit | before | unit_scale | after |
|---|---|---|---|
| Longbowman | 0.91 (0.85-0.95) | 1.04 | 0.98 (0.91-1.03) |
| Scout | 1.36 (1.26-1.54) | 0.88 | 1.06 (0.96-1.19, the spear) |
| Explorer | 1.13 (1.02-1.19) | 0.96 | 1.05 (0.95-1.11) |
| Chasquis Scout | 0.86 (0.77-0.98) | 1.07 | 0.98 (0.88-1.11) |
