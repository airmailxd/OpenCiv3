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
	TreeItem mutualProtectionPact;
	// The civ each military alliance or trade embargo item is against.
	Dictionary<TreeItem, ID> alliances = new();
	Dictionary<TreeItem, ID> embargoes = new();
	TreeItem lumpSumGold;
	TreeItem goldPerTurn;
	// The resource (by key) each resource item stands for.
	Dictionary<TreeItem, string> resources = new();
	// The tech each technology item stands for.
	Dictionary<TreeItem, Tech> techs = new();
	// The city each city item stands for.
	Dictionary<TreeItem, City> cities = new();
	// The worker each worker item stands for.
	Dictionary<TreeItem, MapUnit> workers = new();
	TreeItem worldMap;
	TreeItem territoryMap;
	// The civ each communications item introduces.
	Dictionary<TreeItem, ID> contacts = new();
	TradeOffer currentOffer;
	List<Tech> tradeableTechs;
	int playerGold;

	public TradeOfferUi(Theme fontTheme, List<Tech> tradeableTechs, List<City> tradeableCities, int playerGold,
						TradeOffer currentOffer, bool requiresPeaceTreaty,
						HorizontalAlignment alignment, List<Resource> tradeableResources = null,
						TradingTree.PactChoices pacts = null, TradingTree.MapChoices maps = null,
						List<MapUnit> tradeableWorkers = null) {
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

		mutualProtectionPact = this.CreateItem(root);
		mutualProtectionPact.SetTextAlignment(0, alignment);
		mutualProtectionPact.SetText(0, "Mutual Protection Pact");
		foreach (Player target in pacts?.allianceTargets ?? new()) {
			TreeItem child = this.CreateItem(root);
			child.SetTextAlignment(0, alignment);
			child.SetText(0, $"Military Alliance against the {target.civilization?.noun}");
			Wrap(child);
			alliances[child] = target.id;
		}
		foreach (Player target in pacts?.embargoTargets ?? new()) {
			TreeItem child = this.CreateItem(root);
			child.SetTextAlignment(0, alignment);
			child.SetText(0, $"Trade Embargo against the {target.civilization?.noun}");
			Wrap(child);
			embargoes[child] = target.id;
		}

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

		foreach (MapUnit worker in tradeableWorkers ?? new()) {
			TreeItem child = this.CreateItem(root);
			child.SetTextAlignment(0, alignment);
			child.SetText(0, TradeOffer.WorkerLabel(worker));
			workers[child] = worker;
		}

		// Always there, hidden unless offered: an AI may offer one.
		worldMap = this.CreateItem(root);
		worldMap.SetTextAlignment(0, alignment);
		worldMap.SetText(0, "World Map");
		territoryMap = this.CreateItem(root);
		territoryMap.SetTextAlignment(0, alignment);
		territoryMap.SetText(0, "Territory Map");
		foreach (Player third in maps?.contacts ?? new()) {
			TreeItem child = this.CreateItem(root);
			child.SetTextAlignment(0, alignment);
			child.SetText(0, $"Communications with the {third.civilization?.noun}");
			Wrap(child);
			contacts[child] = third.id;
		}

		RefreshUiForOffer();
	}

	// Wraps an item too long for half of the table, like "Military
	// Alliance against the Portuguese", which otherwise lost the civ's name
	// to an ellipsis.
	private static void Wrap(TreeItem item) {
		item.SetAutowrapMode(0, TextServer.AutowrapMode.WordSmart);
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
			if (workers.TryGetValue(ti, out MapUnit itemWorker)) {
				currentOffer.workers.Remove(itemWorker);
			}
			if (resources.TryGetValue(ti, out string resourceKey)) {
				currentOffer.resources.Remove(resourceKey);
			}
			if (ti == goldPerTurn) {
				currentOffer.goldPerTurn = 0;
			}
			if (ti == worldMap) {
				currentOffer.worldMap = false;
			}
			if (ti == territoryMap) {
				currentOffer.territoryMap = false;
			}
			if (contacts.TryGetValue(ti, out ID contact)) {
				currentOffer.contacts.Remove(contact);
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
			if (ti == mutualProtectionPact) {
				currentOffer.mutualProtectionPact = false;
				other.currentOffer.mutualProtectionPact = false;
			}
			if (alliances.ContainsKey(ti)) {
				currentOffer.militaryAllianceAgainst = null;
				other.currentOffer.militaryAllianceAgainst = null;
			}
			if (embargoes.ContainsKey(ti)) {
				currentOffer.tradeEmbargoAgainst = null;
				other.currentOffer.tradeEmbargoAgainst = null;
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
		foreach (var (ti, worker) in workers) {
			ti.Visible = currentOffer.workers.Contains(worker);
		}
		worldMap.Visible = currentOffer.worldMap;
		territoryMap.Visible = currentOffer.territoryMap;
		foreach (var (ti, contact) in contacts) {
			ti.Visible = currentOffer.contacts.Contains(contact);
		}

		if (peaceTreaty != null) {
			peaceTreaty.Visible = currentOffer.partOfPeaceTreaty;
		}
		rightOfPassage.Visible = currentOffer.rightOfPassage;
		mutualProtectionPact.Visible = currentOffer.mutualProtectionPact;
		foreach (var (ti, target) in alliances) {
			ti.Visible = currentOffer.militaryAllianceAgainst == target;
		}
		foreach (var (ti, target) in embargoes) {
			ti.Visible = currentOffer.tradeEmbargoAgainst == target;
		}
	}
}
