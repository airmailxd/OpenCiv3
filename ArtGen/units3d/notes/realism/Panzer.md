# Panzer (realism pass): design

The Panzer is a Panzer IV Ausf. H (1943-44): the long 75 mm KwK 40 L/48
with its double-baffle muzzle brake, the spaced armour skirts (Schürzen)
round the turret and along the hull sides, eight small road wheels in four
leaf-spring bogies. Built as a painted 1/56 tabletop miniature in the house
palette: Dunkelgelb (dark yellow, the house sand) with sprayed bands of
olive green and red-brown, the 1943-44 German three-tone scheme. Neighbours
for the comparison: Tank and Modern Armor (stock).

## Baseline (round 0, as shipped)
- A generic box tank: a big boxy turret with a short gun and a large muzzle
  brake (the brake on the short L/24 is wrong: it never had one), a tall
  slab-sided hull, 8 even road wheels, no skirts. Reads "tank", not
  "Panzer IV".
- Pale cream with a small grey-green mottle; reads as the Modern Armor's
  sibling at 1x.
- Civ colour: four "E" brackets on the hull (front, rear, sides), 0.9-3 %
  per direction (mean 2.2 %); the head-on and rear views lowest. Red crosses
  with white outlines read as medical crosses (a Balkenkreuz is black with a
  white outline).

## What must read at 1x
1. The long gun: a dark barrel reaching well past the nose (about a third
   of the hull length), a light top edge, and the double-baffle brake as a
   distinct fat knob with a light front ring. Out of the fit (no_fit) and
   shadowless, as the Tank's.
2. The slab-sided Schürzen silhouette: a wide box of thin plates round the
   turret's sides and rear (the cupola poking up at its back), and five
   hull-skirt plates per side over the track's top half, with dark gaps
   between plates and a rail with brackets. This is the Panzer IV H's
   signature and separates it from the round Sherman and the long, low
   Abrams.
3. The stepped front: a vertical driver's plate (visor on the left, ball MG
   on the right) set back over a low transmission deck and a sloped nose
   with a row of spare track links.
4. Running gear under the skirts: four leaf-spring bogies, each a close
   pair of small road wheels with a dark gap between pairs, the high front
   sprocket and the rear idler.

## Paint
- Base Dunkelgelb ~#c6b07c (the house sand, a little warmer than the
  Tank's #c4b68c).
- Sprayed bands: olive green ~#8a8a5a and red-brown ~#93684a, soft edged
  and stretched vertically (wavy bands, not blobs): a new optional camo
  stretch per layer in props._camo (default off: other units unchanged).
- The barrel dark (gun_olive) with the light top edge, the brake blackened
  with a light front ring (the house rule for guns).
- Separation: a dark shadow line where the hull meets the running gear,
  the skirt plates a touch darker at their lower edge (dust kept low).
- Weathering: the Tank's opt-in keys (dust low, chips, fade, streaks,
  exhaust soot at the rear).

## Markings and civ colour
- Stock class tint (tint.py, alpha>127, DEFAULT): Panzer 0.9-3.0 % (mean
  2.2), Tank 6.8-9.1 % (7.9), Modern Armor 0.2-4.1 % (3.3), Mech Infantry
  4.4-5.7 % (5.0), Mobile SAM 3.2-6.4 % (5.6), Radar Artillery 2.4-3.6 %
  (3.1). Aim: 2.5-5 %, even across directions.
- Civ regions (decal-like, never a panel):
  1. Three-digit turret numbers on both turret-skirt sides and on its
     rear, block numerals with a thin dark outline (the period practice:
     coloured numbers outlined).
  2. A small tactical sign on the driver's front plate (head-on views).
  3. A thin stripe along the top of the hull skirts? Only if the per-
     direction tint stays low.
- Not civ colour: the Balkenkreuz, black with a white outline, one on each
  middle hull-skirt plate and one on the rear plate (period-correct; the
  old red cross is gone).

