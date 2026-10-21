using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using QueryCiv3;
using Xunit;

namespace EngineTests.GameData;

// Civ3's tutorial: when its tips come up (C7GameData.Tutorial), and reading
// them from tutorial.txt (TutorialText).
public class TutorialTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private static readonly string[] Words = ["river", "lake", "hill", "mountain"];

	public TutorialTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		us = gameData.players.First(p => p.isHuman);
		gameData.tutorial.enabled = true;
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	// Civ3 tells the settler and the worker by their place in the rules.
	private UnitPrototype Settler => gameData.unitPrototypes[0];
	private UnitPrototype Worker => gameData.unitPrototypes[1];
	private UnitPrototype Warrior => gameData.unitPrototypes.First(p => p.name == "Warrior");

	private Terraform TerraformFor(string action) => gameData.Terraforms.First(t => t?.UIAction == action);

	private City OurCity() {
		return us.cities.FirstOrDefault()
			?? CityInteractions.BuildCity(us.units.First(u => u.unitType.isSettler).location, us, "Tutorville");
	}

	private TutorialTip Tip(MapUnit unit) => Tutorial.ForActiveUnit(gameData, unit, Words);

	private void MarkShown(params string[] keys) {
		foreach (string key in keys) {
			gameData.tutorial.shown.Add(key);
		}
	}

	[Fact]
	public void TheRulesStartWithTheSettlerAndTheWorker() {
		Assert.Equal("Settler", Settler.name);
		Assert.Equal("Worker", Worker.name);
	}

	[Fact]
	public void TheFirstSettlerGetsTheFirstMessageOnce() {
		MapUnit settler = us.units.First(u => u.unitType == Settler);
		Assert.Equal(new TutorialTip(Tutorial.FirstMessage).Key, Tip(settler).Key);
		Assert.Null(Tip(settler));
		Assert.Contains(Tutorial.FirstMessage, gameData.tutorial.shown);
	}

	[Fact]
	public void AWorkerIsIntroducedBeforeItsJobs() {
		City city = OurCity();
		Terraform irrigate = TerraformFor(C7Action.UnitIrrigate);
		MapUnit probe = gameData.SpawnUnit(us, Worker, city.location);
		Tile field = gameData.map.tiles.First(t => t.continent == city.location.continent && probe.CanPerformTerraformAction(irrigate, t));
		MapUnit worker = gameData.SpawnUnit(us, Worker, field);

		Assert.Equal(Tutorial.FirstMove, Tip(worker).Key);
		TutorialTip tip = Tip(worker);
		Assert.Equal(Tutorial.Irrigate, tip.Key);
		Assert.Equal(NearestCityName(worker), tip.Variables[0]);
		Assert.Contains(tip.Variables[1], new[] { "river", "lake" });
		// Each is shown once.
		Assert.NotEqual(Tutorial.Irrigate, Tip(worker)?.Key);
	}

	[Fact]
	public void AWorkerOnAHillIsToldItCouldMineIt() {
		City city = OurCity();
		MarkShown(Tutorial.FirstMove, Tutorial.Irrigate);
		Terraform mine = TerraformFor(C7Action.UnitBuildMine);
		TerrainType hills = gameData.terrainTypes.Single(tt => tt.Key == "hills");
		MapUnit probe = gameData.SpawnUnit(us, Worker, city.location);
		Tile hill = gameData.map.tiles.First(t => t.continent == city.location.continent && !t.HasCity()
			&& t.overlayTerrainType == hills && probe.CanPerformTerraformAction(mine, t));
		MapUnit worker = gameData.SpawnUnit(us, Worker, hill);

		TutorialTip tip = Tip(worker);
		Assert.Equal(Tutorial.Mine, tip.Key);
		Assert.Equal("hill", tip.Variables[0]);
		Assert.Equal(NearestCityName(worker), tip.Variables[1]);
	}

	[Fact]
	public void TheWorkerTipsNeedACityOnTheLandmass() {
		MarkShown(Tutorial.FirstMove);
		foreach (City c in us.cities.ToList()) {
			c.location.continent = -12345;
		}
		Terraform road = TerraformFor(C7Action.UnitBuildRoad);
		MapUnit probe = gameData.SpawnUnit(us, Worker, us.units.First().location);
		Tile tile = gameData.map.tiles.First(t => t.IsLand() && probe.CanPerformTerraformAction(road, t)
			&& t.Resource == Resource.NONE && t.neighbors.Values.All(n => n.Resource == null || n.Resource == Resource.NONE));
		MapUnit worker = gameData.SpawnUnit(us, Worker, tile);

		Assert.Null(Tip(worker));
	}

	[Fact]
	public void AStrategicResourceNamesAUnitThatNeedsIt() {
		City city = OurCity();
		MarkShown(Tutorial.FirstMove, Tutorial.Irrigate, Tutorial.Mine, Tutorial.Road);
		UnitPrototype needing = gameData.unitPrototypes.First(p => p.requiredResources.Count > 0);
		Resource resource = needing.requiredResources.First();
		Assert.Equal(ResourceCategory.STRATEGIC, resource.Category);
		if (resource.Prerequisite != null) {
			us.knownTechs.Add(resource.Prerequisite);
		}
		ClearResourcesAround(city.location);
		city.location.neighbors.Values.First(t => t.IsLand()).Resource = resource;
		MapUnit worker = gameData.SpawnUnit(us, Worker, city.location);

		TutorialTip tip = Tip(worker);
		Assert.Equal(Tutorial.StrategicResource, tip.Key);
		Assert.Equal(resource.Name, tip.Variables[0]);
		Assert.Equal(gameData.unitPrototypes.First(p => p.requiredResources.Contains(resource)).name, tip.Variables[1]);
	}

	[Fact]
	public void AResourceWhoseTechIsUnknownIsNotMentioned() {
		City city = OurCity();
		MarkShown(Tutorial.FirstMove, Tutorial.Irrigate, Tutorial.Mine, Tutorial.Road, Tutorial.StrategicLuxury, Tutorial.ConnectCities);
		Resource resource = gameData.unitPrototypes.SelectMany(p => p.requiredResources).First(r => r.Prerequisite != null);
		us.knownTechs.Remove(resource.Prerequisite);
		ClearResourcesAround(city.location);
		city.location.neighbors.Values.First(t => t.IsLand()).Resource = resource;
		MapUnit worker = gameData.SpawnUnit(us, Worker, city.location);
		Assert.Null(Tip(worker));

		us.knownTechs.Add(resource.Prerequisite);
		Assert.Equal(Tutorial.StrategicResource, Tip(worker).Key);
	}

	[Fact]
	public void ALuxuryNearTheWorkerNamesTheNearestCity() {
		City city = OurCity();
		MarkShown(Tutorial.FirstMove, Tutorial.Irrigate, Tutorial.Mine, Tutorial.Road, Tutorial.StrategicResource);
		Resource luxury = gameData.Resources.First(r => r.Category == ResourceCategory.LUXURY);
		if (luxury.Prerequisite != null) {
			us.knownTechs.Add(luxury.Prerequisite);
		}
		ClearResourcesAround(city.location);
		city.location.neighbors.Values.First(t => t.IsLand()).Resource = luxury;
		MapUnit worker = gameData.SpawnUnit(us, Worker, city.location);

		TutorialTip tip = Tip(worker);
		Assert.Equal(Tutorial.StrategicLuxury, tip.Key);
		Assert.Equal(luxury.Name, tip.Variables[0]);
		Assert.Equal(city.name, tip.Variables[1]);
	}

	private static void ClearResourcesAround(Tile center) {
		foreach (Tile t in center.neighbors.Values.Append(center)) {
			t.Resource = Resource.NONE;
		}
	}

	[Fact]
	public void ConnectCitiesComesWithASecondCity() {
		City first = OurCity();
		MarkShown(Tutorial.FirstMove, Tutorial.Irrigate, Tutorial.Mine, Tutorial.Road, Tutorial.StrategicResource, Tutorial.StrategicLuxury);
		MapUnit worker = gameData.SpawnUnit(us, Worker, first.location);
		if (us.cities.Count == 1) {
			Assert.Null(Tip(worker));
			Tile site = gameData.map.tiles.First(t => t.IsLand() && t.IsAllowCities() && !t.HasCity()
				&& t.RankDistanceTo(first.location) > 4 && t.GetTilesWithinRankDistance(1).All(n => !n.HasCity()));
			CityInteractions.BuildCity(site, us, "Second");
		}
		Assert.Equal(Tutorial.ConnectCities, Tip(worker).Key);
	}

	[Fact]
	public void TheFirstMilitaryUnitThenOneLargeCity() {
		City city = OurCity();
		foreach (City other in us.cities.Where(c => c != city).ToList()) {
			us.cities.Remove(other);
		}
		MapUnit warrior = gameData.SpawnUnit(us, Warrior, city.location);
		TutorialTip tip = Tip(warrior);
		Assert.Equal(Tutorial.FirstMilitaryUnit, tip.Key);
		Assert.Equal(city.name, tip.Variables[0]);

		// "Player has only one city, and it is size 4+".
		while (city.residents.Count > 3) {
			city.residents.RemoveAt(city.residents.Count - 1);
		}
		Assert.Null(Tip(warrior));
		while (city.residents.Count < 4) {
			city.residents.Add(new CityResident { city = city, citizenType = gameData.citizenTypes.First(), tileWorked = Tile.NONE });
		}
		tip = Tip(warrior);
		Assert.Equal(Tutorial.OneCity, tip.Key);
		Assert.Equal(city.name, tip.Variables[0]);
		Assert.Null(Tip(warrior));
	}

	[Fact]
	public void CivilDisorderIsExplainedOnce() {
		City city = OurCity();
		Assert.Null(Tutorial.ForCivilDisorder(gameData, us));
		city.isInCivilDisorder = true;
		TutorialTip tip = Tutorial.ForCivilDisorder(gameData, us);
		Assert.Equal(Tutorial.CivilDisorder, tip.Key);
		Assert.Equal(city.name, tip.Variables[0]);
		Assert.Null(Tutorial.ForCivilDisorder(gameData, us));
	}

	[Fact]
	public void TheCityScreenTourComesOnceInCiv3sOrder() {
		Assert.Equal(new[] { Tutorial.FirstCityManagement, Tutorial.ProductionButton, Tutorial.Shields, Tutorial.Citizens },
			Tutorial.ForCityScreen(gameData).Select(t => t.Key));
		Assert.Empty(Tutorial.ForCityScreen(gameData));
	}

	[Fact]
	public void TheTutorialIsSavedWithTheGame() {
		gameData.tutorial.shown.Add(Tutorial.FirstMove);
		C7GameData.GameData loaded = SaveGame.FromJSON(SaveGame.FromGameData(gameData).ToCompactJSON()).ToGameData(fixture.behaviors);
		Assert.True(loaded.tutorial.enabled);
		Assert.Contains(Tutorial.FirstMove, loaded.tutorial.shown);

		// A game that never had it on doesn't save it, and loads with it off.
		gameData.tutorial = new TutorialState();
		SaveGame save = SaveGame.FromGameData(gameData);
		Assert.Null(save.Tutorial);
		Assert.False(SaveGame.FromJSON(save.ToCompactJSON()).ToGameData(fixture.behaviors).tutorial.enabled);
	}

	[Fact]
	public void TheTextIsReadLikeCiv3sScriptText() {
		TutorialText text = TutorialText.Parse("""
			#COMMENT ____ Settler messages


			#TUT_A
			#ADVISOR Domestic sad
			#Text
			^{You have a city}. The city of $CITY0 is
			growing near the $RESOURCE1.
			^
			^Press [B] to {B}uild a $LINK<city=GCON_Cities>.

			#COMMENT  a note about $RIVER_OR_LAKE1

			#TUT_B
			#ADVISOR Domestic Happy
			#x 500
			This tip has no caret and no #Text.

			#Variables
			river
			lake
			""".Replace("\t", ""));

		Assert.Equal(new[] { "river", "lake" }, text.Variables);
		TutorialEntry a = text.Get("TUT_A");
		Assert.Equal("Domestic", a.Advisor);
		Assert.Equal("sad", a.Mood);
		Assert.Equal(3, a.Lines.Count);
		Assert.Equal("You have a city. The city of Rome is growing near the Iron.\n\nPress B to Build a city.",
			text.PlainTextOf(new TutorialTip("TUT_A", "Rome", "Iron")));
		Assert.Equal("This tip has no caret and no #Text.", text.PlainTextOf(new TutorialTip("TUT_B")));
		Assert.Null(text.PlainTextOf(new TutorialTip("TUT_MISSING")));
	}

	[SkippableFact]
	public void Civ3sTutorialTextHasEveryTip() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), Civ3TestData.SkipReason);
		// Civ3 Conquests has no tutorial.txt of its own; it reads PTW's.
		string path = new[] { "Conquests", "civ3PTW", "" }
			.Select(dir => Path.Combine(Civ3Location.GetCiv3Path(), dir, "Text", "tutorial.txt"))
			.First(File.Exists);
		TutorialText text = TutorialText.Load(path);

		Assert.Equal(Words, text.Variables);
		foreach (string key in new[] {
				Tutorial.FirstMessage, Tutorial.FirstMove, Tutorial.Irrigate, Tutorial.Mine, Tutorial.Road,
				Tutorial.StrategicResource, Tutorial.StrategicLuxury, Tutorial.ConnectCities, Tutorial.FirstMilitaryUnit,
				Tutorial.OneCity, Tutorial.CivilDisorder, Tutorial.FirstCityManagement, Tutorial.ProductionButton,
				Tutorial.Shields, Tutorial.Citizens }) {
			Assert.NotNull(text.Get(key));
		}
		string mine = text.PlainTextOf(new TutorialTip(Tutorial.Mine, "hill", "Rome"));
		Assert.StartsWith("This hill contains valuable production materials.", mine);
		Assert.Contains("Rome's production of shields", mine);
		Assert.DoesNotContain("$", text.PlainTextOf(new TutorialTip(Tutorial.StrategicLuxury, "Furs", "Rome")));
	}

	// With one city, the nearest is that one.
	private string NearestCityName(MapUnit unit) {
		return us.cities.Single(c => c.location.continent == unit.location.continent).name;
	}
}
