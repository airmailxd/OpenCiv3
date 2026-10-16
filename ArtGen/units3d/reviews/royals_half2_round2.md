# Royal fixes, round 2b (review of roy2_*)

**DATA** = an entry change in remaster_royals.py. **FEATURE** = needs code. Fixes are ranked by visibility at 1x.

## Scores (round 1 -> round 2)

Columns: Imp = imposing/regal, AI = not-AI, Fai = faithful, 1x = readable at 1x, Mat = materials, Det = detail vs original.

| Unit | Imp | AI | Fai | 1x | Mat | Det | Result |
|---|---|---|---|---|---|---|---|
| King_Japanese | 5->7 | 6->7 | 4->7 | 6->7 | 6->7 | 4->6 | FAIL (detail) |
| King_Korean | 5->6 | 6->7 | 5->5 | 6->7 | 6->7 | 4->4 | FAIL |
| King_Mayan | 6->7 | 6->7 | 5->6 | 6->7 | 6->7 | 4->6 | FAIL |
| King_Mongol | 4->7 | 5->7 | 4->6 | 5->7 | 6->7 | 4->7 | FAIL (sword angle) |
| King_Netherlands | 4->6 | 5->6 | 3->5 | 5->7 | 5->6 | 3->5 | FAIL |
| King_Ottoman | 5->7 | 5->7 | 4->7 | 5->7 | 6->7 | 4->6 | FAIL (detail) |
| King_Persian | 4->6 | 5->7 | 4->6 | 5->7 | 6->7 | 4->6 | FAIL |
| King_Portugal | 4->6 | 5->7 | 3->6 | 5->7 | 6->7 | 3->6 | FAIL |
| King_Roman | 5->7 | 5->7 | 5->7 | 5->6 | 5->7 | 5->7 | FAIL (eagle clipped) |
| King_Russian | 4->5 | 6->7 | 3->5 | 5->6 | 6->7 | 3->5 | FAIL |
| King_Spanish | 4->6 | 5->6 | 3->5 | 5->7 | 5->6 | 3->5 | FAIL |
| King_Sumeria | 4->7 | 5->7 | 3->6 | 5->7 | 6->7 | 3->6 | FAIL |
| King_Viking | 4->7 | 4->7 | 4->7 | 5->7 | 5->7 | 4->6 | FAIL (detail) |
| King_Zulu | 5->6 | 5->7 | 4->6 | 5->7 | 6->7 | 4->5 | FAIL |
| Leader_Ancient_Times | 4->5 | 5->6 | 4->5 | 5->6 | 6->7 | 3->5 | FAIL |
| Leader_Industrial_Ages | 5->7 | 6->7 | 5->7 | 6->7 | 6->7 | 4->6 | FAIL (detail) |
| Leader_Middle_Ages | 5->6 | 5->6 | 5->6 | 6->7 | 6->7 | 4->5 | FAIL |
| Leader_Modern_Times | 5->7 | 6->7 | 4->7 | 5->7 | 6->7 | 4->6 | FAIL (detail) |
| Princess | 5->6 | 6->7 | 4->6 | 6->6 | 6->7 | 4->5 | FAIL |
| SciLeader | 4->5 | 5->7 | 4->5 | 5->6 | 6->7 | 3->4 | FAIL |
| Shogun | 4->6 | 5->7 | 3->6 | 5->7 | 6->7 | 3->6 | FAIL |
| The_King | 5->6 | 6->6 | 5->6 | 6->7 | 6->6 | 4->5 | FAIL |

## Shared problems
1. **Belt gear still shows on robed royals.** A yellow (or tinted) buckle/pouch square sits at the hip or on the back of the skirt.
   - Seen on: Korean, Ottoman, Netherlands, Sumeria (pink), The King, and Japanese (two red blobs on the back of the hakama).
   - DATA: `'belt_gear': False` on every royal. Or FEATURE: default it off for royals.
2. **Tall items still clipped at the frame top.**
   - Leader Ancient: the crook runs off the top in all 8 directions; the hook is never seen.
   - Roman: the eagle touches y=0 in directions 0 and 4-7.
   - Sumeria: the crown tip touches y=0.
   - The renders also sit about 8-10 px (2x) lower than the originals. FEATURE: raise the anchor, or clamp the frame to the tallest item.
3. **The women are now smaller than the originals.** Russian and Princess heads are about 12 px (2x) lower than the original's, and they read weak at 1x.
   - DATA: `'unit_scale': 0.95` for gown units. If that key isn't per-entry, it's a FEATURE.
4. **Capes are still rigid flat boards.** They show as thin vertical planes in the side views and have no flare.
   - Affected: Netherlands, Spanish (white veil-cape), Leader Middle, The King.
   - The originals flare to a wide hem: Netherlands has a train on the ground, Leader Middle a bell shape.
   - The new cape hems hardly show because of it. FEATURE: cape flare/width.
