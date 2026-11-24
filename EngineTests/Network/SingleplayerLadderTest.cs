using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.Network;
using C7GameData;
using C7GameData.Save;
using C7GameServer;
using Xunit;

namespace EngineTests.Network;

// Which finished games go to the singleplayer ladder, and the queue they
// wait in until the game server takes them.
public class SingleplayerLadderTest : IDisposable {
	private readonly string folder = Path.Combine(Path.GetTempPath(), "c7-ladder-test-" + Guid.NewGuid().ToString("N"));

	public void Dispose() {
		if (Directory.Exists(folder)) {
			Directory.Delete(folder, true);
		}
	}

	private static C7GameData.GameData Game(int humans = 1, int ais = 3, bool over = true) {
		C7GameData.GameData gameData = new() { gameOver = over };
		for (int i = 0; i < humans + ais; ++i) {
			gameData.players.Add(new Player() { isHuman = i < humans });
		}
		return gameData;
	}

	[Fact]
	public void OnlyAFinishedGameWithOneHumanHereIsSent() {
		Assert.True(SingleplayerLadder.IsLadderGame(Game(), networked: false));
		Assert.False(SingleplayerLadder.IsLadderGame(Game(), networked: true));
		Assert.False(SingleplayerLadder.IsLadderGame(Game(over: false), networked: false));
		Assert.False(SingleplayerLadder.IsLadderGame(Game(humans: 2), networked: false));
		Assert.False(SingleplayerLadder.IsLadderGame(Game(humans: 0), networked: false));
		Assert.False(SingleplayerLadder.IsLadderGame(null, networked: false));
		C7GameData.GameData email = Game();
		email.playByEmail = true;
		Assert.False(SingleplayerLadder.IsLadderGame(email, networked: false));
		C7GameData.GameData watched = Game();
		watched.observerMode = true;
		Assert.False(SingleplayerLadder.IsLadderGame(watched, networked: false));
		C7GameData.GameData debug = Game();
		debug.debugMode = true;
		Assert.False(SingleplayerLadder.IsLadderGame(debug, networked: false));
	}

	[Fact]
	public void TheVictoryIsNamedOnlyWhenThePlayerWon() {
		C7GameData.GameData game = Game();
		Player human = game.players[0];
		Player ai = game.players[1];
		Assert.Equal("SpaceRace", SingleplayerLadder.VictoryName(game, human, new SpaceRaceVictory()));
		Assert.Equal("Domination", SingleplayerLadder.VictoryName(game, human, new DominationVictory(66, 66)));
		Assert.Null(SingleplayerLadder.VictoryName(game, ai, new SpaceRaceVictory()));
		Assert.Null(SingleplayerLadder.VictoryName(game, null, null));
		Assert.Equal("Conquest", SingleplayerLadder.VictoryName(new ConquestVictory()));
		Assert.Equal("Cultural", SingleplayerLadder.VictoryName(new CulturalVictory(20000, 100000)));
		Assert.Equal("Diplomatic", SingleplayerLadder.VictoryName(new DiplomaticVictory()));
		Assert.Equal("Score", SingleplayerLadder.VictoryName(new ScoreVictory()));
		Assert.Null(SingleplayerLadder.VictoryName(null));
	}

	[Fact]
	public void OnlyStandardGamesAreSent() {
		Assert.True(SingleplayerLadder.IsLadderType(new SaveGame()));
		SaveGame war = new();
		war.Rules.PermanentWar = true;
		Assert.False(SingleplayerLadder.IsLadderType(war));
		SaveGame mdj = new();
		mdj.Rules.OwnTechTree = true;
		Assert.False(SingleplayerLadder.IsLadderType(mdj));
	}

