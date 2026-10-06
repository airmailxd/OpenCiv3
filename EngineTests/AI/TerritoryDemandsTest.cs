using System.Collections.Generic;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;
using static C7GameData.PlayerRelationship;

namespace EngineTests.AI;

public class TerritoryDemandsTest : MapBase {
	private readonly C7GameData.GameData gameData = new();
	private readonly Player us = MakeCiv("player-1");
	private readonly Player them = MakeCiv("player-2");

	// A row of plains running east: the first three tiles are inside our
	// borders, the rest belong to no one.
	private readonly List<Tile> row = new();

	public TerritoryDemandsTest() {
		EngineStorage.InitializeGameDataForTests(gameData);
		gameData.players.Add(us);
		gameData.players.Add(them);
		us.EnsureRelationshipExists(them);

		InitilizeStartTile(MakePlainsTile(), new TileLocation(50, 50));
		row.Add(startTile);
		for (int i = 1; i < 6; ++i) {
			Tile next = AddNeighborsAndUpdateMap(row[i - 1], MakePlainsTile(), TileDirection.EAST);
			AddNeighborsAndUpdateMap(next, row[i - 1], TileDirection.WEST);
			row.Add(next);
		}
		City capital = new(Tile.NONE, us, "Capital", ID.None("city"));
		for (int i = 0; i < 3; ++i) {
			row[i].owningCity = capital;
		}
	}

	private static Player MakeCiv(string id) {
		return new Player() { id = ID.FromString(id), civilization = new Civilization(), government = new Government() };
	}

	private MapUnit MakeUnit(Player owner, Tile location) {
		MapUnit unit = new(ID.None("unit")) {
			owner = owner,
			unitType = new UnitPrototype() { attack = 1, defense = 1 },
			location = location,
		};
		unit.unitType.categories.Add("Land");
		location.unitsOnTile.Add(unit);
		owner.units.Add(unit);
		return unit;
	}

	[Fact]
	public void UnitsInOurTerritoryAreTrespassing() {
		MapUnit intruder = MakeUnit(them, row[1]);
		MakeUnit(them, row[4]);

		Dictionary<Player, List<MapUnit>> trespassers = TerritoryDemands.FindTrespassers(us, gameData);

		Assert.Equal([intruder], trespassers[them]);
	}

	[Fact]
	public void RightOfPassageLetsUnitsIn() {
		MakeUnit(them, row[1]);
		RegisterMultiTurnDeal(us, them, new MultiTurnDeal(DealType.DiplomaticAgreement,
			DealSubType.RightOfPassage, DealDetails.Exchange));

		Assert.Empty(TerritoryDemands.FindTrespassers(us, gameData));
	}

	[Fact]
	public void CivsAtWarFightInsteadOfTalking() {
		MakeUnit(them, row[1]);
		them.DeclareWarOn(us, gameData.turn);

		Assert.Empty(TerritoryDemands.FindTrespassers(us, gameData));
	}

	[Fact]
	public async Task AnAiTrespasserWithdrawsToTheNearestFreeTile() {
		MapUnit intruder = MakeUnit(them, row[1]);

		await TerritoryDemands.MakeDemands(us, gameData);

		Assert.Same(row[3], intruder.location);
		Assert.Contains(intruder, row[3].unitsOnTile);
		Assert.DoesNotContain(intruder, row[1].unitsOnTile);
		Assert.True(AtPeace(us, them));
	}

	[Fact]
	public async Task ComingBackAfterPromisingToLeaveEndsInWar() {
		for (int i = 0; i < TerritoryDemands.WITHDRAWALS_BEFORE_WAR; ++i) {
			MapUnit intruder = MakeUnit(them, row[1]);
			await TerritoryDemands.MakeDemands(us, gameData);
			Assert.Same(row[3], intruder.location);
			Assert.True(AtPeace(us, them));
			gameData.turn++;
		}

		MakeUnit(them, row[1]);
		await TerritoryDemands.MakeDemands(us, gameData);

		Assert.True(AtWar(us, them));
		// We threw them out, so we started it.
		Assert.True(them.playerRelationships[us.id].otherStartedCurrentWar);
	}

	[Fact]
	public async Task OldPromisesAreForgotten() {
		for (int i = 0; i < TerritoryDemands.WITHDRAWALS_BEFORE_WAR; ++i) {
			MakeUnit(them, row[1]);
			await TerritoryDemands.MakeDemands(us, gameData);
			gameData.turn += TerritoryDemands.MEMORY_TURNS + 1;
		}

		MakeUnit(them, row[1]);
		await TerritoryDemands.MakeDemands(us, gameData);

		Assert.True(AtPeace(us, them));
	}
	// Puts the demand to a human, who answers with the given messages, and
	// returns the demand they were shown.
	private async Task<MsgShowTerritoryDemand> DemandFromHuman(params MessageToEngine[] answers) {
		them.isHuman = true;
		Task demands = TerritoryDemands.MakeDemands(us, gameData);

		MsgShowTerritoryDemand shown = null;
		while (EngineStorage.TryDequeueNextMessageToUI(out MessageToUI msg)) {
			shown ??= msg as MsgShowTerritoryDemand;
		}
		Assert.NotNull(shown);
		foreach (MessageToEngine answer in answers) {
			answer.playerID = them.id;
			answer.send();
			EngineStorage.ProcessNextMessageToEngine();
		}
		await demands;
		return shown;
	}

	[Fact]
	public async Task AHumanWhoRefusesToLeaveIsAttacked() {
		MakeUnit(them, row[1]);
		MakeUnit(them, row[2]);

		MsgShowTerritoryDemand shown = await DemandFromHuman(
			new MsgRespondToTerritoryDemand(false), new MsgDiplomacyCompleted());

		Assert.Same(us, shown.aiPlayer);
		Assert.Same(them, shown.humanPlayer);
		Assert.Equal(2, shown.unitCount);
		Assert.True(AtWar(us, them));
	}

	[Fact]
	public async Task AHumanWhoAgreesToLeaveIsMovedOut() {
		MapUnit intruder = MakeUnit(them, row[1]);

		await DemandFromHuman(new MsgRespondToTerritoryDemand(true), new MsgDiplomacyCompleted());

		Assert.Same(row[3], intruder.location);
		Assert.True(AtPeace(us, them));
	}

	[Fact]
	public async Task ClosingTheDemandWithoutAnsweringIsAgreeing() {
		MapUnit intruder = MakeUnit(them, row[1]);

		await DemandFromHuman(new MsgDiplomacyCompleted());

		Assert.Same(row[3], intruder.location);
		Assert.True(AtPeace(us, them));
	}
}
