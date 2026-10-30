# Suede's own numbers: targets for the benchmarks (ZUR-110)

**Short answer.** Suede almost never says a turn number aloud (the turn counter is only on screen, and we read captions, not video). So most of the "cities at turn 50/100", "Monarchy turn" and "Steam Power turn" figures **cannot be read from the captions**. What the captions do give are the *state* checks he states himself: cities and workers at the end of expansion, how he leaves Despotism and into what, how many workers he wants, what he sells techs for. Where a turn figure is missing, the table says so; nothing is invented. Figures marked **GUESS** are my reading of the captions, not his words.

Captions re-read for this ticket (2026-10-30, auto-captions, no video watched): YZDmlnJSVdE (Over-explaining the Expansion Phase, Regent, Portugal, standard size, 80% water Pangaea), MnfWobW2zyc (Expansion Phase, Archipelago), 8WeGVE84mVU (101 Tips), -1A9i20JFBs (Top 7 Mistakes), 3nlb-_3N8WI (7 Mistakes by Intermediate Players), B_N_SmT9ZIc / xZEd7dxXaK4 / CQ3I4OSkD3c (Hardest Difficulty Done Right 1-3, Sid), yggDJgeF0Ho (60% Water Demigod), jhxl7CA7Q5k (Emperor speedrun), u_B2rs8mGGY (No Frills Deity), -NssY4xLWNs (Winning on Sid), DHDbdRq3OBw (Gandhi on Sid 1), LeBaV5ihB_o (midgame economy).
Video IDs are abbreviated as in `doc/ai-strategy-notes.md`. Times are m:ss into the video.

## Table

Measure names are the ones in the ZUR-109 list (`govTurn`, `cities50`, `cities100`, `techs100`, `workersPerCity100`, `techGold`, `killRatio`) plus `steamTurn`/`philTurn` from the existing trial.

