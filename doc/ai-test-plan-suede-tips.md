# AI test plan: Civ3 tips from Suede's "Reacting to your Civ 3 games"

Source: Suede, "Reacting to your Civ 3 games" (YouTube, CwiW63gSvEA, 2 h 27 min), a stream reviewing three
viewer games (Egypt at Regent, Rome at Demigod, Sumeria at Deity). The video has no captions; it was
transcribed locally with Whisper large-v3 and the tips below are paraphrased from that transcript. The
full paraphrased notes (86 tips) are kept outside the repo; this plan keeps the 27 that concern the AI.

## How each tip is tested

Each change goes in behind an `AiExperiments` switch and is judged by `AiTrialTest`:

- **Paired** first (`C7_TRIAL_PAIRED=1`): half the civs use the switch, compared with the same civs
  without it, same seeds. Normal games (no scenario), 6 civs, 80x80, Pangaea and Continents.
  - Economy and expansion tips: 24 games of 200 turns.
  - War and diplomacy tips: 16 games of 300 turns (wars are rare before 150).
  - Tips about difficulty (13, 25, 4b): run at Regent and at Deity (`C7_TRIAL_DIFFICULTY`).
- **Every civ** (`C7_TRIAL_ALL=1`) for any change that touches war, peace or trade: a paired trial
  can't see effects on the whole world (see the AI trial memory note).
- **Kings & Nomads** as well where the tip applies there (war targets, combat, workers): 40 paired
  games of 120 turns.
- **Adopt** when the main measure improves and nothing else gets significantly worse (2 standard
  errors); **drop** otherwise, and record the result in the code comment either way.

Measures named below are `AiTrialTest`'s. A few need adding (marked *new*).

## Owner's selection (2026-10-26)

Only these are being tested; the rest of this plan is not.

