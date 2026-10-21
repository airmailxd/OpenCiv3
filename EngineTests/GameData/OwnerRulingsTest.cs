using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// The project owner's rulings of 2026-10-21: shields lost when a rival
// completes a wonder, the AI hurrying improvements, armies' members and unit
// support, slaves from razing, settlers in cities that can't grow, golden
// age triggers, mobilization, and obsolete wonders losing their effects.
public class OwnerRulingsTest : IClassFixture<SaveGameFixture>, System.IDisposable {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;

	public OwnerRulingsTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman).Take(2).ToArray();
		us = civs[0];
		them = civs[1];
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private Building BuildingNamed(string name) => gameData.Buildings.Single(b => b.name == name);
	private UnitPrototype Prototype(string name) => gameData.unitPrototypes.Single(p => p.name == name);
	private Tech TechNamed(string name) => gameData.techs.Single(t => t.Name == name);

	private static bool IsEmptyLand(Tile t) {
		return t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp && !t.IsImpassable();
	}

	private City BuildCity(Player owner, System.Func<Tile, bool> where = null) {
		Tile tile = gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities()
			&& t.neighbors.Values.All(n => n == Tile.NONE || !n.HasCity())
			&& t.GetTilesWithinRankDistance(2).All(n => n == Tile.NONE || !n.HasCity())
			&& (where == null || where(t)));
		return CityInteractions.BuildCity(tile, owner, $"City {owner.cities.Count}");
	}

	private void GrowTo(City city, int size, CitizenType type = null) {
		while (city.residents.Count < size) {
			CityResident r = new() {
				city = city,
				nationality = city.owner.civilization,
				citizenType = type ?? gameData.citizenTypes.Find(x => x.IsDefaultCitizen),
			};
			city.AddCitizen(r);
			if (type == null) {
				CityTileAssignmentAI.AssignNewCitizenToTile(gameData, r);
			}
		}
	}

	// ---- 1. A wonder lost to a rival ----

	[Fact]
	public void ShieldsBeyondTheNewItemAreLostWhenARivalCompletesTheWonder() {
		Building wonder = gameData.Buildings.First(b => b.IsGreatWonder() && them.ShieldCost(b) > 150);
		Building barracks = BuildingNamed("Barracks");
		City ours = BuildCity(us);
		City theirs = BuildCity(them);

		ours.SetItemBeingProduced(wonder);
		ours.SetStoredShields(150);
		ours.EnqueueProduction(barracks);
		theirs.SetItemBeingProduced(wonder);
		theirs.SetStoredShields(them.ShieldCost(wonder));

		theirs.HandleCityProduction(gameData);

		Assert.Contains(wonder.name, gameData.GreatWondersBuilt);
		Assert.Equal(barracks, ours.itemBeingProduced);
		// Lost at the switch, so switching again can't win them back.
		Assert.Equal(us.ShieldCost(barracks), ours.shieldsStored);
	}

	// ---- 2. The AI hurries improvements ----

	private Government PaidLabor() => gameData.governments.First(g => g.hurryingType == Government.HurryProductionType.PaidLabor && !g.transitionType);

	private City CityBuildingTemple(int gold) {
		us.government = PaidLabor();
		City city = BuildCity(us);
		// A defender, so the city has no unit to hurry first.
		gameData.SpawnUnit(us, Prototype("Spearman"), city.location);
		Building temple = BuildingNamed("Temple");
		us.knownTechs.Add(temple.requiredTech.id);
		city.SetItemBeingProduced(temple);
		city.SetStoredShields(5);
		us.gold = gold;
		return city;
	}

	[Fact]
	public void TheAiHurriesAnImprovementAboveItsReserve() {
		City city = CityBuildingTemple(5000);
		AiExperiments.Enable(us, DevelopmentAI.ImprovementHurry);
		int reserve = DevelopmentAI.StockGoldReserve(us);
		Assert.True(us.gold > reserve);

		DevelopmentAI.HurryProduction(us);

		Assert.True(city.hurriedThisTurn);
		Assert.True(us.gold >= reserve);
	}

	[Fact]
	public void TheAiKeepsItsReserveRatherThanHurryAnImprovement() {
		City city = CityBuildingTemple(0);
		AiExperiments.Enable(us, DevelopmentAI.ImprovementHurry);
		us.gold = DevelopmentAI.StockGoldReserve(us) + 1;

		DevelopmentAI.HurryProduction(us);

		Assert.False(city.hurriedThisTurn);
	}

	// ---- 3. An army's members need support ----

	[Fact]
	public void UnitsInAnArmyCountTowardUnitSupport() {
		City city = BuildCity(us);
		int before = us.TotalUnitsAllowedUnitsAndSupportCostRaw().Item1;
		MapUnit army = gameData.SpawnUnit(us, Prototype("Army"), city.location);
		int withArmy = us.TotalUnitsAllowedUnitsAndSupportCostRaw().Item1;
		for (int i = 0; i < 2; i++) {
			MapUnit member = gameData.SpawnUnit(us, Prototype("Swordsman"), city.location);
			army.Board(member);
			Assert.True(member.IsInArmy());
		}

		Assert.Equal(withArmy + 2, us.TotalUnitsAllowedUnitsAndSupportCostRaw().Item1);
		Assert.True(withArmy >= before);
	}

	// ---- 4. Razing gives slaves ----

	[Fact]
	public void RazingACityGivesASlaveWorkerForEachCitizenOverOne() {
		BuildCity(us);
		BuildCity(them);
		City city = BuildCity(them);
		GrowTo(city, 5);
		Tile tile = city.location;

		CityInteractions.CaptureCity(city, us);
		Assert.Equal(4, city.residents.Count);
		Assert.True(CityInteractions.MayRaze(us, city, gameData));

		List<MapUnit> slaves = CityInteractions.RazeCity(city);

		Assert.False(tile.HasCity());
		Assert.Equal(3, slaves.Count);
		Assert.All(slaves, s => {
			Assert.True(s.unitType.isWorker);
			Assert.Equal(us, s.owner);
			Assert.Equal(tile, s.location);
			Assert.Same(them.civilization, s.nationality);
			Assert.True(s.IsCaptive());
		});
	}

	[Fact]
	public void OnlyACityJustTakenCanBeRazed() {
		BuildCity(us);
		City own = BuildCity(us);
		Assert.False(CityInteractions.MayRaze(us, own, gameData));
	}

	// ---- 5. Settlers in cities that can't grow ----

	[Fact]
	public void ASizeTwoCityThatCannotGrowIsNotOfferedTheSettler() {
		UnitPrototype settler = Prototype("Settler");
		CitizenType specialist = gameData.citizenTypes.First(c => !c.IsDefaultCitizen);
		BuildCity(us);
		City city = BuildCity(us);
		city.RemoveAllCitizens();
		// Entertainers work no tiles, so the city eats more than it makes.
		GrowTo(city, 2, specialist);
		Assert.True(city.FoodGrowthPerTurn() <= 0);
		Assert.False(city.CanGrow());
		Assert.DoesNotContain(settler, city.ListProductionOptions(gameData));
		Assert.NotEqual(settler, ChooseProducible.Choose(city, us));

		// A bigger city can pay for it.
		GrowTo(city, 3, specialist);
		Assert.Contains(settler, city.ListProductionOptions(gameData));
	}

	[Fact]
	public void ASmallCityThatIsGrowingKeepsTheSettler() {
		UnitPrototype settler = Prototype("Settler");
		City city = BuildCity(us, t => t.FoodYield(us).yield >= 2);
		Assert.True(city.CanGrow());
		Assert.Contains(settler, city.ListProductionOptions(gameData));
	}

	// ---- 6. Golden ages ----

	[Fact]
	public void SmallWondersNeverTriggerAGoldenAge() {
		Building small = gameData.Buildings.First(b => b.isSmallWonder && !b.isCenterOfEmpire && !b.IsSpaceshipPart && b.requiredBuilding == null);
		City city = BuildCity(us);
		// No small wonder counts as a great one toward the civ's traits.
		Assert.All(gameData.Buildings.Where(b => b.isSmallWonder), b => Assert.False(b.IsGreatWonder()));
		city.SetItemBeingProduced(small);
		city.SetStoredShields(us.ShieldCost(small));
		city.HandleCityProduction(gameData);
		Assert.False(us.InGoldenAge);
	}

	[Fact]
	public void CapturingWondersDoesNotStartAGoldenAgeByItself() {
		List<Building> wonders = gameData.Buildings.Where(b => b.IsGreatWonder() && b.traits.Overlaps(us.civilization.traits)).ToList();
		BuildCity(us);
		BuildCity(them);
		City city = BuildCity(them);
		GrowTo(city, 3);
		foreach (Building w in wonders) {
			city.AddBuilding(w);
		}

		CityInteractions.CaptureCity(city, us);

		Assert.Equal(us, city.owner);
		Assert.False(us.InGoldenAge);
		// The check runs when the civ next builds a great wonder itself, and
		// the captured ones count toward its traits.
		us.MaybeStartGoldenAgeFromWonders(gameData);
		Assert.True(us.InGoldenAge);
	}

	private class ScriptedRandom(double rest) : System.Random {
		protected override double Sample() => rest;
		public override double NextDouble() => Sample();
	}

	[Fact]
	public async Task AUniqueUnitWinningABattleStartsAGoldenAge() {
		UnitPrototype unique = gameData.unitPrototypes.First(p => p.startsGoldenAge && p.attack > 0 && p.IsLandUnit());
		us.DeclareWarOn(them, gameData.turn);
		Tile from = gameData.map.tiles.First(t => IsEmptyLand(t) && t.OwningPlayer() == null
			&& t.neighbors.Values.Any(n => n != Tile.NONE && IsEmptyLand(n) && n.OwningPlayer() == null));
		Tile to = from.neighbors.Values.First(n => n != Tile.NONE && IsEmptyLand(n) && n.OwningPlayer() == null);
		MapUnit attacker = gameData.SpawnUnit(us, unique, from);
		MapUnit defender = gameData.SpawnUnit(them, Prototype("Warrior"), to);

		System.Random original = C7GameData.GameData.rng;
		C7GameData.GameData.rng = new ScriptedRandom(0.0);
		try {
			CombatResult result = await attacker.Fight(defender);
			Assert.Equal(CombatResult.DefenderKilled, result);
		} finally {
			C7GameData.GameData.rng = original;
		}
		Assert.True(us.InGoldenAge);
	}

	// ---- 7. Mobilization ----

	private void LearnNationalism(Player player) {
		foreach (Tech t in gameData.techs.Where(t => t.HasFlag(SaveTech.Flag.EnablesMobilization))) {
			player.knownTechs.Add(t.id);
		}
	}

	[Fact]
	public void NationalismEnablesMobilization() {
		Tech nationalism = gameData.techs.Single(t => t.HasFlag(SaveTech.Flag.EnablesMobilization));
		Assert.Equal("Nationalism", nationalism.Name);
		Assert.False(Mobilization.CanMobilize(us));
		LearnNationalism(us);
		Assert.True(Mobilization.CanMobilize(us));
	}

	[Fact]
	public void AMobilizedCivBuildsOnlyMilitaryImprovements() {
		City city = BuildCity(us);
		Building temple = BuildingNamed("Temple");
		Building barracks = BuildingNamed("Barracks");
		us.knownTechs.Add(temple.requiredTech.id);
		LearnNationalism(us);
		city.SetItemBeingProduced(temple);
		Assert.True(Mobilization.Mobilize(us));

		List<IProducible> options = city.ListProductionOptions(gameData).ToList();
		Assert.Contains(barracks, options);
		Assert.Contains(Prototype("Settler"), options);
		Assert.Contains(Prototype("Warrior"), options);
		// The temple under way may be finished, but not started elsewhere.
		Assert.Contains(temple, options);
		City other = BuildCity(us);
		Assert.DoesNotContain(temple, other.ListProductionOptions(gameData));
		Assert.False(other.CanChangeProduction(temple));
	}

	[Fact]
	public void MobilizationAddsAShieldOnlyForAttackingUnits() {
		City city = BuildCity(us, t => t.ProductionYield(us).yield >= 1);
		Tile center = city.location;
		LearnNationalism(us);
		Mobilization.Mobilize(us);

		city.SetItemBeingProduced(Prototype("Worker"));
		int forWorker = center.ProductionYield(city).yield;
		city.SetItemBeingProduced(Prototype("Warrior"));
		int forWarrior = center.ProductionYield(city).yield;

		Assert.Equal(forWorker + 1, forWarrior);
	}

	[Fact]
	public void MobilizationHalvesCultureAndLastsUntilPeace() {
		City city = BuildCity(us);
		city.AddBuilding(BuildingNamed("Temple"));
		int culture = city.GetCulturePerTurn();
		Assert.True(culture > 0);
		LearnNationalism(us);
		us.DeclareWarOn(them, gameData.turn);
		Mobilization.Mobilize(us);

		Assert.Equal(culture / 2, city.GetCulturePerTurn());
		Assert.False(Mobilization.CanDemobilize(us));
		Assert.False(Mobilization.Demobilize(us));

		Mobilization.OnWarEnded(us);
		Assert.True(Mobilization.Demobilize(us));
		Assert.False(us.mobilized);
		Assert.Equal(culture, city.GetCulturePerTurn());
	}

	[Fact]
	public void MobilizationIsSaved() {
		LearnNationalism(us);
		Mobilization.Mobilize(us);
		Mobilization.OnWarEnded(us);

		C7GameData.GameData loaded = SaveGame.FromGameData(gameData).ToGameData(fixture.behaviors);
		Player again = loaded.players.Single(p => p.id == us.id);
		Assert.True(again.mobilized);
		Assert.True(again.mayEndMobilization);
	}

	// ---- 8. Pollution starts over size 12 (see PollutionTest) ----

	// ---- 9. Obsolete wonders ----

	[Fact]
	public void TheColossusStopsAddingCommerceOnceObsoleteButKeepsItsCulture() {
		Building colossus = BuildingNamed("The Colossus");
		Assert.NotNull(colossus.renderedObsoleteBy);
		// Despotism's tile penalty would take the Colossus's commerce back.
		us.government = gameData.governments.First(g => !g.hasTilePenalty && !g.transitionType);
		City city = BuildCity(us, t => t.NeighborsWater());
		Tile ocean = city.location.neighbors.Values.First(n => n != Tile.NONE && n.IsWater());
		int without = ocean.CommerceYield(city).yield;
		city.AddBuilding(colossus);
		int with = ocean.CommerceYield(city).yield;
		int culture = city.GetCulturePerTurnRaw();
		Assert.Equal(without + 1, with);

		us.knownTechs.Add(colossus.renderedObsoleteBy.id);

		Assert.True(colossus.isGreatWonderObsolete(us));
		Assert.Equal(without, ocean.CommerceYield(city).yield);
		Assert.Equal(culture, city.GetCulturePerTurnRaw());
		// It is still the city's: listed, and not built again.
		Assert.Contains(city.GetBuildings(), cb => cb.building == colossus);
	}
}
