using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine.AI.UnitAI;
using C7GameData;
using Serilog;

namespace C7Engine.AI {
	// At peace, an AI with more soldiers than it wants (DevelopmentAI's
	// military cap: a defender and a half per city, more near rivals, an
	// escort per settler and a couple to spare) that also pays gold for
	// units over its free support disbands its obsolete ones (by Civ3's rule:
	// the civ can build what they upgrade to), weakest first and those in a
	// city first (where disbanding leaves the city some of their shields),
	// one every DisbandIntervalTurns turns while it is over the cap and its
	// free support. Units with a job
	// that matters now are kept: on a war campaign, escorting, aboard a ship,
	// a city's last defender, leaders and armies. (An AI heuristic, not a
	// Civ3 rule.) Disbanding them all at once cost the AI a third of its
	// army in AI trials, and some score; the owner chose to have it go
	// slowly instead, which cost nothing measurable.
	public static class ObsoleteUnits {
		private static readonly ILogger log = Log.ForContext(typeof(ObsoleteUnits));

		// How many cities' production options are looked at to tell which
		// units the civ can build: the largest few, which have the most
		// resources and buildings, and a coastal one for ships.
		private const int CitiesLookedAt = 4;

		// How often an AI may disband an obsolete unit.
		private const int DisbandIntervalTurns = 5;

		// The turn each AI last disbanded one.
		private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Player, System.Runtime.CompilerServices.StrongBox<int>> lastDisbanded = new();

		public static async Task DisbandOverSupport(Player player) {
			if (player.isHuman || player.isBarbarians || player.cities.Count == 0) {
				return;
			}
			if (PlayerRelationship.IsInAnyWar(player, EngineStorage.gameData.players)) {
				return;
			}
			int turn = EngineStorage.gameData.turn;
			System.Runtime.CompilerServices.StrongBox<int> last = lastDisbanded.GetValue(player, _ => new(int.MinValue / 2));
			if (turn - last.Value < DisbandIntervalTurns) {
				return;
			}
			(int totalUnits, int allowedUnits, int supportCost) = player.TotalUnitsAllowedUnitsAndSupportCost();
			(int soldiers, int wanted) = DevelopmentAI.MilitaryNeed(player);
			int over = System.Math.Min(totalUnits - allowedUnits, soldiers - wanted);
			if (over <= 0 || supportCost <= 0) {
				return;
			}

			HashSet<UnitPrototype> buildable = BuildableUnits(player);
			Dictionary<UnitPrototype, bool> obsolete = new();
			bool IsObsolete(UnitPrototype type) {
				if (!obsolete.TryGetValue(type, out bool result)) {
					result = UnitPrototype.UpgradeGraph.For(type).Candidates(type, player.civilization).Any(buildable.Contains);
					obsolete[type] = result;
				}
				return result;
			}

			List<MapUnit> candidates = player.units
				.Where(u => DevelopmentAI.IsMilitaryLandUnit(u.unitType) && MayGo(u) && IsObsolete(u.unitType))
				.OrderBy(u => u.location.cityAtTile?.owner == player ? 0 : 1)
				.ThenBy(u => u.unitType.attack + u.unitType.defense)
				.ToList();
			foreach (MapUnit unit in candidates) {
				// A city's last defender stays.
				if (IsLastDefender(unit)) {
					continue;
				}
				log.Information("{Player} disbands obsolete {Unit}, being {Over} units over its cap", player, unit, over);
				last.Value = turn;
				await unit.Disband();
				break;
			}
		}

		// Whether the unit is free of anything that needs it now.
		private static bool MayGo(MapUnit unit) {
			if (unit.hitPointsRemaining <= 0 || unit.IsLoaded() || unit.IsInArmy() || unit.PassengerCount() > 0 || unit.IsLeader()) {
				return false;
			}
			return unit.currentAI is not WarCampaign.MemberAI && unit.currentAI is not EscortAI && unit.currentAI is not FerryAI;
		}

		private static bool IsLastDefender(MapUnit unit) {
			City city = unit.location.cityAtTile;
			if (city == null || city.owner != unit.owner || !unit.CanDefendOnLand()) {
				return false;
			}
			foreach (MapUnit other in unit.location.unitsOnTile) {
				if (other != unit && other.owner == unit.owner && other.CanDefendOnLand()) {
					return false;
				}
			}
			return true;
		}

		// The units the civ can build in its largest cities and a coastal one.
		private static HashSet<UnitPrototype> BuildableUnits(Player player) {
			List<City> cities = player.cities.OrderByDescending(c => c.residents.Count).Take(CitiesLookedAt).ToList();
			City coastal = player.cities.Where(c => c.location.NeighborsWater()).OrderByDescending(c => c.residents.Count).FirstOrDefault();
			if (coastal != null && !cities.Contains(coastal)) {
				cities.Add(coastal);
			}
			HashSet<UnitPrototype> result = new();
			foreach (City city in cities) {
				foreach (IProducible option in city.ListProductionOptions(EngineStorage.gameData)) {
					if (option is UnitPrototype type) {
						result.Add(type);
					}
				}
			}
			return result;
		}
	}
}
