using Godot;
using ConvertCiv3Media;

[Tool]
public partial class MenuButton : Civ3TextureButton {

	[Export]
	private PopupOverlay popupOverlay;

	public override void _Ready() {
		ImageTexture menuTexture = TextureLoader.Load("upper_left_navigation.menu");
		this.TextureNormal = menuTexture;
	}

	public override void _Pressed() {
		// The menu button belongs to the game scene, whose root is the game.
		Game game = Owner as Game;
		GameMenu menu = new(canRevealWholeMap: game?.CanRevealWholeMap ?? false, wholeMapRevealed: game?.IsWholeMapRevealed ?? false);
		popupOverlay.ShowPopup(menu, PopupOverlay.PopupCategory.Info);
		ReleaseFocus();
	}

}
