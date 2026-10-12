using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// The choices made when setting up a game are remembered for the next
// setup and for Quick Start.
public class NewGameChoicesTest {
	private static readonly Lazy<SaveGame> basicRuleset = new(() => SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")).GetSave());

	// The ruleset a new game starts from, without a map or players.
	private static SaveGame Ruleset() {
		return basicRuleset.Value.Clone();
	}

	private static Dictionary<string, string> Written(NewGameChoices choices) {
		Dictionary<string, string> values = [];
		choices.Write((key, value) => {
			if (value == null) {
				values.Remove(key);
			} else {
				values[key] = value;
			}
		});
		return values;
	}

	private static NewGameChoices ReadBack(Dictionary<string, string> values) {
		return NewGameChoices.Read(key => values.GetValueOrDefault(key));
	}

	private static NewGameChoices EveryChoiceMade() {
		return new NewGameChoices {
			WorldSize = "Large",
			Barbarians = BarbarianActivity.Raging,
			Landform = WorldCharacteristics.Landform.Archipelago,
			OceanCoverage = WorldCharacteristics.OceanCoverage.Percent_60,
			Climate = WorldCharacteristics.Climate.Wet,
			Temperature = WorldCharacteristics.Temperature.Cool,
			Age = WorldCharacteristics.Age.Billion_5,
			Civilization = "Rome",
			Difficulty = "Monarch",
			PlayerName = "Ann; #1 | the first",
			Opponents = ["Babylon", NewGameChoices.Random, "Egypt", "Greece"],
			HotseatSlots = new() { [2] = "Bob%20", [3] = "" },
			LanSlots = [0, 1],
			ConquestVictory = false,
			DominationVictory = true,
			SpaceRaceVictory = false,
			DiplomaticVictory = true,
			CulturalVictory = false,
			TurnLimit = true,
			ShowScoreboard = false,
			CoreCitiesFreeOfCorruption = true,
			AcceleratedProduction = true,
			CultureFlips = false,
			ScientificLeaders = false,
			CorruptionRate = 1.25f,
		};
	}

	[Fact]
	public void EveryChoiceIsReadBackAsSaved() {
		NewGameChoices read = ReadBack(Written(EveryChoiceMade()));
		NewGameChoices made = EveryChoiceMade();

		Assert.Equal(made.WorldSize, read.WorldSize);
		Assert.Equal(made.Barbarians, read.Barbarians);
		Assert.Equal(made.Landform, read.Landform);
		Assert.Equal(made.OceanCoverage, read.OceanCoverage);
		Assert.Equal(made.Climate, read.Climate);
		Assert.Equal(made.Temperature, read.Temperature);
		Assert.Equal(made.Age, read.Age);
		Assert.Equal(made.Civilization, read.Civilization);
		Assert.Equal(made.Difficulty, read.Difficulty);
		Assert.Equal(made.PlayerName, read.PlayerName);
		Assert.Equal(made.Opponents, read.Opponents);
		Assert.Equal(made.HotseatSlots, read.HotseatSlots);
		Assert.Equal(made.LanSlots, read.LanSlots);
		Assert.Equal(made.ConquestVictory, read.ConquestVictory);
		Assert.Equal(made.DominationVictory, read.DominationVictory);
		Assert.Equal(made.SpaceRaceVictory, read.SpaceRaceVictory);
		Assert.Equal(made.DiplomaticVictory, read.DiplomaticVictory);
		Assert.Equal(made.CulturalVictory, read.CulturalVictory);
		Assert.Equal(made.TurnLimit, read.TurnLimit);
		Assert.Equal(made.ShowScoreboard, read.ShowScoreboard);
		Assert.Equal(made.CoreCitiesFreeOfCorruption, read.CoreCitiesFreeOfCorruption);
		Assert.Equal(made.AcceleratedProduction, read.AcceleratedProduction);
		Assert.Equal(made.CultureFlips, read.CultureFlips);
		Assert.Equal(made.ScientificLeaders, read.ScientificLeaders);
		Assert.Equal(made.CorruptionRate, read.CorruptionRate);
	}

	[Fact]
	public void RandomChoicesAreRememberedAsRandom() {
		NewGameChoices read = ReadBack(Written(new NewGameChoices { WorldSize = NewGameChoices.Random, Barbarians = null }));
		Assert.Equal(NewGameChoices.Random, read.WorldSize);
		Assert.Null(read.Barbarians);
	}

	[Fact]
	public void NothingSavedGivesTheDefaults() {
		NewGameChoices read = ReadBack([]);
		Assert.Null(read.WorldSize);
		Assert.Equal(BarbarianActivity.Roaming, read.Barbarians);
		Assert.Equal(WorldCharacteristics.Landform.Pangaea, read.Landform);
		Assert.Equal(WorldCharacteristics.OceanCoverage.Percent_70, read.OceanCoverage);
		Assert.Null(read.Civilization);
		Assert.Empty(read.Opponents);
		Assert.Empty(read.HotseatSlots);
		Assert.Null(read.TurnLimit);
		Assert.Null(read.CorruptionRate);

		SaveGame save = Ruleset();
		GameSetup setup = read.QuickStartGame(save, 42, new Random(1));
		Assert.Equal("Netherlands", setup.playerCivilization.name);
		Assert.Equal("Regent", setup.difficulty.Name);
		Assert.Equal(NewGameChoices.DefaultWorldSize(save.WorldSizes).name, setup.worldCharacteristics.worldSize.name);
		Assert.All(setup.opponents, o => Assert.True(o.isRandom));
		Assert.True(setup.showScoreboard);
		Assert.False(setup.victoryConditions.UseTurnLimit);
		Assert.Null(setup.acceleratedProduction);
		Assert.Null(setup.corruptionRate);
	}

	[Fact]
	public void ValuesThatCantBeReadAreIgnored() {
		NewGameChoices read = ReadBack(new() {
			["Landform"] = "Donut",
			["Age"] = "7",
			["BarbarianActivity"] = "",
			["TurnLimit"] = "maybe",
			["CorruptionRate"] = "99",
			["HotseatSlots"] = "x|2",
			["HotseatNames"] = "Ann|Bob",
		});
		Assert.Equal(WorldCharacteristics.Landform.Pangaea, read.Landform);
		Assert.Equal(WorldCharacteristics.Age.Billion_4, read.Age);
		Assert.Equal(BarbarianActivity.Roaming, read.Barbarians);
		Assert.Null(read.TurnLimit);
		Assert.Equal(Rules.MaxCorruptionRate, read.CorruptionRate);
		Assert.Equal(new SortedDictionary<int, string> { [2] = "Bob" }, read.HotseatSlots);
	}

	// The opponents as the settings file kept them before more was
	// remembered.
	[Fact]
	public void OpponentsSavedTheOldWayAreRead() {
		NewGameChoices read = ReadBack(new() { ["Opponents"] = "Babylon|Random|Egypt" });
		Assert.Equal(["Babylon", NewGameChoices.Random, "Egypt"], read.Opponents);
	}

	// Civilizations, world sizes and difficulties only one ruleset has fall
	// back to the defaults in the other.
	[Fact]
	public void ChoicesTheRulesetLacksFallBack() {
		SaveGame save = Ruleset();
		NewGameChoices choices = new() {
			Civilization = "Atlantis",
			Difficulty = "Impossible",
			WorldSize = "Galactic",
			Opponents = ["Atlantis", "Rome", "Rome", "Netherlands", "Barbarians"],
		};

		Civilization player = choices.ChosenCivilization(save.Civilizations);
		Assert.Equal("Netherlands", player.name);
		Assert.False(player.isBarbarian);
		Assert.Equal("Regent", choices.ChosenDifficulty(save.Difficulties).Name);
		Assert.Equal(NewGameChoices.DefaultWorldSize(save.WorldSizes), choices.ChosenWorldSize(save.WorldSizes, new Random(1)));

		// Unknown civs, ones already taken and the player's own are random.
		Assert.Equal([null, "Rome", null, null, null, null], choices.ChosenOpponents(6, save.Civilizations, player.name));
	}

	[Fact]
	public void QuickStartPlaysTheSameSetupOnANewMap() {
		SaveGame save = Ruleset();
		NewGameChoices choices = EveryChoiceMade();
		choices.WorldSize = save.WorldSizes[1].name;
		Assert.True(save.WorldSizes[1].numberOfCivs >= 5);
		choices.Difficulty = save.Difficulties[2].Name;

		GameSetup first = choices.QuickStartGame(save, 1111, new Random(1), "invalid maps");
		GameSetup second = choices.QuickStartGame(Ruleset(), 2222, new Random(2));

		foreach (GameSetup setup in new[] { first, second }) {
			WorldCharacteristics world = setup.worldCharacteristics;
			Assert.Equal(save.WorldSizes[1].name, world.worldSize.name);
			Assert.Equal(BarbarianActivity.Raging, world.barbarianActivity);
			Assert.Equal(WorldCharacteristics.Landform.Archipelago, world.landform);
			Assert.Equal(WorldCharacteristics.OceanCoverage.Percent_60, world.oceanCoverage);
			Assert.Equal(WorldCharacteristics.Climate.Wet, world.climate);
			Assert.Equal(WorldCharacteristics.Temperature.Cool, world.temperature);
			Assert.Equal(WorldCharacteristics.Age.Billion_5, world.age);
			Assert.Equal("Rome", setup.playerCivilization.name);
			Assert.Equal(save.Difficulties[2].Name, setup.difficulty.Name);

			// Slots 0 and 1 were LAN seats, played by the computer now; the
			// civ chosen for slot 1 was Random. Slots 2 and 3 are hotseat.
			int slots = world.worldSize.numberOfCivs - 1;
			Assert.Equal(slots - 2, setup.opponents.Count);
			Assert.Equal("Babylon", setup.opponents[0].Name);
			Assert.False(setup.opponents[0].isRandom);
			Assert.True(setup.opponents[1].isRandom);
			Assert.All(setup.opponents.Skip(2), o => Assert.True(o.isRandom));
			Assert.Equal(2, setup.hotseatPlayers.Count);
			Assert.Equal("Egypt", setup.hotseatPlayers[0].civilization.name);
			Assert.Equal("Bob%20", setup.hotseatPlayers[0].name);
			Assert.Equal("Greece", setup.hotseatPlayers[1].civilization.name);
			Assert.Equal("Player 3", setup.hotseatPlayers[1].name);
			Assert.Equal("Ann; #1 | the first", setup.playerName);

			Assert.False(setup.victoryConditions.AllowConquestVictory);
			Assert.True(setup.victoryConditions.AllowDominationVictory);
			Assert.False(setup.victoryConditions.AllowSpaceRaceVictory);
			Assert.True(setup.victoryConditions.AllowDiplomaticVictory);
			Assert.False(setup.victoryConditions.AllowCulturalVictory);
			Assert.True(setup.victoryConditions.UseTurnLimit);
			Assert.Equal(save.VictoryConditions.DominationTerritoryPercent, setup.victoryConditions.DominationTerritoryPercent);
			Assert.False(setup.showScoreboard);
			Assert.True(setup.coreCitiesFreeOfCorruption);
			Assert.True(setup.acceleratedProduction);
			Assert.False(setup.allowCultureFlips);
			Assert.False(setup.scientificLeaders);
			Assert.Equal(1.25f, setup.corruptionRate);
		}
		Assert.Equal(1111, first.worldCharacteristics.mapSeed);
		Assert.Equal(2222, second.worldCharacteristics.mapSeed);
		Assert.Equal("invalid maps", first.invalidMapsDirectory);
	}

	[Fact]
	public void QuickStartMakesRandomChoicesAgain() {
		SaveGame save = Ruleset();
		NewGameChoices choices = new() { WorldSize = NewGameChoices.Random, Barbarians = null };
		HashSet<string> sizes = [];
		HashSet<BarbarianActivity> barbarians = [];
		Random random = new(7);
		for (int i = 0; i < 100; ++i) {
			GameSetup setup = choices.QuickStartGame(save, i, random);
			sizes.Add(setup.worldCharacteristics.worldSize.name);
			barbarians.Add(setup.worldCharacteristics.barbarianActivity);
		}
		Assert.Equal(save.WorldSizes.Select(w => w.name).ToHashSet(), sizes);
		Assert.Equal(Enum.GetValues<BarbarianActivity>().ToHashSet(), barbarians);
	}

	// A Quick Start game is generated with the civilizations chosen.
	[Fact]
	public void QuickStartGamesHaveTheCivilizationsChosen() {
		SaveGame save = Ruleset();
		WorldSize smallest = save.WorldSizes.OrderBy(w => w.width * w.height).First();
		NewGameChoices choices = new() {
			WorldSize = smallest.name,
			Civilization = "Rome",
			Opponents = ["Babylon", "Egypt"],
			HotseatSlots = new() { [1] = "Bob" },
		};
		choices.QuickStartGame(save, 12345, new Random(1)).Populate(save);

		List<SavePlayer> players = save.Players.Where(p => !save.Civilizations.Find(c => c.name == p.civilization).isBarbarian).ToList();
		Assert.Equal(smallest.numberOfCivs, players.Count);
		Assert.Contains(players, p => p.human && p.civilization == "Rome");
		Assert.Contains(players, p => p.human && p.civilization == "Egypt" && p.name == "Bob");
		Assert.Contains(players, p => !p.human && p.civilization == "Babylon");
		Assert.Equal(12345, save.Seed);
	}
}
