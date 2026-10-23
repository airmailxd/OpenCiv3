using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using C7GameData;

namespace C7Engine.AI {
	// How the AI bids in Eternal War's Mercenary Market (see Staging) and
	// where it puts what it wins. The market isn't Civ3's, so nothing here
	// is Civ3's AI; every rule is a judgement, marked HEURISTIC.
	//
	// The AI works out what each unit on offer is worth to it in gold: the
	// shields it would take to build as much fighting strength of that kind
	// itself, at what gold buys shields when hurrying, scaled by how badly
	// it needs that kind (defenders for cities short of them or under
	// threat, attackers for a civ already safe at home, ships hardly at all
	// on Eternal War's one landmass), less the upkeep the unit would cost.
	// It bids a little above what it reckons a rival would pay, never more
	// than the unit is worth to it nor more than it reckons the richest
	// rival could bid, and keeps a reserve. It learns the going price from its own wins
	// and losses, since the market tells no one the bids.
	public static class MarketAI {
		// What the AI remembers of the market: the gold per shield it
		// reckons wins a unit.
		private sealed class Memory {
			public double pricePerShield = InitialPricePerShield;
		}

		private static readonly ConditionalWeakTable<Player, Memory> memories = new();

		private static Memory MemoryOf(Player player) => memories.GetOrCreateValue(player);

		// The gold per shield the player reckons wins a unit now.
		internal static double PricePerShield(Player player) => MemoryOf(player).pricePerShield;

		// HEURISTIC: the going price the AI first expects, in gold per
		// shield of the unit's cost: about the most a civ bidding without
		// thought would offer.
		private const double InitialPricePerShield = 1.5;
		// HEURISTIC: the going price never drops below this, so the AI
		// doesn't come to think units free and stop competing.
		private const double MinPricePerShield = 0.25;

		// HEURISTIC: a civ keeps this much gold per city out of the market,
		// and ten turns of any deficit (as hurrying does, see
		// DevelopmentAI.HurryProduction), counting the upkeep of what it buys.
		private const int ReservePerCity = 2;
		private const int ReserveDeficitTurns = 10;

		// HEURISTIC: a unit past the free support is charged this many turns
		// of its upkeep against its worth.
		private const int UpkeepTurns = 20;

		// HEURISTIC: how much above the going price the AI bids, at least
		// and at most (chosen at random between), so that it wins against a
		// rival bidding about the going price without paying much more.
		private const double MinMargin = 0.05;
		private const double MaxMargin = 0.25;

		// HEURISTIC: a unit worth this many times the going price or more is
		// worth contesting: one time in ContestOneIn the AI bids well over
		// the going price for it, in case a rival wants it as badly.
		private const double ContestWorth = 2;
		private const int ContestOneIn = 3;
		private const double ContestMargin = 0.6;

		// HEURISTIC: the defenders a city wants: two, and two more while
		// enemy attackers are within ThreatDistance tiles of it.
		private const int DefendersWanted = 2;
		private const int ThreatenedExtra = 2;
		private const int ThreatDistance = 3;

		// What the AI knows of its own position when it bids, worked out
		// once a turn.
		internal sealed class Position {
			public Player player;
			// Defenders short of what the cities want, all together, and
			// whether any city short of them is threatened.
			public int defenderShortfall;
			public bool threatenedShort;
			// The civ's attackers, and its cities.
			public int attackers;
			public int cities;
			// Whether it has a city on the coast.
			public bool coastal;
			// The best attacker and defender it can build, by strength (see
			// Strength), and what they cost it.
			public double bestAttack, bestDefense;
			public int bestAttackCost, bestDefenseCost;
			// Whether a war campaign wants more attackers.
			public bool campaignWantsAttackers;
			// Units over the free support, and the gold each costs a turn.
			public int unitsOverSupport;
			public int unitCost;
			public int goldPerTurn;
		}

		private enum Role { Attacker, Defender, Ship }

		private static Role RoleOf(UnitPrototype unit) {
			if (unit.IsSeaUnit()) {
				return Role.Ship;
			}
			return unit.defense >= unit.attack ? Role.Defender : Role.Attacker;
		}

		// HEURISTIC: a unit's fighting strength in its role. An attacker's is
		// its attack, more for each move beyond the first (it reaches the
		// enemy sooner and picks its fights) and a little for its defence; a
		// defender's is its defence and a little for its attack. Extra hit
		// points count in proportion to a regular's three.
		private static double Strength(UnitPrototype unit, Role role) {
			double hp = (3.0 + Math.Max(0, unit.hpBonus)) / 3.0;
			int moves = Math.Max(1, unit.movement);
			double s = role == Role.Attacker
				? unit.attack * (1 + 0.25 * (moves - 1)) + 0.25 * unit.defense
				: unit.defense + 0.25 * unit.attack;
			return s * hp;
		}

