using System;
using System.Linq;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

// Per the project owner, the AI never builds a palace to move its capital.
public sealed class AiPalaceTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player player;
	private readonly City secondCity;
	private readonly Building palace;

	public AiPalaceTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		player = gameData.players.First(p => !p.isBarbarians && !p.isHuman && p.units.Any(u => u.unitType.isSettler));
		foreach (Tech tech in gameData.techs) {
			player.knownTechs.Add(tech.id);
		}

		Tile capitalTile = player.units.First(u => u.unitType.isSettler).location;
		CityInteractions.BuildCity(capitalTile, player, player.GetNextCityName());
		Tile elsewhere = capitalTile.GetTilesWithinTileSquare(5).First(t => t != Tile.NONE && t.IsLand() && !t.HasCity()
			&& t.continent == capitalTile.continent && t.DistanceTo(capitalTile) >= 4);
		secondCity = CityInteractions.BuildCity(elsewhere, player, player.GetNextCityName());
		palace = gameData.Buildings.First(b => b.isCenterOfEmpire);
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	[Fact]
	public void AiCitiesAreNeverOfferedThePalace() {
		Assert.DoesNotContain(palace, secondCity.ListProductionOptions(gameData));
	}

	// The palace is the most expensive thing a city can build, so this is
	// what an AI city building a wonder that someone else finished used to
	// switch to.
	[Fact]
	public void AiNeverPicksThePalaceEvenWhenItIsTheMostExpensiveOption() {
		for (int i = 0; i < 50; ++i) {
			Assert.NotEqual<IProducible>(palace, ChooseProducible.Choose(secondCity, player));
		}
	}

	[Fact]
	public void HumansCanStillMoveTheirPalace() {
		player.isHuman = true;
		Assert.Contains(palace, secondCity.ListProductionOptions(gameData));
	}
}
