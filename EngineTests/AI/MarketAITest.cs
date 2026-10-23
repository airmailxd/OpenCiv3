using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using EngineTests.GameData;
using Xunit;

namespace EngineTests.AI;

// How the AI bids in Eternal War's Mercenary Market and where it puts what
// it wins (see MarketAI).
public class MarketAITest {
	private static (C7GameData.GameData, Player) NewGame(int seed) {
		(_, C7GameData.GameData gameData) = EternalWarScenarioTest.NewGame(seed);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player ai = gameData.players.First(p => !p.isHuman && !p.isBarbarians && p.cities.Count >= 2);
		return (gameData, ai);
	}

	private static UnitPrototype Unit(C7GameData.GameData gameData, string name) {
		return gameData.unitPrototypes.First(u => u.name == name);
	}

	private static List<Player> Rivals(C7GameData.GameData gameData, Player ai) {
		return gameData.players.Where(p => p != ai && !p.isBarbarians).ToList();
	}

	// The bids come to no more than the treasury less the reserve, and a
	// civ with no gold bids nothing.
	[Fact]
	public void BidsKeepAReserve() {
		(C7GameData.GameData gameData, Player ai) = NewGame(21);
		List<UnitPrototype> offers = [Unit(gameData, "Knight"), Unit(gameData, "Pikeman"), Unit(gameData, "Swordsman")];
		foreach (Player p in Rivals(gameData, ai)) {
			p.gold = 1000;
		}
		ai.gold = 60;
		List<int> bids = MarketAI.Bids(gameData, ai, offers);
		Assert.Equal(offers.Count, bids.Count);
		Assert.Contains(bids, b => b > 0);
		Assert.InRange(bids.Sum(), 1, ai.gold - MarketAI.Reserve(MarketAI.Assess(gameData, ai), 0));

		ai.gold = 0;
		Assert.All(MarketAI.Bids(gameData, ai, offers), b => Assert.Equal(0, b));
	}

	// The AI can't see its rivals' gold: what it bids doesn't depend on
	// their treasuries, only on what it guesses from their cities.
	[Fact]
	public void DoesNotPeekAtRivalsGold() {
		(C7GameData.GameData gameData, Player ai) = NewGame(22);
		ai.gold = 1000;
		List<UnitPrototype> offers = [Unit(gameData, "Knight"), Unit(gameData, "Pikeman"), Unit(gameData, "Musketman")];
		List<int> Bid(int rivalGold) {
			foreach (Player p in Rivals(gameData, ai)) {
				p.gold = rivalGold;
			}
			System.Random original = C7GameData.GameData.rng;
			C7GameData.GameData.rng = new System.Random(5);
			try {
				return MarketAI.Bids(gameData, ai, offers);
			} finally {
				C7GameData.GameData.rng = original;
			}
		}
		Assert.Equal(Bid(3), Bid(5000));
		Player rival = Rivals(gameData, ai).First();
		Assert.Equal((int)System.Math.Ceiling(1000.0 / ai.cities.Count * rival.cities.Count * 1.5), MarketAI.EstimatedGold(ai, rival));
	}

	// A defender is worth more to a civ whose cities are bare than to one
	// whose cities are well held.
	[Fact]
	public void BareCitiesWantDefendersMost() {
		(C7GameData.GameData gameData, Player ai) = NewGame(23);
		UnitPrototype pikeman = Unit(gameData, "Pikeman");
		foreach (MapUnit u in ai.units.Where(u => u.unitType.defense >= u.unitType.attack && !u.unitType.isWorker).ToList()) {
			gameData.RemoveUnit(u);
		}
		int bare = MarketAI.Worth(gameData, MarketAI.Assess(gameData, ai), pikeman);
		foreach (City city in ai.cities) {
			for (int i = 0; i < 4; ++i) {
				gameData.SpawnUnit(ai, pikeman, city.location);
			}
		}
		int held = MarketAI.Worth(gameData, MarketAI.Assess(gameData, ai), pikeman);
		Assert.True(bare > held, $"bare {bare} held {held}");
	}

