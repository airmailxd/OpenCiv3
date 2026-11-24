# Oda Arquebusier: realism design (2026-11-23)

MDJ unit, remade in units3d: a teppo ashigaru of Oda Nobunaga's gunners (Nagashino, 1575).

## The real model
A **tall pointed jingasa** of lacquered iron with the mon (civ) on its front; a black **okegawa-do** and a short
kusazuri laced in grey over a kosode whose sleeves show in the civ colour; dark kobakama and suneate, waraji;
a katana at the hip; the **tanegashima**: a slim octagonal iron barrel on a full-length oak stock with the short
Japanese cheek butt, a brass lock with its serpentine on the right, brass pins, the slow match coiled on it.

## What must read at 1x
1. The tall pointed jingasa (a peaked cone, unlike the Ashigaru's flat one);
2. the long gun upright at his side, its barrel dark against the light stock;
3. civ sleeves either side of a black cuirass.

## Pose
'ready': the gun upright in the right fist out at his side, muzzle up, a little forward; the left hand at his
side. Its animations are the gun template's (brought to the cheek, fired, the smoke).

## Civ colour
The kosode's sleeves and the jingasa's mon. Stock Musketman 33% (28-37); shipped 19% (16-24). Target 15-30%.

## Neighbours and size
Critic sheet: shipped, new, Samurai, Musketman. Size counterpart: the Musketman.

## Build rounds (PC2, numpy-mdj-foot, 2026-11-24)
- Tint now 'top', 'hat', 'lace' (the lacing was grey: civ was 12 %). unit_scale 1.18 (sized against the Musketman).
- a1 numbers: tint 17.1 % (NumPy 35.3); opaque area 0.93 of the stock Musketman, 1.39 of the NumPy sprite; specks clean.

## Critic round a1: A 70.5 / B 56.5 = 63.5 (critical, both: the gun read as a staff)
a2 fixes: `nmf_matchlock`: a wide dark stock with a deep butt, a thick dark octagonal barrel with a swelled muzzle,
the brass lock, serpentine and pan at the hand, a pale match cord coiled at the wrist; carried on the diagonal in
both hands (ready, two_hands, grip2 -0.32), not upright. Tabi grey (#8a8478). The sleeves are no longer civ: civ is
the lacing, the jingasa's mon and a sashimono (0.27 x 0.62).

## Critic round a2: A 84.75 / B 70.5 = 77.6 (gun critical closed by both)
a3 fixes (the last round): the jingasa set higher (brow_drop 0.06) so the face shows in dirs 1-3; the do and lacquer
in a lighter brown; a bright polished flat along the top of the barrel; the gun slanted more across the chest
(weapon_dir 0.42, -0.32, 0.85) so it no longer stands upright in dirs 3 and 7.

## FINAL: a3, A 86.5 / B 75.5 = 81.0 (rounds: a1 63.5, a2 77.6, a3 81.0); no critical open
Final sheet: reviews/realism/oda_arquebusier/final(_red).png (shipped NumPy, final, Musketman, Samurai).
Later pass: the sashimono is edge-on in dir 3; turn it a little (its yaw).
