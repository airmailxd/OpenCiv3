# Suede rule registry (ZUR-117)

`doc/suede-rules.json` holds one entry for each of the 127 tips in `doc/ai-strategy-notes.md` (the owner's numbers 1-98 are the same tips). It is data, not code: nothing reads it yet. ZUR-118 (category weights) and ZUR-119 (ablation) are the intended users.

## Counts

- By category: diplomacy 29, expansion 18, infrastructure 10, military 40, research-economy 13, wonders 5, workers 12.
- By `implemented`: no 50, partly 59, yes 18.
- By `validation`: disabled 9, disputed 10, unverified 102, verified 6.

## Fields

| Field | Meaning |
|---|---|
| `id`, `name`, `category` | Tip number from the notes. Category is one of expansion, workers, research-economy, military, diplomacy, infrastructure, wonders. |
| `base_weight`, `weight_reason` | 0-100. The owner's values are used where the owner gave one (20 tips); the rest are my proposals with a one-line reason. Category starting weights: expansion 95, workers 90, research-economy 88, military 85, diplomacy 75, infrastructure 70, wonders 40. My proposals are not trial results. |
| `phase` | Era or game stage where the tip applies. |
| `conditions`, `exceptions` | When it applies, and when it must not. |
| `modifiers` | `{fact, factor, engine_computable}`: multiply the weight by `factor` when the fact is true (contested land x1.2, scarce land x0.5, invasion threat x0.4). Fact names are in the `facts` table at the top of the JSON, each with how the engine would compute it. |
| `not_computable_today` | Facts named in the conditions that the engine cannot compute today (see below). |
| `validation` | `verified` = a Civ3 mechanic confirmed in `doc/civ3-compendium/` (cited in `validation_note`). `unverified` = Suede's claim only. `disputed` = Suede's videos or other sources disagree. `disabled` = not usable by the AI (human-only, stock-AI behaviour, or doubtful). Per standing rule 5, no tip is `verified` on Suede's word alone. |
| `code`, `implemented` | The switch, knob or class that does it today (`none` if nothing); `yes` / `partly` / `no`. A switch that exists but is off by default (for example `early-rush`) counts as implemented; the `code` text says it is off. |
| `evidence` | Trial result with ticket and numbers, else empty. Only 7 tips have one; trials of `trade-gpt`, `snipe-workers`, `railroad-first` and `philosophy-first` are still pending. |
| `metrics` | Benchmark measures that should move (names as in `AiTrialTest`; `civCaps` and `land` and `luxuries` and `production` are not recorded today and need adding). |
| `conflicts` | Other tip ids and a rule for deciding between them from game state. |

## Facts the engine cannot compute today

- `enemyDefenderTech`: the AI sees only the units it has met, not the rival's tech list. Affects #35, #81, #100. Workaround: use the best defender actually seen.
- `richestBuyer`: rival treasuries are not exposed to the AI. Affects #50. Workaround: the buyer's last paid price.
- Measures not in `AiTrialTest`: `civCaps`, `land`, `luxuries`, `production`, `warships` counts by type.

## The owner's conflicts

- **#19 vs #92.** Decide by phase and city count: while expansionPhase is on and we own fewer than 4 cities, #92 wins (first worker after the first settler, second after 3-4 cities). Once 4 or more cities, or the expansion phase ends, #19 wins (1.5 workers a city, more in corrupt cities, more at high difficulty). The roadmap's trial of a lower worker target (0.8-0.9 a city) was neutral, so keep #19's count a tuning knob, not a fixed 1.5.
- **#23 vs #75.** Republic (#23) is the default only at peace. #75 overrides it: never switch government during a war, and stay out of Republic when atWar with invasionThreat above 1.0. After the war, switch.
- **#23 vs #76.** #76 prefers Republic outright, #23 keeps Monarchy as a fallback. Decide from state: Republic when at peace with a short list of enemies and core cities at size 5 or more (#73); Monarchy when atWar, or when Republic is not yet known and Despotism has lasted past turn 60. Videos disagree, so the ladder should search this switch point.
- **#28 vs #51.** Both state the same gold-per-turn trick; #51 adds the dispute (lump sum versus GPT). Decide from the buyer: when the buyer's surplus covers the price over 20 turns use GPT, otherwise take a lump sum. Measure both arms in the trade-gpt trial (techGold, gpt).
- **#64 vs #68.** Great Library (#64) versus not delaying expansion for wonders (#68). #68 wins while expansionPhase is on. #64 applies after expansion, only when at least two rivals know techs we lack (the library gives techs two or more civs know) and the city has library and university.
- **#83 vs #24.** Keeping a full-health Army in border cities (#83) costs unit support, which #24 cuts. Decide by threat: keep the army only in border cities with invasionThreat above 1.0; elsewhere #24 wins.
- **#98 vs #1.** Goody huts (#98) can give a free city, which supports #1, but popping one while a settler is in production wastes the reward. Decide per hut: pop at once if our city count is at or below the average and no settler is in production; otherwise delay until the settler is done or switch the box. Disputed (SvjbJ0 differs), so keep weight low until a trial.

Pairs the owner named by single numbers:

- **#64 (Great Library, weight 50).** Conflicts with #68 and #67 (above), and with #1 (expand first): it applies only after expansion and only while rivals know techs we lack.
- **#83 (full-health Army).** Conflicts with #24 (few units) and #44 (heal first), above.
- **#98 (goody huts).** Disputed; conflicts with #1 as above.

Other conflicts and duplicates are listed in each entry's `conflicts`. Where the deciding rule is "apply the higher base_weight", the tips are near-duplicates (#6, #91, #127; #35, #81, #99, #100).

## Caveats

Weights outside the owner's list are my judgement, not measurements. A tip's weight should change only on trial numbers (ZUR-119 ablation). Six entries are `verified`, all citing the compendium; everything else waits for a check against the game.
