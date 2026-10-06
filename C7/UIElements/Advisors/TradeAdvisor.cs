using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using Godot;
using Resource = C7GameData.Resource;

[GlobalClass]
[Tool]
public partial class TradeAdvisor : Control {

	[Export] public TextureRect background;

	private TextureButton _close;
	private TextureRect _advisorHead;
	private TextureButton _dialogBox;
	private Label _dialogBoxLabel;

	private HBoxContainer _localResources;
	private HBoxContainer _importedResources;
	private HBoxContainer _exportedResources;
	private VBoxContainer _networkList;
	private Label _citiesHeader;
	private VBoxContainer _cityList;
	private VBoxContainer _partnerList;

	// Which of the networks listed on the left has its cities shown.
	private int _selectedNetwork = 0;

	// The height of a list row, so rows sit on the background's ruled lines.
	private const float RowHeight = 44;

	public TradeAdvisor() {
		MouseFilter = MouseFilterEnum.Stop;
	}

	public override void _Ready() {
		this.CreateUI();
	}

	private void CreateUI() {
		background.Texture = TextureLoader.Load("advisors.trade.background");

		_advisorHead = AdvisorUtils.CreateAdvisorHead(background, AdvisorHead.Advisor.Trade);
		_close = AdvisorUtils.CreateExitButton(background);
		_close.Pressed += () => { this.GetParent<Advisors>().Hide(); };
		(_dialogBox, _dialogBoxLabel) = AdvisorUtils.CreateAdvisorDialogBox(background);

		AdvisorUtils.CreateAdvisorTitle(background, background.Texture.GetWidth(), "TRADE ADVISOR");
		AdvisorUtils.CreateAdvisorSidebar(background, AdvisorHead.Advisor.Trade);

		// The resources we have, top, a band each for where they come from
		// or go to.
		AdvisorUtils.CreateLabel(background, "Luxuries and Strategic Resources", new Rect2(96, 110, 680, 30), 14, HorizontalAlignment.Center);
		_localResources = CreateResourceBand("Local", 142, 60);
		_importedResources = CreateResourceBand("Import", 202, 60);
		_exportedResources = CreateResourceBand("Export", 262, 36);

		// Our trade networks, and the cities on the chosen one.
		AdvisorUtils.CreateLabel(background, "Trading Cities", new Rect2(90, 322, 156, 20), 13, HorizontalAlignment.Center);
		_networkList = AdvisorUtils.CreateList(background, new Rect2(90, 344, 156, 362));
		_citiesHeader = AdvisorUtils.CreateLabel(background, "Cities", new Rect2(280, 322, 244, 20), 13, HorizontalAlignment.Center);
		_cityList = AdvisorUtils.CreateList(background, new Rect2(280, 344, 244, 352));

		// The civs we know, by whether we can trade with them.
		_partnerList = AdvisorUtils.CreateList(background, new Rect2(584, 322, 350, 374));
	}

	private HBoxContainer CreateResourceBand(string name, float top, float height) {
		AdvisorUtils.CreateLabel(background, name, new Rect2(100, top, 70, height), 13);
		HBoxContainer icons = new() { Position = new Vector2(170, top), Size = new Vector2(610, height) };
		icons.AddThemeConstantOverride("separation", 4);
		background.AddChild(icons);
		return icons;
	}

	public void ShowAdvisor() {
		Show();

		EngineStorage.ReadGameData((GameData gameData) => {
			Player player = gameData.GetUIControllerPlayer();

			FillResources(_localResources, TradeReport.LocalResources(gameData, player)
				.OrderBy(r => r.Key.Category).ThenBy(r => r.Key.Name)
				.Select(r => (r.Key, r.Value > 1 ? $"{r.Key.Name} ({r.Value})" : r.Key.Name)), 40);
			List<ResourceDeal> imports = TradeReport.ResourceDeals(gameData, player, DealDetails.Inbound);
			FillResources(_importedResources, imports.Select(d =>
				(d.resource, $"{d.resource.Name} from the {AdvisorUtils.CivName(d.partner)} ({d.turnsRemaining} turns)")), 40);
			List<ResourceDeal> exports = TradeReport.ResourceDeals(gameData, player, DealDetails.Outbound);
			FillResources(_exportedResources, exports.Select(d =>
				(d.resource, $"{d.resource.Name} to the {AdvisorUtils.CivName(d.partner)} ({d.turnsRemaining} turns)")), 30);

			FillNetworks(gameData, player);
			FillPartners(gameData, player);

			_dialogBoxLabel.Text = Advice(gameData, player);
			_advisorHead.Texture = AdvisorHead.GetPopupImage(AdvisorHead.Advisor.Trade, AdvisorHead.Mood.Happy, player.EraIndex());
		});
	}

