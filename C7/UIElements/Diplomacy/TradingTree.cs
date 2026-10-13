using C7Engine;
using System.Linq;
using C7GameData;
using Godot;
using Resource = C7GameData.Resource;
using System;
using System.Collections.Generic;
using ConvertCiv3Media;

public partial class TradingTree : Tree {
	TreeItem goldHeader;
	TreeItem techHeader;
	TreeItem lumpSum;
	TreeItem perTurn;
	TreeItem luxuryHeader;
	TreeItem resourceHeader;
	// The resource (by key) each luxury or resource item stands for.
	Dictionary<TreeItem, string> resourceItems = new();
	// The tech each technology item stands for.
	Dictionary<TreeItem, Tech> techItems = new();
	TreeItem diplomacyHeader;
	TreeItem peaceTreaty;
	TreeItem rightOfPassage;
	TreeItem cityHeader;
	// The city each city item stands for.
	Dictionary<TreeItem, City> cityItems = new();

	List<Tech> tradeableTechs;
	TradeOffer currentOffer;
	int playerGold;
	int maxGoldPerTurn;

	// maxGoldPerTurn is the most gold per turn the player can promise (see
	// TradeOffer.MaxGoldPerTurn), and tradeableResources the luxuries and
	// strategic resources it has to spare (TradeOffer.TradableResources).
	public TradingTree(Theme fontTheme,
						int playerGold,
						List<Tech> tradeableTechs, List<City> tradeableCities,
						TradeOffer currentOffer, bool requiresPeaceTreaty,
						bool canSignRightOfPassage = false,
						int maxGoldPerTurn = 0, List<Resource> tradeableResources = null) {
		this.tradeableTechs = tradeableTechs;
		this.currentOffer = currentOffer;
		this.playerGold = playerGold;
		this.maxGoldPerTurn = maxGoldPerTurn;
		this.Columns = 1;

		ConfigureTreeTheme(this, fontTheme);
		TreeItem root = CreateTreeRoot(this);

		// Match the size of the texture used in the deal screen.
		this.Size = new Vector2(190, 400);

		if (requiresPeaceTreaty) {
			diplomacyHeader = this.CreateItem(root);
			diplomacyHeader.SetText(0, "Diplomatic Agreements");
			diplomacyHeader.Collapsed = false;

			peaceTreaty = this.CreateItem(diplomacyHeader);
			peaceTreaty.SetText(0, "Peace Treaty");
		} else if (canSignRightOfPassage) {
			// Only offered to civs that may sign one (see RightOfPassage).
			diplomacyHeader = this.CreateItem(root);
			diplomacyHeader.SetText(0, "Diplomatic Agreements");
			diplomacyHeader.Collapsed = false;

			rightOfPassage = this.CreateItem(diplomacyHeader);
			rightOfPassage.SetText(0, "Right of Passage");

			// TODO: mutual protection pacts, military alliances, etc.
		}

		// Gold
		{
			goldHeader = this.CreateItem(root);
			goldHeader.SetText(0, $"Gold ({playerGold} in treasury)");
			goldHeader.Collapsed = true;

			lumpSum = this.CreateItem(goldHeader);
			lumpSum.SetText(0, "Lump sum");

			if (maxGoldPerTurn > 0) {
				perTurn = this.CreateItem(goldHeader);
				perTurn.SetText(0, $"Per turn ({maxGoldPerTurn} available)");
			}
		}

		// Only spare luxuries and strategic resources may be traded, and not
		// in a peace treaty (see TradeOffer.ProblemWithDeal).
		if (!requiresPeaceTreaty && tradeableResources != null) {
			foreach (Resource r in tradeableResources) {
				bool luxury = r.Category == ResourceCategory.LUXURY;
				TreeItem header = luxury ? luxuryHeader : resourceHeader;
				if (header == null) {
					header = this.CreateItem(root);
					header.SetText(0, luxury ? "Luxuries" : "Resources");
					header.Collapsed = true;
					if (luxury) {
						luxuryHeader = header;
					} else {
						resourceHeader = header;
					}
				}
				TreeItem child = this.CreateItem(header);
				child.SetText(0, r.Name);
				resourceItems[child] = r.Key;
			}
		}

		if (tradeableTechs.Count > 0) {
			techHeader = this.CreateItem(root);
			techHeader.SetText(0, "Technology");
			techHeader.Collapsed = true;

			foreach (Tech tech in tradeableTechs) {
				TreeItem child = this.CreateItem(techHeader);
				child.SetText(0, tech.Name);
				techItems[child] = tech;
			}
		}

		if (tradeableCities.Count > 0) {
			cityHeader = this.CreateItem(root);
			cityHeader.SetText(0, "Cities");
			cityHeader.Collapsed = true;

			foreach (City city in tradeableCities) {
				TreeItem child = this.CreateItem(cityHeader);
				child.SetText(0, $"{city.name} ({city.residents.Count})");
				cityItems[child] = city;
			}
		}

		RefreshUiForOffer();
	}