		internal static Position Assess(GameData gameData, Player player) {
			AIFogOfWar.Refresh(player);
			Position p = new() { player = player, cities = player.cities.Count };
			foreach (City city in player.cities) {
				int defenders = city.location.unitsOnTile.Count(u => u.owner == player && u.IsLandUnit()
					&& u.unitType.defense >= u.unitType.attack && u.unitType.defense > 0 && !u.unitType.isWorker);
				bool threatened = Threat(player, city) > 0;
				int wanted = DefendersWanted + (threatened ? ThreatenedExtra : 0);
				if (defenders < wanted) {
					p.defenderShortfall += wanted - defenders;
					p.threatenedShort |= threatened;
				}
				p.coastal |= city.location.NeighborsOcean();
			}
			p.attackers = player.units.Count(u => u.IsLandUnit() && u.unitType.attack > u.unitType.defense && !u.unitType.isWorker && !u.IsArmy());

			// What the civ can build now, as its capital lists it (so with
			// the resources it has).
			City home = player.cities.FirstOrDefault(c => c.IsCapital()) ?? player.cities.FirstOrDefault();
			if (home != null) {
				foreach (UnitPrototype u in home.ListProductionOptions(gameData).OfType<UnitPrototype>()) {
					if (!u.IsLandUnit() || u.isWorker || u.isSettler || u.isArmy || u.isLeader || u.isKing) {
						continue;
					}
					double attack = Strength(u, Role.Attacker), defense = Strength(u, Role.Defender);
					if (u.attack > u.defense && attack > p.bestAttack) {
						p.bestAttack = attack;
						p.bestAttackCost = player.ShieldCost(u);
					}
					if (u.defense >= u.attack && u.defense > 0 && defense > p.bestDefense) {
						p.bestDefense = defense;
						p.bestDefenseCost = player.ShieldCost(u);
					}
				}
			}
			p.campaignWantsAttackers = WarCampaign.WantsAttackers(player);
			(int total, int allowed, _) = player.TotalUnitsAllowedUnitsAndSupportCostRaw();
			p.unitsOverSupport = total - allowed;
			p.unitCost = player.government?.allUnitsFree == true ? 0 : Math.Max(0, player.government?.unitCost ?? 0);
			p.goldPerTurn = player.CalculateGoldPerTurn();
			return p;
		}

		// The enemy land units that could attack, within ThreatDistance of
		// the city, that the player can see.
		internal static int Threat(Player player, City city) {
			int count = 0;
			foreach (Tile t in city.location.GetTilesWithinRankDistance(ThreatDistance)) {
				if (!AIFogOfWar.SeesUnitsOn(player, t)) {
					continue;
				}
				foreach (MapUnit u in t.unitsOnTile) {
					if (u.owner != player && u.IsLandUnit() && u.unitType.attack > 0 && AIThreat.IsHostile(player, u.owner)) {
						++count;
					}
				}
			}
			return count;
		}

		// HEURISTIC: how much the civ wants a unit of the role now, as a
		// factor on its worth. Defenders matter most while cities are short
		// of them, and most of all a threatened one; once every city has its
		// defenders, a few more still help hold what is taken. Attackers are
		// what win Eternal War, wanted the more when a campaign is short of
		// them, and less while the cities are bare. Ships do little on one
		// landmass, and nothing for a civ with no coast.
		private static double Need(Position p, Role role, UnitPrototype unit) {
			switch (role) {
				case Role.Defender:
					if (p.defenderShortfall <= 0) {
						return 0.3;
					}
					return p.threatenedShort ? 1.5 : 1.2;
				case Role.Attacker:
					double need = 1.2;
					if (p.campaignWantsAttackers) {
						need += 0.25;
					}
					if (p.defenderShortfall > p.cities) {
						need -= 0.3;
					}
					// A civ with many attackers for its cities already has
					// a strong offence; one more adds less.
					if (p.attackers > 4 * Math.Max(1, p.cities)) {
						need -= 0.2;
					}
					return need;
				default:
					if (!p.coastal) {
						return 0.1;
					}
					return unit.attack > 0 ? 0.35 : 0.15;
			}
		}