## Construction notes
- Chassis gets optional keys (defaults keep every other vehicle as is):
  leaf-spring bogies (wheel pairs with a bracket and a leaf pack), an upper
  hull shorter than the lower one (so the transmission deck shows in front
  of the driver's plate).
- Turret: hexagonal in plan (cheeks angled in at the front), sloped sides,
  the mantlet a cast block with the coax and sight ports, the cupola at the
  rear centre, the stowage bin on the rear inside the skirt.
- Deck: the driver's and radio operator's hatches ahead of the turret, the
  engine deck's two louvred hatches, the big muffler across the rear plate.
- No antenna (it counts in the fit), no small deck clutter (orange specks
  at 1x).

## Rounds
Render times are Blender + compose inside run_locked (plus any wait for
the lock). Tint is tint.py's share per direction (compare columns 1-8).

- Round 1 (17 s, waited 35 s): the new model. Reads as a Panzer IV H at
  2x (skirts, long gun, brake), but the hull skirts reach below the wheel
  hubs: no running gear at all, a flat slab; the camo too faint to see;
  the numbers specks. Tint 0.5-1.6 %.
- Round 2 (17 s, waited 10 s): skirts end just over the wheel tops (the
  four wheel pairs show), plates hung with small individual tilts, camo
  denser and stretched into bands (olive #717548, red-brown #8a5a3c).
  Reads as the German three-tone at 1x. Turret skirt too far out (a
  turret sitting in a big open box). Tint 1.0-2.7 %.
- Round 3 (17 s, waited 60 s): turret skirt closer (0.36), one hull-skirt
  plate lost per side (left: the second from the rear, right: the fourth),
  so the hull side and the running gear show through; numbers bigger.
  The thick-stroked numerals closed up into white blocks with slits; "4"
  read as "9". Tint 1.3-3.7 %.
- Round 4 (17 s): number "532", a civ tactical sign on each front skirt
  plate, turret paint 0.8 of the hull's (a darker mass). Numerals still
  blocks. Tint 1.9-3.8 %.
- Round 5 (17 s): open numerals (width 0.64 h, stroke 0.19 h): "532"
  reads at 2x in every direction, never mirrored. Tint 1.9-3.8 %.
- Round 6 (17 s, waited 695 s for the lock): camo bands softened toward the
  house (olive #7f8052, red-brown #966546); bigger tactical signs front
  and on the front skirt plates, smaller ones on the rear. Sent to
  critic round 1. Tint per direction (columns 1-8): 2.5 / 2.6 / 2.5 /
  2.1 / 3.3 / 3.0 / 3.2 / 2.1 % (mean 2.7). ppm 74.99 (Tank 70.42).
  Tank re-rendered with the shared changes: identical to its final
  (mean abs diff 0.00001, max 1/255).

Shared changes (opt-in, defaults keep the other vehicles as they were):
props._camo takes an optional per-layer stretch; armor.chassis takes
'leaf' (leaf-spring bogies), 'upper_len' (an upper hull set back from
the nose) and 'wheel_face'; tracked() takes 'turret_tone' (a darker
'armor_turret' paint with the camo scaled the same); new marks: block
numerals (number), the Balkenkreuz, a rhomb tactical sign.

## Critic round 1: 68 (continuity 68, fresh 68), FAIL, no critical failure
Both: the skirts make a walled tub (an APC), the turret small inside a
railing-like frame, the gun a 1-px line with a dot brake at 1x; the camo
busy and pink. Fixes:
1. Hull skirts lowered: top at a third of the superstructure's height
   (skirt_top 0.35), bottom just over the wheels, spaced 0.07 off the
   hull on hanger brackets (one per plate) so the upper hull shows above;
   a dark seam across each plate, a bent corner on the rear plate, the
   lost plate kept; plate tilts down to a third (skirt_tilt 0.35: at
   full tilt the edges were jagged head-on). Hull wider and lower
   (W 1.04, H 0.2): wider than tall head-on and from the rear.
2. Turret 20 % bigger (0.72 x 0.7 x 0.25); its skirt solid 20 mm plates
   leaning in 9 deg with the turret, below the roof, no light top strip
   (it read as a railing).
3. Gun r 0.034 -> 0.05; the brake a wide octagonal block (1.7 x the
   barrel, 0.12 long); a bigger mantlet (0.32 x 0.18) with light top and
   side catches.
4. Camo calmer and nearer the house sand (olive #8c8a5c, earth #a07c58,
   lower frequency, wider bands).

- Round 7 (17 s, waited 85 s): all four in; camo almost gone (plain sand),
  skirt plates jagged head-on. Tint 2.1-3.3 %.
- Round 8 (17 s): tilts reduced, camo a step back up. Sent to critic
  round 2. Tint per direction (columns 1-8): 2.8 / 2.9 / 2.7 / 2.1 /
  3.3 / 3.3 / 3.3 / 2.1 % (mean 2.8). ppm 73.66. No shared code changed
  since round 6 (the Tank check there stands).

## Critic round 2: 70.75 (continuity 75.5, fresh 66), no critical failure
Both: the turret read as a Tiger I (a big vertical-sided box merging with
its skirts); the sprocket and idler hidden; a flat front; pale tracks;
the hull a washed-out, weightless cream box. Split on the skirt height
(kept, with more structure) and on the barrel colour (kept dark). Fixes:
1. Turret narrower (0.63) and tapered: sides sloped 0.3, the front plate
   sloped 0.28; the turret skirt flares out 6 deg with a clear gap; its
   brackets set low so they don't show over the top (they read as wire
   handles).
2. Skirt row shorter (-0.64 to 0.56): the sprocket, the idler and the
   track's top run show at both ends; wider gaps between the plates;
   the plates in the darker turret paint. Front: a bigger visor block,
   a bigger MG ball in a dark ring, the round final-drive housings at
   the nose's corners. Tracks thicker (0.04, a new opt-in chassis key
   'track_th') and darker (#26231f), with less dust.
3. Weight: base paint darker (#b49e6e, olive #828056, earth #967252),
   skirts darker, the skirt gap off the hull 0.05.
4. Brake: two separate chambers on a bare neck (a stepped tip at 1x),
   the rear chamber a step smaller; the barrel's root sleeve 1.7x.

- Round 9 (17 s, waited 115 s): all in; still a little pale; final-drive
  housings big pale blobs. Tint 1.9-2.8 %.
- Round 10 (17 s, waited 25 s): darker base, smaller darker final drives,
  numbers 0.175, larger skirt signs. Sent to critic round 3. Tint per
  direction (columns 1-8): 2.8 / 2.5 / 2.8 / 2.3 / 3.2 / 2.9 / 3.2 /
  2.3 % (mean 2.8). ppm 73.52.
  Tank re-rendered after the track_th key: identical to its final (mean
  abs diff 0.000004, max 1/255).

## Critic round 3: 74.5 (continuity 81, fresh 68), no critical failure
Both: the turret skirt read as an open bucket from above; the staggered
hull-skirt plates with dark gaps read as crates at 1x; the front details
lost at 1x, the hull faces pale. Fixes:
1. Turret skirt upright (no flare), just under the roof, 0.39 out, with a
   flange from the roof's edge out to it on the sides and rear (the gap
   closed from above: a solid turret with a plate standing off it); the
   cupola a taller drum (0.1).
2. Hull skirts: one even row of six plates, no tilts, thin seams
   (gap 0.004), straight top edge on the dark rail, a thin dark line under
   it, no light top strip, no cross-seams or bent corner; one plate still
   lost per side.
3. Front: the MG ball 1.6x in the darker paint in a dark ring, the visor
   block 1.6x, the final drives bigger (r 0.085) and dark, bigger spare
   links, a light edge where the nose meets the transmission deck; the
   superstructure's vertical faces in the darker paint; camo a little
   stronger and denser on the hull.

- Round 11 (17 s, waited 115 s): all in. Sent to critic round 4. Tint per
  direction (columns 1-8): 2.9 / 2.2 / 2.9 / 2.3 / 3.4 / 3.5 / 3.4 /
  2.3 % (mean 2.9). ppm 73.62. No shared code changed since round 10.

## Critic round 4: 80.1 (continuity 85.25 PASS, fresh 75)
1. The light top strip on the turret skirt read as a white frame at 1x:
   removed (tskirt_edge False); the flange's top face carries the edge.
2. Hull ratio checked: hull length / width was 1.65 / 1.04 = 1.59 (1.45
   over the skirts); real Pz IV 5.92 / 2.88 = 2.06 (1.80 over the
   skirts). Now L 1.92 (hull 1.84), W 0.98: 1.88 (1.70 over the skirts);
   the four bogies spread (+-0.2, +-0.6), sprocket and idler moved out,
   the skirt row longer; superstructure a touch lower (H 0.185). Still
   eight wheels in four pairs. ppm 73.62 -> 70.99 (stock Panzer 70.22).
3. Turret mass: the longer hull carries it; the cupola raised to 0.13.
   Optional: the ball mount darker (armor_shade). The bent plate was not
   added (risk of noise at 1x).

- Round 12 (17 s, waited 725 s): all in. Sent to critic round 5. Tint per
  direction (columns 1-8): 2.6 / 2.2 / 2.6 / 2.3 / 3.3 / 3.0 / 3.3 /
  2.3 % (mean 2.7). No shared code changed.

## Critic round 5: 81.6 (continuity 87.25 PASS, fresh 76)
The fresh critics flip on the turret size and the skirts: both kept.
1. Front: the sprocket pulled back (y 0.86 -> 0.8) and the hull lengthened
   (Lh 1.84 -> 1.86) so the track front sits about level with the nose.
2. Earth tone cooler (#967252 -> #887656); spare track links racked on a
   bar along both deck edges beside the grilles.
3. Front civ signs taller (0.2 x 0.17) at x 0.39; the visor and the MG
   ball moved in (x 0.2) to make room. The skirt tops' faint pale edge
   was not changed (no light strip there now; it is the plates' lit top
   face).

- Round 13 (17 s, waited 40 s): all in. Sent to critic round 6. Tint per
  direction (columns 1-8): 2.7 / 2.3 / 2.8 / 2.2 / 3.3 / 3.3 / 3.3 /
  2.2 % (mean 2.8). ppm 71.58. No shared code changed.

## Critic round 6: 80.5 (continuity 88.5 PASS, fresh 72.5): no gain
1. Tonal separation: the turret, both skirts and the vertical hull faces
   at 0.74 of the hull paint (was 0.8) against the lit deck; dark shadow
   gaps where the turret-skirt flange meets the turret and between the
   hull sides and the skirts (seen from above); camo contrast halfway
   back (olive #737548, earth #84694c, a little denser).
2. Gun: r 0.05 -> 0.058; the brake in the mid gun tone ('gun_edge') with
   a light top edge on each chamber (a stepped light block).
3. Front civ colour: a civ plate (0.075 x 0.12) on each angled front end
   of the turret skirt.
   Small: the rear-deck links now one clean dark rack per side in a light
   frame.

- Round 14 (17 s): all in. Sent to critic round 7. Tint per direction
  (columns 1-8): 3.0 / 2.9 / 3.1 / 2.5 / 3.3 / 3.2 / 3.3 / 2.5 % (mean
  3.0). ppm 71.58. No shared code changed.

## Critic round 7: 84.4 (continuity 86.25 PASS, fresh 82.5): new best
1. Brake: near-white (its front face and the chamber tops were in the
   light 'muzzle_light'). Now a dark steel ('brake_steel' #4a483c, one
   step over the barrel), its front face and narrow top edges in the mid
   'gun_edge'; the stepped chambers and the side slots kept.
2. The front-corner civ plate merged with "532": now a small diamond at
   the far front end of the angled skirt plate, with a gap.
3. Rear-deck links dropped (dark ticks at 1x).
Turret size and hull length kept (the critics contradict).

- Round 15 (17 s): all in. Sent to critic round 8. Tint per direction
  (columns 1-8): 2.8 / 2.5 / 2.9 / 2.4 / 3.3 / 3.2 / 3.3 / 2.4 % (mean
  2.8). ppm 71.58. No shared code changed (one new colour key,
  'brake_steel', unused by the other vehicles).

## Critic round 8: 82.25 (continuity 88.25 PASS, fresh 76.25): no gain
Fresh critics keep reading the turret-skirt box as a Tiger and the big
faces as slabs; work on the read, not the proportions:
1. The turret skirt open again (no flange), dropped to 0.05 under the
   roof, its inner faces and the deck in the gap dark: thin plates
   standing off a smaller turret whose roof and sloped front show inside;
   the cupola up to 0.15.
2. A dark shadow line on the hull side just above the hung skirt plates.
3. "532" strokes 0.19 -> 0.23 of the height, the dark outline 8 -> 12 mm.
Brake, front details, turret diamond and camo untouched.

- Round 16 (17 s): all in. Sent to critic round 9. Tint per direction
  (columns 1-8): 3.1 / 2.6 / 3.1 / 2.9 / 3.8 / 3.8 / 3.8 / 2.9 % (mean
  3.2). ppm 71.32. No shared code changed.

## Critic round 9: 84.1 (continuity 88.25 PASS, fresh 80): no gain -> plateau

## FINAL: critic round 7 (own round 14, commit 41bc3b51)
Two critic rounds in a row (8, 9) failed to beat round 7's 84.4 by 1.5:
the Panzer plateaued. armor.py is restored to 41bc3b51 (the code of
rounds 15-16 was Panzer-only); the re-render matches critic 7's sheets
(mean abs diff 0.000003, max 2/255 on the comparison sheet). Tint per
direction (columns 1-8): 3.0 / 2.9 / 3.1 / 2.5 / 3.3 / 3.2 / 3.3 / 2.5 %
(mean 3.0). ppm 71.58. final.png / final_red.png are critic 7's (rows:
shipped, final, Tank, Modern Armor).

Critic history (average; continuity / fresh):
- C1 68 (68 / 68)
- C2 70.75 (75.5 / 66)
- C3 74.5 (81 / 68)
- C4 80.1 (85.25 / 75)
- C5 81.6 (87.25 / 76)
- C6 80.5 (88.5 / 72.5)
- C7 84.4 (86.25 / 82.5) = FINAL
- C8 82.25 (88.25 / 76.25)
- C9 84.1 (88.25 / 80); plateau

Open points for a later pass (tried after C7, worth reusing; not in the
final):
- The brake renders near-white in C7 (its front face and chamber tops in
  'muzzle_light'). C8 fixed it: dark steel #4a483c ('brake_steel'), the
  front face and thin top edges in the mid 'gun_edge' (commit 912854f5).
- C9's turret skirt standing off a smaller turret: no flange, 0.05 under
  the roof, dark inner faces and a dark deck in the gap; the cupola at
  0.15. It killed the Tiger read for the continuity critic (0fb4cba7).
- A dark shadow line on the hull side above the hung skirts (0fb4cba7).
- Bolder "532" (stroke 0.23, outline 12 mm): red read as strong as blue.
- The fresh critics' recurring notes: flat, low-contrast surfaces at 1x;
  the running gear blurring with the hull sides into one pale block; the
  camo wants more value contrast, like the stock Tank's shading.
Tank re-rendered with the restored armor.py: identical to its final (mean abs
diff 0.000003, max 1/255).
