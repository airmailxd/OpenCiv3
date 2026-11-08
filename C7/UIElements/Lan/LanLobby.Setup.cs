using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.Network;
using C7GameData;
using C7Relay;
using Godot;
using static SetupStyle;

// The lobby as Civ3's Multiplayer Setup screen, in OpenCiv3's look (see
// LanLobby): the game's settings and how to join it on the left; the
// players, the rules and the messages on the right; the chat line along the
// bottom. The host changes what can still be changed here; guests see the
// rest.
public partial class LanLobby {
	private Control setupRoot;
	private Panel setupFrame;

	// GAME SETTINGS: the settings, and for the host how players join.
	private VBoxContainer settingsList;
	private string shownSettings;

	// PLAYER SETUP: the column headings, a row for each player, and the
	// buttons under them.
	private HBoxContainer playerHeadings;
	private VBoxContainer playerRows;
	private HBoxContainer playerButtons;
	private Button startButton;
	private Button joinGameButton;

	// GAME RULES.
	private GridContainer rulesGrid;
	private string shownRules;

	// MESSAGES: the status line, then what has happened.
	private VBoxContainer statusSlot;
	private RichTextLabel messages;

	// Who has come and gone, for the messages.
	private readonly LobbyEvents lobbyEvents = new();

	// Hosting: the game type shown, which goes from LAN to Internet when the
	// game is hosted online.
	private Label gameTypeValue;

	// Hosting: how players join, and who may.
	private HostJoiningSection joiningSection;

	// Column widths in a player row: the slot's number, the team, and what
	// can be done to the player.
	private const float NumberWidth = 26;
	private const float TeamWidth = 92;
	private const float ActionsWidth = 156;

	private void BuildSetupScreen() {
		setupRoot = new Control { Visible = false };
		setupRoot.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(setupRoot);
		setupFrame = MakeFrame();
		setupRoot.AddChild(setupFrame);
		AddTitle(setupFrame, this, "MULTIPLAYER SETUP");

		HBoxContainer body = MakeBody(setupFrame);
		body.AddChild(MakeSettingsPanel());
		VBoxContainer right = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		right.AddThemeConstantOverride("separation", 8);
		body.AddChild(right);
		right.AddChild(MakePlayersPanel());
		right.AddChild(MakeRulesPanel());
		right.AddChild(MakeMessagesPanel());
		AddBottomStrip(setupFrame, BackToMenu);

		GetViewport().SizeChanged += FitSetup;
		FitSetup();
	}

	public override void _ExitTree() {
		GetViewport().SizeChanged -= FitSetup;
	}

	private void FitSetup() => Fit(setupFrame, GetViewportRect().Size);

	private Control MakeSettingsPanel() {
		(PanelContainer panel, VBoxContainer box) = MakePanel(this, "GAME SETTINGS");
		panel.CustomMinimumSize = new Vector2(280, 0);
		ScrollContainer scroll = new() {
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		box.AddChild(scroll);
		settingsList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		settingsList.AddThemeConstantOverride("separation", 2);
		scroll.AddChild(settingsList);
		return panel;
	}

	private Control MakePlayersPanel() {
		(PanelContainer panel, VBoxContainer box) = MakePanel(this, "PLAYER SETUP");
		panel.SizeFlagsVertical = SizeFlags.ExpandFill;
		panel.SizeFlagsStretchRatio = 1.4f;
		playerHeadings = new HBoxContainer();
		box.AddChild(playerHeadings);
		// Room for eight rows, as Civ3's screen has; more scroll.
		ScrollContainer scroll = new() {
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 8 * 27),
		};
		box.AddChild(scroll);
		playerRows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		playerRows.AddThemeConstantOverride("separation", 3);
		scroll.AddChild(playerRows);

		playerButtons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		playerButtons.AddThemeConstantOverride("separation", 24);
		box.AddChild(playerButtons);
		startButton = MakeButton("Start Game", "Start the game once a player has taken each Open slot.", true);
		startButton.Visible = false;
		startButton.Pressed += StartHostedGame;
		playerButtons.AddChild(startButton);
		joinGameButton = MakeButton("Join Game", "Join the game in progress, playing the slots you ticked.", true);
		joinGameButton.Visible = false;
		joinGameButton.Pressed += JoinGameInProgress;
		playerButtons.AddChild(joinGameButton);
		return panel;
	}

