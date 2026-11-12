using System;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

// The AI pillages its enemies' land in wartime with its units' leftover
// moves (see PillageAI).
public class PillageAITest : MapBase, IDisposable {
	private readonly C7GameData.GameData gameData = new();
	private readonly Player us = MakeCiv("player-1");
	private readonly Player them = MakeCiv("player-2");
	private readonly TerrainImprovement road = new(Tile.TileOverlays.ROAD, TerrainImprovement.Layer.Roads, movementCost: 1.0f / 3);
	private readonly TerrainImprovement mine = new(Tile.TileOverlays.MINE, TerrainImprovement.Layer.ResourceDevelopment);

	// A row of plains running east: the first is ours, the rest theirs.
	private readonly Tile[] row = new Tile[4];
	private readonly bool fogBefore = EngineStorage.aiFogOfWar;

	public PillageAITest() {
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.aiFogOfWar = false;
		gameData.players.Add(us);
		gameData.players.Add(them);
		us.EnsureRelationshipExists(them);
		them.EnsureRelationshipExists(us);

		InitilizeStartTile(MakePlainsTile(), new TileLocation(50, 50));
		row[0] = startTile;
		for (int i = 1; i < row.Length; ++i) {
			row[i] = AddNeighborsAndUpdateMap(row[i - 1], MakePlainsTile(), TileDirection.EAST);
			AddNeighborsAndUpdateMap(row[i], row[i - 1], TileDirection.WEST);
		}
		row[0].owningCity = new City(Tile.NONE, us, "Ours", ID.None("city"));
		City theirs = new(Tile.NONE, them, "Theirs", ID.None("city"));
		for (int i = 1; i < row.Length; ++i) {
			row[i].owningCity = theirs;
			row[i].overlays.Add(road);
			row[i].overlays.Add(mine);
		}
		row[0].overlays.Add(road);
	}

	public void Dispose() {
		EngineStorage.aiFogOfWar = fogBefore;
	}

	private static Player MakeCiv(string id) {
		return new Player() { id = ID.FromString(id), civilization = new Civilization(), government = new Government() };
	}

	private MapUnit MakeUnit(Player owner, Tile location, int movement = 2) {
		MapUnit unit = new(ID.None("unit")) {
			owner = owner,
			nationality = owner.civilization,
			unitType = new UnitPrototype() { name = "Swordsman", attack = 3, defense = 2, movement = movement },
			experienceLevel = new ExperienceLevel("regular", "Regular", 3, 0, 0),
			location = location,
		};
		unit.unitType.categories.Add("Land");
		unit.unitType.actions.Add(UnitAction.Pillage);
		unit.hitPointsRemaining = unit.maxHitPoints;
		unit.movementPoints.reset(movement);
		location.unitsOnTile.Add(unit);
		owner.units.Add(unit);
		return unit;
	}

	private bool IsBare(Tile tile) => !tile.overlays.HasImprovement(road) && !tile.overlays.HasImprovement(mine);

	[Fact]
	public void AtWarAUnitWithMovesLeftPillagesEnemyLand() {
		MapUnit raider = MakeUnit(us, row[1]);
		us.DeclareWarOn(them, gameData.turn);

		Assert.Equal(1, PillageAI.PlayTurn(us, gameData));
		Assert.True(IsBare(row[1]));
		Assert.True(raider.hasPillagedThisTurn);
	}

	[Fact]
	public void NotAtPeace() {
		MakeUnit(us, row[1]);

		Assert.Equal(0, PillageAI.PlayTurn(us, gameData));
		Assert.False(IsBare(row[1]));
	}

	[Fact]
	public void NotOurOwnLand() {
		MakeUnit(us, row[0]);
		us.DeclareWarOn(them, gameData.turn);

		Assert.Equal(0, PillageAI.PlayTurn(us, gameData));
		Assert.True(row[0].overlays.HasImprovement(road));
	}

	[Fact]
	public void NotWithAnEnemyAttackerNextToIt() {
		MakeUnit(us, row[1]);
		MakeUnit(them, row[2]);
		us.DeclareWarOn(them, gameData.turn);

		Assert.Equal(0, PillageAI.PlayTurn(us, gameData));
		Assert.False(IsBare(row[1]));
	}

	[Fact]
	public void NotWhenBadlyWounded() {
		MapUnit raider = MakeUnit(us, row[1]);
		raider.hitPointsRemaining = 1;
		us.DeclareWarOn(them, gameData.turn);

		Assert.Equal(0, PillageAI.PlayTurn(us, gameData));
	}

	[Fact]
	public void NotWithNoMovesLeft() {
		MapUnit raider = MakeUnit(us, row[1]);
		raider.movementPoints.onConsumeAll();
		us.DeclareWarOn(them, gameData.turn);

		Assert.Equal(0, PillageAI.PlayTurn(us, gameData));
	}

	[Fact]
	public void AHumansUnitsAreLeftAlone() {
		us.isHuman = true;
		MakeUnit(us, row[1]);
		us.DeclareWarOn(them, gameData.turn);

		Assert.Equal(0, PillageAI.PlayTurn(us, gameData));
	}

	[Fact]
	public void AFortifiedUnitFortifiesAgain() {
		MapUnit raider = MakeUnit(us, row[1]);
		raider.Fortify();
		us.DeclareWarOn(them, gameData.turn);

		Assert.Equal(1, PillageAI.PlayTurn(us, gameData));
		Assert.True(raider.isFortified);
	}
}
