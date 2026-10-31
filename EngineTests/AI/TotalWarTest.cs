using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

// The total-war switch: whom it picks, and that it never makes peace.
public class TotalWarTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;

	public TotalWarTest(SaveGameFixture fixture) {
		this.fixture = fixture;
	}

	private static TotalWar.Candidate Rival(Player p, int distance, float strength, int production) =>
		new(p, distance, strength, production);

	[Fact]
	public void NearestTakesTheClosestRivalWeOutProduce() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		Player[] p = gameData.players.Take(4).ToArray();
		var near = Rival(p[0], 5, 900, 10);
		var nearButRicher = Rival(p[1], 3, 100, 40);
		var far = Rival(p[2], 12, 50, 8);
		Assert.Same(near, TotalWar.Choose([near, nearButRicher, far], 20, weakest: false));
	}

	[Fact]
	public void WeakestTakesTheLowestStrengthWeOutProduce() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		Player[] p = gameData.players.Take(4).ToArray();
		var near = Rival(p[0], 5, 900, 10);
		var weakFar = Rival(p[2], 12, 50, 8);
		var weakerButRicher = Rival(p[1], 3, 10, 40);
		Assert.Same(weakFar, TotalWar.Choose([near, weakFar, weakerButRicher], 20, weakest: true));
	}

	[Fact]
	public void NoVictimWhenWeOutProduceNone() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		Assert.Null(TotalWar.Choose([Rival(gameData.players[0], 3, 10, 40)], 20, weakest: false));
	}

	[Fact]
	public void TheVictimGetsNoPeaceButOthersDo() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player ai = gameData.players.First(p => !p.isHuman && !p.isBarbarians);
		Player victim = gameData.players.First(p => p != ai && !p.isBarbarians && !p.IsIndependent);
		Player other = gameData.players.First(p => p != ai && p != victim && !p.isBarbarians && !p.IsIndependent);
		AiExperiments.Enable(ai, TotalWar.Switch);
		TotalWar.Begin(ai, victim, gameData.turn);

		Assert.True(TotalWar.RefusesPeace(ai, victim));
		Assert.False(TotalWar.RefusesPeace(ai, other));
		Assert.False(PeaceAI.WouldAcceptPeace(ai, victim, new TradeOffer { partOfPeaceTreaty = true }, new TradeOffer { partOfPeaceTreaty = true }, 1000, 0));
		Assert.Null(PeaceAI.BuildPeaceOffer(gameData, ai, victim));
	}

	[Fact]
	public void WithoutTheSwitchPeaceIsNotRefused() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player ai = gameData.players.Last(p => !p.isHuman && !p.isBarbarians && !p.IsIndependent);
		Player victim = gameData.players.First(p => p != ai && !p.isBarbarians && !p.IsIndependent);
		Assert.False(TotalWar.RefusesPeace(ai, victim));
	}
}
