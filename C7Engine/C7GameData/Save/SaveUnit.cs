using System.Collections.Generic;
using System.Linq;

namespace C7GameData.Save {

	public class SaveUnit : IHasID {
		public ID id { get; set; }
		public string name;
		public string nationality;
		public string prototype;
		public ID owner;
		public TileLocation previousLocation = new TileLocation();
		public TileLocation currentLocation;
		public List<TileLocation> path;
		public int hitPointsRemaining;
		public float movePointsRemaining;
		public string action; // "fortified", "sentry" or "sentryEnemyOnly"
		public TileDirection facingDirection = TileDirection.SOUTHEAST;
		public string experience;
		public float WorkerProgressTowardsJob;
		public ID WorkerJob;
		public ID loadedOnUnitId;

		// True for multiple types of automation, including worker automation
		// and automated exploring.
		public bool isAutomated;

		// True if the unit held this turn before moving, so it still heals.
		public bool heldWithoutMoving;
		public bool hasAttackedThisTurn;
		public bool hasPillagedThisTurn;

		// True once the unit's victory has produced a great leader; each unit
		// produces at most one.
		public bool hasProducedLeader;

		// How many more times this turn the unit may bombard an attacker of
		// its tile. Saves made before it was saved give a fresh unit's one.
		public int defensiveBombardsRemaining = 1;

		public SaveUnit() { }

		// A copy sharing everything with this one, for a LAN host to change
		// a little for one guest (see C7Engine.Network.SnapshotFilter).
		internal SaveUnit ShallowCopy() {
			return (SaveUnit)MemberwiseClone();
		}

		public SaveUnit(MapUnit unit) {
			id = unit.id;
			name = unit.name;
			nationality = unit.nationality.name;
			prototype = unit.unitType.name;
			owner = unit.owner.id;
			if (unit.previousLocation is not null) {
				previousLocation = new TileLocation(unit.previousLocation);
			}
			currentLocation = new TileLocation(unit.location);
			loadedOnUnitId = unit.loadedOnUnitId;
			if (unit.path?.PathLength() > 0) {
				path = unit.path.path.ToList().ConvertAll(tile => new TileLocation(tile));
			}
			hitPointsRemaining = unit.hitPointsRemaining;
			action = unit.isFortified ? "fortified"
				: unit.isSentried ? (unit.sentryEnemyOnly ? "sentryEnemyOnly" : "sentry")
				: "";
			isAutomated = unit.isAutomated;
			heldWithoutMoving = unit.heldWithoutMoving;
			hasAttackedThisTurn = unit.hasAttackedThisTurn;
			hasPillagedThisTurn = unit.hasPillagedThisTurn;
			hasProducedLeader = unit.hasProducedLeader;
			defensiveBombardsRemaining = unit.defensiveBombardsRemaining;
			facingDirection = unit.facingDirection;
			experience = unit.experienceLevelKey;
			movePointsRemaining = unit.movementPoints.remaining;
			WorkerProgressTowardsJob = unit.WorkerProgressTowardsJob;
			WorkerJob = unit.WorkerJob?.Id;
		}


		// Lookup tables for converting many units, built once rather than
		// searching the lists for every unit. Like List.Find, the first match
		// wins.
		internal class Lookups {
			internal readonly Dictionary<string, UnitPrototype> prototypesByName = new();
			internal readonly Dictionary<string, ExperienceLevel> experienceLevelsByKey = new();
			internal readonly Dictionary<ID, Player> playersById = new();
			internal readonly Dictionary<string, Player> playersByCivilizationName = new();
			internal readonly Dictionary<ID, Terraform> terraformsById = new();
			// The experience level of a unit whose level is unknown.
			internal readonly ExperienceLevel defaultExperienceLevel;

			internal Lookups(List<UnitPrototype> prototypes, List<ExperienceLevel> experienceLevels, List<Player> players, List<Terraform> terraforms,
					string defaultExperienceLevelKey = null) {
				foreach (UnitPrototype p in prototypes) {
					if (p.name != null) prototypesByName.TryAdd(p.name, p);
				}
				foreach (ExperienceLevel el in experienceLevels) {
					if (el.key != null) experienceLevelsByKey.TryAdd(el.key, el);
				}
				foreach (Player player in players) {
					if (player.id is not null) playersById.TryAdd(player.id, player);
					if (player.civilization?.name != null) playersByCivilizationName.TryAdd(player.civilization.name, player);
				}
				foreach (Terraform tf in terraforms) {
					if (tf.Id is not null) terraformsById.TryAdd(tf.Id, tf);
				}
				defaultExperienceLevel = Find(experienceLevelsByKey, defaultExperienceLevelKey)
					?? (experienceLevels.Count > 0 ? experienceLevels[0] : null);
			}

