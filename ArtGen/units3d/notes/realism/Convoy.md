# Convoy (League modern mod; realism pass, numpy-vehicles): design

Identity (NumPy model `humvee`): an up-armoured M1151 Humvee: slotted
grille, brush guard, thick-glassed doors, a cargo deck with jerrycans, an
O-GPK gunner's shield with its windows and an M2, a gunner, a civ marker
panel on the hood. Shipped tint 1.4 %.

## Real object
M1151 (2000s): 4.9 m long, 2.3 m wide, roof ~1.9 m, wheelbase 3.3 m,
37-inch tyres (r ~0.47 m), the wide flat hood over the fenders with its
seven-slot grille, an armoured cab with near-vertical sides and small thick
windows in the four doors, a two-pane windscreen, the cargo deck behind;
the O-GPK: a three-facet front shield with ballistic windows and side wings
round the roof ring, the M2 forward.

## Build
`armor_mod._humvee`, built in metres (Metric, K units per metre): tyres
with beadlock rims and lugs, frame, axles, differentials; body tub, hood,
rear fenders, grille and lamps, bumper, winch, brush guard; cab with
windscreen, doors (outlines, windows, handles), mirrors; cargo deck with
jerrycans, ammo cans and a bedroll; O-GPK ring, shields, wings, the gunner
and the M2.

## What must read at 1x
1. The low wide Humvee body on four big tyres, the long flat hood.
2. The gunner's shield and the M2 on the roof.

## Civ colour (shipped 1.4 %; vehicles aim 3-5 %)
The hood marker panel, a panel low on each door, a band across the
shield's front facet.

## Rounds
- d1: pale, tyres with lugs read as cogs, whip antennas broke into specks.
- d2: darker paint and harder camo, no antennas, door and shield civ panels.
- d3: plain tyres with tread grooves, a shadowed underbody, smaller panes.
- Critic round 1: + a tailgate civ panel. Tint 2.9 / 3.2 / 2.9 / 2.4 / 3.2 /
  1.8 / 3.2 / 2.4 % (mean 2.7; shipped 1.4); fitcheck bbox 1.00, pixels 1.07;
  specks clean.

## Critic round 1: 85.0 (A 83 / B 87), PASS. FINAL = B1 (8041131b)
Finals: reviews/realism/convoy/final.png (+_red). Size against stock: 1.11
of the stock Mech Infantry, 1.22 of the stock Mobile SAM (inside the stock
armour span: Mech Infantry 1.06 and Panzer 1.30 of the Modern Armor; the
Convoy is 1.18), so unchanged.
Later-pass notes: wheels ~15 % bigger with tread; grille, headlights and a
winch bumper; a darker, slightly higher gunner's helmet; the hood civ
square thinned to a stripe (in red it can read as a medical cross).

## Fit fix (lead: a Humvee must not draw bigger than the IFV and the MBT)
fit_scale 0.89 (re-render only, nothing else changed). Opaque area vs the
stock Mech Infantry 1.11 -> 0.88 (0.87-0.89 per direction); vs the stock
Mobile SAM 1.22 -> 0.97; vs the NumPy sprite 1.07 -> 0.85. Specks clean; tint
2.7 % (unchanged). final.png / final_red.png rebuilt in both review folders.
