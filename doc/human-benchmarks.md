# How strong humans play: benchmarks from CivFanatics' Conquests COTM

**Short answer.** Strong human players found cities at about the Advanced AI's pace up to turn 100: a median of **4 cities at turn 50 and 13-16 at turn 100**, against the Advanced AI's ~4 and ~11.5-12. After turn 100 the two part ways. The Advanced AI levels off at ~17 cities. The humans reach a median of **~30 cities at turn 150 and ~54 at turn 200**.

The gap is closed by **two things at once**:
- **War from about turn 100.** The median first city taken from another civ is at turn 107. Half the cities humans end with were captured.
- **Settling that never stops.** Humans found another 5-12 cities in every 25 turns after turn 100, on razed and conquered land, in the gaps, and overseas. Founded cities alone (not counting captures) are 22 at turn 150 and 29 at turn 200, more than the Advanced AI's 16.7 and 17.4 total.

The conquest armies are **masses of one fast attacker**: 40-110 Horsemen, Knights, Cavalry or a mounted unique unit, carried by Galleys, with little siege.

Built 2026-11-01 (branch `human-gotm`). The data is in `doc/human-benchmarks.json` and the tools are in `tools/human-games/`.

## Sources and method

- **CivFanatics GOTM site, Conquests COTM 1-176** (2004-2026).
  - Each game has two pages:
    - a game page: civ, difficulty, map, rule changes. Example: COTM 176, https://gotm.civfanatics.net/game-info/game-details.php?month=30176
    - a results table: player, score, victory type, finish date, final save. Example: https://gotm.civfanatics.net/results/index.php?month=30176
  - COTM N is month 30000+N on both pages.
  - All 176 games' pages were fetched (`tools/human-games/fetch_gotm.py`). That is 5,170 entries in total.
- **Final saves.** 132 were downloaded from 35 games, using `tools/human-games/download_saves.py`:
  - **36 at Regent, 25 at Monarch, 27 at Emperor, 18 at Demigod and 18 at Deity** (each about 5-9 games).
  - Up to 4 per game, taken from the open-class *winners*: 1st, 2nd, then spread down to the median entry.
  - 7 more saves (COTM 6 and 34) returned 404.
  - 8 saves could not be parsed: QueryCiv3 found "no header where one was expected" at about 2.2-3.0 MB into the decompressed file (one each from COTM 8, 24, 34, 40, 54, 71, 77, 89).
  - **124 saves from 33 games were read.**
- **What the saves give.** A Conquests save keeps the whole game's replay (RPLS):
  - every city founded, captured or destroyed, and every tile that changes owner, by turn;
  - the histograph: score and power per civ per turn.
  - `tools/human-games/GotmReader` plays the replay forward. A city belongs to whoever owns its tile, the same rule as `C7Engine/C7GameData/ReplayPlayback.cs`. From that it reads each civ's cities, captures and territory at every turn.
  - **Check:** the replay's city count at the save's own turn equals `LEAD.NumberOfCities` in **all 124** saves.
- **What the saves don't give.**
  - **Government changes and war declarations** are not in the replay. The government turns below come from the players' write-ups. "First war" is measured as the **first city captured from another civ**, which comes later than the declaration.
  - **Unit counts** are only available for the final turn.
- **Player write-ups.** The COTM spoiler threads on forums.civfanatics.com cover about 17 games and 40 players; links are given at each figure. The quotes come through a summarising fetch tool, so the wording may differ slightly from the posts.

**Calendar.** Civ3's default calendar runs 540 turns, starting at 4000 BC (`tools/human-games/civ3_calendar.py`). Source: https://forums.civfanatics.com/threads/time-options-time-scale.456771/ The calendar was checked against a save: COTM 176, I.Larkin, turn 126 = 30 BC.

| Turn | 25 | 50 | 75 | 90 | 100 | 125 | 140 | 150 | 175 | 200 |
|---|---|---|---|---|---|---|---|---|---|---|
| Year | 2750 BC | 1750 BC | 1125 BC | 750 BC | 550 BC | 50 BC | 250 AD | 350 AD | 600 AD | 850 AD |

