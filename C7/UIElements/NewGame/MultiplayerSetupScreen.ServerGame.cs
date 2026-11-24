using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.Network;
using C7GameData.Save;
using C7GameServer;
using Godot;
using static SetupStyle;

// The Multiplayer Setup screen setting up a server game (see
// doc/server-games.md), opened from the Server Games screen's New Server
// Game. The game is set up as a hotseat game is, each Open slot another
// human player; Launch makes the world, then asks for the game's title, its
// password if any and which of the humans the host plays, and puts it on the
// game server, where the other players claim their seats from Open Games.
public partial class MultiplayerSetupScreen {
	// Set by the Server Games screen before opening this one.
	public static bool ServerGame;

	private bool serverGame;

	private const string ServerGameHelp = "Each Open slot is the seat of another human player, who claims it from Open Games on the "
		+ "game server. Choose each seat's civilization here; Rename gives it a name. Launch makes the world and puts the game on the server.";

	private void SetUpForServerGame() {
		gameType.Select(Array.FindIndex(GameTypes, t => t.type == MultiplayerGameType.Hotseat));
		gameType.Disabled = true;
		gameType.TooltipText = "A server game: each player plays on their own computer, through the game server.";
		// The name is the game's title on the server.
		gameNameEdit.MaxLength = GameServerProtocol.MaxTitleLength;
		gameNameEdit.TooltipText = "The game's title, as the players see it in My Games and Open Games.";
		launchButton.TooltipText = "Make the world and put the game on the game server.";
	}

	// The world is made: the host gives the game's title and password and
	// chooses whom they play, then it's put on the server.
	private void ServerGameCreated(SaveGame created, byte[] saveBytes) {
		if (!IsInstanceValid(this) || !IsInsideTree()) {
			return;
		}
		List<SavePlayer> humans = created.Players.Where(p => p.human && p.isIncludedInGame).ToList();

		AcceptDialog dialog = new() {
			Title = "Server Game",
			OkButtonText = "Put on Server",
			DialogHideOnOk = false,
			Exclusive = true,
		};
		Button cancel = dialog.AddCancelButton("Cancel");
		VBoxContainer box = new() { CustomMinimumSize = new Vector2(420, 0) };
		box.AddThemeConstantOverride("separation", 6);
		dialog.AddChild(box);

		box.AddChild(new Label { Text = "Title:" });
		LineEdit title = new() { Text = gameNameEdit.Text.Trim(), MaxLength = GameServerProtocol.MaxTitleLength };
		box.AddChild(title);
		box.AddChild(new Label { Text = "Password (leave it blank for anyone to claim a seat):" });
		LineEdit password = new() { Secret = true, MaxLength = GameServerProtocol.MaxPasswordLength };
		box.AddChild(password);
		box.AddChild(new Label { Text = "You play:" });
		OptionButton you = new();
		foreach (SavePlayer human in humans) {
			string name = string.IsNullOrEmpty(human.name) ? "" : $" ({human.name})";
			you.AddItem($"{human.civilization}{name}");
		}
		// The host's own row on this screen, unless renamed to match another.
		you.Select(Math.Max(0, humans.FindIndex(p => p.name == HostName)));
		box.AddChild(you);
		Label problem = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(420, 0) };
		problem.AddThemeColorOverride("font_color", ProblemColor);
		box.AddChild(problem);
		dialog.RegisterTextEnter(title);
		dialog.RegisterTextEnter(password);

		bool uploading = false;
		bool done = false;
		dialog.Confirmed += async () => {
			if (uploading) {
				return;
			}
			string tidyTitle = GameServerProtocol.TidyTitle(title.Text);
			if (tidyTitle == null) {
				problem.Text = $"A game's title has 1 to {GameServerProtocol.MaxTitleLength} characters.";
				title.GrabFocus();
				return;
			}
			if (you.Selected < 0 || you.Selected >= humans.Count) {
				problem.Text = "Choose the player you play.";
				return;
			}
			uploading = true;
			dialog.GetOkButton().Disabled = true;
			// Once sent, the game may be made whatever happens here.
			cancel.Disabled = true;
			problem.Text = "";
			AddMessage("Putting the game on the server...");
			try {
				GameServerClient client = ServerGames.Client();
				GameSummary game = await client.CreateGameAsync(tidyTitle, password.Text == "" ? null : password.Text,
					humans[you.Selected].id.ToString(), saveBytes);
				log.Information("Put server game {Id} ({Title}) on the server", game?.Id, tidyTitle);
				done = true;
				if (IsInstanceValid(this) && IsInsideTree()) {
					dialog.Hide();
					BackToMenu();
				}
			} catch (GameServerException e) {
				log.Warning("Couldn't put the game on the server: {Message}", e.Message);
				if (!IsInstanceValid(problem)) {
					return;
				}
				problem.Text = e.SessionEnded
					? "Your session on the game server has ended. Cancel, log in again from Server Games, and set the game up again."
					: e.Message;
			} finally {
				uploading = false;
				if (IsInstanceValid(dialog)) {
					dialog.GetOkButton().Disabled = false;
					cancel.Disabled = false;
				}
			}
		};
		dialog.VisibilityChanged += () => {
			if (dialog.Visible) {
				return;
			}
			dialog.QueueFree();
			if (!done && IsInstanceValid(this) && IsInsideTree()) {
				// Not put on the server: the world is let go of, and the
				// rules it changed are loaded fresh, for another try.
				SetLaunching(false);
				LoadRules(loadedRulesetDir != null ? scenario : GameScenario.None);
				AddMessage("The game wasn't put on the server.");
			}
		};
		AddChild(dialog);
		dialog.PopupCentered();
		title.CallDeferred(Control.MethodName.GrabFocus);
	}
}