	private Control MakeRulesPanel() {
		(PanelContainer panel, VBoxContainer box) = MakePanel(this, "GAME RULES");
		ScrollContainer scroll = new() {
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.ShowAlways,
			CustomMinimumSize = new Vector2(0, 5 * 24),
		};
		box.AddChild(scroll);
		rulesGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		rulesGrid.AddThemeConstantOverride("h_separation", 12);
		rulesGrid.AddThemeConstantOverride("v_separation", 0);
		scroll.AddChild(rulesGrid);
		return panel;
	}

	private Control MakeMessagesPanel() {
		(PanelContainer panel, VBoxContainer box) = MakePanel(this, "MESSAGES");
		panel.SizeFlagsVertical = SizeFlags.ExpandFill;
		statusSlot = new VBoxContainer();
		box.AddChild(statusSlot);
		messages = SetupStyle.MakeMessages();
		box.AddChild(messages);
		return panel;
	}

	// Empties the lobby's panels, on going back to the list of games.
	private void ClearLobbyView() {
		FreeChildren(settingsList);
		FreeChildren(playerRows);
		FreeChildren(rulesGrid);
		shownSettings = null;
		shownRules = null;
		lobbyEvents.Reset();
		messages.Clear();
		startButton.Visible = false;
		joinGameButton.Visible = false;
	}

	private static void FreeChildren(Node node) {
		foreach (Node child in node.GetChildren()) {
			node.RemoveChild(child);
			child.QueueFree();
		}
	}

	// ---- Settings ----

	private void AddSetting(string label, Control value) {
		settingsList.AddChild(MakeSettingLabel(label));
		settingsList.AddChild(value);
	}


