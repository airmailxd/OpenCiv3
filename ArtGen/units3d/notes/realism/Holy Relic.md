# Holy Relic: realism design (2026-11-23, realism/misc)

## The real-world model
The Ark of the Covenant as Exodus 25 and Numbers 4 describe it and Civ3 shows it: a gilded acacia chest of
about 1.1 x 0.7 x 0.7 m under the mercy seat with two kneeling cherubim, four gold rings and two long poles,
carried by Levites. Civ3's original: the gold ark on its poles with a bearer at each end, walking.

## The features that must read at 1x
1. The gold ark with its cherubim (unchanged: it read in every direction already).
2. The two poles held in the bearers' fists (before: the poles passed below their hands).
3. Two bearers as Levites: white linen sleeves, a civ ephod (vest) and kilt, a belt; walking.

## Code
siege.holy_relic (the ark; new RELIC_POLE_Z, the poles raised to 0.9 m so the hands reach them) and
crews.CREW['Holy Relic'] (new LEVITE dict: the 'ready' stance with each fist placed on a pole).

## Civ colour
Stock: the shipped Holy Relic 13-35 % (mean 21: two whole civ tunics). Aim near the shipped share, on the
bearers only: the ephods and kilts, a trim on the tunics; the ark stays gold.

## Stock neighbours
Settler and Worker (other non-combat units).

## Rounds
### Build rounds 1-2 (drafts b1-b2, then a1 at HEAD on the GPU)
- b1: the poles raised to 0.9 m and each fist placed on its pole (the 'ready' stance's hand_*_at); white linen
  and a civ kilt only: 7 %, too little.
- b2 = a1: a civ ephod over the white sleeves, the civ kilt: civ 8-29 % (mean 16.6; shipped 21); fitcheck
  pixels 1.00; specks clean. Open: the kilt is a front flap, so the legs show brown trousers from behind.

### Critic round 1 (batch a1): 70.5 / 79 = 74.75
One critic gave a critical: in dirs 3 and 7 the hands didn't hold the poles, and the near pole clipped
through a bearer. Also: the brown panels read as a wooden crate; the cherubim read as a gold swirl.

### Build round for a2
- New siege.holy_relic_carried, selected by RELIC_CARRIED (False gives the old model). The Ark rides on
  the shoulders of four Levites: RELIC_POLE_Z 1.76, the shoulder top probed on the crew body. The rings
  sit high on the sides, and each bearer has the pole on his inner shoulder (bearers at x ±0.62,
  y ±1.3).
- Hands: crews.LEVITE_R and LEVITE_L. The near fist is in front of the shoulder with the elbow bent and
  down; the other hand reaches across the chest to the pole.
- The panels are a deeper gold (#a8822c), so the chest is gold on every face.
- The cherubim are kneeling figures (siege._cherub) facing each other: body, bowed head, hands together,
  and wings arching forward with about a 0.4 m gap between the tips.
- Wing tips thickened, which cleared a 2 px speck in the side views.
- a2 at 23526328: civ 13-47 % (mean 25.8; dir 5, the end view, shows two whole bearers); fitcheck pixels
  1.03 (bbox 1.14: four bearers and the raised Ark); specks clean.

### Critic round 2 (batch a2): 84 / 85.5 = 84.75
The pole critical is closed. Open: from the front the wings swept up like ears (a trophy look); in dir 1
one Levite showed bare legs and a civ front flap.

### Build round for a3 (on the merged code: the bearers' Settler base was redesigned by foot-civil, and
crews now default to posture False)
- The wings are lower and reach forward toward the other cherub over the lid.
- The Levites wear a full skirt all round (bottom 'skirt', length 0.6) instead of the kilt flap.
- Bearers, a3 final: a civ tunic with its skirt to the knee all round (coat_length 0.58) under a white
  linen ephod, over linen breeches. The plain 'skirt' still left one bearer with bare legs, and with a
  white tunic the civ share fell to 10 %.

### Critic round 3 (batch a3): 85 / 87.5 = 86.25, PASS

## FINAL: round a3 (853d96b7), 86.25, PASS
Scores: a1 74.75 (critical: the poles); a2 84.75; a3 86.25. Civ 15-30 % (mean 21.5; shipped 21.4); fitcheck
pixels 1.08; specks clean. Finals: reviews/realism/holy_relic/ and the handoff review/holy-relic/ (shipped,
final, Settler, Worker).
Later pass: the two cherubim still merge into one shape in dirs 1 and 5 (end-on); more air between them
or a lower, more open wing arch.
