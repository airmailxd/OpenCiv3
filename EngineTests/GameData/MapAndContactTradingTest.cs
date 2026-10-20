using System;
using System.Collections.Generic;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using Xunit;

namespace EngineTests.GameData;

// Trading world maps, territory maps and contacts (see TradeOffer).
public class MapAndContactTradingTest : IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player us = MakeCiv("player-1");
	private readonly Player them = MakeCiv("player-2");
	private readonly Player stranger = MakeCiv("player-3");
	private readonly Tech navigation;
	private readonly Tech printingPress;
	private readonly List<Tile> ourTerritory = new();
	private readonly List<Tile> explored = new();

	public MapAndContactTradingTest() {
		navigation = MakeTech("tech-1", "Navigation", SaveTech.Flag.EnablesMapTrading);
		printingPress = MakeTech("tech-2", "Printing Press", SaveTech.Flag.EnablesCommunicationTrading);
		gameData = new() { rules = new Rules() { DefaultDealDuration = 20 }, techs = [navigation, printingPress] };
		EngineStorage.InitializeGameDataForTests(gameData);
		gameData.players.Add(us);
		gameData.players.Add(them);
		gameData.players.Add(stranger);
		us.EnsureRelationshipExists(them);
		us.EnsureRelationshipExists(stranger);
		us.isHuman = true;
		us.gold = 1000;
		them.gold = 1000;

		// We know 40 tiles, 20 of them in our territory.
		Tile cityTile = new(ID.None("tile"));
		City city = new(cityTile, us, "Rome", ID.None("city"));
		cityTile.cityAtTile = city;
		us.cities.Add(city);
		for (int i = 0; i < 40; ++i) {
			Tile t = i == 0 ? cityTile : new(ID.None("tile"));
			if (i < 20) {
				t.owningCity = city;
				ourTerritory.Add(t);
			} else {
				explored.Add(t);
			}
			us.tileKnowledge.knownTiles.Add(t);
		}
	}

	public void Dispose() {
		EngineStorage.pendingMessages.Clear();
		EngineStorage.messagesToUI.Clear();
	}

	private static Player MakeCiv(string id) {
		return new Player() { id = ID.FromString(id), civilization = new Civilization(id), government = new Government() };
	}

	private static Tech MakeTech(string id, string name, SaveTech.Flag flag) {
		SaveTech tech = new() { id = ID.FromString(id), Name = name };
		tech.flags.Add(flag);
		return tech.ToTechWithoutPrereqs();
	}

	private string Problem(TradeOffer weGive, TradeOffer weWant) => TradeOffer.ProblemWithDeal(gameData, us, them, weGive, weWant);

	[Fact]
	public void MapsNeedNavigationOnBothSides() {
		TradeOffer map = new() { worldMap = true };
		Assert.NotNull(Problem(map, new TradeOffer { gold = 1 }));
		us.knownTechs.Add(navigation.id);
		Assert.NotNull(Problem(map, new TradeOffer { gold = 1 }));
		them.knownTechs.Add(navigation.id);
		Assert.Null(Problem(map, new TradeOffer { gold = 1 }));
	}

	[Fact]
	public void TheWorldMapShowsEverythingWeKnow() {
		us.knownTechs.Add(navigation.id);
		them.knownTechs.Add(navigation.id);

		Assert.True(them.ExecuteDeal(gameData, us, new TradeOffer { worldMap = true }, new TradeOffer()));

		Assert.All(ourTerritory, t => Assert.True(them.tileKnowledge.isTileKnown(t)));
		Assert.All(explored, t => Assert.True(them.tileKnowledge.isTileKnown(t)));
	}

	// "-1 if you have done any recent trades (maps or techs)" (AI Attitude
	// Study).
	[Fact]
	public void AMapTradeIsARecentTrade() {
		us.knownTechs.Add(navigation.id);
		them.knownTechs.Add(navigation.id);
		gameData.turn = 12;

		Assert.True(them.ExecuteDeal(gameData, us, new TradeOffer { territoryMap = true }, new TradeOffer { gold = 10 }));

		Assert.Equal(12, us.playerRelationships[them.id].lastTechTradeTurn);
		Assert.Equal(12, them.playerRelationships[us.id].lastTechTradeTurn);
	}

	[Fact]
	public void TheTerritoryMapShowsOnlyOurTerritory() {
		us.knownTechs.Add(navigation.id);
		them.knownTechs.Add(navigation.id);

		Assert.True(them.ExecuteDeal(gameData, us, new TradeOffer { territoryMap = true }, new TradeOffer()));

		Assert.All(ourTerritory, t => Assert.True(them.tileKnowledge.isTileKnown(t)));
		Assert.All(explored, t => Assert.False(them.tileKnowledge.isTileKnown(t)));
	}

	[Fact]
	public void TheAIValuesAMapByWhatItHasNotSeen() {
		Assert.Equal(40 / TradeOffer.MapTilesPerGold, new TradeOffer { worldMap = true }.GoldEquivalentFor(gameData, them, counterpart: us));
		Assert.Equal(20 / TradeOffer.MapTilesPerGold, new TradeOffer { territoryMap = true }.GoldEquivalentFor(gameData, them, counterpart: us));
		foreach (Tile t in explored) {
			them.tileKnowledge.knownTiles.Add(t);
		}
		Assert.Equal(20 / TradeOffer.MapTilesPerGold, new TradeOffer { worldMap = true }.GoldEquivalentFor(gameData, them, counterpart: us));
	}

	[Fact]
	public void ContactsNeedPrintingPressAndAnEmbassy() {
		TradeOffer contact = new() { contacts = { stranger.id } };
		us.knownTechs.Add(printingPress.id);
		them.knownTechs.Add(printingPress.id);
		Assert.NotNull(Problem(contact, new TradeOffer()));

		us.playerRelationships[them.id].hasEmbassy = true;
		Assert.Null(Problem(contact, new TradeOffer()));
		Assert.Equal([stranger], TradeOffer.TradableContacts(gameData, us, them));
		// They can't introduce us to anyone we know.
		Assert.Empty(TradeOffer.TradableContacts(gameData, them, us));
		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, them, us, new TradeOffer { contacts = { stranger.id } }, new TradeOffer()));
	}

	[Fact]
	public void AContactIntroducesTheCivs() {
		us.knownTechs.Add(printingPress.id);
		them.knownTechs.Add(printingPress.id);
		us.playerRelationships[them.id].hasEmbassy = true;
		Assert.False(them.playerRelationships.ContainsKey(stranger.id));

		// The AI buys an introduction for a little gold.
		TradeOffer contact = new() { contacts = { stranger.id } };
		TradeOffer gold = new() { gold = TradeOffer.ContactGold };
		Assert.True(them.WouldAcceptDealFrom(gameData, us, contact, gold));
		Assert.True(them.ExecuteDeal(gameData, us, contact, gold));

		Assert.True(them.playerRelationships.ContainsKey(stranger.id));
		Assert.True(stranger.playerRelationships.ContainsKey(them.id));
		Assert.True(them.IsAtPeaceWith(stranger));
	}

	[Fact]
	public void MapsAndContactsAreNotPartOfAPeaceTreaty() {
		us.knownTechs.Add(navigation.id);
		them.knownTechs.Add(navigation.id);
		PlayerRelationship.DeclareWar(us, them, false, gameData.turn + 10);
		Assert.NotNull(Problem(new TradeOffer { partOfPeaceTreaty = true, worldMap = true }, new TradeOffer { partOfPeaceTreaty = true }));
	}
}
