using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

// The AI drafting defenders in a city in danger (see DraftAI).
public class DraftAITest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player player;
	private readonly Player barbarians;
	private readonly City city;

	public DraftAITest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		player = gameData.players.First(p => !p.isBarbarians && !p.isHuman && p.units.Any(u => u.unitType.isSettler));
		barbarians = gameData.players.First(p => p.isBarbarians);
		Tile tile = player.units.First(u => u.unitType.isSettler).location;
		city = CityInteractions.BuildCity(tile, player, player.GetNextCityName());
		player.government = gameData.governments.First(g => g.name == "Monarchy");
		player.knownTechs.Add(gameData.techs.First(t => t.HasFlag(SaveTech.Flag.EnablesConscription)).id);
		player.knownTechs.Add(gameData.unitPrototypes.First(u => u.name == "Spearman").requiredTech.id);
		// Only drafted units defend the city.
		foreach (MapUnit u in city.location.unitsOnTile.ToList()) {
			u.RemoveFromPlay();
		}
		while (city.residents.Count < 9) {
			CityResident r = new() {
				city = city,
				nationality = player.civilization,
				citizenType = gameData.citizenTypes.Find(x => x.IsDefaultCitizen),
			};
			city.AddCitizen(r);
			CityTileAssignmentAI.AssignNewCitizenToTile(gameData, r);
		}
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private void BarbariansNextToTheCity(int count) {
		Tile next = city.location.neighbors.Values.First(t => t != Tile.NONE && t.IsLand() && !t.IsImpassable());
		UnitPrototype warrior = gameData.unitPrototypes.First(p => p.name == "Warrior");
		for (int i = 0; i < count; ++i) {
			gameData.SpawnUnit(barbarians, warrior, next);
		}
	}

	[Fact]
	public void ACityInDangerDraftsDefenders() {
		BarbariansNextToTheCity(2);
		int size = city.residents.Count;

		List<MapUnit> drafted = DraftAI.PlayTurn(player, gameData);

		// As many as the attackers, within the government's limit.
		Assert.Equal(Math.Min(2, player.government.draftLimit), drafted.Count);
		Assert.All(drafted, u => Assert.Equal(city.location, u.location));
		Assert.Equal(size - drafted.Count, city.residents.Count);
	}

	[Fact]
	public void NoDraftWithoutDanger() {
		Assert.Empty(DraftAI.PlayTurn(player, gameData));
	}

	[Fact]
	public void NoDraftWhenDefendersAreEnough() {
		BarbariansNextToTheCity(1);
		gameData.SpawnUnit(player, gameData.unitPrototypes.First(p => p.name == "Spearman"), city.location);
		Assert.Empty(DraftAI.PlayTurn(player, gameData));
	}
}
