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

**A. Strategic plan per player (`StrategicPlan`, saved).**
- Fields:
  - weights for growth, expansion, production, science, gold, wonders, culture, offense, defense, naval and air;
  - a victory goal;
  - attitude toward each rival;
  - optional overrides for next tech and next government;
  - a set turn and an expiry.
- Read by production (`ChooseProducible`), settling, war plotting (`StockWarPlotting`/`WarCampaign`), research and government.
- The neutral plan reproduces the Standard AI exactly.
- It replaces the unused `StrategicPriorityArbitrator` output.

**B. AI type per player** (Standard / Advanced) — done. Not covered: Civ3 imports and .biq scenarios always use Standard; LAN guests can't see or change it in the lobby.
- Set in the setup screens: `PlayerSetup`, `MultiplayerSetupScreen`, `NewGameChoices`, `ScenarioSetup`.
- Stored on `SelectedOpponent` → `Player` → `SavePlayer`. It is nullable, so old saves load as Standard.

**C. Planner.**
- The **rule-based planner** is used when no API key is set. It reacts to the situation: losing a war → defense; boxed in → overseas expansion; behind in tech → science.
- The **model planner**:
  - Runs every 5 turns, and also on a war or peace, a city lost or captured, contact made, or a new era.
  - Gets a Markdown summary of what the player can see:
    - the civ, a few lines per city, and rivals with attitude and power;
    - threat zones and victory progress;
    - events since the last decision;
    - the legal options;
    - its own last plan with its reasons.
  - Answers in one structured call. The answer is validated and clamped, and only changes beyond a deadband are applied.
  - Runs asynchronously; a late answer applies the following turn.
  - Keeps a per-civ notes file between calls.
- Beyond-Civ3 fixes are switched on only for Advanced AIs: amphibious invasions, pillaging, retreating damaged units.

**D. Evaluation.**
- Trials with mixed seats (Standard vs Advanced), measuring score, cities, wars won and survival over many seeds.
- Controls: `none` (no planner) and `null` (neutral plan).
- Per-call cost and latency are logged for the model planner.