		// What the unit is worth to the civ in gold, before what it has
		// already planned to buy this turn. HEURISTIC: the shields of the
		// civ's own best unit of its kind, scaled by how strong this one is
		// beside it (a weaker one counts for much less, as it loses its
		// fights), at the gold a shield costs when hurrying, times the need,
		// less UpkeepTurns of upkeep if the civ is past its free support.
		internal static int Worth(GameData gameData, Position p, UnitPrototype unit) {
			Role role = RoleOf(unit);
			int goldPerShield = Math.Max(1, gameData.rules.ShieldValueInGold);
			double strength = Strength(unit, role);
			double best = role == Role.Attacker ? p.bestAttack : role == Role.Defender ? p.bestDefense : 0;
			int bestCost = role == Role.Attacker ? p.bestAttackCost : p.bestDefenseCost;
			double shields;
			if (role == Role.Ship || best <= 0 || bestCost <= 0) {
				shields = p.player.ShieldCost(unit);
			} else {
				double ratio = strength / best;
				shields = bestCost * (ratio < 1 ? ratio * ratio : ratio);
			}
			double worth = shields * goldPerShield * Need(p, role, unit);
			if (p.unitsOverSupport >= 0 && unit.requiresSupport) {
				worth -= UpkeepTurns * p.unitCost;
			}
			return Math.Max(0, (int)worth);
		}

		// The gold the civ keeps back from the market, given the upkeep it
		// would add a turn.
		internal static int Reserve(Position p, int addedUpkeep) {
			int deficit = Math.Max(0, addedUpkeep - p.goldPerTurn);
			return Math.Max(ReservePerCity * p.cities, ReserveDeficitTurns * deficit);
		}

		// The AI's bids, one for each offer.
		public static List<int> Bids(GameData gameData, Player player, List<UnitPrototype> offers) {
			List<int> bids = offers.Select(_ => 0).ToList();
			if (player.cities.Count == 0 || player.gold <= 0) {
				return bids;
			}
			Position p = Assess(gameData, player);
			Memory memory = MemoryOf(player);
			// No rival can bid more than it has, so there's no use bidding
			// far above what the richest rival could. The AI can't see the
			// rivals' treasuries (owner's ruling, 2026-10-23), so it guesses
			// from what it can see, their cities.
			int richestRival = gameData.players
				.Where(o => o != player && !o.isBarbarians && !o.defeated && o.cities.Count > 0)
				.Select(o => EstimatedGold(player, o)).DefaultIfEmpty(0).Max();

			int spent = 0, addedUpkeep = 0;
			HashSet<int> planned = new();
			// Plan one unit at a time, the best bargain first, and take each
			// planned unit into account before the next: a second defender
			// is wanted less than the first.
			while (planned.Count < offers.Count) {
				int bestIndex = -1, bestBid = 0;
				double bestSurplus = 0;
				for (int i = 0; i < offers.Count; ++i) {
					if (planned.Contains(i)) {
						continue;
					}
					UnitPrototype unit = offers[i];
					int worth = Worth(gameData, p, unit);
					int upkeep = UpkeepOf(p, unit);
					// A bid short of the going price may still win, the
					// rivals being short of gold too, and a lost bid costs
					// nothing, so the civ bids what it can spare.
					int bid = Math.Min(BidFor(memory, unit, worth, richestRival), player.gold - spent - Reserve(p, addedUpkeep + upkeep));
					if (bid <= 0) {
						continue;
					}
					// What the civ can expect to gain for its gold, as a
					// share of the bid. HEURISTIC: a bid below the going
					// price wins with the square of its share of it.
					double share = Math.Min(1, bid / Math.Max(1, memory.pricePerShield * Math.Max(1, unit.shieldCost)));
					double surplus = share * share * (worth - bid) / bid;
					if (bestIndex < 0 || surplus > bestSurplus) {
						bestIndex = i;
						bestBid = bid;
						bestSurplus = surplus;
					}
				}
				if (bestIndex < 0) {
					break;
				}
				planned.Add(bestIndex);
				bids[bestIndex] = bestBid;
				spent += bestBid;
				UnitPrototype chosen = offers[bestIndex];
				addedUpkeep += UpkeepOf(p, chosen);
				if (chosen.requiresSupport) {
					++p.unitsOverSupport;
				}
				switch (RoleOf(chosen)) {
					case Role.Defender:
						p.defenderShortfall = Math.Max(0, p.defenderShortfall - 1);
						if (p.defenderShortfall == 0) {
							p.threatenedShort = false;
						}
						break;
					case Role.Attacker:
						++p.attackers;
						break;
				}
			}
			return bids;
		}

		private static int UpkeepOf(Position p, UnitPrototype unit) {
			return unit.requiresSupport && p.unitsOverSupport >= 0 ? p.unitCost : 0;
		}

		// HEURISTIC: a little above the going price (see MinMargin), or for
		// a unit worth much more than that, now and then well above it (see
		// ContestWorth); never above the unit's worth, nor more than one over
		// the richest rival's treasury, which can't be outbid.
		// HEURISTIC: a rival's treasury, guessed as the AI's own gold per
		// city times the rival's cities, and half again for safety.
		public static int EstimatedGold(Player player, Player rival) {
			double perCity = player.gold / (double)Math.Max(1, player.cities.Count);
			return (int)Math.Ceiling(perCity * rival.cities.Count * 1.5);
		}

