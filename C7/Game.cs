using Godot;
using System;
using System.Diagnostics;
using C7Engine;
using C7GameData;
using Serilog;
using C7Engine.Pathing;
using System.Collections.Generic;
using System.Linq;
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
	private bool lanDisconnectShown = false;

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

		try {
			await InitializeGame();
			await StartGame();
		} catch (Exception ex) {
			errorOnLoad = true;
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
		// Ensure we clear out our image caches, as scenarios and games will
		// use the same filenames but have different content for them.
		Util.ClearCaches();

		GameParams options = CreateGameParams();

		await CreateGameAndAssignPlayerController(options);
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
		} else if (LanSession.IsActive) {
			// Whoever plays first may be at another machine.
			Player active = EngineStorage.gameData.GetPlayer(EngineStorage.activePlayerID);
			if (active != null && active.id != controller.id) {
				ShowLanWaiting(active);
			} else {
				MaybeAutoplayLanTurn();
			}
		} else if (TurnHandling.IsHotseat(EngineStorage.gameData)) {
			// The first hotseat player also needs the others to look away.
			ShowHotseatHandoff(controller, OnPlayerStartTurn);
		}

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

			Vector2? cameraLocation = null;
			float cameraZoom = 1.0f;
			if (mapView != null) {
				cameraLocation = mapView.cameraLocation;
				cameraZoom = mapView.cameraZoom;
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
				// The whole map is shown, so whose eyes we borrow only
				// matters for things like the starting camera position.
				EngineStorage.gameData.observerMode = true;
				EngineStorage.uiControllerID = EngineStorage.gameData.players
					.FirstOrDefault(p => p.isHuman && !p.defeated)?.id ?? EngineStorage.gameData.players[0].id;
			} else {
				EngineStorage.uiControllerID = client.PlayerID;
			}
			controller = EngineStorage.gameData.GetUIControllerPlayer();
			client.SnapshotReceived = OnLanSnapshot;
			client.UiMessageReceived = json => HandleEngineMessage(NetSerialization.DeserializeMessageToUI(json));
		}
	}

	private void PollLanSession() {
		LanSession.Host?.Poll();

		LanClient client = LanSession.Client;
		if (client == null) {
			return;
		}
		client.Poll();
		if (!client.IsConnected && !lanDisconnectShown) {
			lanDisconnectShown = true;
			HideLanWaiting();
			popupOverlay.ShowPopup(
				new ConfirmationPopup(
					"We have lost contact with the host.\nThe game cannot continue.\n\n",
					"Return to the main menu.",
					"Let me look around first.",
					OnRetire),
				PopupOverlay.PopupCategory.Advisor);
		}
	}

	// A LAN client shows the host's game: replace ours with the snapshot,
	// and point the UI at the new units, cities and players.
	private void OnLanSnapshot(C7GameData.Save.SaveGame save) {
		Stopwatch applyTime = Stopwatch.StartNew();
		GameData gameData = CreateGame.ReplaceWithSnapshot(save, Global.GameMode.behaviors);
		TextureLoader.ForgetGameObjects();
		// A spectator sees the whole map.
		gameData.observerMode = LanSession.IsSpectator;

		controller = gameData.GetUIControllerPlayer();
		InitializeMapView();
		unitSelector.RefreshAfterGameReplaced();
		cityScreen.RefreshAfterGameReplaced();
		if (bombardInfo != null) {
			MapUnit bombarder = gameData.GetUnit(bombardInfo.bombardingUnit.id);
			setBombard(bombarder);
		}
		lastTile = null;

		if (applyTime.ElapsedMilliseconds > 100) {
			log.Information("Showing the host's snapshot took {Milliseconds} ms", applyTime.ElapsedMilliseconds);
		}
	}

	// With the --lan-autoplay developer option, ends our turn soon after it
	// starts, for watching turns pass between machines.
	private void MaybeAutoplayLanTurn() {
		if (!LanSession.DevAutoplay) {
			return;
		}
		GetTree().CreateTimer(1.5).Timeout += () => {
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
		log.Information("Waiting for {Player} to play their turn", active);
		string playerName = active.name ?? active.civilization.leader;
		ShowLanBanner($"Waiting for {playerName} of the {active.civilization.noun} to play their turn...");
	}

	private void ShowLanBanner(string text) {
		CurrentState = GameState.ComputerTurn;
		HideLanWaiting();

		Label label = new() {
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
		};
		label.AddThemeFontSizeOverride("font_size", 20);
		label.AddThemeColorOverride("font_color", Colors.White);

		PanelContainer panel = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.65f) });
		panel.AddChild(label);

		lanWaitingBanner = new CanvasLayer { Layer = 90 };
		lanWaitingBanner.AddChild(panel);
		AddChild(lanWaitingBanner);

		// A strip across the top of the screen, below the toolbar, leaving
		// room for the scoreboard on the right.
		panel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		panel.OffsetLeft = 160;
		panel.OffsetRight = -440;
		panel.OffsetTop = 70;
		panel.OffsetBottom = 110;
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
			// Each machine plays its own player; the others wait for them.
			EngineStorage.activePlayerID = next.id;
			if (next.id != controller.id) {
				ShowLanWaiting(next);
				return;
			}
			HideLanWaiting();
			OnPlayerStartTurn();
			MaybeAutoplayLanTurn();
			return;
		}

		if (!TurnHandling.IsHotseat(EngineStorage.gameData)) {
			controller = next;
			OnPlayerStartTurn();
			return;
		}

		ShowHotseatHandoff(next, OnPlayerStartTurn);
	}

	private void ShowHotseatHandoff(Player next, Action onBeginTurn) {
		ShowHotseatHandoff(next,
			"It is your turn. Make sure the other players aren't looking.",
			"Begin Turn",
			onBeginTurn);
	}

	private void ShowHotseatHandoff(Player next, string message, string buttonText, Action onContinue) {
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
		if (controller != null && controller != next) {
			hotseatCameraLocations[controller.id] = mapView.cameraLocation;
		}

		controller = next;
		if (hotseatCameraLocations.TryGetValue(controller.id, out Vector2 cameraLocation)) {
			mapView.cameraLocation = cameraLocation;
		} else {
			CenterCameraOnController();
		}

		hotseatHandoff?.QueueFree();
		// Games without player names (e.g. older saves) fall back to the leader.
		string playerName = controller.name ?? controller.civilization.leader;
		hotseatHandoff = new HotseatHandoff(
			$"{playerName} - {controller.civilization.name}",
			message,
			buttonText,
			() => {
				hotseatHandoff = null;
				onContinue();
			});
		CanvasLayer curtainLayer = new() { Layer = 100 };
		curtainLayer.AddChild(hotseatHandoff);
		hotseatHandoff.TreeExited += curtainLayer.QueueFree;
		AddChild(curtainLayer);
	}

	public void HandleEngineMessage(MessageToUI msg) {
		GameData gameData = EngineStorage.gameData;

		// Hold messages for a human player who isn't at the screen (for
		// example barbarians raiding them during the AI turns) until they are.
		if (!LanSession.IsActive && msg.recipient != null && msg.recipient.isHuman && msg.recipient.id != controller.id) {
			if (!heldMessages.TryGetValue(msg.recipient.id, out Queue<MessageToUI> held)) {
				held = new();
				heldMessages[msg.recipient.id] = held;
			}
			held.Enqueue(msg);
			return;
		}

		switch (msg) {
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
			case MsgCityDestroyed mCD:
				// A LAN client has already redrawn the map without the city.
				if (mCD.city != null) {
					mapView.cityLayer.UpdateAfterCityDestruction(mCD.city);
				}
				break;
			case MsgCityCaptured mCCap:
				mapView.cityLayer.UpdateAfterCityCapture(mCCap.city);
				break;
			case MsgCivilizationDestroyed mCivD when LanSession.IsSpectator:
				ShowSpectatorNews($"The {mCivD.civilization.noun} have been destroyed");
				break;
			case MsgCivilizationDestroyed mCivD:
				popupOverlay.ShowPopup(new CivilizationDestroyed(mCivD.civilization), PopupOverlay.PopupCategory.Advisor);
				InterestingEvent();
				break;
			case MsgShowMilitaryAdvisorPopup mSMAP:
				if (!popupOverlay.Visible) {
					var mood = mSMAP.happy ? AdvisorHead.Mood.Happy : AdvisorHead.Mood.Angry;
					var pop = new InformationalPopup(mSMAP.message, AdvisorHead.Advisor.Military, mood);
					popupOverlay.ShowPopup(pop, PopupOverlay.PopupCategory.Advisor);
				}
				break;
			case MsgShowScienceAdvisor mSSA:
				EmitSignal(SignalName.ShowSpecificAdvisor, C7Action.ShowScienceAdvisor);
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
				popupOverlay.ShowPopup(
					new ConfirmationPopup(
						DealScreen.DescribeDeal(mSDP.proposer, mSDP.proposerGives, mSDP.proposerWants),
						"We accept.",
						"We refuse.",
						() => { new MsgRespondToDeal(true).send(); },
						() => { new MsgRespondToDeal(false).send(); }),
					PopupOverlay.PopupCategory.Advisor);
				InterestingEvent();
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
			case MsgCityChanged mCC:
				cityScreen.RefreshCity(mCC.city);
				break;
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

	private void InterestingEvent() {
		// Break out of fast forward mode after interesting events.
		turnsLeftToFastForward = 0;
	}

	public override void _Process(double delta) {
		PollLanSession();
		ProcessActions();

		if (!EngineStorage.HasPendingAnimations())
			EngineStorage.ProcessNextMessageToEngine();

		if (EngineStorage.TryDequeueNextMessageToUI(out MessageToUI msg))
			HandleEngineMessage(msg);
		else
			ReplayHeldMessage();
	}

	// Shows messages that were held for the UI controller while another
	// player had the screen, one popup at a time, once their turn is underway.
	private void ReplayHeldMessage() {
		if (CurrentState != GameState.PlayerTurn || hotseatHandoff != null || popupOverlay.Visible) {
			return;
		}
		if (heldMessages.TryGetValue(controller.id, out Queue<MessageToUI> held) && held.TryDequeue(out MessageToUI msg)) {
			HandleEngineMessage(msg);
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

	private void OnPlayerStartTurn() {
		EngineStorage.ReadGameData((GameData gameData) => {
			log.Information("Starting player turn");

			// If the player can now pick a new government, force them to do so.
			// When the popup is closed we call OnPlayerStartTurn again. This isn't
			// ideal, but we don't yet have a general purpose "show a popup and
			// wait for the player to acknowledge it" system. On a LAN the choice
			// may not be back from the host yet, so only ask once a turn.
			if (controller.government.transitionType && TurnHandling.GetTurnNumber() >= controller.inAnarchyUntilTurn
					&& (!LanSession.IsClient || governmentPromptTurn != gameData.turn)) {
				governmentPromptTurn = gameData.turn;
				popupOverlay.ShowPopup(
					new GovernmentSelection(controller, controller.GetAvailableGovernments(gameData)),
					PopupOverlay.PopupCategory.Info);
			}

			// If the player can pick a new tech to research, prompt them to do so
			// once they have a city.
			if (controller.cities.Count > 0
					&& controller.currentlyResearchedTech == null
					&& controller.GetAvailableTechsToResearch(gameData.techs).Count > 0) {
				popupOverlay.ShowPopup(
						new ScienceSelection(controller),
						PopupOverlay.PopupCategory.Info);

				// Research something even if the player dismisses the popup.
				new MsgPickDefaultResearch().send();
			}

			// Allow fast forwarding in observer mode.
			if (gameData.observerMode && turnsLeftToFastForward > 0) {
				--turnsLeftToFastForward;
				new MsgEndTurn().send();
				return;
			}

			CurrentState = GameState.PlayerTurn;
		});

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
		new MsgEndTurn().send(); // Triggers actual backend processing
		EmitSignal(SignalName.PlayerTurnEnd);
	}

	public void OnRetire() {
		// Quit to main menu, freeing previous scene data
		GetTree().ChangeSceneToFile("res://UIElements/MainMenu/main_menu.tscn");
	}

	public void OnSaveGame() {
		popupOverlay.OnHidePopup(); // hide game menu

		// FileDialog is a Window, not a Control, so we have the popup overlay present a blank control
		popupOverlay.ShowBlank();

		// TODO: this should go to our own saves directory.
		FileDialog.SetDirectoryForSaving(@"Conquests/Saves");

		// TODO: sound -- see MainMenu.PlayButtonPressedSound();
		FileDialog.Popup();
	}

	public void OnLoadGame() {
		popupOverlay.OnHidePopup(); // hide game menu

		// FileDialog is a Window, not a Control, so we have the popup overlay present a blank control
		popupOverlay.ShowBlank();

		// TODO: this should go to our own saves directory.
		FileDialog.SetDirectoryForLoading(@"Conquests/Saves");

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
	}

	public override void _UnhandledInput(InputEvent @event) {
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
			OldPosition = eventMouseButton.Position;
			IsMovingCamera = true;

			if (CanDoubleClick(eventMouseButton)) {
				doubleClickHandler.Accept(eventMouseButton);
			} else {
				OnSingleLeftMouseButtonClick(eventMouseButton);
			}
		} else {
			IsMovingCamera = false;
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

	private void OnSingleLeftMouseButtonClick(InputEventMouseButton eventMouseButton) {
		if (gotoInfo != null) {
			this.ResolveMovement(gotoInfo);
			this.SetGotoMode(false);
		} else if (bombardInfo != null) {
			Tile tile = PositionToTile(eventMouseButton.Position);
			if (bombardInfo.bombardingUnit.CanBombardTile(tile, out var bombardTarget)) {
				bombardInfo.bombardTarget = bombardTarget;
				HandleBombardClick(bombardInfo, tile);
			}
			setBombard(null);
		} else {
			// Select unit on tile at mouse location
			HandleUnitSelectionTileClick(eventMouseButton);
		}
	}

	private void OnDoubleLeftMouseButtonClick(InputEventMouseButton eventMouseButton) {
		Tile tile = PositionToTile(eventMouseButton.Position);
		if (tile?.cityAtTile != null && LanSession.IsSpectator) {
			// A spectator looks at the game as the city's owner, so the city
			// screen and the advisors show that civilization.
			controller = tile.cityAtTile.owner;
			EngineStorage.uiControllerID = controller.id;
		}
		if (tile?.cityAtTile?.owner == controller) {
			EngineStorage.ReadGameData((GameData gameData) => {
				ShowCityScreenForCity(gameData, tile.cityAtTile);
			});
		}
	}

	private void HandleUnitSelectionTileClick(InputEventMouseButton eventMouseButton) {
		Tile tile = PositionToTile(eventMouseButton.Position);
		if (tile == null) {
			return;
		}

		// TODO: This should really be the top unit.
		MapUnit unit = tile.unitsOnTile.FirstOrDefault();
		if (unit == null || unit.owner != controller) {
			return;
		}

		SelectUnit(unit, eventMouseButton.Position);
	}

	private void SelectUnit(MapUnit unit, Vector2 screenPosition) {
		bool canMove = unitSelector.SetSelectedUnit(unit);

		if (unit.WorkerJob != null) {
			return;
		}

		Tile tile = PositionToTile(screenPosition);

		if (!canMove) {
			new MsgShowTemporaryPopup("This unit has already moved.", tile).send();
		}
	}

	public void SelectUnit(MapUnit unit) {
		var screenPos = mapView.screenLocationOfTile(unit.location);
		SelectUnit(unit, screenPos);
	}

	private void HandleRightMouseButton(InputEventMouseButton eventMouseButton) {
		this.SetGotoMode(false);

		Tile tile = PositionToTile(eventMouseButton.Position);
		if (tile != null) {
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

	public void ShowTileInfo(Tile tile) {
		tileInfo = new TileInfo(tile);
		var zoom = mapView.cameraZoom;
		var tileCenter = mapView.screenLocationOfTile(tile, true);
		var tileInfoPopup = new TileInfoPopup(this, tile, tileCenter, zoom);
		popupOverlay.ShowPopup(tileInfoPopup, PopupOverlay.PopupCategory.TileInfo);
	}

	public void HideTileInfo() {
		tileInfo = null;
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
			mapView.cameraLocation += OldPosition - eventMouseMotion.Position;
			OldPosition = eventMouseMotion.Position;
		} else if (gotoInfo != null) {
			gotoInfo = GetGotoInfo(eventMouseMotion.Position);
		} else if (bombardInfo != null) {
			bombardInfo.mouseTile = PositionToTile(eventMouseMotion.Position);
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
							(string turns) => { turnsLeftToFastForward = int.Parse(turns); }),
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
	}

	private void HandleMagnifyGesture(InputEventMagnifyGesture magnifyGesture) {
		double newScale = mapView.cameraZoom * magnifyGesture.Factor;

		mapView.setCameraZoom((float)newScale, magnifyGesture.Position);
	}

	private void ProcessActions() {
		Godot.Collections.Array<StringName> actions = InputMap.GetActions();

		foreach (StringName action in actions) {
			// Match modifiers exactly, so that Shift+Enter or Ctrl+L don't also
			// trigger the actions bound to plain Enter or L.
			if (Input.IsActionJustPressed(action, exactMatch: true)) {
				ProcessAction(action.ToString());
			} else if (Input.IsActionJustReleased(action)) {
				ProcessOnReleaseAction(action.ToString());
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
		if (popupOverlay.Visible || cityScreen.Visible || diplomacy.Visible)
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

		if (currentAction == C7Action.Escape && tileInfo != null) {
			HideTileInfo();
			return;
		}

		if (currentAction == C7Action.Escape && popupOverlay.ShowingPopup) {
			popupOverlay.OnHidePopup();
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
			popupOverlay.ShowPopup(
				new ConfirmationPopup(
					$"Disband {CurrentlySelectedUnit.name}? Pardon me but these are OUR people.\nDo you really want to disband them?",
					"Yes, we need to!",
					"No. Maybe you are right, advisor.",
					() => {
						new MsgUnitCommand(CurrentlySelectedUnit.id, MsgUnitCommand.Command.Disband).send();
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
			popupOverlay.ShowPopup(
				new ConfirmationPopup(
					$"A previous terrain enhancement ({replacementTarget.key.Capitalize()}) will be replaced \nby this operation. Do you wish to continue?",
					"Continue.",
					"Cancel action.",
					() => {
						new MsgStartWorkerJob(CurrentlySelectedUnit.id, terraform).send();
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

	private void SetGotoMode(bool isOn) {
		if (isOn) {
			this.gotoInfo = new();
		} else {
			this.gotoInfo = null;
			this.lastTile = null;
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
				this.MaybeDeclareWar(stashed.requiresWarDeclarationOnPlayer, gameData.turn, () => {
					stashed.requiresWarDeclarationOnPlayer = null;
					this.ResolveMovement(stashed);
					this.SetGotoMode(false);
				});
			} else {
				new MsgSetUnitPath(CurrentlySelectedUnit.id, info.path).send();
			}
		});
	}

	private void MaybeDeclareWar(Player player, int currentTurn, Action callback) {
		popupOverlay.ShowPopup(new WarConfirmation(player,
			() => {
				new MsgDeclareWar(player).send();
				callback();
			}), PopupOverlay.PopupCategory.Advisor);
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
		GotoInfo result = new();

		EngineStorage.ReadGameData((GameData gameData) => {
			if (tile == this.lastTile && this.gotoInfo != null) {
				result = this.gotoInfo;
				return;
			}
			this.lastTile = result.destinationTile = tile;

			// Figure out what unit is in goto mode. If the tile we're hovering over is
			// different than the tile the unit is on, calculate the path to move there.
			MapUnit unit = tile == null ? null : gameData.GetUnit(CurrentlySelectedUnit.id);

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
		if (info == null || tile == null) {
			return;
		}

		EngineStorage.ReadGameData((GameData gameData) => {
			if (info.RequiresWarDeclaration(tile, out var player)) {
				MaybeDeclareWar(player, gameData.turn, () => {
					new MsgBombard(CurrentlySelectedUnit.id, tile).send();
				});
			} else {
				new MsgBombard(CurrentlySelectedUnit.id, tile).send();
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
		if (CurrentlySelectedUnit != null)
			new MsgBuildCity(CurrentlySelectedUnit, name).send();
	}

	public void ShowCityScreenForCity(GameData gameData, City city) {
		city.RecalculateCitizenMoods(gameData);
		EmitSignal(SignalName.ShowCityScreen, new ParameterWrapper<City>(city));
	}

	public void OnDiplomacySelected(ParameterWrapper<ID> opponentPlayer) {
		diplomacy.ShowTalkScreenForPlayer(controller.id, opponentPlayer.Value);
	}
}
