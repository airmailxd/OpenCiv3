using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Goody huts, and the news of a great wonder's completion.
public class GoodyHutTest : IClassFixture<SaveGameFixture> {
	private readonly C7GameData.GameData gameData;
	private readonly Player human;
	private readonly Player ai;
	private readonly Player barbarians;

	public GoodyHutTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		EngineStorage.messagesToUI.Clear();

		human = gameData.players.First(p => p.isHuman);
		ai = gameData.players.First(p => !p.isBarbarians && !p.isHuman);
		barbarians = gameData.players.First(p => p.isBarbarians);
	}

	private UnitPrototype Prototype(string name) {
		return gameData.unitPrototypes.Single(p => p.name == name);
	}

	private static bool IsEmptyLand(Tile t) {
		return t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp && !t.IsImpassable();
	}

	// An unowned land tile with empty land all around, and a neighbor of it
	// to walk in from.
	private (Tile from, TileDirection dir, Tile hut) FindHutSite() {
		foreach (Tile hut in gameData.map.tiles.Where(t => IsEmptyLand(t) && t.OwningPlayer() == null)) {
			if (!hut.neighbors.Values.All(n => n != Tile.NONE && IsEmptyLand(n) && n.OwningPlayer() == null)) {
				continue;
			}
			(TileDirection dir, Tile from) = hut.neighbors.First();
			return (from, dir.Reversed(), hut);
		}
		throw new System.Exception("No site for a hut found");
	}

	private class FixedRandom(double sample) : System.Random {
		protected override double Sample() => sample;
	}

	[Fact]
	public async Task EnteringAHutOpensIt() {
		(Tile from, TileDirection dir, Tile hut) = FindHutSite();
		hut.hasGoodyHut = true;
		MapUnit warrior = gameData.SpawnUnit(ai, Prototype("Warrior"), from);

		Assert.True(await warrior.Move(dir));

		Assert.Equal(hut, warrior.location);
		Assert.False(hut.hasGoodyHut);
	}

	[Fact]
	public void BarbariansAndShipsLeaveHutsAlone() {
		(Tile from, _, Tile hut) = FindHutSite();
		hut.hasGoodyHut = true;
		MapUnit barbarian = gameData.SpawnUnit(barbarians, Prototype("Warrior"), from);

		Assert.Null(GoodyHuts.Enter(gameData, barbarian, hut));
		Assert.True(hut.hasGoodyHut);

		Tile coast = gameData.map.tiles.First(t => t.IsCoast() && t.unitsOnTile.Count == 0);
		MapUnit galley = gameData.SpawnUnit(ai, Prototype("Galley"), coast);
		Assert.Null(GoodyHuts.Enter(gameData, galley, hut));
		Assert.True(hut.hasGoodyHut);
	}

	[Fact]
	public void HutsSurviveASave() {
		Tile hut = FindHutSite().hut;
		hut.hasGoodyHut = true;

		SaveTile saved = new(hut);
		Assert.Contains("goodyHut", saved.features);
		Assert.True(saved.ToTile(gameData.terrainTypes, gameData.Resources, gameData.terrainImprovements).hasGoodyHut);
	}

	[Fact]
	public void HarderLevelsHaveStingierHuts() {
		(Tile from, _, Tile hut) = FindHutSite();
		MapUnit warrior = gameData.SpawnUnit(ai, Prototype("Warrior"), from);

		Dictionary<GoodyHuts.Outcome, double> easy = GoodyHuts.Weigh(gameData, warrior, hut, 0);
		Dictionary<GoodyHuts.Outcome, double> hard = GoodyHuts.Weigh(gameData, warrior, hut, 1);

		Assert.True(hard[GoodyHuts.Outcome.Barbarians] >= easy[GoodyHuts.Outcome.Barbarians]);
		Assert.True(hard[GoodyHuts.Outcome.Unit] < easy[GoodyHuts.Outcome.Unit]);
	}

	[Fact]
	public void HutGoldIs25EarlyAndGrowsWithTheEras() {
		ai.eraCivilopediaName = "ERAS_Ancient_Times";
		Assert.Equal(25, GoodyHuts.GoldFor(ai));
		ai.eraCivilopediaName = "ERAS_Middle_Ages";
		Assert.Equal(50, GoodyHuts.GoldFor(ai));
		ai.eraCivilopediaName = "ERAS_Industrial_Age";
		Assert.Equal(75, GoodyHuts.GoldFor(ai));
	}

	[Fact]
	public void TechsOnlyComeFromHutsInTheAncientEra() {
		(Tile from, _, Tile hut) = FindHutSite();
		MapUnit warrior = gameData.SpawnUnit(ai, Prototype("Warrior"), from);
		ai.eraCivilopediaName = "ERAS_Middle_Ages";
		Assert.Equal(0, GoodyHuts.Weigh(gameData, warrior, hut, 0.5)[GoodyHuts.Outcome.Tech]);
	}

	[Fact]
	public void UnitsThatCantFightDontFindBarbarians() {
		(Tile from, _, Tile hut) = FindHutSite();
		UnitPrototype scoutType = gameData.unitPrototypes.First(p => p.IsLandUnit() && p.attack == 0 && !p.isSettler && !p.isWorker);
		MapUnit scout = gameData.SpawnUnit(ai, scoutType, from);
		Assert.Equal(0, GoodyHuts.Weigh(gameData, scout, hut, 1)[GoodyHuts.Outcome.Barbarians]);
	}

	[Fact]
	public void ExpansionistCivsDontFindBarbarians() {
		(Tile from, _, Tile hut) = FindHutSite();
		MapUnit warrior = gameData.SpawnUnit(ai, Prototype("Warrior"), from);
		ai.civilization.traits.Add(Civilization.Trait.Expansionist);
		Assert.Equal(0, GoodyHuts.Weigh(gameData, warrior, hut, 1)[GoodyHuts.Outcome.Barbarians]);
	}

	[Fact]
	public void HumansHutsFollowTheDifficultyLevel() {
		gameData.gameDifficulty = gameData.difficulties.First();
		Assert.Equal(0, GoodyHuts.Hardness(gameData, human));
		gameData.gameDifficulty = gameData.difficulties.Last();
		Assert.Equal(1, GoodyHuts.Hardness(gameData, human));
		// AIs always get middling huts.
		Assert.Equal(0.5, GoodyHuts.Hardness(gameData, ai));
	}

	[Fact]
	public void OnlyCivsBehindOnCitiesFindCities() {
		(Tile from, _, Tile hut) = FindHutSite();
		MapUnit warrior = gameData.SpawnUnit(ai, Prototype("Warrior"), from);
		List<City> aiCities = ai.cities.ToList();

		// With no cities while another civ has one, the AI is behind.
		ai.cities.Clear();
		if (!gameData.players.Any(p => p.cities.Count > 0)) {
			Player other = gameData.players.First(p => p != ai && !p.isBarbarians);
			CityInteractions.BuildCity(gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities() && t.DistanceTo(hut) > 3), other, "Elsewhere");
		}
		Assert.True(GoodyHuts.Weigh(gameData, warrior, hut, 0.5)[GoodyHuts.Outcome.City] > 0);

		// With every other civ's cities gone, it's ahead of the average.
		ai.cities.AddRange(aiCities);
		if (ai.cities.Count == 0) {
			ai.cities.Add(CityInteractions.BuildCity(gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities() && t.DistanceTo(hut) > 3), ai, "Aheadville"));
		}
		foreach (Player p in gameData.players.Where(p => p != ai)) {
			p.cities.Clear();
		}
		Dictionary<GoodyHuts.Outcome, double> ahead = GoodyHuts.Weigh(gameData, warrior, hut, 0.5);
		Assert.Equal(0, ahead[GoodyHuts.Outcome.City]);
		Assert.Equal(0, ahead[GoodyHuts.Outcome.Settler]);
	}

	[Fact]
	public void AFoundTechIsLearned() {
		(Tile from, _, Tile hut) = FindHutSite();
		hut.hasGoodyHut = true;
		MapUnit warrior = gameData.SpawnUnit(ai, Prototype("Warrior"), from);
		ai.eraCivilopediaName = "ERAS_Ancient_Times";
		int before = ai.knownTechs.Count;

		// Find the roll that lands on the tech: Gold and Tech come first in
		// the outcomes' order.
		Dictionary<GoodyHuts.Outcome, double> weights = GoodyHuts.Weigh(gameData, warrior, hut, 0.5);
		Assert.True(weights[GoodyHuts.Outcome.Tech] > 0);
		double total = weights.Values.Sum();
		double sample = (weights[GoodyHuts.Outcome.Gold] + weights[GoodyHuts.Outcome.Tech] / 2) / total;

		System.Random original = C7GameData.GameData.rng;
		C7GameData.GameData.rng = new FixedRandom(sample);
		GoodyHuts.Outcome? outcome;
		try {
			outcome = GoodyHuts.Enter(gameData, warrior, hut);
		} finally {
			C7GameData.GameData.rng = original;
		}

		Assert.Equal(GoodyHuts.Outcome.Tech, outcome);
		Assert.Equal(before + 1, ai.knownTechs.Count);
	}

	[Fact]
	public void HumansHearWhatTheyFound() {
		(Tile from, _, Tile hut) = FindHutSite();
		hut.hasGoodyHut = true;
		MapUnit warrior = gameData.SpawnUnit(human, Prototype("Warrior"), from);
		EngineStorage.messagesToUI.Clear();

		Assert.NotNull(GoodyHuts.Enter(gameData, warrior, hut));
		Assert.Single(EngineStorage.messagesToUI.OfType<MsgShowMilitaryAdvisorPopup>(), m => m.recipient == human);
	}

	// ---- Wonder news ----

	[Fact]
	public void EveryHumanHearsOfACompletedWonder() {
		Building wonder = gameData.Buildings.First(b => b.greatWonderProperties != null && !gameData.GreatWondersBuilt.Contains(b.name));
		City city = ai.cities.FirstOrDefault()
			?? CityInteractions.BuildCity(gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities()), ai, "Wonderville");
		city.SetItemBeingProduced(wonder);
		city.FillProductionBox();
		EngineStorage.messagesToUI.Clear();

		city.HandleCityProduction(gameData);

		Assert.Contains(wonder.name, gameData.GreatWondersBuilt);
		MsgShowDomesticAdvisorPopup news = Assert.Single(EngineStorage.messagesToUI.OfType<MsgShowDomesticAdvisorPopup>(), m => m.recipient == human);
		Assert.Contains(wonder.name, news.message);
		Assert.Contains(ai.civilization.noun ?? ai.civilization.name, news.message);
	}
}
