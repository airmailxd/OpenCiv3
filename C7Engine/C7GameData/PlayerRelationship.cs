using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using Serilog;

namespace C7GameData;

// A class holding all the state of the relationship between two civs.
// If a relationship between 2 active civs doesn't exist it means that they haven't met yet.
// If a relationship exists but doesn't have any multi-turn deals on either side,
// it means that these 2 civs are at war, because Peace itself is a multi-turn deal.
// A civ can't (and shouldn't) have a relationship with themselves, barbarians or defeated players.
//
// Another important detail is that, a PlayerRelationship is a two part relationship,
// or a two-way relationship if it's easier to think about it like that.
// Therefore, a multiturn deal exists in both player's relationship info.
// If a deal is removed from player's 1 multiTurnDeals list, it still very much exists in player's 2 list,
// unless explicitly removed.
public class PlayerRelationship {
	private static ILogger log = Log.ForContext<PlayerRelationship>();

	// p1.playerRelationships[p2].warDeclarationCount is the number of times
	// p2 declared war on p1.
	// TODO: contribute towards reputation
	public int warDeclarationCount = 0;

	// p1.playerRelationships[p2].warDeclarationWithRoPActiveCount is the number of times
	// p2 declared war on p1 while having an active RoP.
	// TODO: contribute towards reputation
	public int warDeclarationWithRoPActiveCount = 0;

	// p1.playerRelationships[p2].nuclearAtrocityCount is the number of
	// nuclear attacks p2 has made (on anyone) since p1 knew them. Civ3
	// treats using nuclear weapons as an atrocity every civ resents.
	// TODO: contribute towards attitude
	public int nuclearAtrocityCount = 0;

	// true if a war declaration happened with units inside the player's
	// borders.
	public bool wasSneakAttacked = false;

	// If at war, refuse contact with the relevant player until this turn
	// has been reached.
	public int refuseContactUntilTurn = -1;

	public List<MultiTurnDeal> multiTurnDeals = new List<MultiTurnDeal>();

	public bool declaredWarWithActiveRightOfPassage = false;

	// p1.playerRelationships[p2].lastWithdrawalDemandTurn is the turn p1
	// last told p2 to take its units out of p1's territory, or -1 if never.
	public int lastWithdrawalDemandTurn = -1;

	// p1.playerRelationships[p2].recentWithdrawals is how many times in a
	// row p2 agreed to leave p1's territory and then came back before p1
	// forgot about it (see TerritoryDemands).
	public int recentWithdrawals = 0;

	// p1.playerRelationships[p2].loneTrespasserSinceTurn is the turn p2
	// started keeping a single combat unit in p1's territory, which p1
	// puts up with for loneTrespasserPatience turns, or -1 if it isn't.
	public int loneTrespasserSinceTurn = -1;
	public int loneTrespasserPatience = 0;

	// p1.playerRelationships[p2].otherStartedCurrentWar is true if p2
	// declared the war p1 and p2 are currently fighting, false if p1 did,
	// and null if unknown (no war since this was recorded, as in saves made
	// before it was, or wars that came from a scenario).
	public bool? otherStartedCurrentWar = null;

	// p1.playerRelationships[p2].warWearinessPoints is how weary p1's
	// people are of fighting p2 (see Player.UpdateWarWeariness). Below zero
	// it is war happiness. It lingers after peace, fading each turn.
	public int warWearinessPoints = 0;

	// p1.playerRelationships[p2].hasEmbassy is true if p1 has an embassy in
	// p2's capital, which diplomatic missions against p2 need.
	public bool hasEmbassy = false;

	// What p1's embassy reported of p2's capital when it was established,
	// or null if p1 has no embassy or it came from a scenario.
	public Espionage.CityReport embassyReport = null;

	// p1.playerRelationships[p2].hasSpy is true if p1 has planted a spy in
	// p2's capital (needs the Intelligence Agency), which espionage missions
	// against p2 need.
	public bool hasSpy = false;

	// p1.playerRelationships[p2].espionageIncidents is the number of p2's
	// diplomatic or espionage missions against p1 that were caught.
	public int espionageIncidents = 0;

	// A copy sharing its deals and report with this one, for a LAN host to
	// change a little for one guest (see C7Engine.Network.SnapshotFilter).
	internal PlayerRelationship ShallowCopy() {
		return (PlayerRelationship)MemberwiseClone();
	}

	public bool AtWar() {
		return multiTurnDeals.Count == 0;
	}

