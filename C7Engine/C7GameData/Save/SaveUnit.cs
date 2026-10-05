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

		public SaveUnit() { }

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

			internal Lookups(List<UnitPrototype> prototypes, List<ExperienceLevel> experienceLevels, List<Player> players, List<Terraform> terraforms) {
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
			}

			internal static T Find<K, T>(Dictionary<K, T> dict, K key) where T : class {
				return key is not null && dict.TryGetValue(key, out T value) ? value : null;
			}
		}

		public MapUnit ToMapUnit(List<UnitPrototype> prototypes, List<ExperienceLevel> experienceLevels, List<Player> players, List<Terraform> terraforms, GameMap map) {
			return ToMapUnit(new Lookups(prototypes, experienceLevels, players, terraforms), map);
		}

		internal MapUnit ToMapUnit(Lookups lookups, GameMap map) {
			MapUnit unit = new MapUnit{
				id = id,
				unitType = Lookups.Find(lookups.prototypesByName, prototype),
				experienceLevelKey = experience,
				experienceLevel = Lookups.Find(lookups.experienceLevelsByKey, experience),
				owner = Lookups.Find(lookups.playersById, owner),
				location = map.tileAt(currentLocation.X, currentLocation.Y),
				loadedOnUnitId = loadedOnUnitId,
				previousLocation = currentLocation.X == - 1 ? Tile.NONE : map.tileAt(previousLocation.X, previousLocation.Y),
				hitPointsRemaining = hitPointsRemaining,
				movementPoints = new MovementPoints(),
				isFortified = action == "fortified",
				isSentried = action == "sentry" || action == "sentryEnemyOnly",
				sentryEnemyOnly = action == "sentryEnemyOnly",
				isAutomated = isAutomated,
				heldWithoutMoving = heldWithoutMoving,
				hasAttackedThisTurn = hasAttackedThisTurn,
				facingDirection = facingDirection,
				WorkerProgressTowardsJob = WorkerProgressTowardsJob,
				WorkerJob = WorkerJob == null ? null : Lookups.Find(lookups.terraformsById, WorkerJob)
			};
			unit.location.unitsOnTile.Add(unit);
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
	}
}
