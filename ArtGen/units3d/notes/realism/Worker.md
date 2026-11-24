# Worker: realism design (2026-11-23)

## The real-world model

A free labourer of the ancient Mediterranean (the diggers and road builders on Egyptian tomb paintings and
Roman reliefs, the road gangs of the Via Appia): lean and wiry, working bare-chested in the sun.

- an **iron-shod spade**: an ash haft of about 1.1-1.2 m and a broad iron blade on a socket (the Roman
  pala), the tool he is named by;
- a **straw sun hat** with a brim (the field hat of Greek and Roman farm hands);
- a **linen loincloth / short apron** on a cord belt, a cloth sash over one shoulder, leg wraps, bare feet.

Civ3's original: slim and wiry, a straw hat, a civ sash over one shoulder and a civ apron, a shovel held
diagonally across the body.

## The features that must read at 1x

1. The **spade**, its pale iron blade clear of the body in all 8 directions (about 2 px or wider, a bright
   edge against a dark haft): the unit's identity.
2. The **straw hat**'s pale disc over the head.
3. The **bare wiry torso** with the civ sash and apron.

## The pose

Carrying the spade at the ready in both hands, diagonal across the body, the blade up and out past his
right shoulder (as the original carries it, so it shows in every direction); the torso upright, a weight
shift through the legs.

## Civ colour

Stock: the shipped Worker 14-29 % (mean 23), Settler 18-29 (22), Scout 14-29 (22). Aim 15-28 %. Regions: the
**sash**, the **apron**, and the **hat band**.

## Stock neighbours

Settler and Scout (the non-combat foot units he stands beside).

## Rounds

### Build rounds 1-2
- R1: the 'ready' stance, the shovel up past the right shoulder in both fists: it read in every direction, but thin.
- R2: a new prop, props.spade (a broad dished iron blade with a bright edge, a socket, a T-grip), held across the
  body port-wise. Civ 14-32 % (mean 26).

### Critic round 1 (batch civil 1): 72 / 76 = 74
Both: the blade edge-on in 0 and 7 reads as a glaive; brandished overhead in 4-6 like a weapon. Also: a flat apron.
Fix: the spade carried on the right shoulder, the blade up and back with its face to the sky.

### Critic round 2 (batch civil 1): 78 / 77 = 77.5
Landed: the shoulder carry (no longer brandished), the broad blade face in 0-2. Still: in 3-7 the blade edge-on or
hidden behind the hat; the fist out at arm's length in 3 and 7; a flat apron and flat skin. Fix: the haft out over
the shoulder's point, the face turned up and out, the fist in closer.

### Critic round 3 (batch civil 1): 80 / 75 = 77.5
Landed: the shoulder carry and the grip; the blade face in 1-3 and 5. Still: edge-on in 7; over the head in 4-5
(the fresh critic); a flat apron; thin forearms and calves.

## Final (round 3)
Scores: R1 72 / 76 = 74; R2 78 / 77 = 77.5; **R3 80 / 75 = 77.5, final** (tied with R2; R3 kept, the continuity
critic's fixes landed in it). Cap of three batch rounds; no critical failure; not 85. Civ 15-36 % (mean 27).
Open points: the blade edge-on in direction 7; pleats or folds and value in the civ apron; thin forearms and
calves (more muscle mass); the blade over the head in 4-5 for one critic.

## Animations
The Worker's own actions (anim.WORKER) replaced the DEFAULT flic with their idle carry and the old shovel: the entry
now keeps its still for DEFAULT ('still_default') and gives the actions the spade ('anim_tool'). Checked at 2x:
run, mine, road, death, capture.

## Shared code added by the civil family (all off by default; no default changed)
- props.spade (new builder, registered as 'spade'): a Roman pala, a broad dished blade in 'forged_iron' (a new
  matte iron material key, metallic 0.45: the mirror 'iron' rendered black where it caught the sky).
- props.kama 'blade' (scales the blade alone), props.torch 'flame', props.backpack 'cords' (vertical lashings and a
  patch; tried on the Settler and dropped).
- garments.skirt 'troughs' (n, depth, seed): a few deep irregular troughs with soft ridges (heavy wool) laid over
  the folds; units entry keys 'skirt_troughs', 'skirt_folds', 'skirt_flare' pass to it.
- garments.shackles 'thick'; units entry key 'shackle_thick'.
- units entry keys 'cape_drape' (the cape hung by cloth simulation with the skirts), 'ankle_chain' (props.shackles
  between the ankles, on the left calf).
- units.equip imports yoroi when a prop's builder is not in props.BUILDERS (katana_drawn, katana_saya).
- anim.unit_actions: entry keys 'still_default' (the flics playing DEFAULT keep the built still, not the Worker's
  animated idle with its own carry) and 'anim_tool' (a prop spec in place of the Worker's SHOVEL in its actions).
