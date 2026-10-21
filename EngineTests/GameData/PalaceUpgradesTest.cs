using System.Collections.Generic;
using System.IO;
using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using Xunit;
using Achievement = C7GameData.PalaceUpgrades.Achievement;

namespace EngineTests.GameData;

// Palace upgrades, as Civ3 earns and spends them (PalaceUpgrades; Civ3
// FUN_00589ae0, FUN_00589a10, FUN_00589a70, FUN_005896c0).
public class PalaceUpgradesTest : System.IDisposable {
	private readonly C7GameData.GameData gameData = new() {
		rules = new Rules(),
		gameDifficulty = new Difficulty(),
	};

	public PalaceUpgradesTest() {
		EngineStorage.InitializeGameDataForTests(gameData);
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private Player NewPlayer(string name = "Rome", bool human = false) {
		Player player = new() {
			id = ID.FromString($"player-{gameData.players.Count + 1}"),
			isHuman = human,
			civilization = new Civilization(name),
			government = new Government(),
			eraCivilopediaName = EraUtils.ANCIENT_TIMES_CVLPD,
			rules = gameData.rules,
		};
		gameData.players.Add(player);
		return player;
	}

	private City NewCity(Player owner, int size, bool capital = false) {
		City city = new(Tile.NONE, owner, $"City {owner.cities.Count + 1}", ID.None("city")) { capital = capital };
		for (int i = 0; i < size; ++i) {
			Tile tile = new(ID.None("tile")) { overlayTerrainType = new TerrainType() };
			city.residents.Add(new CityResident() { tileWorked = tile, citizenType = new CitizenType() });
		}
		owner.cities.Add(city);
		return city;
	}

	[Fact]
	public void EachAchievementEarnsOneUpgradeOnce() {
		PalaceUpgrades palace = new();

		Assert.True(palace.Earn(Achievement.CapitalSize10));
		Assert.False(palace.Earn(Achievement.CapitalSize10));
		Assert.True(palace.Earn(Achievement.Culture5000));

		Assert.Equal(2, palace.upgradesToPlace);
		Assert.True(palace.HasEarned(Achievement.CapitalSize10));
	}

	// "Builds_Done + Builds_Remained < 0x20" (FUN_00589a10).
	[Fact]
	public void NoMoreThan32UpgradesAreEarned() {
		PalaceUpgrades palace = new() { piecesBuilt = 30, upgradesToPlace = 1 };

		Assert.True(palace.Earn(Achievement.CapitalSize10));
		Assert.False(palace.Earn(Achievement.CapitalSize20));

		Assert.Equal(PalaceUpgrades.MaxUpgrades, palace.UpgradesEarned);
		Assert.False(palace.HasEarned(Achievement.CapitalSize20));
	}

	// One offer covers every upgrade earned since the last, and the offers
	// stop at 17 (FUN_00589a50, FUN_00589a70).
	[Fact]
	public void TheOfferCoversEveryUpgradeSinceTheLast() {
		PalaceUpgrades palace = new();
		Assert.False(palace.HasUpgradeToAnnounce);

		palace.Earn(Achievement.CapitalSize10);
		palace.Earn(Achievement.CitySize10);
		Assert.True(palace.HasUpgradeToAnnounce);
		palace.MarkAnnounced();
		Assert.Equal(2, palace.upgradesAnnounced);
		Assert.False(palace.HasUpgradeToAnnounce);

		palace.upgradesAnnounced = PalaceUpgrades.MaxAnnouncedUpgrades;
		palace.upgradesToPlace = 20;
		Assert.False(palace.HasUpgradeToAnnounce);
	}

	[Fact]
	public void BuildingAPieceSpendsAnUpgrade() {
		PalaceUpgrades palace = new();
		Assert.False(palace.Build(3, 0));

		palace.Earn(Achievement.CapitalSize10);
		palace.Earn(Achievement.CapitalSize20);
		Assert.True(palace.Build(3, 4));

		Assert.Equal(4, palace.CultureOf(3));
		Assert.True(palace.IsBuilt(3));
		Assert.False(palace.IsBuilt(2));
		Assert.Equal(1u << 3, palace.BuiltPieces());
		Assert.Equal(1, palace.piecesBuilt);
		Assert.Equal(1, palace.upgradesToPlace);

		// Not the same piece again, nor a culture Civ3 doesn't have.
		Assert.False(palace.Build(3, 1));
		Assert.False(palace.Build(5, PalaceUpgrades.CultureCount));
		Assert.False(palace.Build(PalaceUpgrades.MaxUpgrades, 0));
	}

	[Fact]
	public void TheEngineBuildsPiecesOnlyWithAnUpgrade() {
		Player human = NewPlayer(human: true);
		EngineStorage.activePlayerID = human.id;

		new MsgBuildPalacePiece(0, 1) { playerID = human.id }.send();
		EngineStorage.ProcessNextMessageToEngine();
		Assert.False(human.palace.IsBuilt(0));

		human.palace.Earn(Achievement.CapitalSize10);
		new MsgBuildPalacePiece(0, 1) { playerID = human.id }.send();
		EngineStorage.ProcessNextMessageToEngine();
		Assert.Equal(1, human.palace.CultureOf(0));
		Assert.Equal(0, human.palace.upgradesToPlace);
	}

	[Fact]
	public void CitiesEarnUpgradesBySizeAndCelebration() {
		Player rome = NewPlayer();
		City capital = NewCity(rome, 20, capital: true);
		NewCity(rome, 10);
		capital.celebrating = true;

		PalaceUpgrades.CheckAchievements(rome, gameData);

		Assert.True(rome.palace.HasEarned(Achievement.CapitalSize10));
		Assert.True(rome.palace.HasEarned(Achievement.CapitalSize20));
		Assert.False(rome.palace.HasEarned(Achievement.CapitalSize30));
		Assert.True(rome.palace.HasEarned(Achievement.CitySize10));
		Assert.False(rome.palace.HasEarned(Achievement.CitySize20));
		Assert.True(rome.palace.HasEarned(Achievement.CapitalCelebrates));
		// Every city must celebrate, and there must be more than five.
		Assert.False(rome.palace.HasEarned(Achievement.AllCitiesCelebrate));
	}

	[Fact]
	public void CultureEarnsUpgrades() {
		Player rome = NewPlayer();
		City capital = NewCity(rome, 1, capital: true);
		City other = NewCity(rome, 1);
		capital.perPlayerCulture[rome] = 1000;
		other.perPlayerCulture[rome] = 4000;

		PalaceUpgrades.CheckAchievements(rome, gameData);

		Assert.True(rome.palace.HasEarned(Achievement.Culture5000));
		Assert.False(rome.palace.HasEarned(Achievement.Culture10000));
		// The capital's border level past 3: 1000 culture.
		Assert.True(rome.palace.HasEarned(Achievement.CapitalCulture));
		Assert.False(rome.palace.HasEarned(Achievement.CitiesWithCulture1000));
	}

	[Fact]
	public void FiveWondersInACityEarnAnUpgrade() {
		Player rome = NewPlayer();
		City capital = NewCity(rome, 1, capital: true);
		for (int i = 0; i < 4; ++i) {
			capital.AddBuilding(new Building(new SaveBuilding { name = $"Wonder {i}", isSmallWonder = true }, gameData));
		}
		PalaceUpgrades.CheckAchievements(rome, gameData);
		Assert.False(rome.palace.HasEarned(Achievement.WondersInCapital));

		capital.AddBuilding(new Building(new SaveBuilding { name = "Great", greatWonderProperties = new() }, gameData));
		PalaceUpgrades.CheckAchievements(rome, gameData);
		Assert.True(rome.palace.HasEarned(Achievement.WondersInCapital));
		Assert.False(rome.palace.HasEarned(Achievement.WondersInCity));
	}

	[Fact]
	public void TreatiesAndAlliancesEarnUpgrades() {
		Player rome = NewPlayer();
		Player greece = NewPlayer("Greece");
		PlayerRelationship relationship = new();
		relationship.multiTurnDeals.Add(new MultiTurnDeal(DealType.DiplomaticAgreement, DealSubType.Peace,
			DealDetails.Exchange, 0, null, 20, gameData.turn, null));
		rome.playerRelationships[greece.id] = relationship;

		PalaceUpgrades.CheckAchievements(rome, gameData);

		Assert.True(rome.palace.HasEarned(Achievement.PeaceOrPact));
		Assert.False(rome.palace.HasEarned(Achievement.MilitaryAlliance));
	}

	// FUN_0056d7f0: entering an era first, with no civ in a later era.
	[Fact]
	public void TheFirstIntoAnEraEarnsAnUpgrade() {
		Tech bronze = new() {
			id = ID.FromString("tech-1"),
			Name = "Bronze Working",
			Cost = 10,
			EraCivilopediaName = EraUtils.ANCIENT_TIMES_CVLPD,
			RequiredForEraAdvancement = true,
		};
		gameData.techs.Add(bronze);
		Player rome = NewPlayer();
		Player greece = NewPlayer("Greece");

		rome.AcquireTech(gameData, bronze);
		Assert.True(rome.palace.HasEarned(Achievement.FirstIntoMiddleAges));

		greece.AcquireTech(gameData, bronze);
		Assert.Equal(EraUtils.MIDDLE_AGES_CVLPD, greece.eraCivilopediaName);
		Assert.False(greece.palace.HasEarned(Achievement.FirstIntoMiddleAges));
	}

	[Fact]
	public void DestroyingACivEarnsAnUpgrade() {
		Player rome = NewPlayer();
		Player greece = NewPlayer("Greece");

		PalaceUpgrades.OnCivDestroyed(greece, rome);

		Assert.True(rome.palace.HasEarned(Achievement.DestroyedACiv));
		Assert.False(greece.palace.HasEarned(Achievement.DestroyedACiv));
	}

	[Fact]
	public void HumansAreToldOfNewUpgradesOnce() {
		Player human = NewPlayer(human: true);
		Player ai = NewPlayer("Greece");
		human.palace.Earn(Achievement.CapitalSize10);
		ai.palace.Earn(Achievement.CapitalSize10);

		PalaceUpgrades.AnnounceToHuman(human);
		PalaceUpgrades.AnnounceToHuman(human);
		PalaceUpgrades.AnnounceToHuman(ai);

		Assert.Single(EngineStorage.messagesToUI.OfType<MsgShowPalaceUpgrade>());
		Assert.Equal(1, human.palace.upgradesAnnounced);
		Assert.Equal(0, ai.palace.upgradesAnnounced);
	}

	[Fact]
	public void ThePalaceSurvivesASave() {
		Player rome = NewPlayer();
		rome.palace.Earn(Achievement.PeaceOrPact);
		rome.palace.Earn(Achievement.CapitalSize10);
		rome.palace.Build(2, 3);
		rome.palace.MarkAnnounced();

		SavePlayer saved = new(rome);
		string json = System.Text.Json.JsonSerializer.Serialize(saved, SaveJsonOptions());
		PalaceUpgrades loaded = System.Text.Json.JsonSerializer.Deserialize<SavePlayer>(json, SaveJsonOptions()).palace;

		Assert.Equal(rome.palace.achievements, loaded.achievements);
		Assert.Equal(3, loaded.CultureOf(2));
		Assert.Equal(1, loaded.upgradesToPlace);
		Assert.Equal(1, loaded.piecesBuilt);
		Assert.Equal(2, loaded.upgradesAnnounced);
	}

	[Fact]
	public void PlayersWithoutUpgradesSaveNoPalace() {
		Assert.Null(new SavePlayer(NewPlayer()).palace);
	}

	private static System.Text.Json.JsonSerializerOptions SaveJsonOptions() {
		return new System.Text.Json.JsonSerializerOptions {
			IncludeFields = true,
			Converters = {
				new IDJsonConverter(),
				new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase),
			},
		};
	}

