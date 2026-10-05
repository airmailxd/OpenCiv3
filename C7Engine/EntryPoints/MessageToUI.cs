namespace C7Engine {
	using C7GameData;
	using System;
	using System.Text.Json.Serialization;

	public interface IMessageToUI {
		public void send();
	}

	public class MessageToUI : IMessageToUI {
		// The player this message is meant for, or null if it is for whoever
		// is at the screen. In a hotseat game the UI holds messages for a human
		// player who isn't at the screen until their next turn.
		public Player recipient;

		// Whether every player should hear about this, such as a war breaking
		// out. A LAN host sends these to every machine.
		[JsonIgnore]
		public virtual bool IsForEveryone => false;

		// The player whose machine a LAN host should deliver this to.
		[JsonIgnore]
		public virtual Player NetworkRecipient => recipient;

		public void send() {
			// What happens while the engine processes a player's message is
			// news for that player.
			if (recipient == null && !IsForEveryone && EngineStorage.processingSenderID != null) {
				recipient = EngineStorage.gameData.GetPlayer(EngineStorage.processingSenderID);
			}
			EngineStorage.SendToUI(this);
		}
	}

	public class AnimationMessage : IMessageToUI {
		internal Guid animationId = Guid.NewGuid();

		public void send() {
			EngineStorage.animationMessages.Enqueue(this);
		}

		public void markCompleted() {
			if (EngineStorage.pendingAnimations.TryGetValue(animationId, out var tcs)) {
				EngineStorage.pendingAnimations.Remove(animationId);
				tcs.TrySetResult(true);
			}
		}
	}

	public class MsgStartUnitAnimation : AnimationMessage {
		public ID unitID;
		public MapUnit.AnimatedAction action;
		public AnimationEnding ending;

		public MsgStartUnitAnimation(MapUnit unit, MapUnit.AnimatedAction action, AnimationEnding ending) {
			this.unitID = unit.id;
			this.action = action;
			this.ending = ending;
		}
	}

	public class MsgStartEffectAnimation : AnimationMessage {
		public int tileIndex;
		public AnimatedEffect effect;
		public AnimationEnding ending;

		public MsgStartEffectAnimation(Tile tile, AnimatedEffect effect, AnimationEnding ending) {
			this.tileIndex = EngineStorage.gameData.map.tileCoordsToIndex(tile.XCoordinate, tile.YCoordinate);
			this.effect = effect;
			this.ending = ending;
		}
	}

	// The engine has handed the turn to a human player.
	public class MsgStartTurn : MessageToUI {
		public Player player;

		public MsgStartTurn(Player player) {
			this.player = player;
		}

		// On a LAN, the other players learn whose turn they are waiting on.
		public override bool IsForEveryone => true;
	}

	public class MsgShowScienceAdvisor : MessageToUI { }

	public class MsgUpdateUiAfterDomesticChange : MessageToUI { }

	public class MsgWarDeclaration : MessageToUI {
		public Player aggressor;
		public Player opponent;

		public MsgWarDeclaration(Player aggressor, Player opponent) {
			this.aggressor = aggressor;
			this.opponent = opponent;
		}

		public override bool IsForEveryone => true;
	}

	public class MsgCityDestroyed : MessageToUI {
		public City city;

		public MsgCityDestroyed(City city) {
			this.city = city;
		}

		public override bool IsForEveryone => true;
	}

	public class MsgCityCaptured : MessageToUI {
		public City city;
		public Player previousOwner;

		public MsgCityCaptured(City city, Player previousOwner) {
			this.city = city;
			this.previousOwner = previousOwner;
		}
	}

	public class MsgCivilizationDestroyed : MessageToUI {
		public Civilization civilization;

		public MsgCivilizationDestroyed(Civilization civilization) {
			this.civilization = civilization;
		}

		public override bool IsForEveryone => true;
	}

	public class MsgCityCreated : MessageToUI {
		public City city;

		public MsgCityCreated(City city) {
			this.city = city;
		}
	}

	public class MsgDisplayHurryProductionPopup : MessageToUI {
		public City city;
		public City.HurryProductionDetails details;

		public MsgDisplayHurryProductionPopup(City city, City.HurryProductionDetails details) {
			this.city = city;
			this.details = details;
		}
	}

	public class MsgDisplayStopWorkerActionPopup : MessageToUI {
		public MapUnit worker;
		public Terraform workerJob;
		public float turnsLeft;

		public MsgDisplayStopWorkerActionPopup(MapUnit worker, Terraform workerJob, float turnsLeft) {
			this.worker = worker;
			this.workerJob = workerJob;
			this.turnsLeft = turnsLeft;
		}
	}

	public class MsgShowCityScreen : MessageToUI {
		public City city;

		public MsgShowCityScreen(City city) {
			this.city = city;
		}
	}

	public class MsgShowMilitaryAdvisorPopup : MessageToUI {
		public string message;
		public bool happy;
		public MsgShowMilitaryAdvisorPopup(Player recipient, string message, bool happy) {
			this.recipient = recipient;
			this.message = message;
			this.happy = happy;
		}
	}

	public class MsgShowDomesticAdvisorPopup : MessageToUI {
		public string message;
		public MsgShowDomesticAdvisorPopup(Player recipient, string message) {
			this.recipient = recipient;
			this.message = message;
		}
	}

	public class MsgShowTemporaryPopup : MessageToUI {
		public string message;
		public Tile location;

		public MsgShowTemporaryPopup(string message, Tile location, Player recipient = null) {
			this.message = message;
			this.location = location;
			this.recipient = recipient;
		}
	}

	public class MsgShowTradeOffer : MessageToUI {
		public Player aiPlayer;
		public Player humanPlayer;
		public TradeOffer aiWant;
		public TradeOffer aiGive;

		public MsgShowTradeOffer(Player aiPlayer, Player humanPlayer, TradeOffer aiWant, TradeOffer aiGive) {
			this.aiPlayer = aiPlayer;
			this.humanPlayer = humanPlayer;
			this.aiWant = aiWant;
			this.aiGive = aiGive;
		}

		// Hotseat hands the screen to the human with a handoff, so this has no
		// recipient, but on a LAN it belongs on the human's own machine.
		public override Player NetworkRecipient => humanPlayer;
	}

	// Another human on a LAN proposes a deal, which the recipient accepts or
	// refuses with MsgRespondToDeal.
	public class MsgShowDealProposal : MessageToUI {
		public Player proposer;
		public Player opponent;
		public TradeOffer proposerGives;
		public TradeOffer proposerWants;

		public MsgShowDealProposal(Player proposer, Player opponent, TradeOffer proposerGives, TradeOffer proposerWants) {
			this.proposer = proposer;
			this.opponent = opponent;
			this.proposerGives = proposerGives;
			this.proposerWants = proposerWants;
		}
	}

	// Tells the player who proposed a deal whether it was accepted.
	public class MsgDealResult : MessageToUI {
		public Player opponent;
		public bool accepted;

		public MsgDealResult(Player recipient, Player opponent, bool accepted) {
			this.recipient = recipient;
			this.opponent = opponent;
			this.accepted = accepted;
		}
	}

	// A city's citizens or production changed, so a city screen showing it
	// should redraw.
	public class MsgCityChanged : MessageToUI {
		public City city;

		public MsgCityChanged(City city) {
			this.city = city;
		}
	}

	public class MsgUnitMoved : MessageToUI {
		public MapUnit Unit;
		public MsgUnitMoved(MapUnit unit) {
			this.Unit = unit;
		}
	}

	public class MsgTransportUnloaded : MessageToUI {
		public MapUnit Unit;
		public MsgTransportUnloaded(MapUnit unit) {
			this.Unit = unit;
		}
	}

	public class MsgDisplayAbandonCityPopup : MessageToUI {
		public City city;
		public MsgDisplayAbandonCityPopup(City city) {
			this.city = city;
		}
	}

	// Every human player has been defeated, so the game is over.
	public class MsgNoHumansRemain : MessageToUI {
		public override bool IsForEveryone => true;
	}

	public class MsgVictory : MessageToUI {
		public Player winner;
		public IVictory victory;

		public MsgVictory(Player winner, IVictory victory) {
			this.winner = winner;
			this.victory = victory;
		}

		public override bool IsForEveryone => true;
	}
}
