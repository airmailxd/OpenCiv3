# Reading log, section 8 (AI behaviour, difficulty levels, civ traits, scoring and victory)

Deep pass of 2026-10-20. One line per URL or local source read: what it contributed to `08-ai-difficulty-and-victory.md`, or "nothing new", "fetch failed" or "not Civ3".

## Local first-hand sources

- `<civ3>/Conquests/conquests.biq` (QueryCiv3 plus throwaway readers `s8biq`, `s8race` in the session scratchpad): DIFF (content, cost factor, OCN %, AI-AI trade, corruption % 100), WSIZ (OCN 14/17/20/28/36, tech rate, distance between civs), GAME (domination 66/66, culture 20000/100000, all victory flags off in the stored defaults, Wonder Victory off), RULE (`DefaultDifficultyLevel=2`, `StartUnitType1/2` = Settler/Worker, `Scout` = PRTO 2, 10 spaceship parts), BLDG trait flags for every building, RACE build-often/never and governor flags for all 31 civs.
- `<civ3>/civ3PTW/civ3X.bix`, `<civ3>/civ3mod.bic`: QueryCiv3 refuses pre-Conquests files; not read this pass.
- `<civ3>/Conquests/Text/Civilopedia.txt`: `#GCON_Strengths` (all eight traits), `#GCON_Victory` / `#DESC_GCON_Victory` (space race at launch, domination "70%", culture by map size, UN candidates, histograph), `#GCON_Spaceship`, `#GCON_Golden_Age`, `#BLDG_United_Nations`.
- `<civ3>/civ3PTW/Text/Civilopedia.txt`: domination "70%", culture "at least twice as much as any rival".
- `<civ3>/Text/Civilopedia.txt` (vanilla): domination "66%", culture "at least twice as much as any rival".
- `<civ3>/Conquests/Text/script.txt`: `#LOSTSPACESHIP`, `#DESTROYEDSPACESHIP`, `#TIMELIMIT` ("WINNER BY SCORE"), `#DIPLOVICTORYVOTEOPTION`, `#NODIPLOVICTORY`.
- `<civ3>/Conquests/Text/labels.txt`: searched for spaceship/victory labels; nothing new.
- `<civ3>/README.txt` (vanilla patches): 1.16f (spaceship parts one city at a time, AI cultural priority reduced, submarines, retiring), 1.17f (spaceship destroyed when capital razed, score averaging bug for future techs, AI city-limit loop, AI worker loop, worthless-tech value bug, spy needed for part pop-ups), 1.17f also "The AI no longer trades during the players turn"; 1.21f (corruption slider, AI-AI trade rate exposed); 1.29f (Respawn AI Players renamed).
- `<civ3>/civ3PTW/readme.txt`: NoAIPatrol option, Expansionist Scouts with abilities off, request to remove units from territory, MP difficulty always Regent bug.
- `<civ3>/Conquests/readme.txt`: Seafaring coastal starts, Wonder Victory works as a time limit.
- `<civ3>/Civ Complete Readme.txt`: Wonder Victory text again; nothing else new.
- MANUAL (scratch text extract of the Complete manual): p.30-33 difficulty descriptions, victory options, "Adjustable AI Aggressiveness"; p.123 future techs add to score; p.173-176 spaceship (race to launch, capital capture destroys ship), domination, histograph.
- Editor help (decompiled CHM): `Cost_Factor_AI_Bonuses_.htm` (growth, shields, research), `AI_to_AI_trade_rate_AI_Bonuses_.htm` (definition), `Corruption_Modifier_Difficulty_Level_.htm`, `Default_Difficulty_Level.htm`, `Max_Government_Transition_Time_AI_Bonuses_.htm`, `Number_of_Defensive/Offensive_Land_Units...htm` (best available unit), `Number_of_Start_Unit_Type_2...htm` (wrongly says settler), `Favorite_Government...htm`, `Shunned_Government...htm`, `Aggression_Level...htm`, `Build_Often/Never...htm`, `Settings_Civilization_Governor_.htm`, `AI_Strategy_Rules.htm`, `Flavor_Page.htm`, `Relationship_Percentage_(Flavors).htm`, `Other_Characteristics_Improvement_Properties_.htm` (trait flags = golden-age triggers on wonders), `Bonuses_(Civilization_Properties).htm`, `Free_Techs...htm`, `Additional_Free_Support...htm`, `Bonus_for_Each_City...htm`, `Distance_Between_Civs...htm`, `Attack_Bonus_Against_Barbarians.htm`, `Victory_Conditions_Scenario_Properties_.htm`, `Culture_Ratio.htm` (nothing new for section 8), `Difficulty_Level.htm` (nothing new).

