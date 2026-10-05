using C7GameData;
using C7GameData.Save;
using Xunit;

namespace EngineTests.GameData;

public class GoldenAgeTest {
	private readonly C7GameData.GameData gameData = new() { rules = new Rules() { GoldenAgeDuration = 20 } };
	private readonly Player player;

	public GoldenAgeTest() {
		C7Engine.EngineStorage.InitializeGameDataForTests(gameData);
		Civilization egypt = new() { name = "Egypt" };
		egypt.traits.Add(Civilization.Trait.Industrious);
		egypt.traits.Add(Civilization.Trait.Religious);
		player = new() { civilization = egypt, government = new Government() };
	}

	private void BuildWonder(params Civilization.Trait[] traits) {
		SaveBuilding sb = new() { name = "Wonder", greatWonderProperties = new SaveBuilding.GreatWonderProperties() };
		sb.traits.UnionWith(traits);
		City city = new(Tile.NONE, player, "Thebes", ID.None("city"));
		player.cities.Add(city);
		city.AddBuilding(new Building(sb, new C7GameData.GameData()));
	}

	[Fact]
	public void WondersCoveringBothTraitsStartAGoldenAge() {
		BuildWonder(Civilization.Trait.Religious);
		player.MaybeStartGoldenAgeFromWonders(gameData);
		Assert.False(player.InGoldenAge);

		BuildWonder(Civilization.Trait.Industrious);
		player.MaybeStartGoldenAgeFromWonders(gameData);
		Assert.True(player.InGoldenAge);
		Assert.Equal(20, player.goldenAgeTurnsRemaining);
	}

	[Fact]
	public void ACivOnlyGetsOneGoldenAge() {
		player.StartGoldenAge(gameData, "test");
		player.goldenAgeTurnsRemaining = 1;
		player.AdvanceGoldenAge();
		Assert.False(player.InGoldenAge);

		player.StartGoldenAge(gameData, "test");
		Assert.False(player.InGoldenAge);
	}

	[Fact]
	public void GoldenAgeAddsShieldsAndCommerceToProductiveTiles() {
		Tile tile = new(ID.None("tile")) {
			overlayTerrainType = new TerrainType() { baseShieldProduction = 1, baseCommerceProduction = 1, baseFoodProduction = 2 },
		};
		int shields = tile.ProductionYield(player).yield;
		int commerce = tile.CommerceYield(player).yield;
		int food = tile.FoodYield(player).yield;

		player.StartGoldenAge(gameData, "test");

		Assert.Equal(shields + 1, tile.ProductionYield(player).yield);
		Assert.Equal(commerce + 1, tile.CommerceYield(player).yield);
		Assert.Equal(food, tile.FoodYield(player).yield);
	}
}
