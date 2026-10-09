using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using C7Engine;
using C7Engine.Network;
using C7GameData;
using C7GameData.Save;
using C7Relay;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.Network;

// What keeps a LAN host's guests and spectators from doing more than they
// should: watching hidden games, guessing passwords, holding connections
// open, sending frames too large or of the wrong kind, and the names they
// give.
public class NetHardeningTest : IClassFixture<SaveGameFixture>, IDisposable {
	public NetHardeningTest(SaveGameFixture fixture) {
		// Engine state is static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.messagesToUI.Clear();
		EngineStorage.pendingMessages.Clear();
		EngineStorage.ResetNetworking();
	}

	public void Dispose() {
		EngineStorage.ResetNetworking();
	}

	private static readonly TimeSpan PumpTimeout = TimeSpan.FromSeconds(30);

	private static void PumpUntil(LanHost host, IEnumerable<LanClient> clients, Func<bool> condition) {
		Stopwatch pumping = Stopwatch.StartNew();
		while (!condition()) {
			Assert.True(pumping.Elapsed < PumpTimeout, "The LAN game never got there");
			host.Poll();
			foreach (LanClient client in clients) {
				client.Poll();
			}
			Thread.Sleep(5);
		}
	}

	private static void PumpUntil(LanHost host, LanClient client, Func<bool> condition) => PumpUntil(host, [client], condition);

	private static LanClient Connect(LanHost host, string name, string token = null) {
		return LanClient.ConnectAsync(new LanAddressEndpoint("127.0.0.1", host.Port), name, token).GetAwaiter().GetResult();
	}

	private static LanHost NewHost() => new("Host", SaveGameFixture.TwoHumanSave(), port: 0, answerDiscovery: false);

	// A connection to the host that sends exactly what a test wants.
	private static LanConnection RawConnection(LanHost host) {
		return new LanConnection(new TcpClient("127.0.0.1", host.Port));
	}

	[Fact]
	public void SpectatorsAreOffWhileHidingWhatPlayersCantSee() {
		using LanHost host = NewHost();
		Assert.True(host.HideUnseen);
		Assert.False(host.AllowSpectators);
		using LanClient turnedAway = Connect(host, "Watcher");
		turnedAway.Watch();
		PumpUntil(host, turnedAway, () => turnedAway.RejectedReason != null);
		Assert.Equal(LanHost.NoSpectatorsReason, turnedAway.RejectedReason);

		// Without hiding, guests see everything anyway.
		host.HideUnseen = false;
		Assert.True(host.AllowSpectators);
		using LanClient watcher = Connect(host, "Watcher");
		watcher.Watch();
		PumpUntil(host, watcher, () => host.Spectators.Count == 1);

		// Hiding again stops them, since they'd see what guests aren't sent.
		host.HideUnseen = true;
		PumpUntil(host, watcher, () => watcher.RejectedReason != null);
		Assert.Equal(LanHost.NoSpectatorsReason, watcher.RejectedReason);
		Assert.Empty(host.Spectators);
	}

	[Fact]
	public void TurningSpectatorsOffStopsThoseWatching() {
		using LanHost host = NewHost();
		host.AllowSpectators = true;
		using LanClient watcher = Connect(host, "Watcher");
		watcher.Watch();
		PumpUntil(host, watcher, () => host.Spectators.Count == 1);
		host.AllowSpectators = false;
		PumpUntil(host, watcher, () => watcher.RejectedReason != null);
		Assert.Empty(host.Spectators);
	}

	[Fact]
	public void OnlySoManyWatchAndEachHasItsOwnID() {
		using LanHost host = NewHost();
		host.AllowSpectators = true;
		List<LanClient> watchers = [];
		try {
			for (int i = 0; i < LanHost.MaxSpectators; ++i) {
				LanClient watcher = Connect(host, "Watcher");
				watcher.Watch();
				watchers.Add(watcher);
			}
			PumpUntil(host, watchers, () => host.Spectators.Count == LanHost.MaxSpectators);
			Assert.Equal(LanHost.MaxSpectators, host.SpectatorList.Select(s => s.id).Distinct().Count());

			using LanClient onceTooMany = Connect(host, "Watcher");
			onceTooMany.Watch();
			PumpUntil(host, onceTooMany, () => onceTooMany.RejectedReason != null);
			Assert.Equal(LanHost.MaxSpectators, host.Spectators.Count);

			// Removing one by its ID removes only that one, though they all
			// have the same name.
			int second = host.SpectatorList[1].id;
			Assert.True(host.KickSpectator(second));
			Assert.DoesNotContain(host.SpectatorList, s => s.id == second);
			Assert.Equal(LanHost.MaxSpectators - 1, host.Spectators.Count);
		} finally {
			foreach (LanClient watcher in watchers) {
				watcher.Dispose();
			}
		}
	}

