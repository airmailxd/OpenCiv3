using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.PalaceMinigame;
using Godot;
using PalaceUpgrades = C7GameData.PalaceUpgrades;
using Player = C7GameData.Player;

// The pieces of the player's palace, and the choosing of new ones. Each
// palace upgrade the people offer (C7GameData.PalaceUpgrades) builds one
// piece, in whichever culture's style the buttons below choose: the pieces
// that may be built light up under the mouse, and a click builds one (Civ3
// FUN_00589870 and FUN_005896c0). A piece may be built once its rules in
// PalaceView.txt are met: the pieces its row of #PVRULES_ marks are built,
// whatever their style.
[Tool]
public partial class PalaceBuildingsLayer : TextureRect {
	[Export] HBoxContainer switchButtonContainer;
	ButtonGroup switchButtonGroup = new();

	string activeCulture;
	Dictionary<string, Culture> cultures = [];

	Building pendingBuilding;

	// The palace as last read from the game, kept up to date as pieces are
	// built here: the culture of each piece built, and how many upgrades
	// there are to place.
	PalaceUpgrades palace = new();

	// The building textures, by path, and the images behind them, to find
	// the piece under the mouse.
	Dictionary<string, ImageTexture> textures = [];
	Dictionary<string, Image> images = [];

	public override void _Ready() {
		base._Ready();

		if (C7Settings.UseStandaloneMode()) {
			return;
		}

		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsPreset(LayoutPreset.FullRect);

		cultures = ParsePalaceView();
		activeCulture = cultures.Keys.First();

		foreach (Culture culture in cultures.Values) {
			AddSwitchButton(culture);
		}

		switchButtonContainer.GetChild<TextureButton>(0).ButtonPressed = true;

		// Only redraw when what's shown changes.
		VisibilityChanged += QueueRedraw;
	}

	private Dictionary<string, Culture> ParsePalaceView() {
		string configPath = Util.Civ3MediaPath("Text/PalaceView.txt");
		ConfigParser parser = new();

		return parser.Parse(configPath);
	}

	private void AddSwitchButton(Culture culture) {
		var bt = culture.ButtonTextures;

		TextureButton button = new() {
			TextureNormal = TextureLoader.LoadByPath(bt.Normal),
			TexturePressed = TextureLoader.LoadByPath(bt.Pressed),
			TextureHover = TextureLoader.LoadByPath(bt.Hover),
			ButtonGroup = switchButtonGroup,
			ToggleMode = true,
		};
		button.Pressed += () => ActivateCulture(culture);

		switchButtonContainer.AddChild(button);
	}

	// Reads the palace of the player at the screen, and starts on their
	// civ's own culture (Civ3 FUN_005895d0).
	public void Refresh() {
		if (cultures.Count == 0) {
			return;
		}
		int cultureGroup = -1;
		EngineStorage.ReadGameData(gameData => {
			Player player = gameData?.GetPlayer(EngineStorage.uiControllerID);
			palace = player?.palace.Copy() ?? new();
			cultureGroup = player?.civilization?.cultureGroup?.index ?? -1;
		});
		int i = 0;
		foreach (Culture culture in cultures.Values) {
			if (culture.Index == cultureGroup) {
				switchButtonContainer.GetChild<TextureButton>(i).ButtonPressed = true;
				ActivateCulture(culture);
				break;
			}
			++i;
		}
		SetPendingBuilding(null);
		QueueRedraw();
	}

	private ImageTexture GetTexture(Building building) {
		if (!textures.TryGetValue(building.TexturePath, out ImageTexture texture) || !IsInstanceValid(texture)) {
			texture = TextureLoader.LoadByPath(building.TexturePath);
			textures[building.TexturePath] = texture;
		}
		return texture;
	}

