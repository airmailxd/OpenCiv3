using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using EngineTests.AI;
using EngineTests.Utils;
using Xunit;
using static C7GameData.PlayerRelationship;

namespace EngineTests.GameData;

// Rights of passage: the rules (see RightOfPassage) and how the AI deals in
// them (see RightOfPassageAI).
//
// The map is a field of plains, 20 tiles across. We are in the west, the
// enemy in the east, and a third civ's land runs from north to south in
// between, so every way from us to the enemy crosses it.
public class RightOfPassageTest : MapBase {
	private static readonly TerrainImprovement testRoad = new("road", TerrainImprovement.Layer.Roads, movementCost: 1.0f / 3);

	private readonly GameMap map;
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player enemy;
	private readonly Player third;
	private readonly City ourCity;
	private readonly City thirdCity;
	private readonly City enemyCity;

	public RightOfPassageTest() {
		map = FixAiPathingTests.MakeMap(20, 20);
		gameData = EngineStorage.gameData;
		gameData.map = map;
		gameData.rules = new Rules() { DefaultDealDuration = 20 };
		gameData.gameDifficulty = new Difficulty();
		EngineStorage.animationsEnabled = false;

		us = MakeCiv("player-1");
		enemy = MakeCiv("player-2");
		third = MakeCiv("player-3");
		foreach (Player a in gameData.players) {
			foreach (Player b in gameData.players) {
				if (a != b) {
					a.EnsureRelationshipExists(b);
				}
			}
		}

		ourCity = MakeCity(us, map.tileAt(2, 10));
		enemyCity = MakeCity(enemy, map.tileAt(18, 10));
		thirdCity = MakeCity(third, map.tileAt(10, 10));
		foreach (Tile t in map.tiles.Where(t => t.XCoordinate >= 7 && t.XCoordinate <= 13)) {
			t.owningCity = thirdCity;
		}
		foreach (Player p in gameData.players) {
			foreach (Tile t in map.tiles) {
				p.tileKnowledge.knownTiles.Add(t);
			}
		}
	}

	private Player MakeCiv(string id) {
		Player player = new() {
			id = ID.FromString(id),
			civilization = new Civilization(id),
			government = new Government(),
			rules = gameData.rules,
		};
		gameData.players.Add(player);
		return player;
	}

	private static City MakeCity(Player owner, Tile tile) {
		City city = new(tile, owner, $"{owner.id} city {owner.cities.Count}", ID.None("city"));
		tile.cityAtTile = city;
		owner.cities.Add(city);
		return city;
	}

	// More cities for the player, off the map's land, so they count
	// towards its size without changing the ways across it.
	private static void AddCities(Player owner, int count) {
		for (int i = 0; i < count; ++i) {
			Tile tile = new(ID.None("tile")) { continent = 99 };
			City city = new(tile, owner, $"{owner.id} extra {i}", ID.None("city"));
			tile.cityAtTile = city;
			owner.cities.Add(city);
		}
	}

	private static MapUnit MakeUnit(Player owner, Tile location, int attack = 1, int defense = 1) {
		MapUnit unit = new(ID.None("unit")) {
			owner = owner,
			unitType = new UnitPrototype() { attack = attack, defense = defense, movement = 1 },
			experienceLevel = new ExperienceLevel("regular", "Regular", 3, 0, 0),
			location = location,
		};
		unit.unitType.categories.Add("Land");
		location.unitsOnTile.Add(unit);
		owner.units.Add(unit);
		return unit;
	}

	private void GiveEmbassy(Player from, Player to) {
		from.playerRelationships[to.id].hasEmbassy = true;
	}

	private static TradeOffer Passage() => new() { rightOfPassage = true };

	// The rules

