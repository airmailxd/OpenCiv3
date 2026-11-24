# Carrier (realism pass): design

The Carrier is a Nimitz-class nuclear carrier (1975 on): a 333 x 77 m
flight deck on a 317 x 41 m hull that flares out to carry it, the angled
deck 9 deg to port, the island to starboard just aft of midships, four
deck-edge elevators (three starboard, one port aft), the air wing parked
round the deck edges. Neighbours (stock): Battleship and Destroyer.

## Baseline (as shipped)
Tint (tint.py, DEFAULT): 13.0 / 11.8 / 12.5 / 11.8 / 11.5 / 11.5 / 13.0 /
13.0 % (mean 12.3). Values (1x, opaque non-tint): mean 95, p10 66, p50 91,
p90 143, std 32, upper 104 / lower 91. Footprint 11241 px (2x, alpha>127,
mean per direction). specks.py: dir 4 has a 1 px speck.

## What must read at 1x
The flat dark flight deck as the one big shape with the angled deck's
white edge lines and dark landing strip; the island to starboard with a
mast; parked aircraft as aircraft; the grey hull flaring out under the
deck; the hull number on the bow.

## Civ colour (stock 12.3 %)
The parked air wing (fuselage, wings, tailplanes; canopies dark, fins
grey), as Civ3 marks its carrier, and a band round the island.

## Rounds
New builder ships.carrier_nimitz in its own block (before
_steel_builder); BUILDERS['Carrier'] set after the dict.
- Round 1: the Nimitz (L 3.4, flared hull, angled-deck plate, island,
  14 parked jets at 1.35x, a Hawkeye). Tint 5.9 % (jets too small), deck a
  step light (values mean 105), the SPS-49 array floated off its post.
- Round 2: jets 1.9x in a herringbone along both edges (12 + one on the
  bow catapult + one aft), deck #3c3f43, the array on a taller post,
  fit_scale 1.03. Tint 11.9 %, footprint 11243.
- Round 3: island 25 % bigger (four decks, bridge windows, civ band,
  "68" on its side, a taller mast; the SPS-48 seated on the top deck);
  jets moved inboard (their tailplanes over the edge speckled).
- Round 4 (GPU, batch 1): the Hawkeye's fins thicker. Tint per direction:
  11.6 / 13.5 / 13.4 / 11.1 / 11.4 / 12.8 / 11.7 / 10.6 % (mean 12.0;
  stock 12.3). Values: mean 97, p10 48, p50 94, p90 154, std 41, upper
  100 / lower 94 (stock 95, 104 / 91). ppm 87.01; footprint 11172 (stock
  11241, -0.6 %). specks.py: dir 4 one 1 px anti-aliased edge pixel on
  the deck's top edge (alpha 130 beside 121; the shipped sheet has one in
  the same direction), otherwise clean.

## Batch critic round 1: 72.0 (A 72.5, B 71.5), no critical failure
Shared: civ poured over whole structures (every jet a civ blob); flat
one-value shading; small parts vanish at 1x. Carrier: grey airframes with
civ tails only, a civ island band and deck-edge stripe; a smaller, greyer
rotodome (the brightest blob); hull depth (hangar openings, sponsons, a
shadowed gallery); elevators flush with supports (loose slabs in 1/8); a
taller island, mast and radar; the bow catapult cleared.

- Batch 2 build rounds (b2a-b2d): jets grey (#8a8e92) with civ fins and
  tailplanes; the flight deck's edge faces civ plus a 0.03 civ stripe
  painted inside the edge all round; the island band deeper; the
  rotodome r 0.03 in #8c8e8a; sponsons under the angled deck and the
  starboard gallery, eleven hangar openings a side, the gallery band
  #2e3236; elevators flush (top at the deck), dark gap lines, a sponson
  and two struts under each; island levels 0.115/0.095/0.07/0.045 tall,
  the mast 0.26, a radar platform, bigger SPS-48/49; the catapult jet
  gone; the waterline a dark wet line with foam only at the bow;
  fit_scale 1.01.
  Sent to batch 2 (GPU, b2d): tint 11.8 / 11.3 / 13.9 / 13.2 / 12.6 /
  11.4 / 12.7 / 11.9 % (mean 12.3; stock 12.3). Values: mean 101, p10 43,
  p50 95, p90 173, std 46, upper 105 / lower 99. Footprint 11210 (stock
  11241, -0.3 %). specks.py: clean.

## Batch critic round 2: 75.6 (A 79.75, fresh 71.5), no critical failure
A: all batch-1 fixes landed. Shared: civ in long thin lines (the deck
outline) reads as UI outlines at 1x; use painted areas. Carrier: drop the
deck-edge outline; keep the island band and jet tails, add panels; the
hull too shallow in 4/8 (~2x taller, hangar openings and sponsons in
it); a stray white line right of the island in 4; jets a step greyer.

- Batch 3 build rounds (b3a-b3c): D and the flight deck 0.17 -> 0.3 (the
  hull side about twice as tall, flare 0.45, the gallery band above 80 %);
  the deck-edge civ faces and stripe gone; civ painted areas instead:
  the four elevator tops, a panel under the bow number, a fantail panel,
  the sponsons' outer faces, a band above the boot topping; the island's
  white edge and foul lines removed; jets #74787c; fit_scale 0.99.
  Sent to batch 3 (GPU, b3c): tint 10.2 / 10.4 / 12.3 / 12.5 / 11.4 / 9.4
  / 10.2 / 11.0 % (mean 10.9; stock 12.3). Values: mean 95, p10 40, p50
  93, p90 158, std 42, upper 103 / lower 89 (stock 95, 104 / 91).
  Footprint 11232 (stock 11241, -0.1 %). specks.py: dir 3 one 1 px
  anti-aliased edge pixel (alpha 142 over 126, diagonal to 188), no
  detached part.

## Batch critic round 3: 85.4 (continuity 86.25, fresh 84.5), PASS, no critical failure

## FINAL = batch round 3 (b3c, code at 20470727), 85.4, PASS
Rounds: batch 1 72.0, batch 2 75.6, batch 3 85.4 (the best). Final
sheets: reviews/realism/carrier/final.png and final_red.png (shipped,
final, Battleship and Destroyer). Tint 10.9 % (stock 12.3), footprint
11232 (stock 11241).

Later pass (the critics' last open points):
- clear the jets off the bow catapults (dir 1);
- calm the civ squares on the hull side (4, 8);
- raise the island and mast ~25 % clear of the jet rows.

Finals gate (GPU re-render at HEAD, scratch/naval-carriers/gate.sh): PASS; diff against final.png mean 9.9e-09, max 0.008 (red the same); specks.py: dir 3 the one 1 px anti-aliased edge pixel the final also has, no detached part.
