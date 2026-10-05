using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;
using C7GameData.Save;
using Serilog;

namespace C7Engine;

public struct SelectedOpponent {
	public bool isRandom;
	public string Name;
}

// An additional human player: sharing the computer in a hotseat game, or
// joining over the LAN.
public struct HotseatPlayer {
	// Null for a random civilization no other human has.
	public Civilization civilization;
	// The person's name, or null if none was given.
	public string name;
}

public class GameSetup {
	private static ILogger log = Log.ForContext<GameSetup>();

	public Civilization playerCivilization { get; init; }
	// The first human player's name, or null if none was given.
	public string playerName { get; init; }
	// Additional human players, sharing this computer in a hotseat game or
	// joining over the LAN. They take their turns after the player above, in
	// list order. A LAN host fills these in once the guests have chosen.
	public List<HotseatPlayer> hotseatPlayers { get; set; } = [];
	public Difficulty difficulty { get; init; }
	public WorldCharacteristics worldCharacteristics { get; init; }
	public List<SelectedOpponent> opponents { get; init; } = [];
	public VictoryConditions victoryConditions { get; set; }
	public bool showScoreboard { get; init; } = true;

	ID.Factory ids;

	public void Populate(SaveGame save) {
		save.GameDifficulty = difficulty;

		save.VictoryConditions = victoryConditions;
		save.Rules.ShowScoreboard = showScoreboard;

		if (save.Map.tiles.Count == 0) {
			log.Information("Starting map generation");
			save.Map = new SaveMap(MapGenerator.GenerateMap(worldCharacteristics));
			save.Seed = worldCharacteristics.mapSeed;
			log.Information("Done with map generation");
		}

		if (save.Players.Count == 0) {
			ids = new(save);
			PopulatePlayers(save);
		}
	}

	private void PopulatePlayers(SaveGame save) {
		Random rand = new(worldCharacteristics.mapSeed + 0x531);

		// Add barbarian
		SavePlayer barbarians = AddPlayer(save, save.Civilizations.Find(c => c.isBarbarian), isHuman: false);
		save.BarbarianInfo.barbarianActivity = worldCharacteristics.barbarianActivity;
		AddCampDefenders(save, barbarians);

		// TODO: There is an option called "Culturally Linked Start Loc."
		// which (if on) puts players with the same culture group near each other

		// Add the human players, with the civilizations they picked; the
		// others get a random one nobody picked.
		HashSet<string> taken = new();
		List<HotseatPlayer> humans = HumanPlayers().ToList();
		foreach (HotseatPlayer human in humans.Where(h => h.civilization != null)) {
			if (!taken.Add(human.civilization.name)) {
				throw new ArgumentException($"{human.civilization.name} was picked by more than one human player");
			}
		}
		foreach (HotseatPlayer human in humans) {
			Civilization civ = human.civilization;
			if (civ == null) {
				string name = RandomCivilization(save, rand, taken);
				taken.Add(name);
				civ = save.Civilizations.Find(c => c.name == name);
			}
			AddPlayer(save, civ, isHuman: true, human.name);
		}

		// Add the opponents.

		foreach (SelectedOpponent opponent in opponents) {
			bool isRandom = opponent.isRandom;
			string selectedName = opponent.Name;

			if (taken.Contains(opponent.Name)) {
				isRandom = true;
			}

			if (isRandom) {
				selectedName = RandomCivilization(save, rand, taken);
			}
			taken.Add(selectedName);

			Civilization civ = save.Civilizations.Find(x => x.name == selectedName);
			AddPlayer(save, civ, isHuman: false);
		}
	}

	private static string RandomCivilization(SaveGame save, Random rand, HashSet<string> taken) {
		string name;
		do {
			name = save.Civilizations[rand.Next(1, save.Civilizations.Count)].name;
		} while (taken.Contains(name));
		return name;
	}

	// The IDs the human players will have once Populate has added them to a
	// new game, in turn order: the first is playerCivilization's, then one
	// for each of hotseatPlayers. They come right after the barbarians'. A
	// LAN host uses these to seat guests before the game is created.
	public static List<ID> HumanPlayerIDs(int count) {
		ID.Factory ids = new();
		ids.CreateID("Player");
		return Enumerable.Range(0, count).Select(_ => ids.CreateID("Player")).ToList();
	}

	private IEnumerable<HotseatPlayer> HumanPlayers() {
		return hotseatPlayers.Prepend(new HotseatPlayer { civilization = playerCivilization, name = playerName });
	}

	// Every barbarian camp starts the game guarded by a basic barbarian unit
	// (a Warrior in the standard rules).
	private void AddCampDefenders(SaveGame save, SavePlayer barbarians) {
		string defender = save.BarbarianInfo.basicBarbarianUnit;
		if (defender == null) {
			return;
		}

		foreach (SaveTile tile in save.Map.tiles.Where(t => t.features.Contains(Tile.TileOverlays.BARBARIAN_CAMP))) {
			AddUnit(save, barbarians, defender, new TileLocation(tile.X, tile.Y));
		}
	}

	private SavePlayer AddPlayer(SaveGame save, Civilization civ, bool isHuman, string name = null) {
		SavePlayer player = new() {
			isBarbarian = civ.isBarbarian,
			human = isHuman,
			name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
			id = ids.CreateID("Player"),
			primaryColorIndex = civ.primaryColorIndex,
			secondaryColorIndex = civ.secondaryColorIndex,
			civilization = civ.name,
			knownTechs = new HashSet<ID>(civ.startingTechs),
			// TODO: stop hardcoding this
			eraCivilopediaName = "ERAS_Ancient_Times",
			// TODO: load this from the rules
			gold = 10,
			governmentId = worldCharacteristics.defaultGovernment.id,
		};
		save.Players.Add(player);

		if (civ.isBarbarian) {
			player.canBePicked = false;
			return player;
		}

		SaveTile startingTile = save.Map.startingLocations[save.Players.Count - 2];
		TileLocation startingLocation = new TileLocation(startingTile.X, startingTile.Y);

		AddUnit(save, player, save.Rules.StartUnitType1, startingLocation);
		AddUnit(save, player, save.Rules.StartUnitType2, startingLocation);
		if (civ.traits.Contains(Civilization.Trait.Expansionist)) {
			AddUnit(save, player, save.Rules.ScoutUnitType, startingLocation);
		}
		return player;
	}

	private void AddUnit(SaveGame save, SavePlayer player, string unitType, TileLocation location) {
		var defaultExpLevelKey = save.DefaultExperienceLevel;

		var defaultExpLevel = save.ExperienceLevels.First(e => e.key == defaultExpLevelKey);
		var barbExpLevel = save.ExperienceLevels.First(e => e.baseHitPoints == save.BarbarianInfo.maxHitpoints);

		var proto = save.UnitPrototypes.First(p => p.name == unitType);

		SaveUnit unit = new() {
			id = ids.CreateID(unitType),
			name = unitType,
			nationality = player.civilization,
			prototype = unitType,
			owner = player.id,
			previousLocation = new TileLocation(-1, -1),
			currentLocation = location,

			hitPointsRemaining = (player.isBarbarian ? barbExpLevel.baseHitPoints : defaultExpLevel.baseHitPoints) + proto.hpBonus,
			movePointsRemaining = proto.movement,
			experience = player.isBarbarian ? barbExpLevel.key : defaultExpLevel.key,

			facingDirection = TileDirection.SOUTHEAST,
		};
		save.Units.Add(unit);
	}
}
