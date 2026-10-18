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
| Tribute demands, the trespass war and joining an alliance all require a war the AI would win: it can reach the enemy, it has no other war, and it is 1.5× the enemy plus that enemy's mutual protection partners. | `PeaceAI.WouldWinWarWith`, `TributeDemands`, `TerritoryDemands`, `PactAI` | roughly 0 (aimed at humans) |
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
