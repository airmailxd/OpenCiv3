using C7Engine;
using C7GameData;
using Godot;
using System.Collections.Generic;
using System.Linq;
using C7.UIElements;
using static C7Engine.MsgChangeSliders;

[GlobalClass]
[Tool]
public partial class DomesticAdvisor : Control {
	[Export] TextureRect background;
	[Export] TextureButton close;
	[Export] TextureButton changeGovernment;
	[Export] Label governmentLabel;
	[Export] Label scienceStatus;
	[Export] Label treasury;
	[Export] Label incomeDetails;
	[Export] Label expenseDetails;
	[Export] Label incomeSummary;
	[Export] Label expenseSummary;
	[Export] Label sumSummary;
	[Export] Label growth;
	[Export] VBoxContainer cityListContainer;
	[Export] Label citiesHeader;
	[Export] Label populationHeader;
	[Export] TextureButton eatenFood;
	[Export] TextureButton fullFood;
	[Export] TextureButton wastedShield;
	[Export] TextureButton goodShield;
	[Export] TextureButton wastedGold;
	[Export] TextureButton goodGold;
	[Export] TextureButton happyFace;
	[Export] TextureButton contentFace;
	[Export] TextureButton beaker;
	[Export] TextureButton treasuryIcon;
	[Export] Civ3HSlider scienceSlider;
	[Export] Civ3HSlider luxurySlider;

	PopupOverlay popupOverlay;

	Label scienceSliderLabel = new();
	Label luxurySliderLabel = new();

	TextureRect advisorHead = new();
	Label DialogBoxAdvise = new();
	private const string DefaultAdvice = "You are running OpenCiv3!";

	// The Y position of the two sliders.
	private int scienceSliderY = 84;
	private int luxurySliderY = 130;

	private Player playerController = null;

	// The column the city list is sorted by, as in Civ3 chosen by clicking
	// the column's header. Clicking the same header again reverses the order.
	private enum SortColumn { None, Name, Food, Production, Commerce, Happiness, Science, Taxes, Population }
	private SortColumn sortColumn = SortColumn.None;
	private bool sortReversed = false;

