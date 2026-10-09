using Godot;
using System;
using System.Diagnostics;
using C7Engine;
using C7GameData;
using Serilog;
using C7Engine.Pathing;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using static C7GameData.MapUnit;
using C7Engine.Network;

public class GotoInfo {
	public Tile destinationTile = null;
	public int moveCost = -1;
	public TilePath path = null;
	public HashSet<System.Numerics.Vector2> pathCoords;
	public bool attackingMove = false;
	public Player requiresWarDeclarationOnPlayer = null;
	public Intent intent = Intent.Disabled;
	// Whether every unit on the selected unit's tile goes, not just the
	// selected one (the J key).
	public bool wholeStack = false;
	// The unit that moves, once a war declaration has to be confirmed first,
	// so the move doesn't go to whichever unit is selected by then.
	public ID unitID = null;
};

public class TileInfo {
	public Tile targetTile;
	public HashSet<Tile> coveredTiles = [];
	public HashSet<Tile> cityLabelsToHide = [];

	public TileInfo(Tile tile) {
		targetTile = tile;

		List<TileDirection> coverage = [TileDirection.SOUTHWEST, TileDirection.SOUTH, TileDirection.SOUTHEAST];
		foreach (var dir in coverage)
			if (TryNeighbor(tile, dir, out var neighbor))
				coveredTiles.Add(neighbor);

		List<TileDirection> labelCoverage = [TileDirection.NORTHWEST, TileDirection.NORTH, TileDirection.NORTHEAST];
		foreach (var dir in labelCoverage)
			if (TryNeighbor(tile, dir, out var neighbor))
				cityLabelsToHide.Add(neighbor);
	}

	private bool TryNeighbor(Tile tile, TileDirection dir, out Tile neighbor) {
		neighbor = tile.neighbors[dir];
		return neighbor != Tile.NONE;
	}

	public bool IsCovered(Tile tile) => tile == targetTile || coveredTiles.Contains(tile);

	public bool HasCityLabelToCover(Tile tile) => cityLabelsToHide.Contains(tile);
};

public class BombardInfo {
	public MapUnit bombardingUnit;
	public Tile mouseTile;
	public MapUnit.BombardTarget bombardTarget = MapUnit.BombardTarget.None;

	public BombardInfo(MapUnit bombardingUnit) {
		this.bombardingUnit = bombardingUnit;
	}

	public bool RequiresWarDeclaration(Tile tile, out Player player) {
		player = null;

		var bombarder = bombardingUnit.owner;

		if (bombardTarget == MapUnit.BombardTarget.None)
			return false;

		if (bombardTarget == MapUnit.BombardTarget.City) {
			player = tile.owningCity.owner;
			return bombarder.IsAtPeaceWith(player);
		}

		if (bombardTarget == MapUnit.BombardTarget.Unit) {
			player = tile.unitsOnTile.First().owner;
			return bombarder.IsAtPeaceWith(player);
		}

		if (bombardTarget == MapUnit.BombardTarget.Improvement) {
			player = tile.OwningPlayer();
			return player != null && player != bombarder && bombarder.IsAtPeaceWith(player);
		}

		throw new NotImplementedException($"A case is missing here. {tile} | {player} | {bombardTarget}");
	}
};

public partial class Game : Node {
	private ILogger log = LogManager.ForContext<Game>();

	[Signal] public delegate void TurnEndedEventHandler();
	[Signal] public delegate void ShowSpecificAdvisorEventHandler();
	[Signal] public delegate void ShowGameViewEventHandler();
	[Signal] public delegate void ShowCityScreenEventHandler();

	[Signal] public delegate void PlayerTurnStartEventHandler();
	[Signal] public delegate void PlayerTurnEndEventHandler();
	[Signal] public delegate void GameInitializedEventHandler();

	[Signal] public delegate void UnitMovedEventHandler();

	[Export]
	Control Toolbar;
	private bool IsMovingCamera;
	private Vector2 OldPosition;

	[Export]
	private PopupOverlay popupOverlay;
	[Export]
	private CityScreen cityScreen;
	[Export]
	private Advisors advisor;
	[Export]
	private GameViews gameViews;
	[Export]
	private Diplomacy diplomacy;
	[Export]
	private StatusMenu statusMenu;

	[Export]
	private DoubleClickHandler doubleClickHandler;
	[Export]
	public AnimationController animationController;
	[Export]
	public UnitSelector unitSelector;

	Stopwatch loadTimer = new Stopwatch();

	GlobalSingleton Global;
	Civ3FileDialog FileDialog;

	public Player controller; // Player that's controlling the UI.

	// In a hotseat game, the curtain hiding the map while the screen is handed
	// to the next human player, and where each player last left the camera.
	private HotseatHandoff hotseatHandoff = null;
	private readonly Dictionary<ID, Vector2> hotseatCameraLocations = new();
	private readonly Dictionary<ID, Queue<MessageToUI>> heldMessages = new();

	// In a LAN game, the banner shown while another machine's player takes
	// their turn, and whether we've told the player the host is gone.
	private CanvasLayer lanWaitingBanner = null;
	private string lanWaitingText = null;
	private bool lanDisconnectShown = false;

	// Whether the banner is for other players taking their turns, rather
	// than for something else we wait on.
	private bool lanWaitingOnOthers = false;

	// While a LAN client has lost the host, the curtain saying it's trying
	// to reconnect. Once it's back, the turns pick up where the host's game
	// is, when that has been shown.
	private CanvasLayer lanReconnectCurtain = null;
	private Label lanReconnectLabel = null;
	private bool lanResyncPending = false;

	// With simultaneous turns: the round we began playing the player at the
	// screen, and this machine's players who have ended their turn this
	// round, which the host's game may not show yet.
	private bool lanGameStarted = false;
	private int simultaneousPlayingRound = -1;
	private int simultaneousRound = -1;
	private readonly HashSet<ID> simultaneousTurnsEnded = new();

	private MapView mapView;

	public enum GameState {
		PlayerTurn,
		ComputerTurn
	}
	public GameState CurrentState { get; private set; } = GameState.PlayerTurn;

	public MapUnit CurrentlySelectedUnit => unitSelector.CurrentlySelectedUnit;
	private bool HasCurrentlySelectedUnit() => CurrentlySelectedUnit != MapUnit.NONE;

	// When the game is in "goto" mode, the current destination and the cost of getting
	// there, in turns.
	//
	// Otherwise null.
	public GotoInfo gotoInfo = null;

	public BombardInfo bombardInfo = null;

	public TileInfo tileInfo = null;

	// When in observer mode, the number of turns to play before prompting the
	// user to advance the turn manually. This allows for more rapid debugging
	// without pressing the spacebar repeatedly.
	public int turnsLeftToFastForward = 0;

	bool errorOnLoad = false;

	public override void _EnterTree() {
		loadTimer.Start();
	}

	// Called when the node enters the scene tree for the first time.
	// The catch should always catch any error, as it's the general catch
	// that gives an error if we fail to load for some reason.
	public override async void _Ready() {
		Global = GetNode<GlobalSingleton>("/root/GlobalSingleton");

		FileDialog = GetNode<Civ3FileDialog>("%LoadDialog");
		FileDialog.Canceled += OnResolved;
		FileDialog.FileSelected += path => OnResolved();

		// A city the player zoomed to from its production popup has had its
		// say; on to the next.
		cityScreen.Hidden += ShowNextProductionPopup;

		try {
			await InitializeGame();
			await StartGame();
		} catch (Exception ex) {
			// The game is only partly set up, so nothing may be played; the
			// error offers the way back to the menu.
			errorOnLoad = true;
			CurrentState = GameState.ComputerTurn;
			string message = ex.Message;
			string[] stack = ex.StackTrace.Split("\r\n");   //for some reason it is returned with \r\n in the string as one line.  let's make it readable!
			foreach (string line in stack) {
				message = message + "\r\n" + line;
			}

			popupOverlay.ShowPopup(new ErrorMessage(message), PopupOverlay.PopupCategory.Advisor);
			log.Error(ex, "Unexpected error in Game.cs _Ready");
		}
	}

	private async Task InitializeGame() {
		// The mod path is left over from whatever was set up last. A game
		// made from a save object (a new game, or a scenario) takes the
		// save's; a saved game being loaded sets its own while it loads,
		// if it has one.
		Util.setModPath(Global.SaveGame?.ScenarioSearchPath);

		// Ensure we clear out our image caches, as scenarios and games will
		// use the same filenames but have different content for them.
		Util.ClearCaches();

		GameParams options = CreateGameParams();

		await CreateGameAndAssignPlayerController(options);
		// Only a LAN game plays turns at the same time; a LAN host decides
		// afresh whether this one does.
		if (!LanSession.IsActive) {
			EngineStorage.gameData.simultaneousTurns = false;
		}
		StartLanGame();

		foreach (var gameDataPlayer in EngineStorage.gameData.players) {
			if (TurnHandling.GetTurnNumber() == 0)
				if (gameDataPlayer.SitsOutFirstTurn())
					TurnHandling.InitTurnData(gameDataPlayer, true);
				else if (Global.SaveGame != null)
					TurnHandling.InitTurnData(gameDataPlayer, false);
		}

		InitializeMapView();
		InitializeAudio();
	}

	private void InitializeAudio() {
		AudioManager audio = GetNode<AudioManager>("/root/GlobalAudioManager");
		audio.StopMusic();
		// TODO: switch to in-game playlist / music logic
	}

	private async Task StartGame() {
		log.Information("Now in game!");

		TurnHandling.OnBeginTurn();

		loadTimer.Stop();
		TimeSpan stopwatchElapsed = loadTimer.Elapsed;
		log.Information("Game scene load time: " + Convert.ToInt32(stopwatchElapsed.TotalMilliseconds) + " ms");

		EmitSignal(SignalName.GameInitialized);

		if (ShouldShowScoreboard(EngineStorage.gameData)) {
			ShowScoreboard();
		}

		if (LanSession.IsSpectator) {
			// Spectators never play; see _UnhandledInput for what they can do.
			CurrentState = GameState.ComputerTurn;
		} else if (SimultaneousLanTurns) {
			ContinueSimultaneousTurns();
		} else if (LanSession.IsActive) {
			// Whoever plays first may be at another machine.
			Player active = EngineStorage.gameData.GetPlayer(EngineStorage.activePlayerID);
			if (active != null && !LanSession.IsLocalPlayer(active)) {
				ShowLanWaiting(active);
			} else if (active != null && LanSession.HasSeveralLocalPlayers) {
				// The others at this machine also need to look away.
				ShowHotseatHandoff(active, BeginLanTurn);
			} else {
				MaybeAutoplayLanTurn();
			}
		} else if (TurnHandling.IsHotseat(EngineStorage.gameData)) {
			// The first hotseat player also needs the others to look away.
			ShowHotseatHandoff(controller, OnPlayerStartTurn);
		}

		lanGameStarted = true;
		Global.ResetLoadGameFields();
	}

	private GameParams CreateGameParams() {
		return new(GamePaths.DefaultBicPath) {
			GetPediaIconsPath = (scenarioSearchPath) => {
				// When the game loading logic tries to load the PediaIcons file, set the
				// scenario search path and then use our Civ3MediaPath searching logic to
				// find the correct version of the file.
				//
				// This weird bit of indirection is necessary because the C7GameData project
				// can't depend on the C7 project without a circular dependency, and the
				// search logic has a Godot dependency, so it doesn't make sense to live
				// in the C7GameData project.
				//
				// This also helps ensure the weird stateful behavior of the Util class works,
				// since the search path/mod path is a static global variable - we want to
				// be sure it is always set properly, so doing it during game creation
				// is reasonable.
				Util.setModPath(scenarioSearchPath);
				log.Debug("RelativeModPath ", scenarioSearchPath);
				return Util.Civ3MediaPath("Text/PediaIcons.txt");
			},
			GameModeLoader = (config) => {
				Global.ActivateGameMode(config);
				return Global.GameMode.behaviors;
			},
		};
	}

	private async Task CreateGameAndAssignPlayerController(GameParams options) {
		// The AI's fog of war is this machine's setting; only a host's
		// engine (or a single machine's) plays the AI.
		EngineStorage.aiFogOfWar = DeveloperSettings.AIFogOfWar;

		// Initializes the game data and returns the "human" player
		if (Global.SaveGame != null) {
			controller = await CreateGame.createGame(Global.SaveGame, options.GameModeLoader);
		} else if (Global.LoadGamePath != null) {
			controller = await CreateGame.createGame(Global.LoadGamePath, options);
		} else {
			throw new InvalidOperationException("Save data was not set");
		}
	}

	private void InitializeMapView() {
		EngineStorage.ReadGameData((GameData gameData) => {
			GameMap map = gameData.map;

			if (mapView != null && mapView.CanShow(map)) {
				// A LAN snapshot of the same game: keep the map view, and
				// with it the camera, and point it at the new game data.
				mapView.RebindToGameData(gameData);
			} else {
				Vector2? cameraLocation = null;
				float cameraZoom = 1.0f;
				// The city whose tiles the city screen shows, if it's open.
				City shownCity = null;
				if (mapView != null) {
					cameraLocation = mapView.cameraLocation;
					cameraZoom = mapView.cameraZoom;
					shownCity = mapView.tileAssignmentLayer.city;
					RemoveChild(mapView);
					mapView.QueueFree();
				}

				mapView = new MapView(this, map.numTilesWide, map.numTilesTall, map.wrapHorizontally, map.wrapVertically);
				AddChild(mapView);

				mapView.cameraZoom = cameraZoom;
				mapView.gridLayer.visible = false;

				if (!cameraLocation.HasValue) {
					CenterCameraOnController();
				} else {
					mapView.cameraLocation = cameraLocation.Value;
				}

				// Carry the open city screen's city over to the new map view, as the same city in the new game data if it's still there.
				// Otherwise the old city is kept for the city screen to notice that it's gone when it's refreshed for the new game data
				// (see CityScreen.RefreshAfterGameReplaced, which the LAN snapshot handler calls next).
				if (shownCity != null) {
					City sameCity = gameData.cities.Find(c => c.id == shownCity.id);
					mapView.tileAssignmentLayer.city = sameCity ?? shownCity;
				}
			}

			// Allow the city screen to control whether tile assignments
			// are visible and map UI locations back to map locations.
			cityScreen.tileAssignmentLayer = mapView.tileAssignmentLayer;
			cityScreen.mapView = mapView;
			cityScreen.citizenTypes = gameData.citizenTypes;

			// Allow the domestic advisor to trigger popups.
			advisor.domesticAdvisor.SetPopupOverlay(popupOverlay);
		});
	}