5. **Swords held out to the side.** In 'stand' with a sword, the blade points nearly horizontally outward in directions 0 and 4-5 (Mongol, Viking, Leader Middle, Shogun). The originals hang the blade down and out at about 30-45°.
   - DATA: `'hang_dir': (-0.15, -0.3, -0.95)`, if stand reads hang_dir. Otherwise a FEATURE: the stand stance's blade angle.
6. **Patterned garments are still missing.** These are the main remaining detail gap:
   - vertical stripes: Korean robe, Russian skirt, Japanese and Princess gold front stripes, Sumeria front stripe, Shogun robe
   - jaguar print: Mayan
   - gold field on the back of the cape: Leader Middle
   - FEATURE: a stripes pattern (count, width, colors incl. 'tint', front-only option) and panels.

## Per unit

### King Japanese
1. FEATURE: a gold vertical stripe down the front and back of the hakama (the original's most visible detail after the colors).
2. DATA: `'belt_gear': False` (two red blobs on the back of the hakama).
3. DATA: the topknot should rise straight up, tall and black. ponytail01 falls back. Try `'hair': 'topknot'`, `'hair_asset': None`. If that gives no knot, it's a FEATURE (a topknot bun asset).

### King Korean
1. FEATURE: robe_stripes. Tinted vertical stripes at the center front, at the sides and on the back. Still the main gap: the original reads mostly through these stripes.
2. DATA: `'belt_gear': False` (yellow square at the hip).
3. DATA: `'bands': [('neck', 'tint'), ('wrist', 'tint')]`. Check the cuffs show; they're hidden by the clasp. Otherwise `'wide_sleeves': False`.

### King Mayan
1. DATA: `'colors'`: `'plume': '#2e7a4a'`. The fan reads red/white; the original's is green/red/white. If feather_crown ignores plume, it's a FEATURE (a third feather color).
2. FEATURE: jaguar stripes/spots on the kilt. The original's kilt has vertical gold/dark stripes.
3. FEATURE: broad_collar. The original has a large gold pectoral; the render has a thin band.

### King Mongol
1. DATA/FEATURE: the blade angle (see shared problem 5). The scimitar points horizontally in directions 0 and 5.
2. DATA: `'belt_gear': False`.
3. Nothing else to fix: the hat, cuffs, hem and goatee match.

### King Netherlands
1. FEATURE: cape flare plus a train on the ground. The original's cape is a wide bell with a gold hem pooling behind; the render's is a narrow board.
2. DATA: `'belt_gear': False` (yellow pouch at the hip).
3. FEATURE: red slashes on the doublet (several small tinted dashes on the chest and sleeves).

### King Ottoman
1. DATA: `'belt_gear': False` (yellow square at the hip).
2. FEATURE: tinted skirt panels: front triangles on both sides and a large arc low on the back. The hem band alone undersells the original's tint.
3. DATA: the turban stack reads as ringed coils. Fine, but `'headgear_scale': 0.9` would bring it closer to the original's round shape.

### King Persian
1. DATA: `'hem_bands': [('bottom', 'tint', 0.06)]` doesn't show; the tunic hem is plain white. Check it was applied, and make it wider (0.08).
2. DATA: `'neck': 'high'` or a sleeved top (`'top': 'sleeved'` with short sleeves if possible). The tunic is off-shoulder; the original has white short sleeves.
3. DATA: `'crown': 'spiked'` with `'headgear_scale': 1.2` (or the crown size key). The crown reads as a thin circlet; the original's crown is big.

### King Portugal
1. DATA: the cavalier's white plume and floppy brim are wrong. The original is a black round hat with a flat, even brim and a gold edge, and no plume. Remove the plume (`'colors'`: `'plume': '#1e1a18'`). FEATURE: wide_brim_hat with a gold edge.
2. DATA: `'wraps': 'boots'`, `'colors'`: `'boots': '#9a9aa2'`, or plain hose. The wraps show stripes; the original has plain grey hose and black shoes.
3. DATA: the gold puffed shoulders are hardly visible. Use `'pauldrons': True` with a larger scale, or a FEATURE (puffed gold shoulder rolls).

### King Roman
1. FEATURE (shared problem 2): the eagle touches the frame top in 5 directions. As a DATA alternative, use `'length': 1.85`.
2. FEATURE: gold strips on the white pteruges.
3. FEATURE: tinted cuirass panels (chest and shoulders).

### King Russian
1. DATA: `'gown_flare': 4.0` or more. It's a cone now. The original is a vast bell, nearly as wide as the figure is tall.
2. FEATURE: alternating red/silver vertical skirt stripes. This is the original's defining look.
3. DATA: the gown scale (shared problem 3). The crown is tiny: `'crown': 'arched'`, `'crown_gems': True`, plus a size bump if there's a key for it. The orb in the left hand is a FEATURE.

### King Spanish
1. FEATURE: the white veil-cape is still a detached flat board standing behind her. The original's veil hangs from the head, close to the back.
2. FEATURE: a white front skirt panel with red sides. DATA: `'gown_flare': 2.4` (the original is a bell, wider at the hem).
3. DATA: `'crown': 'spiked'` with `'crown_gems': True`. 'tall' reads as a gold cylinder/fez; the original is an open crown with points.

### King Sumeria
1. DATA: widen the tinted bands: `'hem_bands': [('bottom', 'tint', 0.09, 0.08), ('bottom', 'tint', 0.09, 0.24), ('bottom', '#ece8e0', 0.04, 0.0)]`. They read as thin lines; the original's are broad.
2. DATA: `'belt_gear': False` (pink/salmon square at the hip).
3. FEATURE: a ball head on the staff and the vertical red front stripe on the skirt.

### King Viking
1. DATA/FEATURE: the blade angle (shared problem 5).
2. DATA: `'hair_asset': 'long01'`, `'mane': 0.1`, `'colors'`: `'hair': '#b07840'`. The long reddish-blond hair behind the helmet is barely visible now.
3. DATA: the gold neck band reads as a yoke. Fine. The original has a brown belt: check `'colors'`: `'belt': '#4a3020'` was applied.

### King Zulu
1. FEATURE: tinted_shield_stripes. The original's shield is red with white bars; the cowhide is untinted.
2. DATA: hold the spear upright as in the original: `'stance_name': 'stand_staff'`. It's level and pointing forward now.
3. DATA: the feather crown reads like a big ruffled cup. Use `'headgear_scale': 0.85` to match the original's compact crown.

### Leader Ancient Times
1. FEATURE/DATA: the crook is clipped in every direction and the hook never shows. Use `'props': {'crook': ('crook', {'length': 1.6})}` with stand_staff, or the frame fix.
2. FEATURE: undergown plus lapel_trim. The original's white front with red edges is its most visible element.
3. DATA: the hem bands read as faint lines. Use `'hem_bands': [('bottom', 'tint', 0.07, 0.02), ('bottom', 'tint', 0.06, 0.12)]`. The white beard still reads as a mask: FEATURE (long_beard style 'long' should hang to the chest).

### Leader Industrial Ages
1. FEATURE: the white waistcoat front (the large white chest panel in the original's front views).
2. DATA: `'colors'`: `'hair': '#ffffff'`. The wig still reads mid-grey.
3. DATA: `'belt_gear': False`. The white belt and buckle at the waist aren't in the original.

### Leader Middle Ages
1. FEATURE: cape flare plus the gold field on the back. The back view is the original's signature, and it's flat red now.
2. DATA: `'hem_bands': [('cape', '#f0ece4', 0.1)]`. The white fur hem doesn't show; make it wider or check it was applied.
3. DATA/FEATURE: the blade angle (shared problem 5).

### Leader Modern Times
1. FEATURE: gold edges on the sash.
2. FEATURE: a tinted shoulder patch.
3. Nothing else: the cap, sash, cuffs and stick all read.

### Princess
1. DATA: size (shared problem 3). `'robe_drop': 0.6` with `'gown_flare': 1.8`. The original's gown is a fuller column.
2. FEATURE: the gold front stripe on the skirt.
3. DATA: `'chest_emblem': ('disc', 'tint', 0.1)`. The emblem is too small to read at 1x. Also make the hem band wider: `'hem_bands': [('bottom', 'tint', 0.07)]`.

### SciLeader
1. FEATURE: tie (a tinted strip down the chest). The original's only tinted element besides the bag.
2. FEATURE: briefcase in the left hand.
3. FEATURE: wild_hair. The original's big white curly hair is its whole identity; the render has a thin grey cap. As DATA, use `'colors'`: `'hair': '#ffffff'` and `'mane': 0.1`.

### Shogun
1. DATA: `'colors'`: `'helmet': '#1a1614'` and remove 'helmet' from `'tint'`. The kabuto renders red; the original's is black with a gold crest.
2. FEATURE: two_hand_raised. The katana should be held up at the shoulder; it's held out low now.
3. FEATURE: dark and gold vertical robe stripes. DATA: `'colors'`: `'cuirass': '#2e221a'`. Check the kataginu reads brown-black.

### The King
1. FEATURE: deep_v. The red block still reads as a vest; the original is a red scarf/V on a white chest.
2. FEATURE: cape flare (it's a board).
3. FEATURE: bell_bottoms (the wide white flares). Also DATA: `'belt_gear': False` (yellow pouch).
