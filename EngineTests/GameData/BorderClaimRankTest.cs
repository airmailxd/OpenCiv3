using System.Linq;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class BorderClaimRankTest : IClassFixture<SaveGameFixture>, System.IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player first;
	private readonly Player second;

	public BorderClaimRankTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler)).ToArray();
		first = civs[0];
		second = civs[1];
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	// A tile in one city's base ring stays with it even when another city
	// with far more culture reaches the tile with its first expansion.
	[Fact]
	public void BaseRingBeatsAnExpansionWhateverTheCulture() {
		(Tile weakSite, Tile strongSite, Tile contested) = FindSites();

		City weak = CityInteractions.BuildCity(weakSite, first, first.GetNextCityName());
		City strong = CityInteractions.BuildCity(strongSite, second, second.GetNextCityName());
		strong.perPlayerCulture[second] = 500;
		gameData.UpdateTileOwners();

		Assert.True(strong.GetBorderExpansionLevel() >= 2);
		Assert.Equal(1, weak.GetBorderExpansionLevel());
		Assert.Equal(weak, contested.owningCity);
	}

	// A city keeps its own tile even when a neighbor with far more culture
	// expands over it.
	[Fact]
	public void ExpansionNeverTakesACitysOwnTile() {
		(Tile strongSite, _, _) = FindSites();
		Tile weakSite = strongSite.GetTilesWithinRankDistance(2).First(t => IsOpenLand(t) && strongSite.RankDistanceTo(t) == 2);

		// The weak city is the older one, so the strong city's claims are
		// resolved after it has taken its own tile.
		City weak = CityInteractions.BuildCity(weakSite, second, second.GetNextCityName());
		City strong = CityInteractions.BuildCity(strongSite, first, first.GetNextCityName());
		strong.perPlayerCulture[first] = 500;
		gameData.UpdateTileOwners();

		Assert.Equal(weak, weakSite.owningCity);
	}

	// Two land city sites, with a land tile in the first site's base ring
	// that is in the second site's first expansion ring.
	private (Tile, Tile, Tile) FindSites() {
		foreach (Tile a in gameData.map.tiles.Where(IsOpenLand)) {
			foreach (Tile contested in a.GetTilesWithinRankDistance(1).Where(t => t != a && IsOpenLand(t))) {
				Tile b = contested.GetTilesWithinRankDistance(2).FirstOrDefault(t =>
					IsOpenLand(t) && contested.RankDistanceTo(t) == 2 && a.RankDistanceTo(t) >= 2);
				if (b != null) {
					return (a, b, contested);
				}
			}
		}
		throw new System.Exception("No suitable city sites on the test map");
	}

	private static bool IsOpenLand(Tile t) {
		return t != Tile.NONE && t.IsLand() && !t.HasCity() && t.owningCity == null && t.unitsOnTile.Count == 0;
	}
}
