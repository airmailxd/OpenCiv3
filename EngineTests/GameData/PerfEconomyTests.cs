using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using Xunit;

namespace EngineTests.GameData;

// Tests for the caches and rewrites done to make the economy code cheaper:
// they check that cached answers follow every change they depend on.
public class PerfEconomyTests {
	private readonly C7GameData.GameData gameData = new() { gameDifficulty = new Difficulty(), rules = new Rules() };

	public PerfEconomyTests() {
		EngineStorage.InitializeGameDataForTests(gameData);
	}

	private Player MakePlayer(string civName = "Testers") {
		Player player = new() {
			isHuman = true,
			civilization = new Civilization(civName),
			government = new Government(),
			rules = gameData.rules,
		};
		gameData.players.Add(player);
		return player;
	}

	private static City MakeCity(Player owner, int continent = 1) {
		Tile tile = new(ID.None("tile")) { continent = continent };
		City city = new(tile, owner, "Testville", ID.None("city"));
		tile.cityAtTile = city;
		owner.cities.Add(city);
		return city;
	}

	private Building MakeBuilding(string name, SaveBuilding.Flag? flag = null, bool greatWonder = false) {
		SaveBuilding sb = new() { name = name };
		if (flag is SaveBuilding.Flag f) {
			sb.flags.Add(f);
		}
		if (greatWonder) {
			sb.greatWonderProperties = new();
		}
		return new Building(sb, gameData);
	}

	private static bool Has(City city, Building building) {
		return city.GetBuildings().Any(cb => cb.building == building);
	}

	private static void MoveCity(City city, Player to) {
		city.owner.cities.Remove(city);
		to.cities.Add(city);
		city.owner = to;
	}

	[Fact]
	public void WonderGrantedBuildingsFollowBuildingChanges() {
		Player player = MakePlayer();
		City wonderCity = MakeCity(player);
		City other = MakeCity(player);
		Building granary = MakeBuilding("Granary", SaveBuilding.Flag.DoublesCityGrowthRate);
		Building pyramids = MakeBuilding("Pyramids", greatWonder: true);
		pyramids.greatWonderProperties.buildingGainedInEveryCity = granary;

		Assert.False(other.HasGranary());
		Assert.Empty(player.GetActiveWonders());

		wonderCity.AddBuilding(pyramids);
		Assert.True(other.HasGranary());
		Assert.True(wonderCity.HasGranary());
		Assert.Single(player.GetActiveWonders());

		wonderCity.RemoveBuilding(wonderCity.constructed_buildings[0]);
		Assert.False(other.HasGranary());
		Assert.Empty(player.GetActiveWonders());
	}

	[Fact]
	public void WonderGrantedBuildingsGoAwayWhenTheWonderGoesObsolete() {
		Player player = MakePlayer();
		City wonderCity = MakeCity(player);
		City other = MakeCity(player);
		Building granary = MakeBuilding("Granary", SaveBuilding.Flag.DoublesCityGrowthRate);
		Building pyramids = MakeBuilding("Pyramids", greatWonder: true);
		pyramids.greatWonderProperties.buildingGainedInEveryCity = granary;
		pyramids.renderedObsoleteBy = new Tech { id = ID.FromString("tech-7") };
		wonderCity.AddBuilding(pyramids);
		Assert.True(other.HasGranary());

		player.knownTechs.Add(ID.FromString("tech-7"));

		Assert.False(other.HasGranary());
		Assert.Empty(player.GetActiveWonders());
	}

