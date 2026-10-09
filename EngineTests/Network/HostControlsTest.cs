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
using C7Relay;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.Network;

// What a LAN host controls about who plays: a password to join, removing and
// banning guests, and how its game is listed publicly.
public class HostControlsTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;

	public HostControlsTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		// Engine state is static; parallelization is disabled repo-wide (XunitSettings.cs)
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
				throw new TimeoutException("The LAN game never got there");
			}
			Thread.Sleep(5);
		}
	}

	private static void PumpUntil(LanHost host, LanClient client, Func<bool> condition) {
		PumpUntil(host, [client], condition);
	}

	private static LanClient Connect(LanHost host, string name, string token = null, string password = null) {
		return LanClient.ConnectAsync(new LanAddressEndpoint("127.0.0.1", host.Port), name, token, password: password)
			.GetAwaiter().GetResult();
	}

	// A guest that has joined the host and taken the seat.
	private static LanClient JoinAndTake(LanHost host, string name, ID seatID, string password = null) {
		LanClient guest = Connect(host, name, password: password);
		PumpUntil(host, guest, () => guest.Lobby != null);
		guest.ClaimSeat(seatID);
		PumpUntil(host, guest, () => guest.YourSeats.Contains(seatID));
		return guest;
	}

	private static void Play(LanClient guest) {
		guest.SnapshotReceived = _ => { };
		guest.UiMessageReceived = _ => { };
		guest.FirstReconnectDelay = TimeSpan.FromMilliseconds(20);
		guest.MaxReconnectDelay = TimeSpan.FromMilliseconds(100);
	}

	[Fact]
	public void TheHostKeepsNoPasswordOnlyWhatChecksIt() {
		string salt = GamePassword.NewSalt();
		string verifier = GamePassword.Verifier("swordfish", salt);
		Assert.DoesNotContain("swordfish", verifier);
		Assert.Equal(verifier, GamePassword.Verifier("swordfish", salt));
		Assert.NotEqual(verifier, GamePassword.Verifier("swordfish", GamePassword.NewSalt()));

		string nonce = GamePassword.NewNonce();
		Assert.True(GamePassword.Check(verifier, nonce, GamePassword.Proof(verifier, nonce)));
		Assert.True(GamePassword.Check(verifier, nonce, GamePassword.Proof(verifier, nonce).ToLowerInvariant()));
		Assert.False(GamePassword.Check(verifier, nonce, GamePassword.Proof(GamePassword.Verifier("Swordfish", salt), nonce)));
		// An answer to another nonce is no good.
		Assert.False(GamePassword.Check(verifier, nonce, GamePassword.Proof(verifier, GamePassword.NewNonce())));
		Assert.False(GamePassword.Check(verifier, nonce, null));
	}

	[Fact]
	public void AGuestNeedsThePasswordToJoin() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false) { AllowSpectators = true };
		host.SetPassword("swordfish");
		Assert.True(host.HasPassword);

		// Without it, the guest is asked for it, and sees nothing of the
		// lobby until it gives it.
		using LanClient guest = Connect(host, "Guest");
		PumpUntil(host, guest, () => guest.PasswordRequest != null);
		Assert.False(guest.PasswordRequest.wrong);
		Assert.Null(guest.Lobby);

		guest.SendPassword("sword fish");
		PumpUntil(host, guest, () => guest.PasswordRequest != null);
		Assert.True(guest.PasswordRequest.wrong);
		Assert.Equal(GamePassword.MaxWrongAttempts - 1, guest.PasswordRequest.attemptsLeft);
		Assert.Null(guest.Lobby);

		guest.SendPassword("swordfish");
		PumpUntil(host, guest, () => guest.Lobby != null);
		Assert.Null(guest.PasswordRequest);
		Assert.Null(guest.RejectedReason);

		// Given when joining, it's sent without asking.
		using LanClient other = Connect(host, "Other", password: "swordfish");
		bool asked = false;
		other.LobbyChanged += () => asked |= other.PasswordRequest != null;
		PumpUntil(host, other, () => other.Lobby != null);
		Assert.False(asked);

		// Someone who asks to watch first watches once in.
		using LanClient watcher = Connect(host, "Watcher");
		watcher.Watch();
		PumpUntil(host, watcher, () => watcher.PasswordRequest != null);
		watcher.SendPassword("swordfish");
		PumpUntil(host, watcher, () => host.Spectators.Contains("Watcher"));
	}

	[Fact]
	public void TooManyWrongPasswordsAreTurnedAway() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false);
		host.SetPassword("swordfish");
		using LanClient guest = Connect(host, "Guest", password: "guess");
		for (int i = 1; i < GamePassword.MaxWrongAttempts; ++i) {
			PumpUntil(host, guest, () => guest.PasswordRequest != null);
			guest.SendPassword($"guess {i}");
		}
		PumpUntil(host, guest, () => guest.RejectedReason != null);
		Assert.Contains("wrong password", guest.RejectedReason);
		Assert.Null(guest.Lobby);
	}

	[Fact]
	public async Task AGuestBackWithItsTokenNeedsNoPassword() {
		using TempDirectory saves = new("lan-autosave");
		SaveGame save = SaveGameFixture.TwoHumanSave();
		LanResumeInfo info;
		ID seatID;
		string token;
		using (LanHost host = new("Host", save, port: 0, answerDiscovery: false) { AutosaveDirectory = saves.Path }) {
			host.SetPassword("swordfish");
			seatID = host.Seats[0].playerID;
			LanClient guest = JoinAndTake(host, "Guest", seatID, "swordfish");
			token = guest.ReconnectToken;
			await CreateGame(save);
			host.StartGame();
			PumpUntil(host, guest, () => guest.StartingGame != null);
			guest.Dispose();
			PumpUntil(host, [], () => host.Seats[0].disconnected);

			using LanClient back = Connect(host, "Guest", token);
			PumpUntil(host, back, () => back.StartingGame != null);
			Assert.Null(back.PasswordRequest);
			Assert.Equal([seatID], back.PlayerIDs);
			await host.LastAutosave;
			info = host.ResumeInfo();
		}

		// The resumed game keeps the password, though not as itself.
		Assert.NotNull(info.passwordVerifier);
		Assert.DoesNotContain("swordfish", System.Text.Encoding.UTF8.GetString(NetSerialization.SerializeData(info)));
		EngineStorage.ResetNetworking();
		using LanHost resumed = LanHost.Resume("Host", save, info, port: 0, answerDiscovery: false);
		Assert.True(resumed.HasPassword);
		using LanClient stranger = Connect(resumed, "Stranger");
		PumpUntil(resumed, stranger, () => stranger.PasswordRequest != null);
		stranger.SendPassword("swordfish");
		PumpUntil(resumed, stranger, () => stranger.Lobby != null);
		using LanClient returning = Connect(resumed, "Guest", token);
		PumpUntil(resumed, returning, () => returning.YourSeats.Count == 1);
		Assert.Null(returning.PasswordRequest);
	}

	[Fact]
	public async Task RemovingAGuestFreesItsSeats() {
		SaveGame save = SaveGameFixture.ThreeHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false);
		List<ID> seatIDs = host.Seats.Select(s => s.playerID).ToList();
		LanClient guest = JoinAndTake(host, "Guest", seatIDs[0]);
		guest.ClaimSeat(seatIDs[1]);
		PumpUntil(host, guest, () => guest.YourSeats.Count == 2);
		string token = guest.ReconnectToken;
		await CreateGame(save);
		host.StartGame();
		PumpUntil(host, guest, () => guest.StartingGame != null);
		Play(guest);
		guest.ReconnectAutomatically = true;

		Assert.True(host.Kick(seatIDs[0]));
		PumpUntil(host, guest, () => guest.RejectedReason != null);
		Assert.Equal(LanHost.KickedReason, guest.RejectedReason);
		Assert.False(guest.Reconnecting);
		// Both its seats are open, not held for it.
		Assert.All(host.Seats, s => Assert.Null(s.takenBy));
		Assert.All(host.Seats, s => Assert.False(s.disconnected));
		Assert.Equal(seatIDs, host.AbsentPlayers);

		// Its token is no good for them any more, but it can join afresh
		// and anyone can take them.
		using LanClient back = Connect(host, "Guest", token);
		PumpUntil(host, back, () => back.RejectedReason != null);
		using LanClient other = Connect(host, "Other");
		PumpUntil(host, other, () => other.Lobby != null);
		other.ClaimSeat(seatIDs[0]);
		PumpUntil(host, other, () => other.StartingGame != null);
		Assert.Equal([seatIDs[0]], other.PlayerIDs);
	}

	[Fact]
	public async Task ABannedGuestStaysBannedEvenInTheResumedGame() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		LanResumeInfo info;
		ID seatID;
		string token;
		using (LanHost host = new("Host", save, port: 0, answerDiscovery: false)) {
			seatID = host.Seats[0].playerID;
			LanClient guest = JoinAndTake(host, "Guest", seatID);
			token = guest.ReconnectToken;
			await CreateGame(save);
			host.StartGame();
			PumpUntil(host, guest, () => guest.StartingGame != null);
			Play(guest);

			// Banned once it has gone, with its seat held.
			guest.Dispose();
			PumpUntil(host, [], () => host.Seats[0].disconnected);
			Assert.True(host.Kick(seatID, ban: true));
			Assert.Null(host.Seats[0].takenBy);
			Assert.False(host.Seats[0].disconnected);

			using LanClient back = Connect(host, "Guest", token);
			PumpUntil(host, back, () => back.RejectedReason != null);
			Assert.Equal(LanHost.BannedReason, back.RejectedReason);
			info = host.ResumeInfo();
		}
		Assert.Contains(token, info.bannedTokens);

		EngineStorage.ResetNetworking();
		using LanHost resumed = LanHost.Resume("Host", save, info, port: 0, answerDiscovery: false);
		using LanClient again = Connect(resumed, "Guest", token);
		PumpUntil(resumed, again, () => again.RejectedReason != null);
		Assert.Equal(LanHost.BannedReason, again.RejectedReason);
	}

	[Fact]
	public void TheHostCanBanAConnectedGuestAndRemoveSpectators() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false) { AllowSpectators = true };
		ID seatID = host.Seats[0].playerID;
		using LanClient guest = JoinAndTake(host, "Guest", seatID);
		string token = guest.ReconnectToken;
		Assert.True(host.Kick(seatID, ban: true));
		PumpUntil(host, guest, () => guest.RejectedReason != null);
		Assert.Equal(LanHost.BannedReason, guest.RejectedReason);
		Assert.Null(host.Seats[0].takenBy);
		Assert.Contains(token, host.ResumeInfo().bannedTokens);
		// This machine's address isn't banned, which would ban everyone on it.
		Assert.Empty(host.ResumeInfo().bannedAddresses);

		using LanClient watcher = Connect(host, "Watcher");
		watcher.Watch();
		PumpUntil(host, watcher, () => host.Spectators.Contains("Watcher"));
		Assert.True(host.KickSpectator("Watcher"));
		PumpUntil(host, watcher, () => watcher.RejectedReason != null);
		Assert.Equal(LanHost.KickedReason, watcher.RejectedReason);
		Assert.Empty(host.Spectators);
		Assert.False(host.KickSpectator("Nobody"));

		// And can keep anyone from watching.
		host.AllowSpectators = false;
		using LanClient another = Connect(host, "Another");
		another.Watch();
		PumpUntil(host, another, () => another.RejectedReason != null);
		Assert.Empty(host.Spectators);
	}

	[Fact]
	public async Task TheListingFollowsTheGame() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		using LanHost host = new("Ann", save, port: 0, answerDiscovery: false);
		ID seatID = host.Seats[0].playerID;
		GameListing listing = host.CurrentListing();
		Assert.Equal("Ann's game", listing.name);
		Assert.Equal("Ann", listing.hostName);
		Assert.Equal(2, listing.seatsTotal);
		Assert.Equal(1, listing.seatsTaken);
		Assert.False(listing.started);
		Assert.False(listing.hasPassword);
		Assert.Equal($"{save.Map.tilesWide}x{save.Map.tilesTall}", listing.mapSize);

		host.PublicName = "  Come play  ";
		host.PublicDescription = " ";
		host.TurnTimeLimit = TimeSpan.FromMinutes(5);
		host.SetPassword("swordfish");
		listing = host.CurrentListing();
		Assert.Equal("Come play", listing.name);
		Assert.Null(listing.description);
		Assert.Equal(300, listing.turnSeconds);
		Assert.True(listing.hasPassword);

		LanClient guest = JoinAndTake(host, "Guest", seatID, "swordfish");
		Assert.Equal(2, host.CurrentListing().seatsTaken);

		C7GameData.GameData gameData = await CreateGame(save);
		host.StartGame();
		PumpUntil(host, guest, () => guest.StartingGame != null);
		Play(guest);
		Assert.True(host.CurrentListing().started);
		Assert.Equal(gameData.turn, host.CurrentListing().turn);

		// A guest who drops still counts: their seat waits for them.
		guest.Dispose();
		PumpUntil(host, [], () => host.Seats[0].disconnected);
		Assert.Equal(2, host.CurrentListing().seatsTaken);
		host.ReleaseSeat(seatID);
		Assert.Equal(1, host.CurrentListing().seatsTaken);
	}
}
