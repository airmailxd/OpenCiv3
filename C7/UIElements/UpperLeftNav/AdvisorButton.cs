using C7Engine;
using Godot;

// The advisors button in the upper left. A left click opens the Domestic
// Advisor; a right click opens the advisors menu, which reaches every
// advisor and the other report screens by mouse too (on a Mac the function
// keys are taken by the system unless Fn is held). (Owner's design,
// 2026-10-23.)
[Tool]
public partial class AdvisorButton : Civ3TextureButton {
	// Called when the node enters the scene tree for the first time.
	public override void _Ready() {
		TextureLoader.SetButtonTextures(this, "upper_left_navigation.advisor");
		TooltipText = "Advisors";
	}

	public override void _Pressed() {
		base._Pressed();
		ReleaseFocus();

		// The button belongs to the game scene, whose root is the game.
		if (Owner is Game game) {
			game.EmitSignal(Game.SignalName.ShowSpecificAdvisor, C7Action.ShowDomesticAdvisor);
		}
	}

	public override void _GuiInput(InputEvent @event) {
		// (A click outside the open menu, on this button too, closes it.)
		if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } && Owner is Game game) {
			AcceptEvent();
			new AdvisorMenu(game).Open(GetGlobalRect().Position + new Vector2(0, Size.Y));
		}
	}
}

// The advisors menu: the six advisors (F1 to F6), then the report screens
// (F7 to F11).
public partial class AdvisorMenu : RightClickMenu {
	public AdvisorMenu(Game game) : base(game) {
		var items = C7Action.AdvisorScreens;
		for (int i = 0; i < items.Length; i++) {
			(string action, _, bool advisor) = items[i];
			if (i > 0 && advisor != items[i - 1].isAdvisor) {
				AddTreeSeparator();
			}
			AddItem(C7Action.AdvisorScreenLabel(i, MacShortcuts.IsMac), () => {
				CloseAndDelete();
				game.EmitSignal(advisor ? Game.SignalName.ShowSpecificAdvisor : Game.SignalName.ShowGameView, action);
			});
		}
	}
}
