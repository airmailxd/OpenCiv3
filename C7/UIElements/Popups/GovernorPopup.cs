using Godot;
using C7Engine;
using C7GameData;

// Civ3's City Governor ("Contact Governor" on a city's right-click menu, or
// G on the city screen), with its wording from labels.txt (#GOVERNOR_WIN).
// As in Civ3 (FUN_00544ef0) it has two pages: General, with the yes-or-no
// options, and Production, with how often to produce each kind of thing.
// The options go to the engine when the player confirms them, for this city
// or, as in Civ3, all their cities or those on its continent.
public partial class GovernorPopup : Popup {
	private const int Width = 440;
	private const int Height = 590;
	private const int FontSize = 13;

	// The Production rows, in Civ3's order, with their labels.txt wording.
	private static readonly (ProductionKind kind, string label, string tooltip)[] ProductionRows = [
		(ProductionKind.OffensiveGroundUnits, "Produce Offensive Ground Units", "Land units with the offense strategy, and armies."),
		(ProductionKind.DefensiveGroundUnits, "Produce Defensive Ground Units", "Land units with the defense strategy."),
		(ProductionKind.Artillery, "Produce Artillery", "Artillery, missiles and nuclear weapons."),
		(ProductionKind.Settlers, "Produce Settlers", "Units that found cities."),
		(ProductionKind.Workers, "Produce Workers", "Units that improve terrain."),
		(ProductionKind.NavalUnits, "Produce Naval Units", "Warships, transports and carriers."),
		(ProductionKind.AirUnits, "Produce Air Units", "Bombers, fighters and air transports."),
		(ProductionKind.Growth, "Produce Growth", "Improvements that help the city grow."),
		(ProductionKind.Production, "Produce Production", "Improvements that add shields or cut waste."),
		(ProductionKind.Happiness, "Produce Happiness", "Improvements that make citizens content."),
		(ProductionKind.Science, "Produce Science", "Improvements that add research."),
		(ProductionKind.Wealth, "Produce Wealth", "Improvements that add gold, and Wealth."),
		(ProductionKind.Trade, "Produce Trade", "Improvements that add trade or trade routes."),
		(ProductionKind.Exploration, "Produce Exploration", "Explorers, and units that explore."),
		(ProductionKind.Culture, "Produce Culture", "Improvements that add culture."),
	];

	private readonly City city;
	private readonly CityGovernor governor;

	private VBoxContainer generalPage;
	private VBoxContainer productionPage;

	public GovernorPopup(City city) {
		alignment = BoxContainer.AlignmentMode.Center;
		margins = new Margins(top: 20);
		this.city = city;
		// A copy, so nothing changes until the player confirms.
		governor = city.governor.Copy();
	}

