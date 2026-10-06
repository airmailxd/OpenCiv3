using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine.Pathing;

namespace C7GameData {
	// What the cultural advisor reports, worked out from the game data.
	public static class CultureReport {
		public static int TotalCulture(Player player) => player.cities.Sum(c => c.GetCulture());

		public static int CulturePerTurn(Player player) => player.cities.Sum(c => c.GetCulturePerTurn());

		// The name of the culture level a civ with this much culture has
		// reached. Each level takes ten times the culture of the one below.
		public static string CultureLevel(Rules rules, int totalCulture) {
			List<string> names = rules.CultureLevelNames;
			if (names == null || names.Count == 0) {
				return "";
			}
			int level = (int)Math.Floor(Math.Log10(Math.Max(1, totalCulture)));
			return names[Math.Clamp(level, 0, names.Count - 1)];
		}

		// How a civ with theirCulture regards a civ with ourCulture, as in
		// "The Romans are <opinion> our culture."
		public static string OpinionOf(Rules rules, int theirCulture, int ourCulture) {
			List<CultureOpinion> opinions = rules.CultureOpinions;
			if (opinions == null || opinions.Count == 0) {
				return "";
			}
			foreach (CultureOpinion opinion in opinions) {
				// ours / theirs >= numerator / denominator, without dividing.
				if ((long)ourCulture * opinion.Denominator >= (long)theirCulture * opinion.Numerator) {
					return opinion.Name;
				}
			}
			return opinions[^1].Name;
		}

		// "a" or "an", for the culture level sentence.
		public static string Article(string word) =>
			word.Length > 0 && "AEIOUaeiou".Contains(word[0]) ? "an" : "a";
	}

	public enum TradeStatus {
		TradingWith,
		CanTradeWith,
		CannotTradeWith,
	}

	// A resource another civ sends to us, or that we send to them.
	public record ResourceDeal(Resource resource, Player partner, int turnsRemaining);

	// What the trade advisor reports, worked out from the game data.
	public static class TradeReport {
		// The luxuries and strategic resources the cities on the capital's
		// network can use, with how many of each there are.
		public static Dictionary<Resource, int> LocalResources(GameData gameData, Player player) {
			Dictionary<Resource, int> result = new();
			List<List<City>> groups = gameData.GetTradeNetwork().CityGroups(player);
			if (groups.Count == 0) {
				return result;
			}
			foreach ((Resource r, int count) in gameData.GetTradeNetwork().GetResourcesAvailableToCity(player, groups[0][0])) {
				if (r.Category is ResourceCategory.LUXURY or ResourceCategory.STRATEGIC) {
					result[r] = count;
				}
			}
			return result;
		}

		// The resources the player gets from (inbound) or gives to (outbound)
		// other civs through deals.
		public static List<ResourceDeal> ResourceDeals(GameData gameData, Player player, DealDetails direction) {
			List<ResourceDeal> result = new();
			foreach ((ID partnerId, PlayerRelationship relationship) in player.playerRelationships) {
				Player partner = gameData.GetPlayer(partnerId);
				if (partner == null) {
					continue;
				}
				foreach (MultiTurnDeal deal in relationship.multiTurnDeals) {
					if (deal.dealDetails != direction || deal.resourcePerTurn == null) {
						continue;
					}
					if (deal.dealSubType is not (DealSubType.ResourcePerTurn or DealSubType.LuxuryPerTurn)) {
						continue;
					}
					Resource resource = gameData.Resources.Find(r => r.Key == deal.resourcePerTurn);
					if (resource != null) {
						result.Add(new ResourceDeal(resource, partner, deal.TurnsRemaining(gameData.turn)));
					}
				}
			}
			return result;
		}

		// The luxuries and strategic resources a civ has to spare: it has at
		// least two on its capital's network, and at least one of them isn't
		// already going to another civ.
		public static List<(Resource resource, int count)> ExcessResources(GameData gameData, Player civ) =>
			ExcessResources(LocalResources(gameData, civ), ResourceDeals(gameData, civ, DealDetails.Outbound));

		// The same, given what the civ has and what it sends to other civs.
		public static List<(Resource resource, int count)> ExcessResources(Dictionary<Resource, int> available,
				List<ResourceDeal> outbound) {
			Dictionary<Resource, int> exported = new();
			foreach (ResourceDeal deal in outbound) {
				exported[deal.resource] = exported.GetValueOrDefault(deal.resource) + 1;
			}
			return available
				.Where(r => r.Value >= 2 && exported.GetValueOrDefault(r.Key) < r.Value)
				.OrderBy(r => r.Key.Category).ThenBy(r => r.Key.Name)
				.Select(r => (r.Key, r.Value))
				.ToList();
		}