	private static void FillResources(HBoxContainer band, IEnumerable<(Resource resource, string tooltip)> resources, float iconSize) {
		AdvisorUtils.ClearList(band);
		foreach ((Resource resource, string tooltip) in resources) {
			TextureRect icon = AdvisorUtils.MakeIcon(TextureLoader.Load("resources.icon", resource, useCache: true), iconSize);
			icon.TooltipText = tooltip;
			band.AddChild(icon);
		}
		if (band.GetChildCount() == 0) {
			band.AddChild(AdvisorUtils.MakeLabel("None", 12));
		}
	}

	// The player's trade networks: the capital's first, then any other
	// cities linked to each other, then the cities on their own.
	private static List<(string name, List<City> cities)> Networks(GameData gameData, Player player) {
		List<List<City>> groups = gameData.GetTradeNetwork().CityGroups(player);
		List<(string, List<City>)> result = new();
		List<City> unconnected = new();
		for (int i = 0; i < groups.Count; i++) {
			if (i == 0) {
				result.Add(("Capital network", groups[i]));
			} else if (groups[i].Count > 1) {
				result.Add(($"Network {result.Count + 1}", groups[i]));
			} else {
				unconnected.AddRange(groups[i]);
			}
		}
		if (unconnected.Count > 0) {
			result.Add(("Unconnected Cities", unconnected));
		}
		return result;
	}

	private void FillNetworks(GameData gameData, Player player) {
		AdvisorUtils.ClearList(_networkList);
		AdvisorUtils.ClearList(_cityList);
		List<(string name, List<City> cities)> networks = Networks(gameData, player);
		if (networks.Count == 0) {
			_citiesHeader.Text = "Cities";
			return;
		}
		if (_selectedNetwork >= networks.Count) {
			_selectedNetwork = 0;
		}

		for (int i = 0; i < networks.Count; i++) {
			int index = i;
			(string name, List<City> cities) = networks[i];
			Button button = new() {
				Text = $"{name}\n{cities.Count} {(cities.Count == 1 ? "city" : "cities")}",
				Flat = true,
				ToggleMode = true,
				ButtonPressed = i == _selectedNetwork,
				CustomMinimumSize = new Vector2(150, RowHeight),
			};
			button.AddThemeColorOverride("font_color", Colors.Black);
			button.AddThemeColorOverride("font_pressed_color", new Color(0.45f, 0, 0.45f));
			button.AddThemeColorOverride("font_hover_color", new Color(0.45f, 0, 0.45f));
			button.AddThemeFontSizeOverride("font_size", 12);
			button.Pressed += () => {
				_selectedNetwork = index;
				ShowAdvisor();
			};
			_networkList.AddChild(button);
		}

		(string selectedName, List<City> selectedCities) = networks[_selectedNetwork];
		_citiesHeader.Text = selectedName;
		foreach (City city in selectedCities) {
			_cityList.AddChild(AdvisorUtils.MakeRow(RowHeight,
				AdvisorUtils.MakeCityButton(this, city, 145),
				AdvisorUtils.MakeLabel(city.IsCapital() ? "Capital" : "", 12, 70, HorizontalAlignment.Right)));
		}
	}

