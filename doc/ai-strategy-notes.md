# AI strategy notes from SuedeCivIII's single-player guides (first pass)

Written by the Strategy Researcher, 2026-10-29. Goal: a stronger Advanced AI with **no difficulty bonuses**.
A tip counts only when a paired trial shows it helps. Nothing here has been tested yet.

## What was read

Read from auto-captions (no raw transcripts are kept in the repo). Times are `m:ss`.

| Key | Video | ID |
|---|---|---|
| EYN | Civilization 3: Everything you need to know | zK171Ww3ETk |
| FUN | 4 Fun Strategies | wpLEOIAy9_Y |
| M7 | Top 7 New Player Mistakes | -1A9i20JFBs |
| M6 | 6 More Mistakes That New Players Make | cXZjtYmEsvA |
| CP | Your City Placement Sucks! | aZSm4iqno8s |
| CMB | Be Better At Combat | n0Q7DDh920c |
| T12 | The 12 most powerful techs | 9GHQXp4b5_A |
| MI | Top 7 Mistakes Made By Intermediate Players | 3nlb-_3N8WI |
| ADV | 5 Top Tips For Advanced Players | iw7JychSETE |
| T101 | 101 Tips And Tricks | 8WeGVE84mVU |
| WK | Worker Management Tutorial | lUovJxu1WsE |
| BW | The 5 Types Of Bad Worker Moves | c59tob8-G5o |
| DIP | Diplomacy Tutorial | SpxprNTrONA |
| GOV | Government Tutorial | _LKcoZd6yYM |
| MECH | 6 Mechanics That Are Hard To Explain | xLjQyZGI9a0 |

**Second pass (ZUR-35).** Every other video on the channel has since been looked at; the per-video status is in
`doc/suede-video-ledger.md` and every extracted tip in `doc/suede-tip-log.md`. The new, de-duplicated tips are in
"Second pass: tips from the rest of the channel" at the end of this file (numbers 50 onward). The first-pass list
above stands. (The first pass cited M7 with a mistyped ID; the video is `-1A9i20JFBs`.)

**Overlap with `doc/ai-test-plan-suede-tips.md`.** That plan (from a different video) already holds E1-E23. Where a tip
here is the same, the line names the E number and adds no new trial. The owner picked only E3, E10, E4 and
`hunt-barbs` for testing; the rest of that plan is still open and several are the best bets below.

