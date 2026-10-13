using System.Collections.Generic;
using C7Engine;
using C7GameData;
using Godot;

// Civ3's F11 screen: the Top 5 Cities on the left, and the Demographics on
// the right, each of the civ's values with its rank among the civs (see
// DemographicsReport).
[GlobalClass]
[Tool]
public partial class DemographicsView : Control {

	[Export] public TextureRect background;

	private TextureButton _close;

	// The labels filled in each time the screen is shown.
	private readonly List<(Label name, Label details)> _cityRows = new();
	private readonly List<(Label value, Label rank)> _demographicRows = new();

	// Where the background's boxes and ruled lines are.
	private const float CitiesLeft = 70, CitiesWidth = 420, CitiesTop = 90, CityRowHeight = 122;
	private const float LabelLeft = 536, LabelWidth = 176;
	private const float ValueLeft = 724, ValueWidth = 146;
	private const float RankLeft = 880, RankWidth = 96;
	private const float HeaderTop = 100, RowsTop = 153, RowHeight = 41.8f;

	public DemographicsView() {
		MouseFilter = MouseFilterEnum.Stop;
	}

	public override void _Ready() {
		AdvisorUtils.CreateUI(this, CreateUI);
	}

	private void CreateUI() {
		background.Texture = TextureLoader.Load("screens.demographics.background");

		_close = AdvisorUtils.CreateExitButton(background);
		_close.Pressed += () => { this.GetParent<GameViews>().Hide(); };

		var title = AdvisorUtils.CreateAdvisorTitle(background, background.Texture.GetWidth(),
			" TOP 5 CITIES    DEMOGRAPHICS");
		title.Position += new Vector2(0, 10);

		for (int i = 0; i < 5; ++i) {
			float top = CitiesTop + i * CityRowHeight;
			Label name = AdvisorUtils.CreateLabel(background, "", new Rect2(CitiesLeft, top + 22, CitiesWidth, 36), 22);
			Label details = AdvisorUtils.CreateLabel(background, "", new Rect2(CitiesLeft + 28, top + 60, CitiesWidth - 28, 40), 15);
			_cityRows.Add((name, details));
		}

		AdvisorUtils.CreateLabel(background, "Rank", new Rect2(RankLeft, HeaderTop, RankWidth, 40), 16, HorizontalAlignment.Center);
		for (int i = 0; i < DemographicsReport.RowCount; ++i) {
			float top = RowsTop + i * RowHeight;
			AdvisorUtils.CreateLabel(background, DemographicsReport.Labels[i], new Rect2(LabelLeft, top, LabelWidth, RowHeight), 15);
			Label value = AdvisorUtils.CreateLabel(background, "", new Rect2(ValueLeft, top, ValueWidth, RowHeight), 13, HorizontalAlignment.Right);
			Label rank = AdvisorUtils.CreateLabel(background, "", new Rect2(RankLeft, top, RankWidth, RowHeight), 15, HorizontalAlignment.Center);
			_demographicRows.Add((value, rank));
		}
	}

	public void ShowView() {
		Show();

		EngineStorage.ReadGameData(Fill);
	}

	private void Fill(GameData gameData) {
		Player player = gameData.GetUIControllerPlayer();
		if (player == null || _demographicRows.Count == 0) {
			return;
		}

		List<City> topCities = DemographicsReport.TopCities(gameData);
		for (int i = 0; i < _cityRows.Count; ++i) {
			(Label name, Label details) = _cityRows[i];
			if (i < topCities.Count) {
				City city = topCities[i];
				name.Text = $"{i + 1}. {city.name}";
				details.Text = $"{AdvisorUtils.CivName(city.owner)}, size {city.residents.Count}, {city.GetCultureFor(city.owner):N0} culture";
			} else {
				name.Text = "";
				details.Text = "";
			}
		}

		List<DemographicsLine> lines = DemographicsReport.For(gameData, player);
		for (int i = 0; i < lines.Count && i < _demographicRows.Count; ++i) {
			_demographicRows[i].value.Text = lines[i].text;
			_demographicRows[i].rank.Text = DemographicsReport.Ordinal(lines[i].rank);
		}
	}
}
