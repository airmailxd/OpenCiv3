using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.Network;
using C7GameData;
using C7GameData.Save;
using C7Relay;
using EngineTests.Utils;
using Xunit;

namespace Relay.Tests;

// LAN games played through a relay running on localhost, as over the
// internet: guests join with the host's code, and get back by themselves when
// they or the host drop, or when the host resumes the game.
public class OnlineGameTests : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;

	public OnlineGameTests(SaveGameFixture fixture) {
		this.fixture = fixture;
		EngineStorage.messagesToUI.Clear();
		EngineStorage.pendingMessages.Clear();
		EngineStorage.ResetNetworking();
	}

	public void Dispose() {
		EngineStorage.ResetNetworking();
	}

	private async Task<C7GameData.GameData> CreateGame(SaveGame save) {
		new MsgSetAnimationsEnabled(false).send();
		EngineStorage.ProcessNextMessageToEngine();
		await C7Engine.CreateGame.createGame(save, (_) => fixture.behaviors);
		TurnHandling.OnBeginTurn();
		TurnHandling.InitTurnData();
		await TurnHandling.AdvanceTurn();
		EngineStorage.messagesToUI.Clear();
		return EngineStorage.gameData;
	}

	private static readonly TimeSpan PumpTimeout = TimeSpan.FromSeconds(60);

	// Runs the host, its engine and the clients until the condition holds.
	private static void PumpUntil(LanHost host, IEnumerable<LanClient> clients, Func<bool> condition) {
		Stopwatch pumping = Stopwatch.StartNew();
		while (true) {
			host?.Poll();
			EngineStorage.ProcessNextMessageToEngine();
			EngineStorage.messagesToUI.Clear();
			foreach (LanClient client in clients) {
				client.Poll();
			}
			if (condition()) {
				return;
			}
			if (pumping.Elapsed > PumpTimeout) {
				throw new TimeoutException("The online game never got there");
			}
			Thread.Sleep(5);
		}
	}

	private static string GoOnline(LanHost host, TestRelay relay) {
		RelayHostLink link = host.HostOnline(relay.Url);
		link.FirstRetryDelay = TimeSpan.FromMilliseconds(20);
		link.MaxRetryDelay = TimeSpan.FromMilliseconds(100);
		PumpUntil(host, [], () => link.State == RelayHostLink.LinkState.Online);
		Assert.NotNull(RelayProtocol.NormalizeCode(link.Code));
		return link.Code;
	}

	private static LanClient Join(TestRelay relay, string code, string name, string token = null) {
		// As typed by a player.
		LanClient client = LanClient.ConnectAsync(new RelayEndpoint(relay.Url, RelayProtocol.FormatCode(code).ToLowerInvariant()), name, token)
			.GetAwaiter().GetResult();
		client.FirstReconnectDelay = TimeSpan.FromMilliseconds(20);
		client.MaxReconnectDelay = TimeSpan.FromMilliseconds(100);
		return client;
	}

	private static void Play(LanClient client, List<MessageToUI> ui = null) {
		client.SnapshotReceived = _ => { };
		client.UiMessageReceived = json => ui?.Add(NetSerialization.DeserializeMessageToUI(json));
		client.ReconnectAutomatically = true;
	}

	[Fact]
	public async Task ConnectionsRunThroughTheRelayAndSharedFramesGoOnce() {
		await using TestRelay relay = await TestRelay.Start();
		System.Collections.Concurrent.ConcurrentQueue<LanTransport> arrived = new();
		using RelayHostLink link = new(relay.Url, arrived.Enqueue);
		link.Start();
		await RelayServerTests.WaitUntil(() => link.State == RelayHostLink.LinkState.Online);

		RelayEndpoint endpoint = new(relay.Url, link.Code);
		using LanConnection ann = new(await endpoint.ConnectAsync(TimeSpan.FromSeconds(10)));
		using LanConnection bob = new(await endpoint.ConnectAsync(TimeSpan.FromSeconds(10)));
		await RelayServerTests.WaitUntil(() => arrived.Count == 2);
		List<LanConnection> guests = arrived.Select(t => new LanConnection(t)).ToList();

		// Each guest's frames reach the host on its own connection.
		ann.Send(FrameKind.Hello, Encoding.UTF8.GetBytes("ann"));
		bob.Send(FrameKind.Hello, Encoding.UTF8.GetBytes("bob"));
		List<string> heard = [];
		await RelayServerTests.WaitUntil(() => {
			foreach (LanConnection guest in guests) {
				if (guest.TryReceive(out Frame frame)) {
					heard.Add(Encoding.UTF8.GetString(frame.payload));
				}
			}
			return heard.Count == 2;
		});
		Assert.Equal(["ann", "bob"], heard.Order());

		// What the host sends everyone goes through the relay once.
		byte[] large = new byte[200_000];
		Random.Shared.NextBytes(large);
		long fannedOut = link.FannedOutMessages;
		for (int i = 0; i < 5; ++i) {
			foreach (LanConnection guest in guests) {
				guest.Send(FrameKind.UiMessage, large);
			}
		}
		// And what's for one guest goes just to it.
		guests[0].Send(FrameKind.Lobby, [1]);
		guests[1].Send(FrameKind.Lobby, [2]);
		foreach (LanConnection client in new[] { ann, bob }) {
			List<Frame> frames = [];
			await RelayServerTests.WaitUntil(() => {
				while (client.TryReceive(out Frame frame)) {
					frames.Add(frame);
				}
				return frames.Count == 6;
			});
			Assert.All(frames.Take(5), f => Assert.Equal(large, f.payload));
			Assert.Equal(FrameKind.Lobby, frames[5].kind);
		}
		Assert.True(link.FannedOutMessages - fannedOut >= 4, $"Only {link.FannedOutMessages - fannedOut} frames went to both at once");

		// A guest that leaves closes its connection at the host.
		ann.Dispose();
		await RelayServerTests.WaitUntil(() => guests.Any(g => g.IsClosed));
		Assert.Single(guests, g => g.IsClosed);
		foreach (LanConnection guest in guests) {
			guest.Dispose();
		}
	}

	[Fact]
	public async Task JoiningWithAWrongCodeSaysWhy() {
		await using TestRelay relay = await TestRelay.Start();
		RelayException e = await Assert.ThrowsAsync<RelayException>(
			() => LanClient.ConnectAsync(new RelayEndpoint(relay.Url, "AAAAAA"), "Guest"));
		Assert.Equal(RelayCloseCodes.UnknownRoom, e.CloseCode);
		Assert.Contains("AAA-AAA", e.Message);

		// And with no relay there, likewise.
		await relay.DisposeAsync();
		await Assert.ThrowsAnyAsync<IOException>(() => LanClient.ConnectAsync(new RelayEndpoint(relay.Url, "AAAAAA"), "Guest"));
	}

	[Fact]
	public async Task TwoGuestsPlayThroughTheRelayAndComeBackWhenAnyoneDrops() {
		await using TestRelay relay = await TestRelay.Start();
		SaveGame save = SaveGameFixture.ThreeHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false);
		string code = GoOnline(host, relay);
		List<ID> seatIDs = host.Seats.Select(s => s.playerID).ToList();

		using LanClient ann = Join(relay, code, "Ann");
		using LanClient bob = Join(relay, code, "Bob");
		LanClient[] both = [ann, bob];
		PumpUntil(host, both, () => ann.Lobby != null && bob.Lobby != null);
		ann.ClaimSeat(seatIDs[0]);
		bob.ClaimSeat(seatIDs[1]);
		PumpUntil(host, both, () => host.AllSeatsTaken);
		Assert.Equal(["Ann", "Bob"], host.Seats.Select(s => s.takenBy));

		C7GameData.GameData gameData = await CreateGame(save);
		Player[] humans = gameData.players.Where(p => p.isHuman).ToArray();
		host.StartGame();
		PumpUntil(host, both, () => ann.StartingGame != null && bob.StartingGame != null);
		Assert.Equal([humans[1].id], ann.PlayerIDs);
		Assert.Equal([humans[2].id], bob.PlayerIDs);
		List<MessageToUI> annUi = [], bobUi = [];
		Play(ann, annUi);
		Play(bob, bobUi);

		// They play a turn, sent the game as patches, mostly once for both.
		long fannedOut = host.Online.FannedOutMessages;
		new MsgEndTurn().send();
		PumpUntil(host, both, () => annUi.OfType<MsgStartTurn>().Any(m => m.player == humans[1]));
		ann.SendCommand(new MsgEndTurn());
		PumpUntil(host, both, () => bobUi.OfType<MsgStartTurn>().Any(m => m.player == humans[2]));
		bob.SendCommand(new MsgEndTurn());
		PumpUntil(host, both, () => gameData.turn == 1);
		Assert.True(ann.SnapshotDeltasReceived > 0);
		Assert.True(bob.SnapshotDeltasReceived > 0);
		Assert.True(host.Online.FannedOutMessages > fannedOut);

		// Ann drops, and is back by herself, in her seat.
		int annWhole = ann.WholeSnapshotsReceived;
		relay.DropGuest(code, 1);
		PumpUntil(host, both, () => ann.Reconnecting);
		PumpUntil(host, both, () => !ann.Reconnecting && host.AllSeatsTaken && ann.WholeSnapshotsReceived > annWhole);
		Assert.Equal([humans[1].id], ann.PlayerIDs);
		Assert.True(bob.IsConnected);

		// The host drops: it claims its code again, and both are back.
		relay.DropHost(code);
		PumpUntil(host, both, () => ann.Reconnecting && bob.Reconnecting);
		PumpUntil(host, both, () => !ann.Reconnecting && !bob.Reconnecting && host.AllSeatsTaken);
		Assert.Equal(code, host.Online.Code);
		Assert.Equal(RelayHostLink.LinkState.Online, host.Online.State);

		// And the game goes on.
		new MsgEndTurn().send();
		PumpUntil(host, both, () => annUi.OfType<MsgStartTurn>().Count(m => m.player == humans[1]) >= 2);
		ann.SendCommand(new MsgEndTurn());
		PumpUntil(host, both, () => bobUi.OfType<MsgStartTurn>().Count(m => m.player == humans[2]) >= 2);
		bob.SendCommand(new MsgEndTurn());
		PumpUntil(host, both, () => gameData.turn == 2);
		while (EngineStorage.HasPendingMessagesToEngine()) {
			EngineStorage.ProcessNextMessageToEngine();
		}
		byte[] expected = System.Security.Cryptography.SHA256.HashData(LanProtocol.SnapshotOf(gameData).ToCompactJSON());
		PumpUntil(host, both, () => ann.ReceivedSnapshotHash?.AsSpan().SequenceEqual(expected) == true
			&& bob.ReceivedSnapshotHash?.AsSpan().SequenceEqual(expected) == true);
	}

	[Fact]
	public async Task AResumedOnlineGameKeepsItsCode() {
		await using TestRelay relay = await TestRelay.Start();
		using TempDirectory saves = new("online-autosave");
		SaveGame save = SaveGameFixture.TwoHumanSave();
		LanHost host = new("Host", save, port: 0, answerDiscovery: false) { AutosaveDirectory = saves.Path };
		string code = GoOnline(host, relay);
		ID seatID = host.Seats[0].playerID;
		using LanClient guest = Join(relay, code, "Guest");
		PumpUntil(host, [guest], () => guest.Lobby != null);
		guest.ClaimSeat(seatID);
		PumpUntil(host, [guest], () => host.AllSeatsTaken);

		await CreateGame(save);
		host.StartGame();
		PumpUntil(host, [guest], () => guest.StartingGame != null);
		Play(guest);
		await host.LastAutosave;

		LanResumeInfo info = LanAutosave.ReadResumeInfo(saves.Path);
		Assert.Equal(relay.Url, info.relayUrl);
		Assert.Equal(code, info.onlineCode);
		Assert.Equal(host.Online.Key, info.onlineKey);

		// The host's game ends; the guest keeps trying.
		host.Dispose();
		PumpUntil(null, [guest], () => guest.Reconnecting);

		// It hosts the game again from the save, online with the same code,
		// and the guest finds its seat waiting.
		EngineStorage.ResetNetworking();
		SaveGame resumedSave = SaveGame.FromJSON(File.ReadAllBytes(LanAutosave.SavePath(saves.Path)));
		using LanHost resumed = LanHost.Resume("Host", resumedSave, info, port: 0, answerDiscovery: false);
		resumed.HostOnline(info.relayUrl, info.onlineCode, info.onlineKey);
		PumpUntil(resumed, [guest], () => !guest.Reconnecting && resumed.AllSeatsTaken);
		Assert.Equal(code, resumed.Online.Code);
		Assert.True(guest.HostIsResuming);

		C7GameData.GameData gameData = await CreateGame(resumedSave);
		resumed.StartGame();
		PumpUntil(resumed, [guest], () => !guest.HostIsResuming);
		new MsgEndTurn().send();
		PumpUntil(resumed, [guest], () => EngineStorage.activePlayerID == seatID);
		guest.SendCommand(new MsgEndTurn());
		PumpUntil(resumed, [guest], () => gameData.turn == 1);
	}
}
