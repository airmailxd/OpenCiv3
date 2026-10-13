using System;
using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Disease from unhealthy terrain (see Disease).
public class DiseaseTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player player;
	private readonly City city;
	private readonly TerrainType floodPlain;
	private readonly TerrainType jungle;
	private readonly TerrainType grassland;

	public DiseaseTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		player = gameData.players.First(p => p.isHuman);
		Tile tile = player.units.First(u => u.unitType.isSettler).location;
		city = CityInteractions.BuildCity(tile, player, player.GetNextCityName());
		floodPlain = Terrain("flood plain");
		jungle = Terrain("jungle");
		grassland = Terrain("grassland");
		city.location.overlayTerrainType = grassland;
		Tech sanitation = SanitationTech();
		player.knownTechs.Remove(sanitation.id);
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private TerrainType Terrain(string key) => gameData.terrainTypes.Single(t => t.Key == key);

	private Tech SanitationTech() => gameData.techs.Single(t => t.HasFlag(SaveTech.Flag.DisablesDiseases));

	// Grows the city, every new citizen working a grassland tile.
	private void GrowTo(int size) {
		while (city.residents.Count < size) {
			CityResident r = new() {
				city = city,
				nationality = player.civilization,
				citizenType = gameData.citizenTypes.Find(x => x.IsDefaultCitizen),
			};
			city.AddCitizen(r);
			CityTileAssignmentAI.AssignNewCitizenToTile(gameData, r);
			if (r.tileWorked != Tile.NONE) {
				r.tileWorked.overlayTerrainType = grassland;
			}
		}
	}

	private CityResident Working() => city.residents.First(r => r.tileWorked != Tile.NONE && r.tileWorked != city.location);

	[Fact]
	public void TheRulesetHasConquestsDiseaseTerrains() {
		Assert.True(floodPlain.causesDisease);
		Assert.True(floodPlain.diseaseCanBeCured);
		Assert.True(jungle.causesDisease);
		Assert.False(jungle.diseaseCanBeCured);
		Assert.True(Terrain("marsh").causesDisease);
		Assert.False(Terrain("marsh").diseaseCanBeCured);
		Assert.False(grassland.causesDisease);
		Assert.False(Terrain("volcano").causesDisease);
		Assert.Equal(50, floodPlain.diseaseStrength);
		Assert.Equal("Sanitation", SanitationTech().Name);
	}

	// conquests.biq's terrain flags have 0xCC filler above the real bits.
	[Fact]
	public void TheImportReadsTheBiqDiseaseFlags() {
		TerrainType imported = TerrainType.ImportFromCiv3(4, new QueryCiv3.Biq.TERR { TerrainFlags = unchecked((int)0xCCCCCCCC), DiseaseStrength = 50 });
		Assert.True(imported.causesDisease);
		Assert.True(imported.diseaseCanBeCured);
		Assert.Equal(50, imported.diseaseStrength);

		TerrainType jungleImport = TerrainType.ImportFromCiv3(8, new QueryCiv3.Biq.TERR { TerrainFlags = unchecked((int)0xCCCCCCC4), DiseaseStrength = 50 });
		Assert.True(jungleImport.causesDisease);
		Assert.False(jungleImport.diseaseCanBeCured);

		TerrainType volcanoImport = TerrainType.ImportFromCiv3(10, new QueryCiv3.Biq.TERR { TerrainFlags = unchecked((int)0xCCCCCCC2), DiseaseStrength = 50 });
		Assert.False(volcanoImport.causesDisease);
	}

	[Fact]
	public void TheDiseaseFieldsSurviveASave() {
		floodPlain.diseaseStrength = 70;
		SaveGame save = SaveGame.FromGameData(gameData);
		SaveGame loaded = SaveGame.FromJSON(save.ToCompactJSON());
		TerrainType reloaded = loaded.TerrainTypes.Single(t => t.Key == "flood plain");
		Assert.True(reloaded.causesDisease);
		Assert.True(reloaded.diseaseCanBeCured);
		Assert.Equal(70, reloaded.diseaseStrength);
	}

	[Fact]
	public void UnhealthyTilesTheCityWorksAddUpTheirStrength() {
		GrowTo(3);
		Assert.Equal(0, Disease.Chance(city));
		city.location.overlayTerrainType = floodPlain;
		Assert.Equal(50, Disease.Chance(city));
		Working().tileWorked.overlayTerrainType = jungle;
		Assert.Equal(100, Disease.Chance(city));
	}

	[Fact]
	public void SanitationCuresFloodPlainsButNotJungle() {
		GrowTo(3);
		city.location.overlayTerrainType = floodPlain;
		Working().tileWorked.overlayTerrainType = jungle;
		player.knownTechs.Add(SanitationTech().id);
		Assert.Equal(50, Disease.Chance(city));
		Assert.False(Disease.IsUnhealthy(floodPlain, player));
		Assert.True(Disease.IsUnhealthy(jungle, player));
	}

	[Fact]
	public void UnworkedUnhealthyTilesDontCount() {
		GrowTo(2);
		Tile unworked = city.location.neighbors.Values.First(t => t != Tile.NONE && !t.IsWater() && t.personWorkingTile == null);
		unworked.overlayTerrainType = jungle;
		Assert.Equal(0, Disease.Chance(city));
	}

	[Fact]
	public void DiseaseKillsACitizenWorkingTheUnhealthyTile() {
		GrowTo(3);
		CityResident sick = Working();
		sick.tileWorked.overlayTerrainType = jungle;
		Tile jungleTile = sick.tileWorked;

		Disease.Strike(city);

		Assert.Equal(2, city.residents.Count);
		Assert.DoesNotContain(sick, city.residents);
		Assert.Null(jungleTile.personWorkingTile);
		Assert.Contains(EngineStorage.messagesToUI.OfType<MsgShowDomesticAdvisorPopup>(), m => m.message == $"Disease strikes {city.name}.");
	}

	[Fact]
	public void ASizeOneCityIsNeverStruck() {
		Assert.Single(city.residents);
		city.location.overlayTerrainType = floodPlain;
		C7GameData.GameData.rng = new Random(7);
		int expected = new Random(7).Next();
		Assert.False(Disease.MaybeStrike(city));
		Assert.Single(city.residents);
		Assert.Equal(expected, C7GameData.GameData.rng.Next());
	}

	[Fact]
	public void AHealthyCityIsNeverStruckNorDrawsARandomNumber() {
		GrowTo(3);
		C7GameData.GameData.rng = new Random(7);
		int expected = new Random(7).Next();
		Assert.False(Disease.MaybeStrike(city));
		Assert.Equal(3, city.residents.Count);
		Assert.Equal(expected, C7GameData.GameData.rng.Next());
	}

	[Fact]
	public void DiseaseStrikesAboutFivePercentOfTurnsPerUnhealthyTile() {
		GrowTo(3);
		city.location.overlayTerrainType = floodPlain;
		Working().tileWorked.overlayTerrainType = jungle;
		C7GameData.GameData.rng = new GameRandom(1234);
		int strikes = 0;
		for (int turn = 0; turn < 2000; ++turn) {
			if (Disease.MaybeStrike(city)) {
				++strikes;
				GrowTo(3);
				Working().tileWorked.overlayTerrainType = jungle;
			}
		}
		// 10% of 2000 turns is 200.
		Assert.InRange(strikes, 150, 250);
	}
}
