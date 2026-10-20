
using System;
using System.Collections.Generic;
using System.Linq;

namespace C7GameData {
	// A class representing one side of a diplomatic agreement between two civs.
	public class TradeOffer {
		// True if two players involved in the agreement were at war prior to
		// the agreement.
		public bool partOfPeaceTreaty = false;

		// A right of passage is mutual (see RightOfPassage), so like a peace
		// treaty it is on the table if either side's offer has it.
		public bool rightOfPassage = false;

		public static bool IncludesRightOfPassage(TradeOffer a, TradeOffer b) {
			return (a?.rightOfPassage ?? false) || (b?.rightOfPassage ?? false);
		}

		// The pacts (see Pacts) are mutual too, so on the table if either
		// side's offer has them. A military alliance or trade embargo is
		// against the civ with that id.
		public bool mutualProtectionPact = false;
		public ID militaryAllianceAgainst = null;
		public ID tradeEmbargoAgainst = null;

		public static bool IncludesMutualProtectionPact(TradeOffer a, TradeOffer b) {
			return (a?.mutualProtectionPact ?? false) || (b?.mutualProtectionPact ?? false);
		}

		public static ID AllianceTarget(TradeOffer a, TradeOffer b) {
			return a?.militaryAllianceAgainst ?? b?.militaryAllianceAgainst;
		}

		public static ID EmbargoTarget(TradeOffer a, TradeOffer b) {
			return a?.tradeEmbargoAgainst ?? b?.tradeEmbargoAgainst;
		}

		// Whether the deal has a pact, which, like things given over time,
		// counts toward trading reputation (see
		// PlayerRelationship.BreaksReputation).
		public static bool IncludesAllianceOrPact(TradeOffer a, TradeOffer b) {
			return IncludesMutualProtectionPact(a, b) || AllianceTarget(a, b) != null;
		}

		public int? gold = null;
		public List<Tech> techs = new();

		// Gold paid each turn for as long as the deal lasts (the rules'
		// DefaultDealDuration, 20 turns in Civ3).
		public int goldPerTurn = 0;

		// The luxuries and strategic resources (by Resource.Key) sent each
		// turn for as long as the deal lasts.
		public List<string> resources = new();

		// Whether this side gives anything over time, which an AI won't take
		// from a civ that broke such a deal (see
		// PlayerRelationship.TradingReputationBroken).
		public bool GivesOverTime() {
			return goldPerTurn > 0 || (resources?.Count ?? 0) > 0;
		}

		// Cities change hands peacefully, without resistance (see
		// CityInteractions.TransferCity with viaDeal).
		public List<City> cities = new();

		// Civ3's "Maps" (World Map, Territory Map) and "Communications"
		// ("with the ...") deal items (the deal screen's headings in
		// Conquests' Text/diplomacy.txt). The world map is every tile the
		// giver has explored; the territory map only those under its
		// cultural influence (Civilopedia, GCON_Maps). Contacts are
		// introductions to the third civs with these ids (GCON_Agreements:
		// "introductions to a third civilization you can grant or receive
		// from a rival").
		public bool worldMap = false;
		public bool territoryMap = false;
		public List<ID> contacts = new();

		// Whether this side of a deal gives nothing at all.
		public bool IsEmpty() {
			return !partOfPeaceTreaty && !rightOfPassage && !mutualProtectionPact && militaryAllianceAgainst == null
				&& tradeEmbargoAgainst == null && (gold ?? 0) == 0 && techs.Count == 0 && cities.Count == 0
				&& !GivesOverTime() && !worldMap && !territoryMap && (contacts?.Count ?? 0) == 0;
		}

		// The cities a player may give away in a deal: any but its capital,
		// and only while it has another city to keep.
		public static List<City> TradableCities(Player player) {
			if (player == null || player.cities.Count < 2) {
				return new List<City>();
			}
			return player.cities.Where(c => !c.IsCapital()).ToList();
		}

