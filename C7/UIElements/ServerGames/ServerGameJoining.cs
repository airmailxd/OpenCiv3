using System;
using System.Threading;
using C7Engine.Network;
using C7GameServer;
using Godot;
using Serilog;

// Joins a server game for Play or View: asks the game server to connect us
// (which may start a worker and load the game, so it can take a while), joins
// the worker through the relay with our seat's token, and goes into the game
// once the worker sends it, as a LAN guest coming back to its seat does.
//
// It hangs off the scene tree's root rather than the menu, and polls the
// client each frame until the game comes, since nothing else polls it before
// the game scene is up.
public partial class ServerGameJoining : Node {
	private static readonly ILogger log = Log.ForContext<ServerGameJoining>();

	private GameSummary game;
	private readonly CancellationTokenSource cancel = new();
	private AcceptDialog dialog;
	private LanClient client;

	public static void Begin(SceneTree tree, GameSummary game) {
		tree.Root.AddChild(new ServerGameJoining { game = game });
	}

	public override async void _Ready() {
		bool play = ServerGames.YourTurn(game);
		dialog = new AcceptDialog {
			Title = play ? "Play" : "View",
			DialogText = $"Connecting to \"{game.Title}\"...\nThe server may take a little while to load the game.",
			OkButtonText = "Cancel",
			Exclusive = true,
		};
		dialog.Confirmed += Cancel;
		dialog.Canceled += Cancel;
		AddChild(dialog);
		dialog.PopupCentered();

		log.Information("{Action} server game {Id} ({Title})", play ? "Playing" : "Viewing", game.Id, game.Title);
		ServerGameJoin join;
		try {
			join = await ServerGames.Client().JoinGameAsync(game.Id, cancel.Token);
		} catch (OperationCanceledException) when (cancel.IsCancellationRequested) {
			return;
		} catch (Exception e) {
			log.Warning(e, "Couldn't join server game {Id}", game.Id);
			if (e is GameServerException { SessionEnded: true }) {
				GameServerSettings.ForgetSession();
			}
			Fail(e.Message);
			return;
		}
		if (cancel.IsCancellationRequested) {
			join.Client.Dispose();
			return;
		}
		client = join.Client;
		LanSession.BeginJoining(client);
		ServerGames.JoinedClient = client;
		dialog.DialogText = $"Connected to \"{game.Title}\". Waiting for the game...";
	}

	public override void _Process(double delta) {
		if (client == null) {
			return;
		}
		client.Poll();
		if (client.RejectedReason != null) {
			string reason = client.RejectedReason;
			client = null;
			LanSession.End();
			Fail(reason);
			return;
		}
		if (client.StartingGame != null) {
			GlobalSingleton global = GetNode<GlobalSingleton>("/root/GlobalSingleton");
			global.ResetLoadGameFields();
			global.SaveGame = client.StartingGame;
			client = null;
			// Back to Server Games when the player leaves the game.
			ServerGames.OpenOnMenu = true;
			GetTree().ChangeSceneToFile(LanSession.GameScene);
			QueueFree();
		}
	}

	private void Cancel() {
		cancel.Cancel();
		if (client != null) {
			client = null;
			LanSession.End();
		}
		QueueFree();
	}

	// Says why we couldn't join, then goes.
	private void Fail(string reason) {
		dialog.Confirmed -= Cancel;
		dialog.Canceled -= Cancel;
		dialog.Title = "Couldn't connect";
		dialog.DialogText = $"Couldn't connect to \"{game.Title}\": {reason}";
		dialog.OkButtonText = "OK";
		dialog.Confirmed += QueueFree;
		dialog.Canceled += QueueFree;
		if (!dialog.Visible) {
			dialog.PopupCentered();
		}
	}
}
