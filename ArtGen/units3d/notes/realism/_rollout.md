# Mounted roll-out (2026-11-23): hats forward, riders seated

Two shared fixes from the Cavalry and Knight passes, rolled out to every rider,
chariot crew and siege crew. Sheets: D:\Dev\_handoff\realism\review\_rollout\
(rollout.png / rollout_red.png, per family, rollout_heads.png, rollout_seats.png).

**Hats forward.** riders.headgear(front_fix=...) for every rider
(mounted.HEADGEAR_BACK_TO_FRONT, empty, is the opt-out), chariot crewman and
siege crewman. New mode front_fix='level' (the default now): the pitch that
stands the hat's crown straight up for the posed head (-2.5 degrees for the
shared 'ride' stance, 16.5 for standing crews). The Cavalry (-2) and Knight
(True = riders.HAT_PITCH 14) keep their own pitches in HAT_PITCH_BY_UNIT.
Close-ups found hair or scalp poking through three hats once they turned:
no hair ('hair_asset': None) for the Rider, the Three Man Chariot crew and the
Hwacha crew; the spiked helms and straw hats lifted 0.03 (headgear_lift).

**Rider height.** mounted.seat_rider puts every rider's hip joints SEAT_PELVIS x k
above the seat (was only the posed units). Measured pelvis-to-seat before:
Horseman 0.155, Mounted Warrior/Ancient Cavalry/Hussar 0.182, Rider 0.168,
Keshik/Sipahi/Ansar 0.235, Conquistador 0.315, Cossack 0.355 m; after
0.115-0.134 (0.1 x k). Close-ups: everyone sits on the saddle, nobody sinks.
Chariot crews stand on the car floor already (their root is at the feet, so
the 1.22 scale doesn't lift them: body's lowest point = the floor within 6 mm);
siege crews are at scale 1.0. No height change needed there.

Unchanged (mean abs diff 0 on DEFAULT): Cavalry, Knight, chariot, Catapult,
Cannon, Trebuchet, Artillery, Flak, TOW Infantry, Holy Relic (their crews wear
fitted helmets, not riders.headgear hats). Tint moves by under 1.1 points.

Seen for the unit loops: the Hussar's shako cords now hang across the front at
eye level (reads as a gold visor); with the riders lower the horse's ears show
around the rider's head from behind (Ansar, Cossack, Conquistador); the
Sipahi's horse plume overlaps the rider's face in S; the Three Man Chariot
crew's helms and the War Chariot's khepresh still hide the faces.

# Family pass (2026-11-23): heavier legs, the stock rider-to-horse ratio
Four fresh critics called the riders "too big on a narrow, pony-like horse".
Measured in Blender, installed-era code (3265fe07) against today's (a fixed
section of the trunk, 55 % back from its front):
- the trunk: unchanged (barrel 0.729 m, back 1.806, belly 0.928). (An earlier
  measure, 0.80 -> 0.73, was an artifact: it was taken at a point set from the
  hooves, which the slimming moved.)
- the front cannon's width (x, at 0.28 of the back's height): installed 0.134,
  before this pass 0.102 (Cavalry), after 0.109 (LEG_PROFILE cannons 0.58 -> 0.72,
  forearms 0.8 -> 0.9; the per-unit 'legs' factors still apply on top: Knight
  0.128, Hussar 0.140).
- rider scale (rider / horse size): installed Cavalry 1.147, Hussar 1.172,
  Knight 1.247, Cossack 1.336, all on horse size 1.0. Before this pass: Cavalry
  1.26, Hussar 1.26 on 1.1 (1.145 relative), Knight 1.247 on 1.14 (1.094
  relative, 12 % under the stock), Cossack 1.336. After: Cavalry 1.147, Hussar
  1.172 on 1.1 (1.065 relative), Knight 1.422 on 1.14 (1.247 relative, the stock
  ratio), Cossack 1.336.
Close-ups (seat and full unit) after the change: everyone sits on the saddle,
the thighs and stirrups clear the horse; no clipping.
Critic folders (rows as their finals): review\cavalry\critic7 (Hussar, Cossack),
review\knight\critic5 (Crusader, Conquistador), review\hussar\critic7 (Cossack,
Cavalry); copies in reviews/realism/<unit>/. Tint: Cavalry 18.7 % (final 19.2),
Knight 23.7 % (24.1), Hussar 22.0 % (22.6).
The old finals stay recoverable: LEG_PROFILE's old values are in its comment,
RIDER_SCALE_BY_UNIT was {'Cavalry': 1.0, 'Hussar': 1.0} (commit a4317862).
