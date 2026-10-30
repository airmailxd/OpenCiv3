using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

// The early-rush switch (see EarlyRush): a civ still expanding plans a rush
// on a weakly held city of size 3 or more; without the switch it plans none.
public sealed class EarlyRushTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;
	private readonly Random originalRandom;

	// Every roll fails, so Civ3's war plotting starts nothing by itself.
	private class HighRandom : Random {
		protected override double Sample() => 0.999999;
	}

	public EarlyRushTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		originalRandom = gameData.random;
		gameData.random = new HighRandom();
		Player[] civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman).Take(2).ToArray();
		us = civs[0];
		them = civs[1];
		us.EnsureRelationshipExists(them);
		them.EnsureRelationshipExists(us);
	}

	public void Dispose() {
		gameData.random = originalRandom;
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private City BuildCity(Player owner, Func<Tile, bool> where) {
		Tile tile = gameData.map.tiles.First(t => t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp
			&& !t.IsImpassable() && t.IsAllowCities() && t.neighbors.Values.All(n => n == Tile.NONE || !n.HasCity()) && where(t));
		return CityInteractions.BuildCity(tile, owner, $"City {owner.cities.Count}");
	}

	// One city each on the same landmass, theirs of the given size.
	private (City ours, City theirs) OneCityEach(int theirSize) {
		foreach (City c in us.cities.ToList()) {
			CityInteractions.DestroyCity(c);
		}
		City ours = BuildCity(us, _ => true);
		List<City> old = them.cities.ToList();
		City theirs = BuildCity(them, t => t.continent == ours.location.continent && t.DistanceTo(ours.location) > 4);
		foreach (City c in old) {
			CityInteractions.DestroyCity(c);
		}
		CityResident template = theirs.residents[0];
		while (theirs.residents.Count < theirSize) {
			theirs.AddCitizen(new CityResident { city = theirs, nationality = them.civilization, citizenType = template.citizenType });
		}
		while (theirs.residents.Count > theirSize) {
			theirs.residents.RemoveAt(theirs.residents.Count - 1);
		}
		return (ours, theirs);
	}

	// We learn the tech of the best attacker we could build, and they know
	// none of the techs we have that they don't.
	private void GiveUsTheBetterAttackers() {
		foreach (UnitPrototype type in gameData.unitPrototypes.Where(u => u.IsLandUnit() && !u.isArmy && u.attack >= 3 && u.requiredTech != null).OrderBy(u => u.attack)) {
			us.knownTechs.Add(type.requiredTech.id);
			them.knownTechs.Remove(type.requiredTech.id);
			if (EarlyRush.Outteched(us, them)) {
				return;
			}
		}
	}

	[Fact]
	public async Task TheSwitchStartsARushWhileExpandingAndOffDoesNot() {
		OneCityEach(3);
		GiveUsTheBetterAttackers();
		Assert.True(EarlyRush.Outteched(us, them), $"attack {EarlyRush.BestAttack(us)} vs defence {EarlyRush.BestDefense(them)}");
		await WarCampaign.PlayTurn(us, gameData);
		Assert.False(WarCampaign.HasCampaign(us));

		AiExperiments.Enable(us, EarlyRush.Switch);
		await WarCampaign.PlayTurn(us, gameData);
		Assert.True(WarCampaign.HasRush(us));
		Assert.True(WarCampaign.IsPlannedTarget(us, them));
	}

	[Fact]
	public async Task ASmallCityIsNotRushed() {
		OneCityEach(2);
		GiveUsTheBetterAttackers();
		AiExperiments.Enable(us, EarlyRush.Switch);
		await WarCampaign.PlayTurn(us, gameData);
		Assert.False(WarCampaign.HasCampaign(us));
	}
}
