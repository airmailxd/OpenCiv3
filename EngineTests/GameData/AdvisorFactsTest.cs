using System.IO;
using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using QueryCiv3;
using Xunit;

namespace EngineTests.GameData;

// What the advisors' speech is chosen from, worked out from a game (see
// AdvisorSpeechTest for the choosing).
public class AdvisorFactsTest : IClassFixture<SaveGameFixture> {
	private readonly C7GameData.GameData gameData;
	private readonly Player player;
	private readonly City city;

	public AdvisorFactsTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		player = gameData.players.First(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler));
		Tile tile = player.units.First(u => u.unitType.isSettler).location;
		city = CityInteractions.BuildCity(tile, player, player.GetNextCityName());
	}

	[Fact]
	public void EveryAdvisorHasSomethingToSay() {
		AdvisorSpeech speech = new();
		foreach (AdvisorKind advisor in System.Enum.GetValues<AdvisorKind>()) {
			AdvisorFacts facts = AdvisorFactsGatherer.Gather(gameData, player, advisor);
			Assert.NotNull(speech.Next(advisor, facts, new System.Random(1)));
		}
	}

	[Fact]
	public void TheDomesticAdvisorKnowsOurCitiesAndCallsUsByOurTitle() {
		AdvisorFacts facts = AdvisorFactsGatherer.Gather(gameData, player, AdvisorKind.Domestic);
		CityAdviceFacts ours = Assert.Single(facts.Cities);
		Assert.Equal(city.name, ours.Name);
		Assert.Equal(city.FoodGrowthPerTurn(), ours.FoodSurplus);
		Assert.NotEmpty(facts.RulerTitles);
		Assert.Equal(0, facts.AnarchyTurns);
	}

	[Fact]
	public void ARulerIsCalledByTheTitlesOfTheirGender() {
		player.government.maleRulerTitles = ["Chief", "Warlord"];
		player.government.femaleRulerTitles = ["Chieftess", ""];
		player.civilization.leaderGender = Gender.Female;
		Assert.Equal(["Chieftess", "Warlord"], AdvisorFactsGatherer.RulerTitles(player));
		player.civilization.leaderGender = Gender.Male;
		Assert.Equal(["Chief", "Warlord"], AdvisorFactsGatherer.RulerTitles(player));
		// Without titles, the government's name.
		player.government.maleRulerTitles = null;
		Assert.Equal([player.government.name], AdvisorFactsGatherer.RulerTitles(player));
	}

	[SkippableFact]
	public void RulerAndLeaderTitlesAreImportedFromTheBiq() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");
		string conquests = Path.Join(Civ3Location.GetCiv3Path(), "Conquests");
		SaveGame game = ImportCiv3.ImportBiq(Path.Join(conquests, "Scenarios", "No Civ Traits.biq"), PathUtils.defaultBicPath,
			_ => Path.GetFullPath(Path.Combine(conquests, "Text", "PediaIcons.txt")));

		Government despotism = game.Governments.First(g => g.name == "Despotism");
		Assert.NotEmpty(despotism.maleRulerTitles);
		Assert.Equal(despotism.maleRulerTitles.Count, despotism.femaleRulerTitles.Count);
		Assert.All(despotism.maleRulerTitles, t => Assert.False(string.IsNullOrEmpty(t)));
		Assert.Contains(game.Civilizations, c => !string.IsNullOrEmpty(c.leaderTitle));
	}
}