## Code-level (executable research)

- https://github.com/maxpetul/C3X/blob/master/default.c3x_config.ini (scratch copy from the section 7 pass): AI-AI demands absent in stock, artillery left in cities, leader-AI rush bug, army-filling bug, 1-3 naval escorts, worker requirement option, human bias in war planning, submarine bug, disembark-all, settler perfume test, bankruptcy order, Agricultural fresh-water exception, minimum city separation 1, city limit 512, production and tech point values.
- https://github.com/maxpetul/C3X/blob/master/Notes/ocn_formula.txt : the full stock OCN formula, including the AI-only difficulty term and the per-player difficulty scaling.
- https://github.com/maxpetul/C3X/blob/master/Civ3Conquests.h : `Leader.power_rank` (31 = strongest), `Leader.player_difficulty`, race bonus enums.
- https://github.com/maxpetul/C3X/blob/master/injected_code.c : stock `ai_adjust_sliders` call and gold rate cap, the 1-in-4 AI-AI negotiation gate, per-player OCN scaling, war-target hook.
- https://github.com/maxpetul/C3X/blob/master/civ_prog_objects.csv : names of stock AI functions (`City_ai_choose_production`, `Match_ai_eval_city_location`, `Leader_ai_eval_war_target`, `Leader_ai_eval_technology`, `Leader_ai_eval_government`, `Leader_get_food_cost_factor`, `Leader_ai_roll_to_declare_after_provocation`).
- https://github.com/maxpetul/C3X/tree/master/Notes (`interesting_object_notes.txt`, `to_investigate.txt`, `galley_attacking_cities_bug.txt`, `balance_ideas.txt`, `suggestions.txt`): nothing new for section 8 (UI internals, the submarine bug again, mod ideas).
- https://forums.civfanatics.com/threads/all-about-ai-aggression-levels-and-war-plotting.703785/ (four fetches): Flintlock's code reading of effective aggression, the war-plotting rolls, target exclusion and scoring, attitude base; justanick's worked odds; Lanzelot's test that a plot cannot be reverted. Main new content of 3.4.
- https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/page-95#post-16587093 : Flintlock on the stock worker valuation; Erlend Philip Wood's 80-90%-of-cities worker count and Build Often having no effect.
- https://forums.civfanatics.com/threads/useful-patches-for-civ-3-conquests-v1-22.526285 (12 pages; one fetch summary): stock 2-tile settle block (v1 notes), worker-strategy bug; Steph's "AI Agression levels 1&2 and 4&5 seem identical" (a wish-list claim, not used).
- https://forums.civfanatics.com/threads/patch-framework-for-c3c-v1-22.526966/ : nothing new (only the science-age 1.25 comment).

## Forum and web sources