	// Whether the piece's art covers the point, rather than leaving it
	// transparent: the pieces' rectangles overlap a lot.
	private bool Covers(Building building, Vector2 point) {
		ImageTexture texture = GetTexture(building);
		Vector2 local = point - new Vector2(building.X, building.Y);
		if (!new Rect2(Vector2.Zero, texture.GetSize()).HasPoint(local)) {
			return false;
		}
		if (!images.TryGetValue(building.TexturePath, out Image image)) {
			image = texture.GetImage();
			images[building.TexturePath] = image;
		}
		return image == null || image.GetPixel((int)local.X, (int)local.Y).A > 0;
	}

	private void SetPendingBuilding(Building building) {
		if (pendingBuilding != building) {
			pendingBuilding = building;
			QueueRedraw();
		}
	}

	// The piece built at the index, in the culture it was built in.
	private Building BuiltBuilding(int index) {
		int cultureIndex = palace.CultureOf(index);
		Culture culture = cultures.Values.FirstOrDefault(c => c.Index == cultureIndex);
		return culture != null && index < culture.Buildings.Count ? culture.Buildings[index] : null;
	}

	private void BuildPending() {
		Building building = pendingBuilding;
		int cultureIndex = cultures[activeCulture].Index;
		// The game only takes orders in the player's turn.
		bool playersTurn = false;
		EngineStorage.ReadGameData(gameData => playersTurn = TurnHandling.IsPlayersTurn(gameData, EngineStorage.uiControllerID));
		if (!playersTurn || !palace.Build(building.Index, cultureIndex)) {
			return;
		}
		new MsgBuildPalacePiece(building.Index, cultureIndex).send();
		SetPendingBuilding(null);
		QueueRedraw();
	}

	public override void _Draw() {
		// The pieces in order, as Civ3 draws them, so later ones go on top.
		int pieces = cultures.Values.Select(c => c.Buildings.Count).DefaultIfEmpty(0).Max();
		for (int i = 0; i < pieces; ++i) {
			Building b = BuiltBuilding(i);
			if (b != null) {
				DrawTexture(GetTexture(b), new Vector2(b.X, b.Y));
			}
		}

		if (pendingBuilding != null) {
			ImageTexture texture = GetTexture(pendingBuilding);
			DrawTexture(texture, new Vector2(pendingBuilding.X, pendingBuilding.Y), new Color(1, 1, 1, 0.45f));
		}

		DrawFrame();
	}

	private void DrawFrame() {
		var size = Size;

		// Outer black border
		DrawRect(new Rect2(Vector2.Zero, size), Colors.Black, false, 1f);

		// Inner white border, inset by 1px
		var inset = new Rect2(Vector2.One, size - new Vector2(2, 2));
		DrawRect(inset, Colors.White, false, 1f);
	}

	public override void _GuiInput(InputEvent @event) {
		if (@event is InputEventMouseButton eventMouseButton) {
			if (pendingBuilding == null) return;

			if (eventMouseButton.ButtonIndex == MouseButton.Left && eventMouseButton.Pressed) {
				BuildPending();
			}
		} else if (@event is InputEventMouseMotion eventMouseMotion) {
			// The topmost piece under the mouse that may be built.
			foreach (Building building in AvailableBuildings().Reverse()) {
				if (Covers(building, eventMouseMotion.Position)) {
					SetPendingBuilding(building);
					return;
				}
			}

			SetPendingBuilding(null);
		}
	}

	// The pieces an upgrade may build in the culture shown: none without an
	// upgrade to place; otherwise those not yet built whose rules are met
	// (Civ3 FUN_00589870).
	private IEnumerable<Building> AvailableBuildings() {
		if (palace.upgradesToPlace <= 0 || !cultures.TryGetValue(activeCulture, out Culture culture)) {
			return [];
		}
		return culture.Buildings
			.Where(b => !palace.IsBuilt(b.Index))
			.Where(b => b.Prerequisites.All(index => palace.IsBuilt(index)));
	}

	public void ActivateCulture(Culture culture) {
		activeCulture = culture.Name;
		SetPendingBuilding(null);
	}
}