	[Fact]
	public void WonderGrantedBuildingsFollowTheWonderCityWhenCaptured() {
		Player us = MakePlayer("Us");
		Player them = MakePlayer("Them");
		City wonderCity = MakeCity(us);
		City ourOther = MakeCity(us);
		City theirCity = MakeCity(them);
		Building granary = MakeBuilding("Granary", SaveBuilding.Flag.DoublesCityGrowthRate);
		Building pyramids = MakeBuilding("Pyramids", greatWonder: true);
		pyramids.greatWonderProperties.buildingGainedInEveryCity = granary;
		wonderCity.AddBuilding(pyramids);
		Assert.True(ourOther.HasGranary());
		Assert.False(theirCity.HasGranary());

		// Captured without any building changing hands or being destroyed.
		MoveCity(wonderCity, them);

		Assert.False(ourOther.HasGranary());
		Assert.True(theirCity.HasGranary());
		Assert.True(wonderCity.HasGranary());
	}

	[Fact]
	public void ContinentalWondersOnlyGrantOnTheirContinent() {
		Player player = MakePlayer();
		City wonderCity = MakeCity(player, continent: 1);
		City sameContinent = MakeCity(player, continent: 1);
		City otherContinent = MakeCity(player, continent: 2);
		Building walls = MakeBuilding("Walls", SaveBuilding.Flag.ProvidesWalls);
		Building greatWall = MakeBuilding("Great Wall", greatWonder: true);
		greatWall.greatWonderProperties.buildingGainedInEveryCityOnContinent = walls;
		wonderCity.AddBuilding(greatWall);

		Assert.True(sameContinent.HasWalls());
		Assert.False(otherContinent.HasWalls());
	}

	[Fact]
	public void SwappingABuildingWithoutChangingTheCountIsSeen() {
		Player player = MakePlayer();
		City city = MakeCity(player);
		Building temple = MakeBuilding("Temple");
		Building walls = MakeBuilding("Walls", SaveBuilding.Flag.ProvidesWalls);
		city.AddBuilding(temple);
		Assert.False(city.HasWalls());

		city.RemoveBuilding(city.constructed_buildings[0]);
		city.AddBuilding(walls);

		Assert.True(city.HasWalls());
		Assert.False(Has(city, temple));
	}

	[Fact]
	public void ANewBuildingListIsSeen() {
		Player player = MakePlayer();
		City city = MakeCity(player);
		city.AddBuilding(MakeBuilding("Walls", SaveBuilding.Flag.ProvidesWalls));
		Assert.True(city.HasWalls());

		city.constructed_buildings = new();

		Assert.False(city.HasWalls());
	}

	[Fact]
	public void GetBuildingsHandsOutNewObjectsForGrantedBuildings() {
		Player player = MakePlayer();
		City city = MakeCity(player);
		Building granary = MakeBuilding("Granary", SaveBuilding.Flag.DoublesCityGrowthRate);
		Building pyramids = MakeBuilding("Pyramids", greatWonder: true);
		pyramids.greatWonderProperties.buildingGainedInEveryCity = granary;
		city.AddBuilding(pyramids);

		List<CityBuilding> first = city.GetBuildings();
		List<CityBuilding> second = city.GetBuildings();

		Assert.Equal(2, first.Count);
		Assert.NotSame(first, second);
		// Built buildings are the stored objects, granted ones are new.
		Assert.Same(city.constructed_buildings[0], first[0]);
		Assert.Same(first[0], second[0]);
		Assert.Equal(granary, first[1].building);
		Assert.NotSame(first[1], second[1]);
		Assert.DoesNotContain(first[1], city.GetBuildings());

		// Changing what we were handed doesn't change the city.
		first.Clear();
		Assert.Equal(2, city.GetBuildings().Count);
	}

	[Fact]
	public void LargerArmiesFollowThePentagon() {
		Player us = MakePlayer("Us");
		Player them = MakePlayer("Them");
		City city = MakeCity(us);
		Assert.False(us.HasLargerArmies());

		city.AddBuilding(MakeBuilding("The Pentagon", SaveBuilding.Flag.AllowsLargerArmies));
		Assert.True(us.HasLargerArmies());

		MoveCity(city, them);
		Assert.False(us.HasLargerArmies());
		Assert.True(them.HasLargerArmies());

		city.RemoveBuilding(city.constructed_buildings[0]);
		Assert.False(them.HasLargerArmies());
	}

