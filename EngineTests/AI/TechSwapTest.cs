using System;
using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

// The "tech-swap" trial switch: AIs swap a tech for a tech.
public sealed class TechSwapTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;
	private readonly Tech ours;
	private readonly Tech theirs;

	public TechSwapTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		var civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman && p.units.Any(u => u.unitType.isSettler)).Take(2).ToList();
		(us, them) = (civs[0], civs[1]);
		foreach (Player p in civs) {
			p.isHuman = false;
			CityInteractions.BuildCity(p.units.First(u => u.unitType.isSettler).location, p, p.GetNextCityName());
		}
		us.EnsureRelationshipExists(them);
		them.EnsureRelationshipExists(us);
		var roots = gameData.techs.Where(t => t.Prerequisites.Count == 0 && !t.CannotBeTraded && !us.knownTechs.Contains(t.id) && !them.knownTechs.Contains(t.id)).Take(2).ToList();
		// Both start with the same techs, bar the two below.
		them.knownTechs.Clear();
		them.knownTechs.UnionWith(us.knownTechs);
		(ours, theirs) = (roots[0], roots[1]);
		us.gold = them.gold = 1000;
		us.knownTechs.Add(ours.id);
		them.knownTechs.Add(theirs.id);
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	[Fact]
	public void WithTheSwitchOffNothingIsSwapped() {
		Assert.False(TechSwap.On(us, them));
		Assert.False(TechSwap.WantsToSwapNow(us));
	}

	[Fact]
	public void WithTheSwitchOnTwoTechsChangeHands() {
		AiExperiments.Enable(us, TechSwap.Switch);
		Assert.True(TechSwap.On(us, them));
		Assert.True(TechSwap.WantsToSwapNow(us));
		Assert.False(TechSwap.WantsToSwapNow(us));

		Assert.True(TechSwap.Swap(gameData, us, them) >= 1);

		Assert.Contains(theirs.id, us.knownTechs);
		Assert.Contains(ours.id, them.knownTechs);
	}

	[Fact]
	public void NothingIsSwappedWhenOneSideHasNothingToGive() {
		AiExperiments.Enable(us, TechSwap.Switch);
		us.knownTechs.Remove(ours.id);

		Assert.Equal(0, TechSwap.Swap(gameData, us, them));
		Assert.DoesNotContain(theirs.id, us.knownTechs);
	}

	// With republic-route on, a tech on Republic's route is asked for ahead
	// of a dearer one off it. (TechValue already adds the route bonus, so this
	// guards the order rather than proving the helper alone.)
	[Fact]
	public void RepublicRouteTechIsAskedForBeforeADearerOne() {
		AiExperiments.Enable(us, TechSwap.Switch);
		AiExperiments.Enable(us, GovernmentAI.RepublicRoute);
		us.government = gameData.governments.First(g => g.hasTilePenalty);
		var path = GovernmentAI.RepublicRoutePath(us, gameData);
		int beakers = us.BeakersPerTurn();
		Tech route = gameData.techs.First(t => path.Contains(t) && t.Prerequisites.All(p => us.HasTech(p.id)) && !t.CannotBeTraded);
		them.knownTechs.Add(route.id);
		Tech dearer = gameData.techs.Where(t => !path.Contains(t) && t != route && t != ours && t != theirs && !us.HasTech(t.id))
			.OrderByDescending(t => DevelopmentAI.TechValue(gameData, us, t, beakers))
			.First(t => { them.knownTechs.Add(t.id); return them.GetTechsTradableTo(us, gameData.techs).Contains(t) || !them.knownTechs.Remove(t.id); });
		Assert.True(DevelopmentAI.TechValue(gameData, us, dearer, beakers) > DevelopmentAI.TechValue(gameData, us, route, beakers) - GovernmentAI.RepublicRouteBonus);
		Assert.Contains(route, them.GetTechsTradableTo(us, gameData.techs));
		Assert.Contains(dearer, them.GetTechsTradableTo(us, gameData.techs));

		Assert.Equal(1, TechSwap.Swap(gameData, us, them));

		Assert.Contains(route.id, us.knownTechs);
		Assert.DoesNotContain(dearer.id, us.knownTechs);
	}
}
