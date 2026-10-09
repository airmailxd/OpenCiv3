using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;
using static C7GameData.PlayerRelationship;

namespace EngineTests.GameData;

// Cities as deal items, and how the AI makes peace.
public class CityTradeAndPeaceTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;

	public CityTradeAndPeaceTest(SaveGameFixture fixture) {
		this.fixture = fixture;
	}

	private static C7GameData.GameData Load(SaveGame save, SaveGameFixture fixture) {
		C7GameData.GameData gameData = save.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		return gameData;
	}

	private static bool IsEmptyLand(Tile t) {
		return t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp && !t.IsImpassable();
	}

	private static City BuildCity(C7GameData.GameData gameData, Player owner) {
		Tile tile = gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities()
			&& t.neighbors.Values.All(n => n == Tile.NONE || (!n.HasCity() && n.neighbors.Values.All(nn => nn == Tile.NONE || !nn.HasCity()))));
		return CityInteractions.BuildCity(tile, owner, $"{owner.civilization.noun} {owner.cities.Count}");
	}

	private static void RemoveAllUnits(C7GameData.GameData gameData, Player player) {
		foreach (MapUnit unit in player.units.ToList()) {
			gameData.RemoveUnit(unit);
		}
	}

	private static void SpawnArmy(C7GameData.GameData gameData, Player player, int count) {
		UnitPrototype warrior = gameData.unitPrototypes.Single(p => p.name == "Warrior");
		Tile tile = player.cities[0].location;
		for (int i = 0; i < count; ++i) {
			gameData.SpawnUnit(player, warrior, tile);
		}
	}

	private static void LetThemTalk(Player a, Player b) {
		a.playerRelationships[b.id].refuseContactUntilTurn = -1;
		b.playerRelationships[a.id].refuseContactUntilTurn = -1;
	}

	[Fact]
	public void HumansCanTradeCities() {
		C7GameData.GameData gameData = Load(SaveGameFixture.TwoHumanSave(), fixture);
		Player[] humans = gameData.players.Where(p => p.isHuman).ToArray();
		Player giver = humans[0], receiver = humans[1];
		giver.EnsureRelationshipExists(receiver);
		City capital = BuildCity(gameData, giver);
		City other = BuildCity(gameData, giver);
		BuildCity(gameData, receiver);
		Assert.True(capital.IsCapital());

		// Not the capital, nor the last city.
		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, giver, receiver, new TradeOffer { cities = [capital] }, new TradeOffer()));
		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, giver, receiver, new TradeOffer { cities = [capital, other] }, new TradeOffer()));
		// Nor someone else's.
		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, receiver, giver, new TradeOffer { cities = [other] }, new TradeOffer()));

		giver.gold = 0;
		receiver.gold = 100;
		TradeOffer gives = new() { cities = [other] };
		TradeOffer wants = new() { gold = 100 };
		Assert.Null(TradeOffer.ProblemWithDeal(gameData, giver, receiver, gives, wants));
		Assert.True(receiver.ExecuteDeal(gameData, giver, gives, wants));

		Assert.Same(receiver, other.owner);
		Assert.Contains(other, receiver.cities);
		Assert.DoesNotContain(other, giver.cities);
		Assert.Equal(100, giver.gold);
		// A city traded away doesn't resist.
		Assert.False(other.IsInResistance);
	}

	private static MapUnit Spawn(C7GameData.GameData gameData, Player owner, string prototype, Tile tile) {
		gameData.SpawnUnit(owner, gameData.unitPrototypes.Single(p => p.name == prototype), tile);
		return tile.unitsOnTile.Last();
	}

	// Per the project owner, the units of a city given away in a deal move
	// to their civ's nearest city.
	[Fact]
	public void UnitsInACityTradedAwayGoToTheNearestCity() {
		C7GameData.GameData gameData = Load(SaveGameFixture.TwoHumanSave(), fixture);
		Player[] humans = gameData.players.Where(p => p.isHuman).ToArray();
		Player giver = humans[0], receiver = humans[1];
		giver.EnsureRelationshipExists(receiver);
		BuildCity(gameData, giver);
		City traded = BuildCity(gameData, giver);
		BuildCity(gameData, giver);
		BuildCity(gameData, receiver);

		MapUnit warrior = Spawn(gameData, giver, "Warrior", traded.location);
		MapUnit worker = Spawn(gameData, giver, "Worker", traded.location);
		City nearest = warrior.FindNearestOwnCity(except: traded);
		Assert.NotNull(nearest);
		Assert.NotSame(traded, nearest);

		Assert.True(receiver.ExecuteDeal(gameData, giver, new TradeOffer { cities = [traded] }, new TradeOffer()));
		Assert.Same(receiver, traded.owner);
		Assert.Same(nearest.location, warrior.location);
		Assert.Same(giver, warrior.owner);
		Assert.Contains(worker.location, giver.cities.Select(c => c.location));
		Assert.Same(giver, worker.owner);
		Assert.DoesNotContain(traded.location.unitsOnTile, u => u.owner == giver);
	}

	// With no other city to go to, they leave for the nearest free tile,
	// or are lost (UNVERIFIED).
	[Fact]
	public void UnitsOfACivWithNoOtherCityLeaveForAFreeTile() {
		C7GameData.GameData gameData = Load(SaveGameFixture.TwoHumanSave(), fixture);
		Player[] humans = gameData.players.Where(p => p.isHuman).ToArray();
		Player giver = humans[0], receiver = humans[1];
		giver.EnsureRelationshipExists(receiver);
		City traded = BuildCity(gameData, giver);
		BuildCity(gameData, receiver);
		MapUnit warrior = Spawn(gameData, giver, "Warrior", traded.location);
		Assert.Null(warrior.FindNearestOwnCity(except: traded));

		CityInteractions.TransferCity(traded, receiver, viaDeal: true);
		Assert.DoesNotContain(warrior, traded.location.unitsOnTile);
		if (giver.units.Contains(warrior)) {
			Assert.NotSame(receiver, warrior.location.OwningPlayer());
			Assert.False(warrior.location.HasCity());
		}
	}

	private (C7GameData.GameData gameData, Player ai, Player human) AIAndHuman() {
		C7GameData.GameData gameData = Load(fixture.saveGame, fixture);
		Player human = gameData.players.First(p => p.isHuman);
		Player ai = gameData.players.First(p => !p.isHuman && !p.isBarbarians);
		human.EnsureRelationshipExists(ai);
		BuildCity(gameData, ai);
		BuildCity(gameData, ai);
		BuildCity(gameData, ai);
		BuildCity(gameData, human);
		return (gameData, ai, human);
	}

	[Fact]
	public void TheAIWontGiveACityAwayInPeacetime() {
		(C7GameData.GameData gameData, Player ai, Player human) = AIAndHuman();
		City city = ai.cities.First(c => !c.IsCapital());
		human.gold = 100000;

		TradeOffer humanGives = new() { gold = 100000 };
		TradeOffer aiGives = new() { cities = [city] };
		Assert.Null(TradeOffer.ProblemWithDeal(gameData, human, ai, humanGives, aiGives));
		Assert.False(ai.WouldAcceptDealFrom(gameData, human, humanGives, aiGives));
	}

	[Fact]
	public void TheAIGivesNothingForACity() {
		(C7GameData.GameData gameData, Player ai, Player human) = AIAndHuman();
		BuildCity(gameData, human);
		City humanCity = human.cities.First(c => !c.IsCapital());
		TradeOffer cityOffer = new() { cities = [humanCity] };
		Assert.Equal(0, cityOffer.GoldEquivalentFor(gameData, ai));

		// Not even a little gold.
		ai.gold = 100;
		TradeOffer aiGives = new() { gold = 1 };
		Assert.False(ai.WouldAcceptDealFrom(gameData, human, cityOffer, aiGives));
	}

	[Fact]
	public void ALosingAICedesACityForPeace() {
		(C7GameData.GameData gameData, Player ai, Player human) = AIAndHuman();
		human.DeclareWarOn(ai, gameData.turn);
		LetThemTalk(ai, human);
		RemoveAllUnits(gameData, ai);
		SpawnArmy(gameData, human, 20);
		Assert.Equal(PeaceAI.WarOutlook.LosingBadly, PeaceAI.Assess(ai, human));

		(TradeOffer weGive, TradeOffer weWant)? offer = PeaceAI.BuildPeaceOffer(gameData, ai, human);
		Assert.NotNull(offer);
		(TradeOffer aiGives, TradeOffer aiWants) = offer.Value;
		City ceded = Assert.Single(aiGives.cities);
		Assert.False(ceded.IsCapital());
		Assert.True(aiGives.partOfPeaceTreaty);

		// The human accepts by proposing the same deal back.
		Assert.True(ai.WouldAcceptDealFrom(gameData, human, aiWants, aiGives));
		Assert.True(ai.ExecuteDeal(gameData, human, aiWants, aiGives));

		Assert.False(AtWar(ai, human));
		Assert.Same(human, ceded.owner);
		Assert.False(ceded.IsInResistance);
	}

	[Fact]
	public void ALosingAIWontCedeMoreThanItMust() {
		(C7GameData.GameData gameData, Player ai, Player human) = AIAndHuman();
		human.DeclareWarOn(ai, gameData.turn);
		LetThemTalk(ai, human);
		RemoveAllUnits(gameData, ai);
		SpawnArmy(gameData, human, 20);

		TradeOffer peace = new() { partOfPeaceTreaty = true };
		TradeOffer twoCities = new() { partOfPeaceTreaty = true, cities = ai.cities.Where(c => !c.IsCapital()).Take(2).ToList() };
		Assert.False(ai.WouldAcceptDealFrom(gameData, human, peace, twoCities));
	}

	[Fact]
	public void ADominatingAIDemandsACityForPeace() {
		(C7GameData.GameData gameData, Player ai, Player human) = AIAndHuman();
		BuildCity(gameData, human);
		ai.DeclareWarOn(human, gameData.turn);
		LetThemTalk(ai, human);
		RemoveAllUnits(gameData, human);
		SpawnArmy(gameData, ai, 20);
		Assert.Equal(PeaceAI.WarOutlook.Dominating, PeaceAI.Assess(ai, human));

		// Peace for nothing won't do.
		TradeOffer peace = new() { partOfPeaceTreaty = true };
		Assert.False(ai.WouldAcceptDealFrom(gameData, human, peace, new TradeOffer { partOfPeaceTreaty = true }));

		(TradeOffer weGive, TradeOffer weWant)? offer = PeaceAI.BuildPeaceOffer(gameData, ai, human);
		Assert.NotNull(offer);
		City demanded = Assert.Single(offer.Value.weWant.cities);
		Assert.Same(human, demanded.owner);
		Assert.True(ai.WouldAcceptDealFrom(gameData, human, offer.Value.weWant, offer.Value.weGive));
	}

	[Fact]
	public void AnEvenWarGetsNoPeaceProposal() {
		(C7GameData.GameData gameData, Player ai, Player human) = AIAndHuman();
		human.DeclareWarOn(ai, gameData.turn);
		RemoveAllUnits(gameData, ai);
		RemoveAllUnits(gameData, human);
		Assert.Equal(PeaceAI.WarOutlook.Even, PeaceAI.Assess(ai, human));
		Assert.Null(PeaceAI.BuildPeaceOffer(gameData, ai, human));
	}
}
