# The owner's own Civ3 play, from his saves

What the owner's real Civ3 Conquests games look like, set beside Civ3's AI in the same games and beside OpenCiv3's Advanced AI in our trials (doc/ai-roadmap.md, Track 2, "Baseline and loss analysis", 2026-11-01).

**These are snapshots of one player's games, not a benchmark.** Most of them are on bigger maps and at higher difficulty than our trials, the saves are wherever he happened to stop, and several are probably the same game at different turns. They show what a good human does differently, not by how much.

## What the saves are

- 40 saves (38 in Saves, 2 in Saves\MP). QueryCiv3 read all of them.
- 7 are turn-0 starts. 3 more have no cities yet (turns 8 and 35).
- 30 are mid or late games, turns 28 to 376:
  - 21 single-player games against the stock Civ3 AI at Emperor, Demigod or Deity (one at Regent): 8 on 100x100 maps with 8 civs (our trial size), 3 on 130x130 with 12 civs, the rest on 160x160 with 16 civs.
  - 1 hotseat/LAN game with 3 humans and 4 AIs ("GG MP SO MP3", Emperor, 100x100).
  - 4 MDJ games with 3 humans each: MGShboMedieval twice (turns 69 and 87), SGMRomeCeltOttos (turn 28), SGMArabsVikingsOttos (turn 62). They carry a 210 KB rule set and everyone starts in the Middle Ages in Monarchy, which matches the MPTournamentMDJ rules (not Medieval Japan).
- The tool reads turn, difficulty, map, civs and, for each civ, cities, citizens, units by kind, government, techs, gold, wars and owned land. The game year didn't come out (the DATE text is empty), so turns only.

## The numbers

How to read the table:
- "Captured?" counts the human's cities whose names are on another civ's city list. That's a guess: it counts flipped cities too, and misses renamed ones.
- "Land/city" is owned land tiles per city.
- "AI" is the average Civ3 AI civ in the same save.
- "Advanced" is our Regent trials on 100x100 Continents maps with 8 civs: about 4 cities at turn 50, 11.5-12 at turn 100, 16.7 at turn 150 and 17.4 at turn 200, with about 45-48 military units at turns 100-150.

| Save | Turn | Level, map | Human cities (captured?) | Citizens | Workers | Military | Govt | Techs (AI) | Land/city (AI) | AI cities, mil |
|---|---|---|---|---|---|---|---|---|---|---|
| fr new | 50 | Deity, 160 | 5 (0) | 8 | 1 | 7 | Despotism | 2 (7.6) | 8.4 (10.9) | 7.7, 23 |
| inca 2 | 52 | Demigod, 160 | 6 (0) | 8 | 6 | 9 | Despotism | 12 (9.9) | 11.7 (10.8) | 7.0, 20 |
| chinaHugeDom | 75 | Emperor, 160 | 10 (0) | 16 | 7 | 19 | Despotism | 14 (10.0) | 12.1 (16.3) | 4.1, 23 |
| maoPeninsula | 82 | Deity, **100/8** | 13 (0) | 31 | 7 | 15 | Despotism | 13 (16.4) | 9.1 (13.8) | 9.7, 35 |
| maya1 | 89 | Demigod, 130 | 11 (4) | 36 | 19 | 19 | Despotism | 9 (16.1) | 9.2 (13.7) | 11.6, 36 |
| maoDeityBehindOnTech | 90 | Deity, 160 | 15 (0) | 42 | 11 | 44 | Despotism | 8 (14.7) | 9.0 (12.6) | 12.8, 60 |
| china OWN | 126 | Emperor, 160 | 37 (14) | 135 | 45 | 65 | Despotism | 23 (18.9) | 10.6 (14.5) | 12.2, 33 |
| cartwonder1 | 139 | Demigod, 160 | 13 (0) | 36 | 9 | 10 | Despotism | 20 (15.3) | 9.2 (12.3) | 8.7, 66 |
| china continents | 152 | Emperor, **100/8** | 25 (8) | 99 | 29 | 34 | Anarchy | 25 (24.1) | 7.5 (13.4) | 9.6, 34 |
| japSnow | 162 | Emperor, 160 | 27 (0) | 63 | 32 | 28 | Despotism | 9 (21.9) | 9.4 (16.8) | 15.2, 68 |
| china cont | 169 | Emperor, **100/8** | 28 (10) | 152 | 25 | 17 | Republic | 28 (27.8) | 8.8 (13.3) | 11.2, 46 |
| iroq v french | 184 | Demigod, **100/8** | 21 (10) | 182 | 27 | 45 | Republic | 37 (37.3) | 10.2 (13.0) | 13.5, 60 |
| FR good island | 193 | Demigod, 160 | 31 (11) | 177 | 15 | 109 | Monarchy | 29 (24.9) | 8.2 (13.9) | 15.2, 118 |
| japan v mongols | 210 | Emperor, **100/8** | 38 (23) | 269 | 28 | 37 | Republic | 40 (39.3) | 11.0 (14.8) | 16.0, 62 |
| iroq demi | 216 | Demigod, **100/8** | 32 (17) | 256 | 43 | 56 | Republic | 43 (45.4) | 12.0 (12.1) | 13.4, 63 |
| fr deity | 216 | Deity, 160 | 20 (0) | 149 | 2 | 22 | Republic | 37 (29.1) | 8.0 (13.5) | 16.3, 151 |
| cartwonder2 | 225 | Demigod, 160 | 34 (0) | 211 | 27 | 37 | Republic | 40 (22.6) | 9.1 (12.6) | 9.6, 102 |
| india toa | 245 | Emperor, 130 | 82 (49) | 677 | 104 | 142 | Republic | 55 (47.5) | 15.6 (17.0) | 16.2, 70 |
| netherlands | 245 | Demigod, 160 | 44 (12) | 434 | 32 | 76 | Republic | 41 (24.3) | 9.4 (12.6) | 10.0, 124 |
| iroq behind | 284 | Demigod, **100/8** | 34 (22) | 315 | 42 | 202 | Republic | 62 (55.7) | 11.0 (12.3) | 11.5, 53 |
| iroqDeityPeninsula | 289 | Deity, **100/8** | 40 (14) | 440 | 41 | 174 | Republic | 54 (57.0) | 10.8 (12.0) | 23.7, 159 |
| cartwonder3 | 314 | Demigod, 160 | 44 (3) | 426 | 5 | 163 | Republic | 59 (34.4) | 9.0 (12.6) | 10.3, 128 |
| eng demigod | 335 | Demigod, 130 | 69 (32) | 645 | 25 | 193 | Republic | 64 (55.0) | 7.8 (11.8) | 9.3, 71 |
| greece square island | 376 | Emperor, **100/8** | 13 (0) | 162 | 9 | 40 | Republic | 53 (29.4) | 7.5 (11.1) | 4.4, 104 |