	[Fact]
	public void ProductionContextAgreesWithTheDirectCheck() {
		Player player = MakePlayer();
		City city = MakeCity(player);
		City other = MakeCity(player);
		Building barracks = MakeBuilding("Barracks");
		Building temple = MakeBuilding("Temple");
		Building cathedral = MakeBuilding("Cathedral");
		cathedral.requiredBuilding = temple;
		Building colossus = MakeBuilding("Colossus", greatWonder: true);
		Building pyramids = MakeBuilding("Pyramids", greatWonder: true);
		Building academy = MakeBuilding("Military Academy", SaveBuilding.Flag.AllowsBuildArmy);
		academy.isSmallWonder = true;
		Building pentagon = MakeBuilding("The Pentagon", SaveBuilding.Flag.AllowsLargerArmies);
		pentagon.isSmallWonder = true;
		Building[] all = [barracks, temple, cathedral, colossus, pyramids, academy, pentagon];

		void AssertAgree() {
			HashSet<Resource> resources = new();
			Building.ProductionContext context = new(city);
			foreach (Building b in all) {
				Assert.Equal(b.CanProduce(city, resources), b.CanProduce(city, resources, context));
			}
		}

		AssertAgree();
		Assert.True(barracks.CanProduce(city, new()));
		Assert.False(cathedral.CanProduce(city, new()));

		city.AddBuilding(barracks);
		city.AddBuilding(temple);
		other.SetItemBeingProduced(colossus);
		other.SetItemBeingProduced(academy);
		city.SetItemBeingProduced(pyramids);
		other.AddBuilding(pentagon);
		AssertAgree();
		Assert.False(barracks.CanProduce(city, new()));
		Assert.True(cathedral.CanProduce(city, new()));
		Assert.False(academy.CanProduce(city, new()));
		Assert.False(pentagon.CanProduce(city, new()));
		// This city building it doesn't take it off its own options.
		Assert.True(pyramids.CanProduce(city, new()));

		other.SetItemBeingProduced(colossus);
		AssertAgree();
		Assert.False(colossus.CanProduce(city, new()));
		Assert.True(academy.CanProduce(city, new()));
	}

	[Fact]
	public void AvailableTechsEnumerateCheapestFirst() {
		Player player = MakePlayer();
		player.eraCivilopediaName = "ERAS_Ancient_Times";
		List<Tech> techs = new();
		int[] costs = [30, 10, 50, 20, 40, 15, 25, 35];
		for (int i = 0; i < costs.Length; ++i) {
			techs.Add(new Tech { id = ID.FromString($"tech-{i}"), Cost = costs[i], EraCivilopediaName = "ERAS_Ancient_Times" });
		}

		OrderedTechSet available = player.GetAvailableTechsToResearch(techs);

		List<int> expected = costs.OrderBy(c => c).ToList();
		Assert.Equal(expected, available.Select(t => t.Cost).ToList());
		List<int> enumerated = new();
		foreach (Tech t in available) {
			enumerated.Add(t.Cost);
		}
		Assert.Equal(expected, enumerated);
		Assert.Equal(10, available.FirstOrDefault().Cost);
		Assert.Contains(techs[0], available);
		Assert.Equal(costs.Length, available.Count);
	}

