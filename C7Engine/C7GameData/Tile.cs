using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using static C7GameData.City;
using static C7GameData.TerrainImprovement;
using static C7GameData.Tile.TileOverlays;

namespace C7GameData {
	public partial class Tile {
		public ID Id { get; internal set; }
		public Civ3ExtraInfo ExtraInfo;
		public int XCoordinate;
		public int YCoordinate;

		// Needed for coordinate wrapping.
		public GameMap map;

		// An arbitrary number indicating which landmass this tile is part of,
		// for land-based tiles, or -1 for water.
		//
		// This is used to avoid the expensive process of pathfinding between
		// two land tiles just to discover they have no land connection.
		public int continent;

		// For water tiles, is this tile part of an inland sea with fresh water?
		public bool isFreshWater = false;

		// An arbitrary number indicating which part of the continent this tile
		// is part of, for the purposes of biome assignment.
		public int biomeRegion = -1;

		public City owningCity; // The city whose border contains this tile
		public TerrainType baseTerrainType = TerrainType.NONE;
		public TerrainType overlayTerrainType = TerrainType.NONE;

		public bool HasCity(out City city) {
			city = null;
			if (IsValidCity(cityAtTile)) {
				city = cityAtTile;
				return true;
			}

			return false;
		}
		public bool HasCity() {
			return HasCity(out _);
		}

		private City _cityAtTile;
		public City cityAtTile {
			get => _cityAtTile;
			set {
				_cityAtTile = value;
				// Build city
				if (value != null) {
					BuildCityCallback();
				}
				// Abandon/Destroy city
				else {
					DestroyCityCallback();
				}
			}
		}

		//One thing to decide is do we want to have a tile have a list of units on it,
		//or a unit have reference to the tile it is on, or both?
		//The downside of both is that both have to be updated (and it uses a miniscule amount
		//of memory for pointers), but I'm inclined to go with both since it makes it easy and
		//efficient to perform calculations, whether you need to know which unit on a tile
		//has the best defense, or which tile a unit is on when viewing the Military Advisor.
		public List<MapUnit> unitsOnTile = new List<MapUnit>();
		public string ResourceKey { get; set; }
		public Resource Resource { get; set; }

		// Behaves like a Dictionary<TileDirection, Tile>, but is backed by an
		// array since it is read in the hottest loops; see TileNeighbors.
		public TileNeighbors neighbors { get; set; } = new TileNeighbors();

		public CityResident personWorkingTile = null;   //allows us to see if another city is working this tile

		public bool hasBarbarianCamp = false;

		// A tribal village that gives a reward, or trouble, to the first civ
		// to enter it. See GoodyHuts.
		public bool hasGoodyHut = false;

		//See discussion on page 4 of the "Babylon" thread (https://forums.civfanatics.com/threads/0-1-babylon-progress-thread.673959) about sub-terrain type and Civ3 properties.
		//We may well move these properties somewhere, whether that's Civ3ExtraInfo, a Civ3Tile child class, a Dictionary property, or something else, in the future.
		public bool isBonusShield;
		public bool isSnowCapped;
		public bool isPineForest;

		public bool riverNorth;
		public bool riverNortheast;
		public bool riverEast;
		public bool riverSoutheast;
		public bool riverSouth;
		public bool riverSouthwest;
		public bool riverWest;
		public bool riverNorthwest;

		// The first time a forest is cleared on a tile it can award shields to
		// a nearby city.
		public bool hasHadForestCleared = false;

		public TileOverlays overlays;

		public Tile(ID id) {
			this.Id = id;
			unitsOnTile = new List<MapUnit>();
			Resource = Resource.NONE;
			overlays = new(this);
		}

		public static Tile NONE = new Tile(ID.None("tile")) {
			XCoordinate = -1,
			YCoordinate = -1,
		};

		public static bool IsTileValid(Tile tile) {
			return tile != null && tile != NONE;
		}

		private void BuildCityCallback() {
			var hasRoad = this.HasRoad();
			var hasRailroad = this.HasRailroad();

			// remove stuff like a forest, jungle etc (whatever can be cleared by a worker action), but not hills/mountains etc
			if (this.overlayTerrainType.allowedFoliageAction != TerrainType.Civ3FoliageAction.None)
				ClearTerrainOverlay();

			overlays.Clear();

			// Auto connect cities to adjacent road/railroad network
			TryAddRoad(this, hasRoad, hasRailroad);
			TryAddRailroad(this, hasRailroad);

			// Somehow in the base game, craters persist when a city is built on top.
			// I choose to implement this differently here,
			// where ruins are cleared when the city is built
		}

