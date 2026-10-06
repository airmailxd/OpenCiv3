using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Serilog;
using C7Engine.Lua;
using C7Engine.Pathing;
using System.Threading.Tasks;
using C7Engine;

[assembly: InternalsVisibleTo("EngineTests")]
namespace C7GameData {
	public class GameData {
		private static ILogger log = Log.ForContext<GameData>();

		public int seed = -1;   //change here to set a hard-coded seed
		public int turn { get; set; }
		public static Random rng; // TODO: Is GameData really the place for this?
		public ID.Factory ids = new();
		public GameMap map { get; set; }
		public List<Player> players = new List<Player>();
		public List<TerrainType> terrainTypes = new List<TerrainType>();
		public List<TerrainImprovement> terrainImprovements = [];
		public List<Resource> Resources = new List<Resource>();
		// Kept as a list so units are processed in a stable order. Use GetUnit
		// to look a unit up by its id.
		public List<MapUnit> mapUnits {
			get => _mapUnits;
			set {
				_mapUnits = value;
				unitsById = null;
			}
		}
		private List<MapUnit> _mapUnits = new List<MapUnit>();

		// An index of mapUnits by id. It is kept up to date by the methods
		// here that add and remove units, and rebuilt whenever its size no
		// longer matches mapUnits (when units were added or removed elsewhere).
		private Dictionary<ID, MapUnit> unitsById;

		// An index of cities by id, holding each city's position in the cities
		// list so that a stale entry can be detected and the index rebuilt.
		private Dictionary<ID, int> cityIndexById;
		private List<City> indexedCities;
		private int indexedCityCount;

		// The player last returned by GetUIControllerPlayer.
		private Player cachedUIControllerPlayer;
		private List<Player> cachedUIControllerPlayers;
		private int cachedUIControllerPlayerCount;

		// The culture (current plus per turn) of each city, cached while
		// UpdateTileOwners runs since nothing changes culture in the meantime.
		private Dictionary<City, int> cultureCache;

		// The position of each city in the list of cities, which is the order
		// they were founded in, cached while UpdateTileOwners runs.
		private Dictionary<City, int> foundingOrderCache;
		public List<UnitPrototype> unitPrototypes = new();
		public List<Building> Buildings = new();
		public List<Inflow> Inflows = new();

		// The names of all great wonders that have been built.
		public HashSet<string> GreatWondersBuilt = new();

		public List<City> cities = new List<City>();

		internal List<Civilization> civilizations = new List<Civilization>();
		internal HashSet<CultureGroup> cultureGroups = new HashSet<CultureGroup>();
		internal HashSet<Alliance> alliances;
		internal Dictionary<Alliance, Alliance> allianceWars = new Dictionary<Alliance, Alliance>();

		public List<ExperienceLevel> experienceLevels = new List<ExperienceLevel>();
		public List<Tech> techs = new();
		public List<CitizenType> citizenTypes = new();
		public List<Terraform> Terraforms = new();
		public List<Government> governments = new();
		public List<WorldSize> worldSizes = new();
		public List<Difficulty> difficulties = new();
		public Difficulty gameDifficulty = new();
		public string defaultExperienceLevelKey;
		public ExperienceLevel defaultExperienceLevel;
		public Rules rules;
		public TimeOptions timeOptions;
		public Dictionary<string, List<HistTurnRecord>> history;

		public VictoryConditions victoryConditions;
		public List<IVictory> victories = new();
		public bool gameOver;
		public Player winner;
		// TODO: Victory type serialization

		// The United Nations elections (see C7Engine.UnitedNations).
		public UnitedNationsState unitedNations = new();

		public BarbarianInfo barbarianInfo = new BarbarianInfo();

		public StrengthBonus fortificationBonus;
		public StrengthBonus riverCrossingBonus;
		public StrengthBonus cityLevel1DefenseBonus;
		public StrengthBonus cityLevel2DefenseBonus;
		public StrengthBonus cityLevel3DefenseBonus;

		public int healRateInFriendlyField;
		public int healRateInNeutralField;
		public int healRateInHostileField;
		public int healRateInCity;

		public bool observerMode = false;
		public bool showGridCoordinates = false;