| Measure | Suede's figure | Difficulty | Map | Source (video, time) | Confidence |
|---|---|---|---|---|---|
| `cities50` | **No figure stated.** He says to build a settler the turn a city reaches size 3 and to take all the land; his first settler comes out ~turn 16-17 (he says "3200 BC" at 12:14, which is turn ~17; the captions read "turned 60", so this is **GUESS**) after growing in 7 turns and building the settler in 12. | Regent | Standard, 80% water Pangaea | YZDmln 6:17-6:40, 12:14 | Low |
| `cities100` | **No turn stated.** End of expansion (when he stops settling and starts wonders, courthouses) = **13 cities** counted at 63:50 of the video; he aims for **20-25 cities** on a normal map and **about 24** on an archipelago map. The in-game turn at that point is not said. | Regent | Standard Pangaea (13); Archipelago (24) | YZDmln 63:44-64:07; Mnf 21:57-22:05, 38:06, 48:05 | Medium for the counts, none for the turn |
| cities by end of expansion (Demigod) | **15 cities, 11 workers** at 74:26 of the stream; "we need to catch up" on workers. | Demigod | 60% water | yggDJg 74:22-74:34 | Medium (count), none (turn) |
| `govTurn` / leaves Despotism | Leaves Despotism **at the end of expansion, for Republic**, not Monarchy ("play Republic, don't play Monarchy" - 162:05; Monarchy "is that bad, yes it is" - 174:26). In the Regent expansion game he switches at 73:11-74:11, after the dyes are hooked up (+happiness), after worker moves are done, and after making peace. Turn not said. Under Despotism the third food/shield/commerce on a tile is lost (-1 on 3+). | Regent (YZDmln); Deity (u_B2rs) | Pangaea | YZDmln 71:51-74:27; u_B2rs 162:05, 174:26; Nss 14:40 ("I am in Republic now") | High for *what*, none for *when* |
| `govTurn` (speedrun) | Switches to Republic "pretty early", paying about 44 gold/turn of unit support the first turn and losing 2-3 cities' worth of upkeep; he says he would not plant those cities again. Emperor, default rules, diplomatic victory in 58 min real time. The speedrun is not typical play. | Emperor | Standard | jhxl7C 15:42-16:08 | Medium |
| Research path to Republic | **Writing, Alphabet first** (expensive, low AI priority, so good trade bait), then **Code of Law before Philosophy**, so that Philosophy's free tech is **Republic**. On Demigod or higher go straight to Philosophy, because the AI may take it first. | Regent (rule); Demigod (exception) | any | YZDmln 3:30-4:07 | High (stated rule) |
| Monarchy turn | **No figure.** He avoids Monarchy; he trades for it only to give the AI ("give them monarchy for engineering") or as a stepping stone when forced. | Demigod | any | Kszjb6 106:37; yggDJg 77:46-77:57 | High that he skips it |
| Currency turn | **No figure.** Calls Currency, Pottery and Mathematics cheap techs to sell early for gold. | Regent | any | notes #58 (YZDmln 53:25, 68:04) | Low (no timing) |
| `philTurn` | **No turn.** Philosophy is the one tech he runs at full science and holds the 50-turn minimum for ("full-tech only Philosophy"). Free tech = Republic on Regent. | Regent and up | any | notes #60; YZDmln 3:48 | Medium |
| `steamTurn` | **No turn.** Steam Power is "phenomenal"; delaying it by 4-6 turns for Medicine "doesn't matter much". Says his Emperor speedrun got "bad luck" free techs and missed it. | Emperor | any | jhxl7C 40:15-41:05 | Low (rule only) |
| `workersPerCity100` | **At least 1 worker per city at the end of expansion**, preferably **~30 workers for 22-25 cities (1.2-1.4/city)**. 13 cities with 12 workers is "need another 5 or so" (target ~17, 1.3/city). A separate mistakes video says **at least 1.5 workers per city** after expansion (6 workers for a 4-city example) but lets you "skimp" during expansion. Also builds workers in the corrupt far cities. | Regent | Pangaea | YZDmln 63:44-64:13; -1A9i 12:19-13:08 | High |
| Delay of first workers | First worker only after the capital's first settler is out; second worker after 3-4 cities. | any | any | notes #92 (MnfWob 13:24) | Medium |
| `techGold` | Sells cheap techs early for a lump (e.g. Pottery for 10 gold). Later: **34 gold/turn for 20 turns plus 50 flat (about 730-750 gold) for one tech swap** ("insane deal"). AI counts gold/turn only up to your surplus, so he cuts science to show a surplus first: with that trick the AI accepted **222 gold/turn** where it refused 489. | Sid / any | any | 3nlb 10:18-11:08; 101T 85:12-87:08; B_N 53:11 | High for method, Low for a turn-100 total (none stated) |
| tech lead and gold | On Sid, saves gold for the last turns of a tech: "writing in 21 turns will just be a free tech basically because we've been saving all our gold". | Sid | any | B_N 13:09-13:47 | Medium |
| Warriors per city in expansion | **2 warriors in each city** under Republic switch ("not a huge threat"); garrison "bodies, not quality" in expansion; disband most warriors at the Republic switch. | Regent | Pangaea | YZDmln 67:46-67:56; notes #74 | Medium |
| First attack size | **No single figure for a first attack.** Rules of thumb: "a stack of about 10 ancient attackers around turn 80 can take whole civs even on high difficulty" (the captions say so; this is the one stated *turn*); early AI cities hold "about one spearman each". Counter-example on the AI side: on China/Monarch the AI archer-rushed him with "10 archers on two cities" about turn 50. | Demigod / Sid | any | DNlbpy 13:44; yggDJg 82:34-82:47 | Medium (one stated turn, from a tip, not a game) |
| Wars: pacing | Takes **4-5 cities per war**, razes big or flip-prone ones, makes peace, waits out the **20-turn** window, repeats. Peace deals often hand him a tech. | Sid / Demigod | any | tip log 37:06 (video id in `suede-tip-log.md`); CQ3I 0:42-0:52 | Medium |
| `killRatio` | **No figure.** He judges war by units lost to the AI's piecemeal attacks ("bleeding out") and catapult stacks, not by a ratio. | any | any | B_N 43:59-46:27 | Low |
| Size-7 cities | Priority for every city under Republic: buy the aqueduct, join spare workers. Saves "like 20 gold per turn" per couple of size-7 cities. | any | any | LeBaV5 1:15-1:58 | High |
| Granaries | **One granary in the expansion phase per 8-9 cities you will end with** (3 for 24 cities). | Regent / archipelago | Archipelago | Mnf 21:53-22:05, 38:06 | High |
| Expansion tempo claim | "Always build a settler the turn a city reaches size 3 is what the AI does ... win by having **twice as many cities**." | any | any | DNlbpy 13:44 | Medium |
| Overall economy standard | "If you have the same land as the AI you should be able to keep up with them up to Demigod ... on Emperor you shouldn't be falling behind." This is his own bar for the midgame. | Demigod/Emperor | any | LeBaV5 0:02-0:22 | High (quote) |

## What the missing figures mean

We have **no Suede turn for cities, Monarchy, Currency, Philosophy, Republic or Steam Power**. The only way to get them is to read the turn counter off the video frames (watching, not captions) or from a save. I did not do that. If the CTO wants turn figures, the next step is a ticket for sampling frames at set times of YZDmln and Mnf (turn counter is top right) and recording cities, government and techs at 10-minute marks. Until then the table above gives *conditions* (13 cities, 1+ workers per city, Republic via free tech) rather than dates.

## The Regent-exam gaps and the five ideas that most likely close them

Gaps (Advanced minus Standard, 16 games): about 135 of 200 turns in Despotism; 10th city around turn 108; army a quarter smaller; wars 0.2 vs 0.6; captured -0.8 ± 0.3.

Ranked by expected size of gain. "New" means not already a numbered tip in `doc/ai-strategy-notes.md` or a switch in `doc/ai-roadmap.md`.

