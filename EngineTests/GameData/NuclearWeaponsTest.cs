using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class NuclearWeaponsTest : IClassFixture<SaveGameFixture>, System.IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;

	public NuclearWeaponsTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler)).ToArray();
		us = civs[0];
		them = civs[1];
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	// Makes every random roll come out as the given value.
	private class FixedRandom(double value) : System.Random {
		protected override double Sample() => value;
		public override int Next(int maxValue) => 0;
		public override int Next(int minValue, int maxValue) => minValue;
	}

	private T WithRandom<T>(double value, System.Func<T> action) {
		System.Random original = C7GameData.GameData.rng;
		C7GameData.GameData.rng = new FixedRandom(value);
		try {
			return action();
		} finally {
			C7GameData.GameData.rng = original;
		}
	}

	private UnitPrototype Prototype(string name) {
		return gameData.unitPrototypes.Single(p => p.name == name);
	}

	private MapUnit Spawn(Player owner, string prototype, Tile tile) {
		return gameData.SpawnUnit(owner, Prototype(prototype), tile);
	}

	private Building Building(string name) {
		return gameData.Buildings.Single(b => b.name == name);
	}

	// Founds the player's city where its settler stands, or on the tile.
	private City FoundCity(Player player, int size, Tile tile = null) {
		tile ??= player.units.First(u => u.unitType.isSettler).location;
		City city = CityInteractions.BuildCity(tile, player, player.GetNextCityName());
		while (city.residents.Count < size) {
			city.AddCitizen(new CityResident() {
				city = city,
				citizenType = city.residents[0].citizenType,
				tileWorked = city.residents[0].tileWorked,
			});
		}
		return city;
	}

	// A land tile at the given distance from the city, to launch from.
	private Tile LandTileAtDistance(Tile from, int distance) {
		return gameData.map.tiles.First(t => t.IsLand() && !t.HasCity() && from.DistanceTo(t) == distance
			&& t.unitsOnTile.Count == 0);
	}

	private MapUnit ReadyNuke(string prototype, Tile launchSite, Tile target) {
		MapUnit nuke = Spawn(us, prototype, launchSite);
		foreach (Tile t in MapUnit.NuclearBlastArea(target)) {
			us.tileKnowledge.knownTiles.Add(t);
		}
		return nuke;
	}

	[Fact]
	public void NuclearUnitsAreImportedWithTheirAbilities() {
		Assert.True(Prototype("Tactical Nuke").isNuclearWeapon);
		Assert.True(Prototype("Tactical Nuke").isTacticalMissile);
		Assert.False(Prototype("Tactical Nuke").isICBM);
		Assert.True(Prototype("ICBM").isNuclearWeapon);
		Assert.True(Prototype("ICBM").isICBM);
		Assert.False(Prototype("Cruise Missile").isNuclearWeapon);
		Assert.True(Building("The Manhattan Project").allowsNuclearWeapons);
		Assert.True(Building("Strategic Missile Defense").decreasesMissileSuccess);
	}

	[Fact]
	public void NukesNeedTheManhattanProject() {
		City city = FoundCity(us, 3);
		UnitPrototype nuke = Prototype("Tactical Nuke");
		us.knownTechs.Add(nuke.requiredTech.id);
		HashSet<Resource> resources = gameData.Resources.ToHashSet();

		Assert.False(UnitPrototype.NuclearWeaponsAllowed(gameData));
		Assert.False(nuke.CanProduce(city, resources));

		// Built by anyone: here, another civ.
		gameData.GreatWondersBuilt.Add("The Manhattan Project");

		Assert.True(UnitPrototype.NuclearWeaponsAllowed(gameData));
		Assert.True(nuke.CanProduce(city, resources));
	}

	[Fact]
	public void NukesStillNeedTheirTech() {
		City city = FoundCity(us, 3);
		gameData.GreatWondersBuilt.Add("The Manhattan Project");

		Assert.False(Prototype("ICBM").CanProduce(city, gameData.Resources.ToHashSet()));
	}

	[Fact]
	public void TacticalNukesHaveLimitedRangeButICBMsDoNot() {
		City target = FoundCity(them, 4);
		Tile far = gameData.map.tiles.First(t => t.IsLand() && target.location.DistanceTo(t) > 10);
		MapUnit tactical = ReadyNuke("Tactical Nuke", far, target.location);
		MapUnit icbm = ReadyNuke("ICBM", far, target.location);

		Assert.False(tactical.CanNukeTile(target.location));
		Assert.True(icbm.CanNukeTile(target.location));
		Assert.True(icbm.CanBombardTile(target.location, out MapUnit.BombardTarget kind));
		Assert.Equal(MapUnit.BombardTarget.City, kind);
	}

	[Fact]
	public void NukesCannotTargetOwnCitiesOrUnknownTiles() {
		City ours = FoundCity(us, 3);
		City theirs = FoundCity(them, 3);
		Tile site = LandTileAtDistance(ours.location, 3);
		MapUnit icbm = Spawn(us, "ICBM", site);
		us.tileKnowledge.knownTiles.Remove(theirs.location);

		Assert.False(icbm.CanNukeTile(ours.location));
		Assert.False(icbm.CanNukeTile(theirs.location));
	}

	[Fact]
	public void DetonationDevastatesTheBlastArea() {
		City target = FoundCity(them, 8);
		Tile site = LandTileAtDistance(target.location, 4);
		MapUnit nuke = ReadyNuke("Tactical Nuke", site, target.location);
		MapUnit defender = Spawn(them, "Warrior", target.location);
		Tile neighbor = target.location.neighbors.Values.First(t => t.IsLand() && !t.HasCity());
		MapUnit bystander = Spawn(us, "Warrior", neighbor);
		Tile outside = gameData.map.tiles.First(t => t.IsLand() && target.location.DistanceTo(t) == 2);
		MapUnit safe = Spawn(them, "Warrior", outside);
		int buildings = target.constructed_buildings.Count(cb => !cb.building.isCenterOfEmpire && !cb.building.isSmallWonder && !cb.building.IsGreatWonder());

		// 0 for every roll: no interception (no defense anyway), every
		// building destroyed and fallout everywhere it can fall.
		MapUnit.NuclearStrikeResult result = WithRandom(0.0, () => nuke.LaunchNuke(target.location));

		Assert.False(result.intercepted);
		Assert.True(nuke.hitPointsRemaining <= 0);
		Assert.True(defender.hitPointsRemaining <= 0);
		Assert.True(bystander.hitPointsRemaining <= 0);
		Assert.True(safe.hitPointsRemaining > 0);
		Assert.Equal(4, target.residents.Count);
		Assert.Contains(target, result.citiesHit);
		Assert.Equal(buildings, result.buildingsDestroyed.Count);
		Assert.False(target.location.HasFallout());
		Assert.True(neighbor.HasFallout());
		Assert.Contains(them, result.victims);
		Assert.DoesNotContain(us, result.victims);
	}

	[Fact]
	public void FalloutStopsATileProducingAnything() {
		Tile tile = gameData.map.tiles.First(t => t.IsLand() && !t.HasCity() && !t.HasPollution()
			&& t.FoodYield(us).yield > 0 && t.ProductionYield(us).yield > 0);

		Assert.True(Tile.TryAddFallout(tile));

		Assert.Equal(0, tile.FoodYield(us).yield);
		Assert.Equal(0, tile.ProductionYield(us).yield);
		Assert.Equal(0, tile.CommerceYield(us).yield);
	}

	[Fact]
	public void NuclearAttackIsAnActOfWarAndAnAtrocity() {
		City target = FoundCity(them, 4);
		Tile site = LandTileAtDistance(target.location, 3);
		MapUnit nuke = ReadyNuke("Tactical Nuke", site, target.location);
		us.EnsureRelationshipExists(them);
		Assert.False(PlayerRelationship.AtWar(us, them));

		WithRandom(0.99, () => nuke.LaunchNuke(target.location));

		Assert.True(PlayerRelationship.AtWar(us, them));
		Assert.Equal(1, them.playerRelationships[us.id].nuclearAtrocityCount);
	}

	[Fact]
	public void StrategicMissileDefenseCanShootDownNukes() {
		City target = FoundCity(them, 6);
		target.AddBuilding(Building("Strategic Missile Defense"));
		Tile site = LandTileAtDistance(target.location, 3);
		MapUnit nuke = ReadyNuke("Tactical Nuke", site, target.location);
		MapUnit defender = Spawn(them, "Warrior", target.location);

		MapUnit.NuclearStrikeResult result = WithRandom(0.5, () => nuke.LaunchNuke(target.location));

		Assert.True(result.intercepted);
		Assert.True(nuke.hitPointsRemaining <= 0);
		Assert.True(defender.hitPointsRemaining > 0);
		Assert.Equal(6, target.residents.Count);
	}

	[Fact]
	public void MissileDefenseMissesOneInFour() {
		City target = FoundCity(them, 6);
		target.AddBuilding(Building("Strategic Missile Defense"));
		Tile site = LandTileAtDistance(target.location, 3);
		MapUnit nuke = ReadyNuke("Tactical Nuke", site, target.location);

		MapUnit.NuclearStrikeResult result = WithRandom(0.8, () => nuke.LaunchNuke(target.location));

		Assert.False(result.intercepted);
		Assert.Equal(3, target.residents.Count);
	}

	[Fact]
	public async Task BombardOrderFiresTheNuke() {
		City target = FoundCity(them, 4);
		Tile site = LandTileAtDistance(target.location, 2);
		MapUnit nuke = ReadyNuke("Tactical Nuke", site, target.location);
		us.DeclareWarOn(them, gameData.turn);

		await nuke.Bombard(target.location);

		Assert.True(nuke.hitPointsRemaining <= 0);
		Assert.Equal(2, target.residents.Count);
	}

	[Fact]
	public void NuclearSubmarinesCarryOnlyTacticalMissiles() {
		Tile sea = gameData.map.tiles.First(t => t.IsWater() && t.unitsOnTile.Count == 0);
		MapUnit sub = Spawn(us, "Nuclear Submarine", sea);
		MapUnit marine = Spawn(us, "Marine", sea);
		MapUnit nuke = Spawn(us, "Tactical Nuke", sea);

		marine.BoardTransport(sub);
		nuke.BoardTransport(sub);

		Assert.False(marine.IsLoadedIn(sub));
		Assert.True(nuke.IsLoadedIn(sub));
	}

	[Fact]
	public void TacticalNukesCanBeFiredFromSubmarines() {
		// A city within a submarine's reach of the sea, wherever the map
		// has one.
		static bool InReach(Tile sea, Tile land) => sea.IsWater() && sea.unitsOnTile.Count == 0
			&& sea.DistanceTo(land) <= 6 && sea.DistanceTo(land) > 1;
		Tile site = gameData.map.tiles.First(t => t.IsLand() && t.IsAllowCities() && !t.HasCity() && t.unitsOnTile.Count == 0
			&& gameData.map.tiles.Any(w => InReach(w, t)));
		City target = FoundCity(them, 4, site);
		Tile sea = gameData.map.tiles.First(t => InReach(t, target.location));
		MapUnit sub = Spawn(us, "Nuclear Submarine", sea);
		MapUnit nuke = ReadyNuke("Tactical Nuke", sea, target.location);
		nuke.BoardTransport(sub);

		Assert.True(nuke.CanNukeTile(target.location));
	}

	[Fact]
	public void AiNukesOnlyWorthwhileEnemyCitiesAtWar() {
		City target = FoundCity(them, 16);
		Tile site = LandTileAtDistance(target.location, 3);
		MapUnit nuke = ReadyNuke("Tactical Nuke", site, target.location);
		us.EnsureRelationshipExists(them);

		Assert.Null(NuclearAI.ChooseTarget(us, nuke));

		us.DeclareWarOn(them, gameData.turn);
		Assert.Equal(target.location, NuclearAI.ChooseTarget(us, nuke));

		// Not with our own units in the blast.
		Tile neighbor = target.location.neighbors.Values.First(t => t.IsLand() && !t.HasCity());
		Spawn(us, "Warrior", neighbor);
		Assert.Null(NuclearAI.ChooseTarget(us, nuke));
	}

	private Player ThirdCiv() {
		return gameData.players.First(p => !p.isBarbarians && p != us && p != them);
	}

	[Fact]
	public void NukesGoToWarWithEveryCivInTheBlast() {
		City target = FoundCity(them, 16);
		Player other = ThirdCiv();
		Tile site = LandTileAtDistance(target.location, 3);
		MapUnit nuke = ReadyNuke("Tactical Nuke", site, target.location);
		us.DeclareWarOn(them, gameData.turn);
		us.EnsureRelationshipExists(other);
		Tile neighbor = target.location.neighbors.Values.First(t => t.IsLand() && !t.HasCity());
		Spawn(other, "Warrior", neighbor);

		// The UI asks about the bystander as well as the target, and the AI
		// won't hit it.
		Assert.Equal([other], nuke.NuclearStrikeWarDeclarations(target.location));
		Assert.Equal(-1, NuclearAI.TargetValue(us, target.location));
		Assert.Null(NuclearAI.ChooseTarget(us, nuke));

		MapUnit.NuclearStrikeResult result = WithRandom(0.99, () => nuke.LaunchNuke(target.location));

		Assert.Contains(other, result.victims);
		Assert.True(PlayerRelationship.AtWar(us, other));
	}

	[Fact]
	public void CargoSunkWithItsTransportCountsAsDestroyed() {
		Tile sea = gameData.map.tiles.First(t => t.IsWater()
			&& MapUnit.NuclearBlastArea(t).All(b => b.unitsOnTile.Count == 0 && b.OwningPlayer() == null && !b.HasCity()));
		Tile site = gameData.map.tiles.First(t => t.IsLand() && t.unitsOnTile.Count == 0 && t.DistanceTo(sea) > 3);
		MapUnit icbm = ReadyNuke("ICBM", site, sea);
		MapUnit transport = Spawn(them, "Transport", sea);
		MapUnit cargo = Spawn(them, "Warrior", sea);
		cargo.BoardTransport(transport);
		Assert.True(cargo.IsLoadedIn(transport));
		us.DeclareWarOn(them, gameData.turn);

		MapUnit.NuclearStrikeResult result = WithRandom(0.99, () => icbm.LaunchNuke(sea));

		Assert.True(cargo.hitPointsRemaining <= 0);
		Assert.Equal(2, result.unitsDestroyed);
		Assert.Contains(them, result.victims);
	}

	[Fact]
	public void WorkersCleanUpFallout() {
		Tile tile = gameData.map.tiles.First(t => t.IsLand() && !t.HasCity() && !t.HasPollution()
			&& t.unitsOnTile.Count == 0 && t.FoodYield(us).yield > 0);
		MapUnit worker = Spawn(us, "Worker", tile);
		Terraform clean = gameData.Terraforms.Single(t => t.UIAction == C7Action.UnitClearDamage);

		// Nothing to clean yet.
		Assert.False(worker.CanPerformTerraformAction(clean));

		Assert.True(Tile.TryAddFallout(tile));
		Assert.True(worker.CanPerformTerraformAction(clean));
		Assert.True(clean.CalculateAIScore(us, tile) > 0);

		clean.OnComplete(us, tile);

		Assert.False(tile.HasFallout());
		Assert.True(tile.FoodYield(us).yield > 0);
		Assert.False(worker.CanPerformTerraformAction(clean));
	}
}
