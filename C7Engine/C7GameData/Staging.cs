using System.Collections.Generic;
using System.Linq;
using C7GameData;
using Serilog;

namespace C7GameData {
	// The saved state of the turn's Staging (see C7Engine.Staging). Players
	// are stored by their ID strings so the state serializes as it is.
	public class StagingState {
		// The turn being staged, or -1 if none has been.
		public int turn = -1;

		// The name of the unit type every civ is given this turn.
		public string unitType;

		// The IDs of the humans yet to choose a city for theirs. No one moves
		// until it's empty.
		public List<string> waiting = new();
	}
}

namespace C7Engine {
	// Eternal War's Staging (owner's design, 2026-10-22). At the start of
	// every turn after the first, before anyone moves, every civ is given a
	// unit, the same type for all. It's chosen at random from the units of
	// the age of the most advanced civ, and may be one a civ can't build yet,
	// but is never a unique unit. Each player chooses which of their cities
	// it appears in: the AIs at once, the humans from a list of their
	// cities, and no one moves until every human has chosen. A ship goes to
	// a coastal city, or for a civ with none, to the sea nearest the city
	// chosen.
	public static class Staging {
		private static readonly ILogger log = Log.ForContext(typeof(Staging));

		// Whether some human is yet to choose, so no one may move.
		public static bool InProgress(GameData gameData) {
			return gameData.rules?.StagingUnits == true && gameData.staging?.waiting.Count > 0;
		}

		public static bool IsWaitingFor(GameData gameData, Player player) {
			return player != null && gameData.staging?.waiting.Contains(player.id.ToString()) == true;
		}

		// The unit type every civ is given this turn, or null if none is.
		public static UnitPrototype UnitType(GameData gameData) {
			string name = gameData.staging.unitType;
			return name == null ? null : gameData.unitPrototypes.Find(p => p.name == name);
		}

		// The unit types Staging may give: the fighting land and sea units
		// of the most advanced civ's era (by the era of the advance each
		// needs) that every civ in the game could build, which leaves out
		// the unique units. If that era has none, the one before.
		public static List<UnitPrototype> Pool(GameData gameData) {
			List<Player> civs = gameData.players.Where(p => !p.isBarbarians && !p.defeated).ToList();
			int era = civs.Count == 0 ? 0 : civs.Max(p => p.EraIndex());
			for (; era >= 0; --era) {
				List<UnitPrototype> pool = gameData.unitPrototypes
					.Where(p => MayBeGiven(p, civs) && EraUtils.GetEraIndex(p.requiredTech.EraCivilopediaName) == era)
					.ToList();
				if (pool.Count > 0) {
					return pool;
				}
			}
			return [];
		}

		private static bool MayBeGiven(UnitPrototype unit, List<Player> civs) {
			return unit.requiredTech != null && !unit.unproducible
				&& (unit.IsLandUnit() || unit.IsSeaUnit()) && !unit.IsAirUnit()
				&& unit.attack + unit.defense > 0
				&& !unit.isSettler && !unit.isWorker && !unit.isKing && !unit.isLeader && !unit.isArmy
				&& !unit.isNuclearWeapon && !unit.isTacticalMissile && !unit.isCruiseMissile
				&& civs.All(c => unit.IsProducibleBy(c.civilization));
		}

		// Starts the turn's Staging as the turn begins: chooses the unit,
		// places the AIs', and leaves the humans to choose.
		public static void BeginTurn(GameData gameData) {
			StagingState state = gameData.staging ??= new();
			state.waiting.Clear();
			if (gameData.rules?.StagingUnits != true) {
				return;
			}
			List<UnitPrototype> pool = Pool(gameData);
			if (pool.Count == 0) {
				log.Warning("No unit types to give out in Staging on turn {Turn}", gameData.turn);
				return;
			}
			UnitPrototype unit = pool[GameData.rng.Next(pool.Count)];
			state.turn = gameData.turn;
			state.unitType = unit.name;
			log.Information("Staging for turn {Turn}: every civ is given a {Unit}", gameData.turn, unit.name);

			foreach (Player player in gameData.players.Where(p => !p.isBarbarians && !p.defeated && p.cities.Count > 0)) {
				if (player.isHuman) {
					state.waiting.Add(player.id.ToString());
				} else {
					Place(gameData, player, unit, AiChoice(gameData, player, unit));
				}
			}
		}

		// The cities the player may choose for the unit: for a ship, their
		// coastal ones, or all of them if none is.
		public static List<City> Choices(Player player, UnitPrototype unit) {
			if (unit.IsSeaUnit()) {
				List<City> coastal = player.cities.Where(c => c.location.NeighborsOcean()).ToList();
				if (coastal.Count > 0) {
					return coastal;
				}
			}
			return player.cities.ToList();
		}

