# AI behaviour review, October 2026

Three groups worked on this review. The local session reviewed the unit AI and fixed it. The PC session ("Civ3 Question Taker") reviewed the empire AI only and made no edits. A fallback review of the empire side was also done locally.

Every change that affects how strong the AI is was run through a paired `AiTrialTest`. Each trial played 8 generated games of 200 turns, on Pangaea and on Continents. Changes that made the AI weaker were dropped. The trial civs were compared against the same civs without the change. Numbers are the difference per civ, ± the standard error. A `*` means the difference is larger than two standard errors.

## Kept

| Change | Where | Trial (score, Continents / Pangaea) |
|---|---|---|
| Workers flee from visible enemies and skip jobs that enemies could reach. Settlers keep out of the land of civs they are at war with, and wait or fall back when fewer than 2 defenders are with them. At war, no city sites next to enemy land. | `AIThreat`, `WorkerAI`, `SettlerAI`, `SettlerLocationAI` | +12 / +126 |
| War campaign fixes: no cooldown after a won campaign; a civ planning a war that gets attacked switches to that war; a war that can't be reached doesn't block planning one that can; a stalled march regroups; the stack stays together after an attack on the way; a threatened captured city keeps 2 defenders. | `WarCampaign` | +115 / -48 |
| Economy fixes: capped cities no longer starve (the food penalty had been set to zero); threatened cities switch to building a defender; unit upgrades come after hurrying; courthouses count lost commerce; aqueducts and hospitals are wanted at the size cap; at peace, walls are built only near rivals. | `CityTileAssignmentAI`, `DevelopmentAI`, `ChooseProducible`, `PlayerAI` | +264 / +178 |
| Peace: an unreachable war is Even and ends in white peace; an even war ends in white peace after 30 turns (`warStartTurn` is saved); the peace price for a winner is now 20 per city, no more than a loser offers (25); no peace while allied against the enemy. | `PeaceAI`, `PlayerRelationship` | roughly 0 (rarely triggered in AI-only games) |
| The trespass war and joining an alliance require a war the AI would win: it can reach the enemy, it has no other war, and it is 1.5× the enemy plus that enemy's mutual protection partners. (Tribute demands did too, until they were replaced by Civ3's own demand gates; see the later section.) | `PeaceAI.WouldWinWarWith`, `TributeDemands`, `TerritoryDemands`, `PactAI` | roughly 0 (aimed at humans) |
| A settler's own walk lowers the value of a site more than 3 tiles away. | `SettlerLocationAI.FindSettlerLocation` | +16 / +56 |
| Workers road the gaps that join cities and luxury or strategic resources to the capital. This uses a quarter of the workers, on our own land only. | `WorkerAI.ComputeConnectionRoads` | +59 / -11 (a version using a third of workers on any land: -225* on Pangaea) |
| No extra workers for a disconnected city on another landmass, or once there are 1.5 workers per city. | `ChooseProducible` | +33 / +6 |
| With humans only: no techs, resources or gold per turn to a civ the AI plans to attack; no techs to a Furious civ; no strategic resources to a stronger civ on the same landmass; a premium on techs that 1 or fewer other civs know; incoming gold per turn is discounted from a risky payer. Between AIs these guards scored -322 on Pangaea, with fewer techs. | `TradeGuards`, `Player.WouldAcceptDealFrom` | not applicable |
| No trading with, or offering a right of passage to, a planned campaign target. An AI offers a human at most one deal every 10 turns, and the turn of the last offer is saved. | `PlayerAI.AttemptTrading`, `RightOfPassageAI` | not run |
| Bug fixes: city tiles count as roads in the trade network; luxuries are priced by what they are worth to the buyer; units fortified outside our land re-plan each turn; defenders go to a city they can reach and drop plans for cities that were lost; campaign members don't count as defenders; settlers re-check their site every turn and retarget within the same plan; a ferry is released when its settler retargets (this fixed a crash); idle workers go to a city; a human's automated worker recovers after an error. | various | not applicable |

