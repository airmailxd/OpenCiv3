using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7GameData;
using Serilog;
using static C7GameData.PlayerRelationship;

namespace C7Engine.AI {
	// How an AI weighs up making peace, and when it asks for it.
	//
	// What Civ3 players report of its AI, which this follows loosely:
	// - It won't talk at first: not until the other side has hurt it ("You
	//   defeat a few of their troops. You take a couple of cities. Atleast
	//   3-5 turns", Chieftess), and "the AI seems to respond to threat more
	//   than to losses" (rcoutme)
	//   (https://forums.civfanatics.com/threads/when-will-ai-sue-for-peace.85975/).
	//   The refusal to talk is PlayerRelationship.refuseContactUntilTurn.
	// - It gives up cities only in a peace treaty, never in ordinary trade:
	//   "You can now only 'buy' cities by demanding them in tribute as part
	//   of a peace treaty. You cannot sell your cities to the AI, for any
	//   price" (Padma), and "the only time I ever managed to get a city is
	//   when I am dominating them in a battle" (Virtual Alex)
	//   (https://forums.civfanatics.com/threads/trading-cities.177929/).
	//
	// Civ3's actual formulas aren't known, so everything below (the
	// strength ratios, the budgets, the chances) is OUR AI HEURISTIC, not a
	// Civ3 rule. Per the project owner, the AI gives cities no value in any
	// deal, never trades its cities away outside a peace treaty, and in a
	// peace treaty may give up cities when losing or demand them when
	// winning.
	public static class PeaceAI {
		private static ILogger log = Log.ForContext(typeof(PeaceAI));

		public enum WarOutlook {
			LosingBadly,
			Losing,
			Even,
			Winning,
			Dominating,
		}

		// HEURISTIC: how much stronger (by Player.CalculateMilitaryStrength,
		// the military advisor's measure) one side must be to be winning,
		// or dominating, the war.
		public const float WinningStrengthRatio = 1.5f;
		public const float DominatingStrengthRatio = 3f;

		// HEURISTIC: at these war weariness levels against the enemy (see
		// Player.WarWearinessLevel), the war looks one step worse again.
		public const int WearyLevel = 2;
		public const int VeryWearyLevel = 4;

		// HEURISTIC: what a winning AI wants for peace, in gold equivalent,
		// for each of the other side's cities.
		public const int PeaceTributePerCity = 30;

		// HEURISTIC: what a losing AI will give for peace, in gold
		// equivalent, for each of its own cities.
		public const int ConcessionPerCityWhenLosing = 25;
		public const int ConcessionPerCityWhenLosingBadly = 75;

		// HEURISTIC: an AI asks for peace on some turns, and not again for a
		// while after asking.
		public const int ProposalChancePercent = 50;
		public const int DemandChancePercent = 20;
		public const int ProposalCooldownTurns = 10;

		public static WarOutlook Assess(Player us, Player them) {
			float ours = us.CalculateMilitaryStrength();
			float theirs = Math.Max(1f, them.CalculateMilitaryStrength());
			float ratio = ours / theirs;
			WarOutlook outlook = ratio >= DominatingStrengthRatio ? WarOutlook.Dominating
				: ratio >= WinningStrengthRatio ? WarOutlook.Winning
				: ratio * WinningStrengthRatio > 1f ? WarOutlook.Even
				: ratio * DominatingStrengthRatio > 1f ? WarOutlook.Losing
				: WarOutlook.LosingBadly;

			// A people tired of the war push for peace.
			int weariness = Player.WarWearinessLevel(us.WarWearinessPointsAgainst(them));
			if (weariness >= WearyLevel && outlook > WarOutlook.LosingBadly) {
				--outlook;
			}
			if (weariness >= VeryWearyLevel && outlook > WarOutlook.LosingBadly) {
				--outlook;
			}
			return outlook;
		}

		// How much, in gold equivalent, we'd give for peace.
		public static int ConcessionBudget(Player us, WarOutlook outlook) {
			int perCity = outlook switch {
				WarOutlook.LosingBadly => ConcessionPerCityWhenLosingBadly,
				WarOutlook.Losing => ConcessionPerCityWhenLosing,
				_ => 0,
			};
			return perCity * Math.Max(1, us.cities.Count);
		}

		// How many of our cities we'd give up for peace.
		public static int MaxCitiesToCede(WarOutlook outlook) {
			return outlook == WarOutlook.LosingBadly ? 1 : 0;
		}

		// How much more than we give, in gold equivalent, we want for peace.
		public static int TributeDemanded(Player them, WarOutlook outlook) {
			return outlook >= WarOutlook.Winning ? PeaceTributePerCity * Math.Max(1, them.cities.Count) : 0;
		}

		// The cities a player could give away in a deal (see
		// TradeOffer.ProblemWithDeal): any but its capital, if it has more
		// than one.
		public static List<City> GivableCities(Player player) {
			return TradeOffer.TradableCities(player);
		}

