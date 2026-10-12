using System.Collections.Generic;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using Xunit;

namespace EngineTests.GameData;

// The AI's attitude toward another civ, in the AI Attitude Study's points
// (https://civfanatics.com/civ3/strategy/game-mechanics/ai-attitude-study/).
public class AttitudesTest {
	private readonly C7GameData.GameData gameData = new() {
		gameDifficulty = new Difficulty(),
		rules = new Rules(),
		turn = 50,
	};
	private readonly Player ai;
	private readonly Player human;

	public AttitudesTest() {
		EngineStorage.InitializeGameDataForTests(gameData);
		ai = MakePlayer("player-1", "Germans");
		human = MakePlayer("player-2", "French");
		// In different governments, so that being in the same one counts
		// only where a test says so.
		human.government = new Government() { name = "Monarchy" };
		ai.EnsureRelationshipExists(human);
		human.EnsureRelationshipExists(ai);
	}

	private Player MakePlayer(string id, string civName) {
		Player player = new() {
			id = ID.FromString(id),
			civilization = new Civilization(civName) { noun = civName },
			government = new Government() { name = "Despotism" },
			rules = gameData.rules,
		};
		gameData.players.Add(player);
		return player;
	}

	private int Points() => Attitudes.Points(gameData, ai, human).Value;

	private PlayerRelationship AiView => ai.playerRelationships[human.id];

	private void PlayOn(string difficulty) {
		foreach (string name in new[] { "Chieftain", "Warlord", "Regent", "Monarch", "Emperor", "Demigod", "Deity" }) {
			gameData.difficulties.Add(new Difficulty() { Name = name });
		}
		gameData.gameDifficulty = gameData.difficulties.Find(d => d.Name == difficulty);
	}

	[Theory]
	[InlineData(-11, AttitudeLevel.Gracious)]
	[InlineData(-10, AttitudeLevel.Polite)]
	[InlineData(-1, AttitudeLevel.Polite)]
	[InlineData(0, AttitudeLevel.Cautious)]
	[InlineData(1, AttitudeLevel.Annoyed)]
	[InlineData(10, AttitudeLevel.Annoyed)]
	[InlineData(11, AttitudeLevel.Furious)]
	public void PointsMakeTheStudysLevels(int points, AttitudeLevel level) {
		Assert.Equal(level, Attitudes.LevelOf(points));
	}

	[Fact]
	public void CivsThatHaventMetHaveNoAttitude() {
		Player stranger = MakePlayer("player-3", "Zulus");
		Assert.Null(Attitudes.Level(gameData, ai, stranger));
	}

	// "Chieftain-Warlord: 5 = +2 ... 1 = -1", "Regent-Monarch: 5 = +3 ...
	// 1 = -1", "Emperor-Deity: 5 = +3 ... 1 = 0".
	[Theory]
	[InlineData("Warlord", 2, 2)]
	[InlineData("Warlord", -2, -1)]
	[InlineData("Monarch", 2, 3)]
	[InlineData("Monarch", 0, 1)]
	[InlineData("Monarch", -2, -1)]
	[InlineData("Emperor", 1, 3)]
	[InlineData("Deity", -2, 0)]
	public void AggressionDependsOnTheDifficulty(string difficulty, int aggression, int points) {
		PlayOn(difficulty);
		ai.civilization.aggressionLevel = aggression;
		Assert.Equal(points, Points());
	}

	[Fact]
	public void SharingTheFavoriteGovernmentPleases() {
		ai.civilization.favoriteGovernment = "Despotism";
		human.government = ai.government;
		// -1 for the same government, and -5 as it is the favourite.
		Assert.Equal(-6, Points());
	}

	[Fact]
	public void TheShunnedGovernmentDispleasesOnlyIfTheAiIsntInIt() {
		ai.civilization.shunnedGovernment = "Monarchy";
		Assert.Equal(4, Points());

		ai.government = new Government() { name = "Monarchy" };
		// Now both are in it: only the -1 for the same government.
		Assert.Equal(-1, Points());
	}

	[Fact]
	public void GiftsPleaseAndAreForgottenBitByBit() {
		human.gold = 200;
		Assert.True(ai.ExecuteDeal(gameData, human, new TradeOffer() { gold = 150 }, new TradeOffer()));
		// -1 for each 10 gold, up to -10.
		Assert.Equal(-10, Points());

		gameData.turn += 10;
		// 150 - 10 * 10 = 50 gold remembered.
		Assert.Equal(-5, Points());

		gameData.turn += 10;
		Assert.Equal(0, Points());
	}

	[Fact]
	public void TributeIsNoGift() {
		human.gold = 100;
		Assert.True(ai.ExecuteDeal(gameData, human, new TradeOffer() { gold = 50 }, new TradeOffer(), tribute: true));
		Assert.Equal(0, AiView.giftGold);
	}

	[Fact]
	public void PayingTributePleasesForAWhile() {
		AiView.lastTributePaidTurn = gameData.turn;
		Assert.Equal(-1, Points());

		gameData.turn += Attitudes.TributeMemoryTurns + 1;
		Assert.Equal(0, Points());
	}

	[Fact]
	public void CallingABluffAndCapturingWorkersDispleaseForGood() {
		AiView.tributeBluffsCalled = 1;
		AiView.workersCaptured = 2;
		gameData.turn += 100;
		Assert.Equal(3, Points());
	}

	[Fact]
	public void CapturingAWorkerIsRemembered() {
		Tile tile = new(ID.None("tile"));
		UnitPrototype workerType = new() { name = "Worker" };
		workerType.actions.Add(UnitAction.Automate);
		// A unit is a worker if it can automate and has any terraform; this
		// one's contents don't matter, so it skips the constructor (which
		// needs the game's behaviors).
		workerType.terraformActions.Add((Terraform)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Terraform)));
		MapUnit worker = new(ID.None("unit")) { owner = ai, unitType = workerType, location = tile };
		ai.units.Add(worker);
		tile.unitsOnTile.Add(worker);
		// A city, so that losing the worker doesn't destroy the civ.
		City city = new(new Tile(ID.None("tile")), ai, "Berlin", ID.FromString("city-1"));
		city.residents.Add(new CityResident { city = city });
		ai.cities.Add(city);
		gameData.cities.Add(city);

		gameData.CaptureUnit(worker, human);

		Assert.Equal(1, AiView.workersCaptured);
	}
}