			internal static T Find<K, T>(Dictionary<K, T> dict, K key) where T : class {
				return key is not null && dict.TryGetValue(key, out T value) ? value : null;
			}
		}

		public MapUnit ToMapUnit(List<UnitPrototype> prototypes, List<ExperienceLevel> experienceLevels, List<Player> players, List<Terraform> terraforms, GameMap map) {
			return ToMapUnit(new Lookups(prototypes, experienceLevels, players, terraforms), map);
		}

		internal MapUnit ToMapUnit(Lookups lookups, GameMap map) {
			UnitPrototype unitType = Lookups.Find(lookups.prototypesByName, prototype)
				?? throw new KeyNotFoundException($"Unit {id} is of unknown type {prototype}");
			Player unitOwner = Lookups.Find(lookups.playersById, owner)
				?? throw new KeyNotFoundException($"Unit {id} is owned by unknown player {owner}");
			ExperienceLevel experienceLevel = Lookups.Find(lookups.experienceLevelsByKey, experience);
			if (experienceLevel == null && lookups.defaultExperienceLevel != null) {
				Serilog.Log.Warning("Unit {Id} has unknown experience level {Level}; making it {Default}", id, experience, lookups.defaultExperienceLevel.key);
				experienceLevel = lookups.defaultExperienceLevel;
			}
			Terraform workerJob = WorkerJob == null ? null : Lookups.Find(lookups.terraformsById, WorkerJob);
			if (WorkerJob != null && workerJob == null) {
				Serilog.Log.Warning("Unit {Id} was working on unknown job {Job}, which it stops", id, WorkerJob);
			}
			MapUnit unit = new MapUnit{
				id = id,
				unitType = unitType,
				experienceLevelKey = experienceLevel?.key ?? experience,
				experienceLevel = experienceLevel,
				owner = unitOwner,
				location = map.tileAt(currentLocation.X, currentLocation.Y),
				loadedOnUnitId = loadedOnUnitId,
				previousLocation = previousLocation.X == -1 ? Tile.NONE : map.tileAt(previousLocation.X, previousLocation.Y),
				hitPointsRemaining = hitPointsRemaining,
				movementPoints = new MovementPoints(),
				isFortified = action == "fortified",
				isSentried = action == "sentry" || action == "sentryEnemyOnly",
				sentryEnemyOnly = action == "sentryEnemyOnly",
				isAutomated = isAutomated,
				heldWithoutMoving = heldWithoutMoving,
				hasAttackedThisTurn = hasAttackedThisTurn,
				hasPillagedThisTurn = hasPillagedThisTurn,
				hasProducedLeader = hasProducedLeader,
				defensiveBombardsRemaining = defensiveBombardsRemaining,
				facingDirection = facingDirection,
				WorkerProgressTowardsJob = WorkerProgressTowardsJob,
				WorkerJob = workerJob,
			};
			// A unit that isn't on the map can't be put on a tile; in
			// particular it mustn't be added to the shared Tile.NONE. The game
			// leaves such units out (see SaveGame.ConvertUnits).
			if (unit.location == Tile.NONE) {
				Serilog.Log.Warning("Unit {Id} is at ({X}, {Y}), which is not on the map", id, currentLocation.X, currentLocation.Y);
			} else {
				unit.location.unitsOnTile.Add(unit);
			}
			unit.path = ToTilePath(map);
			unit.movementPoints.reset(movePointsRemaining);
			unit.name = string.IsNullOrEmpty(name) ? unit.unitType.name : name;

			Player originalNationality = Lookups.Find(lookups.playersByCivilizationName, nationality);
			if (originalNationality != null) {
				unit.nationality = originalNationality.civilization;
			} else {
				unit.nationality = unit.owner.civilization;
			}

			return unit;
		}

		// The path the unit was following, or null if it had none. A path
		// leaving the map is dropped, since the unit couldn't follow it.
		private TilePath ToTilePath(GameMap map) {
			if (path == null || path.Count == 0) {
				return null;
			}
			Queue<Tile> tiles = new(path.Count);
			foreach (TileLocation location in path) {
				Tile tile = map.tileAt(location.X, location.Y);
				if (tile == Tile.NONE) {
					Serilog.Log.Warning("Dropping the path of unit {Id}, which leaves the map at ({X}, {Y})", id, location.X, location.Y);
					return null;
				}
				tiles.Enqueue(tile);
			}
			return new TilePath(tiles.Last(), tiles);
		}
	}
}
