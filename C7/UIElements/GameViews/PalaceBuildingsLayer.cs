using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.PalaceMinigame;
using Godot;

[Tool]
public partial class PalaceBuildingsLayer : TextureRect {
	[Export] HBoxContainer switchButtonContainer;
	ButtonGroup switchButtonGroup = new();

	string activeCulture;
	Dictionary<string, Culture> cultures = [];

	Building pendingBuilding;
	// Kept sorted by Index, the order they are drawn in.
	List<Building> assignedBuildings = [];
	HashSet<int> assignedIndexes = [];

	// The building textures, by path.
	Dictionary<string, ImageTexture> textures = [];

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

	private ImageTexture GetTexture(Building building) {
		if (!textures.TryGetValue(building.TexturePath, out ImageTexture texture) || !IsInstanceValid(texture)) {
			texture = TextureLoader.LoadByPath(building.TexturePath);
			textures[building.TexturePath] = texture;
		}
		return texture;
	}

	private void SetPendingBuilding(Building building) {
		if (pendingBuilding != building) {
			pendingBuilding = building;
			QueueRedraw();
		}
	}

	private void AssignBuilding(Building building) {
		// Insert after any buildings with the same index, as a stable sort
		// by index would.
		int i = assignedBuildings.Count;
		while (i > 0 && assignedBuildings[i - 1].Index > building.Index) {
			--i;
		}
		assignedBuildings.Insert(i, building);
		assignedIndexes.Add(building.Index);
		QueueRedraw();
	}

	public override void _Draw() {
		foreach (Building b in assignedBuildings) {
			ImageTexture texture = GetTexture(b);
			DrawTexture(texture, new Vector2(b.X, b.Y));
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
				AssignBuilding(pendingBuilding);
				SetPendingBuilding(null);
			}
		} else if (@event is InputEventMouseMotion eventMouseMotion) {
			foreach (Building building in AvailableBuildings()) {
				ImageTexture texture = GetTexture(building);
				Rect2 textureRect = new() {
					Position = new(building.X, building.Y),
					Size = texture.GetSize()
				};

				if (textureRect.HasPoint(eventMouseMotion.Position)) {
					SetPendingBuilding(building);
					return;
				}
			}

			SetPendingBuilding(null);
		}
	}

	private IEnumerable<Building> AvailableBuildings() {
		return cultures[activeCulture].Buildings
			.Where(b => !assignedIndexes.Contains(b.Index))
			.Where(b => b.Prerequisites.All(index => assignedIndexes.Contains(index)));
	}

	public void ActivateCulture(Culture culture) {
		activeCulture = culture.Name;
	}
}
