# Rifleman: realism design (PC2, realism/infantry)

## The real-world model
A line rifleman of the 1860s-70s (the French infantry of 1870, the American Civil War): a kepi, a dark blue tunic with short skirts, the trousers of the regiment (the French pantalon garance), a black leather waist belt and cartridge box, a canvas haversack, white gaiters; a long rifle with its bayonet.

## The features that must read at 1x
1. the rifle and long bayonet at the port, the bayonet above the head line; 2. the kepi; 3. the dark tunic over the civ trousers; 4. the white gaiters and haversack.

## The pose
Upright, the rifle at the port (butt at the right hip, muzzle at the left shoulder), the head turned a little.

## Civ colour
Civ on the kepi, the trousers and the cuffs (the regiment's facing colour). The shipped unit is 62 % (the whole uniform). Target: 15-30 % in every direction (foot stock 9-34 %), in 2-3 painted regions.

## Stock neighbours
Musketeer and Infantry

## Rounds
### Build rounds 1-5
- R1: a dark blue tunic with short skirts, civ kepi, civ trousers and civ cuffs, black belts, white gaiters: civ 25-32 % (the shipped one is 62 %).
- R2-5: the bayonet broke into 1 px specks at its point: a wider blade with a blunt point (`bayonet_w` 3.0) and a thicker barrel (`barrel_w` 1.5).
- R6-7: the specks were the bayonet's needle point: the widened blade now runs near parallel to a blunt point, round in section. Clean.

### Critic batch 1: 63.5 / 82 = 72.75 (A: civ critical, the whole trousers)

### Round 2 (fixes)
- Sky-grey trousers (no civ); civ on the kepi's crown over a navy band (as the French kepi's red crown), flat civ cuffs and collar; the kepi taller, sloped 14 degrees, a broad peak (new kepi options `band`, `slope`, `peak`, `tall`); the tunic a lighter navy, a brass belt plate; a thicker rifle (`stock_w` 1.5, `barrel_w` 1.7). Civ 8-12 %, mean 10 %. Brass buttons not done (no button option).

### Critic batch 2: 75 / 85 = 80.0

### Round 3 (the last; fixes)
- The kepi taller (1.6) and tipped 20 degrees over the peak; the rifle across the body (gun_dir 0.7, -0.08, 0.7) so dir 0 sees its length. Civ 8-12 %, mean 10.5 %.

### Critic batch 3: R3 averaged 78.75 (the rifle end-on in dir 0, a sliver in dirs 7-8: reads as unarmed)

## FINAL: R2 80.0, restored (scoped: REALISM['Rifleman'] only; R3's values were kepi tall 1.6 / slope 20 and gun_dir (0.7, -0.08, 0.7))

Rounds: R1 72.75, **R2 80.0**, R3 78.75. Cap reached (3 critic rounds). Pictures: reviews/realism/rifleman/final(_red).png (shipped, final, two stock neighbours); the finals gate re-renders it at HEAD.

Later pass:
- the rifle end-on in dirs 0 and 7