		public string scenarioSearchPath;   //legacy from Civ3, we'll probably have a more modern format someday but this keeps legacy compatibility

		internal BehaviorEngine luaBehaviorEngine;
		internal GameMode.Config gameModeConfig;

		// The cached trade network for all players. This is invalidated whenever
		// a road is built or a city is created/destroyed.
		private TradeNetwork tradeNetwork;

		// An action called after initialization of EngineStorage
		internal Action onGameCreation;

		public GameData(int customSeed = -1) {
			map = new GameMap();
			seed = customSeed;
			// this will probably never happen, leaving it as a fallback
			if (seed == -1) {
				log.Information("Random seed was not set, generating...");
				rng = new Random();
				seed = rng.Next(int.MaxValue);
			}
			rng = new Random(seed);
			log.Information("Seed is {Seed}", seed);
		}

		// Returns the player whose perspective the UI is currently showing. In a
		// hotseat game this changes as each human player takes their turn, and
		// in observer mode it is still the player the UI was following, even
		// though that player is no longer marked as human.
		public Player GetUIControllerPlayer() {
			// This is called for every tile on every frame by the map renderer,
			// so remember the answer while the controller and players are
			// unchanged.
			ID id = EngineStorage.uiControllerID;
			Player cached = cachedUIControllerPlayer;
			if (cached != null && cached.id == id
				&& ReferenceEquals(cachedUIControllerPlayers, players)
				&& cachedUIControllerPlayerCount == players.Count) {
				return cached;
			}
			Player player = GetPlayer(id);
			cachedUIControllerPlayer = player;
			cachedUIControllerPlayers = players;
			cachedUIControllerPlayerCount = players.Count;
			return player;
		}

		public List<Player> GetRivals(Player player) {
			return players.Where(p => !p.isBarbarians && p != player && !p.defeated).ToList();
		}

		public List<Player> GetKnownRivals(Player player) {
			var rivals = GetRivals(player);
			return rivals.Where(x => player.playerRelationships.ContainsKey(x.id)).ToList();
		}

		public MapUnit GetUnit(ID id) {
			if (id is null) {
				return _mapUnits.Find(u => u.id == id);
			}
			if (unitsById == null || unitsById.Count != _mapUnits.Count) {
				RebuildUnitIndex();
			}
			return unitsById.GetValueOrDefault(id);
		}

		private void RebuildUnitIndex() {
			unitsById = new Dictionary<ID, MapUnit>(_mapUnits.Count);
			foreach (MapUnit unit in _mapUnits) {
				// Like List.Find, the first unit with a given id wins.
				if (unit.id is not null) {
					unitsById.TryAdd(unit.id, unit);
				}
			}
		}

		// Adds a unit to mapUnits, keeping the index up to date.
		internal void AddMapUnit(MapUnit unit) {
			_mapUnits.Add(unit);
			if (unitsById != null && unit.id is not null) {
				unitsById.TryAdd(unit.id, unit);
			}
		}

		private void RemoveMapUnit(MapUnit unit) {
			if (!_mapUnits.Remove(unit)) {
				return;
			}
			if (unitsById != null && unit.id is not null
				&& unitsById.TryGetValue(unit.id, out MapUnit indexed) && indexed == unit) {
				unitsById.Remove(unit.id);
			}
		}

		// Returns the city with the given id, or null if there is none.
		public City GetCity(ID id) {
			if (id is null) {
				return cities.Find(c => c.id == id);
			}
			if (cityIndexById == null || !ReferenceEquals(indexedCities, cities) || indexedCityCount != cities.Count) {
				// The list was replaced, or cities were added or removed.
				RebuildCityIndex();
			} else if (cityIndexById.TryGetValue(id, out int index)) {
				if (index < cities.Count && cities[index].id == id) {
					return cities[index];
				}
				// The list was changed without its size changing.
				RebuildCityIndex();
			} else {
				// Most likely there is no such city. Only if there is (a city
				// was swapped in without the size changing) is the index stale.
				// Checking is cheaper than rebuilding the index on every miss.
				if (!cities.Exists(c => c.id == id)) {
					return null;
				}
				RebuildCityIndex();
			}
			return cityIndexById.TryGetValue(id, out int found) && found < cities.Count && cities[found].id == id
				? cities[found]
				: null;
		}

