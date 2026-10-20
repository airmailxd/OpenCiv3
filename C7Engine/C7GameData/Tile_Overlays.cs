using System.Collections.Generic;
using System.Linq;
using C7Engine;
using Serilog;
using static C7GameData.TerrainImprovement;
using static C7GameData.Tile.TileOverlays;

namespace C7GameData;

public partial class Tile {
	public static void TryAddRoad(Tile tile, bool hasRoad, bool hasRailroad) {
		var shouldConnectToToNetwork = !hasRailroad && AnyNeighbor(tile, static t => t.HasRoad());
		if (shouldConnectToToNetwork || hasRoad) {
			var roadTerraform = ToTerraform(ROAD);
			if (roadTerraform != null && tile.cityAtTile.owner.HasTech(roadTerraform.RequiredTech)
				&& TileOverlays.TerrainAllows(tile.overlayTerrainType, roadTerraform.Improvement)) {
				tile.overlays.Add(roadTerraform.Improvement);
			}
		}
	}
	public static void TryAddRailroad(Tile tile, bool hasRailroad) {
		var shouldConnectToToNetwork = !hasRailroad && AnyNeighbor(tile, static t => t.HasRailroad());
		if (shouldConnectToToNetwork || hasRailroad) {
			var railroadTerraform = ToTerraform(RAILROAD);
			if (railroadTerraform != null && tile.cityAtTile.owner.HasTech(railroadTerraform.RequiredTech)
				&& TileOverlays.TerrainAllows(tile.overlayTerrainType, railroadTerraform.Improvement)) {
				tile.overlays.Add(railroadTerraform.Improvement);
			}
		}
	}
	public static void TryAddRuins(Tile tile) {
		var ruins = FindTerrainImprovement(RUINS);
		if (ruins != null)
			tile.overlays.Add(ruins);
	}
	// Clears the rubble of a destroyed city, if there is any.
	public static void TryRemoveRuins(Tile tile) {
		var ruins = FindTerrainImprovement(RUINS);
		if (ruins != null && tile.overlays.HasImprovement(ruins))
			tile.overlays.Remove(ruins);
	}
	public static void TryAddCraters(Tile tile) {
		var craters = FindTerrainImprovement(CRATERS);
		if (craters != null)
			tile.overlays.Add(craters);
	}

	// Pollution, as from a city (see City.MaybePollute). Returns whether it
	// was added.
	public static bool TryAddPollution(Tile tile) {
		var pollution = FindTerrainImprovement(POLLUTION);
		if (pollution == null)
			return false;
		tile.overlays.Add(pollution);
		TileChangeJournal.Record(tile);
		return true;
	}

	// Nuclear fallout, left by nuclear detonations. Returns whether it was added.
	public static bool TryAddFallout(Tile tile) {
		var fallout = FindTerrainImprovement(FALLOUT);
		if (fallout == null)
			return false;
		tile.overlays.Add(fallout);
		return true;
	}

	// Whether workers have pollution, fallout or craters to clean up here:
	// "A worker can repair a damaged square (cleaning up pollution and
	// volcanic fallout, and filling in bomb craters), thus restoring the
	// square to its former production capacity." (Conquests Civilopedia,
	// TFRM_Clear_Damage)
	public bool HasDamage() {
		return HasPollution() || HasFallout() || HasCraters();
	}

	// Cleans up the tile's pollution, fallout and craters, as a worker's
	// Clear Damage job does. Civ3 cleans fallout the same way as pollution.
	public void ClearDamage() {
		TerrainImprovement craters = overlays.ImprovementAtLayer(Layer.Craters);
		if (craters != null) {
			overlays.Remove(craters);
		}
		TerrainImprovement pollution = overlays.ImprovementAtLayer(Layer.Pollution);
		if (pollution != null) {
			overlays.Remove(pollution);
		}
		TerrainImprovement fallout = overlays.ImprovementAtLayer(Layer.Fallout);
		if (fallout != null) {
			overlays.Remove(fallout);
		}
		TileChangeJournal.Record(this);
	}

	private static bool AnyNeighbor(Tile tile, System.Func<Tile, bool> predicate) {
		foreach (Tile t in tile.neighbors.Values) {
			if (predicate(t)) {
				return true;
			}
		}
		return false;
	}

	// The first of the game's terrain improvements with the given key, or
	// null. A plain loop: the list is short, and this avoids allocating a
	// closure per lookup.
	private static TerrainImprovement FindTerrainImprovement(string key) {
		List<TerrainImprovement> improvements = EngineStorage.gameData.terrainImprovements;
		for (int i = 0; i < improvements.Count; i++) {
			if (improvements[i].key == key) {
				return improvements[i];
			}
		}
		return null;
	}

