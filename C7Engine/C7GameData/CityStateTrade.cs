using System.Collections.Generic;
using System.Linq;
using C7Engine.Pathing;
using C7GameData;
using Serilog;

namespace C7Engine {
	// Kings & Nomads' trade with the independent city-states (owner's
	// design, 2026-10-23). A city-state builds no settlers (no barbarian
	// city builds anything), but a civ can trade with it by road: the
	// independents' land doesn't cut a civ's trade network, and a city-state
	// the network reaches shares every luxury and strategic resource inside
	// its borders with the civ, and pays it IncomePerCityState gold a turn
	// in trade. See TradeNetwork. Trade is worth more than conquest (owner's
	// design, 2026-10-24): each of the civ's cities on a road to a
	// city-state makes ShieldsPerCityState shields more for it, which
	// nothing wastes, and each turn a city-state may, by GiftPercent chance,
	// give every civ trading with it a unit.
	public static class CityStateTrade {
		private static readonly ILogger log = Log.ForContext(typeof(CityStateTrade));

		// HEURISTIC: the gold a turn each connected city-state brings in.
		public const int IncomePerCityState = 3;

		// HEURISTIC: the shields a turn each city-state a city trades with
		// adds to it.
		public const int ShieldsPerCityState = 1;

		// HEURISTIC: the chance, in percent, that a city-state gives its
		// trading partners a unit on a given turn.
		public const int GiftPercent = 10;

		public static bool IsOn(GameData gameData) {
			return gameData?.rules?.SettlersOnlyInConqueredCities == true && gameData.rules.FallenCitiesGoIndependent;
		}

		// Whether the city is an independent city-state.
		public static bool IsCityState(GameData gameData, City city) {
			return city?.owner?.isBarbarians == true && IsOn(gameData);
		}

		// The same, by the rules the city's owner plays by, for while a game
		// is loading.
		public static bool IsCityState(City city) {
			Rules rules = city?.owner?.rules;
			return city.owner.isBarbarians && rules != null && rules.SettlersOnlyInConqueredCities && rules.FallenCitiesGoIndependent;
		}

		// The gold a turn the player's trade with city-states brings in.
		public static int Income(GameData gameData, Player player) {
			if (!IsOn(gameData) || player.isBarbarians || player.cities.Count == 0) {
				return 0;
			}
			return gameData.GetTradeNetwork().ConnectedCityStates(player).Sum(c => IncomePerCityState
				+ CityStates.Specialists(c) * CityStates.SpecialistGold);
		}

		// The beakers a turn the city-states' specialists bring the player
		// (see CityStates).
		public static int Beakers(GameData gameData, Player player) {
			if (!IsOn(gameData) || player.isBarbarians || player.cities.Count == 0) {
				return 0;
			}
			return gameData.GetTradeNetwork().ConnectedCityStates(player).Sum(c => CityStates.Specialists(c) * CityStates.SpecialistBeakers);
		}

		// The shields the city's trade with city-states adds to it.
		public static int Shields(GameData gameData, City city) {
			if (!IsOn(gameData) || city.owner == null || city.owner.isBarbarians) {
				return 0;
			}
			return gameData.GetTradeNetwork().CityStatesTradingWith(city.owner, city).Count * ShieldsPerCityState;
		}

		// The civs trading with the city-state, each with its city on the
		// road nearest it.
		public static Dictionary<Player, City> Partners(GameData gameData, City cityState) {
			Dictionary<Player, City> result = new();
			TradeNetwork network = gameData.GetTradeNetwork();
			foreach (Player player in gameData.players.Where(p => !p.isBarbarians && !p.defeated && p.cities.Count > 0)) {
				City nearest = player.cities.Where(c => network.CityStatesTradingWith(player, c).Contains(cityState))
					.OrderBy(c => c.location.DistanceTo(cityState.location)).FirstOrDefault();
				if (nearest != null) {
					result[player] = nearest;
				}
			}
			return result;
		}

		// Called as each turn begins: each city-state, by chance, gives its
		// trading partners a unit, in their city nearest it.
		public static void GiveGifts(GameData gameData) {
			if (!IsOn(gameData) || gameData.turn <= 0) {
				return;
			}
			Player independents = gameData.players.Find(p => p.isBarbarians);
			if (independents == null || CityStates.Defender(gameData) is not UnitPrototype gift) {
				return;
			}
			foreach (City cityState in independents.cities.ToList()) {
				if (GameData.rng.Next(100) >= GiftPercent) {
					continue;
				}
				foreach ((Player player, City city) in Partners(gameData, cityState)) {
					gameData.SpawnUnit(player, gift, city.location);
					log.Information("{CityState} gives {Player} a {Unit} in {City}", cityState.name, player.civilization.name, gift.name, city.name);
					if (player.IsToldNews) {
						new MsgShowMilitaryAdvisorPopup(player, $"Our trading partner {cityState.name} has sent {("AEIOU".Contains(gift.name[0]) ? "an" : "a")} {gift.name} to {city.name} as a gift!", happy: true).send();
					}
				}
			}
		}
	}
}
