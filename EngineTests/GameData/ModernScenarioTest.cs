using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace EngineTests.GameData;

// Modern (the League's Modern_2.4.biq; see GameSetup.BundledRules.cs and
// GameSetup.Modern.cs): its governments, their buildings and wonders, its
// units, and the AI playing it.
public class ModernScenarioTest : System.IDisposable {
	private readonly ITestOutputHelper output;

	public ModernScenarioTest(ITestOutputHelper output) {
		this.output = output;
		EngineStorage.messagesToUI.Clear();
		EngineStorage.pendingMessages.Clear();
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private static GameMode.Config Mode => new("civ3", ["standalone"]);

	// Modern's rules as a new game gets them.
	private static SaveGame Rules() => GameSetup.LoadRulesFor(GameScenario.Modern, PathUtils.GameModesDir, Mode);

	private static SaveGame NewSave(int seed, int civs, string civilization = null) {
		SaveGame save = Rules();
		WorldCharacteristics wc = new(save) {
			landform = WorldCharacteristics.Landform.Pangaea,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_70,
			age = WorldCharacteristics.Age.Billion_5,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			barbarianActivity = BarbarianActivity.Roaming,
			worldSize = new WorldSize() { width = 70, height = 70, numberOfCivs = civs, distanceBetweenCivs = 10, techRate = 240, optimalNumberOfCities = 16 },
			mapSeed = seed,
		};
		new GameSetup {
			playerCivilization = save.Civilizations.First(c => civilization == null ? !c.isBarbarian : c.name == civilization),
			difficulty = save.Difficulties[2],
			worldCharacteristics = wc,
			opponents = Enumerable.Repeat(new SelectedOpponent { isRandom = true }, civs - 1).ToList(),
			victoryConditions = save.VictoryConditions,
			scenario = GameScenario.Modern,
		}.Populate(save);
		return save;
	}

	private static C7GameData.GameData NewGame(string civilization, int seed = 11) {
		C7GameData.GameData gameData = SaveGameFixture.HydrateSaveGame(NewSave(seed, 4, civilization));
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		return gameData;
	}

	private static Player Civ(C7GameData.GameData gameData, string name) => gameData.players.Single(p => p.civilization.name == name);

	private static City Found(C7GameData.GameData gameData, Player civ, string name) {
		Tile start = civ.units.First(u => u.unitType.actions.Contains(UnitAction.BuildCity)).location;
		Tile tile = civ.cities.Count == 0 ? start : gameData.map.tiles.First(t => t.IsLand() && t.IsAllowCities() && !t.HasCity()
			&& t.unitsOnTile.Count == 0 && t.DistanceTo(start) is >= 4 and <= 8 && t.continent == start.continent);
		return CityInteractions.BuildCity(tile, civ, name);
	}

	private static void Adopt(C7GameData.GameData gameData, Player civ, string government) {
		civ.AdoptGovernment(gameData.governments.Single(g => g.name == government));
	}

	// The BIQ's playable civilizations (its GAME section): those with a Civ
	// Tech but Vietnam. The others (Rome, the Spectators, ...) are left over
	// in the BIQ, and the import leaves them out.
	private static readonly string[] ModernCivilizations = [
		"Egypt", "European Union", "USSR", "China", "America", "Japan", "India", "Iran", "Mexico", "Nigeria",
		"Canada", "Brazil", "Australia", "Saudi Arabia", "Indonesia", "Israel", "Pakistan",
	];

	// Only the BIQ's playable civilizations, each with its own Civ Tech, in
	// the bundled rules themselves; no unit names another.
	[Fact]
	public void OnlyTheModsCivilizationsPlay() {
		SaveGame save = Rules();
		SaveGame bundled = GameSetup.LoadBundledRules(PathUtils.GameModesDir, Mode, GameSetup.ModernRules);
		Assert.Equal(ModernCivilizations.Order(), save.Civilizations.Where(c => !c.isBarbarian).Select(c => c.name).Order());
		Assert.Equal(save.Civilizations.Select(c => c.name), bundled.Civilizations.Select(c => c.name));
		Assert.All(save.UnitPrototypes, u => Assert.Subset(save.Civilizations.Select(c => c.name).ToHashSet(), u.producibleBy));
		HashSet<ID> hidden = save.Techs.Where(t => t.EraCivilopediaName == EraUtils.HIDDEN_CVLPD).Select(t => t.id).ToHashSet();
		Assert.All(save.Civilizations.Where(c => !c.isBarbarian), c => Assert.Single(c.startingTechs, hidden.Contains));
		for (int seed = 1; seed <= 3; ++seed) {
			SaveGame game = NewSave(seed, 8);
			Assert.All(game.Players.Select(p => game.Civilizations.Single(c => c.name == p.civilization)).Where(c => !c.isBarbarian),
				c => Assert.Contains(c.name, ModernCivilizations));
		}
	}

	// A government's buildings carry its initial, as the mod's own do; the
	// second parts of the wonders are named after their civilization; what
	// referred to a renamed building still does. The bundled rules have
	// these names themselves, as the import of the BIQ makes them.
	[Fact]
	public void TheBuildingsReadAsTheModNamesThem() {
		SaveGame save = Rules();
		Dictionary<string, SaveBuilding> byName = save.Buildings.ToDictionary(b => b.name);
		foreach (string name in new[] { "T Barracks", "F Barracks", "O Barracks (+1 Gold)", "E Barracks", "T Airport", "F Airport", "O Airport",
				"E Coal Plant", "F Coal Plant", "O Coal Plant", "T Coal Plant", "E Hydro Plant", "F Hydro Plant", "O Hydro Plant",
				"T Marketplace", "T Service Part 2 (America)", "O Experiments Part 2 (America)", "Barracks" }) {
			Assert.Contains(name, byName.Keys);
		}
		Assert.DoesNotContain(save.Buildings, b => b.name.EndsWith(")") && save.Governments.Any(g => b.name.EndsWith($" ({g.name})")));
		Assert.DoesNotContain(save.Buildings, b => System.Text.RegularExpressions.Regex.IsMatch(b.name, @" \d+$"));
		foreach (SaveBuilding b in save.Buildings.Where(b => b.requiredGovernment != null)) {
			string government = save.Governments.Single(g => g.id == b.requiredGovernment).name;
			Assert.StartsWith(government[..1] + " ", b.name);
		}
		foreach (SaveBuilding b in save.Buildings) {
			foreach (string reference in new[] { b.requiredBuilding, b.doublesHappinessOf, b.greatWonderProperties?.buildingGainedInEveryCity,
					b.greatWonderProperties?.buildingGainedInEveryCityOnContinent }.Where(r => r != null)) {
				Assert.Contains(reference, byName.Keys);
			}
		}
		// Sun Tzu's barracks is the one only it gives, of no government
		// (the BIQ's reference to it, by index, is to the last of its five
		// Barracks; by name it was once taken for Tradition's, the first).
		SaveBuilding barracks = byName[byName["Sun Tzu's"].greatWonderProperties.buildingGainedInEveryCityOnContinent];
		Assert.Equal("Barracks", barracks.name);
		Assert.Null(barracks.requiredGovernment);
		Assert.Equal("The Hanging Gardens", barracks.requiredBuilding);
		SaveGame bundled = GameSetup.LoadBundledRules(PathUtils.GameModesDir, Mode, GameSetup.ModernRules);
		Assert.Equal(save.Buildings.Select(b => b.name), bundled.Buildings.Select(b => b.name));
	}

	// Every civ can leave the placeholder government on the first turn for
	// any of the four, and builds only its government's buildings.
	[Fact]
	public void TheFourGovernmentsAreThereFromTheStart() {
		C7GameData.GameData gameData = NewGame("America");
		Player america = Civ(gameData, "America");
		Assert.Equal("!Switch Governments!", america.government.name);
		Assert.True(america.CanStartRevolution(gameData));
		Assert.Equal(["Equality", "Freedom", "Order", "Tradition"],
			america.GetAvailableGovernments(gameData).Select(g => g.name).Where(n => n != "!Switch Governments!").Order());
		City city = Found(gameData, america, "Washington");
		Building tBarracks = gameData.Buildings.Single(b => b.name == "T Barracks");
		Building fBarracks = gameData.Buildings.Single(b => b.name == "F Barracks");
		Assert.False(tBarracks.CanProduce(city, []));
		Adopt(gameData, america, "Tradition");
		Assert.True(tBarracks.CanProduce(city, []));
		Assert.False(fBarracks.CanProduce(city, []));
	}

	// Tradition's second wonder: its second part, a great wonder, can be
	// built only where the first part is, and only as the civilization's own
	// copy; it puts an aqueduct in every city and doubles the T Temple.
	[Fact]
	public void TraditionsSecondWonderIsBuiltInTwoParts() {
		C7GameData.GameData gameData = NewGame("America");
		Player america = Civ(gameData, "America");
		Adopt(gameData, america, "Tradition");
		City capital = Found(gameData, america, "Washington");
		City other = Found(gameData, america, "Boston");
		Building part1 = gameData.Buildings.Single(b => b.name == "T Service (Worker/2, +7 GPT)");
		Building ours = gameData.Buildings.Single(b => b.name == "T Service Part 2 (America)");
		Building egypts = gameData.Buildings.Single(b => b.name == "T Service Part 2 (Egypt)");
		Building temple = gameData.Buildings.Single(b => b.name == "T Temple (+3 Happy and +1 GPT)");
		Building aqueduct = gameData.Buildings.Single(b => b.name == "Aqueduct (+1 Culture)");
		america.knownTechs.Add(part1.requiredTech.id);
		Assert.False(ours.CanProduce(capital, []));
		capital.AddBuilding(part1);
		Assert.True(ours.CanProduce(capital, []));
		Assert.False(ours.CanProduce(other, []));
		Assert.False(egypts.CanProduce(capital, []));

		other.AddBuilding(temple);
		int before = other.BuildingContentFaces(other.EffectiveBuildings());
		Assert.False(other.IsProvidedByWonders(aqueduct));
		capital.AddBuilding(ours);
		Assert.True(other.IsProvidedByWonders(aqueduct));
		Assert.Equal(before + temple.contentFacesInCity, other.BuildingContentFaces(other.EffectiveBuildings()));
	}

	// E Utopia's four content faces reach every city, and F Prosperity
	// makes cities grow by two (Longevity's effect), though both are small
	// wonders.
	[Fact]
	public void SmallWondersReachEveryCity() {
		C7GameData.GameData gameData = NewGame("USSR");
		Player ussr = Civ(gameData, "USSR");
		City capital = Found(gameData, ussr, "Moscow");
		City other = Found(gameData, ussr, "Kiev");
		int before = other.BuildingContentFaces(other.EffectiveBuildings());
		capital.AddBuilding(gameData.Buildings.Single(b => b.name == "E Utopia (+4 Happy All Cities)"));
		Assert.Equal(before + 4, other.BuildingContentFaces(other.EffectiveBuildings()));
		Assert.False(ussr.GetBuildingSnapshot().doublesCityGrowthEverywhere);
		capital.AddBuilding(gameData.Buildings.Single(b => b.name == "F Prosperity"));
		Assert.True(ussr.GetBuildingSnapshot().doublesCityGrowthEverywhere);
	}

	// T Guard gives a RAD Marine every four turns.
	[Fact]
	public void TheGuardGivesAMarineEveryFourTurns() {
		C7GameData.GameData gameData = NewGame("America");
		Player america = Civ(gameData, "America");
		City capital = Found(gameData, america, "Washington");
		capital.AddBuilding(gameData.Buildings.Single(b => b.name == "T Guard (+5 GPT, Marine/4t)"));
		int Marines() => america.units.Count(u => u.unitType.name == "RAD Marine (9/4/1)");
		for (int turn = 1; turn <= 8; ++turn) {
			WonderUnits.ProduceFreeUnits(america, gameData);
			Assert.Equal(turn / 4, Marines());
		}
	}

	// A tactical missile (the Sub Cruise) goes in a boat that carries them,
	// the Cruiser among them, and not in a Transport; a Mini Cruise goes in
	// a Transport.
	[Fact]
	public void TacticalMissilesGoOnlyInTheBoatsThatCarryThem() {
		C7GameData.GameData gameData = NewGame("America");
		Player america = Civ(gameData, "America");
		Tile sea = gameData.map.tiles.First(t => t.IsWater() && t.unitsOnTile.Count == 0);
		MapUnit Spawn(string name) => gameData.SpawnUnit(america, gameData.unitPrototypes.Single(p => p.name == name), sea);
		MapUnit transport = Spawn("Transport");
		MapUnit cruiser = Spawn("Cruiser");
		MapUnit subCruise = Spawn("Sub Cruise");
		MapUnit miniCruise = Spawn("Mini Cruise");
		subCruise.BoardTransport(transport);
		Assert.False(subCruise.IsLoadedIn(transport));
		miniCruise.BoardTransport(cruiser);
		Assert.False(miniCruise.IsLoadedIn(cruiser));
		Assert.True(cruiser.CanCarryUnits());
		subCruise.BoardTransport(cruiser);
		Assert.True(subCruise.IsLoadedIn(cruiser));
		miniCruise.BoardTransport(transport);
		Assert.True(miniCruise.IsLoadedIn(transport));
	}

	// The AI plays every civ of a Modern game for some turns: each leaves
	// the placeholder government for one of the four, founds cities, builds
	// and grows, and nothing goes wrong on the way.
	[Fact]
	public async Task TheAiPlaysModern() {
		new MsgSetAnimationsEnabled(false).send();
		EngineStorage.ProcessNextMessageToEngine();
		BehaviorEngine behaviors = GameMode.Load(PathUtils.GameModesDir, GameSetup.BundledGameMode(Mode, GameSetup.ModernRules)).behaviors;
		await CreateGame.createGame(NewSave(7, 4), _ => behaviors);
		C7GameData.GameData gameData = EngineStorage.gameData;
		gameData.observerMode = true;
		foreach (Player player in gameData.players) {
			player.isHuman = false;
		}
		const int turns = 30;
		try {
			TurnHandling.OnBeginTurn();
			TurnHandling.InitTurnData();
			Task firstTurn = TurnHandling.AdvanceTurn();
			EngineWaits.WaitForStartTurnMessage(_ => { });
			await firstTurn;
			for (int turn = 0; turn < turns && !gameData.gameOver; ++turn) {
				new MsgEndTurn { turn = gameData.turn }.send();
				EngineWaits.WaitForStartTurnMessage(_ => { });
			}
		} finally {
			gameData.observerMode = false;
		}
		string[] four = ["Equality", "Tradition", "Freedom", "Order"];
		foreach (Player civ in gameData.players.Where(p => !p.isBarbarians && !p.defeated)) {
			output.WriteLine($"turn {gameData.turn} {civ.civilization.name}: {civ.government.name} cities={civ.cities.Count} "
				+ $"citizens={civ.cities.Sum(c => c.residents.Count)} units={civ.units.Count} techs={civ.knownTechs.Count} "
				+ $"buildings={string.Join(", ", civ.cities.SelectMany(c => c.constructed_buildings).Select(cb => cb.building.name))}");
			Assert.Contains(civ.government.name, four);
			Assert.True(civ.cities.Count >= 2, $"{civ.civilization.name} has {civ.cities.Count} cities");
			Assert.True(civ.cities.Sum(c => c.residents.Count) > civ.cities.Count, $"{civ.civilization.name}'s cities haven't grown");
			Assert.True(civ.units.Count(u => !u.unitType.isSettler && !u.unitType.isWorker && u.unitType.attack + u.unitType.defense > 0) > 0,
				$"{civ.civilization.name} has built no military units");
		}
	}
}
