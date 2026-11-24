using System;
using System.Collections.Generic;
using System.IO;
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

// Future (the League's newfuture3.3.biq, see GameSetup.BundledRules.cs):
// its tribes, their traits, unique units and golden ages, the start every
// civ gets, and the AI playing it.
public class FutureScenarioTest {
	private readonly ITestOutputHelper output;

	public FutureScenarioTest(ITestOutputHelper output) {
		this.output = output;
		EngineStorage.messagesToUI.Clear();
		EngineStorage.pendingMessages.Clear();
	}

	private static GameMode.Config Mode => new("civ3", ["standalone"]);

	private static SaveGame Rules() => GameSetup.LoadBundledRules(PathUtils.GameModesDir, Mode, GameSetup.FutureRules);

	private static SaveGame NewSave(int seed, Civilization player, IEnumerable<SelectedOpponent> opponents, int size = 70) {
		SaveGame save = Rules();
		List<SelectedOpponent> others = opponents.ToList();
		WorldCharacteristics wc = new(save) {
			landform = WorldCharacteristics.Landform.Pangaea,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_70,
			age = WorldCharacteristics.Age.Billion_5,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			barbarianActivity = BarbarianActivity.Roaming,
			worldSize = new WorldSize() { width = size, height = size, numberOfCivs = others.Count + 1, distanceBetweenCivs = 10, techRate = 240, optimalNumberOfCities = 16 },
			mapSeed = seed,
		};
		new GameSetup {
			playerCivilization = save.Civilizations.Single(c => c.name == player.name),
			difficulty = save.Difficulties[2],
			worldCharacteristics = wc,
			opponents = others,
			victoryConditions = save.VictoryConditions,
			scenario = GameScenario.Future,
		}.Populate(save);
		return save;
	}

	// The League's 21 tribes (its Future build tool, FutureTribe.kt), with
	// their traits and unique units, and the resources each unit needs
	// (FutureUnit.kt). The BIQ gives the Dutch, whom the League doesn't
	// play, the Swiss Mercenary: they aren't among its playable
	// civilizations, so not in the rules, and no one builds it.
	public static TheoryData<string, string, string, string> Tribes => new() {
		{ "America", "expansionist+industrious", "F-15", "Aluminum+Oil" },
		{ "Arabia", "expansionist+religious", "Ansar Warrior", "Horses+Iron" },
		{ "Byzantines", "scientific+seafaring", null, null },
		{ "Carthage", "industrious+seafaring", null, null },
		{ "China", "industrious+militaristic", "Rider", "Horses+Iron" },
		{ "Egypt", "industrious+religious", null, null },
		{ "England", "commercial+seafaring", "Man-O-War", "" },
		{ "France", "commercial+industrious", "Musketeer", "Saltpeter" },
		{ "Germany", "militaristic+scientific", "Panzer", "Oil+Rubber" },
		{ "Hittites", "commercial+expansionist", null, null },
		{ "India", "commercial+religious", "War Elephant", "Ivory" },
		{ "Japan", "militaristic+religious", "Samurai", "Iron" },
		{ "Korea", "commercial+scientific", "Hwach'a", "" },
		{ "Mongols", "expansionist+militaristic", "Keshik", "Horses" },
		{ "Ottomans", "industrious+scientific", "Sipahi", "Horses+Saltpeter" },
		{ "Persia", "industrious+scientific", null, null },
		{ "Portugal", "expansionist+seafaring", "Carrack", "" },
		{ "Rome", "commercial+militaristic", null, null },
		{ "Russia", "expansionist+scientific", "Cossack", "Horses+Saltpeter" },
		{ "Spain", "religious+seafaring", "Conquistador", "Horses" },
		{ "Scandinavia", "militaristic+seafaring", "Berserk", "" },
	};

