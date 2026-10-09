using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class EspionageTest : IClassFixture<SaveGameFixture>, System.IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player actor;
	private readonly Player target;
	private readonly Player third;

	// Rolls that always succeed and always fail.
	private const int Succeed = 0;
	private const int Fail = 99;

	public EspionageTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler)).ToArray();
		actor = civs[0];
		target = civs[1];
		third = civs[2];
		Tech writing = gameData.techs.Find(t => t.Name == Espionage.EmbassyTechName);
		if (writing != null) {
			actor.knownTechs.Add(writing.id);
		}
		actor.gold = 100000;
		target.gold = 100;
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private City FoundCity(Player player, int size, Tile site = null) {
		Tile tile = site ?? player.units.First(u => u.unitType.isSettler).location;
		City city = CityInteractions.BuildCity(tile, player, player.GetNextCityName());
		while (city.residents.Count < size) {
			city.AddCitizen(new CityResident() {
				city = city,
				citizenType = city.residents[0].citizenType,
				tileWorked = city.residents[0].tileWorked,
				nationality = player.civilization,
			});
		}
		foreach (Player p in gameData.players) {
			p.tileKnowledge.knownTiles.Add(tile);
		}
		return city;
	}

	// A second city for the player, next to its first.
	private City FoundSecondCity(Player player, int size) {
		City capital = player.cities.First();
		Tile site = capital.location.neighbors.Values.SelectMany(t => t.neighbors.Values)
			.First(t => t != Tile.NONE && t.IsLand() && !t.HasCity() && t.DistanceTo(capital.location) >= 2);
		return FoundCity(player, size, site);
	}

	private void Meet(Player a, Player b) {
		a.EnsureRelationshipExists(b);
	}

	private void GiveIntelligenceAgency(Player player) {
		SaveBuilding sb = new() { name = "Intelligence Agency" };
		sb.flags.Add(SaveBuilding.Flag.AllowsSpyMissions);
		player.cities.First().AddBuilding(new Building(sb, gameData));
	}

	private City SetUpEmbassy() {
		FoundCity(actor, 3);
		City city = FoundCity(target, 4);
		Meet(actor, target);
		actor.playerRelationships[target.id].hasEmbassy = true;
		return city;
	}

	[Fact]
	public void EmbassyNeedsContactAndThenIsEstablished() {
		FoundCity(actor, 3);
		FoundCity(target, 3);
		Assert.NotNull(Espionage.Unavailable(gameData, actor, EspionageMission.EstablishEmbassy, target, null));

		Meet(actor, target);
		int cost = Espionage.Cost(gameData, actor, EspionageMission.EstablishEmbassy, target, null);
		Assert.Equal(100, Espionage.SuccessPercent(gameData, actor, EspionageMission.EstablishEmbassy, target, null));

		Espionage.MissionResult result = Espionage.Perform(gameData, actor, EspionageMission.EstablishEmbassy, target, null, Fail);
		Assert.True(result.success);
		Assert.True(Espionage.HasEmbassy(actor, target));
		Assert.False(Espionage.HasEmbassy(target, actor));
		Assert.Equal(100000 - cost, actor.gold);

		Assert.NotNull(Espionage.Unavailable(gameData, actor, EspionageMission.EstablishEmbassy, target, null));
	}

	[Fact]
	public void EmbassyReportsTheCapitalAndRevealsItsRadius() {
		FoundCity(actor, 3);
		City capital = FoundCity(target, 4);
		capital.SetStoredShields(12);
		Meet(actor, target);
		gameData.turn = 7;
		List<Tile> radius = capital.location.GetTilesWithinRankDistance(target.rules.MaxRankOfWorkableTiles)
			.Where(t => t != Tile.NONE).ToList();
		Assert.Contains(radius, t => !actor.tileKnowledge.isTileKnown(t));

		Espionage.MissionResult result = Espionage.Perform(gameData, actor, EspionageMission.EstablishEmbassy, target, null, Succeed);
		Assert.True(result.success);
		Espionage.CityReport report = actor.playerRelationships[target.id].embassyReport;
		Assert.NotNull(report);
		Assert.Equal(7, report.turn);
		Assert.Equal(capital.name, report.cityName);
		Assert.Equal(4, report.size);
		Assert.Equal(12, report.shieldsStored);
		Assert.All(radius, t => Assert.True(actor.tileKnowledge.isTileKnown(t)));
		Assert.Equal(capital, result.city);

		// The report is of the capital as it was, not as it is.
		capital.SetStoredShields(30);
		Assert.Equal(12, actor.playerRelationships[target.id].embassyReport.shieldsStored);
	}

	// The land around the capital is shown as it is while the embassy's
	// view of it is open, and then is fogged, though still known.
	[Fact]
	public void PeekedTilesAreInViewUntilThePeekEnds() {
		FoundCity(actor, 3);
		City capital = FoundCity(target, 4);
		Meet(actor, target);
		Espionage.Perform(gameData, actor, EspionageMission.EstablishEmbassy, target, null, Succeed);
		List<Tile> radius = Espionage.CityRadius(capital);
		Tile far = radius.First(t => !actor.tileKnowledge.isActiveTile(t));

		actor.tileKnowledge.Peek(radius);
		Assert.All(radius, t => Assert.True(actor.tileKnowledge.isActiveTile(t)));

		actor.tileKnowledge.EndPeek();
		Assert.False(actor.tileKnowledge.isActiveTile(far));
		Assert.True(actor.tileKnowledge.isTileKnown(far));
	}

	[Fact]
	public void EmbassyNeedsWriting() {
		Tech writing = gameData.techs.Find(t => t.Name == Espionage.EmbassyTechName);
		Assert.NotNull(writing);
		FoundCity(actor, 3);
		FoundCity(target, 3);
		Meet(actor, target);
		actor.knownTechs.Remove(writing.id);
		Assert.NotNull(Espionage.Unavailable(gameData, actor, EspionageMission.EstablishEmbassy, target, null));
	}

	[Fact]
	public void DiplomaticMissionsNeedAnEmbassyAndGold() {
		FoundCity(actor, 3);
		City city = FoundCity(target, 4);
		Meet(actor, target);
		Assert.NotNull(Espionage.Unavailable(gameData, actor, EspionageMission.InvestigateCity, target, city));

		actor.playerRelationships[target.id].hasEmbassy = true;
		Assert.Null(Espionage.Unavailable(gameData, actor, EspionageMission.InvestigateCity, target, city));

		actor.gold = 0;
		Assert.NotNull(Espionage.Unavailable(gameData, actor, EspionageMission.InvestigateCity, target, city));
		Espionage.MissionResult result = Espionage.Perform(gameData, actor, EspionageMission.InvestigateCity, target, city, Succeed);
		Assert.False(result.performed);
		Assert.Equal(0, actor.gold);
	}

	[Fact]
	public void InvestigatingReportsTheCity() {
		City city = SetUpEmbassy();
		city.SetStoredShields(17);

		Espionage.MissionResult result = Espionage.Perform(gameData, actor, EspionageMission.InvestigateCity, target, city, Fail);
		Assert.True(result.success);
		Assert.Equal(city.name, result.report.cityName);
		Assert.Equal(4, result.report.size);
		Assert.Equal(17, result.report.shieldsStored);
		Assert.Contains(result.report.buildings, b => b == city.constructed_buildings[0].building.name);
		Assert.Equal(target.gold, result.report.ownerGold);
	}

	[Fact]
	public void StealingTechnologyGivesAnAdvanceTheTargetKnows() {
		City city = SetUpEmbassy();
		Tech stealable = gameData.techs.First(t => t.Prerequisites.All(p => actor.knownTechs.Contains(p.id)) && !actor.knownTechs.Contains(t.id));
		foreach (Tech t in gameData.techs) {
			if (t != stealable && !actor.knownTechs.Contains(t.id)) {
				target.knownTechs.Remove(t.id);
			}
		}
		target.knownTechs.Add(stealable.id);

		Espionage.MissionResult result = Espionage.Perform(gameData, actor, EspionageMission.StealTechnology, target, city, Succeed);
		Assert.True(result.success);
		Assert.Equal(stealable, result.stolenTech);
		Assert.Contains(stealable.id, actor.knownTechs);

		// Nothing left to steal.
		Assert.NotNull(Espionage.Unavailable(gameData, actor, EspionageMission.StealTechnology, target, city));
	}

	[Fact]
	public void CaughtMissionsCostGoldAndCauseAnIncident() {
		City city = SetUpEmbassy();
		city.SetStoredShields(20);
		int cost = Espionage.Cost(gameData, actor, EspionageMission.SabotageProduction, target, city);

		Espionage.MissionResult result = Espionage.Perform(gameData, actor, EspionageMission.SabotageProduction, target, city, Fail);
		Assert.True(result.performed);
		Assert.False(result.success);
		Assert.Equal(20, city.shieldsStored);
		Assert.Equal(100000 - cost, actor.gold);
		Assert.Equal(1, target.playerRelationships[actor.id].espionageIncidents);
	}

	[Fact]
	public void SabotageDestroysProduction() {
		City city = SetUpEmbassy();
		city.SetStoredShields(20);

		Espionage.MissionResult result = Espionage.Perform(gameData, actor, EspionageMission.SabotageProduction, target, city, Succeed);
		Assert.True(result.success);
		Assert.Equal(0, city.shieldsStored);
	}

	[Fact]
	public void CapitalsCannotBeIncited() {
		City capital = SetUpEmbassy();
		Assert.True(capital.IsCapital());
		Assert.NotNull(Espionage.Unavailable(gameData, actor, EspionageMission.InciteRevolt, target, capital));
	}

	[Fact]
	public void IncitedCityJoinsTheActorWithItsUnits() {
		SetUpEmbassy();
		City city = FoundSecondCity(target, 3);
		MapUnit defender = target.units.First(u => !u.unitType.isSettler && u.location != null);
		defender.location.unitsOnTile.Remove(defender);
		defender.location = city.location;
		city.location.unitsOnTile.Add(defender);

		Espionage.MissionResult result = Espionage.Perform(gameData, actor, EspionageMission.InciteRevolt, target, city, Succeed);
		Assert.True(result.success);
		Assert.Equal(actor, city.owner);
		Assert.Contains(city, actor.cities);
		Assert.DoesNotContain(city, target.cities);
		Assert.Equal(3, city.residents.Count);
		Assert.Equal(actor, defender.owner);
	}

	[Fact]
	public void IncitedCityForgetsTheOldOwnersProductionQueue() {
		SetUpEmbassy();
		City city = FoundSecondCity(target, 3);
		city.EnqueueProduction(city.ListProductionOptions(gameData).First());
		MapUnit defender = target.units.First(u => !u.unitType.isSettler && u.location != null);
		defender.location.unitsOnTile.Remove(defender);
		defender.location = city.location;
		city.location.unitsOnTile.Add(defender);

		CityInteractions.TransferCity(city, actor);

		Assert.Equal(actor, city.owner);
		Assert.Empty(city.productionQueue);
		Assert.Equal(actor, defender.owner);
		Assert.DoesNotContain(city.location.unitsOnTile, u => u.owner == target);
	}

	[Fact]
	public void UndefinedMissionsAreRejected() {
		City city = SetUpEmbassy();
		EspionageMission bogus = (EspionageMission)999;

		Assert.NotNull(Espionage.Unavailable(gameData, actor, bogus, target, city));
		Espionage.MissionResult result = Espionage.Perform(gameData, actor, bogus, target, city, Succeed);
		Assert.False(result.performed);
	}

	[Fact]
	public void InciteRevoltCostFollowsTheTargetsTreasuryAndDisorder() {
		SetUpEmbassy();
		City city = FoundSecondCity(target, 4);
		target.gold = 0;
		int calm = Espionage.InciteRevoltCost(actor, city);
		target.gold = 1000;
		int rich = Espionage.InciteRevoltCost(actor, city);
		Assert.True(rich > calm);

		city.isInCivilDisorder = true;
		Assert.True(Espionage.InciteRevoltCost(actor, city) < rich);
	}

	[Fact]
	public void SpiesNeedTheIntelligenceAgency() {
		SetUpEmbassy();
		Assert.NotNull(Espionage.Unavailable(gameData, actor, EspionageMission.PlantSpy, target, null));

		GiveIntelligenceAgency(actor);
		Espionage.MissionResult result = Espionage.Perform(gameData, actor, EspionageMission.PlantSpy, target, null, Succeed);
		Assert.True(result.success);
		Assert.True(Espionage.HasSpy(actor, target));
	}

	[Fact]
	public void SpiesAndTheAgencyImproveTheChances() {
		City city = SetUpEmbassy();
		int before = Espionage.SuccessPercent(gameData, actor, EspionageMission.StealTechnology, target, city);
		GiveIntelligenceAgency(actor);
		actor.playerRelationships[target.id].hasSpy = true;
		int after = Espionage.SuccessPercent(gameData, actor, EspionageMission.StealTechnology, target, city);
		Assert.Equal(before + 25, after);
	}

	[Fact]
	public void ExposingAnEnemySpyRemovesIt() {
		SetUpEmbassy();
		GiveIntelligenceAgency(actor);
		actor.playerRelationships[target.id].hasSpy = true;
		FoundCity(third, 2);
		Meet(third, target);
		third.playerRelationships[target.id].hasSpy = true;

		Espionage.MissionResult result = Espionage.Perform(gameData, actor, EspionageMission.ExposeEnemySpy, target, null, Succeed);
		Assert.True(result.success);
		Assert.Equal(third, result.exposedSpyOwner);
		Assert.False(third.playerRelationships[target.id].hasSpy);
		Assert.Equal(1, target.playerRelationships[third.id].espionageIncidents);
	}

	[Fact]
	public void AIEstablishesEmbassiesItCanAfford() {
		FoundCity(actor, 3);
		FoundCity(target, 3);
		Meet(actor, target);
		actor.isHuman = false;
		actor.gold = 0;

		C7Engine.AI.EspionageAI.PlayTurn(actor, gameData);
		Assert.False(Espionage.HasEmbassy(actor, target));

		actor.gold = 1000;
		C7Engine.AI.EspionageAI.PlayTurn(actor, gameData);
		Assert.True(Espionage.HasEmbassy(actor, target));
		Assert.True(actor.gold >= 100);
	}

	[Fact]
	public void EmbassiesAndSpiesAreSaved() {
		SetUpEmbassy();
		actor.playerRelationships[target.id].hasSpy = true;
		actor.playerRelationships[target.id].embassyReport = Espionage.Investigate(target.cities.First(), 5);
		target.playerRelationships[actor.id].espionageIncidents = 2;
		gameData.unitedNations.votingTurn = 42;
		gameData.unitedNations.offerTurn = 41;
		gameData.unitedNations.humanVotes["x"] = "y";

		SaveGame save = SaveGame.FromGameData(gameData);
		SaveGame reloaded = SaveGame.FromJSON(save.ToCompactJSON());
		SavePlayer savedActor = reloaded.Players.First(p => p.id.ToString() == actor.id.ToString());
		SavePlayer savedTarget = reloaded.Players.First(p => p.id.ToString() == target.id.ToString());
		Assert.True(savedActor.playerRelationships[target.id.ToString()].hasEmbassy);
		Assert.True(savedActor.playerRelationships[target.id.ToString()].hasSpy);
		Espionage.CityReport savedReport = savedActor.playerRelationships[target.id.ToString()].embassyReport;
		Assert.Equal(5, savedReport.turn);
		Assert.Equal(target.cities.First().name, savedReport.cityName);
		Assert.Equal(2, savedTarget.playerRelationships[actor.id.ToString()].espionageIncidents);
		Assert.Equal(42, reloaded.UnitedNations.votingTurn);
		Assert.Equal(41, reloaded.UnitedNations.offerTurn);
		Assert.Equal("y", reloaded.UnitedNations.humanVotes["x"]);
	}
}
