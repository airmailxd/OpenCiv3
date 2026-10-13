using Godot;
using C7Engine;
using C7GameData;

// Civ3's City Governor ("Contact Governor" on a city's right-click menu, or
// G on the city screen), with its wording from labels.txt (#GOVERNOR_WIN).
// Its General options are here; the Production ones ("Manage Production"
// and what to build) aren't available yet. The options go to the engine
// when the player confirms them, for this city or, as in Civ3, all their
// cities or those on its continent.
public partial class GovernorPopup : Popup {
	private const int Width = 420;
	private const int Height = 330;
	private const int FontSize = 13;

	private static readonly Color DisabledModulate = new(1, 1, 1, 0.45f);

	private readonly City city;
	private readonly CityGovernor governor;

	public GovernorPopup(City city) {
		alignment = BoxContainer.AlignmentMode.Center;
		margins = new Margins(top: 40);
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

		AddHeading(column, "General");
		AddOption(column, "Manage citizen moods", governor.manageMoods, on => governor.manageMoods = on,
			"Before the turn ends, and when the city grows, makes citizens entertainers so the city doesn't riot, "
			+ "and puts entertainers back to work once they aren't needed.");
		AddOption(column, "Emphasize Food", governor.emphasizeFood, on => governor.emphasizeFood = on,
			"Citizens favour tiles with more food.");
		AddOption(column, "Emphasize Production", governor.emphasizeProduction, on => governor.emphasizeProduction = on,
			"Citizens favour tiles with more shields.");
		AddOption(column, "Emphasize Commerce", governor.emphasizeCommerce, on => governor.emphasizeCommerce = on,
			"Citizens favour tiles with more commerce.");

		AddHeading(column, "Production");
		Civ3Checkbox manageProduction = MakeCheckbox("Manage Production", false, "Not available yet.");
		manageProduction.Disabled = true;
		manageProduction.Modulate = DisabledModulate;
		column.AddChild(manageProduction);

		HBoxContainer scopeRow = new();
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
		column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
		column.AddChild(scopeRow);

		AddConfirmButton(new Vector2(Width - 66, Height - 66), () => {
			GovernorScope chosen = (GovernorScope)scope.GetSelectedId();
			GetParent<PopupOverlay>().OnHidePopup();
			new MsgSetCityGovernor(city, governor, chosen).send();
		});
		AddCancelButton(new Vector2(Width - 106, Height - 66));
	}

	private static void AddHeading(VBoxContainer column, string text) {
		Label heading = new() { Text = text, CustomMinimumSize = new Vector2(0, 26), VerticalAlignment = VerticalAlignment.Bottom };
		heading.AddThemeFontSizeOverride("font_size", 16);
		column.AddChild(heading);
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