	public DomesticAdvisor() {
		MouseFilter = MouseFilterEnum.Stop;
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready() {
		this.CreateUI();
	}

	private void CreateUI() {
		ImageTexture DomesticBackground = TextureLoader.Load("advisors.domestic.background");
		background.Texture = DomesticBackground;

		AdvisorUtils.CreateAdvisorTitle(background, background.Texture.GetWidth(), "DOMESTIC ADVISOR");
		AdvisorUtils.CreateAdvisorSidebar(background, AdvisorHead.Advisor.Domestic);

		advisorHead.Texture = AdvisorHead.GetPopupImage(AdvisorHead.Advisor.Domestic, AdvisorHead.Mood.Happy, eraIndex: 0);
		advisorHead.SetPosition(new Vector2(851, 0));
		background.AddChild(advisorHead);

		ImageTexture DialogBoxTexture = TextureLoader.Load("advisors.dialog_box");
		TextureButton DialogBox = new TextureButton();
		DialogBox.TextureNormal = DialogBoxTexture;
		DialogBox.SetPosition(new Vector2(806, 110));
		background.AddChild(DialogBox);

		//TODO: Multi-line capabilities
		DialogBoxAdvise.Text = DefaultAdvice;
		DialogBoxAdvise.SetPosition(new Vector2(815, 119));
		background.AddChild(DialogBoxAdvise);

		TextureLoader.SetButtonTextures(close, "ui.exit");
		close.Pressed += () => {
			GetParent<Advisors>().Hide();
		};

		TextureLoader.SetButtonTextures(changeGovernment, "advisors.domestic.button");
		changeGovernment.Pressed += ChangeGovernments;

		ImageTexture scienceSliderTexture = TextureLoader.Load("icons.science");
		ImageTexture luxurySliderTexture = TextureLoader.Load("icons.luxury");

		// Placeholder values
		int scienceRate = 5;
		int luxuryRate = 5;

		scienceSlider.Value = scienceRate;

		scienceSlider.rangeTheme
			.AddGrabber(scienceSliderTexture)
			.AddGrabberHighlight(scienceSliderTexture)
			.AddSliderStyleBox(new StyleBoxEmpty());

		scienceSlider.ValueChanged += value => {
			UpdateScienceSlider((int)value);
		};

		luxurySlider.Value = luxuryRate;

		luxurySlider.rangeTheme
			.AddGrabber(luxurySliderTexture)
			.AddGrabberHighlight(luxurySliderTexture)
			.AddSliderStyleBox(new StyleBoxEmpty());

		luxurySlider.ValueChanged += value => {
			UpdateLuxurySlider((int)value);
		};

		scienceSliderLabel.Text = $"{scienceRate * 10}%";
		scienceSliderLabel.SetPosition(new Vector2(760, scienceSliderY + 5));
		background.AddChild(scienceSliderLabel);

		luxurySliderLabel.Text = $"{luxuryRate * 10}%";
		luxurySliderLabel.SetPosition(new Vector2(760, luxurySliderY + 4));
		background.AddChild(luxurySliderLabel);

		var plusConfigKey = "advisors.domestic.plus";
		var minusConfigKey = "advisors.domestic.minus";

		TextureButton moreScience = new();
		TextureLoader.SetButtonTextures(moreScience, plusConfigKey);
		moreScience.SetPosition(new Vector2(732, scienceSliderY + 27));
		moreScience.Pressed += MoreScience;
		background.AddChild(moreScience);

		TextureButton lessScience = new();
		TextureLoader.SetButtonTextures(lessScience, minusConfigKey);
		lessScience.SetPosition(new Vector2(562, scienceSliderY + 29));
		lessScience.Pressed += LessScience;
		background.AddChild(lessScience);

		TextureButton moreLuxury = new();
		TextureLoader.SetButtonTextures(moreLuxury, plusConfigKey);
		moreLuxury.SetPosition(new Vector2(732, luxurySliderY - 5));
		moreLuxury.Pressed += MoreLuxury;
		background.AddChild(moreLuxury);

		TextureButton lessLuxury = new();
		TextureLoader.SetButtonTextures(lessLuxury, minusConfigKey);
		lessLuxury.SetPosition(new Vector2(562, luxurySliderY - 3));
		lessLuxury.Pressed += LessLuxury;
		background.AddChild(lessLuxury);

		// Column header icons.
		eatenFood.TextureNormal = TextureLoader.Load("icons.eaten_food");
		fullFood.TextureNormal = TextureLoader.Load("icons.full_food");
		wastedShield.TextureNormal = TextureLoader.Load("icons.wasted_shield");
		goodShield.TextureNormal = TextureLoader.Load("icons.good_shield");
		wastedGold.TextureNormal = TextureLoader.Load("icons.wasted_gold");
		goodGold.TextureNormal = TextureLoader.Load("icons.good_gold");
		happyFace.TextureNormal = TextureLoader.Load("icons.happy_face");
		contentFace.TextureNormal = TextureLoader.Load("icons.content_face");
		beaker.TextureNormal = TextureLoader.Load("icons.beaker");
		treasuryIcon.TextureNormal = TextureLoader.Load("icons.treasury");

		// Clicking a column header sorts the city list by that column.
		eatenFood.Pressed += () => SortBy(SortColumn.Food);
		fullFood.Pressed += () => SortBy(SortColumn.Food);
		wastedShield.Pressed += () => SortBy(SortColumn.Production);
		goodShield.Pressed += () => SortBy(SortColumn.Production);
		wastedGold.Pressed += () => SortBy(SortColumn.Commerce);
		goodGold.Pressed += () => SortBy(SortColumn.Commerce);
		happyFace.Pressed += () => SortBy(SortColumn.Happiness);
		contentFace.Pressed += () => SortBy(SortColumn.Happiness);
		beaker.Pressed += () => SortBy(SortColumn.Science);
		treasuryIcon.Pressed += () => SortBy(SortColumn.Taxes);
		MakeClickableHeader(citiesHeader, SortColumn.Name);
		MakeClickableHeader(populationHeader, SortColumn.Population);
	}

	private void MakeClickableHeader(Label header, SortColumn column) {
		header.MouseFilter = MouseFilterEnum.Stop;
		header.MouseDefaultCursorShape = CursorShape.PointingHand;
		header.GuiInput += (InputEvent e) => {
			if (e is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed) {
				SortBy(column);
				header.AcceptEvent();
			}
		};
	}

	private void SortBy(SortColumn column) {
		if (column == sortColumn) {
			sortReversed = !sortReversed;
		} else {
			sortColumn = column;
			sortReversed = false;
		}
		ShowAdvisor();
	}

	// Returns the player's cities in the order chosen by the column headers:
	// names A to Z, everything else largest first. Ties keep the founding order.
	private List<City> SortedCities(List<City> cities) {
		if (sortColumn == SortColumn.None) {
			return cities;
		}

		IOrderedEnumerable<City> sorted;
		if (sortColumn == SortColumn.Name) {
			sorted = cities.OrderBy(c => c.name, System.StringComparer.CurrentCultureIgnoreCase);
		} else {
			sorted = cities.OrderByDescending(c => sortColumn switch {
				SortColumn.Food => c.FoodGrowthPerTurn(),
				SortColumn.Production => c.CurrentProductionYield().useful,
				SortColumn.Commerce => CommerceAfterCorruption(c.CurrentCommerceYield()),
				SortColumn.Happiness => HappyCitizenCount(c),
				SortColumn.Science => c.CurrentCommerceYield().beakers,
				SortColumn.Taxes => c.CurrentCommerceYield().taxes,
				SortColumn.Population => c.residents.Count,
				_ => 0,
			});
		}

		List<City> result = sorted.ToList();
		if (sortReversed) {
			result.Reverse();
		}
		return result;
	}

	// The happy citizens, counted the way the happiness column shows them.
	private static int HappyCitizenCount(City city) {
		return city.residents.Count(cr => cr.citizenType.IsDefaultCitizen && cr.mood == CityResident.Mood.Happy);
	}

	private static int CommerceAfterCorruption(CommerceBreakdown commerce) {
		return commerce.taxes + commerce.beakers + commerce.happiness;
	}

	// The sliders ask for the rate they were moved to, rather than a step
	// each time they change: a click on the track jumps several steps, and
	// the player's rates may not have caught up with an earlier move yet (as
	// on a LAN client). Only a change the player made asks for anything.
	private void UpdateScienceSlider(int value) {
		if (playerController == null || refreshingSliders)
			return;
		DomesticPolicyChoice direction = value > playerController.scienceRate ? DomesticPolicyChoice.MoreScience : DomesticPolicyChoice.LessScience;
		new MsgChangeSliders(direction, value).send();
	}

	private void UpdateLuxurySlider(int value) {
		if (playerController == null || refreshingSliders)
			return;
		DomesticPolicyChoice direction = value > playerController.luxuryRate ? DomesticPolicyChoice.MoreLuxury : DomesticPolicyChoice.LessLuxury;
		new MsgChangeSliders(direction, value).send();
	}

	// Set while the sliders are moved to show the player's rates.
	private bool refreshingSliders = false;

	// The plus and minus buttons move a rate one step.
	private void MoreScience() {
		new MsgChangeSliders(DomesticPolicyChoice.MoreScience).send();
	}

	private void MoreLuxury() {
		new MsgChangeSliders(DomesticPolicyChoice.MoreLuxury).send();
	}

	private void LessScience() {
		new MsgChangeSliders(DomesticPolicyChoice.LessScience).send();
	}

	private void LessLuxury() {
		new MsgChangeSliders(DomesticPolicyChoice.LessLuxury).send();
	}

	public void ShowAdvisor() {
		Show();

		EngineStorage.ReadGameData((GameData gameData) => {
			playerController = gameData.players.First(p => p.id == EngineStorage.uiControllerID);
			// Add up the economy once, and work everything shown out from it.
			PlayerCommerceBreakdown totalIncome = playerController.AggregateFlows();

			int scienceRate = playerController.scienceRate;
			int luxuryRate = playerController.luxuryRate;

			refreshingSliders = true;
			scienceSlider.Value = scienceRate;
			luxurySlider.Value = luxuryRate;
			refreshingSliders = false;
			scienceSliderLabel.Text = $"{scienceRate * 10}%";
			luxurySliderLabel.Text = $"{luxuryRate * 10}%";

			governmentLabel.Text = $"{playerController.government.name}";
			scienceStatus.Text = ScienceEstimates.SummarizeScience(gameData, playerController, totalIncome.beakers);
			treasury.Text = $"Treasury: {playerController.gold}";

			incomeDetails.Text = $"From cities: +{totalIncome.CityInflows()}\nFrom taxmen: +{totalIncome.taxmenTaxes}\nFrom other civs: +{totalIncome.fromOtherCivs}\nFrom interest: +{totalIncome.interest}\nFrom tourism: +{totalIncome.tourism}";
			expenseDetails.Text = $"-{totalIncome.beakers}: Science\n-{totalIncome.happiness}: Entertainment\n-{totalIncome.corrupted}: Corruption\n-{totalIncome.maintenance}: Maintenance\n-{totalIncome.unitSupport}: Unit costs\n-{totalIncome.toOtherCivs}: To other civs";
			incomeSummary.Text = $"Income: {totalIncome.Inflows()}";
			expenseSummary.Text = $"Expenses: {totalIncome.Outflows()}";

			int goldPerTurn = totalIncome.Netflows();
			if (goldPerTurn > 0) {
				sumSummary.Text = $"Net gain: +{goldPerTurn}";
				growth.Text = "Growing!";
			} else if (goldPerTurn < 0) {
				sumSummary.Text = $"Net loss: {goldPerTurn}";
				growth.Text = "Shrinking!";
			} else {
				sumSummary.Text = $"Neutral: {goldPerTurn}";
				growth.Text = "Balanced";
			}

			//TODO: Randomize or set logically
			int eraIndex = playerController.EraIndex();
			advisorHead.Texture = AdvisorHead.GetPopupImage(AdvisorHead.Advisor.Domestic, AdvisorHead.Mood.Happy, eraIndex);

			// Disable the change government button unless we have a government to
			// switch to.
			changeGovernment.Disabled = playerController.GetAvailableGovernments(gameData).Count == 1;

			if (playerController.government.transitionType && playerController.inAnarchyUntilTurn > gameData.turn) {
				DialogBoxAdvise.Text = $"{playerController.inAnarchyUntilTurn - gameData.turn} turns of anarchy left";
			} else {
				DialogBoxAdvise.Text = DefaultAdvice;
			}

			// Reuse the rows already made, only adding or removing rows when
			// the number of cities changes, and update them in place.
			List<City> cities = SortedCities(playerController.cities);
			if (!rowsMade) {
				// Clear out anything the scene came with.
				foreach (var node in cityListContainer.GetChildren()) {
					cityListContainer.RemoveChild(node);
					node.QueueFree();
				}
				rowsMade = true;
			}
			while (cityRows.Count > cities.Count) {
				CityRow extra = cityRows[cityRows.Count - 1];
				cityRows.RemoveAt(cityRows.Count - 1);
				cityListContainer.RemoveChild(extra.row);
				extra.row.QueueFree();
			}
			while (cityRows.Count < cities.Count) {
				CityRow row = MakeCityRow();
				cityRows.Add(row);
				cityListContainer.AddChild(row.row);
			}
			for (int i = 0; i < cities.Count; ++i) {
				UpdateCityRow(cityRows[i], cities[i], eraIndex);
			}
		});
	}

	// Called when the game was replaced (e.g. by a LAN snapshot): the rows
	// point at the old game's cities, so let go of them. Showing the advisor
	// points them at the new game's cities again.
	public void ForgetGameObjects() {
		playerController = null;
		foreach (CityRow row in cityRows) {
			row.city = null;
		}
	}

	private int CalculateSliderXPos(int sliderRate) {
		int minX = 570;
		int maxX = 725;

		return minX + (int)((maxX - minX) * (sliderRate / 10.0));
	}

	public void SetPopupOverlay(PopupOverlay po) {
		popupOverlay = po;
	}

	private void ChangeGovernments() {
		EngineStorage.ReadGameData((GameData gameData) => {
			Player player = gameData.GetUIControllerPlayer();

			if (player.government.transitionType) {
				popupOverlay.ShowPopup(
					new InformationalPopup("You already started a revolution. Remember?"),
					PopupOverlay.PopupCategory.Advisor);
				return;
			}

			popupOverlay.ShowPopup(
				new ConfirmationPopup(
					"You say you want a revolution?",
					"Yes, you know it's gonna be alright.",
					"No. You can count me out.",
					() => { new StartGovernmentTransitionMsg().send(); }),
				PopupOverlay.PopupCategory.Advisor);
		});
	}

	// The controls of one row of the city list, kept so the row can be
	// updated in place for another city or after a change.
	private class CityRow {
		public HBoxContainer row;
		public City city;
		public Button cityName;
		public Label foodLabel;
		public Label shieldsLabel;
		public Label commerceLabel;
		public Label maintenanceLabel;
		public Label happinessLabel;
		public Label scienceLabel;
		public Label taxesLabel;
		public Control populationContainer;
		public List<TextureRect> popHeads = new();
		public Label productionLabel;
	}

	private readonly List<CityRow> cityRows = new();
	private bool rowsMade = false;

	// Every blank separator shares one empty style box.
	private static readonly StyleBoxEmpty emptyStyleBox = new();

	private CityRow MakeCityRow() {
		CityRow cityRow = new();
		HBoxContainer hboxContainer = new();
		cityRow.row = hboxContainer;

		// Use an empty pany container to make it blank, unlike an HSeparator
		PanelContainer hSeparator1 = new();
		hSeparator1.CustomMinimumSize = new Vector2(30, 50);
		hSeparator1.AddThemeStyleboxOverride("panel", emptyStyleBox);
		hboxContainer.AddChild(hSeparator1);

		Button cityName = new();
		cityName.CustomMinimumSize = new Vector2(127, 0);
		cityName.ClipText = true;
		cityName.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		cityName.Pressed += () => {
			if (cityRow.city == null)
				return;
			GetParent<Advisors>().Hide();
			new MsgShowCityScreen(cityRow.city).send();
		};
		hboxContainer.AddChild(cityName);
		cityRow.cityName = cityName;

		Label foodLabel = new();
		foodLabel.CustomMinimumSize = new Vector2(40, 0);
		foodLabel.HorizontalAlignment = HorizontalAlignment.Center;
		hboxContainer.AddChild(foodLabel);
		cityRow.foodLabel = foodLabel;

		Label shieldsLabel = new();
		shieldsLabel.CustomMinimumSize = new Vector2(40, 0);
		shieldsLabel.HorizontalAlignment = HorizontalAlignment.Center;
		hboxContainer.AddChild(shieldsLabel);
		cityRow.shieldsLabel = shieldsLabel;

		Label commerceLabel = new();
		commerceLabel.CustomMinimumSize = new Vector2(40, 0);
		commerceLabel.HorizontalAlignment = HorizontalAlignment.Center;
		hboxContainer.AddChild(commerceLabel);
		cityRow.commerceLabel = commerceLabel;

		// Use an empty pany container to make it blank, unlike an HSeparator
		PanelContainer hSeparator2 = new();
		hSeparator2.CustomMinimumSize = new Vector2(25, 0);
		hSeparator2.AddThemeStyleboxOverride("panel", emptyStyleBox);
		hboxContainer.AddChild(hSeparator2);

		Label maintenanceLabel = new();
		maintenanceLabel.CustomMinimumSize = new Vector2(40, 0);
		maintenanceLabel.HorizontalAlignment = HorizontalAlignment.Center;
		maintenanceLabel.VerticalAlignment = VerticalAlignment.Center;
		maintenanceLabel.ClipText = true;
		hboxContainer.AddChild(maintenanceLabel);
		cityRow.maintenanceLabel = maintenanceLabel;

		Label happinessLabel = new();
		happinessLabel.CustomMinimumSize = new Vector2(40, 0);
		happinessLabel.HorizontalAlignment = HorizontalAlignment.Center;
		happinessLabel.VerticalAlignment = VerticalAlignment.Center;
		happinessLabel.ClipText = true;
		hboxContainer.AddChild(happinessLabel);
		cityRow.happinessLabel = happinessLabel;

		Label scienceLabel = new();
		scienceLabel.CustomMinimumSize = new Vector2(40, 0);
		scienceLabel.HorizontalAlignment = HorizontalAlignment.Center;
		scienceLabel.VerticalAlignment = VerticalAlignment.Center;
		scienceLabel.ClipText = true;
		hboxContainer.AddChild(scienceLabel);
		cityRow.scienceLabel = scienceLabel;

		Label taxesLabel = new();
		taxesLabel.CustomMinimumSize = new Vector2(40, 0);
		taxesLabel.HorizontalAlignment = HorizontalAlignment.Center;
		taxesLabel.VerticalAlignment = VerticalAlignment.Center;
		taxesLabel.ClipText = true;
		hboxContainer.AddChild(taxesLabel);
		cityRow.taxesLabel = taxesLabel;

		// Use an empty pany container to make it blank, unlike an HSeparator
		PanelContainer hSeparator3 = new();
		hSeparator3.CustomMinimumSize = new Vector2(25, 0);
		hSeparator3.AddThemeStyleboxOverride("panel", emptyStyleBox);
		hboxContainer.AddChild(hSeparator3);

		Control popuplationContainer = new();
		popuplationContainer.CustomMinimumSize = new Vector2(220, 0);
		hboxContainer.AddChild(popuplationContainer);
		cityRow.populationContainer = popuplationContainer;

		PanelContainer productionContainer = new();
		productionContainer.CustomMinimumSize = new Vector2(50, 0);
		productionContainer.AddThemeStyleboxOverride("panel", emptyStyleBox);
		hboxContainer.AddChild(productionContainer);

		Label productionLabel = new();
		productionLabel.CustomMinimumSize = new Vector2(75, 0);
		productionLabel.VerticalAlignment = VerticalAlignment.Center;
		productionLabel.ClipText = true;
		productionLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		hboxContainer.AddChild(productionLabel);
		cityRow.productionLabel = productionLabel;

		return cityRow;
	}

	// Residents sorted by how they are shown, reused for each row.
	private readonly List<CityResident> happyResidents = new();
	private readonly List<CityResident> contentResidents = new();
	private readonly List<CityResident> unhappyResidents = new();
	private readonly List<CityResident> specialists = new();

	private void UpdateCityRow(CityRow cityRow, City city, int eraNum) {
		cityRow.city = city;
		SetText(cityRow.cityName, city.name);

		SetText(cityRow.foodLabel, SpaceAlignedDotFormat(city.FoodConsumedPerTurn(), city.FoodGrowthPerTurn()));

		{
			CorruptableValue prod = city.CurrentProductionYield();
			SetText(cityRow.shieldsLabel, SpaceAlignedDotFormat(prod.corrupt, prod.useful));
		}

		CommerceBreakdown commerce = city.CurrentCommerceYield();
		SetText(cityRow.commerceLabel, SpaceAlignedDotFormat(commerce.corrupted, CommerceAfterCorruption(commerce)));
		SetText(cityRow.maintenanceLabel, city.MaintenanceCosts().ToString());

		// Sort the residents by how they are shown, in one pass.
		happyResidents.Clear();
		contentResidents.Clear();
		unhappyResidents.Clear();
		specialists.Clear();
		foreach (CityResident cr in city.residents) {
			if (!cr.citizenType.IsDefaultCitizen) {
				specialists.Add(cr);
			} else if (cr.mood == CityResident.Mood.Happy) {
				happyResidents.Add(cr);
			} else if (cr.mood == CityResident.Mood.Content) {
				contentResidents.Add(cr);
			} else if (cr.mood == CityResident.Mood.Unhappy) {
				unhappyResidents.Add(cr);
			}
		}

		SetText(cityRow.happinessLabel, SpaceAlignedDotFormat(happyResidents.Count, contentResidents.Count));
		SetText(cityRow.scienceLabel, $"{commerce.beakers}");
		SetText(cityRow.taxesLabel, $"{commerce.taxes}");

		UpdatePopHeads(cityRow, city.residents.Count, 220, eraNum);

		int turnsUntilFinished = city.TurnsUntilProductionFinished();
		string turnsLeft = turnsUntilFinished == int.MaxValue ? "(9999999 turns)" : $"({turnsUntilFinished} turns)";
		SetText(cityRow.productionLabel, $"{city.itemBeingProduced.name}\n{turnsLeft}");
	}

	private static void SetText(Label label, string text) {
		if (label.Text != text) {
			label.Text = text;
		}
	}

	private static void SetText(Button button, string text) {
		if (button.Text != text) {
			button.Text = text;
		}
	}

	// Returns a.b, always taking up 5 characters as long as a and b are less
	// than 100, adding leading or trailing spaces if necessary.
	string SpaceAlignedDotFormat(int a, int b) {
		string result = "";
		if (a < 10) {
			result += " ";
		}
		result += $"{a}.{b}";
		if (b < 10) {
			result += " ";
		}
		return result;
	}

	// Shows the residents sorted by UpdateCityRow, reusing the row's heads.
	void UpdatePopHeads(CityRow cityRow, int residentCount, int maxWidth, int eraNum) {
		// Leave a 1 head gap if we have specialists.
		int width = residentCount * PopHead.HEAD_SIZE;
		if (specialists.Count > 0) {
			width += PopHead.HEAD_SIZE;
		}

		// Leave a 1 head gap between each section of moods.
		int numMoodsPresent = (happyResidents.Count > 0 ? 1 : 0)
			+ (contentResidents.Count > 0 ? 1: 0)
			+ (unhappyResidents.Count > 0 ? 1: 0);
		width += (numMoodsPresent - 1) * PopHead.HEAD_SIZE;

		// Figure out the actual spacing we'll use, to ensure we fit withing the
		// bounds of our container.
		int spacer = PopHead.HEAD_SIZE;
		if (width > maxWidth) {
			spacer = (int)((float)maxWidth / width * spacer);
		}

		int xPos = 0;
		int headIndex = 0;

		// Add each of the default citizens. These are buttons with the idea that
		// we can eventually support clicking on the heads to view details, such
		// as the reason for unhappiness.
		foreach (CityResident cr in happyResidents) {
			xPos = AddCitizen(cityRow, headIndex++, cr, xPos, spacer, eraNum);
		}
		if (happyResidents.Count > 0 && (contentResidents.Count > 0 || unhappyResidents.Count > 0)) {
			xPos += spacer;
		}
		foreach (CityResident cr in contentResidents) {
			xPos = AddCitizen(cityRow, headIndex++, cr, xPos, spacer, eraNum);
		}
		if (contentResidents.Count > 0 && unhappyResidents.Count > 0) {
			xPos += spacer;
		}
		foreach (CityResident cr in unhappyResidents) {
			xPos = AddCitizen(cityRow, headIndex++, cr, xPos, spacer, eraNum);
		}

		// Add space before specialists.
		xPos += spacer;

		// Add the specialists.
		foreach (CityResident cr in specialists) {
			xPos = AddCitizen(cityRow, headIndex++, cr, xPos, spacer, eraNum);
		}

		// Drop the heads this city no longer needs.
		while (cityRow.popHeads.Count > headIndex) {
			TextureRect extra = cityRow.popHeads[cityRow.popHeads.Count - 1];
			cityRow.popHeads.RemoveAt(cityRow.popHeads.Count - 1);
			cityRow.populationContainer.RemoveChild(extra);
			extra.QueueFree();
		}
	}

	private int AddCitizen(CityRow cityRow, int headIndex, CityResident cr, int xPos, int spacer, int eraNum) {
		TextureRect tr;
		if (headIndex < cityRow.popHeads.Count) {
			tr = cityRow.popHeads[headIndex];
		} else {
			tr = new() { MouseFilter = Control.MouseFilterEnum.Pass, Theme = PopHead.TooltipTheme };
			cityRow.populationContainer.AddChild(tr);
			cityRow.popHeads.Add(tr);
		}
		tr.Texture = PopHead.GetTexture(cr, eraNum);
		tr.TooltipText = PopHead.GetTooltip(cr);
		tr.SetPosition(new Vector2(xPos, 0));
		return xPos + spacer;
	}
}
