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

	// Below the list: what's queued after the current item, and a button to
	// clear the queue.
	Label queueLabel;
	Button clearQueueButton;
	const int queuePanelHeight = 70;

	public ProductionMenu() { }

	public override void _Ready() {
		this.Texture = TextureLoader.Load("city_screen.production_queue");

		// Load the font we'll use, at a fixed size that doesn't affect other
		// code using the same font.
		fontTheme.DefaultFont = FixedSizeFonts.Get("res://Fonts/NotoSans-Regular.ttf", 10);
	}

	// The city the menu lists options for, and what to do with the choice:
	// a click changes what the city builds, a shift-click adds the item to
	// the city's production queue.
	private City city;
	private Action<IProducible> chooseProduction;
	private Action<IProducible> enqueueProduction;
	private Action clearQueue;
	// Set when the city changed while the menu was hidden, so the list is
	// brought up to date when it is next shown rather than on every change.
	private bool stale = false;

	// The options listed, in order, and whose they are.
	private readonly List<IProducible> shownOptions = new();
	private Player shownOwner;

	public void AddItems(GameData gameData, City city, Action<IProducible> chooseProduction,
			Action<IProducible> enqueueProduction, Action clearQueue) {
		this.city = city;
		this.chooseProduction = chooseProduction;
		this.enqueueProduction = enqueueProduction;
		this.clearQueue = clearQueue;
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
			CreateQueuePanel();
		}
		RefreshQueuePanel();

		List<IProducible> options = new(city.ListProductionOptions(gameData));

		// The same options as before (e.g. after a citizen moved): just update
		// the build times, keeping the list where it was scrolled to.
		bool sameOptions = shownOwner == city.owner && options.Count == shownOptions.Count;
		for (int i = 0; sameOptions && i < options.Count; ++i) {
			sameOptions = ReferenceEquals(options[i], shownOptions[i]);
		}
		if (sameOptions) {
			foreach (var (item, option) in itemMapping) {
				SetItemText(item, option);
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
			TreeItem child = tree.CreateItem(root);
			SetItemText(child, option);
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

	// Shows the option's name, build time and, before the player switches
	// to it, how many stored shields switching would waste (or why it
	// can't be chosen).
	private void SetItemText(TreeItem item, IProducible option) {
		string text = $"{option.name}";
		if (option is UnitPrototype proto) {
			string attackDesc = (proto.bombard > 0) ? $"{proto.attack}({proto.bombard})" : proto.attack.ToString();
			text += $" {attackDesc}.{proto.defense}.{proto.movement}";
		}
		string warning = city.ProductionChangeWarning(option);
		if (warning != null) {
			text += $"\n({warning})";
		}
		item.SetText(0, text);
		item.SetTooltipText(0, warning ?? "");
		item.SetText(1, $"{city.TurnsToProduce(option)} turns");
		bool blocked = !city.CanChangeProduction(option);
		item.SetSelectable(0, !blocked);
		item.SetSelectable(1, !blocked);
		if (warning != null) {
			item.SetCustomColor(0, blocked ? Colors.DimGray : Colors.DarkRed);
		} else {
			item.ClearCustomColor(0);
		}
	}

	private void CreateTree() {
		tree = new();

		// Set up the tree of items. We use a tree so we get a scroll bar and
		// other niceties.
		AddChild(tree);
		tree.Columns = 2;
		tree.Size = new Vector2(203, 360 - queuePanelHeight);
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
				if (Input.IsKeyPressed(Key.Shift)) {
					enqueueProduction?.Invoke(option);
				} else {
					chooseProduction?.Invoke(option);
				}
			}
		};
	}

	private void CreateQueuePanel() {
		int top = 360 - queuePanelHeight;
		queueLabel = new Label() {
			Position = new Vector2(8, top + 4),
			Size = new Vector2(187, 36),
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
			ClipText = true,
			Theme = fontTheme,
		};
		queueLabel.AddThemeColorOverride("font_color", Colors.Black);
		AddChild(queueLabel);

		clearQueueButton = new Button() {
			Text = "Clear Queue",
			Position = new Vector2(8, top + 42),
			Size = new Vector2(187, 22),
			Theme = fontTheme,
		};
		clearQueueButton.Pressed += () => clearQueue?.Invoke();
		AddChild(clearQueueButton);
	}

	private void RefreshQueuePanel() {
		List<string> queued = city.productionQueue.ConvertAll(p => p.name);
		queueLabel.Text = queued.Count == 0
			? "Shift+click to queue production."
			: $"Then: {string.Join(", ", queued)}";
		queueLabel.TooltipText = queued.Count == 0 ? "" : string.Join("\n", queued);
		// Hurried production can't be changed until the turn ends.
		if (city.hurriedThisTurn) {
			queueLabel.Text = $"Hurried this turn, so production can't change. {queueLabel.Text}";
		}
		queueLabel.MouseFilter = MouseFilterEnum.Pass;
		clearQueueButton.Disabled = queued.Count == 0;
	}
}
