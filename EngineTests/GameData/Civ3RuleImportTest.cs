using System.IO;
using System.Linq;
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

	// The era splash's words: each era's researcher names (the BIQ ERAS
	// section's) and each government's ruler titles (its GOVT pairs in use).
	[SkippableFact]
	public void TheEraSplashWordsComeFromTheBiq() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		SaveGame game = ImportScenario("Scenarios", "No Civ Traits.biq");

		Assert.Equal(4, game.Rules.EraResearcherNames.Count);
		Assert.All(game.Rules.EraResearcherNames.Values, Assert.NotEmpty);
		Assert.All(game.Governments, g => Assert.Equal(g.maleRulerTitles.Count, g.femaleRulerTitles.Count));
		Assert.Equal(["Wise-Men", "Alchemists", "Sooth Sayers"], game.Rules.EraResearcherNames["ERAS_Middle_Ages"]);
		Government communism = Assert.Single(game.Governments, g => g.name == "Communism");
		Assert.Equal(["Comrade"], communism.maleRulerTitles);
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

	// A Coastal Fortress gives "+50% against naval attacks", and City Walls
	// nothing against them
	// (https://civfanatics.com/civ3/civilopedia/improvements/).
	[SkippableFact]
	public void CoastalFortressDefendsAgainstShips() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		SaveGame game = ImportScenario("Scenarios", "No Civ Traits.biq");

		Assert.Equal(0.5, game.Buildings.Single(b => b.name == "Coastal Fortress").navalDefenseBonus);
		Assert.Equal(0, game.Buildings.Single(b => b.name == "Walls").navalDefenseBonus);
	}

	// Wheeled units "cannot cross mountains or jungle tiles without a road"
	// (https://sullla.com/Civ3/CQ/CQ_units.html); the Helicopter carries
	// only foot soldiers, and the Explorer treats all terrain as roads
	// (https://forums.civfanatics.com/threads/is-there-a-guide-on-unit-abilities.57822/).
	[SkippableFact]
	public void MovementFlagsAreImported() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		SaveGame game = ImportScenario("Scenarios", "No Civ Traits.biq");

		Assert.True(game.TerrainTypes.Single(t => t.Key == "mountains").impassableByWheeled);
		Assert.True(game.TerrainTypes.Single(t => t.Key == "jungle").impassableByWheeled);
		Assert.False(game.TerrainTypes.Single(t => t.Key == "grassland").impassableByWheeled);
		SaveUnitPrototype Unit(string name) => game.UnitPrototypes.Single(p => p.name == name);
		Assert.Contains(SaveUnitPrototype.Flag.Wheeled, Unit("Catapult").flags);
		Assert.Contains(SaveUnitPrototype.Flag.FootSoldier, Unit("Warrior").flags);
		Assert.Contains(SaveUnitPrototype.Flag.CanCarryFootUnitsOnly, Unit("Helicopter").flags);
		Assert.Contains(SaveUnitPrototype.Flag.AllTerrainAsRoads, Unit("Explorer").flags);
		Assert.DoesNotContain(SaveUnitPrototype.Flag.Wheeled, Unit("Warrior").flags);
	}

	// The BIQ's "Requires Support": unchecked only for the King units in
	// conquests.biq (compendium 05, BIQ-PRTO).
	[SkippableFact]
	public void RequireSupportIsImported() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		SaveGame game = ImportScenario("Scenarios", "No Civ Traits.biq");

		Assert.All(game.UnitPrototypes.Where(p => p.flags.Contains(SaveUnitPrototype.Flag.King)),
			p => Assert.Contains(SaveUnitPrototype.Flag.NoSupportRequired, p.flags));
		Assert.DoesNotContain(SaveUnitPrototype.Flag.NoSupportRequired, game.UnitPrototypes.Single(p => p.name == "Warrior").flags);
		Assert.DoesNotContain(SaveUnitPrototype.Flag.NoSupportRequired, game.UnitPrototypes.Single(p => p.name == "Leader").flags);
	}

	// The Javelin Thrower enslaves its foes as Workers, the Privateer as
	// Privateers
	// (https://forums.civfanatics.com/threads/anyone-know-exactly-how-enslavement-works.107253/).
	[SkippableFact]
	public void EnslaveResultsAreImported() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		SaveGame game = ImportScenario("Scenarios", "No Civ Traits.biq");

		Assert.Equal("Worker", game.UnitPrototypes.Single(p => p.name == "Javelin Thrower").enslavesTo);
		Assert.Equal("Privateer", game.UnitPrototypes.Single(p => p.name == "Privateer").enslavesTo);
		Assert.Null(game.UnitPrototypes.Single(p => p.name == "Warrior").enslavesTo);
	}

	// Aircraft fly their missions within their operational range: the
	// Bomber's is "a larger operational range" than the Fighter's; the
	// Fighter "may execute all air missions except precision strikes"; and
	// stealth aircraft "can execute all air missions, except air
	// superiority" (Conquests Civilopedia, PRTO_Fighter, PRTO_Bomber,
	// PRTO_Stealth_Fighter). Paratroopers "execute airdrops"
	// (PRTO_Paratrooper). The chances to intercept are 50% and 5% for stealth
	// aircraft (https://forums.civfanatics.com/threads/aerial-combat-mechanics.90783/).
	[SkippableFact]
	public void AirUnitsAndInterceptionChancesAreImported() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		SaveGame game = ImportScenario("Scenarios", "No Civ Traits.biq");
		SaveUnitPrototype Unit(string name) => game.UnitPrototypes.Single(p => p.name == name);

		Assert.True(Unit("Bomber").operationalRange > Unit("Fighter").operationalRange);
		Assert.True(Unit("Fighter").operationalRange > 0);
		Assert.Equal(0, Unit("Warrior").operationalRange);
		Assert.Superset(new[] { UnitAction.Bombing, UnitAction.Recon, UnitAction.Intercept, UnitAction.Rebase }.ToHashSet(), Unit("Fighter").actions);
		Assert.DoesNotContain(UnitAction.PrecisionBombing, Unit("Fighter").actions);
		Assert.Contains(UnitAction.PrecisionBombing, Unit("Stealth Fighter").actions);
		Assert.DoesNotContain(UnitAction.Intercept, Unit("Stealth Fighter").actions);
		Assert.Contains(SaveUnitPrototype.Flag.Stealth, Unit("Stealth Bomber").flags);
		Assert.DoesNotContain(SaveUnitPrototype.Flag.Stealth, Unit("Bomber").flags);
		Assert.Contains(UnitAction.Airdrop, Unit("Paratrooper").actions);
		Assert.Equal(50, game.Rules.ChanceToInterceptAirMissions);
		Assert.Equal(5, game.Rules.ChanceToInterceptStealthMissions);
		// "A=8 for the SAM" (alexman, same thread).
		Assert.Equal(8, game.Buildings.Single(b => b.name == "SAM Missile Battery").airPower);
	}

	// The BIQ's PRTO "Join City" worker action: "when the settler is active
	// on a city, click the "Join City" button" (Conquests Civilopedia,
	// PRTO_Settler; the same for workers, PRTO_Worker).
	[SkippableFact]
	public void JoinCityIsImported() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		SaveGame game = ImportScenario("Scenarios", "No Civ Traits.biq");
		SaveUnitPrototype Unit(string name) => game.UnitPrototypes.Single(p => p.name == name);

		Assert.Contains(UnitAction.JoinCity, Unit("Settler").actions);
		Assert.Contains(UnitAction.JoinCity, Unit("Worker").actions);
		Assert.DoesNotContain(UnitAction.JoinCity, Unit("Warrior").actions);
	}

	// "[Submarines] can operate invisibly to enemy units ... However, other
	// submarines and AEGIS Cruisers and Destroyers can see them plainly. In
	// addition, Submarines have Stealth Attack" (Conquests Civilopedia,
	// PRTO_Submarine); Privateers "carry no nationality markings"
	// (PRTO_Privateer); F-15's have Stealth Attack (PRTO_F-15).
	[SkippableFact]
	public void InvisibilityStealthAttackAndHiddenNationalityAreImported() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		SaveGame game = ImportScenario("Scenarios", "No Civ Traits.biq");
		SaveUnitPrototype Unit(string name) => game.UnitPrototypes.Single(p => p.name == name);

		Assert.Contains(SaveUnitPrototype.Flag.Invisible, Unit("Submarine").flags);
		Assert.Contains(SaveUnitPrototype.Flag.Invisible, Unit("Nuclear Submarine").flags);
		Assert.DoesNotContain(SaveUnitPrototype.Flag.Invisible, Unit("Destroyer").flags);
		foreach (string seer in new[] { "Submarine", "Nuclear Submarine", "Destroyer", "AEGIS Cruiser" }) {
			Assert.Contains(SaveUnitPrototype.Flag.DetectInvisible, Unit(seer).flags);
		}
		Assert.DoesNotContain(SaveUnitPrototype.Flag.DetectInvisible, Unit("Battleship").flags);

		Assert.Contains(SaveUnitPrototype.Flag.HiddenNationality, Unit("Privateer").flags);
		Assert.DoesNotContain(SaveUnitPrototype.Flag.HiddenNationality, Unit("Frigate").flags);

		Assert.Contains(SaveUnitPrototype.Flag.StealthAttack, Unit("Submarine").flags);
		Assert.Contains(SaveUnitPrototype.Flag.StealthAttack, Unit("F-15").flags);
		Assert.Contains("Transport", Unit("Submarine").stealthTargets);
		Assert.DoesNotContain("Warrior", Unit("Submarine").stealthTargets);
		Assert.DoesNotContain(SaveUnitPrototype.Flag.StealthAttack, Unit("Destroyer").flags);
		Assert.Null(Unit("Destroyer").stealthTargets);
	}

	// Each civ's King is the unit its RACE section names ("for the
	// Americans, the 'King' unit is Abraham Lincoln", Conquests Civilopedia,
	// GCON_Short_Game_Modes), and conquests.biq places the kings itself.
	[SkippableFact]
	public void EachCivHasItsOwnKing() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		SaveGame game = ImportScenario("Scenarios", "No Civ Traits.biq");

		Assert.Equal("Lincoln", game.Civilizations.Single(c => c.name == "America").kingUnit);
		SaveUnitPrototype lincoln = game.UnitPrototypes.Single(p => p.name == "Lincoln");
		Assert.Contains(SaveUnitPrototype.Flag.King, lincoln.flags);
		Assert.True(lincoln.unproducible);
		Assert.DoesNotContain(SaveUnitPrototype.Flag.King, game.UnitPrototypes.Single(p => p.name == "Warrior").flags);
		Assert.True(game.VictoryConditions.AutoPlaceKings);
		Assert.Equal(1, game.VictoryConditions.CityEliminationCount);
	}

	// Fall of Rome plays Elimination, with each civ able to lose 8 cities;
	// the Middle Ages plays Mass Regicide with its kings on the map.
	[SkippableFact]
	public void ScenariosKeepTheirInstantEliminationRules() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		VictoryConditions fallOfRome = ImportScenario("Conquests", "3 Fall of Rome.biq").VictoryConditions;
		Assert.True(fallOfRome.CityElimination);
		Assert.Equal(8, fallOfRome.CityEliminationCount);
		Assert.False(fallOfRome.Regicide);

		VictoryConditions middleAges = ImportScenario("Conquests", "4 Middle Ages.biq").VictoryConditions;
		Assert.True(middleAges.MassRegicide);
		Assert.False(middleAges.AutoPlaceKings);
		Assert.False(middleAges.CityElimination);
	}

	// Mesopotamia "ends immediately as soon as all seven Great Wonders of
	// the World have been constructed, [or] as soon as a civilzation has
	// accumulated 5500 victory points", scoring "Wonder Cost (2.5 VP * build
	// cost)", "Defeating an enemy unit (5 VP * unit cost)", "Advancement Cost
	// (5 VP * tech cost)" and "City Conquest (100 VP * city population)"
	// (its Civilopedia's GCON_Victory).
	[SkippableFact]
	public void MesopotamiaScoresVictoryPoints() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		VictoryConditions conditions = ImportScenario("Conquests", "1 Mesopotamia.biq").VictoryConditions;

		Assert.True(conditions.AllowWonderVictory);
		Assert.True(conditions.VictoryLocations);
		Assert.Equal(5500, conditions.VictoryPointLimit);
		Assert.Equal(25, conditions.WonderVictoryPoints);
		Assert.Equal(5, conditions.DefeatVictoryPoints);
		Assert.Equal(5, conditions.AdvanceVictoryPoints);
		Assert.Equal(100, conditions.CityConquestVictoryPoints);
	}

	// The Middle Ages scores Victory Locations its map places, 25 points a
	// turn each, and places none itself.
	[SkippableFact]
	public void TheMiddleAgesHasVictoryLocations() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		SaveGame game = ImportScenario("Conquests", "4 Middle Ages.biq");

		Assert.True(game.VictoryConditions.VictoryLocations);
		Assert.Equal(25, game.VictoryConditions.VictoryLocationPoints);
		Assert.False(game.VictoryConditions.AutoPlaceVictoryLocations);
		Assert.Contains(game.Map.tiles, t => t.features.Contains(SaveTile.VictoryLocationFeature));
		// Not every tile.
		Assert.True(game.Map.tiles.Count(t => t.features.Contains(SaveTile.VictoryLocationFeature)) < 100);
	}
}