- https://codehappy.net/apolyton/threads/70419-1.htm : Aeson's score summary (vanilla), future techs small.
- https://codehappy.net/apolyton/threads/33409-1.htm : nothing new (score components from the histograph only).
- https://codehappy.net/apolyton/threads/41463-1.htm : nothing new (one untested post on domination).
- https://codehappy.net/apolyton/threads/44417-1.htm : nothing new (no Soren posts; opinions on the Forbidden Palace).
- https://codehappy.net/apolyton/threads/39744-1.htm : Soren on unit AI strategies and city governors.
- https://codehappy.net/apolyton/threads/54606-1.htm : nothing new (Jon Shafer: no AI scripting in Civ3).
- https://codehappy.net/apolyton/threads/50345-1.htm (page 1 of 3): Coracle on settlers everywhere; rest opinion.
- https://codehappy.net/apolyton/threads/48213-1.htm : one line that 1.21f toned down AI expansion.
- https://codehappy.net/apolyton/threads/66928-1.htm (page 1 of 2): alexman's PTW debug test, no luxury slider, entertainers and irrigation.
- https://codehappy.net/apolyton/threads/59444-1.htm : 1.29f naval and war observations; Soren's offensive/defensive fix via Jon Shafer.
- https://codehappy.net/apolyton/threads/96269-1.htm : PTW 1.27f island AI naval landings.
- https://codehappy.net/apolyton/threads/70852-1.htm : nothing new (mixed reports of AI-AI wars; superseded by the code reading).
- https://codehappy.net/apolyton/threads/32685-1.htm : nothing new (Nov 2001 untested list with pre-release level names).
- https://codehappy.net/apolyton/threads/102278-1.htm (page 1 of 2): alexman, flavors unused in the stock epic game.
- https://codehappy.net/apolyton/threads/39845-1.htm (page 1 of 6): mid-turn AI trading in 1.16f; AI settlers going for resources.
- https://codehappy.net/apolyton/threads/84970-1.htm : Harovan's confirmed cheat list; dexters' debug check of resource knowledge.
- https://codehappy.net/apolyton/threads/34826-1.htm (page 1 of 2): Soren chat quote via zapperio.
- https://codehappy.net/apolyton/threads/56739-1.htm : alexman, AI uses the default difficulty's OCN %, Regent.
- https://codehappy.net/apolyton/threads/51961-1.htm : nothing new (part resources and one-city rule, already covered).
- https://codehappy.net/apolyton/threads/30684-1.htm : nothing new (Wittlich: win at launch; superseded by the manual).
- https://codehappy.net/apolyton/threads/37264-1.htm : Freiherr on the 1.16f one-city-per-part wording.
- https://codehappy.net/apolyton/threads/51960-1.htm : UN interval recollections (4/10/20/40), all hedged.
- https://codehappy.net/apolyton/threads/93177-1.htm (page 1 of 2): Jaybe "11 turns, actually".
- https://codehappy.net/apolyton/threads/76255-1.htm : alexman relaying Firaxis on Commercial OCN 25% (10% before 1.29f); Nor Me's city-tile figures.
- https://codehappy.net/apolyton/threads/106962-1.htm : Dominae's AU501 Seafaring facts.
- https://codehappy.net/apolyton/threads/107137-1.htm : nothing new (AU mod rules, not stock).
- https://codehappy.net/apolyton/threads/91706-1.htm : nothing new (pre-release speculation on the C3C traits).
- https://codehappy.net/apolyton/threads/84396-1.htm : alexman (Firaxis) on PTW worker selling and Despotism irrigation; Jaybe on multiple stacks.
- https://codehappy.net/apolyton/threads/37691-1.htm : nothing new (1.16f anecdotes).
- https://codehappy.net/apolyton/threads/89862-1.htm : Mike Breitkreutz (Firaxis) on chokepoints and no AI change in 1.21f.
- https://codehappy.net/apolyton/threads/88537-1.htm : dexters on workers disbanded when broke, landing loop.
- https://codehappy.net/apolyton/threads/49489-1.htm : nothing new (factories built, just less often).
- https://codehappy.net/apolyton/threads/56961-1.htm : Soren on build categories, overlap, shield-building priority; alexman on the vanilla build-often lists.
- https://codehappy.net/apolyton/threads/42759-1.htm : vanilla 1.17 observations (fills every spot, research 30-70%, gold per turn).
- https://codehappy.net/apolyton/threads/49615-1.htm : nothing new (unit-cost test only).
- https://codehappy.net/apolyton/threads/52367-1.htm : nothing new (untested multi-strategy claims).
- https://codehappy.net/apolyton/threads/56854-1.htm : Soren on AI renegotiating lopsided deals.
- https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/50248-why-does-the-ai-commit-suicide : nothing new (Dec 2001 speculation; superseded by the code reading).
- https://codehappy.net/apolyton/threads/43643-1.htm : chiefpaco's territory score test; cultural borders count.
- https://codehappy.net/apolyton/threads/35851-1.htm : DickK, last part triggers the end game.
- https://codehappy.net/apolyton/threads/37799-1.htm : nothing new (2x rule for the civ-wide win only, from memory).
- https://codehappy.net/apolyton/threads/33666-1.htm : nothing new (no evidence of Civ3 civil war on capital capture).
- https://forums.civfanatics.com/threads/study-of-inner-workings-of-military-advisor.83599/ : ProPain's advisor strength formula (C3C 1.15, tested). Not used in the section: the advisor formula is not shown to be the F8 power used in war plotting.
- https://forums.civfanatics.com/threads/the-other-domination-limit.122472/ (page 1 of 2): nothing new (domination milking theory, assumptions on water tiles).
- https://forums.civfanatics.com/threads/scoring-of-future-tech.31261 : Bamspeedy's test, one future tech = one content citizen per turn.
- https://forums.civfanatics.com/threads/100-k-culture-requirements.356991/ : culture 2x rule (memory), map-size values, open question on eliminated civs.
- https://forums.civfanatics.com/threads/guide-to-cultural-victory.124277/ : Twonky (C3C, 2005) on the 2x rule.
- https://forums.civfanatics.com/threads/why-does-the-ai-declare-war.162170 : nothing new (2006 anecdotes; superseded by the code reading).
- https://forums.civfanatics.com/threads/catch-the-runaway-ai.111918/ : nothing new (one game narrative).
- https://forums.civfanatics.com/threads/agricultural-trait.351427/ : nothing new (opinions only).
- https://forums.civfanatics.com/threads/starting-units.309166/ : not Civ3 (Civ4 XML).
- https://forums.civfanatics.com/threads/changing-starting-number-of-settler-units.243710/ : not Civ3 (Civ4).
- https://forums.civfanatics.com/threads/what-makes-deity-difficult.673903 : not Civ3 (Civ4).
- https://forums.civfanatics.com/threads/ai-declaring-war.686908 : not Civ3 (Civ5).
- https://forums.civfanatics.com/threads/ai-city-distance.408506 : not Civ3 (Civ5; found by search).
- https://civfanatics.com/civ3/strategy/misc/strategy-for-emperor-and-deity-levels/ : nothing new.
- https://civfanatics.com/civ3/strategy/introductory/monarch-to-emperor-the-great-leap/ : nothing new (restates the DIFF table).
- https://forums.civfanatics.com/threads/multiplayer-ai-aggression-or-lack-thereof.700748/ : nothing new for single player (MP-only passivity reports; aggression not settable in MP, already quoted from 703785).
- https://forums.civfanatics.com/threads/suicidal-ai-scouts.660070/ : AI disbands scouts when the map is revealed to it.
- https://forums.civfanatics.com/threads/notes-on-scoring.263313/ : not Civ3 (Civ4 scoring).
- https://forums.civfanatics.com/threads/maximizing-your-score.18729/ : nothing new (SirPleb: the finish bonus "is the same regardless of how you win", already cited from 14617).
- https://forums.civfanatics.com/threads/getting-the-ai-to-use-worker-units-without-all-worker-jobs-enabled.515992/ : the all-worker-jobs rule is the editor's; the game reads the Terraform strategy flag.
- https://codehappy.net/apolyton/threads/36170-1.htm : respawn of early-killed civs (anecdotes); "destroying all cities and all settlers".
- https://codehappy.net/apolyton/threads/61752-1.htm : nothing new (unanswered question).
- https://codehappy.net/apolyton/threads/49432-1.htm : nothing new (1.21 anecdotes of AI artillery bombarding; superseded by C3X-INI).
- https://codehappy.net/apolyton/threads/107869-1.htm : nothing new (alliance anecdotes, guesses).
- https://forums.civfanatics.com/threads/ai-difficulty-level-bonuses.37490/ : fetch failed (HTTP 429, three tries); already cited from earlier passes.
- https://forums.civfanatics.com/threads/tmits-guide-to-un-victories.304815 : fetch failed (HTTP 429).
- Web searches (2): "civ3 AI city placement distance" (only Civ4/Civ5 results plus 408506) and "C3X city location desirability" (no code description found).

