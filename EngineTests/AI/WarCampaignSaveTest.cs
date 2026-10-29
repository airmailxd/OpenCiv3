using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

// The AI's war campaign and its cooldown are saved (see AiSaveState), so a
// reloaded game plays on as it would have.
public sealed class WarCampaignSaveTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;

	// Every roll comes out 0, so Civ3's war plotting always plots.
	private class LowRandom : Random {
		protected override double Sample() => 0.0;
	}

	private static readonly JsonSerializerOptions JsonOptions = new() {
		IncludeFields = true,
		Converters = { new IDJsonConverter() },
	};

	public WarCampaignSaveTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		Player[] civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman).Take(2).ToArray();
		us = civs[0];
		them = civs[1];
		us.EnsureRelationshipExists(them);
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private static bool IsEmptyLand(Tile t) {
		return t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp && !t.IsImpassable();
	}

	private City BuildCity(Player owner, Func<Tile, bool> where) {
		Tile tile = gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities()
			&& t.neighbors.Values.All(n => n == Tile.NONE || !n.HasCity()) && where(t));
		return CityInteractions.BuildCity(tile, owner, $"City {owner.cities.Count}");
	}

	// One city each, on the same landmass some way apart, and soldiers in
	// ours: the AI plots a war on them and gathers its attackers.
	private async Task StartCampaign() {
		foreach (City c in us.cities.Concat(them.cities).ToList()) {
			CityInteractions.DestroyCity(c);
		}
		City ours = BuildCity(us, _ => true);
		BuildCity(them, t => t.continent == ours.location.continent && t.DistanceTo(ours.location) > 6);
		UnitPrototype sword = gameData.unitPrototypes.Single(p => p.name == "Swordsman");
		UnitPrototype spear = gameData.unitPrototypes.Single(p => p.name == "Spearman");
		for (int i = 0; i < 2; ++i) {
			gameData.SpawnUnit(us, spear, ours.location);
		}
		for (int i = 0; i < 3; ++i) {
			gameData.SpawnUnit(us, sword, ours.location);
		}
		gameData.random = new LowRandom();
		await WarCampaign.PlayTurn(us, gameData);
		Assert.True(WarCampaign.HasCampaign(us));
	}

	private C7GameData.GameData Reload(Action<SaveGame> change = null) {
		SaveGame save = SaveGame.FromJSON(SaveGame.FromGameData(gameData).ToCompactJSON());
		change?.Invoke(save);
		C7GameData.GameData reloaded = save.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(reloaded);
		return reloaded;
	}

	[Fact]
	public async Task TheCampaignAndCooldownSurviveASave() {
		await StartCampaign();
		WarCampaign.StartCooldown(us);
		SaveWarCampaign before = WarCampaign.Save(us);
		Assert.NotEmpty(before.units);
		int? cooldownBefore = WarCampaign.CooldownUntil(us);
		Assert.NotNull(cooldownBefore);

		C7GameData.GameData reloaded = Reload();
		Player loaded = reloaded.players.Single(p => p.id == us.id);
		Player loadedThem = reloaded.players.Single(p => p.id == them.id);

		Assert.True(WarCampaign.IsPlannedTarget(loaded, loadedThem));
		Assert.Equal(JsonSerializer.Serialize(before, JsonOptions), JsonSerializer.Serialize(WarCampaign.Save(loaded), JsonOptions));
		Assert.Equal(cooldownBefore, WarCampaign.CooldownUntil(loaded));
		// Its units are members again, so the unit AI leaves them be.
		foreach (ID id in before.units) {
			Assert.IsType<WarCampaign.MemberAI>(loaded.units.Single(u => u.id == id).currentAI);
		}
		Assert.True(WarCampaign.TryGetCampaign(loaded, out _, out City target, out _, out _));
		Assert.Same(reloaded.cities.Single(c => c.id == before.target), target);
	}

	// An older save, without the AI's memory, loads with no campaign.
	[Fact]
	public async Task AnOlderSaveLoadsWithNoCampaign() {
		await StartCampaign();
		WarCampaign.StartCooldown(us);

		C7GameData.GameData reloaded = Reload(save => save.Players.ForEach(p => p.ai = null));
		Player loaded = reloaded.players.Single(p => p.id == us.id);

		Assert.False(WarCampaign.HasCampaign(loaded));
		Assert.Null(WarCampaign.CooldownUntil(loaded));
		Assert.DoesNotContain(loaded.units, u => u.currentAI is WarCampaign.MemberAI);
	}

	// The turns the AI last asked civs for things are saved too, so it
	// doesn't ask again at once after a load.
	[Fact]
	public void TheAIsOtherMemoriesSurviveASave() {
		Player third = gameData.players.First(p => p != us && p != them && !p.isBarbarians);
		SaveAiState state = new() {
			dealRequestTurns = new() { [them.id.ToString()] = 3 },
			passageRequestTurns = new() { [third.id.ToString()] = 5 },
			lastDisbandedTurn = 2,
			marketPricePerShield = 1.25,
		};
		AiSaveState.Restore(us, gameData, state);

		C7GameData.GameData reloaded = Reload();
		Player loaded = reloaded.players.Single(p => p.id == us.id);

		Assert.Equal(JsonSerializer.Serialize(state, JsonOptions), JsonSerializer.Serialize(AiSaveState.Save(loaded), JsonOptions));
	}
}
