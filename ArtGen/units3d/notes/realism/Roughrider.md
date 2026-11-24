# Roughrider (realism pass: a League NumPy unit remade in units3d)

## Design in words

**Who.** A trooper of the 1st US Volunteer Cavalry ("Rough Riders"),
Cuba 1898, made as a tabletop miniature.

**Must read at 1x:**
1. The drab slouch (campaign) hat: a creased felt crown, a broad brim, a
   dark band (new hat league_riders 'slouch_hat').
2. The dark blue flannel shirt (the civ colour, as the shipped sprite), a
   pale neckerchief, the broad Mills cartridge belt.
3. The Krag carbine held upright from the thigh (the 'cavalry' seat).
4. A bay on the black McClellan saddle, the folded army blanket under it
   (civ, with an orange edge stripe), a blanket roll, reins to the hand,
   stirrups.

**Identity from the shipped NumPy sprite** (art-base `league_mounted.py`):
drab slouch hat, civ shirt, neckerchief, cartridge belt, brown trousers
and leggings, the carbine up from the thigh; a bay in the McClellan saddle
on a civ blanket with an orange stripe.

**Civ colour:** the shirt and the saddle blanket. Shipped 15.9 %; the
closed Cossack final ~15.5 %.

**Entries:** league_riders.ENTRIES; calibration from the art-base sheet.

**Stock neighbours:** Knight (stock) and the closed Cossack final.

## Rounds
### Round 1
First build: tint 18.2 %, pixels 1.01 against the NumPy sprite, specks
clean.
### Round 2 (the lead's size rule)
Sized to the stock Cavalry (pixels 0.78 at FIT_SCALE 1.0): FIT_SCALE 1.13.

## Round 1 critics: A 71, B 73, average 72.0 (no critical)
### Round 2 build (batch_numpymounted_3, 1ed7a4c7)
The carbine forward off the thigh (pose 'carbine_thigh', WEAPON_UP (0.3, 1.0,
0.42)), rifle scale 1.15; an army blue-grey blanket with a thin civ edge
(border_tint); the slouch hat's brim 0.2 and a pinched front; a big red
neckerchief (0.12), a broader Mills belt.

## Round 2 critics: A 79, B 83, average 81.0 (no critical)
### Round 3 build (the last; solo, 44a0c5c7)
One carry in every direction: the carbine muzzle forward, nearly level across
the pommel, the stock at the hip (WEAPON_UP (0.12, 1.0, 0.1), hand_r (-0.3,
-0.18, 0.26)); a lighter gunmetal (#585c64) and a longer barrel (1.1); the
level carbine widened the box: FIT_SCALE 1.07 (pixels vs the Cavalry 1.11 at
1.13). Sheet review/roughrider/solo3/final.png (+ _red): shipped, final,
Knight, Cavalry.

## Round 3 critics: A 80.25, B 85.5, average 82.9 (no critical)
## FINAL = round 3 (44a0c5c7), in the gate
review/roughrider/final.png (+ _red) = the solo3 sheet (shipped, final,
Knight, Cavalry); copies in ArtGen/units3d/reviews/realism/roughrider/.
Later pass: in N and NE the carbine still angles up over the shoulder and in S
it foreshortens to a stub: bring them to the level, muzzle-forward carry of
E, W, SW, SE and NW.