		private void RebuildCityIndex() {
			indexedCities = cities;
			indexedCityCount = cities.Count;
			cityIndexById = new Dictionary<ID, int>(cities.Count);
			for (int i = 0; i < cities.Count; ++i) {
				if (cities[i].id is not null) {
					cityIndexById.TryAdd(cities[i].id, i);
				}
			}
		}

		public Player GetPlayer(ID id) {
			foreach (Player p in players) {
				if (p.id == id) {
					return p;
				}
			}
			return null;
		}

		public Tech GetTech(ID id) {
			return techs.Find(p => p.id == id);
		}

		public ExperienceLevel GetExperienceLevelAfter(ExperienceLevel experienceLevel) {
			int n = experienceLevels.IndexOf(experienceLevel);
			if (n + 1 < experienceLevels.Count)
				return experienceLevels[n + 1];
			else
				return null;
		}

		// Players are in an active locked war (can't make peace)
		private bool AreAlliancesInLockedWar(Alliance one, Alliance other) {
			return allianceWars.Any(
				kvp => (one != null && other != null) &&
					((kvp.Key.name == one.name && kvp.Value.name == other.name)
					|| (kvp.Key.name == other.name && kvp.Value.name == one.name)));
		}
		public bool AreInLockedWar(Player one, Player other) {
			return AreAlliancesInLockedWar(one.alliance, other.alliance) && one != other;
		}

		// Both players are part of the same alliance (can't declare war)
		// We exclude ourselves from this.
		private bool AreInSameAlliance(Alliance one, Alliance other) {
			return one != null && other != null && one == other;
		}
		public bool AreInLockedPeace(Player one, Player other) {
			return AreInSameAlliance(one.alliance, other.alliance) && one != other;
		}

		public void UpdateTileOwners() {
			cultureCache = new Dictionary<City, int>();
			foundingOrderCache = null;
			try {
				ResolveCityBorders();
				ResolveBorderGaps();
			} finally {
				cultureCache = null;
				foundingOrderCache = null;
			}
		}

		private void ResolveCityBorders() {
			foreach (City city in cities) {
				if (city.residents.Count == 0) {
					continue; // skip destroyed cities
				}

				SetTileOwner(city.location, city);

				foreach (Tile t in city.GetTilesWithinBorders()) {
					// Borders of a city near the map's edge run off it: skip the
					// off-map placeholder, which two such cities would otherwise
					// fight over (crashing in FindInRing, as NONE has no map).
					if (t == Tile.NONE) {
						continue;
					}

					// If another city has claim to this tile, we need to resolve
					// that conflict.
					//
					// We recompute the active tiles once per player afterwards,
					// so we don't need to do it for each tile here.
					if (t.owningCity != null && ResolveTileOwnershipConflict(t.owningCity, city, t, out City winnerCity)) {
						SetTileOwner(t, winnerCity);
						winnerCity.owner.tileKnowledge.AddTilesToKnown(t, false);
						continue;
					}

					SetTileOwner(t, city);
					city.owner.tileKnowledge.AddTilesToKnown(t, false);
				}
			}
		}

		// Brings each player's active tiles and resources up to date, and
		// gives unowned tiles caught between two tiles of the same player to
		// that player (Laws VII and VIII).
		private void ResolveBorderGaps() {
			Dictionary<Player, List<Tile>> tilesByOwner = BucketTilesByOwner(out List<Tile> ownedTiles);

			// Laws VII and VIII only apply to an unowned tile next to an owned
			// one, and almost always there is no such tile that they would
			// give away. In that case there is nothing to resolve, and the
			// order in which the tiles are visited doesn't matter.
			if (!AnyBorderGapToResolve(ownedTiles)) {
				foreach (Player player in players) {
					player.tileKnowledge.RecomputeActiveTiles();
					player.UpdateResourcesInBorders(OwnedTilesOf(tilesByOwner, player));
				}
				return;
			}

			// Otherwise resolve the gaps exactly as we always have: player by
			// player, visiting each player's known tiles in order, since giving
			// away one tile can let another one be given away.
			++borderGapResolutionCount;
			bool ownersChanged = false;
			foreach (Player player in players) {
				player.tileKnowledge.RecomputeActiveTiles();
				if (ownersChanged) {
					tilesByOwner = BucketTilesByOwner(out _);
					ownersChanged = false;
				}
				player.UpdateResourcesInBorders(OwnedTilesOf(tilesByOwner, player));

				List<Tile> candidates = new();
				foreach (Tile t in player.tileKnowledge.knownTiles) {
					if (t.owningCity == null && HasOwnedEdgeNeighbor(t)) {
						candidates.Add(t);
					}
				}
				foreach (Tile t in candidates) {
					// Law VII
					ownersChanged |= TryResolveOpposingNeighbors(t, TileDirection.NORTHWEST, TileDirection.SOUTHEAST);
					if (t.owningCity != null) continue;
					// Law VIII
					ownersChanged |= TryResolveOpposingNeighbors(t, TileDirection.NORTHEAST, TileDirection.SOUTHWEST);
				}
			}
		}

