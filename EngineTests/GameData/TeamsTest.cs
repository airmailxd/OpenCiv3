using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Teams set up with a new game (see GameSetup.Teams.cs).
public class TeamsTest {
	// A six-civ game: the player on playerTeam, the opponents on the teams
	// given, in turn order.
	private static (SaveGame, C7GameData.GameData) NewGame(int playerTeam, int[] opponentTeams,
			GameScenario scenario = GameScenario.None) {
		SaveGame save = SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")).GetSave();
		WorldCharacteristics wc = new(save) {
			landform = WorldCharacteristics.Landform.Continents,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_60,
			age = WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			barbarianActivity = BarbarianActivity.Roaming,
			worldSize = new WorldSize() { width = 100, height = 100, numberOfCivs = 6, distanceBetweenCivs = 12, techRate = 240, optimalNumberOfCities = 20 },
			mapSeed = 7,
		};
		new GameSetup {
			playerCivilization = save.Civilizations.First(c => !c.isBarbarian),
			playerTeam = playerTeam,
			difficulty = save.Difficulties.First(d => d.Name == "Regent"),
			worldCharacteristics = wc,
			opponents = opponentTeams.Select(t => new SelectedOpponent { isRandom = true, team = t }).ToList(),
			victoryConditions = new VictoryConditions(),
			scenario = scenario,
			eternalWarCitiesPerCiv = scenario == GameScenario.EternalWar ? 2 : null,
		}.Populate(save);
		return (save, SaveGameFixture.HydrateSaveGame(save));
	}

	private static List<Player> Civs(C7GameData.GameData gameData) {
		return gameData.players.Where(p => !p.isBarbarians).ToList();
	}

	[Fact]
	public void TeammatesAreLockedAlliesWhoHaveMetAndWinTogether() {
		// Two teams of two, and a civ alone on team 3, and one with none.
		(_, C7GameData.GameData gameData) = NewGame(1, [2, 1, 2, 3, 0]);
		List<Player> civs = Civs(gameData);
		Assert.True(gameData.rules.TeamVictory);

		Assert.Equal("Team 1", civs[0].alliance?.name);
		Assert.Same(civs[0].alliance, civs[2].alliance);
		Assert.Equal("Team 2", civs[1].alliance?.name);
		Assert.Same(civs[1].alliance, civs[3].alliance);
		// A team of one is no alliance.
		Assert.Null(civs[4].alliance);
		Assert.Null(civs[5].alliance);

		Assert.True(gameData.AreInLockedPeace(civs[0], civs[2]));
		Assert.True(gameData.AreTeammates(civs[0], civs[2]));
		Assert.False(gameData.AreInLockedPeace(civs[0], civs[1]));
		Assert.False(gameData.AreTeammates(civs[4], civs[5]));

		// Teammates start out in contact, at peace, with a mutual protection
		// pact; rivals haven't met.
		PlayerRelationship mates = civs[0].playerRelationships[civs[2].id];
		Assert.Equal(0, mates.firstContactTurn);
		Assert.False(mates.AtWar());
		Assert.Contains(mates.multiTurnDeals, d => d.dealSubType == DealSubType.MutualProtectionPact);
		Assert.Contains(civs[2].playerRelationships[civs[0].id].multiTurnDeals, d => d.dealSubType == DealSubType.Peace);
		Assert.False(civs[0].playerRelationships.ContainsKey(civs[1].id));

		// Conquest is won when only one team is left.
		ConquestVictory conquest = new();
		Assert.Equal(4, conquest.Evaluate(civs[0], gameData).RivalsRemaining);
		foreach (Player rival in new[] { civs[1], civs[3], civs[4], civs[5] }) {
			rival.defeated = true;
		}
		Assert.True(conquest.HasVictory(conquest.Evaluate(civs[0], gameData)));
		Assert.True(conquest.HasVictory(conquest.Evaluate(civs[2], gameData)));
	}

	// Teammates can't go to war with each other, whichever way war is
	// declared; anyone else can.
	[Fact]
	public void TeammatesCantDeclareWarOnEachOther() {
		(_, C7GameData.GameData gameData) = NewGame(2, [1, 2, 1, 2]);
		EngineStorage.InitializeGameDataForTests(gameData);
		List<Player> civs = Civs(gameData);
		(Player us, Player mate, Player rival) = (civs[0], civs[2], civs[1]);
		EngineStorage.activePlayerID = us.id;
		Assert.True(TurnHandling.IsPlayersTurn(gameData, us.id));

		new MsgDeclareWar(mate) { playerID = us.id }.send();
		while (EngineStorage.HasPendingMessagesToEngine()) {
			EngineStorage.ProcessNextMessageToEngine();
		}
		us.DeclareWarOn(mate, gameData.turn);
		PlayerRelationship.DeclareWar(mate, us, sneakAttack: false, refuseContactUntilTurn: 0);
		Assert.False(PlayerRelationship.AtWar(us, mate));
		Assert.False(PlayerRelationship.AtWar(mate, us));

		us.EnsureRelationshipExists(rival);
		rival.EnsureRelationshipExists(us);
		us.DeclareWarOn(rival, gameData.turn);
		Assert.True(PlayerRelationship.AtWar(us, rival));
	}

	[Fact]
	public void AGameWithoutTeamsIsAFreeForAll() {
		(_, C7GameData.GameData gameData) = NewGame(0, [0, 0, 0, 0, 0]);
		Assert.False(gameData.rules.TeamVictory);
		Assert.All(Civs(gameData), p => Assert.Null(p.alliance));
	}

	[Fact]
	public void EveryCivOnOneTeamIsRefused() {
		ArgumentException e = Assert.Throws<ArgumentException>(() => NewGame(2, [2, 2, 2, 2, 2]));
		Assert.Contains("same team", e.Message);
	}

	[Fact]
	public void InEternalWarTeammatesAreAtPeaceAndEveryoneElseAtWar() {
		(_, C7GameData.GameData gameData) = NewGame(1, [2, 1, 2, 1, 2], GameScenario.EternalWar);
		List<Player> civs = Civs(gameData);
		Assert.True(gameData.rules.PermanentWar);

		Assert.False(gameData.AreInLockedWar(civs[0], civs[2]));
		Assert.False(civs[0].playerRelationships[civs[2].id].AtWar());
		EngineStorage.gameData = gameData;
		Assert.True(civs[0].WillAcceptCommunicationFrom(civs[2], gameData.turn));
		Assert.False(civs[0].WillAcceptCommunicationFrom(civs[1], gameData.turn));

		Assert.True(gameData.AreInLockedWar(civs[0], civs[1]));
		Assert.True(civs[0].playerRelationships[civs[1].id].AtWar());
	}

	[Theory]
	[InlineData("Tiny", 2)]
	[InlineData("Small", 2)]
	[InlineData("Standard", 2)]
	[InlineData("Large", 4)]
	[InlineData("Huge", 4)]
	public void PlayersAreDealtOutOverTwoTeamsOrFourOnABigWorld(string size, int teams) {
		Assert.Equal(teams, GameSetup.DefaultTeamCount(new WorldSize { name = size }));
		// In turn order, round the teams.
		Assert.Equal(Enumerable.Range(0, 2 * teams).Select(i => i % teams + 1),
			Enumerable.Range(0, 2 * teams).Select(i => GameSetup.DefaultTeam(i, teams)));
	}
}