	// If the UI controller has any cities, focus on their capital. Otherwise,
	// focus on their starting settler.
	private void CenterCameraOnController() {
		if (controller.cities.Count > 0) {
			City capital = controller.cities.Find(c => c.IsCapital());
			if (capital != null)
				mapView.centerCameraOnTile(capital.location);
		} else {
			MapUnit startingSettler =
				controller.units.Find(u => u.unitType.actions.Contains(UnitAction.BuildCity));
			if (startingSettler != null)
				mapView.centerCameraOnTile(startingSettler.location);
		}
	}

	// Hooks this game up to the LAN game being hosted or joined, if any.
	private void StartLanGame() {
		if (LanSession.Host != null) {
			LanSession.Host.StartGame();
			controller = EngineStorage.gameData.GetUIControllerPlayer();
		} else if (LanSession.Client != null) {
			LanClient client = LanSession.Client;
			EngineStorage.uiFollowsActivePlayer = false;
			if (client.IsSpectator) {
				// As the whole game, or as one or all of the civilizations.
				LanSession.ShowSpectatorView(EngineStorage.gameData);
			} else {
				// Of this machine's players, whoever plays first.
				ID active = EngineStorage.gameData.simultaneousTurns
					? TurnHandling.PlayersToMove(EngineStorage.gameData).FirstOrDefault(p => client.PlayerIDs.Contains(p.id))?.id
					: EngineStorage.activePlayerID;
				EngineStorage.uiControllerID = client.PlayerIDs.Contains(active) ? active : client.PlayerIDs.FirstOrDefault();
			}
			controller = EngineStorage.gameData.GetUIControllerPlayer();
			client.SnapshotReceived = OnLanSnapshot;
			client.UiMessageReceived = json => HandleEngineMessage(NetSerialization.DeserializeMessageToUI(json));
			client.PlayersChanged = OnLanPlayersChanged;
			// A spectator's new view shows straight away, and its snapshots
			// follow.
			client.SpectatorViewChanged = () => {
				LanSession.ShowSpectatorView(EngineStorage.gameData);
				controller = EngineStorage.gameData.GetUIControllerPlayer();
				mapView?.InvalidateMap();
			};
			// Our own moves show before the host's snapshot does.
			client.OrderPredicted = () => mapView?.InvalidateMap();
			// Losing the host from here on, we try to get back to it; and
			// after closing the game, the player can rejoin it.
			client.ReconnectAutomatically = true;
			LanSession.RememberJoinedGame(client);
		}
	}

	// Rejoining with several seats can take them one at a time, so the
	// player we were waiting on may turn out to be one of ours.
	private void OnLanPlayersChanged() {
		if (lanResyncPending) {
			// Back after losing the host: the turns pick up once its game
			// is shown.
			return;
		}
		if (SimultaneousLanTurns) {
			ContinueSimultaneousTurns();
			return;
		}
		Player active = EngineStorage.gameData.GetPlayer(EngineStorage.activePlayerID);
		if (IsWaitingForRemotePlayer && active != null && LanSession.IsLocalPlayer(active)) {
			OnControllerTurnStart(active);
		}
	}

	private void PollLanSession() {
		LanSession.Host?.Poll();
		LanSession.Client?.Poll();

		LanClient client = LanSession.Client;
		if (client != null && !PollLanConnection(client)) {
			return;
		}

		// With simultaneous turns, the host's game says when each of our
		// players' turns is over, even when their time runs out.
		if (lanGameStarted && SimultaneousLanTurns && !lanDisconnectShown) {
			ContinueSimultaneousTurns();
		} else if (lanWaitingOnOthers && !SimultaneousLanTurns) {
			// Whoever we wait on may have lost their connection, or come
			// back.
			Player active = EngineStorage.gameData.GetPlayer(EngineStorage.activePlayerID);
			if (active != null && !LanSession.IsLocalPlayer(active)) {
				ShowLanWaiting(active);
			}
		}
	}

	// Keeps a client's game screen in step with its connection to the host:
	// while it reconnects, or waits for the host to resume the game, nothing
	// can be done. Returns whether the game goes on as usual.
	private bool PollLanConnection(LanClient client) {
		if (client.Reconnecting) {
			// Once back, the turns pick up when the host's game is shown,
			// which may be as soon as the host answers.
			lanResyncPending = true;
			// Through a relay, the relay says why, like the host being away.
			ShowLanReconnecting(client.ReconnectAttempt, client.Endpoint is RelayEndpoint ? client.LastReconnectError : null);
			return false;
		}
		if (lanReconnectCurtain != null) {
			HideLanReconnecting();
		}
		if (!client.IsConnected && (client.RejectedReason != null || !client.ReconnectAutomatically)) {
			if (!lanDisconnectShown) {
				lanDisconnectShown = true;
				HideLanWaiting();
				string why = client.RejectedReason == null ? "We have lost contact with the host." : $"The host turned us away: {client.RejectedReason}";
				popupOverlay.ShowPopup(
					new ConfirmationPopup(
						$"{why}\nThe game cannot continue.\n\n",
						"Return to the main menu.",
						"Let me look around first.",
						OnRetire),
					PopupOverlay.PopupCategory.Advisor);
			}
			return false;
		}
		if (client.HostIsResuming) {
			// The host is hosting our game again, from where it last saved
			// it, and shows it to us once it starts.
			lanResyncPending = true;
			ShowLanBanner("Waiting for the host to resume the game...");
			return false;
		}
		return !lanDisconnectShown;
	}

