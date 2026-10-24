using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using Godot;

// Staging (see C7Engine.Staging): asks a human which of their cities the
// turn's unit goes to, from a list of the cities with each one's size and
// garrison, shown as a count and an icon for each kind of unit. It must be
// answered; Escape takes the city chosen in the list. The map shows the
// city chosen, below the popup.
public partial class StagingPopup : Popup {
	private const int Width = 660;
	private const int Height = 230;
	private const int RowHeight = 34;
	private const int IconSize = 28;

	private readonly Player player;
	private readonly string unitType;
	private readonly List<City> cities;
	private readonly Action<City> choose;
	private readonly Action<City> show;
	private bool ship;
	private int selected = 0;
	// The list: a button showing the city chosen, which opens the others.
	private Button list;
	private PopupPanel dropdown;

	// Whether a Staging popup is open, during which the map shows the city
	// chosen and doesn't follow other units' moves.
	public static bool IsOpen { get; private set; }

	// show moves the map so the city is in the middle of the screen.
	public StagingPopup(Player player, string unitType, List<City> cities, Action<City> choose, Action<City> show = null) {
		alignment = BoxContainer.AlignmentMode.Center;
		margins = new Margins(top: 100);
		this.player = player;
		this.unitType = unitType;
		this.cities = cities;
		this.choose = choose;
		this.show = show;
	}

	// The player may look around the map while choosing.
	public override bool LetsMapMove => true;

	public override bool OnEscape() {
		Choose();
		return false;
	}

	public override void _Ready() {
		base._Ready();
		AddTexture(Width, Height);
		AddBackground(Width, Height);
		AddHeader("Staging", 10);

		UnitPrototype unit = EngineStorage.gameData?.unitPrototypes.Find(p => p.name == unitType);
		ship = unit != null && unit.IsSeaUnit();
		string where = ship ? "\nSent to an inland city, it will put to sea at the nearest coast." : "";
		Label message = new() {
			Text = $"{player.civilization.noun}: a {unitType} has joined our forces. Which city shall it report to?{where}",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(Width - 50, 0),
		};
		message.SetPosition(new Vector2(25, 55));
		AddChild(message);

		list = new() { CustomMinimumSize = new Vector2(Width - 50, RowHeight) };
		list.SetPosition(new Vector2(25, 118));
		list.Pressed += OpenDropdown;
		AddChild(list);
		ShowSelected();
		// Once the popup is in the tree.
		Callable.From(ShowChosen).CallDeferred();
		IsOpen = true;

		AddConfirmButton(new Vector2(Width - 66, Height - 66), Choose);
	}

	public override void _ExitTree() {
		IsOpen = false;
		base._ExitTree();
	}

	// Puts the city chosen on the list's button, with the arrow that says it
	// opens.
	private void ShowSelected() {
		foreach (Node child in list.GetChildren()) {
			child.QueueFree();
		}
		HBoxContainer row = Row(cities[selected], list.GetThemeColor("font_color", "OptionButton"));
		row.SetAnchorsPreset(LayoutPreset.FullRect);
		row.OffsetLeft = 6;
		row.OffsetRight = -6;
		row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
		row.AddChild(new TextureRect {
			Texture = list.GetThemeIcon("arrow", "OptionButton"),
			StretchMode = TextureRect.StretchModeEnum.KeepCentered,
			MouseFilter = MouseFilterEnum.Ignore,
		});
		list.AddChild(row);
	}

	// The list of every city, under the button.
	private void OpenDropdown() {
		dropdown?.QueueFree();
		dropdown = new PopupPanel();
		VBoxContainer rows = new();
		Color text = rows.GetThemeColor("font_color", "PopupMenu");
		for (int i = 0; i < cities.Count; i++) {
			int index = i;
			Button item = new() { Flat = true, CustomMinimumSize = new Vector2(Width - 50, RowHeight) };
			HBoxContainer row = Row(cities[i], text);
			row.SetAnchorsPreset(LayoutPreset.FullRect);
			row.OffsetLeft = 6;
			item.AddChild(row);
			item.Pressed += () => {
				dropdown.Hide();
				selected = index;
				ShowSelected();
				ShowChosen();
			};
			rows.AddChild(item);
		}
		dropdown.AddChild(rows);
		AddChild(dropdown);
		Rect2 at = list.GetGlobalRect();
		dropdown.Popup(new Rect2I((Vector2I)(at.Position + new Vector2(0, at.Size.Y)), new Vector2I((int)at.Size.X, 0)));
	}

	// A city's line: "Paris (size 5)", "inland" too for a ship if the city
	// isn't on the coast, then for each kind of unit in its garrison the
	// count and the unit's icon, most first.
	private HBoxContainer Row(City city, Color text) {
		HBoxContainer row = new() { MouseFilter = MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 4);
		Label Text(string s) {
			Label label = new() { Text = s, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
			label.AddThemeColorOverride("font_color", text);
			return label;
		}
		string inland = ship && !city.location.NeighborsOcean() ? ", inland" : "";
		row.AddChild(Text($"{city.name} (size {city.residents.Count}{inland})"));
		row.AddChild(new Control { CustomMinimumSize = new Vector2(10, 0), MouseFilter = MouseFilterEnum.Ignore });
		var garrison = Staging.Garrison(city)
			.GroupBy(u => u.unitType)
			.OrderByDescending(g => g.Count()).ThenBy(g => g.Key.name)
			.ToList();
		if (garrison.Count == 0) {
			row.AddChild(Text("no garrison"));
		}
		foreach (var group in garrison) {
			row.AddChild(Text(group.Count().ToString()));
			row.AddChild(new TextureRect {
				Texture = TextureLoader.Load("unit_icons", new ItemContext(group.Key, city.owner), useCache: true),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				CustomMinimumSize = new Vector2(IconSize, IconSize),
				SizeFlagsVertical = SizeFlags.ShrinkCenter,
				TooltipText = group.Key.name,
				MouseFilter = MouseFilterEnum.Ignore,
			});
		}
		return row;
	}

	// Moves the map so the city chosen is in the middle of the screen, just
	// below the popup, as for a city's production.
	private void ShowChosen() {
		if (show == null || selected >= cities.Count || !IsInsideTree()) {
			return;
		}
		show(cities[selected]);
	}

	private void Choose() {
		if (selected >= cities.Count) {
			return;
		}
		dropdown?.Hide();
		GetParent<PopupOverlay>().OnHidePopup();
		choose(cities[selected]);
	}
}