		// The number of times Laws VII and VIII had tiles to give away, for
		// tests.
		internal int borderGapResolutionCount { get; private set; }

		private static readonly TileDirection[] edgeDirections = {
			TileDirection.NORTHEAST, TileDirection.NORTHWEST, TileDirection.SOUTHEAST, TileDirection.SOUTHWEST,
		};

		private static bool HasOwnedEdgeNeighbor(Tile t) {
			foreach (TileDirection direction in edgeDirections) {
				if (t.neighbors.TryGetValue(direction, out Tile e) && e.owningCity != null) {
					return true;
				}
			}
			return false;
		}

		private static IEnumerable<Tile> OwnedTilesOf(Dictionary<Player, List<Tile>> tilesByOwner, Player player) {
			return tilesByOwner.TryGetValue(player, out List<Tile> tiles) ? tiles : Array.Empty<Tile>();
		}

		// Groups the owned tiles of the map by owner, keeping map order.
		private Dictionary<Player, List<Tile>> BucketTilesByOwner(out List<Tile> ownedTiles) {
			Dictionary<Player, List<Tile>> result = new();
			ownedTiles = new();
			foreach (Tile t in map.tiles) {
				City city = t.owningCity;
				if (city == null) {
					continue;
				}
				ownedTiles.Add(t);
				Player owner = city.owner;
				if (owner == null) {
					continue;
				}
				if (!result.TryGetValue(owner, out List<Tile> tiles)) {
					tiles = new List<Tile>();
					result[owner] = tiles;
				}
				tiles.Add(t);
			}
			return result;
		}

		// True if Law VII or VIII would give away some unowned tile next to
		// one of the owned tiles.
		private bool AnyBorderGapToResolve(List<Tile> ownedTiles) {
			HashSet<Tile> checkedTiles = new();
			foreach (Tile owned in ownedTiles) {
				foreach (TileDirection direction in edgeDirections) {
					// Neighbors are symmetric, so every unowned tile with an
					// owned edge neighbor is an edge neighbor of an owned tile.
					if (!owned.neighbors.TryGetValue(direction, out Tile t) || t == Tile.NONE || t.owningCity != null) {
						continue;
					}
					if (!checkedTiles.Add(t)) {
						continue;
					}
					try {
						if (EvaluateOpposingNeighbors(t, TileDirection.NORTHWEST, TileDirection.SOUTHEAST, out City winner) && winner != null) {
							return true;
						}
						if (EvaluateOpposingNeighbors(t, TileDirection.NORTHEAST, TileDirection.SOUTHWEST, out winner) && winner != null) {
							return true;
						}
					} catch (Exception) {
						// Leave it to the exact algorithm to run into this, if
						// it does.
						return true;
					}
				}
			}
			return false;
		}

		// Works out what Law VII or VIII (depending on the directions) says
		// about an unowned tile. Returns false if the law doesn't apply, and
		// otherwise the city that gets the tile, which is null if Law II
		// leaves the tile unowned.
		private bool EvaluateOpposingNeighbors(Tile t, TileDirection dirA, TileDirection dirB, out City newOwner) {
			newOwner = null;
			if (!t.neighbors.TryGetValue(dirA, out Tile a) || !t.neighbors.TryGetValue(dirB, out Tile b)) return false;
			if (a.owningCity == null || b.owningCity == null) return false;
			if (a.owningCity.owner != b.owningCity.owner) return false;
			if (!ResolveTileOwnershipConflict(a.owningCity, b.owningCity, t, out City winnerCity)) return false;

			// Law II
			if (t.baseTerrainType.Key == "ocean" && t.RankDistanceTo(winnerCity.location) > 2) {
				return true;
			}
			newOwner = winnerCity;
			return true;
		}

