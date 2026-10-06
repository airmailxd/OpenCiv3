using C7Engine;
using C7GameData;
using Godot;
using System.Collections.Generic;
using static C7GameData.PlayerRelationship;

// At a high level the deal screen has 4 parts; 2 "TradingTree"s per player, and
// 2 "TradeOfferUi"s per player.
//
// The trading tree is the far left and right parts of the screen, where sections
// can be collapsed (like technology, gold, etc). The trade offer ui is the part
// in the middle, where we show what is actually being offered.
//
// The basic idea is to have all the possible items present in both components,
// and then use the shared TradeOffer object to determine what is visible. So
// when a technology is clicked in the trading tree, it is added to the
// TradeOffer object, and then we refresh the UIs of the trading tree and the
// trade offer ui to swap the visibility.
//
// Things that aren't yet implemented:
//  - Gifts
//  - Demands
//  - What do you need for ...
//  - What will you give me for ...
//  - Per-turn deals
//  - Interesting messages from the opponent player
public partial class DealScreen : TextureRect {
	private ID humanPlayerId;
	private ID opponentPlayerId;

	Theme fontTheme = new();
	FontFile font;

	TradingTree opponentTree;
	TradingTree humanTree;

	TradeOfferUi opponentOfferUi;
	TradeOfferUi humanOfferUi;

	Button acceptDeal;
	Label opponentResponse;

	// The "Active Deals" button swaps the negotiation for a list of the
	// deals already in force, and back.
	Button activeDealsButton;
	Control activeDealsPanel;
	readonly List<CanvasItem> negotiationControls = new();

	TradeOffer opponentOffer = new();
	TradeOffer humanOffer = new();

	public DealScreen(ID humanPlayer, ID opponentPlayer, TradeOffer humanGives, TradeOffer humanWants) {
		this.humanPlayerId = humanPlayer;
		this.opponentPlayerId = opponentPlayer;
		this.humanOffer = humanGives;
		this.opponentOffer = humanWants;
	}

	public override void _Ready() {
		this.CreateUI();
	}

	private void CreateUI() {
		// Load the font we'll use, at a fixed size that doesn't affect other
		// code using the same font.
		font = FixedSizeFonts.Get("res://Fonts/NotoSans-Regular.ttf", 13);
		fontTheme.DefaultFont = font;

		Theme blueFontTheme = new();
		blueFontTheme.DefaultFont = font;
		blueFontTheme.SetColor("font_color", "Label", Colors.Blue);

		this.Texture = TextureLoader.Load("diplomacy.deal");

		EngineStorage.ReadGameData((GameData gD) => {
			Player opponentPlayer = gD.players.Find(x => x.id == opponentPlayerId);
			Player humanPlayer = gD.players.Find(x => x.id == humanPlayerId);
			bool playersAtWar = AtWar(humanPlayer, opponentPlayer);
			GetParent<Diplomacy>().AddLeaderHeadAndLabel(this, opponentPlayer, fontTheme);

			// Figure out which technologies can be traded by each player, if any.
			List<Tech> techsOpponentCanTrade = gD.techs.FindAll(x => {
				return opponentPlayer.knownTechs.Contains(x.id) && !humanPlayer.knownTechs.Contains(x.id);
			});
			List<Tech> techsHumanCanTrade = gD.techs.FindAll(x => {
				return humanPlayer.knownTechs.Contains(x.id) && !opponentPlayer.knownTechs.Contains(x.id);
			});

			// Left hand side UI components.
			opponentTree = new TradingTree(fontTheme, opponentPlayer.gold, techsOpponentCanTrade, opponentOffer,
				playersAtWar);
			AddChild(opponentTree);
			opponentTree.Position = new Vector2(45, 220);

			Label weWant = new();
			weWant.Text = "We Want";
			weWant.SetPosition(new Vector2(320, 440));
			weWant.Theme = blueFontTheme;
			AddChild(weWant);

			opponentOfferUi = new(fontTheme, techsOpponentCanTrade, opponentPlayer.gold, opponentOffer, playersAtWar,
				HorizontalAlignment.Left);
			AddChild(opponentOfferUi);
			opponentOfferUi.Position = new Vector2(314, 453);

			// Right hand side UI components.
			humanTree = new TradingTree(fontTheme, humanPlayer.gold, techsHumanCanTrade, humanOffer, playersAtWar);
			AddChild(humanTree);
			humanTree.Position = new Vector2(789, 220);

			Label weOffer = new();
			weOffer.Text = "We Offer";
			weOffer.SetPosition(new Vector2(660, 440));
			weOffer.Theme = blueFontTheme;
			AddChild(weOffer);

			humanOfferUi = new(fontTheme, techsHumanCanTrade, humanPlayer.gold, humanOffer, playersAtWar,
				HorizontalAlignment.Right);
			AddChild(humanOfferUi);
			humanOfferUi.Position = new Vector2(527, 453);

			var syncUis = () => {
				opponentTree.RefreshUiForOffer();
				humanTree.RefreshUiForOffer();
				opponentOfferUi.RefreshUiForOffer();
				humanOfferUi.RefreshUiForOffer();
			};

			// Link up the tree components appropriately.
			opponentTree.HandleClicks(humanTree, () => {
				syncUis();
				UpdateText();
			});
			humanTree.HandleClicks(opponentTree, () => {
				syncUis();
				UpdateText();
			});

			humanOfferUi.HandleClicks(opponentOfferUi, () => {
				syncUis();
				UpdateText();
			});
			opponentOfferUi.HandleClicks(humanOfferUi, () => {
				syncUis();
				UpdateText();
			});

			// Add the buttons at the bottom.
			acceptDeal = new();
			acceptDeal.Text = "\"Will you accept this deal?\"";
			acceptDeal.SetPosition(new Vector2(512 - 205, 630));
			acceptDeal.Theme = fontTheme;
			acceptDeal.Pressed += AttemptDeal;
			AddChild(acceptDeal);

			Button goodbye = new();
			goodbye.Text = "\"Never mind...\"";
			goodbye.SetPosition(new Vector2(512 - 205, 650));
			goodbye.Theme = fontTheme;
			goodbye.Pressed += () => { GetParent<Diplomacy>().Hide(); };
			AddChild(goodbye);

			activeDealsButton = new();
			activeDealsButton.Text = "Active Deals";
			activeDealsButton.SetPosition(new Vector2(512 - 205, 670));
			activeDealsButton.Theme = fontTheme;
			activeDealsButton.Pressed += ToggleActiveDeals;
			AddChild(activeDealsButton);

			negotiationControls.AddRange(new CanvasItem[] {
				opponentTree, humanTree, weWant, weOffer, opponentOfferUi, humanOfferUi, acceptDeal,
			});
			activeDealsPanel = CreateActiveDealsPanel(gD, humanPlayer, opponentPlayer, blueFontTheme);
			activeDealsPanel.Visible = false;
			AddChild(activeDealsPanel);

			// And the text from the opponent, if they reject a deal.
			opponentResponse = new();
			opponentResponse.Text = "\"Let's get down to business...\"";
			opponentResponse.SetPosition(new Vector2(512 - 205, 345));
			opponentResponse.Theme = fontTheme;
			AddChild(opponentResponse);
		});
	}

