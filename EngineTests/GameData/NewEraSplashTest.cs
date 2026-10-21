using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using Xunit;

namespace EngineTests.GameData;

// Civ3's era splash (FUN_0052d140) greets the human player entering a new
// era, though not on the game's first turn (FUN_0056a220).
public class NewEraSplashTest {
	private readonly C7GameData.GameData gameData = new();
	private readonly Tech bronze;
	private readonly Tech feudalism;
	private readonly Tech monotheism;
	private readonly Tech chivalry;

	// Picks the same index (wrapped to the range) every time.
	private class FixedRandom(int pick) : Random {
		public override int Next(int maxValue) => pick % maxValue;
	}

	public NewEraSplashTest() {
		// Engine state is static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.pendingMessages.Clear();
		EngineStorage.messagesToUI.Clear();
		EngineStorage.ResetNetworking();
		gameData.turn = 40;
		bronze = NewTech(1, "Bronze Working", EraUtils.ANCIENT_TIMES_CVLPD);
		feudalism = NewTech(2, "Feudalism", EraUtils.MIDDLE_AGES_CVLPD, bronze);
		monotheism = NewTech(3, "Monotheism", EraUtils.MIDDLE_AGES_CVLPD, bronze);
		chivalry = NewTech(4, "Chivalry", EraUtils.MIDDLE_AGES_CVLPD, feudalism);
	}

	private Tech NewTech(int id, string name, string era, params Tech[] prereqs) {
		Tech tech = new() {
			id = ID.FromString($"tech-{id}"),
			Name = name,
			Cost = 10,
			EraCivilopediaName = era,
			RequiredForEraAdvancement = true,
		};
		tech.Prerequisites.AddRange(prereqs);
		gameData.techs.Add(tech);
		return tech;
	}

	private Player NewPlayer(bool human, Gender gender = Gender.Male) {
		Civilization civ = new("Babylon") { leader = "Hammurabi", leaderGender = gender };
		Player p = new() {
			id = ID.FromString($"player-{gameData.players.Count + 1}"),
			civilization = civ,
			government = new Government {
				maleRulerTitles = ["King", "Emperor"],
				femaleRulerTitles = ["Queen", ""],
			},
			eraCivilopediaName = EraUtils.ANCIENT_TIMES_CVLPD,
			isHuman = human,
		};
		gameData.players.Add(p);
		return p;
	}

	private List<MsgEnteredNewEra> Splashes() {
		List<MsgEnteredNewEra> result = new();
		while (EngineStorage.TryDequeueNextMessageToUI(out MessageToUI msg)) {
			if (msg is MsgEnteredNewEra splash) {
				result.Add(splash);
			}
		}
		return result;
	}

	[Fact]
	public void AHumanEnteringANewEraIsShownTheSplash() {
		Player us = NewPlayer(human: true);

		us.AcquireTech(gameData, bronze);

		MsgEnteredNewEra splash = Assert.Single(Splashes());
		Assert.Equal(us, splash.recipient);
		Assert.Equal(EraUtils.ANCIENT_TIMES_CVLPD, splash.previousEraCivilopediaName);
		Assert.Equal(EraUtils.MIDDLE_AGES_CVLPD, splash.eraCivilopediaName);
	}

	[Fact]
	public void NoSplashForAComputerPlayer() {
		Player ai = NewPlayer(human: false);

		ai.AcquireTech(gameData, bronze);

		Assert.Equal(EraUtils.MIDDLE_AGES_CVLPD, ai.eraCivilopediaName);
		Assert.Empty(Splashes());
	}

	[Fact]
	public void NoSplashOnTheFirstTurn() {
		gameData.turn = 0;
		Player us = NewPlayer(human: true);

		us.AcquireTech(gameData, bronze);

		Assert.Equal(EraUtils.MIDDLE_AGES_CVLPD, us.eraCivilopediaName);
		Assert.Empty(Splashes());
	}

	[Fact]
	public void NoSplashWithinAnEra() {
		Player us = NewPlayer(human: true);
		us.eraCivilopediaName = EraUtils.MIDDLE_AGES_CVLPD;
		us.knownTechs.Add(bronze.id);

		us.AcquireTech(gameData, feudalism);

		Assert.Empty(Splashes());
	}

	[Fact]
	public void TheTextNamesTheRulerAndTheEraResearchers() {
		Player us = NewPlayer(human: true);
		us.eraCivilopediaName = EraUtils.MIDDLE_AGES_CVLPD;
		Rules rules = new();
		rules.EraResearcherNames[EraUtils.MIDDLE_AGES_CVLPD] = ["Monks", "Scholars"];

		// 1 picks the second title pair, the second line and the second name.
		string text = NewEra.Announcement(us, rules, new FixedRandom(1));

		Assert.Equal("The discoveries of our Scholars have led to a new era of prosperity for our people, Emperor!", text);
		Assert.Equal("King, our historians agree, we have entered a distinctly new age in our development!",
			NewEra.Announcement(us, rules, new FixedRandom(0)));
	}

	[Fact]
	public void AFemaleLeaderHasTheFemaleTitleUnlessItIsBlank() {
		Player us = NewPlayer(human: true, Gender.Female);

		Assert.Equal("Queen", NewEra.RulerTitle(us, new FixedRandom(0)));
		Assert.Equal("Emperor", NewEra.RulerTitle(us, new FixedRandom(1)));
	}

	[Fact]
	public void WithoutResearcherNamesTheTextIsTheOneThatNeedsNone() {
		Player us = NewPlayer(human: true);
		us.government.maleRulerTitles = null;

		string text = NewEra.Announcement(us, new Rules(), new FixedRandom(1));

		Assert.Equal("Hammurabi, our historians agree, we have entered a distinctly new age in our development!", text);
	}

	[Fact]
	public void TheBigPictureResearchIsATechOfTheEraThatCanBeResearched() {
		Player us = NewPlayer(human: true);
		us.eraCivilopediaName = EraUtils.MIDDLE_AGES_CVLPD;
		us.knownTechs.Add(bronze.id);

		HashSet<Tech> picks = Enumerable.Range(0, 4)
			.Select(i => NewEra.BigPictureResearch(us, gameData, new FixedRandom(i)))
			.ToHashSet();

		// Never Chivalry, which needs Feudalism first.
		Assert.Equal(new HashSet<Tech> { feudalism, monotheism }, picks);
	}
}
