using System.Linq;
using System.Text.RegularExpressions;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// An AI gets the difficulty's extra start units once, on the first turn it
// has a city, even if it founded its capital after turn 1 (compendium 08
// section 1.1: the AI gets them "at start").
public class AiStartUnitsTest : IClassFixture<SaveGameFixture>, System.IDisposable {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;
	private readonly Player ai;
	// The difficulty may be shared with the fixture's save, so it is put
	// back afterwards.
	private readonly (int, int, int, int) difficultyBefore;

	public AiStartUnitsTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		Difficulty d = gameData.gameDifficulty;
		difficultyBefore = (d.NumberOfAIDefensiveStartingUnits, d.NumberOfAIOffensiveStartingUnits, d.ExtraStartUnit1, d.ExtraStartUnit2);
		gameData.gameDifficulty.NumberOfAIDefensiveStartingUnits = 2;
		gameData.gameDifficulty.NumberOfAIOffensiveStartingUnits = 1;
		gameData.gameDifficulty.ExtraStartUnit1 = 0;
		gameData.gameDifficulty.ExtraStartUnit2 = 0;
		ai = gameData.players.First(p => !p.isBarbarians && !p.isHuman && p.cities.Count == 0 && p.units.Any(u => u.unitType.isSettler));
	}

	public void Dispose() {
		Difficulty d = gameData.gameDifficulty;
		(d.NumberOfAIDefensiveStartingUnits, d.NumberOfAIOffensiveStartingUnits, d.ExtraStartUnit1, d.ExtraStartUnit2) = difficultyBefore;
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private void FoundCity() {
		CityInteractions.BuildCity(ai.units.First(u => u.unitType.isSettler).location, ai, ai.GetNextCityName());
	}

	[Fact]
	public void AnAiThatFoundsItsCapitalLateStillGetsTheUnitsOnce() {
		gameData.turn = 1;
		ai.MaybeSpawnBonusUnits(gameData);
		Assert.False(ai.receivedStartUnits);

		gameData.turn = 4;
		FoundCity();
		int before = ai.units.Count;
		ai.MaybeSpawnBonusUnits(gameData);
		Assert.True(ai.receivedStartUnits);
		Assert.Equal(before + 3, ai.units.Count);

		gameData.turn = 5;
		ai.MaybeSpawnBonusUnits(gameData);
		Assert.Equal(before + 3, ai.units.Count);
	}

	[Fact]
	public void HumansGetNone() {
		ai.isHuman = true;
		FoundCity();
		int before = ai.units.Count;
		gameData.turn = 1;
		ai.MaybeSpawnBonusUnits(gameData);
		Assert.Equal(before, ai.units.Count);
	}

	[Fact]
	public void TheFlagSurvivesSaveAndLoad() {
		FoundCity();
		gameData.turn = 1;
		ai.MaybeSpawnBonusUnits(gameData);

		C7GameData.GameData loaded = SaveGame.FromGameData(gameData).ToGameData(fixture.behaviors);

		Player again = loaded.players.Single(p => p.id == ai.id);
		Assert.True(again.receivedStartUnits);
		int before = again.units.Count;
		loaded.turn = 2;
		again.MaybeSpawnBonusUnits(loaded);
		Assert.Equal(before, again.units.Count);
	}

	// A save from before the flag was saved: an AI with cities after turn 1
	// had its units then, and one without cities has yet to get them.
	private C7GameData.GameData LoadOldSave(int turn) {
		gameData.turn = turn;
		string json = System.Text.Encoding.UTF8.GetString(SaveGame.FromGameData(gameData).ToCompactJSON());
		string oldJson = Regex.Replace(json, @"""receivedStartUnits"":(true|false|null),?", "");
		Assert.DoesNotContain("receivedStartUnits", oldJson);
		return SaveGame.LoadFromJSON(oldJson).ToGameData(fixture.behaviors);
	}

	[Fact]
	public void OldSavesDoNotHandOutTheUnitsAgain() {
		FoundCity();
		C7GameData.GameData loaded = LoadOldSave(10);

		Player again = loaded.players.Single(p => p.id == ai.id);
		Assert.True(again.receivedStartUnits);
		int before = again.units.Count;
		again.MaybeSpawnBonusUnits(loaded);
		Assert.Equal(before, again.units.Count);
	}

	[Fact]
	public void OldSavesStillGiveTheUnitsToAnAiWithoutCities() {
		C7GameData.GameData loaded = LoadOldSave(10);

		Assert.False(loaded.players.Single(p => p.id == ai.id).receivedStartUnits);
	}
}
