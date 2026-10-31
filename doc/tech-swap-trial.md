# Trial: tech-swap on vs off (ZUR-199, switch from ZUR-193)

16 games, 200 turns, Regent, 8 civs, Continents, seeds 1000-1015, Advanced AI. Commit 3a24d9fe.
Paired = each game also played with no switch on, so the difference is "Advanced civ minus Standard civ" in each run, and the effect of the switch is on-diff minus off-diff.
All-civs = every civ Advanced, same seeds, switch on vs off.

## Paired (switch effect = on diff - off diff)

| measure | off | on | effect |
|---|---|---|---|
| score diff | +166.7 +-25.3 | +217.3 +-35.2 | +50.6 (about 1.2 SE, not clear) |
| rank diff (lower is better) | -0.81 | -1.09 | -0.28 (about 0.7 SE) |
| first-place share diff | +0.16 | +0.14 | none |
| techsBought (swaps count 1 per side) | +0.05 +-0.35 | +1.55 +-0.40 * | +1.5 |
| techs100 | -0.02 +-0.32 | +0.66 +-0.27 * | +0.7 |
| techs at end | +0.66 | +0.70 | none |
| govTurn (lower is better) | -8.2 +-5.6 | -10.6 +-4.3 * | -2.3 (not clear) |
| first-contact turn (metTurn, 200 if never) | +3.0 +-1.4 | +1.8 +-1.2 | none; contact comes at turn ~140 |
| gold diff | +9.5 | +21.2 * | +11.7 |
| war declared | +0.06 | -0.13 | none |

## All civs (same seeds)

| measure | off | on |
|---|---|---|
| score | 1158 | 1190 |
| techs at end | 25.6 | 26.2 |
| techs100 | 15.4 | 15.8 |
| techsBought | 4.0 | 7.8 |
| govTurn | 133.9 | 124.6 |
| contact turn | 141.2 | 140.6 |
| wars declared | 0.1 | 0.2 |
| rank 4.5 both; first-place share 0.1 both | | |

## Reading

The switch does what it is meant to: about 3.5-4 more techs bought, 0.4-0.7 more techs by turn 100 and an earlier government (about 9 turns across all civs, 2 in the paired view). Score and rank move the right way but not beyond noise. The world stays sane with it on (no more wars to speak of, techs and score up). Contact is late on this map (turn ~140), so swapping "at first contact" has little time to matter before turn 100; the gain comes from later swaps.
Raw results: not committed (runs in .paperclip/runs-zur199).
