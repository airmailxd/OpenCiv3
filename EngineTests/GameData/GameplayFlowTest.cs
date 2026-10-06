using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// What happens when a city finishes building something or a tech is
// discovered, and the order units are selected in.
public class GameplayFlowTest : IClassFixture<SaveGameFixture>, System.IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player human;
	private readonly Player ai;

	public GameplayFlowTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		human = gameData.players.First(p => p.isHuman);
		ai = gameData.players.First(p => !p.isHuman && !p.isBarbarians);
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private static bool IsEmptyLand(Tile t) {
		return t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp && !t.IsImpassable();
	}

	private City BuildCity(Player owner) {
		Tile tile = gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities()
			&& t.neighbors.Values.All(n => n == Tile.NONE || !n.HasCity()));
		return CityInteractions.BuildCity(tile, owner, $"City {owner.cities.Count}");
	}

	private void Finish(City city, IProducible item) {
		city.SetItemBeingProduced(item);
		city.SetStoredShields(city.owner.ShieldCost(item));
		city.HandleCityProduction(gameData);
	}

	private List<MessageToUI> DrainMessages() {
		List<MessageToUI> messages = new();
		while (EngineStorage.TryDequeueNextMessageToUI(out MessageToUI msg)) {
			messages.Add(msg);
		}
		return messages;
	}

	[Fact]
	public void HumanCityBuildsTheSameUnitAgain() {
		City city = BuildCity(human);
		UnitPrototype unit = city.ListProductionOptions(gameData).OfType<UnitPrototype>().First(u => u.populationCost == 0);
		DrainMessages();

		Finish(city, unit);

		Assert.Same(unit, city.itemBeingProduced);
		MsgCityProductionCompleted msg = DrainMessages().OfType<MsgCityProductionCompleted>().Single();
		Assert.Same(city, msg.city);
		Assert.Same(human, msg.recipient);
		Assert.Equal(unit.name, msg.completed);
		Assert.Equal(unit.name, msg.next);
	}

	[Fact]
	public void AiCityIsNotToldOfProduction() {
		City city = BuildCity(ai);
		UnitPrototype unit = city.ListProductionOptions(gameData).OfType<UnitPrototype>().First(u => u.populationCost == 0);
		DrainMessages();

		Finish(city, unit);

		Assert.Empty(DrainMessages().OfType<MsgCityProductionCompleted>());
	}

	[Fact]
	public void QueuedItemsAreBuiltInOrder() {
		City city = BuildCity(human);
		List<IProducible> options = city.ListProductionOptions(gameData).ToList();
		UnitPrototype unit = options.OfType<UnitPrototype>().First(u => u.populationCost == 0);
		Building building = options.OfType<Building>().First();
		IProducible other = options.First(o => o != unit && o != building);

		city.EnqueueProduction(building);
		city.EnqueueProduction(other);
		Finish(city, unit);

		Assert.Same(building, city.itemBeingProduced);
		Assert.Equal([other], city.productionQueue);
	}

	[Fact]
	public void QueuedItemThatCanNoLongerBeBuiltIsSkipped() {
		City city = BuildCity(human);
		List<IProducible> options = city.ListProductionOptions(gameData).ToList();
		UnitPrototype unit = options.OfType<UnitPrototype>().First(u => u.populationCost == 0);
		Building building = options.OfType<Building>().First(b => b.greatWonderProperties == null);
		UnitPrototype otherUnit = options.OfType<UnitPrototype>().First(u => u != unit);

		city.EnqueueProduction(building);
		city.EnqueueProduction(otherUnit);
		city.AddBuilding(building);
		Finish(city, unit);

		Assert.Same(otherUnit, city.itemBeingProduced);
		Assert.Empty(city.productionQueue);
	}

	[Fact]
	public void WonderBuiltElsewhereMovesOnToTheQueue() {
		Building wonder = new(new SaveBuilding { name = "Test Wonder", shieldCost = 10, greatWonderProperties = new() }, gameData);
		City builder = BuildCity(ai);
		City rival = BuildCity(human);
		Building next = rival.ListProductionOptions(gameData).OfType<Building>().First(b => b.greatWonderProperties == null);
		rival.SetItemBeingProduced(wonder);
		rival.EnqueueProduction(next);

		Finish(builder, wonder);

		Assert.Contains(wonder.name, gameData.GreatWondersBuilt);
		Assert.Same(next, rival.itemBeingProduced);
		Assert.Empty(rival.productionQueue);
	}

	[Fact]
	public void ProductionQueueIsSaved() {
		City city = BuildCity(human);
		List<IProducible> options = city.ListProductionOptions(gameData).ToList();
		UnitPrototype unit = options.OfType<UnitPrototype>().First();
		Building building = options.OfType<Building>().First();
		city.EnqueueProduction(building);
		city.EnqueueProduction(unit);

		City loaded = new SaveCity(city).ToCity(gameData.map, gameData.players, gameData.unitPrototypes,
			gameData.civilizations, gameData.Buildings, gameData.citizenTypes, gameData.Inflows);

		Assert.Equal([building, unit], loaded.productionQueue);
	}

	private Tech AvailableTech(Player player) {
		return player.GetAvailableTechsToResearch(gameData.techs).First();
	}

	[Fact]
	public void HumanIsAskedWhatToResearchNext() {
		Tech tech = AvailableTech(human);
		human.ResearchQueue.Clear();
		human.freeTechsRemaining = 0;
		human.SetCurrentlyResearchedTech(tech.id);

		human.AcquireTech(gameData, tech);

		Assert.Null(human.currentlyResearchedTech);
		Assert.Same(tech, human.lastDiscoveredTech);

		// Choosing the next tech forgets the one discovered.
		human.SetCurrentlyResearchedTech(AvailableTech(human).id);
		Assert.Null(human.lastDiscoveredTech);
	}

	[Fact]
	public void HumanCarriesOnWithTheirResearchQueue() {
		List<Tech> available = human.GetAvailableTechsToResearch(gameData.techs).Take(2).ToList();
		human.ResearchQueue.Clear();
		human.freeTechsRemaining = 0;
		human.AddTechItemToResearchQueue(available[0]);
		human.AddTechItemToResearchQueue(available[1]);
		human.SetCurrentlyResearchedTech(available[0].id);

		human.AcquireTech(gameData, available[0]);

		Assert.Equal(available[1].id, human.currentlyResearchedTech);
	}

	[Fact]
	public void AiPicksItsNextResearch() {
		Tech tech = AvailableTech(ai);
		ai.ResearchQueue.Clear();
		ai.freeTechsRemaining = 0;
		ai.SetCurrentlyResearchedTech(tech.id);

		ai.AcquireTech(gameData, tech);

		Assert.NotNull(ai.currentlyResearchedTech);
	}

	[Fact]
	public void UnitsInThePreviousStackAreSelectedFirst() {
		ID original = EngineStorage.uiControllerID;
		EngineStorage.uiControllerID = human.id;
		try {
			UnitInteractions.ClearWaitQueue();
			List<MapUnit> selectable = human.units
				.Where(u => u.movementPoints.canMove && !u.isFortified && !u.IsLockedInArmy() && !u.IsBusy())
				.ToList();
			Assert.NotEmpty(selectable);
			MapUnit last = selectable.Last();

			// With no stack, or a stack with no one to select, it's the first
			// unit as before.
			Assert.Same(selectable[0], UnitInteractions.getNextSelectedUnit());
			Assert.Same(selectable[0], UnitInteractions.getNextSelectedUnit(Tile.NONE, last.unitType));

			// The same type in the stack first, then anyone in the stack.
			Assert.Same(selectable.First(u => u.location == last.location && u.unitType == last.unitType),
				UnitInteractions.getNextSelectedUnit(last.location, last.unitType));
			Assert.Same(selectable.First(u => u.location == last.location),
				UnitInteractions.getNextSelectedUnit(last.location, null));
		} finally {
			UnitInteractions.ClearWaitQueue();
			EngineStorage.uiControllerID = original;
		}
	}
}
