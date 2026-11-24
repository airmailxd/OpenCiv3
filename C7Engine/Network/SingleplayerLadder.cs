using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using C7GameData;
using C7GameData.Save;
using C7GameServer;
using Serilog;

namespace C7Engine.Network;

// Finished singleplayer games, sent to the game server for its singleplayer
// ladder (GameServerProtocol.SingleplayerResultsPath): a game played to its
// end by the one human player on this computer, in the Standard game type,
// when the player has a session on the game server. Each is written to the
// pending folder (LadderQueue) first and sent from there, so a game the
// server couldn't be reached for is sent the next time the game starts.
public static class SingleplayerLadder {
	private static readonly ILogger log = Log.ForContext(typeof(SingleplayerLadder));

	// The only game type the ladder ranks (see LanGameList.GameTypeOf).
	public const string GameType = "Standard";

	// What became of a game sent.
	public enum Outcome {
		// The server ranked it: it's done with.
		Ranked,
		// The server won't rank it, and never will: it's dropped.
		Refused,
		// The server couldn't be reached, failed, or wants the player to
		// log in again: it's kept, to send again.
		TryAgain,
	}

	// Whether a game that has just ended is one for the ladder: over (a
	// game retired from isn't), with one human player and no one else's
	// moves played here, by email or otherwise, and not in debug mode.
	// Networked is whether it's a LAN or server game.
	public static bool IsLadderGame(GameData gameData, bool networked) {
		return gameData != null && gameData.gameOver && !networked && !gameData.observerMode && !gameData.debugMode
			&& !PlayByEmail.IsOn(gameData) && gameData.players.Count(p => p.isHuman) == 1;
	}

	// The victory the game was won by, as the ladder names it (its
	// ?victory=), if the human player or their team won it; null if they
	// lost, or for a victory the ladder has no name for.
	public static string VictoryName(GameData gameData, Player winner, IVictory victory) {
		Player human = gameData?.players.FirstOrDefault(p => p.isHuman);
		if (human == null || winner == null || (winner != human && !gameData.AreTeammates(human, winner))) {
			return null;
		}
		return VictoryName(victory);
	}

	public static string VictoryName(IVictory victory) {
		return victory switch {
			ConquestVictory => "Conquest",
			CulturalVictory => "Cultural",
			DiplomaticVictory => "Diplomatic",
			DominationVictory => "Domination",
			EmpireVictory => "Empire",
			RegicideCondition => "Regicide",
			EliminationCondition => "Elimination",
			RisingTideVictory => "RisingTide",
			ScoreVictory => "Score",
			SpaceRaceVictory => "SpaceRace",
			TimeLimitVictory => "TimeLimit",
			VictoryPointVictory => "VictoryPoints",
			WonderVictory => "Wonder",
			_ => null,
		};
	}

	// The save as sent: gzipped, which the server reads as it reads the
	// save itself; a save shrinks to a twentieth or so.
	public static byte[] Gzip(byte[] save) {
		using MemoryStream packed = new();
		using (GZipStream gzip = new(packed, CompressionLevel.Optimal, leaveOpen: true)) {
			gzip.Write(save);
		}
		return packed.ToArray();
	}

	// Whether the ladder ranks games of the save's type.
	public static bool IsLadderType(SaveGame save) {
		return LanGameList.GameTypeOf(save) == GameType;
	}

	// The game's save as it stands, safe to write on another thread while
	// the game goes on (as SaveManager.SaveInBackground takes it).
	public static SaveGame Snapshot(GameData gameData) {
		SaveGame save = SaveGame.FromGameData(gameData);
		SnapshotDetacher.Detach(save);
		return save;
	}

	// What to do with a game the server answered with this status, or that
	// got no answer at all (null).
	public static Outcome OutcomeOf(HttpStatusCode? status) {
		if (status is not HttpStatusCode code) {
			return Outcome.TryAgain;
		}
		int n = (int)code;
		if (n >= 200 && n < 300) {
			return Outcome.Ranked;
		}
		// A session ended can be logged in again; a timeout or too many
		// tries is the server busy, not the game's fault.
		if (n >= 400 && n < 500 && code is not (HttpStatusCode.Unauthorized or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests)) {
			return Outcome.Refused;
		}
		return Outcome.TryAgain;
	}

