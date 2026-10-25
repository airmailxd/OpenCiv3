using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using C7GameData.AIData;
using EngineTests.GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

// How the AI plays Kings & Nomads (see KingsAndNomadsAI): the horde marches
// on a city-state together and storms it, the King never attacks and
// retires to a city with a guard that stays, city-states keep their
// garrisons, and captured cities build settlers. None of it touches an
// ordinary game.
public sealed class KingsAndNomadsAITest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;

	public KingsAndNomadsAITest(SaveGameFixture fixture) {
		this.fixture = fixture;
		EngineStorage.animationsEnabled = false;
		EngineStorage.aiFogOfWar = false;
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private sealed class World {
		public C7GameData.GameData gameData;
		public Player civ;
		public Player independents;
		public MapUnit king;
		// The city-state nearest the civ's King on its landmass.
		public City cityState;
	}

	private static World NewWorld(int seed) {
		(_, C7GameData.GameData gameData) = KingsAndNomadsScenarioTest.NewGame(seed);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		EngineStorage.aiFogOfWar = false;
		World w = new() { gameData = gameData, independents = gameData.players.Single(p => p.isCityStates) };
		foreach (Player civ in gameData.players.Where(p => !p.IsIndependent)) {
			civ.isHuman = false;
		}
		// A civ with a city-state on its landmass.
		foreach (Player civ in gameData.players.Where(p => !p.IsIndependent)) {
			MapUnit king = civ.units.Single(u => u.unitType.isKing);
			City near = w.independents.cities.Where(c => c.location.continent == king.location.continent)
				.OrderBy(c => c.location.DistanceTo(king.location)).FirstOrDefault();
			if (near != null) {
				(w.civ, w.king, w.cityState) = (civ, king, near);
				return w;
			}
		}
		throw new InvalidOperationException("No civ shares a landmass with a city-state");
	}

	private static void Teleport(MapUnit u, Tile t) {
		u.location.unitsOnTile.Remove(u);
		t.unitsOnTile.Add(u);
		u.location = t;
	}

	private static UnitPrototype Prototype(C7GameData.GameData gameData, string name) {
		return gameData.unitPrototypes.Single(p => p.name == name);
	}

	// An open land tile next to the city, from which the horde can attack it.
	private static Tile NextTo(City city) {
		return city.location.neighbors.Values.First(t => t != Tile.NONE && t.IsLand() && !t.HasCity() && t.unitsOnTile.Count == 0
			&& !t.IsImpassable() && t.continent == city.location.continent);
	}

	[Fact]
	public void OnlyTheScenarioPlaysThisWay() {
		World w = NewWorld(1);
		Assert.True(KingsAndNomadsAI.IsActive(w.gameData));
		Assert.True(KingsAndNomadsAI.PlaysFor(w.civ));
		Assert.False(WarCampaign.Enabled(w.civ));
		MapUnit garrison = w.cityState.location.unitsOnTile.First(u => u.owner == w.independents);
		Assert.True(KingsAndNomadsAI.HoldsCityState(w.independents, garrison));

		C7GameData.GameData ordinary = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(ordinary);
		Player ai = ordinary.players.First(p => !p.IsIndependent && !p.isHuman);
		Assert.False(KingsAndNomadsAI.IsActive(ordinary));
		Assert.False(KingsAndNomadsAI.PlaysFor(ai));
		Assert.True(WarCampaign.Enabled(ai));
	}

	// The horde, King and all, sets out for the city-state together.
	[Fact]
	public async Task TheHordeMarchesTogetherOnACityState() {
		World w = NewWorld(1);
		int before = w.king.location.DistanceTo(w.cityState.location);
		await KingsAndNomadsAI.PlayTurn(w.civ, w.gameData);

		List<MapUnit> fighters = w.civ.units.Where(KingsAndNomadsAI.IsFighter).ToList();
		Assert.NotEmpty(fighters);
		Assert.All(fighters, u => Assert.IsType<WarCampaign.MemberAI>(u.currentAI));
		Assert.IsType<WarCampaign.MemberAI>(w.king.currentAI);
		Assert.True(w.king.location.DistanceTo(w.cityState.location) < before);
		// The King is never left alone.
		Assert.Contains(w.king.location.unitsOnTile, u => u.owner == w.civ && KingsAndNomadsAI.IsFighter(u));
	}

	// Next to a worn-down city-state, the horde attacks until it falls, and
	// walks in; the King comes in too, and a garrison stays.
	[Fact]
	public async Task TheHordeStormsTheCityStateAndTheKingMovesIn() {
		World w = NewWorld(2);
		Tile rally = NextTo(w.cityState);
		foreach (MapUnit u in w.civ.units.Where(u => u.location.continent == w.king.location.continent && u.IsLandUnit()).ToList()) {
			Teleport(u, rally);
		}
		foreach (MapUnit u in w.cityState.location.unitsOnTile.Where(u => u.owner == w.independents)) {
			u.hitPointsRemaining = 1;
		}
		await KingsAndNomadsAI.PlayTurn(w.civ, w.gameData);

		Assert.Equal(w.civ, w.cityState.owner);
		Assert.Equal(w.cityState.location, w.king.location);
		Assert.Contains(w.cityState.location.unitsOnTile, u => u.owner == w.civ && u.CanDefendOnLand() && !u.unitType.isKing
			&& u.currentAI is not WarCampaign.MemberAI);
		Assert.Contains(w.king, w.civ.units);
	}

	// The King is no fighter: it never attacks, even an enemy in its way.
	[Fact]
	public async Task TheKingNeverAttacks() {
		World w = NewWorld(3);
		Assert.False(KingsAndNomadsAI.IsFighter(w.king));
		CityInteractions.CaptureCity(w.cityState, w.civ);
		Tile from = w.king.location;
		Tile blocked = from.neighbors.Values.First(t => t != Tile.NONE && t.IsLand() && !t.HasCity() && t.unitsOnTile.Count == 0 && !t.IsImpassable());
		MapUnit warrior = w.gameData.SpawnUnit(w.independents, Prototype(w.gameData, "Warrior"), blocked);
		warrior.hitPointsRemaining = 1;
		KingsAndNomadsAI.KingAI ai = new() {
			destination = w.cityState.location,
			path = new TilePath(w.cityState.location, new Queue<Tile>([blocked, w.cityState.location])),
		};
		await ((C7GameData.UnitAI)ai).PlayTurn(w.civ, w.king);

		Assert.Equal(from, w.king.location);
		Assert.Equal(1, warrior.hitPointsRemaining);
		Assert.Contains(warrior, w.independents.units);
	}

	// Once the civ has a city, the King goes there, and stays.
	[Fact]
	public void TheKingRetiresToOurCity() {
		World w = NewWorld(4);
		CityInteractions.CaptureCity(w.cityState, w.civ);
		Teleport(w.king, NextTo(w.cityState));
		C7GameData.UnitAI ai = PlayerAI.GetAIForUnit(w.king, w.civ);
		KingsAndNomadsAI.KingAI king = Assert.IsType<KingsAndNomadsAI.KingAI>(ai);
		Assert.Equal(w.cityState.location, king.destination);

		Teleport(w.king, w.cityState.location);
		king = Assert.IsType<KingsAndNomadsAI.KingAI>(PlayerAI.GetAIForUnit(w.king, w.civ));
		Assert.Equal(w.cityState.location, king.destination);
	}

	// The King's city keeps the defenders it wants, which the rest of the AI
	// leaves alone; any more are free to go.
	[Fact]
	public void TheKingsCityKeepsItsGuard() {
		World w = NewWorld(5);
		CityInteractions.CaptureCity(w.cityState, w.civ);
		foreach (MapUnit u in w.cityState.location.unitsOnTile.Where(u => u.owner != w.civ).ToList()) {
			w.gameData.RemoveUnit(u);
		}
		Teleport(w.king, w.cityState.location);
		List<MapUnit> spearmen = Enumerable.Range(0, 6)
			.Select(_ => w.gameData.SpawnUnit(w.civ, Prototype(w.gameData, "Spearman"), w.cityState.location)).ToList();
		int want = KingsAndNomadsAI.GarrisonWanted(w.civ, w.cityState, AIThreat.Assess(w.civ));
		Assert.InRange(want, 3, 5);
		Assert.Equal(want, spearmen.Count(u => KingsAndNomadsAI.GuardsTheKing(u, w.civ)));
		foreach (MapUnit guard in spearmen.Where(u => KingsAndNomadsAI.GuardsTheKing(u, w.civ))) {
			C7Engine.AI.UnitAI.DefenderAI stay = Assert.IsType<C7Engine.AI.UnitAI.DefenderAI>(PlayerAI.GetAIForUnit(guard, w.civ));
			Assert.Equal(guard.location, stay.data.destination);
		}
	}

	// A captured city builds a defender first, then settlers (which only a
	// captured city can build); the first founds our capital.
	[Fact]
	public void ACapturedCityWantsADefenderAndThenSettlers() {
		World w = NewWorld(6);
		CityInteractions.CaptureCity(w.cityState, w.civ);
		foreach (MapUnit u in w.cityState.location.unitsOnTile.ToList()) {
			w.gameData.RemoveUnit(u);
		}
		UnitPrototype spearman = Prototype(w.gameData, "Spearman");
		UnitPrototype settler = w.gameData.unitPrototypes.First(p => p.isSettler);
		Assert.True(KingsAndNomadsAI.UnitBonus(w.cityState, w.civ, spearman) > 0);
		Assert.Equal(0, KingsAndNomadsAI.UnitBonus(w.cityState, w.civ, settler));

		w.gameData.SpawnUnit(w.civ, spearman, w.cityState.location);
		Assert.Equal(0, KingsAndNomadsAI.UnitBonus(w.cityState, w.civ, spearman));
		Assert.True(KingsAndNomadsAI.UnitBonus(w.cityState, w.civ, settler) > 0);
	}

	// The civ holds the city-state, and its King and a horde of eight
	// fighters and three spearmen are there.
	private static void KingAtHomeWithAHorde(World w) {
		CityInteractions.CaptureCity(w.cityState, w.civ);
		foreach (MapUnit u in w.cityState.location.unitsOnTile.Where(u => u.owner != w.civ).ToList()) {
			w.gameData.RemoveUnit(u);
		}
		foreach (MapUnit u in w.civ.units.Where(u => u.IsLandUnit() && !u.unitType.isWorker).ToList()) {
			Teleport(u, w.cityState.location);
		}
		foreach (string name in new[] { "Archer", "Archer", "Spearman", "Spearman", "Spearman" }) {
			w.gameData.SpawnUnit(w.civ, Prototype(w.gameData, name), w.cityState.location);
		}
	}

	private static void NewTurn(World w) {
		foreach (MapUnit u in w.civ.units) {
			u.movementPoints.reset(u.MaxMovementPoints());
		}
	}

	// Our units that could defend the King's tile.
	private static int DefendersWithTheKing(World w) {
		return w.king.location.unitsOnTile.Count(u => u.owner == w.civ && u != w.king && u.CanDefendOnLand() && !u.unitType.isWorker);
	}

	// Once the civ has a city, the strongest horde picks the King up and
	// he marches with it, never alone.
	[Fact]
	public async Task TheKingMarchesWithTheAttackingHorde() {
		World w = NewWorld(8);
		KingAtHomeWithAHorde(w);
		await KingsAndNomadsAI.PlayTurn(w.civ, w.gameData);
		Assert.IsType<WarCampaign.MemberAI>(w.king.currentAI);
		// The city keeps a defender.
		Assert.Contains(w.cityState.location.unitsOnTile, u => u.owner == w.civ && u.CanDefendOnLand() && u.currentAI is not WarCampaign.MemberAI
			&& !u.unitType.isKing);

		NewTurn(w);
		await KingsAndNomadsAI.PlayTurn(w.civ, w.gameData);
		Assert.NotEqual(w.cityState.location, w.king.location);
		Assert.IsType<WarCampaign.MemberAI>(w.king.currentAI);
		Assert.True(DefendersWithTheKing(w) >= 2);
		Assert.Contains(w.king.location.unitsOnTile, u => u.owner == w.civ && KingsAndNomadsAI.IsFighter(u));
	}

	// The horde with the King attacks from his tile, so with his bonus; he
	// never attacks.
	[Fact]
	public async Task TheHordeAttacksFromBesideTheKing() {
		World w = NewWorld(9);
		City target = w.independents.cities.Where(c => c != w.cityState && c.location.continent == w.cityState.location.continent)
			.OrderBy(c => c.location.DistanceTo(w.cityState.location)).First();
		KingAtHomeWithAHorde(w);
		await KingsAndNomadsAI.PlayTurn(w.civ, w.gameData);
		Assert.IsType<WarCampaign.MemberAI>(w.king.currentAI);

		Tile rally = NextTo(target);
		foreach (MapUnit u in w.civ.units.Where(u => u.currentAI is WarCampaign.MemberAI).ToList()) {
			Teleport(u, rally);
		}
		foreach (MapUnit u in target.location.unitsOnTile.Where(u => u.owner == w.independents)) {
			u.hitPointsRemaining = 1;
		}
		NewTurn(w);
		List<(MapUnit attacker, int fromKing)> attacks = new();
		AiExperiments.onCombat = (attacker, _, _) => {
			if (attacker.owner == w.civ) {
				attacks.Add((attacker, attacker.location.DistanceTo(w.king.location)));
			}
		};
		try {
			await KingsAndNomadsAI.PlayTurn(w.civ, w.gameData);
		} finally {
			AiExperiments.onCombat = null;
		}

		Assert.NotEmpty(attacks);
		Assert.All(attacks, a => Assert.True(a.fromKing <= 1));
		Assert.DoesNotContain(attacks, a => a.attacker == w.king);
		Assert.Equal(w.civ, target.owner);
		Assert.Contains(w.king, w.civ.units);
	}

	// Faced with more enemies than his stack could hold off, the King is
	// taken back to our city, and stays.
	[Fact]
	public async Task TheKingRetreatsWhenThreatened() {
		World w = NewWorld(10);
		KingAtHomeWithAHorde(w);
		await KingsAndNomadsAI.PlayTurn(w.civ, w.gameData);
		Assert.IsType<WarCampaign.MemberAI>(w.king.currentAI);

		// The horde three tiles out, and a rival's knights next to it,
		// further out.
		Tile home = w.cityState.location;
		bool Open(Tile t) => t != Tile.NONE && t.IsLand() && !t.HasCity() && !t.IsImpassable() && t.continent == home.continent
			&& t.unitsOnTile.Count == 0;
		(Tile field, Tile enemyAt) = w.gameData.map.tiles.Where(t => Open(t) && t.DistanceTo(home) == 3)
			.SelectMany(t => t.neighbors.Values.Where(n => Open(n) && n.DistanceTo(home) == 4).Select(n => (t, n))).First();
		foreach (MapUnit u in w.civ.units.Where(u => u.currentAI is WarCampaign.MemberAI).ToList()) {
			Teleport(u, field);
		}
		Player rival = w.gameData.players.First(p => p != w.civ && !p.IsIndependent);
		w.civ.DeclareWarOn(rival, w.gameData.turn);
		for (int i = 0; i < 20; ++i) {
			w.gameData.SpawnUnit(rival, Prototype(w.gameData, "Knight"), enemyAt);
		}

		for (int turn = 0; turn < 6 && w.king.location != home; ++turn) {
			NewTurn(w);
			int before = w.king.location.DistanceTo(home);
			await KingsAndNomadsAI.PlayTurn(w.civ, w.gameData);
			Assert.True(w.king.location.DistanceTo(home) < before);
			Assert.True(DefendersWithTheKing(w) >= 1);
		}
		Assert.Equal(home, w.king.location);
		Assert.IsType<KingsAndNomadsAI.KingAI>(w.king.currentAI);
	}

	// A civ with no city, no capital and no settler plays its turn through
	// the whole AI without trouble.
	[Fact]
	public async Task ACivWithNoCityPlaysItsTurn() {
		World w = NewWorld(7);
		Assert.Empty(w.civ.cities);
		await PlayerAI.PlayTurn(w.civ, w.gameData);
		Assert.False(w.civ.defeated);
		Assert.All(w.civ.units.Where(KingsAndNomadsAI.IsFighter), u => Assert.IsType<WarCampaign.MemberAI>(u.currentAI));
	}

	// A horde spares a city-state its civ trades with, unless a rival city
	// is near enough to take it in its stead.
	[Fact]
	public void TradePartnersAreSparedWhileNoRivalIsNear() {
		World w = NewWorld(8);
		Tile cs = w.cityState.location;
		(Tile site, Tile middle) = cs.GetTilesWithinRankDistance(2)
			.Where(t => t != Tile.NONE && t.IsLand() && t.IsAllowCities() && !t.HasCity() && t.DistanceTo(cs) == 2)
			.SelectMany(t => t.neighbors.Values.Where(m => m != Tile.NONE && m.IsLand() && cs.neighbors.ContainsValue(m)).Select(m => (t, m)))
			.First();
		CityInteractions.BuildCity(site, w.civ, "Trader");
		middle.overlays.Add(w.gameData.terrainImprovements.Single(ti => ti.key == C7GameData.Tile.TileOverlays.ROAD));
		w.gameData.InvalidateCachedTradeNetwork();
		Assert.Contains(w.cityState, KingsAndNomadsAI.SparedPartners(w.civ, w.gameData));

		Player rival = w.gameData.players.First(p => !p.IsIndependent && p != w.civ);
		Tile near = w.gameData.map.tiles.First(t => t.IsAllowCities() && !t.HasCity() && t.unitsOnTile.Count == 0
			&& t.DistanceTo(cs) <= KingsAndNomadsAI.SpareRivalDistance && t.DistanceTo(cs) > 2 && t.DistanceTo(site) > 1);
		CityInteractions.BuildCity(near, rival, "Rival");
		Assert.DoesNotContain(w.cityState, KingsAndNomadsAI.SparedPartners(w.civ, w.gameData));
	}
}
