using System.Linq;
using C7GameData;

namespace C7Engine {
	// Kings & Nomads' trade with the independent city-states (owner's
	// design, 2026-10-23). A city-state builds no settlers (no barbarian
	// city builds anything), but a civ can trade with it by road: the
	// independents' land doesn't cut a civ's trade network, and a city-state
	// the network reaches shares every luxury and strategic resource inside
	// its borders with the civ, and pays it IncomePerCityState gold a turn
	// in trade. See TradeNetwork.
	public static class CityStateTrade {
		// HEURISTIC: the gold a turn each connected city-state brings in.
		public const int IncomePerCityState = 3;

		public static bool IsOn(GameData gameData) {
			return gameData?.rules?.SettlersOnlyInConqueredCities == true && gameData.rules.FallenCitiesGoIndependent;
		}

		// Whether the city is an independent city-state.
		public static bool IsCityState(GameData gameData, City city) {
			return city?.owner?.isBarbarians == true && IsOn(gameData);
		}

		// The gold a turn the player's trade with city-states brings in.
		public static int Income(GameData gameData, Player player) {
			if (!IsOn(gameData) || player.isBarbarians || player.cities.Count == 0) {
				return 0;
			}
			return gameData.GetTradeNetwork().ConnectedCityStates(player).Count * IncomePerCityState;
		}
	}
}
