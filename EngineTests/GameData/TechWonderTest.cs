using System.Collections.Generic;
using C7GameData;
using C7GameData.Save;
using Xunit;

namespace EngineTests.GameData;

// The Great Library and Theory of Evolution.
public class TechWonderTest {
	private const string ANCIENT = "ERAS_Ancient_Times";

	private readonly C7GameData.GameData gameData = new();
	private readonly Tech alphabet;
	private readonly Tech writing;
	private readonly Tech pottery;
	private readonly Tech obsoleting;
	private readonly Player us;
	private readonly Player rome;
	private readonly Player greece;
	private readonly Player persia;

	public TechWonderTest() {
		C7Engine.EngineStorage.InitializeGameDataForTests(gameData);

		alphabet = NewTech(1, "Alphabet", 5);
		writing = NewTech(2, "Writing", 10, alphabet);
		pottery = NewTech(3, "Pottery", 20);
		obsoleting = NewTech(4, "Obsoleting", 50);

		us = NewPlayer(1, "Egypt");
		rome = NewPlayer(2, "Rome");
		greece = NewPlayer(3, "Greece");
		persia = NewPlayer(4, "Persia");
	}

	private Tech NewTech(int id, string name, int cost, params Tech[] prereqs) {
		Tech tech = new() { id = ID.FromString($"tech-{id}"), Name = name, Cost = cost, EraCivilopediaName = ANCIENT };
		tech.Prerequisites.AddRange(prereqs);
		gameData.techs.Add(tech);
		return tech;
	}

	private Player NewPlayer(int id, string civ) {
		Player p = new() {
			id = ID.FromString($"player-{id}"),
			civilization = new Civilization(civ),
			government = new Government(),
			eraCivilopediaName = ANCIENT,
		};
		gameData.players.Add(p);
		return p;
	}

	private static void Meet(Player a, Player b) {
		a.playerRelationships[b.id] = new PlayerRelationship();
		b.playerRelationships[a.id] = new PlayerRelationship();
	}

	private static void Know(Player p, params Tech[] techs) {
		foreach (Tech t in techs) {
			p.knownTechs.Add(t.id);
		}
	}

	private Building BuildWonder(SaveBuilding.Flag flag) {
		SaveBuilding sb = new() { name = "Wonder", greatWonderProperties = new SaveBuilding.GreatWonderProperties() };
		sb.flags.Add(flag);
		Building building = new(sb, new C7GameData.GameData());
		City city = new(Tile.NONE, us, "Thebes", ID.None("city"));
		us.cities.Add(city);
		city.AddBuilding(building);
		return building;
	}

	[Fact]
	public void GreatLibraryGivesTechsKnownByTwoContactedCivs() {
		BuildWonder(SaveBuilding.Flag.GainAnyTechKnownByTwoCivs);
		Meet(us, rome);
		Meet(us, greece);
		Know(rome, alphabet, writing, pottery);
		Know(greece, alphabet, writing);

		List<Tech> learned = us.DoGreatLibraryUpdates(gameData);

		// Writing needs Alphabet, which the library gives first.
		Assert.Equal(new List<Tech> { alphabet, writing }, learned);
		Assert.Contains(alphabet.id, us.knownTechs);
		Assert.Contains(writing.id, us.knownTechs);
		// Only Rome knows Pottery.
		Assert.DoesNotContain(pottery.id, us.knownTechs);
	}

	[Fact]
	public void GreatLibraryIgnoresCivsWeHaveNotMet() {
		BuildWonder(SaveBuilding.Flag.GainAnyTechKnownByTwoCivs);
		Meet(us, rome);
		Know(rome, alphabet);
		Know(greece, alphabet);
		Know(persia, alphabet);

		Assert.Empty(us.DoGreatLibraryUpdates(gameData));
		Assert.DoesNotContain(alphabet.id, us.knownTechs);

		Meet(us, persia);
		Assert.Equal(new List<Tech> { alphabet }, us.DoGreatLibraryUpdates(gameData));
	}

	[Fact]
	public void GreatLibraryIgnoresBarbariansAndDefeatedCivs() {
		BuildWonder(SaveBuilding.Flag.GainAnyTechKnownByTwoCivs);
		Meet(us, rome);
		Meet(us, greece);
		Know(rome, alphabet);
		Know(greece, alphabet);
		greece.defeated = true;

		Assert.Empty(us.DoGreatLibraryUpdates(gameData));
	}

