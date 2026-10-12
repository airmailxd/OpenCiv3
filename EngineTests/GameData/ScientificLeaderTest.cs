using System.Linq;
using C7Engine;
using C7Engine.AI.UnitAI;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Scientific great leaders: rolled for by the first civ to research a tech,
// and used to finish anything a city builds, great wonders included.
public class ScientificLeaderTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;
	private readonly City capital;

	public ScientificLeaderTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		EngineStorage.messagesToUI.Clear();

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman).Take(2).ToArray();
		us = civs[0];
		them = civs[1];
		us.civilization.traits.Remove(Civilization.Trait.Scientific);
		capital = us.cities.FirstOrDefault(c => c.capital) ?? us.cities.FirstOrDefault() ?? BuildCity(us);
	}

	private class FixedRandom(double sample) : System.Random {
		protected override double Sample() => sample;
	}

	private T WithRandom<T>(double sample, System.Func<T> action) {
		System.Random original = C7GameData.GameData.rng;
		C7GameData.GameData.rng = new FixedRandom(sample);
		try {
			return action();
		} finally {
			C7GameData.GameData.rng = original;
		}
	}

	private static bool IsEmptyLand(Tile t) {
		return t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp && !t.IsImpassable();
	}

	private City BuildCity(Player owner) {
		Tile tile = gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities() && t.OwningPlayer() == null
			&& t.neighbors.Values.All(n => n == Tile.NONE || !n.HasCity()));
		return CityInteractions.BuildCity(tile, owner, $"City {owner.cities.Count}");
	}

	private Building BuildingNamed(string name) {
		return gameData.Buildings.Single(b => b.name == name);
	}

	private MapUnit SpawnScientificLeader(Tile tile) {
		MapUnit leader = gameData.SpawnUnit(us, gameData.unitPrototypes.Single(p => p.name == "Leader"), tile);
		leader.isScientificLeader = true;
		return leader;
	}

	// Finishes researching a tech no civ knows yet, rolling the sample.
	private Tech ResearchNewTech(double sample) {
		Tech tech = us.GetAvailableTechsToResearch(gameData.techs)
			.First(t => !gameData.players.Any(p => p.knownTechs.Contains(t.id)));
		us.SetCurrentlyResearchedTech(tech.id);
		us.turnsResearched = 1000;
		WithRandom(sample, () => { us.DoPerTurnScienceUpdates(gameData); return 0; });
		Assert.Contains(tech.id, us.knownTechs);
		return tech;
	}

	private MapUnit ScientificLeader() => us.units.SingleOrDefault(u => u.IsScientificLeader());

	[Fact]
	public void ChancesAreThreeAndFivePercent() {
		Assert.Equal(0.03, MapUnit.ScientificLeaderChanceFor(us));
		us.civilization.traits.Add(Civilization.Trait.Scientific);
		Assert.Equal(0.05, MapUnit.ScientificLeaderChanceFor(us));
	}

	[Fact]
	public void BeingFirstToResearchATechCanBringOne() {
		ResearchNewTech(0.029);

		MapUnit leader = ScientificLeader();
		Assert.NotNull(leader);
		Assert.Equal(capital.location, leader.location);
		Assert.Equal("Scientific Leader", leader.name);
	}

	[Theory]
	[InlineData(false, 0.031, false)]
	[InlineData(true, 0.031, true)]
	[InlineData(true, 0.049, true)]
	[InlineData(true, 0.051, false)]
	public void ScientificCivsHaveTheBetterChance(bool scientific, double roll, bool expected) {
		if (scientific) {
			us.civilization.traits.Add(Civilization.Trait.Scientific);
		}
		ResearchNewTech(roll);
		Assert.Equal(expected, ScientificLeader() != null);
	}

	[Fact]
	public void TechsOthersKnowBringNone() {
		Tech tech = us.GetAvailableTechsToResearch(gameData.techs).First();
		them.knownTechs.Add(tech.id);
		us.SetCurrentlyResearchedTech(tech.id);
		us.turnsResearched = 1000;
		WithRandom(0.0, () => { us.DoPerTurnScienceUpdates(gameData); return 0; });

		Assert.Contains(tech.id, us.knownTechs);
		Assert.Null(ScientificLeader());
	}

	[Fact]
	public void TechsGainedOtherwiseBringNone() {
		Tech tech = us.GetAvailableTechsToResearch(gameData.techs)
			.First(t => !gameData.players.Any(p => p.knownTechs.Contains(t.id)));
		WithRandom(0.0, () => { us.AcquireTech(gameData, tech); return 0; });
		Assert.Null(ScientificLeader());
	}

	[Fact]
	public void TheRuleCanTurnThemOff() {
		gameData.rules.ScientificLeaders = false;
		ResearchNewTech(0.0);
		Assert.Null(ScientificLeader());
	}

	[Fact]
	public void TheRuleIsOnByDefaultAndSaved() {
		Assert.True(new Rules().ScientificLeaders);
		gameData.rules.ScientificLeaders = false;
		C7GameData.GameData loaded = SaveGame.FromGameData(gameData).ToGameData(fixture.behaviors);
		Assert.False(loaded.rules.ScientificLeaders);
	}

	[Fact]
	public void AScientificLeaderSurvivesASave() {
		MapUnit leader = SpawnScientificLeader(capital.location);
		C7GameData.GameData loaded = SaveGame.FromGameData(gameData).ToGameData(fixture.behaviors);
		Assert.True(loaded.mapUnits.Single(u => u.id == leader.id).IsScientificLeader());
	}

	[Fact]
	public void ItFinishesAGreatWonder() {
		MapUnit leader = SpawnScientificLeader(capital.location);
		Building wonder = BuildingNamed("The Great Wall");
		capital.SetItemBeingProduced(wonder);

		Assert.Null(leader.HurryProductionBlocker());
		Assert.Contains(UnitAction.HurryBuilding, leader.GetAvailableActions());
		Assert.True(leader.HurryProductionAsLeader());

		Assert.Equal(us.ShieldCost(wonder), capital.shieldsStored);
		Assert.DoesNotContain(leader, gameData.mapUnits);
		capital.HandleCityProduction(gameData);
		Assert.Contains(wonder.name, gameData.GreatWondersBuilt);
	}

	[Fact]
	public void ItCantFormAnArmy() {
		MapUnit leader = SpawnScientificLeader(capital.location);
		Assert.False(leader.CanFormArmy());
		Assert.DoesNotContain(UnitAction.BuildArmy, leader.GetAvailableActions());
	}

	// A scientific leader doesn't stop an elite unit producing a military one.
	[Fact]
	public void ItDoesntCountAgainstTheMilitaryLeaderLimit() {
		SpawnScientificLeader(capital.location);
		Tile tile = capital.location.neighbors.Values.First(t => t != Tile.NONE && t.IsLand());
		MapUnit tank = gameData.SpawnUnit(us, gameData.unitPrototypes.Single(p => p.name == "Tank"), tile);
		tank.experienceLevel = gameData.experienceLevels.Last();
		tank.experienceLevelKey = tank.experienceLevel.key;
		MapUnit enemy = gameData.SpawnUnit(them, gameData.unitPrototypes.Single(p => p.name == "Warrior"), tile);
		Assert.True(tank.CouldProduceLeaderBeating(enemy, wasElite: true));
	}

	// The AI finishes the costliest wonder it could build with its leader,
	// switching the city to it.
	[Fact]
	public void TheAIUsesItOnItsCostliestWonder() {
		capital.SetItemBeingProduced(BuildingNamed("Temple"));
		(City city, Building wonder) = PlayerAI.BestWonderForScientificLeader(us);
		Assert.NotNull(wonder);
		Assert.True(City.IsWonder(wonder));
		foreach (IProducible p in city.ListProductionOptions(gameData)) {
			if (City.IsWonder(p)) {
				Assert.True(us.ShieldCost(p) <= us.ShieldCost(wonder));
			}
		}

		MapUnit leader = SpawnScientificLeader(city.location);
		Assert.True(PlayerAI.UseLeaderInCity(leader, us));

		Assert.Equal(wonder, city.itemBeingProduced);
		Assert.Equal(us.ShieldCost(wonder), city.shieldsStored);
		Assert.DoesNotContain(leader, gameData.mapUnits);
	}

	// Elsewhere, it heads for that city.
	[Fact]
	public void TheAISendsItToTheCityWithTheWonder() {
		(City city, _) = PlayerAI.BestWonderForScientificLeader(us);
		Assert.NotNull(city);
		Tile away = gameData.map.tiles.First(t => IsEmptyLand(t) && t.DistanceTo(city.location) is > 1 and < 4);
		MapUnit leader = SpawnScientificLeader(away);

		Assert.False(PlayerAI.UseLeaderInCity(leader, us));
		DefenderAI ai = Assert.IsType<DefenderAI>(leader.currentAI);
		Assert.Equal(city.location, ai.data.destination);
	}
}