	private void ToggleActiveDeals() {
		bool showDeals = !activeDealsPanel.Visible;
		activeDealsPanel.Visible = showDeals;
		foreach (CanvasItem control in negotiationControls) {
			control.Visible = !showDeals;
		}
		activeDealsButton.Text = showDeals ? "Back to the negotiation" : "Active Deals";
	}

	// Lists the deals in force with the opponent, over the middle of the
	// screen where offers are shown.
	private Control CreateActiveDealsPanel(GameData gD, Player humanPlayer, Player opponentPlayer, Theme headerTheme) {
		Theme blackFontTheme = new();
		blackFontTheme.DefaultFont = font;
		blackFontTheme.SetColor("font_color", "Label", Colors.Black);

		ScrollContainer scroll = new();
		scroll.SetPosition(new Vector2(314, 440));
		scroll.Size = new Vector2(400, 200);
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;

		VBoxContainer list = new();
		list.AddThemeConstantOverride("separation", 1);
		list.CustomMinimumSize = new Vector2(400, 0);
		scroll.AddChild(list);

		void AddLine(Container parent, string text, Theme theme, bool indent = false,
				HorizontalAlignment alignment = HorizontalAlignment.Left) {
			Label label = new();
			// Indent toward the middle, on whichever side the text is.
			string padding = indent ? "    " : "";
			label.Text = alignment == HorizontalAlignment.Right ? text + padding : padding + text;
			label.HorizontalAlignment = alignment;
			label.Theme = theme;
			parent.AddChild(label);
		}

		void AddSection(Container parent, string header, List<string> lines,
				HorizontalAlignment alignment = HorizontalAlignment.Left) {
			if (lines.Count == 0) {
				return;
			}
			AddLine(parent, header, headerTheme, alignment: alignment);
			foreach (string line in lines) {
				AddLine(parent, line, blackFontTheme, true, alignment);
			}
		}

		AddLine(list, $"Active deals with the {opponentPlayer.civilization.noun}", headerTheme);
		if (AtWar(humanPlayer, opponentPlayer)) {
			AddLine(list, "We are at war.", blackFontTheme, true);
			return scroll;
		}

		ActiveDeals deals = ActiveDeals.Between(gD, humanPlayer, opponentPlayer);
		if (deals.IsEmpty) {
			AddLine(list, "We have no active deals.", blackFontTheme, true);
			return scroll;
		}
		AddSection(list, "Agreements", deals.agreements);

		// What each side gives goes on its side of the table, as the offers
		// do: theirs on the left, ours on the right.
		HBoxContainer columns = new();
		columns.AddThemeConstantOverride("separation", 0);
		list.AddChild(columns);
		VBoxContainer theyGive = new();
		VBoxContainer weGive = new();
		foreach (VBoxContainer column in new[] { theyGive, weGive }) {
			column.AddThemeConstantOverride("separation", 1);
			column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			columns.AddChild(column);
		}
		AddSection(theyGive, "They Give", deals.theyGive);
		AddSection(weGive, "We Give", deals.weGive, HorizontalAlignment.Right);
		return scroll;
	}