	[Fact]
	public void ScriptTextFindsAnEntryAndItsCaption() {
		string[] lines = [
			"#ADVISORANGRY",
			"#advisor Angry",
			"I'm not even supposed to [be] here today!",
			"",
			"#comment ____",
			"",
			"#PALACE_VIEW_UPGRADE",
			"#caption The Caption",
			"The first line.",
			"^^Second.",
			"",
			"",
			"#SSVIEW_SWITCH_PROD",
			"Something else.",
		];

		ScriptEntry entry = ScriptText.Parse(lines).Get("PALACE_VIEW_UPGRADE");

		Assert.Equal("The Caption", entry.Caption);
		Assert.Equal(["The first line.", "Second."], entry.Lines);
		Assert.Null(ScriptText.Parse(lines).Get("MISSING"));
		Assert.Null(ScriptText.Load(Path.Combine(Path.GetTempPath(), "no-such-script.txt")).Get("PALACE_VIEW_UPGRADE"));
	}

	[Fact]
	public void PalaceCulturesAreNumberedAsCiv3Does() {
		Assert.Equal(0, C7Engine.PalaceMinigame.Culture.IndexOf("american"));
		Assert.Equal(2, C7Engine.PalaceMinigame.Culture.IndexOf("MEDITERRANEAN"));
		Assert.Equal(4, C7Engine.PalaceMinigame.Culture.IndexOf("asian"));
		Assert.Equal(-1, C7Engine.PalaceMinigame.Culture.IndexOf("martian"));
	}
}
