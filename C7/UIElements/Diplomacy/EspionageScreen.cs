using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using Godot;

// The Espionage screen, opened by the "E" button. As the Civilopedia describes it (GCON_Espionage,
// GCON_Espionage_Missions): "The Espionage screen is comprised of: current
// opponents, available missions, your current asset information, city
// selection, and operational cost options." A mission is sent by picking
// the target civilization in the opponents list ("followed by icons that
// indicate your Espionage status within that civilization": embassy and
// spy), the mission type (Diplomatic or Spy) and the mission, the target
// city "(if appropriate)" and the operational cost, then Execute; Cancel
// or leaving the screen sends nothing.
//
// The screen is drawn like the advisors' (its art is the game's own, made by
// ArtGen/screens/espionage.py: C7/ModernArt/Art/Espionage): the advisors'
// sandstone and paper, with three columns under taupe bands. On the left the
// rivals, a plate each with the leader's face, the name and the embassy and
// spy marks; in the middle the mission type (two tabs), the missions with
// their check boxes, and two cards, the briefing (the mission picked, or why
// it can't be sent) and our assets; on the right the target cities, the
// three operational costs and Execute and Cancel. The words are the old
// labels where there were some: "Select Mission", "Unmet", the assets'
// "Diplomatic Advance:", "Espionage Advance:", "Intelligence Center:", "Spy
// Experience:", "Immediately", "Carefully", "Safely" and "Execute".
public partial class EspionageScreen : Control {
	private static readonly Vector2 WindowSize = new(1024, 768);
	private static readonly Color Parchment = Color.Color8(232, 214, 168);
	private static readonly Color Ink = Color.Color8(40, 28, 16);
	private static readonly Color Faded = Color.Color8(110, 100, 90);
	private static readonly Color PickedInk = Color.Color8(150, 60, 0);
	private static readonly Color UnavailableInk = Color.Color8(78, 70, 62);

	// The layout: the same numbers as ArtGen/screens/espionage.py's bands
	// and cards, which the background has.
	private static readonly Rect2 RivalsBand = new(80, 78, 300, 24);
	private static readonly Rect2 MissionsBand = new(396, 78, 284, 24);
	private static readonly Rect2 TargetBand = new(696, 78, 272, 24);
	private static readonly Rect2 CostBand = new(696, 452, 272, 24);
	private static readonly Rect2 BriefingCard = new(396, 340, 284, 206);
	private static readonly Rect2 AssetsCard = new(396, 560, 284, 144);
	private const float CardHead = 22;

	private static readonly Rect2 CivListArea = new(80, 110, 288, 594);
	private static readonly Vector2 CivButtonSize = new(288, 48);
	private const int CivButtonGap = 6;
	// The width of the square at the left end of a rival's plate, which
	// holds the leader's face.
	private const float CivButtonTab = 46;
	private static readonly Rect2 CivScrollBar = new(372, 110, 8, 594);

	private static readonly Rect2 DiplomaticTab = new(396, 110, 140, 28);
	private static readonly Rect2 SpyTab = new(540, 110, 140, 28);
	private static readonly Vector2 FirstBox = new(398, 150);
	private const int BoxPitch = 30;

	private static readonly Rect2 CityListArea = new(696, 110, 260, 330);
	private static readonly Vector2 BarSize = new(260, 28);
	private const int BarGap = 4;
	private static readonly Rect2 CityScrollBar = new(960, 110, 8, 330);

	private static readonly Rect2[] RiskBars = [new(696, 484, 272, 34), new(696, 524, 272, 34), new(696, 564, 272, 34)];
	private static readonly Rect2 ExecuteBox = new(696, 664, 132, 36);
	private static readonly Rect2 CancelBox = new(836, 664, 132, 36);
	private static readonly Rect2 AssetsArea = new(406, 588, 264, 110);
	private static readonly Rect2 InfoArea = new(406, 368, 264, 170);

	private enum MissionType {
		Diplomatic,
		Spy,
	}