	private void AttemptDeal() {
		EngineStorage.ReadGameData((GameData gD) => {
			Player opponentPlayer = gD.players.Find(x => x.id == opponentPlayerId);
			Player humanPlayer = gD.players.Find(x => x.id == humanPlayerId);

			// A human on another machine is asked over the network, and the
			// engine tells us their answer.
			if (LanSession.IsRemotePlayer(opponentPlayer)) {
				opponentResponse.Text = $"\"{opponentPlayer.civilization.leader} is considering the offer...\"";
				new MsgProposeDeal(opponentPlayer, humanOffer, opponentOffer).send();
				return;
			}

			// Another human player (in a hotseat game) decides for themselves
			// rather than having the AI decide for them.
			if (opponentPlayer.isHuman) {
				AskHumanOpponentToAccept(humanPlayer, opponentPlayer);
				return;
			}

			// The AI decides; if it accepts, the engine's answer takes us back
			// to the previous screen.
			new MsgProposeDeal(opponentPlayer, humanOffer, opponentOffer).send();
		});
	}

	private void AskHumanOpponentToAccept(Player humanPlayer, Player opponentPlayer) {
		string message =
			$"{opponentPlayer.civilization.leader}, please take the screen.\n" +
			DescribeDeal(humanPlayer, humanOffer, opponentOffer);

		opponentResponse.Text = $"\"{opponentPlayer.civilization.leader} is considering the offer...\"";
		GetParent<Diplomacy>().ShowScreenPopup(
			new ConfirmationPopup(message, "We accept.", "We refuse.",
				yesAction: () => {
					// The deal screen may have been replaced while the
					// popup was up.
					if (!IsInstanceValid(this) || !IsInsideTree()) {
						return;
					}
					new MsgProposeDeal(opponentPlayer, humanOffer, opponentOffer) { opponentAgreed = true }.send();
				},
				noAction: () => {
					if (!IsInstanceValid(this) || !IsInsideTree()) {
						return;
					}
					opponentResponse.Text = $"\"{opponentPlayer.civilization.leader} refused the offer.\"";
				}));
	}

	public static string DescribeDeal(Player proposer, TradeOffer proposerGives, TradeOffer proposerWants) {
		static string Describe(TradeOffer offer) {
			string description = offer.ToString();
			return description == "" ? "nothing" : description.Replace(",", ", ");
		}

		return $"The {proposer.civilization.noun} offer: {Describe(proposerGives)}\n" +
			$"In return they want: {Describe(proposerWants)}";
	}

	// The engine's answer to a deal we proposed.
	public void OnDealResult(ID opponent, bool accepted) {
		if (opponent != opponentPlayerId) {
			return;
		}
		if (accepted) {
			GetParent<Diplomacy>().ShowTalkScreenForPlayer(humanPlayerId, opponentPlayerId);
		} else {
			EngineStorage.ReadGameData((GameData gD) => {
				Player opponentPlayer = gD.players.Find(x => x.id == opponentPlayerId);
				if (opponentPlayer.isHuman) {
					opponentResponse.Text = $"\"{opponentPlayer.civilization.leader} refused the offer.\"";
				}
			});
		}
	}

	private void UpdateText() {
		EngineStorage.ReadGameData((GameData gD) => {
			Player opponentPlayer = gD.players.Find(x => x.id == opponentPlayerId);
			Player humanPlayer = gD.players.Find(x => x.id == humanPlayerId);

			// The gold valuation below is the AI's view of the deal.
			if (opponentPlayer.isHuman) {
				opponentResponse.Text = "\"Let's get down to business...\"";
				return;
			}

			int theirGoldValue = opponentOffer.GoldEquivalentFor(gD, opponentPlayer);
			int ourGoldValue = humanOffer.GoldEquivalentFor(gD, opponentPlayer);
			opponentResponse.Text =
				$"\"I value my offer at {theirGoldValue} gold and I value your offer at {ourGoldValue} gold\"";
		});
	}
}
