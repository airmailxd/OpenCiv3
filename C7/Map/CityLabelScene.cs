using C7.Textures;
using C7GameData;
using Godot;

namespace C7.Map {
	public partial class CityLabelScene : Node2D {
		City city;
		public Vector2I tileCenter;
		Color civColor;

		const byte TRANSPARENCY = 192; // 25%
		const int CITY_LABEL_HEIGHT = 23;
		const int LEFT_RIGHT_BOXES_WIDTH = 24;
		const int LEFT_RIGHT_BOXES_HEIGHT = CITY_LABEL_HEIGHT - 2;
		const int CENTRAL_PANEL_SEPARATOR_WIDTH = 4;
		const int BUILDING_ICON_BOX_WIDTH = 25;

		// Loaded when first needed, and forgotten with the other city textures, since it can differ between games.
		static ImageTexture nonEmbassyStar;
		static ImageTexture NonEmbassyStar => nonEmbassyStar ??= TextureLoader.Load("icons.capital_star");

		static readonly FontFile smallFont;
		static readonly FontFile midSizedFont;
		static readonly Theme smallFontTheme = new();
		static readonly Theme popThemeRed = new();
		static readonly Theme popSizeTheme = new();

		PanelContainer labelPanel = new();
		HBoxContainer mainContainer = new();
		PanelContainer popSizePanel = new();
		VBoxContainer centerContainer = new();
		// Made when the city first becomes a capital.
		PanelContainer capitalPanel;
		// The barracks, harbor and airport icons, in the order they're shown after the capital star.
		readonly BuildingIcon[] buildingIcons = [new("barracks"), new("harbor"), new("airport")];
		HSeparator centerDivider = new();

		// The styles in the owner's color, kept so they can be recolored.
		StyleBoxFlat popStyle;
		StyleBoxLine centerSeparatorStyle;
		StyleBoxFlat capitalStyle;

		HSeparator borderTop = new();
		HSeparator borderBottom = new();

		VSeparator leftSeparator = new();
		VSeparator rightSeparator = new();

		Label cityNameLabel = new();
		Label productionLabel = new();
		Label popSizeLabel = new();

		// What the labels show, so they're only set when it changes.
		string cityNameText = null;
		string productionLabelText = null;
		string popSizeText = null;
		bool popSizeRed = false;

		Color topRowGrey = Color.Color8(32, 32, 32, TRANSPARENCY);
		Color bottomRowGrey = Color.Color8(48, 48, 48, TRANSPARENCY);
		Color backgroundGrey = Color.Color8(64, 64, 64, TRANSPARENCY);
		Color borderGrey = Color.Color8(80, 80, 80, TRANSPARENCY);

		// The fonts are loaded before the themes that use them are made.
		static CityLabelScene() {
			// The mid-sized font is the shared, cached one, used at its own size.
			midSizedFont = ResourceLoader.Load<FontFile>("res://Fonts/NotoSans-Regular.ttf");

			// The small font skips the cache, since setting its FixedSize would otherwise make everything using the font small.
			smallFont = ResourceLoader.Load<FontFile>("res://Fonts/NotoSans-Regular.ttf", null, ResourceLoader.CacheMode.Ignore);
			// Must set the FixedSize so Godot can calculate the width of the font for city labels
			smallFont.FixedSize = 11;

			smallFontTheme.DefaultFont = smallFont;
			smallFontTheme.SetColor("font_color", "Label", Color.Color8(255, 255, 255, 255));
			smallFontTheme.SetFontSize("font_size", "Label", 11);
			popSizeTheme.DefaultFont = midSizedFont;
			popSizeTheme.SetColor("font_color", "Label", Color.Color8(255, 255, 255, 255));
			popSizeTheme.SetFontSize("font_size", "Label", 18);
			popThemeRed.DefaultFont = midSizedFont;
			popThemeRed.SetColor("font_color", "Label", Color.Color8(255, 0, 0, 255));
			popThemeRed.SetFontSize("font_size", "Label", 18);
		}

		// Forgets the textures, which can differ between games.
		public static void ClearTextureCache() {
			nonEmbassyStar = null;
		}

