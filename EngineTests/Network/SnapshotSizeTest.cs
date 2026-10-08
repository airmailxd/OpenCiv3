using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.Network;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace EngineTests.Network;

// How much a LAN host sends: a whole snapshot, compressed, and the patches
// that follow it as the game is played. The sizes are written to the test
// output, and only checked against generous bounds.
public class SnapshotSizeTest : RemoteSaveLoader, IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;
	private readonly ITestOutputHelper output;

	public SnapshotSizeTest(SaveGameFixture fixture, ITestOutputHelper output) {
		this.fixture = fixture;
		this.output = output;
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

	private static byte[] GzipFastest(byte[] json) {
		MemoryStream compressed = new();
		using (GZipStream gzip = new(compressed, CompressionLevel.Fastest, leaveOpen: true)) {
			gzip.Write(json);
		}
		return compressed.ToArray();
	}

	// The fastest of a few runs, in milliseconds, once the JIT has optimized
	// the code as it would in a game that has been running a while.
	private static double Time(Action action) {
		Stopwatch warming = Stopwatch.StartNew();
		while (warming.ElapsedMilliseconds < 500) {
			action();
		}
		double best = double.MaxValue;
		for (int i = 0; i < 5; ++i) {
			Stopwatch watch = Stopwatch.StartNew();
			action();
			best = Math.Min(best, watch.Elapsed.TotalMilliseconds);
		}
		return best;
	}

	private void Measure(string name, C7GameData.GameData gameData) {
		EncodedSnapshot start = LanProtocol.EncodeSnapshot(LanProtocol.SnapshotOf(gameData));
		byte[] gzip = GzipFastest(start.Json);
		byte[] zstd = start.Compressed;
		double gzipTime = Time(() => GzipFastest(start.Json));
		double zstdTime = Time(() => SnapshotCompression.Compress(start.Json));
		output.WriteLine($"{name}: JSON {start.Json.Length:N0} bytes; whole: gzip fastest {gzip.Length:N0} bytes ({gzipTime:F1} ms), "
			+ $"zstd {zstd.Length:N0} bytes ({zstdTime:F1} ms)");
		Assert.True(zstd.Length < gzip.Length);

		// One unit fortifies.
		Player human = gameData.players.First(p => p.isHuman);
		new MsgSetFortification(human.units.First(u => !u.isFortified).id, true) { playerID = human.id }.send();
		ProcessEngineMessages();
		EncodedSnapshot fortified = LanProtocol.EncodeSnapshot(LanProtocol.SnapshotOf(gameData), start);
		Report(name, "after a unit fortifies", start, fortified);

		// The humans end the turn, and the AIs move.
		int turn = gameData.turn;
		foreach (Player player in gameData.players.Where(p => p.isHuman)) {
			new MsgEndTurn { playerID = player.id }.send();
		}
		Stopwatch waiting = Stopwatch.StartNew();
		while (gameData.turn == turn) {
			Assert.True(waiting.Elapsed < TimeSpan.FromSeconds(120), "The turn never ended");
			if (EngineStorage.HasPendingMessagesToEngine()) {
				EngineStorage.ProcessNextMessageToEngine();
			} else {
				Thread.Sleep(5);
			}
		}
		ProcessEngineMessages();
		EncodedSnapshot nextTurn = LanProtocol.EncodeSnapshot(LanProtocol.SnapshotOf(gameData), fortified);
		Report(name, "after the turn ends and the AIs move", fortified, nextTurn);
	}

	private void Report(string name, string change, EncodedSnapshot before, EncodedSnapshot after) {
		byte[] patch = after.PatchFrom(before);
		Assert.NotNull(patch);
		double time = Time(() => SnapshotCompression.CompressPatch(after.Json, before.Json, after.Json.Length));
		output.WriteLine($"{name}: patch {change}: {patch.Length:N0} bytes ({time:F1} ms), whole zstd {after.Compressed.Length:N0} bytes");
		Assert.True(patch.Length * 10 < after.Compressed.Length, $"The patch {change} is {patch.Length} bytes");
		ReceivedSnapshot applied = LanProtocol.ReadSnapshot(FrameKind.SnapshotDelta, patch, new ReceivedSnapshot(before.Json, before.Hash));
		Assert.Equal(after.Json, applied.Json);
	}

	private static void ProcessEngineMessages() {
		while (EngineStorage.HasPendingMessagesToEngine()) {
			EngineStorage.ProcessNextMessageToEngine();
		}
	}

	[Fact]
	public async Task ANewTwoPlayerGame() {
		Measure("2 players, 100x100", await CreateGame(SaveGameFixture.TwoHumanSave()));
	}

	[SkippableFact]
	public async Task ASixteenPlayerGame() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");
		(SaveGame save, Exception ex, _) = await LoadGameAndData(RemoteSaves.Conquests16PlayersJson);
		Assert.Null(ex);
		Measure("16 players", await CreateGame(save));
	}
}
