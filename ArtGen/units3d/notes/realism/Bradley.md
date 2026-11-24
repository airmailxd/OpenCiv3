# Bradley (League modern mod; realism pass, numpy-vehicles): design

Identity (NumPy model `light_ifv`): an M2A3 Bradley with BRAT reactive
tiles on its sides and nose, the 25 mm Bushmaster turret, the twin TOW box
on the left, the commander's independent viewer (CIV), smoke banks, a
stowage rack. Shipped: the NumPy sprite (art-base), tint 3.7 % mean.

## Real object
M2A3 (2000s): the M2 hull (6.55 m x 3.6 m over the armour, roof ~2.6 m),
BRAT tiles (two rows of square reactive tiles bolted over the side armour,
a row on the upper glacis), the CIV on a post at the turret's rear right,
the TOW box folded on the turret's left.

## Build
`armor_mod._bradley_a3`: `armor._bradley` (the closed Mech Infantry's
builder) with the Mech Infantry's config copied, plus: BRAT tiles (2 rows x
8 per side, bolted, a dark line between rows; 3 on the glacis), the CIV,
a civ panel on the TOW box's outer face. Own paint: a lighter tan with
softer brown and khaki patches (the Mech Infantry has hard dark ones), the
turret 0.8. fit_scale 0.93.

## What must read at 1x
1. The tiled sides (a grid of light tiles over the tracks): the A3's tell.
2. The small turret with the long 25 mm and the TOW box on its left.
3. The ramp, the tall slab hull.

## Civ colour (shipped 3.7 %; aim 3-5 %)
One tile column each side (both rows), the TOW box's outer face, the
turret-side and ramp chevrons and the glacis chevron (from _bradley).

## Rounds
- d1 (CPU draft): reads as an A3; 1.12x the shipped pixels; camo faint.
- d2: fit_scale 0.93 (pixels 0.97), harder camo; tint 2.7 %.
- d3: two civ tile columns a side, thicker antennas (specks). Tint 3.9 %.
- Critic round 1 (batch_numpyvehicles_1, GPU at 8041131b+): tint per
  direction 4.7 / 2.4 / 3.9 / 4.3 / 4.3 / 2.3 / 4.5 / 4.8 % (mean 3.9; shipped
  3.7); fitcheck bbox 1.02, pixels 0.97; specks clean.

## Critic round 1: 85.0 (A 84 / B 86), PASS. FINAL = B1 (8041131b)
Finals: reviews/realism/bradley/final.png (+_red): shipped, final, Modern
Armor, Mech Infantry. Size against stock (lead's rule): opaque area 0.99 of
the stock Mech Infantry, 0.98 of the stock Tank.
Later-pass notes: vary tile sizes and shading (the BRAT reads as a
spreadsheet); move the civ 2x2 block off the tiles onto a hull or turret
panel; more weight on the track band and sprocket.
