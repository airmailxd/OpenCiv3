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
	private (Tile from, TileDirection dir, Tile hut) FindHutSite(System.Func<Tile, bool> suits = null) {
		foreach (Tile hut in gameData.map.tiles.Where(t => IsEmptyLand(t) && t.OwningPlayer() == null && (suits == null || suits(t)))) {
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

	// A city site far (as corruption counts) from the hut and every city.
	private Tile FarCitySite(Tile hut) {
		return gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities() && t.OwningPlayer() == null
			&& GoodyHuts.CityDistance(t, hut) >= 6
			&& gameData.cities.All(c => GoodyHuts.CityDistance(c.location, t) >= 3));
	}

	// A hut site at least five tiles from any city.
	private (Tile from, TileDirection dir, Tile hut) FindRemoteHutSite() {
		return FindHutSite(t => t.IsAllowCities() && gameData.cities.All(c => GoodyHuts.CityDistance(c.location, t) >= 5));
	}

	// Leaves the player with one city, far from the hut, and behind the
	// average, as a city from a hut needs.
	private void LeaveBehindWithOneCity(Player player, Tile hut) {
		player.cities.Clear();
		CityInteractions.BuildCity(FarCitySite(hut), player, "Home");
		Player other = gameData.players.First(p => p != player && !p.isBarbarians);
		List<Player> civs = gameData.players.Where(p => !p.isBarbarians && !p.defeated).ToList();
		while (civs.Average(p => p.cities.Count) <= 1) {
			CityInteractions.BuildCity(FarCitySite(hut), other, $"Elsewhere {other.cities.Count}");
		}
	}

	// Enters the hut with the dice loaded so that it holds the wanted
	// outcome: the roll lands in the middle of that outcome's share, the
	// shares coming in the outcomes' order.
	private GoodyHuts.Outcome? EnterFinding(MapUnit unit, Tile hut, GoodyHuts.Outcome wanted) {
		Dictionary<GoodyHuts.Outcome, double> weights = GoodyHuts.Weigh(gameData, unit, hut, GoodyHuts.Level(gameData, unit.owner));
		Assert.True(weights[wanted] > 0);
		double before = System.Enum.GetValues<GoodyHuts.Outcome>().TakeWhile(o => o != wanted).Sum(o => weights.GetValueOrDefault(o));
		double sample = (before + weights[wanted] / 2) / weights.Values.Sum();

		System.Random original = C7GameData.GameData.rng;
		C7GameData.GameData.rng = new FixedRandom(sample);
		try {
			return GoodyHuts.Enter(gameData, unit, hut);
		} finally {
			C7GameData.GameData.rng = original;
		}
	}

	// A city founded next to a hut opens it for the city's owner, and no
	// barbarians come out (owner's decision, 2026-10-12).
	[Fact]
	public void FoundingACityNextToAHutOpensItWithoutBarbarians() {
		(Tile from, TileDirection _, Tile hut) = FindHutSite();
		Assert.Equal(0, GoodyHuts.Weigh(gameData, human, null, hut, GoodyHuts.Level(gameData, human))[GoodyHuts.Outcome.Barbarians]);

		int barbarianUnits = barbarians.units.Count;
		foreach (double sample in new[] { 0.0, 0.3, 0.6, 0.99 }) {
			hut.hasGoodyHut = true;
			System.Random original = C7GameData.GameData.rng;
			C7GameData.GameData.rng = new FixedRandom(sample);
			try {
				GoodyHuts.OpenNextToNewCity(gameData, new City(from, human, "Test", ID.None("")));
			} finally {
				C7GameData.GameData.rng = original;
			}
			Assert.False(hut.hasGoodyHut);
		}
		Assert.Equal(barbarianUnits, barbarians.units.Count);
	}

	[Fact]
	public void BuildingACityOpensTheHutsNextToIt() {
		(Tile from, TileDirection _, Tile hut) = FindHutSite();
		hut.hasGoodyHut = true;
		CityInteractions.BuildCity(from, human, "Hutton");
		Assert.False(hut.hasGoodyHut);
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
		ai.civilization.traits.Remove(Civilization.Trait.Expansionist);

		Dictionary<GoodyHuts.Outcome, double> easy = GoodyHuts.Weigh(gameData, warrior, hut, 0);
		Dictionary<GoodyHuts.Outcome, double> hard = GoodyHuts.Weigh(gameData, warrior, hut, 7);

		Assert.True(hard[GoodyHuts.Outcome.Barbarians] >= easy[GoodyHuts.Outcome.Barbarians]);
		Assert.True(hard[GoodyHuts.Outcome.Unit] < easy[GoodyHuts.Outcome.Unit]);
	}

	[Fact]
	public void HutGoldIs25AndFromTheMiddleAgesAlso50() {
		ai.eraCivilopediaName = "ERAS_Ancient_Times";
		for (int i = 0; i < 20; ++i) {
			Assert.Equal(25, GoodyHuts.GoldFor(ai));
		}
		foreach (string era in new[] { "ERAS_Middle_Ages", "ERAS_Industrial_Age" }) {
			ai.eraCivilopediaName = era;
			HashSet<int> amounts = new();
			for (int i = 0; i < 100; ++i) {
				amounts.Add(GoodyHuts.GoldFor(ai));
			}
			Assert.Equal(new HashSet<int> { 25, 50 }, amounts);
		}
	}

	// Only Ancient techs, not the current research unless it's the last,
	// and cheaper ones likelier.
	[Fact]
	public void HutTechsAreAncientCheapLikelierAndNotTheCurrentResearch() {
		ai.eraCivilopediaName = "ERAS_Ancient_Times";
		List<Tech> techs = GoodyHuts.TechsToLearn(gameData, ai);
		Assert.All(techs, t => Assert.Equal(0, EraUtils.GetEraIndex(t.EraCivilopediaName)));
		Assert.True(techs.Count > 1);
		ai.SetCurrentlyResearchedTech(techs[0].id);
		Assert.DoesNotContain(techs[0], GoodyHuts.TechsToLearn(gameData, ai));

		Tech cheap = new() { Name = "Cheap", Cost = 1 };
		Tech dear = new() { Name = "Dear", Cost = 9 };
		int cheapPicks = 0;
		C7GameData.GameData.rng = new System.Random(5);
		for (int i = 0; i < 1000; ++i) {
			if (GoodyHuts.PickTech([cheap, dear]) == cheap) {
				++cheapPicks;
			}
		}
		// Nine to one.
		Assert.InRange(cheapPicks, 850, 950);
	}

	// A hut becomes a city only four tiles (as corruption counts) from any
	// city, and only for a civ that has one.
	[Fact]
	public void HutCitiesNeedACityOfTheirOwnAndRoom() {
		(Tile from, _, Tile hut) = FindRemoteHutSite();
		MapUnit warrior = gameData.SpawnUnit(ai, Prototype("Warrior"), from);
		LeaveBehindWithOneCity(ai, hut);
		Assert.True(GoodyHuts.Weigh(gameData, warrior, hut, GoodyHuts.MiddleLevel)[GoodyHuts.Outcome.City] > 0);

		// A city 3.5 away (three diagonal steps and one more) is too near.
		Tile near = gameData.map.tiles.FirstOrDefault(t => IsEmptyLand(t) && t.IsAllowCities()
			&& GoodyHuts.CityDistance(t, hut) < GoodyHuts.MinCityDistance && GoodyHuts.CityDistance(t, hut) >= 2);
		Assert.NotNull(near);
		Player other = gameData.players.First(p => p != ai && !p.isBarbarians);
		City blocker = CityInteractions.BuildCity(near, other, "Too Near");
		Assert.Equal(0, GoodyHuts.Weigh(gameData, warrior, hut, GoodyHuts.MiddleLevel)[GoodyHuts.Outcome.City]);
		CityInteractions.DestroyCity(blocker);

		// No city of its own: none from a hut.
		ai.cities.Clear();
		Assert.Equal(0, GoodyHuts.Weigh(gameData, warrior, hut, GoodyHuts.MiddleLevel)[GoodyHuts.Outcome.City]);
	}

	[Fact]
	public void CorruptionDistanceCountsDiagonalStepsAsOneAndAHalf() {
		Tile a = gameData.map.tileAt(10, 10);
		// One diagonal step changes both coordinates by one.
		Assert.Equal(1, GoodyHuts.CityDistance(a, gameData.map.tileAt(11, 11)));
		// Two tiles east is one step along each axis.
		Assert.Equal(1.5, GoodyHuts.CityDistance(a, gameData.map.tileAt(12, 10)));
		Assert.Equal(4, GoodyHuts.CityDistance(a, gameData.map.tileAt(14, 14)));
	}

	// Border growth opens huts; with every Ancient tech known, such a hut
	// holds no unit, settler or city.
	[Fact]
	public void BorderGrowthOpensHuts() {
		(Tile from, _, Tile hut) = FindRemoteHutSite();
		hut.hasGoodyHut = true;
		// A city two tiles off, whose first borders don't reach the hut.
		Tile site = gameData.map.tiles.First(t => t.RankDistanceTo(hut) == 2 && IsEmptyLand(t) && t.IsAllowCities() && t.OwningPlayer() == null);
		City city = CityInteractions.BuildCity(site, ai, "Grower");
		Assert.True(hut.hasGoodyHut);

		// Its culture grows the borders over the hut.
		city.perPlayerCulture[ai] = 10;
		gameData.UpdateTileOwners();
		Assert.Same(city, hut.owningCity);
		GoodyHuts.OpenWithinBorders(gameData, city);
		Assert.False(hut.hasGoodyHut);

		foreach (Tech t in gameData.techs.Where(t => EraUtils.GetEraIndex(t.EraCivilopediaName) == 0)) {
			ai.knownTechs.Add(t.id);
		}
		Dictionary<GoodyHuts.Outcome, double> late = GoodyHuts.Weigh(gameData, ai, null, hut, GoodyHuts.MiddleLevel, byBorders: true);
		Assert.Equal(0, late[GoodyHuts.Outcome.Unit]);
		Assert.Equal(0, late[GoodyHuts.Outcome.Settler]);
		Assert.Equal(0, late[GoodyHuts.Outcome.City]);
		Assert.Equal(0, late[GoodyHuts.Outcome.Barbarians]);
	}

	[Fact]
	public void TechsOnlyComeFromHutsInTheAncientEra() {
		(Tile from, _, Tile hut) = FindHutSite();
		MapUnit warrior = gameData.SpawnUnit(ai, Prototype("Warrior"), from);
		ai.eraCivilopediaName = "ERAS_Middle_Ages";
		Assert.Equal(0, GoodyHuts.Weigh(gameData, warrior, hut, GoodyHuts.MiddleLevel)[GoodyHuts.Outcome.Tech]);
	}

	// "The unit popping the hut must not have the "All Terrain As Roads"
	// ability." (CFC 93244) Other units can, fighters or not.
	[Fact]
	public void UnitsThatTreatAllTerrainAsRoadsDontFindBarbarians() {
		(Tile from, _, Tile hut) = FindHutSite();
		ai.civilization.traits.Remove(Civilization.Trait.Expansionist);
		if (ai.cities.Count == 0) {
			CityInteractions.BuildCity(gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities() && t.DistanceTo(hut) > 3), ai, "Homeville");
		}
		gameData.SpawnUnit(ai, Prototype("Warrior"), from);

		MapUnit explorer = gameData.SpawnUnit(ai, Prototype("Explorer"), from);
		Assert.True(explorer.unitType.allTerrainAsRoads);
		Assert.Equal(0, GoodyHuts.Weigh(gameData, explorer, hut, 7)[GoodyHuts.Outcome.Barbarians]);

		UnitPrototype nonFighterType = gameData.unitPrototypes.First(p => p.IsLandUnit() && p.attack == 0 && !p.allTerrainAsRoads && p.isWorker);
		MapUnit worker = gameData.SpawnUnit(ai, nonFighterType, from);
		Assert.True(GoodyHuts.Weigh(gameData, worker, hut, 7)[GoodyHuts.Outcome.Barbarians] > 0);
	}

	// "You cant get a settler or city when building a city" (CFC 93244).
	[Fact]
	public void AHutOpenedByANewCityHoldsNoSettlerOrCity() {
		(Tile from, TileDirection _, Tile hut) = FindRemoteHutSite();
		MapUnit warrior = gameData.SpawnUnit(human, Prototype("Warrior"), from);
		// The human is behind on cities, so a hut a unit enters can hold a
		// city.
		LeaveBehindWithOneCity(human, hut);
		const int level = 0;
		Dictionary<GoodyHuts.Outcome, double> entered = GoodyHuts.Weigh(gameData, human, warrior, hut, level);
		Assert.True(entered[GoodyHuts.Outcome.City] > 0);

		Dictionary<GoodyHuts.Outcome, double> founded = GoodyHuts.Weigh(gameData, human, null, hut, level);
		Assert.Equal(0, founded[GoodyHuts.Outcome.Settler]);
		Assert.Equal(0, founded[GoodyHuts.Outcome.City]);
	}

	[Fact]
	public void ExpansionistCivsDontFindBarbarians() {
		(Tile from, _, Tile hut) = FindHutSite();
		MapUnit warrior = gameData.SpawnUnit(ai, Prototype("Warrior"), from);
		ai.civilization.traits.Add(Civilization.Trait.Expansionist);
		Assert.Equal(0, GoodyHuts.Weigh(gameData, warrior, hut, 7)[GoodyHuts.Outcome.Barbarians]);
	}

	[Fact]
	public void HumansHutsFollowTheDifficultyLevel() {
		Assert.Equal(GoodyHuts.LevelCount, gameData.difficulties.Count);
		for (int i = 0; i < gameData.difficulties.Count; i++) {
			gameData.gameDifficulty = gameData.difficulties[i];
			Assert.Equal(i, GoodyHuts.Level(gameData, human));
		}
		// AIs always get middling huts.
		Assert.Equal(GoodyHuts.MiddleLevel, GoodyHuts.Level(gameData, ai));
	}

	// A ruleset with fewer levels spreads them over the table's columns.
	[Fact]
	public void OtherNumbersOfLevelsSpanTheTable() {
		gameData.difficulties.RemoveRange(4, gameData.difficulties.Count - 4);
		gameData.gameDifficulty = gameData.difficulties[0];
		Assert.Equal(0, GoodyHuts.Level(gameData, human));
		gameData.gameDifficulty = gameData.difficulties[3];
		Assert.Equal(7, GoodyHuts.Level(gameData, human));
		gameData.gameDifficulty = gameData.difficulties[1];
		Assert.Equal(2, GoodyHuts.Level(gameData, human));
	}

	[Fact]
	public void EachLevelsOddsAddUpToAHundred() {
		for (int level = 0; level < GoodyHuts.LevelCount; level++) {
			Assert.Equal(100, GoodyHuts.BaseOdds.Values.Sum(row => row[level]), 3);
			// The transcribed percentages are rounded to a tenth.
			Assert.InRange(GoodyHuts.ExpansionistOdds.Values.Sum(row => row[level]), 99.6, 100.4);
		}
	}

	// The odds of what's possible are the table's, on the player's level.
	[Theory]
	[InlineData(0)]
	[InlineData(2)]
	[InlineData(7)]
	public void TheOddsComeFromTheTable(int level) {
		(Tile from, _, Tile hut) = FindHutSite(t => t.Resource == null || t.Resource == Resource.NONE);
		MapUnit warrior = gameData.SpawnUnit(ai, Prototype("Warrior"), from);
		ai.civilization.traits.Remove(Civilization.Trait.Expansionist);
		Dictionary<GoodyHuts.Outcome, double> weights = GoodyHuts.Weigh(gameData, warrior, hut, level);
		foreach (GoodyHuts.Outcome o in new[] { GoodyHuts.Outcome.Gold, GoodyHuts.Outcome.Map, GoodyHuts.Outcome.Unit, GoodyHuts.Outcome.Nothing }) {
			Assert.Equal(GoodyHuts.BaseOdds[o][level], weights[o]);
		}

		ai.civilization.traits.Add(Civilization.Trait.Expansionist);
		weights = GoodyHuts.Weigh(gameData, warrior, hut, level);
		foreach (GoodyHuts.Outcome o in new[] { GoodyHuts.Outcome.Gold, GoodyHuts.Outcome.Map, GoodyHuts.Outcome.Unit, GoodyHuts.Outcome.Nothing }) {
			Assert.Equal(GoodyHuts.ExpansionistOdds[o][level], weights[o]);
		}
	}

	// Picking with the table's odds finds each outcome about as often as the
	// table says, and an outcome that can't happen never, its share going to
	// the rest in proportion.
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void PicksFollowTheOdds(bool expansionist) {
		IReadOnlyDictionary<GoodyHuts.Outcome, double[]> table = expansionist ? GoodyHuts.ExpansionistOdds : GoodyHuts.BaseOdds;
		const int level = 2; // Regent
		Dictionary<GoodyHuts.Outcome, double> weights = table.ToDictionary(kv => kv.Key, kv => kv.Value[level]);
		weights[GoodyHuts.Outcome.Tech] = 0;
		double total = weights.Values.Sum();

		System.Random original = C7GameData.GameData.rng;
		C7GameData.GameData.rng = new System.Random(12345);
		Dictionary<GoodyHuts.Outcome, int> counts = new();
		const int samples = 40000;
		try {
			for (int i = 0; i < samples; i++) {
				GoodyHuts.Outcome o = GoodyHuts.Pick(weights);
				counts[o] = counts.GetValueOrDefault(o) + 1;
			}
		} finally {
			C7GameData.GameData.rng = original;
		}

		Assert.Equal(0, counts.GetValueOrDefault(GoodyHuts.Outcome.Tech));
		foreach ((GoodyHuts.Outcome o, double weight) in weights) {
			Assert.InRange(counts.GetValueOrDefault(o) / (double)samples, weight / total - 0.01, weight / total + 0.01);
		}
	}

	[Fact]
	public void AHutsMapShowsTheLandAround() {
		(Tile from, _, Tile hut) = FindHutSite();
		hut.hasGoodyHut = true;
		MapUnit warrior = gameData.SpawnUnit(ai, Prototype("Warrior"), from);
		Tile far = hut.GetTilesWithinRankDistance(GoodyHuts.MapRadius)
			.FirstOrDefault(t => t != Tile.NONE && t.DistanceTo(hut) >= 4 && !ai.tileKnowledge.isTileKnown(t));
		Assert.NotNull(far);

		Assert.Equal(GoodyHuts.Outcome.Map, EnterFinding(warrior, hut, GoodyHuts.Outcome.Map));
		Assert.True(ai.tileKnowledge.isTileKnown(far));
	}

	// The AI's cities keep a free tile from any other (owner's decision,
	// 2026-10-20), those from huts too.
	[Fact]
	public void AnAIFindsNoCityTwoTilesFromAnother() {
		(Tile from, _, Tile hut) = FindHutSite();
		MapUnit warrior = gameData.SpawnUnit(ai, Prototype("Warrior"), from);
		ai.cities.Clear();
		Player other = gameData.players.First(p => p != ai && !p.isBarbarians);
		Tile near = gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities() && t.DistanceTo(hut) == 2 && t != from);
		CityInteractions.BuildCity(near, other, "Nearby");
		Assert.Null(hut.OwningPlayer());

		Assert.Equal(0, GoodyHuts.Weigh(gameData, warrior, hut, GoodyHuts.MiddleLevel)[GoodyHuts.Outcome.City]);
	}

	[Fact]
	public void OnlyCivsBehindOnCitiesFindCities() {
		(Tile from, _, Tile hut) = FindRemoteHutSite();
		MapUnit warrior = gameData.SpawnUnit(ai, Prototype("Warrior"), from);

		// With one city while others have more on average, the AI is behind.
		LeaveBehindWithOneCity(ai, hut);
		Assert.True(GoodyHuts.Weigh(gameData, warrior, hut, GoodyHuts.MiddleLevel)[GoodyHuts.Outcome.City] > 0);

		// With every other civ's cities gone, it's ahead of the average.
		foreach (Player p in gameData.players.Where(p => p != ai)) {
			p.cities.Clear();
		}
		Dictionary<GoodyHuts.Outcome, double> ahead = GoodyHuts.Weigh(gameData, warrior, hut, GoodyHuts.MiddleLevel);
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

		Assert.Equal(GoodyHuts.Outcome.Tech, EnterFinding(warrior, hut, GoodyHuts.Outcome.Tech));
		Assert.Equal(before + 1, ai.knownTechs.Count);
	}

	// The UI drops the hut's news while another popup is up, so the news
	// must come before the question of what to research next.
	[Fact]
	public void HutNewsComesBeforeTheResearchQuestion() {
		if (human.cities.Count == 0) {
			CityInteractions.BuildCity(gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities()), human, "Homeville");
		}
		(Tile from, _, Tile hut) = FindHutSite(t => human.cities.All(c => c.location.DistanceTo(t) > 3));
		hut.hasGoodyHut = true;
		MapUnit warrior = gameData.SpawnUnit(human, Prototype("Warrior"), from);
		human.eraCivilopediaName = "ERAS_Ancient_Times";
		human.ResearchQueue.Clear();
		human.SetCurrentlyResearchedTech(null);
		EngineStorage.messagesToUI.Clear();

		Assert.Equal(GoodyHuts.Outcome.Tech, EnterFinding(warrior, hut, GoodyHuts.Outcome.Tech));

		List<MessageToUI> sent = EngineStorage.messagesToUI.ToList();
		int news = sent.FindIndex(m => m is MsgShowMilitaryAdvisorPopup);
		int question = sent.FindIndex(m => m is MsgShowScienceSelection);
		Assert.True(news >= 0, "no news of the hut");
		Assert.True(question >= 0, "not asked what to research");
		Assert.True(news < question, "the hut's news came after the research question");
	}

	// A city from a hut opens its screen for a human, as one they found does,
	// and after the news of it.
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void AHutsCityIsShownToAHuman(bool isHuman) {
		Player player = isHuman ? human : ai;
		(Tile from, _, Tile hut) = FindRemoteHutSite();
		hut.hasGoodyHut = true;
		MapUnit warrior = gameData.SpawnUnit(player, Prototype("Warrior"), from);
		// Behind on cities, while some other civ has more.
		LeaveBehindWithOneCity(player, hut);
		EngineStorage.messagesToUI.Clear();

		Assert.Equal(GoodyHuts.Outcome.City, EnterFinding(warrior, hut, GoodyHuts.Outcome.City));
		Assert.NotNull(hut.cityAtTile);

		List<MessageToUI> sent = EngineStorage.messagesToUI.ToList();
		if (!isHuman) {
			Assert.Empty(sent.OfType<MsgCityCreated>());
			return;
		}
		MsgCityCreated created = Assert.Single(sent.OfType<MsgCityCreated>());
		Assert.Same(hut.cityAtTile, created.city);
		Assert.Same(human, created.recipient);
		Assert.True(sent.FindIndex(m => m is MsgShowMilitaryAdvisorPopup) < sent.IndexOf(created));
	}

	// Barbarians can't open huts, so they mustn't make for one: they'd wait
	// on it forever.
	[Fact]
	public void BarbariansDontExploreTowardHuts() {
		(Tile from, _, Tile hut) = FindHutSite();
		hut.hasGoodyHut = true;

		// An AI's warrior next to the hut heads for it...
		MapUnit warrior = gameData.SpawnUnit(ai, Prototype("Warrior"), from);
		ai.tileKnowledge.AddTilesToKnown(from);
		Assert.Equal(hut, ExplorerAI.MaybeMakeAiData(warrior, ai)?.destination);
		gameData.RemoveUnit(warrior);

		// ...but a barbarian's, next to it or on it, doesn't.
		foreach (Tile at in new[] { from, hut }) {
			MapUnit barbarian = gameData.SpawnUnit(barbarians, Prototype("Warrior"), at);
			barbarians.tileKnowledge.AddTilesToKnown(at);
			Assert.NotEqual(hut, ExplorerAI.MaybeMakeAiData(barbarian, barbarians)?.destination);
		}
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

	// The news is Civ3's (Conquests' Text/script.txt), naming the village's
	// tribe from the barbarians' list.
	[Fact]
	public void HutNewsIsInCiv3sWords() {
		(Tile from, _, Tile hut) = FindHutSite(t => t.Resource == null || t.Resource == Resource.NONE);
		string tribe = BarbarianInteractions.TribeName(gameData, hut);
		Assert.Contains(tribe, barbarians.civilization.cityNames);
		Assert.Equal(tribe, BarbarianInteractions.TribeName(gameData, hut));

		hut.hasGoodyHut = true;
		MapUnit warrior = gameData.SpawnUnit(human, Prototype("Warrior"), from);
		EngineStorage.messagesToUI.Clear();
		int gold = GoodyHuts.GoldFor(human);
		Assert.Equal(GoodyHuts.Outcome.Gold, EnterFinding(warrior, hut, GoodyHuts.Outcome.Gold));
		Assert.Equal($"We got {gold} gold from the {tribe} tribe's village.",
			EngineStorage.messagesToUI.OfType<MsgShowMilitaryAdvisorPopup>().Single().message);

		hut.hasGoodyHut = true;
		EngineStorage.messagesToUI.Clear();
		Assert.Equal(GoodyHuts.Outcome.Map, EnterFinding(warrior, hut, GoodyHuts.Outcome.Map));
		Assert.Equal($"The friendly {tribe} tribe gave us maps of their region.",
			EngineStorage.messagesToUI.OfType<MsgShowMilitaryAdvisorPopup>().Single().message);

		hut.hasGoodyHut = true;
		EngineStorage.messagesToUI.Clear();
		Assert.Equal(GoodyHuts.Outcome.Unit, EnterFinding(warrior, hut, GoodyHuts.Outcome.Unit));
		Assert.Equal($"This friendly {tribe} village gave us a skilled Warrior.",
			EngineStorage.messagesToUI.OfType<MsgShowMilitaryAdvisorPopup>().Single().message);
	}

	[Fact]
	public void DispersingACampIsNewsInCiv3sWords() {
		(Tile from, TileDirection dir, Tile camp) = FindHutSite();
		camp.hasBarbarianCamp = true;
		gameData.map.barbarianCamps.Add(camp);
		MapUnit warrior = gameData.SpawnUnit(human, Prototype("Warrior"), from);
		EngineStorage.messagesToUI.Clear();

		warrior.OnEnterTile(camp, movedThere: true);

		Assert.Equal($"We dispersed a {BarbarianInteractions.TribeName(gameData, camp)} encampment and took 25 gold!",
			EngineStorage.messagesToUI.OfType<MsgShowMilitaryAdvisorPopup>().Single().message);
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
		MsgWonderCompleted news = Assert.Single(EngineStorage.messagesToUI.OfType<MsgWonderCompleted>(), m => m.recipient == human);
		Assert.Equal($"The {ai.civilization.noun ?? ai.civilization.name} have completed {wonder.name} in {city.name}!", news.Announcement());
		Assert.DoesNotContain(EngineStorage.messagesToUI, m => m is MsgShowDomesticAdvisorPopup);
	}

	[Fact]
	public void TheBuilderOfAWonderIsNotToldTwice() {
		// A second human, as in a hotseat game.
		ai.isHuman = true;
		Building wonder = gameData.Buildings.First(b => b.greatWonderProperties != null && !gameData.GreatWondersBuilt.Contains(b.name));
		City city = human.cities.FirstOrDefault()
			?? CityInteractions.BuildCity(gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities()), human, "Wonderville");
		city.SetItemBeingProduced(wonder);
		city.FillProductionBox();
		EngineStorage.messagesToUI.Clear();

		city.HandleCityProduction(gameData);

		// The builder hears of it only from the production popup.
		Assert.DoesNotContain(EngineStorage.messagesToUI.OfType<MsgWonderCompleted>(), m => m.recipient == human);
		Assert.DoesNotContain(EngineStorage.messagesToUI, m => m is MsgShowDomesticAdvisorPopup);
		Assert.Single(EngineStorage.messagesToUI.OfType<MsgCityProductionCompleted>(), m => m.recipient == human && m.completed == wonder.name);
		MsgWonderCompleted news = Assert.Single(EngineStorage.messagesToUI.OfType<MsgWonderCompleted>());
		Assert.Equal(ai, news.recipient);
		Assert.Equal(human, news.builder);
		Assert.Equal(wonder.name, news.wonder);
	}
}