		// The city a player would part with most easily: its smallest, and
		// of those, the furthest from its capital.
		private static City Backwater(Player player) {
			City capital = player.cities.Find(c => c.IsCapital());
			return GivableCities(player)
				.OrderBy(c => c.residents.Count)
				.ThenByDescending(c => capital == null ? 0 : c.location.DistanceTo(capital.location))
				.FirstOrDefault();
		}

		// Whether we, an AI at war with them, would make peace on these
		// terms, where they give theirOffer and we give ourOffer, worth the
		// given gold equivalents to us (cities count for nothing).
		public static bool WouldAcceptPeace(Player us, Player them, TradeOffer theirOffer, TradeOffer ourOffer,
				int theirValue, int ourValue) {
			WarOutlook outlook = Assess(us, them);
			if (ourOffer.cities.Count > MaxCitiesToCede(outlook)) {
				return false;
			}
			// Dominating, a city of theirs is our price, whatever else is in
			// the deal; failing that, tribute.
			if (outlook == WarOutlook.Dominating && theirOffer.cities.Count > 0) {
				return theirValue >= ourValue;
			}
			return theirValue - ourValue >= TributeDemanded(them, outlook) - ConcessionBudget(us, outlook);
		}

		// The peace we'd propose to them, as what we give and what we want,
		// or null if we wouldn't propose one: losing, we offer techs and
		// (losing badly) a city; dominating, we demand a city.
		public static (TradeOffer weGive, TradeOffer weWant)? BuildPeaceOffer(GameData gameData, Player us, Player them) {
			WarOutlook outlook = Assess(us, them);
			TradeOffer weGive = new() { partOfPeaceTreaty = true };
			TradeOffer weWant = new() { partOfPeaceTreaty = true };
			switch (outlook) {
				case WarOutlook.Losing:
				case WarOutlook.LosingBadly: {
						int budget = ConcessionBudget(us, outlook);
						List<Tech> techs = us.GetTechsTradableTo(them, gameData.techs);
						foreach (Tech t in techs.OrderByDescending(t => gameData.TechCostFor(t, us))) {
							int cost = gameData.TechCostFor(t, us);
							if (cost <= budget) {
								weGive.techs.Add(t);
								budget -= cost;
							}
						}
						if (MaxCitiesToCede(outlook) > 0 && Backwater(us) is City city) {
							weGive.cities.Add(city);
						}
						break;
					}
				case WarOutlook.Dominating: {
						if (Backwater(them) is not City city) {
							return null;
						}
						weWant.cities.Add(city);
						break;
					}
				default:
					return null;
			}
			return (weGive, weWant);
		}

		// Asks the civs we're at war with for peace, if the war is going
		// badly for us (or demands a city for it, if it's going very well).
		// A human is shown the offer and may accept it on the deal screen.
		public static async Task ProposePeace(Player us, GameData gameData) {
			if (us.isHuman || us.isBarbarians) {
				return;
			}
			foreach (Player them in gameData.players.ToList()) {
				if (them == us || them.isBarbarians || them.defeated
					|| !TryGetRelationship(us, them, out PlayerRelationship ourView) || !ourView.AtWar()
					|| gameData.AreInLockedWar(us, them)) {
					continue;
				}
				if (ourView.lastPeaceProposalTurn >= 0 && gameData.turn - ourView.lastPeaceProposalTurn < ProposalCooldownTurns) {
					continue;
				}
				// Both sides must be willing to talk.
				if (!us.WillAcceptCommunicationFrom(them, gameData.turn)
					|| (!them.isHuman && !them.WillAcceptCommunicationFrom(us, gameData.turn))) {
					continue;
				}
				(TradeOffer weGive, TradeOffer weWant)? offer = BuildPeaceOffer(gameData, us, them);
				if (offer == null) {
					continue;
				}
				(TradeOffer weGive, TradeOffer weWant) = offer.Value;
				int chance = weWant.cities.Count > 0 ? DemandChancePercent : ProposalChancePercent;
				if (GameData.rng.Next(100) >= chance) {
					continue;
				}
				if (TradeOffer.ProblemWithDeal(gameData, us, them, weGive, weWant) != null) {
					continue;
				}
				ourView.lastPeaceProposalTurn = gameData.turn;
				log.Information("{Us} proposes peace to {Them}, giving {Give} for {Want}", us, them, weGive, weWant);

				if (them.isHuman) {
					// A LAN guest who has left can't answer, so isn't asked.
					if (!EngineStorage.IsPlayerReachable(them.id)) {
						continue;
					}
					// The human receiving the offer takes the UI to respond.
					if (EngineStorage.uiFollowsActivePlayer) {
						EngineStorage.uiControllerID = them.id;
					}
					EngineStorage.diplomacyPlayerID = them.id;
					EngineStorage.diplomacyAIPlayerID = us.id;
					new MsgShowTradeOffer(us, them, weWant, weGive).send();
					await EngineStorage.WaitForDiplomacyCompleted(them.id);
					EngineStorage.diplomacyPlayerID = null;
					EngineStorage.diplomacyAIPlayerID = null;
				} else if (them.WouldAcceptDealFrom(gameData, us, weGive, weWant)) {
					us.ExecuteDeal(gameData, them, weWant, weGive);
				}
			}
		}
	}
}
