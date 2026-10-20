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
