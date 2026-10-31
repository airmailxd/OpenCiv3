using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class CityCaptureTest : IClassFixture<SaveGameFixture>, System.IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player attacker;
	private readonly Player defender;

	public CityCaptureTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler)).ToArray();
		attacker = civs[0];
		defender = civs[1];
	}

	// Captures send messages to the UI; don't leave them for other tests.
	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private City FoundCity(Player player, int size) {
		Tile tile = player.units.First(u => u.unitType.isSettler).location;
		City city = CityInteractions.BuildCity(tile, player, player.GetNextCityName());
		while (city.residents.Count < size) {
			city.AddCitizen(new CityResident() {
				city = city,
				citizenType = city.residents[0].citizenType,
				tileWorked = city.residents[0].tileWorked,
			});
		}
		return city;
	}

	private static List<MsgShowMilitaryAdvisorPopup> MilitaryNewsFor(Player player) {
		List<MsgShowMilitaryAdvisorPopup> news = new();
		while (EngineStorage.TryDequeueNextMessageToUI(out MessageToUI msg)) {
			if (msg is MsgShowMilitaryAdvisorPopup n && n.recipient == player) {
				news.Add(n);
			}
		}
		return news;
	}

	// The news is addressed by one of the government's ruler titles, picked
	// at random; these tests give it just the one.
	private string Title() {
		defender.government.maleRulerTitles = ["Your Majesty"];
		defender.government.femaleRulerTitles = ["Your Majesty"];
		return "Your Majesty";
	}

	// Civ3's THEY_CAPTURE_CITY_GOLD (script.txt), with "#map_center 1": the
	// player who lost the city is told where it is (see LossNews).
	[Fact]
	public void ThePlayerWhoLostACityIsToldWhereItIs() {
		defender.isHuman = true;
		string title = Title();
		FoundCity(attacker, 1);
		City city = FoundCity(defender, 3);
		defender.gold = 100;
		int plunder = CityInteractions.CaptureGold(city, defender);
		Assert.True(plunder > 0);
		MilitaryNewsFor(defender);

		CityInteractions.CaptureCity(city, attacker);

		MsgShowMilitaryAdvisorPopup news = Assert.Single(MilitaryNewsFor(defender));
		Assert.Equal($"Terrible news, {title}!\n{city.name} was captured by the evil {attacker.civilization.noun}!\n"
			+ $"They have stolen {plunder} gold from its vaults.", news.message);
		Assert.Equal(city.location, news.location);
		Assert.False(news.supersedesNewsHere);
	}

	// CITY_LOST_WITH_WONDER: a great wonder survives the capture, and the
	// news names it.
	[Fact]
	public void TheNewsOfACityLostWithAWonderNamesIt() {
		defender.isHuman = true;
		FoundCity(attacker, 1);
		City city = FoundCity(defender, 3);
		Building wonder = gameData.Buildings.First(b => b.IsGreatWonder());
		city.AddBuilding(wonder);
		MilitaryNewsFor(defender);

		CityInteractions.CaptureCity(city, attacker);

		MsgShowMilitaryAdvisorPopup news = Assert.Single(MilitaryNewsFor(defender));
		Assert.Contains($"The evil {attacker.civilization.noun} have captured {city.name} -- along with {wonder.name}!", news.message);
		Assert.EndsWith("We must make them pay!", news.message);
	}

	// THEY_DESTROY_CITY_GOLD, for a city destroyed as it is taken.
	[Fact]
	public void ThePlayerWhoseCityIsDestroyedAsItsTakenIsToldSo() {
		defender.isHuman = true;
		string title = Title();
		FoundCity(attacker, 1);
		City city = FoundCity(defender, 1);
		Assert.False(CityInteractions.SurvivesCapture(city));
		Tile tile = city.location;
		MilitaryNewsFor(defender);

		CityInteractions.CaptureCity(city, attacker);

		Assert.False(tile.HasCity());
		MsgShowMilitaryAdvisorPopup news = Assert.Single(MilitaryNewsFor(defender));
		Assert.Equal($"Terrible news, {title}!\nThe evil {attacker.civilization.noun} have stolen 0 gold from {city.name} and burned it to the ground!\n"
			+ "They should pay dearly for this atrocity!", news.message);
		Assert.Equal(tile, news.location);
	}

	// THEY_RAZE_CITY_GOLD: razing a city just taken tells the civ it was
	// taken from, with the gold taken with it, and replaces the news of the
	// capture if they've yet to see it.
	[Fact]
	public void ThePlayerWhoseCityIsRazedIsToldSo() {
		defender.isHuman = true;
		string title = Title();
		FoundCity(attacker, 1);
		City city = FoundCity(defender, 3);
		defender.gold = 100;
		int plunder = CityInteractions.CaptureGold(city, defender);
		CityInteractions.CaptureCity(city, attacker);
		Tile tile = city.location;
		MilitaryNewsFor(defender);

		CityInteractions.RazeCity(city);

		MsgShowMilitaryAdvisorPopup news = Assert.Single(MilitaryNewsFor(defender));
		Assert.StartsWith($"Terrible news, {title}!\n", news.message);
		Assert.Contains($"The evil {attacker.civilization.noun} have stolen {plunder} gold from {city.name} and burned it to the ground!", news.message);
		Assert.Equal(tile, news.location);
		Assert.True(news.supersedesNewsHere);
	}

	[Fact]
	public void CapturedCityChangesHandsAndShrinks() {
		FoundCity(attacker, 1);
		City city = FoundCity(defender, 3);
		Assert.True(city.IsCapital());

		CityInteractions.CaptureCity(city, attacker);

		Assert.Equal(attacker, city.owner);
		Assert.Contains(city, attacker.cities);
		Assert.DoesNotContain(city, defender.cities);
		Assert.Equal(2, city.residents.Count);
		Assert.False(city.IsCapital());
		Assert.DoesNotContain(city.constructed_buildings, cb => cb.building.isCenterOfEmpire);
		Assert.Equal(city, city.location.cityAtTile);
	}

	// Flintlock, reading the 1.22 code (C3X thread post-16726482): "If it's
	// the last city your opponent has, you get their entire treasury";
	// otherwise the treasury over their city count, times "0.5 for cities
	// of level 1 (towns), 0.75 for level 2, and 1.0 for level 3".
	[Fact]
	public void CaptureGoldDependsOnTheCitySizeLevelAndTheLastCityGivesAll() {
		FoundCity(attacker, 1);
		City city = FoundCity(defender, 3);
		defender.gold = 400;
		attacker.gold = 0;
		List<City> others = new();
		for (int i = 0; i < 3; ++i) {
			others.Add(new City(Tile.NONE, defender, $"Other {i}", ID.None("city")));
		}
		defender.cities.AddRange(others);
		try {
			Assert.Equal(50, CityInteractions.CaptureGold(city, defender));
			while (city.residents.Count < 7) {
				city.AddCitizen(new CityResident() { city = city, citizenType = city.residents[0].citizenType });
			}
			Assert.Equal(75, CityInteractions.CaptureGold(city, defender));
			while (city.residents.Count < 13) {
				city.AddCitizen(new CityResident() { city = city, citizenType = city.residents[0].citizenType });
			}
			Assert.Equal(100, CityInteractions.CaptureGold(city, defender));
		} finally {
			foreach (City other in others) {
				defender.cities.Remove(other);
			}
		}

		CityInteractions.CaptureCity(city, attacker);

		Assert.Equal(400, attacker.gold);
		Assert.Equal(0, defender.gold);
	}

	[Fact]
	public void CapturingASizeOneCityDestroysIt() {
		FoundCity(attacker, 1);
		City city = FoundCity(defender, 1);
		Tile tile = city.location;

		CityInteractions.CaptureCity(city, attacker);

		Assert.Null(tile.cityAtTile);
		Assert.DoesNotContain(city, attacker.cities);
		Assert.DoesNotContain(city, gameData.cities);
	}

	// A captured city comes without a palace, so a civ whose only city was
	// captured gets its palace in the first city it founds, which then has
	// no corruption.
	[Fact]
	public void FirstFoundedCityAfterACaptureIsTheCapital() {
		City captured = FoundCity(defender, 3);
		CityInteractions.CaptureCity(captured, attacker);
		Assert.False(captured.IsCapital());

		City founded = FoundCity(attacker, 1);

		Assert.True(founded.IsCapital());
		Assert.Contains(founded.constructed_buildings, cb => cb.building.isCenterOfEmpire);
		Assert.Equal(0, founded.corruption);
		Assert.Equal(0, founded.CurrentProductionYield().corrupt);
	}

	[Fact]
	public void SizeOneCityWithExpandedBordersIsKeptWithTheCaptorsBorders() {
		FoundCity(attacker, 1);
		City city = FoundCity(defender, 1);
		city.perPlayerCulture[defender] = 25;
		gameData.UpdateTileOwners();
		Assert.Equal(2, city.GetBorderExpansionLevel());
		List<Tile> outerRing = city.GetTilesWithinBorders().Where(t => t != Tile.NONE && t.owningCity == city).ToList();

		CityInteractions.CaptureCity(city, attacker);
		List<Tile> innerTiles = city.GetTilesWithinBorders();
		outerRing.RemoveAll(innerTiles.Contains);
		Assert.NotEmpty(outerRing);
		Assert.All(outerRing, t => Assert.NotEqual(city, t.owningCity));

		Assert.Equal(attacker, city.owner);
		Assert.Equal(city, city.location.cityAtTile);
		Assert.Single(city.residents);
		Assert.Equal(1, city.GetBorderExpansionLevel());
		Assert.Equal(25, city.GetCultureFor(defender));
	}

	[Fact]
	public void LosingTheCapitalMovesThePalace() {
		FoundCity(attacker, 1);
		City capital = FoundCity(defender, 3);
		Tile secondSite = defender.units.First(u => u.unitType.isSettler).location.neighbors.Values
			.First(t => t.IsLand() && !t.HasCity());
		City other = CityInteractions.BuildCity(secondSite, defender, defender.GetNextCityName());
		Assert.False(other.IsCapital());

		CityInteractions.CaptureCity(capital, attacker);

		Assert.True(other.IsCapital());
		Assert.Contains(other.constructed_buildings, cb => cb.building.isCenterOfEmpire);
	}

	// A captured city belongs to its captor, so units resting there heal at
	// the city rate even though the civ it was taken from is still an enemy.
	[Fact]
	public void UnitsHealAtTheCityRateInACapturedCity() {
		FoundCity(attacker, 1);
		City city = FoundCity(defender, 3);
		attacker.DeclareWarOn(defender, gameData.turn);
		CityInteractions.CaptureCity(city, attacker);

		MapUnit unit = gameData.SpawnUnit(attacker, gameData.unitPrototypes.Single(p => p.name == "Warrior"), city.location);
		unit.hitPointsRemaining = 1;
		unit.Fortify();

		Assert.Equal(gameData.healRateInCity, unit.HealRateAt(city.location));
		unit.OnBeginTurn();
		Assert.Equal(1 + gameData.healRateInCity, unit.hitPointsRemaining);
	}

	[Fact]
	public void RebuiltCityContinuesDownTheNameList() {
		FoundCity(attacker, 1);
		City capital = FoundCity(defender, 3);
		Tile secondSite = defender.units.First(u => u.unitType.isSettler).location.neighbors.Values
			.First(t => t.IsLand() && !t.HasCity());
		City other = CityInteractions.BuildCity(secondSite, defender, defender.GetNextCityName());

		CityInteractions.CaptureCity(capital, attacker);

		Assert.Equal(defender.civilization.cityNames[2], defender.GetNextCityName());
		Assert.NotEqual(capital.name, defender.GetNextCityName());
		Assert.NotEqual(other.name, defender.GetNextCityName());
	}
}