		// The player's units in the city that could defend it.
		public static List<MapUnit> Garrison(City city) {
			return city.location.unitsOnTile.Where(u => u.owner == city.owner && u.unitType.defense > 0 && !u.unitType.isWorker).ToList();
		}

		// HEURISTIC (Staging isn't Civ3's, so nothing says how an AI would
		// choose): a defender (no stronger in attack than defence) goes to
		// the city with the fewest defenders, and anything else to the city
		// nearest an enemy city, to strike from; each breaks ties by the
		// other.
		public static City AiChoice(GameData gameData, Player player, UnitPrototype unit) {
			List<City> choices = Choices(player, unit);
			List<Tile> enemyCities = gameData.cities
				.Where(c => c.owner != player && PlayerRelationship.AtWar(player, c.owner))
				.Select(c => c.location).ToList();
			int ToEnemy(City city) => enemyCities.Count == 0 ? 0 : enemyCities.Min(t => city.location.DistanceTo(t));
			if (unit.defense >= unit.attack && !unit.IsSeaUnit()) {
				return choices.OrderBy(c => Garrison(c).Count).ThenBy(ToEnemy).First();
			}
			return choices.OrderBy(ToEnemy).ThenBy(c => Garrison(c).Count).First();
		}

		// Puts the player's unit in the city, or a ship the city can't hold
		// on the nearest sea it can sail safely. Returns the unit, or null
		// if there was nowhere for it.
		public static MapUnit Place(GameData gameData, Player player, UnitPrototype unit, City city) {
			Tile tile = city.location;
			if (unit.IsSeaUnit() && !tile.NeighborsOcean()) {
				tile = NearestSea(gameData, player, unit, city.location);
				if (tile == null) {
					log.Warning("No sea for {Player}'s Staging {Unit} near {City}", player, unit.name, city);
					return null;
				}
			}
			MapUnit placed = gameData.SpawnUnit(player, unit, tile);
			player.tileKnowledge.RecomputeActiveTiles();
			log.Information("{Player}'s Staging {Unit} placed at {City}", player, unit.name, city);
			return placed;
		}

		// The salt water tile nearest the given one that the ship can sail
		// safely, with no one else's units on it.
		private static Tile NearestSea(GameData gameData, Player player, UnitPrototype unit, Tile from) {
			bool Safe(Tile t) => !((unit.sinksInOcean && t.baseTerrainType.IsOcean) || (unit.sinksInSea && t.baseTerrainType.IsSea))
				|| player.CanSailSafelyOn(t.baseTerrainType);
			return gameData.map.tiles
				.Where(t => t.baseTerrainType.IsWater && !t.isFreshWater && Safe(t) && t.unitsOnTile.All(u => u.owner == player))
				.OrderBy(t => from.DistanceTo(t))
				.FirstOrDefault();
		}

		// Asks the humans yet to choose. One who can't answer (a LAN guest
		// who has left) or has nowhere left to put the unit is chosen for as
		// an AI would be.
		public static void AskHumans(GameData gameData) {
			foreach (string id in gameData.staging.waiting.ToList()) {
				Player player = gameData.players.Find(p => p.id.ToString() == id);
				if (player == null) {
					gameData.staging.waiting.Remove(id);
					continue;
				}
				AskHuman(gameData, player);
			}
		}

		public static void AskHuman(GameData gameData, Player player) {
			if (!IsWaitingFor(gameData, player)) {
				return;
			}
			UnitPrototype unit = UnitType(gameData);
			if (unit == null || player.defeated || player.cities.Count == 0 || !EngineStorage.IsPlayerReachable(player.id)) {
				ChooseFor(gameData, player);
				return;
			}
			new MsgShowStaging(player, unit.name, Choices(player, unit)).send();
		}

		// The human's choice of city. Returns false if they weren't to choose,
		// or may not choose that city.
		public static bool Answer(GameData gameData, Player player, City city) {
			UnitPrototype unit = UnitType(gameData);
			if (!IsWaitingFor(gameData, player) || unit == null || city?.owner != player || !Choices(player, unit).Contains(city)) {
				return false;
			}
			gameData.staging.waiting.Remove(player.id.ToString());
			Place(gameData, player, unit, city);
			return true;
		}

		// Places a waiting human's unit where an AI would.
		private static void ChooseFor(GameData gameData, Player player) {
			gameData.staging.waiting.Remove(player?.id.ToString());
			UnitPrototype unit = UnitType(gameData);
			if (player != null && unit != null && !player.defeated && player.cities.Count > 0) {
				Place(gameData, player, unit, AiChoice(gameData, player, unit));
			}
		}

		// A game loaded during Staging starts with a human's turn, so the
		// units still waiting for a city are placed where an AI would.
		public static void FinishOnLoad(GameData gameData) {
			if (gameData.staging == null) {
				return;
			}
			foreach (string id in gameData.staging.waiting.ToList()) {
				ChooseFor(gameData, gameData.players.Find(p => p.id.ToString() == id));
			}
		}
	}
}