	// What is picked.
	private ID selectedCiv;
	private MissionType missionType = MissionType.Diplomatic;
	private EspionageMission? selectedMission;
	private City selectedCity;
	private EspionageRisk risk = EspionageRisk.Immediately;

	private VBoxContainer civList;
	private ScrollContainer civScroll;
	private VBoxContainer cityList;
	private ScrollContainer cityScroll;
	private Control missionBoxes;
	private readonly Button[] riskButtons = new Button[3];
	private Button diplomaticButton;
	private Button spyButton;
	private Label assets;
	private Label info;
	private Label cityHint;
	private Button execute;
	private Button cancel;

	public EspionageScreen() {
		MouseFilter = MouseFilterEnum.Stop;
		CustomMinimumSize = WindowSize;
		Size = WindowSize;
	}

	public override void _Ready() {
		AdvisorUtils.CreateUI(this, CreateUI);
	}

	private static ImageTexture Art(string key) => TextureLoader.TryLoad("espionage." + key);

	private void CreateUI() {
		ImageTexture background = Art("background");
		if (background != null) {
			AddChild(new TextureRect { Texture = background, Size = WindowSize, MouseFilter = MouseFilterEnum.Ignore });
		} else {
			// Without the art, a plain page with the bands and cards drawn
			// in.
			AddChild(new ColorRect { Color = Parchment, Size = WindowSize, MouseFilter = MouseFilterEnum.Ignore });
			Color band = Color.Color8(190, 174, 140);
			foreach (Rect2 r in new[] { RivalsBand, MissionsBand, TargetBand, CostBand }) {
				AddChild(new ColorRect { Color = band, Position = r.Position, Size = r.Size, MouseFilter = MouseFilterEnum.Ignore });
			}
			foreach (Rect2 r in new[] { BriefingCard, AssetsCard }) {
				AddChild(new ColorRect { Color = Colors.Black, Position = r.Position - Vector2.One, Size = r.Size + 2 * Vector2.One, MouseFilter = MouseFilterEnum.Ignore });
				AddChild(new ColorRect { Color = Color.Color8(242, 240, 229), Position = r.Position, Size = r.Size, MouseFilter = MouseFilterEnum.Ignore });
				AddChild(new ColorRect { Color = Color.Color8(195, 193, 182), Position = r.Position, Size = new Vector2(r.Size.X, CardHead), MouseFilter = MouseFilterEnum.Ignore });
			}
		}

		AdvisorUtils.CreateAdvisorTitle(this, WindowSize.X, "ESPIONAGE");

		// The headings, on the bands and the cards' heads.
		Heading("Rivals", RivalsBand);
		Heading("Missions", MissionsBand);
		Heading("Target City", TargetBand);
		Heading("Operational Cost", CostBand);
		Heading("Briefing", new Rect2(BriefingCard.Position, new Vector2(BriefingCard.Size.X, CardHead)), 13);
		Heading("Our Assets", new Rect2(AssetsCard.Position, new Vector2(AssetsCard.Size.X, CardHead)), 13);

		// The rivals.
		(civScroll, civList) = MakeScrollList(CivListArea, CivButtonGap, CivScrollBar);

		// The mission type, on two tabs under the missions' band.
		diplomaticButton = MakePlateButton("Diplomatic", DiplomaticTab);
		diplomaticButton.Alignment = HorizontalAlignment.Center;
		diplomaticButton.Pressed += () => PickMissionType(MissionType.Diplomatic);
		spyButton = MakePlateButton("Spy", SpyTab);
		spyButton.Alignment = HorizontalAlignment.Center;
		spyButton.Pressed += () => PickMissionType(MissionType.Spy);

		// The missions, a check box each under the tabs.
		missionBoxes = new Control { Position = Vector2.Zero, Size = WindowSize, MouseFilter = MouseFilterEnum.Ignore };
		AddChild(missionBoxes);

		// The target cities.
		(cityScroll, cityList) = MakeScrollList(CityListArea, BarGap, CityScrollBar);
		cityHint = AdvisorUtils.CreateLabel(this, "", new Rect2(CityListArea.Position + new Vector2(8, 6), new Vector2(CityListArea.Size.X - 16, 60)), 13);
		cityHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		cityHint.VerticalAlignment = VerticalAlignment.Top;
		cityHint.AddThemeColorOverride("font_color", Faded);

		// The operational costs, on three plates under their band, in
		// script.txt's order (#ESPIONAGE_SAFETY_LEVEL).
		for (int i = 0; i < 3; i++) {
			EspionageRisk r = (EspionageRisk)i;
			riskButtons[i] = MakePlateButton("", RiskBars[i]);
			riskButtons[i].Pressed += () => {
				risk = r;
				Refresh();
			};
		}

		// Execute and Cancel, at the foot of the right column.
		execute = MakeActionButton("Execute", ExecuteBox);
		execute.Pressed += Execute;
		cancel = MakeActionButton("Cancel", CancelBox);
		cancel.Pressed += () => {
			selectedMission = null;
			Refresh();
		};

		info = AdvisorUtils.CreateLabel(this, "", InfoArea, 13);
		info.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		info.VerticalAlignment = VerticalAlignment.Top;
		info.AddThemeColorOverride("font_color", Ink);

		assets = AdvisorUtils.CreateLabel(this, "", AssetsArea, 13);
		assets.ClipText = false;
		assets.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
		assets.AddThemeConstantOverride("line_spacing", 1);
		assets.VerticalAlignment = VerticalAlignment.Top;
		assets.AddThemeColorOverride("font_color", Ink);

		TextureButton close = AdvisorUtils.CreateExitButton(this);
		close.Pressed += () => { this.GetParent<Advisors>().Hide(); };
	}