		// Calculate how much this trade offer is worth for a given player. This
		// has to be per-player because tech costs vary based on how many civs
		// a player have already researched the tech.
		//
		// Cities are worth nothing here: per the project owner, the AI (whose
		// view of a deal this is) never values a city, either way. Civ3's AI
		// "doesn't evaluate city trades very well", which is why a patch took
		// city trading out of ordinary deals
		// (https://forums.civfanatics.com/threads/trading-cities.177929/).
		//
		// Things given over time are worth what they give over the deal's
		// length: gold per turn is goldPerTurn for each of its turns, and a
		// resource is worth what p needs it for (see ResourceWorth). given
		// is true when p gives this side, false when p is given it, which
		// matters only for resources.
		//
		// counterpart is the other side of the deal: the one giving this side
		// to p, or receiving it from p. Maps are worth nothing without it.
		public int GoldEquivalentFor(GameData gameData, Player p, bool given = false, Player counterpart = null) {
			int result = 0;
			if (gold.HasValue) {
				result += gold.Value;
			}
			// An AI prices techs traded with a human at the human's research
			// cost: the trade price "is dependant on the human player's
			// research cost", which "on monarch and higher" is the Regent
			// cost "divided by the AI's cost factor" (TheNiceOne, "AI-human
			// trade revisited", Civ3 1.29,
			// https://forums.civfanatics.com/threads/ai-human-trade-revisited.42110/;
			// GameData.TechCostFor already divides a human's cost so). The
			// AI's markup goes on top (see C7Engine.AI.TradeMarkup).
			Player techPricer = !p.isHuman && counterpart != null && counterpart.isHuman ? counterpart : p;
			foreach (Tech t in techs) {
				result += gameData.TechCostFor(t, techPricer);
			}
			int turns = DealDuration(gameData);
			result += Math.Max(0, goldPerTurn) * turns;
			foreach (string key in resources ?? new()) {
				Resource r = gameData.Resources.Find(x => x.Key == key);
				if (r != null) {
					result += ResourceWorth(gameData, p, r, given, counterpart) * turns;
				}
			}
			if (counterpart != null) {
				Player giver = given ? p : counterpart;
				Player receiver = given ? counterpart : p;
				if (worldMap) {
					result += MapWorth(gameData, giver, receiver, territoryOnly: false);
				} else if (territoryMap) {
					result += MapWorth(gameData, giver, receiver, territoryOnly: true);
				}
			}
			result += (contacts?.Count ?? 0) * (given ? ContactGivenGold : ContactGold);

			return result;
		}

		// HEURISTIC (no Civ3 source gives the AI's prices): a map is worth a
		// gold for each MapTilesPerGold tiles it shows the receiver that it
		// hasn't seen, to either side; an introduction to a civ is worth
		// ContactGold to the civ introduced, and costs the introducer
		// ContactGivenGold.
		public const int MapTilesPerGold = 10;
		public const int ContactGold = 30;
		public const int ContactGivenGold = 10;

		public static int MapWorth(GameData gameData, Player giver, Player receiver, bool territoryOnly) {
			return MapTiles(giver, receiver, territoryOnly).Count() / MapTilesPerGold;
		}

		// The tiles the giver's map shows that the receiver doesn't know.
		private static IEnumerable<Tile> MapTiles(Player giver, Player receiver, bool territoryOnly) {
			if (giver?.tileKnowledge == null || receiver?.tileKnowledge == null) {
				return Enumerable.Empty<Tile>();
			}
			return giver.tileKnowledge.knownTiles.Where(t => !receiver.tileKnowledge.isTileKnown(t)
				&& (!territoryOnly || t.OwningPlayer() == giver));
		}

