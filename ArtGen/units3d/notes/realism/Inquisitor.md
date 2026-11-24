# Inquisitor: realism design (2026-11-23)

## The real-world model

An inquisitor of the Spanish Inquisition (about 1480-1530, Torquemada's tribunals). The inquisitors were
Dominican friars, so the true dress is the **Dominican habit**: a white wool tunic and scapular to the feet,
and over it the **black cappa** (an open cloak with its hood), a leather belt with the rosary. Pedro
Berruguete's *Auto de fe* (about 1495) paints this. The light-and-dark habit is also the best read at 1x: the
white front between the black cloak's edges, never a dark blob.

- a **white habit** to the floor (heavy wool, falling in a few deep vertical troughs, with soft drape onto
  the feet: sculpted, not regular ripples);
- the **black cappa** from the shoulders, open in front, its hood up over a shadowed face;
- a **book** (the breviary, or the edict of faith) held at the chest in the left hand, civ cover;
- a **torch** upright in the right hand (the original's identity prop: the auto de fe).

Civ3's original: a floor-length charcoal hooded robe, a gold cross and sash, a civ book cover, civ shoulder
band and cuffs, the book raised in the left hand, a torch beside the head in the right.

## The features that must read at 1x

1. The **long robe** to the floor: the bell silhouette, light inside dark.
2. The **torch**: its bright flame above the head on the right.
3. The **hood** up, the face in shadow but not black.
4. The **civ book** raised at the chest.

## The pose

Upright and solemn, the weight shift small (a long robe hides the legs): the torch held up beside his head,
the book at the chest.

## Civ colour

Stock: the shipped Inquisitor 2-9 % (mean 6): too little to tell blue from red at 1x. The foot stock is
9-34 %. Aim about 10-18 %: the **book cover**, a **stole** or band (civ, at the neck and shoulders, so it shows
from behind too) and the **cuffs**.

## Stock neighbours

Explorer and Settler.

## Rounds

### Build rounds 1-3
- R1: the Dominican habit (white) under the black cappa (cape) with its hood; troughs in the habit
  (garments.skirt 'troughs', new, off by default; units 'skirt_troughs', 'skirt_folds', 'skirt_flare').
- R2: civ cuffs and belt (6-13 %). R3: the habit to the floor (robe_drop 0.68), a civ cross on the cappa's back
  (the Holy Office's badge), so the back shows the colour. Civ 6-13 % (mean 9).

### Critic round 1 (batch civil 1): 77 / 82 = 79.5
Both: almost no civ colour from the front; the habit flat, the brightest value on the sheet; the cappa a flat black
sheet; a tiny flame. Fixes: the cappa lined in the civ colour, a wider civ belt, an off-white habit with deeper
troughs, the cappa draped by simulation (units 'cape_drape', new, off by default), a bigger cross, a bigger flame
(props.torch 'flame', new).

### Critic round 2 (batch civil 1): 81 / 83 = 82
Landed: the front civ colour (belt, breast cross), the off-white habit and its troughs, the draped cappa (much
better). Still: the torch low at the chest in 1-3, the flame over the face in 2; no cuffs (hidden under the wide
sleeves); plain props (a flat flame, a slab of a book). Fixes: the torch raised above the head; plain sleeves with
civ cuffs.

### Critic round 3 (batch civil 1): 85 / 78 = 81.5
The continuity critic passed it (85): the torch raised in every direction, the civ cuffs, the draped cappa. The
fresh critic: front civ colour low (it measures 9-18 %, mean 13), thin arms, a flat habit.

### Solo round (round 3 plus the continuity critic's optional points: the torch arm further out, the habit
### greyer, '#aca490'): 85 / 77 = 81
The continuity critic: PASS at 85. The fresh critic: front civ colour low (measured with the shadow; the sheets
measure 9-18 %, mean 13), the book edge-on at the belly from the front and its corner over the hood from behind, the
habit's folds too soft.

## Final (solo state)
Scores: R1 77 / 82 = 79.5; R2 81 / 83 = 82; R3 85 / 78 = 81.5; **solo 85 / 77 = 81, final**. The three last rounds
sit within a point of each other (a plateau); the final is the latest, not round 2's nominal 82, because round 2
held the torch at the chest with the flame over the face (dir 2) and hid the civ cuffs, both fixed since and
confirmed by the continuity critic (85 in round 3 and solo). No critical failure; the continuity critic passes it,
the fresh critics do not. Open points: the book cover-out at the chest (it is edge-on from the front and pokes above
the hood from behind); a bigger breast cross; deeper sculpted folds in the habit from the belt to the hem; the flame
against the hood in direction 3.

## Animations
The templates (run, death) from the final still; checked at 2x. Open point: in RUN the legs stride through the
baked habit (the draped skirt is a fixed mesh), showing bare shins at the front.
