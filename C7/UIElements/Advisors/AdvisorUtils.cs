using C7Engine;
using C7GameData;
using Godot;

public static class AdvisorUtils {
	public static TextureButton CreateExitButton(Control parent, Vector2? position = null) {
		Vector2 buttonPosition = position ?? new Vector2(952, 720);

		TextureButton btn = new();
		TextureLoader.SetButtonTextures(btn, "ui.exit");
		btn.SetPosition(buttonPosition);
		parent.AddChild(btn);
		return btn;
	}

	public static TextureRect CreateAdvisorHead(Control parent, AdvisorHead.Advisor advisor, Vector2? position = null) {
		Vector2 headPosition = position ?? new Vector2(851, 0);

		TextureRect advisorHead = new();
		advisorHead.Texture = AdvisorHead.GetPopupImage(advisor, AdvisorHead.Mood.Happy, eraIndex: 0);
		advisorHead.SetPosition(headPosition);
		parent.AddChild(advisorHead);

		return advisorHead;
	}

	// The texture config key of each advisor's face in the sidebar.
	private static string SidebarKey(AdvisorHead.Advisor advisor) => advisor switch {
		AdvisorHead.Advisor.Domestic => "domestic",
		AdvisorHead.Advisor.Trade => "trade",
		AdvisorHead.Advisor.Military => "military",
		AdvisorHead.Advisor.Foreign => "foreign",
		AdvisorHead.Advisor.Culture => "culture",
		AdvisorHead.Advisor.Science => "science",
		_ => throw new System.ArgumentOutOfRangeException(nameof(advisor)),
	};

	/// <summary>
	/// Adds the column of advisor faces down the left edge of an advisor
	/// screen. The face of the advisor being shown is highlighted; clicking
	/// another face switches to that advisor.
	/// </summary>
	public static void CreateAdvisorSidebar(Control parent, AdvisorHead.Advisor current, Vector2? position = null) {
		Vector2 sidebarPosition = position ?? new Vector2(5, 244);
		const int spacing = 62;

		foreach (AdvisorHead.Advisor advisor in System.Enum.GetValues<AdvisorHead.Advisor>()) {
			string key = "advisors.sidebar." + SidebarKey(advisor);
			TextureButton face = new();
			if (advisor == current) {
				ImageTexture active = TextureLoader.Load(key + ".active");
				face.TextureNormal = active;
				face.TextureDisabled = active;
				face.Disabled = true;
			} else {
				TextureLoader.SetButtonTextures(face, key);
				face.Pressed += () => FindAdvisors(parent)?.ShowAdvisor(advisor);
			}
			face.SetPosition(sidebarPosition + new Vector2(0, spacing * (int)advisor));
			parent.AddChild(face);
		}
	}

	private static Advisors FindAdvisors(Node node) {
		for (Node n = node; n != null; n = n.GetParent()) {
			if (n is Advisors advisors) {
				return advisors;
			}
		}
		return null;
	}

	private static readonly StyleBoxEmpty emptyStyleBox = new();