		// Shows the receiver the giver's world or territory map. The tiles
		// become known as they are now, as with Player.RevealWholeMap.
		// UNVERIFIED: whether Civ3 shows them as the giver last saw them.
		public static void GiveMap(Player giver, Player receiver, bool territoryOnly) {
			List<Tile> tiles = MapTiles(giver, receiver, territoryOnly).ToList();
			foreach (Tile t in tiles) {
				receiver.tileKnowledge.AddTileToKnown(t);
			}
			if (tiles.Count > 0) {
				receiver.tileKnowledge.RecomputeActiveTiles();
			}
		}

		// Why the giver can't give the receiver its maps, or null. Maps can
		// be traded "after your civilization discovers Navigation"
		// (Civilopedia, GCON_Maps), the BIQ's EnablesMapTrading flag.
		// UNVERIFIED (no Civ3 source found): that the receiver must know it
		// too, as for the pacts (see Pacts), and that the territory map
		// needs the same tech.
		public static string ProblemTradingMaps(GameData gameData, Player giver, Player receiver) {
			if (!Pacts.KnowsTech(gameData, giver, Save.SaveTech.Flag.EnablesMapTrading)
				|| !Pacts.KnowsTech(gameData, receiver, Save.SaveTech.Flag.EnablesMapTrading)) {
				return "trading maps needs a tech both civs must know";
			}
			return null;
		}

		// The civs the giver can introduce the receiver to: ones the giver
		// has met and the receiver hasn't. Contacts are traded "through your
		// Embassies" (Civilopedia, GCON_Agreements; either civ's will do, as
		// for the agreements, see Pacts) once both know the tech that
		// "Can now trade communications between civilizations" (Printing
		// Press, the BIQ's EnablesCommunicationTrading flag). UNVERIFIED (no
		// Civ3 source found): that the receiver must know the tech too.
		public static List<Player> TradableContacts(GameData gameData, Player giver, Player receiver) {
			if (giver == null || receiver == null || !Pacts.HasEmbassyEitherWay(giver, receiver)
				|| !Pacts.KnowsTech(gameData, giver, Save.SaveTech.Flag.EnablesCommunicationTrading)
				|| !Pacts.KnowsTech(gameData, receiver, Save.SaveTech.Flag.EnablesCommunicationTrading)) {
				return new List<Player>();
			}
			return gameData.players.Where(third => third != giver && third != receiver && !third.isBarbarians && !third.defeated
				&& PlayerRelationship.TryGetRelationship(giver, third, out _) && PlayerRelationship.TryGetRelationship(third, giver, out _)
				&& !receiver.playerRelationships.ContainsKey(third.id) && !third.playerRelationships.ContainsKey(receiver.id))
				.ToList();
		}

		// How long deals made now last: the rules' DefaultDealDuration, 20
		// turns in Civ3 ("The basic Military Alliance lasts for 20 turns",
		// https://forums.civfanatics.com/threads/how-to-form-a-strategic-alliance.154837/;
		// "20-turn deal", https://civfanatics.com/civ3/faq/).
		public static int DealDuration(GameData gameData) {
			int duration = gameData?.rules?.DefaultDealDuration ?? 0;
			return duration > 0 ? duration : 20;
		}

		// HEURISTIC (no Civ3 source gives the AI's prices): what a resource
		// is worth to p each turn of a deal, in gold.
		// - Given to p: a luxury it lacks moves a face toward happy in every
		//   one of its cities (City's mood code), as a gold of luxury
		//   spending does in one city, so it's worth LuxuryGold plus
		//   LuxuryGoldPerCity for each city. A strategic resource it lacks
		//   is worth StrategicNeededGold (plus a little for each of its
		//   cities) if p knows how to build a unit that needs it, and
		//   StrategicSpareGold if not. One it already has is worth nothing,
		//   as more of the same luxury makes nobody happier.
		// - Given by p: only spare copies can be given (ProblemGiving), so p
		//   loses nothing it uses, but it asks SurplusGold a turn for one,
		//   and more for a strategic resource, which arms the other civ. For
		//   a luxury it asks LuxuryAskPercent of what it's worth to the
		//   receiver (when it knows who that is): the receiver's every city
		//   is happier for it, and the AI shouldn't sell that for a gold.
		public const int LuxuryGold = 2;
		public const int LuxuryGoldPerCity = 1;
		public const int LuxuryAskPercent = 75;
		public const int StrategicNeededGold = 3;
		public const int StrategicSpareGold = 1;
		public const int CitiesPerExtraGold = 2;
		public const int SurplusGold = 1;
		public const int StrategicSurplusGold = 2;