	public override void _Ready() {
		base._Ready();

		AddTexture(Width, Height);
		AddBackground(Width, Height);
		AddHeader("City Governor", 10);

		VBoxContainer column = new() { Position = new Vector2(36, 50), CustomMinimumSize = new Vector2(Width - 72, 0) };
		column.AddThemeConstantOverride("separation", 2);
		AddChild(column);

		Label name = new() { Text = city.name, CustomMinimumSize = new Vector2(0, 22) };
		name.AddThemeFontSizeOverride("font_size", 14);
		column.AddChild(name);

		// The page tabs.
		HBoxContainer tabs = new();
		tabs.AddThemeConstantOverride("separation", 6);
		Button generalTab = MakeTab("General");
		Button productionTab = MakeTab("Production");
		ButtonGroup group = new();
		generalTab.ButtonGroup = group;
		productionTab.ButtonGroup = group;
		generalTab.ButtonPressed = true;
		tabs.AddChild(generalTab);
		tabs.AddChild(productionTab);
		column.AddChild(tabs);
		column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });

		generalPage = new VBoxContainer();
		generalPage.AddThemeConstantOverride("separation", 2);
		column.AddChild(generalPage);
		AddOption(generalPage, "Manage citizen moods", governor.manageMoods, on => governor.manageMoods = on,
			"Before the turn ends, and when the city grows, makes citizens entertainers so the city doesn't riot, "
			+ "and puts entertainers back to work once they aren't needed.");
		AddOption(generalPage, "Emphasize Food", governor.emphasizeFood, on => governor.emphasizeFood = on,
			"Citizens favour tiles with more food.");
		AddOption(generalPage, "Emphasize Production", governor.emphasizeProduction, on => governor.emphasizeProduction = on,
			"Citizens favour tiles with more shields.");
		AddOption(generalPage, "Emphasize Commerce", governor.emphasizeCommerce, on => governor.emphasizeCommerce = on,
			"Citizens favour tiles with more commerce.");
		AddOption(generalPage, "Manage Production", governor.manageProduction, on => governor.manageProduction = on,
			"When the city finishes something and nothing is queued, the governor chooses what it builds next, "
			+ "following the Production page, instead of the city building the same unit again.");
		AddOption(generalPage, "Never build Wonders", governor.neverBuildWonders, on => governor.neverBuildWonders = on,
			"The governor never chooses a great wonder.");
		AddOption(generalPage, "Never build Small Wonders", governor.neverBuildSmallWonders, on => governor.neverBuildSmallWonders = on,
			"The governor never chooses a small wonder.");

		productionPage = new VBoxContainer { Visible = false };
		productionPage.AddThemeConstantOverride("separation", 2);
		column.AddChild(productionPage);
		foreach ((ProductionKind kind, string label, string tooltip) in ProductionRows) {
			AddFrequencyRow(productionPage, kind, label, tooltip);
		}

		generalTab.Pressed += () => ShowPage(general: true);
		productionTab.Pressed += () => ShowPage(general: false);

		HBoxContainer scopeRow = new() { Position = new Vector2(36, Height - 64) };
		scopeRow.AddThemeConstantOverride("separation", 8);
		Label inLabel = new() { Text = "in", VerticalAlignment = VerticalAlignment.Center };
		inLabel.AddThemeFontSizeOverride("font_size", FontSize);
		scopeRow.AddChild(inLabel);
		OptionButton scope = new() { FocusMode = FocusModeEnum.None, TooltipText = "Which of your cities these options are for." };
		scope.AddItem("This City", (int)GovernorScope.ThisCity);
		scope.AddItem("All Cities", (int)GovernorScope.AllCities);
		scope.AddItem("Continental Cities", (int)GovernorScope.ContinentalCities);
		scope.Select(0);
		scopeRow.AddChild(scope);
		AddChild(scopeRow);

		AddConfirmButton(new Vector2(Width - 66, Height - 66), () => {
			GovernorScope chosen = (GovernorScope)scope.GetSelectedId();
			GetParent<PopupOverlay>().OnHidePopup();
			new MsgSetCityGovernor(city, governor, chosen).send();
		});
		AddCancelButton(new Vector2(Width - 106, Height - 66));
	}

	private void ShowPage(bool general) {
		generalPage.Visible = general;
		productionPage.Visible = !general;
	}

	private static Button MakeTab(string text) {
		Button tab = new() { Text = text, ToggleMode = true, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(110, 0) };
		tab.AddThemeFontSizeOverride("font_size", 15);
		return tab;
	}

	private void AddFrequencyRow(VBoxContainer page, ProductionKind kind, string text, string tooltip) {
		HBoxContainer row = new();
		Label label = new() {
			Text = text,
			TooltipText = tooltip,
			MouseFilter = MouseFilterEnum.Pass,
			VerticalAlignment = VerticalAlignment.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		label.AddThemeFontSizeOverride("font_size", FontSize);
		row.AddChild(label);
		OptionButton frequency = new() { FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(100, 0) };
		frequency.AddThemeFontSizeOverride("font_size", FontSize);
		frequency.AddItem("Never", (int)CityGovernor.Frequency.Never);
		frequency.AddItem("Sometimes", (int)CityGovernor.Frequency.Sometimes);
		frequency.AddItem("Often", (int)CityGovernor.Frequency.Often);
		frequency.Select(frequency.GetItemIndex((int)governor.HowOften(kind)));
		frequency.ItemSelected += index => governor.SetHowOften(kind, (CityGovernor.Frequency)frequency.GetItemId((int)index));
		row.AddChild(frequency);
		page.AddChild(row);
	}

	private static Civ3Checkbox MakeCheckbox(string text, bool pressed, string tooltip) {
		return new Civ3Checkbox {
			Text = text,
			FontSize = FontSize,
			ToggleMode = true,
			ButtonPressed = pressed,
			TooltipText = tooltip,
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
			FocusMode = FocusModeEnum.None,
		};
	}

	private static void AddOption(VBoxContainer column, string text, bool pressed, System.Action<bool> set, string tooltip) {
		Civ3Checkbox checkbox = MakeCheckbox(text, pressed, tooltip);
		checkbox.Toggled += on => set(on);
		column.AddChild(checkbox);
	}
}