	[Fact]
	public void GreatLibraryStopsWhenObsolete() {
		Building library = BuildWonder(SaveBuilding.Flag.GainAnyTechKnownByTwoCivs);
		library.renderedObsoleteBy = obsoleting;
		Know(us, obsoleting);
		Meet(us, rome);
		Meet(us, greece);
		Know(rome, alphabet);
		Know(greece, alphabet);

		Assert.Empty(us.DoGreatLibraryUpdates(gameData));
	}

	[Fact]
	public void NoGreatLibraryNoTechs() {
		Meet(us, rome);
		Meet(us, greece);
		Know(rome, alphabet);
		Know(greece, alphabet);

		Assert.Empty(us.DoGreatLibraryUpdates(gameData));
	}

	[Fact]
	public void GreatLibraryTechBeingResearchedMovesResearchOn() {
		BuildWonder(SaveBuilding.Flag.GainAnyTechKnownByTwoCivs);
		Meet(us, rome);
		Meet(us, greece);
		Know(rome, alphabet);
		Know(greece, alphabet);
		us.SetCurrentlyResearchedTech(alphabet.id);

		us.DoGreatLibraryUpdates(gameData);

		Assert.Contains(alphabet.id, us.knownTechs);
		Assert.Equal(writing.id, us.currentlyResearchedTech);
	}

	[Fact]
	public void TheoryOfEvolutionCompletesCurrentResearchAndTheNext() {
		BuildWonder(SaveBuilding.Flag.TwoFreeAdvances);
		us.SetCurrentlyResearchedTech(alphabet.id);

		List<Tech> learned = us.GrantFreeTechs(gameData, 2, "Theory of Evolution", us.cities[0]);

		// Alphabet was being researched; Writing is the next (cheapest) pick.
		Assert.Equal(2, learned.Count);
		Assert.Contains(alphabet.id, us.knownTechs);
		Assert.Contains(writing.id, us.knownTechs);
		Assert.Equal(0, us.freeTechsRemaining);
		Assert.Equal(pottery.id, us.currentlyResearchedTech);
	}

	[Fact]
	public void TheoryOfEvolutionFollowsTheResearchQueue() {
		us.AddTechItemToResearchQueue(pottery);
		us.AddTechItemToResearchQueue(alphabet);
		us.SetCurrentlyResearchedTech(pottery.id);

		us.GrantFreeTechs(gameData, 2, "Theory of Evolution", null);

		Assert.Contains(pottery.id, us.knownTechs);
		Assert.Contains(alphabet.id, us.knownTechs);
		Assert.DoesNotContain(writing.id, us.knownTechs);
	}

	[Fact]
	public void TheoryOfEvolutionKeepsTheFreeTechsUntilResearchIsChosen() {
		List<Tech> learned = us.GrantFreeTechs(gameData, 2, "Theory of Evolution", null);
		Assert.Empty(learned);
		Assert.Equal(2, us.freeTechsRemaining);

		us.SetCurrentlyResearchedTech(pottery.id);
		Assert.Contains(pottery.id, us.knownTechs);
		Assert.Equal(0, us.freeTechsRemaining);
	}

	[Fact]
	public void GreatLibraryLetsAHumanChooseWhatToResearchNext() {
		BuildWonder(SaveBuilding.Flag.GainAnyTechKnownByTwoCivs);
		us.isHuman = true;
		us.freeTechsRemaining = 1;
		Meet(us, rome);
		Meet(us, greece);
		Know(rome, alphabet);
		Know(greece, alphabet);

		Assert.Equal(new List<Tech> { alphabet }, us.DoGreatLibraryUpdates(gameData));

		// Nothing is picked for the player, so the free tech is still theirs
		// to spend.
		Assert.Null(us.currentlyResearchedTech);
		Assert.Equal(1, us.freeTechsRemaining);
		Assert.DoesNotContain(writing.id, us.knownTechs);
		Assert.Equal(alphabet, us.lastDiscoveredTech);
	}

	[Fact]
	public void GreatLibraryFollowsAHumansResearchQueue() {
		BuildWonder(SaveBuilding.Flag.GainAnyTechKnownByTwoCivs);
		us.isHuman = true;
		Meet(us, rome);
		Meet(us, greece);
		Know(rome, alphabet);
		Know(greece, alphabet);
		us.AddTechItemToResearchQueue(pottery);

		us.DoGreatLibraryUpdates(gameData);

		Assert.Equal(pottery.id, us.currentlyResearchedTech);
	}
}