Multi-human games (each human civ; AI average in brackets):

| Save | Turn | Level | Human civs: cities, workers, military, government | AI cities, mil |
|---|---|---|---|---|
| SGMRomeCeltOttos (MDJ) | 28 | Emperor | Rome 7, 8, 5; Celts 4, 6, 6; Ottomans 4, 6, 4; all Monarchy | 6.2, 12 |
| SGMArabsVikingsOttos (MDJ) | 62 | Regent | Arabia 21, 32, 14; Ottomans 7, 9, 5; Scandinavia 9, 6, 18; all Monarchy | 5.8, 10 |
| MGShboMedieval (MDJ) | 69 | Emperor | Hittites 19, 27, 26; Babylon 14, 15, 32; Ottomans 7, 9, 19; all Monarchy | 9.0, 19 |
| MGShboMedieval (MDJ) | 87 | Emperor | Hittites 30 (9 captured?), 48, 48; Babylon 15, 27, 32; Ottomans 11, 19, 17 | 7.2, 18 |
| GG MP SO MP3 | 102 | Emperor | Greece 13, 12, 26; Persia 5, 3, 12; Ottomans 14, 11, 28; all Despotism | 6.5, 22 |

We can't tell which civ in the multi-human games is the owner.

## What the human does differently

- **More cities, and he keeps adding them.** Early on he's only a little ahead: 5-6 cities at turn 50, about what Advanced has (~4) and fewer than the Deity/Demigod AIs. By turn 80-90 he has 11-15 (Advanced has ~9-11 at that point). After turn 100 he pulls away: 25-28 cities at turn 150-170 on the 100x100 maps, against Advanced's ~17 and the Civ3 AIs' ~11. He doesn't stop when the land runs out, which is exactly where Advanced stalls (turns 100-125).
- **Much of that growth is conquest.** In the later 100x100 games roughly a third to two thirds of his cities have foreign names: 10 of 28, 10 of 21, 17 of 32, 22 of 34, 23 of 38. On bigger maps it's the same: India 49 of 82, England 32 of 69. Advanced rarely conquers. The exceptions are wonder or builder games (the cartwonder series, fr deity, japSnow, greece square island), where he took almost nothing.
- **Denser cities.** He owns about 8-10 land tiles per city, against 12-14 for the Civ3 AI in the same games, so he packs his cities closer together. The AI civs on our 100x100 maps own about 13 tiles each.
- **A small army for his size, except when he goes to war.** In peaceful stretches he has well under one military unit per city: 17 for 28 cities, 10 for 13, 34 for 25. That's below the Civ3 AIs (34-68) and below Advanced's 45-48. When he decides to conquer, the army is huge and modern: 160-200 units late on, mostly Cavalry/Infantry/Artillery, with bombers and armies.
- **About one worker per city or more.** He usually has 0.9-1.6 workers per city: 45 for 37, 29 for 25, 43 for 32, 48 for 30 in MDJ. Captured workers count here. Civ3's AI aims at about 0.85, the same as Advanced unless `workers-1-2` is on. The exceptions are a few late builder saves (fr deity has 2 workers for 20 cities).
- **Stays in Despotism late too, then goes straight to Republic.** This is the surprise. He is in Despotism at turns 126, 139 and 162 in three games, in anarchy at turn 152, and in Republic at 169 and later. He skips Monarchy, except in one game (FR good island, Monarchy at turn 193). That isn't faster than Advanced (Despotism to ~105-135). The difference is where he lands: Republic, not Monarchy. In MDJ everyone starts in Monarchy.
- **He saves gold.** He often holds 3,000-4,700 gold in mid game while the AIs sit near zero (cartwonder1 4,445 at turn 139, china OWN 4,669 at turn 126, china continents 3,195 at turn 152). Presumably it's for rushing, upgrades or a revolution.
- **Techs level with the AI, not ahead.** On the 100x100 maps his tech count is within a few of the AI average. On the big Demigod maps he's well ahead late (59 vs 34, 41 vs 24). Two saves are far behind (japSnow 9 techs at turn 162 with only Warriors; maoDeityBehindOnTech). These look like deliberate challenge or experiment games, so don't read them as normal play.

## How it was done

- The tool is `tools/human-games` (a console app on QueryCiv3, not in the solution or any test run).
- Arguments: the path to `conquests.biq`, then a folder of `.SAV` files.
- Units are sorted into kinds by their unit type's name (Settler, Worker, ship) and attack/defence; Civ3's unit strategy flags didn't separate settlers from workers.
- Military counts every land or air unit with attack, defence or bombard, so it includes Warriors kept as police.
- The saves were copied to a scratch folder first; the Steam folder was only read.
