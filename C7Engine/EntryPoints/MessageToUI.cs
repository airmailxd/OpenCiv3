namespace C7Engine {
	using C7GameData;
	using System;
	using System.Collections.Generic;
	using System.Linq;
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

		// Whether only those watching the game without playing should get
		// this, such as news the players only hear about through embassies.
		[JsonIgnore]
		public virtual bool IsForSpectatorsOnly => false;

		// Whether this is news a spectator may be shown: something a player
		// is told, rather than a question for them to answer or what only
		// redraws their screens. A LAN host shows it to those watching as a
		// player told it (see IsToldTo); a message for every player is news
		// to every spectator too.
		[JsonIgnore]
		public virtual bool IsNews => false;

		// Whether the player is told this: every player if it's for everyone,
		// otherwise the player it's delivered to, which without a recipient
		// is whoever is at the screen.
		public virtual bool IsToldTo(Player player) {
			if (IsForEveryone) {
				return true;
			}
			ID told = NetworkRecipient?.id ?? EngineStorage.uiControllerID;
			return told != null && player?.id == told;
		}

		// Whether a spectator watching as this player hears this news: just
		// what the player would be told in the game. The spectators' own
		// copies, such as a captured city's headline, are for those watching
		// every civ, unless they tell the player what it would hear of no
		// other way.
		public virtual bool IsToldAsCivTo(Player player) => IsToldTo(player);

		// What a spectator is shown of this news as a line in its list, or
		// null to show it as a player would see it.
		public virtual string SpectatorHeadline() => null;

		// The line a spectator watching as the player (null for none) is
		// shown of this news: the player's own news reads as they're told it,
		// rather than with whose it is.
		public string SpectatorHeadline(ID watchedAs) {
			if (SpectatorHeadline() is not string headline) {
				return null;
			}
			return watchedAs != null && recipient?.id == watchedAs && OwnHeadline() is string own ? own : headline;
		}

		// This news as the player it's for reads it; null if it reads the
		// same to everyone.
		protected virtual string OwnHeadline() => null;

		// The event this news tells of, when several players are told of it
		// each their own way, such as both sides of a captured city. A
		// spectator watching more than one civ hears only the first of its
		// copies (see LanHost.SpectatorNews); one watching a single civ hears
		// that civ's own. Null when there is only the one copy.
		[JsonIgnore]
		public NewsEvent newsEvent;

		// A player's own news, which reads as theirs ("We have..."), so a
		// spectator is told whose it is.
		protected string Whose(string message) {
			string text = OneLine(message);
			Civilization civ = recipient?.civilization;
			string who = civ?.noun ?? civ?.name;
			return who == null ? text : $"{who}: {text}";
		}

		protected static string OneLine(string message) => message?.Replace('\n', ' ').Trim() ?? "";

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

	// One event that several players are told of, which the copies told to
	// each share (see MessageToUI.newsEvent).
	public sealed class NewsEvent { }

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

		// Whether this is the copy for spectators, who hear of every war.
		public bool forSpectators;

		public MsgWarDeclaration(Player aggressor, Player opponent) {
			this.aggressor = aggressor;
			this.opponent = opponent;
		}

		[JsonIgnore]
		public override bool IsForSpectatorsOnly => forSpectators;

		// Spectators hear of a war from their own copy, which tells those
		// watching as a civ that would know of it.
		[JsonIgnore]
		public override bool IsNews => forSpectators;

		public override bool IsToldTo(Player player) {
			return forSpectators ? player != null && HearsOfWar(player, aggressor, opponent) : base.IsToldTo(player);
		}

		// Watching as a civ, a spectator hears of the war as its player
		// would, which the civ that declared it isn't told.
		public override bool IsToldAsCivTo(Player player) {
			return forSpectators && player != aggressor && IsToldTo(player);
		}

		public override string SpectatorHeadline() {
			return $"The {aggressor.civilization.noun} declared war on the {opponent.civilization.noun}";
		}

		// Tells the human players who would know of the war: the civ it was
		// declared on and those with an embassy with either side, like in
		// Civ3. Spectators hear of every war.
		public static void Announce(Player aggressor, Player opponent) {
			foreach (Player player in EngineStorage.gameData.players) {
				if (player.isHuman && !player.defeated && player != aggressor && HearsOfWar(player, aggressor, opponent)) {
					new MsgWarDeclaration(aggressor, opponent) { recipient = player }.send();
				}
			}
			new MsgWarDeclaration(aggressor, opponent) { forSpectators = true }.send();
		}

		public static bool HearsOfWar(Player player, Player aggressor, Player opponent) {
			return player == aggressor || player == opponent
				|| Espionage.HasEmbassy(player, aggressor) || Espionage.HasEmbassy(player, opponent);
		}
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

		// News to the city's new owner and the one it was taken from.
		[JsonIgnore]
		public override bool IsNews => true;

		public override bool IsToldTo(Player player) {
			return player != null && (player == city?.owner || player == previousOwner);
		}

		// The headline is for spectators watching every civ: one watching
		// either side hears that side's own news of it instead.
		public override bool IsToldAsCivTo(Player player) => false;

		public override string SpectatorHeadline() {
			return $"The {city.owner?.civilization.noun} have taken {city.name} from the {previousOwner?.civilization.noun}";
		}
	}

	public class MsgCivilizationDestroyed : MessageToUI {
		public Civilization civilization;

		public MsgCivilizationDestroyed(Civilization civilization) {
			this.civilization = civilization;
		}

		public override bool IsForEveryone => true;

		public override string SpectatorHeadline() => $"The {civilization.noun} have been destroyed";
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

		[JsonIgnore]
		public override bool IsNews => true;

		public override string SpectatorHeadline() => Whose(message);

		protected override string OwnHeadline() => OneLine(message);
	}

	public class MsgShowDomesticAdvisorPopup : MessageToUI {
		public string message;
		public MsgShowDomesticAdvisorPopup(Player recipient, string message) {
			this.recipient = recipient;
			this.message = message;
		}

		[JsonIgnore]
		public override bool IsNews => true;

		public override string SpectatorHeadline() => Whose(message);

		protected override string OwnHeadline() => OneLine(message);
	}

	// Tells a human player that another civ has completed a great wonder.
	// The wonder and city are named, so the news reads the same on a LAN
	// guest that can't see the city.
	public class MsgWonderCompleted : MessageToUI {
		public Player builder;
		public string wonder;
		public string city;

		// Whether this is the copy for spectators. Every civ is told of a
		// wonder: the others by this news, the builder by its city.
		[JsonIgnore]
		public bool forSpectators;

		public MsgWonderCompleted(Player recipient, Player builder, string wonder, string city) {
			this.recipient = recipient;
			this.builder = builder;
			this.wonder = wonder;
			this.city = city;
		}

		[JsonIgnore]
		public override bool IsForSpectatorsOnly => forSpectators;

		[JsonIgnore]
		public override bool IsNews => forSpectators;

		public override bool IsToldTo(Player player) => forSpectators ? player != null : base.IsToldTo(player);

		public override string SpectatorHeadline() => Announcement();

		public string Announcement() {
			Civilization civ = builder?.civilization;
			string civName = civ?.noun ?? civ?.name ?? builder?.ToString();
			return $"The {civName} have completed {wonder} in {city}!";
		}
	}

	// Asks a human player what to research next, having just discovered a
	// tech (or null if it isn't known which).
	public class MsgShowScienceSelection : MessageToUI {
		public Tech discovered;
		public MsgShowScienceSelection(Player recipient, Tech discovered) {
			this.recipient = recipient;
			this.discovered = discovered;
		}
	}

	// Tells a human player that a city has finished building something, and
	// what it builds next. The items are named, as they are in
	// MsgChooseProduction.
	public class MsgCityProductionCompleted : MessageToUI {
		public City city;
		public string completed;
		public string next;
		public MsgCityProductionCompleted(Player recipient, City city, string completed, string next) {
			this.recipient = recipient;
			this.city = city;
			this.completed = completed;
			this.next = next;
		}
	}

	// News from the science advisor, such as the space race.
	public class MsgShowScienceAdvisorPopup : MessageToUI {
		public enum Mood { Happy, Angry, Sad, Surprised }

		public string message;
		public Mood mood;
		public MsgShowScienceAdvisorPopup(Player recipient, string message, Mood mood) {
			this.recipient = recipient;
			this.message = message;
			this.mood = mood;
		}

		[JsonIgnore]
		public override bool IsNews => true;

		public override string SpectatorHeadline() => Whose(message);

		protected override string OwnHeadline() => OneLine(message);
	}

	public class MsgShowTemporaryPopup : MessageToUI {
		public string message;
		public Tile location;

		public MsgShowTemporaryPopup(string message, Tile location, Player recipient = null) {
			this.message = message;
			this.location = location;
			this.recipient = recipient;
		}

		// A spectator sees it where it happened, as the player does.
		[JsonIgnore]
		public override bool IsNews => true;
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

	// An AI tells a human to take their units out of its territory or face
	// war. The human answers with MsgRespondToTerritoryDemand.
	public class MsgShowTerritoryDemand : MessageToUI {
		public Player aiPlayer;
		public Player humanPlayer;
		public int unitCount;

		// The human already promised to leave once and came back.
		public bool repeatOffense;

		public MsgShowTerritoryDemand(Player aiPlayer, Player humanPlayer, int unitCount, bool repeatOffense) {
			this.aiPlayer = aiPlayer;
			this.humanPlayer = humanPlayer;
			this.unitCount = unitCount;
			this.repeatOffense = repeatOffense;
		}

		// Like a trade offer, this goes to the human's own machine on a LAN.
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

	// Tells a human whether the AI they told to leave their territory
	// withdrew its units, or refused and declared war.
	public class MsgWithdrawalDemandResult : MessageToUI {
		public Player opponent;
		public bool withdrew;

		public MsgWithdrawalDemandResult(Player recipient, Player opponent, bool withdrew) {
			this.recipient = recipient;
			this.opponent = opponent;
			this.withdrew = withdrew;
		}

		[JsonIgnore]
		public override bool IsNews => true;

		public override string SpectatorHeadline() {
			string whose = recipient?.civilization.adjective ?? "foreign";
			return withdrew
				? $"The {opponent.civilization.noun} withdrew their units from {whose} territory"
				: $"The {opponent.civilization.noun} refused to leave {whose} territory";
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

		[JsonIgnore]
		public override bool IsNews => true;

		public override string SpectatorHeadline() {
			return $"The {opponent.civilization.noun} {(accepted ? "accepted" : "refused")} a deal with the {recipient?.civilization.noun}";
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

	// Asks the player who just captured a city whether to keep or raze it.
	public class MsgDisplayRazeCityPopup : MessageToUI {
		public City city;
		public MsgDisplayRazeCityPopup(Player recipient, City city) {
			this.recipient = recipient;
			this.city = city;
		}
	}

	// Every human player has been defeated, so the game is over.
	public class MsgNoHumansRemain : MessageToUI {
		public override bool IsForEveryone => true;
	}

	// Asks a human player how they vote in the United Nations election,
	// offering the (up to three) candidates they have met; they answer with
	// MsgCastUnitedNationsVote.
	public class MsgShowUnitedNationsVote : MessageToUI {
		public List<Player> candidates;

		public MsgShowUnitedNationsVote(Player recipient, params Player[] candidates) {
			this.recipient = recipient;
			this.candidates = candidates.Where(c => c != null).ToList();
		}
	}

	// The outcome of a United Nations election. The winner is null if no
	// candidate won a majority.
	public class MsgUnitedNationsElectionResult : MessageToUI {
		public List<Player> candidates;
		// The votes for each candidate, one per civ, in the order of
		// candidates.
		public List<int> votes;
		public int abstentions;
		public Player winner;

		public MsgUnitedNationsElectionResult(List<Player> candidates, List<int> votes, int abstentions, Player winner) {
			this.candidates = candidates;
			this.votes = votes;
			this.abstentions = abstentions;
			this.winner = winner;
		}

		public override bool IsForEveryone => true;

		public override string SpectatorHeadline() {
			return winner == null
				? "No candidate won a majority in the United Nations election"
				: $"{winner.civilization.leader} of the {winner.civilization.noun} has been elected Secretary General";
		}
	}

	// The outcome of a diplomatic or espionage mission the player sent.
	public class MsgEspionageResult : MessageToUI {
		public EspionageMission mission;
		public bool performed;
		public bool success;
		public string message;
		// The capital an embassy was established in, which the player is
		// shown once.
		public City city;

		public MsgEspionageResult(Player recipient, EspionageMission mission, bool performed, bool success, string message, City city = null) {
			this.recipient = recipient;
			this.mission = mission;
			this.performed = performed;
			this.success = success;
			this.message = message;
			this.city = city;
		}

		[JsonIgnore]
		public override bool IsNews => true;

		public override string SpectatorHeadline() => Whose(message);

		protected override string OwnHeadline() => OneLine(message);
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