## Tried and dropped (the AI got weaker)

- **Exact win odds** (the race-of-rounds probability): Continents -143. `EstimateChanceToWin` is badly calibrated: it reports 30% for a fight that is really about 16%. But the attack thresholds were tuned against it. Changing it would mean retuning the thresholds in a trial.
- **Defend home first** (drop a campaign while enemies are in our land): -136 / -80, with fewer cities captured*.
- **Peacetime barbarian hunting**: -60 / -120, with fewer citizens*.
- **Sending defenders by need** (more for the capital and threatened cities): -116 / +19, with a smaller army*.
- **Re-checking odds right before an attack**: -85 / -205, with fewer cities*.
- **Units on a path don't fight whatever steps onto it**: +25 / -220.
- **Garrison sorties and rethinking spare attackers**: within noise, so not adopted.
- **AI mood governing at the disorder check**: disorder fell 5 turns per civ*, but score was -167 / -100.
- **Disbanding units to stay solvent**: -10 / -37.
- **Per-city "at war" for production**: -38 / -16.
- **Dropping the mass wake at priority re-evaluation**: within noise.
- **`lean-military`** (an existing `AiExperiments` switch): -207 ± 127 on Pangaea, with fewer citizens*. Leave it off.

## Overall

These figures compare the final code against Development, across the same 8 seeds each, as averages per civ. The runs are unpaired, so a gap smaller than about 100 in score is within noise.

- Pangaea: score 1676 → 1702, citizens 131 → 137, research 222 → 236 beakers, disorder 4.9 → 3.5.
- Continents: score 1671 → 1641, citizens 125 → 126, research 199 → 216 beakers, disorder 4.3 → 1.9.
- On both maps, fewer cities change hands (captures 6.4 → 3.4 on Pangaea), while attacks and attacks won hold steady or rise.

## Still open

- No seaborne or amphibious campaigns, so wars across water can't be fought. They now end in white peace instead.
- Bombers don't support attacks; warships don't blockade or bombard enemy ports; there is no pillaging rule anywhere; nukes have no rule for when to escalate; units aren't upgraded by priority; healthy units never retreat from a stack that overwhelms them.
- Research has a single goal (Monarchy) and then always picks the cheapest tech.
- The choice between mine and irrigation ignores what the city needs (`terraforms.lua`).
- The `StrategicAI` priority layer has no effect beyond the mass wake.
- Refused offers to humans aren't remembered by kind; there is only the 10-turn limit.

## Sliders, war governments and start units (20 October)

Paired trials as above: 8 games of 200 turns on each map, the switch's civs against the same civs without it.

