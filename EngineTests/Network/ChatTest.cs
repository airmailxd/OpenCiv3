using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.Network;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.Network;

// The game's chat in a LAN game (host and guests in this one process) and
// on a dedicated host: All to every human player, Team to the team only,
// never to the computer's players or spectators.
public class ChatTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;

	public ChatTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		// Engine state is static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.messagesToUI.Clear();
		EngineStorage.pendingMessages.Clear();
		EngineStorage.ResetNetworking();
	}

	public void Dispose() {
		EngineStorage.ResetNetworking();
	}

	private static readonly TimeSpan PumpTimeout = TimeSpan.FromSeconds(60);

	private static void PumpUntil(LanHost host, IEnumerable<LanClient> clients, Func<bool> condition) {
		Stopwatch pumping = Stopwatch.StartNew();
		while (true) {
			host.Poll();
			EngineStorage.ProcessNextMessageToEngine();
			EngineStorage.messagesToUI.Clear();
			foreach (LanClient client in clients) {
				client.Poll();
			}
			if (condition()) {
				return;
			}
			if (pumping.Elapsed > PumpTimeout) {
				throw new TimeoutException("The game never got there");
			}
			Thread.Sleep(5);
		}
	}

	private async Task CreateGame(SaveGame save) {
		new MsgSetAnimationsEnabled(false).send();
		EngineStorage.ProcessNextMessageToEngine();
		await C7Engine.CreateGame.createGame(save, (_) => fixture.behaviors);
		TurnHandling.InitTurnData();
		EngineStorage.messagesToUI.Clear();
	}

	// What one machine hears.
	private sealed class Ears {
		public readonly List<ChatLineInfo> lines = [];
		public IEnumerable<string> Shown => lines.Select(GameChat.Format);
		public bool Heard(string text) => lines.Any(l => l.text == text);
	}

	private static Ears Listen(LanClient client) {
		Ears ears = new();
		client.SnapshotReceived = _ => { };
		client.UiMessageReceived = _ => { };
		client.ChatLineReceived = ears.lines.Add;
		return ears;
	}

	[Fact]
	public async Task AllReachesEveryoneAndTeamOnlyTheTeam() {
		// The host and Ann are a team, with a computer player; Bob plays alone.
		SaveGame save = SaveGameFixture.ThreeHumanSave();
		SavePlayer[] humans = save.Players.Where(p => p.human).ToArray();
		SavePlayer ai = save.Players.First(p => !p.human && !p.IsIndependent);
		Alliance team = new(1, GameSetup.TeamName(1));
		save.Alliances.Add(team);
		humans[0].alliance = team.name;
		humans[1].alliance = team.name;
		ai.alliance = team.name;

		using LanHost host = new("Host", save, port: 0, answerDiscovery: false) { AllowSpectators = true };
		Ears hostEars = new();
		host.ChatReceived += hostEars.lines.Add;
		Assert.Equal([humans[1].id, humans[2].id], host.Seats.Select(s => s.playerID));

		using LanClient ann = LanClient.Connect("127.0.0.1", host.Port, "Ann");
		using LanClient bob = LanClient.Connect("127.0.0.1", host.Port, "Bob");
		using LanClient watcher = LanClient.Connect("127.0.0.1", host.Port, "Watcher");
		LanClient[] everyone = [ann, bob, watcher];
		PumpUntil(host, everyone, () => ann.Lobby != null && bob.Lobby != null && watcher.Lobby != null);
		ann.ClaimSeat(humans[1].id);
		bob.ClaimSeat(humans[2].id);
		watcher.Watch();
		PumpUntil(host, everyone, () => host.AllSeatsTaken && host.Spectators.Count == 1);

		await CreateGame(save);
		host.StartGame();
		PumpUntil(host, everyone, () => everyone.All(c => c.StartingGame != null));
		Ears annEars = Listen(ann), bobEars = Listen(bob), watcherEars = Listen(watcher);

		// All: every human player, the speaker too; not the spectator.
		host.Say(ChatChannel.All, "  Good luck,\tall  ");
		PumpUntil(host, everyone, () => annEars.Heard("Good luck, all") && bobEars.Heard("Good luck, all"));
		Assert.Equal(["[All] Host: Good luck, all"], hostEars.Shown);
		Assert.Equal(["[All] Host: Good luck, all"], bobEars.Shown);
		Assert.Equal(humans[0].id, annEars.lines.Single().playerID);

		// Team: Ann's reaches the host and Ann, not Bob. Bob's All line after
		// it reaches Bob after Ann's would have, as frames keep their order.
		ann.Say(ChatChannel.Team, "Their capital is open");
		PumpUntil(host, everyone, () => hostEars.Heard("Their capital is open") && annEars.Heard("Their capital is open"));
		Assert.Equal("[Team] Ann: Their capital is open", hostEars.Shown.Last());
		Assert.Equal(1, annEars.lines.Last().team);
		bob.Say(ChatChannel.All, "gl hf");
		PumpUntil(host, everyone, () => bobEars.Heard("gl hf") && annEars.Heard("gl hf") && hostEars.Heard("gl hf"));
		Assert.False(bobEars.Heard("Their capital is open"));
		Assert.Equal(["[All] Host: Good luck, all", "[All] Bob: gl hf"], bobEars.Shown);

		// The host's Team line reaches Ann.
		host.Say(ChatChannel.Team, "Agreed");
		PumpUntil(host, everyone, () => annEars.Heard("Agreed"));
		Assert.Equal("[Team] Host: Agreed", annEars.Shown.Last());

		// Bob has no team, and is told so; nobody else hears it.
		bob.Say(ChatChannel.Team, "anyone?");
		PumpUntil(host, everyone, () => bobEars.lines.Any(l => l.from == null));
		Assert.Equal(GameChat.NotOnTeam, bobEars.lines.Last().text);
		bob.Say(ChatChannel.All, "ok");
		PumpUntil(host, everyone, () => annEars.Heard("ok") && hostEars.Heard("ok"));
		Assert.False(annEars.Heard("anyone?") || hostEars.Heard("anyone?"));

		// The spectator and the computer's player hear nothing; a spectator
		// isn't heard either.
		watcher.Say(ChatChannel.All, "psst");
		host.Say(ChatChannel.All, "last");
		PumpUntil(host, everyone, () => annEars.Heard("last"));
		Assert.Empty(watcherEars.lines);
		Assert.False(annEars.Heard("psst"));

		// Five lines at once, then one a second.
		for (int i = 0; i < 7; i++) {
			bob.Say(ChatChannel.All, $"spam {i}");
		}
		PumpUntil(host, everyone, () => bobEars.lines.Any(l => l.text == GameChat.TooFast));
		Assert.True(annEars.lines.Count(l => l.text.StartsWith("spam")) <= 6);
	}

	[Fact]
	public async Task ADedicatedHostPassesLinesBetweenItsGuests() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		await CreateGame(save);
		Player[] humans = EngineStorage.gameData.players.Where(p => p.isHuman).ToArray();
		using LanHost host = LanHost.Dedicated("Friday Game", save, [
			new LanHost.DedicatedSeat(humans[0].id, "first", "alice"),
			new LanHost.DedicatedSeat(humans[1].id, "second", "bob"),
		]);
		host.StartGame();
		bool hostHeard = false;
		host.ChatReceived += _ => hostHeard = true;

		using LanClient alice = LanClient.Connect("127.0.0.1", host.Port, "alice", "first");
		using LanClient bob = LanClient.Connect("127.0.0.1", host.Port, "bob", "second");
		Ears aliceEars = Listen(alice), bobEars = Listen(bob);
		PumpUntil(host, [alice, bob], () => alice.PlayerIDs.Count > 0 && bob.PlayerIDs.Count > 0);

		alice.Say(ChatChannel.All, "your move");
		PumpUntil(host, [alice, bob], () => bobEars.Heard("your move") && aliceEars.Heard("your move"));
		// Named as the server names them.
		Assert.Equal("[All] alice: your move", bobEars.Shown.Single());
		host.Say(ChatChannel.All, "nobody");
		Assert.False(hostHeard);

		// A free-for-all: nobody has a team.
		bob.Say(ChatChannel.Team, "hello?");
		PumpUntil(host, [alice, bob], () => bobEars.lines.Any(l => l.from == null));
		Assert.Equal(GameChat.NotOnTeam, bobEars.lines.Last().text);
	}

	[Fact]
	public void TypedLinesAreReadAsCommands() {
		Assert.Equal(new ChatCommand(ChatCommandKind.Say, ChatChannel.All, Text: "hi"), GameChat.Parse(" hi ", ChatChannel.All));
		Assert.Equal(new ChatCommand(ChatCommandKind.Say, ChatChannel.Team, Text: "hi"), GameChat.Parse("hi", ChatChannel.Team));
		Assert.Equal(new ChatCommand(ChatCommandKind.Say, ChatChannel.Team, Text: "go"), GameChat.Parse("/t go", ChatChannel.All));
		Assert.Equal(new ChatCommand(ChatCommandKind.Say, ChatChannel.All, Text: "go"), GameChat.Parse("/ALL go", ChatChannel.Team));
		Assert.Equal(new ChatCommand(ChatCommandKind.OpenChannel, ChatChannel.Team), GameChat.Parse("/team", ChatChannel.All));
		Assert.Equal(new ChatCommand(ChatCommandKind.OpenChannel, ChatChannel.All), GameChat.Parse("/a", ChatChannel.Team));
		Assert.Equal(new ChatCommand(ChatCommandKind.Whisper, ChatChannel.All, "carol", "see you"), GameChat.Parse("/w carol see you", ChatChannel.All));
		Assert.Equal("carol", GameChat.Parse("/msg carol hi", ChatChannel.All).Name);
		Assert.Equal("carol", GameChat.Parse("/whisper carol hi", ChatChannel.All).Name);
		Assert.Null(GameChat.Parse("/w carol", ChatChannel.All).Text);
		Assert.Equal(new ChatCommand(ChatCommandKind.Reply, ChatChannel.All, Text: "yes"), GameChat.Parse("/r yes", ChatChannel.All));
		Assert.Equal(new ChatCommand(ChatCommandKind.Ignore, ChatChannel.All, "dave"), GameChat.Parse("/ignore dave", ChatChannel.All));
		Assert.Equal(new ChatCommand(ChatCommandKind.Unignore, ChatChannel.All, "dave"), GameChat.Parse("/unignore dave", ChatChannel.All));
		Assert.Equal(ChatCommandKind.Unknown, GameChat.Parse("/dance", ChatChannel.All).Kind);
		Assert.Equal(ChatCommandKind.Nothing, GameChat.Parse("   ", ChatChannel.All).Kind);

		Assert.Null(GameChat.Tidy(" \u0007 "));
		Assert.Equal("a b", GameChat.Tidy("a\nb\u0000"));
		Assert.Equal(GameChat.MaxLength, GameChat.Tidy(new string('x', 1000)).Length);
		Assert.Equal("[From carol]: hi", GameChat.FormatWhisper("carol", "hi", fromMe: false));
		Assert.Equal("[To carol]: hi", GameChat.FormatWhisper("carol", "hi", fromMe: true));
	}
}
