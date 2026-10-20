# Reading log, section 6 (diplomacy, espionage, AI attitude)

Deep pass, 2026-10-20. One line per source read: what it added, "nothing new",
"fetch failed", or "not Civ3".

## Local first-hand sources

- `Conquests/Text/diplomacy.txt` (all section tags): loan request/refusal lines, AIWILLRETREAT / AIWILLRETURNHOME / AIDONTTEST / AIBORDERWAR, AIPEACETREATYTHREAT / AINOPEACETREATY; confirmed no ceasefire strings.
- `Conquests/Text/script.txt`: MUTUALPROTECTIONOURWAR (MPP trigger wording), NODIPLOMACY, CONFIRMRENEGOTIATEPEACE, espionage message tags; MP_DIPLOCONTACT (multiplayer only, not used).
- `Conquests/Text/Civilopedia.txt`: GCON_Territory ("eject rival military units"), GCON_Espionage (screen at Writing), GCON_Embassies, GCON_Agreements, GCON_Maps.
- vanilla `README.txt`: 1.29f zero-turn-war fix, 1.21f per-turn-gold bargaining fix, 1.16f embassy-at-war popup and submarine fixes, Espionage button errata.
- `civ3PTW/readme.txt`: 1.21f "could not ask an AI civ to remove its units" fix, HN removal change (version headers checked).
- Ceasefire grep over vanilla, PTW and C3C text files and `Civ3Conquests.exe` strings: no ceasefire state.
- Manual text extract (scratchpad `manual/r.txt`, Steam manual.pdf, pp. 63, 77, 143, 153-175): first contact, contact permanent, peace/war only, agreement conditions and 20-turn terms, MPP needs Nationalism on either side, reputation recovery text, embassy reopening, trade items, Workers in capital, international incidents, sabotage half, Steal Plans rest of turn, UN majority.
- Strategy Compilation extract (scratchpad `strat/s.txt`, https://www.civfanatics.com/wp-content/uploads/2024/07/Civ3_Strat_Compilation.pdf): GPT valued about 18:1, removed units teleported to nearest non-offending spot, workers in capital tradable, AI trades big for cities.
- EDHELP (decompiled chm): AI to AI trade rate wording, mission base-cost formulas (Propaganda gold term), aggression level, diplomat experience, bribery modifier, Immune To.
- C3X-INI (https://raw.githubusercontent.com/maxpetul/C3X/master/default.c3x_config.ini): AIs don't demand from each other in stock, MA evaluation divides by distance, submarine-collision war, steal_plans_duration default 1.

## CivFanatics

- https://forums.civfanatics.com/threads/ai-attitude.44999/ (pages 1-8, all read): embassy -2 confirmed, war declaration +4/+1/+2 cap, MA target +10, starving +1, privateers, disbanding foreign city = razing, gift caps and decay, espionage +3 temporary / +1 permanent, nukes permanent, polite UN threshold, resource-deal composition (+1/+4), no penalty for refusing offers.
- https://civfanatics.com/civ3/strategy/game-mechanics/ai-attitude-study/ (re-read): only "-2" embassy lines; decay quotes.
- https://forums.civfanatics.com/threads/ai-human-trade-revisited.42110/ (single page): nothing new beyond the cited table; no formula for known civs.
- https://forums.civfanatics.com/threads/ai-attitude-and-trade.59079/ (single page): nothing new; AI offer "81% of the maximum negotiated price".
- https://forums.civfanatics.com/threads/trading-reputation.113376/ (pages 1-3): rep-risky deals, restore by cutting contact, destroy before contact, peace-treaty dispute, Tone/Whomp troop position.
- https://forums.civfanatics.com/threads/reputations.160891/ : tribute "give in" anecdote only.
- https://forums.civfanatics.com/threads/tech-trade-valuation-help.217576 : not Civ3 (Civ4 forum).
- https://forums.civfanatics.com/threads/trade-is-difficulty-with-ai-civs.115977/ : SJ Frank monopoly x2 (opinion), AI lacks money.
- https://forums.civfanatics.com/threads/how-do-ais-value-techs-in-market.126379/ : TimBentley factors (government/draft/mobilization), "no formula computed".
- https://forums.civfanatics.com/threads/a-study-of-espionage-mission.97042/ (pages 1-4): carefully x1.5 / safely x2, flag details, plant modifiers by level, exposed 90%, steal tech at war with planted spy, AI espionage.
- https://civfanatics.com/civ3/strategy/game-mechanics/a-study-of-espionage-missions/ (re-read): "multiply by 50% for carefully" wording.
- https://forums.civfanatics.com/threads/diplomatic-difficulties.44888/ : not Civ3 (Civ2 forum).
- https://forums.civfanatics.com/threads/first-post-why-cant-i-see-all-the-civs.120770/ : "within a space of their units" (one account); diplomacy screen holds eight.
- https://forums.civfanatics.com/threads/tmits-guide-to-un-victories.304815 : not Civ3 (Civ4).
- https://forums.civfanatics.com/threads/the-road-to-diplomatic-victory.40013/ (pages 1-2): majority of all civs (TimBentley), candidate priority (EMan).
- https://forums.civfanatics.com/threads/are-all-civs-equal-for-diplomatic-victory.34475/ : one vote per civ, >50%.
- https://forums.civfanatics.com/threads/diplomatic-victory-deity-level.19457/ : candidates vote for themselves (anecdote).
- https://forums.civfanatics.com/threads/diplomatic-victory-question.112336 : only UN owner calls vote, 11 turns.
- https://forums.civfanatics.com/threads/expanding-the-un.419281 : not Civ3 (Civ5-era ideas thread).
- https://forums.civfanatics.com/threads/everyone-keeps-messing-with-me.406900/ : nothing new (anecdotes).
- https://forums.civfanatics.com/threads/trespass-doctrine.690905/ : telling AI to leave lowers attitude (Spoonwood).
- https://forums.civfanatics.com/threads/all-about-ai-aggression-levels-and-war-plotting.703785/ (pages 1-2): code-level effective aggression, demand chance, war-plot rolls, MPP exclusion, DiploEffect by attitude, shunned gov 5 points, war declared at attack.
- https://forums.civfanatics.com/threads/how-does-the-ai-price-resources-and-luxuries.695776/ : luxuries priced by happy faces (justanick); nothing tested on strategic resources.
- https://forums.civfanatics.com/threads/whats-a-good-rule-of-thumb-to-trade-techs-with-ai.700894/ : competing sellers lower price; research-cost fractions.
- https://forums.civfanatics.com/threads/multiplayer-ai-aggression-or-lack-thereof.700748/ : nothing new (MP/BIQ corruption issue).
- https://forums.civfanatics.com/threads/use-the-trade-tab-as-a-guide-to-military-alliances.692937/ : AI refuses embargo on trade partner; grey items without route.
- https://forums.civfanatics.com/threads/managing-the-world-the-machiavellian-doctrine.116771/ (page 1): loan interest depends on attitude, not military.
- https://forums.civfanatics.com/threads/the-lazy-mans-war-lets-you-and-him-fight.75972/ : nothing new (AI makes peace inside an MPP term; anecdote).
- https://forums.civfanatics.com/threads/loaning-money.449848 : 18 gold per 1 gpt.
- https://forums.civfanatics.com/threads/ai-refusing-gold-per-turn.26118/ (page 1): AI pays GPT only from excess (owner-ruling source).
- https://forums.civfanatics.com/threads/trading-mainly-for-beginners-but-others-may-find-it-useful.24553/ : ROP by territory, map by uncovered tiles, luxuries by happy faces, contact prices fall (1.29f).
- https://civfanatics.com/civ3/strategy/introductory/trading-tips-for-beginners/ : same Bamspeedy points, nothing further.
- https://forums.civfanatics.com/threads/mutual-proctection-do-you-sign-on-or-not.8546/ : MPP fires on first attack (hedged), partners declare next turn.
- https://forums.civfanatics.com/threads/cancel-right-of-passage.228499/ : cannot cancel in first 20 turns; cancel before declaring.
- https://forums.civfanatics.com/threads/clarification-on-peace-treaties.113271 : nothing new (20-turn peace, renewal anecdotes).
- https://forums.civfanatics.com/threads/how-do-you-get-ceasefires.173866 : not Civ3 (Civ4).
- https://forums.civfanatics.com/threads/civ-3-espionage-mission.31904/ : nothing new (failed plants can cause war; anecdotes).
- https://forums.civfanatics.com/threads/spaceship-and-espionage-question.267825/ : not Civ3 (Civ4 BTS).
- https://civfanatics.com/civ3/faq/ (both halves): one vote per civ, "Cities (as part of a peace deal only)", second candidate by score, ships count as units in territory.
- https://forums.civfanatics.com/threads/how-do-you-end-a-trade-embargo.11624/ : nothing new (end via Active button; Tone on rep hit for peace inside an alliance).
- https://forums.civfanatics.com/threads/right-of-passage-agreements.427477 : units "ejected, sometimes to odd places"; workers not booted.

## Apolyton (apolyton.net and the codehappy.net mirror)

- https://codehappy.net/apolyton/threads/74045-1.htm : TheNiceOne's first formula guess "[(Research cost * A + B) / CF + C] * AH + D" (not used; superseded by "revisited").
- https://codehappy.net/apolyton/threads/74045-2.htm : fetch failed (404; single page).
- https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/86979-ai-human-trade-revisited : nothing new (mirror, one page).
- https://codehappy.net/apolyton/threads/70620-1.htm : nothing new (asks, no answer).
- https://codehappy.net/apolyton/threads/67912-1.htm : nothing new.
- https://codehappy.net/apolyton/threads/48219-1.htm and -2.htm : Soren on AI-AI full value, trade rate "how much the AI will bend", tech-value function.
- https://codehappy.net/apolyton/threads/40548-1.htm and -2.htm : Soren "same function ... AI-to-AI and human-to-AI", value falls with more civs.
- https://codehappy.net/apolyton/threads/49524-1.htm : nothing new.
- https://codehappy.net/apolyton/threads/40489-1.htm : nothing new (1.16f trading-on-your-turn bug; fixed per 1.17f readme).
- https://codehappy.net/apolyton/threads/68856-1.htm : trade rate is "deficit spending only" (Soren via WarpStorm).
- https://codehappy.net/apolyton/threads/41866-1.htm and -2.htm : Soren on when the AI talks peace.
- https://codehappy.net/apolyton/threads/57428-1.htm : nothing new.
- https://codehappy.net/apolyton/threads/68602-1.htm : tribute giving-in anecdotes (military superiority).
- https://codehappy.net/apolyton/threads/71589-1.htm : "troops approach" works with a real threat; joncnunn no-deficit GPT.
- https://codehappy.net/apolyton/threads/55198-1.htm : units teleported when removed; destination disputed.
- https://codehappy.net/apolyton/threads/53506-1.htm (page 1 of 2): bad rep blocks GPT and ROP; Catt "no affect on trade prices".
- https://codehappy.net/apolyton/threads/64554-1.htm, -2.htm, -3.htm : alexman opinion on per-turn deals vs sneak attacks; pages 2-3 nothing new.
- https://codehappy.net/apolyton/threads/85162-1.htm : nothing new (opinions).
- https://codehappy.net/apolyton/threads/86477-1.htm (page 1 of 2): Konquest02's list of embassy-gated agreements.
- https://codehappy.net/apolyton/threads/46862-1.htm : Aeson "Writing (and establishing an embassy)" for ROP (owner-ruling source).
- https://codehappy.net/apolyton/threads/97851-1.htm : PTW 1.27 peace ends alliances (player report).
- https://codehappy.net/apolyton/threads/43821-1.htm : 1.17f notes (AIs don't sign ROP with each other; tech ~6x cost at Regent); not used, superseded by later patches.
- https://codehappy.net/apolyton/threads/59851-1.htm : worker trading rules and price; alexman nationality bug.
- https://codehappy.net/apolyton/threads/62078-1.htm : nothing new.
- https://codehappy.net/apolyton/threads/33796-1.htm : cort, MPP needs Nationalism on one side only.
- https://codehappy.net/apolyton/threads/54597-1.htm : nothing new (unexplained war, 1.21f).
- https://codehappy.net/apolyton/threads/37521-1.htm : nothing new.
- https://codehappy.net/apolyton/threads/60084-1.htm (page 1 of 2): same-turn auto-fail claim (player1).
- https://codehappy.net/apolyton/threads/107183-1.htm (page 1 of 4): ambassador cannot investigate at war (Jaybe).
- https://codehappy.net/apolyton/threads/49422-1.htm : AI uses espionage in 1.21.
- https://codehappy.net/apolyton/threads/63254-1.htm : PTW espionage screen preview (no rules).
- https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/57646-how-do-i-get-more-than-peace-treaty-for-ending-war : peace first, then other items.
- https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/43819-exploit-or-strategy : nothing new (200 gold for 20 gpt exploit, 2001).
- https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/41120-all-questions-about-trade-answered : nothing new (pre-release trade rules).
- https://codehappy.net/apolyton/threads/33523-1.htm : sabotage made the AI "start the production over" (one report).
- https://codehappy.net/apolyton/threads/36792-1.htm : nothing new (propaganda culture chances already in BIQ).
- https://codehappy.net/apolyton/threads/34978-1.htm : nothing new for section 6 (saved RNG seed note).

## Other

- https://civilization.fandom.com/wiki/Diplomacy_(Civ3) : fetch failed (402).

## Left unread (section 6 entries in sources.md)

CFC: Some notes about diplomacy and Foreign relations (already marked non-Civ3),
Modern Age Big Picture Tech Trading (692320), Using Settlers to Steal Resources
(698017), Pangea Maps and Trading (689998), How do you get the AI to go to war?
(77022), Shut down trade (160680), Sneaky tactics during Peace (7619), How to
form a strategic alliance (154837), befriending AI (189245), Successful
espionage / Steal technology re-reads, Machiavellian Doctrine page 2, AI
refusing GPT page 2, Trading Reputation War Academy page re-read.
Apolyton: AI - human trade revealed later pages (none exist), Give peace a chance
p.2, How do trade agreements work? (33095), Another cheap exploit (33963),
Trading Absurdity (49585), Tip to trade tech with AI (59508), How overseas
trading works (99668), AI diplomacy is not logical (33455), ai hates me
(59601), AI annoyed by Privateers? (64882), 1.16f - trade broken (37376),
Embassies, how do you make them? (31344), Reputation: What's the point? p.2,
AI opening embassy p.2, Cost of spying p.2, Espionage--worthless? pp.2-4, Spies...who needs them? (80040),
Spies: Useless or Indispensible? (81355), How do I plant a spy (64558),
UN threads (51960, 93177, 75554, 39204),
v1.17 AI vs AI tech trading (42905), What technologies don't the AIs research
(61744).

## Second reading round (2026-10-20)

Code-level:

- https://github.com/maxpetul/C3X/blob/master/injected_code.c (`try_ai_demand_from_other_ai`, `patch_Leader_get_attitude_toward`): the stock demand gates in order (power place, attitude and aggression rolls, war-damage balance, 32-turn cooldown, MPP block on the war roll); the attitude score's sign (higher is worse). Added to 6.9 and 6.7.
- https://github.com/maxpetul/C3X/blob/master/Civ3Conquests.h (`struct Reputation`): the attitude memory counters. Added to 6.7.
- https://github.com/maxpetul/C3X/blob/master/changelog.txt : the extraterritorial-colony sign fix (supports the sign reading); nothing else for section 6.
- https://github.com/maxpetul/C3X/blob/master/README.md : nothing new for section 6 (bug list only; "Possible division by zero in AI logic to evaluate proposed alliances").

Apolyton (codehappy.net mirror):

- https://codehappy.net/apolyton/threads/42905-1.htm : **Soren Johnson** on AI-AI tech trades (full value, half value at worst, only known civs count, buyer's progress, per-turn deals only from income). Added to 6.8 and 6.6.
- https://codehappy.net/apolyton/threads/42905-2.htm, -3.htm, -4.htm : AIs at war don't trade with each other (Zachriel, ShuShu); wonder-tech value observation (Ironikinit); Aeson's AI-AI price ratio (not used).
- https://codehappy.net/apolyton/threads/49585-1.htm, -2.htm, -3.htm : observed strategic-resource prices (Hurricane, player1); unsourced "cities only for peace" claim (Random Passerby). Added to open item 7 and Disputed.
- https://codehappy.net/apolyton/threads/59508-1.htm : JohnM2433, AI GPT capped at income. Added to 6.6.
- https://codehappy.net/apolyton/threads/59508-2.htm : nothing new.
- https://codehappy.net/apolyton/threads/81355-1.htm : Jaybe, no spy lockout in 1.16, 3-4 turns in PTW. Added to Disputed.
- https://codehappy.net/apolyton/threads/81355-2.htm : nothing new.
- https://codehappy.net/apolyton/threads/80040-1.htm : nothing new (opinions).
- https://codehappy.net/apolyton/threads/80040-2.htm : nothing new (restates the PTW readme's "2-4 turns"; Odin's planting-at-war anecdote).
- https://codehappy.net/apolyton/threads/33095-1.htm : nothing new.
- https://codehappy.net/apolyton/threads/33963-1.htm, -2.htm, -3.htm : nothing new (road-cutting resource exploit, gold overflow bug; vanilla 2001).
- https://codehappy.net/apolyton/threads/99668-1.htm : nothing new (war cuts a coastal trade route; unsourced AI-AI rep claim).
- https://codehappy.net/apolyton/threads/33455-1.htm, -2.htm : nothing new.
- https://codehappy.net/apolyton/threads/59601-1.htm, -2.htm : nothing new.
- https://codehappy.net/apolyton/threads/64882-1.htm, -2.htm : nothing new (privateer ownership anecdotes; the study already has the privateer effects).
- https://codehappy.net/apolyton/threads/37376-1.htm : nothing new.
- https://codehappy.net/apolyton/threads/31344-1.htm : nothing new (embassy basics).
- https://codehappy.net/apolyton/threads/64558-1.htm : nothing new.
- https://codehappy.net/apolyton/threads/75554-1.htm, -2.htm : nothing new (no UN interval or candidate rule).
- https://codehappy.net/apolyton/threads/39204-1.htm : nothing new.
- https://codehappy.net/apolyton/threads/61744-1.htm (page 1 of 2) : nothing new (anecdotes).
- https://codehappy.net/apolyton/threads/41866-2.htm : nothing new.
- https://codehappy.net/apolyton/threads/53506-2.htm : nothing new.

Local and CivFanatics:

- `<civ3>/Conquests/conquests.biq` TECH flags (QueryCiv3, scratch reader `r2tech`): trade over sea = Astronomy, over ocean = Navigation and Magnetism, map trading = Navigation, communication trading = Printing Press, MPP and embargo = Nationalism, MA and diplomats = Writing, ROP = Map Making. Added to 6.4.
- `<civ3>/Conquests/Text/Civilopedia.txt` `GCON_Trade` (water routes need Map Making / Navigation / Magnetism), `TERR_Sea`, `TERR_Ocean`, `TERR_Coast`: the sea-route tech discrepancy. Added to 6.4.
- https://codehappy.net/apolyton/threads/79615-1.htm : alexman (Firaxis) on privateers costing attitude, not reputation. Added to 6.7.
- https://forums.civfanatics.com/threads/how-to-form-a-strategic-alliance.154837/ : ending an alliance by destroying the enemy (SimpleMonkey, TimBentley). Added to 6.3.
- https://forums.civfanatics.com/threads/pangea-maps-and-trading.689998/ : one partner needs Astronomy; barbarian galleys cut coastal routes; Great Lighthouse. Added to 6.4.
- https://forums.civfanatics.com/threads/befriending-ai-the-fastest-way.189245 : not Civ3 (Civ4).
- https://forums.civfanatics.com/threads/how-do-you-get-to-ai-to-go-to-war.77022/ : fetch failed (CFC error page).
- https://forums.civfanatics.com/threads/shut-down-trade-for-an-entire-civilization-during-wartime.160680/ : nothing new (pillage and blockade advice).
- https://forums.civfanatics.com/threads/using-settlers-to-steal-resources.698017/ : nothing new for section 6 (border-flip anecdotes, section 3).
- https://forums.civfanatics.com/threads/modern-age-big-picture-tech-trading-lower-difficulty-levels.692320/ : nothing new (broke AIs can't buy techs at low levels).

Still unread for section 6: Sneaky tactics during Peace (7619), 77022 (retry), Machiavellian Doctrine page 2, AI refusing GPT page 2, 61744 page 2, and the re-reads listed under "Left unread" above. The UN threads (75554, 39204) and the Apolyton list there are now read.
