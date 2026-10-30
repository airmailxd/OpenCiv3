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
- **Policy review by Claude (`PolicyReview`, `PolicyPlaybook`)**, used when a key is set (Settings → Advanced AI, or `ANTHROPIC_API_KEY`). The owner's design, 2026-10-29: Claude doesn't play the game. It adjusts behaviour by time and situation, building on past success in small steps.
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
  - Not yet tried against the live API.
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
- Advanced (no bonuses) plays Standard AIs at Regent, then Monarch, then Emperor.
- **Search:** a coordinate search over the knobs in paired trials on training seeds. A change is kept only if it also helps on held-out seeds.
- **Pass:** an Advanced civ wins at least 75% of exam games (50% by chance) and ranks clearly better.
- **If a rung isn't passed:** the AI needs better play, not better knobs.

### Next
- **Better play for Advanced only:**
  - expansion pace;
  - worker use;
  - war execution: artillery with stacks, defeat in detail, amphibious;
  - tech trading.
- **Suede tip switches waiting for paired trials** (off by default; a tip is kept only if the trial numbers show it helps):
  - `railroad-first` and `philosophy-first` (ZUR-33): +150 research value for Steam Power or Philosophy. Read `steamTurn`/`philTurn`, `techs`, `midtechs`, `beakers` and `score` at 300 turns.
- **Live model trial**, with a key: compare the cost per game and the result against rules alone. Run trials one after another so the playbook builds up.
- **Human-proxy opponents:** the C7AI bots (C:\Dev\C7AI) over LAN.

### Not covered
- Civ3 imports and .biq scenarios always use Standard.
- LAN guests can't see or change the AI type in the lobby.