### 1. Research to Republic through Philosophy's free tech (Code of Law first) - **new**
- Suede: Alphabet, Writing, Code of Law, then Philosophy; free tech = Republic. On Regent, nobody beats him to Philosophy. YZDmln 3:30-4:07.
- Today: `DevelopmentAI.ChooseResearch` (`C7Engine/AI/DevelopmentAI.cs:138-140`) is the stock weighted pick. `philosophy-first` (line 28, 326) adds value to Philosophy only, with no plan for which tech the free pick takes (the bonus tech is chosen by the same weights). `GovernmentAI` leaves Despotism whenever a better government is *known* (`GovernmentAI.cs:42-56`), so Advanced only leaves when research happens to reach one.
- Closes: the Despotism gap, the largest (135 of 200 turns).
- Trial: paired, Regent, 200-300 turns. Read `govTurn` (target: well under 100), `philTurn`, the government the civ ends in, `cities100`, score. This is related to ZUR-111 `early-government`; the new part is the *route* (free-tech Republic) rather than only the switch. Confirm with the CTO that it does not duplicate ZUR-111 before ticketing.

### 2. Contact early and swap techs at once: scouts first, trade bait first - **new**
- Suede: builds scouts early because a Pangaea lets him "meet all the civs", then trades expensive low-priority techs (Alphabet, Writing) for the ones he lacks. YZDmln 3:23-3:45.
- Today: tech-for-gold-per-turn trading is `TradeGpt` (`C7Engine/AI/TradeGpt.cs:9-69`), which sells for gold. The AI never builds Explorers (an owner's choice, `ChooseProducible.cs:205-206`) and has no opening rule to swap tech for tech; first contact comes from its warriors and boats. The Explorer ban is an owner ruling, so the first step is to ask the owner whether a scout for contact is allowed. Advanced has the same slow early research as Standard, which is why it sits in Despotism.
- Closes: Despotism gap (techs sooner), partly the gold gap.
- Trial: paired, Regent. Read the turn of first contact (new measure, or `techs100`), `techsBought`, `techGold`, `govTurn`.

### 3. Workers to 1.0+ per city by the end of expansion (today it caps at 0.85) - **new angle on notes #19**
- Suede: 1 worker per city minimum, aim for ~1.2-1.4; 13 cities with 12 workers is behind. YZDmln 63:44-64:13; Mistakes 12:19-13:08 says 1.5 after expansion.
- Today: `ChooseProducible.EnoughWorkers` returns `ceil(cities * 0.85)` (`ChooseProducible.cs:410-414`) and a worker scores `int.MinValue` once `NumWorkers >= EnoughWorkers` (`ChooseProducible.cs:387-389`). The notes (#19) say the AI already does 1.5 workers per city; the code does not. The 0.85 is from the stock AI's behaviour, not from Suede.
- Closes: slower growth (cities reach size 3 later, settlers later), which feeds the late 10th city.
- Trial: paired, Regent. A switch that raises the constant to 1.2 once no open city spots remain (expansion over, per `InExpansionPhase`, `ChooseProducible.cs:41`). Read `workersPerCity100`, `cities100`, `size7`, score. Risk: more upkeep under Despotism; the trial will show it.

### 4. War in short rounds: 4-5 cities, peace, 20-turn wait, repeat - **new**
- Suede: take only 4-5 cities, raze the big or flip-prone ones, make peace, wait out the window, go again; use peace deals to pick up a tech. Tip log 37:06; CQ3I 0:42.
- Today: `WarCampaign` runs a campaign and ends it with a log line (`WarCampaign.cs:277`) but has no city quota; war starts only when Civ3's own plotting rolls (`WarCampaign.cs:620`, `StockWarPlotting.RollsToPlot`). `EarlyRush` and `ArcherRush` fire rarely (ZUR-65/67 trials: no measurable gain).
- Closes: wars 0.2 vs 0.6 and captured -0.8. This is the only one of the five aimed at the army gap, and it comes with a risk: the exam also shows Advanced is meant to keep a lean army (`lean-military` kept in the roadmap).
- Trial: paired, Regent. Read wars, captured, `killRatio`, `unitsLost`, score. Treat this as the riskiest and last of the five.

### 5. Granary in the best food city, and settler/worker pumping (one granary per 8-9 cities) - **partly in notes (#125, line 297), new as a build rule**
- Suede: 1 granary per 8-9 cities planned, in a high-food city that pumps settlers and workers; workers from poor cities join low-food ones. Mnf 21:53-22:05, 38:06; tip log 43:45.
- Today: `ChooseProducible` has no granary-for-expansion rule; settlers are scored by site (`ChooseProducible.cs:341-353`) and held back by `CityIsTooSmall` (`ChooseProducible.cs:925-937`), which is right but only gives the settler when the city has the size.
- Closes: the 10th-city-at-turn-108 gap by shortening the time between settlers in the best city.
- Trial: paired, Regent. Read `cities50`, `cities100`, first-settler turn, `size7`.

## What I cannot supply and what to do next

1. Exact Suede turns for cities, Monarchy/Currency/Philosophy/Republic/Steam Power. Needs frame sampling or saves (see above). I recommend a small follow-up ticket, not a rewrite of this doc.
2. Rules he states about the game (Despotism -1 on 3+, settler-pop rules, culture flips) are claims; the game wins where they differ. I used them only as context.
3. Order if tickets are written: 1 and 3 first (cheap, large, measurable by `govTurn` and `workersPerCity100`), then 2, then 5, then 4.
