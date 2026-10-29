using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Which AI plays each computer civ (AiType) is chosen when setting up a game,
// and kept in the save.
public class AiTypeTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;

	public AiTypeTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private Player AiCiv() => gameData.players.First(p => !p.isHuman && !p.IsIndependent);

	[Fact]
	public void AdvancedSurvivesSaveAndLoad() {
		Player player = AiCiv();
		player.aiType = AiType.Advanced;

		C7GameData.GameData loaded = SaveGame.FromGameData(gameData).ToGameData(fixture.behaviors);

		Player reloaded = loaded.players.Single(p => p.id == player.id);
		Assert.Equal(AiType.Advanced, reloaded.aiType);
		Assert.True(reloaded.IsAdvancedAi);
		Assert.All(loaded.players.Where(p => p.id != player.id), p => Assert.Equal(AiType.Standard, p.aiType));
	}

	[Fact]
	public void AdvancedSurvivesASnapshot() {
		Player player = AiCiv();
		player.aiType = AiType.Advanced;

		C7GameData.GameData loaded = CreateGame.ReplaceWithSnapshot(SaveGame.FromGameData(gameData), fixture.behaviors);

		Assert.Equal(AiType.Advanced, loaded.players.Single(p => p.id == player.id).aiType);
	}

	[Fact]
	public void SavesWithoutAnAiTypeLoadAsStandard() {
		Player player = AiCiv();
		player.aiType = AiType.Advanced;
		string json = System.Text.Encoding.UTF8.GetString(SaveGame.FromGameData(gameData).ToCompactJSON());
		Assert.Contains(@"""aiType"":""advanced""", json);
		string oldJson = Regex.Replace(json, @"""aiType"":""\w+"",?", "");
		Assert.DoesNotContain("aiType", oldJson);

		C7GameData.GameData loaded = SaveGame.LoadFromJSON(oldJson).ToGameData(fixture.behaviors);

		Assert.All(loaded.players, p => Assert.Equal(AiType.Standard, p.aiType));
	}

	[Fact]
	public void StandardIsLeftOutOfTheSave() {
		foreach (Player p in gameData.players) {
			p.aiType = AiType.Standard;
		}
		string json = System.Text.Encoding.UTF8.GetString(SaveGame.FromGameData(gameData).ToCompactJSON());
		Assert.DoesNotContain("aiType", json);
	}

	[Fact]
	public void OnlyComputerCivsPlayAsAdvanced() {
		Player human = gameData.players.First(p => p.isHuman);
		human.aiType = AiType.Advanced;
		Assert.False(human.IsAdvancedAi);

		Player barbarians = gameData.players.First(p => p.isBarbarians);
		barbarians.aiType = AiType.Advanced;
		Assert.False(barbarians.IsAdvancedAi);

		Player ai = AiCiv();
		Assert.False(ai.IsAdvancedAi);
		ai.aiType = AiType.Advanced;
		Assert.True(ai.IsAdvancedAi);
	}

	[Fact]
	public void GameSetupGivesEachOpponentTheAiChosen() {
		SaveGame save = SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")).GetSave();
		WorldCharacteristics wc = new(save) {
			landform = WorldCharacteristics.Landform.Continents,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_60,
			age = WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			barbarianActivity = BarbarianActivity.Roaming,
			worldSize = new WorldSize() { width = 80, height = 80, numberOfCivs = 4, distanceBetweenCivs = 12, techRate = 240, optimalNumberOfCities = 20 },
			mapSeed = 11,
		};
		new GameSetup {
			playerCivilization = save.Civilizations.First(c => !c.isBarbarian),
			difficulty = save.Difficulties.First(d => d.Name == "Regent"),
			worldCharacteristics = wc,
			opponents = [
				new SelectedOpponent { isRandom = true, aiType = AiType.Advanced },
				new SelectedOpponent { isRandom = true },
				new SelectedOpponent { isRandom = true, aiType = AiType.Advanced },
			],
			victoryConditions = new VictoryConditions(),
		}.Populate(save);

		List<SavePlayer> civs = save.Players.Where(p => !p.IsIndependent).ToList();
		Assert.Equal([AiType.Standard, AiType.Advanced, AiType.Standard, AiType.Advanced], civs.Select(p => p.aiType ?? AiType.Standard));

		C7GameData.GameData loaded = SaveGameFixture.HydrateSaveGame(save);
		List<Player> players = loaded.players.Where(p => !p.IsIndependent).ToList();
		Assert.Equal([false, true, false, true], players.Select(p => p.IsAdvancedAi));
	}

	[Fact]
	public void TheAiChosenForEachSlotIsRemembered() {
		NewGameChoices choices = new() { AdvancedAiSlots = [1, 3] };
		Dictionary<string, string> values = [];
		choices.Write((key, value) => {
			if (value != null) {
				values[key] = value;
			}
		});
		NewGameChoices read = NewGameChoices.Read(key => values.GetValueOrDefault(key));

		Assert.Equal([1, 3], read.AdvancedAiSlots);
		Assert.Equal(AiType.Standard, read.AiTypeOf(0));
		Assert.Equal(AiType.Advanced, read.AiTypeOf(1));
	}

	[Fact]
	public void QuickStartPlaysTheAiChosenForEachSlot() {
		SaveGame save = SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")).GetSave();
		NewGameChoices choices = new() { WorldSize = "Tiny", AdvancedAiSlots = [0, 2], HotseatSlots = new() { [1] = "" } };

		GameSetup setup = choices.QuickStartGame(save, 42, new Random(1));

		// Slot 1 is a hotseat player, so the opponents are slots 0, 2, 3...
		Assert.Equal(AiType.Advanced, setup.opponents[0].aiType);
		Assert.Equal(AiType.Advanced, setup.opponents[1].aiType);
		Assert.All(setup.opponents.Skip(2), o => Assert.Equal(AiType.Standard, o.aiType));
	}

	[Fact]
	public void MultiplayerComputerSlotsKeepTheirAi() {
		List<Civilization> civs = SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")).GetSave().Civilizations;
		List<PlayerSlot> slots = [
			new PlayerSlot(SlotKind.Computer, aiType: AiType.Advanced),
			new PlayerSlot(SlotKind.Open, aiType: AiType.Advanced),
			new PlayerSlot(SlotKind.Computer),
		];

		MultiplayerPlan plan = MultiplayerSlots.Plan(MultiplayerGameType.Hotseat, NewGameChoices.Random, slots, civs, new Random(1));
		Assert.Equal([AiType.Advanced, AiType.Standard], plan.opponents.Select(o => o.aiType));

		NewGameChoices choices = new();
		MultiplayerSlots.RememberSlots(choices, MultiplayerGameType.Hotseat, slots);
		Assert.Equal([0, 1], choices.AdvancedAiSlots);
		List<PlayerSlot> again = MultiplayerSlots.SlotsAsLastTime(choices, MultiplayerGameType.Hotseat, 3);
		Assert.Equal(slots.Select(s => s.aiType), again.Select(s => s.aiType));
	}
}
