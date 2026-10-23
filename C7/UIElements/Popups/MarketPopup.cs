using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using Godot;

// The Mercenary Market (see C7Engine.Staging): the units on offer, each with
// a box for the player's bid, and a countdown. The bids go in when time runs
// out; until then they can't add up to more than the player's gold. No one
// sees anyone else's bids.
public partial class MarketPopup : Popup {
	private const int Width = 660;
	private const int RowHeight = 44;
	private const int IconSize = 36;

	private readonly Player player;
	private readonly List<string> offers;
	private readonly int gold;
	private readonly Action<List<int>> bid;
	private readonly List<SpinBox> boxes = new();
	private Label total;
	private Label clock;
	private double secondsLeft;
	private bool sent;

	public MarketPopup(Player player, List<string> offers, int gold, int seconds, Action<List<int>> bid) {
		alignment = BoxContainer.AlignmentMode.Center;
		margins = new Margins(top: 100);
		this.player = player;
		this.offers = offers;
		this.gold = gold;
		this.bid = bid;
		secondsLeft = seconds;
	}

	private int Height => 150 + offers.Count * RowHeight;

	// The player may look around the map while bidding.
	public override bool LetsMapMove => true;

	// The market closes only when time runs out.
	public override bool OnEscape() {
		return false;
	}

	public override void _Ready() {
		base._Ready();
		AddTexture(Width, Height);
		AddBackground(Width, Height);
		AddHeader("Mercenary Market", 10);

		Label message = new() {
			Text = $"{player.civilization.noun}: mercenaries offer their swords. Bids are secret; the highest bid for each wins it.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(Width - 50, 0),
		};
		message.SetPosition(new Vector2(25, 50));
		AddChild(message);

		for (int i = 0; i < offers.Count; i++) {
			UnitPrototype unit = EngineStorage.gameData?.unitPrototypes.Find(p => p.name == offers[i]);
			HBoxContainer row = new() { CustomMinimumSize = new Vector2(Width - 50, RowHeight) };
			row.SetPosition(new Vector2(25, 95 + i * RowHeight));
			row.AddThemeConstantOverride("separation", 10);
			if (unit != null) {
				row.AddChild(new TextureRect {
					Texture = TextureLoader.Load("unit_icons", new ItemContext(unit, player), useCache: true),
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					CustomMinimumSize = new Vector2(IconSize, IconSize),
					SizeFlagsVertical = SizeFlags.ShrinkCenter,
				});
			}
			string stats = unit == null ? "" : $"  {unit.attack}/{unit.defense}/{unit.movement}";
			row.AddChild(new Label {
				Text = offers[i] + stats,
				VerticalAlignment = VerticalAlignment.Center,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			});
			row.AddChild(new Label { Text = "Bid:", VerticalAlignment = VerticalAlignment.Center });
			SpinBox box = new() {
				MinValue = 0,
				MaxValue = gold,
				Step = 1,
				Rounded = true,
				Suffix = "gold",
				CustomMinimumSize = new Vector2(130, 0),
				SizeFlagsVertical = SizeFlags.ShrinkCenter,
			};
			box.ValueChanged += _ => KeepWithinGold(box);
			boxes.Add(box);
			row.AddChild(box);
			AddChild(row);
		}

		total = new Label { CustomMinimumSize = new Vector2(Width - 50, 0) };
		total.SetPosition(new Vector2(25, Height - 50));
		AddChild(total);
		clock = new Label { HorizontalAlignment = HorizontalAlignment.Right, CustomMinimumSize = new Vector2(200, 0) };
		clock.SetPosition(new Vector2(Width - 225, Height - 50));
		AddChild(clock);
		ShowTotals();
	}

	// The bids together can't come to more than the player's gold: the one
	// just changed is held down to what's left.
	private void KeepWithinGold(SpinBox changed) {
		int others = boxes.Where(b => b != changed).Sum(b => (int)b.Value);
		if (others + (int)changed.Value > gold) {
			changed.SetValueNoSignal(Math.Max(0, gold - others));
		}
		ShowTotals();
	}

	private void ShowTotals() {
		int bids = boxes.Sum(b => (int)b.Value);
		total.Text = $"Bidding {bids} of {gold} gold";
		clock.Text = $"Closes in {Math.Max(0, (int)Math.Ceiling(secondsLeft))}s";
	}

	public override void _Process(double delta) {
		if (sent) {
			return;
		}
		secondsLeft -= delta;
		ShowTotals();
		if (secondsLeft <= 0) {
			Send();
		}
	}

	private void Send() {
		sent = true;
		List<int> amounts = boxes.Select(b => (int)b.Value).ToList();
		GetParent<PopupOverlay>().OnHidePopup();
		bid(amounts);
	}
}

// The Mercenary Market's results: who won each unit, and what the player
// paid for theirs. Nobody's bids are shown.
public partial class MarketResultsPopup : Popup {
	private const int Width = 560;
	private const int RowHeight = 40;
	private const int IconSize = 32;

	private readonly Player player;
	private readonly List<MarketResult> results;

	public MarketResultsPopup(Player player, List<MarketResult> results) {
		alignment = BoxContainer.AlignmentMode.Center;
		margins = new Margins(top: 100);
		this.player = player;
		this.results = results;
	}

	private int Height => 130 + results.Count * RowHeight;

	public override void _Ready() {
		base._Ready();
		AddTexture(Width, Height);
		AddBackground(Width, Height);
		AddHeader("Mercenary Market", 10);

		for (int i = 0; i < results.Count; i++) {
			MarketResult result = results[i];
			UnitPrototype unit = EngineStorage.gameData?.unitPrototypes.Find(p => p.name == result.unitType);
			HBoxContainer row = new() { CustomMinimumSize = new Vector2(Width - 50, RowHeight) };
			row.SetPosition(new Vector2(25, 55 + i * RowHeight));
			row.AddThemeConstantOverride("separation", 10);
			if (unit != null) {
				row.AddChild(new TextureRect {
					Texture = TextureLoader.Load("unit_icons", new ItemContext(unit, player), useCache: true),
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					CustomMinimumSize = new Vector2(IconSize, IconSize),
					SizeFlagsVertical = SizeFlags.ShrinkCenter,
				});
			}
			string outcome;
			if (result.winners.Count == 0) {
				outcome = "unsold";
			} else {
				string who = string.Join(", ", result.winners.Select(w => w == player ? "us" : w.civilization.noun));
				outcome = (result.winners.Count > 1 ? "tied, one each to " : "won by ") + who;
				if (result.winners.Contains(player)) {
					outcome += $" (we paid {result.price} gold)";
				}
			}
			row.AddChild(new Label {
				Text = $"{result.unitType}: {outcome}",
				VerticalAlignment = VerticalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			});
			AddChild(row);
		}

		AddConfirmButton(new Vector2(Width - 66, Height - 66), () => GetParent<PopupOverlay>().OnHidePopup());
	}
}
