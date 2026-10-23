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
		player.knownTechs.Remove(SanitationTech().id);
		// Only what each test puts there: no other unhealthy terrain nearby.
		foreach (Tile t in city.location.GetTilesWithinRankDistance(2)) {
			if (t != Tile.NONE && t.overlayTerrainType.causesDisease) {
				t.overlayTerrainType = grassland;
			}
		}
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

	// Half a percent a turn (strength 50 / 10, in thousandths) for any
	// unhealthy terrain in the radius, the strongest counting.
	[Fact]
	public void UnhealthyTerrainInTheRadiusGivesTheCityAChance() {
		GrowTo(3);
		Assert.Equal(0, Disease.Chance(city));
		city.location.overlayTerrainType = floodPlain;
		Assert.Equal(5, Disease.Chance(city));
		// More unhealthy tiles don't add up.
		Working().tileWorked.overlayTerrainType = jungle;
		Assert.Equal(5, Disease.Chance(city));
	}

	// Sanitation's flag cures flood plains, as intended, not tech number 8
	// (Writing) as in the shipped game (owner's decision, 2026-10-20).
	[Fact]
	public void SanitationCuresFloodPlainsButNotJungle() {
		Tech cure = Disease.CuringTech(gameData);
		Assert.Equal("Sanitation", cure.Name);
		GrowTo(3);
		city.location.overlayTerrainType = floodPlain;
		Tech writing = gameData.techs[8];
		Assert.Equal("Writing", writing.Name);
		player.knownTechs.Add(writing.id);
		Assert.True(Disease.IsUnhealthy(floodPlain, player));
		Assert.Equal(5, Disease.Chance(city));
		player.knownTechs.Add(cure.id);
		Assert.False(Disease.IsUnhealthy(floodPlain, player));
		Assert.Equal(0, Disease.Chance(city));
		Assert.True(Disease.IsUnhealthy(jungle, player));
	}

	// Not working the unhealthy tile doesn't protect the city.
	[Fact]
	public void UnworkedUnhealthyTilesCount() {
		GrowTo(2);
		Tile unworked = city.location.neighbors.Values.First(t => t != Tile.NONE && !t.IsWater() && t.personWorkingTile == null);
		unworked.overlayTerrainType = jungle;
		Assert.Equal(5, Disease.Chance(city));
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

	// A strike takes one citizen, half the time another the next turn
	// whatever the roll, and the city is then spared for a while (owner's
	// ruling, 2026-10-23).
	[Fact]
	public void AStrikeCostsOneOrTwoCitizensAndSparesTheCityAWhile() {
		GrowTo(6);
		city.location.overlayTerrainType = floodPlain;
		// A roll of 0 strikes, and takes the second citizen too.
		C7GameData.GameData.rng = new FixedRandom(0);
		Assert.True(Disease.MaybeStrike(city, 10));
		Assert.Equal(5, city.residents.Count);
		Assert.True(city.diseaseStrikesNextTurn);
		C7GameData.GameData.rng = new FixedRandom(999);
		Assert.True(Disease.MaybeStrike(city, 11));
		Assert.Equal(4, city.residents.Count);
		Assert.False(city.diseaseStrikesNextTurn);

		C7GameData.GameData.rng = new FixedRandom(0);
		Assert.False(Disease.MaybeStrike(city, 12));
		Assert.False(Disease.MaybeStrike(city, 11 + Disease.ImmuneTurns));
		Assert.Equal(4, city.residents.Count);

		// Without the second citizen.
		C7GameData.GameData.rng = new SequenceRandom(0, 99);
		Assert.True(Disease.MaybeStrike(city, 12 + Disease.ImmuneTurns));
		Assert.Equal(3, city.residents.Count);
		Assert.False(city.diseaseStrikesNextTurn);
	}

	[Fact]
	public void TheSecondStrikeSurvivesASave() {
		city.diseaseStrikesNextTurn = true;
		SaveGame save = SaveGame.FromGameData(gameData);
		SaveGame loaded = SaveGame.FromJSON(save.ToCompactJSON());
		Assert.True(loaded.Cities.Single(c => c.name == city.name).diseaseStrikesNextTurn);
	}

	[Fact]
	public void TheLastStrikeSurvivesASave() {
		city.lastDiseaseTurn = 42;
		SaveGame save = SaveGame.FromGameData(gameData);
		SaveGame loaded = SaveGame.FromJSON(save.ToCompactJSON());
		Assert.Equal(42, loaded.Cities.Single(c => c.name == city.name).lastDiseaseTurn);
	}

	[Fact]
	public void ASizeOneCityIsNeverStruck() {
		Assert.Single(city.residents);
		city.location.overlayTerrainType = floodPlain;
		C7GameData.GameData.rng = new Random(7);
		int expected = new Random(7).Next();
		Assert.False(Disease.MaybeStrike(city, 1));
		Assert.Single(city.residents);
		Assert.Equal(expected, C7GameData.GameData.rng.Next());
	}

	[Fact]
	public void AHealthyCityIsNeverStruckNorDrawsARandomNumber() {
		GrowTo(3);
		C7GameData.GameData.rng = new Random(7);
		int expected = new Random(7).Next();
		Assert.False(Disease.MaybeStrike(city, 1));
		Assert.Equal(3, city.residents.Count);
		Assert.Equal(expected, C7GameData.GameData.rng.Next());
	}

	[Fact]
	public void DiseaseStrikesAboutHalfAPercentOfItsTurns() {
		GrowTo(3);
		city.location.overlayTerrainType = floodPlain;
		C7GameData.GameData.rng = new GameRandom(1234);
		int outbreaks = 0;
		int exposed = 0;
		for (int turn = 0; turn < 40000; ++turn) {
			if (city.lastDiseaseTurn < 0 || turn - city.lastDiseaseTurn > Disease.ImmuneTurns) {
				++exposed;
			}
			bool second = city.diseaseStrikesNextTurn;
			if (Disease.MaybeStrike(city, turn)) {
				if (!second) {
					++outbreaks;
				}
				GrowTo(3);
			}
		}
		// 0.5% of the turns it wasn't spared.
		Assert.InRange(outbreaks, exposed * 0.005 * 0.7, exposed * 0.005 * 1.3);
	}

	private Tile JungleNextToTheCity() {
		Tile tile = city.location.neighbors.Values.First(t => t != Tile.NONE && t.IsLand() && !t.HasCity());
		tile.overlayTerrainType = jungle;
		return tile;
	}

	// Only fortified units die of disease, and not workers.
	[Fact]
	public void OnlyFortifiedUnitsOnUnhealthyTerrainCanDie() {
		Tile tile = JungleNextToTheCity();
		MapUnit unit = gameData.SpawnUnit(player, gameData.unitPrototypes.Single(p => p.name == "Warrior"), tile);
		Assert.False(Disease.CanStrikeUnit(unit));
		unit.isFortified = true;
		Assert.True(Disease.CanStrikeUnit(unit));

		MapUnit worker = gameData.SpawnUnit(player, gameData.unitPrototypes.Single(p => p.name == "Worker"), tile);
		worker.isFortified = true;
		Assert.False(Disease.CanStrikeUnit(worker));

		tile.overlayTerrainType = grassland;
		Assert.False(Disease.CanStrikeUnit(unit));
		tile.overlayTerrainType = jungle;

		// A roll under the strength (50 in 50,000) kills it.
		C7GameData.GameData.rng = new FixedRandom(49);
		Assert.Contains(unit, Disease.StrikeFortifiedUnits(player));
		Assert.DoesNotContain(unit, player.units);
		Assert.Contains(worker, player.units);
		Assert.Contains(EngineStorage.messagesToUI.OfType<MsgShowMilitaryAdvisorPopup>(), m => m.message == "Our Warrior has died of disease from the jungle!");
	}

	[Fact]
	public void ARollAtTheStrengthSparesTheUnit() {
		MapUnit unit = gameData.SpawnUnit(player, gameData.unitPrototypes.Single(p => p.name == "Warrior"), JungleNextToTheCity());
		unit.isFortified = true;
		C7GameData.GameData.rng = new FixedRandom(50);
		Assert.Empty(Disease.StrikeFortifiedUnits(player));
		Assert.Contains(unit, player.units);
	}

	// Rolls the given number for every Next(max).
	// Rolls the given numbers for Next(max) in turn, the last one after.
	private class SequenceRandom(params int[] rolls) : Random {
		private int next;
		public override int Next(int maxValue) => Math.Min(rolls[Math.Min(next++, rolls.Length - 1)], maxValue - 1);
		protected override double Sample() => 0.0;
	}

	private class FixedRandom(int roll) : Random {
		public override int Next(int maxValue) => Math.Min(roll, maxValue - 1);
		protected override double Sample() => 0.0;
	}
}