	// Each tribe has its traits, and its unique unit is its own, needs the
	// resources the League lists, starts a golden age when it wins, and
	// replaces the unit the League says it does.
	[Theory]
	[MemberData(nameof(Tribes))]
	public void EachTribeHasItsTraitsAndUnit(string civName, string traits, string unique, string resources) {
		SaveGame save = Rules();
		Civilization civ = save.Civilizations.Single(c => c.name == civName);
		Assert.Equal(traits.Split('+').OrderBy(t => t), civ.traits.Select(t => t.ToString().ToLowerInvariant()).OrderBy(t => t));
		List<SaveUnitPrototype> own = save.UnitPrototypes.Where(u => !u.unproducible && u.producibleBy?.Count == 1 && u.producibleBy.Contains(civName)
			&& !u.flags.Contains(SaveUnitPrototype.Flag.King)).ToList();
		if (unique == null) {
			Assert.Empty(own);
			return;
		}
		SaveUnitPrototype unit = Assert.Single(own);
		Assert.Equal(unique, unit.name);
		Assert.Contains(SaveUnitPrototype.Flag.StartsGoldenAge, unit.flags);
		Assert.Equal(resources.Split('+', StringSplitOptions.RemoveEmptyEntries).OrderBy(r => r), (unit.requiredResources ?? []).OrderBy(r => r));
		// The standard unit it stands in for isn't the tribe's to build.
		string replaced = unique switch {
			"Panzer" => "Tank",
			"Cossack" or "Sipahi" => "Cavalry",
			"Man-O-War" => "Frigate",
			"Rider" or "Samurai" or "Keshik" or "Conquistador" or "Ansar Warrior" or "War Elephant" => "Knight",
			_ => null,
		};
		if (replaced != null) {
			Assert.DoesNotContain(civName, save.UnitPrototypes.Single(u => u.name == replaced).producibleBy);
		}
	}

	// Each unit needs the resources the BIQ lists, all three of them when
	// it lists three: Modern Armor needs Aluminum as well as Oil and Rubber.
	// The check against the BIQ is skipped without it (see
	// BundledScenariosTest.TheBundledRulesAreTheBiqs).
	[SkippableFact]
	public void UnitsNeedTheBiqsResources() {
		SaveGame save = Rules();
		Assert.Equal(["Aluminum", "Oil", "Rubber"], save.UnitPrototypes.Single(u => u.name == "Modern Armor").requiredResources.OrderBy(r => r));
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");
		string folder = Environment.GetEnvironmentVariable("C7_LEAGUE_MODS")
			?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Scenarios");
		string path = Path.Combine(folder, GameSetup.FutureRules.BiqName);
		Skip.If(!File.Exists(path), $"No {path}");
		SaveGame biq = GameSetup.ImportBundledBiq(path, QueryCiv3.Civ3Location.GetCiv3Path(), GameSetup.FutureRules);
		Assert.Equal(biq.UnitPrototypes.Select(u => (u.name, string.Join("+", u.requiredResources.OrderBy(r => r)))),
			save.UnitPrototypes.Select(u => (u.name, string.Join("+", u.requiredResources.OrderBy(r => r)))));
	}

	// The BIQ's game settings let 21 civilizations play, the League's
	// tribes, and the rules have only those: the Greeks, Babylonians, Zulu
	// and Dutch are in the BIQ's list of civilizations but not among them,
	// and no unit names them.
	[Fact]
	public void TheLeaguesTribesAreAllHere() {
		SaveGame save = Rules();
		HashSet<string> names = save.Civilizations.Where(c => !c.isBarbarian).Select(c => c.name).ToHashSet();
		Assert.Equal(Tribes.Select(t => (string)t[0]).Order(), names.Order());
		Assert.Equal(21, names.Count);
		Assert.All(save.UnitPrototypes, u => Assert.Subset(save.Civilizations.Select(c => c.name).ToHashSet(), u.producibleBy));
		Assert.Empty(save.UnitPrototypes.Single(u => u.name == "Swiss Mercenary").producibleBy);
	}