## Second reading round (2026-10-20)

Code-level:

- https://github.com/maxpetul/C3X/blob/master/injected_code.c (`try_ai_demand_from_other_ai`): the stock demand gates; the refusal war roll is blocked by an MPP. Cross-referenced in 3.4.1 (full text in section 6.9).
- https://github.com/maxpetul/C3X/blob/master/README.md , changelog.txt , Notes/"Antal's Hex Patches.txt" : nothing new for section 8.

CivFanatics:

- https://forums.civfanatics.com/threads/ai-difficulty-level-bonuses.37490/ (page 1, now read in full): verbatim TheNiceOne on AI content citizens and the barbarian bonus. Added to 1.2.
- https://forums.civfanatics.com/threads/ai-difficulty-level-bonuses.37490/page-2 : Spoonwood vs justanick (2025) on when AI extra start units appear; MAS "the AI "thinks" the same at all difficulty levels". Added as Disputed 15.
- https://forums.civfanatics.com/threads/useful-patches-for-civ-3-conquests-v1-22.526285/ (page 1) and /page-2 : nothing new for section 8 (Civinator: the stock AI misassigns citizens after a government change, an observation).
- https://forums.civfanatics.com/threads/how-the-demographics-works.37033/ : Bamspeedy's demographics rows (PTW debug tests). Added as 5.3.
- https://forums.civfanatics.com/threads/lulthymes-0-point-histograph.114890 : Dianthus, land/coast tiles, no points for destroying a civ, average rounded down. Added to 5.1 and the domination open question.
- https://forums.civfanatics.com/threads/histograpgh-attempt-close-but-no-cigar.657407 : Spoonwood, sea tiles don't count toward the domination limit. Added to the open question.
- https://forums.civfanatics.com/threads/no-settlers-no-workers.704355/ : vmxa, Sid AIs disband workers at the support cap. Added to 3.6.
- https://forums.civfanatics.com/threads/the-lazy-rulers-guide-to-a-histographic-victory-in-10-steps.299691/ : nothing new (conquest strategy).
- https://forums.civfanatics.com/threads/very-short-summary-for-deity-level.162116/ : nothing new.
- https://forums.civfanatics.com/threads/civilization-3-trait-rate.61001/ : nothing new (trait opinions, PTW).
- https://forums.civfanatics.com/threads/tmits-guide-to-un-victories.304815 : not Civ3 (Civ4; per reading-log-06), not refetched.