		// Returns true if the tile was given to a city.
		private bool TryResolveOpposingNeighbors(Tile t, TileDirection dirA, TileDirection dirB) {
			if (!EvaluateOpposingNeighbors(t, dirA, dirB, out City winnerCity)) return false;

			if (winnerCity == null) {
				SetTileOwner(t, null);
				return false;
			}
			SetTileOwner(t, winnerCity);
			winnerCity.owner.tileKnowledge.AddTilesToKnown(t);
			return true;
		}

		// Changes the city that owns a tile. All ownership changes should go
		// through here, so that the players' active tiles are kept up to date.
		private static void SetTileOwner(Tile t, City city) {
			if (t.owningCity != city) {
				t.owningCity = city;
				TileChangeJournal.Record(t);
			}
		}

		public void UpdateTileOwnersOnCityDestruction(City city) {
			SetTileOwner(city.location, null);

			var borderTiles = city.GetTilesWithinBorders();
			var borderTileSet = borderTiles.ToHashSet();

			foreach (Tile tile in borderTiles) {
				SetTileOwner(tile, null);

				// Aggressively remove ownership from edge neighbors around the city outside the natural border.
				// This clears out tile ownership due to Law VII or Law VIII.
				// Regular tile ownership update will re-assign ownership where needed.
				foreach (var fringeTile in tile.GetEdgeNeighbors()) {
					if (borderTileSet.Contains(fringeTile))
						continue;
					if (fringeTile.HasCity()) // skip encountered cities, just in case
						continue;

					SetTileOwner(fringeTile, null);
				}
			}

			UpdateTileOwners();
		}

		// Records that a city has changed hands, so that every player
		// re-examines the tiles it owns. Its borders now come from the new owner's culture there, which may
		// not reach as far, so it lets go of the tiles beyond them. Updating
		// tile owners afterwards hands those to whoever has claim to them.
		internal void OnCityOwnerChanged(City city) {
			HashSet<Tile> withinBorders = city.GetTilesWithinBorders().ToHashSet();
			foreach (Tile t in map.tiles) {
				if (t.owningCity != city) {
					continue;
				}
				if (t != city.location && !withinBorders.Contains(t)) {
					SetTileOwner(t, null);
				} else {
					TileChangeJournal.Record(t);
				}
			}
			TileChangeJournal.Record(city.location);
		}

		public void CheckForCivDestructionAndNotifyUi(Player player) {
			if (this.CheckForCivDestruction(player)) {
				this.CivDestructionCallback(player);
				// Let the UI know about the civ destruction.
				new MsgCivilizationDestroyed(player.civilization).send();
			}
		}

		// Destroys any civ that meets the destruction conditions but slipped
		// past the checks done when a city or unit is lost, so that it stops
		// showing up in diplomacy and counting as a war opponent.
		public void DestroyDefeatedCivs() {
			foreach (Player player in players.ToList()) {
				if (!player.defeated && player.isIncludedInGame) {
					CheckForCivDestructionAndNotifyUi(player);
				}
			}
			RemoveRelationshipsWithDefeatedCivs();
		}

		// Drops every relationship with a defeated civ. Older saves can still
		// carry these, since nothing used to clean them up on load.
		public void RemoveRelationshipsWithDefeatedCivs() {
			HashSet<ID> defeatedIds = players.Where(p => p.defeated).Select(p => p.id).ToHashSet();
			if (defeatedIds.Count == 0) {
				return;
			}
			foreach (Player p in players) {
				if (p.defeated) {
					p.playerRelationships.Clear();
					continue;
				}
				foreach (ID id in defeatedIds) {
					p.playerRelationships.Remove(id);
				}
			}
		}