	public void HandleClicks(TradingTree other, Action offerUpdated) {
		this.ItemSelected += () => {
			TreeItem ti = this.GetSelected();

			// Implement header collapsing.
			ti.Collapsed = !ti.Collapsed;
			ti.Deselect(0);

			// Ensure that we are synced with the folding of the other tree.
			if (ti == goldHeader && other.goldHeader != null) {
				other.goldHeader.Collapsed = goldHeader.Collapsed;
			}
			if (ti == techHeader && other.techHeader != null) {
				other.techHeader.Collapsed = techHeader.Collapsed;
			}
			if (ti == diplomacyHeader && other.diplomacyHeader != null) {
				other.diplomacyHeader.Collapsed = diplomacyHeader.Collapsed;
			}
			if (ti == cityHeader && other.cityHeader != null) {
				other.cityHeader.Collapsed = cityHeader.Collapsed;
			}
			if (ti == luxuryHeader && other.luxuryHeader != null) {
				other.luxuryHeader.Collapsed = luxuryHeader.Collapsed;
			}
			if (ti == resourceHeader && other.resourceHeader != null) {
				other.resourceHeader.Collapsed = resourceHeader.Collapsed;
			}

			if (resourceItems.TryGetValue(ti, out string resourceKey) && !currentOffer.resources.Contains(resourceKey)) {
				currentOffer.resources.Add(resourceKey);
			}

			if (cityItems.TryGetValue(ti, out City itemCity) && !currentOffer.cities.Contains(itemCity)) {
				currentOffer.cities.Add(itemCity);
			}

			// Handle techs being clicked on.
			if (techItems.TryGetValue(ti, out Tech itemTech)) {
				Tech t = tradeableTechs.Find(x => x.Name == itemTech.Name);
				currentOffer.techs.Add(t);
			}

			// Allow user input for gold amounts.
			if (ti == lumpSum) {
				Diplomacy diplomacy = GetParent<DealScreen>().GetParent<Diplomacy>();
				var handleTextInput = (string input) => {
					// The deal screen may have been replaced while the
					// dialog was up.
					if (!IsInstanceValid(this) || !IsInsideTree()) {
						return;
					}
					int gold = 0;
					bool parsed = int.TryParse(input, out gold);
					if (!parsed) {
						return;
					}
					if (gold > playerGold || gold <= 0) {
						diplomacy.ShowScreenPopup(new InformationalPopup("Insufficient gold"));
						return;
					}

					currentOffer.gold = gold;
					offerUpdated();
					RefreshUiForOffer();
				};

				diplomacy.ShowScreenPopup(new TextDialog("Enter amount...",
											"Gold: ", "0",
											BoxContainer.AlignmentMode.Center,
											handleTextInput));
			}

			if (ti != null && ti == perTurn) {
				Diplomacy diplomacy = GetParent<DealScreen>().GetParent<Diplomacy>();
				var handleGoldPerTurn = (string input) => {
					// The deal screen may have been replaced while the
					// dialog was up.
					if (!IsInstanceValid(this) || !IsInsideTree()) {
						return;
					}
					if (!int.TryParse(input, out int amount)) {
						return;
					}
					if (amount > maxGoldPerTurn || amount <= 0) {
						diplomacy.ShowScreenPopup(new InformationalPopup("Insufficient gold"));
						return;
					}
					currentOffer.goldPerTurn = amount;
					offerUpdated();
					RefreshUiForOffer();
				};
				diplomacy.ShowScreenPopup(new TextDialog("Enter amount...",
											"Gold per turn: ", "0",
											BoxContainer.AlignmentMode.Center,
											handleGoldPerTurn));
			}

			if (ti == peaceTreaty) {
				currentOffer.partOfPeaceTreaty = true;
				other.currentOffer.partOfPeaceTreaty = true;
			}

			// A right of passage is mutual, so it goes on both sides of the
			// table.
			if (ti != null && ti == rightOfPassage) {
				currentOffer.rightOfPassage = true;
				other.currentOffer.rightOfPassage = true;
			}

			offerUpdated();
			RefreshUiForOffer();
		};
	}

