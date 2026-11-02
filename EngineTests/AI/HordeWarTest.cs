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

// The horde-war switch (see HordeWar): from about turn 90 an Advanced AI
// masses its best fast attacker against the best victim it can reach on its
// landmass, whatever its plan says of them, and declares once the horde has
// gathered. Off, or for a Standard AI, nothing: Civ3's plotting rolls
// decide, and here they all fail.
public sealed class HordeWarTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;
	private readonly Random originalRandom;
	private City ours;
	private City theirs;

	// Every roll fails, so Civ3's war plotting starts nothing by itself.
	private class HighRandom : Random {
		protected override double Sample() => 0.999999;
	}

	public HordeWarTest(SaveGameFixture fixture) {
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
		OneCityEach();
		foreach (Tile t in gameData.map.tiles) {
			us.tileKnowledge.knownTiles.Add(t);
		}
		// Late enough to mass a horde whatever land is left.
		gameData.turn = HordeWar.LateStartTurn;
		UnitPrototype horseman = gameData.unitPrototypes.First(p => p.name == "Horseman");
		if (horseman.requiredTech != null && !us.knownTechs.Contains(horseman.requiredTech.id)) {
			us.knownTechs.Add(horseman.requiredTech.id);
		}
		// Horses on our city's tile, so that we can build horsemen.
		Resource horses = horseman.requiredResources.First();
		ours.location.Resource = horses;
		if (horses.Prerequisite != null) {
			us.knownTechs.Add(horses.Prerequisite);
		}
		gameData.InvalidateCachedTradeNetwork();
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

	// One city each on the same landmass, theirs 5 to 10 tiles from ours,
	// and none of either side's old units.
	private void OneCityEach() {
		foreach (City c in us.cities.ToList()) {
			CityInteractions.DestroyCity(c);
		}
		foreach (Player p in new[] { us, them }) {
			foreach (MapUnit u in p.units.ToList()) {
				p.units.Remove(u);
				u.location.unitsOnTile.Remove(u);
			}
		}
		ours = BuildCity(us, _ => true);
		List<City> old = them.cities.ToList();
		theirs = BuildCity(them, t => t.continent == ours.location.continent && t.DistanceTo(ours.location) >= 5 && t.DistanceTo(ours.location) <= 10);
		foreach (City c in old) {
			CityInteractions.DestroyCity(c);
		}
	}

	private void Play(AiType type) {
		us.aiType = type;
		// The plan calls them a friend ("much stronger: keep the peace"),
		// which the horde doesn't heed.
		us.strategicPlan = new StrategicPlan { source = "rules", setTurn = gameData.turn };
		us.strategicPlan.stances[them.id.ToString()] = RivalStance.Friend;
	}

	// Defenders to stay home and the horde, in our city, and a spearman in
	// theirs.
	private void Army(int horsemen) {
		UnitPrototype spearman = gameData.unitPrototypes.First(p => p.name == "Spearman");
		UnitPrototype horseman = gameData.unitPrototypes.First(p => p.name == "Horseman");
		gameData.SpawnUnit(us, spearman, ours.location).currentAI = null;
		gameData.SpawnUnit(us, spearman, ours.location).currentAI = null;
		for (int i = 0; i < horsemen; ++i) {
			gameData.SpawnUnit(us, horseman, ours.location).currentAI = null;
		}
		gameData.SpawnUnit(them, spearman, theirs.location).currentAI = null;
	}

	private bool AtWar() => us.playerRelationships[them.id].AtWar();

	[Fact]
	public async Task OffNoHorde() {
		Play(AiType.Advanced);
		Army(HordeWar.MaxHorde);
		await WarCampaign.PlayTurn(us, gameData);
		Assert.False(WarCampaign.HasCampaign(us));
		Assert.False(AtWar());
	}

	[Fact]
	public async Task AStandardAiWithTheSwitchMassesNoHorde() {
		Play(AiType.Standard);
		Army(HordeWar.MaxHorde);
		AiExperiments.Enable(us, HordeWar.Switch);
		Assert.False(HordeWar.On(us));
		await WarCampaign.PlayTurn(us, gameData);
		Assert.False(WarCampaign.HasCampaign(us));
		Assert.False(AtWar());
	}

	[Fact]
	public void TheHordeIsOfHorsemen() {
		Play(AiType.Advanced);
		UnitPrototype unit = HordeWar.HordeUnit(us);
		Assert.NotNull(unit);
		Assert.True(unit.movement >= 2 && unit.attack > unit.defense, unit.name);
		Assert.True(HordeWar.IsFastAttacker(gameData.unitPrototypes.First(p => p.name == "Horseman")));
		Assert.False(HordeWar.IsFastAttacker(gameData.unitPrototypes.First(p => p.name == "Archer")));
		Assert.InRange(HordeWar.HordeSize(us, them), HordeWar.MinHorde, HordeWar.MaxHorde);
	}

	[Fact]
	public async Task OnAHordeGathersAndWaitsForItsSize() {
		Play(AiType.Advanced);
		AiExperiments.Enable(us, HordeWar.Switch);
		Army(3);
		await WarCampaign.PlayTurn(us, gameData);
		Assert.True(WarCampaign.HasHordeCampaignAgainst(us, them));
		// Three horsemen are not a horde: no war yet, and the best cities
		// build more.
		Assert.False(AtWar());
		Assert.True(WarCampaign.WantsAttackers(us));
		UnitPrototype horseman = gameData.unitPrototypes.First(p => p.name == "Horseman");
		Assert.True(HordeWar.ProductionBonus(ours, us, horseman, cityGuarded: true) > 0);
	}

	[Fact]
	public async Task OnTheMassedHordeDeclaresWar() {
		Play(AiType.Advanced);
		AiExperiments.Enable(us, HordeWar.Switch);
		Army(HordeWar.HordeSize(us, them) + 2);
		await WarCampaign.PlayTurn(us, gameData);
		Assert.True(AtWar());
		Assert.True(WarCampaign.HasCampaignAgainst(us, them));
		Assert.True(WarCampaign.HasHordeCampaign(us));
		int horsemen = us.units.Count(u => u.currentAI is WarCampaign.MemberAI && u.unitType.name == "Horseman");
		Assert.True(horsemen >= HordeWar.MinHorde, $"{horsemen} horsemen marched");
	}

	[Fact]
	public async Task OnTooSoonNoHorde() {
		Play(AiType.Advanced);
		AiExperiments.Enable(us, HordeWar.Switch);
		Army(HordeWar.MaxHorde);
		// Horsemen don't outclass spearmen, so not before StartTurn.
		UnitPrototype spearman = gameData.unitPrototypes.First(p => p.name == "Spearman");
		if (spearman.requiredTech != null) {
			them.knownTechs.Add(spearman.requiredTech.id);
		}
		Assert.False(HordeWar.Outclasses(us, them));
		gameData.turn = HordeWar.StartTurn - 1;
		await WarCampaign.PlayTurn(us, gameData);
		Assert.False(WarCampaign.HasCampaign(us));
		Assert.False(AtWar());
	}

	// Horsemen do outclass warriors (attack 2 against defence 1): the
	// horde comes sooner.
	[Fact]
	public async Task OnAnOutclassedNeighbourGetsAHordeSooner() {
		Play(AiType.Advanced);
		AiExperiments.Enable(us, HordeWar.Switch);
		Army(3);
		Assert.True(HordeWar.Outclasses(us, them));
		gameData.turn = HordeWar.EarliestTurn;
		await WarCampaign.PlayTurn(us, gameData);
		Assert.True(WarCampaign.HasHordeCampaignAgainst(us, them));
	}

	[Fact]
	public async Task OnNoHordeInThePeaceCooldown() {
		Play(AiType.Advanced);
		AiExperiments.Enable(us, HordeWar.Switch);
		Army(HordeWar.MaxHorde);
		us.playerRelationships[them.id].peaceSignedTurn = gameData.turn - 1;
		them.playerRelationships[us.id].peaceSignedTurn = gameData.turn - 1;
		await WarCampaign.PlayTurn(us, gameData);
		Assert.False(WarCampaign.HasCampaign(us));
		Assert.False(AtWar());
	}
}
