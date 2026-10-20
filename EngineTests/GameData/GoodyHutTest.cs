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
		Assert.Equal(0, GoodyHuts.Weigh(gameData, warrior, hut, GoodyHuts.MiddleLevel)[GoodyHuts.Outcome.Tech]);
	}

	[Fact]
	public void UnitsThatCantFightDontFindBarbarians() {
		(Tile from, _, Tile hut) = FindHutSite();
		UnitPrototype scoutType = gameData.unitPrototypes.First(p => p.IsLandUnit() && p.attack == 0 && !p.isSettler && !p.isWorker);
		MapUnit scout = gameData.SpawnUnit(ai, scoutType, from);
		Assert.Equal(0, GoodyHuts.Weigh(gameData, scout, hut, 7)[GoodyHuts.Outcome.Barbarians]);
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
		(Tile from, _, Tile hut) = FindHutSite();
		MapUnit warrior = gameData.SpawnUnit(ai, Prototype("Warrior"), from);
		List<City> aiCities = ai.cities.ToList();

		// With no cities while another civ has one, the AI is behind.
		ai.cities.Clear();
		if (!gameData.players.Any(p => p.cities.Count > 0)) {
			Player other = gameData.players.First(p => p != ai && !p.isBarbarians);
			CityInteractions.BuildCity(gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities() && t.DistanceTo(hut) > 3), other, "Elsewhere");
		}
		Assert.True(GoodyHuts.Weigh(gameData, warrior, hut, GoodyHuts.MiddleLevel)[GoodyHuts.Outcome.City] > 0);

		// With every other civ's cities gone, it's ahead of the average.
		ai.cities.AddRange(aiCities);
		if (ai.cities.Count == 0) {
			ai.cities.Add(CityInteractions.BuildCity(gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities() && t.DistanceTo(hut) > 3), ai, "Aheadville"));
		}
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
		(Tile from, _, Tile hut) = FindHutSite(t => t.IsAllowCities());
		hut.hasGoodyHut = true;
		MapUnit warrior = gameData.SpawnUnit(player, Prototype("Warrior"), from);
		// Behind on cities, while some other civ has one.
		player.cities.Clear();
		if (!gameData.players.Any(p => p.cities.Count > 0)) {
			Player other = gameData.players.First(p => p != player && !p.isBarbarians);
			CityInteractions.BuildCity(gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities() && t.DistanceTo(hut) > 3), other, "Elsewhere");
		}
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
