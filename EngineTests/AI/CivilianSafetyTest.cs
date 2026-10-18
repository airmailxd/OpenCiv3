using System;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using C7GameData.AIData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

// Workers and settlers keep away from enemies they can see, and out of the
// land of civs they're at war with (see AIThreat).
public sealed class CivilianSafetyTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player ai;
	private readonly Player enemy;
	private readonly Tile home;

	public CivilianSafetyTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		EngineStorage.aiFogOfWar = false;
		Player[] civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman).Take(2).ToArray();
		ai = civs[0];
		enemy = civs[1];
		ai.EnsureRelationshipExists(enemy);

		// A city of ours in the middle of open flat land.
		home = gameData.map.tiles.First(t => IsFlatEmptyLand(t) && t.IsAllowCities()
			&& t.GetTilesWithinTileSquare(6).All(IsOpenLand));
		CityInteractions.BuildCity(home, ai, "Home");
		foreach (Tile t in home.GetTilesWithinTileSquare(7)) {
			ai.tileKnowledge.knownTiles.Add(t);
		}
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private static bool IsFlatEmptyLand(Tile t) {
		return t != Tile.NONE && t.IsLand() && !t.HasCity() && t.unitsOnTile.Count == 0 && !t.hasBarbarianCamp
			&& !t.IsImpassable() && t.OwningPlayer() == null
			&& (t.overlayTerrainType.Key == "grassland" || t.overlayTerrainType.Key == "plains");
	}

	private static bool IsOpenLand(Tile t) {
		return t != Tile.NONE && t.IsLand() && !t.HasCity() && t.unitsOnTile.Count == 0 && !t.hasBarbarianCamp
			&& !t.IsImpassable() && t.OwningPlayer() == null;
	}

	private UnitPrototype Prototype(string name) {
		return gameData.unitPrototypes.Single(p => p.name == name);
	}

	private Tile Step(Tile from, TileDirection direction, int steps = 1) {
		Tile t = from;
		for (int i = 0; i < steps; ++i) {
			t = t.neighbors[direction];
		}
		return t;
	}

	[Fact]
	public void AnEnemyNextToATileThreatensIt() {
		ai.DeclareWarOn(enemy, gameData.turn);
		Tile field = Step(home, TileDirection.EAST, 2);
		gameData.SpawnUnit(enemy, Prototype("Warrior"), Step(field, TileDirection.EAST));

		AIThreat threat = AIThreat.Assess(ai);
		Assert.True(threat.EnemyCanReach(field));
		Assert.True(threat.IsUnsafeForCivilian(field));
		Assert.False(threat.IsUnsafeForCivilian(home));

		// Not with a defender of ours there.
		gameData.SpawnUnit(ai, Prototype("Spearman"), field);
		Assert.False(threat.IsUnsafeForCivilian(field));
	}

	[Fact]
	public void UnitsOfACivWeAreAtPeaceWithDontThreaten() {
		Tile field = Step(home, TileDirection.EAST, 2);
		gameData.SpawnUnit(enemy, Prototype("Warrior"), Step(field, TileDirection.EAST));

		Assert.False(AIThreat.Assess(ai).IsUnsafeForCivilian(field));
	}

	[Fact]
	public async Task AWorkerRunsFromAnEnemyToOurCity() {
		ai.DeclareWarOn(enemy, gameData.turn);
		Tile field = Step(home, TileDirection.EAST);
		MapUnit worker = gameData.SpawnUnit(ai, Prototype("Worker"), field);
		gameData.SpawnUnit(enemy, Prototype("Warrior"), Step(field, TileDirection.EAST));

		worker.currentAI = new WorkerAI(WorkerAI.MakeAiData(worker, ai));
		await worker.currentAI.PlayTurn(ai, worker);

		Assert.Equal(home, worker.location);
	}

	// A settler in our city with a single escort, and an enemy horseman
	// next to the city that could reach any tile it steps onto.
	private (MapUnit settler, SettlerAI settlerAi) SettlerWithOneEscortAndAHorsemanNearby() {
		ai.DeclareWarOn(enemy, gameData.turn);
		gameData.SpawnUnit(enemy, Prototype("Horseman"), Step(home, TileDirection.WEST));
		MapUnit settler = gameData.SpawnUnit(ai, Prototype("Settler"), home);
		MapUnit escort = gameData.SpawnUnit(ai, Prototype("Spearman"), home);
		SettlerAI settlerAi = new(new SettlerAIData {
			goal = SettlerAIData.SettlerGoal.BUILD_CITY,
			destination = Step(home, TileDirection.EAST, 5),
			escort = escort,
		});
		escort.currentAI = new EscortAI(new EscortAIData { unitToEscort = settler });
		settler.currentAI = settlerAi;
		return (settler, settlerAi);
	}

	[Fact]
	public async Task ASettlerWithOneEscortWaitsForEnemiesNearbyToLeave() {
		(MapUnit settler, SettlerAI settlerAi) = SettlerWithOneEscortAndAHorsemanNearby();

		Assert.Equal(C7GameData.UnitAI.Result.InProgress, await settler.currentAI.PlayTurn(ai, settler));
		Assert.Equal(home, settler.location);
	}

	[Fact]
	public async Task ASettlerWithTwoEscortsGoesOn() {
		(MapUnit settler, SettlerAI settlerAi) = SettlerWithOneEscortAndAHorsemanNearby();
		gameData.SpawnUnit(ai, Prototype("Spearman"), home);

		await settler.currentAI.PlayTurn(ai, settler);
		Assert.NotEqual(home, settler.location);
	}

	[Fact]
	public void NoCitySitesNextToTheLandOfACivWeAreAtWarWith() {
		Tile theirs = Step(home, TileDirection.EAST, 6);
		CityInteractions.BuildCity(theirs, enemy, "Theirs");
		// Their borders reach two tiles out.
		foreach (Tile t in theirs.GetTilesWithinTileSquare(2)) {
			t.owningCity = theirs.cityAtTile;
		}
		Tile doorstep = Step(home, TileDirection.EAST, 3);
		Assert.Null(doorstep.OwningPlayer());

		Assert.Contains(doorstep, SettlerLocationAI.GetScoredSettlerCandidates(home, ai).Keys);
		ai.DeclareWarOn(enemy, gameData.turn);
		Assert.DoesNotContain(doorstep, SettlerLocationAI.GetScoredSettlerCandidates(home, ai).Keys);
	}
}