	// Commercial civs start with Being Commercial, the others with Being
	// NonCommercial, hidden advances that can't be researched or traded:
	// a commercial civ builds the cheaper C. Market, C. Bank, C. Dock, C.
	// Stock Exchange and C. Wall Street, the others the usual ones.
	[Fact]
	public void CommercialCivsBuildCheaperCommerce() {
		SaveGame save = Rules();
		foreach (Civilization civ in save.Civilizations.Where(c => !c.isBarbarian)) {
			string being = civ.traits.Contains(Civilization.Trait.Commercial) ? "Being Commercial" : "Being NonCommercial";
			string[] beings = civ.startingTechs.Select(id => save.Techs.Single(t => t.id == id).Name).Where(n => n.StartsWith("Being ")).ToArray();
			Assert.Equal([being], beings);
		}
		Assert.All(save.Techs.Where(t => t.Name.StartsWith("Being ")), t => Assert.Equal(EraUtils.HIDDEN_CVLPD, t.EraCivilopediaName));
		int Cost(string name) => save.Buildings.Single(b => b.name == name).shieldCost;
		Assert.True(Cost("C. Market") < Cost("Marketplace"));
		Assert.True(Cost("C. Bank") < Cost("Bank"));
		Assert.True(Cost("C. Dock") < Cost("Dock"));
		Assert.True(Cost("C. Stock Exchange (+50% Lux)") < Cost("Stock Exchange (+50% Lux)"));

		C7GameData.GameData gameData = SaveGameFixture.HydrateSaveGame(NewSave(3, save.Civilizations.Single(c => c.name == "Rome"),
			[new SelectedOpponent { Name = "Egypt" }]));
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		foreach ((string civName, string market, string notMarket) in new[] { ("Rome", "C. Market", "Marketplace"), ("Egypt", "Marketplace", "C. Market") }) {
			Player player = gameData.players.Single(p => p.civilization.name == civName);
			City city = CityInteractions.BuildCity(player.units.First(u => u.unitType.actions.Contains(UnitAction.BuildCity)).location, player, civName);
			List<string> options = city.ListProductionOptions(gameData).Select(p => p.name).ToList();
			Assert.Contains(market, options);
			Assert.DoesNotContain(notMarket, options);
			Assert.DoesNotContain(gameData.techs.Where(t => t.Name.StartsWith("Being ")), t => player.GetAvailableTechsToResearch(gameData.techs).Contains(t));
		}
	}

	// Each tribe's unique unit, of whatever era, can be built given its
	// resources until the tribe has had its golden age, though the units it
	// upgrades to can be built too (the Musketeer beside the Rifleman);
	// after it, only one that ends its upgrade chain, as the Carrack and the
	// F-15 do.
	[Fact]
	public void UniqueUnitsStayBuildableUntilTheGoldenAge() {
		SaveGame rules = Rules();
		C7GameData.GameData gameData = SaveGameFixture.HydrateSaveGame(NewSave(5, rules.Civilizations.Single(c => c.name == "Rome"),
			[new SelectedOpponent { Name = "Egypt" }]));
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		Player player = gameData.players.Single(p => p.civilization.name == "Rome");
		Tile coast = player.units.First(u => u.unitType.isSettler).location;
		coast = gameData.map.tiles.Where(t => t.IsLand() && t.NeighborsWater() && !t.HasCity())
			.OrderBy(t => Math.Abs(t.XCoordinate - coast.XCoordinate) + Math.Abs(t.YCoordinate - coast.YCoordinate)).First();
		City city = CityInteractions.BuildCity(coast, player, "Ostia");
		HashSet<Resource> all = gameData.Resources.ToHashSet();
		Civilization rome = player.civilization;
		try {
			foreach (object[] tribe in Tribes.Where(t => t[2] != null)) {
				player.civilization = gameData.civilizations.Single(c => c.name == (string)tribe[0]);
				UnitPrototype unique = gameData.unitPrototypes.Single(u => u.name == (string)tribe[2]);
				player.hadGoldenAge = false;
				Assert.True(unique.CanProduce(city, all), $"{unique.name} before the golden age");
				if (unique.requiredResources.Count > 0) {
					Assert.False(unique.CanProduce(city, all.Where(r => !unique.requiredResources.Contains(r)).ToHashSet()), $"{unique.name} without its resources");
				}
				player.hadGoldenAge = true;
				Assert.Equal(unique.upgradesTo.Count == 0, unique.CanProduce(city, all));
			}
		} finally {
			player.civilization = rome;
			player.hadGoldenAge = false;
		}
	}