Apolyton (codehappy.net mirror and apolyton.net):

- https://codehappy.net/apolyton/threads/52411-1.htm : **Soren Johnson**, the AI default difficulty sets the AI's level; "on regent, cities start with 2 content citizens"; "the AI always plays at Regent". Settles content citizens (1.2, Disputed 1).
- https://codehappy.net/apolyton/threads/101437-1.htm : alexman (Firaxis), C3C 1.0: the AI plays at Regent "unless you change the "AI Default Difficulty Level""; Harovan on start unit 1/2. Added to 1.2 and Disputed 1.
- https://codehappy.net/apolyton/threads/41866-1.htm : Soren on when the AI accepts peace. Added to 3.4.1 (cross-reference to 6.5).
- https://codehappy.net/apolyton/threads/102345-1.htm : Risa (wars within 20 turns once land runs out), notyoueither (DoW 1 in 3-4 when told to leave). Both added to 3.4.2; Catt on the game-wide aggression setting is already covered (FLINTLOCK-WAR post #8).
- https://codehappy.net/apolyton/threads/39845-2.htm : Aeson, the AI declares war when you won't trade what it wants. Added to 3.4.2.
- https://codehappy.net/apolyton/threads/39845-3.htm : nothing new (second-hand Soren: the AI sees the whole map).
- https://codehappy.net/apolyton/threads/89030-1.htm : PTW 1.21 players, the AI declares war when it can't pay a per-turn deal. Added to 3.4.2.
- https://codehappy.net/apolyton/threads/61489-1.htm and -2.htm : nothing new (Catt's figures misstate the cost factor).
- https://codehappy.net/apolyton/threads/93287-1.htm and -2.htm : nothing new.
- https://codehappy.net/apolyton/threads/61900-1.htm : nothing new (poll).
- https://codehappy.net/apolyton/threads/50345-2.htm and -3.htm : nothing new (BillChin: "The current AI always expands to available space").
- https://codehappy.net/apolyton/threads/66928-2.htm : nothing new.
- https://codehappy.net/apolyton/threads/34826-2.htm : nothing new.
- https://codehappy.net/apolyton/threads/102278-2.htm : nothing new.
- https://codehappy.net/apolyton/threads/93177-2.htm : nothing new (no UN interval on page 2).
- https://apolyton.net/forum/civilization-series/civilization-iii/136613-difficulty-levels : nothing new (editor table).
- https://apolyton.net/forum/civilization-series/civilization-iii/133999-the-differences-in-difficulty-levels : nothing new.
- http://apolyton.net/showthread.php/143061-AI-bonuses-and-penalties-per-difficulty-level : not Civ3 (Civ4).
- Web searches (3, codehappy.net/apolyton.net only, by a helper agent): AI difficulty bonuses, AI war declaration, anarchy and city count.

Still unread for section 8: `Play Deity like Chieftain` 91523, `Finally winning on Deity` 100078, `Sid Strategies` 347484, `HOLY JIHAD!` 116220, the civ-review threads (opinion), `gotm2 and qsc` 150462, `A guide to tiny high scores` 78888, `top rank` 77599, `Useful Patches` pages 3-12.

## Third reading round (code readings)

C3X source read from a local clone of https://github.com/maxpetul/C3X (commit d4e71c4, 12 Oct 2026). Only its statements about what the unmodded 1.22 game does are used; mod features are out of scope.

- changelog.txt and injected_code.c (`aggressively_penalize_bankruptcy`): contribution: CORRECTION to 3.6 "Out of money"; the order there is a mod option, not stock.
- injected_code.c (`patch_Unit_ai_move_leader`): contribution: the stock leader AI disbands a leader on a continent without any of its cities (3.2).
- injected_code.c (`patch_Leader_ai_eval_war_target`, `patch_Match_ai_eval_city_location`, the evaluation wrappers): nothing new (wrappers only).
- https://forums.civfanatics.com/threads/useful-patches-for-civ-3-conquests-v1-22.526285/page-3 to page-12 (re-read with all sections in view): nothing new for section 8 (AI artillery and land-transport observations, already covered).
- https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/ (192 pages): read by helper agents in this round; their results were not merged before hand-back (see the round report).