		// Whether the player has a trade embargo against the target.
		public static bool HasEmbargoAgainst(Player player, Player target) {
			foreach (PlayerRelationship relationship in player.playerRelationships.Values) {
				foreach (MultiTurnDeal deal in relationship.multiTurnDeals) {
					if (deal.dealSubType == DealSubType.TradeEmbargo && deal.againstPlayer == target.id) {
						return true;
					}
				}
			}
			return false;
		}

		// Whether the player has any ongoing trade (resources or gold) with
		// the partner.
		public static bool IsTradingWith(Player player, Player partner) {
			if (!player.playerRelationships.TryGetValue(partner.id, out PlayerRelationship relationship)) {
				return false;
			}
			return relationship.multiTurnDeals.Any(d => d.dealSubType is DealSubType.ResourcePerTurn
				or DealSubType.LuxuryPerTurn or DealSubType.GoldPerTurn);
		}

		public static TradeStatus StatusWith(Player player, Player partner) {
			if (PlayerRelationship.AtWar(player, partner)
				|| HasEmbargoAgainst(player, partner) || HasEmbargoAgainst(partner, player)) {
				return TradeStatus.CannotTradeWith;
			}
			return IsTradingWith(player, partner) ? TradeStatus.TradingWith : TradeStatus.CanTradeWith;
		}
	}

	// What the military advisor reports, worked out from the game data.
	public static class MilitaryReport {
		// Captured workers, which work at half speed, so the advisor lists
		// them apart from the workers we trained ourselves.
		public static bool IsSlave(MapUnit unit) => unit.unitType.isWorker && unit.IsCaptive();

		// The name a unit is listed under: its type, or "Slave <type>".
		public static string GroupName(UnitPrototype type, bool slaves) => slaves ? $"Slave {type.name}" : type.name;

		// The player's units grouped by type, in the order of the game's unit
		// types, with slave workers grouped apart from, and after, their type.
		public static List<(UnitPrototype type, bool slaves, List<MapUnit> units)> UnitsByType(GameData gameData, Player player) {
			Dictionary<(UnitPrototype, bool), List<MapUnit>> groups = new();
			foreach (MapUnit unit in player.units) {
				(UnitPrototype, bool) key = (unit.unitType, IsSlave(unit));
				if (!groups.TryGetValue(key, out List<MapUnit> list)) {
					list = new();
					groups[key] = list;
				}
				list.Add(unit);
			}
			List<UnitPrototype> order = gameData.unitPrototypes;
			return groups
				.OrderBy(g => order.IndexOf(g.Key.Item1) is int i && i >= 0 ? i : int.MaxValue)
				.ThenBy(g => g.Key.Item2)
				.Select(g => (g.Key.Item1, g.Key.Item2, g.Value))
				.ToList();
		}

		// The player's units grouped by the city they're in, in the order of
		// the player's cities. Units outside the player's cities are under a
		// null city, last.
		public static List<(City city, List<MapUnit> units)> UnitsByCity(Player player) {
			Dictionary<City, List<MapUnit>> inCities = new();
			List<MapUnit> inTheField = new();
			foreach (MapUnit unit in player.units) {
				City city = unit.location?.cityAtTile;
				if (city != null && city.owner == player) {
					if (!inCities.TryGetValue(city, out List<MapUnit> list)) {
						list = new();
						inCities[city] = list;
					}
					list.Add(unit);
				} else {
					inTheField.Add(unit);
				}
			}
			List<(City, List<MapUnit>)> result = new();
			foreach (City city in player.cities) {
				if (inCities.TryGetValue(city, out List<MapUnit> list)) {
					result.Add((city, list));
				}
			}
			if (inTheField.Count > 0) {
				result.Add((null, inTheField));
			}
			return result;
		}

		// The cities building units, with the unit each is building.
		public static List<(City city, UnitPrototype unit)> UnitsInProduction(Player player) {
			List<(City, UnitPrototype)> result = new();
			foreach (City city in player.cities) {
				if (city.itemBeingProduced is UnitPrototype unit) {
					result.Add((city, unit));
				}
			}
			return result;
		}
	}
}
