using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;
using Row = C7GameData.DemographicsReport.Row;

namespace EngineTests.GameData;

public class DemographicsReportTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;

	public DemographicsReportTest(SaveGameFixture fixture) {
		this.fixture = fixture;
	}

	[Fact]
	public void TheRowsAreCiv3sInItsOrder() {
		// Conquests/Text/labels.txt, #DEMO_WIN.
		Assert.Equal(["Approval Rating", "Population", "GNP", "Mfg. Goods", "Land Area", "Literacy", "Disease",
			"Pollution", "Life Expectancy", "Family Size", "Military Service", "Annual Income", "Productivity"],
			DemographicsReport.Labels);
	}

	[Fact]
	public void Rank_TiedCivsShareARankAndTheNextSkipsIt() {
		long[] all = [30, 50, 50, 10];
		Assert.Equal(1, DemographicsReport.Rank(50, all, lowerIsBetter: false));
		Assert.Equal(3, DemographicsReport.Rank(30, all, lowerIsBetter: false));
		Assert.Equal(4, DemographicsReport.Rank(10, all, lowerIsBetter: false));
	}

	[Fact]
	public void Rank_LessIsBetterForDiseaseAndPollution() {
		long[] all = [30, 50, 10];
		Assert.Equal(1, DemographicsReport.Rank(10, all, DemographicsReport.LowerIsBetter(Row.Pollution)));
		Assert.Equal(3, DemographicsReport.Rank(50, all, DemographicsReport.LowerIsBetter(Row.Disease)));
		Assert.Equal(1, DemographicsReport.Rank(50, all, DemographicsReport.LowerIsBetter(Row.GNP)));
	}

	[Theory]
	[InlineData(Row.ApprovalRating, 75, "75%")]
	[InlineData(Row.Population, 1250000, "1,250,000")]
	[InlineData(Row.GNP, 1234, "$1,234 million")]
	[InlineData(Row.MfgGoods, 40, "40 megatons")]
	[InlineData(Row.LandArea, 3200, "3,200 square miles")]
	[InlineData(Row.Pollution, 2, "2 tons")]
	[InlineData(Row.LifeExpectancy, 46, "46 years")]
	[InlineData(Row.FamilySize, 2, "2 children")]
	[InlineData(Row.AnnualIncome, 4, "$4 per capita")]
	public void Format_UsesCiv3sUnits(Row row, long value, string expected) {
		Assert.Equal(expected, DemographicsReport.Format(row, value));
	}

	[Theory]
	[InlineData(1, "1st")]
	[InlineData(2, "2nd")]
	[InlineData(3, "3rd")]
	[InlineData(4, "4th")]
	[InlineData(11, "11th")]
	[InlineData(12, "12th")]
	[InlineData(21, "21st")]
	public void Ordinal(int n, string expected) {
		Assert.Equal(expected, DemographicsReport.Ordinal(n));
	}

	private static Player Civ(string id, string name, params long[] values) {
		return new Player {
			id = ID.FromString(id),
			civilization = new Civilization(name),
			hostFacts = new PlayerFacts { demographics = values },
		};
	}

	private static long[] Values(long first, long disease) {
		long[] v = new long[DemographicsReport.RowCount];
		v[0] = first;
		v[(int)Row.Disease] = disease;
		return v;
	}

	// Each row ranks the viewer among every civ still in the game, met or
	// not, and leaves out the barbarians and the defeated.
	[Fact]
	public void For_RanksTheViewerAmongTheCivsLeft() {
		Player us = Civ("player-1", "Rome", Values(60, 5));
		Player greece = Civ("player-2", "Greece", Values(80, 2));
		Player egypt = Civ("player-3", "Egypt", Values(60, 9));
		Player defeated = Civ("player-4", "Babylon", Values(100, 0));
		defeated.defeated = true;
		Player barbarians = Civ("player-5", "Barbarians", Values(100, 0));
		barbarians.civilization.isBarbarian = true;
		C7GameData.GameData gameData = new();
		gameData.players.AddRange([us, greece, egypt, defeated, barbarians]);

		List<DemographicsLine> lines = DemographicsReport.For(gameData, us);

		Assert.Equal(DemographicsReport.RowCount, lines.Count);
		Assert.Equal("Approval Rating", lines[0].label);
		Assert.Equal("60%", lines[0].text);
		// Greece is ahead, and Egypt tied with us.
		Assert.Equal(2, lines[0].rank);
		// Less disease is better: Greece has less, Egypt more.
		Assert.Equal(2, lines[(int)Row.Disease].rank);
	}

	private (C7GameData.GameData gameData, Player player, City city) GameWithACity(int size) {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player player = gameData.players.First(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler));
		Tile tile = player.units.First(u => u.unitType.isSettler).location;
		City city = CityInteractions.BuildCity(tile, player, player.GetNextCityName());
		CitizenType laborer = gameData.citizenTypes.First(c => c.IsDefaultCitizen);
		city.residents.Clear();
		for (int i = 0; i < size; ++i) {
			city.residents.Add(new CityResident { citizenType = laborer, tileWorked = Tile.NONE });
		}
		return (gameData, player, city);
	}

	private static Building Named(C7GameData.GameData gameData, string name) => gameData.Buildings.First(b => b.name == name);

	// A third for each of a library, a university and a research lab, all
	// for a scientific great wonder, half for the Apollo Program.
	[Fact]
	public void LiteracyShare_CountsTheScientificBuildings() {
		(C7GameData.GameData gameData, _, City city) = GameWithACity(4);
		Assert.Equal(0, DemographicsReport.LiteracyShare(city));
		city.AddBuilding(Named(gameData, "Library"));
		Assert.Equal(1 / 3.0, DemographicsReport.LiteracyShare(city), 6);
		city.AddBuilding(Named(gameData, "Apollo Program"));
		Assert.Equal(0.5, DemographicsReport.LiteracyShare(city), 6);
		city.AddBuilding(Named(gameData, "University"));
		Assert.Equal(2 / 3.0, DemographicsReport.LiteracyShare(city), 6);
		city.AddBuilding(Named(gameData, "The Great Library"));
		Assert.Equal(1, DemographicsReport.LiteracyShare(city), 6);
	}

	[Fact]
	public void LiteracyShare_CopernicusDoesNotCount() {
		(C7GameData.GameData gameData, _, City city) = GameWithACity(4);
		city.AddBuilding(Named(gameData, "Copernicus' Observatory"));
		Assert.Equal(0, DemographicsReport.LiteracyShare(city));
	}

	[Fact]
	public void TheValuesOfACivWithOneCity() {
		(C7GameData.GameData gameData, Player player, City city) = GameWithACity(4);
		city.residents[0].mood = CityResident.Mood.Happy;
		city.residents[1].mood = CityResident.Mood.Happy;
		city.residents[2].mood = CityResident.Mood.Content;
		city.residents[3].mood = CityResident.Mood.Unhappy;
		city.AddBuilding(Named(gameData, "Granary"));
		city.AddBuilding(Named(gameData, "Library"));
		city.foodStored = 0;

		List<Player> civs = [player];
		long[] v = DemographicsReport.ValuesOf(gameData, civs)[player];

		// 50 + (2 happy - 1 unhappy) / 4 citizens / 2 * 100.
		Assert.Equal(62, v[(int)Row.ApprovalRating]);
		// (1 + 2 + 3 + 4) * 10,000 with an empty food box.
		Assert.Equal(100000, v[(int)Row.Population]);
		// 20 + a third of the citizens * 80.
		Assert.Equal(46, v[(int)Row.LifeExpectancy]);
		// A third, without Literature.
		Assert.Equal(33, v[(int)Row.Literacy]);
		// 10 years * the military units / 4 citizens.
		int military = player.units.Count(u => u.unitType.attack > 0 && u.unitType.defense > 0);
		Assert.Equal(10 * military / 4, v[(int)Row.MilitaryService]);
		// Each territory tile is 100 square miles.
		int territory = gameData.map.tiles.Count(t => t.OwningPlayer() == player);
		Assert.True(territory > 0);
		Assert.Equal(territory * 100, v[(int)Row.LandArea]);
		Assert.True(v[(int)Row.AnnualIncome] >= 1);
		Assert.True(v[(int)Row.FamilySize] >= 1);
	}

	// The cities with the most culture, the bigger first when even.
	[Fact]
	public void TopCities_RanksByCultureThenSize() {
		(C7GameData.GameData gameData, Player player, City city) = GameWithACity(2);
		Player rival = gameData.players.First(p => p != player && !p.isBarbarians && p.units.Any(u => u.unitType.isSettler));
		City theirs = CityInteractions.BuildCity(rival.units.First(u => u.unitType.isSettler).location, rival, rival.GetNextCityName());
		theirs.residents.Add(new CityResident { citizenType = city.residents[0].citizenType, tileWorked = Tile.NONE });
		theirs.residents.Add(new CityResident { citizenType = city.residents[0].citizenType, tileWorked = Tile.NONE });
		theirs.residents.Add(new CityResident { citizenType = city.residents[0].citizenType, tileWorked = Tile.NONE });
		city.perPlayerCulture[player] = 50;
		theirs.perPlayerCulture[rival] = 50;

		Assert.Equal([theirs, city], DemographicsReport.TopCities(gameData).Take(2));

		city.perPlayerCulture[player] = 60;
		Assert.Equal([city, theirs], DemographicsReport.TopCities(gameData).Take(2));
		Assert.True(DemographicsReport.TopCities(gameData).Count <= 5);
	}
}
