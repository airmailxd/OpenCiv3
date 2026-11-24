# Bowman: realism design (2026-11-23)

## The real-world model

Civ3's Bowman is Babylon's unique archer. Here he is a Neo-Assyrian / Neo-Babylonian archer of about 650 BC, as on
the Nineveh and Nimrud reliefs:

- a tall **pointed helmet** of iron with a bronze brow band and short ear flaps;
- a long, square, black **beard** and short hair under the helmet;
- a knee-length **tunic** with short sleeves (the civ colour), a dark fringed hem, a broad belt, a leather vest
  (the reliefs show scale or lamellar shirts on the better-armed);
- an angular **composite bow**, a quiver on the back on a strap, a short sword at the left hip;
- bare legs and laced boots.

The Civ3 original: a civ cap with a gold knob, a leather vest over a civ knee tunic, a quiver, leg wraps, the bow
held low. The cap becomes the pointed helmet: it is the Assyrian shape and it keeps him apart from the Archer
(Cretan, felt cap, exomis).

## The features that must read at 1x

1. The **bow** in a low forward ready hold: upright in the left fist at belt height, out at his left front, an arrow
   nocked and pointed at the ground ahead. It shows in every direction (the Archer's open point: a full draw hid
   the bow behind the body in some views and read as a crossbow).
2. The **pointed helmet**.
3. The long black **beard**.
4. The knee-length civ **tunic** under the dark vest; the quiver's fletching over the right shoulder.

## The pose

Weight on the right leg, the left foot a half step forward; torso upright, shoulders counter-rolled. The bow arm
down and forward (fist at the belt, out from the hip), the bow upright, a little canted out. The right fist at the
nock just behind the bow. Head level.

## Civ colour

The tunic's sleeves and skirt (the vest covers the chest). Stock: the shipped Bowman 21-32 % (mean 26), Archer 6-24
(16). Aim 15-25 %.

## Stock neighbours

Archer and Spearman (shipped).

## Shared-code options this unit uses (off by default)

- `units.equip`: `bow_ready` with `hand_l_at` / `elbow_l` places the bow fist from the shoulder (the low hold).
- `riders.HEADGEAR['assyrian_helm']` (new), in the new matte iron key `iron_matte`.

## Rounds

### Build rounds 1-2
- The pointed helmet, the vest over the civ tunic and the quiver read; the bow in the low hold shows in the side and
  back views, but is lost against the body in directions 0 and 4. The black beard read as a dark face: lighter.
  Civ 13-23 % (mean 19).

### Critic round 1 (batch 1): 76.5 / 81 = 78.75
Both: the bow vanishes in 0 and 4; the beard a dark blob; the short sword not seen. Fixes: the bow canted out
(`bow_up` x 0.45) and the fist further out, the draw elbow lower, a brown beard and lighter skin, a bigger sword.

### Critic round 2 (batch 1): 78 / 73.5 = 75.75
The right hand reached across for a nock out of reach (the bow fist out at the side, the bow's depth scaled 2.5x
by `bow_thick`): its forearm crossed the face. Fixed before this round: `bow_depth` 1.2 and the bow fist in front
of the left hip, the arrow across to the left. Landed: the bow clear in 2, 3, 6, 7; the sword. Still: the beard a
dark mask; the bow edge-on in 0 and 4. Fixes: a mid-brown beard, lighter skin, no ear flaps.

### Critic round 3 (batch 1): 78 / 69 = 73.5

## Final (critic round 1)
Scores: **R1 76.5 / 81 = 78.75, final**; R2 78 / 73.5 = 75.75; R3 78 / 69 = 73.5. Cap reached, no critical
failure, not 85. The entry is restored to round 1 (the bow fist out at his left, `bow_thick` 2.3 scaling the bow's
depth too). Civ 13-23 % (mean 19).

Open points:
- the bow edge-on and lost against the body in directions 0 and 4 (the low hold still crosses the body there);
- the face and beard a dark mass under the helmet (the brown beard of round 3 did not lift it at 1x);
- the vest and tunic flat: a fringed hem, a buckle, stitching on the vest;
- at round 1 the right hand reaches for a nock out of reach (`bow_depth` unset): round 2's fix (`bow_depth` 1.2, the
  fist in front of the left hip) put the hand on the nock but scored lower; a later pass should keep it.

Size check (2026-11-24): opaque area at 1x 0.97 of the shipped (0.88-1.08); no fit change.