	[Fact]
	public void TechLookupsMatchALinearSearch() {
		List<Tech> techs = new();
		for (int i = 0; i < 10; ++i) {
			techs.Add(new Tech { id = ID.FromString($"tech-{i}"), EnablesBridges = i == 6 });
		}

		foreach (Tech t in techs) {
			Assert.Same(t, Tech.FindById(techs, ID.FromString(t.id.ToString())));
		}
		Assert.Null(Tech.FindById(techs, ID.FromString("tech-99")));
		Assert.Null(Tech.FindById(techs, null));
		Assert.Same(techs[6], Tech.FindBridgeTech(techs));

		// A different list is indexed afresh.
		List<Tech> others = new() { new Tech { id = ID.FromString("tech-3") } };
		Assert.Same(others[0], Tech.FindById(others, ID.FromString("tech-3")));
		Assert.Null(Tech.FindBridgeTech(others));
		Assert.Same(techs[3], Tech.FindById(techs, ID.FromString("tech-3")));

		// As is the same list once it grows.
		techs.Add(new Tech { id = ID.FromString("tech-10") });
		Assert.Same(techs[10], Tech.FindById(techs, ID.FromString("tech-10")));
	}

	[Fact]
	public void BridgingRoadsNeedsTheBridgeTech() {
		Player player = MakePlayer();
		gameData.techs.Add(new Tech { id = ID.FromString("tech-1") });
		gameData.techs.Add(new Tech { id = ID.FromString("tech-2"), EnablesBridges = true });

		Assert.False(player.CanBridgeRoads());
		player.knownTechs.Add(ID.FromString("tech-1"));
		Assert.False(player.CanBridgeRoads());
		player.knownTechs.Add(ID.FromString("tech-2"));
		Assert.True(player.CanBridgeRoads());
	}

	[Fact]
	public void ResearchQueueListsUnknownPrerequisitesOnce() {
		Player player = MakePlayer();
		Tech bronze = new() { id = ID.FromString("tech-1"), Cost = 1 };
		Tech alphabet = new() { id = ID.FromString("tech-2"), Cost = 2 };
		Tech writing = new() { id = ID.FromString("tech-3"), Cost = 3, Prerequisites = { alphabet } };
		Tech currency = new() { id = ID.FromString("tech-4"), Cost = 4, Prerequisites = { bronze } };
		Tech trade = new() { id = ID.FromString("tech-5"), Cost = 5, Prerequisites = { writing, currency, alphabet } };
		gameData.techs.AddRange([bronze, alphabet, writing, currency, trade]);
		player.knownTechs.Add(bronze.id);

		player.CalculateFreshTechQueueAndAssignNewCurrent(trade);

		List<Tech> queue = player.ResearchQueue.ToList();
		Assert.Equal(queue.Count, queue.Distinct().Count());
		Assert.DoesNotContain(bronze, queue);
		Assert.Equal(trade, queue[^1]);
		Assert.Equal(4, queue.Count);

		player.CalculateTechQueueAndAppendToCurrentQueue(trade);
		Assert.Equal(4, player.ResearchQueue.Count);
	}

	[Fact]
	public void GettingTheRawTimeHasNoSideEffects() {
		TimeOptions options = new();
		int before = options.currentYear;

		Assert.Equal(-3950, options.GetRawNumber(1));
		Assert.Equal(-2750, options.GetRawNumber(25));

		Assert.Equal(before, options.currentYear);
	}

	[Fact]
	public void TurnScoreIsComputedWhenFirstRead() {
		int computed = 0;
		VictoryStatus status = new();
		status.SetTurnScoreSource(() => { ++computed; return 7; });
		Assert.Equal(0, computed);

		Assert.Equal(7, status.TurnScore);
		Assert.Equal(7, status.TurnScore);
		Assert.Equal(1, computed);

		status.SetTurnScoreSource(() => 9);
		status.TurnScore = 3;
		Assert.Equal(3, status.TurnScore);
	}

	[Fact]
	public void HistoryKeyFollowsTheId() {
		Player player = MakePlayer();
		player.id = ID.FromString("player-1");
		Assert.Equal("player-1", player.HistoryKey);
		player.id = ID.FromString("player-2");
		Assert.Equal("player-2", player.HistoryKey);
	}