		private bool CheckForCivDestruction(Player player) {
			// TODO: Implement the full set of conditions for destroying a civ;
			// handling cases like 1 city elimination, regicide, settlers that
			// are still alive, etc.
			if (player.isBarbarians) {
				return false;
			}
			if (player.RemainingCities() > 0) {
				return false;
			}
			if (player.units.Any(u => u.unitType.isSettler)) {
				return false;
			}
			// A civ that has never held a city or a settler, like a scenario
			// civ that starts with only units, lasts as long as its units do.
			if (player.neverHadCityOrSettler && player.units.Count > 0) {
				return false;
			}

			return true;
		}

		private void CivDestructionCallback(Player player) {
			// Mark player as defeated, so when we start removing units,
			// we don't have to check each time if the civ is destroyed
			player.defeated = true;

			// Start from the end and start deleting backwards
			// because the other way doesn't actually go through all units
			// probably because we keep modifying the list and its count gets all messed up
			// Removing a transport or army can take its cargo with it, so
			// always remove whatever unit is currently last.
			while (player.units.Count > 0) {
				RemoveUnit(player.units[^1]);
			}

			// Remove this civ from all other player's relationships, and
			// theirs from this civ's.
			foreach (Player p in players) {
				p.playerRelationships.Remove(player.id);
			}
			player.playerRelationships.Clear();
		}

		[LuaMethod]
		public void SendMessageToUiFromLua(string message, Tile location) {
			log.Information("[LUA API] - {Message}", message);
			new MsgShowTemporaryPopup(message, location).send();

		}

		public async Task DisbandUnit(MapUnit unit) {
			log.Information("Player {Player} disbands unit: {Unit}", unit.owner, unit);

			var disbandFunction = this.luaBehaviorEngine.ImportFunc<Action<MapUnit>>("gameplay.units.disband");
			disbandFunction.Invoke(unit);

			await unit.animateAsync(MapUnit.AnimatedAction.DEATH, AnimationEnding.Pause);

			// Disbanding an army releases the units in it, rather than
			// disbanding them too.
			if (unit.IsArmy()) {
				foreach (MapUnit member in unit.Passengers())
					member.loadedOnUnitId = null;
			}

			RemoveUnit(unit);
		}

		internal void RemoveUnit(MapUnit unit) {
			// Set unit's hit points to zero to indicate that it's no longer alive. Ultimately we may not want to do this. I'm only doing it right
			// now since this way all the UI needs to do to check if the selected unit has been destroyed is to check its hit points.
			unit.hitPointsRemaining = 0;
			unit.movementPoints.onConsumeAll();

			if (unit.currentAI != null) {
				unit.currentAI.UpdateOnDeath();
				unit.currentAI = null;
			}

			// Deal with anything this unit was carrying. The units in an army
			// die with it, as does cargo at sea; a transport's cargo in port is
			// simply unloaded. Either way nothing is left pointing at the
			// removed unit.
			foreach (MapUnit loaded in unit.Passengers()) {
				if (unit.IsArmy() || unit.location.IsWater()) {
					RemoveUnit(loaded);
				} else {
					loaded.loadedOnUnitId = null;
				}
			}

			// EngineStorage.animTracker.endAnimation(unit, false);   TODO: Must send message instead of call directly
			unit.location.unitsOnTile.Remove(unit);
			TileChangeJournal.Record(unit.location);
			RemoveMapUnit(unit);

			Player owner = unit.owner;
			owner.units.Remove(unit);

			log.Information("Player {Player} removed unit: {Unit}", owner, unit);

			// Only the tiles recorded in the journal are re-examined, so this
			// is cheap.
			owner.tileKnowledge.RecomputeActiveTiles();

			if (!owner.defeated)
				CheckForCivDestructionAndNotifyUi(unit.owner);
		}

