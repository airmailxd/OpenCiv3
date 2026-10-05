using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using Godot;

[GlobalClass]
[Tool]
public partial class WondersView : Control {

	[Export] public TextureRect background;

	private TextureButton _close;
	private GridContainer grid;

	public WondersView() {
		MouseFilter = MouseFilterEnum.Stop;
	}

	public override void _Ready() {
		this.CreateUI();
	}

	private void CreateUI() {
		background.Texture = TextureLoader.Load("screens.wonders.background");

		_close = AdvisorUtils.CreateExitButton(background);
		_close.Pressed += () => { this.GetParent<GameViews>().Hide(); };

		AdvisorUtils.CreateAdvisorTitle(background, background.Texture.GetWidth(), "WONDERS OF THE WORLD");

		Vector2 size = background.Texture.GetSize();
		ScrollContainer scroll = new() {
			Position = new Vector2(60, 90),
			Size = size - new Vector2(120, 150),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		background.AddChild(scroll);

		grid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		grid.AddThemeConstantOverride("h_separation", 24);
		grid.AddThemeConstantOverride("v_separation", 4);
		scroll.AddChild(grid);
	}

	public void ShowView() {
		Show();

		EngineStorage.ReadGameData(DrawWonders);
	}

	// Each great wonder: who has it and where, or who is building it.
	private void DrawWonders(GameData gameData) {
		foreach (Node child in grid.GetChildren()) {
			child.QueueFree();
		}

		AddRow(18, "Wonder", "Owner", "City");

		Dictionary<Building, City> builtIn = new();
		foreach (City city in gameData.cities) {
			foreach (CityBuilding cityBuilding in city.GetBuildings()) {
				if (cityBuilding.building.IsGreatWonder()) {
					builtIn[cityBuilding.building] = city;
				}
			}
		}

		// The cities building each wonder, found in one pass over the cities.
		Dictionary<IProducible, List<City>> buildingCities = new();
		foreach (City city in gameData.cities) {
			if (city.itemBeingProduced == null) {
				continue;
			}
			if (!buildingCities.TryGetValue(city.itemBeingProduced, out List<City> list)) {
				list = new();
				buildingCities[city.itemBeingProduced] = list;
			}
			list.Add(city);
		}

		foreach (Building wonder in gameData.Buildings.Where(b => b.IsGreatWonder())) {
			if (builtIn.TryGetValue(wonder, out City city)) {
				AddRow(14, wonder.name, city.owner.civilization.noun, city.name);
			} else if (gameData.GreatWondersBuilt.Contains(wonder.name)) {
				AddRow(14, wonder.name, "Lost", "");
			} else {
				List<string> builders = buildingCities.TryGetValue(wonder, out List<City> cities)
					? cities.Select(c => $"{c.name} ({c.owner.civilization.noun})").ToList()
					: new();
				AddRow(14, wonder.name, builders.Count == 0 ? "" : "Being built", string.Join(", ", builders));
			}
		}
	}

	private void AddRow(int fontSize, params string[] values) {
		foreach (string value in values) {
			Label label = new() { Text = value, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(160, 0) };
			label.AddThemeFontSizeOverride("font_size", fontSize);
			grid.AddChild(label);
		}
	}
}