	[Fact]
	public void SneakAttacksNeedOurUnitOnTopInTheirKnownTerritory() {
		Player us = MakePlayer("Us");
		Player them = MakePlayer("Them");
		City theirCity = MakeCity(them);
		Tile theirTile = new(ID.None("tile")) { owningCity = theirCity };
		UnitPrototype warrior = new() { name = "Warrior" };
		MapUnit unit = warrior.GetInstance(ID.None("unit"), warrior, us, location: theirTile);
		unit.nationality = us.civilization;
		theirTile.unitsOnTile.Add(unit);
		us.AddUnit(unit);

		// They don't know the tile.
		Assert.False(IsSneakAttack(us, them));

		them.tileKnowledge.knownTiles.Add(theirTile);
		Assert.True(IsSneakAttack(us, them));

		// Their unit is on top.
		MapUnit theirs = warrior.GetInstance(ID.None("unit"), warrior, them, location: theirTile);
		theirTile.unitsOnTile.Insert(0, theirs);
		Assert.False(IsSneakAttack(us, them));
	}

	private static bool IsSneakAttack(Player us, Player them) {
		return (bool)typeof(Player)
			.GetMethod("IsASneakAttackOn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
			.Invoke(us, [them]);
	}

	[Fact]
	public void DisbandingPicksTheSameRandomUnitAsBefore() {
		Player player = MakePlayer();
		player.government = new Government() { unitCost = 2 };
		City city = MakeCity(player);
		Tile worked = new(ID.None("tile")) { overlayTerrainType = new TerrainType() };
		city.residents.Add(new CityResident() { city = city, tileWorked = worked, citizenType = new CitizenType() { IsDefaultCitizen = true } });

		UnitPrototype warrior = new() { name = "Warrior" };
		Civilization foreign = new("Foreign");
		List<MapUnit> nonCaptives = new();
		for (int i = 0; i < 6; ++i) {
			MapUnit unit = warrior.GetInstance(ID.FromString($"unit-{i}"), warrior, player, location: city.location);
			// Every other unit is a captive, which is free and never disbanded.
			unit.nationality = i % 2 == 0 ? player.civilization : foreign;
			city.location.unitsOnTile.Add(unit);
			player.AddUnit(unit);
			gameData.mapUnits.Add(unit);
			if (i % 2 == 0) {
				nonCaptives.Add(unit);
			}
		}

		// Support costs 6 gold a turn. Leave the treasury one gold short, so
		// exactly one unit has to go.
		int goldPerTurn = player.CalculateGoldPerTurn();
		Assert.Equal(6, player.AggregateFlows().unitSupport);
		Assert.True(goldPerTurn < -1, $"gpt {goldPerTurn}");
		player.gold = -goldPerTurn - 1;

		C7GameData.GameData.rng = new Random(1234);
		MapUnit expected = nonCaptives[new Random(1234).Next(nonCaptives.Count)];

		player.DoPerTurnFinanceUpdates(gameData);

		Assert.Equal(5, player.units.Count);
		Assert.DoesNotContain(expected, player.units);
		// One gold short, then two gold a turn saved.
		Assert.Equal(1, player.gold);
	}

	[Fact]
	public void AggregateFlowsSplitsTaxmenFromCitizens() {
		Player player = MakePlayer();
		player.taxRate = 10;
		player.scienceRate = 0;
		player.luxuryRate = 0;
		City city = MakeCity(player);
		Tile worked = new(ID.None("tile")) { overlayTerrainType = new TerrainType() { baseCommerceProduction = 4 } };
		city.residents.Add(new CityResident() { city = city, tileWorked = worked, citizenType = new CitizenType() { IsDefaultCitizen = true } });
		city.residents.Add(new CityResident() { city = city, tileWorked = worked, citizenType = new CitizenType() { Taxes = 2 } });

		PlayerCommerceBreakdown flows = player.AggregateFlows();

		Assert.Equal(2, flows.taxmenTaxes);
		Assert.Equal(city.CurrentCommerceYield().taxes - 2, flows.taxes);
		Assert.Equal(flows.Netflows(), player.CalculateGoldPerTurn());
	}
}