		public CityLabelScene(City city) {
			this.city = city;

			civColor = CivColorOf(city);

			// Set up UI hierarchy
			AddChild(labelPanel);
			labelPanel.AddChild(mainContainer);

			// Left side (population)
			mainContainer.AddChild(popSizePanel);
			popSizePanel.AddChild(popSizeLabel);

			// Center (city name, production and population growth)
			mainContainer.AddChild(leftSeparator);
			mainContainer.AddChild(centerContainer);
			mainContainer.AddChild(rightSeparator);
			SetupCenterPanel();

			mainContainer.AddThemeConstantOverride("separation", 0);
			centerContainer.AddThemeConstantOverride("separation", 0);

			borderTop.AddThemeConstantOverride("separation", 1);
			borderBottom.AddThemeConstantOverride("separation", 1);
			centerDivider.AddThemeConstantOverride("separation", 1);

			popSizeLabel.HorizontalAlignment = HorizontalAlignment.Center;
			popSizeLabel.VerticalAlignment = VerticalAlignment.Center;
			popSizePanel.CustomMinimumSize = new Vector2(LEFT_RIGHT_BOXES_WIDTH, LEFT_RIGHT_BOXES_HEIGHT);

			labelPanel.MouseFilter = Control.MouseFilterEnum.Ignore;

			cityNameLabel.Theme = smallFontTheme;
			productionLabel.Theme = smallFontTheme;
			popSizeLabel.Theme = popSizeTheme;

			ApplyStyles();

			// The label is only updated when the city changes, and Godot settles its size afterwards, so keep it as small as its contents
			// and centered under the city whenever that happens.
			labelPanel.MinimumSizeChanged += ShrinkToContents;
			labelPanel.Resized += UpdatePosition;
		}

		// Points the label at the same city in new game data, e.g. a LAN snapshot.
		public void Rebind(City city) {
			this.city = city;
		}

		private static Color CivColorOf(City city) {
			return new Color(TextureLoader.LoadColor(city.owner.GetPlayerColor()), TRANSPARENCY);
		}

		// Shows the label in the colors of the city's owner, e.g. after the city was captured.
		public void UpdateCivColor() {
			Color color = CivColorOf(city);
			if (color == civColor) {
				return;
			}
			civColor = color;
			popStyle.BgColor = color;
			centerSeparatorStyle.Color = color;
			if (capitalStyle != null) {
				capitalStyle.BgColor = color;
			}
			foreach (BuildingIcon icon in buildingIcons) {
				if (icon.style != null) {
					icon.style.BgColor = color;
				}
			}
		}

		public void SetTileCenter(Vector2I tileCenter) {
			if (this.tileCenter == tileCenter) {
				return;
			}
			this.tileCenter = tileCenter;
			UpdatePosition();
		}

		private void ShrinkToContents() {
			labelPanel.Size = Vector2.Zero;
			UpdatePosition();
		}

		private void UpdatePosition() {
			labelPanel.Position = new Vector2(tileCenter.X - labelPanel.Size.X / 2, tileCenter.Y + 24);
		}

		private void SetupCenterPanel() {
			centerContainer.AddChild(borderTop);
			centerContainer.AddChild(cityNameLabel);
			centerContainer.AddChild(centerDivider);
			centerContainer.AddChild(productionLabel);
			centerContainer.AddChild(borderBottom);

			centerContainer.CustomMinimumSize = new Vector2(60, -1);

			cityNameLabel.HorizontalAlignment = HorizontalAlignment.Center;
			cityNameLabel.VerticalAlignment = VerticalAlignment.Center;

			productionLabel.HorizontalAlignment = HorizontalAlignment.Center;
			productionLabel.VerticalAlignment = VerticalAlignment.Center;
		}

		private void SetupCapitalPanel() {
			capitalPanel = MakeIconPanel(out capitalStyle);
			capitalPanel.AddChild(new TextureRect() {
				Texture = NonEmbassyStar,
				StretchMode = TextureRect.StretchModeEnum.KeepCentered
			});
		}

		// A box in the owner's color at the right of the label, for an icon.
		private PanelContainer MakeIconPanel(out StyleBoxFlat style) {
			PanelContainer panel = new();
			style = new() {
				BgColor = civColor
			};
			panel.AddThemeStyleboxOverride("panel", style);
			panel.CustomMinimumSize = new Vector2(LEFT_RIGHT_BOXES_WIDTH, LEFT_RIGHT_BOXES_HEIGHT);
			return panel;
		}

		// A box at the end of the label with the icon for one of the city's buildings, made when the city first has one.
		class BuildingIcon(string textureKey) {
			public readonly string textureKey = "city_label_icons." + textureKey;
			public PanelContainer panel;
			public StyleBoxFlat style;
			public Building shown;
		}

		// Shows the icon for the building at the end of the label, or removes it when there's no building.
		private void ShowBuildingIcon(BuildingIcon icon, Building building) {
			bool attached = icon.panel?.GetParent() == mainContainer;
			if (building == null) {
				if (attached) {
					mainContainer.RemoveChild(icon.panel);
				}
				return;
			}
			if (icon.panel == null) {
				icon.panel = MakeIconPanel(out icon.style);
				// Wide enough for Civ3's icons at their own size; larger ones are shrunk to fit.
				icon.panel.CustomMinimumSize = new Vector2(BUILDING_ICON_BOX_WIDTH, LEFT_RIGHT_BOXES_HEIGHT);
				icon.panel.AddChild(new TextureRect() {
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
				});
			}
			if (icon.shown != building) {
				icon.shown = building;
				icon.panel.GetChild<TextureRect>(0).Texture = TextureLoader.Load(icon.textureKey, building, useCache: true);
			}
			if (attached) {
				// Keep the icons in order after the capital star.
				mainContainer.MoveChild(icon.panel, -1);
			} else {
				mainContainer.AddChild(icon.panel);
			}
		}