**Caveat on rules.** Suede is a strong player, not the rulebook. Tips marked **VERIFY** state a rule; check it in the
game (or OpenCiv3's rules) before building on it. Where the game or an owner ruling disagrees, it wins.

Columns: **AI today** = does the Advanced AI already do it (code cited). **Trial** = how a paired trial would measure it
(`AiTrialTest` measures; *new* = needs adding).

## Expansion and city placement

| # | Source | Tip | AI today | Trial |
|---|---|---|---|---|
| 1 | EYN 6:40; M7 0:00-2:03; M6 7:44 | Expand first. Early on build only warriors, workers, settlers (and a granary or two); no wonders, temples or walls until the land is taken. | Partly. `DevelopmentAI` has an expansion phase (`stats.InExpansionPhase`); wonder and military switches exist (`wonder-timing`, `lean-military`) but are untested/off. | E5, E6, E7 in the old plan. Cities, city10turn, score. |
| 2 | CP 0:00-2:00; T101 52:53-58:00 | Do **not** plant on bonus food (the food is lost). Planting on bonus commerce is good (not the capital); on a 1-shield tile pays at size 7. **VERIFY** the city-centre rule in OpenCiv3. | No. `SettlerLocationAI` adds the centre tile's own yield, so it rewards settling on bonus food. | E2 `site-food`. |
| 3 | CP 2:32-4:32; M6 3:07-4:40 | Every land tile should be inside some city's work area; cities 2-5 apart (3 tiles between against the AI). Don't leave good tiles unusable. | Partly. Distance penalty in `SettlerLocationAI.DistancePenalty`; no check that a bonus tile is left outside every city's reach. | Land, citizens, cities. Fold into E2. |
| 4 | CP 3:32-4:32; ADV 3:06 | Settle on the coast (harbour) and on a river or lake (no aqueduct). | Partly. Site score has coast/water terms, but no "lake or river, no aqueduct needed" term. | E2 (river/lake term). |
| 5 | M6 4:40-6:41 | First extra cities: high food (or high shields) 3-4 tiles from the capital, never more than 5. | Partly. Distance penalty favours near sites. | City5turn, city10turn. |
| 6 | CP 5:03; ADV 5:40 | Settle towards rivals and contested land first; don't leave gap sites for AI settlers to walk in. | No. Sites near rival cities are penalised. | E9 `contested-first`. |
| 7 | CP 7:36 | Take whole islands before moving on; fewer borders means fewer wars. | No. | Cities, wars. Low priority. |
| 8 | CP 8:06 | Plant close to the AI to pull its resources into your borders. | No. | Land, luxuries. Low priority. |
| 9 | M6 0:00 | Never build a courthouse in the capital. | **Yes.** `ChooseProducible.cs:473-476`. | None. |
| 10 | T101 36:11-39:56 | More cities are always good, if far from the capital (rank corruption). Rings placement is gone. | Partly. The AI keeps settling; no push for far sites. | E13. |
| 11 | EYN 6:40; T101 46:43 | Build the settler at size 3; a city growing this turn can finish the settler the same turn. | Partly. `ChooseProducible.CityIsTooSmall` exists. | E16. |
| 12 | MI 5:11-9:17 | Block settling/landing with a wall of cheap units. | No. Hard to build. | Skip for now. |

## Workers

| # | Source | Tip | AI today | Trial |
|---|---|---|---|---|
| 13 | EYN 4:36; WK 1:34; GOV 6:43 | Despotism: mine green, irrigate brown. | **Yes.** `terraforms.lua` `yield_improvement` (no grassland irrigation, no plains mines under the despotism penalty). | None. |
| 14 | WK 3:36-9:21 | Edge cases the rule gets wrong: sugar/deer on plains, cow on grassland (irrigate, don't mine), hills (don't mine under despotism). Compute real yield after the penalty. | No. The code only blanket-refuses two cases. | E8 `worker-yields`. |
| 15 | WK 10:21-13:06 | Don't move workers in a stack onto new tiles; improve before roading; improve tiles the city is using first. | Partly. | Unimproved tiles, citizens. Part of E8. |
| 16 | WK 13:06-15:16; BW 5:08 | Don't hook up resources you don't need yet; don't rush a road to iron/luxury. | No. The AI adds 20 points to roads on any known luxury/strategic tile (`ai_score.road`). | E8. |
| 17 | BW 1:30-3:33 | Don't improve tiles outside every city's reach; don't build outposts without a plan. | Unknown. | Unimproved tiles. |
| 18 | BW 8:44-9:15 | Chop before roading; don't chop forest on tundra/fur/deer tiles. | Partly. `ai_score.clear_forest` only chops when food-short. | E15. |
| 19 | M7 12:20; MI (workers); ADV 0:03 | At least 1.5 workers a city, more at high difficulty; build them in corrupt cities. | **Yes** for 1.5. `ChooseProducible.cs:382,390`. Not for "more at high difficulty" or corrupt cities. | E13, E18. |
| 20 | BW 11:18 | Rail/road the best work first (mine a plains before railroading hills). | Partly (`railroad_transport_points` below a mine). | None. |

## Research and economy

| # | Source | Tip | AI today | Trial |
|---|---|---|---|---|
| 21 | EYN 5:07; T12 | Philosophy first (free tech), then trade. | No. `DevelopmentAI.ChooseResearchAsCiv3` follows the stock Civ3 AI's weighted pick; "Bonus Tech" is worth 0. | New: `philosophy-first`. Techs, score. |
| 22 | T12 6:40-8:13; EYN 15:26; CMB 8:08 | Steam Power (railroads) is the strongest tech; Replaceable Parts (artillery, infantry) second; Flight, Industrialisation next. Get coal and iron. | No. Research is the stock weighted pick, which values units, governments and flags but not rails. | New: `railroad-first`. Turn Steam Power known, production, score at 300 turns. |
| 23 | EYN 10:49; MI 0:00; GOV 9:46 | Play Republic (commerce bonus); Monarchy is the easy fallback. | **Yes.** `GovernmentAI.Score` adds the trade bonus and subtracts war weariness and unit-support cost. | None. |
| 24 | EYN 12:53; M7 7:41-9:45; GOV 7:14 | Under Republic, cut units: strong units, few of them; core cities need none; disband outdated units. | Partly. `ObsoleteUnits.DisbandOverSupport` (obsolete units only). Garrison is `StockGarrisonBonus` per city regardless of border. | New: `core-light`. Gold, unit-support cost, cities lost. |
| 25 | M7 2:34-5:37; MI 5:11; EYN 11:19 | Fix unhappiness with luxuries, then military police, then the happiness slider (10-40%); not with temples or entertainers. An entertainer costs about 7 gold/turn, the slider 1. | Partly. Luxury slider is capped at 5 (`PlayerAI.cs:1702`, `MAX_AI_LUXURY_SLIDER`); the rest goes to entertainers (`FixRemainingUnhappyCities`). A trial showed dropping the slider hurts (`no-ai-luxury`). | New: `lux-slider-high`. Citizens, entertainers, disorder, score. |
| 26 | MI 3:07-5:11; DIP 10:20; EYN 11:19 | Import luxuries from AIs for useless techs and resources. | **Yes.** `DealProposalAI` / `PlayerAI.AttemptTrading` buys a luxury or strategic it lacks. | None. |
| 27 | MI 9:17-11:25; ADV 8:11-9:13; DIP 6:12 | Trade techs constantly, even cheap ones for 10-20 gold, and with everyone on contact. | Partly: tech-for-tech plus lump gold on random rolls (`PlayerAI.TechDeal`, `:1542`). | E1 `trade-gpt`. Techs, beakers, gold, score. |
| 28 | MI 10:51; ADV 8:41; T101 85:39-87:10; DIP 5:40 | Pay and take gold **per turn**; the AI only counts gold per turn up to your surplus, so drop science to show a bigger surplus, make the deal, then raise it back. | No. Tech deals pay lump gold; gold per turn only for resources. | E1. |
| 29 | ADV 8:11; MI 10:51 | Buy techs with gold per turn and resell them; tech lead = income, not just tech. | No. | E1 (add: resale). |
| 30 | M7 8:42; T101 | Keep trade reputation: never break a 20-turn deal. | Unknown. `TradeGuards.cs` exists. | None. |
| 31 | M7 4:11; MI 6:07 | Granaries are for expansion only (1 per ~9 cities); don't build them later. | No. | Buildings, upkeep, gold. |
| 32 | T101 36:50; MECH 7:34 | Forbidden Palace in a corrupt city with good land. | **Yes** (`ChooseProducible.cs:594-597`). Choice of site not checked. | Low priority. |
| 33 | T101 71:35-74:15 | Science can be turned down when the tech finishes anyway; no overflow. Move citizens so food and shields aren't wasted. | Unknown. | Gold, beakers. Low priority. |
| 34 | M7 5:37 | Culture beyond 10 for the first border expansion does little; don't build temples for it. | Partly. | Buildings, upkeep. |

## War

| # | Source | Tip | AI today | Trial |
|---|---|---|---|---|
| 35 | M6 7:44-13:00; M7 10:47; EYN 9:16 | Attack in the expansion phase: AIs build settlers, not defenders. Barracks in every city, five attackers from the capital, one stack at the enemy capital or a size 3+ city, then back to settlers. | No. `WarCampaign`/`StockWarPlotting` ignore the enemy's defender tech and the expansion phase. | New: `early-rush`; E12. Captured, civCaps, cities, wars, unitsLost. 16 games of 300 turns, every-civ check. |
| 36 | CMB 0:31-4:36; MI 11:55; ADV 10:43 | Defeat in detail: only attack with enough units to kill every defender; never lose two attackers per defender. | **Yes.** `WarCampaign` gathers a stack and attacks only if `CampaignStrongEnough`; `ReadyHealth` 0.7. | None. |
| 37 | CMB 5:06-5:36 | Ancient era: attackers favoured, take size 3+ cities; size-1/2 cities are razed. | Partly. `PickTarget` favours size but still takes small ones. | E4 (owner's selection). |
| 38 | CMB 7:06; ADV 11:14 | Retreating units (knights, veterans) that survive don't give the enemy promotions. | Unknown. | Attack wins, unitsLost. |
| 39 | CMB 8:08-8:39; ADV 12:48-13:49 | Artillery + railroads, bombard defenders to 1 hp. | Partly. Siege units in the stack. | Out of scope for now. |
| 40 | CMB 9:39; T101 5:07 | Armies don't die to the AI at full health; build and use Great Leader armies. | **Yes.** `ArmyAI`. | None. |
| 41 | CMB 10:11 | Put many units in a captured city for one turn to put down resistance. | Unknown. | Cities lost to flips. |
| 42 | DIP 1:01; T101 83:34 | Declare war **before** moving units into their land (else rite-of-passage reputation drops). | Unknown. | Low priority. |
| 43 | CMB 10:41 | Don't fight at sea; use boats to move land units only. | Partly. | None. |
| 44 | MI 11:55-13:00 | Wait for hurt units to heal before pushing on; disband obsolete units after a war. | **Yes.** `RestAI`; `ObsoleteUnits`. | None. |
| 45 | MECH 11:38-14:09 | War weariness comes from losing cities and units; short wars. | Partly. `GovernmentAI.WarPressure`. | None. |

## Diplomacy

| # | Source | Tip | AI today | Trial |
|---|---|---|---|---|
| 46 | M7 10:47; ADV 9:13; DIP 7:45 | Ally against the runaway leader; weaken the strongest, strengthen the weakest. | Partly. `PactAI` handles alliances when asked; the AI doesn't seek them against a leader. | E11, E12; every-civ check. |
| 47 | DIP 9:19-10:20 | Sell excess strategic resources; give them to weak civs. | Partly. | Gold. |
| 48 | MI 9:48 | Contact every civ early (sacrifice galleys, scouts). | Partly. | E10 (owner's selection). |
| 49 | T101 82:32 | Hold only one copy of a resource to avoid AI demands. | No. | Low priority. |

## Not for the AI, or not for default rules

Pop-rush and chain-slave rules (ADV 1:04), goody hut rules (T101 31:33; MECH 0:31), spy flags, map generation notes,
Golden Age timing, FUN's four strategies (Temple of Artemis/Sistine, war-chariot rush, Persian Immortals bluff,
archipelago wonder stacking), tourism wonders. The war-chariot rush is the same idea as #35.

## Top five untested tips, by my expected win-rate gain

My estimates, not measurements. Ranked by how much of an economy/war swing Suede attributes to them, how often
they apply, and how cheap they are to build. Posted to the CTO on ZUR-27.

1. **Tech trade for gold per turn, on first contact and on new tech** (#27-29; E1 `trade-gpt`). Suede names trading as the main edge on every difficulty. Moderate build.
2. **Early rush during expansion** (#35; `early-rush`, with E12). Biggest swing he claims; the AI is weakest on defence then. Larger build; needs the every-civ check.
3. **Luxury slider up to ~30% before entertainers** (#25; `lux-slider-high`). One constant behind a switch; the old `no-ai-luxury` trial points the same way.
4. **Food-first city sites** (#2-4; E2 `site-food`). Cities drive everything; fixes planting on bonus food and adds river/lake. Small build.
5. **Research Steam Power / Philosophy earlier** (#21-22; `railroad-first`, `philosophy-first`). Cheap tech-value tweak; the AI's stock research ignores railroads.

Runners-up: E8 `worker-yields` (#14-16), E5 `settler-first` (#1), `core-light` (#24), E9 `contested-first` (#6).

## Second pass: tips from the rest of the channel (ZUR-35)

Read 2026-10-30 from the auto-captions of the other single-player videos (full transcripts for guides and the
"Hardest Difficulty" series; keyword-filtered excerpts for the long streams, so a tip may be missed). Source codes are the
first six characters of the video ID (look them up in `doc/suede-video-ledger.md`), then the time. **Nothing here is
tested.** Where two videos disagree the line says so. Rule claims are marked **VERIFY**: Suede is a strong player, not the
rulebook, and the captions are machine-written, so check numbers in the game before building on them.

Videos that matter most: `Dl2HRV`/`lkjmNi`/`0zbT7V`/`b2QeCu`/`f_wvXo` (Deity/Sid streams, tech selling), `B_N_Sm` +
`xZEd7d` + `CQ3I4O` + `Yk1_ZK` + `CEVx58` (the "Hardest Difficulty Done Right" series: war), `UvMdec` (buildings),
`RCXLB8` + `-V6T3u` + `qkx_qy` (wonders), `lg12b5` (mechanics), `LeBaV5` (midgame economy), `YZDmln`, `MnfWob` (expansion).

### Diplomacy and trade

| # | Source | Tip | AI today | Trial |
|---|---|---|---|---|
| 50 | Dl2HRV 70:15; lkjmNi 130:18, 139:41; 0zbT7V 162:58; MuPr3V 118:01; OIwNJe 103:35; EMGfLE 144:08 | The AI pays a lot for government techs (Monarchy, Republic, Democracy, Communism, Nationalism); sell them to several AIs in turn (about 1300 gold each in his games), sell to the richest buyer first, and don't buy them (they are priced very high: QaLghI 48:50; iCPJ3t 10:48). | Partly. `TechDeal` and `AiTechPricing` price a tech at the buyer's cost (Civ3's rule); no step that picks the richest buyer or sells the same tech again to the next civ. | E1 `trade-gpt` (add: sell the same tech down the list). Gold, techs. |
| 51 | b2QeCu 34:39, 57:23; B_N_Sm 53:15; MI/ADV (first pass #28) | Gold per turn is counted by the AI at about 70% of face value, and only up to your displayed surplus: set science to 0 first, pledge the biggest GPT you can, then restore science. **Videos disagree:** b2QeCu says prefer lump sums; 0zbT7V 87:52 and the first pass say prefer GPT (it also slows a rival's tech). | No. See #28. | E1; compare lump-sum and GPT arms. |
| 52 | 0zbT7V 87:52; 9-yx59 12:48; Dl2HRV 109:50; cUKu2w 103:56 | Don't sell or give war techs (railroad, artillery, bombers, Communism) or iron/saltpeter to the strongest neighbour; give them to a weaker civ. | No. `DealProposalAI.SellResource` refuses strategic resources only to civs it dislikes; techs are traded without regard to the buyer's threat. | New `no-arm-leader`. Wars lost, cities lost, score. |
| 53 | QnH_D6 30:19; Dl2HRV; 3zJvM6 43:25 | Sell resources for one-turn lump sums (not per turn); the AI cancels expensive per-turn deals by declaring war when the route is cut. | `DealProposalAI.SellResource` sells for gold per turn (halfway). | Low priority. |
| 54 | CEVx58 4:36, 20:44; -NssY4 156:22; 3zJvM6 43:25; Z1M1Vc 189:48; 6MB1Rk 117:32 | Keep weaker neighbours alive as buffers; set the strong AIs against each other (alliances, tech and gold gifts); don't attack the runaway AI while its stack is in position to hit you. | Partly. `PactAI` handles asks; no seeking alliances against a leader. | E11, E12 (see #46). |
| 55 | CQ3I4O 37:06; Yk1_ZK 48:17 | Take four or five cities per war, raze the big or flip-prone ones, make peace, wait out the 20-turn deal, repeat. Razing many cities hurts the AI's opinion (20+ razed is a global penalty). | Unknown. `PeaceAI` exists; no cap on cities per war. | New `war-bite`. Cities, flips, wars. |
| 56 | CQ3I4O 34:26; xZEd7d 29:31; CQ3I4O 8:43; first pass #30 | Don't declare war within 20 turns of a peace deal that gave you a tech or city (trade reputation); don't promise a resource whose route a war can cut. | Unknown. `TradeGuards.cs` exists. | None until checked. |
| 57 | YZDmln 29:27, 72:21; Ntkw5o 26:01 | Early on say yes to AI demands and let its units cross your land; refusing or ordering them out causes the early wars. Once at war, make peace the moment the AI accepts. | Unknown (`TerritoryDemands.cs`, `TributeDemands.cs`, `RightOfPassageAI.cs`). | New `soft-diplomacy`. Wars declared on us, cities lost. |
| 58 | YZDmln 53:25, 68:04; 9-yx59 12:48 | Keep Map Making (the AI then settles by sea) and Republic until you've expanded; give away cheap techs (Pottery, Currency, Mathematics) for gold first. | No. | Part of E1. |
| 59 | QnH_D6 72:44; ZUR first pass #42 | Avoid Right of Passage unless it stops a civ you can't fight; **disputed** (YZDmln 29:27 says grant it). | See #57. | Skip. |

### Research and economy

| # | Source | Tip | AI today | Trial |
|---|---|---|---|---|
| 60 | f_wvXo 9:05; QaLghI 108:24; yggDJg 21:42; u_B2rs 46:38; Z1M1Vc 33:26; CwiW63 97:16 | A tech can't take more than 50 turns if any science is allocated: set the slider on turn 1, run the minimum in expansion to bank gold, full-tech only Philosophy (or the one tech that wins a race), and don't switch mid-way. **VERIFY** (min/max tech turns). | No. | E19 `min-tech` already holds this. |
| 61 | UvMdec 9:42, 10:12, 11:15, 8:42; AmyDMP 5:32 | Building order in core cities: Marketplace, Library, Aqueduct, Courthouse, University, Harbor. With 6-7 different luxuries a Marketplace gives 6-9 content faces, so skip Temple/Coliseum/Cathedral until about size 14. **VERIFY** the market-happiness numbers. | Partly. `ChooseProducible` favours markets in big cities (:440); roles of temples/coliseums not checked. | New `market-first`. Buildings, happiness, score. |
| 62 | UvMdec 4:04, 8:11, 0:32; LeBaV5 22:33; MuPr3V 97:20 | Courthouse only in mid-ranked cities of size 5+ with something to save; Aqueduct only when a city nears its cap (size 5-6); Harbor only where 2+ ocean tiles will be worked. | Partly. `NeedsRoomToGrow` gates the aqueduct (:453); no courthouse rule beyond the capital. | Low priority. |
| 63 | LeBaV5 17:21; C9pwQZ 29:42 | Never build Wealth with spare production. | **Yes** (`ChooseProducible.cs:190`). | None. |
| 64 | RCXLB8 29:30, 62:00; -V6T3u 9:46; KA9-qc 66:33; Yk1_ZK 4:49; dEzNmq 11:17 | The Great Library is the one wonder to win on every difficulty (the AI ranks Literature low). **VERIFY**: it gives techs known by two or more civs, so it pays only while others are ahead; capturing it late yields more. | **Yes** in value (`WonderPlanner` prices Great Library techs). | None. |
| 65 | RCXLB8 25:25; QScGa5 87:38; EMGfLE 175:53 | Rush Theory of Evolution (the AI neglects Medicine); queue the techs you'd want next before it lands. | Partly (`wonder-value`). | `wonder-value` run. |
| 66 | -V6T3u 5:12; dEzNmq 8:15; RCXLB8 26:26 | Statue of Zeus (needs ivory, ~200 shields, a free ancient cavalry every 5 turns) and Sun Tzu (replaces barracks) are better than they look. **VERIFY** rates. | Unknown. | `wonder-value` run. |
| 67 | qkx_qy 8:41, 3:35; RCXLB8 0:00; nuIluH 2:33 | Skip Oracle, Universal Suffrage, Colossus (except tiny empires), Longevity, SETI, Cure for Cancer; Leonardo is overrated. | Unknown. | `wonder-value` run. |
| 68 | RCXLB8 15:17; 3e_oTh 7:37; nuIluH 66:53 | Don't delay expansion for wonders (a handful of cities at low levels, 1-2 at high); wonder gold scales with library/university/market, so build wonders in the city that has them. | Partly (`wonder-timing` builds in the best city while expanding). | `wonder-timing` run. |
| 69 | YZDmln 54:57; AmyDMP 9:03; CwiW63 33:39; U_KYbt 174:40 | Pop-rush (20 shields per citizen) only in corrupt cities, only for the last 20-25 shields, and with some shields already in the box (zero shields costs double). **VERIFY**. | Partly (`onHurry`; `GovernorProduction`). Not checked for these conditions. | E17 `poprush-defense`; new `rush-corrupt`. |
| 70 | B_N_Sm 34:13; Yk1_ZK 34:25; PwVh6- 163:30; IOvWgf 13:24 | Pick builds that fit the shield rate (10 shields makes a 30-shield unit every 3 turns); move workers or citizens to hit a break point. | No. | Low priority. |
| 71 | 3zJvM6 22:12, 65:29; 5CBA60 14:57, 17:29; d0xjoY 5:08 | Wartime production bonus: start a unit that gets the bonus, switch the box to the building you want; time a golden age to a war. **VERIFY** (wartime mode is a Conquests feature; check it is in default rules). | Unknown. | Low priority. |
| 72 | AmyDMP 6:02; CwiW63 86:39; wPESEy 36:18 | When behind in tech, science to 0 and buy the tech with gold per turn; techs get cheaper the more civs own them. | Partly (`AiTechPricing`). | E1, E19. |

### Government and happiness

| # | Source | Tip | AI today | Trial |
|---|---|---|---|---|
| 73 | LeBaV5 1:31; Yk1_ZK 37:17; ZjceNL 39:41; yggDJg 146:09; EMGfLE 58:09 | Grow core cities to size 7 before Republic (more unit support, bigger commerce bonus); buy aqueducts for that. | Partly. `GovernmentAI.UnitSupportCost` (:168) counts free units by size band; nothing grows cities to size 7 first. | New `size7-republic`. |
| 74 | Dl2HRV 87:50; MuPr3V 61:05; CwiW63 98:36; LeBaV5 7:40 | When switching to Republic, disband the early warriors at once and hold back worker count; accept 30-40 gold/turn of support rather than leave home empty. | **Yes** in part (`ObsoleteUnits.DisbandOverSupport`; obsolete only). | `core-light` (first pass). |
| 75 | jWw1DL 77:08; bmEszo 112:32; iCPJ3t 13:23; U_KYbt 182:09 | Switch government only at peace (not mid-war); stay out of Republic in a long war with many units. | Unknown. | None. |
| 76 | f_wvXo 74:02; mH5SOu 142:09; wmL-EO 5:39; u_B2rs 161:49 | Republic by default; skip Monarchy and Feudalism when Republic is near; Communism to close a won war. **Disputed:** Ntkw5o/YZDmln keep Monarchy as an easy fallback (first pass #23). | **Yes** (`GovernmentAI`). | None. |
| 77 | f_wvXo 211:36; bmEszo 225:20 | The AI picks Monarchy at peace and Republic/Democracy at war; keep a rival at war to keep it out of Republic. **VERIFY**; **disputed** by BHQ8_l 97:49 (don't force wars: Republic AIs tech faster). | Not applicable. | Skip. |
| 78 | 3e_oTh 18:55; MnfWob 13:55; LeBaV5 9:40; WzGLhx 193:00 | Use military police in far corrupt cities, entertainers never early, and aim for zero entertainers in the capital and its six nearest cities by slider. | Partly (`PlayerAI` slider loop, :1723). | `lux-slider-high` (first pass). |
| 79 | jhxl7C 33:48 | A lapsing luxury deal makes "we love the king day" flicker; renew luxury trades before they expire. | Unknown. | Low priority. |
| 80 | 3zJvM6 23:27; LeBaV5 2:01 | Cut the luxury slider by importing spare luxuries; a 10% slider was worth about 95 gold a turn in one game. | **Yes** (`DealProposalAI` buys luxuries). | None. |

### War

| # | Source | Tip | AI today | Trial |
|---|---|---|---|---|
| 81 | B_N_Sm 36:48; 3UZmrS 94:53; Yk1_ZK 3:48 | Strike before the target gets Pikemen (Feudalism), Musketeers or Walls; tempo matters more than numbers. | Partly. `WarCampaign` weighs defenders it sees, not the era window. | `early-rush`, E4. |
| 82 | xZEd7d 10:23, 9:21; BHQ8_l 66:45; -NssY4 88:50 | Turn the first Great Leader into an Army; win easy fights with a regular unit to earn the promotion that makes leaders, keep the elite for hard fights; spend a leader on Heroic Epic over the Forbidden Palace unless that city is very corrupt. | **Yes** in part (`PlayerAI.cs:571,597` use leaders). Which use first is not checked. | New `leader-army`. |
| 83 | xZEd7d 23:14; Yk1_ZK 27:42; -NssY4 250:24; f_wvXo 339:31; CEVx58 23:52 | The Civ3 AI won't attack a full-health Army; keep a full-health Army with stacks and in border cities. (The AI will hit an Army missing an HP or two.) | Unknown for the Advanced AI's own targeting. | New `army-escort`. Cities lost, unitsLost. |
| 84 | xZEd7d 44:39, 40:31; wKrCQ4 32:55; ovXDaU 9:15; jWw1DL 58:14; OIwNJe 86:31; CwiW63 29:31 | Culture flips: a captured city can't flip the turn you take it; pile units in to put down resisters; don't build in a flip-prone city; raze or replant big-pop ones; a city with no foreign citizens or tiles can't flip; kill the old owner outright and it can't flip back. **VERIFY** each. | Partly. `CultureFlip` exists in the engine; the AI's reaction to it isn't checked. | Cities lost to flips (`onCultureFlip`). |
| 85 | CQ3I4O 21:15; Dl2HRV 90:01; lg12b5 20:58 | Road the line to the front before the war (roads give nothing in enemy land); keep two routes; pull hurt units back. | Partly (`RestAI`). | Low priority. |
| 86 | B_N_Sm 39:00, 41:36; Yk1_ZK 18:51; 6QAtWU 15:08 | Hold captured cities with cheap units so attackers stay free; don't hit fortified veterans on hills (about +75%) or walled cities with catapults (they do nothing to walls). | Partly. | E23 `finisher`, E22. |
| 87 | Yk1_ZK 38:19; tyyCDL 9:55; 3zJvM6 36:56 | Wait for a target to shrink below size 7 (about +50% vs +25% defence); block mountain passes and chokepoints to cut cities off; the AI doesn't defend or block passes. | No. | Low priority. |
| 88 | ur_pWo; d0xjoY 5:38; VZquUM 7:38, 8:40; ovXDaU 5:41; 5CBA60 20:34 | Late-game attackers: bombers and artillery as the core, not battleships/cruise missiles/helicopters; artillery on railroads with settlers takes a continent. | Not checked (unit pick is by stats). | Out of scope for now. |
| 89 | dEzNmq 4:40; Dl2HRV 39:16, 48:11 | On Regent the AI sends unescorted settlers: capture them with an early unit; don't rely on pillaging, and prefer making yourself stronger to weakening an AI. | Unknown. | Low priority. |
| 90 | jhxl7C 14:16; ur_pWo 1:03; YZDmln 43:15 | The AI judges strength by raw attack/defence and unit count; many cheap warriors or a few high-attack units keep it from declaring war. | Not applicable (it is a rule of the stock AI). | Skip. |

### Expansion, placement and workers

| # | Source | Tip | AI today | Trial |
|---|---|---|---|---|
| 91 | B_N_Sm 4:06, 8:18; EMGfLE 52:51, 37:21; DHDbdR 40:07 | Contested land first, four tiles apart, fill gaps later; park two units on a contested tile; take whole islands. | No. | E9 `contested-first`. |
| 92 | riHr1j 6:38; DNlbpy 14:46; MnfWob 13:24; CwiW63 96:16 | Delay the first worker until the capital's first settler is out; second worker after 3-4 cities; build warriors/workers with the shields while the city grows to size 3; the new city makes workers and scouts. **Differs from** #19 (1.5 workers a city). | Partly (`ChooseProducible.cs:382,390`). | New `late-worker`; E5 `settler-first`. |
| 93 | cUKu2w 164:27; 0zbT7V 125:39; BHQ8_l 82:07; wKrCQ4 9:26 | Capture or plant on coal/iron/saltpeter/rubber/aluminium you lack; deny coal to AIs. | Unknown (target scoring in `WarCampaign.PickTarget`). | New `resource-target`. |
| 94 | QnH_D6 38:23; Kszjb6 35:58; QaLghI 23:12 | Plant denser on 80% water maps, looser on 60%. **VERIFY** (said: corruption follows map size, not land area). | No. | Low priority. |
| 95 | YZDmln 4:04, 14:27; cUKu2w 24:27; f_wvXo 9:39; OIwNJe 140:46 | Irrigate before road; irrigate a cow or bonus grassland when food surplus is under five, mine when over; chop only into a build that can use the shields (no overflow). | Partly (`terraforms.lua`). | E8, E15. |
| 96 | cUKu2w 17:52; 6MB1Rk 151:44; QaLghI 203:20; KA9-qc 224:17 | Railroad N-S and E-W lines first (diagonals give less), flat tiles before hills, around the least corrupt factory city. | Unknown. | Low priority. |
| 97 | BHQ8_l 172:02; MnfWob 31:32; x-b5jD 8:25 | Never automate workers when trying to win; build workers in corrupt cities; always road a tile you improve. | Not applicable (the AI automates by design). | Skip. |
| 98 | YZDmln 5:05; mH5SOu 28:51; MnfWob 23:16; 8nSxHt 5:51 | Goody hut: no free city or settler if you are above the average city count or have a settler in production; switch off Settlers before popping; do tech trades first. **VERIFY** (SvjbJ0 8:26 says otherwise for vanilla). | Partly. `ExplorerAI` detours for a nearby hut; no production switch. | Low priority. |

### Not for the AI, or contradicted

Challenge-game rules (no roads, one-city culture, always-war), Conquests-only wartime and airfield rules (`5CBA60`), scenario and save-file tricks, civ and trait tier lists (`1AE643`, `gHUigO`; they affect which civ to pick, not how to play one), the Space Race screen trick (`CQ3I4O 10:46`), exploits of how the stock AI behaves that only matter to a human (`nuIluH 24:36`, `mCC1hv 1:02`). The 5-reasons and tier-list videos on Civ 4-7 were skipped.

### Top untested additions, by my expected gain

My estimates, not measurements. All build on the first-pass five.

1. **Sell government techs down the list of rich AIs, and stop selling war techs to the leader** (#50, #52). Suede's biggest Deity/Sid gold source. Moderate build; fits E1.
2. **Strike before Pikemen/Musketeers, then stop after four or five cities** (#81, #55). Adds a timing rule to `early-rush`.
3. **Great Leader into an Army, and keep a full-health Army in border cities** (#82, #83). Small build if `ArmyAI` already forms armies.
4. **Market-first buildings, no Temple/Coliseum before size 14** (#61). One scoring change in `ChooseProducible`.
5. **Grow to size 7 before Republic** (#73). One term in `GovernmentAI.Score`.

## Third pass: tricks against the AI's weaknesses (ZUR-68)

Written by the Strategy Researcher, 2026-10-30. Sources are CivFanatics and Apolyton threads (2002-2011), read through
web search and page summaries, so quotes are second-hand. **Almost all of it is anecdote from the stock Civ3 AI.** Rule claims are marked **UNVERIFIED**
until checked against the game or `doc/civ3-compendium/`; Rule 5 applies. Not found: Sirian, Aeson and Bamspeedy guides
(search returned none; their points reach me only as forum quotes). The SuedeCivIII videos not yet covered are left to the ZUR-35 ledger.
Tip numbers continue from 99. Source keys are the thread names; links are in the Links list at the end.

Columns: **Switch** = proposed name. **AI today** = checked in `C7Engine/AI/`. **Measures** = for a paired trial
(16 games of 300 turns, every-civ check for war/diplomacy changes).

### 1. Early war against weak AIs

| # | Source | Tip | AI today | Switch | Measures |
|---|---|---|---|---|---|
| 99 | A-archer-rush (Dominae, Nor Me, Egbert) | Five archers (about 100-120 shields) against the nearest neighbour, go for the capital first; capital often a size-1 city after a settler. Claimed payoff: 300+ beakers of techs and room to expand. Not tested on Deity by the posters. | Partly. `early-rush` is built (`WarCampaign`); count and timing rules are not from this source. | `rush-archers`: 5 archers, launch when the target has 2 cities or fewer. Adds to `early-rush`. | Cities, captured, techs at turn 100, unitsLost. |
| 100 | A-archer-rush (Alexman); R-rush (anon.) | Strike before the target has Bronze Working or Spearmen; "above Prince the AI starts with archers" (that is a difficulty bonus; we play with none, so it does not apply to our AI vs a Regent-bonus-free AI). | No. `PickTarget` does not look at the target's defender tech. Same as #81. | `rush-window` | As #99, split by target defender tech. |
| 101 | A-archer-rush (Dominae, Arrian) | The AI always counterattacks, sending archers "in ones and twos" without spearmen. Fortify on hills/forest next to its city and kill them one at a time; time the next counterattack from when its cities complete units. | Partly. The stack fortifies (`WarCampaign.cs:439-477`); no choice of terrain at the staging tile. | `stage-on-defence`: stage on the best defence tile in reach. | unitsLost per kill; campaign success rate. |
| 102 | A-archer-rush (Dominae) | After the field army is dead, a city holds "a spearman or warrior at most." Follow up at once. | Partly. `CampaignStrongEnough` rechecks as the stack arrives. | None. | None. |
| 103 | A-archer-rush (Nor Me, Arrian) | Autoraze can fire on a size-1 city you wanted. Raze only when you do not want the city; keep size 2+ cities. | Unknown. | `keep-size2` (needs engine check of captured-city rule). | Cities held after 30 turns. |
| 104 | A-archer-rush (Chemical Ollie vs Dominae) | Reputation: Ollie says a rush may make the world gang up; Dominae says it matters little if you never raze. **Disputed. UNVERIFIED** in OpenCiv3. | Attitudes track `workersCaptured` and `tributeBluffsCalled` (`Attitudes.cs:258`). | None until checked. | Wars started against us after a rush. |

### 2. Capturing workers and settlers, razing, peace terms

| # | Source | Tip | AI today | Switch | Measures |
|---|---|---|---|---|---|
| 105 | Capturing-Enemy-Settlers; Plucking-workers; Zouave in AI-stupidities | At war, the AI walks settler+spearman teams and workers into your land unescorted. Kill or capture them; a worker costs it far more than it costs you to take. The AI does not guard workers at war (anon. in "too many workers"). | **No.** `CombatAI` hunts barbarians only (`CombatAI.cs:15`); no code targets a rival's workers or settlers. The rules for capture exist (`GameData.cs:867-935`: captured settler becomes two workers, captured worker keeps its nationality). | `snipe-workers`: when at war, a spare fast unit attacks a rival worker or settler in range if the tile has no defender. | workersCaptured, enemy worker count, own unitsLost. |
| 106 | Plucking-workers (Grey Fox, Chieftess) | In Conquests units cannot attack coastal workers from a boat if a defender is on the shore. **UNVERIFIED** for OpenCiv3. | n/a | None. | None. |
| 107 | Plucking-workers (Grille, Grey Fox) | The AI disbands workers it captures if it expects to lose them next turn; with escorts the worker is kept. | n/a (describes the stock AI). | None. | None. |
| 108 | emperor-level-advice (vmxa1, bernardjan) | Wars of 5-6 turns with Mounted Warriors then Cavalry; short wars finish before war weariness. | Partly. See #45. | None. | None. |
| 109 | Sirian via "flaws in the rules" (Analyst Redux, post 11, 13) | Razing and starving a captured city pays better than holding it; captured wonders give a non-builder no culture or score. Wonder cities are the highest flip risk. **UNVERIFIED** (Civ3 culture rules differ by patch). | Unknown. See #84 (flip). | `raze-wonder-city` | Score, cities lost to flips. |
| 110 | how-do-I-have-a-splendid-little-war; when-will-AI-sue-for-peace | A defeated AI offers gold or cities for peace, often when you are about to take another city; its demands drop when your power rises (e.g. after you found a city). Anecdote only; table of tiers seen was from Civ5 and is ignored. | Yes in outline. `PeaceAI.TributeDemanded` asks for `PeaceTributePerCity` x their cities when `outlook >= Winning` (`PeaceAI.cs:192`); `MaxCitiesToCede` is 1 when losing badly. | `peace-squeeze`: when winning, ask gold plus one city, then continue. | Gold from peace, cities gained. |

### 3. Diplomacy and trade

| # | Source | Tip | AI today | Switch | Measures |
|---|---|---|---|---|---|
| 111 | Crippling-the-AI's-Research (civfanatics.com/?p=170) | Give each new tech to **all** civs on the same turn; the AI re-trades among itself and also pays gold per turn, up to what its economy can bear. When 5-6 techs ahead, hand over only harmless dead-end techs. Payment is capped by the AI's income. | Partly. `trade-gpt` is built; the all-at-once and dead-end-only rules were not in the first pass. | `sell-to-all` | Gold, tech lead, techs held by rivals. |
| 112 | Trading-techs-for-GPT (Aceman101, gmaharriet) | Price floor: over 20 gold per turn for a Middle-Age tech, 30 Industrial, 40 Modern; trade on your turn so the AI cannot pass it on first; do not sell to a civ that cannot pay. | Unknown. `AiTechPricing.cs` sets the seller's ask from buyer cost. | `gpt-floor` | Gold per turn earned per sale. |
| 113 | AI-refusing-gold-per-turn (pstanhag); Trading-techs-for-GPT | Probe the AI's limit: raise the per-turn ask one step at a time until it says "never"; step back one. Use a lump sum if it says "never" at the first step. | Unknown. | `gpt-probe` | Gold per sale. |
| 114 | Trading-techs-for-GPT (Othniel) | Pay the AI to sign a Military Alliance and sell it a tech for gold per turn the same turn; the AI often breaks the alliance early but keeps paying until term. **UNVERIFIED** in OpenCiv3; possibly a bug to avoid. | No. | Skip until a rules check. | None. |
| 115 | flaws-in-the-rules (Sirian, post 10; Nakar Gabab, post 9) | The AI suffers war weariness, collapses into communism and loses trade; getting AIs to fight each other works. Betrayal of treaties has no lasting penalty for the AI. | Partly. `PactAI` handles alliances when asked; the AI does not seek wars between others. | `incite-war` (ask an ally to declare on the leader; see #46). | Rival wars, leader score. |
| 116 | AI-stupidities (Zouave) | A war-time AI builds happiness wonders instead of units; settler/spearman teams keep arriving. | n/a | None. | None. |

### 4. AI weaknesses in the field

| # | Source | Tip | AI today | Switch | Measures |
|---|---|---|---|---|---|
| 117 | flaws-in-the-rules (Sirian, post 3) | The AI reacts to threats within about two tiles of a city, with two defenders each and more only in a front-line city. Stop three tiles away, then strike. **UNVERIFIED** (tested by one player on Emperor). | Partly. `WarCampaign` has a staging tile (`:882`); distance not tuned. | `stage-3` | Defenders met per attack, captured. |
| 118 | how-to-kill-a-big-army (DogCiv, Coracle, Jawa Jocky) | Bait with an empty square, or a lightly held city, then ring the AI stack and kill it in the open with artillery then cavalry; hold hills either side of a corridor. | No. Our AI does not set bait. | `bait-stack` (large; later). | Enemy units killed in the open. |
| 119 | how-to-kill-a-big-army (asleepathewheel) | Split an incoming stack by placing cheap units on different tiles so the stack fragments. | No. | Skip for now. | None. |
| 120 | flaws-in-the-rules (Sirian, post 11, Analyst Redux) | Cut AI land into pockets; the AI treats each as separate and counterattacks without coordination. | No. | `cut-pockets` (target ranking by cut value). Large. | Campaign speed. |
| 121 | crafty-ai (Lanzelot, Buttercup) | The AI's naval landings are poor: it lands one boat at a time, leaves landed units next to empty cities, and does not know non-amphibious units can take workers from a boat. | n/a. Check our own `FerryAI`: do we land all at once, and never leave a loaded stack one turn off the beach? **UNVERIFIED** | `land-together` | Landing success, units lost at sea. |
| 122 | AI-stupidities (mongolhorde) | When your stack nears its capital, defenders sometimes sally out for futile attacks on workers or weak cities, leaving the city open. | n/a (stock AI). | None. | None. |
| 123 | flaws-in-the-rules (KoenigMkII) | The AI keeps old defenders in the interior; blitz past the border to the railway-linked core. | Partly. `PickTarget` size-first. | `deep-target` (Industrial era; low). | Captured. |

### 5. Expansion and land

| # | Source | Tip | AI today | Switch | Measures |
|---|---|---|---|---|---|
| 124 | emperor-level-advice (vmxa1) | Capital pumps a settler about every 4 turns; add a settler at size 6-7 in good cities; coastal city early with galleys for contact. | Partly. See #11, #48. | `capital-pump` | City5turn, city10turn. |
| 125 | settler-worker-pumps (Theseus, Sava, Panag) | Poor cities (and captured cities left at size 1) make workers only; high-food river cities grow large and build; granary in the first pump city. Sell the city's current build on turn one to avoid the hurry penalty (**UNVERIFIED**). | Partly. | `pump-cities` | Workers per city, cities. |
| 126 | Deity-help (anon., via search) | Early war is the only way to keep up with the AI's expansion at higher levels; send settlers behind the army to claim conquered land on crowded maps. | No. After a capture the AI does not send a settler. | `settle-behind` | Cities taken by war that are replanted. |
| 127 | AI-stupidities (Zouave) | The AI floods open land with settlers, even beside its own colonies and while at war, and sends settlers to the same spot twice. Claim the contested land first (see #6, #91). | No (E9 `contested-first` is open). | E9. | Land, cities. |

### Not for the AI, or doubtful

The "AI cheats" claims in "AI stupidities" (Infantry moving two tiles, free techs, magic galleys) are about vanilla 1.x and are not rules. The Wall Street gold-lending trick (1 gold per turn loaned for interest, roughly 8 gold over 20 turns) pays nothing worth the code. Reputation-based and tribute-based claims from Civ4/Civ5 pages were skipped. Culture-border "claiming" (the Owner's area 5) found no Civ3 source beyond "border cities check AI expansion" (Apolyton `43463`, Randomturn); no switch proposed.

### Top 8 by expected win-rate gain against effort

My estimates, not measurements. Ranked by swing, how often it applies, and cost.

1. **`snipe-workers`** (#105). No code today; a few lines in the war-time unit step. Cheap, clear metrics.
2. **`rush-archers`** (#99, #100). Owner's lead; extends the built `early-rush` with count, timing and the defender-tech window.
3. **`stage-on-defence`** (#101). Make the rush stack survive the counterattack by choosing defensible ground. Small.
4. **`sell-to-all`** (#111) with **`gpt-floor`/`gpt-probe`** (#112, #113). Gold is the biggest steady gain; first check what `trade-gpt` already does.
5. **`settle-behind`** (#126). Wins captured land before the AI walks in. Moderate.
6. **`stage-3`** (#117). One constant in the staging distance; UNVERIFIED rule, so run it as a trial only.
7. **`land-together`** (#121). Check `FerryAI` first; may need no code.
8. **`peace-squeeze`** (#110). Tune `PeaceAI` demands when winning; needs the every-civ check.

Runners-up: `incite-war` (#115), `capital-pump` (#124), `bait-stack` (#118, large).

### Links

- A-archer-rush: https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/102930-successful-archer-rush-can-you-lose-afterward
- R-rush: https://forums.civfanatics.com/threads/early-game-rush.318390/latest and https://forums.civfanatics.com/threads/warrior-rush-thats-it.262707/
- how-to-kill-a-big-army: https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/62424-how-to-kill-a-big-army
- flaws-in-the-rules: https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/53452-article-concerning-flaws-in-the-rules
- emperor-level-advice: https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/116176-emperor-level-advice
- crafty-ai: https://forums.civfanatics.com/threads/crafty-ai.551704/
- AI-stupidities: https://forums.civfanatics.com/threads/ai-stupidities-in-civ3.16900/post-196241
- Plucking-workers: https://forums.civfanatics.com/threads/plucking-workers-from-the-coast.72176/
- Capturing-Enemy-Settlers: https://forums.civfanatics.com/threads/capturing-enemy-settlers.517311/latest
- Crippling the AI's Research: https://civfanatics.com/?p=170
- Trading-techs-for-GPT: https://forums.civfanatics.com/threads/trading-techs-for-gpt.230607/
- AI-refusing-gold-per-turn: https://forums.civfanatics.com/threads/ai-refusing-gold-per-turn.26118/latest
- settler-worker-pumps: https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/93001-using-certain-cities-primarily-as-settler-worker-pumps/page2
- splendid-little-war: https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/80496-how-do-i-have-a-splendid-little-war
