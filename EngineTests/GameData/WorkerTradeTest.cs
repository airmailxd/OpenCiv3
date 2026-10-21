using System;
using System.Linq;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Workers as deal items: "Any Worker currently in your capital city can be
// offered for trade. ... A traded Worker retains its nationality, just like
// a captured one." (Civ3 manual, p.~165). Per the owner's decision,
// 2026-10-21, they go to the receiver's capital as slave workers (see
// TradeOffer.workers).
public class WorkerTradeTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;
	private readonly City ourCapital;
	private readonly City ourTown;
	private readonly City theirCapital;
	private readonly UnitPrototype workerType;

	public WorkerTradeTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman).Take(2).ToArray();
		us = civs[0];
		them = civs[1];
		foreach (Player p in gameData.players) {
			foreach (City c in p.cities.ToList()) {
				CityInteractions.DestroyCity(c);
			}
		}
		us.EnsureRelationshipExists(them);
		if (PlayerRelationship.AtWar(us, them)) {
			PlayerRelationship.SignPeaceAfterWar(us, them, gameData);
		}
		us.playerRelationships[them.id].refuseContactUntilTurn = -1;
		them.playerRelationships[us.id].refuseContactUntilTurn = -1;

		ourCapital = BuildCity(us);
		ourTown = BuildCity(us);
		theirCapital = BuildCity(them);
		Assert.True(ourCapital.IsCapital());
		Assert.True(theirCapital.IsCapital());
		workerType = gameData.unitPrototypes.First(p => p.name == "Worker");
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private static bool IsEmptyLand(Tile t) {
		return t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp && !t.IsImpassable();
	}

	private City BuildCity(Player owner) {
		Tile tile = gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities()
			&& t.neighbors.Values.All(n => n == Tile.NONE || (!n.HasCity() && n.neighbors.Values.All(nn => nn == Tile.NONE || !nn.HasCity()))));
		return CityInteractions.BuildCity(tile, owner, owner.GetNextCityName());
	}

	[Fact]
	public void OnlyTheGiversOwnWorkersInItsCapitalCanBeTraded() {
		MapUnit inCapital = gameData.SpawnUnit(us, workerType, ourCapital.location);
		MapUnit inTown = gameData.SpawnUnit(us, workerType, ourTown.location);
		MapUnit theirs = gameData.SpawnUnit(them, workerType, theirCapital.location);

		Assert.Equal([inCapital], TradeOffer.TradableWorkers(us));
		Assert.Null(TradeOffer.ProblemWithDeal(gameData, us, them, new TradeOffer { workers = [inCapital] }, new TradeOffer()));
		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, us, them, new TradeOffer { workers = [inTown] }, new TradeOffer()));
		// Nor someone else's, nor the same one twice.
		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, us, them, new TradeOffer { workers = [theirs] }, new TradeOffer()));
		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, us, them, new TradeOffer { workers = [inCapital, inCapital] }, new TradeOffer()));
	}

	[Fact]
	public void ATradedWorkerGoesToTheReceiversCapitalAsASlave() {
		MapUnit worker = gameData.SpawnUnit(us, workerType, ourCapital.location);
		them.gold = 100;
		TradeOffer weGive = new() { workers = [worker] };
		TradeOffer weWant = new() { gold = 10 };

		Assert.True(them.ExecuteDeal(gameData, us, weGive, weWant));

		Assert.Same(them, worker.owner);
		Assert.Same(theirCapital.location, worker.location);
		Assert.Contains(worker, theirCapital.location.unitsOnTile);
		Assert.DoesNotContain(worker, ourCapital.location.unitsOnTile);
		Assert.Contains(worker, them.units);
		Assert.DoesNotContain(worker, us.units);
		// It keeps its nationality, so works at the captured workers' rate.
		Assert.Same(us.civilization, worker.nationality);
		Assert.True(worker.IsCaptive());
		// Trading it isn't capturing it.
		Assert.Equal(0, us.playerRelationships[them.id].workersCaptured);
	}

	// HEURISTIC prices (see TradeOffer.WorkerWorth): its shields' worth to
	// its own people, half that to the civ it would be a slave to.
	[Fact]
	public void TheAIPricesAWorkerByItsShieldCost() {
		MapUnit worker = gameData.SpawnUnit(us, workerType, ourCapital.location);
		int full = workerType.shieldCost * TradeOffer.WorkerGoldPerShield;
		Assert.True(full > 0);
		TradeOffer offer = new() { workers = [worker] };

		Assert.Equal(full, offer.GoldEquivalentFor(gameData, us, given: true, counterpart: them));
		Assert.Equal(full / 2, offer.GoldEquivalentFor(gameData, them, counterpart: us));

		// So the AI buys one for what it is worth to it, and no more.
		them.gold = 1000;
		Assert.True(them.WouldAcceptDealFrom(gameData, us, offer, new TradeOffer { gold = full / 2 }));
		Assert.False(them.WouldAcceptDealFrom(gameData, us, offer, new TradeOffer { gold = full / 2 + 1 }));
	}
}