		private void DestroyCityCallback() {
			ClearTerrainOverlay();
			overlays.Clear();
			TryAddRuins(this);
		}

		public bool HasPollution() {
			return this.overlays.HasImprovementWithKey(POLLUTION, Layer.Pollution);
		}
		public bool HasRuins() {
			return this.overlays.HasImprovementWithKey(RUINS, Layer.Ruins);
		}
		public bool HasFallout() {
			return this.overlays.HasImprovementWithKey(FALLOUT, Layer.Fallout);
		}
		public bool HasCraters() {
			return this.overlays.HasImprovementWithKey(CRATERS, Layer.Craters);
		}

		// TODO: this should be either an extension in C7Engine, or otherwise
		// calculated somewhere else, but it's not obvious to someone unfamiliar
		// with the save format that it's the overaly terrain that has actual
		// movement cost
		public int MovementCost() {
			return overlayTerrainType.movementCost;
		}

		// Like MovementCost(), impassability comes from the overlay terrain
		// that actually exists on the tile.
		public bool IsImpassable() {
			return overlayTerrainType.impassable;
		}

		//This should be used when we want to check if land tiles are next to water tiles.
		//Usually this is coast, but it could be Sea - see the "Deepwater Harbours" topics at CFC.
		//Sometimes we care *specifically* about the Coast terrain, e.g. galleys can only move on that terrain, not Sea or Ocean
		//Those cases should not use this method.
		public bool NeighborsWater() {
			foreach (Tile neighbor in neighbors.Values) {
				if (neighbor.baseTerrainType.IsWater) {
					return true;
				}
			}
			return false;
		}

		public bool NeighborsFreshWater() {
			foreach (Tile neighbor in neighbors.Values) {
				if (neighbor.baseTerrainType.IsWater && neighbor.isFreshWater) {
					return true;
				}
			}
			return false;
		}