	private static Label MakeNote(string text) {
		Label note = new() { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		note.AddThemeFontSizeOverride("font_size", 12);
		return note;
	}

	private static string TurnTimeText(double? seconds) {
		if (seconds is not double s) {
			return "None";
		}
		return s % 60 == 0 ? $"{(int)(s / 60)} Minutes" : $"{(int)s} Seconds";
	}

	// The host's settings: those that can still be changed, then the game's
	// as set up, then how players join and who may.
	private void BuildHostSettings() {
		LanHost host = LanSession.Host;
		LineEdit name = MakeDarkField();
		name.Text = host.PublicName ?? "";
		name.PlaceholderText = host.DefaultPublicName;
		name.MaxLength = RelayProtocol.MaxGameNameLength;
		name.TooltipText = "The game's name, as the lists of games show it.";
		name.TextChanged += text => host.PublicName = text;
		settingsList.AddChild(name);

		gameTypeValue = MakeValue("LAN", "Players on your network join from \"Join LAN Game\"; hosted online, players anywhere join with its code.");
		AddSetting("Game Type:", gameTypeValue);
		AddSetting("Turn Timer:", MakeTurnTimer());
		foreach (LobbySetting setting in host.LobbySettings ?? []) {
			AddSetting(setting.label, MakeValue(setting.value));
		}
		AddTurnRules();
		ShowRules(host.LobbyRules);

		joiningSection = new HostJoiningSection();
		joiningSection.Build(this, hostOnlineButtonShown: true);
		joiningSection.OnlineChanged += online => gameTypeValue.Text = online ? "Internet" : "LAN";
		settingsList.AddChild(joiningSection);
		// A game resumed from one hosted online goes online again, with the
		// same code, so that its guests find it where they left it.
		if (LanSession.ResumeGame is LanResumeInfo { onlineCode: not null } resume) {
			joiningSection.HostOnline(resume.relayUrl, resume.onlineCode, resume.onlineKey);
		} else if (LanSession.DevHostOnline || LanSession.PendingGame?.hostOnline == true) {
			joiningSection.HostOnline();
		}
	}

	// How long each player has for their turn; a resumed game keeps its own.
	private OptionButton MakeTurnTimer() {
		OptionButton choice = MakeDropdown("How long each player has for their turn before it ends for them. "
			+ "With simultaneous moves, how long everyone has for the round.");
		AddItems(choice, TurnTimeChoices.Select(m => m == 0 ? "None" : $"{m} Minutes"), 0);
		TimeSpan? startingLimit = LanSession.Host.TurnTimeLimit;
		if (startingLimit is TimeSpan limit) {
			int index = Array.IndexOf(TurnTimeChoices, (int)limit.TotalMinutes);
			if (index < 0 || limit.TotalMinutes != (int)limit.TotalMinutes) {
				choice.AddItem(TurnTimeText(limit.TotalSeconds));
				index = choice.ItemCount - 1;
			}
			choice.Select(index);
		}
		choice.ItemSelected += index => {
			if (index >= TurnTimeChoices.Length) {
				// The resumed game's own time.
				LanSession.Host.TurnTimeLimit = startingLimit;
				return;
			}
			int minutes = TurnTimeChoices[index];
			LanSession.Host.TurnTimeLimit = minutes == 0 ? null : TimeSpan.FromMinutes(minutes);
		};
		if (LanSession.DevTurnSeconds is int seconds) {
			LanSession.Host.TurnTimeLimit = TimeSpan.FromSeconds(seconds);
			choice.Disabled = true;
			choice.AddItem($"{seconds} Seconds");
			choice.Select(choice.ItemCount - 1);
		}
		return choice;
	}

	// Simultaneous moves, unless a resumed game wasn't or the Multiplayer
	// Setup screen said otherwise; and guests sent only what their players
	// could know, unless a resumed game wasn't.
	private void AddTurnRules() {
		LanHost host = LanSession.Host;
		bool simultaneousTurns = LanSession.ResumeGame?.simultaneousTurns ?? LanSession.PendingGame?.simultaneousTurns ?? true;
		host.SimultaneousTurns = simultaneousTurns;
		Civ3Checkbox simultaneous = MakeRule("Simultaneous Moves", simultaneousTurns,
			"Every human plays their turn at the same time, rather than waiting for each other. The computer's civilizations move after them.");
		simultaneous.Toggled += on => LanSession.Host.SimultaneousTurns = on;
		settingsList.AddChild(simultaneous);

		bool hideUnseen = LanSession.ResumeGame?.hideUnseen ?? true;
		host.HideUnseen = hideUnseen;
		Civ3Checkbox hide = MakeRule("Hide What Players Can't See", hideUnseen,
			"Each guest's computer is sent only what its players could know of the game, so nobody can read "
			+ "other civilizations' hidden units, cities or treasuries from it. Spectators are off unless you allow them, "
			+ "and see only what the civilizations know unless you let them see the whole game.");
		hide.Toggled += on => {
			LanSession.Host.HideUnseen = on;
			joiningSection?.ShowSpectators();
		};
		settingsList.AddChild(hide);
	}

	// A guest's view of the settings, which only the host can change.
	private void ShowGuestSettings(LobbyInfo lobby) {
		LobbyDetails details = lobby.details;
		List<(string label, string value)> shown = [];
		if (details != null) {
			shown.Add(("", details.gameName));
		}
		shown.Add(("Turn Timer:", TurnTimeText(details?.turnSeconds)));
		foreach (LobbySetting setting in details?.settings ?? []) {
			shown.Add((setting.label, setting.value));
		}
		shown.Add(("Simultaneous Moves:", lobby.simultaneousTurns ? "Yes" : "No"));
		shown.Add(("Hide What Players Can't See:", lobby.hideUnseen ? "Yes" : "No"));
		shown.Add(("Password:", details == null ? "Unknown" : details.hasPassword ? "Needed to join" : "None"));
		shown.Add(("Spectators:", lobby.spectatorViews == SpectatorViews.None ? "Not allowed" : "Allowed"));
		string key = string.Join("\n", shown.Select(s => s.label + "\t" + s.value));
		if (key == shownSettings) {
			return;
		}
		shownSettings = key;
		FreeChildren(settingsList);
		foreach ((string label, string value) in shown) {
			if (label == "") {
				LineEdit name = MakeDarkField();
				name.Text = value;
				name.Editable = false;
				name.AddThemeColorOverride("font_uneditable_color", FieldText);
				settingsList.AddChild(name);
			} else {
				AddSetting(label, MakeValue(value));
			}
		}
		Label note = MakeNote($"Only {lobby.hostName}, hosting, can change these.");
		note.AddThemeColorOverride("font_color", Faded);
		settingsList.AddChild(note);
	}

	// ---- Rules ----

	// The game's rules, in two columns as Civ3's: ticked if on. Only the
	// Multiplayer Setup screen changes them, so here they can't be.
	private void ShowRules(List<LobbyRule> rules) {
		string key = rules == null ? "" : string.Join("\n", rules.Select(r => $"{r.name}\t{r.on}"));
		if (key == shownRules) {
			return;
		}
		shownRules = key;
		FreeChildren(rulesGrid);
		if (rules == null || rules.Count == 0) {
			rulesGrid.AddChild(MakeNote("The host's game doesn't say."));
			return;
		}
		foreach (LobbyRule rule in rules) {
			Civ3Checkbox box = MakeRule(rule.name, rule.on, rule.tooltip ?? (rule.on ? "On in this game." : "Off in this game."));
			// Shown as set, not greyed out, but can't be changed here.
			box.MouseFilter = MouseFilterEnum.Pass;
			box.FocusMode = FocusModeEnum.None;
			box.Toggled += _ => box.SetPressedNoSignal(rule.on);
			rulesGrid.AddChild(box);
		}
	}

	// ---- Players ----

	private void ShowHeadings(bool teams) {
		FreeChildren(playerHeadings);
		playerHeadings.AddThemeConstantOverride("separation", 6);
		Label Heading(string text, float width = 0, float ratio = 1) {
			Label label = new() {
				Text = text,
				HorizontalAlignment = HorizontalAlignment.Center,
				CustomMinimumSize = new Vector2(width, 0),
				SizeFlagsHorizontal = width > 0 ? SizeFlags.Fill : SizeFlags.ExpandFill,
				SizeFlagsStretchRatio = ratio,
			};
			label.AddThemeFontSizeOverride("font_size", 13);
			playerHeadings.AddChild(label);
			return label;
		}
		Heading("", NumberWidth);
		Heading("Players", 0, 1.3f);
		Heading("Civilizations");
		if (teams) {
			Heading("Team", TeamWidth);
		}
		Heading("", ActionsWidth);
	}

	// A row of the player list: the slot's number, the player, their
	// civilization, their team if the game has teams, then what can be
	// done to them.
	private void AddPlayerRow(int number, Control player, Control civilization, Control team, bool teams, params Control[] actions) {
		HBoxContainer row = new();
		row.AddThemeConstantOverride("separation", 6);
		Label numberCell = MakeValue(number > 0 ? number.ToString() : "");
		numberCell.CustomMinimumSize = new Vector2(NumberWidth, 22);
		numberCell.SizeFlagsHorizontal = SizeFlags.Fill;
		row.AddChild(numberCell);
		player.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		player.SizeFlagsStretchRatio = 1.3f;
		row.AddChild(player);
		civilization.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(civilization);
		if (teams) {
			Control cell = team ?? new Control();
			cell.CustomMinimumSize = new Vector2(TeamWidth, 22);
			cell.SizeFlagsHorizontal = SizeFlags.Fill;
			row.AddChild(cell);
		}
		HBoxContainer actionCell = new() { CustomMinimumSize = new Vector2(ActionsWidth, 0), Alignment = BoxContainer.AlignmentMode.End };
		actionCell.AddThemeConstantOverride("separation", 4);
		foreach (Control action in actions) {
			if (action != null) {
				actionCell.AddChild(action);
			}
		}
		row.AddChild(actionCell);
		playerRows.AddChild(row);
	}

	// The player in a seat, as everyone sees them: who has taken it, under
	// the name they play as if that's another.
	private static string Occupant(SeatInfo seat) {
		if (seat.takenBy == null) {
			return seat.playerName == null ? "Open" : $"Open ({seat.playerName})";
		}
		string who = seat.playerName != null && seat.playerName != seat.takenBy ? $"{seat.takenBy} as {seat.playerName}" : seat.takenBy;
		if (seat.disconnected) {
			who += " (disconnected)";
		}
		if (seat.away) {
			who += ", away";
		}
		return who;
	}

	private static string CivilizationText(SeatInfo seat) => seat.civilization ?? "Random";

	private static string TeamText(int team) => team > 0 ? GameSetup.TeamName(team) : "";

	// A picker for one of the teams numbered 1 to teams, on team at first.
	private static OptionButton MakeTeamPicker(int teams, int team, Action<int> chosen) {
		OptionButton picker = MakeDropdown("Teammates are allies for good, and win together.");
		for (int t = 1; t <= teams; ++t) {
			picker.AddItem(GameSetup.TeamName(t), t);
		}
		picker.Select(Math.Clamp(team, 1, teams) - 1);
		picker.ItemSelected += index => chosen(picker.GetItemId((int)index));
		return picker;
	}

	// Tells the messages who has come and gone since the players were last
	// shown.
	private void NoteChanges(IEnumerable<SeatInfo> seats, IEnumerable<string> spectators) {
		lobbyEvents.Note(seats, spectators, text => AddMessage(messages, text));
	}

	// The host's view of the players: their own row, where they can rename
	// themselves; each seat, with its guest to remove or ban; then those
	// watching.
	private void ShowHostSeats() {
		LanHost host = LanSession.Host;
		NoteChanges(host.Seats, host.Spectators);
		bool teams = host.TeamCount > 0;
		ShowHeadings(teams);
		FreeChildren(playerRows);

		LineEdit hostName = MakeDarkField();
		hostName.Text = host.HostName;
		hostName.MaxLength = LanHost.MaxPlayerNameLength;
		hostName.TooltipText = "You, hosting: what the other players see you as. Press Enter to change it.";
		void Rename() {
			if (LanSession.Host == null || string.IsNullOrWhiteSpace(hostName.Text) || hostName.Text == LanSession.Host.HostName) {
				return;
			}
			LanSession.Host.HostName = hostName.Text;
			LanSession.PlayerName = LanSession.Host.HostName;
		}
		hostName.TextSubmitted += _ => Rename();
		hostName.FocusExited += Rename;
		Control hostTeam = teams && !creatingGame ? MakeTeamPicker(host.TeamCount, host.HostTeam, team => host.HostTeam = team)
			: teams ? MakeValue(TeamText(host.HostTeam)) : null;
		AddPlayerRow(1, hostName, MakeValue(host.HostCivilization ?? "Random"), hostTeam, teams, MakeNote("Hosting"));

		int number = 2;
		foreach (SeatInfo seat in host.Seats) {
			// A seat held for a guest who hasn't come back can be given to
			// anyone.
			Button release = seat.disconnected
				? MakeSmallButton("Release", "Give this slot to whoever takes it next.", () => host.ReleaseSeat(seat.playerID)) : null;
			// A guest can be removed, which frees all their seats, or banned.
			Button remove = null, ban = null;
			if (seat.takenBy != null) {
				remove = MakeSmallButton("Remove", $"Remove {seat.takenBy} from the game, freeing their slots for anyone to take.",
					() => HostActions.Remove(seat.playerID));
				ban = MakeSmallButton("Ban", $"Remove {seat.takenBy} and keep them out of this game.",
					() => HostActions.ConfirmBan(this, seat.takenBy, seat.playerID));
			}
			Label player = MakeValue(Occupant(seat));
			if (seat.takenBy == null) {
				player.AddThemeColorOverride("font_color", Faded);
			}
			AddPlayerRow(number++, player, MakeValue(CivilizationText(seat)), teams ? MakeValue(TeamText(seat.team)) : null, teams,
				release, remove, ban);
		}
		foreach (SpectatorInfo spectator in host.SpectatorList) {
			AddPlayerRow(0, MakeValue(spectator.name), MakeValue("Watching"), null, teams,
				MakeSmallButton("Remove", $"Stop {spectator.name} watching.", () => host.KickSpectator(spectator.id)),
				MakeSmallButton("Ban", $"Stop {spectator.name} watching, and keep them out of this game.",
					() => HostActions.ConfirmBanSpectator(this, spectator.name, spectator.id)));
		}

		startButton.Visible = true;
		ShowHostStatus();
	}

	// A guest's view of the lobby: the settings and rules, and the players,
	// with their own slots to name and choose a civilization for.
	private void ShowGuestLobby(LanClient client, LobbyInfo lobby) {
		ShowGuestSettings(lobby);
		ShowRules(lobby.details?.rules);
		NoteChanges(lobby.seats, lobby.spectators ?? []);
		if (messages.GetParsedText().Length == 0) {
			AddMessage(messages, $"In {lobby.hostName}'s game.");
		}

		IReadOnlyList<ID> mine = client.YourSeats;
		bool canChoose = lobby.civilizations != null && mine.Count > 0 && !client.IsSpectator;
		bool teams = lobby.teams > 0 || lobby.seats.Any(s => s.team > 0);
		ShowHeadings(teams);
		FreeChildren(playerRows);
		rejoinSeats.RemoveWhere(id => !lobby.seats.Any(s => s.playerID == id && s.takenBy == null));

		int number = 1;
		foreach (SeatInfo seat in lobby.seats) {
			if (seat.isHost) {
				AddPlayerRow(number++, MakeValue(seat.playerName), MakeValue(CivilizationText(seat)),
					teams ? MakeValue(TeamText(seat.team)) : null, teams, MakeNote("Hosting"));
				continue;
			}
			bool isMine = mine.Contains(seat.playerID);
			bool open = seat.takenBy == null && !client.IsSpectator && !lobby.creatingGame;
			List<Control> actions = [];
			if (open && !lobby.started) {
				actions.Add(MakeSmallButton("Take Slot", "Play this civilization.",
					() => client.ClaimSeat(seat.playerID, NameForAnotherSeat(lobby, mine))));
			} else if (open) {
				// In a game in progress, taking a seat takes us into it, so
				// every seat to take is ticked first.
				Civ3Checkbox tick = MakeRule("Take", rejoinSeats.Contains(seat.playerID), "Tick each slot to play, then Join Game.");
				tick.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
				tick.Toggled += on => {
					if (on) {
						rejoinSeats.Add(seat.playerID);
					} else {
						rejoinSeats.Remove(seat.playerID);
					}
					joinGameButton.Disabled = rejoinSeats.Count == 0;
				};
				actions.Add(tick);
			}

			if (isMine && !lobby.started && !lobby.creatingGame) {
				actions.Add(MakeSmallButton("Leave Slot", "Give up this slot for someone else to take.", () => client.LeaveSeat(seat.playerID)));
				Control civ = canChoose ? MakeCivilizationPicker(client, lobby, seat) : MakeValue(CivilizationText(seat));
				Control team = lobby.teams > 0 ? MakeTeamPicker(lobby.teams, seat.team, t => client.ChooseTeam(t, seat.playerID))
					: teams ? MakeValue(TeamText(seat.team)) : null;
				AddPlayerRow(number++, MakeSeatNameEdit(client, seat), civ, team, teams, [.. actions]);
				continue;
			}
			Label player = MakeValue(Occupant(seat) + (isMine ? " (you)" : ""));
			if (seat.takenBy == null) {
				player.AddThemeColorOverride("font_color", Faded);
			}
			AddPlayerRow(number++, player, MakeValue(CivilizationText(seat)), teams ? MakeValue(TeamText(seat.team)) : null, teams, [.. actions]);
		}
		foreach (ID gone in seatNameDrafts.Keys.Where(id => !mine.Contains(id)).ToList()) {
			seatNameDrafts.Remove(gone);
		}
		foreach (string spectator in lobby.spectators ?? []) {
			AddPlayerRow(0, MakeValue(spectator), MakeValue("Watching"), null, teams);
		}

		startButton.Visible = false;
		joinGameButton.Visible = lobby.started && !client.IsSpectator && lobby.seats.Any(s => !s.isHost && s.takenBy == null);
		joinGameButton.Disabled = rejoinSeats.Count == 0;
		status.Text = GuestStatus(client, lobby, canChoose);
	}

	// Takes the ticked slots in a game in progress, which joins it.
	private void JoinGameInProgress() {
		LanClient client = LanSession.Client;
		LobbyInfo lobby = client?.Lobby;
		if (lobby == null) {
			return;
		}
		IReadOnlyList<ID> mine = client.YourSeats;
		foreach (SeatInfo seat in lobby.seats.Where(s => rejoinSeats.Contains(s.playerID))) {
			client.ClaimSeat(seat.playerID, seat.playerName ?? NameForAnotherSeat(lobby, mine));
		}
		rejoinSeats.Clear();
	}

	// The civilizations a guest can choose for one of their slots, as the
	// Multiplayer Setup screen's lists: those other players have are greyed
	// out, and each says who leads it.
	private static OptionButton MakeCivilizationPicker(LanClient client, LobbyInfo lobby, SeatInfo seat) {
		OptionButton picker = MakeDropdown("Your civilization; Random gives you one nobody else has.");
		List<CivilizationChoice> choices = lobby.civilizations;
		AddItems(picker, choices.Select(c => c.name).Append("Random"), choices.Count);
		HashSet<string> takenByOthers = lobby.seats
			.Where(s => s.playerID != seat.playerID && s.civilization != null)
			.Select(s => s.civilization)
			.ToHashSet();
		for (int i = 0; i < choices.Count; ++i) {
			CivilizationChoice choice = choices[i];
			bool taken = takenByOthers.Contains(choice.name);
			picker.SetItemDisabled(i, taken || lobby.creatingGame);
			string traits = choice.traits?.Count > 0 ? $" ({string.Join(", ", choice.traits)})" : "";
			picker.SetItemTooltip(i, taken ? "Another player has this civilization." : $"{choice.leader} of the {choice.noun}{traits}");
			if (choice.name == seat.civilization) {
				picker.Select(i);
			}
		}
		picker.SetItemTooltip(choices.Count, "A random civilization nobody else has.");
		picker.Disabled = lobby.creatingGame;
		picker.ItemSelected += index => client.ChooseCivilization(index < choices.Count ? choices[(int)index].name : null, seat.playerID);
		return picker;
	}

	// A box for the name of the player in one of our seats, sent to the host
	// on Enter or on leaving the box.
	private LineEdit MakeSeatNameEdit(LanClient client, SeatInfo seat) {
		ID id = seat.playerID;
		LineEdit edit = MakeDarkField();
		edit.Text = seatNameDrafts.TryGetValue(id, out string draft) ? draft : seat.takenBy;
		edit.PlaceholderText = "Player's name";
		edit.MaxLength = 40;
		edit.TooltipText = "Your name in this slot. To play several people at this computer, taking turns, take a slot for each "
			+ "and give each player's name.";
		edit.TextChanged += text => seatNameDrafts[id] = text;
		void Send() {
			if (seatNameDrafts.Remove(id, out string name) && !string.IsNullOrWhiteSpace(name) && name.Trim() != seat.takenBy) {
				client.ClaimSeat(id, name.Trim());
			}
		}
		edit.TextSubmitted += _ => {
			Send();
			edit.ReleaseFocus();
		};
		edit.FocusEntered += () => editingSeatName = id;
		edit.FocusExited += () => {
			// Redrawing the seats frees the box; the one replacing it takes
			// over the typing.
			if (edit.IsQueuedForDeletion()) {
				return;
			}
			editingSeatName = null;
			Send();
		};
		if (editingSeatName == id) {
			Callable.From(() => {
				if (IsInstanceValid(edit) && edit.IsInsideTree()) {
					edit.GrabFocus();
					edit.CaretColumn = edit.Text.Length;
				}
			}).CallDeferred();
		}
		return edit;
	}
}