	[Fact]
	public void ASpectatorComesBackWithItsToken() {
		using LanHost host = NewHost();
		host.AllowSpectators = true;
		host.SetPassword("swordfish");
		string token;
		using (LanClient watcher = LanClient.ConnectAsync(new LanAddressEndpoint("127.0.0.1", host.Port), "Watcher",
			password: "swordfish").GetAwaiter().GetResult()) {
			watcher.Watch();
			PumpUntil(host, watcher, () => host.Spectators.Count == 1 && watcher.ReconnectToken != null);
			token = watcher.ReconnectToken;
		}
		PumpUntil(host, [], () => host.Spectators.Count == 0);

		// Back without the password, to watch again.
		using LanClient back = Connect(host, "Watcher", token);
		back.Watch();
		PumpUntil(host, back, () => host.Spectators.Count == 1);
		Assert.Null(back.PasswordRequest);
		Assert.Null(back.RejectedReason);
	}

	[Fact]
	public void WrongPasswordsCountAcrossConnections() {
		using LanHost host = NewHost();
		host.SetPassword("swordfish");
		using (LanClient guest = Connect(host, "Guest")) {
			for (int i = 0; i < GamePassword.MaxWrongAttempts - 1; ++i) {
				PumpUntil(host, guest, () => guest.PasswordRequest != null);
				guest.SendPassword($"guess {i}");
			}
			PumpUntil(host, guest, () => guest.PasswordRequest?.wrong == true);
			Assert.Equal(1, guest.PasswordRequest.attemptsLeft);
		}

		// Connecting again doesn't give more tries.
		using LanClient again = Connect(host, "Guest");
		PumpUntil(host, again, () => again.PasswordRequest != null);
		Assert.Equal(1, again.PasswordRequest.attemptsLeft);
		again.SendPassword("guess again");
		PumpUntil(host, again, () => again.RejectedReason != null);

		// And the address must wait, even with the right one.
		using LanClient locked = Connect(host, "Guest");
		PumpUntil(host, locked, () => locked.RejectedReason != null);
		Assert.Contains("Try again in", locked.RejectedReason);
	}

	[Fact]
	public void AGuestThatNeverSaysHelloIsDropped() {
		TimeSpan timeout = LanHost.HelloTimeout;
		LanHost.HelloTimeout = TimeSpan.FromMilliseconds(200);
		try {
			using LanHost host = NewHost();
			using LanConnection silent = RawConnection(host);
			PumpUntil(host, [], () => silent.IsClosed);
		} finally {
			LanHost.HelloTimeout = timeout;
		}
	}

	[Fact]
	public void ALargeFrameBeforeHelloDropsTheConnection() {
		using LanHost host = NewHost();
		using LanConnection guest = RawConnection(host);
		guest.Send(FrameKind.Hello, new HelloInfo(LanProtocol.Version, new string('a', 2 * LanProtocol.MaxFrameBytesBeforeAdmission)));
		PumpUntil(host, [], () => guest.IsClosed);
	}

	[Fact]
	public void FramesAGuestNeverSendsCountAsBad() {
		using LanHost host = NewHost();
		using LanConnection guest = RawConnection(host);
		guest.Send(FrameKind.Hello, new HelloInfo(LanProtocol.Version, "Guest"));
		for (int i = 0; i < LanConnection.MaxBadFramesInARow; ++i) {
			guest.Send(FrameKind.Lobby, Encoding.UTF8.GetBytes("{}"));
		}
		PumpUntil(host, [], () => guest.IsClosed);
	}

	[Fact]
	public void AHelloThatCantBeReadIsAnswered() {
		using LanHost host = NewHost();
		using LanConnection guest = RawConnection(host);
		guest.Send(FrameKind.Hello, Encoding.UTF8.GetBytes("{"));
		Frame rejected = null;
		PumpUntil(host, [], () => guest.TryReceive(out rejected) && rejected.kind == FrameKind.Rejected);
		Assert.Contains("different version", Encoding.UTF8.GetString(rejected.payload));
	}

