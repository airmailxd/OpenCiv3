using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using Godot;

// Staging (see C7Engine.Staging): asks a human which of their cities the
// turn's unit goes to, from a list of the cities with each one's size and
// garrison. It must be answered; Escape takes the city chosen in the list.
public partial class StagingPopup : Popup {
	private const int Width = 660;
	private const int Height = 230;

	private readonly Player player;
	private readonly string unitType;
	private readonly List<City> cities;
	private readonly Action<City> choose;
	private OptionButton list;

	public StagingPopup(Player player, string unitType, List<City> cities, Action<City> choose) {
		alignment = BoxContainer.AlignmentMode.Center;
		margins = new Margins(top: 100);
		this.player = player;
		this.unitType = unitType;
		this.cities = cities;
		this.choose = choose;
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
		string where = unit != null && unit.IsSeaUnit() && !cities.Any(c => c.location.NeighborsOcean())
			? "\nNone of our cities is on the coast, so it will put to sea nearest the city chosen."
			: "";
		Label message = new() {
			Text = $"{player.civilization.noun}: a {unitType} has joined our forces. Which city shall it report to?{where}",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(Width - 50, 0),
		};
		message.SetPosition(new Vector2(25, 55));
		AddChild(message);

		list = new() { CustomMinimumSize = new Vector2(Width - 50, 0) };
		foreach (City city in cities) {
			list.AddItem(Describe(city));
		}
		list.Select(0);
		list.SetPosition(new Vector2(25, 125));
		AddChild(list);

		AddConfirmButton(new Vector2(Width - 66, Height - 66), Choose);
	}

	// "Paris (size 5), garrison: Pikeman x2, Knight"
	private static string Describe(City city) {
		List<string> garrison = Staging.Garrison(city)
			.GroupBy(u => u.unitType.name)
			.OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
			.Select(g => g.Count() > 1 ? $"{g.Key} x{g.Count()}" : g.Key)
			.ToList();
		return $"{city.name} (size {city.residents.Count}), garrison: {(garrison.Count > 0 ? string.Join(", ", garrison) : "none")}";
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
