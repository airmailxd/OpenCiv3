using System.Linq;
using C7GameData;
using C7GameData.Save;
using Xunit;

namespace EngineTests.GameData;

// Great wonders that make citizens content: in their own city, in every city
// of the owner (or only those on the wonder's continent), and by doubling the
// effect of a building (the Oracle for Temples, the Sistine Chapel for
// Cathedrals).
public class HappinessWonderTest {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly City home, other, overseas;
	private readonly CitizenType laborer = new() { IsDefaultCitizen = true };

	public HappinessWonderTest() {
		// Nobody is born content, so every content citizen is down to the
		// buildings and wonders.
		gameData = new C7GameData.GameData() {
			gameDifficulty = new Difficulty() { NumberOfCitizensBornContent = 0 },
		};
		C7Engine.EngineStorage.InitializeGameDataForTests(gameData);

		us = new() { civilization = new Civilization("Rome"), id = ID.FromString("player-1"), government = new Government() };
		gameData.players.Add(us);

		home = MakeCity("Rome", continent: 1);
		other = MakeCity("Antium", continent: 1);
		overseas = MakeCity("Carthage", continent: 2);
	}

	private City MakeCity(string name, int continent) {
		City city = new(new Tile(ID.None(name + " tile")) { continent = continent }, us, name, ID.None(name));
		us.cities.Add(city);
		for (int i = 0; i < 10; ++i) {
			city.residents.Add(new CityResident() { citizenType = laborer, tileWorked = Tile.NONE });
		}
		return city;
	}

	private static Building MakeBuilding(string name, int contentFaces = 0, int contentFacesAllCities = 0,
			bool wonder = false, bool continental = false) {
		SaveBuilding sb = new() { name = name, contentFacesInCity = contentFaces, contentFacesAllCities = contentFacesAllCities };
		if (wonder) {
			sb.greatWonderProperties = new();
		}
		if (continental) {
			sb.flags.Add(SaveBuilding.Flag.ContinentalMoodEffects);
		}
		return new Building(sb, new C7GameData.GameData());
	}

	private int Content(City city) {
		city.RecalculateCitizenMoods(gameData);
		return city.residents.Count(r => r.mood == CityResident.Mood.Content);
	}

	[Fact]
	public void HangingGardensMakeOneContentInEveryOtherCity() {
		home.AddBuilding(MakeBuilding("The Hanging Gardens", contentFaces: 3, contentFacesAllCities: 1, wonder: true));

		Assert.Equal(3, Content(home));
		Assert.Equal(1, Content(other));
		Assert.Equal(1, Content(overseas));
	}

	[Fact]
	public void ContinentalWonderOnlyReachesItsContinent() {
		home.AddBuilding(MakeBuilding("JS Bach's Cathedral", contentFaces: 2, contentFacesAllCities: 2, wonder: true, continental: true));

		Assert.Equal(2, Content(home));
		Assert.Equal(2, Content(other));
		Assert.Equal(0, Content(overseas));
	}

	[Fact]
	public void AllCityWondersStack() {
		home.AddBuilding(MakeBuilding("The Hanging Gardens", contentFaces: 3, contentFacesAllCities: 1, wonder: true));
		other.AddBuilding(MakeBuilding("Cure for Cancer", contentFaces: 1, contentFacesAllCities: 1, wonder: true));

		Assert.Equal(4, Content(home));
		Assert.Equal(2, Content(other));
		Assert.Equal(2, Content(overseas));
	}

	[Fact]
	public void ObsoleteWonderStopsWorking() {
		Tech electricity = new() { id = ID.FromString("tech-45") };
		Building gardens = MakeBuilding("The Hanging Gardens", contentFaces: 3, contentFacesAllCities: 1, wonder: true);
		gardens.renderedObsoleteBy = electricity;
		home.AddBuilding(gardens);
		Assert.Equal(3, Content(home));

		us.knownTechs.Add(electricity.id);

		Assert.Equal(0, Content(home));
		Assert.Equal(0, Content(other));
	}

	[Fact]
	public void OracleDoublesTemplesInAllCities() {
		Building temple = MakeBuilding("Temple", contentFaces: 1);
		Building oracle = MakeBuilding("The Oracle", wonder: true);
		oracle.doublesHappinessOf = temple;
		home.AddBuilding(oracle);
		home.AddBuilding(temple);
		other.AddBuilding(temple);

		Assert.Equal(2, Content(home));
		Assert.Equal(2, Content(other));
		// No temple, nothing to double.
		Assert.Equal(0, Content(overseas));
	}

	[Fact]
	public void SistineChapelDoublesCathedrals() {
		Building cathedral = MakeBuilding("Cathedral", contentFaces: 3);
		Building sistine = MakeBuilding("Sistine Chapel", wonder: true);
		sistine.doublesHappinessOf = cathedral;
		home.AddBuilding(sistine);
		overseas.AddBuilding(cathedral);

		Assert.Equal(6, Content(overseas));
	}

	[Fact]
	public void OracleDoublesTemplesGrantedByTempleOfArtemis() {
		Building temple = MakeBuilding("Temple", contentFaces: 1);
		Building artemis = MakeBuilding("Temple of Artemis", wonder: true);
		artemis.greatWonderProperties.buildingGainedInEveryCityOnContinent = temple;
		Building oracle = MakeBuilding("The Oracle", wonder: true);
		oracle.doublesHappinessOf = temple;
		home.AddBuilding(artemis);
		home.AddBuilding(oracle);

		Assert.Equal(2, Content(other));
		Assert.Equal(0, Content(overseas));
	}

	[Fact]
	public void ShakespearesTheaterMakesEightContent() {
		home.AddBuilding(MakeBuilding("Shakespeare's Theater", contentFaces: 8, wonder: true));

		Assert.Equal(8, Content(home));
		Assert.Equal(0, Content(other));
	}
}