		/// <summary>
		/// Hands a unit over to the player that captured it. The unit keeps its
		/// nationality, so it becomes a captive (e.g. a slave worker).
		/// </summary>
		internal void CaptureUnit(MapUnit unit, Player captor) {
			Player previousOwner = unit.owner;
			log.Information("Player {Captor} captured unit: {Unit}", captor, unit);

			if (unit.currentAI != null) {
				unit.currentAI.UpdateOnDeath();
				unit.currentAI = null;
			}
			unit.isAutomated = false;
			unit.isFortified = false;
			unit.path = TilePath.NONE;
			unit.resetWorkerJob();
			// Captives can be put to work, or led away, straight away.
			unit.movementPoints.reset(unit.MaxMovementPoints());

			previousOwner.units.Remove(unit);
			unit.owner = captor;
			captor.AddUnit(unit);
			TileChangeJournal.Record(unit.location);

			previousOwner.tileKnowledge.RecomputeActiveTiles();
			captor.tileKnowledge.RecomputeActiveTiles();

			if (!previousOwner.defeated)
				CheckForCivDestructionAndNotifyUi(previousOwner);
		}

		/// <summary>
		/// A captured settler becomes two slave workers for the captor.
		/// </summary>
		internal void CaptureSettler(MapUnit settler, Player captor) {
			Tile tile = settler.location;
			Civilization nationality = settler.nationality;
			UnitPrototype worker = unitPrototypes.FirstOrDefault(p => p.name == "Worker")
				?? unitPrototypes.First(p => p.isWorker);
			log.Information("Player {Captor} captured settler {Settler} as two workers", captor, settler);

			RemoveUnit(settler);
			for (int i = 0; i < 2; i++) {
				MapUnit slave = SpawnUnit(captor, worker, tile);
				slave.nationality = nationality;
			}
			captor.tileKnowledge.RecomputeActiveTiles();
		}

		internal MapUnit SpawnUnit(Player player, UnitPrototype proto, Tile tile) {
			// TODO: consolidate unit spawning routines (here)

			var defaultExpLevel = this.defaultExperienceLevel;
			var barbExpLevel = this.experienceLevels.First(e => e.baseHitPoints == this.barbarianInfo.maxHitpoints);

			MapUnit newUnit = proto.GetInstance(this.GenerateID(proto.name), proto, player, location: tile);

			newUnit.experienceLevel = player.isBarbarians ? barbExpLevel : defaultExpLevel;
			newUnit.experienceLevelKey = player.isBarbarians ? barbExpLevel.key : defaultExpLevel.key;
			newUnit.hitPointsRemaining = newUnit.maxHitPoints;

			tile.unitsOnTile.Add(newUnit);
			TileChangeJournal.Record(tile);
			AddMapUnit(newUnit);
			player.units.Add(newUnit);

			log.Debug("New unit of type {type} added at {tile} for player {player}",
				proto.name, tile, player);
			return newUnit;
		}

		public int TechCostFor(Tech tech, Player player) {
			// Cost formula from https://forums.civfanatics.com/threads/research-cost-formula-v1-29f.29485/.
			// Research Cost = [MM * [10*COST * (1 - N/[CL*1.75])]/(CF * 10)] - progress
			//
			// MM = map modifier (tiny=160, small=200, standard=240, large=320, huge=400)
			// COST = tech cost
			// CF = difficulty factor
			// N = number of known civs that have discovered the tech
			// CL = civs left in game
			//
			// We also have the min/max turns to research of 4 and 50 (defined
			// in the rules)
			// TODO: See this this whole equation can be configurable
			int knownCivsThatKnowTheTech = 0;
			int civsLeft = 0;
			foreach (Player p in players) {
				if (player.playerRelationships.ContainsKey(p.id) && p.knownTechs.Contains(tech.id)) {
					++knownCivsThatKnowTheTech;
				}
				if (!p.isBarbarians && !p.defeated) {
					++civsLeft;
				}
			}

			// CF is the difficulty's cost factor, and only the human pays it: the
			// AI always researches as if on Regent (CF 10), while on Emperor (CF 8)
			// a human's techs cost 10/8 as much. See
			// https://forums.civfanatics.com/threads/ai-difficulty-level-bonuses.37490/.
			int difficultyFactor = player.isHuman ? gameDifficulty.AiCostFactor : gameDifficulty.HumanCostFactor;
			float knowledgeFactor = 1.0f - knownCivsThatKnowTheTech / (civsLeft * 1.75f);
			float researchCost = map.techRate * 10 * tech.Cost  * knowledgeFactor/ (difficultyFactor * 10);

			// Only include the progress factor if this is the tech actively
			// being researched.
			if (player.currentlyResearchedTech == tech.id) {
				researchCost -= player.beakers;
			}

			return (int)Math.Max(Math.Floor(researchCost), 0);
		}