		public static int ResourceWorth(GameData gameData, Player p, Resource r, bool given, Player counterpart = null) {
			if (given) {
				if (r.Category == ResourceCategory.STRATEGIC) {
					return StrategicSurplusGold;
				}
				if (r.Category == ResourceCategory.LUXURY && counterpart != null) {
					int toThem = ResourceWorth(gameData, counterpart, r, given: false);
					return Math.Max(SurplusGold, toThem * LuxuryAskPercent / 100);
				}
				return SurplusGold;
			}
			if (TradeReport.CapitalResources(gameData, p).ContainsKey(r)) {
				return 0;
			}
			if (r.Category == ResourceCategory.LUXURY) {
				return LuxuryGold + LuxuryGoldPerCity * p.cities.Count;
			}
			int perCity = p.cities.Count / CitiesPerExtraGold;
			bool needed = gameData.unitPrototypes.Any(u => u.requiredResources.Contains(r) && p.HasRequiredTechnology(u));
			return needed ? StrategicNeededGold + perCity : StrategicSpareGold;
		}

		// The most gold per turn the player can promise: its income plus a
		// tenth of its treasury, "10(Income)+Treasury = Maximum GPT / 10"
		// (https://forums.civfanatics.com/threads/gold-per-turn-who-knows-the-formula.139584/,
		// found by observation rather than from the game's code). Income
		// already takes out what it pays in other deals.
		public static int MaxGoldPerTurn(Player player) {
			return Math.Max(0, player.CalculateGoldPerTurn() + Math.Max(0, player.gold) / 10);
		}

		// Whether an AI can pay goldPerTurn more each turn without its
		// income going negative. Per the owner's decision, 2026-10-20, an AI
		// never agrees to or proposes a deal that would put it in deficit,
		// whatever MaxGoldPerTurn allows. Income already takes out what it
		// pays in other deals.
		public static bool AffordsGoldPerTurn(Player ai, int goldPerTurn) {
			return goldPerTurn <= 0 || ai.CalculateGoldPerTurn() - goldPerTurn >= 0;
		}

		// The resources the giver can send the receiver: those it has more
		// of on its capital's network than the one it needs and the copies
		// it already sends, "Wines (2 extra)" (https://civfanatics.com/civ3/faq/),
		// that the receiver knows of.
		public static List<Resource> TradableResources(GameData gameData, Player giver, Player receiver) {
			Dictionary<Resource, int> sent = new();
			foreach (ResourceDeal deal in TradeReport.ResourceDeals(gameData, giver, DealDetails.Outbound)) {
				sent[deal.resource] = sent.GetValueOrDefault(deal.resource) + 1;
			}
			return TradeReport.LocalResources(gameData, giver)
				.Where(r => r.Value - 1 - sent.GetValueOrDefault(r.Key) > 0 && receiver.KnowsAboutResource(r.Key))
				.Select(r => r.Key)
				.OrderBy(r => r.Category).ThenBy(r => r.Name)
				.ToList();
		}

