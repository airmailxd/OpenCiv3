using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.AI;
using C7Engine.AI.UnitAI;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI.UnitAI;

// How the AI fights with its warships, and when it builds them (see
// NavalAI).
public sealed class NavalAITest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;
	private readonly List<(MapUnit attacker, MapUnit defender)> fights = new();

	public NavalAITest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		EngineStorage.aiFogOfWar = false;
		Player[] civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman).Take(2).ToArray();
		us = civs[0];
		them = civs[1];
		us.EnsureRelationshipExists(them);
		Prototype("Privateer").hasHiddenNationality = true;
		AiExperiments.onCombat = (attacker, defender, _) => fights.Add((attacker, defender));
	}

	public void Dispose() {
		AiExperiments.onCombat = null;
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private UnitPrototype Prototype(string name) => gameData.unitPrototypes.Single(p => p.name == name);

	private MapUnit Spawn(Player owner, string prototype, Tile tile) {
		gameData.SpawnUnit(owner, Prototype(prototype), tile);
		return tile.unitsOnTile.Last();
	}

	private static bool IsOpenSea(Tile t) {
		return t != Tile.NONE && t.IsWater() && !t.isFreshWater && t.unitsOnTile.Count == 0 && !t.HasCity();
	}

	// A coastal land tile with open sea all about it: a city of ours there,
	// and the sea tiles next to it.
	private (City city, List<Tile> sea) CoastalCity() {
		foreach (Tile land in gameData.map.tiles.Where(t => t.IsLand() && !t.HasCity() && t.unitsOnTile.Count == 0 && t.OwningPlayer() == null)) {
			List<Tile> sea = land.neighbors.Values.Where(IsOpenSea).ToList();
			if (sea.Count < 3 || land.neighbors.Values.Any(n => n.HasCity())) {
				continue;
			}
			// Room for a few ships beyond the coast.
			if (sea.Any(s => s.neighbors.Values.Count(IsOpenSea) >= 4)) {
				City city = CityInteractions.BuildCity(land, us, us.GetNextCityName());
				return (city, sea);
			}
		}
		throw new Exception("No coastal tile found");
	}

	// An open sea tile next to `tile`, other than those given.
	private static Tile SeaNextTo(Tile tile, params Tile[] except) {
		return tile.neighbors.Values.First(t => IsOpenSea(t) && !except.Contains(t));
	}

	private static void Learn(Player player, UnitPrototype unit) {
		player.knownTechs.Add(unit.requiredTech.id);
	}

	[Fact]
	public void WarshipsAreShipsThatFightAndCarryNothing() {
		Assert.True(NavalAI.IsWarship(Prototype("Frigate")));
		Assert.True(NavalAI.IsWarship(Prototype("Ironclad")));
		Assert.True(NavalAI.IsWarship(Prototype("Privateer")));
		Assert.False(NavalAI.IsWarship(Prototype("Galleon")));
		Assert.False(NavalAI.IsWarship(Prototype("Galley")));
		Assert.False(NavalAI.IsWarship(Prototype("Carrier")));
		Assert.False(NavalAI.IsWarship(Prototype("Warrior")));
	}

	[Fact]
	public void AtPeaceAWarshipIsLeftToTheOtherUnitAIs() {
		(City city, List<Tile> sea) = CoastalCity();
		MapUnit frigate = Spawn(us, "Frigate", sea[0]);
		Assert.Null(NavalAI.MaybeMake(frigate, us));
		Assert.IsNotType<NavalAI>(PlayerAI.GetAIForUnit(frigate, us));
	}

	[Fact]
	public async Task AtWarAWarshipAttacksAWeakEnemyShipInReach() {
		(City city, List<Tile> sea) = CoastalCity();
		MapUnit ironclad = Spawn(us, "Ironclad", sea[0]);
		MapUnit galley = Spawn(them, "Galley", SeaNextTo(sea[0], sea[0]));
		us.DeclareWarOn(them, gameData.turn);

		C7GameData.UnitAI ai = PlayerAI.GetAIForUnit(ironclad, us);
		Assert.IsType<NavalAI>(ai);
		await ai.PlayTurn(us, ironclad);

		Assert.Contains(fights, f => f.attacker == ironclad && f.defender == galley);
	}

	[Fact]
	public async Task AWarshipDoesntAttackAStrongerShip() {
		(City city, List<Tile> sea) = CoastalCity();
		MapUnit frigate = Spawn(us, "Frigate", sea[0]);
		Spawn(them, "Ironclad", SeaNextTo(sea[0], sea[0]));
		us.DeclareWarOn(them, gameData.turn);

		await ((C7GameData.UnitAI)new NavalAI()).PlayTurn(us, frigate);

		Assert.DoesNotContain(fights, f => f.attacker == frigate);
	}

	[Fact]
	public async Task AHurtWarshipGoesHomeToHeal() {
		(City city, List<Tile> sea) = CoastalCity();
		Tile start = sea[0].neighbors.Values.First(t => IsOpenSea(t) && t.DistanceTo(city.location) == 2);
		MapUnit frigate = Spawn(us, "Frigate", start);
		frigate.hitPointsRemaining = 1;
		us.DeclareWarOn(them, gameData.turn);

		await ((C7GameData.UnitAI)new NavalAI()).PlayTurn(us, frigate);

		Assert.Equal(city.location, frigate.location);
	}

	[Fact]
	public async Task AWarshipBombardsAnEnemyCityInRange() {
		(City city, List<Tile> sea) = CoastalCity();
		// Their city on a coast elsewhere, with our frigate next to it.
		Tile theirs = gameData.map.tiles.First(t => t.IsLand() && !t.HasCity() && t.unitsOnTile.Count == 0 && t.DistanceTo(city.location) > 4
			&& !t.neighbors.Values.Any(n => n.HasCity()) && t.neighbors.Values.Count(IsOpenSea) >= 2);
		City enemyCity = CityInteractions.BuildCity(theirs, them, them.GetNextCityName());
		Spawn(them, "Spearman", theirs);
		MapUnit frigate = Spawn(us, "Frigate", SeaNextTo(theirs));
		us.DeclareWarOn(them, gameData.turn);

		await ((C7GameData.UnitAI)new NavalAI()).PlayTurn(us, frigate);

		Assert.True(frigate.hasAttackedThisTurn);
		Assert.Equal(enemyCity, theirs.cityAtTile);
	}

	[Fact]
	public async Task APrivateerRaidsAWeakCivsShipsWithoutWar() {
		(City city, List<Tile> sea) = CoastalCity();
		MapUnit privateer = Spawn(us, "Privateer", sea[0]);
		MapUnit galley = Spawn(them, "Curragh", SeaNextTo(sea[0], sea[0]));
		// A hurt ship, for odds good enough to raid.
		galley.hitPointsRemaining = 1;
		// They have fewer cities than us.
		Assert.True(them.cities.Count * 3 < us.cities.Count * 2);
		Assert.True(us.IsAtPeaceWith(them));

		C7GameData.UnitAI ai = NavalAI.MaybeMake(privateer, us);
		Assert.NotNull(ai);
		await ai.PlayTurn(us, privateer);

		Assert.Contains(fights, f => f.attacker == privateer && f.defender == galley);
		Assert.True(us.IsAtPeaceWith(them));
	}

	[Fact]
	public void APrivateerLeavesAsStrongACivAlone() {
		(City city, List<Tile> sea) = CoastalCity();
		MapUnit privateer = Spawn(us, "Privateer", sea[0]);
		Spawn(them, "Curragh", SeaNextTo(sea[0], sea[0]));
		while (them.cities.Count < us.cities.Count) {
			Tile site = gameData.map.tiles.First(t => t.IsLand() && !t.HasCity() && t.unitsOnTile.Count == 0
				&& !t.GetTilesWithinRankDistance(2).Any(n => n.HasCity()));
			CityInteractions.BuildCity(site, them, them.GetNextCityName());
		}
		Assert.Null(NavalAI.MaybeMake(privateer, us));
	}

	[Fact]
	public void AtWarWithACoastalEnemyTheAIBuildsWarships() {
		(City city, List<Tile> sea) = CoastalCity();
		UnitPrototype frigate = Prototype("Frigate");
		Learn(us, frigate);
		// An enemy ship within reach of our waters.
		Spawn(them, "Galley", sea.Last().neighbors.Values.First(t => IsOpenSea(t) && t.DistanceTo(city.location) == 2));

		Assert.False(NavalAI.WantsWarships(us, city));

		us.DeclareWarOn(them, gameData.turn);
		gameData.turn++;
		Assert.True(NavalAI.WantsWarships(us, city));
		Assert.True(Score(city, frigate) > int.MinValue);
	}

	private float Score(City city, UnitPrototype unit) {
		List<IProducible> options = city.ListProductionOptions(gameData).ToList();
		ChooseProducible.ProducibleStats stats = ChooseProducible.CalculateStats(city, us, options);
		// A defender at home, so the city may build other things.
		if (!city.location.unitsOnTile.Any(u => u.CanDefendOnLand())) {
			Spawn(us, "Spearman", city.location);
			stats = ChooseProducible.CalculateStats(city, us, options);
		}
		return ChooseProducible.ScoreUnit(stats, city, us, unit);
	}
}
