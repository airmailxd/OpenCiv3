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
		Dictionary<GoodyHuts.Outcome, double> weights = GoodyHuts.Weigh(gameData, unit, hut, GoodyHuts.Hardness(gameData, unit.owner));
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