	private void Heading(string text, Rect2 box, int fontSize = 14) {
		Label label = AdvisorUtils.CreateLabel(this, text, new Rect2(box.Position + new Vector2(8, 0), box.Size - new Vector2(16, 0)), fontSize);
		label.AddThemeColorOverride("font_color", Ink);
		label.MouseFilter = MouseFilterEnum.Ignore;
	}

	// The plate an entry or a choice is drawn on, in a state (normal, hover,
	// selected or disabled), stretched to the control's size; a plain box
	// without the art.
	private static StyleBox Plate(string state) {
		if (Art("plate." + state) is ImageTexture texture) {
			StyleBoxTexture box = new() { Texture = texture };
			box.SetTextureMarginAll(8);
			box.SetContentMarginAll(4);
			box.ContentMarginLeft = box.ContentMarginRight = 10;
			return box;
		}
		StyleBoxFlat flat = new() {
			BgColor = state switch {
				"hover" => Color.Color8(222, 190, 128),
				"selected" => Color.Color8(204, 160, 104),
				"disabled" => Color.Color8(226, 212, 180),
				_ => Color.Color8(206, 190, 154),
			},
			BorderColor = state == "selected" ? Color.Color8(226, 136, 28) : Color.Color8(92, 78, 56, (byte)(state == "disabled" ? 90 : 160)),
		};
		flat.SetBorderWidthAll(state == "selected" ? 2 : 1);
		flat.SetCornerRadiusAll(5);
		flat.SetContentMarginAll(4);
		flat.ContentMarginLeft = flat.ContentMarginRight = 10;
		return flat;
	}