	// A golden age comes from a unique unit's victory or from great wonders
	// covering both the civ's traits. The mod names each wonder's traits
	// after it; the League's twelve wonders have the traits it lists
	// (FutureWonder.kt), and the three marked (GA) have every trait, so
	// building one starts any civ's golden age.
	[Fact]
	public void WondersCarryTheirTraits() {
		SaveGame save = Rules();
		var wonders = new (string name, string traits)[] {
			("Copernicus'", "militaristic+expansionist+scientific"),
			("Cure for Cancer", "industrious+militaristic+seafaring"),
			("Hoover Dam", "industrious"),
			("JS Bach's", "scientific+religious"),
			("Leonardo's", "militaristic+religious+seafaring"),
			("Magellan's", "industrious+expansionist+commercial+seafaring"),
			("The Manhattan Project", "militaristic+scientific"),
			("Mausoleum", "scientific+seafaring"),
			("Newton's", "industrious+scientific+commercial"),
			("The Pyramids", "industrious+religious"),
			("Sistine Chapel", "religious+commercial+seafaring"),
			("Sun Tzu's", "militaristic+expansionist+commercial"),
		};
		foreach ((string name, string traits) in wonders) {
			SaveBuilding wonder = save.Buildings.Single(b => b.name.StartsWith(name + " ("));
			Assert.NotNull(wonder.greatWonderProperties);
			// Hoover Dam's and the Pyramids' agricultural trait is no
			// civ's.
			Assert.Equal(traits.Split('+').OrderBy(t => t),
				wonder.traits.Where(t => t != Civilization.Trait.Agricultural).Select(t => t.ToString().ToLowerInvariant()).OrderBy(t => t));
		}
		foreach (string name in new[] { "Longevity (GA)", "The Internet (GA)", "Universal Suffrage (GA)" }) {
			Assert.Equal(Enum.GetValues<Civilization.Trait>().Length, save.Buildings.Single(b => b.name == name).traits.Count);
		}
		Assert.Empty(save.Civilizations.Where(c => c.traits.Contains(Civilization.Trait.Agricultural)));
	}

