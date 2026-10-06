using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using Xunit;

namespace EngineTests.GameData;

// Who hears that one civ declared war on another.
public class WarDeclarationNewsTest : IDisposable {
	private readonly C7GameData.GameData gameData = new();
	private readonly Player aggressor = MakeCiv("player-1", human: false);
	private readonly Player opponent = MakeCiv("player-2", human: false);
	private readonly Player bystander = MakeCiv("player-3", human: true);

	public WarDeclarationNewsTest() {
		// Engine state is static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.messagesToUI.Clear();
		EngineStorage.ResetNetworking();
		gameData.players.AddRange([aggressor, opponent, bystander]);
		foreach (Player p in gameData.players) {
			foreach (Player other in gameData.players) {
				p.EnsureRelationshipExists(other);
			}
		}
	}

	public void Dispose() {
		EngineStorage.messagesToUI.Clear();
		gameData.observerMode = false;
	}

	private static Player MakeCiv(string id, bool human) {
		return new Player() { id = ID.FromString(id), civilization = new Civilization(), government = new Government(), isHuman = human };
	}

	private List<MsgWarDeclaration> Sent() {
		List<MsgWarDeclaration> sent = EngineStorage.messagesToUI.OfType<MsgWarDeclaration>().ToList();
		EngineStorage.messagesToUI.Clear();
		return sent;
	}

	[Fact]
	public void ACivWithoutAnEmbassyDoesntHearOfTheWar() {
		MsgWarDeclaration.Announce(aggressor, opponent);

		Assert.Empty(Sent());
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void AnEmbassyWithEitherSideBringsTheNews(bool withAggressor) {
		bystander.playerRelationships[(withAggressor ? aggressor : opponent).id].hasEmbassy = true;

		MsgWarDeclaration.Announce(aggressor, opponent);

		MsgWarDeclaration news = Assert.Single(Sent());
		Assert.Same(bystander, news.recipient);
		Assert.False(news.forSpectators);
	}

	[Fact]
	public void TheCivWarIsDeclaredOnAlwaysHears() {
		opponent.isHuman = true;

		MsgWarDeclaration.Announce(aggressor, opponent);

		MsgWarDeclaration news = Assert.Single(Sent());
		Assert.Same(opponent, news.recipient);
	}

	[Fact]
	public void AnEmbassyWithAThirdCivDoesntCount() {
		Player other = MakeCiv("player-4", human: false);
		gameData.players.Add(other);
		bystander.EnsureRelationshipExists(other);
		bystander.playerRelationships[other.id].hasEmbassy = true;

		MsgWarDeclaration.Announce(aggressor, opponent);

		Assert.Empty(Sent());
	}

	[Fact]
	public void AWatchedGameShowsEveryWar() {
		gameData.observerMode = true;

		MsgWarDeclaration.Announce(aggressor, opponent);

		MsgWarDeclaration news = Assert.Single(Sent());
		Assert.True(news.forSpectators);
	}
}