	/// <summary>
	/// Returns <b>true</b>, and the left's player relationship to the right player, if it exists.<br/>
	/// Otherwise returns <b>false</b>.<br/>
	/// If you want the opposite relationship, flip the arguments when calling the method.
	/// </summary>
	/// <param name="left"></param>
	/// <param name="right"></param>
	/// <param name="relationship"></param>
	/// <returns></returns>
	public static bool TryGetRelationship(Player left, Player right, out PlayerRelationship relationship) {
		relationship = null;

		if (left == null || right == null) return false;
		if (left.id == right.id) return false;
		if (left.isBarbarians || right.isBarbarians) return false;
		if (left.defeated || right.defeated) return false;
		if (!left.playerRelationships.TryGetValue(right.id, out var pr)) return false;

		relationship = pr;
		return true;
	}

	public static bool AtWar(Player left, Player right) {
		// only one is barbarians, but not both
		if (left.isBarbarians != right.isBarbarians) return true;

		if (TryGetRelationship(left, right, out var relationship) && relationship.AtWar()) {
			return true;
		}

		return false;
	}

	public static bool AtPeace(Player left, Player right) {
		return !AtWar(left, right);
	}

	/// <summary>
	/// Returns true if the player is at war with any of the other players/AI except the barbarians.
	/// </summary>
	/// <param name="player"></param>
	/// <param name="players"></param>
	/// <returns></returns>
	public static bool IsInAnyWar(Player player, List<Player> players) {
		if (players.Any(other => !other.isBarbarians && AtWar(player, other))) {
			return true;
		}

		return false;
	}

	public static bool HaveActiveRightOfPassage(Player left, Player right) {
		return TryGetRelationship(left, right, out var pr) &&
			   pr.multiTurnDeals.Any(d => d.dealSubType == DealSubType.RightOfPassage);
	}

	// Breaks peace and all other multiturn deals when war is declared.
	// refuseContactUntilTurn is the (absolute) turn until which the defender
	// refuses to talk; currentTurn defaults to the game's.
	public static void DeclareWar(Player aggressor, Player defender, bool sneakAttack, int refuseContactUntilTurn, int? currentTurn = null) {
		var defenderRelationshipToAggressor = defender.playerRelationships[aggressor.id];
		var aggressorRelationshipToDefender = aggressor.playerRelationships[defender.id];
		// increment the times the aggressor has declared war on the defender
		defenderRelationshipToAggressor.warDeclarationCount++;

		// increment the times the aggressor has declared war on the defender while there is an active RoP
		if (HaveActiveRightOfPassage(aggressor, defender)) {
			defenderRelationshipToAggressor.warDeclarationWithRoPActiveCount++;
		}

		// update whether the defender was sneak attacked
		defenderRelationshipToAggressor.wasSneakAttacked = sneakAttack;

		// record who started this war
		defenderRelationshipToAggressor.otherStartedCurrentWar = true;
		aggressorRelationshipToDefender.otherStartedCurrentWar = false;

		// Set for how many turns the defender will refuse contact from the aggressor
		defenderRelationshipToAggressor.refuseContactUntilTurn = refuseContactUntilTurn;

		// TODO: Figure out a better formula to calculate the aggressor's refusal in turns.
		// Right now the aggressor refuses contact for half as many turns as
		// the defender.
		//
		// The thinking is that if AI attacks a human, the human as a defender
		// might try to talk to the AI aggressor earlier than an AI in it's place is programmed to do.
		int turn = currentTurn ?? EngineStorage.gameData?.turn ?? 0;
		aggressorRelationshipToDefender.refuseContactUntilTurn = turn + Math.Max(0, refuseContactUntilTurn - turn) / 2;

		// Finally clear all multi-turn deals, including Peace, which is how we actually declare war
		aggressorRelationshipToDefender.multiTurnDeals = new List<MultiTurnDeal>();
		defenderRelationshipToAggressor.multiTurnDeals = new List<MultiTurnDeal>();

		log.Information("{Aggressor} declared war on {Defender}{SneakAttack}!" +
						" Defender is refusing contact for at least up to turn {RefuseContactUntilTurn}" +
						" ({Turns} turns)!",
						aggressor, defender, sneakAttack ? " in a sneak attack" : "",
						refuseContactUntilTurn, refuseContactUntilTurn - EngineStorage.gameData.turn);
	}