| Change | Where | Trial (score, Continents / Pangaea) | Result |
|---|---|---|---|
| `despotism-no-surplus`: in Despotism the AI takes back the last science step it gave up when the treasury can pay the small deficit, so it runs at about nothing a turn (the owner's decision). Before, it had a surplus on about 3% of its Despotism turns, nearly all from that overshoot. | `PlayerAI.SpendDespotismSurplus` | +167 ± 57* / +3 ± 79 (Continents: more cities*, citizens*, techs*, beakers*) | Kept |
| `war-government`: at war, the AI may change government when its war weariness level against an enemy is 2 or more under a government with war weariness, or when a war has lasted 30 turns. War weariness then counts for more in the choice, which favours Monarchy, Communism and Fascism. This follows chiefpaco (CFC 21298). | `GovernmentAI.WarPressure` | -29 ± 25 / +6 ± 47 (about 1 more turn of anarchy per civ) | Kept (the owner asked for it, and it is not clearly weaker) |
| `no-ai-luxury`: no luxury slider; only entertainers. This follows Ision (CFC 92917). | `PlayerAI.AdjustSliders` | -39 ± 62 / -36 ± 87 (disorder +3.2 / +1.7 city-turns, fewer citizens) | Dropped; the switch stays |
| The difficulty's AI start units come on the first turn the AI has a city, not only on turn 1. The flag is saved. | `Player.MaybeSpawnBonusUnits` | not run (a rules fix) | Kept |

## Stock war plotting, AI trade gate and the AI's extra optimal cities (20 October)

Paired trials as above, at Monarch: 8 games of 200 turns on each map. These follow code-level readings of the C3C 1.22 executable (compendium 08, sections 1.2 and 3.4.1).

| Change | Where | Trial (score, Continents / Pangaea) | Result |
|---|---|---|---|
| `war-plot-roll`: before it plots a war at peace, the AI must pass every one of Civ3's per-turn rolls (power rank, aggression, at war, anarchy, open land, war weariness, garrison, treasury). These replace waiting until no wide-open land is left (Flintlock, CFC 703785). | `StockWarPlotting.RollsToPlot` | +32 ± 44 / +76 ± 34* (wars declared -0.7 / -1.0*, cities lost -1.2* on Pangaea) | Kept |
| `war-target-score`: the AI picks its target by Civ3's score (power rank, distance, attitude, land units per city, pacts, trades, common enemies). Mutual protection partners are never targets, and others drop out at random. This replaces "the weakest neighbour, if we are 1.3 times as strong". The 1.3 check at the declaration stays. | `StockWarPlotting.Excluded`, `TargetScore` | +124 ± 67 / +87 ± 109 | Kept |
| Both together | | +42 ± 48 / +128 ± 56* (cities lost -1.6* on Pangaea) | Kept |
| `ai-trade-gate`: an AI talks trade with another AI only on a one in four roll each turn (C3X: "the base game's 1-in-4 gate before ai_negotiate_with_other_ai"). | `PlayerAI.RollsToNegotiate` | +25 ± 110 / -13 ± 169 | Kept (Civ3's rule; no measurable change) |
| `ai-ocn-bonus`: on Monarch and harder game difficulties, the AI's base optimal city number gets b/8, b/4 or b/2 more cities (C3X `ocn_formula.txt`, `n_ai`). | `Player.AiOptimalCityBonus` | +128 ± 89 / +34 ± 58 | Removed: against 24k cities from Civ3 saves, exact corruption matches fell from 77.9% to 69.3% with it |
| All four together | | +93 ± 51 / -93 ± 151 (military -134*, wars -1.2*) | within noise |
| The three without the trade gate | | -67 ± 95 / +34 ± 69 | within noise |

The combined runs move both ways by about one standard error, so they show no interaction beyond noise. All four switches became the default and are gone.

## Tribute gates and AI-AI tech prices (20 October)

These follow the compendium's second reading round (06, sections 6.8 and 6.9).

| Change | Where | Trial (score, Continents / Pangaea) | Result |
|---|---|---|---|
| Tribute demands use the stock gates that C3X re-creates (`injected_code.c`, `try_ai_demand_from_other_ai`). The AI asks only of a civ whose power place is at least half its own. It then makes a one-in-(own place) roll, an attitude roll (always passed at Cautious or worse), and an aggression roll. These replace a 5% chance a turn and the "stronger by the military advisor" check. No war follows a refused demand while the two have a mutual protection pact. The stock war-damage gate is left out because the engine does not track war damage. The owner's 40-turn cooldown stays in place of the stock 32 turns. | `TributeDemands.WouldDemandFrom`, `RollsToDemand`, `DemandFrom` | not run (AIs demand only from humans) | Kept |
| `ai-tech-buyer-price`: between AIs, a tech is priced at the buyer's research cost. Only civs the buyer knows count, and the buyer's own progress comes off. An AI with nothing more to give may buy for 100/rate of the value, never less than half (Soren Johnson, Apolyton 42905 and 48219). | `AiTechPricing`, `TradeOffer.GoldEquivalentFor`, `PlayerAI.TechDeal` | +82 ± 76 / -76 ± 88 (techs +1.8* / +3.3*, mid-game techs +1.4* / +1.3*, rights of passage +1.3* / +2.4*) | Kept (Civ3's rule, not clearly weaker); the switch is gone |

## Why AIs stopped declaring war after the stock war plotting (21 October)

With every civ playing by the stock plotting rules (8 games of 200 turns at Monarch, no switches), AIs declared about 0.2 wars each. Counting each gate in `StockWarPlotting.RollsToPlot` over those games (about 9,500 turns with 3 or more cities and no war within reach):

- The power-rank roll (1 in 32 - PowerRank, 1 in 4.5 on average) failed 66% of turns, and the aggression roll (1 in 12 at aggression 0) failed 92% of the rest. These are Civ3's own rules and directions (PowerRank 31 is the strongest, as in C3X's `power_rank` field).
- About one turn in seven of what was left passed the rest. The open-land roll applied 52% of the time and war weariness 23%. The garrison rolls (two defensive units per city) applied 77 to 93% of the time, and the treasury roll (under 16 gold per city) 92%. AI civs kept about 1.5 defensive and 1.5 offensive units per city and 5 gold per city.
- So about 35 plots came out of 64 civs and 200 turns, roughly what justanick's worked example in CFC 703785 gives (1/48 a turn for the strongest civ, 1/96 for the second). About half of them found a target: 2 in 3 civs at peace drop out at random, and some had no city within reach.
- At the declaration, WarCampaign's own check (1.3 times the target's strength) then turned down 10 of 18 campaigns on Continents and 22 of 29 on Pangaea. The stock code has no such check, and its target score favours the stronger civs.

| Change | Where | Trial (score, Continents / Pangaea) | Result |
|---|---|---|---|
| The garrison rolls and the target's land units per city count units by their BIQ AI strategy (AIOffense, AIDefense) in place of attack against defence. The Warrior and Chariot are offensive units in conquests.biq. A type with both strategies counts as defensive when at least as strong in defence. | `StockWarPlotting.IsOffensive`, `IsDefensive` | not run (a rules fix); all civs: wars 0.2 / 0.2 | Kept |
| `stock-declare`: a plotted war is declared once the attackers are gathered, without the 1.3 strength check (Flintlock: "War is declared only when the attack actually happens"). | `WarCampaign.Gather` | +11 ± 7 / -2 ± 38 (wars +0.1 / +0.2) | Kept; the switch is gone |
| `stock-treasury`: hurrying keeps 16 gold a city. | `DevelopmentAI.HurryProduction` | +38 ± 26 / +22 ± 29 | Dropped: treasuries rose only from 5.0 to 5.4 gold a city. The low treasury comes from the sliders, not from hurrying |
| `stock-garrison`: production favours defensive units up to 2 per city and offensive units up to 1 per city, by BIQ strategy (justanick, CFC 703785 #13; Ision, CFC 92917). | `ChooseProducible.StockGarrisonBonus` | -67 ± 54 / +64 ± 72 (units +9 / +15*) | Switch kept for the owner to decide; not the default |

AI trials with every civ AI and the same switches for all (8 games of 200 turns), per civ:

| Run | Wars declared | Captured | Attacks | Score |
|---|---|---|---|---|
| Before the stock plotting (1d69de73, Continents, as reported) | 1.0 | 2.1 | 27 | |
| Development before this change, 02912a11 (Continents / Pangaea) | 0.2 / 0.2 | 0.4 / 0.5 | 9.2 / 15.3 | 1624 / 1570 |
| Now: BIQ strategies and no strength check | 0.2 / 0.4 | 0.7 / 1.2 | 11.9 / 16.8 | 1611 / 1552 |
| Now, plus `stock-garrison` | 0.7 / 0.6 | 1.5 / 1.2 | 21.4 / 21.2 | 1533 / 1505 |

The faithful rules give rare wars in the engine for two reasons. First, the AI does not keep Civ3's garrison of two defensive units and one offensive unit per city, which justanick says makes the stock AI "eager to start wars". Second, it runs a treasury of about 5 gold a city against the 16 the roll asks for. Other things not from the stock code also hold wars back: no plotting below 3 cities (about 20% of turns), the 20-turn cooldown after a campaign, and targets limited to cities on our landmass within 14 tiles. Overseas targets count in Civ3, at half score.

## Civ3's AI gold reserve (20 October)

This follows Flintlock's code readings in the C3X thread (compendium 08, the upgrade and rush-buying bullets). There were two trials, each 8 games of 200 turns at Monarch on each map: a paired one as above, and an all-civs one (`C7_TRIAL_ALL=1`, every civ with the switch). The all-civs run was compared with a run at the same commit without the switch, on the same seeds. These runs include this branch's rules changes (bankruptcy, capture gold, zone of control, advanced barbarians and the rest), in both arms.

| Change | Where | Trial (score, Continents / Pangaea) | Result |
|---|---|---|---|
| `stock-gold-reserve`: the AI keeps 16 gold per city, plus the treasury that earns full interest (1000 gold with Wall Street), before it upgrades a unit or hurries one. It makes no upgrades at all while below that reserve. This replaces a flat 100 for upgrades and 10 + 2 a city for hurrying. Civ3's AI hurries only improvements, but this AI hurries only units, so the reserve applies to unit hurrying. | `DevelopmentAI.StockGoldReserve`, `PlayerAI.UpgradeUnits`, `DevelopmentAI.HurryProduction` | Paired: +9 ± 48 / +14 ± 25 (hurries -1.0* on Continents; citizens +5.7*, disorder +0.4*, wars +0.16* on Pangaea). All civs against the baseline, per civ: Continents score 1632 / 1623, cities 16.8 / 16.8, techs 33.0 / 33.0, units 75.6 / 75.8, war turns 1.7 / 1.3, gold 98 / 63. Pangaea score 1586 / 1576, cities 15.2 / 15.3, techs 39.5 / 39.0, units 69.5 / 71.0, war turns 3.4 / 5.2, gold 113 / 88. | Kept (Civ3's rule, not weaker); the switch is gone |

The first all-civs Pangaea run hung on turn 198 of its last game. An explorer kept stepping back and forth along a railroad, and those moves cost nothing. `UnitAI.PlayTurn` now ends a unit's turn after 300 free steps in a row. The baseline gave the same numbers with and without this fix.

## Civ3's war starting without the engine's own gates (20 October)

The owner's decision, 2026-10-20: "Defer to Civ3's logic on AI war starting. Strip away the restrictions we made as the default option." Civ3's plotting rolls and target score (`StockWarPlotting`) now decide alone. These engine gates are gone:

| Removed | Where |
|---|---|
| No war plotting below 3 cities | `WarCampaign.MaybeStartCampaign` |
| No plotting for 20 turns after a campaign ends. The cooldown now only holds off the next campaign in a war already under way, so its attackers fight on their own for a while | `WarCampaign.MaybeStartCampaign` |
| No plotting while at war with a civ within reach by land. Civ3's own one in 16 roll for being at war stays | `WarCampaign.MaybeStartCampaign` |
| Targets only among civs with a known city on our landmass within 14 tiles. Every civ we may declare on is now scored, and one with no city on a landmass we share gets half score, as in Civ3. A campaign city on our landmass may be at any distance | `WarCampaign.PlotWar`, `PickTarget` |
| A civ planning a war dropped the plan when a third civ attacked it | `WarCampaign.PlayCampaign` |
| `stock-garrison` is now the default (the AI builds toward 2 defensive and 1 offensive unit per city, justanick and Ision); the switch is gone | `ChooseProducible.StockGarrisonBonus` |

The engine has no seaborne campaigns. A target with no city our land units can march on (an overseas civ, or one whose cities we don't know) therefore gets the declaration at once, rather than when an attack is ready. The war is then fought by whatever the unit AI can do. `AiTrialTest` can now list each war (`C7_TRIAL_WARS=1`).

AI trials with every civ AI and no switches (8 games of 200 turns at Monarch, seeds 1000 to 1007), per civ. Before is Development at fc577537:

| Run | Wars declared | War turns | Captured | Lost | Attacks | Units | Cities | Score | Techs |
|---|---|---|---|---|---|---|---|---|---|
| Continents, before | 0.17 | 6.1 | 0.5 | 0.5 | 12.2 | 78.3 | 16.8 | 1626 | 32.8 |
| Continents, after | 0.44 | 18.1 | 0.7 | 0.7 | 16.7 | 91.3 | 16.5 | 1564 | 31.6 |
| Pangaea, before | 0.42 | 18.9 | 0.9 | 0.9 | 17.9 | 78.1 | 15.3 | 1564 | 38.7 |
| Pangaea, after | 0.97 | 43.7 | 1.5 | 1.5 | 26.1 | 93.6 | 15.3 | 1516 | 34.8 |

What the war lists show (after, all 16 games):

- Wars: 28 on Continents (11 before) and 62 on Pangaea (27 before). All but 3 came from war plotting; those 3 were allies joining by a pact. No run hung.
- Overseas wars: 3, all on Continents. None saw a single attack, and each ended in peace within 8 to 9 turns: `PeaceAI` takes a white peace with a civ it cannot fight. So an overseas war is in practice a short declaration with no fighting.
- Wars with no attacks on the same landmass: 2 of 28 on Continents and 6 of 62 on Pangaea (one of them a pact ally's). Before, there were 1 of 11 and 4 of 27. They end by the 30-turn stalemate peace, or were still under way at turn 200, so no civ stayed at war forever without fighting.
- Peace broken again at once: in 4 wars on Continents and 13 on Pangaea (2 and 3 before), the two signed peace and one declared war again within the same round of turns. Every case came from war plotting: once at peace, the at-war roll no longer applies, and peace only halves the target score and adds 2 to the exclusion roll. Flintlock believed there is a cooldown "within 8 turns of signing peace", but he left it unverified (compendium 08, section 3.4.1), so it is not applied. This is the main behaviour for the owner to look at.
- Scores are 62 and 48 lower per civ, and techs 1.2 and 3.9 lower, with about 13 and 15 more units per civ. That matches the all-civs `stock-garrison` run above (scores 77 and 47 lower).

## Improvement hurrying and mobilization (21 October)

The owner's decisions, 2026-10-21: the AI hurries improvements as Civ3's does, keeping its gold reserve (1000 / interest buildings + 16 a city; Flintlock, compendium 08), and it uses the new Conquests mobilization mode in a major war. Each switch had a paired trial and an all-civs trial (8 games at Monarch on each map). The all-civs runs are compared with a run without the switch on the same seeds. Both arms include this branch's rules changes.

| Change | Where | Trial (score, Continents / Pangaea) | Result |
|---|---|---|---|
| `stock-improvement-hurry`: under governments that hurry with gold, the AI also buys the improvement a city is building, after every unit reason, while the treasury stays at or above the stock reserve. Wonders are never bought. | `DevelopmentAI.HurryProduction`, `IsHurriableImprovement` | 200 turns. Paired: +3 ± 12 / -5 ± 10 (rushes +0.3 / +0.4; 6 and 4 of the 8 games came out the same). All civs against the baseline, per civ: Continents score 1556 / 1549, buildings 65.0 / 63.4, gold 103 / 105. Pangaea score 1516 / 1508, buildings 60.5 / 61.9, gold 95 / 108. | Kept (Civ3's rule, not weaker); the switch is gone |
| `ai-mobilize` (HEURISTIC): the AI mobilizes when at war with a civ on a shared landmass whose units' strength is at least 3/4 of its own, and returns to Normalcy once it may and is at war with no one. | `Mobilization.PlayAi` | 200 turns: no civ knew Nationalism, so it changed nothing. 350 turns, paired: +30 ± 52 / -73 ± 142. On Continents: turns mobilized +11.0\*, attacks +10\*, wars +0.4\*, cities +0.5\*. On Pangaea: turns mobilized +34.7\*, units +23, buildings -27, beakers -81, wars +0.6\*. All civs against the baseline, per civ: Continents score 2215 / 2210, cities 17.3 / 17.3, techs 58.3 / 58.8, buildings 178 / 177, units 170 / 171, mobilized 9.8 turns. Pangaea score 2113 / 2109, cities 15.4 / 15.4, techs 61.2 / 61.4, buildings 147 / 156, units 203 / 174, beakers 373 / 422, mobilized 26.3 turns. | Kept (within the noise; the world plays on); the switch is gone. On Pangaea a mobilized AI builds fewer buildings and does less research while it stays mobilized, which the owner may want to look at |

`AiTrialTest` now reports the turns each civ spent mobilized (`mobilized`).

## Great leaders (21 October)

The owner asked for better choices on using great leaders, labelled as heuristics and kept unless clearly weaker. These follow C3C's rules: a military leader forms an army or hurries an improvement or small wonder, never a great wonder.

| Change | Where |
|---|---|
| A military leader forms an army only when the civ is at war or has a war campaign planned, and at least 2 good attackers are within 6 tiles. A good attacker is an offensive unit with at least 3/4 of the attack of the civ's best one. Before, it also formed one at peace when the civ had no army. | `ArmyAI.ShouldFormArmyForWar` |
| Otherwise it rushes the improvement or small wonder with the most shields left, in any of the civ's cities, and goes there if need be. A build counts if the city is building it, or if the city would want it once the penalty for its build time is put back (the leader saves that time). Defensive buildings in a threatened city count half again, and production buildings a quarter again. The value drops by 3 shields for each tile the leader has to walk. A build is skipped if it is nearly done (less than half its cost, or 3 turns of the city's shields, left), and so is any non-defensive build in a threatened city. With nothing that qualifies, the older choice applies. | `PlayerAI.BestBuildForMilitaryLeader`, `UseMilitaryLeaderByChoice` |
| A scientific leader standing in a safe city of its civ is used there if it would save at least half as many shields as at the empire's best target. A probe of the trial games showed why: as cities switched production, the best target moved every turn, and one scientific leader visited 12 cities in 12 turns without being used. | `PlayerAI.UseScientificLeader` |

The trials were 8 games of 200 turns at Monarch on each map: a paired one, and an all-civs one compared with a run at the same commit without the switch.

| Trial | Continents | Pangaea |
|---|---|---|
| Paired (score) | +3 ± 4 | +58 ± 53 |
| All civs, score with / without | 1562 / 1562 | 1523 / 1506 |
| All civs, techs with / without | 30.3 / 30.3 | 35.9 / 35.2 |

Kept, and the switch is gone. Military leaders almost never appear in these AI-only games (about 0.1 per civ, with very few elite units), so the trials mostly measure the scientific leader change. With only the military changes, the first round played exactly the same games as the baseline.

## Other rulings of 21 October

These are rules changes, not trial changes:

- **Goody-hut techs** are now a weighted roll by `DevelopmentAI.TechValue`, the same way research picks. This replaces the inverse-cost weighting (`GoodyHuts.PickTech`).
- **Barbarians** search both ways: the 6 steps all round as before, and the stock game's NW/SE diagonal out to 12 steps. A reachable target on the diagonal comes first (`CombatAI.BarbarianDiagonalTiles`).
- **Air superiority** used to be cleared at the end of the round, so a fighter set by a civ late in the turn order lost the next round's interturn. It now lasts until its owner's next turn begins (`TurnHandling.BeginPlayersTurn`, `MapUnit.airSuperiorityTurn`, which is saved).
- **AI-vs-AI war weariness** already gave each side its own points; a test now covers it.
- **Railroads:** the engine has no separate "railroad to" command. Every railroad tile, whether a human or an automated worker builds it, goes through the same Iron, Coal and terrain check; a test now covers it.
- **Bombardment:** in a town of size 6 or less, walls from the Great Wall make it immune to land bombardment (the C3C bug, kept on purpose), and great wonders can never be bombarded away.