	// The curtain over the game while we try to get back to the host.
	private void ShowLanReconnecting(int attempt, string why = null) {
		CurrentState = GameState.ComputerTurn;
		string text = attempt <= 1
			? "Lost connection to the host. Reconnecting..."
			: $"Lost connection to the host. Reconnecting... (attempt {attempt})";
		if (why != null) {
			text += $"\n{why}";
		}
		if (lanReconnectCurtain != null) {
			lanReconnectLabel.Text = text;
			return;
		}
		log.Information("Lost the connection to the host, reconnecting");
		HideLanWaiting();

		// Dims the game and takes every click.
		ColorRect dimmer = new() { Color = new Color(0, 0, 0, 0.6f), MouseFilter = Control.MouseFilterEnum.Stop };
		dimmer.SetAnchorsPreset(Control.LayoutPreset.FullRect);

		VBoxContainer box = new() { Alignment = BoxContainer.AlignmentMode.Center };
		box.AddThemeConstantOverride("separation", 16);
		box.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		dimmer.AddChild(box);

		lanReconnectLabel = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
		lanReconnectLabel.AddThemeFontSizeOverride("font_size", 24);
		lanReconnectLabel.AddThemeColorOverride("font_color", Colors.White);
		box.AddChild(lanReconnectLabel);

		Button menu = new() { Text = "Return to main menu", SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
		menu.AddThemeFontSizeOverride("font_size", 18);
		menu.Pressed += () => {
			LanSession.Client?.StopReconnecting();
			OnRetire();
		};
		box.AddChild(menu);

		// Above the hotseat curtain, too.
		lanReconnectCurtain = new CanvasLayer { Layer = 110 };
		lanReconnectCurtain.AddChild(dimmer);
		AddChild(lanReconnectCurtain);
	}

	private void HideLanReconnecting() {
		log.Information("Reconnected to the host");
		lanReconnectCurtain?.QueueFree();
		lanReconnectCurtain = null;
		lanReconnectLabel = null;
	}

	// Back with the host after losing it, or after it resumed the game:
	// each of our players picks up their turn where the host's game is now.
	private void ResumeLanTurns() {
		HideLanWaiting();
		CurrentState = GameState.ComputerTurn;
		if (LanSession.IsSpectator) {
			return;
		}
		GameData gameData = EngineStorage.gameData;
		if (SimultaneousLanTurns) {
			// Begin again with whichever of ours is still to move.
			simultaneousPlayingRound = -1;
			ContinueSimultaneousTurns();
			return;
		}
		// The host told us whose turn it is before showing us the game.
		if (LanSession.TurnClock?.activePlayerID is ID activeID) {
			EngineStorage.activePlayerID = activeID;
		}
		Player active = gameData.GetPlayer(EngineStorage.activePlayerID);
		if (active != null && active.isHuman && !active.hasPlayedThisTurn) {
			OnControllerTurnStart(active);
		}
	}

	// A LAN client shows the host's game: replace ours with the snapshot,
	// and point the UI at the new units, cities and players.
	private void OnLanSnapshot(C7GameData.Save.SaveGame save) {
		Stopwatch applyTime = Stopwatch.StartNew();
		GameData gameData = CreateGame.ReplaceWithSnapshot(save, Global.GameMode.behaviors);
		// Textures, art names and colors looked up by game object would
		// otherwise keep the old game's objects alive; the map looks up
		// terrain textures by value.
		TextureLoader.ForgetGameObjects();
		AnimationManager.ForgetGameObjects();
		C7.Textures.PlayerTextureUtil.ForgetGameObjects();
		// Animations of units that aren't in the snapshot would never end.
		animationController.animTracker.forgetRemovedUnits(gameData);
		// A spectator sees the map as the host sends it.
		LanSession.ShowSpectatorView(gameData);

		controller = gameData.GetUIControllerPlayer();
		InitializeMapView();
		unitSelector.RefreshAfterGameReplaced();
		cityScreen.RefreshAfterGameReplaced();
		if (bombardInfo != null) {
			MapUnit bombarder = gameData.GetUnit(bombardInfo.bombardingUnit.id);
			setBombard(bombarder);
		}

		// The tile info box and the goto path refer to tiles, which are
		// new objects too: point them at the same places on the new map.
		if (tileInfo != null) {
			Tile target = gameData.map.tileAt(tileInfo.targetTile.XCoordinate, tileInfo.targetTile.YCoordinate);
			tileInfo = target != Tile.NONE ? new TileInfo(target) : null;
		}
		// So do the messages held for our other players.
		foreach (Queue<MessageToUI> held in heldMessages.Values) {
			foreach (MessageToUI msg in held) {
				RebindToGame(msg, gameData);
			}
		}

		Tile gotoDestination = gotoInfo?.destinationTile;
		lastTile = null;
		if (gotoDestination != null) {
			gotoInfo = GetGotoInfo(gameData.map.tileAt(gotoDestination.XCoordinate, gotoDestination.YCoordinate));
		}

		// An open advisor shows the old game's cities and techs.
		advisor.RefreshAfterGameReplaced();

		if (lanResyncPending && LanSession.Client?.HostIsResuming != true) {
			lanResyncPending = false;
			ResumeLanTurns();
		}

		if (applyTime.ElapsedMilliseconds > 100) {
			log.Information("Showing the host's snapshot took {Milliseconds} ms", applyTime.ElapsedMilliseconds);
		}
	}

	// Points the players, cities, units and tiles a message refers to at
	// the same ones in a LAN snapshot's game. Any that aren't in the snapshot
	// (like a city since destroyed) are left as they were, which is enough
	// for the message's text.
	private static void RebindToGame(object msg, GameData gameData) {
		for (Type type = msg.GetType(); type != null && type != typeof(object); type = type.BaseType) {
			foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)) {
				object rebound = field.GetValue(msg) switch {
					Player p => gameData.GetPlayer(p.id),
					City c => gameData.cities.Find(x => x.id == c.id),
					MapUnit u when u != MapUnit.NONE => gameData.GetUnit(u.id),
					Tile t when t != Tile.NONE => gameData.map.tileAt(t.XCoordinate, t.YCoordinate),
					_ => null,
				};
				if (rebound != null) {
					field.SetValue(msg, rebound);
				}
			}
		}
	}

	// With the --lan-autoplay developer option, ends our turn soon after it
	// starts, for watching turns pass between machines.
	private void MaybeAutoplayLanTurn() {
		if (!LanSession.DevAutoplay) {
			return;
		}
		GetTree().CreateTimer(1.5).Timeout += () => {
			// The game may have been left in the meantime.
			if (!IsInstanceValid(this) || !IsInsideTree()) {
				return;
			}
			popupOverlay.OnHidePopup();
			if (CurrentState == GameState.PlayerTurn) {
				DoActualEndTurn();
			}
		};
	}

	// Games with more than one human show the scoreboard unless it was
	// turned off when the game was set up.
	private static bool ShouldShowScoreboard(GameData gameData) {
		return gameData.rules?.ShowScoreboard != false
			&& (LanSession.IsActive || TurnHandling.IsHotseat(gameData));
	}

	// The players' scores and the turn clock, in the top right corner under
	// the toolbar. It is part of the HUD, so advisors and popups cover it.
	private void ShowScoreboard() {
		Scoreboard scoreboard = new();
		GetNode<Control>("CanvasLayer/Control").AddChild(scoreboard);
		scoreboard.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		scoreboard.GrowHorizontal = Control.GrowDirection.Begin;
		scoreboard.OffsetLeft = scoreboard.OffsetRight = 0;
		scoreboard.OffsetTop = scoreboard.OffsetBottom = 48;
	}

	// Shows whose turn it is while another machine's player moves.
	private void ShowLanWaiting(Player active) {
		ShowLanWaiting([active]);
	}

	// Shows who we wait on while other machines' players move. The host can
	// go on without those who have lost their connection.
	private void ShowLanWaiting(List<Player> others) {
		List<Player> absent = others.Where(p => !IsLanPlayerHere(p)).ToList();
		string text;
		if (absent.Count > 0) {
			string names = string.Join(", ", absent.Select(p => $"{p.name ?? p.civilization.leader} of the {p.civilization.noun}"));
			text = $"Waiting for {names} to reconnect...";
		} else if (others.Count == 1) {
			Player active = others[0];
			string playerName = active.name ?? active.civilization.leader;
			text = $"Waiting for {playerName} of the {active.civilization.noun} to play their turn...";
		} else {
			text = $"Waiting for {others.Count} other players to finish their turns...";
		}
		if (lanWaitingText == null || !lanWaitingText.StartsWith(text)) {
			log.Information("{Waiting}", text);
		}
		if (absent.Count > 0 && LanSession.Host != null) {
			ShowLanBanner(text, "Continue without them", LanSession.Host.ContinueWithoutAbsentPlayers);
		} else {
			ShowLanBanner(text);
		}
		lanWaitingOnOthers = true;
	}

	// Whether the player is at their machine, or the host has gone on
	// without them so their turn ends by itself. Before the host says who is
	// connected, everyone is taken to be.
	private static bool IsLanPlayerHere(Player player) {
		TurnClockInfo clock = LanSession.TurnClock;
		if (clock?.connectedPlayers == null || clock.connectedPlayers.Count == 0) {
			return true;
		}
		return clock.connectedPlayers.Contains(player.id) || clock.awayPlayers?.Contains(player.id) == true;
	}

	private void ShowLanBanner(string text, string buttonText = null, Action onPressed = null) {
		CurrentState = GameState.ComputerTurn;
		if (lanWaitingBanner != null && lanWaitingText == text + buttonText) {
			return;
		}
		HideLanWaiting();
		lanWaitingText = text + buttonText;

		Label label = new() {
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		label.AddThemeFontSizeOverride("font_size", 20);
		label.AddThemeColorOverride("font_color", Colors.White);

		HBoxContainer row = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 12);
		row.AddChild(label);
		if (buttonText != null) {
			Button button = new() { Text = buttonText };
			button.AddThemeFontSizeOverride("font_size", 16);
			button.Pressed += onPressed;
			row.AddChild(button);
		}

		PanelContainer panel = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.65f) });
		panel.AddChild(row);

		lanWaitingBanner = new CanvasLayer { Layer = 90 };
		lanWaitingBanner.AddChild(panel);
		AddChild(lanWaitingBanner);

		// A strip across the top of the screen, below the toolbar, leaving
		// room for the scoreboard on the right.
		panel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		panel.OffsetTop = 70;
		panel.OffsetBottom = 110;
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		LayOutLanBanner(panel);
		Viewport viewport = GetViewport();
		Action relayout = () => {
			if (IsInstanceValid(panel)) {
				LayOutLanBanner(panel);
			}
		};
		viewport.SizeChanged += relayout;
		panel.TreeExiting += () => viewport.SizeChanged -= relayout;
	}

	// Keeps the banner clear of the toolbar on the left and the scoreboard on
	// the right while there's room, giving up those margins in proportion on
	// a narrow window so the strip never turns inside out.
	private void LayOutLanBanner(Control panel) {
		const float left = 160, right = 440, minWidth = 300;
		float width = GetViewport().GetVisibleRect().Size.X;
		float scale = Math.Clamp((width - minWidth) / (left + right), 0, 1);
		panel.OffsetLeft = left * scale;
		panel.OffsetRight = -right * scale;
	}

	// A spectator hears about the world's events without having to answer
	// a popup for each: they show for a while in a list down the left.
	private VBoxContainer spectatorNews;

	private void ShowSpectatorNews(string text) {
		if (spectatorNews == null) {
			spectatorNews = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			spectatorNews.AddThemeConstantOverride("separation", 4);
			spectatorNews.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
			spectatorNews.Position = new Vector2(16, 80);
			CanvasLayer layer = new() { Layer = 90 };
			layer.AddChild(spectatorNews);
			AddChild(layer);
		}

		Label label = new() { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
		label.AddThemeStyleboxOverride("normal", TemporaryPopup.PopupStyleBox());
		label.AddThemeColorOverride("font_color", Colors.White);
		label.AddThemeFontSizeOverride("font_size", 16);
		spectatorNews.AddChild(label);
		while (spectatorNews.GetChildCount() > 8) {
			Node oldest = spectatorNews.GetChild(0);
			spectatorNews.RemoveChild(oldest);
			oldest.QueueFree();
		}
		GetTree().CreateTimer(10).Timeout += () => {
			if (IsInstanceValid(label)) {
				label.QueueFree();
			}
		};
	}

	private void HideLanWaiting() {
		lanWaitingBanner?.QueueFree();
		lanWaitingBanner = null;
		lanWaitingText = null;
		lanWaitingOnOthers = false;
	}

	// Whether this machine plays in a LAN game whose humans play their turns
	// at the same time.
	private static bool SimultaneousLanTurns =>
		LanSession.IsActive && !LanSession.IsSpectator && EngineStorage.gameData?.simultaneousTurns == true;

	// This machine's players yet to finish this round, in turn order.
	private List<Player> LocalPlayersToMove() {
		GameData gameData = EngineStorage.gameData;
		NoteSimultaneousRound(gameData);
		return TurnHandling.PlayersToMove(gameData)
			.Where(p => LanSession.IsLocalPlayer(p) && !simultaneousTurnsEnded.Contains(p.id))
			.ToList();
	}

	// Forgets whose turns ended once a new round has begun.
	private void NoteSimultaneousRound(GameData gameData) {
		if (gameData.turn != simultaneousRound) {
			simultaneousRound = gameData.turn;
			simultaneousTurnsEnded.Clear();
		}
	}

	// With simultaneous turns, everyone plays at once, but a machine's own
	// players still take turns at it. Once the player at the screen is done,
	// this hands it to the next of them still to move, or says who we're
	// waiting on once they all are. It's called whenever that may change.
	private void ContinueSimultaneousTurns() {
		GameData gameData = EngineStorage.gameData;
		if (gameData.gameOver) {
			return;
		}
		List<Player> ours = LocalPlayersToMove();
		bool playing = (CurrentState == GameState.PlayerTurn || hotseatHandoff != null)
			&& simultaneousPlayingRound == gameData.turn && ours.Contains(controller);
		if (playing) {
			return;
		}

		if (ours.Count == 0) {
			// The curtain may be up for a player whose time ran out, and
			// others may be waiting behind it.
			hotseatHandoff?.QueueFree();
			hotseatHandoff = null;
			queuedHandoffs.Clear();
			List<Player> others = TurnHandling.PlayersToMove(gameData).Where(p => !LanSession.IsLocalPlayer(p)).ToList();
			if (others.Count > 0) {
				ShowLanWaiting(others);
			} else {
				// The computer players are moving.
				CurrentState = GameState.ComputerTurn;
				HideLanWaiting();
			}
			return;
		}

		Player next = ours[0];
		simultaneousPlayingRound = gameData.turn;
		HideLanWaiting();
		if (LanSession.HasSeveralLocalPlayers) {
			ShowHotseatHandoff(next, BeginLanTurn);
			return;
		}
		controller = next;
		EngineStorage.uiControllerID = next.id;
		BeginLanTurn();
	}

	public override void _ExitTree() {
		// Leaving the game leaves the LAN game, too.
		LanSession.End();
	}

	// Called when the engine hands the UI to a player at the start of their
	// turn. In a hotseat game this may be a different player than before, so
	// hide the map until the new player is at the screen.
	private void OnControllerTurnStart(Player next) {
		if (LanSession.IsSpectator) {
			// Turns pass without the spectator.
			return;
		}

		if (LanSession.IsActive) {
			// Each machine plays its own players; the others wait for them.
			EngineStorage.activePlayerID = next.id;
			if (!LanSession.IsLocalPlayer(next)) {
				ShowLanWaiting(next);
				return;
			}
			HideLanWaiting();
			if (LanSession.HasSeveralLocalPlayers) {
				// This machine's players take turns at it, as in a hotseat
				// game.
				ShowHotseatHandoff(next, BeginLanTurn);
				return;
			}
			BeginLanTurn();
			return;
		}

		if (!TurnHandling.IsHotseat(EngineStorage.gameData)) {
			controller = next;
			OnPlayerStartTurn();
			return;
		}

		ShowHotseatHandoff(next, OnPlayerStartTurn);
	}

	private void BeginLanTurn() {
		OnPlayerStartTurn();
		MaybeAutoplayLanTurn();
	}

	private void ShowHotseatHandoff(Player next, Action onBeginTurn) {
		ShowHotseatHandoff(next,
			"It is your turn. Make sure the other players aren't looking.",
			"Begin Turn",
			onBeginTurn);
	}

	// Handoffs asked for while the curtain is already up, shown in turn once
	// it's dismissed, so that what each of them goes on to do isn't lost.
	private readonly Queue<(ID next, string message, string buttonText, Action onContinue)> queuedHandoffs = new();
	// Who the curtain is up for, and what it says.
	private (ID, string) hotseatHandoffShown;

	private void ShowNextQueuedHandoff() {
		while (hotseatHandoff == null && queuedHandoffs.Count > 0) {
			var (nextID, message, buttonText, onContinue) = queuedHandoffs.Dequeue();
			// Found by id, as a LAN client's snapshots replace the players.
			Player next = EngineStorage.gameData?.GetPlayer(nextID);
			if (next == null) {
				continue;
			}
			// The player is already at the screen.
			if (next.id == controller?.id) {
				onContinue();
				continue;
			}
			ShowHotseatHandoff(next, message, buttonText, onContinue);
		}
	}

	private void ShowHotseatHandoff(Player next, string message, string buttonText, Action onContinue) {
		if (hotseatHandoff != null) {
			// The same news twice, like a turn starting again for whoever the
			// curtain is already up for, is only shown once.
			bool duplicate = hotseatHandoffShown == (next.id, message)
				|| queuedHandoffs.Any(h => h.next == next.id && h.message == message);
			if (!duplicate) {
				queuedHandoffs.Enqueue((next.id, message, buttonText, onContinue));
			}
			return;
		}
		hotseatHandoffShown = (next.id, message);

		CurrentState = GameState.ComputerTurn;

		// Close anything the previous player left open, and remember where
		// they were looking so we can restore it on their next turn.
		if (tileInfo != null) {
			HideTileInfo();
		}
		SetGotoMode(false);
		setBombard(null);
		cityScreen.Hide();
		advisor.Hide();
		gameViews.Hide();
		diplomacy.Hide();
		// Production news waits for its owner's turn.
		HoldShownProductionPopup();
		if (controller != null && controller != next) {
			hotseatCameraLocations[controller.id] = mapView.cameraLocation;
		}

		controller = next;
		// A hotseat game's engine has already moved the UI to them; a LAN
		// client's players move it themselves.
		EngineStorage.uiControllerID = next.id;
		if (hotseatCameraLocations.TryGetValue(controller.id, out Vector2 cameraLocation)) {
			mapView.cameraLocation = cameraLocation;
		} else {
			CenterCameraOnController();
		}

		// Games without player names (e.g. older saves) fall back to the leader.
		string playerName = controller.name ?? controller.civilization.leader;
		hotseatHandoff = new HotseatHandoff(
			$"{playerName} - {controller.civilization.name}",
			message,
			buttonText,
			() => {
				hotseatHandoff = null;
				onContinue();
				ShowNextQueuedHandoff();
			});
		CanvasLayer curtainLayer = new() { Layer = 100 };
		curtainLayer.AddChild(hotseatHandoff);
		hotseatHandoff.TreeExited += curtainLayer.QueueFree;
		AddChild(curtainLayer);
	}

	public void HandleEngineMessage(MessageToUI msg) {
		// Whatever the engine tells us about may have changed the map.
		mapView?.InvalidateMap();

		// The map shows everyone's cities, so it's updated right away, even
		// for a message that's held for another player.
		UpdateMapForMessage(msg);

		// Hold messages for a human player who isn't at the screen (for
		// example barbarians raiding them during the AI turns) until they are.
		// On a LAN that is only one of this machine's players. A deal another
		// human proposes can't wait, as they are waiting on it.
		if (msg.recipient != null && msg.recipient.isHuman && msg.recipient.id != controller.id
			&& (!LanSession.IsActive || LanSession.IsLocalPlayer(msg.recipient))
			&& msg is not MsgShowDealProposal) {
			if (!heldMessages.TryGetValue(msg.recipient.id, out Queue<MessageToUI> held)) {
				held = new();
				heldMessages[msg.recipient.id] = held;
			}
			held.Enqueue(msg);
			return;
		}

		ShowEngineMessage(msg);
	}

	// What a message changes on the map, done when the message arrives.
	private void UpdateMapForMessage(MessageToUI msg) {
		switch (msg) {
			case MsgCityDestroyed mCD:
				// A LAN client has already redrawn the map without the city.
				if (mCD.city != null) {
					mapView?.cityLayer.UpdateAfterCityDestruction(mCD.city);
				}
				break;
			case MsgCityCaptured mCCap:
				mapView?.cityLayer.UpdateAfterCityCapture(mCCap.city);
				break;
		}
	}

	// Shows a message to the player at the screen: the popups, screens and
	// other reactions to the message. Held messages are shown when they're
	// replayed.
	private void ShowEngineMessage(MessageToUI msg) {
		GameData gameData = EngineStorage.gameData;

		switch (msg) {
			case MsgStartTurn when SimultaneousLanTurns:
				// Every human is told at once; we play ours one by one.
				ContinueSimultaneousTurns();
				break;
			case MsgStartTurn mST:
				// Hotseat follows the UI controller, which the engine moved to
				// the player whose turn it is; a LAN game is told who they are.
				OnControllerTurnStart(LanSession.IsActive ? mST.player : EngineStorage.gameData.GetUIControllerPlayer());
				break;
			case MsgShowCityScreen mSCS:
				ShowCityScreenForCity(gameData, mSCS.city);
				break;
			case MsgCityCreated mCC:
				ShowCityScreenForCity(gameData, mCC.city);
				break;
			case MsgCivilizationDestroyed mCivD when LanSession.IsSpectator:
				ShowSpectatorNews($"The {mCivD.civilization.noun} have been destroyed");
				break;
			case MsgCivilizationDestroyed mCivD:
				popupOverlay.ShowPopup(new CivilizationDestroyed(mCivD.civilization), PopupOverlay.PopupCategory.Advisor);
				InterestingEvent();
				break;
			case MsgShowMilitaryAdvisorPopup mSMAP: {
					// News like a golden age or a city lost to disorder waits its
					// turn behind any popup already showing, rather than being lost.
					var mood = mSMAP.happy ? AdvisorHead.Mood.Happy : AdvisorHead.Mood.Angry;
					var pop = new InformationalPopup(mSMAP.message, AdvisorHead.Advisor.Military, mood);
					popupOverlay.ShowPopup(pop, PopupOverlay.PopupCategory.Advisor);
					break;
				}
			case MsgShowScienceAdvisorPopup mSSAP: {
					// The space race news (such as the ship being complete) is too
					// important to drop, so it waits its turn behind any popup
					// already showing.
					AdvisorHead.Mood scienceMood = mSSAP.mood switch {
						MsgShowScienceAdvisorPopup.Mood.Happy => AdvisorHead.Mood.Happy,
						MsgShowScienceAdvisorPopup.Mood.Angry => AdvisorHead.Mood.Angry,
						MsgShowScienceAdvisorPopup.Mood.Sad => AdvisorHead.Mood.Sad,
						_ => AdvisorHead.Mood.Surprised,
					};
					var pop = new InformationalPopup(mSSAP.message, AdvisorHead.Advisor.Science, scienceMood);
					popupOverlay.ShowPopup(pop, PopupOverlay.PopupCategory.Advisor);
					break;
				}
			case MsgWonderCompleted mWC: {
					// Like the space race news, this is too important to drop, so
					// it waits its turn behind any popup already showing.
					var pop = new InformationalPopup(mWC.Announcement(), AdvisorHead.Advisor.Domestic, AdvisorHead.Mood.Surprised);
					popupOverlay.ShowPopup(pop, PopupOverlay.PopupCategory.Advisor);
					break;
				}
			case MsgShowDomesticAdvisorPopup mSDAP: {
					// Like the military advisor's news, this waits its turn.
					var pop = new InformationalPopup(mSDAP.message, AdvisorHead.Advisor.Domestic, AdvisorHead.Mood.Angry);
					popupOverlay.ShowPopup(pop, PopupOverlay.PopupCategory.Advisor);
					break;
				}
			case MsgShowScienceAdvisor mSSA:
				EmitSignal(SignalName.ShowSpecificAdvisor, C7Action.ShowScienceAdvisor);
				break;
			case MsgShowScienceSelection mSSS:
				popupOverlay.ShowPopup(new ScienceSelection(controller, mSSS.discovered), PopupOverlay.PopupCategory.Info);
				// Research something even if the player dismisses the popup.
				new MsgPickDefaultResearch().send();
				break;
			case MsgCityProductionCompleted mCPC when mCPC.city != null:
				EnqueueProductionPopup(mCPC);
				break;
			case MsgUpdateUiAfterDomesticChange mUUASC:
				// Ensure the citizen moods are correct before displaying them.
				foreach (City c in controller.cities) {
					c.RecalculateCitizenMoods(gameData);
				}
				EmitSignal(SignalName.ShowSpecificAdvisor, C7Action.ShowDomesticAdvisor);
				break;
			case MsgShowTradeOffer mSTO:
				Action showOffer = () => diplomacy.ShowDealScreenForPlayer(
					mSTO.humanPlayer.id, mSTO.aiPlayer.id,
					humanGives: mSTO.aiWant,
					humanWants: mSTO.aiGive);
				// In a hotseat game the offer may be for a player who isn't at
				// the screen, so hand it to them first.
				if (mSTO.humanPlayer.id != controller.id) {
					ShowHotseatHandoff(mSTO.humanPlayer,
						$"The {mSTO.aiPlayer.civilization.noun} have a proposal for you.",
						"Hear Them Out",
						showOffer);
				} else {
					showOffer();
				}
				break;
			case MsgShowTerritoryDemand mSTD:
				Action showDemand = () => diplomacy.ShowTerritoryDemand(
					mSTD.humanPlayer.id, mSTD.aiPlayer.id, mSTD.unitCount, mSTD.repeatOffense);
				if (mSTD.humanPlayer.id != controller.id) {
					ShowHotseatHandoff(mSTD.humanPlayer,
						$"The {mSTD.aiPlayer.civilization.noun} demand to speak with you.",
						"Hear Them Out",
						showDemand);
				} else {
					showDemand();
				}
				break;
			case MsgDisplayHurryProductionPopup mDHPP:
				if (mDHPP.details.errorMessage != null) {
					popupOverlay.ShowPopup(
						new InformationalPopup(mDHPP.details.errorMessage),
						PopupOverlay.PopupCategory.Advisor);
				} else {
					popupOverlay.ShowPopup(
						new ConfirmationPopup(message: mDHPP.details.costMessage,
												yesText: "Yes I'm sure!",
												noText: "Maybe you're right. Nevermind.",
												yesAction: () => {
													new MsgDoHurryProduction(mDHPP.city).send();
												}),
						PopupOverlay.PopupCategory.Advisor);
				}
				break;
			case MsgDisplayStopWorkerActionPopup mDSWA:
				popupOverlay.ShowPopup(
					new ConfirmationPopup(
						$"This worker has been ordered to {C7Action.ToTooltip(mDSWA.workerJob.UIAction)} and will be done in {mDSWA.turnsLeft} turns." +
						$"\nDo you want them to stop?",
						"Yes, there is more important work to do!",
						"No, carry on.",
						() => {
							new MsgDoStopWorkerAction(mDSWA.worker).send();
						}),
					PopupOverlay.PopupCategory.Advisor);
				break;
			case MsgWarDeclaration mWD when LanSession.IsSpectator:
				ShowSpectatorNews($"The {mWD.aggressor.civilization.noun} declared war on the {mWD.opponent.civilization.noun}");
				break;
			case MsgWarDeclaration mWD:
				popupOverlay.ShowPopup(
					new InformationalPopup($"The {mWD.aggressor.civilization.noun} declared war on the {mWD.opponent.civilization.noun}"),
					PopupOverlay.PopupCategory.Advisor);
				InterestingEvent();
				break;
			case MsgShowTemporaryPopup mSTP:
				Vector2 pos = mapView.screenLocationOfTile(mSTP.location, true);
				TemporaryPopup.Show(this, mSTP.message, pos);
				break;
			case MsgUnitMoved mUUAAB:
				EmitSignal(SignalName.UnitMoved, new ParameterWrapper<MapUnit>(mUUAAB.Unit));
				break;
			case MsgTransportUnloaded mTU:
				// UnitMoved is enough to refresh UI
				EmitSignal(SignalName.UnitMoved, new ParameterWrapper<MapUnit>(mTU.Unit));
				break;
			case MsgDisplayAbandonCityPopup mDACP:
				// The menu greys the order out for a city that can't be
				// abandoned (e.g. our only one), but explain if it gets here.
				string cannotAbandon = null;
				EngineStorage.ReadGameData((GameData gameData) => {
					cannotAbandon = CityInteractions.WhyCannotAbandon(mDACP.city.owner, mDACP.city);
				});
				if (cannotAbandon != null) {
					popupOverlay.ShowPopup(new InformationalPopup(cannotAbandon, AdvisorHead.Advisor.Domestic, AdvisorHead.Mood.Sad),
						PopupOverlay.PopupCategory.Advisor);
					break;
				}
				popupOverlay.ShowPopup(
					new ConfirmationPopup(
						$"Are you sure you want to abandon {mDACP.city.name}?",
						"Yes, we don't want it anymore.",
						"No, sorry.",
						() => {
							new MsgAbandonCity(mDACP.city).send();
						}),
					PopupOverlay.PopupCategory.Advisor);
				break;
			case MsgDisplayRazeCityPopup mDRCP:
				popupOverlay.ShowPopup(
					new ConfirmationPopup(
						$"We have taken {mDRCP.city.name}. What shall we do with it?",
						"Keep the city.",
						"Raze it to the ground!",
						() => { },
						() => {
							new MsgAbandonCity(mDRCP.city).send();
						}),
					PopupOverlay.PopupCategory.Advisor);
				break;
			case MsgNoHumansRemain:
				popupOverlay.ShowPopup(
					new ConfirmationPopup(
						"Every human player has been defeated.\nThis game is over.\n\n",
						"Return to the main menu.",
						"Let me look around first.",
						() => {
							OnRetire();
						}),
					PopupOverlay.PopupCategory.Advisor);

				InterestingEvent();
				break;
			case MsgShowDealProposal mSDP:
				Action showProposal = () => {
					popupOverlay.ShowPopup(
						new ConfirmationPopup(
							DealScreen.DescribeDeal(mSDP.proposer, mSDP.proposerGives, mSDP.proposerWants),
							"We accept.",
							"We refuse.",
							() => { new MsgRespondToDeal(true).send(); },
							() => { new MsgRespondToDeal(false).send(); }) { escapeMeansNo = true },
						PopupOverlay.PopupCategory.Advisor);
					InterestingEvent();
				};
				// A LAN client's players take turns at its screen, so the deal
				// may be for one who isn't at it.
				if (mSDP.opponent.id != controller.id) {
					ShowHotseatHandoff(mSDP.opponent,
						$"The {mSDP.proposer.civilization.noun} propose a deal.",
						"Hear Them Out",
						showProposal);
				} else {
					showProposal();
				}
				break;
			case MsgDealResult mDR:
				if (diplomacy.Visible) {
					diplomacy.OnDealResult(mDR.opponent.id, mDR.accepted);
				} else if (mDR.opponent.isHuman) {
					string answer = mDR.accepted ? "accepted" : "refused";
					popupOverlay.ShowPopup(
						new InformationalPopup($"{mDR.opponent.civilization.leader} {answer} our offer."),
						PopupOverlay.PopupCategory.Advisor);
				}
				break;
			case MsgWithdrawalDemandResult mWDR:
				if (diplomacy.Visible) {
					diplomacy.OnWithdrawalDemandResult(mWDR.opponent.id, mWDR.withdrew);
				} else if (mWDR.withdrew) {
					popupOverlay.ShowPopup(
						new InformationalPopup($"The {mWDR.opponent.civilization.noun} withdrew their units from our territory."),
						PopupOverlay.PopupCategory.Advisor);
				}
				break;
			case MsgCityChanged mCC:
				cityScreen.RefreshCity(mCC.city);
				break;
			case MsgShowUnitedNationsVote mSUNV: {
					List<ChoicePopup.Choice> choices = new();
					foreach (Player candidate in new[] { mSUNV.candidateA, mSUNV.candidateB }) {
						if (candidate != null) {
							choices.Add(new ChoicePopup.Choice($"Vote for {candidate.civilization.leader} of the {candidate.civilization.noun}",
								() => new MsgCastUnitedNationsVote(candidate).send()));
						}
					}
					choices.Add(new ChoicePopup.Choice("Abstain", () => new MsgCastUnitedNationsVote(null).send()));
					popupOverlay.ShowPopup(
						new ChoicePopup("United Nations",
							"The United Nations is electing a Secretary General.\nHow do we vote?",
							choices, cancellable: false),
						PopupOverlay.PopupCategory.Advisor);
					InterestingEvent();
					break;
				}
			case MsgUnitedNationsElectionResult mUNER: {
					string outcome = mUNER.winner == null
					? "No candidate won a majority."
					: $"{mUNER.winner.civilization.leader} of the {mUNER.winner.civilization.noun} has been elected Secretary General!";
					popupOverlay.ShowPopup(
						new ChoicePopup("United Nations",
							$"{mUNER.candidateA.civilization.noun}: {mUNER.votesForA} votes\n"
							+ $"{mUNER.candidateB.civilization.noun}: {mUNER.votesForB} votes\n"
							+ $"Abstaining: {mUNER.abstentions} votes\n{outcome}",
							[new ChoicePopup.Choice("Very well.", () => { })], cancellable: false),
						PopupOverlay.PopupCategory.Advisor);
					InterestingEvent();
					break;
				}
			case MsgEspionageResult mER: {
					// A new embassy shows us their capital once, then the land
					// around it stays on the map as it was.
					bool showCapital = mER.success && mER.mission == EspionageMission.EstablishEmbassy && mER.city != null;
					if (showCapital) {
						mapView.InvalidateMap();
					}
					City capital = mER.city;
					popupOverlay.ShowPopup(
						new ChoicePopup(Espionage.Describe(mER.mission), Embassies.Wrap(mER.message ?? ""),
							[new ChoicePopup.Choice("Very well.", () => {
							if (showCapital) {
								ShowEmbassyCapital(capital);
							}
						})], cancellable: false),
						PopupOverlay.PopupCategory.Advisor);
					break;
				}
			case MsgVictory mV:
				var endMsg =
					$"The {mV.winner.civilization.noun} have won a {mV.victory.Header()} victory!\n"
					+ "This game is over: No further score will be entered.\n\n";

				popupOverlay.ShowPopup(
					new ConfirmationPopup(
						endMsg,
						"Good! I’m Done!",
						"Wait, lemme just play a couple of more turns...",
						() => {
							OnRetire();
						}),
					PopupOverlay.PopupCategory.Advisor);

				InterestingEvent();
				break;
		}
	}

	// The cities waiting to tell their owner they've finished building
	// something, by owner. Cities produce at the end of the round, so in a
	// hotseat game these wait for each player's turn to start. They're shown
	// one at a time, and not while the city screen is open, so a city the
	// player zoomed to gets its production chosen before the next city asks.
	private readonly Dictionary<ID, LinkedList<MsgCityProductionCompleted>> pendingProductionPopups = new();
	private CityProductionPopup shownProductionPopup = null;
	private MsgCityProductionCompleted shownProductionMessage = null;

	private LinkedList<MsgCityProductionCompleted> PendingProductionPopupsFor(ID playerID) {
		if (!pendingProductionPopups.TryGetValue(playerID, out LinkedList<MsgCityProductionCompleted> pending)) {
			pending = new();
			pendingProductionPopups[playerID] = pending;
		}
		return pending;
	}

	private static ID ProductionPopupOwner(MsgCityProductionCompleted msg) {
		return msg.recipient?.id ?? msg.city.owner?.id;
	}

	private void EnqueueProductionPopup(MsgCityProductionCompleted msg) {
		ID ownerID = ProductionPopupOwner(msg);
		if (ownerID == null) {
			return;
		}
		PendingProductionPopupsFor(ownerID).AddLast(msg);
		ShowNextProductionPopup();
	}

	private void ShowNextProductionPopup() {
		// Only once the player's turn is underway, so the news isn't shown
		// to whoever had the screen before them.
		if (CurrentState != GameState.PlayerTurn || hotseatHandoff != null || controller == null) {
			return;
		}
		LinkedList<MsgCityProductionCompleted> pending = PendingProductionPopupsFor(controller.id);
		while (shownProductionPopup == null && !cityScreen.Visible && pending.Count > 0) {
			MsgCityProductionCompleted msg = pending.First.Value;
			pending.RemoveFirst();
			// Found by id, as a LAN client's snapshots replace the cities. It
			// may have been lost or destroyed since.
			City city = EngineStorage.gameData.cities.Find(c => c.id == msg.city.id);
			if (city == null || city.owner?.id != controller.id) {
				continue;
			}
			mapView?.centerCameraOnTile(city.location);
			CityProductionPopup popup = new(city, msg.completed,
				// The next popup waits for the city screen to close.
				onZoom: () => ShowCityScreenForCity(EngineStorage.gameData, city),
				onDone: ShowNextProductionPopup);
			shownProductionPopup = popup;
			shownProductionMessage = msg;
			// However the popup goes away, the next one may follow.
			popup.TreeExiting += () => {
				if (shownProductionPopup == popup) {
					shownProductionPopup = null;
					shownProductionMessage = null;
				}
				Callable.From(ShowNextProductionPopup).CallDeferred();
			};
			popupOverlay.ShowPopup(popup, PopupOverlay.PopupCategory.Advisor);
		}
	}

	// Takes down the production popup on screen (or waiting behind another
	// popup) and keeps its news for its owner's next turn, e.g. when the
	// screen is handed to another hotseat player.
	private void HoldShownProductionPopup() {
		if (shownProductionPopup == null) {
			return;
		}
		CityProductionPopup popup = shownProductionPopup;
		MsgCityProductionCompleted msg = shownProductionMessage;
		shownProductionPopup = null;
		shownProductionMessage = null;
		ID ownerID = ProductionPopupOwner(msg);
		if (ownerID != null) {
			PendingProductionPopupsFor(ownerID).AddFirst(msg);
		}
		if (IsInstanceValid(popup)) {
			popupOverlay.Dismiss(popup);
		}
	}

	private void InterestingEvent() {
		// Break out of fast forward mode after interesting events.
		turnsLeftToFastForward = 0;
	}

	// How long a frame may spend handling the engine's messages to the UI.
	private static readonly long uiMessageBudgetTicks = Stopwatch.Frequency * 4 / 1000;

	public override void _Process(double delta) {
		if (errorOnLoad) {
			return;
		}
		PollLanSession();
		ProcessActions();

		// The engine waits for animations to finish before going on.
		if (!EngineStorage.HasPendingAnimations())
			EngineStorage.ProcessNextMessageToEngine();

		// Handle the waiting messages to the UI, as many as fit in the frame's
		// budget. None of them start animations (those have their own queue),
		// so there's no need to pace them.
		long start = Stopwatch.GetTimestamp();
		bool handledMessage = false;
		while (EngineStorage.TryDequeueNextMessageToUI(out MessageToUI msg)) {
			HandleEngineMessage(msg);
			handledMessage = true;
			if (Stopwatch.GetTimestamp() - start >= uiMessageBudgetTicks) {
				break;
			}
		}
		if (!handledMessage)
			ReplayHeldMessage();
	}

	// Whether the hotseat curtain hides the whole map.
	public bool IsMapHidden => hotseatHandoff != null;

	// Whether the game may change without the UI being told, i.e. while the
	// AI plays, animations run, or the engine has messages to process. The
	// map is redrawn regularly while this is true. A LAN client's game only
	// changes with the host's snapshots and messages, which it is told about.
	// Neither does a LAN host's while it waits for another machine's player:
	// their moves are messages its engine processes, which redraw the map.
	public bool MapMayChangeWithoutNotice =>
		(CurrentState == GameState.ComputerTurn && !LanSession.IsClient && !IsWaitingForRemotePlayer)
		|| EngineStorage.HasPendingAnimations()
		|| EngineStorage.HasPendingMessagesToEngine();

	// Whether another machine's player is taking their turn in a LAN game,
	// while the banner saying so is shown.
	public bool IsWaitingForRemotePlayer => lanWaitingBanner != null;

	// Shows messages that were held for the UI controller while another
	// player had the screen, one popup at a time, once their turn is underway.
	private void ReplayHeldMessage() {
		if (CurrentState != GameState.PlayerTurn || hotseatHandoff != null || popupOverlay.Visible) {
			return;
		}
		if (heldMessages.TryGetValue(controller.id, out Queue<MessageToUI> held) && held.TryDequeue(out MessageToUI msg)) {
			// The map was updated for the message when it arrived.
			mapView?.InvalidateMap();
			ShowEngineMessage(msg);
		}
	}

	// If "location" is not already near the center of the screen, moves the camera to bring it into view.
	public void ensureLocationIsInView(Tile location) {
		if (controller.tileKnowledge.isTileKnown(location) && location != Tile.NONE) {
			Vector2 relativeScreenLocation = mapView.screenLocationOfTile(location, true) / mapView.getVisibleAreaSize();
			if (relativeScreenLocation.DistanceTo(new Vector2((float)0.5, (float)0.5)) > 0.30)
				mapView.centerCameraOnTile(location);
		}
	}

	private void _onEndTurnButtonPressed() {
		if (CurrentState == GameState.PlayerTurn) {
			OnPlayerEndTurn();
		} else {
			log.Information("It's not your turn!");
		}
	}

	private int governmentPromptTurn = -1;

	// If the player can now pick a new government, forces them to do so. On a
	// LAN the choice may not be back from the host yet, so only asks once a
	// turn.
	private void PromptForGovernmentIfDue(GameData gameData) {
		if (controller.government.transitionType && TurnHandling.GetTurnNumber() >= controller.inAnarchyUntilTurn
				&& (!LanSession.IsClient || governmentPromptTurn != gameData.turn)) {
			governmentPromptTurn = gameData.turn;
			popupOverlay.ShowPopup(
				new GovernmentSelection(controller, controller.GetAvailableGovernments(gameData), OnGovernmentSelectionClosed),
				PopupOverlay.PopupCategory.Info);
		}
	}

	// A government selection that went away without a choice (e.g. taken
	// down while the screen changed hands) asks again if it's still this
	// player's turn; otherwise their next turn will.
	private void OnGovernmentSelectionClosed(bool chosen) {
		if (chosen) {
			return;
		}
		governmentPromptTurn = -1;
		Callable.From(() => {
			if (!IsInstanceValid(this) || !IsInsideTree() || CurrentState != GameState.PlayerTurn || hotseatHandoff != null || controller == null) {
				return;
			}
			EngineStorage.ReadGameData(PromptForGovernmentIfDue);
		}).CallDeferred();
	}

	private void OnPlayerStartTurn() {
		EngineStorage.ReadGameData((GameData gameData) => {
			log.Information("Starting player turn");

			PromptForGovernmentIfDue(gameData);

			// If the player can pick a new tech to research, the engine
			// prompts them to do so, naming the tech they just discovered.
			new MsgAskWhatToResearch().send();

			// Allow fast forwarding in observer mode.
			if (gameData.observerMode && turnsLeftToFastForward > 0) {
				--turnsLeftToFastForward;
				new MsgEndTurn().send();
				return;
			}

			CurrentState = GameState.PlayerTurn;
		});

		// Cities that finished building something at the end of the round
		// can tell the player now.
		ShowNextProductionPopup();
		EmitSignal(SignalName.PlayerTurnStart);
	}

	private void OnPlayerEndTurn() {
		if (CurrentState != GameState.PlayerTurn) {
			return;
		}

		// Prompt the user if they would have a city riot when the turn ended.
		bool doEndTurn = true;
		EngineStorage.ReadGameData((GameData gameData) => {
			foreach (City city in controller.cities) {
				if (!controller.isHuman) {
					continue;
				}

				City.Mood cityMood = city.RecalculateCitizenMoods(gameData);
				if (cityMood == City.Mood.Unhappy) {
					popupOverlay.ShowPopup(
						new ConfirmationPopup(
							$"{city.name} will riot! Are you sure?",
							"Yes, let them riot!",
							"No. Maybe you are right, advisor.",
							() => {
								DoActualEndTurn();
							}),
						PopupOverlay.PopupCategory.Advisor);
					doEndTurn = false;
					return;
				}
			}
		});
		if (doEndTurn) {
			DoActualEndTurn();
		}
	}

	private void DoActualEndTurn() {
		log.Information("Ending player turn");
		EmitSignal(SignalName.TurnEnded);
		log.Information("Starting computer turn");
		CurrentState = GameState.ComputerTurn;
		new MsgEndTurn { turn = EngineStorage.gameData.turn }.send(); // Triggers actual backend processing
																	  // Production news the player hasn't seen is out of date. Other
																	  // hotseat players keep theirs for their own turns.
		if (controller != null) {
			pendingProductionPopups.Remove(controller.id);
		}
		EmitSignal(SignalName.PlayerTurnEnd);

		// With simultaneous turns, the next of our players can go on while
		// others are still moving.
		if (SimultaneousLanTurns && controller != null) {
			NoteSimultaneousRound(EngineStorage.gameData);
			simultaneousTurnsEnded.Add(controller.id);
			ContinueSimultaneousTurns();
		}
	}

	// Whether the whole map can be revealed: not in LAN games, where it would show other players what they haven't explored.
	public bool CanRevealWholeMap => !LanSession.IsActive && mapView != null;

	public bool IsWholeMapRevealed => mapView?.revealWholeMap ?? false;

	// Whether every tile is drawn as known: in observer mode, or while the whole map is revealed.
	public bool ShowsWholeMap(GameData gameData) {
		return mapView?.ShowsWholeMap(gameData) ?? gameData.observerMode;
	}

	// Reveals the whole map, or puts the fog of war back. Only this client's drawing changes; the engine's map knowledge is untouched.
	public void OnToggleRevealWholeMap() {
		if (!CanRevealWholeMap) {
			return;
		}
		mapView.revealWholeMap = !mapView.revealWholeMap;
	}

	public void OnRetire() {
		// Quit to main menu, freeing previous scene data
		GetTree().ChangeSceneToFile("res://UIElements/MainMenu/main_menu.tscn");
	}

	public void OnSaveGame() {
		popupOverlay.OnHidePopup(); // hide game menu

		// FileDialog is a Window, not a Control, so we have the popup overlay present a blank control
		popupOverlay.ShowBlank();

		FileDialog.SetDirectoryForSaving();

		// TODO: sound -- see MainMenu.PlayButtonPressedSound();
		FileDialog.Popup();
	}

	public void OnLoadGame() {
		popupOverlay.OnHidePopup(); // hide game menu

		// FileDialog is a Window, not a Control, so we have the popup overlay present a blank control
		popupOverlay.ShowBlank();

		FileDialog.SetDirectoryForLoadingSaves();

		// TODO: sound -- see MainMenu.PlayButtonPressedSound();
		FileDialog.Popup();
	}

	public void OnResolved() {
		popupOverlay.OnHidePopup();
	}

	public void _on_Zoom_value_changed(float value) {
		mapView.setCameraZoomFromMiddle(value);
	}

	public override void _Input(InputEvent @event) {
		if (@event is InputEventKey e && e.Pressed && !e.IsAction(C7Action.UnitGoto)) {
			this.SetGotoMode(false);
		}

		// A unit drag ends when the button is released, even if a control or a modal takes the release before the map sees it. The check
		// is deferred so that a release the map does see finishes the drag first.
		if (draggingUnit && @event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }) {
			Callable.From(AbandonUnitDrag).CallDeferred();
		}
	}

	public override void _UnhandledInput(InputEvent @event) {
		if (errorOnLoad) {
			return;
		}
		// Don't handle if there's an open modal, if it's the AI's turn, or if
		// the screen is being handed to the next hotseat player.
		// A spectator may always look around.
		bool waiting = CurrentState == GameState.ComputerTurn && !LanSession.IsSpectator;
		if ((HasVisibleModal() && !IsModalSwitchEvent(@event)) || waiting || hotseatHandoff != null) {
			IsMovingCamera = false;
			return;
		}

		// Control node must not be in the way and/or have mouse pass enabled
		if (@event is InputEventMouseButton eventMouseButton) {
			HandleMouseButtonInput(eventMouseButton);
		} else if (@event is InputEventMouseMotion eventMouseMotion) {
			HandleMouseMotionInput(eventMouseMotion);
		} else if (@event is InputEventKey eventKeyDown && eventKeyDown.Pressed) {
			HandleKeyboardInput(eventKeyDown);
		} else if (@event is InputEventMagnifyGesture magnifyGesture) {
			HandleMagnifyGesture(magnifyGesture);
		}
	}

	private void HandleMouseButtonInput(InputEventMouseButton eventMouseButton) {
		if (CurrentState == GameState.ComputerTurn && !LanSession.IsSpectator) return;
		if (eventMouseButton.ButtonIndex == MouseButton.Left) {
			HandleLeftMouseButton(eventMouseButton);
		} else if (eventMouseButton.ButtonIndex == MouseButton.Right && !eventMouseButton.IsPressed()) {
			HandleRightMouseButton(eventMouseButton);
		} else if (eventMouseButton.ButtonIndex == MouseButton.WheelUp) {
			AdjustZoom(0.1f);
		} else if (eventMouseButton.ButtonIndex == MouseButton.WheelDown) {
			AdjustZoom(-0.1f);
		}
	}

	private void AdjustZoom(float delta) {
		float newScale = mapView.cameraZoom + delta;
		mapView.setCameraZoom(newScale, GetViewport().GetMousePosition());
		GetViewport().SetInputAsHandled();
	}

	private void HandleLeftMouseButton(InputEventMouseButton eventMouseButton) {
		GetViewport().SetInputAsHandled();
		Control uiHover = GetViewport().GuiGetHoveredControl();
		// Can't drag the map when the mouse is over a ui element
		if (eventMouseButton.IsPressed() && uiHover is not TextureButton) {
			// As in Civ3, pressing on the selected unit and dragging picks
			// where it goes; it moves when the button is released.
			if (TryStartUnitDrag(eventMouseButton)) {
				return;
			}

			OldPosition = eventMouseButton.Position;
			IsMovingCamera = true;

			if (CanDoubleClick(eventMouseButton)) {
				AcceptPossibleDoubleClick(eventMouseButton);
			} else {
				HandleSingleClick(PositionToTile(eventMouseButton.Position));
			}
		} else {
			IsMovingCamera = false;
			if (draggingUnit) {
				FinishUnitDrag(eventMouseButton);
			}
		}
	}

	private bool TryStartUnitDrag(InputEventMouseButton eventMouseButton) {
		draggingUnit = false;
		if (bombardInfo != null || CurrentState != GameState.PlayerTurn || !IsMapUnitValid(CurrentlySelectedUnit)) {
			return false;
		}
		MapUnit unit = CurrentlySelectedUnit;
		if (unit.owner != controller || unit.location != PositionToTile(eventMouseButton.Position)
			|| !unit.GetAvailableActions().Contains(UnitAction.Goto)) {
			return false;
		}

		// Keep moving the whole stack if J was pressed first.
		SetGotoMode(true, gotoInfo?.wholeStack ?? false);
		draggingUnit = true;
		return true;
	}

	private void FinishUnitDrag(InputEventMouseButton eventMouseButton) {
		draggingUnit = false;
		Tile tile = PositionToTile(eventMouseButton.Position);
		bool released = gotoInfo != null && IsMapUnitValid(CurrentlySelectedUnit);
		if (released && Tile.IsTileValid(tile) && tile != CurrentlySelectedUnit.location) {
			gotoInfo = GetGotoInfo(tile);
			ResolveMovement(gotoInfo);
			SetGotoMode(false);
			return;
		}

		// Released where it started: that's an ordinary click on the tile.
		SetGotoMode(false);
		if (!Tile.IsTileValid(tile)) {
			return;
		}
		if (CanDoubleClick(eventMouseButton)) {
			AcceptPossibleDoubleClick(eventMouseButton);
		} else {
			HandleUnitSelectionTileClick(tile);
		}
	}

	// The tile pressed on and where, for a click that may turn out to be
	// the first of a double click. The click is only handled once it's
	// clear that it isn't, by which time the map may have been dragged, so
	// the tile is the one under the mouse when it was pressed.
	private Tile pendingClickTile;
	private Vector2 pendingClickPosition;

	// How far the mouse may move before a press is a drag of the map rather
	// than a click.
	private const float ClickSlop = 8;

	private void AcceptPossibleDoubleClick(InputEventMouseButton eventMouseButton) {
		pendingClickTile = PositionToTile(eventMouseButton.Position);
		pendingClickPosition = eventMouseButton.Position;
		doubleClickHandler.Accept(eventMouseButton);
	}

	// Ends a drag whose release the map didn't see, without moving the unit.
	private void AbandonUnitDrag() {
		if (draggingUnit) {
			SetGotoMode(false);
		}
	}

	private Tile PositionToTile(Vector2 position) {
		Tile tile = null;
		EngineStorage.ReadGameData((GameData gameData) => {
			tile = mapView.tileOnScreenAt(gameData.map, position);
		});
		return tile;
	}

	private bool CanDoubleClick(InputEventMouseButton eventMouseButton) {
		Tile tile = PositionToTile(eventMouseButton.Position);

		return gotoInfo == null && tile?.cityAtTile != null && (tile.cityAtTile.owner == controller || LanSession.IsSpectator);
	}

	// Called by the double click handler once a click turned out to be single.
	private void OnSingleLeftMouseButtonClick(InputEventMouseButton eventMouseButton) {
		Tile tile = pendingClickTile ?? PositionToTile(eventMouseButton.Position);
		pendingClickTile = null;
		HandleSingleClick(tile);
	}

	private void HandleSingleClick(Tile tile) {
		if (gotoInfo != null) {
			this.ResolveMovement(gotoInfo);
			this.SetGotoMode(false);
		} else if (bombardInfo != null) {
			if (Tile.IsTileValid(tile) && bombardInfo.bombardingUnit.CanBombardTile(tile, out var bombardTarget)) {
				bombardInfo.bombardTarget = bombardTarget;
				HandleBombardClick(bombardInfo, tile);
			}
			setBombard(null);
		} else {
			// Select unit on tile at mouse location
			HandleUnitSelectionTileClick(tile);
		}
	}

	private void OnDoubleLeftMouseButtonClick(InputEventMouseButton eventMouseButton) {
		Tile tile = pendingClickTile ?? PositionToTile(eventMouseButton.Position);
		pendingClickTile = null;
		if (tile?.cityAtTile != null && LanSession.IsSpectator && LanSession.SpectatorMayLookAs(tile.cityAtTile.owner)) {
			// A spectator looks at the game as the city's owner, so the city
			// screen and the advisors show that civilization; the map stays
			// as the spectator watches it.
			controller = tile.cityAtTile.owner;
			EngineStorage.uiControllerID = controller.id;
			LanSession.ShowSpectatorView(EngineStorage.gameData);
			mapView.InvalidateMap();
		}
		if (tile?.cityAtTile?.owner == controller) {
			EngineStorage.ReadGameData((GameData gameData) => {
				ShowCityScreenForCity(gameData, tile.cityAtTile);
			});
		}
	}

	private void HandleUnitSelectionTileClick(Tile tile) {
		if (!Tile.IsTileValid(tile)) {
			return;
		}

		// Pick a top unit, never one carried by an army or a ship: the selected
		// unit if it's here, otherwise one that can still move.
		MapUnit unit = null;
		foreach (MapUnit u in tile.unitsOnTile) {
			if (u.owner != controller || u.IsLoaded()) {
				continue;
			}
			if (u == CurrentlySelectedUnit) {
				unit = u;
				break;
			}
			if (unit == null || (!unit.movementPoints.canMove && u.movementPoints.canMove)) {
				unit = u;
			}
		}
		if (unit == null) {
			return;
		}

		SelectUnit(unit, tile);
	}

	private void SelectUnit(MapUnit unit, Tile tile) {
		bool canMove = unitSelector.SetSelectedUnit(unit);

		if (unit.WorkerJob != null) {
			return;
		}

		if (!canMove && Tile.IsTileValid(tile)) {
			new MsgShowTemporaryPopup("This unit has already moved.", tile).send();
		}
	}

	public void SelectUnit(MapUnit unit) {
		SelectUnit(unit, unit.location);
	}

	private void HandleRightMouseButton(InputEventMouseButton eventMouseButton) {
		this.SetGotoMode(false);

		Tile tile = PositionToTile(eventMouseButton.Position);
		if (Tile.IsTileValid(tile)) {
			HandleRightClickOnTile(tile, eventMouseButton);
		} else {
			log.Debug("Didn't click on any tile");
		}
	}

	private void HandleRightClickOnTile(Tile tile, InputEventMouseButton eventMouseButton) {
		bool shiftDown = Input.IsKeyPressed(Godot.Key.Shift);

		var activeTile = controller.tileKnowledge.isActiveTile(tile);

		if (bombardInfo != null)
			setBombard(null);
		// Handle the shortcut of shift+right clicking a city to get the change production menu.
		else if (shiftDown && activeTile && tile.cityAtTile?.owner == controller)
			new RightClickChooseProductionMenu(this, tile.cityAtTile).Open(eventMouseButton.Position);
		else if (!shiftDown && activeTile && tile.unitsOnTile.Count > 0)
			// There are units on this title, so open that menu.
			new RightClickTileMenu(this, tile).Open(eventMouseButton.Position);
		else if (!shiftDown && activeTile && tile.cityAtTile?.owner == controller)
			// There are no units, but this is the player's city.
			new RightClickCityMenu(this, tile).Open(eventMouseButton.Position);
		else
			ShowTileInfo(tile);

		LogTileDetails(tile);
	}

	// Lists the diplomatic and espionage missions the player at the screen
	// could send against a foreign civ and one of its cities, with their
	// cost and chance of success.
	public void ShowEspionageMissions(Player target, City city) {
		List<ChoicePopup.Choice> choices = new();
		EngineStorage.ReadGameData((GameData gameData) => {
			choices = Embassies.MissionChoices(gameData, controller, target, city);
		});
		string title = city == null ? $"Missions to the {target.civilization.noun}" : $"Missions to {city.name}";
		popupOverlay.ShowPopup(new ChoicePopup(title, $"Treasury: {controller.gold} gold", choices),
			PopupOverlay.PopupCategory.Advisor);
	}

	public void ShowTileInfo(Tile tile) {
		tileInfo = new TileInfo(tile);
		// The fog and cities around the tile are drawn differently under the box.
		mapView.InvalidateMap();
		var zoom = mapView.cameraZoom;
		var tileCenter = mapView.screenLocationOfTile(tile, true);
		var tileInfoPopup = new TileInfoPopup(this, tile, tileCenter, zoom);
		popupOverlay.ShowPopup(tileInfoPopup, PopupOverlay.PopupCategory.TileInfo);
	}

	public void HideTileInfo() {
		tileInfo = null;
		mapView.InvalidateMap();
		popupOverlay.OnHidePopup();
	}

	private void LogTileDetails(Tile tile) {
		string yield = tile.YieldString(controller);
		log.Debug($"({tile.XCoordinate}, {tile.YCoordinate}): {tile.overlayTerrainType.DisplayName} {yield}");

		if (tile.cityAtTile != null) {
			LogCityDetails(tile.cityAtTile);
		}

		if (tile.unitsOnTile.Count > 0) {
			LogUnitDetails(tile.unitsOnTile);
		}
	}

	private void LogCityDetails(City city) {
		log.Debug($"  {city.name}, production {city.shieldsStored} of {city.owner.ShieldCost(city.itemBeingProduced)}");
		foreach (CityResident resident in city.residents) {
			log.Debug($"  Resident working at {resident.tileWorked}");
		}
	}

	private void LogUnitDetails(List<MapUnit> unitsOnTile) {
		foreach (MapUnit unit in unitsOnTile) {
			log.Debug("  Unit on tile: " + unit);
			if (unit.currentAI != null) {
				log.Debug("  Strategy: " + unit.currentAI.SummarizePlan());
			}
		}
	}

	private void HandleMouseMotionInput(InputEventMouseMotion eventMouseMotion) {
		if (IsMovingCamera) {
			GetViewport().SetInputAsHandled();
			// Dragging the map isn't clicking on it.
			if (pendingClickTile != null && eventMouseMotion.Position.DistanceTo(pendingClickPosition) > ClickSlop) {
				pendingClickTile = null;
				doubleClickHandler.Cancel();
			}
			mapView.cameraLocation += OldPosition - eventMouseMotion.Position;
			OldPosition = eventMouseMotion.Position;
		} else if (gotoInfo != null) {
			gotoInfo = GetGotoInfo(eventMouseMotion.Position);
		} else if (bombardInfo != null) {
			Tile tile = PositionToTile(eventMouseMotion.Position);
			bombardInfo.mouseTile = Tile.IsTileValid(tile) ? tile : null;
		}
	}

	private void HandleKeyboardInput(InputEventKey eventKeyDown) {
		if (eventKeyDown.Keycode == Godot.Key.O && eventKeyDown.ShiftPressed && eventKeyDown.IsCommandOrControlPressed() && eventKeyDown.AltPressed) {
			if (!LanSession.IsActive) {
				ToggleObserverMode();
			}
		}

		if (eventKeyDown.Keycode == Godot.Key.F1) {
			EmitSignal(SignalName.ShowSpecificAdvisor, C7Action.ShowDomesticAdvisor);
		}
		if (eventKeyDown.Keycode == Godot.Key.F2) {
			EmitSignal(SignalName.ShowSpecificAdvisor, C7Action.ShowTradeAdvisor);
		}
		if (eventKeyDown.Keycode == Godot.Key.F3) {
			EmitSignal(SignalName.ShowSpecificAdvisor, C7Action.ShowMilitaryAdvisor);
		}
		if (eventKeyDown.Keycode == Godot.Key.F4) {
			EmitSignal(SignalName.ShowSpecificAdvisor, C7Action.ShowForeignAdvisor);
		}
		if (eventKeyDown.Keycode == Godot.Key.F5) {
			EmitSignal(SignalName.ShowSpecificAdvisor, C7Action.ShowCulturalAdvisor);
		}
		if (eventKeyDown.Keycode == Godot.Key.F6) {
			EmitSignal(SignalName.ShowSpecificAdvisor, C7Action.ShowScienceAdvisor);
		}

		if (eventKeyDown.Keycode == Godot.Key.F7) {
			EmitSignal(SignalName.ShowGameView, C7Action.ShowWondersView);
		}
		if (eventKeyDown.Keycode == Godot.Key.F8) {
			EmitSignal(SignalName.ShowGameView, C7Action.ShowVictoryStatusView);
		}
		if (eventKeyDown.Keycode == Godot.Key.F9) {
			EmitSignal(SignalName.ShowGameView, C7Action.ShowPalaceView);
		}
		if (eventKeyDown.Keycode == Godot.Key.F10) {
			EmitSignal(SignalName.ShowGameView, C7Action.ShowSpaceRaceView);
		}
		if (eventKeyDown.Keycode == Godot.Key.F11) {
			EmitSignal(SignalName.ShowGameView, C7Action.ShowDemographicsView);
		}

		// The same as the diplomacy button.
		if (eventKeyDown.Keycode == Godot.Key.D
			&& eventKeyDown.IsCommandOrControlPressed()
			&& eventKeyDown.ShiftPressed
			&& !eventKeyDown.AltPressed
			&& !eventKeyDown.Echo) {
			statusMenu.OpenDiplomacyPopup();
		}

		if (eventKeyDown.Keycode == Godot.Key.C && HasCurrentlySelectedUnit()) {
			mapView.centerCameraOnTile(CurrentlySelectedUnit.location);
		}
		if (eventKeyDown.Keycode == Godot.Key.H) {
			City capital = controller.cities.Find(c => c.IsCapital());
			if (capital != null) {
				mapView.centerCameraOnTile(capital.location);
			}
		}
		// For inputs that have the same keys mapped to multiple actions like G
		// we need to manually handle what is triggered by adding extra conditions.
		// Otherwise when pressing CTRL + G for example, both the go-to
		// and the toggle grid actions are triggered, because godot does not distinguish
		// single key presses from combos, it sends both signals.
		// Sometimes even worse, when pressing CTRL + G, it only sends the go-to signal.
		// We continue to map these to an action and not call them directly,
		// because we could add a button in the ui that does the same and this would call the action too.
		if (eventKeyDown.Keycode == Godot.Key.G) {
			// Toggle Coordinates
			if (eventKeyDown.IsCommandOrControlPressed()
				&& eventKeyDown.ShiftPressed
				&& eventKeyDown.AltPressed) {
				ProcessAction(C7Action.ToggleCoordinates);
			}
			// Toggle Grid
			else if (eventKeyDown.IsCommandOrControlPressed()) {
				ProcessAction(C7Action.ToggleGrid);
			}
			// Trigger Unit go-to
			else if (!eventKeyDown.IsCommandOrControlPressed()
					   && !eventKeyDown.ShiftPressed
					   && !eventKeyDown.AltPressed) {
				ProcessAction(C7Action.UnitGoto);
			}
		}

		// Go-to for every unit on the selected unit's tile.
		if (eventKeyDown.Keycode == Godot.Key.J
			&& !eventKeyDown.IsCommandOrControlPressed()
			&& !eventKeyDown.ShiftPressed
			&& !eventKeyDown.AltPressed
			&& CurrentState == GameState.PlayerTurn
			&& IsMapUnitValid(CurrentlySelectedUnit)) {
			SetGotoMode(true, wholeStack: true);
		}

		// Show only the terrain and resources, hiding cities, units, roads and the like.
		if (eventKeyDown.Keycode == Godot.Key.M
			&& eventKeyDown.IsCommandOrControlPressed()
			&& eventKeyDown.ShiftPressed
			&& !eventKeyDown.AltPressed
			&& !eventKeyDown.Echo) {
			mapView.bareMap = !mapView.bareMap;
		}
	}

	private void ToggleObserverMode() {
		EngineStorage.ReadGameData((GameData gameData) => {
			gameData.observerMode = !gameData.observerMode;
			if (gameData.observerMode) {
				SetObserverModeOn(gameData);
			} else {
				SetObserverModeOff(gameData);
			}
		});
		// Observers see the whole map.
		mapView.InvalidateMap();
	}

	// The human players to restore when observer mode is turned off.
	private HashSet<ID> observerModeHumans = new();

	private void SetObserverModeOn(GameData gameData) {
		observerModeHumans = gameData.players.Where(p => p.isHuman).Select(p => p.id).ToHashSet();
		foreach (Player player in gameData.players) {
			player.isHuman = false;
		}
		animationController.SetAnimationsEnabled(false);
		popupOverlay.ShowPopup(
			new TextDialog("How many turns to fast forward through?",
							"Turns: ", "100",
							BoxContainer.AlignmentMode.Begin,
							(string turns) => {
								// Ignore what isn't a number of turns rather than throwing.
								if (int.TryParse(turns, out int n) && n >= 0) {
									turnsLeftToFastForward = n;
								}
							}),
				PopupOverlay.PopupCategory.Advisor);
	}

	private void SetObserverModeOff(GameData gameData) {
		foreach (Player player in gameData.players) {
			if (player.id == EngineStorage.uiControllerID || observerModeHumans.Contains(player.id)) {
				player.isHuman = true;
			}
		}
	}

	private void ToggleGridCoordinates() {
		EngineStorage.ReadGameData((GameData gameData) => {
			gameData.showGridCoordinates = !gameData.showGridCoordinates;
		});
		mapView.InvalidateMap();
	}

	private void HandleMagnifyGesture(InputEventMagnifyGesture magnifyGesture) {
		double newScale = mapView.cameraZoom * magnifyGesture.Factor;

		mapView.setCameraZoom((float)newScale, magnifyGesture.Position);
	}

	// The input actions and their names, fetched once rather than every frame.
	private StringName[] inputActions;
	private string[] inputActionNames;

	private void ProcessActions() {
		if (inputActions == null) {
			Godot.Collections.Array<StringName> actions = InputMap.GetActions();
			inputActions = new StringName[actions.Count];
			inputActionNames = new string[actions.Count];
			for (int i = 0; i < actions.Count; i++) {
				inputActions[i] = actions[i];
				inputActionNames[i] = actions[i].ToString();
			}
		}

		// Keys typed into a text field are text, not commands.
		Control focused = GetViewport().GuiGetFocusOwner();
		bool typing = focused is LineEdit or TextEdit && focused.IsVisibleInTree();

		for (int i = 0; i < inputActions.Length; i++) {
			// Match modifiers exactly, so that Shift+Enter or Ctrl+L don't also
			// trigger the actions bound to plain Enter or L.
			if (!typing && Input.IsActionJustPressed(inputActions[i], exactMatch: true)) {
				ProcessAction(inputActionNames[i]);
			} else if (Input.IsActionJustReleased(inputActions[i])) {
				ProcessOnReleaseAction(inputActionNames[i]);
			}
		}
	}

	private bool tempAnimationsFlipped = false;

	private void ProcessOnReleaseAction(string currentAction) {
		if (currentAction == C7Action.EnableTempAnimations && tempAnimationsFlipped) {
			tempAnimationsFlipped = false;
			animationController.ToggleAnimationsEnabled();
		}
	}

	private bool HasVisibleModal() {
		if (popupOverlay.Visible || cityScreen.Visible || diplomacy.Visible || RightClickMenu.IsAnyOpen)
			return true;

		if (advisor.Visible || gameViews.Visible)
			return true;

		return false;
	}

	private bool IsModalSwitchEvent(InputEvent @event) {
		if (@event is InputEventKey eventKeyDown && eventKeyDown.Pressed) {
			return eventKeyDown.Keycode is >= Key.F1 and <= Key.F11;
		}

		return false;
	}

	private void ProcessAction(string currentAction) {
		// Nothing happens behind the hotseat curtain.
		if (hotseatHandoff != null) {
			return;
		}

		// An open right-click menu takes the keys; Escape closes it.
		if (RightClickMenu.IsAnyOpen) {
			return;
		}

		if (currentAction == C7Action.Escape && tileInfo != null) {
			HideTileInfo();
			return;
		}

		if (currentAction == C7Action.Escape && popupOverlay.ShowingPopup) {
			popupOverlay.OnEscape();
			return;
		}

		if (currentAction == C7Action.Escape && cityScreen.Visible) {
			cityScreen.Hide();
			return;
		}

		if (currentAction == C7Action.Escape && advisor.Visible) {
			advisor.Hide();
			return;
		}

		if (currentAction == C7Action.Escape && gameViews.Visible) {
			gameViews.Hide();
			return;
		}

		if (currentAction == C7Action.Escape && diplomacy.Visible) {
			diplomacy.Hide();
			return;
		}

		if (currentAction == C7Action.Escape && bombardInfo != null) {
			setBombard(null);
			return;
		}

		// never poll for actions if UI elements are visible
		if (HasVisibleModal()) {
			return;
		}

		if (currentAction == C7Action.EndTurn && !this.HasCurrentlySelectedUnit()) {
			log.Verbose("end_turn key pressed");
			this.OnPlayerEndTurn();
		}

		if (currentAction == C7Action.EndTurnNow) {
			log.Verbose("end_turn_now key pressed");
			this.OnPlayerEndTurn();
		}

		if (this.HasCurrentlySelectedUnit()) {
			TileDirection? dir = C7Action.ToTileDirection(currentAction);

			if (dir.HasValue) {
				this.gotoInfo = GetGotoInfo(CurrentlySelectedUnit.location.GetTileAtNeighborIndex((int)(dir) + 1));
				this.ResolveMovement(this.gotoInfo);
				this.SetGotoMode(false);
			}
		}

		if (currentAction == C7Action.ToggleGrid) {
			this.mapView.gridLayer.visible = !this.mapView.gridLayer.visible;
		}

		if (currentAction == C7Action.ToggleCoordinates) {
			ToggleGridCoordinates();
		}

		if (currentAction == C7Action.Escape && this.gotoInfo == null) {
			log.Debug("Got request for escape/quit");
			popupOverlay.ShowPopup(new EscapeQuitPopup(), PopupOverlay.PopupCategory.Info);
		}

		if (currentAction == C7Action.ToggleZoom) {
			if (mapView.cameraZoom != 1) {
				mapView.setCameraZoomFromMiddle(1.0f);
			} else {
				mapView.setCameraZoomFromMiddle(0.5f);
			}
		}

		if (currentAction == C7Action.ToggleAnimations) {
			animationController.ToggleAnimationsEnabled();
		}

		// Holding the key flips animations until it's released.
		if (currentAction == C7Action.EnableTempAnimations && !tempAnimationsFlipped) {
			tempAnimationsFlipped = true;
			animationController.ToggleAnimationsEnabled();
		}

		// actions with unit buttons, which are only relevant during the player
		// turn.
		if (CurrentState != GameState.PlayerTurn) {
			return;
		}

		if (currentAction == C7Action.SaveGame) {
			OnSaveGame();
			return;
		}

		if (currentAction == C7Action.LoadGame) {
			OnLoadGame();
			return;
		}

		if (!IsMapUnitValid(CurrentlySelectedUnit)) return;

		if (currentAction == C7Action.UnitHold) {
			new MsgUnitCommand(CurrentlySelectedUnit.id, MsgUnitCommand.Command.SkipTurn).send();
		}

		if (currentAction == C7Action.UnitWait) {
			UnitInteractions.waitUnit(CurrentlySelectedUnit.id);
			unitSelector.SetNextUnit();
		}

		if (currentAction == C7Action.UnitFortify) {
			new MsgSetFortification(CurrentlySelectedUnit.id, true).send();
		}

		if (currentAction == C7Action.UnitDisband) {
			// The unit asked about, not whichever is selected once confirmed.
			ID disbandedID = CurrentlySelectedUnit.id;
			popupOverlay.ShowPopup(
				new ConfirmationPopup(
					$"Disband {CurrentlySelectedUnit.name}? Pardon me but these are OUR people.\nDo you really want to disband them?",
					"Yes, we need to!",
					"No. Maybe you are right, advisor.",
					() => {
						new MsgUnitCommand(disbandedID, MsgUnitCommand.Command.Disband).send();
					}),
				PopupOverlay.PopupCategory.Advisor);
		}

		// unit_goto's behavior is more complicated than other actions - it
		// toggles the go to state, but must be detoggled in _*Input methods if
		// it is not the input being pressed.
		if (currentAction == C7Action.UnitGoto) {
			this.SetGotoMode(true);
		}

		if (currentAction == C7Action.UnitExplore) {
			new MsgUnitCommand(CurrentlySelectedUnit.id, MsgUnitCommand.Command.Explore).send();
		}

		if (currentAction == C7Action.UnitAutomate) {
			new MsgUnitCommand(CurrentlySelectedUnit.id, MsgUnitCommand.Command.Automate).send();
		}

		if (currentAction == C7Action.UnitSentry) {
			new MsgSentry(CurrentlySelectedUnit.id, enemyOnly: false).send();
		}

		if (currentAction == C7Action.UnitSentryEnemyOnly) {
			new MsgSentry(CurrentlySelectedUnit.id, enemyOnly: true).send();
		}

		if (currentAction == C7Action.UnitBuildCity && CurrentlySelectedUnit.canBuildCity()) {
			EngineStorage.ReadGameData((GameData gameData) => {
				MapUnit currentUnit = gameData.GetUnit(CurrentlySelectedUnit.id);
				log.Debug(currentUnit.Describe());
				if (currentUnit.canBuildCity()) {
					popupOverlay.ShowPopup(new BuildCityDialog(controller.GetNextCityName()),
						PopupOverlay.PopupCategory.Advisor);
				}
			});
		}

		if (currentAction == C7Action.UnitBombard) {
			if (CurrentlySelectedUnit.HasBombardAbility()) {
				EngineStorage.ReadGameData((GameData gameData) => {
					MapUnit currentUnit = gameData.GetUnit(CurrentlySelectedUnit.id);
					setBombard(currentUnit);
				});
			}
		}


		if (currentAction == C7Action.UnitPillage && CurrentlySelectedUnit.CanPillage()) {
			new MsgPillage(CurrentlySelectedUnit.id).send();
		}

		if (currentAction == C7Action.UnitBuildArmy && CurrentlySelectedUnit.CanFormArmy()) {
			new MsgUnitCommand(CurrentlySelectedUnit.id, MsgUnitCommand.Command.FormArmy).send();
		}

		if (currentAction == C7Action.UnitHurryBuilding && CurrentlySelectedUnit.CanOfferHurryProduction()) {
			string blocker = CurrentlySelectedUnit.HurryProductionBlocker();
			if (blocker == null) {
				new MsgUnitCommand(CurrentlySelectedUnit.id, MsgUnitCommand.Command.HurryProduction).send();
			} else {
				popupOverlay.ShowPopup(new InformationalPopup(blocker, AdvisorHead.Advisor.Military, AdvisorHead.Mood.Angry),
					PopupOverlay.PopupCategory.Advisor);
			}
		}

		if (currentAction == C7Action.UnitLoad) {
			// TODO: Which transport?
			new MsgLoadToTransport(CurrentlySelectedUnit.id).send();
		}
		// Upgrading and unloading share a key; upgrading wins when possible.
		if (currentAction == C7Action.UnitUpgrade) {
			ConfirmUpgrade(CurrentlySelectedUnit);
		}
		if (currentAction == C7Action.UnitUnload && CurrentlySelectedUnit.GetAvailableUpgrade() == null) {
			new MsgUnloadTransport(CurrentlySelectedUnit.id).send();
		}

		Terraform terraform = C7Action.ToTerraform(currentAction);

		// the `r` key is mapped to the 'Build Road' action
		// if there is a road we want to map this to 'Build Railroad' action
		if (CurrentlySelectedUnit.location.HasRoad()
			&& !CurrentlySelectedUnit.location.HasRailroad()
			&& currentAction == C7Action.UnitBuildRoad) {
			terraform = C7Action.ToTerraform(C7Action.UnitBuildRailroad);
		}

		if (terraform == null || !CurrentlySelectedUnit.CanPerformTerraformAction(terraform))
			return;

		TerrainImprovement replacementTarget = CurrentlySelectedUnit.location.overlays.GetReplacementTarget(terraform);
		if (replacementTarget != null) {
			ID workerID = CurrentlySelectedUnit.id;
			popupOverlay.ShowPopup(
				new ConfirmationPopup(
					$"A previous terrain enhancement ({replacementTarget.key.Capitalize()}) will be replaced \nby this operation. Do you wish to continue?",
					"Continue.",
					"Cancel action.",
					() => {
						new MsgStartWorkerJob(workerID, terraform).send();
					}),
				PopupOverlay.PopupCategory.Advisor);
			return;
		}
		new MsgStartWorkerJob(CurrentlySelectedUnit.id, terraform).send();
	}

	private void ConfirmUpgrade(MapUnit unit) {
		UnitPrototype upgrade = unit.GetAvailableUpgrade();
		if (upgrade == null) {
			return;
		}

		int cost = unit.UpgradeCost(upgrade);
		if (unit.owner.gold < cost) {
			popupOverlay.ShowPopup(
				new InformationalPopup(
					$"Upgrading our {unit.name} to {upgrade.name} would cost {cost} gold.\nWe only have {unit.owner.gold}.",
					AdvisorHead.Advisor.Domestic, AdvisorHead.Mood.Surprised),
				PopupOverlay.PopupCategory.Advisor);
			return;
		}

		popupOverlay.ShowPopup(
			new ConfirmationPopup(
				$"Upgrade our {unit.name} to {upgrade.name} for {cost} gold?",
				"Yes, upgrade it.",
				"No, not now.",
				() => { new MsgUpgradeUnit(unit.id).send(); }),
			PopupOverlay.PopupCategory.Advisor);
	}

	// Whether the player is dragging the selected unit to pick its destination.
	private bool draggingUnit = false;

	private void SetGotoMode(bool isOn, bool wholeStack = false) {
		this.lastTile = null;
		if (isOn) {
			this.gotoInfo = new() { wholeStack = wholeStack };
		} else {
			this.gotoInfo = null;
			this.draggingUnit = false;
		}
	}

	private void setBombard(MapUnit bombardingUnit) {
		bombardInfo = bombardingUnit == null ? null : new BombardInfo(bombardingUnit);
		if (bombardingUnit == null)
			Input.SetCustomMouseCursor(null);
	}

	private void ResolveMovement(GotoInfo info) {
		if (info == null) {
			return;
		}

		log.Debug($"Resolve movement intent: {info.intent}");

		if (info.intent == Intent.NoticeUnit) {
			new MsgShowTemporaryPopup($"Non-combat units may not attack.", info.destinationTile).send();
			return;
		}
		if (info.intent == Intent.NoticeCity) {
			new MsgShowTemporaryPopup($"Only combat units can capture cities and improvements.", info.destinationTile).send();
			return;
		}
		if (info.intent == Intent.NoticeAlliance) {
			new MsgShowTemporaryPopup($"No aggression against alliance.", info.destinationTile).send();
			return;
		}

		if (info.moveCost == -1) {
			return;
		}

		EngineStorage.ReadGameData((GameData gameData) => {
			// If this move would require declaring war, display a popup that checks
			// if the player really wants to declare war. If they do, declare the
			// war for them, clear out the player, and call this method again.
			if (info.requiresWarDeclarationOnPlayer != null) {
				GotoInfo stashed = info;
				stashed.unitID ??= CurrentlySelectedUnit.id;
				this.MaybeDeclareWar(stashed.requiresWarDeclarationOnPlayer, gameData.turn, () => {
					stashed.requiresWarDeclarationOnPlayer = null;
					this.ResolveMovement(stashed);
					this.SetGotoMode(false);
				});
			} else if (info.wholeStack) {
				MoveStack(gameData, info);
			} else {
				new MsgSetUnitPath(info.unitID ?? CurrentlySelectedUnit.id, info.path).send();
			}
		});
	}

	// Sends the selected unit along its path and every other unit of ours on
	// its tile to the same destination, each along its own path. Only the
	// selected unit attacks; the rest go only where they can walk.
	private void MoveStack(GameData gameData, GotoInfo info) {
		MapUnit leader = gameData.GetUnit(info.unitID ?? CurrentlySelectedUnit.id);
		if (leader == null) {
			return;
		}
		Tile destination = info.destinationTile;
		List<(ID, TilePath)> moves = [(leader.id, info.path)];
		foreach (MapUnit unit in leader.location.unitsOnTile) {
			// Loaded units ride along with their transport. Fortified units
			// stay put, as do units that have already moved this turn.
			if (unit == leader || unit.owner != leader.owner || unit.IsLoaded()
				|| unit.isFortified
				|| unit.movementPoints.remaining < unit.MaxMovementPoints()
				|| !unit.GetAvailableActions().Contains(UnitAction.Goto)) {
				continue;
			}
			TilePath path = PathingAlgorithmChooser.GetAlgorithm(unit).PathFrom(unit.location, destination, unit);
			if (path != null && path.PathLength() > 0) {
				moves.Add((unit.id, path));
			}
		}

		// The paths are all worked out before anything moves, since moving
		// changes who's on the tile.
		foreach ((ID id, TilePath path) in moves) {
			new MsgSetUnitPath(id, path).send();
		}
	}

	private void MaybeDeclareWar(Player player, int currentTurn, Action callback) {
		popupOverlay.ShowPopup(new WarConfirmation(player,
			() => {
				new MsgDeclareWar(player).send();
				callback();
			}), PopupOverlay.PopupCategory.Advisor);
	}

	// Asks to declare war on each of the players from the index on, one after
	// another, and calls back once all of them are confirmed. Declining any
	// of them cancels the rest.
	private void ConfirmWarDeclarations(List<Player> players, int index, int currentTurn, Action callback) {
		if (index >= players.Count) {
			callback();
			return;
		}
		MaybeDeclareWar(players[index], currentTurn, () => {
			ConfirmWarDeclarations(players, index + 1, currentTurn, callback);
		});
	}

	private Tile lastTile = null;
	private GotoInfo GetGotoInfo(Vector2 mousePos) {
		GotoInfo result = new();

		// We're in "goto" mode and moved the mouse over a tile.
		//
		// Figure out which tile it was.
		EngineStorage.ReadGameData((GameData gameData) => {
			Tile tile = mapView.tileOnScreenAt(gameData.map, mousePos);
			result = GetGotoInfo(tile);
		});

		return result;
	}

	private GotoInfo GetGotoInfo(Tile tile) {
		GotoInfo result = new() { wholeStack = this.gotoInfo?.wholeStack ?? false };

		EngineStorage.ReadGameData((GameData gameData) => {
			if (tile == this.lastTile && this.gotoInfo != null) {
				result = this.gotoInfo;
				return;
			}
			this.lastTile = result.destinationTile = tile;

			// Figure out what unit is in goto mode. If the tile we're hovering over is
			// different than the tile the unit is on, calculate the path to move there.
			MapUnit unit = !Tile.IsTileValid(tile) || !IsMapUnitValid(CurrentlySelectedUnit) ? null : gameData.GetUnit(CurrentlySelectedUnit.id);

			// Units like the Bomber don't have a go-to action
			if (unit != null && !unit.GetAvailableActions().Contains(UnitAction.Goto)) {
				result = null;
			} else if (unit != null && unit.location != tile) {
				result.path = PathingAlgorithmChooser.GetAlgorithm(unit).PathFrom(unit.location, tile, unit);
				result.moveCost =
					result.path.PathCost(unit.owner, unit.location, unit.MaxMovementPoints(), unit.movementPoints.remaining);
				result.pathCoords = result.path.GetPathCoords();

				// If we couldn't path onto the tile, but the tile is next to us and
				// we could enter the tile if combat is allowed (or if we could
				// declare war with the move) mark the path.
				var distanceToTile = unit.location.DistanceTo(tile);
				var canEnterForcefully = unit.CanEnterForcefully(tile, out Intent intent);
				result.intent = intent;

				if (distanceToTile == 1 && canEnterForcefully) {
					Queue<Tile> pathQueue = new();
					pathQueue.Enqueue(tile);

					result.path = new TilePath(tile, pathQueue);
					result.moveCost = result.path.PathCost(unit.owner, unit.location, unit.MaxMovementPoints(),
						unit.movementPoints.remaining);
					result.pathCoords = result.path.GetPathCoords();

					// If we couldn't enter this tile without a war declaration,
					// record which civ we need to declare war on.
					if (intent == Intent.WarDeclaration) {
						result.attackingMove = true;
						if (tile.cityAtTile != null) {
							result.requiresWarDeclarationOnPlayer = tile.cityAtTile.owner;
						} else {
							result.requiresWarDeclarationOnPlayer = tile.unitsOnTile[0].owner;
						}
					}
				}
			} else {
				// Hide the goto cursor, we don't have a valid move.
				result.moveCost = -1;
			}
		});

		return result;
	}

	private void HandleBombardClick(BombardInfo info, Tile tile) {
		if (info == null || !Tile.IsTileValid(tile)) {
			return;
		}
		// The unit bombarding, not whichever is selected once war is declared.
		ID bombarderID = info.bombardingUnit.id;

		EngineStorage.ReadGameData((GameData gameData) => {
			// A nuke goes to war with every civ it hits, not only the target
			// tile's owner, so ask about each of them in turn.
			if (info.bombardingUnit.IsNuclearWeapon()) {
				List<Player> wars = info.bombardTarget == MapUnit.BombardTarget.None
					? []
					: info.bombardingUnit.NuclearStrikeWarDeclarations(tile);
				ConfirmWarDeclarations(wars, 0, gameData.turn, () => {
					new MsgBombard(bombarderID, tile).send();
				});
				return;
			}

			if (info.RequiresWarDeclaration(tile, out var player)) {
				MaybeDeclareWar(player, gameData.turn, () => {
					new MsgBombard(bombarderID, tile).send();
				});
			} else {
				new MsgBombard(bombarderID, tile).send();
			}
		});
	}

	/**
	 * User quit.  We *may* want to do some things here like make a back-up save, or call the server and let it know we're bailing (esp. in MP).
	 **/
	private void OnQuitTheGame() {
		log.Information("Goodbye!");
		GetTree().Quit();
	}

	private void OnBuildCity(string name) {
		if (IsMapUnitValid(CurrentlySelectedUnit))
			new MsgBuildCity(CurrentlySelectedUnit, name).send();
	}

	// Shows the capital a new embassy reports on, with the tiles around it as
	// they are now, until the city screen is closed.
	private void ShowEmbassyCapital(City capital) {
		Player viewer = controller;
		viewer.tileKnowledge.Peek(Espionage.CityRadius(capital));
		mapView.InvalidateMap();
		EngineStorage.ReadGameData((GameData gameData) => {
			cityScreen.ShowForeignCity(gameData, capital, () => {
				viewer.tileKnowledge.EndPeek();
				mapView.InvalidateMap();
			}, (GameData newGameData, City newCapital) => {
				// A LAN snapshot replaced the players and their knowledge,
				// so look again with the new ones.
				Player newViewer = newGameData.GetPlayer(viewer.id);
				if (newViewer == null) {
					return;
				}
				viewer = newViewer;
				viewer.tileKnowledge.Peek(Espionage.CityRadius(newCapital));
				mapView.InvalidateMap();
			});
		});
	}

	public void ShowCityScreenForCity(GameData gameData, City city) {
		city.RecalculateCitizenMoods(gameData);
		EmitSignal(SignalName.ShowCityScreen, new ParameterWrapper<City>(city));
	}

	public void OnDiplomacySelected(ParameterWrapper<ID> opponentPlayer) {
		diplomacy.ShowTalkScreenForPlayer(controller.id, opponentPlayer.Value);
	}
}