	public static void SignPeaceAfterWar(Player left, Player right, GameData gameData) {
		if (left.isBarbarians || left.defeated || right.isBarbarians || right.defeated || left.id == right.id)
			throw new Exception($"Can't sign peace between {left} and {right}");

		if (!AtWar(left, right))
			throw new Exception($"This is not the proper method to use if the two players are not at war");

		MultiTurnDeal mtd = new MultiTurnDeal(DealType.DiplomaticAgreement, DealSubType.Peace, DealDetails.Exchange,
			0, null, gameData.rules.DefaultDealDuration, gameData.turn, null);

		RegisterMultiTurnDeal(left, right, mtd);

		left.playerRelationships[right.id].refuseContactUntilTurn = -1;
		right.playerRelationships[left.id].refuseContactUntilTurn = -1;

		// the war is over, so nobody started a current one
		left.playerRelationships[right.id].otherStartedCurrentWar = null;
		right.playerRelationships[left.id].otherStartedCurrentWar = null;

		log.Information("{Left} signed a peace treaty with {Right}", left, right);
	}

	public static void RegisterMultiTurnDeal(Player left, Player right, MultiTurnDeal mtd) {
		if (mtd == null || mtd.dealDetails == DealDetails.None)
			throw new Exception("Not a valid deal");

		if (mtd.dealDetails == DealDetails.Exchange) {
			RegisterTwoWayDeal(left, right, mtd);
			return;
		}
		RegisterOneWayDeal(left, right, mtd);
	}

	private static void RegisterOneWayDeal(Player left, Player right, MultiTurnDeal mtd) {
		if (mtd.dealDetails == DealDetails.None || mtd.dealDetails == DealDetails.Exchange)
			throw new Exception("This is not a valid one way deal. Perhaps you intended to use RegisterTwoWayDeal() instead.");

		// add the deal for this player
		left.playerRelationships[right.id].multiTurnDeals.Add(mtd);

		// add the deal for the other player
		right.playerRelationships[left.id].multiTurnDeals.Add(new MultiTurnDeal(mtd.dealType, mtd.dealSubType,
			mtd.dealDetails == DealDetails.Inbound ? DealDetails.Outbound : DealDetails.Inbound,
			mtd.goldPerTurn, mtd.resourcePerTurn, mtd.dealDuration, mtd.turnStartDeal, mtd.againstPlayer));
	}

	private static void RegisterTwoWayDeal(Player left, Player right, MultiTurnDeal mtd) {
		if (mtd.dealDetails != DealDetails.Exchange)
			throw new Exception("This is not a valid two way deal. Perhaps you intended to use RegisterOneWayDeal() instead.");

		// add the deal for this player
		left.playerRelationships[right.id].multiTurnDeals.Add(mtd);

		// add the deal for the other player
		right.playerRelationships[left.id].multiTurnDeals.Add(mtd);
	}

	/// <summary>
	/// This ends all multi-turn deals except peace, when they go over the initial agreed upon duration.<br/>
	/// The multi-turn deal is cancelled for both parties, so neither keeps it until the other's turn.<br/>
	/// It also cancels the resource deals the player is part of that can no longer be kept (see CancelBrokenResourceDeals).
	/// </summary>
	/// <param name="player"></param>
	/// <param name="players"></param>
	/// <param name="currentTurn"></param>
	public static void CheckForObsoleteDeals(Player player, List<Player> players, int currentTurn) {
		log.Information("Checking to terminate any deals past their due duration for player {Player}", player);

		CancelBrokenResourceDeals(EngineStorage.gameData, player, players);

		// check player's relationship with the other players
		foreach (Player other in players) {
			// if the player doesn't have a relationship with the other civ, or they are at war exit
			if (TryGetRelationship(player, other, out var relationship) && !relationship.AtWar()) {
				// we don't want to cancel peace
				List<MultiTurnDeal> deadDeals = relationship.multiTurnDeals
					.Where(mtd => mtd != null
							  && mtd.dealSubType != DealSubType.Peace
							  && mtd.TurnsRemaining(currentTurn) <= 0)
					// Mutual protection pacts between locked allies never
					// expire (they are made without an end turn).
					.Where(mtd => !(mtd.dealSubType == DealSubType.MutualProtectionPact
									&& EngineStorage.gameData.AreInLockedPeace(player, other)))
					.ToList();

				foreach (MultiTurnDeal deadDeal in deadDeals) {
					// TODO: Add a popup to notify if an AI/Human deal expires
					// TODO: Add renegotiate logic (plus preferences option Always Renegotiate Deals)
					log.Information("Cancelling multi turn deal: {Player} -- {Other}", player, other);
					UnRegisterMultiTurnDeal(player, other, deadDeal);
				}
			}
		}
	}

	// Removes both players' copies of a deal. The other player's copy is
	// looked up before this player's is removed.
	private static void UnRegisterMultiTurnDeal(Player player, Player other, MultiTurnDeal mtd) {
		MultiTurnDeal counterpart = MultiTurnDeal.GetCounterpartDeal(player, other, mtd);
		player.playerRelationships[other.id].multiTurnDeals.Remove(mtd);
		if (counterpart != null) {
			other.playerRelationships[player.id].multiTurnDeals.Remove(counterpart);
		}
	}