	// The proposer needs an embassy with the other civ; theirs with the
	// proposer doesn't do (owner's decision, 2026-10-11).
	[Fact]
	public void TheProposerNeedsAnEmbassy() {
		Assert.NotNull(RightOfPassage.ProblemSigning(gameData, us, third));
		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, us, third, Passage(), Passage()));

		GiveEmbassy(third, us);
		Assert.NotNull(RightOfPassage.ProblemSigning(gameData, us, third));
		Assert.Null(RightOfPassage.ProblemSigning(gameData, third, us));

		GiveEmbassy(us, third);
		Assert.Null(RightOfPassage.ProblemSigning(gameData, us, third));
		Assert.Null(TradeOffer.ProblemWithDeal(gameData, us, third, Passage(), Passage()));
	}

	// The proposer needs Writing, the other civ doesn't, and Map Making
	// isn't needed (owner's decision, 2026-10-11).
	[Fact]
	public void TheProposerNeedsWriting() {
		GiveEmbassy(us, third);
		Tech writing = new() { id = ID.FromString("tech-99"), Name = "Writing" };
		Tech mapMaking = new() { id = ID.FromString("tech-15"), Name = "Map Making", EnablesRightOfPassage = true };
		gameData.techs.Add(writing);
		gameData.techs.Add(mapMaking);

		Assert.NotNull(RightOfPassage.ProblemSigning(gameData, us, third));
		us.knownTechs.Add(writing.id);
		Assert.Null(RightOfPassage.ProblemSigning(gameData, us, third));
	}

	[Fact]
	public void NoRightOfPassageAtWarNorInAPeaceTreaty() {
		GiveEmbassy(us, enemy);
		us.DeclareWarOn(enemy, gameData.turn);

		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, us, enemy, Passage(), Passage()));
		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, us, enemy,
			new TradeOffer { partOfPeaceTreaty = true, rightOfPassage = true }, new TradeOffer { partOfPeaceTreaty = true }));
	}

	[Fact]
	public void ADealSignsARightOfPassageForTheDealLengthAndItExpires() {
		GiveEmbassy(us, third);
		third.isHuman = true;
		us.isHuman = true;
		gameData.turn = 30;

		Assert.True(third.ExecuteDeal(gameData, us, Passage(), new TradeOffer()));

		Assert.True(HaveActiveRightOfPassage(us, third));
		Assert.True(HaveActiveRightOfPassage(third, us));
		MultiTurnDeal deal = us.playerRelationships[third.id].multiTurnDeals.Single(d => d.dealSubType == DealSubType.RightOfPassage);
		Assert.Equal(DealDetails.Exchange, deal.dealDetails);
		Assert.Equal(50, deal.turnEndDeal);
		// Already in force: no second one.
		Assert.NotNull(RightOfPassage.ProblemSigning(gameData, us, third));

		CheckForObsoleteDeals(us, gameData.players, 49);
		Assert.True(HaveActiveRightOfPassage(us, third));
		CheckForObsoleteDeals(us, gameData.players, 50);
		Assert.False(HaveActiveRightOfPassage(us, third));
		Assert.False(HaveActiveRightOfPassage(third, us));
		Assert.True(AtPeace(us, third));
	}

	[Fact]
	public void RoadsInAnotherCivsLandNeedARightOfPassage() {
		Tile a = map.tileAt(8, 10);
		Tile b = map.tileAt(10, 10).neighbors[TileDirection.WEST];
		Assert.Same(a, b);
		Tile c = map.tileAt(10, 10);
		a.overlays.Add(testRoad);
		c.overlays.Add(testRoad);

		Assert.False(Player.CanMoveFreely(us, a, c));
		Assert.Equal(1f, TilePath.GetMovementCost(us, a, TileDirection.EAST, c));

		RightOfPassage.Sign(gameData, us, third);
		Assert.True(Player.CanMoveFreely(us, a, c));
		Assert.Equal(1f / 3, TilePath.GetMovementCost(us, a, TileDirection.EAST, c), 5);
	}

	[Fact]
	public void ARightOfPassageDoesNotOpenAThirdCivsRoads() {
		// A road from the third civ's land into the enemy's.
		Tile from = map.tileAt(12, 10);
		Tile to = map.tileAt(14, 10);
		to.owningCity = enemyCity;
		from.overlays.Add(testRoad);
		to.overlays.Add(testRoad);
		RightOfPassage.Sign(gameData, us, third);

		Assert.False(Player.CanMoveFreely(us, from, to));
		Assert.False(Player.CanMoveFreely(us, to, from));
	}

	[Fact]
	public void DeclaringWarWithUnitsInsideIsABetrayalThatEndsTheRightOfPassage() {
		RightOfPassage.Sign(gameData, us, third);
		MapUnit inside = MakeUnit(us, map.tileAt(10, 12));
		third.tileKnowledge.knownTiles.Add(inside.location);

		us.DeclareWarOn(third, gameData.turn);

		Assert.False(HaveActiveRightOfPassage(us, third));
		PlayerRelationship theirView = third.playerRelationships[us.id];
		Assert.True(theirView.wasSneakAttacked);
		Assert.Equal(1, theirView.warDeclarationWithRoPActiveCount);
		Assert.Equal(1, theirView.rightOfPassageBetrayals);
		// The units stay where they were.
		Assert.Same(map.tileAt(10, 12), inside.location);
		Assert.True(RightOfPassage.HasBetrayed(us, gameData.players));
		// And nobody signs one with us again.
		Assert.True(RightOfPassageAI.Refuses(gameData, enemy, us));
	}

	// "having a Scout/worker/any other unit in enemy territory when
	// declaring war is enough to be a ROP-breaker", with or without a right
	// of passage (CivFanatics, "Reputation for sneak attacks").
	[Fact]
	public void ASneakAttackWithoutARightOfPassageIsABetrayalToo() {
		Assert.False(HaveActiveRightOfPassage(us, third));
		MapUnit inside = MakeUnit(us, map.tileAt(10, 12));
		third.tileKnowledge.knownTiles.Add(inside.location);
		int before = Attitudes.Points(gameData, third, us).Value;

		us.DeclareWarOn(third, gameData.turn);

		PlayerRelationship theirView = third.playerRelationships[us.id];
		Assert.True(theirView.wasSneakAttacked);
		Assert.Equal(1, theirView.rightOfPassageBetrayals);
		Assert.True(RightOfPassage.HasBetrayed(us, gameData.players));
		Assert.True(RightOfPassageAI.Refuses(gameData, enemy, us));
		// The study's +4 for declaring war and +6 for breaking a right of
		// passage with units inside.
		Assert.Equal(before + 4 + 6, Attitudes.Points(gameData, third, us).Value);
	}

	[Fact]
	public void DeclaringWarWithNoUnitsInsideIsNoBetrayal() {
		RightOfPassage.Sign(gameData, us, third);

		us.DeclareWarOn(third, gameData.turn);

		Assert.Equal(1, third.playerRelationships[us.id].warDeclarationWithRoPActiveCount);
		Assert.Equal(0, third.playerRelationships[us.id].rightOfPassageBetrayals);
		Assert.False(RightOfPassage.HasBetrayed(us, gameData.players));
	}

	// The AI

	[Fact]
	public void TheBiggerEmpireGivesMoreSoTheSmallerPays() {
		AddCities(third, 4);
		us.gold = 100;

		// No war: only the sizes count.
		Assert.Equal(RightOfPassageAI.GoldPerCity * 4, RightOfPassageAI.Worth(gameData, us, third));
		Assert.Equal(-RightOfPassageAI.GoldPerCity * 4, RightOfPassageAI.Worth(gameData, third, us));

		GiveEmbassy(us, third);
		// The third civ, an AI, wants paying for letting a smaller civ in.
		Assert.False(third.WouldAcceptDealFrom(gameData, us, Passage(), Passage()));
		TradeOffer paid = new() { rightOfPassage = true, gold = RightOfPassageAI.GoldPerCity * 4 };
		Assert.True(third.WouldAcceptDealFrom(gameData, us, paid, Passage()));
		// And we, the smaller, take one for nothing (when they propose it,
		// they need their embassy).
		GiveEmbassy(third, us);
		Assert.True(us.WouldAcceptDealFrom(gameData, third, Passage(), Passage()));
	}

	[Fact]
	public void TheAiFindsTheWayToItsEnemyCrossesAThirdCivsLand() {
		MakeUnit(us, ourCity.location);
		Assert.Empty(RightOfPassageAI.TerritoryOnWarRoutes(gameData, us));

		gameData.turn++;
		us.DeclareWarOn(enemy, gameData.turn);
		Dictionary<Player, int> crossed = RightOfPassageAI.TerritoryOnWarRoutes(gameData, us);

		Assert.True(crossed.GetValueOrDefault(third) >= RightOfPassageAI.MinTilesCrossed);
		Assert.False(crossed.ContainsKey(enemy));
		Assert.True(RightOfPassageAI.Need(gameData, us, third) > 0);
		Assert.Equal(0, RightOfPassageAI.Need(gameData, third, us));
	}

	[Fact]
	public async Task AnAiAtWarBuysARightOfPassageThroughTheCivInTheWay() {
		MakeUnit(us, ourCity.location);
		// Defended well enough not to fear us.
		MakeUnit(third, thirdCity.location, attack: 1, defense: 2);
		AddCities(third, 2);
		GiveEmbassy(us, third);
		us.gold = 100;
		us.DeclareWarOn(enemy, gameData.turn);

		await RightOfPassageAI.PlayTurn(us, gameData);

		Assert.True(HaveActiveRightOfPassage(us, third));
		// The third civ is two cities bigger, and is paid for it.
		Assert.Equal(100 - 2 * RightOfPassageAI.GoldPerCity, us.gold);
		Assert.Equal(2 * RightOfPassageAI.GoldPerCity, third.gold);
	}

	[Fact]
	public async Task NoRightOfPassageWithoutAnEmbassy() {
		MakeUnit(us, ourCity.location);
		MakeUnit(third, thirdCity.location, attack: 1, defense: 2);
		// Too little gold to establish one.
		us.gold = 20;
		us.DeclareWarOn(enemy, gameData.turn);

		await RightOfPassageAI.PlayTurn(us, gameData);

		Assert.False(us.playerRelationships[third.id].hasEmbassy);
		Assert.False(HaveActiveRightOfPassage(us, third));
	}

	[Fact]
	public async Task AnAiEstablishesTheEmbassyItNeedsToAskForOne() {
		MakeUnit(us, ourCity.location);
		MakeUnit(third, thirdCity.location, attack: 1, defense: 2);
		us.gold = 100;
		us.DeclareWarOn(enemy, gameData.turn);

		await RightOfPassageAI.PlayTurn(us, gameData);

		Assert.True(us.playerRelationships[third.id].hasEmbassy);
		Assert.True(HaveActiveRightOfPassage(us, third));
	}

	[Fact]
	public async Task AnAiAtPeaceDoesNotAskForOne() {
		MakeUnit(us, ourCity.location);
		GiveEmbassy(us, third);
		us.gold = 100;

		await RightOfPassageAI.PlayTurn(us, gameData);

		Assert.False(HaveActiveRightOfPassage(us, third));
	}

	// The size part is paid only as far as the payer's treasury goes
	// (owner's decision, 2026-10-12).
	[Fact]
	public void TheSizePaymentIsCappedByThePayersTreasury() {
		AddCities(third, 4);
		us.gold = 7;

		Assert.Equal(RightOfPassageAI.GoldPerCity * 4, RightOfPassageAI.SizeGold(us, third));
		Assert.Equal(7, RightOfPassageAI.SizePayment(us, third));
		Assert.Equal(0, RightOfPassageAI.SizePayment(third, us));
		Assert.Equal(-7, RightOfPassageAI.Worth(gameData, third, us));
	}

	// An AI "should ask for gold if their empire is bigger, or offer gold
	// if smaller" (owner's decision, 2026-10-12).
	[Fact]
	public void ABiggerAiAsksAHumanForGold() {
		AddCities(us, 3);
		third.isHuman = true;
		third.gold = 100;
		GiveEmbassy(us, third);
		GiveEmbassy(third, us);

		(TradeOffer weGive, TradeOffer weWant) = RightOfPassageAI.MakeOffer(gameData, us, third);

		Assert.Null(weGive.gold);
		Assert.Equal(RightOfPassageAI.GoldPerCity * 3, weWant.gold);
		// The human agreeing proposes it back, and we accept it, but not
		// without the gold.
		Assert.True(us.WouldAcceptDealFrom(gameData, third, weWant, weGive));
		Assert.False(us.WouldAcceptDealFrom(gameData, third, Passage(), Passage()));
	}

	[Fact]
	public void ABiggerAiAsksOnlyWhatThePartnerHas() {
		AddCities(us, 3);
		third.isHuman = true;
		third.gold = 6;
		GiveEmbassy(us, third);
		GiveEmbassy(third, us);

		(TradeOffer weGive, TradeOffer weWant) = RightOfPassageAI.MakeOffer(gameData, us, third);

		Assert.Equal(6, weWant.gold);
		Assert.True(us.WouldAcceptDealFrom(gameData, third, weWant, weGive));
	}

	[Fact]
	public void ASmallerAiOffersAHumanGold() {
		AddCities(third, 2);
		third.isHuman = true;
		us.gold = 100;
		GiveEmbassy(us, third);
		GiveEmbassy(third, us);

		(TradeOffer weGive, TradeOffer weWant) = RightOfPassageAI.MakeOffer(gameData, us, third);

		Assert.Equal(RightOfPassageAI.GoldPerCity * 2, weGive.gold);
		Assert.Null(weWant.gold);
		Assert.True(us.WouldAcceptDealFrom(gameData, third, weWant, weGive));
	}

	[Fact]
	public void EqualEmpiresTradeNoGold() {
		third.isHuman = true;
		us.gold = 100;
		third.gold = 100;
		GiveEmbassy(us, third);

		(TradeOffer weGive, TradeOffer weWant) = RightOfPassageAI.MakeOffer(gameData, us, third);

		Assert.Null(weGive.gold);
		Assert.Null(weWant.gold);
	}

	// Offered one by a bigger civ, a smaller AI pays up to the size part.
	[Fact]
	public void ASmallerAiPaysABiggerCivUpToTheSizePart() {
		AddCities(third, 2);
		us.gold = 100;
		GiveEmbassy(third, us);

		Assert.True(us.WouldAcceptDealFrom(gameData, third, Passage(),
			new TradeOffer { rightOfPassage = true, gold = RightOfPassageAI.GoldPerCity * 2 }));
		Assert.False(us.WouldAcceptDealFrom(gameData, third, Passage(),
			new TradeOffer { rightOfPassage = true, gold = RightOfPassageAI.GoldPerCity * 2 + 1 }));
	}

	// Now and then an AI offers a right of passage in friendship (owner's
	// decision, 2026-10-11); a smaller civ, which gets more out of it,
	// signs, and pays the bigger for it (owner's decision, 2026-10-12).
	[Fact]
	public async Task AnAiOffersARightOfPassageInFriendship() {
		AddCities(us, 3);
		GiveEmbassy(us, third);
		GiveEmbassy(us, enemy);
		third.gold = 50;
		enemy.gold = 50;

		await RightOfPassageAI.OfferFriendlyPassage(us, gameData);

		// The third civ is the nearest.
		Assert.True(HaveActiveRightOfPassage(us, third));
		Assert.Equal(RightOfPassageAI.GoldPerCity * 3, us.gold);
		Assert.Equal(50 - RightOfPassageAI.GoldPerCity * 3, third.gold);
	}

	// A smaller AI offers a bigger one gold with it.
	[Fact]
	public async Task ASmallerAiOffersGoldInFriendship() {
		AddCities(third, 3);
		AddCities(enemy, 3);
		GiveEmbassy(us, third);
		GiveEmbassy(us, enemy);
		us.gold = 100;

		await RightOfPassageAI.OfferFriendlyPassage(us, gameData);

		Assert.True(HaveActiveRightOfPassage(us, third));
		Assert.Equal(100 - RightOfPassageAI.GoldPerCity * 3, us.gold);
		Assert.Equal(RightOfPassageAI.GoldPerCity * 3, third.gold);
	}

	// A much stronger neighbour isn't refused for its strength (owner's
	// decision, 2026-10-11).
	[Fact]
	public void AnAiDoesNotRefuseAMuchStrongerNeighbour() {
		GiveEmbassy(us, third);
		for (int i = 0; i < 5; ++i) {
			MakeUnit(us, ourCity.location, attack: 4, defense: 2);
		}
		Assert.False(RightOfPassageAI.Refuses(gameData, third, us));
	}

	[Fact]
	public void AnAiDoesNotOpenTheWayToItsAlly() {
		GiveEmbassy(us, third);
		RegisterMultiTurnDeal(third, enemy, new MultiTurnDeal(DealType.DiplomaticAgreement,
			DealSubType.MutualProtectionPact, DealDetails.Exchange));
		us.DeclareWarOn(enemy, gameData.turn);

		Assert.True(RightOfPassageAI.Refuses(gameData, third, us));
	}
}