		public bool NeighborsOcean() {
			foreach (Tile neighbor in neighbors.Values) {
				if (neighbor.baseTerrainType.IsWater && !neighbor.isFreshWater) {
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Returns neighbors along edges only, skipping off-map ones
		/// (Tile.NONE).
		/// This is used by some graphics algorithms.
		/// </summary>
		/// <returns></returns>
		public Tile[] GetEdgeNeighbors() {
			int count = 0;
			foreach (TileDirection dir in EdgeDirections) {
				Tile t = neighbors.Get(dir);
				if (t != null && t != NONE) {
					count++;
				}
			}
			Tile[] edgeNeighbors = new Tile[count];
			int i = 0;
			foreach (TileDirection dir in EdgeDirections) {
				Tile t = neighbors.Get(dir);
				if (t != null && t != NONE) {
					edgeNeighbors[i++] = t;
				}
			}
			return edgeNeighbors;
		}

		// The directions of the neighbors sharing an edge with a tile, in the
		// order GetEdgeNeighbors returns them.
		public static readonly TileDirection[] EdgeDirections = {
			TileDirection.NORTHEAST,
			TileDirection.NORTHWEST,
			TileDirection.SOUTHEAST,
			TileDirection.SOUTHWEST,
		};

		// Whether any of the neighbors returned by GetEdgeNeighbors matches,
		// without allocating.
		public bool AnyEdgeNeighbor(Func<Tile, bool> predicate) {
			foreach (TileDirection dir in EdgeDirections) {
				Tile t = neighbors.Get(dir);
				if (t != null && t != NONE && predicate(t)) {
					return true;
				}
			}
			return false;
		}

		public override string ToString() {
			return "[" + XCoordinate + ", " + YCoordinate + "] (" + overlayTerrainType.Key + " on " + baseTerrainType.Key + ")";
		}

		public List<Tile> GetLandNeighbors() {
			List<Tile> result = new(neighbors.Count);
			foreach (Tile tile in neighbors.Values) {
				if (tile != NONE && !tile.baseTerrainType.IsWater) {
					result.Add(tile);
				}
			}
			return result;
		}

		// Whether the given tile is one of GetLandNeighbors(), without
		// allocating.
		public bool HasLandNeighbor(Tile other) {
			if (other == null || other == NONE || other.baseTerrainType.IsWater) {
				return false;
			}
			return neighbors.ContainsValue(other);
		}

		/**
		 * Returns neighbors of the "Coast" type, not including Sea or Ocean.  This is used e.g. for Galley movement.
		 * Eventually, this should be refactored into a more general "get valid neighbors to move to" type of method,
		 * which could work e.g. for units that can move anywhere except desert.
		 **/
		public List<Tile> GetCoastNeighbors() {
			List<Tile> result = new(neighbors.Count);
			foreach (Tile tile in neighbors.Values) {
				if (tile.baseTerrainType.IsCoast) {
					result.Add(tile);
				}
			}
			return result;
		}

		public bool HasRiverCrossing(TileDirection dir) {
			switch (dir) {
				case TileDirection.NORTH: return riverNorth;
				case TileDirection.NORTHEAST: return riverNortheast;
				case TileDirection.EAST: return riverEast;
				case TileDirection.SOUTHEAST: return riverSoutheast;
				case TileDirection.SOUTH: return riverSouth;
				case TileDirection.SOUTHWEST: return riverSouthwest;
				case TileDirection.WEST: return riverWest;
				case TileDirection.NORTHWEST: return riverNorthwest;
				default: return false;
			}
		}

		public bool IsLand() {
			return !baseTerrainType.IsWater;
		}

		public bool IsWater() {
			return baseTerrainType.IsWater;
		}

		public bool IsCoast() {
			return baseTerrainType.IsCoast;
		}

		public bool IsSea() {
			return baseTerrainType.IsSea;
		}

		public bool IsCountedForDomination() {
			return IsLand() || IsCoast();
		}

		public bool IsCountedForScore() {
			return IsLand() || IsCoast() || IsSea();
		}

		public bool IsAllowCities() {
			return overlayTerrainType.allowCities && !hasBarbarianCamp;
		}

		public bool IsVolcano() {
			return overlayTerrainType.IsVolcano;
		}

		public bool IsRoaded() {
			return this.HasRoad() || this.HasRailroad();
		}

		public bool HasRoad() {
			if (this.HasRailroad())
				return true;

			if (this.overlays.terrainImprovementByLayer.TryGetValue(Layer.Roads, out var value)) {
				if (this.overlays.ImprovementAtLayer(value.layer).key == ROAD)
					return true;
			}
			return false;
		}

		public bool HasRailroad() {
			if (this.overlays.terrainImprovementByLayer.TryGetValue(Layer.Roads, out var value)) {
				if (this.overlays.ImprovementAtLayer(value.layer).key == RAILROAD)
					return true;
			}
			return false;
		}

		public bool HasIrrigation() {
			if (this.overlays.terrainImprovementByLayer.TryGetValue(Layer.ResourceDevelopment, out var value)) {
				if (this.overlays.ImprovementAtLayer(value.layer).key == IRRIGATION)
					return true;
			}
			return false;
		}

		public bool BordersRiver() {
			return riverNorth || riverNortheast || riverEast || riverSoutheast || riverSouth || riverSouthwest || riverWest || riverNorthwest;
		}

		// Whether the player can irrigate this tile. Irrigation needs fresh
		// water (a river, lake or irrigated tile next to it) until the player
		// knows a tech that enables irrigation everywhere (Electricity in
		// Civ3, see Tech.EnablesIrrigationEverywhere).
		public bool CanBeIrrigated(TerrainImprovement irrigation, Player player) {
			// Irrigation can't be done if there is no irrigation bonus for the
			// tile or if there's already an improvement or city on the tile.
			if (!overlays.CanAdd(this, irrigation) ||
				irrigation.GetYieldBonus(overlayTerrainType, YieldType.Food) <= 0 ||
				cityAtTile != null) {
				return false;
			}

			if (player != null && player.KnowsTechWhere(t => t.EnablesIrrigationEverywhere)) {
				return true;
			}

			// If a tile borders a river or fresh water, it has fresh water access.
			if (this.BordersRiver() || this.NeighborsFreshWater()) {
				return true;
			}

			foreach (KeyValuePair<TileDirection, Tile> dirToTile in neighbors) {
				// If a neighboring tile is irrigated, this tile has fresh water access.
				if (dirToTile.Value.overlays.HasImprovement(irrigation)) {
					return true;
				}

				// Special case, if we are neighboring a city, check
				// if the city can act as part of an irrigation chain.
				if (dirToTile.Value.cityAtTile != null) {
					if (dirToTile.Value.BordersRiver() || dirToTile.Value.NeighborsFreshWater()) {
						return true;
					}

					foreach (var (dir, tile) in dirToTile.Value.neighbors) {
						if (tile.overlays.HasImprovement(irrigation)) {
							return true;
						}
					}
				}
			}

			return false;
		}

		public Player? OwningPlayer() {
			if (cityAtTile != null) {
				return cityAtTile.owner;
			}
			if (owningCity != null) {
				return owningCity.owner;
			}
			return null;
		}

		public void MaybeAwardForestClearingShields() {
			MaybeAwardForestClearingShields(null);
		}

		// Awards the forest's shields when the forest on this tile, within
		// some city's borders, is cleared. If clearer is given, the territory
		// must be theirs: clearing a forest in someone else's territory
		// doesn't feed their city. Per the project owner, chopped shields
		// never go to a wonder: they go to the city whose territory holds the
		// tile unless it is building a wonder, and otherwise to the nearest
		// other city of the same owner with the tile in its workable radius
		// that isn't, or else they are lost. A city given them can't switch
		// to a wonder until it completes something (see
		// City.WhyCannotChangeProduction).
		public void MaybeAwardForestClearingShields(Player clearer) {
			if (hasHadForestCleared) {
				return;
			}
			hasHadForestCleared = true;

			// Shields can only be awarded if the forest is within some city's
			// borders.
			City territoryCity = cityAtTile ?? owningCity;
			if (territoryCity == null || territoryCity.location == null || territoryCity.location == NONE) {
				return;
			}
			Player beneficiary = territoryCity.owner;
			if (clearer != null && beneficiary != clearer) {
				return;
			}

			// The forest has to be within the city's big fat cross.
			int maxRank = EngineStorage.gameData.rules.MaxRankOfWorkableTiles;
			City c = beneficiary.cities
				.Where(city => city.location != null && city.location != NONE
					&& (city.location == this || city.location.RankDistanceTo(this) <= maxRank)
					&& city.CanReceiveForestShields())
				.OrderBy(city => city == territoryCity ? 0 : 1)
				.ThenBy(city => city.location.RankDistanceTo(this))
				.FirstOrDefault();
			if (c == null) {
				return;
			}

			int shieldsAwarded = EngineStorage.gameData.rules.ForestValueInShields;
			c.AddForestShields(shieldsAwarded);

			if (c.owner.isHuman) {
				new MsgShowTemporaryPopup($"{shieldsAwarded} shields awarded to {c.name} for clearing forests", c.location, c.owner).send();
			}
		}

		public MapUnit FindTopDefenderForBombard(MapUnit opponent) {
			return FindTopDefenderForBombard(this, opponent);
		}

		public MapUnit FindTopDefenderForBombard(Tile tile, MapUnit opponent) {
			// Units in an army are hit through the army. Bombardment never
			// kills, per the project owner, so units down to their last hit
			// point can't be hit.
			return FindTopCombatUnit(opponent, tile.unitsOnTile, CandidateFilter.NonLethalBombardTarget);
		}

		public MapUnit FindTopDefender(MapUnit opponent) {
			return FindTopDefender(opponent, unitsOnTile);
		}

		public MapUnit FindTopDefender(MapUnit opponent, List<MapUnit> units) {
			// Units in an army don't defend by themselves; the army defends
			// with them.
			return FindTopCombatUnit(opponent, units, CandidateFilter.Defender);
		}

		public MapUnit FindTopCombatUnit(MapUnit opponent, List<MapUnit> units) {
			return FindTopCombatUnit(opponent, units, CandidateFilter.All);
		}

		private enum CandidateFilter {
			All,
			Defender,
			BombardTarget,
			NonLethalBombardTarget,
		}

		private static bool IsCandidate(MapUnit u, MapUnit opponent, CandidateFilter filter) {
			switch (filter) {
				case CandidateFilter.Defender:
					return u.CanDefendAgainst(opponent) && !u.IsInArmy();
				case CandidateFilter.BombardTarget:
					return u.IsCombatUnit() && !u.IsInArmy();
				case CandidateFilter.NonLethalBombardTarget:
					return u.IsCombatUnit() && !u.IsInArmy() && u.CompositeHitPoints() > 1;
				default:
					return true;
			}
		}

		// Picks the candidate that has priority as defender over all the
		// others (see MapUnit.HasPriorityAsDefender). Each candidate's
		// priority is computed once, and candidates are compared exactly as
		// calling HasPriorityAsDefender against the current leader would, so
		// the first of several equally good candidates wins.
		private static MapUnit FindTopCombatUnit(MapUnit opponent, List<MapUnit> units, CandidateFilter filter) {
			MapUnit leader = MapUnit.NONE;
			bool leaderIsEnemy = false;
			double leaderStrength = 0;
			foreach (MapUnit u in units) {
				if (!IsCandidate(u, opponent, filter)) {
					continue;
				}
				bool isEnemy = u.IsEnemyDefenderAgainst(opponent);
				double strength = u.TotalDefensiveStrengthVersus(opponent);
				if (leader == MapUnit.NONE || MapUnit.HasPriorityAsDefender(isEnemy, strength, leaderIsEnemy, leaderStrength)) {
					leader = u;
					leaderIsEnemy = isEnemy;
					leaderStrength = strength;
				}
			}
			return leader;
		}

		/// <summary>
		/// Disbands non-defending units on a tile.  This should only be called when all defending units have been destroyed,
		/// hence its name.  E.g. if only air/sea units remain after a land battle, this should be called.
		///
		/// Eventually, we should also have a method to make relevant units (workers, artillery, etc.) be captured.
		/// </summary>
		/// <param name="tile"></param>
		public void DisbandNonDefendingUnits(Player owner) {
			//There may have been naval units, if so, disband them
			if (unitsOnTile.Count > 0) {
				//Copy to a separate array so we don't crash due to concurrent modification exceptions
				MapUnit[] unitsOnTile = new MapUnit[this.unitsOnTile.Count];
				this.unitsOnTile.CopyTo(unitsOnTile);
				foreach (MapUnit destroyedUnit in unitsOnTile) {
					// Ensure we only destroy units of the losing side of the
					// combat, not the unit entering the city.
					if (destroyedUnit.owner == owner) {
						destroyedUnit.RemoveFromPlay();
					}
				}
			}
		}

		/// <summary>
		/// After a WorkerJob has finished, Cclean up all the WorkerJobs and set the correct overlay
		/// </summary>
		/// <param name="tile">the current tile</param>
		/// <param name="currentWorkerJob">the worker job currently finished, must not be null</param>
		public void FinishWorkerJob(Terraform currentWorkerJob) {
			// Reset All Workers working on the finished Job
			Player player = null;
			foreach (MapUnit unit in unitsOnTile) {
				player = unit.owner;
				if (currentWorkerJob == unit.WorkerJob) {
					unit.resetWorkerJob();
				}
			}

			currentWorkerJob.OnComplete(player, this);
		}

		public float GetCurrentUnaccountedJobProgress(Terraform currentWorkerJob) {
			float progress = 0;
			foreach (MapUnit unit in unitsOnTile) {
				if (currentWorkerJob == unit.WorkerJob) {
					progress += unit.workerSpeed();
				}
			}
			return progress;
		}

		public async Task AnimateAsync(AnimatedEffect effect) {
			if (!EngineStorage.animationsEnabled) return;

			// The UI only plays effects on tiles its player can see, and
			// just marks the others completed on its next frame. Skip those
			// here so the engine doesn't wait a frame for nothing.
			if (!IsVisibleToUIPlayer(this)) return;

			var msg = new MsgStartEffectAnimation(this, effect, AnimationEnding.Stop);
			msg.send();

			await EngineStorage.WaitForAnimationFinished(msg.animationId);
		}

		// Whether the UI's player sees the tile, i.e. whether the UI would
		// play an animation on it. When the UI's player isn't known, says yes
		// so that animations keep being sent.
		internal static bool IsVisibleToUIPlayer(Tile tile) {
			Player uiPlayer = EngineStorage.gameData?.GetUIControllerPlayer();
			if (uiPlayer?.tileKnowledge == null) {
				return true;
			}
			return tile != null && uiPlayer.tileKnowledge.isActiveTile(tile);
		}

		public void Animate(AnimatedEffect effect) {
			_ = AnimateAsync(effect);
		}

		public void ClearTerrainOverlay() {
			overlayTerrainType = baseTerrainType;
		}

		public bool HasImprovements => overlays.HasBeenImproved();
	}
}
