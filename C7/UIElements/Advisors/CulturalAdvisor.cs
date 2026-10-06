using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using Godot;

[GlobalClass]
[Tool]
public partial class CulturalAdvisor : Control {

	[Export] public TextureRect background;

	private TextureButton _close;
	private TextureRect _advisorHead;
	private TextureButton _dialogBox;
	private Label _dialogBoxLabel;

	private Label _summaryLabel;
	private Label _totalLabel;
	private VBoxContainer _cityList;
	private VBoxContainer _influenceList;

	// The height of a city row, so rows sit on the background's ruled lines.
	private const float RowHeight = 45;

	public CulturalAdvisor() {
		MouseFilter = MouseFilterEnum.Stop;
	}

	public override void _Ready() {
		this.CreateUI();
	}

	private void CreateUI() {
		background.Texture = TextureLoader.Load("advisors.culture.background");

		_advisorHead = AdvisorUtils.CreateAdvisorHead(background, AdvisorHead.Advisor.Culture);
		_close = AdvisorUtils.CreateExitButton(background);
		_close.Pressed += () => { this.GetParent<Advisors>().Hide(); };
		(_dialogBox, _dialogBoxLabel) = AdvisorUtils.CreateAdvisorDialogBox(background);

		AdvisorUtils.CreateAdvisorTitle(background, background.Texture.GetWidth(), "CULTURAL ADVISOR");
		AdvisorUtils.CreateAdvisorSidebar(background, AdvisorHead.Advisor.Culture);

		// The state of our culture, top left.
		_summaryLabel = AdvisorUtils.CreateLabel(background, "", new Rect2(92, 98, 274, 128), 14, HorizontalAlignment.Center);
		_summaryLabel.ClipText = false;
		_summaryLabel.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
		_summaryLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;

		// The green box with our total.
		_totalLabel = AdvisorUtils.CreateLabel(background, "", new Rect2(493, 99, 187, 64), 13, HorizontalAlignment.Center);

		// Column headers over the city list.
		AdvisorUtils.CreateLabel(background, "Cities", new Rect2(117, 247, 140, 41), 13);
		AdvisorUtils.CreateLabel(background, "culture value", new Rect2(263, 247, 98, 41), 13, HorizontalAlignment.Right);
		AdvisorUtils.CreateLabel(background, "/turn", new Rect2(374, 247, 58, 41), 13, HorizontalAlignment.Center);
		AdvisorUtils.CreateLabel(background, "Culture Rating = Improvements + Wonders", new Rect2(445, 247, 236, 41), 11, HorizontalAlignment.Center);
		_cityList = AdvisorUtils.CreateList(background, new Rect2(81, 290, 600, 410));

		// How other civs see us, and our wonders, in the box on the right.
		AdvisorUtils.CreateLabel(background, "Influence", new Rect2(724, 250, 262, 24), 14, HorizontalAlignment.Center);
		_influenceList = AdvisorUtils.CreateList(background, new Rect2(734, 278, 244, 420));
	}

	public void ShowAdvisor() {
		Show();

		EngineStorage.ReadGameData((GameData gameData) => {
			Player player = gameData.GetUIControllerPlayer();
			int total = CultureReport.TotalCulture(player);
			int perTurn = CultureReport.CulturePerTurn(player);
			string level = CultureReport.CultureLevel(gameData.rules, total);

			_summaryLabel.Text = $"Our people have developed {CultureReport.Article(level)} {level} culture.\n\n"
				+ $"Current Date: {gameData.timeOptions?.GetDisplayTime(gameData.turn)}";
			_totalLabel.Text = $"Our Total Culture Value:\n{total} ({perTurn}/turn)";

			FillCities(player);
			FillInfluence(gameData, player, total);

			_dialogBoxLabel.Text = Advice(player, perTurn);
			_advisorHead.Texture = AdvisorHead.GetPopupImage(AdvisorHead.Advisor.Culture, AdvisorHead.Mood.Happy, player.EraIndex());
		});
	}

	private void FillCities(Player player) {
		AdvisorUtils.ClearList(_cityList);
		foreach (City city in player.cities.OrderByDescending(c => c.GetCulture())) {
			(int improvements, int wonders) = city.GetCulturePerTurnBySource();
			_cityList.AddChild(AdvisorUtils.MakeRow(RowHeight,
				AdvisorUtils.MakeSpacer(30),
				AdvisorUtils.MakeCityButton(this, city, 140),
				AdvisorUtils.MakeLabel(city.GetCulture().ToString(), 13, 100, HorizontalAlignment.Right),
				AdvisorUtils.MakeLabel(city.GetCulturePerTurn().ToString(), 13, 64, HorizontalAlignment.Center),
				AdvisorUtils.MakeLabel($"{improvements + wonders} = {improvements} + {wonders}", 13, 236, HorizontalAlignment.Center)));
		}
	}

	private void FillInfluence(GameData gameData, Player player, int ourCulture) {
		AdvisorUtils.ClearList(_influenceList);

		List<Player> rivals = gameData.GetKnownRivals(player);
		foreach (Player rival in rivals) {
			string opinion = CultureReport.OpinionOf(gameData.rules, CultureReport.TotalCulture(rival), ourCulture);
			_influenceList.AddChild(WrappedLabel($"The {AdvisorUtils.CivName(rival)} are {opinion} our culture."));
		}
		if (rivals.Count == 0) {
			_influenceList.AddChild(WrappedLabel("We know of no other civilizations."));
		}

		_influenceList.AddChild(AdvisorUtils.MakeSpacer(0));
		Label wondersHeader = AdvisorUtils.MakeLabel("Wonders", 14, 244, HorizontalAlignment.Center);
		wondersHeader.CustomMinimumSize = new Vector2(244, 34);
		_influenceList.AddChild(wondersHeader);

		List<Tuple<City, CityBuilding>> wonders = player.GetActiveWonders();
		foreach ((City city, CityBuilding cb) in wonders) {
			_influenceList.AddChild(WrappedLabel($"{cb.building.name}\n    Built in {city.name}"));
		}
		if (wonders.Count == 0) {
			_influenceList.AddChild(WrappedLabel("We have built no wonders."));
		}
	}

	private static Label WrappedLabel(string text) => AdvisorUtils.MakeWrappedLabel(text, 12, 236);

	private static string Advice(Player player, int perTurn) {
		if (player.cities.Count == 0) {
			return "We have no cities to grow our culture.";
		}
		if (perTurn == 0) {
			return "Our cities make no culture. Temples and wonders would help.";
		}
		// The city that will next push its borders out, and how soon.
		City next = null;
		int fewestTurns = int.MaxValue;
		foreach (City city in player.cities) {
			int cityPerTurn = city.GetCulturePerTurn();
			if (cityPerTurn <= 0) {
				continue;
			}
			int needed = (int)Math.Pow(10, city.GetBorderExpansionLevel()) - city.GetCulture();
			int turns = (needed + cityPerTurn - 1) / cityPerTurn;
			if (turns < fewestTurns) {
				fewestTurns = turns;
				next = city;
			}
		}
		if (next == null) {
			return "Our culture grows.";
		}
		return $"{next.name}'s borders will expand in {fewestTurns} {(fewestTurns == 1 ? "turn" : "turns")}.";
	}
}
