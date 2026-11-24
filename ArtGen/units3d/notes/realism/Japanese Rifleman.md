# Japanese Rifleman: realism design (2026-11-23)

MDJ unit (the mod's 'japaneseRM'), remade in units3d. No catalog entry on this branch ('units3d_only').

## The real model
An infantryman of the Imperial Japanese Army, 1937-45: the **Type 90 steel helmet** under a cloth cover (a high
round dome, a short flared brim; mdj_kit 'type90'), a khaki tunic with a stand collar, breeches and **puttees**
(wraps), ankle boots, leather webbing crossed on the chest, a field pack with a rolled blanket; the **Arisaka
Type 38** (1.28 m) with the long Type 30 bayonet, carried at the port.

## What must read at 1x
1. The long rifle and bayonet across the chest (the longest rifle in the roster);
2. the round helmet in the civ colour;
3. khaki figure with wrapped lower legs and a pack.

## Pose
'port_arms': the rifle diagonal across the chest, muzzle up at the left shoulder.

## Civ colour
The helmet cover and the pack. Stock Infantry 25% (22-28), Rifleman 35%; shipped 1%. Target 12-25%.

## Neighbours and size
Critic sheet: shipped, new, Samurai, Rifleman. Size counterpart: the Rifleman (a modern gunner).

## Build rounds (PC2, numpy-mdj-foot, 2026-11-24)
- The shipped NumPy sprite: a field cap with a civ band, a pack with a civ horseshoe blanket roll, the rifle at the
  port. Kept the Type 90 helmet (the civ cover) and took the NumPy's civ blanket roll for the pack.
- Port arms hid the rifle at 1x: now 'ready', both hands (grip2 -0.3), the rifle up and across to his left
  (weapon_dir 0.45, -0.15, 0.88) so it reads as a diagonal in all 8 directions, never pointing at the camera.
- `nmf_arisaka` (nmf_kit.py): full-length stock, dark barrel, a broad bright Type 30 blade (0.026 thick: thinner
  ones broke into specks edge-on in dir 1) with a dark hilt. `nmf_ija_pack` at size 0.85 (1.1 read as a block).
- unit_scale 1.15 (sized against the stock Rifleman).
- a1 numbers: tint 14.2 % (NumPy 11.4); opaque area 0.99 of the stock Rifleman, 1.23 of the NumPy sprite; specks clean.

## Critic round a1: A 85.3 / B 70.5 = 77.9
a2 fixes: `nmf_type90` (khaki cover, cord net, the civ only on the cover's band, raised above the brow: band_z 0.05);
civ collar tabs (neck band) and a bigger civ blanket roll keep the civ near 11 %; cartridge pouches, a canteen and a
rear pouch on the belt (`nmf_pouch`, `nmf_canteen`); more grime on the khaki; the rifle out further to his left
(weapon_dir 0.7, -0.2, 0.68) so the bayonet clears the head in dir 1.

## Critic round a2: A 77.5 / B 71.5 = 74.5 (A: critical, the rifle unreadable in dir 1, nearly in dir 8)
a3 fixes (the last round): the rifle slanted across the body with a large upward component (weapon_dir 0.42,
-0.32, 0.85), so it is never end-on: full length in dirs 1 and 8; the helmet all khaki, no net (the band read as a
visor); civ on the pack's top flap, the blanket roll and the collar tabs; a bigger dark mess tin on the pack.

## FINAL: a3, A 85.8 / B 75 = 80.4 (rounds: a1 77.9, a2 74.5, a3 80.4); no critical open
Final sheet: reviews/realism/japanese_rifleman/final(_red).png (shipped NumPy, final, Rifleman, Infantry).
Later pass: a blanket roll or greatcoat on the pack; the khaki body is plain (texture, folds).
