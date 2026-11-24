# Javelin Thrower: realism design (2026-11-23)

## The real-world model

Civ3's Javelin Thrower is the Maya unique unit. Here he is a Late Classic Maya warrior (the Bonampak murals, about
790 AD):

- a crown of upright **feathers** on a cloth band (civ band, civ and natural feathers);
- a sleeveless jacket of **quilted cotton** armour (pale);
- a **loincloth** (ex) with its front flap (civ), sandals, cotton knee bands;
- **javelins** (darts) with obsidian or flint heads, thrown by hand or with the atlatl;
- a small round **shield** with a feather fringe at its foot.

The Civ3 original: a civ hood with yellow feather tufts and a red mask, a civ top and loincloth, a bundle of
javelins planted at his left, one javelin low. The red mask goes (it read as a blob); the planted bundle becomes the
shield, so the throwing arm is free.

## The features that must read at 1x

1. The **javelin cocked back** over the right shoulder, the point forward and up: the thrower's silhouette.
2. The **feather crown**.
3. The round **shield** with its civ face and pale centre, the feather fringe below, on the left arm forward.
4. The pale quilted jacket and the civ loincloth.

## The pose

Weight on the right (rear) leg, the left foot forward; torso upright. The right hand back and up beside the head,
the javelin through it; the shield arm forward, the board turned to the front.

## Civ colour

The shield's face, the loincloth, the feather crown's band and alternate feathers. Stock: the shipped Javelin
Thrower 34-57 % (mean 44); the foot neighbours 16-26. Aim 15-30 %.

## Stock neighbours

Archer and Spearman (shipped).

## Shared-code options this unit uses (off by default)

- `props.chimalli(face_tint=True, rim=True)`: the civ face with a pale centre, a feather-work rim; `shield_back` key.

## Rounds

### Build rounds 1-2
- The feather crown, the fringed civ shield and the cocked javelin read in most directions. The vest read as a
  tank top: a quilted jacket (a cuirass shell with a fine check). Quetzal-green feathers. Civ 10-26 % (mean 18).

### Critic round 1 (batch 1): 63.5 / 67 = 65.25
Both: the javelin a stub in 0-1 (pointing at the camera), upright in 4-5; the jacket flat. Fixes: the javelin level
and across to the left, the hand behind the shoulder; stronger quilting and a red bound edge.

### Critic round 2 (batch 1): 64 / 64.5 = 64.25 (both: critical, the javelin unreadable in 0, 1, 5)
Fixes: the javelin canted up 30 degrees across to the left, the hand out and up past the headdress; the crown's
band raised off the eyes (`brow_drop` 0.06).

### Critic round 3 (batch 1): 67.5 / 58.5 = 63 (the critical failure lifted; dir 0 still a stub)

## Final (critic round 1)
Scores: **R1 63.5 / 67 = 65.25, final**; R2 64 / 64.5 = 64.25 (critical); R3 67.5 / 58.5 = 63. Restored to round 1.
Not 85; no critical failure was called at round 1, but both critics name the javelin in 0-1 as a stub.

Open points:
- the javelin foreshortened in direction 0 (and 1 at round 1): round 3's cant (up 30 degrees across to the left)
  fixed 1-7 and should be kept with the hand further out in 0;
- the jacket reads as a tube top: padded relief, shoulders, a visible bound edge;
- the feather crown is stiff rods like a brush: tapered, curved plumes of varied length.

Size check (2026-11-24): opaque area at 1x 1.04 of the shipped (0.87-1.16); no fit change.