	public void RefreshUiForOffer() {
		bool needsPeaceTreaty = peaceTreaty != null && !currentOffer.partOfPeaceTreaty;

		if (needsPeaceTreaty) {
			goldHeader.Visible = false;
			peaceTreaty.Visible = true;

			if (techHeader != null) {
				techHeader.Visible = false;
			}
			if (cityHeader != null) {
				cityHeader.Visible = false;
			}
		} else {
			goldHeader.Visible = true;
			lumpSum.Visible = !currentOffer.gold.HasValue;
			if (perTurn != null) {
				perTurn.Visible = currentOffer.goldPerTurn == 0;
			}
			foreach (var (ti, key) in resourceItems) {
				ti.Visible = !currentOffer.resources.Contains(key);
			}

			if (techHeader != null) {
				techHeader.Visible = true;
			}

			HashSet<string> offeredTechs = new(currentOffer.techs.Select(x => x.Name));
			foreach (var (ti, tech) in techItems) {
				ti.Visible = !offeredTechs.Contains(tech.Name);
			}

			if (cityHeader != null) {
				cityHeader.Visible = true;
			}
			foreach (var (ti, city) in cityItems) {
				ti.Visible = !currentOffer.cities.Contains(city);
			}

			if (peaceTreaty != null) {
				peaceTreaty.Visible = false;
			}
			if (rightOfPassage != null) {
				rightOfPassage.Visible = !currentOffer.rightOfPassage;
			}
		}
	}

	public static TreeItem CreateTreeRoot(Tree tree) {
		// All trees have one root, but we don't want a single root, so hide it.
		TreeItem root = tree.CreateItem();
		tree.HideRoot = true;
		tree.HideFolding = true;

		return root;
	}

	// The central styling for the trading tree and trade offer ui trees.
	public static void ConfigureTreeTheme(Tree tree, Theme fontTheme) {
		// Configure the tree to look like we expect it to in civ - a transparent
		// background with no lines between items.
		Color transparent = new Color(0, 0, 0, 0);
		tree.Theme = fontTheme;
		// A style box of our own, so the theme shared by the whole game is
		// left alone. (Tree has no "bg" style box in Godot 4 to copy.)
		StyleBoxFlat styleBox = new() {
			BgColor = transparent,
			BorderWidthLeft = 0,
			BorderWidthRight = 0,
			BorderWidthTop = 0,
			BorderWidthBottom = 0,
		};
		tree.AddThemeStyleboxOverride("panel", styleBox);
		tree.AddThemeStyleboxOverride("focus", styleBox);
		tree.AddThemeColorOverride("font_color", Colors.Black);
		tree.AddThemeColorOverride("children_hl_line_color", transparent);
		tree.AddThemeColorOverride("parent_hl_line_color", transparent);
		tree.AddThemeColorOverride("relationship_line_color", transparent);
		tree.AddThemeColorOverride("guide_color", transparent);
		tree.AddThemeConstantOverride("v_separation", 1);
	}
}