	public class TileOverlays {
		public const string ROAD = "road";
		public const string RAILROAD = "railroad";
		public const string IRRIGATION = "irrigation";
		public const string MINE = "mine";
		public const string FORTRESS = "fortress";
		public const string BARRICADE = "barricade";
		public const string BARBARIAN_CAMP = "barbarianCamp";
		public const string GOODY_HUT = "goodyHut";
		public const string RUINS = "ruins";
		public const string POLLUTION = "pollution";
		public const string CRATERS = "craters";
		public const string FALLOUT = "fallout";
		// Holdings a worker builds for its civ, which belong to it (see
		// Tile.holdingOwner).
		public const string OUTPOST = "outpost";
		public const string COLONY = "colony";
		public const string AIRFIELD = "airfield";
		public const string RADAR_TOWER = "radarTower";

		private readonly Tile tile;
		public Dictionary<Layer, TerrainImprovement> terrainImprovementByLayer { get; private set; } = [];

		public TileOverlays(Tile tile) {
			this.tile = tile;
		}

		public void Add(TerrainImprovement improvement) {
			if (improvement == null) {
				return;
			}
			terrainImprovementByLayer.TryGetValue(improvement.layer, out TerrainImprovement replacedImprovement);

			terrainImprovementByLayer[improvement.layer] = improvement;

			ApplyTerrainImprovementChange(replacedImprovement, improvement);
		}

		public void Remove(TerrainImprovement improvement) {
			if (!terrainImprovementByLayer.Remove(improvement.layer))
				Log.Warning("Failed to remove terrain improvement.");

			ApplyTerrainImprovementChange(improvement, null);
		}

		private void ApplyTerrainImprovementChange(TerrainImprovement oldImprovement, TerrainImprovement newImprovement) {
			// Path costs depend on the road layer, upgrades included.
			if (oldImprovement?.layer == Layer.Roads || newImprovement?.layer == Layer.Roads) {
				C7Engine.Pathing.LandmarkHeuristic.OnOverlaysChanged(tile);
			}

			var roadCreated = oldImprovement == null && newImprovement?.layer == Layer.Roads;
			var roadRemoved = oldImprovement?.layer == Layer.Roads && newImprovement == null;

			// If there's a change in road coverage, invalidate the cached trade
			// networks it can change.
			if (roadCreated || roadRemoved) {
				EngineStorage.gameData.GetTradeNetwork().OnRoadChanged(tile);
			}

			// An outpost, colony, airfield or radar tower going away, however
			// it goes (pillaged, bombarded, built over, taken in by borders),
			// takes its owner's sight, resource, air base or radar with it.
			if (oldImprovement != null && oldImprovement != newImprovement && IsOwnedHolding(oldImprovement)) {
				tile.ForgetHolding(oldImprovement);
			}
		}

		// Whether the improvement is one that belongs to a civ: an outpost,
		// a colony, an airfield or a radar tower.
		public static bool IsOwnedHolding(TerrainImprovement improvement) {
			return improvement.key is OUTPOST or COLONY or AIRFIELD or RADAR_TOWER;
		}

		public TerrainImprovement ImprovementAtLayer(Layer layer) {
			terrainImprovementByLayer.TryGetValue(layer, out TerrainImprovement ti);
			return ti;
		}

		// Returns an existing improvement that would be replaced by the given terraform.
		// Returns null if there is no such improvement,
		// or the new improvement upgrades from the existing one (upgrades don't count as replacements)
		public TerrainImprovement GetReplacementTarget(Terraform terraform) {
			var newImp = terraform.Improvement;
			if (newImp == null)
				return null;

			var current = ImprovementAtLayer(newImp.layer);
			if (current == null)
				return null;

			return newImp.upgradesFrom != current ? current : null;
		}

		public bool HasImprovement(TerrainImprovement improvement) {
			return terrainImprovementByLayer.TryGetValue(improvement.layer, out TerrainImprovement val) && val == improvement;
		}

		// Whether the tile has an improvement with the given key. Such
		// improvements normally live on their own layer (expectedLayer),
		// which is checked first; the others are checked too in case a
		// ruleset put the key on another layer. Doesn't allocate.
		public bool HasImprovementWithKey(string key, Layer expectedLayer) {
			if (terrainImprovementByLayer.TryGetValue(expectedLayer, out TerrainImprovement atLayer) && atLayer.key == key) {
				return true;
			}
			foreach (TerrainImprovement ti in terrainImprovementByLayer.Values) {
				if (ti.key == key) {
					return true;
				}
			}
			return false;
		}

