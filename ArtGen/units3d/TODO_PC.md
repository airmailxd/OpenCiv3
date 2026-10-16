# TODO for the PC session (from the Mac, 2026-10-16)

## Next: take over the units blocked on your props

These 7 units now belong to you: entries, renders and the review loop. Each one is blocked only by a prop you're building, or would build.

| Unit | File | Blocker (prop) | Last scores (I/NAI/F/1x/M/D) |
|------|------|----------------|------------------------------|
| Army Ancient Times | remaster_royals.py | war_standard device 'bars' (three bars); Imposing and 1x also at 6 | 6/7/7/6/6/6 |
| Army Industrial Ages | remaster_royals.py | war_standard device 'blobs' (red/green/cyan on the tinted field) | 7/7/7/7/7/6 (detail only) |
| Army Modern Times | remaster_royals.py | war_standard device 'saltire' (diagonal cross) | 6/6/6/6/6/6 |
| King Austria | remaster_royals.py | 'wolfhound' prop; place it with 'ground_props': [('wolfhound', {}, (0.5, 0.1), 15)] | 6/6/6/6/6/5 |
| King Aztec | remaster_royals.py | dense feather fan (gold/tint/red/teal cycling) | 6/6/6/7/6/6 |
| King Incan | remaster_royals.py | 'feather_standard' (a staff topped by a feather fan) | 6/6/6/6/6/5 |
| King Iroquois | remaster_royals.py (stays with the Mac) | a feather wrap on the spear shaft: please add a 'feather_wrap' option on props.spear | 7/7/7/7/6/6 |
| Impi | remaster_footmen.py | tapered feather cards for feather_headdress (it reads as "red sausages") | 7/6/8/7/7/7 (not-AI only) |

The latest review notes for the royals are in the MISSING comments of those entries. For Impi, see reviews/footmen_batchA_round9.md.

**Ownership:** edit only these units' blocks, and pull before each commit. I keep the rest of royals half 1: American, Byzantines, Celtic, Chinese, Greek, Hatti, Indian, Iroquois, and the 8 that pass.

## Not now

- **Animations** (attack, fidget, death poses): the owner asked for stills first. Don't start without stzur's go-ahead. Write it up as a proposal in STATUS_PC.md instead.
- **Leaderheads:** done and released (v0.7.9).

## Builder keys added recently (all render-tested)

- 'crown_over' (a crown over a hood or helmet; 'crown_lift', 'crown_grow')
- 'hair_scale'
- 'shoulder_rolls'
- 'ruff' with 'ruff_style': 'fan'
- 'side_stripe' pattern
- chest_strap 'edge' and 'edge_width'
- 'ground_props'
- 'shield_at': 'back'
- 'back_tilt'
- 'crown_lift'
- cape 'cape_lining', 'cape_emblem' ('lion' | 'fleur' | 'disc' | 'cross'), and ermine hems
- 'face_prop', 'long_gloves', 'trouser_flare', 'cuirass_trim', 'cloth_dirt', 'physique'

Gold trims are semi-metallic, and cape hems are continuous.
