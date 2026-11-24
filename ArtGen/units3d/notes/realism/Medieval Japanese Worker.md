# Medieval Japanese Worker: realism design (2026-11-23)

MDJ unit, remade in units3d.

## The real model
A peasant labourer: the wide **sugegasa** sedge hat (civ, as the shipped one; mdj_kit 'kasa'), a short tan jacket
with civ armbands, dark knee breeches, bare shins, waraji; the **kuwa** (a broad hoe) on the right shoulder.

## What must read at 1x
1. The wide flat hat (the biggest civ shape);
2. the hoe's iron blade up behind the shoulder;
3. a lean figure in a short jacket and dark breeches.

## Pose
'ready' as the civil family's Worker final: the haft on the right shoulder, the blade up and back, the left hand
at the side. Animations: the Worker's (anim.WORKER_LIKE).

## Civ colour
The hat and the armbands. Shipped 4%; stock Worker 15% (12-19). Target 12-22%.

## Neighbours and size
Critic sheet: shipped, new, Samurai, Worker. Size counterpart: the Worker.

## Build rounds (PC2, numpy-mdj-foot, 2026-11-24)
- The tools.py hoe was tiny: `kuwa_broad` (nmf_kit.py), a broad iron blade on a head block, named apart from the
  other PC's coming `kuwa`.
- The civ upper-arm bands are gone (rule: no rolls on the upper arm). The whole civ kasa measured 30 %: now
  `nmf_kasa`, natural sedge with a civ cap over its crown (crown 0.66), plus a civ obi.
- unit_scale 1.08 (sized against the stock Worker).
- a1 numbers: tint 12.7 % (NumPy 23.5); opaque area 0.96 of the stock Worker, 1.00 of the NumPy sprite; specks clean.

## Critic round a1: A 72 / B 56.5 = 64.25 (critical, both: the blade clipped the brim and face in dirs 4-5)
a2 fixes: the kuwa over the right shoulder and out to the side behind him (weapon_dir -0.6, 0.7, 0.25), the blade
hanging clear of the brim in every direction; the blade bigger (0.3 x 0.32), thicker and dark iron with a bright
edge; `nmf_kasa` taller and conical (height 0.15) with a woven texture (rings and ribs), civ on the crown only;
long momohiki in a natural tone instead of black breeches.

## Critic round a2: A 78.3 / B 67 = 72.65 (clip critical closed; A: new critical, a different grip in each direction)
a3 fixes (the last round): one carry in all 8 directions: the right fist on the haft in front of the shoulder, the
haft back over the right shoulder (weapon_dir -0.38, 0.92, 0.12), the blade hanging beside and behind the shoulder,
close to the figure (haft 0.95); the civ crown smaller (0.4, the straw reads first); civ knee ties and a wider civ
obi.

## FINAL: a3, A 83.3 / B 70.3 = 76.8 (rounds: a1 64.25, a2 72.65, a3 76.8); no critical open
Final sheet: reviews/realism/medieval_japanese_worker/final(_red).png (shipped NumPy, final, Worker, Settler).
Later pass: the blade hides behind the hat in dir 1 and is a sliver in dir 5: angle the haft out; the blade's
shadow is detached.