		// Returned as the dictionary's value collection (an IEnumerable),
		// so a foreach over it uses a struct enumerator and doesn't allocate.
		public Dictionary<Layer, TerrainImprovement>.ValueCollection GetImprovements() {
			return terrainImprovementByLayer.Values;
		}
		public IEnumerable<TerrainImprovement> GetManMadeImprovements() {
			return GetImprovements().Where(IsManMade);
		}
		public IEnumerable<TerrainImprovement> GetEffectImprovements() {
			return GetImprovements().Where(i => !IsManMade(i));
		}

		// Whether the terrain lets the improvement be built on it at all.
		// Forts (and barricades, which go on a fort), airfields, radar
		// towers, outposts and colonies follow the terrain's BIQ flags (see
		// TerrainType.allowForts). Roads and railroads need terrain with a
		// road bonus: "Restricted roads/railroads from being built on terrain
		// that has no road bonus." (Civ3 1.29 readme), so none go on a
		// Volcano. A road's bonus is the commerce it adds to the terrain
		// (BIQ TERR RoadBonus, imported as the road's bonus yield).
		public static bool TerrainAllows(TerrainType terrain, TerrainImprovement improvement) {
			if (terrain == null || improvement == null) {
				return true;
			}
			switch (improvement.key) {
				case ROAD:
				case RAILROAD:
					return improvement.GetYieldBonus(terrain, YieldType.Commerce) > 0;
				case FORTRESS:
				case BARRICADE:
					return terrain.allowForts;
				case AIRFIELD:
					return terrain.allowAirfields;
				case RADAR_TOWER:
					return terrain.allowRadarTowers;
				case OUTPOST:
					return terrain.allowOutposts;
				case COLONY:
					return terrain.allowColonies;
				default:
					return true;
			}
		}

		public bool CanAdd(Tile targetTile, TerrainImprovement improvement) {
			if (!TerrainAllows(targetTile.overlayTerrainType, improvement)) {
				return false;
			}

			var hasCity = targetTile.HasCity();

			// we can't ever add anything on a city tile, except roading
			if (hasCity && improvement.layer != Layer.Roads)
				return false;

			var hasImprovement = terrainImprovementByLayer.TryGetValue(improvement.layer, out var current);
			var canBeReplaced = hasImprovement && current.CanBeReplacedBy(improvement);

			if (hasCity && improvement.layer == Layer.Roads && canBeReplaced)
				return true;

			if (!hasImprovement)
				return improvement.upgradesFrom == null;

			return canBeReplaced;
		}

		// Will return a -1 if the tile movement cost is unaffected by the improvements
		public float MovementCost() {
			if (terrainImprovementByLayer.TryGetValue(Layer.Roads, out TerrainImprovement road)) {
				return road.movementCost;
			}

			// since we added roads & railroads on city tiles, this is probably not needed
			// I am only leaving this here, because it doesn't seem to affect the game
			// and it's also related to the tile path algorithm, and I think I want to favourite 
			// going through cities if possible, for better defense.
			if (tile.HasCity()) {
				return 0;
			}

			return -1;
		}

		public int GetBaseYieldBonus(YieldType type) {
			int bonus = 0;
			foreach (TerrainImprovement ti in terrainImprovementByLayer.Values) {
				bonus += ti.GetYieldBonus(tile.overlayTerrainType, type);
			}
			return bonus;
		}

		public static bool IsManMade(TerrainImprovement i) {
			return i.layer != Layer.Craters
				&& i.layer != Layer.Pollution
				&& i.layer != Layer.Fallout
				&& i.layer != Layer.Ruins;
		}

		public bool HasBeenImproved() {
			foreach (TerrainImprovement ti in terrainImprovementByLayer.Values) {
				if (IsManMade(ti)) {
					return true;
				}
			}
			return false;
		}

		public IEnumerable<StrengthBonus> GetDefenseBonuses() {
			return terrainImprovementByLayer.Values
				.Select(ti => ti.defenseBonus)
				.Where(v => v.HasValue)
				.Select(v => v.Value);
		}

		public void Clear() {
			TerrainImprovement holding = tile.OwnedHolding();
			terrainImprovementByLayer.Clear();
			if (holding != null) {
				tile.ForgetHolding(holding);
			}
			// Also called when a city is built or destroyed, which changes the
			// tile's movement cost without a road.
			C7Engine.Pathing.LandmarkHeuristic.OnOverlaysChanged(tile);
		}
	}
}