	// As in Civ3, a deal to send a resource is cancelled when the exporter
	// no longer has enough of it on its capital's network, or when the two
	// capitals are no longer connected. When the exporter is short, its
	// newest deals for the resource go first. This cancels the broken deals
	// the player sends or receives, and tells the humans involved.
	public static void CancelBrokenResourceDeals(GameData gameData, Player player, List<Player> players) {
		List<(Player exporter, Player importer, MultiTurnDeal deal, Resource resource)> broken = BrokenResourceDeals(gameData, player, players);
		foreach (Player other in players) {
			if (other != player) {
				broken.AddRange(BrokenResourceDeals(gameData, other, players).Where(b => b.importer == player));
			}
		}

		foreach ((Player exporter, Player importer, MultiTurnDeal deal, Resource resource) in broken) {
			log.Information("Cancelling the {Resource} deal from {Exporter} to {Importer}", resource.Name, exporter, importer);
			UnRegisterMultiTurnDeal(exporter, importer, deal);
			NotifyOfCancelledDeal(exporter, $"Our deal to send {resource.Name} to the {importer.civilization?.noun} has been cancelled.");
			NotifyOfCancelledDeal(importer, $"Our deal to receive {resource.Name} from the {exporter.civilization?.noun} has been cancelled.");
		}
	}

	// The exporter's resource deals that can no longer be kept, as the
	// exporter's copies.
	private static List<(Player exporter, Player importer, MultiTurnDeal deal, Resource resource)> BrokenResourceDeals(
			GameData gameData, Player exporter, List<Player> players) {
		List<(Player, Player, MultiTurnDeal, Resource)> result = new();
		Dictionary<Resource, List<(Player importer, MultiTurnDeal deal)>> byResource = new();
		C7Engine.Pathing.TradeNetwork network = gameData.GetTradeNetwork();
		foreach (Player importer in players) {
			if (!TryGetRelationship(exporter, importer, out PlayerRelationship relationship) || relationship.AtWar()) {
				continue;
			}
			bool connected = network.CapitalsConnected(exporter, importer);
			foreach (MultiTurnDeal deal in relationship.multiTurnDeals) {
				if (deal == null || deal.dealDetails != DealDetails.Outbound || deal.resourcePerTurn == null
					|| deal.dealSubType is not (DealSubType.ResourcePerTurn or DealSubType.LuxuryPerTurn)) {
					continue;
				}
				Resource resource = gameData.Resources.Find(r => r.Key == deal.resourcePerTurn);
				if (resource == null) {
					continue;
				}
				if (!connected) {
					result.Add((exporter, importer, deal, resource));
					continue;
				}
				if (!byResource.TryGetValue(resource, out var deals)) {
					deals = new();
					byResource[resource] = deals;
				}
				deals.Add((importer, deal));
			}
		}

		foreach ((Resource resource, var deals) in byResource) {
			int excess = deals.Count - network.CapitalResourceCount(exporter, resource);
			if (excess > 0) {
				result.AddRange(deals
					.OrderByDescending(d => d.deal.turnStartDeal)
					.Take(excess)
					.Select(d => (exporter, d.importer, d.deal, resource)));
			}
		}
		return result;
	}

	// Tells a human that one of their deals was cancelled, over their
	// capital. Hotseat holds the message until it is their turn.
	private static void NotifyOfCancelledDeal(Player player, string message) {
		if (!player.isHuman) {
			return;
		}
		City city = player.cities.Find(c => c.IsCapital()) ?? player.cities.FirstOrDefault();
		if (city != null) {
			new MsgShowTemporaryPopup(message, city.location, player).send();
		}
	}
}

public class MultiTurnDeal {
	// Civ3's standard deal length, for a deal made without a game (and so
	// without its rules' DefaultDealDuration) to hand.
	private const int FallbackDealDuration = 20;

	// Passed for a deal's duration to mean the rules' default.
	public const int DefaultDuration = -1;
	public DealType dealType { get; private set; }
	public DealSubType dealSubType { get; private set; }
	public DealDetails dealDetails { get; private set; }
	public int goldPerTurn { get; private set; }
	public string resourcePerTurn { get; private set; }
	public int dealDuration { get; private set; }
	public int turnStartDeal { get; private set; }

	public int turnEndDeal { get; private set; }

	// only applicable for Military Alliances and Trade Embargoes
	public ID againstPlayer { get; private set; }

