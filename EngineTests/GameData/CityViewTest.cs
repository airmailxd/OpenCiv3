using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Civ3's City View (see C7GameData.CityView): which pieces of art the
// picture of a city is made of, and where, as Civ3's cityview module works
// them out.
public class CityViewTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;

	public CityViewTest(SaveGameFixture fixture) {
		this.fixture = fixture;
	}

	private const string BUILDINGS = CityView.ART + "Buildings/";
	private const string WALLS = CityView.ART + "City Walls/";

	// A Roman town on plains with Conquests' city size levels, its rules
	// knowing every building with art.
	private static CityView.Input Town(params int[] built) {
		CityView.Input input = new() {
			era = 0,
			cultureGroup = 2,
			terrain = CityView.Terrain.Plains,
			population = 3,
			maxTownSize = 6,
			maxCitySize = 12,
			mapX = 20,
			mapY = 30,
		};
		for (int art = 0; art < CityView.ART_COUNT; art++) {
			if (CityView.ArtKey(art) != null) {
				input.knownArt.Add(art);
			}
		}
		input.builtArt.UnionWith(built);
		return input;
	}

	private static List<CityView.Sprite> Pictures(List<CityView.Sprite> sprites, int art) =>
		sprites.Where(s => s.art == art && !s.isShadow).ToList();

	[Fact]
	public void BuildingsAreFoundByTheirCivilopediaKeys() {
		Assert.Equal(3, CityView.ArtOf("BLDG_Temple"));
		Assert.Equal(0, CityView.ArtOf("BLDG_Palace"));
		Assert.Equal(30, CityView.ArtOf("BLDG_Pyramids"));
		Assert.Equal(74, CityView.ArtOf("BLDG_Internet"));
		// Conquests' own keys for the research lab and the Sistine Chapel.
		Assert.Equal(20, CityView.ArtOf("BLDG_Lab"));
		Assert.Equal(38, CityView.ArtOf("BLDG_Sistine_Chapel"));
		// Conquests' new wonders have no art.
		Assert.Equal(-1, CityView.ArtOf("BLDG_Zeus"));
		Assert.Equal(-1, CityView.ArtOf(null));
	}

	[Fact]
	public void TheLayoutFollowsTheCitySize() {
		CityView.Input input = Town();
		Assert.Equal(0, CityView.SizeIndex(input));
		input.population = 7;
		Assert.Equal(1, CityView.SizeIndex(input));
		input.population = 13;
		Assert.Equal(2, CityView.SizeIndex(input));
		Assert.Equal(CityView.ART + "Backgrounds/P-LRG.pcx", CityView.Compose(input)[0].path);
	}

	[Fact]
	public void ATownWithABuildingOnlyAMetropolisHasRoomForGetsItsLayout() {
		// A recycling center has a cell only in the metropolis's layout.
		CityView.Input input = Town(15);
		Assert.Equal(2, CityView.SizeIndex(input));
		Assert.Equal(CityView.ART + "Backgrounds/P-LRG.pcx", CityView.Compose(input)[0].path);
	}

	[Fact]
	public void TheBackgroundShowsTheCitysTerrain() {
		CityView.Input input = Town();
		input.terrain = CityView.Terrain.Desert;
		CityView.Sprite background = CityView.Compose(input)[0];
		Assert.Equal(CityView.ART + "Backgrounds/D-SML.pcx", background.path);
		Assert.True(background.WholeFile);
		Assert.Equal((0, 0), (background.x, background.y));
	}

	[Fact]
	public void ABuildingIsDrawnInItsCellFromItsCulturesColumn() {
		List<CityView.Sprite> sprites = CityView.Compose(Town(3));
		CityView.Sprite temple = Assert.Single(Pictures(sprites, 3));
		Assert.Equal(BUILDINGS + "IMP-Cultures1.pcx", temple.path);
		// The Mediterranean column, 75x108.
		Assert.Equal((2 * 76 + 1, 1, 75, 108), (temple.cropX, temple.cropY, temple.cropWidth, temple.cropHeight));
		// The temple's cell in the town's layout, row 4, column 8.
		Assert.Equal((327, 310), (temple.x, temple.y));
		// Its shadow goes just before it.
		CityView.Sprite shadow = sprites[sprites.IndexOf(temple) - 1];
		Assert.True(shadow.isShadow);
		Assert.Equal(BUILDINGS + "IMP-Cultures1_Sh.pcx", shadow.path);
		Assert.Equal((temple.x, temple.y), (shadow.x, shadow.y));
	}

	[Fact]
	public void EraSheetsHaveAColumnForEachEra() {
		CityView.Input input = Town(2);
		input.era = 3;
		CityView.Sprite granary = Assert.Single(Pictures(CityView.Compose(input), 2));
		Assert.Equal(BUILDINGS + "IMP-Ages1.pcx", granary.path);
		Assert.Equal((3 * 76 + 1, 1 + 2 * 109), (granary.cropX, granary.cropY));
	}

	[Fact]
	public void BuildingsTheCityLacksAreNotDrawn() {
		List<CityView.Sprite> sprites = CityView.Compose(Town());
		Assert.DoesNotContain(sprites, s => s.art >= 0);
	}

	[Fact]
	public void ThePictureOfACityIsAlwaysTheSame() {
		Assert.Equal(CityView.Compose(Town(1, 3, 5)), CityView.Compose(Town(1, 3, 5)));
	}

	[Fact]
	public void HousesAndStreetsFillTheGround() {
		List<CityView.Sprite> sprites = CityView.Compose(Town());
		Assert.Contains(sprites, s => s.path == CityView.ART + "Miscellaneous/Filler-ROM.pcx");
		// The main street's square, from the grassland, plains... rows of
		// the ancient roads: plains are the fourth pair of rows.
		CityView.Sprite square = Assert.Single(sprites, s => s.path.EndsWith("ROADS-ANC.pcx") && s.cropX == 1 + 76 * 7
			&& s.cropY == 655 + 109);
		Assert.Equal((444, 389), (square.x, square.y));
	}

	[Fact]
	public void ABuildingDrawnWholeGoesWhereCiv3PutsIt() {
		CityView.Input input = Town(30);
		input.terrain = CityView.Terrain.Desert;
		List<CityView.Sprite> sprites = CityView.Compose(input);
		CityView.Sprite pyramids = Assert.Single(Pictures(sprites, 30));
		Assert.Equal(BUILDINGS + "WON-Pyramid-D.pcx", pyramids.path);
		Assert.True(pyramids.WholeFile);
		Assert.Equal((106, 36), (pyramids.x, pyramids.y));
		Assert.Equal(BUILDINGS + "WON-Pyramid_Sh.pcx", sprites[sprites.IndexOf(pyramids) - 1].path);
	}

	[Fact]
	public void ABuildingThatMustBeNearARiverShowsOnlyByWater() {
		CityView.Input input = Town(17);
		input.nearRiverArt.Add(17);
		Assert.Empty(Pictures(CityView.Compose(input), 17));
		input.waterCode = 1;
		CityView.Sprite dam = Assert.Single(Pictures(CityView.Compose(input), 17));
		Assert.Equal(BUILDINGS + "IMP-PlantHydro-P.pcx", dam.path);
		Assert.Equal((602, 563), (dam.x, dam.y));
	}

	[Fact]
	public void TheWaterCodeSaysWhichRiverAndCoastPiecesShow() {
		Assert.Equal(0, CityView.WaterCode(0, false, false, false, false));
		Assert.Equal(1, CityView.WaterCode(0, true, false, false, false));
		Assert.Equal(8, CityView.WaterCode(0, true, false, true, false));
		Assert.Equal(2, CityView.WaterCode(2, false, false, true, false));
		Assert.Equal(5, CityView.WaterCode(4, false, false, true, true));
		Assert.Equal(6, CityView.WaterCode(5, true, false, true, false));
		Assert.Equal(9, CityView.WaterCode(3, true, true, true, false));

		CityView.Input input = Town(25);
		input.waterCode = 3;
		List<string> paths = CityView.Compose(input).Select(s => s.path).ToList();
		string water = CityView.ART + "Backgrounds/P-H2O-";
		Assert.Contains(water + "CoastRight.pcx", paths);
		Assert.Contains(water + "NOTtheRiver.pcx", paths);
		Assert.Contains(water + "Harbor.pcx", paths);
		Assert.DoesNotContain(water + "CoastBack.pcx", paths);
		Assert.Contains(BUILDINGS + "IMP-Harbor-ANC.pcx", paths);
	}

	[Fact]
	public void ACitysWallsAndATownsAreDrawnFromDifferentPieces() {
		CityView.Input city = Town(7);
		city.population = 8;
		city.wallsDefend = true;
		List<string> paths = CityView.Compose(city).Select(s => s.path).ToList();
		Assert.Contains(WALLS + "WallROM1-SMLold.pcx", paths);
		Assert.Contains(WALLS + "WallROM2-NOFort.pcx", paths);
		Assert.Contains(WALLS + "WallROM3-NOHarb.pcx", paths);
		Assert.Contains(WALLS + "WallROM4-SML.pcx", paths);
		Assert.Contains(WALLS + "Wall5SML-PTree.pcx", paths);

		CityView.Input town = Town(7);
		town.townHasBombardDefense = true;
		paths = CityView.Compose(town).Select(s => s.path).ToList();
		Assert.Contains(WALLS + "WallROM1-SML.pcx", paths);
		Assert.Contains(WALLS + "WallROM4-SMLold.pcx", paths);

		Assert.DoesNotContain(CityView.Compose(Town()), s => s.path.StartsWith(WALLS));
	}

	[Fact]
	public void TheFirstPalaceIsTheCave() {
		List<CityView.Sprite> sprites = CityView.Compose(Town(0));
		CityView.Sprite palace = Assert.Single(Pictures(sprites, 0));
		Assert.Equal(BUILDINGS + "Imp-Palace-CAVE.pcx", palace.path);
		CityView.Sprite secondPiece = Assert.Single(Pictures(sprites, CityView.PALACE_SECOND_PIECE));
		Assert.Equal((1, 110, 75, 108), (secondPiece.cropX, secondPiece.cropY, secondPiece.cropWidth, secondPiece.cropHeight));
	}

	[Fact]
	public void TheInputComesFromTheCity() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player player = gameData.players.First(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler));
		Tile tile = player.units.First(u => u.unitType.isSettler).location;
		City city = CityInteractions.BuildCity(tile, player, player.GetNextCityName());
		city.AddBuilding(gameData.Buildings.Single(b => b.civilopediaEntry == "BLDG_Temple"));

		CityView.Input input = CityView.InputFor(gameData, city);
		Assert.Contains(3, input.builtArt);
		Assert.Contains(CityView.PALACE, input.knownArt);
		Assert.Contains(20, input.knownArt);
		Assert.Equal(player.civilization.cultureGroup.index, input.cultureGroup);
		Assert.Equal(city.residents.Count, input.population);
		Assert.Equal((tile.XCoordinate, tile.YCoordinate), (input.mapX, input.mapY));
		Assert.Single(Pictures(CityView.Compose(input), 3));
	}
}
