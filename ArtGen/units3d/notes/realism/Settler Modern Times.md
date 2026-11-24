# Settler Modern Times: realism design (2026-11-23, realism/misc)

## The real-world model
A 20th-century homesteader or migrant setting out with everything he owns: a slouch (boonie) hat, a work
shirt and a vest, khaki trousers, high boots, a tall frame pack with a bedroll, a walking staff, and a
suitcase in the free hand. Civ3's original: the hiker with the staff and the big pack, a civ hat and vest.

## The features that must read at 1x
1. The big pack with its bedroll (the settler's load).
2. The suitcase in the left hand (new: the belongings, a migrant's read).
3. The tall staff planted at his right.
4. The civ hat and vest over the white shirt.

## Civ colour
Stock: the shipped unit 13-23 % (mean 18), Settler 13-25 (18), Explorer 21-28 (24). Regions: the hat and
the vest (unchanged); the suitcase stays dark leather.

## Stock neighbours
Settler and Explorer.

## Rounds
### Build rounds 1-3 (drafts b1-b3, then a1 at HEAD on the GPU)
- b1: a suitcase (props.briefcase at 1.6) in the left hand; it read, but the same brown as the pack.
- b2: the case dark leather with brass corners.
- b3: the plain box pack replaced by a new props.settler_pack (rails, a flapped bag with straps and buckles,
  side pockets, the bedroll strapped on top, a billy can, a coil of rope); a lighter skin under the brim.
  Pixels 1.14: unit_scale 0.96.
- a1: civ 11-18 % (mean 15.2; shipped 18); fitcheck pixels 1.05; specks clean. Open: the face is still
  shaded under the brim in dir 0.

### Critic round 1 (batch a1): 80.3 / 72.75 = 76.5
Both critics: the hat was an oversized disc that hid the face. They also wanted a modern cue, said the
staff arm was near T-pose (and the hand off the staff in dir 7), and asked for trim on the suitcase.

### Build round for a2
- A new felt slouch hat (riders 'slouch_hat'): a smaller, turned-down brim, a dented and pinched crown,
  a dark band.
- The pack's frame is now aluminium tubing with a top bar and a padded hip belt (a 1960s external-frame
  pack).
- A new props.suitcase: piped edges, brass corners and latches, two straps with buckles, a thick handle.
- The staff is held in the 'ready' stance with the elbow bent and the fist forward of the hip.
- A civ neckerchief. Civ 10-18 % (mean 12.7).
- a2 at 4d1e1f57: civ 10-18 % (mean 12.7); fitcheck pixels 1.03; specks clean.

### Critic round 2 (batch a2): 85.8 / 77.25 = 81.5
Open: in dirs 4, 5 and 8 the staff arm stuck straight out at shoulder height; the brass-studded suitcase
was not modern.

### Build round for a3
- The staff is planted a forearm out from the body: elbow bent and down, fist at chest height
  (hand_r_at (-0.16, -0.24, -0.22), elbow_r 0), its top leaning in, grip 1.3 so the foot meets the ground.
- A new props.holdall replaces the suitcase: an olive nylon duffel with dark end panels, a zip, a front
  pocket, webbing handles and a clipped strap.
- Staff arm, a3 final: hand_r_at (-0.04, -0.1, -0.36), checked with a Blender probe. The upper arm hangs
  at the side and the forearm runs forward and down to the fist before the hip; the staff (grip 1.0) is
  planted beside him. With the fist further forward (as at a2), the forearm stood out level and read as
  an arm stuck straight out in the side views.

### Critic round 3 (batch a3): 85 / 85.5 = 85.25, PASS

## FINAL: round a3 (853d96b7), 85.25, PASS
Scores: a1 76.5; a2 81.5; a3 85.25. Civ 11-18 % (mean 13.2); fitcheck pixels 1.00; specks clean. Finals:
reviews/realism/settler_modern_times/ and the handoff review/settler-modern-times/ (shipped, final, Settler,
Explorer).
Later pass: the holdall reads as a dark lump in dirs 0, 1 and 7 (a lighter nylon, or handles and a strap
that read); the brim still shades the face in dir 0.
