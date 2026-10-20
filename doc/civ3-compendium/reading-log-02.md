# Reading log: section 2 (City economy) deep pass

Each line gives a URL or local path that was read, and what it added to `02-city-economy.md`.

## Local first-hand sources

- `Conquests/Text/Civilopedia.txt` (GCON_Hurry_Production, GCON_Food, GCON_Shields, GCON_Commerce, GCON_Treasury, GCON_Pollution, GCON_Golden_Age, GCON_Corruption, GCON_Strengths, BLDG_Forbidden_Palace, BLDG_Granary, BLDG_Courthouse, BLDG_Police_Station): nothing new beyond what was cited. Confirmed the FP "ten cities" text.
- `Text/Civilopedia.txt` and `civ3PTW/Text/Civilopedia.txt` (BLDG_Wealth, BLDG_Forbidden_Palace, GCON_Golden_Age, GCON_Corruption): Wealth is 8:1 (4:1 after Economics) in vanilla/PTW; the vanilla FP needs 8 cities; the vanilla GA text has no barbarian exclusion.
- `Conquests/Text/script.txt`: `#MAINTSHORT` (a building is lost when its upkeep cannot be paid), `#NOSUPPORT` (a unit is disbanded), the three `#GLOBALWARMING*` messages, `#POPULATION_LIMIT`, `#STARVE`, `#DISBANDSHIELDS`, `#POLLUTION`. Vanilla and PTW `script.txt` have the same warming keys.
- `README.txt`, `civ3PTW/README.txt`, `Conquests/readme.txt`: the 1.16f sun-brightness line and "Corruption effects have been reduced." Everything else was already cited.
- Steam manual PDF text (scratchpad `manual/m.txt`, `r.txt`; https://cdn.akamai.steamstatic.com/steam/apps/3910/manuals/manual.pdf): no switching penalty (pp.61, 141); rush at four times the remaining shields (p.144); rushed items arrive next turn and leaders can rush wonders (p.145); no rushing with resisters (p.149); polluted tile yields zero (p.89); pollution symbols are a probability (p.88); selling buildings (p.143); raze or keep (p.73); the city tile is always worked (p.75); the treasury deficit (p.139).
- `civ3mod.bic`, `civ3PTW/civ3X.bix`, `Conquests/conquests.biq`, decompressed with QueryCiv3 `Util.ReadFile` (scratchpad `rule/`, `v.bic`, `ptw.bix`, `c.biq`), RULE/GOVT/WSIZ/DIFF read by offset: `ShieldsCostPerGold` V/PTW 8, C3C 4; `ShieldValueInGold` 4 and `CitizenValueInShields` 20 in all three; `ScienceRateCap` 10 for every government in all three; vanilla/PTW OCN 12/14/16/24/32 and difficulty % 100/95/90/85/80/70.
- `conquests.biq` BLDG trait flags (QueryCiv3): the Great Wonder trait table for Golden Ages, and the trait flags on improvements.
- EDHELP (`Pollution_Tile_Properties_.htm`, `Railroad_Tile_Properties_.htm`, `Pollution_Improvement_Properties_.htm`, `Capitalization_Improvement_Flag_.htm`, `Shield_Cost_Per_Gold.htm`, `Citizen_Value_in_Shields.htm`, `Join_City_Unit_Worker_Engineer_Actions_.htm`, `Build_City_Unit_Worker_Engineer_Actions_.htm`, `Level_Two_City_Maximum_Size.htm`, `Hurrying_Production_Government_Properties_.htm`, `Food_Consumption_Citizen.htm`): nothing new; "Default is 8 gold" identified as the vanilla default.
- Other compendium sections, consulted for cross-references (not edited): 03 §3.8 (WLTKD effects), 05 §5.5 (hurry), 07 §4.2-4.3 and §9 (pollution yield, railroad, minimum distance), 08 traits table.

## Web sources

- https://forums.civfanatics.com/threads/firaxis-corruption-breakdown.73485/ : Tavis (Firaxis): C3C changed only multi-FP handling and ranking; the FP bug measured rank from the real capital; distance was correct (#15).
- https://forums.civfanatics.com/threads/firaxis-corruption-breakdown.73485/page-2 : nothing new (anarres on the beta, no tests).
- https://forums.civfanatics.com/threads/firaxis-corruption-breakdown.73485/page-3 : nothing new.
- https://forums.civfanatics.com/threads/firaxis-corruption-breakdown.73485/page-4 : nothing new.
- https://forums.civfanatics.com/threads/firaxis-corruption-breakdown.73485/page-6 : nothing new (one before/after patch anecdote).
- https://forums.civfanatics.com/threads/firaxis-corruption-breakdown.73485/page-8 : Tavis: "return it to pre-C3C w/ the fixes", and "No Corruption" added to the editor; akots' beta-1.12 rank measurements (not used).
- https://forums.civfanatics.com/threads/firaxis-corruption-breakdown.73485/page-9 : nothing new.
- https://forums.civfanatics.com/threads/firaxis-corruption-breakdown.73485/page-11 : nothing new (code discussion, untested).
- https://forums.civfanatics.com/threads/firaxis-corruption-breakdown.73485/page-14 : the corrected model was in 1.15 (Chieftess).
- https://forums.civfanatics.com/threads/do-you-think-you-understand-corruption-pre-c3c.19922/ : the full vanilla/PTW formula (alexman) and the 95% cap.
- https://forums.civfanatics.com/threads/do-you-think-you-understand-corruption-pre-c3c.19922/page-2 : multipliers apply after corruption; the custom-map OCN bug; 1.21f changed only courthouse, police and WLTK; Soren via BillChin.
- https://forums.civfanatics.com/threads/do-you-think-you-understand-corruption-pre-c3c.19922/page-8 : the thread was retitled "specific to Vanilla/PTW"; nothing else new.
- https://forums.civfanatics.com/threads/everything-about-corruption-c3c-edition.76619/page-2 : FP affects rank via OCN (Roland Johansen); Democracy distance factor 3/4 (alexman).
- https://forums.civfanatics.com/threads/everything-about-corruption-c3c-edition.76619/page-3 : "Of course the readme is wrong!"; the Cmax clamp at 0.
- https://forums.civfanatics.com/threads/everything-about-corruption-c3c-edition.76619/page-4 : Cmax caps the total; the capital is at distance 0 in C3C; founding-date tie-break; PTW vs C3C differences (#74).
- https://forums.civfanatics.com/threads/everything-about-corruption-c3c-edition.76619/page-5 : foreigners have no effect; Commercial affects waste; the wrong-world-size bug; the AI at Regent level.
- https://forums.civfanatics.com/threads/everything-about-corruption-c3c-edition.76619/page-6 : nothing new (specialists are uncorrupted, already cited).
- https://forums.civfanatics.com/threads/everything-about-corruption-c3c-edition.76619/page-7 : justanick's sign correction (the section already had the correct form).
- https://forums.civfanatics.com/threads/interturn-mechanics.443195/ : Lanzelot's end-of-turn order (new §13).
- https://forums.civfanatics.com/threads/interturn-mechanics.443195/page-2 : city order by internal ID; tourism as commerce; disease timing; Spoonwood on wonder lag (§13).
- https://forums.civfanatics.com/threads/interturn-mechanics.443195/page-3 : the list of missing steps (§13). The first try returned HTTP 429; the retry worked.
- https://forums.civfanatics.com/threads/how-the-population-system-works.118422/ : displayed population is 1000 per food and cosmetic (§2).
- https://forums.civfanatics.com/threads/whipping-to-rush-vs-rushing-with-gold.257616/ : the unit gold rate is the same as for improvements (pol1, Worker 80 gold).
- https://forums.civfanatics.com/threads/whipping-to-rush-vs-rushing-with-gold.257616/page-2 : the whip rounding order (PaperBeetle).
- https://forums.civfanatics.com/threads/whipping-to-rush-vs-rushing-with-gold.257616/page-3 : nothing new.
- https://forums.civfanatics.com/threads/hurry-production.95782/ : unit examples at 4 g/shield (WackenOpenAir, petermarkab).
- https://forums.civfanatics.com/threads/wealth.24405 : the vanilla 8:1 rate and the Economics halving; 1-7 shields gives 1 gold.
- https://codehappy.net/apolyton/threads/73546-1.htm : nothing new for section 2 (already used in section 5).
- https://forums.civfanatics.com/threads/pollution-bug.251335/ : not Civ3 (Civ1 thread).
- https://forums.civfanatics.com/threads/global-warming-doesnt-work.539761/ : warming in Civ3 Complete removed only forests and jungles (observation).
- https://codehappy.net/apolyton/threads/60991-1.htm : WarpStorm, second-hand from Firaxis, says ground pollution doesn't count; the risk follows the world's city triangles and nukes.
- https://codehappy.net/apolyton/threads/60991-2.htm : nothing new (anecdotes).
- https://codehappy.net/apolyton/threads/60246-1.htm : nothing new (opinions; a mod's coast changes).
- https://apolyton.net/forum/miscellaneous/archives/civ3-play-the-world/82366-how-does-pollution-occur : nothing new beyond what was cited.
- https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/54339-pollution : roadcage's facilities/pop/tech model; the hedged "2%" recollection.
- https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/48566-bug-in-food-production : conflicting polluted-yield claims (§8).
- https://forums.civfanatics.com/threads/power-plants-everything-you-wanted-to-know-but-were-afraid-to-ask.52745/ : waste before multipliers; one plant per city; meltdown timing and the tested 10-15% chance.
- https://forums.civfanatics.com/threads/power-plants-everything-you-wanted-to-know-but-were-afraid-to-ask.52745/page-2 : the C3C 1.22 test (plants add 50% of base); banks work without a market.
- https://forums.civfanatics.com/threads/golden-age-triggers-by-great-wonders.411703/ : the wonder-trait table (matches the BIQ); captured wonders and the check timing (Puppeteer).
- https://forums.civfanatics.com/threads/great-wonder-list-to-trigger-golden-ages.99092 : the vanilla/PTW trait list differs; captured-wonder rule (bouncelot).
- https://forums.civfanatics.com/threads/production-phase-micromanagement-a-powerful-exploit.63168/ : commerce is totalled before shields and food; the tile-reuse exploit (§13).
- https://forums.civfanatics.com/threads/compiled-list-of-bugs-and-non-bugs-not-a-dicussion-thread.70063/ : city-tile shields; granary half-full; no rushed shields into wonders; GA check on any wonder.
- https://forums.civfanatics.com/threads/civ3-conquests-additions-changes-list.104294/ : tourism 2-14 gpt uncorrupted; Age of Science "does not work"; SGLs rush wonders.
- https://codehappy.net/apolyton/threads/78438-1.htm : DaveMcW's city-tile shield formula; Industrious moved to size 13 in PTW.
- https://codehappy.net/apolyton/threads/49358-1.htm : Nero Would's vanilla 1.21f city-tile formula (food 2, minimum 1 shield, the "4 gold" minimum).
- https://forums.civfanatics.com/threads/city-tile-yield.665936/ : C3C city tile: food fixed at 2, minimum 1 shield, size bonuses.
- https://codehappy.net/apolyton/threads/104560-1.htm : nothing for section 2 (hill-city irrigation chaining; belongs to section 7).
- https://www.civ-wiki.de/wiki/Stadtfeldertrag_(Civ3) : the German CivWiki city-tile table (food, shields, commerce, capital minimum 4).
- https://wiki.civforum.de/wiki/Stadtfeldertrag_(Civ3) : fetch failed (TLS certificate mismatch); read at civ-wiki.de instead.
- https://sullla.com/Civ3/strat7.html : nothing new for the city tile.
- https://forums.civfanatics.com/threads/join-city.121112/ : nothing new (no cap rule).
- https://forums.civfanatics.com/threads/population-limit.87448 : nothing new (probably not a Civ3 forum thread).
- https://forums.civfanatics.com/threads/free-palace-jump.47304/ : the full scoring; the radius is around the candidate city.
- https://forums.civfanatics.com/threads/free-palace-jump.47304/page-2 : walking distance; the revised weights; database tie-break.
- https://forums.civfanatics.com/threads/free-palace-jump.47304/page-4 : nothing new.
- https://forums.civfanatics.com/threads/looking-for-the-formula-on-how-the-palace-gets-relocated.703374/ : justanick confirms the formula (2026).
- https://forums.civfanatics.com/threads/list-of-bugs-ive-experienced.139564 : not Civ3 (Civ4 thread). The "50% switching penalty" claim does not exist for Civ3.
- https://forums.civfanatics.com/threads/insight-to-prebuilding-only-for-newbies.92070/ : nothing new (prebuild strategy only).
- https://forums.civfanatics.com/threads/civ-engineers.74944 : Civil Engineer shields are held back from units, restored on switch-back (Grille).
- https://forums.civfanatics.com/threads/libraries-make-money.73160/ : nothing new.
- https://forums.civfanatics.com/threads/multiplier-buildings-a-practical-primer.242296/ : the order of multiplier rounding; MAS on the trade bonus and tourism before corruption.
- https://forums.civfanatics.com/threads/things-civilopedia-cant-explain.470744 : nothing new.
- https://forums.civfanatics.com/threads/today-i-learned-about-civ3.687642/ : page 1 only; trait building classes (section 8 matter). Nothing new for section 2.
- https://civfanatics.com/civ3/faq/ (both halves) : draft size 6 (Disputed 12); the FAQ warming text.
- https://forums.civfanatics.com/threads/getting-rid-of-cities.14157/ : early-vanilla settler/worker delay; no rushing that would disband a city.
- https://forums.civfanatics.com/threads/raise-abandon.113940 : abandoned cities leave ruins; the size-2 Settler abandon prompt; slave workers from razing.
- https://forums.civfanatics.com/threads/occupy-vs-raze.313823 : slave workers from razing (C3C only); about 2 from size 5 (IIRC).
- https://forums.civfanatics.com/threads/capturing-cities.189234 : not Civ3 (Civ4 thread).
- https://forums.civfanatics.com/threads/a-rank-corruption-discovery-and-exploit-to-negate-rank-corruption.62851/ : the PTW rank-from-Palace exploit, confirmed on 1.27f.
- https://forums.civfanatics.com/threads/how-much-improvements-really-help-corruption.18500/ : nothing used (early-1.17f measurements, superseded by alexman's formula).
- https://codehappy.net/apolyton/threads/56739-1.htm : the AI uses the default-difficulty OCN %; vanilla Communism distance flat 30%; the cap is "rounded down" (Disputed 13).
- https://codehappy.net/apolyton/threads/56334-1.htm : nothing new (a second-hand summary only: Commercial affects shields, FP doubles OCN in Communism).
- https://codehappy.net/apolyton/threads/56822-1.htm : nothing new (it only links the log).
- https://codehappy.net/apolyton/threads/49610-1.htm : nothing new (excerpt without economy answers).
- http://apolyton.net/misc/chat/civ3/129log1.shtml : fetch failed (404).
- https://web.archive.org/web/2005/http://apolyton.net/misc/chat/civ3/129log1.shtml : fetch failed (the tool cannot reach web.archive.org).
- https://codehappy.net/apolyton/threads/31711-1.htm : nothing new (questions only).
- https://codehappy.net/apolyton/threads/38475-1.htm : nothing new (page 1; not about economy).
- https://codehappy.net/apolyton/threads/32876-1.htm : nothing new (the transcript is not on the page).
- https://forums.civfanatics.com/threads/c3c-frequently-asked-questions.68004/ : page 1 only; Feudalism/Fascism maintenance (section 5 matter). Nothing new for section 2.
- https://sullla.com/Civ3/conquestsed.html : nothing new (opinion).
- WebSearch (one query, "civ3 conquests city center tile automatically irrigated..."): led to CFC 665936 and codehappy 104560.

## Not yet read (section 2 entries and relevant general ones)

- Corruption: CFC 88390 (Tarzus), 91379 (RCP player aid), 13177 (WLTKD defeats corruption), 8516, 148420, 448286; FP threads 548537, 46657, 309768, 7604; CFC 107789 (palace cost forum); Apolyton 9476781, 121599; codehappy 35907, 41039, 45440, 56739 pages 2-3, 100156, 37677, 31654, 36740, 34669, 59349, 40742, 31545, 60645, 89629, 87167, 70202. Pages 5, 7, 10, 12 and 13 of CFC 73485, and pages 3-7 of CFC 19922.
- Hurry/production: CFC 99341, 76152, 112249, 141733, 83199; codehappy 37559, 42288, 49624, 36836.
- Food/growth/tiles: CFC 86630 (Case for Food forum), 68433, 250673, 221152, 362279, 281089, 92731, 54172, 100256, 17235, 116891, 57026, 267177, 278338, 16158, 136055, 299337, 152415, 30062, 7785; Apolyton 140963; codehappy 35010, 58357, 62765, 59185, 68134; Bamspeedy's Babylon tutorial.
- Commerce/maintenance: CFC 57429, 108789, 19713, 265862, 46626, 467878, 11573, 515542, 700654, 522375, 70438; page 2 of CFC 242296; codehappy 36832, 35196, 54812, 42734, 57142, 55133, 36339.
- Pollution/warming: CFC 119983; codehappy 60246 pages 2-3, 76575, 35557.
- Golden ages: CFC 263019, 623812, 296138, 29424; codehappy 31322, 43284, 62110, 80959.
- General: the patch readme threads (CFC 11091, 16272, 20502, 27296, 46040, 60369) and the codehappy patch threads (36944, 37330, 42183, 42330, 47713, 47992, 56058, 56271, 58701, 94259); CFC 52591; Apolyton 127875; the full Firaxis chat logs (apolyton.net/misc/chat pages, not reachable); Quick Answers threads; HoF rules and banned-exploit threads; the Antal1987 exe-research threads (526966, 526285), which could settle the city-tile, pollution and hurry formulas from code.
