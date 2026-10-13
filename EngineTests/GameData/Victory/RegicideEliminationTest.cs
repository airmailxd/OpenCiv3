using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData.Victory;

// Regicide, Mass Regicide and Elimination (see InstantElimination), per the
// Conquests Civilopedia's GCON_Short_Game_Modes.
public class RegicideEliminationTest : IClassFixture<SaveGameFixture>, System.IDisposable {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;
	private readonly Player attacker;
	private readonly Player defender;

	public RegicideEliminationTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler)).ToArray();
		attacker = civs[0];
		defender = civs[1];
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private City FoundCity(Player player, int size, Tile tile = null) {
		tile ??= player.units.First(u => u.unitType.isSettler).location;
		City city = CityInteractions.BuildCity(tile, player, player.GetNextCityName());
		while (city.residents.Count < size) {
			city.AddCitizen(new CityResident() {
				city = city,
				citizenType = city.residents[0].citizenType,
				tileWorked = city.residents[0].tileWorked,
			});
		}
		return city;
	}

	// A second city for the player, a few tiles from the first.
	private City FoundSecondCity(Player player, City first, int size) {
		Tile site = gameData.map.tiles.First(t => t.IsLand() && !t.HasCity() && t.unitsOnTile.Count == 0
			&& t.DistanceTo(first.location) >= 3 && t.DistanceTo(first.location) <= 6);
		return FoundCity(player, size, site);
	}

	private MapUnit SpawnKing(Player player, Tile tile) {
		UnitPrototype king = gameData.unitPrototypes.First(p => p.isKing);
		return gameData.SpawnUnit(player, king, tile);
	}

	[Fact]
	public void TheCiv3RulesetGivesEachCivItsOwnKing() {
		UnitPrototype lincoln = gameData.unitPrototypes.Single(p => p.name == "Lincoln");
		Assert.True(lincoln.isKing);
		Assert.Equal(1, lincoln.attack);
		Assert.Equal(1, lincoln.defense);
		Assert.Equal(2, lincoln.movement);
		Assert.Equal("Lincoln", gameData.civilizations.Single(c => c.name == "America").kingUnit);
		Assert.False(gameData.unitPrototypes.Single(p => p.name == "Warrior").isKing);
	}

	[Fact]
	public void UnderRegicideLosingTheKingDestroysTheCivAndItsCities() {
		gameData.victoryConditions = new VictoryConditions { Regicide = true };
		City capital = FoundCity(defender, 3);
		City other = FoundSecondCity(defender, capital, 2);
		MapUnit king = SpawnKing(defender, capital.location);
		Tile otherTile = other.location;

		gameData.RemoveUnit(king);

		Assert.True(defender.defeated);
		Assert.Empty(defender.cities);
		Assert.Empty(defender.units);
		Assert.Null(otherTile.cityAtTile);
		Assert.DoesNotContain(other, gameData.cities);
		Assert.False(attacker.defeated);
	}

	[Fact]
	public void WithoutRegicideLosingTheKingDoesNothing() {
		gameData.victoryConditions = new VictoryConditions();
		City capital = FoundCity(defender, 3);
		MapUnit king = SpawnKing(defender, capital.location);

		gameData.RemoveUnit(king);

		Assert.False(defender.defeated);
		Assert.Contains(capital, defender.cities);
	}

	[Fact]
	public void UnderMassRegicideEveryKingMustDie() {
		gameData.victoryConditions = new VictoryConditions { MassRegicide = true };
		City capital = FoundCity(defender, 3);
		MapUnit first = SpawnKing(defender, capital.location);
		MapUnit second = SpawnKing(defender, capital.location);

		gameData.RemoveUnit(first);
		Assert.False(defender.defeated);
		Assert.Equal(1, defender.kingsLost);

		gameData.RemoveUnit(second);
		Assert.True(defender.defeated);
		Assert.Empty(defender.cities);
	}

	[Fact]
	public void UnderEliminationLosingACityEliminatesTheCiv() {
		gameData.victoryConditions = new VictoryConditions { CityElimination = true, CityEliminationCount = 1 };
		FoundCity(attacker, 1);
		City capital = FoundCity(defender, 3);
		City other = FoundSecondCity(defender, capital, 3);

		CityInteractions.CaptureCity(other, attacker);

		Assert.True(defender.defeated);
		Assert.Empty(defender.cities);
		Assert.Null(capital.location.cityAtTile);
		// The city taken is the captor's.
		Assert.Equal(attacker, other.owner);
		Assert.False(attacker.defeated);
	}

	[Fact]
	public void UnderEliminationACivMayLoseFewerCitiesThanTheCount() {
		gameData.victoryConditions = new VictoryConditions { CityElimination = true, CityEliminationCount = 2 };
		FoundCity(attacker, 1);
		City capital = FoundCity(defender, 3);
		City second = FoundSecondCity(defender, capital, 3);
		City third = FoundSecondCity(defender, second, 3);

		CityInteractions.CaptureCity(second, attacker);
		Assert.False(defender.defeated);
		Assert.Equal(1, defender.citiesLost);

		CityInteractions.CaptureCity(third, attacker);
		Assert.True(defender.defeated);
		Assert.Empty(defender.cities);
	}

	[Fact]
	public void ACityDestroyedInTheTakingIsLostToo() {
		gameData.victoryConditions = new VictoryConditions { CityElimination = true, CityEliminationCount = 1 };
		FoundCity(attacker, 1);
		City capital = FoundCity(defender, 3);
		City small = FoundSecondCity(defender, capital, 1);

		CityInteractions.CaptureCity(small, attacker);

		Assert.True(defender.defeated);
	}

	[Fact]
	public void WithoutEliminationLosingACityDoesNotEliminate() {
		gameData.victoryConditions = new VictoryConditions();
		FoundCity(attacker, 1);
		City capital = FoundCity(defender, 3);
		City other = FoundSecondCity(defender, capital, 3);

		CityInteractions.CaptureCity(other, attacker);

		Assert.False(defender.defeated);
		Assert.Contains(capital, defender.cities);
	}

	[Fact]
	public void BarbariansAreNeverEliminated() {
		Player barbarians = gameData.players.First(p => p.isBarbarians);
		barbarians.citiesLost = 5;
		barbarians.kingsLost = 5;
		VictoryConditions all = new() { Regicide = true, MassRegicide = true, CityElimination = true };
		Assert.False(InstantElimination.IsEliminated(barbarians, all));
	}

	[Fact]
	public void KingsToPlaceFollowsTheRules() {
		Assert.Equal(0, InstantElimination.KingsToPlace(new VictoryConditions()));
		Assert.Equal(1, InstantElimination.KingsToPlace(new VictoryConditions { Regicide = true }));
		Assert.Equal(7, InstantElimination.KingsToPlace(new VictoryConditions { MassRegicide = true }));
		Assert.Equal(0, InstantElimination.KingsToPlace(new VictoryConditions { Regicide = true, AutoPlaceKings = false }));
	}

	[Fact]
	public void TheStatusScreenShowsKingsLeftAndCitiesLost() {
		City capital = FoundCity(defender, 3);
		SpawnKing(defender, capital.location);
		defender.citiesLost = 1;

		RegicideCondition regicide = new(all: false);
		VictoryStatus kings = regicide.Evaluate(defender, gameData);
		Assert.False(regicide.HasVictory(kings));
		Assert.Equal(["Lose kings: 1", "", "Kings left:", "1", "", ""], regicide.GenerateStatusRows(kings, []).Single());
		Assert.Equal("Lose kings: All", new RegicideCondition(all: true).GenerateStatusRows(kings, []).Single()[0]);

		EliminationCondition elimination = new(3);
		VictoryStatus cities = elimination.Evaluate(defender, gameData);
		Assert.False(elimination.HasVictory(cities));
		Assert.Equal(["Lose cities:", "3", "Cities lost:", "1", "", ""], elimination.GenerateStatusRows(cities, []).Single());
	}

	[Fact]
	public void TheConditionsShowOnlyWhenOn() {
		SaveGame save = fixture.saveGame;
		save.VictoryConditions = new VictoryConditions { MassRegicide = true, CityElimination = true, CityEliminationCount = 2 };
		C7GameData.GameData withThem = save.ToGameData(fixture.behaviors);
		Assert.Single(withThem.victories.OfType<RegicideCondition>());
		Assert.Single(withThem.victories.OfType<EliminationCondition>());

		save = fixture.saveGame;
		save.VictoryConditions = new VictoryConditions();
		C7GameData.GameData without = save.ToGameData(fixture.behaviors);
		Assert.Empty(without.victories.OfType<RegicideCondition>());
		Assert.Empty(without.victories.OfType<EliminationCondition>());
	}

	[Fact]
	public void TheSetupChoicesAreRemembered() {
		NewGameChoices choices = new();
		choices.SetVictoryConditions(new VictoryConditions { CityElimination = true, MassRegicide = true });
		Dictionary<string, string> values = [];
		choices.Write((key, value) => values[key] = value);

		NewGameChoices read = NewGameChoices.Read(key => values.GetValueOrDefault(key));
		VictoryConditions chosen = read.ChosenVictoryConditions(new VictoryConditions { CityEliminationCount = 3 });

		Assert.True(chosen.CityElimination);
		Assert.True(chosen.MassRegicide);
		Assert.False(chosen.Regicide);
		// The count is the ruleset's.
		Assert.Equal(3, chosen.CityEliminationCount);
		// Never chosen, they are off.
		Assert.False(new NewGameChoices().ChosenVictoryConditions(null).Regicide);
	}

	[Fact]
	public void KingsAndCitiesLostAreSaved() {
		defender.kingsLost = 2;
		defender.citiesLost = 3;
		gameData.victoryConditions = new VictoryConditions { Regicide = true, CityEliminationCount = 4, AutoPlaceKings = false };

		SaveGame loaded = SaveGame.FromJSON(SaveGame.FromGameData(gameData).ToCompactJSON());
		SavePlayer saved = loaded.Players.Single(p => p.id == defender.id);

		Assert.Equal(2, saved.kingsLost);
		Assert.Equal(3, saved.citiesLost);
		Assert.True(loaded.VictoryConditions.Regicide);
		Assert.Equal(4, loaded.VictoryConditions.CityEliminationCount);
		Assert.False(loaded.VictoryConditions.AutoPlaceKings);
	}

	[Fact]
	public void AScenarioCivWithoutAKingGetsOneInItsCapital() {
		SaveGame save = fixture.saveGame;
		save.VictoryConditions = new VictoryConditions { Regicide = true };
		List<SavePlayer> civs = save.Players.Where(p => !p.isBarbarian && p.isIncludedInGame
			&& !save.Civilizations.Single(c => c.name == p.civilization).isBarbarian).ToList();
		Assert.NotEmpty(civs);
		bool IsKing(SaveUnit u) => save.UnitPrototypes.Single(p => p.name == u.prototype).flags.Contains(SaveUnitPrototype.Flag.King);

		GameSetup.PlaceScenarioKings(save);
		GameSetup.PlaceScenarioKings(save);

		foreach (SavePlayer civ in civs) {
			List<SaveUnit> kings = save.Units.Where(u => u.owner == civ.id && IsKing(u)).ToList();
			Assert.Single(kings);
			string expected = save.Civilizations.Single(c => c.name == civ.civilization).kingUnit;
			Assert.Equal(expected, kings[0].prototype);
		}
	}

	[Fact]
	public void ANewGameUnderMassRegicideStartsEachCivWithSevenKings() {
		SaveGame save = SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")).GetSave();
		WorldCharacteristics world = new(save) {
			landform = WorldCharacteristics.Landform.Pangaea,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_60,
			age = WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			barbarianActivity = BarbarianActivity.None,
			worldSize = new WorldSize() {
				width = 40,
				height = 40,
				numberOfCivs = 2,
				distanceBetweenCivs = 6,
				techRate = 240,
				optimalNumberOfCities = 10,
			},
			mapSeed = 77,
		};
		new GameSetup {
			playerCivilization = save.Civilizations.Single(c => c.name == "America"),
			difficulty = save.Difficulties.First(),
			worldCharacteristics = world,
			opponents = [new SelectedOpponent { Name = "Rome" }],
			victoryConditions = new VictoryConditions { MassRegicide = true },
		}.Populate(save);

		SavePlayer america = save.Players.Single(p => p.civilization == "America");
		SavePlayer rome = save.Players.Single(p => p.civilization == "Rome");
		List<SaveUnit> americanKings = save.Units.Where(u => u.owner == america.id && u.prototype == "Lincoln").ToList();
		Assert.Equal(InstantElimination.MassRegicideKings, americanKings.Count);
		Assert.Equal(InstantElimination.MassRegicideKings, save.Units.Count(u => u.owner == rome.id && u.prototype == "Caesar"));
		// With the rest of its starting units.
		SaveUnit settler = save.Units.First(u => u.owner == america.id && u.prototype == save.Rules.StartUnitType1);
		Assert.All(americanKings, k => Assert.Equal(settler.currentLocation, k.currentLocation));
	}
}