		// Why the proposer and opponent can't make this deal, in which the
		// proposer gives proposerGives for proposerWants, or null if they
		// can. Offers may come from another machine, so nothing in them is
		// trusted: each side must have the gold and techs it gives, the
		// other side must be able to use the techs, the two must have met,
		// and while at war they can only make peace (and peace only then).
		//
		// Civ3 sources for these rules:
		// - Techs go only to a civ that has their prerequisites: "they can
		//   be traded with any other civilization, as long as that civ has
		//   the prerequisite technologies"
		//   (https://forums.civfanatics.com/threads/revolutionizing-the-tech-tree-mechanics.102803/).
		// - A peace treaty may come with gold and techs (but not resources)
		//   (https://forums.civfanatics.com/threads/a-question-about-peace-treaties.400809/).
		// - Civs in alliances a scenario locks at war "will _always_ remain
		//   at war" with each other, so can't make peace
		//   (https://forums.civfanatics.com/threads/how-do-i-add-alliances-in-editor.188963/).
		// - UNVERIFIED (no Civ3 source found): that while at war nothing can
		//   be traded without a peace treaty. The deal screen already
		//   offered only a peace treaty at war before this check existed.
		// - UNVERIFIED (no Civ3 source found): that the two must have met.
		//   Only civs met can be picked for diplomacy.
		// - Cities may be traded in any deal, at peace or in a peace treaty,
		//   per the project owner. (Civ3 patched them out of everything but
		//   peace treaties: "You can now only 'buy' cities by demanding them
		//   in tribute as part of a peace treaty",
		//   https://forums.civfanatics.com/threads/trading-cities.177929/.)
		//   A civ can't give away its capital or its last city.
		// - Gold per turn: no more than MaxGoldPerTurn. It may come with a
		//   peace treaty: reputation can be mended by "renegotiating peace
		//   treaties with gpt deals included"
		//   (https://civfanatics.com/civ3/strategy/game-mechanics/trading-reputation/).
		// - Resources: only spare copies, "Wines (2 extra)"
		//   (https://civfanatics.com/civ3/faq/), and only between civs whose
		//   capitals are connected: "Grayed-out resources indicate missing
		//   trade connections or routes" (same page; see
		//   TradeNetwork.CapitalsConnected). Not with a peace treaty (above).
		public static string ProblemWithDeal(GameData gameData, Player proposer, Player opponent,
			TradeOffer proposerGives, TradeOffer proposerWants) {
			if (proposer == null || opponent == null || proposerGives == null || proposerWants == null) {
				return "the deal is incomplete";
			}
			if (proposer == opponent) {
				return "a player can't trade with themselves";
			}
			if (gameData.GetPlayer(proposer.id) != proposer || gameData.GetPlayer(opponent.id) != opponent) {
				return "a player isn't in the game";
			}
			if (proposer.isBarbarians || opponent.isBarbarians || proposer.defeated || opponent.defeated) {
				return "barbarians and defeated players can't trade";
			}
			if (!PlayerRelationship.TryGetRelationship(proposer, opponent, out _)
				|| !PlayerRelationship.TryGetRelationship(opponent, proposer, out _)) {
				return "the players haven't met";
			}

			bool peace = proposerGives.partOfPeaceTreaty || proposerWants.partOfPeaceTreaty;
			bool atWar = PlayerRelationship.AtWar(proposer, opponent);
			if (peace && !atWar) {
				return "the players aren't at war, so can't make peace";
			}
			if (atWar && !peace) {
				return "the players are at war, and can only make peace";
			}
			if (peace && gameData.AreInLockedWar(proposer, opponent)) {
				return "the players' alliances are at war";
			}
			// UNVERIFIED (no Civ3 source found): that a peace treaty can't
			// come with a right of passage. A right of passage needs peace
			// (RightOfPassage.ProblemSigning), which the treaty only makes.
			if (IncludesRightOfPassage(proposerGives, proposerWants)) {
				if (peace) {
					return "a right of passage can't be part of a peace treaty";
				}
				string problem = RightOfPassage.ProblemSigning(gameData, proposer, opponent);
				if (problem != null) {
					return problem;
				}
			}

			string pactProblem = ProblemWithPacts(gameData, proposer, opponent, proposerGives, proposerWants, peace);
			if (pactProblem != null) {
				return pactProblem;
			}

			// Civs under a trade embargo don't trade (see Pacts).
			if ((proposerGives.GivesOverTime() || proposerWants.GivesOverTime())
				&& (Pacts.Embargoes(proposer, opponent) || Pacts.Embargoes(opponent, proposer))) {
				return "one of the civs has an embargo against the other";
			}

			return proposerGives.ProblemGiving(gameData, proposer, opponent, peace)
				?? proposerWants.ProblemGiving(gameData, opponent, proposer, peace);
		}

