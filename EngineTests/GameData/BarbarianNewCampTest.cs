using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Barbarian camps that appear during the game, where no civ can see.
public class BarbarianNewCampTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;

	public BarbarianNewCampTest(SaveGameFixture fixture) {
		this.fixture = fixture;
	}

	private static List<Player> Civs(C7GameData.GameData gameData) =>
		gameData.players.Where(p => !p.isBarbarians && !p.defeated).ToList();

	[Fact]
	public void NewCampSitesAreUnseenAndUnowned() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		List<Tile> sites = BarbarianInteractions.PossibleNewCampSites(gameData);
		Assert.NotEmpty(sites);
		foreach (Tile site in sites) {
			Assert.Null(site.owningCity);
			Assert.False(site.hasBarbarianCamp);
			Assert.Empty(site.unitsOnTile);
			Assert.DoesNotContain(Civs(gameData), civ => civ.tileKnowledge.isActiveTile(site));
		}
	}

	[Fact]
	public void NoCampAppearsWhereACivCanSee() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		Tile site = BarbarianInteractions.PossibleNewCampSites(gameData).First();
		Tile next = site.neighbors.Values.First(t => t != Tile.NONE && t.IsLand() && t.unitsOnTile.Count == 0);

		// A civ's warrior next to the site sees it.
		Player civ = Civs(gameData)[0];
		civ.tileKnowledge.knownTiles.Add(next);
		civ.tileKnowledge.knownTiles.Add(site);
		gameData.SpawnUnit(civ, gameData.unitPrototypes.First(p => p.name == "Warrior"), next);

		Assert.DoesNotContain(site, BarbarianInteractions.PossibleNewCampSites(gameData));
	}

	[Fact]
	public void NoCampAppearsOnOwnedLand() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		Tile site = BarbarianInteractions.PossibleNewCampSites(gameData).First();
		Player civ = Civs(gameData)[0];
		site.owningCity = new City(Tile.NONE, civ, "Test", gameData.ids.CreateID("city"));

		Assert.DoesNotContain(site, BarbarianInteractions.PossibleNewCampSites(gameData));
	}

	[Fact]
	public void CampsAppearOverTimeOnlyOnUnseenUnownedTiles() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		gameData.barbarianInfo.barbarianActivity = BarbarianActivity.Raging;
		int campsBefore = gameData.map.barbarianCamps.Count;

		Tile added = null;
		HashSet<Tile> sites = null;
		for (int turn = 0; turn < 1000 && added == null; ++turn) {
			sites = BarbarianInteractions.PossibleNewCampSites(gameData).ToHashSet();
			added = BarbarianInteractions.MaybeAddCamp(gameData);
		}

		Assert.NotNull(added);
		Assert.Contains(added, sites);
		Assert.True(added.hasBarbarianCamp);
		Assert.Contains(added, gameData.map.barbarianCamps);
		Assert.Equal(campsBefore + 1, gameData.map.barbarianCamps.Count);
		MapUnit defender = Assert.Single(added.unitsOnTile);
		Assert.True(defender.owner.isBarbarians);
	}

	[Fact]
	public void NoNewCampsWithoutBarbarians() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		gameData.barbarianInfo.barbarianActivity = BarbarianActivity.None;
		for (int turn = 0; turn < 1000; ++turn) {
			Assert.Null(BarbarianInteractions.MaybeAddCamp(gameData));
		}
	}
}
