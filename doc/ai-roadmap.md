# AI roadmap (started 2026-10-29)

Two tracks:

1. **Standard AI plays like Civ3's.** Each change is sourced from `doc/civ3-compendium/` (mostly section 08).
2. **Advanced AI**, chosen per AI player at setup:
   - An AI model sets each player's strategic plan every few turns.
   - The classical AI carries the plan out.
   - Without an API key, a rule-based planner sets the plan instead.

## Ideas borrowed

- **Vox Deorum** (Civ5 + Vox Populi; Chen et al., FDG '26, arXiv 2512.18564).
  - The LLM only turns strategy knobs: flavors, grand strategy, attitude toward each rival, next tech or policy. The native AI does all the building and fighting.
  - Decisions run off the critical path, after the player's own turn.
  - Knobs expire after 10 turns, so a dead or slow model hands control back.
  - Every choice must come from an enumerated list of options, and every value is clamped.
  - The state is given as Markdown, not JSON. It is fog-filtered, and the map is summed up as threat zones rather than coordinates.
  - Lower-priority events are trimmed by importance tier; turning points (wars, peace, captures) are always kept.
  - Results:
    - Out of the box it ties the native AI.
    - Deciding every 5 turns beat deciding every turn. The best setup won 68% against the native AI.
    - An "agentic" (free-form multi-step) loop was no better than a fixed schedule.
  - Failure modes: stubbornness, wishful thinking ("winning wars" while losing), poor spatial sense, context bloat.
  - Controls: `none` (native AI only) and `null` (knobs reset to neutral) separate "steering at all" from "steering well".
- **rs-sdk / RuneBench** (Max Bittker; talk https://www.youtube.com/watch?v=cNm4zyUtWj0).
  - Give the model structured state, never pixels.
  - Use two layers of actions, "plumbing and porcelain" (low-level raw actions under high-level helpers). Actions return honest outcomes; silent no-ops were the worst failures.
  - The LLM supervises policy code rather than acting every step.
  - Memory is a per-agent notes file.
  - Score on several metrics across many seeds: one metric gets gamed, and short runs are noisy.
  - Improve in a loop: run overnight, ask what went wrong, triage, fix.

## Track 1: Civ3 parity

| # | Change | Source | Status |
|---|---|---|---|
| 1 | AI pays full building upkeep (house rule default 0 → 100) | 08 §1: no DIFF field gives a maintenance bonus | done: about 1 tech fewer, slightly fewer captures (8 games, all civs) |
| 2 | No new war on a civ for 8 turns after peace with it | 08 §3.4 (Flintlock's belief: 8 turns) | done: wars and captures unchanged in trial |
| 3 | Favorite/shunned government adds 12.5% to its score | 05 §5.8 (Flintlock); add vs multiply unverified | done: neutral in trial |
| 4 | Import the BIQ's build-often/never flags and use them in production | 08 §3.2 (small effect, about 5%) | todo |
| 5 | Workers: stop at about 0.8–0.9 per city, not 1.5 | 08 §3.2 | done: neutral in trial |
| 6 | Peacetime units: about 53% defense, 43% offense | 08 §3.2 (Ozymandias) | no change: C3 ends at about 62:38, inside his "3:2 to 4:3" D:A ratio (trial measures defUnits/offUnits) |
| 7 | Save war campaigns and their cooldowns (now lost on reload) | bug | done: also saves deal and passage request turns, last disband, market price. Not yet saved: strategic priorities, Kings & Nomads hordes |

Kept on purpose, not Civ3: city spacing, coordinated war stacks, no Wealth, no Explorers.

War and diplomacy changes also need the all-civs (`C7_TRIAL_ALL=1`) trial against a baseline on the same seeds.

## Track 2: Advanced AI

**Goal (owner, 2026-10-29):** an Advanced AI that can beat a human with no advantages over one.

### Built
- **Fair play.** Advanced AIs:
  - get Regent's AI bonuses (none) whatever the difficulty (`GameData.AiBonusDifficulty`);
  - get no AI house rules;
  - always play with the fog of war (`AIFogOfWar.EnabledFor`).
- **Strategic plan (`StrategicPlan`, saved with the AI's state).**
  - Fields:
    - a focus of 0–100 for each of Civ3's build-often/never categories;
    - a war-aggression shift of −2..+2;
    - a stance per rival (Target / Friend / Neutral);
    - a victory goal;
    - a rationale and notes.
  - It nudges production scores (`PlanEffects.ProductionAdjustment`), and war plotting's aggression, exclusions and target score (`StockWarPlotting`).
  - A neutral, expired or Standard plan changes nothing.
- **Planning cadence.** A plan is made every 5 turns, and at once when a war starts or ends or the civ's city count changes. Plans lapse after 10 turns.
- **Rule planner (`RulePlanner`).**
  - Losing a war → defend, and no new wars.
  - Winning → press the weakest enemy.
  - At peace with room → expand.
  - No room → target a much weaker reachable neighbour, or build up.
  - Behind in tech → science. Disorder → happiness.
- **Policy review by Claude (`PolicyReview`, `PolicyPlaybook`)**, **off by default**: it needs its own switch (Settings → Advanced AI → Model review, or `C7_TRIAL_MODEL=1` in trials) as well as a key; a key alone does nothing (ZUR-126). The owner's design, 2026-10-29: Claude doesn't play the game. It adjusts behaviour by time and situation, building on past success in small steps.
  - **Every 50 turns** it reviews the player's own policy (`AdvancedPolicy`, saved): the knob values and behaviour switches the rule planner plays by.
  - **It sees:**
    - the situation (`PlanBriefing.Situation`);
    - a table of the civ's standing at each review (score rank, cities and techs vs rivals, military vs the strongest, wars);
    - the knobs, with their ranges and meanings;
    - its past reviews this game and what followed each;
    - its notes;
    - the 20 latest playbook lessons, same era first.
  - **It may change at most 3 knobs, each at most 2 steps.** Answers are checked and clamped.
  - **Cross-game memory.** At each review, the last review's outcome goes into the playbook (`AdvancedAiPlaybook.jsonl` beside C7.ini): situation, changes, outcome, gain and lesson.
  - The game doesn't wait for answers; trials do (`ModelPlanner.waitForAnswers`).
  - Cost: about 8 calls per Advanced player in a 400-turn game, and about 3,500–4,000 input tokens per call once the playbook fills.
  - **Live trial (ZUR-20, Haiku `claude-haiku-5-5`, 48 paired games at Regent, 200 turns): looked harmful.** Model-on minus rules-only per Advanced civ: score -329, cities -2.3, techs -2.8; worse in 37, 35 and 38 of 48 games. 624 calls, about2.84. 
  - **Re-check (ZUR-126): the harm didn't reproduce.** Against a rules-only re-run of the same 48 seeds on the same settings (bit-identical to the analyst's own rules-only files for seeds 1012–1047), model-on minus rules-only per Advanced civ is score +18.6 (SE 28), cities +0.1, techs +0.1: no effect either way. The games part ways right after the turn-50 review. The -329 most likely came from a mismatched baseline (difficulty 2 with tuned knobs); asked the analyst to confirm. Haiku's knob changes weren't logged, so they can't be recovered.
  - It stays off because it hasn't been shown to help. Each review is now logged (`review` lines in `C7_TRIAL_STATE`, `reviews` in `C7_TRIAL_JSON`: situation, briefing, knobs before and after, rationale, tokens), and `C7_TRIAL_POLICY_REPLAY=<state log>` re-applies recorded changes for free.
  - Before any new paid run (fresh plan, owner's approval): rank civs among all, not just met ones, in the past-review table; record failed calls so a civ doesn't ask again every turn; limit the model to the numeric knobs (no behaviour switches); prompt less war-leaning than "win against all others".
- **Tuning (`AdvancedTuning`).**
  - The planner's numbers, plus which unadopted on-trial AI behaviours Advanced uses.
  - Trials override them with `C7_ADVANCED_TUNING`.
- **Trials.**
  - `C7_TRIAL_ADVANCED=1` makes the trial half of the civs Advanced.
  - `C7_TRIAL_MODEL=1` lets them call Claude, with `C7_TRIAL_MODEL_NAME` and `C7_TRIAL_PLAYBOOK`. The results end with the call and token totals.
  - New measures: `rank` and `first`.

### Results
- **Regent, defaults, 16 paired games** (`C7_TRIAL_DIFFICULTY=2`), Advanced vs Standard:
  - score +87 ± 37;
  - cities +1.4 ± 0.6;
  - first place 13 points more often (about 23% of games vs 10%).

### Training ladder (`tools/advanced-ai-training/train.py`)
- Advanced (no bonuses) plays Standard AIs at Regent, Monarch, Emperor, Demigod, then Deity.
- **Search:** a coordinate search over the knobs in paired trials on training seeds. A change is kept only if it also helps on held-out seeds.
- **Pass:** an Advanced civ wins at least 100% / 90% / 75% / 50% of exam games (50% by chance) at Regent / Monarch / Emperor / Demigod and ranks clearly better. Deity is reported only.
- **If a rung isn't passed:** the ladder no longer stops (Owner order, ZUR-127/128). Nonstop mode (ZUR-129) starts a new cycle on fresh training seeds, picks up new code from `Development`, tries the lines in `<out>/ideas.txt` alongside the knob steps, and sits an exam after every kept change. It stops only on `<out>\STOP` or when the owner says so.
- **Status (2026-10-29):** stopped at Regent after 4 rounds; exam 80% won vs 100% needed, not passed. Kept: ThreatStrengthRatio 1.75, lean-military, BuildScience 55.
- **Status (2026-10-30):** restarted from the new defaults (Suede tips, `early-government` and `phase-plan` searchable) in `C:\Dev\ladder-runs\ladder-2026-10-30`, from worktree `.paperclip/worktrees/runner-ladder`. The 100-game Regent exam (ZUR-108 run 1, old knobs): Advanced won 63 of 100 (the 16-game exam's 80% was luck). In round 1 it crashed on a trial line that reads `-+0.00`; fixed, and it was restarted in nonstop mode on `Development` b5f6292e (ZUR-129). Round 1 reruns on the new code; the old code's partial round 1 is in `runs/stale-0eedc9b5`. Round 0 kept `trade-gpt`.
- **Progress page:** `python tools/advanced-ai-training/dashboard.py --out <ladder dir>` writes `dashboard.html` there (one offline page: Overview plus a tab per difficulty with ideas tried, best win rate, win rate by round, knobs) and a Findings tab from the hand-curated `findings.md` in the same folder; the CTO updates it every 20 minutes while runs go (routine "Training check-in", ZUR-128). The page shows when it was built and a Running/Stopped status from `heartbeat.json`; `train.py` rebuilds it every 60 s while it runs (`--dashboard-every`), an open page reloads itself and says when it is stale, and `dashboard.py --watch` rebuilds by hand (ZUR-104). A Runs tab lists every trial the ladder ran (`runs/<tag>.txt`), newest first, and shows the chosen run's settings, a sortable measures table with significant rows highlighted, and its wonders/techs/time footer; the choice survives a refresh (ZUR-112, `0b572ab9`, `2f967f42`).
- **Map of a game:** set `C7_TRIAL_MAP=<folder>` on a trial run to save one map file per game (borders, cities, units and each civ's techs, every `C7_TRIAL_MAP_EVERY` turns, default 10), then open the files in `tools/trial-map-viewer/index.html` (offline; turn slider, Advanced civs marked with a star). Recording doesn't change the game: seed 7500 replayed turn for turn. `state.log`'s `ai` column now shows each civ's real AI type; before, it said Standard for every civ (ZUR-121).
- **Turn progress of running games:** `python tools\trial-progress\watch.py` (`--once` for one snapshot) lists every trial game being played, with the game number, current turn, a bar where turn 450 = 100%, and runs done out of total. It refreshes every 5 s and also writes `C:\Dev\ladder-runs\progress.html`. It runs at low priority and reads only the end of each state log. For now it reads the state logs. The per-game progress file (ZUR-137) is still to come (ZUR-138).

### Next
- **Pass the Regent exam with Suede-based play, steered by benchmarks (ZUR-106, owner's direction 2026-10-30).**
  - What the exam shows (16 games, Advanced minus Standard): no real edge (score +29 ± 43, cities -0.2 ± 0.7); a smaller, more passive army (military -50 ± 11, wars 0.2 vs 0.6, captured -0.8 ± 0.3); about 135 of 200 turns in Despotism, same as Standard. It ran with none of the Suede tips that won their trials.
  - ZUR-107: `lux-slider-high`, `site-food` and `site-tight-7` on by default for Advanced; every on-trial switch becomes an Advanced behaviour the ladder can search; the Regent knobs become defaults. **Landed** (`1f021072`): AdvancedTuning and `train.py` list `lux-slider-high`, `site-food`, `site-tight-7`, `site-tight-12`, `trade-gpt`, `railroad-first`, `philosophy-first`, `early-rush`, `archer-rush`, `snipe-workers`; ThreatStrengthRatio 1.75, lean-military 1, BuildScience 55. Short check (4 games, 100 turns): switches on vs off differ (score +94 vs +82), too few games to size the effect. The ZUR-86 sell-to-all switch joins once ZUR-86 lands.
  - ZUR-108: a 100-game Regent exam before and after ZUR-107, with a loss autopsy.
  - ZUR-109: benchmark measures (`govTurn`, `cities50`, `cities100`, `techs100`, `workersPerCity100`, `techGold`, `killRatio`) and a scorecard and loss autopsy after every exam. **Landed** (`3c5f1503`, `9b10f9a9`): `AiTrialTest` records the seven measures (and each civ's final `govName` in the JSON); `tools/advanced-ai-training/benchmarks.py` writes `<exam>-report.md`, `train.py` runs it after each exam and links it from `log.md`, the dashboard has a Benchmarks tab. The Suede column stays blank until ZUR-110 writes `doc/suede-benchmarks.md`. Not yet run on a full exam.
  - ZUR-110: Suede's own figures for each benchmark (`doc/suede-benchmarks.md`), and the 5 ideas most likely to close the gaps. **Landed** (`ea42ea80`). Ticketed: idea 1 `republic-route` (ZUR-130), idea 3 `workers-1-2` (ZUR-131). Idea 2 (scouts for early contact) waits for the owner's ruling on Explorers.
  - ZUR-111: `early-government`, leave Despotism early. **Landed** (`3a5cc75c`), off by default.
  - ZUR-123: `phase-plan` (expand toward a rival, muster, first war, consolidate). **Landed** (`0eedc9b5`), off by default. Paired Regent trial: cities +1.25 ± 0.39, captured +1.27 ± 0.31, first war about 39 turns sooner; score +21 ± 30 (not clear). ZUR-124 (16 paired games each, 200 turns): Regent score +188 ± 34, rank -1.03 ± 0.31, captured +1.91 ± 0.38; Monarch score -216 ± 43, rank +0.69 ± 0.22 (worse), cities100 -2.50 ± 0.42; all civs at Regent (8 games) score 1077 vs 1180, units lost 12.6 vs 1.9. The first war fires (turn ~161-174) and takes cities in all three. Stays off by default; the ladder may still pick it. At Monarch civs sit in Muster for 25-65+ turns with settling cut back, so ZUR-148 keeps settling during the muster and gives it a time limit.
  - **Weighted rules (owner's idea, 2026-10-30).** The tips become weighted, conditional rules rather than a flat list:
    - ZUR-117 (Strategy Researcher): `doc/suede-rules.json`, every tip with category, base weight 0-100, conditions, exceptions, modifiers, conflicts, validation status (verified / unverified / disputed / disabled) and the code that does it today.
    - ZUR-118 (Engine Engineer): 7 category weights (expansion 95, workers 90, research 88, military 85, diplomacy 75, infrastructure 70, wonders 40) scaled by era, state and opportunity, wired to the existing levers; 50 = today's play. Logs which categories drove each decision (`ruleUse`). The ladder searches the weights and the policy review can adjust them. The owner's values become defaults only if a trial shows they help.
    - ZUR-119 (Trials Analyst): ablation, each category off in turn on the Regent exam seeds.
    - The model still doesn't pick actions each turn (Vox Deorum: per-turn deciding did worse, and costs about 100 times more). The engine does the arithmetic; the model steers weights at its 50-turn reviews.
  - **Game phases (owner's idea, ZUR-122).** What the AI wants at the start differs from later. A `phase-plan` switch (Advanced only, off until trialled) runs the owner's script:
    1. Expand to 3-5 cities, sited toward the nearest rival, with a road toward them.
    2. Muster: the capital and the 1-2 best production, lowest corruption cities build Barracks and 9-12 archers (more at higher difficulty).
    3. First war on a weak reachable civ, along the roads (re-uses `archer-rush`'s stack).
    4. Consolidate: fill the gaps, then muster again.
    - ZUR-123 (Engine Engineer) builds it; ZUR-124 (Trials Analyst) trials it at Regent, Monarch and all civs. ZUR-118's weights will scale by phase once both land.
- **Better play for Advanced only:**
  - expansion pace;
  - worker use;
  - war execution: artillery with stacks, defeat in detail, amphibious;
  - tech trading.
- **Suede tip switches waiting for paired trials** (off by default; a tip is kept only if the trial numbers show it helps):
  - `railroad-first` and `philosophy-first` (ZUR-33): +150 research value for Steam Power or Philosophy. Read `steamTurn`/`philTurn`, `techs`, `midtechs`, `beakers` and `score` at 300 turns.
  - `site-food` (ZUR-32): city sites scored food-first (centre tile's bonus food ignored, river/lake bonus, two 2-food tiles wanted). Trial ZUR-61: leans helpful alone; with `site-tight-7` (ZUR-62) cities +2.8 ± 0.8, score +158 ± 59. Both go on for Advanced in ZUR-107.
  - `site-tight-12` / `site-tight-7` (ZUR-58): sites 3-4 tiles from our own cities with at least 12 / 7 good food tiles get a flat +40 score. Landed; trial ZUR-62. Trial measures `size7`, `size12` added.
  - `lux-slider-high` (ZUR-31): luxury raised until no city is unhappy before entertainers. Trial ZUR-61: helps (score +204 ± 61, citizens +13 ± 6, gold per turn -9.6 ± 3.9). On for Advanced in ZUR-107.
  - `early-rush` (ZUR-30): while expanding, 5 attackers hit a weakly held capital or size 3+ city when our attack is 1.5x their defence by tech. Trial ZUR-65 (16 paired games, 300 turns): helps at Chieftain (captured +1.5 ± 0.3, rank −4.1 ± 0.4) and Warlord (captured +0.9 ± 0.4, rank −1.6 ± 0.4), but most of that gain is the Advanced AI beating weak opponents, not the rush. No clear effect at Regent (captured +0.7 ± 0.7; wars +0.8 ± 0.2, so it fires but rarely takes cities). All civs at Regent, same seeds: captured 2.9 vs 2.7, unitsLost 20.3 vs 15.6, score 1350 vs 1382 (means only, no error bars). Stays off; `archer-rush` is the follow-up.
  - `trade-gpt` (ZUR-29): AI tech deals pay gold per turn; trade at once on contact and on a new tech. New measure `techsBought`. Trial ZUR-66, paired and all civs.
  - `early-government` (ZUR-111): research bonus (+400) for the techs on the way to the best government within 4 techs, and a revolt the moment it is known (no margin; not while losing a war). Landed, off by default; the ladder can search it. Paired trial ZUR-113 (16 games, Regent): despotism -14.0 ± 6.2 turns, score +41 ± 34, rank -0.4 ± 0.2; anarchy unchanged at ~8 turns.
  - `workers-1-2` (ZUR-131, Suede idea 3): Advanced builds workers up to 1 per city while expanding, 1.2 after (Standard keeps Civ3's 0.85). Landed, off by default; the ladder can search it. Paired trial ZUR-132 (16 games, 200 turns, Regent): score +78 ± 21, cities +1.3 ± 0.5, citizens +8.5 ± 2.8, rank -0.4 ± 0.2, gold no clear change. Helps. That trial ran on a branch without the ZUR-109 measures, so `workersPerCity100` comes from the ladder's exams.
- **Owner's ideas from watching Advanced games (ZUR-56)**, built and trialled one at a time, in build order (archer-rush, then 2, 5, 3, 6):
  1. `site-tight-12` / `site-tight-7` (ZUR-58, trial ZUR-62): sites closer to our cities where they keep 12 (or 7) good food tiles. New measures `size7`, `size12`.
  2. `lean-buildings` (ZUR-59, trial ZUR-63): no Walls in interior cities, Temples/Colosseums only when needed, no Barracks in low-shield cities.
  3. `siege-escort` (ZUR-60, trial ZUR-64): catapults never alone outside borders. New measure `siegeLost`; also an all-civs run.
  4. `archer-rush` (ZUR-67, after ZUR-58): the Owner's later idea, archers stacked early against a civ within 10 tiles (6 archers and a defender, small cities may be razed, keeps going after a win). Read with the `early-rush` trials at low difficulty. Landed, off by default; new measures `razed` and `eliminated`. Paired trials at Chieftain, Warlord and Regent (ZUR-67): the rush rarely fires, no measurable gain. Stays off.
  5. `lean-garrison` (ZUR-71, trial ZUR-73): Advanced AI only. Interior cities keep 1 defender (2 at war or with an enemy close), border cities 2 (3 at war); the shields go to attackers. The Owner saw 20 Infantry in a city far from any enemy. Standard keeps Civ3's 2 defenders + 1 attacker per city. Shares the interior-city test with `lean-buildings`. Paired and all civs.
  6. `grow-to-settle` (ZUR-72, trial ZUR-74): Advanced AI only. Fast-growing cities build Settlers and Workers to shed population; specialists only when the luxury slider at its cap can't stop disorder. Trialled on top of `lux-slider-high`.
- **More ideas from how players beat the AI:** the Strategy Researcher's third pass (ZUR-68, `doc/ai-strategy-notes.md` tips 99-127) ranks 8 ideas. Next to build:
  1. `snipe-workers` (#105): at war, a spare unit kills or captures an unguarded rival worker or settler, never emptying one of our cities and not when an enemy fighter nearby would likely win. Landed off by default (ZUR-84); new measure `workersCaptured`. Paired trial ZUR-99.
  2. `stage-on-defence` (#101): the rush stack waits on the best defensive tile, so the AI's piecemeal counterattacks die on it.
  3. `sell-to-all` with `gpt-probe` (#111, #113): sell a new tech to every civ in one turn and find the most gold per turn each will pay. Check what `trade-gpt` already does first.
  - Its #2 `rush-archers` is mostly `archer-rush`; its two extra points (strike before the target has Bronze Working; go while the target has 2 cities or fewer) are follow-ups if the `archer-rush` trial asks for them.
- **Human-proxy opponents:** the C7AI bots (C:\Dev\C7AI) over LAN.

### Not covered
- Civ3 imports and .biq scenarios always use Standard.
- LAN guests can't see or change the AI type in the lobby.