		// Why the pacts in the deal can't be made (see Pacts), or null.
		// UNVERIFIED (no Civ3 source found): that none can be part of a peace
		// treaty, like a right of passage (above).
		private static string ProblemWithPacts(GameData gameData, Player proposer, Player opponent,
				TradeOffer gives, TradeOffer wants, bool peace) {
			bool pact = IncludesMutualProtectionPact(gives, wants);
			ID allianceTarget = AllianceTarget(gives, wants);
			ID embargoTarget = EmbargoTarget(gives, wants);
			if (!pact && allianceTarget == null && embargoTarget == null) {
				return null;
			}
			if (peace) {
				return "an agreement can't be part of a peace treaty";
			}
			if ((gives.militaryAllianceAgainst != null && wants.militaryAllianceAgainst != null
					&& gives.militaryAllianceAgainst != wants.militaryAllianceAgainst)
				|| (gives.tradeEmbargoAgainst != null && wants.tradeEmbargoAgainst != null
					&& gives.tradeEmbargoAgainst != wants.tradeEmbargoAgainst)) {
				return "the two sides name different civs";
			}
			if (pact) {
				string problem = Pacts.ProblemSigningMutualProtectionPact(gameData, proposer, opponent);
				if (problem != null) {
					return problem;
				}
			}
			if (allianceTarget != null) {
				string problem = Pacts.ProblemSigningMilitaryAlliance(gameData, proposer, opponent, gameData.GetPlayer(allianceTarget));
				if (problem != null) {
					return problem;
				}
			}
			if (embargoTarget != null) {
				string problem = Pacts.ProblemSigningTradeEmbargo(gameData, proposer, opponent, gameData.GetPlayer(embargoTarget));
				if (problem != null) {
					return problem;
				}
			}
			return null;
		}

