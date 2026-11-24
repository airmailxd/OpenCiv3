using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.Network;
using C7GameData;
using C7GameData.Save;
using C7GameServer;
using Godot;
using Serilog;

// Sends a singleplayer game played to its end to the game server's
// singleplayer ladder (see SingleplayerLadder), in the background, when the
// player has a session on the game server: the one handed over for this run
// (ServerHandoff) or the one they logged in with on the Server Games screen.
// The ladder's answer is shown as a line in the corner for a while; nothing
// is shown if it can't be sent, and it's tried again the next time the game
// starts.
public static class LadderSubmit {
	private static readonly ILogger log = Log.ForContext(typeof(LadderSubmit));

	// Kept with the rest of the player's own data, beside the Hall of Fame.
	private const string FolderPath = "user://ladder-pending";

	// Above the end of the game's screens (EndOfGame), below the LAN
	// reconnecting curtain.
	private const int Layer = 106;

	private static bool sending = false;
	private static bool startTried = false;
	// The game last sent, so a game that ends twice (won, played on, then
	// lost) is sent once.
	private static WeakReference<GameData> lastSent;

	private static LadderQueue Queue() => new(ProjectSettings.GlobalizePath(FolderPath));

	// The session to send with, or null.
	private static GameServerClient Client() {
		string url = GameServerSettings.Url;
		Session session = GameServerSettings.SavedSession(url);
		if (session == null || GameServerSettings.Problem(url) != null) {
			return null;
		}
		try {
			return new GameServerClient(GameServerSettings.Tidy(url), session);
		} catch (GameServerException) {
			return null;
		}
	}

	// The game has just ended (won by winner with victory, or with every
	// human defeated, when both are null): if it's one for the ladder, it's
	// sent, and the answer shown over host.
	public static void GameEnded(Node host, Player winner = null, IVictory victory = null) {
		try {
			if (LanSession.IsActive || ServerGames.InServerGame) {
				return;
			}
			GameServerClient client = Client();
			if (client == null) {
				return;
			}
			SaveGame save = null;
			string victoryName = null;
			EngineStorage.ReadGameData((GameData gameData) => {
				if (lastSent != null && lastSent.TryGetTarget(out GameData sent) && sent == gameData) {
					return;
				}
				if (SingleplayerLadder.IsLadderGame(gameData, networked: false)) {
					lastSent = new WeakReference<GameData>(gameData);
					save = SingleplayerLadder.Snapshot(gameData);
					victoryName = SingleplayerLadder.VictoryName(gameData, winner, victory);
				}
			});
			if (save == null || !SingleplayerLadder.IsLadderType(save)) {
				return;
			}
			Send(host, client, save, victoryName);
		} catch (Exception e) {
			log.Warning(e, "Couldn't send the game to the singleplayer ladder");
		}
	}

	// Sends the games still waiting from earlier runs, the first time the
	// main menu shows, if there's a session to send them with.
	public static void SendPendingOnStart() {
		if (startTried) {
			return;
		}
		startTried = true;
		try {
			if (Queue().Pending().Count == 0 || Client() is not GameServerClient client) {
				return;
			}
			Send(null, client, null, null);
		} catch (Exception e) {
			log.Warning(e, "Couldn't send the games waiting for the singleplayer ladder");
		}
	}

	// Writes the save into the queue (if there is one), then sends the queue,
	// all on other threads; resumes here, on the main thread, to show the
	// save's answer over host.
	private static async void Send(Node host, GameServerClient client, SaveGame save, string victory) {
		LadderQueue queue = Queue();
		string path = null;
		try {
			if (save != null) {
				path = await Task.Run(() => queue.Add(save, DateTime.UtcNow, victory));
			}
			if (sending) {
				// What's being sent now goes on; this waits for the next start.
				return;
			}
			sending = true;
			Dictionary<string, LadderResult> ranked;
			try {
				ranked = await Task.Run(() => SingleplayerLadder.SendPendingAsync(queue, (bytes, won) => client.SubmitSingleplayerResultAsync(bytes, won)));
			} finally {
				sending = false;
			}
			if (path != null && ranked.TryGetValue(path, out LadderResult result) && result != null) {
				Show(host, result);
			}
		} catch (Exception e) {
			log.Warning(e, "Couldn't send the game to the singleplayer ladder");
		}
	}

	// "Ladder: 1,834, rank 12" in the bottom left corner, for a while.
	private static void Show(Node host, LadderResult result) {
		if (host == null || !GodotObject.IsInstanceValid(host) || !host.IsInsideTree()) {
			return;
		}
		string text = string.Format(CultureInfo.InvariantCulture, "Ladder: {0:N0}, rank {1:N0}{2}", result.Score, result.Rank,
			result.Best ? ", your best" : "");
		CanvasLayer layer = new() { Layer = Layer };
		Label label = new() {
			Text = text,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		label.AddThemeFontSizeOverride("font_size", 16);
		label.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.9f));
		label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f));
		label.AddThemeConstantOverride("shadow_offset_x", 1);
		label.AddThemeConstantOverride("shadow_offset_y", 1);
		// Pinned to the corner, growing up and right to fit the text.
		label.AnchorTop = 1;
		label.AnchorBottom = 1;
		label.OffsetLeft = 16;
		label.OffsetRight = 16;
		label.OffsetTop = -12;
		label.OffsetBottom = -12;
		label.GrowVertical = Control.GrowDirection.Begin;
		label.GrowHorizontal = Control.GrowDirection.End;
		layer.AddChild(label);
		host.AddChild(layer);
		Tween tween = label.CreateTween();
		tween.TweenInterval(20);
		tween.TweenProperty(label, "modulate:a", 0.0, 2);
		tween.TweenCallback(Callable.From(layer.QueueFree));
	}
}
