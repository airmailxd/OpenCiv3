using System;
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

public class TerritoryDemandsTest : MapBase, IDisposable {
	private readonly C7GameData.GameData gameData = new();
	private readonly Player us = MakeCiv("player-1");
	private readonly Player them = MakeCiv("player-2");

	// A row of plains running east: the first three tiles are inside our
	// borders, the rest belong to no one.
	private readonly List<Tile> row = new();

	public TerritoryDemandsTest() {
		// Engine state is static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.pendingMessages.Clear();
		EngineStorage.messagesToUI.Clear();
		EngineStorage.ResetNetworking();
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

	public void Dispose() {
		EngineStorage.ResetNetworking();
		EngineStorage.activePlayerID = null;
	}

	private static Player MakeCiv(string id) {
		return new Player() { id = ID.FromString(id), civilization = new Civilization(), government = new Government() };
	}

	private MapUnit MakeUnit(Player owner, Tile location) {
		MapUnit unit = new(ID.None("unit")) {
			owner = owner,
			unitType = new UnitPrototype() { attack = 1, defense = 1 },
			experienceLevel = new ExperienceLevel("regular", "Regular", 3, 0, 0),
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
	public void MilitaryAlliesAreWelcome() {
		MakeUnit(them, row[1]);
		RegisterMultiTurnDeal(us, them, new MultiTurnDeal(DealType.DiplomaticAgreement,
			DealSubType.MilitaryAlliance, DealDetails.Exchange));

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
	public async Task AHumanWhoKeepsComingBackAfterPromisingToLeaveIsAttacked() {
		for (int i = 0; i < TerritoryDemands.WITHDRAWALS_BEFORE_WAR; ++i) {
			MapUnit intruder = MakeUnit(them, row[1]);
			await DemandFromHuman(new MsgRespondToTerritoryDemand(true), new MsgDiplomacyCompleted());
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
	public async Task AnAiThatKeepsComingBackIsOnlyEverWarned() {
		for (int i = 0; i < TerritoryDemands.WITHDRAWALS_BEFORE_WAR * 2; ++i) {
			MapUnit intruder = MakeUnit(them, row[1]);
			await TerritoryDemands.MakeDemands(us, gameData);
			Assert.Same(row[3], intruder.location);
			gameData.turn++;
		}

		Assert.True(AtPeace(us, them));
	}

	[Fact]
	public async Task OldPromisesAreForgotten() {
		for (int i = 0; i < TerritoryDemands.WITHDRAWALS_BEFORE_WAR; ++i) {
			MakeUnit(them, row[1]);
			await DemandFromHuman(new MsgRespondToTerritoryDemand(true), new MsgDiplomacyCompleted());
			gameData.turn += TerritoryDemands.MEMORY_TURNS + 1;
		}

		MakeUnit(them, row[1]);
		await DemandFromHuman(new MsgRespondToTerritoryDemand(true), new MsgDiplomacyCompleted());

		Assert.True(AtPeace(us, them));
	}

	[Fact]
	public async Task UnitsWithNowhereToGoAreNotToldToLeave() {
		City capital = row[0].owningCity;
		foreach (Tile tile in row) {
			tile.owningCity = capital;
		}
		them.isHuman = true;
		MapUnit stuck = MakeUnit(them, row[1]);

		for (int i = 0; i < TerritoryDemands.WITHDRAWALS_BEFORE_WAR + 1; ++i) {
			await TerritoryDemands.MakeDemands(us, gameData);
			gameData.turn++;
		}

		Assert.False(EngineStorage.TryDequeueNextMessageToUI(out _));
		Assert.Same(row[1], stuck.location);
		Assert.Equal(0, us.playerRelationships[them.id].recentWithdrawals);
		Assert.True(AtPeace(us, them));
	}

	[Fact]
	public async Task AHumanWhoCantBeReachedIsTakenToAgree() {
		MapUnit intruder = MakeUnit(them, row[1]);
		them.isHuman = true;
		// As for a LAN guest whose seat is empty.
		EngineStorage.playerReachable = player => player != them.id;

		await TerritoryDemands.MakeDemands(us, gameData);

		Assert.DoesNotContain(EngineStorage.messagesToUI, msg => msg is MsgShowTerritoryDemand);
		Assert.Same(row[3], intruder.location);
		Assert.True(AtPeace(us, them));
	}

	[Fact]
	public async Task OnlyTheHumanAskedCanAnswer() {
		Player bystander = MakeCiv("player-3");
		bystander.isHuman = true;
		gameData.players.Add(bystander);
		// The last human to play is still the active player during AI turns.
		EngineStorage.activePlayerID = bystander.id;
		MapUnit intruder = MakeUnit(them, row[1]);
		them.isHuman = true;

		Task demands = TerritoryDemands.MakeDemands(us, gameData);
		EngineStorage.messagesToUI.Clear();
		new MsgRespondToTerritoryDemand(false) { playerID = bystander.id }.send();
		new MsgDiplomacyCompleted { playerID = bystander.id }.send();
		EngineStorage.ProcessNextMessageToEngine();
		EngineStorage.ProcessNextMessageToEngine();
		await Task.Delay(50);
		Assert.False(demands.IsCompleted);

		new MsgDiplomacyCompleted { playerID = them.id }.send();
		EngineStorage.ProcessNextMessageToEngine();
		await demands;

		Assert.Same(row[3], intruder.location);
		Assert.True(AtPeace(us, them));
	}

	[Fact]
	public void ALandUnitDoesNotWithdrawAcrossWater() {
		MakeWater(row[3]);
		MapUnit intruder = MakeUnit(them, row[1]);

		Assert.False(intruder.WithdrawToNearestFreeTile());
		Assert.Same(row[1], intruder.location);
	}

	[Fact]
	public void ACutOffLandUnitGoesHomeToItsCity() {
		MakeWater(row[3]);
		row[5].cityAtTile = new City(row[5], them, "Home", ID.None("city"));
		MapUnit intruder = MakeUnit(them, row[1]);

		Assert.True(intruder.WithdrawToNearestFreeTile());
		Assert.Same(row[5], intruder.location);
	}

	[Fact]
	public void AShipDoesNotWithdrawIntoAnotherBodyOfWater() {
		MakeWater(row[1]);
		MakeWater(row[4]);
		MapUnit ship = MakeUnit(them, row[1]);
		ship.unitType.categories = new() { "Sea" };

		Assert.False(ship.WithdrawToNearestFreeTile());
		Assert.Same(row[1], ship.location);

		// Joined up, the water leads somewhere.
		MakeWater(row[2]);
		MakeWater(row[3]);
		Assert.True(ship.WithdrawToNearestFreeTile());
		Assert.Same(row[3], ship.location);
	}

	private static void MakeWater(Tile tile) {
		tile.baseTerrainType = new() { Key = "coast" };
		tile.overlayTerrainType = new() { Key = "coast", movementCost = 1 };
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

	// The human, us, tells the AI, them, to leave our territory.
	private MsgWithdrawalDemandResult DemandOfAi() {
		us.isHuman = true;
		EngineStorage.activePlayerID = us.id;
		new MsgDemandWithdrawal(them) { playerID = us.id }.send();
		EngineStorage.ProcessNextMessageToEngine();

		MsgWithdrawalDemandResult result = null;
		while (EngineStorage.TryDequeueNextMessageToUI(out MessageToUI msg)) {
			result ??= msg as MsgWithdrawalDemandResult;
		}
		return result;
	}

	[Fact]
	public void AnAiAHumanTellsToLeaveWithdraws() {
		MapUnit intruder = MakeUnit(them, row[1]);
		MakeUnit(us, row[0]);
		MakeUnit(us, row[0]);

		MsgWithdrawalDemandResult result = DemandOfAi();

		Assert.True(result.withdrew);
		Assert.Same(them, result.opponent);
		Assert.Same(us, result.recipient);
		Assert.Same(row[3], intruder.location);
		Assert.True(AtPeace(us, them));
	}

	[Fact]
	public void AStrongerAiRefusesToLeaveAndDeclaresWar() {
		MapUnit intruder = MakeUnit(them, row[1]);
		MakeUnit(them, row[4]);
		us.tileKnowledge.knownTiles.Add(row[1]);

		MsgWithdrawalDemandResult result = DemandOfAi();

		Assert.False(result.withdrew);
		Assert.Same(row[1], intruder.location);
		Assert.True(AtWar(us, them));
		// The AI started the war, but openly, having been warned.
		Assert.True(us.playerRelationships[them.id].otherStartedCurrentWar);
		Assert.False(us.playerRelationships[them.id].wasSneakAttacked);
	}

	[Fact]
	public void AnAiWithNoUnitsInOurTerritoryHasNothingToAnswer() {
		MakeUnit(them, row[4]);

		Assert.Null(DemandOfAi());
		Assert.True(AtPeace(us, them));
	}
}