	public MultiTurnDeal(DealType dealType, DealSubType dealSubType, DealDetails dealDetails, int goldPerTurn = 0,
		string resourcePerTurn = null, int dealDuration = DefaultDuration, int turnStartDeal = 0, ID againstPlayer = null) {
		// Without a duration, deals last as long as the rules say. (The
		// parameter is an int, not int?, because loading a save builds deals
		// through this constructor, whose parameters must match the
		// properties' types.)
		int duration = dealDuration != DefaultDuration ? dealDuration
			: EngineStorage.gameData?.rules?.DefaultDealDuration ?? FallbackDealDuration;
		this.dealSubType = dealSubType;
		this.dealType = dealType;
		this.dealDetails = dealDetails;
		this.goldPerTurn = goldPerTurn;
		this.resourcePerTurn = resourcePerTurn;
		this.dealDuration = duration;
		this.turnStartDeal = turnStartDeal;
		// basically peace from the start, don't need to know when it ends
		if (dealSubType == DealSubType.Peace && turnStartDeal == 0)
			this.turnEndDeal = 0;
		else
			this.turnEndDeal = turnStartDeal + duration;
		this.againstPlayer = againstPlayer;
	}

	public int TurnsRemaining(int currentTurn) {
		return turnEndDeal - currentTurn <= 0 ? 0 : turnEndDeal - currentTurn;
	}

	// A multi turn peace deal that is from the start of the game without end.
	// Used for when a civ meets another civ to establish the initial peace.
	public static MultiTurnDeal DEFAULT_PEACE => new MultiTurnDeal(DealType.DiplomaticAgreement, DealSubType.Peace,
			DealDetails.Exchange, 0, null, 0, 0, null);

	public static MultiTurnDeal DEFAULT_MUTUAL_PROTECTION_PACT => new MultiTurnDeal(DealType.DiplomaticAgreement, DealSubType.MutualProtectionPact,
			DealDetails.Exchange, 0, null, 0, 0, null);

	/// <summary>
	/// Returns the counterpart to a deal.<br/><br/>
	/// The simplest example would be, if PlayerA gives wines to PlayerB,
	/// PlayerA has an Outbound deal in their multiTurnDeals info,
	/// whereas PlayerB has an Inbound deal, while the rest is the same.<br/><br/>
	/// This method, given PlayerA, PlayerB and the PlayerA's side of the deal (Outbound),
	/// returns the PlayerB's side of the deal (Inbound).<br/><br/>
	/// We could have this also the other way around, and provide
	/// PlayerB, PlayerA and PlayerB's side of the deal (Inbound),
	/// and retrieve PlayerA's side of the deal (Outbound).
	/// </summary>
	/// <param name="playerA"></param>
	/// <param name="playerB"></param>
	/// <param name="original"></param>
	/// <returns></returns>
	public static MultiTurnDeal GetCounterpartDeal(Player playerA, Player playerB, MultiTurnDeal original) {
		MultiTurnDeal mtd = null;
		if (PlayerRelationship.TryGetRelationship(playerB, playerA, out var relationship)) {
			DealDetails oppositeDetails = DealDetails.None;
			if (original.dealDetails == DealDetails.Exchange) {
				oppositeDetails = DealDetails.Exchange;
			} else {
				if (original.dealDetails == DealDetails.Inbound) {
					oppositeDetails = DealDetails.Outbound;
				} else {
					oppositeDetails = DealDetails.Inbound;
				}
			}

			mtd = relationship.multiTurnDeals.FirstOrDefault(d => {
				return
					d.dealType == original.dealType
					&& d.dealSubType == original.dealSubType
					&& d.dealDetails == oppositeDetails
					&& d.goldPerTurn == original.goldPerTurn
					&& d.resourcePerTurn == original.resourcePerTurn
					&& d.dealDuration == original.dealDuration
					&& d.turnStartDeal == original.turnStartDeal
					&& d.againstPlayer == original.againstPlayer;
			});

		}
		return mtd;
	}
}

// https://github.com/maxpetul/C3X/blob/064c8307c5085205c0dc8f2ee5b61ad2c2606523/Civ3Conquests.h#L1399
public enum DealType {
	DiplomaticAgreement,
	Alliance,
	Embargo,
	Map,
	Communication,
	Resource,
	Luxury,
	Gold,
	Technology,
	City,
	Unit,
	None,
}

public enum DealSubType {
	Peace,
	MutualProtectionPact,
	RightOfPassage,
	MilitaryAlliance,
	TradeEmbargo,
	GoldPerTurn,
	ResourcePerTurn,
	LuxuryPerTurn,
	None,
}

public enum DealDetails {
	Inbound,
	Outbound,
	Exchange,
	None,
}
