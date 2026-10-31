using Godot;
using C7GameData;
using C7Engine;
using System;
using System.Collections.Generic;

// Tells the player a city has finished building something, as in Civ 3: they
// can carry on with what the city builds next, pick something else from the
// dropdown, or zoom to the city.
public partial class CityProductionPopup : Popup {
	private readonly City city;
	private readonly string completed;
	private readonly Action onZoom;
	private readonly Action onDone;

	// The options in the dropdown, in order.
	private readonly List<IProducible> options = new();
	// What the city builds as far as the popup knows: what it had, then
	// whatever was last picked.
	private IProducible chosen;

	public CityProductionPopup(City city, string completed, Action onZoom, Action onDone) {
		alignment = BoxContainer.AlignmentMode.Center;
		margins = new Margins(top: 100);
		this.city = city;
		this.completed = completed;
		this.onZoom = onZoom;
		this.onDone = onDone;
	}

	public override void _Ready() {
		base._Ready();

		const int width = 480;
		const int height = 230;
		AddTexture(width, height);
		AddBackground(width, height);
		AddHeader(city.name, 10);

		Label messageLabel = new() { Text = $"{city.name} has completed {completed}.\nWhat shall we build next?" };
		messageLabel.SetPosition(new Vector2(25, 55));
		AddChild(messageLabel);

		OptionButton optionButton = MakeOptionButton();
		optionButton.SetPosition(new Vector2(25, 105));
		optionButton.CustomMinimumSize = new Vector2(width - 50, 0);
		AddChild(optionButton);

		AddButton($"Zoom to {city.name}", 150, () => {
			Close();
			onZoom();
		});
		AddButton("OK. Sounds good.", 180, () => {
			Close();
			onDone();
		});
		AddConfirmButton(new Vector2(width - 40, height - 40), () => {
			Close();
			onDone();
		});
	}

	private void Close() {
		GetParent().EmitSignal(PopupOverlay.SignalName.HidePopup);
	}

	// The item picked in the dropdown changes production at once, as in
	// Civ3, so it holds however the popup is closed: Escape used to close it
	// without sending the choice, and the city went back to what it had
	// (owner's report). Closing without picking anything leaves the city
	// building what it already was.
	private void Choose(int selected) {
		if (selected < 0 || selected >= options.Count || options[selected] == chosen) {
			return;
		}
		chosen = options[selected];
		new MsgChooseProduction(city.id, chosen.name).send();
	}

	private OptionButton MakeOptionButton() {
		OptionButton optionButton = new();
		StyleBoxFlat styleBox = new() {
			BorderColor = Color.Color8(50, 50, 50, 220),
			BorderWidthBottom = 2,
			BorderWidthLeft = 2,
			BorderWidthRight = 2,
			BorderWidthTop = 2,
			ContentMarginLeft = 4,
			ContentMarginRight = 4,
			ContentMarginTop = 4,
			ContentMarginBottom = 4
		};
		optionButton.AddThemeStyleboxOverride("normal", styleBox);
		optionButton.AddThemeStyleboxOverride("hover", styleBox);
		optionButton.AddThemeStyleboxOverride("pressed", styleBox);
		PopupMenu popup = optionButton.GetPopup();
		// The menu is drawn with the theme's panel (C7Theme.tres), opaque and
		// readable, not the button's see-through box.

		EngineStorage.ReadGameData((GameData gameData) => {
			foreach (IProducible option in city.ListProductionOptions(gameData)) {
				int turns = city.TurnsToProduce(option);
				string turnsStr = turns == int.MaxValue ? "--" : $"{turns}";
				// Say how many shields switching would waste, or why it can't.
				string warning = city.ProductionChangeWarning(option);
				optionButton.AddIconItem(RightClickChooseProductionMenu.GetProducibleIcon(option, city.owner),
					warning == null ? $"{option.name} ({turnsStr} turns)" : $"{option.name} ({turnsStr} turns; {warning})");
				optionButton.SetItemDisabled(options.Count, !city.CanChangeProduction(option));
				if (option == city.itemBeingProduced) {
					optionButton.Select(options.Count);
				}
				options.Add(option);
			}
		});

		for (int i = 0; i < popup.ItemCount; ++i) {
			popup.SetItemAsRadioCheckable(i, false);
			popup.SetItemAsCheckable(i, false);
		}
		chosen = city.itemBeingProduced;
		optionButton.ItemSelected += index => Choose((int)index);
		return optionButton;
	}
}
