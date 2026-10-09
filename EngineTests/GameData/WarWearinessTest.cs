using C7GameData;
using C7GameData.Save;
using Xunit;

namespace EngineTests.GameData;

public class WarWearinessTest {
	private static City MakeCity(int warWearinessLevel, int weariness, int size = 8) {
		C7Engine.EngineStorage.InitializeGameDataForTests(new C7GameData.GameData());
		Player player = new() {
			civilization = new Civilization(),
			government = new Government() { warWeariness = warWearinessLevel },
			warWeariness = weariness,
		};
		City city = new(Tile.NONE, player, "Weary", ID.None("city"));
		player.cities.Add(city);
		for (int i = 0; i < size; ++i) {
			city.residents.Add(new CityResident());
		}
		return city;
	}

	[Fact]
	public void GovernmentsWithoutWarWearinessIgnoreIt() {
		City city = MakeCity(warWearinessLevel: 0, weariness: 100);
		Assert.Equal(0, city.owner.WarWearinessUnhappiness(city));
	}

	[Fact]
	public void HighWarWearinessHurtsMoreThanLow() {
		City low = MakeCity(warWearinessLevel: 1, weariness: 40);
		City high = MakeCity(warWearinessLevel: 2, weariness: 40);
		Assert.Equal(2, low.owner.WarWearinessUnhappiness(low));
		Assert.Equal(4, high.owner.WarWearinessUnhappiness(high));
	}

	[Fact]
	public void PoliceStationsTakeAQuarterOff() {
		City city = MakeCity(warWearinessLevel: 2, weariness: 40);
		SaveBuilding police = new() { name = "Police Station" };
		police.flags.Add(SaveBuilding.Flag.ReducesWarWeariness);
		city.AddBuilding(new Building(police, new C7GameData.GameData()));
		// Half of 8 citizens at level 1 in a democracy, less a quarter.
		Assert.Equal(3, city.owner.WarWearinessUnhappiness(city));
	}

	[Theory]
	[InlineData(-1, -1)]
	[InlineData(0, 0)]
	[InlineData(30, 0)]
	[InlineData(31, 1)]
	[InlineData(60, 1)]
	[InlineData(61, 2)]
	[InlineData(90, 2)]
	[InlineData(91, 3)]
	[InlineData(120, 3)]
	[InlineData(121, 4)]
	[InlineData(1000, 4)]
	public void LevelsFollowTheArticle(int points, int level) {
		Assert.Equal(level, Player.WarWearinessLevel(points));
	}

	// A player at war with another, both with relationships to each other.
	private static (City city, Player enemy, C7GameData.GameData gameData) AtWar(int warWearinessLevel, int size = 8) {
		City city = MakeCity(warWearinessLevel, weariness: 0, size);
		Player us = city.owner;
		us.id = ID.FromString("player-1");
		Player enemy = new() {
			id = ID.FromString("player-2"),
			civilization = new Civilization(),
			government = new Government(),
		};
		C7GameData.GameData gameData = C7Engine.EngineStorage.gameData;
		gameData.players.Add(us);
		gameData.players.Add(enemy);
		us.playerRelationships[enemy.id] = new PlayerRelationship();
		enemy.playerRelationships[us.id] = new PlayerRelationship();
		return (city, enemy, gameData);
	}

	[Fact]
	public void EachEnemyCountsSeparately() {
		(City city, Player enemy, _) = AtWar(warWearinessLevel: 1);
		Player us = city.owner;
		Player other = new() { id = ID.FromString("player-3"), civilization = new Civilization() };
		us.playerRelationships[other.id] = new PlayerRelationship();

		// 40 points against each is level 1 twice: a quarter of 8, twice.
		us.AddWarWeariness(enemy, 40);
		us.AddWarWeariness(other, 40);
		Assert.Equal(4, us.WarWearinessUnhappiness(city));

		// 80 against one is level 2: a half of 8, the same.
		us.AddWarWeariness(other, -40);
		us.AddWarWeariness(enemy, 40);
		Assert.Equal(4, us.WarWearinessUnhappiness(city));
	}