	private void FillPartners(GameData gameData, Player player) {
		AdvisorUtils.ClearList(_partnerList);
		List<Player> rivals = gameData.GetKnownRivals(player);
		if (rivals.Count == 0) {
			_partnerList.AddChild(AdvisorUtils.MakeRow(RowHeight,
				AdvisorUtils.MakeLabel("Alas, we know of no one with whom to trade", 13)));
			return;
		}

		AddPartnerSection(gameData, player, rivals, TradeStatus.TradingWith, "Are trading with");
		AddPartnerSection(gameData, player, rivals, TradeStatus.CanTradeWith, "Can trade with");
		AddPartnerSection(gameData, player, rivals, TradeStatus.CannotTradeWith, "Cannot Trade With");
	}

	private void AddPartnerSection(GameData gameData, Player player, List<Player> rivals, TradeStatus status, string title) {
		List<Player> partners = rivals.Where(r => TradeReport.StatusWith(player, r) == status).ToList();
		if (partners.Count == 0) {
			return;
		}
		_partnerList.AddChild(AdvisorUtils.MakeRow(RowHeight, AdvisorUtils.MakeLabel(title, 14)));
		foreach (Player partner in partners) {
			// The civ and why it's in this section, with what it has to spare
			// below.
			VBoxContainer details = new() { SizeFlagsVertical = SizeFlags.ShrinkCenter };
			details.AddThemeConstantOverride("separation", 0);
			details.AddChild(AdvisorUtils.MakeRow(20,
				AdvisorUtils.MakeLabel($"the {AdvisorUtils.CivName(partner)}", 13, 120),
				AdvisorUtils.MakeLabel(Reason(player, partner, status), 12, 200)));
			HBoxContainer spare = AdvisorUtils.MakeRow(22);
			spare.AddThemeConstantOverride("separation", 2);
			foreach ((Resource resource, int count) in TradeReport.ExcessResources(gameData, partner, player)) {
				TextureRect icon = AdvisorUtils.MakeIcon(TextureLoader.Load("resources.icon", resource, useCache: true), 22);
				icon.TooltipText = $"{resource.Name} ({count})";
				spare.AddChild(icon);
			}
			if (spare.GetChildCount() > 0) {
				details.AddChild(spare);
			}
			_partnerList.AddChild(AdvisorUtils.MakeRow(RowHeight, AdvisorUtils.MakeSpacer(16), details));
		}
	}

	// Why a civ is in its section, or what we trade with them.
	private static string Reason(Player player, Player partner, TradeStatus status) {
		if (status == TradeStatus.CannotTradeWith) {
			if (PlayerRelationship.AtWar(player, partner)) {
				return "We are at war";
			}
			if (TradeReport.HasEmbargoAgainst(player, partner)) {
				return "We have an embargo";
			}
			return "Embargo against us";
		}
		if (status == TradeStatus.TradingWith
			&& player.playerRelationships.TryGetValue(partner.id, out PlayerRelationship relationship)) {
			int gold = relationship.multiTurnDeals
				.Where(d => d.dealSubType == DealSubType.GoldPerTurn)
				.Sum(d => d.dealDetails == DealDetails.Inbound ? d.goldPerTurn : -d.goldPerTurn);
			int resources = relationship.multiTurnDeals.Count(d => d.dealSubType is DealSubType.ResourcePerTurn or DealSubType.LuxuryPerTurn);
			List<string> parts = new();
			if (resources > 0) {
				parts.Add($"{resources} {(resources == 1 ? "resource" : "resources")}");
			}
			if (gold != 0) {
				parts.Add($"{(gold > 0 ? "+" : "")}{gold} gold/turn");
			}
			return string.Join(", ", parts);
		}
		return "";
	}

	private static string Advice(GameData gameData, Player player) {
		List<(string name, List<City> cities)> networks = Networks(gameData, player);
		List<City> unconnected = networks.Where(n => n.name == "Unconnected Cities").SelectMany(n => n.cities).ToList();
		if (unconnected.Count > 0) {
			return $"{unconnected[0].name} needs a road to our trade network.";
		}
		// Imported luxuries count, as they reach the cities too.
		int luxuries = TradeReport.CapitalResources(gameData, player).Keys.Count(r => r.Category == ResourceCategory.LUXURY);
		if (luxuries == 0) {
			return "We have no luxuries. Trading for some would make our people happier.";
		}
		return $"Our cities share {luxuries} {(luxuries == 1 ? "luxury" : "luxuries")}.";
	}
}