	// A black label for the advisor screens, for a container (or placed
	// by the caller).
	public static Label MakeLabel(string text, int fontSize = 13, float width = 0,
			HorizontalAlignment align = HorizontalAlignment.Left) {
		Label label = new() {
			Text = text,
			HorizontalAlignment = align,
			VerticalAlignment = VerticalAlignment.Center,
			// Trimming makes a label's minimum width just the ellipsis, so
			// only labels given a width trim their text.
			ClipText = width > 0,
			TextOverrunBehavior = width > 0 ? TextServer.OverrunBehavior.TrimEllipsis : TextServer.OverrunBehavior.NoTrimming,
			CustomMinimumSize = new Vector2(width, 0),
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", Colors.Black);
		return label;
	}

	// A label that wraps its text over as many lines as it needs.
	public static Label MakeWrappedLabel(string text, int fontSize, float width) {
		Label label = MakeLabel(text, fontSize);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(width, 0);
		return label;
	}

	// A label placed in a box of the background, with the text aligned in it.
	public static Label CreateLabel(Control parent, string text, Rect2 box, int fontSize = 13,
			HorizontalAlignment align = HorizontalAlignment.Left) {
		Label label = MakeLabel(text, fontSize, box.Size.X, align);
		label.Position = box.Position;
		label.Size = box.Size;
		parent.AddChild(label);
		return label;
	}

	// A list that scrolls vertically within the given area of the
	// background, for rows of the given height so they sit on its ruled lines.
	public static VBoxContainer CreateList(Control parent, Rect2 area) {
		ScrollContainer scroll = new() {
			Position = area.Position,
			Size = area.Size,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		VBoxContainer list = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		list.AddThemeConstantOverride("separation", 0);
		scroll.AddChild(list);
		parent.AddChild(scroll);
		return list;
	}

	public static void ClearList(Container list) {
		foreach (Node child in list.GetChildren()) {
			list.RemoveChild(child);
			child.QueueFree();
		}
	}

	// A row of a list, with its items side by side.
	public static HBoxContainer MakeRow(float height, params Control[] items) {
		HBoxContainer row = new() { CustomMinimumSize = new Vector2(0, height) };
		row.AddThemeConstantOverride("separation", 6);
		foreach (Control item in items) {
			row.AddChild(item);
		}
		return row;
	}

	public static Control MakeSpacer(float width) {
		PanelContainer spacer = new() { CustomMinimumSize = new Vector2(width, 0) };
		spacer.AddThemeStyleboxOverride("panel", emptyStyleBox);
		return spacer;
	}

	public static TextureRect MakeIcon(Texture2D texture, float size) {
		return new TextureRect {
			Texture = texture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(size, size),
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
		};
	}

	// A flat button with the city's name that opens the city screen.
	public static Button MakeCityButton(Control advisor, City city, float width) {
		Button button = new() {
			Text = city.name,
			Flat = true,
			Alignment = HorizontalAlignment.Left,
			ClipText = true,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
			CustomMinimumSize = new Vector2(width, 0),
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
		};
		button.AddThemeColorOverride("font_color", Colors.Black);
		button.AddThemeColorOverride("font_hover_color", new Color(0.45f, 0, 0.45f));
		button.AddThemeFontSizeOverride("font_size", 13);
		button.Pressed += () => {
			FindAdvisors(advisor)?.Hide();
			new MsgShowCityScreen(city).send();
		};
		return button;
	}

	// How the advisors name another civ, as in "the Romans".
	public static string CivName(Player player) => player.civilization?.noun ?? player.civilization?.name ?? "";

	public static (TextureButton box, Label label) CreateAdvisorDialogBox(Control parent, Vector2? position = null) {
		Vector2 boxPosition = position ?? new Vector2(806, 110);
		Vector2 labelPosition = boxPosition + new Vector2(9, 9);

		ImageTexture dialogBoxTexture = TextureLoader.Load("advisors.dialog_box");
		TextureButton dialogBox = new TextureButton();
		dialogBox.TextureNormal = dialogBoxTexture;
		dialogBox.SetPosition(boxPosition);
		parent.AddChild(dialogBox);

		// The advice wraps within the box, leaving room for its border.
		Label dialogBoxLabel = new();
		dialogBoxLabel.Text = "You are running OpenCiv3!";
		dialogBoxLabel.SetPosition(labelPosition);
		dialogBoxLabel.Size = dialogBoxTexture.GetSize() - new Vector2(18, 18);
		dialogBoxLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		parent.AddChild(dialogBoxLabel);

		return (dialogBox, dialogBoxLabel);
	}

	public static Label CreateAdvisorTitle(Control parent, float containerWidth, string advisorTitleString) {
		int bigFontSize = 26;
		// int middleFontSize = 20;
		int bigFontGlyphSpacing = 14;
		int bigFontGlyphSpaceSpacing = 22;

		FontFile regularFont = ResourceLoader.Load<FontFile>("res://Fonts/NotoSans-Regular.ttf");
		Theme regularBigFontTheme = new();
		regularBigFontTheme.DefaultFont = regularFont;
		regularBigFontTheme.SetFontSize("font_size", "Label", bigFontSize);

		FontVariation fontVariation = new FontVariation
		{
			BaseFont = regularFont,
			SpacingGlyph = bigFontGlyphSpacing,
			SpacingSpace = bigFontGlyphSpaceSpacing,
		};

		Theme regularThemeWithCustomSpacing = new Theme();
		regularThemeWithCustomSpacing.SetFont("font", "Label", fontVariation);
		regularThemeWithCustomSpacing.SetFontSize("font_size", "Label", bigFontSize);

		float advisorTitleStringWidth = GetStringSizeWithCustomSpacing(regularFont, advisorTitleString, bigFontSize,
			bigFontGlyphSpacing, bigFontGlyphSpaceSpacing).X;

		float advisorTitleOffsetLeft = (containerWidth / 2.0f) - (advisorTitleStringWidth) / 2.0f;

		Label advisorTitle = new() {
			Text = advisorTitleString,
			OffsetLeft = advisorTitleOffsetLeft,
			OffsetTop = 15,
			Theme = regularThemeWithCustomSpacing,
		};
		parent.AddChild(advisorTitle);

		return advisorTitle;
	}

	private static Vector2 GetStringSizeWithCustomSpacing(Font font, string input, int fontSize = 16, int glyphSpacing = 0, int glyphSpaceSpacing = 0) {

		float extraSpacing = 0.0f;
		for (int i = 0; i < input.Length; i++) {
			if (i < input.Length - 1) {
				if (char.IsWhiteSpace(input[i]) && glyphSpaceSpacing > 0) {
					extraSpacing += glyphSpaceSpacing;
				} else {
					extraSpacing += glyphSpacing;
				}
			}
		}

		Vector2 originalSize = font.GetStringSize(input, fontSize: fontSize);
		return new Vector2(originalSize.X + extraSpacing, originalSize.Y);
	}
}

// Research estimates for screens that show many of them at once. The beakers
// a player makes per turn are added up once (over all their cities) and
// passed in, rather than for every tech; the formula itself is the engine's.
public static class ScienceEstimates {
	public static int BeakersPerTurn(C7GameData.Player player) => player.BeakersPerTurn();

	public static int TurnsToResearch(C7GameData.GameData gameData, C7GameData.Player player, C7GameData.Tech tech, int beakersPerTurn) =>
		player.EstimateTurnsToResearch(gameData, tech, beakersPerTurn);

	public static string SummarizeScience(C7GameData.GameData gameData, C7GameData.Player player, int beakersPerTurn) =>
		player.SummarizeScience(gameData, beakersPerTurn);
}