	// A weak unit is worth less than a strong one of its kind, and a ship
	// little, and less still to a civ with no coast.
	[Fact]
	public void WeakUnitsAndShipsAreWorthLess() {
		(C7GameData.GameData gameData, Player ai) = NewGame(24);
		MarketAI.Position p = MarketAI.Assess(gameData, ai);
		Assert.True(MarketAI.Worth(gameData, p, Unit(gameData, "Warrior")) < MarketAI.Worth(gameData, p, Unit(gameData, "Swordsman")));
		Assert.True(MarketAI.Worth(gameData, p, Unit(gameData, "Spearman")) < MarketAI.Worth(gameData, p, Unit(gameData, "Musketman")));

		UnitPrototype frigate = Unit(gameData, "Frigate");
		UnitPrototype knight = Unit(gameData, "Knight");
		Assert.True(MarketAI.Worth(gameData, p, frigate) * knight.shieldCost < MarketAI.Worth(gameData, p, knight) * frigate.shieldCost);
		p.coastal = true;
		int coastal = MarketAI.Worth(gameData, p, frigate);
		p.coastal = false;
		Assert.True(MarketAI.Worth(gameData, p, frigate) < coastal);
	}

	// The AI learns the going price from the market: up after it's outbid,
	// down after it wins, and down when a unit goes unsold.
	[Fact]
	public void LearnsTheGoingPrice() {
		(C7GameData.GameData gameData, Player ai) = NewGame(25);
		Player rival = Rivals(gameData, ai).First();
		UnitPrototype knight = Unit(gameData, "Knight");
		StagingState state = new() { offers = [knight.name] };
		double start = MarketAI.PricePerShield(ai);

		state.bids[ai.id.ToString()] = [(int)(start * knight.shieldCost)];
		MarketAI.Learn(gameData, state, [new MarketResult { unitType = knight.name, winners = [rival] }]);
		double outbid = MarketAI.PricePerShield(ai);
		Assert.True(outbid > start);

		state.bids[ai.id.ToString()] = [(int)(outbid * knight.shieldCost)];
		MarketAI.Learn(gameData, state, [new MarketResult { unitType = knight.name, winners = [ai] }]);
		double won = MarketAI.PricePerShield(ai);
		Assert.True(won < outbid);

		state.bids[ai.id.ToString()] = [0];
		MarketAI.Learn(gameData, state, [new MarketResult { unitType = knight.name }]);
		Assert.True(MarketAI.PricePerShield(ai) < won);
	}

	// A defender goes to the city with the fewest defenders, and a ship to
	// a coastal city.
	[Fact]
	public void PlacesDefendersWhereTheyreShortAndShipsOnTheCoast() {
		(C7GameData.GameData gameData, Player ai) = NewGame(15);
		UnitPrototype pikeman = Unit(gameData, "Pikeman");
		City bare = ai.cities.Last();
		foreach (MapUnit u in bare.location.unitsOnTile.Where(u => u.owner == ai && u.unitType.defense > 0).ToList()) {
			gameData.RemoveUnit(u);
		}
		foreach (City city in ai.cities.Where(c => c != bare)) {
			for (int i = 0; i < 3; ++i) {
				gameData.SpawnUnit(ai, pikeman, city.location);
			}
		}
		Assert.Equal(bare, MarketAI.Choice(gameData, ai, pikeman, ai.cities.ToList()));

		if (ai.cities.Any(c => c.location.NeighborsOcean())) {
			Assert.True(MarketAI.Choice(gameData, ai, Unit(gameData, "Frigate"), ai.cities.ToList()).location.NeighborsOcean());
		}
		Assert.Contains(MarketAI.Choice(gameData, ai, Unit(gameData, "Knight"), ai.cities.ToList()), ai.cities);
	}
}
