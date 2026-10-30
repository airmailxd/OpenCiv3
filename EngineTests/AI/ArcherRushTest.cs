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

// The archer-rush switch (see ArcherRush): early in the game, a civ with an
// attacker that beats a rival's best defence plans a rush on that rival's
// nearest city, whatever its size, if it is within 10 tiles; without the
// switch, or with the rival's city further off, it plans none.
public sealed class ArcherRushTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;
	private readonly Random originalRandom;

	// Every roll fails, so Civ3's war plotting starts nothing by itself.
	private class HighRandom : Random {
		protected override double Sample() => 0.999999;
	}

	public ArcherRushTest(SaveGameFixture fixture) {
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

	// One city each on the same landmass, theirs between min and max tiles
	// from ours.
	private (City ours, City theirs) OneCityEach(int min, int max) {
		foreach (City c in us.cities.ToList()) {
			CityInteractions.DestroyCity(c);
		}
		City ours = BuildCity(us, _ => true);
		List<City> old = them.cities.ToList();
		City theirs = BuildCity(them, t => t.continent == ours.location.continent && t.DistanceTo(ours.location) >= min && t.DistanceTo(ours.location) <= max);
		foreach (City c in old) {
			CityInteractions.DestroyCity(c);
		}
		while (theirs.residents.Count > 1) {
			theirs.residents.RemoveAt(theirs.residents.Count - 1);
		}
		return (ours, theirs);
	}

	// We learn the tech of the best attacker we could build (archers in the
	// default rules), and they know none of the techs we have that they
	// don't.
	private void GiveUsArchers() {
		foreach (UnitPrototype type in gameData.unitPrototypes.Where(u => u.IsLandUnit() && !u.isArmy && u.attack >= ArcherRush.WantedAttack && u.requiredTech != null).OrderBy(u => u.attack)) {
			us.knownTechs.Add(type.requiredTech.id);
			them.knownTechs.Remove(type.requiredTech.id);
			if (ArcherRush.Beats(us, them)) {
				return;
			}
		}
	}

	[Fact]
	public async Task TheSwitchStartsARushOnANearCivAndOffDoesNot() {
		OneCityEach(5, 10);
		GiveUsArchers();
		Assert.True(ArcherRush.Beats(us, them), $"attack {EarlyRush.BestAttack(us)} vs defence {EarlyRush.BestDefense(them)}");
		Assert.True(EarlyRush.BestAttack(us) >= ArcherRush.WantedAttack);
		await WarCampaign.PlayTurn(us, gameData);
		Assert.False(WarCampaign.HasCampaign(us));

		AiExperiments.Enable(us, ArcherRush.Switch);
		await WarCampaign.PlayTurn(us, gameData);
		Assert.True(WarCampaign.HasArcherRush(us));
		Assert.True(WarCampaign.IsPlannedTarget(us, them));
	}

	[Fact]
	public async Task ACivOverTenTilesOffIsNotRushed() {
		OneCityEach(11, 40);
		GiveUsArchers();
		AiExperiments.Enable(us, ArcherRush.Switch);
		await WarCampaign.PlayTurn(us, gameData);
		Assert.False(WarCampaign.HasCampaign(us));
	}

	[Fact]
	public async Task NoRushAfterTheFirstHundredTurns() {
		OneCityEach(5, 10);
		GiveUsArchers();
		AiExperiments.Enable(us, ArcherRush.Switch);
		gameData.turn = ArcherRush.WindowTurns + 1;
		await WarCampaign.PlayTurn(us, gameData);
		Assert.False(WarCampaign.HasCampaign(us));
	}

	[Fact]
	public void ResearchWantsTheAttackerWeLack() {
		OneCityEach(5, 10);
		AiExperiments.Enable(us, ArcherRush.Switch);
		foreach (UnitPrototype type in gameData.unitPrototypes.Where(u => u.IsLandUnit() && u.attack >= ArcherRush.WantedAttack && u.requiredTech != null)) {
			us.knownTechs.Remove(type.requiredTech.id);
		}
		Assert.True(ArcherRush.NeedsAttacker(gameData, us));
		GiveUsArchers();
		Assert.False(ArcherRush.NeedsAttacker(gameData, us));
	}
}
