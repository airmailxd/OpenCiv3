# Teutonic Knight (realism pass: a League NumPy unit remade in units3d)

## Design in words

**Who.** A knight-brother of the Teutonic Order, 13th century (the
Baltic crusades), made as a tabletop miniature.

**Must read at 1x:**
1. The Order's white: a white mantle over a white surcoat with the black
   cross front and back.
2. The great helm (the closed Knight's helm) and mail.
3. The heater shield with the black cross, held on the left forearm; the
   lance upright at rest with a pennon.
4. A barded destrier in a long white trapper with black crosses.

**Identity from the shipped NumPy sprite** (realism-pc1/art-base,
`league_mounted.py`, archetype `league_horse_rider`): white mantle and
surcoat with the black cross, Topfhelm, white heater with the black cross,
lance with a civ pennon; a white caparison with black crosses and a broad
civ hem.

**Civ colour, accents only (the lead):** the shield's field (behind the
black cross), the trapper's border band and the lance pennon. The white
mantle and black cross stay the Order's. Shipped 21.9 %; the accents-only
rule sets a lower target.

**Built on:** the closed Knight final's code (its remaster entry, seat
'knight_rest', destrier shape and trapper), re-dressed: TACK_OVERRIDES
cap_field white, no paly, black 'cap_cross' charges painted on the cloth
(cap_decal False), a civ border band (cap_band 0.02-0.2); remaster
chest_emblem cross front and back, a white cape (the mantle), the heater's
cross black ('shield_pale'), the lance pennon.

**Entries:** league_riders.ENTRIES (the League catalog is not on this
branch); calibration from the art-base sheet (cal_from_sheet.py).

**Stock neighbours:** Knight (stock) and the closed Cossack final.

## Rounds
### Round 1
White mantle 0.7 x 0.5 covered the horse and the crosses; pixels 1.34
against the NumPy sprite: FIT_SCALE 0.8; tint 3.4 %.
### Round 2
A smaller mantle (0.5 x 0.4), a wider civ border band, bigger charges; the
decal crosses did not show: painted on the cloth instead.
### Round 3 (the lead's size rule)
Sized to the stock Knight, not the NumPy sprite: FIT_SCALE 0.92.

## Round 1 critics: A 79.5, B 85, average 82.25 (no critical)
### Round 2 build (batch_numpymounted_3, 1ed7a4c7)
The lance angled out to the side (WEAPON_UP (0.5, 0.5, 0.75)), a bigger head;
the Order's cross on the mantle's back (cape_emblem); the civ border band
wider (0.02-0.28); the trapper closed over the chest (breast_cloth, offset
0.11).
The lance point and pennon tails broke into 1 px specks in 4 directions:
new props.lance option 'bold' (a rounder point, a 2 cm pennon with broader
tails; off by default): specks clean.

## Round 2 critics: A 86.5, B 85.5, average 86.0, PASS
## FINAL = round 2 (58b7ec66), in the gate
review/teutonic-knight/final.png (+ _red): shipped (art-base), final, Knight,
Keshik.
Later pass: longer trapper side skirts: no black-leg slits in SE and E.
