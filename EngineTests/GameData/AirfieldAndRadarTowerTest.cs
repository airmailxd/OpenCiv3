using System;
using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;
using static C7GameData.Tile.TileOverlays;

namespace EngineTests.GameData;

// Airfields and radar towers, holdings a worker builds for its civ (see
// Tile_Holdings): an airfield is an air base for its owner, and passes to
// a civ whose borders take it in, if they know Flight; a radar tower gives
// its owner's units +25% in fights within two tiles, and is destroyed by
// another civ's borders. Both use up the worker (Conquests Civilopedia,
// GCON_Airfields and GCON_Radar_Towers).
public class AirfieldAndRadarTowerTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;
	private readonly Terraform airfield;
	private readonly Terraform radarTower;

	public AirfieldAndRadarTowerTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler)).ToArray();
		us = civs[0];
		them = civs[1];
		airfield = gameData.Terraforms.Single(t => t.Name == "Airfield");
		radarTower = gameData.Terraforms.Single(t => t.Name == "Radar Tower");
		// Airfields need Flight, radar towers Radio.
		us.knownTechs.Add(airfield.RequiredTech);
		us.knownTechs.Add(radarTower.RequiredTech);
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private UnitPrototype Proto(string name) => gameData.unitPrototypes.Single(p => p.name == name);

	private TerrainImprovement Improvement(string key) => gameData.terrainImprovements.Single(ti => ti.key == key);

	// Flat open land far from every unit and city, inside no one's borders.
	private Tile LonelyFlatLand(Func<Tile, bool> also = null) {
		return gameData.map.tiles.First(t => t.IsLand() && !t.IsImpassable() && t.overlayTerrainType.height < 2
			&& t.OwningPlayer() == null && !t.hasBarbarianCamp && !t.hasGoodyHut && !t.overlays.HasBeenImproved()
			&& t.GetTilesWithinRankDistance(5).All(n => n.unitsOnTile.Count == 0 && !n.HasCity() && n.OwningPlayer() == null)
			&& (also == null || also(t)));
	}

	// Lonely land with a neighbor a city can go on.
	private Tile LonelyLandNextToACitySite() {
		return LonelyFlatLand(t => t.neighbors.Values.Any(n => n.IsLand() && n.IsAllowCities()));
	}

	private Tile CitySiteNextTo(Tile site) => site.neighbors.Values.First(n => n.IsLand() && n.IsAllowCities());

	private Tile BuildWithAWorker(Terraform job, Tile site) {
		MapUnit worker = gameData.SpawnUnit(us, Proto("Worker"), site);
		Assert.Contains(job, worker.GetAvailableTerraforms());
		worker.PerformTerraformAction(job);
		if (worker.WorkerJob != null) {
			worker.PerformEndOfTurnAction();
		}
		// "Construction of the Airfield [Radar Tower] consumes the worker."
		Assert.DoesNotContain(worker, us.units);
		Assert.Empty(site.unitsOnTile);
		return site;
	}

	[Fact]
	public void AnAirfieldIsAnAirBaseForItsOwnerOnly() {
		Tile site = BuildWithAWorker(airfield, LonelyFlatLand());

		Assert.True(site.HasAirfield());
		Assert.Same(us, site.AirfieldOwner());
		Assert.True(MapUnit.HasAirfieldFor(site, us));
		Assert.True(MapUnit.IsAirBaseTile(site, us));
		// "Airfields can only be used by the player who created it."
		Assert.False(MapUnit.IsAirBaseTile(site, them));

		// "Airfields can be a re-base mission destination."
		Tile nearby = site.GetTilesWithinRankDistance(3).First(t => t != site && t.IsLand());
		MapUnit fighter = gameData.SpawnUnit(us, Proto("Fighter"), nearby);
		Assert.True(fighter.CanRebaseTo(site));
		fighter.Rebase(site);
		Assert.Same(site, fighter.location);
		Assert.True(fighter.IsAtAirBase());
	}

	[Fact]
	public void ParatroopersDropFromTheirOwnersAirfield() {
		Tile site = BuildWithAWorker(airfield, LonelyFlatLand());
		MapUnit paratrooper = gameData.SpawnUnit(us, Proto("Paratrooper"), site);
		Assert.True(paratrooper.CanAirdropFrom());
		Tile target = site.GetTilesWithinRankDistance(3).First(t => t.RankDistanceTo(site) == 3 && t.IsLand() && !t.IsImpassable()
			&& t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp && paratrooper.unitType.CanEnterTerrainOf(t));
		Assert.True(paratrooper.CanAirdropTo(target));

		// Not from someone else's.
		MapUnit theirs = gameData.SpawnUnit(them, Proto("Paratrooper"), site);
		Assert.False(theirs.CanAirdropFrom());
		// Nor from open country.
		paratrooper.Airdrop(target);
		Assert.Same(target, paratrooper.location);
		paratrooper.movementPoints.reset(paratrooper.MaxMovementPoints());
		Assert.False(paratrooper.CanAirdropFrom());
	}

	[Fact]
	public void AnAirfieldGoesOnlyOnEmptyOrRoadedLand() {
		Tile site = LonelyFlatLand();
		Assert.True(site.CanTakeAirfield(us));
		site.overlays.Add(Improvement(ROAD));
		Assert.True(site.CanTakeAirfield(us));
		site.overlays.Add(Improvement(MINE));
		Assert.False(site.CanTakeAirfield(us));
		site.overlays.Remove(Improvement(MINE));
		site.overlays.Add(Improvement(FORTRESS));
		Assert.False(site.CanTakeAirfield(us));
	}

	[Fact]
	public void AnAirfieldNeedsFlight() {
		Tile site = LonelyFlatLand();
		MapUnit worker = gameData.SpawnUnit(us, Proto("Worker"), site);
		Assert.Contains(airfield, worker.GetAvailableTerraforms());
		us.knownTechs.Remove(airfield.RequiredTech);
		Assert.DoesNotContain(airfield, worker.GetAvailableTerraforms());
	}

	[Fact]
	public void ARivalWhoKnowsFlightTakesOverAnAirfieldInTheirBorders() {
		Tile site = BuildWithAWorker(airfield, LonelyLandNextToACitySite());
		them.knownTechs.Add(airfield.RequiredTech);

		// "If it falls into the territory of another civilization, however,
		// the airfield becomes solely useable by the player whose territory
		// it lies in."
		CityInteractions.BuildCity(CitySiteNextTo(site), them, "Annexia");

		Assert.True(site.HasAirfield());
		Assert.Same(them, site.AirfieldOwner());
		Assert.True(MapUnit.IsAirBaseTile(site, them));
		Assert.False(MapUnit.HasAirfieldFor(site, us));
	}

	[Fact]
	public void ARivalWithoutFlightDestroysAnAirfieldInTheirBorders() {
		Tile site = BuildWithAWorker(airfield, LonelyLandNextToACitySite());
		them.knownTechs.Remove(airfield.RequiredTech);

		// "(unless they have yet to research Flight, in which case the
		// airfield is destroyed)"
		CityInteractions.BuildCity(CitySiteNextTo(site), them, "Groundia");

		Assert.False(site.HasAirfield());
		Assert.Null(site.holdingOwner);
	}

	[Fact]
	public void PillagingAnAirfieldDestroysIt() {
		Tile site = BuildWithAWorker(airfield, LonelyFlatLand());
		site.overlays.Remove(site.overlays.ImprovementAtLayer(TerrainImprovement.Layer.Holdings));
		Assert.Null(site.holdingOwner);
		Assert.False(MapUnit.HasAirfieldFor(site, us));
	}

	// A tile inside our borders, with a city of ours next to it.
	private Tile OurLand() {
		Tile site = LonelyLandNextToACitySite();
		CityInteractions.BuildCity(CitySiteNextTo(site), us, "Radarville");
		Assert.Same(us, site.OwningPlayer());
		return site;
	}

	[Fact]
	public void ARadarTowerGoesOnlyInOurOwnTerritory() {
		Assert.False(LonelyFlatLand().CanTakeRadarTower(us));
		Tile site = OurLand();
		Assert.False(site.CanTakeRadarTower(them));
		Assert.True(site.CanTakeRadarTower(us));
		site.overlays.Add(Improvement(MINE));
		Assert.True(site.CanTakeRadarTower(us));
		site.overlays.Add(Improvement(FORTRESS));
		Assert.False(site.CanTakeRadarTower(us));
	}

	private static double Multiplier(MapUnit unit, MapUnit opponent, CombatRole role) {
		return StrengthBonus.ListToMultiplier(unit.ListStrengthBonusesVersus(opponent, role, null));
	}

	[Fact]
	public void ARadarTowerGivesOurUnitsWithinTwoTilesAQuarterMore() {
		Tile site = OurLand();
		Tile twoAway = site.GetTilesWithinTileSquare(2).First(t => t.RankDistanceTo(site) == 2 && t.IsLand() && !t.HasCity());
		Tile threeAway = site.GetTilesWithinTileSquare(3).Except(site.GetTilesWithinTileSquare(2)).First(t => t.IsLand() && !t.HasCity());
		MapUnit near = gameData.SpawnUnit(us, Proto("Warrior"), twoAway);
		MapUnit far = gameData.SpawnUnit(us, Proto("Warrior"), threeAway);
		MapUnit rival = gameData.SpawnUnit(them, Proto("Warrior"), twoAway.neighbors.Values.First(t => t.IsLand() && t != site));
		double nearAttack = Multiplier(near, rival, CombatRole.Attack), nearDefense = Multiplier(near, rival, CombatRole.Defense);
		double farAttack = Multiplier(far, rival, CombatRole.Attack);
		double rivalDefense = Multiplier(rival, near, CombatRole.Defense);

		BuildWithAWorker(radarTower, site);
		Assert.Same(us, site.RadarTowerOwner());

		// "Friendly units within the Radar Towers range get a +25% attack and
		// defense bonus."
		Assert.Equal(nearAttack + 0.25, Multiplier(near, rival, CombatRole.Attack), 6);
		Assert.Equal(nearDefense + 0.25, Multiplier(near, rival, CombatRole.Defense), 6);
		Assert.Equal(farAttack, Multiplier(far, rival, CombatRole.Attack), 6);
		// "Radar Towers can only be used by the player who created them."
		Assert.Equal(rivalDefense, Multiplier(rival, near, CombatRole.Defense), 6);

		// "This bonus can only be applied once, even if multiple towers
		// overlap."
		Tile second = twoAway.neighbors.Values.First(t => t.IsLand() && t.unitsOnTile.Count == 0 && t.OwningPlayer() == us && t.CanTakeRadarTower(us));
		BuildWithAWorker(radarTower, second);
		Assert.Equal(nearAttack + 0.25, Multiplier(near, rival, CombatRole.Attack), 6);
	}

	[Fact]
	public void ARivalsBordersDestroyARadarTower() {
		// A tower of ours left outside our borders, as a scenario may place
		// one (or as our borders may shrink).
		Tile site = LonelyLandNextToACitySite();
		site.PlaceHolding(Improvement(RADAR_TOWER), us);
		Assert.Same(us, site.RadarTowerOwner());

		// "Should a Radar Tower fall into the territory of another
		// civilization, it will be destroyed."
		CityInteractions.BuildCity(CitySiteNextTo(site), them, "Jammer");

		Assert.False(site.HasRadarTower());
		Assert.Null(site.holdingOwner);
	}

	[Fact]
	public void AirfieldsAndRadarTowersKeepTheirOwnersThroughASave() {
		Tile airfieldSite = BuildWithAWorker(airfield, LonelyFlatLand());
		Tile radarSite = BuildWithAWorker(radarTower, OurLand());

		C7GameData.GameData reloaded = SaveGame.FromJSON(SaveGame.FromGameData(gameData).ToCompactJSON()).ToGameData(fixture.behaviors);
		Tile loadedAirfield = reloaded.map.tileAt(airfieldSite.XCoordinate, airfieldSite.YCoordinate);
		Tile loadedRadar = reloaded.map.tileAt(radarSite.XCoordinate, radarSite.YCoordinate);
		Assert.Equal(us.id, loadedAirfield.AirfieldOwner()?.id);
		Assert.Equal(us.id, loadedRadar.RadarTowerOwner()?.id);
	}
}