		public TradeNetwork GetTradeNetwork() {
			if (tradeNetwork == null) {
				tradeNetwork = new(this);
			}
			return tradeNetwork;
		}

		public void InvalidateCachedTradeNetwork() {
			tradeNetwork = null;
		}

		// A city's culture plus its culture per turn, which decides contested
		// tiles. Computing the culture per turn is expensive, so it is cached
		// while UpdateTileOwners runs.
		private int CultureForBorders(City city) {
			if (cultureCache == null) {
				return city.GetCulture() + city.GetCulturePerTurn();
			}
			if (!cultureCache.TryGetValue(city, out int culture)) {
				culture = city.GetCulture() + city.GetCulturePerTurn();
				cultureCache[city] = culture;
			}
			return culture;
		}

		// Where a city comes in the order the cities were founded, or -1 if it
		// isn't in the list of cities. New cities are added to the end of the
		// list and captured cities keep their place, so the list is in founding
		// order.
		private int FoundingOrder(City city) {
			if (foundingOrderCache == null) {
				if (cultureCache == null) {
					return cities.IndexOf(city);
				}
				foundingOrderCache = new Dictionary<City, int>();
				for (int i = 0; i < cities.Count; ++i) {
					foundingOrderCache.TryAdd(cities[i], i);
				}
			}
			return foundingOrderCache.TryGetValue(city, out int order) ? order : -1;
		}

		// Rules taken from https://forums.civfanatics.com/threads/the-eight-laws-of-border-dynamics.106882/
		private bool ResolveTileOwnershipConflict(City a, City b, Tile t, out City owner) {
			owner = null;
			if (a.Equals(b)) { owner = a; return true; }

			int aRank = a.location.RankDistanceTo(t);
			int bRank = b.location.RankDistanceTo(t);

			// Law I
			// Cities can claim tiles of rank n+1, where n is the city's expansion level
			if (a.GetBorderExpansionLevel() + 1 < aRank && b.GetBorderExpansionLevel() + 1 >= bRank) { owner = b; return true; }
			if (b.GetBorderExpansionLevel() + 1 < bRank && a.GetBorderExpansionLevel() + 1 >= aRank) { owner = a; return true; }

			// Law III
			// The city with the lowest rank claim gets the tile, so a city's
			// base ring always beats another city's first expansion, which
			// always beats a second expansion, and so on. Culture only
			// decides between claims of the same rank.
			if (aRank > bRank) { owner = b; return true; }
			if (aRank < bRank) { owner = a; return true; }

			// Law IV
			// If the claims are equally strong, the city with more culture gets
			// the tile.
			int aCulture = CultureForBorders(a);
			int bCulture = CultureForBorders(b);
			if (aCulture < bCulture) { owner = b; return true; }
			if (aCulture > bCulture) { owner = a; return true; }

			// Law V
			// If the cultures are equal the oldest city gets the tile.
			int aOrder = FoundingOrder(a);
			int bOrder = FoundingOrder(b);
			if (aOrder >= 0 && bOrder >= 0 && aOrder != bOrder) {
				owner = aOrder < bOrder ? a : b;
				return true;
			}

			// Law VI
			// Starting North of the disputed tile, we go counter-clockwise
			// trying to find the first tile that has one of the competing cities.
			// We start at (rank - 1) because the rank distance does not necessarily reflect the actual "ring"
			// the city tile is in, so a tile at rank 3, could well mean it's in the 2nd ring.
			for (int r = aRank - 1; r <= aRank; r++) {
				if (r <= 0) continue;
				Tile winner = t.FindInRing(r, tile => tile.HasCity() && (tile.cityAtTile == a || tile.cityAtTile == b), false);
				if (winner == null) continue;
				owner = winner.owningCity;
				return true;
			}

			// should never happen, if it does some part of the algorithm has gone wrong
			throw new Exception($"Failed to resolve ownership of {t} between {a.name} and {b.name}, something went wrong");
		}
	}

	public static class GameDataUtils {
		public static ID GenerateID(this GameData gameData, string identifier) {
			return gameData.ids.CreateID(identifier);
		}
	}
}