	// Every civilization starts a Future game alike: in the Modern Era,
	// under Despotism with 10 gold, a Settler, a Worker and its King
	// (Regicide), knowing every advance of the earlier eras; and each is
	// drawn in standalone mode with its own leader portrait, a colour of
	// the palette and its own city names.
	[Fact]
	public void EveryCivStartsAndReadsWell() {
		SaveGame rules = Rules();
		List<Civilization> civs = rules.Civilizations.Where(c => !c.isBarbarian).ToList();
		string portraits = PathUtils.getBasePath("../C7/ModernArt/Art/Leaderheads");
		Assert.All(civs, c => {
			Assert.True(File.Exists(Path.Combine(portraits, c.name + ".png")), $"{c.name} has no portrait");
			Assert.InRange(c.primaryColorIndex, 0, 31);
			Assert.InRange(c.secondaryColorIndex, 0, 31);
			Assert.True(c.cityNames.Count >= 16, $"{c.name} has {c.cityNames.Count} city names");
			Assert.Equal(c.cityNames.Count, c.cityNames.Distinct().Count());
			Assert.False(string.IsNullOrWhiteSpace(c.leader));
			Assert.Contains(rules.UnitPrototypes, u => u.name == c.kingUnit && u.flags.Contains(SaveUnitPrototype.Flag.King));
		});
		int seed = 10;
		foreach (Civilization[] group in civs.Chunk(5)) {
			C7GameData.GameData gameData = SaveGameFixture.HydrateSaveGame(NewSave(seed++, group[0],
				group.Skip(1).Select(c => new SelectedOpponent { Name = c.name }), size: 60));
			foreach (Player civ in gameData.players.Where(p => !p.IsIndependent)) {
				Assert.Equal(EraUtils.MODERN_ERA_CVLPD, civ.eraCivilopediaName);
				Assert.Equal("Despotism", civ.government.name);
				Assert.Equal(10, civ.gold);
				Assert.Single(civ.units, u => u.unitType.name == "Settler");
				Assert.Single(civ.units, u => u.unitType.name == "Worker");
				Assert.Single(civ.units, u => u.unitType.isKing && u.unitType.name == civ.civilization.kingUnit);
				HashSet<string> known = civ.knownTechs.Select(id => gameData.techs.Single(t => t.id == id).Name).ToHashSet();
				Assert.Equal(82, known.Count(n => !n.StartsWith("Being ")));
				Assert.Single(known, n => n.StartsWith("Being "));
				Assert.Equal(["ICBM", "Integrated Defense"], gameData.techs.Where(t => !known.Contains(t.Name) && !t.Name.StartsWith("Being ")).Select(t => t.Name).OrderBy(n => n));
			}
			Assert.Equal(group.Select(c => c.name).OrderBy(n => n), gameData.players.Where(p => !p.IsIndependent).Select(p => p.civilization.name).OrderBy(n => n));
		}
	}

