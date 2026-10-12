using C7GameData;
using Xunit;

namespace EngineTests.GameData;

// A scientific civ gets a free tech, one of the new era's first techs, as it
// enters each era (the Conquests Civilopedia, GCON_Strengths;
// https://civfanatics.com/civ3/faq/).
public class ScientificEraTechTest {
	private readonly C7GameData.GameData gameData = new();
	private readonly Tech bronze;
	private readonly Tech feudalism;
	private readonly Tech monotheism;
	private readonly Tech chivalry;
	private readonly Tech optionalAncient;
	private readonly Tech needsOptional;

	public ScientificEraTechTest() {
		C7Engine.EngineStorage.InitializeGameDataForTests(gameData);
		bronze = NewTech(1, "Bronze Working", EraUtils.ANCIENT_TIMES_CVLPD, required: true);
		optionalAncient = NewTech(2, "Optional", EraUtils.ANCIENT_TIMES_CVLPD, required: false);
		feudalism = NewTech(3, "Feudalism", EraUtils.MIDDLE_AGES_CVLPD, required: true, bronze);
		monotheism = NewTech(4, "Monotheism", EraUtils.MIDDLE_AGES_CVLPD, required: true, bronze);
		chivalry = NewTech(5, "Chivalry", EraUtils.MIDDLE_AGES_CVLPD, required: true, feudalism);
		needsOptional = NewTech(6, "Needs Optional", EraUtils.MIDDLE_AGES_CVLPD, required: false, optionalAncient);
	}

	private Tech NewTech(int id, string name, string era, bool required, params Tech[] prereqs) {
		Tech tech = new() {
			id = ID.FromString($"tech-{id}"),
			Name = name,
			Cost = 10,
			EraCivilopediaName = era,
			RequiredForEraAdvancement = required,
		};
		tech.Prerequisites.AddRange(prereqs);
		gameData.techs.Add(tech);
		return tech;
	}

	private Player NewPlayer(params Civilization.Trait[] traits) {
		Civilization civ = new("Babylon");
		foreach (Civilization.Trait t in traits) {
			civ.traits.Add(t);
		}
		Player p = new() {
			id = ID.FromString("player-1"),
			civilization = civ,
			government = new Government(),
			eraCivilopediaName = EraUtils.ANCIENT_TIMES_CVLPD,
		};
		gameData.players.Add(p);
		return p;
	}

	[Fact]
	public void ScientificCivGetsAFirstTechOfTheNewEra() {
		Player us = NewPlayer(Civilization.Trait.Scientific, Civilization.Trait.Religious);

		us.AcquireTech(gameData, bronze);

		Assert.Equal(EraUtils.MIDDLE_AGES_CVLPD, us.eraCivilopediaName);
		// One of the techs whose prerequisites were known as the era began:
		// never Chivalry, which needs Feudalism, nor a tech needing an
		// ancient tech we skipped.
		Assert.True(us.knownTechs.Contains(feudalism.id) ^ us.knownTechs.Contains(monotheism.id));
		Assert.DoesNotContain(chivalry.id, us.knownTechs);
		Assert.DoesNotContain(needsOptional.id, us.knownTechs);
		Assert.Equal(2, us.knownTechs.Count);
	}

	[Fact]
	public void OtherCivsGetNoFreeTech() {
		Player us = NewPlayer(Civilization.Trait.Militaristic, Civilization.Trait.Religious);

		us.AcquireTech(gameData, bronze);

		Assert.Equal(EraUtils.MIDDLE_AGES_CVLPD, us.eraCivilopediaName);
		Assert.Single(us.knownTechs);
	}

	[Fact]
	public void NoFreeTechWithinAnEra() {
		Player us = NewPlayer(Civilization.Trait.Scientific);
		us.eraCivilopediaName = EraUtils.MIDDLE_AGES_CVLPD;
		us.knownTechs.Add(bronze.id);

		us.AcquireTech(gameData, feudalism);

		Assert.Equal(2, us.knownTechs.Count);
	}
}