	// Sends the games waiting in the queue, oldest first, with submit (as
	// GameServerClient.SubmitSingleplayerResultAsync): each save gzipped,
	// with the victory it was won by, if any. A game ranked or
	// refused leaves the queue; once one is to be tried again, so are the
	// rest, later. Returns what the server said of each game ranked, by its
	// file. Never throws.
	public static async Task<Dictionary<string, LadderResult>> SendPendingAsync(LadderQueue queue,
		Func<byte[], string, Task<LadderResult>> submit) {
		Dictionary<string, LadderResult> ranked = [];
		List<string> pending;
		try {
			pending = queue.Pending();
		} catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
			log.Warning(e, "Couldn't list the games waiting for the singleplayer ladder");
			return ranked;
		}
		foreach (string path in pending) {
			byte[] save;
			try {
				save = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
			} catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
				log.Warning(e, "Couldn't read {Path} for the singleplayer ladder", Path.GetFileName(path));
				continue;
			}
			HttpStatusCode? status;
			try {
				LadderResult result = await submit(Gzip(save), LadderQueue.VictoryOf(path)).ConfigureAwait(false);
				ranked[path] = result;
				log.Information("The singleplayer ladder ranked {Path}: score {Score}, rank {Rank}{Best}", Path.GetFileName(path),
					result?.Score, result?.Rank, result?.Best == true ? ", the player's best" : "");
				queue.Drop(path);
				continue;
			} catch (GameServerException e) {
				status = e.Status;
				log.Information("The singleplayer ladder didn't take {Path} ({Status}): {Error}", Path.GetFileName(path),
					status is HttpStatusCode s ? (int)s : (object)"no answer", e.Message);
			} catch (Exception e) {
				status = null;
				log.Warning(e, "Couldn't send {Path} to the singleplayer ladder", Path.GetFileName(path));
			}
			if (OutcomeOf(status) == Outcome.Refused) {
				queue.Drop(path);
				continue;
			}
			// Not now: the rest wait too, rather than each failing in turn.
			break;
		}
		return ranked;
	}
}

// The finished games waiting to be sent to the singleplayer ladder, each a
// save in the folder, named for when it was added, so the oldest sorts
// first. At most Max are kept, the oldest dropped to make room.
public sealed class LadderQueue(string folder, int max = LadderQueue.DefaultMax) {
	public const int DefaultMax = 5;

	private const string Extension = ".json";
	private const string Writing = ".part";

	public string Folder { get; } = folder;
	public int Max { get; } = Math.Max(1, max);

	// The saves waiting, oldest first.
	public List<string> Pending() {
		if (!Directory.Exists(Folder)) {
			return [];
		}
		return Directory.GetFiles(Folder, "*" + Extension).Order(StringComparer.Ordinal).ToList();
	}

	// Between a save's name and the victory it was won by.
	private const char VictoryMark = '~';

	// Writes the save into the queue, as the game writes it to disk, with
	// the victory it was won by (a VictoryName, or null), and returns its
	// file. Only a save written whole is ever in the queue.
	public string Add(SaveGame save, DateTime now, string victory = null) {
		Directory.CreateDirectory(Folder);
		string name = $"{now.ToUniversalTime():yyyyMMdd-HHmmss-fff}-{Guid.NewGuid().ToString("N")[..8]}";
		if (!string.IsNullOrEmpty(victory) && victory.All(char.IsAsciiLetter)) {
			name += VictoryMark + victory;
		}
		string path = Path.Combine(Folder, name + Extension);
		string part = path + Writing;
		save.Save(part);
		File.Move(part, path);
		Trim();
		return path;
	}

	// The victory the queued save was won by, or null.
	public static string VictoryOf(string path) {
		string name = Path.GetFileNameWithoutExtension(path);
		int mark = name.IndexOf(VictoryMark);
		return mark < 0 ? null : name[(mark + 1)..];
	}

	// Drops the oldest until at most Max are left.
	public void Trim() {
		List<string> pending = Pending();
		foreach (string path in pending.Take(Math.Max(0, pending.Count - Max))) {
			Drop(path);
		}
	}

	public void Drop(string path) {
		try {
			File.Delete(path);
		} catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
			Log.ForContext<LadderQueue>().Warning(e, "Couldn't drop {Path} from the singleplayer ladder's queue", Path.GetFileName(path));
		}
	}
}
