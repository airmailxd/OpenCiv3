using C7Engine;
using C7GameData;
using Godot;
using Resource = C7GameData.Resource;
using System;
using System.Linq;
using System.Collections.Generic;
using ConvertCiv3Media;

public partial class TradeOfferUi : Tree {
	TreeItem peaceTreaty;
	TreeItem rightOfPassage;
	TreeItem lumpSumGold;
	TreeItem goldPerTurn;
	// The resource (by key) each resource item stands for.
	Dictionary<TreeItem, string> resources = new();
	// The tech each technology item stands for.
	Dictionary<TreeItem, Tech> techs = new();
	// The city each city item stands for.
	Dictionary<TreeItem, City> cities = new();
	TradeOffer currentOffer;
	List<Tech> tradeableTechs;
	int playerGold;

	public TradeOfferUi(Theme fontTheme, List<Tech> tradeableTechs, List<City> tradeableCities, int playerGold,
						TradeOffer currentOffer, bool requiresPeaceTreaty,
						HorizontalAlignment alignment, List<Resource> tradeableResources = null) {
		this.currentOffer = currentOffer;
		this.tradeableTechs = tradeableTechs;
		this.playerGold = playerGold;
		this.Columns = 1;
		this.AllowRmbSelect = true;
		TradingTree.ConfigureTreeTheme(this, fontTheme);
		TreeItem root = TradingTree.CreateTreeRoot(this);

		// Match the size of the texture used in the deal screen.
		this.Size = new Vector2(190, 100);

		if (requiresPeaceTreaty) {
			peaceTreaty = this.CreateItem(root);
			peaceTreaty.SetTextAlignment(0, alignment);
			peaceTreaty.SetText(0, "Peace Treaty");
		}

		// Always there, hidden unless offered: an AI may offer one.
		rightOfPassage = this.CreateItem(root);
		rightOfPassage.SetTextAlignment(0, alignment);
		rightOfPassage.SetText(0, "Right of Passage");

		// Add tree items for all the possible things that could be traded - we
		// determine whether they're visible based on the current offer in
		// RefreshUiForOffer.
		lumpSumGold = this.CreateItem(root);
		lumpSumGold.SetTextAlignment(0, alignment);
		goldPerTurn = this.CreateItem(root);
		goldPerTurn.SetTextAlignment(0, alignment);

		foreach (Resource r in tradeableResources ?? new()) {
			TreeItem child = this.CreateItem(root);
			child.SetTextAlignment(0, alignment);
			child.SetText(0, r.Name);
			resources[child] = r.Key;
		}

		foreach (Tech tech in tradeableTechs) {
			TreeItem child = this.CreateItem(root);
			child.SetTextAlignment(0, alignment);
			child.SetText(0, tech.Name);
			techs[child] = tech;
		}

		foreach (City city in tradeableCities) {
			TreeItem child = this.CreateItem(root);
			child.SetTextAlignment(0, alignment);
			child.SetText(0, $"{city.name} ({city.residents.Count})");
			cities[child] = city;
		}

		RefreshUiForOffer();
	}

	public void HandleClicks(TradeOfferUi other, Action offerUpdated) {
		this.ItemMouseSelected += (Vector2 mousePos, long mouseButtonIndex) => {
			TreeItem ti = this.GetSelected();
			ti.Deselect(0);

			// Handle techs being clicked on.
			if (techs.TryGetValue(ti, out Tech itemTech)) {
				Tech t = tradeableTechs.Find(x => x.Name == itemTech.Name);
				currentOffer.techs.Remove(t);
			}
			if (cities.TryGetValue(ti, out City itemCity)) {
				currentOffer.cities.Remove(itemCity);
			}
			if (resources.TryGetValue(ti, out string resourceKey)) {
				currentOffer.resources.Remove(resourceKey);
			}
			if (ti == goldPerTurn) {
				currentOffer.goldPerTurn = 0;
			}
			if (ti == lumpSumGold && mouseButtonIndex != 2) {
				currentOffer.gold = null;
			}

			// Allow right clicking the gold amount to change it.
			if (ti == lumpSumGold && mouseButtonIndex == 2) {
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
											"Gold: ", "" + currentOffer.gold.Value,
											BoxContainer.AlignmentMode.Center,
											handleTextInput));
			}

			// A right of passage is mutual, so it leaves both sides of the
			// table.
			if (ti == rightOfPassage) {
				currentOffer.rightOfPassage = false;
				other.currentOffer.rightOfPassage = false;
			}

			if (ti == peaceTreaty) {
				// If we click the peace treaty off the trading table, remove
				// everything else too.
				if (currentOffer.partOfPeaceTreaty) {
					currentOffer.Clear();
					other.currentOffer.Clear();
				} else {
					currentOffer.partOfPeaceTreaty = true;
				}
			}

			offerUpdated();
			RefreshUiForOffer();
		};
	}

	public void RefreshUiForOffer() {
		if (currentOffer.gold.HasValue) {
			lumpSumGold.SetText(0, $"{currentOffer.gold.Value} gold");
			lumpSumGold.Visible = true;
		} else {
			lumpSumGold.Visible = false;
		}

		goldPerTurn.SetText(0, $"{currentOffer.goldPerTurn} gold per turn");
		goldPerTurn.Visible = currentOffer.goldPerTurn > 0;
		foreach (var (ti, key) in resources) {
			ti.Visible = currentOffer.resources.Contains(key);
		}

		HashSet<string> offeredTechs = new(currentOffer.techs.Select(x => x.Name));
		foreach (var (ti, tech) in techs) {
			ti.Visible = offeredTechs.Contains(tech.Name);
		}
		foreach (var (ti, city) in cities) {
			ti.Visible = currentOffer.cities.Contains(city);
		}

		if (peaceTreaty != null) {
			peaceTreaty.Visible = currentOffer.partOfPeaceTreaty;
		}
		rightOfPassage.Visible = currentOffer.rightOfPassage;
	}
}
