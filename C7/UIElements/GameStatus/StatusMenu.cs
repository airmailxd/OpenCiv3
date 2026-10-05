using Godot;
using C7GameData;
using C7Engine;

[GlobalClass]
public partial class StatusMenu : Control {
	[Signal] public delegate void ShowGameViewEventHandler();

	[Export] ConsoleButton openDiplomacy;
	[Export] ConsoleButton openPalaceScreen;

	[Export] PopupOverlay popupOverlay;

	public override void _Ready() {
		openDiplomacy.Pressed += OpenDiplomacyPopup;
		openPalaceScreen.Pressed += () => {
			EmitSignal(SignalName.ShowGameView, C7Action.ShowPalaceView);
		};
	}

	// What the buttons were last set to, so they are only changed when that
	// changes, and the player shown, so they aren't looked up every frame.
	private GameData shownGameData;
	private ID shownControllerId;
	private Player shownPlayer;
	private bool buttonsSet = false;
	private bool diplomacyShown;

	public override void _Process(double delta) {
		GameData gD = EngineStorage.gameData;
		if (gD == null || gD.observerMode) {
			return;
		}

		if (!ReferenceEquals(gD, shownGameData) || shownControllerId != EngineStorage.uiControllerID || shownPlayer == null) {
			shownGameData = gD;
			shownControllerId = EngineStorage.uiControllerID;
			shownPlayer = gD.GetUIControllerPlayer();
		}

		// Only show the diplomacy button if we have civs to talk to.
		//
		// After meeting a civ and revealed the button,
		// if that civ gets destroyed and we don't have any more relationships
		// with other civs (haven't met them yet) we should hide the button again.
		// It will be shown again when we meet another civ.
		bool showDiplomacy = shownPlayer.playerRelationships.Count > 0;
		if (buttonsSet && showDiplomacy == diplomacyShown) {
			return;
		}
		buttonsSet = true;
		diplomacyShown = showDiplomacy;

		if (showDiplomacy) {
			openDiplomacy.ShowButton();
		} else {
			openDiplomacy.HideButton();
		}

		// TODO: Don't show the palace button if the player can't start building the palace
		openPalaceScreen.ShowButton();
	}

	private void OpenDiplomacyPopup() {
		EngineStorage.ReadGameData((GameData gD) => {
			Player player = gD.GetUIControllerPlayer();

			popupOverlay.ShowPopup(new DiplomacySelection(player, gD.players), PopupOverlay.PopupCategory.Info);
		});
	}
}