	// Draws a button on the plates: on the selected one if it's picked (as
	// the rival, the city or the operational cost picked is).
	private static void StyleAsPlate(Button button, bool selected) {
		StyleBox picked = Plate("selected");
		button.AddThemeStyleboxOverride("normal", selected ? picked : Plate("normal"));
		button.AddThemeStyleboxOverride("hover", selected ? picked : Plate("hover"));
		button.AddThemeStyleboxOverride("pressed", picked);
		button.AddThemeStyleboxOverride("hover_pressed", picked);
		button.AddThemeStyleboxOverride("disabled", Plate("disabled"));
		button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" }) {
			button.AddThemeColorOverride(state, Ink);
		}
		button.AddThemeColorOverride("font_disabled_color", Faded);
	}

	// A list that scrolls, with its own thin scroll bar beside it.
	private (ScrollContainer, VBoxContainer) MakeScrollList(Rect2 area, int gap, Rect2 barArea) {
		ScrollContainer scroll = new() {
			Position = area.Position,
			Size = area.Size,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever,
		};
		VBoxContainer list = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		list.AddThemeConstantOverride("separation", gap);
		scroll.AddChild(list);
		AddChild(scroll);

		VScrollBar bar = new() { Position = barArea.Position, Size = barArea.Size, Visible = false };
		// A taupe groove, as the bands are, with a darker grip.
		StyleBoxFlat groove = new() { BgColor = Color.Color8(110, 95, 70, 70) };
		groove.SetCornerRadiusAll(4);
		bar.AddThemeStyleboxOverride("scroll", groove);
		bar.AddThemeStyleboxOverride("scroll_focus", groove);
		foreach ((string state, Color color) in new[] {
				("grabber", Color.Color8(120, 100, 70)),
				("grabber_highlight", Color.Color8(170, 110, 40)),
				("grabber_pressed", Color.Color8(150, 90, 30)) }) {
			StyleBoxFlat grip = new() { BgColor = color };
			grip.SetCornerRadiusAll(4);
			bar.AddThemeStyleboxOverride(state, grip);
		}
		AddChild(bar);
		VScrollBar inner = scroll.GetVScrollBar();
		void Sync() {
			bar.MaxValue = inner.MaxValue;
			bar.Page = inner.Page;
			bar.SetValueNoSignal(inner.Value);
			bar.Visible = inner.MaxValue > inner.Page + 1;
		}
		inner.Changed += Sync;
		inner.ValueChanged += _ => Sync();
		bar.ValueChanged += v => scroll.ScrollVertical = (int)v;
		return (scroll, list);
	}

	// A choice on a plate, its words on the left.
	private Button MakePlateButton(string text, Rect2 box) {
		Button button = new() {
			Text = text,
			Alignment = HorizontalAlignment.Left,
			Position = box.Position,
			Size = box.Size,
			CustomMinimumSize = box.Size,
			ClipText = true,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
		};
		button.AddThemeFontSizeOverride("font_size", 13);
		StyleAsPlate(button, false);
		AddChild(button);
		return button;
	}

	// A raised button that does something (Execute, Cancel); a plain box
	// without the art.
	private Button MakeActionButton(string text, Rect2 box) {
		Button button = new() { Text = text, Position = box.Position, Size = box.Size, CustomMinimumSize = box.Size };
		button.AddThemeFontSizeOverride("font_size", 15);
		foreach ((string state, string art) in new[] {
				("normal", "normal"), ("hover", "hover"), ("pressed", "pressed"), ("hover_pressed", "pressed"), ("disabled", "disabled") }) {
			StyleBox face;
			if (Art("button." + art) is ImageTexture texture) {
				StyleBoxTexture t = new() { Texture = texture };
				t.SetTextureMarginAll(8);
				face = t;
			} else {
				StyleBoxFlat flat = new() {
					BgColor = art switch {
						"hover" => Color.Color8(244, 220, 156),
						"pressed" => Color.Color8(198, 182, 146),
						"disabled" => Color.Color8(214, 210, 200),
						_ => Color.Color8(232, 225, 204),
					},
					BorderColor = art == "disabled" ? Color.Color8(150, 145, 135) : Color.Color8(58, 54, 44),
				};
				flat.SetBorderWidthAll(1);
				flat.SetCornerRadiusAll(4);
				face = flat;
			}
			button.AddThemeStyleboxOverride(state, face);
		}
		button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" }) {
			button.AddThemeColorOverride(state, Ink);
		}
		button.AddThemeColorOverride("font_disabled_color", Faded);
		AddChild(button);
		return button;
	}

	// Without the art, a check box is a plain box.
	private static void AddFallbackFace(TextureButton button, Vector2 size) {
		if (button.TextureNormal == null) {
			button.AddChild(new ColorRect { Color = Color.Color8(200, 184, 140), Size = size, MouseFilter = MouseFilterEnum.Ignore, ShowBehindParent = true });
		}
	}

	// Shows the screen, with the civ (and city) picked if given, as from a
	// foreign city's menu.
	public void ShowScreen(ID civ = null, City city = null) {
		if (civ != null) {
			selectedCiv = civ;
			selectedCity = city;
			selectedMission = null;
		}
		Show();
		Refresh();
	}

	// After a mission was sent, or the game changed: draws the screen again.
	public void Refresh() {
		if (!IsInsideTree() || civList == null) {
			return;
		}
		EngineStorage.ReadGameData((GameData gameData) => {
			Player player = gameData.GetUIControllerPlayer();
			if (player == null) {
				return;
			}
			List<Player> civs = Opponents(gameData, player);
			List<Player> met = civs.Where(c => Met(player, c)).ToList();
			Player target = met.FirstOrDefault(c => c.id == selectedCiv)
				?? met.FirstOrDefault(c => Espionage.HasEmbassy(player, c)) ?? met.FirstOrDefault();
			if (target?.id != selectedCiv) {
				selectedCity = null;
				selectedMission = null;
			}
			selectedCiv = target?.id;

			FillCivs(gameData, player, civs, target);
			FillMissions(gameData, player, target);
			FillCities(gameData, player, target);
			FillRisks(gameData, player, target);
			FillAssets(gameData, player);
		});
	}

	// The other civs in the game, met or not, in the game's order. Those
	// we haven't met are listed but not named (labels.txt has "Unmet" among
	// the Espionage screen's words).
	private static List<Player> Opponents(GameData gameData, Player player) {
		return gameData.players.Where(p => p != player && !p.defeated && !p.IsIndependent).ToList();
	}

	private static bool Met(Player player, Player civ) => PlayerRelationship.TryGetRelationship(player, civ, out _);

	private const string UnmetName = "Unmet Player";

	private void FillCivs(GameData gameData, Player player, List<Player> civs, Player target) {
		AdvisorUtils.ClearList(civList);
		foreach (Player civ in civs) {
			if (!Met(player, civ)) {
				civList.AddChild(MakeUnmetButton());
				continue;
			}
			bool selected = civ == target;
			Button button = new() {
				CustomMinimumSize = CivButtonSize,
				TooltipText = EmbassyReport(gameData, player, civ),
			};
			StyleAsPlate(button, selected);
			AddLeaderHead(button, civ);
			// The name goes on the plate, right of the leader's face, and
			// stops short of the marks.
			Label name = MakeCivName(AdvisorUtils.CivTitle(civ), selected ? PickedInk : Ink);
			button.AddChild(name);
			// "If the Embassy Icon is displayed, you currently have an
			// embassy within that civilization's capitol city. If the Spy
			// icon is present, you have a spy planted within that
			// civilization."
			AddIcon(button, "embassy", Espionage.HasEmbassy(player, civ), new Vector2(CivButtonSize.X - 64, 12), "An Embassy");
			AddIcon(button, "spy", Espionage.HasSpy(player, civ), new Vector2(CivButtonSize.X - 34, 12), "A Spy");
			ID id = civ.id;
			button.Pressed += () => {
				selectedCiv = id;
				Refresh();
			};
			civList.AddChild(button);
		}
	}

	// The square at the left end of a rival's plate that the leader's face
	// is shown in.
	private static readonly Rect2 HeadBox = new(5, 5, CivButtonTab - 8, 48 - 10);

	// A rival's name on its plate, right of the leader's face and short of
	// the marks.
	private static Label MakeCivName(string text, Color color) {
		float left = CivButtonTab + 6;
		float width = CivButtonSize.X - 70 - left;
		Label name = AdvisorUtils.MakeLabel(text, 15, width);
		name.Position = new Vector2(left, 3);
		name.Size = new Vector2(width, CivButtonSize.Y - 6);
		name.AddThemeColorOverride("font_color", color);
		name.MouseFilter = MouseFilterEnum.Ignore;
		return name;
	}

	// The civ's leader head (for its era, as on the diplomacy screens) in
	// the square, filling it, framed by a thin dark line.
	private static void AddLeaderHead(Control button, Player civ) {
		Panel well = new() {
			Position = HeadBox.Position - Vector2.One,
			Size = HeadBox.Size + 2 * Vector2.One,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		StyleBoxFlat line = new() { BgColor = Color.Color8(150, 136, 108), BorderColor = Color.Color8(58, 54, 44) };
		line.SetBorderWidthAll(1);
		well.AddThemeStyleboxOverride("panel", line);
		button.AddChild(well);
		Texture2D head;
		try {
			head = LeaderHeadTextures.Get(civ);
		} catch (Exception) {
			// Art that isn't there (TextureLoader logs it): an empty tab.
			return;
		}
		if (head == null) {
			return;
		}
		Control frame = new() {
			Position = HeadBox.Position,
			Size = HeadBox.Size,
			ClipContents = true,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		frame.AddChild(new TextureRect {
			Texture = head,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			Size = HeadBox.Size,
			MouseFilter = MouseFilterEnum.Ignore,
		});
		button.AddChild(frame);
	}

	// A rival we haven't met: its plate, greyed, that can't be picked.
	private static Button MakeUnmetButton() {
		Button button = new() {
			CustomMinimumSize = CivButtonSize,
			Disabled = true,
			MouseDefaultCursorShape = CursorShape.Arrow,
		};
		StyleAsPlate(button, false);
		button.AddChild(MakeCivName(UnmetName, UnavailableInk));
		return button;
	}

	private static void AddIcon(Control parent, string key, bool shown, Vector2 position, string fallback) {
		if (!shown) {
			return;
		}
		ImageTexture art = Art(key);
		if (art != null) {
			parent.AddChild(new TextureRect { Texture = art, Position = position, MouseFilter = MouseFilterEnum.Ignore });
		} else {
			Label label = AdvisorUtils.MakeLabel(key == "embassy" ? "E" : "S", 13);
			label.Position = position;
			label.MouseFilter = MouseFilterEnum.Ignore;
			parent.AddChild(label);
		}
	}

	// What our embassy reported of their capital when it was established.
	private static string EmbassyReport(GameData gameData, Player player, Player civ) {
		if (!PlayerRelationship.TryGetRelationship(player, civ, out PlayerRelationship pr) || !pr.hasEmbassy) {
			return $"We have no embassy with the {civ.civilization.noun}.";
		}
		Espionage.CityReport report = pr.embassyReport;
		if (report == null) {
			return $"We have an embassy with the {civ.civilization.noun}.";
		}
		return $"Our embassy in {report.cityName} reported in {gameData.timeOptions.GetDisplayTime(report.turn)}:\n"
			+ Espionage.DescribeReport(report);
	}

	private static IEnumerable<EspionageMission> MissionsOf(MissionType type) {
		return Espionage.Missions.Where(m => Espionage.IsEspionageMission(m) == (type == MissionType.Spy));
	}

	// The mission's option for the target, at the risk picked: for a city
	// mission, against the city picked (their capital until one is).
	private Espionage.MissionOption Option(GameData gameData, Player player, Player target, EspionageMission mission, EspionageRisk r) {
		City city = Espionage.TargetsCity(mission) ? selectedCity ?? Espionage.Capital(target) : null;
		return Espionage.GetOption(gameData, player, mission, target, city, r);
	}

	private void FillMissions(GameData gameData, Player player, Player target) {
		foreach (Node child in missionBoxes.GetChildren()) {
			missionBoxes.RemoveChild(child);
			child.QueueFree();
		}
		// The type shown is the tab picked.
		StyleAsPlate(diplomaticButton, missionType == MissionType.Diplomatic);
		StyleAsPlate(spyButton, missionType == MissionType.Spy);

		int row = 0;
		foreach (EspionageMission mission in MissionsOf(missionType)) {
			Vector2 position = FirstBox + new Vector2(0, row++ * BoxPitch);
			bool picked = selectedMission == mission;
			Espionage.MissionOption option = target == null ? null : Option(gameData, player, target, mission, EspionageRisk.Immediately);
			bool available = option?.available ?? false;
			// A mission that can't be sent has a greyed box, and can still
			// be picked to read why in the briefing.
			TextureButton box = new() {
				Position = position,
				CustomMinimumSize = new Vector2(24, 24),
				TextureNormal = Art(picked ? "box.checked" : available ? "box.normal" : "box.disabled"),
				TextureHover = Art(picked ? "box.checked_hover" : "box.hover"),
				TooltipText = option?.reason ?? "",
			};
			AddFallbackFace(box, new Vector2(24, 24));
			Button label = new() {
				Text = Espionage.Describe(mission),
				Flat = true,
				Alignment = HorizontalAlignment.Left,
				Position = position + new Vector2(28, -2),
				Size = new Vector2(252, 28),
				TooltipText = option?.reason ?? "",
			};
			label.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
			label.AddThemeFontSizeOverride("font_size", 14);
			// Dark on the stone of the panel: black if it can be sent, grey if
			// not, and dark orange when picked.
			Color color = picked ? PickedInk : available ? Colors.Black : UnavailableInk;
			foreach (string state in new[] { "font_color", "font_pressed_color", "font_focus_color" }) {
				label.AddThemeColorOverride(state, color);
			}
			label.AddThemeColorOverride("font_hover_color", PickedInk);
			void Pick() {
				selectedMission = picked ? null : mission;
				Refresh();
			}
			box.Pressed += Pick;
			label.Pressed += Pick;
			missionBoxes.AddChild(box);
			missionBoxes.AddChild(label);
		}

		if (target == null) {
			info.Text = "";
		} else if (selectedMission is EspionageMission chosen) {
			Espionage.MissionOption option = Option(gameData, player, target, chosen, risk);
			info.Text = option.available
				? $"{Espionage.Describe(chosen)} against the {target.civilization.noun}"
					+ (Espionage.TargetsCity(chosen) ? $" in {(selectedCity ?? Espionage.Capital(target))?.name}" : "") + "."
				: option.reason;
		} else {
			info.Text = "Select Mission";
		}
	}

	// The cities of the target that a city mission may be sent to: those we
	// know of. Only for city missions.
	private void FillCities(GameData gameData, Player player, Player target) {
		AdvisorUtils.ClearList(cityList);
		cityHint.Text = target == null ? ""
			: selectedMission is not EspionageMission m ? "Pick a mission."
			: !Espionage.TargetsCity(m) ? "This mission needs no target city."
			: "";
		if (target == null || selectedMission is not EspionageMission mission || !Espionage.TargetsCity(mission)) {
			return;
		}
		City picked = selectedCity ?? Espionage.Capital(target);
		foreach (City city in target.cities.Where(c => player.tileKnowledge.isTileKnown(c.location))
				.OrderByDescending(c => c.IsCapital()).ThenBy(c => c.name)) {
			bool selected = city == picked;
			Button bar = new() {
				Text = city.IsCapital() ? $"{city.name} (capital)" : city.name,
				Alignment = HorizontalAlignment.Left,
				CustomMinimumSize = BarSize,
				ClipText = true,
				TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
			};
			bar.AddThemeFontSizeOverride("font_size", 13);
			StyleAsPlate(bar, selected);
			City c = city;
			bar.Pressed += () => {
				selectedCity = c;
				Refresh();
			};
			cityList.AddChild(bar);
		}
		if (cityList.GetChildCount() == 0) {
			cityHint.Text = "We know of none of their cities.";
		}
	}

	// The operational costs of the mission picked: "[Immediately] ($NUM0
	// gold - low chance of success)", "[Carefully] ($NUM1 gold - moderate
	// chance of success)", "[Safely] ($NUM2 gold - high chance of success)"
	// (script.txt, #ESPIONAGE_SAFETY_LEVEL), with the chance in numbers.
	// Only Immediately where the risk makes no difference (see
	// Espionage.RiskMatters).
	private void FillRisks(GameData gameData, Player player, Player target) {
		EspionageMission? mission = selectedMission;
		bool choice = mission is EspionageMission m && Espionage.RiskMatters(m);
		if (!choice) {
			risk = EspionageRisk.Immediately;
		}
		Espionage.MissionOption chosen = null;
		for (int i = 0; i < 3; i++) {
			EspionageRisk r = (EspionageRisk)i;
			string name = r.ToString();
			Button button = riskButtons[i];
			bool enabled = target != null && mission != null && (choice || r == EspionageRisk.Immediately);
			button.Disabled = !enabled;
			StyleAsPlate(button, enabled && r == risk);
			if (target == null || mission == null) {
				button.Text = name;
				continue;
			}
			Espionage.MissionOption option = Option(gameData, player, target, mission.Value, r);
			if (r == risk) {
				chosen = option;
			}
			string chance = option.successPercent >= 100 ? "" : $", {option.successPercent}% chance";
			// A mission that can't be sent has no price to show (its
			// reason is above the mission type).
			button.Text = enabled && option.available ? $"{name} ({option.cost} gold{chance})" : name;
		}
		execute.Disabled = chosen == null || !chosen.available;
		cancel.Disabled = mission == null;
	}

	// "The assets section provides a summary of your current monetary and
	// technological assets, including the amount of gold in your treasury,
	// your technological achievements (those that apply to espionage), and
	// your level of spy experience."
	// In Civ3's words (labels.txt, after "ESPIONAGE"): "Diplomatic
	// Advance:", "Espionage Advance:", "Intelligence Center:" and "Spy
	// Experience:", answered "Yes" or "No". The advances are the one that
	// enables embassies (Writing) and the one the spy missions' building
	// needs (Espionage), as the BIQ has them. UNVERIFIED: the spy
	// experience's wording (the experience level's name).
	private void FillAssets(GameData gameData, Player player) {
		static string YesNo(bool yes) => yes ? "Yes" : "No";
		bool Knows(Tech tech) => tech != null && player.knownTechs.Contains(tech.id);
		Tech diplomatic = Tech.FindEmbassyTech(gameData.techs);
		Tech espionage = gameData.Buildings.FirstOrDefault(b => b.allowsSpyMissions)?.requiredTech;
		List<string> lines = [
			$"Treasury: {player.gold} gold",
			$"Diplomatic Advance: {YesNo(Knows(diplomatic))}",
			$"Espionage Advance: {YesNo(Knows(espionage))}",
			$"Intelligence Center: {YesNo(Espionage.HasIntelligenceAgency(player))}",
			$"Spy Experience: {(Espionage.SpyLevel(player) == Espionage.Agent.VeteranSpy ? "Veteran" : "Regular")}",
		];
		assets.Text = string.Join("\n", lines);
	}

	private void PickMissionType(MissionType type) {
		if (type != missionType) {
			missionType = type;
			selectedMission = null;
		}
		Refresh();
	}

	private void Execute() {
		if (selectedMission is not EspionageMission mission) {
			return;
		}
		EngineStorage.ReadGameData((GameData gameData) => {
			Player player = gameData.GetUIControllerPlayer();
			Player target = gameData.GetPlayer(selectedCiv);
			if (player == null || target == null) {
				return;
			}
			City city = Espionage.TargetsCity(mission) ? selectedCity ?? Espionage.Capital(target) : null;
			if (!Option(gameData, player, target, mission, risk).available) {
				return;
			}
			new MsgPerformEspionage(mission, target, city, risk).send();
		});
		selectedMission = null;
	}

	// Lets go of the game's objects after the game was replaced (as by a LAN
	// snapshot); the civ picked is kept by its id.
	public void ForgetGameObjects() {
		selectedCity = null;
	}
}
