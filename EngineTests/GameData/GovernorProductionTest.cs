using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// The city governor's Production options: "Manage Production", "Never build
// (Small) Wonders" and how often to produce each kind of thing (see
// GovernorProduction).
public class GovernorProductionTest : IClassFixture<SaveGameFixture>, System.IDisposable {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;
	private readonly Player human;

	public GovernorProductionTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		human = gameData.players.First(p => p.isHuman);
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private City BuildCity(Player owner) {
		Tile tile = gameData.map.tiles.First(t => t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp
			&& !t.IsImpassable() && t.IsAllowCities() && t.neighbors.Values.All(n => n == Tile.NONE || !n.HasCity()));
		return CityInteractions.BuildCity(tile, owner, $"City {owner.cities.Count}");
	}

	private UnitPrototype Unit(string name) => gameData.unitPrototypes.Single(p => p.name == name);
	private Building Building(string name) => gameData.Buildings.Single(b => b.name == name);

	// The kinds come from the BIQ's AI strategies (Civ3 FUN_004453e0).
	[Fact]
	public void UnitsAreSortedByTheirAIStrategies() {
		Assert.Equal(ProductionKind.Settlers, GovernorProduction.KindsOf(Unit("Settler"), often: false));
		Assert.Equal(ProductionKind.Settlers | ProductionKind.Growth, GovernorProduction.KindsOf(Unit("Settler"), often: true));
		Assert.Equal(ProductionKind.Workers, GovernorProduction.KindsOf(Unit("Worker"), often: false));
		Assert.True(GovernorProduction.KindsOf(Unit("Warrior"), often: false).HasFlag(ProductionKind.OffensiveGroundUnits));
		Assert.True(GovernorProduction.KindsOf(Unit("Spearman"), often: false).HasFlag(ProductionKind.DefensiveGroundUnits));
		Assert.True(GovernorProduction.KindsOf(Unit("Catapult"), often: false).HasFlag(ProductionKind.Artillery));
		Assert.True(GovernorProduction.KindsOf(Unit("Explorer"), often: false).HasFlag(ProductionKind.Exploration));
		Assert.True(GovernorProduction.KindsOf(Unit("Galley"), often: false).HasFlag(ProductionKind.NavalUnits));
		Assert.True(GovernorProduction.KindsOf(Unit("Bomber"), often: false).HasFlag(ProductionKind.AirUnits));
	}

	// The kinds come from the improvement's effects (Civ3 FUN_004450d0).
	[Fact]
	public void ImprovementsAreSortedByTheirEffects() {
		Assert.True(GovernorProduction.KindsOf(Building("Temple"), often: false).HasFlag(ProductionKind.Happiness));
		Assert.True(GovernorProduction.KindsOf(Building("Library"), often: false).HasFlag(ProductionKind.Science));
		Assert.True(GovernorProduction.KindsOf(Building("Library"), often: false).HasFlag(ProductionKind.Culture));
		Assert.True(GovernorProduction.KindsOf(Building("Granary"), often: false).HasFlag(ProductionKind.Growth));
		Assert.Equal(ProductionKind.Production | ProductionKind.Science | ProductionKind.Wealth | ProductionKind.DefensiveGroundUnits,
			GovernorProduction.KindsOf(Building("Courthouse"), often: false));
		Assert.True(GovernorProduction.KindsOf(Building("Barracks"), often: false).HasFlag(ProductionKind.OffensiveGroundUnits));
		Assert.True(GovernorProduction.KindsOf(Building("Harbor"), often: false).HasFlag(ProductionKind.Trade));
	}