	// Standalone mode has a Civilopedia article for each of Future's
	// civilizations, units, buildings, advances (but the hidden two) and
	// governments.
	[Fact]
	public void EverythingHasAnArticle() {
		SaveGame save = Rules();
		string text = File.ReadAllText(PathUtils.getBasePath("../C7/Civilopedia/Text/Civilopedia.txt"));
		HashSet<string> articles = System.Text.RegularExpressions.Regex.Matches(text, @"^#(\S+)", System.Text.RegularExpressions.RegexOptions.Multiline)
			.Select(m => m.Groups[1].Value.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
		IEnumerable<(string name, string key)> all = save.Civilizations.Where(c => !c.isBarbarian).Select(c => (c.name, c.civilopediaEntry))
			.Concat(save.UnitPrototypes.Select(u => (u.name, u.civilopediaEntry)))
			.Concat(save.Buildings.Select(b => (b.name, b.civilopediaEntry)))
			.Concat(save.Techs.Where(t => t.EraCivilopediaName != EraUtils.HIDDEN_CVLPD).Select(t => (t.Name, t.CivilopediaEntry)))
			.Concat(save.Governments.Select(g => (g.name, g.civilopediaEntry)));
		// Future's own keys still waiting for their articles are LeagueArtTest's.
		Assert.Empty(all.Where(x => x.key == null || !articles.Contains(x.key) && !LeagueArtTest.AwaitsArticle(x.key, articles.Contains))
			.Select(x => $"{x.name} ({x.key})"));
	}

	// The AI plays a Future game for some turns: it founds cities, builds
	// more than palaces, leaves Despotism, raises soldiers, and nothing goes
	// wrong on the way. (On this map the Mongols start on an island too
	// small for a second city, which the Standard AI never sails from.)
	[Theory]
	[InlineData(AiType.Standard)]
	[InlineData(AiType.Advanced)]
	public async Task TheAiPlaysFuture(AiType aiType) {
		new MsgSetAnimationsEnabled(false).send();
		EngineStorage.ProcessNextMessageToEngine();
		BehaviorEngine behaviors = GameMode.Load(PathUtils.GameModesDir, GameSetup.BundledGameMode(Mode, GameSetup.FutureRules)).behaviors;
		SaveGame rules = Rules();
		string[] civNames = ["Germany", "Mongols", "England", "Korea", "Arabia", "America"];
		SaveGame save = NewSave(7, rules.Civilizations.Single(c => c.name == civNames[0]),
			civNames.Skip(1).Select(n => new SelectedOpponent { Name = n }), size: 60);
		await CreateGame.createGame(save, _ => behaviors);
		C7GameData.GameData gameData = EngineStorage.gameData;
		gameData.observerMode = true;
		foreach (Player player in gameData.players) {
			player.isHuman = false;
			if (!player.IsIndependent) {
				player.aiType = aiType;
			}
		}
		int turns = int.TryParse(Environment.GetEnvironmentVariable("C7_FUTURE_TURNS"), out int t) ? t : 40;
		Dictionary<Player, HashSet<string>> built = gameData.players.ToDictionary(p => p, _ => new HashSet<string>());
		try {
			TurnHandling.OnBeginTurn();
			TurnHandling.InitTurnData();
			Task firstTurn = TurnHandling.AdvanceTurn();
			EngineWaits.WaitForStartTurnMessage(_ => { });
			await firstTurn;
			for (int turn = 0; turn < turns && !gameData.gameOver; ++turn) {
				new MsgEndTurn { turn = gameData.turn }.send();
				EngineWaits.WaitForStartTurnMessage(_ => { });
				foreach (Player p in gameData.players.Where(p => !p.IsIndependent && !p.isBarbarians)) {
					foreach (MapUnit u in p.units) {
						built[p].Add(u.unitType.name);
					}
					foreach (City c in p.cities) {
						built[p].UnionWith(c.constructed_buildings.Select(cb => cb.building.name));
					}
					if (turn % 5 == 4 || turn == turns - 1) {
						output.WriteLine($"turn {gameData.turn} {p.civilization.name}: {p.government.name} cities={p.cities.Count} pop={p.cities.Sum(c => c.residents.Count)} units={p.units.Count} gold={p.gold} ga={p.InGoldenAge} wars={gameData.players.Count(o => o != p && !o.IsIndependent && p.playerRelationships.TryGetValue(o.id, out PlayerRelationship r) && r.AtWar())} building={string.Join(",", p.cities.Select(c => c.itemBeingProduced?.name))}");
					}
				}
			}
		} finally {
			gameData.observerMode = false;
		}
		foreach (Player p in gameData.players.Where(p => !p.IsIndependent && !p.isBarbarians)) {
			output.WriteLine($"{p.civilization.name} had: {string.Join(", ", built[p].OrderBy(n => n))}");
		}
		int kills = gameData.players.Sum(p => p.unitsKilled);
		output.WriteLine($"units destroyed: {kills}");
		List<Player> civs = gameData.players.Where(p => !p.IsIndependent && !p.isBarbarians && !p.defeated).ToList();
		Assert.Equal(civNames.Length, civs.Count);
		Assert.All(civs, civ => Assert.NotEqual("Despotism", civ.government.name));
		Assert.All(civs, civ => Assert.NotEmpty(civ.cities));
		Assert.True(civs.Sum(c => c.cities.Count) >= 3 * civs.Count, $"{civs.Sum(c => c.cities.Count)} cities");
		Assert.True(civs.Sum(civ => civ.cities.Sum(c => c.constructed_buildings.Count)) >= 2 * civs.Count,
			$"{civs.Sum(civ => civ.cities.Sum(c => c.constructed_buildings.Count))} buildings in {civs.Sum(c => c.cities.Count)} cities");
		Assert.True(civs.Count(civ => built[civ].Any(name => gameData.unitPrototypes.Any(u => u.name == name && u.attack > 0 && !u.isKing && !u.isSettler))) >= civs.Count / 2,
			"Few civs raised soldiers");
	}
}