	[Fact]
	public void CombatAddsWarWearinessToBothSides() {
		(City city, Player enemy, _) = AtWar(warWearinessLevel: 1);
		Player us = city.owner;
		UnitPrototype warrior = new() { name = "Warrior", attack = 1, defense = 1 };
		MapUnit ours = new(ID.None("unit")) { owner = us, unitType = warrior };
		MapUnit theirs = new(ID.None("unit")) { owner = enemy, unitType = warrior };

		// Our unit attacked theirs and lost: 2 for the attack on their unit,
		// which can defend, and 2 for our defeated attacker.
		Player.AddWarWearinessForAttack(ours, theirs, attackerDefeated: true);
		Assert.Equal(2, us.WarWearinessPointsAgainst(enemy));
		Assert.Equal(2, enemy.WarWearinessPointsAgainst(us));

		// A unit that can't defend being attacked adds nothing.
		MapUnit worker = new(ID.None("unit")) { owner = enemy, unitType = new UnitPrototype() { name = "Worker" } };
		Player.AddWarWearinessForAttack(ours, worker, attackerDefeated: false);
		Assert.Equal(2, enemy.WarWearinessPointsAgainst(us));
	}

	[Fact]
	public void LosingACityAddsSixteenOrSeventeen() {
		(City city, Player enemy, _) = AtWar(warWearinessLevel: 1);
		Player us = city.owner;
		Player.AddWarWearinessForLostCity(us, enemy, size: 1);
		Assert.Equal(16, us.WarWearinessPointsAgainst(enemy));
		Player.AddWarWearinessForLostCity(us, enemy, size: 5);
		Assert.Equal(33, us.WarWearinessPointsAgainst(enemy));
		Assert.Equal(0, enemy.WarWearinessPointsAgainst(us));
	}

	[Fact]
	public void QuietTurnsAtWarWearItDownByOne() {
		(City city, Player enemy, C7GameData.GameData gameData) = AtWar(warWearinessLevel: 1);
		Player us = city.owner;
		us.AddWarWeariness(enemy, 40);
		us.UpdateWarWeariness(gameData);
		Assert.Equal(39, us.WarWearinessPointsAgainst(enemy));

		// Not below level 1, though.
		us.AddWarWeariness(enemy, 31 - 39);
		us.UpdateWarWeariness(gameData);
		Assert.Equal(30, us.WarWearinessPointsAgainst(enemy));
		us.UpdateWarWeariness(gameData);
		Assert.Equal(30, us.WarWearinessPointsAgainst(enemy));
	}

	[Fact]
	public void WarHappinessMakesAQuarterHappy() {
		(City city, Player enemy, _) = AtWar(warWearinessLevel: 0);
		Player us = city.owner;
		us.AddWarWeariness(enemy, Player.WarWearinessWhenTheAIAttacks);
		Assert.Equal(2, us.WarHappiness(city));
		Assert.Equal(0, us.WarWearinessUnhappiness(city));
	}

	[Fact]
	public void UnhappinessIsCappedAtCitySize() {
		City city = MakeCity(warWearinessLevel: 2, weariness: 1000, size: 3);
		Assert.Equal(3, city.owner.WarWearinessUnhappiness(city));
	}

	[Fact]
	public void WarWearinessAgainstACivFadesAtPeace() {
		(City city, Player enemy, C7GameData.GameData gameData) = AtWar(warWearinessLevel: 2);
		Player us = city.owner;
		// At peace: a peace treaty is a multi-turn deal.
		us.playerRelationships[enemy.id].multiTurnDeals.Add(MultiTurnDeal.DEFAULT_PEACE);
		us.AddWarWeariness(enemy, 121);

		// A twentieth, rounded up, each turn: 7 of 121. (The article's own
		// table of turns loses 8 here, and is gone in 43 turns rather than
		// 48; we keep its stated rule, as the project owner asked.)
		us.UpdateWarWeariness(gameData);
		Assert.Equal(114, us.WarWearinessPointsAgainst(enemy));
		for (int turn = 1; turn < 47; ++turn) {
			us.UpdateWarWeariness(gameData);
		}
		Assert.True(us.WarWearinessPointsAgainst(enemy) > 0);
		us.UpdateWarWeariness(gameData);
		Assert.Equal(0, us.WarWearinessPointsAgainst(enemy));
	}

	[Fact]
	public void WarWearinessFadesAtPeace() {
		City city = MakeCity(warWearinessLevel: 2, weariness: 50);
		C7GameData.GameData gameData = new();
		gameData.players.Add(city.owner);

		// A twentieth, rounded up, fades each turn...
		city.owner.UpdateWarWeariness(gameData);
		Assert.Equal(47, city.owner.warWeariness);

		// ...so it is gone after a few dozen turns.
		for (int i = 0; i < 40; ++i) {
			city.owner.UpdateWarWeariness(gameData);
		}
		Assert.Equal(0, city.owner.warWeariness);
	}
}
