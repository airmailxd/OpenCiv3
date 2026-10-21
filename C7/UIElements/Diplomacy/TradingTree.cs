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
	// Whether the two civs' capitals are connected, so resources can be
	// traded (TradeNetwork.CapitalsConnected).
	readonly bool resourcesConnected;
	// The tech each technology item stands for.
	Dictionary<TreeItem, Tech> techItems = new();
	TreeItem diplomacyHeader;
	TreeItem peaceTreaty;
	TreeItem rightOfPassage;
	TreeItem mutualProtectionPact;
	// The civ each military alliance or trade embargo item is against,
	// under a heading of each: "Military Alliance against the Portuguese"
	// in one line didn't fit, and lost the civ's name to an ellipsis.
	TreeItem allianceHeader;
	TreeItem embargoHeader;
	Dictionary<TreeItem, ID> allianceItems = new();
	Dictionary<TreeItem, ID> embargoItems = new();
	TreeItem cityHeader;
	// The city each city item stands for.
	Dictionary<TreeItem, City> cityItems = new();
	TreeItem workerHeader;
	// The worker each worker item stands for.
	Dictionary<TreeItem, MapUnit> workerItems = new();
	TreeItem mapHeader;
	TreeItem worldMap;
	TreeItem territoryMap;
	TreeItem communicationsHeader;
	// The civ each communications item introduces.
	Dictionary<TreeItem, ID> contactItems = new();

	List<Tech> tradeableTechs;
	TradeOffer currentOffer;
	int playerGold;
	int maxGoldPerTurn;

	// maxGoldPerTurn is the most gold per turn the player can promise (see
	// TradeOffer.MaxGoldPerTurn), and tradeableResources the luxuries and
	// strategic resources it has to spare (TradeOffer.TradableResources).
	// The pacts (see Pacts) the two civs may sign: a mutual protection
	// pact, and military alliances and trade embargoes against these civs.
	public record PactChoices(bool mutualProtectionPact, List<Player> allianceTargets, List<Player> embargoTargets) {
		public bool Any => mutualProtectionPact || allianceTargets.Count > 0 || embargoTargets.Count > 0;
	}

	// Whether this side may give its maps (see TradeOffer.ProblemTradingMaps),
	// and the civs it may introduce the other side to (TradeOffer.TradableContacts).
	public record MapChoices(bool maps, List<Player> contacts);

	public TradingTree(Theme fontTheme,
						int playerGold,
						List<Tech> tradeableTechs, List<City> tradeableCities,
						TradeOffer currentOffer, bool requiresPeaceTreaty,
						bool canSignRightOfPassage = false,
						int maxGoldPerTurn = 0, List<Resource> tradeableResources = null, PactChoices pacts = null,
						MapChoices maps = null, bool resourcesConnected = true, List<MapUnit> tradeableWorkers = null) {
		this.resourcesConnected = resourcesConnected;
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
		} else if (canSignRightOfPassage || (pacts?.Any ?? false)) {
			// Only what the civs may sign (see RightOfPassage and Pacts).
			diplomacyHeader = this.CreateItem(root);
			diplomacyHeader.SetText(0, "Diplomatic Agreements");
			diplomacyHeader.Collapsed = false;

			if (canSignRightOfPassage) {
				rightOfPassage = this.CreateItem(diplomacyHeader);
				rightOfPassage.SetText(0, "Right of Passage");
			}
			if (pacts?.mutualProtectionPact ?? false) {
				mutualProtectionPact = this.CreateItem(diplomacyHeader);
				mutualProtectionPact.SetText(0, "Mutual Protection Pact");
			}
			if ((pacts?.allianceTargets.Count ?? 0) > 0) {
				allianceHeader = this.CreateItem(diplomacyHeader);
				allianceHeader.SetText(0, "Military Alliance");
				foreach (Player target in pacts.allianceTargets) {
					TreeItem child = this.CreateItem(allianceHeader);
					child.SetText(0, $"against the {target.civilization?.noun}");
					allianceItems[child] = target.id;
				}
			}
			if ((pacts?.embargoTargets.Count ?? 0) > 0) {
				embargoHeader = this.CreateItem(diplomacyHeader);
				embargoHeader.SetText(0, "Trade Embargo");
				foreach (Player target in pacts.embargoTargets) {
					TreeItem child = this.CreateItem(embargoHeader);
					child.SetText(0, $"against the {target.civilization?.noun}");
					embargoItems[child] = target.id;
				}
			}
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
				// Without a trade route between the capitals they're shown
				// but can't be picked: "Grayed-out resources indicate missing
				// trade connections or routes" (https://civfanatics.com/civ3/faq/).
				if (!resourcesConnected) {
					child.SetSelectable(0, false);
					child.SetCustomColor(0, new Color(0.5f, 0.5f, 0.5f));
					child.SetTooltipText(0, "No trade route links our capitals: a road, harbors in both cities with a route their ships can sail, or airports.");
				}
			}
		}

		// Civ3's "Maps" and "Communications" headings (Conquests'
		// Text/diplomacy.txt). Not in a peace treaty (see
		// TradeOffer.ProblemWithDeal).
		if (!requiresPeaceTreaty && (maps?.maps ?? false)) {
			mapHeader = this.CreateItem(root);
			mapHeader.SetText(0, "Maps");
			mapHeader.Collapsed = true;
			worldMap = this.CreateItem(mapHeader);
			worldMap.SetText(0, "World Map");
			territoryMap = this.CreateItem(mapHeader);
			territoryMap.SetText(0, "Territory Map");
		}
		if (!requiresPeaceTreaty && (maps?.contacts?.Count ?? 0) > 0) {
			communicationsHeader = this.CreateItem(root);
			communicationsHeader.SetText(0, "Communications");
			communicationsHeader.Collapsed = true;
			foreach (Player third in maps.contacts) {
				TreeItem child = this.CreateItem(communicationsHeader);
				child.SetText(0, $"with the {third.civilization?.noun}");
				contactItems[child] = third.id;
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

		// The workers in the capital (see TradeOffer.workers), named by
		// the nationality they keep.
		if ((tradeableWorkers?.Count ?? 0) > 0) {
			workerHeader = this.CreateItem(root);
			workerHeader.SetText(0, "Workers");
			workerHeader.Collapsed = true;

			foreach (MapUnit worker in tradeableWorkers) {
				TreeItem child = this.CreateItem(workerHeader);
				child.SetText(0, TradeOffer.WorkerLabel(worker));
				workerItems[child] = worker;
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
			if (ti == allianceHeader && other.allianceHeader != null) {
				other.allianceHeader.Collapsed = allianceHeader.Collapsed;
			}
			if (ti == embargoHeader && other.embargoHeader != null) {
				other.embargoHeader.Collapsed = embargoHeader.Collapsed;
			}
			if (ti == cityHeader && other.cityHeader != null) {
				other.cityHeader.Collapsed = cityHeader.Collapsed;
			}
			if (ti == workerHeader && other.workerHeader != null) {
				other.workerHeader.Collapsed = workerHeader.Collapsed;
			}
			if (ti == luxuryHeader && other.luxuryHeader != null) {
				other.luxuryHeader.Collapsed = luxuryHeader.Collapsed;
			}
			if (ti == resourceHeader && other.resourceHeader != null) {
				other.resourceHeader.Collapsed = resourceHeader.Collapsed;
			}
			if (ti == mapHeader && other.mapHeader != null) {
				other.mapHeader.Collapsed = mapHeader.Collapsed;
			}
			if (ti == communicationsHeader && other.communicationsHeader != null) {
				other.communicationsHeader.Collapsed = communicationsHeader.Collapsed;
			}

			// The world map shows all the territory map does, so one or
			// the other.
			if (ti != null && ti == worldMap) {
				currentOffer.worldMap = true;
				currentOffer.territoryMap = false;
			}
			if (ti != null && ti == territoryMap) {
				currentOffer.territoryMap = true;
				currentOffer.worldMap = false;
			}
			if (ti != null && contactItems.TryGetValue(ti, out ID contact) && !currentOffer.contacts.Contains(contact)) {
				currentOffer.contacts.Add(contact);
			}

			if (resourcesConnected && resourceItems.TryGetValue(ti, out string resourceKey) && !currentOffer.resources.Contains(resourceKey)) {
				currentOffer.resources.Add(resourceKey);
			}

			if (cityItems.TryGetValue(ti, out City itemCity) && !currentOffer.cities.Contains(itemCity)) {
				currentOffer.cities.Add(itemCity);
			}
			if (workerItems.TryGetValue(ti, out MapUnit itemWorker) && !currentOffer.workers.Contains(itemWorker)) {
				currentOffer.workers.Add(itemWorker);
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

			// So are the pacts.
			if (ti != null && ti == mutualProtectionPact) {
				currentOffer.mutualProtectionPact = true;
				other.currentOffer.mutualProtectionPact = true;
			}
			if (ti != null && allianceItems.TryGetValue(ti, out ID enemy)) {
				currentOffer.militaryAllianceAgainst = enemy;
				other.currentOffer.militaryAllianceAgainst = enemy;
			}
			if (ti != null && embargoItems.TryGetValue(ti, out ID embargoed)) {
				currentOffer.tradeEmbargoAgainst = embargoed;
				other.currentOffer.tradeEmbargoAgainst = embargoed;
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
			if (workerHeader != null) {
				workerHeader.Visible = false;
			}
		} else {
			if (worldMap != null) {
				worldMap.Visible = !currentOffer.worldMap;
				territoryMap.Visible = !currentOffer.worldMap && !currentOffer.territoryMap;
			}
			foreach (var (ti, contact) in contactItems) {
				ti.Visible = !currentOffer.contacts.Contains(contact);
			}
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
			if (workerHeader != null) {
				workerHeader.Visible = true;
			}
			foreach (var (ti, worker) in workerItems) {
				ti.Visible = !currentOffer.workers.Contains(worker);
			}

			if (peaceTreaty != null) {
				peaceTreaty.Visible = false;
			}
			if (mutualProtectionPact != null) {
				mutualProtectionPact.Visible = !currentOffer.mutualProtectionPact;
			}
			// One alliance and one embargo a deal.
			if (allianceHeader != null) {
				allianceHeader.Visible = currentOffer.militaryAllianceAgainst == null;
			}
			if (embargoHeader != null) {
				embargoHeader.Visible = currentOffer.tradeEmbargoAgainst == null;
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
