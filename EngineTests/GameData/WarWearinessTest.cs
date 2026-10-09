using C7GameData;
using C7GameData.Save;
using Xunit;

namespace EngineTests.GameData;

public class WarWearinessTest {
	private static City MakeCity(int warWearinessLevel, int weariness, int size = 8) {
		C7Engine.EngineStorage.InitializeGameDataForTests(new C7GameData.GameData());
		Player player = new() {
			civilization = new Civilization(),
			government = new Government() { warWeariness = warWearinessLevel },
			warWeariness = weariness,
		};
		City city = new(Tile.NONE, player, "Weary", ID.None("city"));
		player.cities.Add(city);
		for (int i = 0; i < size; ++i) {
			city.residents.Add(new CityResident());
		}
		return city;
	}

	[Fact]
	public void GovernmentsWithoutWarWearinessIgnoreIt() {
		City city = MakeCity(warWearinessLevel: 0, weariness: 100);
		Assert.Equal(0, city.owner.WarWearinessUnhappiness(city));
	}

	[Fact]
	public void HighWarWearinessHurtsMoreThanLow() {
		City low = MakeCity(warWearinessLevel: 1, weariness: 40);
		City high = MakeCity(warWearinessLevel: 2, weariness: 40);
		Assert.Equal(2, low.owner.WarWearinessUnhappiness(low));
		Assert.Equal(4, high.owner.WarWearinessUnhappiness(high));
	}

	[Fact]
	public void PoliceStationsHalveWarWeariness() {
		City city = MakeCity(warWearinessLevel: 2, weariness: 40);
		SaveBuilding police = new() { name = "Police Station" };
		police.flags.Add(SaveBuilding.Flag.ReducesWarWeariness);
		city.AddBuilding(new Building(police, new C7GameData.GameData()));
		Assert.Equal(2, city.owner.WarWearinessUnhappiness(city));
	}

	[Fact]
	public void UnhappinessIsCappedAtCitySize() {
		City city = MakeCity(warWearinessLevel: 2, weariness: 1000, size: 3);
		Assert.Equal(3, city.owner.WarWearinessUnhappiness(city));
	}

	[Fact]
	public void WarWearinessFadesAtPeace() {
		City city = MakeCity(warWearinessLevel: 2, weariness: 50);
		C7GameData.GameData gameData = new();
		gameData.players.Add(city.owner);

		// It lingers for a while...
		city.owner.UpdateWarWeariness(gameData);
		Assert.InRange(city.owner.warWeariness, 1, 49);

		// ...but is gone before long.
		for (int i = 0; i < 20; ++i) {
			city.owner.UpdateWarWeariness(gameData);
		}
		Assert.Equal(0, city.owner.warWeariness);
	}
}
