using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using C7Engine;
using C7Engine.AI;
using C7Engine.AI.Advanced;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

// The Advanced AI's map profiles (see AdvancedProfiles): which profile a map
// plays by, how it layers with the defaults and trial overrides, and the
// map's key surviving save and load.
public sealed class AdvancedProfilesTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;

	public AdvancedProfilesTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		AdvancedTuning.Override("");
		AdvancedTuning.UseProfile(null, null);
	}

	public void Dispose() {
		AdvancedTuning.Override("");
		AdvancedTuning.UseProfile(null, null);
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private static readonly AdvancedProfiles Profiles = AdvancedProfiles.Parse("""
		{ "profiles": {
			"Pangaea":             { "FocusPoints": 20 },
			"Huge":                { "FocusPoints": 25 },
			"Pangaea/70":          { "FocusPoints": 30 },
			"huge/pangaea/70":     { "FocusPoints": 40 },
			"Huge/Archipelago/80": { "FocusPoints": 50 },
			"60/Continents":       { "FocusPoints": 60 }
		} }
		""");

	private static MapGeneration Map(string world, string landform, int water) => new() { worldSize = world, landform = landform, water = water };

	[Theory]
	// The most specific profile that fits wins, whatever the case and order of its key.
	[InlineData("Huge", "Pangaea", 70, "huge/pangaea/70")]
	[InlineData("Small", "Pangaea", 70, "Pangaea/70")]
	[InlineData("Small", "Pangaea", 60, "Pangaea")]
	[InlineData("Huge", "Continents", 70, "Huge")]
	[InlineData("Huge", "Archipelago", 80, "Huge/Archipelago/80")]
	[InlineData("Small", "Continents", 60, "60/Continents")]
	// Equally specific: the world size outranks the landform, which outranks the water.
	[InlineData("Huge", "Pangaea", 60, "Huge")]
	[InlineData("Huge", "Continents", 60, "60/Continents")]
	// Nothing fits: none.
	[InlineData("Small", "Archipelago", 80, null)]
	// A map with no world size name still fits by landform and water.
	[InlineData(null, "Pangaea", 70, "Pangaea/70")]
	public void TheMostSpecificFittingProfileWins(string world, string landform, int water, string expected) {
		(string key, Dictionary<string, double> knobs) = Profiles.Resolve(Map(world, landform, water));
		Assert.Equal(expected, key);
		if (expected != null) {
			Assert.Same(Profiles.profiles[expected], knobs);
		}
	}

	[Fact]
	public void AMapWithoutAKeyHasNoProfile() {
		Assert.Equal((null, null), Profiles.Resolve(null));
	}

	[Fact]
	public void AKeyPartTheMapDoesNotHaveDoesNotFit() {
		Assert.False(AdvancedProfiles.Fits("Pangaea/Huge", Map("Small", "Pangaea", 70), out _, out _));
		Assert.False(AdvancedProfiles.Fits("Pangaea/Pangaea", Map("Small", "Pangaea", 70), out _, out _));
		Assert.False(AdvancedProfiles.Fits("", Map("Small", "Pangaea", 70), out _, out _));
	}

	[Fact]
	public void TheProfileLayersOverTheDefaultsAndTrialOverridesBeatIt() {
		double settlers = AdvancedTuning.KnobNamed("ExpandSettlers").defaultValue;
		AdvancedTuning.UseProfile("Pangaea", new Dictionary<string, double> { ["FocusPoints"] = 20, ["BuildScience"] = 75 });
		Assert.Equal(20, AdvancedTuning.Get("FocusPoints"));
		Assert.Equal(75, AdvancedTuning.Get("BuildScience"));
		Assert.Equal(settlers, AdvancedTuning.Get("ExpandSettlers"));

		AdvancedTuning.Override("FocusPoints=33");
		Assert.Equal(33, AdvancedTuning.Get("FocusPoints"));
		Assert.Equal(75, AdvancedTuning.Get("BuildScience"));

		// A player's own policy still beats both.
		Player player = new() { advancedPolicy = new AdvancedPolicy() };
		player.advancedPolicy.knobs["FocusPoints"] = 5;
		Assert.Equal(5, AdvancedTuning.Get(player, "FocusPoints"));
		Assert.Equal(75, AdvancedTuning.Get(player, "BuildScience"));
	}

	[Fact]
	public void MissingOrEmptyProfilesAreTheDefaults() {
		AdvancedProfiles missing = AdvancedProfiles.Load(Path.Combine(Path.GetTempPath(), "no-such-dir-" + Guid.NewGuid(), AdvancedProfiles.FileName));
		Assert.Empty(missing.profiles);
		Assert.Empty(AdvancedProfiles.Parse("{}").profiles);
		Assert.Empty(AdvancedProfiles.Parse("""{ "profiles": {} }""").profiles);

		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		AdvancedTuning.StartGame(gameData, AdvancedProfiles.Parse("""{ "profiles": {} }"""));
		Assert.Null(AdvancedTuning.ProfileKey);
		foreach (AdvancedTuning.Knob k in AdvancedTuning.Knobs) {
			Assert.Equal(k.defaultValue, AdvancedTuning.Get(k.name));
		}
		AdvancedTuning.StartGame(gameData, null);
		Assert.Null(AdvancedTuning.ProfileKey);
	}

	[Fact]
	public void AnUnknownKnobIsAnError() {
		Assert.Throws<ArgumentException>(() => AdvancedProfiles.Parse("""{ "profiles": { "Pangaea": { "NoSuchKnob": 1 } } }"""));
	}

	// The file the game ships: readable, every knob a real one.
	[Fact]
	public void TheShippedProfilesFileIsValid() {
		string path = Path.Combine(PathUtils.GameModesDir, "civ3", AdvancedProfiles.FileName);
		Assert.True(File.Exists(path), path);
		AdvancedProfiles.Load(path);
		Assert.NotNull(fixture.behaviors.advancedProfiles);
	}

	[Fact]
	public void StartingAGamePicksItsMapsProfileAndItsBehaviours() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		// The test game is generated as a Pangaea with 70% water (see SaveGameFixture).
		Assert.Equal("Pangaea/70", gameData.map.generation.Key);
		AdvancedTuning.StartGame(gameData, AdvancedProfiles.Parse("""
			{ "profiles": { "Pangaea": { "fast-expand": 1 }, "Archipelago": { "FocusPoints": 0 } } }
			"""));
		Assert.Equal("Pangaea", AdvancedTuning.ProfileKey);
		Player player = gameData.players.First(p => !p.isHuman && !p.IsIndependent);
		player.aiType = AiType.Advanced;
		Assert.Contains(FastExpand.Switch, AdvancedTuning.BehavioursOn(player));
		Assert.Equal(AdvancedTuning.KnobNamed("FocusPoints").defaultValue, AdvancedTuning.Get(player, "FocusPoints"));

		// A trial override turning it off beats the profile.
		AdvancedTuning.Override(FastExpand.Switch + "=0");
		Assert.DoesNotContain(FastExpand.Switch, AdvancedTuning.BehavioursOn(player));
	}

	[Fact]
	public void TheMapKeySurvivesSaveAndLoad() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		gameData.map.generation = Map("Huge", "Archipelago", 80);
		C7GameData.GameData loaded = SaveGame.FromJSON(SaveGame.FromGameData(gameData).ToCompactJSON()).ToGameData(fixture.behaviors);
		Assert.Equal("Huge/Archipelago/80", loaded.map.generation.Key);
		AdvancedTuning.StartGame(loaded, Profiles);
		Assert.Equal("Huge/Archipelago/80", AdvancedTuning.ProfileKey);
		Assert.Equal(50, AdvancedTuning.Get("FocusPoints"));
	}

	// A save from before maps kept what they were generated as, or an
	// imported Civ3 game: no key, so no profile.
	[Fact]
	public void ASaveWithoutTheKeyLoadsWithNoProfile() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		JsonNode json = JsonNode.Parse(SaveGame.FromGameData(gameData).ToCompactJSON());
		Assert.NotNull(json["map"]["generation"]);
		json["map"].AsObject().Remove("generation");
		C7GameData.GameData loaded = SaveGame.FromJSON(System.Text.Encoding.UTF8.GetBytes(json.ToJsonString())).ToGameData(fixture.behaviors);
		Assert.Null(loaded.map.generation);
		AdvancedTuning.StartGame(loaded, Profiles);
		Assert.Null(AdvancedTuning.ProfileKey);
		Assert.Equal(AdvancedTuning.KnobNamed("FocusPoints").defaultValue, AdvancedTuning.Get("FocusPoints"));
	}

	// A map generated for a named world size keeps the name.
	[Fact]
	public void AGeneratedMapKeepsItsWorldSizeLandformAndWater() {
		SaveGame save = fixture.saveGame;
		WorldSize tiny = save.WorldSizes.First(w => w.name == "Tiny");
		GameMap map = MapGenerator.GenerateMap(new WorldCharacteristics(save) {
			landform = WorldCharacteristics.Landform.Archipelago,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_80,
			age = WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			worldSize = tiny.Clone(),
			mapSeed = 99,
		});
		Assert.Equal("Tiny/Archipelago/80", map.generation.Key);
	}
}