	// An option is ruled out only when all its kinds are "Never".
	[Fact]
	public void NeverRulesOutOnlyWhatIsAllNever() {
		CityGovernor governor = new();
		governor.SetHowOften(ProductionKind.Production, CityGovernor.Frequency.Never);
		Assert.False(GovernorProduction.Excludes(governor, Building("Courthouse")));

		governor.SetHowOften(ProductionKind.Science, CityGovernor.Frequency.Never);
		governor.SetHowOften(ProductionKind.Wealth, CityGovernor.Frequency.Never);
		governor.SetHowOften(ProductionKind.DefensiveGroundUnits, CityGovernor.Frequency.Never);
		Assert.True(GovernorProduction.Excludes(governor, Building("Courthouse")));

		// "Often" kinds don't count for "Never": a Settler is Growth only
		// when it comes to "Often".
		CityGovernor noGrowth = new();
		noGrowth.SetHowOften(ProductionKind.Growth, CityGovernor.Frequency.Never);
		Assert.False(GovernorProduction.Excludes(noGrowth, Unit("Settler")));
		Assert.False(GovernorProduction.Excludes(new CityGovernor(), Unit("Settler")));
	}

	[Fact]
	public void NeverBuildWondersRulesOutWonders() {
		Building wonder = gameData.Buildings.First(b => b.IsGreatWonder());
		Building smallWonder = gameData.Buildings.First(b => b.isSmallWonder);
		Assert.True(GovernorProduction.Excludes(new CityGovernor { neverBuildWonders = true }, wonder));
		Assert.False(GovernorProduction.Excludes(new CityGovernor { neverBuildWonders = true }, smallWonder));
		Assert.True(GovernorProduction.Excludes(new CityGovernor { neverBuildSmallWonders = true }, smallWonder));
		Assert.False(GovernorProduction.Excludes(new CityGovernor { neverBuildSmallWonders = true }, Building("Temple")));
	}

	// "Often" doubles the score, or adds a quarter while the owner has
	// fewer than 3 cities.
	[Fact]
	public void OftenRaisesTheScore() {
		CityGovernor governor = new();
		governor.SetHowOften(ProductionKind.Settlers, CityGovernor.Frequency.Often);
		BuildCity(human);
		Assert.True(human.cities.Count < 3);
		Assert.Equal(12.5f, GovernorProduction.AdjustScore(governor, human, Unit("Settler"), 10));
		Assert.Equal(10f, GovernorProduction.AdjustScore(governor, human, Unit("Worker"), 10));
		while (human.cities.Count < 3) {
			BuildCity(human);
		}
		Assert.Equal(20f, GovernorProduction.AdjustScore(governor, human, Unit("Settler"), 10));
		Assert.Equal(-5f, GovernorProduction.AdjustScore(governor, human, Unit("Settler"), -10));
	}

	[Fact]
	public void FrequenciesAreKeptApart() {
		CityGovernor governor = new();
		Assert.Equal(CityGovernor.Frequency.Sometimes, governor.HowOften(ProductionKind.Culture));
		governor.SetHowOften(ProductionKind.Culture, CityGovernor.Frequency.Often);
		Assert.Equal(CityGovernor.Frequency.Often, governor.HowOften(ProductionKind.Culture));
		governor.SetHowOften(ProductionKind.Culture, CityGovernor.Frequency.Never);
		Assert.Equal(CityGovernor.Frequency.Never, governor.HowOften(ProductionKind.Culture));
		Assert.Equal(ProductionKind.None, governor.produceOften);
		governor.SetHowOften(ProductionKind.Culture, CityGovernor.Frequency.Sometimes);
		Assert.Equal(ProductionKind.None, governor.produceNever);
	}

