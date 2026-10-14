using System.Collections.Generic;
using C7Engine;
using Godot;
using C7Engine.PalaceMinigame;

[Tool]
public partial class PalaceView : Control {
	[Export] public TextureRect background;
	[Export] public TextureRect buildingsLayer;

	private TextureButton _close;

	public override void _Ready() {
		base._Ready();

		// Standalone mode has the palace's background but not the pieces
		// it's built from (PalaceBuildingsLayer), so it shows the grounds
		// with a way out. Before, it showed nothing at all, and F9 or the
		// Palace View button seemed to do nothing.
		background.Texture = TextureLoader.TryLoad("screens.palace.background");

		MouseFilter = MouseFilterEnum.Stop;

		_close = AdvisorUtils.CreateExitButton(background);
		_close.Pressed += () => { this.GetParent<GameViews>().Hide(); };
	}

	public void ShowView() {
		Show();
	}
}
