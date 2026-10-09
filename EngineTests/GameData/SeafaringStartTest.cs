using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class SeafaringStartTest {
	private static bool IsSeafaring(Civilization civ) => civ.traits.Contains(Civilization.Trait.Seafaring);

	// Seafaring civs, picked or random, start next to the sea, as long as
	// there are enough coastal starts for them.
	[Theory]
	[InlineData(WorldCharacteristics.Landform.Pangaea)]
	[InlineData(WorldCharacteristics.Landform.Continents)]
	public void SeafaringCivsStartNextToTheSea(WorldCharacteristics.Landform landform) {
		int checkedSeafaring = 0;
		for (int seed = 1; seed <= 6; ++seed) {
			SaveGame save = SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")).GetSave();
			List<Civilization> seafaring = save.Civilizations.Where(c => !c.isBarbarian && IsSeafaring(c)).ToList();
			Assert.True(seafaring.Count >= 2, "the rules should have seafaring civs");

			WorldCharacteristics wc = new(save) {
				landform = landform,
				oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_70,
				age = WorldCharacteristics.Age.Billion_4,
				climate = WorldCharacteristics.Climate.Normal,
				temperature = WorldCharacteristics.Temperature.Temperate,
				barbarianActivity = BarbarianActivity.Roaming,
				worldSize = new WorldSize() { width = 100, height = 100, numberOfCivs = 8, distanceBetweenCivs = 12, techRate = 240, optimalNumberOfCities = 20 },
				mapSeed = seed,
			};

			// The human and one opponent are picked seafaring civs; the rest
			// are random, and may be seafaring too.
			List<SelectedOpponent> opponents = [new SelectedOpponent { Name = seafaring[1].name }];
			opponents.AddRange(Enumerable.Repeat(new SelectedOpponent { isRandom = true }, 6));
			new GameSetup {
				playerCivilization = seafaring[0],
				difficulty = save.Difficulties.First(),
				worldCharacteristics = wc,
				opponents = opponents,
				victoryConditions = new VictoryConditions(),
			}.Populate(save);

			C7GameData.GameData gameData = SaveGameFixture.HydrateSaveGame(save);
			List<Tile> starts = gameData.map.startingLocations;
			int coastalStarts = starts.Count(t => t.NeighborsOcean());
			List<Player> civs = gameData.players.Where(p => !p.isBarbarians).ToList();
			Assert.Contains(civs, p => p.civilization.name == seafaring[0].name);
			Assert.Contains(civs, p => p.civilization.name == seafaring[1].name);

			int seafaringCivs = civs.Count(p => IsSeafaring(p.civilization));
			if (coastalStarts >= 2) {
				// Random seafaring civs are swapped out when there aren't
				// enough coastal starts for them all.
				Assert.True(seafaringCivs <= coastalStarts, $"seed {seed}: {seafaringCivs} seafaring civs for {coastalStarts} coastal starts");
			}
			foreach (Player p in civs.Where(p => IsSeafaring(p.civilization))) {
				Tile start = p.units.First().location;
				if (seafaringCivs <= coastalStarts) {
					Assert.True(start.NeighborsOcean(), $"seed {seed}: seafaring {p.civilization.name} starts inland at {start.XCoordinate},{start.YCoordinate}");
				}
				++checkedSeafaring;
			}
		}
		Assert.True(checkedSeafaring >= 12);
	}
}