	[Fact]
	public void RefusedGamesAreDroppedAndFailedOnesKept() {
		Assert.Equal(SingleplayerLadder.Outcome.Ranked, SingleplayerLadder.OutcomeOf(HttpStatusCode.OK));
		Assert.Equal(SingleplayerLadder.Outcome.Refused, SingleplayerLadder.OutcomeOf(HttpStatusCode.BadRequest));
		Assert.Equal(SingleplayerLadder.Outcome.Refused, SingleplayerLadder.OutcomeOf(HttpStatusCode.RequestEntityTooLarge));
		Assert.Equal(SingleplayerLadder.Outcome.TryAgain, SingleplayerLadder.OutcomeOf(HttpStatusCode.Unauthorized));
		Assert.Equal(SingleplayerLadder.Outcome.TryAgain, SingleplayerLadder.OutcomeOf(HttpStatusCode.TooManyRequests));
		Assert.Equal(SingleplayerLadder.Outcome.TryAgain, SingleplayerLadder.OutcomeOf(HttpStatusCode.NotFound));
		Assert.Equal(SingleplayerLadder.Outcome.TryAgain, SingleplayerLadder.OutcomeOf(HttpStatusCode.UpgradeRequired));
		Assert.Equal(SingleplayerLadder.Outcome.TryAgain, SingleplayerLadder.OutcomeOf(HttpStatusCode.InternalServerError));
		Assert.Equal(SingleplayerLadder.Outcome.TryAgain, SingleplayerLadder.OutcomeOf(HttpStatusCode.BadGateway));
		Assert.Equal(SingleplayerLadder.Outcome.TryAgain, SingleplayerLadder.OutcomeOf(null));
	}

	[Fact]
	public void TheQueueKeepsTheNewestFew() {
		LadderQueue queue = new(folder, max: 3);
		DateTime start = new(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);
		List<string> added = Enumerable.Range(0, 5).Select(i => queue.Add(new SaveGame(), start.AddMinutes(i))).ToList();
		Assert.Equal(added.Skip(2), queue.Pending());
		// Written as the game writes a save to disk, and nothing half written.
		string saved = Path.Combine(folder, "saved.json");
		new SaveGame().Save(saved);
		Assert.Equal(File.ReadAllBytes(saved), File.ReadAllBytes(added[4]));
		File.Delete(saved);
		Assert.Empty(Directory.GetFiles(folder, "*.part"));
	}

	[Fact]
	public async Task SendingDropsWhatTheServerTookOrRefusedAndStopsAtAFailure() {
		LadderQueue queue = new(folder);
		DateTime start = new(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);
		List<string> added = Enumerable.Range(0, 4).Select(i => queue.Add(new SaveGame(), start.AddMinutes(i), i == 0 ? "SpaceRace" : null)).ToList();
		int sent = 0;
		List<string> victories = [];
		Dictionary<string, LadderResult> ranked = await SingleplayerLadder.SendPendingAsync(queue, (save, victory) => {
			// Gzipped, and as the game writes the save once unzipped.
			using MemoryStream unzipped = new();
			using (GZipStream gzip = new(new MemoryStream(save), CompressionMode.Decompress)) {
				gzip.CopyTo(unzipped);
			}
			Assert.Equal(File.ReadAllBytes(added[sent]), unzipped.ToArray());
			victories.Add(victory);
			return ++sent switch {
				1 => Task.FromResult(new LadderResult(1834, 12, true)),
				2 => throw new GameServerException("Only Standard games are ranked.", HttpStatusCode.BadRequest),
				3 => throw new GameServerException("Couldn't reach the game server."),
				_ => throw new InvalidOperationException("Sent after a failure"),
			};
		});
		Assert.Equal(3, sent);
		Assert.Equal(["SpaceRace", null, null], victories);
		Assert.Equal([added[0]], ranked.Keys);
		Assert.Equal(new LadderResult(1834, 12, true), ranked[added[0]]);
		Assert.Equal(added.Skip(2), queue.Pending());

		// The next time, with a session ended: kept for after logging in.
		ranked = await SingleplayerLadder.SendPendingAsync(queue,
			(save, victory) => throw new GameServerException("Log in again.", HttpStatusCode.Unauthorized));
		Assert.Empty(ranked);
		Assert.Equal(added.Skip(2), queue.Pending());
	}
}
