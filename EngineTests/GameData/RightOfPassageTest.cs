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

	[Fact]
	public void ARightOfPassageNeedsAnEmbassyEitherWay() {
		Assert.NotNull(RightOfPassage.ProblemSigning(gameData, us, third));
		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, us, third, Passage(), Passage()));

		// Their embassy with us does as well as ours with them.
		GiveEmbassy(third, us);
		Assert.Null(RightOfPassage.ProblemSigning(gameData, us, third));
		Assert.Null(TradeOffer.ProblemWithDeal(gameData, us, third, Passage(), Passage()));
	}

	[Fact]
	public void ARightOfPassageNeedsMapMakingWhereTheRulesHaveIt() {
		GiveEmbassy(us, third);
		Tech mapMaking = new() { id = ID.FromString("tech-15"), Name = "Map Making", EnablesRightOfPassage = true };
		gameData.techs.Add(mapMaking);

		Assert.NotNull(RightOfPassage.ProblemSigning(gameData, us, third));
		us.knownTechs.Add(mapMaking.id);
		Assert.NotNull(RightOfPassage.ProblemSigning(gameData, us, third));
		third.knownTechs.Add(mapMaking.id);
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

		// No war: only the sizes count.
		Assert.Equal(RightOfPassageAI.GoldPerCity * 4, RightOfPassageAI.Worth(gameData, us, third));
		Assert.Equal(-RightOfPassageAI.GoldPerCity * 4, RightOfPassageAI.Worth(gameData, third, us));

		GiveEmbassy(us, third);
		// The third civ, an AI, wants paying for letting a smaller civ in.
		Assert.False(third.WouldAcceptDealFrom(gameData, us, Passage(), Passage()));
		us.gold = 100;
		TradeOffer paid = new() { rightOfPassage = true, gold = RightOfPassageAI.GoldPerCity * 4 };
		Assert.True(third.WouldAcceptDealFrom(gameData, us, paid, Passage()));
		// And we, the smaller, take one for nothing.
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
		us.gold = 100;
		us.DeclareWarOn(enemy, gameData.turn);

		await RightOfPassageAI.PlayTurn(us, gameData);

		Assert.False(HaveActiveRightOfPassage(us, third));
	}

	[Fact]
	public async Task AnAiAtPeaceDoesNotAskForOne() {
		MakeUnit(us, ourCity.location);
		GiveEmbassy(us, third);
		us.gold = 100;

		await RightOfPassageAI.PlayTurn(us, gameData);

		Assert.False(HaveActiveRightOfPassage(us, third));
	}

	[Fact]
	public void AnAiDoesNotLetAMuchStrongerNeighbourIn() {
		GiveEmbassy(us, third);
		for (int i = 0; i < 5; ++i) {
			MakeUnit(us, ourCity.location, attack: 4, defense: 2);
		}
		Assert.True(RightOfPassageAI.IsThreat(third, us));
		// Not the other way round, nor when it's far off.
		Assert.False(RightOfPassageAI.IsThreat(us, third));
		ourCity.location.cityAtTile = null;
		us.cities.Clear();
		AddCities(us, 1);
		Assert.False(RightOfPassageAI.IsThreat(third, us));
		ourCity.location.cityAtTile = ourCity;
		us.cities.Add(ourCity);
		Assert.False(third.WouldAcceptDealFrom(gameData, us, new TradeOffer { rightOfPassage = true }, Passage()));
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