		private void ApplyStyles() {
			// style for the main panel
			StyleBoxFlat labelStyle = new() {
				BgColor = backgroundGrey,
				BorderColor = borderGrey,
				BorderWidthBottom = 1,
				BorderWidthLeft = 1,
				BorderWidthRight = 1,
				BorderWidthTop = 1
			};

			popStyle = new() {
				BgColor = civColor
			};

			centerSeparatorStyle = new() {
				Color = civColor,
				GrowBegin = CENTRAL_PANEL_SEPARATOR_WIDTH,
				GrowEnd = CENTRAL_PANEL_SEPARATOR_WIDTH,
			};

			StyleBoxLine bottomBorderStyle = new() {
				Color = bottomRowGrey,
				GrowBegin = CENTRAL_PANEL_SEPARATOR_WIDTH,
				GrowEnd = CENTRAL_PANEL_SEPARATOR_WIDTH,
			};

			StyleBoxLine topBorderStyle = new() {
				Color = topRowGrey,
				GrowBegin = CENTRAL_PANEL_SEPARATOR_WIDTH,
				GrowEnd = CENTRAL_PANEL_SEPARATOR_WIDTH,
			};

			StyleBoxLine separatorStyle = new() {
				Color = new Color(0, 0, 0, 0), // transparent,
				Thickness = CENTRAL_PANEL_SEPARATOR_WIDTH,
				Vertical = true
			};

			labelPanel.AddThemeStyleboxOverride("panel", labelStyle);
			popSizePanel.AddThemeStyleboxOverride("panel", popStyle);
			centerDivider.AddThemeStyleboxOverride("separator", centerSeparatorStyle);
			borderTop.AddThemeStyleboxOverride("separator", topBorderStyle);
			borderBottom.AddThemeStyleboxOverride("separator", bottomBorderStyle);
			leftSeparator.AddThemeStyleboxOverride("separator", separatorStyle);
			rightSeparator.AddThemeStyleboxOverride("separator", separatorStyle);
		}

		// The label shows what the city is doing, which is worked out from its yields, so it's only updated when the city may have changed.
		// As in Civ3, other players' cities only show their name and size, not their growth or production.
		public void UpdateContent(bool showDetails) {
			centerDivider.Visible = showDetails;
			productionLabel.Visible = showDetails;

			int turnsUntilGrowth = showDetails ? city.TurnsUntilGrowth() : 0;
			string turnsUntilGrowthText = turnsUntilGrowth == int.MaxValue || turnsUntilGrowth < 0 ? "- -" : "" + turnsUntilGrowth;

			string productionText;
			if (!showDetails) {
				productionText = "";
			} else if (city.itemBeingProduced != null) {
				int turnsUntilProductionFinished = city.TurnsUntilProductionFinished();
				productionText = turnsUntilProductionFinished == int.MaxValue
					? $"{city.itemBeingProduced.name} : --"
					: $"{city.itemBeingProduced.name} : {turnsUntilProductionFinished}";
			} else {
				productionText = "-- : --";
			}

			SetText(cityNameLabel, ref cityNameText, showDetails ? $"{city.name} : {turnsUntilGrowthText}" : city.name);
			SetText(productionLabel, ref productionLabelText, productionText);
			SetText(popSizeLabel, ref popSizeText, city.residents.Count.ToString());

			// Update population label color based on growth
			bool shrinking = turnsUntilGrowth < 0;
			if (popSizeRed != shrinking) {
				popSizeRed = shrinking;
				popSizeLabel.Theme = shrinking ? popThemeRed : popSizeTheme;
			}

			// Update the panel with the capital star
			bool hasCapitalIndicator = capitalPanel?.GetParent() == mainContainer;

			if (city.IsCapital() && !hasCapitalIndicator) {
				if (capitalPanel == null) {
					SetupCapitalPanel();
				}
				mainContainer.AddChild(capitalPanel);
			} else if (!city.IsCapital() && hasCapitalIndicator) {
				mainContainer.RemoveChild(capitalPanel);
			}

			// Then the barracks, harbor and airport icons, which like growth and production are only shown for the player's own cities.
			ShowBuildingIcon(buildingIcons[0], showDetails ? city.Barracks() : null);
			ShowBuildingIcon(buildingIcons[1], showDetails ? city.Harbor() : null);
			ShowBuildingIcon(buildingIcons[2], showDetails ? city.Airport() : null);

			// Force the layout to recalculate
			ShrinkToContents();
		}

		// Label.Text crosses into the engine and relayouts the label, so only set it when it changes.
		private static void SetText(Label label, ref string current, string text) {
			if (current != text) {
				current = text;
				label.Text = text;
			}
		}
	}
}
