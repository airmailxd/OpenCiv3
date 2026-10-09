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
	public bool coreCitiesFreeOfCorruption { get; init; } = false;
	// Null keeps the setting the scenario or ruleset came with.
	public bool? acceleratedProduction { get; init; } = null;

	ID.Factory ids;

	public void Populate(SaveGame save) {
		save.GameDifficulty = difficulty;

		save.VictoryConditions = victoryConditions;
		save.Rules.ShowScoreboard = showScoreboard;
		save.Rules.CoreCitiesFreeOfCorruption = coreCitiesFreeOfCorruption;
		if (acceleratedProduction.HasValue) {
			save.Rules.AcceleratedProduction = acceleratedProduction.Value;
		}

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

		// Pick the human players' civilizations, the ones they chose or a
		// random one nobody picked, then the opponents'.
		HashSet<string> taken = new();
		List<HotseatPlayer> humans = HumanPlayers().ToList();
		foreach (HotseatPlayer human in humans.Where(h => h.civilization != null)) {
			if (!taken.Add(human.civilization.name)) {
				throw new ArgumentException($"{human.civilization.name} was picked by more than one human player");
			}
		}
		List<PlannedPlayer> planned = new();
		foreach (HotseatPlayer human in humans) {
			Civilization civ = human.civilization;
			if (civ == null) {
				string name = RandomCivilization(save, rand, taken);
				taken.Add(name);
				civ = save.Civilizations.Find(c => c.name == name);
			}
			planned.Add(new PlannedPlayer { civ = civ, isHuman = true, name = human.name, isRandom = human.civilization == null });
		}

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
			planned.Add(new PlannedPlayer { civ = civ, isHuman = false, isRandom = isRandom });
		}

		List<SaveTile> starts = AssignStartingLocations(save, rand, planned, taken);
		for (int i = 0; i < planned.Count; ++i) {
			AddPlayer(save, planned[i].civ, planned[i].isHuman, planned[i].name, i < starts.Count ? starts[i] : null);
		}
	}

	private class PlannedPlayer {
		public Civilization civ;
		public bool isHuman;
		public string name;
		// Whether the civilization was picked at random, so it may be
		// swapped for another.
		public bool isRandom;
	}

	private static bool IsSeafaring(Civilization civ) {
		return civ.traits.Contains(Civilization.Trait.Seafaring);
	}

	// Pairs each planned player with a starting location, in order.
	// Seafaring civs must start next to the sea: if one would start inland,
	// it swaps starts with a civ that isn't seafaring but has a coastal one.
	// If there are more seafaring civs than coastal starts, seafaring civs
	// that were picked at random are swapped for other civs first.
	private static List<SaveTile> AssignStartingLocations(SaveGame save, Random rand, List<PlannedPlayer> planned, HashSet<string> taken) {
		List<SaveTile> starts = save.Map.startingLocations.Take(planned.Count).ToList();
		HashSet<SaveTile> coastal = starts.Where(t => IsNextToSea(save.Map, t)).ToHashSet();

		int seafaring = planned.Take(starts.Count).Count(p => IsSeafaring(p.civ));
		foreach (PlannedPlayer p in planned.Take(starts.Count).Where(p => p.isRandom && IsSeafaring(p.civ)).ToList()) {
			if (seafaring <= coastal.Count) {
				break;
			}
			List<Civilization> others = save.Civilizations.Skip(1)
				.Where(c => !c.isBarbarian && !taken.Contains(c.name) && !IsSeafaring(c)).ToList();
			if (others.Count == 0) {
				break;
			}
			Civilization replacement = others[rand.Next(others.Count)];
			taken.Remove(p.civ.name);
			taken.Add(replacement.name);
			p.civ = replacement;
			--seafaring;
		}

		for (int i = 0; i < starts.Count; ++i) {
			if (!IsSeafaring(planned[i].civ) || coastal.Contains(starts[i])) {
				continue;
			}
			List<int> swaps = Enumerable.Range(0, starts.Count)
				.Where(j => coastal.Contains(starts[j]) && !IsSeafaring(planned[j].civ)).ToList();
			if (swaps.Count == 0) {
				log.Warning($"No coastal start left for seafaring {planned[i].civ.name}");
				continue;
			}
			int j = swaps[rand.Next(swaps.Count)];
			(starts[i], starts[j]) = (starts[j], starts[i]);
		}
		return starts;
	}

	// Whether any of the eight tiles around the tile is salt water.
	private static bool IsNextToSea(SaveMap map, SaveTile tile) {
		(int dx, int dy)[] around = { (1, -1), (1, 1), (-1, 1), (-1, -1), (2, 0), (-2, 0), (0, 2), (0, -2) };
		foreach ((int dx, int dy) in around) {
			int x = tile.X + dx;
			int y = tile.Y + dy;
			if (map.wrapHorizontally) {
				x = (x % map.tilesWide + map.tilesWide) % map.tilesWide;
			}
			SaveTile n = map.tiles.Find(t => t.X == x && t.Y == y);
			if (n != null && !n.isFreshWater && n.baseTerrain is "coast" or "sea" or "ocean") {
				return true;
			}
		}
		return false;
	}

	private static string RandomCivilization(SaveGame save, Random rand, HashSet<string> taken) {
		// The first civilization is the barbarians'. With every other one
		// taken, there is nothing to pick from.
		if (save.Civilizations.Skip(1).All(c => taken.Contains(c.name))) {
			throw new ArgumentException($"There are more players than the {save.Civilizations.Count - 1} civilizations to play");
		}
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

	private SavePlayer AddPlayer(SaveGame save, Civilization civ, bool isHuman, string name = null, SaveTile startingTile = null) {
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

		startingTile ??= save.Map.startingLocations[save.Players.Count - 2];
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
