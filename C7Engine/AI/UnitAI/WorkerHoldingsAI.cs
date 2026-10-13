using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using C7GameData;

namespace C7Engine.AI.UnitAI {
	// Where an AI worker might found a colony or build an outpost, both of
	// which use the worker up (see MapUnit_Colony and Tile_Holdings for the
	// rules). When and where the AI does so is all HEURISTIC: Civ3's AI does
	// build both, but no source says when.
	//
	// - A colony goes on a luxury, or a strategic resource the civ can use,
	//   that its capital doesn't have, outside every civ's borders, a short
	//   walk from one of its cities so the worker can road it to the network
	//   on the way there. Only one worker is sent at a time, and only while
	//   the civ has at least ColonyMinWorkers workers and one per city.
	// - An outpost goes, rarely, on a hill or mountain in the civ's borders
	//   that looks over a rival civ's land, while the civ has workers to
	//   spare (OutpostWorkersPerCity per city), one at a time, and at most one
	//   per OutpostCitiesEach cities.
	internal static class WorkerHoldingsAI {
		// HEURISTIC limits that keep the AI from throwing away workers.
		internal const int ColonyMinWorkers = 3;
		internal const int ColonyMaxCityDistance = 4;
		internal const int OutpostMinWorkers = 4;
		internal const int OutpostWorkersPerCity = 2;
		internal const int OutpostCitiesEach = 4;
		// How close a rival's land must be for an outpost to watch it.
		internal const int OutpostRivalDistance = 2;
		// Outposts are kept at least this far apart.
		internal const int OutpostSpacing = 4;

		// The turn each player last looked for a site and found none, so the
		// search isn't repeated for every worker that turn.
		private sealed class LastSearch {
			public int colonyTurn = -1;
			public int outpostTurn = -1;
		}
		private static readonly ConditionalWeakTable<Player, LastSearch> lastSearch = new();

		private static int WorkerCount(Player player) {
			return player.units.Count(u => u.unitType.isWorker && !u.IsCaptive());
		}

		// The sites for a colony, nearest the unit first, or none.
		internal static List<Tile> ColonySites(MapUnit unit, Player player, bool tripUnderway) {
			if (player.isHuman || player.isBarbarians || tripUnderway || !unit.unitType.actions.Contains(UnitAction.BuildColony)) {
				return [];
			}
			int workers = WorkerCount(player);
			if (workers < ColonyMinWorkers || workers < player.cities.Count) {
				return [];
			}
			GameData gameData = EngineStorage.gameData;
			LastSearch last = lastSearch.GetOrCreateValue(player);
			if (last.colonyTurn == gameData.turn) {
				return [];
			}

			Dictionary<Resource, int> have = TradeReport.CapitalResources(gameData, player);
			HashSet<Tile> seen = new();
			List<Tile> sites = new();
			foreach (City city in player.cities) {
				if (city.location.continent != unit.location.continent) {
					continue;
				}
				foreach (Tile t in city.location.GetTilesWithinRankDistance(ColonyMaxCityDistance)) {
					if (!seen.Add(t) || !t.IsLand() || t.Resource == null || t.Resource == Resource.NONE || have.ContainsKey(t.Resource)) {
						continue;
					}
					if (!player.tileKnowledge.knownTiles.Contains(t) || !t.CanTakeColony(player) || !Wanted(gameData, player, t.Resource)
						|| IsThreatened(t, player)) {
						continue;
					}
					sites.Add(t);
				}
			}
			if (sites.Count == 0) {
				last.colonyTurn = gameData.turn;
			}
			return sites.OrderBy(t => t.DistanceTo(unit.location)).ToList();
		}

		// Whether the civ has a use for the resource: any luxury, or a
		// strategic resource a unit it can build needs.
		private static bool Wanted(GameData gameData, Player player, Resource r) {
			if (r.Category == ResourceCategory.LUXURY) {
				return true;
			}
			return r.Category == ResourceCategory.STRATEGIC
				&& gameData.unitPrototypes.Any(u => u.requiredResources.Contains(r) && player.HasRequiredTechnology(u));
		}

		// Whether another civ's (or the barbarians') units are on or next to
		// the tile.
		private static bool IsThreatened(Tile t, Player player) {
			if (t.unitsOnTile.Any(u => u.owner != player)) {
				return true;
			}
			foreach (Tile n in t.neighbors.Values) {
				if (n != Tile.NONE && n.unitsOnTile.Any(u => u.owner != player)) {
					return true;
				}
			}
			return false;
		}

		// The terraform that builds an outpost, or null.
		internal static Terraform OutpostTerraform(MapUnit unit) {
			return unit.unitType.terraformActions.FirstOrDefault(t => t.Improvement?.key == Tile.TileOverlays.OUTPOST);
		}

		// The sites for an outpost, nearest the unit first, or none.
		internal static List<Tile> OutpostSites(MapUnit unit, Player player, bool tripUnderway) {
			if (player.isHuman || player.isBarbarians || tripUnderway) {
				return [];
			}
			Terraform outpost = OutpostTerraform(unit);
			if (outpost == null) {
				return [];
			}
			int workers = WorkerCount(player);
			if (workers < OutpostMinWorkers || workers < OutpostWorkersPerCity * player.cities.Count) {
				return [];
			}
			GameData gameData = EngineStorage.gameData;
			LastSearch last = lastSearch.GetOrCreateValue(player);
			if (last.outpostTurn == gameData.turn) {
				return [];
			}

			HashSet<Tile> seen = new();
			List<Tile> ours = new();
			foreach (City city in player.cities) {
				foreach (Tile t in city.GetTilesWithinBorders()) {
					if (seen.Add(t)) {
						ours.Add(t);
					}
				}
			}
			List<Tile> outposts = ours.Where(t => t.OutpostOwner() == player).ToList();
			if (outposts.Count >= System.Math.Max(1, player.cities.Count / OutpostCitiesEach)) {
				last.outpostTurn = gameData.turn;
				return [];
			}

			List<Tile> sites = new();
			foreach (Tile t in ours) {
				if (t.overlayTerrainType.height < 2 || t.continent != unit.location.continent || t.HasCity()
					|| outposts.Any(o => o.DistanceTo(t) < OutpostSpacing)
					|| !FacesRival(t, player) || IsThreatened(t, player) || !unit.CanPerformTerraformAction(outpost, t)) {
					continue;
				}
				sites.Add(t);
			}
			if (sites.Count == 0) {
				last.outpostTurn = gameData.turn;
			}
			return sites.OrderBy(t => t.DistanceTo(unit.location)).ToList();
		}

		// Whether land of another (non-barbarian) civ is near the tile.
		private static bool FacesRival(Tile t, Player player) {
			foreach (Tile n in t.GetTilesWithinRankDistance(OutpostRivalDistance)) {
				Player owner = n.OwningPlayer();
				if (owner != null && owner != player && !owner.isBarbarians) {
					return true;
				}
			}
			return false;
		}
	}
}