	// The choice never falls on what the governor rules out.
	[Fact]
	public void ChoiceRespectsNever() {
		City city = BuildCity(human);
		// The techs for some improvements.
		foreach (string name in new[] { "Temple", "Granary", "Barracks", "Library" }) {
			if (Building(name).requiredTech is Tech tech) {
				human.knownTechs.Add(tech.id);
			}
		}
		// No units at all.
		CityGovernor governor = new();
		foreach (ProductionKind kind in new[] { ProductionKind.OffensiveGroundUnits, ProductionKind.DefensiveGroundUnits,
				ProductionKind.Artillery, ProductionKind.Settlers, ProductionKind.Workers, ProductionKind.NavalUnits,
				ProductionKind.AirUnits, ProductionKind.Exploration }) {
			governor.SetHowOften(kind, CityGovernor.Frequency.Never);
		}
		city.governor = governor;
		Assert.Contains(city.ListProductionOptions(gameData), o => o is Building && !GovernorProduction.Excludes(governor, o));
		for (int i = 0; i < 10; ++i) {
			IProducible choice = ChooseProducible.Choose(city, human);
			Assert.NotNull(choice);
			Assert.False(GovernorProduction.Excludes(governor, choice), $"chose {choice}");
			Assert.IsNotType<UnitPrototype>(choice);
		}
	}

	// If the governor rules out everything, the city still builds something.
	[Fact]
	public void RulingOutEverythingStillChoosesSomething() {
		City city = BuildCity(human);
		CityGovernor governor = new() { neverBuildWonders = true, neverBuildSmallWonders = true };
		foreach (ProductionKind kind in System.Enum.GetValues<ProductionKind>()) {
			governor.SetHowOften(kind, CityGovernor.Frequency.Never);
		}
		city.governor = governor;
		Assert.NotNull(ChooseProducible.Choose(city, human));
	}

	private void Finish(City city, IProducible item) {
		city.SetItemBeingProduced(item);
		city.SetStoredShields(city.owner.ShieldCost(item));
		city.HandleCityProduction(gameData);
	}

	// Without "Manage Production" a human's city builds the same unit
	// again; with it, the governor chooses, here never the unit it was told
	// never to produce.
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void ManageProductionChoosesAfterAUnit(bool manage) {
		City city = BuildCity(human);
		UnitPrototype warrior = Unit("Warrior");
		CityGovernor governor = new() { manageProduction = manage };
		governor.SetHowOften(ProductionKind.OffensiveGroundUnits, CityGovernor.Frequency.Never);
		city.governor = governor;

		Finish(city, warrior);

		if (manage) {
			Assert.NotEqual(warrior, city.itemBeingProduced);
			Assert.False(GovernorProduction.Excludes(governor, city.itemBeingProduced));
		} else {
			Assert.Equal(warrior, city.itemBeingProduced);
		}
	}

	// The build queue comes before the governor's choice.
	[Fact]
	public void QueueComesFirst() {
		City city = BuildCity(human);
		city.governor = new CityGovernor { manageProduction = true };
		UnitPrototype worker = Unit("Worker");
		city.EnqueueProduction(worker);

		Finish(city, Unit("Warrior"));

		Assert.Equal(worker, city.itemBeingProduced);
	}

	[Fact]
	public void ProductionOptionsSurviveSaveAndLoad() {
		City city = BuildCity(human);
		city.governor = new CityGovernor { manageProduction = true, neverBuildSmallWonders = true };
		city.governor.SetHowOften(ProductionKind.Artillery, CityGovernor.Frequency.Never);
		city.governor.SetHowOften(ProductionKind.Culture, CityGovernor.Frequency.Often);

		C7GameData.GameData loaded = CreateGame.ReplaceWithSnapshot(SaveGame.FromGameData(gameData), fixture.behaviors);

		CityGovernor governor = loaded.cities.Single(c => c.id == city.id).governor;
		Assert.True(governor.manageProduction);
		Assert.False(governor.neverBuildWonders);
		Assert.True(governor.neverBuildSmallWonders);
		Assert.Equal(CityGovernor.Frequency.Never, governor.HowOften(ProductionKind.Artillery));
		Assert.Equal(CityGovernor.Frequency.Often, governor.HowOften(ProductionKind.Culture));
		Assert.Equal(CityGovernor.Frequency.Sometimes, governor.HowOften(ProductionKind.Growth));
		Assert.True(governor.SameAs(city.governor));
	}
}
