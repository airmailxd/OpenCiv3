using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Drafting citizens, after the Conquests Civilopedia's GCON_Draft (see
// City.Draft).
public class DraftTest : IClassFixture<SaveGameFixture> {
	private readonly C7GameData.GameData gameData;
	private readonly Player player;
	private readonly City city;

	public DraftTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		player = gameData.players.First(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler));
		Tile tile = player.units.First(u => u.unitType.isSettler).location;
		city = CityInteractions.BuildCity(tile, player, player.GetNextCityName());
		player.government = gameData.governments.First(g => g.name == "Monarchy");
		player.knownTechs.Add(gameData.techs.First(t => t.HasFlag(SaveTech.Flag.EnablesConscription)).id);
		player.knownTechs.Add(gameData.unitPrototypes.First(u => u.name == "Spearman").requiredTech.id);
		GrowTo(7);
	}

	private void GrowTo(int size) {
		while (city.residents.Count < size) {
			CityResident r = new() {
				city = city,
				nationality = player.civilization,
				citizenType = gameData.citizenTypes.Find(x => x.IsDefaultCitizen),
			};
			city.AddCitizen(r);
			CityTileAssignmentAI.AssignNewCitizenToTile(gameData, r);
		}
	}

	[Fact]
	public void NationalismEnablesConscriptionAndTheBasicDefendersAreDraftable() {
		Assert.Equal("Nationalism", gameData.techs.Single(t => t.HasFlag(SaveTech.Flag.EnablesConscription)).Name);
		string[] draftable = gameData.unitPrototypes.Where(u => u.isDraftable).Select(u => u.name).ToArray();
		Assert.Contains("Spearman", draftable);
		Assert.Contains("Pikeman", draftable);
		Assert.Contains("Musketman", draftable);
		Assert.Contains("Rifleman", draftable);
		Assert.Contains("Infantry", draftable);
		Assert.Contains("Mech Infantry", draftable);
		Assert.DoesNotContain("Warrior", draftable);
	}

	[Fact]
	public void DraftingMakesAConscriptFromACitizen() {
		Assert.Null(city.WhyCannotDraft(gameData));
		UnitPrototype expected = city.DraftableUnit(gameData);

		MapUnit unit = city.Draft(gameData);

		Assert.NotNull(unit);
		Assert.Equal(expected, unit.unitType);
		Assert.True(unit.unitType.isDraftable);
		Assert.Equal(gameData.experienceLevels[0], unit.experienceLevel);
		Assert.Equal(city.location, unit.location);
		Assert.Equal(6, city.residents.Count);
		Assert.Equal(gameData.rules.TurnPenaltyForEachDraftedCitizen, city.turnsOfUnhappinessDueToDrafting);
	}

	[Fact]
	public void OnlyCitiesOfSizeSevenCanDraft() {
		city.RemoveCitizens(1);
		Assert.NotNull(city.WhyCannotDraft(gameData));
		Assert.Null(city.Draft(gameData));
		Assert.Equal(6, city.residents.Count);
	}

	// The test is the size level: "cities must be at least of level 2 size
	// in order to draft." (Flintlock, C3X thread post-16833134), so a BIQ
	// with bigger towns raises the minimum.
	[Fact]
	public void TheDraftNeedsACityOfSizeLevelTwo() {
		int townMax = player.rules.MaximumLevel1CitySize;
		try {
			player.rules.MaximumLevel1CitySize = 7;
			Assert.NotNull(city.WhyCannotDraft(gameData));
			GrowTo(8);
			Assert.Null(city.WhyCannotDraft(gameData));
		} finally {
			player.rules.MaximumLevel1CitySize = townMax;
		}
	}

	[Fact]
	public void NationalismIsNeeded() {
		player.knownTechs.Remove(gameData.techs.First(t => t.HasFlag(SaveTech.Flag.EnablesConscription)).id);
		Assert.NotNull(city.WhyCannotDraft(gameData));
	}

	[Fact]
	public void AnarchyCantDraft() {
		player.government = gameData.governments.First(g => g.transitionType);
		Assert.NotNull(city.WhyCannotDraft(gameData));
	}

	[Fact]
	public void TheGovernmentLimitsDraftsPerCityPerTurn() {
		GrowTo(12);
		int limit = player.government.draftLimit;
		Assert.Equal(2, limit);
		for (int i = 0; i < limit; ++i) {
			Assert.NotNull(city.Draft(gameData));
		}
		Assert.NotNull(city.WhyCannotDraft(gameData));

		city.HandleCityProduction(gameData);
		Assert.Null(city.WhyCannotDraft(gameData));
	}

	[Fact]
	public void ARepublicDraftsOnePerTurn() {
		player.government = gameData.governments.First(g => g.name == "Republic");
		Assert.Equal(1, player.government.draftLimit);
	}

	[Fact]
	public void EachDraftedCitizenMakesOneUnhappyUntilThePenaltyEnds() {
		int before = Unhappy();
		city.turnsOfUnhappinessDueToDrafting = gameData.rules.TurnPenaltyForEachDraftedCitizen;
		Assert.Equal(before + 1, Unhappy());
		city.turnsOfUnhappinessDueToDrafting = 2 * gameData.rules.TurnPenaltyForEachDraftedCitizen;
		Assert.Equal(before + 2, Unhappy());

		city.turnsOfUnhappinessDueToDrafting = 1;
		player.DecrementCityUnhappinessPenalties(gameData);
		Assert.Equal(0, city.turnsOfUnhappinessDueToDrafting);
		Assert.Equal(before, Unhappy());
	}

	private int Unhappy() {
		city.RecalculateCitizenMoods(gameData);
		return city.residents.Count(r => r.mood == CityResident.Mood.Unhappy);
	}

	[Fact]
	public void TheDraftCountersAreSaved() {
		city.turnsOfUnhappinessDueToDrafting = 17;
		city.unitsDraftedThisTurn = 1;

		City loaded = new SaveCity(city).ToCity(gameData.map, gameData.players, gameData.unitPrototypes,
			gameData.civilizations, gameData.Buildings, gameData.citizenTypes, gameData.Inflows);

		Assert.Equal(17, loaded.turnsOfUnhappinessDueToDrafting);
		Assert.Equal(1, loaded.unitsDraftedThisTurn);
	}

	[Fact]
	public void TheStandaloneRulesetHasTheDraftFlags() {
		SaveGameFixture fixture = new();
		C7GameData.GameData standalone = fixture.standaloneSaveGame.ToGameData(fixture.behaviors);
		Assert.Equal("Nationalism", standalone.techs.Single(t => t.HasFlag(SaveTech.Flag.EnablesConscription)).Name);
		Assert.Contains(standalone.unitPrototypes, u => u.isDraftable && u.name == "Rifleman");
	}
}