		// Why the giver can't give this side of a deal to the receiver, or
		// null if they can.
		private string ProblemGiving(GameData gameData, Player giver, Player receiver, bool peace) {
			if (gold.HasValue && gold.Value < 0) {
				return $"{giver} can't give negative gold";
			}
			if (gold.HasValue && gold.Value > giver.gold) {
				return $"{giver} doesn't have {gold.Value} gold";
			}
			if (techs == null || cities == null || resources == null || contacts == null) {
				return "the techs, cities, resources or contacts are missing";
			}
			// UNVERIFIED (no Civ3 source found): that maps and contacts
			// can't come with a peace treaty, which is known to take gold
			// and techs (above).
			if ((worldMap || territoryMap || contacts.Count > 0) && peace) {
				return "maps and contacts can't be part of a peace treaty";
			}
			if (worldMap || territoryMap) {
				string problem = ProblemTradingMaps(gameData, giver, receiver);
				if (problem != null) {
					return problem;
				}
			}
			if (contacts.Count > 0) {
				List<Player> tradable = TradableContacts(gameData, giver, receiver);
				HashSet<ID> offered = new();
				foreach (ID third in contacts) {
					if (third == null || !offered.Add(third)) {
						return "a contact is missing or offered twice";
					}
					if (!tradable.Exists(p => p.id == third)) {
						return $"{giver} can't introduce {receiver} to {third}";
					}
				}
			}
			if (goldPerTurn < 0) {
				return $"{giver} can't give negative gold per turn";
			}
			if (goldPerTurn > 0 && goldPerTurn > MaxGoldPerTurn(giver)) {
				return $"{giver} can't pay {goldPerTurn} gold per turn";
			}
			if (resources.Count > 0) {
				if (peace) {
					return "resources can't be part of a peace treaty";
				}
				List<Resource> tradable = TradableResources(gameData, giver, receiver);
				HashSet<string> offered = new();
				foreach (string key in resources) {
					if (key == null || !offered.Add(key)) {
						return "a resource is missing or offered twice";
					}
					if (!tradable.Exists(r => r.Key == key)) {
						return $"{giver} has no {key} to spare for {receiver}";
					}
				}
				if (!gameData.GetTradeNetwork().CapitalsConnected(giver, receiver)) {
					return $"{giver}'s capital isn't connected to {receiver}'s";
				}
			}
			if (cities.Count > 0) {
				HashSet<City> given = new();
				foreach (City c in cities) {
					if (c == null || !given.Add(c)) {
						return "a city is missing or offered twice";
					}
					if (c.owner != giver || !giver.cities.Contains(c)) {
						return $"{giver} doesn't own {c.name}";
					}
					if (c.IsCapital()) {
						return $"{giver} can't give away their capital";
					}
				}
				if (cities.Count >= giver.cities.Count) {
					return $"{giver} can't give away their last city";
				}
			}
			if (techs.Count > 0) {
				HashSet<ID> tradable = giver.GetTechsTradableTo(receiver, gameData.techs).Select(t => t.id).ToHashSet();
				HashSet<ID> offered = new();
				foreach (Tech t in techs) {
					if (t == null || !offered.Add(t.id)) {
						return "a tech is missing or offered twice";
					}
					// The giver knows it, and the receiver doesn't yet but
					// knows what it takes to use it.
					if (!tradable.Contains(t.id)) {
						return $"{giver} can't give {t.Name} to {receiver}";
					}
				}
			}
			return null;
		}

		public void Clear() {
			partOfPeaceTreaty = false;
			rightOfPassage = false;
			gold = null;
			techs.Clear();
			cities.Clear();
			goldPerTurn = 0;
			resources.Clear();
			mutualProtectionPact = false;
			militaryAllianceAgainst = null;
			tradeEmbargoAgainst = null;
			worldMap = false;
			territoryMap = false;
			contacts.Clear();
		}

		private static string Noun(ID id) {
			Player p = C7Engine.EngineStorage.gameData?.GetPlayer(id);
			return p?.civilization?.noun ?? id.ToString();
		}

		public override string ToString() {
			List<string> pieces = new();
			if (partOfPeaceTreaty) {
				pieces.Add("peace treaty");
			}
			if (rightOfPassage) {
				pieces.Add("right of passage");
			}
			if (mutualProtectionPact) {
				pieces.Add("mutual protection pact");
			}
			if (militaryAllianceAgainst != null) {
				pieces.Add($"military alliance against {Noun(militaryAllianceAgainst)}");
			}
			if (tradeEmbargoAgainst != null) {
				pieces.Add($"trade embargo against {Noun(tradeEmbargoAgainst)}");
			}
			if (gold != null) {
				pieces.Add($"{gold.Value} gold");
			}
			if (goldPerTurn > 0) {
				pieces.Add($"{goldPerTurn} gold per turn");
			}
			foreach (string r in resources) {
				pieces.Add(r);
			}
			foreach (Tech t in techs) {
				pieces.Add(t.Name);
			}
			foreach (City c in cities) {
				pieces.Add(c.name);
			}
			if (worldMap) {
				pieces.Add("world map");
			}
			if (territoryMap) {
				pieces.Add("territory map");
			}
			foreach (ID c in contacts ?? new()) {
				pieces.Add($"contact with the {Noun(c)}");
			}
			return string.Join(",", pieces);
		}
	}
}
