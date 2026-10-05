using System.Collections.Generic;
using Godot;
using C7GameData;
using C7Engine;
using System;

[Tool]
[GlobalClass]
public partial class ProductionMenu : Civ3TextureRect {
	private Dictionary<TreeItem, IProducible> itemMapping = new();

	Tree tree;
	Theme fontTheme = new();

	public ProductionMenu() { }

	public override void _Ready() {
		this.Texture = TextureLoader.Load("city_screen.production_queue");

		// Load the font we'll use, at a fixed size that doesn't affect other
		// code using the same font.
		fontTheme.DefaultFont = FixedSizeFonts.Get("res://Fonts/NotoSans-Regular.ttf", 10);
	}

	// The city the menu lists options for, and what to do with the choice.
	private City city;
	private Action<IProducible> chooseProduction;
	// Set when the city changed while the menu was hidden, so the list is
	// brought up to date when it is next shown rather than on every change.
	private bool stale = false;

	// The options listed, in order, and whose they are.
	private readonly List<IProducible> shownOptions = new();
	private Player shownOwner;

	public void AddItems(GameData gameData, City city, Action<IProducible> chooseProduction) {
		this.city = city;
		this.chooseProduction = chooseProduction;
		if (Visible) {
			Refresh(gameData);
		} else {
			stale = true;
		}
	}

	public override void _Notification(int what) {
		if (what == NotificationVisibilityChanged && Visible && stale && city != null) {
			EngineStorage.ReadGameData(Refresh);
		}
	}

	private void Refresh(GameData gameData) {
		stale = false;
		if (city == null) {
			return;
		}

		if (tree == null) {
			CreateTree();
		}

		List<IProducible> options = new(city.ListProductionOptions(gameData));

		// The same options as before (e.g. after a citizen moved): just update
		// the build times, keeping the list where it was scrolled to.
		bool sameOptions = shownOwner == city.owner && options.Count == shownOptions.Count;
		for (int i = 0; sameOptions && i < options.Count; ++i) {
			sameOptions = ReferenceEquals(options[i], shownOptions[i]);
		}
		if (sameOptions) {
			foreach (var (item, option) in itemMapping) {
				item.SetText(1, $"{city.TurnsToProduce(option)} turns");
			}
			return;
		}

		// Remember the option at the top of the list, to scroll back to it.
		IProducible topOption = null;
		if (tree.GetScroll().Y > 0) {
			TreeItem top = tree.GetItemAtPosition(new Vector2(10, 10));
			if (top != null) {
				itemMapping.TryGetValue(top, out topOption);
			}
		}

		itemMapping.Clear();
		tree.Clear();
		shownOptions.Clear();
		shownOptions.AddRange(options);
		shownOwner = city.owner;

		TreeItem root = TradingTree.CreateTreeRoot(tree);

		foreach (IProducible option in options) {
			int buildTime = city.TurnsToProduce(option);

			TreeItem child = tree.CreateItem(root);
			string text = $"{option.name}";
			if (option is UnitPrototype proto) {
				string attackDesc = (proto.bombard > 0) ? $"{proto.attack}({proto.bombard})" : proto.attack.ToString();
				text += $" {attackDesc}.{proto.defense}.{proto.movement}";
			}
			child.SetText(0, text);
			child.SetText(1, $"{buildTime} turns");
			child.SetIcon(0, RightClickChooseProductionMenu.GetProducibleIcon(option, city.owner));
			child.SetCustomMinimumHeight(40);
			child.SetAutowrapMode(0, TextServer.AutowrapMode.WordSmart);
			itemMapping[child] = option;
			if (option == topOption) {
				TreeItem scrollTo = child;
				// Once the tree has laid out its new items.
				Callable.From(() => {
					if (IsInstanceValid(tree) && IsInstanceValid(scrollTo)) {
						tree.ScrollToItem(scrollTo);
					}
				}).CallDeferred();
			}
		}
	}

	private void CreateTree() {
		tree = new();

		// Set up the tree of items. We use a tree so we get a scroll bar and
		// other niceties.
		AddChild(tree);
		tree.Columns = 2;
		tree.Size = new Vector2(203, 360);
		tree.SetColumnExpand(0, true);
		tree.SetColumnExpand(1, false);
		tree.SetColumnCustomMinimumWidth(1, 50);
		tree.SetColumnTitleAlignment(1, HorizontalAlignment.Right);
		TradingTree.ConfigureTreeTheme(tree, fontTheme);

		// We only want clickable behavior, not selectable behavior.
		tree.ItemSelected += () => {
			TreeItem ti = tree.GetSelected();
			ti.Deselect(0);
			if (itemMapping.TryGetValue(ti, out IProducible option)) {
				chooseProduction?.Invoke(option);
			}
		};
	}
}
