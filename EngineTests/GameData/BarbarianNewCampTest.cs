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

	// Gives each civ placeholder cities, up to the count.
	private static void GiveEveryCivCities(C7GameData.GameData gameData, int count) {
		foreach (Player player in Civs(gameData)) {
			while (player.cities.Count < count) {
				player.cities.Add(new City(Tile.NONE, player, "Test", gameData.ids.CreateID("city")));
			}
		}
	}

	// Takes away camps until there is room for another under the limit of
	// one per player.
	private static void MakeRoomForACamp(C7GameData.GameData gameData) {
		while (gameData.map.barbarianCamps.Count >= BarbarianInteractions.MaxCamps(gameData)) {
			Tile camp = gameData.map.barbarianCamps[^1];
			camp.hasBarbarianCamp = false;
			gameData.map.barbarianCamps.Remove(camp);
		}
	}

	private static Tile TryForACamp(C7GameData.GameData gameData) {
		Tile added = null;
		for (int turn = 0; turn < 1000 && added == null; ++turn) {
			added = BarbarianInteractions.MaybeAddCamp(gameData);
		}
		return added;
	}

	// "No barb camps will appear until everyone has settled and there are at
	// least (2*NumPlayers) cities." (CFC 267101)
	[Fact]
	public void NoNewCampsUntilEveryoneHasSettledAndThereAreTwoCitiesPerPlayer() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		gameData.barbarianInfo.barbarianActivity = BarbarianActivity.Raging;
		MakeRoomForACamp(gameData);
		List<Player> civs = Civs(gameData);

		// One civ with all the cities, but the others unsettled.
		while (civs[0].cities.Count < 2 * civs.Count) {
			civs[0].cities.Add(new City(Tile.NONE, civs[0], "Test", gameData.ids.CreateID("city")));
		}
		Assert.False(BarbarianInteractions.CampsMayAppear(gameData));
		Assert.Null(TryForACamp(gameData));

		// Everyone settled, but one city each.
		foreach (Player civ in civs) {
			civ.cities.Clear();
		}
		GiveEveryCivCities(gameData, 1);
		Assert.False(BarbarianInteractions.CampsMayAppear(gameData));
		Assert.Null(TryForACamp(gameData));

		GiveEveryCivCities(gameData, 2);
		Assert.True(BarbarianInteractions.CampsMayAppear(gameData));
		Assert.NotNull(TryForACamp(gameData));
	}

	// "The maximum number of camps is the number of players" (CFC 267101).
	[Fact]
	public void NoMoreCampsThanPlayers() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		gameData.barbarianInfo.barbarianActivity = BarbarianActivity.Raging;
		GiveEveryCivCities(gameData, 2);
		MakeRoomForACamp(gameData);
		for (int turn = 0; turn < 5000; ++turn) {
			BarbarianInteractions.MaybeAddCamp(gameData);
		}
		Assert.Equal(BarbarianInteractions.NumPlayers(gameData), gameData.map.barbarianCamps.Count);
	}

	[Fact]
	public void CampsAppearOverTimeOnlyOnUnseenUnownedTiles() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		gameData.barbarianInfo.barbarianActivity = BarbarianActivity.Raging;
		GiveEveryCivCities(gameData, 2);
		MakeRoomForACamp(gameData);
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
