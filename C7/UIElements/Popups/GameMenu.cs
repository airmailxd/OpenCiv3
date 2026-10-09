using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Serilog;

public partial class GameMenu : Popup {
	// Whether the menu offers to reveal the whole map (or hide it again), and whether it is revealed now.
	private readonly bool canRevealWholeMap;
	private readonly bool wholeMapRevealed;

	public GameMenu(bool canRevealWholeMap = false, bool wholeMapRevealed = false) {
		this.canRevealWholeMap = canRevealWholeMap;
		this.wholeMapRevealed = wholeMapRevealed;
		alignment = BoxContainer.AlignmentMode.Center;
		margins = new Margins(top: 100);
	}

	public override void _Ready() {
		base._Ready();

		AddTexture(370, 300);
		AddBackground(370, 300);

		AddHeader("Main Menu", 10);

		// Note: Enable buttons as the features are implemented

		List<Tuple<string, Action>> buttons = [
			new("Map", ShowMap),
			new("Load Game", Load),
			// new("New Game (Ctrl-Shift-Q)", NewGame),
			// TODO: Quick Start?
			// new("Preferences (Ctrl-P)", OpenPreferences),
			new("Retire", Retire),
			new("Save Game", Save),
			new("Quit Game (ESC)", Quit)
		];
		// Shows the whole map without fog of war, to look over the generated terrain.
		if (canRevealWholeMap) {
			buttons.Add(new(wholeMapRevealed ? "Hide Unexplored Map" : "Reveal Whole Map", ToggleRevealWholeMap));
		}

		int verticalOffset = 60;
		int rowHeight = 25;

		var indexedButtons = buttons.Select((x, i) =>
			new { Label = x.Item1, Func = x.Item2, Idx = i });

		foreach (var button in indexedButtons) {
			AddButton(button.Label, verticalOffset + button.Idx * rowHeight, button.Func);
		}
	}

	private void Load() {
		GetParent().EmitSignal(PopupOverlay.SignalName.LoadGame);
	}

	private void Save() {
		GetParent().EmitSignal(PopupOverlay.SignalName.SaveGame);
	}


	private void ShowMap() { // i.e., Cancel: return from menu to game
		GetParent().EmitSignal(PopupOverlay.SignalName.HidePopup);
	}

	private void ToggleRevealWholeMap() {
		GetParent().EmitSignal(PopupOverlay.SignalName.ToggleRevealWholeMap);
		GetParent().EmitSignal(PopupOverlay.SignalName.HidePopup);
	}

	private void Retire() {
		GetParent().EmitSignal(PopupOverlay.SignalName.Retire);
	}

	private void Quit() {
		GetParent().EmitSignal(PopupOverlay.SignalName.Quit);
	}

	private void NewGame() {
		// TODO: Wire to a NewGame scene
	}

	private void OpenPreferences() {
		// TODO: Preferences management - disable animation, etc.
	}
}
