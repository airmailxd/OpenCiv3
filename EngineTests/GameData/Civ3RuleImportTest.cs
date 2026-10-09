using System.IO;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using QueryCiv3;
using Xunit;

namespace EngineTests.GameData;

// The rules a game imported from a Civ3 BIQ takes from it, checked against
// what CivFanatics says Civ3 does.
public class Civ3RuleImportTest {
	private static SaveGame ImportScenario(string folder, string name) {
		string conquests = Path.Join(Civ3Location.GetCiv3Path(), "Conquests");
		string scenarios = Path.Join(conquests, folder);
		return ImportCiv3.ImportBiq(Path.Join(scenarios, name), PathUtils.defaultBicPath, modPath => {
			if (string.IsNullOrEmpty(modPath)) {
				return Path.GetFullPath(Path.Combine(conquests, "Text", "PediaIcons.txt"));
			}
			if (!System.OperatingSystem.IsWindows()) {
				modPath = modPath.Replace("\\conquests\\", "/Conquests/");
			}
			return Path.GetFullPath(Path.Combine(scenarios, modPath, "Text", "PediaIcons.txt"));
		});
	}

	// "The standard game length is 540 turns" (https://civfanatics.com/civ3/faq/),
	// though the time scale a standard BIQ lists adds up to only 440.
	[SkippableFact]
	public void AStandardGameLasts540Turns() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		SaveGame game = ImportScenario("Scenarios", "No Civ Traits.biq");

		Assert.Equal(540, game.TimeOptions.turnLimit);
		// Its "use time limit" flag is clear, so the game goes on until
		// someone wins.
		Assert.False(game.VictoryConditions.UseTurnLimit);
	}

	[SkippableFact]
	public void AScenarioEndsOnItsOwnTurnLimit() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		SaveGame game = ImportScenario("Conquests", "4 Middle Ages.biq");

		Assert.Equal(204, game.TimeOptions.turnLimit);
	}

	// "Preserve random seed" is about reloading, not the starting seed, so a
	// scenario's random numbers start from its map's seed either way.
	[SkippableFact]
	public void AScenarioStartsFromItsMapsSeed() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		string path = Path.Join(Civ3Location.GetCiv3Path(), "Conquests", "Conquests", "4 Middle Ages.biq");
		BiqData biq = BiqData.LoadFile(path);
		Assert.False(biq.Game[0].PreserveRandomSeed);

		SaveGame game = ImportScenario("Conquests", "4 Middle Ages.biq");

		Assert.Equal(biq.Wmap[0].MapSeed, game.Seed);
	}

	// "The number of shields added is 1/4 the shield cost of the unit,
	// rounded down" (https://civfanatics.com/civ3/faq/).
	[SkippableFact]
	public void ADisbandedUnitGivesAQuarterOfItsShields() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		SaveGame game = ImportScenario("Conquests", "4 Middle Ages.biq");

		Assert.Equal(0.25f, game.Rules.ShieldRateForDisbanding);
		Assert.Equal(0.25f, new Rules().ShieldRateForDisbanding);
	}

	// No Civ3 source says what a scenario's cities start on, so they start
	// on a Worker, as before eb8aad18, where their owner can build one.
	[SkippableFact]
	public void AScenariosCitiesStartOnAWorker() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		SaveGame game = ImportScenario("Conquests", "4 Middle Ages.biq");

		Assert.NotEmpty(game.Cities);
		Assert.Contains(game.Cities, c => c.producible == "Worker" && c.producibleType == ProducibleType.UNIT);
	}

	// A standard game's RULE values: roads let a unit "travel three tiles"
	// for a move (https://civfanatics.com/civ3/faq/), and a fortress gives
	// "a 50% bonus to their defense"
	// (https://forums.civfanatics.com/threads/combat-system-explained.7679/).
	[SkippableFact]
	public void AStandardGamesRulesComeFromItsRule() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		SaveGame game = ImportScenario("Scenarios", "No Civ Traits.biq");

		Assert.Equal(3, game.Rules.MovementAlongRoads);
		Assert.Equal(50, game.Rules.FortressDefensiveBonus);
		Assert.Equal(2, game.Rules.FoodConsumptionPerCitizen);
		Assert.Equal(10, game.Rules.StartingTreasury);
	}

	// City Walls are the only building with both a land defense bonus and a
	// bombard defense; Civil Defense has only the first, and a Coastal
	// Fortress guards against ships
	// (https://civfanatics.com/civ3/civilopedia/improvements/).
	[SkippableFact]
	public void OnlyCityWallsAreWalls() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		SaveGame game = ImportScenario("Scenarios", "No Civ Traits.biq");

		SaveBuilding walls = Assert.Single(game.Buildings, b => b.flags.Contains(SaveBuilding.Flag.ProvidesWalls));
		Assert.Equal("Walls", walls.name);
	}
}
