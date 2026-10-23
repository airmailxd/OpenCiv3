using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using Godot;

// Staging (see C7Engine.Staging): asks a human which of their cities the
// turn's unit goes to, from a list of the cities with each one's size and
// garrison. It must be answered; Escape takes the city chosen in the list.
// The map shows the city chosen, below the popup.
public partial class StagingPopup : Popup {
	private const int Width = 660;
	private const int Height = 230;

	private readonly Player player;
	private readonly string unitType;
	private readonly List<City> cities;
	private readonly Action<City> choose;
	private readonly Action<City, Vector2> show;
	private OptionButton list;

	// show moves the map so the city is at the given point on the screen.
	public StagingPopup(Player player, string unitType, List<City> cities, Action<City> choose, Action<City, Vector2> show = null) {
		alignment = BoxContainer.AlignmentMode.Center;
		margins = new Margins(top: 100);
		this.player = player;
		this.unitType = unitType;
		this.cities = cities;
		this.choose = choose;
		this.show = show;
	}

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
		bool ship = unit != null && unit.IsSeaUnit();
		string where = ship ? "\nSent to an inland city, it will put to sea at the nearest coast." : "";
		Label message = new() {
			Text = $"{player.civilization.noun}: a {unitType} has joined our forces. Which city shall it report to?{where}",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(Width - 50, 0),
		};
		message.SetPosition(new Vector2(25, 55));
		AddChild(message);

		list = new() { CustomMinimumSize = new Vector2(Width - 50, 0) };
		foreach (City city in cities) {
			list.AddItem(Describe(city, ship));
		}
		list.Select(0);
		list.ItemSelected += _ => ShowChosen();
		list.SetPosition(new Vector2(25, 125));
		AddChild(list);
		// Once the popup is laid out, so its bottom edge is known.
		Callable.From(ShowChosen).CallDeferred();

		AddConfirmButton(new Vector2(Width - 66, Height - 66), Choose);
	}

	// "Paris (size 5), garrison: Pikeman x2, Knight", with "inland" for a
	// ship if the city isn't on the coast.
	private static string Describe(City city, bool ship) {
		List<string> garrison = Staging.Garrison(city)
			.GroupBy(u => u.unitType.name)
			.OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
			.Select(g => g.Count() > 1 ? $"{g.Key} x{g.Count()}" : g.Key)
			.ToList();
		string inland = ship && !city.location.NeighborsOcean() ? ", inland" : "";
		return $"{city.name} (size {city.residents.Count}{inland}), garrison: {(garrison.Count > 0 ? string.Join(", ", garrison) : "none")}";
	}

	// Moves the map so the city chosen is in the middle of the screen below
	// the popup.
	private void ShowChosen() {
		int index = list?.Selected ?? -1;
		if (show == null || index < 0 || index >= cities.Count || !IsInsideTree()) {
			return;
		}
		Vector2 screen = GetViewportRect().Size;
		float below = Mathf.Min(GetGlobalRect().End.Y, screen.Y);
		show(cities[index], new Vector2(screen.X / 2, (below + screen.Y) / 2));
	}

	private void Choose() {
		int index = list?.Selected ?? -1;
		if (index < 0 || index >= cities.Count) {
			return;
		}
		GetParent<PopupOverlay>().OnHidePopup();
		choose(cities[index]);
	}
}
