using C7Engine;
using C7Engine.AI;
using C7GameData;
using Godot;
using System;
using System.Collections.Generic;
using ConvertCiv3Media;

public partial class TalkScreen : TextureRect {
	private ID humanPlayerId;
	private ID opponentPlayerId;

	// The things we can say, which go once the AI has answered a demand.
	private readonly List<Button> choices = new();

	Theme fontTheme = new();
	FontFile font;

	public TalkScreen(ID humanPlayer, ID opponentPlayer) {
		this.humanPlayerId = humanPlayer;
		this.opponentPlayerId = opponentPlayer;
	}

	public override void _Ready() {
		this.CreateUI();
	}

	private void CreateUI() {
		// Load the font we'll use, at a fixed size that doesn't affect other
		// code using the same font.
		font = FixedSizeFonts.Get("res://Fonts/NotoSans-Regular.ttf", 14);
		fontTheme.DefaultFont = font;

		this.Texture = TextureLoader.Load("diplomacy.offer");

		string civNameText = "";
		string leaderName = "";
		EngineStorage.ReadGameData((GameData gD) => {
			Player opponentPlayer = gD.players.Find(x => x.id == opponentPlayerId);
			GetParent<Diplomacy>().AddLeaderHeadAndLabel(this, opponentPlayer, fontTheme);
			civNameText = $"{opponentPlayer.civilization.name} (Cautious)";
			leaderName = opponentPlayer.civilization.leader;
		});

		int offset = 500;
		int gap = 20;

		Button proposeDeal = new();
		proposeDeal.Text = "We would like to propose a deal...";
		proposeDeal.SetPosition(new Vector2(512 - 205, offset));
		proposeDeal.Theme = fontTheme;
		proposeDeal.Pressed += () => {
			GetParent<Diplomacy>().ShowDealScreenForPlayer(humanPlayerId, opponentPlayerId);
		};
		AddChild(proposeDeal);
		choices.Add(proposeDeal);
		offset += gap;

		// As in Civ3, an AI with units in our territory can be told to take
		// them out or face war.
		EngineStorage.ReadGameData((GameData gD) => {
			Player human = gD.GetPlayer(humanPlayerId);
			Player opponent = gD.GetPlayer(opponentPlayerId);
			if (!opponent.isHuman && TerritoryDemands.UnitsToWithdraw(human, opponent, gD).Count > 0) {
				Button demandWithdrawal = new();
				demandWithdrawal.Text = "Remove your troops from our territory, or prepare for war!";
				demandWithdrawal.SetPosition(new Vector2(512 - 205, offset));
				demandWithdrawal.Theme = fontTheme;
				demandWithdrawal.Pressed += () => { new MsgDemandWithdrawal(opponent).send(); };
				AddChild(demandWithdrawal);
				choices.Add(demandWithdrawal);
				offset += gap;
			}
		});

		EngineStorage.ReadGameData((GameData gD) => {
			if (!gD.AreInLockedPeace(gD.GetPlayer(humanPlayerId), gD.GetPlayer(opponentPlayerId))) {
				Button declareWar = new();
				declareWar.Text = "That's it! Prepare for WAR!";
				declareWar.SetPosition(new Vector2(512 - 205, offset));
				declareWar.Theme = fontTheme;
				declareWar.Pressed += DeclareWar;
				AddChild(declareWar);
				choices.Add(declareWar);
			} else {
				Button tradeWorldMaps = new();
				tradeWorldMaps.Text = "Care to trade World Maps?";
				tradeWorldMaps.SetPosition(new Vector2(512 - 205, offset));
				tradeWorldMaps.Theme = fontTheme;
				tradeWorldMaps.Pressed += TradeWorldMaps;
				AddChild(tradeWorldMaps);
				choices.Add(tradeWorldMaps);
			}
			offset += gap;
		});

		Button goodbye = new();
		goodbye.Text = $"That's it. Goodbye, {leaderName}";
		goodbye.SetPosition(new Vector2(512 - 205, offset));
		goodbye.Theme = fontTheme;
		goodbye.Pressed += () => { GetParent<Diplomacy>().Hide(); };
		AddChild(goodbye);
	}

	// The AI answers our demand that it leave our territory. Having
	// answered, it has nothing more to say, so all that's left is goodbye.
	public void OnWithdrawalDemandResult(ID opponent, bool withdrew) {
		if (opponent != opponentPlayerId) {
			return;
		}
		foreach (Button choice in choices) {
			RemoveChild(choice);
			choice.QueueFree();
		}
		choices.Clear();

		string adjective = "";
		EngineStorage.ReadGameData((GameData gD) => {
			adjective = gD.GetPlayer(opponentPlayerId).civilization.adjective;
		});
		Label reply = new();
		reply.Theme = fontTheme;
		reply.SetPosition(new Vector2(0, 400));
		AddChild(reply);
		reply.SetTextAndCenterLabel(withdrew
			? "Very well. We will withdraw our forces from your lands."
			: $"You dare threaten us? The {adjective} forces go where they please. Prepare for war!");
	}

	private void DeclareWar() {
		Diplomacy diplomacy = GetParent<Diplomacy>();
		EngineStorage.ReadGameData((GameData gD) => {
			Player opponentPlayer = gD.players.Find(x => x.id == opponentPlayerId);
			diplomacy.ShowScreenPopup(new WarConfirmation(opponentPlayer,
				() => {
					// The talk screen may have been replaced while the
					// popup was up.
					if (!IsInstanceValid(this) || !IsInsideTree()) {
						return;
					}
					new MsgDeclareWar(opponentPlayer).send();
					diplomacy.Hide();
				}));
		});
	}

	private void TradeWorldMaps() {
		var weGive = new TradeOffer();
		var theyGive = new TradeOffer();
		// TODO: make maps tradeable, and (pre)add them to this deal/ call the method that adds them
		GetParent<Diplomacy>().ShowDealScreenForPlayer(humanPlayerId, opponentPlayerId, weGive, theyGive);
	}
}
