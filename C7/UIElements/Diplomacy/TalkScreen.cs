using C7Engine;
using C7GameData;
using Godot;
using System;
using System.Collections.Generic;
using ConvertCiv3Media;

public partial class TalkScreen : TextureRect {
	private ID humanPlayerId;
	private ID opponentPlayerId;

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
		offset += gap;

		EngineStorage.ReadGameData((GameData gD) => {
			if (!gD.AreInLockedPeace(gD.GetPlayer(humanPlayerId), gD.GetPlayer(opponentPlayerId))) {
				Button declareWar = new();
				declareWar.Text = "That's it! Prepare for WAR!";
				declareWar.SetPosition(new Vector2(512 - 205, offset));
				declareWar.Theme = fontTheme;
				declareWar.Pressed += DeclareWar;
				AddChild(declareWar);
			} else {
				Button tradeWorldMaps = new();
				tradeWorldMaps.Text = "Care to trade World Maps?";
				tradeWorldMaps.SetPosition(new Vector2(512 - 205, offset));
				tradeWorldMaps.Theme = fontTheme;
				tradeWorldMaps.Pressed += TradeWorldMaps;
				AddChild(tradeWorldMaps);
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