	[Fact]
	public void NamesAreTidiedAndNeverTheAddress() {
		Assert.Equal("evil name", LanHost.TidyName("  \u202Eevil\n  name "));
		Assert.Null(LanHost.TidyName(" \u200B "));
		Assert.Equal(LanHost.MaxPlayerNameLength, LanHost.TidyName(new string('x', 500)).Length);

		using LanHost host = NewHost();
		using LanClient guest = Connect(host, "   ");
		PumpUntil(host, guest, () => guest.Lobby != null);
		guest.ClaimSeat(host.Seats[0].playerID);
		PumpUntil(host, guest, () => guest.YourSeats.Count == 1);
		Assert.Equal(LanHost.UnnamedGuest, host.Seats[0].takenBy);
	}

	[Fact]
	public void LeaderArtOnlyComesFromTheGamesOwnFiles() {
		Assert.True(LanProtocol.IsSafeArtPath("art\\advisors\\CE_all.pcx"));
		Assert.True(LanProtocol.IsSafeArtPath("Art/Leaderheads/AM.pcx"));
		Assert.False(LanProtocol.IsSafeArtPath(null));
		Assert.False(LanProtocol.IsSafeArtPath("art\\..\\..\\secret.pcx"));
		Assert.False(LanProtocol.IsSafeArtPath("C:\\art\\advisors\\CE_all.pcx"));
		Assert.False(LanProtocol.IsSafeArtPath("/etc/art/passwd.pcx"));
		Assert.False(LanProtocol.IsSafeArtPath("art\\advisors\\CE_all.png"));
	}

	[Fact]
	public void TheResumeInfoIsSavedAsSeatsAreTaken() {
		using TempDirectory saves = new("lan-resume");
		LanResumeInfo info = new("Host", 0, null, false, []);
		using LanHost host = LanHost.Resume("Host", SaveGameFixture.TwoHumanSave(), info, port: 0, answerDiscovery: false);
		host.AutosaveDirectory = saves.Path;
		using LanClient guest = Connect(host, "Guest");
		PumpUntil(host, guest, () => guest.Lobby != null);
		guest.ClaimSeat(host.Seats[0].playerID);
		PumpUntil(host, guest, () => LanAutosave.ReadResumeInfo(saves.Path)?.seats?.Count == 1);
		Assert.Equal(guest.ReconnectToken, LanAutosave.ReadResumeInfo(saves.Path).seats[0].reconnectToken);
	}

	[Fact]
	public void TheRelaySaysWhereAGameMoved() {
		string reason = RelayProtocol.MovedReason("KQ74MZ");
		Assert.Equal("KQ74MZ", RelayProtocol.MovedTo(reason));
		Assert.Null(RelayProtocol.MovedTo("The host isn't connected to the relay right now."));
		Assert.Equal("KQ74MZ", new RelayException(reason, RelayCloseCodes.Moved).MovedTo);
		Assert.True(RelayProtocol.IsBanScope("0123456789ABCDEF0123456789ABCDEF"));
		Assert.False(RelayProtocol.IsBanScope("short"));
		Assert.False(RelayProtocol.IsBanScope("0123456789ABCDEF:123456789ABCDEF"));
	}

	[Fact]
	public void DiscoveryOnlyAnswersLocalNetworks() {
		Assert.True(LanHost.IsLocalNetwork(System.Net.IPAddress.Parse("192.168.1.20")));
		Assert.True(LanHost.IsLocalNetwork(System.Net.IPAddress.Parse("10.0.0.5")));
		Assert.True(LanHost.IsLocalNetwork(System.Net.IPAddress.Parse("172.20.1.1")));
		Assert.True(LanHost.IsLocalNetwork(System.Net.IPAddress.Parse("100.100.1.1")));
		Assert.True(LanHost.IsLocalNetwork(System.Net.IPAddress.Parse("fe80::1")));
		Assert.False(LanHost.IsLocalNetwork(System.Net.IPAddress.Parse("8.8.8.8")));
		Assert.False(LanHost.IsLocalNetwork(System.Net.IPAddress.Parse("172.32.1.1")));
		Assert.False(LanHost.IsLocalNetwork(System.Net.IPAddress.Parse("2001:db8::1")));
	}
}