| Trial | Switches | Notes |
|---|---|---|
| River crossings in attack odds, and stepping round a river before attacking (E3) | `river-odds`, `river-approach` | |
| Scouts go far and fast to meet civs early (E10) | `scout-far` | |
| War targets that survive capture (E4) | `capturable-targets` | The owner thinks the AI shouldn't discriminate either way: adopt only on a clear win |
| Hunt barbarians actively (owner's request, not from the video) | `hunt-barbs` | Attackers seek out barbarian units they can beat, and camps, within 6 tiles |

## Already done (no trial needed)

| # | Tip | Where |
|---|---|---|
| 16 | Don't chase barbarians; let them come | AI ignores barbarian units (`CombatAI.GetPlayersAtWarWith`). The owner wants the opposite: see `hunt-barbs` |
| 20 | Heal before attacking; not in enemy land | `RestAI`, `WarCampaign.ReadyHealth` |
| 22 | Disband obsolete units after a war | `ObsoleteUnits.DisbandOverSupport` |
| 23 | A defender for every new city | `ChooseProducible` (unguarded city, `StockGarrisonBonus`) |

## Trials, in the order to run them

Ordered by expected value against the cost of building the change. Tier 1 first.

### Tier 1: likely large, cheap to build

**E1 `trade-gpt` (tip 11): buy techs with gold per turn, and trade on contact or on a new tech.**
- Now: tech deals are tech-for-tech plus a lump of gold, tried on random rolls; gold per turn only for
  resources.
- Change: let `PlayerAI.TechDeal` pay `goldPerTurn` (cap about 3-5 a turn), and force a trading pass
  on first contact and on learning a tech before others have it.
- Measures: techs, beakers, gold, score; *new*: techs bought.

**E2 `site-food` (tip 3): food-first city sites.**
- Now: the site score adds the centre tile's own yield (rewarding settling on bonus food), has no
  fresh-water term (any water counts) and no "two 2-food tiles" check.
- Change in `SettlerLocationAI.AssignTileScores`:
  - Don't count a bonus-food resource under the centre.
  - Add a river/lake term.
  - Penalise sites with fewer than two 2-food tiles.
- Measures: city5turn, city10turn, citizens, cities, score.

**E3 `river-odds` (tip 15): river crossings in attack odds.**
- Now: `CombatAI.EstimateChanceToWin` and `WarCampaign.ExpectedHits` pass no attack direction, so the
  river-crossing bonus is ignored; rally tiles don't avoid crossings.
- Change: pass the direction, and prefer rally tiles that don't attack across a river.
- Measures: attackWins / attacks, unitsLost, captured. Also Kings & Nomads.

**E4 `capturable-targets` (tip 18): aim at cities that survive capture.**
- Now: `WarCampaign.PickTarget` favours bigger cities but still picks size-1 cities with no culture,
  which are razed on capture.
- Change: skip or heavily discount targets failing `CityInteractions.SurvivesCapture`; bonus for size
  4+ or with culture.
- Measures: captured, civCaps, cities, unitsLost, wars. Also Kings & Nomads.

**E5 `settler-first` (tip 1): settlers before workers while expanding.**
- Now: settlers and workers are scored against each other; no cap on workers.
- Change: while `InExpansionPhase`, a city builds a worker only if it has none of its own and fewer
  than 1 worker per city exists; the 1.5-per-city target applies after expansion.
- Measures: city5turn, city10turn, cities, citizens, unimproved.

**E6 re-test `wonder-timing` (tip 5): no early wonders while expanding.**
- Already built as a switch; re-run on current code with the same settings.
- Also try a per-wonder priority (Pyramids, then Great Library; Great Lighthouse on island maps) in
  `WonderPlanner.EffectWorth`.
- Measures: cities, city10turn, wonders, wStarted, wLost, score.

**E7 re-test `lean-military` plus core barracks only after expansion (tip 2).**
- Already built as a switch. Add: `CoreBarracksBonus` only once `!InExpansionPhase`.
- Measures: cities, city10turn, military, lost, vetPct.

### Tier 2: useful, more work

**E8 `worker-yields` (tip 6): value improvements by real yield.**
- Now: improvements are scored per terrain bonus; despotism is handled only by blanket refusals; any
  resource gets a road bonus; nearest tile first.
- Change in Lua `terraforms.ai_score`:
  - Compute before/after yield, including the despotism penalty and bonus resources.
  - Road only resources the civ lacks.
- Change in `WorkerAI.GetPlanToImproveNearestUnimproved`: weigh tile value against walking distance.
- Measures: citizens, beakers, gold, unimproved, score.

**E9 `contested-first` (tip 4): settle toward rivals first, wider spacing at high difficulty.**
- Now: sites near rival cities are penalised (the opposite of the tip), and spacing ignores difficulty.
- Change: a contested-land bonus in place of the rival penalty; at Demigod and above, raise
  `DistancePenaltyRadius` / minimum spacing.
- Measures: cities, land, lost, score. Run at Regent and Deity.

**E10 `scout-far` (tip 24): explore far and fast to meet civs early.**
- Now: exploration stays near home by design.
- Change in `ExplorerAI.CalculateExplorationScores`:
  - Early on, drop the distance-from-home penalty.
  - Prefer straight-line moves and land over coast.
  - Bonus for unexplored land toward unmet civs.
- Measures: *new* turn of first contact with each civ; techs, beakers.
- Pairs with E1: more contacts make trading worth more.

**E11 `peace-gpt` (tip 12): end pointless wars sooner, paying a little.**
- Now: peace terms use techs and cities only; stalemate peace waits 30 turns.
- Change: allow a few gold per turn in `PeaceAI.BuildPeaceOffer`, and a shorter stalemate when neither
  side has taken anything.
- Measures: warTurns, wars, lost, unitsLost, score. Every-civ check required.

**E12 `attack-window` (tip 19): strike before the enemy has Pikemen and size-7 cities.**
- Now: campaigns ignore the enemy's defender tech and city sizes.
- Change in `StockWarPlotting` / `WarCampaign.MaybeStartCampaign`: favour targets whose best defender
  is weak and whose cities are below the aqueduct cap; drop the window once they're past it.
- Measures: captured, civCaps, wars, unitsLost. Every-civ check required.

**E13 `corrupt-settle` (tip 26): far corrupt cities make settlers and workers.**
- Now: unit builders exclude corrupt cities, but nothing pushes those cities toward settlers or
  workers, and the base barracks term applies everywhere.
- Change: no barracks score in high-corruption cities; a settler/worker bonus that grows with
  corruption while sites or jobs remain.
- Measures: cities, buildings, upkeep, gold.

**E14 `raze-far` (tip 27): raze and replant far, worthless captures.**
- Now: the AI keeps every city that survives capture.
- Change: an AI branch in `CityInteractions.CaptureCity` that razes small, far, high-corruption
  captures (by distance to the palace and expected corruption).
- Measures: cities, gold, upkeep, score. Every-civ check required.

### Tier 3: small or situational

| Trial | Tip | Change | Measures |
|---|---|---|---|
| E15 `chop-overflow` | 8 | Lua `clear_forest`: only chop into a build that can use the shields before it completes (never a wonder) | buildings, city10turn |
| E16 `settler-size` | 7 | `ChooseProducible.CityIsTooSmall`: if a settler would finish before the city can afford it, build a warrior or granary first | city5turn, city10turn |
| E17 `poprush-defense` | 9 | Under forced labour, hurry a defender by population only when threatened and with shields in the box | lost, citizens |
| E18 `shed-pop` | 10 | Bonus for settlers/workers in a city past its unit-support size while sites or jobs remain | cities, citizens, disorder |
| E19 `min-tech` | 13 | At Demigod and above, run science at the minimum and rely on trading (needs E1) | techs, gold, score (Deity) |
| E20 `guard-resources` | 21 | When raiders are near, put a defender on a connected strategic or luxury tile | *new* pillaged tiles; lost |
| E21 `escort-high` | 25 | At Demigod and above, a settler leaving our land waits for an escort | *new* settlers lost; cities |
| E22 `field-stack` | 17 | A defensive stack on high ground between two threatened cities | lost, unitsLost |
| E23 `finisher` | 14 | Against a badly hurt defender, attack with the weakest unit that still has sure odds | unitsLost, attackWins |

## Measures to add to AiTrialTest first

- Techs bought (E1).
- Turn of first contact with each civ (E10).
- Settlers lost (E21).
- Tiles pillaged by enemies (E20).