		private static int BidFor(Memory memory, UnitPrototype unit, int worth, int richestRival) {
			int cost = Math.Max(1, unit.shieldCost);
			double going = memory.pricePerShield * cost;
			double margin = MinMargin + GameData.rng.NextDouble() * (MaxMargin - MinMargin);
			if (worth >= ContestWorth * going && GameData.rng.Next(ContestOneIn) == 0) {
				margin += ContestMargin;
			}
			int bid = (int)Math.Ceiling(going * (1 + margin));
			bid = Math.Min(bid, richestRival + 1);
			return Math.Max(1, Math.Min(bid, worth));
		}

		// HEURISTIC: what the AI learns when the market closes. A unit it
		// won went for its bid, perhaps more than it needed to pay, so it
		// expects to pay a little less next time; a unit it lost went for
		// more than its bid, so it expects to pay more; a unit no one bought
		// tells it prices are low. Units it didn't bid on that someone else
		// won tell it nothing it can use, as no one is told the price.
		public static void Learn(GameData gameData, StagingState state, List<MarketResult> results) {
			foreach (Player player in gameData.players.Where(p => !p.isHuman && !p.isBarbarians)) {
				if (!state.bids.TryGetValue(player.id.ToString(), out List<int> bids)) {
					continue;
				}
				Memory memory = MemoryOf(player);
				for (int i = 0; i < results.Count; ++i) {
					UnitPrototype unit = gameData.unitPrototypes.Find(u => u.name == results[i].unitType);
					int cost = Math.Max(1, unit?.shieldCost ?? 1);
					int bid = i < bids.Count ? bids[i] : 0;
					double perShield = bid / (double)cost;
					if (bid > 0 && results[i].winners.Contains(player)) {
						memory.pricePerShield = 0.8 * memory.pricePerShield + 0.2 * perShield * 0.85;
					} else if (bid > 0) {
						memory.pricePerShield = Math.Max(memory.pricePerShield, 0.6 * memory.pricePerShield + 0.4 * perShield * 1.3);
					} else if (results[i].winners.Count == 0) {
						memory.pricePerShield *= 0.9;
					}
					memory.pricePerShield = Math.Max(MinPricePerShield, memory.pricePerShield);
				}
			}
		}

		// Where the AI puts a unit it won. HEURISTIC: a defender goes to the
		// city that most needs one: the most enemy attackers near it for its
		// defenders, then the fewest defenders, then nearest the enemy. An
		// attacker joins the war campaign's gathering city if there is one,
		// or else goes to the city nearest the weakest enemy city near us (by
		// distance and the defenders we can see there), to strike from. A
		// ship goes to the coastal city nearest an enemy city.
		public static City Choice(GameData gameData, Player player, UnitPrototype unit, List<City> choices) {
			AIFogOfWar.Refresh(player);
			List<City> enemyCities = gameData.cities
				.Where(c => c.owner != player && PlayerRelationship.AtWar(player, c.owner)).ToList();
			int ToEnemy(City city) => enemyCities.Count == 0 ? 0 : enemyCities.Min(e => city.location.DistanceTo(e.location));
			int Defenders(City city) => city.location.unitsOnTile.Count(u => u.owner == player && u.IsLandUnit()
				&& u.unitType.defense >= u.unitType.attack && u.unitType.defense > 0 && !u.unitType.isWorker);
			Role role = RoleOf(unit);
			if (role == Role.Ship) {
				List<City> coastal = choices.Where(c => c.location.NeighborsOcean()).ToList();
				return (coastal.Count > 0 ? coastal : choices).OrderBy(ToEnemy).First();
			}
			if (role == Role.Defender) {
				// The enemies near it for each defender, and one more for a
				// city short of the defenders it wants anyway.
				double Danger(City c) => (Threat(player, c) + 1.0) / (Defenders(c) + 1.0) + (Defenders(c) < DefendersWanted ? 1 : 0);
				return choices
					.OrderByDescending(Danger)
					.ThenBy(Defenders)
					.ThenBy(ToEnemy)
					.First();
			}
			if (WarCampaign.CampaignStagingCity(player) is City staging && choices.Contains(staging)) {
				return staging;
			}
			// HEURISTIC: a seen defender counts as three tiles of distance.
			double TargetScore(City ours, City theirs) {
				int defenders = AIFogOfWar.SeesUnitsOn(player, theirs.location)
					? theirs.location.unitsOnTile.Count(u => u.CanDefendOnLand()) : 2;
				return ours.location.DistanceTo(theirs.location) + 3 * defenders;
			}
			if (enemyCities.Count == 0) {
				return choices.OrderBy(Defenders).First();
			}
			return choices
				.OrderBy(c => enemyCities.Min(e => TargetScore(c, e)))
				.ThenByDescending(Defenders)
				.First();
		}
	}
}