**How these games differ from the AI trials.**
- **Map:** every save read is 100x100, the same as the trials. The game pages give 60-80% water, mostly Continents or Pangaea.
- **Rivals:** 7 AIs in 93 of 124 saves, 6-11 in the rest.
- **Civ:** one civ is set for everyone in each game, and there are small rule changes (for example, "an additional Worker has been given the Indian nation", COTM 176).
- **AI bonuses:** the humans face the AI's bonuses at the stated difficulty. The Advanced AI plays with Regent's (none), so **Regent is the fair comparison**.
- **Scoring favours early finishes.** Players are ranked by "Jason score", which rewards finishing early (https://gotm.civfanatics.net/games/score.php). This pushes them toward early conquest or domination. It is part of how strong humans play these games, but it is a bias.
- **Samples shrink at later turns.** A game that ended (for example, a domination win) drops out of the later turns. At turn 150, 117 of 124 saves are still playing; at turn 200, 83 are. Many of the dropped games are the fastest conquerors, so the medians at turns 175-200 are **lower bounds** for the strongest play.

## Table: human medians by difficulty (from the saves)

Median [interquartile range], n = saves still playing at that turn. "AI" is the Advanced AI: Regent, 8 civs, 100x100 Continents, 200 turns (`doc/ai-roadmap.md` Track 2; ZUR-236 baseline).

| Measure | Regent (36 saves, 9 games) | Monarch (25, 7) | Emperor (27, 7) | Demigod (18, 5) | Deity (18, 5) | Advanced AI |
|---|---|---|---|---|---|---|
| `cities50` | 4 [3-4] | 3 [3-4] | 4 [3-5] | 4 [4-5] | 4.5 [4-6] | ~4 |
| `city5turn` | 60.5 | 60 | 54 | 52.5 | 50 | — |
| `city10turn` | 87 [82-96] | 86 [81-102] | 83 [74-91] | 80 [75-85] | 71 [64-78] | ~108 (Suede doc's exam) |
| `cities100` | 13 [10-15] | 14 [9-18] | 14 [11-17] | 14.5 [13-18] | 16 [14-20] | ~11.5-12 |
| city20turn | 127 [117-144] | 123 [107-136] | 119 [103-133] | 112 [108-130] | 110 [100-136] | never (plateau ~17) |
| cities150 | 28 [20-38] (n35) | 34 [23-49] (n24) | 30.5 [25-45] (n24) | 41.5 [28-60] (n16) | 40.5 [24-57] (n18) | ~16.7 |
| cities200 | 57 [37-85] (n29) | 48 [35-89] (n16) | 62 [36-77] (n17) | 64.5 [48-119] (n10) | 54 [37-94] (n11) | ~17.4 |
| founded by turn 100 / 150 / 200 (not counting captures) | 12 / 21 / 30 | 13 / 25 / 31.5 | 12 / 22 / 29 | 11.5 / 19.5 / 23 | 16 / 22 / 29 | (all founded) |
| captured by turn 150 / 200 | 4 / 24 | 10 / 24.5 | 12.5 / 27 | 22.5 / 50 | 16.5 / 23 | ~0-2 |
| first city taken from a civ (turn) | 122 [97-137] | 112 [89-139] | 100 [82-114] | 96.5 [88-102] | 109.5 [103-131] | rare |
| share with a capture by turn 100 | 25% | 36% | 52% | 72% | 11% | — |
| captured share of all cities gained (end) | 0.58 | 0.57 | 0.52 | 0.58 | 0.51 | ~0 |
| that game's AI civs, mean cities at turn 100 | 7.4 | 7.9 | 8.3 | 10.5 | 11.7 | — |
| power vs strongest AI at turn 100 / 150 | 1.2 / 1.8 | 1.3 / 2.3 | 1.2 / 2.1 | 0.8 / 1.7 | 0.7 / 1.7 | — |
| government at the end | Republic 33, Monarchy 3 | Republic 20, other 5 | Republic 23, Monarchy 2, Democracy 2 | Republic 10, Monarchy 5, other 3 | Republic 15, Monarchy 3 | — |

**All 124 saves pooled:** cities 4 at turn 50, 14 at turn 100, 30 at turn 150 (n117), 54 at turn 200 (n83). 5th city at turn 55.5, 10th at turn 83. First capture from a civ: median turn 107 (quartiles 93-129). Every one of the 124 took at least one city.

**Top 3 of each field against the rest** (43 and 81 saves):
- cities at turn 100: 17 against 13;
- cities at turn 150: 57.5 against 28;
- first capture: turn 100 against turn 114.

Per-save figures come from GotmReader, and each row is in `doc/human-benchmarks.json` → `saves`. Some examples to check against:
- COTM 169, choxorn (Regent): 14 cities at turn 100, first capture turn 182, 36 cities at turn 200. In his write-up (below) he says he first went to war in 670 AD, which is turn 182.
- COTM 171, templar_x (Emperor): first capture turn 92, Conquest at turn 137, ending with 110 Horsemen.

## Victory types and finish turns (published result tables, all entries)

From the result tables of all normal games. Open class only. Variant games are left out (one-city, scenario, custom-rule and mod games: about COTM 97-118 and a few others). Finish turns are converted from the posted dates, assuming the default calendar.

| Difficulty | Games | Entries | Wins | Losses | Conquest or Domination, all wins | ... top third of each field | Median finish turn: Conquest / Domination / Spaceship / Diplomatic |
|---|---|---|---|---|---|---|---|
| Regent | 27 | 835 | 797 | 23 | 52% | 79% | 209 / 262.5 / 372.5 / 332.5 |
| Monarch | 38 | 1582 | 1434 | 118 | 55% | 79% | 199 / 262 / 394 / 331.5 |
| Emperor | 31 | 593 | 488 | 72 | 53% | 76% | 211.5 / 235 / 359 / 297 |
| Demigod | 26 | 375 | 254 | 91 | 44% | 74% | 223.5 / 223 / 330 / 271 |
| Deity | 17 | 254 | 170 | 69 | 55% | 72% | 227 / 232 / 326 / 281 |

These tables include every entrant, weak players too. The "top third" column ranks each field by Jason score.

## When the land runs out

Humans don't wait for the land to run out before going to war. They are still founding cities when the first war starts, and they keep founding afterwards.

1. **Expansion to turn ~100 is at the AI's pace, and a little faster.**
   - They reach the 10th city at turn 83 (pooled), against about turn 108 for the Advanced AI.
   - They found 3-5 cities in every 25-turn window up to turn 100 (Regent: 3.5, 3.2, 5.0).
   - Write-ups give 8-13 cities at 1000 BC (turn 80):
     - "12 cities at 1000 BC" (COTM 33, Honda[SU]), https://forums.civfanatics.com/threads/cotm33-first-spoiler.205994
     - 9-12 cities at 1025-1000 BC (COTM 40, klarius, Più Freddo, eldar, ignas), https://forums.civfanatics.com/threads/cotm40-first-spoiler.242362
     - 13 at 1000 BC (COTM 171, templar_x), https://forums.civfanatics.com/threads/cotm-171-portugal-emperor.697043/
2. **The first war starts while the last sites are being filled.**
   - The median first capture from a civ is at turn 107. It is earlier on the harder levels (Emperor 100, Demigod 96.5) and later at Regent (122).
   - By then the human has founded a median 14.5 cities (pooled; Regent 17), and **4 of them in the 25 turns just before**.
   - **So the trigger is not "no sites left". It is a neighbour in reach and an army ready**, while the core fills in. Write-ups put the end of core settling at about 1200-750 BC (turns 70-90). Example: COTM 40, Più Freddo: last founding 825 BC (turn 87), first war long before (UNVERIFIED by save; COTM 40's saves could not be parsed).
3. **There are two styles; the saves show both, and every save has captures.**
   - **Rushers.** They declare early (turns 60-105) on the nearest civ:
     - with archers or swordsmen first: "Russia went down easily from the Swordsmen and Archers I built in advance" (COTM 62, MeteorPunch), https://forums.civfanatics.com/threads/cotm62-first-spoiler.327891
     - then with masses of horse units: COTM 171, templar_x, about 150 horses for the 30 AD push, "GREECE IS OUT, conquest reached". In his save: 110 Horsemen at the end and Conquest at turn 137.
     - COTM 35: "my horsemen captured the other four Zulu cites in four turns" (PrinceMyshkin, about 590 BC, turn 98), https://forums.civfanatics.com/threads/cotm35-second-spoiler.216350
   - **Builders.** They fight only defensive wars at first and make their first real offensive around turns 135-185 with medieval units.
     - COTM 169, Regent, choxorn: about 24 cities by 250 AD (turn 140) with "almost all the land settled", Republic at 30 BC (turn 126), first war in 670 AD (turn 182). His save agrees: first capture turn 182. https://forums.civfanatics.com/threads/news-cotm-169-arabs.692280/ (page 1 #20, page 3 #42-53)
     - COTM 161, Takeo: "didn't decide on Conquest until well after 10 AD", with offensives from 450 AD (turn 160). https://forums.civfanatics.com/threads/cotm161-persia-regent.679224/
4. **After the first war they keep settling.**
   - In the 50 turns after the first capture, humans found a median 8 more cities (6.5-10 by level): on razed sites, in gaps between captured cities, and on empty land of the beaten civ.
   - COTM 169, choxorn: after the second war "refilled the empty former Greek land with new cities".
   - At Regent, founding per 25 turns goes 5.9 (turns 100-125), 6.2 (125-150), 9.8 (150-175), 8.1 (175-200). Captures go 2.5, 6.7, 11.1, 12.3 over the same windows.
   - About half the final empire is captured and half founded.
5. **What they fight with.**
   - The final units of the 31 saves that ended by turn 185 (all Conquest or Domination wins) are dominated by **one fast attacker in bulk**:
     - Knights 37-72 (COTM 7, 17, 42, 50, 57, 78)
     - Cavalry 40-96 (COTM 13, 49, 50, 54, 89, 171, 174)
     - Horsemen 72-110 (COTM 56, 171)
     - a mounted unique unit: Keshik 84, Mounted Warrior 56, War Elephant 55-58, War Chariot 73, Samurai 54, Hussar 88
     - Medieval Infantry 65 (COTM 20)
   - Most also have 8-25 Galleys; 6 of the 31 have none.
   - Siege units are few: only 12 of the 31 have 3 or more Catapults, Trebuchets, Cannons or Artillery, and none has more than about 15.
   - Write-ups: COTM 76, Lanzelot used "about 10 Sipahi Armies". COTM 169, jasa_m won with "catapult and swordsman spam" (UNVERIFIED by save).
6. **Government.**
   - Most write-ups switch to **Republic at 1750-1200 BC (turns 50-72)**, often through the Philosophy free tech ("Went for, and got, the Republic slingshot", COTM 57, The Professor, https://forums.civfanatics.com/threads/cotm57-first-spoiler.309484/).
   - Monarchy is used for long wars on the hard levels: "stayed in Monarchy the whole game ... almost constantly at war" (COTM 172, LaSalle, https://forums.civfanatics.com/threads/cotm172-sumeria-demigod.698801/).
   - The saves agree at the end: Republic in 101 of 124, Monarchy in 15 (5 of the 18 at Demigod).
   - **No turn for the switch can be read from the saves (UNVERIFIED).**
7. **Peaceful players.**
   - In the result tables, Spaceship, Diplomatic and Cultural wins are about 45% of all wins, but only 21-28% of each field's top third.
   - Even the peaceful winners in the saves took cities: COTM 161, Più Freddo, Spaceship: 58 captured, and 133 cities at turn 200.

## What this means for the Advanced AI

- **The gap opens at turn 100-125, not before.** At turn 100 the AI is about 1-2 cities behind the human median (11.5-12 against 13-14). By turn 150 it is about 13 behind.
- **About half of that gap is settling the AI doesn't do.**
  - The humans' *founded* cities alone grow from 13 (turn 100) to 22 (turn 150) and 29 (turn 200), while the AI stops at 17.
  - This fits the roadmap's finding that the Advanced AI's open sites hit 0 at turns 100-125. Humans make new sites: they raze and refill, take the beaten civ's empty land, and settle overseas.
- **The other half is conquest.**
  - It starts at a median turn of about 107 (Regent 122), by which time the human has a power lead of about 1.2x over the strongest AI.
  - The army is a stack of 40-100 copies of the era's best fast attacker. That is far larger than `prey-war`'s 8+ attackers.
- **Benchmarks to add:** `cities150` (human median ~30), `cities200` (~54), `city20turn` (~120), `firstCaptureTurn` (~107), `foundedAfter100` (~10-15 by turn 150).

## Wiring into benchmarks.py

The `doc/human-benchmarks.json` → `targets` field holds the per-difficulty medians under the names benchmarks.py already uses: `cities50`, `cities100`, `city5turn`, `city10turn`.

For a "Human" column next to "Suede" (not done here):
- add `--human doc/human-benchmarks.json --human-level Regent` to `benchmarks.py`;
- read `json.load(...)["targets"][level]` into a dict shaped like `load_targets`'s result;
- print it as a fourth column in `scorecard()`.

`cities150`, `cities200` and `city20turn` need the trial's state log (cities by turn, which the autopsy already reads at turns 50/100/150) before they can be compared.

**To re-run** (the downloads stay in a scratch directory, never in the repo):
1. `python -I tools/human-games/fetch_gotm.py <cache> <cotm_all.json>`
2. `python -I tools/human-games/download_saves.py <cotm_all.json> <saves> 4 <COTM numbers>`
3. `dotnet run -c Release --project tools/human-games/GotmReader -- <conquests.biq> <saves>/*/*.SAV > reader.jsonl`
4. `python -I tools/human-games/analyze.py <cotm_all.json> <saves>/manifest.json reader.jsonl doc/human-benchmarks.json`

The COTM numbers used were:
- Regent: 20 33 44 50 56 62 161 169 173
- Monarch: 6 10 12 24 34 40 57 64 71
- Emperor: 7 17 30 42 77 171 175
- Demigod: 8 27 54 78 176
- Deity: 13 49 73 89 174

## Not obtained

- Government-change and war-declaration turns: not in the save, only in write-ups.
- Unit counts before the final turn.
- Saves of COTM 6 and 34 (404) and 8 unparsable saves (QueryCiv3 header error, not investigated).
- The game-page statistics and "spotlight" pages: the site has turned them off.
- COTM's three classes: only open-class entries were used. Conquest class is easier and Predator harder.
- A difficulty split of the write-ups: many posts don't say which class or level they played.
